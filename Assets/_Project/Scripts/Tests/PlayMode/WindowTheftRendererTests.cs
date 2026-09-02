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
    /// ★ 창 도둑(docs/UX_FLOW.md 27-1) 시각 레이어 회귀 테스트.
    ///
    /// ============================================================================
    /// 이 테스트가 막으려는 실패 모드 (이 프로젝트에서 4번 반복된 바로 그것)
    /// ============================================================================
    /// 말풍선/드래그/격파(당시) 3건 모두 "로직은 완성됐는데 아무도 구독/배치를 안 해서 화면에 한 픽셀도
    /// 안 나온다"로 끝났다. 그래서 이 테스트는 렌더러 클래스를 <b>단독으로 new 해서</b> 검사하지 않고,
    /// SceneBootstrapper가 실제로 구워낸 <b>Main.unity를 그대로 로드</b>해서 검사한다 — 렌더러 코드가
    /// 아무리 완벽해도 씬에 배치돼 있지 않으면 이 테스트는 첫 줄에서 실패한다.
    ///
    /// ============================================================================
    /// 절대 조건으로 단언하는 것 (상대적 여유 금지 — 이 프로젝트는 "상대적 여유" 방식 테스트가
    /// 버그를 2라운드 연속 놓친 전례가 있다)
    /// ============================================================================
    ///  ① 씬에 WindowTheftDirector / WindowTheftRenderer가 <b>정확히 1개씩</b> 있다.
    ///     (0개 = 배치 누락, 2개 이상 = 중복 배치 함정 — 둘 다 즉시 실패시킨다.)
    ///  ② Started 이벤트를 발행하면 렌더러가 <b>실제로 GameObject를 만든다</b>
    ///     (ActiveVisualCount &gt; 0 이고 "WindowTheftGhostOverlay"가 씬에 실존한다).
    ///  ③ 그 오버레이는 콜라이더를 <b>정확히 0개</b> 만든다(27-1 관전 전용 = 클릭관통 유지).
    ///  ④ Completed / Cancelled 어느 쪽으로 끝나도 <b>전부 정리된다</b>
    ///     (ActiveVisualCount == 0 이고 컨테이너 GameObject가 씬에서 실제로 사라진다).
    ///  ⑤ 렌더러 컴포넌트를 비활성화해도 오버레이가 화면에 남지 않는다(OnDisable 정리 관례).
    /// </summary>
    public sealed class WindowTheftRendererTests
    {
        private const string ContainerName = "WindowTheftGhostOverlay";
        private static readonly Rect TargetRect = new Rect(160f, 140f, 420f, 260f);

        private WindowTheftRenderer _renderer;

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null; // Awake/Start가 완전히 실행되도록 한 프레임 더.

            // ① 배치 자체를 절대 조건으로 잠근다.
            var directors = Object.FindObjectsByType<WindowTheftDirector>(FindObjectsSortMode.None);
            Assert.AreEqual(1, directors.Length,
                $"씬의 WindowTheftDirector 개수가 {directors.Length}개입니다 — 1개여야 합니다. " +
                "0개면 Assets/Editor/SceneBootstrapper.cs 배치 누락(이 프로젝트에서 4번 반복된 실패 모드), " +
                "2개 이상이면 씬에 중복 배치된 것입니다.");

            var renderers = Object.FindObjectsByType<WindowTheftRenderer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, renderers.Length,
                $"씬의 WindowTheftRenderer 개수가 {renderers.Length}개입니다 — 1개여야 합니다. " +
                "2개 이상이면 두 번째 렌더러도 같은 전역 이벤트를 받아 고스트 창이 두 벌 그려집니다" +
                "(2026-08-29 격파 미니게임에서 실측된 버그 — 기능은 2026-09-02 삭제, 함정은 그대로).");

            _renderer = renderers[0];
            Assert.IsFalse(_renderer.IsVisible, "테스트 시작 시점에는 오버레이가 떠 있으면 안 됩니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "테스트 시작 시점의 시각 오브젝트는 0개여야 합니다.");
        }

        /// <summary>② + ③ — Started를 받으면 진짜로 그린다. 그리고 콜라이더는 0개다.</summary>
        [UnityTest]
        public IEnumerator StartedEventActuallyCreatesGhostWindowObjects()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowTheftOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);

            Assert.IsTrue(_renderer.IsVisible,
                "WindowTheftOverlayChanged(Started)를 발행했는데 렌더러가 아무것도 그리지 않았습니다 — " +
                "이벤트 구독이 끊겼거나 카메라/캐릭터 배선이 없습니다.");
            Assert.Greater(_renderer.ActiveVisualCount, 0,
                "오버레이가 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기).");
            Assert.AreEqual(0, _renderer.ActiveColliderCount,
                "창 도둑 오버레이는 관전 전용이라 콜라이더를 단 하나도 만들면 안 됩니다 — " +
                "콜라이더가 생기면 그 자리에서 클릭관통이 풀려 비침해 원칙 2를 위반합니다(27-1).");

            yield return null; // 실제로 한 프레임 렌더 루프(LateUpdate)를 태워본다.

            Assert.IsNotNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 실존하지 않습니다 — 렌더러가 오브젝트를 만들었다고 " +
                "보고했지만 실제 씬에는 없습니다.");
            Assert.Greater(_renderer.ActiveVisualCount, 0, "한 프레임 뒤 시각 오브젝트가 사라졌습니다.");
            Assert.AreEqual(0, _renderer.ActiveColliderCount, "한 프레임 뒤 콜라이더가 생겼습니다(클릭관통 위반).");

            Debug.Log($"[창도둑테스트] Started 검증 통과 — 시각 오브젝트 {_renderer.ActiveVisualCount}개 생성, 콜라이더 0개.");
        }

        /// <summary>④ 정상 종료(Completed) — 페이드아웃이 끝나면 하나도 남지 않는다.</summary>
        [UnityTest]
        public IEnumerator CompletedEventRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowTheftOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);
            yield return null;
            int spawned = _renderer.ActiveVisualCount;
            Assert.Greater(spawned, 0, "정리 검증의 사전 조건(오브젝트가 실제로 만들어졌는지)이 성립하지 않습니다.");

            StickmanEventBus.RaiseWindowTheftOverlayChanged(TargetRect, SpectacleOverlayPhase.Completed);
            yield return new WaitForSeconds(1.0f); // FadeOutSeconds(0.55초) + 여유.

            Assert.IsFalse(_renderer.IsVisible, "Completed 후에도 렌더러가 여전히 '보인다'고 보고합니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                $"Completed 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다(생성 시 {spawned}개).");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 그대로 남아 있습니다 — 컨테이너가 실제로 파괴되지 않았습니다.");

            Debug.Log($"[창도둑테스트] Completed 정리 검증 통과 — {spawned}개 생성 후 전부 제거(잔존 0개).");
        }

        /// <summary>④ 취소(Cancelled) — 유저가 실제로 그 창을 만졌을 때. 훨씬 빨리 사라져야 한다.</summary>
        [UnityTest]
        public IEnumerator CancelledEventRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowTheftOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);
            yield return null;
            Assert.Greater(_renderer.ActiveVisualCount, 0, "정리 검증의 사전 조건이 성립하지 않습니다.");

            StickmanEventBus.RaiseWindowTheftOverlayChanged(TargetRect, SpectacleOverlayPhase.Cancelled);
            yield return new WaitForSeconds(0.5f); // CancelFadeSeconds(0.14초) + 여유.

            Assert.IsFalse(_renderer.IsVisible, "Cancelled 후에도 렌더러가 여전히 '보인다'고 보고합니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "Cancelled 후에도 시각 오브젝트가 남아 있습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[창도둑테스트] Cancelled 정리 검증 통과 — 0.5초 안에 전부 제거.");
        }

        /// <summary>⑤ 컴포넌트가 꺼져도 화면에 유령이 남지 않는다(OnDisable 정리 관례).</summary>
        [UnityTest]
        public IEnumerator DisablingRendererRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseWindowTheftOverlayChanged(TargetRect, SpectacleOverlayPhase.Started);
            yield return null;
            Assert.Greater(_renderer.ActiveVisualCount, 0, "정리 검증의 사전 조건이 성립하지 않습니다.");

            _renderer.enabled = false;
            yield return null;

            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                "렌더러를 비활성화했는데 오버레이 오브젝트가 남아 있습니다 — 이 컴포넌트가 꺼진 뒤에는 " +
                "누구도 그것을 지워줄 수 없으므로 화면에 영구히 남습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[창도둑테스트] OnDisable 정리 검증 통과.");
        }
    }
}
