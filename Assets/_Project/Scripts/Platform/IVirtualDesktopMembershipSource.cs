namespace StickMate.Platform
{
    /// <summary>
    /// 우리 오버레이 창이 <b>지금 사용자가 보고 있는 가상 데스크톱</b>에 속해 있는가.
    ///
    /// <para>「모른다」를 <b>값으로</b> 갖는 것이 이 열거형의 존재 이유다. <c>bool</c> 하나로 만들면
    /// 「다른 데스크톱에 있다」와 「조회가 실패했다」가 같은 <c>false</c>로 뭉개지고, 그 순간
    /// <b>조회 실패가 캐릭터를 영원히 숨긴다</b> — 사용자에게는 「앱이 안 뜬다」로 보인다.
    /// 이 저장소가 <c>IWindowEnumerationCostSource</c>(미지원을 0이 아니라 음수로)와
    /// <c>ReservedEdgeInsets.MeasuredEdges</c>(측정된 0 ≠ 못 잰 0)에서 이미 두 번 세운 규약이다.</para>
    /// </summary>
    public enum VirtualDesktopMembership
    {
        /// <summary>조회하지 못했다. <b>「현재 데스크톱에 있다」와 같게 다룬다</b>(보수 규칙 — 숨기지 않는다).</summary>
        Unknown = 0,

        /// <summary>지금 활성인 가상 데스크톱에 있다. 평상시의 값이다.</summary>
        CurrentDesktop = 1,

        /// <summary>다른 가상 데스크톱에 있다 — 사용자는 우리를 보고 있지 않다.</summary>
        OtherDesktop = 2,
    }

    /// <summary>
    /// ★ 2026-09-05 (M-8) — <b>선택적 기능 인터페이스</b>. 우리 창의 가상 데스크톱 소속을
    /// 알려 줄 수 있는 플랫폼 서비스.
    ///
    /// ============================================================================
    /// 리더 판정 2026-09-05 — 옵션 (a) 「소속만 확인한다」를 채택했다
    /// ============================================================================
    /// 예전 <c>PlatformParityAuditTests.미해결_Windows에는_가상데스크톱_동행_배선이_없다</c>가 두 갈래를
    /// 남겨 두고 리더 판단을 기다리고 있었다:
    /// <list type="bullet">
    ///  <item><b>(a) 소속만 확인하고, 남의 데스크톱에 있는 동안 스스로 숨는다</b> —
    ///        <c>IVirtualDesktopManager::IsWindowOnCurrentVirtualDesktop</c>은 <b>공개·문서화된 COM</b>이다.
    ///        비침해 원칙 2와 같은 방향이다. <b>채택.</b></item>
    ///  <item>(b) 모든 데스크톱에 고정한다(macOS <c>.canJoinAllSpaces</c>와 같은 «결과») —
    ///        <b>비공개 API</b>가 필요하고 OS 업데이트마다 깨진다. 게다가 비문서 API가 쌓이면
    ///        백신 휴리스틱 위험이 커진다(M-10과 같은 저울: 우리 Win32 표면은 이미
    ///        <c>EnumWindows · GetAsyncKeyState · OpenProcess</c> 조합이고 사용자 실기는 V3다).
    ///        「출시 전 권장」 등급 항목에 그 위험을 살 이유가 없다. <b>기각.</b></item>
    /// </list>
    /// <b>그래서 macOS와 «결과»가 다르다</b>: macOS는 따라붙고(Spaces), Windows는 사라진다.
    /// 그 비대칭은 <b>의도된 것</b>이며, 되돌리려면 위 저울을 다시 놓아야 한다.
    ///
    /// ============================================================================
    /// 계약 — "소속"만 말한다. 무엇을 할지는 <see cref="VirtualDesktopSuspendPolicy"/>가 정한다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>읽기 전용.</b> 창을 다른 데스크톱으로 <b>옮기는</b> API
    ///        (<c>IVirtualDesktopManager</c>의 세 번째 슬롯)는 이 계약의 구현체가 절대 부르지 않는다.
    ///        우리 창이라도 마찬가지다 — 사용자가 어느 데스크톱에서 앱을 켰는지는 사용자의 결정이다.</item>
    ///  <item><b>실패는 <see cref="VirtualDesktopMembership.Unknown"/></b>이고, 소비 측은 그때
    ///        <b>숨지 않는다</b>. 오판의 대가가 비대칭이기 때문이다 — 잘못 숨으면 사용자는
    ///        「앱이 사라졌다」를 신고하고, 잘못 안 숨으면 지금까지의 동작 그대로다.</item>
    ///  <item><b>구현하지 않아도 된다.</b> macOS·모바일·테스트 스텁은 이 인터페이스를 달지 않고,
    ///        소비자는 <c>service as IVirtualDesktopMembershipSource</c>로 물어 null이면
    ///        <see cref="VirtualDesktopMembership.Unknown"/>으로 읽는다 = <b>이 기능이 없던 동작</b>.
    ///        <see cref="IForeignFullscreenTierSource"/>가 세운 관례 그대로다.</item>
    ///  <item><b>폴링 규율</b>: 소비자는 이 메서드를 <b>매 프레임 부르지 않는다</b>.
    ///        <c>StickConfig.fullscreenPollInterval</c>(기본 1.5초) 틱에 얹혀 폴링당 1회만 부른다.</item>
    /// </list>
    /// </summary>
    public interface IVirtualDesktopMembershipSource
    {
        /// <summary>지금 이 순간 우리 오버레이 창의 가상 데스크톱 소속.</summary>
        VirtualDesktopMembership GetVirtualDesktopMembership();
    }
}
