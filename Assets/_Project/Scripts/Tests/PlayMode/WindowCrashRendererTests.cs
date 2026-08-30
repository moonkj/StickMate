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
    /// ★ 윈도우 크래시(docs/UX_FLOW.md 27-4) 시각 레이어 회귀 테스트.
    ///
    /// WindowTheftRendererTests와 같은 이유로 <b>Main.unity를 실제로 로드해서</b> 검사한다 — 렌더러가
    /// 아무리 잘 만들어져 있어도 씬에 배치돼 있지 않으면 첫 줄에서 실패해야 한다("로직은 완성, 화면엔
    /// 0픽셀"이 이 프로젝트에서 4번 반복된 실패 모드다).
    ///
    /// ============================================================================
    /// 27-4가 못박은 비침해 계약을 코드로 고정한다
    /// ============================================================================
    /// 27-7 체크리스트의 검증 포인트는 <b>"크랙 레이어가 3초 내내 100% 클릭관통 상태인지"</b>다.
    /// 이 프로젝트에서 클릭관통이 풀리는 유일한 경로는 콜라이더 존재이므로(UniWindowController의
    /// Raycast 히트테스트 — Interaction/AppControlDirector.cs의 _menuBlocker, BattleMinigameRenderer의
    /// CreateClickTarget이 그 경로를 의도적으로 쓰는 두 사례다), <b>오버레이가 만든 콜라이더 개수가
    /// 항상 정확히 0</b>임을 생성 직후 · 유지 중 · 파편 낙하 중 세 시점에서 각각 단언한다.
    /// "적어도 이전보다는 적다" 같은 상대적 여유를 쓰지 않는 이유는, 이 프로젝트가 그 방식으로 버그를
    /// 2라운드 연속 놓친 전례가 있기 때문이다.
    /// </summary>
    public sealed class WindowCrashRendererTests
    {
        private const string ContainerName = "WindowCrashOverlay";
        private static readonly Rect TargetRect = new Rect(120f, 90f, 640f, 400f);

        private WindowCrashRenderer _renderer;

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var directors = Object.FindObjectsByType<WindowCrashDirector>(FindObjectsSortMode.None);
            Assert.AreEqual(1, directors.Length,
                $"씬의 WindowCrashDirector 개수가 {directors.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락, 2개 이상이면 씬에 중복 배치된 것입니다.");

            var renderers = Object.FindObjectsByType<WindowCrashRenderer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, renderers.Length,
                $"씬의 WindowCrashRenderer 개수가 {renderers.Length}개입니다 — 1개여야 합니다. " +
                "2개 이상이면 두 번째 렌더러도 전역 이벤트를 받아 균열이 두 벌 그려집니다.");

            _renderer = renderers[0];
            Assert.IsFalse(_renderer.IsVisible, "테스트 시작 시점에는 균열이 떠 있으면 안 됩니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "테스트 시작 시점의 시각 오브젝트는 0개여야 합니다.");
        }

        /// <summary>Started를 받으면 진짜로 균열을 그린다 — 그리고 콜라이더는 정확히 0개다.</summary>
        [UnityTest]
        public IEnumerator StartedEventActuallyCreatesCrackObjectsAndStaysClickThrough()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowCrashOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);

            Assert.IsTrue(_renderer.IsVisible,
                "WindowCrashOverlayChanged(Started)를 발행했는데 렌더러가 아무것도 그리지 않았습니다.");
            Assert.Greater(_renderer.ActiveVisualCount, 0,
                "균열이 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기).");
            Assert.AreEqual(0, _renderer.ActiveColliderCount,
                "생성 직후 크랙 오버레이에 콜라이더가 있습니다 — 27-4가 명시적으로 금지한 유일한 위반 " +
                "(\"보기엔 깨진 유리, 만지면 평범한 창\"이 유일하게 허용되는 구현).");

            yield return null;
            Assert.IsNotNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 실존하지 않습니다.");

            // "3초 내내" 계약 — 균열이 번지는 동안과 유지 중에도 계속 0개여야 한다.
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.2f);
                Assert.AreEqual(0, _renderer.ActiveColliderCount,
                    $"유지 중({(i + 1) * 0.2f:F1}초 경과) 크랙 오버레이에 콜라이더가 생겼습니다 — " +
                    "이 순간 대상 창의 클릭/타이핑이 막힙니다(비침해 원칙 2 위반).");
                Assert.IsTrue(_renderer.IsVisible,
                    "Director가 아직 Completed를 발행하지 않았는데 균열이 스스로 사라졌습니다 — " +
                    "유지 시간을 렌더러가 따로 세고 있다는 뜻입니다(두 곳에서 같은 시간을 세면 어긋난다).");
            }

            Debug.Log($"[창크래시테스트] Started + 유지 검증 통과 — 시각 오브젝트 {_renderer.ActiveVisualCount}개, 콜라이더 0개 유지.");
        }

        /// <summary>Completed(3초 경과) — 파편이 떨어지는 연출이 끝나면 하나도 남지 않는다.</summary>
        [UnityTest]
        public IEnumerator CompletedEventShattersAndRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowCrashOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);
            yield return null;
            int spawned = _renderer.ActiveVisualCount;
            Assert.Greater(spawned, 0, "정리 검증의 사전 조건이 성립하지 않습니다.");

            StickmanEventBus.RaiseWindowCrashOverlayChanged(TargetRect, SpectacleOverlayPhase.Completed);

            // 파편이 날아가는 동안에도 클릭관통은 유지된다.
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(0, _renderer.ActiveColliderCount, "파편 낙하 중 콜라이더가 생겼습니다(클릭관통 위반).");

            yield return new WaitForSeconds(0.8f); // ShatterSeconds(0.42초) + 여유.

            Assert.IsFalse(_renderer.IsVisible, "Completed 후에도 렌더러가 여전히 '보인다'고 보고합니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                $"Completed 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다(생성 시 {spawned}개).");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 그대로 남아 있습니다 — 컨테이너가 실제로 파괴되지 않았습니다.");

            Debug.Log($"[창크래시테스트] Completed 정리 검증 통과 — {spawned}개 생성 후 전부 제거(잔존 0개).");
        }

        /// <summary>Cancelled(대상 창 닫힘/최소화, 전체화면 게임 감지, 긴급정지) — 즉시 걷힌다.</summary>
        [UnityTest]
        public IEnumerator CancelledEventRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowCrashOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);
            yield return null;
            Assert.Greater(_renderer.ActiveVisualCount, 0, "정리 검증의 사전 조건이 성립하지 않습니다.");

            StickmanEventBus.RaiseWindowCrashOverlayChanged(TargetRect, SpectacleOverlayPhase.Cancelled);
            yield return new WaitForSeconds(0.5f); // CancelFadeSeconds(0.12초) + 여유.

            Assert.IsFalse(_renderer.IsVisible, "Cancelled 후에도 렌더러가 여전히 '보인다'고 보고합니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "Cancelled 후에도 시각 오브젝트가 남아 있습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[창크래시테스트] Cancelled 정리 검증 통과 — 0.5초 안에 전부 제거.");
        }

        /// <summary>컴포넌트가 꺼져도 균열이 화면에 남지 않는다(OnDisable 정리 관례).</summary>
        [UnityTest]
        public IEnumerator DisablingRendererRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowCrashOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);
            yield return null;
            Assert.Greater(_renderer.ActiveVisualCount, 0, "정리 검증의 사전 조건이 성립하지 않습니다.");

            _renderer.enabled = false;
            yield return null;

            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                "렌더러를 비활성화했는데 균열 오브젝트가 남아 있습니다 — 화면에 영구히 남습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[창크래시테스트] OnDisable 정리 검증 통과.");
        }
    }
}
