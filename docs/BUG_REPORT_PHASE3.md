**Major 1건 발견 — Coder로 반려 필요**

# StickMate — Phase 3 버그 리포트 (Debugger, Teammate2)
> 작성: Debugger · 작성일: 2026-08-27 · 대상: 커밋 `a2ae139`("Phase 3: 전투/커서상호작용 5개 기능 + 텍스트-액션 회귀 테스트 인프라")
> 범위: Phase 3 신규/수정 파일 전체 — `Interaction/*.cs`(BattleMinigameDirector, ClickHitboxRectUtility, DragThrowController, RivalEncounterDirector, RivalPursuitIntentSource, RivalStickmanAgent, RodeoCursorWatcher, StickmanClickHitbox), `Platform/ILocalClickCaptureService.cs`, `Platform/LocalClickCaptureGate.cs`, `Platform/FallbackPlatformWindowService.cs`(수정분), `Platform/NullPlatformWindowService.cs`/`Platform/Windows/Win32WindowService.cs`(ILocalClickCaptureService 구현분), `States/BattleMinigameState.cs`, `States/DragThrowState.cs`, `States/RodeoCursorState.cs`, `States/RagdollImpactResolver.cs`, `States/AttackState.cs`(완성분), `Core/SpectacleEventLock.cs`, `Core/StickmanAgent.cs`(수정분), `Core/StickmanEventBus.cs`/`Core/StickConfig.cs`(Phase 3 추가분), `Tests/EditMode/DialogueTextActionSyncTests.cs` + asmdef 2종.
> 환경: Unity 배치모드 컴파일 2회 실행. ① 1차(캐시 재사용): `error CS`/`warning CS` 0건, `Batchmode quit successfully`/`Exiting batchmode successfully now` 정상 종료. ② 2차(독립 검증): `Library/ScriptAssemblies`/`Library/Bee`/`Library/PlayerDataCache` 강제 삭제 후 `-runTests -testPlatform EditMode` 재실행 — `177 items updated`, `script compilation time 3.100944s`로 실제 재컴파일 확인, `error CS`/`warning CS` 매치 0건. 테스트 결과 xml 직접 파싱: `testcasecount="8" result="Passed" total="8" passed="8" failed="0"`. **클린 재빌드로 "에러 0/경고 0" 기준선 + "8/8 통과" 둘 다 독립 재확인 완료.**

## 결론 요약

**Blocker 0건, Major 1건(BUG-P3-M1), Minor 2건 + 미반영 확인 1건(Architect 지시, 다음 라운드 이월 예정이었음).**

