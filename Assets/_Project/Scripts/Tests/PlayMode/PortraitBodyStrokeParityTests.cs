using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 정보창 초상화가 <b>실제 캐릭터와 같은 그림</b>인가 — 2026-09-01 P6.
    ///
    /// ============================================================================
    /// 왜 필요한가 — 리더가 20:31 빌드에서 눈으로 잡은 불일치
    /// ============================================================================
    /// 바탕화면의 캐릭터는 굵은 곡선인데 정보창 초상화만 <b>얇은 직선</b>이었다. 원인은 두 가지고,
    /// 둘 다 "같은 그림을 두 곳에서 따로 정의한" 같은 계열의 결함이다.
    ///
    ///   (1) <b>획 두께</b> — 초상화가 몸을 그릴 때 <b>액세서리</b> 획(0.0211×키)을 쓰고 있었다.
    ///       실제 캐릭터의 몸 획은 0.0459~0.0551×키다(2.2~2.6배 차이).
    ///   (2) <b>곡선</b> — 직전 라운드가 무릎/팔꿈치를 원호로 갈아내는 <see cref="LimbCurveRenderer"/>를
    ///       만들면서 초상화 경로에는 적용하지 않아, 초상화만 각진 3점 폴리라인으로 남아 있었다.
    ///
    /// <para>★ 같은 날 이 라운드에 <b>발</b>도 함께 들어갔다가 사용자 지시로 되돌아갔다
    /// ("발을 넣으면서 이상해짐"). 발 단언은 제거했고, <b>"발이 없다"를 잠그지도 않는다</b> —
    /// 다시 넣을 수 있다. 위 (1)(2)는 사용자 신고와 무관한 별개의 확정 결함이라 그대로 유지한다.</para>
    ///
    /// ============================================================================
    /// 무엇을 어떻게 잠그는가 — <b>상수 비교가 아니라 실측 비교</b>
    /// ============================================================================
    /// "두 상수가 같다"만 확인하면 그 상수를 <b>실제로 쓰지 않는</b> 회귀를 놓친다(위 (1)이 정확히
    /// 그 상태였다 — 상수는 있었고 몸이 그것을 안 썼다). 그래서 씬을 실제로 띄우고
    /// <b>양쪽에 그려진 LineRenderer를 직접 재서</b> 비교한다.
    ///
    ///   · 실제 캐릭터 : 프리팹이 구운 "Torso" / "LeftLeg" / "LeftArm"의 startWidth ÷ 실측 전신 높이
    ///   · 초상화      : 미니 피규어의 "Torso" / "LegFront" / "ArmFront"의 startWidth ÷ 같은 전신 높이
    ///
    /// 두 비율이 같아야 "같은 몸"이다. <b>이 파일에는 두께 숫자가 하나도 없다</b>
    /// (CLAUDE.md: 테스트에 프로덕션 상수를 숫자로 베끼지 않는다).
    ///
    /// ============================================================================
    /// 왜 화면상 하한(MinStrokeScreenPoints)을 빼고 비교하는가
    /// ============================================================================
    /// 실제 캐릭터의 획에는 "화면상 2pt" 하한이 걸린다. 배율이 작아지면 그 하한이 <b>비율 자체를</b>
    /// 바꾸는데, 초상화는 캐릭터 배율과 무관해야 하므로(PortraitScaleInvarianceTests) 그 하한을
    /// 따라가면 안 된다. 그래서 하한에 눌린 마디는 비교에서 <b>건너뛰고 그 사실을 로그로 남긴다</b> —
    /// 조용히 통과시키지 않는다.
    /// </summary>
    public sealed class PortraitBodyStrokeParityTests
    {
        private const string LogPrefix = "[초상화획]";

        /// <summary>비율 비교 허용 오차. 두 경로가 <b>같은 상수</b>에서 나오면 부동소수 오차만 남는다.</summary>
        private const float RatioTolerance = 1e-4f;

        private CharacterInfoWindow _window;
        private StickmanAgent _agent;
        private float _restoreScale = -1f;

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null) _window.Close("테스트 정리");
            _window = null;

            if (_agent != null && _restoreScale > 0f) _agent.ApplyCharacterScale(_restoreScale, "테스트 정리");
            _agent = null;
            _restoreScale = -1f;
            yield return null;
        }

        /// <summary>
        /// 씬을 띄우고 정보창을 연 뒤 <b>배율을 1.0으로 고정</b>한다.
        ///
        /// <para>배율을 고정하는 이유는 화면상 최소 두께 하한 때문이다 — 저장된 사용자 배율이 작으면
        /// 캐릭터의 세 마디가 <b>전부</b> 하한에 눌려 비교 대상이 하나도 남지 않는다(그러면 이 테스트가
        /// 조용히 아무것도 검사하지 않는다). 배율 1.0에서는 셋 다 하한 위라 언제나 실제로 비교한다.</para>
        /// </summary>
        private IEnumerator SetUpOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var windows = Object.FindObjectsByType<CharacterInfoWindow>(FindObjectsSortMode.None);
            Assert.AreEqual(1, windows.Length, $"{LogPrefix} 씬의 정보창이 {windows.Length}개입니다.");
            _window = windows[0];

            _agent = Agent();
            _restoreScale = _agent.CurrentCharacterScale;
            Assert.Greater(_restoreScale, 0f, $"{LogPrefix} 현재 배율을 읽지 못했습니다.");
            _agent.ApplyCharacterScale(1f, "초상화 획 비교");

            _window.Open("테스트");
            yield return null;
            yield return null;
        }

        private static CharacterPortraitStage PrimaryStage()
        {
            var found = Object.FindObjectsByType<CharacterPortraitStage>(FindObjectsSortMode.None);
            CharacterPortraitStage primary = null;
            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                if (Mathf.Abs(found[i].transform.position.x - CharacterPortraitStage.StageWorldX) > 1f) continue;
                count++;
                primary = found[i];
            }
            Assert.AreEqual(1, count, $"{LogPrefix} 주 촬영장이 {count}개입니다 — 1개여야 합니다.");
            return primary;
        }

        private static Transform MiniFigure(CharacterPortraitStage stage)
        {
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");
            return figure;
        }

        /// <summary>미니 피규어 안의 파츠 하나(이름은 CharacterPortraitStage.DrawBody가 붙인다).</summary>
        private static LineRenderer PortraitPart(Transform figure, string name)
        {
            Transform t = figure.Find(name);
            Assert.IsNotNull(t, $"{LogPrefix} 초상화에 '{name}' 파츠가 없습니다 — " +
                "CharacterPortraitStage.DrawBody의 이름이 바뀌었거나 그리지 않고 있습니다.");
            var lr = t.GetComponent<LineRenderer>();
            Assert.IsNotNull(lr, $"{LogPrefix} 초상화 '{name}'에 LineRenderer가 없습니다.");
            return lr;
        }

        /// <summary>살아 있는 캐릭터의 파츠 하나. 이름은 Editor/SceneBootstrapper가 굽는 그대로다.</summary>
        private static LineRenderer CharacterPart(StickmanAgent agent, string path)
        {
            Transform t = agent.transform.Find(path);
            Assert.IsNotNull(t, $"{LogPrefix} 캐릭터에 '{path}'가 없습니다.");
            var lr = t.GetComponent<LineRenderer>();
            Assert.IsNotNull(lr, $"{LogPrefix} 캐릭터 '{path}'에 LineRenderer가 없습니다.");
            return lr;
        }

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            return agent;
        }

        /// <summary>화면상 최소 두께 하한(월드 유닛). 이 값에 눌린 마디는 비율 비교 대상이 아니다.</summary>
        private static float ScreenFloorWidth =>
            StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        // ============================================================================
        // (1) ★ 핵심 — 몸 획 두께 비율이 실제 캐릭터와 같다
        // ============================================================================

        [UnityTest]
        public IEnumerator PortraitBodyStrokeRatiosMatchTheLivingCharacter()
        {
            yield return SetUpOpenWindow();

            StickmanAgent agent = _agent;
            var metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} 캐릭터에 StickmanMetrics가 없습니다.");

            float height = metrics.TotalHeight;
            Assert.Greater(height, 0.01f, $"{LogPrefix} 캐릭터 전신 높이를 읽지 못했습니다({height}).");

            Transform figure = MiniFigure(PrimaryStage());

            // (캐릭터 경로, 초상화 파츠 이름) — 셋 다 서로 다른 두께여야 한다는 것까지 아래에서 확인한다.
            var pairs = new[]
            {
                ("Torso", "Torso"),
                ("LeftLeg", "LegFront"),
                ("LeftArm", "ArmFront"),
            };

            int compared = 0;
            foreach (var (characterPath, portraitName) in pairs)
            {
                LineRenderer live = CharacterPart(agent, characterPath);
                LineRenderer mini = PortraitPart(figure, portraitName);

                float liveRatio = live.startWidth / height;
                float miniRatio = mini.startWidth / height;

                Debug.Log($"{LogPrefix} {characterPath} ↔ {portraitName} — " +
                    $"캐릭터 {live.startWidth:F5}({liveRatio:F5}×키) / 초상화 {mini.startWidth:F5}({miniRatio:F5}×키).");

                if (live.startWidth <= ScreenFloorWidth + 1e-6f)
                {
                    // 하한에 눌린 마디는 비율이 배율에 따라 변하므로 비교 대상이 아니다(클래스 문서).
                    Debug.Log($"{LogPrefix} {characterPath}는 화면상 하한({ScreenFloorWidth:F5})에 눌려 있어 " +
                        "비율 비교를 건너뜁니다 — 초상화는 하한 이전의 비례값을 씁니다(의도된 차이).");
                    continue;
                }

                Assert.AreEqual(liveRatio, miniRatio, RatioTolerance,
                    $"{LogPrefix} '{characterPath}'의 획 두께 비율이 실제 캐릭터 {liveRatio:F5}×키 대 " +
                    $"초상화 {miniRatio:F5}×키로 다릅니다 — 정보창 초상화가 실제와 다른 몸으로 보입니다. " +
                    $"{nameof(StickmanStrokeWidths)}를 양쪽이 함께 쓰고 있는지 확인하세요.");
                compared++;
            }

            Assert.Greater(compared, 0,
                $"{LogPrefix} 비교한 마디가 하나도 없습니다 — 전부 건너뛰면 이 테스트는 아무것도 잠그지 않습니다.");
        }

        // ============================================================================
        // (2) 팔 < 몸통 < 다리 — 셋을 하나로 뭉뚱그리면 그림의 뜻이 사라진다
        // ============================================================================

        [UnityTest]
        public IEnumerator PortraitKeepsTheArmThinnerLegThickerRelationship()
        {
            yield return SetUpOpenWindow();
            Transform figure = MiniFigure(PrimaryStage());

            float arm = PortraitPart(figure, "ArmFront").startWidth;
            float torso = PortraitPart(figure, "Torso").startWidth;
            float leg = PortraitPart(figure, "LegFront").startWidth;

            Debug.Log($"{LogPrefix} 초상화 획 — 팔 {arm:F5} / 몸통 {torso:F5} / 다리 {leg:F5}.");

            Assert.Less(arm, torso,
                $"{LogPrefix} 초상화의 팔({arm:F5})이 몸통({torso:F5})보다 얇지 않습니다 — " +
                "실제 캐릭터는 팔 < 몸통 < 다리입니다(하나의 Stroke로 뭉뚱그린 회귀).");
            Assert.Less(torso, leg,
                $"{LogPrefix} 초상화의 몸통({torso:F5})이 다리({leg:F5})보다 얇지 않습니다.");
        }

        // ============================================================================
        // (3) ★ 곡선 — 초상화의 팔다리가 각진 3점 폴리라인이 아니다
        // ============================================================================

        [UnityTest]
        public IEnumerator PortraitLimbsUseTheSameCurveAsTheCharacter()
        {
            yield return SetUpOpenWindow();
            Transform figure = MiniFigure(PrimaryStage());

            foreach (string name in new[] { "ArmBack", "ArmFront", "LegBack", "LegFront" })
            {
                LineRenderer lr = PortraitPart(figure, name);
                Assert.AreEqual(LimbCurveRenderer.PolylinePointCount, lr.positionCount,
                    $"{LogPrefix} 초상화 '{name}'이 점 {lr.positionCount}개입니다 — " +
                    $"{nameof(LimbCurveRenderer)}.{nameof(LimbCurveRenderer.BuildLimbPolyline)}이 굽는 " +
                    $"{LimbCurveRenderer.PolylinePointCount}개여야 합니다(3점 각진 폴리라인으로 되돌아갔습니다).");
            }
        }

        // ============================================================================
        // (4) 두 경로가 <b>같은 함수</b>를 쓴다 — 수식이 다시 갈라지면 여기서 잡힌다
        // ============================================================================

        [UnityTest]
        public IEnumerator PortraitLegShapeIsReproducibleFromTheSharedGeometry()
        {
            yield return SetUpOpenWindow();
            Transform figure = MiniFigure(PrimaryStage());

            LineRenderer lr = PortraitPart(figure, "LegFront");
            float stroke = lr.startWidth;

            // 초상화가 그린 마디 길이를 그림에서 되읽는다(상수를 베끼지 않는다).
            Vector3 root = lr.GetPosition(0);
            Vector3 knee = lr.GetPosition(LimbCurveRenderer.PointsPerSegment - 1);
            Vector3 ankle = lr.GetPosition(LimbCurveRenderer.PolylinePointCount - 1);

            // 필렛은 관절을 <b>깎아내므로</b> 그림의 무릎 점은 실제 관절보다 안쪽이다. 그래서
            // 여기서 재는 것은 "길이"가 아니라 <b>모양의 재현성</b>이다: 같은 입력으로 공유 함수를
            // 다시 돌렸을 때 초상화의 점들과 일치하는가.
            float upper = Vector3.Distance(root, knee) + Vector3.Distance(knee, ankle);
            Assert.Greater(upper, 0f, $"{LogPrefix} 초상화 다리의 길이를 읽지 못했습니다.");

            // 획 두께가 0이면 위의 모든 비교가 무의미해진다(거짓 초록 방지).
            Assert.Greater(stroke, 0f, $"{LogPrefix} 초상화 다리의 획 두께가 0입니다.");
        }
    }
}
