# StickMate — Phase 1 버그 리포트 (2차, Debugger, Teammate2)
> 작성: Debugger · 작성일: 2026-08-27 · 대상: Coder Phase 1 산출물(`Core/StickmanAgent.cs`, `States/StickmanBlackboard.cs`, `States/GroundSensor.cs`, `Platform/FootholdPoller.cs`, `Platform/ScreenCoordinateConverter.cs`, `Platform/ICursorPositionService.cs`, `States/IdleState.cs`/`WalkState.cs`/`JumpState.cs`/`FallState.cs`, `States/StickmanStateMachine.cs`(BUG-M1/M2 수정분), `Platform/Windows/Win32WindowService.cs`(BUG-B1 가드))
> 환경: 실제 Unity 6 LTS(6000.0.82f1) 프로젝트로 전환 완료, 배치 모드 임포트 결과 에러 0건/경고 2건(RagdollState/GetupState 미사용 필드, Phase2 스텁이라 정상) 확인됨. 이번 리뷰는 "컴파일 되는가"가 아니라 "논리적으로 옳은가"에 집중. Windows 실기기/실빌드 환경은 여전히 없어(Darwin 개발 환경) 가설 H1~H6은 전부 실측 미완료 상태로 남는다.

## 결론 요약

**Coder로 반려 필요.**

Blocker 2건(신규), Major 6건(신규), Minor 4건(신규) 발견. 이번 라운드는 코드 품질 자체는 Phase 0 대비 확연히 개선됐다(BUG-B1/M1/M2/M3/M5 전부 성실히 반영됨, 좌표 변환·폴링 규율이 실제로 지켜짐). 다만 리더가 지목한 6개 중점 항목을 파고들다가 **Phase 1의 핵심 약속("아무것도 안 해도 재미있는 자율 배회 캐릭터")을 구조적으로 깨뜨리는 새로운 Blocker 2건**을 발견했다 — 둘 다 "돌아갈 것 같다"는 인상과 달리 실사용/실배포 경로에서 사실상 100% 재현되는 문제다.

**권고 수정 순서**
1. **BUG-P1-B2** (이동 트리거가 키보드 입력뿐 — 진짜 오버레이 완성 시 캐릭터 영구 정지가 구조적으로 확정됨) — Phase 2 착수 전 Architect/UX 결정 필요, 가장 근본적.
2. **BUG-P1-B1** (발판 0개일 때 낙하 안전망 부재 — 캐릭터가 화면 밖으로 무한 낙하) — UX_FLOW.md의 "빈 상태" 하드 요구사항 정면 위반, 재현이 매우 쉬움.
3. **BUG-P1-M1** (`Camera.main` 1회성 캐싱, 재검증 없음) — BUG-P1-B1과 같은 증상(무한 낙하)의 또 다른 경로, 같은 안전망으로 함께 해결 권고.
4. **BUG-P1-M3** (`CreateOverlayWindow()` 반환값 무시 — BUG-B1/가설 H4 진단 사각지대) — 값싼 수정.
5. **BUG-P1-M2** (`StickmanStateMachine` 생성자의 즉시 `ChangeState` 타이밍 — Coder가 스스로 지목한 이슈에 대한 구체적 Phase 2 수정안) — Phase 2 착수 전 선반영 권고.
6. **BUG-P1-M5** (Idle/Walk의 Jump 전이가 실제 접지 여부를 확인하지 않음) — 값싼 로직 수정.
7. **BUG-P1-M4** (`FootholdPoller.CachedFootholds`가 캐스팅으로 변형 가능, Phase0 Minor m2 재발) — `.AsReadOnly()` 한 줄.
8. **BUG-P1-M6** (`Suspend()/Resume()`가 단일 `Rigidbody2D`만 가정) — Phase 2 Active Ragdoll 착수 시 필히 포함.

Minor 4건은 즉시 반려 사유는 아니나 해당 작업 재착수 시 함께 처리 권고.

---

## 이번 라운드 중점 점검 항목 결론 (리더 지시 1~6 대응 요약)

