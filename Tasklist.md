# StickMate Tasklist — 팀 공유 진행상황 트래커
> 규칙: 각 팀원은 작업 시작 시 상태를 `진행중`으로, 종료 시 `완료/차단/반려`로 갱신하고 한 줄 메모를 남긴다. 리더(아키텍트)는 Phase 게이트를 승인한다.

## 상태 범례
`대기` `진행중` `완료` `차단(사유)` `반려→재작업`

---

## Phase 0 — 스캐폴딩
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| Unity 프로젝트 구조 / 폴더 컨벤션 | Coder | 완료 | Assets/_Project/Scripts 하위 Core/States/Platform(+Windows/MacOS/Mobile)/Plugins/Dialogue 폴더 생성 |
| IPlatformWindowService 인터페이스 + Null 폴백 | Coder | 완료 | 읽기전용 열거(PlatformFoothold)+오버레이 생성/클릭관통/항상위/전체화면감지만 노출, 타윈도우 이동·종료 API 없음. Win32WindowService(#if UNITY_STANDALONE_WIN) P/Invoke 스텁 포함, NullPlatformWindowService 폴백 완료 |
| ScreenshotBackdropPlatformService (모바일) 스텁 | Coder | 완료 | 아키텍처 0-1절 — 유저 탭 지정 발판(가변 리스트, Add/Remove/Clear) + IsConfigured 가드 + 배경 교체 시 발판 동시 무효화(UX 9절-7,8 반영). UNITY_IOS 가드 없이 범용 작성(에디터 테스트 가능) |
| StickmanEventBus, IStickmanState 골격 | Coder | 완료 | StateTransitioned(IsForcedInterrupt 플래그 포함)/FootholdsChanged/DialogueRequested·Expired/GlobalEmergencyStopRequested(UX 9절-6 대비 예약) 이벤트. IStickmanState 8종(Idle/Walk/Jump/Fall/ParkourClimb/Attack/Ragdoll/Getup) + StickmanStateMachine 골격, Ragdoll↔Getup 전이조건 주석화 |
| DialogueIntent 텍스트-액션 계약 스캐폴딩 | Coder | 완료 | StateTransitionContext(전이확정 시에만 발급)를 요구하는 생성자 + TransitionGeneration 불일치 감지로 같은 프레임 자동 만료. 알려진 한계: default(StateTransitionContext) 우회 가능 — 아래 로그 참고 |
| MotionPluginSO / EffectPluginSO 스텁 | Coder | 완료 | [CreateAssetMenu] DLC 매니페스트, 이름/아이콘/적용대상 StickmanStateId[] 필드만 정의 |
| StickConfig 스텁 | Coder | 완료 | 이동/물리/Ragdoll전이 임계값/파쿠르/클릭관통기본값/폴링주기/색상 필드 정의 (기본값은 임시 추정치) |
| UX 플로우 1차 문서 (docs/UX_FLOW.md) | UX Designer | 완료 | 최초실행(데스크톱/모바일)·코어루프·모바일 발판탭 온보딩·파쿠르 피드백·DialogueIntent UX 계약·예외상태·설정창 와이어프레임 작성. Coder 영향사항은 UX_FLOW.md 9절 + 아래 교차 레이어 로그 참고 |
| 모바일 발판 재지정/배경 재캡처 온보딩 흐름 반영 | UX Designer | 완료 | UX_FLOW.md 3절·7절 — 배경 교체 시 발판 좌표 무효화 및 재온보딩 강제 흐름 정의 |
| 방해성 이벤트(인질극/로데오) 긴급 정지 UX 규칙 | UX Designer | 완료 | UX_FLOW.md 6-5절 — 트레이 상시 긴급정지 버튼 + 최대 지속시간 상한 정의, Phase 3/4 Coder 구현 전 필독 |

## Phase 1 — 코어 루프
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| 중력/발판 인식(창 상단 Y 스냅) | Coder | 완료 | Debugger의 EnumerateFootholds 폴링 규율 지적사항 동시 반영. **[Debugger, BUG_REPORT_PHASE0.md BUG-M3/BUG-M5]** `EnumerateFootholds()` "매 프레임 호출 금지" 계약을 강제하는 코드가 전혀 없음(주석뿐) — `StickConfig.footholdPollInterval`을 소비하는 `FootholdPoller` 유틸 없이 Tick()에서 직접 호출하면 Win32는 `EnumWindows`+창마다 3회 P/Invoke라 24시간 상주 앱에 실제 CPU 부담. 또한 좌상단/좌하단 원점 변환·DPI·멀티모니터를 다룰 공용 좌표 변환 유틸이 전무 — 각 상태가 개별 구현하면 좌표계 혼용 버그 위험 높음. 두 인프라 모두 이 작업의 전제로 선행 권고. **[Coder, 2026-08-27]** BUG-M3/M5 반영 완료 — `Platform/FootholdPoller.cs`(주기 폴링+변경시에만 이벤트) 및 `Platform/ScreenCoordinateConverter.cs`(Unity 스크린↔OS 데스크톱, DPI 배율 `StickConfig.desktopDpiScale`) 신규 추가, `States/GroundSensor.cs`가 이 두 유틸만 거쳐 접지 판정(허용오차 `StickConfig.groundSnapTolerance`). 모든 State는 `IPlatformWindowService`를 직접 호출하지 않고 `StickmanBlackboard.SenseGround()`만 사용하도록 강제. **[Debugger, 2026-08-27, 2차 리포트 — BLOCKER, BUG-P1-B1]** `docs/BUG_REPORT_PHASE1.md` 참고 — 발판이 0개가 되는 순간(`Win32WindowService`가 제목 있는 가시 창을 하나도 못 찾음, 흔한 상황) `GroundSensor`/`GroundedTick`이 안전망 없이 캐릭터를 무한 낙하시킴. `NullPlatformWindowService`의 "화면 하단 더미 발판" 폴백이 실제 데스크톱 구현체(`Win32WindowService`)에는 이식되지 않음 — UX_FLOW.md 1-A/6-1절의 "빈 상태" 요구사항 위반. 이 행을 "완료"로 유지하려면 이 폴백 이식이 선행되어야 함. 관련: `BUG-P1-M1`(`Camera.main` 미캐시 재검증도 동일 실패모드). **[Coder, 2026-08-27, 3차 반영 — BUG-P1-B1(Blocker)/M1/M4 해소]** (b)안 채택 — 데코레이터 `Platform/FallbackPlatformWindowService.cs` 신설: 내부 `IPlatformWindowService`의 `EnumerateFootholds()`가 0개를 반환하면 "화면 하단 가로 전체 폭" 합성 발판 1개(`NullPlatformWindowService`와 동일 개념)로 대체 반환. `StickmanAgent.CreatePlatformService()`의 `UNITY_STANDALONE_WIN` 분기가 `Win32WindowService`를 이 데코레이터로 감싸 반환하도록 배선(에디터 `NullPlatformWindowService`는 이미 항상 더미 발판을 반환해 감쌀 필요 없음). **모바일(`ScreenshotBackdropPlatformService`)은 의도적으로 감싸지 않음** — 그 서비스의 "발판 0개"는 버그가 아니라 "유저가 아직 탭 지정 안 함"이라는 의도된 온보딩 게이트 신호(`IsConfigured`)라서, 감싸면 그 게이트가 조용히 무력화되기 때문(코드 주석에 판단 근거 명시). `ICursorPositionService`는 내부 서비스가 지원하면 그대로 통과시켜(delegate) 기존 커서 조회 설계가 깨지지 않게 함. BUG-P1-M1도 함께 반영 — `StickmanAgent.Awake()`가 `Camera.main == null`이면 `Debug.LogError` 즉시 발생(리포트 수정안 (a), 재획득 로직까지는 아님). BUG-P1-M4도 함께 반영 — `FootholdPoller.CachedFootholds`가 `_cache.AsReadOnly()`로 감싼 `ReadOnlyCollection`(생성자에서 1회만 래핑, 매 프레임 재할당 없음)을 반환해 캐스팅 변형 경로 차단. |
| 화면 경계 이탈 → 낙하 | Coder | 완료 | **[Debugger]** 위 BUG-M5(좌표 변환 유틸)와 동일 인프라 의존. 교차 레이어 로그 9절-5(모니터 경계 노출, 아직 미반영)도 이 작업과 함께 결정 필요 — "바닥 없는 논리적 간격" 판정은 모니터 경계 API 없이는 구현 불가. **[Coder, 2026-08-27]** "모든 발판의 좌우 범위 이탈 → Fall"은 `StickmanBlackboard.CheckScreenBoundsOrFall()`로 구현 완료(Idle/Walk/Jump/Fall 공통 호출). 단, 9절-5 "모니터 간 논리적 간격"은 여전히 미반영 — `IPlatformWindowService`에 모니터 경계 열거 API가 없어 범위 밖(별도 작업 필요), Debugger 지적대로 미해결 상태 유지. **[Debugger, 2026-08-27, 2차 리포트]** `CheckScreenBoundsOrFall`은 발판이 0개면 스스로 no-op되어 "화면 밖 낙하" 안전망 역할을 못 함 — 위 BUG-P1-B1과 같은 근본 원인 공유. |
| IDLE/WALK/JUMP/FALL 상태 구현 | Coder | 완료 | **[Debugger, BUG-M2]** `StickmanStateMachine.ChangeState()`가 원자적이지 않음 — `_states[next]` 조회가 `Exit()`/`TransitionGeneration` 증가 **이후**에 일어나, 미등록 키로 호출되면 `KeyNotFoundException` 발생 시점에 `_current`가 이미 Exit된 옛 상태를 계속 가리키는 "좀비" 상태로 고착되고 복구 경로가 없음(상태머신 데드락). 여러 상태를 오가는 이 작업에서 가장 먼저 걸릴 수 있는 문제이므로 `TryGetValue` 선검증으로 선반영 권고. **[Coder, 2026-08-27]** BUG-M2 반영 완료 — `StickmanStateMachine.ChangeState()`가 `TryGetValue` 선검증 후 실패 시 뮤테이션 없이 안전 반환하도록 수정. BUG-M1도 함께 반영 — `StateTransitionContext` 생성자/필드, `CurrentTransitionGeneration`을 `public`→`internal`로 좁힘(어셈블리 내부 위조까지는 못 막는 절반의 방어라는 한계는 Debugger 지적대로 유지, Phase 2 토큰화로 완결 예정). Idle/Walk/Jump/Fall Tick() 전이 규칙 실구현(`States/StickmanBlackboard.cs`, `States/GroundSensor.cs` 신규) — Idle<->Walk 입력 기반, Jump 정점통과→Fall, Fall 착지confirm(`fallGraceDuration` 재사용)→Idle/Walk. **[Debugger, 2026-08-27, 2차 리포트 — BLOCKER, BUG-P1-B2]** `docs/BUG_REPORT_PHASE1.md` 참고 — Idle/Walk 전이의 유일한 입력원이 `StickmanAgent.Update()`의 `Input.GetAxisRaw("Horizontal")`/`GetButtonDown("Jump")`(키보드)뿐이고, 자율 배회 AI/행동 결정 로직이 프로젝트 어디에도 없음. UX_FLOW.md 2절/8절 P0 원칙("아무것도 안 해도 재미있어야 함")을 정면 위반하며, BUG-B1(Phase0)이 올바르게 해결되어 진짜 `WS_EX_NOACTIVATE` 오버레이가 완성되면 그 창은 키보드 포커스를 받을 수 없어 이 입력 경로 자체가 영구히 죽는다(가설 H6) — 즉 지금은 "우연히 동작"하는 것뿐이다. 이 행을 "완료"로 유지하면 안 됨. 부가로 `BUG-P1-M5`(Jump 전이가 실제 접지 여부를 확인하지 않음, 문서화된 전이 규칙과 불일치)도 이 작업 범위에서 함께 발견됨. **[UX Designer, 2026-08-27, 긴급]** BUG-P1-B2 대응 자율 배회 AI 행동 설계 완료 — `docs/UX_FLOW.md` **26절** 참고. Idle/Walk 지속시간·경계 도달 시 정지+반전(90%)/점프시도(10%)·유휴 모션 트리거·지터 비율 등 수치표 전부 수록. 커서 근접 반응은 Phase 2로 연기(훅만 준비 권고, 26-4). 키보드 입력은 완전 제거 확정, 대결모드(Phase 3)도 마우스 기반 유지 재확인(26-5). Coder용 `IMovementIntentSource`/`AutoWanderController` 계약 제안 포함(26-6) — 기존 IdleState/WalkState/JumpState/FallState는 무수정으로 소비 가능한 구조. `GroundSensor.GroundInfo`에 "현재 딛고 있는 발판 하나"의 좌우 경계 필드 추가가 선행 필요함을 26-7에 명시(전역 통합 경계만으로는 26-2 판정 불가). **[Coder, 2026-08-27, 3차 반영 — BUG-P1-B2/M5 해소]** `UnityEngine.Input.GetAxisRaw`/`GetButtonDown` 참조를 `StickmanAgent.cs`에서 완전히 제거(프로젝트 전체 grep으로 재확인, 키보드 Input 참조 0건). 최소 계약 `States/IMovementIntentSource.cs`(`float MoveInputX { get; } bool JumpRequested { get; }`) 신설, `StickmanBlackboard.MoveInputX`/`JumpPressed`를 필드에서 `IntentSource` 기반 계산 프로퍼티로 전환해 State 4종은 무수정으로 그대로 동작(예상대로). UX 26절 스펙이 도착해 **임시 구현 단계 없이 정식 `States/AutoWanderController.cs`로 바로 구현** — Idle 2~6초/Walk 1.5~4초 랜덤(26-1), Walk 중 0.5초마다 8% 즉흥 방향전환(최대 1회/페이즈), Idle 종료 후 Walk 75%/연장 20%/제자리점프 5% 가중치 분기, "지금 딛고 있는 발판" 단위 경계 판정(26-2, 90% 정지+반전 / 10% 점프시도, 화면 실제 끝에서는 점프확률 강제 0), ±17.5% 지터(26-3, 개체별 독립 `System.Random` 시드), 두리번거리기/앉기·하품 트리거를 `StickmanEventBus.WanderAmbientMotionRequested`로 발행(실제 재생은 Phase 2 렌더링 담당, 지금은 트리거 조건만). 26-7 요구사항대로 `GroundSensor.GroundInfo`에 `CurrentFootholdLeftWorldX`/`RightWorldX`(현재 딛고 있는 발판 하나만의 경계) 필드 추가 — 전역 통합 경계(`ScreenLeft/RightWorldX`)와 분리. `JumpRequested`는 매 `Tick()` 시작 시 리셋되는 1프레임 펄스 계약 준수. 26-4 커서 근접 반응은 Phase 2로 연기(UX 확정) — `AutoWanderController.CursorProvider` 훅만 예약해 `StickmanAgent.TryGetCursorPosition`을 미리 연결해둠(로직은 비어 있음). 키보드는 대결모드(Phase 3)에도 부활 안 함(26-5 확정 재확인). `StickConfig`에 `wander*` 필드 19개 신설(26-6 제안표 그대로, 매직넘버 금지 컨벤션 준수). BUG-P1-M5(코요테 타임)도 이 작업 범위에서 함께 해소 — 아래 "클릭 관통" 행 인접 항목이 아니라 이 행 소관이라 별도 기재: `StickConfig.coyoteTimeDuration` 신설, `StickmanBlackboard.IsWithinCoyoteTime()` 추가, IdleState/WalkState의 Jump 전이 조건에 `&& _blackboard.IsWithinCoyoteTime(info)` 명시 추가(`StickmanStateMachine.cs` 전이 규칙 주석도 "접지 또는 코요테 타임 이내"로 정정). Unity 배치모드 컴파일 재검증 완료(에러 0, 경고 2 — RagdollState/GetupState 기존 미사용 필드뿐, 신규 경고 없음). **자율 배회 AI가 UX 26절 스펙의 최종 구현이고 임시 구현 단계가 없었으므로 이 행을 완료로 전환한다.** 남은 것은 Phase 2 렌더링 레이어 연동(두리번거리기/앉기 애니메이션 실제 재생, 26-4 커서 반응 로직)뿐이며 이는 애초에 이 작업의 Phase 1 스코프가 아니다. |
| 클릭 관통 기본 ON | Coder | 진행중 | **[Debugger — BLOCKER, BUG-B1]** `Win32WindowService.CreateOverlayWindow()`는 실제 오버레이 창이 아니라 Unity 게임 자신의 `MainWindowHandle`을 재사용하는 스텁. 지금 상태로 `SetClickThrough(true)`/`SetAlwaysOnTop(true)`를 그대로 호출하면 **게임 창 자체가 클릭관통되어 모든 마우스 입력이 막히고**, 항상 최상단 고정으로 데스크톱을 가릴 수 있음(비침해 원칙 정반대). `WS_EX_NOACTIVATE` 누락으로 포커스 탈취 위험도 있음(가설 H2, 검증 필요). **이 작업을 현재 스텁 그대로 완료 처리하지 말 것 — 별도 HWND 기반 진짜 오버레이 구현이 선행되어야 함.** 상세: `docs/BUG_REPORT_PHASE0.md` Blocker 섹션. **[Coder, 2026-08-27]** Architect 판단대로 "진짜 HWND 오버레이 구현"은 이번 Phase 1 범위를 넘어 보류. 대신 임시 안전가드 적용: `Win32WindowService`에 `_usingUnsafeSelfWindowFallback` 플래그 추가, `SetClickThrough`/`SetAlwaysOnTop`가 이 플래그가 켜진 동안(현재 항상 켜짐) `NotSupportedException`을 던져 게임 창 자체 파괴를 원천 차단. `WS_EX_NOACTIVATE` 상수 추가 및 `SetClickThrough`에 적용(BUG-B1(c)). `Core/StickmanAgent.cs`에 "앱 시작 시 SetClickThrough(true) 호출 지점"을 마련했고, 이 예외를 잡아 로그만 남기고 나머지 초기화는 계속 진행하도록 처리 — 따라서 Windows에서는 클릭관통이 아직 실제로 켜지지 않는다(의도된 안전 실패). 그래서 이 행은 완료 처리하지 않음 — 실제 분리 오버레이(CreateWindowEx) 구현이 후속 작업으로 남아있음. 커서 좌표 조회는 `ICursorPositionService`(신규, `Platform/ICursorPositionService.cs`)로 클릭관통과 완전히 독립된 경로에 배선 완료(UX 9절-3), Win32/Null 양쪽 구현. **[Debugger, 2026-08-27, 2차 리포트]** `docs/BUG_REPORT_PHASE1.md` BUG-P1-M3 — `StickmanAgent.Start()`가 `CreateOverlayWindow()`의 반환값을 무시함. `Win32WindowService`의 `_overlayHwnd==IntPtr.Zero` 체크가 `_usingUnsafeSelfWindowFallback` 체크보다 먼저 실행되므로, 가설 H4(부트스트랩 타이밍에 핸들이 Zero)가 실제 발생하면 `SetClickThrough`/`SetAlwaysOnTop`가 예외 없이 조용히 no-op되어 BUG-B1 가드가 의도한 "알아챌 수 있게 실패"가 지켜지지 않음 — 반환값 체크+로그 추가 권고(값싼 수정). `ICursorPositionService` 신규 인터페이스 분리 설계는 Debugger 검토 결과 **승인**(모바일에 억지 no-op 구현을 강요하지 않는 게 더 안전). **[Coder, 2026-08-27, 3차 반영 — BUG-P1-M3 해소]** `StickmanAgent.Start()`가 `CreateOverlayWindow()`의 반환값을 `overlayReady`로 받아 `false`면 `Debug.LogWarning`을 남기도록 수정(가설 H4 진단 사각지대 해소). 단, 이 행 자체는 여전히 **완료 처리하지 않음** — 진짜 분리 오버레이(CreateWindowEx) 구현이라는 원래 반려 사유는 이번 라운드 스코프 밖이라 그대로 남아 있음. |
| 전체화면 게임 감지 → 자동 숨김 | Coder | 완료 | **[Debugger]** `IsFullscreenAppActive()`의 "전경 창 사각형 == 모니터 전체 사각형" 휴리스틱은 향후 진짜 오버레이가 화면 전체 크기 투명 창으로 구현되면 자기 자신을 오탐할 위험(현재 `fg == _overlayHwnd` 자기 제외로 방어하나 BUG-B1 재구현과 함께 재검증 필요). 교차 레이어 로그 9절-4 "Suspended" 개념 미반영도 이 작업의 선행 조건. **[Coder, 2026-08-27]** `Core/StickmanAgent.cs`가 `StickConfig.fullscreenPollInterval` 주기로 `IsFullscreenAppActive()`를 폴링해 감지 시 Suspend, 해제 시 Resume. Suspended 개념은 "상태 인스턴스를 유지한 채 `Machine.Tick()` 호출 자체를 건너뜀"으로 구현(IDLE 리셋 없음, 진행 중이던 상태의 내부 타이머까지 그대로 보존) + `Rigidbody2D.simulated=false`로 물리도 함께 정지 + 렌더러 비활성화. Debugger 지적대로 오버레이 자기오탐 재검증은 BUG-B1 실구현 이후 과제로 남김. **[Coder, 2026-08-27, 3차 반영 — BUG-P1-M6/Minor m4 해소]** `Suspend()`/`Resume()`가 단일 루트 `Rigidbody2D`만 토글하던 것을 `Awake()`에서 `GetComponentsInChildren<Rigidbody2D>(true)`로 1회 캐싱한 `_allBodies` 전체를 순회하도록 일반화(`SetRenderersEnabled`와 대칭, Phase 2 다중 파츠 Active Ragdoll 대비). `Resume()`이 `_footholdPoller.PollImmediately()`를 호출하도록 추가(Minor m4) — Suspended 동안 폴러 `Tick()` 자체가 건너뛰어져 스테일 캐시로 재개되는 문제 해소. |
| 위 항목 버그 리포트 | Debugger | 완료 | 1차 리포트 `docs/BUG_REPORT_PHASE0.md` 작성 완료 — Blocker 1건(BUG-B1), Major 8건(BUG-M1~M8), Minor 8건. Coder로 반려 필요 판정, 수정 우선순위 리포트 상단에 명시. Phase 1 실구현 진행되며 위 각 행에 대응 메모 추가 완료. Phase 1 실구현이 더 진행되면 2차 리포트 예정. **[Debugger, 2026-08-27]** 2차 리포트 `docs/BUG_REPORT_PHASE1.md` 작성 완료(실제 Unity 6 LTS 프로젝트 전환 후 첫 리뷰, 배치모드 컴파일 에러 0/경고 2 확인됨 — 이번 리뷰는 논리 검증 중심) — Blocker 2건(BUG-P1-B1 발판0개 무한낙하, BUG-P1-B2 키보드 의존 이동/자율 AI 부재), Major 6건(BUG-P1-M1~M6), Minor 4건. **Coder로 반려 필요** 판정 유지. 신규 가설 H5/H6 추가(아래 과학적 토론 로그). **[Coder, 2026-08-27, 3차 반영 — 2차 리포트 Blocker 2건/Major 6건/Minor 4건 전체 대응]** Blocker/Major 8항목 전부 반영: BUG-P1-B2(키보드 완전 제거 → `AutoWanderController`, UX_FLOW.md 26절 정식 구현), BUG-P1-B1(`FallbackPlatformWindowService` 데코레이터로 화면 하단 폴백 발판 데스크톱 실구현체에 이식), BUG-P1-M1(`Camera.main` null 시 `Debug.LogError`), BUG-P1-M2(`StickmanStateMachine` 생성/`Start()` 분리), BUG-P1-M3(`CreateOverlayWindow()` 반환값 확인+로그), BUG-P1-M4(`FootholdPoller.CachedFootholds`를 `ReadOnlyCollection`으로), BUG-P1-M5(`coyoteTimeDuration` 별도 필드 신설, 의도된 코요테 타임으로 채택), BUG-P1-M6(`Suspend()/Resume()` 다중 Rigidbody2D 일반화). Minor m4(`Resume()`의 `PollImmediately()`)도 반영. Unity 배치모드 재검증: 에러 0건, 경고 2건(RagdollState/GetupState 기존 미사용 필드, 신규 경고 없음) — 상세는 각 항목 소관 행의 [Coder] 메모 참고. Debugger 3차 리뷰 요청. |

## Phase 2 — Ragdoll / 파쿠르 / 텍스트-액션 계약
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| Active Ragdoll(RAGDOLL/GETUP) 전환 | Coder | 완료 | **[Debugger, 2026-08-27, docs/BUG_REPORT_PHASE1.md 참고]** 착수 시 필히 포함할 선행 수정 2건: (1) **BUG-P1-M2** — `StickmanStateMachine` 생성자가 즉시 `ChangeState(initialState)`를 호출해 `Enter()`가 `blackboard.Machine` 배선 전에 실행됨(현재는 Idle이 Machine을 안 써서 우연히 안전). 생성자를 `StickmanStateMachine(states)`(Enter 호출 없음)와 `Start(initialState)`(Machine 배선 완료 후 1회 호출, 이때 ChangeState 실행)로 분리해 구조적으로 안전하게 만들 것 — 리포트에 구체적 코드 스니펫 제시함. (2) **BUG-P1-M6** — `StickmanAgent.Suspend()/Resume()`가 `[RequireComponent(typeof(Rigidbody2D))]`로 확보한 단일 루트 `Rigidbody2D`만 멈춤/재개함. RAGDOLL은 몸통/사지 여러 개의 `Rigidbody2D`+`Joint2D` 구조이므로, 전체화면 감지로 Suspend 중 캐릭터가 RAGDOLL 상태면 사지가 계속 시뮬레이션되어 자세가 흐트러지고 CPU도 계속 소모됨 — `GetComponentsInChildren<Rigidbody2D>(true)`로 전체 순회하도록 일반화 필요(`SetRenderersEnabled`는 이미 이렇게 일반화되어 있어 비대칭). **[Coder, 2026-08-27]** 두 선행 수정 모두 Phase 1 반려 재작업 라운드에서 선반영 완료 — (1) `StickmanStateMachine`이 `StickmanStateMachine(states)`/`Start(initialState)`로 분리됨(`StickmanAgent.Awake()`가 `blackboard.Machine` 배선 후 `Start()` 호출). (2) `StickmanAgent.Suspend()/Resume()`이 `_allBodies`(`GetComponentsInChildren<Rigidbody2D>(true)`) 전체 순회로 일반화됨. Phase 2 착수 시 이 두 항목은 이미 충족된 상태에서 시작 가능. **[UX Designer, 2026-08-27]** 피격 임팩트 연출(히트스톱/흑백플래시/폭발선 3단 콤보)·충격크기별 최소 낙사시간 구간표·GETUP 3단계(정지유지→꿈틀→기상) 분해 및 권장 지속시간·유저 개입 없음 재확인 — `docs/UX_FLOW.md` **29절** 참고. `StickConfig` 신규 필드 제안 포함(`ragdollMinFloorDurationLow/Mid/High`, `ragdollMaxFloorDuration`, `getupStirDelay/Duration`, `getupRiseDuration`). **[Coder, 2026-08-27, Phase 2 구현 완료]** 아키텍처 0절/`StickmanStateMachine.cs` 전이 규칙 주석 그대로 구현. 신규 `States/RagdollRig.cs`(순수 C#, `FootholdPoller`/`GroundSensor`와 같은 컨벤션) — `Transform` 루트에서 `GetComponentsInChildren<Rigidbody2D>(true)`/`<HingeJoint2D>(true)`로 파츠/관절을 1회 캐싱(Phase1 `Suspend()/Resume()`와 동일 패턴 재사용). `EnterRagdoll()`이 모든 `HingeJoint2D.useMotor=false`로 전신을 물리에 위임, `GetMaxSpeed()`(전신 중 최댓값, 평균 아님 — 사지 하나가 계속 요동쳐도 오판 방지)를 `RagdollState.Tick()`의 `_settleTimer` 누적/리셋 판정에 실제로 사용(기존 미사용 경고 필드 해소). `RagdollState.Enter()`가 `_blackboard.GetRagdollRig()?.EnterRagdoll()` 호출. `GetupState`는 `BeginGetup()`으로 널브러진 관절 각도를 캡처하고 `TickGetup(progress, motorGain, maxTorque)`가 비례 제어(P control)로 각 관절을 직립(0도)까지 구동 — `_getupProgress`(기존 미사용 경고 필드)를 `StickConfig.getupDuration`으로 정규화해 실제로 사용. RAGDOLL 강제 진입은 상태 전용 코드가 아니라 `Core/StickmanAgent.ReportExternalImpact(float impulseMagnitude)`라는 **단일 진입점**으로 설계 — `OnCollisionEnter2D`(루트 파츠)와 신규 `Core/RagdollLimbImpactRelay.cs`(비루트 파츠, 프리팹 부착용, 실제 배선은 Phase 2 범위 밖)가 여기로 통지하면 `impulseMagnitude >= ragdollForceThreshold`일 때 현재 상태가 무엇이든(Idle/Walk/Jump/Fall/ParkourClimb/Attack/Getup) `ChangeState(Ragdoll, isForcedInterrupt: true)`. Getup 도중 재인터럽트도 이 단일 경로로 자동 커버(별도 코드 불필요 — `ChangeState`가 이미 Ragdoll이어도 `Enter()`를 재실행해 `_settleTimer`를 리셋). `StickConfig`에 `getupDuration`(0.6)/`getupMotorGain`(6)/`getupMaxMotorTorque`(50) 신규 필드 추가. UX 29절(히트스톱/흑백플래시/폭발선, 충격크기별 낙사시간 구간표, GETUP 3단계 Stir/Rise 분해)은 **이번 라운드에 구현하지 않음** — 29-1은 `Time.timeScale` 기반 전역 이펙트라 렌더링 레이어 설계가 먼저 필요하고, 29-2/29-3은 현재의 단순 settle+hold/단일진행도 메커니즘 위에 얹는 정제(refinement)라 판단해 다음 라운드로 미룸(아래 교차 레이어 로그에 상세 근거 기록, Debugger/Architect 조율 요청). 대신 UX 31절(최우선) 요구사항은 이번에 함께 반영 — 아래 "DialogueIntent" 행 참고. |
| PARKOUR_CLIMB (벽타기/매달리기/구르기) | Coder | 완료 | **[UX Designer, 2026-08-27]** 4절 보강 — 벽 근접 예고 UI는 넣지 않기로 결정(캐릭터 자율행동이라 신호를 받을 유저 판단 주체가 없음, 대신 손 뻗기 예비동작으로 diegetic 처리), 스태미나 게이지 신설 안 함(HANG 기존 타임아웃 재사용 + 잔여시간 반비례 손떨림 진폭 연출로 대체), ROLL은 구조적으로 실패 조건 없음을 확인(RAGDOLL 임계값 미만에서만 진입하므로 필터링이 이미 선행됨) + 임계값 그레이존은 ROLL 쪽으로 편향 권고. `docs/UX_FLOW.md` **30절** 참고. **[Coder, 2026-08-27, Phase 2 구현 완료]** 트리거는 키보드가 아니라 26절 `AutoWanderController`의 `JumpRequested` 펄스 + 발판 경계 근접 조합 그대로 사용(신규 로직 추가 없이 기존 10% 점프시도 분기를 자연 확장) — `WalkState.Tick()`의 기존 `JumpPressed && IsWithinCoyoteTime` 분기 안에서 `info.Grounded`일 때만 `StickmanBlackboard.TryFindClimbableWall(info, direction, ...)`을 먼저 확인해 벽(진행방향에 `parkourDetectionRadius` 이상 더 높은 발판)이 있으면 `ParkourClimb`, 없으면 기존 `Jump`로 분기. `IdleState`의 "제자리 점프"는 방향 의도가 없어(`MoveInputX==0`) 의도적으로 이 판정을 건너뜀(코드 주석에 근거 명시, UX 4절 "애매하면 안전한 쪽" 원칙 적용). 좌표 변환/발판 순회 로직은 기존 컨벤션대로 전부 `GroundSensor`에 신규 정적 메서드 2개로 추가(`TryFindClimbableWall`, `TryGetFootholdTopWorldY` — `PlatformFoothold.Handle`로 "잡은 발판"을 식별해 등반 중 매 프레임 "잡을 곳이 사라졌는지"(창 이동/닫힘)를 재확인, 사라지면 즉시 Fall). `StickmanBlackboard`에 얇은 래퍼 2개만 노출해 `ParkourClimbState`/`WalkState`가 좌표 변환식을 직접 만들지 않게 함(BUG-M5 컨벤션 유지). `ParkourClimbState.Enter()`가 속도를 죽여 매달림을 표현하고 `Tick()`이 `parkourClimbDuration`(신규, 0.5초) 동안 `Body.position.y`를 시작 높이->벽 상단으로 Lerp, 완료 시 이동 입력 유무로 Idle/Walk 복귀. 낙하 구르기(ROLL) 훅: `FallState`가 `Enter()` 시점 월드 Y를 캐싱해두고 착지 확정 시 낙하 높이를 계산, `StickConfig.rollLandingHeightThreshold`(신규, 2.0) 이상이면 신규 `StickmanEventBus.LandingRollRequested(float fallHeight)` 이벤트만 발행(구독자 없음 — 실제 파티클/애니메이션은 Phase 2+ 렌더링 레이어 담당, WanderAmbientMotionRequested와 동일 패턴). UX 30-3이 가정한 "낙하 충격이 `ragdollForceThreshold`를 넘으면 이미 RAGDOLL로 처리됨" 모델과 지금의 "낙하 높이 기반" 훅은 서로 다른 축(물리 충격량 vs 월드 높이)이라는 점을 교차 레이어 로그에 명시 — 착지는 가상 발판(Collider2D 없음) 기반이라 `OnCollisionEnter2D`가 착지 시 발동하지 않으므로 두 모델이 자동으로 합쳐지지 않는다(Debugger/Architect 조율 요청). **[Debugger, 2026-08-27 — Major, BUG-P2-M1, 반려 필요]** `Tick()`(`ParkourClimbState.cs:99-105`)이 `Body.position.y`만 매 프레임 Lerp로 덮어쓰고 `Body.linearVelocity.y`는 `Enter()` 1회만 0으로 초기화 — 등반 중 중력이 `linearVelocity.y`에 숨어서 계속 누적되다가 등반 완료 직후 Idle/Walk 전이 시점부터 다음 프레임 `SnapToGround`가 상쇄하기 전까지 최소 1 FixedUpdate 동안 실제로 적용되어 착지 튐(pop)이 매 등반마다 발생함(상태 자신의 주석 및 UX 4절 "급격한 포즈 점프 금지" 위반). 수정안: `Tick()`에서도 `linearVelocity.y`를 0으로 재확정(기존 `SnapToGround` 패턴과 동일). 상세: `docs/BUG_REPORT_PHASE2.md`. **[Coder, 2026-08-28, 하강 방향 완성]** 사용자 명시 요청 "내려갈때도 매달려서 내려가는형태로"를 신규 `States/LedgeHangState.cs` + `StickmanStateId.LedgeHang`으로 구현(ParkourClimb 확장이 아닌 별도 상태로 간 근거는 그 enum 문서 참고 — 3페이즈이고 종료가 항상 Fall이라 공유할 코드가 사실상 없다). `GroundSensor.TryFindDescendTarget`/`TryGetFootholdEdgeWorld` 신설, `IMovementIntentSource.LedgeHangRequested` 펄스 신설, `StickmanPoseAnimator.ApplyLedgeHangPose`(팔을 위로 뻗어 모서리를 잡고 몸이 늘어진 포즈) 신설. 발동 확률 `StickConfig.ledgeHangChance`(0.35). 안전 규칙 3종(발판 소실 시 즉시 낙하 / 절대 상한 타임아웃 / 화면 밖 금지) 전부 `Tests/PlayMode/LedgeHangDescentTests.cs`로 실측(1프레임 낙하, 0.900초 타임아웃). 상세는 이 파일 맨 아래 "매달려 내려가기(LedgeHang) + 보행 접지 보정" 절. |
| DialogueIntent 텍스트-액션 싱크 계약 | Coder | 완료 | **[UX Designer, 2026-08-27, 최우선]** BUG-M7(`Func<StickmanStateId,string>`이 파라미터를 못 실음) 대응 — "한 발 더"/"오늘은 여기까지"가 반드시 같은 매핑 함수·같은 파라미터 스냅샷 안의 조건 분기여야 하며 서로 다른 시점/이벤트에서 독립 트리거되면 안 된다는 핵심 원칙 + Attack/Ragdoll/Getup/ParkourClimb/(Phase3 격파 미니게임 선행) 5개 상태-파라미터-텍스트 매핑 표(`IHasDialogueParams` 파이프라인에 바로 꽂을 수 있는 실전 예시) + Test Engineer용 일반 체크리스트 한 줄. `docs/UX_FLOW.md` **31절** 참고. **[Coder, 2026-08-27, Phase 2 구현 완료 — BUG-P0-M1 + BUG-M7 동시 해결]** **(BUG-P0-M1, 토큰화 완결)** `StateTransitionContext`를 `readonly struct`에서 `sealed class`로 전환(`States/IStickmanState.cs`) — 클래스는 암묵적 매개변수 없는 public 생성자가 없어 `default(...)`/`new StateTransitionContext()`류의 "공짜 위조"가 컴파일 자체가 안 됨. 추가로 1회용 발급 토큰(`TryConsumeToken()`, 최초 호출만 true)을 내부에 둬 `DialogueIntent` 생성자가 텍스트를 만들기 직전에 소비하도록 해, 같은 컨텍스트로 `DialogueIntent`를 두 번 만드는 시도를 `InvalidOperationException`으로 차단(Debugger가 지목한 "같은 세대로 위조된 컨텍스트 재사용" 경로 원천 차단). `IStickmanState.Enter(StateTransitionContext context)` 시그니처는 타입 이름이 동일해 변경 없음. 정직하게 문서화한 잔여 한계: asmdef 분리가 없어 같은 어셈블리 내부 코드가 `internal` 생성자를 직접 호출해 완전히 새로운 컨텍스트를 조작하는 것 자체는 여전히 가능(다만 이제는 "재사용"이 아니라 "처음부터 조작"하는 훨씬 노골적인 코드가 되어 리뷰로 걸러내기 쉬움). **(BUG-M7, 파라미터 파이프라인)** 신규 `Dialogue/IHasDialogueParams.cs`(`object DialogueParams { get; }`) — 상태가 선택적으로 구현해 파라미터를 구조적으로 노출. `StickmanStateMachine`에 `internal IStickmanState CurrentState => _current;` 추가(`ChangeState()`가 `Enter()` 호출 **전**에 이미 `_current`를 새 상태로 바꿔두므로 `Enter()` 안에서는 항상 "지금 확정된 그 상태"를 가리킴). `DialogueIntent`에 `Func<StickmanStateId, object, string>` 오버로드 추가 — 파라미터는 호출자가 자유롭게 넘기는 게 아니라 생성자 내부에서 `context.OriginMachine.CurrentState as IHasDialogueParams`로 직접 읽어온다(상태와 무관한 파라미터 위조 불가, 원칙 1을 파라미터 레벨까지 확장). 파라미터 없는 기존 `Func<StickmanStateId,string>` 생성자는 편의 오버로드로 유지(내부적으로 위임). **실전 시연 3건(UX 31-2 표 그대로)**: `AttackState`(`ShotsRemaining`, "N발 더!"/"타앗!"), `RagdollState`(`ImpactRatio` = 충격량/`ragdollForceThreshold`, "윽...!"/"으악!"/"으아아아악?!" 3구간 — `StickmanAgent.ReportExternalImpact()`가 전이 직전 충격량을 `StickmanBlackboard.LastImpactMagnitude`에 스냅샷), `ParkourClimbState`(`ClimbHeightUnits`, "가뿐하네"/"헉... 높다"). 세 곳 모두 31-1 원칙(같은 매핑 함수, 같은 `Enter()` 스냅샷, 함수 내부 조건 분기만 허용)을 그대로 따름 — 별도 이벤트/타이머로 뒤늦게 텍스트를 바꾸는 경로 없음. `Getup`(reimpactCount)과 Phase3 격파 미니게임(chargeRatio) 예시는 각각 "여러 사이클에 걸친 카운터 추적"과 "Phase 3 스코프"라 이번 라운드에 구현하지 않음(파이프라인 자체는 이미 3개 상태로 검증 완료라 추가 비용 대비 실익 낮다고 판단, 필요 시 다음 라운드에 동일 패턴으로 추가 가능). Unity 배치모드 컴파일 재검증: 에러 0/경고 0(기존 2건도 `_settleTimer`/`_getupProgress` 실사용으로 자연 소멸). **[Debugger, 2026-08-27 — Minor]** 토큰/파이프라인 자체는 우회 경로 없이 정확히 구현됨(모든 `DialogueIntent` 생성이 `StateTransitionContext.TryConsumeToken()` 한 곳을 통과, grep으로 재확인). Ragdoll/ParkourClimb 대사는 UX 31-2 표와 텍스트까지 완전 일치. 다만 `AttackState.cs:48-53`의 데모 텍스트("{N}발 더!"/"타앗!")는 표의 리터럴("한 발 더!"/"오늘은 여기까지")과 다르고, `DemoShotsRemaining`이 상수 1이라 `==0` 분기는 현재 도달 불가능한 죽은 코드 — 31-1 원칙 위반은 아니지만(같은 함수/스냅샷 유지) Phase 3 실제 전투 로직 연결 시 문구를 표와 맞추길 권고. 상세: `docs/BUG_REPORT_PHASE2.md` Minor 1. |
| 텍스트-액션 싱크 회귀 테스트 | Test Engineer | 완료 | **[Test Engineer, 2026-08-27]** 기획서 0번 항목 직결 회귀 테스트를 EditMode 자동화 테스트로 고정. 신규 `Assets/_Project/Scripts/Tests/EditMode/DialogueTextActionSyncTests.cs`(8개 `[Test]`) — (1) 정상 케이스: `ChangeState` 확정 시 같은 전이의 `DialogueIntent.IsValid==true`. (2) 강제 취소(핵심, 2건): 인터럽트/일반 후속 전이 모두 직전 `DialogueIntent`를 즉시 만료시키고 `StickmanEventBus.DialogueExpired`가 정확히 1회, 동일 인스턴스로 발행됨을 확인 — "한 발 더라고 말만 하고 안 쏨" 버그의 구조적 재현 불가를 고정. (3) 위조 방지(2건): 리플렉션으로 `StateTransitionContext`가 public 생성자 0개인 sealed class임을 확인 + `TryConsumeToken()`을 반복 호출하면 최초 1회만 true. (4) 파라미터 스냅샷 무결성(UX 31-1, 2건): `IHasDialogueParams` 가짜 상태로 `Enter()` 이후 원본 파라미터 값을 바꿔도 이미 만든 `DialogueIntent.Text`는 불변임을 확인 + 경계값 양쪽(`shotsRemaining` 1/0)으로 재진입 시 대응 텍스트 한 종류만 나옴을 확인. (5) 동일 컨텍스트 재사용 차단: 같은 `StateTransitionContext`로 두 번째 `DialogueIntent` 생성 시도가 `InvalidOperationException`. **asmdef 구성(실행 검증 완료)**: 프로덕션 코드가 asmdef 없이 기본 `Assembly-CSharp`에 있어 커스텀 asmdef가 이를 직접 참조할 수 없는 Unity 제약이 실측됨 — `Assets/_Project/Scripts/StickMate.Runtime.asmdef`(전 프로덕션 스크립트를 이름 있는 어셈블리로 승격, 씬/프리팹 자산이 아직 없어 GUID 참조 파손 위험 없음 확인 후 진행) + `Assets/_Project/Scripts/Tests/EditMode/StickMate.Tests.EditMode.asmdef`(`StickMate.Runtime`/`UnityEngine.TestRunner`/`UnityEditor.TestRunner` 참조, `nunit.framework.dll` precompiled 참조, `defineConstraints: UNITY_INCLUDE_TESTS`)로 2-asmdef 분리 — Unity가 `com.unity.test-framework`(1.6.0, `Packages/manifest.json`에 신규 추가)의 `UnityEngine.TestRunner`/`UnityEditor.TestRunner`를 `autoReferenced:false`로 배포해(패키지 자체 asmdef 확인) 기본 어셈블리 방식으로는 NUnit API에 접근 불가함을 실측 후 이 구성으로 확정. `internal` 멤버(`StateTransitionContext` 생성자/`TryConsumeToken`) 접근을 위해 `Assets/_Project/Scripts/AssemblyInfo.cs`에 `[assembly: InternalsVisibleTo("StickMate.Tests.EditMode")]` 추가(런타임 동작 변경 없음, 테스트 어셈블리에만 가시성 허용). **실행 결과**: `-runTests -testPlatform EditMode` 2회 실행(1차 + `Library/ScriptAssemblies`/`Bee`/`PlayerDataCache` 삭제 후 클린 재컴파일 1회) 모두 8/8 통과, 결과 xml `result="Passed" failed="0"` 직접 파싱 확인. 클린 재컴파일 배치모드 로그(`-quit`)에서 `error CS`/`warning CS` 0건 — 기존 에러 0/경고 0 기준선이 테스트 코드 추가로 깨지지 않음을 재확인. 프로덕션 코드 버그는 발견되지 않음(모두 스펙대로 동작). |
| 버그 리포트 | Debugger | 완료(핫픽스 재확인) | **[Debugger, 2026-08-27, 핫픽스 재확인]** 커밋 `7d209bd`(`ParkourClimbState.cs` 1개 파일, `Tick()` +10줄) — 수정 제안 1번과 정확히 일치(`pos.y` Lerp 직후 `linearVelocity.y`만 0으로 재확정, `v.x` 미접촉으로 부작용 없음), 배치모드 컴파일 에러 0/경고 0 기준선 유지 확인. **Phase 2 최종 승인.** 상세: `docs/BUG_REPORT_PHASE2.md` "핫픽스 재확인" 섹션. **[Debugger, 2026-08-27]** 커밋 `2ce8c95` 전면 검토 완료. 결론: **Blocker 0 / Major 1(BUG-P2-M1) / Minor 5 — Coder로 반려 필요.** 중점 점검 1(토큰 소비 경로 우회 불가)/2(`ReportExternalImpact` 단일 진입점 가드·재인터럽트 시 `_settleTimer` 리셋 보장)/4(파쿠르 좌표계 일관성·매 프레임 재확인·`AutoWanderController`와 경합 없음)/5(낙하높이 vs 충격량 축 분리, Architect 결정과 구현 정확히 일치 — `FallState`의 `LandingRollRequested`는 순수 이벤트뿐 상태전이 없음 확인)/6의 Ragdoll·ParkourClimb 대사 매핑은 전부 스펙대로 정확함을 코드로 직접 확인. **신규 Major(BUG-P2-M1)**: `ParkourClimbState.Tick()`이 매 프레임 `Body.position.y`만 Lerp로 덮어쓰고 `Body.linearVelocity.y`는 `Enter()` 1회만 0으로 초기화 — 등반 중 중력이 `linearVelocity.y`에 숨어서 계속 누적되다가, 등반 완료 직후 Idle/Walk로 전이되는 순간부터 다음 프레임 `GroundedTick`(`SnapToGround`)이 상쇄하기 전까지 최소 1 FixedUpdate 동안 실제로 적용되어 착지 튐(pop)이 매 등반마다 100% 재현됨(상태 자신의 주석 "중력에 의한 낙하는 발생하지 않는다"와 UX_FLOW.md 4절 "급격한 포즈 점프는 UX 결함" 원칙 둘 다 위반). 수정안: `Tick()`에서 `pos.y` 설정 시 `linearVelocity.y`도 0으로 재확정(파일 1개, 수 줄 규모). Minor 5건(AttackState 데모 텍스트가 UX 31-2 표와 다름/`impulseMagnitude` 공식 2곳 중복/RagdollRig 파츠 0개 시 즉시 Getup 자동전이/ParkourClimbState.Enter()의 벽 판정 재계산/Getup 대사 미구현에 대한 표 각주 권고)과 가설 H7(GETUP P-control 게인, 실제 프리팹 배선 후 실측 필요)은 급하지 않음. 상세: `docs/BUG_REPORT_PHASE2.md`. Unity 배치모드 컴파일 독립 재검증(캐시 재사용 1회 + `Library/ScriptAssemblies`/`Bee`/`PlayerDataCache` 강제 삭제 후 클린 재컴파일 1회) — 두 실행 모두 에러 0/경고 0, 클린 재빌드로 "경고 2건 해소" 주장 재확인 완료. |

## Phase 3 — 전투 / 커서 상호작용
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| UX: 격파 미니게임 플로우/와이어프레임 | UX Designer | 완료 | UX_FLOW.md 10절 — 기 모으기 게이지/스위트스팟/실패 재도전 3회 정의 |
| UX: 라이벌 스틱맨 조우 연출/탈출 규칙 | UX Designer | 완료 | UX_FLOW.md 11절 — 관전 전용 확정, 스폰확률/쿨다운/최대30초 정의 |
| UX: 드래그&던지기 상호작용 규칙 | UX Designer | 완료 | UX_FLOW.md 12절 — 속도 clamp/스무딩, 부분적 클릭관통 해제 요구사항 포함 |
| UX: 로데오 커서 / 인질극 긴급탈출 상세 | UX Designer | 완료 | UX_FLOW.md 13절(로데오)·14절(인질극 4중 안전망)·15절(부분적 클릭관통 해제 통합), 6-5절과 정합 확인 완료 |
| 격파 미니게임(기 모으기+타이밍) | Coder | 완료 | **[Coder, 2026-08-27, Phase 3 구현 완료]** 신규 `States/BattleMinigameState.cs`(`StickmanStateId.BattleMinigame` 추가) — Charging(1.5~2s 랜덤, 재시도마다 재추첨)/Resolving 2단계 내부 상태머신. 클릭 판정 대상은 실제 오브젝트 스프라이트 대신 캐릭터 자신의 히트박스를 재사용(UX 10절 "캐릭터/오브젝트의 화면 히트박스" 중 "캐릭터" 쪽 — 소환 오브젝트 렌더링은 Phase 2+ 담당, WanderAmbientMotionRequested류 "트리거 조건은 지금, 비주얼은 나중" 패턴 재사용). 스위트스팟 70~85%(`StickConfig.battleSweetSpotStart/End`), 실패 시 1.5초 후 자동 재시작, 최대 3회(`battleMaxRetries`) 초과 시 소진 종료, 이벤트 시작 후 5초(`battleInputTimeoutSeconds`) 무클릭이면 자동 취소. Enter() 고정 대사 "좋아, 간다"(10절 원문 그대로) 구현, 성공/실패/소진 결과는 신규 `StickmanEventBus.BattleMinigamePhaseChanged`로만 통지(**알려진 설계 한계**: UX_FLOW.md 31-2 표 #5의 "릴리즈 순간 chargeRatio 기반 대사"는 그 스냅샷 시점이 `Enter()`가 아니라 `Tick()` 도중이라 "DialogueIntent는 Enter() 안에서만" 원칙(9절-1/31-1)과 구조적으로 충돌 — 표 자체도 "지금 구현 대상 아님"이라 명시했으므로 이번 라운드엔 텍스트를 만들지 않고 이벤트만 발행, Architect/UX 조율 요청 아래 교차 레이어 로그 참고). 클릭 입력/자동발동/유휴판정/락 획득은 신규 `Interaction/BattleMinigameDirector.cs`(MonoBehaviour)가 전담 — `Interaction/StickmanClickHitbox.MouseDown`을 구독해 `blackboard.BattleClickSignaled` 펄스로 상태에 전달, `SpectacleEventLock`+`Platform.ILocalClickCaptureService`를 함께 획득한 뒤에만 `ChangeState(BattleMinigame)` 호출, `StateTransitioned(From==BattleMinigame)` 구독으로 두 락을 자동 해제. 트레이 긴급정지(`GlobalEmergencyStopRequested`) 구독해 소유 중이면 즉시 강제 Idle 전이. `TriggerManually()` 공개 메서드로 트레이 메뉴 수동 발동 지점 마련. **[Debugger, 2026-08-27]** 격파 미니게임 self-transition(Architect 지시, 위 교차 레이어 로그) 미반영 확인 — `BattleMinigameState.ResolveOutcome()`이 여전히 `Tick()` 도중 이벤트만 발행하고 자기-전이/`chargeRatio` 대사를 만들지 않는다. 커밋 메시지가 "다음 라운드"로 명시했으므로 버그는 아니나 다음 라운드 최우선 이월 확인. `BattleMinigameDirector`는 BUG-P3-M1(Major, `OnDisable()`에서 SpectacleEventLock/ILocalClickCaptureService 미반환)의 대상 중 하나 — 상세: `docs/BUG_REPORT_PHASE3.md`. **[Coder, 2026-08-27, 2차 반영]** BUG-P3-M1 반영 — `BattleMinigameDirector.OnDisable()`에 `ReleaseOwnedLocks()` 추가(소유 중이면 캐릭터를 Idle로 강제 복귀시킨 뒤 `SpectacleEventLock`+`ILocalClickCaptureService` 둘 다 해제, 두 Release 계열 메서드 모두 소유자 확인 후 no-op이라 중복 호출해도 안전). Architect 지시(self-transition, 위 교차 레이어 로그) 반영 — `BattleMinigameState.TickCharging()`이 판정을 직접 내리지 않고 `TriggerResolution()`으로 `chargeRatio` 스냅샷만 기록한 뒤 `Machine.ChangeState(BattleMinigame, isForcedInterrupt:false)`로 자기 자신에게 재전이시키며, 재실행된 `Enter()`의 신규 `ResolveOutcome()`이 성공/실패/재도전/소진 판정과 `StickmanEventBus.BattleMinigamePhaseChanged` 통지, `chargeRatio` 기반 `DialogueIntent`(≥0.9 "필살기다!"/미만 "어... 어라?", UX 31-2 표 #5 원문)를 함께 만든다 — RagdollState의 반복 피격 self-transition과 동일 컨벤션(`BattleMinigameState`에 `IHasDialogueParams` 구현 추가). **부수 발견 및 수정**: self-transition은 From==To==BattleMinigame인 `StateTransitioned`도 발행하므로, 기존 `BattleMinigameDirector.OnStateTransitioned()`가 이를 "빠져나감"으로 오판해 릴리즈 순간마다 락을 조기 해제하는 신규 결함을 유발할 뻔했음 — `evt.To==BattleMinigame`이면 조기 반환하도록 함께 수정해 예방. Unity 배치모드 클린 재빌드(Library/ScriptAssemblies·Bee·PlayerDataCache 삭제 후 재컴파일 확인) 에러0/경고0 + EditMode 8/8 통과 재확인. |
| 라이벌 스틱맨 AI | Coder | 완료 | **[Coder, 2026-08-27, Phase 3 구현 완료]** 지시대로 플레이어의 `AutoWanderController`를 재사용하지 않고 완전히 별도로 구현. 신규 `Interaction/RivalStickmanAgent.cs`(MonoBehaviour, `[RequireComponent(Rigidbody2D)]`) — 플레이어(`StickmanAgent`)와 **별개의 `StickmanBlackboard`/`StickmanStateMachine` 인스턴스**를 갖되 `FootholdPoller`/`MainCamera`는 참조 공유(발판 재열거 없이 "두 캐릭터가 발판을 공유" 요구사항 충족). `Idle/Walk/Fall/Attack/Ragdoll/Getup` 6종만 등록(점프/파쿠르는 최소 스코프 제외 — `RivalPursuitIntentSource.JumpRequested`가 항상 false). 신규 `Interaction/RivalPursuitIntentSource.cs`(`IMovementIntentSource` 구현, 목표=플레이어 위치, `rivalStopDistance` 이내면 정지)가 유일한 추적 로직. 스폰 확률(3~5%, `rivalSpawnChance`)/쿨다운(20분, `rivalSpawnCooldownSeconds`)/최대지속(30초, `rivalMaxDurationSeconds`)/유효발판 부족 시 이연(`rivalSpawnMinFootholds`)/10절과 상호배제는 신규 `Interaction/RivalEncounterDirector.cs`(MonoBehaviour)가 전담 — `SpectacleEventLock`만 사용(11절 "관전 전용, 부분적 클릭관통 해제 불필요"를 코드로도 강제 — `ILocalClickCaptureService` 참조가 이 기능 전체에 단 한 줄도 없음). 근접 시 전투 교환은 `RivalStickmanAgent`가 "심판" 역할로 직접 판정(무작위 50:50 선타, `rivalAttackCooldownSeconds` 쿨다운) — 라이벌이 맞을 때는 `States.RagdollImpactResolver`(신규 공용 헬퍼, 아래 참고)로 자기 자신에게, 플레이어가 맞을 때는 반드시 `StickmanAgent.ReportExternalImpact()`(공개 메서드, Suspended 가드 유지) 경유. 충격량은 `ragdollForceThreshold * rivalAttackImpactMultiplier`로 계산해 항상 RAGDOLL 전이를 보장. `rivalDuelHitsToLose`(기본 2회) 피격 시 해당 진영 패배, 최대지속 도달 시 무승부 — `StickmanEventBus.RivalDuelStarted/Ended(RivalDuelResult)` 발행. **UX 11절 "전체화면 감지 시 즉시 취소" 준수**: 라이벌은 플레이어 `StickmanStateMachine`에 속하지 않아 `StickmanAgent.Suspend()`의 일반 처리 대상이 아니므로, 신규 공개 게터 `StickmanAgent.IsSuspended`를 매 프레임 직접 폴링해 true면 즉시 무승부 종료(`Draw`)로 구현. `AttackState`도 이 기능의 유일한 실사용처라 이번에 완성(아래 "DialogueIntent" 관련 별도 기재 없이 이 행에서 함께 보고: 파라미터 없는 생성자를 블랙보드 주입형으로 전환, `Tick()`에 `attackDuration` 경과 시 `context.From`으로 기억해둔 진입 직전 상태로 복귀하는 로직을 신규 구현 — 예전엔 Tick()이 완전히 비어 있어 한 번 Attack에 들어가면 영원히 못 나오는 상태였음, `docs/BUG_REPORT_PHASE2.md` Minor 1 지적사항(데모 텍스트가 UX 31-2 표와 다름)도 함께 해소해 대사 리터럴을 "한 발 더!"/"오늘은 여기까지"로 표와 일치시킴). **[Debugger, 2026-08-27]** 별도 StickmanBlackboard/StickmanStateMachine 인스턴스로 완전히 독립됨을 확인 — `DialogueIntent`의 만료 판정이 인스턴스별 `_originMachine.CurrentTransitionGeneration`만 비교해 정적 `StickmanEventBus`를 공유해도 라이벌↔플레이어 대사 상호오염 없음(코드로 직접 검증). `IsSuspended` 폴링도 정확히 플레이어(`_opponent`)를 참조. 다만 (1) `RivalEncounterDirector`도 BUG-P3-M1(Major) 대상 — `OnDisable()`에서 SpectacleEventLock 미반환. (2) Minor 2 — `TickCombatExchange()`가 라이벌 선타일 때만 `TryPlayAttackAnimation()`으로 라이벌 자신의 Attack을 트리거하고, 플레이어가 선타일 때는 플레이어 쪽 `Blackboard.Machine.ChangeState(Attack)`을 전혀 호출하지 않아(grep 확인) 대결이 시각적으로 비대칭("라이벌만 공격한다"). 상세: `docs/BUG_REPORT_PHASE3.md`. **[Coder, 2026-08-27, 2차 반영]** BUG-P3-M1 반영 — `RivalEncounterDirector.OnDisable()`에 `ReleaseOwnedLock()` 추가(소유 중이면 `_rival.ForceEndDuel()`로 대결을 무승부 종료시킨 뒤 `SpectacleEventLock` 해제, 멱등). Minor 1(ShotsRemaining 상시 0) 반영 — 신규 `StickmanBlackboard.AttackShotsRemaining` 필드를 추가해 `AttackState.Enter()`가 하드코딩 0 대신 이 값을 읽게 하고, `RivalStickmanAgent.TryPlayAttackAnimation()`이 `ChangeState(Attack)` 직전에 `rivalDuelHitsToLose - (지금까지 맞은 횟수) - 1`을 계산해 채워 넣는다 — 결정타가 아니면 "한 발 더!", 결정타면 "오늘은 여기까지"로 대결 진행 상황에 맞게 갈린다(31-1 스냅샷 원칙 그대로 유지, 값은 ChangeState 호출 직전 한 번만 기록). Minor 2(플레이어 쪽 Attack 미발동 비대칭) 반영 — 신규 `TryPlayOpponentAttackAnimation()` 추가: 플레이어가 선타를 낼 때(현재 Idle/Walk일 때만, 라이벌 쪽 가드와 동일) `_opponent.Blackboard.Machine.ChangeState(StickmanStateId.Attack)`을 호출해 플레이어도 대칭으로 Attack 모션 상태에 진입시킨다(같은 ShotsRemaining 스냅샷 규칙 적용, `attackDuration` 경과 후 플레이어 AttackState가 스스로 원복하므로 별도 후처리 불필요). Unity 배치모드 클린 재빌드 에러0/경고0 + EditMode 8/8 통과 재확인. |
| 드래그&던지기(커서 물리 상호작용) | Coder | 완료 | **[Coder, 2026-08-27, Phase 3 구현 완료]** 신규 `States/DragThrowState.cs`(`StickmanStateId.Dragged` 추가) — `Enter()`에서 `Rigidbody2D.bodyType`을 Kinematic으로 전환(지시대로 "Kinematic 전환 고려"), `Vector2.SmoothDamp`(`dragFollowSmoothTime`)로 커서를 스프링·댐퍼 관성감 있게 추종. 원형 버퍼(32슬롯, 매 프레임 할당 없음)에 (위치,시각) 표본을 쌓아 놓치는 순간 `dragThrowVelocitySampleWindowSeconds`(0.12초, UX 명시값) 구간 평균 속도를 계산 → `dragThrowMaxSpeed`로 clamp(**"실종 버그" 방지** — 기획서 0번 항목이 지목한 버그 유형과 같은 계열, 상한 없이 던지면 화면 밖으로 사라져 안 돌아오는 신뢰 문제로 직결) → Dynamic 복귀 + 그 속도로 던짐. 던진 속도(질량 곱=충격량)가 `ragdollForceThreshold` 이상이면 즉시 Ragdoll로 자연 전이, 미만이면 평범한 Fall(포물선 낙하) — 신규 공용 헬퍼 `States/RagdollImpactResolver.cs`로 이 판정식을 통일(아래 교차 레이어 로그 참고). 종료 경로 3가지 모두 구현: (1) `DragReleaseSignaled` 펄스(마우스업/트레이긴급정지 공용), (2) `dragThrowMaxHoldSeconds`(10초) 초과, (3) 커서 좌표 조회 실패(화면/모니터 경계 이탈로 간주 → 마지막 유효 위치에서 자유낙하). 진입/해제/락 배선은 신규 `Interaction/DragThrowController.cs`가 전담(`StickmanClickHitbox`+`ILocalClickCaptureService`+`SpectacleEventLock`, 매 프레임 히트박스 영역 갱신으로 15절 제약1 "동적 히트박스 추적" 충족). **알려진 한계**: 창(발판) 충돌 시 국소 충격 파티클/흔들림(12절)은 발판이 가상 판정(Collider2D 없음, Phase 2에서 이미 기록된 한계)이라 이번에 구현하지 못함 — 렌더링 레이어가 붙을 때 발판 사각형과의 거리 기반 근사 판정 추가 설계 필요(코드 주석에 명시). **[Debugger, 2026-08-27]** `Enter()`/`Exit()`의 Kinematic↔Dynamic 페어링이 SpectacleEventLock 해제/`Suspend()`의 강제 Idle 전이/RAGDOLL 강제인터럽트 세 경로 모두에서 `StickmanStateMachine.ChangeState()`의 Exit-먼저-Enter-나중 순서 덕에 항상 정확히 복구됨을 코드로 확인 — 드래그 도중 전체화면 감지/외력 초과 둘 다 안전. Kinematic 바디는 gravityScale 영향을 받지 않아 BUG-P2-M1류 속도 누적 위험도 없음. `DragThrowController`도 BUG-P3-M1(Major) 대상 — 상세: `docs/BUG_REPORT_PHASE3.md`. **[Coder, 2026-08-27, 2차 반영]** BUG-P3-M1 반영 — `DragThrowController.OnDisable()`에 `ReleaseOwnedLocks()` 추가(소유 중이면 캐릭터를 Idle로 강제 복귀시켜 `Exit()`의 Kinematic→Dynamic 방어적 복구를 그대로 태운 뒤 `SpectacleEventLock`+`ILocalClickCaptureService` 둘 다 해제, 멱등). Unity 배치모드 클린 재빌드 에러0/경고0 + EditMode 8/8 통과 재확인. |
| 로데오 커서 | Coder | 완료 | **[Coder, 2026-08-27, Phase 3 구현 완료]** 지시대로 클릭 불필요 — 신규 `States/RodeoCursorState.cs`(`StickmanStateId.RodeoCursor` 추가)는 `ILocalClickCaptureService`를 전혀 참조하지 않는다(13절 "부분적 클릭관통 해제 대상 아님" 명시를 코드 구조로도 강제). 트리거 감시는 신규 `Interaction/RodeoCursorWatcher.cs`가 9절-3 기존 전역 커서 폴링 채널(`StickmanAgent.TryGetCursorPosition`)만 재사용해 담당(신규 폴링 채널 없음) — 커서가 `rodeoStillRadiusPx`(5px) 안에서 `rodeoStillTriggerSeconds`(5초) 이상 정지 + 캐릭터와의 OS화면거리가 `rodeoReachDistancePx` 이내(도달 가능)일 때만 발동. 상태는 Mounting(`rodeoMountDurationSeconds` 동안 Lerp 접근, ParkourClimbState 등반 Lerp와 동일 컨벤션)→Mounted(Kinematic으로 커서 위치 직접 추종) 2단계. **3중 안전망 전부 구현**: (1) 암묵적 — 커서 이동속도가 `rodeoShakeSpeedThresholdWorldPerSec` 이상이면 "거친 흔들기"로 판정, `RagdollImpactResolver`에 `ragdollForceThreshold * rodeoShakeImpactMultiplier`(확정적으로 임계값 초과)를 흘려 반드시 Ragdoll 전이(13절 "낙하→RAGDOLL→GETUP", 실패 아닌 코믹 리액션 톤은 기존 RagdollState 충격강도별 대사가 그대로 담당). (2) 타임아웃 — `rodeoMaxDurationSeconds`(10초) 도달 시 정상 종료(5절 (a) 경로). (3) 트레이 긴급정지 — `RodeoCursorWatcher`가 `GlobalEmergencyStopRequested`를 구독해 소유 중이면 즉시 Idle 강제 전이(Phase 0에서 예약해둔 이벤트 슬롯을 Phase 3에서 처음 실제로 구독/발행하는 사례). **[Debugger, 2026-08-27]** 3중 안전망 전부 실제로 동작함을 확인. `RodeoCursorWatcher`도 BUG-P3-M1(Major) 대상 — `OnDisable()`에서 SpectacleEventLock 미반환. 상세: `docs/BUG_REPORT_PHASE3.md`. **[Coder, 2026-08-27, 2차 반영]** BUG-P3-M1 반영 — `RodeoCursorWatcher.OnDisable()`에 `ReleaseOwnedLock()` 추가(기존 `OnEmergencyStop()`과 동일한 판정을 재사용 — 소유 중이면서 아직 RodeoCursor 상태면 Idle로 강제 복귀시킨 뒤 `SpectacleEventLock` 해제, 멱등). Unity 배치모드 클린 재빌드 에러0/경고0 + EditMode 8/8 통과 재확인. |
| 부분적 클릭관통 해제 (선행 인프라) | Coder | 완료 | **[Coder, 2026-08-27]** UX_FLOW.md 15절 계약 구현 — 상세는 아래 교차 레이어 로그 "부분적 클릭관통 해제 실제 구현 한계" 항목 참고. 요약: OS 레벨 히트테스트는 BUG-B1(진짜 분리 오버레이 미구현)에 가로막혀 아직 불가능하고 그 사실을 `Platform/ILocalClickCaptureService.cs` 문서 상단에 명시적으로 기록, 대신 (a) 단일 소유자 락 + 동적 영역 부기(`Platform/LocalClickCaptureGate.cs`, Win32/Null/Fallback 3개 구현체)와 (b) Unity 게임 오브젝트 레벨 클릭 감지(`Interaction/StickmanClickHitbox.cs`, `OnMouseDown`/`OnMouseUp`)를 완성해 격파 미니게임/드래그&던지기 두 기능이 실제로 동작하게 함. 4개 스펙터클 이벤트(격파/라이벌/드래그/로데오) 간 상호배제는 별도의 `Core/SpectacleEventLock.cs`로 구현(15절-4/16절-10/15 요구사항 통합 충족). **[Debugger, 2026-08-27]** 단일 소유자 락 자체(동시 요청 시 하나만 성공)는 Unity 단일 스레드 실행 특성상 결정론적으로 정확함을 확인. 클릭관통이 실제로는 지금 전역적으로 꺼져 있는 상태(`Win32WindowService.SetClickThrough`가 `NotSupportedException`으로 항상 차단됨, `StickmanAgent.Start()`가 이를 잡아 로그만 남김)임을 코드로 재확인 — "영역 밖 100% 관통" 요구사항이 지금 당장 깨질 위험은 실질적으로 없다는 문서 주장은 사실. 단, 이 락도 소유자(Director)가 `OnDisable()`/`OnDestroy()`될 때 반환되지 않는 BUG-P3-M1(Major)의 대상 — 상세: `docs/BUG_REPORT_PHASE3.md`. **[Coder, 2026-08-27, 2차 반영]** BUG-P3-M1은 이 공용 락 클래스(`SpectacleEventLock`/`LocalClickCaptureGate`) 자체의 결함이 아니라 4개 Director 호출부의 누락이었음을 재확인 — `Release()`/`ReleaseCapture()`가 이미 소유자 확인 후 no-op하는 멱등 가드를 갖고 있어 클래스는 무수정, 4개 Director(격파/라이벌/드래그/로데오) 각각의 `OnDisable()`에 해제 호출을 추가하는 것으로 해결했다(각 행 참고). |
| 투사체(화살/농구공) 라이프사이클 테스트 | Test Engineer | 대기 | "화살이 사라지는 버그" 재발 방지 — Coder Phase 3 구현 완료. 드래그&던지기의 `dragThrowMaxSpeed` clamp(States/DragThrowState.cs)가 이 버그 유형(0번 항목)의 직접 대응책이니 회귀 테스트 설계 시 참고 바람. |
| 텍스트-액션 싱크 회귀 테스트(EditMode 8건) | Test Engineer | 완료 | **[Test Engineer, 2026-08-27]** `Tests/EditMode/DialogueTextActionSyncTests.cs`(8개 `[Test]`) + `StickMate.Runtime.asmdef`/`StickMate.Tests.EditMode.asmdef` 2-asmdef 분리 + `AssemblyInfo.cs`의 `InternalsVisibleTo`. **[Debugger, 2026-08-27]** 8건 전부 리플렉션으로 `sealed class`/생성자 개수 확인, `InvalidOperationException`/이벤트 발행 횟수 검증 등 실제 계약을 검증하며 상시 통과하는 가짜 테스트가 아님을 확인. `InternalsVisibleTo("StickMate.Tests.EditMode")`는 테스트 어셈블리에만 `internal` 가시성을 허용할 뿐 프로덕션 공개 API를 부당하게 넓히지 않음. 클린 재빌드(캐시 삭제 후) 독립 재검증: `error CS`/`warning CS` 0건, 테스트 결과 xml `result="Passed" total="8" passed="8" failed="0"` 직접 파싱 확인 — 8/8 통과 재확인. |
| 버그 리포트 | Debugger | 완료 | **[Debugger, 2026-08-27]** `docs/BUG_REPORT_PHASE3.md` 작성 완료(커밋 `a2ae139` 전면 검토, Unity 배치모드 컴파일 2회 + EditMode 테스트 클린 재빌드 1회 직접 실행) — **Blocker 0건, Major 1건(BUG-P3-M1: 4개 Interaction Director가 `OnDisable()`/`OnDestroy()`에서 SpectacleEventLock/ILocalClickCaptureService를 반환하지 않아 소유자 컴포넌트가 비활성화되면 락이 영구히 풀리지 않음 — 지금 당장 유발 경로는 없으나 UX_FLOW.md가 명시적으로 요청한 향후 설정 토글이 밟을 가능성이 높은 경로), Minor 2건(AttackState.ShotsRemaining 상시 0으로 죽은 코드+라이벌 대결 맥락 어색함, 라이벌 대결 중 플레이어 쪽 Attack 미발동으로 시각적 비대칭)**. 중점 점검 1(격파 미니게임 self-transition, Architect 지시)은 **미반영 확인** — 커밋 메시지가 "다음 라운드"로 명시했으므로 버그 집계에는 포함하지 않되 다음 Coder 라운드 최우선 이월로 명확히 기록. 중점 점검 2~7(SpectacleEventLock 상호배제/LocalClickCaptureGate 단일소유자락/RivalStickmanAgent 독립성/DragThrowState Kinematic 전환/AttackState 완성분/부분적 클릭관통 해제 문서화 정확성)은 BUG-P3-M1을 제외하고 전부 검증 통과. **Coder로 반려 필요** 판정(BUG-P3-M1 수정 전까지 Phase 4 착수 보류 권고). **[Debugger, 2026-08-27, 반려 수정 재확인]** 커밋 `3a7bc22` 좁은 타겟 재검토 — BUG-P3-M1(4곳 락 반환, 멱등 확인)/self-transition 반영(잔여 대사 생성 경로 없음, 이탈 오판 가드 정확)/Minor 2건(ShotsRemaining, 라이벌 대칭) 전부 문제 없음. Unity 배치모드 재실행: 컴파일 에러0/경고0, EditMode 8/8 통과 재확인. **Phase 3 최종 승인 — Phase 4 착수 가능.** 상세: `docs/BUG_REPORT_PHASE3.md` "반려 수정 재확인" 섹션. |

## Phase 4 — OS 장난 / PC 연동
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| UX: 창 도둑/청소부/그라피티/크래시/블랙홀/PC 하드웨어 반응 선행 설계 | UX Designer | 완료 | UX_FLOW.md 27절(6개 기능 각각 목표/트리거/유저가 보는 것/액션/예외상태/탈출구 + "실제로 건드리면 안 되는 것" 명시)·28절(교차 레이어 영향 로그 24~29번). 27-2/27-5(청소부/블랙홀)는 "복제 스프라이트 캡처→오버레이 애니메이션→제거" 공용 파이프라인 제안, 27-4(크래시)는 기획 원문 트리거(키보드 속도/에러 창 감지) 대신 유휴 저확률 추첨으로 대체(원칙 3/26절 키보드 폐기 결정과 충돌 방지). 27-6(PC 하드웨어)은 기존 23절 공통원칙을 신호별 구체 폴링주기로 확장. 27-7에 6개 기능 전체 "절대 실제 OS 상태 변경 금지" 체크리스트 수록 — Coder/Test Engineer 구현·검증 시 필독 |
| 창 끌기 시늉(read-only 검증 필수) | Coder | 완료 | **[Coder, 2026-08-28]** UX 27-1절 — 신규 `States/WindowTheftState.cs`(`StickmanStateId.WindowTheft`) + `Interaction/WindowTheftDirector.cs`. 대상 창은 `FootholdPoller.CachedFootholds`에서 핸들 음수(안전망 합성 발판) 제외 후 폭이 캐릭터 신장(`ClickHitboxRectUtility.ComputeOsRect` OS px 환산)의 `windowTheftMaxTargetWidthMultiplier`(기본 3배) 이하인 것 중 무작위 선정. 유휴 판정(체크주기/확률/쿨다운 `StickConfig` 신규 필드)은 Idle/Walk에서만, `SpectacleEventLock`으로 기존 4개 스펙터클과 상호배제. 매 프레임 대상 핸들이 캐시에서 사라지면(닫힘) 또는 `ScreenRect`가 스냅샷과 달라지면(유저가 실제로 드래그) 즉시 강제 Idle 취소 — 오직 읽기 전용 비교만 하고 `SetWindowPos`/`MoveWindow`류는 프로젝트 전체 0건(Test Engineer의 `UserAssetImmutabilityAuditTests` 정적 스캔으로 교차 검증됨, 아래 참고). "성공" 케이스 없음 — 1/2회차 시도(대사 없음) 후 2회 소진 시 `BattleMinigameState`와 동일한 self-transition 패턴으로 재진입해 확정된 실패 상태에서만 "헥헥... 안 되겠다..." 대사 파생(원칙 1 준수). 전체화면 감지 시 `StickmanAgent.Suspend()`의 강제 Idle 목록에 편입. 실제 팔 IK/파티클 애니메이션은 신규 `StickmanEventBus.WindowTheftOverlayChanged`(Started/Cancelled/Completed + 대상 창 스냅샷 사각형)를 Phase2+ 렌더링이 구독해 담당(트리거 조건은 지금, 비주얼은 나중 — 기존 컨벤션 재사용). |
| 그라피티 낙서 | Coder | 완료 | **[Coder, 2026-08-28]** UX 27-3절 — 신규 `Interaction/GraffitiDirector.cs` + `States/TimedSpectacleState.cs`(`StickmanStateId.Graffiti`, 재사용 가능한 "물리/입력 변경 없는 순수 타이머" 상태, 아래 4개 기능이 함께 씀). 캐릭터 OS 좌표 기준 200~300px(`graffitiMinRadiusPx/MaxRadiusPx`) 반경에서 무작위 각도 후보를 최대 `graffitiCandidateSearchAttempts`회 시도해, 화면(가상 데스크톱 전체 — 모니터별 경계 API 부재는 9절-5 기존 한계 재확인) 안쪽이면서 실제 발판(창) 사각형과 `Rect.Overlaps`로 안 겹치는 정사각형 영역을 선정, 못 찾으면 이번 유휴 주기는 스킵(억지로 창 위에 그리지 않음). 배경화면 파일/설정 API 호출 0건(정적 스캔으로 교차 검증). 도중 그 영역에 새 창이 열려 겹치면 즉시 취소. 3~5초(`graffitiHoldDurationMin/Max`) 유지 후 정상 Idle 복귀 — 실제 스프레이 애니메이션/그림/페이드아웃은 `StickmanEventBus.GraffitiOverlayChanged` 이벤트로 Phase2+ 렌더링에 위임. |
| 블랙홀 / 윈도우 크래시(3초 원복) | Coder | 완료 | **[Coder, 2026-08-28]** UX 27-4/27-5절, 27-2절(청소부)까지 3개 기능을 함께 구현(아래 상세). **블랙홀+청소부(27-2/27-5)**: 신규 `Platform/IDesktopIconLayoutService.cs`(아이콘 영역/좌표 읽기 전용 조회, `ICursorPositionService`와 동일하게 `IPlatformWindowService`에서 분리) + `Interaction/DesktopIconMirrorDirector.cs`(28절-25 권고대로 청소부/블랙홀 공용 컴포넌트 하나, 인스펙터의 `DesktopIconMirrorKind`로 분기) + `TimedSpectacleState` 2인스턴스(`StickmanStateId.DesktopTidy`/`BlackholeSummon`). 파이프라인: 시작 시 아이콘 좌표 1회 캡처 → `DesktopIconMirrorOverlayChanged(Started, 좌표목록)` 발행(재배치 API는 이 파일 어디에도 없음, 좌표는 오버레이가 어디를 덮을지 계산하는 읽기 전용 용도로만 사용) → 진행 중 매 프레임 (a) 9절-3 전역 커서 폴링을 읽기 전용 관찰해 커서가 캡처 영역에 들어오면 클릭 가로채기 없이 스스로 즉시 취소(28절-27 요구사항 그대로, 실제 전역 클릭 상태 API가 프로젝트에 없어 좌표 진입 자체를 활동 근사 신호로 채택 — 실제 클릭보다 이르게만 취소되므로 원칙 2/3 침해 방향 아님, 코드 주석에 근거 명시), (b) 아이콘 영역 재조회 결과가 스냅샷과 다르면(유저가 실제 드래그) 즉시 취소, (c) 새 실제 창이 영역을 덮으면 자연 종료 → 정상 종료 시 `Completed` 발행. 청소부/블랙홀은 동일한 전역 `SpectacleEventLock` 하나만으로 서로도 자동 상호배제(27-2/27-5 요구사항, 별도 락 불필요). `IDesktopIconLayoutService` 구현: `NullPlatformWindowService`는 에디터 테스트용 합성 그리드 반환, `Win32WindowService`는 **정직한 미구현 스텁**(false/빈 목록) — 실제 Windows 아이콘 좌표는 Progman→SysListView32 크로스 프로세스 IPC(VirtualAllocEx/ReadProcessMemory)가 필요해 기존 P/Invoke보다 훨씬 복잡하고 이 환경엔 검증할 Windows 하드웨어가 없어(Unity 배치모드는 macOS 실행) 검증 불가능한 코드를 작성하지 않음(BUG-B1/macOS 미구현과 동일 계열의 정직한 커버리지 공백, 결과적으로 Windows 실빌드에서 청소부/블랙홀은 "트리거만 억제"되는 안전한 no-op) — 후속 작업으로 명확히 이월. **윈도우 크래시(27-4)**: 신규 `Interaction/WindowCrashDirector.cs` + `TimedSpectacleState`(`StickmanStateId.WindowCrash`, 캐릭터 해머 스윙만 담당, `windowCrashSwingDuration` 뒤 자동 Idle 복귀). 기획 원문 트리거(키보드타건속도/에러창 감지)는 UX 설계에서 이미 배제된 대로 구현하지 않고 유휴 저확률(1~3%)+쿨다운(20~30분)만 사용. **크랙 오버레이 자체(3초 수명)는 캐릭터 상태와 의도적으로 분리** — 스윙이 짧게 끝나 캐릭터가 Idle로 복귀한 뒤에도 크랙은 남아 있어야 하므로, `SpectacleEventLock` 해제 시점은 `WindowCrashDirector`의 독립 타이머가 결정(3초 경과/대상 창 소멸/전체화면 감지 시 `Cancelled`, 정상 시 `Completed`). 대상은 신규 폴링 없이 `IsTopmost==true`인 기존 발판 캐시에서 재사용. **크랙 레이어는 `ILocalClickCaptureService`/`StickmanClickHitbox`를 이 파일 어디서도 참조하지 않아 구조적으로 100% 클릭관통**(28절-26 "시각 전용 오버레이는 항상 클릭관통" 단일 규칙) — 대상 창의 실제 입력 수신을 막을 방법 자체가 코드에 없음. |
| CPU/배터리/네트워크 반응 | Coder | 완료 | **[Coder, 2026-08-28]** UX 23/27-6절 — 신규 `Interaction/HardwareReactionDirector.cs` 하나가 4개 신호를 각자 다른 타이머로 관리하는 공용 스케줄러(28절-28, `FootholdPoller`와 동일 정신 — 신호별 독립 `Update()` 없음). 전부 `UnityEngine.SystemInfo`/`Application`의 읽기 전용 조회만 사용, 시스템 제어(쓰기) API 0건(정적 스캔 대상). 배터리(`SystemInfo.batteryLevel`, 60~120초 폴링, 연속 2회 확인 후 확정)/네트워크(`Application.internetReachability`, 15~30초, 연속 2회 확인)/충전(`SystemInfo.batteryStatus==Charging`, 30초 폴링 — **Unity에 크로스플랫폼 충전 이벤트 콜백이 없어** 27-6 표가 허용한 폴백 폴링을 항상 사용, 코드 주석에 근거 명시)/CPU(5~10초 샘플 평균 `Time.deltaTime` → 30~60초 누적 초과 시 확정, **알려진 한계**: Unity에는 프로세스별 실제 CPU% API가 없어 이 앱 자신의 프레임타임 저하를 시스템 부하의 매우 거친 근사치로 사용함을 `StickConfig`/코드 주석에 명시). "회복 확인 전 재알림 금지" + 5~10분 재알림 쿨다운을 신호별로 적용(`Sustained`/`Notified`/`RecoveryCooldownRemaining` 상태 머신), 동시 충족 시 배터리>CPU>네트워크>충전 우선순위로 하나만 `HardwareReactionChanged(Active=true)` 표현(이미 표시 중인 반응은 강제로 끊지 않고 회복될 때까지 유지 — "산만함 방지"를 "표현 전환 시 깜빡임 방지"로 해석, 판단 근거 주석 명시). `SpectacleEventLock`은 의도적으로 쓰지 않음 — UX 28절-29가 상호배제 세트를 27-1~27-5로만 명시했고 하드웨어 반응은 23절의 별도 경량 규율(idle 자세 변형)을 따르는 것이 UX 원문에 더 부합한다고 판단(교차 레이어 로그에 판단 근거 기록). 전체화면 Suspended 중에는 폴링 자체를 스킵. 실제 은유 연출(비틀거림/부채질/두리번거림 등)은 `HardwareReactionChanged` 이벤트로 Phase2+ 렌더링에 위임 — 배터리/CPU % 숫자는 이벤트에 싣지 않음(23절 "은유만 담당"). |
| 실제 파일/창 미변경 감사 테스트 | Test Engineer | 완료(정적 감사) | **[Test Engineer, 2026-08-27]** `Tests/EditMode/UserAssetImmutabilityAuditTests.cs`(5개 `[Test]`) 신규 추가 — Coder의 Phase 4 구현(창 도둑/청소부/그라피티/크래시/블랙홀)을 기다리지 않고 지금 작성 가능한 **소스코드 텍스트 정적 스캔** 방식(리플렉션 아님, 파일 시스템에서 `Assets/_Project/Scripts/` 전체 `.cs`를 grep하듯 검사)으로 구현해 Coder가 나중에 파일을 추가/변경해도 파일명 하드코딩 없이 자동으로 커버됨. (1) 금지 API 블랙리스트 스캔 — `File.Delete(`/`File.Move(`/`Directory.Delete(`/`SetWindowPos(`/`MoveWindow(`/`DestroyWindow(`/`CloseWindow(`/`.Kill(` 8종 핵심 + `TerminateProcess(`/`LVM_SETITEMPOSITION`/`SPI_SETDESKWALLPAPER` 3종 보강(27-2/27-3/27-5가 명시적으로 경고한 API, 현재 0건). 화이트리스트는 `Win32WindowService.cs`의 자기 오버레이 Z-order 조정(`SetWindowPos` + `SWP_NOMOVE`+`SWP_NOSIZE`+`_overlayHwnd` 동시 존재를 라인 단위로 재검증, 다른 위반이 같은 파일에 추가돼도 함께 숨겨지지 않음) 1건뿐 — 예외 근거를 테스트 코드 주석에 명시. (2) `IPlatformWindowService` 리플렉션 — 메서드명에 Move/Resize/Close/Destroy/Delete/Minimize/Kill/Terminate/Activate/Focus 포함 여부 확인(현재 0건), `PlatformFoothold`의 모든 공개 필드가 `readonly`인지 확인. (3) 스캔 자체의 유효성 가드(파일 40개 이상 발견, 알려진 파일 존재, Tests 폴더 자기 제외 확인)와 화이트리스트가 죽은 코드로 방치되지 않는지 확인하는 메타 테스트 포함. **네거티브 체크로 실제 검증력 확인**: `File.Delete("test.txt")`를 호출하는 임시 스텁 파일을 `Scripts/`에 추가해 재실행 → 정확히 그 위반만 파일:라인:사유로 잡아내며 13건 중 1건 Fail 확인(가짜 통과 테스트가 아님을 실증), 임시 파일 제거 후 재실행해 13/13 재통과 확인. Unity 배치모드 실행 결과 컴파일 에러0/경고0, `testResults.xml` 직접 파싱 확인 `total="13" passed="13" failed="0"`(기존 8건 + 신규 5건). **주의(다음 라운드 인수인계)**: 이 테스트는 정적 텍스트 스캔이라 "크랙 레이어가 실제로 3초 내내 클릭관통되는지", "복제 스프라이트 오버레이 아래 실제 아이콘 더블클릭이 정상 실행되는지" 같은 **런타임 동작 검증은 범위 밖** — Coder가 Phase 4 기능을 실제로 구현하면 EditMode만으로는 부족하고 PlayMode/수동 QA로 27-7절 "구현/검증 시 확인 포인트" 열을 별도 재검증해야 함. |
| 버그 리포트 | Debugger | 완료 | **[Debugger, 2026-08-28]** `docs/BUG_REPORT_PHASE4.md` 작성 완료(커밋 `577a7eb` 전면 검토, Unity 배치모드 클린 재빌드 1회 + EditMode 테스트 직접 실행) — **Blocker 0건, Major 1건(BUG-P4-M1, 신규), Minor 2건.** 최우선 점검(유저 자산 불변): `UserAssetImmutabilityAuditTests.CollectScannedSourceFiles()`가 하드코딩된 파일 목록이 아니라 `Directory.GetFiles(..., SearchOption.AllDirectories)`로 `Scripts/` 전체를 스캔함을 코드로 확인해 Phase 4 8개 신규 파일 전부 자동 커버됨을 재확인, Phase 4 파일 전체에 리플렉션 기반 API 조립 호출(`GetMethod`/`.Invoke(`/`DllImport`) 0건(grep), `DesktopIconMirrorDirector.MonitorActive()`가 매 프레임(스로틀 없음) 커서 좌표를 관찰해 자진 취소하므로 27-7 "즉시 자진 취소" 판정 지연 위험 없음(설령 지연돼도 실제 클릭 판정은 항상 실제 좌표 기준이라 원칙 침해로 이어지지 않음). **신규 Major(BUG-P4-M1)**: `HardwareReactionDirector`의 배터리/충전/네트워크 3개 신호가 `UpdateSignalLifecycle(...)` 호출 시 회복-쿨다운 감소량으로 그 폴링 주기 전체(수십 초~분)가 아니라 그 프레임 하나의 `Time.deltaTime`만 넘겨(`:87,100,138`), 기본 7분 쿨다운이 실제로 소진되려면 배터리 약 26일/충전 약 8.7일/네트워크 약 5.8일이 걸림 — 사실상 세션 내내 최초 1회만 알림되고 이후 영구 침묵. `TickCpu`만 실제 경과시간(`elapsedThisSample`)을 올바르게 넘겨 정상 동작. 정적 스캔·기존 테스트로는 검출 불가능한 은닉 버그. WindowTheftState self-transition(중점 점검 5, Phase 3 함정 재발 여부): `WindowTheftDirector.OnStateTransitioned()`가 `evt.To==WindowTheft` 가드를 처음부터 정확히 갖추고 있어 재발하지 않음 확인. IDesktopIconLayoutService Win32 스텁(중점 점검 6): 예외/무한대기 없이 `false`/빈 목록 반환 + 상위 Director가 조용히 스킵함을 확인. 윈도우 크래시 100% 클릭관통(중점 점검 3): `ILocalClickCaptureService`/`StickmanClickHitbox` 문자열이 `WindowCrashDirector.cs`에 등장하는 유일한 자리가 "참조하지 않는다"는 문서 주석 자체임을 확인(실제 참조 0건), 스윙/크랙 수명 분리도 Architect 승인 해석과 코드가 정확히 일치. Minor 2건은 급하지 않음(Minor 1: 하드웨어 반응 우선순위가 "동시 신규 충족" 시에만 적용되고 이미 표시 중인 낮은 우선순위를 선점하지 않는 해석 — Architect 확인 대기 중인 기존 사안, Minor 2: CPU 프레임타임 근사치가 향후 Phase2+ 무거운 스펙터클 렌더링과 동시에 돌 때 오탐할 잠재 위험, 지금은 렌더링 미구현이라 재현 불가). **Coder로 반려 필요** 판정(BUG-P4-M1 수정 전까지 Phase 5 착수 보류 권고). 상세: `docs/BUG_REPORT_PHASE4.md`. **[Debugger, 2026-08-28, 핫픽스 재확인]** 커밋 `10aef27` 좁은 타겟 재검토 — `TickBattery`/`TickCharging`/`TickNetwork` 3곳 모두 `TickCpu`의 `elapsedThisSample`과 동일한 스냅샷 패턴으로 통일됨을 확인, 연속-확인/우선순위 로직 부작용 없음. Unity 배치모드 재실행: 컴파일 에러0/경고0, EditMode 13/13 재확인. **Phase 4 최종 승인 — Phase 5(생산성/반항·스트레스/육성) 착수 가능.** 상세: `docs/BUG_REPORT_PHASE4.md` "핫픽스 재확인" 섹션. |

## Phase 5 — 생산성 / 반항·스트레스 / 육성
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| UX: 투두 말풍선 / 포모도로 감시자 플로우 | UX Designer | 완료 | UX_FLOW.md 17절(들고다니기/포스트잇, 포스트잇은 별도 위젯 창 권고)·18절(딴짓감지는 절대치 마우스 움직임 대신 포커스 전환 빈도+극단값 조합, 연속주기 누적 판정, 3단계 에스컬레이션) |
| 투두 말풍선 / 포모도로 감시자 | Coder | 완료 | **[Coder, 2026-08-28]** UX 17/18절 구현 완료. **투두(17절)**: 신규 `Core/TodoListModel.cs`(정적, SpectacleEventLock과 동일한 이유의 정적 클래스) — 활성/완료함 분리 보관, `ToggleComplete`(체크 왕복 허용으로 "3초 되돌리기" 충족), `SweepCompleted(todoCompletedLingerSeconds)`로 유예 후 완료함 이동, `Add`가 소프트캡(`todoActiveCountSoftCap`=15) 초과 여부만 반환하고 추가 자체는 막지 않음(17절 "강제 차단 지양"). **들고 다니는 모드**는 신규 `Interaction/TodoReminderDirector.cs`(WindowTheftDirector와 동일한 유휴 판정+SpectacleEventLock 패턴)가 유휴 판정마다 1개(FIFO 우선순위)만 골라 `StickmanStateId.TodoReminder`로 전이시킨다 — 이 상태는 신규 상태 클래스 없이 `States/TimedSpectacleState.cs`를 재사용(아래 일반화 참고). **포스트잇 모드**는 신규 `Interaction/TodoPostItWidget.cs` — UX 17절이 명시한 "15절 부분적 클릭관통 해제 락과 절대 공유하지 말 것" 요구사항을 코드 구조로 강제: `Core.SpectacleEventLock`/`Platform.ILocalClickCaptureService`/`Interaction.StickmanClickHitbox`를 이 파일 어디서도 참조하지 않음(주석에 grep 검증 가능 명시, 실제 재확인 완료 — 참조 0건). 체크박스는 uGUI `Button`(자체 `GraphicRaycaster`+`EventSystem`)로만 판정, 씬에 `EventSystem`이 없으면 자체 생성. Canvas/패널/행을 전부 런타임에 코드로 구성해(씬 프리팹 수동 배선 없이도 동작) 빈 상태 숨김/`[+N개 더보기]`/`[숨기기]`(세션 한정)까지 구현. **패키지 의존성 추가**: `UnityEngine.UI`/`UnityEngine.EventSystems`가 프로젝트에 아직 없어(`Packages/manifest.json`에 `com.unity.ugui` 부재 확인) `com.unity.ugui: 2.0.0`을 추가(공식 Unity 패키지, 유저 자산 불변 원칙과 무관한 프로젝트 자체 의존성 추가). **포모도로(18절)**: 신규 `Interaction/FocusWatchDirector.cs` — 딴짓 감지 1차 신호(포커스 전환 빈도)는 신규 폴링을 만들지 않고 `StickmanBlackboard.FootholdPoller.CachedFootholds`의 `IsTopmost`(이미 `Win32WindowService.OnEnumWindow()`가 `GetForegroundWindow()`로 계산해두던 값)를 `StickmanEventBus.FootholdsChanged` 구독으로 관찰해 얻는다(25절-16 "기존 전경 창 감지 인프라 재사용" 요구사항 충족 — 신규 OS 폴링 0건, 판단 근거는 아래 교차 레이어 로그 참고). 2차 신호(마우스 무입력/정처없는 이동)는 기존 9절-3 `StickmanAgent.TryGetCursorPosition` 채널을 세션 활성 중에만 읽어 보조 판정. 2분 관찰창 누적(연속 3주기→1단계 Glance, +2주기→2단계 Nudge, +2주기→3단계 WindowTap), 즉시 리셋 규칙, 시작 2분 유예, 전체화면 Suspended 중 일시정지 전부 구현. 타이머 시작/정상종료/중도취소/2단계 대사는 `TimedSpectacleState` 재사용 등록(`FocusStart`/`FocusComplete`/`FocusCancelled`/`FocusNudge`), 3단계(창 두드림+흔들림)는 상태 전이 없는 순수 앰비언트 이벤트(`FocusWatchTierChanged`)로만 발행(Phase2+ 렌더링 담당). 민감도(관대/보통/예민)·순수 타이머 전용 토글은 공개 프로퍼티로 준비(설정창 미구현으로 소비자 없음). **공통 일반화**: `States/TimedSpectacleState.cs`에 선택적 4번째 생성자 인자(`dialogueTextSelector`)를 추가해 "순수 타이머 + 고정 대사 1회" 패턴을 가진 신규 상태(TodoReminder/FocusStart/FocusComplete/FocusCancelled/FocusNudge/Sulky, 아래 행 참고) 전부가 새 상태 클래스 없이 이 하나의 클래스로 등록됨(기존 4개 Phase4 등록은 4번째 인자 생략으로 무수정 하위호환, `null`이면 대사를 만들지 않음). Unity 배치모드 컴파일 에러0/경고0, EditMode 13/13 통과 확인. **[Debugger, 2026-08-28]** TodoPostItWidget의 SpectacleEventLock/ILocalClickCaptureService/StickmanClickHitbox 참조 0건, EventSystem 중복생성 방지(`EventSystem.current != null` 가드, 프로젝트 전체에서 EventSystem 생성 코드는 이 한 곳뿐) 재확인 — 문제 없음. FocusWatchDirector의 FootholdsChanged 재사용도 `FootholdPoller.HasChanged()`가 `IsTopmost`까지 항목별 비교함을 코드로 확인해 "포커스 전환" 근사로 타당함(창 목록 불변인 채 포그라운드만 바뀌어도 이벤트 발행됨), 매 프레임 폴링 규율 위반 없음 — 문제 없음. 단 `FocusWatchDirector.OnEmergencyStop()`이 다른 8개 Director와 달리 SpectacleEventLock 소유권 가드 없이 무조건 실행되어, 무관한 긴급정지(예: 로데오 취소)에도 진행 중이던 Pomodoro 세션이 함께 취소되는 부작용 발견(Minor 2, Architect 확인 요청) — 상세: `docs/BUG_REPORT_PHASE5.md`. **[Coder, 2026-08-28, Minor 2 반영]** 구독 자체를 제거하는 안은 채택하지 않음 — 18절이 "탈출구: 타이머 링 클릭 또는 트레이 메뉴로 언제든 즉시 종료. 트레이 긴급정지도 항상 유효"라고 명시적으로 요구해, 트레이 긴급정지가 Pomodoro 자체의 정당한 종료 경로이기도 하다는 문서 계약을 지우면 안 된다고 판단(3단계 "창 두드림" 에스컬레이션 전용으로만 좁히는 대안도 검토했으나, 그러면 흔한 경우인 "다른 이벤트 없이 Pomodoro만 조용히 실행 중일 때"조차 긴급정지로 못 끄게 되어 18절 탈출구 요구를 오히려 더 크게 축소시킴). 대신 6-5절이 긴급정지를 "인질극/로데오/창 점령 등 악동·반항 계열 방해성 이벤트"를 끄는 안전판으로 정의하고 있다는 점에 근거해, `OnEmergencyStop()`에 `if (SpectacleEventLock.IsActive && SpectacleEventLock.CurrentOwner != (object)this) return;` 가드를 추가 — 다른 방해성 이벤트가 현재 SpectacleEventLock을 쥐고 있을 때만(=그 긴급정지가 그 이벤트를 겨냥한 것이 명백할 때만) 무시하고, 락이 비어있거나 Pomodoro 자신의 포즈 상태가 락을 쥔 경우(다른 대상이 없는 경우)는 기존대로 즉시 세션을 종료한다 — 로데오 등 무관한 이벤트를 끌 때 Pomodoro가 함께 날아가는 부작용만 제거하고 18절의 "항상 유효한 탈출구"는 그대로 보존. |
| UX: 스트레스 게이지 / 가출 / 인질극 구분 | UX Designer | 완료 | UX_FLOW.md 19절(게이지 3단 노출·예고신호는 현재형만)·20절(가출 탐색+간식+자동복귀 타임아웃)·24절(가출=2단계 반항 vs 인질극=1단계, 표로 구분 확정) |
| 스트레스 게이지 / 가출 | Coder | 완료 | **[Coder, 2026-08-28]** UX 19/20/24절 구현 완료. **스트레스 게이지(19절)**: 신규 `Core/StressGauge.cs`(정적, 값 0~1 보관 + `StickmanEventBus.StressLevelChanged` 발행만 담당 — 지시대로 3단 노출 UI는 만들지 않고 이벤트 훅까지만). 트리거 판정은 신규 `Interaction/StressGaugeDirector.cs`: (1) 격파훈련 과다 — `StateTransitioned`로 `BattleMinigame`(재도전 self-transition 제외)/`Dragged` 진입을 관찰해 5분 창에 8회 초과 시 증가, (2) 장시간 방치 — 클릭/투두변경/긴급정지를 "상호작용"으로 정의(유휴 자동발동 스펙터클은 의도적으로 제외, 근거는 코드 주석+아래 로그)해 12시간 무상호작용부터 시간당 증가, (3) 긴급정지 반복 사용 — 10분 창에 3회 초과 시 아주 약한 가중치(19절 "긴급정지 사용을 주저하게 하면 안 됨" 준수), (4) 최근 상호작용이 있을 때만 시간당 자연 감소(단조증가 방지, Coder 판단 — 아래 로그). **SULKY**는 임계값(80%) 이상일 때 저확률 추첨으로 `TimedSpectacleState` 재사용 등록("아 몰라..." 고정 대사, 예고 아닌 현재형). **가출(20/24절)**: 신규 `States/RunawayState.cs`(전용 상태 — Fleeing→Hidden→Found→Reconciling/SelfReturning 5페이즈, self-transition 패턴으로 페이즈별 대사 "나 안 해!"/"흥... 그럼 한 입만이다"/"심심해서 왔어..."/"어... 알았어, 갈게" 확정 파생) + 신규 `Interaction/RunawayDirector.cs`(트리거/신호 배선). 스트레스가 `stressRunawayThreshold`(기본 1.0) 도달 시 확률이 아니라 확정 발동(24절). 은신처는 화면 네 모서리(`ScreenCoordinateConverter` 왕복 변환으로 월드좌표 계산) 중 무작위 — 은신 중 캐릭터는 **`Rigidbody2D.simulated=false`가 아니라 `Kinematic`**으로 전환(판단 근거: `simulated=false`는 Physics2D 쿼리 대상에서 콜라이더가 제외될 위험이 있어 "클릭으로 찾기"의 `StickmanClickHitbox.OnMouseDown`이 죽을 수 있음 — DragThrowState와 동일한 안전한 전례로 Kinematic 채택) + 렌더러만 숨김(신규 `StickmanBlackboard.SetCharacterVisible` 델리게이트가 `StickmanAgent`의 기존 private `SetRenderersEnabled`를 그대로 노출, 새 메서드 없음). 발견은 신규 채널 없이 기존 `StickmanClickHitbox.MouseDown`을 세 번째 구독자로 재사용. 간식 주기(`OfferSnack()`)는 스트레스를 `runawaySnackStressRelief`(0.5)만큼만 감소(완전 리셋 아님, 20절 명시)시키고 화해 대사로 이어짐. **자동 복귀 타임아웃 구현**(20절 필수 요구사항) — Hidden+Found 통틀어 `runawayAutoReturnSeconds`(기본 1.5시간) 절대 상한, 트레이 `RecallManually()`(수동 소환)도 동일 자진복귀 경로. **긴급정지 = 강제 소환(24절)**: `RunawayDirector`가 `GlobalEmergencyStopRequested`를 구독해 현재 상태가 Runaway일 때만 `RunawayForceSummonSignaled` 펄스를 세팅 — 같은 전역 이벤트를 재사용하되 라벨/의미는 구독자가 상태별로 다르게 해석(다른 상태들의 "종료" 핸들러는 무수정). 14절 인질극이 아직 미구현이라 "사과 먹이기 패턴 재사용"은 문자 그대로의 코드 재사용이 아니라 톤/UI 패턴(앱 소유 버튼, Phase2+ 렌더링 위임)만 차용했음을 명시(아래 로그). 전체화면 Suspended 목록에 Runaway를 넣지 않음(20절 예외 — "화면에 안 보이는 상태이므로 취소 불필요", Suspended가 Tick 자체를 건너뛰어 자동으로 함께 정지/재개됨). Unity 배치모드 컴파일 에러0/경고0, EditMode 13/13 통과 확인. **던전 파밍/세포분열 행은 건드리지 않음(보류 P3 그대로 유지, 리더 결정 존중).** | **[Debugger, 2026-08-28, Major 2건 발견 — Coder로 반려 필요]** `docs/BUG_REPORT_PHASE5.md` 작성 완료(커밋 `3a22ff2` 전면 검토, Unity 배치모드 클린 재빌드 1회 + EditMode 테스트 직접 재실행 — 에러0/경고0, 13/13 통과 재확인). Kinematic 채택 근거(점검 1)는 Unity 공식 문서 기준 정적 검증 가능하며 정확함을 확인(버그 아님). 자동복귀 타이머는 Suspended 중 Tick과 함께 정확히 정지/재개됨을 확인했으나(점검 2), 그 조사 과정에서 **BUG-P5-M1(Major, 신규)**을 발견 — `StickmanAgent.Resume()`(`Core/StickmanAgent.cs:293-302`)이 무조건 `SetRenderersEnabled(true)`를 호출해, 가출 Hidden 페이즈 중(최대 1.5시간) 전체화면 감지 Suspend/Resume 왕복이 한 번이라도 발생하면 아직 발견되지 않은 캐릭터가 강제로 다시 노출된다 — `RunawayState`가 도입한 독립 가시성 상태(`SetCharacterVisible`)를 `Suspend()/Resume()`이 전혀 모르는 것이 근본 원인. 긴급정지 다중 구독자(점검 3)는 8/9개 Director가 소유권 가드로 정확히 격리됨을 확인, `FocusWatchDirector`만 예외(위 포모도로 행 Minor 2). 스트레스 4트리거(점검 4) 논리는 상충 없음, 다만 과다사용 트리거의 반복 가산 방식은 Minor 1로 기록. **BUG-P5-M2(Major, 신규)** — Tasklist.md 이 행이 "UX 19/20/24절 구현 완료"라고 명시했으나, 24절 1단계 요구사항("로데오 발동 확률에 스트레스 가중치 연결", 25절-21 line 856 근거)은 `Interaction/RodeoCursorWatcher.cs`(이번 커밋 미수정, `StressGauge` 참조 0건 확인)에 전혀 반영되지 않았고 이 갭이 알려진 한계로도 문서화되어 있지 않음. 두 항목 모두 크래시/데이터손상은 아니고 기존 안전망(자동복귀 타임아웃/긴급정지/실제클릭)으로 결국 회복되나, 20절 핵심 계약 위반(M1)과 미구현 요구사항의 오보고(M2)라 다음 라운드 착수 전 처리 권고. 상세 수정 제안: `docs/BUG_REPORT_PHASE5.md`. **[Coder, 2026-08-28, 반려 재작업 반영]** BUG-P5-M1/M2 모두 수정 완료. **BUG-P5-M1**: 수정 제안 (b) 채택(IStickmanState에 신규 훅을 추가하는 (a)안은 인터페이스를 구현하는 다른 10여 개 상태 전부가 영향 범위에 들어와 더 침습적이라 판단) — `StickmanBlackboard`에 `IsCharacterHiddenByRunaway` 플래그 신설. `RunawayState.HideCharacterAtHideSpot()`이 Hidden 진입 시 true로, `ShowCharacterRevealed()`(Found)/`RestoreCharacter()`(Reconciling/SelfReturning)/`Exit()`(방어적 복구)/`Enter()`(신규 Fleeing 진입, 이전 사이클 잔류 방지)가 false로 되돌린다. `Core/StickmanAgent.Resume()`은 이 플래그가 true인 동안 `SetRenderersEnabled(true)` 호출을 건너뛴다 — Hidden 페이즈 중 전체화면 Suspend/Resume이 왕복해도 은신 렌더러 상태가 더 이상 덮어써지지 않음. **BUG-P5-M2**: `StickConfig`에 `stressRodeoWeightThreshold`(기본 0.6)/`rodeoStressTriggerSecondsMultiplier`(기본 0.7) 신규 필드 추가, `Interaction/RodeoCursorWatcher.cs`에 `GetEffectiveStillTriggerSeconds()`를 신설해 `StressGauge.CurrentLevel`이 임계값 이상이면 `rodeoStillTriggerSeconds`에 배율(0.7)을 곱해 정지 판정 시간을 완만히 단축 — 발동 조건 자체(정지 시간 도달)는 그대로 두고 그 시간만 스트레스에 따라 짧아지는 "약한 가중치"로 24절 1단계 요구사항 충족(과하게 공격적인 확률 승수 대신 완만한 시간 단축을 선택한 근거는 코드 주석 참고). Unity 배치모드 컴파일 에러0/경고0 + EditMode 13/13 재확인(아래 이번 라운드 결과 참고). **[Architect 결정, 2026-08-28, Minor 1 — 코드 변경 불필요, 설계 의도로 확정]** `StressGaugeDirector.RecordOveruseEntry()`/`RecordEmergencyStopEntry()`가 관찰 창 안에서 임계값을 초과한 뒤에도 매 후속 이벤트마다 계속 `stressOveruseIncrement`/`stressEmergencyStopIncrement`를 반복 가산하는 것(1회성 트리거로 리셋되지 않음)은 **의도된 에스컬레이션**으로 승인한다 — "과하게 다룰수록 그만큼 더 빨리 화난다"는 것이 19절 취지에 더 부합하며, 1회성으로 바꾸면 짧은 시간에 격파/드래그를 몰아쳐도(또는 긴급정지를 몰아 눌러도) 스트레스가 딱 한 번만 오르고 끝나 "과다 사용" 트리거의 체감 강도가 오히려 약해진다. 코드는 그대로 유지, 다른 "알려진 한계" 항목과 동일한 수준으로 이 설계 의도만 여기에 명시적으로 기록한다(`docs/BUG_REPORT_PHASE5.md` Minor 1 대응). **[Debugger, 2026-08-28, 핫픽스 재확인]** 커밋 `8d45ab0` 좁은 타겟 재검토 — `IsCharacterHiddenByRunaway` 플래그가 `RunawayState`의 Hidden 진입/Found·Reconciling·SelfReturning·Exit 전 경로에서 정확히 관리되고(`ChangeState()`가 self-transition 포함 매 전이마다 `Exit()`을 무조건 호출해 영구 고착 경로 없음 확인), `Resume()`이 이를 정확히 확인함을 확인. `RodeoCursorWatcher`의 스트레스 가중치가 임계값 이상일 때만 정지판정시간을 완만히(최대 30%) 단축함을 확인(과하지 않음). `FocusWatchDirector.OnEmergencyStop()`의 `SpectacleEventLock.IsActive && CurrentOwner != this` 가드가 락이 비어있거나 Pomodoro 자신이 소유한 경우엔 즉시 종료, 다른 이벤트 소유 중일 때만 무시함을 의도대로 정확히 동작함을 확인. Unity 배치모드 재실행: 컴파일 에러0/경고0, EditMode 13/13 재확인. **Phase 5 최종 승인 — Phase 6(성능점검/최종리뷰/문서화) 착수 가능.** 상세: `docs/BUG_REPORT_PHASE5.md` "핫픽스 재확인" 섹션. |
| UX: 던전 파밍 / 세포분열·군대 플로우 | UX Designer | 완료 | UX_FLOW.md 21절(던전 오버레이는 14절과 동일한 클릭관통 역예외, 원본 창 조작 100% 유지 확인)·22절(개체 태그만 제공, 개별지휘 스코프 제외 권고, 개체수 상한+도감 전환) |
| 던전 파밍 / 세포분열 (스코프 논의 필요) | Coder | 보류(P3) | **[Architect, 2026-08-28]** 의도적 보류 확정. ARCHITECTURE.md 1절 우선순위표에서 이미 P3(최저)로 분류됨. 던전 파밍은 몬스터/루프 콘텐츠 파이프라인이 추가로 필요해 순수 메커니즘 이상의 콘텐츠 제작 부담이 큼. 세포분열은 RivalStickmanAgent(Phase3)가 이미 "독립된 StickmanBlackboard/StateMachine 인스턴스를 여러 개 동시 운용" 패턴을 증명했으므로 기술적 난이도는 낮으나, 이번 Phase 5는 UX 17~20절(생산성/스트레스·반항)에 집중하고 이 둘은 별도 백로그 항목으로 유지. UX 설계(21·22절)는 이미 완료되어 있어 언제든 재개 가능.

## Phase 6 — 마감
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| 성능 점검(Idle CPU, 할당, 폴링주기) | Performance Engineer | 완료 | **[Perf Eng, 2026-08-28]** `docs/PERFORMANCE_REPORT.md` 작성 완료. 전수 grep 감사(Update/FixedUpdate 전체 목록화, `EnumerateFootholds` 전 호출부 재검증, LINQ/문자열연결/GetComponent/Camera.main/`new` 할당 패턴) + 의심 지점 소스 직독. **Coder 반영 필요 등급 실질적 문제 0건.** (1) `EnumerateFootholds()` 실호출부는 `FootholdPoller.cs` 한 곳뿐(우회 직접호출 없음, 0.5초 주기 게이트), `IsFullscreenAppActive()`/`EnumerateIconRects()`도 동일하게 주기 게이트 준수. (2) Phase 3~5 신설 15개 Director 전부 자체 누적 타이머로 저빈도 폴링, 매 프레임 작업은 float 감산/enum 비교 수준이라 개수(13개+) 합산해도 24시간 상주 CPU 부담 무의미. (3) `DialogueIntent` 생성은 전 호출부(`AttackState`/`BattleMinigameState`/`RagdollState`/`ParkourClimbState`/`RunawayState`/`WindowTheftState`/`TimedSpectacleState`)가 예외 없이 `Enter()` 안에서만 이루어짐(설계 보장 실제 준수 확인) — `Tick()` 안 생성 0건. LINQ는 테스트 코드에만 존재, 프로덕션 0건. (4) `Camera.main`/`GetComponentsInChildren`는 `Awake()` 1회 캐싱, `GetComponent`는 null-가드 지연 캐싱 패턴으로 전부 사실상 1회 실행. (5) `SpectacleEventLock`은 O(1) 정적 락(구독자 수 무관), `StateTransitioned` 구독자 11개는 상태 전이 확정 시점에만 발행되는 저빈도 이벤트라 팬아웃 비용 무의미 — Phase 0 Minor m4 재평가 결론: 문제없음. **참고 사항(조치 불필요, 성능 범위 밖)**: Rigidbody2D 속도/위치 설정이 `FixedUpdate()` 없이 `Tick()`(→`Update()`) 경로에서만 이루어짐(프로젝트 전체 FixedUpdate 0건) — GC/CPU 문제가 아니라 물리 스텝 타이밍 이슈라 이번 점검 범위 밖으로 기록만 남김, 코드 수정 없음(재컴파일 불필요). |
| 최종 코드 리뷰 | Reviewer(Test Eng 겸임) | 대기 | 개선점 있으면 Architect로 반려 |
| README/기술문서 | Doc Writer(Perf Eng 겸임) | 완료 | **[Doc Writer, 2026-08-28]** 리포 루트 `README.md` 신규 작성(GitHub용, 표/목록 위주 5분 스캔 분량). CLAUDE.md/ARCHITECTURE.md/UX_FLOW.md 목차/Tasklist.md/BUG_REPORT_PHASE0~5.md/PERFORMANCE_REPORT.md/CODE_REVIEW_FINAL.md/process.md/Scripts 폴더 구조를 근거로 작성. `Packages/manifest.json`+`ProjectSettings/GraphicsSettings.asset` 직접 확인 결과 URP 패키지 부재 + `m_CustomRenderPipeline` 미설정을 발견 — ARCHITECTURE.md의 "URP 2D" 설계와 실제 적용(Built-in RP) 사이 괴리를 정직하게 문서화(알려진 한계 섹션에 렌더 파이프라인 결정 필요 항목 추가). 씬/프리팹 부재 사실과 그 이유(코드 레이어만 완성, 다음 단계가 캐릭터 프리팹/씬 배선)도 빌드/실행 섹션에 명시해 향후 혼동 방지. |

---

## 교차 레이어 영향 로그 (실시간 공유)
> 한 팀원의 변경이 다른 레이어에 준 영향을 여기에 기록한다.

- **[Architect, 2026-08-27]** Coder의 Phase 4 확인 요청 2건 모두 승인. (1) 윈도우 크래시: 스윙 애니메이션(짧음)과 크랙 오버레이 수명(3초, 원 기획 "3초 뒤 원상복구"와 정확히 일치)을 분리한 해석 승인 — 캐릭터 동작과 시각효과 지속시간은 별개 축이어야 자연스럽다. (2) 하드웨어 반응(배터리/CPU/충전/네트워크)에 `SpectacleEventLock` 미적용 판단 승인 — 이 4가지는 일회성 "스펙터클"이 아니라 지속적인 배경 무드/상태 표현(캐릭터가 헐떡이거나 땀 흘리는 등)에 가까워, 격파미니게임/라이벌대결/드래그던지기/로데오/창도둑/그라피티/크래시/블랙홀 같은 일회성 이벤트와 동시에 발생해도 시각적으로 자연스럽게 공존해야 한다. UX_FLOW.md 28절-29에 이미 이렇게 범위가 한정되어 있었으므로 문서와 구현이 일치.

- **[Architect, 2026-08-27] 결정: 격파 미니게임 "릴리즈 순간" 대사도 자기-전이(self-transition)로 Enter()를 통과시킬 것.** Coder가 남긴 질문(UX 31-2표 #5 대사가 `Tick()` 중 클릭 판정 시점에 확정되어 `Enter()` 전용 원칙과 구조적으로 충돌) 답: 예외를 만들지 않는다. 클릭이 성공/실패로 판정되는 그 프레임에 `Machine.ChangeState(StickmanStateId.BattleMinigame, isForcedInterrupt:false)`로 **자기 자신에게 재전이**시키고, 재전이 직전에 `_dialogueParams`(판정 결과)를 갱신해라 — `RagdollState`가 반복 피격 시 이미 이 패턴(같은 상태로 재전이 → `Enter()` 재실행 → `_settleTimer` 리셋)을 쓰고 있으니 새 개념이 아니라 기존 컨벤션의 재사용이다. 이렇게 하면 "대사는 오직 확정된 전이의 Enter()에서만 파생"이라는 원칙에 예외를 두지 않고도, "판정 순간"과 "전이 확정 순간"을 코드 구조상 같은 프레임의 같은 사건으로 만들 수 있다. Coder는 다음 라운드에서 `BattleMinigameState`의 클릭 판정 지점(현재 `Tick()` 내부로 추정)을 이 패턴으로 교체할 것 — Debugger는 이 항목이 반영됐는지 함께 확인할 것.
  - **[Debugger, 2026-08-27, Phase 3 검토]** 커밋 `a2ae139` 기준 **미반영 확인**. `BattleMinigameState.ResolveOutcome()`(`States/BattleMinigameState.cs:118-142`)이 여전히 `Tick()` 도중 `StickmanEventBus.RaiseBattleMinigamePhaseChanged()`만 호출하고, `Machine.ChangeState(BattleMinigame, ...)` 자기-전이도 `chargeRatio` 기반 `DialogueIntent`도 만들지 않는다(상태 자신의 클래스 주석이 이 한계를 스스로 재확인해두었음). 커밋 메시지가 "다음 라운드"로 명시했으므로 이번 라운드 기준 결함으로 잡지 않았으나(`docs/BUG_REPORT_PHASE3.md` 중점 점검 1), **다음 Coder 라운드 착수 시 최우선 반영 필요**를 재확인한다.
  - **[Coder, 2026-08-27, 반영 완료]** 지시대로 `BattleMinigameState`를 재구성했다: `TickCharging()`이 클릭 판정 순간 `chargeRatio`만 필드에 스냅샷하고 `TriggerResolution()`을 거쳐 `Machine.ChangeState(StickmanStateId.BattleMinigame, isForcedInterrupt:false)`로 자기 자신에게 재전이한다. 재실행된 `Enter()`가 보류 플래그(`_pendingResolution`)를 확인해 신규 `ResolveOutcome(chargeRatio, context)`를 호출 — 성공/실패/재도전/소진 판정(스위트스팟 70~85% 기준, 기존 로직 그대로)과 `BattleMinigamePhaseChanged` 통지, 그리고 `chargeRatio`(별개 축, 표 원문 임계값 0.9) 기반 `DialogueIntent`("필살기다!"/"어... 어라?")를 전부 이 시점에 함께 만든다. `BattleMinigameState`에 `IHasDialogueParams`(`BattleDialogueParams.ChargeRatio`)를 구현해 RagdollState와 동일한 파이프라인을 재사용했다. 배선 부작용 하나를 함께 고쳤다: self-transition도 `StateTransitioned(From=To=BattleMinigame)`를 발행하므로 `BattleMinigameDirector.OnStateTransitioned()`가 이를 "상태 이탈"로 오판해 릴리즈 순간마다 락을 조기 해제할 뻔했다 — `evt.To==BattleMinigame`이면 무시하도록 가드를 추가했다. Unity 배치모드 클린 재빌드(에러0/경고0) + EditMode 8/8 통과로 재검증 완료.
  - **[Debugger, 2026-08-27, 재확인]** 반영 완료 확인 — `Tick()`/`TickResolving()`에 잔여 대사 생성 경로 없음, 판정·대사 파생 전부 `Enter()→ResolveOutcome()` 경유. 이탈 오판 방지 가드(`evt.To==BattleMinigame`이면 무시)도 정확. Unity 배치모드 재실행 에러0/경고0, EditMode 8/8 재확인. 상세: `docs/BUG_REPORT_PHASE3.md` "반려 수정 재확인" 섹션.

- **[Debugger → Coder, 2026-08-27, Phase 3 검토, BUG-P3-M1(Major)]** `SpectacleEventLock`/`Platform.ILocalClickCaptureService`(단일 소유자 락 2종)를 쥐는 4개 Interaction Director(`BattleMinigameDirector`/`DragThrowController`/`RivalEncounterDirector`/`RodeoCursorWatcher`) 전부 `OnDisable()`(구독 해제용으로만 존재)/`OnDestroy()`(어디에도 없음, grep 확인)에서 락을 반환하지 않는다. 두 락 모두 "소유자 본인만 해제 가능"(타임아웃/강제회수 없음, 정적 상태)이라 소유자 컴포넌트가 비활성화/파괴되면 앱 재시작 전까지 4개 스펙터클 기능 전부가 영구 발동 불가능해진다. 지금 당장 이 경로를 유발하는 코드는 없지만, UX_FLOW.md 10절/11절이 명시적으로 권고한 "격파 미니게임 자동발생 끄기"/"라이벌 대결 이벤트 끄기" 설정 토글이 컴포넌트 `enabled`를 끄는 방식으로 구현되면 정확히 이 경로를 밟는다 — Phase 5/6 설정창 구현 착수 전 반드시 선반영 필요. 상세/수정 제안: `docs/BUG_REPORT_PHASE3.md` BUG-P3-M1.
  - **[Coder, 2026-08-27, 반영 완료]** 4개 Director(`BattleMinigameDirector`/`DragThrowController`/`RivalEncounterDirector`/`RodeoCursorWatcher`) 전부 `OnDisable()`에 락 반환 로직을 추가했다. 공통 패턴: (1) 이벤트 구독 해제(기존 코드) → (2) 소유 중이면 캐릭터를 `ChangeState(Idle, isForcedInterrupt:true)`로 안전 복귀(구독을 이미 해제했으므로 `OnStateTransitioned` 캐스케이드에 의존하지 않고 직접 처리) → (3) `SpectacleEventLock.Release(this)`/`ILocalClickCaptureService.ReleaseLocalClickCapture(this)` 명시적 호출. 두 Release 계열 메서드 모두 "소유자 본인일 때만 동작, 아니면 no-op"인 기존 가드 덕에 멱등(중복 호출/미소유 상태에서 호출돼도 예외 없음) — 요구사항대로 락 클래스 자체는 무수정. `RivalEncounterDirector`는 Debugger 제안 그대로 `_rival?.ForceEndDuel()` 경유로 처리. 공용 락 클래스(`SpectacleEventLock`/`LocalClickCaptureGate`)는 변경하지 않았다(이미 멱등 가드가 있어 추가 방어가 필요 없었음). Unity 배치모드 클린 재빌드(에러0/경고0) + EditMode 8/8 통과로 재검증 완료 — 상세는 Tasklist.md Phase 3의 각 행 참고.
  - **[Debugger, 2026-08-27, 재확인]** 4곳 전부 `Release()`/`ReleaseLocalClickCapture()`의 소유자 확인 가드로 멱등 보장됨을 코드로 재확인(예외 없음). BUG-P3-M1 최종 해소. Unity 배치모드 재실행 에러0/경고0, EditMode 8/8 재확인. 상세: `docs/BUG_REPORT_PHASE3.md` "반려 수정 재확인" 섹션.

- **[Architect, 2026-08-27]** Debugger 3차 검토(BUG-P1-R3-B1, Blocker)에서 발견된 "발판 사이 빈틈 점프 시 무한낙하"를 리더가 직접 핫픽스: `FallbackPlatformWindowService.EnumerateFootholds()`를 "real.Count==0일 때만 대체"에서 "항상 안전망 1개를 목록 끝에 추가"로 변경. 조사 중 추가 발견: 원래 합성 발판 Rect가 y=0(OS 좌표계 좌상단 원점 기준 화면 맨 위)에 놓여 있어 주석("화면 하단")과 실제 배치가 반대였던 좌표계 버그도 함께 수정(Screen.height*dpi 기준 하단 근처로 정정). `StickConfig`(desktopDpiScale)를 옵션 인자로 받도록 생성자 확장, 호출부(`StickmanAgent.CreatePlatformService`) 갱신. Unity 배치모드 컴파일 재검증: 에러 0/신규경고 0.
  - **[Debugger, 2026-08-27, 4차 최종 확인]** 커밋 `b901020` 단독 적대적 재검증 — 로직/좌표계 수정 둘 다 정확함을 코드·수식으로 직접 재검산, `ScreenLeftWorldX/RightWorldX` 전체 확장 부작용은 신규 버그 아니고 3차 리포트가 예견한 의도된 트레이드오프(오히려 단일 발판 상황의 숨은 오판 부수적 개선), 다른 호출부 config 누락 없음(grep 확인). `Library/ScriptAssemblies` 삭제 후 클린 재컴파일로 에러 0/경고 2(기존과 동일) 독립 재확인 완료. **Phase 1 최종 승인.** 상세: `docs/BUG_REPORT_PHASE1_R3.md` "4차 최종 확인" 섹션.

- **[UX Designer → Coder, 2026-08-27]** `DialogueIntent`는 상태머신의 `Enter()` 호출(또는 강제 전이 훅)과 반드시 같은 프레임에 생성/취소되어야 함. 전제조건 판정은 `Enter()` 이전에 끝나야 하고, `Enter()` 호출 자체가 "행동 확정" 유일 신호여야 함. 강제 인터럽트(RAGDOLL 등)는 말풍선 최소 노출시간 규칙을 항상 이김(즉시 철회). 근거: `docs/UX_FLOW.md` 5절.
- **[UX Designer → Coder, 2026-08-27]** 상태머신에 "일반 전이"와 "강제 인터럽트 전이"를 구분하는 우선순위/플래그가 이벤트버스에 필요함(현재 설계에 명시적 구분 없음 — 추가 검토 요청). 근거: `docs/UX_FLOW.md` 9절-2.
- **[UX Designer → Coder, 2026-08-27]** 클릭 관통 ON 상태에서도 커서 근접 앰비언트 반응을 위해 전역 커서 좌표 폴링 경로가 클릭-패스스루 구현과 독립적으로 필요(클릭 관통이 좌표 조회까지 막으면 안 됨). 근거: `docs/UX_FLOW.md` 9절-3.
- **[UX Designer → Coder, 2026-08-27]** 전체화면 게임 감지로 숨김 시 상태머신에 "Suspended" 개념 필요(상태/파라미터 보존 후 그대로 재개, IDLE 리셋 금지). 근거: `docs/UX_FLOW.md` 9절-4, 6-4절.
- **[UX Designer → Coder, 2026-08-27]** 멀티모니터 간 논리적 간격(바닥 없음)은 FALL로 처리되어야 하며, 데스크톱 `IPlatformWindowService` 구현체가 모니터별 경계 정보를 노출해야 함. 근거: `docs/UX_FLOW.md` 9절-5, 6-3절.
- **[UX Designer → Coder, 2026-08-27]** 인질극/로데오 등 방해성 이벤트는 일반 상태 전이 규칙을 완전히 우회하는 "글로벌 강제 종료 훅"(트레이 긴급정지) 채널이 필요 — Phase 3/4 설계 시 지금 인터페이스에 자리를 예약할 것. 근거: `docs/UX_FLOW.md` 9절-6, 6-5절.
- **[UX Designer → Coder, 2026-08-27]** `ScreenshotBackdropPlatformService`의 발판 목록은 가변(추가/삭제/재지정)이어야 하고, 코어 루프 시작 전 "발판 0개" 여부를 막는 `IsConfigured` 가드가 필요함. 배경 이미지 교체 시 기존 발판 좌표는 같은 트랜잭션으로 무효화되어야 함(불일치 상태 금지). 근거: `docs/UX_FLOW.md` 9절-7~8, 3절/7절.
- **[Coder → UX Designer/Architect, 2026-08-27]** Phase 0 스캐폴딩 완료 보고 및 UX_FLOW.md 9절 대응 현황:
  - (9절-1) 반영 완료. `DialogueIntent` 생성자는 `StateTransitionContext`를 요구하고, 이 컨텍스트는 `StickmanStateMachine.ChangeState()` 내부에서 `Enter()` 호출 직전에만 발급됨 — 구조적으로 `Enter()` 호출 시점과 동일 프레임에서만 생성 가능.
  - (9절-2) 반영 완료. `ChangeState(next, isForcedInterrupt:)`와 `StateTransitionEvent.IsForcedInterrupt` 플래그를 추가해 "일반 전이"와 "강제 인터럽트"를 이벤트버스에서 구분 가능하게 함. 단, `DialogueIntent`의 즉시 만료 자체는 이 플래그와 무관하게 `TransitionGeneration` 불일치만으로 항상 발동함(정상 종료든 강제 취소든 다음 전이 시점에 만료) — UI 레이어가 최소 노출시간 로직을 짤 때 "만료 이벤트 + IsForcedInterrupt 값"을 함께 보고 페이드아웃(false)/즉시제거(true)를 분기해야 함. Phase 2 DialogueIntent 실제 구현 시 필독.
  - (9절-3, 커서 좌표 폴링) 미반영. `IPlatformWindowService`에 커서 전역 좌표 조회 메서드가 아직 없음 — Phase 1에서 클릭관통 구현 시 별도 메서드(또는 별도 인터페이스) 추가 필요. 지금 인터페이스에 자리를 예약하지 않은 이유: Phase 0 태스크 범위가 "읽기전용 열거/오버레이 생성/클릭관통/항상위/전체화면감지"로 명시되어 있어 임의 확장을 피함. Phase 1 착수 전 Architect 확인 요청.
  - (9절-4, Suspended 개념) 미반영. 현재 `IStickmanState`/`StickmanStateMachine`에 일시정지/재개 API가 없음. Phase 1 "전체화면 게임 감지 → 자동 숨김" 구현 시 상태·파라미터 보존 방식(예: `Suspend()`/`Resume()` 또는 숨김 중 Tick 스킵) 설계 필요.
  - (9절-5, 모니터 경계 노출) 미반영. `PlatformFoothold`는 개별 창 사각형만 제공하고 모니터 자체의 경계 목록은 없음. Phase 1 멀티모니터 FALL 판정 구현 시 `IPlatformWindowService`(Windows/macOS 구현체)에 모니터 열거 메서드 추가 필요.
  - (9절-6, 글로벌 긴급정지 예약) 반영 완료. `StickmanEventBus.GlobalEmergencyStopRequested` 이벤트 슬롯을 미리 예약해둠(현재는 발행/구독자 없음, Phase 3/4에서 트레이 UI가 발행, 방해성 이벤트 상태가 구독 예정).
  - (9절-7, 8) 반영 완료. `IsConfigured` 프로퍼티 추가, `SetBackdropScreenshot()`가 호출 시 `ClearUserDefinedFootholds()`를 같은 트랜잭션으로 실행해 배경-발판 불일치를 방지.
- **[Coder → Debugger/Architect, 2026-08-27]** `DialogueIntent`/`StateTransitionContext` 설계의 알려진 한계: C# 구조체는 항상 매개변수 없는 생성자를 가지므로 `default(StateTransitionContext)`로 `OriginMachine == null`인 가짜 컨텍스트를 만들 수 있음. `DialogueIntent` 생성자가 이 경우 즉시 `ArgumentException`을 던지도록 방어했으나 컴파일 타임 차단은 아님 — 코드 리뷰로 "Enter() 밖에서 DialogueIntent를 만들지 않는다"는 컨벤션을 지켜야 함. Test Engineer는 Phase 2 회귀 테스트에 "Enter() 콜스택 밖에서 생성 시 예외 발생" 케이스 추가 검토 요청.
- **[UX Designer → Coder, 2026-08-27]** Phase 3/4 선행 설계에서 "부분적 클릭관통 해제(Partial Click-Through Override)" 개념 도출 — **지금 Coder가 배선 중인 Phase 1 전역 클릭관통 토글과 직접 연결됨**. 정의: 화면 전체 클릭관통 상태와 별개로, 캐릭터의 현재 화면 히트박스 영역에 한해서만 한정된 시간 동안 클릭을 앱이 수신하고 그 외 영역은 항상 관통되는 예외 메커니즘. 격파 미니게임/드래그&던지기/인질극 드래그&셰이크(3개 기능)가 공통으로 요구하며, 기존 7절 대결모드 토글도 사실 이 메커니즘을 유저가 수동으로 무기한 지속시키는 형태로 재정의 가능. **Phase 1 요청사항**: 지금 전역 클릭관통 토글 인터페이스를 "단순 불리언 하나"로 확정하지 말고, 나중에 영역(hitbox)·지속시간 인자를 끼워 넣을 수 있는 여지를 열어둘 것(예: 향후 `EnableLocalClickThroughOverride(hitboxRegion, releaseCondition)` 같은 단일 API로 확장 가능한 구조). 반드시 지킬 제약: (1) 히트박스 밖 클릭은 예외 활성 중에도 100% 관통(원칙 2), (2) 동적 히트박스 추적(정적 좌표 고정 금지), (3) 단일 소유자 락(동시에 둘 이상 이벤트가 이 리소스를 요청 못 하게), (4) 마우스다운→반영 지연 1프레임 이내 목표(9절-3 커서 폴링 채널과 결합 권장), (5) **인질극의 "닫기 버튼 막기"는 이 예외를 절대 적용하지 않는 유일한 역예외**(실제 창 클릭은 항상 그대로 관통되어야 함, 원칙 3). macOS는 `NSWindow.ignoresMouseEvents`가 전체 온/오프만 지원해 영역 기반 부분 해제에 우회 설계가 필요할 수 있음(정보 공유). 근거: `docs/UX_FLOW.md` 10/12/14/15/16절.
- **[Debugger → Coder/Architect, 2026-08-27]** Phase 0 산출물 적대적 검증 완료. 전체 리포트: `docs/BUG_REPORT_PHASE0.md` (Blocker 1 / Major 8 / Minor 8, **Coder로 반려 필요** 판정). 교차 레이어 영향이 큰 항목만 요약:
  - **(BUG-B1, Blocker)** `Win32WindowService.CreateOverlayWindow()`가 Unity 게임 자신의 `MainWindowHandle`을 재사용하는 스텁인데, `SetClickThrough`/`SetAlwaysOnTop`이 그 핸들에 아무 가드 없이 직접 부작용을 가함 — 위 "부분적 클릭관통 해제" 요구사항과 정확히 같은 코드 경로를 공유하므로, **진짜 오버레이 HWND 구현 없이는 전역 클릭관통도 부분 해제도 둘 다 안전하게 만들 수 없음**. Phase 1 클릭관통 작업의 최우선 선결 과제로 격상 요청.
  - **(BUG-M1, Major)** Coder가 98번 로그에서 스스로 지목한 `default(StateTransitionContext)` 우회는 실제 위험의 일부에 불과함 — `StateTransitionContext` 생성자와 `StickmanStateMachine.CurrentTransitionGeneration`이 모두 `public`이라, `default()`가 아니라 **머신 참조 하나만 있으면 `Enter()` 밖 어디서든 "진짜처럼 통과하는" 컨텍스트를 위조**할 수 있어 원칙 1(행동-텍스트 싱크) 방어선이 실질적으로 뚫려 있음. Phase 2까지 기다리지 말고 지금 생성자/프로퍼티 접근 제한자를 좁히는 최소 수정을 권고(상세: 리포트 BUG-M1).
  - **(BUG-M2, Major)** `ChangeState()`가 `_states[next]` 조회를 `Exit()`/세대 증가 이후에 수행해 원자적이지 않음 — 미등록 키 호출 시 상태머신이 "좀비" 상태로 영구 고착되는 상태머신 데드락 위험(상세: 리포트 BUG-M2). Phase 1 IDLE/WALK/JUMP/FALL 배선 전 `TryGetValue` 선검증 추가 권고.
  - **(BUG-M4, Major)** `ScreenshotBackdropPlatformService.SetBackdropScreenshot()`이 "새 배경으로 교체"와 "이전 세션 상태 복원"을 구분하지 못해 무조건 발판을 초기화함 — 모바일 영속화(재실행 시 이전 배경/발판 복원)가 붙는 순간 앱을 켤 때마다 발판 재지정 온보딩이 강제로 뜨는 심각한 UX 회귀가 될 수 있음(상세: 리포트 BUG-M4). Phase 1에서 모바일 영속화를 다루게 되면 착수 전 필수 확인.
  - **(BUG-M8, Major, 정보 공유)** `StateTransitionEvent`/`DialogueIntent`에 캐릭터(소스) 식별자가 전혀 없음 — 위 UX Designer의 "라이벌 스틱맨 조우"(Phase 3) 설계와 맞물려, 다중 `StickmanStateMachine`이 공존하는 시점에 "이 전이/대사가 어느 캐릭터 것인지" 구분이 구조적으로 불가능함. 필드 추가 비용이 지금이 가장 싸므로 Phase 3 착수 훨씬 전인 지금 자리만 예약해둘 것을 제안(상세: 리포트 BUG-M8).
  - 나머지(BUG-M3/M5/M6/M7, Minor 8건)는 위 Phase 1 표의 각 작업 행에 개별 메모로 남겨두었음.
- **[Debugger → Coder/Architect/UX Designer, 2026-08-27]** Phase 1 산출물(실제 Unity 6 LTS 프로젝트 기준) 2차 적대적 검증 완료. 전체 리포트: `docs/BUG_REPORT_PHASE1.md` (Blocker 2 / Major 6 / Minor 4, **Coder로 반려 필요** 판정). 교차 레이어 영향이 큰 항목만 요약:
  - **(BUG-P1-B2, Blocker, UX/Architect 결정 필요)** `IdleState`/`WalkState`의 이동 트리거가 `StickmanAgent.Update()`의 `Input.GetAxisRaw("Horizontal")`/`GetButtonDown("Jump")`(키보드) 뿐이고, 자율 배회/행동 결정 로직이 프로젝트 전체에 전무함(`Random`/`Wander`/`AI` grep 결과 0건). `docs/UX_FLOW.md` 2절/8절이 못박은 "P0 — 아무것도 안 해도 재미있어야 함" 원칙을 정면 위반하며, 더 심각하게는 BUG-B1(Phase0)이 올바르게 해결되어 진짜 `WS_EX_NOACTIVATE` 오버레이가 완성되는 순간 그 창은 키보드 포커스를 받을 수 없어(가설 H6) 이 입력 경로 자체가 영구히 죽고 캐릭터가 Idle에 고착된다 — "지금은 우연히 동작"하는 구조. UX Designer/Architect가 "자율 배회 AI"를 Phase 1.5/2 필수 선행 태스크로 Tasklist에 신규 등재할지 결정 필요(현재 어느 Phase 정의에도 이 태스크가 명시된 적이 없어 아무도 지목하지 않고 넘어갈 뻔했음).
  - **(BUG-P1-B1, Blocker)** `Win32WindowService`가 제목 있는 가시 창을 하나도 못 찾으면(흔한 상황 — 모든 창 최소화 등, 가설 H5) `GroundSensor`/`GroundedTick`에 안전망이 없어 캐릭터가 화면 밖으로 무한 낙하함. `NullPlatformWindowService`의 "화면 하단 더미 발판" 폴백 개념이 실제 데스크톱 구현체에 이식되어 있지 않음 — `docs/UX_FLOW.md` 1-A/6-1절 "빈 상태" 요구사항 위반(상세: 리포트 BUG-P1-B1).
  - **(BUG-P1-M1, Major)** `StickmanAgent`가 `Camera.main`을 `Awake()`에서 1회만 캐싱하고 재검증/재획득이 없음 — 씬에 `MainCamera` 태그가 없거나 카메라가 파괴되면 BUG-P1-B1과 동일한 무한 낙하 실패모드로 이어짐. 아직 씬/프리팹이 없어(Phase 2+ 배선 예정) 잠재 상태지만 흔한 Unity 실수 하나로 즉시 발현되므로 씬 구성 시점에 미리 방어 코드 필요(상세: 리포트 BUG-P1-M1).
  - **(BUG-P1-M6, Major, Phase 2 직결)** `StickmanAgent.Suspend()/Resume()`가 단일 `Rigidbody2D`만 가정 — Phase 2 Active Ragdoll(다중 파츠) 도입 전 반드시 `GetComponentsInChildren<Rigidbody2D>(true)` 기반으로 일반화 필요(상세: 리포트 BUG-P1-M6, Phase 2 표 행에도 메모).
  - 나머지(BUG-P1-M2/M3/M4/M5, Minor 4건)는 위 Phase 1/Phase 2 표의 해당 작업 행에 개별 메모로 남겨두었음.
- **[Coder → Debugger/Architect, 2026-08-27]** Debugger 지적사항 반영 완료 보고 (Phase 1 실구현과 함께 처리, Architect 지시대로 B1/M2/M1/M3/M5 우선 반영 — 상세는 각각 Phase 1 표 행의 [Coder] 메모 참고):
  - **(BUG-M3, BUG-M5)** `Platform/FootholdPoller.cs`(주기 폴링+캐시, `StickConfig.footholdPollInterval` 소비)와 `Platform/ScreenCoordinateConverter.cs`(Unity 스크린↔OS 데스크톱 좌표, `StickConfig.desktopDpiScale`) 신규 추가. `States/GroundSensor.cs`가 이 두 유틸만으로 접지/화면경계 판정을 계산하는 단일 창구 — 모든 State(Idle/Walk/Jump/Fall)는 `StickmanBlackboard.SenseGround()`만 호출하고 `IPlatformWindowService`나 좌표 변환식을 직접 만들지 않음(좌표계 혼용 버그 재발 방지).
  - **(BUG-M2)** `StickmanStateMachine.ChangeState()`를 `_states.TryGetValue` 선검증 후 실패 시 뮤테이션(Exit/세대증가) 없이 안전 반환하도록 수정. 기존 호출부 동작(유효한 키)에는 영향 없음.
  - **(BUG-M1)** `StateTransitionContext`(생성자+5개 필드)와 `StickmanStateMachine.CurrentTransitionGeneration`을 `public`→`internal`로 좁힘. `IStickmanState`/`DialogueIntent`/`StickmanEventBus`의 공개 시그니처(Enter/Tick/Exit, DialogueIntent 생성자, 이벤트 목록)는 전혀 건드리지 않음 — Coder 작업 지침(4대 보호 인터페이스 시그니처 변경 금지)을 지키면서 그 "옆"의 캡슐화만 강화. **한계(Debugger 원 지적 그대로 유지)**: 이 프로젝트에 asmdef가 전혀 없어 States/Dialogue 네임스페이스가 전부 같은 기본 어셈블리(Assembly-CSharp)로 컴파일되므로, `internal`은 "다른 어셈블리에서의 위조"만 막고 같은 어셈블리 내부의 임의 코드가 `internal` 생성자를 직접 호출하는 것까지는 막지 못함. 완전한 방어(발급 1회용 토큰 + sealed 클래스)는 Debugger/Coder 합의대로 Phase 2로 유지.
  - **(BUG-B1, Blocker)** Architect 지시("이번 스코프를 넘는다면 최소한 가드")에 따라 진짜 분리 오버레이(CreateWindowEx) 구현은 하지 않고, `Win32WindowService`에 임시 안전가드만 적용: `_usingUnsafeSelfWindowFallback` 플래그가 켜진 동안(현재 `CreateOverlayWindow()`가 항상 켜둠) `SetClickThrough`/`SetAlwaysOnTop`가 `NotSupportedException`을 던져 게임 창 자체 파괴를 차단. `WS_EX_NOACTIVATE` 상수 추가 및 `SetClickThrough`의 스타일 조합에 포함(BUG-B1(c), 가설 H2 완화). `Core/StickmanAgent.cs`가 이 예외를 잡아 로그만 남기고 나머지 초기화를 계속 진행 — 결과적으로 **Windows에서는 클릭관통이 아직 실제로 켜지지 않는다(의도된 안전 실패)**. "클릭 관통 기본 ON" Tasklist 행은 Debugger 지시대로 완료 처리하지 않고 진행중으로 유지. **다음 담당자에게**: 진짜 오버레이 HWND(CreateWindowEx, 가상 데스크톱 전체 크기, WS_EX_LAYERED|TRANSPARENT|TOPMOST|NOACTIVATE 처음부터 적용) 구현이 별도 후속 작업으로 필요.
  - 커서 좌표 조회는 UX_FLOW.md 9절-3 요구사항에 따라 `IPlatformWindowService`를 확장하지 않고 신규 `Platform/ICursorPositionService.cs`로 분리 배선(Win32는 `GetCursorPos`, 에디터는 `NullPlatformWindowService`가 `Input.mousePosition`으로 대체 구현) — 클릭관통 여부와 완전히 독립. 이 설계 판단(기존 인터페이스 확장 대신 신규 인터페이스 신설) 자체에 대해 **Debugger 검토 요청**.
  - **[Coder → Debugger/Architect, 검토 요청]** `StickmanStateMachine` 생성자가 즉시 `ChangeState(initialState)`를 호출해 초기 상태의 `Enter()`를 실행하는데, `Core/StickmanAgent.cs`에서는 이 생성자 호출 시점에 아직 `StickmanBlackboard.Machine`이 할당되지 않은 상태다(생성자 반환 후에야 `_blackboard.Machine = _machine` 대입). 현재 `IdleState.Enter()`는 `blackboard.Machine`을 참조하지 않아 Phase 1에서는 문제없지만, Phase 2 이후 어떤 초기 상태의 `Enter()`가 `Machine`을 참조하게 되면 `NullReferenceException`이 발생한다. `StickmanStateMachine` 생성자 타이밍(즉시 ChangeState 호출) 자체를 바꾸는 건 구조 변경이라 이번 Phase 1에서 임의로 고치지 않고 그대로 남겨둔다 — Phase 2 착수 전 검토 요청.
  - **[Coder, 범위 외 확인]** BUG-M4/M6(모바일 `ScreenshotBackdropPlatformService`의 배경 교체-복원 구분, 텍스처 파괴 누락)은 Architect 지시대로 이번 Phase 1에서 손대지 않음(모바일 영속화/온보딩 붙을 때 처리). `NullPlatformWindowService`/`ScreenshotBackdropPlatformService` 둘 다 `FootholdPoller`(신규)와 인터페이스 호환 확인 완료 — 별도 수정 불필요(모바일은 탭 즉시 자체적으로 `RaiseFootholdsChanged()`를 이미 호출하므로 폴러의 주기 재확인은 무해한 중복).
  - **[Coder, 범위 외 확인]** macOS `IPlatformWindowService` 구현체는 여전히 없음(m8, Null 폴백만 존재) — `Core/StickmanAgent.cs`의 플랫폼 팩토리는 `UNITY_STANDALONE_WIN`/`UNITY_IOS`/`UNITY_ANDROID`만 분기하고 macOS는 Null로 폴백. 기존에 이미 커버리지 공백으로 기록된 사안 그대로 유지, Phase 1 범위 밖.
- **[Coder → Debugger/Architect/UX Designer, 2026-08-27, 3차 반영]** `docs/BUG_REPORT_PHASE1.md` 2차 리포트(Blocker 2 / Major 6 / Minor 4) 전체 대응 완료. 교차 레이어 영향이 있는 설계 판단만 요약(개별 항목 상세는 Phase 1/2 표의 해당 행 [Coder] 메모 참고):
  - **(BUG-P1-B2, Blocker)** UX Designer가 같은 날 `docs/UX_FLOW.md` 26절(자율 배회 AI 행동 설계)을 긴급 작성해줘서, 리더가 지시한 "임시 구현 → 스펙 도착 시 교체" 단계를 거치지 않고 **정식 구현을 바로** 넣었다. `States/IMovementIntentSource.cs`(`MoveInputX`/`JumpRequested` getter만 있는 최소 계약)와 `States/AutoWanderController.cs`(26-1~26-3 수치 그대로)를 신설했고, `StickmanBlackboard.MoveInputX`/`JumpPressed`를 필드에서 `IntentSource` 위임 프로퍼티로 바꾼 것 외에 `IdleState`/`WalkState`/`JumpState`/`FallState` 4개 State 클래스는 **한 줄도 수정하지 않았다**(26-6에서 UX가 예측한 그대로). `UnityEngine.Input` 참조는 프로젝트 전체에서 0건(grep 재확인) — 대결모드(Phase 3)에도 키보드를 다시 넣지 않기로 한 26-5 결정을 그대로 따름, Phase 3 착수 팀은 마우스/클릭 기반 인터랙션만 설계할 것.
  - **(26-7 요구사항 반영)** `GroundSensor.GroundInfo`에 `CurrentFootholdLeftWorldX`/`RightWorldX`(전역 통합 경계와 별개로, "지금 딛고 있는 그 발판 하나"만의 경계) 필드를 추가했다 — `GroundInfo` 생성자 시그니처가 2개 인자 늘어났지만 이 구조체를 직접 생성하는 곳은 `GroundSensor.cs` 내부 2곳뿐이라 다른 레이어에 영향 없음(grep으로 확인).
  - **(26-3 이벤트버스 확장)** `StickmanEventBus`에 `WanderAmbientMotionRequested`(페이로드: `WanderAmbientMotion.LookAround`/`SitAndYawn`) 이벤트를 신설했다. 지금은 아무도 구독하지 않는다 — Phase 2 렌더링 레이어가 이 이벤트를 구독해 실제 두리번거리기/앉기-하품 애니메이션을 재생하면 된다(트리거 조건 계산은 이미 `AutoWanderController`가 전담).
  - **(26-4 훅 예약)** UX Designer 판단대로 커서 근접 반응은 Phase 2로 연기하되, 지금 재배선 비용을 없애기 위해 `AutoWanderController.CursorProvider`(델리게이트 프로퍼티) 훅만 열어뒀다. `StickmanAgent.Awake()`가 생성 직후 `TryGetCursorPosition`을 연결해두지만 `AutoWanderController`는 아직 이 값을 전혀 읽지 않는다 — Phase 2에서 26-4 표의 수치(150px 반경, Walk 중만, 0.4~0.8초 정지)로 반응 로직만 채우면 된다.
  - **(BUG-P1-B1, Blocker)** 수정안 (b) 채택 — `Platform/FallbackPlatformWindowService.cs`(데코레이터) 신설. **판단 근거를 남겨둠**: 이 데코레이터는 `Win32WindowService`에만 적용하고 `ScreenshotBackdropPlatformService`(모바일)에는 절대 적용하지 않는다 — 모바일의 "발판 0개"는 `IsConfigured` 온보딩 게이트가 의도적으로 관찰하는 신호라서, 여기서 항상 발판이 있는 척하면 그 게이트가 조용히 죽는다. Phase 1/모바일 온보딩 UI를 맡을 담당자는 이 경계를 반드시 인지할 것.
  - **(BUG-P1-M2, Phase 2 선반영)** `StickmanStateMachine(states)` + `Start(initialState)` 분리를 Phase 2 착수 전에 미리 반영했다. Phase 2 담당자는 새 초기 상태를 등록할 때 생성자가 더 이상 즉시 `Enter()`를 호출하지 않는다는 점만 유의하면 된다(`blackboard.Machine` 배선 후 `Start()` 호출 순서를 지킬 것).
  - **(BUG-P1-M6, Phase 2 선반영)** `StickmanAgent.Suspend()/Resume()`을 `_allBodies`(`GetComponentsInChildren<Rigidbody2D>(true)`) 전체 순회로 일반화했다. Phase 2에서 RAGDOLL 상태의 사지 Rigidbody2D를 몸통 자식으로 배치하기만 하면 별도 수정 없이 Suspend/Resume이 자동으로 전부 커버한다.
  - 나머지(BUG-P1-M1/M3/M4/M5, Minor m4)는 단일 파일 내 값싼 수정이라 교차 레이어 영향 없음 — 각 항목 상세는 Phase 1 표의 해당 행 참고. Unity 배치모드 컴파일 재검증: 에러 0건, 경고 2건(RagdollState/GetupState 기존 미사용 필드뿐, 신규 경고 없음 — 이전 라운드 기준선 그대로 유지).
- **[Debugger → Coder/Architect, 2026-08-27, 3차 타겟 검토]** 커밋 `6ee9be4`(2차 반려 수정분)만 적대적 재검증. 전체 리포트: `docs/BUG_REPORT_PHASE1_R3.md` (Blocker 1건 신규, **Coder로 재반려 필요**).
  - BUG-P1-B2(키보드 의존): `grep`으로 프로젝트 전체 `GetAxisRaw`/`GetButtonDown` 등 실제 호출 0건 재확인 — **완전 해결**. BUG-P1-M2(생성자/Start 분리): `StickmanAgent.Awake()`가 `_blackboard.Machine = _machine;` 다음에 `_machine.Start(...)`를 호출하는 순서를 코드로 직접 재확인 — **정확히 배선됨**. `AutoWanderController`의 UX_FLOW.md 26절 수치(Idle/Walk 지속시간, 방향전환 8%, Idle후 75/20/5% 분기, 경계 90/10%, 지터 ±17.5% 등)는 `StickConfig` 필드와 1:1 대조 완료, 전부 일치.
  - **(BUG-P1-R3-B1, Blocker, 신규)** `AutoWanderController`의 "발판 경계 10% 점프 시도"(26-2)가, 서로 떨어진 두 발판(예: 창 A/B) 사이의 빈 틈으로 점프했을 때 착지 실패 시 안전망이 없다 — `GroundSensor.GroundInfo.ScreenLeftWorldX/RightWorldX`가 "모든 발판의 합집합 바운딩 박스"라서 발판 사이 틈을 표현하지 못하고, `FallbackPlatformWindowService`는 실제 발판이 1개라도 있으면 개입하지 않기 때문(`real.Count > 0`이면 그대로 통과). 결과: 캐릭터가 Y축으로 영원히 낙하(Y축 하한 체크/리스폰 로직 전무, grep 확인) — BUG-P1-B1이 막으려던 것과 동일한 "영구 소멸" 실패 모드가 유저 조작 없이 자율적으로, 통계적으로 필연에 가깝게 재발한다. **수정 권고**: `FallbackPlatformWindowService`가 실제 발판이 있어도 화면 최하단 안전망을 항상 추가로 덧붙이도록 변경(현재는 "0개일 때만 대체"). 상세: `docs/BUG_REPORT_PHASE1_R3.md` BUG-P1-R3-B1.
  - 배치모드 재컴파일: 에러 0건 확인(경고 카운트는 Library 캐시 재사용으로 이번 로그에서 독립 재확인 못함 — 이전 기준선과 상충하는 증거는 없음).
  - **[Debugger, 2026-08-27, 후속]** BUG-P1-R3-B1은 Architect 핫픽스(`b901020`)로 해결됨 — 4차 확인에서 클린 재빌드 포함 검증 완료, **Phase 1 최종 승인**. 상세는 위 4차 최종 확인 로그 및 `docs/BUG_REPORT_PHASE1_R3.md` 참고.
- **[Coder → Debugger/Architect/UX Designer, 2026-08-27, Phase 2 완료 보고]** Active Ragdoll(RAGDOLL/GETUP)/ParkourClimb/DialogueIntent 강화(BUG-P0-M1+BUG-M7) 구현 완료(상세는 위 Phase 2 표의 각 행 [Coder] 메모 참고). UX_FLOW.md 29~31절(같은 날 UX Designer가 병행 작성)과 대조한 결과 공유:
  - **UX 31절(최우선, DialogueIntent 파라미터 카피 원칙)은 이번 라운드에 전면 반영**했다 — 31-1 원칙("같은 매핑 함수·같은 스냅샷 안의 조건 분기만 허용")을 `IHasDialogueParams` 설계 자체가 구조적으로 강제하도록(파라미터를 호출자가 아니라 `StickmanStateMachine.CurrentState`에서 직접 읽음) 만들었고, 31-2 표의 5개 예시 중 3개(Attack/Ragdoll/ParkourClimb)를 실제 코드로 시연했다. 나머지 2개(Getup의 `reimpactCount`, Phase3 격파 미니게임의 `chargeRatio`)는 각각 "여러 RAGDOLL↔GETUP 사이클에 걸친 누적 카운터 추적"(추가 상태 설계 필요)과 "Phase 3 스코프 명시(31-2 표 자체가 '지금 구현 대상 아님'이라고 명시)"라 이번 라운드에서 제외했다 — 파이프라인 자체는 이미 3개 상태로 교차 검증되어 구조적 타당성은 확인됨.
  - **UX 29-1(히트스톱/흑백플래시/폭발선 3단 콤보)은 미반영** — `Time.timeScale` 기반 전역/로컬 타임스케일 조작은 물리 시뮬레이션(RagdollRig 속도 측정, `_settleTimer` 누적)과 상호작용이 커서 렌더링 레이어 설계가 먼저 필요하다고 판단했다. Phase 2+ 렌더링 담당자가 착수 전 Architect 확인 요청.
  - **UX 29-2(충격 크기별 최소 낙사시간 3구간)/29-3(GETUP Stir/Rise 3단계 분해)은 미반영** — 이번 라운드는 리딩 에이전트 지시 및 `ARCHITECTURE.md` 0절/`StickmanStateMachine.cs` 전이 규칙 주석이 명시한 단순 메커니즘(속도 임계값+지속시간 하나, 진행도 하나)을 그대로 구현했다. UX의 정제(refinement)는 구조적으로 상충하지 않는 추가 레이어이며, 마침 이번에 `StickmanBlackboard.LastImpactMagnitude`(충격량 스냅샷)를 이미 배선해뒀으므로 29-2의 "충격 크기별 구간화"는 다음 라운드에 이 필드를 재사용해 저비용으로 얹을 수 있다. 29-3의 3단계 분해도 `GetupState._getupProgress`를 3개 구간 타이머로 나누는 정도의 리팩터라 기존 구조와 충돌하지 않는다. 우선순위 판단 요청.
  - **UX 30-3(ROLL/RAGDOLL 경계)과 지금 구현의 축이 다름 — Debugger/Architect 조율 필요**: 30-3은 "낙하 충격이 `ragdollForceThreshold`(물리 충격량)를 넘으면 이미 RAGDOLL로 처리되므로 ROLL엔 실패 조건이 구조적으로 없다"고 가정하지만, 이번에 구현한 낙하 구르기 훅은 리딩 에이전트 지시대로 **낙하 높이**(월드 Y, `StickConfig.rollLandingHeightThreshold`) 기반이다. 두 모델이 자동으로 합쳐지지 않는 이유: 이 프로젝트의 발판(foothold)은 가상 판정(`GroundSensor`가 좌표만 비교)이라 실제 `Collider2D`가 없고, 따라서 착지 시 `StickmanAgent.OnCollisionEnter2D`(물리 충돌 기반, RAGDOLL 강제 진입의 단일 진입점)가 애초에 발동하지 않는다 — 즉 "세게 착지 = RAGDOLL"이라는 30-3의 전제가 지금 구조에서는 자동 성립하지 않는다. 높이 기반 훅과 충격량 기반 RAGDOLL 진입을 하나의 일관된 모델로 합칠지(예: 착지 시 낙하 속도로부터 가상 임팩트를 계산해 `ReportExternalImpact()`에 흘려보내는 방식 도입) 여부는 Architect 판단 요청 — 지금은 두 축이 병렬로 존재하는 상태임을 명확히 기록해둔다.

**[Architect, 2026-08-27] 결정: 두 축을 합치지 않는다 — 지금 구현(병렬 유지)이 정답이다.** 기획 원문 1-4절("높은 창에서 바탕화면 맨 아래 작업표시줄로 떨어질 때 덤블링하여 부드럽게 착지하는 이펙트")은 낙하 높이와 무관하게 착지가 **항상** 우아한 구르기여야 한다고 명시한다. 즉 "세게 떨어지면 Ragdoll로 나뒹굴어야 한다"는 30-3의 전제 자체가 낙하/착지 케이스에는 적용되면 안 되고, RAGDOLL은 오직 실제 외부 충격(던져짐/피격/해머 등 진짜 `Collider2D` 충돌)에만 발동해야 이 앱의 "절대 안 부서지고 항상 멋지게 착지하는" 캐릭터 톤이 유지된다. `LandingRollRequested`(낙하높이 기반, 순수 연출)와 `ReportExternalImpact`(충격량 기반, 상태전이)는 서로 다른 목적의 별도 이벤트로 영구히 유지한다 — 이 항목은 종결.
  - **UX 30-1(벽 근접 예고 UI 없음)/30-2(HANG 손떨림 진폭 연출)**: 상충 없음 — 30-1은 "하지 말라"는 지시라 구현 자체가 없고(원래 안 넣었음), 30-2는 순수 렌더링 레이어(Perlin 노이즈 진폭 곡선) 몫이라 이번 상태머신 구현과 무관.
  - Ragdoll/Getup/ParkourClimb 상태 생성자가 전부 `StickmanBlackboard` 주입형으로 바뀌어(`Idle/Walk/Jump/Fall`과 동일 패턴) `StickmanAgent.Awake()`의 상태 등록 딕셔너리가 갱신됨 — `AttackState`만 아직 파라미터 없는 생성자(Phase 3 전투 로직 붙을 때 함께 갱신 예정). Unity 배치모드 컴파일 재검증 2회(중간/최종) 모두 에러 0건/경고 0건(기존 2건이던 `RagdollState`/`GetupState` 미사용 필드 경고가 `_settleTimer`/`_getupProgress`를 실제로 소비하게 되며 예상대로 자연 소멸).

- **[Coder, 2026-08-27, Phase 3 완료 보고]** 격파 미니게임(10절)/라이벌 스틱맨 AI(11절)/드래그&던지기(12절)/로데오 커서(13절) + 선행 인프라 "부분적 클릭관통 해제"(15절) 구현 완료. 신규 폴더 `Assets/_Project/Scripts/Interaction/`(Phase 3 전용 컨트롤러 레이어, StickmanAgent/States는 이 폴더의 존재를 전혀 모른다 — Core→States는 기존처럼 참조하되 Interaction→Core/States만 참조하는 단방향 유지) — `StickmanClickHitbox`/`ClickHitboxRectUtility`/`DragThrowController`/`BattleMinigameDirector`/`RodeoCursorWatcher`/`RivalPursuitIntentSource`/`RivalStickmanAgent`/`RivalEncounterDirector` 8개 신규 파일. `StickmanStateId`에 `BattleMinigame`/`Dragged`/`RodeoCursor` 3종 추가(총 11종), 각각 신규 `States/BattleMinigameState.cs`/`DragThrowState.cs`/`RodeoCursorState.cs`로 구현하고 `StickmanAgent.Awake()`의 상태 딕셔너리에 등록. `AttackState`도 이번에 파라미터 없는 생성자에서 블랙보드 주입형으로 전환하며 `Tick()`을 처음으로 완성(라이벌 대결의 유일한 실사용처, 상세는 위 "라이벌 스틱맨 AI" 행 참고).

  - **핵심 설계 결정 1 — 부분적 클릭관통 해제(15절)를 `IPlatformWindowService` 확장이 아니라 별도 `ILocalClickCaptureService`로 분리.** UX Designer가 예시로 제안한 `RequestLocalClickCapture(hitboxOsScreen, owner)`/`ReleaseLocalClickCapture(owner)` API 형태는 그대로 채택했지만, 인터페이스 자체는 `ICursorPositionService`(Phase 1)와 똑같은 이유로 분리했다 — 모바일(`ScreenshotBackdropPlatformService`)에는 "전역 클릭관통"이라는 개념 자체가 없어(9절 코멘트 그대로) "그 일부를 국소 해제"한다는 개념도 성립하지 않기 때문. `Platform/LocalClickCaptureGate.cs`(단일 소유자 락 + 동적 영역 부기 순수 로직)를 `Win32WindowService`/`NullPlatformWindowService`가 각자 인스턴스로 들고 위임하고, `FallbackPlatformWindowService`는 내부 서비스로 그대로 delegate — `ICursorPositionService` 캐스팅 패턴을 그대로 재사용해 새 캐스팅 관례를 만들지 않았다.
  - **핵심 설계 결정 2 — "부분적 클릭관통 해제"의 실제 구현 한계(이번 작업 지시의 최우선 기록 요구사항).** `Platform/ILocalClickCaptureService.cs` 파일 상단에 상세히 문서화했고 요지는 다음과 같다:
    - **지금 가능한 것(완성)**: (a) 단일 소유자 락 + 동적 히트박스 영역 부기(`LocalClickCaptureGate`) — "격파 미니게임과 드래그&던지기가 동시에 캐릭터 클릭을 다투는 상황"을 상태머신 레벨에서 완전히 차단한다(15절-4 충족). (b) **Unity 게임 오브젝트 레벨의 클릭 감지**(`Interaction/StickmanClickHitbox.cs`, `OnMouseDown`/`OnMouseUp`) — Unity가 Camera 기준 Physics2D 히트테스트를 매 프레임 자체 수행하므로 "캐릭터가 움직이면 히트박스도 함께 움직인다"는 동적 추적(15절 제약1)이 별도 폴링 코드 없이 그대로 성립하고, "캐릭터를 클릭하면 앱이 안다"는 지금 실제로 100% 동작한다.
    - **지금 불가능한 것(후속 작업으로 명확히 남김)**: **진짜 OS 레벨 히트테스트**(히트박스 영역 밖 클릭은 100% 관통, 영역 안 클릭만 앱이 수신) — 이건 실제로 "분리된 오버레이 창"(HWND, `CreateWindowEx` 기반)이 존재해야만 구현 가능한데, 그 오버레이는 BUG-B1(Phase 0 Blocker, 아직 미해결)이 막고 있다. 더 정확히 말하면: **지금 Windows/에디터 빌드는 전역 클릭관통 자체가 실제로 켜져 있지 않다** — `Win32WindowService.SetClickThrough()`가 BUG-B1 안전가드로 `NotSupportedException`을 던져(게임 자신의 창이라 클릭관통을 걸면 모든 입력이 막히는 훨씬 나쁜 결과를 방지) 아예 호출을 스킵한다(`StickmanAgent.Start()`가 이 예외를 잡고 로그만 남김, Phase 1부터 이어진 상태). 즉 지금은 "클릭관통이 이미 꺼져 있어서 게임 창이 어디를 클릭해도 항상 입력을 받는" 상태이므로, 이 인터페이스의 Request/Update/Release는 실제 OS 히트테스트를 전혀 바꾸지 않고 **오직 소유권 부기 + 향후 확장 지점** 역할만 한다.
    - **결론**: "캐릭터 히트박스 클릭 감지"(Unity 레벨, 완성)와 "그 외 영역 100% 관통 보장"(OS 레벨, 미완성 — BUG-B1에 종속)은 서로 다른 절반이며, 지금은 앞의 절반만 완성됐다. 격파 미니게임/드래그&던지기가 "지금 당장 실제로 동작하는" 것은 클릭관통이 이미 꺼져 있는 현재 빌드 특성 덕분이지, 부분적 해제 메커니즘이 완성돼서가 아니다 — 진짜 분리 오버레이(BUG-B1)가 구현되고 전역 클릭관통이 실제로 켜지는 순간, 이 두 기능이 "캐릭터 클릭"을 계속 받으려면 `ILocalClickCaptureService` 구현체가 그 오버레이 HWND에 `SetWindowRgn` 또는 `WM_NCHITTEST` 커스텀 처리로 실제 리전을 걸어야 한다 — 지금은 그 지점만 인터페이스로 예약해뒀다(15절 "Phase 1과의 접점" 요구사항 충족).
    - **인질극 역예외(14절)와의 관계**: 인질극의 "닫기 버튼 막기"는 이 메커니즘을 절대 쓰지 않는다는 원칙은 이번 라운드에 인질극 자체를 구현하지 않아 코드 검증 대상은 아니지만, `ILocalClickCaptureService`를 사용하는 4개 후보(격파/라이벌/드래그/로데오) 중 실제로 구현한 것은 격파와 드래그뿐이고 라이벌은 애초에 이 인터페이스를 참조하지 않으며(관전 전용, 위 표 참고) 로데오도 참조하지 않는다(클릭 불필요) — "쓰지 말아야 할 곳에서 안 쓴다"는 설계 의도가 코드 구조로 검증 가능하다.
  - **핵심 설계 결정 3 — 4개 스펙터클 이벤트 상호배제를 `SpectacleEventLock`(신규, `Core/SpectacleEventLock.cs`)이라는 별도 락으로 분리.** `ILocalClickCaptureService`의 락(15절-4, "누가 클릭을 가로채는가")과 이 락(16절-15, "누가 지금 스펙터클/개입 이벤트를 점유했는가")은 목적이 다르다 — 로데오 커서는 클릭을 안 쓰므로 전자는 필요 없지만 후자(다른 스펙터클과 동시 발동 금지)는 여전히 필요하다. 오너 토큰은 보통 같은 MonoBehaviour 인스턴스(`this`)를 재사용해 두 락이 항상 같은 생명주기로 해제되게 했다.
  - **핵심 설계 결정 4 — RAGDOLL 강제 전이 판정을 `States/RagdollImpactResolver.cs`(신규 정적 헬퍼)로 통일.** `StickmanAgent.ReportExternalImpact()`(Phase 2, 유일한 진입점)가 MonoBehaviour 인스턴스 메서드라 블랙보드만 가진 순수 클래스(`DragThrowState`/`RodeoCursorState`/`RivalStickmanAgent`)에서 직접 호출할 수 없었다 — 이 판정식("충격량 ≥ ragdollForceThreshold면 Ragdoll")을 세 곳 이상에서 각자 다시 구현하면 값이 어긋날 위험이 있어 하나로 모았다. `StickmanAgent.ReportExternalImpact()`의 **공개 시그니처는 전혀 바뀌지 않았고**(기존 `OnCollisionEnter2D`/`RagdollLimbImpactRelay` 호출부 무수정으로 계속 동작), 내부 구현만 이 헬퍼에 위임하도록 리팩터했다.
  - **UX 31-2 표 #5(격파 미니게임 chargeRatio 대사)에 대한 Architect 조율 요청 — 위 "격파 미니게임" 행에 상세 기재.** 요지만 반복: "릴리즈 확정 순간"이라는 스냅샷 시점이 `Enter()`가 아니라 `Tick()` 도중이라 "DialogueIntent는 오직 Enter() 안에서만 생성 가능"이라는 지금까지의 원칙(9절-1/31-1)과 구조적으로 충돌한다. 표 자체가 이 라운드의 구현 대상이 아니라고 명시했으므로 강행 구현 대신 이벤트(`BattleMinigamePhaseChanged`)만 발행하고 텍스트는 비워뒀다 — "같은 상태 안에서 여러 차례 반복되는 판정 각각에 스냅샷 대사를 붙이는" 일반해가 필요하다면(예: 재도전마다 새 대사) 별도 설계 라운드가 필요하다는 점을 명확히 기록해둔다(30-3 ROLL/RAGDOLL 축 분리 때와 동일하게, Architect 판단 있을 때까지 미해결로 유지).
  - Unity 배치모드 컴파일 검증 2회(증분 1회 + `Library/ScriptAssemblies`/`Bee`/`PlayerDataCache` 강제 삭제 후 클린 재컴파일 1회) 모두 에러 0/경고 0. 기존 Phase 2 EditMode 회귀 테스트(`DialogueTextActionSyncTests`, 8개)도 재실행해 8/8 통과 확인(`AttackState`/`StickmanAgent` 변경이 그 테스트가 검증하는 계약을 깨지 않았음을 재확인) — Test Engineer 정식 리뷰는 위 Phase 3 표 각 행 참고.

- **[Coder, 2026-08-28, Phase 4 완료 보고]** UX_FLOW.md 27절 6개 기능(창 도둑/청소부/그라피티/크래시/블랙홀/PC 하드웨어 반응) 전부 구현 완료(상세는 위 Phase 4 표 각 행 참고). 세션 시작 시점에 Test Engineer가 이미 정적 감사 테스트(`Tests/EditMode/UserAssetImmutabilityAuditTests.cs`, 5건)를 선반영해둔 상태였고, 이번 구현 전체가 그 스캔(디렉터리 전체 훑기 방식이라 파일명 하드코딩 없이 자동 포함됨)에 그대로 걸려 통과함을 최종 컴파일/테스트에서 재확인했다 — 아래 교차 레이어 요지만 정리한다(개별 파일 설계 근거는 각 소스 파일 상단 문서 주석 참고):
  - **설계 결정 1 — 신규 재사용 상태 `States/TimedSpectacleState.cs`.** 그라피티/청소부/블랙홀/크래시(캐릭터 스윙)는 전부 "물리/입력 변경 없이 정해진 시간만 머물다 정상 Idle 복귀"라는 동일한 형태라, `StickmanStateId`별 인스턴스만 다르게(지속시간 선택자 `Func<StickConfig,float>` 주입) 하나의 클래스를 공유한다 — Phase 3에서 상태 4종을 각각 새 파일로 만들던 관행과 달리, 이번엔 "형태가 진짜로 동일한 경우"라 의도적으로 통합했다. 창 도둑만 확정된 실패(2회 시도 소진)에서 대사를 파생해야 해서(원칙 1) `BattleMinigameState`와 동일한 self-transition 패턴을 쓰는 전용 `States/WindowTheftState.cs`로 남겨뒀다.
  - **설계 결정 2 — 청소부/블랙홀 "복제 스프라이트" 공용 파이프라인을 신규 `Platform/IDesktopIconLayoutService.cs` + `Interaction/DesktopIconMirrorDirector.cs` 하나로 통합(28절-25 권고 그대로).** `ICursorPositionService`/`ILocalClickCaptureService`와 동일한 이유로 `IPlatformWindowService`에서 분리했고, `NullPlatformWindowService`/`Win32WindowService`/`FallbackPlatformWindowService` 3개 구현체 모두에 델리게이트 패턴을 그대로 재사용해 새 캐스팅 관례를 만들지 않았다. **Win32 구현은 정직한 미구현 스텁**(`TryGetIconRegion` 항상 false)이다 — 실제 아이콘 좌표는 Progman→SHELLDLL_DefView→SysListView32에 `LVM_GETITEMPOSITION`을 보내되 응답이 탐색기 프로세스 메모리에 있어 `VirtualAllocEx`/`ReadProcessMemory` 기반 크로스 프로세스 IPC가 추가로 필요한데, 이 개발 환경(Unity 배치모드가 macOS에서 실행)에는 검증할 실제 Windows 하드웨어가 없다. BUG-B1(진짜 오버레이 미구현)/macOS 네이티브 플러그인 미구현(BUG_REPORT_PHASE0.md m8)과 동일 계열의 "정직한 커버리지 공백"으로 판단해, 검증 불가능한 크로스 프로세스 코드 대신 안전한 no-op(트리거만 조용히 억제)을 택했다 — 후속 작업으로 명확히 이월한다. 에디터(`NullPlatformWindowService`)는 합성 아이콘 그리드를 반환해 오버레이 파이프라인/취소 판정 로직 자체는 지금도 완전히 검증 가능하다.
  - **설계 결정 3 — 27-2/27-5의 "클릭 가로채기 없이 스스로 취소" 요구(28절-27)를, 실제 전역 클릭 상태 조회 API가 이 프로젝트에 없다는 제약 위에서 "커서 좌표가 캡처 영역에 진입하는 것 자체"를 활동 근사 신호로 채택해 구현했다.** 진짜 클릭/더블클릭 이벤트가 아니라 좌표 진입만으로 취소하므로 실제 클릭보다 더 이르게(더 보수적으로) 취소되는 방향으로만 어긋날 수 있다 — 원칙 2/3(비침해/유저 자산 불변)을 침해하는 방향의 오차가 아니라는 점을 `DesktopIconMirrorDirector.cs` 문서 주석에 명시했다. Debugger 검토 시 이 근사 판단 자체에 대한 이견이 있으면 알려달라.
  - **설계 결정 4 — 윈도우 크래시(27-4)의 크랙 오버레이 수명을 캐릭터 상태(`StickmanStateId.WindowCrash`, 스윙만) 수명과 의도적으로 분리했다.** UX 원문이 "3초 뒤 오버레이만 사라짐"이라 명시했는데 해머 스윙 자체는 훨씬 짧아야(순간적) 자연스럽다고 판단 — 스윙이 끝나 캐릭터가 Idle로 복귀한 뒤에도 크랙은 `WindowCrashDirector`의 독립 타이머가 3초를 다 채울 때까지 `SpectacleEventLock`을 계속 쥔 채 남아 있는다(다른 4개 Director가 "캐릭터 상태 이탈=락 해제"인 것과 다른 유일한 지점, 근거를 파일 상단 문서 주석에 명시). Architect/Debugger 확인 요청 — UX 원문에 "스윙도 3초여야 한다"는 명시가 없어 이렇게 해석했지만, 만약 스윙 자체가 3초 내내 유지되길 원했다면 반려 바람.
  - **설계 결정 5 — PC 하드웨어 반응(27-6)에는 의도적으로 `SpectacleEventLock`을 쓰지 않았다.** 리더 작업 지시 원문은 "기존 4개 스펙터클과도 상호배제"를 6개 기능 공통 원칙으로 제시했지만, `docs/UX_FLOW.md` 28절-29(교차 레이어 영향 로그)는 상호배제 세트를 "27-1~27-5"로만 명시적으로 한정하고 있고 23절은 하드웨어 반응을 "idle 자세 변형" 수준의 훨씬 가벼운 반응으로 별도 규율(지속조건/쿨다운/회복게이트/우선순위 1개 표현)한다 — 더 상세한 UX 문서 쪽을 우선했다. **Architect 확인 요청**: 이 해석이 틀렸다면(즉 하드웨어 반응도 격파/라이벌/드래그/로데오와 동시 발동을 막아야 한다면) `HardwareReactionDirector.ResolveAndNotify()`에 `if (SpectacleEventLock.IsActive) return;` 한 줄만 추가하면 되는 저비용 변경이다.
  - Unity 배치모드 컴파일 검증 2회(증분 1회 + `Library/ScriptAssemblies`/`Bee`/`PlayerDataCache` 강제 삭제 후 클린 재컴파일 1회) 모두 에러 0/경고 0. EditMode 테스트 재실행 결과 `total="13" passed="13" failed="0"`(기존 `DialogueTextActionSyncTests` 8건 + Test Engineer가 선반영한 `UserAssetImmutabilityAuditTests` 5건, 신규 Phase 4 코드 전체가 그 정적 감사 스캔에 포함되어 통과함을 확인) — Test Engineer의 **런타임 동작 검증(크랙 3초 내내 클릭관통, 복제 스프라이트 아래 실제 아이콘 더블클릭 정상 동작 등, PlayMode/수동 QA)은 여전히 필요**(위 Phase 4 "실제 파일/창 미변경 감사 테스트" 행의 Test Engineer 본인 메모에도 명시되어 있음) — Coder 구현 완료를 알리며 정식 리뷰 요청.
  - **[Debugger → Coder/Architect, 2026-08-28, BUG-P4-M1(Major)]** `HardwareReactionDirector`의 `TickBattery`/`TickCharging`/`TickNetwork`(`Interaction/HardwareReactionDirector.cs:73-139`) 3곳이 `UpdateSignalLifecycle(state, sustainedNow, dt, cooldownSeconds)` 호출 시 마지막에서 두 번째 인자로 그 신호의 실제 폴링 간격(배터리 90초/충전 30초/네트워크 20초 기본값)이 아니라 그 프레임 하나의 `Time.deltaTime`(`dt`, ~0.016초)을 그대로 넘긴다 — `TickCpu`(:103-124)만 `elapsedThisSample`(그 샘플 구간의 실제 경과시간)을 올바르게 넘겨 형태가 다르다. `UpdateSignalLifecycle`의 `RecoveryCooldownRemaining -= dt`(:160)가 이 값으로 감소하므로, 기본 쿨다운 420초(7분)가 실제로 소진되려면 배터리 약 26일/충전 약 8.7일/네트워크 약 5.8일이 걸린다 — 27-6절 "회복 확인 후 쿨다운 경과 시에만 재알림" 요구사항이 사실상 "최초 1회만 알림, 이후 그 세션 내내 영구 침묵"으로 깨진다. 정적 스캔 대상이 아니고 전용 EditMode 테스트도 없어 자동화로는 검출되지 않는 은닉 버그(수 시간~수일 단위 드라이프 세션에서만 관찰 가능). **수정 제안**: 3곳 모두 `dt` 대신 각 함수가 이미 로컬로 갖고 있는 `interval`(폴링 주기 로컬 변수)을 넘기면 됨 — `TickCpu`의 기존 패턴과 동일한 한 줄 수정. 상세: `docs/BUG_REPORT_PHASE4.md` BUG-P4-M1.
  - **[Debugger → Architect, 2026-08-28, Minor 1, 참고]** 위 "설계 결정 5" 문단과 별개로, `HardwareReactionDirector.ResolveAndNotify()`(:169-198)의 우선순위(배터리>CPU>네트워크>충전) 적용 범위에 대해서도 확인 요청: 현재 구현은 "여러 신호가 동시에 새로 충족되는" 순간에만 우선순위 순서로 후보를 고르고, 이미 낮은 우선순위가 표시 중일 때 더 높은 우선순위가 나중에 충족돼도 선점(preempt)하지 않는다(코드 주석에 "표현 전환 시 깜빡임 방지"로 판단 근거 명시). CPU 과부하와 네트워크 끊김처럼 충분히 동시 발생 가능한 조합에서 낮은 우선순위가 높은 우선순위를 오래 가릴 수 있다 — 위험도가 낮아 버그로 집계하지 않았으나(Minor 1, `docs/BUG_REPORT_PHASE4.md`), 다음 라운드 착수 전 이 해석 확정 요청.

- **[Coder, 2026-08-28] Phase 5(생산성/스트레스·반항) 설계 결정 정리 — Architect/Debugger 확인 요청.** 지시문 "절대 원칙"이 명시적으로 요구한 대로 SpectacleEventLock 적용 여부 판단 근거를 여기 남긴다.
  - **설계 결정 1 — SpectacleEventLock 참여 기준을 "ChangeState()를 직접 호출해 단일 상태 슬롯을 다투는가"로 통일했다.** `HardwareReactionDirector`(Phase 4, 설계 결정 5)가 이 락을 쓰지 않는 이유가 "ChangeState를 전혀 호출하지 않는 순수 오버레이 신호"이기 때문이라는 선례를 그대로 일반화한 것이다. 이 기준에 따라 `TodoReminder`/`FocusStart`/`FocusComplete`/`FocusCancelled`/`FocusNudge`(공용 kind `FocusPose`)/`Sulky`/`Runaway`는 전부 `Core/SpectacleEventLock.cs`의 `SpectacleEventKind`에 참여시켰다(전부 실제 `ChangeState()` 호출). 반대로 `Core/StressGauge.cs`(값 보관+이벤트 발행만, `ChangeState` 호출 0건)와 포모도로 1/3단계(Glance/WindowTap, 순수 앰비언트 이벤트 `FocusWatchTierChanged`만 발행하고 상태 전이 없음)는 이 락과 무관하다. `Runaway`는 UX_FLOW.md 25절-20이 애초에 명시적으로 요구한 항목이라 이 기준과 무관하게도 포함이 확정적이었다.
  - **설계 결정 2 — 포모도로 감시자(18절)의 "딴짓 감지" 1차 신호(전경 창 포커스 전환 빈도)는 신규 OS 폴링을 전혀 추가하지 않고, 이미 `StickmanBlackboard.FootholdPoller`가 `StickConfig.footholdPollInterval` 주기로 채우는 캐시의 `PlatformFoothold.IsTopmost`를 관찰해 얻었다.** 이 값은 `Win32WindowService.OnEnumWindow()`가 `GetForegroundWindow()`로 이미 계산해두던 것이라(전체화면 감지 `IsFullscreenAppActive()`와는 별개의 호출이지만 같은 API), `StickmanEventBus.FootholdsChanged`(발판 캐시가 바뀔 때만 발행)를 구독해 그 순간의 최상단 실제 핸들(`Handle>=0`, `FallbackPlatformWindowService` 안전망 합성 발판 제외)이 직전과 달라졌는지만 비교한다. 지시문이 "`IsFullscreenAppActive` 관련 폴러 재사용 가능한지 검토"라고 콕 집었던 그 폴러 자체(1.5초 간격, 불리언만 반환)는 "어느 창인지" 식별할 수단이 없어 문자 그대로는 재사용 불가능했고, 대신 UX_FLOW.md 25절-16 원문 요구사항("기존 전경 창 감지 인프라 재사용, 신규 상시 폴링 금지")을 더 정확히 만족하는 대안(같은 프로세스가 이미 계산해 캐싱해둔 값을 이벤트로 통지받기만 함, 신규 P/Invoke 0건)을 택했다. 이견 있으면 반려 바람.
  - **설계 결정 3 — 가출(20절) 은신 중 캐릭터를 `Rigidbody2D.simulated=false`가 아니라 `bodyType=Kinematic`으로 전환했다.** `StickmanAgent.Suspend()`(전체화면 은닉)는 `simulated=false`를 쓰지만, 이 프로젝트에서 그 경로는 "유저가 클릭할 수 없는 상황"(전체화면 다른 앱이 화면을 덮음)에서만 쓰인다. 가출의 "찾기" 상호작용(20절)은 정반대로 "숨어 있는 캐릭터를 유저가 클릭해서 찾는" 것이 핵심 메커니즘인데, Unity의 `Rigidbody2D.simulated=false`는 해당 콜라이더를 Physics2D 쿼리(레이캐스트 포함) 대상에서 제외할 수 있어 `Interaction/StickmanClickHitbox.cs`의 `OnMouseDown`(카메라 레이캐스트 기반)이 죽을 위험이 있었다 — 이 프로젝트에 실제로 Play 모드에서 이 조합을 검증할 씬이 아직 없어(Directors가 소스 형태로만 존재, 아래 참고) 실측으로 확인하지 못했고, 위험을 감수하는 대신 이미 안전이 증명된 전례(`States/DragThrowState.cs`가 드래그 중 Kinematic을 쓰며 정상 동작 확인됨)를 그대로 채택했다. 물리적으로는 "얼어붙어 있지만 여전히 클릭 가능한 콜라이더"가 되어 "찾기" 요구사항을 만족한다.
  - **설계 결정 4 — 투두 포스트잇(17절)의 uGUI Canvas를 지원하기 위해 `Packages/manifest.json`에 `com.unity.ugui: 2.0.0`을 신규 추가했다.** 프로젝트에 `UnityEngine.UI`/`UnityEngine.EventSystems`가 없어(기존 manifest에 `com.unity.modules.ui`만 있고 `com.unity.ugui`는 부재 확인) 컴파일이 불가능한 상태였다 — 공식 Unity Editor 내장 패키지(Editor의 `BuiltInPackages` 디렉터리에 이미 존재, `com.unity.modules.ui`/`com.unity.modules.imgui`에만 의존)를 추가한 것으로, "유저 실제 파일/OS 설정 변경 금지" 원칙과는 무관한 프로젝트 자체 의존성 선언이다. 추가 후 Unity 배치모드 클린 컴파일로 정상 해석/컴파일 확인 완료.
  - **알려진 한계, 정직하게 문서화** — 이번 라운드는 Phase 0~4와 동일하게 소스 파일만 작성했고 씬/프리팹 조립은 하지 않았다(CLAUDE.md "환경 메모" 컨벤션 그대로). 따라서 `Interaction/TodoPostItWidget.cs`의 실제 uGUI 클릭 판정(Play 모드에서 Button.onClick이 실제로 발동하는지), `Interaction/RunawayDirector.cs`의 은신 중 `OnMouseDown` 재발동 여부(위 설계 결정 3의 실측 검증), `Interaction/FocusWatchDirector.cs`의 실제 포커스 전환 카운팅 정확도는 전부 **런타임 동작 검증(PlayMode/수동 QA) 대상으로 남아있다** — Phase 4 "실제 파일/창 미변경 감사 테스트" 행에서 Test Engineer가 남긴 것과 동일한 성격의 한계다.
  - **범위 확정 — 던전 파밍/세포분열(21/22절)은 이번 라운드에 전혀 건드리지 않았다.** Architect가 이미 보류(P3)로 확정한 결정을 그대로 존중했으며, 해당 행/UX 설계 완료 행 모두 무수정이다.
  - Unity 배치모드 컴파일 검증(전체 재컴파일, `com.unity.ugui` 신규 패키지 해석 포함) 에러 0/경고 0, EditMode 테스트 재실행 `total="13" passed="13" failed="0"` 확인 — Coder 구현 완료를 알리며 정식 리뷰 요청.

- **[Debugger, 2026-08-28, Phase 5 검토, Major 2건 발견 — Coder로 반려 필요]** `docs/BUG_REPORT_PHASE5.md` 작성 완료(커밋 `3a22ff2` 전면 검토, Unity 배치모드 클린 재빌드 1회 + EditMode 재실행 — 에러0/경고0, 13/13 통과 직접 재확인). 리더 지시 중점 점검 6개 항목 결론 요지:
  - **점검 1(Kinematic 채택 근거) — 실측 없이도 정적 검증 가능, Coder 판단 정확함을 확인.** `Rigidbody2D.simulated=false`가 Physics2D 쿼리(레이캐스트 포함) 대상에서 콜라이더를 제외시킨다는 것은 Unity 공식 문서 기준 사실이고, `Interaction/StickmanClickHitbox.cs` 자신의 문서 주석도 "OnMouseDown은 엔진이 Camera 기준 Physics2D 히트테스트를 매 프레임 자체 수행"이라고 명시한다 — `Kinematic`을 택한 것이 이론적으로 맞다. Kinematic의 "타 Rigidbody2D 충돌 무시" 부작용은 20/24절 상호배제 세트가 가출 중 다른 스펙터클(충돌 유발원) 발동 자체를 막아 실질 위험이 낮음도 확인.
  - **점검 2(자동복귀 타이머) — Suspended 중 Tick과 함께 정확히 정지/재개됨을 확인, 다만 조사 중 별개의 실제 버그 발견.** `RunawayState`는 `Suspend()`의 강제 Idle 목록에 없어 Tick 자체가 항상 정상 호출됨(Suspended 제외)을 코드로 확인 — "영원히 숨어있는" 회귀는 없다. 그러나 `Resume()`(`Core/StickmanAgent.cs:293-302`)이 무조건 `SetRenderersEnabled(true)`를 호출해, Hidden 페이즈 중 Suspend/Resume 왕복이 한 번이라도 발생하면 아직 발견 안 된 캐릭터가 강제로 노출되는 **BUG-P5-M1(Major, 신규)**을 발견했다 — `RunawayState`가 도입한 독립 가시성 상태를 `Suspend()/Resume()`이 전혀 모르는 것이 근본 원인. 상세 재현 경로/수정 제안: `docs/BUG_REPORT_PHASE5.md`.
  - **점검 3(긴급정지 다중 구독자) — 8/9개 정확히 격리, 1개 예외.** `GlobalEmergencyStopRequested` 구독 9개 Director 전수 확인 — `RunawayDirector` 포함 8개는 `SpectacleEventLock` 소유권(또는 `CurrentStateId`) 가드로 자기 소관 아니면 즉시 no-op(로데오 중 긴급정지를 눌러도 `RunawayDirector`는 반응 안 함, 확인). `StressGaugeDirector`는 의도적으로 무가드(긴급정지 반복 빈도 자체를 추적해야 함). `FocusWatchDirector`만 무가드로 무조건 `IsSessionActive=false` 실행 — 무관한 긴급정지에도 진행 중이던 Pomodoro가 함께 취소되는 부작용(Minor 2, Architect 확인 요청).
  - **점검 4(스트레스 4트리거) — 논리적 상충 없음(방치=증가만/비방치=감소만 단일분기, 설계 정합적), 트리거 간 이중가산도 없음(독립 카운터).** 다만 `RecordOveruseEntry()`가 임계값 초과 후에도 후속 이벤트마다 계속 가산하는 방식이라 짧은 시간에 몰아쓰면 게이지가 급속 포화될 수 있음 — 의도된 에스컬레이션인지 불명확(Minor 1, Architect 확인 요청).
  - **점검 5(FocusWatchDirector의 FootholdsChanged 재사용) — 타당함, 폴링 규율 위반 없음.** `Platform/FootholdPoller.cs:96-109`의 `HasChanged()`가 `IsTopmost`까지 항목별 비교함을 확인 — 창 목록이 안 바뀌어도 포그라운드만 바뀌면 이벤트가 발행되어 "포커스 전환"을 정확히 근사한다. 이벤트 기반(폴링 주기가 찰 때만 발행)이라 매 프레임 호출 금지 규율도 지킨다.
  - **점검 6(TodoPostItWidget 독립성) — 참조 0건, 중복생성 경로 없음 확인.** `SpectacleEventLock`/`ILocalClickCaptureService`/`StickmanClickHitbox` 실참조 0건(grep, 문서 주석에만 텍스트로 등장) 재확인. `EnsureEventSystem()`의 `EventSystem.current != null` 가드 확인, 프로젝트 전체에서 EventSystem 생성 코드는 이 한 곳뿐(중복 생성 경로 없음).
  - **BUG-P5-M2(Major, 신규) — UX 24절 1단계 요구사항 미구현이 "구현 완료"로 보고됨.** 이 로그 위쪽 Coder 항목(및 위 Phase 5 표 행)이 "UX 19/20/24절 구현 완료"라 명시했으나, 25절-21(line 856)이 요구한 "13/14절(로데오/인질극)에 스트레스 발동확률 가중치 연결"은 로데오(현재 구현된 유일한 대상, 인질극은 아직 없음)에 전혀 반영되지 않았다 — `Interaction/RodeoCursorWatcher.cs`는 이번 커밋에서 수정되지 않았고 `StressGauge` 참조 0건(grep 확인). 이 갭은 같은 Coder 로그가 다른 항목(TodoPostItWidget PlayMode 미검증 등)은 "알려진 한계"로 정직하게 남긴 것과 달리 어디에도 기록되어 있지 않다.
  - 두 Major 항목 모두 크래시/데이터손상은 아니며 기존 안전망(자동복귀/긴급정지/실클릭)으로 결국 회복되나, **Coder로 반려 필요** 판정. 수정 후 Phase 6(성능점검/최종리뷰/문서화) 착수 가능 — 이번 라운드도 새 아키텍처 패턴 없이 기존 컨벤션 재사용만으로 구현되어 구조적 위험은 낮다.

> 가설 → 검증방법 → 결과 → 결론 순으로 기록. 아래는 Phase 0 정적 검토만으로는 확답할 수 없어 가설로 남긴 항목(실측 필요) — 전체 근거는 `docs/BUG_REPORT_PHASE0.md` 하단 참고.

- **[Debugger, 2026-08-27] 가설 H1**: `Win32WindowService`가 (BUG-B1 수정 전 상태로) Unity의 실제 렌더링 창(DXGI/OpenGL 스왑체인이 붙은 창)에 `WS_EX_LAYERED`를 직접 걸면, DWM 합성 방식과 충돌해 화면이 멈추거나 검게 나올 수 있다.
  - **검증 방법**: Windows Standalone 빌드에서 `CreateOverlayWindow()` → `SetClickThrough(true)` 호출 후 게임 화면이 정상적으로 계속 렌더링되는지 육안 확인, `Player.log`에 DXGI/GL 관련 오류가 남는지 확인.
  - **결과/결론**: (다음 라운드에서 Windows 실빌드 확보 후 채움)
- **[Debugger, 2026-08-27] 가설 H2**: `WS_EX_NOACTIVATE`가 빠져 있어(`Win32WindowService.cs` 상수 목록/`SetClickThrough` 모두 미적용), 클릭관통이 켜진 상태에서도 오버레이(현재는 게임 창 자체)가 간헐적으로 OS 포그라운드 포커스를 가져가 사용자가 다른 앱에 입력 중인 포커스를 뺏을 수 있다.
  - **검증 방법**: 별도 텍스트 에디터에 타이핑하며 `SetClickThrough(true)`/`SetAlwaysOnTop(true)`를 반복 토글해, 타이핑이 끊기거나 포커스가 게임 창으로 전환되는지 확인.
  - **결과/결론**: (다음 라운드에서 채움)
- **[Debugger, 2026-08-27] 가설 H3**: Windows 프로세스의 DPI 인식 설정에 따라 `GetWindowRect`가 반환하는 좌표가 물리 픽셀/논리(가상화) 픽셀 중 무엇인지가 달라져, 고DPI 또는 배율이 다른 멀티모니터 환경에서 발판 좌표가 실제 픽셀 위치와 어긋날 수 있다.
  - **검증 방법**: Unity 플레이어의 DPI 인식 설정(Player Settings/매니페스트) 확인 + 배율 150%/200% 모니터에서 알려진 위치의 창에 대해 `GetWindowRect` 반환값을 실측값과 비교.
  - **결과/결론**: (다음 라운드에서 채움)
- **[Debugger, 2026-08-27] 가설 H4**: `CreateOverlayWindow()`가 앱 기동 직후(첫 프레임 이전) 호출되면 `Process.GetCurrentProcess().MainWindowHandle`이 아직 OS에 등록되지 않아 `IntPtr.Zero`를 반환하고, 재시도 로직이 없어 오버레이 관련 기능이 영구 비활성 상태로 남을 수 있다.
  - **검증 방법**: 실제 기동 시퀀스에서 `CreateOverlayWindow()` 반환값을 로그로 남겨 호출 타이밍(Awake vs 첫 프레임 이후)에 따라 결과가 달라지는지 확인.
  - **결과/결론**: (다음 라운드에서 채움)
- **[Debugger, 2026-08-27, 2차 리포트] 가설 H5**: 표준 Windows 데스크톱에서 유저가 열려 있는 모든 앱 창을 최소화하면, `Win32WindowService.OnEnumWindow`의 필터(`IsWindowVisible` + `GetWindowTextLength != 0`)를 통과하는 창이 실제로 0개가 된다 — 작업표시줄(`Shell_TrayWnd`) 자체가 보통 제목 없는 창이라 제외되기 때문이다. `docs/BUG_REPORT_PHASE1.md` BUG-P1-B1(무한 낙하)이 "드문 엣지 케이스"인지 "일상적 결함"인지를 가르는 핵심 전제.
  - **검증 방법**: Windows Standalone 빌드에서 모든 앱 창을 최소화한 뒤 `EnumerateFootholds()` 반환 리스트 길이를 로그로 실측.
  - **결과/결론**: (다음 라운드, Windows 실빌드 확보 후 채움)
- **[Debugger, 2026-08-27, 2차 리포트] 가설 H6**: BUG-B1(Phase 0)이 완전히 해결되어 `CreateWindowEx` 기반 진짜 분리 오버레이(`WS_EX_LAYERED|TRANSPARENT|TOPMOST|NOACTIVATE`)가 구현되면, 그 창은 `WS_EX_NOACTIVATE` 특성상 OS 키보드 포커스를 받을 수 없어 `UnityEngine.Input.GetAxisRaw`/`GetButtonDown`이 항상 0/false만 반환한다 — 즉 `docs/BUG_REPORT_PHASE1.md` BUG-P1-B2(키보드 의존 이동)가 "지금은 우연히 동작하지만 BUG-B1이 올바르게 해결되는 순간 캐릭터 영구 정지"로 현실화됨을 의미한다.
  - **검증 방법**: 진짜 분리 오버레이 구현 완료 후, 그 창에 포커스를 주려는 시도(클릭/Alt-Tab)가 실패하는지, 그 상태에서 WASD/Space 입력 시 `Input.GetAxisRaw`/`GetButtonDown`이 반응하는지 실측.
  - **결과/결론**: (진짜 오버레이 구현 이후 라운드에서 채움)
- **[Debugger, 2026-08-27, Phase 2 리포트] 가설 H7**: `RagdollRig.TickGetup()`의 비례 제어(게인 `getupMotorGain=6`(도/초 per 도 오차), `maxMotorTorque=50`, D항 없는 순수 P 제어)가 실제 캐릭터 프리팹의 관절 관성/댐핑 값과 결합했을 때 목표 각도(직립, 0도) 주변에서 오버슈트-진동을 일으킬 가능성이 있다 — 안정성을 HingeJoint2D 자체의 물리적 댐핑에만 의존하는 구조이기 때문. 실제 프리팹/조인트 배치가 Phase 2 범위 밖이라 정적 검토로는 확답 불가(`docs/BUG_REPORT_PHASE2.md` 가설 항목 참고).
  - **검증 방법**: 실제 캐릭터 프리팹(몸통+머리+양팔+양다리, HingeJoint2D 배선) 완성 후 RAGDOLL→GETUP 전이를 반복 트리거해 `_getupProgress`가 1에 도달할 때까지 각 관절의 `jointAngle`이 진동 없이 단조 수렴하는지, 아니면 여러 번 왕복하는지 Play 모드에서 관찰/로그로 확인.
  - **결과/결론**: (실제 프리팹 배선 이후 라운드에서 채움)
- **[Coder, 2026-08-28] `docs/BUG_REPORT_PHASE5.md` 반려 재작업 완료(Major 2건 + Minor 2건).**
  - **BUG-P5-M1(Major)**: `StickmanAgent.Resume()`이 가출 Hidden 페이즈 은신을 무시하고 렌더러를 무조건 복원하던 문제. 수정 제안 (a)(IStickmanState에 `OnSuspendResumed()` 훅 추가)와 (b)(블랙보드 플래그) 중 (b)를 채택 — (a)는 인터페이스 계약을 바꿔 이 버그와 무관한 나머지 10여 개 상태 구현체 전부가 영향 범위에 들어오는 반면, (b)는 `StickmanBlackboard.IsCharacterHiddenByRunaway` 필드 하나만 추가하고 `RunawayState`/`StickmanAgent.Resume()` 두 파일만 건드리면 되어 더 침습적이지 않다고 판단(BUG_REPORT_PHASE5.md도 두 안 모두 제시하며 최소 침습 쪽 선택을 요청). 관련 파일: `Assets/_Project/Scripts/States/StickmanBlackboard.cs`, `Assets/_Project/Scripts/States/RunawayState.cs`, `Assets/_Project/Scripts/Core/StickmanAgent.cs`.
  - **BUG-P5-M2(Major)**: UX 24절 1단계(로데오 발동확률 스트레스 가중치) 미구현. `StickConfig`에 `stressRodeoWeightThreshold`(0.6)/`rodeoStressTriggerSecondsMultiplier`(0.7) 추가, `RodeoCursorWatcher`가 스트레스 임계값 이상일 때 `rodeoStillTriggerSeconds`를 완만히 단축하는 방식으로 반영(확률 승수가 아니라 시간 단축 — "약한 가중치" 지시를 과하지 않게 해석). 관련 파일: `Assets/_Project/Scripts/Core/StickConfig.cs`, `Assets/_Project/Scripts/Interaction/RodeoCursorWatcher.cs`.
  - **Minor 1**: `StressGaugeDirector`의 과다사용 반복 가산은 Architect 결정으로 의도된 에스컬레이션 확정, 코드 변경 없음 — 위 Phase 5 표 "스트레스 게이지 / 가출" 행에 설계 의도 기록.
  - **Minor 2**: `FocusWatchDirector.OnEmergencyStop()`이 무관한 긴급정지에도 항상 Pomodoro를 취소하던 문제. 구독 제거 대신 `SpectacleEventLock.IsActive && CurrentOwner != this`일 때만 무시하도록 좁힘 — 18절의 "긴급정지도 Pomodoro의 항상 유효한 탈출구" 요구는 보존하면서, 로데오 등 무관한 이벤트를 끌 때만 발생하는 부작용을 제거(판단 근거는 위 Phase 5 표 "투두 말풍선 / 포모도로 감시자" 행 참고). 관련 파일: `Assets/_Project/Scripts/Interaction/FocusWatchDirector.cs`.
  - **검증**: Unity 배치모드 컴파일 `error CS`/`warning CS` 매치 0건 확인, 이어서 `-runTests -testPlatform EditMode` 재실행 후 `testResults.xml` 직접 파싱 — `testcasecount="13" result="Passed" total="13" passed="13" failed="0"`. 에러0/경고0 + 13/13 통과 기준선 유지 재확인 완료.

---

## 개선 R2 — `SpectacleEventLock` 해제 보일러플레이트 공용 헬퍼 추출 (리뷰어 반려 재작업)

| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| `SpectacleEventLock` 해제 3단계 보일러플레이트 공용 헬퍼 추출 | Coder | 완료 | 아래 상세 |

- **배경**: `docs/CODE_REVIEW_FINAL.md`가 유일한 "심각한 DRY 위반"으로 반려 — 12개 `Interaction/*` Director가 `OnDisable()`(및 일부는 `OnEmergencyStop()`)에서 "소유권 확인 → (필요 시) 강제 Idle 전이 → `SpectacleEventLock.Release()`(+해당 시 `ILocalClickCaptureService.ReleaseLocalClickCapture()`)" 3단계를 각자 손으로 복붙해온 것. Phase 3 `BUG-P3-M1`으로 이미 한 번 지적됐던 문제가 이후 Phase 4/5에서 8개 Director가 더 늘며 재생산됨(리뷰 문서 원문 근거).
- **12곳 전수 정독 결과 — 겉보기와 다른 실사용 편차 3가지를 먼저 확인**:
  1. **소유권 사전 확인 유무가 갈렸다.** 9곳(GraffitiDirector/TodoReminderDirector/RunawayDirector/WindowTheftDirector/DesktopIconMirrorDirector/RodeoCursorWatcher/StressGaugeDirector/FocusWatchDirector/RivalEncounterDirector)은 `if (SpectacleEventLock.CurrentOwner != this) return;`를 먼저 확인했지만, **BattleMinigameDirector/DragThrowController/WindowCrashDirector 3곳은 이 확인 없이 곧장 상태 비교만 하고 있었다.** 세 파일의 호출부를 전부 추적해 "`SpectacleEventLock.TryAcquire()` 성공 직후에만 guardedState로 `ChangeState()`한다"는 불변식이 예외 없이 지켜짐(다른 어떤 경로도 그 상태로 전이하지 않음)을 확인했고, 그 결과 `CurrentStateId==guardedState`이면 항상 그 director 자신이 소유자이기도 하다는 사실을 확인했다 — 헬퍼에 소유권 확인을 항상 포함시켜도(이 3곳엔 원래 없던 가드를 추가하는 셈이지만) 관찰 가능한 동작은 바뀌지 않는다고 판단해 단일 헬퍼로 통합했다.
  2. **강제 전이 대상/방식은 12곳 전부 `StickmanStateId.Idle` + `isForcedInterrupt: true`로 예외 없이 동일했다** — 헬퍼에서 고정값으로 박아 파라미터를 늘리지 않았다(과설계 방지).
  3. **클릭캡처 해제(`ILocalClickCaptureService.ReleaseLocalClickCapture()`)는 12곳 중 BattleMinigameDirector/DragThrowController 2곳에만 있었다** — 헬퍼에 `ILocalClickCaptureService clickCapture = null` 옵션 파라미터로 흡수, 나머지 8곳은 인자를 생략(no-op).
- **추가한 헬퍼**: `Assets/_Project/Scripts/Core/SpectacleEventLock.cs`에 `public static void ReleaseIfOwned(object owner, StickmanStateMachine machine, StickmanStateId guardedState, ILocalClickCaptureService clickCapture = null)` 추가. `using StickMate.Platform;` / `using StickMate.States;`를 새로 추가했으나 순환 참조 아님 — 프로젝트 전체가 `StickMate.Runtime.asmdef` 단일 어셈블리이고 `Core/StickmanAgent.cs`가 이미 States/Platform을 참조하는 선례가 있어 안전.
- **10곳 교체 완료**: GraffitiDirector, TodoReminderDirector, RunawayDirector, WindowTheftDirector, DesktopIconMirrorDirector(`TargetStateId` 동적 프로퍼티를 그대로 인자로 전달), RodeoCursorWatcher, BattleMinigameDirector, DragThrowController, StressGaugeDirector(`ReleaseOwnedSulkyLock`), WindowCrashDirector(`ReleaseOwned` — 크랙 오버레이 3초 수명 정리(`_overlayActive`/`RaiseOverlay(Cancelled)`)는 이 컨트롤러만의 고유 부수효과라 헬퍼 밖에 그대로 남기고, 공통 3단계만 헬퍼 호출로 대체). Graffiti/WindowTheft/DesktopIconMirror의 `_hasRegion`/`_hasTarget` 로컬 필드 리셋도 director 전용 부수효과라 헬퍼가 관여하지 않게 하고 헬퍼 호출 직전 줄에 그대로 남겼다(원래는 `ChangeState`와 `Release` 사이에서 실행됐지만, 그 사이 구간에 해당 필드를 읽는 콜백이 없음 — `OnStateTransitioned` 구독이 헬퍼 호출 전에 이미 해제돼 있음 — 실행 순서를 바꿔도 동작은 동일).
- **2곳은 리뷰어가 문서에서 직접 예외로 승인한 지점이라 헬퍼 미적용, 코드에 근거 주석 추가**:
  - `RivalEncounterDirector.ReleaseOwnedLock()`: 상태 비교가 아니라 `_rival?.ForceEndDuel()`(대결 상대의 별도 상태머신 정리) 경유라 `guardedState` 개념 자체가 없음.
  - `FocusWatchDirector.ReleaseOwnedLock(bool forceIdle)`: 단일 상태가 아니라 `IsFocusPoseState()`로 4개 상태(FocusStart/FocusComplete/FocusCancelled/FocusNudge) 중 하나인지 확인하는 커스텀 가드 — 이 한 곳을 위해 헬퍼 시그니처에 predicate delegate 파라미터를 추가하면 추상화 비용이 절감분보다 크다고 판단(참고: 코드 확인 결과 `forceIdle` 인자는 실제로는 두 호출부 모두 `true`만 전달해 `false` 분기가 죽은 코드지만, 리팩터링 범위를 헬퍼 추출로 한정하기 위해 이 메서드 자체는 손대지 않았다).
- **검증**: Unity 배치모드 컴파일 재실행(`compile_r2.log`) — `error CS`/`warning CS` 매치 0건, exit code 0. `-runTests -testPlatform EditMode` 재실행(`testresults_r2.xml`) — `testcasecount="13" result="Passed" total="13" passed="13" failed="0"`. 에러0/경고0 + 13/13 기준선 유지 확인(리팩터링이 기존 동작을 깨지 않았다는 핵심 증거 — 순수 구조 변경, 신규 기능/동작 변경 없음).

---

## 씬/프리팹 배선 — Phase 0~6 코드 레이어를 실제 씬/프리팹으로 처음 배선 (README.md "빌드/실행 방법" 3번 항목 해소)

| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| `DefaultStickConfig.asset` 생성 | Coder | 완료 | 아래 상세 |
| `Stickman.prefab`(Rigidbody2D+HingeJoint2D 리그, 코드 생성 스프라이트) | Coder | 완료 | 아래 상세 |
| `Main.unity`(카메라+캐릭터 인스턴스, 빌드 설정 등록) | Coder | 완료 | 아래 상세 |
| 배치모드 PlayMode 스모크 테스트로 실측 검증 | Coder | 완료 | 아래 상세 — 실측 로그 포함 |

- **배경**: Phase 0~6은 `Assets/_Project/Scripts/`의 순수 C# 게임로직만 구현하고 씬(`.unity`)/프리팹(`.prefab`)이 하나도 없는 상태로 남겨져 있었다(README.md "빌드/실행 방법" 3번 항목에 명시적으로 기록됨). 이번 작업은 그 코드가 실제로 Rigidbody2D/Update 루프 위에서 동작하는지 처음으로 검증하는 것이 목적이다.
- **신규 에셋**:
  - `Assets/_Project/Data/DefaultStickConfig.asset` — `StickConfig` 인스턴스. 필드 대부분은 코드 기본값 그대로 두었고, `groundSnapTolerance`만 6→20으로 조정(사유는 아래 "실측으로 발견한 이슈" 참고).
  - `Assets/_Project/Data/Sprites/{RectSprite,CircleSprite}.asset` — 실제 아트 에셋이 없어 `Texture2D.SetPixels32`+`Sprite.Create`로 코드 생성한 흰 사각형/원 스프라이트(64x64, PPU=64 → 스케일 1로 세계단위 1x1). `AssetDatabase.CreateAsset`+`AddObjectToAsset`으로 영구 에셋화해(런타임 전용 `Sprite.Create` 결과를 프리팹이 임베드에 의존하지 않도록) 재실행/재생성 시에도 안정적으로 재사용된다.
  - `Assets/_Project/Prefabs/Stickman.prefab` — 루트(`Rigidbody2D` Dynamic + `CapsuleCollider2D` + `StickmanClickHitbox` + `StickmanAgent`, `_config`는 `SerializedObject`로 위 asset 배선) + 자식 6개: Torso/Head(시각 전용, Head만 작은 `CircleCollider2D` 보유 — Rigidbody2D가 없어 루트의 compound collider로 자동 합산됨), LeftArm/RightArm/LeftLeg/RightLeg(각각 독립 `Rigidbody2D`+`HingeJoint2D`, `connectedBody`=루트, `autoConfigureConnectedAnchor=false`로 anchor/connectedAnchor를 수동 고정해 초기 배치와 정확히 일치시킴 — 초기 구속 오차 0). **팔다리에는 의도적으로 Collider2D를 붙이지 않았다** — 몸통/팔다리 콜라이더가 상시 물리 시뮬레이션 중(걷기 등 RAGDOLL이 아닌 상태에서도 4개 Rigidbody2D가 계속 시뮬레이션됨) 서로 겹쳐 충돌 판정을 일으켜 캐릭터가 떨리거나 튕겨나가는 것을 원천 차단하기 위함(스프라이트는 조인트 anchor 계산에 스케일이 관여하지 않도록 별도 "Visual" 자식에서만 스케일).
  - `Assets/_Project/Scenes/Main.unity` — Main Camera(직교, orthographicSize=20, 이유는 아래) + Stickman 프리팹 인스턴스 1개(카메라 뷰포트 상단 가장자리 0.3유닛 위에서 낙하 시작). `EditorBuildSettings.scenes`에도 등록 완료(`ProjectSettings/EditorBuildSettings.asset` 확인됨).
  - `Assets/Editor/SceneBootstrapper.cs` — 위 3개 에셋을 재생성하는 에디터 빌더(메뉴 `StickMate/Build All...` 또는 `-executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll`). 리더 지시대로 작업 종료 후에도 남겨둠 — 나중에 프리팹 리그를 조정하거나 씬을 재생성할 때 코드로 일관되게 재현 가능.
  - `Assets/_Project/Scripts/Tests/PlayMode/{StickMate.Tests.PlayMode.asmdef,StickmanPlaytestSmokeTests.cs}` — 정식 Unity Test Framework PlayMode 테스트(EditMode처럼 `-runTests -testPlatform PlayMode`로 CI 재실행 가능한 영구 회귀 자산). `Main.unity`를 로드해 15초간 0.5초 간격으로 `StickmanAgent.transform.position`을 샘플링하며 `Debug.Log`로도 남겨(`[PLAYTEST]` 접두사) `-logFile` 결과에서 grep으로 실측 로그를 바로 확인할 수 있게 했다. 검증 항목: (a) 정착 구간(t≥8s) Y 변동폭 <0.05유닛(무한낙하 아님), (b) 전체 구간 X 변동폭 >0.3유닛(자율 배회가 실제로 걸음), (c) UTF 기본 동작상 `Debug.LogError`/`LogException`이 한 번이라도 발생하면 테스트가 자동 실패(별도 예외 감지 코드 불필요).
- **좌표계 관찰(코드 무수정, 기록만)**: `Platform/NullPlatformWindowService.cs`의 더미 발판은 OS 좌상단 원점 기준 `Rect(0,0,width,40)`, 즉 화면 최상단 40px 밴드다. `Platform/ScreenCoordinateConverter.cs`를 거치면 이는 카메라 위치/orthographicSize와 무관하게 항상 "카메라 뷰포트의 최상단 가장자리" 월드 Y로 고정 환산된다(top = cam.y + orthographicSize). 즉 캐릭터의 발(=`StickmanBlackboard.SenseGround()`가 그대로 발 위치로 쓰는 `Body.position`)이 이 지점에 스냅되면 몸통 대부분이 카메라 시야 밖(더 위)으로 벗어난다 — 코드 주석은 "화면 하단"이라 적혀 있어 실제 배치(최상단)와 이름이 어긋나 보이지만, 이미 EditMode 테스트로 검증된 기존 Phase 1 코드이고 씬/프리팹 배선 스코프 밖이라 손대지 않았다. 플레이테스트는 화면 렌더링이 아니라 `transform.position` 실측 로그로 검증하므로 이 프레이밍 이슈와는 무관하게 유효하다. Phase 2+ 렌더링 담당자가 참고할 수 있도록 `Assets/Editor/SceneBootstrapper.cs` 클래스 문서 상단에도 동일 내용을 기록해뒀다.
- **실측으로 발견한 이슈 2건과 대응(전부 "데이터/씬 배선" 조정으로 해결 — `States/*.cs` 로직 자체는 무수정)**:
  1. **접지 감지 터널링**: 최초 시도(spawn 1유닛 낙하, `groundSnapTolerance` 기본값 6px, `Screen=640x480` 헤드리스 환경)에서 캐릭터가 접지 판정 밴드(약 0.125유닛 폭)를 한 프레임에 통과해 무한 낙하하는 것을 실측 확인(`t=0.5s y=2.174` → `t=2.0s y=-53.449`, 계속 발산). 원인은 README에 이미 기록된 기존 알려진 한계("물리 갱신이 Update() 경로")와 같은 계열 — 헤드리스 배치 환경의 낮은 프레임레이트에서 프레임당 이동거리가 얇은 판정 밴드보다 커질 수 있음. `StickConfig.cs` 자체가 "기본값은 추후 UX/디자인·물리 튜닝으로 교체될 임시값"이라고 명시한 값이므로, 코드가 아니라 `DefaultStickConfig.asset`의 `groundSnapTolerance`를 20px로, 낙하 시작 높이를 1유닛→0.3유닛으로 조정해 재검증 — 정착.
  2. **단일 더미 발판 화면 이탈**: `orthographicSize=5`(세계 폭 약 13.3유닛)로는 15초 관찰 구간 안에 자율 배회 AI가 실제로 유일한 더미 발판의 가장자리에 도달해 `CheckScreenBoundsOrFall`이 정상적으로 Fall 전이시키는 것을 실측 확인(`t=12.0s x=-6.446` → `t=14.5s x=-8.188, y=-63.594`로 발산). **이것은 버그가 아니라 "발판 이탈 시 낙하"라는 기존 설계 그대로의 동작**이다 — 다만 더미 폴백이 발판을 화면 전체 폭 1개로만 제공해 배회 관찰 시간 대비 세계가 좁았던 것이 원인. 카메라 `orthographicSize`를 5→20(세계 폭 약 4배)으로 넓혀 재검증 — 15초 동안 가장자리에 닿지 않고 배회 관찰 가능. 순수 카메라 프레이밍 조정이며 캐릭터/물리/AI 로직은 무관.
- **실측 결과(최종 통과 런, `Logs/playmode_run3.log`에서 grep, `[PLAYTEST]` 접두사 로그 원문 발췌)**:
  ```
  [PLAYTEST] 시작 — initialY=20.300, duration=15s, interval=0.5s
  [PLAYTEST] DIAG Screen=640x480, cam.orthoSize=20, cam.y=0
  [PLAYTEST] t=0.5s x=0.001 y=19.974
  [PLAYTEST] t=1.0s x=0.000 y=19.974
  [PLAYTEST] t=2.5s x=0.068 y=19.974
  [PLAYTEST] t=5.0s x=6.444 y=19.973
  [PLAYTEST] t=8.0s x=11.358 y=19.973
  [PLAYTEST] t=11.0s x=13.874 y=19.973
  [PLAYTEST] t=15.0s x=18.640 y=19.974
  [PLAYTEST] 정착 구간(t>=8s) Y 범위: 0.0015 (min=19.973, max=19.974)
  [PLAYTEST] 전체 구간 X 범위: 18.6405 (min=-0.001, max=18.640)
  [PLAYTEST] 완료 — 정착 Y범위=0.0015(<0.05), X범위=18.6405(>0.3)
  ```
  (a) Y가 낙하 시작(20.3) 직후 즉시 ~19.974로 정착해 15초 내내 0.0015유닛 이내로 유지됨(무한낙하 아님, 정상 스냅). (b) X가 0에서 18.64유닛까지 단조 증가(자율 배회 AI가 실제로 `Rigidbody2D`를 걷게 함 — 이번 런은 우연히 한 방향으로 계속 걸었으나, 도중 여러 샘플 구간의 증가율 변화(예: t=6.5~7.5s 구간 완만해짐)가 Idle/turn-check 개입의 흔적). (c) 전체 로그(`Logs/playmode_run3.log`)에 `Error`/`Exception` 매치 0건. `-runTests` 종료 코드 0, `Logs/playmode_results3.xml`에 `total="1" passed="1" failed="0"`.
- **검증**: Unity 배치모드 전체 컴파일(`Logs/final_compile.log`) — `error CS`/`warning CS` 매치 0건, exit code 0. EditMode 재실행(`Logs/editmode_final.xml`) — `total="13" passed="13" failed="0"` 유지(신규 씬/프리팹/config 데이터가 기존 순수 로직 테스트에 영향 없음 확인). PlayMode 신규 테스트(`Logs/playmode_results3.xml`) — `total="1" passed="1" failed="0"`.
- **남은 것(다음 단계 후보, 이번 스코프 밖)**: (1) 실제 아트 에셋으로 코드 생성 스프라이트 교체. (2) `HingeJoint2D` 각도 제한(`useLimits`)이나 실제 RAGDOLL 강제 피격(`ReportExternalImpact`) 경로는 이번 배선에서 구조만 만족시켰을 뿐 실제 격파/드래그 등 Phase 3 상호작용으로는 아직 검증 안 함(다음 플레이테스트 후보). (3) 위 "실측으로 발견한 이슈 2건"이 시사하듯, README에 이미 기록된 "물리 갱신이 Update() 경로" 한계는 헤드리스/저프레임레이트 환경에서 실제로 판정 밴드 터널링을 일으킬 수 있음이 이번에 처음 실측으로 확인됨 — `groundSnapTolerance` 데이터 튜닝으로 우회했지만, 근본적으로는 FixedUpdate 이관이나 연속 충돌 검사 방식 도입이 더 견고한 해법일 수 있어 Architect 판단 요청. (4) `NullPlatformWindowService` 더미 발판이 "화면 하단"이라는 주석과 달리 실제로는 화면 최상단에 배치되는 좌표계 불일치는 이번에 처음 시각적으로 드러났다(위 "좌표계 관찰" 참고) — Phase 1 로직 자체는 EditMode 테스트로 이미 검증된 상태라 이번 스코프에서 수정하지 않았으나, 실제 렌더링 레이어 착수 전 Architect/Debugger 확인 권고.
| 위 항목 버그 리포트 | Debugger | 완료 | **[Debugger, 2026-08-27]** `docs/BUG_REPORT_SCENE_WIRING.md` 작성 완료 — Blocker 0건/Major 3건/Minor 3건, **Coder로 반려 필요** 판정. Unity 배치모드 컴파일 독립 재검증(에러0/경고0) + EditMode 13/13 + PlayMode 1/1(RNG 시드가 달라 Coder 원 로그와 다른 배회 경로였으나 기준 통과, 재현성 확인) 전부 재확인. 중점 점검 4(`_config` 참조)는 완전 정상 확인(guid 일치). 신규 발견 3건: **BUG-SW-M1** — 팔다리 Collider2D 미부착 + 씬 전체에 바닥 Collider2D 전무로, `RagdollState`의 Getup 전이 조건(전신 속도 임계값 이하 지속)이 감쇠 없는 중력 하에서 수학적으로 영원히 충족 불가능함을 코드로 확인(RAGDOLL 진입 시 화면 밖으로 무한 낙하, 절대 복귀 못 함). 이번 스모크 테스트는 RAGDOLL을 전혀 트리거하지 않아 겉으로 드러나지 않았으나, 이미 구현된 `DragThrowState`/`RivalStickmanAgent`(Phase 3)가 전부 `ReportExternalImpact()`로 RAGDOLL을 강제 트리거하도록 짜여 있어 Phase 3 착수 전 Architect/Coder 결정 필요(가상 바닥 Collider2D 추가/시간 기반 강제 Getup 안전망/RAGDOLL 중 중력 조정 중 택1). 덤으로 `RagdollLimbImpactRelay`가 어떤 프리팹에도 부착되어 있지 않고, 부착해도 팔다리에 Collider2D가 없어 영구 무동작임도 확인. **BUG-SW-M2** — `groundSnapTolerance`(6→20px)와 `orthographicSize`(5→20) 두 튜닝이 곱연산으로 상호작용함을 계산으로 확인: px/world-unit 비율(`Screen.height/(2*orthographicSize)`)이 orthoSize 변경으로 48→12로 떨어지면서, 실제 접지 허용 밴드가 SceneBootstrapper.cs 주석이 계산한 "0.3~0.4유닛"이 아니라 실측 약 1.667유닛(4~5배 괴리, 최초 버그 조합 대비 13배)임을 확인 — 지금은 무해하지만(스냅이 순간 강제 대입이라 밴드 폭이 떨림을 유발 안 함) `orthographicSize`를 나중에 게임플레이용 값으로 되돌리면 groundSnapTolerance 단독으로는 원래 터널링 버그를 막기 부족해질 수 있고, 다른 7개 OS-px `StickConfig` 필드(rodeoReachDistancePx 등, Phase 3~5 미검증)도 이번 카메라 확대로 조용히 4배 재조정됨. **BUG-SW-M3** — `SceneBootstrapper.BuildAll()`을 실제로 재실행(`-executeMethod`)한 뒤 `git diff`로 직접 비교해 멱등적이지 않음을 실측 확인(config는 완전 동일, 그러나 `Stickman.prefab`/`Main.unity`는 모든 GameObject/컴포넌트의 fileID가 재실행마다 무작위로 재할당됨, 222줄/26줄 변경 — 테스트 후 `git checkout`으로 원상복구, 저장소 잔여 변경 없음). 이 때문에 프리팹만 부분 재실행하면 `Main.unity`의 스폰 위치 오버라이드가 옛 fileID를 가리킨 채 고아가 될 수 있고, `BuildAll` 전체를 재실행하면 씬을 완전히 새로 만들기 때문에(diff/patch 아님) 그 사이 수동으로 추가한 씬 내용이 경고 없이 전부 소실된다 — 클래스 문서가 재사용을 권장하고 있어 위험 방치 시 다음 팀원이 실제로 밟을 가능성 높음, 최소 경고 문구 추가를 반려 조건으로 요구. Minor 3건(PlayMode 테스트 커버리지가 Idle/Walk/접지스냅에 한정된다는 점을 문서에 명시 권고, CreateOrLoadConfig의 groundSnapTolerance 비대칭 강제 덮어쓰기, HingeJoint2D useLimits 미설정 재확인)은 급하지 않음. 상세: `docs/BUG_REPORT_SCENE_WIRING.md`.
| Major 3건 반려 수정(BUG-SW-M1/M2/M3) | Coder | 완료 | **[Coder, 2026-08-28]** 아래 상세 — 3건 전부 Architect 결정대로 수정, 실측 검증 로그 포함.

### Major 3건 반려 수정 상세 (Coder, 2026-08-28)

- **BUG-SW-M1(RAGDOLL 영구 무한낙하) — 표준 랙돌 레이어 기법으로 수정**. `Assets/Editor/SceneBootstrapper.cs`:
  - 신규 Physics2D 레이어 `StickmanLimb`(`EnsureStickmanLimbLayer()` — `ProjectSettings/TagManager.asset`을 `SerializedObject`로 조작해 첫 빈 사용자 슬롯(8번)에 배정, 재실행 시 기존 슬롯 재사용해 멱등적)를 만들고 `Physics2D.IgnoreLayerCollision(layer, layer, true)`로 같은 레이어끼리의 충돌만 껐다. **루트(몸통+머리 compound collider)와 팔다리 4개 전부**를 이 레이어에 배정했다 — 처음 지시는 "팔다리 4개"만 언급했지만, 루트의 `CapsuleCollider2D`(x:[-0.2,0.2], y:[0,1.8])와 다리 콜라이더(예: LeftLeg x:[-0.18,-0.06], y:[0,0.6])가 기하학적으로 크게 겹치므로 루트를 빼면 자체충돌 떨림이 몸통-다리 사이에서 그대로 재발했을 것 — 이건 원래 우려("몸통/팔다리 겹치는 콜라이더의 상시 떨림")를 다시 불러오는 셈이라 표준 기법대로 루트까지 포함시켰다(코드 주석에 판단 근거 기록).
  - 팔다리 각각에 `BoxCollider2D`(시각 스프라이트와 동일 크기)를 추가하고, `RagdollLimbImpactRelay`를 부착해 `_agent`를 `SerializedObject`로 루트의 `StickmanAgent`에 직접 배선(런타임 `Reset()/Awake()` 자동탐색에 의존하지 않음 — 에디터 스크립팅 중 생명주기 콜백 실행이 보장되지 않으므로 `_config` 배선과 동일한 패턴 사용).
  - `BuildMainScene()`에 `CreateGroundCollider()`로 정적 바닥(`Rigidbody2D` 없음 — Unity 표준 정적 콜라이더, `BoxCollider2D` 200x2, 레이어 Default) 추가. Y좌표는 `cam.y+orthographicSize`(더미 발판이 논리적으로 대응하는 높이, `StickmanBlackboard.SnapToGround`가 스냅시키는 바로 그 Y)와 일치시켜, 루트 `CapsuleCollider2D`의 바닥(로컬 y=0)이 스폰 즉시 바닥과 거의 맞닿게 했다.
  - **실측 검증**: 신규 PlayMode 테스트 `Assets/_Project/Scripts/Tests/PlayMode/StickmanRagdollRecoveryTests.cs` 작성 — 씬 로드 후 3초 대기(Idle/Walk 안착 확인) → `StickmanAgent.ReportExternalImpact(threshold*5)` 직접 호출로 강제 RAGDOLL 진입 → 0.25초 간격으로 상태/위치/`RagdollRig.GetMaxSpeed()`를 로그하며 GETUP 경유 및 Idle/Walk 복귀를 폴링. 2회 독립 실행 모두 동일 패턴으로 통과, 실측 로그:
    ```
    [RAGDOLL-TEST] 충격 전 t=3.0s 상태=Idle, pos=(0.05, 5.00, 0.00)
    [RAGDOLL-TEST] ReportExternalImpact(40.0) 호출(threshold=8.0)
    [RAGDOLL-TEST] t=0.25s state=Ragdoll pos=(0.048,5.006) maxLimbSpeed=0.118
    [RAGDOLL-TEST] t=0.50s state=Getup pos=(0.049,5.007) maxLimbSpeed=0.142
    [RAGDOLL-TEST] t=0.75s state=Getup pos=(0.043,5.009) maxLimbSpeed=0.224
    [RAGDOLL-TEST] t=1.00s state=Getup pos=(0.024,5.014) maxLimbSpeed=0.207
    [RAGDOLL-TEST] t=1.25s state=Walk pos=(-0.020,5.028) maxLimbSpeed=3.256
    [RAGDOLL-TEST] 관찰 종료 — sawGetup=True, recoveredToActive=True, elapsed=1.25s, finalState=Walk
    ```
    RAGDOLL 진입 즉시(다음 샘플 t=0.25s)부터 이미 바닥과 접촉해 속도가 임계값(0.3) 근방으로 낮게 유지되고, GETUP을 1초간 거쳐 t=1.25s에 Walk로 완전히 복귀함을 확인했다(Y가 5.00~5.03 범위에 계속 머물러 무한낙하 재발 없음). "아마 될 것"이 아니라 실측 로그로 확인.

- **BUG-SW-M2(`orthographicSize`/`groundSnapTolerance` 튜닝 괴리) — 카메라를 원복하고 발판 폭만 독립적으로 넓힘**. `Assets/Editor/SceneBootstrapper.cs`의 `orthographicSize`를 20→5(원래 설계값)로 되돌렸다. "화면이 좁아 배회 AI가 화면 끝에 닿는다"는 원래 문제는 `Assets/_Project/Scripts/Platform/NullPlatformWindowService.cs`의 더미 발판 폭을 `Screen.width` 그대로가 아니라 `Screen.width * DummyFootholdWidthMultiplier(4f)`로 화면 중심 기준 좌우 대칭 확장해 해결 — px/world-unit 비율(orthographicSize)과 배회 관찰 범위(발판 폭)를 완전히 독립적인 두 축으로 분리했다. `groundSnapTolerance`는 20px 유지(재조정 불필요) — orthoSize=5 기준 20/48≈0.417유닛 밴드로, `SceneBootstrapper.cs` 원래 주석이 계산했던 "0.3~0.4유닛" 설계 의도와 정확히 다시 일치함을 확인(카메라 원복만으로 4~5배 괴리가 저절로 해소됨).
  - **StickConfig의 OS-px 단위 필드 전수 재검토(요청 표)** — `groundSnapTolerance` + 리포트가 지목한 7개 필드를 실제 소비 코드까지 추적해 orthoSize 의존 여부를 정확히 재확인했다(리포트의 "7개 필드 전부 4배 넓어졌다"는 요약은 방향은 맞지만 일부 필드엔 과했다 — 아래 표 참고):

    | 필드 | 소비 위치 | 기본값 | orthoSize 의존성(실제 코드 추적 결과) | 현재(orthoSize=5) 값 평가 |
    |---|---|---|---|---|
    | `groundSnapTolerance` | `GroundSensor.Sense` — 캐릭터 월드위치→OS-px 변환값 vs 발판 OS-px 비교 | 20px | **의존함** — 캐릭터 좌표가 월드→OS-px 변환을 거치므로 orthoSize가 커질수록 유효 월드 밴드가 넓어짐 | 20/48≈0.417유닛, 설계 의도(0.3~0.4유닛)와 일치 — 합리적 |
    | `wanderCursorReactionRadiusPx` | 미사용(`StickConfig.cs` 주석: Phase 2 예약, `AutoWanderController` 아직 미소비) | 150px | 아직 코드가 없어 판단 불가 — 로데오와 유사 패턴(캐릭터 월드위치 비교)으로 구현되면 의존하게 될 가능성 높음 | 당장 영향 없음. **구현 시 이 표 재확인 필요**(경고를 클래스 문서에도 남김) |
    | `rodeoStillRadiusPx` | `RodeoCursorWatcher` — 이번 프레임 커서 OS좌표 vs 직전 프레임 커서 OS좌표 거리 | 5px | **무관** — 양쪽 다 순수 OS 커서 좌표, 월드/카메라 변환이 전혀 개입하지 않음 | orthoSize와 무관하게 항상 "5px 이내 정지" — 합리적, 재검토 불필요 |
    | `rodeoReachDistancePx` | `RodeoCursorWatcher` — 캐릭터 월드위치→OS-px vs 커서 OS좌표 거리 | 400px | **의존함** — 캐릭터 좌표가 월드→OS-px 변환을 거침 | 400/48≈8.33유닛(화면 폭 13.3유닛의 약 63%) — 합리적 |
    | `graffitiMinRadiusPx`/`graffitiMaxRadiusPx` | `GraffitiDirector.TryFindEmptyRegion` — 캐릭터 OS좌표 기준 순수 OS-px 반경 탐색, 겹침판정도 전부 OS-px 공간(발판 `ScreenRect`) | 200/300px | **무관** — 탐색·경계·겹침판정이 전부 OS-px 공간에서만 이뤄짐(화면상 "몇 px 떨어진 곳"이라는 의미가 카메라 줌과 무관하게 유지되는 것이 오히려 올바른 설계) | orthoSize와 무관하게 항상 화면상 200~300px — 합리적, 재검토 불필요 |
    | `graffitiRegionSizePx` | 위와 동일 컨텍스트 — OS-px 정사각형 한 변 | 96px | **무관**(위와 동일 이유) | 합리적, 재검토 불필요 |
    | `runawayHideSpotMarginPx` | `RunawayDirector.ComputeHideSpotWorldPos` — 화면 네 모서리에서 OS-px 여백만큼 인셋한 뒤 월드로 역변환 | 60px | 여백 자체(화면상 인셋 픽셀 수)는 orthoSize와 무관하게 항상 "화면 가장자리에서 60px" — 결과 월드좌표는 orthoSize에 따라 달라지지만 이는 의도된 동작(캐릭터가 항상 화면 시각적 모서리 근처에 숨어야 하므로) | 합리적, 재검토 불필요 |

    결론: 실제로 orthoSize 변경에 취약한 필드는 `groundSnapTolerance`와 `rodeoReachDistancePx`(+ 향후 구현될 `wanderCursorReactionRadiusPx`) 뿐이었다 — 나머지 5개(`rodeoStillRadiusPx`/`graffitiMinRadiusPx`/`graffitiMaxRadiusPx`/`graffitiRegionSizePx`/`runawayHideSpotMarginPx`)는 전부 OS-px 공간에서만 비교/계산되어 카메라 크기와 무관하다. 이번 orthoSize 원복으로 취약한 필드들도 원래 설계 의도값으로 되돌아갔다. `SceneBootstrapper.cs` 클래스 문서 상단에는 리포트가 지목한 8개 필드 전부를 보수적으로 나열해 "orthographicSize 변경 시 재검토" 경고를 남겨뒀다(실제 취약 필드가 일부뿐임은 이 표에서만 정밀하게 구분).

- **BUG-SW-M3(`BuildAll()` 비멱등성) — 기본은 스킵, 강제 옵션 분리**. `Assets/Editor/SceneBootstrapper.cs`:
  - `CreateOrLoadConfig(bool force)`/`BuildStickmanPrefab(StickConfig, bool force)`/`BuildMainScene(GameObject, bool force)` 전부 `force==false`(기본값)면 대상 에셋이 이미 존재할 때 건드리지 않고 로그만 남기고 반환한다(기존 값/파일 완전 보존).
  - 메뉴를 `StickMate/Build All (최초 1회)`(기본, 스킵)와 `StickMate/Rebuild All (기존 자산 덮어씀, 주의)`(대화형 확인 다이얼로그 후 강제 덮어씀, 배치 모드에서는 다이얼로그 없이 바로 진행)로 분리했다. 배치 모드/CI용 `-executeMethod ...BuildAll`은 커맨드라인 끝에 `--force`를 추가하면 강제 재생성된다(`HasForceFlag()`가 `Environment.GetCommandLineArgs()`에서 확인).
  - `EnsureStickmanLimbLayer()`(레이어/충돌 매트릭스 설정)는 멱등적이고 되돌릴 위험이 없어 프리팹 스킵 여부와 무관하게 항상 재확인하도록 남겨뒀다(BUG-SW-M1과의 상호작용 고려).
  - **실측 검증**: `-executeMethod ...BuildAll --force`로 3개 에셋을 전부 재생성한 뒤, `md5`로 해시를 남기고 **`--force` 없이 `BuildAll`을 재실행** — 로그에 `"...이미 존재해 건너뜁니다..."` 3줄이 정확히 찍혔고, 재실행 후 `Stickman.prefab`/`Main.unity`/`DefaultStickConfig.asset`의 md5 해시가 **재실행 전후 완전히 동일**함을 확인(바이트 단위로 전혀 건드려지지 않음 — fileID 재할당 재발 없음).

- **최종 재검증(전부 실측 로그 확보)**:
  - Unity 배치모드 컴파일: `error CS`/`warning CS` 매치 0건, exit code 0.
  - EditMode: `total="13" passed="13" failed="0"` (기준선 유지, 신규 씬/프리팹 배선이 기존 순수 로직 테스트에 영향 없음).
  - PlayMode: `total="2" passed="2" failed="0"`(`StickmanFallsSettlesAndWanders` 기존 스모크 테스트 + 신규 `RagdollEntersAndRecoversToActiveState`, 기준선 1/1 초과 달성). 참고: 첫 회 실행에서 `StickmanFallsSettlesAndWanders`가 1회 실패한 적이 있었는데, 원인은 `AutoWanderController`가 `System.Guid.NewGuid()` 기반 비결정 RNG를 쓰는 기존 설계상 "Idle 종료 후 저확률(5%) 제자리 점프"가 우연히 테스트의 "정착 구간"(t≥8s) 안에서 발생해 순간적으로 Y가 튄 것(t=12.5s에 0.249유닛 튐, 곧바로 원위치)이었다 — 이번 3개 버그 수정과는 무관한 기존 테스트 설계의 RNG 시드 비결정성(재실행 시 재현 안 됨, 이후 2회 연속 재실행 모두 2/2 통과)이며, Debugger 리포트의 Minor 1(테스트 커버리지 범위 협소)과 연결되는 별도 관찰 사항으로 기록만 해둔다(이번 반려 수정 스코프 밖).

## 2026-08-28 (계속) — 씬/프리팹 반려 수정 + 디플레이킹 최종 확인 (Debugger)

**결론: 최종 승인 보류 — 신규 Major(BUG-SW-M4) 발견, Coder 재작업 필요.** 상세는 `docs/BUG_REPORT_SCENE_WIRING.md` 맨 아래 섹션 참고.

- BUG-SW-M1/M2/M3 배선 자체(레이어/충돌매트릭스/바닥콜라이더/relay 부착/orthoSize/멱등성)는 TagManager.asset·Physics2DSettings.asset·prefab/scene YAML 직접 확인으로 전부 정확함을 재검증.
- 디플레이킹 수정(`finalState==Idle/Walk` 판정)은 코드 구조상 역방향 위양성 불가능함을 확인, 8회 반복 실행 전부 통과, 버그 주입(접지판정 강제실패)으로 실제 탐지력도 실증 — 건전함.
- **그러나 신규 발견**: 검증차 PlayMode를 8회 반복 실행한 결과 신규 `StickmanRagdollRecoveryTests`가 2/8(25%) 실패 — 전부 "이동 중(Walk) 피격" 케이스였고, "정지 중(Idle) 피격"은 8/8 성공. 45초 확장 진단 결과 이동 중 피격 시 팔다리 속도가 감쇠 없이 계속 진동(약 2초 주기, 0.02~0.65 사이)해 GETUP 조건이 사실상 영원히 성립하지 않음을 확인. 원래 BUG-SW-M1의 "화면 밖 무한낙하"는 해결됐으나, "RAGDOLL→GETUP 복귀"는 이동 중 피격이라는 실전에서 드물지 않은 케이스에서 여전히 깨져 있음(감쇠 없는 Rigidbody2D + `EnterRagdoll()`이 속도를 초기화 안 함 + 이동 관성이 조인트로 팔다리에 전파됨이 원인으로 추정).
- 검증에 사용한 임시 진단 테스트 파일과 2건의 임시 버그 주입은 전부 검증 직후 삭제/원복, `git status`/`git diff` 클린 확인.
- 다음: Architect/Coder에게 BUG-SW-M4 전달 — damping 추가 또는 시간 기반 강제 Getup 안전망 등 수정 후 Debugger 재확인 필요.

## macOS 네이티브 창 열거 — `docs/BUG_REPORT_PHASE0.md` m8 해소 (Coder, 2026-08-28)

| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| `MacWindowService`(CoreGraphics/CoreFoundation P/Invoke) 구현 | Coder | 완료 | 아래 상세 |
| `StickmanAgent.CreatePlatformService()` macOS 실빌드 분기 배선 | Coder | 완료 | 아래 상세 |
| 실측 검증(`-executeMethod`, 실제 열린 창 열거 확인) | Coder | 완료 | 아래 상세 — 실측 로그 포함 |

- **배경**: Phase 0부터 `Platform/MacOS/`는 `.gitkeep`뿐인 플레이스홀더였다(`docs/BUG_REPORT_PHASE0.md` m8, README.md "알려진 한계"). ARCHITECTURE.md는 macOS를 Windows와 동급 1차 데스크톱 타깃으로 명시하지만 실구현이 전무했다. Windows(`Win32WindowService`)도 창 열거는 되지만 진짜 분리 오버레이(클릭관통 활성화)는 아직 없다(BUG-B1, 안전가드로 차단) — 이번 라운드는 macOS를 정확히 그 수준까지만 맞춘다. 진짜 네이티브 플러그인(.bundle) 빌드는 범위 밖.
- **왜 Objective-C++ 플러그인 없이 가능한가**: macOS는 CoreGraphics 공개 C ABI(`CGWindowListCopyWindowInfo`, `CGEventCreate`/`CGEventGetLocation`, `CGDisplayBounds` 등, `/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics`)를 C#에서 직접 P/Invoke할 수 있어, CoreFoundation 보조 함수(`CFArrayGetCount/GetValueAtIndex`, `CFDictionaryGetValue`, `CFNumberGetValue`, `CFStringCreateWithCString/GetCString`, `CFRelease`, `/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation`)와 함께 쓰면 네이티브 플러그인 없이도 안전한 조회 전용 열거가 가능하다.
- **신규 파일 `Assets/_Project/Scripts/Platform/MacOS/MacWindowService.cs`**(`#if UNITY_STANDALONE_OSX`로 전체 격리, Win32WindowService.cs와 동일한 파일 단위 P/Invoke 격리 컨벤션) — `IPlatformWindowService` + `ICursorPositionService` 구현:
  - `EnumerateFootholds()`: `kCGWindowListOptionOnScreenOnly|kCGWindowListExcludeDesktopElements`로 열거, `kCGWindowLayer==0`(일반 앱 창)만 채택(메뉴바/Dock/데스크톱아이콘/알림센터 등 시스템 레이어 제외), `kCGWindowOwnerPID`(1차, 정확한 식별자)+`kCGWindowOwnerName`(보조 신호) 이중 판정으로 자기 자신 창 제외. `kCGWindowBounds`는 X/Y/Width/Height를 손으로 하나씩 파싱하는 대신 CoreGraphics 공식 왕복 함수 `CGRectMakeWithDictionaryRepresentation`으로 한 번에 CGRect 변환(마샬링 표면적 축소). 반환이 z-order 전→후 보장(Apple 문서화)됨을 이용해 필터 통과 후 첫 항목을 `IsTopmost=true`로 표시(Win32의 `hWnd==GetForegroundWindow()`와 동일 의도의 근사).
  - `CreateOverlayWindow()`: Win32처럼 "자기 창 핸들 재사용" 폴백도 시도하지 않는다 — `Process.MainWindowHandle`은 .NET BCL상 Windows 전용이라 macOS에서 아예 호출 불가. 대신 열거 파이프라인에서 ownerPID==자신인 창을 찾아 CGWindowID만 기록하는 진단적 구현(실사용처 없음 — 아래 두 메서드가 무조건 거부하므로).
  - `SetClickThrough()`/`SetAlwaysOnTop()`: 항상 `NotSupportedException`. 실제 NSWindow의 `ignoresMouseEvents`/`level` 조작은 Cocoa 오브젝트 접근이 필요해 CoreGraphics/CoreFoundation 공개 C ABI로는 원천 불가능(비공개 SkyLight API는 금지 대상), Objective-C++ 네이티브 플러그인이 있어야만 가능 — Win32의 BUG-B1 가드와 같은 패턴이되, Win32는 "진짜 오버레이가 생기면 조건부로 풀리는" 가드인 반면 macOS는 "네이티브 플러그인이 생기기 전까지 원천 불가능"이라 조건 없이 항상 거부(차이를 클래스 문서에 명시).
  - `IsFullscreenAppActive()`: (자신 제외) 최상단 일반 레이어 창의 bounds가 `CGDisplayBounds(CGMainDisplayID())`와 오차 0.5px 이내로 일치하면 true — Win32의 "전경창==모니터 전체" 휴리스틱을 그대로 이식.
  - `ICursorPositionService.TryGetGlobalCursorPosition()`: `CGEventCreate(NULL)`+`CGEventGetLocation()`으로 전역 커서 좌표 조회(입력 주입 없는 순수 조회). 좌표계 확인: `CGEventGetLocation`/`CGWindowListCopyWindowInfo`/`CGDisplayBounds`는 전부 동일한 "Quartz 디스플레이 좌표계"(메인 디스플레이 좌상단 원점, y 아래로 증가)를 쓰며 `PlatformFoothold.ScreenRect`/Win32 `GetWindowRect`와 이미 일치 — 추가 y반전 불필요(AppKit `NSWindow`/`NSScreen` 좌표계와는 다른 얘기라 향후 실제 Cocoa 플러그인 추가 시 그쪽은 별도 반전 필요, 클래스 문서에 명시).
  - `ILocalClickCaptureService`/`IDesktopIconLayoutService`는 이번 라운드에 의도적으로 미구현(요청 범위 밖) — `FallbackPlatformWindowService`의 `as` 캐스팅이 null로 안전 처리.
  - **마샬링 함정 발견/수정**: CoreFoundation의 `Boolean`/`bool` 반환값은 1바이트인데 `[MarshalAs(UnmanagedType.I1)]` 없이 선언하면 .NET 기본 마샬러가 4바이트로 오독해 쓰레기 상위 바이트까지 읽어 true/false 판정이 무작위로 깨질 수 있다(Win32 `BOOL`은 반대로 4바이트라 이 속성이 없어야 맞음 — 두 플랫폼 규칙이 다름, 다른 파일 패턴을 그대로 복사하면 안 됨). `CGRectMakeWithDictionaryRepresentation`/`CFStringGetCString`/`CFNumberGetValue`에 전부 명시적으로 이 속성을 붙였고, 아래 실측에서 실제로 정확히 동작함을 확인했다.
  - `kCGWindow*` 딕셔너리 키는 심볼을 `dlsym`으로 조회하는 대신 동일한 리터럴 문자열로 직접 CFString을 만들어 사용(`CGWindowListCopyWindowInfo`가 반환하는 CFDictionary는 `kCFTypeDictionaryKeyCallBacks`로 생성되어 키 비교가 포인터 동일성이 아닌 `CFEqual`(내용 비교)이므로 안전 — 여러 언어의 검증된 CoreGraphics FFI 관용구, dlsym 왕복보다 마샬링 표면적이 적음).
- **`StickmanAgent.CreatePlatformService()` 배선** (`Assets/_Project/Scripts/Core/StickmanAgent.cs`): 기존 `#else`(macOS/에디터 공용 Null 폴백)를 `#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR`(신규, `FallbackPlatformWindowService(new MacWindowService(), _config)` — Win32와 동일하게 "발판 0개 무한낙하" 안전망으로 감쌈) / 나머지 `#else`(기존 `NullPlatformWindowService` 유지)로 분리.
  - **`&& !UNITY_EDITOR`가 필수인 이유(지시문의 가정을 실측으로 검증 → 가정이 틀렸음을 확인)**: 작업 지시는 "`UNITY_STANDALONE_OSX`는 실제 빌드에서만 정의되고 에디터에서는 정의 안 됨"이라 가정했으나, 임시 진단 스크립트로 `-executeMethod` 실측한 결과 **이 프로젝트의 활성 빌드 타깃이 `StandaloneOSX`로 설정돼 있어 에디터 컴파일 컨텍스트에도 `UNITY_EDITOR`와 `UNITY_STANDALONE_OSX`가 동시에 정의됨을 확인**했다(`Logs/define_diag.log`). Win32 분기(`UNITY_STANDALONE_WIN`)에 `!UNITY_EDITOR`가 없는 것은 "안전해서"가 아니라 "이 프로젝트의 활성 빌드 타깃이 지금까지 Windows였던 적이 없어 그 분기가 에디터에서 컴파일된 적이 없었을 뿐"임을 시사한다(Win32 분기는 이번 스코프 밖이라 손대지 않고 관찰만 기록). 이 가드가 없었다면 에디터/배치모드에서도 `MacWindowService`가 조용히 활성화되어, 지금까지 모든 실측 플레이테스트가 의존해온 `NullPlatformWindowService` 더미 발판을 대체해버렸을 것이다.
- **실측 검증** (`Assets/Editor/MacWindowEnumerationDiagnostic.cs`, 메뉴 `StickMate/Diagnostics/Log macOS Window Enumeration` — SceneBootstrapper.cs와 동일 컨벤션으로 영구 진단 도구 보존): `-executeMethod`로 이 macOS 세션에 실제 열려 있던 창을 대상으로 실행. 교차 확인용 `osascript`로 확보한 실제 포그라운드 프로세스 목록: `Cursor, KakaoTalk, Notes, Finder, Simulator, Google Chrome, Claude, Unity Hub`. 실측 로그(`Logs/mac_enum_test.log`) 발췌:
  ```
  [MACWIN-TEST] EnumerateFootholds() 결과 개수 = 2
  [MACWIN-TEST] foothold[0] handle=865 rect=(x=0.0, y=33.0, w=1512.0, h=874.0) isTopmost=True   # Cursor 에디터 창
  [MACWIN-TEST] foothold[1] handle=50 rect=(x=256.0, y=147.0, w=1000.0, h=660.0) isTopmost=False # Notes(메모) 창
  [MACWIN-TEST] IsFullscreenAppActive() = False
  [MACWIN-TEST] TryGetGlobalCursorPosition() = True, pos=(649.1,390.7)
  [MACWIN-TEST] CreateOverlayWindow() = False   # 배치모드는 온스크린 창이 없어 정상적으로 "못 찾음"
  [MACWIN-TEST] SetClickThrough()/SetAlwaysOnTop() 안전가드 정상 동작(NotSupportedException)
  [MACWIN-TEST] 원시 온스크린 창 총 개수(필터링 전) = 21
  ```
  원시 21개 중 필터링 제외 항목 전수 대조: 제어 센터(layer=25) 9개, Window Server 메뉴바(layer=24)/커서 트래킹(layer=2147483630), Dock(layer=20), 알림 센터(layer=-2147483601) 2개, Finder 데스크톱 레이어(-2147483603), Wallpaper(layer=-2147483624 — `kCGDesktopIconWindowLevel`과 정확히 일치, 마샬링 정확성의 강력한 증거), Window Server 배경 레이어(-2147483602/-2147483626) — 전부 `layer≠0`이라 정확히 제외됨. `layer==0`인 진짜 일반 앱 창은 Cursor·Notes 2개뿐이었고 정확히 그 2개만 채택됨. 오너 이름의 한글 문자열("제어 센터"/"메모"/"알림 센터")도 깨짐 없이 정확히 디코딩되어 `CFStringGetCString`+UTF-8 마샬링이 올바름을 추가 확인. 결론: **P/Invoke 마샬링이 실제 실행에서 정확하게 동작함을 실측으로 확인**(단순 컴파일 통과가 아니라 실측으로 검증 완료).
- **검증**: Unity 배치모드 컴파일(`Logs/mac_compile_check.log`) — `error CS`/`warning CS` 매치 0건, exit code 0. EditMode(`Logs/mac_editmode_results.xml`) — `total="13" passed="13" failed="0"`(기준선 유지, 에디터가 여전히 `NullPlatformWindowService`를 쓰는 회귀 확인 겸함).
- **참고로 발견한 무관한 사항(이번 스코프 밖, 수정 안 함)**: PlayMode 스모크 테스트를 추가 확인 차 2회 재실행한 결과 `0/2`, `1/2`로 실행마다 달라졌다. `git diff`로 이번 변경분이 씬/프리팹/설정 파일을 전혀 건드리지 않았음을 확인했고, `StickmanAgent.CreatePlatformService()`의 신규 분기도 `!UNITY_EDITOR` 조건 때문에 에디터에서는 논리적으로 완전히 no-op이라 이 실패와 무관함이 코드로 보장된다. 위 Debugger 로그(바로 위 절, BUG-SW-M4)와 대조한 결과 이동 중 RAGDOLL 피격 시 GETUP 복귀가 25% 확률로 실패하는 이미 접수된 별도 결함과 정확히 일치하는 패턴이었다 — 이번 macOS 작업과는 무관하므로 고치지 않고 교차 확인만 기록해둔다.

## BUG-SW-M4 수정 — 이동 중 피격 RAGDOLL 정착 실패 (Coder, 2026-08-28)

**배경**: Debugger가 `docs/BUG_REPORT_SCENE_WIRING.md` 맨 아래 절에서 `StickmanRagdollRecoveryTests`를 8회 독립 반복 실행해 2/8(25%) 실패를 발견 — 전부 "이동(Walk) 중 피격" 케이스, "정지(Idle) 중 피격"은 8/8 성공. 45초 확장 진단으로 `maxLimbSpeed`가 감쇠 없이(진폭 축소 추세 없음, 약 2초 주기로 0.02~0.65 사이를 무한 반복) 진동해 `RagdollState`의 정착 판정(`GetMaxSpeed() <= ragdollSettleSpeedThreshold`가 `ragdollSettleHoldDuration` 이상 유지)이 사실상 영원히 성립하지 않는 구조적 문제로 진단됨. 원인: (1) 팔다리 4개 `Rigidbody2D`가 전부 `linearDamping=0`(Unity 기본값), (2) `RagdollRig.EnterRagdoll()`이 속도를 전혀 초기화하지 않아 걷기 관성이 HingeJoint2D를 통해 그대로 실려 있는 채로 RAGDOLL 진입.

**Architect 결정**: 실제 랙돌은 항상 0이 아닌 damping을 갖는다 — 설계 결함이 아니라 프리팹 튜닝 누락. 프리팹을 손으로 고치는 대신 `SceneBootstrapper.cs`(프리팹을 코드로 조립하는 유일한 소스)를 고치고 `--force`로 재생성해 반영.

### 적용한 수정

1. **`Assets/Editor/SceneBootstrapper.cs`** — 클래스 상단에 `LimbLinearDamping = 0.6f`, `LimbAngularDamping = 1.5f` 상수를 신설하고, `CreateLimb()`에서 각 팔다리 `Rigidbody2D` 생성 시 `rb.linearDamping`/`rb.angularDamping`에 적용(기존에는 `mass`/`gravityScale`만 설정하고 damping은 Unity 기본값 그대로 방치되어 있었음). 루트 몸통 `Rigidbody2D`는 건드리지 않음(정지 중 피격은 이미 8/8 정상이었고, 문제의 근원이 팔다리 쪽 관성 전파였기 때문).
2. **`Assets/_Project/Scripts/States/RagdollRig.cs`** — `EnterRagdoll()`에서 관절 모터를 끄는 것에 더해, 모든 파츠(`_bodies`, 루트 포함)의 `angularVelocity`를 진입 시 한 번만 절반(`×0.5`)으로 깎는 보조 조치 추가. `linearVelocity`는 건드리지 않아 "충격에 붕 날아가는" 손맛은 유지하면서, 회전 관성만 초기부터 덜어 damping이 나머지를 정리할 시간을 벌어준다.
3. `StickConfig.ragdollSettleSpeedThreshold`(0.3)/`ragdollSettleHoldDuration`(0.5)는 값 변경 없이 유지 — 아래 실측 결과 damping 조정만으로 충분한 여유(15초 관찰 한도 대비 절반 이하 시간에 정착)가 확보되어 추가 완화가 불필요했음.

값 선정(`linearDamping=0.6`, `angularDamping=1.5`)은 임시 진단 PlayMode 테스트(`CoderDiagRagdollWalkImpactTest.cs`, 실측 검증 직후 삭제 — 삭제 후 저장소에 잔여 변경 없음 확인)로 실제 걷기 상태를 기다렸다가 강제 충격을 준 뒤 20초 관찰해 튜닝: 너무 작으면 기존 버그 재발, 너무 크면 랙돌이 순간 정지처럼 뻣뻣해 보여 "충격에 나가떨어지는" 손맛이 사라짐. 최종 값에서는 충격 직후 `maxLimbSpeed`가 최대 약 4.9까지 튀어 오르는(=여전히 격렬하게 나뒹구는 구간이 존재) 것을 확인해 과도한 경직 없음을 확인했다.

### 실측 검증

- **재생성**: `-executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll --force`로 `Stickman.prefab`/`Main.unity`/`DefaultStickConfig.asset` 재생성. `git diff --stat` 확인 — 프리팹/씬만 갱신(fileID 재할당 포함, BUG-SW-M3 경고대로 전체 재실행이라 안전), config는 값 무변경. 재생성된 프리팹 YAML에서 4개 팔다리 전부 `m_LinearDamping: 0.6`/`m_AngularDamping: 1.5` 반영 확인, 루트는 기존 기본값(`0`/`0.05`) 그대로 유지 확인.
- **임시 진단 테스트 6회 반복**(실제 Walk 상태를 폴링으로 기다린 뒤 강제 충격 + 20초 관찰): **6/6 성공**, 정착+복귀 소요 5.50~6.75초(20초 한도 대비 충분한 여유). 검증 후 진단 테스트 파일 삭제.
- **공식 `StickmanRagdollRecoveryTests` + `StickmanPlaytestSmokeTests` 15회 독립 반복 실행**(매회 새 프로세스, `-runTests -testPlatform PlayMode` — `-quit` 미사용, 매회 `System.Guid` 기반 RNG로 경로 상이):
  - **15/15 전부 통과(100%)** — 두 테스트 합계 `total="2" passed="2" failed="0"` 15회 전부 확인(실행 로그: `Logs/coder_pm_run1.log`~`coder_pm_run15.log`, 결과 XML: `Logs/coder_pm_run1.xml`~`coder_pm_run15.xml`).
  - "충격 전 상태" 분포: Idle 11회, **Walk 4회**(run1, run5, run9, run14 — 이전 100% 실패 재현 케이스). **Walk 피격 4/4 전부 성공**, 소요 시간 3.25~4.75초(관찰 한도 15초 대비 여유 충분). Idle 피격 11/11 전부 성공, 소요 1.25초로 기존과 동일(회귀 없음).
  - 이전 Debugger 실측(8회 중 Walk 2/2 실패)과 대조하면 표본 수/비율이 늘었음에도(15회 중 Walk 4/4 성공) 완전히 역전됨을 확인.
- **최종 재검증**: Unity 배치모드 컴파일(`Logs/coder_final_compile.log`) — `error CS`/`warning CS` 매치 0건, exit code 0. EditMode(`Logs/coder_final_editmode.xml`) — `total="13" passed="13" failed="0"`(기준선 유지).
- 검증에 쓴 임시 진단 파일(`CoderDiagRagdollWalkImpactTest.cs`)은 삭제 완료, `git status` 기준 이번 작업으로 남은 변경은 `Assets/Editor/SceneBootstrapper.cs`/`Assets/_Project/Scripts/States/RagdollRig.cs`(코드)와 `Assets/_Project/Prefabs/Stickman.prefab`/`Assets/_Project/Scenes/Main.unity`(재생성된 에셋)뿐이다(무관한 macOS 작업 파일들은 손대지 않음).

**결론**: BUG-SW-M4 해소. Debugger 재확인 대상.

## macOS 열거 + 랙돌감쇠 + 가드대칭화 통합 확인 (Debugger, 2026-08-28)

**전체 승인 — 이번 라운드(씬배선+macOS+랙돌감쇠) 최종 완료.** 상세는 `docs/BUG_REPORT_SCENE_WIRING.md` 맨 아래 섹션 참고.

- BUG-SW-M4: 프리팹 팔다리 damping(0.6/1.5) + `EnterRagdoll()` 각속도 0.5배 감쇠 코드 확인. PlayMode 20회 독립 재실행 100% 통과(Walk 피격 2/2 포함, run8 t=3.25s/run11 t=6.75s 정상 복귀) — Coder의 15회와 별개로 표본 재확보.
- MacWindowService: Boolean 마샬링(`[MarshalAs(UnmanagedType.I1)]`) 3개 함수 전부 정확, 안전가드(SetClickThrough/SetAlwaysOnTop 무조건 예외) Win32와 대칭 확인, 실제 부작용 코드 없음. `MacWindowEnumerationDiagnostic` 직접 재실행 — Coder 원 실측과 일치(이번 세션 창 2개 정확히 열거, 나머지 19개 시스템레이어 제외, 한글 디코딩 정상).
- `!UNITY_EDITOR` 가드: Windows/macOS 두 분기 모두 대칭 적용 확인. `Library/EditorUserBuildSettings.asset`에서 활성 빌드타깃 `OSXUniversal` 확인. EditMode 13/13·PlayMode 20/20 전부 통과 + 로그에 MacWindowService 흔적 없음(계속 NullPlatformWindowService 더미 발판)으로 에디터 무영향 간접 확인.
- 컴파일 에러0/경고0 재확인.

## BUG-P1-R4-B1 수정 — 캐릭터가 화면 상단에 잘려 보이는 카메라 프레이밍 버그 (Coder, 2026-08-28)

**배경**: 리더가 사용자에게 실제 동작을 보여주려고 Unity 에디터를 GUI 모드로 띄우고 `Main.unity`를 Play시켰다. 사용자가 직접 화면을 보고 "화면 제일 상단에서 뭐가 좀 왔다갔다하고 안 보인다"고 보고 — 캐릭터가 카메라 뷰포트 맨 위 경계에 걸쳐 잘려 보이는 것으로 리더가 진단했다. 지금까지 어떤 자동 테스트도 이 버그를 잡아내지 못했다(기존 테스트는 `transform.position.y`가 "발산하지 않는지"만 확인했을 뿐 실제로 화면 안에 보이는지는 검증한 적이 없었다).

**근본 원인**: `Assets/_Project/Scripts/Platform/NullPlatformWindowService.cs` 생성자가 더미 발판("작업표시줄" 역할)을 `new Rect(widenedX, 0f, widenedWidth, dummyTaskbarHeight)`로 만들었는데, 이 프로젝트가 일관되게 쓰는 OS 좌표계(좌상단 원점, y 아래로 갈수록 증가 — `Platform/ScreenCoordinateConverter.cs` 문서)에서 y=0은 화면 "맨 위"다. 즉 주석은 "작업표시줄"이라 해놓고 실제로는 화면 최상단에 발판을 놓은 반대 버그였다 — `Platform/FallbackPlatformWindowService.cs`에서 예전에(BUG-P1-R3-B1) 고쳤던 것과 정확히 같은 종류의 실수인데, 그때는 `NullPlatformWindowService`를 건드리지 않아 여기 남아 있었다. 이 발판 위치가 `Assets/Editor/SceneBootstrapper.cs`의 `groundTopWorldY = cam.transform.position.y + cam.orthographicSize`(카메라 뷰포트 "상단" 가장자리) 계산과 맞물려, 캐릭터가 정확히 뷰포트 최상단 경계에 정착하도록 만들었다.

### 적용한 수정

1. **`Assets/_Project/Scripts/Platform/NullPlatformWindowService.cs`** — 더미 발판을 화면 진짜 "맨 아래"에 두도록 수정(`FallbackPlatformWindowService.GetFallbackFoothold()`와 동일한 `y = height - 두께` 패턴). 다만 단순히 위/아래만 뒤집으면 이번엔 반대쪽(화면 하단) 가장자리에서 캐릭터가 잘리는 동일 계열 버그가 재발할 위험이 있어(발판 두께가 고정 픽셀값 40이면, 해상도가 클수록 발판이 화면 맨 아래에 더 바짝 붙는다), 발판 두께를 고정 픽셀이 아니라 `Screen.height`에 대한 비율(`DummyFootholdHeightFraction = 0.2f`, 신규 `public const`)로 바꿨다. 이러면 발판 상단 가장자리가 대응하는 월드 Y가 `Screen.height` 실측값과 무관하게 항상 `cam.y - orthographicSize*(1-2*f)`라는 카메라 설정만의 폐쇄형 값이 된다(해상도가 배치모드 640x480이든 GUI의 임의 Game View 크기든 동일하게 안전). `DummyFootholdHeightFraction`을 `public`으로 노출해 `SceneBootstrapper.cs`가 매직 넘버로 따로 계산하지 않고 이 값을 직접 참조하게 했다 — 두 파일이 서로 다른 가정을 갖게 된 것 자체가 이번 버그의 근본 원인 중 하나였기 때문(재발 방지).
2. **`Assets/Editor/SceneBootstrapper.cs`** — 신규 헬퍼 `ComputeGroundTopWorldY(Camera cam)`가 위 폐쇄형 수식(`cam.y - orthographicSize*(1-2*f)`)을 계산하고, `CreateGroundCollider()`(RAGDOLL 물리 바닥)와 `BuildMainScene()`의 캐릭터 초기 배치 둘 다 이 헬퍼 하나만 거치도록 통일(기존에는 두 곳이 각자 `cam.transform.position.y + cam.orthographicSize`를 따로 계산해 서로 어긋날 위험이 있었음). `orthographicSize`는 BUG-SW-M2 경고(8개 OS-px 필드 종속) 때문에 그대로 5로 유지했고, 대신 `DummyFootholdHeightFraction=0.2`를 골라 캐릭터 전신(발~머리, 로컬 y 0~1.8유닛)이 뷰포트 안에 넉넉한 여백을 두고 들어오도록 설계했다: `groundTopWorldY = -3`(orthoSize=5, cam.y=0 기준) → 지면이 뷰포트 하단(cam.y-5)에서 2유닛 위, 머리 정수리(약 -1.2)가 뷰포트 상단(cam.y+5)에서 6.2유닛 아래 — 위/아래 여백 모두 최소 요구치(0.5~1유닛)를 크게 상회.
3. **`Assets/_Project/Scripts/Tests/PlayMode/StickmanOnScreenFramingTests.cs`(신규)** — 재발 방지 테스트. `Main.unity`를 실제로 Play시켜 캐릭터의 모든 `SpriteRenderer.bounds`를 합친 월드 바운딩박스(발끝~머리끝)를 `Camera.main.WorldToScreenPoint()`로 스크린 좌표 변환한 뒤, 정착 후 5초/10초/15초 시점에 화면 세로 범위([marginPx, Screen.height-marginPx], margin=0.5월드유닛을 px로 환산) 안에 있는지 검증한다. 가로(X)는 의도적으로 화면 폭 안 포함을 강제하지 않는다 — `NullPlatformWindowService`의 더미 발판은 BUG-SW-M2 대응으로 카메라 뷰포트보다 4배 넓게 설계되어 있고 `AutoWanderController`가 그 범위를 자유롭게 배회하는 것이 명시적으로 의도된 동작이라, 15초 관찰 구간에서 X가 카메라 뷰포트를 벗어나는 것은 정상이다(실측으로도 확인 — 아래 참고). X는 발산(NaN/Infinity) 여부만 확인한다.

### 실측 검증

- **재생성**: `-executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll --force`. 재생성된 `Main.unity`에서 `PhysicsGround.m_LocalPosition.y = -4`(기대값: `groundTopWorldY(-3) - thickness/2(1) = -4`, 일치), Stickman 프리팹 인스턴스 오버라이드 `m_LocalPosition.y = -2.7`(기대값: `-3 + 0.3 = -2.7`, 일치) 확인.
- **컴파일**: Unity 배치모드 — `error CS`/`warning CS` 매치 0건, exit code 0(`Logs/coder_compile_check1.log`, `Logs/coder_final_compile.log`).
- **EditMode**: `total="13" passed="13" failed="0"`(기준선 유지, `Logs/coder_editmode.xml`).
- **PlayMode 5회 독립 반복 실행**(매회 새 프로세스, `-runTests -testPlatform PlayMode`, `-quit` 미사용): **5/5 전부 통과**, 매회 `total="3" passed="3" failed="0"`(기존 `StickmanFallsSettlesAndWanders`/`RagdollEntersAndRecoversToActiveState` + 신규 `StickmanStaysWithinVerticalViewportMargin`, 로그: `Logs/coder_pm_probe1.log`~`coder_pm_probe5.log`, XML: `Logs/coder_pm_probe1.xml`~`coder_pm_probe5.xml`).
  - 신규 프레이밍 테스트 실측(5회 전체): `bottomScreen.y`(발) 86.7~96.7px, `topScreen.y`(머리) 117.4~183.0px — 여백 하한 24px(=0.5유닛)와 상단 한계 456px(=Screen.height-24)에 전혀 근접하지 않고 항상 화면 중하단부에 안정적으로 위치.
  - 가로 드리프트 실측으로 X 미검증 결정이 타당함을 확인: run2 t=10s에 `topScreen.x=729.1`으로 `Screen.width=640`을 이미 초과 — AutoWander가 카메라 뷰포트 밖으로 나가는 것이 실제로 자주 발생하는 정상 동작임을 실측으로 재확인(따라서 X를 화면 폭으로 강제 검증했다면 이 버그 수정과 무관한 이유로 테스트가 빈번히 실패했을 것).
  - 기존 회귀 없음: `StickmanRagdollRecoveryTests` 5/5 `recoveredToActive=True`(1.25s), `StickmanPlaytestSmokeTests` 5/5 최종상태 Idle/Walk(접지) 유지.

**결론**: BUG-P1-R4-B1 해소. `git status` 기준 이번 작업 변경분은 `Assets/_Project/Scripts/Platform/NullPlatformWindowService.cs`/`Assets/Editor/SceneBootstrapper.cs`(코드), `Assets/_Project/Scripts/Tests/PlayMode/StickmanOnScreenFramingTests.cs`(신규 테스트), `Assets/_Project/Prefabs/Stickman.prefab`/`Assets/_Project/Scenes/Main.unity`(재생성된 에셋)뿐이다. Debugger 재확인 대상.

## 2026-08-28 (계속) — 카메라 프레이밍 수정 Debugger 승인

- 더미 발판 배치식을 직접 대수적으로 검산해 `ComputeGroundTopWorldY`와 정확히 일치함을 확인, grep 전수조사로 지면 Y 계산이 그 헬퍼 한 곳에서만 이뤄짐을 확인, 신규 프레이밍 테스트가 진짜 assert이며 X축 미검증 근거(walkSpeed×최대Walk지속 > 뷰포트 반폭)도 산술적으로 타당함을 확인.
- 컴파일 에러0/경고0, EditMode 13/13, PlayMode 5회 독립 재실행 5/5 전부 통과(랙돌 정착 위치도 새 지면 Y와 일치, 회귀 없음). 상세는 `docs/BUG_REPORT_SCENE_WIRING.md` 맨 아래 섹션 참고. **승인.**

## "바로 바탕화면에서 구동" — macOS 네이티브 오버레이 플러그인 + 실제 Standalone 빌드 (Coder, 2026-08-28)

**배경**: 사용자가 명시적으로 요청 — "바로 바탕화면에서 구동할 수 있게 구현해줘". `MacWindowService.CreateOverlayWindow()`/`SetClickThrough()`/`SetAlwaysOnTop()`가 지금까지 안전가드(`NotSupportedException`)로 막혀 있던 이유는 Unity 에디터 Play 모드의 게임뷰가 에디터 UI 안의 패널일 뿐 실제 OS 창이 아니라서였다 — 진짜 투명/클릭관통 오버레이를 만들려면 (1) 실제 Standalone 빌드가 있어야 하고 (2) 그 빌드가 만드는 진짜 `NSWindow`를 네이티브 코드로 조작해야 한다. 이 프로젝트는 지금까지 씬/프리팹만 만들었을 뿐 한 번도 실제 `.app` 빌드를 만든 적이 없었다.

### 적용한 수정

1. **`Assets/Plugins/macOS/StickMateOverlayPlugin.m`(신규)** — Objective-C 네이티브 플러그인. `extern "C"` C ABI로 `SM_ConfigureOverlayWindow(makeClickThrough, alwaysOnTop, transparent)`/`SM_GetOverlayWindowLevel()`/`SM_IsMainWindowFound()` 세 함수를 export한다. `StickMate_FindMainWindow()`가 `[NSApplication sharedApplication].windows`에서 `isVisible && isMainWindow`인 창을 우선 찾고, 없으면 "첫 보이는 창"으로 폴백한다 — 절대 다른 프로세스의 창에는 접근하지 않는다(우리 프로세스 자신의 `NSApplication.windows`만 순회). 클릭관통은 `setIgnoresMouseEvents:`, 항상위는 `setLevel:(NSFloatingWindowLevel/NSNormalWindowLevel)`, 투명은 `setOpaque:NO`+`backgroundColor=clearColor`+`hasShadow:NO`+`FullSizeContentView`+타이틀바 투명화+콘텐츠뷰 레이어 비불투명 시도로 구현했다. `clang -dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -framework Cocoa`로 유니버설 바이너리(`Assets/Plugins/macOS/build.sh`, 재현 가능한 빌드 스크립트도 함께 커밋)로 컴파일해 `StickMateOverlayPlugin.bundle`(Info.plist `CFBundlePackageType=BNDL` 포함)로 패키징했다.
2. **`Assets/Editor/BuildStandalone.cs`(신규)** — `ConfigureNativePluginImporter()`가 `PluginImporter.SetCompatibleWithAnyPlatform(false)`+`SetCompatibleWithEditor(false)`+`SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true)`+`SetPlatformData(..., "CPU", "AnyCPU")`로 이 플러그인을 macOS Standalone 전용으로 명시 잠근다(에디터 비활성화는 안전상 중요 — Unity 에디터 자신의 메인 창을 실수로 클릭관통/항상위로 바꿔버리는 사고를 원천 차단). `PerformBuild()`가 `BuildPipeline.BuildPlayer`로 `Builds/macOS/StickMate.app`을 만든다(`-executeMethod StickMate.EditorTools.BuildStandalone.PerformBuild -quit`로 배치 실행 가능 — 실제 빌드 자체는 `-quit`과 함께 써도 안전하고, "`-quit` 금지" 컨벤션은 PlayMode 테스트 전용임을 확인해둠).
3. **`Assets/_Project/Scripts/Platform/MacOS/MacWindowService.cs`** — `CreateOverlayWindow()`/`SetClickThrough()`/`SetAlwaysOnTop()`를 `[DllImport("StickMateOverlayPlugin")]`로 위 네이티브 함수를 호출하는 실제 구현으로 교체(기존 CoreGraphics 전용 읽기 전용 코드는 그대로 유지 — `EnumerateFootholds`/`IsFullscreenAppActive`/커서 조회는 무수정). `SM_IsMainWindowFound()==0`이면 `SetClickThrough`/`SetAlwaysOnTop`은 여전히 `NotSupportedException`으로 실패를 명시적으로 알린다(조용히 무시하지 않음 — 기존 컨벤션 유지). `_clickThroughEnabled`/`_alwaysOnTopEnabled` 두 상태를 기억해 `SM_ConfigureOverlayWindow`(3개 인자 동시 적용 단일 함수) 호출 시 서로의 상태를 되돌리지 않게 했다.
4. **`Assets/Editor/SceneBootstrapper.cs`** — Main Camera 배경색 알파를 `1`→`0`으로 변경(`Clear Flags=Solid Color` 유지). 카메라가 그리는 알파가 그대로여야 네이티브 플러그인의 창 투명화가 의미를 가지므로 두 절반이 반드시 짝을 이룬다. `-executeMethod SceneBootstrapper.BuildAll --force`로 `Main.unity`/`Stickman.prefab`/`DefaultStickConfig.asset`을 재생성해 반영했다(BUG-SW-M3 컨벤션대로 — 재생성 전 씬은 SceneBootstrapper가 만든 최소 구성(Main Camera+PhysicsGround+Stickman 인스턴스)뿐이라 수동 편집 유실 위험 없음을 확인 후 진행).
5. **`Assets/_Project/Scripts/Core/StickmanAgent.cs`(긴급 종료 안전장치)** — 클릭관통이 켜지는 순간부터 클릭으로 우리 창에 포커스를 되돌릴 방법이 사라지는 위험에 대응해 이중 방어선을 추가했다: (1) `Start()`가 `SetClickThrough`를 더 이상 즉시 호출하지 않고 `EnableClickThroughAfterSafetyDelay()` 코루틴으로 `ClickThroughSafetyDelaySeconds=5f`초 지연시킨다(항상위는 위험이 없으므로 즉시 적용 유지). (2) `Update()` 최상단(다른 모든 early-return보다 위)에서 `KeyCode.Escape`(`EmergencyDisableKey`)를 감지하면 `ApplyClickThrough(false)`로 즉시 클릭관통을 강제 OFF한다. 두 경로 모두 `ApplyClickThrough()` 단일 헬퍼를 공유. **정직한 한계**: 이 두 장치 모두 "우리 창이 키보드 포커스를 유지하고 있을 때"만 유효하다(Unity Input은 전역 핫키가 아님, Accessibility 권한 없이는 불가능) — 클릭관통 상태에서 사용자가 다른 창을 클릭해 포커스가 넘어가면 앱 내부에서 되돌릴 방법이 없다. 최종 안전망은 터미널에서 프로세스를 직접 종료하는 것뿐이며, 이번 검증에서는 실제로 그 경로(PID 직접 kill 가능)를 확보해둔 채 진행했다.
6. **`Assets/Editor/MacWindowEnumerationDiagnostic.cs`** — 기존 진단 메뉴가 "안전가드는 항상 `NotSupportedException`을 던진다"고 가정했던 것을 갱신. 이 도구는 `Assets/Editor/`(에디터 전용)에 있고 네이티브 플러그인은 `SetCompatibleWithEditor(false)`로 잠겨 있으므로, 에디터에서 실행하면 이제 `DllNotFoundException`이 "정상"이다 — 그 경우를 명시적으로 잡아 로그를 남기도록 catch 절을 갱신(우연히 에디터에도 플러그인이 로드되는 회귀가 생기면 오히려 이 로그로 드러나게 설계).

### 실측 검증

- **컴파일**: Unity 배치모드 3회(코드 변경마다 재확인) — 최종 `Logs/coder_compile_after_scene.log` 기준 `error CS`/`warning CS` 매치 0건, exit code 0.
- **EditMode**: `Logs/coder_native_editmode.xml` — `total="13" passed="13" failed="0"`(기준선 유지, 에디터 무영향 재확인).
- **PlayMode**: `Logs/coder_native_playmode.log`/`.xml` — `total="3" passed="3" failed="0"`(기존 3종 전부 회귀 없음). 로그 전문에 `MacWindowService`/`StickMateOverlayPlugin` 문자열이 전혀 없음을 grep으로 확인 — 에디터 PlayMode는 여전히 `NullPlatformWindowService` 더미 발판만 쓰고 네이티브 플러그인과 완전히 무관함을 재확인.
- **PluginImporter 설정**: `ConfigureNativePluginImporter()` 실행 후 `.meta` 확인 — `platformData.Any.enabled=0`, `platformData.Editor.enabled=0`, `platformData.OSXUniversal.enabled=1`(`CPU: AnyCPU`)로 정확히 macOS Standalone 전용 잠금 확인.
- **빌드**: `Logs/coder_build.log` — `[BuildStandalone] 빌드 결과: Succeeded, 총 에러 0건, 총 경고 0건, 소요 00:00:11.09, 크기 102179062 bytes`. 산출물 `Builds/macOS/StickMate.app` 확인. `Contents/MacOS/StickMateSkeleton`이 이미 `arm64+x86_64` 유니버설 바이너리(프로젝트 Player Settings 기본값)임을 `file`로 확인. `Contents/PlugIns/StickMateOverlayPlugin.bundle`이 앱 번들 안에 실제로 포함됨을 확인(Unity가 자동으로 넣어줌 — 별도 복사 스크립트 불필요).
- **실행**: 빌드된 실행파일을 직접 백그라운드로 실행(`nohup .../StickMateSkeleton & disown`) — **PID 49739**, `ps`로 지속 실행 확인(`PPID=1`로 정상 detach, 이 세션 종료 후에도 계속 살아있음).
- **네이티브 플러그인 실측 로그**(`~/Library/Logs/DefaultCompany/StickMateSkeleton/Player.log`):
  - `SM_ConfigureOverlayWindow 적용 완료: clickThrough=0 alwaysOnTop=0 transparent=1 level=0` — `CreateOverlayWindow()` 초기 적용, `SM_IsMainWindowFound()`가 실제 창을 찾음.
  - `SetAlwaysOnTop(True) 적용 완료 — windowLevel=3` — `NSFloatingWindowLevel`(=3) 적용 확인.
  - 정확히 **5.033초 뒤** `SetClickThrough(True) 적용 완료 — windowLevel=3` — `ClickThroughSafetyDelaySeconds=5f` 지연 로직이 실제로 그 시간만큼 지연시킨 뒤 클릭관통을 켰음을 타임스탬프로 실측 확인(`05:40:29.731` → `05:40:34.764`).
- **창 레벨 외부(독립 프로세스) 검증**: `CGWindowListCopyWindowInfo`(공개 API, Screen Recording 권한 불필요) 기반 별도 컴파일 도구로 PID 49739의 창을 직접 조회 — 메인 창(`windowNumber=1221`, `owner="StickMateSkeleton"`, `onScreen=1`, `bounds=(0,33,1512,949)`)의 **`kCGWindowLayer=3`**(=`NSFloatingWindowLevel`)을 확인. 우리 프로세스 내부 로그(`SM_GetOverlayWindowLevel()=3`)와 완전히 독립적인 외부 관측치가 정확히 일치 — 네이티브 플러그인이 실제 OS 레벨에서 창 레벨을 바꿨음이 이중으로 확증됨.

### 정직한 한계(사용자 직접 확인 필요)

- **클릭관통 자체(실제로 마우스 클릭이 창을 통과하는가)는 이번 라운드에서 프로그래밍적으로 검증하지 못했다** — Accessibility 권한 없이는 합성 클릭 이벤트를 만들어 "정말 아래 창이 클릭됐는지" 확인할 신뢰 가능한 방법이 없다. `NSWindow.ignoresMouseEvents=YES`가 네이티브 코드에서 실제로 호출됐다는 로그(`SM_ConfigureOverlayWindow 적용 완료: clickThrough=1 ...`)까지만 확인했고, 이는 Apple 공식 API 계약상 클릭관통을 보장하지만 최종 체감은 사용자가 데스크톱에서 직접 다른 창을 클릭해 확인해야 한다.
- **화면 투명도(스틱맨 뒤 데스크톱 배경이 실제로 완전히 비쳐 보이는지)도 육안 확인이 필요하다** — 카메라 알파=0 + `setOpaque:NO`+`backgroundColor=clearColor`까지는 확인했지만, Unity Standalone Mac Player의 렌더 서페이스(Metal 레이어)가 기본적으로 불투명 합성을 가정하고 있어 이 조합만으로 100% 완전 투명이 보장되는지는 이번 라운드에서 실측하지 못했다(`StickMateOverlayPlugin.m` 문서 주석에 이 한계를 그대로 남겨둠). 리더가 이미 확인한 대로 이 환경에는 Screen Recording 권한이 없어 스크린샷으로 직접 확인할 수도 없었다.
- **긴급 종료 안전장치(5초 지연 + Escape 키)는 우리 창이 키보드 포커스를 유지하는 동안만 유효**하다 — 전역 핫키가 아니므로 클릭관통 상태에서 포커스가 다른 앱으로 넘어가면 앱 내부에서 되돌릴 방법이 없다(위 "적용한 수정" 5번 참고). 실사용 배포판이라면 메뉴바 아이콘 등 별도 UX가 필요 — 이번 라운드 범위 밖으로 남겨둔다.
- **Player Settings 아키텍처를 코드로 강제하지 않았다** — Unity 6에서 `EditorUserBuildSettings.macOSXArchitecture` 공개 필드를 찾지 못해(컴파일 에러로 확인) 추측성 API 호출 대신 프로젝트 기본값에 맡겼다. 다행히 기본값이 이미 유니버설(`arm64+x86_64`, 실측 `file` 명령으로 확인)이라 이번 검증에는 영향 없었지만, Intel Mac 배포 시에는 Xcode Build Settings UI로 재확인 권고.

**결론**: 사용자가 지금 데스크톱에서 직접 확인 가능한 실제 `.app`이 PID **49739**로 백그라운드에서 계속 실행 중이다(이 세션이 종료돼도 살아있음, `kill 49739`로 언제든 종료 가능). 네이티브 플러그인의 실제 호출/윈도우 레벨 변경은 프로세스 내부 로그와 외부 독립 도구 양쪽에서 이중 확증했다. 클릭관통 체감/완전 투명 여부는 사용자 육안 확인이 필요한 항목으로 명확히 남겨둔다. Debugger/Architect 재확인 대상.

## "고전적 졸라맨" 시각 교체 + 오버레이 타이틀바 보수적 조정 (Coder, 2026-08-28)

**배경**: 리더가 실행 중인 앱을 사용자에게 직접 보여줬고, 사용자가 캐릭터가 "이상하게 나온다"고 지적했다. 두 스타일 중 고르게 했더니 **"고전적 졸라맨 느낌(가는 선만으로)"**을 확정 선택 — 동그란 머리(테두리만) + 얇은 단일 선 몸통/팔다리(참고 이미지: `O` / `|` / `/|\ ` / `/ \`). 기존에는 `Assets/Editor/SceneBootstrapper.cs`가 채워진 흰색 사각형(몸통/팔다리)+원(머리) `SpriteRenderer` 스프라이트로 블록 형태를 만들고 있었다.

### 적용한 수정

1. **`Assets/Editor/SceneBootstrapper.cs`** — 시각 표현을 전부 `LineRenderer` 기반으로 교체, **물리 구조(Rigidbody2D/HingeJoint2D/Collider2D 배치·크기·mass·damping)는 전혀 무변경**.
   - 머리: `CreateHeadRingVisual()` 신설 — `HeadRingSegments=24`개 점을 원주 위에 찍고 `loop=true`로 닫아 "속이 빈 동그라미"를 그린다. 시각 반경은 `HeadVisualRadius=0.25`(신규 상수)이고, 물리 `CircleCollider2D.radius=0.4`는 BUG-SW-M1 이후 값 그대로 유지(판정 크기 무변경, 시각만 교체).
   - 몸통: `CreateLineSegmentVisual()` 신설 — 기존 사각형과 동일한 세로 범위(로컬 y 0.6~1.4)를 유지하는 얇은 세로 선 하나로 교체(화면 프레이밍 BUG-P1-R4-B1에 영향 없음).
   - 팔다리(4개): 기존 `CreateLimb()`의 "Visual" 자식+`SpriteRenderer`를 제거하고, limb 오브젝트 자신에 `LineRenderer`를 직접 붙여 `useWorldSpace=false`로 관절(anchor) 쪽 끝~반대쪽 끝(손/발)을 잇는 선을 그린다. limb는 이미 `localScale=1`(조인트 anchor 계산 때문)이라, **로컬 좌표 LineRenderer는 HingeJoint2D가 매 프레임 회전/이동시켜도 별도의 "매프레임 추종 스크립트" 없이 자동으로 따라간다**(Renderer가 매 프레임 자신이 속한 Transform의 world 행렬로 로컬 정점을 다시 그리는 표준 동작 — MeshRenderer/SpriteRenderer와 동일 원리). 지시서가 제안한 `LimbLineVisual.cs`류의 별도 Update() 컴포넌트는 이 방식이면 불필요해 만들지 않았다.
   - 손/발 표현(보너스, 지시서상 선택사항): `CreateEndMark()` 신설 — 각 limb 끝(손/발 위치)에 짧은 가로선을 하나 더 그려 "졸라맨" 느낌을 강화(4개 limb 전부 적용).
   - 공통 헬퍼 `ConfigureLine()`/`GetLineMaterial()` 신설 — `startWidth=endWidth=0.05`(`LineWidth`), `numCapVertices=numCornerVertices=4`(`LineCapVertices`, 끝을 살짝 둥글려 손그림 느낌), 색상은 기존 컨벤션 그대로 `config.primaryOutlineColor`(기본 검정)를 재사용. 머티리얼은 새 에셋을 만들지 않고 `AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat")`(SpriteRenderer가 기본으로 쓰던 것과 동일한 Unity 내장 머티리얼)를 그대로 재사용 — 프로젝트가 Built-in 렌더 파이프라인(`ProjectSettings/GraphicsSettings.asset`의 `m_CustomRenderPipeline: {fileID: 0}` 확인)임을 먼저 확인하고 결정.
   - 더 이상 쓰지 않는 `GetOrCreateSprite()`/`SpriteTextureSize`/`SpritesFolder`/`CreateStaticVisual()` 제거, 이제 고아가 된 `Assets/_Project/Data/Sprites/{Rect,Circle}Sprite.asset`(+`.meta`)와 빈 `Sprites/` 폴더도 삭제.
   - `-executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll --force`로 `Stickman.prefab`/`Main.unity` 재생성해 반영.
2. **`Assets/_Project/Scripts/Tests/PlayMode/StickmanOnScreenFramingTests.cs`** — `SpriteRenderer[] renderers = ...GetComponentsInChildren<SpriteRenderer>(true)`를 `Renderer[] renderers = ...GetComponentsInChildren<Renderer>(true)`로 일반화(공통 베이스 타입이라 `LineRenderer`/`SpriteRenderer` 등 어떤 렌더러 조합이든 자동으로 커버). `ComputeCombinedBounds` 시그니처도 동일하게 `Renderer[]`로 변경. 이러면 이번처럼 렌더러 종류가 통째로 바뀌어도 이 재발방지 테스트가 깨지지 않는다.
3. **`Assets/Plugins/macOS/StickMateOverlayPlugin.m`** — 사용자가 지적한 "이상하게 나온다"의 원인 후보로 타이틀바 처리를 재검토. 기존에는 `transparent!=0`일 때 `NSWindowStyleMaskFullSizeContentView`까지 추가해 콘텐츠 뷰를 타이틀바 영역까지 확장시켰는데, 그 결과 불투명하게 그려지는 Unity 게임 렌더 서페이스가 타이틀바 신호등 버튼(빨강/노랑/초록) 바로 아래까지 파고들어, 버튼이 정상 타이틀바 배경 스트립 위가 아니라 게임 화면 위에 붕 뜬 것처럼 보이는 부자연스러운 경계가 생길 수 있었다(신호등이 사라지거나 클릭 불가능해지지는 않지만, "창이 이상해 보인다"는 지적과 정확히 들어맞을 수 있는 원인). **`NSWindowStyleMaskFullSizeContentView` 관련 3줄을 제거**하고 `setTitlebarAppearsTransparent:YES`+`setTitleVisibility:NSWindowTitleHidden`만 남기는 보수적 조정으로 바꿨다 — 이러면 타이틀바 영역은 표준 레이아웃(신호등이 놓이는 스트립)을 그대로 유지한 채 배경만 투명해지고, 콘텐츠 뷰는 여전히 타이틀바 아래에서 시작해 위 문제가 생기지 않는다. `setOpaque:NO`/`backgroundColor=clearColor`/`hasShadow:NO`/콘텐츠뷰 레이어 비-불투명 시도는 "바로 바탕화면에서 구동" 기능 자체이므로 무변경. `build.sh`로 재컴파일해 `.bundle` 갱신, `otool -tV` 디스어셈블로 재컴파일된 바이너리와 `BuildStandalone`이 앱에 패키징한 사본의 `__TEXT` 명령어가 바이트 단위로 동일함을 확인(해시가 다른 건 Unity가 앱에 넣을 때 Info.plist를 바인딩해 다시 서명하기 때문 — 코드 자체는 무변경).

### 실측 검증

- **재생성**: `--force`로 `Stickman.prefab`/`Main.unity` 재생성. 프리팹 검사 — `LineRenderer:` 10개(팔다리 4개 라인 + EndMark 4개 + 몸통 1개 + 머리 링 1개), `SpriteRenderer:` 0개. 물리 크기 무변경 확인: 다리/팔 `BoxCollider2D.size` 4개(`{0.12,0.6}`×2, `{0.1,0.5}`×2), 루트 `CapsuleCollider2D.size={0.4,1.8}`, 머리 `CircleCollider2D.radius=0.4` — 전부 교체 전과 동일.
- **컴파일**: Unity 배치모드 — `error CS`/`warning CS` 매치 0건, exit code 0(`Logs/coder_visual_compile.log`).
- **EditMode**: `total="13" passed="13" failed="0"`(기준선 유지, `Logs/coder_visual_editmode.xml`).
- **PlayMode 4회 독립 반복 실행**(매회 새 프로세스, `-runTests -testPlatform PlayMode`, `-quit` 미사용): **4/4 전부 `total="3" passed="3" failed="0"`**(`Logs/coder_visual_playmode.xml`, `coder_visual_playmode_run{2,3,4}.xml`). 리더 지시대로 화면가시성 테스트(`StickmanOnScreenFramingTests`)를 특히 확인 — `Renderer[]` 일반화 후에도 `GetComponentsInChildren<Renderer>` 가 `LineRenderer` 10개를 정상적으로 찾아내고(`renderers.Length > 0` 통과), 매 샘플 시점(5/10/15초)마다 `Renderer.bounds`가 실제 값(예: `bounds.min=(-1.08, -3.66, -0.50) bounds.max=(0.80, -0.57, 0.50)`)으로 정상 계산되어 화면 세로 여백 판정을 계속 통과함을 로그로 확인(`LineRenderer.bounds`가 `SpriteRenderer.bounds`와 동일하게 표준 `Renderer` 베이스 API로 잘 잡힘 — 특수 처리 불필요했음).
- **네이티브 플러그인 재빌드**: `build.sh`로 `.bundle` 재컴파일(universal `x86_64 arm64` 확인). `otool -arch arm64 -tV`로 재컴파일된 원본과 `BuildStandalone`이 앱 안에 패키징한 사본을 디스어셈블 비교 — 파일 경로 헤더 줄만 다르고 나머지 어셈블리 명령어는 완전히 동일(`__TEXT` 세그먼트 파일크기도 양쪽 아키텍처 슬라이스 각각 정확히 일치) — 코드 레벨 동일성 확증(SHA-256 해시 자체는 Unity의 앱 내 재서명으로 인해 달라짐 — `codesign -dvvv` 비교로 원인 확인: 소스 사본은 `Info.plist=not bound`, 앱 내 사본은 `Info.plist entries=10`로 바인딩됨).
- **빌드**: `Logs/coder_visual_build.log` — `[BuildStandalone] 빌드 결과: Succeeded, 총 에러 0건, 총 경고 0건`, 산출물 `Builds/macOS/StickMate.app`.
- **실행**: 이전 프로세스(PID 49739)는 이번 라운드 시작 전 리더가 이미 종료했음을 `ps`로 확인(잔여 프로세스 없음). 새로 빌드한 앱을 백그라운드로 실행 — **새 PID 57301**(`nohup ... & disown`, `ps`로 `PPID=1` 정상 detach 재확인). `Player.log`에 에러/경고/예외 없음, 오버레이 플러그인 로그가 기존과 동일한 순서로 정상 출력됨(`transparent=1` 즉시 적용 → `alwaysOnTop` 즉시 → 5초 지연 후 `clickThrough=1`, `windowLevel=3` 유지). 외부 독립 도구(`CGWindowListCopyWindowInfo`, 공개 API)로 PID 57301의 메인 창을 재조회 — `layer=3`(`NSFloatingWindowLevel`), `bounds=(0,33,1512,949)`로 이전 검증(PID 49739)과 동일한 패턴 확인.
- **정직한 한계**: 이번 세션에도 Screen Recording 권한이 없어(기존에 문서화된 환경 제약과 동일) `kCGWindowListOptionOnScreenOnly` 필터로는 창이 조회되지 않았다(권한 없이는 On-screen 여부 자체를 WindowServer가 알려주지 않는 것으로 보임) — `kCGWindowListOptionAll`로는 정상적으로 창 메타데이터(bounds/layer)가 조회되어 창 자체는 확실히 존재/등록돼 있음을 확인했지만, 새 타이틀바 조정과 새 졸라맨 모양이 실제로 사용자 화면에 "정상적으로 보이는지"의 최종 육안 확인은 여전히 사용자 몫으로 남는다.

**결론**: 캐릭터 시각을 사용자가 확정 선택한 "고전적 졸라맨"(가는 선 + 속이 빈 원) 스타일로 교체했고 물리 동작은 전혀 손대지 않았다. 오버레이 타이틀바는 신호등 버튼/구조를 보존하면서 타이틀 텍스트만 숨기는 보수적 조합으로 되돌렸다. 새 빌드가 PID 57301로 백그라운드 실행 중이다(`kill 57301`로 종료 가능). git commit은 하지 않음. Debugger/Architect/사용자 재확인 대상.

## "까만 화면에 이상하게 나온다" 2연속 재발 대응 — 방어적 배경색 + 실측으로 찾은 진짜 이동불가 버그 수정 (Coder, 2026-08-28)

**배경**: 사용자가 두 라운드 연속 "까만 화면에 제대로 움직이지도 않는다", "엄청 이상한데 졸라맨같지도 않다"고 보고. Architect 진단: (1) 진짜 투명 창이 실패하면 카메라 배경(당시 알파만 0, RGB는 기본 검정)이 불투명 검정으로 렌더링되어 검정 선 캐릭터와 겹쳐 안 보였을 가능성, (2) "제대로 움직이지 않는다"는 별도 재확인 필요. 이번 라운드는 진짜 투명 창은 포기하고 "확실히 보이는 졸라맨"을 최우선으로 확정한다.

### 1) 방어적 배경색 — StickConfig.backgroundFallbackColor 신설

- **`Assets/_Project/Scripts/Core/StickConfig.cs`**: `backgroundFallbackColor`(기본 `(0.94,0.94,0.94)`, 밝은 회색) 필드 신설. 매직 넘버를 코드에 직접 두지 않는다는 이 클래스 상단 컨벤션에 따라, 이전 라운드가 `Assets/Editor/SceneBootstrapper.cs`에 하드코딩해뒀던 동일 목적 값(`0.85,0.85,0.85`)을 config 필드로 승격.
- **`Assets/Editor/SceneBootstrapper.cs`**: `BuildMainScene(GameObject, StickConfig, bool)`로 시그니처 변경(config 파라미터 추가, 인자 없는 오버로드는 `CreateOrLoadConfig(false)`로 자동 로드). 카메라 배경색을 `new Color(config.backgroundFallbackColor.r/g/b, 0f)`로 설정 — **알파는 항상 0 고정, RGB만 config에서 읽음**. 진짜 투명이 성공하면 알파=0이라 이 RGB는 안 보이고, 실패해서 불투명 렌더링되면 밝은 회색 배경이 되어 검정 선 캐릭터(`primaryOutlineColor`, 검정 유지)와 항상 대비된다 — "검정 위에 검정" 최악의 경우를 원천 차단.
- `--force` 재생성 후 `DefaultStickConfig.asset`/`Main.unity` 실측 확인: `backgroundFallbackColor: {r: 0.94, g: 0.94, b: 0.94}`, 씬 카메라 `m_BackGroundColor: {r: 0.94, g: 0.94, b: 0.94, a: 0}` — 의도한 값 정확히 반영.

### 2) StickMateOverlayPlugin.m — 투명 시도 보강 + 진단 로그(진짜 투명은 여전히 미해결, 다음 라운드 이월)

- `StickMate_ApplyTransparencyRecursive()` 신설 — 기존에는 `contentView` 자신의 레이어만 비-불투명 시도했는데, 이제 그 서브뷰 트리 전체에 재귀 적용(Unity 렌더 서페이스가 별도 자식 뷰일 가능성 대비).
- `StickMate_LogViewHierarchy()` 신설 — Screen Recording 권한이 없어 스크린샷 확인이 불가능한 이 환경에서, `SM_ConfigureOverlayWindow(transparent=1)` 호출 시 Player.log에 뷰 트리(클래스명/frame/layer)를 자동으로 남기도록 배선.
- **실측 결과(중요한 진단 정보)**: 실제 `.app` 실행 로그에서 `contentView` 자신이 곧 `PlayerWindowView`(클래스명)이고 그 `layer`가 바로 `CAMetalLayer`임을 확인 — 서브뷰가 별도로 더 있는 구조가 아니라 **콘텐츠 뷰 자신이 실제 렌더 서페이스**였다. 즉 기존 코드의 `contentView.layer.opaque=NO` 시도가 원래도 "맞는 대상"을 건드리고 있었다는 뜻이지만, 그럼에도 진짜 데스크톱 투과 여부는 스크린샷 없이는 100% 확인 불가 — Unity가 매 프레임 이 `CAMetalLayer`의 `opaque`를 자체적으로 되돌릴 가능성은 엔진이 비공개라 배제할 수 없다. **Architect 결정대로 이번 라운드는 진짜 투명 실현을 포기하고 다음 라운드 과제로 명시 이월**(위 1번 방어적 배경색으로 최악의 경우만 방지).

### 3) "제대로 움직이지 않는다" 재확인 — 실측으로 진짜 원인을 찾아 수정함 (BUG-P1-R5-B2)

- LineRenderer가 물리 파츠를 못 따라가는 버그인지 코드 재검토: `ConfigureLine()`이 `useWorldSpace=false`로 올바르게 설정돼 있고, 팔다리 LineRenderer는 Rigidbody2D/HingeJoint2D를 가진 limb 오브젝트 자신에 직접 붙어 있어 물리 회전/이동을 자동으로 따라감 — **이 경로는 문제 없음을 코드로 재확인**.
- `AutoWanderController`가 실제로 Idle/Walk를 오가며 `Rigidbody2D`를 움직이는지는 기존 PlayMode 스모크 테스트로 재확인(3/3 통과) — 하지만 **이 테스트는 에디터/배치모드 전용 `NullPlatformWindowService`(더미 발판)만 거친다**는 점이 이번에 결정적으로 중요했다.
- 지시대로 임시 디버그 로그(`transform.position`/`MoveInputX`/상태/발판정보를 1초 간격으로 Player.log에 기록)를 `StickmanAgent.Update()`에 추가하고 **실제 Standalone `.app`을 직접 실행**해 확인한 결과, **PlayMode 테스트에서는 드러나지 않는 진짜 버그를 실측으로 발견**:
  - 실제 `.app` 실행 중 `state=Fall footholds=1 grounded=False`가 30초 넘게 고정 — 캐릭터가 **영원히 FallState에 갇혀 좌우로 전혀 움직이지 않음**(`AutoWanderController`의 `MoveInputX`는 -1/0/1로 정상 오가고 있었지만 FallState는 그 값을 소비하지 않음).
  - **근본 원인**: `FallbackPlatformWindowService`(macOS/Windows 실제 빌드가 실제 창을 하나도 못 찾을 때 쓰는 안전망 발판)가 예전에는 고정 픽셀 두께(40px)로 "화면의 진짜 맨 아래"에 안전망을 뒀는데, `Assets/Editor/SceneBootstrapper.cs`가 캐릭터 스폰/RAGDOLL 바닥 Y를 계산하는 기준은 `NullPlatformWindowService.DummyFootholdHeightFraction`(화면 하단에서 위로 20%)이다 — **두 값이 서로 다른 Y를 가정**하고 있었다. 에디터/배치모드 테스트는 전부 `NullPlatformWindowService`만 쓰므로(`!UNITY_EDITOR` 가드) 이 불일치가 지금까지 어떤 EditMode/PlayMode 테스트에도 걸리지 않고 숨어 있었다 — **진짜 `.app`을 실제로 실행해봐야만 드러나는 버그**였다.
  - **수정**: `Assets/_Project/Scripts/Platform/FallbackPlatformWindowService.cs`의 안전망 발판 두께를 고정 40px 대신 `height * NullPlatformWindowService.DummyFootholdHeightFraction`으로 변경(단일 소스 공유 — `NullPlatformWindowService.cs`가 이미 "Editor/SceneBootstrapper.cs와 어긋나면 안 된다"고 명시한 원칙을 이 실제 플랫폼 안전망에도 동일 적용). 실제 창이 화면에 있으면 그 창이 먼저 매치되므로(안전망은 항상 리스트 끝에 추가) 정상 사용 시나리오는 영향 없음.
  - **수정 후 재실행 실측**: `state=Idle→Walk→Idle→Walk` 정상 전이, `grounded=True`로 바뀌었고 `pos.x`가 실제로 `-0.50→-2.63→-4.77→-5.50`(Walk 구간)처럼 시간에 따라 뚜렷하게 이동함을 확인. 검증 후 임시 디버그 로그(필드+호출부)는 완전히 제거.

### 실측 검증(최종)

- **컴파일**: Unity 배치모드 — `error CS`/`warning CS` 매치 0건, exit code 0.
- **EditMode**: `total="13" passed="13" failed="0"`(기준선 유지, `FallbackPlatformWindowService` 수정은 `NullPlatformWindowService` 경로에 영향 없음을 재확인).
- **PlayMode**: `total="3" passed="3" failed="0"`(수정 전/후 양쪽 다 재확인 — 회귀 없음).
- **Standalone 빌드**: `총 에러 0건, 총 경고 0건`, `Builds/macOS/StickMate.app` 재생성.
- **실제 `.app` 실행**(임시 디버그 로그 제거 후 최종본) — PID **58671**, `Player.log`에 예외/에러 없음, 오버레이 플러그인 로그 정상 순서(`transparent=1` 즉시 → `alwaysOnTop` 즉시 → 5초 지연 후 `clickThrough=1`).

### 사용자 확인 포인트(정확한 기대값)

- **정상이라면 화면에 밝은 회색(`#F0F0F0` 근처) 배경 위에 검정 선으로 그려진 졸라맨(속이 빈 원 머리 + 가는 선 몸통/팔다리)이 보여야 한다** — 만약 여전히 완전히 까맣게 보인다면 이번 방어적 배경색 자체가 적용 안 된 것(빌드 미갱신 등)이므로 그건 별도 버그.
- 캐릭터가 몇 초 간격으로 가만히 서 있다가(Idle) 좌우로 걷다가(Walk) 다시 멈추는 동작을 반복해야 정상 — 위 2)에서 확인했듯 실측으로 실제 좌우 이동을 재확인했다.
- 진짜 데스크톱이 캐릭터 뒤로 비쳐 보이는지(완전 투명)는 이번 라운드에서 여전히 미해결/미검증 — 안 비치고 밝은 회색 배경이 보여도 "설계대로"이며 버그 아님(다음 라운드 과제로 명시 이월).

**결론**: 배경 대비 문제는 config 기반 방어적 폴백으로 재발 방지 구조를 갖췄고, "제대로 움직이지 않는다"는 막연한 재확인이 아니라 **실제 `.app` 실행 실측으로 진짜 원인(FallbackPlatformWindowService 안전망 Y 불일치)을 찾아 수정**했다 — 이번 수정이 없었다면 사용자가 실제 데스크톱에서 앱을 켤 때(주변에 마침 캐릭터 스폰 높이와 맞는 실제 창이 없는 흔한 경우) 캐릭터가 계속 정지 화면처럼 보였을 것이다. 진짜 투명 창은 Architect 결정대로 다음 라운드 과제로 명시 이월. git commit은 하지 않음. Debugger/Architect/사용자 재확인 대상.

## Architect 실측 지적 후속 대응 — Retina 낙하고착/랙돌 폭주 Blocker + 손발 표현 레퍼런스 반영 (Coder, 2026-08-28)

**배경**: Architect가 사용자에게 보여준 임시 진단 빌드(PID 58459 추정)의 Player.log를 직접 읽고 두 가지를 지적: (1) 실제 Retina 화면(`1512x949` 포인트 vs `3024x1898` 백킹 픽셀)에서 한참 잘 걷다가(`t=30.1 state=Walk grounded=True`) 갑자기 `grounded=False`가 6초 넘게 지속되는(`t=31.2~37.3`) 낙하 고착이 재발했고, 이것이 뒤이은 격렬한 랙돌 폭주(2번째 스크린샷의 팔다리가 뒤엉킨 모습)로 이어졌을 것으로 추정 — Retina DPI 배율 미보정을 유력한 원인 가설로 제시. (2) 사용자가 "먼저 졸라맨 레퍼런스를 확인하고 다시 구현하라"고 지적, Architect가 웹 조사한 결과 손/발은 "짧은 직각선(hook)"이 아니라 "작은 점(채워진 원)"으로 그리는 게 표준(봉선화/棒線畵).

### 1) 손/발 표현 — 채워진 작은 점으로 교체

- **`Assets/Editor/SceneBootstrapper.cs`**: `CreateEndMark()`를 "짧은 가로선"에서 "작은 채워진 원"으로 교체. `HandFootDotRadius=0.04`(Architect 지시 범위 0.03~0.05의 중간값), `HandFootDotSegments=8`, `HandFootDotLineWidth=반지름×2.4`(반지름보다 두꺼운 선으로 작은 원 경로를 그려 "속이 빈 원"이 아니라 "채워진 점"처럼 보이게 함 — `SpriteRenderer`를 재도입하지 않고 이번 라운드에서 확립한 "LineRenderer만 사용" 컨벤션 유지). 더 이상 쓰지 않는 `EndMarkHalfWidth` 상수 제거.
- 비율 재점검(사용자 요청 대응): 머리 지름(0.5)/전신 높이(1.85) ≈ 27%, 팔(0.5)<다리(0.6)<몸통(0.7) — 일반적인 단순 졸라맨 비율 범위 안에 있음을 산술로 확인, 별도 조정 불필요로 판단.
- `--force` 재생성 후 프리팹 실측: `EndMark` 4개 전부 `m_Loop: 1`(닫힌 원), `widthCurve.value=0.096`(=0.04×2.4), 반지름 0.04 원주 8점 좌표 확인.

### 2) Retina 낙하고착/랙돌 폭주 Blocker — 조사 결과 원인은 DPI가 아니라 안전망 발판의 "폭"이었음

- **1차 가설 검증(폐기)**: `PlayerSettings.macRetinaSupport = false`로 꺼서 `Screen.width/height`를 AppKit과 같은 "포인트" 단위로 강제하는 방법을 먼저 시도했으나, **실측 결과 이 프로젝트의 Unity 6 Metal 렌더러에서는 이 설정이 `Screen.width/height`에 전혀 영향을 주지 않았다**(빌드 후 진단 로그로 `screenWH=(3024x1898)` 그대로 확인 — `NSHighResolutionCapable` Info.plist 키는 사라졌지만 Metal 백킹 레이어는 이를 무시하는 것으로 보임). 이 접근은 폐기하고 코드를 되돌렸다(정직하게 실패 기록).
- **진짜 원인(실측)**: `Platform/FallbackPlatformWindowService.cs`의 안전망 발판이 **폭**을 `Screen.width` 그대로(뷰포트 폭 그대로, 절반폭 약 8유닛) 써왔는데, `AutoWanderController`의 한 Walk 페이즈 최대 이동거리(walkSpeed×wanderWalkDurationMax×지터, 기본값 기준 약 11.75유닛)가 이를 초과할 수 있다 — `NullPlatformWindowService`(에디터/배치모드 전용 더미 발판)는 정확히 이 문제 때문에 이미 `DummyFootholdWidthMultiplier`(4배)로 폭을 넓혀뒀지만, 그 넓히기가 실제 macOS/Windows 배포판이 쓰는 이 안전망에는 한 번도 이식되지 않았었다. 에디터 테스트는 4배 넓은 관찰 범위 덕에 이 가장자리에 거의 안 닿지만, 실제 배포판은 정상적인 배회만으로도 수십~백여 초 안에 가장자리에 닿을 수 있어 재현됐다(직전 라운드 BUG-P1-R5-B2로 "t=0부터 영원히 낙하 고착"은 이미 고쳤지만, 이 폭 문제는 "한참 잘 걷다가 나중에 재발"하는 별도 사례였다).
- **수정**: `Platform/NullPlatformWindowService.cs`의 `DummyFootholdWidthMultiplier`를 `private`→`public`으로 승격(단일 소스 공유, `DummyFootholdHeightFraction`과 동일 원칙). `FallbackPlatformWindowService.GetFallbackFoothold()`가 이 배율로 안전망 폭도 화면 중심 기준 좌우 대칭으로 넓히도록 수정.
- **남은 진짜 DPI 문제는 실제로 존재해서 추가로 고쳤음**: 안전망(합성 발판)은 폭/높이 계산이 전부 Unity 자신의 `Screen.width/height`를 일관되게 재사용해 물리픽셀/포인트 단위 차이가 자체 상쇄되지만, **실제 다른 OS 창**(`CGWindowListCopyWindowInfo`, 항상 AppKit 포인트 단위)을 발판으로 인식하는 경로는 이 상쇄 혜택이 없어 Retina Mac에서는 여전히 어긋난 상태였다(`StickConfig.desktopDpiScale` 기본값 1 그대로). 이를 제대로 고치기 위해:
  - `Assets/Plugins/macOS/StickMateOverlayPlugin.m`에 `SM_GetMainWindowBackingScaleFactor()` 신설(`[window backingScaleFactor]` 반환, 창 못 찾으면 안전값 1.0).
  - `Assets/_Project/Scripts/Platform/MacOS/MacWindowService.cs`에 `DetectDesktopDpiScale()` 신설(`1.0/backingScaleFactor` 반환).
  - `Assets/_Project/Scripts/Core/StickmanAgent.cs`의 `CreatePlatformService()` macOS 분기가 `FallbackPlatformWindowService`로 감싸기 전에 이 값을 `_config.desktopDpiScale`에 1회 적용(씬 에셋 파일이 아니라 실행 중 메모리 인스턴스만 갱신).
  - 실측: 이 Retina 환경에서 `desktopDpiScale=0.500`으로 정확히 감지됨(백킹배율 2.0의 역수, 이론값과 정확히 일치) — Player.log로 확인.
  - **정직한 한계**: 이 DPI 자동감지 자체는 "값이 이론상 정확한 숫자로 계산됨"까지 실측했으나, 이 검증 환경에는 발밑에 밟을 실제 다른 OS 창이 하나도 없어서(항상 `footholds=1`, 안전망만 매치) "실제 다른 창 위에 정밀하게 올라서는" 종단 시나리오까지는 이번 라운드에서 실측하지 못했다 — 다음 라운드에서 실제 창이 있는 환경(예: Finder/Terminal 창을 화면에 띄운 채로) 재검증 권장.

### 실측 검증(최종, 60초+ 요구사항 충족)

- **컴파일**: Unity 배치모드 — `error CS`/`warning CS` 매치 0건.
- **EditMode**: `total="13" passed="13" failed="0"`.
- **PlayMode**: `total="3" passed="3" failed="0"`.
- **Standalone 빌드**: `총 에러 0건, 총 경고 0건`.
- **실제 `.app` 117초+ 연속 실행 실측**(임시 디버그 로그, 폭 넓히기+DPI 감지 적용 후): `grounded=False`(낙하) 이벤트 **0건**, 최고 속도 2.51(`walkSpeed` 기본값 2.5와 일치, 폭주 없음), `state` Idle 82회/Walk 35회로 정상 순환. 캐릭터가 `x=-31.61`까지 걸어간 뒤(이론적 안전망 경계 약 ±31.86과 정확히 근접) 자연스럽게 멈춰서는 것을 확인 — 폭 넓히기가 의도한 대로 실제 경계에서 정상적으로 정지/방향전환하는 것을 실측으로 확증. 검증 후 임시 디버그 로그 완전 제거, 최종 클린 빌드로 재확인(예외/에러 0건).

**결론**: Architect가 지적한 낙하고착/랙돌 폭주 Blocker는 실측 조사 결과 최초 가설(Retina Screen.width/height 단위 불일치)이 원인이 아니라 안전망 발판의 폭이 실제 배포 환경에서만 좁았던 것이 원인이었음을 확인하고 수정했다(에디터 테스트로는 재현 불가능한 종류). 진짜 Retina DPI 보정도 별도로 필요해서 네이티브 API로 자동 감지하도록 구현했고, 안전망 자체는 이 보정 없이도 자체 상쇄로 안전했지만 향후 실제 창 인식 정확도를 위해 필요한 기반을 마련했다. 손/발 표현은 레퍼런스에 맞게 채워진 작은 점으로 교체했다. git commit은 하지 않음. Debugger/Architect/사용자 재확인 대상 — 특히 실제 다른 창이 있는 환경에서의 종단 검증을 다음 라운드에서 권장.

## 캐릭터 근본 재구현 — 능동 상태의 물리 기반 포즈 제어 폐기, 순수 절차적 애니메이션으로 교체 (Coder, 2026-08-28)

**배경**: 사용자가 실제 앱을 여러 번 실행해 보낸 스크린샷이 매번 동일했다 — 캐릭터가 **바닥에 쓰러져 누운 채 팔다리가 제멋대로 뻗어 있는 모습**. 서 있는 모습이 잠깐 보인 적도 있지만 곧 무너졌다. 사용자 최종 지적: "캐릭자체가 구현이 제대로 안됨 확실히 해줘". Architect가 프리팹/코드를 직접 조사해 확정한 근본 원인은 **튜닝이 아니라 설계 자체의 오류**였다.

### 왜 물리 기반 능동 상태를 포기했는가 (근거)

1. `Assets/_Project/Prefabs/Stickman.prefab`의 **Rigidbody2D 5개 전부 `m_Constraints: 0`** — 루트(몸통) 회전이 전혀 고정돼 있지 않았다. 몸통이 자유롭게 넘어질 수 있으니 캐릭터가 서 있을 **이유 자체가 없었다**.
2. **팔다리 4개가 전부 `m_BodyType: 0`(Dynamic)** — 관절에 매달린 순수 물리 객체라 중력에 그냥 늘어졌다.
3. 그 위에서 `WalkCycleAnimator`가 `HingeJoint2D` 모터 토크로 버텨보려 했지만, **모터로 중력과 싸우는 것은 근본적으로 지는 싸움**이다. 지난 라운드들의 대응(모터 게인 하향 8→3.5, 최대토크 50→12, 모터속도 상한 150도/초 신설, 물리적 각도 제한 신설)은 전부 "무너지는 속도를 늦추는" 완화책이었을 뿐 원인을 제거하지 못했고, 매 라운드 같은 증상이 재발했다.

즉 지금 구조는 **"물리 랙돌이 스스로 서 있기를 기대하는" 잘못된 설계**였다. `docs/ARCHITECTURE.md` 0절의 원래 의도("능동 상태는 IK/모터로 포즈 제어, RAGDOLL만 전신 물리 위임")는 옳았는데, 구현이 능동 상태에서도 물리에 의존해버린 것이다. Architect 결정: **능동 상태에서는 물리를 쓰지 말고 팔다리를 절차적 애니메이션으로 직접 제어하고, RAGDOLL 상태에서만 물리에 넘긴다.** 이 프로젝트에는 이미 검증된 전례가 있다 — `States/DragThrowState.cs`가 드래그 중 Kinematic 전환을 쓰고 정상 동작이 확인됐다(같은 패턴 재사용).

### 구현

- **`States/StickmanPoseAnimator.cs` (신규, `States/WalkCycleAnimator.cs` 삭제 후 대체)** — 능동 상태 전용 순수 절차적 포즈 드라이버. 모터를 전혀 쓰지 않고 각 팔다리의 `transform.localRotation`을 직접 세팅한다. **회전 중심 보정이 핵심 수학**: 프리팹의 팔다리는 자기 중심이 원점이고 관절은 위쪽 끝(`HingeJoint2D.anchor`)에 있어서, `localRotation`만 바꾸면 다리가 고관절이 아니라 허벅지 한가운데를 축으로 돌아 몸에서 분리돼 보인다. 그래서 매번 `localPosition = connectedAnchor - (localRotation * anchor)`로 위치를 함께 보정한다 — 이 식은 HingeJoint2D의 구속 조건 그 자체이므로, RAGDOLL에서 관절을 다시 켤 때 위치 오차로 인한 튕김이 구조적으로 발생하지 않는다. anchor 두 값은 하드코딩하지 않고 관절에서 읽어 쓴다(프리팹 배치가 바뀌어도 자동 추종). 제공 기능: Idle 중립 포즈 / Walk 사인파 포즈(왼다리·오른다리 반대 위상, 팔은 다리와 반대 위상·진폭 축소) / GETUP 포즈 보간 / 디버그용 각도 조회.
- **`States/RagdollRig.cs` (재작성)** — 물리 모드 전환의 **단독 소유자**로 승격. `EnterActiveMode()`(관절 disable → 팔다리 Kinematic → 루트 `constraints |= FreezeRotation`) / `EnsureRagdollMode()`(루트 제약 해제 → 팔다리 Dynamic → 관절 enable) / `SnapRootUpright()` / `TickGetupRoot(progress)`. 두 모드 전환 메서드 모두 멱등이며 실제 모드가 바뀔 때만 컴포넌트를 건드린다. `EnterRagdoll()`은 `EnsureRagdollMode()` + BUG-SW-M4 각속도 완충(진입 이벤트당 1회) — 완충을 멱등 버전에 두면 매 프레임 0.5배가 곱해져 RAGDOLL이 아예 회전하지 못하는 정반대 버그가 되므로 의도적으로 분리했다. **관절을 Kinematic만으로 무력화하지 않고 `enabled=false`까지 끄는 이유**: HingeJoint2D는 두 바디 중 Dynamic인 쪽(=루트)을 움직여 구속을 만족시키려 하므로, 팔다리가 Kinematic이어도 살아있는 관절이 루트를 미세하게 흔들 수 있다.
- **`States/StickmanBlackboard.TickPose()` (신규)** — 물리 모드 + 포즈의 **단일 진실 공급원**. `StickmanAgent.Update()`가 `_machine.Tick()` **직후** 매 프레임 1회 호출한다. 규칙: `Ragdoll`→물리 위임 / `Getup`→능동 모드 복귀하되 루트 각도는 GetupState가 보간 / `Walk`→능동 모드 + 직립 스냅(팔다리는 WalkState가 이미 사인파로 세팅) / 그 외 능동 상태 전부→능동 모드 + 직립 스냅 + Idle 중립 포즈. **각 상태의 Enter/Exit에 흩어놓지 않은 이유**: 상태가 14개가 넘고 하나라도 물리 모드 복구를 빠뜨리면 그 상태에서만 캐릭터가 다시 무너진다(실제로 예전 구현이 그렇게 무너졌다). 게다가 전체화면 Suspend의 강제 취소, 테스트의 직접 `ChangeState`, `ReportExternalImpact`의 강제 인터럽트 등 상태 밖에서 상태가 바뀌는 경로가 여럿이라, "지금 상태 ID가 무엇인가"만 보고 매 프레임 멱등 재적용하면 그 모든 경로가 자동으로 커버된다. `StickmanAgent.Awake()`에서도 1회 호출해 첫 FixedUpdate 이전에 직립 포즈를 확정한다.
- **`States/GetupState.cs`** — 모터 비례 제어(수렴 보장 없음 → 반쯤 일어난 채 Idle로 넘어가는 경로가 존재했다)를 **결정론적 보간 2갈래**로 교체: `RagdollRig.TickGetupRoot()`가 루트 회전각을, `StickmanPoseAnimator.TickGetupPose()`가 팔다리 각도를 같은 progress로 "널브러진 실제 각도 → 직립 중립 각도"로 직접 보간. `progress=1`이면 반드시 정확히 직립이므로 **기상 실패라는 경로가 없다**.
- **`States/WalkState.cs`** — 모터/각도제한 설정 코드 제거, `TickWalkPose()` 호출로 교체. `Exit()`는 이제 할 일이 없다(다음 프레임 `TickPose()`가 자동 복구).
- **`Editor/SceneBootstrapper.cs`** — 프리팹 저장값 자체를 능동 모드 기본값으로: 루트 `rb.constraints = FreezeRotation`, 팔다리 `bodyType = Kinematic`, `joint.enabled = false`. 또한 **어깨 부착점 X를 ±0.28 → ±0.05로 이동** — 예전에는 팔이 몸통(x=0)에서 너무 멀어 "몸에 붙은 팔"이 아니라 옆에 따로 뜬 두 막대로 보였다. 벌어짐은 이제 위치가 아니라 각도(`idleArmSpreadDegrees`)가 만든다.
- **`Core/StickConfig.cs`** — 모터 관련 5개 필드(`walkCycleMotorGain`/`walkCycleMaxMotorTorque`/`walkCycleMaxMotorSpeedDegPerSec`/`walkCycleLegAngleLimitDegrees`/`walkCycleArmAngleLimitDegrees`)와 `getupMotorGain`/`getupMaxMotorTorque` 제거 — 절차적 제어에는 토크/게인/각도제한이라는 개념이 존재하지 않는다(목표 각도가 곧 실제 각도). 신규: `idleLegSpreadDegrees=13`, `idleArmSpreadDegrees=32`, `walkCycleArmSwingRatio=0.55`. `walkCycleFrequencyPerSpeed`는 모터 추종 지연이 사라져 0.45→0.6, `walkCycleLegSwingDegrees` 18→22로 복원(모터 시절 안전 하향값이 더 이상 필요 없음).

### Idle 기본 포즈 정의 (사용자 확정 참고 실루엣)

```
   O      머리(속이 빈 원)
   |      몸통
  /|\     팔: 어깨에서 바깥·아래로 ±32도
  / \     다리: 고관절에서 바깥·아래로 ±13도
```

부호 규약: 팔다리 로컬 +y가 관절 쪽, Z축 양의 회전이 손/발을 +x로 보낸다 → 왼쪽 = 음수각, 오른쪽 = 양수각.

### 실측 검증

- **컴파일**: Unity 배치모드 — `error CS`/`warning CS` 매치 **0건**.
- **EditMode**: `total="13" passed="13" failed="0"`.
- **PlayMode**: `total="3" passed="3" failed="0"` (기존 3종 테스트 무수정 통과 — 새 구조가 기존 검증 의도를 그대로 만족).
- **`--force` 재생성 후 프리팹 실측**: 루트 `m_BodyType: 0` + `m_Constraints: 4`(=FreezeRotation), 팔다리 4개 전부 `m_BodyType: 1`(Kinematic) + 관절 `m_Enabled: 0`, 어깨 `m_ConnectedAnchor: {x: ±0.05, y: 1.3}`.
- **Standalone 빌드**: `총 에러 0건, 총 경고 0건`.
- **실제 `.app` 90초+ 연속 실행 실측**(임시 디버그 로그 `[POSE-DIAG][TEMP]`, 0.5초 간격): **루트 `rootRotZ`가 능동 상태 전 구간 `0.00` 유지**(= 이번 라운드 성공 판정 기준 충족), Idle에서 `legs=(-13.0,13.0) arms=(-32.0,32.0)` 정확히 고정, Walk에서 다리 각도가 사인파로 진동, 강제 RAGDOLL 트리거 후 Dynamic 전환 → GETUP → 다시 Kinematic + `rootRotZ=0.00` 직립 복귀 확인. 검증 후 임시 로그 전량 제거하고 클린 재빌드.
- **기존 버그 재발 없음**: 낙하 고착(`pos.y`가 `-3.00` 안정 유지), 랙돌 폭주(강제 트리거 외 RAGDOLL 진입 0건), 화면 가시성(PlayMode `StickmanOnScreenFramingTests` 통과).

**결론**: 지난 여러 라운드가 모터 파라미터를 반복해서 낮추는 완화책이었던 데 반해, 이번에는 "능동 상태에서 물리를 쓴다"는 전제 자체를 제거했다. 이제 캐릭터가 넘어지는 것은 **물리적으로 불가능**하다(루트 회전을 만들 수 있는 주체가 하나도 남지 않는다) — RAGDOLL 모드로 명시적으로 전환될 때만 물리가 개입한다. git commit은 하지 않음.

### 리더 지적 반영 재수정 (같은 라운드, 2026-08-28) — "움직임이 이상하고 관절이 나눠져있음"

사용자가 새 빌드 스크린샷으로 **직립은 성공**했음을 확인했으나 두 가지를 지적했고, 리더가 코드로 원인을 확정해 지시했다. 두 지적 모두 반영했다.

**(1) 관절이 나눠져 보임 — 팔다리 transform 원점을 관절 부착점으로 이동**

`transform.localRotation`은 항상 그 transform의 **원점**을 중심으로 회전한다. 기존 프리팹은 팔다리 GameObject의 원점이 팔다리 **한가운데**에 있고 관절 위치는 `HingeJoint2D.anchor=(0,+halfLength)`로만 표현돼 있었다. 능동 상태에서는 관절이 꺼져 있어 `anchor`가 시각적 회전에 아무 영향을 주지 않으므로, 원점을 그대로 둔 채로는 `StickmanPoseAnimator`의 위치 보정식(`localPosition = connectedAnchor - rotation*anchor`)에 **전적으로 의존**해야 했다 — 리더가 지적한 대로 이 구조 변경으로 생긴 사각지대다.

`Editor/SceneBootstrapper.CreateLimb()`를 재작성해 기하학 레벨에서 문제를 제거했다: 팔다리 GameObject `localPosition = attachLocal`(관절 부착점 그 자체), LineRenderer는 `(0,0) → (0,-length)`, EndMark(손/발 점)는 `(0,-length)`, `BoxCollider2D.offset = (0,-length/2)`, `HingeJoint2D.anchor = (0,0)`, `connectedAnchor = attachLocal`. 시그니처도 `worldSize/localPos/anchor/connectedAnchor` 4개에서 `attachLocal/length/thickness` 3개로 줄여, **시각·물리·회전축이 전부 하나의 값(`attachLocal`)에서 파생**되므로 서로 어긋나는 것 자체가 불가능해졌다.

추가로 리더 지적대로 `hipX`를 `0.12 → 0.05`로 줄였다 — 고전 졸라맨은 다리가 몸통 끝 한 점에서 갈라져 나오는 형태(`/ \`)인데, ±0.12는 몸통 선(x=0)과 다리 사이에 가로 틈을 만들어 스크린샷에서 눈에 띄었다. 벌어짐은 이제 위치가 아니라 각도가 만든다. 중립 벌림각도 리더 제시 범위에 맞춰 `idleLegSpreadDegrees` 13→**12**, `idleArmSpreadDegrees` 32→**30**으로 조정(`.asset`도 함께 갱신).

`StickmanPoseAnimator.ApplyAngle()`의 보정식은 **그대로 유지**했다. 새 기하학에서는 `anchor=(0,0)`이라 결과가 상수(`connectedAnchor`)로 축약되지만, (a) RAGDOLL 도중 물리가 팔다리를 끌고 다닌 뒤 GETUP 복귀 시 위치를 부착점으로 확실히 되돌려주고, (b) 나중에 프리팹 기하학이 다시 바뀌어도 시각이 조용히 깨지지 않게 하는 이중 안전장치이기 때문이다.

**(2) 움직임 자체가 이상함 — 보행 사이클을 "점핑잭"에서 "보폭 교차"로 교체**

최초 구현은 Idle 중립각(바깥으로 벌린 `±spread`)을 기준으로 좌우 **대칭**으로 흔들었다(`left = -(spread+swing)`, `right = +(spread+swing)`). 수학적으로는 반대 위상이 맞지만, 시각적으로는 두 다리가 "벌어졌다 오므렸다"를 반복해 **걷는 게 아니라 제자리 점핑잭**처럼 보인다(실측 로그: `legs=(-27.5,27.5) → (1.5,-1.5) → (-35.0,35.0)`, 항상 좌우 대칭).

졸라맨의 `/ \` 벌림은 정면을 향한 **정지 자세**이고 보행 사이클은 측면도로 읽히는 게 표준이므로(한쪽 다리는 앞, 반대쪽은 뒤), Walk 중에는 중립각 0을 기준으로 순수하게 앞뒤 교차시키도록 바꿨다: `leftLeg=+stride`, `rightLeg=-stride`, `leftArm=-stride*ratio`, `rightArm=+stride*ratio`(같은 쪽 팔은 다리와 반대 — 실제 보행 반동). Idle↔Walk 전환 시 각도가 순간이동하지 않도록 `WalkPoseBlendSeconds=0.18`초 선형 보간(`_walkBlend`)을 추가했다. 재실측 로그: `legs=(17.0,-17.0) → (-2.6,2.6) → (-21.8,21.8)`, `arms=(-14.6,14.6) → (1.4,-1.4) → (12.0,-12.0)` — 다리가 0을 통과하며 앞뒤로 교차하고 팔이 반대로 흔들리는 정상 보행 패턴.

**재검증**: 컴파일 `error CS`/`warning CS` 0건 / EditMode 13-13 / PlayMode 3-3 / Standalone 빌드 에러0·경고0 / 프리팹 실측(팔다리 `m_LocalPosition {x:±0.05, y:0.6 또는 1.3}`, `m_Anchor {0,0}`, `m_Offset {0,-0.3 또는 -0.25}`, 루트 `m_Constraints: 4`) / 실제 `.app` 90초+ 실행에서 `rootRotZ` 전 구간 `0.00` 유지.

### 리더 지정 좌표 기반 지오메트리 재작업 + 부드러운 보간 + 눈 추가 (같은 라운드 3차, 2026-08-28)

사용자 스크린샷 판독 결과 리더가 (1) 팔이 안 보임 (2) 엉덩이 검은 뭉치 (3) 눈 없음 (4) 몸통이 짧음을 지적하고 정확한 좌표를 지정했다. 지정 좌표를 그대로 구현했다.

**(1) 지오메트리 — 리더 지정 좌표 그대로 + 접지 보정**

리더 지정(루트 로컬): 머리 중심 `(0,1.60)` 반경 `0.25` / 몸통 `(0,1.35)→(0,0.45)` / 어깨 `(0,1.15)` / 엉덩이 `(0,0.45)` / 팔 길이 `0.5` 중립 `±40°` / 다리 길이 `0.6` 중립 `±18°`. **좌우 팔다리가 모두 x=0인 같은 점에서 시작**한다 — 직전 라운드에 어깨를 `±0.05`로 두었더니 팔이 거의 수직인 순간 몸통 선과 완전히 겹쳐 **팔이 아예 안 보였다**. 레퍼런스 졸라맨은 팔다리가 몸통 위 한 점에서 갈라져 나오고 벌어짐은 전적으로 **각도**가 만든다.

**접지 보정(`footLift`)**: 지정 좌표를 그대로 쓰면 중립 발끝 y = `0.45 - 0.6·cos(18°) = -0.12`로 루트 원점보다 아래로 내려가는데, 이 프로젝트는 `GroundSensor`/`SnapToGround()`가 **"루트 원점 = 발 높이"**를 전제로 접지를 계산한다. 실루엣(상대 거리·각도)은 지정값 그대로 두고 **전체를 낙차만큼 위로 평행이동**했다(`footLift = legLength·cos(idleLegSpread) - specHipY ≈ 0.1206`). 하드코딩이 아니라 실제 다리 길이/각도에서 유도하므로 나중에 값이 바뀌어도 접지가 자동으로 맞는다. 프리팹 실측: 엉덩이 `y=0.5706`, 어깨 `y=1.2706`, 머리 `y=1.7206`, 몸통 중심 `y=1.0206`(길이 0.9 = 머리 지름의 1.8배, "몸통이 짧다" 지적 반영).

중립 각도는 **`transform.localRotation` 초기값**으로만 준다(LineRenderer는 항상 로컬 `(0,0)→(0,-length)`로 곧게 그린다) — 선을 비스듬히 그린 뒤 회전까지 시키면 각도가 이중으로 더해진다. 프리팹 실측: `LeftLeg -18.00°`, `RightLeg +18.00°`, `LeftArm -40.00°`, `RightArm +40.00°`.

**끝점 마커**: `CreateEndMark`를 일반화한 `CreateFilledDot()`으로 교체하고 **손끝/발끝 4개에만** 그린다(프리팹 실측 `EndMark count = 4`, 위치 `(0,-0.6)`×2 / `(0,-0.5)`×2). 부착점 쪽에는 마커가 없다 — 그게 검은 뭉치의 원인이었다.

**(2) 부드러운 움직임 — 지수 감쇠 보간(사용자가 두 번 강조)**

목표각 즉시 대입을 폐기하고 3중 스무딩을 넣었다. 전부 **프레임레이트 독립**이다:
- **각도**: `Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-rate*dt))`. 리더가 금지한 `Lerp(a,b,rate*dt)`(fps에 따라 수렴 속도가 달라짐)는 쓰지 않았다. 계수는 신규 `StickConfig.poseSmoothingRate = 14`.
- **위상 적분**: `_phase += 2π·f·speed·dt`. 시간×주파수 방식은 걷는 속도가 바뀌는 순간 위상이 통째로 점프해 다리가 툭 튄다.
- **속도 스무딩**: 주파수 입력이 되는 수평 속도 자체를 `walkSpeedSmoothingRate = 6`으로 감쇠 — 걷기 시작/멈춤에 주파수가 튀지 않고, 0에서 차오르며 보폭이 자연스럽게 빨라진다.

Walk 이탈 시 즉시 리셋하던 문제도 자동 해소됐다: `TickPose()`가 같은 스무딩 경로로 Idle 중립 포즈를 적용하므로 다리가 서서히 모인다(`_walkBlend` 특수 처리는 제거 — 지수 감쇠가 그 역할을 포함한다). RAGDOLL→능동 전환 프레임에는 `SyncFromTransform()`으로 보간 상태값을 물리가 만든 실제 각도에 동기화해, 랙돌 이전의 낡은 각도에서 튀지 않게 했다. 진폭도 하향(`legSwing` 22→20, `frequencyPerSpeed` 0.6→0.55, `armSwingRatio` 0.55→0.5). Walk 중 팔은 항상 중립의 45%(`ArmWalkBaseRatio`)만큼 바깥으로 벌린 채 흔들려 몸통에 가려 사라지지 않는다.

**(3) 눈 추가 (시각만, 커서 추적은 다음 단계)**

머리 GameObject의 **자식**으로 눈동자 점 2개(`LeftEye`/`RightEye`, localPosition `(±0.09, +0.02)`, 반경 `0.035`, sortingOrder 4 = 머리 링(3)보다 위)를 추가했다 — 자식이라 RAGDOLL로 머리가 뒹굴 때도 따라간다. 신규 `States/EyeController.cs`(순수 C# 클래스, `RagdollRig`/`StickmanPoseAnimator`와 같은 컨벤션)가 `SetLookDirection(Vector2)` / `LookForward()`를 제공하며, 눈동자 이동 범위는 `MaxPupilOffset = 0.055`로 제한해 어떤 방향으로도 머리 링(반경 0.25) 밖으로 나가지 않는다(`0.09+0.055+0.035 = 0.18 < 0.25`). 지시대로 **커서 추적 로직은 구현하지 않았고** 지금은 항상 정면을 본다. 다음 라운드 배선 지점(`StickmanBlackboard.TryGetCursorWorldPosition` → 머리 기준 방향 벡터 → `SetLookDirection`, 호출 위치는 `TickPose()`의 `LookForward()` 자리)을 클래스 문서에 3단계로 명시해뒀다.

### 사용자 피드백 4건 반영 (같은 라운드 4차, 2026-08-28) — 손발 점 제거 / 비율 / 눈 크기 / 뻣뻣함

리더가 사용자 피드백 4건을 좌표까지 지정해 전달했고, 직전 좌표 지시를 덮어쓰는 것으로 처리했다.

**(1) 손/발 끝 점 완전 제거** — 사용자 "손과 발에 동그란 뭉치같은건 필요없을거 같은데". `CreateEndMark()`와 모든 호출부, 전용 상수(`HandFootDotRadius`/`HandFootDotLineWidth`)를 제거했다. 팔다리는 그냥 선으로 끝난다. 원 근사 세그먼트 상수만 `FilledDotSegments`로 이름을 바꿔 눈동자 전용으로 남겼다. 프리팹 실측: `EndMark` GameObject **0개**(계층에 `Head/LeftEye/RightEye/Torso/LeftArm/RightArm/LeftLeg/RightLeg/Stickman`만 존재).

**(2) 비율 재조정** — 사용자 "팔 몸 다리 비율이 이상하고". 리더 지정값 그대로: 머리 반경 `0.25→0.22`, 팔 길이 `0.5→0.75`, 다리 길이 `0.6→0.95`, 다리 중립 벌림 `18°→12°`, 어깨 `(0,1.18)`, 엉덩이·몸통 유지. 머리 중심은 고정 상수 대신 **몸통 상단에서 유도**(`headY = torsoTopY + HeadVisualRadius`)해 몸통 길이나 머리 반경을 바꿔도 목이 끊기거나 파묻히지 않게 했다. 루트 `CapsuleCollider2D`도 고정 1.8이 새 비율과 어긋나므로 전신 높이에서 유도(`size.y = totalHeight`, `offset.y = totalHeight/2`)해 발끝~머리끝을 정확히 덮게 했다 — RAGDOLL이 바닥에 자연스럽게 눕는 데 필요하다.

**접지 재계산**: 다리가 길어지고 각도가 줄어 낙차가 `0.6·cos18°=0.571` → `0.95·cos12°=0.929`로 커졌다. `footLift`는 하드코딩이 아니라 이 낙차에서 유도하므로 자동으로 따라왔다. 프리팹 실측: 엉덩이 `y=0.9292`, 어깨 `y=1.6592`, 몸통 `0.9292~1.8292`, 머리 `y=2.0492`, 캡슐 `size.y=2.2692 offset.y=1.1346`. **중립 발끝 y = 0.9292 − 0.95·cos(12°) = 0.0000** — 루트 원점(=지면)에 정확히 닿는다. PlayMode 프레이밍 테스트도 통과.

**(3) 눈 크기 축소** — 사용자 "눈도 너무 커서 이상함". 눈동자 반경 `0.035→0.018`, 위치 `(±0.09,+0.02)→(±0.075,+0.02)`. `EyeController.MaxPupilOffset`도 `0.055→0.05`로 맞춰 재계산했다(`0.075+0.05+0.018 = 0.143 < 0.22` — 어떤 방향으로 밀어도 머리 링 밖으로 안 나간다).

**(4) 뻣뻣함 해소 — 지수 감쇠 위에 3가지 추가** (사용자 "너무 뻣뻣하게 움직임")
- **상하 바운스**: Walk 중 몸 전체를 보행 사인파의 **2배 주파수**로 `walkBounceAmplitude=0.03` 유닛만큼 오르내린다(`(0.5 - |sin(phase)|) × amplitude` — 다리가 모였을 때 높고 벌어졌을 때 낮은, 실제 보행의 그 리듬). **핵심 제약 준수**: `Rigidbody2D.position`은 절대 건드리지 않는다(접지 판정이 루트의 물리 위치를 발 높이로 쓴다). 대신 `SetBodyOffset()`이 시각 오브젝트(Torso/Head)의 `localPosition`과 **팔다리 부착점 오프셋**에 같은 값을 적용해 몸이 통째로 움직이면서도 팔다리가 몸에서 떨어지지 않게 했다.
- **팔 follow-through**: 팔의 보간 계수를 다리의 `ArmSmoothingRatio=0.55`배로 낮춰 팔이 살짝 늦게 따라온다(사지가 전부 같은 타이밍에 맞아떨어지면 로봇처럼 보인다). 위상은 지시대로 다리와 정확히 반대(180°)를 유지한다.
- **Idle 미세 호흡**: 완전 정지 대신 `idleBreathFrequencyHz=0.8`(약 1.25초 주기)로 몸 전체가 `idleBreathAmplitude=0.012` 유닛 오르내리고, 양팔 각도가 `idleBreathArmDegrees=1.5`도 범위에서 아주 천천히 벌어졌다 모인다. 실측 로그에서 Idle 팔 각도가 `-39.9 / -41.1 / -38.8`로 중립 −40 주위를 미세 진동하는 것을 확인했다(다리는 −12/12 고정).
- GETUP 중에는 바운스/호흡을 끈다(`SetBodyOffset(0)`) — 기상 보간과 연출이 겹쳐 보이지 않게.

### 팔다리 2분절(무릎/팔꿈치) + Alan Becker 레퍼런스 시각 스타일 전면 교체 (같은 라운드 5차, 2026-08-28)

사용자가 **"손이랑 다리가 다 그냥 막대기 같음"**이라고 지적했고, 이어서 **정확한 시각 레퍼런스 이미지**(Alan Becker "Animator vs Animation" 계열 스틱맨)를 제시했다. 리더가 두 건을 합쳐 지시했고, 이 지시가 이전 모든 시각 관련 지시보다 우선한다.

**(1) 팔다리 2분절 재구성 — 뻣뻣함의 진짜 근본 원인**

기존 팔다리는 각각 **곧은 선 하나**이고 부착점 한 곳에서만 회전했다. 그러면 보간을 아무리 부드럽게 하고 몸 바운스를 넣어도 "막대기가 흔들리는" 것 이상이 될 수 없다 — 사람이 걷는 게 자연스러워 보이는 결정적 이유는 **무릎과 팔꿈치가 접히기 때문**이고, 레퍼런스도 확실히 무릎이 접힌다. 각 팔다리를 2마디로 재구성했다:

- 위 마디(대퇴/상완): 부모=root, 원점=부착점, 선 `(0,0)→(0,-lenUpper)`.
- 아래 마디(정강이/전완): **위 마디의 자식**, 원점=무릎/팔꿈치 `(0,-lenUpper)`, 선 `(0,0)→(0,-lenLower)`.
- 길이: 팔 0.75 = 상완 `0.38` + 전완 `0.37`, 다리 0.95 = 대퇴 `0.50` + 정강이 `0.45`.

계층 부모-자식이므로 위 마디를 돌리면 아래 마디가 딸려오고, 아래 마디를 추가로 돌리면 관절이 접힌다(아래 마디의 `localRotation`이 곧 "몇 도 접혔는가").

**뒤로 안 꺾이는 보장**: 굽힘량을 `Max(0, sin(t + phase))`로 계산해 **절대 음수가 되지 않게** 하고 거기에 고정 부호(`KneeBendSign = -1`, `ElbowBendSign = +1`)를 곱한다 — 무릎/팔꿈치가 반대로 꺾이는 경우의 수가 산술적으로 존재하지 않는다. 보행 각도는 엉덩이 `±20°` 사인파, 무릎 `4° + 30°·Max(0,sin(t+π/2))`, 어깨는 다리와 반대 위상 `20°×0.85`, 팔꿈치 `10° + 15°·Max(0,sin(t+π/2))`. **Idle에서도 완전히 펴지 않는다**(무릎 `4°`, 팔꿈치 `10°`) — 완전히 곧은 상태가 바로 "막대기" 느낌의 원인이다.

**RAGDOLL 대응**: 마디가 8개로 늘었지만 `RagdollRig`는 `GetComponentsInChildren`로 순회하므로 코드 수정 없이 전부 잡힌다. 각 마디에 `StickmanLimb` 레이어/`RagdollLimbImpactRelay`/`Rigidbody2D`(Kinematic 기본)/`HingeJoint2D`(비활성 기본)를 전부 부여했고, 무릎/팔꿈치 관절에는 `useLimits`로 각도 제한을 걸었다(프리팹 실측: 무릎 `-100~+5`, 팔꿈치 `-5~+100`). **정직한 한계**를 코드 주석에 기록했다 — `HingeJoint2D`의 각도 제한은 관절이 enable될 때의 상대 자세를 기준으로 해석되므로 RAGDOLL 진입 시점 포즈가 기준이 된다(항상 해부학적 0도 기준은 아니다). 그래도 진입 자세에서 크게 벗어나는 과신전은 확실히 막힌다.

**접지 재계산**: 무릎이 굽으면 발끝 y가 올라간다. `LimbDrop()`이 대퇴는 `hipAngle`, 정강이는 `hipAngle + 무릎각`의 **누적 각도**로 각각 계산해 더하고, 좌우 낙차 중 **큰 쪽**을 기준으로 `footLift`를 잡아 어느 발도 지면 아래로 내려가지 않게 했다(무릎 부호가 좌우 공통이라 낙차가 0.013유닛 차이 난다 — 육안 구분 불가). 프리팹 실측: 엉덩이 `y=0.9347`, 오른발 끝 `0.9347 − (0.5·cos12° + 0.45·cos8°) = 0.0000`(지면에 정확히 접촉), 왼발 끝 `+0.013`.

**(2) 시각 스타일 전면 교체 (레퍼런스 반영)**

- **굵은 획**: `LineWidth` `0.05 → 0.11`(몸통), 다리 `0.12`, 팔 `0.10`. 레퍼런스는 가는 실선이 아니라 굵은 검은 획이다.
- **둥근 캡**: `numCapVertices`/`numCornerVertices` `4 → 8`. 이게 이번 라운드의 숨은 이득이다 — 굵은 선 + 둥근 캡이면 관절에서 둥근 끝끼리 자연스럽게 겹쳐 **우리가 계속 고생한 "관절이 나눠져 보임"/"검은 뭉치" 문제가 저절로 해결된다**. 리더 명시: 관절 부위가 살짝 뭉쳐 보이는 게 이 스타일에서는 정상이다.
- **머리를 채워진 검은 덩어리로**: 링(테두리만) 방식(`CreateHeadRingVisual`)을 폐기하고 `CreateFilledHead()`로 교체 — 길이 0인 선분에 지름만큼의 선 폭 + 둥근 캡(`HeadCapVertices=16`)을 주면 LineRenderer 하나로 완전히 채워진 원이 나온다(SpriteRenderer 재도입 없이 "LineRenderer만 사용" 컨벤션 유지).
- **눈을 배경색으로**: 머리가 검게 꽉 차 검은 눈은 보이지 않으므로 눈동자를 `StickConfig.backgroundFallbackColor`(밝은 회색)로 그린다 — 검은 머리에 밝은 점 두 개. sortingOrder 4(머리 3보다 위).
- **목 연결**: 몸통 위쪽 끝을 머리 반경의 절반만큼 머리 원 안으로 파고들게 배치해 굵은 획이 머리 덩어리와 자연스럽게 이어지게 했다.

**(3) 유지된 것**: 지수 감쇠 보간, 몸 상하 바운스, 팔 follow-through 지연(+ 아래 마디는 위 마디보다 더 느슨한 `LowerSegmentSmoothingRatio=0.75`로 관절 연쇄 시차 추가), Idle 미세 호흡, 루트 회전 고정, 능동 Kinematic + RAGDOLL만 물리, 부착점 x=0 공유, 손발 끝 점 없음, `runInBackground`.

**정직한 실측 한계(RAGDOLL 중 관절 제한)**: 능동 상태의 "무릎/팔꿈치가 절대 뒤로 안 꺾임"은 산술적으로 보장된다(`Max(0,·)` × 고정 부호). 반면 **RAGDOLL 중**에는 실측 로그에서 팔꿈치가 제한 범위(`-5~+100`)를 넘어 `-59°`까지 간 순간이 관찰됐다 — `HingeJoint2D`의 각도 제한이 `enabled` 토글 시점의 상대 자세를 기준으로 재해석되기 때문으로 보인다(코드 주석에 이미 기록). RAGDOLL은 원래 "전신이 물리에 완전히 위임되어 아무렇게나 뒹구는" 구간이고 GETUP이 항상 정확히 중립 포즈로 복귀시키는 것을 실측 확인했으므로(아래 로그) 기능상 문제는 없지만, 완전한 해부학적 제한이 필요하면 다음 라운드에서 `LimitJoint2D` 조합이나 진입 시 `referenceAngle` 보정을 검토해야 한다.

### 얼굴 색 정정 + "뒤로 걷는" 문제 해결 (같은 라운드 6차, 2026-08-28)

**(1) 얼굴 = 흰색 채움 + 검은 테두리 (리더 직전 지시 정정)**

사용자 정정: "얼굴은 흰색에 눈이 검은색이어야지". 리더가 직전에 지시한 "머리를 꽉 찬 검은 덩어리로, 눈은 배경색 밝은 점으로"를 뒤집었다. 배경이 밝은 회색(`backgroundFallbackColor`)이라 흰 얼굴만으로는 배경과 구분되지 않으므로 검은 테두리가 필수다. sortingOrder로 세 겹을 쌓는다:

- `3` — `CreateFilledHead()`가 그리는 **흰색 채움**(길이 0 선분 + 지름만큼의 선 폭 + 둥근 캡 16). 이 오브젝트가 이름 `Head`이자 `CircleCollider2D`(반경 0.4, 무변경)와 `StickmanPoseAnimator`의 몸 바운스/`EyeController`의 부모 노릇을 겸한다.
- `4` — `CreateRing()`가 그리는 **검은 테두리 링**(`HeadOutlineWidth = 0.09`, 팔다리 획 0.10~0.12보다 약간 얇게).
- `5` — **검은 눈동자 점 2개**(반경 0.018, `(±0.075, +0.02)`).

프리팹 실측: `Head` width `0.44`(=반경 0.22×2) capVerts `16`, `HeadOutline` width `0.09` 24점 링, 눈 order `5`. 몸통/팔다리는 지시대로 굵은 검은 획 유지(몸통 `0.11`, 다리 `0.12`, 팔 `0.10`, capVerts `8`).

**(2) "이상하게 뒤로 걸어" — 좌우 미러링 도입**

원인은 리더 진단 (a): 캐릭터가 왼쪽으로 이동해도 다리 스윙/무릎 접힘 방향은 오른쪽 기준으로 고정돼 있어 문워크처럼 보였다.

**구현 방식 차이(정직한 기록)**: 리더는 "시각 전용 부모 오브젝트의 `localScale.x`를 ±1로 뒤집으라"고 지시했지만, **최종 각도에 방향 부호를 곱하는 방식**으로 구현했다. 근거: 이 캐릭터는 모든 시각 요소가 x=0 축 위에 있고(부착점·몸통·머리 전부 x=0) 좌우 차이를 만드는 것은 오직 각도뿐이라, 각도를 뒤집으면 **스케일 뒤집기와 시각적으로 완전히 동일한 결과**가 나온다. 그러면서 리더 자신이 경고한 위험("물리 루트의 스케일을 뒤집으면 콜라이더/조인트 계산이 꼬인다" — 2D 물리에서 음수 스케일은 콜라이더 뒤집힘/조인트 앵커 오차의 흔한 원인)을 새 계층 오브젝트를 만들지 않고 원천적으로 피한다. 피벗 X도 함께 미러링해 나중에 좌우 비대칭 배치가 생겨도 깨지지 않게 했다.

- `StickmanPoseAnimator._facingSign` + `SetFacing()`: 부호는 **최종 적용 지점(`ApplyAngle`)에서만** 곱한다 — `CurrentAngle`은 "방향 중립" 공간에 유지되므로 좌우가 뒤집히는 순간에도 지수 감쇠 보간 상태가 깨지지 않는다.
- `EyeController.SetFacing()`: 눈 중립 X도 함께 미러링해 "보고 있는 방향"이 몸 방향과 어긋나지 않게 한다.
- `StickmanBlackboard.TickPose()`가 `MoveInputX`의 부호로 매 프레임 갱신하되, **`moveInputDeadzone`을 넘을 때만** 바꾼다(0 근처에서 부호가 떨리면 캐릭터가 좌우로 깜빡인다). 정지 중에는 마지막 방향을 유지한다.

**실측 검증(PlayMode 로그)**: `moveX=-1.00 → facing=-1`일 때 `knees=(12.6, 6.0)`(양수), `moveX=+1.00 → facing=+1`일 때 `knees=(-11.5, -8.8)`(음수) — 이동 방향이 바뀌는 즉시 포즈 전체가 미러링되고, **무릎 굽힘은 어느 방향으로 걸든 항상 "진행 방향 기준 뒤쪽" 한 방향으로만 접힌다**(부호가 통째로 뒤집힐 뿐, 한 방향성은 그대로 유지). 팔꿈치도 동일하게 반대 부호로 일관된다.

---

## UniWindowController 도입 — 진짜 투명 데스크톱 오버레이 달성 (2026-08-28, Coder)

여러 라운드 실패했던 **"회색 창 안이 아니라 진짜 바탕화면 위에 투명하게 떠서 돌아다니게 만들기"** 를 이번 라운드에 달성했다(사용자 실측 확인 — 바탕화면과 Dock이 캐릭터 뒤로 그대로 비쳐 보이는 스크린샷). 자체 제작 Objective-C 플러그인을 전부 버리고 검증된 오픈소스로 교체한 것이 결정적이었다.

### (1) 설치 / 배선

- **패키지**: `Packages/manifest.json`에 UPM git 의존성 `"com.kirurobo.uniwinc": "https://github.com/kirurobo/UniWindowController.git#upm"` 추가(해석된 커밋 `304f9ba2aa4a`, 버전 0.9.8). `Packages/packages-lock.json`에 자동 기록됨. 동봉 네이티브 `LibUniWinC.bundle`은 x86_64+arm64 유니버설이라 Apple Silicon에서 추가 작업 불필요(빌드 산출물 `StickMate.app/Contents/PlugIns/LibUniWinC.bundle`에 정상 포함 확인).
- **asmdef**: `StickMate.Runtime.asmdef`의 `references`에 `Kirurobo.UniWindowController` 추가. `Assets/Editor/`는 asmdef가 없어(Assembly-CSharp-Editor) `autoReferenced: true`로 자동 참조된다.
- **어댑터**: `Platform/MacOS/MacWindowService.cs`의 `CreateOverlayWindow()`/`SetClickThrough()`/`SetAlwaysOnTop()`이 `[DllImport("StickMateOverlayPlugin")]` 대신 `UniWindowController`의 `isTransparent`/`isClickThrough`/`isTopmost`/`isHitTestEnabled`를 세팅한다. `EnumerateFootholds()`/`IsFullscreenAppActive()`/`ICursorPositionService`의 CoreGraphics 조회 경로는 무변경.
- **씬 자동 배치**: `SceneBootstrapper.ConfigureUniWindowController()`가 패키지 프리팹 `Packages/com.kirurobo.uniwinc/Runtime/Prefabs/UniWindowController.prefab`을 인스턴스화한다(경로를 못 찾으면 빈 GameObject + `AddComponent` 폴백). `--force`로 완전 재현 가능 — 수동 씬 편집 없음.

### (2) 자체 제작 플러그인 제거 범위 (참조 0건)

삭제: `Assets/Plugins/macOS/StickMateOverlayPlugin.m`(+`.meta`), `StickMateOverlayPlugin.bundle`(+`.meta`), `build.sh`(+`.meta`), 그리고 빈 `Assets/Plugins/` 폴더 트리 전체.
코드 제거: `MacWindowService`의 `[DllImport]` 4종(`SM_ConfigureOverlayWindow`/`SM_GetOverlayWindowLevel`/`SM_IsMainWindowFound`/`SM_GetMainWindowBackingScaleFactor`), `BuildStandalone.ConfigureNativePluginImporter()`와 `PluginAssetPath` 상수 및 그 호출부, `MacWindowEnumerationDiagnostic`의 `DllNotFoundException` 처리 분기.
남은 언급은 **"무엇을 왜 제거했는가"를 설명하는 문서 주석 3곳뿐**이며 실제 코드 참조는 0건이다.

### (3) 안전장치 — 유지하되 라이브러리 자동 제어와 충돌 해결 (중요)

`StickmanAgent`의 기존 안전장치(시작 후 5초 지연 + Escape 즉시 강제 해제)는 **로직 무수정**으로 그대로 유지된다. 다만 그대로 두면 무력화되는 함정이 있어 어댑터 쪽에서 해결했다:

> `UniWindowController`는 `isHitTestEnabled=true`일 때 **매 프레임** 커서 아래 픽셀 알파를 보고 `isClickThrough`를 자동으로 켜고 끈다(`UpdateClickThrough()`). 즉 Escape로 클릭관통을 꺼도 **다음 프레임에 라이브러리가 다시 켜버린다.**

그래서 `MacWindowService.SetClickThrough()`가 두 값을 함께 다룬다:
- `SetClickThrough(false)`(Escape 긴급 해제 / 시작 후 5초 구간) → `isHitTestEnabled=false` **+** `isClickThrough=false` — 자동 제어까지 정지시켜 해제가 실제로 "계속" 유지된다.
- `SetClickThrough(true)`(정상 오버레이) → `isClickThrough=true` + `isHitTestEnabled=true`.

### (4) `isHitTestEnabled` — Phase 3 "부분적 클릭관통 해제"를 대체 가능 (다음 라운드 과제)

**이 기능으로 `docs/UX_FLOW.md` 15절의 "부분적 클릭관통 해제"(`ILocalClickCaptureService`)를 대체 가능하다.** 그 인터페이스는 "진짜 OS 히트테스트는 별도 오버레이 창 없이는 불가능"이라며 상태 부기만 하고 실제 히트테스트를 미뤄뒀는데, `UniWindowController`의 `hitTestType = Opacity`가 바로 그것을 실제로 해준다 — 커서 아래 픽셀 알파가 `opacityThreshold`(0.1) 이상이면 클릭을 앱이 받고, 그 외 영역은 100% 관통된다. 이번 라운드에서는 **켜고 동작 확인까지만** 했고(실측 로그에서 `isHitTestEnabled=True` 확인), `ILocalClickCaptureService` 구조를 이 기능으로 전면 대체하는 리팩터링은 **다음 라운드 과제**다.

### (5) 실측으로 발견해 고친 2건 (둘 다 "부착 타이밍" 문제)

`UniWindowController`는 자기 NSWindow를 `Awake()`가 아니라 **첫 `Update()`** 에서 붙잡는다(`UpdateTargetWindow()` → `AttachMyWindow()`). 우리 배선 지점인 `StickmanAgent.Start()`는 그보다 먼저다.

1. **항상위(topmost)가 조용히 사라짐** — `SetTopmost(true)`는 `_isTopmost = _uniWinCore.IsTopmost`로 되읽는데 `IsTopmost`가 `IsActive && _isTopmost`라 부착 전엔 **무조건 false**로 되돌아간다. 실측: 로그 `SetAlwaysOnTop(True) 적용 완료 — isTopmost=False` + 외부 `CGWindowListCopyWindowInfo` 조회에서 `kCGWindowLayer=0`. → **신규 `Platform/MacOS/MacOverlayStateEnforcer.cs`** (런타임 전용, 씬 미저장)가 목표 상태를 들고 있다가 `windowSize != (0,0)`으로 부착을 확인한 뒤 0.5초 간격 5회 재적용하고 결과를 되읽어 로그로 남긴다.
2. **DPI 보정 실패** — 같은 이유로 `clientSize=(0,0)`을 읽어 `desktopDpiScale=1.000`(보정 없음)이 나왔다. → `DetectDesktopDpiScale()`을 **창이 아니라 디스플레이** 기준으로 재구현: `CGDisplayCopyDisplayMode` + `CGDisplayModeGetWidth`(포인트) / `CGDisplayModeGetPixelWidth`(백킹 픽셀). 부착 여부와 무관하게 항상 즉시 정확하다.

### (6) 헤드리스 크래시 — PlayMode 테스트가 통째로 죽던 사고

`-batchmode -nographics`에는 NSWindow가 **하나도 없어** 네이티브 `LibUniWinC._findMyWindow()`(Swift) 안에서 **프로세스가 통째로 크래시**했다(스택: `_findMyWindow` ← `AttachMyWindow` ← `UpdateTargetWindow` ← `Update`). PlayMode 테스트가 `EXIT=133`으로 죽고 결과 XML조차 생성되지 않았다.

**해결**: `SceneBootstrapper`가 이 GameObject를 **비활성(`SetActive(false)`) 상태로 씬에 저장**하고, 활성화는 실제 Standalone Player에서만 인스턴스화되는 `MacWindowService.CreateOverlayWindow()`가 담당한다(`ResolveController(activateIfInactive: true)` — `FindObjectsInactive.Include`로 비활성까지 찾은 뒤 `SetActive(true)`, 이때 `Awake()`가 동기 실행되어 `UniWindowController.current`가 채워진다). 부수 효과로 "에디터 Play 모드에서 에디터 자신의 창을 건드리는" 사고도 원천 차단된다 — 공식 문서도 "투명은 에디터에서 동작하지 않으니 빌드해서 테스트하라"고 경고한다.

### (7) 카메라 배경 — 알파 0 복귀 + 방어책 유지

`SceneBootstrapper`가 `clearFlags=SolidColor`, `backgroundColor = (backgroundFallbackColor.rgb, alpha 0)`으로 되돌렸다. **RGB는 밝은 회색 그대로 유지**한다 — 이전 라운드에서 확립된 방어책으로, 만에 하나 투명화가 실패해도 최악의 결과가 "밝은 회색 창 안의 검정 캐릭터"(최소한 보이는 상태)이지 "검정-on-검정"이 아니게 한다. 같은 이유로 `UniWindowController.autoSwitchCameraBackground = false`로 꺼둔다 — 켜져 있으면 라이브러리가 투명화 시점에 배경을 `Color.clear`(= RGB 0,0,0 + 알파 0)로 덮어써 이 방어책을 무력화한다.

### (8) 실측 검증 결과 (구체 수치)

Player.log 되읽음 값 (`MacOverlayStateEnforcer` 로그):
```
isTransparent=True, isTopmost=True, isClickThrough=True, isHitTestEnabled=True
windowSize=(1512, 846), clientSize=(1512, 846), windowPosition=(0, 75)
cameraBg=clearFlags=SolidColor, rgba=(0.94,0.94,0.94,0.00)
desktopDpiScale=0.500  (디스플레이 포인트폭 1512 / 백킹픽셀폭 3024 = backingScaleFactor 2.000)
```
외부 독립 프로세스(Swift + `CGWindowListCopyWindowInfo`, `Assets/Editor/MacWindowEnumerationDiagnostic.cs`와 같은 방식)로 조회한 우리 창(`winID` 메인 창):
```
변경 전:  kCGWindowAlpha=1.0  kCGWindowLayer=0    kCGWindowIsOnscreen=true
변경 후:  kCGWindowAlpha=1.0  kCGWindowLayer=101  kCGWindowIsOnscreen=true
```
- `kCGWindowLayer` `0 → 101`: 항상위가 **윈도우서버 레벨에서 실제로 적용됨**을 확인(`Enforcer` 재적용 전에는 0이었다).
- `kCGWindowAlpha=1.0`은 **정상이다** — 이 값은 `NSWindow.alphaValue`(창 전체 균일 불투명도)이지 픽셀별 알파가 아니다. 픽셀 단위 투명은 이 필드에 나타나지 않으며, 실제 투명 성공은 사용자 스크린샷(바탕화면/Dock이 그대로 비쳐 보임)으로 확인했다.
- 스크린샷은 이 환경에 Screen Recording 권한이 없어 에이전트 쪽에서는 불가능(시도하지 않음).

기준선: **컴파일 에러 0 / 경고 0**, **EditMode 13/13**, **PlayMode 3/3**, 빌드 `Succeeded`(에러 0, 경고 0).

### (9) 같은 라운드 사용자 피드백 3건 반영

**(a) 얼굴을 "비워서" 투명하게** — 사용자: "얼굴이 흰색이 아니고 색 자체가 없어야지, 비워져있어야함". 직전의 "흰 채움 + 검은 테두리"는 **불투명 회색 배경을 전제로 한 설계**였다. 진짜 투명 창이 동작하게 됐으므로 흰 채움 원을 제거했다 — `CreateFilledHead()`(LineRenderer로 흰 원을 그리던 함수)를 **`CreateHeadAnchor()`**(렌더러 없는 순수 앵커)로 대체. `Head` GameObject 자체는 그대로 남는다(`CircleCollider2D`, `StickmanPoseAnimator`가 이름 "Head"로 찾는 몸 바운스 기준, `EyeController`의 눈 부모 역할). 최종 모습: **검은 링 + 그 안에 검은 점 2개, 나머지는 전부 투명**. 프리팹 실측: 흰색 `(1,1,1,1)` 항목 0건, LineRenderer 12개(몸통 1 + 머리링 1 + 눈 2 + 팔다리 8)로 흰 채움 1개가 정확히 사라졌다. 사용하지 않게 된 `HeadCapVertices` 상수도 제거.

**(b) 계단 현상(알파 앤티에일리어싱) 제거** — `BuildStandalone.ConfigureAntiAliasing()` 신설(`ConfigureRunInBackground()`와 같은 멱등 패턴): 전체 6개 품질 레벨의 `QualitySettings.antiAliasing`을 **4x**로 강제. `ProjectSettings/QualitySettings.asset` 실측으로 6개 레벨 전부 `antiAliasing: 4` 확인. 씬 쪽은 `cam.allowMSAA = true`, **`cam.allowHDR = false`**(HDR 버퍼는 투명 합성에서 알파를 잃을 수 있다). 머티리얼은 점검 결과 그대로 두면 된다 — `Sprites-Default.mat`의 `Blend One OneMinusSrcAlpha`(프리멀티플라이드)는 `dstA = srcA + dstA(1-srcA)`로 알파를 올바르게 출력하므로, MSAA 커버리지 리졸브가 가장자리에 중간 알파를 만들어준다. **MSAA를 켠 뒤 투명이 여전히 정상인지 재검증 완료**(`isTransparent=True` 되읽음 + `kCGWindowLayer=101` 유지).

**(c) 캐릭터 크기 축소** — 사용자: "사이즈도 너무 커", "창 위로 돌아다니고 해야 하는데 너무 크잖아". 카메라 `orthographicSize` **5 → 12**(`OrthographicSize` 상수로 승격). 계산 근거: 캐릭터 전신 높이는 지오메트리 상수에서 유도되어 **2.27 월드유닛**, 실측 창 높이 **846pt** → 화면상 높이 = `2.27 / (2·orthoSize) · 846`. `orthoSize=5`면 **192pt**(너무 큼), `orthoSize=12`면 **80pt**(목표 구간 70~90pt의 한가운데). 선 두께는 `LineWidthScale = 0.7` 일괄 배율 도입(몸통 0.11→0.077, 다리 0.12→0.084, 팔 0.10→0.070, 머리링 0.09→0.063) — 화면상 약 2.5~3.0pt로 리더 지시 "2~3px 유지"에 맞췄다.

> **BUG-SW-M2 함정 재확인(리더 경고 대응, "조용히" 바꾸지 않았다)**: `orthographicSize`는 `ScreenCoordinateConverter`의 OS-px↔월드유닛 변환 비율에 곱연산으로 반영되어 `StickConfig`의 px 필드 유효 월드 크기를 **2.4배** 넓힌다. 과거 5→20(4배) 변경이 접지 터널링을 유발해 되돌린 이력이 있다. 이번이 안전하다고 판단한 근거를 수치로 확인했다: `groundSnapTolerance = 6 OS-px` → 월드 환산 `6·(24/1692) = 0.085유닛`으로 캐릭터 전신(2.27유닛) 대비 **3.7%**(변경 전 0.036유닛 = 1.6%). **허용 오차가 넓어지는 방향**이라 터널링은 오히려 덜 일어난다. 지면 Y(`ComputeGroundTopWorldY`)와 RAGDOLL 바닥(`CreateGroundCollider`)은 둘 다 카메라에서 유도되므로 자동으로 따라온다. **프리팹의 월드 크기/질량/관절은 전혀 건드리지 않았다** — 프리팹 축소 방식 대신 이 방식을 고른 가장 큰 이유이며, "안 넘어지고 걷는다"는 이미 검증된 물리 거동이 그대로 보존된다.

### (10) 남은 한계 (정직한 기록)

- **`isHitTestEnabled` + 얇은 선의 UX 상충**: 클릭관통 ON 상태에서는 커서가 **불투명 픽셀 위**에 있을 때만 클릭이 전달되는데, 선 두께를 화면상 2.5~3pt로 줄였으므로 "캐릭터를 클릭"하려면 그 얇은 획을 정확히 맞춰야 한다. `opacityThreshold=0.1`과 MSAA의 부분 알파가 약간 완화해주지만 근본 해결은 아니다 — 다음 라운드에서 `hitTestType = Raycast`(Collider2D 기반, `CircleCollider2D` 반경 0.4가 이미 있다)로 바꾸면 캐릭터 주변 넉넉한 영역에서 클릭을 받을 수 있다. `ILocalClickCaptureService` 대체 리팩터링과 함께 다루면 좋다.
- **창이 화면 전체를 덮지 않는다**: 실측 `windowSize=(1512, 846)`, `windowPosition=(0, 75)` — 화면 폭은 전부 덮지만 세로는 846pt로 하단/상단 일부가 빠진다. 데스크톱 펫이 화면 어디든 갈 수 있으려면 `shouldFitMonitor`/`isFreePositioningEnabled`(메뉴바 위 배치 허용) 검토가 필요하다. 이번 라운드 범위 밖.
- **`StickMate.Runtime.asmdef`의 플랫폼 범위**: `Kirurobo.UniWindowController`의 `includePlatforms`는 Editor/macOS/Windows Standalone 뿐인데 `StickMate.Runtime`은 전 플랫폼이다. macOS 빌드에서는 문제없음을 실측 확인했지만, 향후 iOS/Android 빌드 시 이 참조가 문제를 일으키는지 그 시점에 확인해야 한다.
- **실제 다른 창 위 정밀 착지**는 이번에도 검증하지 못했다(`desktopDpiScale=0.5`가 이제 실제로 적용되므로 조건은 갖춰졌지만, 실행 환경에 발밑에 밟을 다른 창이 없어 안전망 발판만 사용됨).

### 같은 라운드 후속 피드백 2건 (2026-08-28, Coder)

**(a) "목이 얼굴을 뚫고 올라와있는거 같고"** — 직전까지 몸통(목) 선의 위쪽 끝을 `torsoTopY + HeadVisualRadius*0.5`로 **머리 원 안쪽 깊숙이** 파고들게 배치했었다. 그때는 얼굴이 흰색으로 꽉 차 있어서(sortingOrder 3) 파고든 부분이 가려졌는데, 이번 라운드에 얼굴을 투명하게 비우면서 그 선이 머리 안에서 그대로 드러났다.

수정: `torsoTopOverlapped = torsoTopY + (HeadOutlineWidth - LineWidth) * 0.5f`. 유도 근거 — 머리 링은 반지름 `R` 원 경로를 두께 `W`로 그리므로 링이 차지하는 반경 구간이 `[R-W/2, R+W/2]`이고, 링의 **안쪽 가장자리**는 `torsoTopY + W/2`다(`torsoTopY = headY - R`). 몸통 선은 둥근 캡 때문에 끝점보다 `LineWidth/2` 더 위로 뻗으므로 `끝점 + LineWidth/2 = torsoTopY + W/2` → `끝점 = torsoTopY + (W - LineWidth)/2`. 이러면 (i) 링 안쪽 빈 공간을 1px도 침범하지 않고 (ii) 몸통 획이 링 두께 구간을 완전히 가로질러 겹치므로 목-머리 사이에 틈도 생기지 않는다.

**(b) "캐릭터 주변이 반짝거림 검은색 선이라 그런가?" — 원인 규명 및 해결**

리더가 MSAA를 끄고 확인해보라고 했지만, **끄지 않고도 원인을 특정해 근본 해결했다**(계단 현상과 반짝임을 동시에 제거 — 리더가 제시한 "둘 다 잡기"의 상위 결과).

**진짜 원인**: 씬에 저장된 카메라 배경이 `(0.94, 0.94, 0.94, alpha 0)` = "밝은 회색 + 알파 0"이었다. 알파 0이라 투명이 성공하면 이 RGB는 보이지 않는다 — **MSAA를 켜기 전까지는.** MSAA는 한 픽셀의 여러 서브샘플을 평균하므로, 캐릭터 윤곽선 픽셀(예: 50% 덮임)은
```
rgb = (검정 0.0 x 0.5) + (배경 0.94 x 0.5) = 0.47
alpha = (1 x 0.5) + (0 x 0.5) = 0.5
```
가 된다. 즉 **알파 0인 배경의 밝은 RGB가 가장자리 픽셀로 새어 들어온다.** 검은 캐릭터 둘레에 밝은 회색 프린지가 생기고, 캐릭터가 서브픽셀로 움직일 때마다 그 밝기가 프레임마다 변해 "반짝거리는" 것처럼 보인다.

**결정적 방증**: `UniWindowController` 자신이 `autoSwitchCameraBackground`가 켜져 있으면 투명화 시점에 배경을 `Color.clear`(= 0,0,0,0)로 바꾼다(`SetCameraBackground()`). 패키지 샘플 씬(`Samples~/01_SimpleSample`)도 에디터에서는 임의의 RGB에 알파 0을 저장해두지만 **런타임에는 라이브러리가 `Color.clear`로 덮어쓴다**. 우리가 그 자동 전환을 끄고(방어책 유지 목적) 밝은 회색을 유지한 것이 바로 이 아티팩트의 원인이었다.

**해결**: `MacOverlayStateEnforcer.ApplyTransparentSafeCameraBackground()` 신설 — 창 부착이 확인되고 `isTransparent`가 true로 **되읽힌 경우에만** 카메라 배경 RGB를 검정으로 낮춘다(알파는 계속 0). 같은 픽셀이 `rgb=0, alpha=0.5`가 되어 프린지 없이 정확히 "50% 농도의 검은 선"으로 합성된다.

**방어책은 그대로 유지된다** — 투명화가 실패한 경우에는 이 교정이 실행되지 않아 배경이 밝은 회색으로 남고, 예전처럼 "밝은 회색 창 안의 검정 캐릭터"(최소한 보이는 상태)가 된다. 즉 리더가 확립한 "검정-on-검정 금지" 원칙을 깨지 않으면서 아티팩트만 제거했다.

실측(Player.log): `투명 확인됨 — 카메라 배경 RGB를 검정으로 교정했습니다 ... (0.94,0.94,0.94,0.00) -> (0.00,0.00,0.00,0.00)`, 이후 재적용 로그의 `cameraBg=rgba=(0.00,0.00,0.00,0.00)` 확인. `kCGWindowLayer=101` 유지, 예외 0건.

**만약 그래도 반짝임이 남는다면**(다음 라운드 참고): 그때는 `BuildStandalone.ConfigureAntiAliasing()`의 `TargetAntiAliasing`을 `0`으로 바꿔 MSAA를 끄면 확실히 사라진다(대신 계단 현상이 돌아온다 — 리더의 우선순위 "반짝임 제거 > 계단현상 제거"에 따른 폴백).

### 후속 과제 (이번 라운드에서 하지 않음 — 리더 지시로 기록만)

- **캐릭터에 얇은 흰색 외곽선(또는 그림자) 추가**: 사용자가 "검은색 선이라 그런가?"라고 물었는데, 실제로 **어두운 배경화면에서 검은 캐릭터는 잘 보이지 않는다**. 데스크톱 펫은 밝은/어두운 배경 어디서든 보여야 하므로, 각 `LineRenderer` 뒤에 조금 더 두꺼운 흰색 선을 한 겹 깔거나(sortingOrder를 한 단계 아래로) 드롭섀도를 두는 방법을 검토할 가치가 있다.
- **`hitTestType`을 `Opacity` → `Raycast`로 전환 검토**: 위 (10) "남은 한계" 참고 — 얇은 선 위를 정확히 클릭해야 하는 UX 문제를 `CircleCollider2D`(이미 존재, 반경 0.4) 기반 판정으로 해결할 수 있다. `ILocalClickCaptureService` 대체 리팩터링과 함께.

### 검증 강도 조정 (리더 지시, 2026-08-28)

**순수 시각 수정(색/크기/좌표)에는 90초 실측과 전체 테스트 스위트를 매번 돌리지 않는다.** 컴파일 확인 + 짧은 실행(20~30초)으로 크래시/예외만 확인하고 빠르게 빌드해 넘긴다. **물리/상태머신 로직을 건드릴 때만** 기존의 엄격한 검증(EditMode 13/13 + PlayMode 3/3 + 90초+ 실측)을 적용한다.

---

## 드래그&던지기 / 로데오 커서 "실제 입력 배선" 라운드 (2026-08-28, Coder)

사용자 지적 **"마우스로 캐릭터를 들고 여러가지 제어가 가능해야하는데 지금은 단순히 돌아만다님"** 대응. Phase 3에 이미 구현돼 있던 로직을 실제 마우스 입력에 연결했다.

### (0) 근본 원인 — 컨트롤러가 씬/프리팹 어디에도 배선되어 있지 않았다

`Stickman.prefab`을 실측 조회한 결과, 붙어 있던 MonoBehaviour는 **`StickmanAgent` / `StickmanClickHitbox` / `RagdollLimbImpactRelay` 3종뿐**이었다. `Interaction/DragThrowController.cs`와 `Interaction/RodeoCursorWatcher.cs`는 코드로만 존재하고 **한 번도 씬에 배치된 적이 없어 단 한 줄도 실행된 적이 없었다.** `StickmanClickHitbox`가 `MouseDown` 이벤트를 쏴도 구독자가 0명이었던 것이다. 그래서 이번 라운드의 핵심 수정은 새 로직이 아니라 **배선**이다(`SceneBootstrapper.BuildStickmanPrefab`에서 코드로 부착 + `SerializedObject`로 `_player`/`_hitbox`/`_hitboxCollider`/`_config` 주입, `--force` 재현 가능).

### (1) 히트테스트 Opacity -> Raycast 전환

직전 라운드 한계("2.5~3pt 획을 정확히 클릭해야 반응") 해소. `SceneBootstrapper.ConfigureUniWindowController()`에서 `hitTestType = HitTestType.Raycast`. 라이브러리 소스(`UniWindowController.HitTestByRaycast()`)를 읽고 이 모드가 요구하는 전제 3가지를 전부 맞췄다:

1. **EventSystem 필수** — `HitTestByRaycast()` 첫 줄이 `EventSystem.current.RaycastAll(...)`인데 **null 체크가 없다.** 씬에 EventSystem이 없으면 NRE로 `HitTestCoroutine`이 통째로 죽고 클릭관통 상태가 마지막 값에 영구히 얼어붙는다("조용한 오동작"). `EnsureEventSystem()` 신설(입력 모듈은 붙이지 않음 — 이 프로젝트엔 Canvas가 없어 필요 없다).
2. **레이어** — 라이브러리 마스크는 `~LayerMask.GetMask("Ignore Raycast")`, Physics2D 쪽은 `DefaultRaycastLayers`. 그래서 **`PhysicsGround`를 레이어 0 -> 2(Ignore Raycast)로 이동**했다. 안 그러면 화면 하단 20% 전체 띠(보이지도 않는 물리 안전망 바닥)에서 클릭이 앱에 잡혀 비침해 원칙 2가 정면으로 깨진다. 레이어 2는 레이캐스트에서만 제외될 뿐 충돌 매트릭스에는 영향이 없어 물리 거동은 무변경.
3. **카메라** — 기존대로 `currentCamera`를 Main Camera로 명시 지정.

**클릭 영역**: 물리용 루트 캡슐(폭 0.4유닛 = 화면상 약 14pt)은 그대로 두고, **`isTrigger=true`인 별도 `CapsuleCollider2D` "GrabArea"(0.8 x 전신+0.3 = 화면상 약 28pt x 90pt)** 를 루트에 추가했다. 트리거는 물리 충돌을 일으키지 않으므로 검증된 바닥/랙돌 거동이 전혀 바뀌지 않으면서, `m_QueriesHitTriggers=1`이라 Unity `OnMouseDown`과 `Physics2D.GetRayIntersection` 히트테스트에는 둘 다 잡힌다. 얇은 획(2.5~3pt) 대비 **약 10배 넓은 표적**. 팔다리 `BoxCollider2D`(Kinematic)도 레이캐스트에 정상적으로 잡히는 것을 실측 확인했다.

### (2) 이중 입력 경로 — `OnMouseDown` + 전역 버튼 폴링

`StickmanClickHitbox`가 이제 두 경로에서 같은 `MouseDown`/`MouseUp`을 낸다(`_pressed` 엣지 플래그로 중복 방지).
- (a) Unity 표준 `OnMouseDown`/`OnMouseUp`.
- (b) **신규** `Platform/IGlobalPointerButtonService`(macOS: `CGEventSourceButtonState`, 조회 전용·권한 불필요) + 기존 커서 폴링 조합. 창 포커스와 무관하다.

(b)가 필요한 이유: 우리 창은 항상위 투명 오버레이 + 평소 클릭관통 + 대개 **비활성 앱**이다. macOS에서 비활성 앱 창의 첫 클릭은 앱 활성화에만 소비되고 콘텐츠 뷰로 안 내려올 수 있다(`acceptsFirstMouse` 기본 NO) — 그러면 "눌렀는데 아무 일도 안 일어남"이 된다. **비침해 원칙은 (b)에서도 유지**: 버튼이 눌렸다는 사실만으로는 아무 일도 안 하고, 그 순간 커서가 캐릭터 `Collider2D` 안에 있을 때만 발동한다(판정 영역이 (a)와 동일).

### (3) 좌표계 버그 2건을 실측으로 발견해 수정 (이번 라운드에서 가장 중요)

리더 지시("드래그 추종이 커서와 정확히 일치하는지 실측")를 따르다 **기존 좌표 변환이 2중으로 틀려 있었음**을 발견했다. 둘 다 고쳤고, 실측으로 오차 0.1px까지 검증했다.

**(a) `desktopDpiScale`이 정확히 2배 틀렸다.** 직전 라운드는 디스플레이 `backingScaleFactor`(=2)의 역수 0.5를 썼다. 그러나 `ProjectSettings`의 **`macRetinaSupport: 0`** 이라 Unity는 백킹 픽셀이 아니라 **포인트**로 렌더/보고한다 — 실측 `Screen=(1512x846)` == 우리 창 크기 `(1512x846 pt)` → 올바른 값은 **1.0**. 수정: `DetectDesktopDpiScale()`이 디스플레이 배율 대신 **자기 창 폭(kCGWindowBounds, OS 포인트) / `Screen.width`(Unity 픽셀)** 를 직접 측정한다. 창 열거는 UniWindowController 부착과 무관하게 `Start()` 시점부터 성공하므로(실측 확인) 직전 라운드가 겪은 "부착 전 clientSize=(0,0)" 함정에도 걸리지 않는다.

**(b) 창이 화면 좌상단에서 시작한다는 가정이 틀렸다.** 실측: 창 원점 Quartz **(0, 61)**, 크기 (1512x846) — 메뉴바/Dock을 뺀 가운데 구간에만 존재한다. `ScreenCoordinateConverter`는 원점 (0,0)을 가정했으므로 커서(전역 좌표)↔월드 변환이 61pt(월드 약 1.7유닛) 통째로 어긋나 있었다. 수정: `ScreenCoordinateConverter.OverlayOriginOsScreen`(static, 기본 (0,0)이라 다른 플랫폼/테스트는 무변경) 신설. 갱신은 `MacWindowService.EnumerateFootholds()`가 **이미 돌고 있는 창 열거 루프** 안에서 자기 창을 만나면 그 `kCGWindowBounds`를 대입한다(추가 시스템 호출 0건, 커서와 **완전히 같은 Quartz 좌표계**라 좌표계 혼용 위험 없음). 라이브러리의 `windowPosition`(=(0,75))은 다른 좌표 규약(AppKit y-up)이라 쓰지 않았다 — 실제로 `982 - 846 - 61 = 75`로 두 값이 서로 뒤집힌 관계임을 확인했다.

> **연쇄 수정**: `FallbackPlatformWindowService`의 합성 안전망 발판은 "창이 (0,0)에서 시작" 전제로 만들어져 있었다. (b) 적용 직후 실측에서 캐릭터가 **`Fall` 상태에 고착**(발 높이와 발판 상단 Y가 61pt 어긋남)되는 것을 확인해, 그 사각형도 오버레이 원점만큼 평행이동하도록 함께 고쳤다. 수정 후 정상적으로 `Idle`로 정착하는 것을 재실측했다.

**실측 검증(결정적)**: 로데오로 캐릭터가 커서에 올라탄 순간, Player.log의 캐릭터 화면 좌표에서 역산한 OS 좌표가 **(690, 825)**, 같은 시각 외부 프로세스가 `CGEventGetLocation`으로 읽은 실제 커서가 **(690.1, 825.1)** — 오차 0.1px. 월드↔OS 왕복이 정확해졌음이 증명됐다.

### (4) `DragThrowState` — 잡은 지점 오프셋 유지

`FollowCursor()`가 몸통 원점을 커서에 그대로 맞추면 (i) 이 프로젝트의 루트 원점은 **발끝**이라 캐릭터가 커서 위쪽에 통째로 매달리고, (ii) 누르는 순간 순간이동하듯 튄다(12절이 명시적으로 배제한 연출). 그래서 `Enter()`에서 "커서->몸통" 오프셋을 기록해 유지한다(전신 대각선 길이로 clamp). 머리를 잡으면 머리가, 다리를 잡으면 다리가 커서에 붙는다. 부수 효과로 좌표 변환에 상수 오차가 남더라도 드래그 추종에서는 상쇄된다. **속도 계산/상한/RAGDOLL 전이 로직은 무수정**.

### (5) 안전장치 재확인 (Escape / 5초 지연)

`hitTestType`을 바꿔도 안전장치 메커니즘은 그대로다 — `SetClickThrough(false)`가 `isHitTestEnabled=false`를 함께 걸어 **라이브러리의 매 프레임 자동 제어 자체를 정지**시키기 때문이며, 이는 히트테스트 "방식"(Opacity/Raycast)과 무관하다. 실측(`MacOverlayStateEnforcer` 신규 감시 로그, 1초 간격): 시작 후 5초 구간 내내 `isHitTestEnabled=False, isClickThrough=False`가 **한 프레임도 뒤집히지 않고** 유지되다가 5초 뒤 둘 다 `True`로 전환. Escape 경로는 이 5초 구간과 **완전히 동일한 코드 경로**(`ApplyClickThrough(false)`)다. 5초 지연도 그대로 유지.

### (6) 신규 실측 감시 로그 (`MacOverlayStateEnforcer.TickHitTestProbe`)

부착 후 25초 동안 1초 간격으로, 라이브러리와 **같은 질의**(`cam.ScreenPointToRay` + `Physics2D.GetRayIntersection`)를 두 지점에 직접 쏴 남긴다. 실측 결과 전 구간에서:
- 캐릭터 지점 -> `Stickman/CapsuleCollider2D` **검출**(클릭 가능)
- 캐릭터에서 화면 가로로 605px 떨어진 빈 지점 -> **미검출**(정상 관통 = 비침해 유지)

### (7) PlayMode 테스트 3/3 실패 -> 원인 규명 후 복구

로데오 감시자를 프리팹에 배선하자마자 PlayMode 3종이 전부 깨졌다. 원인: `-batchmode -nographics`에는 **마우스 커서가 애초에 존재하지 않는데** `NullPlatformWindowService.TryGetGlobalCursorPosition()`이 `Input.mousePosition`의 고정값 (0,0)을 "유효한 커서"라고 `true`로 보고 → 5초 정지 판정 → 로데오 자동 발동 → 캐릭터가 화면 좌상단으로 끌려감. 수정: **배치 모드에서는 `false`(커서 없음)를 정직하게 반환**한다. 소비자는 전부 이 bool을 확인하므로 안전하며, 사람이 조작하는 에디터 Play 모드는 예전과 동일하다. 기능을 끈 것이 아니라 **가짜 입력이 게임플레이를 움직이던 것**을 막았다.

### (8) 로데오 커서 — 동작 확인됨 + 재트리거 버그 1건 수정

실측 로그에서 상태가 `Idle -> RodeoCursor`로 실제 전이했고, 그 시점 캐릭터 위치가 실제 커서와 0.1px 이내로 일치했다(위 (3) 검증에 쓴 바로 그 데이터). **정상 동작한다.**

다만 배선 직후 실측에서 캐릭터가 커서에서 **영원히 내려오지 않는** 현상을 발견했다(상태 시퀀스가 17초 연속 `RodeoCursor`). 타임아웃(`rodeoMaxDurationSeconds=10`)은 정상 동작하고 있었고, 진짜 원인은 **재트리거 조건**이었다: `RodeoCursorWatcher.Update()`의 `_stillTimer`가 상태와 무관하게 매 프레임 누적되므로, 10초 라이딩이 끝나 Idle로 돌아온 바로 그 프레임에 이미 10초가 쌓여 있어 다음 프레임에 즉시 재발동한다. `TryTrigger()`가 발동 직전 `_stillTimer = 0f`로 리셋하며 남긴 주석("즉시 재트리거 방지 — 다음 5초를 다시 채워야 함")이 이미 의도를 명시하고 있는데 라이딩 중 누적이 그 의도를 무력화한 것이다.

**수정**: `OnStateTransitioned`(RodeoCursor 이탈)에서 `_stillTimer`를 한 번 더 리셋 — 새 규칙 추가가 아니라 **이미 문서화된 규칙이 실제로 지켜지게** 만드는 2줄 수정이다. 이 수정 없이는 사용자가 마우스를 가만히 두는 순간 캐릭터가 커서에 붙어버려(그 상태에서는 `Dragged` 진입 조건인 Idle/Walk가 아니다) **드래그&던지기를 시험조차 할 수 없어**, 이번 라운드의 목표 자체가 검증 불가능해진다.

> **다음 라운드 검토 과제(수정하지 않음)**: 로데오 발동 중에는 캐릭터가 커서 위에 있으므로 히트테스트가 `isClickThrough=False`를 유지한다 — 즉 그동안 사용자의 클릭이 앱에 잡힌다. 사용자가 마우스를 한 번도 건드리지 않은 채 앱을 켜두면 5초 뒤 자동 발동하므로, "유저가 캐릭터를 발견하기도 전에 첫 클릭을 삼키는" 시나리오가 가능하다. 완화안 후보: (a) 앱 시작 후 커서가 한 번이라도 움직인 뒤에만 로데오를 arming, (b) 로데오 중 클릭이 감지되면 즉시 내려오기.

### (9) 실측으로 발견한 "지면 밑 영구 고착" 1건 수정

로데오가 정상 발동하기 시작하자마자 새 현상이 드러났다: 라이딩이 끝나 캐릭터가 커서에서 내려온 뒤 **`Fall` 상태에 영구 고착**된다(실측 상태 시퀀스 `RodeoCursor` x10 -> `Fall` x7, 위치 고정).

**원인**: 씬의 논리적 지면(발판 상단)은 창 하단에서 위로 20% 지점(월드 y=-7.2)인데, 커서는 그보다 **아래**(macOS Dock 영역)에 있을 수 있다. Kinematic `MovePosition`은 정적 바닥 콜라이더를 그대로 통과하므로 캐릭터가 바닥 **밑**에 놓이고, 거기서 Dynamic으로 돌아가면 `GroundSensor`의 접지 판정(`|footOs.y - 발판상단| <= groundSnapTolerance`)이 영원히 false이며 물리 바닥이 위로 올려주지도 못한다. **드래그&던지기도 똑같이 노출된다** — 사용자가 캐릭터를 화면 하단 20% 안으로 끌고 내려가 놓으면 같은 고착에 빠진다.

**수정 3곳**:
- `GroundSensor.TryGetSurfaceWorldY()` 신설 — "이 x에서 딛을 수 있는 가장 높은 발판 상단은 어디인가"를 **접지 허용 오차와 무관하게** 답한다(`Sense()`의 Grounded로는 답할 수 없는 질문). `StickmanBlackboard.TryGetGroundSurfaceWorldY()`로 노출.
- `DragThrowState.FollowCursor()` — 추종 목표 Y를 지면 아래로 내려가지 않게 **소프트 클램프**. UX_FLOW.md 12절이 이미 명시한 "안쪽으로 소프트 클램프" 처리다.
- `RodeoCursorWatcher.TryTrigger()` — 커서가 지면선보다 아래면 **발동 자체를 억제**. UX_FLOW.md 13절 예외("캐릭터가 물리적으로 도달 불가능한 위치에 정지해 있으면 트리거를 억제")가 이미 규정한 정답이며, 클램프해서 어정쩡하게 태우는 것보다 스펙에 맞다.

### (10) 범위 밖으로 남긴 것 (리더 지시 준수)

`isHitTestEnabled`로 `ILocalClickCaptureService`(15절 부분적 클릭관통 해제)를 **대체하는 리팩터링은 하지 않았다.** 기존 소유권 부기 구조를 그대로 두고 충돌만 없게 했다 — macOS의 `MacWindowService`는 그 인터페이스를 구현하지 않으므로 `DragThrowController`에서 `as` 캐스팅이 null이 되고, 실제 OS 히트테스트는 UniWindowController가 별도로 담당한다(두 메커니즘이 서로 간섭하지 않음). 실측 로그에서도 `부분클릭관통해제 서비스=지원`(FallbackPlatformWindowService 경유)으로 잡히지만 내부 서비스가 미구현이라 요청은 실패 처리되고, `DragThrowController`가 그 경우를 이미 방어하고 있어 드래그 진입에는 영향이 없다.

### 검증 결과

컴파일 **에러 0 / 경고 0**, **EditMode 13/13**, **PlayMode 3/3**, 빌드 `Succeeded`(에러 0, 경고 0). 실제 `.app` 실행 60초+ — 예외/크래시 0건.

**실측 로그 요약**
```
DetectDesktopDpiScale(): 자기 창 실측 — 창=(0,33,1512x874), Screen=(1512x846) -> desktopDpiScale=1.000
오버레이 창 원점(Quartz) — origin=(0, 61), size=(1512x846)
[StickmanClickHitbox]   준비 완료 — 콜라이더 11/11개 활성, MouseDown 구독자=1명, 레이어=StickmanLimb(8)
[DragThrowController]   준비 완료 — player=True, hitbox=True, hitboxCollider=CapsuleCollider2D
히트테스트 감시 — 모드=Raycast / 캐릭터지점=Stickman/CapsuleCollider2D 검출 / 빈지점=없음(정상 관통)
0~5초 구간: isHitTestEnabled=False, isClickThrough=False (안전장치 유지) -> 5초 후 둘 다 True
좌표 실측: 캐릭터 역산 OS (690, 825) vs 실제 커서 CGEventGetLocation (690.1, 825.1) — 오차 0.1px
```

---

## 후속 핫픽스 — "마우스로 안 잡힘" 원인 규명 + 캐릭터 색상 선택 (2026-08-28, Coder)

사용자 실측 신고 **"마우스로 안 잡힘"**. 리더가 Player.log에서 `[StickmanClickHitbox] 마우스다운 감지`는 찍히는데 `Dragged` 전이 로그가 전혀 없다는 결정적 증거를 특정해줬다. 즉 **히트테스트/클릭 감지는 정상이고, 컨트롤러가 이벤트를 받고도 조용히 되돌아가고 있었다.**

### (1) 진짜 원인 — 데코레이터 계약 구멍 2개 (둘 다 "조용한 실패")

**(a) 드래그가 중단된 지점 — `ILocalClickCaptureService`**

`MacWindowService`는 "실제 OS 히트테스트는 UniWindowController가 하니 부기는 불필요"라는 이유로 `ILocalClickCaptureService`를 **의도적으로 구현하지 않았다.** 그런데 이 서비스는 항상 `FallbackPlatformWindowService` 데코레이터로 감싸여 소비되고, 그 데코레이터는 인터페이스를 **자기가 구현**하면서 내부 서비스에 위임한다:

```
_innerClickCapture = (MacWindowService as ILocalClickCaptureService) == null
  -> RequestLocalClickCapture(...) 가 항상 false
```

그 결과 `DragThrowController.OnMouseDown()`의

```csharp
if (_clickCapture != null && !_clickCapture.RequestLocalClickCapture(hitboxOs, this))
{ SpectacleEventLock.Release(this); return; }   // <-- 매번 여기서 되돌아감
```

가 **매 클릭마다 성립**했다. `_clickCapture`는 데코레이터라 non-null인데 요청은 false이므로, 클릭은 감지되는데 `ChangeState(Dragged)`에 절대 도달하지 못했다. 직전 라운드 보고서에 "미구현이라 요청은 실패 처리되고 컨트롤러가 방어하고 있어 드래그 진입에는 영향이 없다"고 적었던 것은 **명백한 오독**이다(그 방어 분기가 바로 중단 지점이었다).

**수정**: `MacWindowService`가 `Win32WindowService`/`NullPlatformWindowService`와 **완전히 동일한 방식**으로 공용 `LocalClickCaptureGate`에 위임해 부기를 구현한다. 리더가 범위 밖으로 지정한 "`isHitTestEnabled`로 15절을 대체하는 리팩터링"이 아니라, 다른 플랫폼이 이미 갖고 있던 부기를 macOS에도 채워 데코레이터 계약을 만족시키는 것이다.

**(b) 전역 버튼 폴링 경로가 한 번도 켜진 적이 없었다 — `IGlobalPointerButtonService`**

같은 데코레이터가 이 신규 인터페이스를 통과시키지 않아 `PlatformService as IGlobalPointerButtonService`가 **항상 null**이었다(실측 로그: `전역버튼경로=미지원`). 즉 직전 라운드에 "창 포커스와 무관한 보조 경로"라고 만들어둔 것이 실제로는 죽어 있었다. `ICursorPositionService`와 동일한 위임 패턴으로 통과시켰다. 수정 후 실측: **`전역버튼경로=사용 가능`**.

### (2) 리더 가설 (b) 대응 — 놓기 판정을 전역 폴링으로 승격

리더가 "`OnMouseDown`/`OnMouseUp`이 항상 즉시 연달아 찍힌다 → 창 마우스 캡처 유실 의심"을 제기했다. Player.log에는 타임스탬프가 없어 **인접한 두 줄만으로는 시간 간격을 알 수 없으므로** 판별 자체가 불가능했다. 두 가지를 함께 처리했다:

- **측정**: `BeginPress` 시각을 기록해 `EndPress`에서 **홀드 시간(초)** 을 직접 로그에 찍는다. 이제 (a)"짧은 클릭"과 (b)"캡처 유실"을 로그만으로 구분할 수 있다.
- **방어**: 전역 폴링 경로가 살아 있으면 **Unity의 `OnMouseUp`으로는 드래그를 끝내지 않는다.** `OnMouseUp`이 와도 `CGEventSourceButtonState`가 "아직 눌려 있다"고 답하면 그 사실을 로그로 남기고 **무시하고 드래그를 계속**한다. 놓기 판정은 전역 폴링이 전담한다(창 포커스/캡처와 무관). 전역 경로가 없는 플랫폼에서는 예전처럼 `OnMouseUp`이 놓기를 담당한다.

즉 (b)가 사실이든 아니든 드래그는 성립하고, 사실 여부는 로그로 확정된다.

### (3) 전 구간 단계 로그 `[n/6]` (리더 지시)

사용자가 시도하면 리더가 로그만 보고 어디서 끊겼는지 즉시 판별할 수 있도록 전 경로에 번호를 매겼다.

| 단계 | 로그 |
|---|---|
| `[0/6]` | `[StickmanClickHitbox] 준비 완료` / `[DragThrowController] 준비 완료` (시작 시 1회) |
| `[1/6]` | `캐릭터 위 마우스다운 감지(소스)` + 커서 월드 좌표 + 전역버튼경로 상태 |
| `[2/6]` | `가드 통과 — Idle -> Dragged 전이 요청` **또는 실패 사유**(상태 불일치 / SpectacleEventLock 점유자 / 클릭캡처 거부 / player null) |
| `[3/6]` | `[DragThrowState] 드래그 시작(Dragged 진입)` + 잡은 오프셋 + 물리모드 |
| `[4/6]` | `드래그 추종 중` — 1초 간격, 커서/몸통/목표 좌표 |
| `[5/6]` | `마우스업 감지(소스) — 홀드 시간 N초` + `놓기 신호 전달` |
| `[6/6]` | `놓음 — 던진 속도/충격량 -> RAGDOLL 또는 Fall` |

**모든 조기 반환에 사유 로그를 붙였다** — 조용한 no-op이 이번 사고의 진단을 지연시킨 직접 원인이었다.

### (4) 캐릭터 색상 선택 (흰색/검은색) — 사용자 요청

- `StickConfig`에 `enum StickmanInkColor { Black = 0, White = 1 }` + `inkColor` 필드 + `whiteInkColor`(기본 흰색) 추가. `primaryOutlineColor`는 Black 프리셋의 실제 색으로 **그대로 재사용**(기존 배선/문서 무효화 없음). 읽기는 반드시 `ResolveInkColor()`를 거친다.
- `SceneBootstrapper`가 프리팹 생성 시 `ResolveInkColor()`를 쓴다.
- **런타임 일괄 갱신**: `StickmanAgent.ApplyInkColor(Color)` / `ApplyInkColorFromConfig()` 신설 — 캐릭터의 모든 `LineRenderer`(몸통/머리링/눈 2개/팔다리 8개 = 12개) 색을 한 번에 바꾼다. `Start()`에서 항상 호출하므로 **프리팹에 저장된 색과 무관하게 런타임에는 항상 `StickConfig.inkColor`가 이긴다** — 즉 에셋 값만 바꾸면 프리팹/씬 재생성 없이 색이 바뀐다. 다음 라운드의 설정 UI/토글 단축키는 이 메서드 하나만 호출하면 된다.
- **설정 UI는 만들지 않았다**(리더 지시, 범위 확장 금지).

**눈 색 결정 — 선과 "같은 색"이 정답이다(반대색 아님).** 이 캐릭터의 머리는 *링(테두리)만 있고 안쪽은 완전히 비어 바탕화면이 그대로 비치는* 구조라, 눈동자는 '얼굴 위의 무늬'가 아니라 **배경 위에 직접 찍힌 잉크 점**이다. 따라서 잉크와 같은 색일 때만 링과 함께 보인다(검정 잉크+밝은 배경 → 검은 링 안 검은 점 / 흰 잉크+어두운 배경 → 흰 링 안 흰 점). 반대색으로 하면 정확히 망가진다 — 흰 캐릭터인데 눈만 검정이면, **흰색이 필요했던 바로 그 어두운 배경 위에 검은 점**을 찍는 셈이라 눈이 사라진다. 기존 코드가 이미 눈에 `outline` 색을 넘기고 있었으므로 추가 분기 없이 그대로 성립한다.

**흰색으로 바꾸는 법**: `Assets/_Project/Data/DefaultStickConfig.asset`에서 `inkColor: 0` -> `inkColor: 1`. 빌드만 다시 하면 되고 프리팹/씬 재생성은 필요 없다(에디터에서는 인스펙터의 "색상" 섹션에서 Ink Color를 White로).

### 검증

컴파일 에러 0 / 경고 0, EditMode 13/13, PlayMode 3/3, 빌드 Succeeded(에러 0/경고 0). 실행 실측:
```
[StickmanAgent] 캐릭터 선 색 적용 — 프리셋=Black, 색=(0.00,0.00,0.00), LineRenderer 12개 갱신.
[StickmanClickHitbox] [0/6] 준비 완료 — 콜라이더 11/11개 활성, 전역버튼경로=사용 가능, MouseDown 구독자=1명
[DragThrowController] [0/6] 준비 완료 — player=True, hitbox=True, hitboxCollider=CapsuleCollider2D
```
`전역버튼경로=미지원 -> 사용 가능` 전환이 (1)(b) 수정의 직접 증거다. **실제 드래그 성립 여부는 사용자 테스트로만 확정 가능하다**(에이전트가 마우스를 조작할 수 없음).

---

## 사용자 피드백 4건 대응 — 드래그 밀착 / 로데오·점프 기본 OFF / 랙돌 자세 (2026-08-28, Coder)

직전 커밋 `4204b25`로 드래그가 실제로 동작하기 시작한 뒤 사용자가 실기기 테스트에서 보고한 4건.
공통 성격: **"동작은 하는데 느낌이 나쁘다" + "자동 발동 행동이 테스트를 방해한다"**.

### (1) "마우스에 딱 붙어서 끌려가야 하는데 이상하게 끌려감" — 즉시 밀착 추종 (최우선)

지연이 **두 겹**으로 쌓여 있었다. 둘 다 제거했다.

| 지연원 | 정체 | 조치 |
|---|---|---|
| `Vector2.SmoothDamp(0.08초)` | 지수 스프링이라 **원리상 목표에 도달하지 않는다.** 커서를 `v` 속도로 끌면 항상 `v × 0.08`만큼 뒤에 끌려간다(5유닛/초면 0.4유닛 ≒ 몸통 높이의 1/5). | `StickConfig.dragFollowSmoothTime` 기본값 `0.08 -> 0`. 0 이하면 `FollowCursor()`가 스프링을 **건너뛰고** 목표를 즉시 대입한다. |
| `Rigidbody2D.MovePosition()` | Kinematic 바디에서는 "다음 물리 스텝까지 이동" 예약이다. `Tick()`은 `Update()`(프레임)에서 도는데 물리는 `FixedUpdate` 주기라 반영이 항상 한 스텝 뒤. | 신규 `SetBodyPositionImmediate()` — `Rigidbody2D.position`(물리)과 `Transform.position`(렌더링)에 **둘 다** 쓴다. 하나만 쓰면 다음 물리 스텝 전까지 화면상 위치가 낡은 값이라 눈에는 여전히 뒤처져 보인다. |

- **잡은 지점(grab offset)은 그대로 유지**한다 — 머리를 잡으면 머리가, 다리를 잡으면 다리가 커서에 붙는다. 누르는 순간의 순간이동도 여전히 없다(오프셋이 그 튐을 흡수한다).
- **던지기 속도 계산은 한 줄도 건드리지 않았다.** `ComputeThrowVelocity()`는 몸통 위치를 **한 번도 읽지 않고** `PushSample()`이 쌓은 **커서 좌표 이력**(`dragThrowVelocitySampleWindowSeconds` = 0.12초 창)만 평균한다. 즉 추종을 아무리 즉각적으로 만들어도 던지는 손맛(12절의 손떨림 방지 스무딩, `dragThrowMaxSpeed` clamp, 충격량 -> RAGDOLL/Fall 분기)은 수치 하나 바뀌지 않는다. **이 둘을 같은 것으로 착각하지 말 것.**
- **UX 12절과의 관계(정직한 기록)**: 12절은 "몸통은 커서를 스프링·댐퍼로 뒤따라오는 관성감(순간 텔레포트처럼 딱 붙지 않음)"을 명시했다. 실제로 만들어 보니 사용자가 이를 **고장으로 인식했다** — 커서로 물건을 끄는 상호작용에서는 잡은 지점이 눈에 띄게 뒤처지는 것 자체가 버그로 읽힌다. 그래서 "잡은 지점은 밀착, 관성감은 팔다리 쪽"으로 해석을 바꿨다. 스프링 경로는 **삭제하지 않았고** 설정값을 0보다 크게 두면 그대로 되살아난다.
- **사용자 검증용 로그**: 에이전트는 마우스를 조작할 수 없어 밀착감을 직접 검증할 수 없다. 대신 기존 `[4/6]` 추종 로그에 **`밀착 오차=N.NNN유닛`**(= 잡은 지점과 커서 사이 거리)을 추가하고 주기를 1초 -> 0.5초로 줄였다. 사용자 테스트 로그에서 이 값이 0.000에 가까우면 밀착 성공, 유의미하게 크면 실패다. (예외: 지면 소프트 클램프가 걸리면 커서가 지면 아래로 내려간 만큼은 **의도적으로** 오차가 남는다 — 바닥 밑으로 끌고 가지 않는다는 기존 규칙.)

### (2) "갑자기 마우스쪽으로 자기혼자 이동" — 로데오 커서 자동 발동 기본 OFF

사용자는 UX 13절의 정식 기능(커서 5초 정지 -> 캐릭터가 다가가 올라탐)을 **버그로 인식했고**, 드래그를 시험할 때마다 끼어들어 테스트 자체를 막았다(로데오 중에는 상태가 Idle/Walk가 아니라 드래그 진입 조건이 성립하지 않는다).

- `StickConfig.rodeoCursorEnabled` **신설, 기본값 `false`**.
- `RodeoCursorWatcher.Update()`가 이 값만 확인하고 조기 반환한다 — **기능/상태(`States/RodeoCursorState.cs`)는 무수정, 그대로 살아 있다.** 조기 반환 시 정지 타이머까지 리셋하므로, 나중에 켜는 순간 "이미 오래 정지해 있었다"는 이유로 즉시 발동하는 부작용도 없다(항상 "지금부터 5초"를 새로 채운다).
- **다시 켜는 법**: `Assets/_Project/Data/DefaultStickConfig.asset`의 `rodeoCursorEnabled: 0` -> `1` (에디터에서는 인스펙터 "로데오 커서" 섹션 체크박스). 빌드만 다시 하면 되고 프리팹/씬 재생성 불필요.
- 검증용 준비 로그 1줄 추가: `[RodeoCursorWatcher] 준비 완료 — 로데오 커서 자동 발동=OFF(기본값).`

### (3) "이상하게 점프도 하고" — 자율 배회의 무작위 점프 기본 OFF

두 확률 모두 이미 `StickConfig` 필드였으므로 **기본값만 0으로** 내렸다(로직 무수정).

| 필드 | 이전 | 이후 | UX 근거 |
|---|---|---|---|
| `wanderPostIdleJumpChance` | 0.05 | **0** | 26-1 Idle 종료 후 제자리 점프 5% |
| `wanderEdgeJumpAttemptChance` | 0.10 | **0** | 26-2 발판 경계에서 10% 점프 시도 |

- `AutoWanderController`의 `Cfg(...)` 폴백 상수도 함께 0으로 맞춰 "설정이 없을 때만 점프가 되살아나는" 불일치를 없앴다.
- **배회 자체는 그대로다** — 꺼진 것은 점프뿐이다. 남은 확률은 각각 "Idle 연장"(Walk 75% / Idle 연장 25%)과 "경계 정지 후 반대 방향 전환"(26-2의 90% 분기)으로 흡수된다.
- **다시 켜는 법**: 같은 에셋에서 `0` -> `0.05` / `0.1`.

### (4) "떨어지면 이상하게 넘어짐" — 랙돌 자세 (이월 과제 해소)

사용자 스크린샷의 문제는 "누워 있는 것"이 아니라 **팔다리가 사람이라면 불가능한 모양으로 쭉 뻗어 있는 것**(수평 일직선, 불가사리)이었다. 원인이 셋이었다.

**(a) 위 마디(대퇴/상완)에는 각도 제한이 아예 없었다** (`useLimits: false`). RAGDOLL에서 팔다리가 고관절·어깨를 축으로 **360도 자유 회전**할 수 있어 "대(大)자"가 물리적으로 허용됐다. -> `HipSwingLimitDegrees = ±65`, `ShoulderSwingLimitDegrees = ±75`를 신설해 두 마디 모두 제한을 건다(90도 미만이라 "몸통에 완전히 수직" 실루엣이 막힌다). 보행 키포즈 최대각(엉덩이 ±25, 어깨 ±18)과 Idle 벌림(다리 12, 팔 40)을 전부 포함하므로 능동 포즈가 제한 밖으로 나가는 일은 없다.

**(b) 아래 마디 제한이 "완전히 편 상태(0도)"를 포함하고 있었다** (팔꿈치 `-5~+100`). 사람의 무릎/팔꿈치는 힘이 빠져도 완전한 일직선이 되지 않는다. -> `MinJointBendDegrees = 3` 신설, 구간에서 0을 **제외**한다: 무릎 `[-100, -3]`, 팔꿈치 `[+3, +100]`. 중립(Idle) 굽힘각(무릎 4 / 팔꿈치 10)보다 작게 잡아 서 있는 자세가 제한 밖이 되지 않게 했다.

**(c) 【이월 과제】 `HingeJoint2D` 제한이 enable 시점 자세 기준으로 재해석되는 문제 — 이번에 해소.** 이 프로젝트는 능동 모드에서 관절을 통째로 disable하고 RAGDOLL에서 다시 enable하므로 **관절이 매 진입마다 새로 만들어지고 그때의 포즈가 `referenceAngle`로 굳는다.** 그래서 프리팹에 적어둔 제한이 진입 포즈만큼 통째로 밀려 해석됐고, 직전 라운드 실측에서 팔꿈치가 **-59도(반대로 꺾임)** 까지 갔다.

- 해법: `RagdollRig`가 (i) **생성 시점에** 프리팹의 해부학 기준 제한을 복사해두고, (ii) RAGDOLL 진입 때 관절을 켠 **직후** Unity가 확정한 `referenceAngle`을 읽어 그 제한을 이번 진입의 jointAngle 좌표계로 **다시 환산해** 넣는다. 진입 포즈가 무엇이든 최종 허용 범위는 항상 같은 해부학적 구간이 된다.
- **부호 규약을 하드코딩하지 않았다.** `jointAngle`이 "자기 바디 - 연결 바디"인지 그 반대인지는 Unity 내부 구현이라 문서만으로 확정할 수 없다. 대신 진입 시점에 이미 알고 있는 값(마디의 `localRotation`)과 Unity가 답한 `referenceAngle`의 **부호를 비교**해 규약을 실측 판정한다(`DetectJointAngleSign`, 진입각 절댓값이 가장 큰 관절을 표본으로 사용). **실측 판정 결과: `-1`(로컬각과 반대)** — 직전 라운드의 -59도 관측과 정확히 일치하는 값이라, 하드코딩했으면 부호를 반대로 넣었을 것이다.
- **damping 상향** (전부 RAGDOLL 구간에서만 유효 — 능동 상태에서 팔다리는 Kinematic이고 루트 회전은 FreezeRotation이라 물리적으로 적용될 대상이 없다. 그래서 걷기/점프/낙하 거동은 무변경이 보장된다):
  - `LimbLinearDamping` 0.6 -> **0.9**, `LimbAngularDamping` 1.5 -> **3.0**
  - 루트 `RootAngularDamping` 0 -> **2.0** 신설(몸통이 팽이처럼 계속 구르지 않고 몇 번 뒤척인 뒤 멈춤). 루트의 **선형** damping은 점프/낙하에 영향이 있으므로 0 그대로 뒀다.
- **실측 검증(PlayMode 랙돌 테스트에 임시 로그를 넣어 0.25초 간격 관측 후 제거)**:

  | | 이전(직전 라운드 실측) | 이번 |
  |---|---|---|
  | 팔꿈치 | **-59도**(반대로 꺾임) | `+1.9 ~ +71.1` — **항상 양수**(정상 방향), 0을 넘어 뒤로 꺾인 순간 0회 |
  | 무릎 | — | `-48.4 ~ -1.0` — **항상 음수**(정상 방향) |

  제한 경계(±3)를 약 2도 넘는 순간은 있다(Box2D 임펄스 기반 제한의 soft overshoot). "완벽한 랙돌 자세"가 아니라 "기괴하게 뻗지 않는 정도"가 목표였으므로 여기서 멈춘다.
- 프리팹은 `SceneBootstrapper.BuildAll --force`로 재생성했다(씬도 함께 재생성 — 둘 다 코드 생성물이라 재현 가능). 재생성 후 프리팹 실측 확인: 다리 위 `[-65,65]` / 팔 위 `[-75,75]` / 무릎 `[-100,-3]` / 팔꿈치 `[3,100]`, 팔다리 damping `0.9/3`, 루트 `0/2`.

### 검증

- Unity 배치모드 컴파일 **에러 0 / 경고 0**
- **EditMode 13/13**, **PlayMode 3/3** (기준선 유지)
- 빌드 **Succeeded, 에러 0 / 경고 0**
- 실행 실측(`.app` 60초+ 연속 실행, `Player.log`): **예외/에러 0건.** 상태 표본 `Idle 15 / Walk 8` — **Jump/Fall/Ragdoll/Getup/RodeoCursor 전이 0회**. 캐릭터 Y좌표가 208로 고정(접지 유지, 낙하 없음)이고 X만 이동해 정상 배회 확인.
  (상태 표본이 23개에서 끊기는 것은 정상이다 — 상태를 찍는 `MacOverlayStateEnforcer.TickHitTestProbe`가 창 부착 후 `ProbeDurationSeconds = 25f` 동안만 로그를 남기도록 **원래 설계된** 것이지 프로세스가 멈춘 것이 아니다. 프로세스는 그 뒤로도 계속 살아 있고 새 예외를 한 건도 남기지 않았다.) 준비 로그 `[RodeoCursorWatcher] 준비 완료 — 로데오 커서 자동 발동=OFF(기본값)` / `[DragThrowController] [0/6] 준비 완료 — ... 부분클릭관통해제 서비스=지원`.
- **드래그 밀착감은 실제 마우스 조작이 필요해 에이전트가 검증할 수 없다.** 사용자 테스트 시 `[4/6]`의 `밀착 오차` 값으로 판별할 것.
- git commit 없음(리더 지시).

---

## 헤드라인 기능 "윈도우 창 = 지형" 실동작 확정 + 사용자 신고 4건 대응 (2026-08-28, Coder)

기획서 1-1절의 핵심 컨셉이 **이번 라운드에 처음으로 실측 검증**됐다. 그 과정에서 사용자가 실사용 중 신고한 4개 증상까지 같은 뿌리에서 함께 고쳤다.

### (A) 오버레이 창을 화면 전체로 확장 — 선행 조건

| | 이전 | 이후 |
|---|---|---|
| Quartz 원점 | (0, 61) | **(0, 0)** |
| 창 크기 / Screen | 1512x846 | **1512x982 / Screen=(1512x982)** |
| 덮지 못하던 영역 | 상단 메뉴바 33pt + 하단 Dock 75pt | 없음(화면 전체) |

- `MacOverlayStateEnforcer.TickFullScreenBounds()` 신설: (a) `Screen.SetResolution` → (b) `isFreePositioningEnabled=true`(macOS의 "visibleFrame 안으로 밀어넣기" 제약 해제) → (c) `windowSize` → `windowPosition` 순서로 대입. 최대 6회 재시도 후 오차 1pt 이내면 중단.
- **좌표 규약을 추측하지 않았다**: `UniWindowController.windowPosition`은 Cocoa(좌하단 원점) 규약이고 `GetMonitorRect()`는 **visibleFrame**(메뉴바/Dock 제외)만 준다는 것을 실측으로 확정했다. 화면 진짜 전체 크기는 `MacWindowService.TryGetMainDisplayBounds()`(CGDisplayBounds, 순수 조회)로 따로 얻는다.
- **좌표 정확도 재검증**: 창 원점이 (0,61)→(0,0)으로 바뀌었지만 `ScreenCoordinateConverter.OverlayOriginOsScreen`이 발판 폴링마다 자동 추종하므로 변환식은 그대로 성립한다. 실측: 캐릭터가 Finder 창 상단(OS y=160.0)에 섰을 때 캐릭터 OS y=**160.0** — 오차 0.0pt.

### (B) ★ 헤드라인 기능이 원리적으로 동작 불가능했던 진짜 원인 — 스윕 착지로 수정

`FallState`는 "발이 창 상단 ±`groundSnapTolerance`(20pt) 안에 **연속 0.1초**(`fallGraceDuration`) 머무를 것"을 착지 조건으로 썼다. 이걸 속도로 풀면:

```
밴드 두께 40pt ÷ (Screen.height/(2·orthographicSize)) ≈ 1.13유닛
착지 가능 최대 낙하속도 ≈ 1.13유닛 / 0.1초 ≈ 11.3유닛/초
gravityScale=3 → 가속도 29.4유닛/s² → 2.2유닛(≈78pt)만 떨어져도 이 상한 초과
```

즉 **78pt 넘게 떨어지면 창 상단을 그냥 통과했다.** 유일하게 착지에 성공하던 곳은 화면 하단 합성 안전망뿐이었는데, 거기에만 `SceneBootstrapper`가 만든 **물리 정적 콜라이더**가 겹쳐 몸이 물리적으로 멈춰야 비로소 0.1초가 채워졌기 때문이다. 실제 타 앱 창에는 그 콜라이더가 없다 → 헤드라인 기능이 한 번도 성립할 수 없었다.

- 수정: `GroundSensor.TryFindLandingCrossing()` 신설 — 낙하를 "점"이 아니라 **"이번 프레임에 발이 지나간 선분"**으로 보고 발판 상단선을 위→아래로 가로질렀는지 검사(연속 충돌 검출). 여러 발판을 한 프레임에 가로지르면 가장 높은(r.y 최소) 것을 채택. 낙하 속도/프레임률과 무관하게 통과가 불가능해진다.
- `FallState`는 1순위로 이 교차 판정을, 2순위로 기존 밴드+유예 판정을 쓴다. 착지 후처리(상단 스냅 + 하강 속도 제거 + 구르기 훅 + Idle/Walk 전이)는 `ConfirmLanding()` 하나로 통합.

### (C) 사용자 신고 4증상 — "발판 선택 로직" 한 덩어리로 재정리

| # | 증상 | 원인 | 수정 |
|---|---|---|---|
| 1 | 가려진 창 위 **허공 부유** | `kCGWindowListOptionOnScreenOnly`는 **완전히 덮여 안 보이는 창도 반환**한다 | 오클루전 컬링(아래) |
| 2 | 창을 최대화해도 **안 떨어짐** | 동일 — 덮인 창이 발판으로 계속 살아 있었음 | 동일 |
| 3 | 새 창 열면 **최상단으로 순간이동** | `Sense()`가 매 프레임 목록 **첫 매치**를 재선택 → 새 창이 z-order 앞에 끼어들면 `SnapToGround()`가 그 높이로 즉시 대입 | 발판 고착(핸들 추적) |
| 4 | **화면 밖으로 사라짐** | 창이 화면 경계를 넘어가면 발판도 화면 밖까지 뻗어, 그 위를 걸어 나가버림 | 발판 화면 클리핑 + 하드 클램프 + 리스폰 |

배제한 가설(코드로 확인): **발판은 이미 "창 상단 테두리"다** — `Sense()`의 세로 판정은 `Mathf.Abs(footOs.y - r.y) <= tolerance`로 `r.y`(상단선)만 본다. 창 내부는 바닥이 아니다. 접지 고착 경로도 없었다(`GroundedTick`이 0.1초 뒤 무조건 Fall).

**최종 사양 6가지 (전부 구현/검증 완료)**
1. **보이는 창만 발판** — `MacWindowService.BuildVisibleTopEdgeFootholds()`: z-order 앞→뒤로 훑으며, 창 i의 상단선 구간에서 "앞선 창 중 그 높이를 세로로 포함하는 것들"의 가로 구간을 뺀다. 남은 조각마다 발판을 하나씩 낸다(핸들은 원본 창 유지). 조각이 없으면 발판 0개 = 낙하. 추가 필터: `kCGWindowAlpha < 0.05` / 60x40pt 미만 / `kCGWindowIsOnscreen=false` / 주 디스플레이 밖 / `kCGWindowLayer != 0` / 자기 자신(PID+이름).
2. **발판 = 창 상단 테두리** (기존 유지, 재확인).
3. **발판 고착** — `StickmanBlackboard.CurrentFootholdHandle`. `GroundSensor.Sense(preferredHandle)`는 0이 아니면 **그 핸들만** 후보로 본다.
4. **사라지거나 X 범위 이탈 시 즉시 Fall** — 고착 핸들이 목록에 없으면 접지 실패 → `GroundedTick`이 0.1초 뒤 Fall(`[발판상실]` 사유 로그).
5. **발판 전환은 낙하→착지로만** — 설정은 `FallState.ConfirmLanding()`, 해제는 `FallState.Enter()`. 유일한 예외는 "공중을 거치지 않은 최초 접지"(앱 시작 직후)의 획득 1회.
6. **화면 밖 소실 방지(최우선)** — 2중 방어: (a) 발판 사각형을 디스플레이 경계로 클리핑(배회 AI가 인식하는 "발판 끝"이 항상 화면 안), (b) `StickmanBlackboard.EnforceScreenBoundsAndRescue()`가 `StickmanAgent.Update()` **맨 마지막**에 캐릭터 OS 좌표를 화면 안으로 하드 클램프. 어떤 상태가 몸을 어디로 옮겼든 되돌린다.
7. **최종 안전망** — Fall이 6초 이상 이어지면 `RescueToSafeGround()`가 화면 가로 중앙의 지면으로 강제 복귀 + Idle.

### (D) 안전망 발판과의 관계 (실측 확인)

- `FallbackPlatformWindowService`는 실제 발판 **뒤에** 안전망(handle `-1`)을 붙이고, `Sense()`는 목록 순서대로 첫 매치를 채택하므로 실제 창이 항상 우선한다 — 실측으로 확인(`딛고있음=실제 창: Finder`가 안전망보다 먼저 채택됨).
- 발판 고착 도입 후에는 순서 의존이 아예 사라졌다(핸들로 직접 지목).
- **정직한 한계**: 안전망은 화면 세로 80% 지점(OS y=785.6)에 있다. 화면 전체를 덮는 창이 모든 발판을 가리면 캐릭터가 여기에 착지하는데, 그 위치는 Dock보다 121pt 위라 **여전히 "창 한가운데 떠 있는" 것처럼 보일 수 있다.** 안전망을 화면 바닥으로 내리려면 `NullPlatformWindowService.DummyFootholdHeightFraction`(=RAGDOLL 물리 바닥/캐릭터 스폰 Y의 단일 소스)을 함께 옮기고 씬/프리팹을 재생성 + PlayMode 프레이밍 테스트 기준선을 다시 잡아야 해서 이번 범위 밖으로 둔다. **다음 라운드 후보.**

### (E) 리더가 로그만으로 판별할 수 있게 만든 진단 (상시)

- `[발판리포트]` (2.5초) — `보이는 상단테두리 N개(원본창 M개 중 완전히 가려져 제외 K개)=[앱이름@(x,y wxh) ...] | 딛고있음=실제 창: Finder / 합성 안전망 / (공중) | 고착핸들 | 발판상단OS y | 캐릭터OS=(x,y) 화면안=예/아니오 | 상태 | 오버레이원점/창/Screen`
- `[창진단]` (7.5초) — 창 전체 덤프: **z-order + 앱이름 + PID + 사각형 + alpha + onscreen + 가려짐 후 보이는 상단폭**, 그리고 **탈락 창과 사유**.
- 이벤트 로그: `[FallState] 착지 확정`(발판핸들/실제창·안전망 구분/낙하높이), `[발판변경] 이전→이후 (사유)`, `[발판상실] (사유 3종 안내)`, `[화면클램프]`, `[캐릭터구조]`.
- `StickConfig.footholdPollInterval` 0.5 → **0.3초**(창 변화 반응 지연 단축).

### 실측 증거 (Player.log, 임시 자가 테스트로 재현)

| 검증 항목 | 결과 |
|---|---|
| 실제 창 위 착지 | `[FallState] 착지 확정 — 발판핸들=3897(실제 창), 착지 월드Y=8.679` (표적 Finder 창 상단과 정확히 일치) |
| 착지 좌표 정확도 | Finder 창 상단 **OS y=160.0** vs 캐릭터 **OS y=160.0** — **오차 0.0pt** |
| 창 위 보행 | 캐릭터 OS x가 456→631→714→671→569로 이동, 전 구간이 창 가로범위 [180,880] 안 |
| 창 이동 추종 | 창이 (180,160)→(234,392)로 옮겨지자 캐릭터도 발판상단 OS y=392로 함께 이동 |
| 창 좌우 끝 이탈 → 낙하 | 창 오른쪽 끝 바깥(OS x=940)에 놓자 15.29유닛 낙하 후 안전망 착지 |
| **최대화 시 낙하(신고 2)** | 전체 덮는 창 생성 → `[발판상실] 핸들=865` → `원본창 3개 중 완전히 가려져 제외 2개` → 안전망 착지 |
| **가려짐 복구** | 덮는 창을 닫자 두 창 모두 `보이는상단폭=700 / 1512`로 즉시 복귀, 탈락 0개 |
| **화면 밖 복귀(신고 4)** | OS (2012,491)→(1504,491), OS (756,-400)→(756,8) — `[화면클램프]` 2/2 성공 |

### 검증
- 컴파일 **에러 0 / 경고 0**, 빌드 **Succeeded 0/0**
- **EditMode 13/13, PlayMode 3/3** (기준선 유지)
- 실행 실측 **예외/에러 0건**
- 임시 검증 코드(`TickLandingSelfTest` / `TickOffscreenRescueSelfTest`)는 검증 후 **전부 제거**했다(137줄). 상시 진단 로그만 남겼다.
- 테스트용으로 띄운 Finder 창은 전부 정리했다. git commit 없음(리더 지시).

### 교차 레이어 영향
- `GroundSensor.GroundInfo`에 `GroundedFootholdHandle` 추가(기본값 있는 선택 인자라 기존 호출부 무영향).
- `GroundSensor.Sense()`에 `preferredHandle` 선택 인자 추가 — **States 레이어 전체의 접지 의미가 "매 프레임 재선택"에서 "고착 추적"으로 바뀌었다.** 몸을 임의 위치로 옮기는 상태(Drag/Rodeo/Ragdoll)는 낡은 핸들 때문에 접지 실패 → 0.1초 뒤 Fall → `Enter()`가 핸들 초기화 → 재획득으로 **스스로 회복**한다(고착이 남지 않는다).
- `FallbackPlatformWindowService.Inner` / `SyntheticFootholdHandle` 공개(진단 전용).
- **테스트 갭 인지(리더 지적)**: `StickmanOnScreenFramingTests`는 `NullPlatformWindowService`의 합성 발판만 쓰므로 실제 창 발판 환경의 화면 경계 이탈을 재현하지 못한다. 그래서 6번(하드 클램프)을 테스트가 아니라 **런타임 불변조건**으로 구현했다.


---

## 안전망 발판을 화면 최하단(Dock 위)으로 — "지금도 떠있는것처럼보임" (2026-08-28, Coder)

사용자 신고: 직전 커밋 `c721251`에서 오클루전 컬링·발판 고착·화면 클램프까지 고쳤는데도 **여전히 캐릭터가 화면 중간에 떠 있는 것처럼 보인다**. 원인은 직전 라운드가 위 (D)절에 "정직한 한계"로 스스로 남긴 그 항목 — 안전망 발판이 화면 세로 **80% 지점(OS y=785.6)** 에 있어, 딛을 창이 하나도 없으면 캐릭터가 화면 바닥에서 **196pt 위**(≈ 화면 한가운데)에 서 있었다.

### (A) 바꾼 값 — 단일 소스 상수 하나

`Platform/NullPlatformWindowService.cs`:

```
DockSafeBottomInsetPoints   = 75f    // macOS Dock 실측 높이(OS pt)
ReferenceScreenHeightPoints = 982f   // 그 Dock을 실측한 화면 전체 높이
DummyFootholdHeightFraction = 75/982 ≈ 0.0764   (이전 0.2)
```

실측 근거는 직전 라운드에 이미 확보돼 있었다(`MacOverlayStateEnforcer`의 전체화면 확장 주석): 라이브러리 `GetMonitorRect()`의 작업영역 `(0,75,1512,874)` vs `CGDisplayBounds`의 화면 전체 `1512x982` → 메뉴바 33pt + **Dock 75pt**. 즉 Dock 상단 = OS y **907**.

**왜 런타임 Dock 조회가 아니라 상수(비율)인가**: 이 값은 `SceneBootstrapper`가 **씬 에셋에 굽는** 지면/스폰 Y의 단일 소스이기도 하다. 런타임 조회값과 구운 값이 갈리면 이 프로젝트가 반복해서 겪은 "두 곳이 따로 계산해 어긋나는" 버그(BUG-P1-R4-B1, BUG-P1-R5-B2)가 그대로 재발한다. 비율이 상수여야 `groundTopWorldY = cam.y - orthoSize*(1-2f)`라는 해상도 무관 폐쇄형이 유지된다.

### (B) 연쇄 갱신 — grep 전수 확인

| 항목 | 이전 | 이후 | 갱신 경로 |
|---|---|---|---|
| 안전망 발판 상단(실제 빌드) | OS y 785.6 | **907.0** | `FallbackPlatformWindowService`가 상수를 그대로 참조 — 코드 무수정 |
| 더미 발판 상단(에디터/배치) | `h*0.8` | `h*0.9236` | 동일 상수 |
| 캐릭터 스폰 Y(씬 에셋) | -6.9 | **-9.867** | `ComputeGroundTopWorldY()+0.3` |
| `PhysicsGround` Y(RAGDOLL 바닥, 씬 에셋) | -8.2 | **-11.167** | `ComputeGroundTopWorldY()-1.0` |
| 지면 월드 Y | -7.2 | **-10.167** | `cam.y - 12*(1-2f)` |
| 카메라 프레이밍 | 무변경 | 무변경 | cam(0,0)/orthoSize 12가 화면 전체와 1:1 — 발이 뷰포트 하단에서 1.83유닛(=화면상 정확히 Dock 75pt) 위 |

`grep -rn "DummyFootholdHeightFraction\|ComputeGroundTopWorldY" Assets` 전수 확인: 실제 계산 지점은 **3곳뿐**(`NullPlatformWindowService:145`, `FallbackPlatformWindowService:156`, `SceneBootstrapper:759`)이고 나머지는 전부 주석/문서. 매직 넘버로 따로 계산하는 곳은 없다. 씬은 구운 값이므로 `BuildAll --force`로 재생성했다(프리팹은 재생성 결과가 바이트 동일 — 오버라이드 고아 없음).

### (C) 실측 (딛을 창 없음 = 안전망 위)

```
[발판리포트] ... | 딛고있음=합성 안전망 | 발판상단OS y=907.0 |
             캐릭터OS=(442.8,907.0) 화면안=예 | 상태=Idle | 창=(1512,982)
```
캐릭터 발 = **OS y 907.0** = 화면 높이 982 − Dock 75. 이전 785.6에서 **121pt 내려왔고**, Dock 바로 위에 선다(Dock에 가려지지도, 허공에 뜨지도 않음).

### (D) 테스트 — 기준선 수치 변경 없음, 검증은 오히려 강화

- **EditMode 13/13, PlayMode 3/3 그대로**(개수·이름 무변경). 기존 프레이밍 테스트는 "뷰포트 대비 0.5유닛 여백"이라는 **상대** 판정이라 새 지면에서도 그대로 통과했다(실측 bottomScreen.y=24.5px vs 요구 10px). 즉 기준선을 새 값에 맞춰 느슨하게 고칠 필요가 없었다.
- 대신 **공백을 메웠다**: 기존 3종은 "캐릭터가 화면 **한가운데** 떠 있어도" 전부 초록불이었다(그래서 이 버그가 두 라운드를 살아남았다). `StickmanOnScreenFramingTests`의 각 샘플에 접지(Idle/Walk) 시 **"발 Y == 발판 상수가 말하는 Y"** 대조를 추가했다(허용오차 1유닛, 회귀 시 오차 ≈3유닛이라 확실히 걸린다). 이 검사는 (1) 안전망이 다시 화면 중앙으로 올라가는 회귀와 (2) **씬에 구운 Y와 상수가 어긋나는 회귀**(과거 두 번 발생, 자동 테스트가 한 번도 못 잡던 계열)를 동시에 잡는다. 실측 오차 0.002유닛. 접지 샘플이 0건이면 실패 처리(무한낙하 은폐 방지).

### (E) 진단 로그 정리 — 삭제 아닌 스위치

`StickConfig.verboseDiagnosticsLogging`(기본 **false**) 신설. 근거: 직전 라운드 Player.log 실측 **443줄 중 372줄(84%)** 이 `[발판리포트]`(2.5초)+`[창진단]`(7.5초)이라 경고/예외가 묻혔다 — 24시간 상주 앱에서는 그 자체가 결함.

| 로그 | 기본(false) | 켰을 때(true) |
|---|---|---|
| `[발판리포트]` | **60초 심장박동**(재빌드 없이 "지금 뭘 딛고 있나" 항상 확인 가능) | 2.5초 |
| `[창진단]` 창 전체 덤프 | 남기지 않음 | 7.5초 |
| 히트테스트 프로브(시작 25초) | 남기지 않음 | 1초 |
| `[화면클램프]` | **항상**(최소 2초 간격 throttle 신설 — 가장자리를 계속 밀면 매 프레임 성립해 초당 수십 줄이 될 수 있었다) | 동일 |
| `[캐릭터구조]`/`[발판변경]`/`[발판상실]`/`[FallState] 착지 확정` | **항상**(이상 신호는 조용해질 이유가 없다) | 동일 |

부착 로그가 현재 모드와 켜는 법을 같이 찍는다 → 리더가 로그만 보고 "지금 조용한 모드구나, 체크박스 켜면 되겠구나"를 안다. 실측: 90초 실행에 로그 **49줄**(이전 같은 구간 ≈36줄이 리포트만).

### 검증
- 컴파일 **에러 0 / 경고 0**, 빌드 **Succeeded 0/0**
- **EditMode 13/13, PlayMode 3/3**
- 실행 실측 예외/에러 **0건**, `화면안=예`, 상태 Idle 정상
- git commit 없음(리더 지시)

---

## 드래그 순간이동 수정 + Dock 발판/바닥 안전망 + 앱 제어 수단 + 눈 커서 추적 (2026-08-28, Coder)

리더 지시 우선순위 ①위쪽 착지 오판 → ②Dock 발판+바닥 안전망+클램프 여유 → ③종료 수단 → ④눈 추적 → ⑤매달려 내려가기.
**①~④ 완료, ⑤는 다음 라운드로 이월**(리더가 명시적으로 허용한 범위).

### ① 사용자 신고 "마우스로 끌었는데 갑자기 다른창위로 올라감" — 원인 2개, 둘 다 수정

**리더 가설(스윕 교차 판정에 하강 조건이 없다)은 코드 확인 결과 사실이 아니었다.** `GroundSensor.TryFindLandingCrossing()`에는 처음부터 방향 조건이 있었다(`currOs.y <= prevOs.y`면 즉시 false + 상단선을 위→아래로 지날 때만 인정). 진짜 원인은 다른 곳에 두 개 있었다.

**(원인 1, 주범) `DragThrowState.FollowCursor()`의 지면 소프트 클램프가 "지면"을 잘못 물었다.**
클램프 식은 `if (desired.y < ground) desired.y = ground;` 라 **위로만** 작동하는 단방향 연산인데, ground를 `GroundSensor.TryGetSurfaceWorldY`(= 그 x에서 **가장 높은** 창 상단)로 물었다. 그래서 커서 x가 화면 위쪽 창의 가로 범위에 걸치기만 하면 화면 아래에서 끌던 캐릭터가 **매 프레임 그 창 상단으로 끌어올려졌다.**
- 실측 규모(PlayMode 테스트 로그): 드래그 위치 월드Y **-9.88** / 예전 클램프 기준(가장 높은 표면) **+8.16** → 한 프레임에 **18.0유닛 순간이동**.
- 수정: `GroundSensor.TryGetFloorWorldY()` 신설(= 그 x에서 **가장 낮은** 표면 = 진짜 바닥). 클램프의 원래 목적("세상 바닥 밑으로 내려보내 Fall 영구 고착되는 것 방지")은 그대로 보존된다.
- 같은 계열의 오류가 `Interaction/RodeoCursorWatcher.cs`에도 있었다(화면 위에 창이 하나만 있어도 그 아래 전 영역이 "지면 아래"로 판정되어 로데오가 사실상 영구 억제). 함께 교체.

**(원인 2, 던진 직후) `FallState`의 2순위 착지 판정(허용오차 밴드+유예)에 방향 개념이 아예 없었다.**
위로 던지면 상승 중에 창 상단선의 ±`groundSnapTolerance` 밴드에 들어가고, 포물선 정점 부근은 속도가 0에 가까워 `fallGraceDuration`(0.1초)을 쉽게 채운다 → **지나쳐 올라가던 창 위에 착지**. 몸이 위로 움직이는 동안(`linearVelocity.y > 0.05`)에는 이 경로도 성립하지 않게 가드 추가.

**드래그 중 접지 판정 확인(리더 요청)**: `DragThrowState.Tick()`은 `SenseGround`/`GroundedTick`을 호출하지 않는다 — 드래그 중 접지 판정은 원래부터 비활성이었다. 발판 고착도 정상: 놓기→Fall 진입에서 `CurrentFootholdHandle=0`으로 해제되고 착지에서 재설정된다(실측 로그 `[발판변경] -2 -> 0 (Fall 진입) → 0 -> -1 (착지)`).

**회귀 테스트 신설** `Tests/PlayMode/FootholdLandingDirectionTests.cs` (3종, PlayMode 3→6):
`FloorProbeReturnsLowestSurfaceSoDragNeverLiftsCharacterUp` / `UpwardPassThroughFootholdTopDoesNotLand` / `DownwardPassThroughFootholdTopStillLandsOnThatWindow`(정상 낙하 착지가 죽지 않았음을 반대편에서 확인, 착지 Y 오차 0.01 이내).

**실행 실측(빌드된 .app + 합성 마우스 이벤트로 실제 드래그 재현)**: 화면 전폭을 덮는 창(Cursor, 상단 OS y=33 = 월드 +11.19)이 열린 상태에서 캐릭터를 화면 전체에 걸쳐 끌었을 때 몸통 월드Y가 **-6.21 ~ -0.10** 범위에 머물렀고(밀착 오차 0.000유닛), 그 창 상단으로 끌려 올라가지 않았다. 예전 코드였다면 모든 프레임이 +11.19였어야 한다.

### ② Dock 발판 + 화면 최하단 안전망 + 클램프 여유

**★ 리더 지시 1항("Dock 프로세스가 소유한 창의 실제 사각형을 발판으로 써라")은 실측 결과 불가능하다.**
`CGWindowListCopyWindowInfo` 전수 덤프(2026-08-28, 이 환경):
```
owner='Dock' name='Dock'       layer=20  alpha=1.0  rect=(0, 0, 1512, 982)
owner='Dock' name='Wallpaper-' layer=-2147483624    rect=(0, 0, 1512, 982)
```
**Dock 창의 bounds는 Dock 막대가 아니라 화면 전체다.** 그대로 발판으로 쓰면 화면 전폭 발판이 화면 **맨 위**(y=0)에 생겨 지금보다 훨씬 나빠진다. 다른 경로도 전부 확인/차단:
- `com.apple.dock` 환경설정 — tilesize(49)/persistent-apps(13)/recent-apps(3)는 읽히지만 **실행 중 앱 타일 수**를 알 수 없어 폭 계산 불가(예측 가능한 17타일로는 실제 폭이 나오지 않는다).
- `CGWindowListCreateImage`로 Dock 창만 캡처해 알파 경계를 재면 **정확히** 나온다(실측: **x 221~1290, 폭 1069pt, 화면 가로 정중앙, 두께 68pt**). 하지만 이 API는 macOS 10.15+에서 **화면 기록 권한**을 요구하고 권한 팝업을 띄운다 → 비침해 원칙(CLAUDE.md 2)과 "권한 없이 동작"이라는 플랫폼 계약에 정면으로 어긋나 **채택하지 않음**.

**따라서 "정확히 알 수 있는 것만 실측값, 알 수 없는 폭만 설정값"으로 나눴다** (`FallbackPlatformWindowService.TryGetDockFoothold`, 핸들 **-2**):
- 세로(정확): 상단 = 화면 바닥 − `StickConfig.dockFootholdThicknessPoints`(기본 75 = 실측 Dock 두께). 0으로 두면 Dock 발판 자체가 사라진다(자동 숨김/좌우 Dock 대응).
- 가로(추정): 화면 가로 정중앙 정렬 + `StickConfig.dockFootholdWidthFraction`(기본 **0.65**). 실측 폭 비율 0.707보다 **일부러 좁게** 잡았다 — 넓으면 Dock 없는 자리에 캐릭터가 서서 사용자가 신고한 "공중 부양"이 재발하지만, 좁으면 실제 Dock 안쪽에서 조금 일찍 떨어질 뿐이라 **틀리는 방향을 안전한 쪽으로 고정**했다. 실행 실측 발판: `(265,907 983x75)` → 실제 Dock `[221,1290]` 안에 완전히 포함.

**바닥 안전망**: 단일 소스 상수 하나만 교체 — `DockSafeBottomInsetPoints(75)` → `BottomSafetyNetInsetPoints(40)`, `DummyFootholdHeightFraction = 40/982 ≈ 0.0407`. 여기서 (a) 더미 발판, (b) 실배포 안전망, (c) 씬에 굽는 지면/스폰/RAGDOLL 바닥 Y, (d) PlayMode 프레이밍 테스트 기대값이 전부 자동 파생된다(`--force` 재생성으로 확인: 스폰 Y −9.867 → **−11.4556**, 실행 실측 발판 상단 OS y **907 → 942**).
- **왜 0(화면 맨 아래)이 아닌 40pt인가 — 테스트가 잡아준 값이다.** 처음에 10pt로 잡았더니 프레이밍 테스트가 즉시 빨간불: 루트(발)는 발판에 정확히 놓이지만 **렌더러 바운즈 아래끝이 루트보다 0.55월드유닛 더 내려간다**(실측: 루트 −11.60, bounds.min.y −12.15) → 발끝이 화면 밖으로 잘림. 40pt면 발이 뷰포트 바닥에서 0.98유닛 위 = 바운즈까지 0.43유닛 여유(RAGDOLL 벌어짐 흡수). 40pt는 Dock 띠(하단 75pt) **안쪽**이라 "Dock 바깥에서는 바닥으로 내려간다"는 요구를 시각적으로 만족한다.

**부수 사고 1건과 그 수정(테스트가 잡음)**: 안전망이 내려오자 화면 하드 클램프가 **지면과 싸우기 시작했다**. 640x480 테스트 화면에서 하단 여유 8 OS px = 0.4월드유닛으로 지면(0.245유닛)보다 위라, RAGDOLL이 지면에 내려앉을 때마다 클램프가 매 프레임 위로 되돌리고 세로 속도를 0으로 만들어 **영원히 안정되지 못했다**(`StickmanRagdollRecoveryTests`가 GETUP 미도달로 실패). → 하단 클램프 여유를 **0**(화면 경계 그 자체)으로 바꿨다. 이 클램프의 목적은 "화면 밖에서 잃어버리지 않는다"이고 발판/지면은 언제나 화면 안이므로 정상 동작에서는 발동하지 않는다.

**스폰 위치 변경(필수 연쇄)**: 안전망(942)이 Dock(907)보다 **아래**가 되면서, 예전처럼 안전망 바로 위에 스폰하면 캐릭터가 **Dock에 영원히 올라갈 수 없다**(착지는 상단선을 위→아래로 가로질러야만 성립, 자율 배회 점프 높이 0.61유닛 < 필요 1.29유닛). 스폰을 **화면 세로 중앙(카메라 y)** 으로 옮겨 첫 프레임부터 자유낙하해 그 x의 가장 높은 표면(창 → Dock → 바닥)에 자연 착지하게 했다. 실행 실측: `[FallState] 착지 확정 — 발판핸들=-2(Dock), 낙하높이=9.99유닛`.

**화면 클램프에 캐릭터 시각 폭 반영**(리더 관찰 "화면 왼쪽 끝에서 잘려 보인다"): `StickmanBlackboard.CharacterVisualHalfWidthWorld` 신설, `StickmanAgent`가 렌더러 바운즈에서 0.25초마다 갱신(포즈에 따라 팔 벌린 폭이 바뀌므로 상수 불가). 실측 로그: `[화면클램프] OS (2.0,445.5) -> (58.2,445.5), 좌우여유=58.2pt(기본 8 + 시각반폭 50.2)`.

**동작 실측(요청한 시나리오 그대로)**: Dock 위(OS y=907)를 걸어다니다가 → 캐릭터를 Dock 가로범위 밖(OS x=90)에 놓자 → `[발판변경] -2 -> 0 (Fall)` → `[FallState] 착지 확정 — 발판핸들=-1(화면 최하단 안전망), 착지 월드Y=-11.022` → OS y **942**에서 보행. 그 뒤 다시 Dock 가장자리에서 ParkourClimb로 Dock 위로 복귀하는 것도 관찰됐다(기존 파쿠르 로직이 35pt 단차를 그대로 처리).

### ③ 앱 제어 수단 — 이제 터미널 없이 끌 수 있다

리더 제시 3안 중 **2안+3안을 함께** 채택(1안 NSStatusItem은 네이티브 Objective-C 플러그인이 필요한데 이 프로젝트는 자체 플러그인을 전부 제거하고 UniWindowController로 교체한 이력이 있어 되돌리는 비용/위험이 라운드 예산 초과).

**2안 전역 단축키** — 핵심 미지수였던 "권한 없이 키 상태를 읽을 수 있는가"를 **먼저 실측**했다: `CGEventSourceKeyState`는 권한 없이 호출해도 크래시 없이 false를 돌려주고, 세션에 실제 키 이벤트가 들어오면 true로 바뀐다(떼면 즉시 false). 이미 쓰고 있는 `CGEventSourceButtonState`와 같은 계열의 조회 전용 API다.
- `Platform/IGlobalKeyStateService.cs` 신설 + `MacWindowService` 구현. **키 7개(Ctrl/Opt/Cmd/Q/C/D/R)만** 열거형으로 노출 — 전체 키맵을 노출하면 조회 전용이라도 사실상 키로거 형태가 되므로 범위를 타입 수준에서 못박았다.
- 조합: **Ctrl+Option+Cmd + Q**(종료) / **C**(잉크색) / **R**(로데오 on-off) / **D**(진단 로그 on-off). Cmd+Shift+Q는 macOS 로그아웃, Cmd+Q는 활성 앱 종료라 둘 다 쓸 수 없다. Ctrl+Opt+Cmd는 시스템/일반 앱이 거의 쓰지 않아 오발동 위험이 사실상 없다.
- **실측**: 빌드된 .app 실행 중 4개 조합을 합성 키 이벤트로 눌러 전부 반응 확인(`[앱제어] 잉크색 전환 -> White` / `로데오 커서 켬` / `진단 로그 켬(촘촘)`).

**3안 캐릭터 우클릭 메뉴**(2안의 이중화 — 향후 macOS가 이 API에 TCC 권한을 요구하게 되어도 종료 수단이 죽지 않도록 단일 실패점을 없앤다): `Interaction/AppControlDirector.cs`.
- 캐릭터 우클릭 → 캐릭터 옆에 패널(앱 종료 / 잉크색 / 로데오 커서 / 진단 로그 / 닫기). 좌클릭은 이미 드래그&던지기가 쓰므로 우클릭을 쓴다(`IGlobalPointerButtonService.TryGetSecondaryButtonPressed` 신설, `CGEventSourceButtonState`의 버튼 번호만 다름).
- **버튼 히트테스트를 uGUI EventSystem이 아니라 전역 커서 좌표로 직접 판정**한다 — 클릭관통 오버레이에서는 창이 마우스 이벤트를 실제로 받는다는 보장이 없기 때문(StickmanClickHitbox가 같은 이유로 이미 쓰는 방식). `ScreenCoordinateConverter.OsScreenToUnityScreen()` 신설(좌표 변환은 이 클래스만 담당한다는 BUG-M5 컨벤션 유지).
- 12초 무동작 자동 닫힘 / 메뉴 밖 클릭 = 취소 / 메뉴 영역 히트테스트 차단막(isTrigger라 캐릭터 물리에 무관).
- **실측(합성 마우스 이벤트로 실제 조작)**: 캐릭터 우클릭 → `[앱제어] 캐릭터 우클릭 — 제어 메뉴를 열었습니다` → 계산한 [앱 종료] 행 좌표 클릭 → `[앱제어] 종료 요청(우클릭 메뉴) — Application.Quit()` → **프로세스 실제 종료 확인**.

**기존 안전장치 무변경 확인**: 이 컴포넌트는 `SetClickThrough`를 한 번도 호출하지 않는다. 시작 5초 클릭관통 지연(`ClickThroughSafetyDelaySeconds`)과 Escape 긴급 해제(`EmergencyDisableKey`)는 코드/동작 모두 그대로이며, 실행 로그에서 5초 뒤 `clickThrough=True, hitTest=True`로 정상 전환되는 것을 확인했다.

### ④ 눈 커서 추적 (사용자 명시 요청 "마우스위치에 따라 눈도 움직여야")

`EyeController.TickLookAt()` 하나가 매 프레임 진입점(`StickmanBlackboard.TickPose` 마지막 줄). 커서 좌표는 기존 채널을 그대로 재사용(`TryGetCursorWorldPosition` → `StickmanAgent.TryGetCursorPosition` → `ICursorPositionService`) — 새 배관 없음.
- **링 밖으로 못 나간다**: `MaxSafePupilOffset=0.09`를 프리팹 실측치에서 유도(링 안쪽 가장자리 0.1885 − 눈 중립 거리 0.0776 − 눈동자 반경 0.018 = 0.0929). 설정값은 항상 이 상한으로 clamp되므로 구조적으로 불가능.
- **부드럽게**: 기존 컨벤션과 같은 프레임레이트 독립 지수 감쇠 `1-exp(-k·dt)`, k=`eyeTrackingFollowRate`(12).
- **가까우면 중립 / 멀면 포화**: 머리~커서 거리를 [`eyeTrackingNeutralRadiusWorld`(0.6), `eyeTrackingFullRangeWorld`(4)] 구간에서 0~1로 정규화.
- **RAGDOLL 대응**: 눈은 머리의 자식이라 따라 도는 것은 자동이지만, 그대로 두면 머리가 뒤집혔을 때 눈이 엉뚱한 쪽을 본다. 월드 방향을 `Transform.InverseTransformDirection`으로 **머리 로컬 공간**으로 변환해 적용하므로 머리가 어떤 각도로 뒹굴어도 화면상 커서 쪽을 계속 본다.
- `StickConfig`: `eyeTrackingEnabled`(기본 ON) / `eyeMaxPupilOffset` / `eyeTrackingFollowRate` / `eyeTrackingNeutralRadiusWorld` / `eyeTrackingFullRangeWorld`.
- **실측 로그**(`[눈추적]`, 시작 직후 6회 + verbose 시 2초 주기):
  - 먼 커서: 거리 23.02유닛 → 눈동자오프셋 (0.0233,0.0442) **길이 0.0500 = 최대치에서 포화** ✔
  - 캐릭터가 걸어가며 커서와의 각도가 변함: 시선 x가 0.467 → 0.340 → 0.258로 연속 변화 ✔
  - 커서를 캐릭터 위에 올림: 거리 1.10유닛 → 오프셋 길이 0.0185로 축소(중립 방향으로 감쇠 중) ✔

### ⑤ 매달려 내려가기 — **이월**
리더가 "여유 있으면"으로 지정한 항목. ①~④의 검증(합성 입력으로 드래그/우클릭 메뉴/종료/Dock 낙하를 실제로 재현)에 시간을 썼고, 무리해서 붙이는 것보다 다음 라운드에 `ParkourClimbState` 확장으로 제대로 하는 편이 낫다고 판단했다.

### 검증
- 컴파일 **에러 0 / 경고 0**, 빌드 **Succeeded 0/0**
- **EditMode 13/13** (무변경), **PlayMode 6/6** (기준선 3 + 신규 회귀 3)
- 실행 실측 예외/에러 **0건**, `화면안=예`
- 임시 검증 코드는 프로젝트에 남기지 않았다 — 모든 실행 검증은 프로젝트 밖 스크립트(합성 CGEvent 입력)로 수행했고, 상시 진단 로그만 남겼다.
- 검증 중 열린 앱 창(메모)은 정리했다. git commit 없음(리더 지시).

### 교차 레이어 영향
- `IGlobalPointerButtonService`에 `TryGetSecondaryButtonPressed` 추가 — 구현체는 `MacWindowService`/`FallbackPlatformWindowService` 2개뿐이라 둘 다 갱신 완료.
- `IGlobalKeyStateService`/`GlobalKey` 신설(Platform 레이어). 미지원 플랫폼은 구현하지 않으면 되고, 소비 측은 `as` 캐스팅 null 판정.
- `GroundSensor.TryGetFloorWorldY` 신설. **`TryGetSurfaceWorldY`(가장 높은 표면)는 그대로 남겨두었다** — `RescueToSafeGround`(화면 중앙 최상단 지면으로 복귀)는 여전히 "가장 높은" 쪽이 맞다. 둘을 혼동하면 이번 버그가 재발하므로 각 문서에 용도를 명시했다.
- `StickmanAgent.Config` 프로퍼티 공개(기존 private 필드 노출, 새 로직 없음).
- **합성 발판 핸들 규약 확장**: -1 = 화면 최하단 안전망, **-2 = Dock**(신규). 진단 로그/착지 로그가 둘을 구분해 표시한다.
- 프레이밍 테스트의 하단 여백 상수를 `MinBottomWorldMarginUnits`(0.05)로 분리 — 안전망이 **의도적으로** 화면 최하단으로 내려갔으므로 기존 0.5유닛은 정상 동작을 실패로 판정한다. 상단은 완화하지 않았고, "발 Y == 발판 상수" 대조는 그대로 유지(오차 실측 0.001유닛).

---

## 매달려 내려가기(LedgeHang) + 보행 접지 보정 (2026-08-28, Coder)

### ① 매달려 내려가기 — 사용자 명시 요청 "내려갈때도 매달려서 내려가는형태로" (직전 라운드 이월분 완료)

**설계 판단: `ParkourClimbState` 확장이 아니라 신규 `States/LedgeHangState.cs`.** 근거는 코드를 읽고 내린 결론이며 `StickmanStateId.LedgeHang` 문서에 남겼다 — ParkourClimb은 "시작Y→벽상단Y를 진행도 하나로 Lerp"하는 **단일 페이즈**에 종료가 Idle/Walk인 반면, 매달려 내려가기는 **잡기→매달림→손놓기 3페이즈**에 종료가 **항상 Fall**이라, 두 상태가 실제로 공유할 코드는 "발판 핸들 재확인" 한 줄뿐이다. 그건 이미 `GroundSensor`의 정적 유틸이라 상태를 합치지 않고도 그대로 재사용된다. 모드 플래그를 넣었으면 거의 모든 줄에 분기가 생겨 이미 검증된 등반 경로까지 회귀 위험에 노출됐을 것이다.

**트리거(확률)**: `IMovementIntentSource`에 `LedgeHangRequested` 펄스 신설(`JumpRequested`와 동일한 1프레임 계약). `AutoWanderController`가 발판 경계에서 기존 두 분기(점프 시도 / 정지 후 반대 방향)보다 **먼저** `StickConfig.ledgeHangChance`(기본 **0.35**)를 추첨한다. 세 조건이 전부 참일 때만 발동: (1) 이 걷기 구간에서 아직 추첨하지 않음(`_ledgeHangRolledThisLeg` — 매 프레임 재추첨하면 경계에 머무는 몇 프레임 동안 확률이 사실상 1이 되어 "일부만 매달리게"라는 요구가 무의미해진다), (2) 화면 자체의 끝이 아님, (3) **내려앉을 발판이 실제로 존재함**. `ledgeHangChance=0`이면 직전 라운드까지의 거동과 100% 동일하다.

**신규 감지 로직** `GroundSensor.TryFindDescendTarget()` — `TryFindClimbableWall()`의 정확한 반대 방향. 모서리 바깥 `ledgeHangEdgeOffset`만큼 나간 x를 세로 범위에 포함하면서 상단이 **매달린 발보다 아래**인 발판 중 가장 높은 것을 고른다. `minDropDepth`는 임의값이 아니라 `StickmanBlackboard.LedgeHangDropDepth`(= 손끝~발끝 거리, 프리팹의 실제 어깨 높이+팔 길이에서 유도, 실측 **2.507유닛**)다. 이 조건이 있어야 "매달렸더니 발이 이미 목적지를 지나쳐 더 아래로 떨어지는" 경우가 원천 차단되고, 덤으로 Dock 턱(35pt=0.86유닛) 같은 미세 단차가 자동으로 걸러진다.

**매달린 자세(신규 포즈)** `StickmanPoseAnimator.ApplyLedgeHangPose()` — 각도 0이 "곧게 아래"인 이 클래스 규약에서 **팔을 위로 뻗는다 = 어깨 180도 근처**다(`180 ∓ spread`). 팔꿈치 약간 굽힘, 다리는 모아 늘어뜨리고 무릎만 조금 접음, 전신에 같은 위상의 사인파 흔들림(다리와 팔은 반대 부호 — 손이 모서리에 고정된 채 몸이 흔들리면 몸에서 본 팔은 반대로 기운다). `StickmanBlackboard.TickPose()`가 상태 ID만 보고 Idle 중립 포즈 자리에 이 포즈를 넣는다(기존 계약 유지). 매달린 루트 Y = 모서리 Y − `HangHandReachAboveRoot()`이며, 이 값도 하드코딩이 아니라 프리팹 지오메트리에서 유도하므로 목 길이/팔 길이를 바꿔도 손이 모서리에서 떨어지거나 파묻히지 않는다.

**안전 규칙(설정이 아니라 코드의 불변식)**: (a) 매 Tick 첫머리에서 붙잡은 핸들 재확인(`TryGetFootholdEdgeWorld` 신설 — 창이 옆으로 움직이면 모서리 X도 따라가야 하므로 상단 Y만으로는 부족) → 사라지면 즉시 Fall, (b) 페이즈 타이머와 **독립적인** 절대 상한 `ledgeHangMaxDuration`(3초), (c) 화면 밖 금지는 진입 조건(화면 끝 제외) + 모서리 바깥 오프셋 제한 + 매 프레임 마지막의 `EnforceScreenBoundsAndRescue` 하드 클램프 3중, (d) `Body.linearVelocity`를 매 프레임 0으로 재확정(BUG-P2-M1과 같은 이유 — 위치를 덮어써도 중력이 속도에 조용히 누적된다).

**실측**(신규 `Tests/PlayMode/LedgeHangDescentTests.cs` 3종, 씬의 실제 StickmanAgent에 결정론적 발판/의도만 주입):
| 검증 | 실측 |
|---|---|
| 정상 시퀀스 | Walk → LedgeHang → Fall → **아래 발판(핸들 7002) 착지**. 매달린 시간 1.23초, 착지 월드Y −8.400(발판 상단과 정확히 일치) |
| 매달린 높이 기하학 | 모서리 Y 6.000 − 손끝~발끝 2.507 = **3.493**, 실측 최저 3.493 (오차 0.000) |
| 발판 소실 시 즉시 낙하 | 붙잡은 발판을 목록에서 제거 → **1프레임** 만에 Fall |
| 무한 매달림 금지 | 유지시간을 999초로 두고 상한 0.90초 → 실제 **0.900초**에 손을 놓음 |

**실행 실측 메모(정직한 한계)**: 지금 이 데스크톱에서는 캐릭터가 Dock(OS y=907) 위에 살고, 그 아래는 최하단 안전망(OS y=942, **0.86유닛**)뿐이라 손끝~발끝(2.507유닛)에 못 미쳐 `TryFindDescendTarget`이 **의도대로 거절**한다 — 즉 실행 중에는 이 기능이 조용하다. 실제 창 위(예: 텍스트 편집기 상단 OS y=140 → Dock까지 17.3유닛)에 캐릭터가 있을 때 발동하는데, 현재 `wanderEdgeJumpAttemptChance=0`(사용자 요청으로 점프 끔)이라 캐릭터가 스스로 창 위로 올라가는 경로가 없다. 그래서 실행 데모 대신 위 PlayMode 실측으로 검증했다(리더 지시의 "코드로 가장자리에 놓고 상태 전이 유도"와 정확히 일치).

### ② 보행 다듬기 — 진행분

**목 길이 절반: 이미 반영되어 있었음(실측 확인).** 프리팹 실좌표 — 어깨 y=**1.7646945**, 몸통 상단(=머리 아래 끝) y=**1.8346945** → 목 구간 **0.070유닛**(지시 전 0.17). 머리 지름 0.44 대비 16%(이전 39%). `SceneBootstrapper`의 `SpecShoulderY=1.28`도 그대로이며 프리팹이 그 값으로 재생성된 상태다. **추가 변경 없음.**

**발 미끄러짐: 자동 회귀 테스트 신설 + 정량 실측.** 신규 `Tests/PlayMode/WalkFootSlipTests.cs`가 3초간 걷게 하고 디딤 국면마다 "디딤발 월드X 이동폭 / 같은 구간 몸 전진"을 잰다. **디딤발은 '더 낮은 발'이 아니라 보행 위상(왼다리 위상<0.5)으로 정의**한다 — 그것이 키포즈 표가 설계한 구간이자 보폭 역산의 기준이기 때문이다.
- **실제 한 걸음 보폭 0.775유닛 vs 명령 0.776유닛(오차 0.1%)** — 즉 "사이클 주파수 = 실제 수평 이동 속도 / 보폭" 역산은 이미 정확하게 동작하고 있다.
- 디딤발 미끄러짐 비율 **0.216**(0=완벽, 1=완전 문워크).

**새로 발견해 수정한 결함 — 바운스 곡선의 위상이 기하학과 반대였다.** 손으로 적은 8키 표 `BounceKeys`(진폭 `walkBounceAmplitude`=0.025)를 다리 기하학과 대조하니 최저점이 t=0.125에 있었는데, 실제로는 그 근처가 최고점이어야 했다. 그 결과 **디딤발이 지면을 최대 0.0252유닛 파고들고(t≈0.12) 최대 0.0696유닛 떠 있었다(t≈0.44)** — 땅에 붙어 있어야 할 발이 계속 지면을 들락거렸다. → `BounceKeys`/`walkBounceAmplitude` 폐기, 신규 `ComputeFootGroundingOffset()`이 **지금 실제로 적용돼 있는 다리 각도**에서 "낮은 쪽 발이 지면에 정확히 닿으려면 몸이 얼마나 오르내려야 하는가"를 매 프레임 계산한다(두 연속 함수의 max라 발이 바뀌는 순간에도 연속, 어떤 발도 지면 아래로 안 내려감). 목표 각도가 아니라 실제 각도를 쓰는 게 핵심 — 지수 감쇠 때문에 실제 진폭이 표보다 작다. 신규 설정은 `walkFootGroundingBlend`(0~1, 기본 1) 하나뿐이고 진폭 자체는 다리 길이·각도에서 자동으로 나온다(실측 약 0.07유닛 = 전신 높이의 3%).
- **접지 오차 실측: 이전 [−0.0252, +0.0696] → 현재 [−0.0124, +0.0388]** (최악값 기준 2.9배 개선). 관절 각도를 하나도 바꾸지 않았으므로 보폭 0.775와 미끄러짐 0.216은 무변경.

**남은 잔여 과제(측정까지 끝냈고 다음 라운드 권고)** — **디딤발 불일치율 20.1%**: 화면에서 더 낮은(=닿아 보이는) 발이 위상상의 디딤발과 다른 시간이 사이클의 20%다(교차 시점이 t=0/0.5가 아니라 ≈0.35/0.85). 원인은 발/발목 마디가 없는 2분절 다리에서 접지 자세(엉덩이+25, 무릎 5 → 깊이 0.876)와 이지 자세(엉덩이 −25, 무릎 10 → 0.822)의 발 높이가 다르다는 것 — 대칭 엉덩이각에서는 무릎 각도만으로 두 높이를 맞추는 해가 존재하지 않는다(`kc = 50 + kt`가 필요). 그 20% 동안 관객은 "흔드는 발이 땅을 긁는다"고 인지한다.
- **시도했다가 되돌린 것(측정 근거와 함께 기록)**: 스윙 무릎을 기하학적으로 더 굽혀 항상 딛는 발보다 위에 있게 하는 클램프를 구현해봤다. 불일치율은 29.5%→**0%**로 떨어졌지만, 접지 직전에 무릎 목표각이 ~57°→5°로 급변해 지수 감쇠가 따라가지 못하고 **실제 보폭이 0.775→0.657(−15%)로 줄어** 디딤발 미끄러짐이 오히려 **0.216→0.564로 악화**됐다(엔진 실측 + 스무딩을 포함한 수치 모델 양쪽에서 동일 결론). 순이득이 없어 **전량 되돌렸다.**
- **권고**: 근본 해법은 "발 궤적을 정의하고 2본 IK로 무릎을 역산"하는 방식이다(디딤 중 발 X를 몸 속도와 정확히 반대로, 발 Y를 상수로 고정 → 미끄러짐이 구조적으로 0). 다만 이는 리더가 지정한 키포즈 표의 역할을 절반 이상 대체하는 **보행 재설계**라 리더 판단이 필요해 이번 라운드에 착수하지 않았다.

### 검증
- 컴파일 **에러 0 / 경고 0**, 빌드 **Succeeded 0/0**(102.7MB)
- **EditMode 13/13**(무변경), **PlayMode 10/10**(기준선 6 + 매달리기 3 + 발 미끄러짐 1)
- 실행 실측 예외/에러 **0건**, `화면안=예`, 캐릭터 Dock 위 정상 배회
- git commit 없음(리더 지시)

### 교차 레이어 영향
- `IMovementIntentSource`에 **`LedgeHangRequested` 추가** — 구현체는 `AutoWanderController`/`RivalPursuitIntentSource` 2개뿐이라 둘 다 갱신 완료(라이벌은 항상 false).
- `StickmanStateId`에 **`LedgeHang` 추가(맨 끝)** — 값 순서를 건드리지 않도록 끝에 붙였다. 이 enum은 어떤 에셋에도 직렬화되지 않음을 grep으로 확인.
- `GroundSensor` 신규 정적 메서드 2개(`TryFindDescendTarget`, `TryGetFootholdEdgeWorld`) + `StickmanBlackboard` 얇은 래퍼 3개(`TryFindDescendTarget`, `TryGetFootholdEdgeWorld`, `LedgeHangDropDepth`). States가 좌표 변환식을 직접 만들지 않는 BUG-M5 컨벤션 유지.
- **`StickConfig.walkBounceAmplitude` 제거 → `walkFootGroundingBlend` 신설**(의미가 "월드 유닛 진폭"에서 "0~1 적용 정도"로 바뀌므로 이름을 함께 바꿨다). `DefaultStickConfig.asset`도 갱신(`walkBounceAmplitude: 0.025` → `walkFootGroundingBlend: 1`).
- `StickmanPoseAnimator.TickWalkPose()` 시그니처의 `bounceAmplitude` → `groundingBlend`. 호출부는 `WalkState` 하나뿐이라 갱신 완료.
- 매달리기용 `StickConfig` 신규 필드 11개(`ledgeHang*`) — 전부 신규라 기존 값과 충돌 없음.
