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
