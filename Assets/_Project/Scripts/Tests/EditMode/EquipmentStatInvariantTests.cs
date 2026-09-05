using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 스탯 · 임계 · 세트의 <b>불변식</b> I-1 ~ I-4 (<c>docs/DESIGN_SYSTEMS_STATS.md</c> §13-6)
    /// 과 그 값들이 어디서 왔는지의 <b>교정</b>.
    ///
    /// ============================================================================
    /// ★ 기대값의 출처를 두 종류로 엄격히 나눈다
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>프로덕션 상수</b>(CAP · 임계 · 등급별 상승폭 · 세트 보너스 · 스탯 수)는
    ///    <b>숫자로 베끼지 않고 참조</b>한다. 베끼면 튜닝하는 날 무관한 테스트가 함께 빨개지고,
    ///    고치는 사람은 "숫자만 맞추면 되는 잡음"으로 학습한다(CLAUDE.md 2026-09-01 확정).</item>
    ///  <item><b>교정 앵커</b>(1일차 11/10/10/11 · 스탯별 최대 33/33/34/34 · 부스탯 편중 6/6/6/6)는
    ///    <b>일부러 숫자로 적는다</b>. 이 값은 프로덕션이 아니라 <c>design-systems</c>가
    ///    독립 계산기(<c>stats_r8_solver.py</c>)로 낸 <b>바깥의 실측</b>이고(§14-3 검산 표),
    ///    그것을 프로덕션 함수로 다시 만들면 <b>기준과 대상이 함께 틀어져 아무것도 못 잰다</b>
    ///    (TEAM.md 「생성기와 검사기가 같이 틀린다」).</item>
    /// </list>
    /// ★ 교정이 깨지면 그 뒤 숫자를 전부 폐기한다 — 표가 낡은 것이다.
    ///
    /// ============================================================================
    /// ★ I-2와 I-3은 짝이다 (§13-6)
    /// ============================================================================
    /// I-2만 걸면 "전부 동시에 되면 더 좋은 것 아닌가"로 흘러가고, I-3만 걸면 "아예 아무도 못 가면
    /// 통과"가 된다. <b>둘 다 걸어야 구간이 닫힌다.</b> 그래서 I-3은 상한(≤2)과 <b>도달 가능성</b>(=2)을
    /// 같은 테스트에서 함께 잰다 — <c>design-systems</c>의 첫 해가 I-2를 통과하고 I-3에서 0쌍이었다.
    /// </summary>
    public sealed class EquipmentStatInvariantTests
    {
        /// <summary>스탯을 주는 4슬롯. 순서는 <see cref="EquipmentStatRules.StatSlotAt"/>가 정한다.</summary>
        private static readonly EquipmentSlot[] StatSlots =
        {
            EquipmentStatRules.StatSlotAt(0), EquipmentStatRules.StatSlotAt(1),
            EquipmentStatRules.StatSlotAt(2), EquipmentStatRules.StatSlotAt(3),
        };

        private static readonly CharacterStat[] AllStats =
            (CharacterStat[])Enum.GetValues(typeof(CharacterStat));

        private static readonly ItemRarity[] AllRarities =
            (ItemRarity[])Enum.GetValues(typeof(ItemRarity));

        [TearDown]
        public void RestoreWornState() => EquipmentModel.ResetForTesting();

        // ====================================================================
        // 0. 교정 — 이 뒤의 숫자를 믿어도 되는 이유
        // ====================================================================

        /// <summary>
        /// 교정 ①: <b>1일차</b>(각 슬롯 0번 4종을 다 걸친 상태)의 4스탯이 §14-3 검산과 같다.
        /// 【앵커】<c>집중력 11 · 관찰력 10 · 매력 10 · 민첩 11 — 전부 초급 이상, 편차 1</c>.
        ///
        /// <para>★★ <b>2026-09-06 — 앵커는 그대로이고 「어디에 걸리는가」가 바뀌었다.</b>
        /// R21 테마 배정(안 B)으로 1일차 무료 4종이 전부 <c>mil</c>이 되어 <b>세트가 실제로 완성</b>된다
        /// (그것이 E2의 목적이다 — *"세트 완성의 첫 경험은 돈이 0원이다"*). 그래서 화면 총합은
        /// <b>앵커 + 세트 보너스</b>다. <b>앵커 숫자를 13/12/12/13으로 고쳐 적지 않는다</b> —
        /// 그러면 「장비 기여」와 「세트 기여」가 한 숫자에 뭉쳐 다음 사람이 무엇이 무엇인지 못 가린다.
        /// 세트 항은 <see cref="EquipmentStatRules.SetCompletionBonusPerStat"/>를 <b>참조</b>해서 더한다.</para>
        ///
        /// <para>★ 그리고 <b>바깥의 두 번째 앵커</b>로 교차 검산한다 — §21-4-c 표의
        /// <c>idx0 = 1일차 무료(E2) : Σstat 16 · +세트 8 = 24</c>. 스탯별 4칸과 총합 1칸이
        /// <b>서로 다른 출처</b>(§14-3 / §21-4-c)라, 한쪽이 낡으면 다른 쪽이 그것을 드러낸다.</para>
        /// </summary>
        [Test]
        public void 교정_1일차_네_종의_스탯이_설계_검산표와_같다()
        {
            StatBuild build = EvaluateIndices(0, 0, 0, 0);

            // E2가 살아 있으면 1일차 4종은 같은 테마다 — 그래서 세트가 붙는다.
            Assert.IsTrue(build.SetComplete,
                "1일차 무료 4종으로 세트가 완성되지 않습니다 — E2(§21-4-c)가 깨졌습니다.");
            int set = EquipmentStatRules.SetCompletionBonusPerStat;

            Assert.AreEqual(11 + set, build.Total.Focus, "1일차 집중력");
            Assert.AreEqual(10 + set, build.Total.Observation, "1일차 관찰력");
            Assert.AreEqual(10 + set, build.Total.Charm, "1일차 매력");
            Assert.AreEqual(11 + set, build.Total.Agility, "1일차 민첩");

            // 【바깥 앵커 2】§21-4-c: 장비 기여 합 16 · 세트 포함 24.
            int equipmentSum = build.EquipmentBonus.Focus + build.EquipmentBonus.Observation
                + build.EquipmentBonus.Charm + build.EquipmentBonus.Agility;
            Assert.AreEqual(16, equipmentSum,
                "1일차 장비 기여 합이 §21-4-c의 16이 아닙니다 — 두 설계 표가 갈라졌습니다.");
            Assert.AreEqual(24, equipmentSum + set * EquipmentStatRules.StatCount,
                "1일차 세트 포함 총합이 §21-4-c의 24가 아닙니다.");

            // "전부 초급 이상"은 임계 상수를 참조해서 다시 잰다(앵커가 아니라 규칙이다).
            foreach (CharacterStat stat in AllStats)
            {
                Assert.GreaterOrEqual(build.TierReached(stat), 1,
                    $"{EquipmentStatRules.StatName(stat)}이 1일차에 초급을 못 넘었습니다 " +
                    "— 사용자 확정(1일차 4스탯 전부 초급)이 깨졌습니다.");
            }
        }

        /// <summary>
        /// 교정 ②: 기본 카탈로그만으로 낼 수 있는 <b>스탯별 최대치</b>가 §21-2-e 검산과 같다.
        /// 【앵커】<c>35 / 33 / 32 / 34</c>(R15). ★ R8 표는 33/33/34/34였다 — 값이 그쪽으로
        /// 돌아가면 이 테스트가 잡는다.
        /// </summary>
        [Test]
        public void 교정_스탯별_최대치가_설계_검산표와_같다()
        {
            StatVector4 max = MaxPerStatOverBaseCatalog();

            Assert.AreEqual(35, max.Focus, "집중력 최대");
            Assert.AreEqual(33, max.Observation, "관찰력 최대");
            Assert.AreEqual(32, max.Charm, "매력 최대");
            Assert.AreEqual(34, max.Agility, "민첩 최대");
        }

        /// <summary>
        /// 교정 ③: 부스탯 편중이 없다. 【앵커】<c>각 스탯 정확히 6개(편차 0)</c>.
        /// <para>이 성질이 §14-3 제약 1의 실체다 — 인계본 표는 「집중력」이 6개로 몰려
        /// 1일차 편차가 4였다.</para>
        /// </summary>
        [Test]
        public void 교정_부스탯_방향이_네_스탯에_고르게_배분됐다()
        {
            var count = new int[EquipmentStatRules.StatCount];
            int total = 0;
            foreach (EquipmentSlot slot in StatSlots)
            {
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    int sub = ItemCatalog.SubStat(slot, i);
                    if (sub == EquipmentStatRules.NoStat) continue;
                    count[sub]++;
                    total++;
                }
            }

            Assert.AreEqual(ItemCatalog.SubStatTableCount, total,
                "카탈로그에서 읽힌 부스탯 개수가 표의 크기와 다릅니다 — 아이디가 갈렸습니다.");
            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(6, count[(int)stat],
                    $"{EquipmentStatRules.StatName(stat)}을 가리키는 부스탯이 6개가 아닙니다 " +
                    "— §14-3 제약 1(각 스탯 정확히 6개)이 깨졌습니다.");
            }
        }

        // ====================================================================
        // 1. 불변식 I-1 — 어떤 조합에서도 스탯 ≤ CAP
        // ====================================================================

        [Test]
        public void I1_기본_카탈로그의_어떤_조합에서도_스탯이_캡을_넘지_않는다()
        {
            int worst = 0;
            ForEachBaseLoadout((h, e, n, b) =>
            {
                StatBuild build = EquipmentStatRules.Evaluate(h, e, n, b);
                foreach (CharacterStat stat in AllStats)
                {
                    int v = build.Total.Of(stat);
                    if (v > worst) worst = v;
                    Assert.LessOrEqual(v, EquipmentStatRules.Cap,
                        $"{EquipmentStatRules.StatName(stat)}이 캡을 넘었습니다({v}).");
                }
            });
            Assert.Greater(worst, 0, "아무 조합도 안 돌았습니다 — 위 단언이 공허합니다.");

            // ★★ F2(§21-3) — 그리고 이 통과는 「공허하게 참」이다. 기본 42종만으로는 원시 총합이
            //    CAP에 닿지도 않는다(C3가 전설 부스탯 수렴을 구조적으로 금지한다). 그 사실 자체를
            //    여기서 못박아 둔다: ① 위 초록의 성질이 무엇인지 다음 사람이 오해하지 않게 하고,
            //    ② coder-ui의 막대 설계(D-4 「M3이면 막대가 최대 87.5%에서 멈춘다」)가 이 사실에
            //    직접 매달려 있다. 실제 클램프 발화는 아래 I-1b가 합성 로드아웃으로 따로 보인다.
            Assert.Less(worst, EquipmentStatRules.Cap,
                $"기본 카탈로그가 캡에 닿았습니다(최대 {worst}). F2가 더 이상 참이 아니고, " +
                "막대가 100%에 닿게 됩니다 — ux-designer(D-4)와 design-systems에 알려야 합니다.");
        }

        /// <summary>
        /// ★★ <b>I-1b</b>(§21-10-c 신설) — I-1의 <b>양성 대조</b>. 클램프가 실제로 발화하는 입력이 존재한다.
        ///
        /// <para><b>이게 없으면 I-1은 공허하게 참이고, 클램프를 삭제해도 초록이다.</b>
        /// §21-3 F2가 그것을 실측으로 못박았다 — 기본 42종만으로는 원시 최대가 <b>35</b>(R15 표)라
        /// CAP 40에 <b>닿지도 않는다</b>(C3가 전설 부스탯 수렴을 구조적으로 금지한다).
        /// 그래서 양성 대조를 <b>기본 카탈로그로 만들면 안 되고</b>, 부스탯이 전부 한 스탯을 가리키는
        /// <b>합성 로드아웃</b>이어야 한다.</para>
        ///
        /// <para>기대값은 상수에서 <b>유도</b>한다(41을 베끼지 않는다). ★ 41은 §14-4가 적어 둔
        /// 숫자인데 그것도 R8 표의 값이 아니라 <b>다른 배정의 값</b>이었다(F1) — 숫자를 옮겨 적었으면
        /// 이 테스트가 지금 빨갰을 것이다.</para>
        /// </summary>
        [Test]
        public void I1b_양성대조_부스탯이_한_스탯에_몰리면_원시값이_캡을_넘고_클램프가_자른다()
        {
            ItemRarity top = MaxRarity();
            const CharacterStat target = CharacterStat.Focus;   // 주스탯 슬롯이 HEAD인 스탯

            StatBuild build = EquipmentStatRules.Evaluate(
                Synthetic(EquipmentSlot.Head, top, target),
                Synthetic(EquipmentSlot.Eyes, top, target),
                Synthetic(EquipmentSlot.Neck, top, target),
                Synthetic(EquipmentSlot.Shoulders, top, target));

            int expectedRaw = EquipmentStatRules.BaseValue(target)
                + EquipmentStatRules.MainStatBonus(top)                 // HEAD 주스탯
                + EquipmentStatRules.SubStatBonus(top) * StatSlots.Length;  // 부스탯 4칸이 전부 여기로

            Assert.AreEqual(expectedRaw, build.Raw.Of(target), "원시 총합이 산식과 다릅니다.");
            Assert.Greater(build.Raw.Of(target), EquipmentStatRules.Cap,
                "원시 최대가 캡을 못 넘습니다 — I-1이 무의미한 통과가 됩니다.");
            Assert.AreEqual(EquipmentStatRules.Cap, build.Total.Of(target),
                "클램프가 안 걸렸습니다 — 화면에 캡을 넘는 숫자가 뜹니다.");
        }

        // ====================================================================
        // 2. 불변식 I-2 — 4스탯 각각 고급 도달 가능
        // ====================================================================

        [Test]
        public void I2_기본_카탈로그만으로_네_스탯_각각이_고급에_도달할_수_있다()
        {
            int top = EquipmentStatRules.TierThreshold(EquipmentStatRules.TierCount);
            StatVector4 max = MaxPerStatOverBaseCatalog();

            foreach (CharacterStat stat in AllStats)
            {
                Assert.GreaterOrEqual(max.Of(stat), top,
                    $"{EquipmentStatRules.StatName(stat)}은 기본 42종만으로 " +
                    $"{EquipmentStatRules.TierName(EquipmentStatRules.TierCount)}에 도달할 수 없습니다 " +
                    $"(최대 {max.Of(stat)} < {top}). 그 스탯의 마지막 눈금이 죽은 콘텐츠가 됩니다(§13-2).");
            }
        }

        // ====================================================================
        // 3. 불변식 I-3 — 동시에 고급 이상인 스탯은 최대 2개 (그리고 2개는 실제로 된다)
        // ====================================================================

        [Test]
        public void I3_동시_고급은_최대_둘이고_둘은_실제로_달성_가능하다()
        {
            int top = EquipmentStatRules.TierThreshold(EquipmentStatRules.TierCount);
            int best = 0;
            ForEachBaseLoadout((h, e, n, b) =>
            {
                StatBuild build = EquipmentStatRules.Evaluate(h, e, n, b);
                int hit = 0;
                foreach (CharacterStat stat in AllStats)
                {
                    if (build.Total.Of(stat) >= top) hit++;
                }
                if (hit > best) best = hit;
            });

            Assert.LessOrEqual(best, 2,
                $"동시에 고급인 스탯이 {best}개입니다 — 부스탯 제로섬이 깨졌습니다(누가 부스탯 규칙을 바꿨습니다).");
            Assert.AreEqual(2, best,
                "동시 고급이 2개인 조합이 하나도 없습니다 — 「선택이 존재한다」가 사라져 " +
                "I-2만 통과하는 배정이 됐습니다(§13-6: I-2와 I-3은 짝이다).");
        }

        /// <summary>
        /// ★ I-3을 <b>열거가 아니라 부등식으로</b> 한 번 더 잠근다(§14-4 "배정과 무관하다").
        /// 카탈로그가 바뀌어도 이 증명은 안 썩는다 — 위 열거 테스트와 <b>다른 자</b>다.
        ///
        /// <para>한 스탯이 고급에 닿으려면 부스탯이 몇 <b>칸</b> 필요한가를 상수에서 유도하고,
        /// 로드아웃이 내놓는 부스탯 칸(= 스탯 슬롯 수)과 견준다. 세트 보너스도 <b>깎아 준 뒤에</b>
        /// 센다 — 세트가 필요량을 낮추는 것을 빼먹으면 이 증명이 실제보다 헐거워진다.</para>
        /// </summary>
        [Test]
        public void I3_구조증명_가장_싼_세_스탯도_부스탯_칸을_초과한다()
        {
            ItemRarity top = MaxRarity();
            int threshold = EquipmentStatRules.TierThreshold(EquipmentStatRules.TierCount);
            int subUnit = EquipmentStatRules.SubStatBonus(top);
            Assert.Greater(subUnit, 0, "부스탯 최대 상승폭이 0입니다 — 아래 나눗셈이 성립하지 않습니다.");

            var slotsNeeded = new List<int>();
            foreach (CharacterStat stat in AllStats)
            {
                // 주스탯은 자기 슬롯 하나에서만 오고, 세트 보너스는 완성 시 모든 스탯에 붙는다.
                int need = threshold
                    - EquipmentStatRules.BaseValue(stat)
                    - EquipmentStatRules.MainStatBonus(top)
                    - EquipmentStatRules.SetCompletionBonusPerStat;
                slotsNeeded.Add(need <= 0 ? 0 : (need + subUnit - 1) / subUnit);
            }
            slotsNeeded.Sort();

            int cheapestThree = slotsNeeded[0] + slotsNeeded[1] + slotsNeeded[2];
            Assert.Greater(cheapestThree, StatSlots.Length,
                $"가장 싼 세 스탯의 부스탯 칸 합({cheapestThree})이 로드아웃이 내놓는 칸 수" +
                $"({StatSlots.Length}) 이하입니다 — 동시 3스탯 고급이 산술적으로 열렸습니다.");
        }

        // ====================================================================
        // 4. 불변식 I-4 — DLC를 무제한 추가해도 I-1·I-3 상한이 안 변한다
        // ====================================================================

        /// <summary>
        /// ★ 팩 등급 상한(<c>MaxDeclaredRarityForPack</c>)을 <b>일부러 무시하고</b> 최고 등급까지
        /// 넣어 본다. 상한이 언젠가 열려도(DS-L8) 이 결론이 안 바뀌는지 보려는 것이다 —
        /// 지금 상한 안에서만 재면 상한이 열리는 날 이 테스트가 아무것도 못 잡는다.
        /// </summary>
        [Test]
        public void I4_임의의_DLC_조합에서도_캡과_동시_고급_상한이_안_변한다()
        {
            int threshold = EquipmentStatRules.TierThreshold(EquipmentStatRules.TierCount);
            StatSlotLoadout[][] candidates = new StatSlotLoadout[StatSlots.Length][];
            for (int s = 0; s < StatSlots.Length; s++) candidates[s] = SyntheticCandidates(StatSlots[s]);

            int worstTotal = 0, bestSimultaneous = 0, combos = 0;
            foreach (StatSlotLoadout h in candidates[0])
            foreach (StatSlotLoadout e in candidates[1])
            foreach (StatSlotLoadout n in candidates[2])
            foreach (StatSlotLoadout b in candidates[3])
            {
                combos++;
                StatBuild build = EquipmentStatRules.Evaluate(h, e, n, b);
                int hit = 0;
                foreach (CharacterStat stat in AllStats)
                {
                    int v = build.Total.Of(stat);
                    if (v > worstTotal) worstTotal = v;
                    if (v >= threshold) hit++;
                }
                if (hit > bestSimultaneous) bestSimultaneous = hit;
            }

            Assert.Greater(combos, 10000, "합성 조합이 너무 적습니다 — 아래 상한 단언이 공허합니다.");
            Assert.LessOrEqual(worstTotal, EquipmentStatRules.Cap,
                "DLC를 섞으니 캡을 넘었습니다 — I-1의 상한이 움직였습니다.");
            Assert.LessOrEqual(bestSimultaneous, 2,
                $"DLC를 섞으니 동시 고급이 {bestSimultaneous}개가 됐습니다 — 재구매 압박(페이투윈)이 생깁니다.");

            // 양성 대조: 이 합성 공간이 실제로 「캡에 닿을 만큼」 세다(즉 상한 단언이 헐겁지 않다).
            Assert.AreEqual(EquipmentStatRules.Cap, worstTotal,
                "합성 공간이 캡에 닿지도 못했습니다 — 위 «캡을 안 넘었다»가 공허합니다.");
        }

        // ====================================================================
        // 5. 세트 — 테마 축, 문자열 동등성 (DS-G3 · §1-7)
        // ====================================================================

        [Test]
        public void 세트는_네_부위가_같은_테마일_때만_완성된다()
        {
            const string t = "office";
            Assert.IsTrue(EquipmentStatRules.IsSetComplete(t, t, t, t, out string theme));
            Assert.AreEqual(t, theme);

            Assert.IsFalse(EquipmentStatRules.IsSetComplete(t, t, t, "mil", out _),
                "한 칸이 다른데 세트가 완성됐습니다.");
        }

        /// <summary>★ <b>부재 단언 + 양성 대조</b>: 빈 테마는 「같다」로 세지 않는다.
        /// 지금 기본 42종의 테마가 전부 비어 있어서, 이 규칙이 없으면 <b>아무 세트도 안 맞춘 사람에게
        /// 세트 완성이 뜬다</b>(화면이 존재하지 않는 것을 약속하는 그 형태).</summary>
        [Test]
        public void 빈_테마는_같다고_세지_않는다()
        {
            Assert.IsFalse(EquipmentStatRules.IsSetComplete("", "", "", "", out _), "빈 문자열 4개");
            Assert.IsFalse(EquipmentStatRules.IsSetComplete(null, null, null, null, out _), "null 4개");
            // 양성 대조 — 같은 함수가 비어 있지 않은 같은 값은 실제로 완성으로 센다.
            Assert.IsTrue(EquipmentStatRules.IsSetComplete("x", "x", "x", "x", out _));
        }

        [Test]
        public void 세트_보너스는_스탯마다_상수만큼_더해지고_캡_안에서_흡수된다()
        {
            const string t = "cyber";
            ItemRarity common = ItemRarity.Common;

            StatBuild without = EquipmentStatRules.Evaluate(
                Synthetic(EquipmentSlot.Head, common, CharacterStat.Focus, "a"),
                Synthetic(EquipmentSlot.Eyes, common, CharacterStat.Focus, "b"),
                Synthetic(EquipmentSlot.Neck, common, CharacterStat.Focus, "c"),
                Synthetic(EquipmentSlot.Shoulders, common, CharacterStat.Focus, "d"));
            StatBuild with = EquipmentStatRules.Evaluate(
                Synthetic(EquipmentSlot.Head, common, CharacterStat.Focus, t),
                Synthetic(EquipmentSlot.Eyes, common, CharacterStat.Focus, t),
                Synthetic(EquipmentSlot.Neck, common, CharacterStat.Focus, t),
                Synthetic(EquipmentSlot.Shoulders, common, CharacterStat.Focus, t));

            Assert.IsFalse(without.SetComplete);
            Assert.IsTrue(with.SetComplete);
            Assert.AreEqual(t, with.SetTheme);

            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(without.Total.Of(stat) + EquipmentStatRules.SetCompletionBonusPerStat,
                    with.Total.Of(stat),
                    $"{EquipmentStatRules.StatName(stat)}에 세트 보너스가 안 들어갔습니다.");
            }

            // ★ 세트 보너스는 클램프 <b>전에</b> 더해진다 — 뒤에 더하면 캡을 넘겨 화면에 큰 숫자가 뜬다.
            ItemRarity top = MaxRarity();
            StatBuild capped = EquipmentStatRules.Evaluate(
                Synthetic(EquipmentSlot.Head, top, CharacterStat.Focus, t),
                Synthetic(EquipmentSlot.Eyes, top, CharacterStat.Focus, t),
                Synthetic(EquipmentSlot.Neck, top, CharacterStat.Focus, t),
                Synthetic(EquipmentSlot.Shoulders, top, CharacterStat.Focus, t));
            Assert.Greater(capped.Raw.Focus, EquipmentStatRules.Cap);
            Assert.AreEqual(EquipmentStatRules.Cap, capped.Total.Focus);
        }

        /// <summary>
        /// ★★ <b>세트가 실제로 완성된다</b> — 기본 42종만으로. 2026-09-05 R21 「안 B」 배정(리더 채택)
        /// 이전에는 이 자리에 정반대의 부재 단언(<i>"어떤 조합으로도 완성되지 않는다"</i>)이 있었고,
        /// 그것은 <b>목적을 다해</b> 존재 단언으로 뒤집혔다.
        ///
        /// <para>★ <b>대조를 같은 테스트 안에 둔다</b>(CLAUDE.md): 완성되는 조합이 있다는 것만 보이면
        /// "전부 완성된다"는 반대 방향의 결함(무소속을 「같다」로 세는 그 사고)을 못 잡는다. 그래서
        /// <b>완성 수</b>와 <b>미완성 수</b>를 같은 순회에서 함께 세고 둘 다 0이 아님을 단언한다.</para>
        ///
        /// <para>★ 완성 조합 수는 <b>E1에서 유도한다</b>(숫자를 베끼지 않는다): 각 테마가 4슬롯에
        /// 정확히 1종씩이면 그 테마를 완성하는 조합은 <b>테마당 정확히 1개</b>이고, 따라서 전체
        /// 완성 조합 수 == 실재 테마 수다. 이 등식이 깨지면 E1이 깨진 것이다.</para>
        /// </summary>
        [Test]
        public void 기본_카탈로그만으로_세트가_실제로_완성되고_대부분의_조합은_완성되지_않는다()
        {
            var completedThemes = new HashSet<string>();
            int complete = 0, incomplete = 0;
            ForEachBaseLoadout((h, e, n, b) =>
            {
                StatBuild build = EquipmentStatRules.Evaluate(h, e, n, b);
                if (build.SetComplete)
                {
                    complete++;
                    Assert.IsFalse(string.IsNullOrEmpty(build.SetTheme),
                        "세트가 완성됐는데 테마 이름이 비었습니다 — 무소속이 「같다」로 세어졌습니다.");
                    completedThemes.Add(build.SetTheme);
                }
                else
                {
                    incomplete++;
                }
            });

            Assert.Greater(complete + incomplete, 1000, "조합을 거의 안 돌았습니다 — 아래 단언이 공허합니다.");
            Assert.Greater(complete, 0,
                "기본 42종만으로 완성되는 세트가 하나도 없습니다 — 테마 배정이 사라졌거나 " +
                "IsSetComplete가 실재 테마를 무소속으로 보고 있습니다.");
            Assert.Greater(incomplete, 0,
                "★ 모든 조합이 완성됐습니다 — 무소속/서로 다른 테마를 「같다」로 세고 있습니다.");

            // E1에서 유도: 테마마다 완성 조합이 정확히 1개 ⇒ 완성 조합 수 == 실재 테마 수.
            Assert.AreEqual(ItemCatalog.AllThemes().Length, completedThemes.Count,
                "완성 가능한 테마 수가 실재 테마 수와 다릅니다 — E1(4슬롯 정확히 1종씩)이 깨졌습니다.");
            Assert.AreEqual(completedThemes.Count, complete,
                "테마 하나가 두 조합으로 완성됩니다 — 한 슬롯에 같은 테마가 2종 있습니다(E1 위반).");
        }

        /// <summary>
        /// ★ <b>1일차 무료 4종만으로 세트가 완성되고, 보너스가 실제로 붙는다</b>(E2의 통합 확인).
        /// <para>E2가 존재하는 이유가 *"세트 완성의 첫 경험은 돈이 0원이다"*(R14 §3-5)이므로,
        /// 「테마가 같다」로 끝내지 않고 <b>그 4종이 정말 Lv.1 보유분인지</b>와
        /// <b>총합이 실제로 <see cref="EquipmentStatRules.SetCompletionBonusPerStat"/>만큼 올랐는지</b>를
        /// 같은 테스트에서 잰다 — 앞의 둘 중 하나만 재면 「완성되는데 이득이 없다」를 못 본다.</para>
        /// </summary>
        [Test]
        public void 첫날_무료_4종만으로_세트가_완성되고_보너스가_붙는다()
        {
            const int firstIndex = 0;
            foreach (EquipmentSlot slot in StatSlots)
            {
                ItemCatalogEntry entry = ItemCatalog.Item(slot, firstIndex);
                Assert.IsNotNull(entry, $"{slot} {firstIndex}번 아이템이 없습니다.");
                Assert.AreEqual(1, entry.RequiredLevel,
                    $"{entry.Id}가 1일차 무료(Lv.1)가 아닙니다 — E2의 전제가 깨졌습니다.");
            }

            StatBuild day1 = EvaluateIndices(firstIndex, firstIndex, firstIndex, firstIndex);
            Assert.IsTrue(day1.SetComplete,
                "1일차 무료 4종으로 세트가 완성되지 않습니다 — E2(§21-4-c) 위반입니다.");

            // 대조 — 한 칸만 다른 테마로 갈아 끼우면 같은 함수가 완성을 취소한다.
            int otherIndex = FindIndexWithDifferentTheme(EquipmentSlot.Head, day1.SetTheme);
            StatBuild broken = EvaluateIndices(otherIndex, firstIndex, firstIndex, firstIndex);
            Assert.IsFalse(broken.SetComplete,
                "모자만 다른 테마로 바꿨는데 세트가 그대로입니다 — 위 「완성」이 판정 불가입니다.");

            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(EquipmentStatRules.SetCompletionBonusPerStat, day1.SetBonus.Of(stat),
                    $"{EquipmentStatRules.StatName(stat)}에 1일차 세트 보너스가 안 들어갔습니다.");
            }
        }

        /// <summary>
        /// ★★ <b>42종 전부 배정 완료</b> — 존재 단언. 종전의 트립와이어
        /// (<i>"42종 전부 미배정이다"</i>)를 뒤집은 것이고, <b>부재 방향을 같은 테스트에 함께 남긴다</b>:
        /// 스탯 4슬롯 24종은 <b>반드시 실재 테마</b>, 외형 3슬롯 18종은 <b>반드시 무소속</b>이다.
        ///
        /// <para>두 방향을 같이 재는 이유가 이 항목의 전부다 — 존재만 재면 「전부 같은 값으로 채워도
        /// 통과」하고, 부재만 재면 「전부 비워도 통과」한다. 실제 결함 두 가지가 각각 그 형태다:
        /// <b>DS-4′-e 침묵 실패</b>(스탯 아이템 하나가 비면 세트가 영원히 안 되는데 화면은 조용하다)와
        /// <b>무소속 오염</b>(외형에 실재 테마를 주면 그 값이 세트 판정에 새어 든다).</para>
        ///
        /// <para>★ <c>rank</c> 파생 금지(DS-4′-a)는 <see cref="테마는_등급이나_자리번호에서_파생되지_않는다"/>가
        /// 따로 지킨다 — 「값이 채워졌다」와 「값이 파생이 아니다」는 다른 질문이다.</para>
        /// </summary>
        [Test]
        public void 테마는_스탯_24종에_전부_배정됐고_외형_18종은_무소속이다()
        {
            var real = new HashSet<string>(ItemCatalog.AllThemes());
            int statItems = 0, appearanceItems = 0;

            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    if (entry == null) continue;

                    if (EquipmentStatRules.IsStatSlot(slot))
                    {
                        statItems++;
                        Assert.AreNotEqual(ItemCatalog.ThemeUnassigned, entry.Theme,
                            $"{entry.Id}(스탯 슬롯)의 테마가 무소속입니다 — DS-4′-e 침묵 실패입니다. " +
                            "이 아이템을 낀 로드아웃은 세트가 영원히 성립하지 않는데 화면은 아무 말도 안 합니다.");
                        Assert.IsTrue(real.Contains(entry.Theme),
                            $"{entry.Id}의 테마 '{entry.Theme}'가 실재 테마 6개에 없습니다 — " +
                            "표에 오타가 있거나 ItemCatalog.AllThemes()가 낡았습니다.");
                    }
                    else
                    {
                        appearanceItems++;
                        Assert.AreEqual(ItemCatalog.ThemeUnassigned, entry.Theme,
                            $"{entry.Id}(외형 슬롯)에 실재 테마 '{entry.Theme}'가 붙었습니다. " +
                            "외형 18종은 세트 계산 밖입니다(§21-4-c E1의 24칸은 스탯 4슬롯뿐).");
                    }
                }
            }

            // 공허 방지 — 두 모집단을 실제로 다 셌는가.
            Assert.AreEqual(ItemCatalog.SubStatTableCount, statItems,
                "스탯 슬롯 아이템 수가 부스탯 표 크기와 다릅니다 — 두 표가 같은 모집단이어야 합니다.");
            Assert.AreEqual(ItemCatalog.ThemeTableCount, statItems,
                "스탯 슬롯 아이템 수가 테마 표 크기와 다릅니다 — 표에 남거나 빠진 행이 있습니다.");
            Assert.Greater(appearanceItems, 0, "외형 아이템을 하나도 못 셌습니다 — 위 부재 단언이 공허합니다.");
            Assert.AreEqual(ItemCatalog.EquipmentCount, statItems + appearanceItems,
                "센 개수가 카탈로그 장비 수와 다릅니다 — 자리에 구멍이 있습니다.");

            // 무소속은 실재 테마가 아니다(DS-4′-b 「없음 ≠ 0」).
            Assert.IsFalse(real.Contains(ItemCatalog.ThemeUnassigned),
                "무소속 값이 실재 테마 목록에 들어 있습니다 — DS-4′-b 위반입니다.");
        }

        /// <summary>
        /// ★ <b>값 교정</b> — 24종의 테마가 인계 표(<c>r21_theme.out.txt</c> §6 「안 B」 ·
        /// <c>EQUIPMENT_HANDOFF_PORT_SPEC</c> §14-12-6)와 <b>한 칸도 다르지 않다</b>.
        ///
        /// <para>★ <b>이 표는 일부러 손으로 옮긴 앵커다</b>(이 파일 클래스 문서의 「교정 앵커」 규칙).
        /// 기대값을 <c>ItemCatalog.ThemeOfItem</c>에서 만들면 기준과 대상이 함께 틀어져
        /// <b>아무것도 못 잰다</b>. 다만 <b>테마 키 문자열은 프로덕션 상수를 참조</b>한다 —
        /// 앵커는 「천모자가 밀리터리다」라는 <b>배정</b>이지 <c>"mil"</c>이라는 철자가 아니다.</para>
        ///
        /// <para>★ 인계본 원문과 <b>4종이 다르다</b>(천모자·고글·외알안경·나비넥타이). 이건 오타가
        /// 아니라 리더 채택 「안 B」다 — 인계본 16종을 한 종도 안 바꾸면 E1·E2·E3를 동시에 만족하는
        /// 배정이 <b>존재하지 않는다</b>(§14-12-6). 되돌리면 세트가 구조적으로 완성 불가가 된다.</para>
        /// </summary>
        [Test]
        public void 테마_배정이_인계_표_안_B와_한_칸도_다르지_않다()
        {
            var anchor = new Dictionary<string, string>
            {
                // ---- HEAD ----                              ★ = 인계본 원문과 다른 4종(안 B)
                { "equip.head.cap",              ItemCatalog.ThemeMil },     // ★ 인계본 ink
                { "equip.head.fur",              ItemCatalog.ThemeSport },
                { "equip.head.fedora",           ItemCatalog.ThemeOffice },
                { "equip.head.crown",            ItemCatalog.ThemeCyber },
                { "equip.head.beret",            ItemCatalog.ThemeNeon },
                { "equip.head.straw",            ItemCatalog.ThemeInk },
                // ---- EYES ----
                { "equip.eyes.sunglasses",       ItemCatalog.ThemeMil },
                { "equip.eyes.round",            ItemCatalog.ThemeOffice },
                { "equip.eyes.goggles",          ItemCatalog.ThemeSport },   // ★ 인계본 cyber
                { "equip.eyes.monocle",          ItemCatalog.ThemeCyber },   // ★ 인계본 ink
                { "equip.eyes.browline",         ItemCatalog.ThemeNeon },
                { "equip.eyes.patch",            ItemCatalog.ThemeInk },
                // ---- NECK ----
                { "equip.neck.bowtie",           ItemCatalog.ThemeMil },     // ★ 인계본 office
                { "equip.neck.striped",          ItemCatalog.ThemeOffice },
                { "equip.neck.scarf",            ItemCatalog.ThemeSport },
                { "equip.neck.bell",             ItemCatalog.ThemeNeon },
                { "equip.neck.pendant",          ItemCatalog.ThemeCyber },
                { "equip.neck.bandana",          ItemCatalog.ThemeInk },
                // ---- BACK ----
                { "equip.shoulders.cape",        ItemCatalog.ThemeMil },
                { "equip.shoulders.long_cape",   ItemCatalog.ThemeCyber },
                { "equip.shoulders.wings",       ItemCatalog.ThemeNeon },
                { "equip.shoulders.backpack",    ItemCatalog.ThemeOffice },
                { "equip.shoulders.poncho",      ItemCatalog.ThemeSport },
                { "equip.shoulders.fairy_wings", ItemCatalog.ThemeInk },
            };

            Assert.AreEqual(ItemCatalog.ThemeTableCount, anchor.Count,
                "앵커 표의 행 수가 프로덕션 표와 다릅니다 — 한쪽에만 아이템이 늘었습니다.");

            int matched = 0;
            foreach (KeyValuePair<string, string> row in anchor)
            {
                ItemCatalogEntry entry = ItemCatalog.FindById(row.Key);
                Assert.IsNotNull(entry, $"카탈로그에 '{row.Key}'가 없습니다 — 앵커 표가 낡았습니다.");
                Assert.AreEqual(row.Value, entry.Theme,
                    $"{row.Key}의 테마가 인계 표(§14-12-6 안 B)와 다릅니다. " +
                    "배정을 바꾸려면 E1·E2·E3를 다시 풀어야 합니다 — 한 칸만 고치면 세트가 깨집니다.");
                matched++;
            }
            Assert.AreEqual(anchor.Count, matched, "앵커 행을 다 못 돌았습니다 — 위 대조가 공허합니다.");

            // 대조 — 같은 비교기가 「틀린 배정」은 실제로 잡는다(위 24건 통과가 공허하지 않다).
            Assert.AreNotEqual(ItemCatalog.FindById("equip.head.cap").Theme,
                ItemCatalog.FindById("equip.head.straw").Theme,
                "천모자와 밀짚모자가 같은 테마입니다 — E1(HEAD에 테마당 1종)이 깨졌습니다.");
        }

        /// <summary>
        /// ★ <b>E1</b>(§21-4-c) — 각 테마는 스탯 4슬롯에 <b>정확히 1종씩</b>.
        /// <para>한 슬롯에 2종·다른 슬롯에 0종이면 <b>그 테마는 4/4 완성이 영원히 불가능</b>하고,
        /// 그 사실은 화면 어디에도 안 뜬다. 그래서 24칸 전수로 센다.</para>
        /// </summary>
        [Test]
        public void E1_각_테마는_스탯_4슬롯에_정확히_1종씩이다()
        {
            string[] themes = ItemCatalog.AllThemes();
            int filled = 0;

            foreach (string theme in themes)
            {
                foreach (EquipmentSlot slot in StatSlots)
                {
                    int n = 0;
                    for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                    {
                        ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                        if (entry != null && entry.Theme == theme) n++;
                    }
                    Assert.AreEqual(1, n,
                        $"테마 '{theme}'가 {slot} 슬롯에 {n}종입니다(기대 1). " +
                        (n == 0 ? "그 테마는 4/4 완성이 영원히 불가능합니다." : "완성 조합이 둘로 갈립니다."));
                    filled += n;
                }
            }

            Assert.AreEqual(themes.Length * StatSlots.Length, filled,
                "6테마 × 4슬롯 = 24칸이 안 찼습니다.");
            Assert.AreEqual(ItemCatalog.ThemeTableCount, filled,
                "24칸 합계가 테마 표 크기와 다릅니다 — 표에 실재 테마가 아닌 값이 있습니다.");
        }

        /// <summary>
        /// ★ <b>E2 · E3</b>(§21-4-c) — 1일차 무료 4종(각 스탯 슬롯 <c>idx0</c>)이 같은 테마이고,
        /// 전설 4종이 같은 테마다. 그리고 <b>그 둘은 서로 다른 테마</b>여야 한다
        /// (같으면 「1일차에 전설 세트를 공짜로 완성」이라 등급 사다리가 무의미해진다).
        ///
        /// <para>★ 전설 4종을 <b>아이디로 적지 않는다</b> — 등급은 <c>requiredLevel</c> 파생이라
        /// 아이디를 베끼면 레벨을 한 칸 옮기는 날 검사만 옛 목록을 지킨다. 등급으로 찾는다.</para>
        /// </summary>
        [Test]
        public void E2_1일차_무료_4종과_E3_전설_4종이_각각_같은_테마다()
        {
            var day1 = new HashSet<string>();
            var legendary = new HashSet<string>();
            int legendaryCount = 0;

            foreach (EquipmentSlot slot in StatSlots)
            {
                ItemCatalogEntry first = ItemCatalog.Item(slot, 0);
                Assert.IsNotNull(first, $"{slot} 0번 아이템이 없습니다.");
                day1.Add(first.Theme);

                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    if (entry == null || ItemCatalog.Rarity(slot, i) != MaxRarity()) continue;
                    legendaryCount++;
                    legendary.Add(entry.Theme);
                }
            }

            Assert.AreEqual(1, day1.Count,
                $"1일차 무료 4종의 테마가 {day1.Count}종입니다 — E2 위반. " +
                "「세트 완성의 첫 경험은 돈이 0원이다」(R14 §3-5)가 성립하지 않습니다.");
            Assert.AreEqual(StatSlots.Length, legendaryCount,
                $"최고 등급이 슬롯당 1종이 아닙니다({legendaryCount}종) — E3의 전제가 깨졌습니다.");
            Assert.AreEqual(1, legendary.Count,
                $"전설 4종의 테마가 {legendary.Count}종입니다 — E3 위반. " +
                "혼합 최고를 이기는 세트가 하나도 없어 6테마 전부가 지배당합니다.");

            var first1 = new List<string>(day1)[0];
            var firstL = new List<string>(legendary)[0];
            Assert.AreNotEqual(first1, firstL,
                "1일차 무료 세트와 전설 세트가 같은 테마입니다 — 1일차에 전설 세트가 완성됩니다.");
        }

        /// <summary>
        /// ★★ <b>E3가 존재하는 이유</b>(§21-4-c) — 전설 세트가 <b>혼합 최고를 실제로 이긴다</b>.
        /// 【바깥 앵커】<c>혼합 최고 Σstat 84 · 전설 세트 84 + 8 = 92</c>.
        ///
        /// <para>「전설 4종이 같은 테마다」만 재면 <b>그게 무슨 이득인지</b>는 아무도 안 잰다. 이 값이
        /// 뒤집히면(세트 보너스를 줄이거나 전설 상승폭을 만지면) <b>6테마 전부가 지배당해</b>
        /// 세트가 죽은 콘텐츠가 되는데, 그 사고는 화면에 아무 증상이 없다.</para>
        ///
        /// <para>84·92는 <c>design-systems</c>가 독립 계산기로 낸 바깥의 수이고, 세트 항만
        /// 프로덕션 상수를 참조한다(둘이 갈라지면 그것이 신호다).</para>
        /// </summary>
        [Test]
        public void E3_전설_세트가_혼합_최고를_실제로_이긴다()
        {
            int mixedBest = 0;
            int legendarySet = -1;
            ForEachBaseLoadout((h, e, n, b) =>
            {
                StatBuild build = EquipmentStatRules.Evaluate(h, e, n, b);
                int equipmentSum = build.EquipmentBonus.Focus + build.EquipmentBonus.Observation
                    + build.EquipmentBonus.Charm + build.EquipmentBonus.Agility;
                if (equipmentSum > mixedBest) mixedBest = equipmentSum;

                if (!build.SetComplete) return;
                int withSet = equipmentSum + build.SetBonus.Focus + build.SetBonus.Observation
                    + build.SetBonus.Charm + build.SetBonus.Agility;
                if (withSet > legendarySet) legendarySet = withSet;
            });

            Assert.AreEqual(84, mixedBest,
                $"혼합 최고 Σstat가 {mixedBest}입니다(§21-4-c 앵커 84) — 등급 상승폭이나 부스탯 표가 " +
                "움직였습니다. 아래 「이긴다」의 기준선이 낡았습니다.");
            Assert.AreEqual(84 + EquipmentStatRules.SetCompletionBonusPerStat * EquipmentStatRules.StatCount,
                legendarySet,
                $"최고 세트의 Σstat가 {legendarySet}입니다(§21-4-c 앵커 92).");
            Assert.Greater(legendarySet, mixedBest,
                "★ 어떤 세트도 혼합 최고를 못 이깁니다 — 6테마 전부가 지배당해 세트가 죽은 콘텐츠가 됩니다.");
        }

        /// <summary>
        /// ★★ <b>DS-4′-a</b> — 테마는 <c>rank</c>·<c>requiredLevel</c>·<c>itemIndex</c> 어디서도
        /// 파생되지 않는다. <b>rank 파생을 끼워 넣으면 컴파일도 되고 다른 테스트도 초록인데
        /// 로직만 틀린다</b>(같은 등급 4개를 걸친 사람에게 테마 세트가 뜬다 — §21-4-b).
        ///
        /// <para>★ 함정이 하나 있다: E2·E3 때문에 <c>idx0</c>은 전부 같고 <c>idx5</c>도 전부 같다.
        /// 그것만 보면 「idx 파생」과 구분이 안 된다. 그래서 <b>나머지 자리에서 실제로 갈리는지</b>를
        /// 잰다 — 파생이면 같은 자리·같은 등급은 <b>반드시</b> 같은 테마다.</para>
        /// </summary>
        [Test]
        public void 테마는_등급이나_자리번호에서_파생되지_않는다()
        {
            // 자리 번호마다 4슬롯의 테마가 갈리는가. ★ 균일한 자리가 <b>정확히 둘</b>이어야 하고,
            //   그 둘은 E2(1일차 무료)와 E3(전설)가 그렇게 요구한 자리다 — 그 밖에 균일한 자리가
            //   생기면 그것이 「idx 파생」의 얼굴이다.
            var uniformThemes = new HashSet<string>();
            int splitByIndex = 0, uniform = 0;
            for (int i = 0; i < ItemCatalog.ItemCountIn(StatSlots[0]); i++)
            {
                var atIndex = new HashSet<string>();
                foreach (EquipmentSlot slot in StatSlots)
                {
                    if (i >= ItemCatalog.ItemCountIn(slot)) continue;
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    if (entry != null) atIndex.Add(entry.Theme);
                }
                if (atIndex.Count > 1) splitByIndex++;
                else if (atIndex.Count == 1)
                {
                    uniform++;
                    foreach (string t in atIndex) uniformThemes.Add(t);
                }
            }
            Assert.Greater(splitByIndex, 0,
                "★ 모든 자리 번호에서 4슬롯의 테마가 같습니다 — 테마가 itemIndex에서 파생되고 " +
                "있습니다(DS-4′-a 위반). 축은 테마이지 rank가 아닙니다(리더 확정 eca8c58).");
            Assert.AreEqual(2, uniform,
                $"4슬롯의 테마가 균일한 자리가 {uniform}개입니다(기대 2 = E2·E3가 요구한 두 자리). " +
                "더 많으면 idx 파생이고, 더 적으면 E2 또는 E3가 깨진 것입니다.");

            // 그 두 자리의 테마 = {1일차 무료 테마, 전설 테마}. 자리 번호를 베끼지 않고 값으로 맞춘다.
            var expectedUniform = new HashSet<string> { ItemCatalog.Item(StatSlots[0], 0).Theme };
            foreach (EquipmentSlot slot in StatSlots)
            {
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    if (ItemCatalog.Rarity(slot, i) != MaxRarity()) continue;
                    expectedUniform.Add(ItemCatalog.Item(slot, i).Theme);
                }
            }
            Assert.AreEqual(2, expectedUniform.Count,
                "1일차 무료 테마와 전설 테마가 같습니다 — 위 대조가 성립하지 않습니다.");
            Assert.IsTrue(uniformThemes.SetEquals(expectedUniform),
                $"균일한 자리의 테마 [{string.Join(",", uniformThemes)}]가 " +
                $"E2·E3가 요구한 [{string.Join(",", expectedUniform)}]와 다릅니다 — " +
                "다른 이유로 균일해진 자리가 있습니다(파생 의심).");

            var byRarity = new Dictionary<ItemRarity, HashSet<string>>();
            foreach (EquipmentSlot slot in StatSlots)
            {
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    if (entry == null) continue;
                    ItemRarity r = ItemCatalog.Rarity(slot, i);
                    if (!byRarity.TryGetValue(r, out HashSet<string> set))
                    {
                        set = new HashSet<string>();
                        byRarity[r] = set;
                    }
                    set.Add(entry.Theme);
                }
            }
            int splitByRarity = 0;
            foreach (KeyValuePair<ItemRarity, HashSet<string>> kv in byRarity)
            {
                if (kv.Value.Count > 1) splitByRarity++;
            }
            Assert.Greater(splitByRarity, 0,
                "★ 모든 등급대에서 테마가 하나뿐입니다 — 테마가 등급에서 파생되고 있습니다. " +
                "그러면 같은 등급 4개를 걸친 사람에게 테마 세트가 뜹니다(§21-4-b가 지목한 무증상 결함).");
            Assert.AreEqual(AllRarities.Length, byRarity.Count,
                "등급 4단이 다 안 나왔습니다 — 위 단언이 일부 등급을 안 봤습니다.");
        }

        /// <summary>
        /// ★★ <b>DS-G7은 폐기됐다 — 그리고 그것을 숫자로 남긴다</b>(§21-4-c F5).
        ///
        /// <para>R14의 원래 DS-G7은 <i>"한 테마의 4종은 「그 슬롯 최고 등급 대비 1단 내림」이 최대
        /// 1슬롯"</i>이었다. 전설 재고가 <b>슬롯당 1개</b>뿐이라 그 규칙은 <b>산술적으로 6테마 중
        /// 최대 1개만</b> 만족한다 — 걸 수 없는 규칙이라 비용이 아니라 모순이고, 그래서
        /// <b>E1~E4(DS-G7′)로 대체</b>됐다.</para>
        ///
        /// <para>이 테스트는 그 상한이 <b>실제로 1이고 배정이 그 1을 실현했는지</b>를 잰다.
        /// 누가 "DS-G7을 6테마 전부에 다시 걸자"고 하는 날 이 숫자가 그 자리에서 답한다.
        /// 만족하는 유일한 테마는 <b>전설 세트</b>(내림 0단 × 4슬롯)여야 한다 — E3와 같은 테마다.</para>
        /// </summary>
        [Test]
        public void DS_G7은_6테마_중_정확히_한_개만_만족하고_그것이_전설_세트다()
        {
            // 슬롯별 최고 등급(파생값이라 숫자로 적지 않고 카탈로그에서 센다).
            var slotTop = new Dictionary<EquipmentSlot, int>();
            foreach (EquipmentSlot slot in StatSlots)
            {
                int top = 0;
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    int r = RarityRung(ItemCatalog.Rarity(slot, i));
                    if (r > top) top = r;
                }
                slotTop[slot] = top;
            }

            var satisfying = new List<string>();
            foreach (string theme in ItemCatalog.AllThemes())
            {
                int oneDown = 0, deeper = 0;
                foreach (EquipmentSlot slot in StatSlots)
                {
                    for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                    {
                        ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                        if (entry == null || entry.Theme != theme) continue;
                        int drop = slotTop[slot] - RarityRung(ItemCatalog.Rarity(slot, i));
                        if (drop == 1) oneDown++;
                        else if (drop >= 2) deeper++;
                    }
                }
                if (oneDown <= 1 && deeper == 0) satisfying.Add(theme);
            }

            Assert.AreEqual(1, satisfying.Count,
                $"DS-G7 원문을 만족하는 테마가 {satisfying.Count}개입니다(F5가 증명한 상한 1). " +
                "0개면 배정이 상한에 못 미친 것이고, 2개 이상이면 F5의 산술이 반증된 것입니다 — " +
                "어느 쪽이든 design-systems에 돌려보내야 합니다.");

            // 그 하나는 전설 세트여야 한다(E3와 같은 테마).
            ItemCatalogEntry topOfHead = null;
            for (int i = 0; i < ItemCatalog.ItemCountIn(EquipmentSlot.Head); i++)
            {
                if (ItemCatalog.Rarity(EquipmentSlot.Head, i) != MaxRarity()) continue;
                topOfHead = ItemCatalog.Item(EquipmentSlot.Head, i);
            }
            Assert.IsNotNull(topOfHead, "HEAD에 최고 등급 아이템이 없습니다.");
            Assert.AreEqual(topOfHead.Theme, satisfying[0],
                "DS-G7을 만족하는 테마가 전설 세트가 아닙니다 — E3와 어긋납니다.");
        }

        // ====================================================================
        // 6. 임계 · 이음매 — 저장 스키마와 눈금이 같은가
        // ====================================================================

        /// <summary>★ <b>모듈 이음매</b>: 임계 단계 수와 저장 필드의 최대 등급이 같은 값이어야 한다.
        /// 갈라지면 «화면은 고급인데 영구 해금은 중급까지만 기록되는» 상태가 조용히 생긴다.</summary>
        [Test]
        public void 임계_단계_수와_저장_스키마의_최대_등급이_같다()
        {
            Assert.AreEqual(CurrencyRules.MaxStatTier, EquipmentStatRules.TierCount,
                "임계 단계 수와 statTierReached의 상한이 갈라졌습니다.");
            Assert.AreEqual(CurrencyRules.StatTierSlotCount, EquipmentStatRules.StatCount,
                "스탯 수와 statTierReached 배열 길이가 갈라졌습니다.");
            Assert.AreEqual(EquipmentStatRules.StatCount, AllStats.Length,
                "CharacterStat 열거값 수가 스탯 수와 다릅니다.");
            Assert.AreEqual(EquipmentStatRules.StatCount, StatSlots.Length,
                "주스탯을 가진 슬롯 수가 스탯 수와 다릅니다.");

            // 슬롯 ↔ 스탯이 전단사인가 — 두 슬롯이 같은 주스탯을 가지면 한 스탯이 두 배로 오른다.
            var seen = new HashSet<int>();
            foreach (EquipmentSlot slot in StatSlots)
            {
                int main = EquipmentStatRules.MainStatOf(slot);
                Assert.AreNotEqual(EquipmentStatRules.NoStat, main, $"{slot}에 주스탯이 없습니다.");
                Assert.IsTrue(seen.Add(main), $"{slot}의 주스탯이 다른 슬롯과 겹칩니다.");
            }
        }

        [Test]
        public void 단계는_임계_경계에서_정확히_바뀐다()
        {
            Assert.AreEqual(0, EquipmentStatRules.TierOf(0), "0이 단계 0이 아닙니다.");
            for (int tier = 1; tier <= EquipmentStatRules.TierCount; tier++)
            {
                int at = EquipmentStatRules.TierThreshold(tier);
                Assert.AreEqual(tier, EquipmentStatRules.TierOf(at), $"{at}에서 {tier}단이 아닙니다.");
                Assert.AreEqual(tier - 1, EquipmentStatRules.TierOf(at - 1),
                    $"{at - 1}에서 {tier - 1}단이 아닙니다 — 경계가 한 칸 밀렸습니다.");
                // ★ 이음매: 화면이 낸 단계가 저장 클램프를 <b>그대로</b> 통과해야 한다.
                //   잘리면 「화면은 고급인데 영구 해금은 중급까지만 기록」이 조용히 생긴다.
                Assert.AreEqual(tier, CurrencyRules.ClampStatTier(tier),
                    $"{tier}단이 저장 클램프에서 잘렸습니다 — 화면 단계와 영구 해금이 갈라집니다.");
            }

            // 최고 단계에서는 「다음까지」가 없다(§1-4 "최고 단계").
            Assert.IsFalse(EquipmentStatRules.TryNextThreshold(
                EquipmentStatRules.TierThreshold(EquipmentStatRules.TierCount), out _));

            // 그 아래에서는 남은 거리가 정확히 임계 − 현재값이다.
            int mid = EquipmentStatRules.TierThreshold(1);
            Assert.IsTrue(EquipmentStatRules.TryNextThreshold(mid, out int remaining));
            Assert.AreEqual(EquipmentStatRules.TierThreshold(2) - mid, remaining);
        }

        /// <summary>막대 눈금 — §1-4 【앵커】<c>10/40 = 25% · 20/40 = 50% · 32/40 = 80%</c>.</summary>
        [Test]
        public void 교정_막대_눈금이_설계_비율과_같다()
        {
            Assert.AreEqual(0.25f, EquipmentStatRules.TierMark01(1), 1e-4f, "초급 눈금");
            Assert.AreEqual(0.50f, EquipmentStatRules.TierMark01(2), 1e-4f, "중급 눈금");
            Assert.AreEqual(0.80f, EquipmentStatRules.TierMark01(3), 1e-4f, "고급 눈금");
            Assert.AreEqual(1f, EquipmentStatRules.Progress01(EquipmentStatRules.Cap), 1e-4f);
            Assert.AreEqual(1f, EquipmentStatRules.Progress01(EquipmentStatRules.Cap * 2), 1e-4f,
                "막대가 100%를 넘었습니다.");
        }

        // ====================================================================
        // 7. 임계 효과 문구 — 있는 것과 「사실인 것」을 가른다
        // ====================================================================

        [Test]
        public void 임계_효과_문구는_열두_칸_전부_채워져_있고_단계_0에는_없다()
        {
            foreach (CharacterStat stat in AllStats)
            {
                Assert.IsEmpty(EquipmentStatRules.TierEffectText(stat, 0),
                    $"{EquipmentStatRules.StatName(stat)}의 「임계 미달」에 효과 문구가 있습니다.");
                for (int tier = 1; tier <= EquipmentStatRules.TierCount; tier++)
                {
                    Assert.IsNotEmpty(EquipmentStatRules.TierEffectText(stat, tier),
                        $"{EquipmentStatRules.StatName(stat)} {tier}단의 효과 문구가 비었습니다.");
                }
            }
        }

        /// <summary>★ 문구가 있는 것과 <b>효과가 있는 것</b>은 다른 사실이다(원칙 1).
        /// 표와 개수가 어긋나면 둘 중 하나가 낡은 것이다 — 누가 효과 하나를 배선하면
        /// 여기가 빨개져서 반드시 같이 갱신하게 된다.</summary>
        [Test]
        public void 지금_실제로_발동하는_임계_효과_개수가_상수와_같다()
        {
            int active = 0;
            foreach (CharacterStat stat in AllStats)
            {
                for (int tier = 1; tier <= EquipmentStatRules.TierCount; tier++)
                {
                    if (EquipmentStatRules.IsTierEffectActive(stat, tier)) active++;
                }
            }
            Assert.AreEqual(EquipmentStatRules.ActiveTierEffectCount, active,
                "IsTierEffectActive 표와 ActiveTierEffectCount가 갈라졌습니다.");
        }

        // ====================================================================
        // 8. 아이템 데이터 — 스탯 슬롯 24종 / 외형 18종
        // ====================================================================

        /// <summary>★ <b>I-15 (제약 C1)</b> — 각 슬롯의 6종 부스탯이 <b>자기 주스탯을 뺀 3스탯을
        /// 정확히 2번씩</b> 가리킨다(§21-10-c I-15). 「자기 주스탯이 아니다」만 걸면 한 슬롯이
        /// 한 스탯에 3번 몰려도 통과한다 — 그 상태가 곧 부스탯 편중이다.</summary>
        [Test]
        public void I15_각_슬롯의_여섯_종이_나머지_세_스탯을_정확히_두_번씩_가리킨다()
        {
            int counted = 0;
            foreach (EquipmentSlot slot in StatSlots)
            {
                int main = EquipmentStatRules.MainStatOf(slot);
                int n = ItemCatalog.ItemCountIn(slot);
                Assert.Greater(n, 0, $"{slot}에 아이템이 없습니다 — 아래 단언이 공허합니다.");

                var perStat = new int[EquipmentStatRules.StatCount];
                for (int i = 0; i < n; i++)
                {
                    int sub = ItemCatalog.SubStat(slot, i);
                    Assert.AreNotEqual(EquipmentStatRules.NoStat, sub,
                        $"{ItemCatalog.Item(slot, i).Id}에 부스탯 방향이 없습니다 — 표에서 빠졌습니다.");
                    Assert.AreNotEqual(main, sub,
                        $"{ItemCatalog.Item(slot, i).Id}의 부스탯이 자기 슬롯의 주스탯과 같습니다 " +
                        "— 그 아이템만 한 스탯을 두 번 올립니다(C1).");
                    perStat[sub]++;
                    counted++;
                }

                foreach (CharacterStat stat in AllStats)
                {
                    int expected = (int)stat == main ? 0 : 2;
                    Assert.AreEqual(expected, perStat[(int)stat],
                        $"{slot}의 부스탯이 {EquipmentStatRules.StatName(stat)}을 " +
                        $"{perStat[(int)stat]}번 가리킵니다(기대 {expected}) — C1이 깨졌습니다.");
                }
            }
            Assert.AreEqual(ItemCatalog.SubStatTableCount, counted);
        }

        /// <summary>
        /// ★★ <b>I-14 (제약 C3)</b> — 각 스탯이 받는 부스탯 6개의 <b>등급 구성</b>이
        /// <c>일반2 · 희귀2 · 영웅1 · 전설1</c>이다(§21-2-b · §21-10-c I-14).
        ///
        /// <para><b>이 축이 없으면 교정 ③(총 6개씩)이 통과하면서도 병이 남는다</b> — R8 표가 정확히
        /// 그랬다: 총 개수는 6/6/6/6인데 <b>영웅 4종을 다 사면 관찰력만 +12 오르고 집중력·매력은 0</b>이고
        /// 관찰력은 전설 부스탯을 하나도 못 받았다. 리더가 §13-4에서 지목한 병의 확대판이다.</para>
        ///
        /// <para>기대 분포는 숫자를 베끼지 않고 <b>24종의 등급 구성에서 유도</b>한다 —
        /// 등급 사다리가 바뀌면 기대값이 저절로 따라간다.</para>
        /// </summary>
        [Test]
        public void I14_각_스탯이_받는_부스탯_여섯_개의_등급_구성이_균등하다()
        {
            // 실제 카탈로그에서 등급별 총 개수를 센 뒤 스탯 수로 나눈다(= 완전 균등 기대치).
            var totalByRarity = new Dictionary<ItemRarity, int>();
            var byStatRarity = new Dictionary<ItemRarity, int[]>();
            foreach (ItemRarity r in AllRarities)
            {
                totalByRarity[r] = 0;
                byStatRarity[r] = new int[EquipmentStatRules.StatCount];
            }

            foreach (EquipmentSlot slot in StatSlots)
            {
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    int sub = ItemCatalog.SubStat(slot, i);
                    if (sub == EquipmentStatRules.NoStat) continue;
                    ItemRarity r = ItemCatalog.Rarity(slot, i);
                    totalByRarity[r]++;
                    byStatRarity[r][sub]++;
                }
            }

            foreach (ItemRarity r in AllRarities)
            {
                int total = totalByRarity[r];
                Assert.AreEqual(0, total % EquipmentStatRules.StatCount,
                    $"등급 {ItemCatalog.RarityName(r)}이 {total}종이라 4스탯으로 정확히 안 나뉩니다 " +
                    "— C3의 산술 전제(§21-2-b)가 깨졌습니다.");
                int expected = total / EquipmentStatRules.StatCount;

                foreach (CharacterStat stat in AllStats)
                {
                    Assert.AreEqual(expected, byStatRarity[r][(int)stat],
                        $"{ItemCatalog.RarityName(r)} 등급의 부스탯이 " +
                        $"{EquipmentStatRules.StatName(stat)}에 {byStatRarity[r][(int)stat]}개 " +
                        $"몰렸습니다(기대 {expected}) — 「그 등급대를 다 사면 한 스탯만 오른다」로 " +
                        "되돌아갔습니다(§13-4).");
                }
            }

            // 공허 방지 — 등급이 실제로 여러 종류 있었는가.
            int nonEmpty = 0;
            foreach (ItemRarity r in AllRarities) if (totalByRarity[r] > 0) nonEmpty++;
            Assert.AreEqual(AllRarities.Length, nonEmpty,
                "등급 4단이 다 안 나왔습니다 — 위 단언이 일부 등급을 안 봤습니다.");
        }

        /// <summary>★ 부재 단언 + 양성 대조 — 외형 3슬롯 18종은 스탯에 <b>구조적으로</b> 기여하지 않는다
        /// (§14-1 "외형 18종은 스탯 기여 0"). 플레이스홀더가 아니라 사실이다.</summary>
        [Test]
        public void 외형_세_슬롯은_스탯에_기여하지_않는다()
        {
            var appearance = new[] { EquipmentSlot.Hair, EquipmentSlot.Fx, EquipmentSlot.Pet };
            int counted = 0;
            foreach (EquipmentSlot slot in appearance)
            {
                Assert.IsFalse(EquipmentStatRules.IsStatSlot(slot), $"{slot}에 주스탯이 생겼습니다.");
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    counted++;
                    var loadout = new StatSlotLoadout(slot, true, MaxRarity(),
                        ItemCatalog.SubStat(slot, i), ItemCatalog.Item(slot, i).Theme);
                    StatVector4 c = EquipmentStatRules.Contribution(loadout);
                    foreach (CharacterStat stat in AllStats)
                    {
                        Assert.AreEqual(0, c.Of(stat),
                            $"{ItemCatalog.Item(slot, i).Id}(외형)이 " +
                            $"{EquipmentStatRules.StatName(stat)}에 기여했습니다.");
                    }
                }
            }
            Assert.Greater(counted, 0, "외형 아이템을 하나도 못 셌습니다 — 위 단언이 공허합니다.");

            // 양성 대조 — 같은 함수가 스탯 슬롯 아이템에서는 실제로 값을 낸다.
            var real = new StatSlotLoadout(EquipmentSlot.Head, true, MaxRarity(),
                ItemCatalog.SubStat(EquipmentSlot.Head, 0), null);
            Assert.Greater(EquipmentStatRules.Contribution(real).Focus, 0,
                "스탯 슬롯 아이템도 0을 냅니다 — 위 「0건」이 판정 불가입니다.");
        }

        /// <summary>착용 상태 → 로드아웃 배선. 값이 아니라 <b>연결</b>을 잰다
        /// (값은 위 교정 테스트가 이미 설계 앵커로 잠갔다).</summary>
        [Test]
        public void 로드아웃은_지금_착용한_것을_읽는다()
        {
            EquipmentModel.ResetForTesting();

            foreach (EquipmentSlot slot in StatSlots)
            {
                int worn = EquipmentModel.WornIndex(slot);
                StatSlotLoadout loadout = EquipmentStatRules.SlotLoadout(slot);

                Assert.AreEqual(worn >= 0, loadout.Worn, $"{slot}의 착용 여부가 어긋납니다.");
                if (worn < 0) continue;

                Assert.AreEqual(ItemCatalog.Rarity(slot, worn), loadout.Rarity, $"{slot}의 등급");
                Assert.AreEqual(ItemCatalog.SubStat(slot, worn), loadout.SubStat, $"{slot}의 부스탯");
            }

            // 미착용 칸의 기여는 0이다(그리고 CurrentBuild가 그것을 그대로 쓴다).
            StatBuild build = EquipmentStatRules.CurrentBuild();
            foreach (CharacterStat stat in AllStats)
            {
                Assert.GreaterOrEqual(build.Total.Of(stat), EquipmentStatRules.BaseValue(stat),
                    $"{EquipmentStatRules.StatName(stat)}이 기본값보다 낮습니다.");
            }
        }

        /// <summary>
        /// ★ 이미 있는 v10 필드 <c>statTierReached</c>에 눈금이 <b>제 칸으로</b> 들어가고,
        /// 한 번 오른 칸은 <b>내려가지 않는다</b>(영구 해금). 새 저장 필드는 하나도 안 만들었다.
        /// <para>1-기준/0-기준이 갈리는 자리라 <b>스탯마다 서로 다른 단계</b>를 넣어서 잰다 —
        /// 네 칸이 같은 값이면 배열이 통째로 밀려도 통과한다.</para>
        /// </summary>
        [Test]
        public void 도달_단계는_이미_있는_v10_영구해금_칸에_래칫으로_기록된다()
        {
            CurrencyModel.ResetForTesting();
            ItemRarity top = MaxRarity();

            // 매력을 고급까지 밀어 올리는 조합: NECK 주스탯 + 매력을 가리키는 부스탯 셋.
            StatBuild high = EquipmentStatRules.Evaluate(
                Synthetic(EquipmentSlot.Head, top, CharacterStat.Charm),
                Synthetic(EquipmentSlot.Eyes, top, CharacterStat.Charm),
                Synthetic(EquipmentSlot.Neck, top, CharacterStat.Charm),
                Synthetic(EquipmentSlot.Shoulders, top, CharacterStat.Agility));

            Assert.IsTrue(EquipmentStatRules.RecordTierHighWaterMarks(high),
                "처음 기록인데 아무 칸도 안 올랐습니다.");
            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(high.TierReached(stat), CurrencyModel.StatTierReached((int)stat),
                    $"{EquipmentStatRules.StatName(stat)}의 눈금이 다른 칸에 들어갔습니다 " +
                    "— 스탯 번호와 저장 배열 자리가 어긋났습니다.");
            }
            // 네 칸이 전부 같은 값이면 위 단언이 배열 밀림을 못 잡는다 — 실제로 갈렸는지 확인한다.
            var distinct = new HashSet<int>();
            foreach (CharacterStat stat in AllStats) distinct.Add(high.TierReached(stat));
            Assert.Greater(distinct.Count, 1, "네 스탯의 단계가 전부 같습니다 — 위 대조가 공허합니다.");

            // 래칫 — 아무것도 안 걸친 상태로 다시 기록해도 내려가지 않는다.
            StatBuild bare = EquipmentStatRules.Evaluate(
                StatSlotLoadout.Empty(EquipmentSlot.Head), StatSlotLoadout.Empty(EquipmentSlot.Eyes),
                StatSlotLoadout.Empty(EquipmentSlot.Neck), StatSlotLoadout.Empty(EquipmentSlot.Shoulders));
            Assert.IsFalse(EquipmentStatRules.RecordTierHighWaterMarks(bare),
                "장비를 다 벗었는데 저장할 변화가 생겼습니다.");
            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(high.TierReached(stat), CurrencyModel.StatTierReached((int)stat),
                    $"{EquipmentStatRules.StatName(stat)}의 영구 해금이 내려갔습니다.");
            }

            // 상한 — 최고 단계가 저장 클램프 안에 있다.
            Assert.LessOrEqual(EquipmentStatRules.TierCount, CurrencyRules.MaxStatTier);
            CurrencyModel.ResetForTesting();
        }

        // ====================================================================
        // 도구
        // ====================================================================

        private static ItemRarity MaxRarity()
        {
            ItemRarity top = AllRarities[0];
            foreach (ItemRarity r in AllRarities)
            {
                if (EquipmentStatRules.MainStatBonus(r) > EquipmentStatRules.MainStatBonus(top)) top = r;
            }
            return top;
        }

        /// <summary>등급 사다리의 <b>단</b>(0 = 최하). ★ <c>(int)rarity</c>로 세지 않는다 —
        /// enum 값 순서가 사다리 순서라는 보장이 없고, 그 가정이 틀어지면 「1단 내림」이 조용히
        /// 다른 것을 센다. 상승폭(<see cref="EquipmentStatRules.MainStatBonus"/>)이 사다리의 실체다.</summary>
        private static int RarityRung(ItemRarity rarity)
        {
            int rung = 0;
            foreach (ItemRarity r in AllRarities)
            {
                if (EquipmentStatRules.MainStatBonus(r) < EquipmentStatRules.MainStatBonus(rarity)) rung++;
            }
            return rung;
        }

        /// <summary>이 슬롯에서 <paramref name="theme"/>가 <b>아닌</b> 아이템의 자리. 대조용이고,
        /// 못 찾으면 그 자체가 결함이라 실패시킨다(E1이 살아 있으면 반드시 있다).</summary>
        private static int FindIndexWithDifferentTheme(EquipmentSlot slot, string theme)
        {
            for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
            {
                ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                if (entry != null && entry.Theme != theme) return i;
            }
            Assert.Fail($"{slot}에 '{theme}'가 아닌 아이템이 없습니다 — 대조를 만들 수 없습니다.");
            return -1;
        }

        private static StatSlotLoadout Synthetic(EquipmentSlot slot, ItemRarity rarity,
            CharacterStat sub, string theme = null)
            => new StatSlotLoadout(slot, true, rarity, (int)sub, theme);

        /// <summary>합성 DLC 후보 — 등급 전부 × 부스탯 방향 전부 + 부스탯 없음 + 미착용.</summary>
        private static StatSlotLoadout[] SyntheticCandidates(EquipmentSlot slot)
        {
            var list = new List<StatSlotLoadout> { StatSlotLoadout.Empty(slot) };
            foreach (ItemRarity r in AllRarities)
            {
                list.Add(new StatSlotLoadout(slot, true, r, EquipmentStatRules.NoStat, "pack"));
                foreach (CharacterStat s in AllStats)
                {
                    list.Add(new StatSlotLoadout(slot, true, r, (int)s, "pack"));
                }
            }
            return list.ToArray();
        }

        private static StatSlotLoadout FromCatalog(EquipmentSlot slot, int itemIndex)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, itemIndex);
            Assert.IsNotNull(entry, $"{slot} {itemIndex}번 아이템이 없습니다.");
            return new StatSlotLoadout(slot, true, ItemCatalog.Rarity(slot, itemIndex),
                entry.SubStat, entry.Theme);
        }

        private static StatBuild EvaluateIndices(int head, int eyes, int neck, int back)
            => EquipmentStatRules.Evaluate(
                FromCatalog(EquipmentSlot.Head, head),
                FromCatalog(EquipmentSlot.Eyes, eyes),
                FromCatalog(EquipmentSlot.Neck, neck),
                FromCatalog(EquipmentSlot.Shoulders, back));

        private static void ForEachBaseLoadout(
            Action<StatSlotLoadout, StatSlotLoadout, StatSlotLoadout, StatSlotLoadout> body)
        {
            var candidates = new StatSlotLoadout[StatSlots.Length][];
            for (int s = 0; s < StatSlots.Length; s++)
            {
                EquipmentSlot slot = StatSlots[s];
                int n = ItemCatalog.ItemCountIn(slot);
                candidates[s] = new StatSlotLoadout[n];
                for (int i = 0; i < n; i++) candidates[s][i] = FromCatalog(slot, i);
            }

            foreach (StatSlotLoadout h in candidates[0])
            foreach (StatSlotLoadout e in candidates[1])
            foreach (StatSlotLoadout n2 in candidates[2])
            foreach (StatSlotLoadout b in candidates[3]) body(h, e, n2, b);
        }

        private static StatVector4 MaxPerStatOverBaseCatalog()
        {
            int f = 0, o = 0, c = 0, a = 0;
            ForEachBaseLoadout((h, e, n, b) =>
            {
                StatBuild build = EquipmentStatRules.Evaluate(h, e, n, b);
                if (build.Total.Focus > f) f = build.Total.Focus;
                if (build.Total.Observation > o) o = build.Total.Observation;
                if (build.Total.Charm > c) c = build.Total.Charm;
                if (build.Total.Agility > a) a = build.Total.Agility;
            });
            return new StatVector4(f, o, c, a);
        }
    }
}
