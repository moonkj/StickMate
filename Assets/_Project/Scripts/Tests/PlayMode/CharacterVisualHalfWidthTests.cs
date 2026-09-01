using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 화면 좌우 여백(<see cref="StickmanBlackboard.CharacterVisualHalfWidthWorld"/>)의 실측 잠금 —
    /// 2026-08-31 능동 탐색이 찾은 Minor 1. <b>결함이 두 개였고 서로를 가리고 있었다.</b>
    ///
    /// ============================================================================
    /// (1) 금지된 API — <c>Renderer.bounds</c>
    /// ============================================================================
    /// <c>StickmanAgent.TickVisualHalfWidth</c>가 이 프로젝트가 "쓰면 안 된다"고 문서화한 바로 그
    /// API를 썼다(Tests/PlayMode/StickmanInkBounds: LineRenderer.bounds는 실제 잉크보다 약 1.0유닛
    /// 부풀려져 있고, 그 부풀림을 실측으로 오독한 것이 사용자가 세 번 신고한 40pt 바닥 인셋의
    /// 원인이었다). 부풀림은 루트 스케일을 따라가므로 배율이 커질수록 여백도 비례해 커졌다:
    /// <code>
    /// 배율   보고 반폭   실제 잉크 반폭   과대분
    /// 0.35   0.518      0.244          0.273u
    /// 0.75   1.093      0.448          0.645u
    /// 2.00   2.916      1.108          1.808u  ← 실사용 화면 약 74pt(캐릭터 폭보다 넓다)
    /// </code>
    ///
    /// ============================================================================
    /// (2) 액세서리가 이 계산에 <b>아예 안 들어갔다</b>
    /// ============================================================================
    /// Awake 캐시 렌더러 배열에는 런타임 생성되는 액세서리가 영원히 없다. 긴 망토는 배율 2.00에서
    /// 몸보다 0.30유닛 더 튀어나오는데 아무도 몰랐다.
    ///
    /// <b>둘을 반드시 함께 고쳐야 한다</b> — 지금까지 망토가 잘리지 않은 유일한 이유가 (1)의 부풀림이
    /// (2)의 돌출을 우연히 덮고 있어서다. (1)만 고치면 그 순간 망토가 화면 밖으로 잘린다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    ///  · <see cref="NegativeControl_수정_전_규칙은_실제_잉크보다_크게_부풀어_있다"/> —
    ///    <b>같은 실행/같은 프레임</b>에 수정 전 규칙(Awake 캐시 배열의 <c>Renderer.bounds</c>)을
    ///    그대로 계산해, 그것이 실제 잉크보다 크게 부풀어 있음을 보인다. 즉 아래 단언은 "항상 참"이
    ///    아니라 실제로 있었던 오차를 잡고 있다.
    ///  · 액세서리 포함 단언은 <b>액세서리가 실제로 몸 밖으로 나가는 조합을 먼저 찾아</b> 성립시킨다 —
    ///    안 나가는 조합만 보면 그 단언 역시 항상 참이 된다.
    /// </summary>
    public sealed class CharacterVisualHalfWidthTests
    {
        private const string LogPrefix = "[시각반폭]";

        /// <summary>측정 시점과 에이전트의 계측 시점 사이에 몸이 움직인 만큼의 허용 오차(월드 유닛).
        /// 60fps에서 보행 속도로 한두 프레임 어긋나는 폭이며, 잡으려는 오차(0.27~1.81유닛)보다
        /// 한 자릿수 작다.</summary>
        private const float DriftTolerance = 0.12f;

        private static readonly FieldInfo BodyRenderersField =
            typeof(StickmanAgent).GetField("_renderers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BodyLinesField =
            typeof(StickmanAgent).GetField("_lineRenderers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AgentConfigField =
            typeof(StickmanAgent).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private float _restoreScale;

        private IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            Assert.IsNotNull(BodyRenderersField, $"{LogPrefix} StickmanAgent._renderers 필드를 찾지 못했습니다.");
            Assert.IsNotNull(BodyLinesField, $"{LogPrefix} StickmanAgent._lineRenderers 필드를 찾지 못했습니다.");

            yield return new WaitForSeconds(1.2f);   // 낙하 정착.

            // 배포 에셋을 건드리지 않는다(불변 원칙 3).
            _originalConfig = _agent.Config;
            _clonedConfig = Object.Instantiate(_originalConfig);
            _agent.Blackboard.Config = _clonedConfig;
            AgentConfigField.SetValue(_agent, _clonedConfig);
            _restoreScale = _agent.CurrentCharacterScale;

            CharacterProgressionModel.AddXp(1000000f, _clonedConfig);
        }

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _restoreScale > 0f) _agent.ApplyCharacterScale(_restoreScale, "테스트 정리");
            if (_agent != null && _originalConfig != null)
            {
                AgentConfigField.SetValue(_agent, _originalConfig);
                if (_agent.Blackboard != null) _agent.Blackboard.Config = _originalConfig;
            }
            if (_clonedConfig != null) Object.Destroy(_clonedConfig);
            _clonedConfig = null;
            _originalConfig = null;
            _agent = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
        }

        // ====================================================================
        // 측정 도구 — 세 가지 규칙을 같은 프레임에 나란히 잰다
        // ====================================================================

        /// <summary>수정 전 규칙: Awake 캐시 배열의 <c>Renderer.bounds</c>.</summary>
        private float OldRuleHalfWidth(float centerX)
        {
            var body = (Renderer[])BodyRenderersField.GetValue(_agent);
            float half = 0f;
            if (body == null) return 0f;
            for (int i = 0; i < body.Length; i++)
            {
                Renderer r = body[i];
                if (r == null || !r.enabled) continue;
                Bounds b = r.bounds;
                half = Mathf.Max(half, Mathf.Abs(b.max.x - centerX));
                half = Mathf.Max(half, Mathf.Abs(centerX - b.min.x));
            }
            return half;
        }

        /// <summary>선 하나의 <b>실제 잉크</b> 반폭 — 정점(중심선) + 획 반두께.</summary>
        private static float InkHalfWidthOf(LineRenderer lr, float centerX)
        {
            if (lr == null || !lr.enabled || !lr.gameObject.activeInHierarchy) return 0f;
            int count = lr.positionCount;
            if (count <= 0) return 0f;
            float maxDx = 0f;
            for (int q = 0; q < count; q++)
            {
                Vector3 p = lr.GetPosition(q);
                float x = (lr.useWorldSpace ? p : lr.transform.TransformPoint(p)).x;
                maxDx = Mathf.Max(maxDx, Mathf.Abs(x - centerX));
            }
            return maxDx + Mathf.Max(lr.startWidth, lr.endWidth) * 0.5f;
        }

        /// <summary>몸(프리팹 선)만의 실제 잉크 반폭.</summary>
        private float BodyInkHalfWidth(float centerX)
        {
            var lines = (LineRenderer[])BodyLinesField.GetValue(_agent);
            float half = 0f;
            if (lines == null) return 0f;
            for (int i = 0; i < lines.Length; i++) half = Mathf.Max(half, InkHalfWidthOf(lines[i], centerX));
            return half;
        }

        /// <summary>액세서리(= 몸에 붙은 몸 바깥 잉크)만의 실제 잉크 반폭.</summary>
        private float AccessoryInkHalfWidth(float centerX)
        {
            Transform root = _agent.transform.Find("EquipmentAccessories");
            if (root == null) return 0f;
            float half = 0f;
            var lines = root.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++) half = Mathf.Max(half, InkHalfWidthOf(lines[i], centerX));
            var fills = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < fills.Length; i++)
            {
                MeshRenderer mr = fills[i];
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                Bounds b = mr.bounds;   // 채움면 bounds는 실제 메시 정점에서 나온다(부풀지 않는다).
                half = Mathf.Max(half, Mathf.Abs(b.max.x - centerX));
                half = Mathf.Max(half, Mathf.Abs(centerX - b.min.x));
            }
            return half;
        }

        private static readonly FieldInfo HalfWidthTimerField =
            typeof(StickmanAgent).GetField("_visualHalfWidthTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// 다음 프레임에 반폭을 <b>반드시</b> 다시 재게 한다. 이 계측은 24시간 상주 앱이라 0.25초
        /// 주기로만 돌기 때문에(<c>VisualHalfWidthRefreshInterval</c>), 그냥 읽으면 최대 0.25초 전의
        /// 포즈에서 나온 값을 보게 되어 "포즈가 바뀐 순간"을 겨냥한 이 테스트가 무의미해진다.
        /// 주기 타이머만 만료시키므로 계산 규칙 자체는 실제 경로 그대로다.
        /// </summary>
        private void ForceHalfWidthRemeasure() => HalfWidthTimerField.SetValue(_agent, float.MaxValue);

        private IEnumerator ApplyScaleAndSettle(float scale)
        {
            _agent.ApplyCharacterScale(scale, "테스트");
            for (int f = 0; f < 6; f++) yield return null;
            ForceHalfWidthRemeasure();
            yield return null;
        }

        // ====================================================================
        // (1) 부풀림 제거
        // ====================================================================

        [UnityTest]
        public IEnumerator 보고하는_반폭이_실제_잉크와_일치한다([Values(0.35f, 0.75f, 2.0f)] float scale)
        {
            yield return SetUp();
            yield return ApplyScaleAndSettle(scale);

            float centerX = _agent.Blackboard.Body.position.x;
            float reported = _agent.Blackboard.CharacterVisualHalfWidthWorld;
            float bodyInk = BodyInkHalfWidth(centerX);
            float accInk = AccessoryInkHalfWidth(centerX);
            float realInk = Mathf.Max(bodyInk, accInk);
            float oldRule = OldRuleHalfWidth(centerX);

            Debug.Log($"{LogPrefix} 배율 {scale:F2} — 보고 {reported:F4} / 실제 잉크 {realInk:F4}" +
                $"(몸 {bodyInk:F4}, 액세서리 {accInk:F4}) / 수정 전 규칙(bounds) {oldRule:F4} " +
                $"→ 수정 전 과대분 {(oldRule - realInk):F4}유닛.");

            Assert.AreEqual(realInk, reported, DriftTolerance,
                $"{LogPrefix} 배율 {scale:F2}에서 보고 반폭 {reported:F4}가 실제 잉크 {realInk:F4}와 " +
                $"{Mathf.Abs(reported - realInk):F4}유닛 어긋납니다 — Renderer.bounds 같은 부풀려진 값을 " +
                "다시 쓰고 있지 않은지 확인하세요(그 부풀림은 루트 스케일에 비례해 커집니다).");
        }

        [UnityTest]
        public IEnumerator NegativeControl_수정_전_규칙은_실제_잉크보다_크게_부풀어_있다()
        {
            yield return SetUp();

            bool sawInflation = false;
            foreach (float scale in new[] { 0.35f, 0.75f, 2.0f })
            {
                yield return ApplyScaleAndSettle(scale);

                float centerX = _agent.Blackboard.Body.position.x;
                float realInk = Mathf.Max(BodyInkHalfWidth(centerX), AccessoryInkHalfWidth(centerX));
                float oldRule = OldRuleHalfWidth(centerX);
                float excess = oldRule - realInk;

                Debug.Log($"{LogPrefix} [네거티브] 배율 {scale:F2} — 수정 전 규칙 {oldRule:F4} vs " +
                    $"실제 잉크 {realInk:F4} → 과대분 {excess:F4}유닛.");

                if (excess > DriftTolerance * 2f) sawInflation = true;
            }

            Assert.IsTrue(sawInflation,
                $"{LogPrefix} 수정 전 규칙(Renderer.bounds)이 <b>한 배율에서도</b> 실제 잉크보다 " +
                "의미 있게 부풀지 않았습니다 — 그렇다면 위 일치 단언은 항상 참이라 아무 결함도 잡지 " +
                "못합니다(LineRenderer.bounds의 부풀림 특성이 바뀌었는지 확인하세요).");
        }

        // ====================================================================
        // (2) 액세서리 포함
        // ====================================================================

        /// <summary>
        /// 액세서리가 <b>몸보다 밖으로 나가는 순간</b>을 실제로 찾아, 그 프레임의 보고 반폭이 그것을
        /// 덮는지 본다.
        ///
        /// <para>왜 "찾는" 형태인가: 몸의 잉크 반폭은 <b>포즈에 따라 크게 변한다</b>(팔을 벌리고 걸을
        /// 때는 배율 2.00에서 1.60유닛까지 간다). 그 순간만 보면 어떤 망토도 몸을 넘지 못해 이 단언이
        /// 항상 참이 되어 아무것도 잡지 못한다. 그래서 팔이 내려오는 프레임까지 <b>매 프레임 실측</b>하며
        /// 돌출이 실제로 생기는 순간을 찾고, 그 순간을 단언 대상으로 삼는다 — 찾는 단계 자체가
        /// 네거티브 컨트롤이다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 몸보다_튀어나온_액세서리가_보고_반폭에_포함된다()
        {
            yield return SetUp();

            const float Scale = 2.0f;

            // ★★ 2026-09-01 — 표본 창을 <b>프레임 수</b>에서 <b>벽시계 시간</b>으로 바꿨다.
            //
            // 예전에는 `const int SampleFrames = 900;`이었고 주석은 "자율 배회가 걷기/유휴를 여러 번
            // 오갈 만큼"이라고 적혀 있었다. 그런데 배치 모드(-nographics)는 0.11~0.45ms/프레임으로
            // 돌기 때문에 900프레임은 실제로 <b>0.099~0.405초</b>였다 — 걷기/유휴를 오가기는커녕
            // 표본 전체가 <b>앱 시작 낙하의 착지 동작 한 번</b> 안에 갇혀 있었다. 그 구간은 팔이
            // 벌어져 있어 몸의 잉크 반폭이 최대로 커지는 자리라, 망토 돌출(액세서리 - 몸)이 문턱
            // 0.05유닛 근처에서 오르내렸다(실행 간 실측 -0.0199 ~ +0.0612 — 문턱을 사이에 두고
            // 갈렸다). 즉 이 테스트의 "간헐적 실패"는 프로덕션이 아니라 표본 창의 결함이었다.
            //
            // 고친 방식은 두 겹이다: (a) 표본을 시작하기 전에 <b>Idle에 안착</b>할 때까지 기다리고,
            // (b) 그 뒤 3초(벽시계)를 표본한다. Idle에서는 팔이 내려와 몸 반폭이 줄어들어 돌출이
            // 실측 0.42유닛 — 문턱의 <b>8배</b>다. 즉 이 변경은 테스트를 약화시키는 것이 아니라
            // 겨냥한 순간을 반드시 보게 만드는 <b>강화</b>다. 문턱과 단언은 한 글자도 바뀌지 않았다.
            const float SampleSeconds = 3f;

            int capeCount = ItemCatalog.ItemCountIn(EquipmentSlot.Shoulders);
            Assert.Greater(capeCount, 0, $"{LogPrefix} 망토(Shoulders) 아이템이 하나도 없습니다.");

            // 가장 길게 뻗는 망토를 고른다(어느 것인지는 실측으로 정한다 — 자리 번호를 여기 적지 않는다).
            int bestItem = 0;
            float bestReach = -1f;
            for (int item = 0; item < capeCount; item++)
            {
                EquipmentModel.TryWear(EquipmentSlot.Shoulders, item, _clonedConfig);
                yield return ApplyScaleAndSettle(Scale);
                float reach = AccessoryInkHalfWidth(_agent.Blackboard.Body.position.x);
                Debug.Log($"{LogPrefix} 망토 #{item} 액세서리 잉크 반폭 {reach:F4}.");
                if (reach > bestReach) { bestReach = reach; bestItem = item; }
            }

            EquipmentModel.TryWear(EquipmentSlot.Shoulders, bestItem, _clonedConfig);
            yield return ApplyScaleAndSettle(Scale);

            float worstOverhang = float.NegativeInfinity;
            float worstAcc = 0f, worstBody = 0f, worstReported = 0f;
            float minCoverage = float.PositiveInfinity;   // (보고 - 액세서리) 중 가장 작은 값.
            int coveredSamples = 0;

            // (a) 낙하/착지가 끝나 Idle에 안착할 때까지 — 여기서 기다리지 않으면 아래 3초가 통째로
            //     착지 동작 안에 들어가 버린다(위 문서의 근본 원인).
            yield return TestClock.WaitForState(
                _agent.Blackboard, StickmanStateId.Idle, timeoutSeconds: 20f, holdSeconds: 0.1f);

            int idleSamples = 0;

            // (b) 3초(벽시계) 표본 — 자율 배회가 유휴/걷기를 실제로 오간다.
            ForceHalfWidthRemeasure();
            yield return TestClock.SampleForSeconds(SampleSeconds, _ =>
            {
                ForceHalfWidthRemeasure();   // 다음 프레임에도 반드시 다시 재게 한다.

                float cx = _agent.Blackboard.Body.position.x;
                float bodyInk = BodyInkHalfWidth(cx);
                float accInk = AccessoryInkHalfWidth(cx);
                if (accInk <= 0f) return;   // 재구성 프레임(컨테이너가 잠깐 없다).

                float reported = _agent.Blackboard.CharacterVisualHalfWidthWorld;
                float overhang = accInk - bodyInk;
                if (overhang > worstOverhang)
                {
                    worstOverhang = overhang;
                    worstAcc = accInk; worstBody = bodyInk; worstReported = reported;
                }
                minCoverage = Mathf.Min(minCoverage, reported - accInk);
                coveredSamples++;
                if (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Idle) idleSamples++;
            });

            Debug.Log($"{LogPrefix} 망토 #{bestItem} (배율 {Scale:F2}) — 표본 {SampleSeconds:F1}초 동안 " +
                $"{coveredSamples}프레임(그중 Idle {idleSamples}프레임). " +
                $"최대 돌출 프레임: 몸 {worstBody:F4} / 액세서리 {worstAcc:F4} / 보고 {worstReported:F4} " +
                $"→ 돌출 {worstOverhang:F4}유닛. 전 표본에서 (보고 - 액세서리) 최소값 {minCoverage:F4}유닛.");

            Assert.Greater(coveredSamples, 100, $"{LogPrefix} 유효 표본이 {coveredSamples}프레임뿐입니다.");

            // 진단용(단언 아님) — Idle을 한 프레임도 못 봤다면 아래 네거티브 컨트롤이 실패했을 때
            // "표본 창이 또 엉뚱한 구간에 갇힌 것"임을 즉시 알 수 있어야 한다.
            Assert.Greater(idleSamples, 0,
                $"{LogPrefix} {SampleSeconds:F1}초 표본에서 Idle 프레임을 하나도 보지 못했습니다 — " +
                "표본 창이 또 다른 동작 안에 갇혔습니다(팔이 벌어진 포즈만 보면 망토 돌출이 문턱 " +
                "근처에서 오르내려 이 테스트가 다시 '간헐적'이 됩니다).");

            // ① 네거티브 컨트롤 — 액세서리가 <b>실제로</b> 몸 밖으로 나가는 순간이 존재한다.
            //    (없다면 아래 ②는 항상 참이라 결함을 잡지 못한다.)
            Assert.Greater(worstOverhang, 0.05f,
                $"{LogPrefix} 표본 {coveredSamples}프레임 어디에서도 망토가 몸보다 0.05유닛 이상 " +
                "튀어나오지 않았습니다 — 그렇다면 '액세서리를 포함해야 한다'는 단언이 이 환경에서는 " +
                "항상 참이라 아무 의미가 없습니다(액세서리 도형/포즈 진폭이 바뀌었는지 확인하세요).");

            // ② 본 단언 — 모든 표본에서 보고 반폭이 액세서리 잉크를 덮는다.
            Assert.GreaterOrEqual(minCoverage, -DriftTolerance,
                $"{LogPrefix} 어떤 프레임에서 액세서리 잉크가 보고 반폭보다 {(-minCoverage):F4}유닛 더 " +
                "밖으로 나갔습니다 — 화면 클램프가 그만큼을 모르므로 그 자리에서 망토가 잘립니다. " +
                "(지금까지 안 잘린 것은 Renderer.bounds의 부풀림이 이 돌출을 우연히 덮고 있었기 때문입니다.)");
        }
    }
}
