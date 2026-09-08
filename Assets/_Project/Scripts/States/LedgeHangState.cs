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
    ///                              (AutoWanderController), (b) ★ 2026-09-03 이후 매달린 X는 모서리
    ///                              **바깥**이 아니라 **안쪽**이다(아래 「적당히 끝쪽」 절) — 즉 이
    ///                              축에서는 예전보다 구조적으로 더 안전해졌다, (c) 최종적으로 매 프레임
    ///                              마지막에 도는 StickmanBlackboard.EnforceScreenBoundsAndRescue()의
    ///                              하드 클램프가 상태와 무관하게 화면 안으로 되돌린다.
    ///  · 발이 목적지를 지나치지 않음 : 진입 판정(TryFindDescendTarget)이 "매달린 발보다 아래에 있는
    ///                              발판"만 목적지로 인정한다 — 그래서 손을 놓는 순간 반드시 그 아래로 떨어진다.
    ///
    /// ── ★ 「완전 끝」이 아니라 「적당히 끝쪽」 (사용자 요청 2026-09-02 / 2026-09-03) ─────────────
    /// <para>2026-09-02: <i>"창에서 매달려 떨어질때 창끝보다는 좀 떨어져서 매달림 좀 안쪽에 매달려야하는데
    /// 그리고 맥같은 경우도 창모서리가 타원이라 끝은 비어있는 공간에 매달려있음"</i><br/>
    /// 2026-09-03: <i>"굳이 창끝에서만 매달리다가 떨어질 필요없잖아 완전 끝말고 적당히 끝쪽만 되도 될거 같은데"</i></para>
    ///
    /// <para><b>무엇이 「끝」이었나(실측)</b>: 예전 <c>Tick()</c>은 매달린 루트를
    /// <c>모서리 X + 방향 × ledgeHangEdgeOffset(0.14)</c>로 <b>강제 스냅</b>했다. 그런데 배회 AI는
    /// 이미 모서리에서 <c>StickmanBlackboard.EdgeStopDistanceWorld</c>(배포 배율 0.75에서 <b>0.400유닛</b>)
    /// 안쪽에 캐릭터를 세워 둔 뒤 추첨한다. 즉 <b>코드가 캐릭터를 바깥으로 0.54유닛(≈22pt) 끌어내
    /// 정확히 모서리 점에 매달았다.</b> macOS 창은 모서리가 둥글어(반경 ≈10pt) 그 점에는 그려진 픽셀이
    /// 없다 — 사용자가 본 "빈 공간에 매달림"은 연출이 아니라 이 강제 스냅 하나였다.</para>
    ///
    /// <para><b>지금</b>: 인셋(모서리에서 안쪽으로 들어간 거리)을 <see cref="ResolveEdgeInsetWorld"/>가
    /// <b>Enter에서 한 번</b> 확정하고, 그 뒤로는 창이 움직여도 그 인셋을 유지한다(매 프레임 다시 풀면
    /// 창 크기 변화가 몸을 좌우로 떨게 만든다).</para>
    /// <code>
    /// 하한 = 곡률 여유(ledgeHangCornerClearancePoints, OS pt → 월드) + 손 여유(ledgeHangHandClearanceHeights × 신장)
    /// 상한 = max(하한, EdgeProbeReachWorld)  그리고  발판 폭 × ledgeHangMaxInsetWidthFraction 로 절단
    /// 인셋 = clamp(지금 서 있는 자리의 인셋, 하한, 상한)      ← 「적당히」의 실체. 바깥으로는 절대 안 끌어낸다.
    /// </code>
    /// <para><b>상한이 왜 저것인가</b>: <c>EdgeProbeReachWorld</c>는 배회 AI가 경계 행동을 평가하는
    /// 최대 거리다. 그보다 안쪽에서 매달리는 그림은 <b>AI가 애초에 만들 수 없는 자세</b>이므로 상한으로
    /// 삼기에 정확하다. 발판 폭 비율 절단은 그 위에 얹는 안전판이다 — 좁은 창 + 큰 배율에서도
    /// "창 한가운데에 매달린" 우스운 그림이 <b>산술적으로 불가능</b>해진다.</para>
    ///
    /// <para><b>★ 알려진 이음매 갈라짐(정직하게 남긴다)</b>: 진입 프로브
    /// (<c>StickmanBlackboard.TryFindDescendTarget</c> → <c>GroundSensor.TryFindDescendTarget</c>)는
    /// 여전히 <c>모서리 + ledgeHangEdgeOffset</c>이라는 <b>바깥</b> 점에서 "내려앉을 발판이 있는가"를
    /// 묻는다. 실제 낙하 X는 이제 <b>안쪽</b>이므로 두 점이 배포 형상(배율 0.75, 40.92pt/유닛)에서
    /// 0.140 + 0.549 = 약 <b>0.69유닛(≈28pt)</b> 벌어진다.
    /// 결과는 (i) 예측한 착지 발판과 실제 착지 발판이 드물게 달라질 수 있음(대사 선택만 영향),
    /// (ii) 반대로 프로브가 더 보수적이라 놓치는 매달림이 생길 수 있음 — <b>안전 불변식(무한 매달림 금지 /
    /// 발판 소실 즉시 낙하 / 화면 밖 금지)에는 영향이 없다.</b> 하나로 합치려면
    /// <c>States/StickmanBlackboard.cs</c>(인자)와 <c>States/GroundSensor.cs</c>(<c>Mathf.Max(0f, …)</c> 클램프)를
    /// 함께 고쳐야 하는데 <b>그 두 파일은 이 라운드의 배정 밖</b>이었다. 리더 배정 대상이다.</para>
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
        private float _edgeInset;          // 모서리에서 **안쪽으로** 들어간 거리(월드). Enter에서 한 번만 확정한다.

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

            /// <summary>
            /// ★ 2026-09-08 — 내려갈 발판을 <b>실제로 찾았는가</b>(<c>TryFindDescendTarget</c>의 결과 그대로).
            ///
            /// <para><b>절대 <see cref="DropHeightUnits"/>가 0인지로 대신 판정하지 마라.</b> 발판을 못 찾으면
            /// 낙차가 0으로 떨어지는데, 0은 <b>「가장 얕은 낙차」와 비트 단위로 같은 값</b>이다. 이 필드가
            /// 없던 동안 매핑 함수는 <b>실패한 측정을 「가장 얕은 낙차」로 읽어</b> «여기로 내려가자»를
            /// 말했고, 정작 그 경로의 실제 행동은 <b>아무 데도 안 내려가고 그냥 Fall</b>이었다
            /// (절대 불변 원칙 1 — 행동-텍스트 싱크 위반. design-narrative 전체 대사 검수에서 발견).</para>
            ///
            /// <para>같은 병을 이 저장소가 이미 한 번 앓았고 처방도 같다 —
            /// <see cref="StickMate.Dialogue.GrabReactionLines.GrabParams.HasGrabPoint"/>
            /// (<c>오프셋 0</c> = 「발끝을 정확히 잡았다」 = 「커서를 못 읽었다」). <b>실패한 측정과
            /// 성공한 측정이 똑같이 생겼다</b>는 그 형태다.</para>
            /// </summary>
            public bool HasDescendTarget;
        }

        private readonly LedgeHangDialogueParams _dialogueParams = new LedgeHangDialogueParams();

        public object DialogueParams => _dialogueParams;

        public LedgeHangState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.LedgeHang;

        /// <summary>"어우... 아찔하네"가 나오는 낙차(신장 배수 H). 그 아래는 "여기로 내려가자".
        /// ★ 판단값이지 실측이 아니다 — UX_FLOW.md 31-2 #6 / MOTION_SPEC 2-7. 거리이므로 반드시
        /// H 배수다(31-4 C1 축 ①).
        /// <para>★ 2026-09-01 개정(UX_FLOW.md 31-2 #6 신규 등재 / MOTION_SPEC 1절 표 #3) — 임계값을
        /// <b>절대 월드 유닛에서 신장 배수(H)로</b> 옮겼다. 구 임계값 3.0유닛은 배율 1.0에서 1.32H이고,
        /// 이 상태가 성립하는 최소 낙차 자체가 1.10H라 "어우... 아찔하네"가 나오려면 낙차가 최소치의
        /// 1.2배를 넘어야 했다. 신장 배수로 적어 배율 슬라이더와 플랫폼(작업표시줄 높이)에 불변이 되게 한다.
        /// <b>1.6이라는 계수는 design-motion의 판단값이지 실측이 아니다</b> — 실기에서 창-창 사이 낙차
        /// 분포를 본 뒤 조정될 수 있다.</para>
        /// <para><c>internal</c>인 이유: 회귀 테스트가 이 임계값을 <b>숫자로 베끼지 않고 참조</b>해야
        /// 한다(CLAUDE.md — 하드코딩 잔존으로 4건이 깨진 2026-09-01 사고 이후 확정).
        /// <c>AssemblyInfo.cs</c>의 <c>InternalsVisibleTo</c>가 EditMode 어셈블리에만 열려 있다.</para></summary>
        internal const float DeepDescentHeights = 1.6f;

        /// <summary>
        /// 텍스트 매핑 함수(<b>순수</b>) — 이 상태의 대사는 전부 여기서만 나온다.
        /// <c>Enter()</c> 안의 익명 람다였던 것을 <b>static으로 끌어낸 이유</b>: 아래 게이트를 회귀
        /// 테스트가 직접 잴 수 있어야 하기 때문이다(<c>Dialogue/GrabReactionLines.Resolve</c>와 같은 자리).
        /// 순수 함수라 시간·난수·전역 상태를 읽지 않는다 — "이 텍스트가 어느 <c>Enter()</c>의 어느
        /// 스냅샷에서 나왔는지"가 그대로 역추적된다(UX_FLOW.md 31-3).
        ///
        /// <para><b>★ 게이트 순서가 이 함수의 전부다.</b> 낙차를 비교하기 <b>전에</b> 「애초에 내려갈
        /// 곳이 있는가」를 먼저 묻는다. 발판을 못 찾은 경로는
        /// <see cref="LedgeHangDialogueParams.DropHeightUnits"/>가 0이고 0은 가장 얕은 낙차와 같은 값이라,
        /// 낙차만 보면 «여기로 내려가자»가 나온다 — 그런데 그 경로의 실제 행동은 <b>어디로도 안 내려가고
        /// 그냥 Fall</b>이다. 낙차 비교 자체는 한 글자도 바뀌지 않았다. 그 앞에 게이트 하나가 붙었을 뿐이다.</para>
        ///
        /// <para><b>왜 새 문안이 아니라 침묵인가</b>: 발판이 없다는 사실에 맞는 새 대사를 코더가 이 자리에서
        /// 지어내면 그건 <c>design-narrative</c> 소관을 대신 정하는 것이다. 그리고 이 저장소에서
        /// <b>안전한 실패는 침묵</b>이다(<c>Dialogue/DialogueKind.cs</c> — "침묵은 거짓말이 아니다").
        /// 빈 텍스트 = 말하지 않는다이고, 호출부가 그 경우 <see cref="DialogueIntent"/>를 아예 만들지 않는다
        /// (<c>States/TimedSpectacleState.cs</c>가 쓰는 것과 같은 관례).</para>
        ///
        /// <para><paramref name="stateId"/>는 계약상 항상 <see cref="StickmanStateId.LedgeHang"/>다
        /// (이 함수를 쓰는 상태가 하나뿐이다) — 분기하지 않는다.</para>
        /// </summary>
        public static DialogueLine ResolveDialogue(StickmanStateId stateId, object dialogueParams)
        {
            var p = dialogueParams as LedgeHangDialogueParams;

            // ★ 게이트 — 내려갈 발판이 없으면(또는 파라미터 자체가 없어 아무것도 모르면) 침묵한다.
            //   여기를 지우면 실패 경로가 다시 "가장 얕은 낙차"로 위장한다.
            if (p == null || !p.HasDescendTarget) return DialogueLine.Say(string.Empty);

            float h = p.CharacterHeightWorld > 0.0001f
                ? p.CharacterHeightWorld
                : StickConfig.BaselineCharacterTotalHeight;
            return p.DropHeightUnits < DeepDescentHeights * h
                ? DialogueLine.Say("여기로 내려가자")
                : DialogueLine.Say("어우... 아찔하네");
        }

        /// <summary>매핑 함수의 <b>캐시된</b> 델리게이트. 메서드 그룹을 호출부에서 그때그때 변환하면
        /// 전이마다 델리게이트가 새로 할당된다(하루 종일 켜져 있는 앱 — 무할당 경로를 유지한다.
        /// 종전 비캡처 람다도 Roslyn이 같은 방식으로 한 번만 만들어 캐시하고 있었다).</summary>
        private static readonly System.Func<StickmanStateId, object, DialogueLine> DialogueMapper = ResolveDialogue;

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

            // ★ 「적당히 끝쪽」 — 매달릴 X를 여기서 한 번 확정한다(클래스 문서의 그 절).
            //   지금 서 있는 자리의 인셋을 기준으로 하되, 곡률+손 여유 아래로는 못 내려가고
            //   AI가 만들 수 있는 최대 거리(그리고 발판 폭 비율) 위로는 못 올라간다.
            //   ★ _startWorldPos는 바로 위에서 잡은 "서 있던 자리"다 — Body가 없으면 하한이 그대로 쓰인다.
            float standingInset = _blackboard.Body != null
                ? _direction * (_ledgeEdgeWorldX - _startWorldPos.x)
                : float.NaN;
            _edgeInset = ResolveEdgeInsetWorld(
                ResolveCornerClearanceWorld(),
                ResolveHandClearanceWorld(),
                standingInset,
                _blackboard.EdgeProbeReachWorld,
                ResolveLedgeWidthWorld(),
                _blackboard.Config != null ? _blackboard.Config.ledgeHangMaxInsetWidthFraction : 0.25f);

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
            // ★ 위 0유닛은 "가장 얕은 낙차"와 구분되지 않는다 — 실패 사실을 별도 축으로 함께 싣는다.
            _dialogueParams.HasDescendTarget = _hasDescendTarget;

            // 종류 = Narrative(진행 서술: "여기로 내려가자" = 지금 내려가는 중이라는 서술).
            // 계획 잔여 체류 = 잡기 보간 + 매달림 유지시간(둘 다 Enter에서 확정).
            // 문장 자체를 고르는 규칙은 ResolveDialogue 하나뿐이다 — 여기서는 "언제/얼마나"만 정한다.
            float grabDuration = _blackboard.Config != null ? _blackboard.Config.ledgeHangGrabDuration : 0.28f;

            // ★ 2026-09-08 — 침묵 판정은 ResolveDialogue **한 곳**에만 둔다. 여기에 조건을 다시 적으면
            //   두 벌이 되고, 두 벌은 반드시 갈라진다(그때 조용히 틀린 대사가 나간다). 순수 함수라
            //   미리 한 번 물어도 부작용이 없고, 토큰을 소비하는 실제 발급은 아래 TryCreate 한 번뿐이다.
            if (!string.IsNullOrEmpty(ResolveDialogue(StateId, _dialogueParams).Text))
            {
                _ = DialogueIntent.TryCreate(context, DialogueMapper, grabDuration + _holdDuration);
            }
            else
            {
                // 화면을 볼 수 없는 검증 환경에서 "왜 이 매달림은 말이 없었나"가 로그만으로 재구성돼야
                // 한다 — 침묵이 계약의 결과인지 버그인지 구분되지 않으면 이 게이트는 검증 불가능한
                // 규칙이 된다(DialogueIntent.TryCreate의 "발화 보류" 로그와 정확히 같은 이유).
                Debug.Log($"[말풍선] 발화 보류 ({StateId}) — 내려갈 발판을 찾지 못했습니다(낙차 0유닛). " +
                    "0은 「가장 얕은 낙차」와 같은 값이라 그대로 대사를 고르면 \"여기로 내려가자\"가 나오는데, " +
                    "이 경로는 실제로는 아무 데도 안 내려가고 그냥 Fall합니다 — 침묵은 거짓말이 아니다(원칙 1).");
            }

            Debug.Log($"[매달리기] 진입 — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                $"모서리핸들={_ledgeHandle}, 모서리(X={_ledgeEdgeWorldX:F3}, Y={_ledgeTopWorldY:F3}), " +
                $"손끝~발끝={_dropDepth:F3}유닛, 매달릴시간={_holdDuration:F2}초, " +
                $"인셋={_edgeInset:F3}유닛(서 있던 자리 {(_blackboard.Body != null ? (_direction * (_ledgeEdgeWorldX - _startWorldPos.x)) : 0f):F3}, " +
                $"하한 {(ResolveCornerClearanceWorld() + ResolveHandClearanceWorld()):F3} = 곡률 {ResolveCornerClearanceWorld():F3} + 손 {ResolveHandClearanceWorld():F3}), " +
                $"내려갈발판={(_hasDescendTarget ? $"핸들 {_descendTargetHandle}(Y={_descendTargetTopWorldY:F3})" : "없음")}.");
        }

        // ============================================================================
        // ★ 「적당히 끝쪽」 인셋 유도 — 순수 함수 하나 + 입력 조립 셋
        // ============================================================================

        /// <summary>
        /// 매달릴 X의 인셋(모서리에서 안쪽으로 들어간 거리, 월드 유닛)을 정한다. <b>순수 함수</b>다 —
        /// 씬도 설정도 카메라도 보지 않으므로 테스트가 상한/하한 계약을 직접 잰다(같은 판정을 테스트가
        /// 다시 적으면 어긋난다는 이 저장소의 관례).
        ///
        /// <para>하한은 "손끝이 실제로 그려진 창 픽셀 위에 있는가"이고(곡률 여유 + 손 여유),
        /// 상한은 "그래도 창 한가운데는 아니다"다. 그 사이에서는 <b>지금 서 있는 자리를 그대로 쓴다</b> —
        /// 사용자가 요구한 "완전 끝말고 적당히 끝쪽"이 이 한 줄이다. 바깥으로 끌어내는 경로는 없다.</para>
        /// </summary>
        /// <param name="cornerClearanceWorld">창 모서리 곡률 몫(월드 유닛). 음수/NaN은 0으로 본다.</param>
        /// <param name="handClearanceWorld">바깥 손끝의 좌우 진폭 몫(월드 유닛). 음수/NaN은 0으로 본다.</param>
        /// <param name="standingInsetWorld">지금 서 있는 자리의 인셋. NaN이면 하한을 쓴다.</param>
        /// <param name="probeReachWorld">배회 AI가 경계 행동을 평가하는 최대 거리(= 상한 후보).</param>
        /// <param name="footholdWidthWorld">붙잡은 발판의 폭. 0 이하/NaN이면 폭 절단을 건너뛴다.</param>
        /// <param name="maxInsetWidthFraction">폭 절단 비율. 내부에서 [0, 0.5]로 자른다.</param>
        public static float ResolveEdgeInsetWorld(float cornerClearanceWorld, float handClearanceWorld,
            float standingInsetWorld, float probeReachWorld, float footholdWidthWorld, float maxInsetWidthFraction)
        {
            float min = Sane(cornerClearanceWorld) + Sane(handClearanceWorld);
            float max = Mathf.Max(min, Sane(probeReachWorld));

            // 폭 절단 — "창 한가운데에 매달린" 그림을 산술적으로 불가능하게 만드는 안전판.
            // 0.5를 넘겨 받으면 0.5로 자른다(0.5 = 정확히 한가운데. 그 너머는 정의상 반대쪽 끝이다).
            if (!float.IsNaN(footholdWidthWorld) && footholdWidthWorld > 0f && maxInsetWidthFraction > 0f)
            {
                float cap = footholdWidthWorld * Mathf.Min(maxInsetWidthFraction, 0.5f);
                max = Mathf.Min(max, cap);
                min = Mathf.Min(min, cap); // 좁은 창에서 min > max로 뒤집히지 않게(Clamp가 조용히 뒤집힌다).
            }

            float standing = float.IsNaN(standingInsetWorld) ? min : standingInsetWorld;
            return Mathf.Clamp(standing, min, max);
        }

        private static float Sane(float v) => float.IsNaN(v) || v < 0f ? 0f : v;

        /// <summary>창 모서리 곡률 몫 — <b>OS 포인트 고정량</b>이라 런타임 환산을 거친다.
        /// 상수 환산(예: DockGeometry.ReferenceWorldUnitsPerPoint)을 쓰면 디스플레이가 바뀔 때 조용히 틀린다.</summary>
        private float ResolveCornerClearanceWorld()
        {
            float points = _blackboard.Config != null ? _blackboard.Config.ledgeHangCornerClearancePoints : 12f;
            if (points <= 0f) return 0f;
            float pointsPerUnit = GroundSensor.ComputeOsPointsPerWorldUnit(_blackboard.MainCamera, _blackboard.Config);
            if (float.IsNaN(pointsPerUnit) || pointsPerUnit <= 0f)
                pointsPerUnit = StickConfig.ReferencePointsPerWorldUnitApprox;
            return points / pointsPerUnit;
        }

        /// <summary>바깥 손끝의 좌우 진폭 몫 — <b>신장 배수</b>라 캐릭터가 커지면 함께 커진다.</summary>
        private float ResolveHandClearanceWorld()
        {
            float heights = _blackboard.Config != null ? _blackboard.Config.ledgeHangHandClearanceHeights : 0.15f;
            if (heights <= 0f) return 0f;
            return heights * _blackboard.CharacterHeightWorld;
        }

        /// <summary>붙잡은 발판의 폭(월드 유닛). 양쪽 모서리를 같은 창구로 물어 만든다.
        /// 조회에 실패하면 0을 돌려 폭 절단을 건너뛰게 한다(폭을 모르면 자르지 않는 쪽이 안전하다).</summary>
        private float ResolveLedgeWidthWorld()
        {
            if (!_hasLedge) return 0f;
            if (!_blackboard.TryGetFootholdEdgeWorld(_ledgeHandle, 1, out _, out float rightX)) return 0f;
            if (!_blackboard.TryGetFootholdEdgeWorld(_ledgeHandle, -1, out _, out float leftX)) return 0f;
            return Mathf.Abs(rightX - leftX);
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

            // 매달린 목표 좌표 — 손끝이 모서리에 정확히 닿는 높이(모서리 Y − 손끝~발끝 거리)에 몸을 두고,
            // X는 모서리에서 **안쪽으로** _edgeInset만큼 들어간다(손을 놓으면 그 X로 떨어진다).
            // ★ 인셋은 Enter에서 한 번 확정된 값이다 — 매 프레임 다시 풀면 창 크기 변화가 몸을 좌우로 떨게 한다.
            //   그래도 _ledgeEdgeWorldX는 매 프레임 갱신되므로 창이 옆으로 움직이면 몸이 그대로 따라간다.
            var hangPos = new Vector2(_ledgeEdgeWorldX - _direction * _edgeInset, _ledgeTopWorldY - _dropDepth);

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
