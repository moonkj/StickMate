using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 부분적 클릭관통 해제(Partial Click-Through Override, docs/UX_FLOW.md 15절) 계약.
    /// ICursorPositionService와 똑같은 이유로 IPlatformWindowService에서 분리한다 — 모바일
    /// (ScreenshotBackdropPlatformService)에는 애초에 "전역 클릭관통"이라는 개념 자체가 없어(이미 모든
    /// 탭을 앱이 직접 받음, "클릭관통 개념 없음") "그 일부를 국소 해제"한다는 개념도 성립하지 않는다.
    /// 데스크톱 구현체만 이 인터페이스를 구현하고, 소비 측은 `as ILocalClickCaptureService`로 지원
    /// 여부를 판정한다(StickMate.Core.StickmanAgent의 ICursorPositionService 캐스팅 패턴과 동일).
    ///
    /// ============================================================================
    /// 핵심 한계 — 정직하게 문서화 (Tasklist.md 교차 레이어 로그에도 동일 내용 기록)
    /// ============================================================================
    /// 이 인터페이스가 실제로 보장하는 것은 "누가 지금 이 자원을 쥐고 있는가 + 그 히트박스 영역이 지금
    /// 무엇인가"라는 상태 부기(단일 소유자 락, 동적 영역 추적, Platform/LocalClickCaptureGate.cs)뿐이다.
    ///
    /// **진짜 OS 레벨 히트테스트(영역 밖 클릭은 100% 관통, 영역 안 클릭만 앱이 수신)는 별도로 분리된
    /// 오버레이 창(HWND, CreateWindowEx 기반)이 실제로 존재해야만 구현 가능**하고, 그 오버레이는
    /// BUG-B1(docs/BUG_REPORT_PHASE0.md Blocker)이 아직 해결되지 않아 존재하지 않는다 — 지금
    /// Win32WindowService.SetClickThrough()는 안전가드로 NotSupportedException을 던져 게임 자신의
    /// 창에 클릭관통을 아예 걸지 않고 있다(StickmanAgent.Start() 참고).
    ///
    /// 즉 **지금 Windows/에디터 빌드에서는 "전역 클릭관통" 자체가 실제로 켜져 있지 않다** — 게임 창이
    /// 이미 일반 창처럼 모든 클릭을 받고 있다. 그 결과 이 인터페이스의 Request/Update/Release는 현재
    /// "다른 방해성 이벤트가 이 자원을 동시에 요청하지 못하게 막는 소유권 부기 + 향후 확장 지점" 역할만
    /// 하며, 실제 OS 히트테스트 변경은 수행하지 않는다(각 구현체의 메서드 본문 주석 참고). 진짜 분리
    /// 오버레이가 생기고 그 창이 실제로 클릭관통 ON 상태가 된 이후에야, 이 인터페이스의 구현이
    /// SetWindowRgn 또는 WM_NCHITTEST 커스텀 처리로 실제 영역 기반 히트테스트를 걸 수 있게 된다(후속
    /// 작업, Tasklist.md에 기록).
    ///
    /// 이 한계와 완전히 별개로, **Unity 게임 오브젝트 레벨의 히트박스 클릭 감지**(OnMouseDown/
    /// Physics2D 기반)는 이 인터페이스와 무관하게 지금 100% 완성 가능하고 완성되어 있다 —
    /// Interaction/StickmanClickHitbox.cs가 그 역할을 담당한다("캐릭터를 클릭하면 반응한다"는 지금 실제로
    /// 동작한다). 다만 "그 외 영역은 항상 100% 관통된다"는 보장은 진짜 오버레이 구현 전까지는 성립하지
    /// 않는다 — 오히려 지금은 클릭관통 자체가 꺼져 있으므로 게임 창이 어디를 클릭해도 항상 입력을 받는
    /// 것이 현재 상태이며, 이는 BUG-B1과 같은 원인에서 비롯된 이미 알려진 결함이다(신규 결함 아님).
    ///
    /// 요약: "캐릭터 히트박스 클릭 감지"(Unity 레벨)와 "그 외 영역 100% 관통 보장"(OS 레벨)은 서로 다른
    /// 절반이며, 지금은 앞의 절반만 완성 상태다. 이 인터페이스는 뒤의 절반을 위한 확장 지점을 미리
    /// 열어두는 것이 목적이다(UX_FLOW.md 15절 "Phase 1과의 접점" 요구사항).
    /// </summary>
    public interface ILocalClickCaptureService
    {
        /// <summary>
        /// hitboxOsScreen 영역에 한해서만, 그 외 영역은 100% 관통 유지한 채로 클릭을 앱이 수신하도록
        /// 요청한다(15절). owner는 단일 소유자 락 식별자 — 이미 다른 owner가 점유 중이면 false.
        /// 반환값이 true라도, 위 "핵심 한계"에 명시된 대로 지금은 실제 OS 히트테스트가 걸리지 않는다는
        /// 점을 호출자가 인지해야 한다(소유권 획득 자체는 유효 — 상호 배제 목적으로는 그대로 사용 가능).
        /// </summary>
        bool RequestLocalClickCapture(Rect hitboxOsScreen, object owner);

        /// <summary>동적 히트박스 추적(15절 제약 1) — 소유자가 매 프레임 호출해 영역을 갱신할 수 있다.
        /// owner가 현재 소유자가 아니면 no-op.</summary>
        void UpdateLocalClickCaptureRegion(Rect hitboxOsScreen, object owner);

        /// <summary>부분적 클릭관통 해제를 해제한다. owner가 현재 소유자가 아니면 no-op(안전한 중복 호출 허용).</summary>
        void ReleaseLocalClickCapture(object owner);

        /// <summary>owner가 현재 이 자원을 쥐고 있는지 조회(방어적 확인용).</summary>
        bool IsLocalClickCaptureOwnedBy(object owner);
    }
}
