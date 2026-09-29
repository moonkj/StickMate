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
    ///
    /// ============================================================================
    /// ★★★ 2026-09-29 DLC·재화 폐지 R5 — <b>동전 지급을 재던 테스트를 뗐다</b>
    /// ============================================================================
    /// 뗀 것과 그 이유(전부 <b>대상 소멸</b>이다 — 판정이 바뀐 것이 아니다):
    /// <list type="bullet">
    ///   <item>§3 <b>8시간 창 래칫 전체</b>(하루 시뮬레이션 · 창 없음 음성 대조 · 「지급된 초만 갉는다」 ·
    ///     프레임 단위 소수분 캐리) — <c>CurrencyRules.IdleTick</c>/<c>IdleTickResult</c>가 삭제됐다.
    ///     ⚠ 그 절이 잡던 것 중 <b>「음성 대조 없이는 5,760이 창의 성과인지 알 수 없다」</b>는 방법론은
    ///     이 파일 다른 곳(<c>창_소비량의_NaN…</c>의 음성 대조)과 <c>FocusXpPayoutTests</c>에 남아 있다.</item>
    ///   <item>회복제 사용(<c>TryUsePotion</c>) · [오늘 할일] 정액 · 첫 실행 시드 3건 —
    ///     지급 API가 삭제됐다. ★ <b>클램프는 남았고 여기서 계속 잰다</b>(§1·§2) — 폐기 필드를
    ///     왕복시킬 때의 위생이기 때문이다.</item>
    ///   <item>T-D-15 창/천장 비율 · 「유휴는 집중의 절반」 — 분자인 유휴 요율이 삭제됐다.</item>
    /// </list>
    /// <para>★ 남긴 것: 하루 경계/리필 래칫 · 활쏘기 두 관문(<b>이제 XP 방어선이다</b>) · 등급 래칫 ·
    /// 댄스 정규화 · 잔액 하한 · 창 NaN 방향 · 상점 가격 사다리(죽은 잔재, 다음 라운드에 함께 삭제).</para>
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

        // ★ 2026-09-29 — 여기 있던 <c>하루에_쓸_수_있는_회복제는_상수만큼이다</c>를 뗐다.
        //   <c>CurrencyModel.TryUsePotion()</c>과 <c>CurrencyModel.DailyCapCoins()</c>가 삭제됐다
        //   (폐지 시점에 <c>TryUsePotion</c>의 프로덕션 호출부는 이미 0이었다).
        //   ⚠ 그 테스트가 지키던 <b>위조 방어</b>는 바로 위 <c>회복제_개수를_위조해도…</c>가 계속 잰다 —
        //   그쪽은 순수 클램프만 부르므로 모델 API가 없어도 살아 있다.

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
        // 3. 8시간 창 래칫 — ★★★ 2026-09-29 <b>절 전체 폐지</b>(T-15-7 (B)의 대상이 사라졌다)
        // ====================================================================
        //
        // 뗀 것: 헬퍼 <c>SimulateOneDay(double)</c>·<c>SimulateOneMinuteOfFrames(bool)</c>와
        //   테스트 4개(<c>창이_있으면_하루_수입이_창_길이에서_잘린다</c> ·
        //   <c>음성대조_창을_없애면_실제로_하루치가_전부_샌다</c> ·
        //   <c>창은_지급이_일어난_초만_갉는다</c> · <c>짧은_틱이_반복돼도_절단으로_동전이_사라지지_않는다</c>).
        //   전부 <c>CurrencyRules.IdleTick</c>을 부르고, 그 함수가 삭제됐다.
        //
        // ★★ <b>여기서 나온 방법론 셋은 기록으로 남긴다</b>(다음 상한·창 기능에서 그대로 필요하다):
        //   ① 「잘렸다」만 재면 <b>창이 잘랐는지 다른 이유로 우연히 같은 값인지</b> 알 수 없다 —
        //      상한을 <c>PositiveInfinity</c>로 준 <b>음성 대조</b>가 그것을 가른다(T-15-7이 「이 라운드에서
        //      가장 중요한 한 줄」이라고 적은 테스트가 그쪽이었다).
        //   ② 프레임 단위 틱은 <b>(int) 절단으로 전부 증발</b>할 수 있다(60fps 한 틱 = 0.0033). 소수분
        //      캐리를 버린 형태를 함께 돌려 «0원이 된다»를 보여야 캐리의 성과가 증명된다.
        //   ③ 그 단언에 <b>등호를 걸지 않는다</b> — 3,600번의 double 덧셈에서 마지막 소수분이 1 문턱을
        //      못 넘을 수 있고, 잠글 것은 「정확히 12」가 아니라 「증발하지 않는다」였다.

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

        /// <summary>
        /// ★ I-15′ — 여러 일일 카운터를 다른 조건으로 나누면 (카) 공격이 각 경계를 따로 넘어 보상이
        /// 배가 되고, T-14-3-a 방어를 여러 곳에 걸어야 한다. 한 곳만 빠뜨려도 조용히 새는 문이 된다.
        ///
        /// <para>★★ <b>2026-09-29 — 전제를 만드는 방법이 둘로 갈렸다.</b> 살아 있는 축(활쏘기)은
        /// 여전히 <b>실제 API</b>로 만들고, 폐기된 축 셋(<c>todayGrantedCoins</c>·<c>potionsUsedToday</c>·
        /// <c>idleWindowUsedSeconds</c>·<c>todoCoinPaidToday</c>)은 지급 API가 없으므로
        /// <b>파일에서 읽은 값</b>으로 세운다. 폐기한 축을 단언에서 빼지 <b>않은</b> 이유:
        /// 롤오버가 그것들을 계속 0으로 되돌린다는 것이 <see cref="CurrencyModel"/>의 명시적 계약이고
        /// (「예외를 만들면 I-15′가 부분적으로만 참인 문장이 된다」), 그 계약이 조용히 깨지면
        /// 재화가 다시 붙는 날 상한이 열린 채로 출하된다.</para>
        /// </summary>
        [Test]
        public void 롤오버는_상한과_무료회복제와_창과_채널카운터를_한꺼번에_되돌린다()
        {
            CurrencyModel.RestoreFromSave(new CurrencySaveState
            {
                CoinBalance = 777,
                TodayGrantedCoins = CurrencyRules.BaseDailyCapCoins / 2,
                PotionsUsedToday = 1,
                IdleWindowUsedSeconds = 120f,
                TodoCoinPaidToday = true,
            });
            Assert.Greater(CurrencyModel.TryClaimArcheryAward(0.0), 0, "전제 — 활쏘기 보상 판정이 통과해야 한다.");

            int balanceBefore = CurrencyModel.CoinBalance;
            Assert.Greater(CurrencyModel.TodayGrantedCoins, 0);
            Assert.Greater(CurrencyModel.PotionsUsedToday, 0);
            Assert.Greater(CurrencyModel.IdleWindowUsedSeconds, 0.0);
            Assert.IsTrue(CurrencyModel.TodoCoinPaidToday);
            Assert.Greater(CurrencyModel.ArcheryCoinsToday, 0);

            // 어제까지 본 것으로 만들고, 프로세스가 방금 켜진 상태(직전 리필 -∞)에서 한 번 굴린다.
            CurrencyModel.SetDayIndexForTesting(0);
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "롤오버가 일어나지 않았습니다.");

            Assert.AreEqual(0, CurrencyModel.TodayGrantedCoins, "① 일일 상한이 리셋되지 않았습니다.");
            Assert.AreEqual(0, CurrencyModel.PotionsUsedToday, "② 무료 회복제가 부활하지 않았습니다.");
            Assert.AreEqual(0.0, CurrencyModel.IdleWindowUsedSeconds, 1e-9, "③ 8시간 창이 리셋되지 않았습니다.");
            Assert.IsFalse(CurrencyModel.TodoCoinPaidToday, "[오늘 할일] 카운터가 리셋되지 않았습니다.");
            Assert.AreEqual(0, CurrencyModel.ArcheryCoinsToday,
                "활쏘기 카운터가 리셋되지 않았습니다 — 이 카운터가 안 열리면 활쏘기 XP가 " +
                "첫날 상한 이후 영구히 막힙니다(2026-09-29 이후 이 축은 XP 천장이다).");

            // ★ 그러나 <b>지갑은 건드리지 않는다</b> — 리셋되는 것은 "오늘의 예산"이지 "지갑"이 아니다.
            //   ★ 2026-09-29 — 지갑에 더하는 경로는 사라졌지만 <b>파일에서 읽은 잔액을 롤오버가 지우면
            //   안 된다</b>는 계약은 그대로다(사용자가 어제까지 번 값이 보존돼야 한다).
            Assert.AreEqual(balanceBefore, CurrencyModel.CoinBalance,
                "롤오버가 지갑까지 비웠습니다 — 하루가 지날 때마다 파일의 잔액이 사라집니다.");

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

        /// <summary>
        /// 활쏘기 정중앙 보상은 두 관문(세션 쿨다운 · 일일 총량)을 지난다.
        /// <para>★★ <b>2026-09-29 — 이 테스트의 무게가 늘었다.</b> 이 두 관문이 재던 것은 원래
        /// 「동전 파밍 상한」이었는데, 재화 폐지 뒤 <b>활쏘기 XP의 유일한 방어선</b>이 됐다
        /// (2026-09-07 보안 결함 수정이 XP를 이 판정 결과에 묶었다 — 없으면 연속 도배로 시간당
        /// ~6,478XP). 그래서 <b>여기가 빨개지면 XP 익스플로잇이 열린 것</b>이다.</para>
        /// </summary>
        [Test]
        public void 활쏘기는_세션_쿨다운과_일일_총량_두_관문을_지난다()
        {
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.TryClaimArcheryAward(0.0));
            Assert.AreEqual(0, CurrencyModel.TryClaimArcheryAward(1.0),
                "쿨다운 중인데 보상 판정이 또 통과했습니다.");
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward,
                CurrencyModel.TryClaimArcheryAward(CurrencyRules.ArcheryAwardCooldownSeconds),
                "쿨다운이 지났는데 보상 판정이 통과하지 않았습니다.");

            // 일일 총량 — 쿨다운을 계속 지나도 상한에서 멈춘다(앱 재시작 파밍의 유일한 상한).
            double t = CurrencyRules.ArcheryAwardCooldownSeconds;
            for (int i = 0; i < CurrencyRules.ArcheryDailyAwardLimit + 5; i++)
            {
                t += CurrencyRules.ArcheryAwardCooldownSeconds;
                CurrencyModel.TryClaimArcheryAward(t);
            }
            Assert.AreEqual(CurrencyRules.ArcheryDailyCoinLimit, CurrencyModel.ArcheryCoinsToday,
                "활쏘기 일일 총량이 상한을 넘었습니다 — XP 도배 상한이 뚫렸습니다.");
            Assert.IsTrue(CurrencyModel.ArcheryDailyLimitReached,
                "상한에 도달했는데 그 사실을 화면 쪽에 알릴 수 없습니다 — 연출은 계속 도는데 " +
                "보상만 안 나오면 사용자는 그걸 '고장'으로 읽습니다(§20-8).");
        }

        [Test]
        public void 활쏘기_판정은_지갑을_건드리지_않는다()
        {
            // ★★ 2026-09-29 DLC·재화 폐지 R5의 <b>핵심 회귀 잠금</b>. 이 관문을 남긴 이유는 XP 보안인데,
            //    그 사실이 「동전도 같이 되살리자」로 오해되기 쉽다. 여기서 부재를 <b>존재 대조와 함께</b>
            //    못박는다: 카운터는 늘고 지갑은 안 늘어야 한다(둘 중 하나만 재면 «아무것도 안 하는
            //    구현»과 구별되지 않는다 — 실패한 측정과 성공한 측정이 똑같이 생긴다).
            CurrencyModel.RestoreFromSave(new CurrencySaveState { CoinBalance = 500 });
            int walletBefore = CurrencyModel.CoinBalance;
            int counterBefore = CurrencyModel.ArcheryCoinsToday;

            Assert.Greater(CurrencyModel.TryClaimArcheryAward(0.0), 0, "전제 — 판정이 통과해야 한다.");

            Assert.Greater(CurrencyModel.ArcheryCoinsToday, counterBefore,
                "양성 대조 — 관문 카운터가 늘지 않았습니다. 이 테스트는 아무것도 재지 않고 있습니다.");
            Assert.AreEqual(walletBefore, CurrencyModel.CoinBalance,
                "★ 활쏘기가 다시 동전을 냅니다 — 사용자가 닫은 문(재화)을 되열었습니다.");
        }

        // ★ 2026-09-29 — 여기 있던 세 테스트를 뗐다(전부 <b>대상 소멸</b>):
        //   <c>할일_보상은_하루에_한_번뿐이다</c>(<c>TryPayTodoDailyCoins</c>·<c>TodoDailyCoins</c> 삭제) ·
        //   <c>시드는_평생_한_번_상수만큼_지급된다</c>와
        //   <c>시드_플래그가_꺼진_구버전_세이브도_시드를_받는다</c>(<c>SeedCoins</c>·<c>CanGrantSeed</c>·
        //   <c>TryGrantSeedCoins</c> 삭제).
        //   ★ 시드 쪽이 잠그던 <b>구조적 사실</b>은 이제 반대 방향으로 중요하다: 기존 사용자 전원의
        //   <c>seedGranted</c>가 아직 <c>false</c>이므로 <b>시드를 되살리면 전원에게 한 번 더 나간다</b>
        //   (<c>CurrencyRules</c>의 「첫 실행 시드」 절이 그 함정을 적어 뒀다).

        // ====================================================================
        // 6. 소유 · 등급 · 댄스 집합
        // ====================================================================

        [Test]
        public void 같은_것을_두_번_살_수_없다()
        {
            // ★ 경제 원칙 E-1(I-15) — 판정 기준 한 줄: "같은 사용자가 같은 것을 두 번 살 수 있는가".
            // ★ 2026-09-29 — 자금을 «지급»이 아니라 «파일에서 읽은 잔액»으로 만든다(지급 API 삭제).
            //   <c>TryPurchaseItem</c> 자체는 아직 죽은 잔재로 남아 있고, 상점 표면을 지우는 병렬
            //   라운드가 착지하면 이 테스트도 그 함수와 함께 사라진다.
            CurrencyModel.RestoreFromSave(new CurrencySaveState { CoinBalance = 100 });
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
        // 7. T-D-15 창/천장 여유 — ★★★ 2026-09-29 <b>절 전체 폐지</b>
        // ====================================================================
        //
        // 뗀 것: <c>창은_정상_사용자에게_먼저_걸리지_않는다</c>(<c>WindowToCeilingRatio</c>·
        //   <c>MinWindowToCeilingRatio</c> 삭제) · <c>유휴_요율은_집중_요율의_정확히_절반이다</c>
        //   (<c>IdleCoinsPerMinute</c> 삭제).
        //
        // ★ <b>상수를 남겨 두고 단언만 살리는 선택을 하지 않았다.</b> 비율의 분자가 유휴 요율이었고
        //   그 요율이 없으므로, 남기면 «참인 채로 아무것도 보장하지 않는 계기»가 된다 —
        //   이 저장소가 반복해 당한 「죽은 프로브가 산 프로브와 똑같이 생겼다」 그 형태다.

        // ====================================================================
        // 8. 상점 가격 — ★ 죽은 잔재(등급 파생). 상점 표면 라운드와 함께 삭제 예정
        // ====================================================================
        //
        // 아래 세 테스트는 <c>CurrencyRules.PriceCoins</c>와 가격 4상수를 재는데, 그 다섯은
        // 2026-09-29 현재 <b>프로덕션 호출부가 0</b>이다(상점 화면을 지우는 병렬 라운드 소관).
        // 남긴 이유: 그 라운드가 착지하기 전까지 선언이 살아 있고, 살아 있는 선언은 계기가 있어야 한다.

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
