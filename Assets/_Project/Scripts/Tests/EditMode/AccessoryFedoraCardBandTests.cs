using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 중절모 띠 — 몸과 카드가 <b>같은 계열</b>인가. 2026-09-01.
    ///
    /// ============================================================================
    /// 배정된 전제가 절반은 이미 참이었다 — 그 사실부터 잠근다
    /// ============================================================================
    /// 배정 내용은 "카드 아이콘의 띠가 관 밑변을 가로지르는 곡선이라 몸 도형과 다른 계열로 보인다"였다.
    /// 좌표는 실제로 그랬다(양 끝 y=22, 한가운데 y=23.5, 관 밑변 y=23 — <b>가로지른다</b>).
    /// 그런데 <b>그 좌표는 화면에 나오지 않는다</b>: 2026-09-01 P0-a에서 카드가
    /// <see cref="AccessoryCardIcon"/>(= 몸과 <b>같은</b> 도형)으로 갈아탔고, 40×40 SVG는
    /// <c>TryBuild</c>가 실패할 때만 쓰는 <b>폴백</b>으로 남았다. 장비 카테고리에서는 실패하지 않는다.
    ///
    /// 그래서 이 파일은 둘 다 잠근다.
    /// <list type="number">
    ///   <item><b>실제로 그려지는 경로</b>가 몸 도형이라는 것(이것이 깨지면 카드가 옛 SVG로 되돌아간다).</item>
    ///   <item>그럼에도 <b>폴백 좌표도 맞춰 두었다</b>는 것 — 폴백은 "몸 도형이 이상 상태일 때"의
    ///         안전망이고, 안전망이 틀린 그림이면 안전망이 아니다.</item>
    /// </list>
    ///
    /// <b>같은 계열</b>의 정의는 몸이 이미 정해 두었다(2026-09-01 띠 라운드):
    /// <b>띠는 좌표를 새로 적지 않고 관 밑변의 두 끝점을 그대로 받는다</b>. 카드 폴백도 같은 규약으로
    /// 바꿨다 — 관 폴리라인의 첫 점과 끝 점을 잇는 <b>직선</b>이다(옛 5점 곡선 -> 2점 직선).
    /// </summary>
    public sealed class AccessoryFedoraCardBandTests
    {
        /// <summary>카드 폴백 격자. <c>CharacterInfoWindow.FromViewBox</c>가 쓰는 값과 같다.</summary>
        private const float ViewBox = 40f;

        /// <summary>옛 폴백 띠(박제). 아래 네거티브 컨트롤 전용이며 <b>살아 있는 값을 읽지 않는다</b>.</summary>
        private static readonly float[] OldCardBand =
        {
            13f, 22f, 16.5f, 23.12f, 20f, 23.5f, 23.5f, 23.12f, 27f, 22f,
        };

        /// <summary>옛 폴백에서의 관 밑변 y(박제). 컨트롤이 재는 <b>쌍의 양쪽</b>을 다 얼린다 —
        /// 한쪽만 박제하면 훗날 관이 움직였을 때 "역사상 존재한 적 없는 쌍"을 재게 된다
        /// (2026-09-01 방울 라운드가 실제로 겪은 실패).
        /// <para>★ 2026-09-02부터 이 값은 <b>순수 역사</b>다 — 살아 있는 폴백은 몸에서 유도되고,
        /// 몸은 챙이 기울어 밑변 두 발의 y가 애초에 하나가 아니다. 그래서 위 검사는 더 이상
        /// 이 값을 살아 있는 좌표와 맞춰 보지 않는다(맞춰 보던 단언이 유도를 막고 있었다).</para></summary>
        private const float OldCardCrownBaseY = 23f;

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

        // ============================================================================
        // 1. 몸 — 띠가 관 밑변의 두 끝점을 <b>그대로</b> 받는다
        // ============================================================================

        /// <summary>
        /// ★★ 2026-09-03(스펙 14-1) — 띠가 <b>낱선에서 닫힌 채움 띠</b>가 됐다. 그래도 이 검사가
        /// 잠그는 사실은 바뀌지 않는다: <b>띠의 아랫변이 관 밑변의 두 끝점 그 자체</b>다.
        /// <para>옛 단언은 <c>Assert.AreEqual(2, band.Points.Length)</c>였고, 그것은 "좌표를 새로
        /// 적지 않는다"는 규약이 아니라 <b>그 규약이 그날 취했던 형태</b>였다. 그래서 형태가
        /// 바뀌자 이 파일이 <b>고치는 것을 막는 테스트</b>가 됐다 — 이 저장소가 베레모에서 이미 한 번
        /// 겪은 사고와 정확히 같은 형태다(<see cref="AccessoryFallbackBodyParityTests"/> 클래스 문서).
        /// 이제는 점 수를 적지 않고 <see cref="AccessoryFilledBandRuler.AssertRaisedBandForm"/>의
        /// 규약("아랫변 + 그 아랫변을 띠 두께만큼 올려 역순으로 이은 윗변")에서 유도한다.</para></summary>
        [Test]
        public void 몸의_중절모_띠는_관_밑변의_두_끝점_그_자체다()
        {
            AccessoryShapeBuilder.Rig rig = AccessorySilhouetteMetrics.Rig();
            List<AccessoryShapeBuilder.Shape> shapes =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora);

            AccessoryShapeBuilder.Shape band = AccessorySilhouetteMetrics.Find(shapes, "FedoraBand");
            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(shapes, "FedoraCrown");

            AccessoryFilledBandRuler.AssertRaisedBandForm(rig, band, "중절모 띠");

            Vector3[] bottom = AccessoryFilledBandRuler.BottomEdge(band);
            Assert.AreEqual(crown.Points[0], bottom[0],
                "띠 아랫변의 뒤 끝이 관 밑변의 뒤 끝과 다릅니다 — 좌표를 새로 적으면 어긋날 자리가 생깁니다.");
            Assert.AreEqual(crown.Points[crown.Points.Length - 1], bottom[bottom.Length - 1],
                "띠 아랫변의 앞 끝이 관 밑변의 앞 끝과 다릅니다.");

            for (int i = 0; i < bottom.Length; i++)
            {
                bool onCrown = false;
                for (int k = 0; k < crown.Points.Length && !onCrown; k++)
                {
                    onCrown = crown.Points[k] == bottom[i];
                }
                Assert.IsTrue(onCrown,
                    $"띠 아랫변의 {i}번 점 {bottom[i]}이 관 폴리라인의 꼭짓점이 아닙니다 — " +
                    "아랫변은 관에서 <b>그대로</b> 받아야 합니다(규칙 4-a).");
            }

            Assert.AreEqual(AccessoryShapeBuilder.Accent, band.Tone,
                "띠가 보조색이 아닙니다 — 중절모의 식별 특징이 사라집니다(규칙 3-2).");
        }

        // ============================================================================
        // 2. 카드 — 실제로 그려지는 것은 <b>몸 도형</b>이다
        // ============================================================================

        /// <summary>
        /// 카드가 폴백으로 새면 이 아이템만 옛 SVG로 남는다. 그 경로를 막는 검사는
        /// <c>AccessoryCardIconTests</c>가 전 카테고리로 이미 갖고 있고, 여기서는 <b>중절모</b>에 대해
        /// "그래서 배정된 결함이 화면에 나올 수 없다"를 이 파일 안에서 읽히게 못 박는다.
        /// </summary>
        [Test]
        public void 중절모_카드는_폴백이_아니라_몸_도형으로_그려진다()
        {
            var go = new GameObject("FedoraCard", typeof(RectTransform));
            _spawned.Add(go);
            var root = go.GetComponent<RectTransform>();

            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora);
            bool built = AccessoryCardIcon.TryBuild(root, EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora,
                40f, 2f, entry.PrimaryColor, entry.SecondaryColor);

            Assert.IsTrue(built,
                "중절모 카드가 몸 도형으로 그려지지 않았습니다 — 폴백(40×40 SVG)으로 새면 " +
                "카드와 몸의 띠가 다시 다른 그림이 됩니다.");
            Assert.Greater(root.childCount, 0, "중절모 카드 그림이 비었습니다.");
        }

        // ============================================================================
        // 3. 폴백 — 안전망도 같은 규약을 쓴다
        // ============================================================================

        [Test]
        public void 폴백_아이콘의_띠도_관_밑변_직선이다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora);
            Assert.IsNotNull(entry.Icon, "중절모의 폴백 아이콘이 사라졌습니다.");

            // ★ 2026-09-02 — 관은 폴백의 <b>0번이 아니다</b>(0번은 챙이다). 몸 도형 순서에서
            //   그늘을 뺀 자리가 곧 폴백의 자리이므로, 여기서 그 자리를 <b>유도</b>한다.
            //   예전에는 icon[0]을 관이라고 가정했고, 유도 폴백으로 바뀌자 챙을 재고 있었다.
            ItemIconPart crown = Piece(AccessoryShapeBuilder.HeadFedora, "FedoraCrown");

            // 관은 <b>채운 다각형</b>이라 마지막 점이 첫 점의 되풀이다 — 밑변의 앞 끝은
            // 마지막 <b>실재</b> 점이다.
            int crownPoints = crown.PointCount;
            float baseBackX = crown.Values[0];
            float baseBackY = crown.Values[1];
            float baseFrontX = crown.Values[(crownPoints - 2) * 2];
            float baseFrontY = crown.Values[(crownPoints - 2) * 2 + 1];

            // ★ 옛 검사는 "밑변 두 끝의 높이가 같다"와 "그 높이가 23이다"를 잠갔다. 둘 다
            //   <b>손으로 그린 옛 폴백</b>의 성질이지 몸의 규약이 아니다 — 몸은 챙이 앞뒤로
            //   기울어 있어 두 발의 y를 <b>일부러</b> 갈라 놓는다(도형 주석: "두 발의 y가
            //   커버선을 사이에 두고 갈린다"). 그래서 그 두 단언을 지우고, 몸이 실제로 그렇다는
            //   사실 쪽을 잠근다.
            Assert.AreNotEqual(baseBackY, baseFrontY,
                "폴백 관의 밑변 두 끝 높이가 같습니다 — 몸은 챙이 기울어 두 발의 y가 갈립니다. " +
                "같아졌다면 폴백이 몸에서 유도된 값이 아닙니다.");

            // ★ 2026-09-03 — 여기에 <b>4</b>(= 2점)를 박아 두면 장비 담당이 폴백을 몸(닫힌 채움 띠)에
            //   맞춰 다시 굽는 순간 이 파일이 그것을 <b>막는다</b>. 위 몸 검사가 겪은 것과 같은
            //   형태의 사고다. 그래서 상한을 <b>몸에서 유도</b>하고(폴백은 몸보다 복잡할 수 없다),
            //   갈라진 실측값은 AccessoryFallbackBodyParityTests의 대장이 만료 장치와 함께 든다.
            AccessoryShapeBuilder.Shape bodyBand = AccessorySilhouetteMetrics.Find(
                AccessorySilhouetteMetrics.Build(AccessorySilhouetteMetrics.Rig(),
                    EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora), "FedoraBand");

            ItemIconPart band = FindAccent(entry.Icon);
            Assert.GreaterOrEqual(band.PointCount, 2,
                $"폴백 띠가 {band.PointCount}점입니다 — 관 밑변의 두 끝을 잇는 선이 아닙니다.");
            Assert.LessOrEqual(band.PointCount, bodyBand.Points.Length,
                $"폴백 띠가 {band.PointCount}점인데 몸의 FedoraBand는 {bodyBand.Points.Length}점입니다 — " +
                "폴백이 몸보다 <b>복잡합니다</b>. 폴백은 40×40 격자의 단순화이므로 이 방향은 " +
                "단순화가 아니라 다른 그림입니다.");

            // 잠글 것은 <b>순서</b>가 아니라 "관 밑변의 두 발을 좌표로 다시 적지 않고 그대로
            // 받았는가"다. 순서로 잠그면(옛 검사가 그랬다) 폴백이 몸처럼 닫힌 띠가 되는 날
            // 마지막 점이 <b>올린 윗변의 뒤 모서리</b>가 되어 또 한 번 고치는 것을 막는다.
            AssertBandTouches(band, baseBackX, baseBackY, "관 밑변의 뒤 발");
            AssertBandTouches(band, baseFrontX, baseFrontY, "관 밑변의 앞 발");

            if (band.PointCount != bodyBand.Points.Length)
            {
                Debug.Log($"[폴백-몸] 중절모 띠: 몸 {bodyBand.Points.Length}점 / 폴백 {band.PointCount}점 " +
                          "— 2026-09-03 스펙 14-1로 몸이 닫힌 채움 띠가 되면서 갈라졌습니다" +
                          "(AccessoryFallbackBodyParityTests.Ledger에 등재됨).");
            }

            for (int i = 0; i < band.Values.Length; i++)
            {
                Assert.That(band.Values[i], Is.InRange(0f, ViewBox),
                    "폴백 띠가 40×40 격자를 벗어났습니다.");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 옛 폴백 띠는 <b>실제로</b> 관 밑변을 가로질렀다.
        /// <para>비교하는 두 값(옛 띠 좌표 · 옛 관 밑변 y)을 <b>둘 다</b> 이 파일 안에 박제했다.
        /// 위 검사가 살아 있는 관 밑변이 아직 그 값인지를 따로 확인하므로, 관이 움직이면
        /// 컨트롤이 조용히 무의미해지는 대신 <b>그쪽이 빨개진다</b>.</para>
        /// </summary>
        [Test]
        public void 컨트롤_옛_폴백_띠는_관_밑변을_가로질렀다()
        {
            bool above = false, below = false;
            for (int i = 1; i < OldCardBand.Length; i += 2)
            {
                if (OldCardBand[i] < OldCardCrownBaseY) above = true;   // 카드 격자는 y가 아래로 자란다
                if (OldCardBand[i] > OldCardCrownBaseY) below = true;
            }

            Assert.IsTrue(above && below,
                "옛 폴백 띠가 관 밑변을 가로지르지 않았다고 나옵니다 — 이 컨트롤이 재현하려는 결함 자체가 " +
                "잘못 적혀 있습니다(기록: 양 끝 y=22로 위, 한가운데 y=23.5로 아래, 관 밑변 y=23).");

            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora);
            ItemIconPart band = FindAccent(entry.Icon);
            Assert.AreNotEqual(OldCardBand.Length, band.Values.Length,
                "지금 폴백 띠가 옛 좌표 그대로입니다 — 수정이 반영되지 않았습니다.");
        }

        /// <summary>몸 도형 <paramref name="shapeName"/>에 대응하는 폴백 조각.
        /// 폴백은 몸 도형 순서를 그대로 옮기되 그늘(Shade)만 빠진다.</summary>
        private static ItemIconPart Piece(int item, string shapeName)
        {
            List<AccessoryShapeBuilder.Shape> body = AccessorySilhouetteMetrics.Build(
                AccessorySilhouetteMetrics.Rig(), EquipmentSlot.Head, item);
            ItemIconPart[] icon = ItemCatalog.Item(EquipmentSlot.Head, item).Icon;

            int index = 0;
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Tone == AccessoryShapeBuilder.Shade) continue;
                if (body[i].Name == shapeName)
                {
                    Assert.Less(index, icon.Length,
                        $"몸의 '{shapeName}'이 폴백 {index}번이어야 하는데 조각이 {icon.Length}개뿐입니다.");
                    return icon[index];
                }
                index++;
            }
            Assert.Fail($"몸 도형에 '{shapeName}'이 없습니다.");
            return default;
        }

        /// <summary>폴백 띠의 점 중 하나가 <paramref name="x"/>,<paramref name="y"/>와 같은가.
        /// <b>순서를 묻지 않는다</b> — 물어야 할 것은 "그 좌표를 그대로 받았는가"이지
        /// "몇 번째에 두었는가"가 아니다.</summary>
        private static void AssertBandTouches(ItemIconPart band, float x, float y, string what)
        {
            for (int i = 0; i < band.PointCount; i++)
            {
                if (Mathf.Abs(band.Values[i * 2] - x) <= 1e-4f
                    && Mathf.Abs(band.Values[i * 2 + 1] - y) <= 1e-4f)
                {
                    return;
                }
            }
            Assert.Fail($"폴백 띠의 어느 점도 {what}({x:F2}, {y:F2})과 같지 않습니다 — " +
                "띠는 좌표를 새로 적지 않고 관 밑변의 두 발을 그대로 받아야 몸과 같은 계열입니다. " +
                "옛 폴백은 여기서 5점짜리 곡선으로 관 밑변을 <b>가로질렀다</b>(아래 네거티브 컨트롤).");
        }

        private static ItemIconPart FindAccent(ItemIconPart[] icon)
        {
            for (int i = 0; i < icon.Length; i++)
            {
                if (icon[i].Tone == 1) return icon[i];
            }
            Assert.Fail("중절모 폴백 아이콘에 보조색 조각(띠)이 없습니다.");
            return default;
        }
    }
}
