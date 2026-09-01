using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 액세서리 도형의 <b>유일한 정의처</b> — 2026-08-30 정보창 리디자인 라운드에 분리했다.
    ///
    /// ============================================================================
    /// 왜 분리했는가
    /// ============================================================================
    /// 이번 라운드에 정보창 초상화가 <b>같은 모자·망토를 한 벌 더</b> 그리게 됐다. 도형 정의를 두 곳에
    /// 두면 "망토 모양을 고쳤는데 초상화만 옛 모양으로 남는" 사고가 난다 — 이 프로젝트가 이미 두 번
    /// 겪은 이중 정의 계열 실패(BUG-P1-R4-B1 씬 지면 Y, BUG-P1-R5-B2 Dock 구간)와 같은 뿌리다.
    /// 그래서 <b>점 좌표를 만드는 코드 자체</b>를 여기 한 곳에 두고,
    /// Interaction/CharacterAccessoryRenderer.cs(실제 캐릭터)와
    /// Interaction/CharacterPortraitStage.cs(초상화 미니 피규어)가 둘 다 이것만 호출한다.
    ///
    /// ============================================================================
    /// 좌표 규약 (원본 렌더러의 규약을 그대로 옮겼다)
    /// ============================================================================
    /// · 로컬 원점은 <b>발바닥</b>, +y가 위(StickmanBlackboard.SenseGround의 프로젝트 공통 규약).
    /// · 도형은 "진행 방향 기준"으로 정의하고 <b>x에만</b> facing 부호를 곱한다
    ///   (localScale.x = -1 뒤집기를 쓰지 않는 이유: 선 두께/캡까지 뒤집혀 미세하게 다른 그림이 된다).
    /// · 월드유닛 절대 상수는 하나도 없다 — 전부 머리 반경 R 또는 몸통 길이의 배수다.
    ///   그래서 characterScale이 바뀌어도 액세서리만 뒤에 남지 않는다
    ///   (회귀 테스트: Tests/PlayMode/CharacterAccessoryScaleTests.cs).
    /// </summary>
    internal static class AccessoryShapeBuilder
    {
        // ==================== 비율 상수 (여기가 유일한 정의처) ====================

        // 모자(캡) — 머리 반경 R 배수.
        // ★ 육안 검증 1회차(배율 0.75, 머리 반경 화면상 약 16pt)에서 챙 선 0.45R / 관 높이 0.78R은
        //   관 안쪽 여백이 획 두께보다 얇아 **머리 전체가 까맣게 메워진 덩어리**로 보였다.
        //   관을 높이고(1.05R) 챙을 눈 위로 올려(0.62R) 안쪽이 비도록 고쳤다.
        internal const float HatBrimLineRatio = 0.62f;
        internal const float HatCrownHeightRatio = 1.05f;
        internal const float HatCrownHalfWidthRatio = 0.80f;
        internal const float HatBrimReachRatio = 1.95f;
        internal const float HatBrimDropRatio = 0.16f;

        /// <summary>챙 <b>뿌리</b>(관 쪽 끝)의 두께. ★ 2026-09-01 규칙 1 위반 수정 — 옛 값 0.10R은
        /// 챙을 닫는 변(4-&gt;0)이 <b>0.29획</b>이라, 양끝이 모두 꺾임인 그 변이 획 하나에 통째로 먹혀
        /// 뒤쪽 끝이 뭉개져 있었다(37-6 규칙 1의 "그리려다 만 점").
        /// <para>획(0.344R) 하나에 10% 여유를 둔 값이다(실측 1.11획). 방향은 <b>아래</b>다 —
        /// 관은 챙선 위에만 있으므로 관을 파고들지 않고, 실루엣도 한 구간도 움직이지 않는다
        /// (프로파일 72구간 전부 소수점 아홉 자리까지 동일. 모자 15쌍 최소 차 2.948685획 유지).</para>
        /// <para>왜 "뒤로 길게"가 아니라 "두껍게"인가: 변을 뒤로 눕혀 길이만 채우면 챙 뿌리는 여전히
        /// 0.10R짜리 얇은 띠라 <b>화면에서는 그대로 선 하나</b>다. 린트만 통과하고 결함은 남는다.</para></summary>
        internal const float HatBrimRootDropRatio = 0.38f;

        // ============================================================================
        // ★ EYES — 2026-09-01 "불투명 바이저(가리개)"로 전면 재설계
        //   (docs/UX_FLOW.md 38-7 옵션 E2 + 리더 승인. 상세 유도는 AppendEyes 위 문단)
        // ============================================================================
        // 옛 값(렌즈 오프셋 0.44 / 반폭 0.32 / 반높이 0.19)은 <b>눈동자가 렌즈 안으로 비치는</b>
        // 그림을 전제로 잡은 좌표였다. 같은 날 눈이 삭제되면서(SceneBootstrapper.BakeEyes = false)
        // 그 전제가 통째로 사라졌고, 남은 것은 "얼굴 위에 그은 빈 네모" 4개였다.
        // 아래 값은 전부 <b>가릴 것이 없어도 스스로 불투명한 판</b>을 만드는 좌표다.
        internal const float GlassesCenterRatio = 0.00f;

        /// <summary>안경류가 <b>진행 반대쪽</b>으로 뻗는 끝(귀 위). 값은 옛 안경다리에서 그대로 왔고,
        /// <see cref="CharacterAccessoryRenderer"/>의 <c>GlassesTempleTipLocalX</c> 프로퍼티와
        /// <c>CharacterAccessoryScaleTests</c>가 이 상수를 읽는다 — <b>지우면 안 된다</b>.
        /// 지금은 선글라스 바이저의 관자놀이 다리 끝이 정확히 이 x다(프로퍼티가 여전히 참이다).</summary>
        internal const float GlassesTempleReachRatio = 1.02f;

        // ============================================================================
        // ★ 2026-09-01 (2차) — "카드 단독 판독" 라운드. 바이저 1장을 <b>렌즈 2장 + 코다리</b>로 바꾼다.
        // ============================================================================
        // 리더 육안 검증: 선글라스 카드가 <b>오른쪽을 가리키는 화살표</b>로 읽혔다(Tasklist V1).
        // 원인은 좌표가 아니라 <b>조형 언어</b>였다 — 앞이 길고 뒤가 짧은 비대칭 판 1장은 얼굴에
        // 얹혔을 때만 바이저로 읽히고, 카드(<see cref="AccessoryCardIcon"/>)는 머리 없이 도형만
        // 그리므로 맥락이 사라지면 그냥 화살촉이다. 게다가 같은 비대칭이 실물에서도 V5(부리처럼
        // 머리 밖으로 튀어나온 끝)·V6(머리에서 떨어져 뜬 안경다리)를 만들고 있었다.
        //
        // 그래서 <b>안경이라는 물건의 최소 신호</b>로 되돌린다: 좌우 렌즈 2장 + 그 둘을 잇는 코다리.
        // 이 신호는 머리가 없어도 성립하고(카드), 얼굴 위에서도 성립한다(실물).
        //   · 관자놀이 다리는 <b>없앴다</b>. 머리 원 반경이 1.0R이라 다리가 실제로 그려질 수 있는
        //     길이는 0.06R(획의 17%)뿐이고, 그보다 길게 그리면 반드시 머리 밖 허공에 뜬다(V6).
        //   · 두 렌즈는 <b>앞쪽이 7% 크다</b>(<see cref="SunglassFrontBiasRatio"/>) — 방향을 잃지
        //     않으면서 화살표로는 읽히지 않는 최소한의 비대칭이다(대칭성 지표 실측 0.059).
        internal const float SunglassInnerRatio = 0.27f;      // 코다리가 걸리는 안쪽 변
        internal const float SunglassInnerTopRatio = 0.32f;
        internal const float SunglassInnerBottomRatio = -0.40f;
        internal const float SunglassOuterRatio = 0.88f;      // 바깥 변(머리 원 안에 머문다)
        internal const float SunglassOuterTopRatio = 0.30f;
        internal const float SunglassOuterBottomRatio = -0.26f;

        /// <summary>앞쪽 렌즈를 이만큼 키운다. 1.0이면 완전 대칭이라 방향이 사라지고,
        /// 크게 잡으면 다시 화살표가 된다. 앞 렌즈 바깥 꼭짓점이 머리 원(1.0R) <b>안</b>에 남는
        /// 상한이기도 하다 — 0.88×1.07 = 0.9416R, 꼭짓점 반경 0.995R.</summary>
        internal const float SunglassFrontBiasRatio = 1.07f;

        /// <summary>코다리(보조색)가 두 렌즈 안쪽 꼭대기를 잇는 아치의 높이.</summary>
        internal const float SunglassBridgeRiseRatio = 0.40f;

        // ==================== 목(NECK) 부착 기준선 — 2026-08-30 사용자 신고 수정 ====================
        // 신고: "넥타이도 착용하면 목 좀 아래쪽에 나와야 하는데 얼굴 아래쪽에 배치되고".
        //
        // 실측으로 확인한 원인(추측 아님). 배율과 무관한 비율로 유도된다:
        //   · 턱(=머리 링 아래 끝) = HeadCenterY − R,  어깨 = 턱 − 0.07·bodyScale
        //   · R = 0.22·bodyScale  ->  드러난 목 길이 = 0.07/0.22 = <b>0.318 R</b> (아주 짧다)
        //   · 옛 기준선 BowTieDropRatio 1.15R = 턱보다 0.15R 아래 = 목의 위쪽 47% 지점
        //   · 나비넥타이 반높이 0.30R -> 위 끝 = 턱보다 <b>0.15R 위</b> = 얼굴(머리 링) 안으로 파고든다
        // 즉 "얼굴 아래에 붙어 있다"는 지적은 정확했고, 도형이 실제로 턱을 넘어가 있었다.
        //
        // 고침: 기준선을 <b>어깨선(목 밑동)</b>에서 유도한다 — 망토 옷깃(CapeCollarLocalY)이 이미 쓰고 있는
        // 것과 <b>같은 좌표계 규약</b>이다. 모자/안경이 머리 중심에서 유도되는 것과 같은 이유로, 목에
        // 걸치는 물건은 목이 몸통과 만나는 선에서 유도되어야 배율/비율이 바뀌어도 제자리에 남는다.
        // 여기에 나비넥타이 반높이를 0.30R -> 0.26R로 줄여, 어깨선 기준에서도 위 끝(0.30R)이 턱(0.318R)
        // 아래에 머무는 것을 산술로 보장한다.
        internal const float NeckCollarRiseRatio = 0.04f;
        internal const float BowTieHalfWidthRatio = 0.68f;
        internal const float BowTieHalfHeightRatio = 0.26f;
        internal const float BowTieKnotRatio = 0.13f;

        // ==================== 망토 — 2026-08-30 사용자 신고로 실루엣 재설계 ====================
        // 신고: "망토가 좀 캐릭터에 펼쳐져서 착용이 되어야하는데 그냥 짐같이 디자인되어있음".
        //
        // 실측으로 확인한 원인(추측 아님). 배율 0.75(R=0.165, 몸통=0.6225) 기준:
        //   · 옛 값의 세로 길이 = 몸통×1.35 = 0.840
        //   · 옛 값의 가로 폭   = 앞 0.40R ~ 뒤 −1.35R = 0.289
        //   -> 세로:가로 = 2.9 : 1. 즉 <b>어깨에 매달린 좁고 긴 띠</b>였다. "펼쳐진 천"이 아니라
        //      등에 멘 보따리로 읽히는 것이 당연하다.
        //
        // 고침: 길이는 그대로 두고(회귀 테스트가 "밑단은 고관절보다 아래"를 잠그고 있다) <b>밑단만
        // 넓힌다</b>. 옷깃은 어깨 너비 그대로, 밑단은 그 3배 이상으로 벌어지는 <b>사다리꼴</b>이 된다.
        //   · 새 가로 폭 = 앞 0.85R ~ 뒤 −2.45R = 0.545  ->  세로:가로 = 1.5 : 1
        // 천이 <b>아래로 갈수록 넓어진다</b>는 것이 눈에 보이는 것이 이 재설계의 유일한 목표다.
        internal const float CapeCollarRiseRatio = 0.10f;
        internal const float CapeCollarFrontRatio = 0.40f;
        internal const float CapeCollarBackRatio = 0.62f;
        internal const float CapeLengthRatio = 1.35f;

        /// <summary>밑단이 <b>진행 반대쪽</b>으로 뻗는 거리(머리 반경 배수). 옛 값 1.35 -> 2.45.</summary>
        internal const float CapeSpreadRatio = 2.45f;

        /// <summary>밑단이 <b>진행 방향쪽</b>으로도 벌어지는 거리. 이게 없으면 천이 한쪽으로만
        /// 날리는 깃발이 되어 "걸쳤다"로 읽히지 않는다(옛 도형에는 아예 없던 값이다).</summary>
        internal const float CapeFrontSpreadRatio = 0.85f;

        internal const float CapeHemWaveRatio = 0.22f;

        // ============================================================================
        // ★ 2026-08-30 외부 핸드오프 32종 확장 (docs/UX_FLOW.md 33-2 ~ 33-4)
        // ============================================================================
        // 아래 상수는 전부 33절이 못박은 값을 그대로 옮긴 것이다. 여기서 새로 만든 숫자는 하나도 없다.
        // 규약은 확장 전과 동일하다: 월드유닛 절대 상수 0개, 전부 R(머리 반경) 또는 TorsoLength 배수.

        // ---- 레이어(sortingOrder) — 33-2-0의 재배치표. 확장 전에는 액세서리 4종이 전부 6으로 뭉쳐 있어
        //      겹칠 때 그리기 순서가 미정이었다. 캐릭터 획 0~3 / 머리 링 4 / 눈동자 5는 프리팹이 소유한다.
        /// <summary>
        /// 망토·날개·배낭 — 몸통 선 <b>뒤</b>.
        /// <para>★ 33-2-0의 표는 이 값을 <c>2</c>로 적었지만 그대로 쓸 수 없다. 프리팹의 실제
        /// sortingOrder를 재 보면 <b>뒤쪽 팔다리 0 / 몸통 1 / 앞쪽 팔다리 2 / 머리 링 4 / 눈동자 5</b>다
        /// (Editor/SceneBootstrapper.cs의 CreateLineSegmentVisual 호출 인자). 즉 2는 몸통(1)보다
        /// <b>앞</b>이고 앞쪽 팔다리와는 동률이라, 표가 적어둔 목적("몸통 선 뒤로 내린다")과 정반대의
        /// 그림 — 가슴 위를 덮는 망토 — 이 나온다. 동률은 그리기 순서가 미정이라는 문제도 그대로다.
        /// 그래서 <b>의도를 구현한다</b>: 캐릭터가 쓰는 최솟값(0)보다 하나 아래인 −1이면 어떤 획보다도
        /// 확실히 뒤이고 동률도 없다. (리더 보고 대상 — 표의 숫자가 아니라 표의 목적을 따랐다.)</para>
        /// </summary>
        internal const int SortBack = -1;
        internal const int SortHair = 6;
        internal const int SortNeck = 7;
        internal const int SortEyes = 8;    // 눈동자 위 — 선글라스를 쓰면 눈동자가 렌즈 뒤로 간다.

        /// <summary>모자. 2026-08-30 채움 면이 생기면서 9 -> 10으로 한 칸 올렸다 — 채움은
        /// <c>SortingOrder − 1</c>(=9)에 깔리므로, 옛 값 9를 그대로 두면 채움이 안경(8)과 동률이 되어
        /// 그리기 순서가 미정이 된다(이 프로젝트가 33-2-0에서 이미 한 번 정리한 함정).</summary>
        internal const int SortHead = 10;
        /// <summary>확장 전 액세서리 4종이 공유하던 값. <c>AddLine</c>의 기본 인자로 남겨 기존 호출부를 무변경으로 지킨다.</summary>
        internal const int SortDefault = 6;

        // ---- 아이템 자리(Core/ItemCatalog.cs 표의 순서). 이 상수들이 표와 어긋나면 엉뚱한 도형이 나오므로
        //      Tests/PlayMode/CharacterAccessoryScaleTests가 아이디 문자열로 32종 전부를 대조해 잠근다.
        internal const int HeadCap = 0, HeadBeanie = 1, HeadFedora = 2, HeadCrown = 3;
        internal const int EyesSunglasses = 0, EyesRound = 1, EyesGoggles = 2, EyesMonocle = 3;
        internal const int NeckBowTie = 0, NeckStriped = 1, NeckScarf = 2, NeckBell = 3;
        internal const int BackCape = 0, BackLongCape = 1, BackWings = 2, BackBackpack = 3;
        internal const int HairCowlick = 0, HairNeat = 1, HairCurly = 2, HairBald = 3;

        // ---- 2026-09-01 카테고리당 +2종(캐러셀 검증용 <b>임시 플레이스홀더</b>, 리더 보고 완료).
        //      전부 <b>형제 도형의 변형</b>이다 — 새 조형 언어를 만들지 않는다. 자리 번호는 표(에셋)의
        //      itemIndex와 같은 값이어야 하고, 그 대조는 AccessoryShapeCatalogTests가 아이디로 잠근다.
        internal const int HeadBeret = 4, HeadStraw = 5;
        internal const int EyesBrowline = 4, EyesPatch = 5;
        internal const int NeckPendant = 4, NeckBandana = 5;
        internal const int BackPoncho = 4, BackFairyWings = 5;
        internal const int HairBowl = 4, HairPonytail = 5;

        // ---- 눈 중립 좌표(33-3-2). 프리팹 실측치를 **나누기 전 형태로** 남긴다 —
        //      0.341이라고만 적어두면 그 숫자가 어디서 왔는지 다음 사람이 알 수 없다.
        //      Editor/SceneBootstrapper가 굽는 배율 1.0 기준값과 같은 출처이며,
        //      Interaction/CharacterPortraitStage.cs도 여기서 파생시켜 이중 정의를 만들지 않는다.
        internal const float BaselineHeadVisualRadius = 0.22f;

        /// <summary>배율 1.0 프리팹 실측 어깨/엉덩이 로컬 Y. 값의 출처는
        /// <see cref="Core.StickmanMetrics"/>의 폴백 상수와 <b>같은 실측표</b>다
        /// (전신 2.2746944 / 머리중심 2.0546944 / 머리반경 0.22 / 어깨 1.7646944 / 엉덩이 0.9346944).
        /// <para>여기 둔 이유는 <b>몸이 없는 소비자</b>(카드 썸네일 — Interaction/AccessoryCardIcon.cs)가
        /// 리그를 만들 때 쓸 값이 필요해서다. 그 소비자가 자기 파일에 숫자를 새로 적으면 비례가 바뀔 때
        /// 카드만 옛 몸으로 남는다(규칙 4-a).</para></summary>
        internal const float BaselineShoulderLocalY = 1.7646944f;

        internal const float BaselineHipLocalY = 0.9346944f;
        internal const float BaselineEyeOffsetX = 0.075f;
        internal const float BaselineEyeOffsetY = 0.02f;
        internal const float EyeOffsetXInHeadRadii = BaselineEyeOffsetX / BaselineHeadVisualRadius; // 0.3409
        internal const float EyeOffsetYInHeadRadii = BaselineEyeOffsetY / BaselineHeadVisualRadius; // 0.0909

        // ============================================================================
        // ★ 획 예산(Stroke Budget) — 2026-09-01 (docs/UX_FLOW.md 37-1 / 37-6 규칙 1)
        // ============================================================================
        // 이 앱의 그림은 전부 LineRenderer 선화이고 액세서리 획에는 <b>화면상 2pt 하한</b>이 걸린다.
        // 그런데 28종의 도형 좌표는 <b>선 굵기가 0인 것처럼</b> 설계돼 있었다 — 배율 0.75(출하 기본)에서
        // 획 하나가 <b>0.344R</b>이라, 그보다 작은 요소는 화면에 존재하지 않는다.
        // ux-designer 실측: 측정 26개 중 '가독' 판정 1개, 20종 중 19종이 최단 선분 &lt; 획 1개.
        //
        // 그 실측이 쓴 숫자를 여기 <b>식으로</b> 옮긴다. 값을 적어 두면 신장/획 비율이 바뀔 때
        // 예산만 옛 숫자로 남는다(이 프로젝트가 반복해서 겪은 이중 정의 계열 실패).
        // 소비자: Tests/EditMode/AccessoryStrokeBudgetTests(도형 검사) — 런타임은 이 값을 읽지 않는다.

        /// <summary>액세서리 획의 <b>비례 두께</b>(배율 1.0 실측, 월드 유닛). 렌더러(몸)·초상화가 이
        /// 값을 신장으로 나눠 쓴다 — 세 곳에 0.048을 각자 적어 두면 한 곳만 고쳐지는 사고가 난다.</summary>
        internal const float BaselineStrokeWidth = 0.048f;

        /// <summary>신장 배수로 표현한 액세서리 획 두께(= <see cref="BaselineStrokeWidth"/> / 기준 신장).</summary>
        internal const float StrokeWidthRatio = BaselineStrokeWidth / StickConfig.BaselineCharacterTotalHeight;

        /// <summary>검산 기준 배율. 37-6 규칙 1이 <b>출하 기본값 0.75로 고정</b>한다 —
        /// 다이얼 최소(0.35)는 획이 0.74R이라 어떤 규칙도 만족시킬 수 없는 <b>실루엣 전용 구간</b>이다.</summary>
        internal const float ShippingCharacterScale = 0.75f;

        /// <summary>
        /// 그 배율에서 <b>실제로 그려지는</b> 획 두께를 머리 반경 R 배수로 환산한 값.
        /// 배율 0.75에서 0.3439R(= 2.00pt). 하한(<see cref="StickConfig.MinStrokeScreenPoints"/>)까지
        /// 포함하므로 렌더러의 <c>RenderStrokeWidth</c>와 같은 식이다.
        /// </summary>
        internal static float StrokeBudgetInHeadRadii(float characterScale)
        {
            float scale = Mathf.Max(0.0001f, characterScale);
            float stroke = Mathf.Max(BaselineStrokeWidth * scale,
                StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox);
            return stroke / (BaselineHeadVisualRadius * scale);
        }

        /// <summary>출하 기본 배율에서의 획 예산(R 배수). 규칙 1의 검산은 전부 이 값으로 한다.</summary>
        internal static float ShippingStrokeBudgetInHeadRadii => StrokeBudgetInHeadRadii(ShippingCharacterScale);

        // ---- 털모자(33-2-1 #2)
        internal const float BeanieBandBottomRatio = 0.42f;   // = 이 모자의 HatCoverLocalY(가장 깊이 눌러쓴다)
        internal const float BeanieBandTopRatio = 0.62f;
        internal const float BeanieBandHalfWidthRatio = 0.92f;
        internal const float BeanieCrownHeightRatio = 0.78f;
        internal const float BeanieCrownHalfWidthRatio = 0.86f;
        // ★ 폼폼 — 2026-09-01 규칙 1(획 예산) 위반 수정.
        //
        // 옛 폼폼은 <b>반지름 0.22R, 8각형</b>이었고 두 가지가 동시에 성립했다.
        //   ① 잉크 사각형 0.44R = <b>1.28획</b> < 1.5획. 규칙 1의 "뚱뚱한 점" 구간이다(확정 위반).
        //   ② 한 변 0.49획인데 <b>꺾임이 정확히 45.0도</b>다. 획 예산 검사의 문턱(CornerDegrees)이
        //      바로 45도라, float32에서 여덟 꼭짓점이 44.999996 / 45.000006으로 <b>번갈아</b> 갈린다.
        //      그러면 "양끝이 <b>모두</b> 꺾임"인 선분이 하나도 없어 검사가 이 변을 통째로 놓친다 —
        //      즉 옛 8각형은 규칙을 어기면서 <b>린트에는 잡히지 않는</b> 자리였다. 방울이 같은 이유로
        //      10각형을 유지했다는 기록(BellSegments 문단)이 이 파일에 이미 있다.
        //
        // 고침은 둘이다. <b>채움은 옛날부터 있었으므로 방울과 달리 "채우기"는 해법이 아니다</b> —
        // 남은 지렛대는 각수와 크기뿐이었다.
        //   ⓐ <b>10각형</b>(꺾임 36도). 문턱에서 9도 떨어져 float 잡음이 판정을 못 뒤집는다.
        //      첫 점을 90도에서 시작해 <b>가장 높은 꼭짓점이 정확히 꼭대기</b>에 오게 한다
        //      (위상 0도면 꼭대기가 72도에 놓여 아래 액자 계산이 어긋난다 — 옛 방울의 결함과 같다).
        //   ⓑ 반지름 0.22R -> <b>0.28R</b>(지름 1.63획, 1.5획 문턱에 8.6% 여유). 방울과 같은 값이다.
        //
        // 왜 <b>꼭대기를 고정</b>하는가: 폼폼 꼭대기는 초상화 액자의 상한
        // (CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR = 1.80R)에 <b>정확히 닿아</b> 있다
        // (0.62 + 0.78 + 0.18 + 0.22 = 1.80). 반지름만 키우면 그대로 잘린다. 그래서 고정 대상은
        // 반지름도 오프셋도 아닌 <b>꼭대기 높이</b>이고, 오프셋은 거기서 유도한다(규칙 4-a).
        // 덕분에 모자 6종 15쌍의 실루엣 차이가 <b>소수점 여섯 자리까지 그대로다</b>(최소 2.95획 유지).
        //
        // 덤으로 규칙 4의 잠복 결함 하나가 함께 닫혔다: 옛 폼폼은 관 표면과 <b>0.01획</b>만 겹쳐
        // 사실상 접해 있었다(어느 쪽으로든 조금만 움직이면 "0 &lt; 간격 &lt; 1획" 최악 구간에 빠진다).
        // 지금은 0.36획 겹친다.

        /// <summary>폼폼 <b>꼭대기</b>가 관 꼭짓점 위로 올라가는 높이. 액자에 닿는 것은 중심이 아니라
        /// 꼭대기이므로, 반지름을 바꿔도 이 값이 고정되면 실루엣 상한이 따라 움직이지 않는다.</summary>
        internal const float BeaniePomCrestRiseRatio = 0.40f;

        internal const float BeaniePomRadiusRatio = 0.28f;

        /// <summary>관 꼭짓점에서 폼폼 <b>중심</b>까지. 매직넘버로 적어 두면 반지름을 고칠 때
        /// 꼭대기가 조용히 액자를 넘는다(규칙 4-a).</summary>
        internal const float BeaniePomOffsetRatio = BeaniePomCrestRiseRatio - BeaniePomRadiusRatio;

        internal const float BeaniePomBackShiftRatio = 0.10f;

        /// <summary>폼폼 원을 근사하는 변의 수. <b>10</b>인 이유는 방울(<see cref="BellSegments"/>)과
        /// 같다 — 8각형은 꺾임이 정확히 45도라 획 예산 검사의 문턱 위에 얹히고, 그 조건을 만족시키려면
        /// 지름이 2.6획까지 커져야 한다(방울 라운드 실측).</summary>
        internal const int BeaniePomSegments = 10;

        /// <summary>첫 꼭짓점의 위상(도). 90도 = 가장 높은 꼭짓점이 정확히 꼭대기에 온다.</summary>
        internal const float BeaniePomStartDegrees = 90f;

        // ---- 중절모(33-2-1 #3)
        internal const float FedoraBrimLineRatio = 0.58f;     // = 이 모자의 HatCoverLocalY
        internal const float FedoraBrimFrontRatio = 1.75f;
        internal const float FedoraBrimBackRatio = 1.25f;
        internal const float FedoraBrimTipRiseRatio = 0.10f;
        internal const float FedoraCrownHeightRatio = 0.72f;
        internal const float FedoraCrownHalfWidthRatio = 0.72f;
        internal const float FedoraCreaseDropRatio = 0.24f;

        /// <summary>크리스(관 꼭대기의 눌린 자국) 반폭 — 관 반폭의 배수. ★ 2026-09-01 규칙 1 위반 수정.
        /// 옛 값 0.30은 잉크 사각형이 <b>1.26획</b>이라 문턱(1.5획) 아래였다: 화면에서 V가 아니라
        /// <b>뚱뚱한 점</b> 하나로 뭉갠다.
        /// <para>0.40이면 1.68획으로 12% 여유가 생긴다. 최소 수정안(0.36)은 1.51획으로 문턱에 0.5%밖에
        /// 안 남아, 좌표를 한 자리만 건드려도 다시 넘어가는 자리라 쓰지 않았다.
        /// 두 끝점은 여전히 관 위 변 안쪽(±0.66 관반폭)이라 <b>실루엣이 한 구간도 안 움직인다</b>.</para></summary>
        internal const float FedoraCreaseHalfWidthRatio = 0.40f;

        // ★ 2026-09-01 — FedoraBandRiseRatio(0.14f)를 여기서 지웠다. 띠는 이제 좌표를 스스로 적지 않고
        //   관(crown) 밑변의 두 끝점을 <b>그대로 받아 쓴다</b>(AppendHead의 HeadFedora 문단 참고).

        // ---- 왕관(33-2-1 #4). HatCoverLocalY = +∞ — 씌우는 것이 아니라 얹는 것이라 밑이 뚫려 있다.
        internal const float CrownBaseRatio = 0.55f;
        internal const float CrownHalfWidthRatio = 0.85f;

        // ---- 동그란 안경 — 2026-09-01(2차). 옛 그림은 <b>아령(덤벨)</b>로 읽혔다(Tasklist V2).
        //      원인은 두 가지가 겹친 것이다: (a) 코받침이 <b>렌즈 한가운데 높이</b>를 가로질러
        //      "봉으로 이은 두 추"가 됐고, (b) 렌즈(지름 0.60R)가 간격(0.52R)에 비해 작았다.
        //      고침도 둘이다: 코다리를 <b>렌즈 꼭대기</b>로 올려 아치로 만들고, 렌즈를 키운다.
        //      간격은 규칙 1의 1.5획(0.516R)을 그대로 지킨다 — 1.28R − 0.76R = 0.52R = 1.51획.
        internal const float RoundLensOffsetRatio = 0.64f;
        internal const float RoundLensRadiusRatio = 0.38f;

        /// <summary>코다리 아치의 꼭대기 높이. 두 끝은 렌즈 12각형의 <b>60도 꼭짓점 그 자체</b>라
        /// 좌표를 새로 적지 않는다(규칙 4-a — 렌즈 크기를 고치면 아치가 따라온다).</summary>
        internal const float RoundBridgeRiseRatio = 0.42f;

        /// <summary>둥근 렌즈/외알 알을 근사하는 변의 수. <b>12</b>여야 60도·120도·270도 꼭짓점이
        /// 인덱스 2·4·9로 정확히 집힌다 — 코다리와 체인이 그 꼭짓점을 <b>그대로 받아 쓰므로</b>
        /// 이 값을 바꾸면 부착점이 조용히 어긋난다(규칙 4-a).</summary>
        internal const int RoundLensSegments = 12;

        // ---- 고글 — 2026-09-01(2차). 옛 그림은 <b>왼쪽에만 곡선이 붙은 기형</b>이었다(Tasklist V3).
        //      옛 스트랩은 "뒤통수를 도는 반원" 하나였고, 그건 <b>머리가 있어야</b> 성립하는 그림이다.
        //      카드에는 머리가 없으니 한쪽으로 쏠린 고리만 남는다.
        //      새 스트랩은 렌즈 테를 감싸고 <b>좌우로 똑같이</b> 뻗는 띠 하나(보조색 채움)다 —
        //      머리가 있든 없든 같은 물건으로 읽히고, 얼굴 위에서는 관자놀이를 지나는 끈이 된다.
        internal const float GoggleHalfWidthRatio = 0.86f;
        internal const float GoggleHalfHeightRatio = 0.44f;
        internal const float GoggleCornerXRatio = 0.72f;          // 모서리를 깎는 지점(반폭 배수)
        internal const float GoggleCornerYRatio = 0.4545f;        // 〃 (반높이 배수) = 0.20R

        /// <summary>스트랩이 좌우로 뻗는 끝. 머리 원(1.0R) <b>밖</b>이다 — 끈은 관자놀이를 지나
        /// 뒤로 돌아가는 물건이라 얼굴 옆으로 나오는 것이 옳다. 상한은 레이어 회귀가 잠근
        /// "안경은 머리 반경의 1.6배를 넘지 않는다"이고, 여기에 5% 여유를 뒀다.</summary>
        internal const float GoggleStrapReachRatio = 1.52f;

        internal const float GoggleStrapHalfHeightRatio = 0.26f;
        internal const float GoggleRimThicknessRatio = 0.34f;     // 테가 렌즈 위로 솟는 두께

        /// <summary>외알 안경 팟의 중심 x. <b>매직넘버 0.40을 버리고</b> 눈 중립 좌표에서 유도한다 —
        /// 37-6 규칙 4-a(실측 상수가 있으면 매직넘버 금지). 옛 0.40f는 구멍 반경의 55%만큼 눈에서
        /// 어긋나 있었고, 그 사실이 37-8 (2)에 실제 사례로 적혀 있다.</summary>
        internal const float MonocleOffsetRatio = EyeOffsetXInHeadRadii;

        /// <summary>2026-09-01(2차) 0.40 -> 0.44. 카드에서 <b>체인과 한 물건으로</b> 묶이려면
        /// 알이 체인보다 확실히 커야 한다(Tasklist V4).</summary>
        internal const float MonocleRadiusRatio = 0.44f;

        // ---- 줄무늬 타이 / 목도리 / 방울 목걸이(33-2-3). 부착 기준선은 나비넥타이와 같은 BowTieLocalY.
        internal const float TieKnotHalfWidthRatio = 0.24f;
        internal const float TieKnotHalfHeightRatio = 0.20f;
        internal const float TieBladeLengthInTorso = 0.55f;
        internal const float TieBladeHalfWidthRatio = 0.15f;
        /// <summary>33-2-5 (D) — 줄무늬 타이 "월요일마다 조금 느슨해진다". 매듭을 R·0.12 내리고 blade를 3도 기울인다.</summary>
        internal const float TieMondayLoosenDropRatio = 0.12f;
        internal const float TieMondayLoosenTiltDegrees = 3f;
        // ★ 목도리 — 2026-09-01(2차) 재설계. 리더 육안 검증: 카드가 <b>장화/파이프</b>로 읽혔다(V8).
        //   원인은 셋이 겹친 것이다.
        //     (a) 띠가 <b>납작한 가로 막대</b>(반높이 0.17R)라 장화의 목처럼 보였고,
        //     (b) 두 자락이 <b>둘 다 뒤쪽</b>(x −0.30R / −0.62R)에 폭 0.22R로 붙어 있어 획에 먹혀
        //         <b>한 덩어리 기둥</b>으로 합쳐졌으며(각 0.64획 — 규칙 1 면제 대장에 그대로 적혀 있었다),
        //     (c) 그 결과 "가로 막대 + 세로 기둥" = 장화의 L자 실루엣이 됐다.
        //   고침: 띠를 <b>목에 감긴 고리</b>(가운데가 처지는 U)로 만들고, 자락을 <b>앞뒤 하나씩</b>
        //   서로 다른 길이로 늘어뜨린다. 폭도 획을 넘겨(1.2획 이상) 두 자락이 각자 보인다.
        internal const float ScarfWrapHalfWidthRatio = 0.88f;
        internal const float ScarfWrapTopRatio = 0.28f;
        internal const float ScarfWrapCenterTopRatio = 0.06f;   // 윗변이 목덜미로 파이는 깊이
        internal const float ScarfWrapSideRatio = -0.14f;
        internal const float ScarfWrapDipRatio = -0.58f;        // 고리가 가슴으로 처지는 최저점
        internal const float ScarfFrontTailLengthInTorso = 0.40f;
        internal const float ScarfBackTailLengthInTorso = 0.58f;
        internal const float CollarHalfWidthRatio = 0.75f;

        /// <summary>목줄 양 끝의 높이(목선 기준). <see cref="CollarCurve"/>가 이 값을 쓴다.</summary>
        internal const float CollarRiseRatio = 0.16f;

        /// <summary>목줄 한가운데가 아래로 처지는 깊이. 매달리는 것(방울/펜던트)은 이 최저점에서
        /// 시작해야 <b>매달린 지점이 보인다</b>(규칙 4) — 그래서 상수로 뽑아 유도한다(규칙 4-a).</summary>
        internal const float CollarDipRatio = 0.30f;

        // ★ 방울 목걸이 — 2026-09-01 규칙 1(획 예산) 위반 수정.
        //
        // 옛 방울은 반지름 0.17R(지름 0.34R = <b>0.99획</b>)이었다. 즉 <b>공 전체가 획 하나 굵기</b>라
        // 2pt 획으로 그 원을 그리면 안쪽 구멍이 통째로 메워져 "뚱뚱한 점"이 된다(37-6 규칙 1의
        // "잉크 사각형 ≥ 1.5획" 위반. 10각형 한 변도 0.31획이었다).
        // 게다가 <b>매달린 지점이 어긋나 있었다</b>: 옛 드롭(0.34R)으로 잡은 공의 위 끝이 목줄 최저점보다
        // 0.038R = 0.11획 아래라, 규칙 4가 "최악"이라고 못박은 0 &lt; 간격 &lt; 1획 구간이었다.
        //
        // 고침은 셋이다.
        //   ① <b>지름을 1.63획</b>으로 키운다(반지름 0.28R). 1.5획 문턱에 8.7% 여유.
        //   ② <b>채운다</b>. 방울은 속이 보여야 하는 물건이 아니라 <b>금속 덩어리</b>다 —
        //      윤곽선으로 남기면 규칙 1이 요구하는 "내부를 보여주는 크기"가 3.0획(1.03R)이라
        //      머리 반지름만 한 방울이 되어야 하고, 그러면 펜던트와 다시 붙는다.
        //   ③ 매다는 위치를 <see cref="CollarLowLocalY"/>에서 <b>유도</b>한다(규칙 4-a). 드롭 상수를
        //      따로 적어 두면 목줄 곡률을 고칠 때 방울만 공중에 남는다 — 펜던트가 이미 같은 유도를 쓴다.
        //
        // 왜 더 키우지 않았나: 방울이 커질수록 <b>펜던트와 다시 가까워진다</b>(둘은 같은 목줄에 매달린
        // 형제고, 갈리는 축은 "얼마나 내려오는가"다). 실측 — 지름 1.63획일 때 펜던트와 1.98획 차이다.
        internal const float BellRadiusRatio = 0.28f;

        /// <summary>방울 원을 근사하는 변의 수. <b>10</b>인 이유는 두 가지다:
        /// (1) 지름 1.63획에서 이 정도면 눈에 원으로 읽힌다. (2) 한 꼭짓점의 꺾임이 36도라
        /// 획 예산 검사가 "매끄러운 곡선"으로 인정한다 — 8각형은 꺾임이 정확히 45도(문턱)라
        /// 각 변이 <b>독립된 획</b>으로 요구되고, 그 조건을 만족하는 8각형은 지름이 2.6획이라
        /// 펜던트와 다시 붙는다(실측). 원을 각지게 만들수록 커져야 한다.</summary>
        internal const int BellSegments = 10;

        // ---- 긴 망토 / 날개 / 배낭(33-2-4)
        // 긴 망토도 같은 이유로 넓힌다. 길이는 2.10 -> 1.85로 줄였다 — 배율 0.75에서 옛 값의 밑단은
        // 로컬 y 0.03(발바닥에서 획 두께 남짓)이라 사실상 바닥을 쓸고 있었다.
        internal const float LongCapeLengthRatio = 1.85f;
        internal const float LongCapeSpreadRatio = 3.10f;
        internal const float LongCapeFrontSpreadRatio = 1.05f;
        internal const float LongCapeHemWaveRatio = 0.30f;

        // ★ 2026-09-01(2차) 제비꼬리(swallowtail) 밑단 — 리더 육안 검증 V9
        //   "짧은망토 vs 긴망토 카드 그림이 거의 동일하다".
        //
        // 원인은 <b>지표가 원리적으로 못 보는 자리</b>에 있었다. 실루엣 지표는 <b>월드 좌표</b>의
        // 반경 프로파일을 재는데, 카드는 도형을 <b>상자에 꽉 차게 다시 스케일</b>한다
        // (AccessoryCardIcon.TryBuild의 scale = size·FitFraction / span). 두 망토는 세로:가로가
        // 1.54 : 1.72로 거의 같아, 정규화 뒤에는 <b>같은 그림</b>이 된다. 즉 "길이가 다르다"는
        // 차이는 카드에서 원리적으로 사라진다 — 길이로는 절대 갈릴 수 없는 자리다.
        //
        // 그래서 <b>정규화에 살아남는 특징</b>을 준다: 긴 망토의 밑단을 갈라 제비꼬리로 만든다.
        // 형태 특징은 크기를 바꿔도 남는다. 실측(정규화 실루엣 차) 0.123 -> 0.240.
        internal const float LongCapeHemNotchRatio = 0.42f;      // 갈라진 골의 깊이(밑단~옷깃 높이 배수)
        internal const float LongCapeHemNotchApexRatio = 0.38f;  // 골의 x(밑단 뒤끝 배수)
        // ★ 2026-09-01(2차) — <b>한 쌍으로</b> 다시 만들었다. 리더 육안 검증: 날개 카드가
        //   "나뭇잎 한 장 / 깃발"로 읽혔고 <b>한 짝만 보였다</b>(Tasklist V7).
        //   확인해 보니 옛 두 깃은 <b>둘 다 진행 반대쪽</b>(x 음수)으로만 뻗어 겹쳐 있었다 —
        //   즉 설계상으로도 날개 <b>한 짝</b>을 두 겹으로 그린 것이었고, 대칭성 지표로 재면 1.000
        //   (완전한 한쪽 쏠림)이 나온다. 날개라는 이름이 요구하는 최소 조건은 <b>쌍</b>이다.
        //   그래서 같은 깃을 좌우로 하나씩 두고, 뿌리를 등 한가운데(x=0)에서 만나게 한다.
        //   상한은 정수리다 — Tests/PlayMode/CharacterAppearanceLayerTests가 잠근다.
        internal const float WingSpineDropInTorso = 0.52f;
        internal const float WingRootRiseRatio = 0.12f;        // 두 깃이 만나는 등 한가운데(x=0)
        internal const float WingOuterReachRatio = 2.30f;
        internal const float WingOuterRiseRatio = 0.96f;
        internal const float WingMidReachRatio = 1.95f;        // 둘째 깃 끝
        internal const float WingInnerReachRatio = 1.30f;      // 셋째 깃 끝
        internal const float PackCenterBackRatio = 0.62f;
        internal const float PackDropInTorso = 0.42f;
        internal const float PackHalfWidthRatio = 0.55f;
        internal const float PackHalfHeightInTorso = 0.34f;
        internal const float PackFlapDropInTorso = 0.12f;

        // ============================================================================
        // ★ 2026-09-01 카테고리당 +2종 — <b>임시 플레이스홀더</b>의 치수표
        // ============================================================================
        // 리더 지시: "완전히 새로 디자인하지 말고 기존 셰이프를 팔레트/비율만 바꿔 변형". 그래서 아래
        // 값들은 전부 <b>바로 위 형제 상수의 변주</b>이고, 새 조형 규칙을 만들지 않는다.
        // 다만 <b>획 예산</b>(37-6 규칙 1, ShippingStrokeBudgetInHeadRadii ≈ 0.344R)만은 지킨다 —
        // 양끝이 모두 꺾임인 선분이 획보다 짧으면 그 선분은 화면에서 통째로 먹힌다. 아래 비율은
        // 그 조건을 손으로 검산해 잡았다(가장 빠듯한 자리마다 주석으로 값을 남겼다).

        // ---- 베레모(모자 4번) — 털모자 관(crown)의 <b>비대칭 변주</b>. 뒤로 처지고 꼭지가 없다.
        internal const float BeretBrimLineRatio = 0.46f;    // = 이 모자의 HatCoverLocalY
        internal const float BeretCrownHeightRatio = 0.66f;
        internal const float BeretBackDroopRatio = 1.30f;   // 뒤로 늘어진 끝
        internal const float BeretFrontRatio = 0.90f;

        /// <summary>뒤로 늘어진 끝이 밑변보다 더 내려가는 깊이. 밑변(= 보조색 테)의 기울기를 만든다.</summary>
        internal const float BeretBackDroopDropRatio = 0.10f;

        // ---- 밀짚모자(모자 5번) — 중절모의 <b>납작·광폭 변주</b>(챙이 넓고 관이 낮다).
        internal const float StrawBrimLineRatio = 0.56f;    // = 이 모자의 HatCoverLocalY
        internal const float StrawBrimFrontRatio = 2.15f;
        internal const float StrawBrimBackRatio = 1.95f;
        internal const float StrawBrimDropRatio = 0.16f;
        internal const float StrawCrownHeightRatio = 0.54f;
        internal const float StrawCrownHalfWidthRatio = 0.78f;

        // ★ 2026-09-01 — StrawBandRiseRatio(0.16f)도 같은 이유로 지웠다(중절모와 같은 결함·같은 해법).

        // ---- 뿔테 안경(안경 4번) — 2026-09-01(2차). 옛 그림(위아래로 겹친 판 2장)은 카드에서
        //      <b>뚜껑 달린 상자</b>로 읽혔다. 뿔테의 정체는 "굵은 눈썹테 <b>아래에 렌즈가 매달린다</b>"이므로
        //      판을 위아래가 아니라 <b>테 1장 + 렌즈 2장</b>으로 나눈다 — 렌즈가 둘로 갈리는 순간
        //      맥락 없이도 안경으로 읽힌다(형제들과 같은 신호). 테는 여전히 보조색이다(규칙 3-2).
        internal const float BrowlineBarOuterRatio = 1.00f;
        internal const float BrowlineBarInnerRatio = 0.90f;
        internal const float BrowlineBarBottomRatio = 0.12f;       // 테 밑변 = 렌즈 윗변(간격 0, 규칙 4)
        internal const float BrowlineBarTopRatio = 0.54f;
        internal const float BrowlineLensInnerRatio = 0.28f;       // 두 렌즈 사이 0.56R = 1.63획
        internal const float BrowlineLensOuterRatio = 0.94f;

        // ---- 안대(안경 5번) — 외알안경과 같은 "앞쪽 눈에만" 규약. 채운 천 + 뒤로 넘어가는 끈.
        //      중심 x는 외알안경과 <b>같은 유도</b>다(규칙 4-a) — 두 아이템이 같은 눈을 가린다.
        //      ★ 2026-09-01(2차): 옛 끈은 머리 링을 도는 <b>독립된 호</b>라 천에서 0.50획 떠 있었다
        //      (연결성 지표가 잡은 미보고 결함). 이제 끈이 천의 뒤쪽 두 꼭짓점에서 <b>출발</b>한다.
        internal const float PatchOffsetRatio = EyeOffsetXInHeadRadii;
        internal const float PatchHalfWidthRatio = 0.40f;
        internal const float PatchHalfHeightRatio = 0.36f;
        internal const float PatchStrapReachRatio = 0.82f;
        internal const float PatchStrapRiseRatio = 0.56f;

        // ★ 펜던트 목걸이(목 4번) — 2026-09-01 실루엣 재설계.
        //
        // 옛 마름모는 반폭 0.20R × 반높이 0.30R(폭 1.16획 × 높이 1.74획)이라 <b>2pt 획이 네 꼭짓점을
        // 통째로 둥글려</b> 화면에서는 "작은 동그라미"였다. 방울(지름 0.99획)과의 외곽 차이가 0.54획뿐,
        // 즉 규칙 1의 1.0획도 못 넘어 <b>형제와 갈리지 않았다</b>(페르소나 실물 확인).
        // 보관함 설명은 "가는 줄에 <b>마름모</b> 장식 하나"라고 말한다 — 그림이 이름을 못 지킨 것이다.
        //
        // 그래서 <b>세로로 길게</b> 뺐다(종횡 1.50 -> 2.21). 원과 갈리는 것은 크기가 아니라 종횡비다:
        // 원은 어떤 각도에서도 반경이 같고, 이 마름모는 세로가 가로의 2.2배다. 빗변도 1.05획 -> 1.98획이
        // 되어 꼭짓점이 획에 먹히지 않는다. 실측: 방울과 외곽 차이 <b>2.50획</b>(옛 0.54획).
        internal const float PendantHalfWidthRatio = 0.28f;

        /// <summary>마름모 반높이. 위 꼭짓점은 <b>목줄 최저점</b>에 붙고 아래 꼭짓점은 가슴께로
        /// 내려온다 — 방울(목선 아래 0.51R)과 <b>매달린 길이</b>로도 갈린다(목선 아래 1.38R).</summary>
        internal const float PendantHalfHeightRatio = 0.62f;

        // ---- 반다나(넥타이 5번) — 목도리 띠의 <b>납작 변주</b> + 앞으로 늘어진 삼각 자락 하나.
        internal const float BandanaWrapRiseRatio = 0.06f;
        internal const float BandanaWrapHalfWidthRatio = 0.80f;
        internal const float BandanaWrapHalfHeightRatio = 0.20f;   // 위아래 변 간격 0.40R(= 1.16획)
        internal const float BandanaTailLengthRatio = 0.62f;
        internal const float BandanaTailHalfWidthRatio = 0.18f;

        // ---- 판초(망토 4번) — 짧은 망토와 <b>같은 도형·다른 비율</b>. 짧고 앞까지 덮는다.
        internal const float PonchoLengthRatio = 1.05f;
        internal const float PonchoSpreadRatio = 1.95f;
        internal const float PonchoFrontSpreadRatio = 1.55f;
        internal const float PonchoHemWaveRatio = 0.12f;

        // ---- 요정 날개(망토 5번) — 날개와 같은 구성의 <b>작고 둥근 변주</b>. 흔들리지 않는다(천이 아니다).
        //      ★ 2026-09-01(2차) 날개와 <b>같은 이유로</b> 한 쌍이 됐다. 형제가 쌍인데 이쪽만 한 짝이면
        //      "요정 날개"라는 이름이 다시 그림과 어긋난다(원칙 1의 그림 버전).
        internal const float FairyWingOuterReachRatio = 1.62f;
        internal const float FairyWingOuterRiseRatio = 0.88f;
        internal const float FairyWingInnerReachRatio = 1.02f;
        internal const float FairyWingSpineDropScale = 0.40f;   // 등뼈 길이(WingSpineDropInTorso 배수 아님 — 몸통 배수)

        // ============================================================================
        // ★ HAIR — 2026-09-01 전면 재설계 (docs/UX_FLOW.md 37-3 (A) / 로드맵 P0)
        // ============================================================================
        // 옛 4종은 <b>합계 선 5개·채움 0개·3종 단색</b>이었고, 다음 세 가지가 동시에 성립했다:
        //   · 곱슬 웨이브 진폭 0.16R = 0.93pt < 획 반폭 1.00pt  ->  <b>곱슬 ≡ 단정</b>(기하학적 동일)
        //   · 단정 1.13R / 곱슬 1.10R = 두피 링(1.0R) <b>바깥</b> 0.13R·0.10R에 떠 있음
        //     (= 획의 0.38·0.29배. 붙은 것도 뗀 것도 아니라 "선을 두 번 그린 실수"로 읽힌다)
        //   · 보조색 4종 전부 EYES 틴트(청록) -> 갈색 머리에 형광 청록 가르마
        //
        // 재설계의 규칙은 취향이 아니라 <see cref="StrokeBudgetInHeadRadii"/>다. 세 가지를 지켰다:
        //   (1) <b>채움</b>. 머리카락은 두피를 가려야 하므로 실루엣이 닫힌 채움 도형이다(37-6 규칙 2).
        //   (2) <b>부착</b>. 실루엣의 안쪽 경계는 <b>이마선</b>(HairlineCrestRatio)이라 두피 링 <b>안쪽</b>을
        //       지난다 — 링을 1W 이상 파고들어 "머리에서 자란 것"으로 읽힌다(규칙 4).
        //   (3) <b>식별 특징 1개</b>. 삐침(뿔)/가르마/컬/테두리 각 1개에만 보조색을 쓴다(규칙 5).
        //
        // ★ 이마선을 <b>동심 원호가 아니라 포물선 현(chord)</b>으로 잡은 이유: 링과 같은 중심의 원호로
        //   안쪽을 막으면 머리카락이 아니라 <b>헬멧 테</b>로 보인다(오프라인 시안 1차에서 실제로 그랬다).
        //   이마선은 얼굴을 가로지르는 선이어야 한다.
        //
        // ★ 눈동자(States/EyeController, sortingOrder 5)와 머리카락 <b>채움</b>(SortHair−1 = 5)은
        //   레이어가 동률이다. 그래서 이마선 마루(0.50R)를 눈동자 위끝(0.0909R + 반지름 0.136R = 0.227R)
        //   보다 확실히 위에 두어 <b>겹칠 일 자체를 없앤다</b>. 이 성질은 EditMode가 직접 단언한다
        //   (동률 레이어는 그리기 순서가 미정이라는 이 프로젝트의 33-2-0 함정과 같은 자리다).
        internal const float HairSpanStartDegrees = -16f;   // 진행 방향(+x)이 0도
        internal const float HairSpanEndDegrees = 196f;
        internal const int HairCapSegments = 14;
        internal const int HairCurlSegments = 22;

        /// <summary>이마선의 반폭(R 배수). 링(1.0R) 안쪽이라 머리카락이 두피에 <b>박힌다</b>.</summary>
        internal const float HairlineHalfWidthRatio = 0.88f;

        /// <summary>이마선 양 끝의 높이(머리 중심 기준). 귀 앞뒤로 살짝 내려온다.</summary>
        internal const float HairlineEdgeRatio = -0.06f;

        /// <summary>이마선 한가운데의 높이. 눈동자 위끝(0.227R)보다 위여야 한다(위 문단).</summary>
        internal const float HairlineCrestRatio = 0.50f;

        internal const int HairlineSegments = 6;

        // 삐친머리 — 실루엣 + 뒤로 솟은 삐침 하나.
        internal const float CowlickCapRadiusRatio = 1.22f;
        internal const float CowlickFrontLiftRatio = 0.06f;
        internal const float CowlickTuftTipRadiusRatio = 1.70f;
        internal const float CowlickTuftTipDegrees = 120f;

        // 단정한머리 — 앞으로 빗어 넘긴 실루엣 + 가르마 가닥 하나.
        internal const float NeatCapRadiusRatio = 1.14f;
        internal const float NeatFrontLiftRatio = 0.16f;
        internal const float NeatPartHalfWidthRatio = 0.19f;   // 가닥 반폭 -> 폭 0.38R = 1.11W

        // 곱슬 — 물결치는 실루엣 + 앞으로 늘어진 컬 하나.
        //  ★ 진폭 0.28R(= 0.81W, 마루-골 1.63W)이라 웨이브가 <b>획에 먹히지 않는다</b>.
        //    옛 값 0.16R은 획 반폭(0.17R)보다 작아 물결이 획 안에 통째로 매몰됐다.
        internal const float CurlBaseRadiusRatio = 1.28f;
        internal const float CurlAmplitudeRatio = 0.28f;
        internal const float CurlWaveCount = 2f;

        // 민머리 — 실루엣이 <b>없는</b> 것이 특징이다. 관자놀이~뒤통수에 남은 테두리 2조각만 그린다.
        // 안쪽 0.58R = 두피 링을 0.42R(=1.22획) 파고든다. 0.62R로도 규칙 4를 통과하지만 여유가
        // 획의 10%뿐이라, 훗날 획 하한이 조금만 올라가도 조용히 '떠 있는 아이템'이 된다.
        internal const float BaldRimInnerRadiusRatio = 0.58f;
        internal const float BaldRimOuterRadiusRatio = 1.18f;
        internal const float BaldRimBackFromDegrees = 122f;
        internal const float BaldRimBackToDegrees = 206f;
        internal const float BaldRimFrontFromDegrees = -26f;
        internal const float BaldRimFrontToDegrees = 24f;

        // ★ 바가지머리(머리 4번) — 2026-09-01 실루엣 재설계.
        //
        // 옛 도형은 형제들과 <b>같은 돔</b>(HairSilhouette)을 반경만 키운 것이었고, 정체는 이마를
        // 가로지르는 내부 선 하나였다. 배율 0.75에서 단정한머리와의 반경 차가 0.20R = <b>0.58획</b>이라
        // 규칙 1의 1.0획도 못 넘었고, 페르소나가 실물 스크린샷에서 "두 장이 같은 그림"이라고 확인했다.
        // (지표도 못 봤다: 옛 검사는 <b>정점만</b> 상반구에서 훑어 빈 각도를 0으로 세는 바람에 같은 쌍을
        //  3.77획으로 부풀렸다. 지금은 변을 조밀 표본하고 360도를 다 본다.)
        //
        // 그래서 정체를 <b>실루엣</b>으로 옮겼다 — 형제들의 "돔 + 포물선 이마선" 대신 <b>수평으로 자른
        // 밑선 셋</b>으로 닫는다: 귀를 덮는 옆머리 두 짝(밑변 <see cref="BowlCutLineRatio"/>)과 이마를
        // 가로지르는 앞머리 선(<see cref="BowlFringeLineRatio"/>). 이름("가지런히 자른")이 곧 도형이다.
        // 실측: 단정한머리와 3.90획, 형제 5종 중 최소 3.90획(옛 0.58획).
        internal const float BowlCapRadiusRatio = 1.52f;

        /// <summary>옆머리를 자른 높이(머리 중심 기준, 음수 = 아래). 귀를 덮는 자리다 —
        /// 형제들의 실루엣은 이 각도대에 잉크가 아예 없어서 반경 프로파일이 실제로 갈린다.</summary>
        internal const float BowlCutLineRatio = -0.46f;

        /// <summary>옆머리 안쪽 변의 x(=얼굴이 드러나는 폭의 절반). 앞머리 선의 반폭이기도 하다 —
        /// 두 값이 갈라지면 모서리에 이가 빠진다.</summary>
        internal const float BowlSideHalfWidthRatio = 0.78f;

        /// <summary>이마를 가로지르는 앞머리 선의 높이. 눈동자 위끝(0.227R) 위로 <b>획 반폭까지 얹어도</b>
        /// 0.141R(0.41획) 떠 있어야 한다 — 옛 앞머리 띠는 여기서 0.029R 겹쳤다(잠복 결함).</summary>
        internal const float BowlFringeLineRatio = 0.54f;

        /// <summary>앞머리 선을 쪼개는 수. 한가운데 점(x=0)이 생겨 실루엣이 두피 링 안쪽
        /// 0.54R까지 파고든 것으로 계측된다(규칙 4의 부착 검사가 정점을 본다).</summary>
        internal const int BowlFringeSegments = 4;

        // 포니테일(머리 5번) — 단정한머리 실루엣 + 뒤통수에서 아래로 떨어지는 채운 묶음 하나.
        // 묶음의 극좌표 5점은 아래 AppendHair에 있고, 가장 짧은 변이 0.52R(= 1.5획)이라 획에 먹히지 않는다.
        internal const float PonytailCapRadiusRatio = 1.16f;
        internal const float PonytailFrontLiftRatio = 0.08f;

        /// <summary>도형을 만드는 데 필요한 몸의 치수 묶음. 전부 <see cref="Core.StickmanMetrics"/> 실측값
        /// 또는 그 폴백에서 온다 — 이 구조체는 값을 <b>만들지 않고 나르기만</b> 한다.</summary>
        internal readonly struct Rig
        {
            public readonly float HeadRadius;
            public readonly float HeadCenterY;
            public readonly float ShoulderY;
            public readonly float HipY;
            public readonly float Facing;   // +1 = 오른쪽을 본다.

            public Rig(float headRadius, float headCenterY, float shoulderY, float hipY, float facing)
            {
                HeadRadius = headRadius;
                HeadCenterY = headCenterY;
                ShoulderY = shoulderY;
                HipY = hipY;
                Facing = facing >= 0f ? 1f : -1f;
            }

            public float TorsoLength => Mathf.Max(0.0001f, ShoulderY - HipY);

            /// <summary>진행 방향 기준 좌표 -> 로컬 좌표. <b>x에만</b> facing 부호를 곱한다.</summary>
            public Vector3 F(float forwardX, float localY) => new Vector3(forwardX * Facing, localY, 0f);
        }

        // ==================== 유도 치수(렌더러의 공개 프로퍼티와 테스트가 읽는 값의 근원) ====================

        internal static float HatBrimLocalY(in Rig rig) => rig.HeadCenterY + rig.HeadRadius * HatBrimLineRatio;
        internal static float HatTopLocalY(in Rig rig) => HatBrimLocalY(rig) + rig.HeadRadius * HatCrownHeightRatio;
        internal static float GlassesLocalY(in Rig rig) => rig.HeadCenterY + rig.HeadRadius * GlassesCenterRatio;
        /// <summary>목에 걸치는 것(나비넥타이/줄무늬타이/목도리/방울목걸이) <b>전부</b>의 부착 기준선.
        /// 어깨선 바로 위 = 목 밑동. 이름이 "BowTie"인 것은 이력이고, 실제 소비자는 NECK 4종 전부다.</summary>
        internal static float NeckLocalY(in Rig rig) => rig.ShoulderY + rig.HeadRadius * NeckCollarRiseRatio;

        /// <summary>이력 호환 별칭 — 렌더러의 공개 프로퍼티/테스트가 이 이름으로 읽는다.</summary>
        internal static float BowTieLocalY(in Rig rig) => NeckLocalY(rig);
        internal static float CapeCollarLocalY(in Rig rig) => rig.ShoulderY + rig.HeadRadius * CapeCollarRiseRatio;
        internal static float CapeHemLocalY(in Rig rig) => CapeCollarLocalY(rig) - rig.TorsoLength * CapeLengthRatio;

        /// <summary>
        /// ★ 33-4-1 — <b>모자가 스스로 선언하는 "내가 덮는 아래 한계선"</b>(로컬 Y).
        /// 머리(HAIR) 도형은 이 선 <b>위쪽이 잘려 나간다</b>(2026-09-01, 옛 "선 통째로 생략"에서 변경 —
        /// <see cref="AppendClippedBelowCover"/> 문단 참고).
        /// <para><see cref="float.PositiveInfinity"/> = 아무것도 가리지 않는다. 미착용도, <b>왕관</b>도 여기다 —
        /// 왕관이 예외인 것은 if 분기가 아니라 <b>이 표의 값</b>이다. 왕관은 씌우는 것이 아니라 얹는
        /// 것이라 밑이 뚫려 있고, 그래서 머리 모양이 함께 보이는 것이 옳다.</para>
        /// </summary>
        internal static float HatCoverLocalY(int hatItemIndex, in Rig rig)
        {
            switch (hatItemIndex)
            {
                case HeadCap: return HatBrimLocalY(rig);
                case HeadBeanie: return rig.HeadCenterY + rig.HeadRadius * BeanieBandBottomRatio;
                case HeadFedora: return rig.HeadCenterY + rig.HeadRadius * FedoraBrimLineRatio;
                case HeadBeret: return rig.HeadCenterY + rig.HeadRadius * BeretBrimLineRatio;
                case HeadStraw: return rig.HeadCenterY + rig.HeadRadius * StrawBrimLineRatio;
                default: return float.PositiveInfinity; // 왕관 / 미착용 / 알 수 없는 자리
            }
        }

        // ==================== 도형 ====================

        /// <summary>캡 모자의 관(crown) — 닫힌 고리.</summary>
        internal static Vector3[] HatCrown(in Rig rig)
        {
            float r = rig.HeadRadius;
            float brimY = HatBrimLocalY(rig);
            float topY = HatTopLocalY(rig);
            float halfW = r * HatCrownHalfWidthRatio;
            return new[]
            {
                rig.F(-halfW, brimY),
                rig.F(-halfW * 0.92f, brimY + (topY - brimY) * 0.55f),
                rig.F(-halfW * 0.62f, topY),
                rig.F(0f, topY + r * 0.05f),
                rig.F(halfW * 0.62f, topY),
                rig.F(halfW * 0.92f, brimY + (topY - brimY) * 0.55f),
                rig.F(halfW, brimY),
            };
        }

        /// <summary>챙 — 진행 방향으로 뻗어 살짝 처지는 닫힌 고리(모자를 <b>비대칭</b>으로 만드는 부분).</summary>
        internal static Vector3[] HatBrim(in Rig rig)
        {
            float r = rig.HeadRadius;
            float brimY = HatBrimLocalY(rig);
            float halfW = r * HatCrownHalfWidthRatio;
            return new[]
            {
                rig.F(-halfW * 0.35f, brimY),
                rig.F(halfW * 0.85f, brimY + r * 0.02f),
                rig.F(r * HatBrimReachRatio, brimY - r * HatBrimDropRatio),
                rig.F(halfW * 0.85f, brimY - r * 0.14f),
                rig.F(-halfW * 0.35f, brimY - r * HatBrimRootDropRatio),
            };
        }

        // ---- EYES 바이저(2026-09-01 재설계). 옛 GlassesLensFront/Back/Bridge/Temple 4개는 여기서
        //      지웠다 — 그 넷은 "렌즈 2개 + 코받침 + 다리"라는 <b>눈이 있는 얼굴</b>의 도형 언어였고,
        //      눈이 삭제된 지금은 어느 것도 가릴 것이 없어 얼굴 위의 빈 네모로만 남는다.

        /// <summary>선글라스 렌즈 한 장(채움 전용 사각형). <paramref name="forward"/>가 true면
        /// <b>진행 방향쪽</b> 렌즈라 <see cref="SunglassFrontBiasRatio"/>만큼 크다.
        /// <para>점 순서는 두 렌즈가 <b>같은 회전 방향</b>이 되도록 갈라 둔다 — 채움 삼각형 분할이
        /// 시계/반시계에 따라 다른 결과를 내므로, 한쪽만 뒤집히면 카드에서 한 렌즈만 구멍이 난다.</para></summary>
        internal static Vector3[] SunglassLens(in Rig rig, bool forward)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            float k = forward ? SunglassFrontBiasRatio : 1f;
            float sx = forward ? 1f : -1f;
            float inner = r * SunglassInnerRatio;
            float outer = r * SunglassOuterRatio * k;
            float innerTop = cy + r * SunglassInnerTopRatio * k;
            float innerBottom = cy + r * SunglassInnerBottomRatio * k;
            float outerTop = cy + r * SunglassOuterTopRatio * k;
            float outerBottom = cy + r * SunglassOuterBottomRatio * k;
            return forward
                ? new[]
                {
                    rig.F(inner, innerTop), rig.F(outer, outerTop),
                    rig.F(outer, outerBottom), rig.F(inner, innerBottom),
                }
                : new[]
                {
                    rig.F(sx * inner, innerBottom), rig.F(sx * outer, outerBottom),
                    rig.F(sx * outer, outerTop), rig.F(sx * inner, innerTop),
                };
        }

        /// <summary>코다리 — 두 렌즈의 <b>안쪽 꼭대기 꼭짓점 그 자체</b>를 잇는 아치(보조색).
        /// 좌표를 새로 적지 않으므로 렌즈를 고치면 다리가 따라온다(규칙 4-a).</summary>
        internal static Vector3[] SunglassBridge(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            return new[]
            {
                rig.F(-r * SunglassInnerRatio, cy + r * SunglassInnerTopRatio),
                rig.F(0f, cy + r * SunglassBridgeRiseRatio),
                rig.F(r * SunglassInnerRatio, cy + r * SunglassInnerTopRatio * SunglassFrontBiasRatio),
            };
        }

        internal static Vector3[] BowTieLeftWing(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = BowTieLocalY(rig);
            float hw = r * BowTieHalfWidthRatio, hh = r * BowTieHalfHeightRatio, knot = r * BowTieKnotRatio;
            return new[] { rig.F(-hw, cy + hh), rig.F(-knot, cy), rig.F(-hw, cy - hh), rig.F(-hw, cy + hh) };
        }

        internal static Vector3[] BowTieRightWing(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = BowTieLocalY(rig);
            float hw = r * BowTieHalfWidthRatio, hh = r * BowTieHalfHeightRatio, knot = r * BowTieKnotRatio;
            return new[] { rig.F(hw, cy + hh), rig.F(knot, cy), rig.F(hw, cy - hh), rig.F(hw, cy + hh) };
        }

        internal static Vector3[] BowTieKnot(in Rig rig)
        {
            float knot = rig.HeadRadius * BowTieKnotRatio;
            return RoundedBox(rig, 0f, BowTieLocalY(rig), knot, knot * 1.2f);
        }

        // ★ 2026-08-30 — 옛 짧은 망토 전용 CapeOutline/CapeFold(단일 인자)를 여기서 지웠다.
        //   같은 도형을 짧은/긴 망토가 매개변수로 공유하게 되면서 정의가 두 벌이 됐고,
        //   실루엣 재설계 때 <b>한쪽만 고쳐 두 망토가 다른 모양</b>이 되는 사고가 실제로 났다.
        //   지금 짧은 망토 기본값은 매개변수판 바로 아래의 오버로드 하나뿐이다.

        // ============================================================================
        // ★ 32종 도형 조립 API (2026-08-30) — 렌더러/초상화가 <b>둘 다 이것만</b> 호출한다
        // ============================================================================

        /// <summary>
        /// 한 아이템이 만드는 선 하나. 이름/점/닫힘 여부에 더해 <b>레이어</b>와 <b>흔들리는 점 구간</b>을
        /// 함께 나른다 — 이 둘을 렌더러가 아이템 이름으로 다시 분기하면 도형 정의가 두 곳으로 쪼개진다.
        /// </summary>
        internal readonly struct Shape
        {
            public readonly string Name;
            public readonly Vector3[] Points;
            public readonly bool Loop;
            public readonly int SortingOrder;

            /// <summary>33-2-5 (A) HemSway — 걸을 때 흔들리는 점 구간의 시작 인덱스(-1이면 흔들지 않는다).</summary>
            public readonly int SwayStart;

            public readonly int SwayCount;

            /// <summary>0 = 아이템 주색, 1 = 보조색. <b>색이 아니라 역할</b>을 나르는 이유는
            /// Core/ItemIconPart.Tone과 같다 — 도형 정의가 색을 몰라야 카드/몸/초상화 세 곳이
            /// 같은 색표(Core/ItemCatalog) 하나만 보게 된다.</summary>
            public readonly byte Tone;

            /// <summary>
            /// ★ 2026-08-30 사용자 신고("모자를 쓰면 모자안 머리는 안보여야하는데 머리도 보이고 모자가
            /// 투명해보임") 대응. 이 앱의 모든 그림은 <see cref="LineRenderer"/> 선화라 <b>채움 면이
            /// 없었고</b>, 그래서 모자 관(crown) 안쪽으로 머리 링의 윗호가 그대로 비쳐 보였다.
            /// <para>이 값이 참이면 그리는 쪽이 <b>같은 점으로 삼각형 면을 하나 더</b> 만들어 윤곽선
            /// 바로 아래(<c>SortingOrder − 1</c>)에 깐다. 몸통/팔다리 같은 <b>캐릭터 본체는 여전히 순수
            /// 선화</b>다 — 채우는 것은 "안에 있는 것을 가려야 하는" 물건(모자)뿐이다.</para>
            /// <para>왕관은 <b>일부러</b> 채우지 않는다. 밑이 뚫린 테라서 머리가 보이는 것이 옳고,
            /// 그 사실은 이미 <see cref="HatCoverLocalY"/>가 +∞로 선언하고 있다.</para>
            /// </summary>
            public readonly bool Filled;

            public Shape(string name, Vector3[] points, bool loop, int sortingOrder,
                int swayStart = -1, int swayCount = 0, byte tone = 0, bool filled = false)
            {
                Name = name;
                Points = points;
                Loop = loop;
                SortingOrder = sortingOrder;
                SwayStart = swayStart;
                SwayCount = swayCount;
                Tone = tone;
                Filled = filled;
            }

            public bool HasSway => SwayStart >= 0 && SwayCount > 0 && Points != null;

            /// <summary>채움 면의 레이어 — 자기 윤곽선 <b>바로 아래</b>. 상수를 새로 만들지 않는 이유는,
            /// 도형이 스스로 선언한 레이어와 채움이 어긋날 수 있는 자리를 아예 없애기 위해서다.</summary>
            public int FillSortingOrder => SortingOrder - 1;
        }

        /// <summary>보조색 역할 표식. 호출부에서 <c>tone: Accent</c>로 읽히도록 상수로 둔다.</summary>
        internal const byte Accent = 1;

        /// <summary>주색을 어둡게 한 그림자 톤. <b>채운 면 위에 그리는 선</b> 전용이다 —
        /// 같은 주색으로 그리면 면에 묻혀 사라지고, 보조색으로 그리면(망토 주름을 아이보리로 그렸던
        /// 2026-08-30 첫 시안) 천에 붙은 <b>끈</b>처럼 읽힌다. 접힌 자국은 같은 천의 그늘이어야 한다.</summary>
        internal const byte Shade = 2;

        /// <summary>
        /// 이 자리의 도형이 <b>머리에 붙어 있는가</b>. 2026-08-30 사용자 신고
        /// "손으로 머리를 만지는 행동을 하는데 모자는 가만히 있고 머리만 움직임" 대응 —
        /// 유휴 앰비언트 "주위 살피기"(States/StickmanPoseAnimator.SetBodyOffset의 headOffsetX)는
        /// <b>머리만</b> 좌우로 민다. 모자/안경/머리카락은 그 오프셋을 따라가야 하고,
        /// 넥타이/망토는 어깨선에서 유도되므로 따라가면 <b>안 된다</b>(목에 건 것이 머리를 따라가면
        /// 목이 늘어난 것처럼 보인다).
        /// </summary>
        internal static bool IsHeadAttached(EquipmentSlot slot)
            => slot == EquipmentSlot.Head || slot == EquipmentSlot.Eyes
            || slot == EquipmentSlot.Hair;

        /// <summary>
        /// 착용 중인 아이템 하나가 만드는 선들을 <paramref name="sink"/>에 넣는다.
        /// 알 수 없는 자리(표가 늘어났는데 도형이 아직 없는 경우)는 <b>아무것도 넣지 않는다</b> —
        /// 예외를 던지면 24시간 상주 앱이 도형 하나 때문에 멈춘다.
        /// </summary>
        /// <param name="hatCoverLocalY">HAIR 전용. 지금 쓴 모자가 선언한 커버선(<see cref="HatCoverLocalY"/>).</param>
        /// <param name="strokeHalfWidth">HAIR 전용. 잘라 낸 조각이 <b>획 하나보다 작으면</b> 버린다
        /// (커버선 위에 점 하나만 남는 것을 막는다 — <see cref="AppendClippedBelowCover"/> 문단).</param>
        /// <param name="mondayLoosened">NECK 전용. 33-2-5 (D) 줄무늬 타이의 요일 상태.</param>
        internal static void Append(List<Shape> sink, EquipmentSlot slot, int itemIndex, in Rig rig,
            float hatCoverLocalY = float.PositiveInfinity, float strokeHalfWidth = 0f, bool mondayLoosened = false)
        {
            if (sink == null || itemIndex < 0) return;
            switch (slot)
            {
                case EquipmentSlot.Head: AppendHead(sink, itemIndex, rig); break;
                case EquipmentSlot.Eyes: AppendEyes(sink, itemIndex, rig); break;
                case EquipmentSlot.Neck: AppendNeck(sink, itemIndex, rig, mondayLoosened); break;
                case EquipmentSlot.Shoulders: AppendBack(sink, itemIndex, rig); break;
                case EquipmentSlot.Hair: AppendHair(sink, itemIndex, rig, hatCoverLocalY, strokeHalfWidth); break;
            }
        }

        // ==================== HEAD (모자) ====================

        private static void AppendHead(List<Shape> sink, int item, in Rig rig)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;

            switch (item)
            {
                case HeadCap:
                    sink.Add(new Shape("HatCrown", HatCrown(rig), true, SortHead, filled: true));
                    sink.Add(new Shape("HatBrim", HatBrim(rig), true, SortHead, tone: Accent, filled: true));
                    break;

                case HeadBeanie:
                {
                    float bandBottom = hc + r * BeanieBandBottomRatio;
                    float bandTop = hc + r * BeanieBandTopRatio;
                    float bandHalf = r * BeanieBandHalfWidthRatio;
                    sink.Add(new Shape("BeanieBand", new[]
                    {
                        rig.F(-bandHalf, bandBottom),
                        rig.F(-bandHalf, bandTop),
                        rig.F(bandHalf, bandTop),
                        rig.F(bandHalf, bandBottom),
                    }, true, SortHead, filled: true));

                    float crownH = r * BeanieCrownHeightRatio;
                    float crownHalf = r * BeanieCrownHalfWidthRatio;
                    float crownTop = bandTop + crownH;
                    sink.Add(new Shape("BeanieCrown", new[]
                    {
                        rig.F(-crownHalf, bandTop),
                        rig.F(-crownHalf * 0.70f, bandTop + crownH * 0.72f),
                        rig.F(0f, crownTop),
                        rig.F(crownHalf * 0.70f, bandTop + crownH * 0.72f),
                        rig.F(crownHalf, bandTop),
                    }, true, SortHead, filled: true));

                    // ★ 2026-09-01 — 8각형 0.22R(1.28획) -> 10각형 0.28R(1.63획). 꼭대기는 그대로다
                    //   (BeaniePomCrestRiseRatio 문단 — 액자 상한 1.80R에 정확히 닿아 있는 자리).
                    sink.Add(new Shape("BeaniePom",
                        Polygon(rig, -r * BeaniePomBackShiftRatio, crownTop + r * BeaniePomOffsetRatio,
                            r * BeaniePomRadiusRatio, BeaniePomSegments, BeaniePomStartDegrees),
                        true, SortHead, tone: Accent, filled: true));
                    break;
                }

                case HeadFedora:
                {
                    float brimY = hc + r * FedoraBrimLineRatio;
                    float rise = r * FedoraBrimTipRiseRatio;
                    float crownHalf = r * FedoraCrownHalfWidthRatio;

                    // ★ 2026-09-01 — 관 밑변의 두 끝점을 <b>한 번만</b> 만들어 챙·관·띠 셋이 나눠 쓴다.
                    // 같은 자리에 세 번 좌표를 적으면 하나만 고쳐지는 사고가 난다(이 파일이 옛 망토에서
                    // 이미 겪은 이중 정의 계열 실패).
                    Vector3 crownBackFoot = rig.F(-crownHalf, brimY);
                    Vector3 crownFrontFoot = rig.F(crownHalf, brimY);

                    // 챙은 **양쪽 다** 뻗지만 앞이 더 길어 방향이 읽힌다(33-2-1 #3).
                    sink.Add(new Shape("FedoraBrim", new[]
                    {
                        rig.F(-r * FedoraBrimBackRatio, brimY + rise),
                        crownBackFoot,
                        crownFrontFoot,
                        rig.F(r * FedoraBrimFrontRatio, brimY + rise),
                        rig.F(crownHalf, brimY - r * 0.12f),
                        rig.F(-crownHalf, brimY - r * 0.12f),
                    }, true, SortHead, filled: true));

                    float crownTop = brimY + r * FedoraCrownHeightRatio;
                    sink.Add(new Shape("FedoraCrown", new[]
                    {
                        crownBackFoot,
                        rig.F(-crownHalf * 0.92f, crownTop),
                        rig.F(0f, crownTop + r * 0.02f),
                        rig.F(crownHalf * 0.92f, crownTop),
                        crownFrontFoot,
                    }, true, SortHead, filled: true));

                    sink.Add(new Shape("FedoraCrease", new[]
                    {
                        rig.F(-crownHalf * FedoraCreaseHalfWidthRatio, crownTop),
                        rig.F(0f, crownTop - r * FedoraCreaseDropRatio),
                        rig.F(crownHalf * FedoraCreaseHalfWidthRatio, crownTop),
                    }, false, SortHead, tone: Shade));

                    // ★ 2026-09-01 — 띠를 <b>관 밑변 그 자체</b>로 옮겼다(간격 0.41획 -> 0).
                    // 옛 띠는 밑변에서 0.14R = 0.41획 위에 떠 있었다. 규칙 4가 "최악"이라고 못박은
                    // 0 &lt; 간격 &lt; 1획 구간이라, 획(0.344R)을 얹으면 두 선의 잉크가 실제로 겹쳐
                    // <b>절반은 회색 절반은 주황인 굵은 막대</b> 하나로 뭉갠다(베레모가 같은 병이었다).
                    //
                    // 왜 <b>위로 1.5획</b>이 아니라 겹침인가 — 관 높이가 0.72R뿐이라, 위아래로 각각
                    // 1.5획(0.516R)씩 띄우려면 관이 1.03R은 돼야 한다. 산술적으로 불가능하다.
                    // 그리고 그 편이 옳다: 중절모의 띠는 원래 <b>관 밑동</b>에 감기는 리본이고,
                    // 이 자리는 챙 윗변이기도 해서 "챙 바로 위에 두른 띠"라는 실물의 그림과 정확히 맞는다.
                    // 좌표를 새로 적지 않고 위 두 끝점을 그대로 받으므로 어긋날 자리 자체가 없다.
                    sink.Add(new Shape("FedoraBand", new[] { crownBackFoot, crownFrontFoot },
                        false, SortHead, tone: Accent));
                    break;
                }

                case HeadCrown:
                {
                    float baseY = hc + r * CrownBaseRatio;
                    float half = r * CrownHalfWidthRatio;
                    sink.Add(new Shape("CrownBase", new[]
                    {
                        rig.F(-half, baseY),
                        rig.F(half, baseY),
                    }, false, SortHead, tone: Accent));

                    // 좌우 대칭이라 facing에 무관하게 같은 그림이 나온다(33-2-1 #4, 정상).
                    sink.Add(new Shape("CrownZigzag", new[]
                    {
                        rig.F(-r * 0.85f, baseY),
                        rig.F(-r * 0.78f, baseY + r * 0.95f),
                        rig.F(-r * 0.40f, baseY + r * 0.40f),
                        rig.F(0f, baseY + r * 1.25f),
                        rig.F(r * 0.40f, baseY + r * 0.40f),
                        rig.F(r * 0.78f, baseY + r * 0.95f),
                        rig.F(r * 0.85f, baseY),
                    }, false, SortHead));
                    break;
                }

                case HeadBeret:
                {
                    // 털모자 관을 뒤로 늘어뜨린 변주 — 밴드도 꼭지도 없다. 그 둘이 없는 것이 정체다.
                    float brimY = hc + r * BeretBrimLineRatio;
                    float top = brimY + r * BeretCrownHeightRatio;
                    Vector3 backTip = rig.F(-r * BeretBackDroopRatio, brimY - r * BeretBackDroopDropRatio);
                    Vector3 frontTip = rig.F(r * BeretFrontRatio, brimY);   // 가장 짧은 변 0.39R = 1.13획
                    sink.Add(new Shape("BeretCrown", new[]
                    {
                        backTip,
                        rig.F(-r * 0.95f, brimY + r * 0.34f),
                        rig.F(0f, top),
                        rig.F(r * 0.66f, brimY + r * 0.36f),
                        frontTip,
                    }, true, SortHead, filled: true));

                    // ★ 2026-09-01 — 테를 <b>밑변 그 자체</b>로 옮겼다(간격 0.01~0.26획 -> 0).
                    // 옛 테는 자기 밑변과 0 &lt; 간격 &lt; 1획, 규칙 4가 "최악"이라고 못박은 구간
                    // 한가운데였다. 관 높이가 0.66R뿐이라 1.5획(0.516R) 위로 올리면 테가 관을
                    // 가로질러 <b>띠 두른 정모</b>가 된다 — 페르소나가 실물에서 본 그 그림이다.
                    // 그래서 규칙 4가 허용하는 나머지 안전 구간인 <b>겹침</b>을 택했다: 새 점을
                    // 만들지 않고 밑변의 두 끝을 그대로 받아 쓴다(어긋날 자리 자체가 없다).
                    sink.Add(new Shape("BeretRim", new[] { frontTip, backTip }, false, SortHead,
                        tone: Accent));
                    break;
                }

                case HeadStraw:
                {
                    // 중절모의 납작·광폭 변주. 챙이 양쪽으로 넓게 나가고 관이 낮다.
                    float brimY = hc + r * StrawBrimLineRatio;
                    float crownHalf = r * StrawCrownHalfWidthRatio;
                    float drop = r * StrawBrimDropRatio;

                    // 중절모와 같은 규약 — 관 밑변 두 끝점을 챙·관·띠가 공유한다.
                    Vector3 crownBackFoot = rig.F(-crownHalf, brimY);
                    Vector3 crownFrontFoot = rig.F(crownHalf, brimY);

                    sink.Add(new Shape("StrawBrim", new[]
                    {
                        rig.F(-r * StrawBrimBackRatio, brimY + r * 0.06f),
                        crownBackFoot,
                        crownFrontFoot,
                        rig.F(r * StrawBrimFrontRatio, brimY + r * 0.06f),
                        rig.F(crownHalf, brimY - drop),
                        rig.F(-crownHalf, brimY - drop),
                    }, true, SortHead, filled: true));

                    float crownTop = brimY + r * StrawCrownHeightRatio;
                    sink.Add(new Shape("StrawCrown", new[]
                    {
                        crownBackFoot,
                        rig.F(-crownHalf * 0.90f, crownTop),
                        rig.F(crownHalf * 0.90f, crownTop),
                        crownFrontFoot,
                    }, true, SortHead, filled: true));

                    // ★ 2026-09-01 — 중절모와 <b>같은 결함·같은 해법</b>(간격 0.47획 -> 0).
                    // 이 모자는 관이 0.54R로 더 낮아서 사정이 더 나빴다: 띠를 띄울 수 있는 여지가
                    // 중절모보다도 작다(1.5획을 위아래로 두려면 관이 1.03R이어야 한다).
                    // 옛 띠는 폭도 관보다 2% 좁아(±0.98) 밑변과 <b>끝만 살짝 어긋난</b> 선이었다 —
                    // 이제 밑변의 끝점을 그대로 받으므로 폭도 정확히 관과 같다.
                    sink.Add(new Shape("StrawBand", new[] { crownBackFoot, crownFrontFoot },
                        false, SortHead, tone: Accent));
                    break;
                }
            }
        }

        // ============================================================================
        // ★ EYES (가리개) — 2026-09-01 "불투명 바이저" 전면 재설계
        // ============================================================================
        // 리더 결정(docs/UX_FLOW.md 38-7, 옵션 E2): 슬롯은 유지하되, 안경을 "렌즈 테두리 + 그 안에
        // 눈동자가 비치는" 물건에서 <b>불투명한 가리개</b>로 다시 그린다.
        //
        // 왜 그림 언어가 통째로 바뀌는가 — 전제가 사라졌다:
        //   · 옛 6종은 전부 <b>채움 없는 윤곽선</b>이었다(안대만 예외). 그 설계는 "렌즈 안으로 눈동자가
        //     비친다"를 그림의 내용으로 삼았고, 그래서 렌즈가 작아도 뜻이 통했다.
        //   · 같은 날 눈이 삭제됐다(Editor/SceneBootstrapper.BakeEyes = false /
        //     Interaction/CharacterPortraitStage.DrawEyes = false). 비칠 것이 없어지자 6종은
        //     "검은 얼굴 위에 그은 빈 네모"로 붕괴한다 — 37-3 (B)가 이미 그 상태라고 진단했고,
        //     눈 삭제가 그것을 확정했다.
        //   · 그래서 이제 <b>모든 아이템이 스스로 불투명한 판</b>이다. 뒤에 무엇이 있든(눈이 돌아와도)
        //     가림은 <b>채움 면과 레이어가</b> 정한다. 카테고리 조건문으로 눈을 지우던 옛 결함
        //     (Tests/PlayMode/PortraitEyeVisibilityTests 문서)이 원리적으로 되살아날 수 없다.
        //
        // 지킨 규칙(전부 오프라인 검산 후 좌표를 잡았다. 획 예산 W = 0.344R @배율 0.75):
        //   규칙 1 — 그려지는 모든 변 ≥ 1.0W, 잉크 사각형 ≥ 1.5W. 가장 빠듯한 자리는
        //            고글 좌우 변 0.386R(1.12획)과 선글라스 앞 경사 0.62R(1.81획)이다.
        //   규칙 2 — 실루엣은 <b>반드시</b> filled: true. 이 카테고리의 존재 이유가 "가린다"이므로
        //            채움은 옵션이 아니다. 6종 모두 채움 도형을 갖는다.
        //   규칙 3 — 보조색 도형은 아이템당 <b>정확히 1개</b>(다리/코받침/스트랩/체인/눈썹테/끈).
        //   규칙 5 — 구성 정원 2~3개. 옛 세트는 4개까지 있었고 그 4번째가 전부 획에 먹혔다.
        //   규칙 8 — 색은 여기서 정하지 않는다. Core/ItemCatalog.WornColor가 채도 하한 0.42 +
        //            명도 창 0.55~0.80을 강제하므로 <b>흰 잉크에서도 검은 잉크에서도</b> 판이 뜬다.
        //
        // 6종의 구분은 <b>판의 형태와 개수</b>다(색만으로 나누지 않는다 — 잉크 프리셋에 따라 색은
        // 흔들려도 실루엣은 안 흔들린다). 오프라인 래스터(셀 = W/2)로 잰 쌍별 실루엣 차이는
        // 최소 0.27(외알 vs 안대)이고, 그 하한을 Tests/EditMode/EyesVisorOpacityTests가 잠근다.
        //
        //     0 선글라스   판 1 · 1.48R×0.68R · 코앞으로 뾰족    + 관자놀이 다리
        //     1 동그란안경 판 2 · 팟 2개(지름 0.60R, 간격 1.51획) + 코받침
        //     2 고글       판 1 · 1.92R×0.92R · 카테고리 최대     + 머리를 도는 스트랩
        //     3 외알안경   판 1 · 앞쪽 눈에만(지름 0.80R)         + 늘어진 체인
        //     4 뿔테안경   판 2 · 렌즈 판 + 그보다 넓은 눈썹테     (눈썹테가 보조색)
        //     5 안대       판 1 · 앞쪽 눈에만(방패꼴)             + 머리를 도는 끈

        private static void AppendEyes(List<Shape> sink, int item, in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);

            switch (item)
            {
                case EyesSunglasses:
                    // 렌즈 2장 + 코다리. "안경이라는 물건"의 최소 신호이자, 머리가 없는 카드에서도
                    // 성립하는 유일한 구성이다(위 SunglassInnerRatio 문단).
                    sink.Add(new Shape("SunglassLensBack", SunglassLens(rig, forward: false),
                        true, SortEyes, filled: true));
                    sink.Add(new Shape("SunglassLensFront", SunglassLens(rig, forward: true),
                        true, SortEyes, filled: true));
                    sink.Add(new Shape("SunglassBridge", SunglassBridge(rig), false, SortEyes, tone: Accent));
                    break;

                case EyesRound:
                {
                    // 채운 렌즈 2개 + <b>꼭대기를 잇는</b> 코다리. 코다리가 렌즈 한가운데를 가로지르면
                    // 그 순간 아령이 된다(V2) — 높이가 이 아이템의 정체다.
                    float dx = r * RoundLensOffsetRatio;
                    float rad = r * RoundLensRadiusRatio;
                    Vector3[] back = Polygon(rig, -dx, cy, rad, RoundLensSegments);
                    Vector3[] front = Polygon(rig, dx, cy, rad, RoundLensSegments);
                    sink.Add(new Shape("RoundLensBack", back, true, SortEyes, filled: true));
                    sink.Add(new Shape("RoundLensFront", front, true, SortEyes, filled: true));
                    // 60도(뒤 렌즈) / 120도(앞 렌즈) 꼭짓점 = 두 렌즈의 <b>안쪽 꼭대기</b>.
                    // 12각형이라 인덱스로 정확히 집힌다(2번 = 60도, 4번 = 120도).
                    sink.Add(new Shape("RoundBridge", new[]
                    {
                        back[2], rig.F(0f, cy + r * RoundBridgeRiseRatio), front[4],
                    }, false, SortEyes, tone: Accent));
                    break;
                }

                case EyesGoggles:
                {
                    float hw = r * GoggleHalfWidthRatio;
                    float hh = r * GoggleHalfHeightRatio;
                    float cornerX = hw * GoggleCornerXRatio;
                    float cornerY = hh * GoggleCornerYRatio;
                    float strap = r * GoggleStrapReachRatio;
                    float strapHalf = r * GoggleStrapHalfHeightRatio;
                    float rim = r * GoggleRimThicknessRatio;

                    // 스트랩+테는 <b>렌즈를 한 점도 덮지 않는다</b> — 되돌아오는 변이 렌즈의 윗변
                    // 그 자체라 두 채움이 겹치지 않는다. EYES 채움은 전부 같은 레이어(SortEyes−1)라
                    // 겹치면 그리기 순서가 미정이 되고, 그때 어느 판이 위로 올지 보장할 수 없다.
                    sink.Add(new Shape("GoggleStrap", new[]
                    {
                        rig.F(-strap, -strapHalf + cy), rig.F(-strap, strapHalf + cy),
                        rig.F(-hw - r * 0.20f, cy + cornerY + r * 0.14f),
                        rig.F(-cornerX - r * 0.12f, cy + hh + rim),
                        rig.F(cornerX + r * 0.12f, cy + hh + rim),
                        rig.F(hw + r * 0.20f, cy + cornerY + r * 0.14f),
                        rig.F(strap, strapHalf + cy), rig.F(strap, -strapHalf + cy),
                        rig.F(hw, cy - cornerY), rig.F(hw, cy + cornerY),
                        rig.F(cornerX, cy + hh), rig.F(-cornerX, cy + hh),
                        rig.F(-hw, cy + cornerY), rig.F(-hw, cy - cornerY),
                    }, true, SortEyes, tone: Accent, filled: true));

                    sink.Add(new Shape("GoggleLens", new[]
                    {
                        rig.F(-cornerX, cy - hh),
                        rig.F(-hw, cy - cornerY),
                        rig.F(-hw, cy + cornerY),
                        rig.F(-cornerX, cy + hh),
                        rig.F(cornerX, cy + hh),
                        rig.F(hw, cy + cornerY),
                        rig.F(hw, cy - cornerY),
                        rig.F(cornerX, cy - hh),
                    }, true, SortEyes, filled: true));
                    break;
                }

                case EyesMonocle:
                {
                    // 앞쪽 눈에만 있다(33-2-2 #4). ★ 2026-09-01(2차): 옛 체인은 알에서 0.30획 떠 있어
                    // 카드에서 "금색 원 + 흰 선"이 따로 놀았다(V4). 이제 체인의 첫 점이 알의
                    // <b>맨 아래 꼭짓점 그 자체</b>다 — 간격 0.
                    float cxm = r * MonocleOffsetRatio;
                    Vector3[] pod = Polygon(rig, cxm, cy, r * MonocleRadiusRatio, RoundLensSegments);
                    sink.Add(new Shape("MonoclePod", pod, true, SortEyes, filled: true));
                    sink.Add(new Shape("MonocleChain", new[]
                    {
                        pod[9],                                   // 270도 = 알의 최하점
                        rig.F(r * 0.26f, cy - r * 0.92f),
                        rig.F(r * 0.56f, cy - r * 1.22f),
                    }, false, SortEyes, tone: Accent));
                    break;
                }

                case EyesBrowline:
                {
                    // 굵은 눈썹테(보조색) <b>아래에</b> 렌즈 2장이 매달린다. 테 밑변과 렌즈 윗변이
                    // 같은 y라 간격 0이다(규칙 4).
                    float barBottom = cy + r * BrowlineBarBottomRatio;
                    sink.Add(new Shape("BrowlineBar", new[]
                    {
                        rig.F(-r * BrowlineBarOuterRatio, barBottom),
                        rig.F(-r * BrowlineBarInnerRatio, cy + r * BrowlineBarTopRatio),
                        rig.F(r * BrowlineBarInnerRatio, cy + r * BrowlineBarTopRatio),
                        rig.F(r * BrowlineBarOuterRatio, barBottom),
                    }, true, SortEyes, tone: Accent, filled: true));

                    sink.Add(new Shape("BrowlineLensBack", BrowlineLens(rig, forward: false),
                        true, SortEyes, filled: true));
                    sink.Add(new Shape("BrowlineLensFront", BrowlineLens(rig, forward: true),
                        true, SortEyes, filled: true));
                    break;
                }

                case EyesPatch:
                {
                    // 외알안경과 같은 규약 — <b>앞쪽 눈에만</b> 있다(33-2-2 #4).
                    float px = r * PatchOffsetRatio;
                    float phw = r * PatchHalfWidthRatio;
                    float phh = r * PatchHalfHeightRatio;
                    // RoundedBox를 쓰지 않는다 — 그 8각 근사는 이 크기에서 마지막 변이 획보다 짧아
                    // 화면에서 통째로 먹힌다(37-6 규칙 1). 네 변 전부가 획보다 긴 방패꼴로 대신한다.
                    var cover = new[]
                    {
                        rig.F(px - phw, cy + phh),
                        rig.F(px + phw, cy + phh * 0.86f),
                        rig.F(px + phw * 0.92f, cy - phh),
                        rig.F(px - phw, cy - phh * 0.86f),
                    };
                    sink.Add(new Shape("PatchCover", cover, true, SortEyes, filled: true));
                    // 끈은 천의 <b>뒤쪽 두 꼭짓점에서 출발</b>해 머리를 돌아 넘어간다. 끝점은 머리
                    // 원 위(반경 0.99R)라 허공에서 끊기지 않는다.
                    sink.Add(new Shape("PatchStrap", new[]
                    {
                        rig.F(-r * PatchStrapReachRatio, cy + r * PatchStrapRiseRatio),
                        cover[0], cover[3],
                        rig.F(-r * PatchStrapReachRatio, cy - r * PatchStrapRiseRatio),
                    }, false, SortEyes, tone: Accent));
                    break;
                }
            }
        }

        /// <summary>뿔테 렌즈 한 장 — 테 밑변에서 시작해 아래로 둥글게 좁아진다.
        /// 선글라스와 같은 이유로 두 장의 회전 방향을 맞춰 둔다(채움 분할 안정성).</summary>
        internal static Vector3[] BrowlineLens(in Rig rig, bool forward)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            float top = cy + r * BrowlineBarBottomRatio;
            float inner = r * BrowlineLensInnerRatio;
            float outer = r * BrowlineLensOuterRatio;
            var pts = new[]
            {
                new Vector2(inner, top),
                new Vector2(outer, top),
                new Vector2(r * 0.86f, cy - r * 0.32f),
                new Vector2(r * 0.54f, cy - r * 0.52f),
                new Vector2(r * 0.26f, cy - r * 0.26f),
            };
            var result = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 p = forward ? pts[i] : pts[pts.Length - 1 - i];
                result[i] = rig.F(forward ? p.x : -p.x, p.y);
            }
            return result;
        }

        // ==================== NECK (넥타이) ====================

        private static void AppendNeck(List<Shape> sink, int item, in Rig rig, bool mondayLoosened)
        {
            float r = rig.HeadRadius;
            float ty = NeckLocalY(rig);

            switch (item)
            {
                case NeckBowTie:
                    sink.Add(new Shape("BowTieLeftWing", BowTieLeftWing(rig), false, SortNeck));
                    sink.Add(new Shape("BowTieRightWing", BowTieRightWing(rig), false, SortNeck));
                    sink.Add(new Shape("BowTieKnot", BowTieKnot(rig), true, SortNeck, tone: Accent));
                    break;

                case NeckStriped:
                {
                    // 33-2-5 (D) — 월요일에는 매듭이 R·0.12 내려가고 blade가 3도 기운다.
                    float knotY = mondayLoosened ? ty - r * TieMondayLoosenDropRatio : ty;
                    float tilt = mondayLoosened ? TieMondayLoosenTiltDegrees * Mathf.Deg2Rad : 0f;
                    float kw = r * TieKnotHalfWidthRatio;
                    float kh = r * TieKnotHalfHeightRatio;

                    sink.Add(new Shape("TieKnot", new[]
                    {
                        rig.F(-kw, knotY + kh),
                        rig.F(kw, knotY + kh),
                        rig.F(kw * 0.68f, knotY - kh),
                        rig.F(-kw * 0.68f, knotY - kh),
                    }, true, SortNeck));

                    float pivotY = knotY - kh;
                    float len = rig.TorsoLength * TieBladeLengthInTorso;
                    float bw = r * TieBladeHalfWidthRatio;

                    // 매듭 아래변을 축으로 기울인다(도형 전체를 다시 정의하지 않는다).
                    // in 파라미터는 로컬 함수가 캡처할 수 없어 한 번 복사한다(readonly struct라 안전).
                    Rig bladeRig = rig;
                    Vector3 Blade(float fx, float dy)
                    {
                        float rx = fx * Mathf.Cos(tilt) - dy * Mathf.Sin(tilt);
                        float ry = fx * Mathf.Sin(tilt) + dy * Mathf.Cos(tilt);
                        return bladeRig.F(rx, pivotY + ry);
                    }

                    // 끝 V의 3점(인덱스 2~4)이 흔들린다. 33절은 "끝 2점"이라고 적었지만 V의 꼭짓점만
                    // 남기고 두 어깨점을 고정하면 V가 찢어져 보인다 — 세 점을 함께 흔든다(도형 무결성 우선).
                    sink.Add(new Shape("TieBlade", new[]
                    {
                        Blade(-bw, 0f),
                        Blade(bw, 0f),
                        Blade(bw, -(len - r * 0.14f)),
                        Blade(0f, -len),
                        Blade(-bw, -(len - r * 0.14f)),
                    }, true, SortNeck, swayStart: 2, swayCount: 3));

                    sink.Add(new Shape("TieStripeA", new[]
                    {
                        Blade(-bw, -len * 0.30f),
                        Blade(bw, -len * 0.30f - r * 0.18f),
                    }, false, SortNeck, tone: Accent));
                    sink.Add(new Shape("TieStripeB", new[]
                    {
                        Blade(-bw, -len * 0.58f),
                        Blade(bw, -len * 0.58f + r * 0.30f),
                    }, false, SortNeck, tone: Accent));
                    break;
                }

                case NeckScarf:
                {
                    // 목에 감긴 <b>고리</b> + 앞뒤로 하나씩 늘어진 자락. 자락 길이가 서로 다른 것이
                    // 이 아이템의 식별 특징이다(형제인 반다나는 자락이 앞으로 하나뿐이다).
                    float wh = r * ScarfWrapHalfWidthRatio;
                    sink.Add(new Shape("ScarfWrap", new[]
                    {
                        rig.F(-wh, ty + r * ScarfWrapTopRatio),
                        rig.F(0f, ty + r * ScarfWrapCenterTopRatio),
                        rig.F(wh, ty + r * ScarfWrapTopRatio),
                        rig.F(wh, ty + r * ScarfWrapSideRatio),
                        rig.F(0f, ty + r * ScarfWrapDipRatio),
                        rig.F(-wh, ty + r * ScarfWrapSideRatio),
                    }, true, SortNeck, filled: true));

                    // 끝 2점(인덱스 2,3)이 흔들린다 — 33-2-5 (A)가 지정한 그대로.
                    float front = rig.TorsoLength * ScarfFrontTailLengthInTorso;
                    sink.Add(new Shape("ScarfTailFront", new[]
                    {
                        rig.F(r * 0.06f, ty - r * 0.44f),
                        rig.F(r * 0.48f, ty - r * 0.34f),
                        rig.F(r * 1.02f, ty - front),
                        rig.F(r * 0.58f, ty - front - rig.TorsoLength * 0.06f),
                    }, true, SortNeck, swayStart: 2, swayCount: 2, tone: Accent, filled: true));

                    float back = rig.TorsoLength * ScarfBackTailLengthInTorso;
                    sink.Add(new Shape("ScarfTailBack", new[]
                    {
                        rig.F(-r * 0.50f, ty - r * 0.30f),
                        rig.F(-r * 0.08f, ty - r * 0.44f),
                        rig.F(-r * 0.44f, ty - back),
                        rig.F(-r * 0.92f, ty - back + rig.TorsoLength * 0.06f),
                    }, true, SortNeck, swayStart: 2, swayCount: 2, tone: Accent, filled: true));
                    break;
                }

                case NeckBell:
                {
                    sink.Add(new Shape("Collar", CollarCurve(rig, ty), false, SortNeck));

                    // 위 꼭짓점이 <b>목줄 최저점 그 자리</b>다 — 펜던트와 똑같은 유도(규칙 4-a).
                    // 그래서 첫 점을 90도(정수리 방향)에서 시작한다: 10각형은 위상 0도로 두면 가장 높은
                    // 꼭짓점이 72도에 놓여 매단 자리가 0.11획 어긋난다(옛 방울이 정확히 그 상태였다).
                    float bellR = r * BellRadiusRatio;
                    float bellY = CollarLowLocalY(rig, ty) - bellR;
                    // 33-2-5 (C) — 오디오 시스템이 없으므로 "소리"는 만들 수 없다. 대신 방울이
                    // 걸을 때 실제로 흔들리게 해 설명문(리더 승인으로 '흔들린다'로 교체 예정)과 맞춘다.
                    sink.Add(new Shape("Bell", Polygon(rig, 0f, bellY, bellR, BellSegments, 90f),
                        true, SortNeck, swayStart: 0, swayCount: BellSegments, tone: Accent, filled: true));

                    // ★ 2026-09-01 — 옛 'BellClapper'(공 아래로 0.10R 내린 선)를 여기서 지웠다.
                    // 잉크 사각형이 <b>0.29획</b>이라 화면에 존재하지 않는 선이었고, 규칙 1을 만족시킬
                    // 길이(1.5획 = 0.516R)로 늘리면 방울 지름보다 길어져 <b>꼬리 달린 공</b>이 된다.
                    // 37-6 규칙 5가 그 경우를 명시한다 — "[선택] 디테일은 W 예산을 못 지키면 넣지 않는다".
                    // 덤으로 보조색 도형이 2개 -> 1개가 되어 규칙 3-2("보조색은 단 한 부분")도 맞는다.
                    break;
                }

                case NeckPendant:
                {
                    // 방울 목걸이의 목줄을 <b>그대로</b> 쓰고(같은 부착선·같은 곡률) 매달린 것만 바꾼다.
                    sink.Add(new Shape("Chain", CollarCurve(rig, ty), false, SortNeck));

                    // 위 꼭짓점을 <b>목줄 최저점 그 자리</b>에 둔다 — 매달린 지점이 보여야 물건이
                    // 공중에 뜨지 않는다(규칙 4). 드롭을 따로 적어 두면 목줄 곡률을 바꿀 때 어긋난다.
                    float hangY = CollarLowLocalY(rig, ty);
                    float phw = r * PendantHalfWidthRatio;
                    float phh = r * PendantHalfHeightRatio;
                    float py = hangY - phh;
                    // 방울과 같은 이유로 흔들린다(33-2-5 (C) — 오디오가 없으므로 움직임으로 말한다).
                    sink.Add(new Shape("Pendant", new[]
                    {
                        rig.F(0f, hangY),
                        rig.F(phw, py),
                        rig.F(0f, py - phh),
                        rig.F(-phw, py),
                    }, true, SortNeck, swayStart: 0, swayCount: 4, tone: Accent, filled: true));
                    break;
                }

                case NeckBandana:
                {
                    // 목도리 띠의 납작 변주 + 앞으로 늘어진 삼각 자락 하나(목도리는 뒤로 두 갈래다).
                    float wrapY = ty + r * BandanaWrapRiseRatio;
                    float wh = r * BandanaWrapHalfWidthRatio;
                    float wv = r * BandanaWrapHalfHeightRatio;
                    sink.Add(new Shape("BandanaWrap", new[]
                    {
                        rig.F(-wh, wrapY + wv),
                        rig.F(0f, wrapY + wv * 0.72f),
                        rig.F(wh, wrapY + wv),
                        rig.F(wh, wrapY - wv),
                        rig.F(0f, wrapY - wv * 1.55f),
                        rig.F(-wh, wrapY - wv),
                    }, true, SortNeck, filled: true));

                    float thw = r * BandanaTailHalfWidthRatio;
                    float tipY = wrapY - wv - rig.TorsoLength * BandanaTailLengthRatio * 0.42f;
                    // 끝 꼭짓점(인덱스 2) 하나만 흔든다 — 밑변 두 점은 매듭이라 고정이다.
                    sink.Add(new Shape("BandanaTail", new[]
                    {
                        rig.F(r * 0.06f, wrapY - wv * 0.6f),
                        rig.F(r * 0.06f + thw * 2f, wrapY - wv * 0.6f),
                        rig.F(r * 0.06f + thw, tipY),
                    }, true, SortNeck, swayStart: 2, swayCount: 1, tone: Accent, filled: true));
                    break;
                }
            }
        }

        /// <summary>목줄 — 아래로 볼록한 5점 곡선. 방울 목걸이와 펜던트 목걸이가 <b>같은 하나</b>를 쓴다
        /// (두 벌로 두면 부착선이 갈라져 한쪽만 목에서 뜬다 — 이 파일이 옛 망토에서 이미 겪은 실패다).</summary>
        private static Vector3[] CollarCurve(in Rig rig, float neckY)
        {
            float r = rig.HeadRadius;
            float ch = r * CollarHalfWidthRatio;
            var collar = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f * 2f - 1f;
                // 양 끝 +CollarRise, 한가운데 그보다 CollarDip 아래(= 아래로 볼록한 목줄)
                collar[i] = rig.F(ch * t,
                    neckY + r * CollarRiseRatio - (1f - t * t) * r * CollarDipRatio);
            }
            return collar;
        }

        /// <summary>목줄이 가장 아래로 처지는 점의 로컬 y. <b>매달리는 것</b>이 이 값에서 시작한다.</summary>
        internal static float CollarLowLocalY(in Rig rig, float neckY)
            => neckY + rig.HeadRadius * (CollarRiseRatio - CollarDipRatio);

        // ==================== BACK (망토/날개/배낭) ====================

        /// <summary>깃 한 짝. <paramref name="sign"/> −1이 진행 반대쪽(등 뒤), +1이 진행 방향쪽이다.
        /// <para>두 짝의 <b>뿌리 점이 같은 자리</b>(x=0)라 등 한가운데에서 만난다 — 그래서 등뼈까지
        /// 세 도형이 하나로 이어지고, "날개는 쌍"이라는 이름의 조건이 좌표로 성립한다.
        /// 점 순서는 두 짝이 <b>같은 회전 방향</b>이 되도록 갈라 둔다(채움 분할 안정성).</para></summary>
        internal static Vector3[] WingBlade(in Rig rig, float sign)
        {
            float r = rig.HeadRadius;
            float sy = rig.ShoulderY;
            float outer = r * WingOuterReachRatio;
            float mid = r * WingMidReachRatio;
            var pts = new[]
            {
                new Vector2(0f, sy + r * WingRootRiseRatio),
                new Vector2(r * 1.05f, sy + r * 0.62f),
                new Vector2(outer, sy + r * WingOuterRiseRatio),
                new Vector2(outer * 0.68f, sy + r * 0.30f),
                new Vector2(mid, sy - r * 0.14f),
                new Vector2(mid * 0.60f, sy - r * 0.26f),
                new Vector2(r * WingInnerReachRatio, sy - r * 0.74f),
                new Vector2(r * 0.44f, sy - r * 0.46f),
            };
            return Mirrored(rig, pts, sign);
        }

        /// <summary>요정 깃 한 짝 — <see cref="WingBlade"/>의 작고 둥근 변주(깃이 하나 적다).</summary>
        internal static Vector3[] FairyWingBlade(in Rig rig, float sign)
        {
            float r = rig.HeadRadius;
            float sy = rig.ShoulderY;
            float outer = r * FairyWingOuterReachRatio;
            var pts = new[]
            {
                new Vector2(0f, sy + r * WingRootRiseRatio),
                new Vector2(outer * 0.52f, sy + r * 0.72f),
                new Vector2(outer, sy + r * FairyWingOuterRiseRatio),
                new Vector2(outer * 0.86f, sy + r * 0.12f),
                new Vector2(r * FairyWingInnerReachRatio, sy - r * 0.52f),
                new Vector2(r * 0.44f, sy - r * 0.40f),
            };
            return Mirrored(rig, pts, sign);
        }

        /// <summary>진행 방향쪽으로 정의된 점열을 <paramref name="sign"/>쪽 짝으로 만든다.
        /// 음수쪽은 점 순서를 뒤집는다 — 그래야 두 짝의 회전 방향이 같아져 채움 삼각형 분할이
        /// 한쪽만 뒤집히는 일이 없다.</summary>
        private static Vector3[] Mirrored(in Rig rig, Vector2[] pts, float sign)
        {
            var result = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 p = sign >= 0f ? pts[i] : pts[pts.Length - 1 - i];
                result[i] = rig.F(sign >= 0f ? p.x : -p.x, p.y);
            }
            return result;
        }

        private static void AppendBack(List<Shape> sink, int item, in Rig rig)
        {
            float r = rig.HeadRadius;

            switch (item)
            {
                case BackCape:
                    // 밑단 5점(인덱스 2~6)이 흔들린다 — "늘 가는 방향의 반대쪽으로 날린다".
                    // 재설계로 밑단이 앞쪽까지 벌어지면서 흔들 구간도 뒤 3점 -> 밑단 전체가 됐다.
                    sink.Add(new Shape("CapeOutline", CapeOutline(rig), true, SortBack,
                        swayStart: 2, swayCount: 5, filled: true));
                    // ★ 2026-08-31 — 주름 <b>끝점</b>(인덱스 1)도 흔들 구간에 넣는다. 이 두 선은 천에 진
                    //   그늘이라, 천이 젖혀지는데 그늘만 제자리에 남으면 "천 위에 붙은 끈"으로 돌아간다
                    //   (그 오해는 2026-08-30 첫 시안에서 이미 한 번 겪었다 — Shade 상수 문서 참고).
                    //   시작점(옷깃, 인덱스 0)은 어깨에 고정된 자리라 움직이면 안 된다.
                    sink.Add(new Shape("CapeFold", CapeFold(rig), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    sink.Add(new Shape("CapeFold2",
                        CapeFold(rig, CapeLengthRatio, CapeSpreadRatio, 0.72f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    break;

                case BackLongCape:
                    sink.Add(new Shape("CapeOutline",
                        CapeOutline(rig, LongCapeLengthRatio, LongCapeSpreadRatio, LongCapeHemWaveRatio,
                            LongCapeFrontSpreadRatio, LongCapeHemNotchRatio),
                        true, SortBack, swayStart: 2, swayCount: 5, filled: true));
                    // ★ 주름의 끝 x를 <b>명시</b>한다. 기본 유도값(0.42 / 0.64)은 제비꼬리 골이 파인
                    //   자리를 그대로 지나가 주름 끝이 천 <b>바깥</b>(갈라진 틈)에 떨어진다.
                    sink.Add(new Shape("CapeFold",
                        CapeFold(rig, LongCapeLengthRatio, LongCapeSpreadRatio, 0.35f, 0.80f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    sink.Add(new Shape("CapeFold2",
                        CapeFold(rig, LongCapeLengthRatio, LongCapeSpreadRatio, 0.72f, 0.96f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    break;

                case BackWings:
                {
                    float sy = rig.ShoulderY;
                    // ★ "뜨지는 않지만 폼은 난다" — 어떤 상태에서도 y 오프셋을 주지 않는다(33-2-4 #3).
                    // 등뼈는 두 깃이 만나는 <b>그 점</b>에서 시작한다(규칙 4 — 좌표를 새로 적지 않는다).
                    sink.Add(new Shape("WingSpine", new[]
                    {
                        rig.F(0f, sy + r * WingRootRiseRatio),
                        rig.F(0f, sy - rig.TorsoLength * WingSpineDropInTorso),
                    }, false, SortBack, tone: Accent));

                    sink.Add(new Shape("WingFeatherA", WingBlade(rig, -1f), true, SortBack, filled: true));
                    sink.Add(new Shape("WingFeatherB", WingBlade(rig, +1f), true, SortBack, filled: true));
                    break;
                }

                case BackBackpack:
                {
                    float sy = rig.ShoulderY;
                    float cx = -r * PackCenterBackRatio;
                    float cyp = sy - rig.TorsoLength * PackDropInTorso;
                    float hw = r * PackHalfWidthRatio;
                    float hh = rig.TorsoLength * PackHalfHeightInTorso;

                    // ★ 2026-09-01 — 어깨끈이 매달릴 <b>배낭 몸의 실재하는 꼭짓점</b>을 한 번만 만들어
                    // 몸과 끈이 나눠 쓴다. 옛 끈의 끝점 (cx+hw, cyp+hh)은 팔각형 <b>모따기 바깥</b>,
                    // 즉 몸에서 0.64획 떠 있는 자리였다 — 규칙 4가 "최악"이라고 못박은 0 &lt; 간격 &lt; 1획
                    // 구간이다. 동시에 끈의 잉크 사각형이 1.32획(문턱 1.5획)이라 규칙 1도 어겼다.
                    // 끝점을 이 꼭짓점으로 내리면 <b>간격 0(겹침) · 잉크 2.30획</b>으로 둘이 함께 닫힌다.
                    // 좌표를 새로 적지 않으므로 어긋날 자리 자체가 없다(중절모 띠/베레모 테와 같은 규약).
                    Vector3 packStrapAnchor = rig.F(cx + hw, cyp + hh * 0.62f);

                    sink.Add(new Shape("PackBody", new[]
                    {
                        rig.F(cx - hw, cyp - hh * 0.62f),
                        rig.F(cx - hw, cyp + hh * 0.62f),
                        rig.F(cx - hw * 0.55f, cyp + hh),
                        rig.F(cx + hw * 0.55f, cyp + hh),
                        packStrapAnchor,
                        rig.F(cx + hw, cyp - hh * 0.62f),
                        rig.F(cx + hw * 0.55f, cyp - hh),
                        rig.F(cx - hw * 0.55f, cyp - hh),
                    }, true, SortBack, filled: true));

                    float flapY = cyp + hh - rig.TorsoLength * PackFlapDropInTorso;
                    sink.Add(new Shape("PackFlap", new[]
                    {
                        rig.F(cx - hw * 0.92f, flapY),
                        rig.F(cx + hw * 0.92f, flapY),
                    }, false, SortBack, tone: Accent));

                    sink.Add(new Shape("PackStrap", new[]
                    {
                        rig.F(r * 0.10f, sy),
                        rig.F(-r * 0.20f, sy - rig.TorsoLength * 0.12f),
                        packStrapAnchor,
                    }, false, SortBack, tone: Accent));
                    break;
                }

                case BackPoncho:
                    // 짧은 망토와 <b>같은 도형·다른 비율</b>. 짧고 앞쪽까지 덮는 것이 정체다.
                    sink.Add(new Shape("CapeOutline",
                        CapeOutline(rig, PonchoLengthRatio, PonchoSpreadRatio, PonchoHemWaveRatio,
                            PonchoFrontSpreadRatio),
                        true, SortBack, swayStart: 2, swayCount: 5, filled: true));
                    sink.Add(new Shape("CapeFold",
                        CapeFold(rig, PonchoLengthRatio, PonchoSpreadRatio, 0.35f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    sink.Add(new Shape("CapeFold2",
                        CapeFold(rig, PonchoLengthRatio, PonchoSpreadRatio, 0.72f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    break;

                case BackFairyWings:
                {
                    // 날개와 같은 구성의 작고 둥근 변주. 천이 아니므로 <b>흔들 점을 선언하지 않는다</b>.
                    float sy = rig.ShoulderY;
                    sink.Add(new Shape("WingSpine", new[]
                    {
                        rig.F(0f, sy + r * WingRootRiseRatio),
                        rig.F(0f, sy - rig.TorsoLength * FairyWingSpineDropScale),
                    }, false, SortBack, tone: Accent));

                    sink.Add(new Shape("WingFeatherA", FairyWingBlade(rig, -1f), true, SortBack, filled: true));
                    sink.Add(new Shape("WingFeatherB", FairyWingBlade(rig, +1f), true, SortBack, filled: true));
                    break;
                }
            }
        }

        // ★ 2026-08-30 FACE(표정) 카테고리 삭제 — 사용자 결정("표정관련은 전부삭제 어차피 구별이
        //   안됨"). 눈/입 도형(Smile/Lid)과 AppendFace가 함께 사라졌다. 눈동자 점 2개는 원래부터
        //   States/EyeController.cs의 단독 소유라 이 삭제와 무관하게 그대로 커서를 따라간다.

        // ==================== HAIR (머리) ====================

        /// <summary>
        /// 머리카락 실루엣 — <b>바깥 윤곽 + 이마선</b>으로 닫히는 채움 도형.
        /// <para>바깥 윤곽은 진행 방향(<see cref="HairSpanStartDegrees"/>)에서 뒤통수까지 도는 극좌표
        /// 곡선이고, 되돌아오는 안쪽 경계는 얼굴을 가로지르는 <b>포물선 이마선</b>이다
        /// (동심 원호로 막으면 머리카락이 아니라 헬멧 테가 된다 — 위 재설계 문단).</para>
        /// </summary>
        /// <param name="baseRadiusRatio">바깥 윤곽의 기준 반경(R 배수).</param>
        /// <param name="waveAmplitudeRatio">웨이브 진폭(R 배수). 0이면 매끈한 곡선.</param>
        /// <param name="waveCount">스팬 전체에 들어가는 웨이브 주기 수.</param>
        /// <param name="frontLiftRatio">앞쪽으로 갈수록 더해지는 반경(앞머리를 내민다).</param>
        internal static Vector3[] HairSilhouette(in Rig rig, float baseRadiusRatio, float waveAmplitudeRatio,
            float waveCount, float frontLiftRatio, int outerSegments)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            int outer = Mathf.Max(2, outerSegments);
            var pts = new Vector3[outer + 1 + HairlineSegments + 1];

            for (int i = 0; i <= outer; i++)
            {
                float t = i / (float)outer;
                float rad = Mathf.Lerp(HairSpanStartDegrees, HairSpanEndDegrees, t) * Mathf.Deg2Rad;
                float radius = baseRadiusRatio
                    + waveAmplitudeRatio * Mathf.Sin(t * waveCount * Mathf.PI * 2f)
                    + frontLiftRatio * (1f - t);
                pts[i] = rig.F(Mathf.Cos(rad) * radius * r, hc + Mathf.Sin(rad) * radius * r);
            }

            // 이마선은 <b>뒤 -> 앞</b>으로 되돌아온다(바깥 윤곽이 앞 -> 뒤였으므로 고리가 닫힌다).
            for (int i = 0; i <= HairlineSegments; i++)
            {
                float u = -1f + 2f * i / HairlineSegments;   // -1(뒤) -> +1(앞)
                float x = u * HairlineHalfWidthRatio;
                float y = HairlineEdgeRatio + (HairlineCrestRatio - HairlineEdgeRatio) * (1f - u * u);
                pts[outer + 1 + i] = rig.F(x * r, hc + y * r);
            }
            return pts;
        }

        /// <summary>
        /// 바가지머리 전용 실루엣 — <b>돔 + 수평으로 자른 밑선 셋</b>으로 닫히는 채움 도형.
        /// <para>형제들의 <see cref="HairSilhouette"/>(돔 + 포물선 이마선)와 <b>도형의 종류 자체가</b>
        /// 다르다. 반경만 키운 형제 복사본은 배율 0.75에서 단정한머리와 0.58획밖에 차이가 나지 않았고,
        /// 그 사실이 이 전용 도형이 존재하는 이유다(위 재설계 문단).</para>
        /// <para>점 순서: 앞쪽 옆머리 끝 → 돔 → 뒤쪽 옆머리 끝 → 뒤 밑변 → 뒤 옆머리 안쪽 변 →
        /// <b>앞머리 선</b> → 앞 옆머리 안쪽 변 → (닫히며) 앞 밑변.</para>
        /// </summary>
        internal static Vector3[] BowlSilhouette(in Rig rig)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;

            // 돔은 <b>옆머리 밑변과 만나는 각도</b>에서 시작/끝난다. 각도를 손으로 적어 두면 반경이나
            // 자른 높이를 바꿀 때 그 자리에 틈이 생기고, 틈은 획에 먹혀 이 빠진 실루엣이 된다.
            float startDeg = Mathf.Asin(Mathf.Clamp(BowlCutLineRatio / BowlCapRadiusRatio, -1f, 1f))
                * Mathf.Rad2Deg;
            float endDeg = 180f - startDeg;

            Vector3[] fringe = BowlFringeLine(rig);
            var pts = new Vector3[(HairCapSegments + 1) + 2 + fringe.Length];
            int w = 0;

            for (int i = 0; i <= HairCapSegments; i++)
            {
                float rad = Mathf.Lerp(startDeg, endDeg, i / (float)HairCapSegments) * Mathf.Deg2Rad;
                pts[w++] = rig.F(Mathf.Cos(rad) * BowlCapRadiusRatio * r,
                    hc + Mathf.Sin(rad) * BowlCapRadiusRatio * r);
            }

            pts[w++] = rig.F(-BowlSideHalfWidthRatio * r, hc + BowlCutLineRatio * r);
            for (int i = 0; i < fringe.Length; i++) pts[w++] = fringe[i];   // 뒤 -> 앞
            pts[w] = rig.F(BowlSideHalfWidthRatio * r, hc + BowlCutLineRatio * r);
            return pts;
        }

        /// <summary>이마를 가로지르는 앞머리 선. <see cref="BowlSilhouette"/>의 안쪽 경계와
        /// <b>같은 점</b>을 쓴다 — 두 벌로 적어 두면 한쪽만 고쳐 선이 어긋난다.</summary>
        internal static Vector3[] BowlFringeLine(in Rig rig)
        {
            float r = rig.HeadRadius;
            float y = rig.HeadCenterY + BowlFringeLineRatio * r;
            var pts = new Vector3[BowlFringeSegments + 1];
            for (int i = 0; i <= BowlFringeSegments; i++)
            {
                float x = Mathf.Lerp(-BowlSideHalfWidthRatio, BowlSideHalfWidthRatio,
                    i / (float)BowlFringeSegments);
                pts[i] = rig.F(x * r, y);
            }
            return pts;
        }

        /// <summary>극좌표 두 점을 잇는 <b>일정한 폭의 띠</b>(가르마 가닥). 폭이 획보다 넓어야
        /// 가닥으로 읽힌다 — 옛 가르마는 폭이 없는 선 1개라 "머리에 그은 금"이었다.</summary>
        private static Vector3[] HairStrand(in Rig rig, float fromDegrees, float fromRadiusRatio,
            float toDegrees, float toRadiusRatio, float halfWidthRatio)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            float a0 = fromDegrees * Mathf.Deg2Rad, a1 = toDegrees * Mathf.Deg2Rad;
            var p0 = new Vector2(Mathf.Cos(a0) * fromRadiusRatio, Mathf.Sin(a0) * fromRadiusRatio);
            var p1 = new Vector2(Mathf.Cos(a1) * toRadiusRatio, Mathf.Sin(a1) * toRadiusRatio);
            Vector2 d = p1 - p0;
            float len = d.magnitude;
            if (len <= 0.0001f) return System.Array.Empty<Vector3>();
            var n = new Vector2(-d.y / len * halfWidthRatio, d.x / len * halfWidthRatio);
            return new[]
            {
                rig.F((p0.x + n.x) * r, hc + (p0.y + n.y) * r),
                rig.F((p1.x + n.x) * r, hc + (p1.y + n.y) * r),
                rig.F((p1.x - n.x) * r, hc + (p1.y - n.y) * r),
                rig.F((p0.x - n.x) * r, hc + (p0.y - n.y) * r),
            };
        }

        /// <summary>머리 중심을 도는 <b>고리 조각</b>(안쪽 반경 ~ 바깥 반경). 민머리의 남은 테두리.</summary>
        private static Vector3[] HeadRimBand(in Rig rig, float fromDegrees, float toDegrees,
            float innerRatio, float outerRatio, int segments)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            int n = Mathf.Max(2, segments);
            var pts = new Vector3[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                float rad = Mathf.Lerp(fromDegrees, toDegrees, i / (float)n) * Mathf.Deg2Rad;
                pts[i] = rig.F(Mathf.Cos(rad) * outerRatio * r, hc + Mathf.Sin(rad) * outerRatio * r);
            }
            for (int i = 0; i <= n; i++)
            {
                float rad = Mathf.Lerp(toDegrees, fromDegrees, i / (float)n) * Mathf.Deg2Rad;
                pts[n + 1 + i] = rig.F(Mathf.Cos(rad) * innerRatio * r, hc + Mathf.Sin(rad) * innerRatio * r);
            }
            return pts;
        }

        /// <summary>극좌표 한 점 -> 로컬 좌표(머리 중심 기준).</summary>
        private static Vector3 HeadPolar(in Rig rig, float degrees, float radiusRatio)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return rig.F(Mathf.Cos(rad) * radiusRatio * rig.HeadRadius,
                rig.HeadCenterY + Mathf.Sin(rad) * radiusRatio * rig.HeadRadius);
        }

        private static void AppendHair(List<Shape> sink, int item, in Rig rig, float hatCoverLocalY, float strokeHalfWidth)
        {
            _hairScratch.Clear();
            switch (item)
            {
                case HairCowlick:
                    _hairScratch.Add(new Shape("HairCap",
                        HairSilhouette(rig, CowlickCapRadiusRatio, 0f, 0f, CowlickFrontLiftRatio, HairCapSegments),
                        true, SortHair, filled: true));
                    // 식별 특징 — 뒤로 솟은 삐침 하나(채운 삼각형). 최소 높이 0.58R = 1.7W라 획에 안 먹힌다.
                    _hairScratch.Add(new Shape("HairTuft", new[]
                    {
                        HeadPolar(rig, 98f, CowlickCapRadiusRatio),
                        HeadPolar(rig, CowlickTuftTipDegrees, CowlickTuftTipRadiusRatio),
                        HeadPolar(rig, 140f, CowlickCapRadiusRatio - 0.04f),
                    }, true, SortHair, tone: Accent, filled: true));
                    break;

                case HairNeat:
                    _hairScratch.Add(new Shape("HairCap",
                        HairSilhouette(rig, NeatCapRadiusRatio, 0f, 0f, NeatFrontLiftRatio, HairCapSegments),
                        true, SortHair, filled: true));
                    // 식별 특징 — 정수리에서 이마선으로 내려오는 가르마 <b>가닥</b>(폭 0.38R = 1.11W).
                    _hairScratch.Add(new Shape("HairPart",
                        HairStrand(rig, 86f, 1.18f, 34f, 0.66f, NeatPartHalfWidthRatio),
                        true, SortHair, tone: Accent, filled: true));
                    break;

                case HairCurly:
                    _hairScratch.Add(new Shape("HairCap",
                        HairSilhouette(rig, CurlBaseRadiusRatio, CurlAmplitudeRatio, CurlWaveCount, 0f,
                            HairCurlSegments),
                        true, SortHair, filled: true));
                    // 식별 특징 — 얼굴 앞으로 늘어진 컬 한 가닥.
                    _hairScratch.Add(new Shape("HairCoil", new[]
                    {
                        HeadPolar(rig, 6f, 1.36f),
                        HeadPolar(rig, -18f, 1.48f),
                        HeadPolar(rig, -40f, 1.20f),
                        HeadPolar(rig, -24f, 0.80f),
                        HeadPolar(rig, 0f, 0.96f),
                    }, true, SortHair, tone: Accent, filled: true));
                    break;

                case HairBald:
                    // 실루엣이 <b>없는</b> 것이 이 아이템의 정체다. 그래도 아무것도 안 그리지는 않는다 —
                    // 착용했는데 화면이 그대로면 그건 착용이 아니다(33-4 #4). 관자놀이/뒤통수에 남은 테 2조각.
                    // ★ 세 번째 조각(정수리 광택)은 <b>일부러 넣지 않았다</b>: 두 테 사이의 맨 두피는
                    //   어느 자리에 놓아도 테와의 간격이 1.5W(0.52R)를 넘지 못한다(오프라인 검산).
                    //   37-6 규칙 5 — "예산을 못 지키는 [선택] 디테일은 넣지 않는다".
                    _hairScratch.Add(new Shape("HairRimBack",
                        HeadRimBand(rig, BaldRimBackFromDegrees, BaldRimBackToDegrees,
                            BaldRimInnerRadiusRatio, BaldRimOuterRadiusRatio, 7),
                        true, SortHair, filled: true));
                    _hairScratch.Add(new Shape("HairRimFront",
                        HeadRimBand(rig, BaldRimFrontFromDegrees, BaldRimFrontToDegrees,
                            BaldRimInnerRadiusRatio, BaldRimOuterRadiusRatio, 4),
                        true, SortHair, tone: Accent, filled: true));
                    break;

                case HairBowl:
                    _hairScratch.Add(new Shape("HairCap", BowlSilhouette(rig), true, SortHair, filled: true));
                    // 식별 특징 — 자른 앞머리 선. 실루엣의 안쪽 경계와 <b>정확히 겹친다(간격 0)</b>:
                    // 규칙 4가 "최악"이라고 못박은 것은 0 &lt; 간격 &lt; 1획이지 겹침이 아니다. 겹치면
                    // 화면에서 선 하나(보조색)로 읽히고, 조금 어긋나면 "선을 두 번 그린 실수"가 된다.
                    // 새 선을 하나도 만들지 않으므로 규칙 1의 간격 예산도 소비하지 않는다.
                    _hairScratch.Add(new Shape("HairFringe", BowlFringeLine(rig), false, SortHair,
                        tone: Accent));
                    break;

                case HairPonytail:
                    _hairScratch.Add(new Shape("HairCap",
                        HairSilhouette(rig, PonytailCapRadiusRatio, 0f, 0f, PonytailFrontLiftRatio,
                            HairCapSegments),
                        true, SortHair, filled: true));
                    // 뒤통수에서 아래로 떨어지는 묶음. 가장 짧은 변이 0.52R(= 1.5획)이라 획에 먹히지 않는다.
                    _hairScratch.Add(new Shape("HairTail", new[]
                    {
                        HeadPolar(rig, 152f, 1.10f),
                        HeadPolar(rig, 178f, 1.55f),
                        HeadPolar(rig, 205f, 1.60f),
                        HeadPolar(rig, 215f, 1.05f),
                        HeadPolar(rig, 185f, 0.95f),
                    }, true, SortHair, tone: Accent, filled: true));
                    break;
            }

            for (int i = 0; i < _hairScratch.Count; i++)
            {
                AppendClippedBelowCover(sink, _hairScratch[i], hatCoverLocalY, strokeHalfWidth);
            }
        }

        /// <summary>도형을 굽는 동안에만 쓰는 임시 목록. 재구성은 <b>착용/방향/색이 바뀐 프레임에만</b>
        /// 도는 경로라 정적 재사용으로 충분하다(매 프레임 할당 금지 규약).</summary>
        private static readonly List<Shape> _hairScratch = new List<Shape>(4);

        /// <summary>모자 밑으로 잘라 낸 조각을 담는 임시 버퍼(같은 이유로 정적 재사용).</summary>
        private static readonly List<Vector3> _clipScratch = new List<Vector3>(48);

        // ============================================================================
        // ★ 커버 규칙: "선 통째로 생략" -> "커버선에서 자르기(clip)" — 2026-09-01
        // ============================================================================
        // 옛 규칙(<c>IsCoveredByHat</c>)은 <b>커버선 위로 올라가는 점이 하나라도 있으면 그 선을
        // 통째로 버렸다</b>. 머리카락이 선 1개짜리 호였을 때는 그것이 "모자 속에 감춘다"와 같았다.
        //
        // 그런데 P0에서 머리카락이 <b>닫힌 채움 도형</b>이 되면서 그 규칙은 정확히 반대로 작동한다:
        // 실루엣 하나가 통째로 버려지므로 <b>모자를 쓰면 머리카락이 전부 사라진다</b>
        // (ux-designer가 37-7 #1에서 리더 보고 대상으로 지목한 그 자리다).
        //
        // 그래서 <b>도형을 커버선에서 자른다</b>. 잘린 자리의 뭉툭한 캡은 옛 주석이 걱정한 그대로지만,
        // 자르는 높이가 <b>모자 자신의 획 중심선</b>이라 그 캡은 모자 획(같은 두께) 아래에 들어간다 —
        // 즉 화면에서는 "모자 밑으로 들어간 머리카락"으로 보이고, 옆으로 삐져나온 부분만 남는다.
        // 실제로 그게 맞는 그림이다(모자를 써도 귀 옆 머리는 보인다).
        //
        // <paramref name="strokeHalfWidth"/>는 이제 <b>버릴지 말지</b>를 정한다: 남은 조각의 잉크
        // 사각형이 획 하나(2 × 반폭)보다도 작으면 그리지 않는다. 그런 조각은 커버선 위에 얹힌
        // <b>점 하나</b>로만 보이기 때문이다(옛 주석의 "뭉툭하게 잘린 선"이 실제로 문제가 되는 유일한 경우).

        /// <summary>
        /// <paramref name="shape"/>에서 <paramref name="coverLocalY"/> <b>위쪽</b>을 잘라 내고
        /// 남는 것만 <paramref name="sink"/>에 넣는다. 커버선이 +∞면 원본을 그대로 넣는다.
        /// </summary>
        internal static void AppendClippedBelowCover(List<Shape> sink, in Shape shape,
            float coverLocalY, float strokeHalfWidth)
        {
            if (sink == null || shape.Points == null || shape.Points.Length == 0) return;
            if (float.IsPositiveInfinity(coverLocalY)) { sink.Add(shape); return; }

            Vector3[] pts = shape.Points;
            bool anyAbove = false, anyBelow = false;
            for (int i = 0; i < pts.Length; i++)
            {
                if (pts[i].y > coverLocalY) anyAbove = true; else anyBelow = true;
            }
            if (!anyAbove) { sink.Add(shape); return; }
            if (!anyBelow && !shape.Loop) return;

            if (shape.Loop) ClipLoop(sink, shape, coverLocalY, strokeHalfWidth);
            else ClipPolyline(sink, shape, coverLocalY, strokeHalfWidth);
        }

        /// <summary>닫힌 도형 — 반평면(y ≤ cover) Sutherland–Hodgman 절단. 결과는 여전히 닫힌 하나다.</summary>
        private static void ClipLoop(List<Shape> sink, in Shape shape, float coverY, float strokeHalfWidth)
        {
            Vector3[] pts = shape.Points;
            _clipScratch.Clear();
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 cur = pts[i];
                Vector3 next = pts[(i + 1) % pts.Length];
                bool curIn = cur.y <= coverY;
                bool nextIn = next.y <= coverY;

                if (curIn) _clipScratch.Add(cur);
                if (curIn != nextIn) _clipScratch.Add(CrossAt(cur, next, coverY));
            }
            EmitIfVisible(sink, shape, _clipScratch, 0, _clipScratch.Count, shape.Loop, strokeHalfWidth);
        }

        /// <summary>열린 선 — 커버선 아래에 남는 <b>연속 구간</b>마다 조각을 하나씩 만든다.</summary>
        private static void ClipPolyline(List<Shape> sink, in Shape shape, float coverY, float strokeHalfWidth)
        {
            Vector3[] pts = shape.Points;
            _clipScratch.Clear();
            int runStart = 0;

            for (int i = 0; i < pts.Length; i++)
            {
                bool inside = pts[i].y <= coverY;
                if (inside)
                {
                    if (_clipScratch.Count == runStart && i > 0 && pts[i - 1].y > coverY)
                    {
                        _clipScratch.Add(CrossAt(pts[i - 1], pts[i], coverY));
                    }
                    _clipScratch.Add(pts[i]);
                    continue;
                }

                if (_clipScratch.Count > runStart)
                {
                    _clipScratch.Add(CrossAt(pts[i - 1], pts[i], coverY));
                    EmitIfVisible(sink, shape, _clipScratch, runStart, _clipScratch.Count - runStart,
                        false, strokeHalfWidth);
                    runStart = _clipScratch.Count;
                }
            }
            if (_clipScratch.Count > runStart)
            {
                EmitIfVisible(sink, shape, _clipScratch, runStart, _clipScratch.Count - runStart,
                    false, strokeHalfWidth);
            }
        }

        /// <summary>선분이 커버선을 지나는 점.</summary>
        private static Vector3 CrossAt(Vector3 a, Vector3 b, float y)
        {
            float dy = b.y - a.y;
            float t = Mathf.Abs(dy) < 1e-9f ? 0f : Mathf.Clamp01((y - a.y) / dy);
            return new Vector3(a.x + (b.x - a.x) * t, y, 0f);
        }

        /// <summary>남은 조각이 <b>획 하나보다 크면</b> 넣는다. 흔들 구간은 잘린 도형에서 인덱스가
        /// 어긋나므로 넘기지 않는다(HAIR는 원래 흔들지 않는다).</summary>
        private static void EmitIfVisible(List<Shape> sink, in Shape shape, List<Vector3> buffer,
            int start, int count, bool loop, float strokeHalfWidth)
        {
            if (count < 2) return;
            // 닫힌 도형은 점 3개부터가 면이다 — 2점짜리 고리는 Triangulate가 빈 배열을 돌려주고
            // 윤곽선만 왕복해서 그려지므로 커버선 위에 얹힌 <b>선 한 줄</b>로 남는다.
            if (loop && count < 3) return;

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = buffer[start + i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            float stroke = Mathf.Max(0f, strokeHalfWidth * 2f);
            if (Mathf.Max(maxX - minX, maxY - minY) <= stroke) return;

            var kept = new Vector3[count];
            for (int i = 0; i < count; i++) kept[i] = buffer[start + i];
            sink.Add(new Shape(shape.Name, kept, loop, shape.SortingOrder,
                tone: shape.Tone, filled: shape.Filled));
        }

        // ==================== 망토 매개변수화(짧은/긴 망토가 같은 코드를 쓴다) ====================

        internal static float CapeHemLocalY(in Rig rig, float lengthRatio)
            => CapeCollarLocalY(rig) - rig.TorsoLength * lengthRatio;

        /// <summary>
        /// ★ 2026-08-30 재설계 — <b>옷깃에서 밑단으로 벌어지는 사다리꼴</b>(위 CapeSpreadRatio 문서).
        /// 점 순서는 옷깃 앞 -> 옷깃 뒤 -> 밑단 뒤끝 -> 밑단(물결 3점) -> 밑단 앞끝이고, 밑단 5점이
        /// 통째로 흔들린다(HemSway). 옛 도형은 밑단 뒤쪽만 벌어져 <b>깃발</b>이었다.
        /// </summary>
        /// <param name="hemNotchRatio">0이면 물결치는 밑단(짧은 망토·판초), 0보다 크면 그 깊이만큼
        /// 갈라진 <b>제비꼬리</b> 밑단(긴 망토). 점 개수는 두 경우가 <b>같다</b> — 밑단 흔들 구간
        /// (인덱스 2~6)을 도형마다 다르게 선언하지 않기 위해서다.</param>
        internal static Vector3[] CapeOutline(in Rig rig, float lengthRatio, float spreadRatio,
            float hemWaveRatio, float frontSpreadRatio, float hemNotchRatio = 0f)
        {
            float r = rig.HeadRadius;
            float collarY = CapeCollarLocalY(rig);
            float hemY = CapeHemLocalY(rig, lengthRatio);
            float drop = collarY - hemY;
            float front = r * CapeCollarFrontRatio;
            float back = r * CapeCollarBackRatio;
            float trail = r * spreadRatio;                 // 밑단 뒤끝
            float lead = r * frontSpreadRatio;             // 밑단 앞끝
            float wave = r * hemWaveRatio;

            if (hemNotchRatio > 0f)
            {
                float apex = LongCapeHemNotchApexRatio;
                return new[]
                {
                    rig.F(front, collarY),
                    rig.F(-back, collarY + r * 0.04f),
                    rig.F(-trail, hemY + drop * 0.14f),               // 2 뒤쪽 꼬리 끝
                    rig.F(-trail * (apex + 0.24f), hemY - wave * 0.30f),
                    rig.F(-trail * apex, hemY + drop * hemNotchRatio),// 4 갈라진 골
                    rig.F(-trail * (apex - 0.34f), hemY - wave * 0.30f),
                    rig.F(lead, hemY + drop * 0.10f),                 // 6 앞쪽 꼬리 끝
                };
            }

            return new[]
            {
                rig.F(front, collarY),                     // 0 옷깃 앞
                rig.F(-back, collarY + r * 0.04f),         // 1 옷깃 뒤
                rig.F(-trail, hemY + drop * 0.14f),        // 2 밑단 뒤끝(살짝 들린다)
                rig.F(-trail * 0.62f, hemY - wave * 0.35f),// 3 ┐
                rig.F(-trail * 0.14f, hemY + wave),        // 4 ├ 물결치는 밑단
                rig.F(lead * 0.55f, hemY - wave * 0.30f),  // 5 ┘
                rig.F(lead, hemY + drop * 0.10f),          // 6 밑단 앞끝
            };
        }

        /// <summary>짧은 망토 기본값(옛 호출부 호환).</summary>
        internal static Vector3[] CapeOutline(in Rig rig)
            => CapeOutline(rig, CapeLengthRatio, CapeSpreadRatio, CapeHemWaveRatio, CapeFrontSpreadRatio);

        /// <summary>
        /// 주름 한 줄. <paramref name="startBackRatio"/>가 클수록 옷깃 <b>뒤쪽</b>에서 시작한다.
        /// <para>끝점을 <c>0.52 × startBackRatio / 0.35</c>로 비례시키면 안 된다 — 두 번째 주름(0.75)에서
        /// 끝점이 <c>trail × 1.11</c>이 되어 <b>망토 외곽선(최대 trail) 바깥으로 삐져나간다</b>.
        /// 시작점만 뒤로 밀고 끝점은 완만하게 따라가게 한다(첫 주름 0.35에서는 옛 값 0.52 그대로라
        /// 짧은 망토의 그림이 한 픽셀도 달라지지 않는다).</para>
        /// </summary>
        /// <param name="endRatioOverride">0보다 크면 유도값 대신 이 값을 쓴다. 제비꼬리 밑단
        /// (긴 망토)은 갈라진 골이 유도값 자리에 있어, 주름 끝이 천 바깥에 떨어지기 때문이다.</param>
        internal static Vector3[] CapeFold(in Rig rig, float lengthRatio, float spreadRatio,
            float startBackRatio, float endRatioOverride = 0f)
        {
            float r = rig.HeadRadius;
            float collarY = CapeCollarLocalY(rig);
            float hemY = CapeHemLocalY(rig, lengthRatio);
            float back = r * CapeCollarBackRatio;
            float trail = r * spreadRatio;
            // 주름은 <b>천이 벌어지는 방향</b>을 따라간다 — 옷깃의 좁은 자리에서 밑단의 넓은 자리로.
            // 끝점 비율을 시작 비율에 비례시키면 안 되는 이유는 아래 문단 참고(외곽선 밖으로 나간다).
            float endRatio = endRatioOverride > 0f
                ? endRatioOverride
                : Mathf.Min(0.92f, 0.42f + (startBackRatio - 0.35f) * 0.60f);
            return new[]
            {
                rig.F(-back * startBackRatio, collarY - r * 0.10f),
                rig.F(-trail * endRatio, hemY + (collarY - hemY) * 0.20f),
            };
        }

        /// <summary>짧은 망토 주름 기본값(옛 호출부 호환).</summary>
        internal static Vector3[] CapeFold(in Rig rig)
            => CapeFold(rig, CapeLengthRatio, CapeSpreadRatio, 0.35f);

        // ============================================================================
        // ★ 밑단 기류 펄럭임 — 2026-08-31 사용자 신고
        //   "떨어지거나 할때 망토도 펄럭여야하는데 고정되어있음"
        // ============================================================================
        //
        // 원인은 추측이 아니라 코드로 확정됐고, <b>두 겹</b>이었다.
        //
        //  ① 흔들림의 유일한 구동원이 Interaction/CharacterAccessoryRenderer.ResolveWalkSpeed01()
        //     = |Body.linearVelocity.x| / 보행속도 하나뿐이었다. 수직 낙하는 x 속도가 0이므로
        //     진폭이 <b>정확히 0</b>이 되고, 그 함수는 0일 때 SetPositions 호출 자체를 건너뛴다.
        //     즉 낙하 중에는 밑단이 한 점도 움직이지 않는 것이 <b>설계상 보장</b>돼 있었다.
        //  ② 밑단 점을 실제로 옮기는 곳이 LineRenderer <b>윤곽선뿐</b>이고, 화면에서 "천"으로 보이는
        //     <b>채움 면</b>(BuildFillMesh로 한 번 구운 정적 메시)은 재구성 전까지 한 정점도 갱신되지
        //     않았다. 그래서 걷는 중에도 천은 고정이고 테두리만 미끄러지고 있었다
        //     (그 공백은 Tests/PlayMode/AccessoryFillRenderingTests가 이미 "m4 공백"으로 계측해
        //      상한만 걸어 둔 상태였다 — 원인이 아니라 증상만 잠근 것이었다).
        //
        // 아래는 ①의 <b>모양 쪽 절반</b>이다: "기류 세기·방향"을 받아 밑단 점 하나의 오프셋을 만드는
        // 순수 함수. 상태를 읽지 않고 시간도 스스로 세지 않는다 — 그래야 렌더러 없이 EditMode에서
        // 경계값(기류 0 -> 오프셋 0)을 단언할 수 있고, 정의가 렌더러와 두 벌이 되지 않는다.
        // "언제 얼마나 부는가"는 상태를 아는 쪽(CharacterAccessoryRenderer.ResolveAirFlow01)이 정한다.
        //
        // 물리 시뮬레이션을 넣지 않은 이유: 이 앱의 망토는 옷깃에서 <b>매달린 사다리꼴 천 한 장</b>이라,
        // 실제로 눈에 들어오는 것은 (a) 기류에 밀려 통째로 젖혀지는 각도와 (b) 자락이 떠는 물결
        // 두 가지뿐이다. 절점-스프링을 넣어도 24시간 상주 앱의 비용만 늘고 그림은 같다.

        /// <summary>기류에 밀려 <b>바람이 가는 쪽</b>으로 젖혀지는 최대 거리(머리 반경 R 배수).
        /// 밑단 점 전체에 <b>같은 값</b>으로 들어간다 — 망토는 옷깃에 매달린 한 장이라, 밑단이
        /// 통째로 젖혀지는 것이 "옷깃을 축으로 회전"으로 읽힌다(점마다 다르게 밀면 천이 찢어져 보인다).</summary>
        internal const float HemAirPushRatio = 0.85f;

        /// <summary>기류에 <b>수직</b>으로 떠는 물결의 최대 진폭(R 배수). 젖힘 방향과 같은 축에 실으면
        /// 그냥 "더 젖혔다 덜 젖혔다"가 되어 펄럭임으로 읽히지 않는다.</summary>
        internal const float HemAirRippleRatio = 0.34f;

        /// <summary>물결의 초당 주기 수(Hz). 걷기 스웨이(0.62초 주기 = 1.6Hz)보다 확실히 빠르다 —
        /// 낙하는 보행보다 훨씬 센 기류이고, 같은 속도로 흔들리면 "걷는 망토"와 구분되지 않는다.</summary>
        internal const float HemAirRippleHz = 4.5f;

        /// <summary>점마다 위상을 어긋내는 양(라디안). 천이 접히며 지나가는 것처럼 보이게 한다.</summary>
        internal const float HemAirPointPhaseStep = 1.15f;

        /// <summary>
        /// 밑단 점 하나가 기류 때문에 받는 오프셋(로컬 좌표계, 월드 유닛).
        ///
        /// <para><paramref name="windDirLocal"/>은 <b>바람이 불어 가는 방향</b>(= 진행 반대 방향)을
        /// 이 도형과 같은 좌표계로 내린 것이다. 정규화는 이 함수가 한다 — 호출부가 정규화를 빠뜨려도
        /// 진폭이 조용히 배가 되지 않게 하기 위해서다.</para>
        ///
        /// <para><b>경계</b>: <paramref name="air01"/>이 0이면 <see cref="Vector2.zero"/>다. 즉
        /// "낙하 속도가 0에 가까우면 펄럭임도 0"이 if 분기가 아니라 <b>식 자체</b>로 성립한다
        /// (회귀 테스트 Tests/EditMode/CapeAirFlutterTests가 이 성질을 직접 잠근다).</para>
        /// </summary>
        /// <param name="headRadius">머리 반경 R. 모든 진폭이 이 값의 배수라 배율을 자동으로 따라간다.</param>
        /// <param name="windDirLocal">바람이 가는 방향(정규화 불필요). 길이가 0이면 오프셋도 0.</param>
        /// <param name="air01">기류 세기 0~1. 0 = 정지, 1 = 최대(낙하 자세가 최대가 되는 속도와 같은 기준).</param>
        /// <param name="timeSeconds">물결 위상을 만드는 시간. 렌더러는 Time.time을 넣는다.</param>
        /// <param name="pointIndex">도형 안에서의 점 번호. 점마다 위상을 어긋내는 데만 쓴다.</param>
        internal static Vector2 HemAirOffset(float headRadius, Vector2 windDirLocal, float air01,
            float timeSeconds, int pointIndex)
        {
            float a = Mathf.Clamp01(air01);
            if (a <= 0f || headRadius <= 0f) return Vector2.zero;

            float len = windDirLocal.magnitude;
            if (len <= 0.0001f) return Vector2.zero;
            Vector2 w = windDirLocal / len;

            // 기류에 수직인 축(왼손/오른손 구분은 무의미하다 — 물결은 사인파라 부호가 뒤집혀도 같은 그림).
            var perp = new Vector2(-w.y, w.x);

            float phase = timeSeconds * Mathf.PI * 2f * HemAirRippleHz + pointIndex * HemAirPointPhaseStep;
            float ripple = Mathf.Sin(phase) * headRadius * HemAirRippleRatio * a;

            return w * (headRadius * HemAirPushRatio * a) + perp * ripple;
        }

        // ==================== 채움 면(2026-08-30) ====================
        //
        // ★ 왜 메시인가. 이 프로젝트에는 채움 도형을 만드는 경로가 하나도 없었다(모든 그림이
        //   LineRenderer 선화다). 그래서 모자 관 안쪽으로 머리 링이 그대로 비쳤다 —
        //   사용자 신고 "모자가 투명해보임"의 정확한 원인이다.
        //   굵은 선으로 안을 메우는 꼼수는 도형마다 다른 값을 손으로 맞춰야 하고 모서리가 뭉개진다.
        //   가장 정직한 방법은 <b>같은 점으로 삼각형을 깔아 주는 것</b>이고, 그게 여기다.
        //
        // ★ 재질은 새로 만들지 않는다 — 캐릭터 선이 쓰는 Sprites-Default를 그대로 빌려 쓰고
        //   색은 <b>정점 색</b>으로 넣는다(그 셰이더가 정점 색을 곱한다). 머티리얼을 색마다 만들면
        //   24시간 상주 앱에서 색을 바꿀 때마다 누수 후보가 하나씩 늘어난다.

        /// <summary>
        /// 단순 다각형(자기교차 없음)을 삼각형으로 나눈다 — 귀 자르기(ear clipping).
        /// 무게중심 부채꼴을 쓰지 않는 이유: 모자 챙처럼 <b>오목한</b> 도형에서 삼각형이 윤곽선
        /// 바깥으로 삐져나온다(챙 밑에 없던 색 조각이 생긴다).
        /// </summary>
        internal static int[] Triangulate(Vector3[] points)
        {
            int n = points != null ? points.Length : 0;
            if (n < 3) return System.Array.Empty<int>();

            var index = new List<int>(n);
            // 시계/반시계를 통일해 둔다 — 부호가 뒤집히면 "귀"를 하나도 못 찾는다.
            bool ccw = SignedArea(points) > 0f;
            for (int i = 0; i < n; i++) index.Add(ccw ? i : n - 1 - i);

            var tris = new List<int>((n - 2) * 3);
            int guard = 0;
            while (index.Count > 3 && guard++ < n * n)
            {
                bool clipped = false;
                for (int i = 0; i < index.Count; i++)
                {
                    int i0 = index[(i + index.Count - 1) % index.Count];
                    int i1 = index[i];
                    int i2 = index[(i + 1) % index.Count];
                    if (!IsEar(points, index, i0, i1, i2)) continue;

                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    index.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;  // 자기교차 도형 — 남은 것은 부채꼴로 덮는다(아래).
            }
            for (int i = 1; i + 1 < index.Count; i++)
            {
                tris.Add(index[0]); tris.Add(index[i]); tris.Add(index[i + 1]);
            }
            return tris.ToArray();
        }

        private static float SignedArea(Vector3[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 c = p[i], d = p[(i + 1) % p.Length];
                a += c.x * d.y - d.x * c.y;
            }
            return a * 0.5f;
        }

        private static bool IsEar(Vector3[] p, List<int> index, int i0, int i1, int i2)
        {
            Vector3 a = p[i0], b = p[i1], c = p[i2];
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (cross <= 0f) return false;   // 볼록하지 않은 꼭짓점은 귀가 될 수 없다.

            for (int k = 0; k < index.Count; k++)
            {
                int j = index[k];
                if (j == i0 || j == i1 || j == i2) continue;
                if (InTriangle(p[j], a, b, c)) return false;
            }
            return true;
        }

        private static bool InTriangle(Vector3 pt, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = Side(pt, a, b), d2 = Side(pt, b, c), d3 = Side(pt, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Side(Vector3 p, Vector3 a, Vector3 b)
            => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        /// <summary>채움 면 하나. <b>부르는 쪽이 다 쓰고 나면 반드시 Destroy 해야 한다</b>(메시는
        /// GameObject를 지워도 함께 사라지지 않는다 — 24시간 상주 앱에서 그대로 누수가 된다).</summary>
        internal static Mesh BuildFillMesh(Vector3[] points, Color color)
        {
            int[] tris = Triangulate(points);
            if (tris.Length == 0) return null;

            var colors = new Color[points.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;

            var mesh = new Mesh { name = "AccessoryFill" };
            mesh.hideFlags = HideFlags.DontSave;
            mesh.vertices = points;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>채움 위에 얹는 윤곽선 색 — 같은 색을 그대로 쓰면 면과 선이 붙어 <b>실루엣만 남은
        /// 덩어리</b>가 된다(털모자의 띠와 관이 한 덩어리로 뭉치는 자리). 같은 색을 어둡게 한 값이라
        /// 팔레트를 늘리지 않으면서 경계를 만든다.</summary>
        internal static Color FillOutlineColor(Color fill)
            => new Color(fill.r * 0.62f, fill.g * 0.62f, fill.b * 0.62f, fill.a);

        // ==================== 공용 도형 유틸 ====================

        /// <summary>정n각형(닫힌 고리로 쓴다). 중심은 <b>진행 방향 기준</b> x.</summary>
        /// <param name="startDegrees">첫 점의 각도(+x = 0도, 반시계). 기본 0은 옛 호출부의 그림을
        /// 그대로 유지한다. <b>매다는 도형</b>은 90을 준다 — 그래야 가장 높은 꼭짓점이 정확히
        /// 원의 꼭대기에 놓여 부착점을 좌표로 단언할 수 있다(방울이 이 이유로 90을 쓴다).</param>
        private static Vector3[] Polygon(in Rig rig, float centerForwardX, float centerY, float radius,
            int segments, float startDegrees = 0f)
        {
            var pts = new Vector3[segments];
            float step = Mathf.PI * 2f / segments;
            float phase = startDegrees * Mathf.Deg2Rad;
            for (int i = 0; i < segments; i++)
            {
                float a = phase + step * i;
                pts[i] = rig.F(centerForwardX + Mathf.Cos(a) * radius, centerY + Mathf.Sin(a) * radius);
            }
            return pts;
        }

        /// <summary>원호(열린 선). 각도는 +x(진행 방향)를 0도로 보고 반시계 방향.</summary>
        private static Vector3[] Arc(in Rig rig, float centerForwardX, float centerY, float radius,
            float fromDegrees, float toDegrees, int points)
        {
            var pts = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float rad = Mathf.Lerp(fromDegrees, toDegrees, points > 1 ? i / (float)(points - 1) : 0f) * Mathf.Deg2Rad;
                pts[i] = rig.F(centerForwardX + Mathf.Cos(rad) * radius, centerY + Mathf.Sin(rad) * radius);
            }
            return pts;
        }

        /// <summary>8각 근사 상자 — 원보다 렌즈/매듭처럼 보이고 점이 적어 가볍다.</summary>
        private static Vector3[] RoundedBox(in Rig rig, float forwardCx, float cy, float halfW, float halfH)
        {
            return new[]
            {
                rig.F(forwardCx - halfW, cy - halfH * 0.45f),
                rig.F(forwardCx - halfW * 0.7f, cy + halfH),
                rig.F(forwardCx + halfW * 0.7f, cy + halfH),
                rig.F(forwardCx + halfW, cy + halfH * 0.35f),
                rig.F(forwardCx + halfW * 0.85f, cy - halfH * 0.75f),
                rig.F(forwardCx + halfW * 0.2f, cy - halfH),
                rig.F(forwardCx - halfW * 0.6f, cy - halfH),
            };
        }
    }
}