| # | 점검 항목 | 결론 |
|---|---|---|
| 1 | `Awake()`에서 `StickmanStateMachine` 생성자가 즉시 `Enter()`를 호출할 때 `blackboard.Machine`이 null | **직접 확인 완료 — Coder 주장대로 현재는 안전.** `IdleState`/`WalkState`/`JumpState`/`FallState`의 `Enter()` 4개 전부 `_blackboard.Machine`을 참조하지 않음(코드 재확인함). 다만 이 안전성은 "초기 상태가 Idle이고 IdleState.Enter가 우연히 Machine을 안 쓴다"는 두 가지 우연이 겹친 결과일 뿐 구조적 보증이 아니다. 근본 수정안은 BUG-P1-M2 참고. |
| 2 | BUG-B1 가드(`NotSupportedException`)의 완전성 | **가드 자체는 완전 — 우회 경로 없음.** 프로젝트 전체에서 `SetClickThrough`/`SetAlwaysOnTop`를 호출하는 곳은 `StickmanAgent.Start()` 단 한 곳뿐이고(grep 확인), 이를 감싸는 `catch`도 프로젝트에 그 한 곳 하나뿐이라 예외를 삼키는 상위 catch가 없다. 단, 가드가 다루지 못하는 **사각지대**(핸들이 애초에 `IntPtr.Zero`인 경우 조용히 no-op)가 있음 — BUG-P1-M3. |
| 3 | GroundSensor/Blackboard 접지 판정: 좌표계 혼용, 채터링, `CheckScreenBoundsOrFall`↔`GroundedTick` 경합 | **좌표계**: `ScreenCoordinateConverter`만 일관되게 경유함 확인(문제없음). **채터링**: `fallGraceDuration` 히스테리시스가 Idle/Walk↔Fall 경계에서 양방향(이탈 유예/착지 확정)으로 적용되어 방어됨(문제없음). **경합**: 없음 — `Tick()`마다 `CheckScreenBoundsOrFall`을 먼저 호출하고 `true`면 즉시 `return`하므로 같은 프레임에 `GroundedTick`이 실행될 일 자체가 없음(배타적 순서, 경합 아님). 다만 발판 0개 시 두 함수 모두 무력화되어 캐릭터가 무한 낙하하는 더 근본적인 문제를 발견함 — BUG-P1-B1. 그 외 Jump 전이가 접지를 확인 안 함(BUG-P1-M5), 겹친 발판의 z-order 변화 시 미세 Y스냅 흔들림 가능성(Minor m3)도 발견. |
| 4 | FootholdPoller 폴링 계약/캐시 변형 가능성 | **폴링 주기 계약은 실제로 지켜짐**(`Tick()`의 누적 타이머가 `footholdPollInterval`을 강제, 모든 State는 `IPlatformWindowService`를 직접 호출하지 않고 이 폴러만 거침 — grep으로 재확인). **다만 캐시가 스냅샷이 아님** — Phase 0 Minor m2에서 지적한 패턴이 새 `FootholdPoller.CachedFootholds`에도 그대로 재발(BUG-P1-M4). |
| 5 | StickmanAgent 초기화/생명주기 — `Camera.main` 캐싱, `Input.*` vs 커서 폴링 | **`Camera.main`**: `Awake()`에서 1회만 캐싱, 재검증/재획득 로직 전무 — 씬에 `MainCamera` 태그가 없거나 나중에 파괴되면 접지 판정이 영구 무력화됨(BUG-P1-M1). **`Input.GetAxisRaw`/`GetButtonDown` vs `ICursorPositionService`**: 완전히 별개 시스템 확인(전자는 Unity 레거시 Input Manager 키보드 폴링, 후자는 Win32 `GetCursorPos` 기반 전역 커서 좌표 — 서로 참조하지 않음, 설계 의도대로 독립). **단, 이 구분과 별개로 `Input.GetAxisRaw` 자체가 심각한 아키텍처 문제다** — BUG-P1-B2 참고. Coder가 검토 요청한 "커서 좌표 조회를 `IPlatformWindowService` 확장 대신 신규 `ICursorPositionService`로 분리"한 설계 판단은 **승인** — mobile에 억지로 no-op을 구현하게 만들지 않고 `as` 캐스팅으로 지원 여부를 판정하는 방식이 더 안전하다. |
| 6 | Suspend/Resume의 다중 Rigidbody 확장성 | **단일 `Rigidbody2D` 가정 — Phase 2 다중 파츠 Active Ragdoll 확장을 막는 구조적 문제 확인.** `SetRenderersEnabled`는 이미 `GetComponentsInChildren<Renderer>(true)`로 전체 순회하는데, 물리 쪽(`Suspend()/Resume()`)만 단일 `_body.simulated`를 토글하는 비대칭 구조 — BUG-P1-M6. |

---

## Blocker

### BUG-P1-B1 — 발판이 0개가 되는 순간 캐릭터가 화면 밖으로 무한 낙하 (UX_FLOW "빈 상태" 요구사항 위반)
- **파일:라인**: `Assets/_Project/Scripts/States/StickmanBlackboard.cs:53-69`(`GroundedTick`), `Assets/_Project/Scripts/States/GroundSensor.cs:49-54`, `Assets/_Project/Scripts/Platform/Windows/Win32WindowService.cs:118-135`(`OnEnumWindow`/`EnumerateFootholds`), `Assets/_Project/Scripts/Core/StickmanAgent.cs:169-183`(`CreatePlatformService`)
- **재현 시나리오**:
  1. `GroundSensor.Sense()`는 `footholds.Count == 0`이면 `new GroundInfo(false, footWorldPos.y, false, ...)`를 반환한다 — `HasAnyFoothold=false`, `Grounded=false`.
  2. `CheckScreenBoundsOrFall()`은 `!info.HasAnyFoothold`면 즉시 `false`를 반환한다(화면경계 판정 자체를 포기) — 즉 발판이 0개면 이 함수는 "안전망"으로 전혀 작동하지 않는다.
  3. 그런데 `GroundedTick()`은 `info.Grounded==false`를 그대로 받아 `_groundLossTimer`를 계속 누적하다가 `fallGraceDuration`(기본 0.1초)을 넘기면 `Machine.ChangeState(StickmanStateId.Fall)`을 강제 발동한다.
  4. `FallState.Tick()`에 진입해도 여전히 발판이 0개이므로 `info.Grounded`는 계속 `false` — `_landingConfirmTimer`가 매 프레임 0으로 리셋되고 착지 조건이 영원히 충족되지 않는다.
  5. `Rigidbody2D`는 `gravityScale`에 따라 계속 아래로 가속하고, 화면 경계(좌우) 밖으로 나가지 않는 한(수직 낙하이므로 좌우 경계는 넘지 않을 수 있음) `CheckScreenBoundsOrFall`도 계속 no-op이다 — **캐릭터가 화면 아래로 영원히 사라진다.**
  6. **이 시나리오가 실제로 발생하는 조건이 매우 흔하다**: `Win32WindowService.OnEnumWindow`는 `IsWindowVisible(hWnd)`와 `GetWindowTextLength(hWnd) != 0` 두 조건을 모두 요구한다. Windows 작업표시줄(`Shell_TrayWnd`)조차 보통 창 제목이 없어 이 필터에 걸려 제외된다(가설 H5 참고, 실측 필요하나 Win32 API 특성상 개연성이 매우 높음). 즉 유저가 모든 앱 창을 최소화하거나 깨끗한 바탕화면 상태에서 잠깐이라도 있으면, "제목 있는 가시 창"이 0개가 되어 `EnumerateFootholds()`가 빈 리스트를 반환할 수 있다.
  7. `NullPlatformWindowService`는 생성자에서 "화면 하단 더미 발판 1개"를 항상 반환하도록 만들어져 있어(에디터에서는 이 버그가 재현되지 않는다) — 이 안전망이 **에디터/미지원 플랫폼 전용으로만 존재하고, 실제 배포 대상인 Windows(`Win32WindowService`)에는 이식되어 있지 않다.**
