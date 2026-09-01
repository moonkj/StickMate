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
    ///
    /// ============================================================================
    /// ★ 2026-09-01 정정 — 이 문서가 <b>구현과 달랐다</b>
    /// ============================================================================
    /// 위 문장은 선언이었을 뿐, 베레모·밀짚모자 검사는 실제로 <b>폴백을 폴백과</b> 대조하면서
    /// 점 수만 상수(2점)로 박아 두고 있었다. 그 사이 몸의 <c>BeretRim</c>이 3점이 되어,
    /// 이 파일이 <b>장비 담당의 수정을 막는</b> 상태가 됐다(리더 판정: "실패한 테스트가 아니라
    /// 거짓말하는 테스트"). 그래서 두 가지를 바꿨다.
    /// <list type="bullet">
    ///   <item>점 수 기대값은 이제 <b>몸에서 읽는다</b>(상수를 적지 않는다).</item>
    ///   <item>폴백 조각끼리 보는 검사는 이름에 "폴백 내부"임을 드러낸다 — 그 자체는 유용하지만
    ///         <b>몸 대조가 아니다</b>. 이름이 그렇게 읽히면 다시 같은 착각이 생긴다.</item>
    /// </list>
    /// <para>30종 전수 몸 대조와 <b>빚 대장</b>은 <see cref="AccessoryFallbackBodyParityTests"/>에 있다.
    /// 이 파일은 아이템별 <b>세부</b> 규약(매달림·종횡비·삭제된 조각)을 계속 맡는다.</para>
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

        /// <summary>닫는 점을 뺀 <b>마지막 실재 점</b>. 채운 다각형(<see cref="ItemIconPartKind.Polygon"/>)은
        /// 마지막 점이 첫 점의 되풀이라, 그냥 <see cref="Last"/>를 쓰면 "밑변의 앞 끝"이 아니라
        /// <b>뒤 끝</b>이 잡힌다(2026-09-02 유도 폴백 전환에서 실제로 걸린 자리).</summary>
        private static Vector2 LastDistinct(ItemIconPart part)
        {
            Vector2 last = Last(part);
            if (!Mathf.Approximately(last.x, part.Values[0]) || !Mathf.Approximately(last.y, part.Values[1]))
            {
                return last;
            }
            int n = part.PointCount;
            return new Vector2(part.Values[(n - 2) * 2], part.Values[(n - 2) * 2 + 1]);
        }

        /// <summary>몸 도형 <paramref name="shapeName"/>에 대응하는 폴백 조각. 폴백은 몸 도형 순서를
        /// 그대로 옮기되 <b>그늘(Shade)만 빠지므로</b>, 그늘을 뺀 목록에서의 자리가 곧 폴백의 자리다.
        /// <para>★ 예전에는 <c>icon[0]</c>을 "관"이라고 가정했다. 유도 폴백에서는 0번이 <b>챙</b>이라
        /// 그 가정이 조용히 다른 조각을 재게 된다.</para></summary>
        private static ItemIconPart Piece(EquipmentSlot slot, int item, string shapeName)
        {
            List<AccessoryShapeBuilder.Shape> body =
                AccessorySilhouetteMetrics.Build(AccessorySilhouetteMetrics.Rig(), slot, item);
            ItemIconPart[] icon = Icon(slot, item);

            int index = 0;
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Tone == AccessoryShapeBuilder.Shade) continue;
                if (body[i].Name == shapeName)
                {
                    Assert.Less(index, icon.Length,
                        $"몸의 '{shapeName}'이 폴백 {index}번이어야 하는데 폴백 조각이 {icon.Length}개뿐입니다.");
                    return icon[index];
                }
                index++;
            }
            Assert.Fail($"몸 도형에 '{shapeName}'이 없습니다 — 이름이 바뀌었다면 이 검사도 함께 갱신하십시오.");
            return default;
        }

        private static void AssertInsideViewBox(ItemIconPart part, string label)
        {
            int coords = part.HasPoints ? part.Values.Length : 2;
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
            ItemIconPart crown = Piece(EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw, "StrawCrown");
            ItemIconPart band = Accent(icon);

            // ★ 기대값을 숫자로 적지 않는다 — 몸의 StrawBand가 몇 점인지 지금 읽어서 쓴다.
            //   (베레모에서 이 자리에 2를 박아 둔 것이 몸의 수정을 막고 있었다.)
            int bodyBandPoints = AccessorySilhouetteMetrics.Find(
                AccessorySilhouetteMetrics.Build(AccessorySilhouetteMetrics.Rig(),
                    EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw), "StrawBand").Points.Length;

            Assert.AreEqual(bodyBandPoints, band.PointCount,
                $"폴백 띠가 {band.PointCount}점인데 몸의 StrawBand는 {bodyBandPoints}점입니다 — " +
                "폴백은 몸과 같은 계열이어야 합니다(몸: crownBackFoot·crownFrontFoot).");
            Assert.AreEqual(First(crown), First(band), "띠의 뒤 끝이 관 밑변의 뒤 끝과 다릅니다.");
            Assert.AreEqual(LastDistinct(crown), Last(band), "띠의 앞 끝이 관 밑변의 앞 끝과 다릅니다.");
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
        // 2. 베레모 — 폴백 내부 정합성 + ★ 몸에서 유도한 점 수 판정
        // ============================================================================

        /// <summary>폴백 <b>안에서</b>의 정합성 — 테가 관의 양 끝에 붙어 있는가.
        /// <para>★ 2026-09-01: 이 검사는 폴백 조각끼리만 본다(몸을 읽지 않는다). 예전에는 이름이
        /// "관 밑변의 <b>두</b> 끝점"이라 <b>2점을 상수로 강제</b>했는데, 몸의 <c>BeretRim</c>은 이미
        /// 3점이라 그 단언이 <b>고치는 것을 막고 있었다</b>. 점 수 판정은 아래 몸 유도 검사로 옮겼다.</para></summary>
        [Test]
        public void 베레모_폴백_테는_관의_양_끝에_붙어_있다()
        {
            ItemIconPart[] icon = Icon(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);
            ItemIconPart crown = icon[0];
            ItemIconPart rim = Accent(icon);

            Assert.GreaterOrEqual(rim.PointCount, 2, "폴백 테가 선이 아닙니다.");

            // ★ 2026-09-02 — 예전에는 "테의 첫 점 = 관의 첫 점 / 테의 끝 점 = 관의 끝 점"으로 잠갔다.
            //   그건 손으로 그린 옛 폴백(관이 뒷발에서 앞발로 <b>열린</b> 선이던 시절)의 성질이지
            //   몸의 규약이 아니다. 몸의 BeretRim은 관 다각형의 [5][6][0]을 받으므로 <b>관의 마지막
            //   구간에서 시작해 첫 점에서 끝난다</b>. 잠글 것은 순서가 아니라 <b>"테의 모든 점이 곧
            //   관의 꼭짓점"</b>이라는 사실이다 — 그것이 "테 = 밑변 그 자체(간격 0)"의 정의다.
            for (int i = 0; i < rim.PointCount; i++)
            {
                var q = new Vector2(rim.Values[i * 2], rim.Values[i * 2 + 1]);
                bool onCrown = false;
                for (int k = 0; k < crown.PointCount && !onCrown; k++)
                {
                    onCrown = Mathf.Approximately(q.x, crown.Values[k * 2])
                           && Mathf.Approximately(q.y, crown.Values[k * 2 + 1]);
                }
                Assert.IsTrue(onCrown,
                    $"폴백 테의 {i}번 점 {q}이 관 폴리라인의 꼭짓점이 아닙니다 — 몸에서는 BeretRim이 " +
                    "관의 밑변 점들을 <b>그대로</b> 받습니다(좌표를 새로 적으면 어긋날 자리가 생깁니다).");
            }
            AssertInsideViewBox(rim, "베레모 폴백 테");
        }

        /// <summary>
        /// ★ <b>몸에서 유도</b>하는 점 수 판정 — 이 파일의 문서가 원래 선언한 방식이다.
        /// <para>지금은 몸 3점(<c>frontFoot → innerFoot → backTip</c>) / 폴백 2점으로 갈라져 있고,
        /// 그 빚은 <see cref="AccessoryFallbackBodyParityTests"/>의 대장에 실측값과 함께 등재돼 있다
        /// (폴백 에셋을 다시 굽는 것은 장비 담당 소유라 테스트가 대신 고치지 않는다).</para>
        /// <para>여기서 잠그는 것은 <b>방향</b>이다: 폴백은 몸의 단순화이므로 몸보다 복잡해질 수 없다.
        /// 장비 담당이 폴백에 <c>innerFoot</c>을 추가하면 이 검사는 그대로 통과한다 — 옛 단언과 달리
        /// <b>고치는 길을 막지 않는다</b>.</para>
        /// </summary>
        [Test]
        public void 베레모_폴백_테는_몸의_BeretRim보다_복잡하지_않다()
        {
            List<AccessoryShapeBuilder.Shape> body = AccessorySilhouetteMetrics.Build(
                AccessorySilhouetteMetrics.Rig(), EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);
            AccessoryShapeBuilder.Shape bodyRim = AccessorySilhouetteMetrics.Find(body, "BeretRim");
            ItemIconPart fallbackRim = Accent(Icon(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret));

            Assert.LessOrEqual(fallbackRim.PointCount, bodyRim.Points.Length,
                $"폴백 테가 {fallbackRim.PointCount}점인데 몸의 BeretRim은 {bodyRim.Points.Length}점입니다 — " +
                "폴백이 몸보다 <b>복잡합니다</b>. 폴백은 40×40 격자의 단순화이므로 이 방향은 " +
                "단순화가 아니라 다른 그림입니다.");

            if (fallbackRim.PointCount != bodyRim.Points.Length)
            {
                Debug.Log($"[폴백-몸] 베레모 테: 몸 {bodyRim.Points.Length}점 / 폴백 {fallbackRim.PointCount}점 " +
                          "— 아직 갈라져 있습니다(AccessoryFallbackBodyParityTests.Ledger에 등재됨).");
            }
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
            ItemIconPart chain = Piece(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "Collar");
            ItemIconPart bell = Accent(icon);

            // ★ 2026-09-02 — 옛 폴백은 <b>Dot</b>(채운 원)이었다. 그때는 그것이 최선이었다:
            //   폴백 형식에 <b>채운 다각형이 없어서</b> 몸의 filled 다각형을 흉내낼 수단이 원뿐이었다.
            //   Polygon이 생긴 지금은 몸과 <b>같은 10각형</b>을 같은 좌표로 그린다 — 흉내가 아니라 그 도형이다.
            Assert.AreEqual(ItemIconPartKind.Polygon, bell.Kind,
                "폴백 방울이 채운 도형이 아닙니다 — 몸의 방울은 filled 다각형이라 " +
                "윤곽선으로만 그리면 카드만 '속이 빈 고리'가 됩니다(규칙 2).");

            float chainLowY = float.MinValue;   // 카드 격자는 y가 아래로 자란다
            for (int i = 1; i < chain.Values.Length; i += 2)
            {
                chainLowY = Mathf.Max(chainLowY, chain.Values[i]);
            }

            float bellTopY = float.MaxValue, bellLowY = float.MinValue;
            for (int i = 1; i < bell.Values.Length; i += 2)
            {
                bellTopY = Mathf.Min(bellTopY, bell.Values[i]);
                bellLowY = Mathf.Max(bellLowY, bell.Values[i]);
            }

            Assert.AreEqual(chainLowY, bellTopY, 1e-4f,
                $"방울의 위 끝이 y={bellTopY}인데 줄 최저점은 y={chainLowY}입니다 — 매달린 지점이 " +
                "보여야 물건이 공중에 뜨지 않습니다(37-6 규칙 4). 몸은 이미 CollarLowLocalY에서 유도합니다.");

            AssertInsideViewBox(bell, "방울 폴백");
            Assert.LessOrEqual(bellLowY, ViewBox, "방울 아래 끝이 격자를 넘습니다.");
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

            // ---- 이 축은 언제나 살아 있다: 원과 갈리는가 ----
            Assert.GreaterOrEqual(cardAspect, 2f,
                $"폴백 마름모가 {cardAspect:F2}배로는 원과 갈리지 않습니다.");
            AssertInsideViewBox(diamond, "펜던트 폴백 마름모");

            // ★ 2026-09-02 — 빚이 갚혔다. 폴백을 <b>몸에서 유도</b>해 다시 구웠으므로 종횡비가
            //   몸과 같은 값(0.64/0.30 = 2.1333)이 됐다. 유예 블록이 스스로 지시한 대로,
            //   그 블록을 지우고 이 한 줄로 되돌린다.
            Assert.AreEqual(bodyAspect, cardAspect, 0.01f,
                $"폴백 마름모 종횡비 {cardAspect:F4} vs 몸 {bodyAspect:F4}. 몸 상수" +
                "(PendantHalfHeightRatio / PendantHalfWidthRatio)가 움직였다면 폴백도 다시 구워야 합니다 — " +
                "옛 사고: 몸이 0.28->0.30 / 0.62->0.64로 가고 폴백만 2.2143에 남았습니다.");
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
