using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// ★★ 무릎앉아 착지(2026-08-29, 사용자 명시 요청: "떨어질때 관절이 이상하게 꺾이면서 넘어지는데
    /// 떨어질때 무릎앉아 형태로 멋지게 착지해야지").
    ///
    /// 높은 곳에서 떨어져 착지한 직후, **한쪽 무릎을 굽혀 낮게 앉아 충격을 흡수했다가 일어서는**
    /// 짧은 능동 상태다. 정상 종료는 Idle/Walk(착지 시점의 이동 의도로 분기).
    ///
    /// ============================================================================
    /// 왜 TimedSpectacleState를 재사용하지 않았는가 (리더가 먼저 검토하라고 지목한 항목)
    /// ============================================================================
    /// TimedSpectacleState는 클래스 문서 그대로 "**캐릭터 쪽 부수 효과가 전혀 없는** 순수 타이머"다 —
    /// 물리도 포즈도 건드리지 않고, 정해진 시간이 지나면 무조건 Idle로 돌아간다. 이 상태는 그 세 조건을
    /// 전부 어긴다:
    ///   (1) 매 프레임 **포즈를 직접 구동**한다(진행 곡선 -> StickmanPoseAnimator.ApplyLandingCrouchPose).
    ///       Walk가 TimedSpectacleState가 아닌 것과 같은 이유다.
    ///   (2) 지속 시간이 상수가 아니라 **낙하 높이에서 매번 계산**된다("높을수록 더 오래 유지").
    ///   (3) 종료가 Idle 고정이 아니라 **이동 의도에 따라 Idle/Walk로 분기**하고, 도중 접지를 잃으면
    ///       Fall로도 빠진다(발판이 닫히거나 움직일 수 있는 이 프로젝트에서는 실제로 일어난다).
    /// 지속시간 선택자에 람다를 하나 더 얹어 (2)만 흉내 낼 수는 있지만 (1)/(3)이 남으므로, 그 클래스에
    /// 포즈 훅과 종료 분기를 더하는 것은 이미 검증된 6개 등록(그라피티/청소부/블랙홀/크래시/투두/포모도로)
    /// 전부를 회귀 위험에 넣는 일이다. LedgeHangState를 ParkourClimbState에 합치지 않기로 한 판단과
    /// 같은 기준이다.
    ///
    /// ============================================================================
    /// 진행 곡선 — "그냥 앉았다 일어남"과 "멋지게 착지"의 차이
    /// ============================================================================
    /// 총 지속 시간을 세 구간으로 나눈다(비율은 전부 StickConfig):
    ///   [1] 눌림(compress) : 아주 짧고 빠르게 최대 깊이까지 — easeOut이라 첫 프레임에 가장 많이 움직여
    ///       "툭" 하고 받는 느낌이 난다. 이 구간이 길면 스스로 앉는 것처럼 보여 충격 흡수로 안 읽힌다.
    ///   [2] 버팀(hold)     : 최대 깊이에서 정지. 애니메이션의 hold 관행 — 이 정지가 있어야 포즈가
    ///       한 장의 그림으로 눈에 남는다.
    ///   [3] 일어섬(rise)   : 앞 62%에서 smoothstep으로 완전히 펴고, 남은 꼬리에서 **중립을 지나쳐**
    ///       더 편 자세(다리 완전 직립 + 팔을 바깥으로 더 벌림)로 갔다가 정확히 0으로 돌아온다.
    ///       StickConfig.landingCrouchReboundAmount가 그 크기이며, 0이면 그냥 스르륵 일어난다.
    /// 곡선의 출력(<see cref="CurrentCrouchAmount"/>)은 0=직립 / 1=최대 깊이 / 음수=중립보다 편 자세다.
    ///
    /// 깊이와 지속 시간은 **하나의 스냅샷**(StickmanBlackboard.LastLandingFallHeight)에서 함께 파생된다.
    /// 낙차가 임계값(StickConfig.rollLandingHeightThreshold)을 갓 넘겼으면 얕고 짧게, 신장의
    /// landingCrouchDeepFallHeights배만큼 더 떨어졌으면 깊고 길게 — 리더 지시 "높을수록 더 깊이 앉고
    /// 더 오래 유지"의 직접 구현이다. 낙차가 임계값 미만이면 FallState가 애초에 이 상태로 보내지
    /// 않으므로(Dock 단차 1.6375유닛 &lt; 2유닛 — 이 개발 머신 tilesize=49 기준, Core/DockGeometry.cs),
    /// 한 계단 내려올 때마다 무릎을 꿇는 일은 없다. ★ 2026-08-30: 예전 주석의 0.855는 안전망이 화면
    /// 최하단 40pt 위였던 시절의 화석이었다. 갱신값 1.6375는 여전히 임계값 미만이지만 여유가 0.363유닛뿐
    /// 이라, Dock 아이콘을 크게(tilesize 64 이상) 쓰는 사용자에게는 Dock 단차에서도 발동한다
    /// (낙차가 실제로 커진 것이므로 물리적으로는 옳은 거동이다).
    ///
    /// ============================================================================
    /// 이 상태는 물리에 포즈를 맡기지 않는다 (아키텍처 0절)
    /// ============================================================================
    /// 능동 상태이므로 팔다리는 Kinematic이고 각도는 전부 절차적 transform.localRotation이다
    /// (StickmanBlackboard.TickPose가 상태 ID를 보고 매 프레임 능동 모드를 재적용한다). 착지 충격을
    /// 물리에 맡기면 그것이 곧 사용자가 신고한 "관절이 이상하게 꺾이면서 넘어지는" 그림이 된다 —
    /// 씬 물리 바닥 충돌이 RAGDOLL로 흐르는 경로가 실제로 열려 있었고(논리 발판이 없는 구간에서
    /// 재현됨), 이번 라운드에 StickConfig.landingImpactRagdollShield로 끊었다.
    /// </summary>
    public sealed class LandingCrouchState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        /// <summary>이번 착지의 총 지속 시간(초) — Enter()에서 낙하 높이로부터 확정된다.</summary>
        private float _duration;

        /// <summary>이번 착지의 최대 깊이 비율(0~1) — 같은 낙하 높이 스냅샷에서 함께 확정된다.</summary>
        private float _depth01;

        /// <summary>Enter() 이후 경과 시간(초).</summary>
        private float _elapsed;

        /// <summary>진단/테스트용 — 이번 착지에서 실제로 쓰인 낙하 높이 스냅샷(월드 유닛).</summary>
        public float FallHeight { get; private set; }

        /// <summary>진단/테스트용 — 이번 착지의 최대 깊이 비율(0~1).</summary>
        public float Depth01 => _depth01;

        /// <summary>진단/테스트용 — 이번 착지의 총 지속 시간(초).</summary>
        public float DurationSeconds => _duration;

        /// <summary>지금 프레임의 진행 곡선 값(0=직립, 1=최대 깊이, 음수=반동으로 중립보다 편 자세).
        /// PlayMode 테스트가 "실제로 앉았는가"를 로그가 아니라 값으로 단언하는 데 쓴다.</summary>
        public float CurrentCrouchAmount { get; private set; }

        public LandingCrouchState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.LandingCrouch;

        public void Enter(StateTransitionContext context)
        {
            _elapsed = 0f;
            FallHeight = _blackboard.LastLandingFallHeight;

            StickConfig cfg = _blackboard.Config;
            float threshold = cfg != null ? cfg.rollLandingHeightThreshold : 2f;
            float deepHeights = cfg != null ? Mathf.Max(0.01f, cfg.landingCrouchDeepFallHeights) : 3f;
            float minDepth = cfg != null ? Mathf.Clamp01(cfg.landingCrouchMinDepth01) : 0.45f;

            // 임계값을 갓 넘긴 낙하(t=0) -> 신장의 deepHeights배만큼 더 떨어진 낙하(t=1).
            // ★ 신장으로 환산하므로 캐릭터 배율을 바꿔도 "체감상 같은 높이"에서 같은 깊이가 나온다.
            float span = deepHeights * _blackboard.CharacterHeightWorld;
            float t = span > 0.0001f ? Mathf.Clamp01((FallHeight - threshold) / span) : 1f;

            _depth01 = Mathf.Lerp(minDepth, 1f, t);
            float shallow = cfg != null ? Mathf.Max(0.05f, cfg.landingCrouchDurationShallow) : 0.32f;
            float deep = cfg != null ? Mathf.Max(shallow, cfg.landingCrouchDurationDeep) : 0.62f;
            _duration = Mathf.Lerp(shallow, deep, t);

            // 착지 순간의 잔여 하강 속도를 제거한다. FallState.ConfirmLanding()이 이미 한 번 지웠지만,
            // 이 상태로 들어오는 경로가 늘어나도(테스트가 직접 ChangeState 하는 등) 앉은 채로 지면을
            // 파고들지 않도록 진입 시점에 멱등적으로 다시 보증한다.
            if (_blackboard.Body != null)
            {
                Vector2 v = _blackboard.Body.linearVelocity;
                if (v.y < 0f) v.y = 0f;
                _blackboard.Body.linearVelocity = v;
            }

            CurrentCrouchAmount = 0f;

            Debug.Log($"[무릎앉아] 착지 연출 시작 — 낙하높이={FallHeight:F2}유닛(임계 {threshold:F2}, " +
                $"신장 {_blackboard.CharacterHeightWorld:F2}), 램프 t={t:F2} -> 깊이={_depth01:F2}, " +
                $"지속={_duration:F2}초.");

            // 대사는 만들지 않는다(불변 원칙 1과 무관한 별개의 판단): 이 프로젝트 사용자는 요청하지 않은
            // 자율 연출이 캐릭터를 가리는 것에 반복적으로 민감했고(StickConfig의
            // enableAutonomousHardwareReactions 도입 경위 참고), 이번 요청은 "자세"에 대한 것이다.
            // 나중에 대사를 붙인다면 여기(전이가 확정된 Enter)에서 이 상태의 파라미터(_depth01)로부터만
            // 파생시켜야 한다 — 대사를 먼저 정하고 행동을 끼워 맞추지 않는다.
        }

        public void Tick(float deltaTime)
        {
            // 착지 연출 중에도 발판은 계속 확인한다 — 이 프로젝트의 발판은 실제 타 앱 창이라 착지한
            // 0.3~0.6초 사이에 닫히거나 움직일 수 있다. 그때는 연출을 접고 Fall로 빠지는 것이 맞다.
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            // 착지 직후 남은 수평 속도를 지수 감쇠로 죽인다(프레임레이트 독립 — 이 프로젝트 표준 공식).
            // 0으로 즉시 대입하지 않는 이유: 공중에서의 수평 이동이 착지 순간 뚝 끊기면 미끄러지듯
            // 멈추는 것보다 오히려 더 부자연스럽다.
            if (_blackboard.Body != null)
            {
                float damping = _blackboard.Config != null ? _blackboard.Config.landingCrouchHorizontalDamping : 12f;
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = damping > 0f ? v.x * Mathf.Exp(-damping * deltaTime) : 0f;
                _blackboard.Body.linearVelocity = v;
            }

            _elapsed += deltaTime;
            float progress = _duration > 0.0001f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            CurrentCrouchAmount = EvaluateCrouchCurve(progress) * _depth01;

            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            pose?.ApplyLandingCrouchPose(deltaTime, _blackboard.BuildPoseSettings(),
                _blackboard.BuildLandingCrouchPoseSettings(),
                _blackboard.LandingCrouchPoseSmoothingRate, CurrentCrouchAmount);

            if (progress < 1f) return;

            // 정상 완료 — 착지 시점의 이동 의도로 Idle/Walk 분기(FallState의 기존 복귀 규칙과 동일).
            float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
            StickmanStateId next = Mathf.Abs(_blackboard.MoveInputX) > deadzone
                ? StickmanStateId.Walk
                : StickmanStateId.Idle;
            _blackboard.Machine.ChangeState(next);
        }

        public void Exit() { }

        /// <summary>
        /// 진행도(0~1) -> 앉은 정도(0=직립 / 1=최대 깊이 / 음수=반동). 세 구간의 근거는 클래스 문서 참고.
        /// 설정값을 읽어 아래 static 본체에 넘기는 얇은 래퍼다.
        /// </summary>
        public float EvaluateCrouchCurve(float progress01)
        {
            StickConfig cfg = _blackboard.Config;
            float compress = cfg != null ? Mathf.Clamp(cfg.landingCrouchCompressFraction, 0.01f, 0.9f) : 0.18f;
            float hold = cfg != null ? Mathf.Clamp(cfg.landingCrouchHoldFraction, 0f, 0.9f) : 0.24f;
            float rebound = cfg != null ? Mathf.Max(0f, cfg.landingCrouchReboundAmount) : 0.12f;
            return EvaluateCrouchCurve(progress01, compress, hold, rebound);
        }

        /// <summary>일어서는 구간 중 "완전히 펴는" 데 쓰는 앞부분의 비율. 나머지 꼬리가 반동(중립을
        /// 지나쳐 더 편 자세)에 쓰인다. 곡선의 **형태**라 튜닝 스칼라가 아니고(반동의 크기만
        /// StickConfig.landingCrouchReboundAmount가 정한다) 여기 상수로 둔다 — 보행 키프레임 표를
        /// StickmanPoseAnimator 상수로 둔 것과 같은 판단 기준.</summary>
        private const float RiseSpanBeforeRebound = 0.62f;

        /// <summary>위 오버로드의 설정 비의존 본체 — 테스트가 곡선의 형태만 직접 검증할 수 있게 분리한다.</summary>
        public static float EvaluateCrouchCurve(float progress01, float compressFraction, float holdFraction, float reboundAmount)
        {
            float t = Mathf.Clamp01(progress01);
            // 두 구간의 합이 1을 넘지 않도록 정규화 — 설정값을 극단적으로 넣어도 일어서는 구간이 사라져
            // 앉은 채로 상태가 끝나는 일이 없어야 한다.
            float compress = Mathf.Clamp(compressFraction, 0.01f, 0.9f);
            float hold = Mathf.Clamp(holdFraction, 0f, 0.98f - compress);

            if (t <= compress)
            {
                // easeOut(1-(1-u)^3) — 첫 프레임에 가장 많이 움직여 "툭" 받는 느낌을 만든다.
                float u = compress > 0f ? t / compress : 1f;
                float inv = 1f - u;
                return 1f - inv * inv * inv;
            }

            if (t <= compress + hold) return 1f; // 버팀 — 포즈가 한 장의 그림으로 남는 정지 구간.

            float riseSpan = 1f - compress - hold;
            float r = riseSpan > 0.0001f ? (t - compress - hold) / riseSpan : 1f;

            // 일어서는 구간을 다시 둘로 나눈다: 앞쪽 RiseSpanBeforeRebound에서 smoothstep으로 완전히
            // 펴고(0 도달), 남은 꼬리에서 sin 한 봉우리만큼 **중립을 지나쳐** 더 편 자세로 갔다가
            // 정확히 0으로 돌아온다. 한 구간에서 두 가지를 동시에 하면(smoothstep에 sin을 그냥 더하면)
            // 두 항이 서로 상쇄되어 반동이 사실상 보이지 않는다 — 실제로 그렇게 먼저 짜봤고, 계산해보니
            // 최저점이 −0.01(설정 0.12의 8%)에 불과했다. 봉우리는 양 끝에서 정확히 0이므로 이 구간의
            // 경계값(시작 0 / 끝 0)은 반동 크기와 무관하게 보존된다.
            if (r <= RiseSpanBeforeRebound)
            {
                float u = r / RiseSpanBeforeRebound;
                return 1f - (u * u * (3f - 2f * u));
            }

            float b = (r - RiseSpanBeforeRebound) / (1f - RiseSpanBeforeRebound);
            return -reboundAmount * Mathf.Sin(Mathf.PI * b);
        }
    }
}
