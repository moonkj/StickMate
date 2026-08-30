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

        // 선글라스 — 머리 반경 R 배수.
        internal const float GlassesCenterRatio = 0.00f;
        internal const float GlassesLensOffsetRatio = 0.44f;
        internal const float GlassesLensHalfWidthRatio = 0.32f;
        internal const float GlassesLensHalfHeightRatio = 0.19f;
        internal const float GlassesTempleReachRatio = 1.02f;

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

        // ---- 눈 중립 좌표(33-3-2). 프리팹 실측치를 **나누기 전 형태로** 남긴다 —
        //      0.341이라고만 적어두면 그 숫자가 어디서 왔는지 다음 사람이 알 수 없다.
        //      Editor/SceneBootstrapper가 굽는 배율 1.0 기준값과 같은 출처이며,
        //      Interaction/CharacterPortraitStage.cs도 여기서 파생시켜 이중 정의를 만들지 않는다.
        internal const float BaselineHeadVisualRadius = 0.22f;
        internal const float BaselineEyeOffsetX = 0.075f;
        internal const float BaselineEyeOffsetY = 0.02f;
        internal const float EyeOffsetXInHeadRadii = BaselineEyeOffsetX / BaselineHeadVisualRadius; // 0.3409
        internal const float EyeOffsetYInHeadRadii = BaselineEyeOffsetY / BaselineHeadVisualRadius; // 0.0909

        // ---- 털모자(33-2-1 #2)
        internal const float BeanieBandBottomRatio = 0.42f;   // = 이 모자의 HatCoverLocalY(가장 깊이 눌러쓴다)
        internal const float BeanieBandTopRatio = 0.62f;
        internal const float BeanieBandHalfWidthRatio = 0.92f;
        internal const float BeanieCrownHeightRatio = 0.78f;
        internal const float BeanieCrownHalfWidthRatio = 0.86f;
        internal const float BeaniePomOffsetRatio = 0.18f;
        internal const float BeaniePomRadiusRatio = 0.22f;
        internal const float BeaniePomBackShiftRatio = 0.10f;

        // ---- 중절모(33-2-1 #3)
        internal const float FedoraBrimLineRatio = 0.58f;     // = 이 모자의 HatCoverLocalY
        internal const float FedoraBrimFrontRatio = 1.75f;
        internal const float FedoraBrimBackRatio = 1.25f;
        internal const float FedoraBrimTipRiseRatio = 0.10f;
        internal const float FedoraCrownHeightRatio = 0.72f;
        internal const float FedoraCrownHalfWidthRatio = 0.72f;
        internal const float FedoraCreaseDropRatio = 0.24f;
        internal const float FedoraBandRiseRatio = 0.14f;

        // ---- 왕관(33-2-1 #4). HatCoverLocalY = +∞ — 씌우는 것이 아니라 얹는 것이라 밑이 뚫려 있다.
        internal const float CrownBaseRatio = 0.55f;
        internal const float CrownHalfWidthRatio = 0.85f;

        // ---- 동그란 안경 / 고글 / 외알 안경(33-2-2)
        internal const float RoundLensOffsetRatio = 0.42f;
        internal const float RoundLensRadiusRatio = 0.30f;
        internal const float RoundBridgeHalfWidthRatio = 0.12f;
        internal const float GoggleHalfWidthRatio = 0.86f;
        internal const float GoggleHalfHeightRatio = 0.30f;
        internal const float GoggleStrapRadiusRatio = 1.02f;
        internal const float MonocleOffsetRatio = 0.40f;
        internal const float MonocleRadiusRatio = 0.34f;

        // ---- 줄무늬 타이 / 목도리 / 방울 목걸이(33-2-3). 부착 기준선은 나비넥타이와 같은 BowTieLocalY.
        internal const float TieKnotHalfWidthRatio = 0.24f;
        internal const float TieKnotHalfHeightRatio = 0.20f;
        internal const float TieBladeLengthInTorso = 0.55f;
        internal const float TieBladeHalfWidthRatio = 0.15f;
        /// <summary>33-2-5 (D) — 줄무늬 타이 "월요일마다 조금 느슨해진다". 매듭을 R·0.12 내리고 blade를 3도 기울인다.</summary>
        internal const float TieMondayLoosenDropRatio = 0.12f;
        internal const float TieMondayLoosenTiltDegrees = 3f;
        internal const float ScarfWrapRiseRatio = 0.08f;
        internal const float ScarfWrapHalfWidthRatio = 0.85f;
        internal const float ScarfWrapHalfHeightRatio = 0.17f;
        internal const float ScarfTailLengthInTorso = 0.62f;
        internal const float ScarfTailWidthRatio = 0.22f;
        internal const float ScarfTailDriftRatio = 0.50f;
        internal const float CollarHalfWidthRatio = 0.75f;
        internal const float BellDropRatio = 0.34f;
        internal const float BellRadiusRatio = 0.17f;

        // ---- 긴 망토 / 날개 / 배낭(33-2-4)
        // 긴 망토도 같은 이유로 넓힌다. 길이는 2.10 -> 1.85로 줄였다 — 배율 0.75에서 옛 값의 밑단은
        // 로컬 y 0.03(발바닥에서 획 두께 남짓)이라 사실상 바닥을 쓸고 있었다.
        internal const float LongCapeLengthRatio = 1.85f;
        internal const float LongCapeSpreadRatio = 3.10f;
        internal const float LongCapeFrontSpreadRatio = 1.05f;
        internal const float LongCapeHemWaveRatio = 0.30f;
        // ★ 2026-08-30 — 망토와 같은 라운드에 키웠다. 옛 값(뻗음 2.05R / 들림 0.55R)은 어깨 옆에 붙은
        //   작은 지느러미로 보여 "날개"로 읽히지 않았다(실측 스크린샷). 위로 더 들어 올리고 뒤로 더
        //   뻗게 해 <b>펼친 날개</b> 실루엣을 만든다. 상한은 정수리다 —
        //   Tests/PlayMode/CharacterAppearanceLayerTests가 "등 아이템은 정수리를 넘지 않는다"를 잠근다
        //   (어깨 + 1.00R = 정수리보다 R·0.2 아래로, 여유를 두고 잡았다).
        internal const float WingSpineDropInTorso = 0.55f;
        internal const float WingOuterReachRatio = 2.55f;
        internal const float WingOuterRiseRatio = 1.00f;
        internal const float WingInnerReachRatio = 1.85f;
        internal const float WingInnerRiseRatio = 0.35f;
        internal const float PackCenterBackRatio = 0.62f;
        internal const float PackDropInTorso = 0.42f;
        internal const float PackHalfWidthRatio = 0.55f;
        internal const float PackHalfHeightInTorso = 0.34f;
        internal const float PackFlapDropInTorso = 0.12f;

        // ---- HAIR(33-4)
        internal const float HairCapRadiusRatio = 1.13f;
        internal const float CurlBaseRadiusRatio = 1.10f;
        internal const float CurlAmplitudeRatio = 0.16f;
        internal const float ShineRadiusRatio = 0.62f;

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
        /// 머리(HAIR) 렌더러는 이 선 위로 올라가는 <b>선을 통째로</b> 생략한다.
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
                rig.F(-halfW * 0.35f, brimY - r * 0.10f),
            };
        }

        internal static Vector3[] GlassesLensFront(in Rig rig)
            => RoundedBox(rig, rig.HeadRadius * GlassesLensOffsetRatio, GlassesLocalY(rig),
                rig.HeadRadius * GlassesLensHalfWidthRatio, rig.HeadRadius * GlassesLensHalfHeightRatio);

        internal static Vector3[] GlassesLensBack(in Rig rig)
            => RoundedBox(rig, -rig.HeadRadius * GlassesLensOffsetRatio, GlassesLocalY(rig),
                rig.HeadRadius * GlassesLensHalfWidthRatio, rig.HeadRadius * GlassesLensHalfHeightRatio);

        internal static Vector3[] GlassesBridge(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            float dx = r * GlassesLensOffsetRatio;
            float hw = r * GlassesLensHalfWidthRatio;
            float hh = r * GlassesLensHalfHeightRatio;
            return new[] { rig.F(-dx + hw, cy + hh * 0.35f), rig.F(dx - hw, cy + hh * 0.35f) };
        }

        /// <summary>안경다리 — 얼굴 <b>뒤쪽</b>(진행 반대 방향)으로 뻗어 귀로 간다(비대칭 요소).</summary>
        internal static Vector3[] GlassesTemple(in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);
            float dx = r * GlassesLensOffsetRatio;
            float hw = r * GlassesLensHalfWidthRatio;
            float hh = r * GlassesLensHalfHeightRatio;
            return new[]
            {
                rig.F(-dx - hw, cy + hh * 0.45f),
                rig.F(-r * GlassesTempleReachRatio, cy + hh * 0.15f),
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
        /// <param name="strokeHalfWidth">HAIR 전용. 커버 판정은 점이 아니라 <b>획 바깥쪽</b>까지 본다
        /// (Interaction/CharacterPortraitStage.TryMeasureRotatedInk가 "보이는 그림"을 재는 방식과 같다).</param>
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

                    sink.Add(new Shape("BeaniePom",
                        Polygon(rig, -r * BeaniePomBackShiftRatio, crownTop + r * BeaniePomOffsetRatio,
                            r * BeaniePomRadiusRatio, 8),
                        true, SortHead, tone: Accent, filled: true));
                    break;
                }

                case HeadFedora:
                {
                    float brimY = hc + r * FedoraBrimLineRatio;
                    float rise = r * FedoraBrimTipRiseRatio;
                    float crownHalf = r * FedoraCrownHalfWidthRatio;
                    // 챙은 **양쪽 다** 뻗지만 앞이 더 길어 방향이 읽힌다(33-2-1 #3).
                    sink.Add(new Shape("FedoraBrim", new[]
                    {
                        rig.F(-r * FedoraBrimBackRatio, brimY + rise),
                        rig.F(-crownHalf, brimY),
                        rig.F(crownHalf, brimY),
                        rig.F(r * FedoraBrimFrontRatio, brimY + rise),
                        rig.F(crownHalf, brimY - r * 0.12f),
                        rig.F(-crownHalf, brimY - r * 0.12f),
                    }, true, SortHead, filled: true));

                    float crownTop = brimY + r * FedoraCrownHeightRatio;
                    sink.Add(new Shape("FedoraCrown", new[]
                    {
                        rig.F(-crownHalf, brimY),
                        rig.F(-crownHalf * 0.92f, crownTop),
                        rig.F(0f, crownTop + r * 0.02f),
                        rig.F(crownHalf * 0.92f, crownTop),
                        rig.F(crownHalf, brimY),
                    }, true, SortHead, filled: true));

                    sink.Add(new Shape("FedoraCrease", new[]
                    {
                        rig.F(-crownHalf * 0.30f, crownTop),
                        rig.F(0f, crownTop - r * FedoraCreaseDropRatio),
                        rig.F(crownHalf * 0.30f, crownTop),
                    }, false, SortHead, tone: Shade));

                    sink.Add(new Shape("FedoraBand", new[]
                    {
                        rig.F(-crownHalf, brimY + r * FedoraBandRiseRatio),
                        rig.F(crownHalf, brimY + r * FedoraBandRiseRatio),
                    }, false, SortHead, tone: Accent));
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
            }
        }

        // ==================== EYES (안경) ====================

        private static void AppendEyes(List<Shape> sink, int item, in Rig rig)
        {
            float r = rig.HeadRadius;
            float cy = GlassesLocalY(rig);

            switch (item)
            {
                case EyesSunglasses:
                    sink.Add(new Shape("GlassesLensFront", GlassesLensFront(rig), true, SortEyes));
                    sink.Add(new Shape("GlassesLensBack", GlassesLensBack(rig), true, SortEyes));
                    sink.Add(new Shape("GlassesBridge", GlassesBridge(rig), false, SortEyes, tone: Accent));
                    sink.Add(new Shape("GlassesTemple", GlassesTemple(rig), false, SortEyes, tone: Accent));
                    break;

                case EyesRound:
                {
                    float dx = r * RoundLensOffsetRatio;
                    float rad = r * RoundLensRadiusRatio;
                    sink.Add(new Shape("RoundLensFront", Polygon(rig, dx, cy, rad, 12), true, SortEyes));
                    sink.Add(new Shape("RoundLensBack", Polygon(rig, -dx, cy, rad, 12), true, SortEyes));
                    sink.Add(new Shape("RoundBridge", new[]
                    {
                        rig.F(-r * RoundBridgeHalfWidthRatio, cy + r * 0.02f),
                        rig.F(r * RoundBridgeHalfWidthRatio, cy + r * 0.02f),
                    }, false, SortEyes, tone: Accent));
                    sink.Add(new Shape("RoundTemple", new[]
                    {
                        rig.F(-dx - rad, cy + r * 0.02f),
                        rig.F(-r * GlassesTempleReachRatio, cy + r * 0.08f),
                    }, false, SortEyes, tone: Accent));
                    break;
                }

                case EyesGoggles:
                {
                    float hw = r * GoggleHalfWidthRatio;
                    float hh = r * GoggleHalfHeightRatio;
                    // 모서리가 아주 둥근 8각 — 통짜 렌즈 1개(좌우로 이어진 고글).
                    sink.Add(new Shape("GoggleLens", new[]
                    {
                        rig.F(-hw, cy - hh * 0.40f),
                        rig.F(-hw, cy + hh * 0.40f),
                        rig.F(-hw * 0.64f, cy + hh),
                        rig.F(hw * 0.64f, cy + hh),
                        rig.F(hw, cy + hh * 0.40f),
                        rig.F(hw, cy - hh * 0.40f),
                        rig.F(hw * 0.64f, cy - hh),
                        rig.F(-hw * 0.64f, cy - hh),
                    }, true, SortEyes));

                    // 하이라이트 — 렌즈 안 윗변 아래 R·0.10에 걸친 얕은 아래볼록 호.
                    float hlY = cy + hh - r * 0.10f;
                    float hlHalf = r * 0.50f;
                    var highlight = new Vector3[5];
                    for (int i = 0; i < 5; i++)
                    {
                        float t = i / 4f * 2f - 1f;               // -1 ~ +1
                        float x = hlHalf * t;
                        float sag = (1f - t * t) * r * 0.06f;     // 가운데가 아래로 처진다
                        highlight[i] = rig.F(x, hlY - sag);
                    }
                    sink.Add(new Shape("GoggleHighlight", highlight, false, SortEyes));

                    // 스트랩 — 머리 링을 따라 뒤쪽 반원. **가장 비대칭인 요소**라 좌우 반전 회귀 대상.
                    sink.Add(new Shape("GoggleStrap",
                        Arc(rig, 0f, rig.HeadCenterY, r * GoggleStrapRadiusRatio, 100f, 260f, 7),
                        false, SortEyes, tone: Accent));
                    break;
                }

                case EyesMonocle:
                {
                    float cxm = r * MonocleOffsetRatio;
                    float cym = cy + r * 0.02f;
                    // 앞쪽 눈에만 있다(33-2-2 #4).
                    sink.Add(new Shape("MonocleRing",
                        Polygon(rig, cxm, cym, r * MonocleRadiusRatio, 12), true, SortEyes));
                    sink.Add(new Shape("MonocleChain", new[]
                    {
                        rig.F(r * 0.30f, cy - r * 0.30f),
                        rig.F(r * 0.12f, cy - r * 0.72f),
                        rig.F(r * 0.34f, cy - r * 1.05f),
                    }, false, SortEyes, tone: Accent));
                    break;
                }
            }
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
                    float wrapY = ty + r * ScarfWrapRiseRatio;
                    float wh = r * ScarfWrapHalfWidthRatio;
                    float wv = r * ScarfWrapHalfHeightRatio;
                    sink.Add(new Shape("ScarfWrap", new[]
                    {
                        rig.F(-wh, wrapY + wv),
                        rig.F(0f, wrapY + wv * 0.76f),
                        rig.F(wh, wrapY + wv),
                        rig.F(wh, wrapY - wv),
                        rig.F(0f, wrapY - wv * 1.65f),   // 아래로 볼록한 띠
                        rig.F(-wh, wrapY - wv),
                    }, true, SortNeck));

                    float tailLen = rig.TorsoLength * ScarfTailLengthInTorso;
                    float tailW = r * ScarfTailWidthRatio;
                    float drift = r * ScarfTailDriftRatio;
                    sink.Add(ScarfTail(rig, "ScarfTailA", -r * 0.30f, wrapY - wv, tailLen, tailW, drift));
                    sink.Add(ScarfTail(rig, "ScarfTailB", -r * 0.62f, wrapY - wv, tailLen, tailW, drift));
                    break;
                }

                case NeckBell:
                {
                    float ch = r * CollarHalfWidthRatio;
                    var collar = new Vector3[5];
                    for (int i = 0; i < 5; i++)
                    {
                        float t = i / 4f * 2f - 1f;
                        float x = ch * t;
                        // 양 끝 +R·0.16, 한가운데 −R·0.14 (아래로 볼록한 목줄)
                        collar[i] = rig.F(x, ty + r * 0.16f - (1f - t * t) * r * 0.30f);
                    }
                    sink.Add(new Shape("Collar", collar, false, SortNeck));

                    float bellY = ty - r * BellDropRatio;
                    float bellR = r * BellRadiusRatio;
                    // 33-2-5 (C) — 오디오 시스템이 없으므로 "소리"는 만들 수 없다. 대신 방울이
                    // 걸을 때 실제로 흔들리게 해 설명문(리더 승인으로 '흔들린다'로 교체 예정)과 맞춘다.
                    sink.Add(new Shape("Bell", Polygon(rig, 0f, bellY, bellR, 10), true, SortNeck,
                        swayStart: 0, swayCount: 10, tone: Accent));
                    sink.Add(new Shape("BellClapper", new[]
                    {
                        rig.F(0f, bellY - bellR),
                        rig.F(0f, bellY - bellR - r * 0.10f),
                    }, false, SortNeck, swayStart: 0, swayCount: 2, tone: Accent));
                    break;
                }
            }
        }

        private static Shape ScarfTail(in Rig rig, string name, float startForwardX, float startY,
            float length, float width, float drift)
        {
            float endY = startY - length;
            float endX = startForwardX - drift;
            // 끝 2점(인덱스 2,3)이 흔들린다 — 33-2-5 (A)가 지정한 그대로.
            return new Shape(name, new[]
            {
                rig.F(startForwardX, startY),
                rig.F(startForwardX - width, startY),
                rig.F(endX - width, endY),
                rig.F(endX, endY),
            }, true, SortNeck, swayStart: 2, swayCount: 2, tone: Accent);
        }

        // ==================== BACK (망토/날개/배낭) ====================

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
                    sink.Add(new Shape("CapeFold", CapeFold(rig), false, SortBack, tone: Shade));
                    sink.Add(new Shape("CapeFold2",
                        CapeFold(rig, CapeLengthRatio, CapeSpreadRatio, 0.72f), false, SortBack, tone: Shade));
                    break;

                case BackLongCape:
                    sink.Add(new Shape("CapeOutline",
                        CapeOutline(rig, LongCapeLengthRatio, LongCapeSpreadRatio, LongCapeHemWaveRatio,
                            LongCapeFrontSpreadRatio),
                        true, SortBack, swayStart: 2, swayCount: 5, filled: true));
                    sink.Add(new Shape("CapeFold",
                        CapeFold(rig, LongCapeLengthRatio, LongCapeSpreadRatio, 0.35f), false, SortBack, tone: Shade));
                    sink.Add(new Shape("CapeFold2",
                        CapeFold(rig, LongCapeLengthRatio, LongCapeSpreadRatio, 0.72f), false, SortBack, tone: Shade));
                    break;

                case BackWings:
                {
                    float sy = rig.ShoulderY;
                    // ★ "뜨지는 않지만 폼은 난다" — 어떤 상태에서도 y 오프셋을 주지 않는다(33-2-4 #3).
                    sink.Add(new Shape("WingSpine", new[]
                    {
                        rig.F(-r * 0.20f, sy),
                        rig.F(-r * 0.35f, sy - rig.TorsoLength * WingSpineDropInTorso),
                    }, false, SortBack, tone: Accent));

                    sink.Add(new Shape("WingFeatherA", new[]
                    {
                        rig.F(-r * 0.20f, sy),
                        rig.F(-r * 0.95f, sy + r * 0.62f),
                        rig.F(-r * WingOuterReachRatio, sy + r * WingOuterRiseRatio),
                        rig.F(-r * 1.20f, sy + r * 0.02f),
                        rig.F(-r * 0.45f, sy - r * 0.18f),
                    }, true, SortBack, filled: true));

                    sink.Add(new Shape("WingFeatherB", new[]
                    {
                        rig.F(-r * 0.25f, sy - r * 0.05f),
                        rig.F(-r * 0.80f, sy + r * 0.22f),
                        rig.F(-r * WingInnerReachRatio, sy + r * WingInnerRiseRatio),
                        rig.F(-r * 0.85f, sy - r * 0.30f),
                        rig.F(-r * 0.30f, sy - r * 0.34f),
                    }, true, SortBack, filled: true));
                    break;
                }

                case BackBackpack:
                {
                    float sy = rig.ShoulderY;
                    float cx = -r * PackCenterBackRatio;
                    float cyp = sy - rig.TorsoLength * PackDropInTorso;
                    float hw = r * PackHalfWidthRatio;
                    float hh = rig.TorsoLength * PackHalfHeightInTorso;
                    sink.Add(new Shape("PackBody", new[]
                    {
                        rig.F(cx - hw, cyp - hh * 0.62f),
                        rig.F(cx - hw, cyp + hh * 0.62f),
                        rig.F(cx - hw * 0.55f, cyp + hh),
                        rig.F(cx + hw * 0.55f, cyp + hh),
                        rig.F(cx + hw, cyp + hh * 0.62f),
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
                        rig.F(cx + hw, cyp + hh),
                    }, false, SortBack, tone: Accent));
                    break;
                }
            }
        }

        // ★ 2026-08-30 FACE(표정) 카테고리 삭제 — 사용자 결정("표정관련은 전부삭제 어차피 구별이
        //   안됨"). 눈/입 도형(Smile/Lid)과 AppendFace가 함께 사라졌다. 눈동자 점 2개는 원래부터
        //   States/EyeController.cs의 단독 소유라 이 삭제와 무관하게 그대로 커서를 따라간다.

        // ==================== HAIR (머리) ====================

        /// <summary>
        /// ★ 33-4-1 — 모자가 선언한 커버선 위로 올라가는 선을 <b>선 단위로 통째로</b> 생략한다.
        /// 점 단위로 자르지 않는 이유: 자른 자리에 둥근 캡이 생겨 "모자 속으로 들어간 머리카락"이 아니라
        /// <b>뭉툭하게 잘린 선</b>으로 보인다.
        /// </summary>
        private static void AppendHair(List<Shape> sink, int item, in Rig rig, float hatCoverLocalY, float strokeHalfWidth)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;

            _hairScratch.Clear();
            switch (item)
            {
                case HairCowlick:
                    _hairScratch.Add(new Shape("Cowlick", new[]
                    {
                        rig.F(-r * 0.28f, hc + r * 0.96f),
                        rig.F(-r * 0.48f, hc + r * 1.34f),
                        rig.F(-r * 0.30f, hc + r * 1.52f),
                        rig.F(-r * 0.62f, hc + r * 1.70f),
                    }, false, SortHair));
                    break;

                case HairNeat:
                    _hairScratch.Add(new Shape("HairCap",
                        Arc(rig, 0f, hc, r * HairCapRadiusRatio, 20f, 160f, 9), false, SortHair));
                    _hairScratch.Add(new Shape("Part", new[]
                    {
                        rig.F(r * 0.10f, hc + r * 1.10f),
                        rig.F(r * 0.62f, hc + r * 0.92f),
                        rig.F(r * 0.90f, hc + r * 0.72f),
                    }, false, SortHair, tone: Accent));
                    break;

                case HairCurly:
                {
                    // 반경 1.10R 호(10°~170°) 위에 사인 4주기·진폭 0.16R을 반경 방향으로 더한다.
                    const int Segments = 16;
                    var pts = new Vector3[Segments + 1];
                    for (int i = 0; i <= Segments; i++)
                    {
                        float deg = Mathf.Lerp(10f, 170f, i / (float)Segments);
                        float rad = deg * Mathf.Deg2Rad;
                        float radius = r * CurlBaseRadiusRatio
                            + r * CurlAmplitudeRatio * Mathf.Sin(i * Mathf.PI * 4f / Segments);
                        pts[i] = rig.F(Mathf.Cos(rad) * radius, hc + Mathf.Sin(rad) * radius);
                    }
                    _hairScratch.Add(new Shape("Curls", pts, false, SortHair));
                    break;
                }

                case HairBald:
                    // 아무것도 안 그리지 않는다 — 착용했는데 화면이 그대로면 그건 착용이 아니다(33-4 #4).
                    _hairScratch.Add(new Shape("Shine",
                        Arc(rig, 0f, hc, r * ShineRadiusRatio, 100f, 150f, 5), false, SortHair, tone: Accent));
                    break;
            }

            for (int i = 0; i < _hairScratch.Count; i++)
            {
                if (!IsCoveredByHat(_hairScratch[i], hatCoverLocalY, strokeHalfWidth)) sink.Add(_hairScratch[i]);
            }
        }

        /// <summary>도형을 굽는 동안에만 쓰는 임시 목록. 재구성은 <b>착용/방향/색이 바뀐 프레임에만</b>
        /// 도는 경로라 정적 재사용으로 충분하다(매 프레임 할당 금지 규약).</summary>
        private static readonly List<Shape> _hairScratch = new List<Shape>(4);

        /// <summary>커버 판정은 점이 아니라 <b>획 바깥쪽</b>까지 본다 — 민머리 하이라이트(최고점 0.611R)가
        /// 천 모자 커버선(0.62R)을 0.009R 차이로 통과해 챙 밑에 한 줄만 남던 경계 사례를 없앤다.</summary>
        private static bool IsCoveredByHat(in Shape shape, float hatCoverLocalY, float strokeHalfWidth)
        {
            if (float.IsPositiveInfinity(hatCoverLocalY) || shape.Points == null) return false;
            for (int i = 0; i < shape.Points.Length; i++)
            {
                if (shape.Points[i].y + strokeHalfWidth > hatCoverLocalY) return true;
            }
            return false;
        }

        // ==================== 망토 매개변수화(짧은/긴 망토가 같은 코드를 쓴다) ====================

        internal static float CapeHemLocalY(in Rig rig, float lengthRatio)
            => CapeCollarLocalY(rig) - rig.TorsoLength * lengthRatio;

        /// <summary>
        /// ★ 2026-08-30 재설계 — <b>옷깃에서 밑단으로 벌어지는 사다리꼴</b>(위 CapeSpreadRatio 문서).
        /// 점 순서는 옷깃 앞 -> 옷깃 뒤 -> 밑단 뒤끝 -> 밑단(물결 3점) -> 밑단 앞끝이고, 밑단 5점이
        /// 통째로 흔들린다(HemSway). 옛 도형은 밑단 뒤쪽만 벌어져 <b>깃발</b>이었다.
        /// </summary>
        internal static Vector3[] CapeOutline(in Rig rig, float lengthRatio, float spreadRatio,
            float hemWaveRatio, float frontSpreadRatio)
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
        internal static Vector3[] CapeFold(in Rig rig, float lengthRatio, float spreadRatio, float startBackRatio)
        {
            float r = rig.HeadRadius;
            float collarY = CapeCollarLocalY(rig);
            float hemY = CapeHemLocalY(rig, lengthRatio);
            float back = r * CapeCollarBackRatio;
            float trail = r * spreadRatio;
            // 주름은 <b>천이 벌어지는 방향</b>을 따라간다 — 옷깃의 좁은 자리에서 밑단의 넓은 자리로.
            // 끝점 비율을 시작 비율에 비례시키면 안 되는 이유는 아래 문단 참고(외곽선 밖으로 나간다).
            float endRatio = Mathf.Min(0.92f, 0.42f + (startBackRatio - 0.35f) * 0.60f);
            return new[]
            {
                rig.F(-back * startBackRatio, collarY - r * 0.10f),
                rig.F(-trail * endRatio, hemY + (collarY - hemY) * 0.20f),
            };
        }

        /// <summary>짧은 망토 주름 기본값(옛 호출부 호환).</summary>
        internal static Vector3[] CapeFold(in Rig rig)
            => CapeFold(rig, CapeLengthRatio, CapeSpreadRatio, 0.35f);

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
        private static Vector3[] Polygon(in Rig rig, float centerForwardX, float centerY, float radius, int segments)
        {
            var pts = new Vector3[segments];
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                pts[i] = rig.F(centerForwardX + Mathf.Cos(step * i) * radius, centerY + Mathf.Sin(step * i) * radius);
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
