# StickMate Process Log
> 리더(아키텍트)가 구현 단계마다 갱신 + GitHub 커밋.

## 2026-08-27
- 프로젝트 초기화. 이전 PC 작업물/메모리 없음을 확인 — git 미초기화 상태였음. `git init` 수행.
- 팀 구성 완료: UX Designer, Coder, Debugger, Test Engineer(겸 Reviewer), Performance Engineer(겸 Doc Writer). 리더=Architect 겸임.
- 리서치: 스틱메이트 무빙 방식 웹서치 → **Active Ragdoll + IK 하이브리드** 채택 (docs/ARCHITECTURE.md 0절 참조).
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
- Coder: Phase 3(전투/커서상호작용) 5개 기능 전부 구현 — 부분적 클릭관통 해제(인프라, 단 진짜 OS 히트테스트는 BUG-B1 미해결로 소유권 부기까지만), 격파 미니게임, 라이벌 스틱메이트 AI(관전전용), 드래그&던지기, 로데오 커서. AttackState도 이번에 완성(예전엔 Tick() 비어있어 영원히 안 빠져나오는 상태였음). 공통 인프라 SpectacleEventLock(4개 스펙터클 상호배제)/RagdollImpactResolver(중복 제거) 신설.
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

## 2026-08-28 (계속) — 고전 스틱메이트(가는 선) 스타일 캐릭터 재작업
- 사용자가 실행 중인 앱을 보고 "이상하게 나온다" + "기존 스틱메이트처럼 만들어달라" 피드백 → 스타일 확인 질문 후 "고전적 스틱메이트 느낌(가는 선만으로)" 확정.
- Coder: 채워진 사각형/원 스프라이트를 전부 LineRenderer 기반으로 교체(속이 빈 원 머리 24점, 두께0.05 둥근캡 선 몸통/팔다리, 손발 끝 짧은 선 보너스). 물리 구조(Rigidbody2D/Collider2D/HingeJoint2D)는 전혀 무수정 확인.
- 오버레이 창 "이상함" 후보 원인 재검토: NSWindowStyleMaskFullSizeContentView가 신호등 버튼 경계를 부자연스럽게 만들 수 있다고 판단해 제거, titlebarAppearsTransparent+titleVisibility만 유지(더 보수적인 조정).
- 재빌드+재실행: 이전 PID 49739 종료 후 **새 PID 57301**로 재실행 중.
- 컴파일 에러0/경고0, EditMode 13/13, PlayMode 4회 반복 전부 통과(화면가시성 테스트도 Renderer[] 일반화로 LineRenderer 커버 확인).

## 2026-08-28 (계속) — 실제 데스크톱 낙하고착/랙돌폭주 버그 수정 + 손발 표현 개선
- 사용자가 실제 macOS 데스크톱에서 실행 중인 앱 스크린샷 3장을 연속 전송 — 검은 배경(안 보임), 팔다리 뒤엉킴(랙돌 폭주), 쓰러진 채 안 일어남(랙돌 고착) 순으로 문제 확인.
- 사용자 요청으로 리더가 "스틱메이트" 원조를 웹 조사(위키백과 등) — 손발은 짧은 직각선이 아니라 작은 점/원으로 표현하는 게 정석임을 확인, Coder에게 전달.
- **실측으로 발견한 진짜 원인 2건**(Blocker급, 이 프로젝트가 지금까지 배치모드 640x480에서만 테스트해서 못 잡았던 실제 데스크톱 전용 버그):
  1. FallbackPlatformWindowService의 안전망 발판이 고정 40px 높이라 씬이 가정하는 지면 Y(화면하단 20%)와 실제 해상도에서 어긋나 낙하 고착.
  2. 안전망 발판 폭이 에디터 더미발판 대비 4배 좁아 자율배회 AI가 정상 배회만으로 가장자리 이탈 반복 → Fall 재발 → 축적된 낙하속도로 충돌 시 랙돌 폭주.
  (참고: 리더의 Retina DPI 가설은 Coder 실측 조사 결과 기각 — PlayerSettings.macRetinaSupport는 Screen.width/height에 영향 없음 확인. 대신 backingScaleFactor 자동감지로 desktopDpiScale 실행시 설정하는 것은 별도로 구현됨, 이 환경 0.500 정상 검출.)
- 손발 표현을 짧은 직각선에서 작은 채워진 점(반지름0.04)으로 교체(스틱메이트 레퍼런스 반영).
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

## 2026-08-28 (계속) — 매달려 내려가기 + 보행 다듬기(부분)
**① 매달려 내려가기 (완료)**: 신규 `States/LedgeHangState.cs` + `StickmanStateId.LedgeHang`. `ParkourClimbState` 확장이 아니라 별도 상태로 간 근거가 타당 — 등반은 "시작Y→벽상단Y" 단일 페이즈에 종료가 Idle/Walk인데, 하강은 **잡기→매달림→손놓기 3페이즈**에 종료가 **항상 Fall**이라 실제 공유 코드가 "발판 핸들 재확인" 한 줄뿐(그것도 이미 `GroundSensor` 정적 유틸이라 합칠 필요 없음). 모드 플래그를 넣으면 검증된 등반 경로까지 회귀 위험에 들어감.
- 트리거: `IMovementIntentSource.LedgeHangRequested` 펄스 신설. `AutoWanderController`가 경계에서 `ledgeHangChance`(0.35)를 **걷기 구간당 1회만** 추첨, 화면 끝 제외 + `GroundSensor.TryFindDescendTarget()`으로 "내려앉을 발판 실존"까지 확인. 0으로 두면 이전 거동과 100% 동일.
- 포즈: 어깨 180°(팔을 위로 뻗어 모서리 잡기), 다리 늘어뜨림, 좌우 흔들림. 매달린 루트Y = 모서리Y − 2.507유닛(프리팹 지오메트리에서 유도).
- 실측(PlayMode 3종 신설): Walk→LedgeHang(1.23초)→Fall→아래 발판 착지(Y −8.400 정확 일치) / 매달린 높이 오차 0.000 / **발판 소실 → 1프레임 만에 Fall** / 유지시간 999초 설정해도 상한 0.90초에 정확히 손 놓음.
- 한계: 현재 데스크톱에선 캐릭터가 Dock 위에 살고 아래가 0.86유닛뿐(<2.507)이라 발동 안 함. **실제 창 위에 있을 때 동작한다.**

