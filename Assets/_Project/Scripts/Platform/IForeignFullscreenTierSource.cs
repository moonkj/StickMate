namespace StickMate.Platform
{
    /// <summary>
    /// <b>선택적 기능 인터페이스</b> — "남의 전체화면 앱이 떴을 때 우리가 물러나는 정도"를
    /// <see cref="ForeignFullscreenTier"/> 등급으로 알려 줄 수 있는 플랫폼 서비스.
    ///
    /// ============================================================================
    /// 왜 <see cref="IPlatformWindowService"/>에 직접 넣지 않았는가 (2026-09-02, 실측으로 결정)
    /// ============================================================================
    /// 처음에는 그 인터페이스에 메서드를 하나 더했다. 그러자 <b>테스트 스텁 32개</b>가 전부
    /// <c>CS0535</c>로 깨졌다(<c>TestFootholdService</c>, <c>StubDockService</c>, … PlayMode 전반).
    /// 그 32개는 전부 발판·Dock 기하를 재는 스텁이라 전체화면 판정과 아무 관계가 없다 —
    /// 관계없는 파일 32개를 고치게 만드는 계약 변경은 계약이 잘못된 것이다.
    ///
    /// <para>이 저장소에는 이미 같은 문제를 푸는 <b>확립된 형태</b>가 있다:
    /// <see cref="ICursorPositionService"/> · <see cref="IReservedBottomBarService"/> ·
    /// <see cref="IReservedTopBarService"/> · <see cref="ILocalClickCaptureService"/> …
    /// 전부 <b>필요한 구현체만 추가로 붙이는 능력(capability) 인터페이스</b>이고, 소비자는
    /// <c>service as IXxx</c>로 물어본다. 이 파일은 그 관례를 그대로 따른다.</para>
    ///
    /// <para><b>구현하지 않아도 된다.</b> 구현하지 않은 서비스(Null·모바일·테스트 스텁)에 대해
    /// 소비자는 기존 <see cref="IPlatformWindowService.IsFullscreenAppActive"/> 하나로 강등한다:
    /// <c>true → Full</c> / <c>false → None</c>. 즉 <b>등급 1이 없던 예전 동작</b>과 정확히 같다.
    /// 이것이 이 설계의 안전판이다 — 새 계약을 모르는 코드는 예전 동작을 그대로 유지한다.</para>
    ///
    /// <para><b>구현 계약</b>: 이 메서드가 <b>원본</b>이다. 네이티브 조회는 여기서 1회만 하고
    /// <c>IsFullscreenAppActive()</c>는 그 결과에서 유도한다 — 두 메서드가 각각 조회하면
    /// 24시간 상주 앱의 폴링 비용이 그대로 두 배가 된다.</para>
    /// </summary>
    public interface IForeignFullscreenTierSource
    {
        /// <summary>이번 폴링의 확정 등급(디바운스 이후).</summary>
        ForeignFullscreenTier GetForeignFullscreenTier();
    }
}
