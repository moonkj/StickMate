using System;

namespace StickMate.Dialogue
{
    /// <summary>
    /// ★ <b>「대사용 달력을 얼마나 자주 볼 것인가」만 아는 물건</b> — 2026-09-06 요일·시간대 배선.
    ///
    /// ============================================================================
    /// 역할이 정확히 하나다
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>지금이 어느 구간인가</b> — <see cref="AmbientCalendarPolicy"/>. 순수 함수, 시계를 안 읽는다.</item>
    ///   <item><b>얼마나 자주 물어보는가</b> — <b>이 파일</b>. 구간 판정이 한 줄도 없다.</item>
    /// </list>
    /// 같은 분리를 재화 쪽이 <c>Core/CurrencyDayRolloverTicker.cs</c>로 먼저 했다. 그쪽 문서에 적힌
    /// 이유가 여기에도 그대로 유효하다 — 「하루가 넘어갔는가」와 「얼마나 자주 물어보는가」가 한 함수에
    /// 섞이면 그게 다음 버그다.
    ///
    /// ============================================================================
    /// 왜 캐시가 필요한가 — 이 앱은 하루 종일 켜져 있다
    /// ============================================================================
    /// 캐시가 없으면 <see cref="AmbientChatter.IsLineEligible"/>가 <b>추첨 한 번에 표 길이만큼</b>
    /// 벽시계를 읽는다. 그리고 더 나쁜 것은 <b>같은 추첨 안에서 시각이 갈릴 수 있다</b>는 점이다 —
    /// 자정·정시 경계를 밟는 순간 앞줄과 뒷줄이 서로 다른 구간을 보고, 후보 개수를 센 뒤 그 개수로
    /// 다시 훑는 2패스 추첨이 <b>어긋난 인덱스를 고른다</b>.
    ///
    /// <para>그래서 <b>추첨 시작 시점에 한 번</b> 스냅샷을 뜨고, 그 값을 15+13번의 자격 판정에
    /// 그대로 넘긴다. 스냅샷 자체도 <see cref="PollIntervalSeconds"/>마다 한 번만 새로 뜬다.</para>
    ///
    /// ============================================================================
    /// ★ 프레임 델타를 누적하지 않는다 — 단조 시계를 직접 뺀다
    /// ============================================================================
    /// 이 기계는 잠들었다 깬다. 델타 누적은 엔진의 프레임 델타 상한에 잘리고, 긴 정지 뒤에 큰 값이
    /// 한 번에 들어오면 잔여 이월 방식에 따라 연속 발화한다. 단조 시계 두 시점의 차는 어느 쪽에도
    /// 걸리지 않는다. (재화 티커가 같은 판단을 먼저 했다.)
    ///
    /// <para><b>정확도가 아니라 「적어도 이 간격마다 한 번 묻는다」가 계약이다.</b> 늦게 알아채도
    /// 잃는 것이 없다 — 요일·시간대는 래칫이 아니라 <b>현재 사실</b>이라, 늦게 물으면 그 순간의
    /// 참값을 그대로 받는다. 대가는 경계 후 최대 <see cref="PollIntervalSeconds"/>초 동안 지난
    /// 구간으로 판정될 수 있다는 것뿐이고, 그 크기는 R23이 연 26.45회로 재 두었다.</para>
    ///
    /// <para><b>플랫폼</b>: <c>UnityEngine</c>을 참조하지 않는다(단조 시각은 인자로 받는다).
    /// <c>#if</c>가 0건이고 4플랫폼 거동이 동일하다.</para>
    /// </summary>
    public sealed class AmbientCalendarClock
    {
        /// <summary>
        /// 달력을 다시 보기까지의 최소 간격(초).
        ///
        /// <para><b>왜 60인가</b> — 이 저장소가 이미 쓰는 값과 같은 값이다
        /// (<c>Core/CurrencyDayRolloverTicker.CheckIntervalSeconds</c>). 두 폴링이 같은 박자로 돌면
        /// 「자정을 넘겼는데 재화는 리셋됐고 대사는 어제 요일을 말한다」는 어긋남이 구조적으로
        /// 생기지 않는다. 그리고 요일·시간대 경계는 <b>정시</b>라(<see cref="AmbientCalendarPolicy"/>)
        /// 초 단위 정밀도가 의미를 갖지 않는다.</para>
        ///
        /// <para>★ <b>경제 수치가 아니다</b> — 지급량·상한이 아니라 폴링 간격이라 <c>design-systems</c>
        /// 소관이 아니고 <c>StickConfig</c>에도 넣지 않는다. 애셋 필드로 만들면
        /// <c>Assets/_Project/Data/DefaultStickConfig.asset</c>까지 함께 관리해야 하는데(애셋이 코드
        /// 기본값을 덮는다), 사용자가 조절할 이유가 하나도 없다.</para>
        /// </summary>
        public const double PollIntervalSeconds = 60.0;

        /// <summary>앱 전체가 공유하는 달력. <b>벽시계는 기계에 하나뿐</b>이므로 캐릭터별로 나눌 값이
        /// 아니고, 그래서 <see cref="StickMate.States.StickmanBlackboard"/>에 싣지 않았다
        /// (블랙보드는 «이 캐릭터의» 상태를 담는 곳이다).</summary>
        public static AmbientCalendarClock Shared { get; } = new AmbientCalendarClock();

