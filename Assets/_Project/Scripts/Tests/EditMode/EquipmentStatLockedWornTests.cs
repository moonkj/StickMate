using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 2026-09-06 — <b>같은 창의 다른 칸에 같은 모순이 남아 있었다.</b>
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 정보창 컬럼 1의 <b>슬롯 줄</b>과 <b>액자</b>·<b>몸</b>은 「걸쳤다 <c>&amp;&amp;</c> 해금됐다」를 함께 보는데,
    /// <see cref="EquipmentStatRules.SlotLoadout"/>만 <c>WornIndex &gt;= 0</c>에서 멈춰 있었다.
    /// 그 값이 <see cref="EquipmentStatRules.CurrentBuild"/>를 거쳐 <b>컬럼 2의 스탯 카드 · 「장비 합」 ·
    /// H-8 등급 눈금</b>에 들어간다.
    ///
    /// <para>실측 재현: 세이브에 <c>고글</c>(Lv11)·<c>요정 날개</c>(Lv28)가 든 <b>Lv3</b> 캐릭터에서
    /// 슬롯 줄과 액자는 「비어 있음」인데 <b>스탯 합계만</b> 그 둘을 계속 셌다. 더 나쁜 것은 그 숫자가
    /// <b>레벨을 요구치까지 올린 뒤와 완전히 같았다</b>는 점이다 — 이 칸에서 요구 레벨은
    /// <b>아무 일도 하지 않고 있었다</b>. 아래 ②가 정확히 그 등호를 금지한다.</para>
    ///
    /// ============================================================================
    /// 여기서 <b>재지 않는</b> 것 — 술어
    /// ============================================================================
    /// 「잠금 술어가 옳게 적혔는가」를 재현하지 않는다. 그러면 프로덕션과 테스트가 같은 식을 두 벌
    /// 갖게 되고 <b>같이 틀리면 아무도 모른다</b>. 대신 <b>두 상태의 관측값</b>을 맞대 본다:
    /// <list type="bullet">
    ///   <item><b>기준선</b> = 그 칸들을 <b>정말로</b> 벗겨 놓은 상태의 합계.</item>
    ///   <item><b>본 사건</b> = 세이브가 잠긴 것을 들고 있는 상태의 합계.</item>
    /// </list>
    /// 둘이 같아야 한다. 기대 숫자를 이 파일에서 계산하지 않으므로 <c>BASE</c>·<c>MAINV</c>·<c>CAP</c>가
    /// 튜닝돼도 이 테스트는 그대로 옳다(CLAUDE.md: 프로덕션 상수를 숫자로 베끼지 않는다).
    ///
    /// ============================================================================
    /// 공허하게 초록이 되지 않도록 — 대조군
    /// ============================================================================
    /// "둘 다 기본값이다"는 <b>아무것도 안 걸친 캐릭터</b>에서도 초록이다. 그래서 각 단계가 대조를 건다:
    /// <list type="number">
    ///   <item><b>해금된 것</b>을 걸치면 합계가 실제로 움직인다 — 이 관측이 애초에 장비를 볼 수 있다는 증명.</item>
    ///   <item>잠긴 상태에서도 <b>모델은 여전히 그것을 들고 있다</b>(<see cref="EquipmentModel.WornIndex"/>) —
    ///         세이브가 지워져서 초록이 된 것이 아님을 증명(<c>RestoreFromSave</c> 계약).</item>
    ///   <item>잠금이 <b>실제로 살아 있다</b>(<see cref="EquipmentModel.IsItemOwned"/>가 false) —
    ///         QA 해금 스위치가 켜진 채였다면 아래 단언은 아무것도 증명하지 못한다.</item>
    /// </list>
    ///
    /// <para>QA 해금 스위치(<see cref="EquipmentDebugUnlock"/>)는 에디터에서 <b>항상 켜져 있어 잠긴
    /// 아이템이 하나도 없다</b>. 그 상태로는 이 신고를 물어볼 수조차 없으므로 테스트 동안만 강제로 끈다.
    /// (EditMode 어셈블리에는 <c>InternalsVisibleTo</c>가 있어 리플렉션이 필요 없다 —
    /// <c>Scripts/AssemblyInfo.cs</c>.)</para>
    /// </summary>
    public sealed class EquipmentStatLockedWornTests
    {
        /// <summary>스탯을 주는 4슬롯. 번호를 베끼지 않고 프로덕션이 정한 순서를 그대로 읽는다.</summary>
        private static readonly EquipmentSlot[] StatSlots = BuildStatSlots();

        private static readonly CharacterStat[] AllStats =
        {
            CharacterStat.Focus, CharacterStat.Observation, CharacterStat.Charm, CharacterStat.Agility
        };

        private static EquipmentSlot[] BuildStatSlots()
        {
            var slots = new EquipmentSlot[EquipmentStatRules.StatCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = EquipmentStatRules.StatSlotAt(i);
            return slots;
        }

        [SetUp]
        public void SetUp()
        {
            // 잠금을 실제로 살린다: QA 해금 OFF + 갓 만든 캐릭터(Lv.1).
            EquipmentDebugUnlock.SetTestOverride(false);
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            // ★ 2026-09-06 정정 — 여기 있던 SetTestOverride(null)이 <b>뒤 픽스처를 물리게 만들고 있었다</b>.
            //   null은 "강제를 푼다"가 아니라 <b>실제 판정으로 되돌린다</b>이고, 에디터의 실제 판정은
            //   개발 구성이라 <b>켜짐</b>이다(EquipmentDebugUnlock.IsDevelopmentConfiguration).
            //   그런데 EditMode 스위트의 규약은 <b>꺼짐</b>이다 — GlobalEditModeTestIsolation.OneTimeSetUp이
            //   스위트 시작에 한 번 꺼 두고, 그 뒤로 아무도 다시 켜지 않는다는 전제로 모든 잠금 테스트가 선다.
            //   픽스처는 알파벳 순으로 도는데(러너 XML 실측) 이 픽스처 뒤에 ItemCatalogTests가 있어,
            //   여기서 켠 스위치가 그대로 넘어가 «Lv.5에 열림»이어야 할 상태 슬롯이 «보유»로 읽혔다(실패 2건).
            //   그러므로 되돌릴 자리는 "실제 판정"이 아니라 <b>스위트 규약</b>이다.
            //   (PlayMode는 규약이 반대라 그쪽 TearDown의 null은 그대로 옳다 — 그쪽엔 전역 강제가 없다.)
            EquipmentDebugUnlock.SetTestOverride(false);
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
        }

        // ====================================================================
        // ① 잠긴 착용물은 스탯 합계에 들어가지 않는다
        // ====================================================================

        [Test]
        public void 잠긴_착용물은_스탯_합계에서_빠진다()
        {
            Assert.IsTrue(TryPickLockedAndOwned(out EquipmentSlot slot, out int lockedItem, out int ownedItem),
                $"QA 해금을 끄고 Lv.{CharacterProgressionModel.Level}인데도 «잠긴 것과 보유한 것이 둘 다 있는» " +
                "스탯 슬롯이 하나도 없습니다 — 관측 전제가 성립하지 않습니다.");

            string lockedName = EquipmentModel.ItemName(slot, lockedItem);
            string ownedName = EquipmentModel.ItemName(slot, ownedItem);
            int requiredLevel = EquipmentModel.RequiredLevel(slot, lockedItem);

            // ---- 기준선 A: 스탯 4칸을 정말로 벗긴 상태 ----
            StripStatSlots();
            StatBuild bare = EquipmentStatRules.CurrentBuild();

            // ---- 대조군 B: 해금된 것을 걸치면 합계가 실제로 움직인다 ----
            WearFromSave(slot, ownedItem);
            StatBuild owned = EquipmentStatRules.CurrentBuild();
            Assert.IsTrue(DiffersSomewhere(bare, owned),
                $"보유한 «{ownedName}»을 걸쳤는데 합계가 하나도 안 움직였습니다 — 이 관측은 장비를 볼 수 " +
                "없으므로 아래 단언은 아무것도 증명하지 못합니다.");
            Assert.IsTrue(EquipmentStatRules.SlotLoadout(slot).Worn,
                $"보유한 «{ownedName}»이 로드아웃에서 미착용으로 읽힙니다 — 정상 경로가 회귀했습니다 " +
                "(이 고침은 잠긴 것만 걸러야 합니다).");

            // ---- 본 사건 C: 잠긴 것을 걸친 세이브 ----
            StripStatSlots();
            WearFromSave(slot, lockedItem);

            // 재현이 정말로 만들어졌는가(둘 다 없으면 아래 초록이 거짓이다).
            Assert.AreEqual(lockedItem, EquipmentModel.WornIndex(slot),
                $"모델이 «{lockedName}»을 더 이상 들고 있지 않습니다 — 화면이 숨긴 것이 아니라 세이브가 " +
                "지워진 것이라면 이 초록은 거짓입니다(RestoreFromSave 계약 위반).");
            Assert.IsFalse(EquipmentModel.IsItemOwned(slot, lockedItem),
                $"Lv.{CharacterProgressionModel.Level}인데 «{lockedName}»(Lv.{requiredLevel})이 보유 상태입니다 " +
                "— 잠금이 살아 있지 않으면 아래 단언은 아무것도 증명하지 못합니다.");

            // ★ 고침의 본체 — 합계가 «정말로 벗긴» 상태와 <b>같은 말</b>을 한다.
            StatBuild locked = EquipmentStatRules.CurrentBuild();
            Assert.IsFalse(EquipmentStatRules.SlotLoadout(slot).Worn,
                $"잠긴 «{lockedName}»(Lv.{requiredLevel} 필요)이 로드아웃에서 착용으로 읽힙니다 — " +
                "액자·몸·슬롯 줄과 다른 술어를 쓰고 있습니다.");
            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(bare.Total.Of(stat), locked.Total.Of(stat),
                    $"{EquipmentStatRules.StatName(stat)} — 액자에는 아무것도 안 그려지는 잠긴 «{lockedName}»" +
                    $"(Lv.{requiredLevel} 필요 / 지금 Lv.{CharacterProgressionModel.Level})이 스탯 합계에는 " +
                    "그대로 세어졌습니다. 같은 창이 두 가지를 동시에 주장합니다 — 2026-09-06 신고 재발.");
                Assert.AreEqual(bare.EquipmentBonus.Of(stat), locked.EquipmentBonus.Of(stat),
                    $"{EquipmentStatRules.StatName(stat)}의 「장비 합」 괄호 값이 잠긴 «{lockedName}»을 " +
                    "세고 있습니다.");
            }
        }

        // ====================================================================
        // ② 숨긴 것이지 지운 것이 아니다 — 레벨이 닿으면 스탯도 되살아난다
        //    ★ 그리고 <b>잠긴 동안의 숫자가 해금 뒤와 같아서는 안 된다</b>.
        //      고치기 전에는 그 둘이 정확히 같았다(요구 레벨이 이 칸에서 아무 일도 안 했다).
        // ====================================================================

        [Test]
        public void 레벨이_요구치에_닿으면_스탯이_저절로_되살아난다()
        {
            Assert.IsTrue(TryPickLockedAndOwned(out EquipmentSlot slot, out int lockedItem, out _),
                "잠긴 아이템이 있는 스탯 슬롯을 찾지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            string lockedName = EquipmentModel.ItemName(slot, lockedItem);
            int requiredLevel = EquipmentModel.RequiredLevel(slot, lockedItem);

            StripStatSlots();
            StatBuild bare = EquipmentStatRules.CurrentBuild();

            WearFromSave(slot, lockedItem);
            StatBuild hidden = EquipmentStatRules.CurrentBuild();

            // 레벨을 요구치까지 올린다. 해금 스위치는 <b>계속 꺼진 채로</b> — 레벨이 유일한 문이다.
            CharacterProgressionModel.RestoreFromSave(requiredLevel, 0f, 0f,
                CharacterProgressionModel.CharacterName);
            Assert.IsTrue(EquipmentModel.IsItemOwned(slot, lockedItem),
                $"Lv.{CharacterProgressionModel.Level}(요구 {requiredLevel})인데 «{lockedName}»이 아직 " +
                "미보유입니다 — 대조를 만들 수 없습니다.");
            StatBuild revived = EquipmentStatRules.CurrentBuild();

            Assert.AreEqual(lockedItem, EquipmentModel.WornIndex(slot),
                "레벨이 오른 뒤 모델의 착용 자리가 달라졌습니다 — 잠긴 동안 세이브가 바뀌었습니다.");
            Assert.IsTrue(DiffersSomewhere(bare, revived),
                $"Lv.{CharacterProgressionModel.Level}에서 «{lockedName}»을 걸쳤는데 합계가 기본값 그대로입니다 " +
                "— 숨긴 것이 아니라 지운 것이 됐습니다.");

            // ★ 이 등호가 바로 결함의 모양이었다.
            Assert.IsTrue(DiffersSomewhere(hidden, revived),
                $"«{lockedName}»(Lv.{requiredLevel} 필요)의 스탯 기여가 <b>잠긴 동안과 해금된 뒤가 같습니다</b> " +
                "— 이 칸에서 요구 레벨이 아무 일도 하지 않고 있습니다.");
        }

        // ====================================================================
        // ③ 세트 완성도 같은 잠금 검사를 따라간다
        //    (잠금 검사를 세트 쪽에 <b>또</b> 적지 않는다 — SlotLoadout 하나로 닫힌다)
        // ====================================================================

        [Test]
        public void 세트_완성은_입을_수_없는_장비로는_뜨지_않는다()
        {
            Assert.IsTrue(TryPickSetTheme(out string theme, out int[] items, out int topLevel),
                "4부위가 같은 테마이면서 «한 칸만 잠기는» 레벨을 만들 수 있는 조합을 카탈로그에서 찾지 " +
                "못했습니다 — 세트 관측 전제가 성립하지 않습니다.");

            StripStatSlots();
            for (int s = 0; s < StatSlots.Length; s++) WearFromSave(StatSlots[s], items[s]);

            // ---- 대조군: 전부 보유하는 레벨에서는 세트가 실제로 뜬다 ----
            CharacterProgressionModel.RestoreFromSave(topLevel, 0f, 0f,
                CharacterProgressionModel.CharacterName);
            StatBuild complete = EquipmentStatRules.CurrentBuild();
            Assert.IsTrue(complete.SetComplete,
                $"Lv.{topLevel}에서 «{theme}» 4부위를 다 걸쳤는데 세트가 안 뜹니다 — 이 관측은 세트를 볼 수 " +
                "없으므로 아래 단언이 무의미해집니다.");
            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(EquipmentStatRules.SetCompletionBonusPerStat, complete.SetBonus.Of(stat),
                    $"{EquipmentStatRules.StatName(stat)}의 세트 보너스가 완성인데도 규정값이 아닙니다.");
            }

            // ---- 본 사건: 한 칸만 잠기는 레벨 ----
            CharacterProgressionModel.RestoreFromSave(topLevel - 1, 0f, 0f,
                CharacterProgressionModel.CharacterName);

            int lockedCount = 0;
            for (int s = 0; s < StatSlots.Length; s++)
            {
                if (!EquipmentModel.IsItemOwned(StatSlots[s], items[s])) lockedCount++;
            }
            Assert.AreEqual(1, lockedCount,
                $"Lv.{CharacterProgressionModel.Level}에서 «{theme}» 4부위 중 잠긴 칸이 {lockedCount}개입니다 " +
                "— 정확히 한 칸만 잠긴 상태를 만들지 못했습니다.");

            StatBuild broken = EquipmentStatRules.CurrentBuild();
            Assert.IsFalse(broken.SetComplete,
                $"Lv.{CharacterProgressionModel.Level}에서 «{theme}» 네 칸 중 한 칸은 <b>입을 수도 없는데</b> " +
                "세트 완성이 떴습니다 — 화면이 존재하지 않는 것을 약속합니다.");
            Assert.IsNull(broken.SetTheme,
                $"세트가 미완성인데 테마 «{broken.SetTheme}»가 남아 있습니다.");
            foreach (CharacterStat stat in AllStats)
            {
                Assert.AreEqual(0, broken.SetBonus.Of(stat),
                    $"{EquipmentStatRules.StatName(stat)} — 미완성인데 세트 보너스가 붙었습니다.");
            }
        }

        // ==================== 도구 ====================

        /// <summary>스탯 4칸을 <b>정말로</b> 미착용으로. 저장 복원 경로를 그대로 쓴다.</summary>
        private static void StripStatSlots()
        {
            foreach (EquipmentSlot slot in StatSlots)
            {
                EquipmentModel.RestoreFromSave(slot, (string)null);
            }
        }

        /// <summary>저장 파일이 그 아이템을 들고 있는 상태를 만든다 — 잠금을 검사하지 않는 <b>바로 그</b>
        /// 경로다(<see cref="EquipmentModel.RestoreFromSave(EquipmentSlot,string)"/>). 착용 API로 만들면
        /// 잠긴 것을 애초에 걸칠 수 없어 신고된 상태를 재현할 수 없다.</summary>
        private static void WearFromSave(EquipmentSlot slot, int itemIndex)
            => EquipmentModel.RestoreFromSave(slot, EquipmentModel.ItemId(slot, itemIndex));

        private static bool DiffersSomewhere(in StatBuild a, in StatBuild b)
        {
            foreach (CharacterStat stat in AllStats)
            {
                if (a.Total.Of(stat) != b.Total.Of(stat)) return true;
                if (a.EquipmentBonus.Of(stat) != b.EquipmentBonus.Of(stat)) return true;
            }
            return false;
        }

        /// <summary>지금 레벨에서 «잠긴 것»과 «보유한 것»이 <b>둘 다</b> 있는 첫 스탯 슬롯.
        /// <para>슬롯 번호도 아이템 번호도 <b>베끼지 않는다</b> — 카탈로그의 요구 레벨이 바뀌거나
        /// 아이템이 늘어도 이 함수는 그대로 옳다.</para></summary>
        private static bool TryPickLockedAndOwned(out EquipmentSlot slot, out int lockedItem, out int ownedItem)
        {
            foreach (EquipmentSlot candidate in StatSlots)
            {
                int locked = -1, owned = -1;
                int count = EquipmentModel.ItemCount(candidate);
                for (int i = 0; i < count; i++)
                {
                    if (EquipmentModel.IsItemOwned(candidate, i)) { if (owned < 0) owned = i; }
                    else if (locked < 0) locked = i;
                }
                if (locked < 0 || owned < 0) continue;

                slot = candidate;
                lockedItem = locked;
                ownedItem = owned;
                return true;
            }

            slot = default;
            lockedItem = -1;
            ownedItem = -1;
            return false;
        }

        /// <summary>스탯 4슬롯 전부에 같은 테마 아이템이 있고, 그중 <b>요구 레벨 최고가 유일</b>한 테마.
        /// 유일해야 <c>최고 - 1</c> 레벨에서 «정확히 한 칸만 잠긴» 상태가 만들어진다.
        /// <para>테마 키를 문자열로 베끼지 않는다 — <see cref="ItemCatalog.AllThemes"/>가 소스다.</para></summary>
        private static bool TryPickSetTheme(out string theme, out int[] items, out int topLevel)
        {
            foreach (string candidate in ItemCatalog.AllThemes())
            {
                if (string.IsNullOrEmpty(candidate)) continue;

                var picked = new int[StatSlots.Length];
                bool full = true;
                for (int s = 0; s < StatSlots.Length; s++)
                {
                    picked[s] = IndexOfThemeIn(StatSlots[s], candidate);
                    if (picked[s] < 0) { full = false; break; }
                }
                if (!full) continue;

                int hi = int.MinValue, second = int.MinValue;
                for (int s = 0; s < StatSlots.Length; s++)
                {
                    int required = EquipmentModel.RequiredLevel(StatSlots[s], picked[s]);
                    if (required == int.MaxValue) { hi = int.MinValue; break; }   // 도달 불가 자리
                    if (required > hi) { second = hi; hi = required; }
                    else if (required > second) second = required;
                }
                if (hi == int.MinValue || hi <= second) continue;

                theme = candidate;
                items = picked;
                topLevel = hi;
                return true;
            }

            theme = null;
            items = null;
            topLevel = 0;
            return false;
        }

        private static int IndexOfThemeIn(EquipmentSlot slot, string theme)
        {
            for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
            {
                ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                if (entry != null && entry.Theme == theme) return i;
            }
            return -1;
        }
    }
}
