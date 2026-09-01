using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 카드 썸네일 ↔ 몸 도형 <b>단일 소스</b> 회귀 — 2026-09-01, 로드맵 P0-a(리더 승인 옵션 2).
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// 한 아이템이 그림을 <b>두 벌</b> 갖고 있었다 — 카드는 손으로 배치한 40×40 SVG, 몸은 절차적 계산.
    /// 그래서 도형을 고칠 때마다 카드만 옛 모양으로 남았고, 사용자의 "카드 그림과 실제 착용 모습의
    /// 퀄리티가 너무 다름"이 거기서 나왔다. 이번 라운드에 머리 4종을 다시 그리므로 통합하지 않으면
    /// 그 괴리가 <b>오히려 더 심해진다</b>.
    ///
    /// 여기서 잠그는 것은 세 가지다:
    ///  (1) 몸 도형이 있는 카테고리는 <b>반드시</b> 새 경로로 그려진다(조용히 옛 아이콘으로 새지 않는다).
    ///  (2) 몸 도형이 없는 카테고리(FX/PET)는 <b>폴백</b>으로 넘어가고, 폴백 그림이 실제로 존재한다.
    ///  (3) 채움 면이 <see cref="Image"/>다 — 정보창의 잠김/해금 색칠이 Image만 수집하기 때문이다.
    /// </summary>
    public sealed class AccessoryCardIconTests
    {
        private const float IconSize = 50f;
        private const float IconStroke = 1.7f * (IconSize / 40f);

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        private RectTransform NewRoot()
        {
            var go = new GameObject("CardIconRoot", typeof(RectTransform));
            _spawned.Add(go);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(IconSize, IconSize);
            return rt;
        }

        /// <summary>몸 도형이 있는 자리 = <see cref="AccessoryShapeBuilder"/>가 아는 자리.</summary>
        private static readonly EquipmentSlot[] BodySlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck,
            EquipmentSlot.Shoulders, EquipmentSlot.Hair,
        };

        [Test]
        public void 몸_도형이_있는_아이템은_전부_새_경로로_그려진다()
        {
            for (int s = 0; s < BodySlots.Length; s++)
            {
                EquipmentSlot slot = BodySlots[s];
                int count = ItemCatalog.ItemCountIn(slot);
                for (int i = 0; i < count; i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    RectTransform root = NewRoot();

                    bool built = AccessoryCardIcon.TryBuild(root, slot, i, IconSize, IconStroke,
                        entry.PrimaryColor, entry.SecondaryColor);

                    Assert.IsTrue(built,
                        $"{slot} {i}번({entry.DisplayName})이 몸 도형에서 카드 그림을 만들지 못했습니다 — " +
                        "폴백으로 새면 이 아이템만 옛 SVG 좌표로 남아 통합이 반쪽이 됩니다.");
                    Assert.Greater(root.childCount, 0, $"{slot} {i}번의 카드 그림이 비었습니다.");
                }
            }
        }

        [Test]
        public void 몸_도형이_없는_카테고리는_폴백으로_넘어가고_폴백_그림이_존재한다()
        {
            var noBodyShape = new[] { EquipmentSlot.Fx, EquipmentSlot.Pet };
            for (int s = 0; s < noBodyShape.Length; s++)
            {
                EquipmentSlot slot = noBodyShape[s];
                int count = ItemCatalog.ItemCountIn(slot);
                Assert.Greater(count, 0, $"{slot} 카테고리가 비었습니다.");

                for (int i = 0; i < count; i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    RectTransform root = NewRoot();

                    bool built = AccessoryCardIcon.TryBuild(root, slot, i, IconSize, IconStroke,
                        entry.PrimaryColor, entry.SecondaryColor);

                    Assert.IsFalse(built,
                        $"{slot} {i}번이 몸 도형에서 그려졌습니다 — 이펙트/펫은 " +
                        "Interaction/AppearanceShapeBuilder 소관이라 여기서 나올 수 없습니다.");
                    Assert.AreEqual(0, root.childCount, $"{slot} {i}번: 실패했는데 조각이 남았습니다.");

                    // 폴백이 실제로 그릴 것이 있어야 한다 — 없으면 카드가 빈 칸이 된다.
                    Assert.IsNotNull(entry.Icon,
                        $"{slot} {i}번({entry.DisplayName})의 폴백 아이콘이 없습니다. " +
                        "AccessoryDefSO.icon[]을 이번 라운드에 지우지 않은 이유가 이것입니다.");
                    Assert.Greater(entry.Icon.Length, 0, $"{slot} {i}번의 폴백 아이콘이 비었습니다.");
                }
            }
        }

        /// <summary>28종 전부가 <b>폴백을 갖고 있다</b> — 새 경로가 통째로 틀려도 카드가 비지 않는다.</summary>
        [Test]
        public void 모든_장비가_폴백_아이콘을_그대로_갖고_있다()
        {
            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    Assert.IsNotNull(entry.Icon, $"{slot} {i}번({entry.DisplayName})의 폴백 아이콘이 사라졌습니다.");
                }
            }
        }

        [Test]
        public void 채움_면은_Image라서_잠김_색칠에_함께_잡힌다()
        {
            // 모자(채움 2개)를 대표로 본다 — 채움이 확실히 있는 자리다.
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap);
            RectTransform root = NewRoot();
            Assert.IsTrue(AccessoryCardIcon.TryBuild(root, EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap,
                IconSize, IconStroke, entry.PrimaryColor, entry.SecondaryColor));

            var fills = root.GetComponentsInChildren<AccessoryFillGraphic>(true);
            Assert.Greater(fills.Length, 0, "모자 카드에 채움 면이 없습니다 — 카드가 다시 선화로 돌아갔습니다.");

            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < fills.Length; i++)
            {
                Assert.Contains(fills[i], images,
                    "채움 면이 Image로 수집되지 않습니다 — CharacterInfoWindow의 잠김/해금 색칠이 " +
                    "이 면만 건너뛰어, 잠긴 카드에서 채움만 제 색으로 남습니다.");
            }
        }

        /// <summary>그림이 카드 상자를 넘지 않는다(획 두께를 감안한 여유 포함).</summary>
        [Test]
        public void 카드_그림이_상자를_넘지_않는다()
        {
            float limit = IconSize * 0.5f + IconStroke;
            for (int s = 0; s < BodySlots.Length; s++)
            {
                EquipmentSlot slot = BodySlots[s];
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    RectTransform root = NewRoot();
                    Assert.IsTrue(AccessoryCardIcon.TryBuild(root, slot, i, IconSize, IconStroke,
                        entry.PrimaryColor, entry.SecondaryColor));

                    var children = root.GetComponentsInChildren<RectTransform>(true);
                    for (int c = 0; c < children.Length; c++)
                    {
                        if (children[c] == root) continue;
                        Vector2 p = children[c].anchoredPosition;
                        Assert.LessOrEqual(Mathf.Abs(p.x), limit,
                            $"{slot} {i}번의 '{children[c].name}'이 카드 상자 밖(x={p.x:F2})에 있습니다.");
                        Assert.LessOrEqual(Mathf.Abs(p.y), limit,
                            $"{slot} {i}번의 '{children[c].name}'이 카드 상자 밖(y={p.y:F2})에 있습니다.");
                    }
                }
            }
        }

        /// <summary>카드 리그의 비율이 <b>몸의 실측 비율</b>과 같은가(규칙 4-a — 매직넘버 금지).</summary>
        [Test]
        public void 카드_리그가_몸의_실측_비율에서_유도된다()
        {
            AccessoryShapeBuilder.Rig rig = AccessoryCardIcon.CardRig();
            Assert.AreEqual(AccessoryShapeBuilder.BaselineHeadVisualRadius, rig.HeadRadius, 1e-5f);
            Assert.AreEqual(StickConfig.BaselineCharacterTotalHeight - AccessoryShapeBuilder.BaselineHeadVisualRadius,
                rig.HeadCenterY, 1e-5f);
            Assert.AreEqual(AccessoryShapeBuilder.BaselineShoulderLocalY, rig.ShoulderY, 1e-5f);
            Assert.AreEqual(AccessoryShapeBuilder.BaselineHipLocalY, rig.HipY, 1e-5f);
            Assert.AreEqual(1f, rig.Facing, 1e-5f, "카드는 언제나 정면(facing +1)이어야 합니다.");
        }

        /// <summary>카드는 <b>단품</b>이다 — 지금 쓴 모자에 따라 머리카락이 잘리면 카드가 상태에 끌려간다.</summary>
        [Test]
        public void 카드_머리카락은_모자_상태에_영향받지_않는다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly);
            RectTransform root = NewRoot();
            Assert.IsTrue(AccessoryCardIcon.TryBuild(root, EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly,
                IconSize, IconStroke, entry.PrimaryColor, entry.SecondaryColor));

            var bare = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(bare, EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly,
                AccessoryCardIcon.CardRig());

            int strokes = 0;
            for (int i = 0; i < bare.Count; i++) strokes += bare[i].Filled ? 1 : 0;
            Assert.Greater(strokes, 0, "곱슬머리에 채움이 없습니다.");
            Assert.AreEqual(strokes, root.GetComponentsInChildren<AccessoryFillGraphic>(true).Length,
                "카드의 채움 개수가 몸 도형의 채움 개수와 다릅니다 — 카드가 다른 그림을 그리고 있습니다.");
        }
    }
}
