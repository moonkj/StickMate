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
    public sealed class Win32WindowService : IPlatformWindowService
    {
        #region Win32 선언 (이 리전 밖으로 유출 금지)
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        #endregion

        // 우리 오버레이(Unity 플레이어) 창 핸들. 절대로 "타 윈도우" 핸들을 여기 담지 않는다.
        private IntPtr _overlayHwnd;

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
            return _overlayHwnd != IntPtr.Zero;
        }

        public void SetClickThrough(bool enabled)
        {
            if (_overlayHwnd == IntPtr.Zero) return;

            int exStyle = GetWindowLong(_overlayHwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED; // 클릭관통을 쓰려면 레이어드 창이어야 함
            exStyle = enabled ? (exStyle | WS_EX_TRANSPARENT) : (exStyle & ~WS_EX_TRANSPARENT);
            SetWindowLong(_overlayHwnd, GWL_EXSTYLE, exStyle);
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            if (_overlayHwnd == IntPtr.Zero) return;
            IntPtr insertAfter = enabled ? HWND_TOPMOST : HWND_NOTOPMOST;
            // SWP_NOMOVE|SWP_NOSIZE: Z-order(항상 위)만 바꾸고 위치/크기는 절대 바꾸지 않는다.
            SetWindowPos(_overlayHwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
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
