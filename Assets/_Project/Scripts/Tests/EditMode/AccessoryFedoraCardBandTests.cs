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
        /// (2026-09-01 방울 라운드가 실제로 겪은 실패).</summary>
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

        [Test]
        public void 몸의_중절모_띠는_관_밑변의_두_끝점_그_자체다()
        {
            AccessoryShapeBuilder.Rig rig = AccessorySilhouetteMetrics.Rig();
            List<AccessoryShapeBuilder.Shape> shapes =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora);

            AccessoryShapeBuilder.Shape band = AccessorySilhouetteMetrics.Find(shapes, "FedoraBand");
            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(shapes, "FedoraCrown");

            Assert.AreEqual(2, band.Points.Length, "띠는 관 밑변 두 끝점만으로 된 직선이어야 합니다.");
            Assert.AreEqual(crown.Points[0], band.Points[0],
                "띠의 뒤 끝이 관 밑변의 뒤 끝과 다릅니다 — 좌표를 새로 적으면 어긋날 자리가 생깁니다.");
            Assert.AreEqual(crown.Points[crown.Points.Length - 1], band.Points[1],
                "띠의 앞 끝이 관 밑변의 앞 끝과 다릅니다.");
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

            ItemIconPart crown = entry.Icon[0];
            float baseBackX = crown.Values[0];
            float baseBackY = crown.Values[1];
            float baseFrontX = crown.Values[crown.Values.Length - 2];
            float baseFrontY = crown.Values[crown.Values.Length - 1];

            Assert.AreEqual(baseBackY, baseFrontY, 1e-4f,
                "폴백 관의 밑변 두 끝 높이가 다릅니다 — 아래 비교의 전제가 깨집니다.");
            Assert.AreEqual(OldCardCrownBaseY, baseBackY, 1e-4f,
                $"폴백 관의 밑변이 y={baseBackY}로 옮겨졌습니다(옛 값 {OldCardCrownBaseY}). " +
                "아래 네거티브 컨트롤이 박제한 값과 어긋나므로 컨트롤도 함께 갱신해야 합니다.");

            ItemIconPart band = FindAccent(entry.Icon);
            Assert.AreEqual(4, band.Values.Length,
                $"폴백 띠가 {band.Values.Length / 2}점입니다 — 관 밑변의 두 끝을 잇는 " +
                "직선(2점)이어야 몸과 같은 계열입니다.");
            Assert.AreEqual(baseBackX, band.Values[0], 1e-4f, "폴백 띠의 뒤 끝 x가 관 밑변과 다릅니다.");
            Assert.AreEqual(baseBackY, band.Values[1], 1e-4f, "폴백 띠의 뒤 끝 y가 관 밑변과 다릅니다.");
            Assert.AreEqual(baseFrontX, band.Values[2], 1e-4f, "폴백 띠의 앞 끝 x가 관 밑변과 다릅니다.");
            Assert.AreEqual(baseFrontY, band.Values[3], 1e-4f, "폴백 띠의 앞 끝 y가 관 밑변과 다릅니다.");

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
