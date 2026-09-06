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
    ///
    /// ============================================================================
    /// ★ 2026-09-06 배관 통일 — 42종이 같은 프레임·같은 획·같은 잉크를 탄다
    /// ============================================================================
    /// 사용자 신고 *"내가 지시한 장비만 고도화 되어있고 나머지 장비들은 고도화 안되어있음"*. 인계본 16종과
    /// v1 14종이 <b>세 가지 배관</b>에서 갈라져 있었고(획 폭 +23.6% · 조각마다 다른 윤곽 색 · 아이템마다 튀는 배율),
    /// 이 라운드가 셋을 닫았다. 여기서 새로 잠그는 것:
    ///  (4) 카드 배율은 <b>슬롯마다 하나</b>다 — 봉투 맞춤(아이템마다 다른 배율)이 되살아나지 않는다.
    ///  (5) 인계본 16종의 <b>넘침 보정이 항등</b>이다 — 이 변경이 16종을 회귀시키지 않았다는 구조적 근거.
    ///  (6) 카드 획은 인계본 비율(2.2/64) 하나이고 <b>부르는 쪽이 준 값에 좌우되지 않는다</b>(죽은 인자 감시).
    ///  (7) v1 조각의 채움 윤곽도 <b>잉크 한 색</b>이다 — 옛 <c>채움×0.28</c>은 한 조각도 남지 않았다.
    /// </summary>
    public sealed class AccessoryCardIconTests
    {
        private const float IconSize = 50f;

        /// <summary>부르는 쪽(<c>CharacterInfoWindow</c>)이 넘기는 옛 폴백 획. ★ 2026-09-06부터 <b>카드는 이 값을 쓰지 않는다</b> —
        /// 그래도 계속 넘기는 이유는 그것이 실제 호출부의 모습이기 때문이고, 무시된다는 사실 자체를
        /// <see cref="카드_획은_인계본_비율이고_부르는_쪽_값에_좌우되지_않는다"/>가 잰다.</summary>
        private const float CallerStroke = 1.7f * (IconSize / 40f);

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

                    bool built = AccessoryCardIcon.TryBuild(root, slot, i, IconSize, CallerStroke,
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

                    bool built = AccessoryCardIcon.TryBuild(root, slot, i, IconSize, CallerStroke,
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
                IconSize, CallerStroke, entry.PrimaryColor, entry.SecondaryColor));

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

        /// <summary>그림이 카드 상자를 넘지 않는다.
        /// <para>★ 2026-09-06 — 여유가 <b>줄었다</b>. 봉투 맞춤 시절에는 도형이 상자의 0.86을 채우고 획이 그 밖으로
        /// 삐져나갔지만, 지금은 슬롯 고정 배율 + 넘침 보정(<c>AccessoryCardIcon.BoxFit</c>)이 <b>점 좌표를</b>
        /// 상자 안으로 넣는다. 획은 선분 <b>중점</b>에 놓이므로 점들의 볼록껍질 안, 즉 상자 안이다.
        /// 그래서 한계가 「반상자 + 획 하나」가 아니라 <b>반상자</b>다 — 이 검사가 그만큼 세졌다.</para></summary>
        [Test]
        public void 카드_그림이_상자를_넘지_않는다()
        {
            // 여유는 부동소수 잡음 몫뿐이다(넘침 보정과 그리기가 같은 값을 다른 순서로 곱한다).
            float limit = IconSize * 0.5f + 0.01f;
            for (int s = 0; s < BodySlots.Length; s++)
            {
                EquipmentSlot slot = BodySlots[s];
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    RectTransform root = NewRoot();
                    Assert.IsTrue(AccessoryCardIcon.TryBuild(root, slot, i, IconSize, CallerStroke,
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

        // ============================================================================
        // ★ 2026-09-05 계약 v2 — 카드 변형이 있는 아이템은 인계본 프레이밍·획·색 문법으로 그려진다
        // ============================================================================

        private static List<AccessoryShapeBuilder.Shape> CardShapes(EquipmentSlot slot, int item)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, AccessoryCardIcon.CardRig(),
                float.PositiveInfinity, 0f, false, AccessorySurface.Card);
            return sink;
        }

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f && Mathf.Abs(a.b - b.b) < 1e-3f;

        /// <summary>인계본 카드의 정체는 「밝은 잉크 윤곽 + 어두운 워시 채움」이다(§13-3-1). 옛 카드는 채움×0.28 의
        /// 어두운 윤곽이라 값 관계가 거꾸로였다 — 설계된 카드에서는 그 옛 윤곽색이 <b>한 조각도</b> 없어야 한다.</summary>
        [Test]
        public void 카드_변형이_있는_아이템은_잉크_윤곽과_워시_채움으로_그려진다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap);
            RectTransform root = NewRoot();
            Assert.IsTrue(AccessoryCardIcon.TryBuild(root, EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap,
                IconSize, CallerStroke, entry.PrimaryColor, entry.SecondaryColor));

            List<AccessoryShapeBuilder.Shape> shapes = CardShapes(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap);
            Assert.IsTrue(AccessoryCardIcon.HasDesignedCard(shapes), "야구모자에 카드 변형이 없습니다 — 아래 대조가 공허합니다.");

            int filledPieces = 0;
            int highlightIndex = -1;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Filled) filledPieces++;
                if (shapes[i].Tone == AccessoryTone.Highlight) highlightIndex = i;
            }
            Assert.AreEqual(filledPieces, root.GetComponentsInChildren<AccessoryFillGraphic>(true).Length,
                "채움 면 수가 카드 변형의 채움 조각 수와 다릅니다.");
            Assert.GreaterOrEqual(highlightIndex, 0, "야구모자 카드에 하이라이트 조각이 없습니다(인계본 H3).");

            // 하이라이트의 기대색을 계약 문장 그대로 다시 적는다: 밑 조각의 <b>재질색 M 불투명</b>(팔레트 R-1) 위 흰 42%.
            // M 은 카탈로그 주색(entry.PrimaryColor) — 카드·몸이 같은 hex 원천이고 등급색은 조각에 0개다(리더 판정 L-1).
            AccessoryShapeBuilder.Shape hl = shapes[highlightIndex];
            Assert.Greater(hl.UnderBack, 0, "야구모자 하이라이트는 관(B0) 위에 얹힌다 — underBack 이 0 입니다.");
            AccessoryShapeBuilder.Shape under = shapes[highlightIndex - hl.UnderBack];
            Assert.IsTrue(under.Filled);
            Assert.AreEqual(AccessoryTone.Primary, under.Tone, "야구모자 관(B0)은 재질색 M 채움이어야 이 대조가 뜻을 갖습니다(팔레트 §5).");
            Assert.AreEqual(1f, under.Alpha, 1e-6f, "재질색 채움은 불투명(α 1.0)입니다 — 워시가 남아 있습니다(팔레트 R-1).");
            Color rarity = UiChrome.RarityColor(ItemCatalog.Rarity(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap));
            Assert.IsFalse(Same(rarity, entry.PrimaryColor), "등급색이 아이템 주색과 같으면 아래 대조가 두 원천을 못 가릅니다.");
            Color underFlat = Color.Lerp(UiChrome.CardSurfaceMuted, entry.PrimaryColor, under.Alpha);
            Color expectedHighlight = AccessoryTone.Highlighted(underFlat);

            Color oldOutline = AccessoryShapeBuilder.FillOutlineColor(entry.PrimaryColor);
            int inkStrokes = 0, oldStrokes = 0, highlightStrokes = 0;
            foreach (Image g in root.GetComponentsInChildren<Image>(true))
            {
                if (g is AccessoryFillGraphic) continue;
                if (Same(g.color, UiChrome.CardIconInk)) inkStrokes++;
                else if (Same(g.color, oldOutline)) oldStrokes++;
                else if (Same(g.color, expectedHighlight)) highlightStrokes++;
            }
            Assert.Greater(inkStrokes, 0, "설계된 카드에 잉크색 윤곽이 하나도 없습니다 — 카드가 옛 색 문법으로 그려집니다.");
            Assert.AreEqual(0, oldStrokes, "설계된 카드에 옛 윤곽색(채움×0.28)이 남아 있습니다 — 값 관계가 다시 거꾸로입니다.");
            Assert.Greater(highlightStrokes, 0, "하이라이트 획이 「밑 조각 워시 위 흰 42% 사전 합성」색으로 그려지지 않았습니다.");
        }

        /// <summary>
        /// ★ 2026-09-06 — <b>v1 조각도</b> 잉크 한 색으로 윤곽을 잡는다(감사 항목 3).
        ///
        /// <para>이 테스트는 <b>뒤집힌 것</b>이다. 2026-09-05까지 여기는 「폴백 카드는 옛 색 표(채움×0.28)를 쓴다」를
        /// <b>지키는</b> 대조군이었다. 그 「대조군」이 곧 사용자가 신고한 격차였다 — 조각마다 윤곽 색이 다르고
        /// 어두운 카드 바탕 위에 어두운 윤곽이라 형태가 서지 않았다. 그래서 이제 <b>같은 자리를 반대로</b> 잠근다:
        /// 잉크가 있어야 하고 옛 윤곽색은 한 조각도 없어야 한다.</para>
        ///
        /// <para>채움 <b>없는</b> 낱선은 여전히 재질색(M/M2)이다 — 인계본 문법(<c>HandoffLineBase</c>)도 같다.</para>
        ///
        /// <para>★★ <b>2026-09-06 R25d 전제 정정.</b> 이 자리는 원래 <i>"베레모는 조각 2개가 모두 채움이라
        /// 낱선이 섞이지 않아 이 대조가 깨끗하다"</i>는 전제 위에 <c>shapes.Count == filled</c>를 걸고 있었다.
        /// <b>그 전제는 죽었다</b> — R25 재저작으로 베레모는 <b>4조각(채움 2 · 낱선 2: 꼭지·하이라이트)</b>이고,
        /// HEAD 의 v1 아이템은 베레모·밀짚모자 둘뿐인데 둘 다 낱선을 가진다. 즉 <b>「전부 채움인 v1 아이템」은
        /// 이제 존재하지 않는다</b> — 표본을 바꿔서 되살릴 수 있는 전제가 아니다.</para>
        ///
        /// <para><b>그 단언이 실제로 막던 것</b>은 «낱선의 재질색이 우연히 옛 윤곽색과 같아 <c>oldStrokes</c>가
        /// 오탐하는 것»이고, 그건 <b>거짓 빨강</b> 쪽 위험이다(조각이 늘어도 <c>oldStrokes</c>는 줄지 않으므로
        /// 거짓 초록은 구조적으로 못 만든다). 그래서 전제를 <b>지우지 않고 좁혀</b> 같은 것을 직접 잰다:
        /// (1) 채움이 <b>실재</b>해야 하고(그래야 <c>inkStrokes &gt; 0</c>가 «채움 윤곽이 잉크»를 뜻한다),
        /// (2) 낱선이 쓰는 재질색 M/M2가 잉크·옛 윤곽색과 <b>구별</b>돼야 한다.
        /// (2)가 깨지면 아래 두 계수가 서로 다른 것을 세게 되므로 그 자리에서 멈춘다.</para>
        /// </summary>
        [Test]
        public void v1_조각도_잉크_한_색으로_윤곽을_잡는다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);
            List<AccessoryShapeBuilder.Shape> shapes = CardShapes(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);
            Assert.IsFalse(AccessoryCardIcon.HasDesignedCard(shapes),
                "베레모가 인계본 조각이 됐습니다 — 이 대조는 v1 조각을 봐야 뜻이 있습니다. 다른 v1 아이템으로 바꾸십시오.");

            int filled = 0, strokeOnly = 0;
            foreach (AccessoryShapeBuilder.Shape s in shapes)
            {
                if (s.Filled) filled++;
                else strokeOnly++;
            }
            Assert.Greater(filled, 0,
                $"베레모 조각 {shapes.Count}개 중 채움이 0개입니다 — 그러면 아래 「채움 윤곽이 잉크다」가 " +
                "<b>대상 없이</b> 통과합니다(이 저장소 거짓 통과 5번 형태).");
            Assert.AreEqual(shapes.Count, filled + strokeOnly, "조각 분류가 새고 있습니다.");

            Color oldPrimaryOutline = AccessoryShapeBuilder.FillOutlineColor(entry.PrimaryColor);
            Color oldSecondaryOutline = AccessoryShapeBuilder.FillOutlineColor(entry.SecondaryColor);
            Assert.IsFalse(Same(oldPrimaryOutline, UiChrome.CardIconInk),
                "옛 윤곽색이 잉크와 같습니다 — 아래 두 단언이 같은 것을 세게 됩니다.");

            // ★ 낱선 2개(꼭지·하이라이트)가 이 대조를 오염시키지 않는가 — 재질색이 두 기준색과 갈리는가.
            //   갈리지 않으면 아래 계수는 «채움 윤곽»이 아니라 «낱선»을 세게 된다.
            if (strokeOnly > 0)
            {
                Assert.IsFalse(Same(entry.PrimaryColor, UiChrome.CardIconInk),
                    $"낱선 {strokeOnly}개가 재질색 M으로 그려지는데 M이 잉크와 같습니다 — " +
                    "inkStrokes가 채움 윤곽이 아니라 낱선을 셉니다.");
                Assert.IsFalse(Same(entry.SecondaryColor, UiChrome.CardIconInk),
                    "재질색 M2가 잉크와 같습니다 — 위와 같은 오염입니다.");
                Assert.IsFalse(Same(entry.PrimaryColor, oldPrimaryOutline) || Same(entry.PrimaryColor, oldSecondaryOutline),
                    "재질색 M이 옛 윤곽색과 같습니다 — oldStrokes가 낱선 때문에 거짓 빨강을 냅니다.");
                Assert.IsFalse(Same(entry.SecondaryColor, oldPrimaryOutline) || Same(entry.SecondaryColor, oldSecondaryOutline),
                    "재질색 M2가 옛 윤곽색과 같습니다 — 위와 같은 오염입니다.");

                // 하이라이트 낱선은 M/M2 가 아니라 «밑 조각 위 흰 42%» 합성색이다(같은 파일의 야구모자
                // 대조가 쓰는 그 식). 그 색까지 잉크와 갈려야 위 계수가 오염되지 않는다.
                int hlChecked = 0;
                for (int i = 0; i < shapes.Count; i++)
                {
                    if (shapes[i].Tone != AccessoryTone.Highlight || shapes[i].UnderBack <= 0) continue;
                    AccessoryShapeBuilder.Shape under = shapes[i - shapes[i].UnderBack];
                    Color underFlat = Color.Lerp(UiChrome.CardSurfaceMuted,
                        under.Tone == AccessoryTone.Accent ? entry.SecondaryColor : entry.PrimaryColor, under.Alpha);
                    Assert.IsFalse(Same(AccessoryTone.Highlighted(underFlat), UiChrome.CardIconInk),
                        "하이라이트 합성색이 잉크와 같습니다 — inkStrokes가 채움 윤곽이 아니라 " +
                        "하이라이트 낱선을 셀 수 있습니다.");
                    hlChecked++;
                }
                Debug.Log($"[v1 잉크] 베레모 조각 {shapes.Count}개(채움 {filled} · 낱선 {strokeOnly}) — " +
                          $"낱선 오염 대조: M · M2 · 하이라이트 합성 {hlChecked}건 전부 잉크와 갈림.");
            }

            RectTransform root = NewRoot();
            Assert.IsTrue(AccessoryCardIcon.TryBuild(root, EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret,
                IconSize, CallerStroke, entry.PrimaryColor, entry.SecondaryColor));

            int inkStrokes = 0, oldStrokes = 0;
            foreach (Image g in root.GetComponentsInChildren<Image>(true))
            {
                if (g is AccessoryFillGraphic) continue;
                if (Same(g.color, UiChrome.CardIconInk)) inkStrokes++;
                if (Same(g.color, oldPrimaryOutline) || Same(g.color, oldSecondaryOutline)) oldStrokes++;
            }
            Assert.Greater(inkStrokes, 0,
                "v1 카드의 채움 윤곽이 잉크가 아닙니다 — 인계본 16종과 나머지 26종이 다시 다른 색 문법으로 그려집니다.");
            Assert.AreEqual(0, oldStrokes,
                "v1 카드에 옛 윤곽색(채움×0.28)이 남아 있습니다 — 어두운 바탕 위 어두운 윤곽이라 「선화」로 읽히지 않습니다.");
        }

        /// <summary>
        /// 카드는 봉투 맞춤이 아니라 <b>슬롯 고정 배율</b>로 놓인다 — 외알안경(폭 1.18R)이 선글라스(2.07R)만큼
        /// 부풀면 크기 관계가 깨진다(§13-4-2 #7). 획 중점의 x 범위가 프레임에서 유도한 값과 같은가로 잰다.
        /// </summary>
        [Test]
        public void 카드는_슬롯_고정_배율로_놓인다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle);
            RectTransform root = NewRoot();
            Assert.IsTrue(AccessoryCardIcon.TryBuild(root, EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle,
                IconSize, CallerStroke, entry.PrimaryColor, entry.SecondaryColor));

            Assert.IsTrue(AccessoryCardIcon.TryGetBoxFit(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle,
                out AccessoryCardIcon.BoxFit fit));
            Assert.IsTrue(fit.IsIdentity,
                "외알안경이 넘침 보정을 받습니다 — 그러면 아래 기대 폭이 슬롯 배율 그대로가 아닙니다.");

            Assert.IsTrue(AccessoryCardIcon.Frame.TryGetCardFrame(EquipmentSlot.Eyes, out float unitsPerR, out _));
            AccessoryShapeBuilder.Rig rig = AccessoryCardIcon.CardRig();
            float pxPerR = unitsPerR * (IconSize / AccessoryCardIcon.Frame.IconViewBox);

            // 프레임 기대: 조각 점들의 x 범위를 슬롯 배율로 옮긴 값.
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (AccessoryShapeBuilder.Shape s in CardShapes(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle))
            {
                foreach (Vector3 p in s.Points)
                {
                    float x = p.x / rig.HeadRadius * pxPerR;
                    float y = (p.y - rig.HeadCenterY) / rig.HeadRadius * pxPerR;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            float expectedWidth = maxX - minX;

            // ★ 대립 가설 — 2026-09-06에 폐기된 v1 「봉투 맞춤」(긴 변이 상자의 이 비율을 채운다). 프로덕션에는 없는
            //   값이라 여기 적는다: 이 검사가 두 방식을 <b>실제로</b> 가를 수 있는지 먼저 확인하기 위한 것이다.
            const float RetiredEnvelopeFillFraction = 0.86f;
            float envelopeWidth = IconSize * RetiredEnvelopeFillFraction
                * (expectedWidth / Mathf.Max(expectedWidth, maxY - minY));
            float tolerance = IconSize * AccessoryCardIcon.Frame.StrokeFraction * 2f + 1f;
            Assert.Greater(envelopeWidth - expectedWidth, tolerance,
                $"슬롯 배율 폭({expectedWidth:F1})과 봉투 맞춤 폭({envelopeWidth:F1})의 차이가 측정 허용오차" +
                $"({tolerance:F1})보다 작습니다 — 이 검사가 두 방식을 가를 수 없습니다. 더 작은 아이템으로 바꾸십시오.");

            // 실제 획(Image)들의 anchoredPosition x 범위 ≈ 기대 범위(선분 중점이라 한 획 반폭 안).
            float gotMin = float.MaxValue, gotMax = float.MinValue;
            foreach (Image g in root.GetComponentsInChildren<Image>(true))
            {
                if (g is AccessoryFillGraphic) continue;
                float x = g.rectTransform.anchoredPosition.x;
                gotMin = Mathf.Min(gotMin, x);
                gotMax = Mathf.Max(gotMax, x);
            }
            Assert.AreEqual(expectedWidth, gotMax - gotMin, tolerance,
                $"외알안경 카드의 폭이 슬롯 고정 배율({expectedWidth:F1})이 아니라 {gotMax - gotMin:F1}입니다 — " +
                "봉투 맞춤으로 부풀었거나 프레임이 틀렸습니다.");
        }

        // ============================================================================
        // ★ 2026-09-06 배관 통일 — 42종이 같은 프레임·같은 획을 탄다
        // ============================================================================

        /// <summary>
        /// 카드 배율은 <b>슬롯마다 하나</b>다. 넘치는 아이템만 예외이고, 그 예외는 <b>줄이는 쪽으로만</b> 벗어난다.
        ///
        /// <para>봉투 맞춤 시절의 실측(아이콘 44px 기준 px/R): HEAD 8.92~15.51 · EYES 17.85~20.28 ·
        /// NECK 18.02~23.65 · BACK 9.36~11.68. 그래서 <b>카테고리 최대 챙</b>을 가진 밀짚모자가 화면에서
        /// 가장 작게 떴다 — 크기 관계가 거꾸로였다. 여기서 잠그는 것은 그 역전이 다시 생길 수 없다는 사실이다.</para>
        /// </summary>
        [Test]
        public void 카드_배율은_슬롯마다_하나이고_넘치는_아이템만_줄어든다()
        {
            int shrunk = 0, exact = 0;
            for (int s = 0; s < BodySlots.Length; s++)
            {
                EquipmentSlot slot = BodySlots[s];
                Assert.IsTrue(AccessoryCardIcon.Frame.TryGetCardFrame(slot, out _, out _),
                    $"{slot}: 카드 프레임이 없습니다 — 그 자리는 카드가 옛 아이콘으로 폴백합니다.");

                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    Assert.IsTrue(AccessoryCardIcon.TryGetBoxFit(slot, i, out AccessoryCardIcon.BoxFit fit),
                        $"{slot} {i}번의 프레이밍을 잴 수 없습니다.");
                    Assert.LessOrEqual(fit.Shrink, 1f + 1e-5f,
                        $"{slot} {i}번의 넘침 보정이 <b>키우고</b> 있습니다({fit.Shrink:F4}) — 봉투 맞춤이 되살아났습니다.");
                    Assert.Greater(fit.Shrink, 0f, $"{slot} {i}번의 축소율이 0 이하입니다.");
                    if (fit.IsIdentity) exact++; else shrunk++;
                }
            }
            Assert.Greater(exact, 0, "슬롯 배율 그대로인 아이템이 하나도 없습니다 — 「고정 배율」이 이름뿐입니다.");
            Debug.Log($"[카드프레이밍] 슬롯 배율 그대로 {exact}종 · 넘침 축소 {shrunk}종.");
        }

        /// <summary>
        /// ★ <b>인계본 16종 무회귀의 구조적 근거</b> — 새로 생긴 넘침 보정이 그 16종에서는 <b>항등</b>이다.
        /// 인계본 조각은 이미 64 viewBox 안에서 저작됐고(그 사실은 <c>CardShapeContractTests.카드_조각은_슬롯_프레임_안에_든다</c>가
        /// 데이터 쪽에서 잠근다), 여기서는 <b>계산</b>이 그 데이터에서 실제로 항등을 내는지를 잰다.
        /// 두 검사가 함께 있어야 「데이터가 상자 안이다」와 「그래서 그림이 안 움직인다」가 둘 다 남는다.
        /// </summary>
        [Test]
        public void 인계본_열여섯_종은_넘침_보정이_항등이다()
        {
            int handoffItems = 0;
            for (int s = 0; s < BodySlots.Length; s++)
            {
                EquipmentSlot slot = BodySlots[s];
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    if (!AccessoryCardIcon.HasDesignedCard(CardShapes(slot, i))) continue;
                    handoffItems++;
                    Assert.IsTrue(AccessoryCardIcon.TryGetBoxFit(slot, i, out AccessoryCardIcon.BoxFit fit));
                    Assert.IsTrue(fit.IsIdentity,
                        $"{slot} {i}번(인계본)이 넘침 보정을 받습니다 — 축소 {fit.Shrink:F4} · 이동 " +
                        $"({fit.OffsetXInUnits:F2}, {fit.OffsetYInUnits:F2})u. 인계본 조각이 슬롯 상자를 벗어났다는 뜻이고, " +
                        "이 라운드의 배관 변경이 인계본 16종을 <b>움직였다</b>는 뜻입니다.");
                }
            }
            Assert.AreEqual(16, handoffItems,
                "인계본 아이템이 16종이 아닙니다 — 위 항등 단언이 무엇을 봤는지 알 수 없습니다.");
        }

        /// <summary>
        /// ★ 카드 획은 인계본 비율(<c>2.2/64</c>) × 조각 배수 하나이고, <b>부르는 쪽이 준 값에 좌우되지 않는다</b>.
        ///
        /// <para>옛 v1 획은 부르는 쪽의 <c>1.7 × size/40</c>(상자의 4.25%)이라 인계본보다 +23.6% 굵었다.
        /// 지금 그 인자는 <b>무시된다</b> — 무시된다는 사실을 재지 않으면 누군가 다시 쓰기 시작해도 아무도 모른다
        /// (죽은 인자가 조용히 되살아나는 형태).</para>
        /// </summary>
        [Test]
        public void 카드_획은_인계본_비율이고_부르는_쪽_값에_좌우되지_않는다()
        {
            const float absurdStroke = 37f;    // 부르는 쪽이 무엇을 주든 그림이 같아야 한다.
            Assert.AreNotEqual(CallerStroke, absurdStroke, "두 입력이 같으면 아래 대조가 공허합니다.");

            for (int s = 0; s < BodySlots.Length; s++)
            {
                EquipmentSlot slot = BodySlots[s];
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    List<float> a = StrokeThicknesses(slot, i, entry, CallerStroke);
                    List<float> b = StrokeThicknesses(slot, i, entry, absurdStroke);

                    Assert.AreEqual(a.Count, b.Count, $"{slot} {i}번: 부르는 쪽 값에 따라 획 개수가 달라집니다.");
                    Assert.Greater(a.Count, 0, $"{slot} {i}번: 획이 하나도 없습니다.");
                    for (int k = 0; k < a.Count; k++)
                    {
                        Assert.AreEqual(a[k], b[k], 1e-3f,
                            $"{slot} {i}번의 {k}번 획이 부르는 쪽 값에 끌려갑니다({a[k]:F3} vs {b[k]:F3}) — " +
                            "죽은 인자가 되살아났습니다.");
                    }

                    // 실제 값도 인계본 비율에서 나와야 한다. 배수는 조각이 선언한 것 그대로다.
                    // ★ 2026-09-06 정정 — "v1 조각은 전부 ×1.0"은 <b>더 이상 참이 아니다</b>. R24 §14-14-5 가
                    //   낱선 5개에 배수를 배정했다: 안대 `PatchStrap` ×1.15 · 펜던트 `Chain` ×0.80(에셋) ·
                    //   판초 `CapeFold`/`CapeFold2` ×0.70 · 요정날개 `WingSpine` ×1.30.
                    //   허용 폭을 조각에서 <b>직접 굽기 때문에</b> 이 검사는 그대로 유효하고, 아래 「옛 v1 획
                    //   잔존」 대조도 살아 있다 — 배정된 다섯 값 중 1.2364(옛/새 비)는 하나도 없다.
                    // ★ 개수로 대조하지 않는다 — 닫힌 고리의 마지막 점이 첫 점과 같은 조각이 있어(골든 실측)
                    //   길이 0 선분은 그려지지 않는다. 개수를 박으면 그 사실 하나에 이 검사가 끌려다닌다.
                    var allowed = new List<float>();
                    foreach (AccessoryShapeBuilder.Shape shape in CardShapes(slot, i))
                    {
                        if (shape.NoStroke) continue;
                        allowed.Add(IconSize * AccessoryCardIcon.Frame.StrokeFraction
                            * AccessoryStroke.Multiplier(shape.StrokeMult));
                    }
                    Assert.IsNotEmpty(allowed, $"{slot} {i}번: 선을 가진 조각이 없습니다.");

                    for (int k = 0; k < a.Count; k++)
                    {
                        bool known = false;
                        for (int w = 0; w < allowed.Count && !known; w++) known = Mathf.Abs(allowed[w] - a[k]) < 1e-3f;
                        Assert.IsTrue(known,
                            $"{slot} {i}번의 획 두께 {a[k]:F3}이 조각이 선언한 어떤 값(인계본 비율 × 배수)과도 다릅니다.");

                        // ★ 옛 v1 획(부르는 쪽의 1.7 × size/40)이 남아 있지 않은가 — 감사 항목 1의 직접 대조.
                        //   어떤 인계본 배수도 이 값을 만들지 않는다(옛/새 비 = 1.2364, 배수 표에 없다).
                        Assert.Greater(Mathf.Abs(CallerStroke - a[k]), 1e-3f,
                            $"{slot} {i}번의 획이 옛 v1 두께({CallerStroke:F3})입니다 — 상자의 4.25%로 " +
                            "인계본(3.4375%)보다 +23.6% 굵습니다.");
                    }
                }
            }
        }

        /// <summary>그려진 획들의 <b>실제 두께</b>(오름차순). <c>UiChrome.AddStroke</c>가 사각형을 알파 램프만큼
        /// 부풀리므로 그 몫을 빼서 되돌린다 — 두께를 여기 숫자로 베끼지 않기 위해서다.</summary>
        private List<float> StrokeThicknesses(EquipmentSlot slot, int item, ItemCatalogEntry entry, float callerStroke)
        {
            RectTransform root = NewRoot();
            Assert.IsTrue(AccessoryCardIcon.TryBuild(root, slot, item, IconSize, callerStroke,
                entry.PrimaryColor, entry.SecondaryColor), $"{slot} {item}번이 그려지지 않았습니다.");

            var widths = new List<float>();
            foreach (Image g in root.GetComponentsInChildren<Image>(true))
            {
                if (g is AccessoryFillGraphic) continue;
                widths.Add(g.rectTransform.sizeDelta.y - UiChrome.EdgeFeatherPoints * 2f);
            }
            widths.Sort();
            return widths;
        }

        /// <summary>카드는 <b>단품</b>이다 — 지금 쓴 모자에 따라 머리카락이 잘리면 카드가 상태에 끌려간다.</summary>
        [Test]
        public void 카드_머리카락은_모자_상태에_영향받지_않는다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly);
            RectTransform root = NewRoot();
            Assert.IsTrue(AccessoryCardIcon.TryBuild(root, EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly,
                IconSize, CallerStroke, entry.PrimaryColor, entry.SecondaryColor));

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
