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
            float osX = unityScreen.x * dpi;
            float osY = (Screen.height - unityScreen.y) * dpi; // 좌상단 원점으로 y 반전
            return new Vector2(osX, osY);
        }

        /// <summary>OS 데스크톱 좌표 -> Unity 월드 좌표. cameraDepth는 WorldToOsScreen에서 얻은 값을 그대로 넘길 것.</summary>
        public static Vector3 OsScreenToWorld(Camera cam, Vector2 osScreenPoint, float cameraDepth, StickConfig config)
        {
            float dpi = config != null ? Mathf.Max(0.0001f, config.desktopDpiScale) : 1f;
            float unityX = osScreenPoint.x / dpi;
            float unityY = Screen.height - (osScreenPoint.y / dpi); // 좌하단 원점으로 y 재반전
            return cam.ScreenToWorldPoint(new Vector3(unityX, unityY, cameraDepth));
        }
    }
}