- 중점 점검 2(SpectacleEventLock 상호배제)/3(LocalClickCaptureGate 단일 소유자 락)의 **핵심 메커니즘 자체**(획득 실패 시 조용히 스킵, 정상완료/타임아웃/RAGDOLL 강제인터럽트 종료 경로에서 락 해제)는 4개 기능(격파/드래그/라이벌/로데오) 전부 정확히 구현되어 있음을 코드로 확인했다. 다만 **소유자 컴포넌트 자체가 `OnDisable()`/`OnDestroy()`되는 경로에서는 락을 반환하지 않는다** — 신규 Major(BUG-P3-M1).
- 중점 점검 4(RivalStickmanAgent 독립성): 별도 `StickmanBlackboard`/`StickmanStateMachine` 인스턴스, `DialogueIntent`의 만료 판정이 인스턴스별 `_originMachine.CurrentTransitionGeneration`을 참조해 `StickmanEventBus`가 정적 클래스임에도 라이벌↔플레이어 대사가 상호 오염되지 않음을 코드로 직접 검증했다. `IsSuspended` 폴링도 정확히 `_opponent`(플레이어)를 참조한다.
- 중점 점검 5(DragThrowState Kinematic 전환): `Enter()`/`Exit()` 페어링이 SpectacleEventLock 해제, `Suspend()`의 강제 Idle 전이, RAGDOLL 강제인터럽트 세 경로 모두에서 `StickmanStateMachine.ChangeState()`의 Exit-먼저-Enter-나중 순서 덕에 항상 정확히 복구됨을 확인했다. Kinematic 바디는 `gravityScale`이 적용되지 않아 BUG-P2-M1류의 숨은 속도 누적 위험도 구조적으로 없다.
- 중점 점검 6(AttackState 완성분): `Tick()`이 `attackDuration` 경과 후 진입 직전 상태로 정상 복귀함을 확인(예전 "영원히 못 나오는 상태" 해소), 텍스트도 UX 31-2 표와 정확히 일치("한 발 더!"/"오늘은 여기까지", 직전 라운드 Minor 1 해소). 다만 `ShotsRemaining`이 상시 0으로 고정돼 `>=1` 분기가 죽은 코드 + 라이벌 대결 맥락에서 의미상 어색함(Minor 1).
- 중점 점검 7(부분적 클릭관통 해제 문서화): 코드로 재확인한 결과 문서의 주장이 정확히 사실과 일치한다 — `Win32WindowService.SetClickThrough()`가 `NotSupportedException`으로 실제 차단되어 있어 전역 클릭관통 자체가 지금 켜져 있지 않으므로, "영역 밖 100% 관통" 요구사항이 지금 당장 위반되는 위험한 상태는 아니다.
- **중점 점검 1(격파 미니게임 self-transition)**: Architect의 지시(Tasklist.md 교차 레이어 로그, "다음 라운드에서 반영")가 **아직 반영되지 않았음을 확인**했다 — `BattleMinigameState.ResolveClick()`/`ResolveOutcome()`은 여전히 `Tick()` 도중 `StickmanEventBus.RaiseBattleMinigamePhaseChanged()`만 발행할 뿐, 자기-전이(`ChangeState(BattleMinigame, ...)` 재호출)도 `chargeRatio` 기반 릴리즈 대사도 만들지 않는다. 커밋 메시지 자체가 "다음 라운드"로 명시했으므로 이번 라운드 기준으로는 결함이 아니지만, **다음 Coder 라운드에 반드시 반영되어야 함을 이 리포트로 명확히 이월**한다(버그 심각도 집계에는 포함하지 않음).
- 텍스트-액션 회귀 테스트(8건)는 리플렉션으로 `sealed class`/생성자 개수를 확인하고 `InvalidOperationException`/이벤트 발행 횟수를 검증하는 등 실제 계약을 검증하며, 상시 통과하는 가짜 테스트가 아님을 확인했다. asmdef `InternalsVisibleTo("StickMate.Tests.EditMode")`는 테스트 어셈블리에만 `internal` 가시성을 허용할 뿐 프로덕션 공개 API를 넓히지 않는다.

---

## 권고 순서

1. **BUG-P3-M1 수정** — 4개 Interaction Director(`BattleMinigameDirector`/`DragThrowController`/`RivalEncounterDirector`/`RodeoCursorWatcher`)의 `OnDisable()`에 "내가 지금 SpectacleEventLock/ILocalClickCaptureService의 소유자라면 반환한다" 방어 로직 추가. 각 파일당 수 줄 규모, 기존 `OnStateTransitioned`/`OnEmergencyStop` 핸들러의 해제 로직을 재사용하면 되므로 반려 사이클이 길지 않을 것.
2. **격파 미니게임 self-transition 패턴 반영** — Architect 지시(Tasklist.md)를 다음 라운드에 실제로 적용할 것. `BattleMinigameState`의 클릭 판정 지점(`ResolveClick`/`ResolveOutcome`)을 `RagdollState`의 반복 피격 패턴처럼 "같은 상태로 `ChangeState` 재호출 → `Enter()` 재실행" 구조로 교체하고, UX 31-2 표 #5("필살기다!"/"어... 어라?")의 `chargeRatio` 기반 대사를 함께 구현할 것.
3. Minor 2건은 급하지 않음 — 다음 라운드 이후 Coder 재량으로 처리해도 무방하나, 특히 Minor 2(라이벌 대결 시 플레이어 쪽 Attack 애니메이션 미발동)는 관전형 스펙터클의 핵심 볼거리("서로 주먹질·발차기 오가는" 인상)에 영향을 주므로 렌더링 레이어 작업 전에 상태머신 레벨에서 먼저 고쳐두는 편이 이후 재작업을 줄인다.

