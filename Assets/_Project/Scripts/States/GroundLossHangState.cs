using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// ★ 발판 상실 공중 유예 — "딛고 있던 창이 사라졌는데 아직 떨어지지는 않는" 구간의 상태
    /// (2026-09-01, 소은 실측 + 리더 결정 "(C) 시간은 두고 연출을 붙인다").
    ///
    /// ============================================================================
    /// 왜 상태로 승격했나 (플래그였던 것을)
    /// ============================================================================
    /// 유예 동안 <b>몸을 붙잡아 두는 것</b>은 "창에서 갑자기 떨어짐" 수정의 본체이고 분리할 수 없다 —
    /// 폴링 한 주기(0.3초)만 자유낙하해도 1.32유닛 = 접지 허용오차 0.489유닛의 2.7배라 튐이 지나가도
    /// 발판으로 돌아올 방법이 없기 때문이다(StickmanBlackboard.GroundedTick의 유예 주석 참고).
    /// 그런데 그 붙잡음의 부작용이 실측으로 드러났다:
    /// <code>
    ///   IDLE 중이면 : 모자 상단 y가 10프레임 넘게 1픽셀도 안 움직임(화소차 0.00%)
    ///                 -> "만화적 연출"이 아니라 **"앱이 멈췄다 / 렉이다"** 로 읽힌다.
    ///   WALK 중이면 : 허공을 수평으로 걸어간다(다리가 계속 돌아간다)
    ///                 -> 와일 E. 코요테 그대로. 귀엽다.
    /// </code>
    /// 같은 빌드·같은 물리·같은 지속시간에서 갈린 결과라, 문제는 "0.45초"라는 길이가 아니라
    /// <b>그 시간에 생명 신호가 있느냐</b>다. 그래서 시간 단축안은 기각됐고 연출을 붙이기로 했는데,
    /// 이 프로젝트의 규약은 <b>"상태 ID 하나로 포즈가 결정된다"</b>(StickmanBlackboard.TickPose)이다.
    /// 플래그를 보고 Idle 포즈를 예외 처리하면 "같은 상태인데 포즈가 두 가지"가 되어 반드시 어긋나므로,
    /// 포즈를 붙이려면 상태 승격이 규약과 맞다(리더 결정 승인사항 2).
    ///
    /// ============================================================================
    /// 왜 Idle/Walk에서만 승격하는가 (스펙터클 상태는 예전처럼 플래그로 붙잡는다)
    /// ============================================================================
    /// 유예의 존재 이유는 <b>"창 열거가 한 번 튄 것"을 흡수해 아무 일도 없던 것처럼 만드는 것</b>이다.
    /// 그런데 Archery/WindowTheft/TimedSpectacle 같은 상태에서까지 이 상태로 전이시키면, 열거가 한 번
    /// 튈 때마다 진행 중이던 연출이 <b>중도에 취소된다</b> — 유예가 흡수하려던 바로 그 사건이 유예 때문에
    /// 눈에 보이는 사고로 바뀐다. 그 상태들은 포즈를 스스로 소유하므로 "같은 상태인데 포즈가 두 가지"
    /// 문제도 애초에 생기지 않는다. 그래서 승격 범위를 <b>포즈 주인이 TickPose 기본 경로인 Idle/Walk</b>로
    /// 좁혔다(그 두 상태가 곧 소은이 실측한 두 케이스이기도 하다).
    /// 나머지 상태의 붙잡음은 <c>StickmanBlackboard._graceHoldFrame</c>이 예전 그대로 담당한다.
    ///
    /// ============================================================================
    /// 나가는 길 — 전부 여기 적는다 (★ 갇히면 캐릭터가 영원히 공중에 뜬다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>유예 만료</b> -> Fall. <see cref="StickmanBlackboard.GroundedTick"/>이 확정한다(공용 경로).</item>
    ///   <item><b>발판 복귀</b> -> Idle/Walk(그 순간의 이동 의도로 분기). 유예의 설계 목적이 성립한 경우다.</item>
    ///   <item><b>발밑이 정말 비었다</b>(걸어서 모서리를 넘었거나 붙잡은 발판이 없다) -> 즉시 Fall.
    ///         붙잡으면 그건 공중부양이다(GroundSensor.GroundInfo.WalkedOffPreferredFoothold).</item>
    ///   <item><b>화면 좌우 이탈</b> -> Fall(<see cref="StickmanBlackboard.CheckScreenBoundsOrFall"/>).</item>
    ///   <item><b>스냅 상한 초과</b>(딛던 발판이 몸을 지나쳐 크게 움직임) -> Fall(GroundedTick 내부).</item>
    ///   <item><b>외력</b> -> Ragdoll 강제 인터럽트. RagdollImpactResolver는 상태 목록을 보지 않으므로
    ///         새 상태가 생겨도 자동으로 옳다(그 파일의 "판정 기준을 부딪힌 대상으로 바꾼다" 주석).</item>
    ///   <item><b>드래그/스펙터클/전체화면 취소</b> -> 외부에서 ChangeState. 이 상태는 Exit()에서
    ///         되돌릴 것이 없고(포즈는 다음 프레임 TickPose가 상태 ID로 다시 정한다) 중력 억제는
    ///         StickmanAgent.Update가 매 프레임 벗겼다 얹으므로 잔재가 남지 않는다.</item>
    ///   <item><b>★ 최후 안전망</b> — 위가 전부 실패해도 <see cref="StickConfig.ResolveGroundLossHangHardTimeout"/>
    ///         (유예의 3배)를 넘기면 <b>자기 시계로</b> Fall로 나간다. 유예 타이머는 블랙보드가 소유하고
    ///         여러 경로가 리셋할 수 있으므로 상태가 스스로도 한 번 더 끊는다. 이 경로가 발동하면
    ///         조용히 넘어가지 않고 경고를 남긴다(발동 자체가 결함의 증거다).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 대사는 만들지 않는다 (명시적 결정)
    /// ============================================================================
    /// 상태로 승격됐으므로 이제 원칙 1을 지키며 대사를 파생시킬 수 있지만, 리더 결정 승인사항 3에 따라
    /// <b>연출만</b> 넣는다 — 사용자가 요청하지 않은 연출/대사에 반복적으로 불만을 표한 이력이 있다.
    /// 그래서 이 파일에는 <c>DialogueIntent</c>가 없고 <c>IHasDialogueParams</c>도 구현하지 않는다.
    /// </summary>
    public sealed class GroundLossHangState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        /// <summary>이 상태에 머문 시간(초). 연출 위상의 시계이자 갇힘 방지 상한의 시계다.</summary>
        private float _elapsed;

        /// <summary>
        /// 무반응 구간이 끝난 뒤 연출 세기가 0 -> 1로 차오르는 데 쓰는 <b>생명 신호 구간의 비율</b>.
        /// StickConfig로 올리지 않은 이유: 이것은 사용자가 조절할 연출 파라미터가 아니라 "어떤 자세에서
        /// 들어와도 튀지 않게 하는" 이음매 처리다(FallState.UpwardLandingVelocityEpsilon,
        /// StickmanPoseAnimator.StruggleArmFrequencyRatio와 같은 계열의 형태 상수).
        /// 0.25 = 생명 신호 구간 0.38초 기준 약 0.095초 — 지수 감쇠 포즈 스무딩(35/초, 시정수 0.029초)의
        /// 3배가 넘어 램프가 스무딩에 먹히지 않고, 그러면서도 첫 걸음을 잡아먹지 않는 길이다.
        /// </summary>
        private const float ScrambleRampFraction = 0.25f;

        /// <summary>같은 진단 로그를 최소 이 간격으로만 남긴다(초, 벽시계). 이 앱은 하루 종일 켜져
        /// 있고 창 열거 튐은 반복될 수 있어, 로그가 유일한 판별 수단이라도 매번 남기면 안 된다
        /// (StickmanBlackboard.ScreenClampLogMinIntervalSeconds와 같은 관례).</summary>
        private const float LogMinIntervalSeconds = 5f;

        // 인스턴스 필드다(static이 아니다) — 세포분열/군대(P3)로 개체가 여러 개가 되면 한 개체의 로그가
        // 다른 개체의 로그를 지우면 안 된다. StickmanBlackboard._lastScreenClampLogTime과 같은 관례.
        private float _lastLogUnscaledTime = float.NegativeInfinity;

        public GroundLossHangState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.GroundLossHang;

        /// <summary>지금 이 상태에 머문 시간(초). 테스트/진단 전용.</summary>
        public float ElapsedSeconds => _elapsed;

        public void Enter(StateTransitionContext context)
        {
            _elapsed = 0f;

            // 수평 속도는 <b>그대로 둔다</b>. WALK에서 들어오면 걷던 속도가 유지되어 "허공을 수평으로
            // 걸어간다"는 실측된 코요테 그림이 그대로 이어지고, IDLE에서 들어오면 원래 0이다.
            // (여기서 0으로 지우면 소은이 "귀엽다"고 판정한 WALK 케이스를 우리 손으로 없애는 셈이다.)

            // 팔 허우적 위상만 0에서 시작시킨다 — 다리 위상(_phase01)은 <b>일부러 건드리지 않는다</b>.
            // 그래야 걷다가 들어온 다리가 이음매 없이 그대로 빨라진다(ResetWalkPhase를 부르면 여기서 툭 튄다).
            _blackboard.GetPoseAnimator()?.ResetHangPhase();

            // 대사는 만들지 않는다 — 클래스 문서 마지막 절 참고(리더 결정 승인사항 3).
            LogRateLimited($"[발판유예] 발판(핸들={_blackboard.CurrentFootholdHandle})을 잃어 " +
                $"공중 유예에 들어갑니다 — {ResolveGrace():F2}초 안에 발판이 돌아오지 않으면 낙하합니다. " +
                "그 동안 몸은 붙잡혀 있고(그게 낙하 수정의 본체다) 제자리 종종걸음 + 팔 허우적으로 " +
                "'살아 있음'을 표시합니다.");
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard == null || _blackboard.Machine == null) return;
            _elapsed += deltaTime;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();

            // (탈출) 화면 좌우 이탈 — 다른 지상 상태와 완전히 같은 첫 관문.
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;

            // (탈출) 발밑이 정말 비었다. 붙잡을 근거가 사라졌으므로 유예를 기다리지 않고 바로 떨어진다 —
            // 여기서 계속 붙잡으면 그건 흡수가 아니라 공중부양이다(G7이 잠그는 반대편 절대 조건).
            if (!info.Grounded &&
                (_blackboard.CurrentFootholdHandle == 0L || info.WalkedOffPreferredFoothold))
            {
                LogRateLimited("[발판유예] 공중 유예를 중단하고 즉시 낙하합니다 — " +
                    $"{(info.WalkedOffPreferredFoothold ? "걸어서 발판 모서리를 넘어갔습니다" : "붙잡고 있던 발판이 없습니다(핸들 0)")}. " +
                    "붙잡을 근거가 없는 구간을 붙잡으면 공중부양이 됩니다.");
                _blackboard.ResetGroundLossTimer();
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            // (탈출) 유예 만료 -> Fall / 스냅 상한 초과 -> Fall. 판정은 공용 경로 한 곳에만 있다.
            // 접지 중이면 이 호출이 위치 스냅과 유예 타이머 리셋까지 함께 끝낸다.
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            // (탈출) 발판 복귀 — 유예가 설계 목적을 달성한 경우다. 복귀 상태는 착지 확정
            // (FallState.ConfirmLanding)과 **같은 규칙**으로 고른다: 이동 의도가 있으면 Walk.
            if (info.Grounded)
            {
                float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
                StickmanStateId next = Mathf.Abs(_blackboard.MoveInputX) > deadzone
                    ? StickmanStateId.Walk
                    : StickmanStateId.Idle;
                LogRateLimited($"[발판유예] 발판이 {_elapsed:F2}초 만에 돌아와 {next}로 복귀합니다 — " +
                    "유예가 창 열거 튐을 설계대로 흡수했습니다(낙하하지 않았습니다).");
                _blackboard.Machine.ChangeState(next);
                return;
            }

            // (탈출) ★ 갇힘 방지 최후 안전망 — 클래스 문서의 마지막 항목. 여기 오면 위의 어떤 경로도
            // 동작하지 않았다는 뜻이라 조용히 넘어가지 않는다.
            float hardTimeout = _blackboard.Config != null
                ? _blackboard.Config.ResolveGroundLossHangHardTimeout()
                : ResolveGrace() * 3f;
            if (_elapsed >= hardTimeout)
            {
                Debug.LogWarning($"[발판유예] ★ 갇힘 방지 안전망 발동 — 공중 유예에 {_elapsed:F2}초 " +
                    $"(상한 {hardTimeout:F2}초 = 유예 {ResolveGrace():F2}초의 " +
                    $"{(_blackboard.Config != null ? _blackboard.Config.groundLossHangHardTimeoutGraceMultiplier : 3f):F1}배) " +
                    "머물러 강제로 Fall로 내보냅니다. 정상 경로(유예 만료)가 동작했다면 여기 올 수 없으므로 " +
                    "이 줄이 보이면 GroundedTick의 유예 타이머가 외부에서 리셋되고 있다는 뜻입니다 — " +
                    "이 상태에 갇히면 캐릭터가 영원히 공중에 뜹니다(원래 버그보다 나쁩니다).");
                _blackboard.ResetGroundLossTimer();
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            TickHangPose(deltaTime);
        }

        public void Exit()
        {
            // 되돌릴 것이 없다 — 포즈는 다음 프레임 StickmanBlackboard.TickPose()가 현재 상태 ID를 보고
            // 다시 정하고(WalkState.Exit과 같은 이유), 상체 기울임은 아무도 요청하지 않는 순간
            // TickBodyLean이 자동으로 직립으로 되돌린다(요청형이라 취소 배관이 필요 없다).
        }

        // ====================================================================
        // 연출 — 0.45초를 세 박자로 (소은 제안 + 실측 근거로 비율 2개만 조정)
        // ====================================================================
        //   [0, 무반응)        : 포즈를 **한 톨도** 건드리지 않는다 = 직전 상태의 마지막 그림 그대로.
        //                        늦게 알아차리는 한 박자가 코요테 개그의 핵심이다.
        //   [무반응, 전조)     : 제자리 종종걸음 + 팔 허우적. 여기가 "살아 있음" 신호다.
        //   [전조, 유예 끝)    : 종종걸음은 계속하면서 상체가 앞으로 기운다(낙하 전조).
        // 세 경계는 전부 **유예 길이에 대한 비율**이라 폴링 주기를 바꾸면 함께 따라간다
        // (StickConfig.groundLossHangReactionDelayRatio / groundLossHangFallTellRatio의 툴팁에 조정 사유).

        private void TickHangPose(float deltaTime)
        {
            StickConfig cfg = _blackboard.Config;
            if (cfg != null && !cfg.groundLossHangStateEnabled) return;

            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            if (pose == null) return;

            float grace = ResolveGrace();
            float reactionDelay = grace * (cfg != null ? Mathf.Clamp01(cfg.groundLossHangReactionDelayRatio) : 0.15f);
            if (_elapsed < reactionDelay) return;   // ★ 무반응 — 의도적으로 아무 것도 하지 않는다.

            float tellStart = Mathf.Max(reactionDelay,
                grace * (cfg != null ? Mathf.Clamp01(cfg.groundLossHangFallTellRatio) : 0.72f));

            // 세기 램프: 무반응 직후에 툭 튀지 않게 0 -> 1로 부드럽게 차오른다(smoothstep).
            float ramp = Mathf.Max(0.0001f, (tellStart - reactionDelay) * ScrambleRampFraction);
            float raw = Mathf.Clamp01((_elapsed - reactionDelay) / ramp);
            float intensity = raw * raw * (3f - 2f * raw);

            float walkSpeed = cfg != null ? cfg.ResolveWalkSpeed() : 2.5f;
            pose.ApplyGroundLossHangPose(deltaTime, _blackboard.BuildPoseSettings(),
                _blackboard.BuildGroundLossHangPoseSettings(), _blackboard.PoseSmoothingRate,
                intensity, walkSpeed,
                cfg != null ? cfg.walkPoseAmplitudeScale : 1f,
                cfg != null ? cfg.walkStrideScale : 0.93f);

            // 낙하 전조 — 목표를 **계단으로** 준다(램프로 주지 않는다). 기울임은 지수 감쇠로 목표를
            // 따라가므로, 남은 시간이 0.126초뿐인 구간에서 목표까지 램프를 걸면 실제 각도가 목표의
            // 42%까지 떨어져 소은이 지적한 "안 보이는 변화" 구간으로 되돌아간다(계단이면 78%다).
            // 계단이어도 화면에는 감쇠된 곡선이 나오므로 툭 튀지 않는다.
            if (_elapsed >= tellStart)
            {
                pose.RequestBodyLeanDegrees(_blackboard.GroundLossHangFallTellLeanDegrees);
            }
        }

        private float ResolveGrace()
            => _blackboard.Config != null ? _blackboard.Config.ResolveGroundLossGraceDuration() : 0.1f;

        private void LogRateLimited(string message)
        {
            if (Time.unscaledTime - _lastLogUnscaledTime < LogMinIntervalSeconds) return;
            _lastLogUnscaledTime = Time.unscaledTime;
            Debug.Log(message + $" (같은 계열 로그는 최소 {LogMinIntervalSeconds:F0}초 간격으로만 남깁니다)");
        }
    }
}
