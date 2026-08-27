**Major 1건 발견 — Coder로 반려 필요**

# StickMate — Phase 2 버그 리포트 (Debugger, Teammate2)
> 작성: Debugger · 작성일: 2026-08-27 · 대상: 커밋 `2ce8c95`("Phase 2: Active Ragdoll + ParkourClimb + DialogueIntent 파라미터 파이프라인")
> 범위: Phase 2 신규/수정 파일 전체 — `States/RagdollRig.cs`, `States/RagdollState.cs`, `States/GetupState.cs`, `States/ParkourClimbState.cs`, `States/GroundSensor.cs`(신규 2메서드), `States/StickmanBlackboard.cs`, `States/IStickmanState.cs`(struct→class 전환), `Dialogue/DialogueIntent.cs`, `Dialogue/IHasDialogueParams.cs`, `Core/StickmanAgent.cs`(`ReportExternalImpact`), `Core/RagdollLimbImpactRelay.cs`, `States/AttackState.cs`, `States/FallState.cs`, `States/WalkState.cs`, `Core/StickmanEventBus.cs`, `Core/StickConfig.cs`.
> 환경: Unity 배치모드 컴파일 2회 실행. ① 1차: `Library/` 캐시가 이미 이 커밋 상태로 최신이라 `0 items updated`(캐시 재사용, 참고용). ② 2차(독립 검증): `Library/ScriptAssemblies`/`Library/Bee`/`Library/PlayerDataCache`를 강제 삭제한 뒤 재실행 — `154 items updated`, `script compilation time 3.199845s`로 실제 재컴파일임을 확인. 두 실행 모두 `error CS`/`warning CS` 매치 0건, `Batchmode quit successfully`/`Exiting batchmode successfully now` 정상 종료. **클린 재빌드로 "에러 0/경고 0" 기준선을 독립 재확인 완료** — 커밋 메시지가 주장한 "기존 경고 2건 해소"와 일치.

## 결론 요약

**Blocker 0건, Major 1건(BUG-P2-M1), Minor 5건.**

- 중점 점검 1(토큰 소비 경로), 2(ReportExternalImpact 단일 진입점/재인터럽트 리셋), 4(파쿠르 벽 감지·좌표계·매 프레임 재확인·WalkState 배회AI 경합), 5(낙하높이/충격량 축 분리), 6의 Ragdoll·ParkourClimb 매핑은 전부 스펙/의도대로 정확히 구현되어 있음을 코드로 직접 확인했다.
- 3(RagdollRig 파츠 0개 안전성)은 NRE 없이 안전하나, 그 결과로 생기는 부수 동작(파츠 0개일 때 즉시 Getup 자동 전이)을 Minor로 기록한다.
- **신규 발견: BUG-P2-M1** — `ParkourClimbState.Tick()`이 매 프레임 `Body.position.y`만 직접 덮어쓰고 `Body.linearVelocity.y`는 `Enter()` 시점 1회만 0으로 초기화한다. 등반 중 중력이 계속 `linearVelocity.y`에 조용히 누적되고, 등반 완료로 Idle/Walk에 복귀하는 순간부터 다음 프레임 `GroundedTick`(→`SnapToGround`)이 이를 상쇄하기 전까지 최소 1 FixedUpdate 동안 그 숨은 속도가 실제로 적용되어 착지 순간 눈에 띄는 "튐(pop)"이 발생할 수 있다. 이 상태 자신의 코드 주석("이 상태 동안은... 중력에 의한 낙하는 발생하지 않는다")과 UX_FLOW.md 4절("급격한 포즈 점프(teleport처럼 보이는 것)는 UX 결함으로 간주")을 모두 위반하며, 매 등반마다 100% 재현되는 구조적 결함이다.
- 6(DialogueIntent 대사 매핑)에서 Ragdoll/ParkourClimb 2곳은 UX_FLOW.md 31-2 표와 텍스트까지 정확히 일치하지만, **AttackState는 텍스트가 표와 다르다**("한 발 더!"/"오늘은 여기까지" 대신 "N발 더!"/"타앗!") — Minor 1로 기록(원칙 위반은 아니고 데모용 문구 차이).

