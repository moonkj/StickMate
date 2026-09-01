using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 카드 <b>폴백</b> 아이콘이 몸 도형과 같은 인상인가 — 2026-09-01 마지막 정리 라운드.
    ///
    /// ============================================================================
    /// 폴백은 안 나오는데 왜 고치는가
    /// ============================================================================
    /// 2026-09-01 P0-a에서 카드가 <see cref="AccessoryCardIcon"/>(= <b>몸과 같은 도형</b>)으로 갈아탔고,
    /// <c>Resources/Items/*.asset</c>의 40×40 SVG는 <c>TryBuild</c>가 <b>실패할 때만</b> 쓰는 폴백으로
    /// 남았다. 장비 카테고리에서는 실패하지 않는다(<see cref="AccessoryCardIconTests"/>가 30종 전부에
    /// 대해 그것을 잠근다).
    ///
    /// 그럼에도 리더가 (a) "넷 다 맞춤"을 택한 이유는 <b>안전망이 틀린 그림이면 안전망이 아니기</b>
    /// 때문이다. 특히 방울 폴백에는 <b>2026-09-01에 삭제된 추(clapper)</b>가 남아 있었다 —
    /// 폴백이 뜨는 순간 <b>존재하지 않는 아이템</b>이 그려진다. 폴백 삭제(c)는 기각됐다.
    ///
    /// ============================================================================
    /// 무엇을 맞췄나 (중절모가 앞 라운드에 세운 선례를 그대로 따른다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>밀짚모자</b> — 띠가 관 밑변보다 3px 위(y=20 vs 23)에 떠 있었다.
    ///         몸에서는 띠가 관 밑변의 두 끝점 <b>그 자체</b>다 → 폴백도 그렇게.</item>
    ///   <item><b>베레모</b> — 테가 <c>7,23 → 31,23</c>인데 관 밑변 끝은 <c>8,23</c>/<c>30,22</c>였다
    ///         (양끝 2px씩, 그리고 기울기까지 어긋났다) → 끝점을 그대로 받는다.</item>
    ///   <item><b>방울</b> — <b>삭제된 추</b>가 남아 있었고, 공은 반원 꺾은선이라 줄에서 3px 떠 있었다
    ///         → 줄 최저점에 붙는 <b>채운 원</b> 하나(몸의 <c>filled: true</c> 다각형과 같은 인상).</item>
    ///   <item><b>펜던트</b> — 마름모 종횡비 1.13인데 몸은 2.21이었다(그 세로 비율이 재설계의 요지였고,
    ///         "원과 갈리는 것은 크기가 아니라 종횡비"라는 것이 그 라운드의 결론이다) → 몸과 같은 비율로.</item>
    /// </list>
    ///
    /// <b>비교는 언제나 "몸이 지금 뭐라고 말하는가"에 건다.</b> 폴백에 숫자를 새로 적어 두면
    /// 몸이 다시 바뀌는 날 폴백만 옛 그림으로 남는다 — 그게 이번에 넷이 한꺼번에 낡은 이유다.
    /// </summary>
    public sealed class AccessoryFallbackIconParityTests
    {
        /// <summary>카드 폴백 격자(<c>CharacterInfoWindow.FromViewBox</c>와 같은 값). y가 <b>아래로</b> 자란다.</summary>
        private const float ViewBox = 40f;

        private static ItemIconPart[] Icon(EquipmentSlot slot, int item)
        {
            ItemIconPart[] icon = ItemCatalog.Item(slot, item).Icon;
            Assert.IsNotNull(icon, $"{slot} {item}번의 폴백 아이콘이 사라졌습니다.");
            Assert.Greater(icon.Length, 0, $"{slot} {item}번의 폴백 아이콘이 비었습니다.");
            return icon;
        }

        private static ItemIconPart Accent(ItemIconPart[] icon)
        {
            for (int i = 0; i < icon.Length; i++)
            {
                if (icon[i].Tone == 1) return icon[i];
            }
            Assert.Fail("폴백 아이콘에 보조색 조각이 없습니다 — 그 조각이 아이템의 식별 특징입니다(규칙 3-2).");
            return default;
        }

        private static Vector2 First(ItemIconPart part) => new Vector2(part.Values[0], part.Values[1]);

        private static Vector2 Last(ItemIconPart part)
            => new Vector2(part.Values[part.Values.Length - 2], part.Values[part.Values.Length - 1]);

        private static void AssertInsideViewBox(ItemIconPart part, string label)
        {
            int coords = part.Kind == ItemIconPartKind.Polyline ? part.Values.Length : 2;
            for (int i = 0; i < coords; i++)
            {
                Assert.That(part.Values[i], Is.InRange(0f, ViewBox), $"{label}이 40×40 격자를 벗어났습니다.");
            }
        }

        // ============================================================================
        // 1. 밀짚모자 — 띠는 관 밑변 그 자체
        // ============================================================================

        [Test]
        public void 밀짚모자_폴백_띠는_관_밑변의_두_끝점이다()
        {
            ItemIconPart[] icon = Icon(EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw);
            ItemIconPart crown = icon[0];
            ItemIconPart band = Accent(icon);

            Assert.AreEqual(4, band.Values.Length,
                $"폴백 띠가 {band.Values.Length / 2}점입니다 — 관 밑변의 두 끝을 잇는 직선(2점)이어야 " +
                "몸(StrawBand = crownBackFoot·crownFrontFoot)과 같은 계열입니다.");
            Assert.AreEqual(First(crown), First(band), "띠의 뒤 끝이 관 밑변의 뒤 끝과 다릅니다.");
            Assert.AreEqual(Last(crown), Last(band), "띠의 앞 끝이 관 밑변의 앞 끝과 다릅니다.");
            AssertInsideViewBox(band, "밀짚모자 폴백 띠");
        }

        /// <summary>몸이 같은 규약을 <b>지금도</b> 지키는지 함께 본다 — 이게 깨지면 위 검사는
        /// "폴백이 몸을 따라간다"가 아니라 "폴백이 어떤 옛 그림을 따라간다"가 된다.</summary>
        [Test]
        public void 몸의_밀짚모자_띠도_관_밑변의_두_끝점이다()
        {
            AccessoryShapeBuilder.Rig rig = AccessorySilhouetteMetrics.Rig();
            List<AccessoryShapeBuilder.Shape> straw =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw);

            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(straw, "StrawCrown");
            AccessoryShapeBuilder.Shape band = AccessorySilhouetteMetrics.Find(straw, "StrawBand");

            Assert.AreEqual(2, band.Points.Length, "몸의 띠가 2점 직선이 아닙니다.");
            Assert.AreEqual(crown.Points[0], band.Points[0]);
            Assert.AreEqual(crown.Points[crown.Points.Length - 1], band.Points[1]);
        }

        // ============================================================================
        // 2. 베레모 — 테는 관 밑변의 두 끝점
        // ============================================================================

        [Test]
        public void 베레모_폴백_테는_관_밑변의_두_끝점이다()
        {
            ItemIconPart[] icon = Icon(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);
            ItemIconPart crown = icon[0];
            ItemIconPart rim = Accent(icon);

            Assert.AreEqual(4, rim.Values.Length, "폴백 테가 2점 직선이 아닙니다.");
            Assert.AreEqual(First(crown), First(rim),
                "테의 한쪽 끝이 관 폴리라인의 첫 점과 다릅니다 — 몸에서는 BeretRim이 " +
                "관의 두 끝점(frontTip·backTip)을 그대로 받습니다.");
            Assert.AreEqual(Last(crown), Last(rim), "테의 다른 끝이 관 폴리라인의 끝 점과 다릅니다.");
            AssertInsideViewBox(rim, "베레모 폴백 테");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 테는 관 밑변 <b>밖으로</b> 삐져나가 있었다.
        /// <para>옛 테 좌표와 <b>옛 관 밑변 끝점</b>을 둘 다 박제한다(한쪽만 얼리면 훗날 관이 움직였을 때
        /// 존재한 적 없는 쌍을 재게 된다 — 2026-09-01 펜던트 컨트롤의 교훈). 위 검사가 살아 있는 관이
        /// 아직 그 좌표인지 따로 확인하므로, 관이 바뀌면 컨트롤이 조용해지는 대신 그쪽이 빨개진다.</para></summary>
        [Test]
        public void 컨트롤_옛_베레모_폴백_테는_관_밖으로_나가_있었다()
        {
            var oldRim = new[] { new Vector2(7f, 23f), new Vector2(31f, 23f) };
            var oldCrownFoot = new[] { new Vector2(8f, 23f), new Vector2(30f, 22f) };

            Assert.Greater(oldCrownFoot[0].x - oldRim[0].x, 0.5f,
                "옛 테의 뒤 끝이 관 밑변보다 바깥이 아니었다고 나옵니다 — 기록은 1px 바깥입니다.");
            Assert.Greater(oldRim[1].x - oldCrownFoot[1].x, 0.5f,
                "옛 테의 앞 끝이 관 밑변보다 바깥이 아니었다고 나옵니다 — 기록은 1px 바깥입니다.");
            Assert.AreNotEqual(oldCrownFoot[1].y, oldRim[1].y,
                "옛 테는 수평선이고 관 앞 끝은 y=22였습니다 — 기울기까지 어긋나 있었다는 것이 " +
                "이 컨트롤이 재현하려는 사실입니다.");

            ItemIconPart rim = Accent(Icon(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret));
            Assert.AreNotEqual(oldRim[0], First(rim), "지금 폴백 테가 옛 좌표 그대로입니다 — 수정이 반영되지 않았습니다.");
        }

        // ============================================================================
        // 3. 방울 — 삭제된 추가 남아 있었다
        // ============================================================================

        [Test]
        public void 방울_폴백에는_추가_없다()
        {
            ItemIconPart[] icon = Icon(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell);

            List<AccessoryShapeBuilder.Shape> body = AccessorySilhouetteMetrics.Build(
                AccessorySilhouetteMetrics.Rig(), EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell);

            Assert.AreEqual(body.Count, icon.Length,
                $"몸은 도형 {body.Count}개인데 폴백은 조각 {icon.Length}개입니다 — 2026-09-01에 " +
                "추(BellClapper)가 몸에서 삭제됐고(잉크 사각형 0.29획), 폴백에만 남아 있으면 " +
                "안전망이 <b>존재하지 않는 아이템</b>을 그립니다.");

            for (int i = 0; i < body.Count; i++)
            {
                Assert.AreNotEqual("BellClapper", body[i].Name,
                    "몸에 추가 되살아났습니다 — 그렇다면 이 검사의 전제가 바뀐 것이므로 함께 갱신해야 합니다.");
            }
        }

        /// <summary>방울은 몸에서 <b>채운</b> 다각형이고 줄 최저점에 정확히 매달린다.
        /// 폴백도 같은 인상이어야 한다 — 채운 원 하나가 줄 최저점에 접한다.</summary>
        [Test]
        public void 방울_폴백은_줄_최저점에_매달린_채운_원이다()
        {
            ItemIconPart[] icon = Icon(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell);
            ItemIconPart chain = icon[0];
            ItemIconPart bell = Accent(icon);

            Assert.AreEqual(ItemIconPartKind.Dot, bell.Kind,
                "폴백 방울이 채운 원(Dot)이 아닙니다 — 몸의 방울은 filled 다각형이라 " +
                "윤곽선으로 그리면 카드만 '속이 빈 고리'가 됩니다(규칙 2).");

            float chainLowY = float.MinValue;   // 카드 격자는 y가 아래로 자란다
            for (int i = 1; i < chain.Values.Length; i += 2)
            {
                chainLowY = Mathf.Max(chainLowY, chain.Values[i]);
            }

            float cy = bell.Values[1];
            float radius = bell.Values[2];
            Assert.AreEqual(chainLowY, cy - radius, 1e-4f,
                $"방울의 위 끝이 y={cy - radius}인데 줄 최저점은 y={chainLowY}입니다 — 매달린 지점이 " +
                "보여야 물건이 공중에 뜨지 않습니다(37-6 규칙 4). 몸은 이미 CollarLowLocalY에서 유도합니다.");

            AssertInsideViewBox(bell, "방울 폴백");
            Assert.LessOrEqual(cy + radius, ViewBox, "방울 아래 끝이 격자를 넘습니다.");
        }

        // ============================================================================
        // 4. 펜던트 — 원과 갈리는 것은 크기가 아니라 종횡비
        // ============================================================================

        [Test]
        public void 펜던트_폴백_마름모의_종횡비가_몸과_같다()
        {
            ItemIconPart[] icon = Icon(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckPendant);
            ItemIconPart diamond = Accent(icon);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < diamond.Values.Length; i += 2)
            {
                minX = Mathf.Min(minX, diamond.Values[i]);
                maxX = Mathf.Max(maxX, diamond.Values[i]);
                minY = Mathf.Min(minY, diamond.Values[i + 1]);
                maxY = Mathf.Max(maxY, diamond.Values[i + 1]);
            }

            float cardAspect = (maxY - minY) / (maxX - minX);
            float bodyAspect = AccessoryShapeBuilder.PendantHalfHeightRatio
                               / AccessoryShapeBuilder.PendantHalfWidthRatio;

            Assert.AreEqual(bodyAspect, cardAspect, 0.01f,
                $"폴백 마름모의 종횡비가 {cardAspect:F2}인데 몸은 {bodyAspect:F2}입니다(옛 폴백 1.13). " +
                "이 카테고리에서 펜던트를 방울과 갈라 주는 것은 크기가 아니라 <b>세로 비율</b>이므로, " +
                "폴백이 1.1이면 카드에서는 두 아이템이 다시 '동그란 덩어리' 둘이 됩니다.");
            Assert.GreaterOrEqual(cardAspect, 2f,
                $"폴백 마름모가 {cardAspect:F2}배로는 원과 갈리지 않습니다.");
            AssertInsideViewBox(diamond, "펜던트 폴백 마름모");
        }

        /// <summary>펜던트는 몸에서 <b>목줄 최저점</b>에 매달린다(규칙 4-a). 폴백도 그렇다.</summary>
        [Test]
        public void 펜던트_폴백은_줄_최저점에_매달린다()
        {
            ItemIconPart[] icon = Icon(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckPendant);
            ItemIconPart chain = icon[0];
            ItemIconPart diamond = Accent(icon);

            float chainLowY = float.MinValue;
            for (int i = 1; i < chain.Values.Length; i += 2) chainLowY = Mathf.Max(chainLowY, chain.Values[i]);

            float diamondTopY = float.MaxValue;
            for (int i = 1; i < diamond.Values.Length; i += 2) diamondTopY = Mathf.Min(diamondTopY, diamond.Values[i]);

            Assert.AreEqual(chainLowY, diamondTopY, 1e-4f,
                "마름모의 위 꼭짓점이 줄 최저점과 어긋났습니다 — 장식이 가슴 앞 공중에 뜬 것으로 보입니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 폴백 마름모는 <b>실제로</b> 원에 가까웠다.
        /// <para>옛 좌표와 <b>옛 판정 기준</b>(종횡비 2.0 = 이 프로젝트가 "원이 아니다"로 정한 하한)을
        /// 둘 다 이 파일 안에 박제한다. 살아 있는 값은 하나도 읽지 않는다.</para></summary>
        [Test]
        public void 컨트롤_옛_펜던트_폴백은_거의_원이었다()
        {
            var old = new[]
            {
                new Vector2(20f, 22f), new Vector2(24f, 26f),
                new Vector2(20f, 31f), new Vector2(16f, 26f),
            };
            const float roundThreshold = 2f;   // 옛 라운드가 "원이 아니다"로 정한 하한

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < old.Length; i++)
            {
                minX = Mathf.Min(minX, old[i].x); maxX = Mathf.Max(maxX, old[i].x);
                minY = Mathf.Min(minY, old[i].y); maxY = Mathf.Max(maxY, old[i].y);
            }

            float oldAspect = (maxY - minY) / (maxX - minX);
            Assert.Less(oldAspect, roundThreshold,
                $"옛 폴백의 종횡비가 {oldAspect:F2}로 측정됩니다 — 기록은 1.13(하한 {roundThreshold:F1} 미달)입니다. " +
                "이 컨트롤이 재현하려는 결함 자체가 잘못 적혀 있습니다.");
            Assert.AreEqual(1.13f, oldAspect, 0.01f, "옛 종횡비가 기록된 1.13과 다릅니다.");
        }
    }
}
