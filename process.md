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

## 2026-08-28 (계속) — 고전 졸라맨(가는 선) 스타일 캐릭터 재작업
- 사용자가 실행 중인 앱을 보고 "이상하게 나온다" + "기존 졸라맨처럼 만들어달라" 피드백 → 스타일 확인 질문 후 "고전적 졸라맨 느낌(가는 선만으로)" 확정.
- Coder: 채워진 사각형/원 스프라이트를 전부 LineRenderer 기반으로 교체(속이 빈 원 머리 24점, 두께0.05 둥근캡 선 몸통/팔다리, 손발 끝 짧은 선 보너스). 물리 구조(Rigidbody2D/Collider2D/HingeJoint2D)는 전혀 무수정 확인.
- 오버레이 창 "이상함" 후보 원인 재검토: NSWindowStyleMaskFullSizeContentView가 신호등 버튼 경계를 부자연스럽게 만들 수 있다고 판단해 제거, titlebarAppearsTransparent+titleVisibility만 유지(더 보수적인 조정).
- 재빌드+재실행: 이전 PID 49739 종료 후 **새 PID 57301**로 재실행 중.
- 컴파일 에러0/경고0, EditMode 13/13, PlayMode 4회 반복 전부 통과(화면가시성 테스트도 Renderer[] 일반화로 LineRenderer 커버 확인).

## 2026-08-28 (계속) — 실제 데스크톱 낙하고착/랙돌폭주 버그 수정 + 손발 표현 개선
- 사용자가 실제 macOS 데스크톱에서 실행 중인 앱 스크린샷 3장을 연속 전송 — 검은 배경(안 보임), 팔다리 뒤엉킴(랙돌 폭주), 쓰러진 채 안 일어남(랙돌 고착) 순으로 문제 확인.
- 사용자 요청으로 리더가 "졸라맨" 원조를 웹 조사(위키백과 등) — 손발은 짧은 직각선이 아니라 작은 점/원으로 표현하는 게 정석임을 확인, Coder에게 전달.
- **실측으로 발견한 진짜 원인 2건**(Blocker급, 이 프로젝트가 지금까지 배치모드 640x480에서만 테스트해서 못 잡았던 실제 데스크톱 전용 버그):
  1. FallbackPlatformWindowService의 안전망 발판이 고정 40px 높이라 씬이 가정하는 지면 Y(화면하단 20%)와 실제 해상도에서 어긋나 낙하 고착.
  2. 안전망 발판 폭이 에디터 더미발판 대비 4배 좁아 자율배회 AI가 정상 배회만으로 가장자리 이탈 반복 → Fall 재발 → 축적된 낙하속도로 충돌 시 랙돌 폭주.
  (참고: 리더의 Retina DPI 가설은 Coder 실측 조사 결과 기각 — PlayerSettings.macRetinaSupport는 Screen.width/height에 영향 없음 확인. 대신 backingScaleFactor 자동감지로 desktopDpiScale 실행시 설정하는 것은 별도로 구현됨, 이 환경 0.500 정상 검출.)
