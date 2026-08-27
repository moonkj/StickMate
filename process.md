# StickMate Process Log
> 리더(아키텍트)가 구현 단계마다 갱신 + GitHub 커밋.

## 2026-08-27
- 프로젝트 초기화. 이전 PC 작업물/메모리 없음을 확인 — git 미초기화 상태였음. `git init` 수행.
- 팀 구성 완료: UX Designer, Coder, Debugger, Test Engineer(겸 Reviewer), Performance Engineer(겸 Doc Writer). 리더=Architect 겸임.
- 리서치: 스틱맨 무빙 방식 웹서치 → **Active Ragdoll + IK 하이브리드** 채택 (docs/ARCHITECTURE.md 0절 참조).
- 산출물: docs/ARCHITECTURE.md(설계 요약), Tasklist.md(공유 트래커), .claude/agents/*(팀원 정의).
- tmux 미설치 확인 — Homebrew도 없어 즉시 설치 보류, Claude Code 네이티브 서브에이전트 병렬 실행으로 대체 진행 (사용자 확인 대기 중).
- 다음: Phase 0 스캐폴딩 — UX Designer, Coder 병렬 착수.

## 2026-08-27 (계속) — Phase 0 게이트 승인
- UX Designer, Coder 병렬 완료 (general-purpose 에이전트에 역할 페르소나 주입 방식 — 커스텀 서브에이전트 타입은 다음 세션부터 이름으로 호출 가능).
- 사용자 확정 스코프 추가: macOS/Windows/iPad/iPhone 4개 플랫폼. 모바일은 "스크린샷 백드롭 모드"(홈 화면 스크린샷을 정적 배경으로 사용) 채택 → docs/ARCHITECTURE.md 0-1절, CLAUDE.md 반영.
- Coder 산출물(18개 스크립트) 리더 직접 검토: SetWindowPos 등 위험 API는 자기 소유 오버레이 창의 Z-order 변경에만 사용됨을 확인, 타 윈도우/파일 변경 없음. DialogueIntent의 TransitionGeneration 기반 자동 만료 설계가 "한 발 더 하고 안 쏨" 버그를 구조적으로 차단함을 확인.
- **Phase 0 게이트 승인.** 알려진 이월 항목(Phase 1에서 처리): 커서 좌표 폴링 독립화, Suspended 상태, 멀티모니터 경계 노출, EnumerateFootholds 폴링 규율, ScreenshotBackdrop 재할당 시 불필요한 재온보딩 방지.
- 다음: Debugger에게 Phase 0 코드 리뷰 위임 (Coder 자체 지목 위험 4건 + UX 계약 준수 여부 중점).

## 2026-08-27 (계속) — Unity 실환경 구축 + Phase 1 완료
- 사용자 요청으로 Unity Hub + Unity 6 LTS(6000.0.82f1, Apple Silicon) + Mac/Windows/iOS 빌드 지원 모듈을 이 머신에 직접 설치 (Rosetta 2 선행 설치 필요했음).
- 이제까지 스크립트만 있던 상태에서 실제 열 수 있는 Unity 프로젝트로 골격 보강: `ProjectSettings/`, `Packages/manifest.json` 추가, Unity 표준 `.gitignore` 추가(Library/Logs/UserSettings 등 생성물 제외).
- **실제 컴파일 검증 수행**: Unity 배치 모드로 프로젝트를 열어 Phase 0+1 전체 스크립트를 임포트/컴파일 → 에러 0건, 경고 2건(Phase 2에서 구현할 Ragdoll/Getup 스텁의 미사용 필드, 예상된 것). 지금까지는 코드 리뷰(정적 검토)로만 검증했는데 이제 컴파일러 레벨 검증까지 확보.
- Debugger가 Phase 0 리뷰에서 Blocker 1건(Win32 오버레이=게임 창 재사용 시 클릭관통이 앱 자체를 파괴) + Major 8건 발견 → 마침 Phase 1을 작업 중이던 Coder에게 실시간 전달, 완료 전 반영시킴(교차 레이어 실시간 조정).
- Coder Phase 1 완료: FootholdPoller/ScreenCoordinateConverter/ICursorPositionService 신설, StickmanAgent(코어 루프 진입점) 작성, BUG-B1은 임시 안전가드(NotSupportedException)로 자기파괴 방지, BUG-M1/M2/M3/M5 전부 반영.
- UX Designer는 병렬로 Phase 3/4(전투/커서/인질극) 및 Phase 5(생산성/스트레스/육성) 선행 설계까지 완료. 특히 "부분적 클릭관통 해제"는 단순 bool이 아니라 히트박스+지속시간+단일소유자락 구조가 필요함을 명확히 함 — 다음 Coder 작업에 반드시 반영 필요.
- **Phase 1 게이트 승인.** 알려진 이월 위험: blackboard.Machine이 Enter() 시점에 null(Phase 1 범위 내 무해, Phase 2 착수 시 반드시 재확인), macOS 네이티브 구현 공백, 실 오버레이 HWND 미구현으로 클릭관통 기능 자체는 아직 비활성.
- 다음: Debugger에게 Phase 1 코드 리뷰 위임.

## 2026-08-27 (계속) — Phase 1 2차 반려 수정 완료
- Debugger 2차 리뷰에서 Blocker 2건 발견: (1) 발판 0개 시 무한낙하 안전망 부재, (2) **이동이 키보드 입력에만 의존 — 기획 원문에 키보드 조작이 아예 없고, 실 오버레이(WS_EX_NOACTIVATE) 완성 시 구조적으로 영구 고장나는 근본 설계 결함.**
- Architect 결정: 키보드 입력 완전 폐기, 자율 배회 AI로 교체. UX Designer에게 배회 행동 스펙(수치/확률/인터페이스 계약)을 긴급 설계시키고, 그 결과를 실시간으로 Coder에게 중계(SendMessage) — Coder가 임시 구현 단계 없이 최종 스펙 그대로 `AutoWanderController` 정식 구현.
- BUG-P1-B1은 `FallbackPlatformWindowService` 데코레이터로 해결 — 발판 0개 시 화면 하단 합성 발판으로 대체, 단 모바일 온보딩 게이트(IsConfigured)는 의도적으로 우회하지 않도록 보존.
- Major 5건(M2 상태머신 생성자 타이밍/M3 반환값 무시/M4 캐시 불변성/M5 코요테 타임 명문화/M6 다중 Rigidbody Suspend) 전부 반영. 컴파일 재검증: 에러 0, 신규 경고 0.
- UX Designer는 병렬로 Phase 4(OS 장난: 창도둑/청소부/그라피티/크래시/블랙홀/PC연동) 선행 설계도 완료 — "윈도우 크래시" 트리거로 기획 원문의 키보드 타건속도 감지를 키보드 폐기 결정과의 일관성을 이유로 스스로 배제한 점이 특히 좋았음.
- **Phase 1 2차 게이트 승인.** 다음: Debugger 3차(타겟) 검토 후 문제 없으면 Phase 2(Ragdoll/파쿠르/DialogueIntent) 착수.

## 2026-08-27 (계속) — Phase 1 3차 검토 + Architect 핫픽스
- Debugger 3차(타겟) 검토: BUG-P1-B2(키보드의존)/BUG-P1-M2(생성자타이밍)/AutoWanderController 스펙 일치 전부 확인 완료. 컴파일 에러 0.
- 신규 Blocker(BUG-P1-R3-B1) 발견: 서로 떨어진 두 발판 사이 빈틈으로 배회 AI가 점프 시도 시 착지 실패 → 무한낙하 재발(FallbackPlatformWindowService가 "실제 발판이 1개라도 있으면" 개입하지 않는 구조였기 때문).
- 범위가 작고 명확해 리더가 직접 핫픽스 적용(전체 Coder 라운드 생략): EnumerateFootholds()가 항상 화면 하단 안전망을 목록 끝에 추가하도록 변경. 조사 중 원래 구현의 좌표계 버그(안전망이 화면 "맨 위"에 배치되던 것 — y=0이 OS 좌표계에서 화면 상단을 뜻함)도 함께 발견해 수정.
- Unity 배치모드 컴파일 재검증: 에러 0, 신규 경고 0(기존 Ragdoll/Getup 스텁 경고 2건만 유지).
- 다음: Debugger 최종 확인(4차, 이번 핫픽스만 타겟) 후 문제 없으면 Phase 1 최종 승인 및 Phase 2(Ragdoll/파쿠르/DialogueIntent) 착수.

## 2026-08-27 (계속) — Phase 1 최종 승인 + Phase 2 구현 완료
- Debugger 4차(타겟, 리더 자체 핫픽스 편향 검증 포함) 최종 승인: Library 캐시 삭제 후 클린 재컴파일까지 독립 재확인, 에러 0/회귀 0. **Phase 1 공식 종료.**
- Phase 2(Active Ragdoll, ParkourClimb, DialogueIntent 강화) — Coder/UX Designer 병렬 진행:
  - Active Ragdoll: `RagdollRig`(런타임 파츠/관절 탐색) + `ReportExternalImpact()` 단일 진입점(충격량 기반, 어떤 능동 상태든 강제 인터럽트) + GETUP 비례제어 기상. Phase1 선반영된 상태머신 생성자 분리/다중Rigidbody Suspend 덕에 무리없이 확장.
  - ParkourClimb: 자율 배회 AI의 경계 점프 시도를 자연 확장(벽 감지 시 Climb, 없으면 기존 Jump).
  - DialogueIntent: `StateTransitionContext` 구조체→봉인 클래스+1회용 토큰 전환(BUG-M1 완결, 컴파일 타임 위조 차단) + `IHasDialogueParams` 파라미터 파이프라인(BUG-P1-M7 해결) — UX 31절 대사 매핑표 그대로 Attack/Ragdoll/ParkourClimb에 실전 연결.
  - 컴파일: 에러 0, 경고 0(기존 미사용 필드 2건 자연 해소).
- **Architect 판단 필요 사항 해결**: 낙하높이기반 구르기 훅 vs 충격량기반 Ragdoll 진입, 두 축 통합 여부 — **통합하지 않기로 결정**. 기획 원문 1-4절("항상 부드럽게 착지")에 따라 낙하는 높이 불문 항상 우아한 연출, Ragdoll은 오직 실제 피격/충돌 전용으로 역할을 명확히 분리 확정.
- 다음: Debugger에게 Phase 2 검토 위임.

## 2026-08-27 (계속) — Phase 2 Major 핫픽스
- Debugger 검토: Blocker 0, Major 1(BUG-P2-M1: ParkourClimb 등반 중 linearVelocity.y 미재확정 → 등반 완료 직후 착지 튐), Minor 5.
- 나머지 전부 통과: 토큰화 우회경로 없음, ReportExternalImpact 가드/리셋 보장, RagdollRig 파츠0개 안전, 파쿠르 좌표계/재확인/배회AI 경합 없음, 낙하높이·충격량 축 분리 결정과 구현 정확히 일치, Ragdoll/ParkourClimb 대사 매핑 UX 31절과 일치.
- 범위 작아(2줄) 리더가 직접 핫픽스: ParkourClimbState.Tick()에서 매 프레임 linearVelocity.y도 0으로 재확정(SnapToGround의 기존 관행과 동일하게). 컴파일 재검증: 에러 0/경고 0.
- 다음: Debugger 짧은 재확인 후 문제 없으면 Phase 2 최종 승인, Phase 3(전투/커서상호작용) 착수.

## 2026-08-27 (계속) — Phase 3 구현 + 텍스트-액션 회귀 테스트 인프라
- Coder: Phase 3(전투/커서상호작용) 5개 기능 전부 구현 — 부분적 클릭관통 해제(인프라, 단 진짜 OS 히트테스트는 BUG-B1 미해결로 소유권 부기까지만), 격파 미니게임, 라이벌 스틱맨 AI(관전전용), 드래그&던지기, 로데오 커서. AttackState도 이번에 완성(예전엔 Tick() 비어있어 영원히 안 빠져나오는 상태였음). 공통 인프라 SpectacleEventLock(4개 스펙터클 상호배제)/RagdollImpactResolver(중복 제거) 신설.
- Test Engineer(병렬): 텍스트-액션 싱크 EditMode 회귀 테스트 8건 작성, 실제 -runTests로 8/8 통과 확인(2회, 클린 재컴파일 포함). 프로덕션 코드를 이름 있는 어셈블리(StickMate.Runtime.asmdef)로 승격 + 테스트 asmdef + InternalsVisibleTo 구성.
- Architect 결정: 격파 미니게임 "릴리즈 순간" 대사(Enter() 밖에서 확정되는 문제) — 예외를 두지 않고 RagdollState의 기존 자기-전이(self-transition) 패턴을 재사용하도록 지시(Tasklist 교차 로그 기록). 다음 라운드에서 Coder가 반영.
- 리더 독립 컴파일 재검증: 에러 0/경고 0.
- 다음: Debugger에게 Phase 3 전체 검토 위임(범위가 커서 꼼꼼히).

## 2026-08-27 (계속) — Phase 3 반려 수정 완료
- Debugger 검토: Blocker 0, Major 1(BUG-P3-M1: 4개 Interaction Director가 OnDisable/OnDestroy 시 SpectacleEventLock/클릭캡처락 미해제 — 영구 잠금 위험), Minor 2(AttackState.ShotsRemaining 죽은코드, 라이벌 대결 비대칭).
- Coder 수정: 4개 Director에 OnDisable 락 반환 로직(멱등) 추가. 격파 미니게임 릴리즈 대사를 Architect 지시대로 self-transition 패턴으로 전환(RagdollState와 동일 파이프라인) — 과정에서 self-transition이 Director를 "이탈"로 오판시킬 뻔한 부작용을 스스로 발견해 가드 추가. Minor 2건도 함께 해소.
- 검증: 클린 재컴파일 에러0/경고0, EditMode 회귀테스트 8/8 통과.
- 다음: Debugger 최종 확인 후 Phase 3 승인, Phase 4(OS 장난/PC연동, UX 설계는 이미 완료됨) 착수.

## 2026-08-28 — Phase 4 구현 완료 (OS 장난/PC연동)
- Coder: 6개 기능(창도둑/청소부/그라피티/크래시/블랙홀/PC하드웨어반응) 전부 구현. 청소부·블랙홀은 공용 DesktopIconMirrorDirector로 통합(UX 28절-25 권고 반영). 윈도우 크래시는 100% 클릭관통 구조적 보장(ILocalClickCaptureService 미참조). Win32 실제 아이콘 좌표 조회는 Windows 실기기 부재로 정직한 미구현 스텁으로 남김(no-op).
- Test Engineer(병렬, Coder보다 먼저 완료): 유저 자산 불변 정적 감사 테스트 5건 — 금지 API 소스 스캔, 가짜 위반 주입으로 실제 탐지력 검증 후 제거. 위반사항 없음.
- Architect 승인 2건: 윈도우크래시 스윙/크랙수명 분리, 하드웨어반응 SpectacleEventLock 미적용(지속적 배경무드라 일회성 스펙터클과 별개 판단).
- 리더 독립 컴파일 재검증: 에러0/경고0.
- 다음: Debugger에게 Phase 4 검토 위임.

## 2026-08-28 (계속) — Phase 4 Major 핫픽스
- Debugger 검토: Blocker 0, Major 1(BUG-P4-M1: HardwareReactionDirector의 배터리/충전/네트워크 회복 쿨다운이 매 프레임 dt만 소진해 실제로는 배터리 26일/충전 8.7일/네트워크 5.8일 걸리는 은닉 버그 — CPU만 올바른 패턴 사용), Minor 2(우선순위 선점 정책 확인 요청, CPU 프레임타임 오탐 잠재위험).
- 나머지 5개 중점 점검(유저자산불변 정적스캔 커버리지, 청소부/블랙홀 락 상호배제, 크래시 100%클릭관통, self-transition 함정 재발 여부, IDesktopIconLayoutService 안전 스텁) 전부 통과.
- 범위 작아 리더가 직접 핫픽스: TickBattery/TickCharging/TickNetwork가 매 프레임 dt 대신 실제 경과 폴 간격을 UpdateSignalLifecycle에 전달하도록 수정(TickCpu의 기존 올바른 패턴과 통일).
- Minor 확인: 우선순위 선점 안 함(이미 표시중인 반응 유지) 정책 승인 — 급전환 방지가 맞는 설계.
- 컴파일 재검증: 에러0/경고0.
- 다음: Debugger 짧은 재확인 후 Phase 5(생산성/반항·스트레스/육성) 착수.

## 2026-08-28 (계속) — Phase 5 구현 완료 (생산성/반항·스트레스)
- Coder: 투두 말풍선(들고다니기+포스트잇 독립 uGUI, 부분적클릭관통해제 인프라 미의존), 포모도로 감시자(FootholdsChanged 재사용 딴짓감지, 3단 에스컬레이션), 스트레스 게이지(이벤트 훅만, UI는 다음 라운드), 가출(5페이즈 self-transition, Kinematic 채택 근거 명확, 자동복귀 1.5h 타임아웃, 긴급정지=강제소환 라벨 분기).
- 던전파밍/세포분열은 P3 보류 그대로 유지(건드리지 않음, 리더 결정 존중 확인).
- SpectacleEventLock 참여 기준을 "ChangeState로 단일 상태 슬롯을 다투는가"로 통일 — 합리적 판단으로 승인.
- 리더 독립 컴파일 재검증: 에러0/경고0.
- 다음: Debugger에게 Phase 5 검토 위임.

## 2026-08-28 (계속) — Phase 5 반려 수정 완료
- Debugger 검토: Blocker 0, Major 2(BUG-P5-M1: Resume()이 가출 은신 중 렌더러 강제복원 / BUG-P5-M2: UX24절 로데오-스트레스 가중치 미구현인데 완료로 오보고), Minor 2(과다사용 반복가산 의도확인, 포모도로 무관긴급정지 취소).
- Coder 수정: IsCharacterHiddenByRunaway 플래그로 Resume()과 RunawayState 가시성 제어 조율(침습 적은 방식 선택). RodeoCursorWatcher에 스트레스 가중치 연동(정지판정시간 완만히 단축, 과하지 않게). Minor1은 의도된 에스컬레이션으로 확정 기록. Minor2는 SpectacleEventLock 소유자 확인 가드로 "진짜 무관한 긴급정지만" 무시하도록 정교화(포모도로 자체 탈출구 기능은 보존).
- 컴파일 재검증: 에러0/경고0, EditMode 13/13 통과.
- **Phase 5 사실상 마무리. 다음: Debugger 최종 확인 후 Phase 6(성능점검/최종리뷰/문서화)로 프로젝트 마감 단계 진입.**

## 2026-08-28 (계속) — Phase 6: 성능 점검 통과 + 리뷰 개선 R2
- Performance Engineer: 24시간 상주 기준 전체 감사 — 실질적 문제 0건. 폴링 규율/캐싱/할당금지 컨벤션이 5라운드 내내 실제로 지켜졌음을 확인. Rigidbody2D 설정이 FixedUpdate 아닌 Update 경로인 점만 향후 물리 타이밍 참고사항으로 기록(성능 문제 아님, 범위 밖).
- Reviewer(품질 리뷰): 좋은 점 4개(주석 컨벤션 일관성, StickmanAgent 저비만화 유지, 플랫폼 확장지점 유효성, RivalStickmanAgent로 다중인스턴스 패턴 실증) 확인. 개선 요청 1건 — SpectacleEventLock 해제 보일러플레이트가 12개 Director에 반복(DRY 위반, 정책 변경 시 12곳 수동 동기화 필요, 락 미해제 회귀 재발 위험).
- **개선 R2**: Coder가 `SpectacleEventLock.ReleaseIfOwned()` 공용 헬퍼 신설, 10곳 교체(2곳은 구조적으로 안 맞아 근거와 함께 예외 유지 — RivalEncounterDirector/FocusWatchDirector). 리더 독립 컴파일 재검증: 에러0/경고0.
- 다음: Reviewer에게 R2 재확인 요청, 승인되면 Doc Writer에게 최종 문서화(README) 위임하고 프로젝트 마감.

## 2026-08-28 (계속) — 개선 R2 재확인 승인
- Reviewer가 R2(락 해제 공용 헬퍼 추출)를 재확인: 헬퍼 설계/10곳 교체 diff/2곳 예외 근거/컴파일·테스트 기준선 전부 독립 재검증 완료. **개선할 부분 없음 — 최종 완료.**
- 사용자 지정 리뷰 프로세스(개선사이클→R2→...→개선없으면 종료) 완료.
- 다음: Doc Writer에게 README/기술문서 최종 정리 위임. 완료되면 프로젝트 1차 마감.

## 2026-08-28 (계속) — README 작성 완료 + 렌더파이프라인 결정 확정 + 프로젝트 1차 마감
- Doc Writer: README.md 신규 작성(113줄) — 소개/컨셉/지원플랫폼/기술스택/폴더구조/빌드방법/구현현황/절대원칙/테스트/알려진한계/더읽을거리. Packages/manifest.json 직접 확인해 실제로는 Built-in RP가 적용되어 있음을 발견(설계 문서는 URP 2D 전제).
- **Architect 결정**: Built-in RP를 공식 기준으로 확정(URP 특화 코드 없음, 불필요한 의존성 회피, 추후 전환 비용 낮음). ARCHITECTURE.md/README.md 갱신.

## 프로젝트 1차 개발 사이클 완료 요약
Phase 0(스캐폴딩) → 1(코어루프) → 2(랙돌/파쿠르) → 3(전투/커서상호작용) → 4(OS장난/PC연동) → 5(생산성/스트레스) → 6(성능점검/최종리뷰 개선R2/문서화) 전 과정을 UX Designer → Architect → Coder → Debugger(필요시 재순환) → Reviewer(개선사이클) → Doc Writer 팀 프로세스로 완주.
- 총 커밋 19개, EditMode 회귀테스트 13건(텍스트-액션싱크 8 + 유저자산불변감사 5) 전부 통과 유지.
- Blocker 다수 발견·해소(발판0개 무한낙하, 키보드의존이동 근본결함, 게임창 자기파괴 위험 등), Major 다수 발견·해소, 개선사이클 1회(R2, DRY위반 해소) 완주.
- 남은 것: 씬/프리팹 배선(캐릭터 리그/UI), macOS 네이티브, 진짜 분리 오버레이(BUG-B1), 던전파밍/세포분열(P3 보류) — README "알려진 한계"에 명시.

## 2026-08-28 (계속) — 씬/프리팹 배선 완료 (README "다음 단계" 첫 항목 착수)
- Coder: DefaultStickConfig.asset, Stickman.prefab(플레이스홀더 스프라이트 리그 — 루트 Rigidbody2D+StickmanAgent, 팔다리 4개는 Rigidbody2D+HingeJoint2D로 RagdollRig 계약 충족, 충돌체는 의도적으로 제외해 상시물리 떨림 방지), Main.unity(직교카메라+인스턴스), SceneBootstrapper.cs(재생성용 에디터 빌더) 생성.
- **실측 플레이테스트**(PlayMode 배치 실행, 15초 관찰): Y좌표 즉시 정착 후 변동폭 0.0015유닛(무한낙하 없음), X좌표 0→18.64유닛 실제 이동(자율배회 AI 실동작 확인), 에러/예외 0건.
- 실측 중 발견해 수정한 이슈 2건(전부 데이터 튜닝, 로직 무수정): (1) groundSnapTolerance가 너무 좁아 저프레임레이트에서 접지판정 터널링 → 6px→20px. (2) 카메라 시야가 좁아 유일한 더미발판 가장자리에 도달해 정상적으로 Fall(설계대로 동작, 버그 아님) → orthographicSize 5→20로 확장해 데모 관찰성 개선.
- PlayMode 정식 회귀 테스트 1건 신설(StickMate.Tests.PlayMode), 향후 -runTests -testPlatform PlayMode로 재실행 가능.
- 리더 독립 컴파일 재검증: 에러0/경고0.
- 다음: Debugger에게 씬/프리팹/구성 검토 위임(코드 로직이 아닌 에셋 배선 관점).

## 2026-08-28 (계속) — 씬/프리팹 반려 수정 완료 + 스모크 테스트 디플레이킹
- Coder: BUG-SW-M1(랙돌 무한낙하) — 표준 랙돌 레이어 기법(StickmanLimb 레이어, 자체충돌 차단) + 바닥 콜라이더 + RagdollLimbImpactRelay 부착으로 해결. 실측: 강제 RAGDOLL 진입 → 0.5~1.0초 내 Getup → Walk 복귀, 2회 독립 확인.
- BUG-SW-M2(px/world-unit 괴리) — orthographicSize를 원복(20→5)하고, 관측 문제는 카메라가 아니라 더미발판 폭 확장으로 분리 해결(스케일에 영향 없음). 7개 px필드 중 실제 영향받는 건 2개뿐임을 추적 확인.
- BUG-SW-M3(빌더 비멱등) — 기존 에셋 존재 시 기본적으로 스킵, 강제 재생성은 별도 메뉴/플래그로 분리. md5 비교로 멱등성 실측 확인.
- Coder가 부수적으로 발견한 기존 스모크테스트 플레이키니스(AutoWanderController RNG로 인한 우연한 제자리점프가 "7초 전체 구간 Y변동" 판정과 충돌) — **리더가 직접 진단/수정**: "Y 변동폭" 판정을 "종료 시점 상태머신이 Idle/Walk(접지)인가"로 교체(더 정확한 의도 표현). PlayMode 테스트 3회 연속(각기 다른 RNG 경로) 전부 통과로 디플레이킹 검증 완료.
- 컴파일: 에러0/경고0. EditMode 13/13, PlayMode 2/2(3회 재현).
- **StickMate 코드+씬 배선 1차 완성.** 유저가 Unity Hub에서 Main.unity를 열고 Play를 누르면 실제로 캐릭터가 자율 배회하는 모습을 볼 수 있는 상태.

## 2026-08-28 (계속) — macOS 네이티브 창 열거 + 랙돌 감쇠 버그 수정 + 에디터 가드 대칭화
- Coder(macOS): `MacWindowService.cs` — 네이티브 .bundle 없이 CoreGraphics/CoreFoundation C ABI 직접 P/Invoke로 창 열거(Phase0 m8 해소). 실측: 이 세션의 실제 창(터미널/노트 등) 정확히 열거, 시스템 레이어(Dock/메뉴바 등) 정확히 제외, 한글 창 제목도 정확히 디코딩 확인.
- **중요 발견**: 활성 빌드 타깃이 macOS면 에디터 컴파일 컨텍스트에도 UNITY_STANDALONE_OSX가 함께 정의됨 실측 확인 → `!UNITY_EDITOR` 가드 필수. **Architect가 대칭 보강**: Windows 분기(`UNITY_STANDALONE_WIN`)에도 동일한 잠재 위험이 있음을 인지해 동일 가드 추가(지금까지 활성 타깃이 계속 macOS여서 드러나지 않았을 뿐, 향후 Windows 개발자가 프로젝트를 열면 에디터 실측이 조용히 깨질 뻔한 위험 사전 차단).
- Coder(랙돌 감쇠, 병렬): BUG-SW-M4(Debugger가 8회 반복 검증 중 발견한 "이동 중 피격 시 25% 확률로 GETUP 영구 실패") — 팔다리 Rigidbody2D에 linearDamping/angularDamping 추가 + EnterRagdoll() 진입 시 각속도 절반 감쇠. **15회 반복 재검증 100% 통과**(이동 중 피격 4/4 포함, 이전엔 전멸하던 케이스).
- README.md 최신화: 씬/프리팹 배선 완료 반영(더 이상 "빈 씬" 아님), PlayMode 테스트 2종 추가, macOS/Windows 오버레이 한계 통합 정리.
- 리더 독립 컴파일 재검증(통합 상태): 에러0/경고0.
- 다음: Debugger에게 이번 라운드(macOS 열거 + 랙돌 감쇠 + 에디터 가드) 통합 검토 위임.

## 2026-08-28 (계속) — 통합 최종 승인
- Debugger 독립 재검증(20회 PlayMode 반복, Walk 피격 2/2 포함): BUG-SW-M4 완전 해소 확인. macOS 마샬링(Boolean 3곳 MarshalAs 확인)/필터/자기제외/실제 창 열거 재현 전부 정상. 에디터 가드 대칭화도 활성빌드타깃(OSXUniversal) 기준 정상 작동(에디터는 계속 NullPlatformWindowService 사용) 확인.
- **전체 승인 — 씬배선+macOS+랙돌감쇠 라운드 최종 완료.**

## 2026-08-28 (계속) — 카메라 프레이밍 버그 수정 (사용자 실측 발견)
- 사용자가 Unity 에디터를 GUI로 직접 보다가 "화면 상단에서 뭔가 왔다갔다하고 안 보임" 발견 — 실제 육안 검증이 자동화 테스트로 못 잡은 버그를 잡아낸 사례.
- 근본원인: NullPlatformWindowService의 더미 발판이 "작업표시줄"이라면서 실제로는 OS좌표계 Y=0(화면 맨 위)에 배치되어 있었음(FallbackPlatformWindowService에서 이미 고쳤던 것과 동일 계열 버그, 이 클래스는 그때 안 건드려서 남아있었음).
- Coder 수정: 더미 발판을 화면 진짜 하단으로 이동 + 해상도 비율 기반 배치로 안정화 + SceneBootstrapper의 지면 계산을 단일 헬퍼로 통일(중복 계산 어긋남 방지). 신규 화면가시성 PlayMode 테스트 추가(캐릭터가 실제로 뷰포트 안에 보이는지 검증 — 이번 버그를 처음부터 잡았을 테스트).
- 실측: 발/머리 스크린좌표 5회 반복 전부 여백 안 충분(24~456px 여유). PlayMode 15/15 통과.
- 다음: Debugger 검토(병행), 그리고 **진짜 바탕화면 오버레이(macOS 네이티브 클릭관통/투명창) 착수** — 사용자 요청으로 Standalone 빌드 + Objective-C 네이티브 플러그인 구현 시작.

## 2026-08-28 (계속) — 진짜 바탕화면 오버레이 구현 (사용자 요청, 최대 규모 작업)
- Coder: `StickMateOverlayPlugin.m`(Objective-C 네이티브 플러그인, clang 직접 컴파일 .bundle) — 우리 자신의 NSWindow 하나만 조작(다른 프로세스 창 접근 자체 불가능한 API로 원천 차단). SetClickThrough/SetAlwaysOnTop/CreateOverlayWindow를 실제 구현으로 교체(그동안의 안전가드 해제).
- 안전장치: 앱 시작 5초간 클릭관통 지연 + Escape 키 긴급 해제(단, 포커스 잃으면 무효 — 정직하게 문서화, 최종 안전망은 터미널 프로세스 종료).
- BuildStandalone.cs로 실제 macOS Standalone 빌드(.app, 유니버설 arm64+x86_64) 생성 및 실행 — **PID 49739, 현재 사용자 실제 데스크톱에서 구동 중**.
- 이중 검증: Player.log 내부 로그(windowLevel=3 NSFloatingWindowLevel) + 외부 독립 프로세스의 CGWindowListCopyWindowInfo 재조회(kCGWindowLayer=3) 완전 일치 확인.
- 리더 직접 코드 검토: 네이티브 플러그인이 NSApplication.windows(자기 프로세스 스코프)만 순회 — 타 프로세스 창 접근 경로 자체가 없음을 확인, 절대 원칙(유저 자산 불변) 준수 확인.
- **정직한 한계**: 완전 투명 렌더링은 100% 미보장(Unity 렌더서페이스 기본 불투명 가정), 클릭관통 체감 자체는 Accessibility 권한 없이 프로그래밍 검증 불가 — 사용자 육안 확인 필요.
- Builds/ 디렉토리는 이미 Phase 0부터 .gitignore에 있어 빌드 산출물(102MB) 커밋 안전하게 제외됨.
