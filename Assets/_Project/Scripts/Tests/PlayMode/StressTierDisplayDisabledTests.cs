using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ "스트레스 단계 상시 표시가 기본 OFF인가" 회귀 잠금 — 사용자 신고
    /// <b>"몸주위로 이상한 주황색 선들이 생김"</b>(2026-08-29) 대응의 테스트.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 그 주황색 선들의 정체는 Interaction/StressGaugeRenderer가 그리는 <b>어깨 처짐 호 2개 +
    /// 한숨 퍼프 원</b>이다(CautionColor 0.72/0.63/0.36). 이것은 확률 기반 구경거리가 아니라
    /// UX_FLOW.md 19절이 "상시 채널"로 설계한 표시라, 직전 라운드에 자율 연출을 전부 OFF로 내릴 때
    /// <b>이것만 빠졌다</b>. 사용자가 "요청하지 않은 표시가 캐릭터를 둘러싼다"고 신고한 것이 이번이
    /// 두 번째다(직전은 하드웨어 발열 이모트).
    ///
    /// ============================================================================
    /// 절대 조건으로 단언한다 — "게이지를 어디까지 올려도 0개"
    /// ============================================================================
    /// "예전보다 덜 뜬다" 같은 상대적 여유를 쓰지 않는다(이 프로젝트가 그 방식으로 버그를 2라운드
    /// 연속 놓친 전례가 있다). 게이지를 0부터 <b>최대치 1.0까지</b> 훑으며 매번 시각 오브젝트가
    /// 정확히 0개임을 단언한다. 특히 1.0은 중요하다 — 임계값을 caution 하나만 올렸을 때 남는
    /// 구멍(경고=빨강 단계는 stressSulkyThreshold=0.8에서 그대로 살아 있었다)이 정확히 이 지점에서
    /// 드러나기 때문이다(StressGaugeRenderer.TierForLevel의 마스터 스위치 주석 참고).
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <see cref="RestoringOriginalThresholdMakesItVisibleAgain"/>이 임계값을 원래 기본값(0.4)으로
    /// 되돌리면 <b>실제로 다시 뜬다</b>는 것을 같은 방식으로 확인한다. 즉 위 테스트가 통과하는 이유가
    /// "렌더러가 원래 아무것도 못 그려서"가 아님을 같은 파일 안에서 증명한다.
    /// </summary>
    public sealed class StressTierDisplayDisabledTests
    {
        private const string StressContainerName = "StressMoodVisuals";

        /// <summary>기본 OFF가 되기 전의 원래 주의 경계값.</summary>
        private const float OriginalCautionLevel = 0.4f;

        private StressGaugeRenderer _renderer;
        private StickConfig _config;
        private float _savedCautionLevel;
        private bool _needsRestore;

        [TearDown]
        public void RestoreThreshold()
        {
            if (_needsRestore && _config != null) _config.stressTierCautionLevel = _savedCautionLevel;
            _needsRestore = false;
            StressGauge.SetLevel(0f);
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var renderers = Object.FindObjectsByType<StressGaugeRenderer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, renderers.Length,
                $"씬의 StressGaugeRenderer 개수가 {renderers.Length}개입니다 — 1개여야 합니다.");
            _renderer = renderers[0];

            var agents = Object.FindObjectsByType<StickmanAgent>(FindObjectsSortMode.None);
            Assert.AreEqual(1, agents.Length, "씬의 StickmanAgent가 1개가 아닙니다.");
            _config = agents[0].Config;
            Assert.IsNotNull(_config, "StickmanAgent에 StickConfig가 배선돼 있지 않습니다.");

            StressGauge.SetLevel(0f);
            yield return null;
        }

        /// <summary>출하 에셋 자체가 도달 불가능한 값을 들고 있는지 — 코드 기본값만 바꾸고 에셋을
        /// 갱신하지 않으면 에셋이 이겨서 아무것도 안 바뀐다(이 프로젝트에서 실제로 겪은 함정).</summary>
        [UnityTest]
        public IEnumerator ShippedConfigAssetHasUnreachableCautionThreshold()
        {
            yield return LoadSceneAndResolve();

            Assert.Greater(_config.stressTierCautionLevel, 1f,
                $"출하 StickConfig 에셋의 stressTierCautionLevel이 {_config.stressTierCautionLevel:F2}입니다 — " +
                "스트레스 게이지는 0~1로 클램프되므로 이 상시 표시를 끄려면 1보다 커야 합니다. " +
                "C# 기본값만 바꾸고 Assets/_Project/Data/DefaultStickConfig.asset을 갱신하지 않으면 " +
                "에셋에 직렬화된 옛 값이 그대로 이깁니다.");

            Debug.Log($"[스트레스OFF테스트] 출하 에셋 stressTierCautionLevel={_config.stressTierCautionLevel:F2} " +
                "(>1 = 도달 불가능 = 기본 OFF).");
        }

        /// <summary>절대 조건 — 게이지를 0부터 최대(1.0)까지 올려도 시각 오브젝트가 하나도 생기지 않는다.</summary>
        [UnityTest]
        public IEnumerator GaugeAtAnyLevelDrawsNothingWithShippedConfig()
        {
            yield return LoadSceneAndResolve();

            float[] levels = { 0f, 0.25f, 0.41f, 0.6f, 0.79f, 0.81f, 0.95f, 1f };
            for (int i = 0; i < levels.Length; i++)
            {
                StressGauge.SetLevel(levels[i]);
                yield return null;
                yield return null;

                Assert.AreEqual(StressMoodTier.Calm, _renderer.CurrentTier,
                    $"게이지 {levels[i]:F2}에서 단계가 {_renderer.CurrentTier}가 됐습니다 — 기본 설정에서는 " +
                    "어떤 값에서도 Calm이어야 합니다(0.81/1.00은 caution만 올렸을 때 남던 '경고=빨강' 구멍입니다).");
                Assert.IsFalse(_renderer.IsVisible,
                    $"게이지 {levels[i]:F2}에서 기분 표시가 떠 있습니다 — 사용자가 신고한 그 선들입니다.");
                Assert.AreEqual(0, _renderer.ActiveVisualCount,
                    $"게이지 {levels[i]:F2}에서 시각 오브젝트가 {_renderer.ActiveVisualCount}개 생겼습니다.");
                Assert.IsNull(GameObject.Find(StressContainerName),
                    $"게이지 {levels[i]:F2}에서 '{StressContainerName}' GameObject가 씬에 생겼습니다.");
            }

            Debug.Log("[스트레스OFF테스트] 절대 조건 통과 — 게이지 0.00~1.00 8지점 전부 시각 오브젝트 0개.");
        }

        /// <summary>네거티브 컨트롤 — 임계값을 원래 값(0.4)으로 되돌리면 정말로 다시 뜬다.
        /// 위 테스트가 "렌더러가 애초에 아무것도 못 그려서" 통과하는 것이 아님을 증명한다.</summary>
        [UnityTest]
        public IEnumerator RestoringOriginalThresholdMakesItVisibleAgain()
        {
            yield return LoadSceneAndResolve();

            _savedCautionLevel = _config.stressTierCautionLevel;
            _needsRestore = true;
            _config.stressTierCautionLevel = OriginalCautionLevel;

            StressGauge.SetLevel(OriginalCautionLevel + 0.01f);
            yield return null;
            yield return null;

            Assert.AreEqual(StressMoodTier.Caution, _renderer.CurrentTier,
                "임계값을 0.4로 되돌렸는데도 단계가 Caution이 되지 않았습니다 — 그렇다면 위의 " +
                "'0개' 단언은 임계값이 아니라 다른 이유(렌더러 고장/미배치)로 통과하는 것이며 " +
                "아무것도 검증하지 못합니다.");
            Assert.IsTrue(_renderer.IsVisible, "임계값 복원 후에도 기분 표시가 뜨지 않습니다.");
            Assert.Greater(_renderer.ActiveVisualCount, 0,
                "임계값 복원 후에도 실제 LineRenderer가 0개입니다.");

            Debug.Log($"[스트레스OFF테스트] 네거티브 컨트롤 통과 — 임계값 0.4 복원 시 " +
                $"시각 오브젝트 {_renderer.ActiveVisualCount}개가 실제로 생김(= 끄는 것이 임계값의 효과임을 증명).");
        }
    }
}
