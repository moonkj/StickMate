using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// ★★ 활쏘기 연출(2026-08-29, 사용자 명시 요청: "하는 행동중 하나가 활을 들고 화살을 쏘는건데
    /// 과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동을 하는거지").
    ///
    /// 캐릭터가 제자리에 멈춰 서서 <b>당기기 → 조준 정지 → 발사</b>를 3회 반복하는 능동 상태다.
    /// 과녁 소환/배치와 트리거는 Interaction/ArcheryDirector.cs가, 실제로 보이는 과녁·활·화살은
    /// Interaction/ArcheryRenderer.cs가 담당한다. 이 상태가 소유하는 것은 <b>타이밍과 포즈, 그리고
    /// 명중/빗나감 시나리오</b> 세 가지뿐이다.
    ///
    /// ============================================================================
    /// 왜 TimedSpectacleState를 재사용하지 않았는가
    /// ============================================================================
    /// LandingCrouchState와 같은 이유다. TimedSpectacleState는 "캐릭터 쪽 부수 효과가 전혀 없는 순수
    /// 타이머"인데, 이 상태는 (1) 매 프레임 포즈를 직접 구동하고(당김 진행도 -> ApplyArcheryPose),
    /// (2) 단일 타이머가 아니라 3발 x 3단계의 페이즈 머신이며, (3) 발마다 결과 이벤트를 발행한다.
    ///
    /// ============================================================================
    /// ★ "물리로 던져놓고 우연에 맡기지 않는다"(리더 지시)
    /// ============================================================================
    /// 명중/빗나감은 <see cref="Enter"/>에서 <b>미리 전부 뽑아둔다</b>(<see cref="_results"/>).
    /// 그리고 화살의 궤적은 그 확정된 도달점을 지나도록 <b>역산</b>된다(ArcheryRenderer.SolveLaunch —
    /// 도달점과 비행 시간이 주어지면 초기 속도가 유일하게 결정된다). 화살에 Rigidbody2D를 달아 힘을
    /// 주는 방식이 아니므로 프레임레이트/충돌 우연에 따라 연출이 매번 달라지는 일이 원리적으로 없다.
    ///
    /// 시나리오 구성(3발이 똑같으면 지루하다 — 리더 지시):
    ///   · <b>마지막 발은 항상 정중앙</b>(Bullseye). 연출의 클라이맥스를 고정한다.
    ///   · 앞의 두 발 중 <b>정확히 하나가 빗나간다</b>(Miss, 과녁에 못 미치고 앞 땅에 꽂힘).
    ///     어느 쪽이 빗나갈지는 매번 새로 뽑으므로 볼 때마다 순서가 다르다.
    ///   · 나머지 한 발은 과녁 바깥 링에 꽂힌다(Hit).
    /// 즉 "빗나감 1 + 외곽 1 + 정중앙 1"의 순서만 섞이는 구조다. 3발 전부를 독립 추첨하면
    /// "3발 다 빗나감"/"3발 다 정중앙" 같은 김빠지는 조합이 나오고, 그걸 사후에 걸러내는 코드는
    /// 결국 여기서 하는 일과 같아진다.
    ///
    /// ============================================================================
    /// 대사를 넣지 않았다 (원칙 1과 별개의 판단)
    /// ============================================================================
    /// LandingCrouchState와 같은 판단이다 — 이 프로젝트 사용자는 요청하지 않은 자율 연출/대사에
    /// 반복적으로 민감했고, 이번 요청도 "행동"에 대한 것이다. 나중에 붙인다면 반드시
    /// <b>결과가 확정된 뒤</b>(<see cref="_results"/>가 정해진 Enter, 또는 명중 판정이 끝난 self-transition)
    /// 그 결과에서만 파생시켜야 한다. 대사를 먼저 정하고 명중 여부를 끼워 맞추지 않는다.
    /// </summary>
    public sealed class ArcheryState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        /// <summary>한 사이클에 쏘는 화살 수. 사용자 요청 "3번정도"의 그 3이며, 시나리오 구성
        /// (빗나감 1 + 외곽 1 + 정중앙 1)이 이 값에 맞춰져 있으므로 튜닝 스칼라가 아니라 구조 상수다.
        /// StickConfig가 아니라 여기 상수로 두는 이유는 보행 키프레임 표와 같은 판단 기준이다.</summary>
        public const int ShotCount = 3;

        /// <summary>Approach = 과녁을 세울 자리까지 <b>실제로 걸어가는</b> 구간(사용자 명시 요구:
        /// "…만큼 캐릭터가 이동한 다음 과녁을 생성후 쏘고"). 도착해야 비로소 과녁이 등장한다(Intro).</summary>
        private enum Phase { Approach, Intro, Draw, Aim, Recover, Outro }

        private Phase _phase;
        private float _timer;
        private int _shotIndex;
        private float _recoilTimer;   // >0이면 발사 반동 재생 중.

        private readonly ArcheryShotResult[] _results = new ArcheryShotResult[ShotCount];

        // ==================== 진단/테스트용 관찰 창구 ====================

        /// <summary>지금까지 실제로 발사한 화살 수(0~3). PlayMode 테스트가 "3발이 정말 나갔는가"를
        /// 로그가 아니라 값으로 단언하는 데 쓴다.</summary>
        public int ShotsFired { get; private set; }

        /// <summary>이번 사이클에서 뽑힌 결과 시나리오(읽기 전용 복사본이 아니라 인덱서 조회).</summary>
        public ArcheryShotResult ResultAt(int index)
            => index >= 0 && index < ShotCount ? _results[index] : ArcheryShotResult.Miss;

        /// <summary>지금 프레임의 시위 당김 진행도(0=놓은 상태, 1=완전히 당김).</summary>
        public float CurrentDrawRatio { get; private set; }

        /// <summary>활을 꺼내 든 정도(0~1). 과녁 등장 구간에서 0->1로 올라가고 그 뒤로는 사이클 내내 1이다.
        /// 당김과 분리해 둔 이유: 시위를 놓은 구간에도 활은 계속 들려 있어야 한다(육안 검증에서 잡은 실수 —
        /// 하나로 묶었더니 발사 직후마다 활이 옆구리로 내려갔다).</summary>
        public float CurrentReadyRatio { get; private set; }

        public ArcheryState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Archery;

        public void Enter(StateTransitionContext context)
        {
            _cfg = _blackboard.Config;
            _phase = Phase.Approach;
            _timer = 0f;
            _shotIndex = 0;
            _recoilTimer = 0f;
            ShotsFired = 0;
            CurrentDrawRatio = 0f;
            CurrentReadyRatio = 0f;

            BuildScenario();

            _blackboard.ArcheryBowVisible = true;
            _blackboard.ArcheryDrawRatio = 0f;
            _blackboard.ArcheryReadyRatio = 0f;
            // 조준 중에 배회 AI의 이동 의도로 몸이 홱 돌아가면 화살이 뒤통수에서 나가는 그림이 된다.
            // 이 상태 동안만 바라보는 방향을 고정한다(Exit에서 반드시 해제 — 아래).
            // 걸어가는 동안에는 진행 방향을 봐야 하므로 아직 방향을 고정하지 않는다 — 도착해서
            // 과녁 쪽으로 돌아선 뒤에 고정한다(BeginIntro).
            _blackboard.FacingLocked = false;

            Debug.Log($"[활쏘기] 접근 시작 — x={_blackboard.Body.position.x:F2}에서 " +
                $"x={_blackboard.ArcheryStandWorldX:F2}까지 걸어간 뒤 과녁을 세웁니다. " +
                $"과녁 예정 {_blackboard.ArcheryTargetWorld.ToString("F2")}, " +
                $"지면 y={_blackboard.ArcheryGroundWorldY:F2}, 방향={(_blackboard.ArcheryFacingSign > 0f ? "오른쪽" : "왼쪽")}. " +
                $"시나리오 = {_results[0]} / {_results[1]} / {_results[2]} " +
                "(마지막은 항상 정중앙, 앞 두 발 중 하나는 반드시 빗나감 — 미리 확정한 뒤 그 도달점을 " +
                "지나도록 궤적을 역산하므로 물리 우연에 맡기지 않는다).");
        }

        /// <summary>클래스 문서 "시나리오 구성" 그대로 — 마지막은 정중앙 고정, 앞 두 발 중 하나만 빗나감.</summary>
        private void BuildScenario()
        {
            int missIndex = Random.Range(0, ShotCount - 1); // 0 또는 1.
            for (int i = 0; i < ShotCount - 1; i++)
            {
                _results[i] = i == missIndex ? ArcheryShotResult.Miss : ArcheryShotResult.Hit;
            }
            _results[ShotCount - 1] = ArcheryShotResult.Bullseye;
        }

        public void Tick(float deltaTime)
        {
            // 연출 중에도 발판은 계속 확인한다 — 이 프로젝트의 발판은 실제 타 앱 창이라 몇 초 사이에
            // 닫히거나 움직일 수 있다(LandingCrouchState와 같은 이유).
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            if (_phase == Phase.Approach)
            {
                TickApproach(deltaTime);
            }
            else if (_blackboard.Body != null)
            {
                // "캐릭터가 멈춰 서고" — 남은 수평 속도를 지수 감쇠로 죽인다(프레임레이트 독립, 프로젝트 표준).
                float damping = (_cfg != null ? _cfg.archeryHorizontalDamping : 14f);
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = damping > 0f ? v.x * Mathf.Exp(-damping * deltaTime) : 0f;
                _blackboard.Body.linearVelocity = v;
            }

            _timer += deltaTime;
            if (_recoilTimer > 0f) _recoilTimer = Mathf.Max(0f, _recoilTimer - deltaTime);

            TickPhase();

            // 활을 꺼내 드는 램프 — 과녁이 등장하는 동안(Intro) 0->1로 올라가고, 그 뒤로는 사이클이
            // 끝날 때까지 1을 유지한다.
            float intro = Mathf.Max(0.05f, (_cfg != null ? _cfg.archeryTargetIntroSeconds : 0.55f));
            CurrentReadyRatio = _phase == Phase.Approach ? 0f
                : _phase == Phase.Intro ? Mathf.Clamp01(_timer / intro)
                : 1f;

            _blackboard.ArcheryDrawRatio = CurrentDrawRatio;
            _blackboard.ArcheryReadyRatio = CurrentReadyRatio;

            float recoilSeconds = (_cfg != null ? _cfg.archeryRecoilSeconds : 0.18f);
            float recoil01 = recoilSeconds > 0.0001f ? Mathf.Clamp01(_recoilTimer / recoilSeconds) : 0f;

            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            if (pose == null) return;

            if (_phase == Phase.Approach)
            {
                // 걷는 구간은 **보행 포즈 그대로** 쓴다 — WalkState.Tick()과 완전히 같은 인자로 호출하므로
                // "활쏘기 전용 걷기"라는 두 번째 보행 구현이 생기지 않는다(같은 값의 두 번째 계산원 금지).
                float speed = Mathf.Abs(_blackboard.Body != null ? _blackboard.Body.linearVelocity.x : 0f);
                pose.TickWalkPose(deltaTime, speed, _blackboard.BuildPoseSettings(),
                    _blackboard.PoseSmoothingRate, _blackboard.WalkSpeedSmoothingRate,
                    _cfg != null ? _cfg.walkFootGroundingBlend : 1f,
                    _cfg != null ? _cfg.walkPoseAmplitudeScale : 1f,
                    _cfg != null ? _cfg.walkStrideScale : 1f,
                    _blackboard.RunBodyLeanDegrees);
                return;
            }

            pose.ApplyArcheryPose(deltaTime, _blackboard.BuildPoseSettings(),
                _blackboard.BuildArcheryPoseSettings(), _blackboard.ArcheryPoseSmoothingRate,
                CurrentReadyRatio, CurrentDrawRatio, recoil01);
        }

        private void TickPhase()
        {
            switch (_phase)
            {
                case Phase.Approach:
                    // 이동은 TickApproach()가 담당하고, 도착 판정도 거기서 한다.
                    CurrentDrawRatio = 0f;
                    break;

                case Phase.Intro:
                    // 과녁이 등장하는 동안은 아직 활을 당기지 않는다(연출 순서: 도착 -> 과녁 -> 활).
                    CurrentDrawRatio = 0f;
                    if (_timer >= (_cfg != null ? _cfg.archeryTargetIntroSeconds : 0.55f)) BeginDraw();
                    break;

                case Phase.Draw:
                {
                    float draw = Mathf.Max(0.05f, (_cfg != null ? _cfg.archeryDrawSeconds : 0.42f));
                    float u = Mathf.Clamp01(_timer / draw);
                    // easeOut — 처음에 빠르게 당기고 마지막에 버티듯 느려진다("힘겹게 끝까지 당긴다").
                    CurrentDrawRatio = 1f - (1f - u) * (1f - u);
                    if (_timer >= draw) { _phase = Phase.Aim; _timer = 0f; }
                    break;
                }

                case Phase.Aim:
                    // 조준 정지 — 애니메이션의 hold. 이 정지가 있어야 "겨눴다"가 한 장의 그림으로 남는다.
                    CurrentDrawRatio = 1f;
                    if (_timer >= (_cfg != null ? _cfg.archeryAimHoldSeconds : 0.30f)) Release();
                    break;

                case Phase.Recover:
                {
                    float recover = Mathf.Max(0.05f, (_cfg != null ? _cfg.archeryRecoverSeconds : 0.34f));
                    CurrentDrawRatio = 0f;
                    if (_timer < recover) break;
                    if (_shotIndex < ShotCount) BeginDraw();
                    else { _phase = Phase.Outro; _timer = 0f; }
                    break;
                }

                case Phase.Outro:
                    CurrentDrawRatio = 0f;
                    // 마지막 화살이 날아가 꽂히는 것을 끝까지 보여준 뒤에 끝낸다.
                    if (_timer >= (_cfg != null ? _cfg.archeryArrowFlightSeconds : 0.62f) + (_cfg != null ? _cfg.archeryOutroSeconds : 0.75f))
                    {
                        _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                    }
                    break;
            }
        }

        /// <summary>
        /// 과녁을 세울 자리까지 걸어간다. <b>순간이동하지 않는다</b>(사용자 명시). 도착하면 과녁 쪽으로
        /// 돌아서고 방향을 고정한 뒤 과녁을 등장시킨다.
        ///
        /// 타임아웃을 두는 이유: 발판이 도중에 사라지거나 화면 클램프에 걸려 목표 X에 영원히 도달하지
        /// 못할 수 있다. 그때는 그 자리에서 시작한다 — 거리 조건은 조금 못 미치더라도 "아무 일도
        /// 일어나지 않는" 것보다는 낫고, 애초에 자리 자체는 발동 시점에 검증됐다.
        /// </summary>
        private void TickApproach(float deltaTime)
        {
            if (_blackboard.Body == null) { BeginIntro("몸통 미배선"); return; }

            float targetX = _blackboard.ArcheryStandWorldX;
            float dx = targetX - _blackboard.Body.position.x;
            float arrive = _blackboard.CharacterHeightWorld * ArriveToleranceRatio;
            float timeout = Mathf.Max(1f, (_cfg != null ? _cfg.archeryApproachTimeoutSeconds : 12f));

            if (Mathf.Abs(dx) <= arrive) { BeginIntro("도착"); return; }
            if (_timer >= timeout) { BeginIntro($"타임아웃({timeout:F0}초) — 남은 거리 {Mathf.Abs(dx):F2}유닛"); return; }

            float dir = dx >= 0f ? 1f : -1f;
            float speed = _cfg != null ? _cfg.ResolveWalkSpeed() : 2.5f;
            Vector2 v = _blackboard.Body.linearVelocity;
            v.x = dir * speed;
            _blackboard.Body.linearVelocity = v;
            _blackboard.SetFacingSign(dir); // 걷는 동안에는 진행 방향을 본다.
        }

        /// <summary>도착 — 과녁 쪽으로 돌아서고, 방향을 고정하고, <b>이제야</b> 과녁을 등장시킨다.</summary>
        private void BeginIntro(string reason)
        {
            _phase = Phase.Intro;
            _timer = 0f;
            CurrentDrawRatio = 0f;

            if (_blackboard.Body != null)
            {
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = 0f;
                _blackboard.Body.linearVelocity = v;
            }

            float facing = _blackboard.ArcheryFacingSign >= 0f ? 1f : -1f;
            _blackboard.SetFacingSign(facing);
            _blackboard.FacingLocked = true;

            // ★ 과녁은 **여기서** 나타난다(사용자 요구 순서: 이동 -> 과녁 생성 -> 발사).
            // 발행 주체가 Director가 아니라 이 상태인 이유: "언제 과녁이 보여야 하는가"를 아는 것은
            // 이동이 끝났음을 아는 이 상태뿐이다. 종료(Completed/Cancelled)는 생애주기를 아는
            // Director가 계속 담당한다.
            StickmanEventBus.RaiseArcheryOverlayChanged(_blackboard.ArcheryTargetWorld,
                _blackboard.ArcheryGroundWorldY, facing, SpectacleOverlayPhase.Started);

            Debug.Log($"[활쏘기] 자리 도착({reason}) — x={(_blackboard.Body != null ? _blackboard.Body.position.x : 0f):F2}. " +
                $"과녁 {_blackboard.ArcheryTargetWorld.ToString("F2")}까지 " +
                $"{Mathf.Abs(_blackboard.ArcheryTargetWorld.x - (_blackboard.Body != null ? _blackboard.Body.position.x : 0f)):F2}유닛. " +
                "이제 과녁을 세우고 3발을 쏩니다.");
        }

        /// <summary>목표 X에 "도착했다"고 볼 허용 오차(신장 배수).</summary>
        private const float ArriveToleranceRatio = 0.12f;

        private void BeginDraw()
        {
            _phase = Phase.Draw;
            _timer = 0f;
            CurrentDrawRatio = 0f;
            RaiseShot(ArcheryShotPhase.Aim);
        }

        private void Release()
        {
            RaiseShot(ArcheryShotPhase.Release);
            ShotsFired++;
            _shotIndex++;
            _recoilTimer = (_cfg != null ? _cfg.archeryRecoilSeconds : 0.18f);
            _phase = Phase.Recover;
            _timer = 0f;
            CurrentDrawRatio = 0f;
        }

        /// <summary>
        /// 이번 발의 <b>확정된</b> 도달점을 함께 실어 발행한다. 렌더러는 이 좌표를 지나도록 궤적을
        /// 역산할 뿐 스스로 명중 여부를 판단하지 않는다 — 판정과 그림이 어긋날 경우의 수를 없앤다.
        /// </summary>
        private void RaiseShot(ArcheryShotPhase phase)
        {
            int index = Mathf.Clamp(_shotIndex, 0, ShotCount - 1);
            ArcheryShotResult result = _results[index];
            Vector2 impact = ComputeImpactWorld(result);
            float flight = ResolveFlightSeconds(impact);
            _lastFlightSeconds = flight;

            StickmanEventBus.RaiseArcheryShotChanged(index, phase, result, impact, flight);

            if (phase == ArcheryShotPhase.Release)
            {
                Debug.Log($"[활쏘기] {index + 1}발째 발사 — 결과={result}(사전 확정), " +
                    $"도달점={impact.ToString("F2")}, 비행 {flight:F2}초. 궤적은 이 도달점을 지나도록 " +
                    "역산된 포물선이며 물리 시뮬레이션이 아니다.");
            }
        }

        /// <summary>
        /// 이번 화살의 비행 시간(초). ★ 고정값이 아니라 <b>실제 사거리에서 파생</b>한다 — 사용자 요구로
        /// 사거리가 "창 폭 전체"(실측 25유닛, 화면상 900pt)까지 늘어났는데, 기준 사거리(신장의 4.6배)에
        /// 맞춘 0.62초를 그대로 쓰면 초당 1600pt가 넘어 화살이 그냥 섬광으로 보인다.
        ///
        /// 거리의 <b>제곱근</b>에 비례시키는 이유: 선형이면 먼 사격이 3배 넘게 느려져 "화살이 너무 늦게
        /// 날라감"(사용자 신고 7번)이 되돌아오고, 고정이면 위의 섬광 문제가 된다. 제곱근은 기준
        /// 사거리에서 정확히 설정값과 같아지면서 먼 사격만 완만히 늘어난다. 상한도 함께 건다.
        /// </summary>
        private float ResolveFlightSeconds(Vector2 impactWorld)
        {
            float baseSeconds = Mathf.Max(0.05f, _cfg != null ? _cfg.archeryArrowFlightSeconds : 0.62f);
            float maxSeconds = Mathf.Max(baseSeconds, _cfg != null ? _cfg.archeryArrowFlightMaxSeconds : 1.25f);
            float height = _blackboard.CharacterHeightWorld;
            float reference = height * Mathf.Max(0.5f, _cfg != null ? _cfg.archeryTargetDistanceRatio : 4.6f);
            float originX = _blackboard.Body != null ? _blackboard.Body.position.x : impactWorld.x;
            float distance = Mathf.Abs(impactWorld.x - originX);
            float scale = Mathf.Sqrt(Mathf.Max(0.25f, distance / Mathf.Max(0.0001f, reference)));
            return Mathf.Clamp(baseSeconds * scale, baseSeconds * 0.6f, maxSeconds);
        }

        /// <summary>마지막으로 쏜 화살의 비행 시간(초) — Outro가 "마지막 화살이 도달할 때까지" 기다리는 데 쓴다.</summary>
        private float _lastFlightSeconds = 0.62f;

        /// <summary>
        /// 결과별 도달점(월드). <b>빗나감은 과녁 앞 땅</b>에 꽂힌다 — 과녁 뒤로 넘기면 화살이 과녁
        /// 면을 관통해 지나가는 그림이 되어(계산상 궤적이 과녁 사각형 안을 통과한다) "빗나갔다"가
        /// 아니라 "뚫었다"로 읽힌다. 앞 땅에 꽂히면 궤적이 과녁 근처 x에 도달하기 전에 끝나므로
        /// 겹칠 경우의 수 자체가 없다.
        /// </summary>
        private Vector2 ComputeImpactWorld(ArcheryShotResult result)
        {
            Vector2 center = _blackboard.ArcheryTargetWorld;
            float height = _blackboard.CharacterHeightWorld;
            float radius = height * (_cfg != null ? _cfg.archeryTargetRadiusRatio : 0.40f);
            float facing = _blackboard.ArcheryFacingSign >= 0f ? 1f : -1f;

            switch (result)
            {
                case ArcheryShotResult.Bullseye:
                    return center;

                case ArcheryShotResult.Hit:
                {
                    // 바깥 링 어딘가 — 정중앙과 확실히 구분되는 반경대에서만 뽑는다.
                    float r = Random.Range(radius * 0.45f, radius * 0.80f);
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    return center + new Vector2(Mathf.Cos(a) * r * 0.35f, Mathf.Sin(a) * r);
                    // x 성분을 줄이는 이유: 과녁은 정면에서 보는 평면이라 좌우로 벗어난 명중점은
                    // 화살이 과녁 옆 허공에 꽂힌 것처럼 보인다. 세로로 흩는 편이 훨씬 자연스럽다.
                }

                default:
                {
                    float shortfall = radius * Mathf.Max(1.05f, (_cfg != null ? _cfg.archeryMissShortfallRadii : 1.5f));
                    return new Vector2(center.x - facing * shortfall, _blackboard.ArcheryGroundWorldY);
                }
            }
        }

        public void Exit()
        {
            _blackboard.ArcheryBowVisible = false;
            _blackboard.ArcheryDrawRatio = 0f;
            _blackboard.ArcheryReadyRatio = 0f;
            CurrentDrawRatio = 0f;
            CurrentReadyRatio = 0f;
            // 어떤 경로로 나가든(정상 종료/강제 인터럽트/긴급정지) 방향 고정은 반드시 풀린다 —
            // 여기서 빠뜨리면 캐릭터가 영영 한쪽만 보고 걷는다.
            _blackboard.FacingLocked = false;
        }

        /// <summary>Enter에서 한 번만 잡아두는 설정 참조 — 매 프레임 경로에서 델리게이트/널체크를
        /// 반복하지 않기 위한 캐시(24시간 상주 앱 컨벤션). Config가 없는 테스트 리그에서는 null이며
        /// 모든 사용처가 각자 폴백 상수를 갖는다.</summary>
        private StickConfig _cfg;
    }
}
