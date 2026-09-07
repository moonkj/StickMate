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
    /// ★ <b>2026-09-06 문장 정정</b> — 이 규칙은 "FX는 보조색을 <b>안 쓴다</b>"가 아니다.
    /// 정확히는 <b>"FX는 입자 한 알을 보조색 때문에 쪼개지 않는다"</b>이고, <b>이미 두 조각이고
    /// 그 간격이 0인 경우는 예외</b>다. 나뭇잎이 그 예외다 — 잎몸과 잎자루는 위 산술과 무관하게
    /// 원래부터 두 조각이었고 접점을 공유한다(<see cref="LeafStem"/>). 거기에 색을 다르게 주는 것은
    /// 알을 키우지 않으므로 39-P의 근거를 하나도 건드리지 않는다. 반대로 색을 안 주면
    /// <b>카드에는 있는 갈색 잎자루가 착용하면 사라진다</b>(이 저장소가 반복해 고친 결함 형태).
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
        /// <para>★ 2026-09-01 <b>0.85 -> 1.00</b>(docs/EQUIPMENT_SHAPE_SPEC_FXPET.md 4-2).</para>
        ///
        /// <para>★ 2026-09-06 — 이제 이것은 <see cref="SparkleStar"/>의 <b>바깥 정점 반경</b>이다
        /// (십자 2획 시절의 "세로 갈래 길이"가 아니다). 가로/세로를 다른 길이로 준 옛 처방
        /// (<c>SparkleHorizontalArmRatio</c> = 0.68)은 '＋'를 면하려던 임시방편이었고, 윤곽 별이
        /// 되면서 <b>오목 정점</b>(<see cref="SparkleConcaveRatio"/>)이 그 일을 대신하므로 삭제했다.</para>
        /// </summary>
        internal const float SparkleArmInR = 1.00f;

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
        /// 달리 <b>검사가 참조할 단일 정의처가 없었다</b>.</para>
        ///
        /// <para>★ 2026-09-06 — 그 두 리터럴을 이 상수로 바꿨다(값 0.5 그대로라 화면은 한 픽셀도
        /// 안 바뀐다). 이제 이 숫자를 고치면 월드와 미리보기가 <b>함께</b> 따라온다.</para></summary>
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
        /// 발자국 한 짝 — <b>위에서 본 밑창을 옆에서 보이는 세계에 눕힌 닫힌 6점</b>. 원점은 발
        /// 한가운데, <c>+x</c>가 <paramref name="facing"/>(진행 방향, 뒤꿈치→발가락)이고
        /// <paramref name="size"/>는 머리 반경이다.
        ///
        /// <para>★ 2026-09-07 — 인계본 새 디자인(design/equipment/verify/r20_coords.txt
        /// <c>look.fx.footprint</c>, (다)군)이 <b>위에서 본 발바닥(112점) + 엄지(24점)</b>로
        /// 카드를 이미 갈았는데, 이 함수(월드/착용 실물)는 옛 3점 옆모습 그대로였다 — 사용자가
        /// 반복 신고한 "카드는 바뀌었는데 실제로 보면 그대로다"의 발자국 쪽 정체.</para>
        ///
        /// <para><b>112점을 그대로 못 옮기는 이유(실측)</b> — 이 항목들은 전부
        /// <c>ShippingStrokeBudgetInHeadRadii</c>(배율 0.75에서 <b>0.344R</b>) 이상인 변만 허용한다
        /// (37-6 규칙 1, Tests/EditMode/AppearanceShapeBudgetTests가 잠근다). 카드의 발 길이는
        /// 64u 상자에서 약 51u ≈ 3.2R 상당인데, 옛 발자국의 실제 크기(<paramref name="size"/> = 머리
        /// 반경 1개)로 그 실루엣을 그대로 축소하면 112개 변의 평균 길이가 <b>0.02R</b> 안팎이 되어
        /// 규칙 1을 수백 곳에서 어긴다(획 하나에 다 먹힌다). 카드처럼 확대하면 발자국이 머리보다 커진다.
        /// 그래서 <b>같은 절대 크기 안에서 표현 가능한 최대 정점 수</b>(획 예산이 사는 상한, 6점 —
        /// <see cref="MaxSegmentsForRadiusInR"/>와 같은 산술을 다각형 둘레에 적용한 값)로 실루엣을
        /// 다시 그렸다. 별도 "엄지" 도형은 포기했다 — 독립 도형이 뚱뚱한 점이 아니려면 잉크 사각형이
        /// 1.5획(0.52R) 이상이어야 하는데 그러면 엄지가 발 전체 길이(0.96R대)의 절반을 넘는다.
        /// 대신 <b>앞쪽(발가락 쪽)을 위로 볼록하게</b> 몰아 "엄지가 있는 쪽"이라는 비대칭만 살렸다.</para>
        ///
        /// <para><b>변환</b>: 카드 64u(y 아래) → R 배수(머리 중심 원점, y 위, 1R=16u)로 되돌린 뒤
        /// 발의 <b>길이축</b>(카드에서는 세로, 엄지가 위쪽)을 세계의 <b>+x</b>(진행 방향)로 90도
        /// 돌리고, <b>폭축</b>을 세계의 <b>y</b>로 삼아 옛 설계와 같은 "땅에 눕는 얇은 자국"으로
        /// 짓눌렀다(옆에서 보는 이 앱에서 위에서 본 발을 그대로 세우면 땅에서 뜬 혹처럼 보인다).
        /// 6점은 실루엣에서 곡률이 큰 지점(뒤꿈치·좌우 볼·앞쪽 볼록·발가락)만 남긴 것이다.</para>
        ///
        /// <para><b>부르는 쪽 계약</b>: 선 두께는 <b>보통 획</b>(<c>RenderStroke</c>)이고, 닫힌 고리
        /// (<c>loop:true</c>)다 — 옛 3점 열린 선과 달리 이제 <b>윤곽이 스스로 닫힌다</b>(카드가 이미
        /// <c>loop=1</c>이었다). 최단 변 <b>1.09획</b>, 잉크 사각형 <b>3.5획</b>(37-6 규칙 1 여유 확보).</para>
        /// </summary>
        internal static Vector3[] FootSole(float size, float facing)
        {
            float f = facing >= 0f ? 1f : -1f;
            float s = size;
            return new[]
            {
                new Vector3(-0.60f * s * f, 0.06f * s, 0f),   // 뒤꿈치
                new Vector3(-0.15f * s * f, 0.11f * s, 0f),   // 뒤꿈치→볼 (위쪽 곡선)
                new Vector3(0.25f * s * f, 0.14f * s, 0f),    // 볼 + 엄지 비대칭 (가장 볼록한 지점)
                new Vector3(0.62f * s * f, 0.09f * s, 0f),    // 발가락 끝
                new Vector3(0.20f * s * f, 0.02f * s, 0f),    // 볼→뒤꿈치 (아래쪽 곡선)
                new Vector3(-0.20f * s * f, 0.03f * s, 0f),   // 아치
            };
        }

        /// <summary>오목 정점의 반경 ÷ 바깥 정점의 반경. 0.34면 별의 변이 <b>2.32획</b>이고
        /// 중심에 지름 1.75획짜리 구멍이 남는다.
        ///
        /// <para>★ 그 구멍은 <b>알려진 트레이드오프</b>다(리더 확인 2026-09-06). 더 오목하게 하면
        /// (비율↓) 구멍이 커지고, 덜 오목하게 하면(비율↑) 별이 팔각형으로 뭉개진다. 0.34는
        /// 카드 아이콘(<c>look_fx_sparkle.asset</c>)이 이미 쓰고 있는 값이라 카드와 착용 모습이
        /// 같은 그림이 된다 — 그 asset의 실측 비율이 4.681 / 13.76 = 0.3402다.</para></summary>
        internal const float SparkleConcaveRatio = 0.34f;

        /// <summary>
        /// 반짝임 — <b>윤곽 별 한 도형</b>(닫힌 8점). 바깥 4정점이 <paramref name="arm"/>,
        /// 오목 4정점이 <c>arm × <see cref="SparkleConcaveRatio"/></c>다.
        ///
        /// <para>★ 2026-09-06 — 옛 도형은 <b>십자 2획</b>(세로 획 + 가로 획)이었다. 가로를 0.68배로
        /// 줄여 '＋'는 면했지만 <b>별로 읽히지는 않았다</b>: 획 두 개가 겹친 그림에는 별의 정체인
        /// <b>오목한 허리</b>가 없기 때문이다. 윤곽 하나로 그리면 그 허리가 생기고, 덤으로 도형 수가
        /// 2 -> 1이 되어 규칙 39-P(입자 한 알은 2개 이하)에 여유가 생긴다.</para>
        ///
        /// <para>검산(<c>arm = 1.0R</c>, 배율 0.75): 여덟 변 전부 <b>2.32획</b>, 잉크 사각형 <b>5.82획</b>.
        /// 부르는 쪽은 <c>loop: true</c>다.</para>
        /// </summary>
        internal static Vector3[] SparkleStar(float arm)
        {
            float inner = arm * SparkleConcaveRatio;
            var pts = new Vector3[8];
            for (int k = 0; k < 4; k++)
            {
                float outerAngle = (90f + 90f * k) * Mathf.Deg2Rad;
                float innerAngle = (135f + 90f * k) * Mathf.Deg2Rad;
                pts[k * 2] = new Vector3(Mathf.Cos(outerAngle) * arm, Mathf.Sin(outerAngle) * arm, 0f);
                pts[k * 2 + 1] = new Vector3(Mathf.Cos(innerAngle) * inner, Mathf.Sin(innerAngle) * inner, 0f);
            }
            return pts;
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

        /// <summary>나뭇잎 잎몸(닫힌 6점). 원점은 <b>뒤끝</b>(잎자루가 붙는 자리, index 0)이고
        /// +x가 잎끝(index 3)이다.
        ///
        /// <para>★ 2026-09-07 — 인계본 새 디자인(r20_coords.txt <c>look.fx.leaf</c>, (다)군)이
        /// "대칭 6점"에서 <b>비대칭 64점</b>으로 갈렸는데 이 함수는 옛 대칭 그대로였다. 64점은
        /// <see cref="FootSole"/> 문서와 같은 이유로 이 크기(<see cref="LeafLengthInR"/> = 1.15R)에서
        /// 그대로 못 옮긴다(획 예산 0.344R당 변 하나, 둘레 ≈2.3R이면 최대 6~7변). 그래서 <b>점 개수는
        /// 그대로 6</b>, 대신 위쪽 볼록(잎맥이 몰린 쪽, index 1·2)과 아래쪽 볼록(index 4·5)을
        /// <b>서로 다르게</b> 틀었다 — 옛 도형은 상하가 완전한 거울상이었지만 새 디자인은 아니다
        /// (실측: 위쪽 마루가 아래쪽 마루보다 앞으로 치우치고 살짝 더 높다). 그래서 이제
        /// <b>회전만으로는 좌우(상하) 반전이 안 된다</b> — 팔랑이는 잎은 원래 몸 전체가 도는 것이라
        /// (<c>CharacterFxRenderer.TickLeaves</c>의 <c>SpinDegrees</c>) 이 비대칭이 오히려 회전할 때
        /// "같은 잎이 계속 돈다"는 인상을 준다(완전 대칭이면 180도마다 그림이 똑같아 안 도는 것처럼
        /// 보일 수 있었다).</para>
        ///
        /// <para>뒤끝(index 0, (−0.50l,0))과 잎끝(index 3, (0.50l,0))은 옛 값 그대로다 —
        /// <see cref="LeafStem"/>이 index 0에 접점을 두므로 여기를 옮기면 잎자루가 떨어진다
        /// (Tests/EditMode/AppearanceShapeBudgetTests의 접점 계약 테스트가 잠근다).</para>
        ///
        /// <para>최단 변 <b>1.09획</b>(1.15R 적용, 옛 산술과 같은 자로 검산), 잉크 사각형 <b>2.9획</b>.</para>
        /// </summary>
        internal static Vector3[] LeafBlade(float length)
        {
            float l = length;
            return new[]
            {
                new Vector3(-0.50f * l, 0f, 0f),
                new Vector3(-0.22f * l, 0.25f * l, 0f),
                new Vector3(0.10f * l, 0.31f * l, 0f),
                new Vector3(0.50f * l, 0f, 0f),
                new Vector3(0.16f * l, -0.28f * l, 0f),
                new Vector3(-0.18f * l, -0.20f * l, 0f),
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

        /// <summary>솔기 호 하나가 테에서 가장 멀리 부푼 거리(연속 곡선 기준) ÷ 공 반지름.
        ///
        /// <para>★ 2026-09-07 — 인계본 새 디자인(r20_coords.txt <c>look.pet.ball</c>, (다)군)의
        /// 카드는 솔기가 <b>둘</b>이고(<c>ball.S1</c>·<c>ball.S2</c>) <b>대칭</b>이다 — 중심 원(반지름
        /// r)에서 양쪽으로 똑같이 7.5/23 = <b>0.3261</b>만큼 부푼다(실측: 카드 64u에서 공 중심 (32,32)
        /// 반지름 23, 솔기 마루 x=39.50/24.50 → |32−39.50|/23 = 0.3261). 옛 값 0.4924는 실물이 한쪽으로만
        /// 부푼 <b>비대칭</b> 단일 호였던 시절의 산출값이다.</para>
        ///
        /// <para>★ <b>그런데 카드 비율(0.3261)을 그대로 쓰면 획 예산 위반이 실측됐다</b> — 이 상수는
        /// <b>연속 곡선</b>의 마루(원호 apex, t=0)를 뜻하는데, <see cref="BallSeam"/>은 호 하나를
        /// <see cref="BallSeamPoints"/>(4점, 3분할) <b>표본</b>으로만 찍는다. 4점 표본에서는 t=0이 표본에
        /// 없고 가장 가까운 표본이 t=±half/3이라, <b>실제로 그려지는 마루가 이론값보다 얕다</b>(옛
        /// 단일 호도 같은 문제를 안고 있었다 — "마루는 원호의 apex(0.28R)가 아니라 0.247R" 문서가
        /// 그 사례다). 카드 비율 0.3261을 그대로 넣으면 반지름 0.5687R에서 <b>실측 마루 0.164R =
        /// 0.478획</b>으로 규칙 1의 획 반폭(0.5획) 문턱에 못 미친다
        /// (Tests/EditMode/AppearanceShapeBudgetTests.공의_솔기는_테_위에_정확히_얹힌다 실측 실패).
        /// 그래서 <b>표본 마루가 문턱을 확실히 넘도록</b> 0.40으로 올렸다 — 실측 마루
        /// 0.201R = <b>1.17획</b>(17% 여유). 카드의 "대칭 두 호"라는 <b>형태</b>는 그대로 지키고,
        /// 부푼 <b>양</b>만 이 크기의 획 예산에 맞춰 다시 잡았다 — 이 파일의
        /// <see cref="BubbleMinRadiusInR"/>·<see cref="CursorSizeInR"/> 등도 같은 이유(카드/원안
        /// 값이 이 절대 크기에서 획 예산을 못 산다)로 실측 후 올린 전례다.</para>
        ///
        /// <para><see cref="BallSeam"/>이 이 비율로 <b>테를 관통하는 두 호</b>(렌즈 모양 닫힌 고리)를
        /// 만든다.</para>
        /// </summary>
        internal const float BallSeamBulgeRatio = 0.40f;

        /// <summary>호 하나(테에서 테까지)에 쓰는 점 개수. 4점(3분할)에서 변이 육안 산술상 충분하다
        /// (아래 <see cref="BallSeam"/> 잉크 사각형 문서 참고) — 5점으로 늘리면 변이 짧아져
        /// <b>같은 실수를 반대편에서</b> 반복하게 된다.</summary>
        private const int BallSeamPoints = 4;

        /// <summary>
        /// 공의 <b>솔기</b> — 이제 <b>테를 관통하는 닫힌 렌즈 고리 1개</b>(6점)다. 회전을 읽히게 하는
        /// 요소이자, 이 알의 <b>유일한 보조색 자리</b>(37-6 규칙 3-2 "정확히 1개")를 지킨다.
        ///
        /// <para>★ 2026-09-01 — 옛 도형은 중심에서 테로 뻗는 <b>반지름 선</b>이었다("바큇살"). 공을
        /// 공으로 읽게 하는 것은 솔기이지 바큇살이 아니다.</para>
        ///
        /// <para>★ 2026-09-07 — 인계본 새 디자인은 솔기가 <b>둘</b>(양쪽 대칭, <see cref="BallSeamBulgeRatio"/>
        /// 문서)이다. 두 호를 <b>별개 도형</b>으로 만들면 이 알에 보조색 조각이 2개가 되어 규칙 3-2
        /// ("정확히 1개")를 어긴다 — PET은 입자가 아니라 그 규칙에 예외가 없다(FX의 39-P와 다르다).
        /// 그래서 두 호를 <b>양 극점(남/북 테)을 공유하는 하나의 닫힌 고리</b>로 잇는다: 남극→(+x 부푼
        /// 호)→북극→(−x 부푼 호)→남극. 결과는 눈(eye) 또는 렌즈 모양의 <b>겹선</b>이고, 실제로는 두
        /// 호를 <b>둘 다</b> 그리면서도 도형 수·보조색 수는 옛 설계와 똑같이 1개다.</para>
        ///
        /// <para>극점 두 개(index 0·3)가 <b>테 위에 정확히 얹히므로</b> 링과의 간격이 0이다
        /// (37-6 규칙 4의 "0 또는 ≥1.5획" 중 0 쪽 — 떠 있는 조각이 아니다). 6점 전부의 최단 변이
        /// <b>1.20획</b>, 표본 마루(실제로 그려지는 부푼 양)가 <b>1.17획</b>이다(반지름 0.5687R 기준
        /// 검산 — <see cref="BallSeamBulgeRatio"/> 문서의 "표본 vs 연속 곡선" 참고).</para>
        /// </summary>
        internal static Vector3[] BallSeam(float radius)
        {
            float r = radius;
            float bulge = r * BallSeamBulgeRatio;
            if (r <= 0f || bulge <= 0f) return new[] { Vector3.zero, new Vector3(r, 0f, 0f) };

            // 세 점 (0,−r) (bulge,0) (0,+r)을 지나는 원 — 한쪽으로 부푼 호(南極→北極).
            float arcRadius = (r * r + bulge * bulge) / (2f * bulge);
            float centerX = bulge - arcRadius;
            float half = Mathf.Asin(Mathf.Min(1f, r / arcRadius));

            var arc = new Vector3[BallSeamPoints];
            for (int i = 0; i < BallSeamPoints; i++)
            {
                float t = -half + 2f * half * i / (BallSeamPoints - 1);
                arc[i] = new Vector3(centerX + Mathf.Cos(t) * arcRadius, Mathf.Sin(t) * arcRadius, 0f);
            }

            // 거울호(−x로 부푼 호)를 북극에서 남극으로 되짚어 이어 붙인다. 양 끝(남/북극)은
            // 공유점이라 한 번만 들어간다 — 그래서 4+4가 아니라 4+(4−2)=6점이다.
            var lens = new Vector3[BallSeamPoints + (BallSeamPoints - 2)];
            for (int i = 0; i < BallSeamPoints; i++) lens[i] = arc[i];
            for (int i = 0; i < BallSeamPoints - 2; i++)
            {
                Vector3 mirrored = arc[BallSeamPoints - 2 - i];
                lens[BallSeamPoints + i] = new Vector3(-mirrored.x, mirrored.y, 0f);
            }
            return lens;
        }

        // ★ 2026-09-06 — 옛 이름 별칭 <c>BallSpoke</c>를 지웠다(호출부 둘을 <see cref="BallSeam"/>으로
        //   바꾸면서). 별칭이 남아 있던 이유는 그 두 파일이 편집 금지였기 때문이고, 그 사유는 끝났다.
        //   "바큇살(spoke)"이라는 낱말이 남아 있으면 다음 사람이 도형을 다시 반지름 선으로 되돌린다.

        /// <summary>종이비행기 <b>윗날개</b>(닫힌 3점, 주색). 원점은 <b>기수(코)</b> — 궤도 접선 회전이
        /// 여기(로컬 +x)를 진행 방향으로 돌린다(<c>CharacterPetRenderer.TickPlane</c>).
        ///
        /// <para>★ 2026-09-07 — 인계본 새 디자인(r20_coords.txt <c>look.pet.plane</c>, (다)군)은 옛
        /// "좌우 대칭 나비형 4점"이 아니라 <b>접은 종이비행기</b>다: 윗날개(<c>plane.B0</c>, M)와
        /// 아랫날개·용골(<c>plane.B1</c>, M2)이 <b>기수와 척추선(코→접힘점)을 공유</b>하는 서로 다른
        /// 두 조각이다. 옛 <c>PlaneFold</c>(용골, 열린 2점 장식선)를 지우고 그 자리에 아랫날개
        /// <b>본체</b>를 넣는다 — 장식선 하나였던 자리가 이제 실제 조각이 되어 잉크가 한 번만 얹힌다.</para>
        ///
        /// <para><b>변환</b>: 카드 64u 좌표의 기수(60,10)→척추 끝(24,34) 벡터를 로컬 −x축에 맞춰
        /// 돌린 뒤, 기수-척추 거리가 <b>1.50w</b>가 되도록 등비 축소했다(옛 <c>PlaneBody</c>의
        /// 기수-후단 거리 1.42w보다 살짝 크다 — 아랫날개·용골의 가장 빠듯한 변을 37-6 규칙 1
        /// 문턱 위로 올리는 데 필요한 최소 여유다. <see cref="PlaneWingSpanInR"/> 자체는 그대로
        /// 1.00R이라 절대 크기 규모는 유지된다). 기수는 이제 정확히 (w,0)이라 옛 계약과 같다.</para>
        ///
        /// <para>최단 변 <b>2.1획</b>, 잉크 사각형 <b>5.7획</b>(37-6 규칙 1 여유 큼).</para></summary>
        internal static Vector3[] PlaneWing(float halfSpan)
        {
            float w = halfSpan;
            return new[]
            {
                new Vector3(w, 0f, 0f),
                new Vector3(-w * 0.9615f, w * 0.5576f, 0f),
                new Vector3(-w * 0.50f, 0f, 0f),
            };
        }

        /// <summary>종이비행기 <b>아랫날개·용골</b>(닫힌 4점, 보조색). <see cref="PlaneWing"/>과
        /// 기수(이 함수의 index 0 = <see cref="PlaneWing"/>의 index 0)·척추 끝(이 함수의 index 1 =
        /// <see cref="PlaneWing"/>의 index 2)을 <b>정확히 공유</b>한다 — 접힌 종이 한 장의 두 면이라
        /// 떨어져 있으면 안 된다(37-6 규칙 4의 간격 0 쪽).
        ///
        /// <para>★ 2026-09-07 — 카드 <c>plane.B1</c>을 같은 등비로 옮겼다. 옛
        /// <c>PlaneFold</c>(열린 2점, 장식용 접힘선)를 대신하는 <b>실제 조각</b>이라
        /// 종이비행기가 한쪽만 채워진 나비가 아니라 진짜 <b>접은 종이</b>로 읽힌다.</para>
        ///
        /// <para>최단 변 <b>1.08획</b>(가장 빠듯한 변), 잉크 사각형 <b>5.1획</b>.</para></summary>
        internal static Vector3[] PlaneKeel(float halfSpan)
        {
            float w = halfSpan;
            return new[]
            {
                new Vector3(w, 0f, 0f),
                new Vector3(-w * 0.50f, 0f, 0f),
                new Vector3(-w * 0.2884f, -w * 0.3077f, 0f),
                new Vector3(-w * 0.75f, -w * 0.75f, 0f),
            };
        }

        /// <summary>리틀스틱메이트의 선 6개(머리 원 / 몸통 / 팔 2 / 다리 2). 원점은 <b>발바닥</b>.
        /// <b>순서는 계약이다</b> — 실시간 렌더러가 인덱스 2~5(팔뒤/팔앞/다리뒤/다리앞)를 뿌리 기준으로
        /// 돌려 보행 스윙·낙하 만세·무릎앉아를 만든다(CharacterPetRenderer.ApplyMiniPose →
        /// RebuildMiniLimb — 2026-09-03 정정, 옛 이름 <c>ApplyMiniLimbDeltas</c>는 존재한 적이 없다).</summary>
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

        // ────────────────────────────────────────────────────────────────────────
        // ★ 커서 친구 — 원점이 <b>화살표 촉끝</b>이고 아래로 뻗는다. 두 조각 다 닫힌 고리다.
        // ────────────────────────────────────────────────────────────────────────
        //
        // ★ 2026-09-01 좌표 재설계. 옛 실루엣은 8변 중 5개가 0.47~0.97획이었다(CursorSizeInR 문서의
        //   진단). 크기만 키우면 비율은 그대로이므로 비율 자체를 다시 잡았다 — 최단 변 비율이
        //   0.26 s가 되도록 꼬리 폭을 넓히고 목을 짧게 했다.
        //
        // ★ 2026-09-06 — 그 한 획(닫힌 8점)을 <b>머리 + 꼬리</b>로 쪼갰다. 스펙
        //   (docs/EQUIPMENT_SHAPE_SPEC_FXPET.md 4-2)이 원래 요구하던 형태이고, 이것으로
        //   37-6 규칙 5(정원 2~4개)와 규칙 3-2(보조색 정확히 1개)가 함께 닫힌다. 쪼갠 자리는
        //   옛 배열의 2번·5번 점이다(머리 = 0·1·2·5·6, 꼬리 = 2·3·4·5) — 두 조각이 그 두 점을
        //   <b>공유</b>하므로 간격이 0이고, 37-6 규칙 4의 "0 또는 ≥1.5획" 중 0 쪽이다.
        //
        //   왜 색이 필요한가: 기하가 뭉개지는 크기에서도 꼬리가 <b>색으로</b> 읽힌다. 카드
        //   (Resources/Items/look_pet_cursor.asset)가 이미 그렇게 두 줄로 그려져 있었다 —
        //   머리 tone 0(주색) / 꼬리 tone 1(보조색). 그 카드 좌표와 아래 좌표는 배율 23.18배
        //   차이로 <b>소수 넷째 자리까지 일치</b>한다.
        //
        //   검산(s = 1.40R, 배율 0.75): 머리 최단 변 1.06획 · 꼬리 최단 변 1.06획 ·
        //   두 조각 다 자기교차 0 · 꼬리 잉크 사각형 1.71획.

        /// <summary>커서 친구의 <b>머리</b>(닫힌 5점, 주색). 부르는 쪽이 <c>loop:true</c>다.</summary>
        internal static Vector3[] CursorHead(float size)
        {
            float s = size;
            return new[]
            {
                new Vector3(0f, 0f, 0f),                            // 촉끝
                new Vector3(0f, -s, 0f),                            // 왼쪽 어깨
                new Vector3(s * 0.26f, -s * 0.74f, 0f),             // ★ 꼬리와 공유하는 점
                new Vector3(s * 0.50f, -s * 0.64f, 0f),             // ★ 꼬리와 공유하는 점
                new Vector3(s * 0.78f, -s * 0.62f, 0f),             // 오른쪽 어깨
            };
        }

        /// <summary>커서 친구의 <b>꼬리</b>(닫힌 4점, 보조색). 첫 점과 마지막 점이
        /// <see cref="CursorHead"/>의 2·3번 점과 <b>정확히 같다</b> — 그 공유가 곧 부착이다.</summary>
        internal static Vector3[] CursorTail(float size)
        {
            float s = size;
            return new[]
            {
                new Vector3(s * 0.26f, -s * 0.74f, 0f),             // ★ 머리와 공유하는 점
                new Vector3(s * 0.42f, -s * 1.06f, 0f),             // 꼬리 바깥
                new Vector3(s * 0.66f, -s * 0.96f, 0f),             // 꼬리 끝
                new Vector3(s * 0.50f, -s * 0.64f, 0f),             // ★ 머리와 공유하는 점
            };
        }
    }
}
