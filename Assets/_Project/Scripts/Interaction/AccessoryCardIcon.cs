using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 카드 썸네일 — <b>같은 정의처</b>(<see cref="AccessoryShapeBuilder.Append"/>)의 <b>카드 표면</b> 조각을 그린다.
    ///
    /// ============================================================================
    /// 이력 — 한 아이템이 <b>두 벌의 그림</b>을 갖고 있었다 (2026-09-01, 로드맵 P0-a)
    /// ============================================================================
    /// 카드 썸네일은 손으로 배치한 40×40 SVG(<see cref="AccessoryDefSO.icon"/>)였고, 몸에 붙는 그림은
    /// 절차적 계산(<see cref="AccessoryShapeBuilder"/>)이었다. 그래서 같은 아이템이
    /// <b>(a) 다른 좌표 (b) 다른 채움 유무 (c) 다른 색 (d) 다른 획 규칙</b>으로 그려졌고,
    /// 사용자의 "카드 그림과 실제 착용 모습의 퀄리티가 너무 다름"이 그 넷의 합이었다. 그 라운드가
    /// (a)(b)를 닫았다 — 카드가 몸 도형 목록을 그대로 받았다.
    ///
    /// ============================================================================
    /// ★ 2026-09-05 계약 v2 — 카드 = 몸 = 인계본 정면 기하 한 벌 (R16 §14-0 #1)
    /// ============================================================================
    /// 사용자 판정 *"장비디자인 자체가 내가 전달한 디자인 컨셉과 다르다"*. 원인은 픽셀이 아니라 배관이었다
    /// (GAME_ARCHITECTURE_REVIEW §15-3-2 반증 ②: 카드 44px 에서 하이라이트 17/17 생존) — 카드가 몸의 3/4
    /// 도형만 받으므로 인계본 정면 조각이 도달할 경로가 없었다. R16 이 착용 표면까지 인계본 정면 기하로 바꿔
    /// 이제 두 표면은 <b>좌표 한 벌</b>이다(망토만 몸 = 무대 도형 · 카드 = 아이콘). 그래서 지금은:
    ///  · <see cref="AccessoryShapeBuilder.Append"/>에 <see cref="AccessorySurface.Card"/>를 묻는다.
    ///  · 인계본 조각(<see cref="AccessoryShapeBuilder.Shape.IsHandoff"/>)이면 <b>인계본 프레이밍</b>(슬롯 고정 배율,
    ///    <see cref="Frame"/>) · <b>인계본 획</b>(2.2/64 × 연속 배수) · <b>재질 팔레트 색 문법</b>(잉크 윤곽 + 재질색 M/M2 불투명
    ///    + 재질선 + 흰 광택 α0.42, docs/EQUIPMENT_PALETTE.md §2)으로 그린다. 투명 렌즈 워시(α0.16/0.20)만 카드 바탕 위에
    ///    <b>사전 합성</b>한다 — 보이는 색은 런타임 알파와 같고 창 알파만 지킨다(UiChrome 「알파 채널의 법칙」:
    ///    반투명 UI 픽셀은 투명 오버레이 창을 그 자리에서 뚫는다).
    ///  · 인계본 조각이 <b>없는</b> 아이템(인계본에 없는 8종 + 머리 6종)은 예전 그대로 — 몸 조각을 봉투에 맞춰
    ///    키우고(<see cref="FitFraction"/>) 몸의 색 표로 칠한다.
    ///
    /// ============================================================================
    /// 폴백을 남긴다 (전환 리스크 관리)
    /// ============================================================================
    /// <see cref="TryBuild"/>가 false를 돌려주면 부르는 쪽은 <b>옛 아이콘</b>을 그린다.
    ///  · 몸 도형이 없는 카테고리(FX/PET) — 이펙트·펫은 애초에 <see cref="AccessoryShapeBuilder"/>가
    ///    모른다(Interaction/AppearanceShapeBuilder.cs 소관). <b>정상 경로</b>다.
    ///  · 도형이 만들어졌는데 잉크 사각형이 0인 이상 상태 — 이때 옛 그림이 대신 나온다.
    /// 그래서 이 파일이 통째로 틀려도 카드가 <b>비지는 않는다</b>.
    /// </summary>
    internal static class AccessoryCardIcon
    {
        /// <summary>v1 조각(몸 도형 폴백) 전용 — 도형이 아이콘 상자를 채우는 비율. 1.0이면 획 두께가 상자
        /// 밖으로 삐져나간다. 인계본 조각은 이 봉투 맞춤을 <b>쓰지 않는다</b>: 외알안경(폭 34.7u)이
        /// 나비넥타이(48u)만큼 부풀어 인계본의 크기 관계가 깨진다(§13-4-2 #7).</summary>
        private const float FitFraction = 0.86f;

        /// <summary>
        /// 인계본이 <b>스스로 선언한</b> 카드 프레임 — README 「졸라맨 캐릭터」(스테이지 200×240, 머리 원 (100,46) r 28,
        /// 렌더 폭 158px, 스테이지 상단 26px)와 「장비 오버레이 위치」(슬롯 박스 한 변·상단 px). 조각 좌표는
        /// 머리 반경 R 단위이므로, 슬롯마다 <b>고정된</b> 「R당 아이콘 단위」와 「아이콘 중심의 y(R)」로 되돌린다
        /// (design/equipment/verify/handoff.py <c>icon_to_R</c>의 역함수 — 같은 선언값에서만 유도한다).
        /// <para>값이 갈라지면 <c>CardShapeContractTests</c>가 골든의 FRAME 줄과 대조해 잡는다.</para>
        /// </summary>
        internal static class Frame
        {
            public const float IconViewBox = 64f;
            /// <summary>인계본 기본 아이콘 획 <c>2.2/64</c>. 현행 <c>IconStroke = 1.7 × 58/40</c>은 상자의 4.25%로 +24% 굵었다(§13-2-1).</summary>
            public const float IconStroke = 2.2f;
            public const float StrokeFraction = IconStroke / IconViewBox;

            private const float StageViewBoxWidth = 200f;
            private const float StageHeadRadius = 28f;
            private const float StageHeadCenterY = 46f;
            private const float StageRenderWidth = 158f;
            private const float StageTopPx = 26f;

            private const float PxPerStageUnit = StageRenderWidth / StageViewBoxWidth;              // 0.79
            internal const float HeadRadiusPx = StageHeadRadius * PxPerStageUnit;                  // 22.12
            internal const float HeadCenterPx = StageTopPx + StageHeadCenterY * PxPerStageUnit;    // 62.34

            /// <param name="unitsPerR">머리 반경 1 R 이 아이콘 단위(64 viewBox) 몇 칸인가.</param>
            /// <param name="centerYInR">아이콘 상자 중심의 y(머리 중심 기준 R).</param>
            public static bool TryGet(EquipmentSlot slot, out float unitsPerR, out float centerYInR)
            {
                float box, top;
                switch (slot)
                {
                    case EquipmentSlot.Head: box = 70f; top = 6f; // head
                        break;
                    case EquipmentSlot.Eyes: box = 48f; top = 38f; // eyes
                        break;
                    case EquipmentSlot.Neck: box = 54f; top = 78f; // neck
                        break;
                    case EquipmentSlot.Shoulders: box = 88f; top = 72f; // back
                        break;
                    default:
                        unitsPerR = 0f;
                        centerYInR = 0f;
                        return false;
                }
                float u = box / IconViewBox;                       // 아이콘 단위당 px
                unitsPerR = HeadRadiusPx / u;
                centerYInR = (HeadCenterPx - top - IconViewBox * 0.5f * u) / HeadRadiusPx;
                return true;
            }
        }

        private static readonly List<AccessoryShapeBuilder.Shape> _shapes =
            new List<AccessoryShapeBuilder.Shape>(16);

        private static readonly Vector2[] _points = new Vector2[128];

        /// <summary>사전 합성의 밑색 — 앞 조각의 평면화된 채움색.</summary>
        private static readonly Color[] _flat = new Color[32];
        private static readonly bool[] _hasFlat = new bool[32];

        /// <summary>
        /// <paramref name="root"/> 아래에 이 아이템의 카드 그림을 그린다.
        /// </summary>
        /// <param name="slot">장비 자리. FX/PET처럼 몸 도형이 없는 자리면 false를 돌려준다.</param>
        /// <param name="size">아이콘 정사각 크기(캔버스 유닛).</param>
        /// <param name="stroke">v1 폴백 윤곽선 두께. 인계본 조각은 인계본 획(<see cref="Frame.StrokeFraction"/>)을 쓴다.</param>
        /// <param name="primary">아이템 재질색 M(카탈로그 <c>PrimaryColor</c>). v1 폴백의 주색이자 인계본 조각의 재질 채움/재질선이다
        /// (재질 팔레트 §2 — 카드와 몸이 같은 hex 원천, 등급색은 조각에 0개).</param>
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
                float.PositiveInfinity, 0f, mondayLoosened: false, surface: AccessorySurface.Card);
            if (_shapes.Count == 0) return false;

            if (HasDesignedCard(_shapes) && Frame.TryGet(slot, out float unitsPerR, out float centerYInR))
            {
                return BuildDesigned(root, rig, size, unitsPerR, centerYInR,
                    AccessoryHandoffPalette.Card(slot, itemIndex, primary, secondary));
            }
            return BuildFitted(root, size, stroke, primary, secondary);
        }

        /// <summary>인계본 조각(계약 v2)이 하나라도 있는가. 없으면 몸 도형 폴백(v1)이다.</summary>
        internal static bool HasDesignedCard(List<AccessoryShapeBuilder.Shape> shapes)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].IsHandoff) return true;
            }
            return false;
        }

        // ============================================================================
        // 인계본 조각 — 인계본 프레이밍 · 인계본 획 · 인계본 색 문법 (§13-2-1 · §13-3-1 · §14-5)
        // ============================================================================

        private static bool BuildDesigned(RectTransform root, in AccessoryShapeBuilder.Rig rig, float size,
            float unitsPerR, float centerYInR, in AccessoryShapeBuilder.HandoffPalette palette)
        {
            float pxPerR = unitsPerR * (size / Frame.IconViewBox);
            float baseStroke = size * Frame.StrokeFraction;
            Color bg = UiChrome.CardSurfaceMuted;
            int count = Mathf.Min(_shapes.Count, _flat.Length);
            for (int i = 0; i < count; i++) _hasFlat[i] = false;

            bool drewAny = false;
            for (int i = 0; i < count; i++)
            {
                AccessoryShapeBuilder.Shape shape = _shapes[i];
                Vector3[] pts = shape.Points;
                if (pts == null || pts.Length < 2) continue;

                int n = Mathf.Min(pts.Length, _points.Length);
                for (int k = 0; k < n; k++)
                {
                    float xR = pts[k].x / rig.HeadRadius;
                    float yR = (pts[k].y - rig.HeadCenterY) / rig.HeadRadius;
                    _points[k] = new Vector2(xR * pxPerR, (yR - centerYInR) * pxPerR);
                }

                // 밑색 = 이 조각이 얹히는 앞 조각의 평면화된 채움(없으면 바탕). 인계본의 알파는 전부 이 위에서
                // 정해졌으므로 여기서 미리 합성하면 화면에는 불투명색만 남는다.
                int underIndex = i - shape.UnderBack;
                Color under = shape.UnderBack > 0 && underIndex >= 0 && _hasFlat[underIndex] ? _flat[underIndex] : bg;

                float strokeWidth = baseStroke * AccessoryStroke.Multiplier(shape.StrokeMult);
                if (shape.Filled)
                {
                    Color fill = Color.Lerp(under, AccessoryShapeBuilder.HandoffFillBase(shape, palette),
                        AccessoryShapeBuilder.HandoffFillAlpha(shape, body: false));
                    fill.a = 1f;
                    AddFill(root, shape.Name + "Fill", _points, n, fill);
                    _flat[i] = fill;
                    _hasFlat[i] = true;
                    drewAny = true;
                }
                if (shape.NoStroke) continue;

                // 윤곽/낱선 = 잉크 C(채움×0.28 이 아니다 — §13-3-1) · 하이라이트 = 흰, 알파는 밑색 위 사전 합성.
                Color line = Color.Lerp(under, AccessoryShapeBuilder.HandoffLineBase(shape, palette),
                    AccessoryShapeBuilder.HandoffLineAlpha(shape));
                line.a = 1f;
                AddStrokes(root, shape, n, strokeWidth, line);
                drewAny = true;
            }
            return drewAny;
        }

        /// <summary><see cref="_points"/>의 앞 <paramref name="count"/>점을 잇는다. 고리면 마지막 선분을 닫는다.</summary>
        private static void AddStrokes(RectTransform root, in AccessoryShapeBuilder.Shape shape, int count,
            float strokeWidth, Color color)
        {
            if (shape.Loop && count < _points.Length)
            {
                _points[count] = _points[0];
                count++;
            }
            UiChrome.AddPolyline(root, shape.Name, _points, count, strokeWidth, color);
        }

        // ============================================================================
        // 폴백 — v1 몸 도형을 봉투에 맞춰 그린다 (2026-09-01 경로 그대로)
        // ============================================================================

        private static bool BuildFitted(RectTransform root, float size, float stroke, Color primary, Color secondary)
        {
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

                Color color = AccessoryShapeBuilder.ResolveToneColor(_shapes, i, 0, primary, secondary);

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
                if (shape.NoStroke) continue;

                AddStrokes(root, shape, count, stroke * AccessoryStroke.Multiplier(shape.StrokeMult), outline);
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

        /// <summary>채운 면 하나. <c>CharacterInfoWindow.BuildIcon</c>의
        /// <see cref="ItemIconPartKind.Polygon"/> 경로도 <b>같은 분할</b>을 쓰라고 internal이다 —
        /// 분할을 두 벌 만들면 오목한 챙에서 폴백과 본경로의 실루엣이 갈린다.</summary>
        internal static void AddFill(RectTransform parent, string name, Vector2[] points, int count, Color color)
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
