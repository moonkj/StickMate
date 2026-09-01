using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ EYES "불투명 바이저(가리개)" 회귀 — 2026-09-01, docs/UX_FLOW.md 38-7 옵션 E2(리더 승인).
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// EYES 6종은 이 라운드 전까지 <b>채움 없는 윤곽선</b>이었다(안대만 예외). 그 설계는
    /// "렌즈 안으로 눈동자가 비친다"를 그림의 내용으로 삼았는데, 같은 날 눈이 삭제되면서
    /// (<c>SceneBootstrapper.BakeEyes = false</c> / <c>CharacterPortraitStage.DrawEyes = false</c>)
    /// 비칠 것이 사라졌다. 남은 것은 <b>검은 얼굴 위에 그은 빈 네모</b>였다.
    ///
    /// 그래서 6종을 "스스로 불투명한 판"으로 다시 그렸고, 이 파일은 그 성질이 <b>값이 아니라
    /// 기하와 색으로</b> 성립하는지를 잠근다. 특히 다음 두 가지 조용한 재발을 막는다:
    ///  ① 누가 <c>filled: true</c>를 빠뜨려도 컴파일은 통과하고 <b>화면에서만</b> 얼굴이 비친다.
    ///  ② 채움 다각형이 자기교차가 되면 <see cref="AccessoryShapeBuilder.Triangulate"/>가
    ///     <b>일부만</b> 덮은 삼각형 뭉치를 돌려준다 — 예외 없이 구멍 뚫린 판이 나온다.
    ///
    /// ============================================================================
    /// 왜 "불투명"을 세 각도에서 재는가
    /// ============================================================================
    /// 불투명은 한 성질이 아니라 세 성질의 <b>동시 성립</b>이다. 하나만 재면 나머지 둘이 조용히 깨진다.
    ///  · <b>기하</b> — 채움 도형이 존재하고, 삼각형이 그 다각형을 <b>빈틈없이</b> 덮는가.
    ///  · <b>위치</b> — 그 채움이 실제로 <b>눈 자리</b>를 지나는가(판이 이마에 붙어 있으면 소용없다).
    ///  · <b>색</b> — 칠해진 색이 잉크(흰/검)와 구분되는가. 잉크와 같은 색 판은 그려도 안 보인다.
    ///
    /// 획 예산(37-6 규칙 1)은 여기서 다시 재지 않는다 — 그 규칙의 단일 정의처는
    /// <see cref="AccessoryStrokeBudgetTests"/>이고, 이번 라운드에 EYES 6종을 그 목록에 넣었다.
    /// </summary>
    public sealed class EyesVisorOpacityTests
    {
        /// <summary>배율 1.0 프리팹 실측 리그(다른 도형 테스트와 같은 출처).</summary>
        private static AccessoryShapeBuilder.Rig Rig(float facing = 1f)
        {
            const float H = StickConfig.BaselineCharacterTotalHeight;
            const float R = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(R, H - R,
                AccessoryShapeBuilder.BaselineShoulderLocalY,
                AccessoryShapeBuilder.BaselineHipLocalY, facing);
        }

        private static List<AccessoryShapeBuilder.Shape> Build(int item, float facing = 1f)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, EquipmentSlot.Eyes, item, Rig(facing));
            return sink;
        }

        private static string Label(int item)
            => $"EYES {item}번({ItemCatalog.Item(EquipmentSlot.Eyes, item).DisplayName})";

        /// <summary>
        /// 카테고리 전종. <b>상수로</b> 적는다 — 이 목록은 테스트 <b>수집</b> 시점에 돌기 때문에
        /// 여기서 <c>Resources.LoadAll</c>(카탈로그 지연 로드)을 깨우지 않는 편이 안전하다.
        /// <para>손으로 적은 목록이 낡는 위험은 <see cref="목록이_카탈로그와_같은_수다"/>가 막는다 —
        /// 아이템이 늘면 그 검사가 먼저 빨간불이 되어 "여기도 추가하라"고 말한다
        /// (Tests/PlayMode/PortraitEyeVisibilityTests가 쓰는 것과 같은 유지보수 알람 패턴).</para>
        /// </summary>
        private static IEnumerable<int> AllEyes()
        {
            yield return AccessoryShapeBuilder.EyesSunglasses;
            yield return AccessoryShapeBuilder.EyesRound;
            yield return AccessoryShapeBuilder.EyesGoggles;
            yield return AccessoryShapeBuilder.EyesMonocle;
            yield return AccessoryShapeBuilder.EyesBrowline;
            yield return AccessoryShapeBuilder.EyesPatch;
        }

        /// <summary>공허한 통과 방지 — 위 목록이 카탈로그보다 짧으면 새 아이템이 <b>아무 검사도 받지 않고</b>
        /// 출하된다(그리고 스위트는 초록불이다).</summary>
        [Test]
        public void 목록이_카탈로그와_같은_수다()
        {
            var listed = new HashSet<int>(AllEyes());
            Assert.AreEqual(ItemCatalog.ItemCountIn(EquipmentSlot.Eyes), listed.Count,
                "EYES 카테고리의 아이템 수와 이 파일의 검사 목록이 어긋납니다 — " +
                $"AllEyes()에 새 자리를 추가하세요(지금 목록: {listed.Count}개).");
        }

        /// <summary><b>앞쪽 눈에만</b> 있다고 스스로 선언한 아이템(33-2-2 #4의 규약). 나머지는 두 눈 다 가린다.</summary>
        private static bool CoversFrontEyeOnly(int item)
            => item == AccessoryShapeBuilder.EyesMonocle || item == AccessoryShapeBuilder.EyesPatch;

        // ============================================================================
        // 1. 기하 — 채움이 있고, 삼각형이 그 다각형을 빈틈없이 덮는다
        // ============================================================================

        [TestCaseSource(nameof(AllEyes))]
        public void 모든_가리개가_채움_실루엣을_갖는다(int item)
        {
            List<AccessoryShapeBuilder.Shape> shapes = Build(item);
            Assert.Greater(shapes.Count, 0, $"{Label(item)}이 도형을 하나도 만들지 않습니다.");

            int filled = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (!shapes[i].Filled) continue;
                filled++;
                Assert.IsTrue(shapes[i].Loop,
                    $"{Label(item)} '{shapes[i].Name}'이 채움인데 닫힌 고리가 아닙니다 — " +
                    "열린 선을 채우면 마지막 변이 제멋대로 이어져 없던 삼각형이 생깁니다.");
            }

            Assert.GreaterOrEqual(filled, 1,
                $"{Label(item)}에 채움 도형이 하나도 없습니다. 이 카테고리의 존재 이유가 '가린다'이므로 " +
                "채움은 옵션이 아닙니다(37-6 규칙 2). 눈이 삭제된 지금 윤곽선만 남은 안경은 " +
                "'검은 얼굴 위에 그은 빈 네모'입니다.");
        }

        /// <summary>
        /// 채움 면이 <b>다각형 전체</b>를 덮는가. 귀 자르기가 자기교차 도형을 만나면
        /// 남은 조각을 부채꼴로 덮는데(<c>Triangulate</c>의 마지막 루프), 그때 삼각형 합계 면적이
        /// 다각형 면적과 어긋난다 — 화면에서는 <b>구멍 뚫린 판</b>이 된다.
        /// </summary>
        [TestCaseSource(nameof(AllEyes))]
        public void 채움_삼각형이_판을_빈틈없이_덮는다(int item)
        {
            foreach (float facing in new[] { 1f, -1f })
            {
                List<AccessoryShapeBuilder.Shape> shapes = Build(item, facing);
                for (int i = 0; i < shapes.Count; i++)
                {
                    if (!shapes[i].Filled) continue;
                    Vector3[] p = shapes[i].Points;
                    int[] tris = AccessoryShapeBuilder.Triangulate(p);

                    Assert.AreEqual((p.Length - 2) * 3, tris.Length,
                        $"{Label(item)} '{shapes[i].Name}'(facing {facing:+0;-0})의 삼각형이 " +
                        $"{tris.Length / 3}개입니다 — 단순 다각형이면 정확히 {p.Length - 2}개여야 합니다.");

                    float triArea = 0f;
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        triArea += Mathf.Abs(TriangleArea(p[tris[t]], p[tris[t + 1]], p[tris[t + 2]]));
                    }
                    float polyArea = Mathf.Abs(SignedArea(p));
                    Assert.Greater(polyArea, 0f, $"{Label(item)} '{shapes[i].Name}'의 면적이 0입니다.");
                    Assert.AreEqual(polyArea, triArea, polyArea * 0.001f,
                        $"{Label(item)} '{shapes[i].Name}'(facing {facing:+0;-0})의 채움 삼각형이 " +
                        $"다각형의 {triArea / polyArea:P1}만 덮습니다 — 자기교차 도형입니다. " +
                        "화면에서는 구멍 뚫린 판으로 보입니다.");
                }
            }
        }

        /// <summary>실제로 <see cref="Mesh"/>가 만들어지고 <b>정점 색이 칠해지는가</b>.
        /// 도형 계산이 옳아도 메시 생성이 null을 돌려주면 화면에는 아무것도 없다.
        /// <para>메시는 <see cref="AccessoryShapeBuilder.BuildFillMesh"/> 문서가 요구하는 대로
        /// 이 테스트가 직접 <c>DestroyImmediate</c> 한다 — 누수 규약을 테스트도 지킨다.</para></summary>
        [TestCaseSource(nameof(AllEyes))]
        public void 채움_메시가_실제로_만들어진다(int item)
        {
            var color = new Color(0.2f, 0.6f, 0.9f, 1f);
            List<AccessoryShapeBuilder.Shape> shapes = Build(item);
            for (int i = 0; i < shapes.Count; i++)
            {
                if (!shapes[i].Filled) continue;
                Mesh mesh = AccessoryShapeBuilder.BuildFillMesh(shapes[i].Points, color);
                try
                {
                    Assert.IsNotNull(mesh,
                        $"{Label(item)} '{shapes[i].Name}'의 채움 메시가 만들어지지 않았습니다.");
                    Assert.GreaterOrEqual(mesh.triangles.Length, 3,
                        $"{Label(item)} '{shapes[i].Name}'의 채움 메시에 삼각형이 없습니다.");
                    Color[] colors = mesh.colors;
                    Assert.AreEqual(shapes[i].Points.Length, colors.Length,
                        $"{Label(item)} '{shapes[i].Name}'의 정점 색이 정점 수와 다릅니다 — " +
                        "Sprites-Default는 정점 색을 곱하므로 색이 비면 판이 검게 나옵니다.");
                    // 정확 일치를 요구하지 않는다 — Mesh의 정점 색 버퍼는 채널당 8비트(Color32)라
                    // 되읽으면 1/255 단위로 양자화된다. "칠해졌는가"를 재는 것이 목적이므로 그 폭을 허용한다.
                    const float Quantization = 1.5f / 255f;
                    for (int c = 0; c < colors.Length; c++)
                    {
                        Assert.AreEqual(color.r, colors[c].r, Quantization,
                            $"{Label(item)} '{shapes[i].Name}'의 {c}번 정점 색(R)이 다릅니다.");
                        Assert.AreEqual(color.g, colors[c].g, Quantization,
                            $"{Label(item)} '{shapes[i].Name}'의 {c}번 정점 색(G)이 다릅니다.");
                        Assert.AreEqual(color.b, colors[c].b, Quantization,
                            $"{Label(item)} '{shapes[i].Name}'의 {c}번 정점 색(B)이 다릅니다.");
                        Assert.AreEqual(color.a, colors[c].a, Quantization,
                            $"{Label(item)} '{shapes[i].Name}'의 {c}번 정점 <b>알파</b>가 다릅니다 — " +
                            "알파가 새면 '불투명한 판'이라는 이 카테고리의 전제가 무너집니다.");
                    }
                }
                finally
                {
                    if (mesh != null) Object.DestroyImmediate(mesh);
                }
            }
        }

        // ============================================================================
        // 2. 위치 — 판이 실제로 눈 자리를 지난다
        // ============================================================================

        /// <summary>드러난 눈 도형인가 — 이름이 <c>*Eye</c>로 끝나는 채움.
        /// <para>가리개 검사(<see cref="가리개_채움이_눈_자리를_덮는다"/>)가 <b>가리개만</b> 보게 하려고
        /// 쓴다. 이름 규약을 코드 한 곳에 두는 이유는, 다음 사람이 눈 좌표를 조금만 옮겨도
        /// "가리개가 뒤 눈을 덮었다"는 <b>엉뚱한 실패 메시지</b>를 보지 않게 하기 위해서다.</para></summary>
        private static bool IsDrawnEye(in AccessoryShapeBuilder.Shape shape)
            => shape.Name != null && shape.Name.EndsWith("Eye");

        /// <summary>
        /// <b>가리개</b>의 채움이 눈 중립 좌표를 덮는가. 이 검사가 없으면 이마에 붙은 띠도 "채움 있음"으로 통과한다.
        ///
        /// <para>눈 좌표는 지금 그려지지 않지만(<c>DrawEyes = false</c>)
        /// <see cref="AccessoryShapeBuilder.EyeOffsetXInHeadRadii"/>가 "얼굴에서 눈이 있던 자리"의
        /// 단일 정의처로 남아 있고, <b>가림 판정</b>이 그것을 읽는다.</para>
        ///
        /// <para>★ 2026-09-01(3차) — 프로브 대상을 "모든 채움"에서 <b>가리개 채움</b>(이름이
        /// <c>*Eye</c>가 아닌 것)으로 좁혔다. 외알안경·안대에 <b>드러난 눈</b>이 채움 도형으로
        /// 들어왔기 때문이다. 좁히지 않아도 지금 좌표에서는 우연히 통과하지만(드러난 눈은
        /// 정규화 거리 1.26으로 옛 눈 자리를 포함하지 않는다), 그건 <b>계약이 아니라 우연</b>이다.
        /// 계약을 명시로 바꾸고, 드러난 눈 쪽 계약은 아래 검사가 따로 맡는다.</para>
        /// </summary>
        [TestCaseSource(nameof(AllEyes))]
        public void 가리개_채움이_눈_자리를_덮는다(int item)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float ex = rig.HeadRadius * AccessoryShapeBuilder.EyeOffsetXInHeadRadii;
            float ey = rig.HeadCenterY + rig.HeadRadius * AccessoryShapeBuilder.EyeOffsetYInHeadRadii;

            var visors = new List<AccessoryShapeBuilder.Shape>();
            foreach (AccessoryShapeBuilder.Shape shape in Build(item))
            {
                if (!IsDrawnEye(shape)) visors.Add(shape);
            }

            Assert.IsTrue(FilledContains(visors, new Vector2(ex, ey)),
                $"{Label(item)}의 가리개 채움이 <b>앞쪽</b> 눈 자리({ex:F3}, {ey:F3})를 덮지 않습니다 — " +
                "가리개가 눈을 비껴가면 이름과 그림이 어긋납니다(원칙 1의 그림 버전).");

            bool backCovered = FilledContains(visors, new Vector2(-ex, ey));
            if (CoversFrontEyeOnly(item))
            {
                Assert.IsFalse(backCovered,
                    $"{Label(item)}는 <b>앞쪽 눈에만</b> 있어야 하는 아이템인데(33-2-2 #4) " +
                    "가리개가 뒤쪽 눈까지 덮었습니다 — 외알안경/안대의 존재 이유가 사라집니다.");
            }
            else
            {
                Assert.IsTrue(backCovered,
                    $"{Label(item)}의 가리개 채움이 <b>뒤쪽</b> 눈 자리를 덮지 않습니다 — " +
                    "두 눈을 가리는 아이템이 한쪽만 가리면 반대 방향을 볼 때 얼굴이 반쯤 드러납니다.");
            }
        }

        /// <summary>
        /// ★ 규칙 2-a — <b>한쪽만 가리는 물건은 가려지지 않은 눈을 보여 준다.</b>
        ///
        /// <para>사용자 요구("외눈안경처럼 한쪽만 가릴 때만 눈 노출")를 계약으로 옮긴 것이다.
        /// 두 눈을 다 가리는 4종은 <b>아무것도 보여 주지 않는다</b> — 렌즈 <b>안</b>으로 눈이 비치는
        /// 그림은 이 배율에서 기하학적으로 불가능하기 때문이다:</para>
        /// <code>
        /// 눈이 보이려면 a ≥ 0.75W · 테와 안 붙으려면 ρ ≥ a + 1.5W = 2.25W
        /// 두 렌즈가 안 붙으려면 d ≥ 3.00W  ->  바깥 끝 d + ρ ≥ 5.25W = 1.805R  >  1.0R (머리 밖)
        /// </code>
        /// <para>머리 지름이 5.82W뿐이라 "테 + 간격 + 눈 + 간격 + 테"가 한쪽 눈에조차 안 들어간다.</para>
        ///
        /// <para>그리고 드러난 눈은 <b>진행 반대쪽</b>에 있어야 한다 — 가려진 쪽에 눈을 그리면
        /// 그것은 "렌즈 안으로 비치는 눈"이고, 위 산술이 금지한 그림이다.</para>
        /// </summary>
        [TestCaseSource(nameof(AllEyes))]
        public void 한쪽만_가리는_물건만_반대쪽_눈을_보여준다(int item)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var eyes = new List<AccessoryShapeBuilder.Shape>();
            foreach (AccessoryShapeBuilder.Shape shape in Build(item))
            {
                if (IsDrawnEye(shape)) eyes.Add(shape);
            }

            if (!CoversFrontEyeOnly(item))
            {
                Assert.IsEmpty(eyes,
                    $"{Label(item)}는 두 눈을 다 가리는 아이템인데 눈 도형이 {eyes.Count}개 있습니다 — " +
                    "가려진 눈을 그리면 그것은 '렌즈 안으로 비치는 눈'이고, 이 배율에서 그릴 수 없습니다(규칙 2-a).");
                return;
            }

            Assert.AreEqual(1, eyes.Count,
                $"{Label(item)}는 한쪽만 가리므로 드러난 눈이 <b>정확히 1개</b>여야 합니다(현재 {eyes.Count}개).");

            AccessoryShapeBuilder.Shape eye = eyes[0];
            Assert.IsTrue(eye.Filled && eye.Loop,
                $"{Label(item)}의 드러난 눈이 채운 닫힌 도형이 아닙니다 — 윤곽선으로 그리면 " +
                "내부를 보이는 데 3.0W(1.03R), 즉 머리 반지름만 한 눈이 필요합니다(규칙 1).");
            Assert.AreEqual(AccessoryShapeBuilder.Accent, eye.Tone,
                $"{Label(item)}의 드러난 눈이 보조색이 아닙니다 — 이 아이템의 결정적 특징은 " +
                "'가려지지 않은 눈이 보인다'이고, 보조색은 그 한 부분에 씁니다(규칙 3-2).");

            for (int i = 0; i < eye.Points.Length; i++)
            {
                Assert.Less(eye.Points[i].x, 0f,
                    $"{Label(item)}의 드러난 눈이 <b>진행 방향쪽</b>(가려진 쪽)에 있습니다 — " +
                    "가려진 눈을 그리는 것은 위 산술이 금지한 '렌즈 안으로 비치는 눈'입니다.");
            }

            // 눈과 가리개가 1.5획 이상 떨어져 있어야 두 잉크가 한 덩어리로 뭉치지 않는다(규칙 4).
            float w = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii * rig.HeadRadius;
            float eyeRight = float.MinValue, visorLeft = float.MaxValue;
            for (int i = 0; i < eye.Points.Length; i++) eyeRight = Mathf.Max(eyeRight, eye.Points[i].x);
            foreach (AccessoryShapeBuilder.Shape shape in Build(item))
            {
                if (IsDrawnEye(shape) || !shape.Filled) continue;
                for (int i = 0; i < shape.Points.Length; i++)
                {
                    visorLeft = Mathf.Min(visorLeft, shape.Points[i].x);
                }
            }
            Assert.GreaterOrEqual((visorLeft - eyeRight) / w, 1.5f,
                $"{Label(item)}의 드러난 눈과 가리개가 {(visorLeft - eyeRight) / w:F2}획 떨어져 있습니다 — " +
                "1.5획 미만이면 두 잉크가 붙어 '가리개가 눈까지 덮은' 한 덩어리로 보입니다(규칙 4).");
        }

        /// <summary>좌우 반전에서도 같은 판이 <b>거울처럼</b> 선다. 비대칭 요소(선글라스 다리·고글 스트랩·
        /// 외알 체인·안대 끈)가 있는 카테고리라 반전은 실제 실패 자리다.</summary>
        [TestCaseSource(nameof(AllEyes))]
        public void 좌우를_반전해도_같은_판이_거울로_선다(int item)
        {
            List<AccessoryShapeBuilder.Shape> right = Build(item, +1f);
            List<AccessoryShapeBuilder.Shape> left = Build(item, -1f);

            Assert.AreEqual(right.Count, left.Count, $"{Label(item)}: 방향에 따라 도형 수가 다릅니다.");
            for (int i = 0; i < right.Count; i++)
            {
                Assert.AreEqual(right[i].Name, left[i].Name, $"{Label(item)}: 도형 순서가 다릅니다.");
                Assert.AreEqual(right[i].Filled, left[i].Filled,
                    $"{Label(item)} '{right[i].Name}': 한쪽 방향에서만 채움입니다.");
                Assert.AreEqual(right[i].Points.Length, left[i].Points.Length);
                for (int p = 0; p < right[i].Points.Length; p++)
                {
                    Assert.AreEqual(-right[i].Points[p].x, left[i].Points[p].x, 1e-5f,
                        $"{Label(item)} '{right[i].Name}'의 {p}번 점 x가 반전되지 않았습니다.");
                    Assert.AreEqual(right[i].Points[p].y, left[i].Points[p].y, 1e-5f,
                        $"{Label(item)} '{right[i].Name}'의 {p}번 점 y가 함께 뒤집혔습니다 " +
                        "(x에만 부호를 곱해야 합니다).");
                }
            }
        }

        // ============================================================================
        // 3. 색 — 잉크와 섞이지 않는다(37-6 규칙 8, 잉크 대비)
        // ============================================================================

        /// <summary>
        /// 판을 칠하는 색이 <b>흰 잉크에서도 검은 잉크에서도</b> 잉크와 구분되는가.
        /// 머리가 솔리드 잉크 원이 된 뒤로 이것은 취향이 아니라 <b>보이느냐 마느냐</b>의 문제다 —
        /// 흰 잉크 위의 아이보리 판, 검은 잉크 위의 짙은 남색 판은 그려도 없는 것과 같다.
        /// <para>구조적으로는 <c>ItemCatalog.WornColor</c>의 채도 하한·명도 창이 보장하지만,
        /// 그 창이 넓어지는 순간 EYES가 가장 먼저 사라진다(판이 얼굴 한복판에 있다).</para>
        /// </summary>
        [TestCaseSource(nameof(AllEyes))]
        public void 판_색이_흰_잉크에서도_검은_잉크에서도_구분된다(int item)
        {
            foreach ((Color ink, string inkName) in new[]
            {
                (Color.white, "흰 잉크"), (Color.black, "검은 잉크"),
            })
            {
                ItemCatalog.ResolveWornPalette(EquipmentSlot.Eyes, item, ink,
                    out Color primary, out Color secondary);

                foreach ((Color c, string role) in new[] { (primary, "주색"), (secondary, "보조색") })
                {
                    float diff = Mathf.Max(Mathf.Abs(c.r - ink.r),
                        Mathf.Max(Mathf.Abs(c.g - ink.g), Mathf.Abs(c.b - ink.b)));
                    Assert.GreaterOrEqual(diff, 0.35f,
                        $"{Label(item)}의 {role}이 {inkName}과 채널 최대 {diff:F2}만큼밖에 다르지 않습니다 " +
                        $"({c}) — 얼굴 한복판의 판이 잉크에 묻힙니다(37-6 규칙 8).");
                }
            }
        }

        // ============================================================================
        // 4. 카테고리 규약 — 레이어 / 구성 정원 / 서로 구분
        // ============================================================================

        [TestCaseSource(nameof(AllEyes))]
        public void 판이_머리카락과_모자_사이의_제자리에_깔린다(int item)
        {
            List<AccessoryShapeBuilder.Shape> shapes = Build(item);
            for (int i = 0; i < shapes.Count; i++)
            {
                Assert.AreEqual(AccessoryShapeBuilder.SortEyes, shapes[i].SortingOrder,
                    $"{Label(item)} '{shapes[i].Name}'이 레이어 {shapes[i].SortingOrder}로 나왔습니다.");

                if (!shapes[i].Filled) continue;
                // 채움은 자기 윤곽선 바로 아래(SortEyes − 1 = 7)에 깔린다. 그 값이 머리카락(6)보다
                // 위여야 "안경이 머리카락을 가린다"가 성립한다.
                Assert.Greater(shapes[i].FillSortingOrder, AccessoryShapeBuilder.SortHair,
                    $"{Label(item)} '{shapes[i].Name}'의 채움 레이어({shapes[i].FillSortingOrder})가 " +
                    $"머리카락({AccessoryShapeBuilder.SortHair}) 아래입니다 — 판 위로 앞머리가 올라옵니다.");
                Assert.Less(shapes[i].FillSortingOrder, AccessoryShapeBuilder.SortHead,
                    $"{Label(item)} '{shapes[i].Name}'의 채움이 모자({AccessoryShapeBuilder.SortHead}) 위로 " +
                    "올라왔습니다 — 모자를 눌러써도 안경이 그 위에 뜹니다.");
            }
        }

        [TestCaseSource(nameof(AllEyes))]
        public void 구성_정원과_보조색_개수를_지킨다(int item)
        {
            List<AccessoryShapeBuilder.Shape> shapes = Build(item);
            Assert.That(shapes.Count, Is.InRange(2, 4),
                $"{Label(item)}의 도형이 {shapes.Count}개입니다 — 정원은 2~4개입니다(37-6 규칙 5). " +
                "1개면 실루엣만 있고 식별 특징이 없고, 5개를 넘으면 배율 0.75에서 서로 먹습니다.");

            int accents = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Tone == AccessoryShapeBuilder.Accent) accents++;
            }
            Assert.AreEqual(1, accents,
                $"{Label(item)}의 보조색 도형이 {accents}개입니다 — 정확히 1개여야 합니다(37-6 규칙 3-2: " +
                "보조색은 '형제들과 나를 가르는 단 한 부분'에만).");
        }

        /// <summary>
        /// 6종이 <b>화면에서</b> 서로 다른가. 색으로 나누면 잉크 프리셋에 따라 흔들리므로
        /// <b>채움 실루엣만</b> 본다.
        /// <para>지표는 획 반폭(W/2)짜리 격자에 판을 찍어 만든 셀 집합의 대칭차 비율
        /// <c>|A△B| / |A∪B|</c>다. 사람이 "다르게 보인다"고 말하는 것에 가장 가까운 계측이고,
        /// 획보다 작은 차이는 격자가 자동으로 버린다.</para>
        /// <para>문턱 0.20의 근거: 실측 최솟값이 <b>0.27</b>(외알안경 vs 안대 — 둘 다 앞쪽 눈만 가리는
        /// 형제라 원래 가장 닮았다)이고, 나머지 14쌍은 0.38 이상이다. 0.20은 그 최솟값 아래로
        /// 여유를 둔 값이지, 통과를 위해 맞춘 값이 아니다.</para>
        /// </summary>
        [Test]
        public void 여섯_종이_화면에서_서로_구분된다()
        {
            const float MinDifference = 0.20f;

            AccessoryShapeBuilder.Rig rig = Rig();
            float cell = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii * rig.HeadRadius * 0.5f;
            var covered = new Dictionary<int, HashSet<long>>();
            foreach (int item in AllEyes()) covered[item] = FilledCells(Build(item), rig, cell);

            var keys = new List<int>(covered.Keys);
            for (int a = 0; a < keys.Count; a++)
            {
                Assert.Greater(covered[keys[a]].Count, 0,
                    $"{Label(keys[a])}의 채움이 격자를 하나도 덮지 않습니다 — 판이 획보다 작습니다.");

                for (int b = a + 1; b < keys.Count; b++)
                {
                    var union = new HashSet<long>(covered[keys[a]]);
                    union.UnionWith(covered[keys[b]]);
                    var sym = new HashSet<long>(covered[keys[a]]);
                    sym.SymmetricExceptWith(covered[keys[b]]);

                    float d = sym.Count / (float)union.Count;
                    Assert.GreaterOrEqual(d, MinDifference,
                        $"{Label(keys[a])}와 {Label(keys[b])}의 채움 실루엣이 {d:P0}만 다릅니다 — " +
                        "카테고리 안에서 구분되는 것이 곧 아이템의 존재 이유입니다(37-6 규칙 7-3).");
                }
            }
        }

        // ============================================================================
        // 계측 도구
        // ============================================================================

        /// <summary>머리를 넉넉히 감싸는 정사각 격자에서 <b>채움에 덮인 칸</b>의 집합.</summary>
        private static HashSet<long> FilledCells(List<AccessoryShapeBuilder.Shape> shapes,
            in AccessoryShapeBuilder.Rig rig, float cell)
        {
            var set = new HashSet<long>();
            float span = rig.HeadRadius * 1.3f;
            int n = Mathf.CeilToInt(span * 2f / cell);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    var p = new Vector2(-span + cell * (i + 0.5f),
                        rig.HeadCenterY - span + cell * (j + 0.5f));
                    if (FilledContains(shapes, p)) set.Add(i * 1000L + j);
                }
            }
            return set;
        }

        private static bool FilledContains(List<AccessoryShapeBuilder.Shape> shapes, Vector2 p)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Filled && Contains(shapes[i].Points, p)) return true;
            }
            return false;
        }

        private static bool Contains(Vector3[] poly, Vector2 p)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = poly[i], b = poly[(i + 1) % n];
                if ((a.y > p.y) != (b.y > p.y))
                {
                    float x = a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y);
                    if (p.x < x) inside = !inside;
                }
            }
            return inside;
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

        private static float TriangleArea(Vector3 a, Vector3 b, Vector3 c)
            => ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5f;
    }
}