---

## 권고 순서

1. **BUG-P2-M1 수정** — `ParkourClimbState.Tick()`에서 `Body.position.y`를 설정할 때마다 `Body.linearVelocity.y`도 0으로 재확정(가장 간단), 또는 `Enter()`에서 `Body.gravityScale`을 0으로 낮췄다가 `Exit()`/전이 시 원래 값으로 복원. 한 줄~두 줄 규모로 예상되어 반려 사이클이 길지 않을 것.
2. Minor 5건은 급하지 않음 — 특히 Minor 1(AttackState 텍스트)은 Phase 3에서 실제 전투 로직이 들어올 때 자연스럽게 다시 손댈 곳이라 지금 급히 고칠 필요는 없으나, UX 31-2 표와 다르다는 점은 기록해둔다.
3. 가설 H7(GETUP P-control 게인 안정성)은 실제 캐릭터 프리팹/관절 배선 이후 실측 필요 — 정적 검토로는 확답 불가, `Tasklist.md` 과학적 토론 로그에 이월.

---

## 이번 라운드 중점 점검 항목 결론

| # | 점검 항목 | 결론 |
|---|---|---|
| 1 | `StateTransitionContext` class 전환 + 1회용 토큰이 모든 `DialogueIntent` 생성 경로에서 빠짐없이 소비되는가 | **정확히 구현됨, 우회 경로 없음.** `grep -rn "new StateTransitionContext("`으로 프로젝트 전체를 확인한 결과 생성 지점은 `StickmanStateMachine.ChangeState()`(`States/StickmanStateMachine.cs:127`) 단 한 곳뿐이다(생성자가 `internal`이라 이 외에는 컴파일 자체가 안 됨). `DialogueIntent`의 두 생성자(`Func<StickmanStateId,string>` 편의 오버로드 — `Dialogue/DialogueIntent.cs:74-77`, `Func<StickmanStateId,object,string>` 본체 — `:96-136`)는 편의 오버로드가 `WrapSimpleTextFunc`로 감싸 항상 본체 생성자에 위임하므로(`:75`), **모든 `DialogueIntent` 생성이 정확히 한 곳(`:116`의 `context.TryConsumeToken()`)을 통과한다.** 두 번째 시도는 `InvalidOperationException`으로 확실히 차단됨을 코드로 확인. 문서가 스스로 인정한 잔여 한계(asmdef 미분리로 같은 어셈블리 내부가 `internal` 생성자를 직접 호출해 새 컨텍스트를 조작하는 것 자체는 방지 못함)도 grep 결과와 정확히 일치 — 실제로 `new StateTransitionContext(` 호출은 위 1곳 외에 존재하지 않아, 문서화된 한계가 "이론상 가능하나 현재 코드베이스에는 그런 위조 코드가 없다"는 사실과 부합한다. |
| 2 | `ReportExternalImpact` 단일 진입점의 가드/공식/재인터럽트 리셋 | **가드 충분, 공식은 내부적으로 일관, 리셋 보장됨.** `Core/StickmanAgent.cs:59-67` — `_isSuspended \|\| _machine == null \|\| _config == null` 가드 후 `impulseMagnitude < ragdollForceThreshold`면 조기 반환, 임계값 이상이면 `_blackboard.LastImpactMagnitude`를 먼저 스냅샷한 뒤 `ChangeState(Ragdoll, isForcedInterrupt: true)` — 순서가 안전하다(스냅샷이 항상 전이보다 먼저). `impulseMagnitude = collision.relativeVelocity.magnitude * _body.mass`(`:72`, `RagdollLimbImpactRelay.cs:39`도 동일 공식)는 엄밀히는 "힘(force)"이 아니라 운동량(momentum) 근사치이지만, `ragdollForceThreshold`(기본 8)와 항상 같은 단위로 비교되므로 내부적으로 일관되고, `walkSpeed=2.5`/`jumpForce=6` 대비 오더가 비슷해 "세게 부딪힘"을 가르는 값으로 합리적인 범위다(실제 물체 질량/속도는 Phase 3 커서 던지기 이후 실측 튜닝 필요, 지금은 정적 검토로 확답 불가). Getup 도중 재인터럽트 시 `_settleTimer` 리셋은 별도 코드 없이 `StickmanStateMachine.ChangeState()`(`:108-132`)가 next와 현재 상태가 같든 다르든 항상 `_current?.Exit()` 후 `_current.Enter(context)`를 실행하고, `RagdollState.Enter()`(`:52-54`)가 매번 `_settleTimer = 0f`로 리셋하므로 **구조적으로 보장됨**을 코드로 직접 확인했다. |
| 3 | `RagdollRig`가 파츠 0개(프리팹 미배선) 상태에서 안전한가 | **NRE 없음, 안전.** 생성자(`States/RagdollRig.cs:27-32`)는 `root == null`이어도 `System.Array.Empty<T>()`로 폴백하고, 모든 순회 루프(`EnterRagdoll` `:40-44`, `GetMaxSpeed` `:54-60`, `BeginGetup` `:67-70`, `TickGetup` `:81-94`)가 개별 원소 `null` 체크와 함께 for 루프를 쓰므로 배열이 비어 있으면 그냥 0회 반복하고 끝난다. `GetMaxSpeed()`는 파츠 0개일 때 `0f`를 반환(`:54` `max` 초기값이 그대로 반환됨) — 이 값은 항상 `ragdollSettleSpeedThreshold`(기본 0.3) 이하이므로, 실제 프리팹이 배선되지 않은 지금 상태로 RAGDOLL에 진입하면 아무 물리 반응 없이도 `ragdollSettleHoldDuration`(0.5초) 경과 즉시 Getup으로 자동 전이된다 — 이는 버그가 아니라 "프리팹이 아직 없다"는 Phase 2 스코프 밖 사실의 자연스러운 결과이지만, Minor 3으로 기록해둔다(아래 참고). `TickGetup`의 P-제어 게인(`getupMotorGain=6`, `getupMaxMotorTorque=50`)이 실제 관절 관성/댐핑에서 발산하지 않는지는 실제 조인트 배치가 없어 정적 검토로 확답 불가 — 가설 H7로 이월. |
| 4 | 파쿠르 벽 감지 좌표계 일관성 / 매 프레임 재확인 / WalkState-배회AI 경합 | **셋 다 통과.** 좌표계: `GroundSensor.TryFindClimbableWall`(`:140-181`)과 `TryGetFootholdTopWorldY`(`:188-204`) 모두 `ScreenCoordinateConverter.WorldToOsScreen`/`OsScreenToWorld`만 거치고 직접 화면 좌표식을 만들지 않음(BUG-M5 컨벤션 준수 재확인, `States/*.cs`가 좌표 변환식을 직접 만드는 곳 0건 grep 확인). 재확인: `ParkourClimbState.Tick()`(`:90-97`)이 매 프레임 `TryGetFootholdTopWorldY(_wallHandle, ...)`를 호출해 실패하면 즉시 `ChangeState(Fall)` — "잡을 곳이 사라지면 즉시 Fall"이 문자 그대로 매 프레임 재확인됨. WalkState 경합: `AutoWanderController.Tick()`(발판 경계 10% 점프 펄스, `AutoWanderController.cs:198-219`)과 `WalkState.Tick()`(`:38-56`)의 소비 순서를 `StickmanAgent.Update()`에서 직접 추적 — `_autoWander.Tick(dt)` → `_machine.Tick(dt)` 순서로 같은 프레임 내에서 펄스가 생성되고 그 즉시 소비되며, `JumpRequested`는 매 `Tick()` 시작 시 리셋되는 1프레임 펄스라 이중 소비 위험이 없다. `WalkState.Tick()`이 `info.Grounded`일 때만 벽 탐지를 시도하므로 공중(코요테 타임)에서 벽을 잡는 오작동도 없다. `ParkourClimbState.Enter()`가 `_direction`/`_hasWall`을 자체적으로 다시 계산하지만(`:50-55`), 같은 프레임 내 `Body.position`/발판 캐시가 변하지 않는 결정적 함수라 `WalkState.Tick()`이 찾은 것과 항상 같은 결과가 나옴을 확인(재계산 자체는 Minor 4로 기록). |
| 5 | 낙하높이(연출) vs 충격량(RAGDOLL 전이) 축 분리 결정이 실제 구현과 일치하는가 | **정확히 일치.** `FallState.Tick()`(`:57-65`)의 `LandingRollRequested` 경로는 `StickmanEventBus.RaiseLandingRollRequested(fallHeight)` 호출 **한 줄**뿐이고, `ReportExternalImpact`/`ChangeState`를 전혀 호출하지 않는다 — 순수 이벤트 발행이며 상태 전이를 유발하지 않음을 코드로 확인. `StickmanEventBus.LandingRollRequested`(`Core/StickmanEventBus.cs:104,124-125`)도 구독자 없이 발행만 하는 구조. 두 축이 자동으로 섞이지 않는 이유도 코드로 재확인됨: 발판(foothold)은 `GroundSensor`가 좌표 비교로만 판정하는 가상 개념이라 `Collider2D`가 없고, 착지는 `SnapToGround`(위치 강제 이동)로 처리되므로 `StickmanAgent.OnCollisionEnter2D`/`RagdollLimbImpactRelay.OnCollisionEnter2D`(물리 충돌 콜백 기반, RAGDOLL 강제 진입의 유일한 트리거)는 착지 시 원천적으로 발동하지 않는다. Architect 결정(Tasklist.md, "두 축을 합치지 않는다")과 정확히 일치하는 구현. |
| 6 | DialogueIntent 파라미터 실전 연결(Attack/Ragdoll/ParkourClimb)이 UX 31절 표와 일치하는가 | **Ragdoll/ParkourClimb는 텍스트까지 완전 일치, Attack은 텍스트가 다름(Minor 1).** `RagdollState.Enter()`(`:64-71`)의 "윽...!"/"으악!"/"으아아아악?!" 3구간 임계값(2.0/4.0)이 UX 31-2 표와 정확히 일치. `ParkourClimbState.Enter()`(`:72-77`)의 "가뿐하네"/"헉... 높다" 임계값(2.0)도 정확히 일치. **`AttackState.Enter()`(`:48-53`)는 표의 리터럴 문자열("한 발 더!"/"오늘은 여기까지")과 다른 텍스트("{N}발 더!"/"타앗!")를 생성한다** — 상세는 Minor 1. 세 상태 모두 파라미터 대입(`_dialogueParams.X = ...`)이 `DialogueIntent` 생성 직전, 같은 `Enter()` 호출 안에서 동기적으로 일어나므로 "같은 매핑 함수·같은 스냅샷" 원칙(31-1) 자체는 세 곳 모두 위반하지 않음 — 이후 별도 이벤트/타이머로 파라미터를 다시 조회해 텍스트를 바꾸는 경로는 어디에도 없다(캐스팅 실패 시 항상 파라미터 무관한 "안전한 쪽" 기본값으로 수렴하도록 방어적으로 작성돼 있음도 확인). Getup(#3, `reimpactCount`)은 `Tasklist.md`에 이미 근거와 함께 "이번 라운드 제외"로 정직하게 기록되어 있어 버그로 잡지 않음(GetupState.cs:40의 TODO 주석과 일치). |

---

## Major

### BUG-P2-M1 — ParkourClimb 등반 중 `Body.linearVelocity.y`가 재확정되지 않아 중력이 조용히 누적, 등반 완료 직후 착지 튐(pop) 발생

- **파일:라인**: `Assets/_Project/Scripts/States/ParkourClimbState.cs:57-65`(Enter의 1회성 속도 제로화), `:82-113`(Tick — `Body.position.y`만 매 프레임 덮어쓰고 `linearVelocity`는 손대지 않음), 비교 대상: `Assets/_Project/Scripts/States/StickmanBlackboard.cs:113-127`(`SnapToGround` — 위치를 옮길 때마다 `linearVelocity.y`도 함께 재확정하는 이 프로젝트의 기존 관행).
- **재현 시나리오(코드 레벨로 확정, 실측 불필요)**:
  1. `ParkourClimbState.Enter()`(`:57-65`)가 `Body.linearVelocity`를 `(0,0)`으로 **딱 한 번** 초기화한다.
  2. `Body`는 `RequireComponent(typeof(Rigidbody2D))`로만 확보된 일반 Dynamic Rigidbody2D이고(`StickConfig.gravityScale`이 Rigidbody2D 자체 설정으로 중력을 건다는 것은 `FallState.cs:7` 주석이 명시), 코드 어디에도 `bodyType`/`isKinematic`을 조작하는 곳이 없다(grep 확인, 매치 0건) — 즉 물리 엔진은 `ParkourClimbState`가 활성인 동안에도 매 `FixedUpdate`마다 정상적으로 중력을 `linearVelocity.y`에 계속 더한다.
  3. `ParkourClimbState.Tick()`(`:103-105`)은 매 프레임 `Body.position`을 `Mathf.Lerp(_startWorldY, _wallTopWorldY, _climbProgress)`로 **직접 덮어쓸 뿐**, `linearVelocity`는 전혀 건드리지 않는다. `Update()`가 매 프레임의 모든 `FixedUpdate` 이후에 실행되므로 등반 도중 렌더링되는 위치 자체는 항상 Lerp 곡선대로 정확하다 — 즉 **등반 도중에는 시각적으로 아무 문제가 없다.** 그러나 그 사이 `linearVelocity.y`는 화면에 보이지 않는 채로 `parkourClimbDuration`(기본 0.5초) 내내 중력만큼 계속 음수로 누적된다(예: `gravityScale=3`, 표준 중력 근사 시 0.5초 동안 대략 -15 유닛/초 안팎까지 누적 가능 — 정확한 값은 실제 `Physics2D.gravity`/`gravityScale` 조합에 따라 달라지지만, 부호와 누적 방향은 코드만으로 확정적임).
  4. `_climbProgress >= 1f`가 되는 프레임(`:107-112`)에 `ChangeState(Idle 또는 Walk)`가 호출된다. `IdleState.Enter()`(`IdleState.cs:26-32`)는 `linearVelocity.x`만 0으로 만들고, `WalkState.Enter()`는 아예 비어 있다 — **둘 다 `linearVelocity.y`를 건드리지 않는다.**
  5. 전이는 이 프레임의 `_machine.Tick(dt)` 호출 안에서 일어나므로, 새 상태의 `Tick()`은 **이번 프레임에는 실행되지 않는다**(다음 `Update()`에서야 실행). 그 사이에 적어도 한 번의 `FixedUpdate`가 먼저 실행되면, 물리 엔진이 등반 내내 숨겨져 있던 큰 음수 `linearVelocity.y`를 실제로 위치에 적용해버린다 — 이때는 `ParkourClimbState`처럼 매 프레임 위치를 강제로 되돌려주는 코드가 더 이상 없으므로(Idle/Walk는 위치를 직접 설정하지 않음), 캐릭터가 등반이 끝나자마자 눈에 띄게 아래로 "픽" 튀는 결과가 나온다. 다음 `Update()`의 `Tick()`이 `GroundedTick()`→`SnapToGround()`(`StickmanBlackboard.cs:113-127`, 접지 중이면 `linearVelocity.y<0`을 0으로 재확정)를 호출해 뒤늦게 이 값을 잡아주지만, 이미 최소 한 프레임만큼의 원치 않는 하강은 발생한 뒤다.
- **왜 Major인가**:
  - 이 상태 자신의 주석(`ParkourClimbState.cs:59`, "이 상태 동안은... 중력에 의한 낙하는 발생하지 않는다")이 스스로 한 약속을 실제 구현이 지키지 못한다 — 의도와 코드가 어긋나는 명백한 결함.
  - **매 등반마다 100% 재현된다**(엣지 케이스가 아님) — Phase 2의 두 헤드라인 기능 중 하나(ParkourClimb)의 마무리 동작에서 항상 발생.
  - UX_FLOW.md 4절이 명시적으로 "급격한 포즈 점프(teleport처럼 보이는 것)는 UX 결함으로 간주"라고 못박은 원칙과 정확히 같은 종류의 문제(정도는 "허공에 붕 뜬 채 멈춤"보다 작지만, "의도치 않은 순간적 위치 이동"이라는 본질은 동일).
  - 이 프로젝트에 이미 존재하는 관행(`SnapToGround`가 위치를 옮길 때마다 반드시 대응하는 속도 성분도 함께 재확정)과 비교하면 `ParkourClimbState.Tick()`만 그 짝을 빠뜨린 것이 뚜렷이 드러나, "설계상 몰랐던 문제"라기보다 "이 함수 하나에서만 빠진 패턴"에 가깝다.
  - 그러나 상태머신 자체가 깨지거나(좀비 상태, 무한루프, NRE) 회복 불가능한 것은 아니고 다음 프레임에 `SnapToGround`가 스스로 상쇄하므로 Blocker는 아니다.
- **수정 제안(우선순위순)**:
  1. **가장 간단**: `ParkourClimbState.Tick()`에서 `pos.y`를 설정하는 곳(`:104-105`) 바로 아래에 `Vector2 v = _blackboard.Body.linearVelocity; v.y = 0f; _blackboard.Body.linearVelocity = v;`를 추가해 매 프레임 재확정(Enter()의 1회성 초기화를 Tick() 전체로 확장하는 것뿐이라 기존 패턴과 일관됨).
  2. **대안**: `Enter()`에서 `_savedGravityScale = Body.gravityScale; Body.gravityScale = 0f;`로 등반 동안 중력 자체를 끄고, `Exit()`에서 원래 값으로 복원(캐릭터가 "스스로 위치를 제어"한다는 주석의 의도를 물리적으로 더 정확히 구현하지만, `Exit()`가 항상 호출되도록(강제 인터럽트 포함) 보장하는 추가 규율이 필요).
  3. 어느 쪽이든 파일 하나, 수 줄 규모로 반려 사이클이 길어지지 않을 것으로 판단.

---

## Minor

1. **AttackState 데모 텍스트가 UX_FLOW.md 31-2 표의 리터럴 문자열과 다름** — `States/AttackState.cs:48-53`: 표는 `shotsRemaining >= 1 → "한 발 더!"` / `shotsRemaining == 0 → "오늘은 여기까지"`를 명시하지만, 실제 코드는 `remaining > 0 ? $"{remaining}발 더!" : "타앗!"`를 생성한다("1발 더!" 등 N을 그대로 노출, `==0` 분기는 "타앗!"). 또한 `DemoShotsRemaining`이 `const int = 1`로 고정돼 있어(`:35`) `==0` 분기(현재 "타앗!")는 지금 코드로는 절대 도달할 수 없는 죽은 코드다. 31-1 원칙(같은 함수·같은 스냅샷) 자체는 위반하지 않지만, 실제 전투 로직이 아직 없는 데모 표기라는 점을 감안해도 표와 다른 문구가 "완료"로 기록된 채 남아있으면 다음 라운드에서 혼동 소지가 있다. Phase 3에서 실제 전투 로직으로 교체할 때 문구도 표와 맞추길 권고.
2. **`impulseMagnitude` 계산식 중복(DRY 위반)** — `Core/StickmanAgent.cs:72`와 `Core/RagdollLimbImpactRelay.cs:39`가 `collision.relativeVelocity.magnitude * _body.mass`를 동일하게 각자 구현하고 있다. `ReportExternalImpact()` 자체는 단일 진입점이 맞지만, 그 인자를 만드는 공식은 두 곳에 중복돼 있어 나중에 공식이 바뀌면(예: 질량 가중치 조정) 한쪽만 고치는 실수 위험이 있다. 정적 헬퍼(`static float ComputeImpulseMagnitude(Collision2D)`) 하나로 합칠 것을 권고.
3. **`RagdollRig` 파츠 0개 상태에서 즉시 Getup 자동 전이** — 위 중점 점검 3 참고. NRE는 없으나, 실제 프리팹 배선 이후에도 어떤 이유로(파괴/비활성화 등) 파츠 배열이 실질적으로 비게 되면 같은 동작(물리 반응 없이 곧장 Getup)이 조용히 재발할 수 있다. 지금 당장 고칠 것은 아니지만, `GetMaxSpeed()`가 0을 반환하는 경우 최소한 `Debug.LogWarning` 한 줄 정도를 고려할 만하다.
4. **`ParkourClimbState.Enter()`가 `WalkState.Tick()`이 이미 계산한 `GroundInfo`/벽 판정을 재사용하지 않고 다시 계산** — `ParkourClimbState.cs:54-55`가 `SenseGround()`+`TryFindClimbableWall()`을 다시 호출한다. 같은 프레임 내 결정적 함수라 결과가 항상 일치함을 확인했으므로 버그는 아니지만(중점 점검 4 참고), 약간의 중복 계산이며 두 호출부가 향후 갈라질(예: 한쪽만 수정) 여지가 있다. `WalkState`가 판정 결과(방향/핸들/상단Y)를 그대로 넘겨줄 방법을 고려할 만하다.
5. **Getup(#3, `reimpactCount`) 대사 파이프라인 미구현** — `GetupState.cs:40`의 TODO 주석대로 구현되지 않았다. `Tasklist.md`에 이미 근거(여러 RAGDOLL↔GETUP 사이클에 걸친 카운터 추적 필요, 추가 비용 대비 실익 낮음 판단)와 함께 정직하게 기록되어 있어 버그로 잡지는 않지만, UX_FLOW.md 31-2 표를 처음 보는 독자는 5개 예시 중 Getup만 빠진 이유를 표만 봐서는 알 수 없다 — 표 근처에 "Getup은 Phase 2에서 의도적으로 보류(Tasklist.md 참고)"라는 각주를 남겨두면 향후 혼동을 줄일 수 있다.

---

## 가설 (원인 불명, 실측 필요 — `Tasklist.md` 과학적 토론 로그에 동시 기록)

- **[Debugger, 2026-08-27] 가설 H7**: `RagdollRig.TickGetup()`의 비례 제어(게인 `getupMotorGain=6`(도/초 per 도 오차), `maxMotorTorque=50`)가 실제 캐릭터 프리팹의 관절 관성/댐핑 값과 결합했을 때 감쇠 없이 목표 각도 주변에서 오버슈트-진동(bang-bang에 가까운 떨림)을 일으킬 가능성이 있다 — 순수 P 제어이고 D(미분) 항이 전혀 없어, 관절 자체의 물리적 댐핑(HingeJoint2D의 기본 저항)에만 안정성을 의존하는 구조이기 때문이다. 실제 프리팹/조인트 배치가 Phase 2 범위 밖이라 정적 검토로는 확답 불가.
  - **검증 방법**: 실제 캐릭터 프리팹(몸통+머리+양팔+양다리, HingeJoint2D 배선)이 만들어진 뒤, RAGDOLL→GETUP 전이를 반복 트리거해 `_getupProgress`가 1에 도달하기까지 각 관절의 `jointAngle`이 목표(0도) 주변에서 진동 없이 단조 수렴하는지, 아니면 오버슈트 후 여러 번 왕복하는지 Play 모드에서 관찰/로그로 확인.
  - **결과/결론**: (실제 프리팹 배선 이후 라운드에서 채움)

---

## 결론

**BUG-P2-M1(Major) 수정 전까지 Phase 3 착수 보류 — Coder로 반려 필요.** 이 항목을 제외한 나머지(토큰화/단일 진입점/재인터럽트 리셋/파쿠르 좌표계·재확인·배회AI 경합/축 분리 결정/Ragdoll·ParkourClimb 대사 매핑)는 전부 검증 통과했다. BUG-P2-M1의 수정안 1번(Tick()마다 `linearVelocity.y` 재확정)은 파일 하나, 두 줄 내외 규모로 예상되어 반려 사이클이 길어지지 않을 것으로 판단한다. Minor 5건은 급하지 않으므로 Coder 재량으로 다음 라운드 이후 처리해도 무방하다.

수정 완료 후 재검토 시 확인할 것: (1) BUG-P2-M1 수정이 실제로 등반 직후 튐을 없애는지(Play 모드 또는 최소한 코드 레벨로 `linearVelocity.y`가 매 Tick 0으로 유지되는지 재확인), (2) 클린 재빌드 기준선(에러 0/경고 0) 유지 여부.

---

## 핫픽스 재확인 (Debugger, 2026-08-27)

대상: 커밋 `7d209bd`("Phase 2 핫픽스: ParkourClimb 착지 튐 버그 수정")—변경 범위는 `Assets/_Project/Scripts/States/ParkourClimbState.cs` 1개 파일, `Tick()`에 10줄 추가뿐임을 `git show --stat`으로 확인.

1. **수정 제안 1번과 정확히 일치**: `ParkourClimbState.cs:103-105`(`pos.y` Lerp 대입) 바로 다음, `:113-115`에 `Vector2 v = _blackboard.Body.linearVelocity; v.y = 0f; _blackboard.Body.linearVelocity = v;`가 추가됨 — 위치를 갱신할 때마다 매 프레임 속도도 재확정하는, 제안한 "가장 간단한" 방식 그대로. `Enter()`의 1회성 초기화(`:61-64`)를 대체한 것이 아니라 `Tick()` 전체로 확장한 것이라 기존 패턴(`SnapToGround`)과 일관됨.
2. **부작용 없음**: 추가된 3줄은 `v.y`만 읽고 쓰며 `v.x`는 전혀 건드리지 않는다(대입도, 참조도 없음). `Tick()`의 다른 곳에서도 `linearVelocity.x`를 별도로 설정하는 코드가 없으므로, `Enter()`에서 0으로 고정된 x축 값은 그대로 유지된다 — 등반 중 x축 이동/그 외 물리 반응에 영향 없음.
3. **컴파일 기준선 유지**: 배치모드 재실행 — `error CS` 0건, `warning CS` 0건, `Batchmode quit successfully`/`Exiting batchmode successfully now` 정상 종료 확인(로그상 `0 items updated`로 Library 캐시 재사용이었으나, 이 커밋 상태의 클린 재빌드는 커밋 메시지 및 이전 라운드에서 이미 별도로 확인된 "에러 0/경고 0"과 일치하는 결과라 상충하는 증거 없음).

**Phase 2 최종 승인 — Phase 3(전투/커서상호작용) 착수 가능**
