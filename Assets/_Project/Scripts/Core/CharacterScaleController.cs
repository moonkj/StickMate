using UnityEngine;
using StickMate.States;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 캐릭터 배율의 <b>단일 소스</b> — 2026-09-01 설정창 신설 선행 리팩터(docs/UX_FLOW.md 35-1-3 ①).
    ///
    /// ============================================================================
    /// 왜 이 클래스가 생겼는가 — "UI가 둘이 되는 순간 진실도 둘이 된다"
    /// ============================================================================
    /// 어제까지 배율을 바꾸는 UI는 구석 호버 다이얼 하나뿐이었고, 그래서 <b>적용 게이트</b>
    /// (랙돌/스펙터클 중에는 최대 3초 유예 — 34-3-6)가 <c>CornerHoverPanel</c>의 private 메서드
    /// 3개(<c>CanApplyNow</c> / <c>TickPendingScale</c> / <c>ApplyScaleNow</c>)에 들어 있었다.
    /// 설정창에 슬라이더가 생기면서 두 가지가 동시에 무너진다:
    ///
    ///   (1) <b>규칙이 두 벌이 된다</b> — 설정창이 같은 게이트를 다시 구현하면, 훗날 유예 시간을
    ///       바꿀 때 한쪽만 고쳐진다. 그러면 "어디서 바꿨느냐에 따라 반응이 다른 앱"이 된다.
    ///   (2) <b>표시가 어긋난다</b> — 설정창에서 1.20×로 바꾸고 구석 패널을 열면 다이얼이 옛 값을
    ///       가리킨다. "켜진 눈금 = 표시 숫자 = 실제 값"(34-3-4)이 깨지는 순간이고, 그것이 곧
    ///       <b>절대 불변 원칙 1</b> 위반이다.
    ///
    /// 그래서 게이트·대기·강제적용을 통째로 여기로 올리고, 알림은
    /// <see cref="StickmanEventBus.CharacterScaleChanged"/> 하나로 흐른다. 다이얼과 슬라이더는
    /// <b>둘 다 이 이벤트의 구독자이자 발행자</b>다 — 어느 쪽에서 바꾸든 다른 쪽이 같은 프레임에 따라온다.
    ///
    /// ============================================================================
    /// 분업 (바꾸지 않았다 — 옮기기만 했다)
    /// ============================================================================
    /// <code>
    ///   [구석 다이얼]  [설정창 슬라이더]        ← 둘 다 구독자이자 발행자
    ///          \             /
    ///           Request(v, reason)              ← 이 클래스: 스냅 + 기억 + 게이트 + 알림
    ///            |         |
    ///            |         └─ UiLayoutModel.SetCharacterScale(v)   (기억 = 저장 파일)
    ///            └─ StickmanAgent.ApplyCharacterScale(v, reason)   (적용 = 5단계 원자 처리)
    /// </code>
    /// 기억(<see cref="UiLayoutModel"/>)과 적용(<see cref="StickmanAgent.ApplyCharacterScale"/>)은
    /// 예전 그대로다. 이 클래스는 <b>둘을 언제 부르는가</b>만 알고 있다.
    ///
    /// ============================================================================
    /// 게이트는 안전이 아니라 <b>연출</b>이다 (34-3-6, 2026-08-30 실측 결론 그대로 이전)
    /// ============================================================================
    /// 물리적으로는 어떤 상태에서 배율을 바꿔도 안전하다(관절 파단 불가 / 구속 오차 증가 0 /
    /// 랙돌 임계 배율 불변). 몸이 굴러가는 중에 크기가 변하면 <b>그 순간의 인과가 읽히지 않기</b>
    /// 때문에 미룰 뿐이라, <see cref="PendingForceSeconds"/> 뒤에는 상태와 무관하게 넣는다.
    ///
    /// ============================================================================
    /// 왜 정적 클래스인가 / 누가 <see cref="Tick"/>을 부르는가
    /// ============================================================================
    /// <see cref="UiLayoutModel"/>·<see cref="CharacterAppearanceModel"/>과 같은 관례다(값 보관 +
    /// 규칙, 수명주기 없음). 대기 해제는 매 프레임 확인이 필요한데, 이 클래스는 MonoBehaviour가
    /// 아니므로 <b>UI 쪽이 부른다</b>: <c>CornerHoverPanel.Update</c>가 <b>열려 있든 아니든 매 프레임</b>
    /// 부르고(그 컴포넌트는 구석 감지를 위해 항상 돌고 있다), 설정창도 열려 있는 동안 함께 부른다.
    /// 두 번 불려도 결과가 같다(경과 시간 기반이라 멱등). 둘 다 없는 조립(테스트 씬)에서는
    /// <see cref="Request"/> 자체가 다음 호출에서 대기를 정리한다.
    /// </summary>
    public static class CharacterScaleController
    {
        /// <summary>눈금 1칸 = 0.05배. <b>다이얼과 슬라이더가 같은 값에 스냅</b>되어야 "설정창에서 고른
        /// 숫자"와 "다이얼이 가리키는 눈금"이 영원히 같다 — 스냅 격자가 두 벌이면 1.175 같은 값이
        /// 한쪽에서 1.15, 다른 쪽에서 1.20으로 보인다(원칙 1 위반). <c>SizeDialWidget.ValueStep</c>이
        /// 이 상수를 그대로 참조한다.</summary>
        public const float ValueStep = 0.05f;

        /// <summary>적용을 무한정 미루지 않는다 — 이 시간이 지나면 상태와 무관하게 넣는다
        /// (옛 <c>CornerHoverPanel.PendingForceSeconds</c>와 같은 값, 같은 이유).</summary>
        public const float PendingForceSeconds = 3f;

        private static StickmanAgent _agent;

        private static bool _hasValue;
        private static float _value = 0.75f;

        private static bool _hasPending;
        private static float _pendingValue;
        private static float _pendingSince;

        /// <summary>
        /// 지금 <b>화면이 보여줘야 하는</b> 배율. 적용이 유예 중이어도 이 값은 사용자가 고른 값이다 —
        /// 유예는 몸이 늦는 것이지 선택이 취소된 것이 아니다(<see cref="StickmanEventBus.CharacterScaleChanged"/>
        /// 문서 참고).
        /// </summary>
        public static float Value => _hasValue ? _value : ResolveFallbackValue();

        /// <summary>사용자/복원이 값을 한 번이라도 확정했는가. false면 <see cref="Value"/>는 지금
        /// 캐릭터에 구워져 있는 배율(또는 저장 모델의 값)을 되비친다.</summary>
        public static bool HasValue => _hasValue;

        /// <summary>적용이 유예 중인가(랙돌/스펙터클). 다이얼의 "곧 적용" 캡션과 설정창의 같은 캡션이
        /// <b>이 하나</b>를 본다.</summary>
        public static bool HasPendingApply => _hasPending;

        /// <summary>유예 중인 값(<see cref="HasPendingApply"/>가 false면 의미 없다).</summary>
        public static float PendingValue => _pendingValue;

        /// <summary>강제 적용까지 남은 시간(초). 진단/테스트용.</summary>
        public static float PendingSecondsRemaining => _hasPending
            ? Mathf.Max(0f, PendingForceSeconds - (Time.unscaledTime - _pendingSince))
            : 0f;

        /// <summary>이 컨트롤러가 실제로 캐릭터를 만질 수 있는가(진단용 — 배선 누락을 조용히 넘기지 않는다).</summary>
        public static bool HasAgent => _agent != null;

        /// <summary>
        /// 배율을 실제로 넣을 캐릭터를 알려 준다. UI(구석 패널/설정창)가 <c>Start</c>에서 부른다.
        /// 여러 번 불려도 되고, 씬이 다시 로드되면 새 인스턴스로 갈아탄다.
        /// </summary>
        public static void Bind(StickmanAgent agent)
        {
            if (agent == null || ReferenceEquals(_agent, agent)) return;

            // 씬 재로드 = 새 캐릭터. 앞 씬의 유예를 끌고 오면 새 캐릭터가 이유 없이 크기를 바꾼다.
            if (_agent != null && _hasPending) ClearPending();
            _agent = agent;
        }

        /// <summary>값을 <b>0.05 눈금에 스냅</b>하고 안전 구간으로 clamp한다. 다이얼/슬라이더/저장 복원이
        /// 전부 이 하나를 지난다.</summary>
        public static float Snap(float value)
        {
            if (float.IsNaN(value)) return StickConfig.MinCharacterScale;
            float clamped = Mathf.Clamp(value, StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale);
            int steps = Mathf.RoundToInt((clamped - StickConfig.MinCharacterScale) / ValueStep);
            return Mathf.Clamp(StickConfig.MinCharacterScale + steps * ValueStep,
                StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale);
        }

        /// <summary>
        /// 지금 실캐릭터에 넣어도 되는가. <b>안전이 아니라 연출</b> 판정이다(클래스 문서 참고) —
        /// 옛 <c>CornerHoverPanel.CanApplyNow</c>를 상태 목록까지 그대로 옮겨 왔다.
        /// </summary>
        public static bool CanApplyNow
        {
            get
            {
                StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
                if (bb == null || bb.Machine == null) return true;
                switch (bb.Machine.CurrentStateId)
                {
                    case StickmanStateId.Ragdoll:
                    case StickmanStateId.ThrowTumble:
                    case StickmanStateId.Getup:
                    case StickmanStateId.Dragged:
                    case StickmanStateId.RodeoCursor:
                        return false;
                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// ★ <b>모든 UI가 지나는 유일한 문</b>. 값을 스냅해 기억하고(저장 모델), 게이트가 열려 있으면
        /// 그 프레임에 캐릭터까지 넣는다. 닫혀 있으면 유예로 등록하고 <see cref="Tick"/>이 마무리한다.
        ///
        /// <para>어느 경우든 <see cref="StickmanEventBus.CharacterScaleChanged"/>를 <b>반드시</b>
        /// 발행한다 — 그래야 다른 UI가 같은 프레임에 같은 숫자를 가리킨다(원칙 1). 값이 이미 같아도
        /// 발행하는 이유: 한쪽 UI가 옛 값을 그리고 있는 상태에서 다른 쪽이 "같은 값"을 요청하면,
        /// 발행하지 않을 경우 그 어긋남이 영원히 남는다.</para>
        /// </summary>
        /// <returns>실제 캐릭터에 이번 호출로 반영됐으면 true(유예로 등록만 됐으면 false).</returns>
        public static bool Request(float desiredScale, string reason)
        {
            float v = Snap(desiredScale);
            _value = v;
            _hasValue = true;
            UiLayoutModel.SetCharacterScale(v);

            if (CanApplyNow)
            {
                ClearPending();
                bool applied = ApplyNow(v, reason);
                StickmanEventBus.RaiseCharacterScaleChanged(v, reason, appliedToCharacter: true);
                return applied;
            }

            _hasPending = true;
            _pendingValue = v;
            _pendingSince = Time.unscaledTime;
            StickmanEventBus.RaiseCharacterScaleChanged(v, reason, appliedToCharacter: false);
            return false;
        }

        /// <summary>
        /// 유예 해제/강제 적용. 값이 없으면 0비용이다(닫혀 있는 UI가 매 프레임 불러도 된다).
        /// </summary>
        public static void Tick()
        {
            if (!_hasPending) return;

            bool forced = Time.unscaledTime - _pendingSince >= PendingForceSeconds;
            if (!CanApplyNow && !forced) return;

            float v = _pendingValue;
            string reason = forced ? "대기 후 강제 적용" : "대기 해제";
            ClearPending();
            ApplyNow(v, reason);
            StickmanEventBus.RaiseCharacterScaleChanged(v, reason, appliedToCharacter: true);
        }

        /// <summary>
        /// 저장 복원처럼 <b>사용자 조작이 아닌</b> 경로가 값을 확정할 때 쓴다 — 게이트를 거치지 않고
        /// 즉시 넣는다(시작 시점에는 랙돌일 수 없고, 여기서 유예하면 첫 화면이 옛 크기로 뜬다).
        /// 저장 모델은 호출부가 이미 갖고 있는 값이므로 여기서 다시 쓰지 않는다.
        /// </summary>
        public static void AdoptRestored(float value, string reason)
        {
            float v = Snap(value);
            _value = v;
            _hasValue = true;
            ClearPending();
            ApplyNow(v, reason);
            StickmanEventBus.RaiseCharacterScaleChanged(v, reason, appliedToCharacter: true);
        }

        private static bool ApplyNow(float v, string reason)
            => _agent != null && _agent.ApplyCharacterScale(v, reason);

        private static void ClearPending()
        {
            _hasPending = false;
            _pendingValue = 0f;
            _pendingSince = 0f;
        }

        /// <summary>아직 아무도 값을 고르지 않았을 때 화면이 보여줄 값. 저장 모델이 있으면 그것,
        /// 없으면 지금 캐릭터에 구워져 있는 배율이다(둘 다 없으면 배포 기본값 0.75).</summary>
        private static float ResolveFallbackValue()
        {
            if (UiLayoutModel.HasCharacterScale) return Snap(UiLayoutModel.CharacterScale);
            if (_agent != null && _agent.CurrentCharacterScale > 0f) return Snap(_agent.CurrentCharacterScale);
            return Snap(0.75f);
        }

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게 —
        /// UiLayoutModel.ResetForTesting과 같은 관례).</summary>
        public static void ResetForTesting()
        {
            _agent = null;
            _hasValue = false;
            _value = 0.75f;
            ClearPending();
        }
    }
}
