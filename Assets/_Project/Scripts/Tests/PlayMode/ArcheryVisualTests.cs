using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 활쏘기 연출(2026-08-29 사용자 요청 "과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동")
    /// 회귀 테스트.
    ///
    /// WindowCrashRendererTests / WindowTheftRendererTests와 같은 이유로 <b>Main.unity를 실제로
    /// 로드해서</b> 검사한다 — 렌더러/디렉터가 아무리 잘 만들어져 있어도 씬에 배치돼 있지 않으면 첫
    /// 줄에서 실패해야 한다("로직은 완성, 화면엔 0픽셀"이 이 프로젝트에서 6번 반복된 실패 모드다).
    /// 그리고 개수를 <b>정확히 1</b>로 단언한다: 0이면 SceneBootstrapper 배치 누락, 2 이상이면 중복
    /// 복제본에서 제거되지 않아 과녁이 두 벌 그려진다는 뜻이다(실측 전례가 있는 버그).
    ///
    /// ============================================================================
    /// 절대 조건 + 네거티브 컨트롤 (이 프로젝트 표준)
    /// ============================================================================
    /// · 콜라이더 수는 "적다"가 아니라 <b>정확히 0</b>(관전 전용, 클릭관통 유지 — 비침해 원칙 2).
    /// · 발사 횟수는 "여러 발"이 아니라 <b>정확히 3</b>이다.
    /// · ★★ 2026-09-03 — 예전 이 자리에는 "결과 시나리오는 <b>마지막은 Bullseye, 앞 두 발 중 정확히
    ///   하나가 Miss</b>"라고 적혀 있었다. 그 결정론적 시나리오가 사용자 신고(<i>"무조건 2대만 과녁에
    ///   명중하고 1대는 무조건 실패"</i>)의 원인이었고 사용자 지시로 폐지됐다. 이제 결과는
    ///   <c>StickConfig.archeryHitChance</c>로 <b>발마다 독립 추첨</b>되고(명중한 발은 다시
    ///   <c>StickConfig.archeryBullseyeChance</c>로 정중앙/외곽을 가른다 — 리더 판정 ②안)
    ///   <b>한 번의 실행으로는
    ///   확률 모델을 단언할 수 없다</b> — 이 PlayMode 테스트는 "뽑힌 결과와 실제 도달점/꽂힌 모양이
    ///   서로 맞는가"만 잠근다. 확률 모델 자체(극값 결정론 · 개수/순서의 독립성)는 EditMode의
    ///   <c>ArcheryShotProbabilityTests</c>가 씬 없이 난수를 주입해 잠근다.
    /// · 배율 검증은 "0.5는 1.0의 절반"이 아니라 <b>바깥에서 온 절대식</b>과 맞댄다
    ///   (과녁 꼭대기 == 캐릭터 정수리). 자기 자신을 기준으로 한 비율 비교는 둘 다 틀린 경우를
    ///   통과시킨다(RendererScaleRatioTests의 판단 기준과 동일).
    /// · <see cref="NoStartedEventMeansNothingIsDrawn"/> / <see cref="AbsoluteSizeWouldBreakScaleInvariant"/>가
    ///   네거티브 컨트롤이다.
    /// </summary>
    public sealed class ArcheryVisualTests
    {
        private const string ContainerName = "ArcheryVisuals";
        private const float Tol = 1e-4f;

        private ArcheryRenderer _renderer;
        private ArcheryDirector _director;
        private StickmanAgent _agent;

        private readonly List<ArcheryShotEvent> _releases = new List<ArcheryShotEvent>(4);
        private bool _listening;

        private readonly List<GameObject> _rigs = new List<GameObject>(3);

        [TearDown]
        public void TearDown()
        {
            if (_listening)
            {
                StickmanEventBus.ArcheryShotChanged -= OnShot;
                _listening = false;
            }
            _releases.Clear();
            for (int i = 0; i < _rigs.Count; i++) if (_rigs[i] != null) Object.DestroyImmediate(_rigs[i]);
            _rigs.Clear();
        }

        private void OnShot(ArcheryShotEvent evt)
        {
            if (evt.Phase == ArcheryShotPhase.Release) _releases.Add(evt);
        }

        // ============================================================================
        // ★★ 간헐 #11(2026-09-15) — 사거리 추첨을 경계에 고정하고, 밴드 울타리를 공개 설정에서 다시 유도한다
        // ============================================================================
        // 원인(코드·로그로 확정): 이 파일의 사거리 상한은 «신장 6.6배 + 0.2배»라는 **숫자**였다(767c985, 2026-09-01).
        // 프로덕션은 da71068(2026-09-07)에서 밴드 상한을 «min(구간, max(U0, min(g×구간폭, Ucap)))»으로 폭 비례로 열었는데
        // 이 파일은 따라가지 않았다. 배치모드 화면(걸어다닐 폭 30.18유닛, 신장 1.706)에서 밴드는 6.64~12.85이고
        // 옛 상한 11.60을 넘는 **합법** 추첨이 약 17.5%였다 — 같은 코드가 확률로 빨갰다.
        //
        // 규칙(CLAUDE.md): 프로덕션 상수를 숫자로 베끼지 않는다 / 기대값을 프로덕션 함수로 만들지 않는다(TEAM.md).
        //   · 설정값은 StickConfig 공개 필드에서 읽는다.
        //   · 여백 상수(CharacterEdgeInsetRatio 등)는 internal이라 이 어셈블리가 못 읽는다(AssemblyInfo.cs의
        //     InternalsVisibleTo는 EditMode만). 그래서 여기서는 **등식이 아니라 울타리(부등식)**만 세운다 —
        //     정확한 등식은 EditMode ArcheryTargetDistanceTests(⑤-2 ExpectedBand)가 잠근다.
        //   · 난수는 고정 시드(InitState)를 쓰지 않는다. 뒤에 도는 다른 테스트의 난수열을 결정론으로 바꿔
        //     다른 간헐을 가리거나 만들 수 있기 때문이다. 현재 생성기 상태에서 앞으로 찾아 **되감는다**.

        private enum RollEdge { Low, High }

        /// <summary>테스트 소유 여유(프로덕션 상수 아님) — 0 또는 1에서 이만큼 안쪽이면 «경계 추첨»으로 본다.</summary>
        private const float RollEdgeEpsilon = 0.001f;

        /// <summary>테스트 소유 여유 — 방향 난수를 0.25 이하/0.75 이상으로 잡는다. 프로덕션의 방향 문턱이
        /// 그 사이 어디에 있든 방향이 결정된다(문턱 숫자를 베끼지 않기 위한 폭).</summary>
        private const float DirectionRollMargin = 0.25f;

        /// <summary>한 번의 성공 확률이 0.001 × 0.25 = 2.5e-4라 기대 4,000회. 이 예산에서 못 찾을 확률은 e^-250 수준.</summary>
        private const int RollSearchBudget = 1_000_000;

        /// <summary>테스트 소유 여유 — 과녁·캐릭터 여백 합(internal 상수)을 신장의 이 배수 이하로 보수적으로 잡는다.
        /// 실제 합은 과녁 반지름을 뺀 나머지가 신장 1배에 한참 못 미친다. 이 값은 «울타리가 거짓 빨강을 내지 않는 쪽»으로만 쓴다.</summary>
        private const float ConservativeInsetHeights = 2f;

        /// <summary>
        /// <see cref="ArcheryDirector.ForceTriggerNow"/>가 소비하는 **다음 두 난수**(인자 평가 순서: 사거리 → 방향)를
        /// 경계로 맞춘다. 근거(코드 판독): GetAvailability와 그 안의 숨김 게이트(IsSuspended 조회)는 난수를 안 먹고,
        /// ArcheryState.Enter의 발별 추첨은 배치 추첨 **뒤**다. 호출과 ForceTriggerNow 사이에 yield를 두지 마라.
        /// </summary>
        private static void PinNextArcheryRolls(RollEdge edge, bool directionRollLow, out float roll, out float dirRoll)
        {
            for (int i = 0; i < RollSearchBudget; i++)
            {
                Random.State before = Random.state;
                float r = Random.value;
                float d = Random.value;
                bool edgeOk = edge == RollEdge.High ? r >= 1f - RollEdgeEpsilon : r <= RollEdgeEpsilon;
                bool dirOk = directionRollLow ? d <= DirectionRollMargin : d >= 1f - DirectionRollMargin;
                if (!edgeOk || !dirOk) continue;
                Random.state = before;
                roll = r;
                dirRoll = d;
                return;
            }
            roll = dirRoll = float.NaN;
            Assert.Fail($"난수 {RollSearchBudget}쌍을 뒤져도 경계({edge}) 추첨값을 못 찾았습니다 — 생성기가 멈췄거나 " +
                "Random.state 되감기가 동작하지 않습니다(이 상태로 재는 값은 전부 무효).");
        }

        /// <summary>
        /// 씬 설정(DefaultStickConfig.asset)의 사거리 밴드 필드가 **코드 기본값과 같다**는 다리.
        /// 아래 울타리들은 프로덕션 클램프(f ≤ 0.9, g ≤ 0.5, s ≤ 0.30 — internal 상수)가 비활성이라는 전제로
        /// 클램프를 생략하는데, 그 전제는 EditMode ArcheryTargetDistanceTests가 **코드 기본값**에 대해 잠근다.
        /// 애셋이 기본값과 갈라지면 그 잠금이 이 테스트로 넘어오지 않는다(TEAM.md 거짓 통과 #9).
        /// </summary>
        private static void AssertBandConfigMatchesCodeDefaults(StickConfig cfg)
        {
            Assert.IsNotNull(cfg, "캐릭터에 StickConfig가 배선돼 있지 않습니다.");
            var defaults = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                const string why = " — 씬 애셋이 코드 기본값과 다르면 EditMode의 클램프 잠금이 이 PlayMode 울타리의 전제로 " +
                    "넘어오지 않습니다. 애셋을 바꾼 라운드라면 StickConfig 기본값과 함께 맞추십시오(TEAM.md 거짓 통과 #9).";
                Assert.AreEqual(defaults.archeryMinTargetDistanceRatio, cfg.archeryMinTargetDistanceRatio, 0f, nameof(cfg.archeryMinTargetDistanceRatio) + why);
                Assert.AreEqual(defaults.archeryMaxTargetDistanceRatio, cfg.archeryMaxTargetDistanceRatio, 0f, nameof(cfg.archeryMaxTargetDistanceRatio) + why);
                Assert.AreEqual(defaults.archeryMinDistanceSpanFraction, cfg.archeryMinDistanceSpanFraction, 0f, nameof(cfg.archeryMinDistanceSpanFraction) + why);
                Assert.AreEqual(defaults.archeryMaxDistanceSpanFraction, cfg.archeryMaxDistanceSpanFraction, 0f, nameof(cfg.archeryMaxDistanceSpanFraction) + why);
                Assert.AreEqual(defaults.archeryMaxDistanceHardCapRatio, cfg.archeryMaxDistanceHardCapRatio, 0f, nameof(cfg.archeryMaxDistanceHardCapRatio) + why);
                Assert.AreEqual(defaults.archeryMinTargetDistanceScreenFraction, cfg.archeryMinTargetDistanceScreenFraction, 0f, nameof(cfg.archeryMinTargetDistanceScreenFraction) + why);
            }
            finally { Object.DestroyImmediate(defaults); }

            // 프로덕션 기준 상한은 max(하한 × 소폭 여유, 설정 상한)이다(뒤집힘 방어). 설정 상한이 하한의 2배를 넘으면
            // 그 방어가 개입하지 않아 U0 = 신장 × 설정 상한이 된다. 2는 테스트 소유 여유다(출하 6.6 대 2.6).
            Assert.Greater(cfg.archeryMaxTargetDistanceRatio, 2f * Mathf.Max(1f, cfg.archeryMinTargetDistanceRatio),
                "사거리 밴드 기준 상한이 절대 하한의 2배 이하입니다 — 프로덕션의 뒤집힘 방어가 개입할 수 있는 영역이라 " +
                "아래 울타리의 U0 식이 성립하지 않습니다.");
        }

        /// <summary>구간 폭 = 딛고 있는 발판 ∩ 걸어다닐 수 있는 화면(ArcheryDirector.TryResolvePlacement와 같은 두 공개 생산자).
        /// 프로덕션의 사거리 구간(과녁 끝 − 캐릭터 끝)은 여백만큼 이보다 **좁다** — 그래서 이 값은 위쪽 울타리에만 쓴다.</summary>
        private static float UsableWidth(GroundSensor.GroundInfo ground, float walkLeft, float walkRight)
            => Mathf.Max(0f, Mathf.Min(ground.CurrentFootholdRightWorldX, walkRight)
                             - Mathf.Max(ground.CurrentFootholdLeftWorldX, walkLeft));

        /// <summary>
        /// 밴드 상한의 **위쪽 울타리**. ResolvePlacement 계약 문장
        /// «상한 = min(구간, max(U0, min(g × 구간, Ucap)))»에 구간 대신 <see cref="UsableWidth"/>(≥ 구간)를 넣었다.
        /// 식이 구간에 대해 단조 증가이므로 참값 이상이다 = 거짓 빨강을 내지 않는다(헐거운 정도 = g × 여백 합).
        /// </summary>
        private static float BandHiCeiling(StickConfig cfg, float height, float usableWidth)
        {
            float u0 = height * cfg.archeryMaxTargetDistanceRatio;
            float cap = Mathf.Max(u0, height * cfg.archeryMaxDistanceHardCapRatio);
            return Mathf.Min(usableWidth, Mathf.Max(u0, Mathf.Min(cfg.archeryMaxDistanceSpanFraction * usableWidth, cap)));
        }

        /// <summary>
        /// 밴드 상한의 **아래쪽 울타리** — 위 울타리가 헐거워서 통과한 게 아님을 보이는 존재 대조.
        /// 구간을 «UsableWidth − 과녁 반지름 − 보수 여백(<see cref="ConservativeInsetHeights"/> × 신장)» 이상으로 잡는다.
        /// 반환값이 true면 이 화면에서 폭 비례 항(g)이 옛 고정 상한 U0를 **실제로 이긴다** — 즉 옛 테스트의 6.6H 상한이
        /// 합법 추첨을 빨갛게 만드는 기하라는 뜻이다.
        /// </summary>
        private static bool WidthTermBinds(StickConfig cfg, float height, float usableWidth, float targetRadius,
            out float bandHiFloor)
        {
            float spanFloor = usableWidth - targetRadius - ConservativeInsetHeights * height;
            float u0 = height * cfg.archeryMaxTargetDistanceRatio;
            float cap = Mathf.Max(u0, height * cfg.archeryMaxDistanceHardCapRatio);
            float widthTerm = Mathf.Min(cfg.archeryMaxDistanceSpanFraction * spanFloor, cap);
            bandHiFloor = Mathf.Min(spanFloor, Mathf.Max(u0, widthTerm));
            return widthTerm > u0;
        }

        /// <summary>경계 추첨 한 번의 관측 — 전부 **발동한 그 프레임**에 읽는다(뒤에 활이 그려지면 시각 반폭이 바뀌어
        /// 걸어다닐 폭이 달라진다. 프로덕션이 추첨에 쓴 기하와 같은 순간이어야 한다).</summary>
        private struct EdgeDraw
        {
            public float Roll, DirRoll, Height, FootX, WalkLeft, WalkRight, Usable, Drawn, TargetX, Facing;
            public long Foothold;
            public float ScreenWidth => WalkRight - WalkLeft;
        }

        /// <summary>경계 난수를 고정하고 **같은 프레임에** 발동해 추첨 결과를 읽는다(yield 없음).</summary>
        private EdgeDraw TriggerAtRollEdge(RollEdge edge, bool directionRollLow, string reason)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            GroundSensor.GroundInfo ground = bb.SenseGround();
            Assert.IsTrue(ground.Grounded, $"{reason}: 발동 직전인데 캐릭터가 발판 위가 아닙니다.");
            Assert.IsTrue(bb.TryGetWalkableScreenBoundsWorld(out float wl, out float wr),
                $"{reason}: 걸어다닐 수 있는 화면 경계를 못 읽었습니다 — 프로덕션은 카메라 폴백으로 넘어가므로 이 울타리는 무효입니다.");

            var s = new EdgeDraw
            {
                Height = bb.CharacterHeightWorld,
                FootX = bb.Body.position.x,
                Foothold = bb.CurrentFootholdHandle,
                WalkLeft = wl,
                WalkRight = wr,
                Usable = UsableWidth(ground, wl, wr),
            };
            PinNextArcheryRolls(edge, directionRollLow, out s.Roll, out s.DirRoll);
            Assert.IsTrue(_director.ForceTriggerNow(reason), $"{reason}: 강제 발동이 거절됐습니다(자리 없음/락/상태).");
            s.TargetX = _director.LastTargetWorld.x;
            s.Drawn = Mathf.Abs(s.TargetX - bb.ArcheryStandWorldX);
            s.Facing = bb.ArcheryFacingSign;
            return s;
        }

        /// <summary>벽시계 기준으로 «Idle/Walk + 발판 딛음 + 연출 락 없음»을 기다린다(CLAUDE.md: 프레임 수 대기 금지).</summary>
        private IEnumerator WaitUntilReadyToShoot(float budgetSeconds, string label)
        {
            float deadline = Time.realtimeSinceStartup + budgetSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var st = _agent.Blackboard.Machine.CurrentStateId;
                if ((st == StickmanStateId.Idle || st == StickmanStateId.Walk)
                    && _agent.Blackboard.CurrentFootholdHandle != 0L && !SpectacleEventLock.IsActive) yield break;
                yield return null;
            }
            Assert.Fail($"{label}: 벽시계 {budgetSeconds:F0}초 안에 쏠 수 있는 상태(Idle/Walk + 발판 + 락 없음)가 되지 않았습니다 " +
                $"(현재 {_agent.Blackboard.Machine.CurrentStateId}, 발판 {_agent.Blackboard.CurrentFootholdHandle}, 락 {SpectacleEventLock.IsActive}).");
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var directors = Object.FindObjectsByType<ArcheryDirector>(FindObjectsSortMode.None);
            Assert.AreEqual(1, directors.Length,
                $"씬의 ArcheryDirector 개수가 {directors.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락, 2개 이상이면 씬에 중복 배치된 것입니다.");

            var renderers = Object.FindObjectsByType<ArcheryRenderer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, renderers.Length,
                $"씬의 ArcheryRenderer 개수가 {renderers.Length}개입니다 — 1개여야 합니다. " +
                "2개 이상이면 두 번째 렌더러도 전역 이벤트를 받아 과녁이 두 벌 그려집니다.");

            _director = directors[0];
            _renderer = renderers[0];
            _agent = _renderer.GetComponent<StickmanAgent>();
            Assert.IsNotNull(_agent, "ArcheryRenderer가 StickmanAgent와 같은 GameObject에 있지 않습니다 — " +
                "이 렌더러는 씬 전체 탐색 폴백을 쓰지 않으므로 그러면 영원히 아무것도 그리지 않습니다.");

            Assert.IsFalse(_renderer.IsVisible, "테스트 시작 시점에는 과녁이 떠 있으면 안 됩니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "테스트 시작 시점의 시각 오브젝트는 0개여야 합니다.");
        }

        // ============================================================================
        // ① 배치 — 씬에 정확히 1개씩
        // ============================================================================

        [UnityTest]
        public IEnumerator SceneHasExactlyOneDirectorAndOneRenderer()
        {
            yield return LoadSceneAndResolve();
            Debug.Log("[활쏘기테스트] 배치 검증 통과 — 디렉터 1개 / 렌더러 1개.");
        }

        // ============================================================================
        // ② 이벤트 발행 시 시각 오브젝트가 실존한다 + 콜라이더는 정확히 0개
        // ============================================================================

        [UnityTest]
        public IEnumerator StartedEventActuallyCreatesTargetAndStaysClickThrough()
        {
            yield return LoadSceneAndResolve();

            Vector2 foot = _agent.Blackboard.Body.position;
            var target = new Vector2(foot.x + 4f, foot.y + 1.2f);
            StickmanEventBus.RaiseArcheryOverlayChanged(target, foot.y, 1f, SpectacleOverlayPhase.Started);

            Assert.IsTrue(_renderer.IsVisible,
                "ArcheryOverlayChanged(Started)를 발행했는데 렌더러가 아무것도 그리지 않았습니다.");
            Assert.Greater(_renderer.ActiveVisualCount, 0,
                "과녁이 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기).");
            Assert.AreEqual(0, _renderer.ActiveColliderCount,
                "과녁 오버레이가 콜라이더를 만들었습니다 — 관전 전용이므로 정확히 0개여야 합니다" +
                "(콜라이더가 있으면 그 자리의 다른 앱을 클릭할 수 없게 되어 비침해 원칙 2 위반).");

            yield return null;
            Assert.IsNotNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 실존하지 않습니다.");

            // 유지 중에도 계속 0개.
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.15f);
                Assert.AreEqual(0, _renderer.ActiveColliderCount,
                    $"유지 중({(i + 1) * 0.15f:F2}초) 콜라이더가 생겼습니다.");
            }

            Debug.Log($"[활쏘기테스트] Started 검증 통과 — 시각 오브젝트 {_renderer.ActiveVisualCount}개, 콜라이더 0개.");
        }

        /// <summary>네거티브 컨트롤 — 이벤트가 없으면 아무것도 그리지 않는다(위 단언이 "무조건 뭔가
        /// 있다"로 통과하는 것이 아님을 증명).</summary>
        [UnityTest]
        public IEnumerator NoStartedEventMeansNothingIsDrawn()
        {
            yield return LoadSceneAndResolve();
            yield return new WaitForSeconds(0.6f);

            Assert.IsFalse(_renderer.IsVisible, "아무 이벤트도 발행하지 않았는데 과녁이 떠 있습니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "이벤트 없이 시각 오브젝트가 생겼습니다.");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"이벤트 없이 '{ContainerName}' GameObject가 씬에 생겼습니다.");
        }

        // ============================================================================
        // ③ 전체 사이클 — 3발이 실제로 나가고, 끝나면 컨테이너가 씬에서 실제로 소멸한다
        // ============================================================================

        [UnityTest]
        public IEnumerator FullCycleFiresExactlyThreeArrowsAndFullyCleansUp()
        {
            yield return LoadSceneAndResolve();

            // 캐릭터가 **실제로 발판을 딛을 때까지** 기다린다. 상태 ID만 보면 안 된다 — 씬 시작 직후의
            // 초기 상태가 이미 Idle이라, 아직 낙하해 발판을 잡기도 전에 조건이 성립해버리고
            // (CurrentFootholdHandle == 0) 활쏘기가 시작되자마자 [발판상실]로 Fall에 빠진다
            // (실측으로 밟은 함정: 발사 이벤트 0건).
            float wait = 0f;
            while (wait < 10f)
            {
                var st = _agent.Blackboard.Machine.CurrentStateId;
                bool ready = (st == StickmanStateId.Idle || st == StickmanStateId.Walk)
                    && _agent.Blackboard.CurrentFootholdHandle != 0L;
                if (ready) break;
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Idle
                || _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Walk,
                $"10초를 기다려도 캐릭터가 Idle/Walk가 되지 않았습니다(현재 {_agent.Blackboard.Machine.CurrentStateId}) — " +
                "활쏘기는 그 두 상태에서만 시작합니다.");
            Assert.AreNotEqual(0L, _agent.Blackboard.CurrentFootholdHandle,
                "10초를 기다려도 캐릭터가 어떤 발판도 딛지 못했습니다 — 이 상태로 활쏘기를 시작하면 " +
                "0.1초 뒤 [발판상실]로 Fall에 빠져 아무것도 검증하지 못합니다.");

            StickmanEventBus.ArcheryShotChanged += OnShot;
            _listening = true;

            // ★★ 간헐 #11 — 사거리 추첨을 **상단 경계**에 고정한다(가장 긴 비행 · 옛 상한을 넘는 쪽 = 이 테스트의 최악 조건).
            //   하단 경계와 반대 방향은 RangeDrawAtBothRollEdgesStaysInsideTheWidthProportionalBand가 잰다.
            //   기하는 발동한 **그 프레임**에 읽는다(TriggerAtRollEdge 문서).
            EdgeDraw pinned = TriggerAtRollEdge(RollEdge.High, directionRollLow: true, "PlayMode 테스트");
            yield return null;

            Assert.AreEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "ForceTriggerNow 후에도 상태가 Archery가 아닙니다 — 과녁 자리를 못 찾았거나 락에 막혔습니다.");
            Assert.IsTrue(SpectacleEventLock.IsActive && SpectacleEventLock.ActiveKind == SpectacleEventKind.Archery,
                "활쏘기가 SpectacleEventLock을 잡지 않았습니다 — 다른 스펙터클과 동시에 발동할 수 있게 됩니다.");

            // ★ 사용자 재정의 사양: "…만큼 캐릭터가 이동한 다음 과녁을 생성후 쏘고".
            // 즉 발동 직후에는 아직 과녁이 없어야 하고, **걸어서 도착한 뒤**에 나타나야 한다.
            Vector2 footAtTrigger = _agent.Blackboard.Body.position;
            Assert.IsFalse(_renderer.IsVisible,
                "발동하자마자 과녁이 나타났습니다 — 사용자 요구 순서는 '이동 -> 과녁 생성 -> 발사'입니다.");

            float approachWait = 0f;
            while (approachWait < 15f && !_renderer.IsVisible)
            {
                approachWait += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(_renderer.IsVisible,
                "15초를 기다려도 과녁이 나타나지 않았습니다 — 이동(Approach) 단계에서 멈춰 있습니다.");

            Vector2 footAtStart = _agent.Blackboard.Body.position;
            long footholdAtStart = _agent.Blackboard.CurrentFootholdHandle;
            float walked = Mathf.Abs(footAtStart.x - footAtTrigger.x);

            // 캐릭터는 과녁 쪽을 보고 있어야 한다(등지고 쏘면 활이 등 뒤에 그려진다 — 실측 버그).
            float dirToTarget = Mathf.Sign(_director.LastTargetWorld.x - footAtStart.x);
            Assert.AreEqual(dirToTarget, _agent.Blackboard.FacingSign, 0.001f,
                $"캐릭터가 과녁 반대쪽을 보고 있습니다(과녁 x={_director.LastTargetWorld.x:F2}, " +
                $"캐릭터 x={footAtStart.x:F2}, 바라보는 방향={_agent.Blackboard.FacingSign}).");
            Assert.AreEqual(_agent.Blackboard.ArcheryFacingSign, _agent.Blackboard.FacingSign, 0.001f,
                "과녁을 놓은 방향과 캐릭터가 보는 방향이 다릅니다.");
            Assert.IsTrue(_agent.Blackboard.FacingLocked,
                "쏘기 시작했는데 방향 고정이 걸려 있지 않습니다 — 배회 AI의 이동 의도로 몸이 돌아가 " +
                "화살이 뒤통수에서 나갈 수 있습니다.");

            // ★ 사거리 조건 — **추첨된 사거리**(서는 자리 ↔ 과녁, Begin()이 블랙보드에 확정한 두 값)로 잰다.
            //   ★★ 간헐 #11: 예전 상한은 걸어간 뒤의 실측 거리에 «신장 6.6배 + 0.2배»를 숫자로 댔다. 6.6은 출하 설정을,
            //   0.2는 도착 허용 오차 여유를 베낀 값이었고, 프로덕션이 da71068에서 상한을 폭 비례로 연 뒤로 합법 추첨의
            //   약 17.5%(배치모드 화면)가 그 숫자를 넘었다. 이제 상한은 공개 설정에서 다시 유도한 울타리이고
            //   (BandHiCeiling), 걸음 오차(ArcheryState 도착 판정)는 사거리 계약과 섞지 않는다.
            //   하한 2.6도 같은 병이라 설정 필드를 참조하게 바꿨다.
            GroundSensor.GroundInfo groundInfo = _agent.Blackboard.SenseGround();
            float shootDistance = Mathf.Abs(_director.LastTargetWorld.x - footAtStart.x);
            StickConfig cfg = _agent.Config;
            AssertBandConfigMatchesCodeDefaults(cfg);
            float absoluteFloor = pinned.Height * cfg.archeryMinTargetDistanceRatio;
            Assert.GreaterOrEqual(pinned.Drawn, absoluteFloor - 1e-3f,
                $"추첨 사거리가 {pinned.Drawn:F2}유닛뿐입니다 — 절대 하한(신장의 {cfg.archeryMinTargetDistanceRatio:F2}배 = " +
                $"{absoluteFloor:F2}유닛)조차 안 됩니다. 코앞에서 쏘면 포물선이 직선처럼 보입니다.");
            Assert.GreaterOrEqual(shootDistance, absoluteFloor - 0.01f,
                $"걸어간 뒤 실제 사거리가 {shootDistance:F2}유닛뿐입니다 — 절대 하한({absoluteFloor:F2}유닛) 미달.");

            if (groundInfo.Grounded)
            {
                Assert.GreaterOrEqual(_director.LastTargetWorld.x, groundInfo.CurrentFootholdLeftWorldX - 0.01f,
                    $"과녁 x={_director.LastTargetWorld.x:F2}가 딛고 있는 발판의 왼쪽 끝" +
                    $"({groundInfo.CurrentFootholdLeftWorldX:F2}) 바깥입니다 — 창 모서리 너머 허공에 뜹니다.");
                Assert.LessOrEqual(_director.LastTargetWorld.x, groundInfo.CurrentFootholdRightWorldX + 0.01f,
                    $"과녁 x={_director.LastTargetWorld.x:F2}가 딛고 있는 발판의 오른쪽 끝" +
                    $"({groundInfo.CurrentFootholdRightWorldX:F2}) 바깥입니다 — 창 모서리 너머 허공에 뜹니다.");

                // ★ 2026-08-31 사용자 재정의로 상한 규칙이 바뀌었다(구간 끝 고정 배치 → 매번 추첨).
                // ★★ 2026-09-07(da71068) 프로덕션이 상한을 폭 비례로 열었다: 상한 = min(구간, max(U0, min(g×구간, Ucap))).
                //   이 파일은 그 뒤에도 «6.6H + 0.2H» 숫자를 들고 있었고 그것이 간헐 #11이다.
                //   여기서는 실물 씬의 **배선**(설정·화면 폭·신장이 추첨에 제대로 들어가는가)만 울타리로 확인하고,
                //   식의 등식과 분포는 EditMode ArcheryTargetDistanceTests가 잠근다.
                float ceiling = BandHiCeiling(cfg, pinned.Height, pinned.Usable);
                Assert.LessOrEqual(pinned.Drawn, ceiling + 1e-3f,
                    $"상단 경계 추첨(roll {pinned.Roll:F4}) 사거리 {pinned.Drawn:F2}유닛이 밴드 상한 울타리 {ceiling:F2}유닛" +
                    $"(구간 폭 {pinned.Usable:F2}유닛, 신장 {pinned.Height:F2})을 넘었습니다 — 사거리가 다시 화면/발판 폭 전체에 " +
                    "끌려가고 있습니다(신고 '무조건 과녁이 화면 끝에만 생김' 재발).");

                // 존재 대조 — 위 울타리가 헐거워서 통과한 게 아니다.
                if (WidthTermBinds(cfg, pinned.Height, pinned.Usable, _renderer.TargetRadius, out float bandHiFloor))
                {
                    Assert.GreaterOrEqual(pinned.Drawn, (1f - RollEdgeEpsilon) * bandHiFloor - 1e-3f,
                        $"상단 경계 추첨(roll {pinned.Roll:F4})인데 사거리가 {pinned.Drawn:F2}유닛으로 폭 비례 상한의 아래 울타리 " +
                        $"{bandHiFloor:F2}유닛에 못 미칩니다 — 난수 고정이 안 먹었거나(추첨이 경계가 아님) 폭 비례 항(g)이 배선에서 빠졌습니다.");
                    Debug.Log($"[활쏘기테스트] 폭 비례 항 생존 — 상단 경계 추첨 {pinned.Drawn:F2}유닛, 옛 고정 상한 U0 " +
                        $"{pinned.Height * cfg.archeryMaxTargetDistanceRatio:F2}유닛(이 차이가 간헐 #11의 원인 기하).");
                }
                else
                {
                    Debug.LogWarning($"[활쏘기테스트] 이 화면(구간 폭 {pinned.Usable:F2}유닛)에서는 폭 비례 항이 U0를 못 이겨 " +
                        "상한 존재 대조를 건너뜀 — 같은 조건에서 RangeDrawAtBothRollEdgesStaysInsideTheWidthProportionalBand가 «측정 무효» 실패로 드러낸다.");
                }

                // ★ 옛 «진행 방향 화면 끝 여유 > 신장 2배» 단언은 RangeDrawAtBothRollEdgesStaysInsideTheWidthProportionalBand의
                //   **하단 경계**로 옮겼다. 옛 전제 두 개(«facing +1이면 standX ≤ 화면중앙 − 한걸음», «사거리 ≤ 6.6H»)가
                //   방향 추첨(2026-09-06)과 폭 비례 상한(da71068)으로 둘 다 거짓이 됐다. 상단 경계에서 올바른 전제로 다시 세우면
                //   성립 조건이 거의 안 잡혀 조용히 건너뛰는 단언이 되므로, 전제가 성립하는 하단 경계에서 잰다.
            }

            Debug.Log($"[활쏘기테스트] 이동 검증 — {walked:F2}유닛 걸어간 뒤 과녁 등장, 사거리 {shootDistance:F2}유닛 " +
                $"(발판 종류: {(ArcheryDirector.IsRealWindowFoothold(groundInfo.GroundedFootholdHandle) ? "창/Dock" : "바탕화면")}).");

            // 사이클 전체를 지켜본다(인트로 0.55 + 3발 3.18 + 아웃트로 1.37 + 렌더러 페이드 0.75 = 약 5.9초).
            float elapsed = 0f;
            int maxSpawned = 0;
            int maxStuck = 0;
            var stuckResults = new List<ArcheryShotResult>(3);
            var stuckDescents = new List<float>(3);
            var stuckOvershoots = new List<float>(3);
            bool leftArchery = false;
            // ★ 2026-08-31 신고 "활대를 잡아야 하는데 이상한 데를 잡고 쏨" — 사이클을 지켜보는 김에
            // **실제로 그려진 활대와 손의 거리**를 매 프레임 잰다(순수 함수 테스트만으로는 배치
            // 코드가 그 함수를 쓰는지 증명되지 않는다).
            StickmanPoseAnimator poseAnimator = _agent.Blackboard.GetPoseAnimator();
            float maxGripError = -1f;
            float maxHandVsPoseError = -1f;
            Vector2 footAtStateEnd = footAtStart;
            float archerySeconds = 0f;
            while (elapsed < 12f)
            {
                elapsed += Time.deltaTime;
                maxSpawned = Mathf.Max(maxSpawned, _renderer.SpawnedArrowCount);
                maxStuck = Mathf.Max(maxStuck, _renderer.StuckArrowCount);
                // 꽂힌 화살의 모양은 **사라지기 전에** 재야 한다 — 루프를 빠져나올 조건이
                // "렌더러가 안 보임"이고 그 시점에는 이미 Teardown으로 화살 목록이 비어 있다.
                if (_renderer.StuckArrowCount == 3 && stuckResults.Count == 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (!_renderer.TryGetStuckArrow(i, out ArcheryShotResult sr, out float sd, out float so)) continue;
                        stuckResults.Add(sr);
                        stuckDescents.Add(sd);
                        stuckOvershoots.Add(so);
                    }
                }
                if (!leftArchery)
                {
                    archerySeconds = elapsed;
                    if (_agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Archery)
                    {
                        // ★ 위치는 **상태가 끝나는 그 순간** 재야 한다. 그 뒤에는 배회 AI가 다시
                        // 걷기 시작하므로, 나중에 재면 "활쏘기가 옮겼다"와 "끝나고 걸어갔다"를
                        // 구분할 수 없다(실측으로 밟은 함정 — 1.31유닛 차이가 전부 종료 후 보행이었다).
                        leftArchery = true;
                        footAtStateEnd = _agent.Blackboard.Body.position;
                    }
                }
                if (_agent.Blackboard.ArcheryDrawRatio > 0.5f
                    && _renderer.TryGetBowGripAndHandWorld(out Vector2 gripWorld, out Vector2 bowHandWorld))
                {
                    maxGripError = Mathf.Max(maxGripError, Vector2.Distance(gripWorld, bowHandWorld));
                    if (poseAnimator != null && poseAnimator.HasLimbs)
                    {
                        poseAnimator.GetHandWorldPositions(out _, out Vector2 frontHand);
                        if (frontHand != Vector2.zero)
                            maxHandVsPoseError = Mathf.Max(maxHandVsPoseError,
                                Vector2.Distance(bowHandWorld, frontHand));
                    }
                }
                if (!_renderer.IsVisible && _releases.Count >= 3) break;
                yield return null;
            }
            Assert.IsTrue(leftArchery, "12초 안에 활쏘기가 끝나지 않았습니다 — 3발 뒤 종료 조건이 걸리지 않습니다.");

            // ★ 손이 잡는 곳 = 활대 한가운데(신고 회귀 잠금). 예전 배치는 이 거리가 정확히
            // 그립 오프셋(신장의 15.5%)이었다 — 손이 활대도 시위도 아닌 빈 공간을 쥔 그림.
            float charHeight = _agent.Blackboard.CharacterHeightWorld;
            Assert.GreaterOrEqual(maxGripError, 0f,
                "만작(당김 50% 초과) 구간에서 활이 한 프레임도 그려지지 않았습니다 — 이 검사가 " +
                "아무것도 보지 못했다는 뜻입니다(테스트가 조용히 무력화된 상태).");
            Assert.Less(maxGripError, charHeight * 0.02f,
                $"활대 한가운데가 활 든 손에서 최대 {maxGripError:F3}유닛(신장의 " +
                $"{maxGripError / charHeight:P1}) 떨어져 그려졌습니다 — 신고된 '이상한 데를 잡고 쏨'입니다.");
            Assert.Less(maxHandVsPoseError, charHeight * 0.25f,
                $"렌더러가 활을 쥐여준 지점이 실제 앞손(오른팔 끝)에서 최대 {maxHandVsPoseError:F3}유닛 " +
                "떨어져 있습니다 — 반대쪽 손이나 폴백 근사치를 쓰고 있다는 뜻입니다.");
            Assert.Less(archerySeconds, 10f,
                $"활쏘기 한 사이클이 {archerySeconds:F1}초나 걸렸습니다 — 3발 연출이 늘어집니다.");

            Assert.AreEqual(3, _releases.Count,
                $"발사 이벤트가 {_releases.Count}건입니다 — 사용자 요청은 '3번정도'이고 코드 상수도 3입니다.");
            Assert.AreEqual(3, maxSpawned,
                $"실제로 스폰된 화살이 {maxSpawned}개입니다 — 발사 이벤트만 나가고 화살이 안 그려지면 " +
                "화면에는 아무 일도 일어나지 않은 것과 같습니다.");
            Assert.AreEqual(3, maxStuck,
                $"과녁/땅에 꽂힌 화살이 {maxStuck}개입니다 — 3발 전부가 도달점까지 날아가 꽂혀야 합니다.");

            // ★★ 2026-09-03 — 옛 단언(마지막은 Bullseye 고정 / 앞 두 발 중 정확히 하나가 Miss)은
            //   사용자 지시로 폐지된 결정론적 시나리오를 잠그고 있었다. 지금은 발마다 독립 추첨이라
            //   "3발 다 명중"도 "3발 다 빗나감"도 정상이며, 그중 어떤 조합이 나와도 이 실행 하나로는
            //   확률 모델을 증명하지 못한다. 그래서 여기서는 **결과가 legal한 값인가**와
            //   **그 결과에 맞는 도달점/모양으로 그려졌는가**만 잠근다(아래 도달점 검증이 그 본체다).
            //   확률 모델은 EditMode ArcheryShotProbabilityTests 소관이다.
            for (int i = 0; i < ArcheryState.ShotCount; i++)
            {
                ArcheryShotResult r = _releases[i].Result;
                Assert.IsTrue(r == ArcheryShotResult.Miss || r == ArcheryShotResult.Hit
                        || r == ArcheryShotResult.Bullseye,
                    $"{i + 1}발째 결과가 정의되지 않은 값입니다({r}).");
            }

            // 도달점 검증: 명중은 과녁 반경 안, 빗나감은 지면 높이.
            Vector2 targetWorld = _director.LastTargetWorld;
            float radius = _renderer.TargetRadius;
            for (int i = 0; i < 3; i++)
            {
                ArcheryShotEvent e = _releases[i];
                if (e.Result == ArcheryShotResult.Miss)
                {
                    Assert.Less(Mathf.Abs(e.ImpactWorld.x - targetWorld.x), radius * 4f,
                        $"{i + 1}발째(빗나감) 도달점이 과녁에서 너무 멉니다 — 화면 밖으로 나갑니다.");
                    Assert.Less(e.ImpactWorld.y, targetWorld.y - radius,
                        $"{i + 1}발째는 빗나감인데 도달점이 과녁 아래(땅)가 아닙니다.");
                }
                else
                {
                    Assert.LessOrEqual(Vector2.Distance(e.ImpactWorld, targetWorld), radius + Tol,
                        $"{i + 1}발째({e.Result}) 도달점이 과녁 바깥입니다.");
                }
                if (e.Result == ArcheryShotResult.Bullseye)
                {
                    Assert.AreEqual(0f, Vector2.Distance(e.ImpactWorld, targetWorld), Tol,
                        "정중앙인데 도달점이 과녁 중심과 다릅니다.");
                }
            }

            // ★ 2026-08-29 사용자 신고 "화살이 과녁에 좀 이상하게 꽂힘 / 다 외곽에 꽂히는거 같음"
            //   회귀 잠금 — 실제 씬에서 꽂힌 3발의 **모양**을 잰다.
            Assert.AreEqual(3, stuckResults.Count,
                "3발이 꽂힌 순간의 모양을 재지 못했습니다 — 관찰 창구(TryGetStuckArrow)가 비어 있습니다.");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0f, stuckOvershoots[i], 1e-4f,
                    $"{i + 1}발째({stuckResults[i]}) 화살이 도달점보다 진행 방향으로 " +
                    $"{stuckOvershoots[i]:F3}유닛 더 나가 있습니다 — 도달점에 꽂히는 것은 **촉**이어야 하는데 " +
                    "오늬(꼬리)가 꽂혀 화살이 과녁을 관통해 반대편으로 삐져나온 그림이 됩니다. " +
                    "정중앙에 맞은 화살조차 촉이 바깥 링에 걸려 '다 외곽에 꽂힌다'로 보인 실제 신고 원인입니다.");
                Assert.GreaterOrEqual(stuckDescents[i], -1f,
                    $"{i + 1}발째 화살이 코를 위로 든 채 꽂혔습니다({stuckDescents[i]:F1}도) — 내려꽂혀야 합니다.");

                if (stuckResults[i] == ArcheryShotResult.Miss)
                {
                    Assert.AreEqual(_renderer.GroundImpactDescentDegrees, stuckDescents[i], 0.75f,
                        $"{i + 1}발째(빗나감) 땅에 꽂힌 각도가 {stuckDescents[i]:F1}도입니다 — " +
                        $"설정값 {_renderer.GroundImpactDescentDegrees:F1}도로 확정되어야 합니다.");
                }
                else
                {
                    Assert.LessOrEqual(stuckDescents[i], _renderer.FaceImpactMaxDescentDegrees + 0.75f,
                        $"{i + 1}발째({stuckResults[i]}) 과녁 면에 {stuckDescents[i]:F1}도로 꽂혔습니다 — " +
                        $"상한 {_renderer.FaceImpactMaxDescentDegrees:F1}도를 넘습니다. 이 정도로 가파르면 " +
                        "화살이 과녁 면을 비스듬히 가로질러 '이상하게 꽂혔다'로 보입니다.");
                }
            }
            Debug.Log($"[활쏘기테스트] 꽂힌 3발 하강각 = {stuckDescents[0]:F1} / {stuckDescents[1]:F1} / " +
                $"{stuckDescents[2]:F1}도, 촉 초과분 = {stuckOvershoots[0]:F3} / {stuckOvershoots[1]:F3} / " +
                $"{stuckOvershoots[2]:F3}유닛(전부 0이어야 정상).");

            // 정리 — 컨테이너가 씬에서 **실제로** 소멸했는가.
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_renderer.IsVisible, "사이클이 끝났는데 렌더러가 여전히 '보인다'고 보고합니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                $"사이클 종료 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다.");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 그대로 남아 있습니다 — 컨테이너가 실제로 파괴되지 않았습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive,
                "사이클이 끝났는데 SpectacleEventLock이 잡힌 채로 남아 있습니다 — 이후 다른 스펙터클이 영영 발동하지 못합니다.");
            // ★ 사용자 신고 "다른행동은 아예안하고 계속 활만쏨" 회귀 잠금 — 끝나자마자 다시 시작할 수 없다.
            Assert.Greater(_director.CooldownRemaining, 1f,
                $"사이클이 끝났는데 자율 재발동 쿨다운이 {_director.CooldownRemaining:F1}초뿐입니다 — " +
                "이러면 Idle로 돌아오자마자 다시 활을 쏴서 유저 눈에는 '끝나지 않는다'로 보입니다.");
            Assert.IsTrue(_agent.Config.archeryChance <= 0f,
                $"출하 설정의 활쏘기 자율 발동 확률이 {_agent.Config.archeryChance}입니다 — 0이어야 합니다" +
                "(검증용 임시값이 원복되지 않은 채 커밋에 섞이는 사고가 이 프로젝트에 이미 1회 있었습니다).");
            Assert.IsFalse(_agent.Blackboard.FacingLocked,
                "사이클이 끝났는데 방향 고정이 풀리지 않았습니다 — 캐릭터가 영영 한쪽만 보고 걷습니다.");
            // 정상 종료는 Idle이지만, 배회 AI가 이미 "걷기" 국면에 들어가 있으면 IdleState가 같은
            // 프레임에 Walk로 넘긴다 — 둘 다 정상적인 능동 상태 복귀다(Fall/Ragdoll이면 문제).
            var endState = _agent.Blackboard.Machine.CurrentStateId;
            Assert.IsTrue(endState == StickmanStateId.Idle || endState == StickmanStateId.Walk,
                $"활쏘기가 끝난 뒤 능동 상태로 복귀하지 않았습니다(현재 {endState}).");

            // 접지 유지 — 활쏘기는 캐릭터를 옮기지도, 띄우지도 않는다(사용자 신고 '하늘로 올라감' 회귀 잠금).
            Assert.AreEqual(footAtStart.y, footAtStateEnd.y, 0.05f,
                $"활쏘기 동안 캐릭터 Y가 {footAtStart.y:F2} -> {footAtStateEnd.y:F2}로 바뀌었습니다 — " +
                "이 연출은 캐릭터를 세로로 1픽셀도 옮기지 않아야 합니다.");
            Assert.AreEqual(footholdAtStart, _agent.Blackboard.CurrentFootholdHandle,
                "활쏘기 전후로 딛고 있는 발판이 바뀌었습니다 — 제자리에서 쏴야 합니다.");
            Assert.AreEqual(footAtStart.x, footAtStateEnd.x, 0.35f,
                $"활쏘기 동안 캐릭터 X가 {footAtStart.x:F2} -> {footAtStateEnd.x:F2}로 움직였습니다 — " +
                "쏘는 동안에는 제자리에 서 있어야 합니다.");

            Debug.Log($"[활쏘기테스트] 전체 사이클 통과 — 3발 발사/3발 꽂힘, 시나리오 " +
                $"{_releases[0].Result}/{_releases[1].Result}/{_releases[2].Result}, 종료 시 전부 소멸 + 락 해제.");
        }

        // ============================================================================
        // ③-a ★★ 간헐 #11(2026-09-15) — 사거리 추첨 경계 두 칸(최소·최대)을 결정적으로 잰다
        // ============================================================================

        /// <summary>
        /// 입력 → 기대 결과(추첨은 발동한 그 프레임에 읽고 곧바로 클릭으로 걷는다 — 사이클을 기다리지 않는다):
        /// <list type="bullet">
        /// <item>두 경계 공통: 추첨 사거리 ≥ 절대 하한(설정 × 신장), ≤ <see cref="BandHiCeiling"/>.</item>
        /// <item>상단 경계(roll ≥ 1 − ε): 폭 비례 항이 살아 있는 화면이면 ≥ 아래 울타리(<see cref="WidthTermBinds"/>).</item>
        /// <item>하단 경계(roll ≤ ε): ≥ max(절대 하한, min(s × 화면 폭, f × 상단 사거리)). 밴드 하한 식의 세 항 중
        ///   폭 비례 항을 뺀 값이 하한의 아래 울타리이고, 상단 사거리 ≤ 밴드 상한이라 성립한다.</item>
        /// <item>두 경계의 차 ≥ (1 − 2ε)(1 − f) × 상단 사거리: 밴드가 붕괴하지 않았고 난수 고정이 실제로 추첨을 움직였다.</item>
        /// <item>하단 경계: 진행 방향 화면 끝 여유 &gt; 신장 2배(FullCycle에서 옮긴 단언, 전제를 다시 세웠다).</item>
        /// </list>
        /// 대조 전제(폭 항 생존 · 화면 바닥 구별 가능 · 끝 여유 전제)가 이 화면에서 안 서면 단언을 조용히 건너뛰지 않고
        /// 마지막에 <b>«측정 무효»로 실패</b>시켜 사유를 드러낸다(조용한 초록 금지).
        /// ★ Ignore가 아닌 이유(2026-09-15): 이것은 닫힐 갭이 아니라 «기하 전제가 깨져 이 테스트가 아무것도 못 잰다»는 신호라
        /// 되살릴 역방향 장치가 없다 — TestClaimExpiryAuditTests의 Ignore 명부 규칙과 맞지 않는다. 배치모드 기하(로그의 화면 폭
        /// 30.17~30.18 · 신장 1.706으로 계산)에서는 세 전제가 모두 여유 있게 선다: 폭 항 11.74 대 U0 11.26 · 화면 바닥 6.64 대
        /// 바닥 없는 배선 6.19 · 끝 여유 전제 5.04 대 3.41.
        /// </summary>
        [UnityTest]
        public IEnumerator RangeDrawAtBothRollEdgesStaysInsideTheWidthProportionalBand()
        {
            yield return LoadSceneAndResolve();
            StickConfig cfg = _agent.Config;
            AssertBandConfigMatchesCodeDefaults(cfg);
            var hitbox = _agent.GetComponent<StickmanClickHitbox>();
            Assert.IsNotNull(hitbox, "StickmanClickHitbox가 캐릭터에 없습니다 — 추첨 뒤 연출을 걷을 방법이 없습니다.");

            yield return WaitUntilReadyToShoot(10f, "하단 경계 준비");
            EdgeDraw low = TriggerAtRollEdge(RollEdge.Low, directionRollLow: true, "PlayMode 경계 추첨(하단)");
            yield return null;
            hitbox.SimulateMouseDownForTests();
            yield return null;
            Assert.AreNotEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "하단 경계 추첨 뒤 클릭 취소가 먹지 않았습니다 — 상단 경계를 잴 수 없습니다.");

            yield return WaitUntilReadyToShoot(10f, "상단 경계 준비");
            EdgeDraw high = TriggerAtRollEdge(RollEdge.High, directionRollLow: false, "PlayMode 경계 추첨(상단)");
            yield return null;
            hitbox.SimulateMouseDownForTests();
            yield return null;
            Assert.AreNotEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "상단 경계 추첨 뒤 클릭 취소가 먹지 않았습니다.");

            string Describe(in EdgeDraw d) =>
                $"roll {d.Roll:F4} · 방향난수 {d.DirRoll:F2} · 방향 {(d.Facing > 0f ? "오른쪽" : "왼쪽")} · 발 x {d.FootX:F2} · " +
                $"사거리 {d.Drawn:F2} · 구간 폭 {d.Usable:F2} · 화면 폭 {d.ScreenWidth:F2} · 신장 {d.Height:F3}";
            Debug.Log($"[활쏘기테스트] 경계 추첨 — 하단: {Describe(low)} / 상단: {Describe(high)}");

            // (1) 두 경계 공통 — 한 번의 추첨만으로 성립하는 울타리.
            foreach (EdgeDraw d in new[] { low, high })
            {
                float absFloor = d.Height * cfg.archeryMinTargetDistanceRatio;
                Assert.GreaterOrEqual(d.Drawn, absFloor - 1e-3f,
                    $"경계 추첨 사거리가 절대 하한 {absFloor:F2}유닛 미만입니다 — {Describe(d)}");
                float ceiling = BandHiCeiling(cfg, d.Height, d.Usable);
                Assert.LessOrEqual(d.Drawn, ceiling + 1e-3f,
                    $"경계 추첨 사거리가 밴드 상한 울타리 {ceiling:F2}유닛을 넘었습니다(신고 '화면 끝에만 생김' 재발) — {Describe(d)}");
            }

            // 아래 비교는 두 추첨이 **같은 밴드**에서 나왔을 때만 뜻이 있다(밴드는 발 위치가 아니라 구간·화면 폭·신장으로 정해진다).
            if (low.Foothold != high.Foothold || Mathf.Abs(low.Usable - high.Usable) > 1e-3f
                || Mathf.Abs(low.ScreenWidth - high.ScreenWidth) > 1e-3f || Mathf.Abs(low.Height - high.Height) > 1e-4f)
            {
                Assert.Fail("측정 무효 — 두 경계 추첨의 기하가 달라 서로 비교할 수 없습니다(사이에 발판·화면 폭·신장이 바뀜) — " +
                    $"하단: {Describe(low)} / 상단: {Describe(high)}");
            }

            float h = high.Height;
            float f = Mathf.Clamp01(cfg.archeryMinDistanceSpanFraction);
            float s = Mathf.Max(0f, cfg.archeryMinTargetDistanceScreenFraction);
            float absoluteFloor = h * cfg.archeryMinTargetDistanceRatio;
            float highCeiling = BandHiCeiling(cfg, h, high.Usable);
            var unmeasured = new List<string>(3);

            // (2) 상단 — 폭 비례 항 생존(위 울타리가 헐거워서 통과한 게 아니다).
            if (WidthTermBinds(cfg, h, high.Usable, _renderer.TargetRadius, out float bandHiFloor))
            {
                Assert.GreaterOrEqual(high.Drawn, (1f - RollEdgeEpsilon) * bandHiFloor - 1e-3f,
                    $"상단 경계 추첨이 폭 비례 상한의 아래 울타리 {bandHiFloor:F2}유닛에 못 미칩니다 — 난수 고정이 안 먹었거나 " +
                    $"폭 비례 항(g)이 배선에서 빠졌습니다. {Describe(high)}");
            }
            else unmeasured.Add($"폭 비례 항 생존(구간 폭 {high.Usable:F2}에서 g 항이 U0를 못 이김)");

            // (3) 하단 — 화면 비례 바닥(2026-09-06 신고 처방)이 배선에 살아 있다.
            float screenTerm = Mathf.Min(s * low.ScreenWidth, f * high.Drawn);
            float lowFloor = Mathf.Max(absoluteFloor, screenTerm);
            Assert.GreaterOrEqual(low.Drawn, lowFloor - 1e-3f,
                $"하단 경계 추첨 {low.Drawn:F2}유닛이 밴드 하한의 아래 울타리 {lowFloor:F2}유닛" +
                $"(화면 비례 {s:P0} × 화면 폭 {low.ScreenWidth:F2}, f × 상단 사거리 {f * high.Drawn:F2} 중 작은 값)에 못 미칩니다 — " +
                "화면 비례 바닥이 배선에서 빠졌습니다(신고 '과녁이 너무 가까움' 재발).");
            // 대조: 화면 비례 바닥을 잃은 배선의 하한(절대, f × U0 이하)보다 이 울타리가 **실제로 높아야** (3)이 그 결함을 가른다.
            float floorWithoutScreenTerm = Mathf.Max(absoluteFloor, f * h * cfg.archeryMaxTargetDistanceRatio);
            if (!(lowFloor - 1e-3f > floorWithoutScreenTerm + RollEdgeEpsilon * highCeiling))
                unmeasured.Add($"화면 비례 바닥 구별(울타리 {lowFloor:F2} ≤ 바닥 없는 배선의 하한 {floorWithoutScreenTerm:F2})");

            // (4) 밴드 폭 — 밴드 하한 ≤ f × 밴드 상한(절대 하한이 그 아래일 때). 두 경계 차가 그만큼 벌어져야 한다.
            if (h * Mathf.Max(1f, cfg.archeryMinTargetDistanceRatio) <= f * high.Drawn)
            {
                float minSpread = (1f - 2f * RollEdgeEpsilon) * (1f - f) * high.Drawn;
                Assert.GreaterOrEqual(high.Drawn - low.Drawn, minSpread - 1e-3f,
                    $"두 경계 추첨의 차가 {high.Drawn - low.Drawn:F2}유닛뿐입니다(최소 {minSpread:F2}) — 밴드가 붕괴했거나" +
                    "(«거리는 항상 랜덤» 사망) 난수 고정이 추첨에 닿지 않았습니다.");
            }
            else unmeasured.Add("밴드 폭(절대 하한이 f × 상한을 넘는 좁은 기하)");

            // (5) 하단 경계 — 진행 방향 화면 끝 여유 > 신장 2배(FullCycle에서 옮김).
            //   서는 자리 ≤ 발 x + 캐릭터 여백(물러서면 더 작다)이므로 과녁 ≤ 발 x + 사거리 + 여백.
            //   여백을 보수적으로 ConservativeInsetHeights × 신장으로 잡아, 전제가 서면 합법 배치의 끝 여유는 반드시 2H를 넘는다.
            float lowRoom = low.Facing > 0f ? low.WalkRight - low.FootX : low.FootX - low.WalkLeft;
            float lowGap = low.Facing > 0f ? low.WalkRight - low.TargetX : low.TargetX - low.WalkLeft;
            if (lowRoom - low.Drawn - ConservativeInsetHeights * h > 2f * h)
            {
                Assert.Greater(lowGap, 2f * h,
                    $"과녁이 진행 방향 화면 끝에서 {lowGap:F2}유닛(신장의 {lowGap / h:F1}배)밖에 안 떨어져 있습니다 — " +
                    $"신고 문구 '화면 끝에만 생김'이 재발한 상태입니다. {Describe(low)}");
            }
            else unmeasured.Add($"끝 여유 전제(여유 {lowRoom:F2} − 사거리 {low.Drawn:F2}가 보수 여백 + 2H 이하)");

            if (unmeasured.Count > 0)
                Assert.Fail("측정 무효 — 경계 추첨의 공통 울타리는 통과했지만 이 화면에서 대조 전제가 서지 않아 못 잰 단언이 있습니다(조용한 초록 금지): " +
                    string.Join(" / ", unmeasured));
        }

        // ============================================================================
        // ③-b 활쏘기 중 캐릭터 클릭 -> 즉시 취소 (사용자 요구)
        // ============================================================================

        /// <summary>
        /// 사용자 요구 "활을 쏘는동안은 캐릭터가 클릭이 안됨. 클릭을하면 과녁이랑 활이 없어져야지".
        /// 클릭 한 번으로 과녁/활/화살이 <b>전부</b> 사라지고 락도 반납되는지를 절대 조건으로 잠근다.
        /// </summary>
        [UnityTest]
        public IEnumerator ClickingCharacterDuringArcheryCancelsEverything()
        {
            yield return LoadSceneAndResolve();

            float wait = 0f;
            while (wait < 10f)
            {
                var st = _agent.Blackboard.Machine.CurrentStateId;
                if ((st == StickmanStateId.Idle || st == StickmanStateId.Walk)
                    && _agent.Blackboard.CurrentFootholdHandle != 0L) break;
                wait += Time.deltaTime;
                yield return null;
            }

            _director.ForceTriggerNow("PlayMode 클릭취소 테스트");
            yield return null;
            Assert.AreEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "사전 조건 불성립 — 활쏘기가 시작되지 않았습니다.");

            // ★ 리더 지시: 클릭 취소는 **이동 단계에서도** 동작해야 한다. 여기서는 아직 과녁이 뜨기
            // 전(걸어가는 중)에 클릭해, 그 단계에서도 정상적으로 걷히는지를 확인한다.
            Assert.IsFalse(_renderer.IsVisible,
                "사전 조건 불성립 — 이동 단계여야 하는데 과녁이 벌써 떠 있습니다.");

            // 실제 클릭 경로와 같은 이벤트를 쏜다(새 입력 경로를 만들지 않았음을 함께 확인).
            var hitbox = _agent.GetComponent<StickmanClickHitbox>();
            Assert.IsNotNull(hitbox, "StickmanClickHitbox가 캐릭터에 없습니다.");
            hitbox.SimulateMouseDownForTests();

            yield return null;
            Assert.AreNotEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "캐릭터를 클릭했는데도 여전히 활쏘기 중입니다 — 클릭이 씹히고 있습니다.");

            yield return new WaitForSeconds(1.2f); // 렌더러 아웃트로 여유.
            Assert.IsFalse(_renderer.IsVisible, "클릭 취소 후에도 과녁이 남아 있습니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                $"클릭 취소 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다.");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 그대로 남아 있습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive,
                "클릭 취소 후 SpectacleEventLock이 잡힌 채로 남았습니다 — 이후 다른 연출이 영영 발동하지 못합니다.");
            Assert.IsFalse(_agent.Blackboard.FacingLocked, "클릭 취소 후 방향 고정이 풀리지 않았습니다.");

            Debug.Log("[활쏘기테스트] 클릭 취소 검증 통과 — 클릭 1회로 과녁/활/화살 전부 소멸 + 락 반납.");
        }

        // ============================================================================
        // ④ 궤적 — 확정된 도달점을 반드시 지나고, 그 사이는 직선이 아니라 위로 부푼 포물선이다
        // ============================================================================

        [Test]
        public void TrajectoryPassesThroughPlannedImpactAndArcsAboveTheChord()
        {
            var from = new Vector2(0f, 1.4f);
            var to = new Vector2(5f, 0.9f);
            const float flight = 0.62f;
            const float apex = 0.65f;

            Vector2 start = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, 0f);
            Vector2 end = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, flight);
            Assert.AreEqual(0f, Vector2.Distance(start, from), 1e-3f, "궤적이 발사점에서 시작하지 않습니다.");
            Assert.AreEqual(0f, Vector2.Distance(end, to), 1e-3f,
                "궤적이 **사전 확정된 도달점**에 정확히 도착하지 않습니다 — 이 연출은 물리 시뮬레이션이 " +
                "아니라 역산이므로 오차가 있으면 안 됩니다(리더 지시: 우연에 맡기지 마라).");

            // 현(직선)보다 위로 부푼다 = 포물선으로 보인다. 중점에서의 벗어남이 정확히 apex여야 한다.
            Vector2 mid = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, flight * 0.5f);
            float chordMidY = (from.y + to.y) * 0.5f;
            Assert.AreEqual(apex, mid.y - chordMidY, 1e-3f,
                $"궤적 중점이 현보다 {mid.y - chordMidY:F3}유닛 위입니다 — 설정한 볼록함 {apex:F3}과 달라 " +
                "포물선의 모양이 배율/거리에 따라 달라진다는 뜻입니다.");

            // 전 구간이 현보다 위(= 아래로 처지는 구간이 없다).
            for (int i = 1; i < 20; i++)
            {
                float u = i / 20f;
                Vector2 p = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, flight * u);
                float chordY = Mathf.Lerp(from.y, to.y, u);
                Assert.Greater(p.y, chordY - 1e-4f, $"t={u:F2}에서 궤적이 현 아래로 처졌습니다.");
            }

            // 볼록함 0이면 정확히 직선 — "포물선을 끄는" 경계 동작.
            Vector2 flat = ArcheryRenderer.TrajectoryPoint(from, to, flight, 0f, flight * 0.5f);
            Assert.AreEqual(chordMidY, flat.y, 1e-3f, "볼록함 0인데 직선이 아닙니다.");
        }

        // ============================================================================
        // ④-b 착탄 각도 — 궤적을 아무리 과장해도 **꽂히는 각도**는 합리적 범위 안이다
        // ============================================================================
        // ★ 2026-08-29 사용자 신고 "화살이 과녁에 좀 이상하게 꽂힘". 원인은 두 가지가 겹친 것인데
        //   그중 하나가 "비행 중의 과장된 포물선 접선을 그대로 고정해 꽂았다"이다. 아래 수치는
        //   실행 중인 빌드의 로그에서 그대로 가져온 실제 사격 조건이다
        //   (사거리 25.34유닛, 비행 1.11초, 신장 1.71, archeryArrowArcApexDistanceRatio=0.18).

        private const float RealSpan = 25.34f;      // 실측 사거리(유닛).
        private const float RealFlight = 1.11f;     // 실측 비행 시간(초).
        private const float RealApex = RealSpan * 0.18f;  // 실측 볼록함(= 4.56유닛).

        [Test]
        public void ExaggeratedArcMakesTheRawTangentAbsurdlySteep_NegativeControl()
        {
            // 네거티브 컨트롤: 보정을 되돌리면(= 접선 각도를 그대로 쓰면) 실제로 과도한 각도가 나오는가.
            var from = new Vector2(0f, 1.33f);
            var to = new Vector2(RealSpan, 1.02f);
            float tangent = ArcheryRenderer.ImpactTangentDegrees(from, to, RealFlight, RealApex);
            float descent = ArcheryRenderer.DescentDegrees(tangent);

            Assert.Greater(descent, 35f,
                $"보정 없는 접선 하강각이 {descent:F1}도뿐입니다 — 이 테스트는 '수정을 되돌리면 실제로 " +
                "과도한 각도가 나온다'를 증명하는 네거티브 컨트롤이라, 여기서 완만하면 아래 클램프 " +
                "테스트가 아무것도 증명하지 못합니다.");

            // 볼록함을 키울수록 단조적으로 더 가팔라진다(원인-결과의 방향성 확인).
            float steeper = ArcheryRenderer.DescentDegrees(
                ArcheryRenderer.ImpactTangentDegrees(from, to, RealFlight, RealApex * 2f));
            Assert.Greater(steeper, descent,
                "볼록함을 2배로 키웠는데 착탄 접선이 더 가팔라지지 않았습니다 — 인과가 성립하지 않습니다.");
        }

        [Test]
        public void SettledImpactAngleClampsTheFaceHitNearHorizontal()
        {
            const float faceMax = 14f;
            var from = new Vector2(0f, 1.33f);

            foreach (float dir in new[] { 1f, -1f })   // 좌우 미러링 — 부호를 방향마다 따로 다루면 반드시 한쪽이 틀린다.
            {
                string label = dir > 0f ? "오른쪽" : "왼쪽";
                var to = new Vector2(RealSpan * dir, 1.02f);
                float tangent = ArcheryRenderer.ImpactTangentDegrees(from, to, RealFlight, RealApex);

                Assert.AreEqual(ArcheryRenderer.DescentDegrees(
                        ArcheryRenderer.ImpactTangentDegrees(new Vector2(0f, 1.33f), new Vector2(RealSpan, 1.02f), RealFlight, RealApex)),
                    ArcheryRenderer.DescentDegrees(tangent), 1e-3f,
                    $"{label}으로 쏠 때의 하강각이 오른쪽과 다릅니다 — 좌우 미러링에서 각도 부호가 깨졌습니다.");

                float settled = ArcheryRenderer.SettledImpactAngle(tangent, faceMax, exact: false);
                float settledDescent = ArcheryRenderer.DescentDegrees(settled);
                Assert.LessOrEqual(settledDescent, faceMax + 1e-3f,
                    $"{label}: 보정 후 하강각이 {settledDescent:F1}도로 상한 {faceMax}도를 넘습니다.");
                Assert.Greater(settledDescent, 0f, $"{label}: 보정 후 화살이 코를 들거나 완전히 수평입니다.");

                // 수평 진행 방향은 절대 뒤집히지 않는다(뒤집히면 화살이 반대로 날아온 것처럼 보인다).
                Assert.AreEqual(Mathf.Sign(Mathf.Cos(tangent * Mathf.Deg2Rad)),
                    Mathf.Sign(Mathf.Cos(settled * Mathf.Deg2Rad)),
                    $"{label}: 보정이 화살의 좌우 진행 방향을 뒤집었습니다.");
            }
        }

        [Test]
        public void SettledImpactAngleNeverSteepensAnAlreadyGentleShot()
        {
            // 아주 가까운 사격은 원래도 완만하다 — 클램프가 그것을 **더 가파르게 만들면 안 된다**.
            var from = new Vector2(0f, 1.33f);
            var to = new Vector2(2.5f, 1.20f);
            float tangent = ArcheryRenderer.ImpactTangentDegrees(from, to, 0.4f, 0.2f);
            float raw = ArcheryRenderer.DescentDegrees(tangent);
            float settled = ArcheryRenderer.DescentDegrees(
                ArcheryRenderer.SettledImpactAngle(tangent, 14f, exact: false));
            Assert.LessOrEqual(settled, raw + 1e-3f,
                $"원래 {raw:F1}도로 완만하던 사격이 보정 후 {settled:F1}도로 더 가팔라졌습니다 — " +
                "클램프는 상한이지 목표값이 아닙니다.");
        }

        [Test]
        public void GroundMissUsesAnExactAngleSoDirtStuckArrowsAlwaysLookTheSame()
        {
            const float ground = 38f;
            var from = new Vector2(0f, 1.33f);
            // 사거리가 크게 달라도 땅에 박힌 모양은 같아야 한다.
            foreach (float span in new[] { 4f, 12f, RealSpan })
            {
                float tangent = ArcheryRenderer.ImpactTangentDegrees(
                    from, new Vector2(span, 0f), RealFlight, span * 0.18f);
                float d = ArcheryRenderer.DescentDegrees(
                    ArcheryRenderer.SettledImpactAngle(tangent, ground, exact: true));
                Assert.AreEqual(ground, d, 1e-3f,
                    $"사거리 {span:F1}유닛에서 땅에 꽂힌 각도가 {d:F1}도입니다 — 확정 각도 {ground}도여야 합니다.");
            }
        }

        [Test]
        public void SettleWeightIsZeroUntilTheLastStretchThenReachesOneExactlyAtImpact()
        {
            const float flight = 1.11f;
            float start = flight * (1f - 0.22f);

            Assert.AreEqual(0f, ArcheryRenderer.SettleWeight(0f, flight, start), 1e-5f,
                "발사 직후부터 각도 보정이 걸리면 비행 중의 포물선 회전이 뭉개집니다.");
            Assert.AreEqual(0f, ArcheryRenderer.SettleWeight(start, flight, start), 1e-5f,
                "보정 시작 지점에서 가중치가 0이 아닙니다 — 그 순간 각도가 툭 튑니다.");
            Assert.AreEqual(1f, ArcheryRenderer.SettleWeight(flight, flight, start), 1e-5f,
                "착탄 순간 가중치가 1이 아닙니다 — 꽂힌 각도가 설정값에 도달하지 못합니다.");

            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float w = ArcheryRenderer.SettleWeight(flight * i / 20f, flight, start);
                Assert.GreaterOrEqual(w, prev - 1e-5f, "보정 가중치가 도중에 되돌아갑니다(각도가 흔들립니다).");
                prev = w;
            }

            // 보정 비율 0 = 기능 끄기(신고된 버그 재현 경로) — 착탄 순간에도 가중치가 0이라
            // 접선 각도가 그대로 꽂힌다. 이것이 이번 신고의 재현 조건이다.
            Assert.AreEqual(0f, ArcheryRenderer.SettleWeight(flight, flight, flight), 1e-5f,
                "보정 비율을 0으로 두었는데도 각도가 보정됩니다 — 설정으로 끌 수 없다는 뜻입니다.");
        }

        [Test]
        public void ShippingConfigKeepsTheImpactAngleSane()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                Assert.Greater(cfg.archeryFaceImpactMaxDescentDegrees, 0f,
                    "과녁 면 착탄 상한이 0도면 화살이 완전히 수평으로 꽂혀 '박혔다'가 안 읽힙니다.");
                Assert.LessOrEqual(cfg.archeryFaceImpactMaxDescentDegrees, 25f,
                    $"출하 설정의 과녁 면 착탄 상한이 {cfg.archeryFaceImpactMaxDescentDegrees}도입니다 — " +
                    "이 정도면 신고된 '이상하게 꽂힘'이 그대로 돌아옵니다.");
                Assert.Greater(cfg.archeryGroundImpactDescentDegrees, cfg.archeryFaceImpactMaxDescentDegrees,
                    "땅에 꽂히는 각도가 과녁 면보다 완만합니다 — 땅에 누운 화살처럼 보입니다.");
                Assert.Greater(cfg.archeryImpactSettleRatio, 0f,
                    "착탄 각도 보정 구간이 0입니다 — 검증용으로 껐던 값이 그대로 커밋된 상태입니다.");
                Assert.LessOrEqual(cfg.archeryImpactSettleRatio, 0.4f,
                    "보정 구간이 비행의 40%를 넘습니다 — 화살이 날아가는 내내 각도가 바뀌어 포물선이 뭉개집니다.");
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        // ============================================================================
        // ⑤ 배율 — 1.0 / 0.75(현재 출하) / 0.5에서 배치가 비례한다 + 절대 조건
        // ============================================================================

        private const float BaseHeight = StickConfig.BaselineCharacterTotalHeight;
        private const float BaseHeadRadius = 0.22f;
        private const float BaseShoulderY = 1.7646944f;
        private const float BaseHipY = 0.9346944f;

        /// <summary>StickmanMetrics가 실측하는 소스만 갖춘 최소 리그(RendererScaleRatioTests와 같은 방식).
        /// 프리팹/씬은 배율 하나로 구워지므로 한 번 실행에 두 배율을 동시에 볼 수 없기 때문이다.</summary>
        private GameObject Rig(float scale)
        {
            var root = new GameObject($"ArcheryScaleRig_{scale:F2}");
            float height = BaseHeight * scale;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.4f * scale, height);
            capsule.offset = new Vector2(0f, height * 0.5f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            var outline = new GameObject("HeadOutline");
            outline.transform.SetParent(head.transform, false);
            var lr = outline.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 1;
            lr.SetPosition(0, new Vector3(BaseHeadRadius * scale, 0f, 0f));

            var arm = new GameObject("LeftArm");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0f, BaseShoulderY * scale, 0f);

            var leg = new GameObject("LeftLeg");
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0f, BaseHipY * scale, 0f);

            root.AddComponent<StickmanMetrics>();
            _rigs.Add(root);
            return root;
        }

        [TestCase(1.0f)]
        [TestCase(0.75f)]
        [TestCase(0.5f)]
        public void ArcheryPlacementScalesWithCharacter(float scale)
        {
            GameObject rig = Rig(scale);
            var m = rig.GetComponent<StickmanMetrics>();
            var r = rig.AddComponent<ArcheryRenderer>();
            string label = $"배율 {scale:F2}";

            Assert.AreEqual(BaseHeight * scale, m.TotalHeight, Tol, $"{label}: 리그 신장이 기대치와 다릅니다.");

            // (A) 비율 x 신장 — 바깥에서 온 숫자(설정 기본값)와 맞댄다.
            const float radiusRatio = 0.40f;
            Assert.AreEqual(BaseHeight * scale * radiusRatio, r.TargetRadius, Tol,
                $"{label}: 과녁 반지름이 신장의 {radiusRatio:P0}가 아닙니다 — 절대 상수가 남아 있으면 " +
                "작은 캐릭터 옆에 몸통보다 큰 과녁이 서게 됩니다.");
            Assert.AreEqual(BaseHeight * scale * 0.30f, r.BowHalfLength, Tol, $"{label}: 활 길이가 비례하지 않습니다.");
            Assert.AreEqual(BaseHeight * scale * 0.34f, r.ArrowShaftLength, Tol, $"{label}: 화살 길이가 비례하지 않습니다.");
            Assert.AreEqual(BaseHeight * scale * 0.0339f, r.StrokeWidth, Tol, $"{label}: 획 두께가 비례하지 않습니다.");

            // (B) 절대 조건 — **모든 배율에서** 과녁 꼭대기가 정확히 캐릭터 정수리 높이다.
            // 이 관계가 곧 "화면 세로 판정이 캐릭터 자신의 판정과 같다"는 보증이다(ArcheryDirector 참고).
            Assert.AreEqual(m.HeadTopLocalY, r.TargetCenterLocalY + r.TargetRadius, Tol,
                $"{label}: 과녁 꼭대기({r.TargetCenterLocalY + r.TargetRadius:F4})가 정수리" +
                $"({m.HeadTopLocalY:F4})와 다릅니다 — 캐릭터가 보이는데 과녁만 화면 밖으로 잘릴 수 있습니다.");
            Assert.Greater(r.TargetCenterLocalY - r.TargetRadius, 0f,
                $"{label}: 과녁 아래 끝이 지면 아래로 내려갑니다(받침이 땅에 파묻힙니다).");

            // 궤적 볼록함도 신장에 비례한다.
            Assert.AreEqual(BaseHeight * scale * 0.38f, r.ArcApexHeight, Tol,
                $"{label}: 포물선 볼록함이 비례하지 않습니다 — 작은 캐릭터에서 궤적이 상대적으로 " +
                "훨씬 높이 솟거나 납작해집니다.");
        }

        /// <summary>네거티브 컨트롤 — 종전 방식대로 <b>절대 유닛 상수</b>를 썼다면 위 (B) 절대 조건이
        /// 실제로 깨진다는 것을 같은 식으로 계산해 보인다. 즉 (B)가 통과하는 이유가 "조건이 너무
        /// 헐거워서"가 아님을 같은 파일 안에서 증명한다.</summary>
        [Test]
        public void AbsoluteSizeWouldBreakScaleInvariant()
        {
            // 배율 1.0에서 검증을 마친 값들을 그대로 절대 상수로 굳혔다고 가정.
            const float frozenRadius = BaseHeight * 0.40f;
            const float frozenCenterY = BaseHeight - frozenRadius;

            const float scale = 0.5f;
            float height = BaseHeight * scale;
            float top = frozenCenterY + frozenRadius; // = BaseHeight — 배율과 무관하게 고정.

            Assert.Greater(Mathf.Abs(top - height), 0.05f,
                "절대 상수를 써도 과녁 꼭대기가 정수리와 맞는다면, 위 배율 테스트는 아무것도 " +
                "검증하지 못하고 있다는 뜻입니다.");
            Assert.Greater(top, height,
                $"배율 {scale:F2}에서 절대 상수 과녁의 꼭대기 {top:F3}이 캐릭터 정수리 {height:F3}보다 " +
                "위에 있어야 합니다(= 몸통보다 큰 과녁이 머리 위로 솟는 그림).");
        }

        // ============================================================================
        // ⑥ 화면 밖으로 나가면 안 된다 — 정면 -> 미러링 -> 발동 포기
        // ============================================================================

        /// <summary>
        /// 발판 종류 판정 — 화면 최하단 안전망(합성 발판)만 "바탕화면"이고, Dock과 실제 창은 둘 다
        /// "창"이다(사용자 명시: "창 일 경우 그 창의 전체 길이의 끝으로 이동"). 이 분류가 뒤집히면
        /// 바탕화면에서 짧게 쏘거나 좁은 창에서 화면 절반을 요구해 발동 자체가 사라진다.
        /// </summary>
        [Test]
        public void FootholdKindClassificationMatchesUserSpec()
        {
            Assert.IsFalse(ArcheryDirector.IsRealWindowFoothold(FallbackPlatformWindowService.SyntheticFootholdHandle),
                "화면 최하단 왼쪽 안전망을 '창'으로 분류했습니다 — 바탕화면이어야 합니다.");
            Assert.IsFalse(ArcheryDirector.IsRealWindowFoothold(FallbackPlatformWindowService.SyntheticFootholdHandleRight),
                "화면 최하단 오른쪽 안전망을 '창'으로 분류했습니다 — 바탕화면이어야 합니다.");
            Assert.IsTrue(ArcheryDirector.IsRealWindowFoothold(FallbackPlatformWindowService.DockFootholdHandle),
                "Dock을 '창'으로 분류하지 않았습니다 — 사용자가 Dock을 창에 포함시켰습니다.");
            Assert.IsTrue(ArcheryDirector.IsRealWindowFoothold(12345L),
                "실제 창 핸들을 '창'으로 분류하지 않았습니다.");
            Assert.IsFalse(ArcheryDirector.IsRealWindowFoothold(0L),
                "'발판 없음'(핸들 0)을 창으로 분류했습니다.");
        }

        /// <summary>과녁 중심 높이가 반지름에서 유도된다는 관계(설정값 이중화 금지)를 못박는다.</summary>
        [TestCase(1.0f)]
        [TestCase(0.75f)]
        [TestCase(0.5f)]
        public void TargetCenterHeightIsDerivedFromRadius(float scale)
        {
            float height = BaseHeight * scale;
            float radius = height * 0.40f;
            Assert.AreEqual(height - radius, ArcheryDirector.TargetCenterHeight(height, radius), Tol,
                "과녁 중심 높이가 '신장 - 반지름'이 아닙니다 — 이 관계가 깨지면 과녁 꼭대기가 " +
                "정수리와 어긋나고, 디렉터의 세로 화면 판정도 함께 틀어집니다.");
        }

        // ============================================================================
        // ⑦ 손이 잡는 곳 = 활대 한가운데 (2026-08-31 사용자 신고)
        // ============================================================================

        /// <summary>
        /// ★ 신고 원문: "활 쏠 때 활대를 잡아야 하는데 이상한 데를 잡고 쏨".
        ///
        /// 활 로컬 좌표계는 <b>시위선이 x=0</b>이고 활대가 +x로 불룩하다. 예전 코드는 활 루트를 손끝에
        /// 그대로 놓아 <b>원점(시위선 한가운데)</b>이 손에 왔다 — 그 지점은 활대에서 신장의 15.5% 뒤,
        /// 당겨진 시위의 V자보다는 앞인 <b>빈 공간</b>이라 손이 아무것도 쥐지 않은 그림이 된다.
        ///
        /// 이 테스트가 잠그는 절대 조건: <b>어떤 조준각/배율에서도 활대 한가운데(BowLimbLocal(0))의
        /// 컨테이너 로컬 위치가 손끝과 정확히 일치</b>한다. 각도를 여러 개 도는 이유는 그립 오프셋을
        /// 회전에 태우지 않고 x축으로만 빼는 흔한 실수(조준각이 0이 아닐 때만 어긋난다)를 잡기 위해서다.
        /// </summary>
        [TestCase(1.0f)]
        [TestCase(0.75f)]
        [TestCase(0.5f)]
        public void BowIsHeldByItsGripNotByTheString(float scale)
        {
            GameObject rig = Rig(scale);
            var r = rig.AddComponent<ArcheryRenderer>();
            string label = $"배율 {scale:F2}";

            // (A) 활 모양 자체의 계약 — 활대 한가운데는 t=0, 양 끝(시위 매듭)은 x=0.
            Assert.AreEqual(r.BowGripLocalX, r.BowLimbLocal(0f).x, Tol,
                $"{label}: 활대 한가운데의 x가 그립 오프셋과 다릅니다 — 두 값이 서로 다른 식에서 나오면 " +
                "손 위치와 그림이 조용히 어긋납니다.");
            Assert.AreEqual(0f, r.BowLimbLocal(0f).y, Tol, $"{label}: 그립이 활 상하 한가운데가 아닙니다.");
            Assert.AreEqual(0f, r.BowLimbLocal(1f).x, Tol, $"{label}: 활 위쪽 끝이 시위선(x=0) 위에 없습니다.");
            Assert.AreEqual(0f, r.BowLimbLocal(-1f).x, Tol, $"{label}: 활 아래쪽 끝이 시위선(x=0) 위에 없습니다.");
            Assert.AreEqual(r.BowHalfLength, r.BowLimbLocal(1f).y, Tol, $"{label}: 활 길이가 어긋납니다.");
            Assert.Greater(r.BowGripLocalX, 0f, $"{label}: 활대가 앞으로 휘지 않았습니다(그립이 시위선 위).");

            // (B) 절대 조건 — 조준각과 무관하게 그립이 손끝에 온다.
            var hands = new[] { new Vector2(0.72f, 1.97f) * scale, new Vector2(-3.1f, 0.9f) * scale };
            foreach (float aim in new[] { -35f, -12f, 0f, 7f, 18f, 44f, 90f, 173f, -168f })
            {
                Quaternion rot = Quaternion.Euler(0f, 0f, aim);
                foreach (Vector2 hand in hands)
                {
                    Vector2 root = ArcheryRenderer.ResolveBowRootLocal(hand, aim, r.BowGripLocalX);
                    Vector2 gripWorld = root + (Vector2)(rot * (Vector3)r.BowLimbLocal(0f));
                    Assert.AreEqual(hand.x, gripWorld.x, Tol,
                        $"{label}/조준 {aim}도: 활대 한가운데의 x가 손끝과 다릅니다.");
                    Assert.AreEqual(hand.y, gripWorld.y, Tol,
                        $"{label}/조준 {aim}도: 활대 한가운데의 y가 손끝과 다릅니다.");

                    // 네거티브 컨트롤 — 예전처럼 루트를 손에 그대로 놓았다면(그립 오프셋 0) 활대가
                    // 손에서 이만큼 떨어진다. "눈에 안 띌 만큼 작은 차이"가 아님을 같은 식으로 보인다.
                    Vector2 oldGrip = hand + (Vector2)(rot * (Vector3)r.BowLimbLocal(0f));
                    float err = Vector2.Distance(oldGrip, hand);
                    Assert.AreEqual(r.BowGripLocalX, err, Tol,
                        $"{label}/조준 {aim}도: 네거티브 컨트롤 계산이 그립 오프셋과 다릅니다.");
                    Assert.Greater(err, r.BowHalfLength * 0.4f,
                        $"{label}: 옛 배치의 어긋남이 활 반길이의 40%도 안 됩니다 — 그렇다면 이 테스트는 " +
                        "아무것도 증명하지 못합니다(신고된 그림이 재현되지 않는다는 뜻).");
                }
            }
        }

        /// <summary>
        /// 만작에서 <b>시위에 걸린 화살의 촉이 활대에 닿는다</b> — 손-그립 일치와 독립적으로 같은
        /// 결론을 요구하는 두 번째 증거다(활 전체가 앞뒤로 밀려 있으면 이 관계부터 깨진다).
        /// 오늬는 뒷손이 정하지만, 그 최대 당김폭(BowMaxPull)과 화살대 길이는 상수라 검산할 수 있다.
        /// </summary>
        [Test]
        public void FullDrawArrowTipReachesTheBowStave()
        {
            GameObject rig = Rig(1f);
            var r = rig.AddComponent<ArcheryRenderer>();

            // 활 로컬에서 오늬 x = -당김폭, 촉 x = 오늬 + 화살대. 그 촉이 활대(그립 x) 근처에 와야 한다.
            float tipX = -r.BowMaxPull + r.ArrowShaftLength;
            Assert.AreEqual(r.BowGripLocalX, tipX, r.BowHalfLength * 0.35f,
                $"만작 화살촉 x={tipX:F3}이 활대 x={r.BowGripLocalX:F3}에서 너무 멉니다 — 화살이 활을 " +
                "관통했거나(+) 활 앞에 한참 못 미칩니다(-).");
        }
    }
}