---

## 이번 라운드 중점 점검 항목 결론

| # | 점검 항목 | 결론 |
|---|---|---|
| 1 | 격파 미니게임 self-transition 반영 여부(Architect 지시) | **미반영 확인(예정된 이월, 버그 아님).** `States/BattleMinigameState.cs:118-142`의 `ResolveOutcome()`이 여전히 `Tick()` 안에서 `StickmanEventBus.RaiseBattleMinigamePhaseChanged()`만 호출하고, `Machine.ChangeState(BattleMinigame, ...)` 자기-전이도 `chargeRatio` 기반 `DialogueIntent`도 만들지 않는다(코드 자체의 클래스 주석 21-28행이 이 한계를 이미 정직하게 재확인해두었다). 커밋 메시지("다음 라운드")와 정확히 일치하는 상태이므로 이번 라운드 기준 결함으로 잡지 않되, 다음 Coder 라운드 착수 시 최우선 항목으로 명확히 이월한다. |
| 2 | SpectacleEventLock 상호배제(락 획득 실패 시 스킵/모든 종료 경로에서 해제) | **핵심 메커니즘 정확, 소유자 사망 경로에 방어 누락(BUG-P3-M1).** 4개 Director(`BattleMinigameDirector`/`DragThrowController`/`RivalEncounterDirector`/`RodeoCursorWatcher`) 전부 시작 전 `SpectacleEventLock.IsActive`/`TryAcquire` 확인 후 실패 시 조용히 return함을 확인. 종료 경로 3가지(정상완료/타임아웃/RAGDOLL 강제인터럽트) 전부 `StickmanEventBus.StateTransitioned` 구독(`evt.From == 자기상태`)으로 락을 해제함을 코드로 확인 — 특히 RAGDOLL 강제 인터럽트는 `RagdollImpactResolver.TryApplyImpact()`가 `ChangeState(Ragdoll, isForcedInterrupt:true)`를 호출하고, `StickmanStateMachine.ChangeState()`가 `_current?.Exit()`를 먼저 실행한 뒤 이벤트를 발행하는 순서(`States/StickmanStateMachine.cs:124-131`)가 보장되어 있어 어떤 강제 인터럽트 경로에서도 `StateTransitioned`가 빠짐없이 발행되고 락이 해제된다. **단, Director 컴포넌트 자신이 `OnDisable()`/`OnDestroy()`되는 경로는 이 4개 파일 어디에도 방어 코드가 없다(grep으로 확인, `OnDestroy` 매치 0건)** — 락을 쥔 채 컴포넌트가 비활성화되면 영구 잠금. 상세는 BUG-P3-M1. |
| 3 | LocalClickCaptureGate 단일 소유자 락 | **동시 요청 시 단일 성공 보장 확인, 소유자 사망 시 위험은 2번과 동일 근본 원인.** `LocalClickCaptureGate.TryRequestCapture()`(`Platform/LocalClickCaptureGate.cs:29-36`)는 `_owner != null && _owner != owner`면 false를 반환하는 단순 동기 검사이고, Unity 스크립트 실행은 단일 스레드라 진짜 경쟁 상태(race condition)는 발생하지 않는다 — 어느 쪽이 먼저 `Update()`/콜백을 타든 결정론적으로 하나만 성공한다. 다만 이 락 역시 `Win32WindowService`/`NullPlatformWindowService`/`FallbackPlatformWindowService` 인스턴스 안에 존재해 앱 생명주기 동안 계속 살아있으므로, 소유자(Director)가 해제하지 않고 사라지면 2번과 같은 영구 잠금 위험을 공유한다(BUG-P3-M1이 두 락 모두를 다룸). |
| 4 | RivalStickmanAgent 독립성(플레이어와 상태 공유 여부, IsSuspended 참조 정확성) | **완전히 독립, 상호 오염 없음 확인.** `RivalStickmanAgent.EnsureMachineBuilt()`(`Interaction/RivalStickmanAgent.cs:78-106`)가 플레이어와 별개의 `StickmanBlackboard`/`StickmanStateMachine`을 생성하며, `FootholdPoller`/`MainCamera`만 참조 공유(의도된 설계, "발판 공유" 요구사항). `StickmanEventBus.StateTransitioned`가 정적 이벤트라 라이벌의 전이도 플레이어 쪽 `DialogueIntent.OnAnyStateTransitioned` 핸들러를 호출하지만, `DialogueIntent.IsValid`/`Expire()` 판정(`Dialogue/DialogueIntent.cs:63,138-144`)이 항상 자신을 만든 **그 특정 `StickmanStateMachine` 인스턴스**(`_originMachine`)의 `CurrentTransitionGeneration`만 비교하므로, 라이벌의 `ChangeState` 호출이 라이벌 자신의 세대 카운터만 증가시킬 뿐 플레이어 머신의 세대에는 전혀 영향을 주지 않는다 — 정적 이벤트버스를 공유해도 인스턴스 스코프 판정 덕에 대사 오염이 발생하지 않음을 코드로 직접 확인했다(단순 성능상 무해한 낭비 호출 1회는 있으나 정확성에는 영향 없음). `LastImpactMagnitude`도 각자의 `StickmanBlackboard` 필드라 마찬가지로 분리되어 있다. `RivalStickmanAgent.Update()`(`:115`)의 `_opponent.IsSuspended`는 `BeginDuel()`이 주입한 실제 플레이어 `StickmanAgent` 참조를 정확히 가리키며, 라이벌 자신은 별도의 Suspended 개념이 없다(전체화면 감지는 플레이어의 `StickmanAgent`만 폴링하는 단일 소스이므로 이 설계가 맞다). |
| 5 | DragThrowState Kinematic 전환과 Suspend()/RagdollRig 충돌 여부 | **충돌 없음, 정확히 복구됨을 확인.** `DragThrowState.Enter()`가 Kinematic 전환, `Exit()`가 "여전히 Kinematic이면" Dynamic으로 방어적 복구(`States/DragThrowState.cs:111-121`, `RodeoCursorState.cs:136-142`도 동일 패턴). 드래그 도중 전체화면 감지 시 `StickmanAgent.Suspend()`(`Core/StickmanAgent.cs:227-252`)가 `ChangeState(Idle, isForcedInterrupt:true)`를 먼저 호출해 `Exit()`(Dynamic 복구)를 강제한 **다음에** `SetBodiesSimulated(false)`로 물리를 멈추므로 Kinematic 상태로 얼어붙지 않는다. 드래그 도중 외력이 임계값을 넘어 RAGDOLL로 강제 전이되는 경우도(이론상 Kinematic 바디도 `OnCollisionEnter2D`를 받을 수 있음) 같은 `ChangeState()` 순서 보장으로 `Exit()`가 먼저 실행되어 동일하게 안전하다. 부가로 Kinematic Rigidbody2D는 애초에 `gravityScale`의 영향을 받지 않으므로, BUG-P2-M1(ParkourClimb 숨은 속도 누적)과 같은 클래스의 문제가 구조적으로 발생할 수 없다. |
| 6 | AttackState 완성분이 정상 종료되는지 + UX 31-2 표 일치 여부 | **정상 종료 확인, 텍스트 일치 확인. 다만 파라미터 상시 0(Minor 1).** `Tick()`(`States/AttackState.cs:73-81`)이 `attackDuration` 경과 시 `context.From`으로 기억해둔 진입 직전 상태로 복귀 — 예전 "빈 Tick()으로 영원히 못 빠져나오는 상태"가 완전히 해소됨을 확인. `Enter()`(`:58-63`)의 대사 리터럴("한 발 더!"/"오늘은 여기까지")이 UX_FLOW.md 31-2 표 #1과 정확히 일치(직전 라운드 Minor 1 해소 확인). 다만 `ShotsRemaining`이 `Enter()`에서 항상 `0`으로 고정돼(`:54`, 라이벌 대결이 유일한 실사용처이고 매번 단발 타격이라는 주석의 설명은 타당하나) `>=1` 분기가 도달 불가능한 죽은 코드로 남아있고, 라이벌이 근접전에서 여러 차례 타격에 성공해도 매번 "오늘은 여기까지"(원래 "탄약 소진" 뉘앙스의 대사)만 반복되어 맥락상 어색하다 — Minor 1로 기록. |
| 7 | 부분적 클릭관통 해제의 문서화된 한계가 실제로 안전한지 | **문서 주장이 코드와 정확히 일치, 현재 실질적 위험 없음.** `ILocalClickCaptureService.cs` 문서 상단이 "지금 Windows/에디터에서는 전역 클릭관통 자체가 켜져 있지 않다"고 명시한 것을 `Core/StickmanAgent.cs:178-194`(`SetClickThrough` 호출을 `try/catch(NotSupportedException)`로 감싸 로그만 남기고 진행)와 `Platform/Windows/Win32WindowService.cs:157-163`(`_usingUnsafeSelfWindowFallback`이 항상 true라 `SetClickThrough`가 항상 예외를 던짐)로 교차 확인했다. 즉 게임 창이 지금 실제로는 모든 클릭을 정상 수신하는 일반 창 상태이므로, UX 15절이 요구하는 "영역 밖 100% 관통"이 지금 당장 깨질 걱정 자체가 성립하지 않는다(관통 자체가 꺼져 있으므로) — 문서의 "실질적 위험 없음" 주장은 사실이다. |

