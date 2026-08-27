# StickMate — Phase 1 버그 리포트 (3차, 타겟 검토, Debugger, Teammate2)
> 작성: Debugger · 작성일: 2026-08-27 · 대상: 커밋 `6ee9be4`("Phase 1 2차 반려 수정: 자율 배회 AI 도입 + 발판 폴백 안전망")
> 범위: **전면 재검토 아님.** 신규 `States/AutoWanderController.cs`, `States/IMovementIntentSource.cs`, `Platform/FallbackPlatformWindowService.cs`와 수정된 `Core/StickmanAgent.cs`, `Core/StickConfig.cs`, `Core/StickmanEventBus.cs`, `Platform/FootholdPoller.cs`, `States/GroundSensor.cs`, `States/IdleState.cs`, `States/StickmanBlackboard.cs`, `States/StickmanStateMachine.cs`, `States/WalkState.cs`만 적대적으로 재검증.
> 환경: 배치 모드 재컴파일 완료 — `error CS`/`warning CS` 0건 확인, `LogAssemblyErrors (0ms)` 2회, `Batchmode quit successfully`. 단 `Asset File Changes: new=0, changed=0`(Library 캐시가 이미 이 커밋 상태로 최신)라 이번 실행에서는 실제 재컴파일이 아니라 캐시 재사용이었을 가능성이 높다 — 2차 리포트가 보고한 "경고 2건"(RagdollState/GetupState 미사용 필드) 문자열 자체는 이번 로그에 나타나지 않았다(경고 여부가 아니라 로그 표시 여부의 문제로 판단). **에러 0건은 확실하나, 경고 카운트는 이번 로그로 독립 재확인하지 못했다** — 필요 시 `Library/` 삭제 후 클린 컴파일로 재검증 권고(아래 결론에는 영향 없음, 경고 종류 자체가 바뀌었다는 증거는 없음).

## 결론 요약

**Blocker 1건 신규 발견 — Coder로 재반려 필요.** 나머지 중점 점검 항목(1~5)은 전부 스펙/의도대로 정확히 구현되어 있음을 확인했다.

- BUG-P1-B1(발판 0개 무한낙하)과 BUG-P1-B2(키보드 의존 이동)는 코드 레벨에서 실제로 해결됐다(아래 항목별 결론 참고).
- 그러나 이번에 신설된 `AutoWanderController`의 "발판 경계에서 10% 확률로 점프 시도"(UX_FLOW.md 26-2) 기능이, `FallbackPlatformWindowService`가 커버하지 못하는 **새로운 무한 낙하 경로**를 자율적으로(유저 조작 없이) 열어버렸다 — BUG-P1-B1과 동일한 "캐릭터가 화면 밖으로 영구히 사라짐" 실패 모드가 다른 문으로 재발한다. 아래 BUG-P1-R3-B1 참고.

---

## 이번 라운드 중점 점검 항목 결론

