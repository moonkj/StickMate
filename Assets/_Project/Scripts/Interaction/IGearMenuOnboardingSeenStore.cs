namespace StickMate.Interaction
{
    /// <summary>
    /// 톱니 부채꼴 「최초 1회 안내를 이미 봤다」 한 비트의 저장소 경계.
    ///
    /// <para><b>왜 경계인가.</b> 이 비트는 프로덕션 <see cref="GearRadialMenuWidget.Expand"/> 한가운데서 읽고
    /// 쓴다. 경계가 없을 때는 PlayMode 테스트가 부채꼴을 <b>열기만 해도</b> 개발자 기계의 실제 설정 저장소
    /// (macOS plist · Windows 레지스트리)를 읽고 1을 적었다. 픽스처 하나를 감싸는 「기억했다 되돌리기」로는
    /// 막을 수 없었다 — 먼저 돈 다른 픽스처가 오염시킨 값을 「원래 값」으로 기억했기 때문이다
    /// (docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md G-1).</para>
    ///
    /// <para>출하본의 구현은 <see cref="PlayerPrefsGearMenuOnboardingSeenStore"/> 하나이고, 테스트 스위트는
    /// <see cref="GearMenuOnboardingSeenStore.UseForTesting"/>로 <see cref="InMemoryGearMenuOnboardingSeenStore"/>를
    /// 넣는다(Tests/PlayMode/GlobalPlayModeTestIsolation).</para>
    ///
    /// <para>「봤음을 지운다」는 멤버가 <b>없는 것은 일부러다</b>. 제품 경로에 되돌리는 코드가 없고, 테스트는
    /// 원하는 초기값으로 새 메모리 저장소를 만들면 된다 — 실제 저장소에 지우기 능력을 줄 이유가 없다.</para>
    /// </summary>
    public interface IGearMenuOnboardingSeenStore
    {
        /// <summary>이 저장소에 「봤음」이 기록돼 있는가.</summary>
        bool IsSeen { get; }

        /// <summary>「봤음」을 기록한다. 이미 봤어도 다시 불러도 된다(같은 값을 쓴다).</summary>
        void MarkSeen();
    }
}