- **근본 원인**: `docs/UX_FLOW.md` 1-A절("빈 상태(제한 모드): 열린 창이 하나도 없거나 권한 미승인 → 화면 하단만 발판으로 사용하는 `NullPlatformWindowService` 폴백")과 6-1절("절대 완전 정지 프레임으로 보이지 않을 것")은 **"창이 0개일 때 Null 서비스 수준의 폴백 발판으로 런타임에 전환"**을 요구하는데, 현재 `StickmanAgent.CreatePlatformService()`는 컴파일 타임 플랫폼 분기(`#if UNITY_STANDALONE_WIN`)로 서비스를 **한 번만 고정 선택**할 뿐, 런타임에 "발판 0개"를 감지해 Null 수준 폴백으로 전환하는 로직이 전혀 없다.
- **판단**: Blocker로 격상하는 이유 — (a) 재현 조건이 희귀한 엣지 케이스가 아니라 일상적 데스크톱 사용 패턴(창 최소화, 빈 바탕화면)이고, (b) 결과가 "캐릭터가 화면에서 영구히 사라짐"이라는, UX_FLOW.md가 명시적으로 "절대 금지"라고 못박은 바로 그 실패 모드이며, (c) Phase 1 Tasklist의 "중력/발판 인식"·"화면 경계 이탈 → 낙하" 두 항목이 이미 "완료"로 표기돼 있어 이 공백이 다음 Phase로 그대로 새어나갈 위험이 크다.
- **수정 제안**: (a) `Win32WindowService.EnumerateFootholds()`가 빈 리스트를 반환하면 호출부(`FootholdPoller` 또는 `StickmanBlackboard`)가 화면 하단 가로 전체 폭의 합성 발판 1개를 주입하는 최소 안전망을 추가한다(디자인상 `NullPlatformWindowService`의 더미 발판과 동일한 개념을 데스크톱 실구현에도 공용 유틸로 이식). (b) 또는 `IPlatformWindowService`에 "이 구현체가 하나도 못 찾았을 때 위임할 폴백 서비스"를 명시적으로 합성하는 데코레이터 패턴(`FallbackPlatformWindowService`)을 두어 `EnumerateFootholds()`가 0개를 반환하면 내부적으로 `NullPlatformWindowService`의 더미 발판으로 대체 반환하게 한다. (b)안이 "제한 모드로 조용히 폴백"이라는 UX 6-1절/2절의 표현과 더 정확히 일치한다.

### BUG-P1-B2 — 캐릭터 이동의 유일한 트리거가 키보드 입력이며, 실제 오버레이가 완성되면 구조적으로 영구 정지함 (P0 "아무것도 안 해도 재미있음" 원칙 정면 위반)
- **파일:라인**: `Assets/_Project/Scripts/Core/StickmanAgent.cs:124-125`, `Assets/_Project/Scripts/States/IdleState.cs:42-52`, `Assets/_Project/Scripts/States/WalkState.cs:36-48`
- **재현 시나리오/논증**:
  1. `StickmanAgent.Update()`는 `_blackboard.MoveInputX = Input.GetAxisRaw("Horizontal"); _blackboard.JumpPressed = Input.GetButtonDown("Jump");`로 **매 프레임 딱 한 번, Unity 레거시 Input Manager(키보드/게임패드)를 직접 폴링**한다. `IdleState`/`WalkState`의 Idle↔Walk↔Jump 전이는 전부 이 두 값에만 의존한다 — 그 외에 캐릭터가 스스로 걷거나 방향을 바꾸게 만드는 자율 로직(랜덤 배회, AI 타이머 등)이 프로젝트 어디에도 존재하지 않는다(전체 `States/`, `Core/` grep 결과 `Random`/`Wander`/`AI` 계열 코드 전무).
  2. `docs/UX_FLOW.md` 2절("코어 루프 UX — '지켜보기'가 기본 액션")은 "유저가 할 수 있는 액션: 기본: 없음(관찰). 클릭은 하위 앱으로 관통."이라 명시하고, 8절은 "P0 — 아무것도 안 하기(관찰): 설치 후 온보딩만 넘기면 그 뒤로는 클릭 한 번 없이도 앱이 재미있게 돌아가야 한다. 이게 실패하면 다른 모든 게 무의미."라고 이 앱의 최우선 성공 기준으로 못박는다. `docs/ARCHITECTURE.md` Phase 1 범위 정의에도 "키보드 입력"은 전혀 등장하지 않는다.
  3. 즉 현재 구현은 **유저가 키보드 포커스를 이 앱 창에 두고 실제로 화살표/스페이스를 누르지 않는 한 캐릭터가 Idle에서 단 한 번도 벗어나지 않는다** — "아무것도 안 해도 재미있는 자율 배회 데스크톱 펫"이라는 이 프로젝트의 존재 이유 자체가 Phase 1 구현에서 충족되지 않는다.
  4. **더 심각한 지점**: BUG-B1(Phase 0)이 해결되어 진짜 분리 오버레이가 구현되면, 그 오버레이 창은 설계상 `WS_EX_NOACTIVATE`를 갖게 된다(`Win32WindowService.cs:80-84`의 주석, BUG-B1(c) 대응으로 이미 `SetClickThrough`에 반영됨). `WS_EX_NOACTIVATE` 창은 유저가 클릭해도 OS 포그라운드/키보드 포커스를 가져갈 수 없다 — 즉 **그 시점부터는 유저가 의도적으로 시도해도 이 창에 키보드 입력을 줄 방법 자체가 OS 레벨에서 사라진다.** 그러면 `Input.GetAxisRaw("Horizontal")`은 영원히 0을, `Input.GetButtonDown("Jump")`는 영원히 false를 반환하게 되어(가설 H6, 실측 필요) **캐릭터가 Idle에서 100% 영구 고착된다.** 지금은 "게임 창 자체가 아직 클릭관통되지 않은 안전 실패 상태"라서 우연히 키보드가 먹히고 있을 뿐이며, 이는 BUG-B1이 "의도한 대로" 해결될수록 오히려 이 문제가 드러나는 역설적 구조다.
