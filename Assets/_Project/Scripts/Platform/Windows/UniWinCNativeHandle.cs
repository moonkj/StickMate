#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// ★ 2026-09-01 (debugger) — <b>라이브러리가 실제로 조작 중인 창</b>의 HWND를 네이티브에게 직접 묻는다.
    ///
    /// ============================================================================
    /// 왜 필요한가 — 지금까지 우리는 "아마 같은 창일 것"이라고 가정하고 있었다
    /// ============================================================================
    /// 지금까지 <c>Win32WindowService</c>/<c>WindowsCompositionProbe</c>는 오버레이 핸들을
    /// <c>Process.GetCurrentProcess().MainWindowHandle</c>로 얻었다. 그런데 네이티브
    /// <c>LibUniWinC</c>가 붙잡는 창은 <b>다른 규칙</b>으로 골라진다(패키지 C++ 소스 실측,
    /// <c>attachOwnerWindowProc</c>):
    ///   · .NET <c>MainWindowHandle</c> — 우리 PID의 <b>보이고 오너가 없는</b> 첫 최상위 창
    ///   · LibUniWinC <c>AttachMyOwnerWindow</c> — 우리 PID의 <b>첫</b> 최상위 창(가시성 무관)을 찾은 뒤
    ///     <c>GW_OWNER</c>가 있으면 <b>그 오너</b>를 붙잡는다
    /// 두 규칙이 같은 창을 고른다는 보장은 어디에도 없다(IME 창 등 프로세스에는 우리가 만들지 않은
    /// 최상위 창이 존재한다). 두 값이 갈리면 <b>진단은 A 창을 재고 라이브러리는 B 창을 고치는</b>
    /// 상태가 되며, 그때 로그의 모든 스타일/알파 판정은 무의미해진다.
    ///
    /// 실기 로그의 <c>[LAYERED-ALPHA] 레이어드 알파=0/255</c>는 "창 전체가 100% 비친다"는 뜻인데
    /// 사용자 화면에는 앱이 <b>보인다</b> — 이 모순의 후보 원인이 정확히 이 핸들 불일치다.
    /// 그래서 추측을 없애고 <b>네이티브에게 직접 묻는다</b>.
    ///
    /// <c>GetWindowHandle</c>은 동봉 DLL의 export 목록에 실제로 존재한다(<c>strings</c> 확인).
    /// 그래도 없을 가능성에 대비해 예외를 삼키고 <see cref="Fallback"/>으로 물러난다 —
    /// 진단/보조 장치가 앱을 죽이지 않는다.
    /// </summary>
    internal static class UniWinCNativeHandle
    {
        private static bool _probed;
        private static bool _available;
        private static bool _mismatchLogged;

        /// <summary>네이티브가 붙잡은 창. 못 얻으면 <see cref="IntPtr.Zero"/>.</summary>
        internal static IntPtr TryGetNative()
        {
            if (!_probed)
            {
                _probed = true;
                try { GetWindowHandle(); _available = true; }
                catch (EntryPointNotFoundException) { _available = false; }
                catch (DllNotFoundException) { _available = false; }
            }
            if (!_available) return IntPtr.Zero;
            try { return GetWindowHandle(); }
            catch (Exception) { return IntPtr.Zero; }
        }

        /// <summary>.NET이 고르는 창(예전 경로). 네이티브를 못 얻을 때만 쓴다.</summary>
        internal static IntPtr Fallback()
        {
            try
            {
                using (var self = System.Diagnostics.Process.GetCurrentProcess())
                {
                    return self.MainWindowHandle;
                }
            }
            catch (Exception) { return IntPtr.Zero; }
        }

        /// <summary>
        /// 최종 오버레이 핸들. 네이티브 값을 우선하고, 두 값이 <b>다르면 한 번만</b> 경고를 남긴다 —
        /// 그 한 줄이 "지금까지의 모든 스타일 진단이 엉뚱한 창을 재고 있었다"를 증명하거나 반증한다.
        /// </summary>
        internal static IntPtr Resolve()
        {
            IntPtr native = TryGetNative();
            IntPtr managed = Fallback();

            if (native != IntPtr.Zero && managed != IntPtr.Zero && native != managed && !_mismatchLogged)
            {
                _mismatchLogged = true;
                Debug.LogWarning("[오버레이핸들] ★ 네이티브(LibUniWinC.GetWindowHandle)와 " +
                    ".NET(Process.MainWindowHandle)이 <서로 다른 창>을 가리킵니다 — " +
                    $"네이티브=0x{native.ToInt64():X}, .NET=0x{managed.ToInt64():X}. " +
                    "지금까지의 [합성진단] 스타일/알파 판정은 .NET 쪽 창을 재고 있었으므로 " +
                    "<라이브러리가 실제로 투명화한 창의 상태가 아닙니다>. 이제부터 네이티브 핸들을 씁니다.");
            }
            else if (native == IntPtr.Zero && managed != IntPtr.Zero && !_mismatchLogged)
            {
                _mismatchLogged = true;
                Debug.Log("[오버레이핸들] LibUniWinC.GetWindowHandle을 아직/영영 쓸 수 없어 " +
                    $".NET MainWindowHandle(0x{managed.ToInt64():X})을 그대로 씁니다(예전과 동일 동작).");
            }

            return native != IntPtr.Zero ? native : managed;
        }

        // ────────────────────────────────────────────────────────────────────────
        // 유리(DWM 확장 프레임)만 다시 거는 경로 — 2026-09-02 (1px 래칫 라운드)
        // ────────────────────────────────────────────────────────────────────────

        private static bool _glassPathUnavailable;
        private static bool _glassPathFailureLogged;

        /// <summary>
        /// 유리 전용 경로를 <b>이미 못 쓴다고 확인된</b> 상태인가. 처음에는 false(낙관)이며,
        /// <see cref="TrySetTransparent"/>가 한 번이라도 실패하면 그때 true가 된다.
        ///
        /// <para>"쓸 수 있는지 미리 알아보려고 한 번 호출해 보는" 방식은 일부러 쓰지 않았다 —
        /// 그 탐침 자체가 부작용 있는 호출이라, 결정을 내리기도 전에 상태를 바꾸게 된다.
        /// 대신 실패하면 <b>같은 틱 안에서</b> 호출자가 전체 경로로 물러난다.</para>
        /// </summary>
        internal static bool GlassOnlyPathKnownUnavailable => _glassPathUnavailable;

        /// <summary>
        /// <b>유리만</b> 다시 건다 — 네이티브 <c>SetTransparent</c>는
        /// <c>DwmExtendFrameIntoClientArea(hWnd, MARGINS{-1})</c> 한 줄이라 <b>창 사각형을 건드리지
        /// 않는다</b>(libuniwinc.cpp <c>enableTransparentByDWM</c>).
        ///
        /// <para>라이브러리의 <c>isTransparent</c> 세터를 쓰지 않는 이유는 그것이
        /// <c>SetTransparent</c>와 <c>SetBorderless</c>를 한 덩어리로 부르기 때문이다
        /// (<c>UniWinCore.EnableTransparent</c>). 그 <c>SetBorderless</c>가 매번 창 폭을 ±1 흔들어
        /// <b>리사이즈 4회 = 스왑체인 재생성 4회</b>를 만들고, 그 잔차가 1px 래칫이 됐다.
        /// 근거 전문은 <c>Platform/OverlayStateReapplyPolicy.cs</c>.</para>
        ///
        /// <para>대상은 네이티브가 이미 붙잡고 있는 <b>우리 자신의 창</b>이다(핸들 인자가 없는 이유 —
        /// 원칙 3 준수). 부착 전이면 네이티브가 <c>if (hTargetWnd_)</c>에서 조용히 아무것도 하지 않는다.</para>
        /// </summary>
        /// <returns>호출이 실제로 내려갔으면 true. false면 호출자는 <b>같은 틱에</b> 라이브러리 전체
        /// 경로로 물러나야 한다 — 회색 불투명 전체화면 창보다는 1px 래칫이 낫다.</returns>
        internal static bool TrySetTransparent(bool enabled)
        {
            if (_glassPathUnavailable) return false;
            try
            {
                SetTransparent(enabled);
                return true;
            }
            catch (Exception e)
            {
                _glassPathUnavailable = true;
                if (!_glassPathFailureLogged)
                {
                    _glassPathFailureLogged = true;
                    Debug.LogWarning("[오버레이핸들] LibUniWinC.SetTransparent를 쓸 수 없습니다 " +
                        $"({e.GetType().Name}) — 재적용은 라이브러리 전체 경로(isTransparent 대입)로 " +
                        "물러납니다. 투명화는 유지되지만 재적용 1회마다 창 리사이즈 4회가 " +
                        "되살아납니다(1px 래칫).");
                }
                return false;
            }
        }

        [DllImport("LibUniWinC", CallingConvention = CallingConvention.Winapi)]
        private static extern void SetTransparent([MarshalAs(UnmanagedType.U1)] bool bEnabled);

        [DllImport("LibUniWinC")]
        private static extern IntPtr GetWindowHandle();
    }
}
#endif
