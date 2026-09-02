#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// 우리 오버레이 창의 <c>GWL_STYLE</c>을 OS에서 <b>그대로 읽어오는</b> 사실 조회 전용 헬퍼
    /// (2026-09-02). 판정은 한 줄도 하지 않는다 — "이 값이 보더리스인가"는 플랫폼 중립
    /// <see cref="StickMate.Platform.OverlayStateReapplyPolicy.IsBorderless"/>에 있다
    /// (CLAUDE.md: 정책은 플랫폼 중립 위치, 플랫폼 전용 코드는 사실 조회만).
    ///
    /// <para><b>왜 라이브러리의 <c>IsBorderless()</c>를 쓰지 않는가</b>: 그 export는 네이티브가 들고
    /// 있는 캐시 필드 <c>bIsBorderless_</c>를 그대로 돌려준다(libuniwinc.cpp:492). 즉 OS가 스타일을
    /// 바꿔도 계속 옛 값을 말한다 — 이 저장소가 <c>isTopmost</c>에서 이미 한 번 당한 함정이고,
    /// 그때의 처방(캐시 대신 <c>GetWindowLong</c> 실측)을 여기서도 그대로 쓴다.</para>
    ///
    /// <para>대상은 <b>우리 자신의 창</b>뿐이고 호출은 전부 읽기다(원칙 3 — 유저 자산 불변).</para>
    /// </summary>
    internal static class WindowsWindowStyleProbe
    {
        private const int GwlStyle = -16;

        /// <summary>
        /// 실패하면 false. 실패의 뜻은 "지금은 모른다"이며, 호출자는 <b>모를 때 안전한 쪽</b>으로
        /// 분기해야 한다(<see cref="StickMate.Platform.OverlayStateReapplyPolicy.DecideTransparencyReapply"/>).
        /// </summary>
        internal static bool TryReadStyle(IntPtr hWnd, out long style)
        {
            style = 0L;
            if (hWnd == IntPtr.Zero) return false;

            try
            {
                if (!IsWindow(hWnd)) return false;
                long raw = GetWindowLongPtr(hWnd, GwlStyle).ToInt64();
                // GetWindowLong 계열은 실패를 0으로 알린다. 실제 창의 스타일이 0인 경우는 없다
                // (최소한 WS_VISIBLE이 서 있다) — WindowsLayeredHybridResolver와 같은 판정.
                if (raw == 0L) return false;
                style = raw;
                return true;
            }
            catch (Exception)
            {
                // 진단/보조 장치가 앱을 죽이지 않는다.
                return false;
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        /// <summary>32비트 플레이어에는 GetWindowLongPtrW가 아예 없다(별칭이 아니다).
        /// WindowsCompositionProbe / WindowsLayeredHybridResolver와 동일한 분기.</summary>
        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }
}
#endif
