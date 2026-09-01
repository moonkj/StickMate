using UnityEngine;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 이펙트(FX)/펫(PET) 도형의 <b>유일한 정의처</b> — 2026-08-30 사용자 신고
    /// ("캐릭터 설정창에서 발자국이나, 공 이런건 왼쪽 캐릭터에서 미리보기로 보여줘야하는데 안보여짐").
    ///
    /// ============================================================================
    /// 왜 생겼는가
    /// ============================================================================
    /// FX/펫은 <b>실시간 캐릭터 전용</b>으로 만들어졌다(발자국은 보폭마다, 공은 주인을 따라 구른다).
    /// 그래서 정보창 초상화에는 아예 붙어 있지 않았고 — 착용해도 액자에 아무 변화가 없었다.
    /// 초상화가 그 그림을 <b>정적으로 한 벌</b> 그리려면 점 좌표가 필요한데, 그 좌표를 초상화 쪽에
    /// 새로 적으면 Interaction/AccessoryShapeBuilder.cs가 생겨난 것과 똑같은 이중 정의가 된다
    /// ("공 모양을 고쳤는데 미리보기만 옛 모양"). 그래서 <b>점을 만드는 코드만</b> 여기로 모으고,
    /// 실시간 렌더러(CharacterFxRenderer/CharacterPetRenderer)와 초상화(CharacterPortraitStage)가
    /// 둘 다 이것만 부른다.
    ///
    /// ============================================================================
    /// 여기 있는 것 / 없는 것
    /// ============================================================================
    /// · 있는 것: <b>점 좌표</b>뿐이다. 전부 순수 계산이고 UnityEngine 오브젝트를 하나도 만들지 않는다.
    /// · 없는 것: 언제 터질지(트리거), 어디에 놓을지(월드 좌표), 얼마나 살지(수명), 어떤 색인지.
    ///   그건 부르는 쪽의 책임이다 — 실시간 렌더러와 정적 미리보기가 <b>바로 그 부분에서만</b> 다르다.
    ///
    /// 좌표 규약은 액세서리와 같다: 로컬 원점, +y 위, 월드유닛 절대 상수 0개(전부 인자의 배수).
    ///
    /// ============================================================================
    /// ★ 규칙 39-P — 입자형(FX)의 정원/보조색은 <b>카드에 걸고 월드 한 알엔 안 건다</b>
    /// ============================================================================
    /// (2026-09-01 리더 승인. docs/EQUIPMENT_SHAPE_SPEC_FXPET.md 3절)
    ///
    /// 37-6 규칙 5(아이템 하나는 도형 2~4개)와 규칙 3-2(보조색 정확히 1개)는 <b>착용 액세서리</b>를
    /// 위한 규칙이다. FX 5종은 한 알이 화면에 여럿 뜨는 <b>입자</b>라 사정이 다르다 — 한 알에 조각 둘을
    /// 넣으려면 알이 커져야 하는데, 발자국으로 산술하면 그것이 <b>불가능</b>하다:
    /// <code>
    ///   조각 하나의 잉크 사각형 ≥ 1.5 W = 0.516 R
    ///   두 조각이 안 붙으려면 중심 간격  ≥ 1.5 W = 0.516 R
    ///   -> 전체 길이 ≥ 0.516 × 3 = 1.548 R = 머리 지름의 78%
    /// </code>
    /// 반면 <b>카드는 이미 무리를 그리고 있다</b>(물방울 카드는 방울 3개, 발자국 카드는 자국 4개).
    /// 그것이 옳다 — 입자의 정체는 "한 알의 모양"이 아니라 <b>"여럿이 만드는 무늬"</b>이기 때문이다.
    ///
    /// 그래서: <b>정원(2~4)과 보조색(1개)은 FX 카드 그림에만 적용하고, 월드의 한 알에는 적용하지
    /// 않는다. 규칙 1(획 예산)은 카드와 월드 양쪽에 그대로 적용한다.</b>
    /// PET은 입자가 아니므로(항상 한 마리) 정원/보조색을 <b>그대로 지킨다</b>.
    ///
    /// 이 한 줄이 발자국·반짝임·먼지·물방울·나뭇잎 5종의 정원/보조색 문제를 동시에 닫는다.
    /// 검사는 Tests/EditMode/AppearanceShapeBudgetTests가 FX와 PET에 <b>다른 자</b>를 대는 형태로 든다.
    /// </summary>
    internal static class AppearanceShapeBuilder
    {
        // ---- 아이템 자리(Core/ItemCatalog.cs FX/PET 표의 순서). 실시간 렌더러와 초상화 미리보기가
        //      같은 상수를 봐야 "카드에서 고른 것"과 "그려지는 것"이 어긋나지 않는다.
        internal const int FxNone = 0, FxFootprint = 1, FxSparkle = 2, FxDust = 3;
        internal const int PetBall = 0, PetPlane = 1, PetMini = 2, PetCursor = 3;

        // ★ 2026-09-01 카테고리당 +2종 라운드의 <b>연출 미구현 4종</b>을 여기서 채운다.
        //   그 라운드는 카드(에셋)만 만들고 "준비 중인 자리"라고 정직하게 적어 두었다 — 이 파일과
        //   두 렌더러가 다른 작업자 소유였기 때문이다. 이 저장소의 확정 규칙
        //   ("착용했는데 화면이 그대로면 그건 착용이 아니다")에 걸리는 상태였고, 이번 라운드에서 해소했다.
        internal const int FxBubble = 4, FxLeaf = 5;
        internal const int PetBalloon = 4, PetSnail = 5;

        // ---- 실시간 렌더러가 쓰는 치수 중 초상화도 알아야 하는 것(같은 크기로 보여야 미리보기다).
        /// <summary>
        /// 반짝임 한 갈래의 길이(머리 반경 배수).
        ///
        /// <para>★ 2026-09-01 <b>0.34 -> 0.85</b> (docs/UX_FLOW.md 37-3 (F)(1) / 로드맵 P4).
        /// 옛 값은 배율 0.75에서 <b>1.98pt</b>였는데 그 배율의 FX 획이 <b>2.00pt</b>다 —
        /// 갈래 길이가 획 두께와 같으니 4갈래 반짝임이 아니라 <b>한 변 4pt짜리 통통한 십자 점</b>이었고,
        /// 갈래 끝 둥근 캡(반경 1pt)만으로 갈래 길이의 51%가 찼다.</para>
        ///
        /// <para>새 값은 획의 <b>2.47배</b>(4.95pt)라 갈래가 갈래로 읽힌다. 상한은 정수리다 —
        /// 발동 높이(<c>CharacterFxRenderer.SparkleHeightInR</c>)가 이 값에 맞춰 함께 올라간다.</para>
        ///
        /// <para>★ 2026-09-01 <b>0.85 -> 1.00</b>(docs/EQUIPMENT_SHAPE_SPEC_FXPET.md 4-2). 이것은
        /// <b>세로</b> 갈래 길이다 — 가로는 <see cref="SparkleHorizontalArmRatio"/>만큼 짧다.
        /// 옛 값은 가로·세로가 <b>정확히 같아서</b> 화면에 뜨는 그림이 반짝임이 아니라
        /// <b>더하기 기호</b>였다. 길이를 다르게 준 순간 십자가 별로 읽힌다.</para>
        /// </summary>
        internal const float SparkleArmInR = 1.00f;

        /// <summary>가로 갈래 ÷ 세로 갈래. 0.68이면 가로 갈래가 0.68R = <b>1.98획</b>이라 여전히
        /// 획에 안 먹히면서(규칙 1) 세로와의 실루엣 차가 1.86획 벌어진다 — 그 차이가 '＋'를 없앤다.</summary>
        internal const float SparkleHorizontalArmRatio = 0.68f;

        /// <summary>공의 반지름(신장 배수).</summary>
        internal const float BallRadiusInHeight = 0.055f;

        /// <summary>종이비행기 반폭(머리 반경 배수).
        /// <para>★ 2026-09-01 <b>0.75 -> 1.00</b>. 옛 값은 작은공과의 실루엣 차가 <b>0.92획</b>뿐이라
        /// 두 펫이 멀리서 같은 얼룩으로 보였다(규칙 6 하한 1.0획). 1.00에서 차이가 1.27획이 되고
        /// 몸 최단변도 1.53 -> <b>2.04획</b>이 된다.</para></summary>
        internal const float PlaneWingSpanInR = 1.00f;

        /// <summary>리틀스틱메이트의 키(주인 신장 배수).</summary>
        internal const float MiniScale = 0.45f;

        /// <summary>리틀스틱메이트의 엉덩이 높이(자기 키 배수) = 다리의 <b>수직</b> 길이이기도 하다
        /// (<see cref="MiniFigure"/>의 다리는 엉덩이에서 정확히 발바닥 높이 0까지 내려온다).
        /// 낙하 회전의 <b>회전 중심</b>과 무릎앉아의 <b>몸 내림 거리</b>가 둘 다 이 값에서 나오므로
        /// 상수를 여기 한 곳에만 둔다 — 도형과 연출이 서로 다른 숫자를 보면 발이 지면을 뚫는다.</summary>
        internal const float MiniHipRatio = 0.40f;

        /// <summary>리틀스틱메이트 다리 끝의 좌우 벌림(자기 키 배수). 무릎앉아의 몸 내림 거리
        /// <c>키·(MiniHipRatio·cosφ − MiniLegTipXRatio·sinφ)</c>에 들어간다.</summary>
        internal const float MiniLegTipXRatio = 0.10f;

        // ────────────────────────────────────────────────────────────────────────
        // ★ 2026-09-01 — 리틀스틱메이트 팔다리를 곧은 막대에서 **완만한 곡선**으로
        //   (사용자 신고: "펫도 캐릭터와 거의 동일하게 부드럽게 움직여야하는데 몸이 뚝딱거림")
        // ────────────────────────────────────────────────────────────────────────
        // 본체는 마디가 둘이라 무릎/팔꿈치를 원호로 갈아냈지만(States/LimbCurveRenderer.cs),
        // 펫의 팔다리는 **마디가 하나**다 — 즉 갈아낼 관절 자체가 없다. 그래서 같은 필렛을 그대로
        // 옮길 수 없고, 대신 마디 전체를 완만한 활로 굽혀 "곧은 막대"라는 인상을 없앤다.
        //
        // ★ 구조적 한계를 숨기지 않는다: 진짜 "본체와 동일한 부드러움"은 펫에도 무릎/팔꿈치가
        //   있어야 나온다. 그건 Interaction/CharacterPetRenderer가 마디당 Transform을 하나 더
        //   돌려야 하는 변경이라 이 파일만으로는 불가능하다(별도 라운드 필요).

        /// <summary>활의 볼록량 = 마디 길이의 이 배수(sagitta 비). 0.09면 화면상 배율 0.75에서
        /// 약 0.9pt — 획 두께(2pt)의 절반이라 "굽었다"가 읽히면서 실루엣은 안 무너진다.
        ///
        /// <para>★ 볼록 <b>방향</b>은 네 마디 모두 <b>진행 방향</b>이다. 실측 렌더로 세 안을 비교한
        /// 결과다: 몸통 바깥쪽으로 굽히면(각 마디가 자기 tipX 쪽) 두 다리가 서로 반대로 휘어
        /// <b>O자 다리</b>가 되고, 안쪽으로 굽히면 X자가 된다. 네 마디를 같은 쪽으로 굽히면
        /// 그 대칭 아티팩트가 원천적으로 생기지 않고 "한 방향으로 흐르는" 손그림 느낌이 난다.</para></summary>
        internal const float MiniLimbBowRatio = 0.09f;

        /// <summary>활 하나에 쓰는 점 개수(양 끝 포함).
        /// <para>본체는 마디당 5점인데 펫은 4점인 근거: 펫의 마디는 화면상 8~11pt(배율 0.75, Retina
        /// 16~22px)로 본체의 1/4이고 관절이 없어 총 회전각이 45도뿐이다. 4점(=3분할)이면 현(chord)
        /// 오차가 0.0036유닛 = <b>0.25 device px</b>로 획 두께(4px)의 1/16이라 육안 한계 아래다.
        /// 3점으로 줄이면 0.56px까지 올라 가장자리가 각져 보이기 시작하고, 5점으로 늘려도 그림이
        /// 같다(24시간 상주 앱이라 무의미한 정점은 늘리지 않는다).</para></summary>
        internal const int MiniLimbPoints = 4;

        /// <summary>커서 친구의 크기(머리 반경 배수).
        ///
        /// <para>★ 2026-09-01 <b>0.90 -> 1.40</b>. 옛 값에서는 8변 중 <b>5개가 0.47~0.97획</b>이라
        /// 화살표 꼬리가 통째로 획 하나에 먹혔다 — 전체 높이가 0.918R(5.34pt)인데 획이 2pt였다.
        /// 같은 화살표가 카드에서는 28.6px로 멀쩡했다(비율 15:1 대 2.7:1). 그것이 사용자가 말한
        /// "카드와 착용 모습이 다르다"의 정체다.</para>
        ///
        /// <para>유도: 이 실루엣의 최단 변 비율이 0.26 s이므로 0.26 s ≥ W(0.343864R)에서
        /// s ≥ 1.323R. 여유를 두어 1.40R로 잡으면 최단 변이 <b>1.06획</b>이 된다.</para>
        ///
        /// <para>클램프 영향: <c>CharacterPetRenderer.TickCursorFriend</c>의
        /// <c>ClampToScreen(_position, HeadRadius * CursorSizeInR)</c> 여백이 0.90R -> 1.40R
        /// (배율 0.75에서 5.2pt -> 8.1pt)로 커진다. 커서 이격(24pt)·화면 가장자리 뒤집기(24pt)보다
        /// 한참 작아 추격 연출은 그대로다.</para></summary>
        internal const float CursorSizeInR = 1.40f;

        // ============================================================================
        // ★ 신규 4종의 공용 치수 (2026-09-01) — 전부 37-6 규칙 1(획 예산)을 검산해 잡았다
        // ============================================================================
        // 출하 배율 0.75에서 획 W ≈ 0.344R이다(AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii).
        // 아래 값은 "가장 짧은 선분 ≥ 1.0 W", "내부를 보여야 하는 윤곽 도형의 지름 ≥ 3.0 W",
        // "구분돼야 하는 두 선의 간격 ≥ 1.5 W"를 손으로 검산한 결과이며, 그 검산을
        // Tests/EditMode/AccessoryStrokeBudgetTests가 도형 좌표에서 다시 잰다.

        /// <summary>물방울 하나의 기본 반지름 하한(머리 반경 배수). 지름 1.24R ≥ 3.0 W(1.03R)라
        /// 링 안쪽이 살아 있다 — 더 작으면 방울이 아니라 <b>까만 점</b>이 된다.
        ///
        /// <para>★ 2026-09-01 <b>0.58 -> 0.62</b>. 옛 값은 <b>발생 배율</b>
        /// (<c>CharacterFxRenderer.BubbleStartScale</c> = 0.90)을 빼먹고 잡은 값이었다 —
        /// 실제로 그려지는 가장 작은 방울은 0.58 × 0.90 = 0.522R이고, 그 반지름으로는
        /// <see cref="BubbleSegments"/>가 낼 수 있는 각수가 9각뿐인데 12각을 쓰고 있었다.
        /// 0.62에서 시작 반지름이 0.558R이 되어 9각형 변이 <b>1.11획</b>이고 지름은 3.25 W다.</para></summary>
        internal const float BubbleMinRadiusInR = 0.62f;

        /// <summary>물방울 하나의 기본 반지름 상한. 머리(1.0R)보다 확실히 작아야 "방울"로 읽힌다.</summary>
        internal const float BubbleMaxRadiusInR = 0.80f;

        /// <summary>먼지 초승달 한 쌍의 기준 반지름(머리 반경 배수).
        ///
        /// <para>★ 2026-09-01 신설. 값은 그대로(0.50)지만 <b>있던 자리가 없었다</b> — 호출부 두 곳
        /// (<c>CharacterFxRenderer.TickDust</c> / <c>CharacterPortraitStage.DrawFxPreview</c>)이 각자
        /// <c>r * 0.5f</c> 리터럴을 갖고 있어서, 이 파일에 사는 다른 FX 치수
        /// (<see cref="SparkleArmInR"/> · <see cref="LeafLengthInR"/> · <see cref="BubbleMinRadiusInR"/>)와
        /// 달리 <b>검사가 참조할 단일 정의처가 없었다</b>. 그 두 파일을 손대는 라운드에서 리터럴을
        /// 이 상수로 바꾼다(그때까지는 값이 같다는 것을 사람이 지켜야 한다).</para></summary>
        internal const float DustRadiusInR = 0.50f;

        /// <summary>나뭇잎 잎몸의 길이(머리 반경 배수). 가장 짧은 잎몸 선분이 0.342×길이이므로
        /// 1.15R에서 0.393R ≥ 1.0 W다(1.006R 미만이면 잎맥 없는 검은 덩어리가 된다).</summary>
        internal const float LeafLengthInR = 1.15f;

        /// <summary>풍선 주머니의 반지름(머리 반경 배수).</summary>
        internal const float BalloonRadiusInR = 0.80f;

        /// <summary>풍선 끈의 길이(머리 반경 배수). 원점(= 묶인 자리)에서 위로 이만큼 올라간 곳이 매듭이다.</summary>
        internal const float BalloonStringInR = 1.70f;

        /// <summary>달팽이의 기준 치수(머리 반경 배수 = 1R). 아래 세 도형이 전부 이 값의 배수다.</summary>
        internal const float SnailSizeInR = 1.0f;

        /// <summary>달팽이 껍데기 바깥 링의 반지름(<see cref="SnailSizeInR"/> 배수).
        /// <para>★ 2026-09-01 <b>0.68 -> 0.78</b>. 속점을 1.5 W까지 키운 뒤에도 링과 1.5 W 간격을
        /// 남기려면 0.26 + 0.516 ≤ r 이어야 한다. 덤으로 링 자신의 12각형 변이 <b>1.17획</b>이 된다
        /// (0.68R에서는 12각이 상한이라 여유가 0이었다).</para></summary>
        internal const float SnailShellRadiusRatio = 0.78f;

        /// <summary>껍데기 속 점의 반지름. 바깥 링과의 간격이 0.52R ≥ 1.5 W라 두 선이 붙어 보이지 않는다.
        ///
        /// <para>★ 2026-09-01 <b>0.15 -> 0.26</b>. 옛 값의 잉크 사각형은 <b>0.87획</b>이었다 —
        /// 규칙 1이 말하는 "그리려다 만 점"이고, 하필 <b>이 아이템의 유일한 식별 특징</b>
        /// (보조색 1개가 여기에만 쓰인다)이 획보다 작았다. 0.26이면 4각·위상 0°에서 폭이 2r =
        /// <b>1.51획</b>이다.</para></summary>
        internal const float SnailShellCoreRatio = 0.26f;

        /// <summary>
        /// 껍데기 중심(발 접지선 기준). 링 아랫변이 발 선과 <b>거의 정확히 만난다</b>(0.02R 아래).
        ///
        /// <para>이 값이 이 도형에서 가장 빠듯한 자리다. 위로 띄우면 껍데기가 <b>공중에 뜬 원</b>이 되고
        /// (37-6 규칙 4가 금지한 그림), 아래로 내리면 껍데기가 <b>땅 밑으로 잠긴다</b>. 두 획은 각각
        /// 반폭 0.5 W를 가지므로 중심선 거리가 그 안이면 잉크가 실제로 겹친다 — 그래서 "닿는다"의
        /// 판정 기준은 좌표가 아니라 <b>획 반폭</b>이다(Tests/EditMode/AppearanceShapeBudgetTests가 잠근다).</para>
        /// </summary>
        /// <para>★ 2026-09-01 <b>(-0.15, 0.66) -> (-0.30, 0.76)</b>. y는 껍데기를 키운 만큼
        /// 함께 올려 <b>닿음 계약을 그대로 유지</b>한다(0.76 - 0.78 = -0.02R, 옛 값과 같은 잠김량).
        /// x를 0.15R 더 물린 것은 산술이 아니라 <b>ASCII 래스터 육안 검증</b>에서 잡았다 —
        /// 껍데기를 키우자 더듬이가 껍데기에 붙어 중심선 간격이 1.12획(규칙 4의 최악 구간)이 됐다.
        /// 뒤로 물리면 더듬이 뿌리 1.52획 / 끝 1.57획으로 풀린다.</para>
        internal const float SnailShellCenterXRatio = -0.30f, SnailShellCenterYRatio = 0.76f;

        // ============================================================================
        // ★ 2026-09-01 — 원의 <b>각수(角數)는 반지름이 산다</b>
        // ============================================================================
        // 반지름 r(R 배수)인 정 n각형의 한 변은 2r·sin(π/n)이다. 그 변이 획 W를 넘으려면
        //
        //     n ≤ π / asin( W / (2r) )          W = ShippingStrokeBudgetInHeadRadii ≈ 0.344 R
        //
        // 옛 각수 12·14는 <b>액세서리 쪽에서 그대로 베껴 온 값</b>이다. 액세서리의 원(모자 관·방울)은
        // 반지름이 0.8~1.0R이라 12각을 살 수 있었지만 FX/PET의 원은 그보다 작다. 아무도 "이 반지름이
        // 이 각수를 살 수 있는가"를 확인하지 않아서, 물방울·공·껍데기·속점 <b>네 도형이 동시에</b>
        // 변 0.33~0.88획으로 그려지고 있었다.
        //
        // ★ 그런데 규칙 1 린트는 그것을 <b>한 줄도 찍지 않았다</b>: 그 검사는 "양끝이 45° 이상 꺾인 변"만
        //   재는데 정12각형의 꺾임은 30°, 정14각형은 25.7°라 전부 문턱 아래다. 이 함정은 폼폼 라운드가
        //   이미 발견해 AccessoryShapeBuilder에 적어 두었지만 그때 폼폼만 고치고 검사는 안 고쳤다.
        //   이번 라운드가 검사에 "꺾임과 무관한 최단 실제 변" 항목을 넣었다
        //   (Tests/EditMode/AppearanceShapeBudgetTests.DescribeShortestEdgeViolation).

        /// <summary>반지름 <paramref name="radiusInR"/>(머리 반경 배수)의 정다각형이 "모든 변 ≥ 1획"을
        /// 지킬 수 있는 최대 각수. 아래 각수 상수들이 이 값을 넘지 않는지는 테스트가 잰다 —
        /// 24시간 상주 앱이라 도형을 만들 때마다 asin을 부르지 않는다.</summary>
        internal static int MaxSegmentsForRadiusInR(float radiusInR, float strokeInR)
        {
            if (radiusInR <= 0f || strokeInR <= 0f) return 3;
            float ratio = Mathf.Min(1f, strokeInR / (2f * radiusInR));
            return Mathf.Max(3, Mathf.FloorToInt(Mathf.PI / Mathf.Asin(ratio)));
        }

        /// <summary>물방울 링의 각수. 실제로 그려지는 가장 작은 방울은 0.62 × 0.90 = <b>0.522R</b>이고
        /// 그 반지름의 상한이 9각이다(옛 값 12). 9각에서 변 <b>1.11획</b>.</summary>
        internal const int BubbleSegments = 9;

        /// <summary>공 링의 각수. 반지름 0.569R의 상한은 10이지만 9로 잡아 여유 13%를 남긴다(옛 값 12).
        /// 9각에서 변 <b>1.13획</b>.</summary>
        internal const int BallSegments = 9;

        /// <summary>달팽이 껍데기 링의 각수. 반지름 0.78R의 상한이 정확히 12다(옛 값 14).
        /// 12각에서 변 <b>1.17획</b>.</summary>
        internal const int SnailShellSegments = 12;

        /// <summary>껍데기 속점의 각수(옛 값 8). 반지름 0.26R로는 <b>4각</b>이 상한이다.
        /// 위상은 <see cref="Circle"/>의 기본값 0°여야 한다 — 45°로 돌리면 좌우 폭이 2r에서
        /// √2·r로 줄어 1.51획이 <b>1.07획</b>이 된다.</summary>
        internal const int SnailCoreSegments = 4;

        // ==================== FX ====================

        /// <summary>
        /// 채운 점 하나를 만드는 2점 선. <b>부르는 쪽이 선 두께를 <c>radius * 2</c>로 잡아야</b>
        /// 둥근 캡이 원이 된다(이 프로젝트에는 채움 도형 경로가 없다 — 굵은 캡이 곧 점이다).
        ///
        /// <para>★ <b>2026-09-01 미완</b>: 발자국은 이 둥근 점을 버리고 <b>옆에서 본 밑창</b>
        /// (열린 3점 (−0.40,+0.10) (−0.02,0) (+0.56,+0.04) R)이 돼야 한다. 지금 지름은 <b>1.19획</b>으로
        /// 규칙 1의 1.5획 문턱에 미달이고, 무엇보다 <b>옆에서 보는 이 앱에서 둥근 점은 발자국이 아니다</b>.
        /// 못 고친 이유는 좌표가 아니라 호출부다: <c>CharacterFxRenderer.BuildDot</c>이 넘겨주는
        /// <c>radius</c>는 R이 아니라 <b>획에서 파생된 값</b>(<c>Stroke * 0.9</c>)이고 선 두께도
        /// <c>radius * 2</c>(= 1.19획)로 못박는다. 밑창은 R 배수 좌표 + 보통 획 두께여야 하므로
        /// 그 파일이 함께 바뀌어야 하는데 이번 라운드의 편집 금지 대상이다.
        /// 면제 대장: Tests/EditMode/AppearanceShapeBudgetTests.</para>
        /// </summary>
        internal static Vector3[] DotSegment(float radius)
            => new[] { new Vector3(-radius * 0.05f, 0f, 0f), new Vector3(radius * 0.05f, 0f, 0f) };

        /// <summary>4갈래 반짝의 획 하나(<paramref name="index"/> 0 = 세로, 1 = 가로).
        /// <para><paramref name="arm"/>은 <b>세로</b> 갈래 길이다. 가로는
        /// <see cref="SparkleHorizontalArmRatio"/>배로 짧다 — 두 길이가 같으면 화면에 뜨는 그림이
        /// 반짝임이 아니라 <b>더하기 기호</b>이기 때문이다(39절 원칙 6: 반짝임은 비대칭 4갈래 별).</para></summary>
        internal static Vector3[] SparkleStroke(float arm, int index)
        {
            if (index == 0)
            {
                return new[] { new Vector3(0f, -arm, 0f), new Vector3(0f, arm, 0f) };
            }

            float across = arm * SparkleHorizontalArmRatio;
            return new[] { new Vector3(-across, 0f, 0f), new Vector3(across, 0f, 0f) };
        }

        /// <summary>먼지 초승달 하나(<paramref name="index"/> 0 = 큰 것, 1 = 위에 얹히는 작은 것).
        /// 착지 먼지(LandingDustRenderer)와 같은 어휘라 "먼지"로 바로 읽힌다.
        ///
        /// <para>★ 2026-09-01 분할 <b>5 -> 3</b>. 200°를 5등분하면 현이 0.71획 / 0.46획이라 두 초승달의
        /// 모든 변이 획에 먹혔다 — 그런데 5분할의 꺾임은 40°라 45° 문턱 아래여서 규칙 1 린트에
        /// <b>한 줄도 안 찍혔다</b>. 3등분(66.7°)에서 큰 초승달 변이 1.21획이 된다.</para>
        ///
        /// <para>★ 작은 초승달은 배수 0.65 -> <b>0.88</b>, 올림량 0.55 -> <b>0.80</b>이다. 배수는 변을
        /// 0.46 -> 1.06획으로 올리기 위한 것이고, 올림량은 <b>마루</b> 때문이다: 0.66이면 작은 쪽 마루가
        /// 큰 쪽보다 0.84획밖에 안 솟아 혹이 안 읽힌다(0.80에서 1.06획).</para>
        ///
        /// <para>★ 두 초승달의 중심선 최소 간격은 <b>0.48획</b>이고 이것은 <b>일부러</b>다.
        /// 규칙 4의 "0 또는 ≥1.5획"은 <b>따로 읽혀야 하는</b> 두 조각에 거는 규칙인데, 먼지는 반대로
        /// 한 덩어리의 울퉁불퉁한 구름이어야 한다(획 반폭이 각각 0.5획이라 실제로 잉크가 겹친다).
        /// <b>나중에 이것을 위반으로 보고 떼어 놓지 마라.</b></para>
        /// </summary>
        internal static Vector3[] DustCrescent(float radius, int index)
        {
            const int Segments = 3;
            var pts = new Vector3[Segments + 1];
            float rr = radius * (index == 0 ? 1f : 0.88f);
            float offsetY = index == 0 ? 0f : radius * 0.80f;
            for (int k = 0; k <= Segments; k++)
            {
                float a = Mathf.Lerp(-10f, 190f, k / (float)Segments) * Mathf.Deg2Rad;
                pts[k] = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.7f + offsetY, 0f);
            }
            return pts;
        }

        /// <summary>물방울 한 알의 테두리(닫힌 고리, 원점 중심). 방울은 <b>속이 보여야</b> 방울이므로
        /// 반지름 하한(<see cref="BubbleMinRadiusInR"/>)을 부르는 쪽이 지켜야 한다.</summary>
        internal static Vector3[] BubbleRing(float radius, int maxSegments)
            => Circle(0f, 0f, radius, Mathf.Min(maxSegments, BubbleSegments));

        /// <summary>나뭇잎 잎몸(닫힌 6점). 원점은 잎의 <b>중심</b>이고 +x가 잎끝이다.
        /// 좌우 대칭이라 회전만으로 팔랑임이 만들어진다(좌우 반전 재구성이 필요 없다).</summary>
        internal static Vector3[] LeafBlade(float length)
        {
            float l = length;
            return new[]
            {
                new Vector3(-0.50f * l, 0f, 0f),
                new Vector3(-0.20f * l, 0.26f * l, 0f),
                new Vector3(0.14f * l, 0.30f * l, 0f),
                new Vector3(0.50f * l, 0f, 0f),
                new Vector3(0.14f * l, -0.30f * l, 0f),
                new Vector3(-0.20f * l, -0.26f * l, 0f),
            };
        }

        /// <summary>나뭇잎 잎자루(열린 2점). 잎몸 뒤끝에서 이어지므로 <b>접점이 곧 부착</b>이다
        /// (37-6 규칙 4 — 떠 있는 조각을 만들지 않는다).
        /// <para>★ 2026-09-01 끝점 (−0.86, −0.16) -> <b>(−0.98, −0.24)</b>. 옛 잎자루는 길이는
        /// 1.32획으로 통과했지만 <b>잉크 사각형이 1.20획</b>이라 규칙 1의 1.5획 문턱에 걸렸다 —
        /// 화면에서는 잎몸 캡에 이어 붙은 뭉툭한 혹이었다. 새 값에서 사각형 1.61획 / 길이 1.79획.</para></summary>
        internal static Vector3[] LeafStem(float length)
            => new[] { new Vector3(-0.50f * length, 0f, 0f), new Vector3(-0.98f * length, -0.24f * length, 0f) };

        // ==================== PET ====================

        /// <summary>공의 테두리(닫힌 고리).</summary>
        internal static Vector3[] BallRing(float radius, int maxSegments)
            => Circle(0f, 0f, radius, Mathf.Min(maxSegments, BallSegments));

        /// <summary>원 하나(닫힌 고리). 공/물방울/달팽이 껍데기가 <b>같은 한 벌</b>을 쓴다 —
        /// 원을 그리는 코드가 세 벌이 되면 그 중 하나만 조용히 달라진다(이 프로젝트의 반복 실패 유형).</summary>
        private static Vector3[] Circle(float centerX, float centerY, float radius, int segments)
        {
            int n = Mathf.Max(3, segments);
            var ring = new Vector3[n];
            float step = Mathf.PI * 2f / n;
            for (int i = 0; i < n; i++)
            {
                ring[i] = new Vector3(centerX + Mathf.Cos(step * i) * radius,
                    centerY + Mathf.Sin(step * i) * radius, 0f);
            }
            return ring;
        }

        /// <summary>솔기가 테에서 가장 멀리 부푼 거리 ÷ 공 반지름. 0.4924면 부푼 양이 0.28R이고
        /// 세 점(위 테 · 부푼 마루 · 아래 테)을 지나는 원의 반지름이 0.7176R이 된다.</summary>
        internal const float BallSeamBulgeRatio = 0.4924f;

        /// <summary>솔기 하나에 쓰는 점 개수. 4점(3분할)에서 변이 1.25획이다 — 5점으로 늘리면
        /// 변이 0.94획으로 떨어져 <b>같은 실수를 반대편에서</b> 반복하게 된다.</summary>
        private const int BallSeamPoints = 4;

        /// <summary>
        /// 공의 <b>솔기</b>(구의 큰 원이 옆에서 보이는 완만한 호). 회전을 읽히게 하는 요소다.
        ///
        /// <para>★ 2026-09-01 — 옛 도형은 중심에서 테로 뻗는 <b>반지름 선</b>이었다. 그건 공이 아니라
        /// <b>바퀴</b>의 어휘다(바큇살). 공을 공으로 읽게 하는 것은 솔기이고, 솔기는 부피를 뜻한다.
        /// 회전 가독성은 그대로다 — 비대칭이라 오히려 더 낫다.</para>
        ///
        /// <para>양 끝점이 <b>테 위에 정확히 얹히므로</b> 링과의 간격이 0이다
        /// (37-6 규칙 4의 "0 또는 ≥1.5획" 중 0 쪽 — 떠 있는 조각이 아니다).</para>
        /// </summary>
        internal static Vector3[] BallSeam(float radius)
        {
            float r = radius;
            float bulge = r * BallSeamBulgeRatio;
            if (r <= 0f || bulge <= 0f) return new[] { Vector3.zero, new Vector3(r, 0f, 0f) };

            // 세 점 (0,−r) (bulge,0) (0,+r)을 지나는 원.
            float arcRadius = (r * r + bulge * bulge) / (2f * bulge);
            float centerX = bulge - arcRadius;
            float half = Mathf.Asin(Mathf.Min(1f, r / arcRadius));

            var pts = new Vector3[BallSeamPoints];
            for (int i = 0; i < BallSeamPoints; i++)
            {
                float t = -half + 2f * half * i / (BallSeamPoints - 1);
                pts[i] = new Vector3(centerX + Mathf.Cos(t) * arcRadius, Mathf.Sin(t) * arcRadius, 0f);
            }
            return pts;
        }

        /// <summary>★ 옛 이름의 별칭 — 호출부(<c>CharacterPetRenderer.BuildBall</c>)가 이번 라운드의
        /// <b>편집 금지 파일</b>이라 이름만 남겨 두었다. 그쪽을 손대는 라운드에서 호출부를
        /// <see cref="BallSeam"/>으로 바꾸고 이 줄을 지운다.</summary>
        internal static Vector3[] BallSpoke(float radius) => BallSeam(radius);

        /// <summary>종이비행기 외곽(닫힌 4점) — icon-paths.json의 실루엣.</summary>
        internal static Vector3[] PlaneBody(float halfSpan)
        {
            float w = halfSpan;
            return new[]
            {
                new Vector3(w, 0f, 0f),
                new Vector3(-w * 0.75f, w * 0.62f, 0f),
                new Vector3(-w * 0.42f, 0f, 0f),
                new Vector3(-w * 0.75f, -w * 0.62f, 0f),
            };
        }

        /// <summary>종이비행기의 <b>용골(keel) 접힘선</b>(열린 2점).
        /// <para>★ 2026-09-01 3점 -> 2점. 옛 3번째 점이 만드는 변
        /// <c>(−0.42w, 0) → (−0.75w, −0.62w)</c>은 <see cref="PlaneBody"/>의 변과 <b>완전히 같은
        /// 두 점</b>이었다 — 잉크만 두 번 얹히고 새 정보가 0이다. 종이의 어휘는
        /// "완벽히 곧은 모서리 + 접힌 자국 하나"다.</para></summary>
        internal static Vector3[] PlaneFold(float halfSpan)
        {
            float w = halfSpan;
            return new[]
            {
                new Vector3(w, 0f, 0f),
                new Vector3(-w * 0.42f, 0f, 0f),
            };
        }

        /// <summary>리틀스틱메이트의 선 6개(머리 원 / 몸통 / 팔 2 / 다리 2). 원점은 <b>발바닥</b>.
        /// <b>순서는 계약이다</b> — 실시간 렌더러가 인덱스 2~5(팔뒤/팔앞/다리뒤/다리앞)를 뿌리 기준으로
        /// 돌려 보행 스윙·낙하 만세·무릎앉아를 만든다(CharacterPetRenderer.ApplyMiniLimbDeltas).</summary>
        internal static Vector3[][] MiniFigure(float height, float facing)
        {
            float h = height;
            float r = h * 0.14f;
            float headY = h - r;
            float shoulderY = h * 0.72f;
            float hipY = h * MiniHipRatio;
            float f = facing >= 0f ? 1f : -1f;

            var head = new Vector3[12];
            float step = Mathf.PI * 2f / 12;
            for (int i = 0; i < 12; i++)
            {
                head[i] = new Vector3(Mathf.Cos(step * i) * r, headY + Mathf.Sin(step * i) * r, 0f);
            }

            return new[]
            {
                head,
                new[] { new Vector3(0f, headY - r, 0f), new Vector3(0f, hipY, 0f) },
                Limb(shoulderY, -h * 0.10f * f, h * 0.30f, f),
                Limb(shoulderY, h * 0.14f * f, h * 0.30f, f),
                Limb(hipY, -h * MiniLegTipXRatio, h * MiniHipRatio, f),
                Limb(hipY, h * MiniLegTipXRatio, h * MiniHipRatio, f),
            };
        }

        /// <summary>
        /// 리틀스틱메이트의 마디 하나 — 뿌리에서 끝까지를 <b>완만한 원호</b>로 잇는다
        /// (<see cref="MiniLimbBowRatio"/> / <see cref="MiniLimbPoints"/> 문서 참고).
        ///
        /// <para><b>양 끝점은 곧은 막대였을 때와 정확히 같다.</b> 이것이 계약이다:</para>
        /// <list type="bullet">
        /// <item>첫 점 (0, rootY) — <c>CharacterPetRenderer.MakeLine</c>이 이 점을 오브젝트 위치로
        ///       옮겨 <b>스윙 회전축</b>으로 쓴다.</item>
        /// <item>마지막 점 (tipX, rootY−length) — <c>LimbNeutralDegrees</c>가 이 점으로 마디의
        ///       기본 각도를 실측하고, 다리의 경우 이 y가 <b>발바닥 높이 0</b>이라 접지 계산이
        ///       여기 얹혀 있다(<see cref="MiniHipRatio"/> 문서).</item>
        /// </list>
        /// <para>즉 이 변경은 순수하게 <b>중간 모양</b>만 바꾼다 — 펫의 자세 계산/접지/스윙 코드는
        /// 한 줄도 건드리지 않는다.</para>
        ///
        /// <para>볼록 방향은 <paramref name="facing"/>(진행 방향)이며 네 마디가 전부 같다 —
        /// 근거는 <see cref="MiniLimbBowRatio"/> 문서의 O자/X자 반증이다.</para>
        /// </summary>
        private static Vector3[] Limb(float rootY, float tipX, float length, float facing)
        {
            var root = new Vector3(0f, rootY, 0f);
            var tip = new Vector3(tipX, rootY - length, 0f);

            float dx = tip.x - root.x, dy = tip.y - root.y;
            float chord = Mathf.Sqrt(dx * dx + dy * dy);
            if (chord < 1e-5f || MiniLimbPoints < 3)
            {
                return new[] { root, tip };
            }

            // 현의 수직 방향 중 +x 쪽 = (−dy, dx)/chord (dy < 0 이므로 x성분이 양수).
            float nx = -dy / chord, ny = dx / chord;
            float side = facing >= 0f ? 1f : -1f;
            float sagitta = chord * MiniLimbBowRatio;

            var points = new Vector3[MiniLimbPoints];
            points[0] = root;
            points[MiniLimbPoints - 1] = tip;
            // 2차 베지어의 제어점을 현 중점에서 2·sagitta 만큼 밀면 t=0.5에서 정확히 sagitta가 된다
            // (원호와의 차이는 이 곡률에서 0.1% 미만이라 4점 표본에서는 구분되지 않는다).
            var control = new Vector3(
                (root.x + tip.x) * 0.5f + nx * side * sagitta * 2f,
                (root.y + tip.y) * 0.5f + ny * side * sagitta * 2f, 0f);
            for (int i = 1; i < MiniLimbPoints - 1; i++)
            {
                float t = i / (float)(MiniLimbPoints - 1);
                float u = 1f - t;
                points[i] = u * u * root + 2f * u * t * control + t * t * tip;
            }
            return points;
        }

        // ---- 풍선(펫 4번). 원점은 <b>끈이 묶인 자리</b>다 — 그래야 Transform 회전 하나로
        //      "끈에 매달려 흔들리는" 그림이 성립한다(주머니를 원점에 두면 끈이 몸을 뚫는다).

        /// <summary>풍선 끈(열린 5점, 원점에서 위로). 가장 짧은 선분이 0.43R ≥ 1.0 W다.</summary>
        internal static Vector3[] BalloonString(float r)
        {
            float s = r * BalloonStringInR;
            return new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.10f * r, 0.247f * s, 0f),
                new Vector3(-0.08f * r, 0.500f * s, 0f),
                new Vector3(0.09f * r, 0.753f * s, 0f),
                new Vector3(0f, s, 0f),
            };
        }

        /// <summary>풍선 주머니(닫힌 12점 타원). <b>첫 점이 매듭</b>이라 끈 끝점과 정확히 만난다 —
        /// 두 도형이 상수를 공유하므로 크기를 바꿔도 매듭이 벌어지지 않는다.</summary>
        internal static Vector3[] BalloonBody(float r)
        {
            float radius = r * BalloonRadiusInR;
            float centerY = r * BalloonStringInR + radius;
            const int Segments = 12;
            var pts = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float a = (-90f + i * (360f / Segments)) * Mathf.Deg2Rad;
                pts[i] = new Vector3(Mathf.Cos(a) * radius * 0.92f, centerY + Mathf.Sin(a) * radius, 0f);
            }
            return pts;
        }

        // ---- 달팽이(펫 5번). 원점은 <b>땅에 닿는 자리</b>이고 +x가 진행 방향이다
        //      (비대칭이라 좌우 반전은 리틀스틱메이트와 같이 도형 재구성으로 처리한다).

        /// <summary>달팽이의 발 + 더듬이(열린 5점, 한 획). 꼬리 -> 배 -> 머리 -> 더듬이가
        /// 한 번에 이어져 도형 개수를 늘리지 않는다(37-6 규칙 5의 정원 2~4개).</summary>
        internal static Vector3[] SnailFoot(float size, float facing)
        {
            float f = facing >= 0f ? 1f : -1f;
            float s = size;
            return new[]
            {
                new Vector3(-0.95f * s * f, 0.10f * s, 0f),
                new Vector3(-0.50f * s * f, 0f, 0f),
                new Vector3(0.50f * s * f, 0f, 0f),
                new Vector3(0.92f * s * f, 0.30f * s, 0f),
                new Vector3(1.02f * s * f, 0.70f * s, 0f),
            };
        }

        /// <summary>달팽이 껍데기 바깥 링(닫힌 고리).</summary>
        internal static Vector3[] SnailShell(float size, float facing, int maxSegments)
            => Circle((facing >= 0f ? 1f : -1f) * SnailShellCenterXRatio * size,
                SnailShellCenterYRatio * size, SnailShellRadiusRatio * size,
                Mathf.Min(maxSegments, SnailShellSegments));

        /// <summary>껍데기 속의 점 — 이 아이템을 형제들과 가르는 <b>단 한 부분</b>이라 보조색은 여기에만 쓴다
        /// (37-6 규칙 3-2). 카드 아이콘의 작은 원과 같은 자리다.</summary>
        internal static Vector3[] SnailShellCore(float size, float facing, int maxSegments)
            => Circle((facing >= 0f ? 1f : -1f) * SnailShellCenterXRatio * size,
                SnailShellCenterYRatio * size, SnailShellCoreRatio * size,
                Mathf.Min(maxSegments, SnailCoreSegments));

        /// <summary>
        /// 커서 친구 — 화살표 실루엣(원점이 <b>화살표 끝점</b>, 아래로 뻗는다. 마지막 점이 첫 점과
        /// 같아 열린 선으로도 닫혀 보인다 — 부르는 쪽이 <c>loop:false</c>다).
        ///
        /// <para>★ 2026-09-01 좌표 재설계. 옛 실루엣은 8변 중 <b>5개가 0.47~0.97획</b>이었다
        /// (<see cref="CursorSizeInR"/> 문서의 진단). 크기만 키우면 비율은 그대로이므로 <b>비율 자체</b>를
        /// 다시 잡았다 — 최단 변 비율이 0.26 s가 되도록 꼬리 폭을 넓히고 목을 짧게 했다.
        /// s = 1.40R에서 모든 변이 <b>1.06획 이상</b>이고 자기교차가 없다(전체 1.09R × 1.48R).</para>
        ///
        /// <para>★ <b>여기가 이번 라운드의 미완이다</b>: 스펙(docs/EQUIPMENT_SHAPE_SPEC_FXPET.md 4-2)은
        /// 이 한 획을 <b>머리(주색) + 꼬리(보조색) 두 조각</b>으로 쪼갠다. 그래야 규칙 5(정원 2~4)와
        /// 규칙 3-2(보조색 정확히 1개)가 함께 닫히고, 기하가 뭉개지는 크기에서도 꼬리가 <b>색으로</b>
        /// 읽힌다. 쪼개려면 <c>CharacterPetRenderer.BuildCursorFriend</c>가 <see cref="LineRenderer"/>를
        /// <b>두 개</b> 만들어야 하는데 그 파일이 이번 라운드의 편집 금지 대상이라 미뤘다.
        /// 쪼개는 자리는 이 배열의 <b>2번 점과 5번 점</b>이다(머리 = 0·1·2·5·6, 꼬리 = 2·3·4·5).
        /// 그 미완은 Tests/EditMode/AppearanceShapeBudgetTests의 면제 대장에 한 줄로 적혀 있다.</para>
        /// </summary>
        internal static Vector3[] CursorArrow(float size)
        {
            float s = size;
            return new[]
            {
                new Vector3(0f, 0f, 0f),                            // 0 촉끝
                new Vector3(0f, -s, 0f),                            // 1 왼쪽 어깨
                new Vector3(s * 0.26f, -s * 0.74f, 0f),             // 2 ★ 머리/꼬리 분기점
                new Vector3(s * 0.42f, -s * 1.06f, 0f),             // 3 꼬리 바깥
                new Vector3(s * 0.66f, -s * 0.96f, 0f),             // 4 꼬리 끝
                new Vector3(s * 0.50f, -s * 0.64f, 0f),             // 5 ★ 머리/꼬리 분기점
                new Vector3(s * 0.78f, -s * 0.62f, 0f),             // 6 오른쪽 어깨
                new Vector3(0f, 0f, 0f),
            };
        }
    }
}
