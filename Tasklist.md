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
| 중력/발판 인식(창 상단 Y 스냅) | Coder | 완료 | Debugger의 EnumerateFootholds 폴링 규율 지적사항 동시 반영. **[Debugger, BUG_REPORT_PHASE0.md BUG-M3/BUG-M5]** `EnumerateFootholds()` "매 프레임 호출 금지" 계약을 강제하는 코드가 전혀 없음(주석뿐) — `StickConfig.footholdPollInterval`을 소비하는 `FootholdPoller` 유틸 없이 Tick()에서 직접 호출하면 Win32는 `EnumWindows`+창마다 3회 P/Invoke라 24시간 상주 앱에 실제 CPU 부담. 또한 좌상단/좌하단 원점 변환·DPI·멀티모니터를 다룰 공용 좌표 변환 유틸이 전무 — 각 상태가 개별 구현하면 좌표계 혼용 버그 위험 높음. 두 인프라 모두 이 작업의 전제로 선행 권고. **[Coder, 2026-08-27]** BUG-M3/M5 반영 완료 — `Platform/FootholdPoller.cs`(주기 폴링+변경시에만 이벤트) 및 `Platform/ScreenCoordinateConverter.cs`(Unity 스크린↔OS 데스크톱, DPI 배율 `StickConfig.desktopDpiScale`) 신규 추가, `States/GroundSensor.cs`가 이 두 유틸만 거쳐 접지 판정(허용오차 `StickConfig.groundSnapTolerance`). 모든 State는 `IPlatformWindowService`를 직접 호출하지 않고 `StickmanBlackboard.SenseGround()`만 사용하도록 강제. |
| 화면 경계 이탈 → 낙하 | Coder | 완료 | **[Debugger]** 위 BUG-M5(좌표 변환 유틸)와 동일 인프라 의존. 교차 레이어 로그 9절-5(모니터 경계 노출, 아직 미반영)도 이 작업과 함께 결정 필요 — "바닥 없는 논리적 간격" 판정은 모니터 경계 API 없이는 구현 불가. **[Coder, 2026-08-27]** "모든 발판의 좌우 범위 이탈 → Fall"은 `StickmanBlackboard.CheckScreenBoundsOrFall()`로 구현 완료(Idle/Walk/Jump/Fall 공통 호출). 단, 9절-5 "모니터 간 논리적 간격"은 여전히 미반영 — `IPlatformWindowService`에 모니터 경계 열거 API가 없어 범위 밖(별도 작업 필요), Debugger 지적대로 미해결 상태 유지. |
| IDLE/WALK/JUMP/FALL 상태 구현 | Coder | 완료 | **[Debugger, BUG-M2]** `StickmanStateMachine.ChangeState()`가 원자적이지 않음 — `_states[next]` 조회가 `Exit()`/`TransitionGeneration` 증가 **이후**에 일어나, 미등록 키로 호출되면 `KeyNotFoundException` 발생 시점에 `_current`가 이미 Exit된 옛 상태를 계속 가리키는 "좀비" 상태로 고착되고 복구 경로가 없음(상태머신 데드락). 여러 상태를 오가는 이 작업에서 가장 먼저 걸릴 수 있는 문제이므로 `TryGetValue` 선검증으로 선반영 권고. **[Coder, 2026-08-27]** BUG-M2 반영 완료 — `StickmanStateMachine.ChangeState()`가 `TryGetValue` 선검증 후 실패 시 뮤테이션 없이 안전 반환하도록 수정. BUG-M1도 함께 반영 — `StateTransitionContext` 생성자/필드, `CurrentTransitionGeneration`을 `public`→`internal`로 좁힘(어셈블리 내부 위조까지는 못 막는 절반의 방어라는 한계는 Debugger 지적대로 유지, Phase 2 토큰화로 완결 예정). Idle/Walk/Jump/Fall Tick() 전이 규칙 실구현(`States/StickmanBlackboard.cs`, `States/GroundSensor.cs` 신규) — Idle<->Walk 입력 기반, Jump 정점통과→Fall, Fall 착지confirm(`fallGraceDuration` 재사용)→Idle/Walk. |
| 클릭 관통 기본 ON | Coder | 진행중 | **[Debugger — BLOCKER, BUG-B1]** `Win32WindowService.CreateOverlayWindow()`는 실제 오버레이 창이 아니라 Unity 게임 자신의 `MainWindowHandle`을 재사용하는 스텁. 지금 상태로 `SetClickThrough(true)`/`SetAlwaysOnTop(true)`를 그대로 호출하면 **게임 창 자체가 클릭관통되어 모든 마우스 입력이 막히고**, 항상 최상단 고정으로 데스크톱을 가릴 수 있음(비침해 원칙 정반대). `WS_EX_NOACTIVATE` 누락으로 포커스 탈취 위험도 있음(가설 H2, 검증 필요). **이 작업을 현재 스텁 그대로 완료 처리하지 말 것 — 별도 HWND 기반 진짜 오버레이 구현이 선행되어야 함.** 상세: `docs/BUG_REPORT_PHASE0.md` Blocker 섹션. **[Coder, 2026-08-27]** Architect 판단대로 "진짜 HWND 오버레이 구현"은 이번 Phase 1 범위를 넘어 보류. 대신 임시 안전가드 적용: `Win32WindowService`에 `_usingUnsafeSelfWindowFallback` 플래그 추가, `SetClickThrough`/`SetAlwaysOnTop`가 이 플래그가 켜진 동안(현재 항상 켜짐) `NotSupportedException`을 던져 게임 창 자체 파괴를 원천 차단. `WS_EX_NOACTIVATE` 상수 추가 및 `SetClickThrough`에 적용(BUG-B1(c)). `Core/StickmanAgent.cs`에 "앱 시작 시 SetClickThrough(true) 호출 지점"을 마련했고, 이 예외를 잡아 로그만 남기고 나머지 초기화는 계속 진행하도록 처리 — 따라서 Windows에서는 클릭관통이 아직 실제로 켜지지 않는다(의도된 안전 실패). 그래서 이 행은 완료 처리하지 않음 — 실제 분리 오버레이(CreateWindowEx) 구현이 후속 작업으로 남아있음. 커서 좌표 조회는 `ICursorPositionService`(신규, `Platform/ICursorPositionService.cs`)로 클릭관통과 완전히 독립된 경로에 배선 완료(UX 9절-3), Win32/Null 양쪽 구현. |
| 전체화면 게임 감지 → 자동 숨김 | Coder | 완료 | **[Debugger]** `IsFullscreenAppActive()`의 "전경 창 사각형 == 모니터 전체 사각형" 휴리스틱은 향후 진짜 오버레이가 화면 전체 크기 투명 창으로 구현되면 자기 자신을 오탐할 위험(현재 `fg == _overlayHwnd` 자기 제외로 방어하나 BUG-B1 재구현과 함께 재검증 필요). 교차 레이어 로그 9절-4 "Suspended" 개념 미반영도 이 작업의 선행 조건. **[Coder, 2026-08-27]** `Core/StickmanAgent.cs`가 `StickConfig.fullscreenPollInterval` 주기로 `IsFullscreenAppActive()`를 폴링해 감지 시 Suspend, 해제 시 Resume. Suspended 개념은 "상태 인스턴스를 유지한 채 `Machine.Tick()` 호출 자체를 건너뜀"으로 구현(IDLE 리셋 없음, 진행 중이던 상태의 내부 타이머까지 그대로 보존) + `Rigidbody2D.simulated=false`로 물리도 함께 정지 + 렌더러 비활성화. Debugger 지적대로 오버레이 자기오탐 재검증은 BUG-B1 실구현 이후 과제로 남김. |
| 위 항목 버그 리포트 | Debugger | 완료 | 1차 리포트 `docs/BUG_REPORT_PHASE0.md` 작성 완료 — Blocker 1건(BUG-B1), Major 8건(BUG-M1~M8), Minor 8건. Coder로 반려 필요 판정, 수정 우선순위 리포트 상단에 명시. Phase 1 실구현 진행되며 위 각 행에 대응 메모 추가 완료. Phase 1 실구현이 더 진행되면 2차 리포트 예정. |

