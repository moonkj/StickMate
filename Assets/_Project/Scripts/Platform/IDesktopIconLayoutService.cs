using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 바탕화면 아이콘 영역을 읽기 전용으로 조회하는 경로 (docs/UX_FLOW.md 27-2/27-5절, "복제 스프라이트"
    /// 파이프라인의 좌표 소스). ICursorPositionService/ILocalClickCaptureService와 똑같은 이유로
    /// IPlatformWindowService에서 분리한다 — 모바일(ScreenshotBackdropPlatformService)에는 "바탕화면
    /// 아이콘" 개념 자체가 없고, 이 앱 자신이 이미 포그라운드라 성립하지 않는다.
    ///
    /// 절대 금지: 아이콘을 재배치/삭제/실행하는 어떤 쓰기 API도 이 인터페이스에 추가하지 않는다
    /// (원칙 3 "유저 자산 불변" — 27-2/27-5의 핵심 전제). 오직 좌표 열거만 허용한다.
    ///
    /// ============================================================================
    /// 알려진 한계 — 정직하게 문서화 (Tasklist.md 교차 레이어 영향 로그에도 동일 내용 기록)
    /// ============================================================================
    /// 실제 Windows 데스크톱 아이콘 좌표는 Progman → SHELLDLL_DefView → SysListView32 창에
    /// LVM_GETITEMCOUNT/LVM_GETITEMPOSITION 메시지를 보내 얻을 수 있지만, 그 응답은 대상 프로세스의
    /// 메모리 공간에 있는 구조체를 가리키므로 VirtualAllocEx/WriteProcessMemory/ReadProcessMemory
    /// 기반 크로스 프로세스 IPC가 추가로 필요하다 — Win32WindowService의 기존 P/Invoke(EnumWindows/
    /// GetWindowRect류, 자기 프로세스 메모리만 다룸)보다 훨씬 복잡하고 위험도가 높으며, 이 개발 환경에는
    /// 검증할 실제 Windows 하드웨어가 없다(Unity 배치모드는 macOS에서 실행). 이번 라운드에서는 이 검증
    /// 불가능한 크로스 프로세스 코드를 작성하지 않고, Win32WindowService.TryGetIconRegion()이 정직하게
    /// false를 반환하도록 남겨둔다(Windows 실빌드에서 청소부/블랙홀은 안전하게 no-op — 트리거만 억제될
    /// 뿐 어떤 실패 모드도 없다). 대신 NullPlatformWindowService(에디터)는 좌표만 있으면 되는 오버레이
    /// 파이프라인/취소 로직을 검증할 수 있도록 합성 그리드를 반환한다. 근거: macOS 네이티브 플러그인
    /// 미구현(BUG_REPORT_PHASE0.md m8), BUG-B1 진짜 오버레이 미구현과 동일한 계열의 "정직한 커버리지 공백".
    /// </summary>
    public interface IDesktopIconLayoutService
    {
        /// <summary>
        /// 지금 바탕화면 아이콘이 차지하는 전체 바운딩 영역(OS 화면 좌표, 픽셀)을 읽기 전용으로 조회한다.
        /// 아이콘 좌표를 알 수 없는 환경(미지원 플랫폼, 아직 구현되지 않은 실제 OS 조회 등)이면 false.
        /// </summary>
        bool TryGetIconRegion(out Rect osScreenRegion);

        /// <summary>
        /// TryGetIconRegion이 반환한 영역 안의 개별 아이콘 셀 사각형들(OS 화면 좌표)을 읽기 전용으로
        /// 열거한다. 실제 아이콘 캡처/썸네일(무엇이 그려져 있는지)은 Phase2+ 렌더링 과제로 미뤄져 있으므로,
        /// 이 좌표들은 "그 위에 자리표시자 오버레이 스프라이트를 대신 움직이는" 용도로만 쓰인다.
        /// 구현체는 매 호출마다 새 List를 할당하지 말고 내부 버퍼를 재사용해야 한다(다른 Enumerate* 계열과
        /// 동일한 24시간 상주 앱 컨벤션).
        /// </summary>
        IReadOnlyList<Rect> EnumerateIconRects();
    }
}
