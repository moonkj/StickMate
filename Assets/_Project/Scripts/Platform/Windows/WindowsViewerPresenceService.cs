#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows용 <see cref="IViewerPresenceService"/> — macOS 구현과 <b>같은 날 같은 라운드에</b> 함께
    /// 만든다. 2026-08-31 오전에 "macOS에서만 고친 가려짐 필터가 Windows로 전파되지 않아 같은 버그가
    /// 계속 살아 있던" 사고가 있었고, 그 재발 방지가 이 프로젝트의 명시적 교훈이다
    /// (<c>VisibleTopEdgeSolver</c> 도입 경위 참고). 판단 로직은 플랫폼 중립
    /// <see cref="FramePacingPolicy"/> 한 곳에만 있고, 이 파일은 <b>OS에 사실을 묻는 일</b>만 한다.
    ///
    /// <para><b>전부 읽기 전용 조회다</b>(CLAUDE.md 원칙 3). 값을 바꾸는 API는 한 개도 없다.</para>
    ///
    /// <para><b>★ 정직한 플랫폼 차이 — 모니터 꺼짐을 아직 감지하지 못한다</b>:
    /// macOS는 <c>CGDisplayIsAsleep</c> 한 번으로 "화면이 꺼져 있다"를 즉답한다. Windows에는 대응하는
    /// 폴링 API가 없고, <c>RegisterPowerSettingNotification(GUID_MONITOR_POWER_ON)</c>으로
    /// <c>WM_POWERBROADCAST</c>를 받아야 한다 — 즉 <b>창 프로시저를 가로채야</b> 하는데 이 앱의 창은
    /// UniWindowController 네이티브 플러그인이 소유하고 있어 이번 라운드 범위를 넘는다. 그래서 여기서는
    /// <c>DisplayAsleep=false</c>로 <b>보수적으로 보고</b>하고(= 절감을 포기하고 정상 동작을 택한다),
    /// 대신 무입력 시간 기반 Away 등급은 양 플랫폼에서 동일하게 동작한다.
    /// 다음 라운드 후보: 화면보호기 실행 여부(<c>SPI_GETSCREENSAVERRUNNING</c>)를 화면 꺼짐의
    /// 근사치로 쓰는 방법 — 다만 화면보호기를 끈 사용자에게는 무용지물이라 근본 해법이 아니다.</para>
    /// </summary>
    internal sealed class WindowsViewerPresenceService : IViewerPresenceService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;      // 0 = 배터리, 1 = AC, 255 = 알 수 없음
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;  // 1 = 절전 모드(배터리 세이버) 켜짐 (Windows 10+)
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        private bool _warnedOnce;

        public bool TryGetPresence(out ViewerPresenceSnapshot snapshot)
        {
            snapshot = default;
            try
            {
                var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
                float idleSeconds = -1f;
                if (GetLastInputInfo(ref info))
                {
                    // dwTime/GetTickCount는 모두 32비트 밀리초 카운터이며 약 49.7일마다 한 바퀴 돈다.
                    // unchecked 뺄셈이면 한 바퀴 도는 순간에도 차이값은 여전히 옳다(부호 없는 랩어라운드).
                    uint delta = unchecked(GetTickCount() - info.dwTime);
                    idleSeconds = delta / 1000f;
                }

                bool onBattery = false;
                bool lowPower = false;
                if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS power))
                {
                    onBattery = power.ACLineStatus == 0;      // 255(알 수 없음)는 배터리로 치지 않는다.
                    lowPower = power.SystemStatusFlag == 1;   // 배터리 세이버 = macOS 저전력 모드에 대응.
                }

                snapshot = new ViewerPresenceSnapshot(false, idleSeconds, lowPower, onBattery);
                return true;
            }
            catch (Exception e)
            {
                if (!_warnedOnce)
                {
                    _warnedOnce = true;
                    Debug.LogWarning($"[프레임페이싱/presence] Windows 관측 실패({e.GetType().Name}) — " +
                        "적응형 프레임 등급을 끄고 항상 활성으로 동작합니다. " + e.Message);
                }
                return false;
            }
        }
    }
}
#endif
