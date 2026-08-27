# StickMate — 최종 코드 리뷰 (Test Engineer / Reviewer)
> 작성: Test Engineer (Teammate3) · 대상: Phase 0~5 전체 구현 · 관점: 가독성/유지보수성/확장성 (정확성은 Debugger가 이미 다회 검증 완료, 이번 리뷰는 정확성 재검증 아님)

## 결론

**개선 사이클 필요 — Architect 복귀 요청**

사소한 스타일 문제는 전부 걸러냈고, 아래 1건만 "심각한 DRY 위반"으로 판단해 반려한다. 그 외 검토 항목(self-transition 패턴, StickConfig 크기, 네이밍)은 근거와 함께 "반려 사유 아님"으로 명시한다.

---

## 좋은 점

1. **"왜" 주석 컨벤션이 6라운드 내내 흔들리지 않았다.** `States/JumpState.cs`(49줄짜리 가장 단순한 상태)조차 전이 조건마다 이유를 남기고(`JumpState.cs:27,35-36,42`), `States/StickmanStateMachine.cs:10-36`의 전이 규칙 주석은 BUG 번호·Architect 결정 근거·값의 개념적 분리 이유까지 명시한다. 프로젝트 전체를 훑는 동안 "코드를 그대로 반복하는" 불필요한 무엇-주석은 발견되지 않았고, 전부 "왜 이 값/이 순서/이 예외인가"를 설명한다. Bug 번호(`BUG-P1-M5`, `BUG-P3-M1` 등)로 히스토리를 역추적할 수 있게 해둔 것도 6라운드짜리 프로젝트에서 실질적 가치가 크다.

2. **`Core/StickmanAgent.cs`가 God Object로 비대해지지 않았다 (363줄).** Phase 0~5를 거치며 매 라운드 신규 상태가 11개까지 늘었지만(`StickmanAgent.cs:140-188`의 상태 딕셔너리), Agent 자신은 "플랫폼 서비스 조립/폴러·상태머신 배선/Suspend·Resume/입력 스냅샷"만 유지하고 있다. Phase 3~5에서 늘어난 게임플레이 로직은 전부 `States/*`·`Interaction/*`로 위임되었고, Agent가 그 신규 폴더들의 존재를 몰라도 되도록 `Blackboard`/`PlatformService`/`IsSuspended` 세 개의 읽기 전용 프로�터티만 열어준 설계(`StickmanAgent.cs:49-66` 주석에 그 이유가 명시됨)가 실제로 지켜졌다.

3. **`IPlatformWindowService` + 3개 옵셔널 인터페이스의 "as 캐스팅" 캐퍼빌리티 패턴이 macOS 확장 지점을 여전히 깨끗하게 유지한다.** `Platform/IPlatformWindowService.cs`가 필수 계약이고, `ICursorPositionService`/`ILocalClickCaptureService`/`IDesktopIconLayoutService`는 소비 측이 전부 `as I...Service`로 옵셔널 조회한다(`Core/StickmanAgent.cs:113`, `Interaction/DragThrowController.cs:91`, `Interaction/DesktopIconMirrorDirector.cs:43`). `Platform/FallbackPlatformWindowService.cs:64-66`가 이 패턴으로 4개 인터페이스를 전부 위임하는 실사례이기도 하다. macOS 구현체는 `Platform/MacOS/`(현재 `.gitkeep`만 존재)에 `IPlatformWindowService` 하나만 구현해도 앱이 동작하고, 나머지 3개는 지원 범위만큼 점진적으로 얹으면 된다 — 아키텍처 0-1절이 약속한 확장 지점이 Phase 5 시점에도 그대로 유효하다.

4. **self-transition 패턴이 억지로 추상화되지 않고 "형태만 문서화된 컨벤션"으로 남아있는 것 자체가 좋은 판단이다** (근거는 "개선할 부분 아님" 섹션 참고).

5. **`Interaction/RivalStickmanAgent.cs`가 독립 `StickmanBlackboard`/`StickmanStateMachine` 인스턴스를 동시에 여러 개 운용 가능함을 이미 실증했다** (`RivalStickmanAgent.cs:82,103`). Tasklist.md Phase 5 로그(line 77)에서 Architect가 이를 근거로 "세포분열(22절)은 기술적 난이도가 낮다"고 판단한 것도 코드로 확인된다 — P3 보류 항목이 이 구조 위에 무리 없이 얹힐 길이 이미 검증된 상태.

