using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 11절 라이벌 스틱맨 대결 — 플레이어(StickmanAgent)와 별개의 상태머신 인스턴스를
    /// 갖는 독립 컴포넌트. 관전 전용(유저 개입 없음, 클릭관통 그대로 유지 — 부분적 클릭관통 해제를
    /// 절대 쓰지 않는다, 10/12/13절과 명확히 분리된 설계 원칙)이라 최소 스코프로 구현한다:
    /// - 별도 StickmanBlackboard/StickmanStateMachine 인스턴스(플레이어와 물리적으로 다른 Rigidbody2D).
    /// - 발판(FootholdPoller)/카메라는 플레이어(StickmanAgent)와 공유 — "두 캐릭터가 발판을 공유"
    ///   요구사항을 발판 재열거 없이 참조 공유로 만족시킨다.
    /// - 추적 AI는 States/AutoWanderController.cs(배회용)를 재사용하지 않고 RivalPursuitIntentSource
    ///   (목표=플레이어 위치로 단순 추적)를 별도 사용.
    /// - Idle/Walk/Fall/Attack/Ragdoll/Getup 6종만 등록(점프/파쿠르는 최소 스코프에서 다루지 않음 —
    ///   RivalPursuitIntentSource.JumpRequested가 항상 false라 등록해도 어차피 트리거되지 않지만,
    ///   등록 자체를 생략해 "라이벌은 파쿠르를 하지 않는다"는 스코프를 코드로도 명확히 한다).
    ///
    /// 전투 판정은 이 컴포넌트가 "심판" 역할로 직접 수행한다(States.AttackState는 순수하게 "타격
    /// 모션을 재생하고 복귀"만 담당 — 누가 누구를 때리는지는 모른다). 근접 시 무작위로 선타를 정해
    /// RagdollImpactResolver(자신이 맞았을 때)/StickmanAgent.ReportExternalImpact(플레이어가 맞았을
    /// 때, Suspended 가드를 지키기 위해 반드시 이 공개 메서드를 거친다)로 피격을 적용한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RivalStickmanAgent : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private Rigidbody2D _body;
        private Renderer[] _renderers;
        private LineRenderer[] _lineRenderers;   // 붉은색 일괄 적용 대상(StickmanAgent.ApplyInkColor와 동일 패턴).
        private DialogueBubbleRenderer _bubble;  // 라이벌 전용 말풍선(있으면 자기 상태머신에 바인딩).
        private StickmanBlackboard _blackboard;
        private StickmanStateMachine _machine;
        private RivalPursuitIntentSource _pursuit;
        private StickmanAgent _opponent;

        private bool _inDuel;
        private float _durationTimer;
        private float _attackCooldownTimer;
        private int _hitsTakenByRival;   // 라이벌이 맞은 횟수(=플레이어 승리 조건에 근접)
        private int _hitsTakenByPlayer;  // 플레이어가 맞은 횟수(=라이벌 승리 조건에 근접)

        public bool InDuel => _inDuel;

        /// <summary>
        /// 이 라이벌의 상태/물리 컨텍스트(대결 전에는 null — <see cref="BeginDuel"/>의
        /// EnsureMachineBuilt()에서 최초 1회 만들어진다). Core/StickmanAgent.Blackboard와 **같은 의도**로
        /// 공개한다: 이 프로젝트의 검증은 로그와 PlayMode 실측이 유일한 수단인데, 라이벌만 이 통로가
        /// 없어서 "플레이어는 고쳤는데 라이벌은 그대로"인 결함(2026-08-30 "한 명이 독 아래에서 계속
        /// 쓰러짐")을 테스트로 잠글 방법이 아예 없었다.
        /// </summary>
        public StickmanBlackboard Blackboard => _blackboard;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _lineRenderers = GetComponentsInChildren<LineRenderer>(true);
            _bubble = GetComponent<DialogueBubbleRenderer>();
            _body.simulated = false; // 스폰 전에는 완전히 비활성 — 관전 전용 스펙터클이 시작될 때만 등장.
            SetRenderersEnabled(false);
            ApplyRivalInkColor();
        }

        /// <summary>
        /// 11절 "붉은 스틱맨" — 자기 LineRenderer 전체를 StickConfig.rivalInkColor로 칠한다.
        /// 씬에 구워둔 색에 의존하지 않고 런타임에 한 번 더 적용하는 이유는 플레이어 쪽
        /// (StickmanAgent.ApplyInkColorFromConfig)과 같다: 에셋 값만 바꿔도 씬/프리팹 재생성 없이
        /// 색이 바뀌어야 하기 때문이다. 플레이어의 잉크색 프리셋(검정/흰색) 전환과는 **독립적**이다 —
        /// 두 캐릭터가 같은 색이 되면 누가 라이벌인지 구분할 수 없다.
        /// </summary>
        private void ApplyRivalInkColor()
        {
            if (_lineRenderers == null || _config == null) return;
            Color c = _config.rivalInkColor;
            for (int i = 0; i < _lineRenderers.Length; i++)
            {
                LineRenderer lr = _lineRenderers[i];
                if (lr == null) continue;
                lr.startColor = c;
                lr.endColor = c;
                if (lr.material != null) lr.material.color = c;
            }
        }

        /// <summary>스폰 확률/쿨다운 판정은 Interaction/RivalEncounterDirector.cs가 전담한다 — 이 메서드는
        /// "지금 대결을 시작한다"는 확정된 사실만 받는다.</summary>
        public void BeginDuel(StickmanAgent opponent, Vector2 spawnWorldPos)
        {
            if (_inDuel || opponent == null) return;
            _opponent = opponent;

            _body.simulated = true;
            // ★ StickmanBlackboard.MoveBodyToWorld와 같은 이유로 Rigidbody2D.position만이 아니라
            // Transform.position도 함께 옮긴다(2026-08-29, "몸 순간이동은 항상 둘 다" 원칙 후속 적용).
            // AutoSyncTransforms가 꺼져 있어(ProjectSettings/Physics2DSettings.asset), 여기서
            // Rigidbody2D만 옮기면 바로 아래 SetRenderersEnabled(true)로 보이게 되는 첫 프레임에
            // Awake() 시점 프리팹 배치 좌표(스폰 좌표가 아닌 곳)가 그대로 그려진다 — RunawayState.
            // RestoreCharacter()에서 실측 확인된 것과 동일한 1프레임 팝 패턴이다. 이 컴포넌트는 별도
            // StickmanBlackboard 인스턴스를 아직 갖고 있지 않을 수 있어(EnsureMachineBuilt 이전)
            // 공용 창구를 못 쓰므로, 여기서는 같은 두 줄을 직접 적용한다.
            _body.position = spawnWorldPos;
            Transform bodyTransform = _body.transform;
            bodyTransform.position = new Vector3(spawnWorldPos.x, spawnWorldPos.y, bodyTransform.position.z);
            _body.linearVelocity = Vector2.zero;
            SetRenderersEnabled(true);

            EnsureMachineBuilt();
            ApplyRivalInkColor(); // _config가 EnsureMachineBuilt에서 뒤늦게 채워졌을 수 있다.

            _inDuel = true;
            _durationTimer = 0f;
            _attackCooldownTimer = 0f;
            _hitsTakenByRival = 0;
            _hitsTakenByPlayer = 0;

            StickmanEventBus.RaiseRivalDuelStarted();
            Debug.Log($"[라이벌] 등장 — 스폰 좌표 {spawnWorldPos}, 색 {(_config != null ? _config.rivalInkColor.ToString() : "기본 붉은색")}, " +
                      $"최대 지속 {(_config != null ? _config.rivalMaxDurationSeconds : 30f):F0}초. 서로 쫓아다니며 싸웁니다(관전 전용).");
        }

        private void EnsureMachineBuilt()
        {
            if (_blackboard != null) return; // 최초 1회만 구성(매 대결마다 재구성 금지 — GC/재탐색 방지).

            // 심층 방어 — 씬 배선이 비어 있어도 플레이어와 같은 설정으로 동작하게 한다
            // (Interaction/RivalEncounterDirector.Awake()와 같은 이유).
            if (_config == null) _config = _opponent.Blackboard.Config;

            _blackboard = new StickmanBlackboard
            {
                Body = _body,
                MainCamera = _opponent.Blackboard.MainCamera,
                Config = _config,
                FootholdPoller = _opponent.Blackboard.FootholdPoller, // 발판 공유(11절 "두 캐릭터가 발판을 공유")
            };

            _pursuit = new RivalPursuitIntentSource(_blackboard, _config, () => _opponent.Blackboard.Body.position);
            _blackboard.IntentSource = _pursuit;

            var states = new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new IdleState(_blackboard) },
                { StickmanStateId.Walk, new WalkState(_blackboard) },
                { StickmanStateId.Fall, new FallState(_blackboard) },
                // ★ 필수(2026-08-29 "무릎앉아 착지" 라운드): FallState는 낙하 높이가
                // StickConfig.rollLandingHeightThreshold를 넘으면 LandingCrouch로 전이한다. 라이벌도
                // 같은 FallState 인스턴스 타입을 쓰므로 여기 등록하지 않으면 ChangeState가 BUG-M2 방어
                // 코드를 밟아(에러 로그 + 현재 상태 유지) 라이벌이 Fall에 영구 고착된다.
                { StickmanStateId.LandingCrouch, new LandingCrouchState(_blackboard) },
                { StickmanStateId.Attack, new AttackState(_blackboard) },
                { StickmanStateId.Ragdoll, new RagdollState(_blackboard) },
                { StickmanStateId.Getup, new GetupState(_blackboard) },
            };

            _machine = new StickmanStateMachine(states);
            _blackboard.Machine = _machine;

            // UX_FLOW.md 5절 규칙 7(다중 캐릭터 동시 발화) — 라이벌의 말풍선은 **자기 상태머신이 발급한
            // 대사만** 그린다. 머신은 첫 대결에서야 만들어지므로 여기가 바인딩할 수 있는 가장 이른
            // 시점이고, 그 전까지는 렌더러의 _requireBoundSpeaker 플래그가 "화자 미지정 = 전부 수신"
            // 폴백을 막아 플레이어의 대사가 라이벌 머리 위에 뜨는 사고를 원천 차단한다.
            _bubble?.Bind(_machine, transform.Find("Head") != null ? transform.Find("Head") : transform);
            // 만화 레터링 배치(2026-08-29)는 "진행 방향의 반대쪽"에 글자를 놓는다. 라이벌은 자기
            // StickmanBlackboard를 따로 들고 있어(StickmanAgent가 아니다) 렌더러가 자동으로 찾을 수
            // 없으므로, 자기 방향을 여기서 직접 물려준다 — 없으면 렌더러가 플레이어의 방향을 읽어
            // 라이벌의 글자가 엉뚱한 쪽에 붙는다(규칙 7 화자 분리의 배치 판).
            if (_bubble != null) _bubble.FacingSource = () => _blackboard != null ? _blackboard.FacingSign : 1f;

            _machine.Start(StickmanStateId.Idle);
        }

        private void Update()
        {
            if (!_inDuel) return;

            // UX 11절 예외: "대결 중 전체화면 게임 감지 → 즉시 취소(6-4절 우선)". 라이벌은 플레이어의
            // StickmanStateMachine에 속하지 않아 StickmanAgent.Suspend()의 일반 처리 대상이 아니므로,
            // 여기서 직접 IsSuspended를 폴링해 동일한 우선순위를 보장한다.
            if (_opponent == null || _opponent.IsSuspended)
            {
                EndDuel(RivalDuelResult.Draw);
                return;
            }

            float dt = Time.deltaTime;
            _durationTimer += dt;

            _pursuit.Tick(dt);
            _machine.Tick(dt);

            // ================================================================================
            // ★★ 2026-08-30 (디버거) — 사용자 신고 **"한 명이 독 아래에서 계속 쓰러짐"**
            // ================================================================================
            // 라이벌은 플레이어(Core/StickmanAgent.Update)가 매 프레임 보장하는 **세 가지**를 하나도
            // 하지 않고 있었다. 그래서 같은 상황에서 플레이어만 회복하고 라이벌만 Dock 아래에 남았다 —
            // 사용자가 "한 명이"라고 정확히 짚은 비대칭의 정체다. 실측 인과:
            //
            //   · 대결 중 라이벌은 rivalAttackCooldownSeconds(1.2초)마다 AttackState에 들어간다.
            //     AttackState는 접지 스냅(GroundedTick)을 부르지 않고, 이 프로젝트의 발판
            //     (Dock/창 상단)은 **논리 발판일 뿐 물리 콜라이더가 없다.** attackDuration 0.4초
            //     자유낙하 = 0.5*29.43*0.4² = 2.35유닛 > Dock 단차 1.64유닛 —
            //     **공격 한 번마다 Dock 아래로 가라앉는다.**
            //   · 가라앉은 자리는 논리 발판이 없는 사각지대라 착지가 확정되지 않고 Fall에 고착된다.
            //     플레이어에게는 6초 강제 복귀(EnforceScreenBoundsAndRescue)가 있지만 **라이벌은
            //     그 호출 자체가 없어서 영원히 못 나온다.**
            //   · 그 상태에서 플레이어의 반격(TickCombatExchange)이 계속 들어와 RAGDOLL이 반복된다
            //     = "계속 쓰러짐".
            //
            // 고치는 방법은 "라이벌 전용 예외"를 만드는 것이 아니라, **플레이어와 같은 세 줄을 같은
            // 순서로 실행**하는 것이다. 라이벌은 별도 StickmanBlackboard/StickmanStateMachine
            // 인스턴스를 갖고 있을 뿐 계약은 완전히 동일하므로, 두 캐릭터의 프레임 계약이 갈라져
            // 있었다는 사실 자체가 결함이었다(이 프로젝트가 반복해 겪은 "한쪽만 고치는" 실패 유형).
            _blackboard.TickGroundKeepingSafetyNet(dt);
            _blackboard.TickPose(dt);

            TickCombatExchange(dt);

            // 플레이어와 동일하게 **마지막에** 화면 클램프 + 사각지대 회수 + 6초 최종 안전망을 돌린다
            // (StickmanAgent.Update()의 마지막 줄과 같은 이유: 어떤 상태가 어떤 이유로 몸을 옮겼든
            // 그 결과를 여기서 되돌린다). 이게 없어서 라이벌만 Dock 아래에 영구히 남았다.
            if (_inDuel) _blackboard.EnforceScreenBoundsAndRescue(dt);

            float maxDuration = _config != null ? _config.rivalMaxDurationSeconds : 30f;
            if (_inDuel && _durationTimer >= maxDuration)
            {
                EndDuel(RivalDuelResult.Draw);
            }
        }

        private void TickCombatExchange(float dt)
        {
            if (_opponent == null || _body == null) return;

            _attackCooldownTimer -= dt;
            if (_attackCooldownTimer > 0f) return;

            float range = _config != null ? _config.rivalAttackRange : 1.0f;
            float dist = Mathf.Abs(_body.position.x - _opponent.Blackboard.Body.position.x);
            if (dist > range) return;

            _attackCooldownTimer = _config != null ? _config.rivalAttackCooldownSeconds : 1.2f;

            float threshold = _blackboard.Config != null ? _blackboard.Config.ragdollForceThreshold : 8f;
            float multiplier = _blackboard.Config != null ? _blackboard.Config.rivalAttackImpactMultiplier : 1.25f;
            float impulse = threshold * multiplier; // 항상 RAGDOLL 전이를 보장하는 확정적 충격량.

            bool rivalStrikes = Random.value < 0.5f; // 근접 시 무작위 선타(50:50) — "정교할 필요 없음".
            if (rivalStrikes)
            {
                TryPlayAttackAnimation();
                _opponent.ReportExternalImpact(impulse); // Suspended 가드를 지키기 위해 공개 메서드 경유.
                _hitsTakenByPlayer++;
            }
            else
            {
                // Minor 2(docs/BUG_REPORT_PHASE3.md) 대응: 라이벌만 Attack 모션에 들어가고 플레이어는
                // 절대 들어가지 않아 "서로 주먹질"이 라이벌 일방 공격처럼 비대칭이었다. 플레이어가
                // 선타를 낼 때도 대칭으로 Attack 상태에 진입시킨다 — 렌더링 레이어가 붙기 전에
                // 상태머신 레벨에서 먼저 맞춰두면 이후 애니메이션 작업이 두 배로 늘지 않는다.
                TryPlayOpponentAttackAnimation();
                RagdollImpactResolver.TryApplyImpact(_blackboard, impulse);
                _hitsTakenByRival++;
            }

            CheckDuelOutcome();
        }

        private void TryPlayAttackAnimation()
        {
            // 자신(라이벌)이 선타를 날릴 때만 자기 Attack 모션을 재생한다 — 맞는 쪽은 곧바로 Ragdoll로
            // 전이하므로 별도 처리가 필요 없다.
            var current = _machine.CurrentStateId;
            if (current == StickmanStateId.Idle || current == StickmanStateId.Walk)
            {
                // Minor 1(docs/BUG_REPORT_PHASE3.md) 대응 — AttackState.Enter()가 읽을 스냅샷을 미리
                // 채운다: 이번 타격 이후 라이벌이 몇 대 더 맞혀야 대결이 끝나는지("한 발 더!" 분기 근거).
                // _hitsTakenByPlayer는 아직 이번 타격이 반영되기 전 값이므로 +1을 감안해 뺀다.
                int hitsToLose = _config != null ? _config.rivalDuelHitsToLose : 2;
                _blackboard.AttackShotsRemaining = Mathf.Max(0, hitsToLose - _hitsTakenByPlayer - 1);
                _machine.ChangeState(StickmanStateId.Attack);
            }
        }

        /// <summary>
        /// Minor 2 대응 — 플레이어가 선타를 낼 때 플레이어 쪽 상태머신에도 Attack을 진입시킨다.
        /// 플레이어 AttackState는 attackDuration(기본 0.4초) 경과 시 스스로 진입 직전 상태(Idle/Walk)로
        /// 복귀하므로(States/AttackState.cs), 여기서 별도의 원복 처리를 할 필요가 없다.
        /// </summary>
        private void TryPlayOpponentAttackAnimation()
        {
            if (_opponent == null) return;
            var opponentMachine = _opponent.Blackboard != null ? _opponent.Blackboard.Machine : null;
            if (opponentMachine == null) return;

            var current = opponentMachine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return;

            // Minor 1과 동일한 스냅샷 원칙 — 이번 타격 이후 플레이어가 몇 대 더 맞혀야 대결이 끝나는지.
            int hitsToLose = _config != null ? _config.rivalDuelHitsToLose : 2;
            _opponent.Blackboard.AttackShotsRemaining = Mathf.Max(0, hitsToLose - _hitsTakenByRival - 1);
            opponentMachine.ChangeState(StickmanStateId.Attack);
        }

        private void CheckDuelOutcome()
        {
            int hitsToLose = _config != null ? _config.rivalDuelHitsToLose : 2;
            if (_hitsTakenByRival >= hitsToLose) { EndDuel(RivalDuelResult.PlayerWon); return; }
            if (_hitsTakenByPlayer >= hitsToLose) { EndDuel(RivalDuelResult.RivalWon); }
        }

        /// <summary>Interaction/RivalEncounterDirector.cs가 트레이 긴급정지 시 강제 종료를 요청할 때 사용.</summary>
        public void ForceEndDuel()
        {
            if (_inDuel) EndDuel(RivalDuelResult.Draw);
        }

        private void EndDuel(RivalDuelResult result)
        {
            _inDuel = false;
            _opponent = null;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false; // 물리 정지 — 다음 스폰까지 완전히 대기 상태로 되돌아간다.
            SetRenderersEnabled(false);
            // 캐릭터가 사라지는데 말풍선만 남으면 안 된다 — 퇴장과 같은 프레임에 지운다.
            _bubble?.HideImmediate();
            // 승패와 무관하게 즉시 퇴장(실제 파일/창은 전혀 변경하지 않음, 원칙 3). 걸어 나가는 연출은
            // Phase 2+ 렌더링 레이어 담당 — 지금은 결과 통지 + 비활성화만 수행한다.
            StickmanEventBus.RaiseRivalDuelEnded(result);
            Debug.Log($"[라이벌] 퇴장 — 결과 {result} (라이벌 피격 {_hitsTakenByRival}회 / 플레이어 피격 {_hitsTakenByPlayer}회).");
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = enabled;
            }
        }
    }
}