---

## Major

### BUG-P3-M1 — 4개 스펙터클 Director가 `OnDisable()`/`OnDestroy()`에서 SpectacleEventLock/ILocalClickCaptureService를 반환하지 않아, 소유자 컴포넌트가 비활성화/파괴되면 락이 영구히 풀리지 않는다

- **파일**: `Assets/_Project/Scripts/Interaction/BattleMinigameDirector.cs`, `Assets/_Project/Scripts/Interaction/DragThrowController.cs`, `Assets/_Project/Scripts/Interaction/RivalEncounterDirector.cs`, `Assets/_Project/Scripts/Interaction/RodeoCursorWatcher.cs` — 4개 파일 전부 `OnDisable()`은 있으나(이벤트 구독 해제용) `OnDestroy()`는 어디에도 없고(grep 확인, 매치 0건), `OnDisable()` 어디에도 `SpectacleEventLock.Release`/`ILocalClickCaptureService.ReleaseLocalClickCapture` 호출이 없다.
- **재현 시나리오(코드 레벨로 확정)**:
  1. 어떤 스펙터클 이벤트(예: 격파 미니게임)가 진행 중이라 `BattleMinigameDirector`가 `SpectacleEventLock`(owner=자기 자신)과 `ILocalClickCaptureService`를 쥐고 있다(`TryBegin()`, `:79-100`).
  2. 이 상태에서 `BattleMinigameDirector`가 부착된 GameObject/컴포넌트가 비활성화되거나 파괴된다. 실제로 이런 경로가 존재하는가? — UX_FLOW.md 10절/11절이 각각 "설정(모드 탭)에서 '격파 미니게임 자동발생' 끄기"/"라이벌 대결 이벤트 끄기" 토글을 **Coder 구현 권고사항으로 명시**하고 있고, 이런 토글의 가장 자연스러운 구현 방식이 정확히 해당 Director 컴포넌트의 `enabled`를 끄는 것이다 — 즉 지금 당장 재현되는 버그는 아니지만, 문서가 명시적으로 요청한 다음 기능이 이 정확한 경로를 밟을 가능성이 높다.
  3. `OnDisable()`이 이벤트 구독만 해제하고 반환하므로, `SpectacleEventLock._owner`와 (해당 시) `LocalClickCaptureGate._owner`는 계속 그 죽은/비활성 컴포넌트 참조를 들고 있다.
  4. `SpectacleEventLock.Release(object owner)`/`LocalClickCaptureGate.ReleaseCapture(object owner)`는 "소유자 본인만 해제 가능"(`_owner != owner`면 no-op)하도록 설계되어 있고, 타임아웃이나 관리자 강제 회수(steal) 경로가 전혀 없다(`Core/SpectacleEventLock.cs:44-48`, `Platform/LocalClickCaptureGate.cs:46-51`) — 즉 한 번 이 상태가 되면 **앱을 재시작하지 않는 한 4개 스펙터클 기능(격파/드래그/라이벌/로데오) 전부가 영구히 발동 불가능**해진다(정적 클래스이므로 씬 전환으로도 회복 안 됨).
