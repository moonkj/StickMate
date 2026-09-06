using System;

namespace StickMate.Dialogue
{
    /// <summary>
    /// 요일 대사가 요구하는 <b>달력 구간</b>. <see cref="None"/>은 "요일을 안 본다"(상시 자격)이고,
    /// 스냅샷 쪽에서는 "오늘은 어느 요일 구간에도 속하지 않는다"(화·수·목)를 뜻한다.
    ///
    /// <para>0을 <see cref="None"/>에 준 것이 계약의 일부다 — 배열에 칸을 늘리고 값을 안 채우면
    /// 기본값이 «상시»가 되어 <b>조용히 통과</b>하지, «월요일»이 되어 엉뚱한 날에 말하지 않는다.
    /// 스냅샷 쪽에서도 같은 방향으로 안전하다: 아직 한 번도 달력을 안 봤으면 요일 줄이 전부 침묵한다
    /// (잘못된 요일 대사보다 침묵이 낫다 — 절대 불변 원칙 1).</para>
    /// </summary>
    public enum AmbientDayBucket
    {
        None = 0,
        Monday,
        Friday,
        Weekend,
    }

    /// <summary>
    /// 시간대 대사가 요구하는 <b>5구간</b>. 경계는 <c>design/systems/timeofday_r23_bounds.out.txt</c>에서
    /// 확정됐다(정시 경계, 반열린 구간).
    ///
    /// <para><see cref="None"/>의 뜻은 <see cref="AmbientDayBucket.None"/>과 같다 — 요구 축에서는
    /// «시간을 안 본다», 스냅샷 축에서는 «아직 달력을 안 봤다». <see cref="AmbientCalendarPolicy.Classify"/>는
    /// <see cref="None"/>을 <b>절대 돌려주지 않는다</b>(5구간이 24시간을 빈틈 0·겹침 0으로 덮는다).</para>
    /// </summary>
    public enum AmbientTimeBucket
    {
        None = 0,
        Morning,
        Lunch,
        Afternoon,
        Evening,
        Night,
    }

    /// <summary>
    /// 한 번 읽은 벽시계를 <b>대사 계층이 쓰는 두 개의 사실</b>로 굳힌 것.
    ///
    /// <para>이 구조체가 존재하는 이유는 <see cref="AmbientChatter.IsLineEligible"/>의 <b>순수성</b>이다.
    /// 그 함수는 추첨 한 번에 표 길이만큼(현재 15+13회) 불리므로, 안에서 <c>DateTime.Now</c>를 읽으면
    /// (가) 한 번의 추첨이 서로 다른 시각을 볼 수 있고 (나) 시스템 호출이 추첨마다 수십 번 난다.
    /// 그래서 <b>바깥에서 한 번 재고 값으로 주입</b>한다.</para>
    /// </summary>
    public readonly struct AmbientCalendarSnapshot
    {
        public readonly AmbientDayBucket Day;
        public readonly AmbientTimeBucket Time;

        public AmbientCalendarSnapshot(AmbientDayBucket day, AmbientTimeBucket time)
        {
            Day = day;
            Time = time;
        }

        /// <summary>아직 달력을 한 번도 안 본 상태. 요일·시간대 줄이 <b>전부</b> 후보에서 빠진다.</summary>
        public static AmbientCalendarSnapshot Unknown => default;

        public override string ToString() => $"{Day}/{Time}";
    }

