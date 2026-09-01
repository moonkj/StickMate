using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 2026-09-02 사용자 신고 — <b>"캐릭터창에서 보이는 캐릭터는 장비 착용 모습만 적용되서
    /// 보여줘야하는데 가끔 움직임"</b>. 판정·실측 전문은 docs/UX_FLOW.md §45, 표면 스펙은
    /// docs/UI_SURFACE_SPEC.md §14.
    ///
    /// ============================================================================
    /// 잠그는 계약 — <b>액자 안에서는 시간이 흐르지 않는다</b>
    /// ============================================================================
    /// 움직임은 <b>두 개</b>였고 크기가 37배 달랐다. 이 파일은 둘을 <b>따로</b> 잰다.
    /// <list type="number">
    ///   <item><b>상시 호흡</b> — 주기 2.004초 / peak-to-peak 1.898pt. 창이 열려 있는 내내.</item>
    ///   <item><b>버킷 전환</b> — 뒷팔 끝이 1프레임에 70.9pt(액자 세로의 39%), 1.2초 뒤 되돌아옴.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ "안 움직인다"를 프레임 두 장으로 재지 않는다
    /// ============================================================================
    /// 호흡은 <b>주기 2초짜리 사인파</b>다. 배치모드 PlayMode는 2,000fps를 넘겨서 "180프레임"이
    /// 실제로는 0.01초일 수 있고(CLAUDE.md 확정 규약), 그런 표본은 사인파의 <b>같은 위상</b>만
    /// 보고 "정지"라고 보고한다. 그래서 관찰 예산은 <b>벽시계 초</b>로 잡고, 그 초 수를
    /// <see cref="CharacterPortraitStage.BreathPeriodSecondsForTests"/>에서 <b>유도</b>한다(베끼지 않는다).
    ///
    /// ============================================================================
    /// ★ 네거티브 컨트롤 — 이 파일이 무의미하게 초록이 되지 않게
    /// ============================================================================
    /// "액자가 안 움직인다"는 <b>아무 일도 일어나지 않은 씬</b>에서도 초록이다. 그래서 각 테스트가
    /// 대조군을 함께 단언한다:
    /// <list type="bullet">
    ///   <item>(1) 상태를 <b>실제로</b> 바꿨는가 — 상태 머신의 현재 ID로 확인.</item>
    ///   <item>(1) 그 상태들이 <b>예전이라면 그림을 바꿨을</b> 것들인가 —
    ///         <see cref="CharacterPortraitStage.PoseForState"/>가 <b>서로 다른 버킷</b>을 돌려주는지 확인.
    ///         (전부 같은 버킷이면 이 테스트는 아무것도 증명하지 못한다.)</item>
    ///   <item>(2) 표본이 <b>한 주기 이상</b>·<b>충분한 장수</b>인가 — 초와 장수를 함께 단언.</item>
    /// </list>
    /// </summary>
    public sealed class PortraitPaperDollTests
    {
        private const string LogPrefix = "[종이인형-TEST]";

        /// <summary>상태 전이가 초상화까지 도달할 기회(벽시계 초). 도달하면 안 되는 것을 재는 테스트라
        /// <b>넉넉히</b> 기다린다 — 짧게 기다려서 초록이면 그건 증명이 아니라 운이다.</summary>
        private const float ReachSeconds = 0.35f;

        private CharacterInfoWindow _window;
        private StickmanAgent _agent;

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            _agent = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator OpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            yield return null;
            yield return null;
        }

        private CharacterPortraitStage Stage()
        {
            CharacterPortraitStage stage = _window.PortraitStageForTests;
            Assert.IsNotNull(stage, $"{LogPrefix} 정보창이 초상화 촬영장을 갖고 있지 않습니다.");
            return stage;
        }

        /// <summary>미니 피규어 루트 — 포즈가 적용되던 바로 그 트랜스폼.</summary>
        private static Transform MiniFigure(CharacterPortraitStage stage)
        {
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");
            return figure;
        }

        /// <summary>지금 그려져 있는 <b>모든 선의 모든 점</b>을 한 줄로 뜬다. 트랜스폼만 보면
        /// "팔 각도만 바뀌는" 버킷 전환을 놓친다 — 실측된 70.9pt가 정확히 그 형태였다.</summary>
        private static List<Vector3> Fingerprint(CharacterPortraitStage stage)
        {
            var points = new List<Vector3>(256);
            var lines = stage.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                for (int p = 0; p < lr.positionCount; p++)
                {
                    points.Add(lr.transform.TransformPoint(lr.GetPosition(p)));
                }
            }
            return points;
        }

        private static float MaxDeviation(List<Vector3> a, List<Vector3> b)
        {
            Assert.AreEqual(a.Count, b.Count,
                $"{LogPrefix} 그림의 점 개수가 달라졌습니다({a.Count} -> {b.Count}) — " +
                "액자가 다시 구워졌다는 뜻입니다.");
            float max = 0f;
            for (int i = 0; i < a.Count; i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i]));
            return max;
        }

        // ============================================================================
        // (1) 상태가 바뀌어도 액자는 <b>한 점도</b> 움직이지 않는다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PortraitDoesNotChangeWhenTheCharacterStateChanges()
        {
            yield return OpenWindow();

            CharacterPortraitStage stage = Stage();
            Transform figure = MiniFigure(stage);
            StickmanBlackboard bb = _agent.Blackboard;
            Assert.IsNotNull(bb, $"{LogPrefix} 블랙보드가 아직 없습니다.");

            List<Vector3> baseline = Fingerprint(stage);
            Assert.Greater(baseline.Count, 0,
                $"{LogPrefix} 액자에 선이 하나도 없습니다 — 관측 전제가 깨졌습니다(그림이 안 그려졌다면 " +
                "무엇이 안 움직이는지 잴 수 없습니다).");
            Vector3 basePos = figure.localPosition;
            Quaternion baseRot = figure.localRotation;
            int baseSignature = stage.SignatureForTests;

            // 예전에 <b>실제로</b> 그림을 바꾸던 상태들. 사용자가 본 "가끔"의 정체(ParkourClimb)와
            // 2026-08-30 신고(Dragged), 그리고 눕던 것(Ragdoll)·비던 것(Runaway)을 전부 넣는다.
            var drive = new[]
            {
                StickmanStateId.ParkourClimb,
                StickmanStateId.Ragdoll,
                StickmanStateId.Dragged,
                StickmanStateId.Runaway,
                StickmanStateId.Idle,
            };

            // 대조군 A — 이 목록이 <b>여러 버킷</b>에 걸쳐 있어야 이 테스트에 뜻이 있다.
            var buckets = new HashSet<PortraitPose>();
            for (int i = 0; i < drive.Length; i++) buckets.Add(CharacterPortraitStage.PoseForState(drive[i]));
            Assert.Greater(buckets.Count, 1,
                $"{LogPrefix} 몰아 본 상태 {drive.Length}개가 전부 같은 포즈 버킷입니다 — " +
                "예전 구현에서도 그림이 안 바뀌었을 목록이라 이 테스트는 아무것도 증명하지 못합니다.");

            int reached = 0;
            for (int i = 0; i < drive.Length; i++)
            {
                bb.Machine.ChangeState(drive[i], isForcedInterrupt: true);

                float deadline = Time.realtimeSinceStartup + ReachSeconds;
                int frames = 0;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    frames++;
                }

                // 대조군 B — 상태가 실제로 그 값을 지났는가(전이가 즉시 튕겨 나갔으면 관측 전제가 깨진다).
                if (bb.Machine.CurrentStateId == drive[i]) reached++;

                Assert.AreEqual(baseSignature, stage.SignatureForTests,
                    $"{LogPrefix} 상태 {drive[i]}에서 액자 서명이 달라졌습니다 — 상태가 액자에 도달했습니다.");
                Assert.AreEqual(0f, MaxDeviation(baseline, Fingerprint(stage)), 1e-5f,
                    $"{LogPrefix} 상태 {drive[i]}에서 액자의 선이 움직였습니다({frames}프레임 관찰). " +
                    "실측된 옛 거동은 뒷팔 끝 70.9pt = 액자 세로의 39%였습니다(45-0-3).");
                Assert.AreEqual(0f, Vector3.Distance(figure.localPosition, basePos), 1e-5f,
                    $"{LogPrefix} 상태 {drive[i]}에서 미니 피규어가 통째로 움직였습니다.");
                Assert.AreEqual(0f, Quaternion.Angle(figure.localRotation, baseRot), 0.001f,
                    $"{LogPrefix} 상태 {drive[i]}에서 미니 피규어가 기울었습니다.");
            }

            Assert.Greater(reached, 1,
                $"{LogPrefix} 몰아 본 상태 {drive.Length}개 중 실제로 도달한 것이 {reached}개뿐입니다 — " +
                "상태가 바뀌지 않았다면 액자가 안 움직인 것은 당연하고, 이 초록은 거짓입니다.");

            Assert.AreEqual(PortraitPose.Standing, stage.Pose,
                $"{LogPrefix} 액자 포즈가 {stage.Pose}입니다 — 정보창이 아직 SetPose를 부르고 있습니다. " +
                "액자 불변식: 캐릭터의 상태는 액자에 도달하지 않습니다(45-1).");

            Debug.Log($"{LogPrefix} 상태 {drive.Length}종(버킷 {buckets.Count}종, 도달 {reached}종)을 " +
                $"몰아도 액자의 선 {baseline.Count}점이 한 점도 움직이지 않았습니다.");
        }

        // ============================================================================
        // (2) 시간이 흘러도 액자는 움직이지 않는다 — <b>호흡 정지</b>
        // ============================================================================
        //
        // 실측 옛 거동: 주기 2.004초 / peak-to-peak 1.898pt(예측 대비 101.9%), 가로 이동 0인
        // 강체 병진. 그래서 여기서는 <b>세로 좌표의 진폭</b>을 본다.

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PortraitDoesNotBreatheAcrossTwoFullPeriods()
        {
            yield return OpenWindow();

            CharacterPortraitStage stage = Stage();
            Transform figure = MiniFigure(stage);

            Assert.IsFalse(CharacterPortraitStage.BreathingEnabledForTests,
                $"{LogPrefix} 숨쉬기 게이트가 켜져 있습니다 — 아래 측정은 반드시 실패할 것이고, " +
                "그 전에 여기서 이유를 밝힙니다(45-2-a).");

            // 예산은 <b>프로덕션 주기</b>에서 유도한다. 두 주기를 보는 이유: 한 주기면 시작 위상이
            // 최대점이었을 때 되돌아온 값만 보고 "정지"로 오판할 여지가 남는다.
            float budget = CharacterPortraitStage.BreathPeriodSecondsForTests * 2f + 0.2f;
            Assert.Greater(budget, 1f, $"{LogPrefix} 관찰 예산({budget:F2}초)이 비정상입니다.");

            float baseY = figure.localPosition.y;
            float minY = baseY, maxY = baseY;
            int samples = 0;

            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < budget)
            {
                yield return null;
                float y = figure.localPosition.y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                samples++;
            }
            float elapsed = Time.realtimeSinceStartup - start;

            // 대조군 — 표본이 <b>한 주기 이상</b>이고 <b>두 장이 아니어야</b> 한다.
            Assert.GreaterOrEqual(elapsed, CharacterPortraitStage.BreathPeriodSecondsForTests,
                $"{LogPrefix} 관찰이 {elapsed:F2}초에 그쳤습니다 — 주기 " +
                $"{CharacterPortraitStage.BreathPeriodSecondsForTests:F2}초짜리 사인파를 한 주기도 " +
                "못 본 표본으로는 '정지'를 말할 수 없습니다.");
            Assert.Greater(samples, 60,
                $"{LogPrefix} 표본이 {samples}장뿐입니다 — 사인파의 같은 위상만 보고 '정지'로 " +
                "오판할 수 있습니다(프레임 두 장으로 재지 않는다).");

            float peakToPeak = maxY - minY;
            Debug.Log($"{LogPrefix} {elapsed:F2}초 / {samples}표본 관찰 — 세로 진폭 " +
                $"peak-to-peak = {peakToPeak:E3} 유닛(옛 거동은 1.898pt / 2.004초였다).");

            Assert.AreEqual(0f, peakToPeak, 1e-6f,
                $"{LogPrefix} 액자 속 인물이 {elapsed:F2}초 동안 세로로 {peakToPeak:E3}유닛 " +
                "움직였습니다 — 장비 A와 B를 겹쳐 비교할 수 없습니다(45-2-a).");
        }

        // ============================================================================
        // (3) 그림을 멈춘 것의 <b>직접 결과</b> — 프레즌스 줄이 유일한 움직임이 된다
        // ============================================================================
        //
        // 실측(45-3-b): 이 줄은 분당 17.4~21.7회 바뀌고, 폭주 구간에서는 2.11초에 문구 4개가
        // 지나갔다(최단 노출 0.22초 — 앱 자신의 가독예산 0.62초 미달). 그림이 멈추면 사용자가
        // 장비를 비교하며 쳐다보는 자리에서 유일하게 깜빡이는 것이 이 줄이 된다.
        //
        // 상한을 <b>베끼지 않는다</b>: 말풍선이 쓰는 가독예산 하한(DialogueBudget.MinSeconds)에서
        // "이 시간 동안 몇 번까지 바뀔 수 있는가"를 유도한다. hold가 그 값에서 나오므로,
        // 언젠가 가독예산이 바뀌면 이 테스트의 기대치도 함께 따라간다.

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PresenceLineHoldsLongEnoughToBeRead()
        {
            yield return OpenWindow();

            StickmanBlackboard bb = _agent.Blackboard;
            Assert.IsNotNull(bb, $"{LogPrefix} 블랙보드가 아직 없습니다.");

            // 라벨이 서로 다른 두 상태를 번갈아 강제한다(둘 다 Standing 버킷이라 액자와는 무관하다 —
            // 45-0-2에서 리더 가설이 반증된 바로 그 쌍이다).
            var flip = new[] { StickmanStateId.Idle, StickmanStateId.Walk };
            Assert.AreNotEqual(CharacterInfoWindow.StateLabel(flip[0]), CharacterInfoWindow.StateLabel(flip[1]),
                $"{LogPrefix} 번갈아 쓸 두 상태의 문구가 같습니다 — 폭주를 만들 수 없습니다.");

            const float StormSeconds = 1.5f;
            const float FlipIntervalSeconds = 0.05f;   // 실측 최단 노출(0.22초)보다 훨씬 촘촘하게 몬다.

            string last = _window.PresenceTextForTests;
            int changes = 0, flips = 0;
            float start = Time.realtimeSinceStartup;
            float nextFlip = start;

            while (Time.realtimeSinceStartup - start < StormSeconds)
            {
                if (Time.realtimeSinceStartup >= nextFlip)
                {
                    bb.Machine.ChangeState(flip[flips % flip.Length], isForcedInterrupt: true);
                    flips++;
                    nextFlip += FlipIntervalSeconds;
                }
                yield return null;

                string now = _window.PresenceTextForTests;
                if (now != last) { changes++; last = now; }
            }
            float elapsed = Time.realtimeSinceStartup - start;

            // 대조군 — 입력이 실제로 폭주였는가. 아니면 "안 바뀐다"는 당연한 결과다.
            Assert.Greater(flips, 10,
                $"{LogPrefix} {elapsed:F2}초 동안 상태를 {flips}번밖에 못 바꿨습니다 — " +
                "폭주를 만들지 못했으므로 아래 단언은 아무것도 증명하지 못합니다.");

            int allowed = Mathf.CeilToInt(elapsed / StickMate.Dialogue.DialogueBudget.MinSeconds) + 1;
            Debug.Log($"{LogPrefix} {elapsed:F2}초 동안 상태 {flips}회 강제 -> 프레즌스 문구 {changes}회 갱신 " +
                $"(허용 {allowed}회, 가독예산 하한 {StickMate.Dialogue.DialogueBudget.MinSeconds:F2}초).");

            Assert.LessOrEqual(changes, allowed,
                $"{LogPrefix} {elapsed:F2}초 동안 프레즌스 문구가 {changes}번 바뀌었습니다 — " +
                $"가독예산 하한({StickMate.Dialogue.DialogueBudget.MinSeconds:F2}초)으로는 최대 {allowed}번입니다. " +
                "hold가 걸리지 않았습니다(45-3-c).");

            // 그리고 <b>멈춰 버리지도</b> 않아야 한다 — hold는 지연이 아니라 최소 노출이다.
            Assert.Greater(changes, 0,
                $"{LogPrefix} 상태를 {flips}번 바꿨는데 문구가 한 번도 갱신되지 않았습니다 — " +
                "hold가 '만료 시 재조회'를 하지 않고 줄을 얼려 버렸습니다.");
        }
    }
}
