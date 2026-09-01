using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// ★★ 던져졌을 때 공중 회전(텀블링) → 착지 직전 자세 정렬 → 무릎앉아 착지
    /// (2026-08-29, 사용자 명시 요청: "마우스로 던졌을때도 이상하게 관절꺽이면서 넘어지는데
    /// 던져도 공중에서 회전하면서 무릎앉아 착지할수있게 해줘").
    ///
    /// ============================================================================
    /// 무엇이 바뀌었나 — 던지기는 더 이상 곧바로 랙돌이 아니다
    /// ============================================================================
    /// 예전에는 DragThrowState가 놓는 순간 "던진 속도 × 질량 >= ragdollForceThreshold"면 곧바로
    /// Ragdoll로 보냈다. 랙돌은 정의상 전신 물리에 완전 위임이라(아키텍처 0절) 팔다리가 관절 제한
    /// 안에서 제멋대로 꺾이며 뒹굴었고, 그것이 사용자가 신고한 "이상하게 관절 꺾이면서 넘어진다"였다.
    /// 이 상태는 그 자리를 대체하는 **능동 상태**다.
    ///
    /// ============================================================================
    /// 랙돌은 어떤 기준으로 남겨두었는가 (리더 지시 4항 — 갈림 기준의 근거)
    /// ============================================================================
    /// 갈림 기준은 **속도의 크기가 아니라 원인**이다.
    ///   · **깨끗하게 던져진 자유 비행** = 유저가 놓은 순간의 속도로 시작하는, 아무 것에도 부딪히지
    ///     않은 포물선. 시작 시점의 속도를 우리가 알고 있으므로 착지 시점까지 전부 예측 가능하다.
    ///     예측 가능하다는 것이 곧 "연출로 만들 수 있다"는 뜻이라 여기는 능동 회전이 맞다.
    ///   · **진짜 충격** = 벽/창에 부딪힘, 로데오에서 거칠게 털려 나감. 언제 어느
    ///     방향에서 들어올지 예측할 수 없고, 예측할 수 없는 것을 미리 짠 연출로 흉내 내면 반드시
    ///     어긋난다. 그래서 이쪽은 그대로 RAGDOLL이다(아키텍처 0절 유지).
    /// 이 구분은 코드에도 그대로 나타난다: 던지기 경로(DragThrowState.ReleaseAndThrow)만 이 상태로
    /// 오고, 충돌 콜백(StickmanAgent.OnCollisionEnter2D → RagdollImpactResolver)과 직접 통지 경로
    /// (RodeoCursorState의 흔들기)는 전혀 손대지 않았다. 즉 **회전 도중에
    /// 벽에 부딪히면 그 순간 랙돌로 인터럽트된다** — 연출이 물리를 이기지 않는다.
    ///
    /// ============================================================================
    /// 회전을 물리에 맡기지 않는다 (아키텍처 0절)
    /// ============================================================================
    /// 능동 상태이므로 팔다리는 Kinematic이고 각도는 전부 절차적 localRotation이다. 몸 전체의 회전도
    /// 루트 Rigidbody2D의 각속도가 아니라 **이 상태가 회전각을 직접 적분해 루트의 시각 회전에 대입**
    /// 한다. 루트에는 FreezeRotation 제약이 걸려 있는데(RagdollRig.EnterActiveMode), 그 제약은
    /// "물리가 회전시키는 것"만 막고 Rigidbody2D.rotation 직접 대입은 언제나 유효하다
    /// (RagdollRig.SnapRootUpright가 이미 같은 성질에 의존한다). 그래서 제약을 풀지 않고도 공존한다 —
    /// 오히려 제약이 남아 있는 편이 안전하다. 충돌 임펄스가 회전에 개입할 수 없으므로 우리가 계산한
    /// 각도가 유일한 진실이 되고, 착지 정렬이 예측 불가능하게 어긋날 여지가 사라진다.
    ///
    /// 단 하나의 예외 배선: StickmanBlackboard.TickPose()가 모든 능동 상태에 매 프레임 걸어주는
    /// RagdollRig.SnapRootUpright()(= 루트 회전각을 0으로 스냅)를 **이 상태에서만** 건너뛴다.
    /// 그 예외가 없으면 회전이 매 프레임 지워진다.
    ///
    /// ============================================================================
    /// 회전 중심은 발이 아니라 엉덩이다 (탄도의 주인공을 바꾼다)
    /// ============================================================================
    /// 이 프로젝트의 루트 원점은 **발바닥**이다(StickmanBlackboard.SenseGround 규약). 루트를 그냥
    /// 회전시키면 몸이 발끝을 축으로 도는 바람개비가 된다 — 머리가 신장만 한 반지름의 원을 그리므로
    /// 텀블링이 아니라 회전 그네로 보인다. 사람은 무게중심 근처를 축으로 돈다.
    ///
    /// 그래서 회전 후 **엉덩이(StickmanMetrics.HipLocalY)가 원래의 탄도 위치에 그대로 남도록** 루트
    /// 위치를 매 프레임 보정한다:
    ///     보정량 = pivot − R(θ)·pivot            (pivot = (0, 엉덩이 높이))
    ///     루트 위치 = 순수 탄도 위치 + 보정량
    /// 즉 포물선을 따라가는 주인공이 발바닥에서 엉덩이로 바뀐다(물리적으로도 이쪽이 옳다 — 포물선을
    /// 그리는 것은 무게중심이다). "순수 탄도 위치"는 직전 프레임에 더한 보정량을 되빼서 얻으므로
    /// (<see cref="_pivotOffset"/>), 물리 엔진의 적분 결과를 우리가 다시 흉내 낼 필요가 없고 오차도
    /// 누적되지 않는다. 착지 시점에는 θ=0이라 보정량이 정확히 0 — 착지 판정/스냅은 예전과 완전히
    /// 같은 좌표계에서 이루어진다.
    ///
    /// ============================================================================
    /// 착지 직전 정렬 — 정수 바퀴로 끝낸다 (리더 지시 2항)
    /// ============================================================================
    /// 거꾸로 선 채 착지하면 무릎앉아가 읽히지 않는다. 그래서 두 국면으로 나눈다.
    ///   [1] 회전(SPIN)  : 던진 세기에서 파생된 일정 각속도로 계속 돈다.
    ///   [2] 정렬(ALIGN) : "지금 각도에서 **다음 정수 바퀴**까지 남은 각도"를 지금 각속도로 돌리는 데
    ///       걸리는 시간이 착지까지 남은 시간에 다다르면(+ throwTumbleAlignLeadSeconds 여유) 전환.
    ///       이후에는 각속도를 상수로 두지 않고 **남은 각도 ÷ 남은 시간**으로 매 프레임 다시 계산한다 —
    ///       예측이 조금씩 틀려도 스스로 보정되고, 남은 각도가 0에 도달하면 그대로 직립으로 고정된다.
    /// 착지까지 남은 시간은 포물선 방정식을 직접 푼다(<see cref="PredictSecondsToGround"/>).
    /// 발판을 못 찾으면(예측 불가) 정렬로 넘어가지 않고 계속 돌다가, 화면 이탈 또는
    /// throwTumbleMaxSeconds 상한으로 평범한 Fall에 넘긴다.
    ///
    /// ============================================================================
    /// 착지는 기존 LandingCrouch를 그대로 재사용한다 (리더 지시 3항)
    /// ============================================================================
    /// 새 착지 상태를 만들지 않았다. 깊이 램프의 입력인 "낙하 높이"만 이 상태가 정해서 넘긴다 —
    /// 던지기는 옆으로도 날아오므로 기하학적 낙차만으로는 세기가 표현되지 않기 때문이다. 환산은
    /// 에너지 보존 그대로다(<see cref="ConfirmLanding"/> 참고): 순수 자유낙하에서는 이 환산값이 실제
    /// 낙차와 **정확히 일치**하므로 기존 무릎앉아의 단위/램프와 어긋날 수 없다.
    ///
    /// 탈출구: StickConfig.throwTumbleEnabled를 끄면 DragThrowState가 이 상태를 아예 쓰지 않고
    /// 예전 경로(Ragdoll 또는 Fall)로 돌아간다. landingCrouchEnabled 등 기존 스위치 관례와 같다.
    /// </summary>
    public sealed class ThrowTumbleState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        /// <summary>배율 1.0 프리팹의 "엉덩이 높이 ÷ 신장". StickmanMetrics를 찾지 못하는 폴백 경로
        /// (테스트 리그/구버전 프리팹)에서만 쓰인다 — 0을 쓰면 회전 중심이 발바닥으로 되돌아가
        /// 연출이 조용히 망가진다. 출처는 StickmanMetrics의 같은 기준 비율이다.</summary>
        private const float FallbackHipHeightRatio = 0.9346944f / StickConfig.BaselineCharacterTotalHeight;

        /// <summary>"수평으로 던졌다"고 볼 최소 속도(유닛/초). 이보다 작으면 회전 방향을 던진 방향에서
        /// 뽑을 수 없으므로(부호가 잡음에 흔들린다) 바라보는 방향으로 대신 정한다. 던지기 속도는
        /// 유닛/초 단위라 이 값도 절대값이지만, 판단에만 쓰이고 연출의 크기에는 관여하지 않는다.</summary>
        private const float ThrowDirectionEpsilon = 0.15f;

        /// <summary>정렬 구간에서 "남은 시간"의 하한(초). 0으로 나누는 것을 막는 수치 안전장치일 뿐이라
        /// 튜닝 값이 아니다(물리 스텝 한 번보다 짧다).</summary>
        private const float MinAlignSeconds = 0.016f;

        /// <summary>"착지 준비"로 볼 남은 회전 각도(도) — 마지막 1/4바퀴. 이 구간부터 웅크린 몸을 펴기
        /// 시작한다. 튜닝 스칼라가 아니라 **연출의 형태**(어느 시점부터 착지 자세를 잡는가)라
        /// StickConfig가 아니라 여기 상수로 둔다 — 보행 키프레임 표/무릎앉아 반동 구간과 같은 판단
        /// 기준이다(펴지는 정도의 크기는 StickConfig.throwTumbleLandingTuck01이 정한다).</summary>
        private const float LandingPrepRemainingDegrees = 90f;

        /// <summary>누적 회전각(도). 0이 직립이며 부호는 Unity 규약(+ = 반시계).</summary>
        private float _angle;

        /// <summary>회전 방향(+1 반시계 / −1 시계). 던진 방향에서 파생된다.</summary>
        private float _spinDirection = -1f;

        /// <summary>회전 국면의 각속도(도/초, 양수). 던진 세기에서 파생된다.</summary>
        private float _spinSpeed;

        /// <summary>지금 착지 준비 국면인가(= 마지막 1/4바퀴 안쪽 — 몸을 펴기 시작한다).</summary>
        private bool _landingPrep;

        /// <summary>이번 비행에서 돌기로 **계획한** 최종 각도(도, 부호 포함). 항상 360의 정수배 지점이며
        /// 그래서 도달하는 순간 몸이 정확히 직립이다. 아직 계획을 세우지 못했으면 float.NaN.</summary>
        private float _targetAngle = float.NaN;

        /// <summary>계획된 회전 바퀴 수(진단/테스트용, 계획 전에는 0).</summary>
        private int _plannedTurns;

        /// <summary>웅크린 정도(0~1). 회전 중 1, 정렬 중 throwTumbleLandingTuck01로 수렴한다.</summary>
        private float _tuck01;

        /// <summary>직전 프레임에 루트에 더해 둔 회전 중심 보정량(클래스 문서 참고).</summary>
        private Vector2 _pivotOffset;

        /// <summary>직전 프레임의 **순수 탄도** 발 위치(보정량을 뺀 값) — 스윕 착지 판정의 선분 시작점.</summary>
        private Vector2 _prevBallisticFoot;
        private bool _hasPrevSample;

        /// <summary>던져진 시점의 월드 Y — 기하학적 낙차 계산용.</summary>
        private float _startWorldY;

        private float _elapsed;

        // ── 진단/테스트용 노출값 (실제 연출은 로그로 판정할 수 없으므로 값으로 단언한다) ──

        /// <summary>지금까지 실제로 돌아간 각도(도, 부호 포함).</summary>
        public float SpinAngleDegrees => _angle;

        /// <summary>이번 던지기에서 확정된 회전 방향(+1 반시계 / −1 시계).</summary>
        public float SpinDirection => _spinDirection;

        /// <summary>이번 던지기에서 확정된 회전 국면 각속도(도/초).</summary>
        public float SpinSpeedDegreesPerSecond => _spinSpeed;

        /// <summary>지금 착지 준비 국면인가(마지막 1/4바퀴 안쪽).</summary>
        public bool IsLandingPrep => _landingPrep;

        /// <summary>이번 비행에서 돌기로 계획한 바퀴 수(계획 전 0). 회전이 **정수 바퀴**로 끝난다는
        /// 이 상태의 핵심 계약을 테스트가 값으로 확인할 수 있게 노출한다.</summary>
        public int PlannedTurns => _plannedTurns;

        /// <summary>이번 회전에서 실제로 돌아간 각도의 절대 최댓값(도) — "정말 회전했는가"의 증거.</summary>
        public float MaxAbsAngleDegrees { get; private set; }

        /// <summary>착지 순간 실제로 넘긴 환산 낙하 높이(월드 유닛). 0이면 아직 착지 전.</summary>
        public float LastLandingEffectiveHeight { get; private set; }

        public ThrowTumbleState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.ThrowTumble;

        public void Enter(StateTransitionContext context)
        {
            _elapsed = 0f;
            _landingPrep = false;
            _targetAngle = float.NaN;
            _plannedTurns = 0;
            _tuck01 = 0f;
            _pivotOffset = Vector2.zero;
            MaxAbsAngleDegrees = 0f;
            LastLandingEffectiveHeight = 0f;

            Rigidbody2D body = _blackboard.Body;
            _angle = body != null ? body.rotation : 0f;
            _startWorldY = body != null ? body.position.y : 0f;
            _prevBallisticFoot = body != null ? body.position : Vector2.zero;
            _hasPrevSample = body != null;

            // 공중에서는 어떤 발판도 붙잡고 있지 않다(FallState.Enter와 같은 초기화 — 이걸 빠뜨리면
            // 낡은 핸들 때문에 다음 착지에서 접지 판정이 실패한다).
            _blackboard.CurrentFootholdHandle = 0L;
            _blackboard.ReportFootholdChangeIfNeeded("던지기 회전 진입 — 공중");

            StickConfig cfg = _blackboard.Config;
            Vector2 throwVelocity = _blackboard.LastThrowVelocity;
            float speed = throwVelocity.magnitude;
            float characterHeight = _blackboard.CharacterHeightWorld;
            float heightsPerSecond = speed / Mathf.Max(0.0001f, characterHeight);

            _spinDirection = ResolveSpinDirection(throwVelocity, _blackboard.FacingSign);
            _spinSpeed = ResolveSpinSpeedDegreesPerSecond(speed, characterHeight, cfg);

            Debug.Log($"[던지기회전] 진입 — 던진 속도={throwVelocity.ToString("F2")}(속력 {speed:F2}유닛/초 = " +
                $"{heightsPerSecond:F2}신장/초), 회전={( _spinDirection > 0f ? "반시계" : "시계")} {_spinSpeed:F0}도/초, " +
                $"시작 Y={_startWorldY:F2}, 신장={_blackboard.CharacterHeightWorld:F2}, " +
                $"회전중심 높이={ResolvePivotLocalY():F3}유닛.");

            // 대사는 만들지 않는다 — LandingCrouchState.Enter()와 같은 판단(요청은 "자세"에 대한 것이고,
            // 이 프로젝트 사용자는 요청하지 않은 자율 대사에 반복적으로 민감했다). 나중에 붙인다면
            // 전이가 확정된 여기에서 이 상태의 파라미터(_spinSpeed 등)로부터만 파생시켜야 한다.
        }

        // ============================================================================
        // 던지기 -> 회전 파생 (순수 함수)
        // ============================================================================
        // 아래 세 정적 메서드가 "던진 속도 하나에서 연출 파라미터가 전부 나온다"는 이 상태의 계약
        // 그 자체다. 정적으로 분리한 이유는 두 가지다:
        //   (1) 진입 판정(IsCleanThrow)을 던지는 쪽(States/DragThrowState.ReleaseAndThrow)과 이 상태가
        //       **같은 식**으로 봐야 한다 — 두 곳에 따로 적으면 어긋나는 순간 "던졌는데 아무 일도 안
        //       일어나는" 상태가 된다(이 프로젝트가 이미 두 번 겪은 실패 유형).
        //   (2) PlayMode 테스트가 **여러 배율에서** 같은 식을 직접 호출해 배율 불변성을 단언할 수 있다.
        //       런타임 프리팹은 한 배율로만 구워져 있어(에디터 툴로 굽는다) 실행 중에 배율을 바꿔가며
        //       회전시켜 볼 수 없기 때문이다 — LandingCrouchState.EvaluateCrouchCurve를 설정 비의존
        //       정적 본체로 분리한 것과 같은 판단이다.

        /// <summary>
        /// "던진 것"으로 볼 만한 속도인가. 속도는 거리 성분이라 **신장으로 나눠 무차원화**한 뒤
        /// StickConfig.throwTumbleMinSpeedHeightsPerSecond와 비교한다 — 그래야 캐릭터 배율이 바뀌어도
        /// "같은 체감 세기"에서 같은 판정이 나온다. 살살 내려놓은 것까지 공중제비를 돌면 그게 오히려
        /// 고장으로 읽히므로 하한을 둔다.
        /// </summary>
        public static bool IsCleanThrow(float throwSpeed, float characterHeightWorld, StickConfig config)
        {
            if (config != null && !config.throwTumbleEnabled) return false;
            float min = config != null ? config.throwTumbleMinSpeedHeightsPerSecond : 1.2f;
            float heightsPerSecond = throwSpeed / Mathf.Max(0.0001f, characterHeightWorld);
            return heightsPerSecond >= min;
        }

        /// <summary>
        /// 던진 세기 -> 회전 각속도(도/초, 양수). 입력은 무차원(초당 몇 신장)이고 출력은 각도 차원이라
        /// 절대값이다 — "거리·속도만 신장으로 나누고 각속도는 절대값"이라는 이 라운드의 단위 규약
        /// (리더 지시)이 정확히 이 한 줄에 들어 있다. 상·하한은 연출 품질을 위한 것으로,
        /// 너무 느리면 회전으로 보이지 않고 너무 빠르면 잔상처럼 뭉개진다.
        /// </summary>
        public static float ResolveSpinSpeedDegreesPerSecond(float throwSpeed, float characterHeightWorld, StickConfig config)
        {
            float heightsPerSecond = throwSpeed / Mathf.Max(0.0001f, characterHeightWorld);
            float perHeight = config != null ? config.throwTumbleDegreesPerHeightSpeed : 90f;
            float minSpin = config != null ? config.throwTumbleMinSpinDegreesPerSecond : 220f;
            float maxSpin = config != null ? Mathf.Max(minSpin, config.throwTumbleMaxSpinDegreesPerSecond) : 720f;
            return Mathf.Clamp(perHeight * heightsPerSecond, minSpin, maxSpin);
        }

        /// <summary>
        /// 던진 방향 -> 회전 방향(+1 반시계 / −1 시계). 오른쪽으로 던지면 몸의 윗부분이 진행 방향으로
        /// 넘어가는 **앞구르기**가 자연스럽다 = 시계 방향 = Unity 규약에서 음의 Z 회전.
        /// 거의 수직으로 던져 좌우 방향이 없으면 바라보는 쪽으로 앞구르기 한다(부호가 잡음에 흔들리는
        /// 구간에서 회전 방향이 프레임마다 뒤집히는 것을 막는다).
        /// </summary>
        public static float ResolveSpinDirection(Vector2 throwVelocity, float facingSign)
        {
            if (Mathf.Abs(throwVelocity.x) > ThrowDirectionEpsilon) return throwVelocity.x > 0f ? -1f : 1f;
            return facingSign >= 0f ? -1f : 1f;
        }

        public void Tick(float deltaTime)
        {
            Rigidbody2D body = _blackboard.Body;
            if (body == null)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            // 이번 프레임의 순수 탄도 위치 = 지금 위치 − 직전 프레임에 더해 둔 회전 중심 보정량.
            // 물리 엔진이 그 위에 v·dt를 더해 두었으므로, 되빼면 회전이 없었을 때의 발 위치가 남는다.
            Vector2 ballisticFoot = body.position - _pivotOffset;

            _elapsed += deltaTime;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            // 화면(발판 좌우 범위) 이탈 -> Fall. Exit()가 회전과 보정량을 되돌린다.
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;

            // ★ 착지 판정은 FallState의 1순위 경로와 **같은 래퍼**를 쓴다(스윕 교차, drop-through 유예
            // 포함). 판정을 여기서 새로 짜면 "창 위에 착지할 수 있게 만든" 그 판정과 두 벌이 되고,
            // 이 프로젝트는 같은 계산이 두 곳에 생겨 어긋난 버그를 이미 두 번 겪었다.
            if (_hasPrevSample &&
                _blackboard.TryFindLandingCrossing(_prevBallisticFoot, ballisticFoot, out long handle, out float landingWorldY))
            {
                ConfirmLanding(landingWorldY, handle);
                return;
            }
            _prevBallisticFoot = ballisticFoot;
            _hasPrevSample = true;

            float maxSeconds = _blackboard.Config != null ? _blackboard.Config.throwTumbleMaxSeconds : 6f;
            if (_elapsed >= maxSeconds)
            {
                // 안전 상한 — 발판이 없거나 예측이 실패해 영영 착지하지 못하는 경우. 평범한 낙하로
                // 넘기면 기존 낙하/구조 안전망(EnforceScreenBoundsAndRescue)이 그대로 받는다.
                Debug.Log($"[던지기회전] 상한 {maxSeconds:F1}초 초과 — 평범한 낙하로 전환합니다" +
                    $"(총 회전 {MaxAbsAngleDegrees:F0}도).");
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            // ★ 회전 계획에 실패하면(회전할 시간이 부족) AdvanceRotation이 Fall로 전이시키고 false를
            // 돌려준다. 그 뒤에 아래 두 줄이 실행되면 이미 Exit()가 직립으로 되돌려 놓은 몸에 회전과
            // 보정 오프셋을 다시 발라버린다 — 전이 이후 자기 상태를 계속 만지지 않는다는 규칙이다.
            if (!AdvanceRotation(deltaTime, ballisticFoot)) return;
            ApplyRootRotation(ballisticFoot);
            TickTuckPose(deltaTime);
        }

        public void Exit()
        {
            // ★ 어떤 경로로 나가든(정상 착지 / 화면 이탈 / 전체화면 강제 취소 / 외력 랙돌 인터럽트 /
            // 테스트의 직접 ChangeState) 루트는 반드시 직립 + 보정량 제거 상태로 되돌린다. 이 정리를
            // 각 분기에 흩어 두면 하나만 빠져도 캐릭터가 기울어진 채 영구히 남는다 — 이 프로젝트가
            // 반복해서 겪은 실패 유형이라 종료 경로를 한 곳(Exit)으로 못박는다. 멱등이다.
            RestoreUprightRoot();
        }

        // ============================================================================
        // 회전
        // ============================================================================

        /// <summary>
        /// 이번 프레임의 회전각 갱신 — 이 상태의 심장부다.
        ///
        /// ============================================================================
        /// 왜 "정수 바퀴를 미리 계획"하는가 (리더 지시 2항의 실제 구현)
        /// ============================================================================
        /// 처음에는 "자연 각속도로 계속 돌다가 착지가 가까워지면 남은 각도를 몰아서 마무리"로 짰다.
        /// 계산해보니 그 방식은 **비행 시간이 한 바퀴에 못 미칠 때 반드시 실패**한다: 예컨대 6유닛
        /// 낙하(0.64초)에 자연 각속도 220도/초면 141도밖에 못 도는데, 마무리 국면은 "다음 정수 바퀴"인
        /// 360도까지 남은 219도를 0.1초 안에 채우려 들어(2000도/초) 상한에 막히고, 결국 **비스듬히 선
        /// 채로 착지**한다. 사용자가 보게 되는 그림이 정확히 그 실패다.
        ///
        /// 그래서 순서를 뒤집었다. 착지까지 남은 시간을 먼저 예측하고, 그 시간에 **정확히 몇 바퀴를
        /// 돌 수 있는지**를 정수로 정한 뒤, 그 바퀴 수를 그 시간에 나눠 각속도를 역산한다.
        ///   · 비행 내내 각속도가 일정하다(중간에 빨라지거나 느려지지 않아 눈에 거슬리지 않는다).
        ///   · 도착 지점이 정의상 360의 정수배라 **착지 순간 몸이 정확히 직립**이다.
        ///   · 던진 세기는 "몇 바퀴를 돌지"로 반영된다(오래 날수록/세게 던질수록 바퀴 수가 늘어난다).
        /// 예측이 조금씩 틀리는 것은 아래 비례 제어가 흡수한다: 각속도를 상수로 굳히지 않고 매 프레임
        /// **남은 각도 ÷ 남은 시간**으로 다시 계산하므로, 발판이 바뀌거나 수평 이동으로 착지면이
        /// 달라져도 도착 지점(정수 바퀴)은 그대로 지켜진다.
        ///
        /// 착지면을 아예 예측할 수 없으면(발판 없음 = 화면 밖으로 던져짐) 계획을 세우지 않고 자연
        /// 각속도로 계속 돈다. 그 경우의 마무리는 화면 이탈/안전 상한이 담당한다.
        /// </summary>
        /// <returns>이 상태를 계속 유지하면 true, 회전할 시간이 부족해 Fall로 넘겼으면 false.</returns>
        private bool AdvanceRotation(float deltaTime, Vector2 ballisticFoot)
        {
            StickConfig cfg = _blackboard.Config;
            float lead = cfg != null ? Mathf.Max(0f, cfg.throwTumbleAlignLeadSeconds) : 0.1f;
            float factor = cfg != null ? Mathf.Max(1f, cfg.throwTumbleAlignMaxSpeedFactor) : 1.6f;
            float secondsToGround = PredictSecondsToGround(ballisticFoot);

            float step;
            if (float.IsInfinity(secondsToGround))
            {
                // 착지면을 예측할 수 없다 — 계획 없이 자연 각속도로 돈다.
                step = _spinSpeed * deltaTime;
                _landingPrep = false;
            }
            else
            {
                if (float.IsNaN(_targetAngle) && !TryPlanRotation(secondsToGround, lead)) return false; // Fall로 전이됨
                float remaining = (_targetAngle - _angle) * _spinDirection;
                if (remaining < 0f) remaining = 0f;

                // 착지 lead초 전에 회전을 끝내도록 매 프레임 다시 계산하는 비례 제어. 상한은 계획
                // 각속도의 factor배 — 예측이 순간적으로 흔들려도 팽이처럼 튀는 프레임이 생기지 않는다.
                float timeLeft = Mathf.Max(MinAlignSeconds, secondsToGround - lead);
                float speed = Mathf.Min(remaining / timeLeft, _spinSpeed * factor);
                step = Mathf.Min(speed * deltaTime, remaining);

                bool prep = remaining <= LandingPrepRemainingDegrees || secondsToGround <= lead;
                if (prep && !_landingPrep)
                {
                    Debug.Log($"[던지기회전] 착지 준비 — 남은 각도={remaining:F0}도, 착지까지 {secondsToGround:F2}초, " +
                        $"지금까지 회전={Mathf.Abs(_angle):F0}도(계획 {_plannedTurns}바퀴).");
                }
                _landingPrep = prep;
            }

            _angle += _spinDirection * step;
            MaxAbsAngleDegrees = Mathf.Max(MaxAbsAngleDegrees, Mathf.Abs(_angle));

            // 목표에 도달했으면 부동소수 잔차를 지워 정확히 정수 바퀴로 고정한다(착지 순간 0.3도쯤
            // 기울어 있는 것을 막는 마무리).
            if (!float.IsNaN(_targetAngle) && Mathf.Abs(_targetAngle - _angle) <= 0.001f) _angle = _targetAngle;
            return true;
        }

        /// <summary>
        /// 이번 비행의 회전 계획을 세운다(위 문서의 "정수 바퀴 계획"). 성공하면 <see cref="_targetAngle"/>과
        /// <see cref="_spinSpeed"/>가 확정된다.
        ///
        /// 회전할 시간이 물리적으로 부족하면(한 바퀴조차 상한 각속도로 못 돈다) **계획하지 않고 평범한
        /// 낙하(Fall)로 넘긴다** — 낮게 스치듯 던진 경우가 그렇다. 억지로 돌리면 잔상처럼 뭉개진 채
        /// 비스듬히 착지하므로, 아무 것도 안 하는 편이 낫다. 그 뒤로는 기존 낙하/무릎앉아 경로가
        /// 그대로 받는다(낙차가 임계값을 넘으면 무릎앉아도 그대로 나온다).
        /// </summary>
        /// <returns>계획에 성공해 이 상태를 계속 유지하면 true, Fall로 넘겼으면 false.</returns>
        private bool TryPlanRotation(float secondsToGround, float leadSeconds)
        {
            StickConfig cfg = _blackboard.Config;
            float maxSpin = cfg != null ? Mathf.Max(1f, cfg.throwTumbleMaxSpinDegreesPerSecond) : 720f;
            float usable = secondsToGround - leadSeconds;

            // 지금 각도에서 회전 방향으로 만나는 **다음** 직립 지점까지의 각도(0이면 한 바퀴로 친다 —
            // 제자리에서 멈춰버리는 퇴화 해를 배제한다).
            float toNextUpright = RemainingDegreesToUpright();
            if (toNextUpright <= 0.0001f) toNextUpright = 360f;

            if (usable > 0.0001f)
            {
                // 자연 각속도로 그 시간 동안 돌 수 있는 각도를 기준으로 바퀴 수를 반올림한다.
                float ideal = _spinSpeed * usable;
                int turns = Mathf.Max(1, Mathf.RoundToInt((ideal - toNextUpright) / 360f) + 1);
                float delta = toNextUpright + 360f * (turns - 1);
                while (turns > 1 && delta / usable > maxSpin)
                {
                    turns--;
                    delta = toNextUpright + 360f * (turns - 1);
                }

                if (delta / usable <= maxSpin)
                {
                    _plannedTurns = turns;
                    _targetAngle = _angle + _spinDirection * delta;
                    _spinSpeed = delta / usable;
                    Debug.Log($"[던지기회전] 회전 계획 — 착지까지 {secondsToGround:F2}초(여유 {leadSeconds:F2} 제외 " +
                        $"{usable:F2}초), {turns}바퀴({delta:F0}도)를 {_spinSpeed:F0}도/초로. " +
                        $"목표 각도={_targetAngle:F0}도.");
                    return true;
                }
            }

            Debug.Log($"[던지기회전] 회전할 시간이 부족합니다(착지까지 {secondsToGround:F2}초, " +
                $"한 바퀴에 필요한 각속도 {(usable > 0.0001f ? toNextUpright / usable : float.PositiveInfinity):F0}도/초 > " +
                $"상한 {maxSpin:F0}) — 평범한 낙하로 넘깁니다.");
            _blackboard.Machine.ChangeState(StickmanStateId.Fall);
            return false;
        }

        /// <summary>
        /// 지금 각도에서 회전 방향으로 계속 돌았을 때 **다음 정수 바퀴(= 직립)** 까지 남은 각도(도, 양수).
        /// 정수 바퀴로 맞추면 "회전을 억지로 되감지 않고 자연스럽게 마무리"가 된다 — 되감으면 공중에서
        /// 갑자기 반대로 도는 그림이 나온다.
        /// </summary>
        private float RemainingDegreesToUpright()
        {
            float frac = Mathf.Repeat(_angle, 360f); // 항상 [0, 360)
            if (_spinDirection >= 0f) return frac <= 0.0001f ? 0f : 360f - frac;
            return frac;
        }

        /// <summary>
        /// 지금 탄도 상태에서 착지면까지 남은 시간(초). 포물선을 직접 푼다:
        ///     0.5·a·t² + v_y·t + (y₀ − y_지면) = 0,  a = 중력가속도(음수)
        /// 판별식은 지면이 아래에 있는 한 항상 양수이고, 물리적으로 의미 있는(양수) 근은 하나뿐이다.
        /// 예측할 수 없으면(발판을 못 찾음/중력 없음) +∞를 돌려주어 호출부가 계속 회전하게 한다.
        /// </summary>
        private float PredictSecondsToGround(Vector2 ballisticFoot)
        {
            Rigidbody2D body = _blackboard.Body;
            if (body == null) return float.PositiveInfinity;

            float a = Physics2D.gravity.y * body.gravityScale;
            if (a >= -0.0001f) return float.PositiveInfinity;

            if (!TryPredictLandingSurfaceY(ballisticFoot, out float groundY)) return float.PositiveInfinity;

            float d = ballisticFoot.y - groundY;
            if (d <= 0f) return 0f; // 이미 지면 높이 이하 — 지금 당장 정렬해야 한다.

            float vy = body.linearVelocity.y;
            float disc = vy * vy - 2f * a * d;
            if (disc < 0f) return float.PositiveInfinity; // 도달 불가(수치상으로만 가능)
            return (-vy - Mathf.Sqrt(disc)) / a;
        }

        /// <summary>
        /// 착지할 것으로 예상되는 면의 월드 Y. 몸보다 **아래**에 있는 가장 높은 발판 상단이 1순위이고
        /// (그게 실제로 먼저 닿는 면이다), 그런 면이 없으면 그 x의 바닥(가장 낮은 발판 상단)으로
        /// 폴백한다 — 아래로 던져 넣은 경우 위쪽 창 상단을 착지면으로 잡으면 정렬 타이밍이 통째로
        /// 틀어지기 때문이다(GroundSensor.TryGetFloorWorldY 문서의 같은 함정).
        ///
        /// 알려진 한계(정직하게): 수평 이동을 고려하지 않고 **지금 x**에서만 조회한다. 옆으로 크게
        /// 날아가는 던지기에서는 착지면이 도중에 바뀔 수 있는데, 그때는 정렬 국면이 매 프레임 남은
        /// 시간을 다시 재므로 각속도가 즉시 재조정된다(그 자기 보정이 이 근사를 감당하는 장치다).
        /// </summary>
        private bool TryPredictLandingSurfaceY(Vector2 ballisticFoot, out float groundY)
        {
            if (_blackboard.TryGetGroundSurfaceWorldY(ballisticFoot, out float surfaceY) && surfaceY < ballisticFoot.y)
            {
                groundY = surfaceY;
                return true;
            }
            if (_blackboard.TryGetFloorWorldY(ballisticFoot, out float floorY))
            {
                groundY = floorY;
                return true;
            }
            groundY = 0f;
            return false;
        }

        /// <summary>
        /// 회전각을 루트의 시각 회전에 적용하고, 회전 중심이 엉덩이가 되도록 위치를 보정한다
        /// (클래스 문서 "회전 중심은 발이 아니라 엉덩이다" 절의 식 그대로).
        ///
        /// Rigidbody2D와 Transform 양쪽에 모두 쓰는 이유: 이 프로젝트는 Physics2D.autoSyncTransforms가
        /// 꺼져 있어 Rigidbody2D에만 쓰면 다음 물리 스텝까지 Transform(=화면에 보이는 것)이 갱신되지
        /// 않아 회전이 한 프레임씩 끊겨 보인다. 두 값이 같으므로 물리 스텝의 되쓰기와도 충돌하지 않는다.
        /// </summary>
        private void ApplyRootRotation(Vector2 ballisticFoot)
        {
            Rigidbody2D body = _blackboard.Body;
            if (body == null) return;

            float pivotY = ResolvePivotLocalY();
            float rad = _angle * Mathf.Deg2Rad;
            // R(θ)·(0, p) = (−sinθ·p, cosθ·p)
            float rotatedX = -Mathf.Sin(rad) * pivotY;
            float rotatedY = Mathf.Cos(rad) * pivotY;
            _pivotOffset = new Vector2(-rotatedX, pivotY - rotatedY);

            // ★ 2026-08-30 (횡단 리뷰 m8) — 위치는 반드시 단일 창구를 통한다.
            // 예전에는 여기서 body.position과 body.transform.position을 **손으로 따로** 썼다. 두 값을
            // 모두 쓰고 있었으므로 그때도 결과는 정확했지만, 커밋 dc1e62a가 하나로 모아 둔 창구
            // (StickmanBlackboard.MoveBodyToWorld) 밖의 사본이라 유지보수 중 한 줄만 빠지면 커밋
            // b014611의 "한 프레임 desync"(Rigidbody만 갱신되고 화면은 옛 위치)가 그대로 재발한다.
            // ★ 회전은 창구가 다루지 않으므로(호출부마다 규칙이 다르다) 여기서 계속 직접 쓴다 —
            //   회전도 같은 이유로 Rigidbody2D와 Transform 양쪽에 함께 써야 한다.
            Vector2 pos = ballisticFoot + _pivotOffset;
            _blackboard.MoveBodyToWorld(pos);
            body.rotation = _angle;
            body.angularVelocity = 0f;
            body.transform.rotation = Quaternion.Euler(0f, 0f, _angle);
        }

        /// <summary>회전 중심의 로컬 높이(발바닥 기준). 실측 창구(StickmanMetrics.HipLocalY)가 1순위이고,
        /// 없으면 신장 × 기준 비율로 되메운다 — 어떤 배율에서도 몸의 같은 지점을 축으로 돈다.</summary>
        private float ResolvePivotLocalY()
        {
            StickmanMetrics metrics = _blackboard.Metrics;
            if (metrics != null)
            {
                float hip = metrics.HipLocalY;
                if (hip > 0.0001f) return hip;
            }
            return _blackboard.CharacterHeightWorld * FallbackHipHeightRatio;
        }

        /// <summary>루트를 직립(회전 0) + 보정량 제거 상태로 되돌린다. 보정량을 빼면서 회전을 0으로
        /// 만들기 때문에 **엉덩이 위치는 그대로 유지되고** 발/머리만 제자리를 찾는다 — 몸이 통째로
        /// 튀어 오르거나 가라앉지 않는다. 멱등(두 번 불러도 같은 결과).</summary>
        private void RestoreUprightRoot()
        {
            Rigidbody2D body = _blackboard.Body;
            if (body == null) return;

            Vector2 pos = body.position - _pivotOffset;
            _pivotOffset = Vector2.zero;
            _angle = 0f;

            // 위치는 단일 창구, 회전만 직접(위 ApplyRootRotation의 m8 주석과 같은 이유).
            _blackboard.MoveBodyToWorld(pos);
            body.rotation = 0f;
            body.angularVelocity = 0f;
            body.transform.rotation = Quaternion.identity;
        }

        // ============================================================================
        // 포즈
        // ============================================================================

        /// <summary>웅크린 정도를 목표값으로 수렴시키고 포즈를 적용한다. 상태 자신이 포즈를 적용하는
        /// 것은 Walk/LandingCrouch와 같은 관례다(StickmanBlackboard.TickPose가 이 상태에서는 아무 것도
        /// 하지 않고 빠져나간다).</summary>
        private void TickTuckPose(float deltaTime)
        {
            StickConfig cfg = _blackboard.Config;
            float landingTuck = cfg != null ? Mathf.Clamp01(cfg.throwTumbleLandingTuck01) : 0.15f;
            float rate = cfg != null ? cfg.throwTumbleTuckFadeRate : 10f;
            float target = _landingPrep ? landingTuck : 1f;
            // 프레임레이트 독립 지수 감쇠(이 프로젝트 표준 공식).
            _tuck01 = rate > 0f
                ? Mathf.Lerp(_tuck01, target, 1f - Mathf.Exp(-rate * deltaTime))
                : target;

            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            pose?.ApplyThrowTumblePose(deltaTime, _blackboard.BuildPoseSettings(),
                _blackboard.BuildThrowTumblePoseSettings(), _blackboard.PoseSmoothingRate, _tuck01);
        }

        // ============================================================================
        // 착지
        // ============================================================================

        /// <summary>
        /// 착지 확정 — 위치 스냅 + 무릎앉아로 인계. FallState.ConfirmLanding과 **같은 후처리**를 하되,
        /// 무릎앉아 깊이의 입력이 되는 "낙하 높이"만 던지기에 맞게 환산한다.
        ///
        /// 왜 환산이 필요한가: 던지기는 옆으로도 날아오므로 기하학적 낙차(시작 Y − 착지 Y)만으로는
        /// 세기가 표현되지 않는다. 수평으로 강하게 던져 낮은 포물선으로 꽂히는 착지는 낙차가 거의 0인데
        /// 실제로는 가장 세게 부딪히는 경우다. 그래서 **착지 순간의 속도가 가진 에너지를 같은 충격의
        /// 자유낙하 높이로 환산**한다:
        ///     h = v² / 2g       (수평 성분은 throwTumbleImpactHorizontalWeight만큼만 반영)
        /// 이 식은 임의의 매핑이 아니라 에너지 보존 그대로라, **순수 자유낙하에서는 환산값이 실제
        /// 낙차와 정확히 일치한다**(v² = 2gh). 즉 기존 무릎앉아 램프의 단위/의미와 어긋날 수 없고,
        /// 던지기만 "세게 던질수록 깊게 앉는다"가 추가된다(리더 지시 3항).
        /// 둘 중 큰 값을 쓰는 이유는, 높이 던져 올렸다가 받는 착지처럼 낙차 쪽이 더 큰 경우도 있기 때문이다.
        /// </summary>
        private void ConfirmLanding(float landingWorldY, long footholdHandle)
        {
            Rigidbody2D body = _blackboard.Body;

            // 먼저 회전/보정을 원상 복구한다 — 그 뒤의 위치 스냅과 착지 판정은 예전과 완전히 같은
            // 좌표계(루트 원점 = 발바닥)에서 이루어져야 한다.
            RestoreUprightRoot();

            float impactSpeedSq = 0f;
            if (body != null)
            {
                Vector2 v = body.linearVelocity;
                float w = _blackboard.Config != null
                    ? Mathf.Clamp01(_blackboard.Config.throwTumbleImpactHorizontalWeight)
                    : 0.5f;
                impactSpeedSq = v.y * v.y + w * v.x * v.x;

                // 위치는 단일 창구(위 ApplyRootRotation의 m8 주석 참고).
                Vector2 pos = body.position;
                _blackboard.MoveBodyToWorld(new Vector2(pos.x, landingWorldY));
                if (v.y < 0f)
                {
                    v.y = 0f;
                    body.linearVelocity = v;
                }
            }

            float g = body != null ? Mathf.Abs(Physics2D.gravity.y * body.gravityScale) : 29.43f;
            float energyHeight = g > 0.0001f ? impactSpeedSq / (2f * g) : 0f;
            float geometricHeight = _startWorldY - landingWorldY;
            float effectiveHeight = Mathf.Max(geometricHeight, energyHeight);
            LastLandingEffectiveHeight = effectiveHeight;

            // 부수 연출(발밑 먼지)용 신호 — FallState와 같은 관례로 같은 값 + 착지 좌표를 싣는다.
            float footX = body != null ? body.position.x : 0f;
            StickmanEventBus.RaiseLandingRollRequested(effectiveHeight, new Vector2(footX, landingWorldY));

            _blackboard.CurrentFootholdHandle = footholdHandle;
            _blackboard.ReportFootholdChangeIfNeeded("던지기 회전 착지");
            _blackboard.ResetGroundLossTimer();

            StickConfig cfg = _blackboard.Config;
            bool crouchEnabled = cfg == null || cfg.landingCrouchEnabled;
            bool always = cfg == null || cfg.throwTumbleAlwaysCrouchOnLanding;
            float threshold = cfg != null
                ? cfg.ResolveLandingSoftAbsorbThreshold(_blackboard.CharacterHeightWorld)
                : 0.35f * StickConfig.BaselineCharacterTotalHeight;

            float landedRotation = body != null ? body.rotation : 0f;
            Debug.Log($"[던지기회전] 착지 — 총 회전={MaxAbsAngleDegrees:F0}도(착지 각도={landedRotation:F2}도), " +
                $"기하 낙차={geometricHeight:F2}유닛, 충격 환산={energyHeight:F2}유닛 -> 채택 {effectiveHeight:F2}유닛" +
                $"(무릎앉아 임계 {threshold:F2}, 항상앉기={always}), 발판핸들={footholdHandle}, " +
                $"비행 {_elapsed:F2}초.");

            if (crouchEnabled && (always || effectiveHeight >= threshold))
            {
                _blackboard.LastLandingFallHeight = effectiveHeight;
                _blackboard.Machine.ChangeState(StickmanStateId.LandingCrouch);
                return;
            }

            float deadzone = cfg != null ? cfg.moveInputDeadzone : 0.15f;
            StickmanStateId next = Mathf.Abs(_blackboard.MoveInputX) > deadzone
                ? StickmanStateId.Walk
                : StickmanStateId.Idle;
            _blackboard.Machine.ChangeState(next);
        }
    }
}