| # | 점검 항목 | 결론 |
|---|---|---|
| 1 | `AutoWanderController`가 UX_FLOW.md 26절 스펙과 일치하는가 | **수치 전부 일치.** Idle 2.0~6.0초(`wanderIdleDurationMin/Max`), Walk 1.5~4.0초(`wanderWalkDurationMin/Max`), 방향전환 판정 주기 0.5초·확률 8%(같은 Walk 페이즈 내 최대 1회, `_spontaneousTurnUsedThisPhase`로 강제)、Idle 종료 후 Walk 75%/Idle연장 20%/점프 5%(누적확률 파티션 `[0,0.75) [0.75,0.80) [0.80,1.0)` 정확), 발판경계 90% 정지·10% 점프(`wanderEdgeJumpAttemptChance=0.10`), 화면 자체의 끝에서는 `isTrueScreenEdge` 판정으로 점프확률 강제 0, 지터 ±17.5%(`wanderDurationJitterRatio=0.175`, 스펙 15~20% 범위 내 정중앙값), 앉기/하품 연속 3회+15%(`wanderRestExtendSitChance`), 최초 방향 50:50+화면경계 시 안쪽 강제(`PickDirectionAvoidingEdge`) 전부 코드와 1:1 대조 완료. `wanderLookAroundDurationMin/Max`는 `StickConfig`에 필드만 있고 `AutoWanderController`가 소비하지 않는데, 이는 스펙 26-3이 명시한 대로 "실제 재생은 Phase 2+ 렌더링 레이어 책임, 지금은 트리거 조건만"이라 의도된 설계이지 누락이 아니다. **단, 26-2의 "10% 점프 시도" 자체가 GroundSensor의 경계 판정 방식과 결합해 새로운 Blocker를 만든다 — BUG-P1-R3-B1 참고.** |
| 2 | BUG-P1-B1이 정말 해결됐는가 | **부분 해결.** `StickmanAgent.CreatePlatformService()`(`Core/StickmanAgent.cs:225`)가 `#if UNITY_STANDALONE_WIN` 분기에서 실제로 `new FallbackPlatformWindowService(new Win32WindowService())`를 반환하도록 배선되어 있음을 코드로 직접 확인(클래스 존재 여부가 아니라 실제 호출 경로 확인 완료). `FallbackPlatformWindowService.EnumerateFootholds()`는 내부 서비스가 **완전히 빈 리스트(Count==0)**를 반환할 때만 화면 하단 합성 발판으로 대체한다 — 원래 BUG-P1-B1 재현 시나리오("모든 창 최소화 → 발판 0개")는 확실히 해결됨. 그러나 **발판이 1개 이상 있지만 서로 떨어져 있는 경우(대부분의 실제 데스크톱)의 "발판 사이 틈으로 낙하"는 이 데코레이터가 감지하지 못한다** — 상세는 BUG-P1-R3-B1. `AutoWanderController`의 경계-점프-시도(10%) 경로가 착지 실패 시 이 폴백으로 안전하게 처리되는지 직접 추적한 결과, **안전하게 처리되지 않는 경로가 실존함**을 확인했다. |
| 3 | BUG-P1-B2가 정말 해결됐는가 | **완전 해결, 직접 검증 완료.** `grep -rn "GetAxisRaw\|GetButtonDown\|GetAxis(\|GetButton(\|GetKeyDown\|GetKey("` 결과 프로젝트 전체에서 실제 호출 코드는 0건 — 유일한 매치는 `IMovementIntentSource.cs`/`StickmanBlackboard.cs`의 **주석**(과거 버그를 설명하는 문서화 목적) 뿐이다. `NullPlatformWindowService.cs`에 `Input.mousePosition` 사용이 있으나 이는 이동 입력이 아니라 `ICursorPositionService`(9절-3, 커서 좌표 조회 — 이동과 완전히 별개 채널)의 에디터 폴백 구현이라 무관함. `StickmanBlackboard.MoveInputX`/`JumpPressed`는 필드가 아니라 `IntentSource`(=`AutoWanderController`)를 읽는 계산된 프로퍼티로 전환되어 있어 키보드로 되돌아갈 구조적 여지 자체가 없다. |
| 4 | 새로운 회귀 — AutoWanderController와 GroundedTick/CheckScreenBoundsOrFall 상호작용, 겹친 발판 일관성 | **회귀 1건 발견(BUG-P1-R3-B1, 위 참고).** 그 외 프레임 순서 자체는 안전함을 확인: `StickmanAgent.Update()`는 `_footholdPoller.Tick(dt)` → `_autoWander.Tick(dt)`(펄스 계산) → `_machine.Tick(dt)`(소비) 순서로, `AutoWanderController`와 활성 State가 같은 프레임에 호출하는 `blackboard.SenseGround()`는 그 사이 `Body.position`/캐시된 발판이 변하지 않으므로 항상 같은 결과를 본다(경합 없음). `JumpRequested` 펄스는 `Tick()` 시작 시 매번 `false`로 리셋 후 해당 프레임에만 조건부로 `true`가 되므로 1프레임 펄스 계약(26-7)도 지켜짐 — 다음 착지 즉시 재점프 같은 이중 발동 위험 없음. **겹친 발판**: `GroundSensor.Sense()`의 for 루프는 `grounded=true`가 확정된 이후에도 좌우 경계 누적(`minLeftOs`/`maxRightOs`)은 계속하지만 "현재 발판" 재판정은 생략(`if (grounded) continue;`)하므로, 리스트에서 **먼저 매칭되는 발판**(플랫폼 서비스가 반환하는 순서, 대체로 Z-order 최상단 우선)이 결정적으로 "현재 발판"이 된다 — 매 호출 항상 같은 입력에 같은 결과를 내므로 계산 자체는 일관적이다. 다만 폴링 주기(0.5초) 사이에 Z-order가 바뀌면 다음 폴링 시점에 "현재 발판"이 순간 전환될 수 있다는 점은 2차 리포트 Minor m3에서 이미 인지된 사안 그대로이며 이번 라운드에서 악화되지 않았다. |
| 5 | `StickmanStateMachine` 생성자/`Start()` 분리(M2)가 실제로 올바르게 배선됐는가 | **정확히 배선됨, 직접 확인 완료.** `Core/StickmanAgent.cs:108-110`: `_machine = new StickmanStateMachine(states); _blackboard.Machine = _machine; _machine.Start(StickmanStateId.Idle);` — 2차 리포트가 제시한 수정안과 글자 그대로 일치하는 순서(생성 → 배선 → 활성화)로 구현되어 있다. `StickmanStateMachine` 생성자는 `_current = null`만 설정하고 `ChangeState`를 호출하지 않으며, `Start()`는 2회 호출을 `Debug.LogError`로 방어한다. |

