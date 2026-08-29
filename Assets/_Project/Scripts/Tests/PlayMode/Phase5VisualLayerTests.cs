using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ Phase 5 시각 레이어(스트레스 게이지 / 가출 / 투두 / 포모도로 감시자) 회귀 테스트.
    ///
    /// ============================================================================
    /// 왜 Main.unity를 실제로 로드하는가
    /// ============================================================================
    /// 이 프로젝트가 여섯 번 반복한 실패 모드는 "로직은 완성됐는데 아무도 구독/배치를 안 해서 화면에
    /// 한 픽셀도 안 나온다"이다. 컴포넌트를 테스트 안에서 새로 만들어 검사하면 그 실패 모드를
    /// <b>구조적으로 놓친다</b> — 씬에 배치돼 있는지 자체가 검사 대상이어야 한다. 그래서 창 도둑/
    /// 크래시/하드웨어 반응 테스트와 같은 관례로 실제 씬을 로드한다.
    ///
    /// ============================================================================
    /// 절대 조건으로 단언하는 것(상대 마진 방식 금지 — 이 프로젝트는 그 방식이 버그를 2라운드 연속
    /// 놓친 전례가 있다)
    /// ============================================================================
    ///  ① 씬에 Phase 5 컴포넌트 9종이 <b>정확히 1개씩</b> 있다(0=배치 누락, 2=라이벌 복제).
    ///  ② 이벤트를 발행하면 시각 오브젝트가 <b>실제로 씬에 생기고</b>, 끝나면 컨테이너 GameObject가
    ///     <b>씬에서 실제로 사라진다</b>(개수가 0인 것으로 만족하지 않고 GameObject.Find로 확인).
    ///  ③ 클릭 대상은 가출의 [간식 주기] 과자 하나뿐이다 — 다른 연출의 콜라이더는 정확히 0개
    ///     (관전 전용 = 클릭관통 유지, CLAUDE.md 불변 원칙 2).
    ///  ④ <b>리더가 "한 번도 검증된 적 없다"고 지목한 항목 2개</b>를 직접 실측한다:
    ///     (a) TodoPostItWidget의 uGUI 클릭이 실제 GraphicRaycaster를 통해 발동하는가,
    ///     (b) 가출 은신 중에도 캐릭터 Collider2D가 물리 쿼리에 잡혀 "안 보이는 캐릭터 클릭"이
    ///         재발동하는가.
    /// </summary>
    public sealed class Phase5VisualLayerTests
    {
        private const string StressContainerName = "StressMoodOverlay";
        private const string RunawayContainerName = "RunawayOverlay";
        private const string FocusContainerName = "FocusWatchRing";
        private const string TodoPaperContainerName = "TodoReminderPaper";

        private StickmanAgent _agent;
        private StressGaugeRenderer _stressRenderer;
        private RunawayRenderer _runawayRenderer;
        private RunawayDirector _runawayDirector;
        private TodoPostItWidget _postIt;
        private TodoReminderDirector _todoDirector;
        private FocusWatchDirector _focusDirector;
        private FocusWatchRenderer _focusRenderer;

        private IEnumerator LoadSceneAndResolve()
        {
            // 정적 전역 상태는 씬 생명주기와 무관하게 살아남는다(Core/StressGauge.cs, SpectacleEventLock).
            // 테스트 간 누수를 막기 위해 매번 초기화한다.
            StressGauge.ResetForTesting();
            TodoListModel.ResetForTesting();
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = ExactlyOne<StickmanAgent>();
            _stressRenderer = ExactlyOne<StressGaugeRenderer>();
            _runawayRenderer = ExactlyOne<RunawayRenderer>();
            _runawayDirector = ExactlyOne<RunawayDirector>();
            _postIt = ExactlyOne<TodoPostItWidget>();
            _todoDirector = ExactlyOne<TodoReminderDirector>();
            _focusDirector = ExactlyOne<FocusWatchDirector>();
            _focusRenderer = ExactlyOne<FocusWatchRenderer>();
        }

        private static T ExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락(이 컴포넌트가 단 한 번도 실행되지 않는다), " +
                "2개 이상이면 라이벌 복제본에서 제거되지 않아 같은 전역 이벤트에 두 번 반응합니다.");
            return found[0];
        }

        // ================================================================================
        // ① 배선 자체 — 이 프로젝트의 반복된 실패 모드를 정면으로 잠근다.
        // ================================================================================

        [UnityTest]
        public IEnumerator EveryPhase5ComponentIsPlacedExactlyOnce()
        {
            yield return LoadSceneAndResolve();

            // LoadSceneAndResolve가 8개를 이미 확인했고, 자체 필드가 없는 나머지 1개도 여기서 확인한다.
            var todoPaper = ExactlyOne<TodoReminderRenderer>();
            var stressDirector = ExactlyOne<StressGaugeDirector>();

            Assert.IsNotNull(todoPaper);
            Assert.IsNotNull(stressDirector);

            // uGUI 클릭의 전제 조건. 이것이 없으면 Button.onClick은 영원히 발동하지 않는다 —
            // 포스트잇 체크박스가 한 번도 눌리지 않았던 원인의 절반이 정확히 이것이었다.
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            Assert.AreEqual(1, eventSystems.Length,
                $"씬의 EventSystem 개수가 {eventSystems.Length}개입니다 — 정확히 1개여야 합니다.");
            Assert.IsNotNull(eventSystems[0].GetComponent<BaseInputModule>(),
                "씬 EventSystem에 입력 모듈(StandaloneInputModule)이 없습니다 — 입력 모듈이 없는 " +
                "EventSystem은 포인터 이벤트를 아예 처리하지 않으므로 uGUI Button.onClick이 영원히 " +
                "발동하지 않습니다(투두 포스트잇 체크박스가 죽습니다).");

            Debug.Log("[Phase5테스트] 배선 검증 통과 — Phase 5 컴포넌트 9종이 정확히 1개씩, " +
                "EventSystem 1개 + 입력 모듈 존재.");
        }

        // ================================================================================
        // ② 스트레스 게이지(19절) — 어깨 처짐이 실제로 그려지고 실제로 사라진다.
        // ================================================================================

        [UnityTest]
        public IEnumerator StressTierChangeDrawsMoodAndFullyCleansUp()
        {
            yield return LoadSceneAndResolve();

            Assert.IsFalse(_stressRenderer.IsVisible, "시작 시점에는 기분 표시가 떠 있으면 안 됩니다.");
            Assert.IsNull(GameObject.Find(StressContainerName),
                $"시작 시점에 '{StressContainerName}'이(가) 이미 씬에 있습니다.");
            Assert.AreEqual(StressMoodTier.Calm, _stressRenderer.CurrentTier);

            StickConfig config = _agent.Config;
            float caution = config.stressTierCautionLevel;
            float alarm = config.stressSulkyThreshold;

            // --- 주의 단계 ---
            StressGauge.SetLevel(caution + 0.01f);
            yield return null;

            Assert.AreEqual(StressMoodTier.Caution, _stressRenderer.CurrentTier,
                $"게이지를 주의 경계({caution:F2}) 위로 올렸는데 단계가 바뀌지 않았습니다.");
            Assert.IsTrue(_stressRenderer.IsVisible, "주의 단계인데 기분 표시가 떠 있지 않습니다.");
            Assert.Greater(_stressRenderer.ActiveVisualCount, 0,
                "기분 표시가 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기).");
            Assert.AreEqual(0, _stressRenderer.ActiveColliderCount,
                "기분 표시가 콜라이더를 만들었습니다 — 관전 전용 연출이므로 클릭관통이 유지되어야 합니다.");
            Assert.IsNotNull(GameObject.Find(StressContainerName),
                $"'{StressContainerName}' GameObject가 씬에 실존하지 않습니다.");
            int cautionVisuals = _stressRenderer.ActiveVisualCount;

            // --- 경고 단계(도형이 더 늘어난다: 굽은 등 한 획 추가) ---
            StressGauge.SetLevel(alarm + 0.01f);
            yield return null;

            Assert.AreEqual(StressMoodTier.Alarm, _stressRenderer.CurrentTier,
                $"게이지를 경고 경계({alarm:F2}) 위로 올렸는데 단계가 바뀌지 않았습니다.");
            Assert.Greater(_stressRenderer.ActiveVisualCount, cautionVisuals,
                "경고 단계인데 주의 단계보다 도형이 늘지 않았습니다 — 19절의 '점진적 비주얼 변화'가 " +
                "단계별로 실제로 달라지지 않는다는 뜻입니다(같은 그림에 색만 바뀌면 유저가 구분할 수 없습니다).");

            // 컨테이너는 항상 1개여야 한다(단계가 바뀔 때 이전 것이 남아 겹치면 안 됨).
            Assert.AreEqual(1, CountRootObjectsNamed(StressContainerName),
                $"'{StressContainerName}' 컨테이너가 여러 개 존재합니다 — 단계 교체 시 이전 도형이 지워지지 않았습니다.");

            // --- 정상 복귀 ---
            StressGauge.SetLevel(0f);
            yield return new WaitForSeconds(2.8f); // FadeOutSeconds(1.1초) + 한숨 퍼프 수명(1.5초) + 여유.

            Assert.AreEqual(StressMoodTier.Calm, _stressRenderer.CurrentTier);
            Assert.AreEqual(0, _stressRenderer.ActiveVisualCount,
                $"정상 복귀 후에도 시각 오브젝트가 {_stressRenderer.ActiveVisualCount}개 남아 있습니다.");
            Assert.IsNull(GameObject.Find(StressContainerName),
                $"'{StressContainerName}' GameObject가 씬에 그대로 남아 있습니다(화면에 영구히 남습니다).");

            Debug.Log($"[Phase5테스트] 스트레스 검증 통과 — 주의 {cautionVisuals}개 -> 경고 " +
                "더 많은 도형 -> 정상 복귀 시 전부 소멸, 콜라이더 0개.");
        }

        // ================================================================================
        // ③ 가출(20절) — 페이즈별 연출 + [간식 주기] 과자가 유일한 클릭 대상
        // ================================================================================

        [UnityTest]
        public IEnumerator RunawayLifecycleDrawsPhasesAndSnackIsTheOnlyClickTarget()
        {
            yield return LoadSceneAndResolve();

            Assert.IsFalse(_runawayRenderer.IsActive, "시작 시점에는 가출 연출이 진행 중이면 안 됩니다.");
            Assert.AreEqual(0, _runawayRenderer.ActiveColliderCount);

            // --- Fleeing: 속도선/먼지가 실제로 난다 ---
            StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Fleeing, default);
            yield return null;
            yield return null;

            Assert.IsTrue(_runawayRenderer.IsActive, "Fleeing을 발행했는데 연출이 시작되지 않았습니다.");
            Assert.AreEqual(RunawayLifecyclePhase.Fleeing, _runawayRenderer.VisiblePhase);
            Assert.IsNotNull(GameObject.Find(RunawayContainerName),
                $"'{RunawayContainerName}' GameObject가 씬에 실존하지 않습니다.");
            Assert.IsFalse(_runawayRenderer.IsSnackOffered,
                "Fleeing 단계에서 과자가 떠 있습니다 — 과자는 발견(Found) 이후에만 나와야 합니다(20절).");
            Assert.AreEqual(0, _runawayRenderer.ActiveColliderCount,
                "Fleeing 연출이 콜라이더를 만들었습니다 — 속도선/먼지는 관전 전용이라 클릭관통이어야 합니다.");
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(_runawayRenderer.ActiveVisualCount, 0,
                "Fleeing인데 시각 오브젝트가 0개입니다 — 속도선/먼지가 하나도 생성되지 않았습니다.");

            // --- Hidden + 힌트 파문 ---
            Vector2 hideSpotOs = new Vector2(60f, 60f);
            StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Hidden, hideSpotOs);
            yield return null;
            Assert.AreEqual(RunawayLifecyclePhase.Hidden, _runawayRenderer.VisiblePhase);
            Assert.IsFalse(_runawayRenderer.IsSnackOffered, "은신 중에는 과자가 있으면 안 됩니다.");

            int pulsesBefore = _runawayRenderer.HintPulseCount;
            StickmanEventBus.RaiseRunawayHintPulseRequested(hideSpotOs);
            yield return null;

            Assert.AreEqual(pulsesBefore + 1, _runawayRenderer.HintPulseCount,
                "RunawayHintPulseRequested를 발행했는데 힌트 파문이 그려지지 않았습니다 — " +
                "20절이 요구한 '은은한 단서'가 없으면 유저는 캐릭터를 영원히 못 찾습니다.");
            Assert.Greater(_runawayRenderer.ActiveVisualCount, 0, "힌트 파문 도형이 하나도 없습니다.");
            Assert.AreEqual(0, _runawayRenderer.ActiveColliderCount,
                "힌트 파문이 콜라이더를 만들었습니다 — 은신처를 클릭으로 찾는 판정은 캐릭터 자신의 " +
                "콜라이더가 담당해야 하고(20절), 파문은 순수 시각 신호여야 합니다.");

            // --- Found: 과자가 정확히 1개의 클릭 대상을 만든다 ---
            StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Found, hideSpotOs);
            yield return null;

            Assert.AreEqual(RunawayLifecyclePhase.Found, _runawayRenderer.VisiblePhase);
            Assert.IsTrue(_runawayRenderer.IsSnackOffered,
                "발견됐는데 [간식 주기] 과자가 나오지 않았습니다 — 20절이 요구한 화해 경로가 사라집니다.");
            Assert.AreEqual(1, _runawayRenderer.ActiveColliderCount,
                $"Found 단계의 클릭 대상 콜라이더가 {_runawayRenderer.ActiveColliderCount}개입니다 — " +
                "정확히 1개(과자)여야 합니다. 0개면 과자를 클릭할 수 없고, 2개 이상이면 관전 전용이어야 할 " +
                "연출까지 클릭관통을 깨뜨리고 있다는 뜻입니다.");

            // --- Reconciled: 과자가 사라지고, 잔여 파티클이 다 죽으면 컨테이너까지 소멸 ---
            StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Reconciled, default);
            yield return null;

            Assert.IsFalse(_runawayRenderer.IsSnackOffered, "화해했는데 과자가 그대로 남아 있습니다.");
            Assert.AreEqual(0, _runawayRenderer.ActiveColliderCount,
                "화해 후에도 클릭 대상 콜라이더가 남아 있습니다 — 그 영역의 클릭관통이 영구히 깨집니다.");

            yield return new WaitForSeconds(1.6f); // 화해 반짝임 수명(1.0초) + 여유.

            Assert.IsFalse(_runawayRenderer.IsActive, "종결 페이즈 후에도 연출이 진행 중이라고 보고합니다.");
            Assert.AreEqual(0, _runawayRenderer.ActiveVisualCount,
                $"정리 후에도 시각 오브젝트가 {_runawayRenderer.ActiveVisualCount}개 남아 있습니다.");
            Assert.IsNull(GameObject.Find(RunawayContainerName),
                $"'{RunawayContainerName}' GameObject가 씬에 그대로 남아 있습니다.");

            Debug.Log("[Phase5테스트] 가출 검증 통과 — Fleeing/Hidden/힌트파문/Found/Reconciled 전 페이즈가 " +
                "실제 오브젝트를 만들고, 클릭 대상은 Found의 과자 1개뿐이며, 종료 시 전부 소멸.");
        }

        /// <summary>
        /// ★ 리더가 "아직 한 번도 검증된 적 없다"고 지목한 항목 (b) — 은신 중 캐릭터 클릭 재발동.
        ///
        /// States/RunawayState.cs는 은신 중 Rigidbody2D를 <c>simulated=false</c>가 아니라
        /// <b>Kinematic</b>으로 바꾼다고 문서화하고 있고, 그 이유가 정확히 "simulated=false면 콜라이더가
        /// 물리 쿼리 대상에서 빠져 클릭으로 찾을 수 없게 된다"는 것이다. 그 주장이 실제로 성립하는지를
        /// 여기서 물리 쿼리로 직접 확인한다 — 렌더러는 꺼졌는데 콜라이더는 그 자리에 살아 있어야 한다.
        /// </summary>
        [UnityTest]
        public IEnumerator HiddenRunawayCharacterStaysClickableAndRevealsOnHitboxDown()
        {
            yield return LoadSceneAndResolve();

            // 확정 임계값을 건너뛰는 정식 데모 경로로 진입한다(테스트가 상태머신을 직접 밀어넣지 않는다 —
            // Director의 진입 조건/락 획득까지 함께 검증하기 위해서다).
            yield return WaitUntilIdleOrWalk();
            _runawayDirector.ForceTriggerNow("PlayMode 테스트");
            yield return null;

            var machine = _agent.Blackboard.Machine;
            Assert.AreEqual(StickmanStateId.Runaway, machine.CurrentStateId,
                "ForceTriggerNow를 호출했는데 Runaway 상태로 전이하지 않았습니다.");

            // Fleeing -> Hidden 전환을 기다린다(runawayFleeDurationSeconds + 여유).
            float flee = _agent.Config.runawayFleeDurationSeconds;
            yield return new WaitForSeconds(flee + 0.5f);

            Assert.AreEqual(RunawayLifecyclePhase.Hidden, _runawayRenderer.VisiblePhase,
                "뛰어가는 시간이 지났는데 은신(Hidden) 페이즈로 넘어가지 않았습니다.");
            Assert.IsTrue(_agent.Blackboard.IsCharacterHiddenByRunaway,
                "은신 중인데 IsCharacterHiddenByRunaway 플래그가 서지 않았습니다.");

            // (1) 렌더러가 실제로 꺼져 있는가 = 화면에서 사라졌는가.
            var lineRenderers = _agent.GetComponentsInChildren<LineRenderer>(true);
            Assert.Greater(lineRenderers.Length, 0, "캐릭터에 LineRenderer가 하나도 없습니다(사전 조건 불성립).");
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                Assert.IsFalse(lineRenderers[i].enabled,
                    $"은신 중인데 캐릭터 LineRenderer '{lineRenderers[i].name}'이(가) 켜져 있습니다 — 숨지 않았습니다.");
            }

            // (2) ★ 핵심 — 콜라이더는 여전히 물리 쿼리에 잡히는가.
            //     StickmanClickHitbox의 두 입력 경로(Unity OnMouseDown / 전역 폴링 + Collider2D.OverlapPoint)가
            //     모두 이 성질에 의존한다. Kinematic 대신 simulated=false를 썼다면 여기서 실패한다.
            Vector2 bodyPos = _agent.Blackboard.Body.position;
            Collider2D hit = Physics2D.OverlapPoint(bodyPos);
            Assert.IsNotNull(hit,
                $"은신처 좌표 {bodyPos}에서 어떤 콜라이더도 잡히지 않았습니다 — 캐릭터가 물리 쿼리에서 " +
                "사라졌다는 뜻이고, 그러면 20절의 '화면 구석을 클릭해 찾는다'가 구조적으로 불가능해집니다.");
            Assert.IsTrue(hit.transform.IsChildOf(_agent.transform) || hit.transform == _agent.transform,
                $"은신처에서 잡힌 콜라이더 '{hit.name}'이(가) 캐릭터 계층이 아닙니다.");
            Assert.AreEqual(RigidbodyType2D.Kinematic, _agent.Blackboard.Body.bodyType,
                "은신 중 Rigidbody2D가 Kinematic이 아닙니다 — States/RunawayState.cs가 문서화한 전제가 깨졌습니다.");
            Assert.IsTrue(_agent.Blackboard.Body.simulated,
                "은신 중 Rigidbody2D.simulated가 꺼졌습니다 — 콜라이더가 물리 쿼리에서 제외될 수 있습니다.");

            // (3) 그 클릭이 실제로 '발견'으로 이어지는가. StickmanClickHitbox.MouseDown을 그대로 울려
            //     모든 구독자(RunawayDirector 포함)를 실제 클릭과 동일하게 통과시킨다 — 배치 모드에는
            //     마우스가 존재하지 않으므로(FallbackPlatformWindowService가 커서 없음을 정직하게 보고),
            //     커서 좌표 판정 부분만 위 (2)의 물리 쿼리로 대신 확인한 셈이다.
            var hitbox = _agent.GetComponent<StickmanClickHitbox>();
            Assert.IsNotNull(hitbox, "캐릭터에 StickmanClickHitbox가 없습니다.");
            RaiseHitboxMouseDown(hitbox);
            yield return null;
            yield return null;

            Assert.AreEqual(RunawayLifecyclePhase.Found, _runawayRenderer.VisiblePhase,
                "은신 중 캐릭터 히트박스 MouseDown이 울렸는데 발견(Found)으로 넘어가지 않았습니다 — " +
                "RunawayDirector가 히트박스를 구독하지 않았거나 페이즈 판정이 어긋났습니다.");
            Assert.IsFalse(_agent.Blackboard.IsCharacterHiddenByRunaway,
                "발견됐는데 숨김 플래그가 그대로입니다.");
            Assert.IsTrue(_runawayRenderer.IsSnackOffered, "발견됐는데 [간식 주기] 과자가 나오지 않았습니다.");

            Debug.Log($"[Phase5테스트] 은신 중 클릭 재발동 검증 통과 — 은신처 {bodyPos}에서 콜라이더 " +
                $"'{hit.name}'이(가) 물리 쿼리에 잡히고(Kinematic/simulated=true), MouseDown이 Found로 이어짐.");
        }

        /// <summary>
        /// field-like event <c>StickmanClickHitbox.MouseDown</c>의 백킹 필드를 그대로 호출한다.
        /// "실제 클릭이 발생했을 때 구독자들이 하는 일"을 한 줄도 우회하지 않고 그대로 통과시키기
        /// 위한 것이며, 배치 모드에 마우스 커서 자체가 없어 실제 OnMouseDown을 낼 방법이 없다는
        /// 제약 때문이다(Platform/FallbackPlatformWindowService.cs — 배치 모드에서는 커서 없음을
        /// 정직하게 false로 보고한다).
        /// </summary>
        private static void RaiseHitboxMouseDown(StickmanClickHitbox hitbox)
        {
            FieldInfo field = typeof(StickmanClickHitbox).GetField("MouseDown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "StickmanClickHitbox.MouseDown 백킹 필드를 찾지 못했습니다.");
            var handler = field.GetValue(hitbox) as System.Action;
            Assert.IsNotNull(handler,
                "StickmanClickHitbox.MouseDown 구독자가 0명입니다 — 캐릭터를 클릭해도 아무 일도 일어나지 않습니다.");
            handler.Invoke();
        }

        // ================================================================================
        // ④ 투두(17절) — 포스트잇 카드 + uGUI 클릭이 실제로 발동하는가
        // ================================================================================

        /// <summary>
        /// ★ 리더가 "아직 한 번도 검증된 적 없다"고 지목한 항목 (a).
        ///
        /// 가짜로 <c>Button.onClick.Invoke()</c>를 부르면 아무것도 증명하지 못한다(그건 그냥 델리게이트
        /// 호출이다). 여기서는 <b>실제 EventSystem의 GraphicRaycaster</b>에 체크박스의 화면 좌표를
        /// 던져서, 레이캐스트가 그 체크박스를 실제로 맞히는지부터 확인하고, 맞은 오브젝트에
        /// <c>ExecuteEvents</c>로 포인터 클릭을 흘려보낸다 — uGUI 파이프라인 전체를 통과시키는 것이다.
        /// </summary>
        [UnityTest]
        public IEnumerator TodoPostItCheckboxIsActuallyClickableThroughUguiRaycast()
        {
            yield return LoadSceneAndResolve();

            // 사전 조건(민감도 확인): 할일이 0건이면 17절 "빈 상태 예외"로 카드가 숨겨져 있어야 한다.
            RectTransform panel = FindPostItPanel();
            Assert.IsNotNull(panel, "포스트잇 패널(PostItPanel)을 찾지 못했습니다 — 위젯이 UI를 만들지 않았습니다.");
            Assert.IsFalse(panel.gameObject.activeSelf,
                "할일이 0건인데 포스트잇 카드가 떠 있습니다 — 17절의 '빈 상태 예외'가 지켜지지 않았습니다.");

            // 할일 추가 경로. 이 경로가 생기기 전에는 TodoListModel.Add 호출자가 프로젝트 전체에 0건이라
            // 카드가 구조적으로 절대 뜨지 않았다.
            yield return WaitUntilIdleOrWalk();
            _todoDirector.ForceTriggerNow("PlayMode 테스트");
            yield return null;

            Assert.Greater(TodoListModel.UncompletedCount, 0,
                "ForceTriggerNow를 호출했는데 할일이 하나도 추가되지 않았습니다.");
            Assert.IsTrue(panel.gameObject.activeSelf,
                "할일을 추가했는데 포스트잇 카드가 여전히 숨겨져 있습니다.");

            // 클릭관통 차단막이 실제로 켜졌는가 — 이것이 없으면 OS가 클릭을 이 창까지 보내지 않는다.
            var blocker = GameObject.Find("TodoPostItClickBlocker");
            Assert.IsNotNull(blocker, "클릭관통 차단막(TodoPostItClickBlocker)이 씬에 없습니다.");
            var blockerCollider = blocker.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(blockerCollider);
            Assert.IsTrue(blockerCollider.enabled,
                "카드가 보이는데 클릭관통 차단막이 꺼져 있습니다 — UniWindowController의 Raycast 히트테스트가 " +
                "카드 위를 계속 '관통'으로 판정해 클릭이 밑의 다른 앱으로 샙니다(체크박스가 영원히 안 눌립니다).");
            Assert.IsTrue(blockerCollider.isTrigger,
                "차단막이 isTrigger가 아닙니다 — 캐릭터가 포스트잇에 물리적으로 부딪히게 됩니다.");

            // 첫 행 체크박스의 화면 좌표를 구해 진짜 GraphicRaycaster에 던진다.
            Transform rows = panel.Find("Rows");
            Assert.IsNotNull(rows, "행 컨테이너(Rows)를 찾지 못했습니다.");
            RectTransform firstRow = null;
            for (int i = 0; i < rows.childCount; i++)
            {
                Transform child = rows.GetChild(i);
                if (child.name == "TodoRow" && child.gameObject.activeSelf) { firstRow = (RectTransform)child; break; }
            }
            Assert.IsNotNull(firstRow, "표시된 할일 행이 하나도 없습니다.");

            var corners = new Vector3[4];
            firstRow.GetWorldCorners(corners); // ScreenSpaceOverlay -> 스크린 픽셀.
            var center = new Vector2((corners[0].x + corners[2].x) * 0.5f, (corners[0].y + corners[2].y) * 0.5f);

            Assert.IsNotNull(EventSystem.current, "EventSystem.current가 null입니다.");
            var pointer = new PointerEventData(EventSystem.current) { position = center };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);

            Assert.Greater(results.Count, 0,
                $"체크박스 중심 화면좌표 {center}에 uGUI 레이캐스트를 던졌는데 아무것도 맞지 않았습니다 — " +
                "GraphicRaycaster가 없거나 캔버스가 화면 밖입니다(클릭이 절대 도달하지 못합니다).");

            GameObject target = results[0].gameObject;
            Assert.IsTrue(target.transform.IsChildOf(firstRow) || target.transform == firstRow.transform,
                $"레이캐스트가 맞힌 오브젝트가 '{target.name}'으로, 클릭하려던 할일 행 밖입니다 — " +
                "다른 UI가 체크박스를 가리고 있습니다.");

            int targetId = TodoListModel.ActiveItems[0].Id;
            bool before = TodoListModel.ActiveItems[0].Completed;

            // 실제 uGUI 클릭 파이프라인(Button은 IPointerClickHandler)을 그대로 통과시킨다.
            var clicked = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler);
            Assert.IsNotNull(clicked,
                "포인터 클릭을 흘려보냈는데 아무 핸들러도 처리하지 않았습니다 — 체크박스 Button이 " +
                "레이캐스트 대상이 아니거나 상호작용이 꺼져 있습니다.");

            yield return null;

            TodoItem item = FindTodoById(targetId);
            Assert.IsNotNull(item, $"토글 대상 항목 #{targetId}이(가) 목록에서 사라졌습니다.");
            Assert.AreNotEqual(before, item.Completed,
                $"체크박스를 실제 uGUI 레이캐스트 경로로 클릭했는데 항목 #{targetId}의 완료 상태가 " +
                $"{before} 그대로입니다 — Button.onClick이 발동하지 않았습니다(입력 모듈/레이캐스터 문제).");

            Debug.Log($"[Phase5테스트] 포스트잇 uGUI 클릭 검증 통과 — 화면좌표 {center}에서 레이캐스트가 " +
                $"'{target.name}'을 맞혔고, 포인터 클릭이 '{clicked.name}'에서 처리되어 항목 #{targetId}의 " +
                $"완료 상태가 {before} -> {item.Completed}로 실제로 바뀌었다.");
        }

        /// <summary>17절 "들고 다니는 모드" — 리마인더 상태 전이에서 손에 든 종이가 실제로 그려진다.</summary>
        [UnityTest]
        public IEnumerator TodoReminderStateDrawsHeldPaperAndCleansUp()
        {
            yield return LoadSceneAndResolve();

            yield return WaitUntilIdleOrWalk();
            _todoDirector.ForceTriggerNow("PlayMode 테스트");
            yield return null;

            var machine = _agent.Blackboard.Machine;
            Assert.AreEqual(StickmanStateId.TodoReminder, machine.CurrentStateId,
                "ForceTriggerNow를 호출했는데 TodoReminder 상태로 전이하지 않았습니다.");

            var paper = ExactlyOne<TodoReminderRenderer>();
            Assert.IsTrue(paper.IsVisible, "TodoReminder 상태인데 종이가 그려지지 않았습니다.");
            Assert.Greater(paper.ActiveVisualCount, 0, "종이가 '보인다'고 보고하면서 LineRenderer는 0개입니다.");
            Assert.AreEqual(0, paper.ActiveColliderCount,
                "종이가 콜라이더를 만들었습니다 — 종이는 관전 전용이라 클릭관통이어야 합니다.");
            Assert.IsNotNull(GameObject.Find(TodoPaperContainerName),
                $"'{TodoPaperContainerName}' GameObject가 씬에 실존하지 않습니다.");

            // 홀드 시간 만료 -> Idle 복귀 -> 접어 넣기 -> 완전 소멸.
            yield return new WaitForSeconds(_agent.Config.todoReminderHoldSeconds + 1.0f);

            Assert.IsFalse(paper.IsVisible, "리마인더가 끝났는데 종이가 그대로 들려 있습니다.");
            Assert.AreEqual(0, paper.ActiveVisualCount, "정리 후에도 종이 도형이 남아 있습니다.");
            Assert.IsNull(GameObject.Find(TodoPaperContainerName),
                $"'{TodoPaperContainerName}' GameObject가 씬에 그대로 남아 있습니다.");

            Debug.Log("[Phase5테스트] 투두 들고 다니는 모드 검증 통과 — 종이 생성 후 접어 넣고 완전 소멸.");
        }

        // ================================================================================
        // ⑤ 포모도로 감시자(18절) — 타이머 링 + 경고 단계 연출
        // ================================================================================

        [UnityTest]
        public IEnumerator FocusSessionDrawsTimerRingAndTierVisualsThenCleansUp()
        {
            yield return LoadSceneAndResolve();

            Assert.IsFalse(_focusRenderer.IsRingVisible, "시작 시점에는 타이머 링이 떠 있으면 안 됩니다.");
            Assert.IsNull(GameObject.Find(FocusContainerName),
                $"시작 시점에 '{FocusContainerName}'이(가) 이미 씬에 있습니다.");

            yield return WaitUntilIdleOrWalk();
            _focusDirector.ForceTriggerNow("PlayMode 테스트");
            yield return null;
            yield return null;

            Assert.IsTrue(_focusDirector.IsSessionActive, "집중 모드가 시작되지 않았습니다.");
            Assert.Greater(_focusDirector.SessionDurationSeconds, 0f,
                "세션 총 길이가 0입니다 — 링의 남은 시간 비율을 계산할 수 없습니다.");
            Assert.IsTrue(_focusRenderer.IsRingVisible,
                "집중 모드가 켜졌는데 타이머 링이 나타나지 않았습니다 — 18절이 명시한 유일한 상시 UI입니다.");
            Assert.Greater(_focusRenderer.ActiveVisualCount, 0,
                "링이 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기).");
            Assert.AreEqual(0, _focusRenderer.ActiveColliderCount,
                "타이머 링이 콜라이더를 만들었습니다 — 관전 전용 연출이므로 클릭관통이 유지되어야 합니다.");
            Assert.IsNotNull(GameObject.Find(FocusContainerName),
                $"'{FocusContainerName}' GameObject가 씬에 실존하지 않습니다.");

            int ringOnlyVisuals = _focusRenderer.ActiveVisualCount;
            Assert.AreEqual(FocusWatchTier.None, _focusRenderer.CurrentTier);

            // 남은 시간 비율이 실제로 줄어드는가(링이 정지 화면이 아닌가).
            float ratioBefore = _focusRenderer.RemainingRatio;
            yield return new WaitForSeconds(0.6f);
            Assert.Less(_focusRenderer.RemainingRatio, ratioBefore,
                $"0.6초가 지났는데 남은 시간 비율이 {ratioBefore:F4} 그대로입니다 — 링이 줄어들지 않습니다.");

            // 1단계(곁눈질) — 도형이 실제로 늘어난다.
            StickmanEventBus.RaiseFocusWatchTierChanged(FocusWatchTier.Glance);
            yield return null;
            Assert.AreEqual(FocusWatchTier.Glance, _focusRenderer.CurrentTier);
            Assert.Greater(_focusRenderer.ActiveVisualCount, ringOnlyVisuals,
                "1단계(곁눈질)로 올렸는데 도형이 늘지 않았습니다 — 링만 있고 경고 연출이 없다는 뜻입니다.");
            int glanceVisuals = _focusRenderer.ActiveVisualCount;

            // 3단계(창 두드림) — 두드림 자국이 더 붙는다.
            StickmanEventBus.RaiseFocusWatchTierChanged(FocusWatchTier.WindowTap);
            yield return null;
            Assert.AreEqual(FocusWatchTier.WindowTap, _focusRenderer.CurrentTier);
            Assert.Greater(_focusRenderer.ActiveVisualCount, glanceVisuals,
                "3단계(창 두드림)로 올렸는데 도형이 1단계보다 늘지 않았습니다 — " +
                "18절의 '점진적 에스컬레이션'이 시각적으로 구분되지 않습니다.");
            Assert.AreEqual(0, _focusRenderer.ActiveColliderCount,
                "3단계 연출이 콜라이더를 만들었습니다 — 흔들리는 것은 링뿐이어야 하고 클릭관통은 유지되어야 합니다.");

            // 즉시 리셋(18절) — 정상 복귀하면 경고 도형만 걷히고 링은 남는다.
            StickmanEventBus.RaiseFocusWatchTierChanged(FocusWatchTier.None);
            yield return null;
            Assert.AreEqual(FocusWatchTier.None, _focusRenderer.CurrentTier);
            Assert.AreEqual(ringOnlyVisuals, _focusRenderer.ActiveVisualCount,
                "정상 범위로 돌아왔는데 경고 도형이 남아 있습니다(18절 '즉시 리셋' 위반).");
            Assert.IsTrue(_focusRenderer.IsRingVisible, "경고만 걷혀야 하는데 링까지 사라졌습니다.");

            // 세션 종료 -> 링 완전 소멸.
            _focusDirector.ForceTriggerNow("PlayMode 테스트(끄기)");
            yield return null;
            yield return null;

            Assert.IsFalse(_focusDirector.IsSessionActive, "집중 모드가 꺼지지 않았습니다.");
            Assert.IsFalse(_focusRenderer.IsRingVisible, "세션이 끝났는데 타이머 링이 그대로입니다.");
            Assert.AreEqual(0, _focusRenderer.ActiveVisualCount, "정리 후에도 링 도형이 남아 있습니다.");
            Assert.IsNull(GameObject.Find(FocusContainerName),
                $"'{FocusContainerName}' GameObject가 씬에 그대로 남아 있습니다.");

            Debug.Log($"[Phase5테스트] 포모도로 검증 통과 — 링 {ringOnlyVisuals}개 -> 1단계 {glanceVisuals}개 -> " +
                "3단계 더 많음 -> 즉시 리셋으로 링만 남음 -> 세션 종료 시 전부 소멸, 콜라이더 0개.");
        }

        // ================================================================================
        // 공용 헬퍼
        // ================================================================================

        /// <summary>Director들의 진입 조건(Idle/Walk)이 만족될 때까지 기다린다 — 씬 로드 직후에는
        /// 캐릭터가 낙하 중(Fall)이라 강제 발동이 조용히 건너뛰어진다.</summary>
        private IEnumerator WaitUntilIdleOrWalk()
        {
            const float timeout = 12f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                var id = _agent.Blackboard.Machine.CurrentStateId;
                if ((id == StickmanStateId.Idle || id == StickmanStateId.Walk) && !SpectacleEventLock.IsActive) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.Fail($"{timeout}초 안에 캐릭터가 Idle/Walk로 정착하지 않았습니다 " +
                $"(현재 {_agent.Blackboard.Machine.CurrentStateId}, 락={SpectacleEventLock.IsActive}).");
        }

        private static int CountRootObjectsNamed(string name)
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name && all[i].parent == null) count++;
            }
            return count;
        }

        private static RectTransform FindPostItPanel()
        {
            var all = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "PostItPanel") return all[i];
            }
            // 비활성 오브젝트는 위 탐색으로 안 잡힐 수 있으므로 캔버스 계층에서 직접 찾는다.
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name != "TodoPostItCanvas") continue;
                Transform found = canvases[i].transform.Find("PostItPanel");
                if (found != null) return (RectTransform)found;
            }
            return null;
        }

        private static TodoItem FindTodoById(int id)
        {
            var items = TodoListModel.ActiveItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id) return items[i];
            }
            var archive = TodoListModel.CompletedArchive;
            for (int i = 0; i < archive.Count; i++)
            {
                if (archive[i].Id == id) return archive[i];
            }
            return null;
        }
    }
}
