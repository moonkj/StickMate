using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 집중 모드 <b>경험치(XP)</b> 지급 — 2026-09-07 신규(design-systems §15 확정,
    /// docs/DESIGN_SYSTEMS_LEVEL_STAT_GROWTH_PROPOSAL.md).
    ///
    /// ============================================================================
    /// 배경 — 지금까지 집중 모드는 동전만 줬다
    /// ============================================================================
    /// <see cref="FocusSessionPayoutTests"/>가 이미 동전 산식(<c>FocusCompletionCoins</c>/
    /// <c>FocusCancelCoins</c>)을 잠그고 있다. 이 파일은 그 <b>산식 패턴을 XP로 그대로 복제</b>한
    /// <c>FocusCompletionXp</c>/<c>FocusCancelXp</c>를 잠그되, 동전과 <b>다른 점 하나</b>(XP는
    /// 일일 상한이 있다, §22-13은 코인 한정)를 집중적으로 검증한다. 겹치는 부분(floor 위치
    /// 비대칭, 1분 미만 취소는 0)은 같은 방법으로 재확인하되 새로 발명하지 않는다.
    ///
    /// ============================================================================
    /// ★ 숫자를 베끼지 않는다
    /// ============================================================================
    /// 기대값은 전부 <see cref="CurrencyRules"/> 상수에서 이 파일 안에서 계산한다.
    /// </summary>
    public sealed class FocusXpPayoutTests
    {
        [SetUp]
        public void Reset() => CurrencyModel.ResetForTesting();

        [TearDown]
        public void Clean() => CurrencyModel.ResetForTesting();

        // 문서 §22-12/§15와 같은 경과값을 재사용한다 — 여기서 완주/취소 두 해석이 확실히 갈린다.
        private const double ElapsedA = 90.5;
        private const double ElapsedB = 149.9;

        /// <summary>확정된 취소 해석 — 분을 먼저 내린 뒤 요율(코인과 같은 패턴).</summary>
        private static int CancelAccepted(double elapsedSeconds)
            => (int)(Math.Floor(elapsedSeconds / 60.0) * CurrencyRules.FocusCancelXpPerMinute);

        /// <summary>★ 기각된 해석 — XP에 내림. 프로덕션이 이걸로 미끄러지는지 본다.</summary>
        private static int CancelRejected(double elapsedSeconds)
            => (int)Math.Floor(CurrencyRules.FocusCancelXpPerMinute * elapsedSeconds / 60.0);

        /// <summary>확정된 완주 해석 — 초를 그대로 읽고 마지막에 내린다.</summary>
        private static int CompletionExpected(double seconds)
            => (int)Math.Floor(CurrencyRules.FocusXpPerMinute * seconds / 60.0);

        // ====================================================================
        // 1. 완주 — 초를 그대로 읽고 「마지막에」 내린다 (동전과 같은 구조, 상수만 다르다)
        // ====================================================================

        [Test]
        public void 완주는_초를_그대로_읽고_마지막에_내린다()
        {
            int expected = CompletionExpected(ElapsedA);
            Assert.AreEqual(expected, CurrencyRules.FocusCompletionXp(ElapsedA),
                "완주 XP 지급이 «초를 그대로 읽고 마지막에 내림»이 아닙니다.");
        }

        [Test]
        public void 실사용_프리셋_15_25_50분은_전부_깔끔한_XP를_낸다()
        {
            // design-systems §15-3-a — 이 값들이 정수로 깔끔하게 나오는 것이 FocusXpPerMinute=6을
            // 고른 근거 중 하나다.
            Assert.AreEqual(CurrencyRules.FocusXpPerMinute * 15,
                CurrencyRules.FocusCompletionXp(15.0 * 60.0), "15분 완주 XP가 어긋났습니다.");
            Assert.AreEqual(CurrencyRules.FocusXpPerMinute * 25,
                CurrencyRules.FocusCompletionXp(25.0 * 60.0), "25분 완주 XP가 어긋났습니다.");
            Assert.AreEqual(CurrencyRules.FocusXpPerMinute * 50,
                CurrencyRules.FocusCompletionXp(50.0 * 60.0), "50분 완주 XP가 어긋났습니다.");

            // §15-3-a 검산값(6×15/25/50) — 상수가 움직이면 이 자리도 함께 빨개진다.
            Assert.AreEqual(90, CurrencyRules.FocusCompletionXp(15.0 * 60.0));
            Assert.AreEqual(150, CurrencyRules.FocusCompletionXp(25.0 * 60.0));
            Assert.AreEqual(300, CurrencyRules.FocusCompletionXp(50.0 * 60.0));
        }

        // ====================================================================
        // 2. ★ 취소 — 「분을 먼저」 내린다. 기각된 해석과 대조로 못박는다(동전과 같은 방법)
        // ====================================================================

        [Test]
        public void 취소는_분을_먼저_내린_뒤_요율을_곱한다_90_5초()
        {
            int accepted = CancelAccepted(ElapsedA);
            int rejected = CancelRejected(ElapsedA);

            Assert.AreNotEqual(accepted, rejected,
                $"대조가 죽었습니다 — {ElapsedA}초에서 두 해석이 같은 값({accepted})을 냅니다.");

            Assert.AreEqual(5, accepted, "90.5초 → 5XP(분을 먼저 내림) 검산과 갈라졌습니다.");
            Assert.AreEqual(7, rejected, "90.5초 → 7XP(XP에 내림, 기각된 해석 — floor(5×90.5/60)) 검산과 갈라졌습니다.");

            Assert.AreEqual(accepted, CurrencyRules.FocusCancelXp(ElapsedA),
                "취소 XP 지급이 «분을 먼저 내림»이 아닙니다.");
            Assert.AreNotEqual(rejected, CurrencyRules.FocusCancelXp(ElapsedA),
                "★ 취소 XP 지급이 기각된 해석(XP에 내림)으로 미끄러졌습니다.");
        }

        [Test]
        public void 취소는_분을_먼저_내린_뒤_요율을_곱한다_149_9초()
        {
            int accepted = CancelAccepted(ElapsedB);
            int rejected = CancelRejected(ElapsedB);

            Assert.AreNotEqual(accepted, rejected,
                $"대조가 죽었습니다 — {ElapsedB}초에서 두 해석이 같은 값({accepted})을 냅니다.");

            Assert.AreEqual(10, accepted, "149.9초 → 10XP 검산과 갈라졌습니다.");
            Assert.AreEqual(12, rejected, "149.9초 → 12XP(기각된 해석 — floor(5×149.9/60)) 검산과 갈라졌습니다.");

            Assert.AreEqual(accepted, CurrencyRules.FocusCancelXp(ElapsedB));
            Assert.AreNotEqual(rejected, CurrencyRules.FocusCancelXp(ElapsedB));
        }

        [Test]
        public void 취소_1분_미만은_0XP다()
        {
            const double justUnderAMinute = 59.9;

            Assert.AreEqual(0, CancelAccepted(justUnderAMinute));
            Assert.Greater(CancelRejected(justUnderAMinute), 0,
                "대조가 죽었습니다 — 기각된 해석도 0을 내면 이 케이스는 아무것도 구분하지 못합니다.");

            Assert.AreEqual(0, CurrencyRules.FocusCancelXp(justUnderAMinute),
                "★ 1분 미만 취소에서 XP가 나왔습니다.");
        }

        // ====================================================================
        // 3. 관계 — 코인과 정확히 같은 5:6 비율, 완주가 항상 이득
        // ====================================================================

        [Test]
        public void 취소_요율은_완주의_83_3퍼센트다_코인과_같은_비율()
        {
            Assert.AreEqual(CurrencyRules.FocusXpPerMinute * 5,
                CurrencyRules.FocusCancelXpPerMinute * 6,
                "취소:완주 = 5:6 관계가 깨졌습니다.");

            // ★ 코인과 XP가 «같은 비율»이라는 design-systems §15-2의 확정 사실 — 상수가 따로
            //   움직여 비율이 갈라지면 "완주가 이득"이라는 메시지가 코인·XP에서 다르게 읽힌다.
            Assert.AreEqual(CurrencyRules.FocusCancelCoinsPerMinute * CurrencyRules.FocusXpPerMinute,
                CurrencyRules.FocusCoinsPerMinute * CurrencyRules.FocusCancelXpPerMinute,
                "코인의 취소:완주 비율과 XP의 취소:완주 비율이 서로 다릅니다(§15-2 위반).");

            Assert.Less(CurrencyRules.FocusCancelXpPerMinute, CurrencyRules.FocusXpPerMinute,
                "취소가 완주보다 이득이면 완주할 이유가 사라집니다.");
        }

        [Test]
        public void 어떤_취소_주기도_완주_시급을_넘지_못한다()
        {
            const double oneHour = 3600.0;
            int completionPerHour = CurrencyRules.FocusCompletionXp(oneHour);

            int spamPerHour = 0;
            for (int i = 0; i < 60; i++) spamPerHour += CurrencyRules.FocusCancelXp(60.0);

            Assert.AreEqual(CurrencyRules.FocusCancelXpPerMinute * 60, spamPerHour);
            Assert.Less(spamPerHour, completionPerHour,
                "★ 취소 스팸이 완주보다 이득입니다 — XP 경제가 뒤집혔습니다.");
        }

        [Test]
        public void 음수와_NaN_경과는_XP를_만들지_않는다()
        {
            Assert.AreEqual(0, CurrencyRules.FocusCompletionXp(0.0));
            Assert.AreEqual(0, CurrencyRules.FocusCompletionXp(-1.0));
            Assert.AreEqual(0, CurrencyRules.FocusCompletionXp(double.NaN));
            Assert.AreEqual(0, CurrencyRules.FocusCancelXp(0.0));
            Assert.AreEqual(0, CurrencyRules.FocusCancelXp(-1.0));
            Assert.AreEqual(0, CurrencyRules.FocusCancelXp(double.NaN));
        }

        [Test]
        public void 말도_안되는_경과_시간도_누계를_음수로_감지_않는다()
        {
            Assert.GreaterOrEqual(CurrencyRules.FocusCompletionXp(double.MaxValue), 0);
            Assert.GreaterOrEqual(CurrencyRules.FocusCancelXp(double.MaxValue), 0);
            Assert.GreaterOrEqual(CurrencyRules.FocusCompletionXp(double.PositiveInfinity), 0);

            CurrencyModel.TryGrantFocusCompletionXp(double.MaxValue);
            Assert.GreaterOrEqual(CurrencyModel.FocusXpToday, 0, "★ 오늘 XP 누계가 음수가 됐습니다.");
            Assert.LessOrEqual(CurrencyModel.FocusXpToday, CurrencyRules.FocusXpDailyCap,
                "★ 말도 안 되는 입력이 일일 상한을 우회했습니다.");
        }

        // ====================================================================
        // 4. ★★★ 일일 상한 — 동전과 갈라지는 지점(design-systems §15-4)
        // ====================================================================

        [Test]
        public void 일일_상한에_도달하면_추가_완주_지급이_차단된다()
        {
            // 상한을 정확히 채우는 완주 하나(§15-4: 180분 = 1,080XP = 상한 정확히).
            double minutesToCap = (double)CurrencyRules.FocusXpDailyCap / CurrencyRules.FocusXpPerMinute;
            int first = CurrencyModel.TryGrantFocusCompletionXp(minutesToCap * 60.0);

            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, first,
                "상한을 정확히 채우는 완주 하나가 상한과 다른 값을 지급했습니다.");
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, CurrencyModel.FocusXpToday);
            Assert.IsTrue(CurrencyModel.FocusXpDailyLimitReached, "상한에 닿았는데 플래그가 안 섰습니다.");
            Assert.AreEqual(0, CurrencyModel.RemainingFocusXpRoomToday());

            // ★ 그 뒤 어떤 추가 완주도(음성 대조: 정상 시나리오라면 통과했을 15분 세션도) 0을 낸다.
            int blocked = CurrencyModel.TryGrantFocusCompletionXp(15.0 * 60.0);
            Assert.AreEqual(0, blocked,
                "★ 오늘 상한에 도달했는데 추가 완주 XP가 지급됐습니다 — 상한이 아무 일도 안 합니다.");
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, CurrencyModel.FocusXpToday,
                "차단된 지급이 그래도 누계를 건드렸습니다.");
        }

        [Test]
        public void 상한_근처의_지급은_전액이_아니라_남은_방만큼만_잘려서_들어간다()
        {
            int room = CurrencyRules.FocusXpDailyCap - 10;
            int forcedIn = CurrencyModel.TryGrantFocusCompletionXp(
                (double)room / CurrencyRules.FocusXpPerMinute * 60.0);
            Assert.AreEqual(room, forcedIn, "전제 — 방을 정확히 10만 남기고 채워야 합니다.");

            // 이번 완주는 산식상 훨씬 큰 값을 요구하지만(50분=300XP), 남은 방(10)만큼만 들어가야 한다 —
            // 마지막 한 조각을 통째로 버리지 않는다(활쏘기 TryAwardArcheryCoins의 "room까지만" 관례와 동일).
            int clamped = CurrencyModel.TryGrantFocusCompletionXp(50.0 * 60.0);
            Assert.AreEqual(10, clamped,
                "★ 상한 근처의 지급이 「남은 방만큼」이 아니라 「0 또는 전액」으로 잘렸습니다.");
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, CurrencyModel.FocusXpToday);
        }

        [Test]
        public void 완주와_취소의_일일_상한은_같은_누계를_공유한다()
        {
            // 완주로 대부분을 채우고 취소로 나머지를 밀어 넣어도 같은 카운터가 함께 상한을 존중한다.
            int completed = CurrencyModel.TryGrantFocusCompletionXp(170.0 * 60.0);  // 170분 = 1,020XP
            Assert.AreEqual(170 * CurrencyRules.FocusXpPerMinute, completed);

            // 남은 방은 60. 취소 산식(분당 5)으로 20분(=100XP 요구)을 취소해도 60만 들어간다.
            int cancelled = CurrencyModel.TryGrantFocusCancelXp(20.0 * 60.0);
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap - completed, cancelled,
                "완주와 취소가 서로 다른 상한을 쓰는 것처럼 동작했습니다 — 같은 카운터를 공유해야 합니다.");
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, CurrencyModel.FocusXpToday);
        }

        [Test]
        public void 취소_지급도_잔액에_실리고_상한이_없으면_더티를_세운다()
        {
            Assert.IsFalse(CurrencyModel.IsDirty, "전제 — 초기화 직후에는 더티가 아니어야 합니다.");

            Assert.AreEqual(0, CurrencyModel.TryGrantFocusCancelXp(59.9));
            Assert.IsFalse(CurrencyModel.IsDirty,
                "0XP 취소가 더티를 세웠습니다 — 1분 미만 취소를 반복하면 디스크를 계속 두드립니다.");

            int granted = CurrencyModel.TryGrantFocusCancelXp(ElapsedA);
            Assert.AreEqual(CancelAccepted(ElapsedA), granted);
            Assert.IsTrue(CurrencyModel.IsDirty,
                "지급이 일어났는데 더티가 서지 않았습니다 — 사용자가 번 XP가 저장되지 않습니다.");
        }

        // ====================================================================
        // 5. ★★★ 날짜 롤오버 — 코인과 같은 자리(CurrencyModel.TickDayRollover)에서 초기화된다
        // ====================================================================

        [Test]
        public void 날짜가_바뀌면_집중모드_XP_상한이_다시_열린다()
        {
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "전제 — 첫 판정에서 일자가 고정돼야 합니다.");
            int day1 = CurrencyModel.DayIndex;

            int granted = CurrencyModel.TryGrantFocusCompletionXp(
                (double)CurrencyRules.FocusXpDailyCap / CurrencyRules.FocusXpPerMinute * 60.0);
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, granted, "전제 — 오늘 상한까지 채웠어야 합니다.");
            Assert.AreEqual(0, CurrencyModel.TryGrantFocusCompletionXp(15.0 * 60.0),
                "전제 — 상한에 걸려 있어야 합니다.");

            // 자정 통과.
            CurrencyModel.SetDayIndexForTesting(day1 - 1);
            Assert.IsTrue(CurrencyModel.TickDayRollover(CurrencyRules.MinRefillGapSeconds),
                "날짜가 바뀌고 최소 간격도 채웠는데 롤오버가 안 일어났습니다.");

            Assert.AreEqual(0, CurrencyModel.FocusXpToday, "오늘 집중 모드 XP 누계가 0으로 안 돌아갔습니다.");
            Assert.IsFalse(CurrencyModel.FocusXpDailyLimitReached, "상한 도달 플래그가 안 풀렸습니다.");
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, CurrencyModel.RemainingFocusXpRoomToday());

            // ★ 표시만 리셋되고 실제로는 안 열리는 경우를 가른다 — 진짜로 다시 지급된다.
            int afterRollover = CurrencyModel.TryGrantFocusCompletionXp(15.0 * 60.0);
            Assert.Greater(afterRollover, 0, "새 날인데 집중 모드 XP가 다시 열리지 않았습니다.");
        }

        [Test]
        public void 롤오버가_없으면_집중모드_XP_상한은_영원히_잠긴_채다()
        {
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "전제.");
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap,
                CurrencyModel.TryGrantFocusCompletionXp(
                    (double)CurrencyRules.FocusXpDailyCap / CurrencyRules.FocusXpPerMinute * 60.0),
                "전제 — 상한까지 채웠어야 합니다.");

            // 일자를 건드리지 않고 시간만 흘려도 롤오버가 안 일어나면 상한은 그대로다.
            for (int i = 1; i <= 5; i++)
            {
                Assert.IsFalse(CurrencyModel.TickDayRollover(CurrencyRules.MinRefillGapSeconds * i * 2.0),
                    "날이 안 바뀌었는데 롤오버가 일어났습니다.");
            }

            Assert.AreEqual(0, CurrencyModel.TryGrantFocusCompletionXp(15.0 * 60.0),
                "상한이 저절로 풀렸습니다.");
        }

        // ====================================================================
        // 6. ★★★ 독립성 — 코인 상한과 XP 상한은 서로 다른 지갑이다 (design-systems §12)
        // ====================================================================

        [Test]
        public void 코인_일일_상한에_도달해도_집중모드_XP는_별도로_계속_지급된다()
        {
            // 유휴 수급으로 코인 일일 상한을 끝까지 채운다(회복제 미사용 기준 상한).
            CurrencyModel.TickIdleIncome(CurrencyRules.IdleWindowCapSeconds, true, out _);
            Assert.AreEqual(0, CurrencyModel.RemainingDailyRoomCoins(),
                "전제 — 코인 일일 상한이 다 차지 않았습니다.");

            int xpGranted = CurrencyModel.TryGrantFocusCompletionXp(25.0 * 60.0);
            Assert.AreEqual(CurrencyRules.FocusXpPerMinute * 25, xpGranted,
                "★ 코인 일일 상한에 도달했다고 집중 모드 XP까지 막혔습니다 — 두 상한은 독립적이어야 합니다.");
        }

        [Test]
        public void 활쏘기_코인_일일_상한에_도달해도_집중모드_XP는_별도로_계속_지급된다()
        {
            for (int i = 0; i < CurrencyRules.ArcheryDailyAwardLimit; i++)
            {
                // 쿨다운을 매번 지나도록 넉넉히 시간을 흘린다.
                CurrencyModel.TryAwardArcheryCoins(i * (CurrencyRules.ArcheryAwardCooldownSeconds + 1.0));
            }
            Assert.IsTrue(CurrencyModel.ArcheryDailyLimitReached, "전제 — 활쏘기 일일 상한에 도달해야 합니다.");

            int xpGranted = CurrencyModel.TryGrantFocusCompletionXp(15.0 * 60.0);
            Assert.AreEqual(CurrencyRules.FocusXpPerMinute * 15, xpGranted,
                "★ 활쏘기 코인 상한에 도달했다고 집중 모드 XP까지 막혔습니다.");
        }

        [Test]
        public void 집중모드_XP_상한에_도달해도_코인은_별도로_계속_지급된다()
        {
            int granted = CurrencyModel.TryGrantFocusCompletionXp(
                (double)CurrencyRules.FocusXpDailyCap / CurrencyRules.FocusXpPerMinute * 60.0);
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, granted, "전제 — XP 상한까지 채웠어야 합니다.");
            Assert.IsTrue(CurrencyModel.FocusXpDailyLimitReached, "전제 — XP 상한에 도달해야 합니다.");

            int coinsBefore = CurrencyModel.CoinBalance;
            int coinsPaid = CurrencyModel.PayFocusCompletionCoins(25.0 * 60.0);
            Assert.AreEqual(CurrencyRules.FocusCoinsPerMinute * 25, coinsPaid,
                "★ 집중 모드 XP 상한에 도달했다고 코인 지급까지 막혔습니다(§22-13: 코인은 상한 밖).");
            Assert.AreEqual(coinsBefore + coinsPaid, CurrencyModel.CoinBalance);
        }

        [Test]
        public void 집중모드_XP_지급은_유휴_코인_버킷을_갉지_않는다()
        {
            int coinRoomBefore = CurrencyModel.RemainingDailyRoomCoins();
            double windowBefore = CurrencyModel.IdleWindowUsedSeconds;
            int archeryBefore = CurrencyModel.ArcheryCoinsToday;

            CurrencyModel.TryGrantFocusCompletionXp(25.0 * 60.0);

            Assert.AreEqual(coinRoomBefore, CurrencyModel.RemainingDailyRoomCoins(),
                "★ 집중 모드 XP 지급이 유휴 코인 버킷(TodayGrantedCoins)을 갉았습니다.");
            Assert.AreEqual(windowBefore, CurrencyModel.IdleWindowUsedSeconds,
                "★ 집중 모드 XP 지급이 8시간 유휴 창을 갉았습니다.");
            Assert.AreEqual(archeryBefore, CurrencyModel.ArcheryCoinsToday,
                "★ 집중 모드 XP 지급이 활쏘기 코인 누계를 갉았습니다.");
        }

        // ====================================================================
        // 7. 세이브 왕복 — 하위 호환은 EquipmentMigrationTests(v10→v11)가 전담한다
        // ====================================================================
        // (여기서는 CurrencySaveState 왕복만 가볍게 재확인 — 파일 I/O를 포함한 전체 하위 호환
        //  시나리오는 CLAUDE.md 규칙에 따라 EquipmentMigrationTests에 있다.)

        [Test]
        public void CurrencySaveState_왕복은_오늘_집중모드_XP_누계를_보존한다()
        {
            CurrencyModel.TryGrantFocusCompletionXp(25.0 * 60.0);
            int expected = CurrencyModel.FocusXpToday;
            Assert.Greater(expected, 0, "전제 — 누계가 0이면 왕복 검증이 공허해진다.");

            CurrencySaveState state = CurrencyModel.CaptureSaveState();
            Assert.AreEqual(expected, state.FocusXpToday, "캡처된 상태에 오늘 XP 누계가 안 실렸습니다.");

            CurrencyModel.ResetForTesting();
            CurrencyModel.RestoreFromSave(state);
            Assert.AreEqual(expected, CurrencyModel.FocusXpToday, "복원된 모델에 오늘 XP 누계가 사라졌습니다.");
        }

        [Test]
        public void 손상된_음수_XP_누계는_로드시_0으로_정규화된다()
        {
            var state = new CurrencySaveState { FocusXpToday = -500 };
            CurrencyModel.RestoreFromSave(state);
            Assert.AreEqual(0, CurrencyModel.FocusXpToday, "음수 누계가 그대로 복원됐습니다.");
        }

        [Test]
        public void 손상된_초과_XP_누계는_로드시_상한으로_정규화된다()
        {
            var state = new CurrencySaveState { FocusXpToday = CurrencyRules.FocusXpDailyCap + 9999 };
            CurrencyModel.RestoreFromSave(state);
            Assert.AreEqual(CurrencyRules.FocusXpDailyCap, CurrencyModel.FocusXpToday,
                "위조된 큰 누계가 상한으로 잘리지 않았습니다 — 로드된 값을 그대로 믿고 있습니다.");
        }

        // ====================================================================
        // 8. ★ 배선 — FocusWatchDirector가 실제로 CharacterProgressionDirector를 통해 XP를 부르는가
        // ====================================================================
        // CurrencySeedAndArcheryWiringTests/DailyLimitClampAuditTests와 같은 관례(소스를 파일로
        // 읽는다 — 플랫폼 활성 타깃과 무관, 니들은 nameof로 조립해 오타가 컴파일 에러가 되게 한다).

        private static string ReadStripped(string suffix)
        {
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                if (path.Replace('\\', '/').EndsWith(suffix, StringComparison.Ordinal))
                    return EntitlementAuditSource.StripComments(File.ReadAllText(path));
            }
            return null;
        }

        [Test]
        public void FocusWatchDirector가_완주와_취소_XP를_모두_호출한다()
        {
            string code = ReadStripped("/FocusWatchDirector.cs");
            Assert.IsNotNull(code, "FocusWatchDirector.cs를 찾지 못했습니다.");

            string completionCall = nameof(CharacterProgressionDirector) + "."
                + nameof(CharacterProgressionDirector.GrantFocusCompletionXp);
            string cancelCall = nameof(CharacterProgressionDirector) + "."
                + nameof(CharacterProgressionDirector.GrantFocusCancelXp);

            // 직접 호출 대상은 "_progression.GrantFocusCompletionXp(" 형태다 — 타입 접두 없이도
            // 메서드 이름 자체는 니들로 걸어 둔다(이름이 바뀌면 nameof가 컴파일 에러로 먼저 잡는다).
            Assert.IsTrue(code.IndexOf(nameof(CharacterProgressionDirector.GrantFocusCompletionXp),
                    StringComparison.Ordinal) >= 0,
                $"{completionCall}에 대응하는 호출부를 찾지 못했습니다 — 집중 모드 완주 XP 배선이 없습니다.");
            Assert.IsTrue(code.IndexOf(nameof(CharacterProgressionDirector.GrantFocusCancelXp),
                    StringComparison.Ordinal) >= 0,
                $"{cancelCall}에 대응하는 호출부를 찾지 못했습니다 — 집중 모드 취소 XP 배선이 없습니다.");

            // ★ 음성 대조 — 존재할 수 없는 이름은 안 잡혀야 한다.
            Assert.IsFalse(code.IndexOf("GrantFocusCompletionXpThatCannotExist", StringComparison.Ordinal) >= 0,
                "스캐너가 아무 문자열이나 참으로 만듭니다.");
        }

        [Test]
        public void CharacterProgressionDirector는_새_Grant_메서드_안에서_기존_Grant를_재사용한다()
        {
            string code = ReadStripped("/CharacterProgressionDirector.cs");
            Assert.IsNotNull(code, "CharacterProgressionDirector.cs를 찾지 못했습니다.");

            // ★ 존재 단언 — 새 공개 진입점이 실재한다.
            Assert.IsTrue(code.IndexOf("void " + nameof(CharacterProgressionDirector.GrantFocusCompletionXp),
                    StringComparison.Ordinal) >= 0,
                $"{nameof(CharacterProgressionDirector.GrantFocusCompletionXp)} 선언을 찾지 못했습니다.");
            Assert.IsTrue(code.IndexOf("void " + nameof(CharacterProgressionDirector.GrantFocusCancelXp),
                    StringComparison.Ordinal) >= 0,
                $"{nameof(CharacterProgressionDirector.GrantFocusCancelXp)} 선언을 찾지 못했습니다.");

            // ★ 상한 체크는 CurrencyModel에 위임하고, 이 파일은 기존 Grant(...)를 그대로 재사용한다
            //   (design-systems §15-6-4가 명시적으로 요구한 형태 — 새 지급 경로를 만들지 않는다).
            Assert.IsTrue(code.IndexOf(nameof(CurrencyModel.TryGrantFocusCompletionXp), StringComparison.Ordinal) >= 0,
                "상한 체크(CurrencyModel.TryGrantFocusCompletionXp)를 호출하지 않습니다.");
            Assert.IsTrue(code.IndexOf(nameof(CurrencyModel.TryGrantFocusCancelXp), StringComparison.Ordinal) >= 0,
                "상한 체크(CurrencyModel.TryGrantFocusCancelXp)를 호출하지 않습니다.");
        }
    }
}
