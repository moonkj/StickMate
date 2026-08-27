# PERFORMANCE_REPORT.md — Phase 6 성능 점검

> 작성: Performance Engineer(Teammate4) · 2026-08-28
> 범위: `Assets/_Project/Scripts/` 전체 (Core/Platform/States/Interaction/Dialogue) — Phase 0~5 구현 완료 기능 전부.
> 방법: 전수 grep 감사(Update/FixedUpdate 목록화, `EnumerateFootholds` 전체 호출부 재검증, LINQ/문자열연결/GetComponent/Camera.main/`new` 할당 패턴 검색) + 의심 지점 소스 직독.

## 총평
**"Coder 반영 필요" 등급의 실질적 문제 0건.** 24시간 상주 앱 성능 컨벤션(폴링 주기화, 매 프레임 할당 금지, OS API 직접호출 금지)이 Phase 0~5 전 구간에 걸쳐 이미 코드 레벨로 강제되어 있고, 이번 라운드에 신설된 Phase 3~5의 13개 Director도 전부 동일 컨벤션을 재사용해 만들어졌다. 아래는 "사소함/참고" 등급 4건과, 항목별 "문제없음" 판정 근거다.

---

## 1. Idle CPU / 폴링 주기 — 문제없음

- `EnumerateFootholds()` 전수 재검증(`grep -rn EnumerateFootholds`): 실제 호출부는 `Platform/FootholdPoller.cs:85` 단 한 곳뿐. States/Interaction 어디에도 우회 직접호출 없음. `FootholdPoller.Tick()`이 `StickConfig.footholdPollInterval`(기본 0.5초) 주기로만 실제 `EnumWindows` P/Invoke를 수행하고, 결과가 이전과 동일하면 이벤트도 재발행하지 않는다(`Platform/FootholdPoller.cs:75-93`).
- `IsFullscreenAppActive()`도 `Core/StickmanAgent.cs:255`(`TickFullscreenSuspend`, 기본 1.5초 주기) 한 곳에서만 호출. `EnumerateIconRects()`도 `Interaction/DesktopIconMirrorDirector.cs:148`에서 스펙터클 발동 확정 시점(60초 체크주기 + 확률 통과 후)에만 1회 호출 — 상시 폴링 아님.
- Phase 3~5 신설 13개 Director(`BattleMinigameDirector`/`DesktopIconMirrorDirector`/`DragThrowController`/`FocusWatchDirector`/`GraffitiDirector`/`HardwareReactionDirector`/`RivalEncounterDirector`/`RivalStickmanAgent`/`RodeoCursorWatcher`/`RunawayDirector`/`StressGaugeDirector`/`TodoPostItWidget`/`TodoReminderDirector`/`WindowCrashDirector`/`WindowTheftDirector`, 15개 확인) 전부 자체 누적 타이머(`_checkTimer += Time.deltaTime; if (_checkTimer < interval) return;`) 패턴을 씀. `StickConfig`에 정의된 주기값(대부분 20~90초, 짧은 것도 `wanderTurnCheckInterval` 0.5초)을 확인 — 실질 상태가 아닐 때(Idle/Walk 외)는 타이머조차 리셋하고 조기 반환.
- 13개 이상의 Director가 동시에 존재해도 매 프레임 각자 수행하는 작업은 float 감산 1~2회 + enum 비교 1~2회 수준(`if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;`)이라 O(15) 합산해도 나노초 단위 — 24시간 상주 CPU 부담으로 유의미하지 않음. **개수가 늘어나는 것 자체는 문제가 아니고, 각 Director가 컨벤션(타이머 게이트)을 지키는지가 핵심인데 전부 지켜지고 있음.**
- 예외적으로 매 프레임 무조건 `TryGetCursorPosition()`(→ Win32 `GetCursorPos`)을 호출하는 곳이 3곳(`Interaction/RodeoCursorWatcher.cs:62`, `Interaction/FocusWatchDirector.cs:150`, `Interaction/DesktopIconMirrorDirector.cs:89`, 단 이건 `MonitorActive()` 안이라 스펙터클 진행 중에만) 있으나, `GetCursorPos`는 `EnumWindows`와 달리 창 열거가 없는 단일 경량 P/Invoke(공유 메모리 read 수준)라 프레임당 호출해도 부담이 사실상 없다 — `Win32WindowService.cs:171-179` 구현 확인. 로데오 커서/포모도로는 프레임 단위 정지 판정이 기능 요구사항이라 의도된 설계.