- **왜 Major인가**:
  - 영향 범위가 이 프로젝트의 상호배제 인프라 전체(4개 기능 공용 락)라 한 기능의 사소한 결함이 아니라 시스템 전체를 마비시킨다.
  - 회복 불가능(타임아웃/steal 메커니즘 없음, 정적 상태라 씬 재로드로도 안 풀림) — 발생하면 반드시 프로세스 재시작이 필요하다.
  - UX_FLOW.md가 명시적으로 요청한 향후 기능(자동발생 끄기 토글)이 정확히 이 경로를 밟을 가능성이 높아, "먼 미래의 이론적 위험"이 아니라 다음 라운드 이후 실제로 밟힐 가능성이 있는 경로다.
  - 반면 Blocker로 올리지 않은 이유: 지금 이 순간 Phase 3 코드 자체만으로는 이 경로를 실제로 유발하는 호출이 어디에도 없다(설정 UI 자체가 아직 미구현) — 즉 지금 당장 앱이 이 버그로 멈추지는 않는다.
- **수정 제안**: 4개 파일의 `OnDisable()`에 "내가 지금 소유자라면 반환" 방어 로직을 추가.
  - `BattleMinigameDirector`/`DragThrowController`: 기존 `OnStateTransitioned` 핸들러가 이미 하는 일(`_clickCapture?.ReleaseLocalClickCapture(this); SpectacleEventLock.Release(this);`)을 `OnDisable()`에서도 호출(멱등이므로 중복 호출해도 안전 — `Release`/`ReleaseLocalClickCapture` 둘 다 이미 no-op 가드가 있음). 단, 이 시점에 상태머신이 여전히 해당 상태(`BattleMinigame`/`Dragged`)에 머물러 있을 수 있으므로, 가능하면 `ChangeState(Idle, isForcedInterrupt:true)`도 함께 호출해 캐릭터가 얼어붙은 중간 상태로 남지 않게 하는 편을 권고.
  - `RivalEncounterDirector`: `if (SpectacleEventLock.CurrentOwner == (object)this) { if (_rival != null) _rival.ForceEndDuel(); else SpectacleEventLock.Release(this); }`.
  - `RodeoCursorWatcher`: 기존 `OnEmergencyStop()`과 동일한 로직을 `OnDisable()`에서도 호출.
  - 파일당 수 줄 규모로 반려 사이클이 길지 않을 것으로 예상.

