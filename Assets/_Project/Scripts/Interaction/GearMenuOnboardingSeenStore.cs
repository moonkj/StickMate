using System;

namespace StickMate.Interaction
{
    /// <summary>
    /// <see cref="IGearMenuOnboardingSeenStore"/>의 현재 인스턴스를 쥔 자리. 위젯은 여기서만 저장소를 얻는다.
    ///
    /// <para><b>왜 정적인가.</b> 위젯은 씬이 만들고, 테스트 스위트는 씬이 뜨기 <b>전에</b> 한 번 저장소를 갈아
    /// 끼워야 한다(<c>[SetUpFixture]</c>). 생성자 주입으로는 그 시점에 닿을 수 없다.
    /// 같은 이유로 <c>CharacterSaveStore</c>도 경로 재지정을 정적으로 연다.</para>
    ///
    /// <para><b>기본값은 실제 저장소다.</b> 메모리 구현이 기본이면 출하본 사용자에게 안내가 <b>매 실행</b> 뜬다
    /// (원칙 2 위반). 반대로 테스트가 주입을 빠뜨리면 실제 저장소로 떨어진다 — 그 누락은
    /// <see cref="PlayerPrefsGearMenuOnboardingSeenStore.ReadCount"/>·<see cref="PlayerPrefsGearMenuOnboardingSeenStore.WriteCount"/>를
    /// 스위트 끝에 0으로 단언해 드러낸다(조용히 쓰는 대신 시끄럽게 빨개진다).</para>
    /// </summary>
    public static class GearMenuOnboardingSeenStore
    {
        private static IGearMenuOnboardingSeenStore s_default;
        private static IGearMenuOnboardingSeenStore s_testOverride;

        /// <summary>지금 쓰는 저장소. 테스트 주입이 없으면 실제 저장소(처음 부를 때 한 번 만든다 — 만들기만으로는
        /// 설정 저장소에 닿지 않는다).</summary>
        public static IGearMenuOnboardingSeenStore Current
            => s_testOverride ?? (s_default ??= new PlayerPrefsGearMenuOnboardingSeenStore());

        /// <summary>테스트 주입이 걸려 있는가.</summary>
        public static bool IsOverriddenForTesting => s_testOverride != null;

        /// <summary>테스트 전용 — 저장소를 갈아 끼우고 <b>직전 주입</b>(없으면 <c>null</c>)을 돌려준다.
        /// 돌려받은 값을 <see cref="RestoreForTesting"/>에 넘기면 스위트 격리를 깨지 않고 원래대로 돌아간다.</summary>
        public static IGearMenuOnboardingSeenStore UseForTesting(IGearMenuOnboardingSeenStore store)
        {
            // null을 받아 주면 「주입했다」고 믿은 채 실제 저장소로 떨어진다.
            if (store == null) throw new ArgumentNullException(nameof(store));
            IGearMenuOnboardingSeenStore previous = s_testOverride;
            s_testOverride = store;
            return previous;
        }

        /// <summary>테스트 전용 — <see cref="UseForTesting"/>가 돌려준 직전 주입으로 되돌린다(<c>null</c>이면 주입 해제).</summary>
        public static void RestoreForTesting(IGearMenuOnboardingSeenStore previousOverride)
            => s_testOverride = previousOverride;

        /// <summary>테스트 전용 — 주입을 걷는다. 정적 상태는 도메인을 넘어 살아남으므로 스위트 끝에 부른다.</summary>
        public static void ResetForTesting() => s_testOverride = null;
    }
}
