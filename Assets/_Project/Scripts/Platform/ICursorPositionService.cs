using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 전역 커서(마우스) 좌표를 조회하는 경로. IPlatformWindowService와 의도적으로 분리된 별도
    /// 인터페이스다.
    ///
    /// 왜 분리했는가 (UX_FLOW.md 9절-3 요구사항): "클릭 관통(SetClickThrough) ON 상태에서도 커서
    /// 근접 앰비언트 반응을 위해 전역 커서 좌표 폴링이 독립적으로 필요"하다. IPlatformWindowService에
    /// 메서드를 추가하는 방식도 검토했으나, 그 인터페이스는 Phase 0에서 팀이 확정한 공개 계약이고
    /// Debugger가 동시에 BUG_REPORT_PHASE0.md를 작성 중이라 시그니처를 임의로 확장하면 편집 충돌
    /// 위험이 있었다. 따라서 클릭 관통(SetClickThrough)이 있는 인터페이스는 그대로 두고, 커서 좌표
    /// 조회만 이 신규 인터페이스로 완전히 독립된 경로에 둔다 — 클릭 관통을 켜고 끄는 동작이 이 조회
    /// 경로에 어떤 영향도 주지 않음이 타입 수준에서 보장된다(Win32 구현체의 GetCursorPos는 SetClickThrough가
    /// 건드리는 WS_EX_TRANSPARENT 확장 스타일과 완전히 무관한 별도 API).
    ///
    /// 구현은 선택적이다: 각 IPlatformWindowService 구현체가 이 인터페이스도 함께 구현하고 싶으면
    /// 구현하되(예: Win32WindowService, NullPlatformWindowService), 모바일(ScreenshotBackdropPlatformService)처럼
    /// "전역 커서" 개념 자체가 없는 플랫폼은 구현하지 않아도 된다 — 소비 측은 `as ICursorPositionService`로
    /// 안전하게 캐스팅해 null 여부로 지원 여부를 판정한다(StickMate.Core.StickmanAgent 참고).
    ///
    /// 교차 레이어 로그: 이 설계 판단(신규 인터페이스 신설 vs 기존 인터페이스 확장)은 Tasklist.md에도
    /// "Debugger 검토 요청"으로 기록했다 — 더 나은 대안이 있다면 Phase 2 전에 재논의 가능.
    /// </summary>
    public interface ICursorPositionService
    {
        /// <summary>
        /// 현재 전역 커서 좌표를 OS 데스크톱 좌표계(좌상단 원점, 픽셀 — PlatformFoothold.ScreenRect와
        /// 동일 좌표계)로 반환한다. 커서 위치를 알 수 없는 환경(예: 터치 전용 기기)이면 false.
        /// </summary>
        bool TryGetGlobalCursorPosition(out Vector2 osScreenPosition);
    }

    /// <summary>
    /// TryGetGlobalCursorPosition과 동일한 시그니처의 델리게이트 — out 매개변수가 있어 System.Func로
    /// 표현할 수 없어 별도 선언한다(AutoWanderController.CursorPositionQuery와 동일한 이유로 별도 선언).
    /// StickmanAgent.TryGetCursorPosition을 여러 소비자(States.StickmanBlackboard.CursorProvider,
    /// Phase 3 드래그&던지기/로데오 커서 등)에 같은 델리게이트 타입으로 연결하기 위한 공용 타입 —
    /// 메서드 그룹 변환은 시그니처만 일치하면 임의의 델리게이트 타입에 성립하므로, 기존
    /// AutoWanderController.CursorProvider 배선(다른 델리게이트 타입)에는 아무 영향이 없다.
    /// </summary>
    public delegate bool CursorPositionQuery(out Vector2 osScreenPosition);
}