---

## Minor

1. **`AttackState.ShotsRemaining`이 상시 0으로 고정되어 `>=1` 분기가 죽은 코드 + 라이벌 대결 맥락에서 의미상 어색함** — `States/AttackState.cs:54`. Phase 2 Minor 1(데모 텍스트 불일치)은 해소됐지만, 이번엔 반대 방향으로 파라미터 쪽이 편향됐다. 라이벌 스틱메이트이 근접전 중 몇 번을 타격에 성공하든(각 타격이 독립된 단발 `Attack` 진입) 매번 "오늘은 여기까지"(원래 "탄약/기회 소진"을 뜻하는 대사)만 나와 "한창 싸우는 중"이라는 상황과 어울리지 않는다. 31-1 원칙(같은 함수·같은 스냅샷) 자체는 위반하지 않으므로 급하지 않지만, Phase 3+에서 라이벌 전투에 어울리는 별도 텍스트 매핑(또는 파라미터 갱신 로직)으로 교체를 권고.
2. **라이벌 대결 중 플레이어 쪽은 `Attack` 상태에 전혀 진입하지 않아 "서로 주먹질" 인상이 비대칭적** — `Interaction/RivalStickmanAgent.cs:135-166`(`TickCombatExchange`)의 `rivalStrikes` 분기 중 `true`(라이벌이 선타)일 때만 `TryPlayAttackAnimation()`(`:168-177`, 라이벌 자신의 `_machine.ChangeState(Attack)`)이 호출되고, `false`(플레이어가 선타, 즉 라이벌이 맞는 쪽)일 때는 `RagdollImpactResolver.TryApplyImpact()`만 호출될 뿐 플레이어의 `Blackboard.Machine.ChangeState(StickmanStateId.Attack)`은 코드 전체에서 단 한 번도 호출되지 않는다(grep 확인). UX 11절이 묘사하는 "서로 달려들어... 주먹질·발차기 오가는" 장면과 달리, 지금 로직상 라이벌만 시각적으로/상태적으로 공격 동작을 하고 플레이어는 (렌더링이 아직 없어 눈에 띄지 않지만) 상태머신 레벨에서도 절대 공격하지 않는다. 렌더링 레이어가 붙기 전에 상태머신 레벨에서 먼저 대칭을 맞춰두는 편(플레이어가 선타를 낼 때도 `_opponent.Blackboard.Machine.ChangeState(StickmanStateId.Attack)` 호출)을 권고 — 지금 고쳐두면 이후 애니메이션 작업이 두 배로 늘지 않는다.