## Phase 2 — Ragdoll / 파쿠르 / 텍스트-액션 계약
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| Active Ragdoll(RAGDOLL/GETUP) 전환 | Coder | 대기 | |
| PARKOUR_CLIMB (벽타기/매달리기/구르기) | Coder | 대기 | |
| DialogueIntent 텍스트-액션 싱크 계약 | Coder | 대기 | |
| 텍스트-액션 싱크 회귀 테스트 | Test Engineer | 대기 | 기획서 0번 항목 직결 — 최우선 |
| 버그 리포트 | Debugger | 대기 | |

## Phase 3 — 전투 / 커서 상호작용
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| UX: 격파 미니게임 플로우/와이어프레임 | UX Designer | 완료 | UX_FLOW.md 10절 — 기 모으기 게이지/스위트스팟/실패 재도전 3회 정의 |
| UX: 라이벌 스틱맨 조우 연출/탈출 규칙 | UX Designer | 완료 | UX_FLOW.md 11절 — 관전 전용 확정, 스폰확률/쿨다운/최대30초 정의 |
| UX: 드래그&던지기 상호작용 규칙 | UX Designer | 완료 | UX_FLOW.md 12절 — 속도 clamp/스무딩, 부분적 클릭관통 해제 요구사항 포함 |
| UX: 로데오 커서 / 인질극 긴급탈출 상세 | UX Designer | 완료 | UX_FLOW.md 13절(로데오)·14절(인질극 4중 안전망)·15절(부분적 클릭관통 해제 통합), 6-5절과 정합 확인 완료 |
| 격파 미니게임(기 모으기+타이밍) | Coder | 대기 | UX 선행 설계 완료 후 착수 |
| 라이벌 스틱맨 AI | Coder | 대기 | |
| 드래그&던지기(커서 물리 상호작용) | Coder | 대기 | |
| 로데오 커서 | Coder | 대기 | |
| 투사체(화살/농구공) 라이프사이클 테스트 | Test Engineer | 대기 | "화살이 사라지는 버그" 재발 방지 |

