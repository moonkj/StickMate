using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 장비 액세서리(모자/선글라스/나비넥타이/망토)의 <b>배율 연동 + 좌우 반전</b> 회귀 테스트 —
    /// 2026-08-29 성장/장비 라운드.
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 두 가지 실패
    /// ============================================================================
    /// (a) <b>배율 미추종</b> — 이 프로젝트가 이미 여러 번 겪은 유형이다. 액세서리 치수를 월드유닛
    ///     절대 상수로 적으면 StickConfig.characterScale이 0.75 -> 0.5로 바뀌는 순간 캐릭터만 작아지고
    ///     모자는 그대로 남아 <b>정수리 한참 위 허공</b>에 뜬다. 예외도 경고도 나지 않고 그림만 조용히
    ///     깨진다(Tests/PlayMode/RendererScaleRatioTests.cs가 같은 실패를 렌더러 4종에 대해 잠근 것과
    ///     같은 형태).
    /// (b) <b>좌우 반전 누락</b> — 이번 세션에 정확히 이 사고가 2번 있었다(무릎앉아 착지의 해부학적
    ///     제한, 활 든 손 방향). 모자 챙/안경다리/망토는 비대칭이라, 캐릭터가 왼쪽으로 걸을 때
    ///     뒤집히지 않으면 챙이 뒤통수에서 튀어나온다.
    ///
    /// ============================================================================
    /// 무엇을 어떻게 단언하는가 — RendererScaleRatioTests와 같은 3축
    /// ============================================================================
    ///  (A) <b>바깥에서 온 숫자</b>와 맞대는 절대 단언: 배율 1.0 프리팹의 실측 치수
    ///      (전신 2.2746944 / 머리반경 0.22 / 어깨 1.7646944 / 고관절 0.9346944)와 렌더러의 비율
    ///      상수만으로 손계산한 기대값 x 배율이 정확히 나온다. 자기 자신을 기준으로 하는 비율 비교가
    ///      아니므로, 절대 상수가 하나라도 남아 있으면 배율 1.0이 아닌 지점에서 즉시 깨진다.
    ///  (B) <b>모든 배율에서 참인 절대 조건</b>: 모자는 머리 위에, 선글라스는 머리 링 안에,
    ///      나비넥타이는 어깨~머리 아래 사이(=목)에, 망토 밑단은 고관절 아래에 있다.
    ///  (C) <b>좌우 반전</b>: facing을 뒤집으면 비대칭 요소의 x 부호가 정확히 뒤집히고, 챙(앞)과
    ///      망토/안경다리(뒤)는 <b>항상 서로 반대 부호</b>다.
    ///
    /// 검사 배율은 1.0(비율 기준선) / 0.75(현재 출하) / 0.5(직전 출하) 세 가지다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <see cref="AbsoluteConstantsWouldFailAtHalfScale"/> / <see cref="NoMirrorWouldFailFacingAssertion"/>가
    /// "절대 상수를 그대로 뒀다면 / 반전을 빼먹었다면 위 조건이 실제로 깨진다"를 같은 식으로 계산해
    /// 단언한다 — (A)/(C)가 통과하는 이유가 "조건이 너무 헐거워서"가 아님을 같은 파일에서 증명한다.
    /// </summary>
    public sealed class CharacterAccessoryScaleTests
    {
        private const float Tol = 1e-4f;

        // 배율 1.0 프리팹의 실측 치수(Editor/SceneBootstrapper.cs가 굽는 값 그대로).
        private const float BaseHeight = StickConfig.BaselineCharacterTotalHeight; // 2.2746944
        private const float BaseHeadRadius = 0.22f;
        private const float BaseShoulderY = 1.7646944f;
        private const float BaseHipY = 0.9346944f;
        private const float BaseHeadCenterY = BaseHeight - BaseHeadRadius;         // 2.0546944
        private const float BaseTorsoLength = BaseShoulderY - BaseHipY;            // 0.83

        // 렌더러의 비율 상수(CharacterAccessoryRenderer와 같은 값 — 바깥에서 손으로 옮겨 적은 사본이라
        // 렌더러 쪽 상수가 바뀌면 이 테스트가 먼저 빨개진다. 그게 이 사본의 목적이다).
        private const float HatBrimLineRatio = 0.62f;
        private const float HatCrownHeightRatio = 1.05f;
        private const float HatBrimReachRatio = 1.95f;
        private const float GlassesCenterRatio = 0.00f;
        private const float GlassesTempleReachRatio = 1.02f;
        private const float NeckCollarRiseRatio = 0.04f;   // 2026-08-30: 머리 중심 기준 -> 어깨선 기준으로 이전
        private const float CapeCollarRiseRatio = 0.10f;
        private const float CapeLengthRatio = 1.35f;
        /// <summary>2026-08-30 실루엣 재설계로 1.35 -> 2.45(AccessoryShapeBuilder.CapeSpreadRatio).
        /// 이 파일이 값을 <b>다시 적는</b> 이유는 원본과 같은 상수를 읽으면 "둘 다 같이 틀리는" 검사가
        /// 되기 때문이다 — 값이 바뀌면 여기도 손으로 바꾸는 것이 이 테스트의 의도다.</summary>
        private const float CapeSpreadRatio = 2.45f;
        private const float StrokeWidthAtScale1 = 0.048f;

        private const float ShippedScale = 0.75f;

        private readonly System.Collections.Generic.List<GameObject> _rigs =
            new System.Collections.Generic.List<GameObject>(2);

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _rigs.Count; i++)
            {
                if (_rigs[i] != null) Object.DestroyImmediate(_rigs[i]);
            }
            _rigs.Clear();
        }

        /// <summary>StickmanMetrics가 실측하는 소스만 갖춘 최소 리그 — RendererScaleRatioTests.BuildRig와
        /// 같은 구성이다(씬/프리팹은 배율 하나로만 구워지므로 한 실행에 두 배율을 볼 수 없다).</summary>
        private CharacterAccessoryRenderer Renderer(float scale, float facing)
        {
            var root = new GameObject($"AccessoryRig_{scale:F2}");
            root.transform.position = Vector3.zero;
            _rigs.Add(root);

            float height = BaseHeight * scale;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.4f * scale, height);
            capsule.offset = new Vector2(0f, height * 0.5f);

            var grab = root.AddComponent<CapsuleCollider2D>();
            grab.isTrigger = true; // StickmanMetrics가 트리거를 제외하는지까지 함께 확인된다.
            grab.size = new Vector2(0.8f * scale, height + 0.6f * scale);
            grab.offset = new Vector2(0f, height * 0.5f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, BaseHeadCenterY * scale, 0f);
            var outline = new GameObject("HeadOutline");
            outline.transform.SetParent(head.transform, false);
            var lr = outline.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 1;
            lr.SetPosition(0, new Vector3(BaseHeadRadius * scale, 0f, 0f));

            var arm = new GameObject("LeftArm");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0f, BaseShoulderY * scale, 0f);

            var leg = new GameObject("LeftLeg");
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0f, BaseHipY * scale, 0f);

            root.AddComponent<StickmanMetrics>();
            var renderer = root.AddComponent<CharacterAccessoryRenderer>();
            renderer.SetFacingForTests(facing);
            return renderer;
        }

        private static StickmanMetrics MetricsOf(CharacterAccessoryRenderer r) => r.GetComponent<StickmanMetrics>();

        // ============================================================================
        // (0) 리그 전제
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void 리그가_기대한_치수를_잰다(float scale)
        {
            StickmanMetrics m = MetricsOf(Renderer(scale, 1f));
            Assert.AreEqual(BaseHeight * scale, m.TotalHeight, Tol, $"배율 {scale:F2} 전신 높이");
            Assert.AreEqual(BaseHeadRadius * scale, m.HeadRadius, Tol, $"배율 {scale:F2} 머리 반경");
            Assert.AreEqual(BaseShoulderY * scale, m.ShoulderLocalY, Tol, $"배율 {scale:F2} 어깨");
            Assert.IsTrue(m.MeasuredFromHierarchy,
                "리그가 폴백 비율로 되메워졌습니다 — 계층 실측 경로를 타지 못하면 이 테스트는 아무것도 검증하지 못합니다.");
        }

        // ============================================================================
        // (A) 배율 비례 — 바깥에서 손계산한 기대값과 정확히 일치해야 한다
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void 액세서리_배치가_배율에_정확히_비례한다(float scale)
        {
            CharacterAccessoryRenderer r = Renderer(scale, 1f);
            string label = $"배율 {scale:F2}";

            float hatBrim = BaseHeadCenterY + BaseHeadRadius * HatBrimLineRatio;
            AssertScaled(hatBrim, scale, r.HatBrimLocalY, label, "모자 챙 선 높이");
            AssertScaled(hatBrim + BaseHeadRadius * HatCrownHeightRatio, scale, r.HatTopLocalY, label, "모자 꼭대기 높이");
            AssertScaled(BaseHeadRadius * HatBrimReachRatio, scale, r.HatBrimTipLocalX, label, "모자 챙 끝 x");
            AssertScaled(BaseHeadCenterY + BaseHeadRadius * GlassesCenterRatio, scale, r.GlassesLocalY, label, "선글라스 높이");
            AssertScaled(-BaseHeadRadius * GlassesTempleReachRatio, scale, r.GlassesTempleTipLocalX, label, "안경다리 끝 x");
            AssertScaled(BaseShoulderY + BaseHeadRadius * NeckCollarRiseRatio, scale, r.BowTieLocalY, label, "나비넥타이 높이");

            float collar = BaseShoulderY + BaseHeadRadius * CapeCollarRiseRatio;
            AssertScaled(collar, scale, r.CapeCollarLocalY, label, "망토 옷깃 높이");
            AssertScaled(collar - BaseTorsoLength * CapeLengthRatio, scale, r.CapeHemLocalY, label, "망토 밑단 높이");
            AssertScaled(-BaseHeadRadius * CapeSpreadRatio, scale, r.CapeTrailTipLocalX, label, "망토 자락 끝 x");
            AssertScaled(BaseTorsoLength, scale, r.TorsoLength, label, "몸통 길이");
            AssertScaled(StrokeWidthAtScale1, scale, r.StrokeWidth, label, "획 두께");
        }

        // ============================================================================
        // (B) 절대 조건 — 배율과 무관하게 "몸의 올바른 자리에 붙어 있다"
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void 액세서리가_모든_배율에서_몸의_제자리에_붙어있다(float scale)
        {
            CharacterAccessoryRenderer r = Renderer(scale, 1f);
            StickmanMetrics m = MetricsOf(r);
            string label = $"배율 {scale:F2}";

            float headTop = m.HeadTopLocalY;
            float headCenter = m.HeadCenterLocalY;
            float headBottom = headCenter - m.HeadRadius;

            // 모자: 챙은 머리 링 안(중심~정수리), 관 꼭대기는 정수리 위 — 단 신장의 15%를 넘게 솟지 않는다.
            Assert.IsTrue(r.HatBrimLocalY > headCenter && r.HatBrimLocalY < headTop,
                $"{label}: 모자 챙 {r.HatBrimLocalY:F4}가 머리 중심({headCenter:F4})~정수리({headTop:F4}) 밖입니다.");
            Assert.Greater(r.HatTopLocalY, headTop,
                $"{label}: 모자 꼭대기 {r.HatTopLocalY:F4}가 정수리({headTop:F4})보다 낮습니다 — 모자가 머리에 파묻혔습니다.");
            Assert.Less(r.HatTopLocalY, headTop + m.TotalHeight * 0.15f,
                $"{label}: 모자 꼭대기 {r.HatTopLocalY:F4}가 정수리 위 신장 15%를 넘어 허공에 떴습니다.");

            // 선글라스: 머리 링 안쪽.
            Assert.Less(Mathf.Abs(r.GlassesLocalY - headCenter), m.HeadRadius,
                $"{label}: 선글라스 {r.GlassesLocalY:F4}가 머리 링(중심 {headCenter:F4}, 반경 {m.HeadRadius:F4}) 밖입니다.");
            Assert.Less(Mathf.Abs(r.GlassesTempleTipLocalX), m.HeadRadius * 1.6f,
                $"{label}: 안경다리 끝 {r.GlassesTempleTipLocalX:F4}이 머리 반경의 1.6배를 넘어 뻗었습니다.");

            // 나비넥타이: 어깨보다 위, 머리 아래 = 목.
            Assert.IsTrue(r.BowTieLocalY > m.ShoulderLocalY && r.BowTieLocalY < headBottom,
                $"{label}: 나비넥타이 {r.BowTieLocalY:F4}가 어깨({m.ShoulderLocalY:F4})~머리 아래({headBottom:F4}) " +
                "사이(=목)에 있지 않습니다.");

            // 망토: 옷깃은 어깨 언저리, 밑단은 고관절 아래이면서 발보다는 위.
            Assert.IsTrue(r.CapeCollarLocalY >= m.ShoulderLocalY && r.CapeCollarLocalY < headBottom,
                $"{label}: 망토 옷깃 {r.CapeCollarLocalY:F4}가 어깨({m.ShoulderLocalY:F4})~머리 아래({headBottom:F4}) 밖입니다.");
            Assert.Less(r.CapeHemLocalY, m.HipLocalY,
                $"{label}: 망토 밑단 {r.CapeHemLocalY:F4}가 고관절({m.HipLocalY:F4})보다 높습니다 — 망토가 아니라 조끼입니다.");
            Assert.Greater(r.CapeHemLocalY, m.TotalHeight * 0.2f,
                $"{label}: 망토 밑단 {r.CapeHemLocalY:F4}이 신장의 20%보다 낮아 바닥을 씁니다.");
            Assert.Less(Mathf.Abs(r.CapeTrailTipLocalX), m.TotalHeight * 0.5f,
                $"{label}: 망토 자락 {r.CapeTrailTipLocalX:F4}이 신장의 절반보다 멀리 뻗었습니다.");

            // 획 두께는 머리 반경보다 훨씬 얇아야 형태가 읽힌다.
            Assert.Less(r.StrokeWidth, m.HeadRadius * 0.5f,
                $"{label}: 획 두께 {r.StrokeWidth:F4}가 머리 반경의 절반 이상이라 도형이 뭉갭니다.");
        }

        // ============================================================================
        // (C) 좌우 반전
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void 비대칭_액세서리가_진행_방향을_따라_뒤집힌다(float scale)
        {
            CharacterAccessoryRenderer right = Renderer(scale, +1f);
            float hatR = right.HatBrimTipLocalX;
            float capeR = right.CapeTrailTipLocalX;
            float templeR = right.GlassesTempleTipLocalX;

            right.SetFacingForTests(-1f);
            float hatL = right.HatBrimTipLocalX;
            float capeL = right.CapeTrailTipLocalX;
            float templeL = right.GlassesTempleTipLocalX;

            string label = $"배율 {scale:F2}";
            Assert.AreEqual(-hatR, hatL, Tol,
                $"{label}: 모자 챙이 좌우 반전되지 않았습니다(오른쪽 {hatR:F4} / 왼쪽 {hatL:F4}) — " +
                "왼쪽으로 걸을 때 챙이 뒤통수에서 튀어나옵니다.");
            Assert.AreEqual(-capeR, capeL, Tol,
                $"{label}: 망토 자락이 좌우 반전되지 않았습니다(오른쪽 {capeR:F4} / 왼쪽 {capeL:F4}).");
            Assert.AreEqual(-templeR, templeL, Tol,
                $"{label}: 안경다리가 좌우 반전되지 않았습니다(오른쪽 {templeR:F4} / 왼쪽 {templeL:F4}).");

            // 챙은 앞(진행 방향), 망토와 안경다리는 뒤 — 어느 방향을 보든 항상 반대 부호여야 한다.
            Assert.Less(hatR * capeR, 0f,
                $"{label}: 오른쪽을 볼 때 모자 챙({hatR:F4})과 망토({capeR:F4})가 같은 쪽을 향합니다 — " +
                "망토는 진행 반대쪽으로 흩날려야 합니다.");
            Assert.Less(hatL * capeL, 0f,
                $"{label}: 왼쪽을 볼 때 모자 챙({hatL:F4})과 망토({capeL:F4})가 같은 쪽을 향합니다.");
            Assert.Less(hatR * templeR, 0f,
                $"{label}: 모자 챙과 안경다리가 같은 쪽을 향합니다 — 안경다리는 귀 쪽(진행 반대)이어야 합니다.");
        }

        [Test]
        public void 나비넥타이는_좌우_대칭이라_반전해도_같다()
        {
            CharacterAccessoryRenderer r = Renderer(ShippedScale, +1f);
            float y = r.BowTieLocalY;
            r.SetFacingForTests(-1f);
            Assert.AreEqual(y, r.BowTieLocalY, Tol,
                "나비넥타이는 대칭 아이템이라 방향이 바뀌어도 배치가 같아야 합니다.");
        }

        // ============================================================================
        // 네거티브 컨트롤
        // ============================================================================

        /// <summary>배율 0.5에서 "절대 상수(=배율 1.0 값)를 그대로 남겼다면" (B)의 절대 조건이 실제로
        /// 깨진다는 것을 확인한다. 이 테스트가 실패하면 (B)가 헐거워 아무것도 걸러내지 못한다는 뜻이다.</summary>
        [Test]
        public void AbsoluteConstantsWouldFailAtHalfScale()
        {
            CharacterAccessoryRenderer r = Renderer(0.5f, 1f);
            StickmanMetrics m = MetricsOf(r);

            float legacyHatTop = BaseHeadCenterY + BaseHeadRadius * (HatBrimLineRatio + HatCrownHeightRatio); // 2.3253
            Assert.Greater(legacyHatTop, m.HeadTopLocalY + m.TotalHeight * 0.15f,
                $"배율 0.5에서 절대 상수 모자 꼭대기 {legacyHatTop:F4}가 허용 상한 " +
                $"{(m.HeadTopLocalY + m.TotalHeight * 0.15f):F4}을 넘지 않습니다 — (B)의 모자 조건이 헐겁습니다.");

            float legacyBowTie = BaseShoulderY + BaseHeadRadius * NeckCollarRiseRatio; // 1.7735
            Assert.Greater(legacyBowTie, m.HeadCenterLocalY - m.HeadRadius,
                $"배율 0.5에서 절대 상수 나비넥타이 {legacyBowTie:F4}가 머리 아래({(m.HeadCenterLocalY - m.HeadRadius):F4})보다 " +
                "높지 않습니다 — (B)의 목 조건이 헐겁습니다.");

            float legacyCapeHem = BaseShoulderY + BaseHeadRadius * CapeCollarRiseRatio - BaseTorsoLength * CapeLengthRatio;
            Assert.Greater(legacyCapeHem, m.HipLocalY,
                $"배율 0.5에서 절대 상수 망토 밑단 {legacyCapeHem:F4}이 고관절({m.HipLocalY:F4})보다 낮습니다 — " +
                "(B)의 망토 조건이 헐겁습니다.");
        }

        /// <summary>"좌우 반전을 빼먹었다면"(= 두 방향에서 같은 x가 나온다면) (C)의 단언이 실제로 깨지는지.</summary>
        [Test]
        public void NoMirrorWouldFailFacingAssertion()
        {
            CharacterAccessoryRenderer r = Renderer(ShippedScale, 1f);
            float hat = r.HatBrimTipLocalX;
            Assert.Greater(Mathf.Abs(hat - (-hat)), Tol,
                $"모자 챙 x가 {hat:F4}로 0에 가까워, 반전을 빼먹어도 (C)의 단언이 통과해버립니다 — " +
                "챙이 실제로 한쪽으로 뻗어 있어야 이 테스트가 의미를 가집니다.");
        }

        private static void AssertScaled(float expectedAtScale1, float scale, float actual, string label, string what)
        {
            Assert.AreEqual(expectedAtScale1 * scale, actual, Tol,
                $"{label}: {what}이 {actual:F4}입니다 — 배율 1.0 기대값 {expectedAtScale1:F4} x {scale:F2} = " +
                $"{(expectedAtScale1 * scale):F4}가 나와야 합니다. 절대 월드유닛 상수가 남아 있거나 비율 분자/분모가 잘못됐습니다.");
        }
    }
}