**② 보행 다듬기 (부분)**: 목 길이는 이미 반영돼 있었음(실측 목 0.070). **발 미끄러짐 역산도 이미 정확**했음 — 실제 보폭 0.775 vs 명령 0.776(오차 0.1%). 대신 **새로 찾아 고침**: 손으로 적은 바운스 곡선의 **위상이 기하학과 반대**여서 디딤발이 지면을 0.0252 파고들고 0.0696 떠 있었음 → 다리 각도에서 유도하는 `ComputeFootGroundingOffset()`으로 교체, 접지 오차 0.0696→0.0388.
- **잔여(리더 판단 필요)**: 디딤발 불일치율 20.1%. 스윙 무릎 클램프를 시도했으나 보폭이 15% 줄어 미끄러짐이 0.216→0.564로 **악화되어 전량 되돌림**. 근본 해법은 2본 IK 재설계.
PID 84611. 컴파일 0/0, EditMode 13/13, **PlayMode 10/10**(6+4).

## 2026-08-29 — 말풍선 렌더링 + 라이벌 스틱메이트 (Phase 3~5 시각 레이어 착수)
**① 말풍선(`Dialogue/DialogueBubbleRenderer.cs` 신규)** — 프로젝트 1순위 원칙("행동-텍스트 일치")의 산출물이 처음으로 화면에 나온다. **원인: `DialogueRequested`를 구독하는 코드가 프로젝트 전체에 하나도 없었다**(grep 확인). 파이프라인은 멀쩡했고 소비자만 없었다 — 이 프로젝트에서 반복된 "로직은 있는데 배선이 없음" 패턴의 또 다른 사례.
- 방식: legacy uGUI(ScreenSpaceOverlay + `Text`). 캐릭터는 LineRenderer지만 말풍선은 글자가 본체라 레이아웃/줄바꿈이 필요하고 TMP가 없음. 캔버스는 **씬 루트**에(캐릭터 자식이면 RAGDOLL 회전이 섞임). 흰 채움+검은 테두리 2.5px+굵은 검은 글씨, 꼬리 삼각형은 알파 커버리지 텍스처를 코드 생성(에셋 없음). **한글은 `RequestCharactersInTexture`→`GetCharacterInfo('한')`로 글리프를 실측**해 폰트 선택 → `Apple SD Gothic Neo`.
- **UX 5절 계약을 PlayMode 4건으로 고정**: 강제 인터럽트 → **같은 프레임 즉시 제거**(실행 로그 `표시 frame=14` → `제거 frame=14`). `IsForcedInterrupt`를 함께 구독하며, `ChangeState`의 발행 순서상 렌더러가 항상 Intent보다 먼저 플래그를 받음(프레임 번호 대조 검증). **정상 종료는 최소 노출 0.7초 후 페이드아웃 — 이 쌍이 있어야 "무조건 즉시 지우기" 오답이 걸러진다.** 큐잉 금지(즉시 교체), 화자 분리 각 1건.
- 대사 빈도: Idle/Walk에 대사가 아예 없어 볼 기회가 없었음 → `Dialogue/AmbientChatter.cs` 신설. **확률/쿨다운 추첨을 `Enter()` 안에서 텍스트 생성 전에 끝내고, 고른 줄 번호를 `IHasDialogueParams` 스냅샷으로** 만들어 매핑 함수는 난수를 안 쓰는 순수 함수(UX 31-3 역추적 가능 원칙 준수). Idle 28%/Walk 14%/쿨다운 11초.

**② 라이벌 스틱메이트** — 씬에 아예 배치되어 있지 않았음. 프리팹 복제→언팩→플레이어 전용 컴포넌트 제거 방식으로 배선(지오메트리 단일 소스 유지), 붉은색 `rivalInkColor` 신설, `Ctrl+Opt+Cmd+V` 강제 소환.
- **배선 중 조용한 버그 발견·수정**: `EditorSceneManager.NewScene`이 StickConfig 에셋을 언로드해 참조가 "가짜 null"이 되면서 `_config`가 null로 직렬화 → 라이벌이 **아무 에러 없이** 영원히 안 뜸. 3겹 방어(재로드+런타임 폴백+경고 로그).
- 실측: 확률을 임시 100%로 올려 검은 플레이어 옆 붉은 라이벌 스크린샷 확인 후 원복. 같은 프레임에 두 캐릭터가 각각 "한 발 더!"/"윽...!"을 띄우고 섞이지 않음(화자 분리 검증).

**신규 단축키**: `Ctrl+Opt+Cmd+B` 말풍선 즉시 띄우기, `Ctrl+Opt+Cmd+V` 라이벌 강제 소환.
PID 88660. 컴파일 0/0, EditMode 13/13, **PlayMode 14/14**(10+4).

## 2026-08-29 (계속) — 격파 미니게임 + 그라피티 시각화
**사전 확인에서 또 같은 패턴**: `BattleMinigamePhaseChanged` 구독자 **0명**, `BattleMinigameDirector`/`GraffitiDirector` **씬·프리팹 미배치**. 로직은 완성돼 있는데 화면엔 한 픽셀도 안 나오는 상태(말풍선/드래그/라이벌과 동일 유형, 이번이 4번째).

**① 격파 미니게임**(`Interaction/BattleMinigameRenderer.cs` 신설): 판자 2장이 순차 낙하해 쌓이고 착지 후 미세 흔들림, 기 모으기 게이지 4겹(흰 트랙/검은 채움/**스위트스팟 밴드**/검은 테두리). **스위트스팟을 채움보다 위에 얹어** 정답 구간이 채움에 덮여 사라지지 않게 함, 진입 중에는 주황으로 굵어지며 깜빡임. 경계값은 하드코딩이 아니라 `StickConfig.battleSweetSpot*` 참조. 성공 시 파편 14조각+임팩트 선 7줄, 실패 시 감쇠 흔들림, 소진/타임아웃 시 축소 페이드.
- 클릭: 판자에 `isTrigger` 콜라이더를 만들어 기존 `StickmanClickHitbox`에 등록(새 입력 경로 없이 `MouseDown`에 합류), 종료 시 즉시 파괴해 클릭관통 원복. 배치는 캐릭터 옆·주먹 높이 이하라 머리 위 말풍선과 안 겹침.
- 실측: 성공(83.9%)·실패(48.2%)·타임아웃 3경로를 실제 클릭으로 전부 확인.

**② 그라피티**(`GraffitiRenderer.cs`): 낙서 4종(웃는얼굴/별/스틱메이트/하트)을 전체 경로 길이 기준 진행률로 스프레이하듯 순차 등장, 정상 종료 0.8초 / 창 침범 취소 0.18초 페이드아웃. **기본 설정(96px)에서는 현재 데스크톱에서 발동 안 함** — 창이 화면을 다 덮어 빈 영역이 없기 때문이며 이는 UX 27-3이 요구한 정상 동작. 렌더러 실물은 영역을 임시 56px로 낮춰 스크린샷 확인 후 **설정 에셋 완전 원복(diff 0)**.