## Phase 4 — OS 장난 / PC 연동
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| 창 끌기 시늉(read-only 검증 필수) | Coder | 대기 | |
| 그라피티 낙서 | Coder | 대기 | |
| 블랙홀 / 윈도우 크래시(3초 원복) | Coder | 대기 | |
| CPU/배터리/네트워크 반응 | Coder | 대기 | |
| 실제 파일/창 미변경 감사 테스트 | Test Engineer | 대기 | |

## Phase 5 — 생산성 / 반항·스트레스 / 육성
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| UX: 투두 말풍선 / 포모도로 감시자 플로우 | UX Designer | 완료 | UX_FLOW.md 17절(들고다니기/포스트잇, 포스트잇은 별도 위젯 창 권고)·18절(딴짓감지는 절대치 마우스 움직임 대신 포커스 전환 빈도+극단값 조합, 연속주기 누적 판정, 3단계 에스컬레이션) |
| 투두 말풍선 / 포모도로 감시자 | Coder | 대기 | |
| UX: 스트레스 게이지 / 가출 / 인질극 구분 | UX Designer | 완료 | UX_FLOW.md 19절(게이지 3단 노출·예고신호는 현재형만)·20절(가출 탐색+간식+자동복귀 타임아웃)·24절(가출=2단계 반항 vs 인질극=1단계, 표로 구분 확정) |
| 스트레스 게이지 / 가출 | Coder | 대기 | |
| UX: 던전 파밍 / 세포분열·군대 플로우 | UX Designer | 완료 | UX_FLOW.md 21절(던전 오버레이는 14절과 동일한 클릭관통 역예외, 원본 창 조작 100% 유지 확인)·22절(개체 태그만 제공, 개별지휘 스코프 제외 권고, 개체수 상한+도감 전환) |
| 던전 파밍 / 세포분열 (스코프 논의 필요) | Coder | 대기 | Phase 3 스코프 아웃 후보 재검토 |