---

## 결론

**Blocker 0 / Major 1(BUG-P3-M1) / Minor 2 — Coder로 반려 필요.** BUG-P3-M1은 지금 당장 Phase 3 자체 코드만으로 유발되지는 않지만(설정 UI 미구현), UX 문서가 명시적으로 요청한 다음 기능(자동발생 끄기 토글)이 밟을 가능성이 높은 경로이고 수정 비용이 파일 4개·각 수 줄 규모로 작아 이번 라운드에 선반영을 권고한다. Minor 2건은 급하지 않으므로 Coder 재량으로 다음 라운드 이후 처리해도 무방하다.

**격파 미니게임 self-transition 미반영(중점 점검 1)은 버그가 아니라 예정된 이월**이지만, 다음 Coder 라운드 착수 시 반드시 최우선으로 반영되어야 하며 이 리포트가 그 사실을 공식적으로 재확인한다.

수정 완료 후 재검토 시 확인할 것: (1) BUG-P3-M1 수정이 실제로 `OnDisable()` 경로에서 두 락 모두 해제되는지(코드 레벨 확인으로 충분, Play 모드 불필요), (2) 격파 미니게임 self-transition 반영 시 UX 31-2 표 #5("필살기다!"/"어... 어라?")와 텍스트가 정확히 일치하는지 + `chargeRatio` 스냅샷이 31-1 원칙(같은 함수·같은 Enter() 스냅샷)을 지키는지, (3) 클린 재빌드 기준선(에러 0/경고 0) + 텍스트-액션 회귀 테스트 8/8 통과 유지 여부.

**Phase 3(전투/커서상호작용) — 위 Major 1건 수정 전까지 Phase 4(OS 장난/PC연동) 착수 보류. 수정 후 재검토 필요.**

---

## 반려 수정 재확인 (Debugger, 2026-08-27, 커밋 `3a7bc22` 대상)

좁은 타겟(위 "수정 완료 후 재검토 시 확인할 것" 3개 항목)만 코드 레벨로 재확인했다.