**실행 중 발견해 고친 버그**: 첫 실행에서 `[격파] 소환`이 2번 찍힘. 라이벌이 플레이어 프리팹 복제본이라 신규 컴포넌트 4개를 물려받고, 렌더러가 전역 정적 이벤트를 구독해 소환물을 두 벌 그림 → `SceneBootstrapper`에서 라이벌의 4개 컴포넌트 제거 + 렌더러의 씬 전체 탐색 폴백 제거(이중 방어).

**신규 단축키**: `Ctrl+Opt+Cmd+K`(격파), `Ctrl+Opt+Cmd+G`(그라피티). 우클릭 메뉴에도 2행 추가.
PID 95122. 컴파일 0/0, EditMode 13/13, PlayMode 14/14.
**미처리**: 사용자 신고 "Dock과 겹쳐서 걸음"(안전망을 Dock 좌우 두 조각으로 분할) — 리더 메시지가 에이전트 작업 계획에 늦게 도착해 이번 라운드에 미반영. 다음 라운드 최우선.

## 2026-08-29 (계속) — Dock 겹침 수정 (안전망 좌우 분할)
사용자 신고 "처음엔 독위에서 잘다니다가 좀 다니다 보면 다시 독과 겹쳐서 걸음". 리더 진단: Dock 가로 끝을 벗어나 화면 최하단 안전망에 착지한 뒤, **안전망이 화면 전체 폭이라 다시 Dock 구간 안쪽으로 걸어 들어가** Dock보다 낮은 높이에서 겹쳐 보임.

- **단일 소스화**: `FallbackPlatformWindowService.TryGetDockSpanOsScreen(out left, out right)` 신설. Dock 발판 사각형(`TryGetDockFoothold`)과 안전망 구멍(`AppendBottomSafetyNet`)이 **둘 다 이 메서드에서만** 좌우 끝을 얻어 틈·겹침이 구조적으로 불가능. Dock 비활성 시엔 예전과 100% 동일한 전체 폭 1장으로 폴백.
- **조각 핸들 분리**: `SyntheticFootholdHandleRight = -3L` 추가. **같은 핸들이면 발판 고착이 두 조각을 하나로 취급해 경계 판정이 반대편 값으로 잡히는** 문제가 있어 반드시 분리 필요.
- **물리 바닥과 역할 분리**(중요): `PhysicsGround`(폭 200유닛)는 **전체 폭 유지**. 논리 발판=접지/착지/경계 판정 전용(Dock 구간에 구멍 필요), 물리 바닥=실제 충돌면(구멍 뚫으면 랙돌이 화면 정중앙에서 바닥을 뚫음). 두 문서에 차이 명시.
- grep 전수 확인: Dock 구간 계산 지점 1곳, `DummyFootholdHeightFraction` 연쇄(안전망/지면/스폰/테스트) 유지, `MacWindowService`에 중복 계산원 없음.
- **실측**(실제 앱 1512x982): `합성=[Dock x265~1247 상단y907, 안전망왼쪽 x0~265 y942, 안전망오른쪽 x1247~1512 y942]` — 조각 끝이 Dock 끝과 정확히 맞물림. PlayMode 프로브 12곳 전부 예상대로. 경계 통과: 왼쪽 조각 보행 한계 x=108.8(Dock 좌단 112.0) 이후 접지 불성립. RAGDOLL: Dock 정중앙 6초 관찰 최저 y=−11.021 vs 바닥 −11.022, 관통 없음.
- **신규 테스트** `Tests/PlayMode/DockSafetyNetSplitTests.cs` 6종(분할 기하/Dock 비활성 폴백/x별 착지 높이/**Dock 밑 보행 차단 회귀 잠금**/폭 추정이 안전한 방향인지/랙돌 관통). **PlayMode 20/20**.
- **정직한 한계 2가지**: (1) Dock 폭 추정 65% vs 실측 70.7% 차이로 Dock 바깥 모서리 44pt 띠에서 약간 겹쳐 보일 수 있음(반대 방향은 "허공 부유"라 의도적으로 이쪽 선택). (2) Dock 상단→바닥 낙차 0.855유닛이 매달리기 최소 낙차보다 작아 캐릭터가 Dock에서 스스로 내려오지 않고 경계에서 되돌아섬 — 배회 정책 결정 필요해 미처리.
PID 96014. 컴파일 0/0, EditMode 13/13, PlayMode 20/20.

## 2026-08-29 (계속) — 낙차가 작은 턱에서 뛰어내리기 + 되올라가기 (커밋 97f644a)
직전 라운드의 미처리 항목 2개를 사용자에게 제시했고, 사용자가 **"2번 뛰어내리기"**를 선택. 즉 Dock 경계에서 되돌아서지 말고 스스로 내려가게 한다.

**리더 판단(지시 내용)**: 새 상태를 만들지 말 것. 낙차로 갈래를 가르되 두 구간이 **틈도 겹침도 없이 맞물리게** 할 것. 그리고 **"뛰어내린 뒤 다시 올라올 수 있어야 한다"** — 경계 점프 확률이 기본 0이라(사용자 신고 "이상하게 점프도 하고"로 껐음) 되올라갈 다른 경로가 없어서, 이게 없으면 한 번 내려간 캐릭터가 영영 Dock 아래에 갇힌다. 반쪽짜리 기능이 된다.

