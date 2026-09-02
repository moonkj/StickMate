using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 등급 파생 — <c>ItemCatalog.Rarity</c>가 <b>슬롯 안 requiredLevel 순위</b>에서만 나오는가.
    ///
    /// ============================================================================
    /// 왜 이게 검사할 가치가 있는가
    /// ============================================================================
    /// 등급은 <b>저장 필드가 0개</b>다(design/systems/ECONOMY_SPEC.md §3-2). 애셋에도 세이브에도
    /// 등급이라는 값이 없고, 전부 <c>requiredLevel</c>에서 파생된다. 그래서 이 파생이 조용히
    /// 어긋나면 <b>어디에도 대조할 원본이 없다</b> — 화면의 리본 칸 수가 그대로 정답이 되어 버린다.
    ///
    /// 그리고 이 규칙이 지키는 것은 색이 아니라 <b>약속</b>이다: "레벨이 오를수록 스탯이 절대
    /// 내려가지 않는다". 등급을 손으로 배정할 수 있게 두면 그 단조성이 사람 손에 맡겨진다
    /// (ECONOMY_SPEC §3-2의 왕관 Lv.20 / 밀짚모자 Lv.26 사례).
    ///
    /// ============================================================================
    /// ★ 이 파일이 <b>지금 트리에서 구분하지 못하는 것</b> (정직하게 먼저 적는다)
    /// ============================================================================
    /// 출하된 42종은 슬롯마다 <c>requiredLevel</c>이 <b><c>itemIndex</c> 순서 그대로 오름차순</b>이다.
    /// 즉 <b>rank == itemIndex</b>이고, "요구 레벨 순위에서 파생"과 "자리 번호에서 파생"은
    /// <b>행동으로 구분되지 않는다</b>. 그래서 아래 두 가지를 함께 한다.
    ///   · 순위를 이 테스트가 <b>애셋 값으로 독립 계산</b>해 프로덕션 출력과 대조한다.
    ///   · 그것만으로는 위 두 가설이 갈리지 않으므로, 파생이 실제로 <c>RequiredLevel</c>을 읽는지
    ///     <b>소스에서</b> 확인한다(<see cref="파생이_실제로_RequiredLevel을_읽는다_소스_확인"/>).
    /// 둘 다 있어야 "요구 레벨에서 나온다"가 주장이 아니라 검사가 된다.
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는다 — 다만 <b>스펙 표</b>는 여기 산다
    /// ============================================================================
    /// <see cref="EconomySpecLadder"/>는 프로덕션 상수의 사본이 <b>아니다.</b> ECONOMY_SPEC §3-2가
    /// 정한 계약 그 자체이고, 프로덕션(<c>ItemCatalog._rarityByRank</c>)이 그 계약을 지키는지
    /// 보는 것이 이 파일의 일이다. 그래서 프로덕션이 바뀌면 <b>여기가 빨개져야 한다</b>.
    /// 그 밖의 모든 값(슬롯 수·아이템 수·요구 레벨)은 카탈로그에게 묻는다.
    /// </summary>
    public sealed class ItemRarityDerivationTests
    {
        private const string LogPrefix = "[등급파생]";

        /// <summary>
        /// ECONOMY_SPEC §3-2를 그대로 옮긴 계약: <c>rank 0,1 = 일반 / 2,3 = 희귀 / 4 = 영웅 / 5 = 전설</c>.
        /// <b>프로덕션 배열을 참조하지 않는다</b> — 참조하면 이 파일이 프로덕션이 아니라 자기 자신을
        /// 검사하게 된다. 이 표와 프로덕션이 갈라지는 것이 곧 결함이다.
        /// </summary>
        private static readonly ItemRarity[] EconomySpecLadder =
        {
            ItemRarity.Common, ItemRarity.Common,
            ItemRarity.Rare, ItemRarity.Rare,
            ItemRarity.Epic,
            ItemRarity.Legendary,
        };

        // ============================================================================
        // 0. 열거형 자체 — 칸 수가 열거값에서 직접 나온다
        // ============================================================================

        /// <summary>★ 리본 칸 수는 <c>(int)rarity + 1</c>이다. 그래서 "개수를 나르는 함수"가 없다.
        /// 열거값을 재배열하거나 구멍을 내면 화면의 칸 수가 조용히 바뀐다 —
        /// 이 검사가 그 순간을 잡는다(PALETTE_SPEC §14-2).</summary>
        [Test]
        public void 등급_열거값이_0부터_빈틈없이_이어진다_칸_수가_여기서_나온다()
        {
            var values = (ItemRarity[])System.Enum.GetValues(typeof(ItemRarity));
            Assert.Greater(values.Length, 0, $"{LogPrefix} ItemRarity에 값이 하나도 없습니다.");

            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(i, (int)values[i],
                    $"{LogPrefix} {values[i]}의 값이 {(int)values[i]}입니다(기대 {i}). 리본 칸 수는 " +
                    "(int)rarity + 1이므로 값에 구멍이 나면 칸 수가 통째로 어긋납니다.");

                int cells = (int)values[i] + 1;
                Assert.AreEqual(i + 1, cells,
                    $"{LogPrefix} {values[i]}의 칸 수가 {cells}입니다(기대 {i + 1}).");
            }

            Debug.Log($"{LogPrefix} 등급 {values.Length}단, 칸 수 1~{values.Length}.");
        }

        /// <summary>낱말이 전 등급에 있고 서로 다르다. 하나가 비면 상세 패널이 빈칸을 그리고,
        /// 둘이 같으면 "색이 못 하는 일"(식별)을 낱말도 못 하게 된다(PALETTE_SPEC §12-0).</summary>
        [Test]
        public void 등급_낱말이_전부_있고_서로_다르다()
        {
            var seen = new Dictionary<string, ItemRarity>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                string name = ItemCatalog.RarityName(r);
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"{LogPrefix} {r}의 낱말이 비었습니다.");
                if (seen.TryGetValue(name, out ItemRarity owner))
                {
                    Assert.Fail($"{LogPrefix} 낱말 '{name}'을(를) {r}와(과) {owner}가 함께 씁니다 — " +
                                "상세 패널이 두 등급을 같은 말로 부릅니다.");
                }
                seen[name] = r;
            }
            Debug.Log($"{LogPrefix} 낱말 {seen.Count}개: {string.Join(" / ", seen.Keys)}");
        }

        // ============================================================================
        // 1. 본안 — 출하된 42종의 등급이 순위에서 나오는가
        // ============================================================================

        /// <summary>
        /// 슬롯마다 <c>requiredLevel</c>로 순위를 <b>이 테스트가 직접</b> 매기고, 그 순위를
        /// ECONOMY_SPEC 사다리에 태운 값이 <c>ItemCatalog.Rarity</c>와 같은가.
        /// <para>사다리는 6종 기준이므로 6종이 아닌 슬롯은 여기서 재지 않는다(비율 환산의 단조성은
        /// <see cref="비율_환산이_단조다_코호트가_6종이_아니어도"/>가 따로 잰다). 대신 <b>몇 개를 쟀는지</b>를
        /// 세어 조용히 0건이 되는 초록을 막는다.</para>
        /// </summary>
        [Test]
        public void 등급이_슬롯_안_요구레벨_순위에서_나온다()
        {
            var failures = new List<string>();
            int judged = 0, ladderSlots = 0;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                if (count != EconomySpecLadder.Length) continue;
                ladderSlots++;

                int[] rank = IndependentRanks(slot, count);
                for (int i = 0; i < count; i++)
                {
                    judged++;
                    ItemRarity expected = EconomySpecLadder[rank[i]];
                    ItemRarity actual = ItemCatalog.Rarity(slot, i);
                    if (expected == actual) continue;

                    ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                    failures.Add($"  {slot}#{i} '{e?.Id}' Lv.{e?.RequiredLevel} rank {rank[i]} " +
                                 $"기대 {expected} / 실제 {actual}");
                }
            }

            Assert.AreEqual(ItemCatalog.SlotCount, ladderSlots,
                $"{LogPrefix} 6종 슬롯이 {ladderSlots}개뿐입니다(전체 {ItemCatalog.SlotCount}). " +
                "슬롯 크기가 달라졌다면 ECONOMY_SPEC §3-2의 2/2/1/1 분포부터 다시 정해야 합니다.");
            Assert.AreEqual(ItemCatalog.EquipmentCount, judged,
                $"{LogPrefix} 잰 아이템이 {judged}종인데 장비는 {ItemCatalog.EquipmentCount}종입니다 — 열거가 샙니다.");
            Assert.IsEmpty(failures,
                $"{LogPrefix} 순위에서 나오지 않는 등급이 {failures.Count}건입니다.\n" + string.Join("\n", failures));

            Debug.Log($"{LogPrefix} 슬롯 {ladderSlots}개 / 아이템 {judged}종의 등급이 요구 레벨 순위와 일치.");
        }

        /// <summary>슬롯 안 분포가 ECONOMY_SPEC §3-1의 <b>2 / 2 / 1 / 1</b>인가.
        /// 순위가 맞아도 사다리가 어긋나면(예: 영웅이 둘) 여기서 갈린다.</summary>
        [Test]
        public void 슬롯마다_등급_분포가_2_2_1_1이다()
        {
            var expected = new Dictionary<ItemRarity, int>();
            foreach (ItemRarity r in EconomySpecLadder)
            {
                expected.TryGetValue(r, out int n);
                expected[r] = n + 1;
            }

            var total = new Dictionary<ItemRarity, int>();
            int slots = 0;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                if (count != EconomySpecLadder.Length) continue;
                slots++;

                var got = new Dictionary<ItemRarity, int>();
                for (int i = 0; i < count; i++)
                {
                    ItemRarity r = ItemCatalog.Rarity(slot, i);
                    got.TryGetValue(r, out int n);
                    got[r] = n + 1;
                    total.TryGetValue(r, out int m);
                    total[r] = m + 1;
                }

                foreach (KeyValuePair<ItemRarity, int> kv in expected)
                {
                    got.TryGetValue(kv.Key, out int n);
                    Assert.AreEqual(kv.Value, n,
                        $"{LogPrefix} {slot}의 {kv.Key}가 {n}종입니다(기대 {kv.Value}).");
                }
            }

            Assert.Greater(slots, 0, $"{LogPrefix} 6종 슬롯이 하나도 없습니다 — 아무것도 재지 않았습니다.");
            var summary = new List<string>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                total.TryGetValue(r, out int n);
                summary.Add($"{ItemCatalog.RarityName(r)} {n}");
            }
            Debug.Log($"{LogPrefix} 전체 분포 — " + string.Join(" / ", summary));
        }

        /// <summary>★ 이 규칙이 존재하는 이유 그 자체 — <b>늦게 열리는 물건이 더 낮은 등급이 되지 않는다.</b>
        /// 요구 레벨이 높은 아이템의 등급은 절대 내려가지 않아야 한다. 6종이 아닌 슬롯에서도 성립한다.</summary>
        [Test]
        public void 요구레벨이_높을수록_등급이_내려가지_않는다()
        {
            var failures = new List<string>();
            int pairs = 0;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                for (int a = 0; a < count; a++)
                {
                    ItemCatalogEntry ea = ItemCatalog.Item(slot, a);
                    if (ea == null) continue;
                    for (int b = 0; b < count; b++)
                    {
                        ItemCatalogEntry eb = ItemCatalog.Item(slot, b);
                        if (b == a || eb == null) continue;
                        int la = ea.RequiredLevel ?? 0, lb = eb.RequiredLevel ?? 0;
                        if (la >= lb) continue;

                        pairs++;
                        if ((int)ItemCatalog.Rarity(slot, a) <= (int)ItemCatalog.Rarity(slot, b)) continue;
                        failures.Add($"  {slot}: '{ea.Id}'(Lv.{la} {ItemCatalog.Rarity(slot, a)})가 " +
                                     $"'{eb.Id}'(Lv.{lb} {ItemCatalog.Rarity(slot, b)})보다 높은 등급입니다 — " +
                                     "나중에 열리는 물건이 더 약해집니다.");
                    }
                }
            }

            Assert.Greater(pairs, 0, $"{LogPrefix} 비교한 쌍이 0건입니다 — 요구 레벨이 전부 같습니까?");
            Assert.IsEmpty(failures, $"{LogPrefix} 단조성 위반 {failures.Count}건.\n" + string.Join("\n", failures));
            Debug.Log($"{LogPrefix} 요구 레벨 쌍 {pairs}건 전부에서 등급 단조성 유지.");
        }

        // ============================================================================
        // 2. 사다리 확장 — 슬롯이 6종이 아닐 때 (DLC가 실제로 하게 될 일)
        // ============================================================================

        /// <summary>
        /// ★ 코호트 크기가 6이 아니면 사다리를 <b>비율</b>로 환산한다. 그 환산이 단조이고 끝을 지키는가.
        /// <para>원칙 4대로 DLC는 기본 로직 무수정으로 아이템을 <b>추가</b>한다 — 팩이 자기 코호트로
        /// 6종이 아닌 묶음을 들고 올 수 있다. 잘라내기(rank ≥ 5는 전부 전설)로 두면 8종 슬롯에서 전설이 3개가 된다.</para>
        /// <para><c>RarityOfRank</c>는 <c>internal</c>이라 공개 이음매를 늘리지 않고 여기서 직접 잰다
        /// (InternalsVisibleTo: Scripts/AssemblyInfo.cs).</para>
        /// </summary>
        [Test]
        public void 비율_환산이_단조다_코호트가_6종이_아니어도()
        {
            int ladder = EconomySpecLadder.Length;

            for (int count = 1; count <= 4 * ladder; count++)
            {
                int previous = -1;
                var histogram = new Dictionary<ItemRarity, int>();
                for (int rank = 0; rank < count; rank++)
                {
                    ItemRarity r = ItemCatalog.RarityOfRank(rank, count);
                    Assert.GreaterOrEqual((int)r, previous,
                        $"{LogPrefix} count={count} rank={rank}에서 등급이 내려갔습니다({(ItemRarity)previous} -> {r}).");
                    previous = (int)r;
                    histogram.TryGetValue(r, out int n);
                    histogram[r] = n + 1;
                }

                Assert.AreEqual(ItemRarity.Common, ItemCatalog.RarityOfRank(0, count),
                    $"{LogPrefix} count={count}에서 가장 먼저 열리는 자리가 일반이 아닙니다.");

                if (count < ladder) continue;

                Assert.AreEqual(ItemRarity.Legendary, ItemCatalog.RarityOfRank(count - 1, count),
                    $"{LogPrefix} count={count}에서 가장 늦게 열리는 자리가 전설이 아닙니다.");

                histogram.TryGetValue(ItemRarity.Common, out int common);
                histogram.TryGetValue(ItemRarity.Legendary, out int legendary);
                Assert.GreaterOrEqual(common, legendary,
                    $"{LogPrefix} 코호트 {count}종에서 전설({legendary})이 일반({common})보다 많습니다 — " +
                    "비율이 뒤집혔습니다.");
            }

            // 6종에서는 환산이 항등이어야 한다 — 지금 트리 전체가 이 경로를 탄다.
            for (int rank = 0; rank < ladder; rank++)
            {
                Assert.AreEqual(EconomySpecLadder[rank], ItemCatalog.RarityOfRank(rank, ladder),
                    $"{LogPrefix} 6종 코호트 rank {rank}의 환산이 ECONOMY_SPEC §3-2와 다릅니다.");
            }

            Debug.Log($"{LogPrefix} 코호트 크기 1~{4 * ladder} 전부에서 환산이 단조.");
        }

        /// <summary>범위 밖 입력이 창을 못 열게 만들지 않는다. 보관함 한 칸의 결손이 예외로 번지면
        /// 유저는 창 자체를 못 연다(<c>EnsureLoaded</c>가 구멍을 이미 LogError로 신고한다).</summary>
        [Test]
        public void 범위_밖_자리는_예외_대신_일반이다()
        {
            Assert.AreEqual(ItemRarity.Common, ItemCatalog.Rarity(EquipmentSlot.Head, -1));
            Assert.AreEqual(ItemRarity.Common, ItemCatalog.Rarity(EquipmentSlot.Head, 9999));
            Assert.AreEqual(ItemRarity.Common, ItemCatalog.Rarity((EquipmentSlot)999, 0));
            Assert.AreEqual(ItemRarity.Common, ItemCatalog.RarityOfRank(0, 0));
            Assert.AreEqual("일반", ItemCatalog.RarityName((ItemRarity)999));
        }

        // ============================================================================
        // 2-B. ★★ 코호트 — DLC 팩이 붙어도 기본 42종의 등급이 안 움직이는가
        // ============================================================================
        //
        // 2026-09-02 game-architect 지적 / 리더 재확인. 결함은 <b>모집단</b>이었다.
        // Resources.LoadAll 은 소유 여부를 보지 않고 같은 슬롯 배열에 전부 꽂고(IsPlaceable 은
        // itemId·슬롯 범위·자리 번호만 본다), 등급이 "슬롯에 로드된 개수"로 나눠지고 있었다.
        //
        //   슬롯  6종: 일반 일반 희귀 희귀 영웅 전설
        //   슬롯 12종: 일반 일반 일반 일반 희귀 희귀   ← 여섯 칸 전부 이동
        //   슬롯 18종: 일반 일반 일반 일반 일반 일반   ← 등급이 통째로 죽는다
        //
        // 즉 팩 하나가 출하되는 순간 <b>기본 42종의 등급이 아무도 안 건드렸는데 미끄러지고</b>,
        // ECONOMY_SPEC 의 가격 스윕·유예·페이투윈 검산이 전부 count=6 위에서 계산됐으므로 함께
        // 무너진다. 그리고 "캡 20은 기본 42종만으로 도달"이라는 <b>사용자 확정 차단선</b>이 깨진다.

        /// <summary>
        /// ★ <b>회귀 잠금</b> — 지금 트리는 코호트가 <b>하나</b>이고, 그래서 코호트 == 슬롯이다.
        /// 이 단언이 서 있는 동안 코호트 도입은 값을 한 개도 바꾸지 않는다(회귀 위험 0의 근거).
        /// <para>팩이 실제로 들어오면 이 검사는 <b>바뀌어야 한다</b> — 그때 바꾸라고 여기 있다.</para>
        /// </summary>
        [Test]
        public void 지금은_코호트가_하나다_그래서_코호트가_곧_슬롯이다()
        {
            int checkedItems = 0;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                for (int i = 0; i < count; i++)
                {
                    ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                    if (e == null) continue;
                    checkedItems++;
                    Assert.AreEqual(ItemCatalog.BaseCohortId, e.CohortId,
                        $"{LogPrefix} '{e.Id}'의 코호트가 {e.CohortId}입니다(기본 {ItemCatalog.BaseCohortId}). " +
                        "팩이 실제로 들어왔다면 이 테스트와 아래 42종 값 잠금을 함께 갱신해야 합니다.");
                }
            }
            Assert.AreEqual(ItemCatalog.EquipmentCount, checkedItems,
                $"{LogPrefix} 확인한 아이템이 {checkedItems}종인데 장비는 {ItemCatalog.EquipmentCount}종입니다.");
            Debug.Log($"{LogPrefix} 장비 {checkedItems}종 전부 기본 코호트 — 코호트 == 슬롯.");
        }

        // ----------------------------------------------------------------------------
        // ★★ 2-B-1. 배선 — 에셋의 코호트가 <b>실제로</b> 카탈로그까지 오는가 (2026-09-02)
        // ----------------------------------------------------------------------------
        //
        // 위 두 절(2-B)은 전부 <b>합성 모집단</b>(ItemCatalogEntry.ForEquipment 에 코호트를 직접 넘긴 것)
        // 위에서 돈다. 즉 <c>RarityOfMember</c>의 <b>판정</b>은 잠겨 있었지만,
        // <b>에셋 -> 항목 변환이 그 코호트를 싣는가</b>는 아무 검사도 없었다.
        //
        // 그리고 실제로 안 싣고 있었다: <c>AccessoryDefSO</c>에 코호트 필드가 0건이었고
        // <c>EnsureLoaded</c>가 <c>ForEquipment</c>를 기본 인자로 불러 언제나 BaseCohortId 로 폴백했다.
        // ★ <b>기본 42종이 전부 기본 코호트라 어떤 초록도 갈라지지 않는다</b> —
        //   증상은 팩 에셋을 처음 넣는 날, 팩을 <b>안 산 사람</b>의 등급이 내려가는 형태로 나온다.
        //   그래서 이 검사는 "코호트를 <b>바꾼</b> def"를 변환에 직접 먹인다. 그것만이 갈린다.

        /// <summary>
        /// ★ <b>이 라운드의 빨간불.</b> 코호트를 실은 에셋 하나를 변환에 직접 먹여, 그 번호가
        /// 항목까지 오는지 본다. 배선이 없던 상태에서는 (1)이 <see cref="ItemCatalog.BaseCohortId"/>를
        /// 돌려주며 즉시 빨개진다.
        /// <para>기대값은 <b>프로덕션 상수를 참조해</b> 만든다(<c>BaseCohortId + 1</c>) — 숫자를 베끼면
        /// 상수가 움직였을 때 이 검사도 함께 움직여 아무것도 못 잰다.</para>
        /// </summary>
        [Test]
        public void 에셋의_코호트가_카탈로그_항목까지_실려_온다()
        {
            var def = ScriptableObject.CreateInstance<AccessoryDefSO>();
            try
            {
                def.itemId = "test.cohort.carry";
                def.slot = EquipmentSlot.Head;
                def.itemIndex = 0;
                def.displayName = "합성";
                def.description = "합성";
                def.requiredLevel = 1;

                // (0) 사전 조건 — 손대지 않은 def 는 기본 코호트다. 기본 42종이 정확히 이 경우다
                //     (그 .asset 들에는 cohortId 키 자체가 없다 — 아래 직렬화 기본값 검사 참고).
                Assert.AreEqual(ItemCatalog.BaseCohortId, def.cohortId,
                    $"{LogPrefix} 새 AccessoryDefSO 의 cohortId 가 {def.cohortId}입니다. 기본값이 " +
                    $"{ItemCatalog.BaseCohortId}(BaseCohortId)가 아니면 기본 42종이 남의 모집단으로 갑니다.");
                Assert.AreEqual(ItemCatalog.BaseCohortId, ItemCatalog.EntryFrom(def).CohortId,
                    $"{LogPrefix} 코호트를 안 적은 에셋이 기본 코호트로 실리지 않았습니다.");

                // (1) 본안 — 팩 번호를 실으면 그 번호가 <b>그대로</b> 나와야 한다.
                int packCohort = ItemCatalog.BaseCohortId + 1;
                def.cohortId = packCohort;
                ItemCatalogEntry entry = ItemCatalog.EntryFrom(def);
                Assert.AreEqual(packCohort, entry.CohortId,
                    $"{LogPrefix} ★ 에셋의 코호트 {packCohort}이 항목에는 {entry.CohortId}로 실렸습니다. " +
                    "변환이 코호트를 버리고 있다는 뜻이고, 그러면 팩 에셋에 코호트를 <b>제대로 적어도</b> " +
                    "기본 42종의 등급이 미끄러집니다(등급 판정 자체는 멀쩡한데 값이 안 도달합니다).");

                // (2) 나머지 필드가 함께 실리는지도 본다 — 코호트만 보면 변환이 반쪽만
                //     돌고 있어도 초록이 된다.
                Assert.AreEqual(def.itemId, entry.Id, $"{LogPrefix} 아이디가 안 실렸습니다.");
                Assert.AreEqual(def.slot, entry.Slot, $"{LogPrefix} 카테고리가 안 실렸습니다.");
                Assert.AreEqual(def.requiredLevel, entry.RequiredLevel, $"{LogPrefix} 요구 레벨이 안 실렸습니다.");
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        /// <summary>
        /// ★ <b>코호트가 다르면 갈리고 같으면 안 갈린다</b>를 <b>에셋 쪽 값으로</b> 확인한다.
        /// 위 (2-B) 대조는 코호트를 손으로 넘겨 만든 항목이었고, 여기서는 <b>에셋 필드</b>가
        /// 그 갈림을 만든다 — 배선이 죽어 있으면 두 모집단이 같아져 (2)의 대조가 무너진다.
        /// </summary>
        [Test]
        public void 에셋_코호트가_다르면_등급이_갈리고_같으면_안_갈린다()
        {
            int[] baseLevels = BaseSlotLevels(out EquipmentSlot sampled);
            int[] packLevels = { 40, 43, 46, 49, 52, 55 };   // '위로 쌓기' — 실측으로 확인된 붕괴 모양

            ItemRarity[] alone = new ItemRarity[baseLevels.Length];
            for (int i = 0; i < baseLevels.Length; i++) alone[i] = ItemCatalog.Rarity(sampled, i);

            // (1) 팩이 <b>다른 코호트</b>를 적었다 -> 기본 등급은 한 칸도 안 움직인다.
            ItemCatalogEntry[] separated = PopulationFromDefs(sampled, baseLevels, packLevels,
                ItemCatalog.BaseCohortId + 1);
            var moved = new List<string>();
            for (int i = 0; i < baseLevels.Length; i++)
            {
                ItemRarity now = ItemCatalog.RarityOfMember(separated, i);
                if (now != alone[i]) moved.Add($"  {i}번(Lv.{baseLevels[i]}) {alone[i]} -> {now}");
            }
            Assert.IsEmpty(moved,
                $"{LogPrefix} ★ 팩이 코호트를 제대로 적었는데도 기본 등급이 {moved.Count}칸 움직였습니다 " +
                "— 에셋의 코호트가 항목까지 오지 않았다는 뜻입니다.\n" + string.Join("\n", moved));

            // (2) ★ 대조의 나머지 절반 — 팩이 코호트를 <b>안 적었다면</b>(= 기본값 0) 움직여야 한다.
            //     여기가 초록이면 위 (1)의 '0칸'은 아무것도 재지 않은 것이다.
            ItemCatalogEntry[] collapsed = PopulationFromDefs(sampled, baseLevels, packLevels,
                ItemCatalog.BaseCohortId);
            var shifted = new List<string>();
            for (int i = 0; i < baseLevels.Length; i++)
            {
                ItemRarity now = ItemCatalog.RarityOfMember(collapsed, i);
                if (now != alone[i]) shifted.Add($"{i}번 {alone[i]}->{now}");
            }
            Assert.IsNotEmpty(shifted,
                $"{LogPrefix} ★대조 실패 — 팩을 기본 코호트에 몰아넣었는데 등급이 하나도 안 움직였습니다. " +
                "위 '0칸 이동' 초록은 무효입니다.");

            Debug.Log($"{LogPrefix} [{sampled}] 에셋 코호트 분리 이동 0칸 / 같은 코호트 {shifted.Count}칸 " +
                      $"({string.Join(" ", shifted)}) — 배선 대조 성립.");
        }

        /// <summary>
        /// ★★ <b>기본 42종이 오늘과 같은 등급으로 남는 근거는 「직렬화 기본값 == 기본 코호트」 하나뿐이다.</b>
        ///
        /// <para>Unity는 <c>.asset</c>에 키가 없으면 그 필드를 <b>C# 기본값 그대로</b> 둔다. 기본 42종의
        /// <c>.asset</c>에는 <c>cohortId</c> 키가 <b>한 파일도 없고</b>(이 라운드에서 한 파일도 고치지
        /// 않았다), 그래서 전부 <c>default(int)</c>로 실린다. 그것이 곧 기본 코호트여야 한다.</para>
        ///
        /// <para>★ <see cref="ItemCatalog.BaseCohortId"/>를 0이 아닌 값으로 바꾸는 순간, 42개 에셋은
        /// <b>파일이 한 바이트도 안 바뀌었는데</b> 조용히 남의 모집단으로 넘어간다. 그 등급 붕괴는
        /// 팩이 없어도 일어난다(코호트 크기가 0이 되는 쪽이라 더 이상하게 깨진다).
        /// 기대값을 프로덕션 상수에서 만들지 않는 이유가 여기다 — 이 검사가 지키는 것은
        /// <b>상수의 값</b>이 아니라 <b>두 기본값이 같다는 등식</b>이다.</para>
        /// </summary>
        [Test]
        public void 코호트를_안_적은_에셋의_기본값이_기본_코호트와_같다_42종의_유일한_근거()
        {
            Assert.AreEqual(default(int), ItemCatalog.BaseCohortId,
                $"{LogPrefix} ★ BaseCohortId 가 {ItemCatalog.BaseCohortId}인데 직렬화 기본값은 " +
                $"{default(int)}입니다. Resources/Items 의 42개 .asset 에는 cohortId 키가 없어서 " +
                "전부 직렬화 기본값으로 실립니다 — 두 값이 갈라지면 기본 42종이 파일 수정 없이 " +
                "남의 모집단으로 넘어가고 등급이 통째로 무너집니다. " +
                "BaseCohortId 를 바꾸려면 42개 .asset 에 cohortId 를 명시적으로 적어야 합니다.");

            // 양성 대조 — 스캐너가 실제로 파일을 읽고 키를 찾을 수 있는가.
            //   (읽지 못하면 아래 '0건'은 '깨끗함'이 아니라 '못 봄'이다 — docs/TEAM.md 4절 사고 #4)
            string dir = Path.Combine(Application.dataPath, "_Project", "Resources", "Items");
            Assert.IsTrue(Directory.Exists(dir), $"{LogPrefix} {dir}를 찾지 못했습니다.");
            string[] files = Directory.GetFiles(dir, "*.asset");
            Assert.IsNotEmpty(files, $"{LogPrefix} 아이템 에셋을 하나도 못 읽었습니다 — 스캐너 고장.");

            int sawRequiredLevel = 0;
            var declared = new List<string>();
            foreach (string f in files)
            {
                foreach (string line in File.ReadAllLines(f))
                {
                    string t = line.Trim();
                    if (t.StartsWith("requiredLevel:", System.StringComparison.Ordinal)) sawRequiredLevel++;
                    if (!t.StartsWith("cohortId:", System.StringComparison.Ordinal)) continue;

                    string v = t.Substring("cohortId:".Length).Trim();
                    if (!int.TryParse(v, out int parsed) || parsed != ItemCatalog.BaseCohortId)
                    {
                        declared.Add($"  {Path.GetFileName(f)} -> cohortId: {v}");
                    }
                }
            }
            Assert.AreEqual(files.Length, sawRequiredLevel,
                $"{LogPrefix} 양성 대조 실패 — 에셋 {files.Length}개 중 requiredLevel 을 {sawRequiredLevel}개에서만 " +
                "찾았습니다. 스캐너가 파일을 제대로 못 읽고 있으므로 아래 '0건'도 무효입니다.");
            Assert.IsEmpty(declared,
                $"{LogPrefix} Resources/Items 의 기본 아이템이 기본 코호트가 아닌 값을 적었습니다. " +
                "이 폴더는 기본 42종의 자리이고, 팩은 자기 코호트를 써야 합니다.\n" + string.Join("\n", declared));

            Debug.Log($"{LogPrefix} 에셋 {files.Length}개 스캔 — cohortId 명시 위반 0건 " +
                      $"(양성 대조: requiredLevel {sawRequiredLevel}건 검출). " +
                      $"직렬화 기본값 {default(int)} == BaseCohortId {ItemCatalog.BaseCohortId}.");
        }

        /// <summary>표본 슬롯의 기본 6종 + 팩 6종을 <b>실제 에셋 변환 경로</b>(<c>ItemCatalog.EntryFrom</c>)로
        /// 만든 모집단. 기본은 앞쪽(0..5), 팩은 뒤쪽(6..11)이다.
        /// <para>합성 항목을 직접 만들지 <b>않는</b> 것이 요점이다 — 이 하니스가 재려는 것이
        /// "판정"이 아니라 "에셋 -> 항목 배선"이기 때문이다.</para></summary>
        private static ItemCatalogEntry[] PopulationFromDefs(EquipmentSlot slot, int[] baseLevels,
            int[] packLevels, int packCohort)
        {
            var list = new List<ItemCatalogEntry>();
            var made = new List<AccessoryDefSO>();
            try
            {
                for (int i = 0; i < baseLevels.Length; i++)
                {
                    // 기본 아이템: 코호트를 <b>적지 않는다</b>(42종 .asset 과 같은 상태).
                    list.Add(ItemCatalog.EntryFrom(MakeDef(made, $"base.{i}", slot, list.Count, baseLevels[i], null)));
                }
                for (int i = 0; i < packLevels.Length; i++)
                {
                    list.Add(ItemCatalog.EntryFrom(MakeDef(made, $"pack.{i}", slot, list.Count, packLevels[i], packCohort)));
                }
                return list.ToArray();
            }
            finally
            {
                foreach (AccessoryDefSO d in made) Object.DestroyImmediate(d);
            }
        }

        private static AccessoryDefSO MakeDef(List<AccessoryDefSO> sink, string id, EquipmentSlot slot,
            int itemIndex, int requiredLevel, int? cohortId)
        {
            var def = ScriptableObject.CreateInstance<AccessoryDefSO>();
            sink.Add(def);
            def.itemId = id;
            def.slot = slot;
            def.itemIndex = itemIndex;
            def.displayName = id;
            def.description = "합성";
            def.requiredLevel = requiredLevel;
            if (cohortId.HasValue) def.cohortId = cohortId.Value;
            return def;
        }

        /// <summary>
        /// ★★ <b>이 라운드의 빨간불</b>. 같은 판정 함수(<c>ItemCatalog.RarityOfMember</c>)에
        /// <b>코호트만 다른</b> 두 모집단을 넣는다 — 분리하면 안 움직이고, 합치면 움직여야 한다.
        ///
        /// <para>★ <b>팩의 「모양」을 세 가지로 잰다. 이게 이 검사의 핵심이다.</b>
        /// 처음 짰을 때 팩 레벨을 기본 사이사이에 흩어 놓았는데, 그러면 rank와 count가 <b>같이 2배</b>가 되어
        /// <c>rank × 6 / count</c>가 <b>보존된다</b> — 합쳐도 등급이 하나도 안 움직여서
        /// <b>대조가 공허하게 초록</b>이었다. 이 저장소가 반복해서 당한 그 모양이라 실측으로 확인하고 고쳤다:</para>
        /// <code>
        ///   기준(6종)              일반 일반 희귀 희귀 영웅 전설
        ///   사이사이 + 합침(12종)  일반 일반 희귀 희귀 영웅 전설   ← 안 움직인다. 대조로 못 쓴다
        ///   위로 쌓기 + 합침(12종) 일반 일반 일반 일반 희귀 희귀   ← 리더 실측 그대로
        ///   아래 깔기 + 합침(12종) 희귀 희귀 영웅 영웅 전설 전설   ← 반대로 <b>부풀어 오른다</b>
        /// </code>
        /// <para><b>아래 깔기</b>(예: Lv.1 스타터 팩)가 더 나쁘다 — 기본 아이템의 스탯을 <b>올려서</b>
        /// 나눠 주므로 페이투윈 차단선을 정면으로 뚫는다.</para>
        /// </summary>
        [Test]
        public void 팩이_붙어도_기본_아이템_등급이_안_움직인다_위로_쌓기()
            => AssertPackDoesNotMoveBaseRarity(new[] { 40, 43, 46, 49, 52, 55 }, "위로 쌓기(고레벨 팩)");

        /// <summary>★ 이쪽이 더 나쁘다 — 기본 아이템의 등급을 <b>올려서</b> 나눠 주므로
        /// 페이투윈 차단선을 정면으로 뚫는다(Lv.1 스타터 팩 한 개면 전설이 두 개가 된다).</summary>
        [Test]
        public void 팩이_붙어도_기본_아이템_등급이_안_움직인다_아래_깔기()
            => AssertPackDoesNotMoveBaseRarity(new[] { 0, 0, 0, 0, 0, 0 }, "아래 깔기(Lv.1 스타터 팩)");

        private static void AssertPackDoesNotMoveBaseRarity(int[] packLevels, string shape)
        {
            int[] baseLevels = BaseSlotLevels(out EquipmentSlot sampled);
            Assert.AreEqual(EconomySpecLadder.Length, baseLevels.Length,
                $"{LogPrefix} 표본 슬롯이 6종이 아닙니다 — 대조 전제가 깨졌습니다.");

            // (0) 기준 — 팩이 없을 때의 등급. 실제 카탈로그와 같은지부터 확인한다
            //     (합성 모집단이 엉뚱하면 그 뒤 대조가 전부 무의미하다).
            ItemCatalogEntry[] alone = BuildPopulation(baseLevels, null,
                ItemCatalog.BaseCohortId, ItemCatalog.BaseCohortId + 1);
            var expected = new ItemRarity[baseLevels.Length];
            for (int i = 0; i < baseLevels.Length; i++)
            {
                expected[i] = ItemCatalog.RarityOfMember(alone, i);
                Assert.AreEqual(ItemCatalog.Rarity(sampled, i), expected[i],
                    $"{LogPrefix} 합성 모집단이 실제 {sampled} 슬롯과 다릅니다({i}번). 대조가 성립하지 않습니다.");
            }

            // (1) 본안 — 팩을 <b>다른 코호트</b>로 끼워 넣는다. 기본 등급은 그대로여야 한다.
            ItemCatalogEntry[] separated = BuildPopulation(baseLevels, packLevels,
                ItemCatalog.BaseCohortId, ItemCatalog.BaseCohortId + 1);

            var moved = new List<string>();
            for (int i = 0; i < baseLevels.Length; i++)
            {
                ItemRarity now = ItemCatalog.RarityOfMember(separated, i * 2);   // 기본은 짝수 자리
                if (now == expected[i]) continue;
                moved.Add($"  기본 {i}번(Lv.{baseLevels[i]}) {expected[i]} -> {now}");
            }
            Assert.IsEmpty(moved,
                $"{LogPrefix} ★ DLC 팩({shape})이 붙자 기본 아이템 등급이 {moved.Count}칸 움직였습니다. " +
                "등급이 슬롯 개수에 의존한다는 뜻이고, 그러면 ECONOMY_SPEC의 가격·유예·페이투윈 검산이 " +
                "(전부 count=6 위에서 계산됐으므로) 함께 무너지고 '캡 20은 기본 42종만으로 도달'이 깨집니다.\n" +
                string.Join("\n", moved));

            // (2) ★ 대조의 나머지 절반 — 같은 12종을 <b>한 코호트</b>로 두면 실제로 움직이는가.
            ItemCatalogEntry[] collapsed = BuildPopulation(baseLevels, packLevels,
                ItemCatalog.BaseCohortId, ItemCatalog.BaseCohortId);

            var shifted = new List<string>();
            for (int i = 0; i < baseLevels.Length; i++)
            {
                ItemRarity now = ItemCatalog.RarityOfMember(collapsed, i * 2);
                if (now == expected[i]) continue;
                shifted.Add($"{i}번 {expected[i]}->{now}");
            }
            Assert.IsNotEmpty(shifted,
                $"{LogPrefix} ★대조 실패 — 12종({shape})을 한 코호트에 몰아넣었는데 등급이 하나도 " +
                "안 움직였습니다. 판정이 모집단 크기를 아예 안 보고 있거나, 이 팩 모양이 " +
                "rank와 count를 같은 비율로 늘려 대조가 공허해진 경우입니다(요약의 '사이사이' 참조). " +
                "어느 쪽이든 위 '0칸 이동' 초록은 무효입니다.");

            Debug.Log($"{LogPrefix} [{shape}] 코호트 분리 이동 0칸 / 합쳤을 때 {shifted.Count}칸 " +
                      $"({string.Join(" ", shifted)}) — 대조 성립.");
        }

        /// <summary>★ 위 대조가 <b>공허해지는 팩 모양</b>을 박제한다. 팩 레벨이 기본 사이사이에 고르게
        /// 놓이면 rank와 count가 같은 비율로 늘어 <c>rank × 6 / count</c>가 보존된다 —
        /// <b>합쳐도 등급이 안 움직인다.</b> 그래서 이 모양으로는 대조를 세울 수 없다.
        /// <para>이 사실이 바뀌면(사다리 산식이 바뀌면) 위 대조의 모양 선택 근거도 다시 봐야 한다.</para></summary>
        [Test]
        public void 대조가_공허해지는_팩_모양이_실재한다_사이사이는_대조로_쓸_수_없다()
        {
            int[] baseLevels = BaseSlotLevels(out _);
            int[] interleaved = { 2, 7, 14, 18, 24, 29 };

            ItemCatalogEntry[] alone = BuildPopulation(baseLevels, null,
                ItemCatalog.BaseCohortId, ItemCatalog.BaseCohortId + 1);
            ItemCatalogEntry[] collapsed = BuildPopulation(baseLevels, interleaved,
                ItemCatalog.BaseCohortId, ItemCatalog.BaseCohortId);

            for (int i = 0; i < baseLevels.Length; i++)
            {
                Assert.AreEqual(ItemCatalog.RarityOfMember(alone, i),
                    ItemCatalog.RarityOfMember(collapsed, i * 2),
                    $"{LogPrefix} '사이사이' 모양에서 등급이 움직였습니다({i}번). 그렇다면 이 모양도 " +
                    "대조로 쓸 수 있으니 위 테스트의 모양 선택 근거를 다시 쓰십시오 — " +
                    "지금 이 단언은 '이 모양은 대조로 못 쓴다'는 사실을 지키고 있습니다.");
            }
            Debug.Log($"{LogPrefix} 확인 — '사이사이' 팩은 합쳐도 등급이 안 움직인다(대조로 쓸 수 없다).");
        }

        /// <summary>표본 슬롯의 요구 레벨을 자리 번호 순으로. 실제 카탈로그에서 읽는다.</summary>
        private static int[] BaseSlotLevels(out EquipmentSlot sampled)
        {
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                if (count != EconomySpecLadder.Length) continue;

                var levels = new int[count];
                bool complete = true;
                for (int i = 0; i < count; i++)
                {
                    ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                    if (e == null) { complete = false; break; }
                    levels[i] = e.RequiredLevel ?? 0;
                }
                if (!complete) continue;

                sampled = slot;
                return levels;
            }

            Assert.Fail($"{LogPrefix} 6종짜리 온전한 슬롯을 하나도 찾지 못했습니다 — 대조를 세울 수 없습니다.");
            sampled = default;
            return System.Array.Empty<int>();
        }

        /// <summary>기본 아이템을 <b>짝수 자리</b>에, 팩 아이템을 <b>홀수 자리</b>에 끼워 넣은 모집단.
        /// 뒤에 몰아 넣으면 자리 번호 순서만으로 우연히 맞을 수 있다.</summary>
        /// <param name="packDeclared">팩이 등급을 <b>선언</b>했는가. 기본값
        /// <see cref="DeclaredRarity.Derived"/>는 "팩이 선언을 빠뜨린 상태"이고, 위 파생 검사들이
        /// 재려는 것이 정확히 그 경우다(선언이 있으면 파생 경로를 아예 안 타므로 코호트 대조가
        /// 공허해진다). 선언까지 함께 재는 검사는 이 인자를 명시한다.</param>
        private static ItemCatalogEntry[] BuildPopulation(int[] baseLevels, int[] packLevels,
            int baseCohort, int packCohort,
            DeclaredRarity baseDeclared = DeclaredRarity.Derived,
            DeclaredRarity packDeclared = DeclaredRarity.Derived)
        {
            var list = new List<ItemCatalogEntry>();
            for (int i = 0; i < baseLevels.Length; i++)
            {
                list.Add(ItemCatalogEntry.ForEquipment($"base.{i}", EquipmentSlot.Head, list.Count,
                    $"기본{i}", "합성", baseLevels[i], null, baseCohort, baseDeclared));
                if (packLevels == null || i >= packLevels.Length) continue;
                list.Add(ItemCatalogEntry.ForEquipment($"pack.{i}", EquipmentSlot.Head, list.Count,
                    $"팩{i}", "합성", packLevels[i], null, packCohort, packDeclared));
            }
            return list.ToArray();
        }

        // ============================================================================
        // 3. ★ 양성 대조 — 이 검사들이 실제로 물는가
        // ============================================================================

        /// <summary>
        /// 본안이 쓰는 <b>같은 비교</b>에 일부러 틀린 사다리를 물려 빨간불이 나는지 본다.
        /// 대조용 판정기를 따로 짜면 그건 대조가 아니다(TEAM.md §4).
        /// </summary>
        [Test]
        public void 양성_대조_사다리가_어긋나면_본안_비교가_잡는다()
        {
            // 실제로 있었을 법한 어긋남 셋. 전부 "순위는 맞는데 사다리가 다른" 형태다.
            var wrongLadders = new List<(string Why, ItemRarity[] Ladder)>
            {
                ("한 칸 밀림(0,1=일반이 아니라 0만 일반)", new[]
                {
                    ItemRarity.Common, ItemRarity.Rare, ItemRarity.Rare,
                    ItemRarity.Epic, ItemRarity.Epic, ItemRarity.Legendary,
                }),
                ("잘라내기(rank>=4를 전부 전설로)", new[]
                {
                    ItemRarity.Common, ItemRarity.Common, ItemRarity.Rare, ItemRarity.Rare,
                    ItemRarity.Legendary, ItemRarity.Legendary,
                }),
                ("전부 일반(등급이 죽은 상태)", new[]
                {
                    ItemRarity.Common, ItemRarity.Common, ItemRarity.Common,
                    ItemRarity.Common, ItemRarity.Common, ItemRarity.Common,
                }),
            };

            foreach ((string why, ItemRarity[] ladder) in wrongLadders)
            {
                List<string> failures = CompareAgainstLadder(ladder);
                Assert.IsNotEmpty(failures,
                    $"{LogPrefix} ★대조 실패 — 틀린 사다리({why})를 본안 비교가 놓쳤습니다. " +
                    "이 파일의 모든 초록을 폐기하십시오.");
                Debug.Log($"{LogPrefix} 대조 통과 — {why}: {failures.Count}건 적발, 첫 줄{failures[0]}");
            }

            // 그리고 옳은 사다리에서는 0건이어야 한다(대조가 무엇이든 잡는 상태가 아님을 보인다).
            Assert.IsEmpty(CompareAgainstLadder(EconomySpecLadder),
                $"{LogPrefix} ★대조 실패 — 옳은 사다리에서도 위반이 나옵니다. 비교기가 고장났습니다.");
        }

        /// <summary>★ 순위를 이 테스트가 <b>애셋에서 직접</b> 계산하는지 확인하는 대조.
        /// 순위 계산이 그냥 <c>itemIndex</c>를 돌려주는 상태라면 요구 레벨을 뒤섞어도 순위가 안 바뀐다.</summary>
        [Test]
        public void 양성_대조_순위_계산기가_요구레벨을_실제로_본다()
        {
            int[] levels = { 30, 1, 22, 5, 28, 12 };
            int[] rank = RanksOf(levels);
            CollectionAssert.AreEqual(new[] { 5, 0, 3, 1, 4, 2 }, rank,
                $"{LogPrefix} ★대조 실패 — 순위 계산기가 요구 레벨을 보지 않습니다(자리 번호를 그대로 " +
                "돌려주고 있을 수 있습니다). 본안의 '순위 일치' 초록이 전부 무의미해집니다.");

            // 동점은 자리 번호가 가른다 — 한 자리에 두 등급이 오는 일이 없어야 한다.
            int[] tied = RanksOf(new[] { 7, 7, 7 });
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, tied,
                $"{LogPrefix} ★대조 실패 — 동점에서 순위가 하나로 정해지지 않습니다.");
        }

        // ============================================================================
        // 4. ★ 소스 확인 — 행동으로 구분되지 않는 가설을 여기서 가른다
        // ============================================================================

        /// <summary>
        /// 출하된 42종은 rank == itemIndex라 "요구 레벨 순위"와 "자리 번호"가 <b>행동으로 갈리지 않는다</b>.
        /// 그래서 파생이 실제로 <c>RequiredLevel</c>을 읽는지 소스에서 확인한다.
        /// <para>이 검사를 지우려면 먼저 요구 레벨과 자리 번호가 어긋나는 아이템이 트리에 있어야 한다 —
        /// 그때는 행동 검사만으로 충분해진다.</para>
        /// </summary>
        [Test]
        public void 파생이_실제로_RequiredLevel을_읽는다_소스_확인()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Core", "ItemCatalog.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} {path}를 찾지 못했습니다.");
            string[] lines = File.ReadAllLines(path);

            int hit = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*")) continue;
                if (t.IndexOf("UnlockRankKey(ItemCatalogEntry", System.StringComparison.Ordinal) < 0) continue;
                hit = i;
                break;
            }
            Assert.GreaterOrEqual(hit, 0,
                $"{LogPrefix} ItemCatalog.cs에서 순위 키 함수(UnlockRankKey)를 찾지 못했습니다 — " +
                "이름이 바뀌었다면 이 검사도 함께 옮기십시오.");

            string body = string.Join("\n", lines, hit, Mathf.Min(4, lines.Length - hit));
            Assert.IsTrue(body.Contains("RequiredLevel"),
                $"{LogPrefix} 순위 키가 RequiredLevel을 읽지 않습니다. 지금 트리에서는 rank == itemIndex라 " +
                "행동 검사가 이 차이를 못 봅니다 — ECONOMY_SPEC §3-2의 '요구 레벨 순위' 계약이 " +
                $"조용히 깨진 상태일 수 있습니다.\n{body}");

            // 그리고 rank == itemIndex라는 사실 자체를 기록한다(왜 이 소스 검사가 필요한지의 근거).
            int aligned = 0, misaligned = 0;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                int[] rank = IndependentRanks(slot, count);
                for (int i = 0; i < count; i++)
                {
                    if (rank[i] == i) aligned++; else misaligned++;
                }
            }
            Debug.Log($"{LogPrefix} rank == itemIndex {aligned}종 / 어긋남 {misaligned}종. " +
                      (misaligned == 0
                          ? "두 가설이 행동으로 갈리지 않으므로 위 소스 검사가 유일한 근거다."
                          : "이제 행동으로도 갈린다 — 소스 검사는 보조가 됐다."));
        }

        // ============================================================================
        // 4. ★★ 등급 「선언」 (DS-1) — 0은 반드시 「선언 없음」이어야 한다
        // ============================================================================
        //
        // 이 절이 지키는 것은 한 문장이다:
        //   <b>Unity 는 .asset 에 키가 없으면 그 필드를 C# 기본값 그대로 둔다.</b>
        // 그래서 선언 필드의 0이 무슨 뜻인지가 곧 <b>기본 42종 전체의 등급</b>이다.
        //
        // ★ 이 라운드에 실제로 잰 값 (Tools/ShapeDump 오프라인 하니스, 프로덕션 직렬화 경로 그대로):
        //   선언 필드를 <c>public ItemRarity declaredRarity;</c> 로 두고 42개 에셋을 읽으면
        //   <b>42/42 가 Common 으로 실리고</b>, 「선언이 이긴다」를 적용하면 <b>28/42 의 등급이
        //   내려앉는다</b>(희귀 12 · 영웅 6 · 전설 7 → 전부 일반). 파일은 한 바이트도 안 바뀐다.
        //   그 실측이 아래 검사들의 존재 이유다.

        /// <summary>
        /// ★★ <b>왜 <see cref="ItemRarity"/>를 선언 필드의 타입으로 쓸 수 없는가</b>를 값으로 증명한다.
        ///
        /// <para>이유는 "0이 일반이라서"가 아니라 <b>「말 안 함」을 담을 칸이 없어서</b>다.
        /// <see cref="ItemRarity"/>의 모든 값은 <b>실재하는 등급</b>이고, 하필 기본값
        /// (<c>default</c>)인 <see cref="ItemRarity.Common"/>은 출하 42종 중 <b>여럿이 실제로
        /// 갖고 있는 값</b>이다. 그래서 "안 적었다"와 "일반이라고 적었다"가 값으로 구분되지 않는다.
        /// 아래는 그 충돌을 <b>카탈로그 실물에서</b> 센다 — 0건이면 이 논증 자체가 무너지므로
        /// 개수를 확인하고 넘어간다.</para>
        ///
        /// <para><see cref="DeclaredRarity"/>는 그 칸을 <see cref="DeclaredRarity.Derived"/>로 만들었고,
        /// <b>그것이 0이어야</b> 키 없는 42종이 「선언 없음」으로 실린다.</para>
        /// </summary>
        [Test]
        public void 선언없음이_0이고_ItemRarity로는_그걸_표현할_수_없다()
        {
            // (가) 선언 열거형: 0 == 선언 없음. 이 등식이 42종의 유일한 근거다.
            Assert.AreEqual(DeclaredRarity.Derived, default(DeclaredRarity),
                $"{LogPrefix} ★ default(DeclaredRarity)가 {default(DeclaredRarity)}입니다. " +
                "Resources/Items 의 42개 .asset 에는 declaredRarity 키가 없어서 전부 직렬화 기본값으로 " +
                "실립니다 — 0이 Derived 가 아니게 되는 순간 42종이 파일 수정 없이 '무언가로 선언됨'이 " +
                "되고 등급이 통째로 무너집니다.");
            Assert.AreEqual(0, (int)DeclaredRarity.Derived,
                $"{LogPrefix} DeclaredRarity.Derived 가 0이 아닙니다({(int)DeclaredRarity.Derived}).");
            Assert.IsFalse(DeclaredRarityRules.IsDeclared(default(DeclaredRarity)),
                $"{LogPrefix} 직렬화 기본값이 '선언됨'으로 판정됩니다 — 키를 안 적은 42종이 전부 선언한 " +
                "것으로 취급됩니다.");

            // (나) ItemRarity 로는 왜 안 되는가 — 기본값이 <b>실재하는 등급</b>이고,
            //     출하 카탈로그가 그 값을 실제로 쓰고 있다.
            Assert.AreEqual(ItemRarity.Common, default(ItemRarity),
                $"{LogPrefix} default(ItemRarity)가 바뀌었습니다 — 이 파일의 논증 전체를 다시 보십시오.");

            int collide = 0, total = 0;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                for (int i = 0; i < count; i++)
                {
                    total++;
                    if (ItemCatalog.Rarity(slot, i) == default(ItemRarity)) collide++;
                }
            }
            Assert.Greater(collide, 0,
                $"{LogPrefix} ★대조 실패 — default(ItemRarity)와 같은 등급을 가진 출하 아이템이 0종입니다. " +
                "그렇다면 '기본값이 실재 등급과 충돌한다'는 이 검사의 전제가 성립하지 않으므로 " +
                "선언 타입 결정 근거를 다시 세워야 합니다.");
            Assert.Greater(total, 0, $"{LogPrefix} 카탈로그를 하나도 못 셌습니다 — 스캐너 고장.");

            // (다) 그리고 DeclaredRarity 에는 그 충돌이 없다 — Derived 는 어떤 등급으로도 안 풀린다.
            foreach (DeclaredRarity d in System.Enum.GetValues(typeof(DeclaredRarity)))
            {
                bool resolved = DeclaredRarityRules.TryResolve(d, out ItemRarity r);
                Assert.AreEqual(d != DeclaredRarity.Derived, resolved,
                    $"{LogPrefix} DeclaredRarity.{d}의 '선언됨' 판정이 Derived 여부와 어긋납니다(resolved={resolved}, r={r}).");
            }

            Debug.Log($"{LogPrefix} default(DeclaredRarity)={default(DeclaredRarity)} / " +
                      $"default(ItemRarity)={default(ItemRarity)}이고 출하 {total}종 중 {collide}종이 그 등급을 " +
                      "실제로 씁니다 — ItemRarity 로는 '안 적음'을 표현할 수 없습니다.");
        }

        /// <summary>
        /// ★ <b>선언 필드의 타입</b>을 소스에서 확인한다. 위 검사는 <c>DeclaredRarity</c>의 성질을
        /// 재지만, 누군가 <c>AccessoryDefSO</c>의 필드 타입만 <c>ItemRarity</c>로 되돌리면
        /// 위 검사는 <b>여전히 전부 초록</b>이다 — 재는 대상이 다르기 때문이다.
        /// 이 검사가 그 구멍을 막는다.
        /// </summary>
        [Test]
        public void 선언_필드의_타입이_ItemRarity가_아니다_소스_확인()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Core", "AccessoryDefSO.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} {path}를 찾지 못했습니다.");

            string declLine = null, cohortLine = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string t = raw.Trim();
                if (t.StartsWith("//", System.StringComparison.Ordinal) ||
                    t.StartsWith("///", System.StringComparison.Ordinal) ||
                    t.StartsWith("*", System.StringComparison.Ordinal)) continue;
                if (!t.StartsWith("public ", System.StringComparison.Ordinal)) continue;

                if (t.Contains(" declaredRarity")) declLine = t;
                if (t.Contains(" cohortId")) cohortLine = t;
            }

            // 양성 대조 — 스캐너가 실제로 필드 선언 줄을 집어내는가.
            //   (못 집으면 아래 '타입이 맞다'는 '못 봤다'와 똑같이 생긴다.)
            Assert.IsNotNull(cohortLine,
                $"{LogPrefix} ★대조 실패 — AccessoryDefSO.cs 에서 cohortId 필드 선언을 못 찾았습니다. " +
                "스캐너가 눈이 먼 상태이므로 아래 판정도 무효입니다.");
            Assert.IsNotNull(declLine,
                $"{LogPrefix} AccessoryDefSO.cs 에서 declaredRarity 필드 선언을 못 찾았습니다.");

            Assert.IsTrue(declLine.StartsWith("public DeclaredRarity declaredRarity", System.StringComparison.Ordinal),
                $"{LogPrefix} ★ 선언 필드의 타입이 DeclaredRarity 가 아닙니다:\n  {declLine}\n" +
                "ItemRarity 로 두면 키 없는 42종이 전부 '일반으로 선언됨'이 되고 28/42 의 등급이 " +
                "내려앉습니다(오프라인 하니스 실측). DeclaredRarity 문단을 먼저 읽으십시오.");

            Debug.Log($"{LogPrefix} 선언 필드 = '{declLine}' (양성 대조: cohortId 줄도 검출).");
        }

        /// <summary>
        /// ★ C-5 ① — <b>키 없는 에셋은 파생이다.</b> 출하 42종에 <c>declaredRarity</c> 키가
        /// <b>한 줄도 없어야</b> 한다. 있으면 그건 기본 아이템이 등급을 선언한 것이고,
        /// 그 순간 "레벨이 오를수록 스탯이 안 내려간다"가 사람 손에 맡겨진다.
        /// </summary>
        [Test]
        public void 기본_42종은_선언_키를_아예_적지_않는다()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Resources", "Items");
            Assert.IsTrue(Directory.Exists(dir), $"{LogPrefix} {dir}를 찾지 못했습니다.");
            string[] files = Directory.GetFiles(dir, "*.asset");
            Assert.IsNotEmpty(files, $"{LogPrefix} 아이템 에셋을 하나도 못 읽었습니다 — 스캐너 고장.");

            int sawRequiredLevel = 0;
            var declared = new List<string>();
            foreach (string f in files)
            {
                foreach (string line in File.ReadAllLines(f))
                {
                    string t = line.Trim();
                    if (t.StartsWith("requiredLevel:", System.StringComparison.Ordinal)) sawRequiredLevel++;
                    if (t.StartsWith("declaredRarity:", System.StringComparison.Ordinal))
                        declared.Add($"  {Path.GetFileName(f)} -> {t}");
                }
            }

            // 양성 대조 — 같은 스캐너가 실제로 키를 찾아낼 수 있음을 먼저 보인다.
            Assert.AreEqual(files.Length, sawRequiredLevel,
                $"{LogPrefix} ★대조 실패 — 에셋 {files.Length}개 중 requiredLevel 을 {sawRequiredLevel}개에서만 " +
                "찾았습니다. 스캐너가 파일을 제대로 못 읽고 있으므로 아래 '0건'도 무효입니다.");
            Assert.IsEmpty(declared,
                $"{LogPrefix} 기본 아이템이 등급을 선언했습니다({declared.Count}건). 기본 42종의 등급은 " +
                "requiredLevel 파생이 유일한 출처입니다.\n" + string.Join("\n", declared));

            Debug.Log($"{LogPrefix} 에셋 {files.Length}개 스캔 — declaredRarity 키 0건 " +
                      $"(양성 대조: requiredLevel {sawRequiredLevel}건 검출).");
        }

        /// <summary>
        /// ★★ <b>C-4 — 선언은 비율 환산을 절대 타지 않는다.</b> 이 라운드에서 가장 중요한 검사다.
        ///
        /// <para>코호트 필터만으로는 부족하다는 것이 요점이다: 팩 코호트에 <b>아이템이 하나뿐</b>이면
        /// <c>rank = 0</c>, <c>count = 1</c>이라 <c>step = 0 × 6 ÷ 1 = 0</c>이 되어
        /// <b>선언이 무엇이든 일반</b>이 나온다. 그래서 아래는 <b>같은 모집단</b>에 선언만 켜고 끈다:</para>
        /// <code>
        ///   팩 1종, 선언 없음  ->  일반   (비율 환산이 뭉갠다 — 이게 「고장」이다)
        ///   팩 1종, 희귀 선언  ->  희귀   (선언이 환산을 건너뛴다 — 이게 「고침」이다)
        /// </code>
        /// <para>아래쪽만 있으면 "원래부터 희귀였을 수도" 있으므로 <b>둘 다</b> 재야 대조가 성립한다.</para>
        /// </summary>
        [Test]
        public void 선언이_있으면_비율_환산을_타지_않는다_코호트_크기_1()
        {
            const int packCohort = ItemCatalog.BaseCohortId + 1;
            int[] baseLevels = { 1, 5, 9, 20, 23, 26 };

            // (0) 선언 없는 팩 1종 — 비율 환산이 무조건 일반으로 뭉갠다.
            var undeclaredPop = new List<ItemCatalogEntry>();
            for (int i = 0; i < baseLevels.Length; i++) undeclaredPop.Add(BaseEntry(i, baseLevels[i]));
            undeclaredPop.Add(PackEntry(baseLevels.Length, packCohort, DeclaredRarity.Derived));

            ItemRarity collapsed = ItemCatalog.RarityOfMember(undeclaredPop.ToArray(), baseLevels.Length);
            Assert.AreEqual(ItemRarity.Common, collapsed,
                $"{LogPrefix} ★대조 실패 — 코호트 크기 1에서 선언 없는 팩이 {collapsed}로 나왔습니다. " +
                "'비율 환산이 크기 1을 일반으로 뭉갠다'는 전제가 깨졌으므로 아래 검사의 의미도 달라집니다.");

            // (1) 같은 자리에 선언만 켠다 — 환산을 건너뛰어야 한다.
            foreach (DeclaredRarity d in System.Enum.GetValues(typeof(DeclaredRarity)))
            {
                if (!DeclaredRarityRules.TryResolve(d, out ItemRarity expected)) continue;

                var pop = new List<ItemCatalogEntry>();
                for (int i = 0; i < baseLevels.Length; i++) pop.Add(BaseEntry(i, baseLevels[i]));
                pop.Add(PackEntry(baseLevels.Length, packCohort, d));

                ItemRarity actual = ItemCatalog.RarityOfMember(pop.ToArray(), baseLevels.Length);
                Assert.AreEqual(expected, actual,
                    $"{LogPrefix} ★ 코호트 크기 1에서 '{d}' 선언이 {actual}로 나왔습니다(기대 {expected}). " +
                    "선언값이 RarityOfRank 의 비율 환산을 통과하고 있습니다 — rank 0 ÷ count 1 이라 " +
                    "무엇을 선언하든 일반으로 뭉개집니다.");
            }

            Debug.Log($"{LogPrefix} 코호트 크기 1 — 선언 없음 -> {collapsed} / 선언 4단은 각각 그대로. " +
                      "선언은 환산을 타지 않는다.");
        }

        /// <summary>
        /// ★ C-2 — 팩 상한은 <see cref="ItemCatalog.MaxDeclaredRarityForPack"/>이 정한다.
        /// <b>숫자를 베끼지 않는다</b>(CLAUDE.md 확정 규칙). 여기서 잠그는 것은 상한의 <b>값</b>이 아니라
        /// <b>"영웅·전설은 타입에 존재하되 상한 위에 있다"</b>는 관계다 — 정책이 바뀌면 상수 하나만
        /// 움직이고 이 검사는 그대로 성립해야 한다.
        /// </summary>
        [Test]
        public void 팩_상한_위의_단이_실재하고_그게_감사의_존재_이유다()
        {
            var above = new List<DeclaredRarity>();
            var atOrBelow = new List<DeclaredRarity>();
            foreach (DeclaredRarity d in System.Enum.GetValues(typeof(DeclaredRarity)))
            {
                if (d == DeclaredRarity.Derived) continue;
                if (d > ItemCatalog.MaxDeclaredRarityForPack) above.Add(d); else atOrBelow.Add(d);
            }

            Assert.IsNotEmpty(above,
                $"{LogPrefix} ★대조 실패 — 상한('{ItemCatalog.MaxDeclaredRarityForPack}') 위의 단이 하나도 " +
                "없습니다. 그러면 상한 검사는 아무것도 막지 못하면서 영원히 초록입니다.");
            Assert.IsNotEmpty(atOrBelow,
                $"{LogPrefix} 상한 이하의 단이 하나도 없습니다 — 팩이 쓸 수 있는 등급이 없습니다.");
            Assert.IsTrue(DeclaredRarityRules.TryResolve(ItemCatalog.MaxDeclaredRarityForPack, out ItemRarity capped),
                $"{LogPrefix} 상한이 '선언 없음'입니다 — 팩이 아무것도 선언할 수 없습니다.");

            Debug.Log($"{LogPrefix} 팩 상한 '{ItemCatalog.MaxDeclaredRarityForPack}'(-> {capped}) / " +
                      $"허용 {atOrBelow.Count}단 · 차단 {above.Count}단({string.Join(",", above)}).");
        }

        // ---------------------------------------------------------------- 감사 (C-5 ②~⑥)

        /// <summary>★ 감사가 <b>깨끗한 것에는 침묵</b>하는가. 그리고 눈이 멀지 않았는가.
        /// 침묵만 확인하면 "규칙이 하나도 안 돌았다"와 구분되지 않으므로, 같은 모집단을
        /// 한 군데만 망가뜨려 <b>실제로 물어야</b> 한다.</summary>
        [Test]
        public void 감사가_올바른_팩에_침묵하고_망가진_팩에는_문다()
        {
            const int packCohort = ItemCatalog.BaseCohortId + 1;
            DeclaredRarity ok = ItemCatalog.MaxDeclaredRarityForPack;

            var clean = new List<ItemCatalogEntry>();
            for (int i = 0; i < 6; i++) clean.Add(BaseEntry(i, 1 + i * 5));
            for (int i = 0; i < 6; i++) clean.Add(PackEntry(6 + i, packCohort, ok));

            List<string> silent = Audit(clean);
            Assert.IsEmpty(silent,
                $"{LogPrefix} 규칙을 다 지킨 팩에서 결함이 {silent.Count}건 나왔습니다.\n" + string.Join("\n", silent));

            // ★ 양성 대조 — 같은 모집단에서 한 칸만 선언을 지운다.
            var broken = new List<ItemCatalogEntry>(clean);
            broken[6] = PackEntry(6, packCohort, DeclaredRarity.Derived);
            Assert.IsNotEmpty(Audit(broken),
                $"{LogPrefix} ★대조 실패 — 팩 한 종의 선언을 지웠는데 감사가 침묵했습니다. " +
                "위 '결함 0건'은 '깨끗함'이 아니라 '못 봄'입니다.");

            Debug.Log($"{LogPrefix} 감사 — 정상 팩 0건 / 선언 하나 지우면 {Audit(broken).Count}건.");
        }

        /// <summary>C-5 ② — 기본 코호트는 선언하지 않는다.</summary>
        [Test]
        public void 감사_기본_코호트가_선언하면_잡는다()
        {
            var pop = new List<ItemCatalogEntry>();
            for (int i = 0; i < 6; i++) pop.Add(BaseEntry(i, 1 + i * 5));
            Assert.IsEmpty(Audit(pop), $"{LogPrefix} 선언 없는 기본 코호트에서 결함이 나왔습니다.");

            pop[3] = BaseEntry(3, 16, ItemCatalog.MaxDeclaredRarityForPack);
            List<string> faults = Audit(pop);
            Assert.IsNotEmpty(faults, $"{LogPrefix} 기본 코호트가 등급을 선언했는데 감사가 침묵했습니다.");
            Debug.Log($"{LogPrefix} ② {faults[0]}");
        }

        /// <summary>★ C-5 ③ — <b>팩이 선언을 빠뜨리는 것</b>. 침묵이 가장 위험하다:
        /// 결과가 예외가 아니라 "파생으로 폴백"이고, 그러면 <b>한 팩 안에서 등급이 갈린다</b>.
        /// 그 갈라짐까지 이 검사가 함께 보여 준다.</summary>
        [Test]
        public void 감사_팩이_선언을_빠뜨리면_잡는다_그리고_그때_팩_안에서_등급이_갈린다()
        {
            const int packCohort = ItemCatalog.BaseCohortId + 1;
            var pop = new List<ItemCatalogEntry>();
            for (int i = 0; i < 6; i++) pop.Add(BaseEntry(i, 1 + i * 5));
            for (int i = 0; i < 6; i++) pop.Add(PackEntry(6 + i, packCohort, DeclaredRarity.Derived));

            List<string> faults = Audit(pop);
            Assert.IsNotEmpty(faults, $"{LogPrefix} 팩 6종이 전부 선언을 빠뜨렸는데 감사가 침묵했습니다.");

            // 그리고 그 침묵의 대가 — 같은 팩 안에서 등급이 갈린다.
            ItemCatalogEntry[] arr = pop.ToArray();
            var seen = new HashSet<ItemRarity>();
            for (int i = 6; i < arr.Length; i++) seen.Add(ItemCatalog.RarityOfMember(arr, i));
            Assert.Greater(seen.Count, 1,
                $"{LogPrefix} ★대조 실패 — 선언을 빠뜨린 팩 6종의 등급이 {seen.Count}종류뿐입니다. " +
                "'선언을 빠뜨리면 팩 안에서 등급이 갈린다'는 이 검사의 논거가 성립하지 않습니다.");

            Debug.Log($"{LogPrefix} ③ 결함 {faults.Count}건 / 선언을 빠뜨린 팩 6종의 등급이 " +
                      $"{seen.Count}종류로 갈림({string.Join(",", seen)}).");
        }

        /// <summary>C-5 ④ — 한 코호트 안에서 등급이 섞이면 안 된다(DS-2 단일 등급).</summary>
        [Test]
        public void 감사_코호트_안_등급_혼재를_잡는다()
        {
            const int packCohort = ItemCatalog.BaseCohortId + 1;
            DeclaredRarity a = ItemCatalog.MaxDeclaredRarityForPack;

            // 같은 코호트에서 a 와 다른 <b>허용 범위 안의</b> 단을 찾는다(상한 위반과 섞이지 않게).
            DeclaredRarity b = DeclaredRarity.Derived;
            foreach (DeclaredRarity d in System.Enum.GetValues(typeof(DeclaredRarity)))
            {
                if (d == DeclaredRarity.Derived || d == a || d > ItemCatalog.MaxDeclaredRarityForPack) continue;
                b = d;
                break;
            }
            if (b == DeclaredRarity.Derived)
            {
                Assert.Ignore($"{LogPrefix} 상한('{a}') 이하의 단이 하나뿐이라 '상한 위반 없는 혼재'를 " +
                    "만들 수 없습니다. 상한이 올라가면 이 검사가 자동으로 살아납니다.");
            }

            var pop = new List<ItemCatalogEntry>
            {
                PackEntry(0, packCohort, a),
                PackEntry(1, packCohort, a),
            };
            Assert.IsEmpty(Audit(pop), $"{LogPrefix} 같은 단만 쓴 코호트에서 결함이 나왔습니다.");

            pop[1] = PackEntry(1, packCohort, b);
            List<string> faults = Audit(pop);
            Assert.IsNotEmpty(faults, $"{LogPrefix} 코호트 안에서 '{a}'와 '{b}'가 섞였는데 감사가 침묵했습니다.");
            Debug.Log($"{LogPrefix} ④ {faults[0]}");
        }

        /// <summary>C-5 ⑤ — 팩 상한 초과. <b>상수를 참조</b>해 상한 바로 위의 단을 찾는다.</summary>
        [Test]
        public void 감사_상한_초과를_잡는다()
        {
            const int packCohort = ItemCatalog.BaseCohortId + 1;

            DeclaredRarity over = DeclaredRarity.Derived;
            foreach (DeclaredRarity d in System.Enum.GetValues(typeof(DeclaredRarity)))
            {
                if (d > ItemCatalog.MaxDeclaredRarityForPack) { over = d; break; }
            }
            Assert.AreNotEqual(DeclaredRarity.Derived, over,
                $"{LogPrefix} ★대조 실패 — 상한 위의 단이 없어 초과를 만들 수 없습니다. " +
                "그러면 이 검사는 영원히 아무것도 안 잡습니다.");

            var pop = new List<ItemCatalogEntry> { PackEntry(0, packCohort, ItemCatalog.MaxDeclaredRarityForPack) };
            Assert.IsEmpty(Audit(pop), $"{LogPrefix} 상한과 같은 단인데 결함이 나왔습니다(경계가 배타적입니다).");

            pop[0] = PackEntry(0, packCohort, over);
            List<string> faults = Audit(pop);
            Assert.IsNotEmpty(faults,
                $"{LogPrefix} 팩이 '{over}'를 선언했는데(상한 '{ItemCatalog.MaxDeclaredRarityForPack}') 감사가 침묵했습니다.");
            Debug.Log($"{LogPrefix} ⑤ {faults[0]}");
        }

        /// <summary>C-5 ⑥ — 팩의 <c>requiredLevel</c>은 <see cref="ItemCatalog.PackRequiredLevel"/>이어야 한다.</summary>
        [Test]
        public void 감사_팩_요구레벨이_1이_아니면_잡는다()
        {
            const int packCohort = ItemCatalog.BaseCohortId + 1;
            DeclaredRarity ok = ItemCatalog.MaxDeclaredRarityForPack;

            var pop = new List<ItemCatalogEntry> { PackEntry(0, packCohort, ok, ItemCatalog.PackRequiredLevel) };
            Assert.IsEmpty(Audit(pop), $"{LogPrefix} 요구 레벨이 규정값인데 결함이 나왔습니다.");

            pop[0] = PackEntry(0, packCohort, ok, ItemCatalog.PackRequiredLevel + 9);
            List<string> faults = Audit(pop);
            Assert.IsNotEmpty(faults,
                $"{LogPrefix} 팩의 requiredLevel 이 {ItemCatalog.PackRequiredLevel + 9}인데 감사가 침묵했습니다.");
            Debug.Log($"{LogPrefix} ⑥ {faults[0]}");
        }

        /// <summary>★ 출하 42종이 감사를 통과하는가. 위 대조들이 감사가 눈멀지 않았음을 이미 보였다.</summary>
        [Test]
        public void 감사_출하_42종에_결함이_0건이다()
        {
            var faults = new List<string>();
            int slots = 0, items = 0;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                if (count == 0) continue;
                slots++;

                var pop = new ItemCatalogEntry[count];
                for (int i = 0; i < count; i++) { pop[i] = ItemCatalog.Item(slot, i); items++; }
                ItemCatalog.AuditDeclarations(pop, faults);
            }

            Assert.AreEqual(ItemCatalog.SlotCount, slots,
                $"{LogPrefix} 훑은 슬롯이 {slots}개뿐입니다(전체 {ItemCatalog.SlotCount}) — 열거가 샙니다.");
            Assert.AreEqual(ItemCatalog.EquipmentCount, items,
                $"{LogPrefix} 감사에 넣은 아이템이 {items}종인데 장비는 {ItemCatalog.EquipmentCount}종입니다.");
            Assert.IsEmpty(faults,
                $"{LogPrefix} 출하 카탈로그에 선언 결함이 {faults.Count}건입니다.\n" + string.Join("\n", faults));

            Debug.Log($"{LogPrefix} 출하 {slots}슬롯 {items}종 감사 — 결함 0건.");
        }

        /// <summary>
        /// ★★ <b>C-6 불변식 — 출하 유저를 지키는 것은 이것 하나다.</b>
        /// 팩이 붙든 안 붙든 <b>기본 42종의 등급이 42/42 그대로</b>여야 한다.
        ///
        /// <para>팩의 <b>모양</b>을 셋 다 잰다(위로 쌓기 / 아래 깔기 / 사이사이). 사이사이는
        /// <c>rank</c>와 <c>count</c>가 같이 2배가 되어 <b>합쳐도 안 움직이므로</b> 그것만으로는
        /// 대조가 공허하다 — 이 파일이 이미 실측으로 확인한 함정이다.</para>
        /// </summary>
        [Test]
        public void 팩이_붙어도_기본_42종의_등급이_42_42_동일하다()
        {
            const int packCohort = ItemCatalog.BaseCohortId + 1;
            DeclaredRarity packDeclared = ItemCatalog.MaxDeclaredRarityForPack;

            var shapes = new (string Name, int[] Levels)[]
            {
                ("위로 쌓기(고레벨 팩)", new[] { 40, 43, 46, 49, 52, 55 }),
                ("아래 깔기(Lv.1 스타터 팩)", new[] { 0, 0, 0, 0, 0, 0 }),
                ("사이사이(끼워 넣기)", new[] { 3, 7, 12, 18, 22, 30 }),
            };

            foreach ((string name, int[] packLevels) in shapes)
            {
                int compared = 0;
                var moved = new List<string>();

                foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                {
                    int count = ItemCatalog.ItemCountIn(slot);
                    if (count == 0) continue;

                    // 기본만 있는 모집단 = 실제 카탈로그 그대로.
                    var withPack = new List<ItemCatalogEntry>();
                    for (int i = 0; i < count; i++) withPack.Add(ItemCatalog.Item(slot, i));
                    for (int i = 0; i < packLevels.Length; i++)
                        withPack.Add(PackEntry(count + i, packCohort, packDeclared, ItemCatalog.PackRequiredLevel));

                    ItemCatalogEntry[] arr = withPack.ToArray();
                    for (int i = 0; i < count; i++)
                    {
                        compared++;
                        ItemRarity alone = ItemCatalog.Rarity(slot, i);       // 팩 없는 출하 상태
                        ItemRarity mixed = ItemCatalog.RarityOfMember(arr, i); // 팩이 같은 슬롯에 로드된 상태
                        if (alone != mixed) moved.Add($"  {slot}#{i} {alone} -> {mixed}");
                    }
                }

                Assert.AreEqual(ItemCatalog.EquipmentCount, compared,
                    $"{LogPrefix} [{name}] 견준 아이템이 {compared}종인데 장비는 {ItemCatalog.EquipmentCount}종입니다.");
                Assert.IsEmpty(moved,
                    $"{LogPrefix} ★ [{name}] 팩이 붙자 기본 아이템 {moved.Count}종의 등급이 움직였습니다. " +
                    "팩을 <b>안 산</b> 사람에게 나타나는 증상이고, '기본 42종만으로 캡 20'이라는 " +
                    "사용자 확정 차단선이 깨집니다.\n" + string.Join("\n", moved));
            }

            // ★ 양성 대조 — 이 검사가 실제로 움직임을 볼 수 있는가.
            //   팩을 <b>기본 코호트에 선언 없이</b> 몰아넣으면 반드시 움직여야 한다.
            //   (이게 안 움직이면 위 '0칸 이동' 초록은 전부 무효다.)
            {
                EquipmentSlot slot = EquipmentSlot.Head;
                int count = ItemCatalog.ItemCountIn(slot);
                var collapsed = new List<ItemCatalogEntry>();
                for (int i = 0; i < count; i++) collapsed.Add(ItemCatalog.Item(slot, i));
                for (int i = 0; i < 6; i++)
                    collapsed.Add(BaseEntry(count + i, 40 + i * 3));   // 기본 코호트 · 선언 없음

                ItemCatalogEntry[] arr = collapsed.ToArray();
                var shifted = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    ItemRarity now = ItemCatalog.RarityOfMember(arr, i);
                    if (now != ItemCatalog.Rarity(slot, i)) shifted.Add($"{i}번 {ItemCatalog.Rarity(slot, i)}->{now}");
                }
                Assert.IsNotEmpty(shifted,
                    $"{LogPrefix} ★대조 실패 — 팩을 기본 코호트에 선언 없이 몰아넣었는데 등급이 하나도 " +
                    "안 움직였습니다. 위 '42/42 동일' 초록은 무효입니다.");

                Debug.Log($"{LogPrefix} C-6 — 팩 모양 3종 전부 0칸 이동 / 코호트를 무너뜨리면 " +
                          $"{shifted.Count}칸 이동({string.Join(" ", shifted)}). 불변식 성립.");
            }
        }

        /// <summary>
        /// ★ <b>C-3 을 구조로 보장한다</b> — 등급 -> 색 매핑이 하나뿐이려면, 선언 타입이
        /// <b>Core 밖으로 나가면 안 된다</b>. <see cref="DeclaredRarity"/>가 UI 까지 흘러가면
        /// 그쪽에서 "선언된 전설은 더 세게" 같은 두 번째 분기가 생길 수 있고,
        /// 그 순간 <c>UiChrome.RarityColor</c>가 유일한 출처가 아니게 된다.
        ///
        /// <para>선언은 <see cref="ItemCatalog.Rarity"/>를 지나 <see cref="ItemRarity"/>가 된 뒤에만
        /// 바깥으로 나간다. 그래서 화면은 선언과 파생을 <b>구분할 방법이 아예 없다</b>.</para>
        /// </summary>
        [Test]
        public void 선언_타입이_Core의_세_파일_밖으로_나가지_않는다()
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            Assert.IsTrue(Directory.Exists(scripts), $"{LogPrefix} {scripts}를 찾지 못했습니다.");

            var allowed = new HashSet<string>(System.StringComparer.Ordinal)
            {
                "ItemRarity.cs",      // 타입이 사는 곳
                "AccessoryDefSO.cs",  // 에셋이 적는 곳
                "ItemCatalog.cs",     // 등급으로 푸는 유일한 곳
            };

            var leaks = new List<string>();
            int scanned = 0, ownerHits = 0;
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}")) continue;
                scanned++;

                string name = Path.GetFileName(file);
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].TrimStart();
                    if (t.StartsWith("//", System.StringComparison.Ordinal) ||
                        t.StartsWith("*", System.StringComparison.Ordinal)) continue;
                    if (t.IndexOf("DeclaredRarity", System.StringComparison.Ordinal) < 0) continue;

                    if (allowed.Contains(name)) { ownerHits++; continue; }
                    leaks.Add($"  {name}:{i + 1}  {t}");
                }
            }

            // 양성 대조 — 스캐너가 실제로 그 낱말을 찾을 수 있는가.
            Assert.Greater(ownerHits, 0,
                $"{LogPrefix} ★대조 실패 — 허용 파일 3개에서 DeclaredRarity 를 한 번도 못 찾았습니다. " +
                "스캐너가 눈이 먼 상태이므로 아래 '유출 0건'도 무효입니다.");
            Assert.Greater(scanned, 40,
                $"{LogPrefix} 훑은 프로덕션 파일이 {scanned}개뿐입니다 — 경로 계산이 틀렸을 수 있습니다.");
            Assert.IsEmpty(leaks,
                $"{LogPrefix} 선언 타입이 Core 밖으로 {leaks.Count}건 새어 나갔습니다. 등급은 " +
                "ItemCatalog.Rarity 를 지나 ItemRarity 가 된 뒤에만 바깥으로 나가야 합니다 " +
                "(그래야 UiChrome.RarityColor 가 유일한 색 출처로 남습니다).\n" + string.Join("\n", leaks));

            Debug.Log($"{LogPrefix} 프로덕션 {scanned}개 파일 — 선언 타입 유출 0건 / 허용 3파일에서 {ownerHits}회 사용.");
        }

        // ---------------------------------------------------------------- 선언 검사용 소도구

        /// <summary>기본 코호트 항목. 선언은 기본적으로 하지 않는다(출하 42종과 같은 상태).</summary>
        private static ItemCatalogEntry BaseEntry(int index, int level,
            DeclaredRarity declared = DeclaredRarity.Derived)
            => ItemCatalogEntry.ForEquipment($"base.{index}", EquipmentSlot.Head, index,
                $"기본{index}", "합성", level, null, ItemCatalog.BaseCohortId, declared);

        /// <summary>팩 코호트 항목.</summary>
        private static ItemCatalogEntry PackEntry(int index, int cohort, DeclaredRarity declared,
            int level = ItemCatalog.PackRequiredLevel)
            => ItemCatalogEntry.ForEquipment($"pack.{index}", EquipmentSlot.Head, index,
                $"팩{index}", "합성", level, null, cohort, declared);

        /// <summary>규칙을 <b>다시 적지 않는다</b> — 프로덕션 감사 함수를 그대로 부른다.
        /// 테스트가 규칙을 두 벌로 갖고 있으면 둘이 갈라지고, 그때 어느 쪽이 옳은지 아무도 모른다.</summary>
        private static List<string> Audit(List<ItemCatalogEntry> population)
        {
            var faults = new List<string>();
            ItemCatalog.AuditDeclarations(population.ToArray(), faults);
            return faults;
        }

        // ============================================================================
        // 유틸 — 순위는 카탈로그가 아니라 여기서 독립 계산한다
        // ============================================================================

        /// <summary>이 슬롯의 자리별 순위. 요구 레벨 오름차순, 동점은 자리 번호가 앞선 쪽이 먼저.</summary>
        private static int[] IndependentRanks(EquipmentSlot slot, int count)
        {
            var levels = new int[count];
            for (int i = 0; i < count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                levels[i] = e?.RequiredLevel ?? 0;
            }
            return RanksOf(levels);
        }

        private static int[] RanksOf(int[] levels)
        {
            var rank = new int[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                int n = 0;
                for (int j = 0; j < levels.Length; j++)
                {
                    if (j == i) continue;
                    if (levels[j] < levels[i] || (levels[j] == levels[i] && j < i)) n++;
                }
                rank[i] = n;
            }
            return rank;
        }

        /// <summary>본안이 쓰는 비교 그대로. 사다리만 인자로 받는다 — 그래야 대조가 같은 함수를 탄다.</summary>
        private static List<string> CompareAgainstLadder(ItemRarity[] ladder)
        {
            var failures = new List<string>();
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int count = ItemCatalog.ItemCountIn(slot);
                if (count != ladder.Length) continue;

                int[] rank = IndependentRanks(slot, count);
                for (int i = 0; i < count; i++)
                {
                    ItemRarity expected = ladder[rank[i]];
                    ItemRarity actual = ItemCatalog.Rarity(slot, i);
                    if (expected == actual) continue;
                    failures.Add($"  {slot}#{i} rank {rank[i]} 기대 {expected} / 실제 {actual}");
                }
            }
            return failures;
        }
    }
}
