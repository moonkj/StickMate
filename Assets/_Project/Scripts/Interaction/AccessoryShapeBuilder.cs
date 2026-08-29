using UnityEngine;

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

        // 나비넥타이 — 머리 반경 R 배수(목은 머리 바로 아래라 R 기준이 자연스럽다).
        internal const float BowTieDropRatio = 1.15f;
        internal const float BowTieHalfWidthRatio = 0.68f;
        internal const float BowTieHalfHeightRatio = 0.30f;
        internal const float BowTieKnotRatio = 0.13f;

        // 망토 — 어깨~고관절 거리(몸통 길이) 배수 + 머리 반경 배수 혼합.
        internal const float CapeCollarRiseRatio = 0.10f;
        internal const float CapeCollarFrontRatio = 0.40f;
        internal const float CapeCollarBackRatio = 0.62f;
        internal const float CapeLengthRatio = 1.35f;
        internal const float CapeSpreadRatio = 1.35f;
        internal const float CapeHemWaveRatio = 0.18f;

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
        internal static float BowTieLocalY(in Rig rig) => rig.HeadCenterY - rig.HeadRadius * BowTieDropRatio;
        internal static float CapeCollarLocalY(in Rig rig) => rig.ShoulderY + rig.HeadRadius * CapeCollarRiseRatio;
        internal static float CapeHemLocalY(in Rig rig) => CapeCollarLocalY(rig) - rig.TorsoLength * CapeLengthRatio;

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

        /// <summary>망토 외곽선 — 어깨에서 진행 <b>반대쪽</b>으로 흘러내리는 가장 비대칭인 아이템.</summary>
        internal static Vector3[] CapeOutline(in Rig rig)
        {
            float r = rig.HeadRadius;
            float collarY = CapeCollarLocalY(rig);
            float hemY = CapeHemLocalY(rig);
            float front = r * CapeCollarFrontRatio;
            float back = r * CapeCollarBackRatio;
            float trail = r * CapeSpreadRatio;
            float wave = r * CapeHemWaveRatio;
            return new[]
            {
                rig.F(front, collarY),                              // 앞 옷깃
                rig.F(-back, collarY + r * 0.04f),                  // 뒤 옷깃(살짝 세워진 칼라)
                rig.F(-trail, hemY + (collarY - hemY) * 0.28f),     // 뒤로 벌어지는 자락
                rig.F(-trail * 0.82f, hemY),                        // 밑단 뒤 끝
                rig.F(-trail * 0.34f, hemY + wave),                 // 물결 1
                rig.F(front * 0.35f, hemY - wave * 0.35f),          // 물결 2(앞쪽 밑단)
            };
        }

        /// <summary>접힌 주름 한 줄 — 평면 도형이 아니라 천이라는 것을 읽히게 하는 최소한의 표현.</summary>
        internal static Vector3[] CapeFold(in Rig rig)
        {
            float r = rig.HeadRadius;
            float collarY = CapeCollarLocalY(rig);
            float hemY = CapeHemLocalY(rig);
            float back = r * CapeCollarBackRatio;
            float trail = r * CapeSpreadRatio;
            return new[]
            {
                rig.F(-back * 0.35f, collarY - r * 0.10f),
                rig.F(-trail * 0.52f, hemY + (collarY - hemY) * 0.18f),
            };
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
