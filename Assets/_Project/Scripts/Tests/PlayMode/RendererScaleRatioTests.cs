using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 렌더러 4종(스트레스 게이지 / 포모도로 감시자 / 가출 / 투두 종이)의 **캐릭터 기준 배치 비율화**
    /// 회귀 테스트 — 2026-08-29 리더 지시 "렌더러 4종 오프셋 비율화".
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// 사용자 요구로 캐릭터 전신 높이가 2.2747 -> 1.1373유닛(배율 0.5)이 되었고 앞으로도 설정으로 계속
    /// 조정된다. 그런데 위 네 렌더러는 배치·크기·속도를 **절대 월드유닛 상수**로 들고 있었다. 절대
    /// 상수는 예외도 경고도 내지 않고 조용히 거동만 바꾼다 — 배율 0.5에서 과자(y+1.02, 반지름 0.30)와
    /// 종이(y+1.02)는 정수리(1.137) 위 허공에 몸통만 한 크기로 떠 있고, 발밑 링(반지름 0.54)은 캐릭터
    /// 키의 절반을 삼켜 몸을 가로지른다. "로직은 도는데 그림이 몸에서 떨어져 나간다"는, 이 프로젝트가
    /// 이미 여러 번 겪은 유형의 실패다.
    ///
    /// ============================================================================
    /// 무엇을 어떻게 단언하는가 — 비율 비교가 아니라 **절대 조건**이다
    /// ============================================================================
    /// "배율 0.5 값 == 배율 1.0 값의 절반"만 단언하면 <b>둘 다 틀린 경우를 통과시킨다</b>. 그래서 세 축을
    /// 함께 못박는다:
    ///
    ///  (A) 종전 절대 상수 x 배율 — 비율화 이전의 **검증을 마친 종전 값**(0.40 / 0.048 / 0.54 / 0.08 /
    ///      0.05 / 0.92 / 1.02 / 0.30 / 0.052 / 0.66 / 0.24 / 0.045)에 배율을 곱한 값이 정확히 나온다.
    ///      배율 1.0에서는 종전과 완전히 같은 그림이라는 뜻이고(= 회귀 없음의 증거), 다른 배율에서는
    ///      절대 상수가 하나라도 남아 있으면 즉시 깨진다. 자기 자신을 기준으로 하는 비율 비교가 아니라
    ///      <b>바깥에서 온 숫자</b>와 맞대는 절대 단언이다.
    ///  (B) 절대 조건 — <b>모든 배율에서</b> 각 연출이 실제로 몸에 붙어 있다. 과자/종이는 발바닥~정수리
    ///      사이에 온전히 들어오고, 어깨 표시는 고관절~머리중심 사이에 있고, 링은 발밑에, 곁눈질은 머리
    ///      안에 있다. 배율과 무관하게 참이어야 하는 명제들이다.
    ///
    /// 검사 배율은 1.0 / 0.75 / 0.5 세 가지다. <b>0.75는 현재 출하 배율</b>이고(사용자 요구
    /// "지금보다 1.5배" -> StickConfig.characterScale = 0.75), 0.5는 직전 출하 배율, 1.0은 비율의 기준선이다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 (이 테스트가 정말 무언가를 보고 있는가)
    /// ============================================================================
    /// AbsoluteConstantsWouldFailScaledAssertion / ...BodyContainmentAtHalfScale이 <b>종전 절대 상수를
    /// 그대로 되살렸을 때</b> (A)/(B)의
    /// 조건이 실제로 깨진다는 것을 같은 식으로 계산해 단언한다. 즉 (B)가 통과하는 이유가 "조건이 너무
    /// 헐거워서"가 아님을 같은 파일 안에서 증명한다.
    /// ★ 실제로 손으로도 확인했다(2026-08-29): 네 렌더러의 앵커 프로퍼티를 종전 절대값
    ///   (ShoulderAnchorLocalY=1.33 / RingRadius=0.54 / SnackOffsetLocalY=1.02 / SnackRadius=0.30 /
    ///   PaperOffsetLocalY=1.02)으로 되돌려 배치 모드로 돌린 결과 4개 테스트가 전부 빨개졌고, 메시지도
    ///   의도한 그대로였다("과자 윗변 1.3200가 정수리 1.1373를 넘어 허공에 떴습니다" 등).
    ///
    /// ============================================================================
    /// 리그를 손으로 조립하는 이유
    /// ============================================================================
    /// 프리팹/씬은 StickConfig.characterScale 하나로 구워지므로 한 번 실행에 두 배율을 동시에 볼 수 없다.
    /// 여기서는 Core/StickmanMetrics.cs가 실측하는 소스(루트의 비-트리거 CapsuleCollider2D, "Head/
    /// HeadOutline" 링 LineRenderer, "LeftArm"/"LeftLeg"의 부착 높이)만 갖춘 최소 리그를 두 벌 만들어
    /// 배율 1.0과 0.5를 나란히 비교한다 — 렌더러가 읽는 창구가 StickmanMetrics 하나뿐이라 가능한 방식이고,
    /// 그 자체가 "치수 조회 경로가 정말 하나인가"에 대한 확인이기도 하다.
    /// </summary>
    public sealed class RendererScaleRatioTests
    {
        private const float Tol = 1e-4f;

        /// <summary>배율 1.0 프리팹의 실측 치수(Editor/SceneBootstrapper.cs가 굽는 값 그대로).</summary>
        private const float BaseHeight = StickConfig.BaselineCharacterTotalHeight; // 2.2746944
        private const float BaseHeadRadius = 0.22f;
        private const float BaseShoulderY = 1.7646944f;
        private const float BaseHipY = 0.9346944f;

        /// <summary>검사 배율 — 1.0(비율 기준선) / 0.75(현재 출하) / 0.5(직전 출하).</summary>
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

        private GameObject Rig(float scale)
        {
            GameObject go = BuildRig($"ScaleRig_{scale:F2}", scale);
            _rigs.Add(go);
            return go;
        }

        /// <summary>
        /// StickmanMetrics가 실측하는 소스만 갖춘 최소 캐릭터 리그. 컴포넌트 부착 순서가 중요하다 —
        /// StickmanMetrics.Awake()가 즉시 계층을 재므로 지오메트리를 <b>먼저</b> 다 만든 뒤에 붙인다.
        /// </summary>
        private static GameObject BuildRig(string name, float scale)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;

            float height = BaseHeight * scale;

            // (1) 전신 높이 — 루트의 비-트리거 캡슐.
            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.4f * scale, height);
            capsule.offset = new Vector2(0f, height * 0.5f);

            // (1-b) 트리거 캡슐(GrabArea)도 한 벌 둔다 — StickmanMetrics가 이것을 **제외**하는지까지
            //       함께 확인된다(포함해 버리면 전신 높이가 부풀어 아래 모든 단언이 어긋난다).
            var grab = root.AddComponent<CapsuleCollider2D>();
            grab.isTrigger = true;
            grab.size = new Vector2(0.8f * scale, height + 0.6f * scale);
            grab.offset = new Vector2(0f, height * 0.5f);

            // (2) 머리 반경 — "Head/HeadOutline" 링 LineRenderer의 첫 점 x.
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            var outline = new GameObject("HeadOutline");
            outline.transform.SetParent(head.transform, false);
            var lr = outline.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 1;
            lr.SetPosition(0, new Vector3(BaseHeadRadius * scale, 0f, 0f));

            // (3)(4) 어깨 / 고관절 — 관절이 없는 리그에서는 위 마디의 localPosition.y가 부착점이다.
            var arm = new GameObject("LeftArm");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0f, BaseShoulderY * scale, 0f);

            var leg = new GameObject("LeftLeg");
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0f, BaseHipY * scale, 0f);

            root.AddComponent<StickmanMetrics>();
            return root;
        }

        private static StickmanMetrics MetricsOf(GameObject rig) => rig.GetComponent<StickmanMetrics>();

        private static T AddRenderer<T>(GameObject rig) where T : MonoBehaviour => rig.AddComponent<T>();

        // ============================================================================
        // (0) 리그 자체가 기대한 치수인가 — 아래 모든 단언의 전제.
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void RigMeasuresExpectedDimensions(float scale)
        {
            StickmanMetrics m = MetricsOf(Rig(scale));

            Assert.AreEqual(BaseHeight * scale, m.TotalHeight, Tol,
                $"배율 {scale:F2} 리그의 전신 높이가 기대치와 다릅니다 — 트리거 캡슐(GrabArea)이 실측에 섞였을 수 있습니다.");
            Assert.AreEqual(scale, m.Scale, Tol, $"배율 {scale:F2} 리그의 Scale이 다릅니다.");
            Assert.AreEqual(BaseShoulderY * scale, m.ShoulderLocalY, Tol,
                $"배율 {scale:F2} 리그의 실측 어깨 높이가 {(BaseShoulderY * scale):F4}가 아닙니다 — 리더가 지정한 기준점입니다.");
            Assert.IsTrue(m.MeasuredFromHierarchy,
                "리그가 폴백 비율로 되메워졌습니다 — 계층 실측 경로를 타지 못하면 이 테스트는 아무것도 검증하지 못합니다.");
        }

        // ============================================================================
        // (1) StressGaugeRenderer — 어깨 표시 / 한숨 퍼프
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void StressGaugePlacementIsCharacterRelative(float scale)
        {
            GameObject rig = Rig(scale);
            var r = AddRenderer<StressGaugeRenderer>(rig);
            StickmanMetrics m = MetricsOf(rig);
            string label = $"배율 {scale:F2}";

            // (A) 종전 절대 상수 x 배율.
            AssertScaled(0.40f, scale, r.ShoulderHalfSpan, label, "어깨 표시 폭");
            AssertScaled(0.048f, scale, r.StrokeWidth, label, "획 두께");

            // (B) 절대 조건 — 어깨 표시는 배율과 무관하게 **고관절과 머리 중심 사이**에 있어야 한다.
            //     종전 상수 1.33은 배율 1.0에서조차 어깨(1.7647)가 아니라 갈비뼈를 가리켰다(접지 보정
            //     footLift 이전 프리팹에서 옮겨 적은 값). 이제는 실측 어깨 그 자체를 쓴다.
            Assert.AreEqual(m.ShoulderLocalY, r.ShoulderAnchorLocalY, Tol,
                $"{label}: 어깨 표시가 StickmanMetrics 실측 어깨({m.ShoulderLocalY:F4})에 붙어 있지 않습니다.");
            Assert.IsTrue(r.ShoulderAnchorLocalY > m.HipLocalY && r.ShoulderAnchorLocalY < m.HeadCenterLocalY,
                $"{label}: 어깨 표시 높이 {r.ShoulderAnchorLocalY:F4}가 고관절({m.HipLocalY:F4})~머리중심({m.HeadCenterLocalY:F4}) 밖입니다.");
            Assert.IsTrue(r.SighSpawnLocalY > m.HeadCenterLocalY && r.SighSpawnLocalY <= m.HeadTopLocalY,
                $"{label}: 한숨 퍼프 높이 {r.SighSpawnLocalY:F4}가 머리(중심 {m.HeadCenterLocalY:F4} ~ 정수리 {m.HeadTopLocalY:F4}) 밖입니다.");
            Assert.IsTrue(r.ShoulderHalfSpan < m.TotalHeight * 0.5f,
                $"{label}: 어깨 표시 폭 {r.ShoulderHalfSpan:F4}이 전신 높이의 절반을 넘습니다.");
        }

        // ============================================================================
        // (2) FocusWatchRenderer — 발밑 타이머 링 / 머리 옆 곁눈질
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void FocusWatchPlacementIsCharacterRelative(float scale)
        {
            GameObject rig = Rig(scale);
            var r = AddRenderer<FocusWatchRenderer>(rig);
            StickmanMetrics m = MetricsOf(rig);
            string label = $"배율 {scale:F2}";

            // (A) 종전 절대 상수 x 배율.
            AssertScaled(0.54f, scale, r.RingRadius, label, "링 반지름");
            AssertScaled(0.08f, scale, r.RingCenterLocalY, label, "링 중심 높이");
            AssertScaled(0.05f, scale, r.StrokeWidth, label, "획 두께");

            {
                // (B) 절대 조건 — 링은 18절이 지정한 "캐릭터 발밑"이다(고관절보다 아래).
                Assert.IsTrue(r.RingCenterLocalY >= 0f && r.RingCenterLocalY < m.HipLocalY,
                    $"{label}: 링 중심 {r.RingCenterLocalY:F4}가 발밑(0 ~ 고관절 {m.HipLocalY:F4})을 벗어났습니다.");
// 18절이 지정한 것은 "캐릭터 발밑"의 작은 링이다. 링 윗변이 고관절을 넘어 올라오면
                // 발밑 위젯이 아니라 몸을 가로지르는 원이 된다 — 배율 0.5에서 종전 절대 반지름 0.54를
                // 그대로 두면 정확히 그 일이 벌어진다(윗변 0.62 vs 고관절 0.4673).
                Assert.IsTrue(r.RingCenterLocalY + r.RingRadius < m.HipLocalY,
                    $"{label}: 링 윗변 {(r.RingCenterLocalY + r.RingRadius):F4}이 고관절 {m.HipLocalY:F4}을 넘어 " +
                    "몸을 가로지릅니다 — '발밑' 위젯이 아닙니다.");
                // 곁눈질은 머리 안(중심 ~ 정수리)에 있어야 한다.
                Assert.IsTrue(r.GlanceLocalY > m.HeadCenterLocalY && r.GlanceLocalY <= m.HeadTopLocalY,
                    $"{label}: 곁눈질 높이 {r.GlanceLocalY:F4}가 머리(중심 {m.HeadCenterLocalY:F4} ~ 정수리 {m.HeadTopLocalY:F4}) 밖입니다.");
            }
        }

        // ============================================================================
        // (3) RunawayRenderer — 과자
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void RunawaySnackPlacementIsCharacterRelative(float scale)
        {
            GameObject rig = Rig(scale);
            var r = AddRenderer<RunawayRenderer>(rig);
            StickmanMetrics m = MetricsOf(rig);
            string label = $"배율 {scale:F2}";

            // (A) 종전 절대 상수 x 배율.
            AssertScaled(0.92f, scale, r.SnackOffsetLocalX, label, "과자 가로 오프셋");
            AssertScaled(1.02f, scale, r.SnackOffsetLocalY, label, "과자 세로 오프셋");
            AssertScaled(0.30f, scale, r.SnackRadius, label, "과자 반지름");
            AssertScaled(0.052f, scale, r.StrokeWidth, label, "획 두께");

            {
                // (B) 절대 조건 — 과자는 몸통 높이 안(발바닥 위, 정수리 아래)에 **온전히** 들어와야 한다.
                //     배율 0.5에서 종전 절대값을 남기면 과자 윗변 1.32가 정수리 1.137을 넘어 허공에 뜬다.
                Assert.IsTrue(r.SnackOffsetLocalY - r.SnackRadius > 0f,
                    $"{label}: 과자 아랫변 {(r.SnackOffsetLocalY - r.SnackRadius):F4}가 발바닥 아래로 내려갔습니다.");
                Assert.IsTrue(r.SnackOffsetLocalY + r.SnackRadius < m.HeadTopLocalY,
                    $"{label}: 과자 윗변 {(r.SnackOffsetLocalY + r.SnackRadius):F4}가 정수리 {m.HeadTopLocalY:F4}를 넘어 허공에 떴습니다.");
                // 손이 닿는 거리 — 가로로 반 키 이상 떨어지면 "옆에 놓아준 과자"로 읽히지 않는다.
                Assert.IsTrue(r.SnackOffsetLocalX < m.TotalHeight * 0.5f,
                    $"{label}: 과자 가로 오프셋 {r.SnackOffsetLocalX:F4}이 전신 높이의 절반 이상 떨어져 있습니다.");
            }
        }

        // ============================================================================
        // (4) TodoReminderRenderer — 손에 든 종이
        // ============================================================================

        [TestCase(1.0f)]
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void TodoPaperPlacementIsCharacterRelative(float scale)
        {
            GameObject rig = Rig(scale);
            var r = AddRenderer<TodoReminderRenderer>(rig);
            StickmanMetrics m = MetricsOf(rig);
            string label = $"배율 {scale:F2}";

            // (A) 종전 절대 상수 x 배율.
            AssertScaled(0.66f, scale, r.PaperOffsetLocalX, label, "종이 가로 오프셋");
            AssertScaled(1.02f, scale, r.PaperOffsetLocalY, label, "종이 세로 오프셋");
            AssertScaled(0.24f, scale, r.PaperHalfWidth, label, "종이 반폭");
            AssertScaled(0.30f, scale, r.PaperHalfHeight, label, "종이 반높이");
            AssertScaled(0.045f, scale, r.StrokeWidth, label, "획 두께");

            {
                // (B) 절대 조건 — 종이는 "손에 들고 있는 것처럼" 보여야 하므로 몸통 높이 안에 온전히 든다.
                Assert.IsTrue(r.PaperOffsetLocalY - r.PaperHalfHeight > 0f,
                    $"{label}: 종이 아랫변 {(r.PaperOffsetLocalY - r.PaperHalfHeight):F4}가 발바닥 아래로 내려갔습니다.");
                Assert.IsTrue(r.PaperOffsetLocalY + r.PaperHalfHeight < m.HeadTopLocalY,
                    $"{label}: 종이 윗변 {(r.PaperOffsetLocalY + r.PaperHalfHeight):F4}가 정수리 {m.HeadTopLocalY:F4}를 넘어 허공에 떴습니다.");
                // 손 닿는 거리.
                Assert.IsTrue(r.PaperOffsetLocalX < m.TotalHeight * 0.5f,
                    $"{label}: 종이 가로 오프셋 {r.PaperOffsetLocalX:F4}이 전신 높이의 절반 이상 떨어져 있습니다.");
            }
        }

        // ============================================================================
        // 네거티브 컨트롤 — 위 (B)의 절대 조건이 정말로 무언가를 걸러내는가
        // ============================================================================

        /// <summary>
        /// 종전 <b>절대 상수</b>를 그대로 되살렸을 때 위 (A)의 단언이 반드시 깨진다는 것을 확인한다.
        /// 배율이 1.0이 아닌 모든 지점에서 성립하는, 이 파일에서 가장 넓게 작동하는 안전망이다.
        /// 이 테스트가 실패한다면 (A)가 아무것도 걸러내지 못한다는 뜻이므로 (A) 쪽을 고쳐야 한다.
        /// </summary>
        [TestCase(ShippedScale)]
        [TestCase(0.5f)]
        public void AbsoluteConstantsWouldFailScaledAssertion(float scale)
        {
            // (0.40 어깨폭 / 0.54 링반지름 / 0.92 과자x / 1.02 과자·종이y / 0.30 과자반지름 / 0.24 종이반폭)
            float[] legacy = { 0.40f, 0.048f, 0.54f, 0.08f, 0.05f, 0.92f, 1.02f, 0.30f, 0.052f, 0.66f, 0.24f, 0.045f };
            for (int i = 0; i < legacy.Length; i++)
            {
                Assert.Greater(Mathf.Abs(legacy[i] - legacy[i] * scale), Tol,
                    $"배율 {scale:F2}: 종전 절대 상수 {legacy[i]}를 그대로 남겨도 기대값 {(legacy[i] * scale):F4}와 " +
                    $"허용오차({Tol}) 안에서 구별되지 않습니다 — (A) 단언이 이 값에 대해서는 아무것도 걸러내지 못합니다.");
            }
        }

        /// <summary>
        /// 종전 <b>절대 상수</b>를 배율 0.5에서 되살렸을 때 (B)의 <b>절대 조건</b>까지 깨진다는 것을 확인한다.
        /// ★ 배율 0.5 전용인 이유: 0.75에서는 종전 절대값이 틀리긴 해도 아직 몸 실루엣 <b>안</b>에 들어와
        ///   있어(정수리 1.7060 &gt; 과자 윗변 1.32) 몸 포함 조건만으로는 구별되지 않는다. 그 구간을 잡는 것은
        ///   위 (A)이고, 여기서는 "가장 심하게 어긋나는 배율에서 (B)도 실제로 빨개진다"를 못박는다.
        /// </summary>
        [Test]
        public void AbsoluteConstantsWouldFailBodyContainmentAtHalfScale()
        {
            StickmanMetrics m = MetricsOf(Rig(0.5f));
            float headTop = m.HeadTopLocalY;        // 1.1373
            float hip = m.HipLocalY;                // 0.4673
            float headCenter = m.HeadCenterLocalY;  // 1.0273

            Assert.IsTrue(1.02f + 0.30f > headTop,
                $"[가출] 종전 절대 상수(y+1.02, 반지름 0.30)의 과자 윗변 1.32가 배율 0.5 정수리 {headTop:F4}를 " +
                "넘지 않습니다 — 절대 조건이 헐거워 아무것도 걸러내지 못합니다.");
            Assert.IsTrue(1.02f + 0.30f > headTop,
                $"[투두] 종전 절대 상수(y+1.02, 반높이 0.30)의 종이 윗변 1.32가 배율 0.5 정수리 {headTop:F4}를 넘지 않습니다.");
            Assert.IsTrue(0.08f + 0.54f >= hip,
                $"[포모도로] 종전 절대 상수(중심 y+0.08, 반지름 0.54)의 링 윗변 0.62가 배율 0.5 고관절 {hip:F4}을 " +
                "넘지 않습니다 — '발밑 위젯인가'를 보는 조건이 헐겁습니다.");
            Assert.IsTrue(1.72f > headTop,
                $"[포모도로] 종전 절대 곁눈질 높이 1.72가 배율 0.5 정수리 {headTop:F4} 아래에 있습니다.");
            Assert.IsTrue(1.33f > headCenter || 1.33f < hip,
                $"[스트레스] 종전 절대 어깨 높이 1.33이 배율 0.5의 고관절({hip:F4})~머리중심({headCenter:F4}) 사이에 " +
                "들어옵니다 — 어깨 조건이 헐겁습니다.");
        }

        /// <summary>"비율화 이전의 검증된 절대 상수 x 배율"이 그대로 나오는지 — 바깥에서 온 숫자와 맞대는
        /// 절대 단언이다(자기 자신을 기준으로 하는 비율 비교가 아니다).</summary>
        private static void AssertScaled(float legacyConstantAtScale1, float scale, float actual,
            string label, string what)
        {
            Assert.AreEqual(legacyConstantAtScale1 * scale, actual, Tol,
                $"{label}: {what}이 {actual:F4}입니다 — 종전 검증값 {legacyConstantAtScale1} x 배율 {scale:F2} = " +
                $"{(legacyConstantAtScale1 * scale):F4}가 나와야 합니다. 절대 월드유닛 상수가 남아 있거나 " +
                "비율 분자/분모가 잘못됐습니다.");
        }
    }
}
