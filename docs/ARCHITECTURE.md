# StickMate — 설계 요약 (Architect 산출물)
> 작성: 팀 리더(아키텍트) · 최종 갱신: 2026-08-27

## 0. 리서치 기반 핵심 기술 결정: 캐릭터 무빙 방식
[리서치] 스프라이트 워크사이클(Pivot Animator류 8프레임 순환) vs **Active Ragdoll 절차적 애니메이션**(Rigidbody2D+Joint2D, IK로 힘을 가해 포즈를 따라가게 하는 방식) 비교 검색 결과:
- 순수 스프라이트 워크사이클: 걷기/공격은 쉽지만, 기획서의 "커서로 잡고 던지기", "창에 부딪혀 튕김", "낙하 구르기", "해머에 맞아 나가떨어짐" 같은 **물리 반응형 이벤트를 자연스럽게 표현 불가** (미리 그린 애니메이션 클립으로만 대응 → 튕기는 방향/속도가 매번 달라 부자연스러움).
- Active Ragdoll: Rigidbody2D 관절 구조에 목표 포즈로의 토크를 가해 평소엔 "걷는 것처럼" 보이되, 외력(던짐/피격/낙하)이 가해지면 그대로 물리에 맡겨 자연스럽게 널브러지고, 힘이 잦아들면 다시 일어나 걷는 상태로 복귀.
- **결정: 하이브리드.** 상태 머신이 `IDLE/WALK/JUMP/FALL/PARKOUR_CLIMB/ATTACK` 등 "능동 상태"일 때는 IK/모터로 포즈 제어, `RAGDOLL`(피격/던짐/낙하 충격) 상태로 전이되면 전신 물리에 위임하고 일정 속도 이하로 감속되면 자동으로 `GETUP` → 능동 상태로 복귀.
- 근거 링크:
  - [Pivot Animator 스틱맨 애니메이션 가이드](https://www.vidnoz.com/ai-solutions/stickman-walking.html)
  - [Alan Zucconi — Procedural Animation 개론](https://www.alanzucconi.com/2017/04/17/procedural-animations/)
  - [Sergio Abreu — Unity Active Ragdoll 제작기](https://sergioabreu-g.medium.com/how-to-make-active-ragdolls-in-unity-35347dcb952d)
  - [2D Stickman Active Ragdoll (Unity, 유튜브 튜토리얼)](https://www.youtube.com/watch?v=q_enFap8Pr8)
  - [2D Ragdoll 걷기 미니 튜토리얼](https://www.youtube.com/watch?v=u3Hkqlq4OVM)

## 1. 핵심 기능 (기획서 매핑)
| 영역 | 기능 | 우선순위 |
|---|---|---|
| 코어 | 윈도우=지형 인식, 중력/발판/화면이탈 낙하 | P0 |
| 코어 | 상태 머신: IDLE/WALK/JUMP/FALL/PARKOUR_CLIMB/ATTACK/RAGDOLL/GETUP | P0 |
| 코어 | 텍스트-액션 싱크 계약 (말풍선은 상태에서 파생, 역방향 금지) | P0 |
| 비침해 | 클릭 관통 기본 ON / 대결모드 시 전환 | P0 |
| 비침해 | 전체화면 게임 감지 → 자동 숨김 | P0 |
| 전투 | 격파 미니게임(기 모으기+타이밍 클릭) | P1 |
| 전투 | 라이벌 스틱맨 AI 추적/공격 | P1 |
| OS 장난 | 창 끌기 시늉/아이콘 정렬 시늉/그라피티 (전부 read-only 시각 연출) | P1 |
| 파괴효과 | 윈도우 크래시(3초 원복)/블랙홀 | P2 |
| 커서 상호작용 | 로데오 커서/커서 공격/드래그&던지기 | P1 |
| PC연동 | CPU/배터리/충전/네트워크 상태 반응 | P2 |
| 보스레이드 | 장시간 방치 시 저확률 이벤트 | P3 |
| 생산성 | 투두 말풍선, 포모도로 감시자 | P2 |
| 육성 | 던전 파밍, 세포분열/군대 | P3 |
| 반항/스트레스 | 스트레스 게이지, 가출, 창 점령 | P2 |
| 악동 | 인질극(닫기버튼 막기), 해킹 요구 | P2 |
| 드로잉 | 스케치북 무기 제작 | P3 |
| 인벤토리 | 파일 먹이기(read-only), 폴더 보물찾기 | P3 |
| 멀티 | Steam P2P 차원이동 | P3 (스코프 아웃 후보, 별도 논의) |
| 수익화 | 스킨/DLC 프리뷰, 업적 | P2 |

## 2. 기술 스택
- **엔진**: Unity 6 LTS, URP 2D, Rigidbody2D + Joint2D 기반 Active Ragdoll
- **플랫폼 오버레이**: `IPlatformWindowService` 인터페이스
  - Windows: Win32 P/Invoke (`SetWindowLong` WS_EX_LAYERED/WS_EX_TRANSPARENT, `EnumWindows`, `DwmGetWindowAttribute`)
  - macOS: Objective-C++ 네이티브 플러그인 (`NSWindow` 오버레이, `CGWindowListCopyWindowInfo`)
  - 에디터/미지원 플랫폼: `NullPlatformWindowService` 폴백
- **DLC/플러그인**: Addressables + ScriptableObject 매니페스트 (`MotionPluginSO`, `EffectPluginSO`) — 기본 로직 무수정으로 추가
- **상태 관리**: `IStickmanState` 명시적 상태 패턴 + 이벤트 버스(`StickmanEventBus`)로 레이어 간(입력/렌더/네이티브/AI) 통신
- **텍스트-액션 계약**: `DialogueIntent`는 상태 전이가 확정된 프레임에만 생성되고, 상태가 중도 취소되면 같은 프레임에 말풍선도 취소됨 (단일 소스)

## 3. 절대 불변 원칙 (전 팀원 공유)
1. 행동과 텍스트는 항상 일치 (상태 확정 → 대사 파생, 역방향 금지)
2. 클릭 관통 기본 ON, 전체화면 게임 감지 시 자동 숨김
3. 유저 실제 파일/아이콘/창은 절대 변경 안 함 — 전부 읽기 전용 열거 + 시각적 복사본
4. 이펙트/모션은 플러그인 구조로 기본 로직 무수정 확장

## 4. 구현 단계 (Phase)
- **Phase 0**: Unity 프로젝트 스캐폴딩, 플랫폼 서비스 인터페이스, 이벤트 버스, 상태머신 골격
- **Phase 1**: 코어 루프 — 중력/발판인식/화면이탈, IDLE/WALK/JUMP/FALL, 클릭관통, 전체화면 감지
- **Phase 2**: Active Ragdoll 전환(RAGDOLL/GETUP), 파쿠르(PARKOUR_CLIMB), 텍스트-액션 계약 시스템
- **Phase 3**: 전투(격파/라이벌 AI), 커서 상호작용(드래그&던지기, 로데오)
- **Phase 4**: OS 장난(창 끌기/그라피티/블랙홀/크래시), PC 하드웨어 연동
- **Phase 5**: 생산성/반항·스트레스/육성 요소, DLC 매니페스트 실제 콘텐츠
- **Phase 6**: 최적화, 문서화, 리뷰 사이클 마감

각 Phase는 UX → (Architect 승인) → Coder → Debugger → Test/Review → (필요시 Perf) 순으로 순환하며, `Tasklist.md`에 진행상황을 기록한다.