## 2. 메모리 할당(GC) — 문제없음

- `DialogueIntent`/`IHasDialogueParams` 전수 확인: 생성 시그니처가 `StateTransitionContext`를 요구하는데, 이 컨텍스트는 `StickmanStateMachine.ChangeState()`가 `Enter()` 호출 시에만 발급(`States/StickmanStateMachine.cs:126-129`)한다. `new DialogueIntent(...)` 호출부 전부(`AttackState.cs:60`, `BattleMinigameState.cs:84/188`, `RagdollState.cs:64`, `ParkourClimbState.cs:72`, `RunawayState.cs:90/102/128`, `WindowTheftState.cs:51`, `TimedSpectacleState.cs:61`)를 grep+직독으로 확인한 결과 **전부 `Enter()` 구현부 안**이며 `Tick()` 안에는 하나도 없음 — 설계 보장(상태 전이 확정 시점에만 대사 생성)이 실제로 지켜짐.
- LINQ(`.Where/.Select/.Any/.ToList` 등) 사용처는 `Tests/EditMode/UserAssetImmutabilityAuditTests.cs` 테스트 코드뿐, 런타임 프로덕션 코드(Core/Platform/States/Interaction)에는 0건.
- 문자열 보간(`$"..."`)/연결(`+`)은 `States/StickmanStateMachine.cs:117-118`(`ChangeState` 실패 시 `Debug.LogError`, 에러 경로 전용) 한 곳뿐 — 정상 경로 매 프레임 실행 안 됨. `Interaction/TodoPostItWidget.cs`의 문자열 연결(`row.Label.text = box + " " + item.Text`)은 `RefreshView()` 안인데, 이는 `TodoListChanged` 이벤트(유저가 투두를 추가/체크할 때만) 또는 UI 버튼 클릭에서만 호출됨 — 매 프레임 아님.
- `new List<T>/Dictionary<T>` 등 참조형 할당은 전부 `readonly` 필드(컴포넌트/서비스 생성 시 1회) 또는 생성자 내부 — `Platform/Windows/Win32WindowService.cs:111`(`_footholdBuffer`, 재사용), `Platform/FootholdPoller.cs:43`(`_cache`, 재사용), `States/DragThrowState.cs:37-38`(순환 버퍼, 재사용) 등 전부 "매 호출 새로 만들지 않고 재사용" 주석이 붙어 있고 실제로도 `Clear()` 후 재사용하는 패턴.
- `Interaction/StressGaugeDirector.cs:145,159`의 `_overuseTimestamps.RemoveAll(t => now - t > window)` / `_emergencyStopTimestamps.RemoveAll(...)`는 람다 클로저 할당이 있으나, 호출부가 `OnStateTransitioned`(격파/드래그 진입 시)와 `OnEmergencyStop`(트레이 긴급정지 버튼)이라 **유저 행동 이벤트 기반**이지 프레임 기반이 아님 — 세션당 많아야 수십 회, GC 압박과 무관. (사소함, 조치 불필요)

## 3. 캐싱 전략 — 문제없음

- `Camera.main`은 `Core/StickmanAgent.cs:101` `Awake()`에서 1회만 조회해 `_mainCamera` 필드로 캐싱, 이후 `StickmanBlackboard.MainCamera`로 전파되어 전 상태가 공유. `Update()` 등 반복 조회 0건.
- `GetComponentsInChildren<T>`(`Rigidbody2D`/`Renderer`)는 `Awake()` 1회(`Core/StickmanAgent.cs:96-110`, `Interaction/RivalStickmanAgent.cs:49-50`, `States/RagdollRig.cs:29-30`).
- `GetComponent<T>()` 호출부(`WindowTheftDirector.cs:32`, `BattleMinigameDirector.cs:27-28`, `DragThrowController.cs:26-27`, `StressGaugeDirector.cs:38`, `RunawayDirector.cs:31` 등)는 전부 `if (_x == null) _x = GetComponent<...>()` null-가드 지연 캐싱 패턴 — `Awake()`에서 1회 실행 후 필드에 고정되어 `Update()`에서 재호출되지 않음. `Interaction/TodoPostItWidget.cs`의 `GetComponent<RectTransform/Image/Text/Button>()`은 UI 위젯을 처음 빌드하는 `BuildUi()`/`CreateRow()` 시점(row 신설 시 1회)에만 실행되고, 이후 `RefreshView()`는 캐싱된 `RowWidgets` 목록만 재사용.

