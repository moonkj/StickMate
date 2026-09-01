namespace StickMate.Platform
{
    /// <summary>
    /// 오버레이 창이 "항상 위(topmost)"를 잃었는지 감시하고, 언제 재적용하고 언제 로그를 남길지
    /// 결정하는 <b>순수 규칙</b>. P/Invoke가 한 줄도 없어야 한다 — 그래야 Windows 실기가 없는
    /// 개발 머신의 EditMode에서 규칙 자체를 검증할 수 있다(FullscreenGameCategory와 같은 설계).
    ///
    /// ============================================================================
    /// 왜 생겼는가 (2026-09-01, 같은 버그 3번째 신고 — 앞선 수정 2회가 전부 엉뚱한 곳이었다)
    /// ============================================================================
    /// 사용자 신고 원문: "엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림
    /// <b>화면 뒤로 넘어 가는 거 같음</b>" → 확인 사살: "자동숨김이 아니라 창뒤로 넘어가는거야".
    ///
    /// 즉 <b>숨김 로직이 아니라 z-order</b> 문제였다. 그런데 Windows에는 topmost를 다시 거는 경로가
    /// 기동 직후 몇 초를 빼면 <b>존재하지 않았다</b>:
    ///   · <c>WindowsOverlayStateEnforcer</c>의 재적용 루프는 <c>ReapplyAttempts(5) x 0.5초 = 2.5초</c>로
    ///     상한이 걸려 있고, 그 뒤에는 <c>MarkDirty()</c>가 불리지 않는 한 영원히 돌지 않는다.
    ///     <c>MarkDirty()</c>를 부르는 곳은 기동 시 1회뿐이다(StickmanAgent.Start의 SetAlwaysOnTop).
    ///   · macOS에는 같은 계열의 상시 감시(<c>MacOverlayStateEnforcer.TickAllSpacesBehavior</c>, 2초)가
    ///     있는데 Windows에는 대응물이 없었다 — 이 프로젝트가 반복해 겪은 "한쪽 플랫폼에만 있는 방어".
    ///
    /// ============================================================================
    /// "이미 topmost다"를 라이브러리에 묻지 않는 이유 (실측으로 확정, 추측 아님)
    /// ============================================================================
    /// <c>UniWindowController.isTopmost</c> 게터는 <c>UniWinCore.IsTopmost</c>를 읽고, 그 프로퍼티의
    /// 실제 구현은 <c>UniWinCore.cs</c>에서
    /// <code>public bool IsTopmost { get { return (IsActive &amp;&amp; _isTopmost); } }</code>
    /// 즉 <b>C# 캐시 필드</b>다. 네이티브 되읽기용 <c>LibUniWinC.IsTopmost()</c> extern은 같은 파일
    /// 78번째 줄에 <b>선언만 되어 있고 패키지 어디에서도 호출되지 않는다</b>(전체 검색으로 확인).
    /// 그래서 OS가 우리 창을 강등시켜도 캐시는 계속 true이고, "이미 목표값이니 건너뛴다"는 판단이
    /// 영원히 참이 된다. 실제 진실은 <c>GetWindowLong(GWL_EXSTYLE) &amp; WS_EX_TOPMOST</c>뿐이며,
    /// 이 규칙은 그 실측값만 입력으로 받는다.
    /// </summary>
    public static class TopmostRestorePolicy
    {
        /// <summary>
        /// 지금 topmost를 다시 걸어야 하는가.
        /// </summary>
        /// <param name="desiredTopmost">우리가 원하는 상태. false면 감시 자체를 하지 않는다.</param>
        /// <param name="osTopmostAlive"><c>GetWindowLong(GWL_EXSTYLE) &amp; WS_EX_TOPMOST</c> 실측값.</param>
        /// <param name="suspended">전체화면 게임 감지로 캐릭터를 숨긴 상태인가. 이때는 재적용하지 않는다 —
        /// 원칙 2(비침해)상 우리는 게임 위로 올라갈 이유가 없고, 독점 전체화면 앱과 z-order를 두고
        /// 다투면 그쪽 화면이 깜빡이는 실害만 남는다.</param>
        public static bool ShouldReassert(bool desiredTopmost, bool osTopmostAlive, bool suspended)
        {
            if (!desiredTopmost) return false;
            if (suspended) return false;
            return !osTopmostAlive;
        }
    }

    /// <summary>이번 관측에서 로그로 남길 가치가 있는 사건. <b>None이면 절대 로그하지 않는다</b> —
    /// 24시간 상주 앱이라 "변화 없음"을 찍는 순간 Player.log가 쓸모없어진다.</summary>
    public enum TopmostWatchEvent
    {
        /// <summary>변화 없음 — 로그 금지.</summary>
        None = 0,

        /// <summary>강등을 처음 발견했고, 같은 틱의 재적용으로 <b>되돌리는 데 성공</b>했다.
        /// 정상 동작 시 사용자가 보게 될 유일한 z-order 로그다.</summary>
        DemotedAndRestored,

        /// <summary>강등을 처음 발견했지만 같은 틱의 재적용으로도 <b>되돌리지 못했다</b>.
        /// 이 줄이 찍히면 라이브러리의 SetTopmost 경로 자체가 안 먹는다는 뜻이라 원인이 완전히 다르다.</summary>
        Demoted,

        /// <summary>이전 틱까지 강등 상태였다가 이번에 복구됐다(재적용이 한 박자 늦게 먹은 경우).
        /// 강등이 지속된 시간이 함께 보고되므로 "되돌아오긴 하는데 눈에 보일 만큼 느리다"를 가른다.</summary>
        Restored,

        /// <summary>topmost는 정상인데 전경 창이 바뀌었다. 강등이 <b>안 일어났다</b>는 증거로 쓰인다 —
        /// 이 줄만 있고 Demoted 계열이 없는데도 캐릭터가 가려진다면 원인은 z-order가 아니다.</summary>
        ForegroundChanged,
    }

    /// <summary>
    /// topmost 실측값과 전경 창 핸들의 <b>전이만</b> 골라내는 상태 추적기(순수 계산).
    /// 폴링은 초당 여러 번 돌지만 이 추적기를 통과하는 사건은 사람이 창을 전환할 때뿐이다.
    /// </summary>
    public struct TopmostWatchdogTracker
    {
        private bool _initialized;
        private bool _alive;
        private long _foreground;
        private double _demotedAtSeconds;
        private int _demotionCount;

        /// <summary>지금까지 관측된 강등 횟수(누적). 로그에 함께 찍어 재발 빈도를 사용자가 세지 않아도 되게 한다.</summary>
        public int DemotionCount => _demotionCount;

        /// <summary>
        /// 이번 폴링 결과를 넣고 로그할 사건을 받는다.
        /// </summary>
        /// <param name="desiredTopmost">우리가 원하는 상태.</param>
        /// <param name="aliveBefore">재적용 <b>전</b> 실측 WS_EX_TOPMOST 생존 여부.</param>
        /// <param name="aliveAfter">재적용 <b>후</b> 실측값(재적용을 안 했으면 aliveBefore와 같은 값).</param>
        /// <param name="foregroundHandle">지금 전경 창 핸들.</param>
        /// <param name="nowSeconds">단조 증가 시계(초).</param>
        /// <param name="demotedForSeconds">강등이 지속된 시간. Restored에서만 의미가 있다.</param>
        public TopmostWatchEvent Observe(
            bool desiredTopmost, bool aliveBefore, bool aliveAfter,
            long foregroundHandle, double nowSeconds, out double demotedForSeconds)
        {
            demotedForSeconds = 0.0;

            // 항상위를 원하지 않는 구간(기동 직후 등)에서는 기준선만 새로 잡고 아무 것도 보고하지 않는다.
            if (!desiredTopmost)
            {
                _initialized = false;
                return TopmostWatchEvent.None;
            }

            if (!_initialized)
            {
                _initialized = true;
                _alive = aliveAfter;
                _foreground = foregroundHandle;
                return TopmostWatchEvent.None;
            }

            bool foregroundChanged = foregroundHandle != _foreground;
            _foreground = foregroundHandle;

            if (!aliveBefore)
            {
                if (_alive)
                {
                    // 이번 관측에서 "처음" 발견한 강등.
                    _demotionCount++;
                    _demotedAtSeconds = nowSeconds;
                    _alive = aliveAfter;
                    return aliveAfter ? TopmostWatchEvent.DemotedAndRestored : TopmostWatchEvent.Demoted;
                }

                // 이미 강등을 알고 있었다. 복구되지 않았다면 매 틱 반복해 찍지 않는다(상주 앱).
                if (!aliveAfter) return TopmostWatchEvent.None;

                _alive = true;
                demotedForSeconds = nowSeconds - _demotedAtSeconds;
                return TopmostWatchEvent.Restored;
            }

            if (!_alive)
            {
                // 우리가 재적용하지 않았는데도 살아났다(OS/라이브러리가 되돌린 경우).
                _alive = true;
                demotedForSeconds = nowSeconds - _demotedAtSeconds;
                return TopmostWatchEvent.Restored;
            }

            return foregroundChanged ? TopmostWatchEvent.ForegroundChanged : TopmostWatchEvent.None;
        }
    }
}
