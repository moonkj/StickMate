namespace StickMate.Core
{
    /// <summary>명령 하나가 지금 실행될 수 있는가 — 세 가지뿐이다.</summary>
    public enum CommandReadyState
    {
        /// <summary>지금 누르면 실제로 실행된다.</summary>
        Ready,

        /// <summary>조건이 안 맞는다. <see cref="CommandAvailability.Reason"/>에 <b>사용자가 읽을</b> 이유가 있다.</summary>
        Blocked,

        /// <summary>이 빌드/씬에 그 Director 자체가 없다(배선 누락). 숨기지 않고 이유를 말한다.</summary>
        Missing,
    }

    /// <summary>
    /// ★ docs/UX_FLOW.md <b>36-7 절대 규칙</b>의 강제 장치 —
    /// <b>"미리 회색으로 만든 판단과 실제 실행 판단은 같은 함수 하나에서 나와야 한다."</b>
    ///
    /// ============================================================================
    /// 왜 이 타입이 필요한가 (실측한 사실)
    /// ============================================================================
    /// 각 Director의 <c>ForceTriggerNow</c>는 지금까지 전부 <c>void</c>였고, ① 배선 없음
    /// ② <see cref="SpectacleEventLock.IsActive"/> ③ 상태가 Idle/Walk 아님 ④ 자리/대상 없음 —
    /// <b>네 가지 이유로 조용히 아무것도 하지 않고 <c>Debug.Log</c>만 남겼다</b>. 단축키만 있을 때는
    /// 로그로 충분했지만, 사용자 UI 버튼을 그 위에 얹으면 "눌렀는데 아무 일도 안 일어나고 이유도 없는
    /// 버튼"이 되어 그 자체가 원칙 1 위반이 된다.
    ///
    /// 회색 처리용 게이트를 <b>창이 따로 구현하면 그 순간 진실이 두 벌</b>이 된다(이 프로젝트가 이미
    /// 밟은 함정: Dock 구간 이중 계산, 캐릭터 치수 이중 정의). 그래서 판정은 Director의
    /// <c>GetAvailability()</c> 하나에만 있고, <c>ForceTriggerNow</c>는 <b>내부에서 그것을 호출</b>한다.
    /// 창은 같은 함수를 폴링해 회색 여부와 이유 문구를 그린다.
    ///
    /// ============================================================================
    /// <see cref="Reason"/>은 반드시 <b>미리 만들어진</b> 문자열이어야 한다
    /// ============================================================================
    /// 이 값은 창이 열려 있는 동안 0.25초마다 6개 타일에 대해 다시 계산된다. 이유 문구를 그때마다
    /// 문자열 보간으로 만들면 초당 24개의 쓰레기가 생긴다 — 하루 종일 켜져 있는 앱에서는 그게 곧 GC다.
    /// <see cref="StickMateDisplayNames"/>가 enum별 완성 문장을 미리 만들어 두는 이유가 이것이다.
    /// </summary>
    public readonly struct CommandAvailability
    {
        public readonly CommandReadyState State;

        /// <summary>불가/부재일 때 <b>사용자에게 그대로 보여줄</b> 한 줄. Ready면 null.</summary>
        public readonly string Reason;

        private CommandAvailability(CommandReadyState state, string reason)
        {
            State = state;
            Reason = reason;
        }

        public bool IsReady => State == CommandReadyState.Ready;

        /// <summary>씬에 Director가 없을 때. 36-7: 칸을 <b>숨기지 않는다</b> — 어제 있던 칸이 사라지면
        /// 사용자는 자기 잘못을 의심한다.</summary>
        public const string MissingReason = "이 빌드에는 없는 기능이에요";

        public static readonly CommandAvailability Ready = new CommandAvailability(CommandReadyState.Ready, null);

        public static readonly CommandAvailability Missing =
            new CommandAvailability(CommandReadyState.Missing, MissingReason);

        /// <param name="reason">사용자가 읽을 한 줄. 코드 분기와 1:1이어야 한다(36-7 표).</param>
        public static CommandAvailability Blocked(string reason)
            => new CommandAvailability(CommandReadyState.Blocked, string.IsNullOrEmpty(reason) ? "지금은 못 해요" : reason);
    }
}