- **밴드 판정으로 일반화**: `GroundSensor.TryFindDescendTarget`이 하드코딩하던 낙차 하한(`Mathf.Max(detectionRadius, minDropDepth)`)을 `[min, max)` 밴드 인자로 바꿔 **호출부가 구간을 정하게** 했다. 매달리기 `[2.507, ∞)` / 뛰어내리기 `[0.35, 2.507)`. 상한 기본값(0)이 매달리기 하한에서 **자동 유도**되므로 두 구간이 어긋날 수 없다 — Dock 구간 단일 소스화와 같은 설계 원칙.
- **경계 행동 추첨 통합**: `AutoWanderController`가 낙차/높이로 갈래를 **먼저 가른 뒤** 그 갈래의 확률로만 추첨하고, 한 걷기 구간당 1회로 제한. 세 갈래를 각각 따로 뽑으면 "아무것도 안 할 확률"이 곱으로 떨어져 경계마다 뭔가를 하게 된다. 공중으로 나가면 추첨권이 리셋된다(모서리를 이미 떠났으므로).
- **확약(commit) 서브 상태**: 당첨 즉시 발을 떼면 몸이 아직 발판 위라 `FallState` 스윕이 **방금 떠난 발판에 도로 착지**시킨다(실측 로그 `낙하높이=0.00유닛`). 모서리 코앞까지 더 걸어간 뒤 떼도록 분리.
- **StepUp 채널 신설**: 기존 점프 분기는 "벽 있으면 등반, 없으면 점프"라 **실패 시 점프로 흘러내린다**. 되올라가기를 거기 얹으면 `wanderEdgeJumpAttemptChance=0` 결정이 무력화되므로 별도 채널로 분리 — 벽을 못 찾으면 아무 일도 안 일어난다.
- **`ParkourClimbState` 잠복 결함 발견·수정**: y만 보간하고 x는 손대지 않아, 등반 완료 시 캐릭터가 **턱 위가 아니라 턱 옆 허공**에 있었다(다음 프레임 접지 실패 → 도로 낙하 → 등반이 통째로 무효). 경계 점프 확률이 0이라 아무도 이 경로를 밟지 않아 드러나지 않았을 뿐이고, 되올라가기를 붙이면서 상시 경로가 되자 즉시 발현. 턱의 가까운 모서리에서 안쪽으로 맨틀 수평 이동 + 발판 핸들 고착으로 수정.
- **신규 `StickConfig` 9개** 전부 0으로 두면 이전 거동과 100% 동일(탈출구 보존).
- **신규 테스트** `Tests/PlayMode/EdgeHopDownTests.cs` 4종. 핵심은 4번 — **스크립트 펄스 없이 자율 배회만으로** Dock에서 내려갔다가 스스로 되올라오는 왕복을 확인한다(리더가 "못 올라오면 반쪽짜리"라고 못박은 항목의 직접 잠금). 3번은 등반 후 X가 Dock 가로 범위 안인지까지 절대값으로 단언 — 상대 마진 방식 테스트가 버그를 2라운드 연속 놓친 전례 때문.
- **실측**(실제 macOS): 낙차 0.855 → 안전망(-3) 착지 → 턱 높이 0.855 감지 → Dock(-2) 되올라감. 자율 배회만으로 5.57초 만에 왕복.
- **리더 독립 검증**: 빌드 산출물 시각(07:27)이 마지막 소스 수정(07:25)보다 뒤 → 컴파일 무결성은 빌드 성공 자체로 입증. Dock 발판 우단과 바닥 조각 좌단이 `TryGetDockSpanOsScreen` 하나에서 파생되므로 벽 감지의 "가로 인접" 조건이 성립함을 코드로 재확인. 임계값 정합성 직접 계산: 0.855가 뛰어내리기 밴드 `[0.35, 2.507)` 안, 되올라가기 상한 1.5 이하, 벽 감지 최소 높이 0.5 초과 — 세 조건 모두 만족.
PID 97258. 컴파일 0/0, EditMode 13/13, PlayMode **24/24**(기존 20 + 신규 4).

**이어서 착수(리더 판단)**: 에이전트가 보고한 잔여 지점 2건을 마감 작업으로 분리 발주.
1. **화면 물리적 끝에서 "제자리 걷기"** — 화면 클램프 한계(약 58pt)가 배회 AI의 경계 판정 거리(0.3유닛≈24pt)보다 커서, 화면 끝에서는 경계 판정이 영영 안 걸리고 클램프를 계속 밀어댄다(걷기 애니메이션은 도는데 위치는 안 변함). Walk 지속시간 만료로만 풀린다. **클램프 한계를 단일 소스로 노출**해 경계 판정이 그 한계까지의 거리를 재게 하는 방향.
2. **발을 뗄 때 앞으로 튀는 거리**(0.31유닛≈25pt를 한 프레임에) 축소 — 순간이동 자체의 근거는 정당하나(스윕이 떠난 발판을 다시 잡음), 이 프로젝트 사용자는 순간이동성 아티팩트에 반복적으로 민감했다. 0.15유닛 이하로 줄이거나, 가능하면 "방금 떠난 발판을 짧은 시간 착지 후보에서 제외"(drop-through 관행)로 0으로.

## 2026-08-29 (계속) — 화면 끝 러닝머신 + 내딛기 순간이동 (커밋 3b5094c)
직전 라운드 에이전트가 보고한 잔여 지점 2건을 리더가 마감 작업으로 분리 발주한 결과.

- **화면 끝 제자리걷기**: 클램프 한계(≈58pt)가 경계 판정 거리(≈24pt)보다 커서 화면 끝에서 경계 판정이 영영 안 걸리던 문제. `ComputeScreenClampOsBounds()`를 유일한 생산자로 두고 하드 클램프와 신규 조회 API가 둘 다 그것만 읽게 했다(Dock 구간 단일 소스화와 같은 원칙 — 이 프로젝트는 두 곳이 따로 계산해 어긋난 버그가 이미 2회 있었다). 이제 클램프에 **닿기 0.15유닛 전에** 스스로 멈추고 0.67초 만에 돌아선다.
- **내딛기 순간이동 0.31유닛 → 0.000유닛**: 리더는 "0.15 이하로 줄여라, 더 나은 구조적 대안이 있으면 그걸 써도 좋다"고 했고 에이전트가 후자를 택했다 — **drop-through**(뛰어내린 직후 방금 떠난 발판을 짧은 시간 착지 후보에서 제외)로 바꿔 몸을 옮기는 코드 자체를 삭제. 유예는 시간 경과로만 만료되어(해제 호출 없음) 영구히 남는 사고가 구조적으로 불가능하다.
- **검증 방식 진전 — 네거티브 컨트롤**: 수정만 되돌리고 같은 테스트를 다시 돌려 8초간 방향 전환 0회임을 확인했다. "새 테스트가 실제로 이 버그를 잡는다"는 증거다. 이 프로젝트는 **통과하는 테스트가 버그를 2라운드 연속 놓친 전례**(프레이밍 테스트의 상대 마진 방식)가 있어 이 방식을 앞으로 표준으로 요구한다.
PID 99406. 컴파일 0/0, EditMode 13/13, PlayMode 26/26.

## 2026-08-29 (계속) — 리더 전수 감사 + Phase 4 시각 레이어 3종 (커밋 9c7c786, 953d92a)
**리더 판단의 전환점**: 이 프로젝트에서 "구현 완료 보고 → 화면에 아무것도 안 나옴"이 **5회** 반복됐다. 매번 사후에 발견했다. 이번엔 순서를 뒤집어 **발주 전에 grep으로 전수 감사**하고 그 목록을 지시문에 넣었다.
- 감사 결과: 구독자 0명 이벤트 **11건**, `SceneBootstrapper` 미배치 디렉터 **9개**. Phase 4/5 기능 상당수가 빌드에서 죽은 코드였다.
- 우선순위 근거: 창 도둑/크래시/하드웨어 반응 3종은 **이미 있는 창 열거 기능만으로 동작**하고 추가 OS 권한이 필요 없고 데스크톱에서 즉시 눈에 보인다 → 이번 라운드.
- **의도적 보류 구분**: 청소부·블랙홀은 "배선을 깜빡한 것"이 아니라 **플랫폼 제약으로 막힌 것**이다(`MacWindowService`가 `IDesktopIconLayoutService` 미구현, 실제 아이콘 좌표는 접근성/화면기록 권한 필요 → 비침해 원칙상 배제). 이 구분을 문서에 명시해 다음 팀원이 "왜 이것만 안 했나"를 다시 묻지 않게 했다.
- **죽은 코드가 계약 위반을 숨긴다**는 실례를 얻었다: `WindowTheftDirector`가 정상 종료 `Completed`를 한 번도 발행하지 않고 있었는데, 구독자가 0명이라 아무도 몰랐다. 렌더러를 붙이는 순간 고스트가 영구히 남는 버그가 됐을 것이다.
- **불변 원칙 3을 실물로 증명**: 가짜 균열이 떠 있는 3초 사이에 계산기 `7` 버튼을 실제로 클릭해 통과를 확인했다(Phase 4 당시 "런타임 검증 필요"로 남아 있던 항목의 직접 해소).
PID 1581. 컴파일 0/0, EditMode 13/13, PlayMode 37/37. 배선 감사 진척: 이벤트 11→8, 디렉터 9→6.

