using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// "지금 마우스 왼쪽 버튼이 눌려 있는가"를 창 포커스와 무관하게 조회하는 채널.
    /// ICursorPositionService("커서가 지금 화면 어디에 있는가")와 정확히 같은 이유로
    /// IPlatformWindowService에서 분리되어 있고, 지원하지 않는 플랫폼은 아예 구현하지 않는다
    /// (소비 측은 `as IGlobalPointerButtonService`로 지원 여부를 판정 — StickmanAgent의
    /// ICursorPositionService 캐스팅 패턴과 동일).
    ///
    /// ============================================================================
    /// 왜 Unity의 OnMouseDown만으로는 부족한가 (드래그&던지기 배선 라운드, 2026-08-28)
    /// ============================================================================
    /// Interaction/StickmanClickHitbox.cs의 Unity 표준 경로(OnMouseDown/OnMouseUp)는 **우리 창이
    /// 마우스 이벤트를 실제로 수신했을 때만** 동작한다. 그런데 이 앱의 창은 (a) 항상위 투명 오버레이이고
    /// (b) 평소 클릭관통 상태이며 (c) 대개 키보드 포커스가 없는 비활성 앱이다. macOS에서 비활성 앱의
    /// 창을 클릭하면 기본적으로 그 첫 클릭은 "앱 활성화"에만 쓰이고 콘텐츠 뷰로 전달되지 않을 수 있다
    /// (NSView.acceptsFirstMouse 기본 NO). 그러면 사용자는 "한 번 눌렀는데 아무 일도 안 일어나는" 경험을
    /// 하게 된다.
    ///
    /// 그래서 이 인터페이스는 **창 포커스와 완전히 무관한 보조 트리거 경로**를 제공한다. 조회 전용이며
    /// (버튼 상태를 읽기만 한다) 어떤 입력도 주입하지 않고, 이벤트 탭(CGEventTap)처럼 접근성 권한을
    /// 요구하지도 않는다 — 유저 자산 불변 원칙(CLAUDE.md 3)과 무관한 순수 read-only 채널이다.
    ///
    /// 판정 규칙(중요, 비침해 원칙 2 유지): 이 채널이 "버튼이 눌려 있다"고 알려주는 것만으로는 아무 일도
    /// 일어나지 않는다. 소비자(StickmanClickHitbox)는 반드시 "그 순간 커서가 캐릭터 콜라이더 위에 있다"는
    /// 조건을 함께 만족할 때만 드래그를 시작한다 — 즉 판정 영역은 Unity 경로와 완전히 동일하고,
    /// 캐릭터 밖 클릭은 이 경로로도 절대 잡히지 않는다.
    /// </summary>
    public interface IGlobalPointerButtonService
    {
        /// <summary>
        /// 왼쪽 버튼이 지금 눌려 있으면 pressed=true. 조회 자체가 불가능한 환경이면 false를 반환하고
        /// 소비자는 Unity 표준 경로만 쓴다(조용한 오동작 대신 "지원 안 함"을 명시적으로 알린다).
        /// </summary>
        bool TryGetPrimaryButtonPressed(out bool pressed);

        /// <summary>
        /// 오른쪽 버튼이 지금 눌려 있으면 pressed=true. 왼쪽과 완전히 같은 조회 API(macOS:
        /// CGEventSourceButtonState의 버튼 번호만 다름)라 추가 권한도, 추가 위험도 없다.
        ///
        /// 왜 오른쪽 버튼이 필요한가(2026-08-28 "앱을 제어할 수단" 라운드): 캐릭터 위 **왼쪽** 클릭은
        /// 이미 드래그&던지기(12절)가 쓰고 있어 다른 의미를 얹을 수 없다. 오른쪽 버튼은 이 앱에서
        /// 아무도 쓰지 않으므로, "캐릭터를 우클릭하면 제어 메뉴가 뜬다"는 데스크톱 앱의 관습적인
        /// 조작을 기존 상호작용과 충돌 없이 얹을 수 있다(Interaction/AppControlDirector.cs).
        /// </summary>
        bool TryGetSecondaryButtonPressed(out bool pressed);
    }
}