1. **BUG-P3-M1(락 미해제) — 4개 Director 전부 해소 확인.** `BattleMinigameDirector`/`DragThrowController`/`RivalEncounterDirector`/`RodeoCursorWatcher`의 `OnDisable()`이 모두 `ReleaseOwnedLock(s)()`를 호출해 `SpectacleEventLock.Release`/`ILocalClickCaptureService.ReleaseLocalClickCapture`를 직접 반환함을 확인. 두 락 구현(`Core/SpectacleEventLock.cs:44-48`, `Platform/LocalClickCaptureGate.cs:46-51`) 모두 `if (_owner == null || _owner != owner) return;` 가드가 있어 소유자가 아니거나 이미 해제된 상태에서 다시 호출해도 예외 없이 no-op임을 코드로 확인(멱등 보장). 캐릭터가 중간 상태(기 모으기/드래그/로데오)로 얼어붙지 않도록 `ChangeState(Idle, isForcedInterrupt: true)`로 안전 복귀시키는 처리도 4곳 모두 존재.
2. **격파 미니게임 self-transition — Architect 지시대로 반영 확인, 잔여 경로 없음.** `States/BattleMinigameState.cs`의 `TickCharging()`→`TriggerResolution()`은 `chargeRatio` 스냅샷만 필드에 기록하고 `ChangeState(BattleMinigame, isForcedInterrupt:false)`로 자기-전이만 시킬 뿐, `DialogueIntent`를 직접 생성하지 않는다. 실제 판정(성공/실패/재도전/소진)과 "릴리즈 순간" `DialogueIntent`(필살기다!/어... 어라?)는 오직 `Enter()`→`ResolveOutcome()` 경로에서만 만들어짐을 확인 — `Tick()`/`TickResolving()`을 포함해 그 외 경로에 잔여 대사 생성 코드 없음. `StickmanStateMachine.ChangeState()`(`States/StickmanStateMachine.cs:108-132`)는 `next == 현재상태`를 특별 취급하지 않고 항상 `Exit()`→세대 증가→`Enter()` 순서를 그대로 실행하므로 self-transition이 실제로 `Enter()` 재실행을 유발함을 구조적으로 확인했다. **이탈 오판 방지 가드**도 `BattleMinigameDirector.OnStateTransitioned()`에 `if (evt.From != BattleMinigame) return; if (evt.To == BattleMinigame) return;` 형태로 존재 — `From==To==BattleMinigame`(self-transition)일 때는 락을 풀지 않고, 실제로 다른 상태로 빠져나갈 때만 락을 해제함을 확인. `DragThrowState`/`RodeoCursorState`는 애초에 self-transition을 쓰지 않아(grep 확인) 해당 가드가 불필요하며 실제로도 추가되지 않았다 — 과잉 수정 없음.
3. **AttackState.ShotsRemaining / 라이벌 대결 비대칭 — 기존 계약과 충돌 없음.** `AttackState.Enter()`가 이제 `StickmanBlackboard.AttackShotsRemaining`(신규 필드, 기본값 0)을 그대로 읽어 텍스트 분기("한 발 더!"/"오늘은 여기까지")에 쓰며, 세팅하지 않는 다른 호출부는 기존과 동일하게 0을 받아 회귀 없음. `RivalStickmanAgent`에서 라이벌/플레이어 양쪽 모두 타격 직전에 `hitsToLose - hitsTaken - 1` 스냅샷을 계산해 채우는 방식이 대칭적으로 동일함을 확인했고, `_hitsTakenByPlayer`/`_hitsTakenByRival` 증가 시점이 스냅샷 계산 이후라 off-by-one 없음(예: 라이벌이 맞을 차례엔 `TryPlayOpponentAttackAnimation()`이 먼저 플레이어 상태머신에 Attack을 진입시키고, 그 다음 `RagdollImpactResolver.TryApplyImpact()`가 라이벌 자신에게 충격을 적용 — 서로 다른 블랙보드라 순서 문제 없음). `CheckDuelOutcome()`의 승패 판정 로직·`DialogueTextActionSyncTests` 8건은 이 변경과 무관한 범용 목(mock) 상태로 테스트하므로 영향 없음.

**검증 — Unity 배치모드 직접 재실행 결과:**
- 컴파일(`-batchmode -nographics -quit`): `error CS` 0건, `warning CS` 0건, `Batchmode quit successfully`/`Exiting batchmode successfully now` 정상 종료.
- EditMode 테스트(`-runTests -testPlatform EditMode`): `testcasecount="8" result="Passed" total="8" passed="8" failed="0"`, 로그 내 `error CS`/`warning CS` 0건.

**결론 — 3개 항목 모두 문제 없음. Phase 3 최종 승인 — Phase 4(OS 장난/PC연동) 착수 가능**