**이어서 발주(리더 판단)**: Phase 5 4종(스트레스 게이지 / 가출 / 투두 포스트잇 / 포모도로 감시자). 지시문에 직전 라운드에서 실제로 밟은 함정 2개(캐릭터 원점이 발 높이 / 창 상단 테두리에서 머리 위가 화면 밖으로 잘림)를 미리 넣었고, 네거티브 컨트롤 검증을 표준으로 요구했다. 아직 한 번도 검증된 적 없는 항목 2개(`TodoPostItWidget`의 uGUI 클릭 실발동, `RunawayDirector` 은신 중 `OnMouseDown` 재발동)를 실측 대상으로 명시했다.

## 2026-08-29 (계속) — Phase 5 시각 레이어 4종 (커밋 aed8cf3)
리더 감사 목록 소진. **배선 감사 최종: 이벤트 11 → 4, 디렉터 9 → 1.** 남은 4건 중 1건은 플랫폼 제약 보류(아이콘 좌표), 3건은 모션/라이벌 계열로 별도 묶음.

- **리더 지목이 적중했다.** 지시문에서 "아직 한 번도 검증된 적 없는 항목"으로 콕 집었던 `TodoPostItWidget`의 uGUI 클릭이 실제로 **두 겹으로 막혀** 있었다: 씬 `EventSystem`에 입력 모듈이 없어 `Button.onClick`이 구조적으로 발동 불가능했고, 클릭관통 차단 콜라이더가 없어 클릭이 밑의 앱으로 샜다. 앞으로 uGUI 클릭에 의존하는 기능은 이 두 겹을 먼저 확인하는 것을 규칙으로 삼는다.
- **패턴 6번째**: `TodoListModel.Add()` 호출자가 프로젝트 전체에 0건이었다 — 목록이 영원히 비어 투두 기능 전체가 도달 불가능. 감사가 이벤트/씬배치는 잡았지만 **"모델 API에 호출자가 없다"는 축은 못 잡았다.** 다음 감사에 이 축을 추가한다.
- 직전 라운드의 실패(머리 위 오버레이가 가슴팍에 겹침)를 지시문에 미리 넣은 효과가 있었다 — 스트레스 렌더러가 어깨 높이 1.33, 하드웨어 이모트가 2.32로 **세로 분리**되어 설계됐다.
- **네거티브 컨트롤이 표준으로 자리잡았다**: 이번에도 수정만 되돌려 새 테스트가 실제로 실패하는지 확인했다.

**동시 작업 사고**: 시각 수정 라운드와 Phase 5가 같은 워킹 트리에서 겹쳐, 한쪽의 편집 중간 상태 때문에 다른 쪽 Unity 실행이 2회 실패했다. 리더가 커밋 시 파일을 분리해 정리했으나, 병렬 발주 시 **지시문의 파일 소유권 경계만으로는 편집 중간 상태를 막지 못한다**는 것이 확인됐다. 앞으로 같은 파일군을 만질 가능성이 있으면 병렬로 띄우지 않는다.

**진행 중**: 사용자 직접 신고 3건(게이지가 캐릭터와 겹침 / 텍스트 폰트가 부드럽지 않음 / 말풍선을 네모 대신 타원으로) 수정 라운드.

## 2026-08-30 — 기어 부채꼴 메뉴 + 죽은 이벤트 3건 + Dock 낙차 단일화 (커밋 5524506)
사용자 지시("최대한 병렬로 진행해줘")에 따라 3개 스트림을 동시 착수: (1) ux-designer 확정 설계를 반영한
기어 부채꼴 메뉴(코더), (2) 잔여 죽은 이벤트 3건 배선(코더), (3) 누적 변경사항 횡단 리뷰(test-engineer).

- **횡단 리뷰(R1)가 Major 3/Minor 8 반려** → Dock 낙차 상수 재교정 + tilesize 편향 수정을 디버거에게
  즉시 병렬 투입(파일 비충돌 확인 후). 원칙 2(전체화면 숨김) 위반 건은 기어메뉴 코더 작업 완료 후로 순연.
- **기어메뉴 코더가 ux-designer의 4개 구조적 반박**(회전·펼침 동시 진행 / atan2 각도 스냅 / 부채꼴 전체
  단위 클램프 / uGUI 전면 이관)을 전부 반영, 실측 근거로 확정 설계에서 3곳 이탈(전부 승인). 검증 중
  비침해 원칙 위반 버그(팝오버 닫힌 뒤 클릭 차단막 미해제) 자체 발견·수정.
- **죽은 이벤트 코더가 진행 중 실제 회귀를 우연히 발견**: 기어메뉴 위젯이 캐릭터 루트 밑에 "Head"란
  이름의 UI 자손을 만들어 `StickmanPoseAnimator`가 이를 진짜 머리로 착각, 캐릭터 머리/몸통이 영구
  고정되던 버그. 자기 파일 안에서만 응급 봉합(탐색 범위를 루트 직속 자식으로 제한)하고 잔여 위험을
  리더에게 보고.
- **리더가 diff를 전부 직접 대조**해 3개 병렬 스트림의 산출물이 충돌 없이 통합됨을 확인, 잔여 위험
  (`BuildLimb`의 동일 계열 취약점)을 같은 파일의 이미 승인된 패턴으로 직접 수정.
- **R2 통합 검증(test-engineer)이 다시 반려**: 이전 판단("다른 에이전트 작업 소관")이 틀렸음을 실측으로
  지적(Dock 스텝업 테스트 2건이 통합 후에도 재현) + 회귀의 생산자 측이 그대로 남아있음(m1)/EyeController의
  동일 계열 취약점(m2)/원칙 2 위반 범위가 부채꼴 완성으로 확대됨(m3) 발견.
