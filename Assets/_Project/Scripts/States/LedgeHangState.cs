using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 발판 **모서리를 붙잡고 매달렸다가 손을 놓아 내려가기**
    /// (docs/UX_FLOW.md 4절 "매달리기(HANG)", 사용자 명시 요청 2026-08-28 "내려갈때도 매달려서 내려가는형태로").
    ///
    /// ParkourClimbState가 "아래 -> 위"라면 이 상태는 "위 -> 아래"다. 왜 그 상태를 확장하지 않고 새로
    /// 만들었는지는 <see cref="StickmanStateId.LedgeHang"/>의 문서에 적어뒀다(요약: 페이즈 수도 종료
    /// 상태도 다르고, 실제로 공유할 코드는 이미 GroundSensor의 정적 유틸이라 상태를 합칠 이유가 없다).
    ///
    /// 진입: WalkState가 <see cref="StickmanBlackboard.LedgeHangPressed"/> 펄스를 소비할 때
    ///       (그 펄스는 AutoWanderController가 발판 경계에서 StickConfig.ledgeHangChance 추첨으로 발생시킨다).
    /// 전이: **정상/비정상 모두 항상 Fall**. 세 갈래뿐이다 —
    ///       (1) 매달림 유지시간 만료 -> 손을 놓음 -> Fall
    ///       (2) 붙잡은 발판이 사라짐(창 이동/닫힘) -> **같은 프레임 즉시** Fall
    ///       (3) 절대 상한(StickConfig.ledgeHangMaxDuration) 초과 -> 무조건 Fall (무한 매달림 금지)
    ///       외력 임계값 초과 시 Ragdoll 인터럽트는 다른 능동 상태와 동일하게
    ///       StickmanAgent.ReportExternalImpact가 상태와 무관하게 처리한다.
    ///
    /// ── 안전 규칙(전부 설정이 아니라 코드의 불변식이다) ───────────────────────────────────────
    ///  · 발판 소실 시 즉시 낙하  : 매 Tick 첫머리에서 붙잡은 핸들을 재확인한다(ParkourClimbState와 동일 계약).
    ///  · 무한 매달림 금지        : 페이즈 타이머와 **독립적인** 절대 상한 타이머를 따로 돌린다.
    ///  · 화면 밖 금지            : (a) 진입 자체가 "화면 자체의 끝이 아닌 경계"로 제한되고
    ///                              (AutoWanderController), (b) 매달린 X는 모서리에서 ledgeHangEdgeOffset
    ///                              만큼만 벗어나며, (c) 최종적으로 매 프레임 마지막에 도는
    ///                              StickmanBlackboard.EnforceScreenBoundsAndRescue()의 하드 클램프가
    ///                              상태와 무관하게 화면 안으로 되돌린다.
    ///  · 발이 목적지를 지나치지 않음 : 진입 판정(TryFindDescendTarget)이 "매달린 발보다 아래에 있는
    ///                              발판"만 목적지로 인정한다 — 그래서 손을 놓는 순간 반드시 그 아래로 떨어진다.
    /// </summary>
    public sealed class LedgeHangState : IStickmanState, IHasDialogueParams
    {
        /// <summary>진입 시점의 서 있던 자세에서 매달린 자세로 옮겨가는 잡기 페이즈 / 매달려 있는 페이즈.</summary>
        private enum Phase
        {
            Grabbing,
            Hanging,
        }

        private readonly StickmanBlackboard _blackboard;

        private Phase _phase;
        private int _direction;
        private bool _hasLedge;
        private long _ledgeHandle;
        private float _ledgeTopWorldY;
        private float _ledgeEdgeWorldX;
        private float _dropDepth;          // 손끝~발끝 거리(= 매달린 루트가 모서리보다 얼마나 아래인가)
        private Vector2 _startWorldPos;    // 잡기 보간의 시작점(= 서 있던 자리)
        private float _phaseTimer;
        private float _totalTimer;         // 페이즈와 무관한 절대 상한 타이머(무한 매달림 금지)
        private float _holdDuration;

        // 진단/대사 파생용 — 진입 시점에 예상한 착지 지점(실제 착지는 언제나 FallState가 확정한다).
        private bool _hasDescendTarget;
        private long _descendTargetHandle;
        private float _descendTargetTopWorldY;

        /// <summary>매달림 유지시간을 [min,max]에서 뽑는 난수. 개체별 독립 시드가 필요한 값이 아니라
        /// (AutoWanderController의 배회 통계와 달리 관찰 가능한 패턴을 만들지 않는다) 공용 Random을 쓴다.</summary>
        private static readonly System.Random Rng = new System.Random();

        /// <summary>
        /// BUG-M7 파라미터 파이프라인(UX_FLOW.md 31-2 <b>#6</b>) — 매달린 곳에서 내려갈 높이(월드 유닛)와
        /// <b>그 프레임의 신장 H</b>.
        ///
        /// ★ 2026-09-01 — H가 함께 실린 이유(교차 레이어 로그 L6): 개정 임계값이 <c>1.6 × H</c>라
        /// 매핑 함수가 신장을 알아야 하는데, 매핑 함수가 설정/에이전트를 직접 읽으면 31-1(하나의
        /// Enter, 하나의 스냅샷)이 깨진다.
        /// </summary>
        public sealed class LedgeHangDialogueParams
        {
            public float DropHeightUnits;
            /// <summary>이 전이 프레임의 캐릭터 실측 신장(월드 유닛).</summary>
            public float CharacterHeightWorld;
        }

        private readonly LedgeHangDialogueParams _dialogueParams = new LedgeHangDialogueParams();

        public object DialogueParams => _dialogueParams;

        public LedgeHangState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.LedgeHang;

        /// <summary>"어우... 꽤 깊네"가 나오는 낙차(신장 배수 H). 그 아래는 "여기로 내려가자".
        /// ★ 판단값이지 실측이 아니다 — UX_FLOW.md 31-2 #6 / MOTION_SPEC 2-7. 거리이므로 반드시
        /// H 배수다(31-4 C1 축 ①).</summary>
        private const float DeepDescentHeights = 1.6f;

        public void Enter(StateTransitionContext context)
        {
            _phase = Phase.Grabbing;
            _phaseTimer = 0f;
            _totalTimer = 0f;
            _direction = _blackboard.MoveInputX >= 0f ? 1 : -1;
            _dropDepth = _blackboard.LedgeHangDropDepth;
            _startWorldPos = _blackboard.Body != null ? _blackboard.Body.position : Vector2.zero;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            _hasLedge = info.Grounded;
            _ledgeHandle = info.GroundedFootholdHandle;
            _ledgeTopWorldY = info.GroundWorldY;
            _ledgeEdgeWorldX = _direction > 0 ? info.CurrentFootholdRightWorldX : info.CurrentFootholdLeftWorldX;
            _hasDescendTarget = _blackboard.TryFindDescendTarget(info, _direction,
                out _descendTargetHandle, out _descendTargetTopWorldY);

            float holdMin = _blackboard.Config != null ? _blackboard.Config.ledgeHangHoldDurationMin : 0.84f;
            float holdMax = _blackboard.Config != null ? _blackboard.Config.ledgeHangHoldDurationMax : 1.5f;
            _holdDuration = holdMax > holdMin ? holdMin + (float)Rng.NextDouble() * (holdMax - holdMin) : holdMin;

            if (_blackboard.Body != null)
            {
                // 매달리기 도입부: 잔여 속도를 죽여 모서리에 붙은 듯 고정한다(ParkourClimbState와 동일).
                _blackboard.Body.linearVelocity = Vector2.zero;
            }

            // 이제 발판을 "딛고" 있지 않다 — 붙잡고 있을 뿐이다. 고착 핸들을 해제해두면 이후 접지 판정이
            // 이 발판을 다시 딛은 것으로 오인하지 않고, 손을 놓은 뒤 FallState가 새 발판을 정상 획득한다.
            _blackboard.CurrentFootholdHandle = 0L;
            _blackboard.ReportFootholdChangeIfNeeded("매달리기 시작");

            _blackboard.GetPoseAnimator()?.ResetHangPhase();

            _dialogueParams.DropHeightUnits = _hasDescendTarget
                ? Mathf.Max(0f, (_ledgeTopWorldY - _dropDepth) - _descendTargetTopWorldY)
                : 0f;
            _dialogueParams.CharacterHeightWorld = _blackboard.CharacterHeightWorld;

            // ★ 2026-09-01 개정(UX_FLOW.md 31-2 #6 신규 등재 / MOTION_SPEC 1절 표 #3) — 임계값을
            //   **절대 월드 유닛에서 신장 배수(H)로** 옮긴다. 구 임계값 3.0유닛은 배율 1.0에서
            //   1.32H이고, 이 상태가 성립하는 최소 낙차 자체가 1.10H(아래 진입 임계값 주석 참고)라
            //   "어우... 꽤 깊네"가 나오려면 낙차가 최소치의 1.2배를 넘어야 했다. 신장 배수로 적어
            //   배율 슬라이더와 플랫폼(작업표시줄 높이)에 불변이 되게 한다.
            //
            //   ★ 1.6이라는 계수는 design-motion의 **판단값이지 실측이 아니다**. 실기에서 창-창
            //     사이 낙차 분포를 본 뒤 조정될 수 있다.
            //
            //   종류 = Narrative(진행 서술: "여기로 내려가자" = 지금 내려가는 중이라는 서술).
            //   계획 잔여 체류 = 잡기 보간 + 매달림 유지시간(둘 다 Enter에서 확정).
            float grabDuration = _blackboard.Config != null ? _blackboard.Config.ledgeHangGrabDuration : 0.28f;
            _ = DialogueIntent.TryCreate(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as LedgeHangDialogueParams;
                float drop = p != null ? p.DropHeightUnits : 0f;
                float h = p != null && p.CharacterHeightWorld > 0.0001f
                    ? p.CharacterHeightWorld
                    : StickConfig.BaselineCharacterTotalHeight;
                return drop < DeepDescentHeights * h
                    ? DialogueLine.Say("여기로 내려가자")
                    : DialogueLine.Say("어우... 꽤 깊네");
            }, grabDuration + _holdDuration);

            Debug.Log($"[매달리기] 진입 — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                $"모서리핸들={_ledgeHandle}, 모서리(X={_ledgeEdgeWorldX:F3}, Y={_ledgeTopWorldY:F3}), " +
                $"손끝~발끝={_dropDepth:F3}유닛, 매달릴시간={_holdDuration:F2}초, " +
                $"내려갈발판={(_hasDescendTarget ? $"핸들 {_descendTargetHandle}(Y={_descendTargetTopWorldY:F3})" : "없음")}.");
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard.Body == null)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            // ── 안전 규칙 1: 무한 매달림 금지(절대 상한). 페이즈 타이머와 별개로 항상 먼저 확인한다 —
            // 어떤 페이즈에 있든, 어떤 이유로 페이즈 타이머가 진행되지 않든 이 상한만은 반드시 걸린다.
            _totalTimer += deltaTime;
            float maxDuration = _blackboard.Config != null ? _blackboard.Config.ledgeHangMaxDuration : 3f;
            if (_totalTimer >= maxDuration)
            {
                Debug.Log($"[매달리기] 타임아웃 — {maxDuration:F2}초 상한에 걸려 손을 놓습니다(무한 매달림 금지).");
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            // ── 안전 규칙 2: 붙잡은 발판이 여전히 존재하는지 매 프레임 재확인(ParkourClimbState와 동일
            // 계약). 창이 옆으로 움직였으면 모서리 좌표도 함께 갱신되므로, 매달린 몸이 창을 따라간다.
            if (!_hasLedge || !_blackboard.TryGetFootholdEdgeWorld(_ledgeHandle, _direction,
                    out _ledgeTopWorldY, out _ledgeEdgeWorldX))
            {
                // 잡을 곳이 사라짐(창 이동/닫힘) -> 즉시 Fall. 이 상태가 만든 대사는 TransitionGeneration
                // 불일치로 같은 프레임에 자동 취소된다(UX_FLOW.md 5절 계약).
                Debug.Log($"[매달리기] 붙잡은 발판(핸들 {_ledgeHandle})이 사라져 즉시 낙하합니다.");
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            // 매달린 목표 좌표 — 손끝이 모서리에 정확히 닿는 높이(모서리 Y − 손끝~발끝 거리)에
            // 몸을 두고, X는 모서리 바깥으로 ledgeHangEdgeOffset만큼 나간다(손을 놓으면 그 X로 떨어진다).
            float edgeOffset = _blackboard.Config != null ? _blackboard.Config.ledgeHangEdgeOffset : 0.14f;
            var hangPos = new Vector2(_ledgeEdgeWorldX + _direction * edgeOffset, _ledgeTopWorldY - _dropDepth);

            if (_phase == Phase.Grabbing)
            {
                float grabDuration = _blackboard.Config != null ? _blackboard.Config.ledgeHangGrabDuration : 0.28f;
                _phaseTimer += deltaTime;
                float t = grabDuration > 0f ? Mathf.Clamp01(_phaseTimer / grabDuration) : 1f;
                // SmoothStep — 몸을 낮추는 동작은 시작/끝에서 속도가 0이어야 "붙잡는" 느낌이 난다
                // (등속 Lerp는 툭 떨어졌다가 툭 멈추는 것처럼 보인다).
                // MoveBodyToWorld: Rigidbody2D.position만 쓰면 그 프레임의 Transform이 낡은 좌표로
                // 남는다(autoSyncTransforms 꺼짐). 붙잡는 0.28초 보간은 프레임당 이동량이 작지만
                // 아래 Hanging과 같은 창구를 쓰게 통일한다.
                _blackboard.MoveBodyToWorld(Vector2.Lerp(_startWorldPos, hangPos, t * t * (3f - 2f * t)));

                if (t >= 1f)
                {
                    _phase = Phase.Hanging;
                    _phaseTimer = 0f;
                    Debug.Log($"[매달리기] 모서리를 붙잡았습니다 — 매달린 몸 Y={hangPos.y:F3} " +
                        $"(모서리 Y={_ledgeTopWorldY:F3}), {_holdDuration:F2}초 뒤 손을 놓습니다.");
                }
            }
            else
            {
                // 매달린 채 유지 — 붙잡은 창이 움직이면 위 hangPos가 그만큼 갱신되므로, 이 대입은
                // 사실상 "창 이동량만큼의 순간이동"이다. 창을 빠르게 드래그하면 한 프레임 이동량이
                // 착지 스냅만큼 커질 수 있어 반드시 Transform까지 함께 써야 한다.
                _blackboard.MoveBodyToWorld(hangPos);
                _phaseTimer += deltaTime;
                if (_phaseTimer >= _holdDuration)
                {
                    Debug.Log($"[매달리기] 손을 놓습니다 — 매달린 시간 {_phaseTimer:F2}초, " +
                        $"낙하 시작 Y={hangPos.y:F3}" +
                        $"{(_hasDescendTarget ? $", 예상 착지 Y={_descendTargetTopWorldY:F3}(핸들 {_descendTargetHandle})" : "")}.");
                    _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                    return;
                }
            }

            // BUG-P2-M1과 같은 이유(ParkourClimbState.Tick 참고): Body는 여전히 일반 Dynamic
            // Rigidbody2D라 위치를 매 프레임 덮어써도 중력이 linearVelocity에 조용히 누적된다. 그대로
            // 두면 손을 놓는 순간 이미 몇 유닛/초로 가속된 채 낙하가 시작돼 매달림이 "떨어뜨림"처럼
            // 보이고 착지 충격 계산도 어긋난다. 매 프레임 속도를 재확정한다.
            _blackboard.Body.linearVelocity = Vector2.zero;
        }

        public void Exit() { }
    }
}
