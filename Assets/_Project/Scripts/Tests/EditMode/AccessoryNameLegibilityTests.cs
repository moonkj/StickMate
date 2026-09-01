using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ "이름대로 읽히는가" 근사 지표 — 2026-09-01(2차), 리더 육안 검증 직후에 만들었다.
    ///
    /// ============================================================================
    /// 왜 새 자(尺)가 필요했나 — 5라운드가 초록불이면서 카드가 안 읽혔다
    /// ============================================================================
    /// 이날 액세서리 도형 라운드가 다섯 번 돌았고 전부 통과했다. 그때 쓴 지표는 둘뿐이었다.
    ///  · <b>규칙 1</b>(<see cref="AccessoryStrokeBudgetTests"/>) — "획보다 두꺼운가".
    ///  · <b>실루엣 구분</b>(<see cref="EyesVisorOpacityTests"/>, <see cref="AccessorySilhouetteDistinctionTests"/>)
    ///    — "두 도형이 서로 다른가".
    /// 그런데 리더가 실제 빌드를 캡처해 확대하니 <b>선글라스가 화살표</b>, <b>동그란안경이 아령</b>,
    /// <b>날개가 나뭇잎 한 장</b>이었다. 즉 깨진 것은 "이 도형이 <b>그 이름의 물건</b>으로 보이는가"인데
    /// <b>그 성질을 잰 지표가 하나도 없었다</b>.
    ///
    /// ============================================================================
    /// 이 파일이 재는 세 가지 (그리고 <b>못 재는 것</b>)
    /// ============================================================================
    /// "물건으로 보이는가"는 통째로는 계측할 수 없다. 대신 <b>깨졌을 때 반드시 함께 깨지는 성질</b>
    /// 셋을 잰다. 셋 다 리더가 실제로 잡은 결함에서 역으로 뽑았고, 각각 <b>옛 좌표를 얼린
    /// 네거티브 컨트롤</b>을 달아 "이 자가 실제로 빨간불을 낼 수 있음"을 같은 스위트에서 증명한다.
    ///
    ///  <b>A. 쌍 대칭성</b> — "쌍이어야 하는 것이 쌍인가". 날개·안경류는 좌우 한 쌍이 정체다.
    ///     옛 날개 실측 <b>1.000</b>(완전한 한쪽 쏠림) / 옛 선글라스 0.429.
    ///  <b>B. 부품 연결성</b> — "부품들이 한 덩어리로 묶여 있는가". 알과 체인이 떨어져 있으면
    ///     두 물건으로 읽힌다. 옛 외알안경 0.30획 / 옛 안대 0.50획만큼 떠 있었다.
    ///  <b>C. 카드 정규화 실루엣 차</b> — "카드에서 두 아이템이 다른 그림인가".
    ///     ★ 이것이 <b>V9(짧은망토 ≡ 긴망토)를 놓친 자리</b>다. 기존 실루엣 지표는 <b>월드 좌표</b>로
    ///     재는데 카드는 도형을 상자에 꽉 차게 <b>다시 스케일</b>한다. 그래서 "길이가 다르다"는
    ///     차이가 카드에서는 원리적으로 사라진다 — 옛 두 망토는 월드 지표를 통과하면서
    ///     정규화 뒤에는 <b>0.123만</b> 달랐다(문턱 0.20).
    ///
    /// <para><b>정직하게 적어 둔다 — 이 셋으로도 못 잡는 것이 있다.</b>
    /// 옛 고글(왼쪽에만 붙은 끈)은 <b>채움</b>이 대칭이라 A가 0.000이고, 끈이 렌즈에서 0.04획밖에
    /// 안 떨어져 B도 통과한다. 즉 <b>V3은 이 세 지표 중 무엇으로도 안 잡힌다</b> —
    /// "머리가 있어야만 성립하는 그림"이라는 성질은 여기 있는 어떤 자로도 잴 수 없다.
    /// 그런 결함은 지금도 <b>육안 캡처</b>로만 잡힌다. 그래서 도형 변경 라운드는 수치만으로
    /// 끝내지 않는다(Tasklist "리더 육안 검증 결과" 절의 결론).</para>
    /// </summary>
    public sealed class AccessoryNameLegibilityTests
    {
        private static float W => AccessorySilhouetteMetrics.StrokeInR;

        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        private static List<AccessoryShapeBuilder.Shape> Build(EquipmentSlot slot, int item)
            => AccessorySilhouetteMetrics.Build(Rig(), slot, item);

        private static string Name(EquipmentSlot slot, int item)
            => ItemCatalog.Item(slot, item).DisplayName;

        // ============================================================================
        // A. 쌍 대칭성 — 쌍이어야 하는 것이 쌍인가
        // ============================================================================

        /// <summary>"한 쌍"이 이름의 조건인 아이템. 앞쪽 눈에만 있는 외알안경·안대는 <b>일부러</b>
        /// 비대칭이라 여기 없다(33-2-2 #4의 규약이 그렇게 못박았다).</summary>
        private static IEnumerable<TestCaseData> PairedItems()
        {
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesSunglasses).SetName("EYES 선글라스");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesRound).SetName("EYES 동그란안경");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesGoggles).SetName("EYES 고글");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesBrowline).SetName("EYES 뿔테안경");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackWings).SetName("BACK 날개");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackFairyWings).SetName("BACK 요정날개");
        }

        /// <summary>문턱 0.15의 근거: 선글라스는 방향을 읽히게 하려고 앞 렌즈를 7% 키워 둔
        /// <b>의도된</b> 비대칭이 있고 그 실측이 0.059다. 0.15는 그 위, 그리고 옛 값
        /// (선글라스 0.429 / 날개 1.000) 아래로 넉넉히 잡은 값이다.</summary>
        private const float MaxPairAsymmetry = 0.15f;

        [TestCaseSource(nameof(PairedItems))]
        public void 쌍이어야_하는_것이_쌍이다(EquipmentSlot slot, int item)
        {
            float d = MirrorAsymmetry(Build(slot, item));
            Assert.LessOrEqual(d, MaxPairAsymmetry,
                $"{Name(slot, item)}의 채움이 좌우로 {d:P0} 어긋납니다 — 이 아이템은 이름부터 " +
                "<b>한 쌍</b>인데 그림은 한쪽으로 쏠려 있습니다(리더 육안 검증 V1·V7).");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 날개(두 깃이 <b>둘 다</b> 진행 반대쪽)를 그대로 재구성해
        /// 같은 자로 잰다. 이 값이 문턱 아래로 떨어지면 위 검사는 공허하게 초록불이 된다.</summary>
        [Test]
        public void 컨트롤_옛_한짝짜리_날개를_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float sy = rig.ShoulderY;
            var old = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("OldWingA", new[]
                {
                    rig.F(-r * 0.20f, sy), rig.F(-r * 0.95f, sy + r * 0.62f),
                    rig.F(-r * 2.55f, sy + r * 1.00f), rig.F(-r * 1.20f, sy + r * 0.02f),
                    rig.F(-r * 0.52f, sy - r * 0.26f),
                }, true, AccessoryShapeBuilder.SortBack, filled: true),
                new AccessoryShapeBuilder.Shape("OldWingB", new[]
                {
                    rig.F(-r * 0.25f, sy - r * 0.05f), rig.F(-r * 0.80f, sy + r * 0.22f),
                    rig.F(-r * 1.85f, sy + r * 0.35f), rig.F(-r * 0.85f, sy - r * 0.30f),
                    rig.F(-r * 0.38f, sy - r * 0.44f),
                }, true, AccessoryShapeBuilder.SortBack, filled: true),
            };

            float d = MirrorAsymmetry(old);
            Assert.Greater(d, MaxPairAsymmetry,
                $"옛 날개의 좌우 어긋남이 {d:P0}으로 측정됩니다 — 실측은 100%(두 깃이 둘 다 뒤쪽)였습니다. " +
                "이 자가 눈이 멀면 위 검사가 아무것도 지키지 못합니다.");
        }

        // ============================================================================
        // B. 부품 연결성 — 한 덩어리로 묶여 있는가
        // ============================================================================

        /// <summary>부품이 "물려 있다"고 볼 최대 간격. <b>0.25획</b>인 이유는 실측 보정이다:
        /// 붙어 보이던 것(옛 선글라스 0.00 / 옛 고글 0.04 / 옛 뿔테 0.08획)은 통과시키고,
        /// 리더·지표가 <b>따로 논다</b>고 판정한 것(옛 외알안경 0.30 / 옛 안대 0.50획)은 잡는
        /// 가장 좁은 자리다. 획 하나가 이 간격의 4배이므로 두 선의 잉크는 여전히 크게 겹친다.</summary>
        private const float MaxJoinGapInStrokes = 0.25f;

        private static IEnumerable<TestCaseData> ConnectedItems()
        {
            for (int i = 0; i < 6; i++)
            {
                yield return new TestCaseData(EquipmentSlot.Eyes, i).SetName($"EYES {i}번");
            }
            yield return new TestCaseData(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckScarf).SetName("NECK 목도리");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackWings).SetName("BACK 날개");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackFairyWings).SetName("BACK 요정날개");
        }

        /// <summary>
        /// ★ 2026-09-01(3차) — <b>드러난 눈</b>(이름이 <c>*Eye</c>로 끝나는 도형)은 이 검사에서 뺀다.
        ///
        /// <para>이유는 예외가 아니라 <b>정의</b>다. 외알안경·안대의 눈은 <b>액세서리의 부품이 아니라
        /// 캐릭터의 눈</b>이고, 규칙 4가 그것을 가리개에서 <b>1.5획 이상 떼어 놓으라</b>고 요구한다
        /// (붙으면 "가리개가 눈까지 덮은" 한 덩어리로 보인다).
        /// 즉 이 검사가 막으려는 실패("떨어진 조각은 <b>다른 물건</b>으로 읽힌다")는 여기서
        /// 실패가 아니라 <b>의도</b>다 — 눈은 실제로 다른 물건이다.</para>
        ///
        /// <para>눈이 정확히 1개이고 진행 반대쪽에 있으며 가리개와 1.5획 이상 떨어져 있다는 계약은
        /// <c>EyesVisorOpacityTests.한쪽만_가리는_물건만_반대쪽_눈을_보여준다</c>가 따로 잠근다.</para>
        /// </summary>
        [TestCaseSource(nameof(ConnectedItems))]
        public void 부품이_하나의_덩어리로_묶인다(EquipmentSlot slot, int item)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var shapes = new List<AccessoryShapeBuilder.Shape>();
            foreach (AccessoryShapeBuilder.Shape shape in Build(slot, item))
            {
                if (shape.Name != null && shape.Name.EndsWith("Eye")) continue;
                shapes.Add(shape);
            }
            float tol = W * rig.HeadRadius * MaxJoinGapInStrokes;

            Assert.IsTrue(IsConnected(shapes, tol, out string worst),
                $"{Name(slot, item)}의 부품이 서로 떨어져 있습니다({worst}) — 카드에는 몸이 없으므로 " +
                "떨어진 조각은 <b>다른 물건</b>으로 읽힙니다(리더 육안 검증 V4: '금색 원과 흰 선이 따로 논다').");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 외알안경의 체인 시작점(알에서 0.30획).</summary>
        [Test]
        public void 컨트롤_옛_외알안경_체인은_알에서_떨어져_있었다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float cy = AccessoryShapeBuilder.GlassesLocalY(rig);
            // 알은 <b>살아 있는 것</b>을 쓰고 체인만 옛 좌표로 얼린다 — 알 크기가 바뀌면 함께 움직인다.
            // 알도 <b>옛 반지름(0.40R)</b>으로 얼린다 — 이 컨트롤이 재는 것은 "옛 설계의 간격"이고,
            // 지금 알(0.44R)과 섞으면 존재한 적 없는 쌍을 재게 된다(그 상태에서 첫 실행이 통과했다).
            // ★ 2026-09-01(3차) — 알의 중심 x도 <b>옛 값(눈 중립 좌표 0.3409R)</b>으로 얼린다.
            //   지금 알은 드러난 눈과 대칭인 +0.62R에 있고, 살아 있는 상수를 쓰면 이 컨트롤이
            //   "옛 체인 + 새 알"이라는 존재한 적 없는 쌍을 재게 된다(이 파일이 이미 두 번 겪은 실패).
            const float OldMonocleOffsetRatio = 0.075f / 0.22f;   // = 옛 EyeOffsetXInHeadRadii
            var oldPod = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                float ang = Mathf.PI * 2f * i / 12f;
                oldPod[i] = rig.F(r * OldMonocleOffsetRatio + Mathf.Cos(ang) * r * 0.40f,
                    cy + Mathf.Sin(ang) * r * 0.40f);
            }
            var pod = new AccessoryShapeBuilder.Shape("OldPod", oldPod, true,
                AccessoryShapeBuilder.SortEyes, filled: true);
            var oldChain = new AccessoryShapeBuilder.Shape("OldChain", new[]
            {
                rig.F(r * 0.24f, cy - r * 0.48f),
                rig.F(r * 0.10f, cy - r * 0.84f),
                rig.F(r * 0.36f, cy - r * 1.08f),
            }, false, AccessoryShapeBuilder.SortEyes, tone: AccessoryShapeBuilder.Accent);

            float tol = W * r * MaxJoinGapInStrokes;
            Assert.IsFalse(IsConnected(new List<AccessoryShapeBuilder.Shape> { pod, oldChain }, tol, out _),
                "옛 체인이 알에 물려 있다고 나옵니다 — 연결성 자가 눈이 멀었습니다.");
        }

        // ============================================================================
        // C. 카드 정규화 실루엣 — 카드에서 두 아이템이 다른 그림인가
        // ============================================================================

        /// <summary>
        /// 문턱 <b>0.15</b>. 이 값은 고른 것이 아니라 <b>실측으로 끼워 맞춘</b> 것이고, 그 근거를 남긴다.
        /// <list type="bullet">
        ///   <item>확인된 결함 — 옛 짧은망토 vs 긴망토 <b>0.108</b>(리더 육안: "거의 동일한 그림").</item>
        ///   <item>가장 빠듯한 합격 — 새 짧은망토 vs 긴망토 0.180, 외알안경 vs 안대 0.217
        ///         (뒤엣것은 <b>둘 다 앞쪽 눈만 가리는</b> 형제라 원래 가장 닮았다).</item>
        ///   <item>여유 있는 합격 — 날개 vs 요정날개 0.272, 짧은망토 vs 날개 0.783.</item>
        /// </list>
        /// <para><b>이 자의 한계를 정직하게 적는다.</b> 0.108과 0.180 사이는 넓지 않다 —
        /// 즉 이 지표는 "확실히 다른가"를 재지 못하고 <b>"거의 같은 그림인가"만</b> 잡는다.
        /// 여기 있는 숫자가 커졌다고 그림이 좋아졌다는 뜻이 아니다. 판정은 여전히 눈이 한다.</para>
        /// <para>왜 <see cref="EyesVisorOpacityTests"/>의 0.20과 다른가: 그쪽은 <b>월드 크기 그대로</b>
        /// <b>채움만</b> 재고, 이쪽은 <b>정규화한 뒤 획까지</b> 잰다. 획을 포함하면 두 도형의 공통
        /// 테두리가 함께 커져 분모(합집합)가 늘고 비율이 내려간다 — 같은 그림 쌍이라도 값이 다르다.</para>
        /// </summary>
        private const float MinCardDifference = 0.15f;

        [TestCase(EquipmentSlot.Shoulders, TestName = "BACK 6종")]
        [TestCase(EquipmentSlot.Eyes, TestName = "EYES 6종")]
        [TestCase(EquipmentSlot.Neck, TestName = "NECK 6종")]
        [TestCase(EquipmentSlot.Head, TestName = "HEAD 6종")]
        public void 카드에서_같은_그림인_쌍이_없다(EquipmentSlot slot)
        {
            int count = ItemCatalog.ItemCountIn(slot);
            var cells = new List<HashSet<long>>(count);
            for (int i = 0; i < count; i++) cells.Add(NormalizedCells(Build(slot, i)));

            for (int a = 0; a < count; a++)
            {
                Assert.Greater(cells[a].Count, 0, $"{Name(slot, a)}의 잉크가 격자를 하나도 덮지 않습니다.");
                for (int b = a + 1; b < count; b++)
                {
                    float d = Difference(cells[a], cells[b]);
                    Assert.GreaterOrEqual(d, MinCardDifference,
                        $"{Name(slot, a)}와 {Name(slot, b)}의 <b>카드 그림</b>이 {d:P0}만 다릅니다. " +
                        "카드는 도형을 상자에 꽉 차게 다시 스케일하므로 <b>크기 차이는 사라집니다</b> — " +
                        "형태로 갈리지 않으면 두 카드는 같은 그림입니다(리더 육안 검증 V9).");
                }
            }
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 두 망토는 <b>월드</b> 실루엣 지표를 통과하면서
        /// 정규화 뒤에는 0.123만 달랐다. 그 두 사실이 동시에 성립함을 못박는다:
        /// 새 자가 옛 자를 대신하는 것이 아니라 <b>옛 자가 못 보던 축</b>을 본다는 증거다.</summary>
        [Test]
        public void 컨트롤_옛_두_망토는_월드에서는_갈리고_카드에서는_안_갈렸다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var shortCape = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("Old짧은망토",
                    AccessoryShapeBuilder.CapeOutline(rig, AccessoryShapeBuilder.CapeLengthRatio,
                        AccessoryShapeBuilder.CapeSpreadRatio, AccessoryShapeBuilder.CapeHemWaveRatio,
                        AccessoryShapeBuilder.CapeFrontSpreadRatio),
                    true, AccessoryShapeBuilder.SortBack, filled: true),
            };
            // 옛 긴 망토 = 지금과 같은 비율에 <b>제비꼬리 골이 없는</b> 밑단(그것이 이번 변경 전부다).
            var longCape = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("Old긴망토",
                    AccessoryShapeBuilder.CapeOutline(rig, AccessoryShapeBuilder.LongCapeLengthRatio,
                        AccessoryShapeBuilder.LongCapeSpreadRatio, AccessoryShapeBuilder.LongCapeHemWaveRatio,
                        AccessoryShapeBuilder.LongCapeFrontSpreadRatio),
                    true, AccessoryShapeBuilder.SortBack, filled: true),
            };

            float world = AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.ProfileOf(rig, shortCape,
                    AccessorySilhouetteMetrics.AnchorLocalY(rig, EquipmentSlot.Shoulders)),
                AccessorySilhouetteMetrics.ProfileOf(rig, longCape,
                    AccessorySilhouetteMetrics.AnchorLocalY(rig, EquipmentSlot.Shoulders)));
            Assert.Greater(world, W,
                $"옛 두 망토가 월드 실루엣에서 {world / W:F2}획만 다르다고 나옵니다 — " +
                "실측은 넉넉히 통과였습니다. 이 컨트롤이 다른 도형을 재고 있습니다.");

            float card = Difference(NormalizedCells(shortCape), NormalizedCells(longCape));
            Assert.Less(card, MinCardDifference,
                $"옛 두 망토의 카드 그림이 {card:P0} 다르다고 나옵니다 — 실측은 12%였습니다. " +
                "정규화 자가 눈이 멀면 V9이 다시 조용히 통과합니다.");
        }

        // ============================================================================
        // 계측 도구
        // ============================================================================

        private const int NormalizedGridSize = 64;

        /// <summary>
        /// 카드에서 <b>획 하나가 도형 전체 폭의 몇 배</b>인가. 이 자가 채움뿐 아니라 <b>선</b>까지
        /// 봐야 하는 이유는 실측이다 — 나비넥타이·왕관·줄무늬타이는 <b>채움이 0개인 선화</b>였고,
        /// 채움만 세면 이 자는 그 카드들을 <b>아예 보지 못했다</b>(첫 실행에서 "채움이 비었습니다"로
        /// 걸렸다). 2026-09-01(3차) 재설계로 셋 다 채움이 생겼지만, "선까지 본다"는 이 규약은
        /// 그대로 둔다 — 다음 아이템이 다시 선화일 수 있고, 획은 실제로 카드 그림의 일부다.
        /// <para>값의 유도(Interaction/CharacterInfoWindow의 아이콘 규약):
        /// 획 = <c>1.7 × (IconSize / 40)</c>, 도형이 차지하는 폭 = <c>IconSize × FitFraction(0.86)</c>.
        /// IconSize가 약분되어 <c>(1.7 / 40) / 0.86 = 0.0494</c>가 남는다 — 아이콘 크기와 무관한 비율이다.</para>
        /// </summary>
        private const float StrokeToSpanRatio = (1.7f / 40f) / 0.86f;

        /// <summary>도형을 <b>카드와 같은 방식</b>으로 상자에 맞춘 뒤(경계 상자의 긴 변을 1로)
        /// 균일 격자에서 <b>잉크에 덮인</b> 칸을 모은다 — 채움 안쪽이거나, 어떤 선에서 획 반폭 이내거나.
        /// <see cref="AccessoryCardIcon"/>의 <c>scale = size · FitFraction / span</c>과 같은 정규화다.</summary>
        private static HashSet<long> NormalizedCells(IList<AccessoryShapeBuilder.Shape> shapes)
        {
            Bounds(shapes, out Vector2 min, out Vector2 max);
            float span = Mathf.Max(max.x - min.x, max.y - min.y);
            var set = new HashSet<long>();
            if (span <= 1e-6f) return set;

            var center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            float step = 1f / NormalizedGridSize;
            float halfStroke = span * StrokeToSpanRatio * 0.5f;
            for (int i = 0; i < NormalizedGridSize; i++)
            {
                for (int j = 0; j < NormalizedGridSize; j++)
                {
                    var p = new Vector2(
                        center.x + (-0.5f + (i + 0.5f) * step) * span,
                        center.y + (-0.5f + (j + 0.5f) * step) * span);
                    if (FilledContains(shapes, p) || NearAnyEdge(shapes, p, halfStroke))
                    {
                        set.Add(i * 1000L + j);
                    }
                }
            }
            return set;
        }

        private static bool NearAnyEdge(IList<AccessoryShapeBuilder.Shape> shapes, Vector2 p, float radius)
        {
            var probe = new Vector3(p.x, p.y, 0f);
            for (int s = 0; s < shapes.Count; s++)
            {
                Vector3[] pts = shapes[s].Points;
                if (pts == null || pts.Length < 2) continue;
                int edges = shapes[s].Loop ? pts.Length : pts.Length - 1;
                for (int e = 0; e < edges; e++)
                {
                    if (PointToSegment(probe, pts[e], pts[(e + 1) % pts.Length]) <= radius) return true;
                }
            }
            return false;
        }

        private static float Difference(HashSet<long> a, HashSet<long> b)
        {
            var union = new HashSet<long>(a);
            union.UnionWith(b);
            if (union.Count == 0) return 0f;
            var sym = new HashSet<long>(a);
            sym.SymmetricExceptWith(b);
            return sym.Count / (float)union.Count;
        }

        /// <summary>채움을 <b>세로축 기준으로 뒤집었을 때</b> 얼마나 어긋나는가(0 = 완전 대칭).</summary>
        private static float MirrorAsymmetry(IList<AccessoryShapeBuilder.Shape> shapes)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float cell = W * rig.HeadRadius * 0.5f;
            Bounds(shapes, out Vector2 min, out Vector2 max);
            float centerY = (min.y + max.y) * 0.5f;
            float span = rig.HeadRadius * 3.2f;
            int n = Mathf.CeilToInt(span * 2f / cell);

            var direct = new HashSet<long>();
            var mirrored = new HashSet<long>();
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float x = -span + cell * (i + 0.5f);
                    float y = centerY - span + cell * (j + 0.5f);
                    if (FilledContains(shapes, new Vector2(x, y))) direct.Add(i * 1000L + j);
                    if (FilledContains(shapes, new Vector2(-x, y))) mirrored.Add(i * 1000L + j);
                }
            }
            return Difference(direct, mirrored);
        }

        /// <summary>도형들이 <paramref name="tolerance"/> 이내로 <b>하나의 덩어리</b>를 이루는가.</summary>
        private static bool IsConnected(IList<AccessoryShapeBuilder.Shape> shapes, float tolerance,
            out string worst)
        {
            worst = string.Empty;
            int n = shapes.Count;
            if (n <= 1) return true;

            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }

            float worstGap = 0f;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    float gap = ShapeGap(shapes[i], shapes[j]);
                    if (gap > tolerance) continue;
                    int a = Find(i), b = Find(j);
                    if (a != b) parent[a] = b;
                }
            }

            var roots = new HashSet<int>();
            for (int i = 0; i < n; i++) roots.Add(Find(i));
            if (roots.Count == 1) return true;

            // 어느 조각이 어디서 떨어졌는지 사람이 읽을 수 있게 남긴다.
            for (int i = 0; i < n; i++)
            {
                float nearest = float.MaxValue;
                int who = -1;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    float gap = ShapeGap(shapes[i], shapes[j]);
                    if (gap >= nearest) continue;
                    nearest = gap;
                    who = j;
                }
                if (who < 0 || nearest <= tolerance || nearest <= worstGap) continue;
                worstGap = nearest;
                worst = $"'{shapes[i].Name}'이 가장 가까운 '{shapes[who].Name}'에서 " +
                    $"{nearest / (W * Rig().HeadRadius):F2}획 떨어져 있습니다";
            }
            return false;
        }

        private const int SamplesPerEdge = 24;

        private static float ShapeGap(in AccessoryShapeBuilder.Shape a, in AccessoryShapeBuilder.Shape b)
        {
            float best = float.MaxValue;
            int ea = a.Loop ? a.Points.Length : a.Points.Length - 1;
            int eb = b.Loop ? b.Points.Length : b.Points.Length - 1;
            for (int i = 0; i < ea; i++)
            {
                Vector3 p0 = a.Points[i], p1 = a.Points[(i + 1) % a.Points.Length];
                for (int k = 0; k <= SamplesPerEdge; k++)
                {
                    Vector3 p = Vector3.Lerp(p0, p1, k / (float)SamplesPerEdge);
                    for (int j = 0; j < eb; j++)
                    {
                        best = Mathf.Min(best, PointToSegment(p,
                            b.Points[j], b.Points[(j + 1) % b.Points.Length]));
                    }
                }
            }
            return best;
        }

        private static float PointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 ab = new Vector2(b.x - a.x, b.y - a.y);
            float len = ab.sqrMagnitude;
            if (len < 1e-12f) return Vector2.Distance(new Vector2(p.x, p.y), new Vector2(a.x, a.y));
            float t = Mathf.Clamp01(((p.x - a.x) * ab.x + (p.y - a.y) * ab.y) / len);
            return Vector2.Distance(new Vector2(p.x, p.y), new Vector2(a.x + ab.x * t, a.y + ab.y * t));
        }

        private static bool FilledContains(IList<AccessoryShapeBuilder.Shape> shapes, Vector2 p)
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

        private static void Bounds(IList<AccessoryShapeBuilder.Shape> shapes, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < shapes.Count; i++)
            {
                Vector3[] pts = shapes[i].Points;
                if (pts == null) continue;
                for (int k = 0; k < pts.Length; k++)
                {
                    min = Vector2.Min(min, new Vector2(pts[k].x, pts[k].y));
                    max = Vector2.Max(max, new Vector2(pts[k].x, pts[k].y));
                }
            }
        }
    }
}
