using UnityEngine;
using StickMate.Core;

namespace StickMate.Platform
{
    /// <summary>
    /// Unity 씬 좌표계와 OS 데스크톱 좌표계 사이의 변환을 한 곳에 모아두는 유틸리티.
    /// (Debugger BUG-M5 대응: "각 상태가 개별 구현하면 좌표계 혼용 버그 위험" — 모든 소비자는
    /// 반드시 이 유틸만 거쳐야 하며 직접 Screen.height/DPI 계산식을 다시 쓰지 않는다.)
    ///
    /// 왜 변환이 필요한가 — 좌표계가 3중으로 다르다:
    /// 1) Unity 월드 좌표: 게임플레이가 쓰는 좌표(유닛), 카메라/오브젝트 배치 기준.
    /// 2) Unity 스크린 좌표: Camera.WorldToScreenPoint()의 결과. 원점이 "좌하단"이고 y가 위로 갈수록
    ///    증가하며, 단위는 Unity가 보고하는 픽셀("points" — 고DPI 화면에서 실제 백킹 픽셀과
    ///    다를 수 있음, 아래 DPI 설명 참고).
    /// 3) OS 데스크톱 좌표: PlatformFoothold.ScreenRect가 쓰는 좌표계(Platform/IPlatformWindowService.cs
    ///    주석 참고). 원점이 "좌상단"이고 y가 아래로 갈수록 증가하며, Win32 GetWindowRect 등 OS API가
    ///    보고하는 실제 화면 픽셀 단위.
    ///
    /// 변환 절차 (Unity 월드 -> OS 데스크톱):
    ///   a. Camera.WorldToScreenPoint로 Unity 스크린 좌표(좌하단 원점) 획득.
    ///   b. y를 Screen.height 기준으로 뒤집어 좌상단 원점으로 전환: osY = Screen.height - unityY.
    ///   c. StickConfig.desktopDpiScale을 곱해 "Unity가 보고하는 픽셀 단위" ↔ "OS가 보고하는 실제
    ///      데스크톱 픽셀 단위" 배율 차이를 보정한다. 예: macOS Retina에서 Unity가 백킹 스토어 픽셀을
    ///      보고하지만 CGWindowListCopyWindowInfo는 포인트(1x) 단위를 반환하는 경우, 또는 Windows에서
    ///      프로세스 DPI 인식 설정에 따라 GetWindowRect가 물리/논리 픽셀 중 무엇을 반환하는지 달라지는
    ///      경우(Debugger 가설 H3, docs/BUG_REPORT_PHASE0.md 참고, 실측 전까지는 1(배율 없음)로 둔다).
    ///
    /// 왕복 정밀도: 카메라가 직교(2D) 투영이더라도 Camera.ScreenToWorldPoint는 세 번째 인자를
    /// "카메라로부터의 거리"로 해석한다. WorldToOsScreen이 반환하는 cameraDepth를 OsScreenToWorld
    /// 호출 시 그대로 재사용해야 같은 z 평면으로 정확히 역변환된다(임의의 world z 값을 넣지 말 것).
    ///
    /// Phase 1 알려진 한계 (문서화 목적 — 임의 확장 금지, Tasklist.md 교차 레이어 로그 참고):
    /// - 오버레이가 OS 가상 데스크톱의 (0,0)에서 시작해 화면 전체를 덮는다고 가정한다. 실제 멀티모니터
    ///   배치(오프셋/다른 해상도)에 따른 보정은 IPlatformWindowService가 모니터 경계를 노출해야
    ///   가능한데, 이는 Phase 0 교차 레이어 로그 9절-5 항목으로 아직 미반영 상태다.
    /// - desktopDpiScale은 화면 전체에 대해 단일 값이다. 모니터마다 DPI가 다른 환경은 Phase 4 정교화 대상.
    /// </summary>
    public static class ScreenCoordinateConverter
    {
        /// <summary>
        /// 오버레이 창(= 우리 Unity Player 창)의 좌상단이 OS 데스크톱 좌표계에서 어디에 있는지.
        /// 기본값 (0,0)은 "창이 화면 좌상단에서 시작한다"는 이 클래스의 원래 가정이며, 그 가정이
        /// 맞는 환경(에디터/헤드리스/Windows 스텁)에서는 아래 두 변환식이 예전과 완전히 동일하게 동작한다.
        ///
        /// ============================================================================
        /// 왜 필요한가 — 드래그&던지기 배선 라운드(2026-08-28)에 실측 로그로 드러난 좌표 어긋남
        /// ============================================================================
        /// 직전 라운드 실측: `windowSize=(1512, 846)`, `windowPosition=(0, 75)`. 즉 우리 창은 화면 폭은
        /// 전부 덮지만 **세로로는 메뉴바/Dock을 뺀 가운데 846pt 구간에만** 존재한다. 그런데 이 클래스의
        /// 기존 식은 `osY = (Screen.height - unityY) * dpi`로 "창 좌상단 = 화면 좌상단"을 가정하므로,
        /// 커서(CGEventGetLocation, 화면 전역 좌표)를 월드로 되돌릴 때 창 오프셋만큼 통째로 틀어진다.
        /// 실측값 기준 세로 오차는 약 60~75 OS-pt = 월드 약 2유닛으로, 캐릭터 전신 높이(2.27유닛)에
        /// 맞먹는다 — 드래그하면 캐릭터가 커서에서 한 몸 길이만큼 벗어난 채 따라다니게 된다.
        ///
        /// 그래서 "OS 데스크톱 좌표 ↔ 창 클라이언트 좌표"의 원점 차이를 이 한 값으로 흡수한다. 실제
        /// 갱신은 Platform/MacOS/MacWindowService.EnumerateFootholds()가 이미 돌고 있는 창 열거
        /// 루프에서 자기 창(IsSelfWindow)의 kCGWindowBounds를 집어 그대로 대입한다 — 추가 시스템 호출이
        /// 전혀 없고, 커서 좌표(CGEventGetLocation)와 **완전히 같은 Quartz 좌표계**라 좌표계 혼용
        /// 위험도 없다(MacWindowService의 ICursorPositionService 주석 참고).
        ///
        /// static 가변 상태인 이유: 이 클래스는 순수 static 유틸이고 소비자(States/Interaction 전역)가
        /// 인스턴스를 들고 다니지 않는다. 프로젝트에 이미 같은 성격의 static이 있다(Core/SpectacleEventLock,
        /// Core/StressGauge). 기본값이 (0,0)이라 세팅하지 않는 플랫폼/테스트는 기존 동작 그대로다.
        /// </summary>
        public static Vector2 OverlayOriginOsScreen { get; set; } = Vector2.zero;

