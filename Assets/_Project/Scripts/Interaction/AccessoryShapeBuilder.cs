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

        // ============================================================================
        // ★ HEAD — 2026-09-01(2차) "얹지 말고 감싼다" 재설계
        //   (docs/EQUIPMENT_SHAPE_SPEC.md 5절 · 사용자 신고 "장비들 모양이 너무 조잡해")
        // ============================================================================
        // 옛 6종의 커버선은 전부 머리 중심 <b>위</b>였다 — 캡 +0.62 / 털모자 +0.42 / 중절모 +0.58 /
        // 베레모 +0.46 / 밀짚 +0.56 R. 즉 모자가 <b>머리 위쪽 1/3에만 얹혀</b> 있었고, 그래서
        // "머리에 씌운 것"이 아니라 "머리 위에 올려 둔 것"으로 보였다.
        //
        // 새 커버선은 전부 머리 중심 언저리(+0.08 ~ −0.06 R)다. 그리고 규칙 4를 <b>측정 가능한 형태</b>로
        // 못박는다: <c>|x| ≥ 0.85R 이면서 y ≤ 0.05R 인 잉크가 존재한다</c>(= 관자놀이를 지나 내려온다).
        // 왕관만 면제다 — 스스로 "얹는 물건"이라 선언하기 때문이고, 그 선언은 if 분기가 아니라
        // <see cref="HatCoverLocalY"/>가 돌려주는 +∞다.
        //
        // 챙은 <b>닫힌 띠</b>다(규칙 8). 뿌리가 1.34~1.51획 두껍고 끝이 점으로 수렴한다 —
        // 옛 챙은 뿌리가 1.10획이라 화면에서 그냥 선 하나였다.

        /// <summary>야구모자의 커버선(= 관과 챙이 만나는 선). <b>이 값이 곧 이 모자의
        /// <see cref="HatCoverLocalY"/></b>이고, 렌더러의 <c>HatBrimLocalY</c>가 그대로 노출한다.
        /// <para>옛 값 0.62R은 머리 위쪽 1/3 자리였다. 0.06R로 내리면서 관 옆벽이 −0.22R까지
        /// 내려와 머리를 감싼다.</para></summary>
        internal const float HatBrimLineRatio = 0.06f;

        /// <summary>관 꼭대기 = 커버선 + 이 값. 렌더러의 <c>HatTopLocalY</c>가 그대로 노출하고,
        /// <c>CharacterAccessoryScaleTests</c>가 "정수리(1.0R)보다 높다"를 잠근다.</summary>
        internal const float HatCrownHeightRatio = 1.18f;

        /// <summary>관 <b>옆벽</b>의 x. 감쌈(|x| ≥ 0.85R)을 만드는 자리라 0.85 아래로 내려가면 안 된다.</summary>
        internal const float HatCrownHalfWidthRatio = 0.94f;

        /// <summary>챙 끝이 진행 방향으로 뻗는 거리.</summary>
        internal const float HatBrimReachRatio = 1.92f;

        /// <summary>챙의 <b>뒤쪽 수렴점</b>이 커버선 아래로 내려가는 깊이 = 그 자리의 닫힘변 길이.
        /// <b>"챙의 두께"가 아니다</b> — 뒤는 점으로 수렴하고 부피는 앞(머리 원 <b>밖</b>)에서 만든다.
        /// <para>★ 2026-09-02 0.46R -&gt; 0.18R. 이력은 0.10 -&gt; 0.38 -&gt; 0.46 -&gt; <b>0.18</b>이고,
        /// 0.38·0.46은 "뿌리를 두껍게"라는 <b>틀린 전제</b>에서 나온 값이다: 그 두께가 전부 머리 원 위에
        /// 얹혀 사용자가 신고한 "ㅁ자 창"이 됐다(챙이 머리 원반에 얹은 색 0.643 R² = 72%).</para>
        /// <para>챙은 원반이라 <b>옆에서 보면 얼굴 앞 구간이 가장 얇고 앞뒤 끝이 두껍다</b>. 옛 좌표는 그
        /// 순서가 뒤집혀 있었다. 지금 닫힘변은 0.3847R = <b>1.12획</b>(규칙 1 하한 1.0)이고, 규칙 1-C가
        /// 요구하는 두꺼운 자리(ρ_max 0.2561R)는 x = +1.22R — 머리 밖이다.</para>
        /// <para><b>얇아졌다고 다시 키우지 마라.</b> 키우는 만큼 그대로 머리를 지운다(그것이 이 값의
        /// 0.38 -&gt; 0.46 회귀가 취한 형태였다). 검산: <c>python3 Tools/ShapeDump/prodverify.py</c>.</para></summary>
        internal const float HatBrimRootDropRatio = 0.18f;

        // ============================================================================
        // ★ EYES — 2026-09-01(3차) "가리개 옆에 눈" (docs/EQUIPMENT_SHAPE_SPEC.md 6절)
        // ============================================================================
        // 사용자 요구: "외눈안경처럼 <b>한쪽만</b> 가릴 때는 반대쪽 눈이 보여야 한다".
        //
        // ★ "렌즈 안으로 눈이 비치는" 그림은 이 배율에서 <b>기하학적으로 불가능하다</b>. 취향이 아니라 산술이다:
        //     눈이 보이려면        잉크 사각형 ≥ 1.5W       -> 눈 반폭 a ≥ 0.75W
        //     테와 눈이 안 붙으려면 간격 ≥ 1.5W             -> 렌즈 반경 ρ ≥ a + 1.5W = 2.25W = 0.774R
        //     두 렌즈가 안 붙으려면 중심 간격 2d ≥ 2ρ + 1.5W -> d ≥ 3.00W = 1.032R
        //                          바깥 끝 = d + ρ ≥ 5.25W = 1.805R  >  1.0R   ← <b>머리 밖</b>
        //   머리 지름이 5.82W뿐이라 "테 + 간격 + 눈 + 간격 + 테"를 한쪽 눈에조차 넣을 수 없다.
        //   같은 산술이 외알안경 자신의 알에도 적용된다 — 그래서 외알안경도 <b>자기가 가린 눈</b>은
        //   보여 주지 않고, <b>가리지 않은 반대쪽</b> 눈만 드러난다. 사용자의 요구와 정확히 일치하고,
        //   그 요구가 <b>왜 옳은지</b>까지 설명한다.
        //
        //   부수 효과: 2026-08-30 "눈 삭제"가 사후적으로 정당화된다. 눈동자 반경 0.136R은 <b>0.79획</b>,
        //   획 하나보다 작았다. 그래서 안 보였던 것이고, 되살리려면 눈이 지금의 2.5배가 되어야 한다.
        //   (그래서 BakeEyes/DrawEyes는 false 그대로다 — 아래 '드러난 눈'은 <b>액세서리 도형</b>이다.)
        //
        //   규칙 2-a — <b>눈은 가리개 안이 아니라 가리개 옆에만 그린다.</b>
        internal const float GlassesCenterRatio = 0.00f;

        /// <summary>안경류가 <b>진행 반대쪽</b>으로 뻗는 끝(귀 위). 렌더러의
        /// <c>GlassesTempleTipLocalX</c> 프로퍼티와 <c>CharacterAccessoryScaleTests</c>가 이 상수를
        /// 읽는다 — <b>지우면 안 된다</b>. 지금은 고글 스트랩이 이 x를 넘어 뻗는 유일한 도형이다.</summary>
        internal const float GlassesTempleReachRatio = 1.02f;

        // ---- 드러난 눈(외알안경·안대 전용). ★ EyeOffsetXInHeadRadii(0.3409)는 <b>"눈이 있던 자리"의
        //      정의처</b>로 그대로 남는다(가림 판정이 그 값을 쓴다). 하지만 <b>그려지는</b> 눈의 위치는
        //      규칙 1이 결정하므로 별도 상수가 필요하다 — 두 값을 하나로 합치면 가림 판정이 눈을 따라
        //      움직여 "가리개가 뒤 눈을 덮었다"는 엉뚱한 실패가 난다.

        /// <summary>드러난 눈의 중심 x. 유도: 2d ≥ 1.5W + ρ_visor(0.36R) + a(0.34R) = 1.216R -> d ≥ 0.608R.</summary>
        internal const float DrawnEyeOffsetRatio = 0.62f;

        /// <summary>반폭 -> 폭 0.68R = 1.98획.</summary>
        internal const float DrawnEyeHalfWidthRatio = 0.34f;

        /// <summary>반높이 -> 높이 0.48R = 1.40획.</summary>
        internal const float DrawnEyeHalfHeightRatio = 0.24f;

        // ---- 선글라스 — 어두운 렌즈 2장 + 코다리. 이름이 "가린다"고 말하므로 눈은 보이지 않는다.
        internal const float SunglassInnerRatio = 0.28f;      // 코다리가 걸리는 안쪽 변
        internal const float SunglassOuterRatio = 1.02f;      // 바깥 변
        internal const float SunglassBridgeRiseRatio = 0.46f;

        /// <summary>앞쪽 렌즈를 이만큼 키운다. 1.0이면 완전 대칭이라 방향이 사라지고, 크게 잡으면
        /// 다시 화살표가 된다(리더 육안 검증 V1). 쌍 대칭성 지표 실측 0.06 — 문턱 0.15의 40%다.</summary>
        internal const float SunglassFrontBiasRatio = 1.05f;

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
        internal const float BowTieHalfWidthRatio = 0.98f;

        /// <summary>날개 <b>바깥 변</b>의 반높이. 위 끝(ty + 0.34R)이 턱(ty + 0.36R 상당)을 넘지 않는다.</summary>
        internal const float BowTieHalfHeightRatio = 0.34f;

        /// <summary>매듭 반폭. ★ 옛 값 0.13R은 잉크 사각형이 <b>0.91획</b>이라 매듭이 통째로 획에
        /// 먹혔다(규칙 1 면제 대장에 그대로 적혀 있던 자리). 0.28R이면 1.63획이다.</summary>
        internal const float BowTieKnotRatio = 0.28f;

        /// <summary>매듭 반높이. 날개가 매듭 <b>안쪽 변</b>에서 시작하므로 이 값이 날개보다 작아야
        /// 매듭이 날개 위에 얹힌 것으로 읽힌다.</summary>
        internal const float BowTieKnotHalfHeightRatio = 0.30f;

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

        // ★ 옷깃 띠 — 2026-09-01(3차). 망토 3종에는 보조색 도형이 <b>0개</b>였다(주름 2개는 Shade 톤이라
        //   보조색이 아니다). 규칙 3-2("아이템당 정확히 1개") 위반이면서, 참고 이미지가 가진
        //   "서명 디테일 하나"가 없었다. 그래서 <b>목을 감는 띠</b>를 넣는다 — 감쌈 원칙과 서명
        //   디테일을 한 번에 만족한다.
        //   <para>처음에는 작은 <b>잠금쇠</b>(0.62 × 0.36R)로 설계했는데, 긴 망토 카드에서 전체 span이
        //   7.13R이라 정규화 뒤 <b>1.02획</b>까지 쪼그라들었다(하한 1.00). 규칙 5의 "예산 못 지키는
        //   [선택] 디테일은 넣지 않는다"에 걸리므로 목을 감는 띠로 키웠다(카드 1.24획).
        //   이것이 이 스펙에서 가장 빠듯한 자리다.</para>
        internal const float CapeCollarBandFrontRatio = 0.40f;
        internal const float CapeCollarBandBackRatio = 0.66f;
        internal const float CapeCollarBandTopRatio = 0.10f;     // 옷깃선 기준. 높이 0.44R = 1.28획
        internal const float CapeCollarBandBottomRatio = -0.34f;
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

        /// <summary>
        /// ★ <b>채운 도형의 윤곽선</b>이 그 배율에서 실제로 그리는 폭(R 배수) — 2026-09-02 M6.
        /// 위 <see cref="StrokeBudgetInHeadRadii"/>와 <b>같은 식</b>이고 하한만
        /// <see cref="StickConfig.MinFillOutlineScreenPoints"/>다.
        ///
        /// <para>규칙 1-C(색면 조건, docs/CHARACTER_FORM_SPEC.md 20-2)가 이 값을 쓴다:
        /// <c>ρ_max ≥ W_out</c>. 배율 0.509 이상에서는 하한이 안 물려 <b>0.21818 R로 상수</b>가 되므로,
        /// 게이트 세 배율(0.60 / 0.75 / 1.00)의 판정이 소수점까지 같아진다.</para>
        ///
        /// <para>소비자: Tests/EditMode/AccessoryFillAreaRuleTests — 런타임은 이 값을 읽지 않는다
        /// (렌더러는 월드 단위로 <c>RenderFillOutlineStrokeWidth</c>를 직접 계산한다).</para>
        /// </summary>
        internal static float FillOutlineBudgetInHeadRadii(float characterScale)
        {
            float scale = Mathf.Max(0.0001f, characterScale);
            float stroke = Mathf.Max(BaselineStrokeWidth * scale,
                StickConfig.MinFillOutlineScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox);
            return stroke / (BaselineHeadVisualRadius * scale);
        }

        // ---- 털모자(33-2-1 #2). ★ 2026-09-02 — 접힌 단이 <b>채움에서 낱선으로</b> 바뀌었다.
        //
        //      사용자 신고: "털모자착용시 거의 머리전체를가림". 실측이 그대로였다 — 배율 0.60에서
        //      남는 머리 0.34획 / 면적 3.2%. 단 채움 하나가 머리 원반의 <b>94%</b>를 덮고 있었다.
        //
        //      ★ 왜 "단을 얇게"가 아니라 "선으로"인가 — 두 요구가 <b>배타적</b>이기 때문이다:
        //        · 남는 머리 1.20획(목표)을 얻으려면 단 밑단이 −0.26R 위여야 한다.
        //        · 그러면 단의 두께는 커버선(−0.06R)과의 차 0.20R뿐이라 ρ_max ≈ 0.10R —
        //          규칙 1-C(색면 조건, ρ_max ≥ 0.21818R)를 <b>채움으로는 절대 통과할 수 없다</b>.
        //        · 통과시키려면 밑단을 −0.50R까지 내려야 하고, 그러면 남는 머리가 0.66획으로 무너진다.
        //      풀이: 관과 단을 <b>한 채움</b>으로 합치고 접힌 자리는 <b>그늘색 낱선 하나</b>로 긋는다
        //      (EQUIPMENT_SHAPE_SPEC 3절 원칙 7 — "실루엣 = 채운 덩어리 / 디테일 = 선 1개").
        //      이 배율에서 접힌 단은 덩어리가 아니라 선이고, 낱선은 규칙 1-C의 대상이 아니다.
        //      합집합이 같아 <b>실루엣은 한 점도 안 변하고</b>, 겹치는 채움이 사라져 ApplyAlpha
        //      페이드 중 이음매가 드러날 위험도 0이 된다.
        //
        //      ★ 관 밑변 두 점(±<see cref="BeanieBandHalfWidthRatio"/>, <see cref="BeanieBandTopRatio"/>)은
        //      <b>커버선</b>이라 여전히 손대지 않았다 — 움직이면 머리카락 자르기가 따라 움직인다(9-1절).

        /// <summary>접힌 단의 <b>아래</b> 끝. 카테고리에서 가장 깊이 내려온다.
        /// <para>★ 2026-09-02 −0.64R -&gt; −0.26R. 잉크 밑단 −0.475R이 되어 남는 머리가
        /// 0.34획 -&gt; <b>1.22획</b>(배율 0.60, 목표 1.20 통과). −0.52R로 되돌리는 것은 답이 아니다 —
        /// <b>−0.52R 시절에도 사용자는 같은 신고를 했고</b> 그 값의 실측이 0.62획이었다.</para></summary>
        internal const float BeanieBandBottomRatio = -0.26f;

        /// <summary>단 밑변의 반폭. <para>★ 2026-09-02 0.64R -&gt; 0.56R. 밑단이 올라가 짧아진 옆변을
        /// 수평 성분으로 벌충한다 — 0.4472R = <b>1.30획 @0.75 / 1.04획 @0.60</b>(획보다 짧은 변은
        /// 둥근 캡에 먹혀 모서리가 뭉갠다).</para>
        /// <para>옛 <c>BeanieCuffFlare*</c> 두 점은 삭제했다: 플레어가 만들던 허리 파임 0.08R은
        /// 배율 0.75에서 0.23획 = 화면상 0.46pt로 <b>어느 배율에서도 보이지 않았다</b>.</para></summary>
        internal const float BeanieCuffBottomHalfWidthRatio = 0.56f;

        /// <summary>접힌 단의 <b>위</b> 끝 = 관의 밑변 = <b>이 모자의 <see cref="HatCoverLocalY"/></b>.
        /// 세 사실이 한 값이라 어긋날 자리가 없다(규칙 4-a).</summary>
        internal const float BeanieBandTopRatio = -0.06f;

        internal const float BeanieBandHalfWidthRatio = 0.96f;

        /// <summary>관 꼭대기 = <see cref="BeanieBandTopRatio"/> + 이 값. 폼폼 꼭대기 계산이 이 합을 쓴다.</summary>
        internal const float BeanieCrownHeightRatio = 1.38f;

        internal const float BeanieCrownHalfWidthRatio = 1.06f;
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
        // 왜 <b>꼭대기를 고정</b>하는가: 폼폼 꼭대기가 초상화 액자의 상한
        // (CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR = 1.80R)을 넘으면 그대로 잘린다.
        // 지금은 −0.06 + 1.38 + 0.12 + 0.28 = <b>1.72R</b>로 0.08R 여유가 있다. 그래서 고정 대상은
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

        // ---- 중절모(33-2-1 #3). ★ 2026-09-01(2차) — 챙이 머리 <b>옆</b>을 지나 앞뒤로 뻗는다.
        //      크리스(관 꼭대기의 눌린 자국)는 <b>없앴다</b>: 커버선이 0.58 -> 0.08R로 내려오며 관이
        //      낮아졌고, 그 관 위에서 규칙 1(잉크 1.5획)을 지키는 V는 관을 가로질러 <b>관을 두 쪽으로
        //      가르는 선</b>이 된다. 37-6 규칙 5 — "예산을 못 지키는 [선택] 디테일은 넣지 않는다".
        //      덕분에 구성이 4개 -> 3개(챙·관·띠)가 되어 정원에도 여유가 생겼다.

        /// <summary>중절모의 커버선. 관 밑변의 두 발(뒤 +0.10R / 앞 +0.06R)의 한가운데다 —
        /// 커버선은 한 값이어야 하는데 챙은 앞뒤로 기울어 있기 때문이다.</summary>
        internal const float FedoraBrimLineRatio = 0.08f;

        internal const float FedoraBrimFrontRatio = 2.06f;
        internal const float FedoraBrimBackRatio = 1.68f;

        /// <summary>관 꼭대기 = <see cref="FedoraBrimLineRatio"/> + 이 값.</summary>
        internal const float FedoraCrownHeightRatio = 1.08f;

        /// <summary>관 밑변의 반폭. 챙·관·띠 <b>셋이 이 한 값</b>에서 발을 만든다.</summary>
        internal const float FedoraCrownHalfWidthRatio = 0.98f;

        // ★ 2026-09-01 — FedoraBandRiseRatio(0.14f)를 여기서 지웠다. 띠는 이제 좌표를 스스로 적지 않고
        //   관(crown) 밑변의 두 끝점을 <b>그대로 받아 쓴다</b>(AppendHead의 HeadFedora 문단 참고).

        // ---- 왕관(33-2-1 #4). HatCoverLocalY = +∞ — 씌우는 것이 아니라 얹는 것이라 밑이 뚫려 있다.
        //      ★ 2026-09-01(2차) — <b>채운 닫힌 도형</b>이 됐다. 옛 왕관은 채움 없는 지그재그
        //      폴리라인이라 봉우리 <b>끝이 둥근 캡으로 뭉갰다</b>(획은 둥근 캡이므로 선으로는
        //      뾰족해질 수 없다 — 37-6 규칙 6). 채운 도형의 <b>꼭짓점</b>만 점으로 수렴할 수 있다.
        //      밑이 뚫린 성질은 채움이 아니라 커버선 +∞가 계속 보장한다(if 분기가 아니다).
        internal const float CrownBaseRatio = 0.02f;
        internal const float CrownHalfWidthRatio = 0.98f;

        // ---- 동그란 안경. 코다리는 <b>렌즈 꼭대기</b>를 잇는 아치다 — 렌즈 한가운데를 가로지르면
        //      그 순간 아령(덤벨)이 된다(Tasklist V2). 간격은 규칙 1의 1.5획을 지킨다:
        //      두 렌즈 안쪽 변 사이 1.24R − 0.80R = 0.44R... 이 아니라, 반경을 키운 지금은
        //      중심 간격 1.24R − 지름 0.80R = <b>0.44R</b>이 아니라 안쪽 변 사이 0.44R = 1.28획이다.
        internal const float RoundLensOffsetRatio = 0.62f;
        internal const float RoundLensRadiusRatio = 0.40f;

        /// <summary>렌즈 중심이 <b>눈높이보다 살짝 위</b>인 양. 안경은 눈 위에 걸치는 물건이다.</summary>
        internal const float RoundLensCenterRiseRatio = 0.02f;

        /// <summary>코다리 아치의 꼭대기 높이. 두 끝은 렌즈 12각형의 <b>30도/150도 꼭짓점 그 자체</b>라
        /// 좌표를 새로 적지 않는다(규칙 4-a — 렌즈 크기를 고치면 아치가 따라온다).</summary>
        internal const float RoundBridgeRiseRatio = 0.50f;

        /// <summary>둥근 렌즈/외알 알을 근사하는 변의 수. <b>12</b>여야 30도·150도·270도 꼭짓점이
        /// 인덱스 1·5·9로 정확히 집힌다 — 코다리와 체인이 그 꼭짓점을 <b>그대로 받아 쓰므로</b>
        /// 이 값을 바꾸면 부착점이 조용히 어긋난다(규칙 4-a).</summary>
        internal const int RoundLensSegments = 12;

        // ---- 고글 — 카테고리 최대 판(3.04 × 1.24R) + 좌우로 똑같이 뻗는 스트랩(머리를 감는다).
        //      스트랩과 렌즈는 <b>한 점도 겹치지 않는다</b>: 스트랩이 되돌아오는 변이 렌즈의 윗변
        //      그 자체다. EYES 채움은 전부 같은 레이어라 겹치면 그리기 순서가 미정이 된다.

        /// <summary>스트랩이 좌우로 뻗는 끝. 머리 원(1.0R) <b>밖</b>이다 — 끈은 관자놀이를 지나
        /// 뒤로 돌아가는 물건이라 얼굴 옆으로 나오는 것이 옳다. 상한은 레이어 회귀가 잠근
        /// "안경은 머리 반경의 1.6배를 넘지 않는다"이고, 여기에 5% 여유를 뒀다.</summary>
        internal const float GoggleStrapReachRatio = 1.52f;

        /// <summary>외알 안경 알의 중심 x. ★ 2026-09-01(3차) — <see cref="EyeOffsetXInHeadRadii"/>에서
        /// 유도하던 것을 <b>끊었다</b>. 그 상수는 "눈이 있던 자리"의 정의처이고 <b>가림 판정</b>이 읽는데,
        /// 알까지 거기 묶어 두면 알 크기를 고칠 때 판정선이 함께 움직인다.
        /// 지금 알은 <see cref="DrawnEyeOffsetRatio"/>와 대칭인 자리(+0.62R)에 있다.</summary>
        internal const float MonocleOffsetRatio = DrawnEyeOffsetRatio;

        /// <summary>알 반경. 드러난 눈과의 간격이 1.5획을 넘는 상한이기도 하다
        /// (2·0.62 − 0.36 − 0.34 = 0.56R = 1.63획).</summary>
        internal const float MonocleRadiusRatio = 0.36f;

        // ---- 줄무늬 타이 / 목도리 / 방울 목걸이(33-2-3). 부착 기준선은 나비넥타이와 같은 BowTieLocalY.
        //      ★ 2026-09-01(3차) — 몸통이 <b>선 하나</b>뿐이므로 목 아이템은 <b>폭이 곧 존재감</b>이다.
        //      면제 대장에 남아 있던 5건(나비 매듭 0.91획 / 타이 blade 0.87획 · 줄무늬 2개)을 전부 닫았다.
        internal const float TieKnotHalfWidthRatio = 0.36f;
        internal const float TieKnotHalfHeightRatio = 0.30f;

        /// <summary>매듭 <b>밑변</b>의 깊이(반높이와 다르다 — 매듭은 아래로 좁아지는 사다리꼴이다).</summary>
        internal const float TieKnotBottomDropRatio = 0.28f;

        internal const float TieBladeLengthInTorso = 0.55f;

        /// <summary>blade 반폭. 옛 값 0.15R은 폭 0.87획이라 <b>획 하나보다 좁은 넥타이</b>였다 —
        /// 화면에서는 그냥 선이다. 0.34R이면 폭 1.98획이라 천으로 읽힌다.</summary>
        internal const float TieBladeHalfWidthRatio = 0.34f;

        /// <summary>33-2-5 (D) — 줄무늬 타이 "월요일마다 조금 느슨해진다". 매듭을 R·0.12 내리고 blade를 3도 기울인다.</summary>
        internal const float TieMondayLoosenDropRatio = 0.12f;
        internal const float TieMondayLoosenTiltDegrees = 3f;

        // ★ 목도리 — 목에 감긴 <b>고리</b>(보조색) + 앞뒤 길이가 다른 자락 2개(주색).
        //   ★ 2026-09-01(3차) 미보고 결함 2건을 함께 닫았다:
        //     (1) <b>보조색이 2개</b>였다(자락 둘 다 보조색) — 규칙 3-2("아이템당 정확히 1개") 위반이
        //         조용히 살아 있었다. 지금은 고리 하나만 보조색이다.
        //     (2) <b>도형을 넣는 순서가 반대</b>였다. 같은 채움 레이어에서는 나중에 넣은 것이 위로 오고,
        //         목도리는 <b>고리가 자락을 덮어야</b> "감았다"로 읽힌다. 옛 순서(고리 먼저)에서는
        //         자락이 고리 위로 떴다. 그래서 자락 2개를 <b>먼저</b>, 고리를 <b>나중에</b> 넣는다.
        internal const float ScarfWrapHalfWidthRatio = 0.92f;
        internal const float ScarfWrapTopRatio = 0.30f;
        internal const float ScarfWrapCenterTopRatio = 0.06f;   // 윗변이 목덜미로 파이는 깊이
        internal const float ScarfWrapSideRatio = -0.20f;
        internal const float ScarfWrapDipRatio = -0.62f;        // 고리가 가슴으로 처지는 최저점
        internal const float ScarfFrontTailLengthInTorso = 0.40f;
        internal const float ScarfBackTailLengthInTorso = 0.62f;
        internal const float CollarHalfWidthRatio = 0.78f;

        /// <summary>목줄 양 끝의 높이(목선 기준). <see cref="CollarCurve"/>가 이 값을 쓴다.</summary>
        internal const float CollarRiseRatio = 0.16f;

        /// <summary>목줄 한가운데가 아래로 처지는 깊이. 매달리는 것(방울/펜던트)은 이 최저점에서
        /// 시작해야 <b>매달린 지점이 보인다</b>(규칙 4) — 그래서 상수로 뽑아 유도한다(규칙 4-a).</summary>
        internal const float CollarDipRatio = 0.32f;

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
        internal const float BellRadiusRatio = 0.30f;

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
        // ★ 배낭 — 2026-09-01(3차) "대괄호"의 정체와 수정.
        //   옛 배낭은 <b>몸 1개(윤곽) + 덮개 선 1개 + 끈 1개</b>였다. 몸의 채움이 몸통 선 뒤(sort −1)에
        //   깔리는데 가로 폭이 1.10R뿐이라 화면에는 <b>세로로 긴 얇은 조각</b>만 남았다 — 그게 대괄호다.
        //   지금은 상자(1.56R × 2.53R) + 뚜껑 + 버클 + 끈이다. 가로를 1.10 -> 1.56R로 넓힌 것이 핵심이다.
        internal const float PackCenterBackRatio = 0.72f;
        internal const float PackDropInTorso = 0.40f;
        internal const float PackHalfWidthRatio = 0.78f;
        internal const float PackHalfHeightInTorso = 0.36f;

        /// <summary>버클(보조색) 반폭. 0.60 × 0.54R = 1.74 × 1.57획이라 규칙 1의 1.5획을 넘는다.</summary>
        internal const float PackBuckleHalfWidthRatio = 0.30f;

        // ============================================================================
        // ★ 2026-09-01 카테고리당 +2종 — <b>임시 플레이스홀더</b>의 치수표
        // ============================================================================
        // 리더 지시: "완전히 새로 디자인하지 말고 기존 셰이프를 팔레트/비율만 바꿔 변형". 그래서 아래
        // 값들은 전부 <b>바로 위 형제 상수의 변주</b>이고, 새 조형 규칙을 만들지 않는다.
        // 다만 <b>획 예산</b>(37-6 규칙 1, ShippingStrokeBudgetInHeadRadii ≈ 0.344R)만은 지킨다 —
        // 양끝이 모두 꺾임인 선분이 획보다 짧으면 그 선분은 화면에서 통째로 먹힌다. 아래 비율은
        // 그 조건을 손으로 검산해 잡았다(가장 빠듯한 자리마다 주석으로 값을 남겼다).

        // ---- 베레모(모자 4번) — 털모자 관(crown)의 <b>비대칭 변주</b>. 뒤로 처지고 꼭지가 없다.
        //      감쌈은 <b>뒤로 처진 끝</b>(−1.46R, y −0.10R)이 만든다 — 이 카테고리에서 유일하게
        //      "옆이 아니라 뒤로" 감싸는 모자다.
        internal const float BeretBrimLineRatio = 0.02f;    // = 이 모자의 HatCoverLocalY
        internal const float BeretCrownHeightRatio = 1.04f;
        internal const float BeretBackDroopRatio = 1.46f;   // 뒤로 늘어진 끝
        internal const float BeretFrontRatio = 0.92f;

        /// <summary>앞 어깨 꼭짓점의 반폭. 밑변 앞발(<see cref="BeretFrontRatio"/>)보다 살짝 밖으로
        /// 나가 앞이 부푼 덩어리로 읽힌다.</summary>
        internal const float BeretFrontShoulderRatio = 0.98f;

        /// <summary>그 어깨점의 y. 밑변(<see cref="BeretBrimLineRatio"/>)과의 거리가 곧 앞 옆변 길이라
        /// 배율이 낮을 때 가장 먼저 뭉개는 자리다.</summary>
        internal const float BeretFrontShoulderTopRatio = 0.54f;

        /// <summary>뒤로 늘어진 끝이 밑변보다 더 내려가는 깊이. 밑변(= 보조색 테)의 기울기를 만든다.</summary>
        internal const float BeretBackDroopDropRatio = 0.12f;

        // ---- 밀짚모자(모자 5번) — 중절모의 <b>납작·광폭 변주</b>. 챙이 카테고리 최대(폭 4.24R)다.
        internal const float StrawBrimLineRatio = 0.08f;    // = 이 모자의 HatCoverLocalY
        internal const float StrawBrimFrontRatio = 2.18f;
        internal const float StrawBrimBackRatio = 2.06f;
        internal const float StrawCrownHeightRatio = 1.06f;
        internal const float StrawCrownHalfWidthRatio = 0.86f;

        // ★ 2026-09-01 — StrawBandRiseRatio(0.16f)도 같은 이유로 지웠다(중절모와 같은 결함·같은 해법).

        // ---- 뿔테 안경(안경 4번) — 2026-09-01(2차). 옛 그림(위아래로 겹친 판 2장)은 카드에서
        //      <b>뚜껑 달린 상자</b>로 읽혔다. 뿔테의 정체는 "굵은 눈썹테 <b>아래에 렌즈가 매달린다</b>"이므로
        //      판을 위아래가 아니라 <b>테 1장 + 렌즈 2장</b>으로 나눈다 — 렌즈가 둘로 갈리는 순간
        //      맥락 없이도 안경으로 읽힌다(형제들과 같은 신호). 테는 여전히 보조색이다(규칙 3-2).
        internal const float BrowlineBarOuterRatio = 1.06f;
        internal const float BrowlineBarInnerRatio = 0.98f;
        internal const float BrowlineBarBottomRatio = 0.14f;       // 테 밑변 = 렌즈 윗변(간격 0, 규칙 4)
        internal const float BrowlineBarTopRatio = 0.58f;
        internal const float BrowlineLensInnerRatio = 0.24f;       // 두 렌즈 사이 0.48R = 1.40획
        internal const float BrowlineLensOuterRatio = 1.06f;

        // ---- 안대(안경 5번) — 외알안경과 같은 "앞쪽 눈에만" 규약. 채운 천 + 뒤로 넘어가는 끈 +
        //      <b>드러난 뒤쪽 눈</b>. ★ 2026-09-01(3차): 끈이 <b>주색</b>으로 바뀌었다 —
        //      보조색 정원 1개를 눈이 가져가기 때문이고, 의미상으로도 옳다(천과 끈은 같은 가죽이다).
        //      끈 끝점을 polar(146°, 0.99) -> <b>polar(122°/238°, 1.02)</b>로 올렸다:
        //      옛 자리는 드러난 눈과 <b>0.77획</b> 떨어져 규칙 4가 "최악"이라 못박은 구간이었다.
        internal const float PatchOffsetRatio = EyeOffsetXInHeadRadii;
        /// <summary>천의 반폭/반높이. ★ 알(외알안경)보다 <b>커야</b> 두 아이템이 갈린다 —
        /// 스펙 초안은 둘 다 0.72 × 0.72R이라 원이 사각형에 <b>내접</b>했고, 채움 격자 구분도가
        /// 0.09(문턱 0.20)까지 떨어졌다. 천은 렌즈가 아니라 <b>덮개</b>이므로 큰 편이 옳다.</summary>
        internal const float PatchHalfWidthRatio = 0.38f;

        internal const float PatchHalfHeightRatio = 0.44f;

        /// <summary>천의 중심 x. 외알 알과 <b>같은 유도</b>(드러난 눈의 거울 자리)다 — 두 아이템이
        /// 같은 눈을 가린다는 사실이 좌표에서 보여야 한다(규칙 4-a).</summary>
        internal const float PatchCenterRatio = DrawnEyeOffsetRatio;

        /// <summary>끈 끝점의 반경(머리 원 밖 0.02R). 두 끝의 각도는 대칭이라 하나로 둔다.</summary>
        internal const float PatchStrapReachRatio = 1.02f;

        /// <summary>끈 끝점의 각도(도). 위쪽 끝이 이 각도, 아래쪽 끝이 360 − 이 각도다.</summary>
        internal const float PatchStrapDegrees = 122f;

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
        internal const float PendantHalfWidthRatio = 0.30f;

        /// <summary>마름모 반높이. 위 꼭짓점은 <b>목줄 최저점</b>에 붙고 아래 꼭짓점은 가슴께로
        /// 내려온다 — 방울(목선 아래 0.51R)과 <b>매달린 길이</b>로도 갈린다(목선 아래 1.38R).</summary>
        internal const float PendantHalfHeightRatio = 0.64f;

        // ---- 반다나(넥타이 5번) — 목도리 띠의 <b>납작 변주</b> + 앞으로 늘어진 삼각 자락 하나.
        internal const float BandanaWrapRiseRatio = 0.06f;
        internal const float BandanaWrapHalfWidthRatio = 0.84f;
        internal const float BandanaWrapHalfHeightRatio = 0.22f;   // 위아래 변 간격 0.44R(= 1.28획)

        /// <summary>자락 끝이 내려가는 길이(몸통 배수). 자락은 <b>채운 삼각형</b>이라 밑변이
        /// 0.48R(1.40획)이어야 꼭짓점 두 개가 획에 안 먹힌다.</summary>
        internal const float BandanaTailLengthRatio = 0.30f;

        internal const float BandanaTailHalfWidthRatio = 0.24f;

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
        // ★ HAIR — 2026-09-01 (2차) "덩어리" 재설계 (docs/EQUIPMENT_SHAPE_SPEC.md 4절)
        // ============================================================================
        // 사용자 신고: "머리스타일 옵션도 이정도 퀄이 되어야지 / 내가 준거랑 차이가 크잖아".
        //
        // 조잡함의 정체는 취향이 아니라 <b>숫자 하나</b>였다. 배율 0.75에서 머리 지름은 획 5.82개뿐인데,
        // 옛 5종은 정수리에서 두피 링 위로 <b>획 하나보다 얇게</b> 덮고 있었다:
        //   삐친 0.64획 · 단정 0.41획 · 곱슬 0.81획 · 포니테일 0.47획 (통과는 바가지 1.51획 하나뿐).
        // 머리카락 윤곽선과 두피 링은 각각 1획이라, 둘 사이가 1.5획 미만이면 <b>화면에서 한 줄로 뭉친다</b>.
        // 그래서 다섯 종이 "머리에 씌운 뚜껑"이 됐고, 유일하게 통과한 바가지머리만 페르소나가 읽어냈다.
        // 그리고 6종 중 <b>어느 것도 턱(−1.0R) 아래로 내려가지 않았다</b>(최저 −0.68R).
        //
        // ★ 옛 상수 두 개(<c>HairSpanEndDegrees</c> 196 / <c>HairlineEdgeRatio</c> −0.06)를 <b>폐기했다</b>.
        //   그 둘을 조정하는 것으로는 못 고친다 — 이유는 기하다. 옛 바깥 윤곽은 <b>반경이 일정한 극좌표
        //   호</b>라, 스팬을 늘리면 옆머리는 내려오지만 반경이 그대로여서 <b>머리에 딱 붙은 껍질</b>이 된다.
        //   반경을 키우면 이번엔 정수리가 초상화 액자(1.75R)를 넘는다. 참고 이미지의 머리카락은
        //   "머리를 감싼 껍질"이 아니라 <b>머리보다 넓은 덩어리</b>다.
        //
        // 그래서 <b>구성 자체</b>를 바꿨다. 6종이 경계를 도는 순서는 전부 같다 —
        // 이 순서가 어긋나면 폴리곤이 자기교차하고 귀 자르기(<see cref="Triangulate"/>)가 깨진다.
        //
        //   돔(앞→뒤, 반경 cap) → 뒤 커튼(내려갔다 안쪽으로) → 두피 안쪽 호(뒤→앞, 0.58R) → 앞 커튼
        //   ↑ 마지막 점이 돔의 첫 점으로 닫힌다
        //
        // ★ 원칙 정정 — 옛 문단은 "채움은 가릴 것이 있을 때만"이라고 적었다. <b>틀렸다.</b> 이 엔진에서
        //   "굵은 덩어리"를 만드는 유일한 수단이 채움이다. 머리카락은 두피를 가리려고가 아니라
        //   <b>덩어리를 만들려고</b> 채운다. 얇은 띠(stroke)로 덩어리를 흉내내면 반드시 소심해진다.

        /// <summary>두피를 파고드는 안쪽 경계의 반경. 규칙 4의 부착 판정선(1 − W = 0.656R)보다
        /// <b>작아야</b> 한다 — 그래야 "머리에서 자란 것"으로 읽힌다.
        /// <para>옛 포물선 이마선(<c>HairlineCrestRatio</c>)을 동심 호로 되돌린 것이 아니다. 옛 이마선은
        /// 얼굴을 <b>가로지르는 현</b>이라 안쪽 경계가 그것 하나뿐이었고, 그래서 덩어리가 정수리에만
        /// 남았다. 지금은 안쪽 호가 두피를 돌고 <b>커튼 두 개</b>가 얼굴 양옆으로 내려온다.</para></summary>
        internal const float HairInnerRadiusRatio = 0.58f;

        /// <summary>돔 반경의 <b>하한</b> = 1.0R + 1.5W. 이 값이 '뚜껑'을 없앤다 —
        /// 정수리에서 머리카락 윤곽과 두피 링이 각자 1획을 갖고도 1.5획 떨어져 있게 하는 최소치다.
        /// <para>매끈한 돔 4종(단정·곱슬·바가지·포니테일)이 이 하한 위에 있고, 삐친머리만 예외다 —
        /// 그쪽은 <b>봉우리와 골이 번갈아 도는 실루엣</b> 자체가 정체라 골이 하한 아래로 내려간다.
        /// 대신 그 골 양옆의 봉우리가 1.70R 이상이라 정수리 부근의 가장 두꺼운 자리는 2.2획이다.</para></summary>
        internal const float HairCapMinRatio = 1.52f;

        /// <summary>돔 반경의 <b>상한</b> = 초상화 액자(1.75R). 넘으면 정보창에서 정수리가 잘린다
        /// (<see cref="CharacterPortraitStage"/>의 <c>TallestAccessoryAboveHeadCenterInR</c>).</summary>
        internal const float HairCapMaxRatio = 1.75f;

        /// <summary>돔의 분할 수. 12면 인접 두 점 사이가 배율 0.75에서도 획보다 길어 각이 지지 않는다.</summary>
        internal const int HairDomeSegments = 12;

        /// <summary>두피 안쪽 호의 분할 수.</summary>
        internal const int HairInnerArcSegments = 7;

        // ---- 0 삐친머리 — 바깥 윤곽 <b>자체</b>가 다섯 번 뾰족하다(머리 위에 붙인 삼각형이 아니다).
        //      옛 도형은 매끈한 돔에 삼각형 하나를 얹은 것이라, 획을 얹으면 삼각형이 돔에 흡수됐다.
        internal const float CowlickSpikeMinRatio = 1.28f;   // 봉우리 사이 골
        internal const float CowlickSpikeMaxRatio = 1.78f;   // 봉우리 끝(액자 안: y = 1.63R)

        // ---- 1 단정한머리 — 곧게 늘어진 생머리. 카테고리에서 <b>가장 길다</b>(뒤 커튼 끝 −2.12R).
        internal const float NeatCapRatio = 1.58f;

        // ---- 2 곱슬머리 — 물결이 정수리가 아니라 <b>커튼</b>에 있다. 이유는 산술이다:
        //      웨이브를 정수리에 실으면 골 ≥ 1.516R(뚜껑 방지)이고 마루 ≤ 1.75R(액자)이라
        //      진폭이 0.117R(0.34획) 이하여야 하는데, 아래 상수는 0.75획 이상을 요구한다.
        //      <b>동시에 만족 불가능</b>하므로 물결을 세로(커튼)로 옮겼다 — 세로에는 상한이 없다.
        internal const float CurlCapRatio = 1.62f;

        /// <summary>커튼 물결 한 굽이의 <b>진폭</b>(R 배수, 마루-골의 절반). 옛 값 0.16R은 획 반폭
        /// (0.17R)보다 작아 물결이 자기 획 안에 통째로 매몰됐다 — 그때 <b>곱슬 ≡ 단정</b>이었다.
        /// <para>이 값은 좌표를 만들지 않고 <b>좌표가 지켜야 할 하한</b>을 선언한다. 실제 커튼 점이
        /// 이 진폭을 갖는지는 <c>AccessoryStrokeBudgetTests</c>가 출하 도형에서 직접 잰다.</para></summary>
        internal const float CurlAmplitudeRatio = 0.28f;

        // ---- 3 민머리 — 덩어리가 <b>없는</b> 것이 정체다. 관자놀이/뒤통수에 남은 테 2조각.
        //      안쪽 0.58R은 형제들의 두피 안쪽 호와 <b>같은 값</b>이다(규칙 4-a — 두 벌로 적지 않는다).
        internal const float BaldRimInnerRadiusRatio = HairInnerRadiusRatio;
        internal const float BaldRimOuterRadiusRatio = 1.20f;
        internal const float BaldRimBackFromDegrees = 120f;
        internal const float BaldRimBackToDegrees = 208f;
        internal const float BaldRimFrontFromDegrees = -28f;
        internal const float BaldRimFrontToDegrees = 26f;
        internal const int BaldRimBackSegments = 7;
        internal const int BaldRimFrontSegments = 4;

        // ---- 4 바가지머리 — 턱선에서 <b>수평으로 자른</b> 단발. '자른 밑선'이 정체다.
        //      돔은 옆머리 밑변과 만나는 각도에서 시작/끝난다(각도를 손으로 적으면 그 자리에 틈이 생긴다).
        internal const float BowlCapRadiusRatio = 1.62f;

        /// <summary>옆머리를 자른 높이(머리 중심 기준). 옛 값 −0.46R은 <b>턱(−1.0R)보다 한참 위</b>라
        /// 단발이 아니라 반모자였다. 6종 중 유일하게 턱을 넘어가는 값이 여기서 나온다.</summary>
        internal const float BowlCutLineRatio = -0.95f;

        /// <summary>옆머리 안쪽 변의 x(= 얼굴이 드러나는 폭의 절반). 앞머리 선의 반폭이기도 하다 —
        /// 두 값이 갈라지면 모서리에 이가 빠진다.</summary>
        internal const float BowlSideHalfWidthRatio = 0.80f;

        /// <summary>이마를 가로지르는 앞머리 선의 높이. 눈동자 위끝(0.227R) 위로 <b>획 반폭까지 얹어도</b>
        /// 0.288R 지점에 머문다(여유 0.18획). 값이 내려간 것은 돔이 커지며 이마가 더 덮였기 때문이다.</summary>
        internal const float BowlFringeLineRatio = 0.46f;

        /// <summary>앞머리 선을 쪼개는 수. 한가운데 점(x=0)이 생겨 실루엣이 두피 링 안쪽까지
        /// 파고든 것으로 계측된다(규칙 4의 부착 검사가 정점을 본다).</summary>
        internal const int BowlFringeSegments = 4;

        /// <summary>바가지 돔의 분할 수. 형제들(12)보다 촘촘한 것은 돔이 반원보다 <b>긴 호</b>라서다
        /// (자른 밑선이 −0.95R까지 내려가 스팬이 250도에 이른다).</summary>
        internal const int BowlDomeSegments = 14;

        // ---- 5 포니테일 — 짧은 덩어리 + 뒤통수에서 묶여 떨어지는 긴 묶음(끝 −2.42R, 뾰족하다).
        internal const float PonytailCapRatio = 1.56f;

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
        /// <para><see cref="NothingCovered"/>(= +∞) = 아무것도 가리지 않는다. 미착용도, <b>왕관</b>도
        /// 여기다 — 왕관이 예외인 것은 if 분기가 아니라 <b>이 표의 값</b>이다. 왕관은 씌우는 것이 아니라
        /// 얹는 것이라 밑이 뚫려 있고, 그래서 머리 모양이 함께 보이는 것이 옳다.</para>
        ///
        /// <para>★ 2026-09-02 — <b>"알 수 없는 번호"를 그 둘과 분리했다.</b> 그 전에는 셋 다
        /// <c>default: return +∞</c> 하나로 뭉뚱그려져 있어서, 7번째 모자를 표에 넣으면 그 모자가
        /// <b>조용히 왕관 취급</b>을 받았다(= 머리카락을 안 자른다 -> 머리카락이 모자를 뚫고 나온다).
        /// 지금은 미착용을 먼저 거르고, 왕관은 <b>명시된 case</b>이며, 남은 default는 결함이므로
        /// <see cref="ShapeCoverageGuard"/>가 큰 소리로 알린다. 돌려주는 값이 여전히 +∞인 이유는
        /// "모르는 모자 밑에서 머리카락을 자르는 것"이 더 파괴적이기 때문이다 — 잘라 버리면 화면에서
        /// 머리카락까지 함께 사라져 원인이 두 겹이 된다.</para>
        /// </summary>
        internal static float HatCoverLocalY(int hatItemIndex, in Rig rig)
        {
            // 미착용(-1). 알 수 없는 번호와 <b>같은 값</b>을 돌려주지만 다른 사실이라 먼저 거른다 —
            // 미착용은 정상이고 알 수 없는 번호는 결함이다.
            if (hatItemIndex < 0) return NothingCovered;

            switch (hatItemIndex)
            {
                case HeadCap: return HatBrimLocalY(rig);
                case HeadBeanie: return rig.HeadCenterY + rig.HeadRadius * BeanieBandTopRatio;
                case HeadFedora: return rig.HeadCenterY + rig.HeadRadius * FedoraBrimLineRatio;
                case HeadBeret: return rig.HeadCenterY + rig.HeadRadius * BeretBrimLineRatio;
                case HeadStraw: return rig.HeadCenterY + rig.HeadRadius * StrawBrimLineRatio;

                // 왕관 — <b>의도된</b> 면제. 얹는 물건이라 밑이 뚫려 있다(위 문단).
                case HeadCrown: return NothingCovered;

                default:
                    ShapeCoverageGuard.ReportUnknownHatCover(hatItemIndex);
                    return NothingCovered;
            }
        }

        /// <summary>"이 모자는 머리카락을 하나도 가리지 않는다"를 뜻하는 커버선 값.
        /// <c>float.PositiveInfinity</c>를 그대로 적으면 그 자리가 <b>선언</b>인지 <b>폴백</b>인지
        /// 코드만 봐서는 구분되지 않는다 — 이름이 그 구분을 대신한다.</summary>
        internal const float NothingCovered = float.PositiveInfinity;

        // ==================== 도형 ====================

        /// <summary>야구모자의 관(crown) — 닫힌 고리. 옆벽이 −0.22R까지 내려와 <b>머리를 감싼다</b>.
        /// <para>밑변의 두 끝점(<c>[7] [8]</c>)은 챙이 그대로 받아 쓴다 — 좌표를 두 번 적으면
        /// 한쪽만 고쳐지는 순간 관과 챙 사이에 틈이 생긴다(규칙 4-a).</para></summary>
        internal static Vector3[] HatCrown(in Rig rig)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            float side = r * HatCrownHalfWidthRatio;
            return new[]
            {
                rig.F(-side, hc - r * 0.22f),
                rig.F(-r * 1.02f, hc + r * 0.36f),
                rig.F(-r * 0.72f, hc + r * 1.02f),
                rig.F(0f, HatTopLocalY(rig)),
                rig.F(r * 0.72f, hc + r * 1.00f),
                rig.F(r * 1.00f, hc + r * 0.34f),
                rig.F(side, hc - r * 0.06f),
                HatCrownFrontFoot(rig),
                HatCrownBackFoot(rig),
            };
        }

        /// <summary>관 밑변의 뒤쪽 발 = <b>커버선 그 자체</b>. 챙의 첫 점이기도 하다.</summary>
        internal static Vector3 HatCrownBackFoot(in Rig rig)
            => rig.F(-rig.HeadRadius * 0.40f, HatBrimLocalY(rig));

        internal static Vector3 HatCrownFrontFoot(in Rig rig)
            => rig.F(rig.HeadRadius * 0.60f, HatBrimLocalY(rig) - rig.HeadRadius * 0.02f);

        /// <summary>챙 — <b>닫힌 띠</b>다(규칙 8). 부피는 머리 원 <b>밖</b>(x = +1.22R, 두께 0.531R)에
        /// 있고 얼굴 앞을 지나는 구간(x = 0)은 0.184R짜리 <b>얇은 판</b>이다 — 원반을 모서리로 보는
        /// 자리라서 그렇다. 뒤는 점으로 수렴한다(<see cref="HatBrimRootDropRatio"/>).
        /// <para>★ 2026-09-02: 옛 좌표는 이 관계가 뒤집혀 머리 위에서 가장 두꺼웠고, 그것이
        /// 사용자 신고 "ㅁ자 창"이다. 머리 원반에 얹는 색 0.643 R²(72%) -&gt; <b>0.319 R²(50%)</b>.
        /// 남는 머리 두께 0.85획 -&gt; <b>1.51획</b>(배율 0.60). ρ_max 0.2561R = 1.17획(규칙 1-C 통과).</para></summary>
        internal static Vector3[] HatBrim(in Rig rig)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            return new[]
            {
                HatCrownBackFoot(rig),
                HatCrownFrontFoot(rig),
                rig.F(r * 1.36f, hc - r * 0.02f),
                rig.F(r * HatBrimReachRatio, hc - r * 0.34f),   // 끝 — 점으로 수렴
                rig.F(r * 1.22f, hc - r * 0.54f),               // ★ 부피는 여기(머리 밖)
                rig.F(r * 0.62f, hc - r * 0.26f),               // ★ 머리 위 구간은 얇은 판
                rig.F(-r * 0.06f, HatBrimLocalY(rig) - r * HatBrimRootDropRatio),
            };
        }

        // ---- EYES 가리개. 옛 GlassesLensFront/Back/Bridge/Temple 4개는 여기서 지웠다 —
        //      그 넷은 "렌즈 2개 + 코받침 + 다리"라는 <b>눈이 있는 얼굴</b>의 도형 언어였다.

        /// <summary>선글라스 렌즈 한 장(채움). <paramref name="forward"/>가 true면 진행 방향쪽이다.
        /// <para>점 순서는 두 렌즈가 <b>같은 회전 방향</b>이 되도록 갈라 둔다 — 채움 삼각형 분할이
        /// 시계/반시계에 따라 다른 결과를 내므로, 한쪽만 뒤집히면 카드에서 한 렌즈만 구멍이 난다.</para></summary>
        internal static Vector3[] SunglassLens(in Rig rig, bool forward)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            // 진행 반대쪽(뒤) 렌즈가 <b>원본</b>이고, 앞 렌즈는 그것을 거울로 뒤집어
            // <see cref="SunglassFrontBiasRatio"/>만큼 키운 것이다. 좌표를 두 벌 적으면
            // 한쪽만 고쳐지는 순간 "안경 한 짝"이 되고, 카드에서 화살표로 읽힌다(리더 육안 검증 V1).
            var back = new[]
            {
                new Vector2(-SunglassInnerRatio, 0.34f),
                new Vector2(-0.96f, 0.30f),
                new Vector2(-SunglassOuterRatio, -0.16f),
                new Vector2(-0.32f, -0.44f),
            };

            var result = new Vector3[back.Length];
            for (int i = 0; i < back.Length; i++)
            {
                // 거울쪽은 점 순서를 뒤집는다 — 두 짝의 회전 방향이 같아야 채움 삼각형 분할이
                // 한쪽만 뒤집히지 않는다(날개가 쓰는 규약과 같다).
                Vector2 p = forward ? back[back.Length - 1 - i] : back[i];
                // 앞 렌즈는 <b>바깥으로만</b> 5% 자란다. 안쪽 변(코다리가 걸리는 자리)은 두 렌즈가
                // 같은 x여야 아치가 기울지 않고, 세로까지 키우면 "한 짝만 큰 안경"이 된다.
                float x = forward
                    ? SunglassInnerRatio + (-p.x - SunglassInnerRatio) * SunglassFrontBiasRatio
                    : p.x;
                result[i] = rig.F(x * r, cy + p.y * r);
            }
            return result;
        }

        /// <summary>코다리 — 두 렌즈의 <b>안쪽 꼭대기 꼭짓점 그 자체</b>를 잇는 아치(보조색).
        /// 좌표를 새로 적지 않으므로 렌즈를 고치면 다리가 따라온다(규칙 4-a).</summary>
        internal static Vector3[] SunglassBridge(in Rig rig)
        {
            Vector3[] back = SunglassLens(rig, forward: false);
            Vector3[] front = SunglassLens(rig, forward: true);
            // 앞 렌즈는 점 순서가 뒤집혀 있으므로 안쪽 꼭대기가 <b>마지막</b> 점이다.
            return new[]
            {
                back[0],
                rig.F(0f, GlassesLocalY(rig) + rig.HeadRadius * SunglassBridgeRiseRatio),
                front[front.Length - 1],
            };
        }

        /// <summary>
        /// <b>드러난 눈</b> — 채운 아몬드 4점(양 끝이 점으로 수렴한다).
        /// <para>한쪽만 가리는 물건(외알안경·안대)에서만 쓴다. 두 눈을 다 가리는 물건은 아무것도
        /// 보여 주지 않는다 — 렌즈 <b>안</b>으로 눈이 비치는 그림은 이 배율에서 기하학적으로
        /// 불가능하기 때문이다(위 EYES 문단의 산술).</para>
        /// <para><b>동공은 넣지 않는다.</b> 내부를 보이려면 3.0W = 1.03R, 즉 머리 반지름만 한 눈이
        /// 필요하다(규칙 1). 아몬드 하나가 이 배율에서 그릴 수 있는 눈의 전부다.</para>
        /// </summary>
        /// <param name="sign">−1 = 진행 반대쪽(= 가려지지 않은 눈). 지금 두 소비자 모두 −1이다.</param>
        internal static Vector3[] DrawnEye(in Rig rig, float sign)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            float dx = DrawnEyeOffsetRatio;
            float hw = DrawnEyeHalfWidthRatio;
            float hh = DrawnEyeHalfHeightRatio;
            return new[]
            {
                rig.F(sign * (dx - hw) * r, cy),
                rig.F(sign * (dx - 0.06f) * r, cy + hh * r),
                rig.F(sign * (dx + hw) * r, cy + 0.02f * r),
                rig.F(sign * (dx + 0.02f) * r, cy - hh * r),
            };
        }

        /// <summary>나비넥타이 왼쪽 날개 — <b>채운</b> 4점. 옛 날개는 열린 선 3점이라 화면에서
        /// 두께가 없었다(규칙 2 — 이 엔진에서 덩어리를 만드는 유일한 수단이 채움이다).</summary>
        internal static Vector3[] BowTieLeftWing(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = BowTieLocalY(rig);
            float hw = r * BowTieHalfWidthRatio, hh = r * BowTieHalfHeightRatio, knot = r * BowTieKnotRatio;
            return new[]
            {
                rig.F(-hw * 0.878f, cy + hh),
                rig.F(-knot, cy + r * 0.02f),
                rig.F(-hw * 0.878f, cy - hh),
                rig.F(-hw, cy),
            };
        }

        internal static Vector3[] BowTieRightWing(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = BowTieLocalY(rig);
            float hw = r * BowTieHalfWidthRatio, hh = r * BowTieHalfHeightRatio, knot = r * BowTieKnotRatio;
            return new[]
            {
                rig.F(hw * 0.878f, cy + hh),
                rig.F(hw, cy),
                rig.F(hw * 0.878f, cy - hh),
                rig.F(knot, cy + r * 0.02f),
            };
        }

        /// <summary>매듭 — <b>채운 직사각형</b>. 옛 8각 근사(<c>RoundedBox</c>, 2026-09-01(3차)에
        /// 마지막 소비자가 사라져 함께 지웠다)를 쓰지 않는 이유는 안대와 같다: 이 크기에서
        /// 모따기 변이 획보다 짧아 화면에서 통째로 먹힌다(규칙 1).</summary>
        internal static Vector3[] BowTieKnot(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = BowTieLocalY(rig);
            float hw = r * BowTieKnotRatio, hh = r * BowTieKnotHalfHeightRatio;
            return new[]
            {
                rig.F(-hw, cy + hh), rig.F(hw, cy + hh), rig.F(hw, cy - hh), rig.F(-hw, cy - hh),
            };
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
        ///
        /// <para>★ 2026-09-02 — 알 수 없는 자리/번호는 <b>더 이상 조용히 넘어가지 않는다.</b>
        /// 옛 주석은 "아무것도 넣지 않는다"였고 그것이 곧 이 저장소가 금지한 <b>조용한 실패</b>였다:
        /// 7번째 모자를 표에 넣으면 카드는 뜨는데 몸에는 아무것도 안 그려지고 에러도 로그도 없었다.
        /// 지금은 <see cref="ShapeCoverageGuard"/>가 로그로 알리고, 개발 게이트가 열려 있으면
        /// 그 자리에 <b>빠졌다는 표식</b>(<see cref="MissingMarker"/>)을 그린다.</para>
        ///
        /// <para>예외는 여전히 던지지 않는다 — 24시간 상주 앱이 도형 하나 때문에 멈추면 안 되고,
        /// 이 경로는 렌더 루프 안이라 예외가 화면 전체를 날린다.</para>
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

                // ★ FX/PET은 몸에 붙는 도형이 <b>원래</b> 없다(Interaction/AppearanceShapeBuilder.cs 소관).
                //   default로 흘려보내면 정상 경로가 매 재구성마다 결함으로 신고된다 — 렌더러는 7개 자리를
                //   전부 순회하며 이 함수를 부르고, 카드(AccessoryCardIcon)도 FX/PET으로 부른다.
                //   그래서 "여기서는 아무것도 그리지 않는다"를 <b>명시한다</b>.
                case EquipmentSlot.Fx:
                case EquipmentSlot.Pet:
                    break;

                default:
                    ShapeCoverageGuard.ReportUnknownSlot(slot);
                    break;
            }
        }

        // ==================== 빠진 도형 표식 (2026-09-02) ====================

        /// <summary>표식 네모의 반폭·반높이(머리 반경 배수). 관자놀이 폭(0.85R)보다 작게 잡아
        /// <b>어느 자리가 비었는지</b>는 보이되 캐릭터 실루엣 자체를 삼키지는 않게 한다.</summary>
        internal const float MissingMarkerHalfRatio = 0.55f;

        /// <summary>표식이 <b>머리에서</b> 떠 있는 높이(HEAD/HAIR 전용). 정수리(1.0R) 위로 올려
        /// 머리 링과 겹치지 않게 한다 — 겹치면 "머리가 깨졌다"로 오해된다.</summary>
        internal const float MissingMarkerHeadRiseRatio = 1.55f;

        /// <summary>
        /// ★ "이 자리에 있어야 할 도형이 없다"를 <b>화면에서</b> 알리는 폴백 표식(빗금 친 네모).
        ///
        /// <para>로그(<see cref="ShapeCoverageGuard"/>)만으로는 부족하다 — 이 결함의 첫 증상이
        /// "화면에서 안 보인다"이고, 화면을 보는 사람(디자이너·페르소나 검증단)은 콘솔을 안 본다.
        /// 반대로 출하된 앱의 사용자 캐릭터에 이 네모가 24시간 붙어 있으면 결함보다 나쁘므로,
        /// <see cref="ShapeCoverageGuard.ShowVisibleFallback"/>(개발 게이트)가 열렸을 때만 그린다.</para>
        ///
        /// <para>좌표는 전부 머리 반경 R의 배수다(이 파일의 규약) — 배율이 바뀌어도 표식만
        /// 뒤에 남지 않는다. 자리마다 <b>다른 높이</b>에 놓아 "어느 카테고리가 비었는지"를
        /// 로그 없이도 읽을 수 있게 한다.</para>
        /// </summary>
        internal static void AppendMissingMarker(List<Shape> sink, EquipmentSlot slot, int item, in Rig rig)
        {
            ShapeCoverageGuard.ReportMissingItemShape(slot, item);
            if (!ShapeCoverageGuard.ShowVisibleFallback || sink == null) return;

            float r = rig.HeadRadius;
            float half = r * MissingMarkerHalfRatio;
            float cy = MissingMarkerLocalY(slot, rig);

            sink.Add(new Shape("MissingBox", new[]
            {
                rig.F(-half, cy - half),
                rig.F(-half, cy + half),
                rig.F(half, cy + half),
                rig.F(half, cy - half),
            }, true, SortHead));

            // 빗금 — 네모만 있으면 "새 아이템의 도형"으로 오독될 수 있다. 빗금이 "지워졌다"를 말한다.
            sink.Add(new Shape("MissingSlash", new[]
            {
                rig.F(-half, cy - half),
                rig.F(half, cy + half),
            }, false, SortHead, tone: Accent));
        }

        /// <summary>표식을 놓을 로컬 Y. 그 자리의 <b>진짜 도형이 걸리는 기준선</b>을 그대로 쓴다 —
        /// 표식이 엉뚱한 데 뜨면 어느 카테고리가 비었는지 읽을 수 없다.</summary>
        internal static float MissingMarkerLocalY(EquipmentSlot slot, in Rig rig)
        {
            switch (slot)
            {
                case EquipmentSlot.Head:
                case EquipmentSlot.Hair:
                    return rig.HeadCenterY + rig.HeadRadius * MissingMarkerHeadRiseRatio;
                case EquipmentSlot.Eyes:
                    return GlassesLocalY(rig);
                case EquipmentSlot.Neck:
                    return NeckLocalY(rig);
                case EquipmentSlot.Shoulders:
                    return CapeCollarLocalY(rig) - rig.HeadRadius * MissingMarkerHalfRatio;
                default:
                    // 자리 자체를 모르면 놓을 기준선도 모른다 — 머리 위(가장 잘 보이는 곳)에 띄운다.
                    return rig.HeadCenterY + rig.HeadRadius * MissingMarkerHeadRiseRatio;
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
                    // 관 밑변 = 접힌 단의 윗변 = 커버선. 세 사실이 <b>같은 두 점</b>이다.
                    float cuffTop = hc + r * BeanieBandTopRatio;
                    float cuffHalf = r * BeanieBandHalfWidthRatio;
                    Vector3 cuffBack = rig.F(-cuffHalf, cuffTop);
                    Vector3 cuffFront = rig.F(cuffHalf, cuffTop);
                    float crownTop = cuffTop + r * BeanieCrownHeightRatio;
                    float crownHalf = r * BeanieCrownHalfWidthRatio;

                    float hemY = hc + r * BeanieBandBottomRatio;
                    float hemHalf = r * BeanieCuffBottomHalfWidthRatio;

                    // 관 + 접힌 단이 <b>한 채움</b>이다. 감쌈(|x| ≥ 0.85R · y ≤ 0.05R)은 커버선의
                    // 두 점(±0.96R, −0.06R)이 그대로 만든다 — 단을 선으로 바꿔도 안 사라진다.
                    sink.Add(new Shape("BeanieCrown", new[]
                    {
                        rig.F(-hemHalf, hemY),
                        cuffBack,
                        rig.F(-crownHalf, hc + r * 0.52f),
                        rig.F(-r * 0.62f, hc + r * 1.16f),
                        rig.F(0f, crownTop),
                        rig.F(r * 0.62f, hc + r * 1.14f),
                        rig.F(crownHalf, hc + r * 0.50f),
                        cuffFront,
                        rig.F(hemHalf, hemY),
                    }, true, SortHead, filled: true));

                    // 접힌 자리 = <b>낱선 하나</b>. 관의 허리 두 점을 그대로 받는다(좌표를 새로 적지
                    // 않는다 — 규칙 4-a). tone은 <b>반드시</b> Shade다: 0이면 관 채움색과 같아져
                    // 화면에서 통째로 사라진다.
                    sink.Add(new Shape("BeanieCuff", new[] { cuffBack, cuffFront },
                        false, SortHead, tone: Shade));

                    sink.Add(new Shape("BeaniePom",
                        Polygon(rig, -r * BeaniePomBackShiftRatio, crownTop + r * BeaniePomOffsetRatio,
                            r * BeaniePomRadiusRatio, BeaniePomSegments, BeaniePomStartDegrees),
                        true, SortHead, tone: Accent, filled: true));
                    break;
                }

                case HeadFedora:
                {
                    float brimY = hc + r * FedoraBrimLineRatio;
                    float crownHalf = r * FedoraCrownHalfWidthRatio;

                    // 관 밑변의 두 끝점을 <b>한 번만</b> 만들어 챙·관·띠 셋이 나눠 쓴다.
                    // 챙이 앞뒤로 기울어 있으므로 두 발의 y가 커버선을 사이에 두고 갈린다.
                    Vector3 crownBackFoot = rig.F(-crownHalf, brimY + r * 0.02f);
                    Vector3 crownFrontFoot = rig.F(crownHalf, brimY - r * 0.02f);

                    // 챙은 양쪽 다 뻗지만 앞이 더 길어 방향이 읽힌다.
                    // ★ 2026-09-02 7점 -> 8점. 부피를 머리 원 <b>밖</b>(x = +1.44R, 두께 0.614R)으로
                    //    옮기고 얼굴 앞 구간은 0.330R짜리 얇은 판으로 눕혔다 — 챙은 원반이라 옆에서
                    //    보면 그 순서가 맞다. 머리 원반에 얹는 색 0.902 R²(68%) -> 0.655 R²(52%),
                    //    남는 머리 0.88획 -> 1.25획(배율 0.60). ρ_max 0.2673R = 1.22획(규칙 1-C).
                    //    감쌈(|x| ≥ 0.85R · y ≤ 0.05R)은 x = ±0.94R의 두 점이 머리 <b>안</b>에서 만든다.
                    sink.Add(new Shape("FedoraBrim", new[]
                    {
                        rig.F(-r * FedoraBrimBackRatio, hc + r * 0.16f),
                        crownBackFoot,
                        crownFrontFoot,
                        rig.F(r * FedoraBrimFrontRatio, hc + r * 0.28f),
                        rig.F(r * 1.44f, hc - r * 0.46f),    // ★ 부피는 여기(머리 밖)
                        rig.F(r * 0.94f, hc - r * 0.24f),    // ★ 얼굴 앞 구간은 얇은 판
                        rig.F(-r * 0.94f, hc - r * 0.26f),   // 감쌈을 만드는 자리
                        rig.F(-r * 1.40f, hc - r * 0.22f),   // 뒤 챙 밑면
                    }, true, SortHead, filled: true));

                    float crownTop = brimY + r * FedoraCrownHeightRatio;
                    sink.Add(new Shape("FedoraCrown", new[]
                    {
                        crownBackFoot,
                        rig.F(-r * 0.92f, hc + r * 0.86f),
                        rig.F(-r * 0.42f, crownTop),
                        rig.F(r * 0.42f, crownTop - r * 0.02f),
                        rig.F(r * 0.92f, hc + r * 0.82f),
                        crownFrontFoot,
                    }, true, SortHead, filled: true));

                    // 띠 — 관 밑변 <b>그 자체</b>(간격 0). 규칙 4가 금지하는 것은 0 < 간격 < 1획이지
                    // 겹침이 아니다. 관 높이로는 1.5획을 띄우는 것이 산술적으로 불가능하다.
                    sink.Add(new Shape("FedoraBand", new[] { crownBackFoot, crownFrontFoot },
                        false, SortHead, tone: Accent));
                    break;
                }

                case HeadCrown:
                {
                    float baseY = hc + r * CrownBaseRatio;
                    float half = r * CrownHalfWidthRatio;
                    // 좌우 대칭이라 facing에 무관하게 같은 그림이 나온다(33-2-1 #4, 정상).
                    // <b>채운 닫힌 도형</b>이라 봉우리 세 개가 꼭짓점으로 수렴한다 — 선으로 그리면
                    // 둥근 캡이 끝을 뭉갠다(옛 왕관이 그 상태였다).
                    sink.Add(new Shape("CrownBody", new[]
                    {
                        rig.F(-half, baseY),
                        rig.F(-r * 0.88f, hc + r * 1.28f),
                        rig.F(-r * 0.46f, hc + r * 0.62f),
                        rig.F(0f, hc + r * 1.52f),
                        rig.F(r * 0.46f, hc + r * 0.62f),
                        rig.F(r * 0.88f, hc + r * 1.28f),
                        rig.F(half, baseY),
                        rig.F(r * 0.60f, hc - r * 0.10f),
                        rig.F(-r * 0.60f, hc - r * 0.10f),
                    }, true, SortHead, filled: true));

                    // 테는 몸의 밑변 네 점을 그대로 받는다(좌표를 새로 적지 않는다).
                    sink.Add(new Shape("CrownRim", new[]
                    {
                        rig.F(-half, baseY),
                        rig.F(-r * 0.60f, hc - r * 0.10f),
                        rig.F(r * 0.60f, hc - r * 0.10f),
                        rig.F(half, baseY),
                    }, false, SortHead, tone: Accent));
                    break;
                }

                case HeadBeret:
                {
                    // 뒤로 처진 비대칭 덩어리. 밴드도 꼭지도 없다 — 그 둘이 없는 것이 정체다.
                    float brimY = hc + r * BeretBrimLineRatio;
                    Vector3 backTip = rig.F(-r * BeretBackDroopRatio, brimY - r * BeretBackDroopDropRatio);
                    Vector3 frontFoot = rig.F(r * BeretFrontRatio, brimY);
                    Vector3 innerFoot = rig.F(-r * 0.30f, brimY - r * 0.04f);

                    sink.Add(new Shape("BeretBody", new[]
                    {
                        backTip,
                        rig.F(-r * 1.02f, hc + r * 0.62f),
                        rig.F(-r * 0.20f, brimY + r * BeretCrownHeightRatio),
                        rig.F(r * 0.62f, hc + r * 0.90f),
                        // ★ 2026-09-02 배율 0.60 실루엣 수정(스펙 12-3-b). y를 0.44 → 0.54로만 올린다.
                        //    아래 변(→ frontFoot)이 0.4243R이라 배율 0.60에서 0.99획 = 획보다 짧아
                        //    앞 어깨 모서리가 뭉갰다. 0.5235R = 1.22획. x·뒤쪽 처짐은 그대로 —
                        //    <b>테(BeretRim)는 [5][6][0]만 받으므로 이 점이 안 들어가 그림이 안 바뀐다.</b>
                        rig.F(r * BeretFrontShoulderRatio, hc + r * BeretFrontShoulderTopRatio),
                        frontFoot,
                        innerFoot,
                    }, true, SortHead, filled: true));

                    // 테 = 밑변 그 자체(간격 0). 관 높이로 1.5획을 띄우면 테가 관을 가로질러
                    // <b>띠 두른 정모</b>가 된다 — 페르소나가 실물에서 본 그 그림이다.
                    sink.Add(new Shape("BeretRim", new[] { frontFoot, innerFoot, backTip },
                        false, SortHead, tone: Accent));
                    break;
                }

                case HeadStraw:
                {
                    float brimY = hc + r * StrawBrimLineRatio;
                    float crownHalf = r * StrawCrownHalfWidthRatio;

                    // 중절모와 같은 규약 — 관 밑변 두 끝점을 챙·관·띠가 공유한다.
                    Vector3 crownBackFoot = rig.F(-crownHalf, brimY + r * 0.02f);
                    Vector3 crownFrontFoot = rig.F(crownHalf, brimY);

                    // ★ 2026-09-02 7점 -> 8점. 중절모와 같은 처방(부피를 머리 밖으로).
                    //    머리 원반에 얹는 색 0.790 R²(60%) -> 0.677 R²(48%), 남는 머리 1.03획 -> 1.25획.
                    //    ρ_max 0.2726R = 1.25획. 도달 거리(StrawBrim*Ratio)는 아이템 정체를 만드는
                    //    값이라 무변경 — 중절모와의 실루엣 차가 거기서 나온다.
                    sink.Add(new Shape("StrawBrim", new[]
                    {
                        rig.F(-r * StrawBrimBackRatio, hc + r * 0.16f),
                        crownBackFoot,
                        crownFrontFoot,
                        rig.F(r * StrawBrimFrontRatio, hc + r * 0.30f),
                        rig.F(r * 1.56f, hc - r * 0.40f),    // ★ 부피는 여기(머리 밖)
                        rig.F(r * 0.92f, hc - r * 0.24f),    // ★ 얼굴 앞 구간은 얇은 판
                        rig.F(-r * 0.92f, hc - r * 0.26f),   // 감쌈을 만드는 자리
                        rig.F(-r * 1.52f, hc - r * 0.20f),   // 뒤 챙 밑면
                    }, true, SortHead, filled: true));

                    sink.Add(new Shape("StrawCrown", new[]
                    {
                        crownBackFoot,
                        rig.F(-r * 0.74f, hc + r * 0.92f),
                        rig.F(0f, brimY + r * StrawCrownHeightRatio),
                        rig.F(r * 0.74f, hc + r * 0.90f),
                        crownFrontFoot,
                    }, true, SortHead, filled: true));

                    sink.Add(new Shape("StrawBand", new[] { crownBackFoot, crownFrontFoot },
                        false, SortHead, tone: Accent));
                    break;
                }

                default:
                    AppendMissingMarker(sink, EquipmentSlot.Head, item, rig);
                    break;
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
                    // 성립하는 유일한 구성이다.
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
                    float lensY = cy + r * RoundLensCenterRiseRatio;
                    Vector3[] back = Polygon(rig, -dx, lensY, rad, RoundLensSegments);
                    Vector3[] front = Polygon(rig, dx, lensY, rad, RoundLensSegments);
                    sink.Add(new Shape("RoundLensBack", back, true, SortEyes, filled: true));
                    sink.Add(new Shape("RoundLensFront", front, true, SortEyes, filled: true));
                    // 30도(뒤 렌즈 index 1) / 150도(앞 렌즈 index 5) = 두 렌즈의 <b>안쪽 위</b> 꼭짓점.
                    sink.Add(new Shape("RoundBridge", new[]
                    {
                        back[1], rig.F(0f, cy + r * RoundBridgeRiseRatio), front[5],
                    }, false, SortEyes, tone: Accent));
                    break;
                }

                case EyesGoggles:
                {
                    float strap = r * GoggleStrapReachRatio;

                    // 렌즈의 윗변 두 끝점을 <b>한 번만</b> 만들어 스트랩이 되돌아올 때 그대로 받는다.
                    // 두 채움이 한 점도 겹치지 않는다 — EYES 채움은 전부 같은 레이어(SortEyes−1)라
                    // 겹치면 그리기 순서가 미정이 되고, 그때 어느 판이 위로 올지 보장할 수 없다.
                    Vector3 lensTopBack = rig.F(-r * 0.66f, cy + r * 0.16f);
                    Vector3 lensTopFront = rig.F(r * 0.66f, cy + r * 0.16f);
                    Vector3 lensCornerBack = rig.F(-r * 1.04f, cy - r * 0.06f);
                    Vector3 lensCornerFront = rig.F(r * 1.04f, cy - r * 0.06f);

                    sink.Add(new Shape("GoggleStrap", new[]
                    {
                        rig.F(-strap, cy - r * 0.24f),
                        rig.F(-strap, cy + r * 0.22f),
                        rig.F(-r * 1.06f, cy + r * 0.40f),
                        rig.F(-r * 0.66f, cy + r * 0.62f),
                        rig.F(r * 0.66f, cy + r * 0.62f),
                        rig.F(r * 1.06f, cy + r * 0.40f),
                        rig.F(strap, cy + r * 0.22f),
                        rig.F(strap, cy - r * 0.24f),
                        lensCornerFront, lensTopFront, lensTopBack, lensCornerBack,
                    }, true, SortEyes, tone: Accent, filled: true));

                    sink.Add(new Shape("GoggleLens", new[]
                    {
                        lensTopBack, lensTopFront, lensCornerFront,
                        rig.F(r * 0.84f, cy - r * 0.50f),
                        rig.F(r * 0.20f, cy - r * 0.62f),
                        rig.F(-r * 0.20f, cy - r * 0.62f),
                        rig.F(-r * 0.84f, cy - r * 0.50f),
                        lensCornerBack,
                    }, true, SortEyes, filled: true));
                    break;
                }

                case EyesMonocle:
                {
                    // 앞쪽 눈에만 알이 있고, <b>가려지지 않은 뒤쪽 눈이 드러난다</b>(보조색).
                    // 체인은 주색이다 — 알과 체인은 같은 금속이고, 보조색 정원 1개는 눈이 가져간다.
                    Vector3[] pod = Polygon(rig, r * MonocleOffsetRatio, cy + r * RoundLensCenterRiseRatio,
                        r * MonocleRadiusRatio, RoundLensSegments);
                    sink.Add(new Shape("MonoclePod", pod, true, SortEyes, filled: true));
                    sink.Add(new Shape("MonocleChain", new[]
                    {
                        pod[9],                                   // 270도 = 알의 최하점(간격 0)
                        rig.F(r * 0.44f, cy - r * 0.76f),
                        rig.F(r * 0.76f, cy - r * 1.16f),
                    }, false, SortEyes));
                    sink.Add(new Shape("MonocleEye", DrawnEye(rig, -1f), true, SortEyes,
                        tone: Accent, filled: true));
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
                        rig.F(r * BrowlineBarInnerRatio, cy + r * (BrowlineBarTopRatio - 0.02f)),
                        rig.F(r * BrowlineBarOuterRatio, barBottom - r * 0.02f),
                        rig.F(r * BrowlineLensInnerRatio, barBottom - r * 0.04f),
                        rig.F(-r * BrowlineLensInnerRatio, barBottom - r * 0.02f),
                    }, true, SortEyes, tone: Accent, filled: true));

                    sink.Add(new Shape("BrowlineLensBack", BrowlineLens(rig, forward: false),
                        true, SortEyes, filled: true));
                    sink.Add(new Shape("BrowlineLensFront", BrowlineLens(rig, forward: true),
                        true, SortEyes, filled: true));
                    break;
                }

                case EyesPatch:
                {
                    // 외알안경과 같은 규약 — 앞쪽 눈만 천으로 덮고 뒤쪽 눈이 드러난다.
                    float phw = r * PatchHalfWidthRatio;
                    float phh = r * PatchHalfHeightRatio;
                    float pcx = r * PatchCenterRatio;
                    Vector3 coverTopBack = rig.F(pcx - phw, cy + phh);
                    Vector3 coverBottomBack = rig.F(pcx - phw + r * 0.04f, cy - phh * 0.82f);
                    sink.Add(new Shape("PatchCover", new[]
                    {
                        coverTopBack,
                        rig.F(pcx + phw, cy + phh * 0.82f),
                        rig.F(pcx + phw - r * 0.06f, cy - phh),
                        coverBottomBack,
                    }, true, SortEyes, filled: true));

                    // 끈은 천의 <b>뒤쪽 두 꼭짓점에서 출발</b>해 머리를 돌아 넘어간다. 끝점은 머리
                    // 원 밖(1.02R)이라 허공에서 끊기지 않고, 드러난 눈과는 1.5획 넘게 떨어진다.
                    sink.Add(new Shape("PatchStrap", new[]
                    {
                        HeadPolar(rig, PatchStrapDegrees, PatchStrapReachRatio),
                        coverTopBack, coverBottomBack,
                        HeadPolar(rig, 360f - PatchStrapDegrees, PatchStrapReachRatio),
                    }, false, SortEyes));

                    sink.Add(new Shape("PatchEye", DrawnEye(rig, -1f), true, SortEyes,
                        tone: Accent, filled: true));
                    break;
                }

                default:
                    AppendMissingMarker(sink, EquipmentSlot.Eyes, item, rig);
                    break;
            }
        }

        /// <summary>뿔테 렌즈 한 장 — 테 밑변에서 시작해 아래로 좁아진다.
        /// 선글라스와 같은 이유로 두 장의 회전 방향을 맞춰 둔다(채움 분할 안정성).</summary>
        internal static Vector3[] BrowlineLens(in Rig rig, bool forward)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            float top = cy + r * BrowlineBarBottomRatio;
            return forward
                ? new[]
                {
                    rig.F(r * BrowlineLensInnerRatio, top - r * 0.04f),
                    rig.F(r * 0.44f, cy - r * 0.52f),
                    rig.F(r * 1.00f, cy - r * 0.34f),
                    rig.F(r * BrowlineBarOuterRatio, top - r * 0.02f),
                }
                : new[]
                {
                    rig.F(-r * BrowlineLensInnerRatio, top - r * 0.02f),
                    rig.F(-r * BrowlineBarOuterRatio, top),
                    rig.F(-r * 0.98f, cy - r * 0.32f),
                    rig.F(-r * 0.44f, cy - r * 0.50f),
                };
        }


        // ==================== NECK (넥타이) ====================

        private static void AppendNeck(List<Shape> sink, int item, in Rig rig, bool mondayLoosened)
        {
            float r = rig.HeadRadius;
            float ty = NeckLocalY(rig);

            switch (item)
            {
                case NeckBowTie:
                    // 날개도 매듭도 <b>채운다</b>. 옛 나비넥타이는 셋 다 열린 선이라 화면에서
                    // "리본"이 아니라 "가는 획 몇 개"였다.
                    sink.Add(new Shape("BowTieLeftWing", BowTieLeftWing(rig), true, SortNeck, filled: true));
                    sink.Add(new Shape("BowTieRightWing", BowTieRightWing(rig), true, SortNeck, filled: true));
                    sink.Add(new Shape("BowTieKnot", BowTieKnot(rig), true, SortNeck,
                        tone: Accent, filled: true));
                    break;

                case NeckStriped:
                {
                    // 33-2-5 (D) — 월요일에는 매듭이 R·0.12 내려가고 blade가 3도 기운다.
                    float knotY = mondayLoosened ? ty - r * TieMondayLoosenDropRatio : ty;
                    float tilt = mondayLoosened ? TieMondayLoosenTiltDegrees * Mathf.Deg2Rad : 0f;
                    float kw = r * TieKnotHalfWidthRatio;
                    float kh = r * TieKnotHalfHeightRatio;
                    float kb = r * TieKnotBottomDropRatio;

                    sink.Add(new Shape("TieKnot", new[]
                    {
                        rig.F(-kw, knotY + kh),
                        rig.F(kw, knotY + kh),
                        rig.F(kw * 0.722f, knotY - kb),
                        rig.F(-kw * 0.722f, knotY - kb),
                    }, true, SortNeck, filled: true));

                    float pivotY = knotY - kb;
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
                    // 남기고 두 어깨점을 고정하면 V가 찢어져 보인다 — 세 점을 함께 흔든다.
                    sink.Add(new Shape("TieBlade", new[]
                    {
                        Blade(-bw, 0f),
                        Blade(bw, 0f),
                        Blade(bw * 1.176f, -len * 0.72f),
                        Blade(0f, -len),
                        Blade(-bw * 1.176f, -len * 0.72f),
                    }, true, SortNeck, filled: true, swayStart: 2, swayCount: 3));

                    // ★ 줄무늬는 <b>하나</b>다. 옛 도형은 열린 선 2개였고 각각 잉크 사각형이 0.87획이라
                    // 둘 다 화면에 존재하지 않았다(규칙 1 면제 대장의 나머지 두 줄). 지금은 blade를
                    // 가로지르는 <b>채운 띠</b> 하나 — 보조색 정원도 2개 -> 1개가 되어 규칙 3-2를 지킨다.
                    sink.Add(new Shape("TieStripe", new[]
                    {
                        Blade(-bw * 1.06f, -len * 0.30f),
                        Blade(bw * 1.06f, -len * 0.30f - r * 0.20f),
                        Blade(bw * 1.12f, -len * 0.52f - r * 0.20f),
                        Blade(-bw * 1.12f, -len * 0.52f),
                    }, true, SortNeck, tone: Accent, filled: true));
                    break;
                }

                case NeckScarf:
                {
                    // ★ 자락을 <b>먼저</b>, 고리를 <b>나중에</b> 넣는다. 같은 채움 레이어에서는 나중에
                    // 넣은 것이 위로 오고, 목도리는 고리가 자락을 덮어야 "감았다"로 읽힌다.
                    // (옛 순서는 반대라 자락이 고리 위로 떴다 — 미보고 결함.)
                    float back = rig.TorsoLength * ScarfBackTailLengthInTorso;
                    sink.Add(new Shape("ScarfTailBack", new[]
                    {
                        rig.F(-r * 0.58f, ty - r * 0.32f),
                        rig.F(-r * 0.06f, ty - r * 0.48f),
                        rig.F(-r * 0.48f, ty - back),
                        rig.F(-r * 1.04f, ty - back + rig.TorsoLength * 0.07f),
                    }, true, SortNeck, swayStart: 2, swayCount: 2, filled: true));

                    float front = rig.TorsoLength * ScarfFrontTailLengthInTorso;
                    sink.Add(new Shape("ScarfTailFront", new[]
                    {
                        rig.F(r * 0.06f, ty - r * 0.48f),
                        rig.F(r * 0.54f, ty - r * 0.34f),
                        rig.F(r * 1.12f, ty - front),
                        rig.F(r * 0.58f, ty - front - rig.TorsoLength * 0.07f),
                    }, true, SortNeck, swayStart: 2, swayCount: 2, filled: true));

                    float wh = r * ScarfWrapHalfWidthRatio;
                    sink.Add(new Shape("ScarfWrap", new[]
                    {
                        rig.F(-wh, ty + r * ScarfWrapTopRatio),
                        rig.F(0f, ty + r * ScarfWrapCenterTopRatio),
                        rig.F(wh, ty + r * ScarfWrapTopRatio),
                        rig.F(wh + r * 0.04f, ty + r * ScarfWrapSideRatio),
                        rig.F(0f, ty + r * ScarfWrapDipRatio),
                        rig.F(-wh - r * 0.04f, ty + r * ScarfWrapSideRatio),
                    }, true, SortNeck, tone: Accent, filled: true));
                    break;
                }

                case NeckBell:
                {
                    sink.Add(new Shape("Collar", CollarCurve(rig, ty), false, SortNeck));

                    // 위 꼭짓점이 <b>목줄 최저점 그 자리</b>다 — 펜던트와 똑같은 유도(규칙 4-a).
                    // 첫 점을 90도에서 시작하는 이유: 10각형을 위상 0도로 두면 가장 높은 꼭짓점이
                    // 72도에 놓여 매단 자리가 0.11획 어긋난다(옛 방울이 정확히 그 상태였다).
                    float bellR = r * BellRadiusRatio;
                    float bellY = CollarLowLocalY(rig, ty) - bellR;
                    sink.Add(new Shape("Bell", Polygon(rig, 0f, bellY, bellR, BellSegments, 90f),
                        true, SortNeck, swayStart: 0, swayCount: BellSegments, tone: Accent, filled: true));
                    break;
                }

                case NeckPendant:
                {
                    // 방울 목걸이의 목줄을 <b>그대로</b> 쓰고(같은 부착선·같은 곡률) 매달린 것만 바꾼다.
                    sink.Add(new Shape("Chain", CollarCurve(rig, ty), false, SortNeck));

                    float hangY = CollarLowLocalY(rig, ty);
                    float phw = r * PendantHalfWidthRatio;
                    float phh = r * PendantHalfHeightRatio;
                    float py = hangY - phh;
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
                    // 목도리 고리의 납작 변주 + 앞으로 늘어진 <b>채운 삼각</b> 자락 하나.
                    float wrapY = ty + r * BandanaWrapRiseRatio;
                    float wh = r * BandanaWrapHalfWidthRatio;
                    float wv = r * BandanaWrapHalfHeightRatio;
                    sink.Add(new Shape("BandanaWrap", new[]
                    {
                        rig.F(-wh, wrapY + wv),
                        rig.F(0f, wrapY + wv * 0.4545f),
                        rig.F(wh, wrapY + wv),
                        rig.F(wh, wrapY - wv),
                        rig.F(0f, wrapY - wv * 1.909f),
                        rig.F(-wh, wrapY - wv),
                    }, true, SortNeck, filled: true));

                    float thw = r * BandanaTailHalfWidthRatio;
                    float baseY = wrapY - r * 0.30f;
                    // 끝 꼭짓점(인덱스 2) 하나만 흔든다 — 밑변 두 점은 매듭이라 고정이다.
                    sink.Add(new Shape("BandanaTail", new[]
                    {
                        rig.F(r * 0.04f, baseY),
                        rig.F(r * 0.04f + thw * 2f, baseY),
                        rig.F(r * 0.04f + thw * 0.75f, baseY - rig.TorsoLength * BandanaTailLengthRatio),
                    }, true, SortNeck, swayStart: 2, swayCount: 1, tone: Accent, filled: true));
                    break;
                }

                default:
                    AppendMissingMarker(sink, EquipmentSlot.Neck, item, rig);
                    break;
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
                    sink.Add(new Shape("CapeCollar", CapeCollarBand(rig), true, SortBack,
                        tone: Accent, filled: true));
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
                    sink.Add(new Shape("CapeCollar", CapeCollarBand(rig), true, SortBack,
                        tone: Accent, filled: true));
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
                    Vector3 packStrapAnchor = rig.F(cx + hw, cyp + hh * 0.46f);

                    sink.Add(new Shape("PackBody", new[]
                    {
                        rig.F(cx - hw, cyp - hh * 0.60f),
                        rig.F(cx - hw, cyp + hh * 0.46f),
                        rig.F(cx - hw * 0.62f, cyp + hh * 0.86f),
                        rig.F(cx + hw * 0.62f, cyp + hh * 0.86f),
                        packStrapAnchor,
                        rig.F(cx + hw, cyp - hh * 0.60f),
                        rig.F(cx + hw * 0.60f, cyp - hh),
                        rig.F(cx - hw * 0.60f, cyp - hh),
                    }, true, SortBack, filled: true));

                    // 뚜껑 — 몸과 <b>같은 주색</b>이라 겹쳐도 그리기 순서가 무관하다(같은 레이어 채움의
                    // 순서 미정 함정을 색으로 피한다). 옛 'PackFlap'(선 1개)은 여기서 지웠다:
                    // 선 하나는 뚜껑이 아니라 상자에 그은 금이고, 규칙 2가 요구하는 덩어리가 아니다.
                    sink.Add(new Shape("PackLid", new[]
                    {
                        rig.F(cx - hw * 0.96f, cyp + hh * 0.40f),
                        rig.F(cx - hw * 0.66f, cyp + hh * 0.92f),
                        rig.F(cx + hw * 0.66f, cyp + hh * 0.92f),
                        rig.F(cx + hw * 0.96f, cyp + hh * 0.40f),
                        rig.F(cx + hw * 0.80f, cyp + hh * 0.06f),
                        rig.F(cx - hw * 0.80f, cyp + hh * 0.06f),
                    }, true, SortBack, filled: true));

                    // 서명 디테일 하나 — 보조색 버클(규칙 3-2).
                    float bhw = r * PackBuckleHalfWidthRatio;
                    sink.Add(new Shape("PackBuckle", new[]
                    {
                        rig.F(cx - bhw, cyp + hh * 0.10f),
                        rig.F(cx + bhw, cyp + hh * 0.10f),
                        rig.F(cx + bhw, cyp - hh * 0.30f),
                        rig.F(cx - bhw, cyp - hh * 0.30f),
                    }, true, SortBack, tone: Accent, filled: true));

                    // 어깨끈은 <b>하나</b>다. 측면도에서 두 번째 끈은 몸통 선에 가려 보이지 않으므로
                    // 정원(2~4)만 먹는다 — 37-6 규칙 5. 주색인 것은 보조색 정원을 버클이 가져가서다.
                    sink.Add(new Shape("PackStrap", new[]
                    {
                        rig.F(r * 0.22f, sy + r * 0.04f),
                        rig.F(-r * 0.10f, sy - rig.TorsoLength * 0.14f),
                        packStrapAnchor,
                    }, false, SortBack));
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
                    sink.Add(new Shape("CapeCollar", CapeCollarBand(rig), true, SortBack,
                        tone: Accent, filled: true));
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

                default:
                    AppendMissingMarker(sink, EquipmentSlot.Shoulders, item, rig);
                    break;
            }
        }

        // ★ 2026-08-30 FACE(표정) 카테고리 삭제 — 사용자 결정("표정관련은 전부삭제 어차피 구별이
        //   안됨"). 눈/입 도형(Smile/Lid)과 AppendFace가 함께 사라졌다. 눈동자 점 2개는 원래부터
        //   States/EyeController.cs의 단독 소유라 이 삭제와 무관하게 그대로 커서를 따라간다.

        // ==================== HAIR (머리) ====================
        //
        // 아래 좌표는 전부 <b>머리 중심 원점 · R 배수 · +x 진행 방향</b>이다
        // (docs/EQUIPMENT_SHAPE_SPEC.md 1절의 규약, 부록 A와 같은 단위).
        // 로컬 좌표(원점 = 발바닥)로 옮기는 곳은 <see cref="HairToLocal"/> 한 곳뿐이다 —
        // 곳곳에서 rig.HeadCenterY를 더하면 한 곳만 빠뜨리는 사고가 난다.

        /// <summary>커튼 좌표는 <b>배열 하나</b>로만 존재한다. 도형을 굽는 경로는 착용/방향/색이
        /// 바뀐 프레임에만 도므로 정적 재사용으로 충분하다(매 프레임 할당 금지 규약).</summary>
        private static readonly List<Vector2> _hairPath = new List<Vector2>(48);

        // ---- 0 삐친머리. (각도, 반경) 쌍 — 봉우리와 골이 번갈아 도는 <b>실루엣 그 자체</b>가 정체다.
        private static readonly Vector2[] CowlickSpikesPolar =
        {
            new Vector2(6f, 1.28f), new Vector2(24f, 1.70f), new Vector2(44f, 1.30f),
            new Vector2(66f, 1.76f), new Vector2(90f, 1.32f), new Vector2(114f, 1.78f),
            new Vector2(138f, 1.34f), new Vector2(160f, 1.72f), new Vector2(184f, 1.30f),
            new Vector2(204f, 1.56f),
        };
        private static readonly Vector2[] CowlickBackCurtain =
            { new Vector2(-1.30f, -1.10f), new Vector2(-0.78f, -0.76f) };
        private static readonly Vector2[] CowlickFrontCurtain =
            { new Vector2(0.86f, 0.04f), new Vector2(1.12f, -0.72f) };

        // ---- 1 단정한머리. 뒤 커튼이 −2.12R까지 내려간다(6종 중 유일하게 어깨 근처까지 닿는다).
        private static readonly Vector2[] NeatBackCurtain =
        {
            new Vector2(-1.46f, -0.92f), new Vector2(-1.10f, -2.12f), new Vector2(-0.62f, -1.26f),
        };
        private static readonly Vector2[] NeatFrontCurtain =
        {
            new Vector2(0.80f, 0.10f), new Vector2(1.06f, -0.62f), new Vector2(1.28f, -1.30f),
        };
        private static readonly Vector2[] NeatPart =
        {
            new Vector2(-0.14f, 1.56f), new Vector2(0.26f, 1.54f),
            new Vector2(0.44f, 0.60f), new Vector2(0.04f, 0.64f),
        };

        // ---- 2 곱슬머리. 커튼 x가 굽이마다 <see cref="CurlAmplitudeRatio"/> 이상 벌어진다.
        private static readonly Vector2[] CurlBackCurtain =
        {
            new Vector2(-1.90f, -0.50f), new Vector2(-1.36f, -0.94f), new Vector2(-1.82f, -1.42f),
            new Vector2(-1.18f, -1.74f), new Vector2(-0.86f, -0.98f),
        };
        private static readonly Vector2[] CurlFrontCurtain =
        {
            new Vector2(0.84f, 0.16f), new Vector2(1.44f, -0.30f),
            new Vector2(1.06f, -0.86f), new Vector2(1.54f, -1.34f),
        };
        private static readonly Vector2[] CurlCoil =
        {
            new Vector2(1.54f, -1.34f), new Vector2(1.94f, -1.68f), new Vector2(1.50f, -2.10f),
            new Vector2(1.00f, -1.80f), new Vector2(1.20f, -1.54f),
        };

        // ---- 5 포니테일.
        private static readonly Vector2[] PonytailBackCurtain =
            { new Vector2(-1.34f, -0.84f), new Vector2(-0.82f, -0.62f) };
        private static readonly Vector2[] PonytailFrontCurtain =
            { new Vector2(0.82f, 0.06f), new Vector2(1.10f, -0.72f) };

        /// <summary>머리 중심 원점·R 배수로 쌓은 점열 -> 로컬 좌표(원점 = 발바닥).</summary>
        private static Vector3[] HairToLocal(in Rig rig, List<Vector2> pathInR)
        {
            var result = new Vector3[pathInR.Count];
            for (int i = 0; i < pathInR.Count; i++)
            {
                Vector2 p = pathInR[i];
                result[i] = rig.F(p.x * rig.HeadRadius, rig.HeadCenterY + p.y * rig.HeadRadius);
            }
            return result;
        }

        /// <summary>같은 단위의 <b>낱개 점열</b>(커튼·삐침·묶음) -> 로컬 좌표.</summary>
        private static Vector3[] HairToLocal(in Rig rig, Vector2[] ptsInR)
        {
            var result = new Vector3[ptsInR.Length];
            for (int i = 0; i < ptsInR.Length; i++)
            {
                result[i] = rig.F(ptsInR[i].x * rig.HeadRadius,
                    rig.HeadCenterY + ptsInR[i].y * rig.HeadRadius);
            }
            return result;
        }

        private static void PathArc(List<Vector2> path, float fromDegrees, float toDegrees,
            float radiusRatio, int segments)
        {
            int n = Mathf.Max(1, segments);
            for (int i = 0; i <= n; i++)
            {
                float rad = Mathf.Lerp(fromDegrees, toDegrees, i / (float)n) * Mathf.Deg2Rad;
                path.Add(new Vector2(Mathf.Cos(rad) * radiusRatio, Mathf.Sin(rad) * radiusRatio));
            }
        }

        private static void PathPoints(List<Vector2> path, Vector2[] pts)
        {
            for (int i = 0; i < pts.Length; i++) path.Add(pts[i]);
        }

        /// <summary>
        /// 머리카락 덩어리 — <b>돔 + 뒤 커튼 + 두피 안쪽 호 + 앞 커튼</b>으로 닫히는 채움 도형.
        /// <para>6종이 <b>같은 순서</b>로 경계를 돈다. 순서가 어긋나면 폴리곤이 자기교차하고
        /// 귀 자르기(<see cref="Triangulate"/>)가 깨져 천이 찢어진 것처럼 삼각형이 튄다.</para>
        /// <para>얼굴(파인 공간)은 두피 안쪽 호가 끝나는 <b>앞·아래</b> 사분면이다 — 그래서 눈동자가
        /// 되살아나도(<c>BakeEyes</c>) 머리카락 채움에 덮이지 않는다.</para>
        /// </summary>
        /// <param name="capRatio">돔 반경. <see cref="HairCapMinRatio"/> ~ <see cref="HairCapMaxRatio"/>.</param>
        internal static Vector3[] HairMass(in Rig rig, float capRatio, float domeFromDegrees,
            float domeToDegrees, Vector2[] backCurtain, float innerFromDegrees, float innerToDegrees,
            Vector2[] frontCurtain)
        {
            _hairPath.Clear();
            PathArc(_hairPath, domeFromDegrees, domeToDegrees, capRatio, HairDomeSegments);
            PathPoints(_hairPath, backCurtain);
            PathArc(_hairPath, innerFromDegrees, innerToDegrees, HairInnerRadiusRatio, HairInnerArcSegments);
            PathPoints(_hairPath, frontCurtain);
            return HairToLocal(rig, _hairPath);
        }

        /// <summary>삐친머리 전용 덩어리 — 돔이 <b>반경 일정한 호가 아니라</b> 봉우리 5개다.
        /// 나머지 세 구간(커튼·안쪽 호·커튼)은 형제들과 완전히 같은 규약이다.</summary>
        internal static Vector3[] CowlickMass(in Rig rig)
        {
            _hairPath.Clear();
            for (int i = 0; i < CowlickSpikesPolar.Length; i++)
            {
                float rad = CowlickSpikesPolar[i].x * Mathf.Deg2Rad;
                float radius = CowlickSpikesPolar[i].y;
                _hairPath.Add(new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius));
            }
            PathPoints(_hairPath, CowlickBackCurtain);
            PathArc(_hairPath, 196f, 76f, HairInnerRadiusRatio, HairInnerArcSegments);
            PathPoints(_hairPath, CowlickFrontCurtain);
            return HairToLocal(rig, _hairPath);
        }

        /// <summary>
        /// 바가지머리 전용 덩어리 — <b>돔 + 수평으로 자른 밑선 셋</b>으로 닫는다.
        /// <para>형제들과 <b>도형의 종류 자체가</b> 다르다. 돔은 옆머리 밑변과 만나는 각도에서
        /// 시작/끝난다 — 각도를 손으로 적어 두면 반경이나 자른 높이를 바꿀 때 그 자리에 틈이 생기고,
        /// 틈은 획에 먹혀 이 빠진 실루엣이 된다.</para>
        /// </summary>
        internal static Vector3[] BowlSilhouette(in Rig rig)
        {
            float startDeg = Mathf.Asin(Mathf.Clamp(BowlCutLineRatio / BowlCapRadiusRatio, -1f, 1f))
                * Mathf.Rad2Deg;
            float endDeg = 180f - startDeg;

            _hairPath.Clear();
            PathArc(_hairPath, startDeg, endDeg, BowlCapRadiusRatio, BowlDomeSegments);
            _hairPath.Add(new Vector2(-BowlSideHalfWidthRatio, BowlCutLineRatio));
            for (int i = 0; i <= BowlFringeSegments; i++)
            {
                _hairPath.Add(new Vector2(Mathf.Lerp(-BowlSideHalfWidthRatio, BowlSideHalfWidthRatio,
                    i / (float)BowlFringeSegments), BowlFringeLineRatio));
            }
            _hairPath.Add(new Vector2(BowlSideHalfWidthRatio, BowlCutLineRatio));
            return HairToLocal(rig, _hairPath);
        }

        /// <summary>이마를 가로지르는 앞머리 선. <see cref="BowlSilhouette"/>의 안쪽 경계와
        /// <b>같은 식</b>을 쓴다 — 두 벌로 적어 두면 한쪽만 고쳐 선이 어긋난다.</summary>
        internal static Vector3[] BowlFringeLine(in Rig rig)
        {
            var pts = new Vector3[BowlFringeSegments + 1];
            for (int i = 0; i <= BowlFringeSegments; i++)
            {
                float x = Mathf.Lerp(-BowlSideHalfWidthRatio, BowlSideHalfWidthRatio,
                    i / (float)BowlFringeSegments);
                pts[i] = rig.F(x * rig.HeadRadius, rig.HeadCenterY + BowlFringeLineRatio * rig.HeadRadius);
            }
            return pts;
        }

        /// <summary>머리 중심을 도는 <b>고리 조각</b>(안쪽 반경 ~ 바깥 반경). 민머리의 남은 테두리.</summary>
        private static Vector3[] HeadRimBand(in Rig rig, float fromDegrees, float toDegrees,
            float innerRatio, float outerRatio, int segments)
        {
            _hairPath.Clear();
            PathArc(_hairPath, fromDegrees, toDegrees, outerRatio, segments);
            PathArc(_hairPath, toDegrees, fromDegrees, innerRatio, segments);
            return HairToLocal(rig, _hairPath);
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
                    _hairScratch.Add(new Shape("HairMass", CowlickMass(rig), true, SortHair, filled: true));
                    // 식별 특징 — 뒤로 솟은 삐침 하나. 끝(2.45R)이 봉우리들보다 확실히 더 나가야
                    // "하나가 유독 뻗쳤다"로 읽힌다(같은 높이면 여섯 번째 봉우리일 뿐이다).
                    _hairScratch.Add(new Shape("HairCrest", new[]
                    {
                        HeadPolar(rig, 126f, 1.34f),
                        HeadPolar(rig, 142f, 2.45f),
                        HeadPolar(rig, 154f, 1.26f),
                    }, true, SortHair, tone: Accent, filled: true));
                    break;

                case HairNeat:
                    _hairScratch.Add(new Shape("HairMass",
                        HairMass(rig, NeatCapRatio, 10f, 206f, NeatBackCurtain, 198f, 78f, NeatFrontCurtain),
                        true, SortHair, filled: true));
                    // 식별 특징 — 정수리에서 이마로 내려오는 가르마 가닥.
                    _hairScratch.Add(new Shape("HairPart", HairToLocal(rig, NeatPart),
                        true, SortHair, tone: Accent, filled: true));
                    break;

                case HairCurly:
                    _hairScratch.Add(new Shape("HairMass",
                        HairMass(rig, CurlCapRatio, 8f, 208f, CurlBackCurtain, 200f, 74f, CurlFrontCurtain),
                        true, SortHair, filled: true));
                    // 식별 특징 — 앞 커튼 <b>끝점 그 자리</b>에서 이어지는 컬 하나(간격 0 — 규칙 4).
                    _hairScratch.Add(new Shape("HairCoil", HairToLocal(rig, CurlCoil),
                        true, SortHair, tone: Accent, filled: true));
                    break;

                case HairBald:
                    // 실루엣이 <b>없는</b> 것이 이 아이템의 정체다. 그래도 아무것도 안 그리지는 않는다 —
                    // 착용했는데 화면이 그대로면 그건 착용이 아니다(33-4 #4).
                    _hairScratch.Add(new Shape("HairRimBack",
                        HeadRimBand(rig, BaldRimBackFromDegrees, BaldRimBackToDegrees,
                            BaldRimInnerRadiusRatio, BaldRimOuterRadiusRatio, BaldRimBackSegments),
                        true, SortHair, filled: true));
                    _hairScratch.Add(new Shape("HairRimFront",
                        HeadRimBand(rig, BaldRimFrontFromDegrees, BaldRimFrontToDegrees,
                            BaldRimInnerRadiusRatio, BaldRimOuterRadiusRatio, BaldRimFrontSegments),
                        true, SortHair, tone: Accent, filled: true));
                    break;

                case HairBowl:
                    _hairScratch.Add(new Shape("HairMass", BowlSilhouette(rig), true, SortHair, filled: true));
                    // 식별 특징 — 자른 앞머리 선. 실루엣의 안쪽 경계와 <b>정확히 겹친다(간격 0)</b>:
                    // 규칙 4가 "최악"이라고 못박은 것은 0 &lt; 간격 &lt; 1획이지 겹침이 아니다.
                    _hairScratch.Add(new Shape("HairFringe", BowlFringeLine(rig), false, SortHair,
                        tone: Accent));
                    break;

                case HairPonytail:
                    _hairScratch.Add(new Shape("HairMass",
                        HairMass(rig, PonytailCapRatio, 12f, 200f, PonytailBackCurtain, 194f, 76f,
                            PonytailFrontCurtain),
                        true, SortHair, filled: true));
                    // 식별 특징 — 뒤통수에서 묶여 떨어지는 긴 묶음. 시작·끝점을 <b>극좌표</b>로 잡아
                    // 덩어리 표면에서 출발하게 한다(좌표를 따로 적으면 묶음이 머리에서 뜬다).
                    _hairScratch.Add(new Shape("HairTail", new[]
                    {
                        HeadPolar(rig, 158f, 1.22f),
                        rig.F(-1.86f * rig.HeadRadius, rig.HeadCenterY + 0.62f * rig.HeadRadius),
                        rig.F(-2.42f * rig.HeadRadius, rig.HeadCenterY - 0.10f * rig.HeadRadius),
                        rig.F(-1.84f * rig.HeadRadius, rig.HeadCenterY - 0.34f * rig.HeadRadius),
                        rig.F(-1.30f * rig.HeadRadius, rig.HeadCenterY - 0.46f * rig.HeadRadius),
                        HeadPolar(rig, 196f, 1.06f),
                    }, true, SortHair, tone: Accent, filled: true));
                    break;

                // ★ 표식은 _hairScratch가 아니라 sink에 <b>직접</b> 넣는다 — 아래 클립 루프는 모자
                //   커버선 위를 잘라내므로, 표식을 거기 태우면 모자를 쓴 순간 표식까지 잘려
                //   "빠졌다는 사실"이 다시 조용해진다.
                default:
                    AppendMissingMarker(sink, EquipmentSlot.Hair, item, rig);
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

        /// <summary>
        /// 망토 3종이 공유하는 <b>서명 디테일</b> — 목을 감는 옷깃 띠(보조색·채움).
        /// <para>세 망토가 <b>같은 하나</b>를 쓴다. 밑단 길이·폭만 다른 형제들이라, 옷깃이 각자
        /// 다른 좌표를 갖는 순간 "같은 옷의 변주"라는 사실이 그림에서 사라진다(규칙 4-a).</para>
        /// </summary>
        internal static Vector3[] CapeCollarBand(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = CapeCollarLocalY(rig);
            return new[]
            {
                rig.F(r * CapeCollarBandFrontRatio, cy + r * CapeCollarBandTopRatio),
                rig.F(r * CapeCollarBandFrontRatio, cy + r * CapeCollarBandBottomRatio),
                rig.F(-r * CapeCollarBandBackRatio, cy + r * (CapeCollarBandBottomRatio - 0.04f)),
                rig.F(-r * CapeCollarBandBackRatio, cy + r * (CapeCollarBandTopRatio - 0.04f)),
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

        /// <summary>
        /// ★ 같은 아이템 안에서 <b>채움 2개가 겹칠 때</b>의 그리기 순서 — 2026-09-01(3차).
        ///
        /// <para>문제: 한 아이템의 채움은 전부 <c>SortingOrder − 1</c>이라 <b>동률</b>이다. 동률은
        /// 그리기 순서가 미정이라는 것이 이 저장소가 33-2-0에서 이미 한 번 정리한 함정이고, HAIR
        /// (덩어리 vs 삐침)에서는 <b>이미</b> 그 상태였다. 이번 재설계로 HEAD(관 vs 챙)·
        /// NECK(고리 vs 자락)·BACK(천 vs 옷깃)까지 늘었다.</para>
        ///
        /// <para>고침: 채움 메시 오브젝트의 <b>로컬 z</b>에 "아이템 안에서 몇 번째로 넣었는가 × 이 값"을
        /// 준다. z가 작을수록 카메라에 가까우므로(2D 카메라는 −z에서 +z를 본다) <b>나중에 넣은 것이
        /// 위로 온다</b> — 목도리가 고리를 자락보다 나중에 넣는 이유가 이것이다.</para>
        ///
        /// <para><c>sortingOrder</c>를 건드리지 않는다는 것이 이 해법의 핵심이다. 레이어를 옮기면
        /// <c>EyesVisorOpacityTests.판이_머리카락과_모자_사이의_제자리에_깔린다</c>(모든 EYES 도형이
        /// <c>SortEyes</c>여야 한다)가 깨진다. 크기는 획 두께의 1/3000이라 원근 왜곡이 없다.</para>
        /// </summary>
        internal const float FillDepthStep = -0.0001f;

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
    }
}
