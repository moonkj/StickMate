namespace StickMate.Platform
{
    /// <summary>
    /// <see cref="FootholdPoller"/>가 "지금 창을 얼마나 넓게 봐야 하는가"를 판단하려면 알아야 하는
    /// <b>캐릭터 쪽 사실</b>들. 값 타입이라 매 프레임 조회에 할당이 없다.
    ///
    /// <para>여기에 <c>StickmanStateId</c>를 그대로 넣지 않고 bool로 풀어 놓은 이유: 이 구조체는
    /// Platform 계층에 있고, 판정 본체인 <see cref="FootholdScanPolicy"/>는 UnityEngine도 States도
    /// 모르는 순수 규칙으로 남아야 개발 머신에서 그대로 실행 검증할 수 있기 때문이다.</para>
    /// </summary>
    public struct FootholdScanContext
    {
        /// <summary>이 값이 false면 폴러는 <b>가장 안전한 쪽</b>(항상 전체 스캔 = 옛 거동)으로 간다.</summary>
        public bool Valid;

        /// <summary>지금 딛고 있는 발판 핸들. 0 = 접지 안 함, 음수 = 합성 발판(Dock/화면 바닥 안전망).</summary>
        public long StandingFootholdHandle;

        /// <summary>유저가 캐릭터를 붙잡고 있다(<c>Dragged</c>) — 곧 어디로든 던져질 수 있다.</summary>
        public bool CharacterGrabbed;

        /// <summary>공중에 있다(<c>Fall</c>/<c>ThrowTumble</c>/<c>Jump</c>/<c>Ragdoll</c> 등).</summary>
        public bool CharacterAirborne;

        /// <summary>오래 정지해 있다 — <c>FramePacing</c>의 등급 판정을 <b>그대로 재사용</b>한다.</summary>
        public bool CharacterStill;

        /// <summary>캐릭터 발의 OS 화면 좌표(포인트). "근처" 판정의 중심이다.</summary>
        public UnityEngine.Vector2 CharacterOsScreen;

        /// <summary>보행 속도를 OS 포인트/초로 환산한 값. "근처" 반경 유도의 입력이다.</summary>
        public float WalkSpeedOsPxPerSecond;
    }

    /// <summary>
    /// 위 사실을 폴러에 넘겨 주는 창구. <c>States/StickmanBlackboard</c>가 구현한다.
    ///
    /// <para>배선하지 않으면(= null) 폴러는 <b>이 라운드 이전과 완전히 같은</b> 주기 폴링으로
    /// 동작한다. 그래서 기존 PlayMode 테스트 20여 곳은 한 줄도 고치지 않아도 된다 —
    /// 새 경로를 켜는 것은 <c>StickmanAgent</c>가 실제로 배선할 때뿐이다.</para>
    /// </summary>
    public interface IFootholdScanContextSource
    {
        bool TryGetFootholdScanContext(out FootholdScanContext context);
    }
}
