using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 리더 판정 L-1(재질 팔레트, 2026-09-05) — <b>인계본 조각의 채움·선은 등급색을 참조하지 않는다.</b>
    /// 등급은 카드 프레임·리본·낱말(<c>CharacterInfoWindow.Cards</c> → <see cref="UiChrome.RarityColor"/>)만의 것이다.
    ///
    /// <para><b>부재 단언은 대조와 함께</b>(CLAUDE.md 「부재 단언용 니들은 썩으면 조용히 초록」): (1) 조각이 해석한 색이 그 아이템의
    /// 등급색과 <b>하나도</b> 같지 않다(부재) — (2) 같은 비교기가 등급색 자체는 잡는다(양성 대조) — (3) 등급색은 카드 크롬 쪽에서
    /// <b>여전히 살아 있다</b>(존재 대조 — <see cref="UiChrome.RarityColor"/>가 등급마다 다른 값을 내고, 그 값이 재질 25색과 겹치지 않는다).
    /// 니들은 전부 상수/함수 참조다 — 문자열 사본이 아니다.</para>
    /// </summary>
    public sealed class HandoffRarityAbsenceTests
    {
        private static readonly EquipmentSlot[] Slots =
            { EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders };

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f && Mathf.Abs(a.b - b.b) < 1e-3f;

        private static List<AccessoryShapeBuilder.Shape> Build(EquipmentSlot slot, int item, AccessorySurface surface)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, AccessoryCardIcon.CardRig(), float.PositiveInfinity, 0f, false, surface);
            return sink;
        }

        [Test]
        public void 인계본_조각의_채움과_선은_그_아이템의_등급색을_참조하지_않는다()
        {
            var hits = new List<string>();
            int handoffPieces = 0;
            var ink = new Color(0.07f, 0.07f, 0.07f, 1f);   // 검은 잉크 사용자(기본값 계열)
            foreach (EquipmentSlot slot in Slots)
            {
                for (int item = 0; item < ItemCatalog.ItemCountIn(slot); item++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, item);
                    Color rarity = UiChrome.RarityColor(ItemCatalog.Rarity(slot, item));
                    AccessoryShapeBuilder.HandoffPalette body = AccessoryHandoffPalette.Body(slot, item, ink);
                    AccessoryShapeBuilder.HandoffPalette card = AccessoryHandoffPalette.Card(slot, item, entry.PrimaryColor, entry.SecondaryColor);

                    foreach ((AccessorySurface surface, AccessoryShapeBuilder.HandoffPalette palette) in
                        new[] { (AccessorySurface.Body, body), (AccessorySurface.Card, card) })
                    {
                        foreach (AccessoryShapeBuilder.Shape s in Build(slot, item, surface))
                        {
                            if (!s.IsHandoff) continue;
                            handoffPieces++;
                            if (s.Filled && Same(AccessoryShapeBuilder.HandoffFillBase(s, palette), rarity))
                                hits.Add($"{slot} {item} [{surface}] '{s.Name}' 채움");
                            if (!s.NoStroke && Same(AccessoryShapeBuilder.HandoffLineBase(s, palette), rarity))
                                hits.Add($"{slot} {item} [{surface}] '{s.Name}' 선");
                        }
                    }
                }
            }
            Assert.Greater(handoffPieces, 100, "인계본 조각이 너무 적게 잡혔습니다 — 아래 부재 단언이 공허합니다.");
            Assert.IsEmpty(hits, "조각이 등급색으로 칠해집니다(리더 판정 L-1 위반):\n  " + string.Join("\n  ", hits));
        }

        /// <summary>양성 대조 — 같은 비교기(<see cref="Same"/>)와 같은 팔레트 함수가 등급색을 <b>잡을 수 있는가</b>. 재질색 자리에 등급색을
        /// 일부러 넣으면 위 검사가 빨개져야 한다. 안 그러면 위 「0건」은 「안 봤다」와 구분되지 않는다.</summary>
        [Test]
        public void 컨트롤_재질색_자리에_등급색을_넣으면_비교기가_잡는다()
        {
            Color rarity = UiChrome.RarityColor(ItemRarity.Legendary);
            var palette = new AccessoryShapeBuilder.HandoffPalette(UiChrome.CardIconInk, 1f, UiChrome.CardSurfaceMuted, UiChrome.CardIconInk,
                rarity, rarity);
            Vector3[] pts = { Vector3.zero, Vector3.one, Vector3.right };
            var filled = new AccessoryShapeBuilder.Shape("f", pts, true, 0, tone: AccessoryTone.Primary, filled: true, strokeInR: 0.1f, alpha: 1f);
            var line = new AccessoryShapeBuilder.Shape("l", pts, false, 0, tone: AccessoryTone.Accent, strokeInR: 0.1f);
            Assert.IsTrue(Same(AccessoryShapeBuilder.HandoffFillBase(filled, palette), rarity), "비교기가 등급색 채움을 못 잡습니다.");
            Assert.IsTrue(Same(AccessoryShapeBuilder.HandoffLineBase(line, palette), rarity), "비교기가 등급색 재질선을 못 잡습니다.");
        }

        /// <summary>존재 대조 — 등급색은 카드 크롬에서 여전히 <b>살아 있는 값</b>이고(등급마다 다르다), 재질 팔레트의 어떤 아이템 색과도 겹치지 않는다.
        /// 겹치면 위 부재 단언이 정당한 재질색을 「등급색」으로 오인해 빨개진다(비교기의 전제).</summary>
        [Test]
        public void 등급색은_카드_크롬에_살아_있고_재질색과_겹치지_않는다()
        {
            var seen = new List<Color>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color c = UiChrome.RarityColor(r);
                foreach (Color prev in seen) Assert.IsFalse(Same(prev, c), $"등급 {r}의 색이 다른 등급과 같습니다 — 등급 램프가 죽었습니다.");
                seen.Add(c);
                foreach (EquipmentSlot slot in Slots)
                {
                    for (int item = 0; item < ItemCatalog.ItemCountIn(slot); item++)
                    {
                        ItemCatalogEntry e = ItemCatalog.Item(slot, item);
                        Assert.IsFalse(Same(e.PrimaryColor, c) || Same(e.SecondaryColor, c),
                            $"{e.Id}의 재질색이 등급 {r}의 색과 같습니다 — 팔레트 §6-(3)의 전제(등급색 ↔ 재질색 분리)가 깨졌습니다.");
                    }
                }
            }
            Assert.Greater(seen.Count, 1);
        }
    }
}
