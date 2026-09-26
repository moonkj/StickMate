using System;

namespace StickMate.Platform
{
    /// <summary>
    /// 게임바 등록 목록(<c>HKCU\System\GameConfigStore\Children</c>) 조회 결과의 <b>성격</b>.
    /// 값이 아니라 "다시 물어야 하는가"를 말한다.
    ///
    /// <para>★ <see cref="Retryable"/>이 <b>0(기본값)</b>인 것은 의도다. 한 번도 읽지 않은 상태
    /// (<c>default</c>)에서 첫 폴링이 <b>반드시</b> 조회해야 하기 때문이다. 기본값이 「유지」 계열이면
    /// 첫 폴링이 조회를 건너뛰고 빈 목록으로 판정해, 게임을 30초 동안 놓친다.</para>
    /// </summary>
    public enum GameListReadOutcome
    {
        /// <summary>다시 물어야 하는 실패 — 키 열기의 그 밖 오류, rc 0인데 핸들 0, 하위 키 열거 오류, 예외.</summary>
        Retryable = 0,

        /// <summary>목록을 끝까지 읽었다.</summary>
        Success,

        /// <summary>키가 없다(게임 바를 한 번도 쓰지 않은 계정). 다시 물어도 결과가 같다.</summary>
        KeyMissing,

        /// <summary>하위 키 상한에 걸려 열거를 중단했다. 항목 수가 그대로면 매번 같은 결과다.</summary>
        SubkeyLimitReached,
    }

    /// <summary>
    /// ============================================================================
    /// FC-1 — 게임 판정 조회 <b>실패를 캐시하지 않는다</b>는 규칙의 순수 함수부 (N-23, 2026-09-26)
    /// ============================================================================
    /// 설계 정본은 <c>docs/platform/GAME_DETECTION_FAILURE_CACHE.md</c>의 「4-1. ★ FC-1 확장 설계」이고,
    /// 이 파일은 그 설계에서 <b>플랫폼과 무관한 판정</b>만 떼어낸 것이다. P/Invoke가 한 줄도 없어야 한다.
    ///
    /// <para><b>무엇이 문제였나.</b> 조회(전경 exe 경로 · 게임바 목록)가 실패하면 그 결과가
    /// 「게임 아님」으로 <b>30초 캐시</b>되어, 이미 감지된 게임 위에서 캐릭터가 약 30초 동안 다시
    /// 나왔다(범위 30.0–31.5초, 원칙 2 조건부 위반). 폴링 1.5초 · 실패 캐시 30초 · 디바운서 1.0초의
    /// 합성이다. 실패를 캐시하지 않고 <b>다음 폴링에 다시 물으면</b> 한 폴링짜리 실패는 기존 1.0초
    /// 디바운서가 흡수한다.</para>
    ///
    /// <para><b>왜 「시간 비교」가 아니라 「다음 폴링」인가</b>(리더 판정). 재시도를
    /// <c>지금 - 마지막 시도 &gt;= N</c>으로 구현하면 N을 폴링 간격과 같게 두어도 경계에서 흡수에
    /// 실패한다 — 폴링 시각은 float 타이머가 정하고 비교는 double 실시간으로 하므로, 실제 경과가
    /// 1.5초에 조금 못 미치는 폴링에서 재시도가 한 폴링 밀리고 두 번째 실패 관측이 디바운서를
    /// 확정시켜 노출 3.0초가 난다. 그래서 시각을 비교하지 않고 <b>연속 실패 횟수</b>만 센다.</para>
    ///
    /// <para><b>왜 이 파일이 <c>Platform/Windows/</c>가 아니라 <c>Platform/</c>에 있는가.</b> 정책이
    /// 플랫폼 폴더에 있으면 반대편 플랫폼이 물리적으로 부를 수 없다(이 저장소가
    /// <c>FullscreenSuspendPolicy</c>에서 실제로 겪은 사고). 게다가 Windows 전용 파일은 이 개발
    /// 머신에서 <b>한 번도 컴파일되지 않으므로</b>, 규칙이 여기 있어야 EditMode로 잴 수 있다.
    /// macOS 대응(<c>MacWindowService.QueryAppCategory</c>)은 같은 규칙을 그대로 부르면 된다 —
    /// 이번 라운드 범위 밖이고(N-8 A · E-3 뒤), 그때 이 파일은 한 줄도 바뀌지 않는다.</para>
    /// </summary>
    public static class GameVerdictRetryPolicy
    {
        /// <summary>연속 실패가 이 횟수면 후퇴한다(K). 지속 실패에서 무한 재시도를 막는 상한이고,
        /// 이 값이 곧 <b>후퇴 창당 조회 수</b>다 — 지속 실패 1시간 조회 수는 현행 120회의 3배인
        /// 360회가 된다(<c>GameVerdictRetryPolicyTests</c>가 계수로 잠근다).</summary>
        public const int ConsecutiveFailureBackoffThreshold = 3;

