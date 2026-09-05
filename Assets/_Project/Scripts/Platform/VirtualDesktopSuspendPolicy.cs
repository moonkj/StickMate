namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-05 (M-8) — <b>"남의 가상 데스크톱에 있는 동안 스스로 숨는다"</b> 하나만 판정하는
    /// 순수 규칙. <c>UnityEngine</c> 의존도 <c>P/Invoke</c>도 <b>한 줄 없다</b>.
    ///
    /// <para>★ 이 파일이 <c>Platform/</c> 바로 아래 있는 것은 취향이 아니라 <b>사고 재발 방지</b>다.
    /// <c>FullscreenSuspendPolicy</c>가 한때 <c>Platform/MacOS/</c> 안에 있었고, 그 자리에 있는 동안
    /// Windows 구현은 같은 규칙을 <b>부를 수조차 없었다</b>. 그리고 이 규칙이
    /// <c>Platform/Windows/</c> 안에 있으면 <b>이 개발 머신에서 한 줄도 실행되지 않는다</b> —
    /// 즉 검증이 구조적으로 불가능한 자리다. 사실 조회(<c>IsWindowOnCurrentVirtualDesktop</c>)만
    /// 그쪽에 남긴다.</para>
    ///
    /// ============================================================================
    /// 보수 규칙 — 오판의 대가가 비대칭이다 (<c>SessionVisibilityPolicy</c>와 같은 방향)
    /// ============================================================================
    /// <list type="bullet">
    ///  <item>잘못 숨으면 → 사용자가 <b>「앱이 사라졌다」</b>를 신고한다. 되돌릴 UI가 화면에 없다
    ///        (숨었으니까). 이 저장소가 2026-09-03에 정확히 그 신고를 받았다 —
    ///        <i>"전부 다 없어져버려서 다시 나오게 할 방법이 없어"</i>.</item>
    ///  <item>잘못 안 숨으면 → <b>지금까지의 동작 그대로</b>다. Windows는 가상 데스크톱을 전환하면
    ///        그 데스크톱에 속한 창을 스스로 감추므로, 대부분의 경우 사용자에게는 아무 차이도 없다.</item>
    /// </list>
    /// 그래서 <see cref="VirtualDesktopMembership.Unknown"/>은 <b>숨지 않는다</b>.
    ///
    /// ============================================================================
    /// ★ 왜 그래도 숨는가 — OS가 이미 감춰 주는데
    /// ============================================================================
    /// 두 가지 때문이다. (1) <b>확신할 수 없다.</b> 우리 창은 <c>WS_EX_TOPMOST</c> + 툴윈도우 +
    /// 레이어드 조합이고, 그 조합이 어느 Windows 빌드에서든 반드시 데스크톱에 묶인다는 <b>1차 출처를
    /// 확인하지 못했다</b>. 새는 경우가 있다면 그건 비침해 원칙 2 위반이고, 이 판정이 그것을 덮는다.
    /// (2) <b>안 보이는 화면을 위해 계속 일하지 않는다.</b> 24시간 상주 앱이고, 숨는 동안은
    /// 물리·틱·렌더러가 멎는다.
    /// </summary>
    public static class VirtualDesktopSuspendPolicy
    {
        /// <summary>
        /// 이번 폴링에서 <b>캐릭터를 숨길 것인가</b>.
        /// <para><see cref="VirtualDesktopMembership.OtherDesktop"/> <b>하나만</b> 참이다 —
        /// <c>!= CurrentDesktop</c>으로 쓰면 <see cref="VirtualDesktopMembership.Unknown"/>이 함께
        /// 딸려 들어와 <b>조회 실패가 캐릭터를 영원히 숨긴다</b>. 그 한 글자가 이 파일의 전부다.</para>
        /// </summary>
        public static bool SuspendsCharacter(VirtualDesktopMembership membership)
            => membership == VirtualDesktopMembership.OtherDesktop;

        /// <summary>
        /// 로그/진단용 한 줄. <b>상수 문자열만 돌려주므로 할당이 0</b>이다(24시간 상주 앱 컨벤션).
        /// <para>이 개발 머신에 Windows가 없어서 <b>실기 로그가 사실상 유일한 확인 수단</b>이다 —
        /// 「소속 조회가 돌긴 하는가」를 사용자 <c>Player.log</c>에서 가릴 수 있어야 한다.</para>
        /// </summary>
        public static string Describe(VirtualDesktopMembership membership)
        {
            switch (membership)
            {
                case VirtualDesktopMembership.CurrentDesktop: return "현재 데스크톱";
                case VirtualDesktopMembership.OtherDesktop: return "다른 데스크톱";
                default: return "모름(조회실패 — 숨지 않습니다)";
            }
        }
    }
}
