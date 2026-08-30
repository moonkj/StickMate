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
    /// </summary>
    internal static class AppearanceShapeBuilder
    {
        // ---- 아이템 자리(Core/ItemCatalog.cs FX/PET 표의 순서). 실시간 렌더러와 초상화 미리보기가
        //      같은 상수를 봐야 "카드에서 고른 것"과 "그려지는 것"이 어긋나지 않는다.
        internal const int FxNone = 0, FxFootprint = 1, FxSparkle = 2, FxDust = 3;
        internal const int PetBall = 0, PetPlane = 1, PetMini = 2, PetCursor = 3;

        // ---- 실시간 렌더러가 쓰는 치수 중 초상화도 알아야 하는 것(같은 크기로 보여야 미리보기다).
        /// <summary>반짝임 한 갈래의 길이(머리 반경 배수).</summary>
        internal const float SparkleArmInR = 0.34f;

        /// <summary>공의 반지름(신장 배수).</summary>
        internal const float BallRadiusInHeight = 0.055f;

        /// <summary>종이비행기 반폭(머리 반경 배수).</summary>
        internal const float PlaneWingSpanInR = 0.75f;

        /// <summary>작은 졸라맨의 키(주인 신장 배수).</summary>
        internal const float MiniScale = 0.45f;

        /// <summary>커서 친구의 크기(머리 반경 배수).</summary>
        internal const float CursorSizeInR = 0.90f;

        // ==================== FX ====================

        /// <summary>
        /// 채운 점 하나를 만드는 2점 선. <b>부르는 쪽이 선 두께를 <c>radius * 2</c>로 잡아야</b>
        /// 둥근 캡이 원이 된다(이 프로젝트에는 채움 도형 경로가 없다 — 굵은 캡이 곧 점이다).
        /// </summary>
        internal static Vector3[] DotSegment(float radius)
            => new[] { new Vector3(-radius * 0.05f, 0f, 0f), new Vector3(radius * 0.05f, 0f, 0f) };

        /// <summary>4갈래 반짝의 획 하나(<paramref name="index"/> 0 = 세로, 1 = 가로).</summary>
        internal static Vector3[] SparkleStroke(float arm, int index)
            => index == 0
                ? new[] { new Vector3(0f, -arm, 0f), new Vector3(0f, arm, 0f) }
                : new[] { new Vector3(-arm, 0f, 0f), new Vector3(arm, 0f, 0f) };

        /// <summary>먼지 초승달 하나(<paramref name="index"/> 0 = 큰 것, 1 = 위에 얹히는 작은 것).
        /// 착지 먼지(LandingDustRenderer)와 같은 어휘라 "먼지"로 바로 읽힌다.</summary>
        internal static Vector3[] DustCrescent(float radius, int index)
        {
            const int Segments = 5;
            var pts = new Vector3[Segments + 1];
            float rr = radius * (index == 0 ? 1f : 0.65f);
            float offsetY = index == 0 ? 0f : radius * 0.55f;
            for (int k = 0; k <= Segments; k++)
            {
                float a = Mathf.Lerp(-10f, 190f, k / (float)Segments) * Mathf.Deg2Rad;
                pts[k] = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.7f + offsetY, 0f);
            }
            return pts;
        }

        // ==================== PET ====================

        /// <summary>공의 테두리(닫힌 고리).</summary>
        internal static Vector3[] BallRing(float radius, int segments)
        {
            var ring = new Vector3[segments];
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                ring[i] = new Vector3(Mathf.Cos(step * i) * radius, Mathf.Sin(step * i) * radius, 0f);
            }
            return ring;
        }

        /// <summary>반지름 선. 이게 없으면 원이 아무리 굴러도 정지해 보인다 — 회전을 읽히게 하는 유일한 요소.</summary>
        internal static Vector3[] BallSpoke(float radius)
            => new[] { Vector3.zero, new Vector3(radius, 0f, 0f) };

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

        /// <summary>종이비행기 접힘선(열린 3점).</summary>
        internal static Vector3[] PlaneFold(float halfSpan)
        {
            float w = halfSpan;
            return new[]
            {
                new Vector3(w, 0f, 0f),
                new Vector3(-w * 0.42f, 0f, 0f),
                new Vector3(-w * 0.75f, -w * 0.62f, 0f),
            };
        }

        /// <summary>작은 졸라맨의 선 6개(머리 원 / 몸통 / 팔 2 / 다리 2). 원점은 <b>발바닥</b>.
        /// 다리 2개가 마지막 두 줄인 것은 계약이다 — 실시간 렌더러가 그 둘만 뿌리 기준으로 흔든다.</summary>
        internal static Vector3[][] MiniFigure(float height, float facing)
        {
            float h = height;
            float r = h * 0.14f;
            float headY = h - r;
            float shoulderY = h * 0.72f;
            float hipY = h * 0.40f;
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
                Limb(shoulderY, -h * 0.10f * f, h * 0.30f),
                Limb(shoulderY, h * 0.14f * f, h * 0.30f),
                Limb(hipY, -h * 0.10f, h * 0.40f),
                Limb(hipY, h * 0.10f, h * 0.40f),
            };
        }

        private static Vector3[] Limb(float rootY, float tipX, float length)
            => new[] { new Vector3(0f, rootY, 0f), new Vector3(tipX, rootY - length, 0f) };

        /// <summary>커서 친구 — 화살표 실루엣(원점이 <b>화살표 끝점</b>, 아래로 뻗는다).</summary>
        internal static Vector3[] CursorArrow(float size)
        {
            float s = size;
            return new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, -s, 0f),
                new Vector3(s * 0.24f, -s * 0.72f, 0f),
                new Vector3(s * 0.40f, -s * 1.02f, 0f),
                new Vector3(s * 0.56f, -s * 0.94f, 0f),
                new Vector3(s * 0.40f, -s * 0.64f, 0f),
                new Vector3(s * 0.66f, -s * 0.62f, 0f),
                new Vector3(0f, 0f, 0f),
            };
        }
    }
}