---

## 개선할 부분

### 1. `SpectacleEventLock` 해제 보일러플레이트가 12개 Director에 복붙됨 — DRY 위반 (반려 사유)

`SpectacleEventLock`을 획득하는 12개 `Interaction/*` 컴포넌트 전부가 `OnDisable()`에서 사실상 동일한 3단계 골격을 각자 손으로 다시 작성했다:

```csharp
private void ReleaseOwnedLock() {
    if (SpectacleEventLock.CurrentOwner != (object)this) return;
    if (_player != null && _player.Blackboard != null && _player.Blackboard.Machine != null &&
        _player.Blackboard.Machine.CurrentStateId == StickmanStateId.X)
    {
        _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
    }
    SpectacleEventLock.Release(this);
}
```

해당 지점: `GraffitiDirector.cs:39-49`, `TodoReminderDirector.cs:34-42`, `RunawayDirector.cs:49-58`, `WindowTheftDirector.cs:51-60`, `DesktopIconMirrorDirector.cs:58-67`, `RodeoCursorWatcher.cs:47-55`, `BattleMinigameDirector.cs:57-68`(`ReleaseOwnedLocks`), `DragThrowController.cs:59-70`(`ReleaseOwnedLocks`), `RivalEncounterDirector.cs:40-44`, `StressGaugeDirector.cs:60-66`, `WindowCrashDirector.cs:38-56`, `FocusWatchDirector.cs:282-301`(`ReleaseOwnedLock(bool forceIdle)`) — 총 12곳.

이게 우연한 수렴이 아니라 **알고도 계속 복붙된 이력**이라는 게 더 중요하다: Phase 3에서 이 정확한 문제가 `BUG-P3-M1`(Major)로 지적됐고(Tasklist.md line 98), 그때 4개 Director에 "공통 패턴"이라 부르며 동일 코드를 복붙해 반영했다(Tasklist.md line 99: "공통 패턴: (1)...(2)...(3)..."). 이후 Phase 4/5에서 8개 Director가 더 늘면서 같은 골격이 또 8번 손으로 재생산됐다. 공용 헬퍼를 만들 기회가 최소 두 번(Phase 3 반려 시점, Phase 4 착수 시점) 있었는데 매번 "그 자리에서 복붙"으로 넘어간 셈이다.

**실질적 위험**: 이 락 해제 정책이 바뀌면(예: 강제 Idle 대신 페이드아웃 연출 추가, 해제 시 로깅 추가, 예외 상태 하나 더 추가) 12곳을 전부 손으로 맞춰 고쳐야 한다. 이미 한 곳이라도 놓치면 Phase 3에서 실제로 겪었던 것과 동일한 계열의 회귀(컴포넌트 `enabled=false` 시 락이 영구 미반환 → 해당 스펙터클 기능이 앱 재시작 전까지 발동 불가)가 재발할 수 있다 — Debugger가 근본 원인이 아니라 각 증상만 막아온 셈.

**권고**: `SpectacleEventLock`에 정적 헬퍼(예: `ReleaseWithStateGuard(object owner, StickmanStateMachine machine, StickmanStateId guardedState)`)를 추가하거나, `Interaction/*` Director들의 공통 베이스 MonoBehaviour를 만들어 이 3단계만 추출할 것을 권고한다. `RivalEncounterDirector`(`_rival?.ForceEndDuel()` 경유)나 `FocusWatchDirector`(`forceIdle` 분기가 있는 커스텀 버전)처럼 정리 로직이 실제로 다른 소수는 헬퍼 적용 대상에서 빼도 무방하다 — 12곳 전부를 강제로 동일한 모양으로 만들 필요는 없고, 최소 8~9곳(순수 반복분)만 추출해도 실익이 크다.

---

## 검토했으나 반려 사유로 보지 않음