        /// <summary>마지막으로 달력을 본 단조 시각.
        /// <para>초기값이 <see cref="double.NegativeInfinity"/>인 것이 계약의 일부다 —
        /// <b>첫 호출은 간격을 기다리지 않는다</b>. 그래서 「앱을 켠 뒤 60초 동안 요일 대사가
        /// 통째로 침묵한다」는 구간이 존재하지 않는다.</para></summary>
        private double _lastPollMonotonic = double.NegativeInfinity;

        private AmbientCalendarSnapshot _snapshot = AmbientCalendarSnapshot.Unknown;

        /// <summary>실제로 벽시계를 읽은 횟수(관측용). 「요일 대사가 0회 나왔다」만으로는
        /// <b>게이트가 막은 것</b>과 <b>오늘이 화요일인 것</b>을 구별할 수 없다 — 테스트가 이 값을 본다.</summary>
        public int PollCount { get; private set; }

        /// <summary>마지막으로 뜬 스냅샷. 아직 한 번도 안 봤으면
        /// <see cref="AmbientCalendarSnapshot.Unknown"/>(요일·시간대 줄 전부 침묵).</summary>
        public AmbientCalendarSnapshot Current => _snapshot;

        /// <summary>
        /// <b>테스트 전용</b> 벽시계 대체. null이면 <see cref="DateTime.Now"/>를 읽는다.
        ///
        /// <para>프로덕션에서 이걸 <b>세우는</b> 코드는 없다(<c>Tests/EditMode/AmbientChatterCalendarTests</c>의
        /// <c>프로덕션은_테스트용_시계_주입_통로를_쓰지_않는다</c>가 소스 스캔으로 잠근다). 자정·정시 경계
        /// 거동은 <b>기다려서 재는 것이 불가능</b>하므로 이 통로 말고 다른 방법이 없다 — 그리고 통로가
        /// <b>하나</b>라야 감사가 성립한다.</para>
        ///
        /// <para>★ 그 감사는 <b>설치와 철거를 가른다</b>: 프로덕션에 «설치»(null이 아닌 대입)는 0건이어야
        /// 하고, «철거»(<c>= null;</c>)는 <b>이 파일의 <see cref="ResetForTesting"/> 한 자리</b>에서만
        /// 허용된다. 둘을 구별하지 않던 구판 감사는 아래 철거 한 줄을 설치로 세어 스스로에게 걸렸다
        /// (2026-09-06 러너 실측). 여기를 고칠 때 <b>대입의 모양</b>이 바뀌면 그 감사도 함께 본다.</para>
        /// </summary>
        public Func<DateTime> WallClockOverrideForTesting { get; set; }

        /// <summary>
        /// 주기 게이트를 지나면 벽시계를 한 번 읽고 스냅샷을 갱신한다. 지나지 않으면 <b>지난 스냅샷을
        /// 그대로</b> 돌려준다 — <b>매 추첨마다 불러도 되는 값싼 호출</b>이며 대부분은 <c>double</c>
        /// 뺄셈 하나로 끝난다(할당 0).
        /// </summary>
        /// <param name="nowMonotonic"><c>Time.realtimeSinceStartupAsDouble</c>. <b>벽시계가 아니다.</b></param>
        public AmbientCalendarSnapshot PollIfDue(double nowMonotonic)
        {
            // NaN은 모든 비교가 false라 아래 게이트를 <b>거꾸로 통과</b>한다. 여기서 먼저 막는다 —
            // 통과시키면 기산점이 NaN이 되어 그 뒤 「now − NaN = NaN」으로 게이트가 영영 사라지고,
            // 매 추첨마다 시스템 시계를 읽게 된다.
            if (double.IsNaN(nowMonotonic)) return _snapshot;

            // 시계가 뒤로 갔으면 차가 음수라 게이트가 막는다(단조 시계라 정상 경로에는 없지만 공짜다).
            if (nowMonotonic - _lastPollMonotonic < PollIntervalSeconds) return _snapshot;

            _lastPollMonotonic = nowMonotonic;
            PollCount++;

            // ★ 이 저장소에서 <b>대사용 벽시계를 읽는 유일한 줄</b>이다.
            Func<DateTime> over = WallClockOverrideForTesting;
            _snapshot = AmbientCalendarPolicy.Classify(over != null ? over() : DateTime.Now);
            return _snapshot;
        }

        /// <summary>주기를 무시하고 지금 즉시 다시 본다. 테스트가 시각을 갈아 끼운 직후에 쓴다.</summary>
        public AmbientCalendarSnapshot PollNow(double nowMonotonic)
        {
            _lastPollMonotonic = double.NegativeInfinity;
            return PollIfDue(nowMonotonic);
        }

        /// <summary>정적 공유 인스턴스가 있으므로 리셋 통로가 필요하다 — 앞 테스트가 갈아 끼운
        /// 시각이 다음 테스트로 새면 그 실패는 <b>무작위로 보인다</b>.</summary>
        public void ResetForTesting()
        {
            _lastPollMonotonic = double.NegativeInfinity;
            _snapshot = AmbientCalendarSnapshot.Unknown;
            PollCount = 0;
            WallClockOverrideForTesting = null;
        }
    }
}
