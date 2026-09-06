using System;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 집중 모드 지급의 <b>floor 위치</b>를 잠근다 (DESIGN_SYSTEMS_STATS §22-12 · DS-5′).
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 것 — 「두 식이 조용히 같아지는 것」
    /// ============================================================================
    /// <code>
    ///   완주 = floor( FocusCoinsPerMinute × 경과초 / 60 )        ← 마지막에 내림
    ///   취소 = floor( 경과초 / 60 ) × FocusCancelCoinsPerMinute  ← 분을 먼저 내림
    /// </code>
    /// 두 자리의 내림 위치가 <b>다른 것이 의도</b>이고, 그 비대칭이 사라지면 취소 지급이 초 단위로
    /// 연속이 되어 사용자가 취소 타이밍을 초 단위로 재는 동기가 생긴다.
    ///
    /// <para>★★ <b>「완주값 ≠ 취소값」으로 짜면 아무것도 안 잡는다.</b> 요율이 24와 20으로 이미 다르므로
    /// 두 식의 내림 위치를 <b>서로 바꿔도</b> 그 단언은 계속 통과한다. 그래서 이 파일은 완주와 취소를
    /// 비교하지 않고, <b>취소를 「기각된 해석」과 대조</b>한다 — 그것이 실제로 갈리는 축이다.</para>
    ///
    /// ============================================================================
    /// ★ 대조가 살아 있는지를 매번 확인한다 (부재 단언이 조용히 썩지 않게)
    /// ============================================================================
    /// <c>Assert.AreNotEqual(확정, 기각)</c>류는 <b>두 값이 우연히 같아지는 순간 조용히 초록</b>이 된다.
    /// 실제로 그런 지점이 있다 — <b>경과 1,500초에서는 두 해석이 둘 다 500</b>이라 대조가 죽는다.
    /// 그래서 모든 대조 케이스는 <b>먼저 두 해석이 실제로 갈리는지를 단언</b>하고 시작한다
    /// (CLAUDE.md: 부재 단언은 썩어도 빨개지지 않는다).
    ///
    /// ============================================================================
    /// ★ 요율을 숫자로 베끼지 않는다
    /// ============================================================================
    /// 기대값은 전부 <see cref="CurrencyRules.FocusCoinsPerMinute"/> ·
    /// <see cref="CurrencyRules.FocusCancelCoinsPerMinute"/>에서 <b>이 파일 안에서 계산</b>한다.
    /// §2-3의 「집중모드 성과물 배율 ×1.15/×1.35/×1.60」이 동전에 걸리는 날, 프로덕션만 배율을 먹고
    /// 상수가 그대로면 <b>이 파일이 먼저 빨개진다</b>(§4325가 요구하는 것이 정확히 그것이다).
    /// </summary>
    public sealed class FocusSessionPayoutTests
    {
        [SetUp]
        public void Reset() => CurrencyModel.ResetForTesting();

        // 문서 §22-12 표가 쓴 두 경과값. 여기서 두 해석이 확실히 갈린다.
        private const double ElapsedA = 90.5;
        private const double ElapsedB = 149.9;

        /// <summary>확정된 취소 해석 (가) — 분을 먼저 내린 뒤 요율.</summary>
        private static int CancelAccepted(double elapsedSeconds)
            => (int)(Math.Floor(elapsedSeconds / 60.0) * CurrencyRules.FocusCancelCoinsPerMinute);

        /// <summary>★ <b>기각된</b> 취소 해석 (나) — 동전에 내림. 프로덕션이 이걸로 미끄러지는지 본다.</summary>
        private static int CancelRejected(double elapsedSeconds)
            => (int)Math.Floor(CurrencyRules.FocusCancelCoinsPerMinute * elapsedSeconds / 60.0);

        /// <summary>확정된 완주 해석 — 초를 그대로 읽고 마지막에 내림.</summary>
        private static int CompletionExpected(double seconds)
            => (int)Math.Floor(CurrencyRules.FocusCoinsPerMinute * seconds / 60.0);

        // ====================================================================
        // 1. 완주 — 초를 그대로 읽고 「마지막에」 내린다
        // ====================================================================

        [Test]
        public void 완주는_초를_그대로_읽고_마지막에_내린다()
        {
            int expected = CompletionExpected(ElapsedA);

            // 문서 §22-12 검산값과 3자 대조. 이 줄이 빨개지면 요율 상수가 움직인 것이고,
            // 그때는 문서(§13-3 표)와 이 파일을 함께 고쳐야 한다 — 조용히 따라가지 않는다.
            Assert.AreEqual(36, expected,
                "요율이 바뀌었습니다. DESIGN_SYSTEMS_STATS §22-12의 90.5초 검산값(완주 36)과 갈라졌습니다.");

            Assert.AreEqual(expected, CurrencyRules.FocusCompletionCoins(ElapsedA),
                "완주 지급이 «초를 그대로 읽고 마지막에 내림»이 아닙니다(DS-5′).");
        }

        [Test]
        public void 완주_25분은_정확히_한_등급분이다()
        {
            // 정본 검산: 25분 = 600동전 = 일반 등급 1개(§13-3).
            const double twentyFiveMinutes = 25.0 * 60.0;
            int paid = CurrencyRules.FocusCompletionCoins(twentyFiveMinutes);

            Assert.AreEqual(CurrencyRules.FocusCoinsPerMinute * 25, paid,
                "분당 요율 × 분과 어긋납니다 — 완주 경로에 절단이 생겼습니다.");
            Assert.AreEqual(CurrencyRules.CommonPriceCoins, paid,
                "§13-3의 «25분 = 600 = 일반 1개» 검산이 깨졌습니다. 요율이나 가격 한쪽이 움직였습니다.");
        }

        // ====================================================================
        // 2. ★ 취소 — 「분을 먼저」 내린다. 기각된 해석과 대조로 못박는다
        // ====================================================================

        [Test]
        public void 취소는_분을_먼저_내린_뒤_요율을_곱한다_90_5초()
        {
            int accepted = CancelAccepted(ElapsedA);
            int rejected = CancelRejected(ElapsedA);

            // ★ 대조가 살아 있는가 — 이 줄이 없으면 두 해석이 같아지는 날 조용히 초록이 된다.
            Assert.AreNotEqual(accepted, rejected,
                $"대조가 죽었습니다 — {ElapsedA}초에서 두 해석이 같은 값({accepted})을 냅니다. " +
                "이 테스트는 더 이상 내림 위치를 검사하지 못합니다.");

            Assert.AreEqual(20, accepted, "§22-12 표의 «90.5초 → 20동전»(해석 가)과 갈라졌습니다.");
            Assert.AreEqual(30, rejected, "§22-12 표의 «90.5초 → 30동전»(해석 나)과 갈라졌습니다.");

            Assert.AreEqual(accepted, CurrencyRules.FocusCancelCoins(ElapsedA),
                "취소 지급이 «분을 먼저 내림»이 아닙니다(DS-5′).");
            Assert.AreNotEqual(rejected, CurrencyRules.FocusCancelCoins(ElapsedA),
                "★ 취소 지급이 기각된 해석(동전에 내림)으로 미끄러졌습니다 — 1.5배 과지급입니다.");
        }

        [Test]
        public void 취소는_분을_먼저_내린_뒤_요율을_곱한다_149_9초()
        {
            int accepted = CancelAccepted(ElapsedB);
            int rejected = CancelRejected(ElapsedB);

            Assert.AreNotEqual(accepted, rejected,
                $"대조가 죽었습니다 — {ElapsedB}초에서 두 해석이 같은 값({accepted})을 냅니다.");

            Assert.AreEqual(40, accepted, "§22-12 표의 «149.9초 → 40동전»(해석 가)과 갈라졌습니다.");
            Assert.AreEqual(49, rejected, "§22-12 표의 «149.9초 → 49동전»(해석 나)과 갈라졌습니다.");

            Assert.AreEqual(accepted, CurrencyRules.FocusCancelCoins(ElapsedB));
            Assert.AreNotEqual(rejected, CurrencyRules.FocusCancelCoins(ElapsedB),
                "★ 취소 지급이 기각된 해석으로 미끄러졌습니다.");
        }

        [Test]
        public void 취소_1분_미만은_0동전이고_그것이_기각된_해석과_가장_크게_갈린다()
        {
            const double justUnderAMinute = 59.9;

            // 여기가 두 해석이 «0 vs 19»로 갈리는 자리다 — 하한 규칙 없이도 스팸 바닥이 막히는 근거.
            Assert.AreEqual(0, CancelAccepted(justUnderAMinute));
            Assert.Greater(CancelRejected(justUnderAMinute), 0,
                "대조가 죽었습니다 — 기각된 해석도 0을 내면 이 케이스는 아무것도 구분하지 못합니다.");

            Assert.AreEqual(0, CurrencyRules.FocusCancelCoins(justUnderAMinute),
                "★ 1분 미만 취소에서 동전이 나왔습니다 — 취소 스팸의 바닥이 뚫렸습니다(§22-12).");
        }

        // ====================================================================
        // 3. 관계 — 상수 둘이 따로 움직이면 여기가 먼저 빨개진다
        // ====================================================================

        [Test]
        public void 취소_요율은_완주의_83_3퍼센트다()
        {
            // 20/24 = 5/6. 정수 곱으로 비교해 부동소수 오차를 아예 없앤다.
            Assert.AreEqual(CurrencyRules.FocusCoinsPerMinute * 5,
                CurrencyRules.FocusCancelCoinsPerMinute * 6,
                "§13-3이 정한 «취소 = 완주의 83.3%» 관계가 깨졌습니다. 두 상수 중 하나만 바뀌었습니다.");

            Assert.Less(CurrencyRules.FocusCancelCoinsPerMinute, CurrencyRules.FocusCoinsPerMinute,
                "취소가 완주보다 이득이면 완주할 이유가 사라집니다.");
        }

        [Test]
        public void 어떤_취소_주기도_완주_시급을_넘지_못한다()
        {
            const double oneHour = 3600.0;
            int completionPerHour = CurrencyRules.FocusCompletionCoins(oneHour);

            // §22-12 표의 최적 취소 주기(1.0분)를 한 시간 동안 반복한다.
            int spamPerHour = 0;
            for (int i = 0; i < 60; i++) spamPerHour += CurrencyRules.FocusCancelCoins(60.0);

            Assert.AreEqual(CurrencyRules.FocusCancelCoinsPerMinute * 60, spamPerHour,
                "1분 주기 취소 스팸의 시급이 §22-12 표(1,200)와 갈라졌습니다.");
            Assert.Less(spamPerHour, completionPerHour,
                "★ 취소 스팸이 완주보다 이득입니다 — 경제가 뒤집혔습니다.");

            // 0.99분 주기는 한 푼도 못 번다 — 하한 규칙이 없어도 되는 이유.
            int subMinuteSpam = 0;
            for (int i = 0; i < 60; i++) subMinuteSpam += CurrencyRules.FocusCancelCoins(59.4);
            Assert.AreEqual(0, subMinuteSpam, "1분 미만 주기 스팸에서 동전이 나왔습니다.");
        }

        // ====================================================================
        // 4. 모델 — 잔액에 실리고, 「일일 상한 밖」이다 (§22-13 · §18-5)
        // ====================================================================

        [Test]
        public void 집중_지급은_일일_상한_밖이라_캡을_다_채운_뒤에도_전액_들어온다()
        {
            // 전제 — 유휴 수급으로 오늘 상한을 끝까지 채운다.
            CurrencyModel.TickIdleIncome(CurrencyRules.IdleWindowCapSeconds, true, out _);
            Assert.AreEqual(0, CurrencyModel.RemainingDailyRoomCoins(),
                "전제가 성립하지 않았습니다 — 일일 상한이 다 차지 않았습니다.");

            int balanceBefore = CurrencyModel.CoinBalance;
            int grantedBefore = CurrencyModel.TodayGrantedCoins;
            double windowBefore = CurrencyModel.IdleWindowUsedSeconds;

            const double twentyFiveMinutes = 25.0 * 60.0;
            int paid = CurrencyModel.PayFocusCompletionCoins(twentyFiveMinutes);

            Assert.AreEqual(CurrencyRules.FocusCoinsPerMinute * 25, paid,
                "★ 집중 완주가 일일 상한에 깎였습니다 — §22-13은 «캡 도달 후에도 전액»입니다.");
            Assert.AreEqual(balanceBefore + paid, CurrencyModel.CoinBalance,
                "지급액이 잔액에 반영되지 않았습니다.");
            Assert.AreEqual(grantedBefore, CurrencyModel.TodayGrantedCoins,
                "★ 집중 지급이 유휴 버킷(TodayGrantedCoins)을 갉았습니다 — 두 버킷은 분리돼야 합니다.");
            Assert.AreEqual(windowBefore, CurrencyModel.IdleWindowUsedSeconds,
                "★ 집중 지급이 8시간 유휴 창을 갉았습니다 — 같은 1초가 두 버킷에서 소비됐습니다(I-7′).");
        }

        [Test]
        public void 취소_지급도_잔액에_실리고_0동전이면_저장을_요청하지_않는다()
        {
            Assert.IsFalse(CurrencyModel.IsDirty, "전제 — 초기화 직후에는 더티가 아니어야 합니다.");

            Assert.AreEqual(0, CurrencyModel.PayFocusCancelCoins(59.9));
            Assert.IsFalse(CurrencyModel.IsDirty,
                "0동전 취소가 더티를 세웠습니다 — 1분 미만 취소를 반복하면 디스크를 계속 두드립니다.");

            int paid = CurrencyModel.PayFocusCancelCoins(ElapsedA);
            Assert.AreEqual(CancelAccepted(ElapsedA), paid);
            Assert.AreEqual(paid, CurrencyModel.CoinBalance);
            Assert.IsTrue(CurrencyModel.IsDirty,
                "지급이 일어났는데 더티가 서지 않았습니다 — 사용자가 번 동전이 저장되지 않습니다.");
        }

        [Test]
        public void 음수와_NaN_경과는_한_푼도_만들지_않는다()
        {
            Assert.AreEqual(0, CurrencyRules.FocusCompletionCoins(0.0));
            Assert.AreEqual(0, CurrencyRules.FocusCompletionCoins(-1.0));
            Assert.AreEqual(0, CurrencyRules.FocusCompletionCoins(double.NaN));
            Assert.AreEqual(0, CurrencyRules.FocusCancelCoins(0.0));
            Assert.AreEqual(0, CurrencyRules.FocusCancelCoins(-1.0));
            Assert.AreEqual(0, CurrencyRules.FocusCancelCoins(double.NaN));

            Assert.AreEqual(0, CurrencyModel.CoinBalance, "잔액이 움직였습니다.");
            Assert.IsFalse(CurrencyModel.IsDirty);
        }

        [Test]
        public void 말도_안되는_경과_시간도_잔액을_음수로_감지_않는다()
        {
            // int 캐스트 오버플로가 나면 잔액이 음수로 감긴다 — 위조 방어가 아니라 우리 버그 방어다.
            Assert.GreaterOrEqual(CurrencyRules.FocusCompletionCoins(double.MaxValue), 0);
            Assert.GreaterOrEqual(CurrencyRules.FocusCancelCoins(double.MaxValue), 0);
            Assert.GreaterOrEqual(CurrencyRules.FocusCompletionCoins(double.PositiveInfinity), 0);

            CurrencyModel.PayFocusCompletionCoins(double.MaxValue);
            Assert.GreaterOrEqual(CurrencyModel.CoinBalance, 0, "★ 잔액이 음수가 됐습니다.");
        }
    }
}
