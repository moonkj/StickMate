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
    ///
    /// ============================================================================
    /// ★ partial 인 이유 — 인계본 조각 (2026-09-05, 계약 v2 · EQUIPMENT_HANDOFF_PORT_SPEC §14-0 #1)
    /// ============================================================================
    /// 카드와 몸은 <b>좌표 한 벌</b>(인계본 정면 기하)이다 — §13-4-1 의 「카드 정면 / 몸 3/4 두 벌」 안은 R16 §14-0 #1 로 폐기됐다.
    /// 한 벌 아이템(눈 3 · 목 4 · 털모자 · 날개)은 카드 좌표에 아이템 변환(HAT_FIT · dy)만 걸어 몸에 그리고, 카드에 없는 조각이
    /// 몸에 필요한 아이템(망토 무대 도형 · R19 모자 2층 분할/뒷벽 · 외알안경 거울/반대쪽 눈 · 배낭 착용면)만 몸 조각을 bodyFixed
    /// 좌표로 따로 갖는다(표면 비트로 명시). 정의처는 그래도 <b>한 함수 · 한 목록</b>이다: <see cref="Append"/> 하나가 같은 목록에
    /// 내고 그리는 쪽이 자기 표면만 거른다. 좌표는 인계본 HTML 에서 기계로 옮긴 것이라 <c>AccessoryShapeBuilder.Handoff.cs</c>
    /// (Tools/CardShapeGen 생성물)에 있다 — <b>같은 클래스, 다른 파일</b>이지 두 주인이 아니다.
    /// 드리프트 방어: <c>Tests/EditMode/CardShapeContractTests</c>가 골든(모델에서 독립 생성)과, <c>CardShapeGeneratorStampTests</c>가
    /// 정본 sha 스탬프와 대조한다.
    /// </summary>
    internal static partial class AccessoryShapeBuilder
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
        // ★★ 2026-09-06 R25b — <b>바로 윗 문단의 «+0.08 ~ −0.06 R»은 더 이상 사실이 아니다.</b>
        //   HEAD 6종의 몸 도형은 전부 v1을 떠났고(0~3 인계본 조각 · 4~5 R21 재저작 배열), R25가
        //   사용자 신고("안경 착용시 모자들이 가리는 현상이 많았음")로 6종을 통째로 올렸다.
        //   커버선은 이제 그 모자의 <b>H-2 착용선</b>이고 값은 +0.3282 ~ +0.4482 R이다.
        //   ⇒ 아래 세 상수(HatBrimLineRatio · BeanieBandTopRatio · FedoraBrimLineRatio)는 <b>커버선만</b>
        //     정한다. 같은 상수를 읽는 <c>AppendHead</c>의 <c>case HeadCap/HeadBeanie/HeadFedora</c>는
        //     <c>AppendHandoff</c>가 먼저 true를 돌려주므로 <b>도달하지 않는 코드</b>이고, 그 아래 문단들의
        //     «커버선 + 관 높이 = 꼭대기» 산술은 전부 <b>그 죽은 v1 도형</b>의 이야기다 — 화면과 무관하다.
        //     (베레모·밀짚모자가 R25에서 v1 조형 상수를 지운 것과 같은 처리를 여기도 해야 한다 —
        //      리더 배정 대기. 그때까지 이 문단이 오독을 막는다.)
        //
        // 챙은 <b>닫힌 띠</b>다(규칙 8). 뿌리가 1.34~1.51획 두껍고 끝이 점으로 수렴한다 —
        // 옛 챙은 뿌리가 1.10획이라 화면에서 그냥 선 하나였다.

        /// <summary>야구모자의 <b>커버선</b> = 이 모자의 <b>H-2 착용선</b>(앞층 채움이 머리 현을
        /// 마지막으로 덮는 y). <b>이 값이 곧 이 모자의 <see cref="HatCoverLocalY"/></b>이고,
        /// 렌더러의 <c>HatBrimLocalY</c>가 그대로 노출한다.
        /// <para>★ 이름의 "Brim"은 <b>이력</b>이다 — 0.62(v1 옛 관·챙 경계) → 0.06(2026-09-01 감쌈 재설계)
        /// → <b>0.4482</b>(2026-09-06 R25b). R25가 인계본 조각으로 모자를 올렸을 때 이 값만 안 따라와서
        /// 커버선과 착용선 사이에 <b>머리카락도 모자도 없는 민머리 띠</b> 0.2410 R이 생겼다
        /// (실측: 머리 원반 노출 면적 0.667 R² · 최대 세로 0.372 R at x = −1.10).
        /// 베레모·밀짚모자가 같은 라운드에 <see cref="BeretBrimLineRatio"/>/<see cref="StrawBrimLineRatio"/>로
        /// 받은 처방과 <b>같은 처방</b>이다.</para>
        /// <para><b>낮추는 방향으로는 건드리지 마라</b>(<c>docs/EQUIPMENT_HANDOFF_PORT_SPEC.md</c> §14-16-5-a):
        /// 커버선이 착용선보다 <i>높으면</i> 머리카락이 덜 잘려 모자 채움 뒤에 숨지만, <i>낮으면</i>
        /// 그 사이가 그대로 맨머리가 된다. 값은 격자 0.004 스캔의 보고값(0.448213)이라 참 착용선
        /// (0.445180)보다 0.0030 R <b>위</b>다 — 안전한 쪽이다.</para></summary>
        internal const float HatBrimLineRatio = 0.4482f;

        /// <summary>관 꼭대기 = 커버선 + 이 값. 렌더러의 <c>HatTopLocalY</c>가 그대로 노출하고,
        /// <c>CharacterAccessoryScaleTests</c>가 "정수리(1.0R)보다 높다"를 잠근다.
        /// <para>★ 2026-09-03 R12 이식 1단계 — 1.18 -&gt; <b>1.16</b>. 꼭대기 0.06 + 1.16 = <b>+1.22 R</b>로
        /// 인계본 관(σ = 1.461, 밑변 −0.2200 R)의 꼭대기 +1.2248 R을 우리 격자(소수 두 자리)로 받는다
        /// (<c>docs/EQUIPMENT_HANDOFF_PORT_SPEC.md</c> §5-5-1 처방 #1).</para>
        /// <para>★ 2026-09-06 R25b — 커버선이 0.06 → 0.4482로 올라가면서 이 합(<c>HatTopLocalY</c>)도
        /// +1.22 → <b>+1.6082 R</b>이 됐다. 그 꼭대기는 <b>죽은 v1 관</b>의 것이다(화면의 야구모자는
        /// 인계본 조각이다). 이 값 자체는 안 움직였다.</para></summary>
        internal const float HatCrownHeightRatio = 1.16f;

        /// <summary>관 <b>옆벽</b>의 x. 감쌈(|x| ≥ 0.85R)을 만드는 자리라 0.85 아래로 내려가면 안 된다.
        /// <para>★ 2026-09-03 R12 이식 1단계 — 0.94 -&gt; <b>1.02</b>. 인계본 관을 σ = 1.461로 키운 결과의
        /// 반폭 1.0201 R이다(§5-5-1 #1). 우리 관은 <b>좌우 대칭</b>이라 인계본 관 중심 −0.01 R을
        /// 버렸다 — 0.01 R = 0.029획(배율 0.75)이라 저작 격자(0.01 R) 한 칸이다.</para></summary>
        internal const float HatCrownHalfWidthRatio = 1.02f;

        /// <summary>★ 2026-09-03 R12 이식 1단계 신설 — 관을 두르는 <b>곧은 띠</b>의 아랫변
        /// (§5-5-1 처방 #3, 띠 규약은 §5-4-1). 인계본 좌표를 버리고 두께
        /// <see cref="AccentBandThicknessRatio"/>(0.46 R)짜리 곧은 띠로 다시 세운 것이고,
        /// 아랫변 y = 관 밑변(−0.22 R) + 0.20 R = <b>−0.02 R</b>이다.
        /// <para>반폭은 <b>좌표를 새로 적지 않고</b> <see cref="HatCrownHalfWidthRatio"/>를 그대로 받는다
        /// (규칙 4-a) — 관이 움직이면 띠가 따라간다.</para>
        /// <para>톤은 <b>그늘</b>이다. 이 모자의 보조색은 <b>챙</b>이고(형제와 나를 가르는 부분),
        /// 규칙 3-2는 아이템당 보조색을 <b>정확히 하나</b>로 못박는다 — 띠까지 보조색으로 만들면
        /// 그 규칙이 깨지고 폴백 아이콘(<c>AccessoryFallbackBodyParityTests</c>)이 함께 갈라진다.</para></summary>
        internal const float HatBandBaseRatio = -0.02f;

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

        /// <summary>드러난 눈의 중심 x. 유도: 2d ≥ 1.5W + ρ_visor(0.36R) + a(0.33R) = 1.206R -> d ≥ 0.603R.</summary>
        internal const float DrawnEyeOffsetRatio = 0.62f;

        /// <summary>드러난 눈의 반지름(<see cref="RoundLensSegments"/>각 원반). ★ 2026-09-05 R13-P1 —
        /// 옛 아몬드(반폭 0.34 / 반높이 0.24)를 원반으로 바꿨다. 아몬드는 이 배율에서 <b>원리상</b>
        /// 규칙 1-C를 통과할 수 없다: 반축 (a,b) 마름모의 최대 내접원은 ρ = ab/√(a²+b²) &lt; min(a,b)라
        /// 반높이 0.24R에서는 <b>폭을 아무리 늘려도</b> ρ &lt; 0.24R이고, 게이트 0.21818R을 1.20획으로
        /// 넘기려면 반높이가 0.410R(눈높이 0.82R)이 되어야 했다.
        /// <para>원반은 ρ = r·cos15° = 0.966r 이라 <b>같은 ρ를 더 작은 발자국</b>으로 얻는다 —
        /// ρ 0.1855R(0.85획) → <b>0.3188R(1.46획)</b>, 발자국은 0.68×0.48R → 0.66×0.66R로 <b>폭이 3% 준다</b>.
        /// 덤으로 꼭짓점 회전이 30°(&lt; 45°)라 배율 0.60의 「양끝 꺾임」 2건이 함께 사라진다.</para>
        /// <para>그리고 이것이 <b>어휘를 하나로</b> 만든다 — 캐릭터 본체의 눈이 이미 원반이고
        /// 액세서리의 눈만 아몬드였다.</para></summary>
        internal const float DrawnEyeRadiusRatio = 0.33f;

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
        // ★ 2026-09-02 B-2 파일럿 — NECK 6종의 <b>형상 비율은 여기 없다</b>.
        //   전부 Resources/Items/equip_neck_*.asset의 wornShapes로 내려갔다(Core/AccessoryDefSO.cs).
        //   여기 남은 것은 <b>부착 기준선</b>(NeckCollarRiseRatio -> NeckLocalY)뿐이고, 그것은
        //   아이템의 조형이 아니라 <b>리그의 사실</b>이라 코드가 갖는 것이 옳다 —
        //   데이터는 그 선을 기저 번호(AccessoryWornBasis.NeckLine)로 가리키기만 한다.

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

        /// <summary>
        /// ★ 2026-09-05 — <b>옷깃 띠(CapeCollarBand)를 어깨 요크로 대체했다.</b> 망토 3종의 보조색은
        /// 여전히 정확히 하나지만, 그 하나가 <b>목 자리</b>에서 <b>어깨 자리</b>로 내려왔다.
        ///
        /// <para><b>왜</b>: 옛 띠는 x[−0.66, +0.40] × y[−1.598, −1.118]로 NECK 6종의 봉투 안에 통째로
        /// 들어가 있었다. NECK은 정렬 7/6, BACK은 −1/−2라 <b>넥타이가 항상 위</b>이므로,
        /// 덮이는 것은 넥타이가 아니라 <b>띠 자신</b>이다. `design-equipment` 실측(R13):
        /// 목도리·반다나에서 남는 색면 <b>0.0%</b>, 나비넥타이 0.1%, 나머지 셋도 잔여 ρ 0.28~0.30획으로
        /// 전부 규칙 1-C(1.00획) 미달 — 즉 <b>망토를 사고 목에 뭘 걸치면 망토의 유일한 보조색이 사라진다.</b>
        /// 카드(44 px)에서도 짧은망토 3.16 px · 긴망토 2.32 px로 자기 윤곽획 둘(3.74 px)에 못 미쳐
        /// <b>몸에서도 카드에서도</b> 안 보이는, 카탈로그에서 유일한 조각이었다.</para>
        ///
        /// <para><b>기각된 대안</b>: 띠 밑변을 −0.34에서 −1.80 R까지 내려 봐도 최악 잔여 ρ가
        /// 0.48획을 못 넘는다 — 목도리 자락·타이 blade가 <b>세로로</b> 앞을 가로질러 남는 색면을 계속
        /// 쪼개기 때문이다. 보조색이 NECK 영역을 <b>떠나야</b> 한다는 것이 그 스윕의 결론이다.</para>
        ///
        /// <para><b>왜 하필 2.40 R인가</b>: 몸(머리·몸통·팔, 정렬 0~4)까지 덮개로 넣은 재측정에서
        /// 최악(목도리 동시착용) 잔여 ρ가 깊이 2.0에서 0.68획, <b>2.4에서 1.12~1.60획</b>으로
        /// 게이트 1.00획을 처음 넘는다. 더 내리면 여유는 늘지만 망토의 보조색 면적이 커진다 —
        /// 그 지점부터는 조형 취향이라 여기서 멈춘다.</para>
        ///
        /// <para>★ 요크는 <b>좌표를 새로 적지 않는다</b>(규칙 4-a). 윗변 두 점은 <see cref="CapeOutline"/>의
        /// 옷깃 두 점 <b>그 자체</b>이고, 아랫변 두 점은 <b>같은 윤곽선의 앞/뒤 변</b>을 이 깊이에서 자른
        /// 점이다. 그래서 망토 길이·폭을 고치면 요크가 저절로 따라오고, 요크가 윤곽 <b>안</b>에 있으므로
        /// 실루엣은 한 점도 움직이지 않는다(BACK 쌍별 최소 차 6.04획 무변화).</para>
        /// </summary>
        internal const float CapeYokeDepthRatio = 2.40f;

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

        /// <summary>
        /// A군 5종(중절모·밀짚모자·왕관·베레모의 띠 + 바가지머리 앞머리)이 <b>열린 낱선에서
        /// 닫힌 채움 띠</b>가 되면서 갖는 두께(R 배수) — 스펙 14-1, 2026-09-03.
        ///
        /// <para>구속 조건은 규칙 1-C(색면 조건)다: 색면이 1획 폭을 가지려면
        /// <c>ρ_max ≥ W_out</c>이고, 여기서 <c>W_out = <see cref="FillOutlineBudgetInHeadRadii"/></c>가
        /// 배율 0.509 이상에서 상수 <b>0.21818 R</b>이다. 얇은 띠는 <c>ρ_max ≈ h/2</c>이므로
        /// 하한이 <b>h ≥ 0.43636 R</b>가 된다.</para>
        ///
        /// <para><c>h = 0.4298 R</c>(배율 0.60에서의 1획)은 <c>ρ_max = 0.2149</c>로 <b>여유 0</b>이라
        /// 기각했다. <c>h = 0.46 R</c>은 5종 실측 <c>ρ_max = 0.22988 ~ 0.23000</c>, 여유
        /// <b>+0.0117 R(+5.4%)</b>이고 다섯 도형 모두 자기교차가 없다.</para>
        /// </summary>
        internal const float AccentBandThicknessRatio = 0.46f;

        // ---- 털모자(33-2-1 #2). ★ 2026-09-03 — R12 이식 1단계로 <b>덩어리의 배치가 뒤집혔다</b>.
        //
        //      옛 형태: 넓은 관이 머리를 덮고 그 <b>아래</b>로 접힌 단이 −0.26R까지 내려왔다.
        //      새 형태(인계본 `furhat`을 세로 아핀 y' = 0.85·y − 0.155로 받은 것, 스펙 §5-5-1 처방 #4):
        //        · <b>넓은 털 단</b>(반폭 1.05R, y = −0.06 ~ +0.52R. 옆면은 곧은 수직선이다)
        //        · 그 <b>위</b>에 얹힌 <b>좁은 관</b>(반폭 0.57R, y = +0.56 ~ +1.44R)
        //      즉 부피가 머리 <b>아래</b>가 아니라 <b>옆</b>으로 갔다.
        //
        //      ★ 2026-09-02가 세운 두 사실은 그대로 지킨다(그것이 사용자 신고의 처방이었다):
        //        · 관과 단은 여전히 <b>한 채움</b>이고 접힌 자리는 <b>그늘색 낱선 하나</b>다.
        //          (AccessoryFilledBandRuler의 「채움 vs 낱선」 대조가 BeanieCuff를 낱선 쪽 표본으로 쓴다 —
        //           채움으로 바꾸면 그 대조가 통째로 죽는다.)
        //        · 잉크 밑단이 더 <b>올라갔다</b>: 남는 머리 1.22획 -> <b>1.69획</b>(배율 0.60),
        //          면적 22.5% -> <b>32.7%</b>. 이식이 이 신고를 더 세게 닫는다.
        //
        //      ★ (R12 당시) 커버선 −0.06R은 한 자리도 안 움직였고 <b>가리키는 변만</b> 바뀌었다 —
        //      「단의 위 끝」 -> 「단의 아래 끝」. 이름은 이력이다.
        //      (이름을 안 바꾼 이유: 이 이름을 읽는 곳이 프로덕션 밖에 넷 있다 —
        //       AccessoryShapeCatalogTests · AccessoryBeaniePomTests · design/equipment/verify/r10_*.py.)
        //
        //      ★★ 2026-09-06 R25b — <b>그 −0.06R은 이제 없다.</b> R25가 인계본 털모자를 올린 뒤에도
        //      커버선만 −0.06에 남아 <b>0.3196 R짜리 민머리 띠</b>가 생겼고, 그래서 커버선을 이 모자의
        //      H-2 착용선 <b>+0.3282</b>로 올렸다. 아래 문단들의 «−0.06 + …» 산술은 전부 <b>죽은 v1
        //      털모자</b>의 것이다(화면의 털모자는 인계본 조각이고, 이 case는 도달하지 않는다).

        /// <summary>털모자의 <b>커버선</b> = 이 모자의 <b>H-2 착용선</b>.
        /// <see cref="HatCoverLocalY"/>가 그대로 돌려준다.
        /// <para>★ 이름의 "BandTop"은 <b>이력</b>이다 — 「단의 위 끝」(~2026-09-02) → 「단의 아래 끝」
        /// (R12 이식) → <b>착용선</b>(2026-09-06 R25b). 값은 −0.06 → <b>+0.3282</b>.</para>
        /// <para>★ 왜 움직였나: R25가 인계본 <c>furhat</c>을 <c>AccessoryWornTransform</c>(u 1.5977 ·
        /// ky 0.676 · dy 0.13521)으로 올렸는데 이 상수만 안 따라왔다. 그 결과 커버선과 착용선 사이
        /// <b>0.3196 R</b>이 통째로 맨머리가 됐다(노출 면적 0.798 R² · 최대 세로 0.380 R) — 6종 중 가장
        /// 컸다. <b>낮추지 마라</b>(§14-16-5-a): 커버선이 착용선보다 높으면 머리카락이 모자 뒤에 숨을
        /// 뿐이지만, 낮으면 그 띠가 그대로 드러난다. 격자 보고값 0.328213 vs 참 착용선 0.325228 —
        /// 0.0030 R 위(안전한 쪽)다.</para>
        /// <para>이 값은 여전히 <b>6종 중 가장 낮다</b>(다음이 베레모 +0.3602) — 「가장 깊이 눌러쓰는
        /// 모자가 털모자」라는 정체를 <c>AccessoryShapeCatalogTests</c>가 이 부등호로 잠근다.</para></summary>
        internal const float BeanieBandTopRatio = 0.3282f;

        /// <summary>접힌 단의 반폭. ★ 2026-09-03 0.96 -&gt; <b>1.05</b> — 인계본 단이 옆으로 가장 부푼
        /// 자리의 반폭 1.0507 R이다. 이 값이 털모자의 <b>실루엣 최대폭</b>이고, 감쌈
        /// (|x| ≥ 0.85R · y ≤ 0.05R)을 만드는 자리이기도 하다.
        /// <para>★ 인계본은 단의 옆면이 <b>둥근 끝</b>이라 밑변 0.9889 R에서 1.0507 R로 부풀었다가
        /// 다시 들어온다. 우리는 그 둥근 끝을 <b>못 받는다</b>: 세로 여유가 0.58 R뿐이라 중간
        /// 꼭짓점을 하나 넣으면 양쪽 변이 각각 0.29 R = <b>0.72획</b>이 되어 「획보다 짧은 변」이
        /// 된다(1획 = 0.3439 R). 실제로 그렇게 넣었다가
        /// <c>AppearanceShapeBudgetTests.최단_실제_변_검사를_액세서리_30종으로_확장한다</c>의
        /// 래칫이 14 -&gt; 15로 잡았다. 그래서 <b>최대폭을 남기고 둥근 끝을 버렸다</b> — 옆면은
        /// 곧은 수직선이고, 실루엣이 정하는 값(최대폭)은 인계본 그대로다.</para></summary>
        internal const float BeanieBandHalfWidthRatio = 1.05f;

        /// <summary>★ 2026-09-03 신설 — 단 <b>윗변</b>이 옆(±<see cref="BeanieBandHalfWidthRatio"/>)에서
        /// 닿는 y(인계본 +0.5179 R).</summary>
        internal const float BeanieCuffTopRatio = 0.52f;

        /// <summary>★ 2026-09-03 신설 — 관의 <b>밑변</b> = 접힌 자리(낱선)의 y. 인계본 관 밑변
        /// +0.5389 R을 우리 격자로 받은 값이고, 단 윗변(<see cref="BeanieCuffTopRatio"/>)보다
        /// 위라서 관과 단이 <b>겹친다</b>(규칙 4가 금지하는 것은 0 &lt; 간격 &lt; 1획이지 겹침이 아니다).</summary>
        internal const float BeanieDomeFootRatio = 0.56f;

        /// <summary>관 꼭대기 = <see cref="BeanieBandTopRatio"/> + 이 값. 폼폼 꼭대기 계산이 이 합을 쓴다.
        /// <para>★ 2026-09-03 1.38 -&gt; <b>1.50</b>. 꼭대기 −0.06 + 1.50 = <b>+1.44 R</b>이고, 이것이
        /// 인계본 관을 아핀으로 받은 값(+1.4426 R)이다. <b>폼폼 꼭대기(+1.72 R)는 안 움직인다</b> —
        /// <see cref="BeaniePomCrestRiseRatio"/>가 같은 양만큼 줄어 합을 보존한다.</para></summary>
        internal const float BeanieCrownHeightRatio = 1.50f;

        /// <summary>관(윗덩어리)의 반폭. ★ 2026-09-03 1.06 -&gt; <b>0.57</b> — 인계본 관 반폭 0.5686 R.
        /// 이제 관은 단보다 <b>좁다</b>(그것이 털모자의 새 실루엣이다).</summary>
        internal const float BeanieCrownHalfWidthRatio = 0.57f;
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
        /// 꼭대기이므로, 반지름을 바꿔도 이 값이 고정되면 실루엣 상한이 따라 움직이지 않는다.
        /// <para>★ 2026-09-03 R12 이식 1단계 — 0.40 -&gt; <b>0.28</b>. <b>고정 대상은 이 값이 아니라
        /// 합(= 실루엣 꼭대기)이다</b>: <see cref="BeanieBandTopRatio"/> + <see cref="BeanieCrownHeightRatio"/>
        /// + 이 값 = −0.06 + 1.50 + 0.28 = <b>+1.72 R</b>로, 옛 −0.06 + 1.38 + 0.40과 <b>같다</b>.
        /// 관이 0.12R 자라고 이 값이 0.12R 줄어 액자(1.80R)와의 여유 0.08R이 그대로다.</para>
        /// <para>★ 2026-09-06 R25b — <see cref="BeanieBandTopRatio"/>가 커버선 전용으로 +0.3282가 되면서
        /// 이 <b>합은 +2.1082 R</b>이 됐고, 그것을 1.72로 잠그던
        /// <c>AccessoryBeaniePomTests.폼폼_꼭대기가_액자_상한에_그대로_머문다</c>는 지금
        /// <c>SkipIfHandoff</c>로 건너뛴다(인계본 털모자에는 폼폼이 없다). <b>죽은 v1 폼폼의 산술이다</b> —
        /// v1 털모자를 되살릴 일이 생기면 이 세 상수를 커버선에서 <b>떼어내는</b> 것이 먼저다.</para>
        /// <para>덤: 반지름과 같아져 <see cref="BeaniePomOffsetRatio"/> = 0 —
        /// 폼폼 <b>중심</b>이 관 꼭짓점 <b>그 자체</b>가 된다(인계본도 폼폼 중심이 관 꼭대기다).
        /// 관과의 겹침은 0.28R = <b>0.81획</b>(옛 0.36획)이라 규칙 4의 최악 구간에서 더 멀어졌다.</para></summary>
        internal const float BeaniePomCrestRiseRatio = 0.28f;

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

        /// <summary>중절모의 <b>커버선</b> = 이 모자의 <b>H-2 착용선</b>.
        /// <para>★ 옛 값 0.08은 v1 관 밑변 두 발의 한가운데였다. 2026-09-06 R25b — R25가 인계본
        /// <c>fedora</c>를 올린 뒤 이 상수만 남아 커버선과 착용선 사이 <b>0.2044 R</b>이 맨머리로
        /// 드러났다(노출 면적 0.573 R² · 최대 세로 0.320 R). <b>낮추지 마라</b>(§14-16-5-a).
        /// 격자 보고값 0.412213 vs 참 착용선 0.411119 — 0.0011 R 위(안전한 쪽)다.</para></summary>
        internal const float FedoraBrimLineRatio = 0.4122f;

        /// <summary>★ 2026-09-03 R12 이식 1단계 — 2.06 -&gt; <b>1.48</b> / 1.68 -&gt; <b>1.10</b>
        /// (§5-5-1 처방 #6: 인계본 중절모 전체 −0.35 R). 인계본 챙은 <b>중심 +0.19 R · 반폭 1.29 R</b>인
        /// 볼록렌즈고, 그 중심 +0.19 R은 우리 현행 <c>FedoraBrim</c>의 중심 그 자체다(§5-5-3 조각 c 표).
        /// 즉 앞뒤 도달은 <b>1.29 ± 0.19</b>에서 나오고 새 숫자를 발명하지 않았다.</summary>
        internal const float FedoraBrimFrontRatio = 1.48f;
        internal const float FedoraBrimBackRatio = 1.10f;

        /// <summary>관 꼭대기 = <see cref="FedoraBrimLineRatio"/> + 이 값.
        /// <para>★ 2026-09-03 1.08 -&gt; <b>1.30</b>. 꼭대기 0.08 + 1.30 = <b>+1.38 R</b>(인계본 +1.3812 R).</para>
        /// <para>★ 2026-09-06 R25b — 그 «0.08»은 이제 커버선이 아니다(+0.4122). 이 합은 <b>죽은 v1 관</b>의
        /// 꼭대기이고, 화면의 중절모는 인계본 조각이다.</para></summary>
        internal const float FedoraCrownHeightRatio = 1.30f;

        /// <summary>관 밑변의 반폭. 챙·관·띠 <b>셋이 이 한 값</b>에서 발을 만든다.
        /// <para>★ 2026-09-03 0.98 -&gt; <b>0.61</b>(인계본 0.6055 R). 관이 좁아지고 높아진 것이
        /// 이 이식의 중절모 실루엣이다.</para></summary>
        internal const float FedoraCrownHalfWidthRatio = 0.61f;

        /// <summary>★ 2026-09-03 신설 — 관 밑변(= 띠 아랫변)이 <b>커버선 위로</b> 올라간 높이.
        /// 인계본 띠 아랫변 +0.3351 R을 우리 커버선(+0.08 R) 기준으로 다시 쓴 값이다.
        /// <para>관 밑변은 챙 <b>윗변 위의 두 꼭짓점</b>이라 관이 챙에 파묻힌다(간격 0) — 규칙 4가
        /// 금지하는 것은 0 &lt; 간격 &lt; 1획이지 겹침이 아니고, 관 높이로 1.5획을 띄우는 것은
        /// 산술적으로 불가능하다(<c>AccessoryHatBandAndBellTests</c> 문단과 같은 판단).</para></summary>
        internal const float FedoraCrownFootRiseRatio = 0.24f;

        // ★ 2026-09-01 — FedoraBandRiseRatio(0.14f)를 여기서 지웠다. 띠는 이제 좌표를 스스로 적지 않고
        //   관(crown) 밑변의 두 끝점을 <b>그대로 받아 쓴다</b>(AppendHead의 HeadFedora 문단 참고).

        // ---- 왕관(33-2-1 #4). HatCoverLocalY = +∞ — 씌우는 것이 아니라 얹는 것이라 밑이 뚫려 있다.
        //      ★ 2026-09-01(2차) — <b>채운 닫힌 도형</b>이 됐다. 옛 왕관은 채움 없는 지그재그
        //      폴리라인이라 봉우리 <b>끝이 둥근 캡으로 뭉갰다</b>(획은 둥근 캡이므로 선으로는
        //      뾰족해질 수 없다 — 37-6 규칙 6). 채운 도형의 <b>꼭짓점</b>만 점으로 수렴할 수 있다.
        //      밑이 뚫린 성질은 채움이 아니라 커버선 +∞가 계속 보장한다(if 분기가 아니다).
        /// <summary>왕관 <b>몸</b>의 밑변. ★ 2026-09-03 R12 이식 1단계 — 0.02 -&gt; <b>0.27</b>
        /// (§5-5-1 처방 #8: 세로 아핀 y' = 0.9164·y − 0.2025. 인계본 밑변 +0.2738 R).
        /// 아핀의 두 계수는 <b>우리 현행 왕관 봉투 [−0.10, +1.52]</b>가 정한 값이라 새 숫자가 아니다.</summary>
        internal const float CrownBaseRatio = 0.27f;

        /// <summary>왕관 <b>몸</b>의 반폭. ★ 2026-09-03 0.98 -&gt; <b>0.94</b>(인계본 0.9395 R).</summary>
        internal const float CrownHalfWidthRatio = 0.94f;

        /// <summary>★ 2026-09-03 신설 — 테(<c>CrownRim</c>)의 반폭. §5-5-1 처방 #9:
        /// 인계본 림 <b>19점을 폐기</b>하고 곧은 띠(§5-4-1)로 다시 세운 것이고, 반폭 1.0136 R은
        /// 그 처방이 직접 적은 값이다.
        /// <para>이 한 처방이 결함 <b>둘</b>을 동시에 닫는다: 림 바깥 끝면이 0.946획이던
        /// <b>몽당변 2건</b>(양끝 꺾임 81.3°·87.1°)과, 같은 림의 <b>규칙 1-C 0.98획 미달</b>.
        /// 두 결함의 원인이 하나(림이 얇다)라서 별도 처방을 쓰지 않았다.</para></summary>
        internal const float CrownRimHalfWidthRatio = 1.01f;

        /// <summary>★ 2026-09-03 신설 — 테의 <b>아랫변</b>. 인계본 −0.1000 R이고, 이것은 우리 현행
        /// 왕관의 밑변(−0.10 R)과 <b>같은 값</b>이다 — 아핀이 그 봉투에 맞춰졌기 때문이다.</summary>
        internal const float CrownRimBaseRatio = -0.10f;

        // ---- 동그란 안경. 코다리는 <b>렌즈 꼭대기</b>를 잇는 아치다 — 렌즈 한가운데를 가로지르면
        //      그 순간 아령(덤벨)이 된다(Tasklist V2). 간격은 규칙 1의 1.5획을 지킨다:
        //      두 렌즈 안쪽 변 사이 1.24R − 0.80R = 0.44R... 이 아니라, 반경을 키운 지금은
        //      중심 간격 1.24R − 지름 0.80R = <b>0.44R</b>이 아니라 안쪽 변 사이 0.44R = 1.28획이다.
        internal const float RoundLensOffsetRatio = 0.62f;
        internal const float RoundLensRadiusRatio = 0.40f;

        /// <summary>렌즈 중심이 <b>눈높이보다 살짝 위</b>인 양. 안경은 눈 위에 걸치는 물건이다.</summary>
        internal const float RoundLensCenterRiseRatio = 0.02f;

        /// <summary>코다리 아치의 꼭대기 높이. 두 끝은 렌즈 12각형의 <b>60도/120도 꼭짓점 그 자체</b>라
        /// 좌표를 새로 적지 않는다(규칙 4-a — 렌즈 크기를 고치면 아치가 따라온다).
        /// <para>★ 2026-09-05 — 붙는 자리를 <b>30도/150도에서 60도/120도로 한 칸 올렸다</b>.
        /// 옛 자리에서 코다리의 잉크 사각형이 <b>0.5472 R = 1.591 W</b>(규칙 1 문턱 1.5 W, 여유 5.7%)로
        /// 카탈로그 <b>최약체</b>였고, "규칙 1 위반 0이 되는 최소 배율 0.7070"을 이 조각 혼자 정하고
        /// 있었다(출하 0.75까지 여유 0.043). 한 칸 올리면 span이 <b>0.84 R = 2.44 W(+53%)</b>가 되고
        /// 최소 배율이 <b>0.6908</b>로 내려간다(여유 +38%).</para>
        /// <para>아치 꼭대기에서의 꺾임은 35.3°로 규칙 1-B의 45° 아래이고, 붙는 점이 렌즈 꼭짓점
        /// 그 자체라 규칙 4의 간격은 여전히 <b>0(겹침)</b>이다. 그리고 이 방향이 이 아이템의 원래
        /// 의도와 같다 — 코다리가 렌즈 한가운데를 가로지르면 아령이 되고, <b>높이가 정체</b>다.</para></summary>
        internal const float RoundBridgeRiseRatio = 0.50f;

        /// <summary>둥근 렌즈/외알 알을 근사하는 변의 수. <b>12</b>여야 60도·120도·270도 꼭짓점이
        /// 인덱스 2·4·9로 정확히 집힌다 — 코다리(2·4)와 체인(9)이 그 꼭짓점을 <b>그대로 받아 쓰므로</b>
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
        /// (2·0.62 − 0.36 − <see cref="DrawnEyeRadiusRatio"/> 0.33 = 0.55R = 1.60획).</summary>
        internal const float MonocleRadiusRatio = 0.36f;


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

        // ============================================================================
        // ★ 2026-09-06 R25 — 베레모·밀짚모자는 <b>좌표 상수를 갖지 않는다</b>
        // ============================================================================
        // 두 종은 R21 재저작본(64u 아이콘 + HAT_FIT 변환)으로 갈아탔고, 그 도형은 상수 몇 개로
        // 표현되지 않는다(2차 곡선 · 2층 챙). 좌표는 <see cref="V1Hat_StrawCrown"/> 이하의
        // <b>배열</b>에 있다 — 인계본 조각과 같은 방식이되 계약은 v1 그대로다.
        //
        // 그래서 옛 상수 9개(BeretCrownHeight/BackDroop/Front/FrontShoulder/FrontShoulderTop/
        // BackDroopDrop · StrawBrimFront/StrawBrimBack/StrawCrownHeight/StrawCrownHalfWidth)를
        // <b>지웠다</b>. 남겨 두면 아무도 안 읽는 옛 도형의 치수가 정본인 척 남는다.
        // 아래 두 개만 남는다 — 그것은 조형이 아니라 <b>커버선</b>(머리카락을 자르는 선)이다.

        /// <summary>베레모의 커버선 = 이 모자의 <b>H-2 착용선</b>(앞층 채움이 머리 현을 마지막으로
        /// 덮는 y). 새 몸이 그 위를 불투명으로 덮으므로 머리카락은 여기서 잘려야 한다
        /// (EQUIPMENT_HANDOFF_PORT_SPEC §14-12-5 #24).
        /// <para>★ 옛 값 0.02는 폐기된 3/4 덩어리의 밑변이었다. 지금 값은 실측
        /// (<c>design/equipment/verify/r24_hats.py</c> ② 채움만)이다.</para></summary>
        internal const float BeretBrimLineRatio = 0.3602f;   // = 이 모자의 HatCoverLocalY

        /// <summary>밀짚모자의 커버선 = 이 모자의 H-2 착용선. 옛 값 0.08의 이력은
        /// <see cref="BeretBrimLineRatio"/>와 같다.</summary>
        internal const float StrawBrimLineRatio = 0.4162f;   // = 이 모자의 HatCoverLocalY

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
        //      끈 끝점은 <b>polar(111°/249°, 1.02)</b>다. ★ 2026-09-05(R13-P2): R13-P1이 드러난 눈을
        //      아몬드에서 원반(r 0.33R)으로 바꾸면서 눈이 위아래로 <b>0.09R씩 자랐고</b>, 그만큼
        //      끈-눈 간격이 <b>0.26획</b> 줄어 규칙 4(1.5획)를 1.31획으로 밑돌았다. 자란 것은
        //      이웃이므로 P1을 되감지 않고 끈 각도만 122° -> 111°로 내렸다(docs/EQUIPMENT_SHAPE_SPEC.md §16).
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

        /// <summary>끈 끝점의 각도(도). 위쪽 끝이 이 각도, 아래쪽 끝이 360 − 이 각도다.
        /// <para>★ 111°는 <b>파레토 최적</b>이다 — 끈-눈 간격에는 천장 1.6032획(끈의 가운데 변이
        /// 천의 뒤변이라 각도와 무관하게 남는 구속)이 있고, 그 천장에 닿는 최대 각도가 111.06°다.
        /// 더 내리면 <b>이득 없이</b> 실루엣만 잃고, 104°에서는 카드 지배축이 세로로 넘어가
        /// 아이템이 카드에서 1.5% 작아진다. 올리면 규칙 4(1.5획)를 다시 깬다.</para></summary>
        internal const float PatchStrapDegrees = 111f;



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
                rig.F(-side, hc + r * 0.36f),
                rig.F(-r * 0.72f, hc + r * 1.04f),
                rig.F(0f, HatTopLocalY(rig)),
                rig.F(r * 0.72f, hc + r * 1.02f),
                rig.F(side, hc + r * 0.34f),
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
        /// <b>드러난 눈</b> — 채운 <see cref="RoundLensSegments"/>각 원반.
        /// <para>한쪽만 가리는 물건(외알안경·안대)에서만 쓴다. 두 눈을 다 가리는 물건은 아무것도
        /// 보여 주지 않는다 — 렌즈 <b>안</b>으로 눈이 비치는 그림은 이 배율에서 기하학적으로
        /// 불가능하기 때문이다(위 EYES 문단의 산술).</para>
        /// <para><b>동공은 넣지 않는다.</b> 내부를 보이려면 3.0W = 1.03R, 즉 머리 반지름만 한 눈이
        /// 필요하다(규칙 1). 원반 하나가 이 배율에서 그릴 수 있는 눈의 전부다.</para>
        /// <para>★ 2026-09-05 R13-P1 — 옛 아몬드 4점을 원반으로 바꾼 이유는
        /// <see cref="DrawnEyeRadiusRatio"/>에 있다(아몬드는 원리상 규칙 1-C를 못 넘었다).</para>
        /// </summary>
        /// <param name="sign">−1 = 진행 반대쪽(= 가려지지 않은 눈). 지금 두 소비자 모두 −1이다.</param>
        internal static Vector3[] DrawnEye(in Rig rig, float sign)
            => Polygon(rig, sign * DrawnEyeOffsetRatio * rig.HeadRadius, GlassesLocalY(rig),
                DrawnEyeRadiusRatio * rig.HeadRadius, RoundLensSegments);

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
            /// <para>★ <b>채움의 이유는 하나가 아니다</b>(2026-09-03 정정). 이 프로젝트에서 채우는
            /// 이유는 셋이고, 서로 독립이다:
            /// <br/>(가) <b>안에 있는 것을 가린다</b> — 모자 관. 이 값이 처음 생긴 이유.
            /// <br/>(나) <b>꼭짓점을 점으로 수렴시킨다</b> — 왕관 봉우리. 획은 둥근 캡이라
            ///     <b>선으로는 뾰족해질 수 없다</b>(2026-09-01 2차, 규칙 6).
            /// <br/>(다) <b>덩어리를 만든다</b> — 머리카락, 그리고 A군 5종의 띠.
            ///     이 배율에서 획 하나보다 얇은 띠는 색면을 못 가진다(규칙 1-C).</para>
            ///
            /// <para>★ <b>왕관으로 이 값을 설명하지 마라 — 왕관은 채운다.</b>
            /// <c>CrownBody</c>는 2026-09-01(2차)에 (나)의 이유로, <c>CrownRim</c>은 2026-09-03에
            /// (다)의 이유로 채움이 됐다. 옛 주석은 "왕관은 일부러 채우지 않는다"였고 <b>지금은 거짓</b>이다.
            /// (실측: 프로덕션 덤프 <c>Tools/ShapeDump/build.sh</c>가 두 도형 모두 <c>filled=1</c>로 찍는다.)</para>
            ///
            /// <para>★ 그리고 <b>「채움」과 「커버선」은 다른 일을 한다</b> — 옛 주석은 둘을 한 문장에
            /// 섞어 두어 하나가 바뀌자 다른 하나까지 틀린 것처럼 읽혔다.
            /// <br/>· <c>Filled</c> = <b>화면에서 덮는다</b>. 액세서리는 <c>SortHead</c>(10)가
            ///   <c>SortHair</c>(6)보다 위라, 겹치는 자리는 채움이 가린다.
            /// <br/>· <see cref="HatCoverLocalY"/> = <b>머리카락 도형을 자른다</b>
            ///   (<see cref="AppendClippedBelowCover"/>). 왕관만 <see cref="NothingCovered"/>다.
            /// <br/>왕관은 <b>덮되 자르지 않는다</b>. 그래서 봉우리 사이와 위로 머리카락이 그대로
            /// 올라오고, 그 사실을 지키는 것은 채움 여부가 아니라 <b>커버선 하나</b>다.</para>
            /// </summary>
            public readonly bool Filled;

            // ---- 계약 v2 (2026-09-05 · R16/R17 정정). 전부 0/false 가 v1과 같은 뜻이라 옛 호출부는 그대로 옳다.
            //      정의처는 Core/AccessoryShapeContract.cs — 에셋(AccessoryWornShapeData)과 같은 표다.

            /// <summary>표면 비트(<see cref="AccessorySurface"/>). <b>0 = Body|Card</b>.</summary>
            public readonly byte Surfaces;

            /// <summary>획 배수(연속, 0 = ×1). 카드 획 = 인계본 아이콘 획 × 이 값.</summary>
            public readonly float StrokeMult;

            /// <summary>몸 표면의 명목 획 폭(R 배수, 배수가 곱해진 값). <b>0 = v1 조각</b>(비례 획 + 2pt/1pt 하한).
            /// 0 보다 크면 인계본 조각이고 색·알파·하한 규칙이 전부 계약 v2 로 바뀐다(<see cref="IsHandoff"/>).</summary>
            public readonly float StrokeInR;

            /// <summary>윤곽/선 없음(인계본 F/CF).</summary>
            public readonly bool NoStroke;

            /// <summary>채움 알파(카드 사전 합성 입력 · 몸 런타임 알파). 0 = 미설정(<see cref="AccessoryCardWash.GradientMeanAlpha"/>).</summary>
            public readonly float Alpha;

            /// <summary>선 알파. 0 = 1.</summary>
            public readonly float LineAlpha;

            /// <summary>얹히는 조각 = 같은 아이템 목록에서 이만큼 앞(0 = 없음). 카드 사전 합성의 밑색.</summary>
            public readonly int UnderBack;

            /// <summary>조각의 몸 층(<see cref="AccessoryPieceLayer"/>). 정렬 번호는 <see cref="LayerOrder"/>가 정한다 — 망토 칼라·걸쇠는
            /// 몸통 앞(<see cref="SortCapeFront"/>), R19 모자 뒤층/배낭 뒷판은 몸 뒤(<see cref="SortBack"/>). 카드는 목록 순서로 겹친다.</summary>
            public readonly byte Layer;

            public bool IsFrontLayer => Layer == (byte)AccessoryPieceLayer.BodyFront;

            /// <summary>좌표가 이미 몸 좌표계라 아이템 몸 변형을 걸지 않는다(반대쪽 눈 등).</summary>
            public readonly bool BodyFixed;

            public Shape(string name, Vector3[] points, bool loop, int sortingOrder,
                int swayStart = -1, int swayCount = 0, byte tone = 0, bool filled = false,
                byte surfaces = AccessorySurfaces.Unset, float strokeMult = 0f, float strokeInR = 0f,
                bool noStroke = false, float alpha = 0f, float lineAlpha = 0f,
                int underBack = 0, byte layer = 0, bool bodyFixed = false)
            {
                BodyFixed = bodyFixed;
                Name = name;
                Points = points;
                Loop = loop;
                SortingOrder = sortingOrder;
                SwayStart = swayStart;
                SwayCount = swayCount;
                Tone = tone;
                Filled = filled;
                Surfaces = surfaces;
                StrokeMult = strokeMult;
                StrokeInR = strokeInR;
                NoStroke = noStroke;
                Alpha = alpha;
                LineAlpha = lineAlpha;
                UnderBack = underBack;
                Layer = layer;
            }

            public bool HasSway => SwayStart >= 0 && SwayCount > 0 && Points != null;

            /// <summary>이 조각이 그 표면에 나오는가(0 = Body|Card 규칙 포함).</summary>
            public bool IsOn(AccessorySurface surface) => AccessorySurfaces.IsOn(Surfaces, surface);

            /// <summary>인계본 조각(계약 v2)인가. 그리는 쪽이 이 하나로 규칙을 가른다.</summary>
            public bool IsHandoff => AccessoryStroke.IsHandoff(StrokeInR);

            /// <summary>점열만 바꾼 사본(머리카락 자르기가 쓴다). 흔들 구간은 잘린 도형에서 인덱스가 어긋나므로 넘기지 않는다.</summary>
            public Shape WithPoints(Vector3[] points, bool loop)
                => new Shape(Name, points, loop, SortingOrder, -1, 0, Tone, Filled, Surfaces, StrokeMult, StrokeInR,
                    NoStroke, Alpha, LineAlpha, UnderBack, Layer, BodyFixed);

            /// <summary>채움 면의 레이어 — 자기 윤곽선 <b>바로 아래</b>. 상수를 새로 만들지 않는 이유는,
            /// 도형이 스스로 선언한 레이어와 채움이 어긋날 수 있는 자리를 아예 없애기 위해서다.</summary>
            public int FillSortingOrder => SortingOrder - 1;
        }

        /// <summary>보조색 역할 표식. 호출부에서 <c>tone: Accent</c>로 읽히도록 상수로 둔다.
        /// 값의 정의처는 <see cref="AccessoryTone"/>(에셋·폴백 아이콘과 같은 표)다.</summary>
        internal const byte Accent = AccessoryTone.Accent;

        /// <summary>주색을 어둡게 한 그림자 톤. <b>채운 면 위에 그리는 선</b> 전용이다 —
        /// 같은 주색으로 그리면 면에 묻혀 사라지고, 보조색으로 그리면(망토 주름을 아이보리로 그렸던
        /// 2026-08-30 첫 시안) 천에 붙은 <b>끈</b>처럼 읽힌다. 접힌 자국은 같은 천의 그늘이어야 한다.</summary>
        internal const byte Shade = AccessoryTone.Shade;

        /// <summary>흰 하이라이트(인계본 H). 카드는 밑색 위 사전 합성, 몸은 런타임 알파.</summary>
        internal const byte Highlight = AccessoryTone.Highlight;

        /// <summary>
        /// v1 조각 — 목록 안 <paramref name="index"/>번 조각의 실제 색(아이템 팔레트). 하이라이트는 <see cref="Shape.UnderBack"/>으로
        /// 밑 조각을 찾아 그 역할 위에 합성한다 — 렌더러·초상화·카드 폴백 경로가 <b>이 한 함수</b>를 쓴다.
        /// </summary>
        /// <param name="start">이 아이템의 첫 조각 인덱스. 밑 조각은 같은 아이템 안에서만 찾는다.</param>
        internal static Color ResolveToneColor(List<Shape> shapes, int index, int start, Color primary, Color secondary)
        {
            Shape shape = shapes[index];
            byte underTone = AccessoryTone.Primary;
            if (shape.Tone == Highlight && shape.UnderBack > 0 && index - shape.UnderBack >= start)
            {
                underTone = shapes[index - shape.UnderBack].Tone;
            }
            return AccessoryTone.Resolve(shape.Tone, underTone, primary, secondary);
        }

        // ==================== 인계본 조각(계약 v2)의 색 — 카드·몸·초상화가 같은 표를 본다 ====================

        /// <summary>
        /// 인계본 조각의 색 원천(재질 팔레트 §2, 2026-09-05): 잉크 · 재질색 M/M2 · 머리 채움 · 대비색 · 그룹 알파.
        /// 카드는 <c>AccessoryHandoffPalette.Card</c>, 몸은 <c>AccessoryHandoffPalette.Body</c>로 만든다 — 틴트(<c>WornColor</c>)를 타지 않는다.
        /// <para>★ <b>등급색 칸이 없다</b>(리더 판정 L-1): 등급은 카드 프레임·리본·낱말(<c>UiChrome.RarityColor</c>)의 것이고
        /// 조각 채움에는 0개다. 칸을 두지 않는 것이 「참조하지 않는다」의 구조적 형태다(<c>HandoffRarityAbsenceTests</c>).</para>
        /// </summary>
        internal readonly struct HandoffPalette
        {
            /// <summary>잉크 — 채움 있는 조각의 윤곽 · 채움 위 낱선. 카드 <c>CardIconInk</c>, 몸 = 유저 잉크.</summary>
            public readonly Color Ink;
            public readonly float GroupAlpha;
            /// <summary>머리 채움색(= 캐릭터 잉크 / 카드 바탕). 역할 <see cref="AccessoryTone.HeadInk"/>의 <b>채움</b>.</summary>
            public readonly Color HeadFill;
            /// <summary>잉크의 대비색(<see cref="AccessoryTone.InkContrast"/>) — 반대쪽 눈.</summary>
            public readonly Color InkContrast;
            /// <summary>재질색 M(역할 <see cref="AccessoryTone.Primary"/>) = <c>entry.PrimaryColor</c>. 카드·몸 같은 hex.</summary>
            public readonly Color Material;
            /// <summary>보조 재질색 M2(역할 <see cref="AccessoryTone.Accent"/>) = <c>entry.SecondaryColor</c>.</summary>
            public readonly Color Material2;

            public HandoffPalette(Color ink, float groupAlpha, Color headFill, Color inkContrast, Color material, Color material2)
            {
                Ink = ink;
                GroupAlpha = groupAlpha > 0f ? groupAlpha : 1f;
                HeadFill = headFill;
                InkContrast = inkContrast;
                Material = material;
                Material2 = material2;
            }
        }

        // 팔레트를 만드는 곳(UiChrome 토큰 · 카탈로그 색)은 Interaction/AccessoryHandoffPalette.cs 다 — 이 파일은
        // Tools/ShapeDump 가 UI 없이 컴파일하므로 여기서 UiChrome 을 부르면 그 오프라인 게이트가 죽는다.

        /// <summary>채움의 바탕색(알파 없음) — 재질 팔레트 §2-3. 0 = M · 1 = M2 · 2 = 그늘(M×0.28) ·
        /// HeadInk = 머리 채움색 · InkContrast = 대비색. 등급색·고정색은 여기 없다(L-1 · I-23).</summary>
        internal static Color HandoffFillBase(in Shape s, in HandoffPalette p)
        {
            switch (s.Tone)
            {
                case AccessoryTone.Accent: return p.Material2;
                case AccessoryTone.Shade: return AccessoryTone.Shaded(p.Material);
                case AccessoryTone.HeadInk: return p.HeadFill;
                case AccessoryTone.InkContrast: return p.InkContrast;
                case AccessoryTone.Glass:   // R20 — 바탕(카드 바탕 / 몸 잉크) 위 M2 α 사전 합성. 결과는 불투명(HandoffFillAlpha 가 1 을 낸다).
                    return Color.Lerp(p.HeadFill, p.Material2, s.Alpha > 0f ? s.Alpha : AccessoryCardWash.GradientMeanAlpha);
                default: return p.Material;
            }
        }

        /// <summary>선의 바탕색(알파 없음) — 재질 팔레트 §2-3. 하이라이트 = 흰 · <b>채움 없는 조각</b>은 역할로 재질선
        /// (0 = M · 1 = M2 · 그 밖(잉크 역할) = 잉크) · 채움 있는 조각의 윤곽 = 잉크.</summary>
        internal static Color HandoffLineBase(in Shape s, in HandoffPalette p)
        {
            if (s.Tone == AccessoryTone.Highlight) return Color.white;
            if (!s.Filled)
            {
                if (s.Tone == AccessoryTone.Primary) return p.Material;
                if (s.Tone == AccessoryTone.Accent) return p.Material2;
            }
            return p.Ink;
        }

        /// <summary>채움 알파 — 재질 팔레트 뒤로 몸은 전부 1.0 이고(생성기가 alpha 1 을 굽는다), 카드 전용 워시 조각만 0.16/0.20 이다.
        /// <paramref name="body"/>는 호출부의 뜻을 남기려고 둔다(표면별 덮어쓰기 필드 bodyAlpha 는 2026-09-05 제거).</summary>
        internal static float HandoffFillAlpha(in Shape s, bool body)
            => s.Tone == AccessoryTone.Glass ? 1f : (s.Alpha > 0f ? s.Alpha : AccessoryCardWash.GradientMeanAlpha);

        internal static float HandoffLineAlpha(in Shape s) => s.LineAlpha > 0f ? s.LineAlpha : 1f;

        /// <summary>몸 표면 최종색(알파 포함). 그룹 알파를 곱한다. 카드는 사전 합성이라 이 함수를 쓰지 않는다.</summary>
        internal static void ResolveHandoffBody(in Shape s, in HandoffPalette p,
            out Color fill, out bool hasFill, out Color line, out bool hasLine)
        {
            hasFill = s.Filled;
            Color fb = HandoffFillBase(s, p);
            fill = new Color(fb.r, fb.g, fb.b, Mathf.Clamp01(HandoffFillAlpha(s, true) * p.GroupAlpha));
            hasLine = !s.NoStroke;
            Color lb = HandoffLineBase(s, p);
            line = new Color(lb.r, lb.g, lb.b, Mathf.Clamp01(HandoffLineAlpha(s) * p.GroupAlpha));
        }

        /// <summary>아이템 단위 몸 표면 파라미터. 코드가 좌표를 갖는 자리는 생성 표(<c>WornTransformCode</c>),
        /// 에셋 자리는 <see cref="ItemCatalog.WornTransform"/>. 카드는 이 값을 모른다.</summary>
        internal static AccessoryWornTransform WornTransformOf(EquipmentSlot slot, int item)
            => IsHandoffCode(slot, item) ? WornTransformCode(slot, item) : ItemCatalog.WornTransform(slot, item);

        /// <summary>몸 표면 변형(머리 중심 기준 배율·세로 오프셋·좌우 반전)을 로컬 점열에 <b>제자리에서</b> 건다.
        /// 카드는 파일 그대로라 부르지 않는다. 좌표 정의처(코드/에셋)가 달라도 변형은 여기 하나다.</summary>
        internal static void ApplyBodyTransform(Vector3[] pts, in Rig rig, in AccessoryWornTransform xf)
        {
            if (pts == null || !xf.IsSet) return;
            float s = xf.Scale > 0f ? xf.Scale : 1f;
            float sy = s * (xf.ScaleY > 0f ? xf.ScaleY : 1f);
            float mirror = xf.MirrorX ? -1f : 1f;
            float hc = rig.HeadCenterY;
            float dy = xf.OffsetYInR * rig.HeadRadius;
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = new Vector3(pts[i].x * s * mirror, hc + (pts[i].y - hc) * sy + dy, pts[i].z);
            }
        }

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
        /// <param name="surface">돌려받을 표면. 기본은 몸이다 — 렌더러·초상화·기존 검사가 전부 몸을 본다.
        /// 카드(<c>AccessoryCardIcon</c>)만 <see cref="AccessorySurface.Card"/>를 묻는다.</param>
        internal static void Append(List<Shape> sink, EquipmentSlot slot, int itemIndex, in Rig rig,
            float hatCoverLocalY = float.PositiveInfinity, float strokeHalfWidth = 0f, bool mondayLoosened = false,
            AccessorySurface surface = AccessorySurface.Body)
        {
            if (sink == null || itemIndex < 0) return;
            int start = sink.Count;

            // ★ 계약 v2 — 인계본 16종(HEAD 4 · EYES 4 · BACK 4)은 코드 표(생성 파일)가 좌표를 갖고, 3/4 재저작 도형
            //   (아래 switch)은 <b>이 표면에서 폐기</b>됐다(§14-0 #1). 인계본에 없는 8종 + 머리는 그대로 switch 다.
            if (!AppendHandoff(sink, slot, itemIndex, rig, surface))
            {
                switch (slot)
                {
                    case EquipmentSlot.Head: AppendHead(sink, itemIndex, rig); break;
                    case EquipmentSlot.Eyes: AppendEyes(sink, itemIndex, rig); break;
                    // ★ NECK은 <b>에셋이 형상을 갖는다</b>(B-2 파일럿). 여기서 좌표를 만들지 않는다.
                    //   인계본 4종도 같은 에셋이다(카드 = 몸 = 한 벌, surfaces 0).
                    case EquipmentSlot.Neck:
                        AppendWorn(sink, slot, itemIndex, rig, SortNeck, mondayLoosened, surface);
                        break;
                    case EquipmentSlot.Shoulders: AppendBack(sink, itemIndex, rig); break;
                    case EquipmentSlot.Hair: AppendHair(sink, itemIndex, rig, hatCoverLocalY, strokeHalfWidth); break;

                    // ★ FX/PET은 <b>몸에 붙는</b> 도형이 원래 없다(Interaction/AppearanceShapeBuilder.cs 소관).
                    //   default로 흘려보내면 정상 경로가 매 재구성마다 결함으로 신고된다 — 렌더러는 7개 자리를
                    //   전부 순회하며 이 함수를 부르고, 카드(AccessoryCardIcon)도 FX/PET으로 부른다.
                    //   그래서 "몸에는 아무것도 그리지 않는다"를 <b>명시한다</b>.
                    //
                    //   ★ 2026-09-07 — <b>카드 표면만</b> 갈라진다(§14-12-5 #23). 12종의 64u 아이콘 조각이
                    //   AccessoryShapeBuilder.FxPetCard.cs(생성 파일)에 있고, 그 조각은 전부 Card 전용이라
                    //   몸 요청에는 한 개도 나오지 않는다 — 위 문장은 지금도 참이다.
                    case EquipmentSlot.Fx:
                    case EquipmentSlot.Pet:
                        AppendFxPetCard(sink, slot, itemIndex, rig, surface);
                        break;

                    default:
                        ShapeCoverageGuard.ReportUnknownSlot(slot);
                        break;
                }
            }

            FilterSurface(sink, start, surface);
        }

        /// <summary><paramref name="start"/>부터 끝까지에서 <paramref name="surface"/>에 없는 조각을 제자리에서
        /// 걷어낸다. 할당 없음 — 재구성 경로라 프레임마다 도는 자리는 아니지만 쓰레기를 남길 이유도 없다.</summary>
        private static void FilterSurface(List<Shape> sink, int start, AccessorySurface surface)
        {
            int write = start;
            for (int i = start; i < sink.Count; i++)
            {
                if (!sink[i].IsOn(surface)) continue;
                if (write != i) sink[write] = sink[i];
                write++;
            }
            if (write < sink.Count) sink.RemoveRange(write, sink.Count - write);
        }

        /// <summary>
        /// 인계본 조각 하나(계약 v2) — 좌표는 <b>머리 중심 원점 · R 배수 · y 위 · +x 진행 방향</b>의 (x,y) 쌍
        /// (인계본 <c>icon_to_R</c>/<c>stage_to_R</c> 변환 그대로, EQUIPMENT_HANDOFF_PORT_SPEC §1-1 · §14-0). 로컬 좌표로
        /// 옮기는 자리는 여기 하나다. 생성 파일(<c>AccessoryShapeBuilder.Handoff.cs</c>)만 부른다.
        /// </summary>
        /// <param name="xf">몸 표면 변형(카드 요청이면 <see cref="AccessoryWornTransform.None"/>).</param>
        private static void HandoffPiece(List<Shape> sink, in Rig rig, in AccessoryWornTransform xf, int sortingOrder,
            string name, float[] xyInR, bool loop, bool filled, byte tone, byte surfaces, float strokeMult, float strokeInR,
            bool noStroke, float alpha, float lineAlpha, int underBack, byte layer,
            int swayStart, int swayCount, bool bodyFixed = false)
        {
            int n = xyInR.Length / 2;
            var pts = new Vector3[n];
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            for (int i = 0; i < n; i++)
            {
                pts[i] = rig.F(xyInR[i * 2] * r, hc + xyInR[i * 2 + 1] * r);
            }
            if (!bodyFixed) ApplyBodyTransform(pts, rig, xf);
            sink.Add(new Shape(name, pts, loop, LayerOrder(layer, sortingOrder), swayStart, swayCount, tone, filled, surfaces,
                strokeMult, strokeInR, noStroke, alpha, lineAlpha, underBack, layer, bodyFixed));
        }

        /// <summary>조각 층(<see cref="AccessoryPieceLayer"/>) → 정렬 번호. 슬롯 기본 층은 부르는 쪽이 준다. <b>여기 한 곳</b>이
        /// 데이터의 층 번호를 렌더 순서로 옮긴다 — 팩이 정렬 번호를 직접 적을 수 없는 이유(<c>AppendWorn</c> 문서).</summary>
        internal static int LayerOrder(byte layer, int slotOrder)
        {
            switch ((AccessoryPieceLayer)layer)
            {
                case AccessoryPieceLayer.Back: return SortBack;
                case AccessoryPieceLayer.BodyFront: return SortCapeFront;
                default: return slotOrder;
            }
        }

        /// <summary>생성 파일의 고정색 리터럴(<c>0xRRGGBB</c>) → 불투명 <see cref="Color"/>.</summary>
        private static Color Hex(int rgb)
            => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>
        /// 망토 칼라·걸쇠의 레이어 — <b>몸통 앞</b>(README 「망토 칼라+클래스프 z 4」, §14-6 #8). 앞쪽 팔다리(2)보다
        /// 앞이고 머리 링(4)보다 뒤라 목에 걸친 것(<see cref="SortNeck"/> 7)이 그 위에 온다. 뒤판은 <see cref="SortBack"/>.
        /// </summary>
        internal const int SortCapeFront = 3;

        // ==================== 에셋이 가진 형상 (2026-09-02 B-2 파일럿) ====================

        /// <summary>
        /// 에셋(<see cref="AccessoryDefSO.wornShapes"/>)이 가진 형상을 <paramref name="sink"/>에 넣는다.
        ///
        /// <para>이 함수는 <b>어떤 아이템인지 모른다</b> — 자리 번호로 데이터를 집어 스트림을 돌릴 뿐이다.
        /// 그것이 이 이행의 목적이다: 7번째 목 아이템은 에셋 하나로 추가되고 이 파일은 안 바뀐다.</para>
        ///
        /// <para>레이어(<paramref name="sortingOrder"/>)는 <b>데이터에 넣지 않았다</b>. 그것은 아이템의
        /// 조형이 아니라 <b>자리끼리의 겹침 규칙</b>이라, 팩 작성자가 정하게 두면 남의 아이템이
        /// 내 모자 위로 올라오는 사고를 팩 하나가 일으킬 수 있다.</para>
        ///
        /// <para>데이터가 없으면 <see cref="AppendMissingMarker"/>가 크게 알린다 — 옛 <c>default:</c>와
        /// 같은 자리다. 스트림이 <b>망가진</b> 경우는 여기까지 오지 않는다:
        /// <c>ItemCatalog</c>가 읽을 때 한 번 검사해서 버리고 그때 큰 소리로 남긴다
        /// (렌더 루프에서 프레임마다 같은 에러를 찍으면 로그가 원인을 덮는다).</para>
        /// </summary>
        /// <param name="stateOn">그 자리의 상태 하나. 지금은 줄무늬 타이의 "월요일" 뿐이다.</param>
        /// <param name="surface">몸 요청이면 에셋의 몸 표면 변형(<see cref="ItemCatalog.WornTransform"/>)을 이 아이템의 조각 전부(v1 포함, bodyFixed 제외)에 건다.</param>
        internal static void AppendWorn(List<Shape> sink, EquipmentSlot slot, int item, in Rig rig,
            int sortingOrder, bool stateOn, AccessorySurface surface = AccessorySurface.Body)
        {
            AccessoryWornShapeData[] data = ItemCatalog.WornShapes(slot, item);
            if (data == null || data.Length == 0)
            {
                AppendMissingMarker(sink, slot, item, rig);
                return;
            }

            AccessoryWornFrame frame = Frame(rig);
            AccessoryWornTransform xf = surface == AccessorySurface.Body
                ? ItemCatalog.WornTransform(slot, item) : AccessoryWornTransform.None;
            for (int i = 0; i < data.Length; i++)
            {
                if (!AccessoryWornShapeReader.TryBuild(data[i], frame, stateOn, out Vector3[] points, out _))
                {
                    // 여기 오는 것은 카탈로그 검사를 통과한 스트림이 런타임에 깨졌다는 뜻이다.
                    // 그 자리만 조용히 빠지면 "왜 반쪽만 그려지지"가 되므로 표식으로 알린다.
                    AppendMissingMarker(sink, slot, item, rig);
                    return;
                }

                AccessoryWornShapeData d = data[i];
                // ★ 2026-09-05 R20 N-1 — 아이템 단위 몸 파라미터(wornOffsetYInR 등)는 <b>그 아이템의 모든 조각</b>에 건다. 처음엔 인계본 조각에만
                //   걸었지만 v1 조각(펜던트·반다나)도 착용선 dy 를 받게 됐다. 스트림 데이터는 손대지 않고(비트 골든 유지) 배치만 옮긴다.
                //   몸 좌표 그대로인 조각(bodyFixed)과 카드 요청(xf = None)은 그대로다.
                if (!d.bodyFixed) ApplyBodyTransform(points, rig, xf);
                byte layer = (byte)d.layer;
                sink.Add(new Shape(d.name, points, d.loop, LayerOrder(layer, sortingOrder), d.swayStart, d.swayCount, (byte)d.tone,
                    d.filled, (byte)d.surfaces, d.strokeMult, d.strokeInR, d.noStroke, d.alpha, d.lineAlpha,
                    d.underBack, layer, d.bodyFixed));
            }
        }

        /// <summary>리그 -> 형상 데이터가 가리키는 기저 값들. <b>유일한 변환 지점</b>이다.</summary>
        internal static AccessoryWornFrame Frame(in Rig rig)
            => new AccessoryWornFrame(rig.HeadRadius, rig.TorsoLength, NeckLocalY(rig),
                rig.ShoulderY, rig.HeadCenterY, rig.HipY, rig.Facing);

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

        // ============================================================================
        // ★ R25 베레모·밀짚모자 좌표 (2026-09-06 · design-equipment 확정 → 코디네이터 통보)
        // ============================================================================
        // <b>단위·원점은 인계본 조각과 같다</b>: 머리 중심 원점 · R 배수 · y 위 · +x 진행 방향.
        // 값은 R21 64u 아이콘에 확정 변환(밀짚 u 0.0750 · dy 3.7600 / 베레 u 0.0700 · dy 2.9480)을
        // 건 결과이고, 생성 자는 <c>design/equipment/verify</c>의 <c>handoff.flatten_path(seg 16)</c>다.
        //
        // ★ <b>왜 상수가 아니라 배열인가.</b> 옛 두 종은 «상수 5~7개 → rig.F 로 손으로 조립»이었다.
        //   그 형태로는 2차 곡선과 <b>2층 챙</b>(뒤층/앞층으로 갈라지는 렌즈꼴)을 적을 수 없다 —
        //   R21 재저작본이 요구하는 것이 정확히 그 둘이다.
        //
        // ★ <b>왜 인계본 생성 파일이 아닌가.</b> <c>AccessoryShapeBuilder.Handoff.cs</c>는
        //   <c>Tools/CardShapeGen</c> 생성물이고 역대조가 조각 수를 못박는다(HandoffPiece 113 · 조각 136).
        //   두 종을 거기 넣으면 그 수가 움직인다. 그래서 <b>v1 계약 그대로</b>(strokeInR 0) 이 파일에 둔다 —
        //   빌린 것은 «좌표를 배열로 적는다»는 형식 하나다.
        //
        // ★ <b>카드 좌표는 한 점도 안 바뀐다</b> — 이 배열은 몸 표면 전용이다(v1 두 종은 카드도 같은
        //   조각을 쓰므로, 카드에서는 이 좌표가 그대로 아이콘 프레임에 얹힌다).

        /// <summary>관 — ★ 2026-09-06 R25d로 <b>꼭대기가 +1.96 → +1.60 R</b>이 됐다(밑변 +0.685 고정,
        /// y 만 ×0.717647). x 는 한 점도 안 바뀐다.
        /// <para><b>왜</b>: 모자 6종 실루엣 최소 차이가 <b>천모자↔밀짚모자 1.19획</b>으로 내려앉아
        /// 래칫(1.45획)을 깼다. 두 모자가 갈라지는 각도대는 정수리(85~110°)와 챙 끝(17.5°) 둘인데,
        /// 챙을 넓히면 착용선이 함께 내려가 오늘 밤 확정한 H-2 값이 움직인다. <b>관 높이는
        /// 착용선을 한 자리도 안 건드린다</b> — 착용선을 정하는 것은 챙(가까운 쪽 호)이고,
        /// 관은 머리 꼭대기 쪽에서 이미 필요폭을 크게 넘겨 덮고 있기 때문이다.
        /// 실측: 착용선 +0.4162 · 커버선 여유 +0.0038 <b>불변</b>, 천모자↔밀짚모자 1.19 → <b>1.52획</b>.</para>
        /// <para>조형으로도 이쪽이 옳다 — 넓은 챙 + 낮은 관이 밀짚(보터/썬햇)의 정체이고,
        /// 천모자(정수리 2.12 R)와 높이로 갈린다.</para></summary>
        private static readonly float[] V1Hat_StrawCrown =
        {
            -1.20000f, 0.68500f, -1.19531f, 0.78950f, -1.18125f, 0.88768f, -1.15781f, 0.97956f, -1.12500f, 1.06513f, -1.08281f, 1.14439f,
            -1.03125f, 1.21735f, -0.97031f, 1.28400f, -0.90000f, 1.34434f, -0.82031f, 1.39837f, -0.73125f, 1.44610f, -0.63281f, 1.48752f,
            -0.52500f, 1.52263f, -0.40781f, 1.55143f, -0.28125f, 1.57393f, -0.14531f, 1.59012f, 0.00000f, 1.60000f, 0.14531f, 1.59012f,
            0.28125f, 1.57393f, 0.40781f, 1.55143f, 0.52500f, 1.52263f, 0.63281f, 1.48752f, 0.73125f, 1.44610f, 0.82031f, 1.39837f,
            0.90000f, 1.34434f, 0.97031f, 1.28400f, 1.03125f, 1.21735f, 1.08281f, 1.14439f, 1.12500f, 1.06513f, 1.15781f, 0.97956f,
            1.18125f, 0.88768f, 1.19531f, 0.78950f, 1.20000f, 0.68500f
        };

        /// <summary>띠 — ★ 2026-09-06 R25d로 <b>「올린 띠」 규약</b>(스펙 14-1)으로 되돌렸다.
        /// R21 원문은 관 곡률을 따라 휜 34점 띠였는데, 그것은 두 계약을 동시에 깼다:
        /// (가) 점 <c>i</c>와 <c>n−1−i</c>의 y 차이가 <see cref="AccentBandThicknessRatio"/>여야 하는데
        /// 순서가 <b>윗변 먼저</b>라 −0.4875 R로 읽혔고, (나) 아랫변이 관 밑변에서 유도되지 않아
        /// <c>AccessoryFallbackIconParityTests.몸의_밀짚모자_띠도_관_밑변의_두_끝점이다</c>가 잡았다.
        /// <para>지금은 <b>아랫변 = <see cref="V1Hat_StrawCrown"/>의 첫 점과 끝 점 그대로</b>,
        /// 윗변 = 그것을 0.46 R 수직으로 올린 것이다. 좌표를 손으로 적지 않는다 — 관이 움직이면
        /// 이 네 수도 같이 움직여야 하고, 그 사실을 위 테스트가 매 실행 다시 잰다.</para></summary>
        private static readonly float[] V1Hat_StrawBand =
        {
            -1.20000f, 0.68500f, 1.20000f, 0.68500f, 1.20000f, 1.14500f, -1.20000f, 1.14500f
        };

        /// <summary>챙 렌즈꼴의 <b>먼 쪽</b>(장축 위 호) — 머리 <b>뒤</b>다. 뒤층이라 머리를 못 덮고,
        /// 그래서 H-2 판정(앞층만)의 합집합에 들어가지 않는다.</summary>
        private static readonly float[] V1Hat_StrawBrimFar =
        {
            -2.25000f, 0.68500f, -1.96875f, 0.77289f, -1.68750f, 0.84906f, -1.40625f, 0.91352f, -1.12500f, 0.96625f, -0.84375f, 1.00727f,
            -0.56250f, 1.03656f, -0.28125f, 1.05414f, 0.00000f, 1.06000f, 0.28125f, 1.05414f, 0.56250f, 1.03656f, 0.84375f, 1.00727f,
            1.12500f, 0.96625f, 1.40625f, 0.91352f, 1.68750f, 0.84906f, 1.96875f, 0.77289f, 2.25000f, 0.68500f
        };

        /// <summary>챙 렌즈꼴의 <b>가까운 쪽</b>(장축 아래 호) — 얼굴 앞이라 앞층이다.</summary>
        private static readonly float[] V1Hat_StrawBrimNear =
        {
            2.25000f, 0.68500f, 1.96875f, 0.59711f, 1.68750f, 0.52094f, 1.40625f, 0.45648f, 1.12500f, 0.40375f, 0.84375f, 0.36273f,
            0.56250f, 0.33344f, 0.28125f, 0.31586f, 0.00000f, 0.31000f, -0.28125f, 0.31586f, -0.56250f, 0.33344f, -0.84375f, 0.36273f,
            -1.12500f, 0.40375f, -1.40625f, 0.45648f, -1.68750f, 0.52094f, -1.96875f, 0.59711f, -2.25000f, 0.68500f
        };

        /// <summary>
        /// ★ 2026-09-06 R25d — 챙 <b>렌즈 전체</b>(먼 쪽 호 + 가까운 쪽 호, 양끝 중복 제거).
        /// <b>채움 두 조각이 이 하나를 쓴다.</b> 낱선(호)은 여전히 반쪽씩이다.
        ///
        /// <para><b>왜 반쪽 채움을 버렸나.</b> 반쪽 렌즈는 두께가 0.375 R이라 최대 내접원 반경이
        /// <b>0.1875 R = 0.86획</b>이고, 규칙 1-C(ρ_max ≥ 1획 @배율 0.60)를 두 조각 다 어긴다
        /// (<c>AccessoryFillAreaRuleTests</c> Head 5번). 그런데 <b>두껍게 만들 수가 없다</b>:
        /// 아래로 깊게 파면 H-2b 밑단(+0.3140 → 하한 +0.28)이 바로 깨지고, 위로 올리면
        /// 챙이 관을 뚫는다. <b>반쪽이라는 것 자체가 원인</b>이었다.</para>
        ///
        /// <para><b>왜 화면이 안 바뀌나.</b> 두 조각은 색이 같고(둘 다 주색 채움 · <c>noStroke</c>),
        /// 앞 조각이 뒤 조각을 덮는다. 겹치는 자리는 <b>같은 색 위에 같은 색</b>이다.
        /// 그리고 앞층이 새로 덮는 구간(장축 위, y 0.685~1.06)은 전부 <b>관 채움 아래</b>이거나
        /// (관이 그 높이에서 머리보다 넓다) <b>머리 바깥</b>이다 — 실측으로 확인했다.</para>
        ///
        /// <para><b>H-2 무영향</b>: 앞층 채움 합집합의 <b>아래쪽</b> 경계는 여전히 가까운 쪽 호다.
        /// 착용선 +0.4162 · 밑단 +0.3140 · 커버선 여유 +0.0038 이 한 자리도 안 움직인다.
        /// 새 ρ_max = <b>0.3749 R(1.72획)</b> — 권장선 1.20획도 넘긴다.</para>
        /// </summary>
        private static readonly float[] V1Hat_StrawBrimLens =
        {
            -2.25000f, 0.68500f, -1.96875f, 0.77289f, -1.68750f, 0.84906f, -1.40625f, 0.91352f, -1.12500f, 0.96625f, -0.84375f, 1.00727f,
            -0.56250f, 1.03656f, -0.28125f, 1.05414f, 0.00000f, 1.06000f, 0.28125f, 1.05414f, 0.56250f, 1.03656f, 0.84375f, 1.00727f,
            1.12500f, 0.96625f, 1.40625f, 0.91352f, 1.68750f, 0.84906f, 1.96875f, 0.77289f, 2.25000f, 0.68500f, 1.96875f, 0.59711f,
            1.68750f, 0.52094f, 1.40625f, 0.45648f, 1.12500f, 0.40375f, 0.84375f, 0.36273f, 0.56250f, 0.33344f, 0.28125f, 0.31586f,
            0.00000f, 0.31000f, -0.28125f, 0.31586f, -0.56250f, 0.33344f, -0.84375f, 0.36273f, -1.12500f, 0.40375f, -1.40625f, 0.45648f,
            -1.68750f, 0.52094f, -1.96875f, 0.59711f
        };

        /// <summary>관 위 하이라이트. ★ R25d — <see cref="V1Hat_StrawCrown"/>과 <b>같은 y 배율</b>
        /// (×0.717647, 밑변 +0.685 고정)을 먹였다. 안 먹이면 관이 낮아진 만큼 하이라이트만 공중에 뜬다.
        /// x 는 안 바뀌므로 규칙 1-A의 긴 변(0.5250 R = 1.53획)도 그대로다.</summary>
        private static readonly float[] V1Hat_StrawHighlight =
        {
            -0.67500f, 1.16941f, -0.65098f, 1.19895f, -0.62578f, 1.22702f, -0.59941f, 1.25361f, -0.57188f, 1.27874f, -0.54316f, 1.30239f,
            -0.51328f, 1.32457f, -0.48223f, 1.34529f, -0.45000f, 1.36452f, -0.41660f, 1.38229f, -0.38203f, 1.39859f, -0.34629f, 1.41340f,
            -0.30938f, 1.42675f, -0.27129f, 1.43864f, -0.23203f, 1.44904f, -0.19160f, 1.45798f, -0.15000f, 1.46544f
        };

        /// <summary>베레모 몸 — 오른쪽으로 처진 원반. ★ R25 조형 수정 2건 중 하나가 여기 있다:
        /// 마지막 곡선이 <c>Q54 40 48 42</c>에서 <c>Q58 45 48 37</c>로 바뀌어 <b>처짐이 머리 원반
        /// 밖으로</b> 나갔다(최저점 x = +1.534 R &gt; 원반 1.1842 R). 밑변은 이제 수평이다.</summary>
        private static readonly float[] V1Hat_BeretBody =
        {
            -1.33000f, 0.35800f, -1.36910f, 0.44495f, -1.39891f, 0.53081f, -1.41941f, 0.61558f, -1.43063f, 0.69925f, -1.43254f, 0.78183f,
            -1.42516f, 0.86331f, -1.40848f, 0.94370f, -1.38250f, 1.02300f, -1.34723f, 1.10120f, -1.30266f, 1.17831f, -1.24879f, 1.25433f,
            -1.18563f, 1.32925f, -1.11316f, 1.40308f, -1.03141f, 1.47581f, -0.94035f, 1.54745f, -0.84000f, 1.61800f, -0.73500f, 1.66777f,
            -0.63000f, 1.71206f, -0.52500f, 1.75089f, -0.42000f, 1.78425f, -0.31500f, 1.81214f, -0.21000f, 1.83456f, -0.10500f, 1.85152f,
            0.00000f, 1.86300f, 0.10500f, 1.86902f, 0.21000f, 1.86956f, 0.31500f, 1.86464f, 0.42000f, 1.85425f, 0.52500f, 1.83839f,
            0.63000f, 1.81706f, 0.73500f, 1.79027f, 0.84000f, 1.75800f, 0.94145f, 1.71261f, 1.03578f, 1.66394f, 1.12301f, 1.61198f,
            1.20313f, 1.55675f, 1.27613f, 1.49823f, 1.34203f, 1.43644f, 1.40082f, 1.37136f, 1.45250f, 1.30300f, 1.49707f, 1.23136f,
            1.53453f, 1.15644f, 1.56488f, 1.07823f, 1.58813f, 0.99675f, 1.60426f, 0.91198f, 1.61328f, 0.82394f, 1.61520f, 0.73261f,
            1.61000f, 0.63800f, 1.63270f, 0.53847f, 1.64828f, 0.44987f, 1.65676f, 0.37222f, 1.65813f, 0.30550f, 1.65238f, 0.24972f,
            1.63953f, 0.20487f, 1.61957f, 0.17097f, 1.59250f, 0.14800f, 1.55832f, 0.13597f, 1.51703f, 0.13487f, 1.46863f, 0.14472f,
            1.41313f, 0.16550f, 1.35051f, 0.19722f, 1.28078f, 0.23987f, 1.20395f, 0.29347f, 1.12000f, 0.35800f
        };

        /// <summary>베레모 띠 — ★ R25 조형 수정 2건 중 둘째. <c>M13 37 L48 42 L48.5 37 L13.5 32 Z</c>
        /// (기울어진 띠)에서 <c>M13 37 L48 37 L48.5 32 L13.5 32 Z</c>로 <b>수평화</b>했다.
        /// 기울어진 띠는 앞쪽 끝이 눈 높이까지 내려와 안경을 덮는 주범이었다.
        ///
        /// <para>★ 2026-09-06 R25d — <b>「올린 띠」 규약</b>(스펙 14-1)으로 다시 맞췄다.
        /// R21 원문의 5 SVG유닛(= 0.3500 R) 두께는 우리 두 규칙을 동시에 어긴다:</para>
        /// <list type="number">
        ///   <item><b>규칙 1-C(색면)</b> — 두께 0.3500 R 은 ρ_max 0.1749 R = <b>0.80획</b>이라
        ///     하한 1.00획 미달이고, 다이얼 최소 배율에서는 <b>색면이 통째로 0</b>이 된다
        ///     (윤곽선 펜 0.36843 R 의 절반 0.18422 R &gt; 0.1749 R). 그 배율에서 이 띠는
        ///     보조색이 아니라 윤곽색 한 덩어리가 된다.</item>
        ///   <item><b>「아랫변 + 올린 윗변」 규약</b> — 윗변이 아랫변에서
        ///     <see cref="AccentBandThicknessRatio"/>만큼 <b>정확히</b> 올라가야 한다
        ///     (<c>AccessoryFilledBandRuler.AssertRaisedBandForm</c>).</item>
        /// </list>
        /// <para>새 좌표는 <b>몸에서 유도</b>한다 — 아랫변 두 점은
        /// <see cref="V1Hat_BeretBody"/>의 <b>첫 점과 끝 점</b>(그 폴리곤의 닫힘변 = 수평 밑변)
        /// 그대로이고, 윗변은 그것을 0.46 R 올린 것이다. 새 ρ_max = <b>0.2300 R(1.05획)</b>.</para>
        /// <para><b>왼쪽 윗 꼭짓점만 기울어 있다</b>(−1.42926) — 몸통 왼쪽 변이 밖으로 벌어져서
        /// 그 꼭짓점을 변 위에 물린 값이고, y = 0.81800 에서의 몸 왼쪽 변 x 그 자체다.
        /// 오른쪽은 수직 압출(1.12000 그대로)이라 «기울인 꼭짓점 1개» 계약을 지킨다 —
        /// 오른쪽까지 변에 물리면 2개가 되어 위 자가 «왜 기울였는가»를 물어본다.</para>
        /// <para><b>H-2 무영향</b>: 이 띠는 몸 채움 <b>안</b>에 완전히 들어간다(y 0.358~0.818 구간에서
        /// 몸은 x [−1.429, +1.613]을 덮는다). 앞층 채움 합집합이 안 변하므로 착용선 +0.3602 ·
        /// 밑단 +0.3116 · 커버선 여유 +0.0022 가 <b>한 자리도 안 움직인다</b>.</para></summary>
        private static readonly float[] V1Hat_BeretBand =
        {
            -1.33000f, 0.35800f, 1.12000f, 0.35800f, 1.12000f, 0.81800f, -1.42926f, 0.81800f
        };

        /// <summary>꼭지 — 이 아이템의 최고점(+2.248 R). 액자 상한 2.551 안이고 최고 아이템도 아니다
        /// (R25 이후 최고는 중절모 2.5424).
        ///
        /// <para>★ 2026-09-06 R25c — <b>뿌리만</b> (0.00000, 1.93300) → (−0.02463, 1.71130)으로 내렸다.
        /// <b>꼭지 끝점과 기울기(연직에서 6.340°)는 한 자리도 안 움직인다</b> — 실루엣은 그대로다.
        /// 고친 것은 두 가지 결함이고 둘 다 <b>보이는 부분 밖</b>에 있었다:</para>
        /// <list type="number">
        ///   <item>규칙 1-A — 잉크 사각형 긴 변이 0.3150 R = <b>0.92획</b>이라 하한 1.5획에 미달했다
        ///     (<c>AccessoryStrokeBudgetTests.모든_도형이_획_예산을_지킨다</c> HEAD 4번이 이것으로 빨간불).
        ///     새 값은 0.5367 R = <b>1.56획</b>이다.</item>
        ///   <item>규칙 4 — 옛 뿌리 1.93300은 몸(<see cref="V1Hat_BeretBody"/>) 윗변 1.86031보다
        ///     <b>0.07000 R(0.20획) 위</b>에 떠 있었다. 규칙 4가 허용하는 간격은 «0 또는 ≥1.5획»이고
        ///     0.20획은 그 사이다. 새 뿌리는 몸 안쪽으로 0.14901 R(0.43획) 들어가 <b>간격 0</b>이다.</item>
        /// </list>
        /// <para><b>왜 안 보이는가</b>: 늘린 구간은 전부 <see cref="V1Hat_BeretBody"/> 채움 <b>안</b>이고,
        /// 꼭지는 채움 없는 낱선이라 색이 <c>tone 0 = primary</c>다. 몸의 채움도 같은 primary이므로
        /// (윤곽만 <see cref="FillOutlineColor"/> = Shaded) 같은 색 위에 같은 색을 긋는다.
        /// 아이템 봉투도 안 바뀐다 — 새 뿌리는 몸의 잉크 사각형 안에 있다.</para>
        /// <para><b>이름은 건드리지 마라</b>: <c>HandoffTestGate.SkipIfR25Hat</c>이 "BeretStem"을
        /// R25 표식 조각으로 쓴다(이름이 사라지면 그 게이트가 조용히 초록이 된다).</para></summary>
        private static readonly float[] V1Hat_BeretStem =
        {
            -0.02463f, 1.71130f, 0.03500f, 2.24800f
        };

        private static readonly float[] V1Hat_BeretHighlight =
        {
            -1.05000f, 1.19800f, -1.02293f, 1.24093f, -0.99422f, 1.28222f, -0.96387f, 1.32187f, -0.93188f, 1.35987f, -0.89824f, 1.39624f,
            -0.86297f, 1.43097f, -0.82605f, 1.46405f, -0.78750f, 1.49550f, -0.74730f, 1.52530f, -0.70547f, 1.55347f, -0.66199f, 1.57999f,
            -0.61688f, 1.60487f, -0.57012f, 1.62812f, -0.52172f, 1.64972f, -0.47168f, 1.66968f, -0.42000f, 1.68800f
        };

        /// <summary>
        /// R 배수 (x,y) 쌍 배열 → 이 리그의 로컬 좌표. <see cref="HandoffPiece"/>와 <b>같은 좌표 규약</b>이지만
        /// 계약은 v1 그대로다(<c>strokeInR 0</c>) — 색·알파·획 하한 규칙이 인계본으로 넘어가면 안 되기 때문이다.
        /// <para><paramref name="layer"/>가 뒤층이면 정렬 번호는 <see cref="LayerOrder"/>가 정한다 —
        /// 모자 2층(먼 쪽 챙)이 머리 뒤로 가는 자리다.</para>
        /// </summary>
        private static void HeadV1Piece(List<Shape> sink, in Rig rig, string name, float[] xyInR,
            bool loop, bool filled, byte tone = 0, bool noStroke = false, byte layer = 0, int underBack = 0)
        {
            int n = xyInR.Length / 2;
            var pts = new Vector3[n];
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            for (int i = 0; i < n; i++)
            {
                pts[i] = rig.F(xyInR[i * 2] * r, hc + xyInR[i * 2 + 1] * r);
            }
            sink.Add(new Shape(name, pts, loop, LayerOrder(layer, SortHead), tone: tone, filled: filled,
                noStroke: noStroke, layer: layer, underBack: underBack));
        }

        // ==================== HEAD (모자) ====================

        private static void AppendHead(List<Shape> sink, int item, in Rig rig)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;

            switch (item)
            {
                case HeadCap:
                {
                    sink.Add(new Shape("HatCrown", HatCrown(rig), true, SortHead, filled: true));
                    sink.Add(new Shape("HatBrim", HatBrim(rig), true, SortHead, tone: Accent, filled: true));

                    // 띠 — 아랫변(<see cref="HatBandBaseRatio"/>)에서 위로
                    // AccentBandThicknessRatio만큼 세운 <b>닫힌 채움</b>이고, 반폭은 관에서 그대로
                    // 받는다(규칙 4-a). 「아랫변 + 역순 윗변」 규약(스펙 14-1)을 그대로 따른다.
                    float capBandY = hc + r * HatBandBaseRatio;
                    float capBandHalf = r * HatCrownHalfWidthRatio;
                    sink.Add(new Shape("HatBand", new[]
                    {
                        rig.F(-capBandHalf, capBandY),
                        rig.F(capBandHalf, capBandY),
                        rig.F(capBandHalf, capBandY + r * AccentBandThicknessRatio),
                        rig.F(-capBandHalf, capBandY + r * AccentBandThicknessRatio),
                    }, true, SortHead, tone: Shade, filled: true));
                    break;
                }

                case HeadBeanie:
                {
                    // 커버선 = 접힌 단의 <b>밑변</b>. 곧은 선이라 x에 무관하다(상수 문단 참고).
                    float cuffLine = hc + r * BeanieBandTopRatio;
                    float cuffHalf = r * BeanieBandHalfWidthRatio;
                    float crownTop = cuffLine + r * BeanieCrownHeightRatio;
                    float crownHalf = r * BeanieCrownHalfWidthRatio;

                    // 관의 두 발 = 접힌 자리(낱선)의 두 끝. <b>한 번만</b> 만들어 채움과 낱선이 나눠
                    // 쓴다 — 좌표를 두 번 적으면 한쪽만 고쳐지는 순간 선이 실루엣에서 뜬다(규칙 4-a).
                    Vector3 domeBack = rig.F(-crownHalf, hc + r * BeanieDomeFootRatio);
                    Vector3 domeFront = rig.F(crownHalf, hc + r * BeanieDomeFootRatio);

                    // 관 + 접힌 단이 <b>한 채움</b>이다(2026-09-02 처방 유지). 감쌈(|x| ≥ 0.85R ·
                    // y ≤ 0.05R)은 단 밑변의 두 끝(±1.05R, −0.06R)이 그대로 만든다.
                    sink.Add(new Shape("BeanieCrown", new[]
                    {
                        rig.F(-cuffHalf, cuffLine),
                        rig.F(cuffHalf, cuffLine),
                        rig.F(cuffHalf, hc + r * BeanieCuffTopRatio),
                        domeFront,
                        rig.F(r * 0.50f, hc + r * 1.06f),
                        rig.F(0f, crownTop),
                        rig.F(-r * 0.50f, hc + r * 1.06f),
                        domeBack,
                        rig.F(-cuffHalf, hc + r * BeanieCuffTopRatio),
                    }, true, SortHead, filled: true));

                    // 접힌 자리 = <b>낱선 하나</b>. 관의 두 발을 그대로 받는다(규칙 4-a).
                    // tone은 <b>반드시</b> Shade다: 0이면 관 채움색과 같아져 화면에서 통째로 사라진다.
                    sink.Add(new Shape("BeanieCuff", new[] { domeBack, domeFront },
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
                    // 챙이 볼록렌즈라 중심(+0.19R)에 가까운 앞발이 뒷발보다 조금 높다.
                    float crownFootY = brimY + r * FedoraCrownFootRiseRatio;
                    Vector3 crownBackFoot = rig.F(-crownHalf, crownFootY - r * 0.02f);
                    Vector3 crownFrontFoot = rig.F(crownHalf, crownFootY + r * 0.02f);

                    // ★ 2026-09-03 R12 이식 1단계 — 챙이 <b>볼록렌즈</b>가 됐다(인계본 `fedora` B2).
                    //    중심 +0.19R · 반폭 1.29R · 두께 0.46R(중심)이고 앞뒤 끝은 점으로 수렴한다.
                    //    관 밑변의 두 발은 <b>이 윗변 위의 꼭짓점</b>이라 관이 챙에 파묻힌다(간격 0).
                    //    옛 챙(반폭 1.87R, 뒤 −0.26R까지 처짐)보다 <b>덜 덮는다</b>:
                    //    남는 머리 1.25획 -> <b>1.64획</b> · 면적 21.5% -> <b>34.2%</b>(배율 0.60).
                    //    감쌈(|x| ≥ 0.85R · y ≤ 0.05R)은 밑변의 (+0.99R, −0.01R)이 만든다.
                    sink.Add(new Shape("FedoraBrim", new[]
                    {
                        rig.F(-r * FedoraBrimBackRatio, hc + r * 0.15f),   // 뒤 끝 — 점으로 수렴
                        crownBackFoot,
                        rig.F(r * 0.19f, hc + r * 0.36f),                  // 렌즈 중심(가장 두껍다)
                        crownFrontFoot,
                        rig.F(r * FedoraBrimFrontRatio, hc + r * 0.15f),   // 앞 끝 — 점으로 수렴
                        rig.F(r * 0.99f, hc - r * 0.01f),                  // 감쌈을 만드는 자리
                        rig.F(r * 0.19f, hc - r * 0.10f),                  // 잉크 밑단
                        rig.F(-r * 0.61f, hc - r * 0.01f),
                    }, true, SortHead, filled: true));

                    // ★ 크리스(관 꼭대기의 눌린 자국)가 <b>돌아왔다</b> — 2026-09-01에 없앤 이유는
                    //    "관이 낮아 V가 관을 두 쪽으로 가르는 <b>선</b>이 된다"였는데, 이식으로 관 높이가
                    //    0.72R -> 1.04R이 되면서 크리스를 <b>채움의 윤곽선 노치</b>로 낼 수 있게 됐다.
                    //    노치의 두 변은 각각 0.476R = <b>1.38획</b>(규칙 1 하한 1.0획)이라 예산 안이다.
                    float crownTop = brimY + r * FedoraCrownHeightRatio;
                    sink.Add(new Shape("FedoraCrown", new[]
                    {
                        crownBackFoot,
                        rig.F(-crownHalf, hc + r * 0.86f),
                        rig.F(-r * 0.30f, crownTop),
                        rig.F(0f, hc + r * 1.01f),          // 크리스 바닥
                        rig.F(r * 0.30f, crownTop),
                        rig.F(crownHalf, hc + r * 0.86f),
                        crownFrontFoot,
                    }, true, SortHead, filled: true));

                    // 띠 — 아랫변은 관 밑변 <b>그 자체</b>(간격 0)고, 거기서 위로
                    // AccentBandThicknessRatio만큼 세운 <b>닫힌 채움</b>이다(스펙 14-1, 2026-09-03).
                    // 규칙 4가 금지하는 것은 0 < 간격 < 1획이지 겹침이 아니다. 관 높이로는 1.5획을
                    // 띄우는 것이 산술적으로 불가능하다.
                    // ★ 윗변 좌표를 새로 적지 않고 관 밑변의 <b>같은 식</b>에 두께를 더한다 —
                    //   관이 움직이면 띠가 따라간다.
                    sink.Add(new Shape("FedoraBand", new[]
                    {
                        crownBackFoot,
                        crownFrontFoot,
                        rig.F(crownHalf, crownFootY + r * 0.02f + r * AccentBandThicknessRatio),
                        rig.F(-crownHalf, crownFootY - r * 0.02f + r * AccentBandThicknessRatio),
                    }, true, SortHead, tone: Accent, filled: true));
                    break;
                }

                case HeadCrown:
                {
                    float baseY = hc + r * CrownBaseRatio;
                    float half = r * CrownHalfWidthRatio;
                    // 좌우 대칭이라 facing에 무관하게 같은 그림이 나온다(33-2-1 #4, 정상).
                    // <b>채운 닫힌 도형</b>이라 봉우리 세 개가 꼭짓점으로 수렴한다 — 선으로 그리면
                    // 둥근 캡이 끝을 뭉갠다(옛 왕관이 그 상태였다).
                    // ★ 2026-09-03 R12 이식 1단계 — 몸이 <b>테 위로 올라앉았다</b>(밑변 0.02 -> 0.27R).
                    //    밑이 뚫린 성질은 여전히 채움이 아니라 커버선 +∞가 보장한다.
                    sink.Add(new Shape("CrownBody", new[]
                    {
                        rig.F(-half, baseY),
                        rig.F(-r * 0.84f, hc + r * 1.36f),
                        rig.F(-r * 0.42f, hc + r * 0.82f),
                        rig.F(0f, hc + r * 1.52f),
                        rig.F(r * 0.42f, hc + r * 0.82f),
                        rig.F(r * 0.84f, hc + r * 1.36f),
                        rig.F(half, baseY),
                    }, true, SortHead, filled: true));

                    // 테 — ★ 2026-09-03 R12 이식 1단계로 <b>곧은 띠</b>가 됐다(§5-5-1 처방 #9).
                    // 옛 테는 몸의 밑변 네 점을 받아 사다리꼴이었고, 그 바깥 끝면이 몽당변(0.946획)이자
                    // 규칙 1-C 미달(0.98획)이었다. 지금은 아랫변 2점 + 그것을 AccentBandThicknessRatio
                    // 만큼 올린 역순 윗변 2점이다(스펙 14-1 「올린 띠」 규약 그대로).
                    float rimHalf = r * CrownRimHalfWidthRatio;
                    float rimY = hc + r * CrownRimBaseRatio;
                    sink.Add(new Shape("CrownRim", new[]
                    {
                        rig.F(-rimHalf, rimY),
                        rig.F(rimHalf, rimY),
                        rig.F(rimHalf, rimY + r * AccentBandThicknessRatio),
                        rig.F(-rimHalf, rimY + r * AccentBandThicknessRatio),
                    }, true, SortHead, tone: Accent, filled: true));
                    break;
                }

                case HeadBeret:
                {
                    // ★ 2026-09-06 R25 — 옛 「뒤로 처진 3/4 덩어리 2조각」을 폐기하고 R21 재저작본으로
                    //   갈아탔다. 정체가 바뀐 것이 아니라 <b>정체가 제대로 그려진 것</b>이다: 정면에서
                    //   본 납작한 원반이 오른쪽으로 처지고, 띠 위에 몸이 앉고, 꼭지가 선다.
                    //   (옛 주석 "밴드도 꼭지도 없다 — 그 둘이 없는 것이 정체다"는 <b>지금 거짓</b>이다.
                    //    그 문장은 상수 7개로 조립하던 시절의 것이고, 그 도형은 H-2 를 통과한 적이 없다 —
                    //    앞층 채움이 머리 꼭대기를 한 줄도 못 덮었다.)
                    HeadV1Piece(sink, rig, "BeretBody", V1Hat_BeretBody, loop: true, filled: true);
                    HeadV1Piece(sink, rig, "BeretRim", V1Hat_BeretBand, loop: true, filled: true,
                        tone: Accent, noStroke: true);
                    HeadV1Piece(sink, rig, "BeretStem", V1Hat_BeretStem, loop: false, filled: false);
                    HeadV1Piece(sink, rig, "BeretHighlight", V1Hat_BeretHighlight, loop: false, filled: false,
                        tone: Highlight, underBack: 3);
                    break;
                }

                case HeadStraw:
                {
                    // ★ 2026-09-06 R25 — 중절모와 같은 <b>2층 챙</b>(렌즈꼴을 장축에서 갈라 먼 쪽은
                    //   머리 뒤, 가까운 쪽은 얼굴 앞)으로 재저작했다. 옛 1층 챙은 머리 <b>위</b>에
                    //   얹혀 안경을 78%까지 지웠다.
                    //   자른 선(장축)은 <b>그리지 않는다</b> — 채움은 noStroke 로 두고 바깥 호만
                    //   따로 낱선으로 낸다. 그리면 챙 한가운데를 가로지르는 금이 생긴다.
                    //   ★ R25d — <b>채움 두 조각은 렌즈 전체</b>(V1Hat_StrawBrimLens)를 쓴다. 반쪽 채움은
                    //   두께 0.375 R이라 규칙 1-C(색면 ≥ 1획)를 구조적으로 못 넘겼고, 두껍게 하려면
                    //   H-2b 밑단이나 관을 깨야 했다. 낱선(호)만 반쪽으로 남는다 — 그려야 하는 것은
                    //   «호 두 개»이지 «렌즈 윤곽»이 아니다(가까운 쪽 호는 앞, 먼 쪽 호는 뒤).
                    HeadV1Piece(sink, rig, "StrawBrimFar", V1Hat_StrawBrimLens, loop: true, filled: true,
                        noStroke: true, layer: (byte)AccessoryPieceLayer.Back);
                    HeadV1Piece(sink, rig, "StrawBrimFarArc", V1Hat_StrawBrimFar, loop: false, filled: false,
                        layer: (byte)AccessoryPieceLayer.Back);
                    HeadV1Piece(sink, rig, "StrawBrimNear", V1Hat_StrawBrimLens, loop: true, filled: true,
                        noStroke: true);
                    HeadV1Piece(sink, rig, "StrawBrimNearArc", V1Hat_StrawBrimNear, loop: false, filled: false);
                    HeadV1Piece(sink, rig, "StrawCrown", V1Hat_StrawCrown, loop: true, filled: true);
                    HeadV1Piece(sink, rig, "StrawBand", V1Hat_StrawBand, loop: true, filled: true,
                        tone: Accent, noStroke: true);
                    // underBack 2 = 이 목록에서 두 칸 앞 = StrawCrown. 하이라이트가 실제로 얹히는 면이고,
                    // 그 조각의 역할(주색)이 흰 선의 밑색이 된다(ResolveToneColor).
                    HeadV1Piece(sink, rig, "StrawHighlight", V1Hat_StrawHighlight, loop: false, filled: false,
                        tone: Highlight, underBack: 2);
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
                    // 60도(뒤 렌즈 index 2) / 120도(앞 렌즈 index 4) = 두 렌즈의 <b>안쪽 위</b> 꼭짓점.
                    // ★ 2026-09-05 — 30도/150도(index 1·5)에서 한 칸 위로 올렸다. 이유는
                    //   <see cref="RoundBridgeRiseRatio"/> 문서.
                    sink.Add(new Shape("RoundBridge", new[]
                    {
                        back[2], rig.F(0f, cy + r * RoundBridgeRiseRatio), front[4],
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
                    //
                    // ★ 배수 ×1.15 — 이것은 <b>머리를 도는 띠</b>라 인계본 「띠·기둥」군(1.10~1.40)이다
                    //   (배낭 손잡이 1.10 · 고글 머리끈 1.40 사이, 슬림한 끈). §14-14-5.
                    //   <b>천장은 ×1.2064</b>다 — 끈-드러난 눈 1.5획 규칙이 배수를 먹는다
                    //   (여백 = (1.1032 − 0.5m)W ≥ 0.5W). 올릴 때 이 산술을 먼저 다시 풀어라.
                    sink.Add(new Shape("PatchStrap", new[]
                    {
                        HeadPolar(rig, PatchStrapDegrees, PatchStrapReachRatio),
                        coverTopBack, coverBottomBack,
                        HeadPolar(rig, 360f - PatchStrapDegrees, PatchStrapReachRatio),
                    }, false, SortEyes, strokeMult: 1.15f));

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


        // ==================== NECK (넥타이) — 형상이 <b>에셋으로 내려간 자리</b> ====================
        //
        // ★ 2026-09-02 DLC 이행 B-2 파일럿. 여기 있던 것:
        //     AppendNeck의 아이템 case 6개(나비넥타이/줄무늬타이/목도리/방울/펜던트/반다나)
        //     + BowTieLeftWing/RightWing/Knot + CollarCurve + CollarLowLocalY + 비율 상수 26개.
        //   전부 Resources/Items/equip_neck_*.asset의 wornShapes로 옮겼고, 이 파일은 그 자리에
        //   대해 <b>아무 좌표도 모른다</b>. 7번째 목 아이템은 이제 에셋 하나로 추가된다(원칙 4).
        //
        // 옮기면서 좌표가 <b>비트 하나도</b> 바뀌지 않았다는 증거:
        //   Tests/EditMode/WornShapeDataGoldenTests.cs + Tests/EditMode/Golden/NeckWornShapeGolden.txt
        //   (릭 10벌 × 6종 × 월요일 on/off의 모든 점을 float 비트 그대로 대조한다)
        //
        // 남은 것은 <b>부착 기준선</b> <see cref="NeckLocalY"/> 하나뿐이다 — 그것은 아이템의 조형이
        // 아니라 리그의 사실이고, 자리가 비었을 때 표식을 어디 띄울지도 그 선이 정한다.
        // 형상 데이터는 그 선을 AccessoryWornBasis.NeckLine 번호로 가리키기만 한다.

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
                {
                    // 밑단 5점(인덱스 2~6)이 흔들린다 — "늘 가는 방향의 반대쪽으로 날린다".
                    // 재설계로 밑단이 앞쪽까지 벌어지면서 흔들 구간도 뒤 3점 -> 밑단 전체가 됐다.
                    Vector3[] outline = CapeOutline(rig);
                    sink.Add(new Shape("CapeOutline", outline, true, SortBack,
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
                    sink.Add(new Shape("CapeYoke", CapeShoulderYoke(rig, outline), true, SortBack,
                        tone: Accent, filled: true));
                    break;
                }

                case BackLongCape:
                {
                    Vector3[] outline = CapeOutline(rig, LongCapeLengthRatio, LongCapeSpreadRatio,
                        LongCapeHemWaveRatio, LongCapeFrontSpreadRatio, LongCapeHemNotchRatio);
                    sink.Add(new Shape("CapeOutline", outline,
                        true, SortBack, swayStart: 2, swayCount: 5, filled: true));
                    // ★ 주름의 끝 x를 <b>명시</b>한다. 기본 유도값(0.42 / 0.64)은 제비꼬리 골이 파인
                    //   자리를 그대로 지나가 주름 끝이 천 <b>바깥</b>(갈라진 틈)에 떨어진다.
                    sink.Add(new Shape("CapeFold",
                        CapeFold(rig, LongCapeLengthRatio, LongCapeSpreadRatio, 0.35f, 0.80f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    sink.Add(new Shape("CapeFold2",
                        CapeFold(rig, LongCapeLengthRatio, LongCapeSpreadRatio, 0.72f, 0.96f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade));
                    sink.Add(new Shape("CapeYoke", CapeShoulderYoke(rig, outline), true, SortBack,
                        tone: Accent, filled: true));
                    break;
                }

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
                {
                    // 짧은 망토와 <b>같은 도형·다른 비율</b>. 짧고 앞쪽까지 덮는 것이 정체다.
                    Vector3[] outline = CapeOutline(rig, PonchoLengthRatio, PonchoSpreadRatio,
                        PonchoHemWaveRatio, PonchoFrontSpreadRatio);
                    sink.Add(new Shape("CapeOutline", outline,
                        true, SortBack, swayStart: 2, swayCount: 5, filled: true));
                    // ★ 배수 ×0.70 — 천의 <b>접힘선</b>(표면 위 정보선)이라 짧은망토 `S2` · 긴망토 `S3/S4`와
                    //   같은 값이다. 판초는 그 둘과 「같은 도형·다른 비율」이므로 굵기까지 같아야 한 가족으로
                    //   읽힌다. 두 접힘선의 위계를 가르지 않는다(긴망토도 S3 = S4). §14-14-5.
                    sink.Add(new Shape("CapeFold",
                        CapeFold(rig, PonchoLengthRatio, PonchoSpreadRatio, 0.35f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade, strokeMult: 0.70f));
                    sink.Add(new Shape("CapeFold2",
                        CapeFold(rig, PonchoLengthRatio, PonchoSpreadRatio, 0.72f), false, SortBack,
                        swayStart: 1, swayCount: 1, tone: Shade, strokeMult: 0.70f));
                    sink.Add(new Shape("CapeYoke", CapeShoulderYoke(rig, outline), true, SortBack,
                        tone: Accent, filled: true));
                    break;
                }

                case BackFairyWings:
                {
                    // 날개와 같은 구성의 작고 둥근 변주. 천이 아니므로 <b>흔들 점을 선언하지 않는다</b>.
                    float sy = rig.ShoulderY;
                    // ★ 배수 ×1.30 — 인계본 날개의 등뼈 `RB4`는 <b>채움 조각</b>(폭 0.20 R)인데 요정날개는
                    //   그것을 <b>선 하나</b>로 대신한다 → 「띠·기둥」군의 위쪽. 0.12432 × 1.30 = 0.1616 R 로
                    //   인계본 등뼈 폭의 81%다. 같게(1.61배) 두지 않는 이유는 이 아이템이 「작고 둥근 변주」라서고,
                    //   1.61은 ×1.5 천장 밖이기도 하다. §14-14-5.
                    sink.Add(new Shape("WingSpine", new[]
                    {
                        rig.F(0f, sy + r * WingRootRiseRatio),
                        rig.F(0f, sy - rig.TorsoLength * FairyWingSpineDropScale),
                    }, false, SortBack, tone: Accent, strokeMult: 1.30f));

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
        /// <summary>가르마 가닥. ★ 2026-09-05 R13-P3 — <b>폭축만 ×1.40</b>했다(길이축 불변).
        /// 채움 폭 ρ는 수직폭에 비례하므로 폭 0.40 → 0.56 R이 규칙 1-C를 닫는다:
        /// ρ 0.1938R(0.89획) → <b>0.2631R(1.21획)</b>, 그리고 배율 0.60의 「양끝 꺾임 0.93획」도 사라진다.
        /// <para>넓히는 방향은 네 점이 만드는 <b>짧은 축</b>(≈(0.9972, −0.0748), 중심 (0.15, 1.085))이고,
        /// 위/아래 두 변의 기울기가 보존되도록 그 축 성분만 1.40배 했다. 아래 값은 그 결과를
        /// 소수 둘째 자리로 정리한 것이다(정확값과 최대 0.0023R 차, ρ는 오히려 0.0004R 넉넉하다).
        /// 꼭대기 1.57R은 초상화 액자 1.75R 안이다.</para></summary>
        private static readonly Vector2[] NeatPart =
        {
            new Vector2(-0.27f, 1.57f), new Vector2(0.29f, 1.54f),
            new Vector2(0.57f, 0.59f), new Vector2(0.01f, 0.64f),
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

        /// <summary>이마를 가로지르는 앞머리 <b>띠</b>. 아랫변은 <see cref="BowlSilhouette"/>의 안쪽
        /// 경계와 <b>같은 식</b>을 쓴다 — 두 벌로 적어 두면 한쪽만 고쳐 선이 어긋난다.
        /// <para>★ 2026-09-03(스펙 14-1) 낱선 → <b>닫힌 채움</b>. 윗변은 같은 x를 <b>역순</b>으로
        /// 되짚어 고리를 닫는다 — 순서대로 이으면 띠가 자기 자신을 가로지른다.</para></summary>
        internal static Vector3[] BowlFringeLine(in Rig rig)
        {
            var pts = new Vector3[(BowlFringeSegments + 1) * 2];
            for (int i = 0; i <= BowlFringeSegments; i++)
            {
                float x = Mathf.Lerp(-BowlSideHalfWidthRatio, BowlSideHalfWidthRatio,
                    i / (float)BowlFringeSegments);
                pts[i] = rig.F(x * rig.HeadRadius, rig.HeadCenterY + BowlFringeLineRatio * rig.HeadRadius);
                pts[pts.Length - 1 - i] = rig.F(x * rig.HeadRadius, rig.HeadCenterY
                    + (BowlFringeLineRatio + AccentBandThicknessRatio) * rig.HeadRadius);
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
                    // 식별 특징 — 자른 앞머리 띠. <b>아랫변</b>이 실루엣의 안쪽 경계와
                    // <b>정확히 겹친다(간격 0)</b>: 규칙 4가 "최악"이라고 못박은 것은
                    // 0 &lt; 간격 &lt; 1획이지 겹침이 아니다.
                    // ★ 2026-09-03 낱선 → 채운 띠(스펙 14-1). 윗변은 아랫변에서 0.46R 위이므로
                    //   실루엣 경계까지의 거리가 0이 아니라 0.46R이다 — 그 값은 채운 도형의 윤곽선
                    //   예산(1pt 하한, 0.21818R)으로 재야 1.5획 밖이다. 낱선 예산(2pt, 0.3439R)으로
                    //   재면 1.34획으로 읽혀 금지 구간 안으로 오판된다.
                    _hairScratch.Add(new Shape("HairFringe", BowlFringeLine(rig), true, SortHair,
                        tone: Accent, filled: true));
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
            sink.Add(shape.WithPoints(kept, loop));
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
        /// 망토 3종이 공유하는 <b>서명 디테일</b> — 어깨 요크(보조색·채움). 자세한 근거는
        /// <see cref="CapeYokeDepthRatio"/> 문서.
        /// <para>세 망토가 <b>같은 규칙 하나</b>를 쓴다: "자기 윤곽의 위 <see cref="CapeYokeDepthRatio"/>
        /// 구간". 좌표를 각자 적지 않으므로 "같은 옷의 변주"가 그림에서 유지되고(규칙 4-a),
        /// 밑단 길이·폭이 달라도 요크가 그 망토의 실제 폭을 그대로 따라간다.</para>
        /// </summary>
        /// <param name="outline"><see cref="CapeOutline"/>가 만든 그 배열. 인덱스 0 = 옷깃 앞,
        /// 1 = 옷깃 뒤, 2 = 밑단 뒤끝, 마지막 = 밑단 앞끝이라는 규약에 의존한다(제비꼬리 밑단도 같다 —
        /// 점 개수를 일부러 맞춰 둔 이유가 이것이다).</param>
        internal static Vector3[] CapeShoulderYoke(in Rig rig, Vector3[] outline)
        {
            float yokeY = CapeCollarLocalY(rig) - rig.HeadRadius * CapeYokeDepthRatio;
            Vector3 collarFront = outline[0];
            Vector3 collarBack = outline[1];
            Vector3 hemBack = outline[2];
            Vector3 hemFront = outline[outline.Length - 1];
            return new[]
            {
                collarFront,
                collarBack,
                CutEdgeAtY(collarBack, hemBack, yokeY),
                CutEdgeAtY(collarFront, hemFront, yokeY),
            };
        }

        /// <summary>변 <paramref name="top"/>→<paramref name="bottom"/>이 높이 <paramref name="y"/>를
        /// 지나는 점. 변이 그 높이에 못 미치면 끝점으로 잘린다(t를 [0,1]로 문다).
        /// <para>두 끝점은 이미 <see cref="Rig.F"/>를 거친 값이라 진행 방향 부호가 들어 있다 —
        /// 성분 보간이라 그 부호가 그대로 보존된다.</para></summary>
        private static Vector3 CutEdgeAtY(Vector3 top, Vector3 bottom, float y)
        {
            float span = top.y - bottom.y;
            float t = span <= 1e-6f ? 0f : Mathf.Clamp01((top.y - y) / span);
            return new Vector3(Mathf.Lerp(top.x, bottom.x, t), Mathf.Lerp(top.y, bottom.y, t), top.z);
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

        /// <summary>
        /// ★ R17d(2026-09-05, 리더 채택) — 밑단을 <b>발목선 아래로 내려보내지 않는다.</b>
        ///
        /// <para>긴망토 밑단은 발목 잉크 원 윗변(−9.055 R)이라 서 있을 때는 발목선(다리 끝점 = 루트 로컬 y 0) 위 0.285 R 에
        /// 있다. 그런데 무릎앉아 착지(<c>LandingCrouch</c>)는 발을 바닥에 두고 <b>몸만</b> 내려앉는다
        /// (<c>StickmanPoseAnimator.ComputeFootGroundingOffset</c>, 최대 −2.46 R) — 망토는 몸에 붙어 있으므로 밑단이
        /// 발목선 아래로 2.18 R 뚫린다. 루트를 띄우는 대안은 기각됐다(몸이 2.46 R 뜬다). 처방은 이 한 줄: 밑단 정점의 y 를
        /// 발목선(컨테이너 좌표로 옮긴 값)으로 받친다.</para>
        ///
        /// <para>순수 함수다 — <paramref name="floorY"/>는 부르는 쪽(<c>CharacterAccessoryRenderer.TickHemMotion</c>)이
        /// 「발목선 − 몸 오프셋」으로 만든다. 발목선은 리그 규약(로컬 원점 = 발바닥)에서 오고 숫자를 따로 적지 않는다.
        /// 몸 오프셋이 0 이면 어떤 점도 안 움직인다(밑단이 발목선 위에 있으므로) — <c>CapeHemFloorClampTests</c>가
        /// 양쪽을 잠근다.</para>
        /// </summary>
        /// <returns>받친 점의 수(0 = 아무것도 안 바꿨다).</returns>
        internal static int ClampAboveFloor(Vector3[] pts, float floorY) => PressHemToFloor(pts, floorY, 0f, 0f);

        /// <summary>
        /// ★★ 2026-09-05 design-motion 정정(docs/UX_MOTION_FAN_AND_CAPE.md §2-5) — <b>바닥은 컨테이너 안에서 기운 선이다.</b>
        /// 웅크릴 때 상체가 피치 θ 로 함께 도는데, 망토 컨테이너는 상체 회전을 그대로 받으므로 화면의 수평 발바닥선은 컨테이너 좌표에서
        /// <c>floorY(x) = intercept + slope·x</c> 다. 스칼라 하나로 받치면 22° 에서 앞자락 3.00 pt 가 남은 채 뚫린다(원래 신고와 같은 결함).
        /// <para>유도: 컨테이너 점 p 의 루트 로컬 y = HipY/s + [R·(p/s − hip)]_y + b, R 의 기저 벡터 y 성분을 <paramref name="sinPitch"/> =
        /// (R·right).y, <paramref name="cosPitch"/> = (R·up).y 라 두면 y_root = HipY/s + sin·p.x/s + cos·(p.y/s − HipY/s) + b ≥ 0 ⇔
        /// p.y ≥ HipY + (−HipY − s·b − sin·p.x)/cos. θ = 0 이면 −s·b — 옛 스칼라식과 정확히 같다(그 식은 θ=0 의 특수해였다).</para>
        /// </summary>
        /// <param name="hipY">엉덩이 높이(월드 단위, 컨테이너 회전 축).</param>
        /// <param name="rootScale">루트 배율 s.</param>
        /// <param name="bodyOffsetRootLocal">몸 오프셋 b(루트 로컬, 음수 = 내려앉음).</param>
        internal static void HemFloorLine(float hipY, float rootScale, float bodyOffsetRootLocal, float sinPitch, float cosPitch,
            out float intercept, out float slope)
        {
            float c = Mathf.Max(0.05f, cosPitch);
            intercept = hipY + (-hipY - rootScale * bodyOffsetRootLocal) / c;
            slope = -sinPitch / c;
        }

        /// <summary>천이 바닥에 닿아 쌓일 때 눌린 깊이 δ 만큼 가로로 퍼지는 비율(§2-6 (가), 리더 확인 항목 L-5). 0 = 칼로 잘린 판자,
        /// 1 = 길이 보존(과장). 최대 웅크림에서 밑단 폭 +73.8%.</summary>
        internal const float HemPressSpread = 0.55f;

        /// <summary>눌린 점의 흔들 진폭 감쇠 — <c>1 − clamp01(δ / δ_ref)</c>(§2-6 (나)). δ_ref = 그 선의 보행 진폭 p-p(2 × A_walk) —
        /// 새 상수가 아니라 <c>SwayAmplitudeRatio</c>에서 나온다. 바닥에 누운 점이 좌우로 미끄러지지(스케이트) 않게 한다.</summary>
        internal static float HemPressDamping(float depth, float depthRef)
            => depthRef <= 0f ? (depth > 0f ? 0f : 1f) : 1f - Mathf.Clamp01(depth / depthRef);

        /// <summary>점이 바닥선 아래로 내려간 깊이(0 이상). 바닥선은 <see cref="HemFloorLine"/>.</summary>
        internal static float HemPressDepth(in Vector3 p, float intercept, float slope)
            => Mathf.Max(0f, intercept + slope * p.x - p.y);

        /// <summary>
        /// ★ 2026-09-06 perf-doc — <b>이 밑단이 이 바닥선에 닿을 수 있는가</b>(보수적 상한, 점 개수와 무관한 O(1)).
        ///
        /// <para><b>거짓이면 <see cref="PressHemToFloor"/>가 반드시 0을 돌려준다</b>(닿는 점이 하나도 없다).
        /// 참이면 「닿을 수도 있다」일 뿐이고 실제 여부는 모른다 — 그래서 <b>한쪽으로만 안전한</b> 판정이며,
        /// 부르는 쪽은 참일 때 언제나 원래 경로를 그대로 돌면 된다.</para>
        ///
        /// <para><b>증명</b>: 바닥선은 <c>floor(x) = intercept + slope·x</c>이고 눌림 깊이는 <c>floor(p.x) − p.y</c>다.
        /// 도형의 모든 점이 <c>|p.x| ≤ maxAbsX</c> · <c>p.y ≥ minY</c>를 만족하므로
        /// <c>floor(p.x) − p.y ≤ intercept + |slope|·maxAbsX − minY</c>. 우변이 0 이하면 어떤 점도 깊이가 양수일 수 없다.
        /// (<c>slope</c>의 부호를 모르므로 절댓값을 쓴다 — 좌우 어느 쪽으로 기울어도 같은 상한이 성립한다.)</para>
        ///
        /// <para>★ <paramref name="minY"/>/<paramref name="maxAbsX"/>는 <b>지금 눌리려는 그 점 배열</b>에서 와야 한다.
        /// 구워진 원본(Base)의 값을 흔들린 버퍼에 대고 쓰면 상한이 깨진다 — 부르는 쪽
        /// (<c>CharacterAccessoryRenderer.TickHemMotion</c>)이 <b>기류·보행 변위가 둘 다 0인 프레임</b>에서만
        /// 이 판정을 쓰는 이유가 그것이다(그 프레임에는 버퍼가 원본과 같다).</para>
        /// </summary>
        /// <param name="minY">그 점 배열의 최저 y.</param>
        /// <param name="maxAbsX">그 점 배열의 |x| 최댓값.</param>
        internal static bool HemCanReachFloor(float intercept, float slope, float minY, float maxAbsX)
            => intercept + Mathf.Abs(slope) * maxAbsX - minY > 0f;

        /// <summary>밑단을 기운 바닥선에 눕히고(δ 만큼 y 를 올린다) 눌린 깊이만큼 가로로 퍼뜨린다(<paramref name="spread"/>, §2-6 (가)).
        /// spread 0 · slope 0 이면 옛 <see cref="ClampAboveFloor"/>와 같다. 순수 함수 — <c>CapeHemFloorClampTests</c>가 잠근다.</summary>
        /// <returns>받친 점의 수.</returns>
        internal static int PressHemToFloor(Vector3[] pts, float intercept, float slope, float spread)
        {
            if (pts == null) return 0;
            int moved = 0;
            for (int i = 0; i < pts.Length; i++)
            {
                float floor = intercept + slope * pts[i].x;
                float depth = floor - pts[i].y;
                if (depth <= 0f) continue;
                pts[i].y = floor;
                pts[i].x += Mathf.Sign(pts[i].x) * depth * spread;
                moved++;
            }
            return moved;
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

        /// <summary>
        /// 같은 아이템 안 채움 조각의 <b>로컬 z 단차</b>(<see cref="FillDepthStep"/>의 유일한 소비자).
        ///
        /// <para>★ 2026-09-06 — 함수로 뺀 이유는 <b>두 렌더러가 각자 구현하다 갈라졌기 때문</b>이다.
        /// 몸(<c>CharacterAccessoryRenderer.AddFill</c>)에는 이 단차가 있었고 초상화
        /// (<c>CharacterPortraitStage.AddFill</c>)에는 <b>없었다</b> — 초상화의 채움은 전부 z=0 ·
        /// 아이템 안에서 <c>sortingOrder</c>도 동률이라, 그리기 순서를 Unity가 렌더러 생성/파괴
        /// 순서로 임의 해소했다. 그것이 사용자 신고 «정보창 미리보기에서 망토를 착탈하면 모자 무늬가
        /// 나타났다 사라졌다»의 정체다(재구성이 순서를 뒤집는다).</para>
        ///
        /// <para>부르는 쪽이 자기 손으로 곱하지 않게 <b>Vector3</b>로 돌려준다 — 그래야 "z에만 준다"는
        /// 사실까지 한 곳에 남는다(x·y에 새면 그림이 밀린다).</para>
        /// </summary>
        /// <param name="orderWithinItem">이 아이템 안에서 몇 번째로 넣은 채움인가(0부터).</param>
        internal static Vector3 FillDepthOffset(int orderWithinItem)
            => new Vector3(0f, 0f, orderWithinItem * FillDepthStep);

        /// <summary>
        /// 채움 색을 <b>그 위에 얹는 윤곽선 색</b>으로 낮추는 계수 — 스펙 14-2(리더 판정 2026-09-03).
        ///
        /// <para>★ <b>0.62 → 0.28.</b> 이 하나의 계수가 서로 다른 세 축에 동시에 걸리고, 천장을
        /// 정하는 것은 <b>축 B</b>다(보조색 조각의 윤곽선 vs 그 밑에 깔린 <b>주색 채움</b>).
        /// 각 축에서 대비 3.0을 지키는 <b>최대</b> 계수는
        /// A(자기 채움) 0.3585 / <b>B(부모 채움) 0.3063</b> / S(그늘 낱선) 0.3678이라 B가 구속한다.</para>
        ///
        /// <para><c>0.30</c>은 그 천장까지 여유가 <b>+0.0063(2.1%)</b>뿐이라 기각했다 — 색이든
        /// 모집단이든 조금만 움직이면 아무 소리 없이 다시 꺼진다. <c>0.28</c>은 여유 <b>+0.0263</b>이고
        /// 세 축의 최저 대비가 A 3.349 / B 3.137 / S 3.392다(<c>0.62</c>에서는 1.934 / 1.611 / 1.955라
        /// 망토 접힘선과 털모자 단이 화면에서 보이지 않았다).</para>
        ///
        /// <para><c>tone == Shade</c> 낱선용 상수를 따로 두지 않는 이유: 축 S의 식은 축 A와 <b>같은 식</b>이고
        /// 천장도 더 느슨하다(0.3678 &gt; 0.3063). 나누면 그늘 쪽이 오히려 나빠진다.</para>
        ///
        /// <para>★ <b>이 값을 찾아 바꾸려고 파일 안의 <c>0.62f</c>를 일괄 치환하지 마라.</b> 계수를
        /// 상수로 뺀 이유가 그것이다 — 예전에는 이 파일에 <c>0.62f</c>가 23곳 있었고 그늘 계수는
        /// 그중 <b>3개뿐</b>이었다. 나머지는 <see cref="DrawnEyeOffsetRatio"/>·
        /// <see cref="CapeCollarBackRatio"/>·<see cref="RoundLensOffsetRatio"/>와 도형 좌표 17곳이라,
        /// 일괄 치환하면 눈 위치·망토 옷깃·렌즈 간격이 조용히 함께 움직인다.</para>
        ///
        /// <para>★ 2026-09-05 — 값의 정의처가 <see cref="AccessoryTone.ShadeFactor"/>(Core)로 내려갔다.
        /// 폴백 아이콘(<c>ItemIconPart.WithPalette</c>)이 같은 그늘을 칠해야 하는데 Core는 이 파일을
        /// 볼 수 없다. 위 문단의 실측과 근거는 그대로 여기 남긴다.</para>
        /// </summary>
        internal const float FillOutlineShadeFactor = AccessoryTone.ShadeFactor;

        /// <summary>채움 위에 얹는 윤곽선 색 — 같은 색을 그대로 쓰면 면과 선이 붙어 <b>실루엣만 남은
        /// 덩어리</b>가 된다(털모자의 띠와 관이 한 덩어리로 뭉치는 자리). 같은 색을 어둡게 한 값이라
        /// 팔레트를 늘리지 않으면서 경계를 만든다. 계수는 <see cref="FillOutlineShadeFactor"/>.</summary>
        internal static Color FillOutlineColor(Color fill) => AccessoryTone.Shaded(fill);

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
