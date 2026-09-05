using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 컬럼 2 「능력치 / STATUS」·「테마 세트 / SET」의 <b>표시 문자열</b> 회귀 잠금
    /// (<see cref="CharacterStatReadout"/>).
    ///
    /// ============================================================================
    /// ★ 기대값의 출처를 두 종류로 엄격히 나눈다 (EquipmentStatInvariantTests와 같은 규약)
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>교정 앵커</b> — <c>docs/DESIGN_SYSTEMS_STATS.md</c> §0의 14/14 PASS 표.
    ///    <c>16 (+8)</c> · <c>중급까지 4</c> · <c>+24</c> · <c>오피스 워커 3/4</c> · 매력 10의 <c>초급</c>.
    ///    이 값은 <b>프로덕션이 아니라 설계가 인계본 렌더에서 독립 재현</b>한 바깥의 실측이라
    ///    일부러 숫자로 적는다. 프로덕션 함수로 만들면 기준과 대상이 함께 틀어져 아무것도 못 잰다
    ///    (TEAM.md 「생성기와 검사기가 같이 틀린다」).</item>
    ///  <item><b>프로덕션 상수</b>(임계 10/20/32 · CAP 40 · 세트 보너스)는 <b>숫자로 베끼지 않고</b>
    ///    <see cref="EquipmentStatRules"/>를 참조한다. 튜닝하는 날 무관한 테스트가 함께 빨개지면
    ///    고치는 사람이 "숫자만 맞추면 되는 잡음"으로 학습한다(CLAUDE.md 2026-09-01 확정).</item>
    /// </list>
    ///
    /// <para>★ 교정이 깨지면 그 뒤 숫자를 전부 폐기한다 — 표가 낡은 것이다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 <b>다시 만들지 않는</b> 것
    /// ============================================================================
    /// 스탯 값·임계·세트 완성의 <b>규칙</b>은 <see cref="EquipmentStatInvariantTests"/>가 이미 잠근다
    /// (I-1~I-4 · 1일차 <b>장비 기여</b> 11/10/10/11 · 스탯별 최대 33/33/34/34). 여기서는 <b>그 결과가
    /// 화면 문자열로 어떻게 옮겨지는가</b>만 본다 — 같은 것을 두 번 재면 한쪽이 낡을 때 아무도 모른다.
    ///
    /// <para>★ 2026-09-06 정정 — 「1일차 11/10/10/11」을 <b>화면 총합</b>으로 읽지 마라. R21 테마
    /// 배정(안 B)으로 1일차 무료 4종이 전부 <c>mil</c>이 되어 <b>세트가 첫날부터 완성</b>되므로
    /// 화면에 뜨는 총합은 거기에 세트 보너스가 더해진 값이다. 그 분해(장비 항 + 세트 항)를
    /// <see cref="EquipmentStatInvariantTests"/>가 상수 참조로 유지하고 있고, 여기서 숫자를
    /// 다시 적지 않는 이유도 같다.</para>
    /// </summary>
    public sealed class CharacterStatReadoutTests
    {
        // ====================================================================
        // 0. 교정 — 이 뒤의 판정을 믿어도 되는 이유 (§0 14/14 표)
        // ====================================================================

        /// <summary>【앵커】§0: 집중력 <c>16 (+8)</c> · 관찰력 <c>11 (+5)</c> · 매력 <c>10 (+5)</c> ·
        /// 민첩 <c>13 (+6)</c>.</summary>
        [Test]
        public void 교정_표시_문자열_네_개가_설계_렌더와_같다()
        {
            Assert.AreEqual("16 (+8)", CharacterStatReadout.FormatDisplay(16, 8), "집중력 표시");
            Assert.AreEqual("11 (+5)", CharacterStatReadout.FormatDisplay(11, 5), "관찰력 표시");
            Assert.AreEqual("10 (+5)", CharacterStatReadout.FormatDisplay(10, 5), "매력 표시");
            Assert.AreEqual("13 (+6)", CharacterStatReadout.FormatDisplay(13, 6), "민첩 표시");
        }

        /// <summary>【앵커】§0: <c>중급까지 4 / 9 / 10 / 7</c>.
        /// <para>★ 이 넷이 <b>임계값 20을 간접적으로 못박는다</b> — 총합 + 잔여가 전부 20이다
        /// (§0-1 리더 가설 검산 「{현재값 + 잔여} = {20} 한 개」). 임계가 움직이면 여기서 빨개진다.</para></summary>
        [Test]
        public void 교정_잔여_문자열_네_개가_설계_렌더와_같다()
        {
            Assert.AreEqual("중급까지 4", CharacterStatReadout.FormatRemaining(16), "집중력 잔여");
            Assert.AreEqual("중급까지 9", CharacterStatReadout.FormatRemaining(11), "관찰력 잔여");
            Assert.AreEqual("중급까지 10", CharacterStatReadout.FormatRemaining(10), "매력 잔여");
            Assert.AreEqual("중급까지 7", CharacterStatReadout.FormatRemaining(13), "민첩 잔여");
        }

        /// <summary>【앵커】§0: 장비 합 <c>+24</c> · 테마 세트 <c>오피스 워커 3/4</c> ·
        /// 매력(총합 10)의 등급명 <c>초급</c>.</summary>
        [Test]
        public void 교정_장비합과_테마진행도와_등급명이_설계_렌더와_같다()
        {
            Assert.AreEqual("+24", CharacterStatReadout.FormatEquipmentSum(24), "장비 합");
            Assert.AreEqual("오피스 워커 3/4",
                CharacterStatReadout.FormatThemeProgress("오피스 워커", 3, 4), "테마 세트");

            // 등급명은 Core가 준다 — 여기서는 「총합 10이면 그 낱말이 나오는가」만 본다.
            Assert.AreEqual("초급", EquipmentStatRules.TierName(EquipmentStatRules.TierOf(10)), "매력 등급명");
        }

        // ====================================================================
        // 1. 임계 경계 — 정확히 임계값일 때 어느 쪽인가
        // ====================================================================

        /// <summary>
        /// 경계는 <b>이상(≥)</b>이다: 총합이 임계값과 <b>같으면 그 단계에 들어간다</b>.
        /// <para>★ 경계 좌표를 숫자로 적지 않고 <see cref="EquipmentStatRules.TierThreshold"/>에서
        /// 가져온다 — 임계를 튜닝해도 이 테스트는 <b>같은 성질</b>을 계속 잰다. 값 자체(10/20/32)는
        /// 위 교정 앵커가 따로 못박고 있다.</para>
        /// </summary>
        [Test]
        public void 총합이_정확히_임계값이면_그_단계에_들어간다()
        {
            int boundaries = 0;
            for (int tier = 1; tier <= EquipmentStatRules.TierCount; tier++)
            {
                int threshold = EquipmentStatRules.TierThreshold(tier);
                Assert.Greater(threshold, 0, $"{tier}단 임계값이 0입니다 — 표가 비었습니다.");
                boundaries++;

                Assert.AreEqual(tier, EquipmentStatRules.TierOf(threshold),
                    $"총합 {threshold}(정확히 {tier}단 임계)이 {tier}단으로 안 읽힙니다 — 경계가 초과(&gt;)로 바뀌었습니다.");
                Assert.AreEqual(tier - 1, EquipmentStatRules.TierOf(threshold - 1),
                    $"총합 {threshold - 1}(임계 바로 아래)이 {tier - 1}단이 아닙니다 — 경계가 한 칸 밀렸습니다.");
            }

            // 양성 대조 — 루프가 실제로 돌았는가(빈 목록이 조용히 초록이 되는 거짓 통과 #5).
            Assert.AreEqual(EquipmentStatRules.TierCount, boundaries,
                "경계를 하나도 못 쟀습니다 — 임계 표가 비어 있으면 위 단언은 전부 공허합니다.");
            Assert.Greater(boundaries, 0, "임계 단계가 0개입니다.");
        }

        /// <summary>임계값 위에서의 잔여 문구 — 그 자리에서 <b>다음</b> 단계를 가리켜야 한다.
        /// 마지막 임계에서는 「최고 단계」다.</summary>
        [Test]
        public void 임계값_위에서의_잔여_문구가_다음_단계를_가리킨다()
        {
            for (int tier = 1; tier < EquipmentStatRules.TierCount; tier++)
            {
                int threshold = EquipmentStatRules.TierThreshold(tier);
                int next = EquipmentStatRules.TierThreshold(tier + 1);
                string expected = $"{EquipmentStatRules.TierName(tier + 1)}까지 {next - threshold}";
                Assert.AreEqual(expected, CharacterStatReadout.FormatRemaining(threshold),
                    $"총합 {threshold}에서의 잔여 문구가 다음 단계를 안 가리킵니다.");
            }

            int top = EquipmentStatRules.TierThreshold(EquipmentStatRules.TierCount);
            Assert.AreEqual(CharacterStatReadout.TopTier, CharacterStatReadout.FormatRemaining(top),
                "마지막 임계에 정확히 닿았는데 「최고 단계」가 아닙니다.");
            Assert.AreEqual(CharacterStatReadout.TopTier, CharacterStatReadout.FormatRemaining(EquipmentStatRules.Cap),
                "CAP에서 「최고 단계」가 아닙니다.");
        }

        /// <summary>임계 미달 구간에서는 <b>첫 임계</b>를 가리키고, 단계 낱말은 「임계 미달」이다.</summary>
        [Test]
        public void 임계_미달에서는_첫_임계를_가리킨다()
        {
            int first = EquipmentStatRules.TierThreshold(1);
            Assert.AreEqual(0, EquipmentStatRules.TierOf(first - 1));
            Assert.AreEqual($"{EquipmentStatRules.TierName(1)}까지 1",
                CharacterStatReadout.FormatRemaining(first - 1));
            Assert.AreEqual($"{EquipmentStatRules.TierName(1)}까지 {first}",
                CharacterStatReadout.FormatRemaining(0));
        }

        // ====================================================================
        // 2. 없는 값 · 이상한 값 — 화면이 죽거나 이상해지지 않는다
        // ====================================================================

        /// <summary>음수 총합/보너스는 화면에 그릴 자리가 없다. 음수 부호가 새어 나오면 안 된다.</summary>
        [Test]
        public void 음수는_화면으로_새지_않는다()
        {
            Assert.AreEqual("0", CharacterStatReadout.FormatTotal(-5));
            Assert.AreEqual("0", CharacterStatReadout.FormatTotal(int.MinValue + 1));
            Assert.AreEqual(string.Empty, CharacterStatReadout.FormatBonus(-3));
            Assert.AreEqual(string.Empty, CharacterStatReadout.FormatBonus(0));
            Assert.AreEqual("0", CharacterStatReadout.FormatDisplay(-1, -1));
            Assert.AreEqual("+0", CharacterStatReadout.FormatEquipmentSum(-7));

            StringAssert.DoesNotContain("-", CharacterStatReadout.FormatDisplay(-1, -1));
            StringAssert.DoesNotContain("−", CharacterStatReadout.FormatEquipmentSum(-7));

            // 양성 대조 — 같은 함수가 양수는 실제로 통과시킨다(단언이 "언제나 참"이 아니다).
            Assert.AreEqual("41 (+33)", CharacterStatReadout.FormatDisplay(41, 33),
                "§7-3의 최대 표기가 안 나옵니다 — 위 «음수 없음» 판정이 무효입니다.");
        }

        /// <summary>0은 <b>참인 사실</b>이라 지우지 않는다 — 「아직 아무것도 안 걸쳤다」가 0이다.
        /// 단 <c>(+0)</c>은 정보가 없으므로 괄호를 아예 안 그린다.</summary>
        [Test]
        public void 장비를_하나도_안_걸쳤을_때의_표기가_일관된다()
        {
            Assert.AreEqual("8", CharacterStatReadout.FormatDisplay(8, 0),
                "보너스 0에서 빈 괄호가 붙거나 총합이 사라졌습니다.");
            Assert.AreEqual("+0", CharacterStatReadout.FormatEquipmentSum(0),
                "장비 합 자리는 0이어도 같은 모양이어야 눈이 값을 찾습니다.");
        }

        /// <summary>테마 이름이 없거나 분모가 0이면 진행도를 <b>주장하지 않는다</b>.</summary>
        [Test]
        public void 테마_진행도는_빈_값에서_아무것도_주장하지_않는다()
        {
            Assert.AreEqual(CharacterStatReadout.NoValue,
                CharacterStatReadout.FormatThemeProgress(null, 3, 4));
            Assert.AreEqual(CharacterStatReadout.NoValue,
                CharacterStatReadout.FormatThemeProgress(string.Empty, 3, 4));
            Assert.AreEqual(CharacterStatReadout.NoValue,
                CharacterStatReadout.FormatThemeProgress("오피스 워커", 3, 0));

            // 분자가 분모를 넘어도 "5/4"를 그리지 않는다.
            Assert.AreEqual("오피스 워커 4/4",
                CharacterStatReadout.FormatThemeProgress("오피스 워커", 9, 4));
            Assert.AreEqual("오피스 워커 0/4",
                CharacterStatReadout.FormatThemeProgress("오피스 워커", -2, 4));
        }

        /// <summary>착용이 없거나(<c>null</c>) 걸친 것이 전부 <b>무소속</b>(외형·팩 = 빈 문자열)이면
        /// 진행도 자체가 없다.
        /// <para>★ 빈 문자열을 「같다」로 세면 <b>아무것도 안 입은 사람에게 4/4</b>가 뜬다.</para>
        /// <para>★ 2026-09-06 정정 — 이 두 입력은 이제 <b>드문 경우</b>다. R21 테마 배정으로 기본
        /// 24종(스탯 4슬롯 × 6테마)에 테마가 붙었으므로, 기본 장비를 한 부위라도 걸치면 진행도가
        /// 생긴다. 「전부 미배정」이 기본값이던 시절의 서술이 여기 남아 있었다.</para></summary>
        [Test]
        public void 빈_테마는_진행도로_세지_않는다()
        {
            Assert.IsFalse(CharacterStatReadout.TryGetThemeProgress(null, null, null, null,
                out _, out int m0, out _));
            Assert.AreEqual(0, m0);

            Assert.IsFalse(CharacterStatReadout.TryGetThemeProgress("", "", "", "",
                out _, out int m1, out _));
            Assert.AreEqual(0, m1);

            // 양성 대조 — 한 칸이라도 실재하면 진행도가 잡힌다(위 두 «없음»이 공허하지 않다).
            Assert.IsTrue(CharacterStatReadout.TryGetThemeProgress("office", "", null, "",
                out string theme, out int matched, out int required));
            Assert.AreEqual("office", theme);
            Assert.AreEqual(1, matched);
            Assert.AreEqual(EquipmentStatRules.StatCount, required);
        }

        // ====================================================================
        // 3. 세트 — 화면 진행도와 Core 판정이 절대 갈라지지 않는다
        // ====================================================================

        /// <summary>
        /// ★ <c>matched == required ⟺ EquipmentStatRules.IsSetComplete</c>를 <b>전수</b>로 확인한다.
        ///
        /// <para>완성 판정의 유일한 출처는 Core이고 화면은 「몇 칸까지 왔는가」만 낸다. 둘이 갈라지면
        /// 화면이 붙지도 않은 <c>+8</c>을 약속하거나, 붙은 보너스를 숨긴다. 4칸 × 알파벳 4개 =
        /// <b>256 조합</b>을 전부 돌린다(무작위가 아니라 전수라 재현이 흔들리지 않는다).</para>
        /// </summary>
        [Test]
        public void 세트_진행도와_Core_완성판정이_전수_조합에서_일치한다()
        {
            string[] alphabet = { null, string.Empty, "office", "cyber" };
            int checkedCombos = 0;
            int completeCombos = 0;
            int partialCombos = 0;

            foreach (string h in alphabet)
            foreach (string e in alphabet)
            foreach (string n in alphabet)
            foreach (string b in alphabet)
            {
                checkedCombos++;
                bool core = EquipmentStatRules.IsSetComplete(h, e, n, b, out string coreTheme);
                bool hasProgress = CharacterStatReadout.TryGetThemeProgress(h, e, n, b,
                    out string theme, out int matched, out int required);

                bool uiComplete = hasProgress && matched >= required;
                Assert.AreEqual(core, uiComplete,
                    $"세트 판정이 갈라졌습니다 — Core={core} / UI={matched}/{required} " +
                    $"(h={Show(h)} e={Show(e)} n={Show(n)} b={Show(b)})");

                if (core)
                {
                    completeCombos++;
                    Assert.AreEqual(coreTheme, theme, "완성인데 테마 이름이 다릅니다.");
                }
                else if (hasProgress && matched > 0)
                {
                    partialCombos++;
                }
            }

            // 양성 대조 두 방향 — 루프가 실제로 두 상태를 다 봤는가.
            Assert.AreEqual(alphabet.Length * alphabet.Length * alphabet.Length * alphabet.Length,
                checkedCombos, "조합을 다 안 돌았습니다.");
            Assert.Greater(completeCombos, 0,
                "완성이 나오는 조합이 하나도 없습니다 — 「항상 false」와 구별되지 않습니다.");
            Assert.Greater(partialCombos, 0,
                "부분 진행이 나오는 조합이 하나도 없습니다 — 진행도 계산이 죽었습니다.");
        }

        private static string Show(string s) => s == null ? "null" : s.Length == 0 ? "\"\"" : s;

        /// <summary>세트 완성 줄은 <b>두 숫자를 다 유도한다</b> — 「4부위」도 「+8」도 상수에서 나온다.
        /// 【앵커】§1-7의 <c>4부위 합계 +8</c>.</summary>
        [Test]
        public void 세트_완성_문구가_설계_문장과_같고_숫자를_유도한다()
        {
            string text = CharacterStatReadout.FormatSetBonus();
            Assert.AreEqual("4부위 합계 +8", text, "§1-7 「스탯 총합 보너스 · 4부위 합계 +8」");

            // 그리고 그 숫자가 프로덕션 상수에서 나왔는지 다시 잰다(문자열만 맞고 유도가 끊길 수 있다).
            Assert.AreEqual(
                $"{EquipmentStatRules.StatCount}부위 합계 " +
                $"+{EquipmentStatRules.SetCompletionBonusPerStat * EquipmentStatRules.StatCount}",
                text, "문구가 상수에서 유도되지 않고 있습니다.");
        }

        // ====================================================================
        // 4. 임계 눈금 점등 (H-8 「한 번 넘긴 눈금만 황동」)
        // ====================================================================

        [Test]
        public void 넘긴_눈금만_켜지고_저장된_최고기록도_함께_본다()
        {
            // 지금 2단인데 저장 기록이 없다 → 1·2 켜짐, 3 꺼짐.
            Assert.IsTrue(CharacterStatReadout.IsTickLit(1, currentTier: 2, reachedTier: 0));
            Assert.IsTrue(CharacterStatReadout.IsTickLit(2, currentTier: 2, reachedTier: 0));
            Assert.IsFalse(CharacterStatReadout.IsTickLit(3, currentTier: 2, reachedTier: 0));

            // 장비를 벗어 지금은 미달이지만 예전에 2단을 넘겼다 → 영구 해금이라 1·2가 켜져 있다.
            Assert.IsTrue(CharacterStatReadout.IsTickLit(1, currentTier: 0, reachedTier: 2));
            Assert.IsTrue(CharacterStatReadout.IsTickLit(2, currentTier: 0, reachedTier: 2));
            Assert.IsFalse(CharacterStatReadout.IsTickLit(3, currentTier: 0, reachedTier: 2));

            // 둘 다 0이면 하나도 안 켜진다(양성 대조의 반대편).
            for (int t = 1; t <= EquipmentStatRules.TierCount; t++)
            {
                Assert.IsFalse(CharacterStatReadout.IsTickLit(t, 0, 0), $"{t}단이 근거 없이 켜집니다.");
            }

            // 눈금 색인은 1-기준이다 — 0이나 음수는 눈금이 아니다.
            Assert.IsFalse(CharacterStatReadout.IsTickLit(0, 3, 3));
            Assert.IsFalse(CharacterStatReadout.IsTickLit(-1, 3, 3));
        }

        // ====================================================================
        // 5. 두 축의 계약 — 규칙 · 저장 · 화면이 한 숫자에 묶여 있는가
        // ====================================================================

        /// <summary>임계 단계 수와 저장 필드 상한이 같아야 한다. 갈라지면 「한 번 넘긴 눈금」이
        /// 마지막 한 칸을 영영 저장하지 못한다(<see cref="CurrencyRules.ClampStatTier"/>가 잘라 버린다).</summary>
        [Test]
        public void 임계_단계_수와_저장_상한이_같은_숫자다()
        {
            Assert.AreEqual(CurrencyRules.MaxStatTier, EquipmentStatRules.TierCount,
                "임계 단계 수와 statTierReached의 상한이 갈라졌습니다 — 마지막 단계가 저장되지 않습니다.");
            Assert.AreEqual(CurrencyRules.StatTierSlotCount, EquipmentStatRules.StatCount,
                "스탯 수와 statTierReached의 칸 수가 갈라졌습니다.");
        }

        /// <summary>【앵커】§1-4: 눈금 3개 = <c>10/40 · 20/40 · 32/40 = 25% · 50% · 80%</c>.
        /// <para>퍼센트를 프로덕션에 적지 않고 임계/캡에서 나눈 결과가 그 값인지 확인한다.</para></summary>
        [Test]
        public void 교정_임계_눈금_위치가_25_50_80퍼센트다()
        {
            Assert.AreEqual(0.25f, EquipmentStatRules.TierMark01(1), 1e-6f, "1단 눈금");
            Assert.AreEqual(0.50f, EquipmentStatRules.TierMark01(2), 1e-6f, "2단 눈금");
            Assert.AreEqual(0.80f, EquipmentStatRules.TierMark01(3), 1e-6f, "3단 눈금");
        }

        // ====================================================================
        // 6. 임계 효과 문구 — 화면이 없는 기능을 약속하지 않는가
        // ====================================================================

        /// <summary>
        /// ★ 오늘 실제로 발동하는 임계 효과는 <b>0칸</b>이다(<c>ActiveTierEffectCount</c>).
        /// 그래서 컬럼 2 스탯 카드는 §1-5의 12칸 문구를 <b>그리지 않는다</b>.
        ///
        /// <para><b>부재 단언에는 존재 대조를 붙인다</b>(CLAUDE.md) — 같은 실행 안에서
        /// (가) 문구 자체는 Core에 실재하고, (나) 그 문구가 화면 조립 경로에는 없다를 함께 보인다.
        /// 그래야 「문구가 사라져서 0건」과 「화면이 안 그려서 0건」이 구분된다.</para>
        ///
        /// <para>효과가 하나라도 배선되면 이 테스트가 <b>빨개진다</b> — 그때가 문구를 켤 시점이고,
        /// 켜면서 이 테스트를 함께 고쳐야 한다.</para>
        /// </summary>
        [Test]
        public void 발동하지_않는_임계_효과_문구는_화면에_그리지_않는다()
        {
            // (가) 존재 대조 — 문구 표는 Core에 살아 있다.
            string sample = EquipmentStatRules.TierEffectText(CharacterStat.Charm, 1);
            Assert.IsNotEmpty(sample, "§1-5 문구 표가 비었습니다 — 아래 «없음» 판정이 무효입니다.");

            // (나) 오늘 발동하는 칸이 0인지 Core에게 직접 묻는다.
            int active = 0;
            foreach (CharacterStat stat in System.Enum.GetValues(typeof(CharacterStat)))
            {
                for (int tier = 1; tier <= EquipmentStatRules.TierCount; tier++)
                {
                    if (EquipmentStatRules.IsTierEffectActive(stat, tier)) active++;
                }
            }
            Assert.AreEqual(EquipmentStatRules.ActiveTierEffectCount, active,
                "IsTierEffectActive와 ActiveTierEffectCount가 어긋났습니다 — 한쪽만 고쳤습니다.");

            // (다) 발동 0이면 화면은 그 문구를 쓰지 않아야 한다.
            if (active != 0) Assert.Pass("임계 효과가 배선됐습니다 — 이제 문구를 화면에 켜고 이 테스트를 갱신하세요.");

            // ★ 니들은 <b>한정된 호출 형태</b>다. <c>TierEffectText</c>만 찾으면 이 파일의 설명 주석
            //   (「그때 이 자리에 TierEffectText를 켜면 된다」)에 스스로 걸려 <b>거짓 빨강</b>이 난다 —
            //   실제로 첫 시도에서 그렇게 걸렸다. 호출부는 다른 클래스의 정적 함수라 반드시 한정형이다.
            const string callNeedle = "EquipmentStatRules.TierEffectText";
            string surface = SourceConstantReader.ReadSurfaceText(SurfacePath("CharacterInfoWindow.cs"));

            Assert.AreEqual(-1, surface.IndexOf(callNeedle, System.StringComparison.Ordinal),
                "발동하는 효과가 0칸인데 정보창이 §1-5 문구를 그리고 있습니다 — 화면이 일어나지 않는 일을 " +
                "약속합니다(원칙 1, 유예 자동 해금과 같은 형태의 결함).");

            // 죽은 니들이 아님을 같은 실행에서 증명한다 — <b>같은 형태</b>의 실재하는 호출은 찾아낸다.
            StringAssert.Contains("EquipmentStatRules.TierName", surface,
                "정보창 표면에서 실재하는 스탯 규칙 호출조차 못 찾았습니다 — 위 «없음» 판정이 무효입니다" +
                "(파일을 잘못 읽었거나 표면 조각이 빠졌습니다).");
        }

        private static string SurfacePath(string file)
            => System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "_Project", "Scripts", "Interaction", file);

        // ====================================================================
        // 7. 테마 이름 — 내부 코드명이 화면으로 새지 않는가 (2026-09-06)
        // ====================================================================

        /// <summary>
        /// ★ 세트 패널이 <c>"mil 3/4"</c>처럼 <b>영문 내부 키</b>를 한글 화면에 찍고 있던 것을 잠근다.
        ///
        /// <para>【앵커】§0의 <c>오피스 워커 3/4</c>를 <b>키에서 끝까지</b> 재현한다 — 위
        /// <c>교정_장비합과_테마진행도와_등급명이_설계_렌더와_같다</c>는 이름을 <b>손으로 넣어</b>
        /// 확인했으므로 「키 → 이름」 구간이 통째로 빠져 있었다. 사용자가 실제로 보는 경로는
        /// 이쪽이다.</para>
        ///
        /// <para>표는 <b>Core에만</b> 있어야 한다(<see cref="ItemCatalog.ThemeDisplayName"/>).
        /// 그래서 «화면이 자기 표를 들지 않았는가»를 6종 전부에서 대조하고, 동시에
        /// «모르는 키가 조용히 사라지지 않는가»(부재가 아니라 <b>키 그대로 보인다</b>)를 못박는다.</para>
        /// </summary>
        [Test]
        public void 테마_키가_아니라_이름이_화면_문자열로_나간다()
        {
            // 【앵커】§0 렌더의 «오피스 워커 3/4» — 이번에는 키에서 출발한다.
            Assert.AreEqual("오피스 워커 3/4",
                CharacterStatReadout.FormatThemeProgress(
                    CharacterStatReadout.ThemeLabel(ItemCatalog.ThemeOffice), 3, 4),
                "키 «office»에서 §0의 표시 문자열이 나오지 않습니다 — 화면에 내부 코드명이 뜹니다.");

            string[] keys = ItemCatalog.AllThemes();
            Assert.Greater(keys.Length, 0,
                "테마 표가 비었습니다 — 아래 판정이 전부 공허합니다(거짓 통과 #5).");

            int named = 0;
            foreach (string key in keys)
            {
                string label = CharacterStatReadout.ThemeLabel(key);
                Assert.IsFalse(string.IsNullOrEmpty(label), $"«{key}»의 표시 이름이 비었습니다.");
                Assert.AreNotEqual(key, label,
                    $"테마 «{key}»가 이름 없이 키 그대로 화면에 나갑니다 — Core 표에 이름을 넣으십시오.");
                Assert.AreEqual(ItemCatalog.ThemeDisplayName(key), label,
                    $"«{key}»의 이름이 Core 표와 다릅니다 — 표시 계층이 자기 표를 들고 있습니다.");
                named++;
            }
            Assert.AreEqual(keys.Length, named, "테마를 다 안 돌았습니다.");

            // ---- 모르는 키: 사라지지 않고 «키 그대로» 보인다 ----
            const string unknown = "pack.theme.that.core.does.not.know";
            Assert.IsNull(ItemCatalog.ThemeDisplayName(unknown),
                "Core가 모르는 키에 이름을 냈습니다 — 아래 대조가 공허합니다(양성 대조 실패).");
            Assert.AreEqual(unknown, CharacterStatReadout.ThemeLabel(unknown),
                "모르는 키를 «—»로 지우면 팩이 새 테마를 실어 오는 날 진행도 줄이 소리 없이 사라집니다.");
            StringAssert.Contains(unknown,
                CharacterStatReadout.FormatThemeProgress(CharacterStatReadout.ThemeLabel(unknown), 2, 4),
                "모르는 테마의 진행도 줄이 통째로 사라졌습니다.");

            // ---- 없는 값은 여전히 «없음»이다(두 곳이 «없음»을 따로 정의하지 않는다) ----
            Assert.IsNull(CharacterStatReadout.ThemeLabel(null), "null이 다른 것으로 바뀌었습니다.");
            Assert.AreEqual(ItemCatalog.ThemeUnassigned,
                CharacterStatReadout.ThemeLabel(ItemCatalog.ThemeUnassigned), "무소속이 다른 것으로 바뀌었습니다.");
            Assert.AreEqual(CharacterStatReadout.NoValue,
                CharacterStatReadout.FormatThemeProgress(CharacterStatReadout.ThemeLabel(null), 2, 4));
            Assert.AreEqual(CharacterStatReadout.NoValue,
                CharacterStatReadout.FormatThemeProgress(
                    CharacterStatReadout.ThemeLabel(ItemCatalog.ThemeUnassigned), 2, 4));
        }

        /// <summary>
        /// ★ 그 이름이 세트 패널 칸에 <b>실제로 들어가는가</b> — 곱셈 모형이 아니라
        /// <c>preferredWidth</c>로 <b>폰트에게 묻는다</b>(<c>UiTextWidthModelTests</c>와 같은 자).
        ///
        /// <para>키(<c>mil</c> 3자)를 이름(<c>밀리터리</c>)으로 바꾸면 <b>글자가 넓어진다</b> —
        /// 최장은 <c>사이버 아포칼립스 4/4</c>다. 칸은
        /// <see cref="CharacterInfoWindow.StatCardContentWidth"/>이고 그 <see cref="Text"/>는
        /// <c>HorizontalWrapMode.Overflow</c>라 넘치면 <b>잘리지도 않고 카드 밖으로 흘러</b>
        /// 세트 패널 테두리를 뚫는다(말줄임이 없는 자리다).</para>
        ///
        /// <para><b>기대값을 프로덕션 폭 함수로 만들지 않는다</b> — <c>SettingsControls.MeasuredWidth</c>를
        /// 부르지 않고 여기서 <c>Mathf.Ceil(preferredWidth)</c>로 직접 잰다.</para>
        /// </summary>
        [Test]
        public void 테마_이름_진행도_한_줄이_세트_패널_칸에_실측으로_들어간다()
        {
            var host = new GameObject("테마폭측정", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Text probe = UiChrome.AddText(host.transform, "Probe", UiChrome.FontBody,
                    TextAnchor.MiddleLeft, UiChrome.TextPrimary);

                // ---- 양성 대조: 폰트가 실제로 재는가. 0이면 아래 «들어간다»가 전부 거짓 초록이다 ----
                float hangul = Ink(probe, "가나다라마");
                float latin = Ink(probe, "abcde");
                Assert.Greater(hangul, 0f,
                    "한글 5자의 폭이 0입니다 — 폰트가 안 올라왔습니다. 이 테스트의 모든 숫자를 폐기하고 " +
                    "PlayMode(UiTextWidthModelTests)에서 재십시오.");
                Assert.Greater(hangul, latin,
                    $"한글 5자({hangul:F1}pt)가 라틴 5자({latin:F1}pt)보다 넓지 않습니다 — 자가 이상합니다.");

                float box = CharacterInfoWindow.StatCardContentWidth;
                int full = EquipmentStatRules.StatCount;   // 최장은 분자·분모가 다 찬 «4/4»다.
                var report = new System.Text.StringBuilder();
                report.Append($"[세트폭-TEST] 진행도 한 줄({UiChrome.FontBody}pt) 대 칸 {box:F0}pt\n");

                float worst = 0f;
                string worstLine = null;
                int probed = 0;
                foreach (string key in ItemCatalog.AllThemes())
                {
                    string line = CharacterStatReadout.FormatThemeProgress(
                        CharacterStatReadout.ThemeLabel(key), full, full);
                    float w = Ink(probe, line);
                    Assert.Greater(w, 0f, $"«{line}»의 폭이 0입니다(측정 실패).");
                    report.Append($"    {line,-20}\t{w,6:F1}pt\t여유 {box - w,6:F1}pt\n");
                    if (w > worst) { worst = w; worstLine = line; }
                    probed++;
                }

                Assert.AreEqual(ItemCatalog.AllThemes().Length, probed, "테마를 다 안 쟀습니다.");
                report.Append($"    최장 «{worstLine}» {worst:F1}pt / 여유 {box - worst:F1}pt");
                Debug.Log(report.ToString());

                Assert.LessOrEqual(worst, box,
                    $"최장 테마 진행도 «{worstLine}»가 {worst:F1}pt로 세트 패널 칸 {box:F0}pt를 넘칩니다 — " +
                    "이 Text는 Overflow라 잘리지 않고 카드 밖으로 흘러 테두리를 뚫습니다. " +
                    "칸을 넓히거나 말줄임을 붙이는 것은 세로·가로 예산이 걸린 판단이라 리더에게 보고할 일입니다.");

                // ---- 음성 대조: 이 자가 «안 들어간다»고 말할 수 있는가 ----
                float overLong = Ink(probe, "가나다라마바사아자차카타파하가나다라마바사아자차카타파하 4/4");
                Assert.Greater(overLong, box,
                    $"27자짜리 가짜 테마명({overLong:F1}pt)조차 칸 {box:F0}pt에 들어간다고 나옵니다 — " +
                    "위 «들어간다» 판정이 공허합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>테스트가 <b>직접</b> 만드는 폭. 프로덕션의 <c>MeasuredWidth</c>를 부르지 않는다 —
        /// 그러면 그 함수가 틀어질 때 기대값도 함께 틀어져 아무것도 못 잰다.</summary>
        private static float Ink(Text text, string content)
        {
            text.text = content ?? string.Empty;
            return Mathf.Ceil(text.preferredWidth);
        }
    }
}