## Phase 6 — 마감
| 작업 | 담당 | 상태 | 메모 |
|---|---|---|---|
| 성능 점검(Idle CPU, 할당, 폴링주기) | Performance Engineer | 대기 | |
| 최종 코드 리뷰 | Reviewer(Test Eng 겸임) | 대기 | 개선점 있으면 Architect로 반려 |
| README/기술문서 | Doc Writer(Perf Eng 겸임) | 대기 | |

---

## 교차 레이어 영향 로그 (실시간 공유)
> 한 팀원의 변경이 다른 레이어에 준 영향을 여기에 기록한다.

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
- **[Coder → Debugger/Architect, 2026-08-27]** Debugger 지적사항 반영 완료 보고 (Phase 1 실구현과 함께 처리, Architect 지시대로 B1/M2/M1/M3/M5 우선 반영 — 상세는 각각 Phase 1 표 행의 [Coder] 메모 참고):
  - **(BUG-M3, BUG-M5)** `Platform/FootholdPoller.cs`(주기 폴링+캐시, `StickConfig.footholdPollInterval` 소비)와 `Platform/ScreenCoordinateConverter.cs`(Unity 스크린↔OS 데스크톱 좌표, `StickConfig.desktopDpiScale`) 신규 추가. `States/GroundSensor.cs`가 이 두 유틸만으로 접지/화면경계 판정을 계산하는 단일 창구 — 모든 State(Idle/Walk/Jump/Fall)는 `StickmanBlackboard.SenseGround()`만 호출하고 `IPlatformWindowService`나 좌표 변환식을 직접 만들지 않음(좌표계 혼용 버그 재발 방지).
  - **(BUG-M2)** `StickmanStateMachine.ChangeState()`를 `_states.TryGetValue` 선검증 후 실패 시 뮤테이션(Exit/세대증가) 없이 안전 반환하도록 수정. 기존 호출부 동작(유효한 키)에는 영향 없음.
  - **(BUG-M1)** `StateTransitionContext`(생성자+5개 필드)와 `StickmanStateMachine.CurrentTransitionGeneration`을 `public`→`internal`로 좁힘. `IStickmanState`/`DialogueIntent`/`StickmanEventBus`의 공개 시그니처(Enter/Tick/Exit, DialogueIntent 생성자, 이벤트 목록)는 전혀 건드리지 않음 — Coder 작업 지침(4대 보호 인터페이스 시그니처 변경 금지)을 지키면서 그 "옆"의 캡슐화만 강화. **한계(Debugger 원 지적 그대로 유지)**: 이 프로젝트에 asmdef가 전혀 없어 States/Dialogue 네임스페이스가 전부 같은 기본 어셈블리(Assembly-CSharp)로 컴파일되므로, `internal`은 "다른 어셈블리에서의 위조"만 막고 같은 어셈블리 내부의 임의 코드가 `internal` 생성자를 직접 호출하는 것까지는 막지 못함. 완전한 방어(발급 1회용 토큰 + sealed 클래스)는 Debugger/Coder 합의대로 Phase 2로 유지.
  - **(BUG-B1, Blocker)** Architect 지시("이번 스코프를 넘는다면 최소한 가드")에 따라 진짜 분리 오버레이(CreateWindowEx) 구현은 하지 않고, `Win32WindowService`에 임시 안전가드만 적용: `_usingUnsafeSelfWindowFallback` 플래그가 켜진 동안(현재 `CreateOverlayWindow()`가 항상 켜둠) `SetClickThrough`/`SetAlwaysOnTop`가 `NotSupportedException`을 던져 게임 창 자체 파괴를 차단. `WS_EX_NOACTIVATE` 상수 추가 및 `SetClickThrough`의 스타일 조합에 포함(BUG-B1(c), 가설 H2 완화). `Core/StickmanAgent.cs`가 이 예외를 잡아 로그만 남기고 나머지 초기화를 계속 진행 — 결과적으로 **Windows에서는 클릭관통이 아직 실제로 켜지지 않는다(의도된 안전 실패)**. "클릭 관통 기본 ON" Tasklist 행은 Debugger 지시대로 완료 처리하지 않고 진행중으로 유지. **다음 담당자에게**: 진짜 오버레이 HWND(CreateWindowEx, 가상 데스크톱 전체 크기, WS_EX_LAYERED|TRANSPARENT|TOPMOST|NOACTIVATE 처음부터 적용) 구현이 별도 후속 작업으로 필요.
  - 커서 좌표 조회는 UX_FLOW.md 9절-3 요구사항에 따라 `IPlatformWindowService`를 확장하지 않고 신규 `Platform/ICursorPositionService.cs`로 분리 배선(Win32는 `GetCursorPos`, 에디터는 `NullPlatformWindowService`가 `Input.mousePosition`으로 대체 구현) — 클릭관통 여부와 완전히 독립. 이 설계 판단(기존 인터페이스 확장 대신 신규 인터페이스 신설) 자체에 대해 **Debugger 검토 요청**.
  - **[Coder → Debugger/Architect, 검토 요청]** `StickmanStateMachine` 생성자가 즉시 `ChangeState(initialState)`를 호출해 초기 상태의 `Enter()`를 실행하는데, `Core/StickmanAgent.cs`에서는 이 생성자 호출 시점에 아직 `StickmanBlackboard.Machine`이 할당되지 않은 상태다(생성자 반환 후에야 `_blackboard.Machine = _machine` 대입). 현재 `IdleState.Enter()`는 `blackboard.Machine`을 참조하지 않아 Phase 1에서는 문제없지만, Phase 2 이후 어떤 초기 상태의 `Enter()`가 `Machine`을 참조하게 되면 `NullReferenceException`이 발생한다. `StickmanStateMachine` 생성자 타이밍(즉시 ChangeState 호출) 자체를 바꾸는 건 구조 변경이라 이번 Phase 1에서 임의로 고치지 않고 그대로 남겨둔다 — Phase 2 착수 전 검토 요청.
  - **[Coder, 범위 외 확인]** BUG-M4/M6(모바일 `ScreenshotBackdropPlatformService`의 배경 교체-복원 구분, 텍스처 파괴 누락)은 Architect 지시대로 이번 Phase 1에서 손대지 않음(모바일 영속화/온보딩 붙을 때 처리). `NullPlatformWindowService`/`ScreenshotBackdropPlatformService` 둘 다 `FootholdPoller`(신규)와 인터페이스 호환 확인 완료 — 별도 수정 불필요(모바일은 탭 즉시 자체적으로 `RaiseFootholdsChanged()`를 이미 호출하므로 폴러의 주기 재확인은 무해한 중복).
  - **[Coder, 범위 외 확인]** macOS `IPlatformWindowService` 구현체는 여전히 없음(m8, Null 폴백만 존재) — `Core/StickmanAgent.cs`의 플랫폼 팩토리는 `UNITY_STANDALONE_WIN`/`UNITY_IOS`/`UNITY_ANDROID`만 분기하고 macOS는 Null로 폴백. 기존에 이미 커버리지 공백으로 기록된 사안 그대로 유지, Phase 1 범위 밖.

## 과학적 토론 로그 (원인 불명 버그)
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
