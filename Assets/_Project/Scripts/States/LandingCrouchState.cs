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
    ///
    /// ============================================================================
    /// ★★ 2026-09-01 — 무게감 6티어 재설계 (MOTION_SPEC 4절). 축은 **낙차 하나뿐이다.**
    /// ============================================================================
    /// 모든 임계값이 <b>신장 배수 hH = 낙차 / 신장</b>으로 적힌다. 구 코드는 램프 상한이
    /// <c>2.0 + 3.0 x 신장</c>이었는데 <b>가산 상수 2.0만 신장에 안 걸려 있어서</b> 배율 0.75에서
    /// 임계값이 1.17 H, 1.5에서 0.59 H가 됐다 — 같은 체감 높이에서 다른 반응이 나왔다.
    ///
    /// <code>
    ///  티어   구간              이름            지속            최대 깊이
    ///  T0     hH &lt; 0.35         무반응          (상태 진입 안 함)
    ///  T0.5   0.35 ~ 0.88       가벼운 흡수      0.14 -&gt; 0.32초   0.08 -&gt; 0.45
    ///  T1     0.88 ~ 2.40       얕은 무릎앉아    0.32 -&gt; 0.48초   0.45 -&gt; 0.78
    ///  T2     2.40 ~ 3.90       깊은 무릎앉아    0.48 -&gt; 0.62초   0.78 -&gt; 1.00
    ///  T3     3.90 ~ 11.00      버티는 착지      0.62 -&gt; 0.88초   1.00 고정
    ///  T4     hH &gt;= 11.00       손짚고 엎어짐    (landingCollapseEnabled — 곡선 미구현, 기본 false)
    /// </code>
    ///
    /// ★ 2026-09-02 — T3 램프 폭이 2.60 → <b>7.10</b> H가 되어 포화점이 6.50에서 <b>11.00 H</b>로 갔다.
    ///   <b>진짜 손잡이는 <c>landingCrouchBraceTailHeights</c> 하나뿐이다</b>:
    ///   <c>u = (hH − 3.90) / braceSpan</c>이 지속/hold/상체를 전부 굴리고,
    ///   <c>landingCollapseThresholdHeights</c>는 <see cref="StickConfig.landingCollapseEnabled"/>가
    ///   켜졌을 때만 <see cref="ResolveTier"/>가 읽는다 — 기본 false인 지금 그 값을 6.50에서 15로 올려도
    ///   <b>거동 변화가 정확히 0</b>이다. 두 필드에 6.50이 함께 적혀 있던 것은
    ///   0.88 + 3.02 + 2.60 = 6.50이라는 <b>우연</b>이었지 연결이 아니었다.
    ///
    /// ★ 정직한 한계(설계자 실측, 기록용): 버팀 램프의 <b>총 분해능은 약 4단계</b>다 —
    ///   지속 0.62→0.88초가 4.1 JND, 상체 22→30도가 2.7 JND, 깊이는 기하학적 포화,
    ///   먼지는 3.88 H에서 이미 포화. <b>램프를 늘리는 것은 단계 수를 늘리지 않고 위치만 옮긴다.</b>
    ///   "가장 무거운 착지"를 한 단계 더 만들려면 새 축(T4 곡선)이 필요하다.
    ///
    /// <b>T0.5를 넣은 이유</b>: 데스크톱의 유일한 상시 단차인 Dock은 신장의 0.72배라 구 임계값
    /// 0.88 H <b>아래</b>였다. 캐릭터가 1분에 열 번 넘게 Dock에서 내려오는데 그때마다 무릎이 1도도
    /// 굽지 않았다 — 발이 바닥에 스며드는 그림이다. 이 클래스가 원래 막으려던 것은 <b>무릎 꿇기
    /// 실루엣</b>이지 <b>무릎이 주는 것</b> 자체가 아니었다. T0.5의 깊이 0.08~0.45는 무릎 꿇기가 아니라
    /// 평범한 계단 내려딛기이므로(Dock에서 깊이 0.34) 그 판단과 충돌하지 않는다.
    ///
    /// <b>T3를 넣은 이유</b>: 구 램프는 신장 3.9배에서 포화하는데 실제 낙하는 신장 10배까지 나온다.
    /// 포화 이후가 실사용의 절반 이상이라 "아무리 높이 떨어져도 반응이 똑같다"가 됐다. 깊이는 이미
    /// 기하학적 상한(무릎이 바닥을 뚫는다)이라 더 못 굽히므로, T3는 <b>지속 + hold 비율 + 상체
    /// 앞기울기</b> 세 축만 키운다.
    ///
    /// <b>충격량 축은 쓰지 않는다(C5)</b>: FallState가 스윕 교차로 착지를 확정하며 몸을 발판 상단으로
    /// 스냅하고 하강 속도를 지우기 때문에 <b>정상 착지의 충격량은 실측 0.00</b>이다. 그래서
    /// <c>landingImpactRagdollShield</c>(자기 착지를 랙돌로 보내지 않는 차단막)는 <b>유지가 옳고</b>,
    /// "아무리 높이 떨어져도 안 넘어진다"의 원인은 차단막이 아니라 낙차 축에 상위 티어가 없었던 것이다.
    ///
    /// ★ 배율 1.0 회귀 검산: 낙차 6.92유닛 -> 지속 0.535초 / 깊이 0.845 (구 0.54 / 0.85).
    ///   일상 낙하는 오늘과 사실상 같다 — 달라지는 것은 (가) Dock 단차가 반응을 얻고,
    ///   (나) 다른 배율에서 체감이 일치하고, (다) 극단 낙하가 갈린다, 셋뿐이다.
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

        /// <summary>이번 착지의 hold 비율(버팀 램프에서 자란다). Enter()에서 확정.</summary>
        private float _holdFraction;

        /// <summary>이번 착지의 최대 상체 앞기울기(도). Enter()에서 확정.</summary>
        private float _torsoPitchDegrees;

        /// <summary>
        /// 착지 무게감 티어(MOTION_SPEC 4-3). 곡선은 티어 경계에서 **연속**이며, 이 값은 진단/로그/
        /// 테스트가 "지금 어느 구간인가"를 값으로 단언하기 위한 라벨이다 — 램프를 자르지 않는다.
        /// </summary>
        public enum LandingTier
        {
            /// <summary>T0.5 — 가벼운 흡수(계단 내려딛기). Dock 단차(0.72 H)가 여기다.</summary>
            SoftAbsorb,
            /// <summary>T1 — 얕은 무릎앉아.</summary>
            ShallowCrouch,
            /// <summary>T2 — 깊은 무릎앉아.</summary>
            DeepCrouch,
            /// <summary>T3 — 버티는 착지(깊이는 포화, 지속/상체/hold만 자란다).</summary>
            Brace,
            /// <summary>T4 — 손짚고 엎어짐. landingCollapseEnabled가 켜져 있을 때만 나온다.</summary>
            Collapse,
        }

        /// <summary>진단/테스트용 — 이번 착지의 티어.</summary>
        public LandingTier Tier { get; private set; }

        /// <summary>진단/테스트용 — 이번 착지의 낙차를 **신장 배수**로 환산한 값(hH). 무게감의
        /// 유일한 입력 축이다(MOTION_SPEC 4-2 C5: 충격량 축은 쓰지 않는다).</summary>
        public float HeightsFallen { get; private set; }

        /// <summary>진단/테스트용 — 이번 착지에서 실제로 쓰인 낙하 높이 스냅샷(월드 유닛).</summary>
        public float FallHeight { get; private set; }

        /// <summary>진단/테스트용 — 이번 착지의 최대 깊이 비율(0~1).</summary>
        public float Depth01 => _depth01;

        /// <summary>진단/테스트용 — 이번 착지의 총 지속 시간(초).</summary>
        public float DurationSeconds => _duration;

        /// <summary>지금 프레임의 진행 곡선 값(0=직립, 1=최대 깊이, 음수=반동으로 중립보다 편 자세).
        /// PlayMode 테스트가 "실제로 앉았는가"를 로그가 아니라 값으로 단언하는 데 쓴다.</summary>
        public float CurrentCrouchAmount { get; private set; }

        /// <summary>지금 프레임의 <b>진입 블렌드</b>(1 = 낙하 자세 그대로, 0 = 오늘의 착지 목표각 그대로).
        /// 압축 구간에 걸쳐 1 → 0으로 내려간다 —
        /// 근거와 검산은 StickmanPoseAnimator.ApplyLandingCrouchPose의 "진입 블렌드" 문단.
        /// 진단/테스트가 "블렌드가 실제로 돌았는가"를 값으로 단언하는 창구다.</summary>
        public float CurrentEntryBlend01 { get; private set; }

        /// <summary>
        /// 진입 블렌드 계수 = 1 − smoothstep(경과 / (압축 비율 x 이번 착지의 총 지속시간)).
        ///
        /// <para><b>왜 고정 시간이 아니라 압축 구간 비례인가</b>: 압축 구간의 절대 길이는 낙차에 따라
        /// 0.034초(T0.5, 지속 0.19 x 0.18)에서 0.158초(T3, 0.88 x 0.18)까지 <b>4.6배</b> 변한다.
        /// 고정 시상수를 쓰면 짧은 쪽에서 블렌드가 압축보다 오래 남아 자세가 통째로 뭉개진다.
        /// 압축에 비례시키면 어떤 티어에서도 "압축이 끝나는 순간 블렌드도 정확히 끝난다".</para>
        ///
        /// <para>smoothstep인 이유: 선형이면 t=0에서 기울기가 유한해 첫 프레임부터 목표가 움직이기
        /// 시작하고, 그러면 되돌림이 완전히 0이 되지 않는다. smoothstep은 양 끝에서 기울기가 0이라
        /// 출발과 도착이 둘 다 매끄럽다.</para>
        /// </summary>
        private float ComputeEntryBlend01(float elapsedSeconds)
        {
            StickConfig cfg = _blackboard.Config;
            float compress = cfg != null ? Mathf.Clamp(cfg.landingCrouchCompressFraction, 0.01f, 0.9f) : 0.18f;
            float span = compress * _duration;
            if (span <= 0.0001f) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedSeconds / span));
        }

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
            float h = _blackboard.CharacterHeightWorld;
            HeightsFallen = h > 0.0001f ? FallHeight / h : 0f;

            // ── 세 개의 램프. 전부 **신장 배수(hH)** 위에서 돈다 — 배율 슬라이더를 0.75로 놓든 1.5로
            //    놓든 "같은 체감 높이에서 같은 반응"이 나온다(위 티어 표 문단 참고).
            float softStart = cfg != null ? Mathf.Max(0f, cfg.landingSoftAbsorbThresholdHeights) : 0.35f;
            float reaction = cfg != null ? Mathf.Max(softStart, cfg.landingReactionThresholdHeights) : 0.88f;
            float deepSpan = cfg != null ? Mathf.Max(0.01f, cfg.landingCrouchDeepFallHeights) : 3.02f;
            float braceSpan = cfg != null ? Mathf.Max(0.01f, cfg.landingCrouchBraceTailHeights) : 7.10f;

            float softSpan = Mathf.Max(0.0001f, reaction - softStart);
            float t0 = Mathf.Clamp01((HeightsFallen - softStart) / softSpan);         // T0.5 램프
            float t = Mathf.Clamp01((HeightsFallen - reaction) / deepSpan);           // 깊이 램프
            float u = Mathf.Clamp01((HeightsFallen - (reaction + deepSpan)) / braceSpan); // 버팀 램프

            float softDepth = cfg != null ? Mathf.Clamp01(cfg.landingSoftAbsorbMinDepth01) : 0.08f;
            float minDepth = cfg != null ? Mathf.Clamp01(cfg.landingCrouchMinDepth01) : 0.45f;
            float softDur = cfg != null ? Mathf.Max(0.02f, cfg.landingSoftAbsorbDurationShallow) : 0.14f;
            float shallow = cfg != null ? Mathf.Max(softDur, cfg.landingCrouchDurationShallow) : 0.32f;
            float deep = cfg != null ? Mathf.Max(shallow, cfg.landingCrouchDurationDeep) : 0.62f;
            float brace = cfg != null ? Mathf.Max(deep, cfg.landingCrouchDurationBrace) : 0.88f;

            bool soft = HeightsFallen < reaction;
            _depth01 = soft ? Mathf.Lerp(softDepth, minDepth, t0) : Mathf.Lerp(minDepth, 1f, t);
            _duration = soft
                ? Mathf.Lerp(softDur, shallow, t0)
                : Mathf.Lerp(shallow, deep, t) + u * (brace - deep);

            // 버팀 램프에서는 hold(가장 깊은 자세로 정지하는) 비율이 함께 자란다 — 깊게 떨어질수록
            // "버틴다"가 길어야 무게가 읽힌다. 깊이는 이미 1.0에서 포화했으므로 여기가 유일한 축이다.
            float holdBase = cfg != null ? Mathf.Clamp(cfg.landingCrouchHoldFraction, 0f, 0.9f) : 0.24f;
            float holdBrace = cfg != null ? Mathf.Clamp(cfg.landingCrouchHoldFractionBrace, 0f, 0.9f) : 0.40f;
            _holdFraction = Mathf.Lerp(holdBase, holdBrace, u);

            // 상체 앞기울기 — 신규 축(MOTION_SPEC 4-4). 무릎은 이미 기하학적 상한에 닿아 있어
            // (StickConfig.landingCrouchFrontKneeDegrees 문서) 더 깊은 착지를 "더 굽혀서" 표현할 수
            // 없다. 참고 이미지 분석의 결론대로 무게는 상체에서 온다.
            float pitchShallow = cfg != null ? cfg.landingCrouchTorsoPitchShallowDegrees : 6f;
            float pitchDeep = cfg != null ? cfg.landingCrouchTorsoPitchDegrees : 22f;
            float pitchBrace = cfg != null ? cfg.landingCrouchTorsoPitchBraceDegrees : 30f;
            // t0에 0.35를 곱하는 이유: T0.5는 무릎 꿇기가 아니라 계단 내려딛기라, 그 구간에서 상체가
            // 최대 기울기의 3분의 1 이상 숙으면 "가볍게 받았다"가 아니라 "휘청였다"로 읽힌다.
            _torsoPitchDegrees = Mathf.Lerp(pitchShallow, pitchDeep, Mathf.Max(t0 * 0.35f, t))
                + u * Mathf.Max(0f, pitchBrace - pitchDeep);

            Tier = ResolveTier(HeightsFallen, reaction, deepSpan, braceSpan, cfg);

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
            CurrentEntryBlend01 = ComputeEntryBlend01(0f);

            // ★ 진입 블렌드의 출발점 — "지금 화면에 그려져 있는 각도"(= 낙하 자세)를 캡처한다.
            //   이 한 줄이 없으면 첫 프레임 목표각이 Idle 중립이 되어 뒷팔이 최대 38.8도까지 목표를
            //   지나쳤다 되돌아온다(StickmanPoseAnimator.ApplyLandingCrouchPose의 진입 블렌드 문단).
            _blackboard.GetPoseAnimator()?.CaptureLandingEntryPose();

            Debug.Log($"[무릎앉아] 착지 연출 시작 — 티어={Tier}, 낙하높이={FallHeight:F2}유닛" +
                $"(신장 {h:F2} -> {HeightsFallen:F2} H, T0.5 시작 {softStart:F2} H / 무릎앉아 시작 {reaction:F2} H), " +
                $"램프 t0={t0:F2} t={t:F2} u={u:F2} -> 깊이={_depth01:F2}, 지속={_duration:F2}초, " +
                $"hold={_holdFraction:F2}, 상체={_torsoPitchDegrees:F1}도.");

            // 대사는 만들지 않는다(불변 원칙 1과 무관한 별개의 판단): 이 프로젝트 사용자는 요청하지 않은
            // 자율 연출이 캐릭터를 가리는 것에 반복적으로 민감했고(StickConfig의
            // enableAutonomousHardwareReactions 도입 경위 참고), 이번 요청은 "자세"에 대한 것이다.
            // ★ 2026-09-01 — UX_FLOW.md 31-2 #7이 이 무발화를 **명시적 계약**으로 등재했다("빠뜨린 것이
            //   아니라 말하지 않기로 한 것"). 유일한 예외인 T4("윽... 너무 높았다")는 아직 포즈 곡선이
            //   없어 landingCollapseEnabled가 기본 false다 — 곡선이 들어올 때 이 자리에서, 이미 확정된
            //   Tier 파라미터로부터만 파생시켜야 한다. 대사를 먼저 정하고 행동을 끼워 맞추지 않는다.
        }

        /// <summary>낙차(신장 배수)에서 티어를 확정한다. 로그/테스트가 "지금 무슨 티어인가"를 문자열
        /// 파싱이 아니라 값으로 단언할 수 있게 분리한다.</summary>
        private static LandingTier ResolveTier(float heightsFallen, float reactionHeights,
            float deepSpanHeights, float braceSpanHeights, StickConfig cfg)
        {
            float collapse = cfg != null ? cfg.landingCollapseThresholdHeights : 11.00f;
            bool collapseEnabled = cfg != null && cfg.landingCollapseEnabled;
            if (collapseEnabled && heightsFallen >= collapse) return LandingTier.Collapse;

            float deepEnd = reactionHeights + deepSpanHeights;      // 3.90 H
            if (heightsFallen >= deepEnd) return LandingTier.Brace;
            if (heightsFallen < reactionHeights) return LandingTier.SoftAbsorb;
            // 얕은/깊은 무릎앉아의 경계는 깊이 램프의 중간(기본 2.40 H) — 표시/진단용 구분이며
            // 곡선은 이 경계에서 연속이다(티어는 램프를 자르지 않는다).
            return heightsFallen < reactionHeights + deepSpanHeights * 0.5f
                ? LandingTier.ShallowCrouch
                : LandingTier.DeepCrouch;
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
            CurrentEntryBlend01 = ComputeEntryBlend01(_elapsed);

            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            pose?.ApplyLandingCrouchPose(deltaTime, _blackboard.BuildPoseSettings(),
                _blackboard.BuildLandingCrouchPoseSettings(),
                _blackboard.LandingCrouchPoseSmoothingRate, CurrentCrouchAmount, CurrentEntryBlend01);

            // ★ 상체 앞기울기(MOTION_SPEC 4-4의 신규 축). 곡선 값과 **같은 진행도**로 묶어 요청하므로
            //   눌림/버팀/일어섬 박자가 다리와 상체에서 어긋날 수 없다. 반동 구간(음수)에서는 0으로
            //   잘라 뒤로 젖히지 않는다 — 착지 반동은 "펴는" 동작이지 "젖히는" 동작이 아니다.
            //   RequestBodyLeanDegrees(SetBodyLean 직접 호출이 아니라)를 쓰는 이유는 그 API 문서 참고:
            //   확정은 언제나 TickBodyLean 한 곳이라 적용 각도가 프레임 시간에 의존하지 않는다.
            if (pose != null && _torsoPitchDegrees != 0f)
            {
                pose.RequestBodyLeanDegrees(_torsoPitchDegrees * Mathf.Max(0f, CurrentCrouchAmount));
            }

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
            // ★ hold는 설정값 그대로가 아니라 **이번 착지에서 확정된 값**을 쓴다(버팀 램프에서 자란다).
            //   Enter()를 거치지 않은 폴백(테스트가 곡선만 직접 부르는 경로)에서는 0이므로 설정값으로
            //   되메운다 — 그때는 예전과 100% 같은 곡선이다.
            float hold = _holdFraction > 0f
                ? Mathf.Clamp(_holdFraction, 0f, 0.9f)
                : (cfg != null ? Mathf.Clamp(cfg.landingCrouchHoldFraction, 0f, 0.9f) : 0.24f);
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
