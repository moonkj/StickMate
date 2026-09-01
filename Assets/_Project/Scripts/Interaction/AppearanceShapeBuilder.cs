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
        /// </summary>
        internal const float SparkleArmInR = 0.85f;

        /// <summary>공의 반지름(신장 배수).</summary>
        internal const float BallRadiusInHeight = 0.055f;

        /// <summary>종이비행기 반폭(머리 반경 배수).</summary>
        internal const float PlaneWingSpanInR = 0.75f;

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

        /// <summary>커서 친구의 크기(머리 반경 배수).</summary>
        internal const float CursorSizeInR = 0.90f;

        // ============================================================================
        // ★ 신규 4종의 공용 치수 (2026-09-01) — 전부 37-6 규칙 1(획 예산)을 검산해 잡았다
        // ============================================================================
        // 출하 배율 0.75에서 획 W ≈ 0.344R이다(AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii).
        // 아래 값은 "가장 짧은 선분 ≥ 1.0 W", "내부를 보여야 하는 윤곽 도형의 지름 ≥ 3.0 W",
        // "구분돼야 하는 두 선의 간격 ≥ 1.5 W"를 손으로 검산한 결과이며, 그 검산을
        // Tests/EditMode/AccessoryStrokeBudgetTests가 도형 좌표에서 다시 잰다.

        /// <summary>물방울 하나의 기본 반지름 하한(머리 반경 배수). 지름 1.16R ≥ 3.0 W(1.03R)라
        /// 링 안쪽이 살아 있다 — 더 작으면 방울이 아니라 <b>까만 점</b>이 된다.</summary>
        internal const float BubbleMinRadiusInR = 0.58f;

        /// <summary>물방울 하나의 기본 반지름 상한. 머리(1.0R)보다 확실히 작아야 "방울"로 읽힌다.</summary>
        internal const float BubbleMaxRadiusInR = 0.80f;

        /// <summary>나뭇잎 잎몸의 길이(머리 반경 배수). 가장 짧은 잎몸 선분이 0.342×길이이므로
        /// 1.15R에서 0.393R ≥ 1.0 W다(1.006R 미만이면 잎맥 없는 검은 덩어리가 된다).</summary>
        internal const float LeafLengthInR = 1.15f;

        /// <summary>풍선 주머니의 반지름(머리 반경 배수).</summary>
        internal const float BalloonRadiusInR = 0.80f;

        /// <summary>풍선 끈의 길이(머리 반경 배수). 원점(= 묶인 자리)에서 위로 이만큼 올라간 곳이 매듭이다.</summary>
        internal const float BalloonStringInR = 1.70f;

        /// <summary>달팽이의 기준 치수(머리 반경 배수 = 1R). 아래 세 도형이 전부 이 값의 배수다.</summary>
        internal const float SnailSizeInR = 1.0f;

        /// <summary>달팽이 껍데기 바깥 링의 반지름(<see cref="SnailSizeInR"/> 배수).</summary>
        internal const float SnailShellRadiusRatio = 0.68f;

        /// <summary>껍데기 속 점의 반지름. 바깥 링과의 간격이 0.53R ≥ 1.5 W라 두 선이 붙어 보이지 않는다.</summary>
        internal const float SnailShellCoreRatio = 0.15f;

        /// <summary>
        /// 껍데기 중심(발 접지선 기준). 링 아랫변이 발 선과 <b>거의 정확히 만난다</b>(0.02R 아래).
        ///
        /// <para>이 값이 이 도형에서 가장 빠듯한 자리다. 위로 띄우면 껍데기가 <b>공중에 뜬 원</b>이 되고
        /// (37-6 규칙 4가 금지한 그림), 아래로 내리면 껍데기가 <b>땅 밑으로 잠긴다</b>. 두 획은 각각
        /// 반폭 0.5 W를 가지므로 중심선 거리가 그 안이면 잉크가 실제로 겹친다 — 그래서 "닿는다"의
        /// 판정 기준은 좌표가 아니라 <b>획 반폭</b>이다(Tests/EditMode/AppearanceShapeBudgetTests가 잠근다).</para>
        /// </summary>
        internal const float SnailShellCenterXRatio = -0.15f, SnailShellCenterYRatio = 0.66f;

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

        /// <summary>물방울 한 알의 테두리(닫힌 고리, 원점 중심). 방울은 <b>속이 보여야</b> 방울이므로
        /// 반지름 하한(<see cref="BubbleMinRadiusInR"/>)을 부르는 쪽이 지켜야 한다.</summary>
        internal static Vector3[] BubbleRing(float radius, int segments) => Circle(0f, 0f, radius, segments);

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
        /// (37-6 규칙 4 — 떠 있는 조각을 만들지 않는다).</summary>
        internal static Vector3[] LeafStem(float length)
            => new[] { new Vector3(-0.50f * length, 0f, 0f), new Vector3(-0.86f * length, -0.16f * length, 0f) };

        // ==================== PET ====================

        /// <summary>공의 테두리(닫힌 고리).</summary>
        internal static Vector3[] BallRing(float radius, int segments) => Circle(0f, 0f, radius, segments);

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
                Limb(shoulderY, -h * 0.10f * f, h * 0.30f),
                Limb(shoulderY, h * 0.14f * f, h * 0.30f),
                Limb(hipY, -h * MiniLegTipXRatio, h * MiniHipRatio),
                Limb(hipY, h * MiniLegTipXRatio, h * MiniHipRatio),
            };
        }

        private static Vector3[] Limb(float rootY, float tipX, float length)
            => new[] { new Vector3(0f, rootY, 0f), new Vector3(tipX, rootY - length, 0f) };

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
        internal static Vector3[] SnailShell(float size, float facing, int segments)
            => Circle((facing >= 0f ? 1f : -1f) * SnailShellCenterXRatio * size,
                SnailShellCenterYRatio * size, SnailShellRadiusRatio * size, segments);

        /// <summary>껍데기 속의 점 — 이 아이템을 형제들과 가르는 <b>단 한 부분</b>이라 보조색은 여기에만 쓴다
        /// (37-6 규칙 3-2). 카드 아이콘의 작은 원과 같은 자리다.</summary>
        internal static Vector3[] SnailShellCore(float size, float facing, int segments)
            => Circle((facing >= 0f ? 1f : -1f) * SnailShellCenterXRatio * size,
                SnailShellCenterYRatio * size, SnailShellCoreRatio * size, segments);

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