- 손발 표현을 짧은 직각선에서 작은 채워진 점(반지름0.04)으로 교체(졸라맨 레퍼런스 반영).
- 배경은 밝은 회색(#F0F0F0 근처) RGB + 알파0으로 설정(투명 성공시 안 보이고 실패해도 검정-on-검정 회피) — 완전 투명은 여전히 미해결, 명시적으로 다음 라운드 이월.
- 실측 검증: 117초+ 연속 실행 낙하고착 0건, 속도폭주 없음. EditMode 13/13, PlayMode 3/3, 컴파일 에러0/경고0.
- 새 프로세스 PID 60646 실행 중.

## 2026-08-28 (계속) — 투명 비활성화 + 보행 애니메이션(관절각도 제한 포함) 수정
- 사용자가 "완전히 까맣게만 보임" → 리더 판단: 알파0 유지 중 투명 컴포지팅 실패 시 RGB 무관하게 검정으로 렌더링되는 것으로 추정, 투명 시도를 이번엔 비활성화(transparent=0)하기로 결정.
- 사용자가 "보여도 제대로 안움직임" → 리더 판단: Walk 상태에 다리 움직임 애니메이션 자체가 없어 통짜 슬라이딩으로 보였을 것 → WalkCycleAnimator.cs 신설(다리 HingeJoint2D 사인파 모터 구동).
- 1차 결과: 사용자가 "관절이 다 부러짐, 부드럽게 움직여야하는데" 스크린샷 제보 → 실시간으로 각도 제한(JointAngleLimits2D) 누락이 원인이라고 진단해 전달.
- 2차 수정: EnterWalking()이 useLimits=true+각도제한을 걸고 StopWalking()/RAGDOLL 전이 시 원복(useLimits=false, 기존 RAGDOLL/GETUP 자유회전 무영향 확인).
- 실측: transparent=0 확인(로그), 다리 관절각도가 -2.5~26.7도 범위 내로 안정적(폭주 없음), 프로세스 116초+ 무에러 실행 확인.
- **사용자 요청으로 오픈소스 리서치 진행**: UniWindowController(kirurobo, 검증된 투명창 라이브러리)/UnityDesktopPetFramework 발견 — 단 URP 파이프라인 전제. 사용자가 URP 전환 + UniWindowController 채택 확정. **다음 라운드: URP 마이그레이션 + UniWindowController 통합.**
- 새 프로세스 PID 66380 실행 중.

## 2026-08-28 (계속) — 캐릭터 직립/보행 근본 재구현 (사용자 실기기 피드백 6라운드 누적 반영)
- **근본 원인(리더 직접 진단)**: 프리팹의 5개 Rigidbody2D가 전부 `m_Constraints: 0`(회전 미고정) + 팔다리 전부 Dynamic이라, "물리 랙돌이 스스로 서 있기를 기대하는" 구조였음. 관절 모터로 중력과 싸우는 건 이길 수 없는 싸움 → 매번 쓰러짐.
- **해결**: 능동 상태에서는 물리를 쓰지 않고 팔다리를 Kinematic + `transform.localRotation` 절차 애니메이션으로 직접 제어. RAGDOLL에서만 Dynamic 전환. 루트는 능동 상태 동안 FreezeRotation.
  - `RagdollRig`가 물리 모드 전환의 단독 소유자(`EnterActiveMode`/`EnsureRagdollMode`), 호출은 `StickmanBlackboard.TickPose()` 한 곳으로 집약(상태 14개+강제 인터럽트 경로를 멱등 재적용으로 전부 커버).
- **회전 피벗 버그**(리더 진단): 팔다리 transform 원점이 마디 중앙이라 `localRotation` 회전이 관절이 아닌 중앙을 축으로 돌아 팔다리가 몸에서 떨어져 보였음 → 원점을 부착점으로 이동.
- **시각 스타일 확정**(사용자 레퍼런스 이미지 제공): 굵은 획(0.11)+둥근 캡(8), 흰 얼굴+검은 테두리+검은 눈, 팔다리 2분절(무릎/팔꿈치, 한방향 굽힘), 손발 끝점 제거.
- **문워크 해결**: 이동 방향 부호에 따라 포즈 각도 전체 반전(리더는 scale.x 반전을 지시했으나 Coder가 음수 스케일의 콜라이더 위험을 회피하는 각도 반전 방식을 택함 — 시각적으로 동일, 타당한 판단으로 승인).
- **실측**: 101초 연속 실행에서 `max |rootRotZ| = 0.0`(성공 판정 기준 충족, 절대 안 넘어짐), 낙하고착 0건, 예외 0건. RAGDOLL 강제 트리거 2회 모두 83.8°까지 넘어진 뒤 GETUP으로 정확히 직립 복귀. EditMode 13/13, PlayMode 3/3.
- **이월 과제**: RAGDOLL 중 팔꿈치가 관절 제한을 순간 초과하는 현상(HingeJoint2D 제한이 enable 시점 자세 기준으로 재해석됨). 기능상 무해하나 다음 라운드 정리 대상.

## 2026-08-28 (계속) — 보행 다듬기 중간 저장 후 투명창으로 전환 (리더 판단)
- 사용자가 "좀더 빠르게 진행해줘" + "바탕화면에서 보이게 작업진행중?" 문의 → **리더 판단: 캐릭터 미세조정을 여기서 중단하고 투명창(바탕화면 적용)으로 전환.** 근거: 캐릭터는 이미 충분히 동작하며, 보행 사이클/목길이 같은 미세조정은 실제 바탕화면 환경에 올린 뒤 판단하는 게 더 정확하고, 사용자의 우선순위가 명확히 "바탕화면에서 보이는 것"이다.
- 중단 시점까지 반영된 것(컴파일 에러0/경고0 확인 후 커밋): 키프레임 보행 사이클 작업 진행분, 화면 이탈 방지(더미 발판 폭 4배→1배 원복), StickmanOnScreenFramingTests의 X축 검증 활성화.
- 미완료로 남긴 것: 목 길이 절반 조정(어깨 1.18→1.28), 발 미끄러짐 역산 검증. **투명창 작업 후 재개.**
- **투명창 사전 조사 완료(리더 직접 수행, 파일 미변경)**:
  - UniWindowController는 **Built-in RP에서도 동작** — URP 마이그레이션 불필요(큰 위험 제거).
  - macOS 네이티브 바이너리 `LibUniWinC.bundle`이 **x86_64+arm64 유니버설** 확인(scratchpad에 clone해 `lipo -info`로 실측) — Apple Silicon 바로 지원, Xcode 직접 빌드 불필요.
  - 설치: UPM git URL `https://github.com/kirurobo/UniWindowController.git#upm`
  - API: `isTransparent` / `isTopmost` / `isClickThrough` / `isHitTestEnabled`
  - **보너스**: `isHitTestEnabled`가 커서 위치 기반 자동 히트테스트를 제공 — Phase 3에서 "부분적 클릭관통 해제"(UX_FLOW 15절)로 설계했으나 "진짜 OS 히트테스트는 불가능"이라며 소유권 부기만 해두고 미뤘던 기능이 라이브러리로 해결됨.
  - 주의: "투명은 Unity 에디터에서 동작 안 함, 빌드해서 테스트할 것"(공식 문서) — 기존 빌드 기반 검증 방식이 옳았음을 확인.

## 2026-08-28 — 🎉 진짜 바탕화면 투명 오버레이 성공 (프로젝트 최대 마일스톤)
여러 라운드 실패했던 투명창을 UniWindowController 통합으로 해결. **사용자가 실제 macOS 바탕화면 위에 캐릭터가 걸어다니는 스크린샷으로 확인.**

- **설치**: `Packages/manifest.json`에 UPM git 의존성(v0.9.8). `LibUniWinC.bundle`(x86_64+arm64 유니버설)이 `.app/Contents/PlugIns/`에 정상 포함.
- **배선**: `MacWindowService`가 `isTransparent/isClickThrough/isTopmost/isHitTestEnabled`를 세팅하는 얇은 어댑터로 교체. `SceneBootstrapper.ConfigureUniWindowController()`로 프리팹 자동 배치(`--force` 재현 가능).
- **자체 플러그인 완전 제거**: `StickMateOverlayPlugin.m`/`.bundle`/`build.sh`/`DllImport` 4종/`ConfigureNativePluginImporter()` 전부 삭제, 실제 코드 참조 0건 확인.
- **실측**: `kCGWindowLayer 0→101`(항상위 윈도우서버 레벨 적용 확인), `isTransparent/isTopmost/isClickThrough/isHitTestEnabled` 전부 되읽기 True, 112초 연속 무예외.

**Coder가 진행 중 발견해 고친 함정 3건(전부 중요)**:
1. **헤드리스 크래시**: `-nographics`엔 NSWindow가 없어 네이티브 `_findMyWindow()`에서 프로세스가 통째로 죽음(PlayMode EXIT=133) → 씬에는 비활성 저장, 실제 Player에서만 활성화.
2. **항상위/DPI 조용한 유실**: 라이브러리는 첫 `Update()`에서 창을 붙잡는데 우리 배선은 `Start()`라 더 빨랐음 → `MacOverlayStateEnforcer.cs` 신설(부착 확인 후 재적용+되읽기 검증).
3. **클릭관통 안전장치 무력화**: `isHitTestEnabled=true`면 라이브러리가 매 프레임 `isClickThrough`를 자동 제어해 **Escape 긴급해제가 다음 프레임에 되살아남** → `SetClickThrough(false)`가 `isHitTestEnabled`까지 함께 끄도록 처리. 안전 계약(5초 지연+Escape) 보존.

**시각 피드백 반영**: 얼굴 투명화(흰 채움 제거→검은 링+검은 눈만), 크기 축소(`orthographicSize` 5→12, 화면상 192pt→80pt), 목이 머리 뚫는 문제 수정, MSAA 4x.
- **반짝임 근본 해결**: 원인은 카메라 배경의 밝은 RGB(0.94)가 MSAA 서브샘플 평균을 통해 가장자리로 새어 밝은 프린지를 만든 것. `MacOverlayStateEnforcer`가 **투명이 실제 확인된 뒤에만** RGB를 검정으로 낮추도록 해 계단현상·반짝임 동시 제거하면서 "투명 실패 시 검정-on-검정 금지" 방어책도 유지.

**후속 과제**: 얇은 선 vs Opacity 히트테스트 상충(`hitTestType=Raycast`로 전환 필요 — 마우스 상호작용 연결 시 함께), 창이 화면 전체를 못 덮음(`shouldFitMonitor` 검토), 어두운 배경에서 검은 캐릭터 시인성(흰 외곽선), 타 창 위 정밀 착지 미검증.
**`isHitTestEnabled`가 Phase 3 "부분적 클릭관통 해제"(UX_FLOW 15절)를 대체 가능** — 다음 라운드에 `ILocalClickCaptureService` 대체 검토.

## 2026-08-28 (계속) — 마우스 상호작용 연결 (잡아서 던지기 실동작)
**핵심 발견**: `DragThrowController`/`RodeoCursorWatcher`가 **씬/프리팹 어디에도 배치되어 있지 않았다.** Phase 3에서 로직은 다 만들었는데 `MouseDown` 이벤트 구독자가 0명이라 한 번도 실행된 적이 없었다. 이번 라운드의 본질은 구현이 아니라 **배선**이었다.

- **Raycast 히트테스트 전환**: `hitTestType = Opacity → Raycast`. 라이브러리 소스를 읽고 전제 3가지를 맞춤 — ① EventSystem 필수(`HitTestByRaycast()`가 null 체크 없이 `EventSystem.current` 사용 → 없으면 NRE로 코루틴이 죽어 클릭관통이 얼어붙음), ② `PhysicsGround`를 Ignore Raycast 레이어로 이동(안 하면 화면 하단 20% 띠가 클릭을 삼켜 비침해 원칙 위반), ③ 카메라 명시 지정.
- **클릭 영역**: `isTrigger` GrabArea 캡슐 추가(화면상 약 28×90pt) — 얇은 획(2.5~3pt) 대비 약 10배. 트리거라 물리 거동 무변경.
- **입력 이중 경로**: Unity `OnMouseDown` + 신규 `IGlobalPointerButtonService`(macOS `CGEventSourceButtonState`, 조회 전용) 폴링. 후자는 비활성 앱의 첫 클릭이 콘텐츠 뷰로 안 내려오는 macOS 동작 대비책.
- **좌표계 버그 2건 실측 발견·수정**(중요): ① `desktopDpiScale`이 정확히 2배 틀렸음(`macRetinaSupport:0`이라 Unity가 포인트 단위 보고 → 0.5가 아니라 1.0) → 자기 창 폭/`Screen.width` 직접 측정 방식으로 교체. ② 창이 화면 좌상단이 아니라 (0,61)에서 시작 → `ScreenCoordinateConverter.OverlayOriginOsScreen` 신설, `FallbackPlatformWindowService` 합성 발판도 연쇄 평행이동. **검증: 캐릭터 위치 역산 OS (690,825) vs 실제 커서 (690.1,825.1) — 오차 0.1px.**
- **로데오 커서 실동작 확인** + 실측 중 2건 수정: ⓐ 라이딩 중 `_stillTimer` 누적으로 내려온 다음 프레임에 즉시 재발동(영원히 커서에 붙어 드래그 불가) → 이탈 시 타이머 리셋, ⓑ 커서가 지면선 아래(Dock 영역)면 캐릭터가 바닥 밑에 놓여 Fall 영구 고착 → UX 13절대로 트리거 억제 + `DragThrowState`에도 소프트 클램프(12절).
- **Escape 안전장치 재확인**: 메커니즘이 히트테스트 방식과 무관함을 확인(`isHitTestEnabled=false`가 라이브러리 자동 제어 자체를 정지). 시작 후 5초 구간에서 한 프레임도 안 뒤집힘 실측.
- PlayMode 일시 실패 원인도 해결: 배치모드엔 커서가 없는데 `NullPlatformWindowService`가 고정 (0,0)을 유효 커서로 보고해 로데오가 자동 발동 → 배치모드에서는 "커서 없음"을 정직하게 반환하도록 수정(에디터 Play 모드 무변경).
- 검증: 컴파일 에러0/경고0, EditMode 13/13, PlayMode 3/3, 73초 가동 예외0. PID 76957.

## 2026-08-28 (계속) — 드래그 실패 원인 규명: 데코레이터 계약 구멍 2개
사용자 "마우스로 안 잡힘" → 리더가 Player.log 분석해 **"클릭 감지는 정상인데 상태 전이가 없다"**로 범위를 좁혀 전달 → Coder가 근본 원인 확정.

**원인 (둘 다 조용한 실패)**:
- **(a) 드래그 중단 지점**: `MacWindowService`가 `ILocalClickCaptureService`를 의도적으로 미구현했는데, 이 서비스는 항상 `FallbackPlatformWindowService` 데코레이터로 감싸여 소비된다. 데코레이터는 그 인터페이스를 **자기가 구현**하며 내부에 위임하므로 `_innerClickCapture`가 null → `RequestLocalClickCapture()`가 항상 false → `DragThrowController.OnMouseDown()`의 방어 분기에서 **매 클릭마다 되돌아감**. (Coder가 직전 라운드 보고서에서 "컨트롤러가 방어하고 있어 영향 없다"고 쓴 것이 오독이었음을 스스로 정정 — 그 방어 분기가 바로 중단 지점이었다.) → Win32/Null과 동일하게 공용 `LocalClickCaptureGate`에 위임하도록 구현.
- **(b) 전역 폴링 경로 미연결**: 같은 데코레이터가 `IGlobalPointerButtonService`도 통과시키지 않아 캐스팅이 항상 null(`전역버튼경로=미지원`) → `ICursorPositionService`와 동일 패턴으로 통과. 수정 후 `전역버튼경로=사용 가능` 실측.

**리더 가설 (b)(캡처 유실) 처리**: Player.log에 타임스탬프가 없어 "즉시 연달아"는 미확정 사실이었음. 측정(홀드 시간 로깅)과 방어(전역 폴링이 살아있으면 Unity `OnMouseUp`으로 드래그를 끝내지 않고, 전역 버튼 상태가 "아직 눌림"이면 무시하고 계속)를 동시 적용 — 가설이 사실이어도 드래그가 성립한다.

**단계 로그 `[n/6]` 도입**: 준비→마우스다운→가드통과/실패사유→Dragged진입→추종중→마우스업(홀드시간)→던진속도. **모든 조기 반환에 사유 로그**를 붙였다(조용한 no-op이 이번 진단 지연의 직접 원인이었음).

**캐릭터 색상 선택**: `StickConfig.inkColor`(enum Black/White) + `StickmanAgent.ApplyInkColor()` 런타임 일괄 갱신(LineRenderer 12개). `Start()`에서 항상 호출하므로 프리팹 저장색과 무관하게 config가 이긴다. 흰색 전환: `DefaultStickConfig.asset`의 `inkColor: 0 → 1`(빌드만, 씬/프리팹 재생성 불필요). **눈 색은 선과 같은 색이 정답** — 머리는 링만 있고 안쪽이 비어 바탕화면이 비치므로, 눈동자는 얼굴 위 무늬가 아니라 배경 위 잉크 점이다. 반대색이면 흰 캐릭터의 검은 눈이 어두운 배경에 묻힌다.

PID 77411. 컴파일 에러0/경고0, EditMode 13/13, PlayMode 3/3, 99초 예외0. **실제 드래그 성립 여부는 사용자 마우스 조작 테스트로 판정 필요.**

## 2026-08-28 (계속) — 드래그 밀착 + 자동행동 기본 OFF + 랙돌 자세 개선
사용자 실기기 테스트 피드백 4건 반영.

**(1) 드래그 밀착** — 지연이 두 겹이었음: ① `SmoothDamp(0.08초)`는 지수 스프링이라 **원리상 목표에 도달하지 않아** 커서를 속도 v로 끌면 항상 `v×0.08`만큼 뒤처짐, ② `MovePosition()`은 Kinematic에서 "다음 물리 스텝까지 이동" 예약인데 `Tick()`이 `Update()`에서 돌아 항상 한 스텝 늦음. → `dragFollowSmoothTime` 기본값 0.08→0, 0 이하면 스프링을 건너뛰고 `Rigidbody2D.position`과 `Transform.position`에 **둘 다** 즉시 대입. 잡은 지점 오프셋 유지. **던지기 속도 계산은 무수정** — `ComputeThrowVelocity()`가 몸통 위치를 읽지 않고 커서 좌표 이력(0.12초)만 평균하므로 추종 방식과 구조적으로 무관함. `[4/6]` 로그에 `밀착 오차` 추가.
**(2) 로데오 기본 OFF** — `rodeoCursorEnabled`(기본 false). Watcher가 조기 반환하며 정지 타이머도 리셋해 켜는 즉시 발동하는 부작용 없음. 상태 클래스 무수정. 재활성: asset에서 `0→1`.
**(3) 무작위 점프 기본 OFF** — `wanderPostIdleJumpChance 0.05→0`, `wanderEdgeJumpAttemptChance 0.10→0`. 배회는 유지.
**(4) 랙돌 자세** — 원인 3가지: ⓐ 위 마디(대퇴/상완)에 각도 제한이 **아예 없어 360도 회전 가능** → 고관절 ±65, 어깨 ±75 신설. ⓑ 아래 마디 제한이 0도(완전히 편 상태)를 포함 → 무릎 [-100,-3], 팔꿈치 [+3,+100]로 0 제외. ⓒ **이월 과제 해소**: 관절 disable/enable 때문에 매 진입마다 `referenceAngle`이 다시 굳어 제한이 통째로 밀렸음(직전 실측 팔꿈치 -59도) → 생성 시점에 해부학 기준 제한을 복사해두고 진입 시 `referenceAngle`로 재환산. **부호 규약을 하드코딩하지 않고 실측 판정했는데 결과가 -1(로컬각과 반대)** — 하드코딩했다면 반대로 넣었을 값이었다. damping 상향은 RAGDOLL에서만 유효(보행 무변경).
- 실측: 팔꿈치 +1.9~+71.1, 무릎 -48.4~-1.0, **반대 꺾임 0회**. 60초+ 실행 예외0, Jump/Fall/Ragdoll/Rodeo 전이 0회, 접지 유지.
- PID 78253. 컴파일 에러0/경고0, EditMode 13/13, PlayMode 3/3.

## 2026-08-28 — 🎉 헤드라인 기능 완성: "윈도우 창 = 지형" 실동작 + 사용자 신고 4건 수정
**사용자 확인: "창위에서 잘걸어다님"** — 기획서 1-1절의 핵심 컨셉이 드디어 실제로 동작.

**근본 원인(이번에 처음 발견, 중요)**: 착지 판정이 "창 상단 ±20pt 밴드에 연속 0.1초 체류"였다. 이는 낙하속도 상한 ≈11.3유닛/초를 뜻하는데, 중력 29.4유닛/s²에서 **2.2유닛(78pt)만 떨어져도 밴드를 한 프레임에 통과**해버린다. 즉 실제 창은 물리 콜라이더가 없어 그냥 뚫고 지나갔고, 물리 콜라이더가 겹쳐 있는 화면 하단 안전망에서만 착지가 성립했다. → `TryFindLandingCrossing()` 스윕 교차 판정으로 교체.

**창 전체 덮기**: Quartz 원점 (0,61)→(0,0), 크기 1512x846→1512x982. 메뉴바 33pt·Dock 75pt 띠까지 포함. `isFreePositioningEnabled=true`(macOS visibleFrame 제약 해제) + `Screen.SetResolution`, 화면 크기는 `GetMonitorRect()`(visibleFrame만 반환)가 아니라 `CGDisplayBounds` 사용.

**실측 증거**: Finder 창 상단 OS y=160.0 vs 캐릭터 착지 OS y=160.0 — **오차 0.0pt**. 창 위 보행 좌표가 창 범위 안에서만 이동, 창을 옮기면 캐릭터도 따라감, 창 밖에 놓으면 15.29유닛 낙하 후 안전망 착지.

**사용자 신고 4건 (전부 재현→수정→로그 검증)**:
1. **허공 부유 / 최대화해도 안 떨어짐** → **오클루전 컬링** 도입. `kCGWindowListOptionOnScreenOnly`가 **완전히 덮인 창도 반환**하는 것이 원인이었다. z-order 앞→뒤로 순회하며 상단선에서 앞 창 구간을 빼고 남은 조각만 발판화. 실측: 덮는 창 생성 → `[발판상실]` → 낙하, 창 닫으면 즉시 복귀.
2. **최상단 순간이동** → **발판 고착**(`CurrentFootholdHandle`). 매 프레임 첫 매치 재선택을 제거, 전환은 낙하→착지로만.
3. **화면 밖 소실** → 2중 방어: 발판을 디스플레이 경계로 클리핑 + `EnforceScreenBoundsAndRescue()` 하드 클램프(Update 최후미). 실측 (2012,491)→(1504,491), (756,-400)→(756,8). Fall 6초 지속 시 화면 중앙 리스폰.
4. 추가 필터: alpha<0.05 / 60x40pt 미만 / IsOnscreen=false / 디스플레이 밖 제외.

**정직한 한계**: 안전망이 화면 80% 지점(OS y=785.6)이라, 전체화면 창이 모든 발판을 가리면 여전히 "창 한가운데 떠 있는" 것처럼 보인다. 내리려면 RAGDOLL 물리 바닥/스폰 Y 단일 소스를 함께 옮기고 씬 재생성+테스트 기준선 재설정이 필요해 다음 라운드 후보.

**상시 진단 로그** 도입: `[발판리포트]`(2.5초), `[창진단]`(7.5초, z-order/PID/사각형/alpha/onscreen/보이는 상단폭/탈락사유), `[발판변경]`/`[발판상실]`/`[화면클램프]`/`[캐릭터구조]`. 폴링 0.5→0.3초.
PID 80377. 컴파일 에러0/경고0, EditMode 13/13, PlayMode 3/3, 예외0.

## 2026-08-28 (계속) — 안전망 발판을 Dock 위로 (허공 부유 잔여 문제 해소)
사용자 "지금도 떠있는것처럼보임" — 직전 라운드가 "정직한 한계"로 스스로 기록했던 항목을 처리.

- **단일 소스 상수 하나만 변경**: `NullPlatformWindowService.DummyFootholdHeightFraction` 0.2 → 75/982≈0.0764(`DockSafeBottomInsetPoints=75f`/`ReferenceScreenHeightPoints=982f`로 자기설명식 정의). 근거는 직전 라운드가 실측해둔 값: `GetMonitorRect()` 작업영역 (0,75,1512,874) vs `CGDisplayBounds` 1512x982 → Dock 75pt, Dock 상단 = OS y 907.
- 연쇄 자동 파생: 안전망 발판 상단 OS y 785.6→907.0, 지면 월드Y -7.2→-10.167, `PhysicsGround` Y -8.2→-11.167, 스폰 Y -6.9→-9.867. 카메라 무변경(발이 뷰포트 하단에서 화면상 정확히 75pt 위).
- `grep` 전수 확인: 실제 계산 지점 3곳뿐, 매직넘버 중복 없음. 씬은 구운 값이라 `BuildAll --force` 재생성(프리팹은 바이트 동일 → 오버라이드 고아 없음).
- **실측**: `딛고있음=합성 안전망 | 발판상단OS y=907.0 | 캐릭터OS=(442.8,907.0) 화면안=예` → 60초 뒤 (1389.8,907.0). 화면 982 − Dock 75 = 907, Dock 바로 위에서 걷는다. 예외 0건.
- **테스트 공백 메움(중요)**: 기존 프레이밍 테스트는 상대 판정("뷰포트 대비 0.5유닛 여백")이라 **캐릭터가 화면 한가운데 떠 있어도 초록불이었다 — 그래서 이 버그가 두 라운드 생존했다.** 이제 접지 샘플마다 "발 Y == 발판 상수가 말하는 Y"(허용 1유닛)를 대조한다. 실측 오차 0.002유닛. 씬에 구운 Y와 상수가 어긋나는 회귀(과거 2회 발생, 자동 테스트가 못 잡던 계열)도 함께 잡힌다. 기준선 수치 변경 없이 EditMode 13/13·PlayMode 3/3 유지.
- **진단 로그 정리**: `StickConfig.verboseDiagnosticsLogging`(기본 OFF) 신설. 직전 로그 443줄 중 372줄(84%)이 리포트였다. OFF: `[발판리포트]` 60초 심장박동만, `[창진단]` 무음. ON: 예전 주기. 이상 신호(`[화면클램프]`/`[캐릭터구조]`/`[발판변경]`)는 항상 유지(클램프는 2초 throttle). 실측 90초 49줄.
- PID 81289.

## 2026-08-28 (계속) — 드래그 순간이동·Dock 발판·종료 수단·눈 추적
**① 드래그 중 위쪽 창으로 순간이동 — 리더 가설이 틀렸다.** 리더는 "스윕 교차 판정에 하강 방향 조건이 없어서"라고 추정했으나, `GroundSensor.TryFindLandingCrossing`에는 **처음부터 하강 조건이 있었다**. 진짜 원인 2개:
- **주범**: `DragThrowState`의 지면 소프트 클램프가 "지면"을 `TryGetSurfaceWorldY`(해당 x에서 **가장 높은** 창 상단)로 조회했다. 클램프는 위로만 작동하는 단방향이라 커서 x가 위쪽 창 범위에 걸치기만 하면 매 프레임 그 창으로 끌어올려졌다(실측 **18.0유닛 순간이동**). → `TryGetFloorWorldY`(가장 낮은 표면) 신설·교체. 같은 오류가 `RodeoCursorWatcher`에도 있어 함께 수정.
- 던진 직후: `FallState`의 2순위 밴드+유예 판정에 방향 개념이 없어 상승 중 착지 가능 → 상승 속도 가드 추가.
- PlayMode 회귀 테스트 3종 신설. 실측: 전폭 창(월드 +11.19) 아래에서 화면 전체를 끌어도 몸통 Y가 −6.21~−0.10 유지.

**② Dock 발판 + 바닥 안전망 — 리더 지시 1항이 실측상 불가능함을 발견.** `CGWindowListCopyWindowInfo`가 반환하는 Dock 창 bounds는 막대가 아니라 **화면 전체(0,0,1512,982)**다. 정확한 폭은 `CGWindowListCreateImage`로만 얻히는데 그건 **화면 기록 권한**이 필요해 비침해 원칙상 배제. → **세로는 실측, 가로만 설정값**: Dock 발판(핸들 −2) 상단 = 화면 바닥 − 75pt, 폭은 정중앙 65%(실측 70.7%보다 **의도적으로 좁게** — 틀리는 방향을 안전한 쪽으로). 안전망은 상수 하나(40pt)만 바꿔 화면 최하단(OS 942)으로 이동, 스폰/지면/씬/테스트 자동 파생. **스폰을 화면 중앙으로 옮겨야 Dock에 착지 가능함도 발견·수정**(사용자가 중간 빌드에서 "독 위를 안 걷는다"고 본 그 증상). 화면 클램프에 시각 반폭 반영(여유 58.2pt).

**③ 종료 수단 2가지(둘 다 실측 검증)**: (1) **Ctrl+Option+Cmd+Q** 종료 / C=잉크색 / R=로데오 / D=진단로그 — `CGEventSourceKeyState`가 권한 없이 동작함을 먼저 실측 확인. (2) **캐릭터 우클릭 → [앱 종료]** 메뉴 패널 — 합성 마우스 입력으로 실제 클릭해 프로세스 종료까지 확인. 안전장치(5초 지연/Escape) 무변경.

**④ 눈 마우스 추적**: 링 밖 이탈 불가(기하 상한 0.09 clamp), 지수 감쇠 k=12, 근접 중립/원거리 포화, 머리 로컬 변환으로 RAGDOLL 대응. 실측 거리 23유닛→오프셋 0.0500(포화), 1.10유닛→0.0185.

**⑤ 매달려 내려가기 — 이월**(리더가 허용한 범위).
PID 83153. 컴파일 0/0, EditMode 13/13, PlayMode 6/6.