---

## Blocker

### BUG-P1-R3-B1 — `AutoWanderController`의 "발판 경계 10% 점프 시도"가 발판 사이 틈에서 새로운 무한 낙하를 자율적으로 유발함 (BUG-P1-B1 안전망의 사각지대)

- **파일:라인**: `Assets/_Project/Scripts/States/AutoWanderController.cs:198-219`(`IsNearFootholdEdge`/`TickMoving`의 10% 점프 분기), `Assets/_Project/Scripts/States/GroundSensor.cs:30-34, 86-88`(`ScreenLeftWorldX`/`ScreenRightWorldX`는 모든 발판을 통틀어 min/max로만 계산 — 발판 사이 틈을 표현하지 못함), `Assets/_Project/Scripts/Platform/FallbackPlatformWindowService.cs:50-55`(`EnumerateFootholds()`가 `real.Count > 0`이면 무조건 실제 목록을 그대로 통과시킴), `Assets/_Project/Scripts/States/StickmanBlackboard.cs:121-135`(`CheckScreenBoundsOrFall`), `Assets/_Project/Scripts/States/FallState.cs:37-56`.
- **재현 시나리오**:
  1. 데스크톱에 서로 떨어진 두 창(발판) A(예: x=0~100)와 B(예: x=500~600)가 열려 있다고 하자. 흔한 실사용 조건이다 — 창 두 개가 화면 가장자리에 나란히 딱 붙어 있는 경우는 오히려 드물다.
  2. `GroundSensor.Sense()`는 `ScreenLeftWorldX`/`ScreenRightWorldX`를 **모든 발판을 통틀어** `minLeftOs`/`maxRightOs`(여기선 x=0, x=600)로만 계산한다(`GroundSensor.cs:86-88`) — A와 B 사이 x=100~500 구간에 발판이 없다는 사실 자체를 표현할 방법이 이 구조체에 없다.
  3. 캐릭터가 A 위를 걷다 오른쪽 끝(x=100)에 도달하면 `AutoWanderController.IsNearFootholdEdge()`가 `isTrueScreenEdge = |CurrentFootholdRightWorldX(100) - ScreenRightWorldX(600)| <= 0.01` → **false**로 판정한다(B가 더 멀리 있으므로 "화면의 진짜 끝"이 아니라고 판단) — 즉 26-2 표 마지막 행("화면 자체의 좌우 끝에서만 점프 확률 강제 0")의 면제 대상이 되어 `wanderEdgeJumpAttemptChance`(10%)가 그대로 적용된다.
  4. 10% 확률에 당첨되면 `_jumpRequestedThisTick = true`로 진행 방향을 유지한 채 점프 펄스를 발동한다. 실제 `JumpState`는 `Body.linearVelocity = (MoveInputX * walkSpeed, jumpForce)`로 A→B 방향 점프 궤적을 만들지만, A와 B 사이 간격(400유닛, 사실상 임의의 간격)이 점프로 도달 가능하다는 보장은 어디에도 없다 — 오히려 desktop 창 배치는 매우 다양해 대부분의 경우 도달 불가능하다.
  5. 점프가 B에 닿지 못하면 `JumpState`는 정점 통과 후 `FallState`로 전이한다. `FallState.Tick()`은 `CheckScreenBoundsOrFall(info)`(x가 `ScreenLeftWorldX`(0)~`ScreenRightWorldX`(600) 안에 있는지만 검사)와 `info.Grounded`만 확인한다 — **캐릭터의 X좌표는 A와 B 사이의 빈 틈(예: x=250)에서도 여전히 [0, 600] 구간 안이므로 `CheckScreenBoundsOrFall`은 계속 `false`를 반환**하고, 그 아래에는 어떤 발판도 없으므로 `info.Grounded`도 영원히 `false`다.
  6. 프로젝트 전체에 Y축(수직) 낙하 하한을 검사하는 코드가 전혀 없음을 확인했다(`GroundWorldY`/`ScreenBottom`/`position.y <` 등 패턴으로 grep, 매치 0건). `Respawn`/`ResetPosition` 등 복구 메커니즘도 전무하다(grep 확인). **결과: 캐릭터가 Y축으로 영원히 낙하하며 화면 밖으로 사라지고, 앱을 재시작하기 전까지 스스로 복구할 방법이 없다.**
  7. `FallbackPlatformWindowService.EnumerateFootholds()`(BUG-P1-B1의 수정)는 `real.Count > 0`이면 무조건 실제 목록을 그대로 통과시키므로(`FallbackPlatformWindowService.cs:52-53`), A/B가 둘 다 여전히 열려 있는 이 시나리오에서는 폴백이 전혀 개입하지 않는다 — **BUG-P1-B1이 막으려던 것과 정확히 같은 증상(무한 낙하)이 그 수정의 적용 범위 바로 바깥에서 재발**한다.
