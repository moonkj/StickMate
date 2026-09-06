namespace StickMate.Platform
{
    /// <summary>
    /// ★★★ <b>「이 플랫폼에서는 어느 다리로 설 것인가」 한 줄만 정하는 분기</b> — 2026-09-06 신설.
    ///
    /// <para>2026-09-03에 <see cref="ISystemAudioActivityProbe"/>의 구현 2개가 착지했는데
    /// <b>아무도 그것을 생성하지 않았다</b>(사운드 감사 2026-09-06 실측: 프로덕션
    /// <c>new</c> 0건). 계약도 정책도 게이트도 다 있는데 <b>«어느 구현을 쓸지» 정하는 줄이 없어서</b>
    /// 기능 전체가 한 번도 실행된 적이 없다. 이 파일이 그 한 줄이다.</para>
    ///
    /// ============================================================================
    /// 왜 <c>Platform/</c> 루트인가 — 그리고 왜 <b>여기가 유일한 분기</b>여야 하는가
    /// ============================================================================
    /// <see cref="FullscreenSuspendPolicy"/> 사고의 교훈 그대로다(CLAUDE.md). 이 <c>#if</c>가
    /// <c>Platform/MacOS/</c> 안에 있으면 Windows는 물리적으로 같은 자리를 못 만들고, 그쪽 라운드가
    /// <b>다른 모양의 분기를 새로 만든다</b>. 그러면 «양쪽이 같은 계약을 쓴다»는 사실이 어디에도
    /// 적혀 있지 않게 된다.
    ///
    /// <para><b>이 기능의 플랫폼 분기는 이 파일 하나뿐이다.</b> 위쪽(<see cref="AudioReactiveDanceRunner"/>,
    /// <see cref="AudioReactiveDanceDirector"/>, <see cref="AudioReactiveDancePolicy"/>,
    /// <c>Core/AudioReactiveDanceGate</c>)에는 <c>#if</c>가 한 개도 없다 — 그래서 Windows가 없는 이
    /// 개발 머신의 EditMode가 <b>양 플랫폼이 실제로 돌릴 규칙</b>을 그대로 검증한다.</para>
    ///
    /// ============================================================================
    /// ★★ <c>&amp;&amp; !UNITY_EDITOR</c> — 실수가 아니라 <c>StickmanAgent.CreatePlatformService</c>와 같은 이유
    /// ============================================================================
    /// Unity 에디터는 활성 빌드 타깃의 <c>UNITY_STANDALONE_*</c> 심볼을 <b>에디터 컴파일 컨텍스트에도
    /// 함께 정의한다</b>(그 파일에 실측 근거가 적혀 있다 — <c>UNITY_EDITOR</c>와 배타가 아니다).
    /// 가드가 없으면 <b>에디터 Play와 배치모드 테스트에서 진짜 CoreAudio/WASAPI가 돈다.</b>
    ///
    /// <para>그것이 왜 나쁜가: 이 저장소의 PlayMode 검증은 전부 배치모드로 돌고, 그 순간
    /// <b>«개발자가 그때 음악을 틀어 놨는가»가 테스트 결과를 바꾼다.</b> 실패한 측정과 성공한 측정이
    /// 똑같이 생기는 형태 그 자체다. 그래서 에디터에서는 <c>null</c>(=배선 없음)로 두고,
    /// 검증이 필요하면 <see cref="AudioReactiveDanceDirector.RunStartup"/>에 <b>가짜 프로브를
    /// 주입</b>한다(<see cref="ReservedBarRevealDirector.RunStartup"/>와 같은 관례).</para>
    ///
    /// ============================================================================
    /// 모바일에는 구현이 없다 — <b>숨기지 않는다</b>
    /// ============================================================================
    /// <c>Platform/Mobile/</c>에는 이 계약의 구현이 <b>없고</b>, 그래서 iPad/iPhone
    /// 「스크린샷 백드롭 모드」에서 음악 반응 춤은 <b>없는 기능</b>이다. CoreAudio의 장치 상태
    /// 조회와 WASAPI 피크 미터는 양쪽 다 데스크톱 전용이고, iOS 샌드박스에서 <b>다른 앱의 오디오를
    /// 관측하는 것 자체가 밖</b>일 가능성이 높다(별도 배정 — <c>PlatformParityAuditTests</c> U-6).
    /// 여기서 <c>null</c>이 나오면 게이트는 <b>영원히 닫힌 채</b>이고, 그것이 설계된 동작이다.
    /// </summary>
    public static class SystemAudioActivityProbeFactory
    {
        /// <summary>프로브가 없을 때 진단 로그에 찍는 태그. <see cref="ISystemAudioActivityProbe.PlatformTag"/>와
        /// 같은 자리에 들어가므로 «어느 다리로 서 있었는가»가 로그 한 줄에서 끝난다.</summary>
        public const string NoProbeTag = "(없음 — 이 플랫폼/에디터에는 시스템 오디오 감지 배선이 없습니다)";

        /// <summary>
        /// 이 빌드에서 쓸 프로브를 만든다. <b>배선이 없으면 <c>null</c></b>이고, 그것은 결함이 아니라
        /// 「이 플랫폼에는 없는 기능」이라는 정직한 신호다.
        ///
        /// <para>★ <c>null</c>을 «무음»으로 접지 마라. 위쪽(<see cref="AudioReactiveDanceRunner"/>)은
        /// 이 값이 <c>null</c>이면 <see cref="AudioReactiveDancePolicy.Evaluate"/>를 <b>아예 부르지
        /// 않는다</b> — 부르면 가짜 <c>offRun</c>이 쌓여 T₂·Lockout 해제가 «실제로 일어나지 않은
        /// 침묵» 위에서 성립한다(그 정책 문서 3절).</para>
        /// </summary>
        public static ISystemAudioActivityProbe Create()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return new StickMate.Platform.Windows.WindowsSystemAudioActivityProbe();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            return new StickMate.Platform.MacOS.MacSystemAudioActivityProbe();
#else
            // 에디터 / 모바일 / 그 외 조합. 위 클래스 문서의 두 문단이 사유다.
            // 여기서 «대충 false를 돌려주는 더미»를 만들지 않는 것이 핵심이다 —
            // 그런 더미는 «영원히 무음»을 정상으로 보이게 만들고, 그 실패는 조용한 초록이다.
            return null;
#endif
        }
    }
}
