using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// "발판"(Foothold) 하나를 표현하는 읽기 전용 데이터.
    /// 데스크톱에서는 실제 타 프로세스 창 하나, 모바일(스크린샷 백드롭 모드)에서는
    /// 유저가 탭으로 지정한 정적 사각형 하나에 대응된다.
    /// 절대 원칙: 이 값을 이용해 원본 창/유저 자산을 이동·수정하는 어떤 API도 호출하지 않는다 (읽기 전용).
    /// </summary>
    public readonly struct PlatformFoothold
    {
        /// <summary>
        /// 플랫폼별 핸들/ID. Windows는 HWND.ToInt64(), macOS는 CGWindowID, 모바일은 유저 지정 항목의 순번 id.
        /// 열거 결과를 구분하기 위한 식별자일 뿐, 이 값으로 원본을 다시 조작하는 API를 호출하지 않는다.
        /// </summary>
        public readonly long Handle;

        /// <summary>
        /// 스크린 좌표계 사각형. 원점은 OS 네이티브 좌상단(0,0), 픽셀 단위이며 멀티 모니터 환경에서는
        /// 음수 좌표도 나올 수 있다. Unity의 Screen/GUI 좌표(좌하단 원점)와 다르므로
        /// 소비 측(렌더링/발판 스냅 로직)에서 명시적으로 변환해야 한다.
        /// </summary>
        public readonly Rect ScreenRect;

        /// <summary>이 발판이 현재 최상단(포그라운드) 창인지 여부. 모바일은 우선순위 개념이 없어 항상 true.</summary>
        public readonly bool IsTopmost;

        public PlatformFoothold(long handle, Rect screenRect, bool isTopmost)
        {
            Handle = handle;
            ScreenRect = screenRect;
            IsTopmost = isTopmost;
        }
    }

    /// <summary>
    /// 데스크톱(macOS/Windows)의 실제 타 윈도우 열거와, 모바일(iPad/iPhone)의 유저 지정 정적 발판을
    /// 동일한 계약으로 추상화하는 인터페이스 (아키텍처 0-1절). 상태머신/게임플레이 코드는 이 인터페이스만
    /// 알고 있어야 하며, Win32WindowService/ScreenshotBackdropPlatformService 등 구체 구현을 직접 참조하지 않는다.
    ///
    /// 절대 금지: 타 윈도우를 이동/크기변경/최소화/종료/포커스 강제하는 메서드는 이 인터페이스에 추가하지 않는다
    /// (아키텍처 3절 "유저 자산 불변" 원칙). 오직 읽기 전용 열거 + "우리 오버레이 자신"의 속성 제어만 허용한다.
    /// </summary>
    public interface IPlatformWindowService
    {
        /// <summary>
        /// 현재 발판 목록을 읽기 전용으로 열거한다. 구현체는 매 호출마다 새 List를 할당하지 말고
        /// 내부 버퍼를 재사용해야 한다(24시간 상주 앱, GC 압박 방지 컨벤션).
        /// 호출 빈도는 매 프레임이 아니라 StickConfig.footholdPollInterval 주기로 제한할 것을 권장하며,
        /// 실제 폴링(타이머)은 이 서비스가 아니라 상위 레이어(추후 Phase의 FootholdWatcher 등)의 책임이다.
        /// </summary>
        IReadOnlyList<PlatformFoothold> EnumerateFootholds();

        /// <summary>
        /// 스틱맨을 그릴 오버레이 창을 확보한다. 데스크톱은 실제 네이티브 오버레이(또는 자기 자신의
        /// 플레이어 창) 핸들을 얻어오는 실동작이고, 모바일은 이 앱 자체가 이미 포그라운드이므로
        /// 항상 성공(true)으로 취급하는 no-op이다.
        /// </summary>
        bool CreateOverlayWindow();

        /// <summary>
        /// 오버레이의 클릭 관통(마우스/터치 입력이 아래 창으로 그대로 통과) on/off.
        /// 비침해 원칙 2번(기본 ON)에 대응. 모바일은 이 개념 자체가 없으므로 no-op.
        /// </summary>
        void SetClickThrough(bool enabled);

        /// <summary>
        /// 오버레이를 항상 최상단으로 유지할지 토글. 데스크톱 전용 실동작이며, 모바일은 no-op.
        /// </summary>
        void SetAlwaysOnTop(bool enabled);

        /// <summary>
        /// 다른 앱이 현재 전체화면(예: 전체화면 게임)으로 실행 중인지 감지한다. 비침해 원칙에 따라
        /// true를 반환하면 오버레이를 자동 숨김하는 데 사용된다. 데스크톱 전용 실동작이며,
        /// 모바일은 이 앱 자체가 포그라운드이므로 "다른 전체화면 앱"이라는 개념이 성립하지 않아 항상 false.
        /// </summary>
        bool IsFullscreenAppActive();
    }
}