- **왜 이것이 BUG-P1-B1/B2와 같은 급의 Blocker인가**:
  - **자율적으로, 유저 조작 없이 발생한다.** `AutoWanderController`는 24시간 자율 운행되며, Walk 페이즈마다 발판 경계에 도달할 때 매번 10% 확률 룰렛이 돌아간다. 유저가 실수로 어떤 조작을 해야 재현되는 게 아니라, 앱을 충분히 오래 켜두면 통계적으로 거의 필연적으로 발생한다(A/B처럼 떨어진 발판이 하나라도 존재하는 흔한 데스크톱 환경이라면).
  - **결과가 회복 불가능하다.** 착지 재시도/타임아웃/화면 밖 이탈 시 재소환 등 어떤 자가 복구 로직도 없어, 한 번 발생하면 프로세스를 재시작할 때까지 캐릭터가 영구히 사라진 채로 남는다 — UX_FLOW.md 6-1절이 "절대 금지"로 못박은 바로 그 실패 모드.
  - **UX_FLOW.md 26-7 스스로도 이 순서 의존성을 명시**했다("착지할 발판이 없으면 Fall로 이어지고, BUG-P1-B1 수정(화면 하단 안전망)이 반드시 선행되어야 '허공에 뜬 채 사라짐'으로 보이지 않는다") — 그러나 실제 구현된 BUG-P1-B1 수정은 "발판이 하나도 없을 때"만 다루지, "발판은 있지만 그 사이 틈에 착지할 곳이 없을 때"는 다루지 않아, 문서가 요구한 선행조건이 충족되지 않은 채로 26-2 기능이 구현됐다.