    /// <summary>
    /// ★ <b>「지금이 언제인가」를 대사가 쓰는 구간으로 번역하는 유일한 자리</b> — 2026-09-06 요일·시간대 배선.
    ///
    /// ============================================================================
    /// 왜 여기(<c>Dialogue/</c>)에 있는가 — 두 개의 금지선 사이
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>재화 쪽(<c>Core/CurrencyModel</c>·<c>Core/CurrencyRules</c>)에 두면 안 된다.</b>
    ///     그쪽은 «수급·정산 경로에서 벽시계를 읽는 곳은 정확히 한 곳»이라는 계약을 갖고 있고
    ///     <c>Tests/EditMode/IncomeTimeSourceAuditTests</c>가 그것을 잠근다. 대사용 시간대 판정이
    ///     거기 끼면 그 감사가 «두 번째 읽기»를 보게 된다.</item>
    ///   <item><b><see cref="AmbientChatter.IsLineEligible"/> 안에 두면 안 된다.</b> 순수 함수다
    ///     (위 <see cref="AmbientCalendarSnapshot"/> 문서).</item>
    /// </list>
    /// 그래서 <b>판정(이 파일, 순수)</b>과 <b>언제 물어볼 것인가(<see cref="AmbientCalendarClock"/>)</b>를
    /// 갈랐다. 같은 분리를 재화 쪽이 <c>Core/CurrencyDayRolloverTicker.cs</c>로 먼저 했고, 그 이유도 같다 —
    /// 같은 사실을 두 곳에서 계산하면 그게 다음 버그다.
    ///
    /// ============================================================================
    /// 경계 — <c>design/systems/timeofday_r23_bounds.out.txt</c> 확정값
    /// ============================================================================
    /// <code>
    ///   아침 [05,11)  6h   점심 [11,14)  3h   오후 [14,18)  4h
    ///   저녁 [18,22)  4h   밤   [22,05)  7h        합 24h · 빈틈 0 · 겹침 0
    /// </code>
    /// <b>정시 경계만 쓴다.</b> 분 단위 경계는 <see cref="AmbientCalendarClock.PollIntervalSeconds"/>
    /// 폴링과 정렬되지 않아, 「경계를 넘었는데 아직 지난 구간으로 판정」되는 창이 의미 없이 커진다
    /// (R23 §4: 정시 경계에서도 그 창이 최대 60초이고, 어긋난 시간대 대사 관측 기대치는 연 26.45회다).
    ///
    /// <para><b>플랫폼</b>: <see cref="DateTime"/> 하나만 본다. <c>#if</c>가 0건이고
    /// macOS/Windows/iPad/iPhone 판정이 동일하다.</para>
    /// </summary>
    public static class AmbientCalendarPolicy
    {
        // ★ 구간 경계는 여기 한 곳에만 적는다. 테스트는 이 상수를 <b>참조</b>해서 검증한다
        //   (숫자를 테스트에 베끼면 기준과 대상이 갈라져도 아무도 모른다 — CLAUDE.md).
        public const int MorningStartHour = 5;
        public const int LunchStartHour = 11;
        public const int AfternoonStartHour = 14;
        public const int EveningStartHour = 18;
        public const int NightStartHour = 22;

        /// <summary>하루의 시간 수. 밤 구간이 자정을 넘어 감기는 것을 이 값으로 설명한다.</summary>
        public const int HoursPerDay = 24;

        /// <summary>벽시계 한 점을 요일·시간대 구간으로. <b>순수 함수</b> — 시계를 읽지 않는다.</summary>
        public static AmbientCalendarSnapshot Classify(DateTime now)
            => new AmbientCalendarSnapshot(DayBucketOf(now.DayOfWeek), TimeBucketOf(now.Hour));

        /// <summary>
        /// 요일 구간. 화·수·목은 <see cref="AmbientDayBucket.None"/>이다 — <b>빈 칸이 설계다</b>.
        /// 대사 풀 계약(R23)의 «최악 조합»이 정확히 그 사흘이고, 거기서도 상시 10줄 + 시간대 2줄로
        /// N=12가 유지되도록 시간대 5구간이 24시간을 전부 덮는다.
        /// </summary>
        public static AmbientDayBucket DayBucketOf(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return AmbientDayBucket.Monday;
                case DayOfWeek.Friday: return AmbientDayBucket.Friday;
                case DayOfWeek.Saturday:
                case DayOfWeek.Sunday: return AmbientDayBucket.Weekend;
                default: return AmbientDayBucket.None;
            }
        }

        /// <summary>
        /// 시간대 구간. 반열린 구간 <c>[시작, 끝)</c>이고 <b>밤만 자정을 넘어 감긴다</b>.
        ///
        /// <para>범위 밖 입력(<see cref="DateTime.Hour"/>는 0~23이므로 정상 경로에서는 없다)은
        /// <see cref="AmbientTimeBucket.Night"/>가 아니라 <see cref="AmbientTimeBucket.None"/>으로
        /// 떨어뜨린다 — 손상된 값이 «밤»으로 읽혀 밤 대사를 하는 것보다 침묵이 낫다.</para>
        /// </summary>
        public static AmbientTimeBucket TimeBucketOf(int hour)
        {
            if (hour < 0 || hour >= HoursPerDay) return AmbientTimeBucket.None;
            if (hour >= NightStartHour || hour < MorningStartHour) return AmbientTimeBucket.Night;
            if (hour < LunchStartHour) return AmbientTimeBucket.Morning;
            if (hour < AfternoonStartHour) return AmbientTimeBucket.Lunch;
            if (hour < EveningStartHour) return AmbientTimeBucket.Afternoon;
            return AmbientTimeBucket.Evening;
        }
    }
}