- **self-transition 패턴** (`RagdollState`/`BattleMinigameState.cs:69-85`/`WindowTheftState.cs:41-56`/`RunawayState.cs:79-129`) — "판정 순간과 대사 파생 순간을 같은 Enter()로 묶는다"는 **형태**만 반복되고, pending 데이터는 상태마다 완전히 다르다(WindowTheft는 bool 하나, Battle은 float 하나, Runaway는 서로 다른 두 종류의 pending을 각각 다른 후속 로직(RestoreCharacter/이벤트 종류/대사)에 연결). 제네릭 베이스 클래스로 강제 통일하면 pending 페이로드 타입이 제각각이라 타입 파라미터가 늘어나고, 각 상태의 실제 판정 로직(성공/실패/재시도 카운트 등)은 어차피 베이스 클래스가 대신해줄 수 없어 오버라이드 지점만 늘어난다 — 추상화 비용이 절감분보다 크다. 참고로 `RagdollState`는 실제로는 이 패턴을 쓰지 않는다(재피격마다 `StickmanAgent.ReportExternalImpact()`가 외부에서 `ChangeState`를 호출하고 `LastImpactMagnitude` 스냅샷을 직접 읽을 뿐, 자기 자신을 self-transition하지 않음) — 클래스 주석(`RagdollState.cs` 등에서 "RagdollState와 동일한 패턴"이라 언급한 부분)이 실제 코드보다 살짝 과장되어 있다는 점만 참고로 남긴다(동작에는 영향 없음).

- **`StickConfig.cs`의 필드 규모 (156개 public 필드, 542줄, 22개 `[Header]` 섹션)** — 규모만 보면 크지만, 22개 Header 전부가 `docs/UX_FLOW.md`의 절 번호와 1:1로 매핑되어 있어(`StickConfig.cs:52,84,148,179,214,229,265,285,312,342,360,400,424,464,510` 등) 특정 기능의 튜닝값을 찾는 데 실제 어려움이 없다. DLC 확장(아키텍처 0-3절)은 애초에 `MotionPluginSO`/`EffectPluginSO`라는 별도 ScriptableObject 경로를 쓰고 `StickConfig`를 건드리지 않으므로, 이 필드 수가 향후 DLC 확장을 막지 않는다. 지금 기능별 SO로 쪼개면 "이 수치가 어느 config 에셋에 있더라"를 찾는 새로운 탐색 비용이 생길 뿐 실이익이 불분명하다 — 유지만 권고, 분리는 불필요.

- **네이밍 — `Interaction/*` 14개 MonoBehaviour 중 12개가 `XxxDirector`, 2개(`DragThrowController.cs`, `RodeoCursorWatcher.cs`)만 다른 접미사.** 두 클래스 모두 나머지 12개와 정확히 같은 역할(트리거 감시 → 락 획득 → `ChangeState` 호출)을 하는데 `Director`가 아닌 이유가 클래스 주석 어디에도 없다(`DesktopIconMirrorDirector.cs:8`처럼 이름의 유래를 UX 문서 인용으로 명시한 사례와 대조적). 실제 혼란을 야기하는 수준은 아니라 판단해 반려 사유로는 올리지 않지만, 다음 라운드에 건드리는 김에 `Director`로 리네임하면 일관성이 완전해진다(우선순위 낮음).

---

## 참고: 검토 범위

`CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/UX_FLOW.md`(목차/전 절), `Tasklist.md`(Phase 0~6 전체 로그), `Assets/_Project/Scripts/` 전체(Core 7파일/Dialogue 2파일/Interaction 18파일/Platform 11파일/States 22파일/Plugins 2파일/Tests 2파일 + AssemblyInfo.cs, 총 65개 .cs) 를 검토했다. 정확성 버그는 찾지 않았다(Debugger가 이미 6라운드에 걸쳐 Blocker/Major/Minor를 검증 완료했고, 이번 리뷰의 스코프가 아니다).

---

## (개선 R2) 재확인

**개선할 부분 없음 — 최종 완료**