- **근본 원인**: `GroundSensor.GroundInfo`의 `ScreenLeftWorldX`/`ScreenRightWorldX`가 "모든 발판의 합집합 바운딩 박스"로 정의되어 있어 발판 사이의 틈(gap)을 표현하지 못한다. `FallbackPlatformWindowService`는 이 구조를 그대로 신뢰해 "실제 목록이 비어있지 않으면 안전하다"고 가정하는데, 그 가정이 26-2의 신규 "경계 점프 시도" 기능과 만나면서 깨진다.
- **수정 제안**(우선순위순):
  1. **가장 확실한 수정**: `FallbackPlatformWindowService`가 발판이 하나라도 있을 때도 화면 최하단 합성 발판을 **항상 추가로 덧붙이도록** 바꾼다(현재처럼 "0개일 때만 대체"가 아니라 "실제 목록 + 바닥 안전망 1개, 매번"). 이렇게 하면 어떤 X좌표로 낙하하든 결국 화면 바닥에서 착지가 보장되어 BUG-P1-R3-B1과 BUG-P1-B1을 동일한 메커니즘으로 함께 닫는다. 모바일(`ScreenshotBackdropPlatformService`)에는 여전히 이 데코레이터를 적용하지 않으므로 온보딩 게이트 우회 우려도 없다(기존 설계 의도 그대로 유지). 부작용: `ScreenLeftWorldX`/`ScreenRightWorldX`가 사실상 항상 화면 전체 폭이 되어 "화면 경계 이탈" 판정 자체는 더 이상 거의 트리거되지 않게 되는데, 이는 오히려 올바른 방향이다(진짜 화면 물리적 끝에서만 이탈로 봐야 함).
  2. **대안(더 정밀하지만 비용 큼)**: `AutoWanderController`의 10% 점프 판정 시점에 착지 가능성을 미리 계산(포물선 사거리 vs 다음 발판까지 거리)해, 도달 불가능하면 점프 대신 90% 분기(정지+반전)로 강제 폴백. `GroundSensor`에 "진행 방향으로 가장 가까운 다음 발판까지의 거리"를 새 필드로 추가해야 해서 26-7이 이미 요청한 `CurrentFootholdLeftWorldX`/`RightWorldX` 확장과 비슷한 규모의 작업이 추가로 필요하다.
  3. 둘 중 하나는 **Phase 2 착수 전 필수** — (1)이 훨씬 저렴하고 BUG-P1-B1의 원래 취지(화면 하단 안전망)와 정확히 같은 개념이라 우선 권고한다.

---

## Major / Minor

이번 라운드에서는 신규 Major/Minor를 발견하지 못했다(타겟 검토 범위 내). 2차 리포트의 기존 Major 6건 중 이번 커밋으로 처리된 항목(M2/M3/M4/M5/M6)은 위 중점 점검 결론 대로 정확히 반영되어 있었다. M1(`Camera.main` 캐싱)은 `Awake()`에서 `Debug.LogError`로 경고만 추가되고 재획득 로직은 여전히 없는데, 이는 2차 리포트가 이미 "즉시 반려 사유는 아님"으로 분류한 항목이라 이번 라운드에서 재격상하지 않는다.

---

## 가설/실측 필요 (이월)

Windows 실기기/실빌드 환경 부재로 H1~H6은 여전히 미검증 상태로 이월. `docs/BUG_REPORT_PHASE1.md` 하단 및 `Tasklist.md` 과학적 토론 로그 참고.

## 결론

**BUG-P1-R3-B1(Blocker) 1건 해결 전까지 Phase 2 착수 보류 — Coder로 재반려.** 이 항목을 제외한 나머지(BUG-P1-B1/B2 본연의 재현 시나리오, M2 배선, 신규 파일 3종의 스펙 정합성)는 전부 검증 통과했다. BUG-P1-R3-B1의 수정안 1번(`FallbackPlatformWindowService`가 항상 바닥 안전망을 덧붙이도록 변경)은 파일 하나, 수십 줄 규모로 예상되어 반려 사이클이 길어지지 않을 것으로 판단한다.
