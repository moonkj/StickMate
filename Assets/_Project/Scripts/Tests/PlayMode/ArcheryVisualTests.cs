using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 활쏘기 연출(2026-08-29 사용자 요청 "과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동")
    /// 회귀 테스트.
    ///
    /// WindowCrashRendererTests / WindowTheftRendererTests와 같은 이유로 <b>Main.unity를 실제로
    /// 로드해서</b> 검사한다 — 렌더러/디렉터가 아무리 잘 만들어져 있어도 씬에 배치돼 있지 않으면 첫
    /// 줄에서 실패해야 한다("로직은 완성, 화면엔 0픽셀"이 이 프로젝트에서 6번 반복된 실패 모드다).
    /// 그리고 개수를 <b>정확히 1</b>로 단언한다: 0이면 SceneBootstrapper 배치 누락, 2 이상이면 중복
    /// 복제본에서 제거되지 않아 과녁이 두 벌 그려진다는 뜻이다(실측 전례가 있는 버그).
    ///
    /// ============================================================================
    /// 절대 조건 + 네거티브 컨트롤 (이 프로젝트 표준)
    /// ============================================================================
    /// · 콜라이더 수는 "적다"가 아니라 <b>정확히 0</b>(관전 전용, 클릭관통 유지 — 비침해 원칙 2).
    /// · 발사 횟수는 "여러 발"이 아니라 <b>정확히 3</b>이고, 결과 시나리오는 "다양하다"가 아니라
    ///   <b>마지막은 Bullseye, 앞 두 발 중 정확히 하나가 Miss</b>다.
    /// · 배율 검증은 "0.5는 1.0의 절반"이 아니라 <b>바깥에서 온 절대식</b>과 맞댄다
    ///   (과녁 꼭대기 == 캐릭터 정수리). 자기 자신을 기준으로 한 비율 비교는 둘 다 틀린 경우를
    ///   통과시킨다(RendererScaleRatioTests의 판단 기준과 동일).
    /// · <see cref="NoStartedEventMeansNothingIsDrawn"/> / <see cref="AbsoluteSizeWouldBreakScaleInvariant"/>가
    ///   네거티브 컨트롤이다.
    /// </summary>
    public sealed class ArcheryVisualTests
    {
        private const string ContainerName = "ArcheryVisuals";
        private const float Tol = 1e-4f;

        private ArcheryRenderer _renderer;
        private ArcheryDirector _director;
        private StickmanAgent _agent;

        private readonly List<ArcheryShotEvent> _releases = new List<ArcheryShotEvent>(4);
        private bool _listening;

        private readonly List<GameObject> _rigs = new List<GameObject>(3);

        [TearDown]
        public void TearDown()
        {
            if (_listening)
            {
                StickmanEventBus.ArcheryShotChanged -= OnShot;
                _listening = false;
            }
            _releases.Clear();
            for (int i = 0; i < _rigs.Count; i++) if (_rigs[i] != null) Object.DestroyImmediate(_rigs[i]);
            _rigs.Clear();
        }

        private void OnShot(ArcheryShotEvent evt)
        {
            if (evt.Phase == ArcheryShotPhase.Release) _releases.Add(evt);
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var directors = Object.FindObjectsByType<ArcheryDirector>(FindObjectsSortMode.None);
            Assert.AreEqual(1, directors.Length,
                $"씬의 ArcheryDirector 개수가 {directors.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락, 2개 이상이면 씬에 중복 배치된 것입니다.");

            var renderers = Object.FindObjectsByType<ArcheryRenderer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, renderers.Length,
                $"씬의 ArcheryRenderer 개수가 {renderers.Length}개입니다 — 1개여야 합니다. " +
                "2개 이상이면 두 번째 렌더러도 전역 이벤트를 받아 과녁이 두 벌 그려집니다.");

            _director = directors[0];
            _renderer = renderers[0];
            _agent = _renderer.GetComponent<StickmanAgent>();
            Assert.IsNotNull(_agent, "ArcheryRenderer가 StickmanAgent와 같은 GameObject에 있지 않습니다 — " +
                "이 렌더러는 씬 전체 탐색 폴백을 쓰지 않으므로 그러면 영원히 아무것도 그리지 않습니다.");

            Assert.IsFalse(_renderer.IsVisible, "테스트 시작 시점에는 과녁이 떠 있으면 안 됩니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "테스트 시작 시점의 시각 오브젝트는 0개여야 합니다.");
        }

        // ============================================================================
        // ① 배치 — 씬에 정확히 1개씩
        // ============================================================================

        [UnityTest]
        public IEnumerator SceneHasExactlyOneDirectorAndOneRenderer()
        {
            yield return LoadSceneAndResolve();
            Debug.Log("[활쏘기테스트] 배치 검증 통과 — 디렉터 1개 / 렌더러 1개.");
        }

        // ============================================================================
        // ② 이벤트 발행 시 시각 오브젝트가 실존한다 + 콜라이더는 정확히 0개
        // ============================================================================

        [UnityTest]
        public IEnumerator StartedEventActuallyCreatesTargetAndStaysClickThrough()
        {
            yield return LoadSceneAndResolve();

            Vector2 foot = _agent.Blackboard.Body.position;
            var target = new Vector2(foot.x + 4f, foot.y + 1.2f);
            StickmanEventBus.RaiseArcheryOverlayChanged(target, foot.y, 1f, SpectacleOverlayPhase.Started);

            Assert.IsTrue(_renderer.IsVisible,
                "ArcheryOverlayChanged(Started)를 발행했는데 렌더러가 아무것도 그리지 않았습니다.");
            Assert.Greater(_renderer.ActiveVisualCount, 0,
                "과녁이 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기).");
            Assert.AreEqual(0, _renderer.ActiveColliderCount,
                "과녁 오버레이가 콜라이더를 만들었습니다 — 관전 전용이므로 정확히 0개여야 합니다" +
                "(콜라이더가 있으면 그 자리의 다른 앱을 클릭할 수 없게 되어 비침해 원칙 2 위반).");

            yield return null;
            Assert.IsNotNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 실존하지 않습니다.");

            // 유지 중에도 계속 0개.
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.15f);
                Assert.AreEqual(0, _renderer.ActiveColliderCount,
                    $"유지 중({(i + 1) * 0.15f:F2}초) 콜라이더가 생겼습니다.");
            }

            Debug.Log($"[활쏘기테스트] Started 검증 통과 — 시각 오브젝트 {_renderer.ActiveVisualCount}개, 콜라이더 0개.");
        }

        /// <summary>네거티브 컨트롤 — 이벤트가 없으면 아무것도 그리지 않는다(위 단언이 "무조건 뭔가
        /// 있다"로 통과하는 것이 아님을 증명).</summary>
        [UnityTest]
        public IEnumerator NoStartedEventMeansNothingIsDrawn()
        {
            yield return LoadSceneAndResolve();
            yield return new WaitForSeconds(0.6f);

            Assert.IsFalse(_renderer.IsVisible, "아무 이벤트도 발행하지 않았는데 과녁이 떠 있습니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "이벤트 없이 시각 오브젝트가 생겼습니다.");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"이벤트 없이 '{ContainerName}' GameObject가 씬에 생겼습니다.");
        }

        // ============================================================================
        // ③ 전체 사이클 — 3발이 실제로 나가고, 끝나면 컨테이너가 씬에서 실제로 소멸한다
        // ============================================================================

        [UnityTest]
        public IEnumerator FullCycleFiresExactlyThreeArrowsAndFullyCleansUp()
        {
            yield return LoadSceneAndResolve();

            // 캐릭터가 **실제로 발판을 딛을 때까지** 기다린다. 상태 ID만 보면 안 된다 — 씬 시작 직후의
            // 초기 상태가 이미 Idle이라, 아직 낙하해 발판을 잡기도 전에 조건이 성립해버리고
            // (CurrentFootholdHandle == 0) 활쏘기가 시작되자마자 [발판상실]로 Fall에 빠진다
            // (실측으로 밟은 함정: 발사 이벤트 0건).
            float wait = 0f;
            while (wait < 10f)
            {
                var st = _agent.Blackboard.Machine.CurrentStateId;
                bool ready = (st == StickmanStateId.Idle || st == StickmanStateId.Walk)
                    && _agent.Blackboard.CurrentFootholdHandle != 0L;
                if (ready) break;
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Idle
                || _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Walk,
                $"10초를 기다려도 캐릭터가 Idle/Walk가 되지 않았습니다(현재 {_agent.Blackboard.Machine.CurrentStateId}) — " +
                "활쏘기는 그 두 상태에서만 시작합니다.");
            Assert.AreNotEqual(0L, _agent.Blackboard.CurrentFootholdHandle,
                "10초를 기다려도 캐릭터가 어떤 발판도 딛지 못했습니다 — 이 상태로 활쏘기를 시작하면 " +
                "0.1초 뒤 [발판상실]로 Fall에 빠져 아무것도 검증하지 못합니다.");

            StickmanEventBus.ArcheryShotChanged += OnShot;
            _listening = true;

            _director.ForceTriggerNow("PlayMode 테스트");
            yield return null;

            Assert.AreEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "ForceTriggerNow 후에도 상태가 Archery가 아닙니다 — 과녁 자리를 못 찾았거나 락에 막혔습니다.");
            Assert.IsTrue(SpectacleEventLock.IsActive && SpectacleEventLock.ActiveKind == SpectacleEventKind.Archery,
                "활쏘기가 SpectacleEventLock을 잡지 않았습니다 — 다른 스펙터클과 동시에 발동할 수 있게 됩니다.");

            // ★ 사용자 재정의 사양: "…만큼 캐릭터가 이동한 다음 과녁을 생성후 쏘고".
            // 즉 발동 직후에는 아직 과녁이 없어야 하고, **걸어서 도착한 뒤**에 나타나야 한다.
            Vector2 footAtTrigger = _agent.Blackboard.Body.position;
            Assert.IsFalse(_renderer.IsVisible,
                "발동하자마자 과녁이 나타났습니다 — 사용자 요구 순서는 '이동 -> 과녁 생성 -> 발사'입니다.");

            float approachWait = 0f;
            while (approachWait < 15f && !_renderer.IsVisible)
            {
                approachWait += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(_renderer.IsVisible,
                "15초를 기다려도 과녁이 나타나지 않았습니다 — 이동(Approach) 단계에서 멈춰 있습니다.");

            Vector2 footAtStart = _agent.Blackboard.Body.position;
            long footholdAtStart = _agent.Blackboard.CurrentFootholdHandle;
            float walked = Mathf.Abs(footAtStart.x - footAtTrigger.x);

            // 캐릭터는 과녁 쪽을 보고 있어야 한다(등지고 쏘면 활이 등 뒤에 그려진다 — 실측 버그).
            float dirToTarget = Mathf.Sign(_director.LastTargetWorld.x - footAtStart.x);
            Assert.AreEqual(dirToTarget, _agent.Blackboard.FacingSign, 0.001f,
                $"캐릭터가 과녁 반대쪽을 보고 있습니다(과녁 x={_director.LastTargetWorld.x:F2}, " +
                $"캐릭터 x={footAtStart.x:F2}, 바라보는 방향={_agent.Blackboard.FacingSign}).");
            Assert.AreEqual(_agent.Blackboard.ArcheryFacingSign, _agent.Blackboard.FacingSign, 0.001f,
                "과녁을 놓은 방향과 캐릭터가 보는 방향이 다릅니다.");
            Assert.IsTrue(_agent.Blackboard.FacingLocked,
                "쏘기 시작했는데 방향 고정이 걸려 있지 않습니다 — 배회 AI의 이동 의도로 몸이 돌아가 " +
                "화살이 뒤통수에서 나갈 수 있습니다.");

            // ★ 사거리 절대 조건 — 발판 종류에 따라 요구치가 다르다(사용자 명시).
            GroundSensor.GroundInfo groundInfo = _agent.Blackboard.SenseGround();
            float shootDistance = Mathf.Abs(_director.LastTargetWorld.x - footAtStart.x);
            float height = _agent.Blackboard.CharacterHeightWorld;
            Assert.GreaterOrEqual(shootDistance, height * 2.6f - 0.01f,
                $"사거리가 {shootDistance:F2}유닛뿐입니다 — 최소 사거리(신장의 2.6배 = " +
                $"{height * 2.6f:F2}유닛)조차 안 됩니다. 코앞에서 쏘면 포물선이 직선처럼 보입니다.");

            if (groundInfo.Grounded)
            {
                Assert.GreaterOrEqual(_director.LastTargetWorld.x, groundInfo.CurrentFootholdLeftWorldX - 0.01f,
                    $"과녁 x={_director.LastTargetWorld.x:F2}가 딛고 있는 발판의 왼쪽 끝" +
                    $"({groundInfo.CurrentFootholdLeftWorldX:F2}) 바깥입니다 — 창 모서리 너머 허공에 뜹니다.");
                Assert.LessOrEqual(_director.LastTargetWorld.x, groundInfo.CurrentFootholdRightWorldX + 0.01f,
                    $"과녁 x={_director.LastTargetWorld.x:F2}가 딛고 있는 발판의 오른쪽 끝" +
                    $"({groundInfo.CurrentFootholdRightWorldX:F2}) 바깥입니다 — 창 모서리 너머 허공에 뜹니다.");

                if (!ArcheryDirector.IsRealWindowFoothold(groundInfo.GroundedFootholdHandle)
                    && _agent.Blackboard.TryGetWalkableScreenBoundsWorld(out float wl, out float wr))
                {
                    // 바탕화면(안전망 발판) — 사용자 명시 "화면 전체 길이의 절반 이상".
                    // 화면/발판 폭이 그만큼도 안 될 때의 타협은 허용하므로, 확보 가능한 최대치와 비교한다.
                    float best = (wr - wl) - height * (0.35f + 0.20f) - _renderer.TargetRadius;
                    float required = Mathf.Min((wr - wl) * 0.5f, best);
                    Assert.GreaterOrEqual(shootDistance, required - 0.05f,
                        $"바탕화면인데 사거리가 {shootDistance:F2}유닛으로 요구치({required:F2}유닛, " +
                        $"화면 폭 {(wr - wl):F2}의 절반 또는 확보 가능한 최대치)에 못 미칩니다.");
                }
            }

            Debug.Log($"[활쏘기테스트] 이동 검증 — {walked:F2}유닛 걸어간 뒤 과녁 등장, 사거리 {shootDistance:F2}유닛 " +
                $"(발판 종류: {(ArcheryDirector.IsRealWindowFoothold(groundInfo.GroundedFootholdHandle) ? "창/Dock" : "바탕화면")}).");

            // 사이클 전체를 지켜본다(인트로 0.55 + 3발 3.18 + 아웃트로 1.37 + 렌더러 페이드 0.75 = 약 5.9초).
            float elapsed = 0f;
            int maxSpawned = 0;
            int maxStuck = 0;
            var stuckResults = new List<ArcheryShotResult>(3);
            var stuckDescents = new List<float>(3);
            var stuckOvershoots = new List<float>(3);
            bool leftArchery = false;
            Vector2 footAtStateEnd = footAtStart;
            float archerySeconds = 0f;
            while (elapsed < 12f)
            {
                elapsed += Time.deltaTime;
                maxSpawned = Mathf.Max(maxSpawned, _renderer.SpawnedArrowCount);
                maxStuck = Mathf.Max(maxStuck, _renderer.StuckArrowCount);
                // 꽂힌 화살의 모양은 **사라지기 전에** 재야 한다 — 루프를 빠져나올 조건이
                // "렌더러가 안 보임"이고 그 시점에는 이미 Teardown으로 화살 목록이 비어 있다.
                if (_renderer.StuckArrowCount == 3 && stuckResults.Count == 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (!_renderer.TryGetStuckArrow(i, out ArcheryShotResult sr, out float sd, out float so)) continue;
                        stuckResults.Add(sr);
                        stuckDescents.Add(sd);
                        stuckOvershoots.Add(so);
                    }
                }
                if (!leftArchery)
                {
                    archerySeconds = elapsed;
                    if (_agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Archery)
                    {
                        // ★ 위치는 **상태가 끝나는 그 순간** 재야 한다. 그 뒤에는 배회 AI가 다시
                        // 걷기 시작하므로, 나중에 재면 "활쏘기가 옮겼다"와 "끝나고 걸어갔다"를
                        // 구분할 수 없다(실측으로 밟은 함정 — 1.31유닛 차이가 전부 종료 후 보행이었다).
                        leftArchery = true;
                        footAtStateEnd = _agent.Blackboard.Body.position;
                    }
                }
                if (!_renderer.IsVisible && _releases.Count >= 3) break;
                yield return null;
            }
            Assert.IsTrue(leftArchery, "12초 안에 활쏘기가 끝나지 않았습니다 — 3발 뒤 종료 조건이 걸리지 않습니다.");
            Assert.Less(archerySeconds, 10f,
                $"활쏘기 한 사이클이 {archerySeconds:F1}초나 걸렸습니다 — 3발 연출이 늘어집니다.");

            Assert.AreEqual(3, _releases.Count,
                $"발사 이벤트가 {_releases.Count}건입니다 — 사용자 요청은 '3번정도'이고 코드 상수도 3입니다.");
            Assert.AreEqual(3, maxSpawned,
                $"실제로 스폰된 화살이 {maxSpawned}개입니다 — 발사 이벤트만 나가고 화살이 안 그려지면 " +
                "화면에는 아무 일도 일어나지 않은 것과 같습니다.");
            Assert.AreEqual(3, maxStuck,
                $"과녁/땅에 꽂힌 화살이 {maxStuck}개입니다 — 3발 전부가 도달점까지 날아가 꽂혀야 합니다.");

            // 시나리오: 마지막은 항상 정중앙, 앞 두 발 중 정확히 하나가 빗나감(3발이 똑같으면 지루하다).
            Assert.AreEqual(ArcheryShotResult.Bullseye, _releases[2].Result,
                "마지막 발이 정중앙이 아닙니다 — 연출의 클라이맥스가 고정돼 있어야 합니다.");
            int missCount = 0;
            for (int i = 0; i < 2; i++) if (_releases[i].Result == ArcheryShotResult.Miss) missCount++;
            Assert.AreEqual(1, missCount,
                $"앞 두 발 중 빗나간 발이 {missCount}개입니다 — 정확히 1개여야 합니다(전부 명중하면 " +
                "3발이 똑같아 지루하고, 전부 빗나가면 김이 샙니다).");

            // 도달점 검증: 명중은 과녁 반경 안, 빗나감은 지면 높이.
            Vector2 targetWorld = _director.LastTargetWorld;
            float radius = _renderer.TargetRadius;
            for (int i = 0; i < 3; i++)
            {
                ArcheryShotEvent e = _releases[i];
                if (e.Result == ArcheryShotResult.Miss)
                {
                    Assert.Less(Mathf.Abs(e.ImpactWorld.x - targetWorld.x), radius * 4f,
                        $"{i + 1}발째(빗나감) 도달점이 과녁에서 너무 멉니다 — 화면 밖으로 나갑니다.");
                    Assert.Less(e.ImpactWorld.y, targetWorld.y - radius,
                        $"{i + 1}발째는 빗나감인데 도달점이 과녁 아래(땅)가 아닙니다.");
                }
                else
                {
                    Assert.LessOrEqual(Vector2.Distance(e.ImpactWorld, targetWorld), radius + Tol,
                        $"{i + 1}발째({e.Result}) 도달점이 과녁 바깥입니다.");
                }
                if (e.Result == ArcheryShotResult.Bullseye)
                {
                    Assert.AreEqual(0f, Vector2.Distance(e.ImpactWorld, targetWorld), Tol,
                        "정중앙인데 도달점이 과녁 중심과 다릅니다.");
                }
            }

            // ★ 2026-08-29 사용자 신고 "화살이 과녁에 좀 이상하게 꽂힘 / 다 외곽에 꽂히는거 같음"
            //   회귀 잠금 — 실제 씬에서 꽂힌 3발의 **모양**을 잰다.
            Assert.AreEqual(3, stuckResults.Count,
                "3발이 꽂힌 순간의 모양을 재지 못했습니다 — 관찰 창구(TryGetStuckArrow)가 비어 있습니다.");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0f, stuckOvershoots[i], 1e-4f,
                    $"{i + 1}발째({stuckResults[i]}) 화살이 도달점보다 진행 방향으로 " +
                    $"{stuckOvershoots[i]:F3}유닛 더 나가 있습니다 — 도달점에 꽂히는 것은 **촉**이어야 하는데 " +
                    "오늬(꼬리)가 꽂혀 화살이 과녁을 관통해 반대편으로 삐져나온 그림이 됩니다. " +
                    "정중앙에 맞은 화살조차 촉이 바깥 링에 걸려 '다 외곽에 꽂힌다'로 보인 실제 신고 원인입니다.");
                Assert.GreaterOrEqual(stuckDescents[i], -1f,
                    $"{i + 1}발째 화살이 코를 위로 든 채 꽂혔습니다({stuckDescents[i]:F1}도) — 내려꽂혀야 합니다.");

                if (stuckResults[i] == ArcheryShotResult.Miss)
                {
                    Assert.AreEqual(_renderer.GroundImpactDescentDegrees, stuckDescents[i], 0.75f,
                        $"{i + 1}발째(빗나감) 땅에 꽂힌 각도가 {stuckDescents[i]:F1}도입니다 — " +
                        $"설정값 {_renderer.GroundImpactDescentDegrees:F1}도로 확정되어야 합니다.");
                }
                else
                {
                    Assert.LessOrEqual(stuckDescents[i], _renderer.FaceImpactMaxDescentDegrees + 0.75f,
                        $"{i + 1}발째({stuckResults[i]}) 과녁 면에 {stuckDescents[i]:F1}도로 꽂혔습니다 — " +
                        $"상한 {_renderer.FaceImpactMaxDescentDegrees:F1}도를 넘습니다. 이 정도로 가파르면 " +
                        "화살이 과녁 면을 비스듬히 가로질러 '이상하게 꽂혔다'로 보입니다.");
                }
            }
            Debug.Log($"[활쏘기테스트] 꽂힌 3발 하강각 = {stuckDescents[0]:F1} / {stuckDescents[1]:F1} / " +
                $"{stuckDescents[2]:F1}도, 촉 초과분 = {stuckOvershoots[0]:F3} / {stuckOvershoots[1]:F3} / " +
                $"{stuckOvershoots[2]:F3}유닛(전부 0이어야 정상).");

            // 정리 — 컨테이너가 씬에서 **실제로** 소멸했는가.
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_renderer.IsVisible, "사이클이 끝났는데 렌더러가 여전히 '보인다'고 보고합니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                $"사이클 종료 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다.");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 그대로 남아 있습니다 — 컨테이너가 실제로 파괴되지 않았습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive,
                "사이클이 끝났는데 SpectacleEventLock이 잡힌 채로 남아 있습니다 — 이후 다른 스펙터클이 영영 발동하지 못합니다.");
            // ★ 사용자 신고 "다른행동은 아예안하고 계속 활만쏨" 회귀 잠금 — 끝나자마자 다시 시작할 수 없다.
            Assert.Greater(_director.CooldownRemaining, 1f,
                $"사이클이 끝났는데 자율 재발동 쿨다운이 {_director.CooldownRemaining:F1}초뿐입니다 — " +
                "이러면 Idle로 돌아오자마자 다시 활을 쏴서 유저 눈에는 '끝나지 않는다'로 보입니다.");
            Assert.IsTrue(_agent.Config.archeryChance <= 0f,
                $"출하 설정의 활쏘기 자율 발동 확률이 {_agent.Config.archeryChance}입니다 — 0이어야 합니다" +
                "(검증용 임시값이 원복되지 않은 채 커밋에 섞이는 사고가 이 프로젝트에 이미 1회 있었습니다).");
            Assert.IsFalse(_agent.Blackboard.FacingLocked,
                "사이클이 끝났는데 방향 고정이 풀리지 않았습니다 — 캐릭터가 영영 한쪽만 보고 걷습니다.");
            // 정상 종료는 Idle이지만, 배회 AI가 이미 "걷기" 국면에 들어가 있으면 IdleState가 같은
            // 프레임에 Walk로 넘긴다 — 둘 다 정상적인 능동 상태 복귀다(Fall/Ragdoll이면 문제).
            var endState = _agent.Blackboard.Machine.CurrentStateId;
            Assert.IsTrue(endState == StickmanStateId.Idle || endState == StickmanStateId.Walk,
                $"활쏘기가 끝난 뒤 능동 상태로 복귀하지 않았습니다(현재 {endState}).");

            // 접지 유지 — 활쏘기는 캐릭터를 옮기지도, 띄우지도 않는다(사용자 신고 '하늘로 올라감' 회귀 잠금).
            Assert.AreEqual(footAtStart.y, footAtStateEnd.y, 0.05f,
                $"활쏘기 동안 캐릭터 Y가 {footAtStart.y:F2} -> {footAtStateEnd.y:F2}로 바뀌었습니다 — " +
                "이 연출은 캐릭터를 세로로 1픽셀도 옮기지 않아야 합니다.");
            Assert.AreEqual(footholdAtStart, _agent.Blackboard.CurrentFootholdHandle,
                "활쏘기 전후로 딛고 있는 발판이 바뀌었습니다 — 제자리에서 쏴야 합니다.");
            Assert.AreEqual(footAtStart.x, footAtStateEnd.x, 0.35f,
                $"활쏘기 동안 캐릭터 X가 {footAtStart.x:F2} -> {footAtStateEnd.x:F2}로 움직였습니다 — " +
                "쏘는 동안에는 제자리에 서 있어야 합니다.");

            Debug.Log($"[활쏘기테스트] 전체 사이클 통과 — 3발 발사/3발 꽂힘, 시나리오 " +
                $"{_releases[0].Result}/{_releases[1].Result}/{_releases[2].Result}, 종료 시 전부 소멸 + 락 해제.");
        }

        // ============================================================================
        // ③-b 활쏘기 중 캐릭터 클릭 -> 즉시 취소 (사용자 요구)
        // ============================================================================

        /// <summary>
        /// 사용자 요구 "활을 쏘는동안은 캐릭터가 클릭이 안됨. 클릭을하면 과녁이랑 활이 없어져야지".
        /// 클릭 한 번으로 과녁/활/화살이 <b>전부</b> 사라지고 락도 반납되는지를 절대 조건으로 잠근다.
        /// </summary>
        [UnityTest]
        public IEnumerator ClickingCharacterDuringArcheryCancelsEverything()
        {
            yield return LoadSceneAndResolve();

            float wait = 0f;
            while (wait < 10f)
            {
                var st = _agent.Blackboard.Machine.CurrentStateId;
                if ((st == StickmanStateId.Idle || st == StickmanStateId.Walk)
                    && _agent.Blackboard.CurrentFootholdHandle != 0L) break;
                wait += Time.deltaTime;
                yield return null;
            }

            _director.ForceTriggerNow("PlayMode 클릭취소 테스트");
            yield return null;
            Assert.AreEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "사전 조건 불성립 — 활쏘기가 시작되지 않았습니다.");

            // ★ 리더 지시: 클릭 취소는 **이동 단계에서도** 동작해야 한다. 여기서는 아직 과녁이 뜨기
            // 전(걸어가는 중)에 클릭해, 그 단계에서도 정상적으로 걷히는지를 확인한다.
            Assert.IsFalse(_renderer.IsVisible,
                "사전 조건 불성립 — 이동 단계여야 하는데 과녁이 벌써 떠 있습니다.");

            // 실제 클릭 경로와 같은 이벤트를 쏜다(새 입력 경로를 만들지 않았음을 함께 확인).
            var hitbox = _agent.GetComponent<StickmanClickHitbox>();
            Assert.IsNotNull(hitbox, "StickmanClickHitbox가 캐릭터에 없습니다.");
            hitbox.SimulateMouseDownForTests();

            yield return null;
            Assert.AreNotEqual(StickmanStateId.Archery, _agent.Blackboard.Machine.CurrentStateId,
                "캐릭터를 클릭했는데도 여전히 활쏘기 중입니다 — 클릭이 씹히고 있습니다.");

            yield return new WaitForSeconds(1.2f); // 렌더러 아웃트로 여유.
            Assert.IsFalse(_renderer.IsVisible, "클릭 취소 후에도 과녁이 남아 있습니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                $"클릭 취소 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다.");
            Assert.IsNull(GameObject.Find(ContainerName),
                $"'{ContainerName}' GameObject가 씬에 그대로 남아 있습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive,
                "클릭 취소 후 SpectacleEventLock이 잡힌 채로 남았습니다 — 이후 다른 연출이 영영 발동하지 못합니다.");
            Assert.IsFalse(_agent.Blackboard.FacingLocked, "클릭 취소 후 방향 고정이 풀리지 않았습니다.");

            Debug.Log("[활쏘기테스트] 클릭 취소 검증 통과 — 클릭 1회로 과녁/활/화살 전부 소멸 + 락 반납.");
        }

        // ============================================================================
        // ④ 궤적 — 확정된 도달점을 반드시 지나고, 그 사이는 직선이 아니라 위로 부푼 포물선이다
        // ============================================================================

        [Test]
        public void TrajectoryPassesThroughPlannedImpactAndArcsAboveTheChord()
        {
            var from = new Vector2(0f, 1.4f);
            var to = new Vector2(5f, 0.9f);
            const float flight = 0.62f;
            const float apex = 0.65f;

            Vector2 start = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, 0f);
            Vector2 end = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, flight);
            Assert.AreEqual(0f, Vector2.Distance(start, from), 1e-3f, "궤적이 발사점에서 시작하지 않습니다.");
            Assert.AreEqual(0f, Vector2.Distance(end, to), 1e-3f,
                "궤적이 **사전 확정된 도달점**에 정확히 도착하지 않습니다 — 이 연출은 물리 시뮬레이션이 " +
                "아니라 역산이므로 오차가 있으면 안 됩니다(리더 지시: 우연에 맡기지 마라).");

            // 현(직선)보다 위로 부푼다 = 포물선으로 보인다. 중점에서의 벗어남이 정확히 apex여야 한다.
            Vector2 mid = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, flight * 0.5f);
            float chordMidY = (from.y + to.y) * 0.5f;
            Assert.AreEqual(apex, mid.y - chordMidY, 1e-3f,
                $"궤적 중점이 현보다 {mid.y - chordMidY:F3}유닛 위입니다 — 설정한 볼록함 {apex:F3}과 달라 " +
                "포물선의 모양이 배율/거리에 따라 달라진다는 뜻입니다.");

            // 전 구간이 현보다 위(= 아래로 처지는 구간이 없다).
            for (int i = 1; i < 20; i++)
            {
                float u = i / 20f;
                Vector2 p = ArcheryRenderer.TrajectoryPoint(from, to, flight, apex, flight * u);
                float chordY = Mathf.Lerp(from.y, to.y, u);
                Assert.Greater(p.y, chordY - 1e-4f, $"t={u:F2}에서 궤적이 현 아래로 처졌습니다.");
            }

            // 볼록함 0이면 정확히 직선 — "포물선을 끄는" 경계 동작.
            Vector2 flat = ArcheryRenderer.TrajectoryPoint(from, to, flight, 0f, flight * 0.5f);
            Assert.AreEqual(chordMidY, flat.y, 1e-3f, "볼록함 0인데 직선이 아닙니다.");
        }

        // ============================================================================
        // ④-b 착탄 각도 — 궤적을 아무리 과장해도 **꽂히는 각도**는 합리적 범위 안이다
        // ============================================================================
        // ★ 2026-08-29 사용자 신고 "화살이 과녁에 좀 이상하게 꽂힘". 원인은 두 가지가 겹친 것인데
        //   그중 하나가 "비행 중의 과장된 포물선 접선을 그대로 고정해 꽂았다"이다. 아래 수치는
        //   실행 중인 빌드의 로그에서 그대로 가져온 실제 사격 조건이다
        //   (사거리 25.34유닛, 비행 1.11초, 신장 1.71, archeryArrowArcApexDistanceRatio=0.18).

        private const float RealSpan = 25.34f;      // 실측 사거리(유닛).
        private const float RealFlight = 1.11f;     // 실측 비행 시간(초).
        private const float RealApex = RealSpan * 0.18f;  // 실측 볼록함(= 4.56유닛).

        [Test]
        public void ExaggeratedArcMakesTheRawTangentAbsurdlySteep_NegativeControl()
        {
            // 네거티브 컨트롤: 보정을 되돌리면(= 접선 각도를 그대로 쓰면) 실제로 과도한 각도가 나오는가.
            var from = new Vector2(0f, 1.33f);
            var to = new Vector2(RealSpan, 1.02f);
            float tangent = ArcheryRenderer.ImpactTangentDegrees(from, to, RealFlight, RealApex);
            float descent = ArcheryRenderer.DescentDegrees(tangent);

            Assert.Greater(descent, 35f,
                $"보정 없는 접선 하강각이 {descent:F1}도뿐입니다 — 이 테스트는 '수정을 되돌리면 실제로 " +
                "과도한 각도가 나온다'를 증명하는 네거티브 컨트롤이라, 여기서 완만하면 아래 클램프 " +
                "테스트가 아무것도 증명하지 못합니다.");

            // 볼록함을 키울수록 단조적으로 더 가팔라진다(원인-결과의 방향성 확인).
            float steeper = ArcheryRenderer.DescentDegrees(
                ArcheryRenderer.ImpactTangentDegrees(from, to, RealFlight, RealApex * 2f));
            Assert.Greater(steeper, descent,
                "볼록함을 2배로 키웠는데 착탄 접선이 더 가팔라지지 않았습니다 — 인과가 성립하지 않습니다.");
        }

        [Test]
        public void SettledImpactAngleClampsTheFaceHitNearHorizontal()
        {
            const float faceMax = 14f;
            var from = new Vector2(0f, 1.33f);

            foreach (float dir in new[] { 1f, -1f })   // 좌우 미러링 — 부호를 방향마다 따로 다루면 반드시 한쪽이 틀린다.
            {
                string label = dir > 0f ? "오른쪽" : "왼쪽";
                var to = new Vector2(RealSpan * dir, 1.02f);
                float tangent = ArcheryRenderer.ImpactTangentDegrees(from, to, RealFlight, RealApex);

                Assert.AreEqual(ArcheryRenderer.DescentDegrees(
                        ArcheryRenderer.ImpactTangentDegrees(new Vector2(0f, 1.33f), new Vector2(RealSpan, 1.02f), RealFlight, RealApex)),
                    ArcheryRenderer.DescentDegrees(tangent), 1e-3f,
                    $"{label}으로 쏠 때의 하강각이 오른쪽과 다릅니다 — 좌우 미러링에서 각도 부호가 깨졌습니다.");

                float settled = ArcheryRenderer.SettledImpactAngle(tangent, faceMax, exact: false);
                float settledDescent = ArcheryRenderer.DescentDegrees(settled);
                Assert.LessOrEqual(settledDescent, faceMax + 1e-3f,
                    $"{label}: 보정 후 하강각이 {settledDescent:F1}도로 상한 {faceMax}도를 넘습니다.");
                Assert.Greater(settledDescent, 0f, $"{label}: 보정 후 화살이 코를 들거나 완전히 수평입니다.");

                // 수평 진행 방향은 절대 뒤집히지 않는다(뒤집히면 화살이 반대로 날아온 것처럼 보인다).
                Assert.AreEqual(Mathf.Sign(Mathf.Cos(tangent * Mathf.Deg2Rad)),
                    Mathf.Sign(Mathf.Cos(settled * Mathf.Deg2Rad)),
                    $"{label}: 보정이 화살의 좌우 진행 방향을 뒤집었습니다.");
            }
        }

        [Test]
        public void SettledImpactAngleNeverSteepensAnAlreadyGentleShot()
        {
            // 아주 가까운 사격은 원래도 완만하다 — 클램프가 그것을 **더 가파르게 만들면 안 된다**.
            var from = new Vector2(0f, 1.33f);
            var to = new Vector2(2.5f, 1.20f);
            float tangent = ArcheryRenderer.ImpactTangentDegrees(from, to, 0.4f, 0.2f);
            float raw = ArcheryRenderer.DescentDegrees(tangent);
            float settled = ArcheryRenderer.DescentDegrees(
                ArcheryRenderer.SettledImpactAngle(tangent, 14f, exact: false));
            Assert.LessOrEqual(settled, raw + 1e-3f,
                $"원래 {raw:F1}도로 완만하던 사격이 보정 후 {settled:F1}도로 더 가팔라졌습니다 — " +
                "클램프는 상한이지 목표값이 아닙니다.");
        }

        [Test]
        public void GroundMissUsesAnExactAngleSoDirtStuckArrowsAlwaysLookTheSame()
        {
            const float ground = 38f;
            var from = new Vector2(0f, 1.33f);
            // 사거리가 크게 달라도 땅에 박힌 모양은 같아야 한다.
            foreach (float span in new[] { 4f, 12f, RealSpan })
            {
                float tangent = ArcheryRenderer.ImpactTangentDegrees(
                    from, new Vector2(span, 0f), RealFlight, span * 0.18f);
                float d = ArcheryRenderer.DescentDegrees(
                    ArcheryRenderer.SettledImpactAngle(tangent, ground, exact: true));
                Assert.AreEqual(ground, d, 1e-3f,
                    $"사거리 {span:F1}유닛에서 땅에 꽂힌 각도가 {d:F1}도입니다 — 확정 각도 {ground}도여야 합니다.");
            }
        }

        [Test]
        public void SettleWeightIsZeroUntilTheLastStretchThenReachesOneExactlyAtImpact()
        {
            const float flight = 1.11f;
            float start = flight * (1f - 0.22f);

            Assert.AreEqual(0f, ArcheryRenderer.SettleWeight(0f, flight, start), 1e-5f,
                "발사 직후부터 각도 보정이 걸리면 비행 중의 포물선 회전이 뭉개집니다.");
            Assert.AreEqual(0f, ArcheryRenderer.SettleWeight(start, flight, start), 1e-5f,
                "보정 시작 지점에서 가중치가 0이 아닙니다 — 그 순간 각도가 툭 튑니다.");
            Assert.AreEqual(1f, ArcheryRenderer.SettleWeight(flight, flight, start), 1e-5f,
                "착탄 순간 가중치가 1이 아닙니다 — 꽂힌 각도가 설정값에 도달하지 못합니다.");

            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float w = ArcheryRenderer.SettleWeight(flight * i / 20f, flight, start);
                Assert.GreaterOrEqual(w, prev - 1e-5f, "보정 가중치가 도중에 되돌아갑니다(각도가 흔들립니다).");
                prev = w;
            }

            // 보정 비율 0 = 기능 끄기(신고된 버그 재현 경로) — 착탄 순간에도 가중치가 0이라
            // 접선 각도가 그대로 꽂힌다. 이것이 이번 신고의 재현 조건이다.
            Assert.AreEqual(0f, ArcheryRenderer.SettleWeight(flight, flight, flight), 1e-5f,
                "보정 비율을 0으로 두었는데도 각도가 보정됩니다 — 설정으로 끌 수 없다는 뜻입니다.");
        }

        [Test]
        public void ShippingConfigKeepsTheImpactAngleSane()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                Assert.Greater(cfg.archeryFaceImpactMaxDescentDegrees, 0f,
                    "과녁 면 착탄 상한이 0도면 화살이 완전히 수평으로 꽂혀 '박혔다'가 안 읽힙니다.");
                Assert.LessOrEqual(cfg.archeryFaceImpactMaxDescentDegrees, 25f,
                    $"출하 설정의 과녁 면 착탄 상한이 {cfg.archeryFaceImpactMaxDescentDegrees}도입니다 — " +
                    "이 정도면 신고된 '이상하게 꽂힘'이 그대로 돌아옵니다.");
                Assert.Greater(cfg.archeryGroundImpactDescentDegrees, cfg.archeryFaceImpactMaxDescentDegrees,
                    "땅에 꽂히는 각도가 과녁 면보다 완만합니다 — 땅에 누운 화살처럼 보입니다.");
                Assert.Greater(cfg.archeryImpactSettleRatio, 0f,
                    "착탄 각도 보정 구간이 0입니다 — 검증용으로 껐던 값이 그대로 커밋된 상태입니다.");
                Assert.LessOrEqual(cfg.archeryImpactSettleRatio, 0.4f,
                    "보정 구간이 비행의 40%를 넘습니다 — 화살이 날아가는 내내 각도가 바뀌어 포물선이 뭉개집니다.");
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        // ============================================================================
        // ⑤ 배율 — 1.0 / 0.75(현재 출하) / 0.5에서 배치가 비례한다 + 절대 조건
        // ============================================================================

        private const float BaseHeight = StickConfig.BaselineCharacterTotalHeight;
        private const float BaseHeadRadius = 0.22f;
        private const float BaseShoulderY = 1.7646944f;
        private const float BaseHipY = 0.9346944f;

        /// <summary>StickmanMetrics가 실측하는 소스만 갖춘 최소 리그(RendererScaleRatioTests와 같은 방식).
        /// 프리팹/씬은 배율 하나로 구워지므로 한 번 실행에 두 배율을 동시에 볼 수 없기 때문이다.</summary>
        private GameObject Rig(float scale)
        {
            var root = new GameObject($"ArcheryScaleRig_{scale:F2}");
            float height = BaseHeight * scale;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.4f * scale, height);
            capsule.offset = new Vector2(0f, height * 0.5f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
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
            _rigs.Add(root);
            return root;
        }

        [TestCase(1.0f)]
        [TestCase(0.75f)]
        [TestCase(0.5f)]
        public void ArcheryPlacementScalesWithCharacter(float scale)
        {
            GameObject rig = Rig(scale);
            var m = rig.GetComponent<StickmanMetrics>();
            var r = rig.AddComponent<ArcheryRenderer>();
            string label = $"배율 {scale:F2}";

            Assert.AreEqual(BaseHeight * scale, m.TotalHeight, Tol, $"{label}: 리그 신장이 기대치와 다릅니다.");

            // (A) 비율 x 신장 — 바깥에서 온 숫자(설정 기본값)와 맞댄다.
            const float radiusRatio = 0.40f;
            Assert.AreEqual(BaseHeight * scale * radiusRatio, r.TargetRadius, Tol,
                $"{label}: 과녁 반지름이 신장의 {radiusRatio:P0}가 아닙니다 — 절대 상수가 남아 있으면 " +
                "작은 캐릭터 옆에 몸통보다 큰 과녁이 서게 됩니다.");
            Assert.AreEqual(BaseHeight * scale * 0.30f, r.BowHalfLength, Tol, $"{label}: 활 길이가 비례하지 않습니다.");
            Assert.AreEqual(BaseHeight * scale * 0.34f, r.ArrowShaftLength, Tol, $"{label}: 화살 길이가 비례하지 않습니다.");
            Assert.AreEqual(BaseHeight * scale * 0.0339f, r.StrokeWidth, Tol, $"{label}: 획 두께가 비례하지 않습니다.");

            // (B) 절대 조건 — **모든 배율에서** 과녁 꼭대기가 정확히 캐릭터 정수리 높이다.
            // 이 관계가 곧 "화면 세로 판정이 캐릭터 자신의 판정과 같다"는 보증이다(ArcheryDirector 참고).
            Assert.AreEqual(m.HeadTopLocalY, r.TargetCenterLocalY + r.TargetRadius, Tol,
                $"{label}: 과녁 꼭대기({r.TargetCenterLocalY + r.TargetRadius:F4})가 정수리" +
                $"({m.HeadTopLocalY:F4})와 다릅니다 — 캐릭터가 보이는데 과녁만 화면 밖으로 잘릴 수 있습니다.");
            Assert.Greater(r.TargetCenterLocalY - r.TargetRadius, 0f,
                $"{label}: 과녁 아래 끝이 지면 아래로 내려갑니다(받침이 땅에 파묻힙니다).");

            // 궤적 볼록함도 신장에 비례한다.
            Assert.AreEqual(BaseHeight * scale * 0.38f, r.ArcApexHeight, Tol,
                $"{label}: 포물선 볼록함이 비례하지 않습니다 — 작은 캐릭터에서 궤적이 상대적으로 " +
                "훨씬 높이 솟거나 납작해집니다.");
        }

        /// <summary>네거티브 컨트롤 — 종전 방식대로 <b>절대 유닛 상수</b>를 썼다면 위 (B) 절대 조건이
        /// 실제로 깨진다는 것을 같은 식으로 계산해 보인다. 즉 (B)가 통과하는 이유가 "조건이 너무
        /// 헐거워서"가 아님을 같은 파일 안에서 증명한다.</summary>
        [Test]
        public void AbsoluteSizeWouldBreakScaleInvariant()
        {
            // 배율 1.0에서 검증을 마친 값들을 그대로 절대 상수로 굳혔다고 가정.
            const float frozenRadius = BaseHeight * 0.40f;
            const float frozenCenterY = BaseHeight - frozenRadius;

            const float scale = 0.5f;
            float height = BaseHeight * scale;
            float top = frozenCenterY + frozenRadius; // = BaseHeight — 배율과 무관하게 고정.

            Assert.Greater(Mathf.Abs(top - height), 0.05f,
                "절대 상수를 써도 과녁 꼭대기가 정수리와 맞는다면, 위 배율 테스트는 아무것도 " +
                "검증하지 못하고 있다는 뜻입니다.");
            Assert.Greater(top, height,
                $"배율 {scale:F2}에서 절대 상수 과녁의 꼭대기 {top:F3}이 캐릭터 정수리 {height:F3}보다 " +
                "위에 있어야 합니다(= 몸통보다 큰 과녁이 머리 위로 솟는 그림).");
        }

        // ============================================================================
        // ⑥ 화면 밖으로 나가면 안 된다 — 정면 -> 미러링 -> 발동 포기
        // ============================================================================

        /// <summary>
        /// 발판 종류 판정 — 화면 최하단 안전망(합성 발판)만 "바탕화면"이고, Dock과 실제 창은 둘 다
        /// "창"이다(사용자 명시: "창 일 경우 그 창의 전체 길이의 끝으로 이동"). 이 분류가 뒤집히면
        /// 바탕화면에서 짧게 쏘거나 좁은 창에서 화면 절반을 요구해 발동 자체가 사라진다.
        /// </summary>
        [Test]
        public void FootholdKindClassificationMatchesUserSpec()
        {
            Assert.IsFalse(ArcheryDirector.IsRealWindowFoothold(FallbackPlatformWindowService.SyntheticFootholdHandle),
                "화면 최하단 왼쪽 안전망을 '창'으로 분류했습니다 — 바탕화면이어야 합니다.");
            Assert.IsFalse(ArcheryDirector.IsRealWindowFoothold(FallbackPlatformWindowService.SyntheticFootholdHandleRight),
                "화면 최하단 오른쪽 안전망을 '창'으로 분류했습니다 — 바탕화면이어야 합니다.");
            Assert.IsTrue(ArcheryDirector.IsRealWindowFoothold(FallbackPlatformWindowService.DockFootholdHandle),
                "Dock을 '창'으로 분류하지 않았습니다 — 사용자가 Dock을 창에 포함시켰습니다.");
            Assert.IsTrue(ArcheryDirector.IsRealWindowFoothold(12345L),
                "실제 창 핸들을 '창'으로 분류하지 않았습니다.");
            Assert.IsFalse(ArcheryDirector.IsRealWindowFoothold(0L),
                "'발판 없음'(핸들 0)을 창으로 분류했습니다.");
        }

        /// <summary>과녁 중심 높이가 반지름에서 유도된다는 관계(설정값 이중화 금지)를 못박는다.</summary>
        [TestCase(1.0f)]
        [TestCase(0.75f)]
        [TestCase(0.5f)]
        public void TargetCenterHeightIsDerivedFromRadius(float scale)
        {
            float height = BaseHeight * scale;
            float radius = height * 0.40f;
            Assert.AreEqual(height - radius, ArcheryDirector.TargetCenterHeight(height, radius), Tol,
                "과녁 중심 높이가 '신장 - 반지름'이 아닙니다 — 이 관계가 깨지면 과녁 꼭대기가 " +
                "정수리와 어긋나고, 디렉터의 세로 화면 판정도 함께 틀어집니다.");
        }
    }
}