- **디버거 + 코더 2차 병렬 투입**: 디버거는 Dock 스텝업 테스트를 결정론적으로 재작성(제품 코드 무수정,
  제시안보다 더 엄밀한 해법을 스스로 채택), 코더는 리더가 전달한 진단 중 하나(CharacterPortraitStage도
  원인이라는 부분)를 코드 추적으로 직접 반증하고 실제 생산자(GearRadialMenuWidget)만 정확히 고친 뒤
  3단계 네거티브 컨트롤로 검증. EyeController 방어 + 원칙 2 적용까지 완료.
- **최종 상태**: 컴파일 0/0, EditMode 88/88, PlayMode 226/226. 리더가 최종 통합·커밋.

**남은 후속 과제(Tasklist.md에 기록)**: `StickmanAgent`에 `IsFullscreenAppActive` 테스트 주입구 없음
(리플렉션으로 우회 중) / 큰 Dock 등반 애니메이션 속도가 높이에 비례하지 않음 / `LandingCrouchState`의
"무릎앉아 금지" 전제가 큰 tilesize에서 성립하지 않음(거동 자체는 정상이라 보류).

## 2026-08-30 (계속) — 외부 디자인 핸드오프 32종 장비 + 다크 리스킨 + 라이벌 삭제 + Dock 근본수정 (커밋 b6755f4)
사용자가 외부 디자인 핸드오프(`design_handoff_character_sheet`)를 주고 "캐릭터 정보창을 이렇게
디자인해줘"라고 요청 — 4부위×1아이템 바이너리 장비 시스템을 8카테고리×4아이템(32종) 선택식으로
전면 확장하기로 사용자가 직접 범위를 확정했다.

- **데이터 모델(코더) + 시각 설계(ux-designer) 병렬 착수**: 핸드오프의 평면 SVG 목업을 실제 물리
  리그 캐릭터 좌표계로 번역하는 설계 문서(UX_FLOW 33절, ~660줄)와 8슬롯 데이터 모델(저장 v5)이
  동시에 나왔다. 레벨 곡선 지수(1.15→1.05)를 리더가 직접 재계산·적용해 최고 요구 레벨(24)의
  현실적 도달 시간을 458h→350h로 완화.
- **레이아웃+액세서리 2차 병렬**: 880px 정보창 재구성과 32종 캐릭터 부착 도형 구현을 병행. 통합
  과정에서 부채꼴 메뉴 위젯이 캐릭터 루트에 "Head"란 이름의 UI 자손을 만들어 포즈 애니메이터가
  진짜 머리로 착각, 캐릭터가 영구 고정되던 실제 회귀를 발견·이중 방어로 수정.
- **R2/R3/R4 통합 검증 3라운드**: 매 라운드 test-engineer가 실제 배치 실행으로 Blocker~Minor를
  잡아냈다 — 라이벌 정리 목록 미배선(프리팹 고아 스크립트 참조), Dock 스텝업 테스트의 실제 기하
  근접 충돌(0.005유닛 여유), sortingOrder 역전으로 인한 UI 겹침 등. **"컴파일 통과 ≠ 화면에 실제로
  나옴"이 이 라운드에서도 5회 이상 반복** — 매번 실측으로만 잡혔다.
- **사용자 실사용 피드백 라운드(빌드 후 직접 확인)**: 다크 글로스 레퍼런스 사진 3장을 근거로 전체
  UI 톤 재요청, 색상 미적용/초상화 FX·펫 미리보기 누락/표정 구분 불가(→ 전체 삭제 결정)/넥타이
  위치/모자 투명(→ 이 프로젝트 최초의 채움 렌더링 도입)/망토 실루엣/캐릭터창 겹침+드래그 불가 등
  7건을 실제 빌드로 확인해가며 순차 수정. **Dock 되올라오기 버그의 진짜 원인**(디버거가 리더의
  1차 진단을 실측으로 재정정 — Dock 지오메트리가 아니라 캐릭터 머리 콜라이더 반경이 지배 형상)도
  이 흐름에서 재확정됐고, 배포 기본 설정이 이미 그 문턱 안쪽이었음을 확인했다.
- **라이벌 기능 전체 삭제**(사용자 지시): 자동 소환/대결/전용 단축키 제거. 격파 미니게임/가출
  기능은 이름만 비슷할 뿐 무관해 보존 — 범위 오인 방지를 위해 사전에 코드로 경계를 확정한 뒤 착수.
- **최종 통합(R4)**: Blocker 0 / Major 0 / Minor 8, "커밋해도 안전하다"는 명시적 승인 + m1(채움
  렌더링 회귀 테스트 0건)만 커밋 전 편입 권고 → 리더가 직접 정식 테스트로 승격 후 커밋.
- **최종 상태**: 컴파일 0/0, EditMode 143/143, PlayMode 253/253.

**다음 예약 작업(사용자 지시)**: 윈도우 지원 착수 — `Win32WindowService.cs`는 창 열거 골격만 있고
BUG-B1(진짜 분리된 오버레이 HWND 없음)이 핵심 블로커. 이 개발 환경(macOS)에서는 컴파일까지만
검증 가능하고 실제 Windows OS 실동작 검증은 불가함을 사용자에게 사전 고지함.

## 2026-08-30 (계속) — 윈도우 지원 1차 구현 (커밋 ddb1231)
사용자가 "맥/윈도우 두 개 다 되냐"고 물어 현황(맥만 완성, 윈도우는 BUG-B1로 항상위가 안전가드에
막혀있음)을 정직하게 보고한 뒤, "지금 작업 끝나면 바로 윈도우도 진행해달라"는 지시로 착수.

- 조사 결과 UniWindowController 패키지가 애초에 Windows/Mac 양쪽을 네이티브로 지원한다는 것을
  패키지 실물(Windows용 LibUniWinC.dll 동봉, P/Invoke 44개 전부 플랫폼 분기 없음)로 확인 —
  Win32를 직접 구현하는 대신 이미 검증된 경로로 통일해 중복 구현을 피했다.
- 이 환경(macOS)에 마침 Windows 빌드 모듈이 있어서 실제 `.exe`까지 만들어 산출물 내용물(플랫폼별
  DLL 구성)을 검증했다 — 컴파일 확인 수준을 넘어선 성과. 다만 실제 Windows OS 런타임 동작은
  이 환경에서 검증 불가함을 명시하고, 첫 실행 시 확인할 항목을 Tasklist.md에 남겼다.
- 부수 효과: Win32WindowService의 쓰기 계열 호출이 전부 사라져 원칙 3 감사 대상이 오히려 줄었고,
  리더가 직전에 편입한 채움 렌더링 테스트의 -nographics 크래시 버그도 같이 잡혔다.
- 맥 빌드/테스트 무회귀: 컴파일 0/0, EditMode 143/143, PlayMode 257/257.

