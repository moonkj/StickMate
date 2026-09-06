using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 재화 규칙의 <b>동작</b> 검증 — 순수 함수라 EditMode다(실시간을 기다릴 이유가 없다,
    /// security T-15-7-b). 소스 스캔(감사)은 <see cref="DailyLimitClampAuditTests"/>가 따로 맡는다:
    /// "코드가 그렇게 생겼나"와 "코드가 그렇게 계산하나"는 <b>다른 질문</b>이고, 섞으면 둘 다 약해진다.
    ///
    /// ============================================================================
    /// ★ 숫자를 한 개도 베끼지 않는다
    /// ============================================================================
    /// 5,760 · 17,280 · 480 · 12 · 2,500을 리터럴로 적으면, 요율이나 창을 조정하는 날
    /// <b>마이그레이션과 무관한 테스트가 함께 빨개지고</b> 고치는 사람은 "숫자만 맞추면 되는 잡음"으로
    /// 학습한다(CLAUDE.md 2026-09-01 확정 규칙). 기대값은 전부
    /// <see cref="CurrencyRules"/> 상수에서 <b>이 파일 안에서 계산</b>한다.
    /// </summary>
    public sealed class CurrencyRulesTests
    {
        [SetUp]
        public void Reset() => CurrencyModel.ResetForTesting();

        /// <summary>
        /// ★ 2026-09-06 — <b>나가는 문에도 같은 리셋을 건다</b>.
        ///
        /// <para><see cref="Reset"/>(SetUp)은 <b>들어오는 것</b>만 막는다. 픽스처의 <b>마지막</b>
        /// 테스트가 남긴 정적 상태는 그 뒤에 도는 픽스처가 그대로 물려받는데, 이 파일에는
        /// <c>같은_것을_두_번_살_수_없다</c>가 <c>equip.head.crown</c>을 실제로 구매한다
        /// (<see cref="CurrencyModel.TryPurchaseItem"/>). 구매 이력은 <b>스위트 전역 사실</b>이다 —
        /// <c>ItemCatalogEntry.IsOwned</c>가 «레벨 파생 ∪ 구매분»이라(DESIGN_SYSTEMS_STATS §20-2-a)
        /// 그 한 줄이 살아남으면 <b>Lv.1에서도 왕관이 「보유」</b>가 된다.</para>
        ///
        /// <para>★ <b>지금은 아직 새지 않는다</b> — NUnit이 픽스처 안의 테스트를 이름순으로 돌고,
        /// 이 파일의 마지막 이름은 <c>회복제_개수를_위조해도…</c>(순수 함수만 부른다)라서 구매 이력이
        /// 남지 않는다. 즉 이 TearDown은 <b>버그 수정이 아니라 잠금</b>이다: 테스트 이름 하나만
        /// 바뀌거나 구매하는 테스트가 하나 더 붙으면 그날 바로 샌다. 그리고 그때 빨개지는 것은
        /// 여기가 아니라 <c>EquipmentDebugUnlockTests.스위치를_끄면_요구_레벨_규칙이_그대로_살아_있다</c>
        /// (알파벳 순으로 뒤, 같은 왕관을 <c>IsFalse(IsOwned)</c>로 잰다)여서, <b>QA 해금 스위치가
        /// 고장났다는 엉뚱한 진단</b>이 먼저 올라온다. 오늘 밤 이미 같은 형태로 한 번 속았다.</para>
        /// </summary>
        [TearDown]
        public void RestoreSuiteDefault() => CurrencyModel.ResetForTesting();

        // ====================================================================
        // 1. 상한은 함수다 (T-D-9) + 회복제 clamp (I-12′)
        // ====================================================================

        [Test]
        public void 일일_상한은_회복제_개수의_함수다()
        {
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, CurrencyRules.DailyCapCoins(0));
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins + CurrencyRules.PotionBonusCoins,
                CurrencyRules.DailyCapCoins(1));
            Assert.AreEqual(CurrencyRules.HardCeilingCoins,
                CurrencyRules.DailyCapCoins(CurrencyRules.MaxPotionsPerDay));
        }

        [Test]
        public void 회복제_개수를_위조해도_상한은_절대_천장에서_멈춘다()
        {
            // ★ I-12′ — 세이브를 메모장으로 열어 9999를 써 넣은 상황. clamp가 유일한 방어선이다.
            Assert.AreEqual(CurrencyRules.HardCeilingCoins, CurrencyRules.DailyCapCoins(9999),
                "회복제 개수 위조가 상한을 그대로 밀어 올렸습니다 — 위조 순이득이 무한이 됩니다.");
            Assert.AreEqual(CurrencyRules.MaxPotionsPerDay, CurrencyRules.ClampPotionsUsed(9999));

            // 반대 방향도 막혀 있다: 내리면 자기 상한이 줄어들 뿐이다(T-D-9가 만든 성질).
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, CurrencyRules.DailyCapCoins(-5));
            Assert.AreEqual(0, CurrencyRules.ClampPotionsUsed(-5));
        }

        [Test]
        public void 하루에_쓸_수_있는_회복제는_상수만큼이다()
        {
            for (int i = 0; i < CurrencyRules.MaxPotionsPerDay; i++)
            {
                Assert.IsTrue(CurrencyModel.TryUsePotion(), $"{i + 1}번째 회복제를 못 썼습니다.");
            }
            Assert.IsFalse(CurrencyModel.TryUsePotion(),
                "상한을 넘겨 회복제를 또 썼습니다 — 「유료 SKU 무한」을 막는 유일한 정상 경로 방어선입니다.");
            Assert.AreEqual(CurrencyRules.HardCeilingCoins, CurrencyModel.DailyCapCoins());
        }

        // ====================================================================
        // 2. ★ NaN은 0이 아니라 상한으로 간다 (T-15-1-c) — 방향이 반대인 유일한 clamp
        // ====================================================================

        [Test]
        public void 창_소비량의_NaN은_0이_아니라_상한으로_간다()
        {
            Assert.AreEqual(CurrencyRules.IdleWindowCapSeconds,
                CurrencyRules.ClampIdleWindowSeconds(double.NaN), 1e-9,
                "손상된 값(NaN)이 창을 0으로 리셋했습니다 — 파일을 망가뜨리는 것이 " +
                "<b>무료 우회</b>가 됩니다. 실패는 보수적인 쪽(상한)으로 보내야 합니다.");

            // ★ 음성 대조 — "전부 상한으로 보낸다"가 아니라 <b>NaN만</b> 그렇다는 것을 보인다.
            //   이게 없으면 "항상 상한을 반환한다"는 망가진 구현도 위 단언을 통과한다.
            Assert.AreEqual(0.0, CurrencyRules.ClampIdleWindowSeconds(-1.0), 1e-9,
                "음수는 0으로 가야 합니다(정상 방향).");
            double mid = CurrencyRules.IdleWindowCapSeconds / 2.0;
            Assert.AreEqual(mid, CurrencyRules.ClampIdleWindowSeconds(mid), 1e-9,
                "정상 범위 값까지 상한으로 밀렸습니다 — 그러면 모두가 그날 유휴 수급을 잃습니다.");
            Assert.AreEqual(CurrencyRules.IdleWindowCapSeconds,
                CurrencyRules.ClampIdleWindowSeconds(CurrencyRules.IdleWindowCapSeconds * 3.0), 1e-9);
        }

        [Test]
        public void 동전_잔액은_상한을_걸지_않고_하한만_건다()
        {
            // security §4 · T-14-5-c · T-15-2 확정 판정 — 잔액 위조는 로컬로 막을 수 없고,
            // 막으려 드는 순간 정직한 유저에게 "당신은 치터입니다"를 말하는 코드가 된다.
            const int forged = 999999;
            Assert.AreEqual(forged, CurrencyRules.ClampCoinBalance(forged),
                "잔액에 상한을 걸었습니다 — §4가 기각한 방어이고, 정상 유저의 큰 잔액을 깎습니다.");

            // 하한은 건다: 음수 잔액은 위조가 아니라 <b>우리 차감 버그</b>의 신호다.
            Assert.AreEqual(0, CurrencyRules.ClampCoinBalance(-1));
        }

        // ====================================================================
        // 3. ★★ 8시간 창 래칫 — T-15-7 (B). 두 번째가 이 파일에서 가장 중요한 테스트다
        // ====================================================================

        /// <summary>하루(1,440분)를 유휴로만 시뮬레이션한다. <b>일일 상한은 무한 롤오버로 무력화됐다고
        /// 가정</b>한다(= <c>todayGrantedCoins</c>를 매 틱 0으로 둔다) — T-15-7의 최악 가정 그대로다.
        /// 그래야 <b>창 하나만</b>이 변수로 남고, 5,760이 창의 성과인지 다른 것의 성과인지 갈린다.
        /// <para>틱 간격을 60초로 두는 이유: 벽시계를 기다리지 않는 순수 계산이라 프레임 수와 무관하고,
        /// 분당 요율이 정수라 부동소수 누적 오차가 소수분 캐리 안에서 닫힌다.</para></summary>
        private static int SimulateOneDay(double windowCapSeconds)
        {
            const double tickSeconds = 60.0;
            int minutesInDay = 24 * 60;

            int total = 0;
            double windowUsed = 0.0;
            double carry = 0.0;

            for (int minute = 0; minute < minutesInDay; minute++)
            {
                CurrencyRules.IdleTickResult tick = CurrencyRules.IdleTick(
                    tickSeconds, true,
                    todayGrantedCoins: 0,               // ★ 롤오버 무한 허용 = 상한이 안 걸린다
                    potionsUsedToday: 0,
                    idleWindowUsedSeconds: windowUsed,
                    idleWindowCapSeconds: windowCapSeconds,
                    carryCoins: carry);

                total += tick.CoinsGranted;
                windowUsed += tick.WindowSecondsSpent;
                carry = tick.CarryCoins;
            }
            return total;
        }

        [Test]
        public void 창이_있으면_하루_수입이_창_길이에서_잘린다()
        {
            int expected = CurrencyRules.IdleWindowCapMinutes * CurrencyRules.IdleCoinsPerMinute;
            Assert.AreEqual(expected, SimulateOneDay(CurrencyRules.IdleWindowCapSeconds),
                "창이 하루 수입을 자르지 못했습니다.");
        }

        [Test]
        public void 음성대조_창을_없애면_실제로_하루치가_전부_샌다()
        {
            // ★★ T-15-7이 "이 라운드에서 가장 중요한 한 줄"이라고 적은 테스트다.
            //    위 테스트만 있으면 "창이 걸려서 5,760"인지 "다른 이유로 우연히 5,760"인지
            //    구별할 수 없다 — 이 저장소가 반복해 당한 형태가 정확히 그것이다
            //    (실패한 측정과 성공한 측정이 똑같이 생겼다).
            int leaked = SimulateOneDay(double.PositiveInfinity);
            int capped = SimulateOneDay(CurrencyRules.IdleWindowCapSeconds);
            int expectedLeak = 24 * 60 * CurrencyRules.IdleCoinsPerMinute;

            Assert.AreEqual(expectedLeak, leaked,
                "창을 없앴는데도 하루치가 다 새지 않았습니다 — 그러면 위 테스트의 '잘렸다'가 " +
                "창의 성과라는 증거가 없습니다.");
            Assert.Greater(leaked, capped,
                "창이 있을 때와 없을 때가 같습니다 — 창이 아무 일도 하지 않고 있습니다.");
        }

        [Test]
        public void 창은_지급이_일어난_초만_갉는다()
        {
            // T-15-1-a · T-D-13 — 상한에 걸려 한 푼도 못 버는 동안 창이 닳으면 그건 버그다.
            CurrencyRules.IdleTickResult atCap = CurrencyRules.IdleTick(
                60.0, true,
                todayGrantedCoins: CurrencyRules.HardCeilingCoins,
                potionsUsedToday: CurrencyRules.MaxPotionsPerDay,
                idleWindowUsedSeconds: 0.0,
                idleWindowCapSeconds: CurrencyRules.IdleWindowCapSeconds,
                carryCoins: 0.0);
            Assert.AreEqual(0, atCap.CoinsGranted);
            Assert.AreEqual(0.0, atCap.WindowSecondsSpent, 1e-9,
                "상한에 걸려 한 푼도 못 버는데 창이 닳았습니다 — 정상 사용자가 아무 이득 없이 창을 잃습니다.");

            CurrencyRules.IdleTickResult notIdle = CurrencyRules.IdleTick(
                60.0, false, 0, 0, 0.0, CurrencyRules.IdleWindowCapSeconds, 0.0);
            Assert.AreEqual(0.0, notIdle.WindowSecondsSpent, 1e-9,
                "집중 세션 중(비유휴)인데 창이 닳았습니다.");

            // 음성 대조 — 정상 조건에서는 실제로 갉는다(위 두 0이 "함수가 늘 0"이 아님을 보인다).
            CurrencyRules.IdleTickResult normal = CurrencyRules.IdleTick(
                60.0, true, 0, 0, 0.0, CurrencyRules.IdleWindowCapSeconds, 0.0);
            Assert.AreEqual(60.0, normal.WindowSecondsSpent, 1e-9);
            Assert.AreEqual(CurrencyRules.IdleCoinsPerMinute, normal.CoinsGranted);
        }

        /// <summary>프레임 단위 틱을 1분치 돌린다. <paramref name="keepCarry"/>가 false면
        /// 소수분을 버리는 <b>옛 형태</b>(security T-3-c가 적은 "적게 쌓이는 방향")를 재현한다.</summary>
        private static int SimulateOneMinuteOfFrames(bool keepCarry)
        {
            const double frame = 1.0 / 60.0;
            int total = 0;
            double carry = 0.0;
            double windowUsed = 0.0;

            for (int i = 0; i < 60 * 60; i++)
            {
                CurrencyRules.IdleTickResult tick = CurrencyRules.IdleTick(
                    frame, true, 0, 0, windowUsed, CurrencyRules.IdleWindowCapSeconds, carry);
                total += tick.CoinsGranted;
                windowUsed += tick.WindowSecondsSpent;
                carry = keepCarry ? tick.CarryCoins : 0.0;
            }
            return total;
        }

        [Test]
        public void 짧은_틱이_반복돼도_절단으로_동전이_사라지지_않는다()
        {
            // 60fps 한 틱의 수입은 0.0033동전이라 (int) 절단이 <b>전부</b> 먹어 버릴 수 있다 —
            // 그러면 하루 종일 켜 둬도 0원이다. 소수분 캐리가 그것을 막는다.
            int withCarry = SimulateOneMinuteOfFrames(true);

            // ★ 등호를 걸지 않는다(걸면 안 된다): 3,600번의 double 덧셈에서 마지막 소수분이
            //   1동전 문턱을 못 넘을 수 있다. 이 테스트가 잠그는 것은 "정확히 12"가 아니라
            //   <b>"수입이 증발하지 않는다"</b>이고, 손실 상한은 <b>1동전 미만</b>이다.
            //   (실측 오차는 하루로 늘려도 요율 1분치를 넘지 않는다 — 캐리가 오차를 누적시키지 않고
            //    매 틱 정수분을 떼어 내기 때문이다.)
            Assert.GreaterOrEqual(withCarry, CurrencyRules.IdleCoinsPerMinute - 1,
                "프레임 단위 틱에서 분당 요율의 1동전 안쪽으로도 못 들어왔습니다 — 절단으로 수입이 샙니다.");
            Assert.LessOrEqual(withCarry, CurrencyRules.IdleCoinsPerMinute,
                "프레임 단위 틱이 분당 요율보다 많이 줬습니다 — 캐리가 중복 지급되고 있습니다.");

            // ★ 음성 대조 — 캐리를 버리면 실제로 <b>0원</b>이 된다는 것을 보인다.
            //   이게 없으면 위 단언이 "캐리 덕분"인지 "원래 그런지" 구별되지 않는다.
            Assert.AreEqual(0, SimulateOneMinuteOfFrames(false),
                "소수분을 버려도 동전이 나왔습니다 — 그러면 위 단언이 캐리의 성과라는 증거가 없습니다.");
        }

        // ====================================================================
        // 4. 날짜 롤오버는 「하나의 사건」이다 (T-D-10 · T-D-14 · I-15′)
        // ====================================================================

        [Test]
        public void 리필은_일자_전진과_최소_간격을_둘_다_요구한다()
        {
            const double now = 1000.0;

            // 앱을 새로 켠 직후(직전 리필 = -∞) 첫 리필은 <b>항상</b> 통과한다 — 오탐 0의 근거.
            Assert.IsTrue(CurrencyRules.MayRefill(20_000, 19_999, now, double.NegativeInfinity));

            // (카) 공격: 같은 프로세스에서 20초 뒤 날짜를 또 넘긴다 → 20시간 미달로 차단.
            Assert.IsFalse(CurrencyRules.MayRefill(20_001, 20_000, now + 20.0, now),
                "세션 안에서 날짜를 반복해 넘기는 (카) 공격이 통과했습니다.");

            // 간격을 채우면 통과한다(음성 대조 — "항상 거짓"이 아니다).
            Assert.IsTrue(CurrencyRules.MayRefill(20_001, 20_000,
                now + CurrencyRules.MinRefillGapSeconds, now));

            // 시계를 되감아 일자가 후퇴하면 리필하지 않는다(T-4-b 래칫).
            Assert.IsFalse(CurrencyRules.MayRefill(19_998, 20_000,
                now + CurrencyRules.MinRefillGapSeconds, now),
                "일자가 후퇴했는데 리필했습니다 — 「오늘 연장」 공격이 열립니다.");
        }

        [Test]
        public void 롤오버는_상한과_무료회복제와_창과_채널카운터를_한꺼번에_되돌린다()
        {
            // ★ I-15′ — 셋을 다른 조건으로 나누면 (카) 공격이 각 경계를 따로 넘어 상금이 배가 되고,
            //   T-14-3-a 방어를 세 곳에 걸어야 한다. 한 곳만 빠뜨려도 조용히 새는 문이 된다.
            Assert.IsTrue(CurrencyModel.TryUsePotion());
            Assert.Greater(CurrencyModel.TickIdleIncome(120.0, true), 0, "전제 — 유휴 수급이 있어야 한다.");
            Assert.Greater(CurrencyModel.TryPayTodoDailyCoins(), 0, "전제 — 할일 보상이 있어야 한다.");
            Assert.Greater(CurrencyModel.TryAwardArcheryCoins(0.0), 0, "전제 — 활쏘기 상금이 있어야 한다.");

            int balanceBefore = CurrencyModel.CoinBalance;
            Assert.Greater(CurrencyModel.TodayGrantedCoins, 0);
            Assert.Greater(CurrencyModel.IdleWindowUsedSeconds, 0.0);
            Assert.Greater(CurrencyModel.ArcheryCoinsToday, 0);

            // 어제까지 본 것으로 만들고, 프로세스가 방금 켜진 상태(직전 리필 -∞)에서 한 번 굴린다.
            CurrencyModel.SetDayIndexForTesting(0);
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "롤오버가 일어나지 않았습니다.");

            Assert.AreEqual(0, CurrencyModel.TodayGrantedCoins, "① 일일 상한이 리셋되지 않았습니다.");
            Assert.AreEqual(0, CurrencyModel.PotionsUsedToday, "② 무료 회복제가 부활하지 않았습니다.");
            Assert.AreEqual(0.0, CurrencyModel.IdleWindowUsedSeconds, 1e-9, "③ 8시간 창이 리셋되지 않았습니다.");
            Assert.IsFalse(CurrencyModel.TodoCoinPaidToday, "[오늘 할일] 카운터가 리셋되지 않았습니다.");
            Assert.AreEqual(0, CurrencyModel.ArcheryCoinsToday, "활쏘기 카운터가 리셋되지 않았습니다.");

            // ★ 그러나 <b>잔액은 건드리지 않는다</b> — 리셋되는 것은 "오늘의 예산"이지 "지갑"이 아니다.
            Assert.AreEqual(balanceBefore, CurrencyModel.CoinBalance,
                "롤오버가 지갑까지 비웠습니다 — 하루가 지날 때마다 번 돈이 사라집니다.");

            // 두 번째 롤오버는 같은 프로세스에서 즉시 일어나지 않는다(T-14-3-a).
            CurrencyModel.SetDayIndexForTesting(CurrencyModel.DayIndex - 1);
            Assert.IsFalse(CurrencyModel.TickDayRollover(20.0),
                "같은 프로세스에서 20초 만에 두 번째 리필이 일어났습니다 — (카) 공격이 그대로 통합니다.");
        }

        [Test]
        public void 날짜_경계_오프셋은_첫_판정에서_고정되고_다시_안_바뀐다()
        {
            Assert.IsFalse(CurrencyModel.HasDayBoundaryOffset, "전제 — 아직 고정되지 않아야 한다.");
            CurrencyModel.TickDayRollover(0.0);

            Assert.IsTrue(CurrencyModel.HasDayBoundaryOffset,
                "첫 판정에서 오프셋이 고정되지 않았습니다 — 시간대를 옮길 때마다 하루 경계가 흔들립니다(T-D-4).");
            int fixedOffset = CurrencyModel.DayBoundaryOffsetMinutes;

            CurrencyModel.TickDayRollover(CurrencyRules.MinRefillGapSeconds * 2.0);
            Assert.AreEqual(fixedOffset, CurrencyModel.DayBoundaryOffsetMinutes,
                "고정된 오프셋이 나중에 바뀌었습니다.");
        }

        [Test]
        public void 일자_번호는_오프셋만큼_경계가_밀린다()
        {
            var utcMidnightIsh = new System.DateTime(2026, 9, 3, 23, 30, 0, System.DateTimeKind.Utc);

            int utc = CurrencyRules.LocalDayIndex(utcMidnightIsh, 0);
            int aheadOneHour = CurrencyRules.LocalDayIndex(utcMidnightIsh, 60);

            Assert.AreEqual(utc + 1, aheadOneHour,
                "오프셋 +60분이 하루 경계를 넘기지 못했습니다 — 로컬 자정 판정이 UTC 자정에 묶여 있습니다.");
            Assert.AreEqual(utc, CurrencyRules.LocalDayIndex(utcMidnightIsh, -60),
                "오프셋 −60분이 엉뚱한 날로 갔습니다.");
        }

        // ====================================================================
        // 5. 채널 상한 · 쿨다운
        // ====================================================================

        [Test]
        public void 활쏘기는_세션_쿨다운과_일일_총량_두_관문을_지난다()
        {
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.TryAwardArcheryCoins(0.0));
            Assert.AreEqual(0, CurrencyModel.TryAwardArcheryCoins(1.0),
                "쿨다운 중인데 상금이 또 나왔습니다.");
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward,
                CurrencyModel.TryAwardArcheryCoins(CurrencyRules.ArcheryAwardCooldownSeconds),
                "쿨다운이 지났는데 상금이 안 나왔습니다.");

            // 일일 총량 — 쿨다운을 계속 지나도 상한에서 멈춘다(앱 재시작 파밍의 유일한 상한).
            double t = CurrencyRules.ArcheryAwardCooldownSeconds;
            for (int i = 0; i < CurrencyRules.ArcheryDailyAwardLimit + 5; i++)
            {
                t += CurrencyRules.ArcheryAwardCooldownSeconds;
                CurrencyModel.TryAwardArcheryCoins(t);
            }
            Assert.AreEqual(CurrencyRules.ArcheryDailyCoinLimit, CurrencyModel.ArcheryCoinsToday,
                "활쏘기 일일 총량이 상한을 넘었습니다.");
            Assert.IsTrue(CurrencyModel.ArcheryDailyLimitReached,
                "상한에 도달했는데 그 사실을 화면 쪽에 알릴 수 없습니다 — 연출은 계속 도는데 " +
                "동전만 안 나오면 사용자는 그걸 '고장'으로 읽습니다(§20-8).");
        }

        [Test]
        public void 할일_보상은_하루에_한_번뿐이다()
        {
            Assert.AreEqual(CurrencyRules.TodoDailyCoins, CurrencyModel.TryPayTodoDailyCoins());
            Assert.AreEqual(0, CurrencyModel.TryPayTodoDailyCoins(), "하루 1회 보상이 두 번 나왔습니다.");
        }

        /// <summary>★ <b>U-42 확정</b>(2026-09-05) — 시드는 <b>정확히 한 번, 상수만큼</b> 지급된다.
        /// <para>금액을 숫자로 베끼지 않는다. 잰 것은 ① 지급액 = 상수 ② 잔액 증가 = 상수
        /// ③ 두 번째 호출은 0이고 잔액이 <b>안 움직인다</b>(평생 1회) 셋이다.</para></summary>
        [Test]
        public void 시드는_평생_한_번_상수만큼_지급된다()
        {
            Assert.Greater(CurrencyRules.SeedCoins, 0,
                "시드가 0입니다 — 첫 실행에 상점 버튼이 전부 회색이 됩니다(§0-6-4(a)).");
            Assert.IsFalse(CurrencyModel.SeedGranted, "초기 상태가 '이미 받음'입니다.");

            int before = CurrencyModel.CoinBalance;
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.TryGrantSeedCoins(),
                "지급액이 상수와 다릅니다.");
            Assert.AreEqual(before + CurrencyRules.SeedCoins, CurrencyModel.CoinBalance,
                "지급했다고 했는데 잔액이 그만큼 안 늘었습니다.");
            Assert.IsTrue(CurrencyModel.SeedGranted);

            int afterFirst = CurrencyModel.CoinBalance;
            Assert.AreEqual(0, CurrencyModel.TryGrantSeedCoins(), "시드가 두 번 나왔습니다.");
            Assert.AreEqual(afterFirst, CurrencyModel.CoinBalance,
                "두 번째 호출이 0을 돌려주고도 잔액을 올렸습니다 — 무한 시드입니다.");
        }

        /// <summary>
        /// ★★ <b>기존 사용자도 시드를 받는다</b>(리더 승인 문구: "기존 사용자 포함 전원에게 평생 1회").
        ///
        /// <para>이게 성립하는 <b>구조적 이유</b>를 잰다: v9 이하 세이브에는 <c>seedGranted</c> 키가
        /// 아예 없어 <c>false</c>로 채워지고, 미확정 기간에 <see cref="CurrencyRules.CanGrantSeed"/>가
        /// <c>SeedCoins &gt; 0</c>을 요구했으므로 <b>플래그가 켜진 적이 없다</b>.
        /// 순서를 반대로(플래그 먼저) 했으면 되돌릴 수 없는 손실이었다.</para>
        ///
        /// <para>구버전 파일을 흉내 내려고 <c>RestoreFromSave</c>에 <b>기본값 상태</b>를 밀어 넣는다 —
        /// 그것이 곧 "그 키가 없던 파일"이 로드된 뒤의 모습이다.</para>
        /// </summary>
        [Test]
        public void 시드_플래그가_꺼진_구버전_세이브도_시드를_받는다()
        {
            CurrencyModel.RestoreFromSave(default);   // v9 파일 = 모든 신규 키가 기본값
            Assert.IsFalse(CurrencyModel.SeedGranted,
                "구버전 세이브가 '이미 받음'으로 읽혔습니다 — 기존 사용자가 시드를 영영 못 받습니다.");
            Assert.IsTrue(CurrencyRules.CanGrantSeed(CurrencyModel.SeedGranted));
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.TryGrantSeedCoins());

            // 양성 대조 — 같은 판정기가 「이미 받음」은 실제로 막는다(위 통과가 공허하지 않다).
            Assert.IsFalse(CurrencyRules.CanGrantSeed(true),
                "이미 받은 사용자에게도 지급 가능으로 나옵니다.");
        }

        // ====================================================================
        // 6. 소유 · 등급 · 댄스 집합
        // ====================================================================

        [Test]
        public void 같은_것을_두_번_살_수_없다()
        {
            // ★ 경제 원칙 E-1(I-15) — 판정 기준 한 줄: "같은 사용자가 같은 것을 두 번 살 수 있는가".
            CurrencyModel.TryPayTodoDailyCoins();
            Assert.IsTrue(CurrencyModel.TryPurchaseItem("equip.head.crown", 10));
            Assert.IsFalse(CurrencyModel.TryPurchaseItem("equip.head.crown", 10),
                "이미 산 것을 또 팔았습니다 — 반복 구매가 생기면 시계 조작 피해 상한이 즉시 무한이 됩니다.");
        }

        [Test]
        public void 잔액이_모자라면_아무_일도_일어나지_않는다()
        {
            Assert.AreEqual(0, CurrencyModel.CoinBalance);
            Assert.IsFalse(CurrencyModel.TryPurchaseItem("equip.head.crown", 1));
            Assert.IsEmpty(CurrencyModel.PurchasedItemIds, "돈을 안 냈는데 구매 이력이 생겼습니다.");
            Assert.AreEqual(0, CurrencyModel.CoinBalance, "잔액이 음수가 됐습니다.");
        }

        [Test]
        public void 등급_해금은_내려가지_않는다()
        {
            Assert.IsTrue(CurrencyModel.RaiseStatTier(0, 2));
            Assert.IsFalse(CurrencyModel.RaiseStatTier(0, 1), "영구 해금이 내려갔습니다.");
            Assert.AreEqual(2, CurrencyModel.StatTierReached(0));

            Assert.IsTrue(CurrencyModel.RaiseStatTier(0, 9999), "위쪽으로는 올라가야 합니다.");
            Assert.AreEqual(CurrencyRules.MaxStatTier, CurrencyModel.StatTierReached(0),
                "등급이 상한을 넘겼습니다.");
        }

        [Test]
        public void 평생_등급_갱신_횟수는_슬롯수_곱하기_최대등급이다()
        {
            // §20-1 #4가 적은 "평생 최대 12회 갱신"이 상수 조합과 어긋나면 둘 중 하나가 낡은 것이다.
            Assert.AreEqual(12, CurrencyRules.StatTierSlotCount * CurrencyRules.MaxStatTier,
                "슬롯 수 × 최대 등급이 설계 문서의 '평생 최대 12회'와 다릅니다 — " +
                "눈금(25/50/80%) 개수나 슬롯 수가 바뀌었다면 §20-1 표도 함께 고쳐야 합니다.");
        }

        [Test]
        public void 장착한_춤은_어떤_경로로도_빈_집합이_되지_않는다()
        {
            CollectionAssert.AreEqual(DanceIds.CreateFreeDefaults(), DanceIds.Normalize(null, null),
                "없음(null)이 무료 2종으로 채워지지 않았습니다.");
            CollectionAssert.AreEqual(DanceIds.CreateFreeDefaults(),
                DanceIds.Normalize(new string[0], null),
                "빈 배열이 null과 다르게 처리됐습니다 — 두 값은 같은 뜻입니다(§20-2-b).");
            CollectionAssert.AreEqual(DanceIds.CreateFreeDefaults(),
                DanceIds.Normalize(new[] { "dance.somethingThatDoesNotExist" }, null),
                "모르는 아이디만 남았는데 빈 집합이 됐습니다.");

            // ★ 3단계 — 보유 필터가 전부 털어 가는 경우(팩 환불 · 엔타이틀먼트 조회 실패).
            CollectionAssert.AreEqual(DanceIds.CreateFreeDefaults(),
                DanceIds.Normalize(new[] { DanceIds.Moonwalk, DanceIds.Robot }, _ => false),
                "보유 필터가 전부 털어 갔는데 무료 2종으로 되돌아가지 않았습니다 — " +
                "|장착| = 0이면 음악이 나와도 아무 일이 안 일어나고, 그건 '고장'으로 읽힙니다.");

            // 음성 대조 — 정상 입력은 그대로 통과한다("항상 무료 2종"이 아니다).
            CollectionAssert.AreEqual(new[] { DanceIds.Moonwalk },
                DanceIds.Normalize(new[] { DanceIds.Moonwalk }, null),
                "정상 입력까지 기본값으로 덮였습니다.");
        }

        // ====================================================================
        // 7. T-D-15 — 창과 천장 사이 여유
        // ====================================================================

        [Test]
        public void 창은_정상_사용자에게_먼저_걸리지_않는다()
        {
            Assert.GreaterOrEqual(CurrencyRules.WindowToCeilingRatio, CurrencyRules.MinWindowToCeilingRatio,
                "창(길이 × 요율)이 하루 절대 천장의 1.5배 아래로 내려왔습니다 — 그러면 " +
                "<b>창이 정상 사용자에게 먼저 걸려</b> 오탐이 기본값이 됩니다(T-D-15). " +
                "요율·상한·창 중 하나를 바꿨다면 나머지도 함께 재검토해야 합니다.");
        }

        [Test]
        public void 유휴_요율은_집중_요율의_정확히_절반이다()
        {
            Assert.AreEqual(CurrencyRules.FocusCoinsPerMinute, CurrencyRules.IdleCoinsPerMinute * 2,
                "유휴가 집중의 절반이 아닙니다 — §18-2가 정한 관계이고, 이게 깨지면 " +
                "'켜 두기만 해도 집중과 같다'가 되어 집중 모드의 존재 이유가 사라집니다.");
        }

        // ====================================================================
        // 8. 상점 가격 — 등급 파생 (U-17 확정 §21-5 · §21-10-a (4))
        // ====================================================================

        /// <summary>사다리가 <b>단조 증가</b>하고 어느 단도 공짜가 아니다.
        /// <para>기대값을 숫자로 베끼지 않는다 — 등급 사다리의 <b>모양</b>만 잰다.
        /// 값 자체는 아래 U-17 테스트가 상수 하나로 못박는다.</para></summary>
        [Test]
        public void 가격은_등급이_오를수록_반드시_비싸진다()
        {
            var ladder = (ItemRarity[])System.Enum.GetValues(typeof(ItemRarity));
            System.Array.Sort(ladder);
            Assert.Greater(ladder.Length, 1, "등급이 하나뿐입니다 — 아래 단조성 단언이 공허합니다.");

            int previous = 0;
            foreach (ItemRarity r in ladder)
            {
                int price = CurrencyRules.PriceCoins(r);
                Assert.Greater(price, previous,
                    $"{ItemCatalog.RarityName(r)} 가격({price})이 아래 단({previous}) 이하입니다 " +
                    "— 등급이 올라가는데 더 싸지면 사다리가 뒤집힙니다.");
                previous = price;
            }
        }

        /// <summary>★ <b>U-17</b> — 전설 가격이 사다리의 최상단이고, 그 값이 상수 하나에서만 온다.
        /// <para>9,600을 <b>여기에 숫자로 적지 않는다</b>. 값이 바뀌면 상수 한 줄만 바뀌어야 하고,
        /// 그때 이 테스트가 <b>함께 빨개지면 안 된다</b>(CLAUDE.md — 무관한 테스트가 함께 빨개지면
        /// 고치는 사람이 "숫자만 맞추면 되는 잡음"으로 학습한다).</para></summary>
        [Test]
        public void 전설_가격은_사다리_최상단이고_출처가_하나다()
        {
            Assert.AreEqual(CurrencyRules.LegendaryPriceCoins,
                CurrencyRules.PriceCoins(ItemRarity.Legendary),
                "PriceCoins가 전설 상수와 다른 값을 냅니다 — 가격의 출처가 둘이 됐습니다.");

            foreach (ItemRarity r in (ItemRarity[])System.Enum.GetValues(typeof(ItemRarity)))
            {
                if (r == ItemRarity.Legendary) continue;
                Assert.Less(CurrencyRules.PriceCoins(r), CurrencyRules.LegendaryPriceCoins,
                    $"{ItemCatalog.RarityName(r)}이 전설보다 비쌉니다.");
            }
        }

        /// <summary>모르는 등급 값이 들어와도 <b>공짜가 되지 않는다</b>.
        /// 0을 돌려주면 손상된 데이터가 곧 무료 아이템이 된다.</summary>
        [Test]
        public void 알_수_없는_등급도_공짜가_되지_않는다()
        {
            Assert.AreEqual(CurrencyRules.CommonPriceCoins, CurrencyRules.PriceCoins((ItemRarity)999),
                "범위 밖 등급이 가장 싼 단으로 안 떨어집니다.");
            Assert.Greater(CurrencyRules.PriceCoins((ItemRarity)(-1)), 0, "음수 등급이 공짜가 됐습니다.");
        }
    }
}