## 4. SpectacleEventLock / StickmanEventBus 팬아웃 — 문제없음 (Phase 0 m4 재평가)

- `Core/SpectacleEventLock.cs`: 정적 필드 `_owner`/`_activeKind` 비교뿐인 O(1) 락 — 구독자 수(Director 개수)와 전혀 무관하게 비용이 고정. Director가 13개든 130개든 `TryAcquire`/`Release` 비용은 동일.
- `StickmanEventBus.StateTransitioned` 구독자 수를 실측(`grep "StateTransitioned +="`): 11개(`DialogueIntent` 포함). 이 이벤트는 "상태 전이가 확정된 프레임"에만 발행되는데, 상태 전이는 사람/AI 타임스케일 이벤트(수초~수십초 간격)라 프레임당 발생 빈도가 극히 낮음 — O(11) 팬아웃이 60fps 매 프레임 발생하는 게 아니라 전이 발생 시에만 1회 발생. 실측 가능한 CPU 영향 없음.
- Phase 0 Minor m4에서 우려했던 "구독자 증가에 따른 팬아웃 비용"은, 실제로 신설된 구독자 수(11개)와 이벤트 발행 빈도(전이 시점만)를 함께 고려하면 24시간 상주 앱에서 문제가 될 규모가 아님 — **재평가 결론: 문제없음, 추가 조치 불필요.**

---

## 참고 사항 (조치 불필요, 다음 라운드 참고용 기록)

1. **Rigidbody2D 속도/위치 설정이 `Update()` 경로(Tick)에서 이루어짐** — `States/WalkState.cs:71`, `JumpState.cs:28`, `IdleState.cs:31`, `ParkourClimbState.cs:64/115`, `DragThrowState.cs:64/129/188`, `RodeoCursorState.cs:53/84/107`, `RunawayState.cs:115`, `StickmanBlackboard.cs:226` 등에서 `Rigidbody2D.linearVelocity`/`MovePosition`을 `StickmanStateMachine.Tick(Time.deltaTime)`(→ `StickmanAgent.Update()`)에서 설정한다. `FixedUpdate()`는 프로젝트 전체에 0건. 이는 GC/CPU 이슈가 아니라 **물리 스텝 타이밍**(프레임레이트 변동 시 물리 잔떨림 가능성) 이슈이며, 여러 상태 파일에 `TODO(Phase 2): 보행 IK/애니메이션 시작`처럼 모터/IK 자체가 아직 스텁인 곳도 있어 지금 시점에 유의미한 체감 문제로 보이지 않는다. 이번 성능 점검 범위(Idle CPU/GC/폴링/캐싱) 밖의 물리 정확성 문제라 구조 변경 없이 기록만 남긴다 — Architect가 렌더링/모터 레이어 착수 시 함께 검토 권고.
2. `StressGaugeDirector`의 `List.RemoveAll(lambda)` (위 2절 참고) — 이벤트 기반 호출이라 무해하지만, 완전 무할당을 원하면 역방향 for-loop + RemoveAt으로 대체 가능. 사소함, 조치 불필요.

## 결론
Phase 6 성능 점검 결과 **Coder 반영이 필요한 실질적 문제는 없음**. 5라운드에 걸쳐 누적된 팀 컨벤션(FootholdPoller 게이트, 델리게이트/버퍼 재사용, DialogueIntent의 Enter() 전용 생성, O(1) 정적 락)이 Phase 3~5 신규 코드에도 일관되게 적용되어 있음을 코드 직독으로 확인했다. 코드 수정 없음 → Unity 배치 재컴파일 불필요.
