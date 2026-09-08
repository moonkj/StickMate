using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★ 유료 팩 12종을 굽기 전에 닫아야 했던 두 장애물 — 2026-09-08
    /// ============================================================================
    ///
    /// <b>(A) 팩은 세트를 이룰 수 없었다.</b> <c>ItemCatalog.ThemeOfItem</c>·<c>SubStatOfItem</c>이
    /// <c>cohortId != BaseCohortId</c>면 표를 <b>아예 보지 않았고</b>, 팩이 테마를 선언할 통로가
    /// 없었다. 그래서 팩 4부위를 다 걸쳐도 <c>IsSetComplete</c>가 영원히 거짓이었고,
    /// 부스탯 기여도 0이었다 — <b>현금으로 산 아이템이 같은 자리의 무료 아이템보다 약했다.</b>
    ///
    /// <b>(B) 팩 모자가 들어오는 순간 결함 로그가 났다.</b> <c>HatCoverLocalY</c>의 <c>default:</c>가
    /// 코드 표 밖의 번호를 <b>전부</b> 「모르는 모자」로 신고했는데, 팩 HEAD 아이템의 자리가
    /// 정확히 거기다(자리 6번 이상).
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — 그리고 <b>양성·음성 대조를 짝으로</b> 둔다
    /// ============================================================================
    /// 이 저장소가 반복해서 당한 형태는 «죽은 프로브가 산 프로브와 똑같이 생겼다»이다.
    /// 그래서 «결함이 안 났다»를 주장하는 모든 자리에 «같은 장치가 실제로 결함을 잡는다»를
    /// 나란히 둔다. 한쪽만 있으면 그 초록은 아무것도 증명하지 않는다.
    ///
    /// <list type="number">
    ///  <item><b>기본 42종은 애셋 선언을 <u>구조적으로</u> 무시한다.</b> 무시가 규칙이지 우연이 아니다 —
    ///        그리고 무시했다는 사실은 감사가 크게 말한다(조용한 무시는 두 번째 진실이 된다).</item>
    ///  <item><b>팩은 선언으로 테마·부스탯을 얻는다</b> ⇒ <c>IsSetComplete</c>가 참이 된다(양성).
    ///        섞인 테마는 여전히 거부한다(음성).</item>
    ///  <item><b>감사 5종</b>(테마 누락 / 기본 테마 침범 / 외형이 선언 / 부스탯 누락 / 키 모양)이
    ///        각각 실제로 걸린다. 그리고 <b>정상 팩은 0건</b>이다(교정).</item>
    ///  <item><b>완성 가능성</b> — 한 슬롯이라도 비면 그 테마는 4/4가 불가능하고, 그건 슬롯 하나만
    ///        보는 감사가 <u>구조적으로 못 보는</u> 사실이다.</item>
    ///  <item><b>Major 4</b> — 커버선의 「가리는가」가 에셋으로 넘어갔고, 출하 6종은 안 움직였다.</item>
    /// </list>
    /// </summary>
    public sealed class PackThemeAndHatCoverTests
    {
        /// <summary>팩이 쓸 코호트 번호. <b>기본 코호트에서 유도</b>한다 — 숫자를 베끼면
        /// <c>BaseCohortId</c>가 바뀌는 날 이 파일만 옛 값을 지킨다.</summary>
        private static int PackCohort => ItemCatalog.BaseCohortId + 1;

        private static int OtherPackCohort => ItemCatalog.BaseCohortId + 2;

        /// <summary>팩이 쓸 테마 키. <b>기본 6테마가 아니어야</b> 한다는 것을 이 자리에서 확인한다
        /// (문자열을 베낀 것이 아니라, 베낀 값이 아직 유효한지 매번 다시 잰다).</summary>
        private const string PackTheme = "mine";

        private static AccessoryShapeBuilder.Rig Rig()
        {
            const float H = StickConfig.BaselineCharacterTotalHeight;
            const float R = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(R, H - R,
                AccessoryShapeBuilder.BaselineShoulderLocalY, AccessoryShapeBuilder.BaselineHipLocalY, 1f);
        }

        /// <summary>세트를 이루는 네 자리. <b>여기 적은 것이 아니라</b>
        /// <see cref="EquipmentModel.IsAppearanceSlot"/>가 정하는 사실을 그대로 옮긴다.</summary>
        private static EquipmentSlot[] StatSlots()
        {
            var list = new List<EquipmentSlot>(4);
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                if (!EquipmentModel.IsAppearanceSlot(slot)) list.Add(slot);
            }
            return list.ToArray();
        }

        private static ItemCatalogEntry PackItem(EquipmentSlot slot, int itemIndex, string id,
            string theme, DeclaredSubStat sub, int cohort = -1)
            => ItemCatalogEntry.ForEquipment(id, slot, itemIndex, id + ".name", id + ".desc",
                ItemCatalog.PackRequiredLevel, null,
                cohort < 0 ? PackCohort : cohort, ItemCatalog.MaxDeclaredRarityForPack, theme, sub);

        private static List<string> Audit(params ItemCatalogEntry[] population)
        {
            var faults = new List<string>();
            ItemCatalog.AuditDeclarations(population, faults);
            return faults;
        }

        // ============================================================================
        // 1. 기본 42종은 애셋 선언을 구조적으로 무시한다 (X-3 구조 결정)
        // ============================================================================

        [Test]
        public void 기본_코호트는_애셋이_테마를_적어도_코드_표를_따른다()
        {
            // 실제 출하 아이템 하나를 골라, 그 아이디로 «팩이 쓸 법한» 테마를 선언해 본다.
            ItemCatalogEntry sample = ItemCatalog.Item(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap);
            Assert.IsNotNull(sample, "모자 0번이 없습니다 — 이 대조의 전제가 깨졌습니다.");

            string fromTable = ItemCatalog.ThemeOfItem(sample.Id, ItemCatalog.BaseCohortId);
            Assert.IsNotEmpty(fromTable,
                "코드 표가 이 아이템의 테마를 모릅니다 — 아래 «선언이 져야 한다»가 공허해집니다.");
            Assert.AreNotEqual(PackTheme, fromTable,
                $"고른 표본의 코드 표 테마가 하필 '{PackTheme}'입니다 — 대조가 성립하지 않습니다.");

            Assert.AreEqual(fromTable,
                ItemCatalog.ResolveTheme(sample.Id, ItemCatalog.BaseCohortId, PackTheme),
                "기본 코호트에서 애셋 선언이 코드 표를 이겼습니다. 애셋 한 줄이 출하 42종의 세트를 " +
                "움직이는 경로가 생기면, 그 라운드는 X-3(신규 테마는 기본 코호트에 안 들어간다)을 " +
                "구조가 아니라 <b>약속</b>으로 지키게 됩니다.");

            int subFromTable = ItemCatalog.SubStatOfItem(sample.Id, ItemCatalog.BaseCohortId);
            Assert.AreEqual(subFromTable,
                ItemCatalog.ResolveSubStat(sample.Id, ItemCatalog.BaseCohortId, DeclaredSubStat.Agility),
                "기본 코호트에서 부스탯 선언이 코드 표를 이겼습니다(위와 같은 사고입니다).");

            // ★ 양성 대조 — 같은 함수가 <b>팩</b>에서는 선언을 실제로 받아들이는가.
            //   이것이 없으면 위 초록은 "함수가 선언을 아예 안 읽는다"와 구분되지 않는다.
            Assert.AreEqual(PackTheme, ItemCatalog.ResolveTheme(sample.Id, PackCohort, PackTheme),
                "팩 코호트에서도 선언이 무시됐습니다 — 그러면 위 대조는 «선언을 아무도 안 읽는다»를 " +
                "확인한 것뿐이고, 팩은 여전히 세트를 이룰 수 없습니다.");
            Assert.AreEqual((int)CharacterStat.Agility,
                ItemCatalog.ResolveSubStat(sample.Id, PackCohort, DeclaredSubStat.Agility),
                "팩 코호트에서 부스탯 선언이 안 읽힙니다.");
        }

        [Test]
        public void 출하_42종은_애셋에_테마도_부스탯도_적지_않았다()
        {
            // ★ 2026-09-08 — 「출하 42종」 = <b>기본 코호트</b>. 팩은 반대로 <b>반드시 적어야</b> 하고
            //   (안 적으면 «현금 아이템이 무료 아이템보다 약하다»), 그 선언은 실제로 읽힌다
            //   (ItemCatalog.ResolveTheme/ResolveSubStat 의 코호트 갈래). 그래서 두 방향을 함께 잰다 —
            //   한쪽만 재면 「아무도 안 읽는다」와 「기본이 안 적었다」가 구분되지 않는다.
            int counted = 0, packCounted = 0;
            var packSilent = new List<string>();
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                int n = ItemCatalog.ItemCountIn(slot);
                for (int i = 0; i < n; i++)
                {
                    ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                    if (e == null) continue;

                    if (!BaseCohortScope.IsBase(e))
                    {
                        packCounted++;
                        // 스탯 4슬롯의 팩 아이템은 테마·부스탯을 <b>둘 다</b> 선언해야 한다.
                        if (!EquipmentStatRules.IsStatSlot(slot)) continue;
                        if (string.IsNullOrEmpty(e.DeclaredTheme)
                            || !DeclaredSubStatRules.IsDeclared(e.DeclaredSubStat))
                        {
                            packSilent.Add($"{e.Id}(테마 '{e.DeclaredTheme}' · 부스탯 {e.DeclaredSubStat})");
                        }
                        continue;
                    }

                    counted++;

                    Assert.IsEmpty(e.DeclaredTheme,
                        $"[{e.Id}] 출하 아이템이 themeKey를 '{e.DeclaredTheme}'로 적었습니다. " +
                        "그 값은 읽히지 않으므로 화면은 안 바뀌지만, 적은 사람은 반영됐다고 믿습니다.");
                    Assert.IsFalse(DeclaredSubStatRules.IsDeclared(e.DeclaredSubStat),
                        $"[{e.Id}] 출하 아이템이 declaredSubStat을 '{e.DeclaredSubStat}'로 적었습니다(위와 같은 이유).");
                }
            }

            Assert.IsEmpty(packSilent,
                "스탯 슬롯의 팩 아이템이 테마 또는 부스탯을 적지 않았습니다: " + string.Join(" · ", packSilent) +
                ". 그러면 그 팩은 4부위를 다 걸쳐도 세트가 성립하지 않고 부스탯도 0입니다 — " +
                "«현금으로 산 물건이 무료 물건보다 약하다»가 됩니다(ItemCatalog.AuditDeclarations ⑩⑪).");

            // 빈 목록을 돌고 초록이 뜨는 형태를 막는다(docs/TEAM.md 거짓 통과 #5).
            Assert.AreEqual(BaseCohortScope.EquipmentCount, counted,
                "출하(기본 코호트) 장비를 " + counted + "종밖에 돌지 않았습니다 — 순회가 비면 위 단언들이 공허해집니다.");
            Assert.AreEqual(BaseCohortScope.PackEquipmentCount, packCounted,
                "팩 장비를 " + packCounted + "종밖에 돌지 않았습니다 — 열거가 샙니다.");
        }

        // ============================================================================
        // 2. 팩은 선언으로 세트를 이룬다 (양성) / 섞이면 거부한다 (음성)
        // ============================================================================

        [Test]
        public void 팩_테마_넷이_같으면_세트가_성립하고_섞이면_거부한다()
        {
            EquipmentSlot[] slots = StatSlots();
            Assert.AreEqual(4, slots.Length, "세트를 이루는 자리가 4개가 아닙니다 — 전제가 깨졌습니다.");

            var packLoadouts = new StatSlotLoadout[4];
            for (int i = 0; i < 4; i++)
            {
                ItemCatalogEntry e = PackItem(slots[i], ItemCatalog.EquipmentCount + i,
                    $"pack.mine.{i}", PackTheme, DeclaredSubStat.Focus);
                Assert.AreEqual(PackTheme, e.Theme,
                    $"팩 아이템 '{e.Id}'가 테마를 못 받았습니다 — 통로가 아직 막혀 있습니다.");
                packLoadouts[i] = new StatSlotLoadout(slots[i], true, ItemRarity.Rare, e.SubStat, e.Theme);
            }

            // (양성) 넷이 같은 팩 테마 -> 완성.
            StatBuild built = EquipmentStatRules.Evaluate(
                packLoadouts[0], packLoadouts[1], packLoadouts[2], packLoadouts[3]);
            Assert.IsTrue(built.SetComplete,
                "팩 4부위를 다 걸쳤는데 세트가 성립하지 않습니다 — 이것이 PART2 전체를 막던 결함입니다.");
            Assert.AreEqual(PackTheme, built.SetTheme, "완성된 테마 키가 팩 테마가 아닙니다.");
            Assert.AreEqual(EquipmentStatRules.SetCompletionBonusPerStat, built.SetBonus.Focus,
                "세트 보너스가 프로덕션 상수와 다릅니다 — 숫자를 베끼지 않고 그 상수를 참조합니다.");

            // (음성 1) 한 자리만 다른 테마 -> 거부.
            var mixed = new StatSlotLoadout(slots[3], true, ItemRarity.Rare,
                packLoadouts[3].SubStat, ItemCatalog.ThemeMil);
            StatBuild mixedBuild = EquipmentStatRules.Evaluate(
                packLoadouts[0], packLoadouts[1], packLoadouts[2], mixed);
            Assert.IsFalse(mixedBuild.SetComplete,
                "테마가 섞였는데 세트가 성립했습니다 — 판정이 문자열 동등성을 안 보고 있습니다.");

            // (음성 2) 무소속 넷 -> 거부. «빈 문자열끼리 같다»로 세면 아무것도 안 낀 사람에게 세트가 뜬다.
            var none = new StatSlotLoadout[4];
            for (int i = 0; i < 4; i++)
            {
                none[i] = new StatSlotLoadout(slots[i], true, ItemRarity.Common,
                    EquipmentStatRules.NoStat, ItemCatalog.ThemeUnassigned);
            }
            Assert.IsFalse(EquipmentStatRules.Evaluate(none[0], none[1], none[2], none[3]).SetComplete,
                "무소속 넷에 세트가 성립했습니다 — 외형만 맞춘 차림에 보너스가 붙습니다.");
        }

        [Test]
        public void 팩이_쓰는_테마는_기본_6테마와_겹치지_않는다()
        {
            string[] baseThemes = ItemCatalog.AllThemes();
            Assert.AreEqual(6, baseThemes.Length,
                "기본 테마가 6개가 아닙니다 — 아래 대조의 전제(R21 안 B)가 바뀌었습니다.");

            // (양성) 기본 6테마는 전부 «기본»으로 판정된다. 이게 없으면 아래 음성이 공허하다.
            for (int i = 0; i < baseThemes.Length; i++)
            {
                Assert.IsTrue(ItemCatalog.IsBaseTheme(baseThemes[i]),
                    $"'{baseThemes[i]}'가 기본 테마로 안 잡힙니다 — 판정기가 죽었습니다.");
            }

            // (음성) 팩 테마와 무소속은 «기본»이 아니다.
            Assert.IsFalse(ItemCatalog.IsBaseTheme(PackTheme),
                $"팩 테마 '{PackTheme}'가 기본 6테마와 겹칩니다 — 리더가 동결한 키를 다시 골라야 합니다.");
            Assert.IsFalse(ItemCatalog.IsBaseTheme(ItemCatalog.ThemeUnassigned),
                "무소속이 «기본 테마»로 잡히면 팩의 빈 칸이 전부 침범으로 신고됩니다.");
        }

        // ============================================================================
        // 3. 감사 — 정상 팩은 0건(교정), 잘못 만든 팩은 각각 잡힌다(양성)
        // ============================================================================

        [Test]
        public void 제대로_만든_팩은_결함이_0건이다()
        {
            EquipmentSlot[] slots = StatSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                List<string> faults = Audit(
                    PackItem(slots[i], 6, $"pack.mine.{i}", PackTheme, DeclaredSubStat.Charm));
                Assert.IsEmpty(faults,
                    $"정상 팩 아이템({slots[i]})이 결함으로 잡혔습니다 — 이 교정이 깨지면 " +
                    "아래 양성 대조의 «잡혔다»는 전부 무효입니다:\n  " + string.Join("\n  ", faults));
            }

            // 외형 슬롯 팩 아이템(테마·부스탯 없음)도 정상이다.
            List<string> appearanceFaults = Audit(
                PackItem(EquipmentSlot.Fx, 6, "pack.mine.fx", null, DeclaredSubStat.None));
            Assert.IsEmpty(appearanceFaults,
                "외형 슬롯 팩 아이템이 결함으로 잡혔습니다 — 외형은 무소속·NoStat이 정상입니다:\n  "
                + string.Join("\n  ", appearanceFaults));
        }

        [Test]
        public void 팩_스탯_아이템이_테마를_안_적으면_잡힌다()
        {
            List<string> faults = Audit(
                PackItem(EquipmentSlot.Head, 6, "pack.mine.helmet", null, DeclaredSubStat.Focus));
            Assert.AreEqual(1, faults.Count,
                "테마를 안 적은 팩 스탯 아이템이 «세트를 영원히 못 이룬다»는 사실로 신고되지 " +
                "않았습니다:\n  " + string.Join("\n  ", faults));
        }

        [Test]
        public void 팩이_기본_테마를_쓰면_잡힌다()
        {
            // 무료 세트의 경제가 유료 아이템으로 새는 형태다(IsSetComplete가 코호트를 안 보므로).
            List<string> faults = Audit(
                PackItem(EquipmentSlot.Head, 6, "pack.mine.helmet", ItemCatalog.ThemeMil, DeclaredSubStat.Focus));
            Assert.AreEqual(1, faults.Count,
                "팩이 기본 테마를 쓰는데 아무도 안 잡았습니다. 그 팩 1종 + 기본 3종 조합에 " +
                "세트 보너스가 붙습니다:\n  " + string.Join("\n  ", faults));
        }

        [Test]
        public void 팩_외형_아이템이_테마나_부스탯을_적으면_잡힌다()
        {
            List<string> withTheme = Audit(
                PackItem(EquipmentSlot.Pet, 6, "pack.mine.pet", PackTheme, DeclaredSubStat.None));
            Assert.AreEqual(1, withTheme.Count,
                "외형 슬롯이 테마를 적었는데 안 잡혔습니다:\n  " + string.Join("\n  ", withTheme));

            List<string> withSub = Audit(
                PackItem(EquipmentSlot.Fx, 6, "pack.mine.fx", null, DeclaredSubStat.Agility));
            Assert.AreEqual(1, withSub.Count,
                "외형 슬롯이 부스탯을 적었는데 안 잡혔습니다:\n  " + string.Join("\n  ", withSub));
        }

        [Test]
        public void 팩_스탯_아이템이_부스탯을_안_적으면_잡힌다()
        {
            List<string> faults = Audit(
                PackItem(EquipmentSlot.Neck, 6, "pack.mine.kerchief", PackTheme, DeclaredSubStat.None));
            Assert.AreEqual(1, faults.Count,
                "부스탯을 안 적은 팩 스탯 아이템이 안 잡혔습니다 — 현금으로 산 아이템이 같은 자리의 " +
                "무료 아이템보다 약한 채로 출하됩니다:\n  " + string.Join("\n  ", faults));
        }

        [Test]
        public void 테마_키가_키_모양이_아니면_잡힌다()
        {
            const string badKey = "Mine!";
            Assert.IsFalse(PackManifestKeys.IsWellFormed(badKey),
                "표본이 이미 키 모양입니다 — 이 대조가 성립하지 않습니다.");

            List<string> faults = Audit(
                PackItem(EquipmentSlot.Eyes, 6, "pack.mine.goggles", badKey, DeclaredSubStat.Focus));
            Assert.AreEqual(1, faults.Count,
                "키 모양이 아닌 테마가 안 잡혔습니다 — 표기가 갈리면 세트가 조용히 안 맞습니다:\n  "
                + string.Join("\n  ", faults));
        }

        // ============================================================================
        // 4. 완성 가능성 — 슬롯 하나만 보는 감사가 «구조적으로 못 보는» 사실
        // ============================================================================

        private static ItemCatalogEntry[][] Empty()
        {
            var bySlot = new ItemCatalogEntry[EquipmentModel.SlotCount][];
            for (int s = 0; s < bySlot.Length; s++) bySlot[s] = new ItemCatalogEntry[0];
            return bySlot;
        }

        [Test]
        public void 네_자리를_다_채운_팩_테마는_완성_가능하다()
        {
            EquipmentSlot[] slots = StatSlots();
            ItemCatalogEntry[][] bySlot = Empty();
            for (int i = 0; i < slots.Length; i++)
            {
                bySlot[(int)slots[i]] = new[]
                {
                    PackItem(slots[i], 6, $"pack.mine.{i}", PackTheme, DeclaredSubStat.Focus),
                };
            }

            var faults = new List<string>();
            ItemCatalog.AuditThemeCompletability(bySlot, faults);
            Assert.IsEmpty(faults,
                "네 자리를 다 채운 팩이 «완성 불가»로 잡혔습니다 — 이 교정이 깨지면 아래 양성 대조가 " +
                "무효입니다:\n  " + string.Join("\n  ", faults));

            // (양성) 한 자리를 빼면 잡힌다. 그리고 그 자리 이름이 문구에 있어야 고칠 수 있다.
            bySlot[(int)slots[3]] = new ItemCatalogEntry[0];
            faults.Clear();
            ItemCatalog.AuditThemeCompletability(bySlot, faults);
            Assert.AreEqual(1, faults.Count,
                "한 자리를 비웠는데 «완성 불가»가 안 잡혔습니다. 슬롯 하나만 보는 감사는 이 사실을 " +
                "구조적으로 못 봅니다 — 그 팩을 산 사람은 다 걸치고도 세트를 못 받습니다.");
            Assert.IsTrue(faults[0].Contains(EquipmentModel.SlotName(slots[3])),
                $"문구에 빠진 자리({EquipmentModel.SlotName(slots[3])})가 없습니다 — 무엇을 더 만들어야 " +
                "하는지 알 수 없습니다.\n  " + faults[0]);
        }

        [Test]
        public void 완성_가능성_감사는_기본_코호트를_보지_않는다()
        {
            // 기본 24종의 E1(각 테마가 4슬롯에 정확히 1종씩)은 EquipmentStatInvariantTests 소관이다.
            // 여기서 또 재면 같은 사실이 두 곳에서 계산된다.
            EquipmentSlot[] slots = StatSlots();
            ItemCatalogEntry[][] bySlot = Empty();
            bySlot[(int)slots[0]] = new[]
            {
                ItemCatalogEntry.ForEquipment("base.solo", slots[0], 0, "n", "d", 1, null,
                    ItemCatalog.BaseCohortId, DeclaredRarity.Derived),
            };

            var faults = new List<string>();
            ItemCatalog.AuditThemeCompletability(bySlot, faults);
            Assert.IsEmpty(faults,
                "기본 코호트를 완성 가능성 감사가 봤습니다 — 같은 사실이 두 곳에서 계산됩니다:\n  "
                + string.Join("\n  ", faults));
        }

        [Test]
        public void 서로_다른_팩은_같은_테마_이름을_써도_따로_센다()
        {
            // 코호트가 다르면 다른 상품이다. 한쪽이 네 자리를 다 채웠다고 다른 쪽이 완성되지 않는다.
            EquipmentSlot[] slots = StatSlots();
            ItemCatalogEntry[][] bySlot = Empty();
            for (int i = 0; i < slots.Length; i++)
            {
                bySlot[(int)slots[i]] = new[]
                {
                    PackItem(slots[i], 6, $"pack.a.{i}", PackTheme, DeclaredSubStat.Focus),
                };
            }
            // 두 번째 팩은 모자 한 자리만 갖고 같은 테마 이름을 쓴다.
            var head = new List<ItemCatalogEntry>(bySlot[(int)slots[0]])
            {
                PackItem(slots[0], 7, "pack.b.0", PackTheme, DeclaredSubStat.Focus, OtherPackCohort),
            };
            bySlot[(int)slots[0]] = head.ToArray();

            var faults = new List<string>();
            ItemCatalog.AuditThemeCompletability(bySlot, faults);
            Assert.AreEqual(1, faults.Count,
                "코호트가 다른 두 팩의 같은 테마 이름이 <b>하나로 합쳐졌습니다</b> — 그러면 B팩이 " +
                "모자 하나만 팔아도 A팩 덕분에 «완성 가능»으로 통과합니다:\n  "
                + string.Join("\n  ", faults));
            Assert.IsTrue(faults[0].Contains(OtherPackCohort.ToString()),
                "신고된 것이 두 번째 팩이 아닙니다:\n  " + faults[0]);
        }

        // ============================================================================
        // 5. Major 4 — 커버선의 「가리는가」가 에셋으로 넘어갔고, 출하 6종은 안 움직였다
        // ============================================================================

        [Test]
        public void 출하_모자_여섯의_커버선은_코드_표가_정본이다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float hc = rig.HeadCenterY, r = rig.HeadRadius;

            // ★ 숫자를 베끼지 않는다 — 프로덕션 상수에서 기대값을 다시 만든다.
            //   (그 상수 자체가 틀어지면 이 대조는 못 잡는다. 그건 AccessoryHatWearLineBandTests 소관이다.)
            AssertCover(AccessoryShapeBuilder.HeadCap, AccessoryShapeBuilder.HatBrimLocalY(rig), rig);
            AssertCover(AccessoryShapeBuilder.HeadBeanie,
                hc + r * AccessoryShapeBuilder.BeanieBandTopRatio, rig);
            AssertCover(AccessoryShapeBuilder.HeadFedora,
                hc + r * AccessoryShapeBuilder.FedoraBrimLineRatio, rig);
            AssertCover(AccessoryShapeBuilder.HeadBeret,
                hc + r * AccessoryShapeBuilder.BeretBrimLineRatio, rig);
            AssertCover(AccessoryShapeBuilder.HeadStraw,
                hc + r * AccessoryShapeBuilder.StrawBrimLineRatio, rig);

            Assert.AreEqual(AccessoryShapeBuilder.NothingCovered,
                AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadCrown, rig),
                "왕관의 면제가 사라졌습니다 — 이 면제는 여전히 <b>표의 값</b>이어야 합니다.");
        }

        private static void AssertCover(int hat, float expected, in AccessoryShapeBuilder.Rig rig)
        {
            Assert.AreEqual(expected, AccessoryShapeBuilder.HatCoverLocalY(hat, rig), 0f,
                $"모자 {hat}번의 커버선이 코드 표의 상수와 <b>비트 단위로</b> 다릅니다. " +
                "Major 4는 「가리는가」만 애셋으로 옮겼습니다 — 출하 6종의 「어디까지」는 " +
                "여기 적힌 case가 정본이고 한 비트도 움직이면 안 됩니다.");
        }

        [Test]
        public void 출하_모자_여섯의_hidesHair는_렌더러가_실제로_하는_말과_같다()
        {
            // ★ 이 대조는 <b>순환이 아니다</b>: 왼쪽은 에셋(ItemCatalog.HidesHair), 오른쪽은 코드 표다.
            //   자리 0~5는 HatCoverLocalY 가 애셋을 아예 보지 않는다(switch 안에서 끝난다).
            AccessoryShapeBuilder.Rig rig = Rig();
            int n = ItemCatalog.ItemCountIn(EquipmentSlot.Head);
            Assert.Greater(n, 0, "모자 카테고리가 비었습니다 — 이 순회가 공허해집니다.");

            // ★ 이 대조(에셋 ↔ 코드 표)는 <b>전 종</b>에 건다 — 팩 자리에서도 두 말이 갈리면 안 된다.
            //   다만 «면제는 왕관 하나»라는 <b>개수</b>는 기본 코호트의 성질이다: 팩 모자의 +∞는
            //   CoverOutsideCodeTable 이 정본화한 «가리지 않는다고 선언했다»이고 왕관과 <b>같은 사실</b>이다
            //   (2026-09-07 확정). 전량으로 세면 팩이 실린 날 «전체 − 2»가 되어 거짓 빨강이 난다.
            int covering = 0, baseCovering = 0;
            int baseCount = BaseCohortScope.CountIn(EquipmentSlot.Head);
            for (int i = 0; i < n; i++)
            {
                bool tableSaysHides =
                    !float.IsPositiveInfinity(AccessoryShapeBuilder.HatCoverLocalY(i, rig));
                if (tableSaysHides)
                {
                    covering++;
                    if (i < baseCount) baseCovering++;
                }

                Assert.AreEqual(tableSaysHides, ItemCatalog.HidesHair(EquipmentSlot.Head, i),
                    $"모자 {i}번에서 에셋(hidesHair)과 코드 표(HatCoverLocalY)가 다른 말을 합니다. " +
                    "HAIR 카테고리가 되살아나는 날 이 어긋남이 곧 «원인 모를 그림 변화»가 됩니다.");
            }

            Assert.AreEqual(baseCount - 1, baseCovering,
                $"덮는 <b>출하</b> 모자가 «{baseCount} − 1»이 아닙니다({baseCovering}종) — " +
                "승인된 코드 표 면제는 왕관 하나뿐입니다.");
            Debug.Log($"[팩테마] HEAD {n}종 중 덮는 모자 {covering}종 " +
                      $"(출하 {baseCovering}/{baseCount} · 팩은 선언으로 면제).");
        }

        [Test]
        public void 팩_모자는_가린다고_선언할_때만_결함으로_신고된다()
        {
            int packSlot = ItemCatalog.ItemCountIn(EquipmentSlot.Head);   // 팩이 쓰는 첫 자리
            ShapeCoverageGuard.ResetForTests();
            try
            {
                // (음성) 가리지 않는다고 선언한 팩 모자 = 왕관과 같은 사실. 신고 없음.
                Assert.AreEqual(AccessoryShapeBuilder.NothingCovered,
                    AccessoryShapeBuilder.CoverOutsideCodeTable(packSlot, declaresHidesHair: false),
                    "가리지 않는다고 선언한 모자의 커버선이 +∞가 아닙니다.");
                Assert.AreEqual(0, ShapeCoverageGuard.HitCount,
                    "가리지 않는다고 선언한 팩 모자를 결함으로 신고했습니다 — 2026-09-07까지 " +
                    "이 자리가 <b>팩 HEAD 아이템 전부</b>를 매 재구성마다 신고했습니다.");

                // (양성) 가린다고 선언했는데 커버선이 없다 = 진짜 결함. 가드가 살아 있음을 증명한다.
                LogAssert.Expect(LogType.Error, new Regex(@"\[도형\].*커버선"));
                Assert.AreEqual(AccessoryShapeBuilder.NothingCovered,
                    AccessoryShapeBuilder.CoverOutsideCodeTable(packSlot, declaresHidesHair: true),
                    "값 자체는 +∞가 맞습니다(모르는 모자 밑에서 자르면 머리카락까지 사라집니다).");
                Assert.AreEqual(1, ShapeCoverageGuard.LoggedCount,
                    "가드가 죽었습니다 — 위 «신고 0건»이 아무것도 증명하지 못하게 됩니다.");
            }
            finally
            {
                ShapeCoverageGuard.ResetForTests();
            }
        }
    }
}