대상 커밋: `b793b7e` "Phase 6 (개선 R2): SpectacleEventLock 락 해제 공용 헬퍼 추출". `git show b793b7e --stat`으로 변경 범위(Core/SpectacleEventLock.cs + Interaction/* 12파일 + Tasklist.md/docs)를 확인하고 아래 4가지를 직접 재검증했다.

1. **헬퍼 설계가 12곳의 실제 다양성을 반영하는지** — `SpectacleEventLock.ReleaseIfOwned(owner, machine, guardedState, clickCapture=null)`을 직접 읽었다. 12곳 중 소유권 사전 확인이 없던 3곳(`BattleMinigameDirector`/`DragThrowController`/`WindowCrashDirector`)에 헬퍼가 확인을 추가한 것이 유일한 동작 변화 후보였는데, 세 파일의 `ChangeState(guardedState)` 호출부를 전부 `grep`으로 추적한 결과 예외 없이 "`SpectacleEventLock.TryAcquire()` 성공 직후에만" 호출된다(다른 어떤 코드도 그 상태로 전이하지 않음) — 즉 `CurrentStateId==guardedState`이면 항상 `CurrentOwner==owner`이므로 가드 추가는 관찰 가능한 동작을 바꾸지 않는다. 강제전이 대상(`Idle`)/방식(`isForcedInterrupt:true`)은 12곳 전부 동일해 고정값으로 처리한 것도 타당하고, 클릭캡처 해제는 실제로 쓰는 2곳(`BattleMinigameDirector`/`DragThrowController`)만 옵션 인자로 넘기는 구조도 억지 통합이 아니다.
2. **10곳 교체가 동작을 바꾸지 않았는지** — `git show b793b7e`로 10개 Director 각각의 `OnDisable()`/`ReleaseOwned*()` diff를 전부 직접 대조했다. `Graffiti`/`WindowTheft`/`DesktopIconMirror` 3곳은 `_hasRegion`/`_hasTarget` 필드 리셋이 "`ChangeState`와 `Release` 사이"에서 "헬퍼 호출 직전"으로 실행 순서가 바뀌었는데, 세 파일 모두 `OnDisable()`에서 `StickmanEventBus.StateTransitioned -= OnStateTransitioned`가 헬퍼 호출보다 먼저 실행돼 그 구간에 해당 필드를 읽는 콜백이 이미 끊겨 있음을 소스에서 직접 확인했다 — 순서 변경이 안전하다는 Coder 주장이 맞다. `WindowCrashDirector`는 컨트롤러 고유의 오버레이 정리(`_overlayActive`/`RaiseOverlay(Cancelled)`)를 헬퍼 밖에 그대로 남겨 공통 3단계만 추출했다. 나머지는 사실상 1:1 치환.
3. **`RivalEncounterDirector`/`FocusWatchDirector` 예외 처리의 타당성** — 코드를 직접 읽었다. `RivalEncounterDirector.ReleaseOwnedLock()`은 `StickmanStateId` 비교가 아니라 `_rival?.ForceEndDuel()`로 상대방의 별도 상태머신을 정리하므로 `guardedState` 개념 자체가 없어 헬퍼 시그니처로 표현 불가능하다. `FocusWatchDirector.ReleaseOwnedLock(bool forceIdle)`은 단일 상태가 아니라 `IsFocusPoseState()`(4개 상태 predicate)로 가드하므로 단일 `StickmanStateId` 파라미터로 표현할 수 없다. 이 두 예외는 애초에 R1 리뷰 본문("`RivalEncounterDirector`나 `FocusWatchDirector`처럼 정리 로직이 실제로 다른 소수는 헬퍼 적용 대상에서 빼도 무방하다")에서 리뷰어가 직접 승인한 지점과 정확히 일치한다 — 게을러서 남긴 예외가 아니라 원 지적 그대로를 이행한 것.
4. **컴파일 + EditMode 테스트 독립 재실행** — Coder가 남긴 `compile_r2.log`/`testresults_r2.xml`은 작업 트리에 남아있지 않아(임시 산출물로 추정) Unity 6000.0.82f1로 리뷰어가 직접 재실행했다. 배치모드 컴파일: `error CS`/`warning CS` 매치 0건. `-runTests -testPlatform EditMode`: `testcasecount="13" result="Passed" total="13" passed="13" failed="0"` — 13개 케이스 전부 개별 확인. 에러0/경고0 + 13/13 기준선 독립 재확인 완료.

R1에서 유일한 반려 사유였던 DRY 위반은 실질적으로 해소됐고, 이번 재작업이 새로운 문제를 도입하지도 않았다. 추가 개선 사이클 불필요.