        /// <summary>후퇴 창의 길이. ★ 창은 <b>연속 실패의 첫 시도</b> 시각부터 잰다 —
        /// 마지막 시도부터 재면 세 폴링짜리 일시 실패의 노출이 약 30초에서 약 33초로 늘어난다.</summary>
        public const double BackoffWindowSeconds = 30.0;

        /// <summary><c>RegOpenKeyExW</c> 성공 코드(<c>ERROR_SUCCESS</c>).</summary>
        public const int RegistryOpenSuccessResultCode = 0;

        /// <summary>
        /// 「키가 없다」를 뜻하는 결과 코드 — <c>ERROR_FILE_NOT_FOUND</c> = 2 (0x2),
        /// 1차 문서 System Error Codes (0-499)의 「The system cannot find the file specified.」
        ///
        /// <para>근거의 세기를 정직하게 적어 둔다: <c>RegOpenKeyExW</c> 문서의 반환값 절은 실패 시
        /// 「a nonzero error code defined in Winerror.h」라고만 하고, 키가 없을 때 이 코드가 온다는 것은
        /// <b>같은 문서 예제 코드</b>(이 코드일 때 「Key not found.」를 찍는 분기)에서 온다. 그래서
        /// 이 값에 기대는 것은 「30초 더 기다린다」 하나뿐이고, 틀려도 조회가 한 창 늦어질 뿐
        /// 판정이 뒤집히지는 않는다.</para>
        /// </summary>
        public const int RegistryKeyMissingResultCode = 2;

        /// <summary>
        /// 키 열기 결과 → 성격. <b>두 갈래다</b>(결과는 둘이고, 입력 경우는 셋이다).
        /// <list type="bullet">
        ///  <item>결과 코드가 <see cref="RegistryKeyMissingResultCode"/> → <see cref="GameListReadOutcome.KeyMissing"/>(30초 유지)</item>
        ///  <item>그 밖 전부 → <see cref="GameListReadOutcome.Retryable"/></item>
        /// </list>
        ///
        /// <para>★ <b>「성공 코드인데 핸들이 0」도 여기서 자동으로 재시도 대상이 된다</b> —
        /// 그 코드는 <see cref="RegistryKeyMissingResultCode"/>가 아니기 때문이다. 문서는 성공하면
        /// 핸들을 받는다고만 적고 그 조합을 설명하지 않으므로 「키 없음」의 근거가 될 수 없다
        /// (발생 조건 미확인). 예전 판은 그 경우를 <c>handleIsNull</c> 인자로 따로 분기했는데,
        /// <b>다음 줄이 같은 값을 돌려줘서 인자가 결과를 한 번도 바꾸지 않았다</b> — 기계적으로
        /// 죽은 분기였고 지워도 테스트가 초록이었다(verify-change 2026-09-26 적발). 동작은 그대로
        /// 두고 분기와 인자를 걷어 「서술과 코드가 같은 갈래 수를 말하게」 했다. 호출자가 핸들을
        /// 확인하는 것 자체는 여전히 필요하다 — 그건 <b>열렸는지</b>의 판정이고 여기 규칙이 아니다.</para>
        /// </summary>
        public static GameListReadOutcome ClassifyKeyOpenResult(int resultCode)
        {
            if (resultCode == RegistryKeyMissingResultCode) return GameListReadOutcome.KeyMissing;
            return GameListReadOutcome.Retryable;
        }

