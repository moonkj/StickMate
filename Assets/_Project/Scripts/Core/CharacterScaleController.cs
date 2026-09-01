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
    /// 원래 배율을 바꾸는 UI는 구석 호버 다이얼 하나뿐이었고, 그래서 <b>적용 게이트</b>
    /// (랙돌/스펙터클 중에는 최대 3초 유예 — 34-3-6)가 <c>CornerHoverPanel</c>의 private 메서드
    /// 3개(<c>CanApplyNow</c> / <c>TickPendingScale</c> / <c>ApplyScaleNow</c>)에 들어 있었다.
    /// 설정창에 슬라이더가 생기면서 두 가지가 동시에 무너진다:
    ///
    ///   (1) <b>규칙이 두 벌이 된다</b> — 설정창이 같은 게이트를 다시 구현하면, 훗날 유예 시간을
    ///       바꿀 때 한쪽만 고쳐진다. 그러면 "어디서 바꿨느냐에 따라 반응이 다른 앱"이 된다.
    ///   (2) <b>표시가 어긋난다</b> — 한쪽에서 1.20×로 바꿨는데 다른 쪽이 옛 값을 가리킨다.
    ///       "표시 숫자 = 실제 값"이 깨지는 순간이고, 그것이 곧 <b>절대 불변 원칙 1</b> 위반이다.
    ///
    /// 그래서 게이트·대기·강제적용을 통째로 여기로 올리고, 알림은
    /// <see cref="StickmanEventBus.CharacterScaleChanged"/> 하나로 흐른다. 배율을 만지는 UI는
    /// <b>이 이벤트의 구독자이자 발행자</b>다 — 어느 쪽에서 바꾸든 다른 쪽이 같은 프레임에 따라온다.
    /// (2026-09-01 현재 UI는 설정창 슬라이더 하나뿐이지만, 문이 하나라는 사실이 곧 원칙 1의 보증이라
    /// 구조는 그대로 둔다.)
    ///
    /// ============================================================================
    /// 분업 (바꾸지 않았다 — 옮기기만 했다)
    /// ============================================================================
    /// <code>
    ///   [설정창 슬라이더]  [저장 복원]           ← 구독자이자 발행자
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
    /// 아니므로 <b>바깥이 부른다</b>.
    ///
    /// <para>★ 2026-09-01 구석 호버 패널 삭제(사용자 요청) — <b>상시 구동자가 바뀌었다</b>.
    /// 예전에는 <c>CornerHoverPanel.Update</c>가 열려 있든 아니든 매 프레임 불러 줬다(그 컴포넌트는
    /// 구석 감지를 위해 항상 돌고 있었다). 그 패널이 사라지면서 남은 호출자가 설정창뿐이 되었는데,
    /// 설정창의 <c>Update</c>는 <c>if (!_open) return;</c>으로 시작한다 — 즉 <b>창을 닫으면 유예가
    /// 영영 안 풀린다</b>(랙돌 중에 크기를 바꾸고 창을 닫으면 그 크기가 사라진다). 그래서 상시 구동
    /// 책임을 <see cref="StickMate.Interaction.CharacterProgressionDirector"/>로 옮겼다 — 저장 파일을
    /// 읽는 주인이자 UI와 무관하게 매 프레임 도는 컴포넌트다.</para>
    ///
    /// 두 번 불려도 결과가 같다(경과 시간 기반이라 멱등)라 설정창은 열려 있는 동안 계속 함께 불러도
    /// 된다. 둘 다 없는 조립(테스트 씬)에서는 <see cref="Request"/> 자체가 다음 호출에서 대기를 정리한다.
    /// </summary>
    public static class CharacterScaleController
    {
        /// <summary>눈금 1칸 = 0.05배. 배율을 만지는 <b>모든 경로가 같은 값에 스냅</b>되어야 "설정창에서
        /// 고른 숫자"와 "실제 값"이 영원히 같다 — 스냅 격자가 두 벌이면 1.175 같은 값이 한쪽에서 1.15,
        /// 다른 쪽에서 1.20으로 보인다(원칙 1 위반).</summary>
        public const float ValueStep = 0.05f;

        /// <summary>적용을 무한정 미루지 않는다 — 이 시간이 지나면 상태와 무관하게 넣는다.</summary>
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

        /// <summary>적용이 유예 중인가(랙돌/스펙터클). "곧 적용" 캡션은 <b>이 하나</b>를 본다.</summary>
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
        /// 배율을 실제로 넣을 캐릭터를 알려 준다. 저장 복원 주인(CharacterProgressionDirector)과
        /// 설정창이 <c>Start</c>에서 부른다.
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
        /// 지금 실캐릭터에 넣어도 되는가. <b>안전이 아니라 연출</b> 판정이다(클래스 문서 참고).
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

        /// <summary>
        /// ★ 저장 파일에 적힌 크기를 캐릭터에 되돌린다 — <b>앱 시작에 정확히 한 번</b>.
        ///
        /// <para>2026-09-01 구석 호버 패널 삭제로 <b>이사 온 로직</b>이다. 원래는
        /// <c>CornerHoverPanel.RestoreSavedScale()</c>이 했는데, 그 컴포넌트와 저장 파일을 읽는
        /// 컴포넌트의 <c>Start</c> 실행 순서가 보장되지 않아 <b>매 프레임 재시도 + 2초 유예 마감</b>
        /// 이라는 경주를 안고 있었다(그 경주가 PlayMode 테스트를 두 번 불안정하게 만들었다).
        /// 이제는 <see cref="StickMate.Core.CharacterSaveStore.Load"/> <b>직후</b>에 같은 호출자가
        /// 부르므로 순서가 구조적으로 보장된다 — 유예도 재시도도 필요 없다.</para>
        ///
        /// <para>상한 clamp를 <b>여기서 한 번</b> 하고 그 값을 저장 모델에 되쓴다. 상한이 2.0에서
        /// 1.5로 내려간 적이 있어 <b>1.5를 넘겨 저장해 둔 사용자가 실재한다</b> — 되쓰지 않으면
        /// 화면은 1.50×인데 저장 모델은 2.00×로 남아 "표시와 진실이 둘"이 된다(원칙 1).</para>
        /// </summary>
        /// <returns>저장된 값이 있어 실제로 복원했으면 true(없으면 캐릭터는 배포 기본 배율 그대로).</returns>
        public static bool RestoreFromSaveModel()
        {
            // ★ <b>"컨트롤러에 이미 값이 있으면 그만둔다"로 막으면 안 된다</b>(옛 RestoreSavedScale의
            //   주석이 못박아 둔 함정 — 테스트가 실제로 잡아냈다). 이 클래스는 정적이라 씬을 다시
            //   로드해도 값이 남는데, 새 캐릭터는 배포 기본 배율로 태어난다. 그때 복원을 건너뛰면
            //   숫자만 1.20이고 몸은 0.75인 화면이 된다. "한 번만"은 호출자의 Start()가 보장한다.
            //
            //   막아야 하는 것은 <b>적용 대기 중</b>인 경우뿐이다: 게이트가 미뤄 둔 사용자의 선택을
            //   복원이 게이트 없이 즉시 덮어써서 유예를 조용히 무효화하는 일.
            if (_hasPending) return false;
            if (!UiLayoutModel.HasCharacterScale) return false;

            float saved = UiLayoutModel.CharacterScale;
            float v = Mathf.Clamp(saved, StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale);
            if (!Mathf.Approximately(v, saved))
            {
                UiLayoutModel.SetCharacterScale(v);
                Debug.Log($"[크기] 저장된 크기 {saved:F2}×가 상한을 넘어 {v:F2}×로 낮췄습니다 " +
                    $"(상한 {StickConfig.MaxCharacterScale:F2}×).");
            }

            AdoptRestored(v, "저장된 크기 복원");
            return true;
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