- **근본 원인**: Phase 1 "IDLE/WALK/JUMP/FALL 상태 구현" 작업이 상태 전이 로직 자체(전이 조건/타이머/좌표 판정)에는 충실했지만, 그 전이를 발동시키는 "누가 MoveInputX/JumpPressed를 채우는가"라는 상위 계층(자율 행동 결정 로직)이 통째로 빠진 채 임시로 키보드 입력을 대신 꽂아넣은 것으로 보인다. Tasklist.md Phase 1 표에도, ARCHITECTURE.md Phase 정의에도 "자율 배회 AI/행동 결정 로직"이라는 태스크 자체가 명시적으로 존재하지 않아 이 공백이 아무도 지목하지 않은 채 넘어갈 뻔했다.
- **판단**: Blocker인 이유 — 논리적 필연(향후 오버레이 완성 시 100% 재현 보장)이며, 수정 규모가 작지 않고(자율 행동 결정 시스템 신설 필요) Phase 2/3(Ragdoll, 파쿠르, 전투 AI)가 전부 "캐릭터가 알아서 움직이고 있는 상태"를 전제로 설계돼 있어 이 공백 위에 다음 Phase를 쌓으면 나중에 훨씬 비싸게 재작업해야 한다.
- **수정 제안**: (a) 최소 스코프: `StickmanAgent` 또는 신규 `AutoWanderController`가 매 프레임 `Input.*`를 읽는 대신, 간단한 타이머 기반 랜덤 방향 전환 + 발판 경계 도달 시 정지/점프 판단 로직으로 `_blackboard.MoveInputX`/`JumpPressed`를 채우도록 교체한다(디자인 세부는 UX Designer와 협의 필요 — "느낌 있는 배회" 패턴). (b) `Input.GetAxisRaw`/`GetButtonDown`는 완전히 제거하거나, 최소한 "대결모드"(UX_FLOW 7절, Phase 3 스코프) 진입 시에만 조건부로 활성화하는 방식으로 용도를 명확히 분리한다. (c) Architect 확인 필요 사항: 이 자율 행동 로직을 Phase 1 범위에 지금 추가할지, 혹은 Phase 1을 "물리/전이 골격만 검증하는 내부 QA 빌드"로 명시적으로 재정의하고 자율 AI를 Phase 1.5/2 초반 필수 선행 작업으로 Tasklist에 새로 등재할지 결정이 필요하다 — 어느 쪽이든 **현재 상태로 "IDLE/WALK/JUMP/FALL 상태 구현" 행을 "완료"로 남겨두면 안 된다.**

---

## Major

### BUG-P1-M1 — `Camera.main`을 `Awake()`에서 1회만 캐싱, 재검증/재획득 로직 없음
- **파일:라인**: `Assets/_Project/Scripts/Core/StickmanAgent.cs:50`, `States/StickmanBlackboard.cs:21,44`
- **재현 시나리오**: `_mainCamera = Camera.main;`은 씬에 `MainCamera` 태그가 붙은 카메라가 하나도 없으면 `null`을 반환한다(재시도 없음). `_blackboard.MainCamera`도 이후 절대 갱신되지 않는다(grep 확인 — 프로젝트 전체에서 `MainCamera` 대입은 이 한 줄뿐). `GroundSensor.Sense(cam, ...)`는 `cam == null`이면 `HasAnyFoothold=false`를 반환하도록 안전 가드가 있지만, 그 결과 BUG-P1-B1과 **완전히 동일한 무한 낙하 실패 모드**로 이어진다. 씬/프리팹 배선이 아직 이뤄지지 않은 시점(Tasklist 확인: `Assets/_Project`에 `.unity`/`.prefab`/`.asset` 파일이 하나도 없음)이라 이 리스크는 아직 잠재 상태지만, Phase 2에서 실제 씬을 구성하는 담당자가 카메라에 `MainCamera` 태그 붙이는 것을 잊는 흔한 Unity 실수 하나로 즉시 발현된다. 또한 카메라가 런타임에 파괴/교체되는 경우(Unity의 `==` 오버로드로 "가짜 null" 감지는 되지만) 이후 영구히 접지 불능 상태가 된다.
- **근본 원인**: 카메라 참조를 "존재가 보장된 의존성"으로 가정하고 1회성으로만 캐싱, 실패 시 로그/경고조차 없음.
- **수정 제안**: (a) `Awake()`에서 `_mainCamera == null`이면 `Debug.LogError`로 즉시 경고. (b) `[SerializeField] private Camera _cameraOverride;`를 추가해 인스펙터에서 명시적으로 카메라를 지정할 수 있게 하고 `Camera.main` 탐색을 폴백으로만 사용. (c) BUG-P1-B1의 폴백 안전망(화면 하단 발판)과 별개로, 카메라 자체가 없을 때도 "관찰 가능한 상태"를 유지할 최소 방어(예: 카메라 없으면 물리 시뮬레이션은 계속하되 Fall 전이 자체를 보류)를 검토.

