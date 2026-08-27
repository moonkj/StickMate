using System;
using UnityEngine;

namespace StickMate.Interaction
{
    /// <summary>
    /// 부분적 클릭관통 해제(docs/UX_FLOW.md 15절)의 Unity 게임 오브젝트 레벨 절반 — 캐릭터의
    /// Collider2D 위에서 마우스 다운/업을 감지한다.
    ///
    /// Unity의 OnMouseDown/OnMouseUp은 Camera.main 기준 물리 히트테스트(2D 콜라이더 포함)를 엔진이
    /// 매 프레임 자체적으로 수행하므로, 이 컴포넌트 자체가 "동적 히트박스 추적"(15절 제약 1)을 사실상
    /// 공짜로 만족시킨다 — 캐릭터가 움직이면 콜라이더도 함께 움직이고, Unity가 매 프레임 그 최신
    /// 위치로 히트테스트하기 때문이다. 별도의 폴링/캐싱 코드가 필요 없다.
    ///
    /// [핵심 한계 — Platform/ILocalClickCaptureService.cs와 동일한 한계를 게임 오브젝트 레벨에서 재확인]
    /// 이 컴포넌트가 보장하는 것은 "캐릭터를 클릭하면 Unity가 그 사실을 안다"까지다. "그 외 영역은
    /// 항상 100% 관통된다"는 보장은 이 컴포넌트의 책임이 아니다 — 그건 OS 레벨에서 캐릭터 창(오버레이)
    /// 자체가 클릭관통 상태여야 성립하는 별개의 계약이고, 그 오버레이가 아직 진짜로 존재하지 않는다
    /// (BUG-B1, docs/BUG_REPORT_PHASE0.md). 지금은 이렇게 두 절반으로 나뉜다:
    ///   (1) "캐릭터 위 클릭 감지" = 이 컴포넌트가 완성(진짜 OS 오버레이가 생기더라도 그대로 재사용 가능).
    ///   (2) "그 외 영역 100% 관통 보장" = 진짜 분리 오버레이 구현 이후 과제(Tasklist.md 교차 레이어 로그 참고).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class StickmanClickHitbox : MonoBehaviour
    {
        /// <summary>이 오브젝트의 콜라이더 위에서 마우스 버튼이 눌린 프레임에 발생.</summary>
        public event Action MouseDown;

        /// <summary>마우스 버튼이 떼졌을 때(Unity 표준 동작상 다운을 시작한 콜라이더 기준으로 항상 발생)
        /// 발생 — 반드시 같은 콜라이더 위에서 뗄 필요는 없다(드래그 도중 캐릭터가 커서를 벗어나도 정상 수신).</summary>
        public event Action MouseUp;

        private void OnMouseDown() => MouseDown?.Invoke();
        private void OnMouseUp() => MouseUp?.Invoke();
    }
}
