using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 카드 썸네일을 <b>몸에 붙는 것과 같은 도형</b>으로 그린다 — 2026-09-01, 로드맵 P0-a
    /// (docs/UX_FLOW.md 37-8 (3) 옵션 2, 리더 승인).
    ///
    /// ============================================================================
    /// 왜 만들었나 — 한 아이템이 <b>두 벌의 그림</b>을 갖고 있었다
    /// ============================================================================
    /// 카드 썸네일은 손으로 배치한 40×40 SVG(<see cref="AccessoryDefSO.icon"/>)였고, 몸에 붙는 그림은
    /// 절차적 계산(<see cref="AccessoryShapeBuilder"/>)이었다. 그래서 같은 아이템이
    /// <b>(a) 다른 좌표 (b) 다른 채움 유무 (c) 다른 색 (d) 다른 획 규칙</b>으로 그려졌고,
    /// 사용자의 "카드 그림과 실제 착용 모습의 퀄리티가 너무 다름"이 그 넷의 합이었다.
    ///
    /// 이 파일이 고치는 것은 <b>(a)와 (b)</b>다. 도형 좌표와 채움을 몸과 <b>같은 한 곳</b>에서 가져온다.
    /// (c) 색 정책(<c>WornColor</c>)과 (d) 획 위계는 각각 P5·P6이라 <b>일부러 손대지 않았다</b> —
    /// 카드는 지금까지처럼 카탈로그 색과 카드 획 두께를 그대로 쓴다. 한 라운드에서 넷을 동시에 바꾸면
    /// 무엇 때문에 그림이 달라졌는지 판정할 수 없다.
    ///
    /// ============================================================================
    /// 선례 — <see cref="CharacterPortraitStage"/>가 이미 같은 방법으로 성공했다
    /// ============================================================================
    /// 초상화도 "같은 모자를 한 벌 더" 그리는 자리였고, 도형을 공유하고 <b>개체만 분리</b>해서 풀었다.
    /// 카드는 그 통합에서 빠져 있던 마지막 한 곳이다.
    ///
    /// ============================================================================
    /// 폴백을 남긴다 (전환 리스크 관리)
    /// ============================================================================
    /// <see cref="TryBuild"/>가 false를 돌려주면 부르는 쪽은 <b>옛 아이콘</b>을 그린다.
    /// false가 되는 경우는 둘이다:
    ///  · 몸 도형이 없는 카테고리(FX/PET) — 이펙트·펫은 애초에 <see cref="AccessoryShapeBuilder"/>가
    ///    모른다(Interaction/AppearanceShapeBuilder.cs 소관). <b>정상 경로</b>다.
    ///  · 도형이 만들어졌는데 잉크 사각형이 0인 이상 상태 — 이때 옛 그림이 대신 나온다.
    /// 그래서 이 파일이 통째로 틀려도 카드가 <b>비지는 않는다</b>.
    /// </summary>
    internal static class AccessoryCardIcon
    {
        /// <summary>도형이 아이콘 상자를 채우는 비율. 1.0이면 획 두께가 상자 밖으로 삐져나간다.</summary>
        private const float FitFraction = 0.86f;

        /// <summary>모든 카드가 <b>같은 배율</b>을 쓰지 않는 이유: 40×40 안에서 나비넥타이와 긴 망토는
        /// 실제 크기 차이가 6배라, 공통 배율로는 한쪽이 점이 되고 다른 쪽이 상자를 넘는다.
        /// 카드는 "이게 어떻게 생겼나"를 보여주는 자리이므로 <b>각자 꽉 차게</b> 맞춘다.</summary>
        private static readonly List<AccessoryShapeBuilder.Shape> _shapes =
            new List<AccessoryShapeBuilder.Shape>(8);

        private static readonly Vector2[] _points = new Vector2[128];

        /// <summary>
        /// <paramref name="root"/> 아래에 이 아이템의 몸 도형을 축소해 그린다.
        /// </summary>
        /// <param name="slot">장비 자리. FX/PET처럼 몸 도형이 없는 자리면 false를 돌려준다.</param>
        /// <param name="size">아이콘 정사각 크기(캔버스 유닛).</param>
        /// <param name="stroke">윤곽선 두께(캔버스 유닛). 카드의 기존 획 규약을 그대로 받는다.</param>
        /// <returns>그렸으면 true. false면 부르는 쪽이 옛 아이콘으로 폴백해야 한다.</returns>
        internal static bool TryBuild(RectTransform root, EquipmentSlot slot, int itemIndex,
            float size, float stroke, Color primary, Color secondary)
        {
            if (root == null) return false;

            // 카드는 언제나 정면(facing +1)이고 모자를 쓰지 않은 <b>단품</b>이다 — 보관함은 "이 아이템
            // 하나"를 보여주는 자리라, 지금 쓴 모자에 따라 머리카락이 잘리면 카드가 상태에 끌려간다.
            AccessoryShapeBuilder.Rig rig = CardRig();
            _shapes.Clear();
            AccessoryShapeBuilder.Append(_shapes, slot, itemIndex, rig,
                float.PositiveInfinity, 0f, mondayLoosened: false);
            if (_shapes.Count == 0) return false;

            if (!TryMeasure(out Vector2 min, out Vector2 max)) return false;

            float span = Mathf.Max(max.x - min.x, max.y - min.y);
            if (span <= 0.0001f) return false;

            float scale = size * FitFraction / span;
            var center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);

            for (int i = 0; i < _shapes.Count; i++)
            {
                AccessoryShapeBuilder.Shape shape = _shapes[i];
                Vector3[] pts = shape.Points;
                if (pts == null || pts.Length < 2) continue;

                Color color = ToneColor(shape.Tone, primary, secondary);

                int count = Mathf.Min(pts.Length, _points.Length);
                for (int k = 0; k < count; k++)
                {
                    _points[k] = new Vector2((pts[k].x - center.x) * scale, (pts[k].y - center.y) * scale);
                }

                // 채움 먼저(윤곽선 아래) — 몸과 같은 규칙이다. 몸에서는 sortingOrder로, uGUI에서는
                // <b>자식 순서</b>로 앞뒤가 정해지므로 면을 먼저 만들면 그것으로 충분하다.
                Color outline = color;
                if (shape.Filled)
                {
                    AddFill(root, shape.Name + "Fill", _points, count, color);
                    outline = AccessoryShapeBuilder.FillOutlineColor(color);
                }

                if (shape.Loop && count < _points.Length)
                {
                    _points[count] = _points[0];   // 고리를 닫는 마지막 선분
                    count++;
                }
                UiChrome.AddPolyline(root, shape.Name, _points, count, stroke, outline);
            }
            return true;
        }

        /// <summary>카드용 리그. 치수의 <b>비율</b>은 배율 1.0 프리팹 실측과 같다 — 절대 크기는
        /// 어차피 위에서 상자에 맞춰 다시 재므로, 여기서 중요한 것은 비율뿐이다.</summary>
        internal static AccessoryShapeBuilder.Rig CardRig()
        {
            const float h = StickConfig.BaselineCharacterTotalHeight;
            const float r = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(r, h - r,
                AccessoryShapeBuilder.BaselineShoulderLocalY,
                AccessoryShapeBuilder.BaselineHipLocalY, 1f);
        }

        private static bool TryMeasure(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            for (int i = 0; i < _shapes.Count; i++)
            {
                Vector3[] pts = _shapes[i].Points;
                if (pts == null) continue;
                for (int k = 0; k < pts.Length; k++)
                {
                    min = Vector2.Min(min, new Vector2(pts[k].x, pts[k].y));
                    max = Vector2.Max(max, new Vector2(pts[k].x, pts[k].y));
                    any = true;
                }
            }
            return any;
        }

        private static Color ToneColor(byte tone, Color primary, Color secondary)
        {
            if (tone == AccessoryShapeBuilder.Accent) return secondary;
            if (tone == AccessoryShapeBuilder.Shade) return AccessoryShapeBuilder.FillOutlineColor(primary);
            return primary;
        }

        private static void AddFill(RectTransform parent, string name, Vector2[] points, int count, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(AccessoryFillGraphic));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var fill = go.GetComponent<AccessoryFillGraphic>();
            fill.color = color;
            fill.raycastTarget = false;
            fill.SetPolygon(points, count);
        }
    }

    /// <summary>
    /// 카드 썸네일의 <b>채움 면</b> 하나. 몸 쪽 채움(<c>AccessoryShapeBuilder.BuildFillMesh</c>)과
    /// <b>같은 삼각형 분할</b>(귀 자르기)을 쓴다 — 분할을 두 벌 만들면 모자 챙 같은 오목 도형에서
    /// 카드와 몸의 실루엣이 달라진다.
    ///
    /// <para><see cref="Image"/>를 상속하는 이유는 취향이 아니다. 정보창은 카드 아이콘의 색을
    /// <c>GetComponentsInChildren&lt;Image&gt;()</c>로 모아 잠김/해금 상태에 따라 갈아끼운다
    /// (<c>CharacterInfoWindow.RestoreIconColors</c>). 순수 <see cref="MaskableGraphic"/>으로 만들면
    /// 그 수집에서 <b>조용히 빠져</b> 잠긴 카드에서도 채움만 제 색으로 남는다.</para>
    ///
    /// <para>스프라이트는 쓰지 않는다(<see cref="OnPopulateMesh"/>를 통째로 대신하므로).
    /// 텍스처가 없으면 uGUI가 흰 텍스처를 쓰고 정점 색이 그대로 나온다.</para>
    /// </summary>
    internal sealed class AccessoryFillGraphic : Image
    {
        private Vector2[] _points;
        private int[] _triangles;

        internal void SetPolygon(Vector2[] points, int count)
        {
            if (points == null || count < 3)
            {
                _points = null;
                _triangles = null;
                SetVerticesDirty();
                return;
            }

            _points = new Vector2[count];
            var lifted = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                _points[i] = points[i];
                lifted[i] = new Vector3(points[i].x, points[i].y, 0f);
            }
            _triangles = AccessoryShapeBuilder.Triangulate(lifted);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points == null || _triangles == null || _triangles.Length < 3) return;

            Color32 c = color;
            for (int i = 0; i < _points.Length; i++)
            {
                vh.AddVert(new Vector3(_points[i].x, _points[i].y, 0f), c, Vector2.zero);
            }
            for (int i = 0; i + 2 < _triangles.Length; i += 3)
            {
                vh.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
            }
        }
    }
}