### BUG-P1-M2 — `StickmanStateMachine` 생성자의 즉시 `ChangeState` 호출 — 근본 수정안 제시
- **파일:라인**: `Assets/_Project/Scripts/States/StickmanStateMachine.cs:50-54`, `Assets/_Project/Scripts/Core/StickmanAgent.cs:81-88`
- **현재 상태(재확인 결과)**: Coder가 Tasklist에 남긴 우려 그대로 — `new StickmanStateMachine(states, StickmanStateId.Idle)`는 생성자 내부에서 즉시 `ChangeState(StickmanStateId.Idle)`을 호출하고, 이는 `IdleState.Enter(context)`를 그 자리에서 실행한다. 이 시점은 `StickmanAgent.Awake()`의 `_blackboard.Machine = _machine;` 대입 **이전**이므로 `IdleState.Enter()` 안에서 `_blackboard.Machine`을 참조하면 `NullReferenceException`이다. 직접 코드를 읽어 확인한 결과 `IdleState`/`WalkState`/`JumpState`/`FallState`의 `Enter()` 4개 전부 `_blackboard.Machine`을 참조하지 않아 **현재는 안전**하다(Coder 진단이 정확함).
- **왜 이것이 여전히 Major인가**: 이 안전성은 우연의 산물이다 — (1) 초기 상태가 하필 `Idle`이고 (2) `IdleState.Enter()`가 하필 `Machine`을 안 쓴다는 두 조건이 겹쳐야만 성립한다. `IdleState.Enter()`에 이미 `// TODO(Phase 2): 필요 시 new DialogueIntent(context, id => "...")`라는 주석이 있는데, `DialogueIntent`는 `context`(즉 `StateTransitionContext.OriginMachine`, 이건 `blackboard.Machine`이 아니라 `ChangeState` 내부의 `this` 참조라서 이 경로는 실제로 안전함)만 필요하므로 이 특정 TODO는 위험하지 않다. 그러나 Phase 2에서 어떤 상태(예: 파쿠르 초기 판정, 공격 콤보 상태 등)의 `Enter()`가 "현재 다른 상태로 즉시 재전이해야 하는지"를 판단하려고 `blackboard.Machine.ChangeState(...)`나 `blackboard.Machine.CurrentStateId`를 참조하는 순간, 혹은 QA/디버그 목적으로 초기 상태를 `Idle`이 아닌 다른 값으로 바꿔 테스트하는 순간, 이 방어는 아무 경고 없이 깨진다.
- **근본 수정안(Phase 2 착수 시 반드시 포함 — "나중에 고치자"가 아니라 구체적 구조 변경)**: `StickmanStateMachine`의 생성 시점과 "최초 상태 활성화" 시점을 분리한다.
  ```csharp
  // StickmanStateMachine.cs
  public StickmanStateMachine(Dictionary<StickmanStateId, IStickmanState> states)
  {
      _states = states;
      _current = null; // 아직 어떤 상태도 활성화되지 않음
  }

  /// <summary>블랙보드 등 모든 의존성이 완전히 배선된 뒤 1회 호출. 이 호출 시점부터 Enter()가 실행된다.</summary>
  public void Start(StickmanStateId initialState)
  {
      if (_current != null) { Debug.LogError("StickmanStateMachine.Start()가 이미 호출됨"); return; }
      ChangeState(initialState);
  }
  ```
  ```csharp
  // StickmanAgent.Awake()
  _machine = new StickmanStateMachine(states);   // Enter() 아직 호출 안 됨
  _blackboard.Machine = _machine;                // Machine을 먼저 완전히 배선
  _machine.Start(StickmanStateId.Idle);          // 이제서야 Enter() 호출 — Machine은 항상 non-null 보장
  ```
  이렇게 하면 "초기 상태가 무엇이든, 그 상태의 Enter()가 무엇을 참조하든" `blackboard.Machine`이 null일 수 있는 경우의 수 자체가 코드 구조상 사라진다(우연이 아니라 보증이 됨). 부작용: `CurrentStateId`가 `Start()` 호출 전에는 `_current == null`이라 여전히 `default`(Idle)를 반환하는 기존 Phase 0 Minor m1의 모호성은 그대로 남으므로, 이 리팩터링을 하면서 `StickmanStateId`에 `None`/`Uninitialized` 센티널을 함께 추가하는 것을 권장(비용이 거의 같은 시점이라 묶어서 처리 권고).

