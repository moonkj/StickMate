#if UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows 전용 IPlatformWindowService 구현체. Win32 P/Invoke는 프로젝트 전체에서 이 파일에만
    /// 격리한다(컨벤션 준수) — 다른 어떤 코드도 user32.dll을 직접 호출하지 않는다.
    ///
    /// 이 클래스가 절대 포함하지 않는 것: 타 프로세스 창을 이동(좌표 변경)시키거나, 크기를 바꾸거나,
    /// 종료(WM_CLOSE 전송/TerminateProcess)시키는 메서드. 오직 열거(읽기)와 "우리 오버레이 자신"의
    /// 확장 스타일(WS_EX_LAYERED/TRANSPARENT)·Z-order만 다룬다 (아키텍처 3절 유저 자산 불변 원칙).
    /// </summary>
    public sealed class Win32WindowService : IPlatformWindowService, ICursorPositionService
    {
        #region Win32 선언 (이 리전 밖으로 유출 금지)
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        // BUG-B1(c) 대응(Debugger/Architect 권고): 클릭관통 창이라도 이 플래그 없이는 Alt-Tab 등을
        // 계기로 간헐적으로 OS 포그라운드 포커스를 가져가 사용자가 다른 앱에 입력 중인 포커스를
        // 뺏을 수 있다(가설 H2, docs/BUG_REPORT_PHASE0.md). 오버레이는 애초에 포커스를 받을 필요가
        // 없으므로 클릭 관통을 켤 때 항상 함께 적용한다.
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        #endregion

        // 우리 오버레이(Unity 플레이어) 창 핸들. 절대로 "타 윈도우" 핸들을 여기 담지 않는다.
        private IntPtr _overlayHwnd;

        // BUG-B1 대응(Blocker, Debugger/Architect 권고, 2026-08-27): CreateOverlayWindow()는 아직
        // 진짜 분리된 오버레이 창을 만들지 않고 Unity 게임 자신의 MainWindowHandle을 재사용하는
        // 스텁이다. 이 상태로 SetClickThrough(true)/SetAlwaysOnTop(true)를 그대로 걸면 게임 창
        // 자체가 클릭관통되어 앱의 모든 마우스 입력이 영구히 막히고, 불투명한 게임 창이 항상
        // 최상단에 고정되어 데스크톱을 가릴 수 있다(비침해 원칙 정반대). 진짜 오버레이 HWND
        // (CreateWindowEx로 가상 데스크톱 전체 크기, 처음부터 WS_EX_LAYERED|WS_EX_TRANSPARENT|
        // WS_EX_TOPMOST|WS_EX_NOACTIVATE를 가진 투명 창) 구현은 Windows 네이티브 창 생성/메시지
        // 루프 통합이 필요한 별도 작업이라 이번 Phase 1 범위를 넘는다(Architect 판단). 그 전까지는
        // 이 플래그로 "위험한 부작용을 내는 대신 안전하게 실패"하도록 가드한다.
        private bool _usingUnsafeSelfWindowFallback;

        // EnumWindows 콜백 델리게이트는 인스턴스당 1회만 생성해 매 호출마다 델리게이트를
        // 재할당하지 않는다 (24시간 상주 앱, GC 압박 방지 컨벤션 — Update성 호출부에서 특히 중요).
        private readonly EnumWindowsProc _enumWindowsCallback;

        // 열거 결과 버퍼. 매 호출 시 새 List를 만들지 않고 Clear 후 재사용한다.
        private readonly List<PlatformFoothold> _footholdBuffer = new List<PlatformFoothold>(64);

        public Win32WindowService()
        {
            _enumWindowsCallback = OnEnumWindow;
        }

        private bool OnEnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            if (!IsWindowVisible(hWnd)) return true;
            if (GetWindowTextLength(hWnd) == 0) return true; // 타이틀 없는 배경 프로세스 창 제외
            if (!GetWindowRect(hWnd, out var rect)) return true;

            bool isTopmost = hWnd == GetForegroundWindow();
            var screenRect = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            _footholdBuffer.Add(new PlatformFoothold(hWnd.ToInt64(), screenRect, isTopmost));
            return true; // true = 열거 계속. 다른 창을 이동/조작하는 코드는 여기 절대 추가하지 않는다.
        }

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            _footholdBuffer.Clear();
            EnumWindows(_enumWindowsCallback, IntPtr.Zero);
            return _footholdBuffer;
        }

        public bool CreateOverlayWindow()
        {
            // Phase 0 스텁: 별도 네이티브 창을 새로 만들지 않고, 이미 실행 중인 Unity 플레이어의
            // 메인 창 핸들을 오버레이로 재사용한다. 실제 레이어드/반투명 합성 세팅(원치 않는 배경 제거 등)은
            // Phase 1/4에서 보강한다.
            _overlayHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            // BUG-B1: 이 핸들은 "우리가 새로 만든 오버레이"가 아니라 게임 자신의 창이다 — 아래
            // SetClickThrough/SetAlwaysOnTop이 안전 가드를 걸 수 있도록 표시해둔다.
            _usingUnsafeSelfWindowFallback = true;
            return _overlayHwnd != IntPtr.Zero;
        }

        public void SetClickThrough(bool enabled)
        {
            if (_overlayHwnd == IntPtr.Zero) return;

            // BUG-B1 안전 가드(Blocker, Debugger/Architect 권고): 진짜 오버레이가 아직 없는 동안
            // 이 메서드가 그대로 실행되면 "게임 창 자체"가 클릭관통되어 앱의 모든 마우스 입력이
            // 영구히 막힌다. 조용히 부작용을 내는 대신 즉시 실패시켜 호출부가 알아채게 한다
            // (StickMate.Core.StickmanAgent가 이 예외를 잡아 로그로 남기고 나머지 초기화를 계속한다).
            if (_usingUnsafeSelfWindowFallback)
            {
                throw new NotSupportedException(
                    "Win32WindowService.SetClickThrough(): 진짜 분리된 오버레이 창이 아직 구현되지 않아 " +
                    "게임 자신의 창에 클릭관통을 걸면 모든 마우스 입력이 막힙니다(BUG-B1, docs/BUG_REPORT_PHASE0.md). " +
                    "CreateWindowEx 기반 실제 오버레이 구현 후 이 가드를 제거하세요.");
            }

            int exStyle = GetWindowLong(_overlayHwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED; // 클릭관통을 쓰려면 레이어드 창이어야 함
            exStyle |= WS_EX_NOACTIVATE; // BUG-B1(c): 포커스 탈취 방지(가설 H2) — 오버레이는 포커스가 필요 없음
            exStyle = enabled ? (exStyle | WS_EX_TRANSPARENT) : (exStyle & ~WS_EX_TRANSPARENT);
            SetWindowLong(_overlayHwnd, GWL_EXSTYLE, exStyle);
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            if (_overlayHwnd == IntPtr.Zero) return;

            // BUG-B1 안전 가드: 진짜 오버레이가 없는 동안 게임의 불투명한 창을 항상 최상단으로
            // 고정하면 데스크톱 전체를 가릴 수 있다(비침해 원칙 정반대) — SetClickThrough와 동일한 이유로 차단.
            if (_usingUnsafeSelfWindowFallback)
            {
                throw new NotSupportedException(
                    "Win32WindowService.SetAlwaysOnTop(): 진짜 분리된 오버레이 창이 아직 구현되지 않아 " +
                    "게임 자신의 불투명한 창이 항상 최상단으로 고정되어 데스크톱을 가릴 수 있습니다(BUG-B1). " +
                    "CreateWindowEx 기반 실제 오버레이 구현 후 이 가드를 제거하세요.");
            }

            IntPtr insertAfter = enabled ? HWND_TOPMOST : HWND_NOTOPMOST;
            // SWP_NOMOVE|SWP_NOSIZE: Z-order(항상 위)만 바꾸고 위치/크기는 절대 바꾸지 않는다.
            SetWindowPos(_overlayHwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }

        // ICursorPositionService — GetCursorPos는 SetClickThrough가 건드리는 WS_EX_TRANSPARENT
        // 확장 스타일과 완전히 무관한 별도 Win32 API라서, 위 BUG-B1 가드와도 무관하게 항상 안전하게
        // 동작한다. 클릭 관통 여부와 관계없이 전역 커서 좌표를 조회하는 독립 경로를 제공한다
        // (UX_FLOW.md 9절-3 요구사항 — ICursorPositionService.cs 설계 의도 참고).
        public bool TryGetGlobalCursorPosition(out Vector2 osScreenPosition)
        {
            if (GetCursorPos(out POINT p))
            {
                osScreenPosition = new Vector2(p.X, p.Y);
                return true;
            }
            osScreenPosition = Vector2.zero;
            return false;
        }

        public bool IsFullscreenAppActive()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == _overlayHwnd) return false; // 우리 자신은 "다른 전체화면 앱"이 아님

            if (!GetWindowRect(fg, out var winRect)) return false;

            IntPtr monitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo)) return false;

            // 단순 휴리스틱: 전경 창 사각형이 모니터 전체 사각형과 정확히 일치하면 전체화면으로 간주.
            // 보더리스 윈도우/독점 전체화면 구분, 다중 모니터 경계 케이스 등은 Phase 4에서 정교화 예정.
            return winRect.Left == monitorInfo.rcMonitor.Left
                && winRect.Top == monitorInfo.rcMonitor.Top
                && winRect.Right == monitorInfo.rcMonitor.Right
                && winRect.Bottom == monitorInfo.rcMonitor.Bottom;
        }
    }
}
#endif
