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
| 중력/발판 인식(창 상단 Y 스냅) | Coder | 대기 | |
| 화면 경계 이탈 → 낙하 | Coder | 대기 | |
| IDLE/WALK/JUMP/FALL 상태 구현 | Coder | 대기 | |
| 클릭 관통 기본 ON | Coder | 대기 | |
| 전체화면 게임 감지 → 자동 숨김 | Coder | 대기 | |
| 위 항목 버그 리포트 | Debugger | 대기 | |

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
| 격파 미니게임(기 모으기+타이밍) | Coder | 대기 | |
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
| 투두 말풍선 / 포모도로 감시자 | Coder | 대기 | |
| 스트레스 게이지 / 가출 | Coder | 대기 | |
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

## 과학적 토론 로그 (원인 불명 버그)
> 가설 → 검증방법 → 결과 → 결론 순으로 기록.

- (아직 없음)