        /// <summary>
        /// 이 결과를 <b>다시 묻지 않고</b> 그대로 유지해도 되는가(= 30초 캐시 대상인가).
        /// 「결정적이라 다시 물어도 같은 답」인 것만 참이다.
        /// </summary>
        public static bool HoldsWithoutRetry(GameListReadOutcome outcome)
        {
            switch (outcome)
            {
                case GameListReadOutcome.Success:
                case GameListReadOutcome.KeyMissing:
                case GameListReadOutcome.SubkeyLimitReached:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 이번 판정을 캐시에 넣어도 되는가 — <b>확정 판정만</b> 저장한다.
        /// 확정 = 전경 실행 파일 경로를 읽었고, 목록 상태가 「유지」 계열이다.
        ///
        /// <para>경로 조회 실패나 재시도 대상 목록 실패에서 나온 <c>false</c>를 저장하면 그 실패가
        /// 30초 굳어 <b>감지된 게임 위에서 캐릭터가 다시 나온다</b>(N-23의 본체).</para>
        /// </summary>
        public static bool ShouldCacheVerdict(bool executablePathResolved, GameListReadOutcome listOutcome)
            => executablePathResolved && HoldsWithoutRetry(listOutcome);
    }

    /// <summary>
    /// 연속 실패 계수와 후퇴 창을 들고 있는 상태. 조회 한 갈래(경로 / 목록)마다 하나씩 쓴다.
    ///
    /// <para>★ <b>왜 struct가 아니라 class인가.</b> <see cref="WallClockIntervalGate"/>가 같은 함정에서
    /// 값 타입을 버렸다 — 값 타입이면 <c>readonly</c> 필드에 담는 순간 C#이 <b>호출마다 복사본</b>에
    /// 메서드를 불러 상태가 저장되지 않고, 문이 매번 열린다. 컴파일 경고도 테스트 실패도 없다.
    /// 후퇴는 "상태가 남아야" 성립하므로 함정 자체를 없앤다.</para>
    ///
    /// <para><b>범위(scope)</b>: 경로 실패의 후퇴는 <b>pid별</b>이다 —
    /// <see cref="ResetIfScopeChanged"/>에 전경 pid를 넘기면 pid가 바뀔 때 계수와 창을 버린다.
    /// 전역으로 두면 매번 실패하는 비게임 전체화면 앱이 후퇴 중일 때 사용자가 게임으로 전환해도
    /// 창이 끝날 때까지 게임을 묻지 않아 <b>게임 위 최대 약 30초 노출이 새로 생긴다</b>.
    /// 목록 실패의 후퇴는 목록이 pid와 무관하므로 <b>전역</b>이다(범위를 안 넘기면 된다).</para>
    /// </summary>
    public sealed class GameVerdictRetryState
    {
        private bool _scopeInitialized;
        private ulong _scopeKey;
        private int _consecutiveFailures;
        private double _firstFailureAtSeconds;

        /// <summary>지금까지 이어진 실패 횟수(사유 문자열·진단용).</summary>
        public int ConsecutiveFailures => _consecutiveFailures;

        /// <summary>후퇴 문턱에 도달했는가(창이 끝났는지는 보지 않는다 — 그것은
        /// <see cref="TryBeginAttempt"/>가 판정한다).</summary>
        public bool ReachedBackoffThreshold
            => _consecutiveFailures >= GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold;

        /// <summary>
        /// 범위가 바뀌었으면 계수와 창을 버린다. 바뀌었으면 true.
        /// <para>첫 호출은 항상 "바뀐 것"으로 본다 — pid 0도 실재하는 입력이라
        /// (전경 창이 있는데 <c>GetWindowThreadProcessId</c>가 0을 주는 경우) 0을
        /// "아직 모름"으로 쓸 수 없다.</para>
        /// </summary>
        public bool ResetIfScopeChanged(ulong scopeKey)
        {
            if (_scopeInitialized && _scopeKey == scopeKey) return false;
            _scopeInitialized = true;
            _scopeKey = scopeKey;
            Clear();
            return true;
        }

        /// <summary>
        /// 이번 폴링에 조회해도 되는가. <b>시각을 비교하는 곳은 후퇴 창 하나뿐</b>이고,
        /// 재시도 자체는 시간 비교 없이 "다음 폴링"이다.
        ///
        /// <para>★ 창이 끝나면 여기서 계수를 0으로 되돌린다. 되돌리지 않으면 첫 시도 시각이 고정돼
        /// 첫 창 이후로 창이 다시 닫히지 않고, 후퇴가 사실상 사라진다(매 폴링 재시도).</para>
        ///
        /// <para>조회하지 않기로 한 폴링에서 이 메서드를 부른 뒤 실제로 조회하지 않아도 해롭지 않다 —
        /// 바꾸는 것은 "이미 끝난 창"을 닫는 것뿐이다.</para>
        /// </summary>
        public bool TryBeginAttempt(double nowSeconds)
        {
            if (_consecutiveFailures < GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold) return true;
            if (nowSeconds - _firstFailureAtSeconds < GameVerdictRetryPolicy.BackoffWindowSeconds) return false;
            Clear();
            return true;
        }

        /// <summary>조회가 성공했다 — 계수와 창을 버린다.</summary>
        public void NoteSuccess() => Clear();

        /// <summary>
        /// 조회가 실패했다. <b>연속 실패의 첫 시도 시각만</b> 기록한다(창의 기준점).
        /// </summary>
        public void NoteFailure(double nowSeconds)
        {
            if (_consecutiveFailures == 0) _firstFailureAtSeconds = nowSeconds;
            _consecutiveFailures++;
        }

        private void Clear()
        {
            _consecutiveFailures = 0;
            _firstFailureAtSeconds = 0.0;
        }
    }
}