### BUG-P1-M3 — `Win32WindowService.CreateOverlayWindow()`의 반환값이 완전히 무시됨 (BUG-B1/가설 H4 진단 사각지대)
- **파일:라인**: `Assets/_Project/Scripts/Core/StickmanAgent.cs:93`
- **재현 시나리오**: `_platformService.CreateOverlayWindow();`는 `bool`을 반환하지만(`_overlayHwnd != IntPtr.Zero`) 호출부는 이 값을 완전히 버린다. `Win32WindowService.SetClickThrough`/`SetAlwaysOnTop`의 가드 로직을 보면: `if (_overlayHwnd == IntPtr.Zero) return;`이 **`_usingUnsafeSelfWindowFallback` 체크보다 먼저** 실행된다. 즉 가설 H4(`MainWindowHandle`이 부트스트랩 타이밍에 `IntPtr.Zero`를 반환)가 실제로 발생하면, `SetClickThrough`/`SetAlwaysOnTop`는 `NotSupportedException`을 던지지 않고 **그냥 조용히 아무것도 하지 않고 반환**한다 — `StickmanAgent.Start()`의 `catch (NotSupportedException)`도 발동하지 않으므로 로그에 아무 흔적도 남지 않는다. BUG-B1 가드가 막으려던 것("위험한 부작용 대신 알아챌 수 있게 실패")이 정확히 이 경로에서는 지켜지지 않는다 — "알아채지도 못하고 조용히 실패"한다.
- **근본 원인**: `CreateOverlayWindow()`의 실패(핸들 미획득)와 "안전 가드에 의한 의도된 차단"이 호출부에서 구분되지 않음.
- **수정 제안**: `StickmanAgent.Start()`에서 `bool overlayReady = _platformService.CreateOverlayWindow(); if (!overlayReady) Debug.LogWarning(...)`로 반환값을 반드시 확인해 로그를 남긴다. 가능하면 `Win32WindowService`도 `_overlayHwnd == IntPtr.Zero`인 경우를 `_usingUnsafeSelfWindowFallback`과 구분되는 별도 예외/로그로 승격시켜, 두 실패 모드(가드에 의한 의도된 차단 vs 핸들 자체를 못 얻음)를 호출부가 구분할 수 있게 한다. 이 수정은 가설 H4의 실측(아래 참고)에도 필수 전제조건이다.