        /// <summary>
        /// Unity 월드 좌표 -> OS 데스크톱 좌표(좌상단 원점, 픽셀).
        /// </summary>
        /// <param name="cameraDepth">
        /// 왕복 변환을 위해 함께 반환하는 "카메라로부터의 거리". 같은 호출 세트 안에서
        /// OsScreenToWorld로 되돌릴 때 이 값을 그대로 넘겨야 정밀도가 보존된다.
        /// </param>
        public static Vector2 WorldToOsScreen(Camera cam, Vector3 worldPos, StickConfig config, out float cameraDepth)
        {
            Vector3 unityScreen = cam.WorldToScreenPoint(worldPos); // 좌하단 원점, Unity 픽셀
            cameraDepth = unityScreen.z;

            float dpi = config != null ? Mathf.Max(0.0001f, config.desktopDpiScale) : 1f;
            Vector2 origin = OverlayOriginOsScreen;
            float osX = unityScreen.x * dpi + origin.x;
            float osY = (Screen.height - unityScreen.y) * dpi + origin.y; // 좌상단 원점으로 y 반전 + 창 오프셋
            return new Vector2(osX, osY);
        }

        /// <summary>OS 데스크톱 좌표 -> Unity 월드 좌표. cameraDepth는 WorldToOsScreen에서 얻은 값을 그대로 넘길 것.</summary>
        public static Vector3 OsScreenToWorld(Camera cam, Vector2 osScreenPoint, float cameraDepth, StickConfig config)
        {
            float dpi = config != null ? Mathf.Max(0.0001f, config.desktopDpiScale) : 1f;
            Vector2 origin = OverlayOriginOsScreen;
            float unityX = (osScreenPoint.x - origin.x) / dpi;
            float unityY = Screen.height - ((osScreenPoint.y - origin.y) / dpi); // 좌하단 원점으로 y 재반전
            return cam.ScreenToWorldPoint(new Vector3(unityX, unityY, cameraDepth));
        }
    }
}
