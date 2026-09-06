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
    ///
    /// ============================================================================
    /// ★ 2026-09-06 배관 통일 — 42종이 <b>같은 프레임 · 같은 획 · 같은 잉크</b>를 탄다
    /// ============================================================================
    /// 사용자 신고 *"내가 지시한 장비만 고도화 되어있고 나머지 장비들은 고도화 안되어있음"*. 격차는 좌표가 아니라
    /// <b>배관</b>이었다 — 인계본 16종만 위 규칙을 타고, 나머지 v1 14종(장비 8 + 머리 6)은 세 가지가 달랐다.
    /// 이 라운드가 그 셋을 닫는다(좌표는 한 점도 건드리지 않는다):
    /// <list type="number">
    ///   <item><b>획 폭</b> — v1 은 부르는 쪽이 준 <c>1.7 × size/40</c>(상자의 4.25%)이라 인계본 <c>2.2/64</c>(3.4375%)보다
    ///     <b>+23.6% 굵었다</b>. 이제 두 계통 모두 <see cref="Frame.StrokeFraction"/> 하나를 쓴다(v1 기준 −19.1%).</item>
    ///   <item><b>윤곽 색</b> — v1 은 <c>FillOutlineColor(fill) = fill×0.28</c>이라 <b>조각마다 윤곽 색이 달랐고</b>,
    ///     어두운 카드 바탕 위에서 어두운 윤곽이라 「선화」로 읽히지 않았다. 이제 채운 조각의 윤곽은
    ///     인계본과 <b>같은 잉크 한 색</b>(<see cref="UiChrome.CardIconInk"/>)이다.
    ///     ★ 채움 <b>없는</b> 낱선은 그대로다 — 역할 0/1은 이미 인계본 문법(M/M2)과 같은 색을 낸다.
    ///     역할 2(그늘) 낱선 2개(판초 주름)만 아직 <c>M×0.28</c>인데, 인계본의 같은 자리는 <b>잉크 α0.35 × 배수 0.7</b>이라
    ///     알파·배수 배정이 필요하다 — 그건 데이터(design-equipment)의 몫이라 여기서 지어내지 않는다.</item>
    ///   <item><b>프레이밍</b> — v1 은 봉투 맞춤(도형을 상자의 0.86에 꽉 채우기)이라 <b>아이템마다 배율이 튀었다</b>
    ///     (밀짚모자 8.92 ~ 펜던트 23.65 px/R). 그래서 카테고리 최대 챙(4.24R)을 가진 밀짚모자가 화면에서 <b>가장 작게</b>
    ///     떴다 — 크기 관계가 거꾸로였다. 이제 v1 도 <b>슬롯 고정 배율</b>(<see cref="Frame"/>)을 탄다.</item>
    /// </list>
    /// <para>★ <b>인계본 16종은 이 변경으로 한 픽셀도 움직이지 않는다</b>: 획·색·프레임 모두 원래 쓰던 값 그대로이고,
    /// 새로 생긴 넘침 보정(<see cref="BoxFit"/>)은 인계본 조각이 이미 64 viewBox 안에 들어 있어(그 사실을
    /// <c>CardShapeContractTests.카드_조각은_슬롯_프레임_안에_든다</c>가 잠근다) <b>구조적으로 항등</b>이다.</para>
    ///
    /// ============================================================================
    /// 폴백을 남긴다 (전환 리스크 관리)
    /// ============================================================================
    /// <see cref="TryBuild"/>가 false를 돌려주면 부르는 쪽은 <b>옛 아이콘</b>을 그린다.
    ///  · 몸 도형이 없는 카테고리(FX/PET) — 이펙트·펫은 애초에 <see cref="AccessoryShapeBuilder"/>가
    ///    모른다(Interaction/AppearanceShapeBuilder.cs 소관). <b>정상 경로</b>다.
    ///  · 카드 프레임이 없는 자리 — 지금은 없다. 새 슬롯이 생기면 카드가 <b>비는 대신</b> 옛 아이콘이 나온다.
    /// 그래서 이 파일이 통째로 틀려도 카드가 <b>비지는 않는다</b>.
    /// </summary>
    internal static class AccessoryCardIcon
    {
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
            /// <summary>인계본 기본 아이콘 획 <c>2.2/64</c> = 상자의 3.4375%. 옛 v1 획 <c>1.7 × size/40</c>은 상자의 4.25%로
            /// <b>+23.6% 굵었다</b>(§13-2-1). 2026-09-06부터 <b>두 계통이 이 값 하나</b>를 쓴다 — 카드 42종의 획 폭이
            /// 아이템에 따라 갈리는 자리가 없어졌다.</summary>
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

            /// <summary>
            /// 카드가 <b>실제로</b> 쓰는 프레임. <see cref="TryGet"/>과 다른 이유는 하나뿐이다 —
            /// 머리(<see cref="EquipmentSlot.Hair"/>)에는 인계본이 선언한 슬롯 박스가 <b>없다</b>.
            /// <para>머리 6종은 카드에서 <b>머리 위에 얹히는 것</b>이라는 점에서 HEAD 와 같은 자리이므로 HEAD 박스를
            /// 빌린다 — 새 숫자를 만들지 않는다. 봉투 맞춤이던 시절 머리 6종은 9.59~15.78 px/R 로 흩어져
            /// <b>민머리가 포니테일보다 1.65배 크게</b> 떴다. ★ 머리 6종은 2026-09-06 라운드에 은퇴 중이고
            /// (착용/외형탭/보관함/상점에서 제외), 여기는 그 뒤에도 카드 경로가 남을 경우의 배율만 맞춰 둔 자리다.</para>
            /// <para><see cref="TryGet"/>은 <b>인계본이 선언한 4개</b> 그대로 둔다 — 골든의 FRAME 줄과 대조하는
            /// <c>CardShapeContractTests</c>가 "머리에는 슬롯 박스가 없다"를 그 함수로 잠그고 있고, 그 사실은 지금도 참이다.</para>
            /// </summary>
            public static bool TryGetCardFrame(EquipmentSlot slot, out float unitsPerR, out float centerYInR)
                => TryGet(slot == EquipmentSlot.Hair ? EquipmentSlot.Head : slot, out unitsPerR, out centerYInR);
        }

        /// <summary>
        /// 슬롯 고정 배율 위에 얹는 <b>넘침 보정</b>. 상자를 넘는 아이템만 넘은 만큼 줄이고(<see cref="Shrink"/> ≤ 1)
        /// 남은 여백으로 밀어 넣는다. <b>절대 키우지 않는다</b> — 키우는 순간 옛 봉투 맞춤이 되살아나
        /// "작은 장신구가 큰 모자만큼 부푸는" 문제가 돌아온다(§13-4-2 #7).
        ///
        /// <para>왜 필요한가: v1 좌표 중 둘이 인계본 아이콘 상자(64u)보다 크다 — <b>밀짚모자 폭 85.75u</b>(챙 4.24R),
        /// <b>판초 높이 65.05u</b>(4.04R). 보정 없이 슬롯 배율만 걸면 그림이 카드 밖으로 나가 이름줄·등급 리본을 덮는다.
        /// 이 둘은 좌표를 줄여야 근본 해결이고(다음 라운드, design-equipment), 그때 이 보정은 <b>저절로 항등</b>이 된다.</para>
        ///
        /// <para>★ 인계본 16종은 전부 항등이다(최대 이탈 30.5u ≤ 32u). 그 전제를
        /// <c>CardShapeContractTests.카드_조각은_슬롯_프레임_안에_든다</c>가 데이터 쪽에서,
        /// <c>AccessoryCardIconTests.인계본_열여섯_종은_넘침_보정이_항등이다</c>가 이 계산 쪽에서 잠근다.</para>
        /// </summary>
        internal readonly struct BoxFit
        {
            /// <summary>슬롯 고정 배율에 곱하는 축소율. 1이면 보정 없음.</summary>
            public readonly float Shrink;
            /// <summary>축소 뒤에도 상자를 벗어나면 밀어 넣는 양(아이콘 64 viewBox 단위).</summary>
            public readonly float OffsetXInUnits;
            public readonly float OffsetYInUnits;

            public BoxFit(float shrink, float offsetXInUnits, float offsetYInUnits)
            {
                Shrink = shrink;
                OffsetXInUnits = offsetXInUnits;
                OffsetYInUnits = offsetYInUnits;
            }

            public static BoxFit Identity => new BoxFit(1f, 0f, 0f);

            public bool IsIdentity => Shrink >= 1f && OffsetXInUnits == 0f && OffsetYInUnits == 0f;
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
        /// <param name="legacyStroke">★ <b>카드는 이 값을 쓰지 않는다</b>(2026-09-06). 획은 두 계통 모두
        /// 인계본 아이콘 획(<see cref="Frame.StrokeFraction"/>) 하나다. 파라미터가 남아 있는 이유는 부르는 쪽
        /// (<c>CharacterInfoWindow.Cards</c>)이 <b>같은 값을 옛 폴백 아이콘</b>(<c>BuildIcon</c> — FX/PET 전용)에도
        /// 쓰기 때문이고, 그 파일은 이 라운드에 다른 작업이 점유 중이라 손대지 않았다.
        /// <para>죽은 인자가 조용히 되살아나지 않도록
        /// <c>AccessoryCardIconTests.카드_획은_인계본_비율이고_부르는_쪽_값에_좌우되지_않는다</c>가
        /// 서로 다른 두 값으로 같은 두께가 나오는지 잰다.</para></param>
        /// <param name="primary">아이템 재질색 M(카탈로그 <c>PrimaryColor</c>). v1 조각의 주색이자 인계본 조각의 재질 채움/재질선이다
        /// (재질 팔레트 §2 — 카드와 몸이 같은 hex 원천, 등급색은 조각에 0개).</param>
        /// <returns>그렸으면 true. false면 부르는 쪽이 옛 아이콘으로 폴백해야 한다.</returns>
        internal static bool TryBuild(RectTransform root, EquipmentSlot slot, int itemIndex,
            float size, float legacyStroke, Color primary, Color secondary)
        {
            if (root == null) return false;

            // 카드는 언제나 정면(facing +1)이고 모자를 쓰지 않은 <b>단품</b>이다 — 보관함은 "이 아이템
            // 하나"를 보여주는 자리라, 지금 쓴 모자에 따라 머리카락이 잘리면 카드가 상태에 끌려간다.
            AccessoryShapeBuilder.Rig rig = CardRig();
            _shapes.Clear();
            AccessoryShapeBuilder.Append(_shapes, slot, itemIndex, rig,
                float.PositiveInfinity, 0f, mondayLoosened: false, surface: AccessorySurface.Card);
            if (_shapes.Count == 0) return false;
            if (!Frame.TryGetCardFrame(slot, out float unitsPerR, out float centerYInR)) return false;

            return BuildFramed(root, rig, size, unitsPerR, centerYInR,
                AccessoryHandoffPalette.Card(slot, itemIndex, primary, secondary), primary, secondary);
        }

        /// <summary>인계본 조각(계약 v2)이 하나라도 있는가. 없으면 v1 조각이다.
        /// <para>★ 그리는 쪽은 이 <b>아이템 단위</b> 판정이 아니라 <see cref="AccessoryShapeBuilder.Shape.IsHandoff"/>
        /// <b>조각 단위</b>로 색 문법을 가른다 — 한 아이템에 두 계통이 섞이는 날에도 갈라질 자리가 없게.
        /// 이 함수는 계약 검사(<c>CardShapeContractTests</c>)와 시트 대조 도구가 쓴다.</para></summary>
        internal static bool HasDesignedCard(List<AccessoryShapeBuilder.Shape> shapes)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].IsHandoff) return true;
            }
            return false;
        }

        /// <summary>
        /// 이 아이템의 <see cref="BoxFit"/>. 그리지 않고 <b>계산만</b> 한다 — 인계본 16종이 항등인지를
        /// 렌더 결과가 아니라 계산에서 직접 잴 수 있게 열어 둔 자리다(<c>AccessoryCardIconTests</c>).
        /// <para>★ <see cref="TryBuild"/>와 <b>같은 정적 버퍼</b>(<c>_shapes</c>)를 쓴다. 이 파일의 모든 진입점은
        /// 상주 앱에서 매 프레임 할당하지 않으려고 버퍼를 공유하므로, 둘을 겹쳐 부르지 마라.</para>
        /// </summary>
        internal static bool TryGetBoxFit(EquipmentSlot slot, int itemIndex, out BoxFit fit)
        {
            fit = BoxFit.Identity;
            AccessoryShapeBuilder.Rig rig = CardRig();
            _shapes.Clear();
            AccessoryShapeBuilder.Append(_shapes, slot, itemIndex, rig,
                float.PositiveInfinity, 0f, mondayLoosened: false, surface: AccessorySurface.Card);
            if (_shapes.Count == 0) return false;
            if (!Frame.TryGetCardFrame(slot, out float unitsPerR, out float centerYInR)) return false;
            fit = MeasureBoxFit(rig, unitsPerR, centerYInR);
            return true;
        }

        // ============================================================================
        // 그리기 — 42종이 <b>같은</b> 프레이밍·획을 타고, 색 문법만 조각의 계통을 따른다
        // (§13-2-1 · §13-3-1 · §14-5 + 2026-09-06 배관 통일)
        // ============================================================================

        private static bool BuildFramed(RectTransform root, in AccessoryShapeBuilder.Rig rig, float size,
            float unitsPerR, float centerYInR, in AccessoryShapeBuilder.HandoffPalette palette,
            Color primary, Color secondary)
        {
            BoxFit fit = MeasureBoxFit(rig, unitsPerR, centerYInR);
            float unitsToPx = size / Frame.IconViewBox;
            float pxPerR = unitsPerR * fit.Shrink * unitsToPx;
            float offsetX = fit.OffsetXInUnits * unitsToPx;
            float offsetY = fit.OffsetYInUnits * unitsToPx;

            // ★ 획은 <b>축소를 따라가지 않는다</b>. 인계본이 아이콘 획을 상자의 비율(2.2/64)로 선언했기 때문이고,
            //   따라가게 만들면 넘치는 아이템만 선이 가늘어져 한 카테고리 안에서 획 굵기가 다시 갈린다.
            float baseStroke = size * Frame.StrokeFraction;
            Color bg = UiChrome.CardSurfaceMuted;
            for (int i = 0; i < _hasFlat.Length; i++) _hasFlat[i] = false;

            bool drewAny = false;
            for (int i = 0; i < _shapes.Count; i++)
            {
                AccessoryShapeBuilder.Shape shape = _shapes[i];
                Vector3[] pts = shape.Points;
                if (pts == null || pts.Length < 2) continue;

                int n = Mathf.Min(pts.Length, _points.Length);
                for (int k = 0; k < n; k++)
                {
                    float xR = pts[k].x / rig.HeadRadius;
                    float yR = (pts[k].y - rig.HeadCenterY) / rig.HeadRadius;
                    _points[k] = new Vector2(xR * pxPerR + offsetX, (yR - centerYInR) * pxPerR + offsetY);
                }

                float strokeWidth = baseStroke * AccessoryStroke.Multiplier(shape.StrokeMult);
                Color line;
                if (shape.IsHandoff)
                {
                    // 밑색 = 이 조각이 얹히는 앞 조각의 평면화된 채움(없으면 바탕). 인계본의 알파는 전부 이 위에서
                    // 정해졌으므로 여기서 미리 합성하면 화면에는 불투명색만 남는다.
                    int underIndex = i - shape.UnderBack;
                    Color under = shape.UnderBack > 0 && underIndex >= 0 && underIndex < _hasFlat.Length
                        && _hasFlat[underIndex] ? _flat[underIndex] : bg;

                    if (shape.Filled)
                    {
                        Color fill = Color.Lerp(under, AccessoryShapeBuilder.HandoffFillBase(shape, palette),
                            AccessoryShapeBuilder.HandoffFillAlpha(shape, body: false));
                        fill.a = 1f;
                        AddFill(root, shape.Name + "Fill", _points, n, fill);
                        if (i < _flat.Length)
                        {
                            _flat[i] = fill;
                            _hasFlat[i] = true;
                        }
                        drewAny = true;
                    }
                    if (shape.NoStroke) continue;

                    // 윤곽/낱선 = 잉크 C(채움×0.28 이 아니다 — §13-3-1) · 하이라이트 = 흰, 알파는 밑색 위 사전 합성.
                    line = Color.Lerp(under, AccessoryShapeBuilder.HandoffLineBase(shape, palette),
                        AccessoryShapeBuilder.HandoffLineAlpha(shape));
                    line.a = 1f;
                }
                else
                {
                    // v1 조각 — 채움은 몸의 색 표(역할 0/1/2) 그대로. 알파가 없어 사전 합성할 것도 없다.
                    Color color = AccessoryShapeBuilder.ResolveToneColor(_shapes, i, 0, primary, secondary);
                    line = color;
                    if (shape.Filled)
                    {
                        // 채움 먼저(윤곽선 아래) — 몸과 같은 규칙이다. 몸에서는 sortingOrder로, uGUI에서는
                        // <b>자식 순서</b>로 앞뒤가 정해지므로 면을 먼저 만들면 그것으로 충분하다.
                        AddFill(root, shape.Name + "Fill", _points, n, color);
                        drewAny = true;

                        // ★ 2026-09-06 — 여기가 「선화로 읽히는가」를 가르는 한 줄이다. 옛 값은
                        //   FillOutlineColor(color) = color × 0.28 이라 <b>조각마다 윤곽 색이 달랐고</b>,
                        //   어두운 카드 바탕 위에 어두운 윤곽이라 형태가 서지 않았다. 인계본과 같은 잉크 한 색으로 바꾼다.
                        line = palette.Ink;
                    }
                    if (shape.NoStroke) continue;
                }

                AddStrokes(root, shape, n, strokeWidth, line);
                drewAny = true;
            }
            return drewAny;
        }

        /// <summary>
        /// 슬롯 고정 배율로 놓았을 때 조각 전체가 아이콘 상자(<see cref="Frame.IconViewBox"/>)를 넘는가,
        /// 넘으면 얼마나 줄이고 밀어야 하는가. <b>줄이기만 한다</b>.
        /// </summary>
        private static BoxFit MeasureBoxFit(in AccessoryShapeBuilder.Rig rig, float unitsPerR, float centerYInR)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;
            for (int i = 0; i < _shapes.Count; i++)
            {
                Vector3[] pts = _shapes[i].Points;
                if (pts == null) continue;
                for (int k = 0; k < pts.Length; k++)
                {
                    float x = pts[k].x / rig.HeadRadius * unitsPerR;
                    float y = ((pts[k].y - rig.HeadCenterY) / rig.HeadRadius - centerYInR) * unitsPerR;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    any = true;
                }
            }
            if (!any) return BoxFit.Identity;

            const float half = Frame.IconViewBox * 0.5f;
            float span = Mathf.Max(maxX - minX, maxY - minY);
            float shrink = span > Frame.IconViewBox ? Frame.IconViewBox / span : 1f;
            return new BoxFit(shrink,
                Nudge(minX * shrink, maxX * shrink, half),
                Nudge(minY * shrink, maxY * shrink, half));
        }

        /// <summary>축소된 구간 [<paramref name="lo"/>, <paramref name="hi"/>]를 [−half, +half] 안으로 밀어 넣는
        /// 최소 이동량. 축소 뒤 구간 길이가 2·half 이하라 두 조건이 동시에 걸리지 않는다.</summary>
        private static float Nudge(float lo, float hi, float half)
        {
            if (lo < -half) return -half - lo;
            if (hi > half) return half - hi;
            return 0f;
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

        /// <summary>카드용 리그. 치수의 <b>비율</b>은 배율 1.0 프리팹 실측과 같다 — 조각 좌표는 이 리그의
        /// 머리 반경 R 단위로 되돌려진 뒤 슬롯 프레임에 놓이므로, 여기서 중요한 것은 비율뿐이다.</summary>
        internal static AccessoryShapeBuilder.Rig CardRig()
        {
            const float h = StickConfig.BaselineCharacterTotalHeight;
            const float r = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(r, h - r,
                AccessoryShapeBuilder.BaselineShoulderLocalY,
                AccessoryShapeBuilder.BaselineHipLocalY, 1f);
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
