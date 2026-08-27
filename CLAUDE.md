# StickMate — 프로젝트 컨벤션 (CLAUDE.md)

바탕화면에서 돌아다니는 졸라맨(스틱맨) 데스크톱 오버레이 앱. Unity 6 LTS 기반.

## 필독 문서
- `docs/ARCHITECTURE.md` — 설계 요약, 기술 스택, 무빙 방식 결정 근거
- `docs/UX_FLOW.md` — UX Designer 산출물 (화면/상태 흐름)
- `Tasklist.md` — 팀 공유 진행상황 트래커 (항상 최신 유지)
- `process.md` — 리더가 단계별로 남기는 진행 로그

## 절대 불변 원칙
1. **행동-텍스트 싱크**: 말풍선 대사는 상태 전이가 확정된 뒤 그 상태로부터만 파생. 대사 먼저 정하고 행동을 끼워 맞추지 않는다.
2. **비침해**: 클릭 관통 기본 ON, 전체화면 게임 감지 시 자동 숨김.
3. **유저 자산 불변**: 실제 파일/아이콘/타 윈도우는 절대 이동·삭제·수정하지 않는다. 전부 읽기 전용 열거 + 시각적 복사본 연출.
4. **플러그인 구조**: 신규 모션/이펙트(DLC)는 기본 로직 무수정으로 ScriptableObject 매니페스트를 통해 추가.

## 캐릭터 무빙 방식 (결정됨, 변경 시 팀 합의 필요)
Active Ragdoll(Rigidbody2D + Joint2D) + IK 하이브리드.
- 능동 상태(IDLE/WALK/JUMP/PARKOUR_CLIMB/ATTACK): 모터/IK가 목표 포즈로 힘을 가함
- RAGDOLL 상태(피격/던짐/추락 충격): 전신 물리에 위임 → 감속 후 GETUP으로 자동 복귀
- 근거: docs/ARCHITECTURE.md 0절

## 팀 구성 (.claude/agents/)
- `ux-designer` — UX 플로우, 와이어프레임, 예외 상태
- 리더(현재 세션) — Architect: 통합/최종 판단, Phase 게이트 승인, process.md 갱신 + 커밋
- `coder` — Teammate1: 실제 구현
- `debugger` — Teammate2: 버그 리포트, 원인불명 버그는 가설 기반 과학적 토론
- `test-engineer` — Teammate3: 테스트 + 최종 리뷰어 겸임 (개선점 있으면 Architect로 반려)
- `perf-doc` — Teammate4: 성능 최적화(요청 시) + 문서화(최종 단계)

## 협업 프로토콜
- 교차 레이어 영향은 `Tasklist.md`의 "교차 레이어 영향 로그"에 즉시 기록
- 원인 불명 버그는 "과학적 토론 로그"에 가설/검증방법/결과/결론 기록
- Phase 완료마다 리더가 process.md 갱신 + git commit

## 환경 메모
- 개발 환경에 tmux/Homebrew/Unity가 설치되어 있지 않을 수 있음 — 팀원 에이전트는 Unity 프로젝트 파일(.cs, .asset, .meta 등)을 소스 형태로 작성하고, 실제 빌드/에디터 실행은 Unity 설치 후 사용자가 수행.
