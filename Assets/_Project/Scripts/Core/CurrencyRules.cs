using System;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 재화(동전) 경제의 <b>순수 규칙</b> — 상태를 하나도 들고 있지 않다.
    ///
    /// ============================================================================
    /// 이 파일이 존재하는 이유 — 「같은 사실이 두 곳에서 계산되지 않게」
    /// ============================================================================
    /// 상한·요율·클램프·창 소비는 <b>세 곳</b>에서 필요해진다: 세이브 로드(정규화), 매 틱 수급,
    /// 그리고 화면 표시(오늘 잔여). 그 셋이 각자 계산하면 그 순간 어긋나고, 어긋난 뒤에는
    /// "누가 옳은가"를 판정할 기준이 사라진다. 그래서 <b>숫자와 산식은 전부 여기 한 곳</b>에 있고
    /// <see cref="CurrencyModel"/>은 그 결과를 담기만 한다.
    ///
    /// <para>정본 문서: <c>docs/DESIGN_SYSTEMS_STATS.md</c> §18-2(확정 상수표) · §18-13(T-D-8~12) ·
    /// §20(v10 필드 확정), <c>docs/security/SECURITY_MODEL.md</c> T-14(clamp·리필 간격) ·
    /// T-15(8시간 창 래칫).</para>
    ///
    /// ============================================================================
    /// ★ 벽시계를 읽지 않는다 (security T-3-a)
    /// ============================================================================
    /// 이 클래스의 함수는 전부 <b>인자로 받은 값</b>만 본다. <c>DateTime.Now</c>/<c>UtcNow</c>도
    /// <c>Time.realtimeSinceStartupAsDouble</c>도 여기서 읽지 않는다 — 단조 시계는 호출부가 읽어
    /// 델타로 넘기고, 날짜 경계 계산(<see cref="LocalDayIndex"/>)은 <b>시각을 인자로 받는다</b>.
    /// 덕분에 이 파일 전체가 EditMode에서 실시간 대기 없이 검증된다(T-15-7-b).
    ///
    /// ============================================================================
    /// ★ 상한은 필드가 아니라 <b>함수</b>다 (T-D-9)
    /// ============================================================================
    /// <see cref="DailyCapCoins"/>는 저장되지 않는다. "오늘의 상한"을 파일에 적으면 그 숫자가
    /// 위조 대상이 되고, 재계산된 숫자는 위조 대상이 아니다. 세이브 스키마에
    /// <c>dailyCap</c>/<c>todayLimit</c>류 필드를 만들지 마라 —
    /// <c>Tests/EditMode/DailyLimitClampAuditTests</c>가 그 부재를 매 실행 확인한다.
    /// </summary>
    public static class CurrencyRules
    {
        // ====================================================================
        // 요율 — §18-2 확정 상수표
        // ====================================================================

        /// <summary>집중 모드 완주 시의 분당 동전. 사용자 최초 지시값.</summary>
        public const int FocusCoinsPerMinute = 24;

        /// <summary>온라인 유휴의 분당 동전. ★ 숫자를 따로 적지 않고 <b>집중의 정확히 1/2</b>로
        /// 유도한다 — §18-2가 정한 관계가 그것이고, 둘을 각각 적으면 한쪽만 바뀌는 날 관계가
        /// 조용히 깨진다. 오프라인 요율은 <b>0</b>이고, 그건 상수가 아니라
        /// "오프라인 수급 코드가 존재하지 않는다"로 표현된다(I-13′).</summary>
        public const int IdleCoinsPerMinute = FocusCoinsPerMinute / 2;

        /// <summary>유휴 초당 동전. 정수 나눗셈을 피하려고 double로 유도한다.</summary>
        public const double IdleCoinsPerSecond = IdleCoinsPerMinute / 60.0;

        // ====================================================================
        // 일일 상한 — §18-2 · T-14-5-b · T-D-9
        // ====================================================================

        /// <summary>회복제를 하나도 안 썼을 때의 하루 유휴 상한. 사용자 확정 1.</summary>
        public const int BaseDailyCapCoins = 1500;

        /// <summary>회복제 1개가 올려 주는 상한. 사용자 확정 1.</summary>
        public const int PotionBonusCoins = 500;

        /// <summary>하루에 쓸 수 있는 회복제 개수. 사용자 확정 1("하루에 회복제사용은 2개까지만").
        /// ★ 무료 1개는 이 2개에 <b>포함된다</b> — 유료 채널이 하루에 팔 수 있는 것은 최대 1개다(§18-2).</summary>
        public const int MaxPotionsPerDay = 2;

        /// <summary>어떤 경우에도 넘을 수 없는 하루 총액. ★ 숫자를 베끼지 않고 유도한다
        /// (1500 + 500×2 = 2500). 셋 중 하나가 바뀌면 이 값이 <b>저절로</b> 따라간다.</summary>
        public const int HardCeilingCoins = BaseDailyCapCoins + PotionBonusCoins * MaxPotionsPerDay;

        // ====================================================================
        // 8시간 창 — T-15
        // ====================================================================

        /// <summary>하루에 <b>동전이 실제로 지급될 수 있는</b> 분. 사용자 최초 지시(480분).
        /// ★ "앱이 켜져 있던 분"이 아니다 — T-15-1-a.</summary>
        public const int IdleWindowCapMinutes = 480;

        /// <summary>창 상한(초). 저장 필드 <c>idleWindowUsedSeconds</c>의 상한이기도 하다.</summary>
        public const double IdleWindowCapSeconds = IdleWindowCapMinutes * 60.0;

        // ====================================================================
        // 리필 최소 간격 — T-14-3-a
        // ====================================================================

        /// <summary>일자 리필(상한 리셋 + 무료 회복제 부활 + 창 리셋) 사이에 요구하는
        /// <b>단조 시계</b> 최소 간격. security 권고 20시간.
        /// <para>★ 이 값과 짝이 되는 "직전 리필 시각"은 <b>세이브에 남기지 않는다</b>(T-14-3-a) —
        /// 프로세스 메모리에만 둔다. 그래서 앱을 새로 켜면 첫 리필은 <b>항상 통과</b>하고,
        /// 정상 사용자를 잠글 수 있는 경로가 문법적으로 존재하지 않는다.</para></summary>
        public const double MinRefillGapSeconds = 20.0 * 60.0 * 60.0;

        // ====================================================================
        // 채널별 카운터 — §20-1 D군
        // ====================================================================

        /// <summary>[오늘 할일] 하루 1회 지급액(§0-2-6).</summary>
        public const int TodoDailyCoins = 300;

        /// <summary>활쏘기 1회 상금.</summary>
        public const int ArcheryCoinsPerAward = 20;

        /// <summary>활쏘기 상금의 쿨다운(초). ★ <b>세이브에 남기지 않는다</b> — 벽시계 유닉스 초를
        /// 저장하던 <c>lastArcheryCoinUnix</c>는 T-3-a 위반이라 폐기됐다(§20-3). 쿨다운은
        /// 단조 시계로 세션 안에서만 산다.</summary>
        public const double ArcheryAwardCooldownSeconds = 600.0;

        /// <summary>★ <b>임시값, U-41 확정 대기.</b> 활쏘기 하루 상금 횟수 상한.
        /// §20-3-b가 원형 D(6,000동전/일)에서 <b>1,200동전 = 60회</b>를 유도하고 1.20배 여유를 얹어
        /// 72회를 권고했지만, <b>실사용 텔레메트리가 없다</b>. 필드(<c>archeryCoinsToday</c>)는 v10에
        /// 넣었고 이 상수만 열어 둔다 — 상수는 코드 한 줄이지만 필드는 나중에 넣으면 v11이다.
        /// <para>정상 사용자(원형 B)의 활쏘기는 하루 4회 = 이 상한의 5.6%라, 이 값이 조금 틀려도
        /// 정상 경로에는 닿지 않는다.</para></summary>
        public const int ArcheryDailyAwardLimit = 72;

        /// <summary>활쏘기 하루 상금 총액 상한(= 횟수 × 1회 상금). 숫자를 따로 적지 않는다.</summary>
        public const int ArcheryDailyCoinLimit = ArcheryDailyAwardLimit * ArcheryCoinsPerAward;

        // ====================================================================
        // 스탯 등급 high-water mark — §20-1 A군 #4
        // ====================================================================

        /// <summary>등급 high-water mark를 기록하는 슬롯 수(H-8 눈금이 걸리는 4스탯).</summary>
        public const int StatTierSlotCount = 4;

        /// <summary>등급 최대치. 눈금 3개(25/50/80%)라 등급은 0~3이다.
        /// ★ "평생 최대 12회 갱신"(§20-1)이 <c>StatTierSlotCount × MaxStatTier</c>와 같은지는
        /// 테스트가 확인한다 — 두 숫자가 어긋나면 둘 중 하나가 낡은 것이다.</summary>
        public const int MaxStatTier = 3;

        // ====================================================================
        // 첫 실행 시드 — U-42 미확정
        // ====================================================================

        /// <summary>★ <b>U-42 미확정.</b> 첫 실행 시드 동전. <c>ECONOMY_SPEC</c>:117의 60은
        /// <b>구단위</b>이고 §13-3 확정표에 시드가 없다 — <b>추측하지 않는다</b>.
        /// <para>그래서 0이고, <see cref="CanGrantSeed"/>가 0일 때 <b>지급도 안 하고 플래그도 안 세운다</b>.
        /// 방향이 중요하다: 플래그를 먼저 세워 버리면 U-42가 확정되는 날 기존 사용자가 시드를
        /// 영영 못 받는다(되돌릴 수 없는 손실). 지금은 아무 일도 일어나지 않고, 값이 정해지면
        /// 그때 처음으로 지급된다.</para></summary>
        public const int SeedCoins = 0;

        /// <summary>시드를 지급할 수 있는 상태인가. 금액이 정해지지 않았으면(U-42) false다.</summary>
        public static bool CanGrantSeed(bool seedAlreadyGranted)
            => !seedAlreadyGranted && SeedCoins > 0;

        // ====================================================================
        // ★ 클램프 — "복구"가 아니라 "정규화"다 (T-14-5-b)
        // ====================================================================
        // 저장된 값을 그대로 믿지 않는다. 읽는 즉시 다듬고, 실패도 경고도 남기지 않는다.
        // 이건 방어이기 전에 <b>위생</b>이다 — 손상된 세이브·구버전 파일·`.writing` 잔해에도
        // 같은 코드가 같은 답을 낸다. 값을 거부하지 않으므로 "우리 버그를 치터보다 먼저 만나는"
        // 종류의 검사가 아니다(§4-1-4).

        /// <summary>오늘 쓴 회복제 개수. ★ I-12′의 유일한 방어선 — 9999를 써 넣어도 2에서 멈춘다.</summary>
        public static int ClampPotionsUsed(int potionsUsedToday)
            => potionsUsedToday < 0 ? 0
               : potionsUsedToday > MaxPotionsPerDay ? MaxPotionsPerDay
               : potionsUsedToday;

        /// <summary>오늘의 상한. ★ <b>필드가 아니라 함수다</b>(T-D-9). 인자는 안에서 다시 클램프하므로
        /// 호출부가 클램프를 잊어도 상한이 새지 않는다.</summary>
        public static int DailyCapCoins(int potionsUsedToday)
            => BaseDailyCapCoins + PotionBonusCoins * ClampPotionsUsed(potionsUsedToday);

        /// <summary>오늘 지급된 동전. 상한은 <see cref="DailyCapCoins"/>다 —
        /// <see cref="HardCeilingCoins"/>보다 <b>같거나 더 좁고</b>, 정상 경로에서 둘의 차이가
        /// 관측되는 경우가 없다(회복제를 n개 쓴 상태에서 지급될 수 있는 최대가 곧 DailyCap이다).
        /// security T-14-5-b가 적은 <c>Clamp(0, HardCeilingCoins)</c>를 <b>더 좁게</b> 만족시킨다.</summary>
        public static int ClampGrantedCoins(int coins, int potionsUsedToday)
        {
            int cap = DailyCapCoins(potionsUsedToday);
            return coins < 0 ? 0 : coins > cap ? cap : coins;
        }

        /// <summary>
        /// 오늘 갉아 먹은 창(초). ★★ <b>NaN을 0이 아니라 상한으로 보낸다</b> — 다른 클램프와
        /// <b>방향이 반대</b>라 반드시 이유를 남긴다(T-15-1-c).
        /// <para>0으로 보내면 <b>손상된 파일이 창을 리셋하는 무료 우회</b>가 된다. 상한으로 보내면
        /// 손상 파일을 만난 정상 사용자가 그날 유휴 수급만 못 하고, 일일 상한 2,500은 그대로
        /// 살아 있어 활쏘기·집중 모드·[오늘 할일]로 계속 벌 수 있다 — <b>잠기지 않는다</b>.</para>
        /// </summary>
        public static double ClampIdleWindowSeconds(double usedSeconds)
        {
            if (double.IsNaN(usedSeconds)) return IdleWindowCapSeconds;
            if (usedSeconds < 0.0) return 0.0;
            return usedSeconds > IdleWindowCapSeconds ? IdleWindowCapSeconds : usedSeconds;
        }

        /// <summary>오늘 활쏘기로 받은 동전(§20-3-b — 위조 이득 상한이 곧 이 값이다).</summary>
        public static int ClampArcheryCoinsToday(int coins)
            => coins < 0 ? 0 : coins > ArcheryDailyCoinLimit ? ArcheryDailyCoinLimit : coins;

        /// <summary>등급 high-water mark 한 칸.</summary>
        public static int ClampStatTier(int tier)
            => tier < 0 ? 0 : tier > MaxStatTier ? MaxStatTier : tier;

        /// <summary>
        /// 동전 잔액. ★ <b>상한을 걸지 않는다</b> — security §4 · T-14-5-c · T-15-2의 확정 판정이다
        /// (<c>coinBalance = 999999</c> 편집 1회는 우리가 로컬로 막을 수 없고, 막으려 드는 순간
        /// 정직한 유저에게 "당신은 치터입니다"를 말하는 코드가 된다).
        /// <para>하한 0만 건다. 음수 잔액은 위조가 아니라 <b>우리 차감 버그의 신호</b>이고,
        /// 화면에 "−300동전"을 그릴 자리가 없다.</para>
        /// </summary>
        public static int ClampCoinBalance(int coins) => coins < 0 ? 0 : coins;

        /// <summary>고정된 날짜 경계 오프셋(분). 실존 시간대는 UTC−12:00 ~ UTC+14:00이라
        /// 그 바깥 값은 손상으로 보고 잘라 낸다.</summary>
        public const int MaxDayBoundaryOffsetMinutes = 14 * 60;

        /// <summary>날짜 경계 오프셋 정규화.</summary>
        public static int ClampDayBoundaryOffsetMinutes(int minutes)
            => minutes < -MaxDayBoundaryOffsetMinutes ? -MaxDayBoundaryOffsetMinutes
               : minutes > MaxDayBoundaryOffsetMinutes ? MaxDayBoundaryOffsetMinutes
               : minutes;

        // ====================================================================
        // 날짜 경계 — ★ 이 저장소에서 시각을 보는 유일한 지점
        // ====================================================================

        private static readonly DateTime UnixEpochUtc =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 고정된 오프셋 기준 "오늘"의 일자 번호. <b>시각을 인자로 받는다</b> — 이 함수 자체는
        /// 벽시계를 읽지 않으므로 테스트가 임의의 날짜를 밀어 넣을 수 있다.
        /// <para>오프셋을 <b>첫 실행에 고정하고 다시 안 바꾸는</b> 이유는 T-D-4다 —
        /// 시간대를 옮겨 다니며(또는 서머타임으로) 하루 경계를 여러 번 넘기는 것을 막는다.
        /// 여행자가 비행기에서 내려도 하루 경계가 흔들리지 않는다.</para>
        /// </summary>
        public static int LocalDayIndex(DateTime utcNow, int dayBoundaryOffsetMinutes)
        {
            DateTime shifted = utcNow.AddMinutes(ClampDayBoundaryOffsetMinutes(dayBoundaryOffsetMinutes));
            return (int)Math.Floor((shifted - UnixEpochUtc).TotalDays);
        }

        /// <summary>
        /// 리필(= 일일 상한 리셋 + 무료 회복제 부활 + 창 리셋)을 허용할 것인가.
        /// <para>두 조건이 <b>동시에</b> 참이어야 한다: ① 일자가 <b>전진</b>했다(T-4-b 래칫 —
        /// 시계를 되감아도 과거로 가지 않는다) ② 직전 리필로부터 단조 시계로
        /// <see cref="MinRefillGapSeconds"/>가 지났다(T-14-3-a — 세션 안에서 날짜를 반복해서
        /// 넘기는 (카) 공격을 완전히 막는다).</para>
        /// <para><paramref name="lastRefillMonotonic"/>의 초기값은
        /// <see cref="double.NegativeInfinity"/>다 — 그래야 <b>앱을 새로 켠 뒤 첫 리필이 항상 통과</b>한다.
        /// 그것이 이 규칙의 오탐이 0인 구조적 이유다.</para>
        /// </summary>
        public static bool MayRefill(int dayIndexNow, int maxDayIndexSeen,
            double nowMonotonic, double lastRefillMonotonic)
            => dayIndexNow > maxDayIndexSeen
               && (nowMonotonic - lastRefillMonotonic) >= MinRefillGapSeconds;

        // ====================================================================
        // 유휴 수급 1틱 — 순수 함수 (T-15-1-b)
        // ====================================================================

        /// <summary>한 틱의 결과. 세 값을 한꺼번에 돌려주는 이유는 <b>세 값이 한 사건</b>이기
        /// 때문이다 — 지급액만 받고 창 소비를 따로 계산하면 그 둘이 어긋난다.</summary>
        public readonly struct IdleTickResult
        {
            /// <summary>이번 틱에 지급할 동전(정수).</summary>
            public readonly int CoinsGranted;

            /// <summary>이번 틱이 창에서 갉아먹은 초. ★ <b>지급이 일어난 초만</b> 값이 있다
            /// (T-15-1-a·T-D-13) — 상한에 걸려 한 푼도 못 버는 동안 창이 닳으면
            /// 그건 방어가 아니라 버그다.</summary>
            public readonly double WindowSecondsSpent;

            /// <summary>다음 틱으로 넘기는 <b>동전 소수분</b>.
            /// <para>★ 이게 없으면 60fps에서 한 틱의 수입이 0.0033동전이라 <c>(int)</c> 절단으로
            /// <b>전부 사라진다</b>(하루 종일 켜 둬도 0원). 같은 계열의 실제 사고를 security T-3-c가
            /// "적게 쌓이는 방향의 공정성 문제"로 적어 뒀다. 소수분은 세이브에 넣지 않는다 —
            /// 최대 1동전 미만이라 잃어도 무해하고, 필드 하나를 아끼는 쪽이 낫다.</para></summary>
            public readonly double CarryCoins;

            public IdleTickResult(int coinsGranted, double windowSecondsSpent, double carryCoins)
            {
                CoinsGranted = coinsGranted;
                WindowSecondsSpent = windowSecondsSpent;
                CarryCoins = carryCoins;
            }
        }

        /// <summary>
        /// 유휴 수급 한 틱. <b>세 축이 전부 여기서 만난다</b>(T-15-3): ① 일일 상한 ② (호출부의)
        /// 리필 간격 ③ 8시간 창.
        ///
        /// <para><paramref name="deltaSeconds"/>는 <b>단조 시계 델타</b>여야 한다
        /// (<c>Time.realtimeSinceStartupAsDouble</c>의 차). 벽시계 델타를 넣으면 T-3-a가 깨진다 —
        /// 이 함수는 그것을 알 수 없으므로 호출부의 책임이고,
        /// <c>Tests/EditMode/DailyLimitClampAuditTests</c>가 소스 스캔으로 그 책임을 잠근다.</para>
        ///
        /// <para><paramref name="idleWindowCapSeconds"/>를 인자로 받는 이유는 <b>네거티브 컨트롤</b>
        /// 때문이다 — <see cref="double.PositiveInfinity"/>를 넣어 "창이 없으면 실제로 얼마까지
        /// 새는가"를 재야 5,760이 창의 성과임이 증명된다(T-15-7 B).</para>
        /// </summary>
        public static IdleTickResult IdleTick(
            double deltaSeconds,
            bool isIdleEarning,
            int todayGrantedCoins,
            int potionsUsedToday,
            double idleWindowUsedSeconds,
            double idleWindowCapSeconds,
            double carryCoins)
        {
            if (double.IsNaN(carryCoins) || carryCoins < 0.0) carryCoins = 0.0;

            // 비유휴/역행/NaN 델타 — 아무 일도 없다. 창도 안 갉는다(T-15-1-a).
            if (!isIdleEarning || !(deltaSeconds > 0.0))
                return new IdleTickResult(0, 0.0, carryCoins);

            int room = DailyCapCoins(potionsUsedToday) - todayGrantedCoins;
            if (room <= 0) return new IdleTickResult(0, 0.0, carryCoins);

            double windowRoom = idleWindowCapSeconds - ClampIdleWindowSeconds(idleWindowUsedSeconds);
            if (!(windowRoom > 0.0)) return new IdleTickResult(0, 0.0, carryCoins);

            double paidSeconds = deltaSeconds < windowRoom ? deltaSeconds : windowRoom;
            double earned = carryCoins + paidSeconds * IdleCoinsPerSecond;

            int pay = (int)Math.Floor(earned);
            double carry = earned - pay;

            // 상한에 걸리면 남은 소수분은 버린다 — 오늘은 어차피 못 받는다.
            if (pay >= room) { pay = room; carry = 0.0; }

            return new IdleTickResult(pay, paidSeconds, carry);
        }

        // ====================================================================
        // 불변식 검산 — 이 파일 안에서 닫힌다 (T-D-15)
        // ====================================================================

        /// <summary>
        /// T-D-15: 창(480분 × 12동전)이 하루 절대 천장(2,500)의 <b>1.5배 이상</b>이어야 한다.
        /// 이 비율이 1.0 아래로 내려가면 <b>창이 정상 사용자에게 먼저 걸려</b> 오탐이 기본값이 된다.
        /// 요율·상한·창 중 하나만 바꿔도 이 비율이 움직이므로, 테스트가 매 실행 확인한다.
        /// </summary>
        public const double MinWindowToCeilingRatio = 1.5;

        /// <summary>현재 상수 조합의 창/천장 비율. 지금은 5,760 / 2,500 = 2.304배다.</summary>
        public static double WindowToCeilingRatio =>
            (IdleWindowCapMinutes * (double)IdleCoinsPerMinute) / HardCeilingCoins;
    }
}