**남은 것**: 실제 Windows 머신에서의 최초 실행 검증(사용자 몫), 단축키 안내 문구의 플랫폼별 표기.

## 2026-08-31 — 호버 패널+다이얼 + GETUP/펫/모자 근본수정 + 윈도우 버그 2건 (커밋 0dd904f)
어제 설계만 끝내둔 왼쪽 아래 호버 패널(캐릭터 크기 다이얼 + 미리보기 카드)을 구현하면서, 그
전제조건이었던 `parkourMantleInset` 유도값 전환까지 함께 처리했다. 이어서 사용자가 실제 macOS
빌드로 라이브 테스트하며 신고한 버그들(GETUP 바닥 관통, 창 최대화 시 펫 어긋남, 회전시 모자
사라짐)을 순차 해결했고, 사용자가 실제 Windows PC에서 처음 실행하며 신고한 버그 4건 중 2건
(작업표시줄 침범/저해상도)을 근본 해결했다.

- **최대 수확**: 통합검증 라운드에서 "캐릭터 크기 변경이 배포용 공유 설정 에셋을 직접 오염시키는"
  구조적 결함을 발견 — 오늘 하루 모든 테스트가 알게 모르게 0.35배로 왜곡되어 돌고 있었다는 사실이
  드러났다(PlayMode 실패 9건). 리더가 제안한 해법(런타임 복제본)을 코더가 실측으로 기각하고
  (다른 15개 소비처가 진실이 둘로 갈라짐을 발견) 더 안전한 해법(`[NonSerialized]` 필드 분리)을
  채택 — 리더 제안을 맹신하지 않고 검증하는 이 프로젝트의 문화가 이번에도 작동했다.
- 다른 세션(사용자가 리모트로 접속 중인 것으로 보이는 세션)과 동시에 같은 저장소를 다루면서
  원인불명 git reset을 한 번 겪었고, 원격 README 수정과의 병합도 한 번 처리했다 — 여러 세션이
  같은 저장소를 동시에 다룰 수 있다는 걸 이번에 처음 확인했다.
- **최종 상태**: 컴파일 0/0, EditMode 150/150, PlayMode 295/295(1 스킵). GitHub에 커밋+푸시 완료.

**남은 것**: 윈도우 창 겹침/렉 원인 미확정(다음 라운드), 같은 종류의 설정에셋 오염 결함 3곳 잔존
(inkColor 등, 저장 자동복원 경로 없어 위험 낮음), 사용자가 전달한 대규모 신규 기획서 2건(기획
정리 문서 + 5인 페르소나 아이디어 검토) 처리 예정.

## 2026-08-31 (계속) — 실사용 버그 5건 + 성능/원칙2 대개선 (커밋 c256a58)
사용자가 실제로 앱을 켜놓고 쓰면서 하루 종일 버그를 신고했고, 그때그때 병렬로 조사·수정한 8개
라운드를 하나로 통합했다. 사용자가 명시적으로 "능동적으로 테스트할 인원을 추가하라"고 요청해
반응형(신고받고 고치기)에서 능동 탐색 테스트 병행으로 전환한 것이 특징이다.

- **가려진 창 발판 오인식**: 2026-08-28에 이미 macOS에서 고친 버그가 macOS 전용 파일 안에 갇혀
  Windows로 전파된 적이 없었던 "플랫폼 패리티 회귀" — `VisibleTopEdgeSolver.cs`로 로직을 플랫폼
  중립화해 재발 구조 자체를 봉쇄.
- **매달리기 손 위치 + 걷기 문워크**: 둘 다 "로컬 유닛을 월드 유닛으로 착각"하는 같은 계열 버그.
  기본 배율(0.75)에서 우연히 숨어있다가 사용자의 실제 저장 배율(0.35)에서 정확히 재현됐다 — 오늘
  아침 고친 "배율이 배포 에셋을 오염시키는" 버그와 한 뿌리에서 나온 연쇄 발견들.
- **렌더러 등록 구조 근본수정**: 전체화면 감지 시 몸은 사라지는데 런타임 생성된 액세서리/펫/이펙트는
  0.25초(실측으론 감지 프레임에 100% 노출) 남아있던 **원칙 2 위반**을 발견·수정. 액세서리 획 두께가
  출하 기본 배율에서도 이미 최소 가독 기준 미달이었던 것과, 그걸 검사하는 척하던 테스트가 실은 캐시
  무효화로 몸만 검사하고 있었다는 것도 함께 해결.
- **성능/전력 — 이번 세션 최대 발견**: macOS 실측 결과 사용자가 보고한 수치(185MB/1.5%)는 실제
  이 macOS 빌드 수치가 아니었고(직접 측정 543MB/28%), Windows 작업관리자 수치였을 가능성이 확인됐다.
  원인은 이 2D 게임이 절대 안 쓰는 깊이/스텐실 버퍼(121MB)와 콘텐츠 0개인데 24시간 무음을 믹싱하는
  오디오 엔진. 더 크게는 **이 앱이 Unity 엔진 기본값 때문에 사용자 디스플레이가 24시간 절전에
  못 들어가게 막고 있었다는 원칙 2 위반**을 발견 — CPU 절감분과는 자릿수가 다른 전력 낭비였다.
  "보는 사람이 있는지"에 따라 자동으로 프레임레이트를 낮추는 4단계 적응형 시스템(위상고정 유지하며
  최대 84% 절감)을 구축했다. 사용자가 직접 검증(암페타민 앱 실행 여부)해 "체감 원인은 아니었을 수
  있다"고 정정했지만, 수정 자체는 일반 사용자에겐 여전히 유효한 개선으로 유지했다.
- **윈도우 BitBlt 잔상/렉**: 리더의 근본 해법 제안(D3D12/Vulkan 전환)이 Unity 공식 문서로 완전히
  막혀있음을 확인 — 투명 레이어드 창의 구조적 요구사항이지 설정 실수가 아니었다. 유일하게 통제
  가능한 변수(프레임 제출 횟수)를 줄이는 방향으로 대응, 정직하게 미확정 부분을 남겼다.
- 최종 통합검증에서 신규 코드의 실제 버그 2건(클램셸 모드 디스플레이 ID 캐시 고착, 원칙2 수정의
  회귀 잠금 누락)을 잡아 즉시 수정 완료.
- **최종 상태**: 컴파일 0/0, EditMode 178/178, PlayMode 313/314(1 기존 스킵), macOS/Windows 빌드
  둘 다 성공.

**다음**: 사용자가 전달한 대규모 신규 기획서 2건(정리 문서 + 5인 페르소나 아이디어 검토) 처리,
소형 창 프로토타입(성능 개선 후보, 별도 Phase로 승인됨), Windows 알파 필터 격차 등 이월 항목.

---

## 2026-09-01 — 릴리즈 라운드 (Windows 미리보기 20260901)

