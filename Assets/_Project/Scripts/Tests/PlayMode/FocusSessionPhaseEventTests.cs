using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 집중 세션 <b>구간 전이 이벤트</b>의 동작 잠금 —
    /// <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 8-4절 / 9-3절 «한 세션에 정확히 2회».
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    ///  ① 한 세션에 <b>정확히 2회</b>: 적응기→몰입기, 몰입기→한계. 그 순서로.
    ///  ② <b>세션이 끝나는 세 경로 전부</b>에서 구간이 <c>None</c>으로 닫힌다
    ///     (완주 / <c>StopFocusSession</c> / 긴급정지). 이 저장소는 세 번째 경로를 빠뜨린 전력이 있다.
    ///  ③ 세션 시작·종료는 <b>이벤트를 쏘지 않는다</b> — 「구간 전이」가 아니라 「세션 경계」다.
    ///
    /// <para>★ 예산은 전부 <b>벽시계(초)</b>다(CLAUDE.md). 배치모드 PlayMode는 2,000fps 이상으로
    /// 돌아서 프레임 수 예산은 실제로 0.01초일 수 있다. 대신 <see cref="Time.timeScale"/>을 올려
    /// <b>세션 시간</b>을 압축한다 — <c>Time.deltaTime</c>은 timeScale이 곱해진 값이고 이 프로젝트의
    /// <c>Maximum Allowed Timestep</c>(0.33333334초)보다 훨씬 작게 유지되므로 구간을 건너뛰지 않는다.</para>
    /// </summary>
    public sealed class FocusSessionPhaseEventTests
    {
        private const string LogPrefix = "[집중구간-EVENT]";

        /// <summary>세션 시간 압축 배율. 60초 세션이 벽시계 약 3~8초에 끝난다.</summary>
        private const float TimeCompression = 12f;

        /// <summary>60초 세션 하나가 끝나기를 기다리는 <b>벽시계</b> 상한(초).
        /// 압축 후 기대 소요는 5초 안팎이라 넉넉히 잡는다.</summary>
        private const float SessionWallClockBudget = 40f;

        /// <summary>이 테스트가 쓰는 세션 길이(분) — 디렉터 하한(60초)에 걸려 실제로는 60초다.
        /// 60초 세션의 구간은 24 / 12 / 24초라 두 경계가 모두 이 예산 안에 들어온다.</summary>
        private const float SessionMinutes = 1f;

        private FocusWatchDirector _director;
        private readonly List<(FocusSessionPhase From, FocusSessionPhase To)> _events =
            new List<(FocusSessionPhase, FocusSessionPhase)>();
        private float _savedTimeScale;

        private void OnPhaseChanged(FocusSessionPhase from, FocusSessionPhase to) => _events.Add((from, to));

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _director = Object.FindFirstObjectByType<FocusWatchDirector>();
            Assert.IsNotNull(_director, $"{LogPrefix} 씬에 FocusWatchDirector가 없습니다.");
            if (_director.IsSessionActive) _director.StopFocusSession();
            yield return null;

            _savedTimeScale = Time.timeScale;
            Time.timeScale = TimeCompression;

            _events.Clear();
            StickmanEventBus.FocusSessionPhaseChanged += OnPhaseChanged;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StickmanEventBus.FocusSessionPhaseChanged -= OnPhaseChanged;
            if (_savedTimeScale > 0f) Time.timeScale = _savedTimeScale;
            if (_director != null && _director.IsSessionActive) _director.StopFocusSession();
            _director = null;
            _events.Clear();
            yield return null;
        }

        /// <summary>구간이 <paramref name="target"/>이 될 때까지 <b>벽시계</b> 예산 안에서 기다린다.</summary>
        private IEnumerator WaitForPhase(FocusSessionPhase target, float budgetSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (_director.CurrentPhase != target)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, budgetSeconds,
                    $"{LogPrefix} {budgetSeconds}초(벽시계) 안에 {target} 구간에 도달하지 못했습니다 " +
                    $"(지금 {_director.CurrentPhase}, 남은 {_director.RemainingSeconds:F1}초).");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator 한_세션에_구간_이벤트가_정확히_2회_온다()
        {
            yield return LoadScene();

            _director.StartFocusSession(SessionMinutes);
            Assert.AreEqual(FocusSessionPhase.Adapt, _director.CurrentPhase,
                $"{LogPrefix} 세션 시작 직후는 적응기여야 합니다.");
            Assert.AreEqual(0, _events.Count,
                $"{LogPrefix} 세션 시작은 「구간 전이」가 아닙니다 — 이벤트가 오면 안 됩니다.");

            float t0 = Time.realtimeSinceStartup;
            while (_director.IsSessionActive)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, SessionWallClockBudget,
                    $"{LogPrefix} 세션이 {SessionWallClockBudget}초(벽시계) 안에 끝나지 않았습니다.");
                yield return null;
            }

            Assert.AreEqual(2, _events.Count,
                $"{LogPrefix} 구간 이벤트는 한 세션에 정확히 2회입니다(실제 {_events.Count}회: " +
                $"{string.Join(", ", _events)}).");
            Assert.AreEqual((FocusSessionPhase.Adapt, FocusSessionPhase.Immersion), _events[0],
                $"{LogPrefix} 첫 전이는 적응기→몰입기여야 합니다.");
            Assert.AreEqual((FocusSessionPhase.Immersion, FocusSessionPhase.Limit), _events[1],
                $"{LogPrefix} 둘째 전이는 몰입기→한계여야 합니다.");
            Assert.AreEqual(FocusSessionPhase.None, _director.CurrentPhase,
                $"{LogPrefix} 완주 뒤에는 구간이 없어야 합니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 완주 경로에서 이벤트 2회 + 종료 후 None.");
        }

        [UnityTest]
        public IEnumerator 중도취소로_끝나도_구간이_닫힌다()
        {
            yield return LoadScene();
            _director.StartFocusSession(SessionMinutes);
            yield return WaitForPhase(FocusSessionPhase.Immersion, SessionWallClockBudget);

            int before = _events.Count;
            _director.StopFocusSession();

            Assert.AreEqual(FocusSessionPhase.None, _director.CurrentPhase,
                $"{LogPrefix} 중도 취소 뒤에는 구간이 없어야 합니다.");
            yield return null;
            Assert.AreEqual(before, _events.Count,
                $"{LogPrefix} 세션 종료는 「구간 전이」가 아닙니다 — 추가 이벤트가 오면 안 됩니다.");

            Debug.Log($"{LogPrefix} ② 통과 — StopFocusSession 경로.");
        }

        [UnityTest]
        public IEnumerator 긴급정지로_끝나도_구간이_닫힌다()
        {
            yield return LoadScene();
            _director.StartFocusSession(SessionMinutes);
            yield return WaitForPhase(FocusSessionPhase.Immersion, SessionWallClockBudget);

            int before = _events.Count;
            StickmanEventBus.RaiseGlobalEmergencyStop();

            Assert.IsFalse(_director.IsSessionActive,
                $"{LogPrefix} 긴급정지는 18절의 「항상 유효한 탈출구」입니다 — 세션이 끝나야 합니다.");
            Assert.AreEqual(FocusSessionPhase.None, _director.CurrentPhase,
                $"{LogPrefix} 긴급정지 뒤에는 구간이 없어야 합니다(이 저장소가 빠뜨린 전력이 있는 세 번째 경로).");
            yield return null;
            Assert.AreEqual(before, _events.Count,
                $"{LogPrefix} 긴급정지도 「구간 전이」가 아닙니다 — 추가 이벤트가 오면 안 됩니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — OnEmergencyStop 경로.");
        }

        /// <summary>★ 양성 대조 — 계기가 실제로 무는지 먼저 보인다. 세션 없이 이벤트를 직접 쏘면
        /// 구독이 살아 있는 한 목록이 늘어야 한다. 안 늘면 위 세 테스트의 「0회/2회」는
        /// 「구독이 죽었다」와 구별되지 않는다.</summary>
        [UnityTest]
        public IEnumerator 계기_양성대조_구독이_살아있다()
        {
            yield return LoadScene();
            Assert.AreEqual(0, _events.Count, $"{LogPrefix} 시작 전 목록이 비어 있어야 합니다.");
            StickmanEventBus.RaiseFocusSessionPhaseChanged(FocusSessionPhase.Adapt, FocusSessionPhase.Immersion);
            Assert.AreEqual(1, _events.Count,
                $"{LogPrefix} 이벤트를 직접 쐈는데 구독자가 못 받았습니다 — 계기가 죽었습니다.");
            Debug.Log($"{LogPrefix} ④ 통과 — 양성 대조(구독 생존).");
        }
    }
}
