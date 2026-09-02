using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 사용자 신고 "그라피티 그릴때 윈도우버전은 캐릭터가 미끄러져이동함"의 회귀 잠금
    /// (2026-09-02, 디버거가 실측으로 원인 확정).
    ///
    /// ============================================================================
    /// 무엇이 버그였나 — 마찰이 원리적으로 0이었다
    /// ============================================================================
    /// <see cref="StickmanBlackboard.ApplyGroundedGravitySuppression"/>이 접지 중
    /// <c>gravityScale = 0</c>으로 만든다 = <b>수직항력 N이 0</b>이다. 쿨롱 마찰 상한은 μN이므로
    /// N=0이면 마찰 상한도 0이고, 걷다가 연출 상태로 들어간 잔여 수평속도는 <b>감속 없이 등속으로</b>
    /// 연출이 끝날 때까지 유지된다. 실측: 3.4초 동안 192pt, 감속 -0.68 pt/s²(사실상 0).
    /// 같은 몸·같은 Dock 콜라이더에서 중력 ON(Fall)은 11.77 u/s²로 0.061유닛 만에 서는데,
    /// 중력 억제(Graffiti)는 3.94유닛을 가속도 0으로 갔다.
    ///
    /// <para>이건 그 수정의 결함이 아니라 <b>부작용</b>이다 — 마찰을 되살리려면 중력을 되살려야 하고
    /// 그러면 원래 신고("창에서 가끔 갑자기 떨어짐")가 돌아온다. 그래서 세로축에서 한 일
    /// (<see cref="StickmanBlackboard.TickGroundKeepingSafetyNet"/>)을 가로축에도 똑같이 한다.</para>
    ///
    /// ============================================================================
    /// ★ 이 파일이 지키는 검증 규율 (2026-09-02 리더 지시 — 같은 밤에 거짓 통과 9건이 나왔다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>시간 예산은 전부 벽시계(초)</b>다. <c>for (i &lt; N프레임)</c> 예산은 쓰지 않는다 —
    ///         이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌아서 180프레임 예산이 실제로는
    ///         0.01~0.08초밖에 안 된다(네 라운드 동안 "불안정"으로 오판된 사고).</item>
    ///   <item><b>네거티브 컨트롤이 짝으로 있다</b>. 스위치를 끄면 표류가 <b>실제로 되살아나는지</b>를
    ///         같은 시나리오에서 잰다. 그게 없으면 초록이 아무것도 증명하지 못한다.</item>
    ///   <item><b>프로덕션 상수를 숫자로 베끼지 않는다</b>. 정지 박자/걷기 속도는 전부
    ///         <see cref="StickConfig"/>에서 읽어 상한을 <b>유도</b>한다.</item>
    /// </list>
    /// </summary>
    public sealed class HorizontalDriftSafetyNetTests
    {
        private const string LogPrefix = "[수평표류-TEST]";

        /// <summary>표류를 관측하는 벽시계 예산(초). 브레이크가 걸리면 0.14초 안에 끝나고, 안 걸리면
        /// 이 시간 내내 등속으로 간다 — 두 결과의 차이가 최소 3배 이상 벌어지도록 잡은 값이다.</summary>
        private const float DriftWindowSeconds = 1.0f;

        /// <summary>발판을 물리 바닥에서 이만큼 위에 둔다(GroundedGravitySuppressionTests와 같은 이유 —
        /// 물리 콜라이더가 개입해 마찰이 생기면 이 관측 자체가 무의미해진다).</summary>
        private const float FootholdAboveFloorUnits = 6f;

        private const long TestFootholdHandle = 992001L;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class FixedIntentSource : IMovementIntentSource
        {
            public float Horizontal;
            public float MoveInputX => Horizontal;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private FixedIntentSource _intent;
        private Vector2 _savedOrigin;
        private TestFootholdService _service;
        private float _footholdTopWorldY;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                StickmanBlackboard bb = _agent.Blackboard;
                bb.ReleaseGroundedGravitySuppression();
                if (_originalConfig != null) bb.Config = _originalConfig;
                if (_originalIntent != null) bb.IntentSource = _originalIntent;
                if (_originalPoller != null) bb.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        // ====================================================================
        // 공통 준비 — GroundedGravitySuppressionTests와 같은 리그(같은 버그 가족이라 형상을 맞춘다)
        // ====================================================================

        private IEnumerator SetUpGroundedOnLogicalFoothold()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            yield return new WaitForSeconds(0.7f);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;
            // 물리 계단이 있으면 진짜 콜라이더가 생겨 마찰이 살아난다 — 그러면 이 신고의 조건
            // ("논리 발판 위, 수직항력 0")이 성립하지 않아 재현 자체가 무의미해진다.
            _clonedConfig.dockPhysicsStepEnabled = false;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            _footholdTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y + FootholdAboveFloorUnits;

            float footholdTopOsY = ScreenCoordinateConverter.WorldToOsScreen(bb.MainCamera,
                new Vector2(0f, _footholdTopWorldY), _clonedConfig, out _).y;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(TestFootholdHandle,
                new Rect(0f, footholdTopOsY, Screen.width, Mathf.Max(1f, Screen.height - footholdTopOsY)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _intent = new FixedIntentSource();
            bb.IntentSource = _intent;

            bb.MoveBodyToWorld(new Vector2(0f, _footholdTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = TestFootholdHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(StickmanStateId.Idle, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — 발판 위에서 Idle을 유지하지 못했습니다.");
            Assert.IsTrue(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} 전제 실패 — 접지 중력 억제가 걸려 있지 않습니다. 이 억제가 곧 '수직항력 0 = " +
                "마찰 0'이라는 이 신고의 물리적 전제이므로, 걸려 있지 않으면 재현이 성립하지 않습니다.");

            Debug.Log($"{LogPrefix} 준비 — 발판 상단 월드Y={_footholdTopWorldY:F3}, 걷기속도={EntrySpeed():F3}유닛/초, " +
                $"정지박자={_clonedConfig.horizontalDriftBrakeSeconds:F3}초, " +
                $"안전망={_clonedConfig.horizontalDriftSafetyNetEnabled}.");
        }

        /// <summary>표류를 만들 진입 속도 — 걷다가 연출로 들어간 상황이므로 <b>걷기 속도</b> 그대로다
        /// (숫자를 베끼지 않고 설정에서 유도한다).</summary>
        private float EntrySpeed() => _clonedConfig.ResolveWalkSpeed();

        /// <summary>
        /// "걷다가 연출 상태로 들어갔다"를 만든 뒤, <b>벽시계</b> <see cref="DriftWindowSeconds"/>초 동안
        /// 실제로 이동한 수평 거리(월드 유닛)를 잰다.
        /// </summary>
        private IEnumerator MeasureSpectacleDrift(StickmanStateId spectacle, System.Action<float, float> onDone)
        {
            StickmanBlackboard bb = _agent.Blackboard;

            bb.Body.linearVelocity = new Vector2(EntrySpeed(), 0f);
            bb.Machine.ChangeState(spectacle);
            yield return null;   // 전이 프레임(Enter만 돌고 Tick은 다음 프레임부터다)

            float startX = bb.Body.position.x;
            yield return new WaitForSeconds(DriftWindowSeconds);   // ★ 벽시계 예산(프레임 수 아님)

            float drift = Mathf.Abs(bb.Body.position.x - startX);
            float endVelocityX = bb.Body.linearVelocity.x;
            onDone(drift, endVelocityX);

            Debug.Log($"{LogPrefix} {spectacle} — 벽시계 {DriftWindowSeconds:F2}초 동안 수평 이동 {drift:F4}유닛, " +
                $"끝 속도 {endVelocityX:F6}유닛/초, 상태={bb.Machine.CurrentStateId}, 발판핸들={bb.CurrentFootholdHandle}.");
        }

        // ====================================================================
        // H1 ★핵심★ — 연출 중 표류가 정지 박자 안에서 멎는다
        // ====================================================================

        [UnityTest]
        public IEnumerator H1_그라피티_중_잔여속도가_정지박자_안에서_0이_된다()
        {
            yield return SetUpGroundedOnLogicalFoothold();

            float drift = 0f, endVx = 0f;
            yield return MeasureSpectacleDrift(StickmanStateId.Graffiti, (d, v) => { drift = d; endVx = v; });

            StickmanBlackboard bb = _agent.Blackboard;

            // 상한은 유도값이다: 어떤 단조 감속이든 정지 박자 안에 0이 되면 이동 거리는
            // 진입속도 x 정지박자를 넘을 수 없다(선형 램프의 실제 값은 그 절반이다).
            float bound = EntrySpeed() * _clonedConfig.horizontalDriftBrakeSeconds;
            Assert.Less(drift, bound,
                $"{LogPrefix} 연출 중 표류가 정지 박자 상한({bound:F4}유닛)을 넘었습니다 — " +
                "수평 표류 안전망이 동작하지 않습니다(신고 '그라피티 그릴때 캐릭터가 미끄러져이동함' 재발).");

            Assert.AreEqual(0f, endVx, 1e-6f,
                $"{LogPrefix} 정지 박자가 지났는데 수평 속도가 정확히 0이 아닙니다({endVx:E3}). " +
                "지수 감쇠는 원리적으로 0에 도달하지 못하므로 선형 램프여야 합니다.");

            Assert.AreEqual(StickmanStateId.Graffiti, bb.Machine.CurrentStateId,
                $"{LogPrefix} 관측 도중 상태가 {bb.Machine.CurrentStateId}로 바뀌었습니다 — 표류가 아니라 " +
                "다른 사건(발판 상실/랙돌)을 잰 것이라 이 측정은 무효입니다.");
            Assert.AreEqual(TestFootholdHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 관측 도중 발판을 놓았습니다 — 측정 전제가 무너졌습니다.");
        }

        // ====================================================================
        // H1n — 네거티브 컨트롤: 스위치를 끄면 표류가 실제로 되살아난다
        //       (이게 없으면 H1의 초록은 "원래 안 미끄러졌다"와 구분되지 않는다)
        // ====================================================================

        [UnityTest]
        public IEnumerator H1n_네거티브컨트롤_안전망을_끄면_같은조건에서_등속으로_미끄러진다()
        {
            yield return SetUpGroundedOnLogicalFoothold();

            _clonedConfig.horizontalDriftSafetyNetEnabled = false;   // 수정을 되돌린다
            yield return null;

            float drift = 0f, endVx = 0f;
            yield return MeasureSpectacleDrift(StickmanStateId.Graffiti, (d, v) => { drift = d; endVx = v; });

            // 마찰이 0이므로 표류는 (진입속도 x 시간)에 수렴한다. 절반만 넘어도 "등속으로 밀린다"는
            // 결론에는 충분하며, H1의 상한(진입속도 x 0.14초)과는 자릿수가 다르다.
            float revivedFloor = EntrySpeed() * DriftWindowSeconds * 0.5f;
            float brakedCeiling = EntrySpeed() * _clonedConfig.horizontalDriftBrakeSeconds;

            Debug.Log($"{LogPrefix} H1n — 안전망 OFF 표류 {drift:F4}유닛(되살아남 기준 {revivedFloor:F4}, " +
                $"안전망 ON 상한 {brakedCeiling:F4}). 두 값의 간격이 이 테스트의 분해능이다.");

            Assert.Greater(drift, revivedFloor,
                $"{LogPrefix} 안전망을 껐는데도 표류가 되살아나지 않았습니다 — 이 시나리오가 애초에 " +
                "버그를 재현하지 못한다는 뜻이라 H1의 초록은 아무것도 증명하지 못합니다(관측 전제 붕괴).");
            Assert.Greater(Mathf.Abs(endVx), 0f,
                $"{LogPrefix} 안전망 OFF인데 수평 속도가 스스로 0이 됐습니다 — 어딘가에 마찰이 살아 있다는 " +
                "뜻이고, 그러면 이 리그는 신고 상황(수직항력 0)을 재현하지 못합니다.");
        }

        // ====================================================================
        // H2 — 정지 박자가 실제로 존재한다(즉시 대입이 아니다)
        // ====================================================================

        [UnityTest]
        public IEnumerator H2_정지까지_걸린_벽시계시간이_설정한_정지박자와_일치한다()
        {
            yield return SetUpGroundedOnLogicalFoothold();

            StickmanBlackboard bb = _agent.Blackboard;
            bb.Body.linearVelocity = new Vector2(EntrySpeed(), 0f);
            bb.Machine.ChangeState(StickmanStateId.Graffiti);
            yield return null;

            float brakeSeconds = _clonedConfig.horizontalDriftBrakeSeconds;
            float t0 = Time.time;                    // ★ 벽시계(스케일 타임) — 프레임 수를 세지 않는다
            float deadline = t0 + brakeSeconds * 10f;
            float observedRate = 0f;
            while (bb.Body.linearVelocity.x != 0f && Time.time < deadline)
            {
                observedRate = Mathf.Max(observedRate, bb.HorizontalDriftBrakeRate);
                yield return null;
            }
            float elapsed = Time.time - t0;

            // 감속률은 상수가 아니라 **진입 속도에서 유도**된다 — 그래야 캐릭터 배율/화면 기하가
            // 바뀌어도 정지 박자(초)가 고정된다. 그 유도가 실제로 일어났는지를 값으로 확인한다.
            float expectedRate = EntrySpeed() / brakeSeconds;
            Debug.Log($"{LogPrefix} H2 — 정지까지 벽시계 {elapsed * 1000f:F1}ms(설정 정지박자 {brakeSeconds * 1000f:F0}ms), " +
                $"관측 감속률 {observedRate:F3}유닛/초² (유도 기대값 {expectedRate:F3}).");
            Assert.AreEqual(expectedRate, observedRate, expectedRate * 0.1f,
                $"{LogPrefix} 감속률이 진입 속도에서 유도되지 않았습니다(관측 {observedRate:F3}, 기대 {expectedRate:F3}). " +
                "상수로 박아 두면 캐릭터 배율이나 화면 기하가 바뀔 때 정지 박자가 함께 바뀝니다.");

            Assert.AreEqual(0f, bb.Body.linearVelocity.x, 1e-6f,
                $"{LogPrefix} 정지 박자의 10배가 지나도록 수평 속도가 0이 되지 않았습니다.");
            Assert.Greater(elapsed, brakeSeconds * 0.5f,
                $"{LogPrefix} 정지가 너무 빠릅니다({elapsed * 1000f:F1}ms) — 즉시 대입이라는 뜻이고, 그러면 " +
                "포즈는 지수 스무딩으로 녹는데 몸만 한 프레임에 얼어붙는 급정지 튐이 생깁니다.");
            Assert.Less(elapsed, brakeSeconds * 2.5f,
                $"{LogPrefix} 정지가 너무 느립니다({elapsed * 1000f:F1}ms) — 감속률 유도가 진입 속도를 " +
                "제대로 반영하지 못한다는 뜻입니다.");
        }

        // ====================================================================
        // H3 — 수평을 스스로 소유하는 상태는 손대지 않는다
        //      (여기서 실수하면 접근 보행이 지워져 "영원히 도착하지 못한다")
        // ====================================================================

        [UnityTest]
        public IEnumerator H3_자기소유_상태의_수평속도는_안전망이_건드리지_않는다()
        {
            yield return SetUpGroundedOnLogicalFoothold();

            StickmanBlackboard bb = _agent.Blackboard;
            foreach (StickmanStateId id in System.Enum.GetValues(typeof(StickmanStateId)))
            {
                if (!StickmanBlackboard.IsHorizontalMotionSelfManaged(id)) continue;

                bb.Machine.ChangeState(id, isForcedInterrupt: true);
                float speed = EntrySpeed();
                bb.Body.linearVelocity = new Vector2(speed, 0f);
                // 상태 Tick이 끼어들지 않게 **같은 프레임 안에서** 안전망만 직접 돌린다.
                bb.TickHorizontalDriftSafetyNet(1f / 60f);

                Assert.AreEqual(speed, bb.Body.linearVelocity.x, 1e-5f,
                    $"{LogPrefix} {id}는 수평 이동을 스스로 소유하는 상태인데 안전망이 속도를 건드렸습니다. " +
                    "접근 페이즈가 있는 상태(활쏘기 등)에서 이 실수는 '영원히 도착하지 못함'으로 나타납니다.");
            }

            Debug.Log($"{LogPrefix} H3 — 자기소유 상태 전부에서 안전망이 손대지 않음을 확인했습니다.");
        }

        // ====================================================================
        // H4 — 실제 보행이 살아 있다(H3의 직접 호출이 아니라 **정상 경로**로 확인)
        // ====================================================================

        [UnityTest]
        public IEnumerator H4_안전망이_켜져있어도_걷기는_정상적으로_이동한다()
        {
            yield return SetUpGroundedOnLogicalFoothold();

            StickmanBlackboard bb = _agent.Blackboard;
            _intent.Horizontal = 1f;
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
            yield return null;

            float startX = bb.Body.position.x;
            const float WalkWindowSeconds = 0.5f;
            yield return new WaitForSeconds(WalkWindowSeconds);   // ★ 벽시계 예산
            float moved = bb.Body.position.x - startX;

            // 가속 램프/스무딩이 있으므로 이론값의 절반만 넘으면 "정상적으로 걷는다"에 충분하다.
            float floor = EntrySpeed() * WalkWindowSeconds * 0.5f;
            Debug.Log($"{LogPrefix} H4 — 걷기 {WalkWindowSeconds:F2}초에 {moved:F4}유닛 이동(기준 {floor:F4}), " +
                $"상태={bb.Machine.CurrentStateId}.");

            Assert.Greater(moved, floor,
                $"{LogPrefix} 안전망이 켜진 상태에서 걷기가 제대로 이동하지 못했습니다 — Walk가 " +
                "IsHorizontalMotionSelfManaged에서 빠졌거나 안전망 호출 순서가 상태 Tick보다 앞섰다는 뜻입니다.");
        }
    }
}