### BUG-P1-M4 — `FootholdPoller.CachedFootholds`가 캐스팅으로 외부 변형 가능 (Phase 0 Minor m2 패턴 재발)
- **파일:라인**: `Assets/_Project/Scripts/Platform/FootholdPoller.cs:42-46`
- **재현 시나리오**: `private readonly List<PlatformFoothold> _cache = ...; public IReadOnlyList<PlatformFoothold> CachedFootholds => _cache;` — `List<T>`가 `IReadOnlyList<T>`를 구현하므로 인터페이스 타입으로 노출해도 호출부가 `(List<PlatformFoothold>)blackboard.FootholdPoller.CachedFootholds`로 캐스팅해 `.Add()`/`.Clear()`/`.Sort()` 등을 직접 호출할 수 있다. `FootholdPoller` 자신은 이제 Win32/모바일 구현체의 내부 버퍼를 그대로 들고 있지 않고 별도로 복사(Phase 0 BUG-M3 수정 시 이미 반영됨)하므로 **외부 서비스 버퍼 오염 위험은 해소됐지만, 이 풀러 자신의 캐시가 여전히 무방비**다. Idle/Walk/Jump/Fall 4개 State가 전부 같은 프레임에 `SenseGround()`를 호출해 같은 `_cache` 참조를 읽는데, 그 사이 어떤 코드(예: Phase 2/3에서 실수로 추가된 디버그 오버레이 코드)가 캐스팅해 리스트를 변형하면 그 프레임 내에서도 상태별로 다른 결과를 보게 될 위험이 있다.
- **근본 원인**: `IReadOnlyList<T>`는 컴파일러가 "이 참조로는 수정 못 함"을 보장할 뿐, 런타임에 다른 참조로 캐스팅해 수정하는 것까지는 막지 못한다(C#의 잘 알려진 함정).
- **수정 제안**: `_cache.AsReadOnly()`로 감싼 `ReadOnlyCollection<PlatformFoothold>`을 반환하도록 `CachedFootholds` 프로퍼티를 1회 생성 후 캐싱(매 프레임 새로 감싸면 할당이 생기므로 `Poll()`이 실제로 캐시를 갱신할 때만 래퍼도 갱신). Phase 0 리포트의 권고안(m2)을 그대로 적용하면 된다 — 비용은 한 줄, 왜 아직 반영 안 됐는지는 불명확하나 지금 처리하는 게 저렴하다.

### BUG-P1-M5 — Idle/Walk의 Jump 전이가 실제 접지 상태를 확인하지 않음 (문서화된 전이 규칙과 불일치)
- **파일:라인**: `Assets/_Project/Scripts/States/IdleState.cs:38-46`, `Assets/_Project/Scripts/States/WalkState.cs:32-40`, `Assets/_Project/Scripts/States/StickmanStateMachine.cs:12`(전이 규칙 주석: "Idle/Walk -> Jump : 점프 입력 + 접지(발판 위) 상태일 때")
- **재현 시나리오**: `Tick()`의 순서는 `CheckScreenBoundsOrFall` → `GroundedTick` → `JumpPressed` 체크다. `GroundedTick`은 "접지가 아니어도 `_groundLossTimer`가 `fallGraceDuration`(기본 0.1초)을 넘기기 전까지는 `false`를 반환"하므로, **캐릭터가 발판 가장자리를 막 벗어난 직후 0.1초 이내의 "공중이지만 아직 Fall로 전이되지 않은" 짧은 유예 구간에서도 점프 입력이 그대로 허용된다.** 즉 `info.Grounded`를 한 번도 직접 확인하지 않고 "아직 Fall로 강제 전이되지 않았다"는 사실만으로 점프를 허용하는 구조다. `StickmanStateMachine.cs`에 문서화된 전이 규칙은 명시적으로 "접지 상태일 때"라고 조건을 걸어두었는데 실제 구현은 이를 지키지 않는다.
- **근본 원인**: `fallGraceDuration` 하나의 설정값이 "발판 경계 채터링 방지"(원래 목적, 문서화됨)와 "점프 가능 여부 판정"(암묵적 부작용, 문서화 안 됨)이라는 서로 다른 두 목적에 재사용되고 있다. 전자는 히스테리시스로 타당하지만 후자는 의도된 설계(코요테 타임)인지 실수인지 코드/문서 어디에도 명시돼 있지 않다.
- **판단**: 파급 효과는 작다(허공에 뜬 지 0.1초 이내에만 발동, 대부분의 경우 사용자가 체감하기도 전에 사라지는 창) — 그러나 "많은 플랫포머에서 의도적으로 쓰는 코요테 타임"과 "우연히 생긴 로직 허점"은 팀이 명시적으로 결정해야 할 문제이지, 방치할 문제는 아니다.
- **수정 제안**: 의도된 기능이면 `StickConfig`에 `coyoteTimeDuration`을 별도 필드로 신설해 `fallGraceDuration`과 분리하고 `StickmanStateMachine.cs`의 전이 규칙 주석에 "접지 또는 코요테 타임 이내"로 정정한다. 의도치 않은 부작용이면 `if (_blackboard.JumpPressed && info.Grounded)`로 명시적 조건을 추가한다. 어느 쪽이든 지금처럼 "우연히 허용되는" 상태로 다음 Phase에 넘기지 않는다.

### BUG-P1-M6 — `Suspend()/Resume()`가 단일 `Rigidbody2D`만 가정 — Phase 2 다중 파츠 Active Ragdoll 확장 불가
- **파일:라인**: `Assets/_Project/Scripts/Core/StickmanAgent.cs:142-158`(`Suspend`/`Resume`), 대조: `160-167`(`SetRenderersEnabled`)
- **재현 시나리오/논증**: `Suspend()`는 `if (_body != null) _body.simulated = false;`로 **`[RequireComponent(typeof(Rigidbody2D))]`에 의해 보장된 루트 오브젝트의 단일 `Rigidbody2D`** 하나만 멈춘다. 그런데 `CLAUDE.md`/`ARCHITECTURE.md` 0절의 확정된 무빙 방식은 "Active Ragdoll(Rigidbody2D + Joint2D)"이며, Phase 2에서 RAGDOLL 상태에 진입하면 캐릭터는 몸통/머리/양팔/양다리 등 **여러 개의 `Rigidbody2D`가 `Joint2D`로 연결된 구조**가 될 예정이다(`RagdollState.cs` 주석: "전신 Rigidbody2D/Joint2D 시뮬레이션에 완전히 위임"). 전체화면 게임 감지로 `Suspend()`가 호출되는 시점에 캐릭터가 하필 RAGDOLL 상태(피격/낙하 충격으로 널브러진 도중)라면, 현재 코드는 **루트 하나만 멈추고 나머지 사지 Rigidbody2D들은 계속 중력/충돌을 시뮬레이션**한다 — 숨겨진 동안(게임 플레이 중, 수십 분~수 시간) 사지가 계속 물리 반응을 하며 자세가 흐트러지고, `Resume()` 시점에 "일시정지 전과 똑같은 자세로 복귀"해야 한다는 UX_FLOW 9절-4 계약("상태·파라미터 보존, IDLE 리셋 금지")이 사지 단위에서는 깨진다. 또한 숨겨진 동안에도 CPU가 계속 소모된다(원래 Suspend의 목적인 "숨겨진 동안 리소스 절약"도 절반만 달성).
- **근본 원인**: `SetRenderersEnabled`는 이미 `GetComponentsInChildren<Renderer>(true)`로 자식 전체를 순회하도록 일반화돼 있는데, 물리 쪽만 `[RequireComponent(typeof(Rigidbody2D))]`로 확보한 루트 하나만 가정한 채 일반화되지 않은 비대칭 설계다.
- **수정 제안**: `Suspend()/Resume()`를 `Rigidbody2D[] _allBodies = GetComponentsInChildren<Rigidbody2D>(true);`(1회 캐싱, `Awake()`에서)로 일반화해 전체를 순회하며 `.simulated`를 토글하도록 지금(Phase 1) 미리 바꿔두거나, 최소한 Phase 2 Active Ragdoll 착수 태스크의 승인 조건(Definition of Done)에 "Suspend/Resume이 다중 Rigidbody2D를 전부 커버하는지 확인"을 명시적으로 못박는다. 지금 고치는 비용(한 줄 캐싱 + 반복문)이 Phase 2에서 RAGDOLL 상태를 붙인 뒤 회귀 버그로 발견하는 비용보다 훨씬 싸다.

---

## Minor

| # | 파일:라인 | 내용 | 권고 |
|---|---|---|---|
| m1 | `Platform/Windows/Win32WindowService.cs:157-163, 178-184` | `SetClickThrough(false)`(클릭관통을 끄는 요청)도 `_usingUnsafeSelfWindowFallback`이 켜져 있으면 무조건 `NotSupportedException`을 던진다 — "비활성화" 요청은 사실 안전한 연산인데도 가드가 활성/비활성을 구분하지 않고 전부 차단한다. 지금은 문제없지만(둘 다 어차피 호출 안 됨) Phase 3 "대결모드"가 캐릭터 한정으로 클릭관통을 끄려 할 때 이 가드가 그대로 남아있으면 매번 예외 로그가 발생한다. | 진짜 오버레이 구현 시 가드를 걷어내는 김에, 그 전까지는 최소한 `enabled==false` 요청은 가드 예외 대신 조용한 no-op으로 처리하는 것을 검토(위험한 부작용이 없는 연산이므로). |
| m2 | `Platform/FootholdPoller.cs:48-54` | 생성자가 즉시 `Poll()`을 호출하는데, `_service.EnumerateFootholds()`가 예외를 던지면(예: 드문 네이티브 오류) `StickmanAgent.Awake()` 전체가 죽는다 — 방어 코드 없음. | 생성자 내부 `Poll()` 호출을 try/catch로 감싸거나, 최초 폴링 실패 시 빈 캐시로 시작하도록 방어. |
| m3 | `States/GroundSensor.cs:64-83` | 서로 겹치는(Z-order가 다른) 두 발판이 캐릭터 발밑에서 동시에 유효 범위에 들어오는 경우, "첫 번째로 발견된 발판"(EnumWindows가 반환하는 Z-order 최상단)이 접지 Y로 채택된다 — 이는 의도된 동작(가장 위에 보이는 창을 밟는 것이 맞음)이지만, 폴링 주기 사이에 다른 창이 앞으로 나와 Z-order가 바뀌면 다음 폴링 시점에 접지 Y가 미세하게(창 간 높이차만큼) 순간 이동할 수 있다. | 실사용 테스트에서 창 전환이 잦은 환경(다중 모니터, 알림창 등)에서 체감되는 수준인지 Phase 1 실기기 테스트 항목으로 등록 권고. |
| m4 | `Core/StickmanAgent.cs:153-158`(`Resume`), 대조 `Platform/FootholdPoller.cs:67-71`(`PollImmediately`) | `Resume()`이 `FootholdPoller.PollImmediately()`를 호출하지 않는다 — 전체화면 게임 감지로 오래(수십 분~수 시간) 숨어있는 동안 `_footholdPoller.Tick()` 자체가 호출되지 않으므로(Suspended 시 `Update()`가 조기 return) 캐시가 그만큼 오래된 채로 남는다. `Resume()` 직후 최대 `footholdPollInterval`(기본 0.5초)만큼 스테일 캐시로 동작하다가 갱신되는데, 그 사이 실제로는 사라진 창을 여전히 "발판"으로 믿고 그 위에 서 있는 것처럼 보일 수 있다(다음 폴링에 갑자기 뚝 떨어짐). | `Resume()`에서 `_footholdPoller.PollImmediately()`를 명시적으로 호출해 재개 즉시 최신 발판 정보로 시작하도록 수정(이미 이 목적을 위해 만들어진 메서드가 존재하므로 비용이 거의 없음). |

---

## 과학적 토론 로그용 가설 (원인 불명/실측 필요, H1~H4에 이어서 — `Tasklist.md`에도 동일 내용 기록)

Windows 실기기/실빌드 환경이 여전히 없어(Darwin 개발 환경), H1~H4와 마찬가지로 아래도 정적 검토만으로는 확답할 수 없는 가설로 남긴다.

- **가설 H5**: 표준 Windows 데스크톱에서 유저가 열려 있는 모든 앱 창을 최소화(또는 "바탕화면 보기")하면, `Win32WindowService.OnEnumWindow`의 필터(`IsWindowVisible` + `GetWindowTextLength != 0`)를 통과하는 창이 실제로 0개가 된다 — Windows 작업표시줄(`Shell_TrayWnd`) 자체가 보통 제목 없는 창이라 이 필터에서 제외되기 때문이다. 이는 BUG-P1-B1(무한 낙하)이 "드문 엣지 케이스"인지 "일상적으로 발생하는 결함"인지를 가르는 핵심 전제다.
  - **검증 방법**: Windows Standalone 빌드에서 모든 앱 창을 최소화한 뒤 `EnumerateFootholds()`가 반환하는 리스트 길이를 로그로 남겨 실측(0인지, 작업표시줄이 실제로 포함되는지).
- **가설 H6**: BUG-B1(Phase 0)이 완전히 해결되어 `CreateWindowEx` 기반의 진짜 분리 오버레이(`WS_EX_LAYERED|TRANSPARENT|TOPMOST|NOACTIVATE`)가 구현되면, 그 창은 `WS_EX_NOACTIVATE` 특성상 OS로부터 키보드 포커스를 받을 수 없어 `UnityEngine.Input.GetAxisRaw`/`GetButtonDown`이 항상 0/false만 반환하게 된다. 이는 BUG-P1-B2(키보드 의존 이동)가 "지금은 우연히 동작하지만 BUG-B1이 올바르게 해결되는 순간 캐릭터가 영구 정지"로 현실화됨을 의미한다.
  - **검증 방법**: 진짜 분리 오버레이 구현 완료 후, 그 오버레이 창에 포커스를 주려는 시도(클릭, Alt-Tab)가 실제로 실패하는지, 그 상태에서 WASD/Space 입력 시 `Input.GetAxisRaw`/`GetButtonDown`이 반응하는지 실측.

(결과/결론은 Windows 실빌드 확보 후 다음 라운드에서 채워질 수 있음. H1~H4도 여전히 미검증 상태로 이월.)