리더가 병렬 라운드 20여 건을 지휘. 커밋 `767c985`(본작업) + `7230099`(문서 용어) 푸시,
GitHub 릴리즈 `windows-preview-20260901` 발행.

### 사용자 신고 버그 3건 해결
- **전체화면 엑셀에서 캐릭터가 사라짐** — 지난 라운드에 macOS만 고쳐졌고 **정작 사용자가 신고한
  Windows는 기하 판정만 쓰고 있었다.** Windows 패리티 감사에서야 드러났고, 이대로 릴리즈될
  뻔했다. 게임바 등록 목록(읽기 전용 레지스트리)을 근거로 게임일 때만 숨기도록 macOS와 대칭
  구조로 구현. → **교훈을 CLAUDE.md 협업 프로토콜에 규칙으로 못박음**(아래).
- **창에서 갑자기 떨어짐** — 접지 중에도 중력이 유지돼 프레임이 한 번만 길게 튀면(절전 250ms)
  허용오차를 넘었다. "유예만 늘리면 되지 않느냐"는 초기 가정은 담당자가 계산으로 반증
  (폴링 한 주기 낙하 1.32유닛 > 허용 0.489 → 돌아올 방법 없음) — 몸을 붙잡아야 했다.
- **떨어질 때 망토가 안 펄럭임** — 조사 지시의 용의선 4건이 전부 반증되고, 진짜 원인은
  **테스트 표본 창이 0.11초**였다. 대신 조사 중 실제 결함(착지 한 프레임에 밑단이 획 7배
  순간이동)을 찾아 수정.

### 이번 세션의 가장 큰 방법론적 발견 3가지
1. **"프레임수 = 시간" 함정** — 배치모드가 실측 **9,300~13,200fps**라 180프레임 예산이 실제로는
   0.014초였다. 전수 점검에서 결함 8건 적발했는데 **상당수가 통과하면서 아무것도 측정하지 않던
   거짓 통과**였다(망토 채움: 흔들림 위상의 1.0%만 샘플 = 정지 화면 한 장). 공용 시간 도구 +
   소스 스캔 린트(자기 자신의 네거티브 컨트롤 포함) 2겹으로 재발 방지.
2. **자를 먼저 의심하라** — "지표가 원리적으로 못 본다"던 과거 기록이 오진이었다. 지표가
   정점만/상반구만 샘플링해 값을 3.77W로 부풀려 읽고 있었다(실제 0.58W).
3. **★ 수치가 통과시킨 것을 눈이 잡았다** — 액세서리 라운드 4회가 전부 초록불이었는데, 리더가
   직접 빌드/실행/캡처해 보니 **카드가 이름대로 안 읽혔다**(선글라스=화살표, 날개=나뭇잎,
   목도리=장화). 원인: 오늘 쓴 지표는 "두 도형이 서로 다른가"와 "획보다 두꺼운가"뿐이었고
   **"그 이름의 물건으로 보이는가"는 잰 적이 없다.** → 도형 변경 라운드는 실물 캡처 육안 확인
   동반 의무화.

### 신규 기능
설정창 신설 / 장비창 카드 캐러셀 + 신규 14종 / 상체 기울임(`SetBodyLean`) / 발판 상실 시
`GroundLossHang` 상태 신설 + 허둥대는 연출 / FX·PET 4종 실제 연출 / 눈 제거 + 머리 채움 /
EYES 6종 바이저 리디자인 / 저장 스키마 v8 + 다운그레이드 방어.

### CLAUDE.md에 새로 못박은 규칙
- **플랫폼 동시 검토** — macOS를 고치면 그 라운드 안에서 Windows도 검토. 완료 보고에
  "Windows 영향: 없음/함께 수정함/별도 배정 필요(사유)" 필수. 신고에 플랫폼 단서가 있으면
  그 플랫폼부터. 정책은 플랫폼 중립 위치에(실제로 정책 파일이 `Platform/MacOS/` 안에 갇혀
  Windows가 호출조차 못 하던 사고가 있었다).
- **테스트에 프로덕션 상수를 숫자로 베끼지 않는다** (오늘 이 패턴 잔존 버그 4건).
- **시간 기반 PlayMode 테스트는 반드시 벽시계 기준.**
- **페르소나 검증단 3명**(민지/재현/소은) 신설 — 기능 변경마다 병렬 호출, 리더가 선별 전달.

### 페르소나 검증단이 실제로 잡은 것
- **민지**: 캐러셀 콘텐츠 1/3이 도달 불가(단서 0) / 내부 디버그 문자열 화면 노출 /
  설정창 열면 캐릭터창이 사라지고 복귀 불가 / 톱니 4바퀴 회전이 앨리어싱으로 안 보임
- **재현**: 저장 다운그레이드 방어가 부팅 1회뿐이라 **구버전 상주 인스턴스가 15초 만에
  신버전 저장을 조용히 덮어씀**(실측) / 팝오버 방치 시 클릭 차단막이 밤새 남음(원칙2)
- **소은**: 창 알파를 낮춰 "유예 구간과 렌더링이 동일한 상태"를 재현하는 실험을 고안,
  **IDLE 정지 = 앱이 멈춘 것처럼 보이고 WALK 정지 = 코요테 개그로 읽힌다**를 통제 비교로 증명
  → "0.45초가 길다"가 아니라 "생명 신호가 없다"가 문제임을 확정

### 리더 자신의 프로세스 결함 2건 (기록)
- Tasklist에 "파일이 비었다"고 적고 **바로 그 파일을 편집할 라운드를 배정하고도 기록을 안 고쳤다.**
  다른 에이전트가 mtime을 직접 재서 동시 편집을 막았다. → 소유권 서술에 측정 근거 동반 의무화.
- 에이전트 간에 단일 측정값(0.04094)을 "개선됐다"고 중계했는데 **실은 잡음**이었다(5회 반복 시
  -0.02~+0.06). 받은 에이전트가 스스로 검증해서 막았다.

### 미해결 / 다음
- **[진행 중]** 카드 아이콘이 이름대로 안 읽히는 문제 수정
- **[릴리즈 후 큐]** 구석 크기 다이얼 제거 → 설정창 슬라이더 일원화 / OS별 단축키 적합성 확인
  (Windows가 `Ctrl+Alt+Win` 조합인데 폴링 방식이라 입력을 가로채지 않아 활성 앱에도 전달된다)
- **[사용자 실기 확인 대기]** 전체화면 게임 판정, 단축키 라벨 길이(4자 → 14자) 레이아웃
- **[백로그]** 털모자 띠/목도리 획 예산, FX 색 정책(P5), 가상 데스크톱 추종(비공개 API),
  획 두께 이중 정의(P6 — `Stroke`를 몸 획으로 재정의하면 미리보기 9종이 함께 두꺼워지니 주의)
