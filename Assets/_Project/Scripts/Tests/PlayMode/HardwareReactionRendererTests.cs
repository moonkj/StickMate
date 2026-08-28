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
    /// ★ PC 하드웨어 반응(docs/UX_FLOW.md 23 / 27-6) 시각 레이어 회귀 테스트.
    ///
    /// 창 도둑/창 크래시 테스트와 같은 이유로 <b>Main.unity를 실제로 로드해서</b> 검사한다 —
    /// HardwareReactionDirector도 이번 라운드 전까지 씬 어디에도 배치돼 있지 않아 Update()가 단 한 번도
    /// 돌지 않았고(폴링 자체가 없었고), HardwareReactionChanged 구독자도 0명이었다.
    ///
    /// ============================================================================
    /// 절대 조건으로 단언하는 것
    /// ============================================================================
    ///  ① 씬에 HardwareReactionDirector / HardwareReactionRenderer가 <b>정확히 1개씩</b> 있다.
    ///  ② 4종(배터리/CPU/네트워크/충전) <b>전부</b> Active=true에서 실제로 오브젝트를 만들고
    ///     Active=false에서 하나도 남기지 않는다 — 한 종류만 통과하고 나머지가 빈 껍데기인 상황을 막는다.
    ///  ③ 이모트는 콜라이더를 <b>정확히 0개</b> 만든다(관전 전용 = 클릭관통 유지).
    ///  ④ 23절 "동시에 두 가지 다른 표정/자세를 겹쳐 보이면 안 됨" — 다른 종류가 Active=true로 들어오면
    ///     이전 이모트가 <b>남아 있지 않고 교체</b>된다(컨테이너가 정확히 1개만 존재).
    ///
    /// SpectacleEventLock에 참여하지 않는 것이 정상이라는 점도 여기서 함께 확인한다(Phase 4 설계 결정 5):
    /// 이 테스트는 락을 전혀 잡지 않은 상태에서 이모트가 정상적으로 떠야 통과한다.
    /// </summary>
    public sealed class HardwareReactionRendererTests
    {
        private const string ContainerName = "HardwareReactionEmote";

        private static readonly HardwareReactionKind[] AllKinds =
        {
            HardwareReactionKind.LowBattery,
            HardwareReactionKind.HighCpu,
            HardwareReactionKind.NetworkDown,
            HardwareReactionKind.Charging,
        };

        private HardwareReactionRenderer _renderer;

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var directors = Object.FindObjectsByType<HardwareReactionDirector>(FindObjectsSortMode.None);
            Assert.AreEqual(1, directors.Length,
                $"씬의 HardwareReactionDirector 개수가 {directors.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락(폴링 자체가 돌지 않는다), 2개 이상이면 라이벌 복제본에서 " +
                "제거되지 않아 같은 신호가 두 번 판정됩니다.");

            var renderers = Object.FindObjectsByType<HardwareReactionRenderer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, renderers.Length,
                $"씬의 HardwareReactionRenderer 개수가 {renderers.Length}개입니다 — 1개여야 합니다. " +
                "2개 이상이면 라이벌 머리 위에도 이모트가 한 벌 더 뜹니다.");

            _renderer = renderers[0];
            Assert.IsFalse(_renderer.IsVisible, "테스트 시작 시점에는 이모트가 떠 있으면 안 됩니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "테스트 시작 시점의 시각 오브젝트는 0개여야 합니다.");
        }

        /// <summary>② + ③ — 4종 전부 실제로 그려지고, 전부 깨끗하게 정리된다.</summary>
        [UnityTest]
        public IEnumerator EveryReactionKindDrawsAndFullyCleansUp()
        {
            yield return LoadSceneAndResolve();

            for (int i = 0; i < AllKinds.Length; i++)
            {
                HardwareReactionKind kind = AllKinds[i];

                StickmanEventBus.RaiseHardwareReactionChanged(kind, active: true);

                Assert.IsTrue(_renderer.IsVisible,
                    $"HardwareReactionChanged({kind}, active:true)를 발행했는데 이모트가 나타나지 않았습니다.");
                Assert.AreEqual(kind, _renderer.VisibleKind,
                    $"표시 중인 반응 종류가 {_renderer.VisibleKind}로, 발행한 {kind}와 다릅니다.");
                Assert.Greater(_renderer.ActiveVisualCount, 0,
                    $"{kind} 이모트가 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기) — " +
                    "종류별 도형 빌더가 아무것도 만들지 않았다는 뜻입니다.");
                Assert.AreEqual(0, _renderer.ActiveColliderCount,
                    $"{kind} 이모트가 콜라이더를 만들었습니다 — 관전 전용 연출이므로 클릭관통이 유지되어야 합니다.");

                yield return null;
                Assert.IsNotNull(GameObject.Find(ContainerName),
                    $"{kind}: '{ContainerName}' GameObject가 씬에 실존하지 않습니다.");

                int spawned = _renderer.ActiveVisualCount;

                StickmanEventBus.RaiseHardwareReactionChanged(kind, active: false);
                yield return new WaitForSeconds(0.8f); // FadeOutSeconds(0.40초) + 여유.

                Assert.IsFalse(_renderer.IsVisible, $"{kind}: active:false 후에도 이모트가 '보인다'고 보고합니다.");
                Assert.IsNull(_renderer.VisibleKind, $"{kind}: 정리 후에도 VisibleKind가 남아 있습니다.");
                Assert.AreEqual(0, _renderer.ActiveVisualCount,
                    $"{kind}: active:false 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다(생성 시 {spawned}개).");
                Assert.IsNull(GameObject.Find(ContainerName),
                    $"{kind}: '{ContainerName}' GameObject가 씬에 그대로 남아 있습니다.");

                Debug.Log($"[하드웨어테스트] {kind} 검증 통과 — 시각 오브젝트 {spawned}개 생성 후 전부 제거, 콜라이더 0개.");
            }
        }

        /// <summary>④ 23절 "동시에 두 가지 표정을 겹쳐 보이면 안 됨" — 새 반응이 이전 것을 교체한다.</summary>
        [UnityTest]
        public IEnumerator NewReactionReplacesPreviousInsteadOfStacking()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.LowBattery, active: true);
            yield return null;
            Assert.AreEqual(HardwareReactionKind.LowBattery, _renderer.VisibleKind, "사전 조건이 성립하지 않습니다.");

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.Charging, active: true);
            yield return null;

            Assert.AreEqual(HardwareReactionKind.Charging, _renderer.VisibleKind,
                "새 반응이 들어왔는데 이전 반응이 그대로 표시되고 있습니다.");

            var containers = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int containerCount = 0;
            for (int i = 0; i < containers.Length; i++)
            {
                if (containers[i].name == ContainerName && containers[i].parent == null) containerCount++;
            }
            Assert.AreEqual(1, containerCount,
                $"'{ContainerName}' 컨테이너가 {containerCount}개 존재합니다 — 이전 이모트가 지워지지 않고 " +
                "새 이모트가 그 위에 겹쳐 그려졌다는 뜻입니다(23절 '동시에 두 가지 표정 금지' 위반).");

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.Charging, active: false);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "교체 후 정리에서 오브젝트가 남았습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[하드웨어테스트] 교체 검증 통과 — 컨테이너는 항상 1개, 종료 후 0개.");
        }

        /// <summary>컴포넌트가 꺼져도 이모트가 화면에 남지 않는다(OnDisable 정리 관례).</summary>
        [UnityTest]
        public IEnumerator DisablingRendererRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.HighCpu, active: true);
            yield return null;
            Assert.Greater(_renderer.ActiveVisualCount, 0, "정리 검증의 사전 조건이 성립하지 않습니다.");

            _renderer.enabled = false;
            yield return null;

            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                "렌더러를 비활성화했는데 이모트 오브젝트가 남아 있습니다 — 화면에 영구히 남습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[하드웨어테스트] OnDisable 정리 검증 통과.");
        }
    }
}
