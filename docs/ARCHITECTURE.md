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

## 0-1. 플랫폼 전략: macOS / Windows / iPad / iPhone 동시 지원
iOS/iPadOS는 앱 샌드박스 정책상 "다른 앱 창 열거"·"시스템 전역 오버레이"·"타 앱 위 클릭 관통"이 **원천적으로 불가능**하다(공개 API 없음, 탈옥 전제 불가). 따라서 데스크톱(macOS/Windows)과 모바일(iPad/iPhone)은 같은 상태머신/이펙트 코드를 공유하되 **지형 인식 방식만 플랫폼별로 교체**한다.

- **데스크톱(macOS/Windows)**: `IPlatformWindowService` 실구현이 실시간으로 타 윈도우를 열거해 상단 Y좌표를 발판으로 사용 (기존 설계 그대로).
- **모바일(iPad/iPhone) — "스크린샷 백드롭 모드"** (사용자 확정 결정, 2026-08-27):
  1. 앱 최초 실행 시 유저가 홈 화면을 직접 캡처한 스크린샷을 앱에 불러오거나(사진첩에서 선택), 앱이 사진첩의 최근 스크린샷을 자동 감지해 제안한다.
  2. 그 스크린샷을 전체화면 정적 배경 이미지로 표시 — **실제 홈 화면이 아니라 그 위에 올린 착시**이며, 앱을 나가면 실제 홈 화면으로 돌아가는 일반 앱임을 최초 실행 시 1회 명확히 안내한다 (기대치 오해 방지, UX Designer 담당).
  3. 아이콘 행(그리드)과 하단 Dock 영역의 좌표를 "발판(Platform)"으로 사용. 1차는 유저가 손가락으로 아이콘 줄 위치를 탭해 지정(간단·정확), 2차 고도화로 이미지 상 아이콘 그리드 자동 감지(균등 그리드 휴리스틱) 도입 가능.
  4. 이 모드는 `IPlatformWindowService`의 별도 구현체 `ScreenshotBackdropPlatformService`로 캡슐화 — 상태머신/이펙트/전투 로직은 데스크톱과 100% 동일 코드 사용.
  5. 클릭관통 개념은 모바일에 없음(오버레이가 아니라 이 앱 자체가 포그라운드 앱이므로) — 대신 유저가 탭하면 캐릭터가 반응하는 상호작용이 기본.
- 4개 플랫폼 모두 같은 Unity 프로젝트, 플랫폼별 `IPlatformWindowService` 구현만 스왑.

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
- **엔진**: Unity 6 LTS, **Built-in RP**(2026-08-28 Architect 확정 — 초기 설계는 URP 2D를 전제했으나 씬/렌더링 작업이 아직 없어 URP 특화 기능에 의존하는 코드가 전무함을 확인, 불필요한 패키지 의존 추가를 피하고 현재 프로젝트 실제 상태를 공식 기준으로 채택. 향후 실제 비주얼/셰이더 작업 착수 시 URP 전환은 Package Manager 설치 한 번으로 가능하며 기존 C# 게임로직에 영향 없음), Rigidbody2D + Joint2D 기반 Active Ragdoll
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

---

# 5. 신규 기획서 2건 — 엔지니어링/인프라 + 콘텐츠 기술 실현성 계획
> 작성: Coder(Teammate1) · 2026-08-31 · **계획 단계(코드 미작성)**
>
> 대상 문서: (1) 「StickMate 기획 정리 (개발 착수용)」, (2) 「StickMate 아이디어 검토 — 5인 페르소나
> 10라운드 회의록」. UX 담당 영역(온보딩 화면 흐름 / 설정창 / 커스터마이징 UX)은 `docs/UX_FLOW.md`에
> ux-designer가 병행 작성 중이며 이 절은 그와 겹치지 않는 **기술 설계**만 다룬다.
>
> 이 절의 모든 판단은 추정이 아니라 **오늘자 코드베이스 실제 확인(grep/read)** 에 근거한다. 확인한
> 사실은 각 항목에 파일 경로와 함께 적었다.

## 5-0. 먼저 보고할 3가지 (리더 판단 필요)

1. **절대 불변 원칙 4(플러그인 구조)는 현재 "선언만 되어 있고 구현되어 있지 않다."**
   `Assets/_Project/Scripts/Plugins/MotionPluginSO.cs`(23줄) / `EffectPluginSO.cs`(26줄)는 Phase 0의
   빈 껍데기이고 **프로젝트 전체 참조가 0건**이다(`Assets/` 전수 grep, Plugins 폴더 자신 제외 0건).
   반면 실제로 출하된 콘텐츠 32종은 `Core/ItemCatalog.cs`의 하드코딩 `new Row(...)` 28개와
   `Interaction/AccessoryShapeBuilder.cs`(1,225줄)의 `switch (itemIndex)` 6개 블록으로 되어 있다.
   `Assets/_Project/Data/`에 존재하는 ScriptableObject 에셋은 `DefaultStickConfig.asset` **단 하나**다.
   → **오늘 DLC 팩 하나를 추가하려면 기본 로직 두 파일을 반드시 수정해야 한다 = 원칙 4 위반.**
   확정 6종 DLC를 전제한 수익 계획 전체가 이 위에 서 있으므로 최우선 구조 과제로 본다(5-3절).
2. **"Unity Job System 멀티스레드 최적화"(기획서 2, 3절)는 반려를 권고한다.**
   오늘 perf-doc 라운드의 `sample` 실측 결론이 `Tasklist.md`에 남아 있다 — **관리(C#) 코드는 메인
   스레드 표본의 0.25%**이고 나머지는 렌더/합성 대기다. Job System은 그 0.25%를 나누는 도구라
   최선의 경우에도 절감이 0.25% 미만이다. 같은 라운드에서 실제 효과가 큰 축(표면적/프레임 제출
   횟수)은 이미 `Platform/FramePacing.cs` + `Platform/ViewerPresence.cs`로 처리됐다. 상세 5-1-1.
3. **"비활성 창 흔들기"(기획서 1, 5절)는 문자 그대로 구현할 수 없다.** 창을 실제로 흔들면 원칙 3
   위반이고, 창 픽셀을 떠서 흔드는 척하려면 화면 캡처 경로를 신설해야 하는데 그건 프라이버시 표면
   신설 + 오늘 Windows에서 싸우고 있는 BitBlt 비용 증가다. 축소 스펙(창은 안 움직이고 **캐릭터만**
   낑낑대고 만화식 동선만 튄다)을 권고한다. 상세 5-2-4.

---

## 5-1. 엔지니어링/인프라 (기획서 2 · 3절 채택 항목)

### 5-1-1. Job System / 성능 — **오늘 라운드와 중복. 반려 권고**
- **(a) 코드베이스 확인**: `Platform/FramePacing.cs`(633줄)가 프레임 페이싱 단일 진입점이며 macOS는
  `vSyncCount`(위상 고정), Windows는 `targetFrameRate`(sleep)라는 **의도된 비대칭**을 문서와 함께
  갖고 있다. `Platform/ViewerPresence.cs`는 `IViewerPresenceService` + `ViewerPresenceSnapshot`
  (DisplayAsleep / SecondsSinceUserInput / LowPowerMode / OnBattery) + 순수 판단부
  `FramePacingPolicy`(4등급 Active/Idle/Away/DisplayOff)를 분리해 담고 있고, EditMode 회귀
  13건(`AdaptiveFramePacingPolicyTests`)이 숫자를 잠그고 있다. 디스플레이 절전 방해(원칙 2 위반)도
  같은 라운드에 수정 + 회귀 잠금 완료.
- **(b) 난이도**: (도입 시) 큼. **(효과) 0.25% 미만.**
- **판단**: 기획서 2의 문구 "기존 물리 업데이트 타이밍 이슈와 함께 처리 권장"에서 말하는 물리 타이밍
  이슈는 오늘 이미 별도로 해결됐다(커밋 `b014611` 착지 1프레임 desync, `0dd904f` GETUP 발판 관통).
  Job System 도입 명분이 남아있지 않다.
- **다만 하나는 살릴 값이 있다** — perf-doc이 "측정만 하고 손대지 않은" 항목:
  Job 워커 스레드 27개(`Job.Worker` 9 + `Background Job.Worker` 9 + `AssetGarbageCollectorHelper` 9)가
  전부 세마포어 대기 상태로 스택만 수십 MB를 쓴다. `JobsUtility.JobWorkerCount` 축소로 -20~30MB 추정.
  **전제 검증 필수**: Unity 6의 2D 물리가 잡 워커를 쓰는지 실측 후에만. 이건 Job System "도입"이
  아니라 "축소"이므로 위 반려와 모순되지 않는다. 난이도 작음.
- **(c) 원칙 충돌**: 없음. **(d) 열린 이슈**: 없음.

### 5-1-2. 자동 업데이트 시스템
- **(a) 확인**: 이 프로젝트에는 **네트워크 코드가 한 줄도 없다**(`UnityWebRequest` / `HttpClient` /
  `Socket` / `System.Net` 전수 grep 0건. `Application.internetReachability`만 있고 그건 조회다).
  즉 자동 업데이트는 **이 앱의 첫 네트워크 호출**이 된다.
- **채널 현실**: 기획서 1의 9절 유통 표대로면 스팀 / MS 스토어 / 맥 앱스토어 **셋 다 자체 업데이터를
  갖고 있다.** 자체 업데이터가 필요한 유일한 채널은 지금 쓰고 있는 **GitHub Release 직배포**뿐이다.
- **설계**:
  - 버전 매니페스트(JSON) 1개를 Release 자산으로 올리고, 앱은 **실행 시 1회 + 24시간 1회** 이하로만
    조회. 조회 실패는 조용한 무시(오프라인 우선).
  - **자동 다운로드/자동 재시작 금지**(원칙 2). "새 버전이 있습니다 → 릴리스 페이지 열기"까지만.
    24시간 켜져 있는 앱을 우리가 임의로 재시작시키는 것은 업무 방해의 정의 그 자체다.
  - 설정에서 끌 수 있어야 하고, **기본값은 리더 판단**(개인정보 관점에서는 옵트인이 안전, 보안 관점
    에서는 옵트아웃이 안전 — 이 앱은 사용자 데이터를 다루지 않으므로 옵트아웃(기본 ON) 권고).
  - 5-1-8 오프라인 감사 화이트리스트에 **이 호출이 첫 등록 항목**이 된다. 순서상 5-1-8을 먼저 한다.
- **(b) 난이도**: 중간. **(c) 원칙 충돌**: 없음(설계대로면). **(d)**: 없음.
- **우선순위**: 유통 채널 확정 이후. 지금은 링크 알림 수준으로 충분.

### 5-1-3. 장시간 실행 메모리 누수 모니터링
- **(a) 확인**: `Platform/FramePacing.cs` 안에 `FrameTimeStats`(569줄~)가 이미 저빈도 통계 루프를
  갖고 있다. 새 MonoBehaviour `Update()`를 만들 필요가 없다.
- **핵심 판단 — 바이트 카운터보다 먼저 잡을 것이 있다.** 이 코드베이스에서 실제로 일어날 확률이
  가장 높은 누수는 힙 증가가 아니라 **정적 이벤트 구독 누수**다. `Core/StickmanEventBus.cs`(631줄)는
  static 이벤트 버스이고, 다수의 렌더러/디렉터가 `OnEnable`에서 구독 / `OnDisable`에서 해제한다
  (예: `Interaction/IdleAmbientMotionRenderer.cs`). 해제 누락이 하나라도 생기면 파괴된 캐릭터 사본이
  영구히 살아남고, 그 순간 "사본이 플레이어 신호를 받아 자기 포즈를 바꾸는" 버그로도 번진다(그
  파일 주석이 이미 그 위험을 명시하고 있다).
- **설계**:
  1. **(우선) 구독자 수 회귀 테스트**: 씬 구성 → 파괴 후 `StickmanEventBus`의 각 이벤트 구독자 수가
     기준선으로 정확히 복귀하는지 EditMode/PlayMode에서 단언. `Tests/PlayMode/GlobalPlayModeTestIsolation.cs`
     가 이미 격리 훅 자리를 갖고 있어 여기에 붙인다(현재는 `CharacterSaveStore.ResetForTesting()`만 호출).
  2. **(부가) 상주 워치독**: `Profiler.GetTotalAllocatedMemoryLong` / `GC.GetTotalMemory(false)`를
     **60초 주기·Idle 이하 등급에서만** 링버퍼 N개로 샘플링하고, 단조 증가가 N회 연속이면
     디버그 콘솔(5-1-7)에만 경고. **매 프레임 할당 금지 컨벤션 준수** — 링버퍼는 사전 할당,
     문자열은 임계 초과 시에만 조립.
- **(b) 난이도**: 작음. **(c)**: 충돌 없음. **(d)**: FramePacing 등급 시스템과 붙으므로 5-1-1의 손잡이를
  건드리지 않도록 관측 전용으로만 붙인다.

### 5-1-4. 크래시 시 마지막 상태 저장 후 자동 복구
- **(a) 확인 — 실제 결함 1건 발견**:
  `Core/CharacterSaveStore.cs:460`이 저장을 **비원자적으로** 한다.
  ```
  File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
  ```
  쓰기 도중 크래시/전원 차단이 나면 세이브가 잘린 JSON이 되어 레벨/장비/투두가 통째로 날아간다.
  나머지 방어는 이미 훌륭하다 — 스키마 `version`(현재 6), 다운그레이드 가드(`NewerVersionFileDetected`),
  마이그레이션 전 백업, 백업 실패 시 `SaveSuspended`로 저장 자체를 포기하는 보수적 정책.
  **즉 "이전 버전 파일"은 지키는데 "쓰다 만 파일"은 못 지킨다.**
- **설계**:
  1. 임시 파일에 쓰고 `File.Replace`(또는 Move+삭제) 로 교체 → 원자적 저장. **난이도 작음, 효과 큼.**
  2. 로드 시 JSON 파싱 실패 → 직전 백업으로 자동 폴백(백업 경로는 이미 존재).
  3. **"마지막 상태(포즈/위치)"는 복원하지 않기를 권고한다.** 상태 머신 도중 상태를 되살리면
     전이 세대(`DialogueIntent`의 `TransitionGeneration`)와 발판 캐시가 없는 채로 부활해 원칙 1이
     보장하는 "상태 확정 → 대사 파생" 사슬이 끊긴다. 대신 **재시작은 항상 첫 실행 낙하 인트로(5-2-2)의
     축약형으로 등장**시키는 것이 이 프로젝트의 계약과 맞고 연출로도 자연스럽다.
     복원 대상은 지금처럼 **영속 모델 데이터(레벨/장비/투두/UI 배치/배율)** 로 한정.
- **(b) 난이도**: 작음. **(c)**: 원칙 1 보호를 위해 위 3번을 반드시 지킬 것. **(d)**: 없음.

### 5-1-5. GitHub Actions 기반 플랫폼별 빌드 자동화
- **(a) 확인**: `.github/` 디렉터리가 **없다**. 반면 `Assets/Editor/BuildStandalone.cs`(503줄)에
  배치모드 진입점이 이미 완비돼 있다 — `PerformBuild()`(macOS) / `PerformBuildWindows()` +
  `ConfigureRunInBackground()` / `ConfigureAntiAliasing()` / `ConfigureResidencyFootprint()` /
  `ConfigureWindowsTransparencySettings()`. **CI는 사실상 배선 작업이다.**
- **설계(잡 3개)**:
  | 잡 | 러너 | 내용 | 비고 |
  |---|---|---|---|
  | `test` | macOS | EditMode + PlayMode 전체 | PlayMode는 그래픽 디바이스가 필요 |
  | `build-mac` | macOS | `PerformBuild` | 네이티브 플러그인 때문에 macOS 러너 필수 |
  | `build-win` | macOS(크로스) 또는 windows | `PerformBuildWindows` | 오늘 macOS에서 크로스빌드 성공 실적 있음 |
- **선결 과제 / 함정**(전부 `Tasklist.md`에 기록된 오늘의 실측):
  - Unity 라이선스 활성화 시크릿(ULF 또는 시리얼)이 리포 시크릿에 필요 — **리더/사용자 조치 필요**.
  - `-nographics`에서 `Camera.Render()` / `WaitForEndOfFrame` / `ScreenCapture`가 배치 Unity를
    반복적으로 죽였다. 확립된 우회는 오프스크린 카메라 + `RenderTexture` + `graphicsDeviceType==Null`
    가드. CI 워크플로는 이 관례를 지키는 테스트만 돌릴 것.
  - 과거 임시 PlayMode 씬이 세션 내 이후 모든 실행을 오염시킨 사례가 있다 → CI는 매 잡 클린 체크아웃.
  - `Library/` 캐시는 잡별로 커야 실행시간이 산다(이 프로젝트 `Library/`는 이미 상당한 크기).
- **(b) 난이도**: 중간. **(c)**: 충돌 없음. **(d)**: 없음.
- **평가**: **이번 주 착수 대상 1순위 후보.** 이후 모든 항목의 회귀 비용을 낮춘다.

### 5-1-6. 스크린리더 호환성
- **(a) 확인**: `Packages/manifest.json`에 `com.unity.modules.accessibility`가 들어 있으나
  프로젝트 코드에서 접근성 노드를 등록하는 곳은 없다. 창 소유권은 **UniWindowController 네이티브**에
  있다(`Packages/manifest.json`의 `com.kirurobo.uniwinc`).
- **위험 가설 2가지**(추측이므로 검증 전 수정 금지):
  1. 항상 최상단 + 클릭 관통 오버레이가 접근성 트리(AX/UIA)에 **노출되어**, 캐릭터가 걸을 때마다
     VoiceOver/내레이터가 창 변경을 다시 읽거나 포커스가 튄다.
  2. 오버레이가 스크린리더 커서의 히트테스트를 가로채 아래 앱을 못 읽게 한다.
- **설계**: 오버레이를 **접근성상 존재하지 않는 것**으로 표시한다.
  macOS는 `NSAccessibility` 상 요소가 아니도록(`accessibilityElement = NO` 계열),
  Windows는 `WS_EX_NOACTIVATE` 유지 + UIA 공급자를 노출하지 않음. 우리 앱 자체의 접근성 노드도
  등록하지 않는다(설정창/정보창이 뜬 순간만 예외 — 그건 ux-designer 설정창 계획과 접점).
  코드 위치는 `Platform/MacOS/MacOverlayStateEnforcer.cs` / `Platform/Windows/WindowsOverlayStateEnforcer.cs`.
- **(b) 난이도**: 중간(네이티브). **선결 리스크**: 창을 UniWindowController가 소유하므로 훅을 넣을 자리가
  있는지부터 확인해야 한다 — 오늘 `RegisterPowerSettingNotification`을 같은 이유로 포기한 선례가 있다.
- **(c)**: 원칙 2의 정신에 정확히 부합(방해 제로). **(d)**: 없음. **검증**: 자동화 불가 → 수동 QA 체크리스트.

### 5-1-7. 로그 레벨 조정 가능한 디버그 콘솔 (개발자 전용)
- **(a) 확인**: 이미 `StickConfig.verboseDiagnosticsLogging`(bool 1개, `StickConfig.cs:1122`)이 있고
  `MacOverlayStateEnforcer` / `RagdollImpactResolver` 등이 이를 참조한다. 즉 **2단계 스위치가 이미 있다.**
- **설계**: 그 bool을 **레벨(Off/Error/Warn/Info/Verbose) + 채널 마스크
  (Foothold/Dock/Pacing/State/Dialogue/Spectacle/Save)** 로 승격. 저장 위치는 컨벤션대로 `StickConfig`.
  화면 콘솔은 숨겨진 조합키(기존 `IGlobalKeyStateService` 채널 재사용)로만 열리고 기본 OFF, 릴리스에서도
  코드는 남되 UI 진입점은 감춘다.
- **반드시 같이 넣을 안전장치 — 이 앱 고유의 함정**: 로그 문자열을 **조립하기 전에** 게이트해야 한다.
  현재 `MacOverlayStateEnforcer.cs:227` 같은 곳은 큰 보간 문자열을 만든 뒤 넘긴다. 24시간 상주 앱에서
  `Update()` 경로에 그런 코드가 하나만 들어가도 GC 압박이 된다.
  → `Tests/EditMode/UserAssetImmutabilityAuditTests.cs`가 이미 갖고 있는 **소스 정적 스캔 프레임워크**를
  재사용해 "`Update()`/`FixedUpdate()` 안의 무가드 `Debug.Log($"..."`"를 실패로 만드는 감사 추가.
- **(b) 난이도**: 작음~중간. **(c)**: 충돌 없음. **(d)**: 없음.

### 5-1-8. 신규 기능 추가 시 "오프라인 우선 원칙" 정기 감사 프로세스
- **(a) 확인 — 지금이 이걸 할 유일한 타이밍이다**: 앞서 적었듯 네트워크 API 사용 **0건**.
  그리고 그 0건을 지킬 장치가 프로젝트에 **하나도 없다**.
  한편 원칙 3(유저 자산 불변)에는 이미 훌륭한 장치가 있다 —
  `Tests/EditMode/UserAssetImmutabilityAuditTests.cs`가 `Assets/_Project/Scripts/` 전체 .cs를 읽어
  금지 API 패턴을 스캔하고, 화이트리스트는 파일명만이 아니라 **그 라인이 정말 안전한 형태인지**까지
  함수로 재검증한다. 스캔이 디렉터리 전체를 훑으므로 미래 파일도 자동 포함된다.
- **설계**: 그 프레임워크를 그대로 복제해 **오프라인 감사 클래스**를 추가한다.
  - 금지 니들: `UnityWebRequest`, `HttpClient`, `WebClient`, `System.Net.Sockets`, `TcpClient`,
    `new Uri(` + 원격 스킴, `Application.OpenURL`(사용자 명시 액션 외 금지).
  - 화이트리스트 항목마다 **(파일, 이유, 동의 게이트 존재 여부)** 3종을 요구. 동의가 필요한 항목
    (크래시 리포트 등)은 "동의 플래그를 읽는 코드가 같은 파일에 있는가"까지 라인 검증.
  - 감사 대상 2호: **전역 키보드**. `Platform/IGlobalKeyStateService.cs`는 조합키 3개 + 동작키 소수만
    노출하며 "전체 키맵을 노출하면 조회 전용이라도 사실상 키로거"라고 명시해 범위를 못박아 뒀다.
    이 열거형이 임의로 커지지 못하게 잠그는 테스트를 같이 넣는다(5-2-6 기분 추측 항목의 전제).
- **(b) 난이도**: 작음. **(c)**: 원칙 2/3의 직접 보강. **(d)**: 없음.
- **평가**: **가장 싸고 가장 레버리지가 큰 항목. 이번 주 착수 0순위.**
  자동 업데이트·크래시 리포트·Steam이 들어오기 **전에** 넣어야 의미가 있다(들어온 뒤엔 그냥 현상 추인).

### 5-1-9. [조건부] 옵트인 크래시 리포트 자동 수집
- **원문 조건(그대로)**: *"크래시리포트 자동 수집 — **옵트인 + 명시 고지 필수**"* (기획서 2, 3절).
- **설계(조건 충족 방법)**:
  - **기본 OFF.** 동의 전에는 **수집도 저장도 하지 않는다**(로컬 큐에 쌓아두고 나중에 보내는 방식도
    금지 — "동의 전에는 존재하지 않음"이 고지와 일치하는 유일한 형태).
  - 동의 플래그는 세이브 v7 신규 필드 + 설정창(ux-designer 담당) 노출. 철회 시 로컬 큐 즉시 삭제.
  - **스크러빙이 이 앱에서는 특히 중대하다**: 이 앱은 **다른 창 목록을 열거**한다. 창 제목·앱 이름·
    파일 경로가 스택트레이스나 로그에 섞여 나갈 수 있고, `Application.persistentDataPath`에는
    **사용자 계정명**이 들어 있다. → 전송 전 화이트리스트 방식(우리 심볼/버전/OS/스택 프레임만)으로
    재조립하고, **창 제목·경로·사용자명은 전송 payload에 등장할 수 없음**을 단위 테스트로 잠근다.
  - 5-1-8 화이트리스트 등재 필수.
- **(b) 난이도**: 중간. **(c)**: 조건 충족 시 충돌 없음. 스크러빙 없이 붙이면 원칙 2/3의 정신을 정면 위반.
- **우선순위**: **스토어 채널 출시 이후.** 프리뷰 배포 단계에서는 사용자가 직접 로그를 보내주는 지금
  방식이 더 정확하고 위험이 0이다.

### 5-1-10. [조건부] 크로스플랫폼 세이브 동기화 (Steam Cloud)
- **원문 조건(그대로)**: *"크로스플랫폼 세이브 동기화(스팀 클라우드) — **스팀 우선, MS스토어/맥은 후순위**"*.
- **설계 — 코드 0줄 경로를 권고한다**:
  Steam **Auto-Cloud**는 파트너 사이트에서 경로 패턴만 등록하면 클라이언트가 알아서 동기화한다.
  `Core/CharacterSaveStore.cs`가 이미 `Application.persistentDataPath` 아래 **고정 파일명 하나**만
  쓰도록 설계돼 있어(그 문서에 "경로를 직접 조립하지 않는다"고 명시) 패턴 등록만으로 끝난다.
  → **Steamworks.NET 의존성 불필요 = 오프라인 우선 원칙 무손상.** ISteamRemoteStorage API 방식은
  네트워크/런타임 의존을 새로 만들므로 권고하지 않는다.
- **선결 스키마 보강(세이브 v7)**: 지금 세이브는 단일 JSON + 단조 `version`이라 **같은 버전·다른 기기**
  충돌을 구분할 수 없다(마지막 저장이 이김). 다운그레이드 가드는 "구버전 빌드가 신버전 파일을 덮어쓰는"
  경우만 막는다.
  → v7에 `lastWriteUnixSeconds` + `deviceId`(하드웨어 파생 아님, 최초 실행 시 난수 1회 생성 — 프라이버시)
  를 추가하고, 충돌 시 "더 나중 + 더 높은 진행도"를 고르거나 사용자에게 묻는다.
- **(b) 난이도**: Auto-Cloud 자체 작음 / 충돌 필드 작음. **선결**: Steam appid(리더/사용자 조치).
- **(c)**: 충돌 없음. **(d)**: 5-1-4의 원자적 저장과 같은 파일을 건드리므로 **묶어서 v7 한 번에** 처리 권고.

---

## 5-2. 콘텐츠 / 인터랙션 기술 실현성 (기획서 1 + 2)

### 5-2-0. 전체 현황표 (오늘 코드베이스 실측)

| 기획 항목 | 현재 상태 | 재사용할 기존 자산 | 난이도 | 원칙 |
|---|---|---|---|---|
| 창 상단=바닥 / 파쿠르 | **출하됨** | `IPlatformWindowService`→`FootholdPoller`→`VisibleTopEdgeSolver`→`GroundSensor`, `ParkourClimbState`, `LedgeHangState`, `DockGeometry` | (잔여) 작음 | OK |
| 첫 실행 회전 낙하 착지 | **부품만 있고 연출 없음** | `ThrowTumbleState`(공중회전), `FallState`+`ApplyFallPose`, `LandingCrouchState` | 중간 | 원칙1 주의 |
| OS 물리 장난 5종 | **출하됨** | `WindowTheft/Graffiti/DesktopTidy/BlackholeSummon/WindowCrash` + `SpectacleEventLock` | — | OK |
| 커서 놀리기 | **출하됨**(로데오/공격) | `RodeoCursorWatcher`, `RodeoCursorState` | — | OK |
| 커서 쓰다듬기(신규 다정 버전) | 없음 | 위와 동일 트리거 채널 | 작음 | OK |
| 하드웨어 반응(배터리/CPU/네트워크/충전) | **출하됨 4종** | `HardwareReactionDirector`(+지속조건·회복게이트·우선순위) | — | OK |
| 배터리/와이파이 "걱정" 유휴 버전 | 없음 | 위 디렉터에 **등급 추가**로 흡수 | 작음 | OK |
| 스트레스 / 반항 / 가출 | **출하됨** | `StressGauge`, `StressGaugeDirector`, `Sulky`, `RunawayState`/`RunawayDirector` | — | OK |
| 투두 말풍선 메모 | **출하됨** | `TodoListModel`, `TodoReminderDirector`, `TodoPostItWidget` | — | OK |
| 집중모드(포모도로) | **출하됨, 사양 변경 필요** | `FocusWatchDirector`(감시 민감도 3단계 포함), `FocusWatchRenderer` | 중간 | OK |
| 유휴 행동 9종 | 없음(공용 스케줄러 부재) | `AutoWanderController` Resting 페이즈, `TimedSpectacleState`, `SpectacleEventLock` | 중간 | 1건 위험 |
| 인질극 이벤트 | 없음 | 스펙터클 취소 선례(`e18ac09`), 클릭관통 유지 원칙 | 중간 | 주의 |
| 스케치북 무기 제작 | 없음 | `LocalClickCaptureGate`(격파 미니게임의 일시 입력 캡처 선례) | 큼 | 4번 주의 |
| 창문-던전 파밍 | 없음 | `CharacterProgressionModel`, `ItemCatalog`, 세이브 v6 | 큼 | OK |
| 미니보스 레이드 | 없음 + **라이벌 서브시스템이 삭제됨**(2026-08-30 사용자 결정) | — | 큼 | **확인 필요** |
| Steam P2P 친구 방문 | 없음 | — | 장기 프로젝트 | 오프라인 원칙 충돌 |
| 마우스 패턴 기분 추측 [조건부] | 없음 | `StickmanAgent.TryGetCursorPosition`, `StressGauge`, `ViewerPresenceSnapshot` | 작음~중간 | 고지+옵트아웃 필수 |

### 5-2-1. 파쿠르 / 창 상단을 바닥으로 취급하는 물리 — **이미 헤드라인 기능이다**
- **(a) 확인**: 파이프라인 전체가 존재한다.
  `IPlatformWindowService.EnumerateFootholds()`(읽기 전용 계약이 인터페이스 주석에 못박혀 있고,
  "타 윈도우를 이동/크기변경/종료/포커스 강제하는 메서드를 이 인터페이스에 추가하지 않는다"는 문장과
  그걸 강제하는 EditMode 테스트가 함께 있다) → `FootholdPoller`(`StickConfig.footholdPollInterval`
  주기 캐시, 내부 버퍼 재사용) → `VisibleTopEdgeSolver`(가려진 창 제외, 오늘 플랫폼 중립화) →
  `GroundSensor.TryFindLandingCrossing()`(스윕 착지 — 허용오차 밴드 방식이 11유닛/초 이상에서
  원리적으로 실패하던 것을 선분 교차로 대체) → `LandingCrouchState`.
- **`DockGeometry.cs`와의 연결점**: Dock/작업표시줄은 "특수한 발판"이고, `DockGeometry`가
  **되올라가기 상한을 실측 낙차에서 유도**한다(`ResolveStepUpMaxHeight`). tilesize 16~128에서 낙차가
  4.3배 변하므로 절대 상수를 쓰면 큰 Dock 사용자가 영영 못 올라온다 — 이미 해결됨.
  **신규 콘텐츠가 지켜야 할 것**: 발판 위 새 연출(낚시/낮잠/닦기)은 좌표를 스스로 추정하지 말고
  `DockGeometry` / `IDockMetricsService` / `IReservedBottomBarService`를 경유할 것. 이 프로젝트는
  "같은 물리 대상을 6개 테스트가 4:2로 다르게 하드코딩"해서 거짓 통과를 겪은 전례가 있다.
- **(d) 열린 이슈와의 상호작용 — 신규 콘텐츠의 전제조건 2건**:
  1. **[Major, 이월] Windows 알파 필터 부재.** macOS의 `kCGWindowAlpha < 0.05` 제외에 해당하는 것이
     `Win32WindowService`에 없다. 오늘 넣은 가려짐 필터가 이 위험을 **새로 만들었다** — 전체화면
     투명 창(스트리밍/접근성/보안 툴)이 그 아래 발판을 전부 지워 캐릭터가 낙하한다.
     → 로프 등반/낚시/낮잠/인질극/던전 **전부 발판 선택에 의존**하므로 이걸 먼저 고치지 않으면
     신규 기능이 이 버그의 누명을 쓴다.
  2. **[Minor, 이월] `GetWindowRect`가 DWM 리사이즈 보더(~7px)를 포함** → `DWMWA_EXTENDED_FRAME_BOUNDS`.
     인질극의 닫기 버튼 조준, 로프 앵커 위치가 Windows에서 일제히 ~7px 어긋난다. 싸고 파급이 크다.
- **(b) 잔여 난이도**: 작음(위 2건). **(c)**: 충돌 없음.

### 5-2-2. 첫 실행 회전 낙하 착지 연출 — **커밋 `9ad6279`와 다른 것을 요구하고 있다**
- **(a) 확인 결과**: `git log`와 소스 대조 결과 **부품은 다 있고 연출만 없다.**
  - `9ad6279` "낙하 자세 + 무릎앉아 착지" = `ApplyFallPose`(낙하 중 자세 부재를 채움) + `LandingCrouchState`
    (눌림→버팀→일어섬 3구간, 낙하 높이에서 지속시간 산출). **회전은 여기에 없다.**
  - `09ab271` "던지기 공중회전 착지" = `ThrowTumbleState`. **회전은 여기 있다** — 단 "던져졌을 때" 전용.
  - `Assets/_Project/Scripts/` 전체에서 `최초 실행` / `FirstRun` / `Onboarding` grep 결과, 인트로
    시퀀스는 **없다**. `firstRunUnixSeconds`는 세이브의 통계 타임스탬프일 뿐이다.
  → 기획서가 요구하는 것은 새 포즈가 아니라 **첫 실행 오케스트레이션**(화면 상단 스폰 → 회전 낙하 →
  작업표시줄 착지 → 첫 멘트)이다.
- **설계**:
  1. `FirstLaunchIntroDirector`(신규, 1회용). 새 상태를 만들지 않고 **기존 상태를 순서대로 태운다** —
     `ThrowTumbleState`의 회전 → 착지 시 `LandingCrouchState`. 이 프로젝트는 "TimedSpectacleState를
     왜 재사용하지 않았는가"를 클래스 문서로 남길 만큼 중복 상태 신설에 엄격하므로 그 관례를 따른다.
  2. 스폰 X는 **하드코딩 금지** — `IDockMetricsService` / `IReservedBottomBarService`로 착지 목표
     (Dock 또는 작업표시줄) 사각형을 얻어 그 중앙 위로 스폰. Dock 자동 숨김/측면 배치/미지원 플랫폼이면
     기존 바닥 안전망(`NullPlatformWindowService.BottomSafetyNetInsetPoints`)으로 폴백.
  3. 인트로 동안 `AutoWanderController` 억제, `SpectacleEventLock` 선점.
  4. **★ 원칙 1 — 여기가 이 항목에서 가장 깨지기 쉬운 지점이다.**
     "첫 멘트"를 **낙하 중 타이머로 재생하면 원칙 1 위반**이다. 반드시 `LandingCrouch → Idle` 전이가
     **확정된 프레임**에 그 `StateTransitionContext`로 `DialogueIntent`를 발급해야 한다. 착지가
     중간에 취소되면(발판이 닫힘/이동) 같은 프레임에 대사도 만료되는 것이 이 계약의 요점이다
     (`Dialogue/DialogueIntent.cs`의 세대 스냅샷 + 1회용 토큰).
  5. 재생 여부 플래그는 세이브 v7 `introPlayed`.
- **(교차 레이어 — 리더 보고 사항)**: 이 플래그의 **소유권이 ux-designer의 온보딩 설계와 겹친다.**
  권고: **온보딩 흐름이 플래그를 소유**하고, 인트로 디렉터는 이벤트를 받아 연출만 한다. 두 곳이 각자
  플래그를 읽으면 "온보딩은 끝났는데 인트로가 또 나온다" 류의 버그가 확정적으로 생긴다.
- **(b) 난이도**: 중간. **(c)**: 위 4번만 지키면 충돌 없음.

### 5-2-3. 집중모드 개편 (기획서 1, 4절) — **사양 변경 1건 주의**
- **(a) 확인**: `FocusWatchDirector`에 세션/딴짓 감지/민감도 3단계(`PomodoroSensitivity`)가 이미 있고,
  신규 폴링을 만들지 않고 **기존 발판 캐시의 `IsTopmost` 변화**로 포커스 전환을 세는 영리한 설계가
  이미 들어 있다(새 OS 호출 0).
- **★ 충돌 발견**: 기획서는 **"머리 위 HUD 링/모래시계"** 를 요구하는데, 현재 `FocusWatchRenderer`의
  타이머 링은 **캐릭터 발밑**이다(`RingCenterYRatio`, 주석에 "18절이 지정한 '발밑'"이라고 명시).
  즉 `docs/UX_FLOW.md` 18절 원문과 신규 기획서가 서로 다른 위치를 지정하고 있다.
  → **리더 확정 필요.** 이동 자체는 상수 2개(중심 Y 비율/반지름 비율) 변경이라 작지만, 머리 위는
  모자 액세서리와 겹치므로 액세서리 렌더러와의 Z/충돌 검토가 따라온다.
- **신규 요구 3건**: 행동 선택(책상업무/곡괭이질), 성과물 시각 누적, 종료 임박 체크 리액션.
  - 행동 선택 = 포즈 2종 + 소품 렌더러 2종. **소품은 손에 들지 않는다**(기획서가 명시적으로 금지 —
    행동 애니메이션과 충돌). 타이머는 캐릭터 상태와 **완전히 독립된 컴포넌트**로 유지 = 지금 구조 그대로.
  - 성과물 누적 = 팩별 차등 → **DLC 매니페스트에서 읽어야 한다**(5-3). 하드코딩하면 원칙 4 위반이
    또 한 겹 쌓인다.
  - 종료 임박 체크 리액션 = 기존 `FocusWatchTierChanged` 앰비언트 이벤트 채널 재사용.
- **(b) 난이도**: 중간. **(c)**: 성과물이 DLC 연동이므로 5-3 단계 B 이후에 하는 것이 순서상 옳다.

### 5-2-4. 유휴 행동 9종 — **공용 스케줄러 1개 + 렌더러 9개** 로 접근한다
- **(a) 확인**: 개별 유휴 행동은 없지만 **재사용할 뼈대는 다 있다.**
  `AutoWanderController`의 `Resting` 페이즈(Idle 유발) + `WanderAmbientMotionRequested` 신호
  (`LookAround` / `SitAndYawn`) + `IdleAmbientMotionRenderer`(신호를 블랙보드 포즈 레이어로 넘기는
  얇은 소비자) + `TimedSpectacleState`(Phase 4/5에서 이미 6개 연출이 이 하나를 인스턴스화해 재사용) +
  `SpectacleEventLock`.
  **결론: 9종은 9개의 서브시스템이 아니라 "스케줄러 1 + 데이터 9"다.**
- **설계 — `IdleActivityDirector`(신규 1개)**:
  - 발동 조건: 상태가 `Idle` **그리고** `AutoWanderController`가 `Resting` **그리고**
    `SpectacleEventLock` 획득 가능 **그리고** 활동별 전제조건 충족.
  - 활동은 가중치 테이블 + 개별 쿨다운(전부 `StickConfig`, 하드코딩 금지).
  - 포즈는 반드시 `StickmanBlackboard.TickPose()` 경로로 — `IdleAmbientMotionRenderer` 주석이
    이유를 이미 적어 뒀다(직접 Transform을 만지면 다음 프레임 중립 포즈가 덮어써서 연출이 사라진다).
  - **★ FramePacing 등급과의 상호작용(신규 발견, 안 보면 반드시 버그가 된다)**: 유휴 활동이 발동하는
    시점은 정확히 `Away`(≈180초 무입력) / `DisplayOff` 등급으로 프레임이 10fps/4fps로 떨어지는
    구간과 겹친다. **4fps 낚시 애니메이션은 고장으로 보인다.** 규칙: 등급이 `Away` 이상이면 새
    활동을 시작하지 않고 진행 중인 것은 중단한다. 어차피 **보는 사람이 없다는 것이 그 등급의 전제**다.
  - **★ 원칙 2 재발 방지**: 오늘 "전체화면 감지 시 몸은 사라지는데 런타임 생성 액세서리/펫이 남는"
    위반을 고쳤다. 신규 유휴 렌더러는 **반드시 `CharacterVisualRegistry`에 등록**하고 종료 시 해제할 것.
- **개별 항목**:
  | 활동 | 전제/재사용 | 난이도 | 비고 |
  |---|---|---|---|
  | 작은 창 로프 등반 | **`Interaction/WindowTheftTargetRules.cs`에 "작은 창" 판정이 이미 있다 — 그대로 재사용** + `ParkourClimbState` 포즈. 로프는 LineRenderer | 중간 | 기획서대로 던전 진입 연출로 겸용 |
  | 작업표시줄 낚시 | `IReservedBottomBarService`(오늘 Windows 작업표시줄 버그로 신설됨) + `SitAndYawn` 계열 앉기 포즈 | 중간 | "월척" 아이템은 **`ItemCatalog`/DLC 매니페스트 경유**, 문자열 하드코딩 금지 |
  | 비활성 창 흔들기 | — | — | **문자 그대로 불가(5-0 3번). 축소 스펙 필요** |
  | 배경화면 감상 리액션 | 신규 `IDesktopWallpaperService`(읽기 전용 경로 조회: macOS `desktopImageURLForScreen` / Windows `SPI_GETDESKWALLPAPER`) + 로컬 1회 다운샘플 | 중간 | 원칙 3 OK(읽기 전용). **화면 캡처 금지**, 서버 전송 금지(기획서도 명시). 설정 옵트아웃 권고 |
  | 그림자 인형극 | 순수 렌더링 | 작음 | 기획서대로 쇼케이스 모드 하위 패턴 |
  | 배터리/와이파이 걱정 | **`HardwareReactionDirector`에 "걱정" 등급 추가로 흡수** | 작음 | 아래 ★ 참고 |
  | 아이콘 사이 숨바꼭질 | `IDesktopIconLayoutService`(읽기 전용) + `RunawayState`의 은신 로직 | 작음 | 아래 ★★ 참고 |
  | 커서 쓰다듬기 | 로데오와 동일한 커서 정지 트리거 채널 | 작음 | 로데오와 상호배제(같은 락) |
  | 창 그림자 밑 낮잠 | `FootholdPoller.CachedRawWindows`에서 사각형 + 고정 오프셋으로 **음영 영역을 합성**(OS는 창 그림자 기하를 노출하지 않는다) | 중간 | 기획서 스스로 "검증 후, 후순위"라고 적음 |
  | 작업표시줄 아이콘 닦기 | `IReservedBottomBarService` | 작음 | 작업표시줄 **아이콘 좌표는 조회 수단이 없다**(`IDesktopIconLayoutService`는 바탕화면용). 개별 아이콘 정렬 시도 말고 바 전체를 훑는 동작으로 |
- **★ 하드웨어 신호 이중 소스 문제(교차 레이어, 리더 보고)**: `HardwareReactionDirector`는
  `SystemInfo.batteryStatus`로 배터리를 폴링하는데, 오늘 신설된 `ViewerPresenceSnapshot`은 이미
  네이티브에서 `OnBattery` / `LowPowerMode`를 들고 온다. **같은 사실에 대한 진실 공급원이 둘**이다 —
  이 프로젝트가 "Dock 낙차 4:2 갈림"으로 이미 크게 데인 바로 그 실패 패턴이다. 신규 "걱정 리액션"을
  붙이기 **전에** 하나로 합칠 것을 권고한다(난이도 작음).
- **★★ 숨바꼭질의 숨은 위험**: 클릭 관통이 켜진 채 캐릭터가 완전히 사라지면 사용자 입장에서
  **앱이 죽은 것과 구분되지 않는다.** 이 프로젝트는 이미 "캐릭터 사라짐"(커밋 `802143f`)으로 한 번
  겪었다. 최대 은신 시간을 `StickConfig` 값으로 두는 것에 더해 **항상 일부가 보이게(빼꼼)** 할 것.
- **(b) 전체 난이도**: 스케줄러 중간 + 개별 작음~중간. **(c)**: "흔들기" 1건만 원칙 3 충돌.

### 5-2-5. 인질극 / 스케치북 / 던전 / 미니보스 / P2P
- **인질극(중간)**: 위험은 "닫기 버튼을 막는다"를 **진짜로 막는 것**이다. 클릭 관통은 계속 ON을
  유지하고 캐릭터는 **막는 것처럼 보이기만** 한다 — 사용자가 실제로 누르면 창은 정상적으로 닫히고,
  그 순간 이벤트는 우아하게 취소돼야 한다(선례: 커밋 `e18ac09`가 스펙터클 5종의 대상 소실 취소 결함을
  이미 고쳤다. 그 취소 경로를 그대로 따를 것). 닫기 버튼 위치는 플랫폼별 크롬 기하가 필요하다
  (macOS 좌상단 신호등 / Windows 우상단) → `IPlatformWindowService`를 부풀리지 말고 **별도
  `IWindowChromeGeometryService`** 로 분리 권고(핵심 인터페이스를 작게 유지하는 기존 방침).
  Windows에서는 5-2-1의 ~7px 이슈를 먼저 고쳐야 조준이 맞는다.
- **스케치북 무기(큼)**: 입력 캡처가 필요 → 격파 미니게임이 쓰는 `LocalClickCaptureGate` 선례를 따라
  **그림 그리는 동안만** 관통을 끄고 끝나면 반드시 복구(원칙 2). **원칙 4 사전 결정 필요**: "컬러 잉크
  팩"이 색을 해금하므로, 사용 가능한 색 목록은 **하드코딩 enum이 아니라 DLC 매니페스트에서 읽어야
  한다.** 이걸 나중에 고치려면 저장된 유저 낙서 데이터까지 마이그레이션해야 하므로 **설계 시점에 못박을 것.**
- **창문-던전 파밍(큼/장기)**: 성장 인프라(`CharacterProgressionModel` 레벨/XP, `ItemCatalog`, 세이브 v6)는
  있다. 없는 것은 미니게임 루프(몬스터/전리품/경제)와 그 밸런싱이다. 로프 등반이 진입 연출이므로
  5-2-4가 선행.
- **미니보스 레이드(큼) — ★ 사용자 확인 필요**: 2인 이상 액터가 필요한데, **라이벌 서브시스템은
  2026-08-30에 사용자 지시로 전체 삭제됐다**(커밋 `b6755f4`). 보스 레이드는 사실상 그 인프라를
  다시 세우는 일이다. 방금 지운 것을 되살리는 결정이므로 코더 판단으로 시작하지 않는다.
- **Steam P2P 친구 방문(장기 프로젝트)**: 기획서 1도 "방문만, 상호작용은 후순위"로 적었다.
  **오프라인 우선 원칙과 정면으로 만나는 유일한 콘텐츠 항목**이므로 5-1-8 화이트리스트 체계가
  자리 잡은 뒤, 스팀 출시 이후로 미룰 것을 권고한다.

### 5-2-6. [조건부] 마우스 패턴 기반 기분 추측
- **원문 조건(그대로)**: *"마우스 이동 패턴으로 '바쁨/심심' 추측해 반응 강도 조절 — **명확한 고지 +
  옵트아웃 필수**"* (기획서 2, 2절).
- **(a) 코드베이스 확인 — 프롬프트의 전제 하나를 정정한다**:
  `Core/CharacterVisualRegistry.cs`는 **기분 시스템이 아니라 렌더 등록부**다(캐릭터가 지금 그리는
  모든 Renderer/LineRenderer를 앵커별로 모아 전체화면 감지 시 한 번에 숨기는 단일 창구, 오늘 원칙 2
  위반 수정으로 신설). 기분 추측과 **연결점이 없다.**
  실제로 "기분"에 해당하는 기존 상태는 두 곳이다 — `Core/StressGauge.cs`(0~1 정적 값)와
  `Platform/ViewerPresence.cs`의 `SecondsSinceUserInput`(이미 네이티브에서 계산돼 매 등급 판단에 쓰임,
  **추가 비용 0**).
- **설계 — 새 상태를 만들지 않는다**:
  - 기획서의 요구는 "새 기분 상태"가 아니라 **"반응 강도 조절"** 이다. → 신규 `EngagementModel`이
    `BusyFactor`(0~1) 하나만 노출하고, 기존 자율 발동 게이트들이 그 값을 **쿨다운 배수**로 곱해 쓴다:
    `HardwareReactionDirector`의 공용 게이트, `AutoWanderController`의 Rest/Move 지속시간,
    신설 `IdleActivityDirector`의 가중치.
  - **★ 원칙 1 방어선**: `BusyFactor`는 **대사를 고르지 않는다.** 빈도만 바꾼다. 만약 이 값이 대사
    선택에 직접 개입하면 "상태 확정 → 대사 파생"이 깨져 원칙 1 위반이다. 이 경계를 코드 리뷰
    체크포인트로 명시한다.
  - **입력원**: 이미 노출된 `StickmanAgent.TryGetCursorPosition` 채널만 사용(신규 폴링 0).
    **키보드는 절대 쓰지 않는다** — `Platform/IGlobalKeyStateService.cs`가 "전체 키맵을 노출하면
    조회 전용이라도 사실상 키로거"라며 조합키 3개 + 동작키 소수로 범위를 못박아 둔 그 방침을 그대로 승계.
  - **저장하지 않는다**: 링버퍼(사전 할당)에 커서 델타만 두고 종료 시 소멸. 세이브 파일에 마우스 관련
    필드를 만들지 않는다. → 고지문이 짧아지고("아무것도 저장하지 않습니다"), 옵트아웃이 완전해진다
    (플래그 하나로 링버퍼를 비우면 끝).
  - 옵트아웃 UI 자체는 ux-designer 설정창 담당. 우리는 **기본값을 리더에게 확인**받는다
    (원문이 "옵트아웃"이므로 기본 ON이 문언에는 맞지만, 기본 OFF가 더 안전하다는 의견을 첨부).
- **(b) 난이도**: 작음~중간. **(c)**: 위 방어선 준수 시 충돌 없음. **(d)**: 없음(신규 OS 호출 0).

---

## 5-3. DLC 매니페스트 스키마 초안 (원칙 4의 실제 구현)

### 5-3-1. 현황 — 왜 스키마가 필요한가
| 사실 | 근거 |
|---|---|
| 플러그인 SO 2종은 **소비자 0명** | `Assets/` 전수 grep, `Scripts/Plugins/` 외 참조 0건 |
| 콘텐츠 32종은 **C# 하드코딩** | `ItemCatalog.cs`의 `new Row(...)` 28개, `AccessoryShapeBuilder.cs` 1,225줄 `switch` |
| 데이터 에셋은 **1개뿐** | `Assets/_Project/Data/DefaultStickConfig.asset` |
| Addressables **미설치** | `Packages/manifest.json` |
→ **확정 6종 DLC(오피스 워커 / 사이버 아포칼립스 / 네온 낙서 / 스포츠 / 컬러 잉크 / 밀리터리)를
지금 구조로 만들면 매 팩마다 기본 로직을 수정하게 된다.**

### 5-3-2. 제안 스키마 — 에셋 3종 + 런타임 레지스트리 1개

**(A) `StickPackManifestSO` — 판매 단위 = 팩**
| 필드 | 타입 | 설명 |
|---|---|---|
| `packId` | string | 역DNS 형식 (`pack.office`, `pack.cyber`). 세이브/엔타이틀먼트 키 |
| `displayName` / `description` | 로컬라이즈 키 | 다국어 로드맵(기획서 2, 4절) 대비 — 원문 문자열 금지 |
| `packVersion` | int | 팩 자체 갱신 |
| `minAppSchemaVersion` | int | 구버전 앱이 신규 팩을 잘못 읽는 것 차단(세이브 다운그레이드 가드와 같은 사고방식) |
| `channels` | flags | Steam / MSStore / MacAppStore / AchievementReward |
| `entitlementIds` | string[] | 채널별 식별자(스팀 DLC appid 등). **무결성 검증은 채널 SDK 책임** |
| `palette` | Color ×3 | 기획서 2 아트 항목 "팩별 색채 팔레트 사전 정의". `StickConfig.ResolveInkColor()` 관례를 따라 렌더러가 직접 색을 쓰지 않고 여기서만 받는다 |
| `accessories` | `AccessoryDefSO[]` | 모자/타이/안경/망토 4종 "완전체 스킨" |
| `motions` / `effects` | `MotionPluginSO[]` / `EffectPluginSO[]` | 기존 스텁 확장 |
| `sounds` | (트리거키, AudioClip)[] | 기획서 1, 8절 "팩별 전용 사운드" |
| `focusOutputProp` | 참조 | 집중모드 성과물(5-2-3) 팩별 차등 |
| `thumbnail` / `gridIconSpec` | Sprite | 아트 항목 "팩별 아이콘/썸네일 통일 그리드 가이드" |
| `previewKey` | string | 설정창 미리보기 + "DLC 3분 체험판" 진입 키 |

**(B) `AccessoryDefSO` — 하드코딩 32종을 흡수할 그릇**
| 필드 | 타입 | 설명 |
|---|---|---|
| `itemId` | string | **기존 형식 그대로 승계**(`equip.neck.striped`). 세이브 v5부터 이미 문자열 ID를 저장하므로 **세이브 마이그레이션 불필요** ← 이게 이 계획 전체에서 가장 큰 행운이다 |
| `slot` | `EquipmentSlot` | Head/Eyes/Neck/Shoulders(=BACK) |
| `displayName` / `description` | 로컬라이즈 키 | |
| `requiredLevel` | int? | 기존 `ItemCatalogEntry.RequiredLevel` 승계 |
| `shape` | `ItemIconPart[]` | 기존 절차적 벡터 파트 구조를 **에셋 필드로 승격**(구조 그대로라 값 이관이 기계적) |
| `facingAsymmetric` | bool | 좌우 반전 검증 대상 표시(오늘 `AccessoryFacingFlipFillTests`가 검사하는 그 속성) |
| `hidesHair` | bool | **★ 오늘 열린 Major 4(`IsCoveredByHat` — 모자를 쓰면 머리 4종이 통째로 사라짐, 16조합 중 12)의 근본 해법.** 지금은 "모자면 무조건 머리를 숨긴다"는 전역 규칙인데, 이건 **아이템별 성질**이다. 스키마로 내리면 규칙이 아니라 데이터가 된다 |
| `stateBinding` | enum? | **★ 기획서 1, 6절 "상태 연동형" 전용 필드.** 졸업모=완료축하 / 수면모자=자리비움 / 안전모=파쿠르 / 연기모자=CPU과부하. 오늘의 32종은 **유저가 골라 입는** 것이고 이건 **상태가 자동으로 갈아끼우는** 것 — 다른 메커니즘이므로 한 필드로 명시 구분한다 |
| `unlockRule` | enum | Free / Pack / Achievement (기획서 1의 "슈퍼히어로 = 업적 보상 전용, 유료 아님") |
| `pointColorTargets` | flags | 기획서 2, 6절 무료 포인트 컬러(눈/윤곽선 하이라이트)가 이 아이템의 어느 파트에 적용되는지. **전신 테마는 팩 팔레트 소관**이라는 스코프 분리를 스키마 수준에서 강제 |

**(C) `MotionPluginSO` / `EffectPluginSO` 확장(기존 필드 유지 + 추가)**
`packId`(역참조) · `trigger`(OnEnter / OnExit / OnImpact / Continuous) · `assetKey`(Addressables 주소) ·
`intensityScale`. 기존 `applicableStates`(`StickmanStateId[]`)는 그대로 — 이미 올바른 축이다.

**(D) `PluginRegistry`(런타임 단일 조회 창구)**
부팅 시 1회 인덱스 구축(`packId`→매니페스트 / `itemId`→AccessoryDef / `(stateId, trigger)`→이펙트 목록),
이후 **조회 시 할당 0**(24시간 상주 앱 컨벤션). `ItemCatalog`는 하드코딩 Row를 버리고 이 레지스트리를
읽는 얇은 파사드로 축소하고, `AccessoryShapeBuilder`의 `switch`는 `shape` 데이터 순회로 대체된다.

### 5-3-3. 이행 경로 — 3단계, 각 단계가 독립적으로 안전하다
| 단계 | 내용 | 난이도 | 회귀 잠금 |
|---|---|---|---|
| **A** | `AccessoryDefSO` 스키마 정의 + 에디터 스크립트로 기존 32종을 .asset 32개로 **값 동일하게** 생성. 런타임은 아직 하드코딩을 읽음 | 중간 | 기존 `AccessoryShapeCatalogTests` / `ItemCatalogTests`가 그대로 통과 + "에셋과 하드코딩이 비트 동일" 대조 테스트 신설 |
| **B** | 카탈로그/렌더러가 하드코딩 대신 레지스트리를 읽게 전환. 하드코딩 제거 | 중간 | 위와 동일 테스트가 **여전히** 통과해야 함. 세이브 마이그레이션 **불필요**(itemId 문자열이 이미 세이브 포맷) |
| **C** | 팩 매니페스트 + Addressables 도입 + 6종 팩 제작 | 큼 | 팩 미설치/부분설치/구버전 팩 3종 로드 테스트 |
- **원칙 4를 실제로 지키기 시작하는 시점은 단계 B가 끝나는 순간이다.**
- **Addressables는 단계 C에서만 도입한다.** 지금 넣으면 `BuildStandalone.cs`와 CI(5-1-5)가 동시에
  복잡해진다. 단계 A/B는 평범한 .asset + `SceneBootstrapper` 참조로 충분하다.
- **부수 효과(공짜로 해결되는 것)**: `hidesHair` 필드가 오늘 UX 판단 대기 중인 Major 4를 데이터 문제로
  바꾼다. `stateBinding` 필드가 ux-designer가 지적한 "선택 착용 vs 상태 자동 변경" 구분을 스키마로 못박는다.

---

## 5-4. 우선순위 — 이번 주 착수 가능 vs 장기 로드맵

### A. 이번 주 바로 시작 가능 (신규 의존성 0, 전부 기존 인프라 재사용)
| # | 항목 | 절 | 난이도 | 시작 근거 |
|---|---|---|---|---|
| 1 | 오프라인 우선 정적 감사 + 키보드 범위 잠금 | 5-1-8 | 작음 | 네트워크 0건인 **지금** 잠가야 의미가 있다. 기존 감사 프레임워크 복제 |
| 2 | GitHub Actions CI 3잡 | 5-1-5 | 중간 | `BuildStandalone.cs` 진입점 완비. 라이선스 시크릿만 필요 |
| 3 | Windows 이월 결함 2건(알파 필터 대응물, `DWMWA_EXTENDED_FRAME_BOUNDS`) | 5-2-1 | 작음+작음 | **모든 신규 발판 콘텐츠의 전제**. 안 고치면 신규 기능이 누명을 쓴다 |
| 4 | 세이브 원자적 쓰기 + v7 필드 예약 | 5-1-4 / 5-1-10 | 작음 | 실재 결함(`CharacterSaveStore.cs:460`). v7 필드를 한 번에 열어 재마이그레이션 방지 |
| 5 | 디버그 콘솔(레벨+채널) + 무가드 로그 정적 감사 | 5-1-7 | 작음~중간 | bool 1개를 승격하는 것 |
| 6 | 이벤트 버스 구독 누수 회귀 테스트 | 5-1-3 | 작음 | 이 코드베이스에서 가장 실현 가능성 높은 누수 |
| 7 | 하드웨어 신호 이중 소스 통합 | 5-2-4 ★ | 작음 | "걱정 리액션" 추가 **전에** 해야 함 |
| 8 | 첫 실행 회전 낙하 인트로 | 5-2-2 | 중간 | 부품 완비. **ux-designer와 플래그 소유권 합의 선행** |

### B. 다음 (2~4주) — 콘텐츠 1차
9. `IdleActivityDirector` 공용 스케줄러 + 저위험 유휴 4종(그림자 인형극 / 커서 쓰다듬기 / 아이콘 닦기 / 숨바꼭질) — 중간
10. **DLC 스키마 단계 A + B** (원칙 4 실제 준수 시작 + Major 4 부수 해결) — 중간 + 중간
11. 낚시 / 로프 등반 (발판 의존 → A-3 완료 후) — 중간
12. 집중모드 개편(HUD 링 위치 확정 → 행동 선택 → 성과물은 10번 이후) — 중간
13. 마우스 패턴 기분 추측(옵트아웃 UI는 설정창 의존) — 작음~중간
14. 배경화면 감상 리액션 / 창 그림자 낮잠 — 중간

### C. 장기 로드맵 (분기 단위 · 외부 선결 조건 있음)
15. **DLC 단계 C** — Addressables + 팩 매니페스트 + 6종 팩 제작 (큼)
16. 자동 업데이트 (유통 채널 확정 후) — 중간
17. 옵트인 크래시 리포트 (스토어 출시 후) — 중간
18. Steam Cloud Auto-Cloud (appid 확보 후) — 작음, 대기 중
19. 스크린리더 호환 (UniWindowController 창 소유권 선결) — 중간
20. 인질극(`IWindowChromeGeometryService` 신설 필요) — 중간
21. 스케치북 무기 / 창문-던전 파밍 — 큼
22. 미니보스 레이드 — **사용자 확인 필요**(라이벌 삭제 결정과 충돌)
23. Steam P2P 친구 방문 — 장기, 오프라인 원칙 체계 확립 후

### D. 하지 말 것 / 축소 권고
- **Job System 멀티스레드 최적화** → 반려 권고(효과 <0.25%). `JobWorkerCount` 축소만 측정 후 검토(5-1-1).
- **비활성 창 흔들기** → 문자 그대로는 원칙 3 위반 또는 화면 캡처 신설. 축소 스펙 권고(5-0 3번).
- **소형 오버레이 창(perf-doc 제안 B-1)** → 이미 별도 Phase로 승인됨. 다개체(세포분열)/전역 연출과
  정면 충돌하므로 **위 콘텐츠 계획과 동시 진행 금지**.

---

## 5-5. 교차 레이어 영향 / 리더 결정 요청 사항
1. **[원칙 4] DLC 이행 3단계 승인 여부** — 6종 팩 계획의 전제. 미승인 시 팩마다 기본 로직 수정 확정(5-3).
2. **[사양 충돌] 집중모드 타이머 링 위치** — `UX_FLOW.md` 18절 "발밑"(구현됨) vs 신규 기획서 "머리 위".
   확정 필요(5-2-3).
3. **[소유권 충돌] 첫 실행 플래그** — 온보딩(ux-designer) vs 인트로 디렉터(코더). 온보딩 소유 권고(5-2-2).
4. **[삭제 결정 충돌] 미니보스 레이드** — 2026-08-30에 삭제된 라이벌 인프라를 되살리는 일. 사용자 확인 필요.
5. **[기본값 결정]** 자동 업데이트 확인(옵트아웃 권고) / 마우스 패턴 추측(기본 OFF 권고) / 배경화면 분석(옵트아웃).
6. **[외부 조치 필요]** GitHub Actions용 Unity 라이선스 시크릿, Steam appid.
7. **[신규 인터페이스 예고]** `IDesktopWallpaperService`, `IWindowChromeGeometryService`.
   둘 다 **읽기 전용**이며 `IPlatformWindowService`를 부풀리지 않기 위해 분리한다(그 인터페이스의
   "메서드를 추가하지 않는다" 주석과 이를 강제하는 EditMode 테스트를 존중).
8. **[성능 규칙 신설 제안]** 신규 유휴/스펙터클 렌더러는 (a) `CharacterVisualRegistry` 등록·해제 필수,
   (b) `FramePacing` 등급이 `Away` 이상이면 시작 금지·진행 중단. 둘 다 오늘 겪은 버그의 재발 방지.

---

# 6. 소형 오버레이 창(제안 B-1) 프로토타입 검증 결과 — perf-doc, 2026-08-31

> 1차 성능 리포트(`Tasklist.md` "5단계 — 리더 판단 필요")에서 **리더 판단**으로 이관됐던 제안 B-1
> ("전체화면 오버레이 대신 캐릭터 주변만 작은 창으로")의 실측 검증 라운드. 결론만 먼저:
> **기술적으로 막혀 있지는 않다. 그러나 실측이 기대 효과를 크게 깎았고(14.5배 → 약 1.6배),
> 가장 순진한 구현(매 프레임 창 추종)은 순효과가 음수다.** 상세는 아래.

## 6-1. 무엇을 어떻게 측정했나

Windows 실기가 이 개발 환경에 없고, 다른 에이전트 3명이 `Platform/MacOS`·궁술·`FramePacing`을
동시 편집 중이라 지금 Unity Player를 굽는 것은 중간 상태를 굽는 일이다. 그래서 **변수를 하나만
남긴 네이티브 프로토타입**을 따로 만들어 컴포지터 비용의 함수 형태를 직접 쟀다.

- 프로토타입: `Tools/PerfProbe/OverlayBench.swift` (Swift + Metal, 약 160줄).
  투명·무테·항상위·클릭관통 창을 만들고 **매 프레임 창 전체 드로어블을 clear 후 present** 한다
  (dirty-rect 부분 갱신이 아닌 전체 표면 제출 = Unity Player의 present와 같은 성질).
- 페이싱: 모든 조건에서 **59.9~60.2fps로 present 횟수를 고정**했다. 즉 조건 간 차이는
  표면적(또는 창 이동)뿐이다. (에이전트 셸에서 스폰된 프로세스는 백그라운드 QoS로 클램프되어
  타이머가 12~23Hz로 코얼레스된다 — 실측으로 확인하고 스핀 페이싱으로 우회했다.)
- 계측: `WindowServer` 프로세스의 누적 CPU 시간 델타. 이 맥에는 측정 중에도 StickMate.app 본체
  (21.7%), Claude, Cursor가 떠 있어 기저 부하가 20~40%로 흔들린다. 그래서 **3초마다 조건을
  교차시키는 페어드 설계**로 인접 쌍의 차이만 보고(드리프트 공통모드 제거) 부호검정을 붙였다.
- 재현 절차: `Tools/PerfProbe/HOWTO.txt`.

## 6-2. 실측 결과 (a: 프로토타입에서 확인한 것 / b: 개선폭)

| # | 실험 | 조건 | WindowServer 차이 | 부호검정 |
|---|---|---|---|---|
| E1 | **표면적** | 1512×982pt(3024×1964px, 5.94Mpx) vs 400×400pt(800×800px, 0.64Mpx) — 면적 9.3배 | **−6.80%p** (중앙값 −7.75) | 12/14 (p≈0.007) |
| E2 | **합성 총비용** | 같은 전체화면 창을 표시 vs 숨김(present는 60fps 유지, frames로 확인) | **+10.62%p** | 9/10 (p≈0.02) |
| E3 | **매 프레임 추종** | 640×640 창을 60Hz로 이동 vs 고정(크기 동일) | **+13.82%p** | **12/12** (p≈0.0005) |
| E4 | 10Hz 추종 | 같은 창을 10Hz로 이동 | +5.02%p | 9/10 |
| E5 | 2Hz 추종 | 같은 창을 2Hz로 이동 | +1.24%p (중앙값 −2.18) = **검출 불가** | 4/10 (= 노이즈) |
| E6 | 이동 호출 자체 | `setFrameOrigin` 1회의 순수 비용(메인스레드 동기 IPC) | **488~573µs/회** | 359~2278회 평균 |

**해석 — 세 가지가 새로 확정됐다.**

1. **표면적 비례성은 실재한다. 그러나 비용의 전부가 아니다.**
   전체화면 60fps 투명 오버레이 1개가 컴포지터에 지우는 총 부담이 **+10.6%p**(E2)인데,
   그중 표면적에 귀속되는 몫은 **6.8%p(약 64%)**이고 나머지 **3.8%p는 present 횟수에만 걸리는
   고정비**다(E1과 E2의 차). 창을 14.5분의 1로 줄여도 컴포지터 비용은 **약 1/3로 줄 뿐**이며
   **1/14.5가 되지 않는다.**
   → **1차 성능 리포트의 "WindowServer 18.0%p → 1.2%p (14.5배)" 추정은 이번 실측으로
   과대추정임이 확인됐다.** 그 표는 면적 귀속률을 100%로 가정했다. **정정한다.**
2. **매 프레임 창 추종은 절감을 삼키고도 남는다.** E3(+13.8%p) > E1(−6.8%p) →
   "작은 창으로 줄이고 캐릭터를 매 프레임 따라다니게 한다"는 **순효과가 음수**다.
   지금보다 느려진다. E6이 그 기구를 보여준다: 창 이동은 메인스레드에서 컴포지터로 가는
   동기 IPC이며 1회 0.5ms — 60Hz면 16.7ms 프레임 예산의 3.4%를 이동 호출만으로 쓴다.
3. **이동 빈도가 설계 변수다.** 60Hz +13.8%p → 10Hz +5.0%p → **2Hz는 측정 한계 아래(E5)**.
   즉 1차 리포트가 완화책으로 제시했던 **"청크 점프"(창을 자주 옮기지 않고 캐릭터가 가장자리
   여유에 닿을 때만 크게 점프)는 실측으로 지지된다** — 단, **초당 2회 이하**여야 한다.

**측정의 한계(정직한 명시).**
- 이것은 macOS `WindowServer` 측정이지 Windows `dwm.exe`/BitBlt 측정이 아니다. 다만 방향은
  **보수적**이다: macOS는 IOSurface를 GPU 텍스처로 그대로 합성해 CPU 복사가 없는 반면,
  Windows BitBlt는 정의상 present마다 표면 전체를 복사한다(`Tasklist.md` 증거 E2). 따라서
  **면적 귀속률은 Windows에서 더 크면 컸지 작지 않다.** 반대로 창 이동 비용(E3)은 Windows에서
  `SetWindowPos` + 레이어드 창 재합성이라 macOS보다 쌀 이유가 없다. **두 결론(면적 절감은
  실재한다 / 매 프레임 추종은 금지)은 Windows에서도 유지된다.**
- GPU 측 비용은 미측정(`powermetrics`가 sudo를 요구).
- 사용자 확인("배터리 세이버 아님, StickMate만 실행하면 느려짐", 2026-08-31)과 **부합한다**:
  E2가 그 크기를 처음으로 격리 측정한 값이다 — 우리 프로세스 CPU와 무관하게 컴포지터에만
  +10.6%p가 실린다.

## 6-3. 라이브러리가 소형 창을 지원하는가 — **중단 조건에 해당하지 않음**

리더가 지정한 즉시 중단 조건("UniWindowController로 작은 창에서 투명/클릭관통/항상위가 안 되면
그 시점에 중단")을 코드 경로로 확인했다.

- `UniWindowController.windowSize` setter → `UniWinCore.SetWindowSize()` → `LibUniWinC.SetSize()`.
  **런타임 임의 크기 설정이 라이브러리 계약에 있고**, 크기 변경이 투명/항상위/클릭관통을
  되돌리는 코드는 패키지 어디에도 없다.
- 더 강한 증거: **우리 제품이 이미 런타임 리사이즈를 하고 있다.**
  `Platform/Windows/WindowsOverlayStateEnforcer.TickFullScreenBounds()`(및 macOS 형제)가
  창 부착 이후 `Screen.SetResolution` + `windowSize`/`windowPosition` 대입으로 창을 모니터
  크기로 **확대**하고, 그 뒤 투명/항상위/클릭관통을 재적용해 되읽기까지 로그로 남긴다.
  "부착 후 크기 변경 + 상태 유지"는 이미 검증된 경로이며 축소가 다른 코드 경로를 타지 않는다.
- 라이브러리가 창을 되돌릴 수 있는 두 경로는 모두 꺼져 있다:
  `shouldFitMonitor = false`(기본값, `SceneBootstrapper`가 켜지 않음), `isHitTestEnabled = false`
  (그래서 매 프레임 `ReadPixels` 히트테스트 경로도 돌지 않는다).
- **정정 1건**: 현재 오버레이는 "전 가상 데스크톱(멀티모니터 포함)"이 아니라 **창 중심이 속한
  모니터 1개**다(`WindowsOverlayStateEnforcer.TryGetTargetMonitorRect`). 지금도 다른 모니터로는
  못 넘어간다.
- **미검증으로 남는 것**: `Screen.SetResolution`이 스왑체인을 재생성하며 만드는 가시적 끊김의
  실제 크기. 아래 난제 2 때문에 전역 연출마다 이 전환이 필요해지므로, 실기에서 반드시 먼저
  눈으로 확인해야 하는 단 하나의 항목이다.

## 6-4. 기술적 난제 — 코드베이스 기준 실제 확인 (c: 난이도)

| # | 난제 | 실제 확인된 내용 | 난이도 |
|---|---|---|---|
| 1 | **`orthographicSize`의 의미가 갈라진다** | `SceneBootstrapper`가 ortho=12로 굽고 픽셀/월드 비가 여기서 파생된다. 창을 400pt로 줄이면 화질 유지를 위해 ortho도 12→4.89로 줄여야 하고, 그 순간 `cam.orthographicSize`를 **"화면 절반 높이"로 쓰던 코드가 전부 "창 절반 높이"로 의미가 바뀐다**. 실사용처: `TodoReminderRenderer:326`, `HardwareReactionRenderer:369`, `StressGaugeRenderer:460`, `RunawayRenderer:509`, `FocusWatchRenderer:403`, `BattleMinigameRenderer:433`, `ArcheryDirector:306,347`, `ArcheryRenderer:307`, `CharacterPetRenderer:463`, `DockPhysicsStep:211`. 전부 "화면 가장자리로 클램프" 의도라 소형 창에서는 **캐릭터 코앞에 클램프**된다. 총 18개 파일이 `orthographicSize`를, 21개 파일이 `Screen.width/height`를 참조한다 | **특대** |
| 2 | **전역 UI가 물리적으로 안 들어간다** | `CharacterInfoWindow`는 **880×861pt**(`PanelWidth`/`PanelHeight`) — 640×640 창에도 안 들어간다. 최소 클램프 320×320으로 접히면 UI가 붕괴한다. `CornerHoverPanel`은 **화면 좌하단 모서리 기준** 264×392pt인데 소형 창에는 "화면 모서리"라는 앵커 자체가 없다. 둘 다 ScreenSpaceOverlay 캔버스라 **창 밖으로 1픽셀도 못 나간다** → 뜰 때마다 전체화면 복귀 필요 → 그때마다 6-3의 미검증 끊김 발생 | **특대** |
| 3 | **좌표계** — 유일한 좋은 소식 | `ScreenCoordinateConverter.OverlayOriginOsScreen`이 "창 좌상단의 OS 좌표"를 **이미 흡수하고 있고**, macOS에서 실전 사용 중이다. `Win32WindowService.CaptureOverlayOrigin()`이 `GetWindowRect`로 원점/배율을 갱신한다. `VisibleTopEdgeSolver`는 순수 OS 좌표 산술이라 창 크기와 **무관**하다. `CheckScreenBoundsOrFall`의 경계도 카메라가 아니라 **발판 합집합**(`GroundSensor.ScreenLeft/RightWorldX`)에서 나오므로 소형 창에서도 캐릭터가 데스크톱 전폭을 걸을 수 있다. **주의 1건**: 창을 옮기는 코드가 **같은 프레임에** `ReportOverlayWindowOsRect`를 직접 불러야 한다(폴링에 맡기면 그 사이 프레임에서 커서↔월드가 틀어진다) | **소** |
| 4 | **멀티모니터** | 지금은 한 모니터 고정이라 토폴로지 변경 훅이 아예 없다. 소형 창은 모니터 경계를 자유롭게 넘나들게 되므로 `WM_DISPLAYCHANGE` / `NSApplicationDidChangeScreenParameters` 훅이 **새로 필요**하다 | 중 |
| 5 | **세포분열 다개체(Phase 5)** | 개체가 화면 양끝에 있으면 바운딩박스 창이 결국 전체화면이 된다 — 1차 리포트와 같은 결론, 변화 없음 | **특대**(컨셉 충돌) |
| 6 | **창 이동 빈도 제약(신규)** | 6-2 E3/E5가 강제하는 새 설계 제약: **창 이동은 초당 2회 이하**. 매 프레임 추종은 금지 | (설계 제약) |

## 6-5. 최종 권고 (d)

**결론: "전면 착수해도 좋다"가 아니라 "기술적으로는 가능하지만 지금 착수하지 말 것 — 리더 판단"이다.**

근거를 순서대로:
1. **막혀 있지는 않다.** 리더가 지정한 즉시 중단 조건(라이브러리 불가)은 **해당하지 않는다**(6-3).
   좌표계도 이미 준비돼 있다(난제 3).
2. **그러나 이득이 실측으로 깎였다.** 최대 이득은 컴포지터 비용의 **약 −64%**(면적 귀속분 6.8%p /
   총 10.6%p)이지 14.5배가 아니다. 1차 리포트의 그 숫자는 **이번 라운드로 정정된다.**
3. **가장 순진한 구현은 지금보다 느리다**(E3 +13.8%p > E1 −6.8%p). 즉 "일단 만들어 보고 튜닝"이
   통하지 않는 종류의 변경이다 — 이동 빈도 설계를 **처음부터** 맞춰야 이득이 존재한다.
4. **공사 규모는 1차 추정보다 커졌다.** 특히 난제 2가 새로 확정됐다: **880×861pt 정보창은
   소형 창과 물리적으로 양립 불가**다. 전역 연출뿐 아니라 **상시 UI 두 개**가 전체화면 복귀를
   요구한다.
5. **더 싼 축이 남아 있다.** 비용 모델 `표면적 × present 횟수 × 그럴 필요가 있는 시간`에서
   이미 적용된 적응형 프레임(2·3번 축)은 앱 CPU 31.6% → 3.6~21.1%를 훨씬 싼 값에 얻었다.
   B-1은 그보다 훨씬 큰 공사로 그보다 작은 이득을 낸다.

**그래서 리더에게 제안하는 다음 한 걸음(B-1보다 먼저, 훨씬 쌈):**
Windows 실기에서 **`FramePacing`의 present 축소가 실제로 `dwm.exe`를 낮추는지 먼저 측정**한다.
코드는 이미 있으므로 **측정만 하면 된다**(작업 관리자 > 성능, `STICKMATE_FORCE_TIER` 환경변수로
등급 강제). macOS에서는 이 축이 실제로 컴포지터를 낮춘다는 것이 이미 확인됐다(Calm에서
WindowServer 13.0% → 7.6%). Windows에서도 같으면 **B-1 없이 사용자 신고가 해소될 수 있다.**

**그럼에도 착수한다면, 다음 5개가 전부 선행 조건이다:**
1. 창 이동은 **초당 2회 이하 청크 점프**(E5). 매 프레임 추종 구현은 착수 즉시 실패한다.
2. `orthographicSize`를 창 높이에서 파생하는 **단일 소스**를 먼저 도입하고, "화면 가장자리"를
   묻는 12곳을 **"데스크톱 경계" 질의 API**로 교체(난제 1). 이 선행 작업 없이 창을 줄이면
   연출 12종이 조용히 캐릭터 코앞으로 클램프된다.
3. `CharacterInfoWindow`/`CornerHoverPanel`은 **전체화면 복귀 모드**로 분리하고, 그 전환의
   `Screen.SetResolution` 끊김을 **실기에서 눈으로 먼저 확인**(6-3 미검증 항목).
4. 디스플레이 토폴로지 변경 훅 신설(난제 4).
5. 다개체는 바운딩박스 창으로 폴백하되, 양끝 배치에서 전체화면으로 되돌아간다는 것을
   **컨셉 차원에서 수용**할 것(난제 5).

**원칙 확인**: 소형 창이 되어도 클릭관통 기본 ON(원칙 2)과 전체화면 게임 감지 자동 숨김은
그대로 유지된다 — 오히려 창이 작아지면 히트테스트 대상 면적이 줄어 유리하다(1차 리포트 4번).
이 변경은 **우리 오버레이 창 하나의 크기/위치만** 바꾸며, 타 프로세스 창은 여전히 읽기 전용
열거 대상이다(원칙 3 무영향).

**프로토타입 코드**: `Tools/PerfProbe/`(`OverlayBench.swift`, `paired.py`, `run.py`, `HOWTO.txt`).
`Assets/` 밖이라 Unity가 임포트하지 않으며 제품 빌드에 포함되지 않는다. 위 수치를 재현하거나
Windows에서 같은 실험을 다시 하고 싶을 때 쓰는 계측 도구로 남겨 뒀다.

## 6-6. Windows 실기 1차 측정(작업관리자 스냅샷 2장) — **판정: 확증도 반증도 아님**

사용자가 Windows 실기에서 작업관리자 자세히 탭으로 `dwm.exe`를 직접 확인했다.
결과: **CPU 00~01%**, 메모리 **219,444K → 226,896K(+7,452K)**.

### (1) CPU 00~01%는 우리 가설을 반증하지 않는다 — 계측기 분해능 문제다

**단위가 다르다.** 6-2의 macOS 수치는 `ps` 누적 CPU 시간 델타 = **코어 1개 기준 %**다.
작업관리자 자세히 탭의 CPU 열은 **논리 프로세서 전체 합계 기준 정수 %**다. 환산하면:

| 논리 프로세서 | macOS 실측 +10.6%(1코어 기준)이 작업관리자에 보이는 값 | 정수 표시 |
|---|---|---|
| 8 | 1.33% | 01% |
| 12 | 0.88% | 01% |
| 16 | 0.66% | 01% |
| 24 | 0.44% | 00% |

**즉 사용자가 관측한 "00~01%"는 우리 예측치와 정확히 일치한다.** 게다가 정수 표시라
분해능 하한이 0.5%이고, 이는 16스레드 기준 **코어 1개의 8%**다 — 우리가 찾는 신호(코어 1개의
약 10%)가 계측기 눈금 한 칸 안에 통째로 들어간다. **이 도구로는 있는 것도 안 보인다.**

추가로, **애초에 CPU가 아니라 GPU를 봐야 할 가능성이 높다.** BitBlt 복사는 GPU 카피 엔진이
수행하므로 `dwm.exe`의 **CPU가 0에 가까운 것이 정상**일 수 있다. 1차 조사(`Tasklist.md` E2)의
"비용이 dwm.exe/GPU 복사 엔진에 계상된다"는 표현에서 **GPU 쪽을 아직 한 번도 안 봤다.**

### (2) 반대로 메모리 델타는 살아 있는 신호다 — 이쪽을 주 계측기로 삼아야 한다

`+7,452K = 7.28MiB`. 레이어드 창의 리디렉션 표면을 BGRA 32bpp로 잡으면:

| 화면 해상도 | 표면 1장 크기 | 사용자 델타(7.28MiB)와의 비 |
|---|---|---|
| 1366×768 | 4.00MiB | 1.82배 |
| **1920×1080** | **7.91MiB** | **0.92배 — 거의 일치** |
| 2560×1440 | 14.06MiB | 0.52배 |

사용자 모니터가 1920×1080이라면 **"dwm이 우리 창 크기만 한 표면을 하나 더 들고 있다"와
정량적으로 부합한다.** 이것이 BitBlt 비용 모델의 **면적 비례 부분에 대한 첫 Windows 측 증거**다.
n=1이라 아직 증거로 확정할 수 없지만, **메모리 열은 분해능이 1K(신호 대비 약 8000:1)**라
CPU 열과 달리 우리가 찾는 크기의 효과를 충분히 분해한다.

### (3) 그래서 요청할 후속 측정 3종 (난이도 순)

**측정 1 — 작업관리자 열 추가 + 4상태 스냅샷 (5분, 클릭 몇 번)**
자세히 탭 열 머리글 우클릭 → **GPU, GPU 엔진** 추가. 상태를 다음 순서로 바꾸며 `dwm.exe`와
`StickMate.exe`의 CPU/메모리/GPU/GPU엔진을 적는다:
(a) StickMate 종료 → (b) 실행 후 유휴 60초(마우스 안 건드림) → (c) 캐릭터를 드래그하며 상호작용
→ (d) 다시 종료. **(a)와 (d)를 둘 다 재는 것이 핵심**이다(배경 부하 드리프트 확인).
함께 알려줄 것: **논리 프로세서 수**(성능 탭 CPU 우하단), **화면 해상도**, **디스플레이 배율(%)**.
성능 탭 > GPU에서 그래프 하나를 **Copy**로 바꿔 두고 앱을 켜고 끌 때 파형이 변하는지도 본다.

**측정 2 — PowerShell 페어드 샘플링 (4분, `Tools/PerfProbe/measure-dwm.ps1`)**
macOS에서 쓴 것과 **같은 방법**(누적 CPU 시간 델타 + OFF/ON 교차 4사이클)을 Windows로 옮긴 것.
`Win32_PerfRawData_PerfProc_Process`를 읽어 **100ns 분해능**으로 재고, 결과를 **코어 1개 기준 %**
(= macOS 수치와 직접 비교 가능)와 작업관리자 환산치로 함께 출력한다.
`Get-Counter`를 쓰지 않는 이유: **한국어 Windows는 성능 카운터 경로가 현지화**되어
`"\Process(dwm)\% Processor Time"`이 실패한다. `Win32_PerfRawData_*`는 언어 무관이다.

**측정 3 — 면적 스케일링 (결정적. B-1 착수 여부를 여기서 가른다)**
우리 오버레이 창은 **모니터 크기에 자동으로 맞춰진다**(`TickFullScreenBounds`). 따라서 **코드를
한 줄도 고치지 않고** 창 면적만 바꿀 수 있다 — Windows 디스플레이 설정에서 해상도를
1920×1080 → 1280×720으로 낮추면 창 면적이 **2.25배** 작아진다.
각 해상도에서 (앱 종료 vs 실행)의 **dwm 메모리 델타**를 재서, 델타가 7.9MiB → 3.5MiB로
**면적에 비례해 줄어드는지** 본다.
- 비례하면 → BitBlt 면적 비례 모델이 Windows에서 직접 확인되고, **B-1의 이득 상한이 실측으로
  확정된다.**
- 비례하지 않으면 → **B-1은 즉시 기각**한다(줄일 것이 애초에 없다는 뜻).
이것이 **코드 변경 없이 소형 창의 효과를 미리 재는 유일한 방법**이다.

### (4) 이 개발 환경에서 원격으로 할 수 있는 것 / 없는 것
- **없다**: Windows 머신에 접근할 수단이 전혀 없다. 실행도 수집도 원격으로 못 한다.
- **있다**: 사용자가 한 번 실행하고 결과를 붙여넣기만 하면 되는 스크립트를 만들어 두는 것.
  그것이 `Tools/PerfProbe/measure-dwm.ps1`이며, UTF-8 BOM으로 저장해 한국어 Windows PowerShell
  5.1에서도 깨지지 않게 했다. 자동 모드(`-Auto`)는 앱을 강제 종료하므로 **최대 60초치 진행도
  (자동저장 주기)를 잃을 수 있다** — 방금 실행한 직후에 돌리면 잃을 것이 없다.

### (5) 현재 판정
**이 데이터만으로는 B-1 착수 여부를 결정할 수 없다.** CPU 열은 우리 신호를 분해하지 못하고
(반증이 아니라 무정보), 메모리 델타는 방향이 맞지만 표본이 1이다. **측정 3이 결정적이며 5분이면
끝난다.** 그 결과가 나오기 전까지 소형 오버레이 창 재설계는 **착수 보류**를 유지한다.

## 6-7. 해상도 4K 확인에 따른 6-6 **정정** 및 요청 측정 갱신

사용자 실제 해상도가 **3840×2160(4K)**으로 확인됐다. 6-6의 메모리 해석이 이 정보로 무너지고,
동시에 **추측이 필요 없는 더 좋은 계측기**가 드러났다.

### (1) 【정정】 "메모리 델타가 1920×1080 표면과 92% 일치" — **철회한다**

| 해상도 | BGRA 표면 1장 | 60fps BitBlt 복사 대역폭 |
|---|---|---|
| **3840×2160(실제)** | **31.64MiB** | **1.99GB/s** |
| 2560×1440 | 14.06MiB | 0.88GB/s |
| 1920×1080 | 7.91MiB | 0.50GB/s |

사용자 델타 `+7,452K = 7.28MiB`는 4K 표면의 **23.0%**에 불과하다. 6-6에서 "1920×1080과 92% 일치"라고
쓴 것은 **해상도를 모르는 상태에서 맞춘 우연이었고, 틀렸다.**

**그리고 "DPI 가상화 때문에 앱이 1080p로 렌더링되는 것 아니냐"는 대안 가설도 반증됐다.**
빌드된 `Builds/Windows/StickMate.exe`의 임베디드 매니페스트를 직접 열어 확인했다:

```
<dpiAware ...>True/PM</dpiAware>
<dpiAwareness ...>PerMonitorV2</dpiAwareness>
```

**Unity Player는 Per-Monitor V2 DPI 인식**이다. 따라서 Windows는 우리 창을 가상화하지 않고,
**디스플레이 배율(100/150/200%)과 무관하게 합성 표면은 언제나 물리 픽셀 = 4K(31.64MiB)**다.
(부수 확인: `Win32WindowService`의 `AutoDpiScale=1.0` 전제와 `GetDpiForWindow` 기반 UI 밀도 보정이
설계대로 동작할 조건이 갖춰져 있다.)

**그래서 남는 해석**: DWM의 합성 표면은 D3D 리소스라 **VRAM 또는 공유 GPU 메모리에 있고
`dwm.exe`의 작업 집합에 (전부) 잡히지 않는다.** +7.28MiB는 표면 자체가 아니라 부수 할당일 가능성이
높다. → **6-6에서 메모리 열을 "주 계측기"로 승격한 판단을 철회한다.** 보조 지표로 강등한다.

### (2) 대신 **추측이 필요 없는 계측기**가 있다 — 우리 앱이 이미 로그로 다 적고 있다

`WindowsOverlayStateEnforcer` / `Win32WindowService`는 이미 **표면 크기 자체를 로그로 남긴다**
(`Screen=(WxH)`, `windowSize`, `clientSize`, `dpi배율`, `UI 밀도(디스플레이 배율 %)`, `MSAA 요청/실측`,
`GPU=...`). 즉 **면적을 메모리로 역추정할 이유가 애초에 없다.** 파일 하나면 끝난다:

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\StickMate\Player.log
```

이 한 파일이 동시에 답하는 것: ① 우리가 정말 4K 전체를 합성 중인가 ② `TickFullScreenBounds`가
성공했는가 ③ 디스플레이 배율이 몇 %이고 UI 밀도 보정이 걸렸는가(= "글씨가 안 보인다" 신고의
사후 검증) ④ **MSAA가 몇 배로 걸려 있는가**.

④가 특히 크다. **4K + MSAA 4x면 컬러버퍼가 126.6MiB, resolve 트래픽이 프레임당 약 158MiB =
60fps에서 약 10.0GB/s**다. 이 비용은 **캐릭터가 덮는 픽셀이 아니라 렌더 타깃 전체**에 걸린다
(1차 리포트 제안 B-3). 4K에서는 이 항목이 BitBlt 복사(1.99GB/s)보다 **5배 크다** — 즉
**소형 창(B-1)보다 MSAA 조정(B-3)이 더 큰 레버일 수 있고, 공사는 비교도 안 되게 작다.**

### (3) 계측기 우선순위 재배치

| 순위 | 계측기 | 왜 |
|---|---|---|
| 1 | **`Player.log`** | 표면 크기·배율·MSAA를 **앱이 직접 보고**. 추측 0. 비용 0 |
| 2 | **GPU / GPU 엔진(Copy) 열 + 성능 탭 전용 GPU 메모리** | BitBlt 복사는 GPU 카피 엔진 몫. 31.6MiB 표면도 여기 잡힌다 |
| 3 | `measure-dwm.ps1` (100ns 분해능 CPU) | 작업관리자 CPU 열이 못 보는 크기를 분해 |
| 4 | ~~메모리 열~~ (보조로 강등) | 표면이 작업 집합 밖일 수 있음 |

### (4) 측정 3 갱신 — **3840×2160 → 1920×1080 (정확히 4배)**

원래 "1920→1280"으로 제안했으나 시작점이 4K이므로 **3840×2160 → 1920×1080**이 맞다.
면적이 **정확히 4배** 줄고, 둘 다 사용자가 실제로 설정할 수 있는 표준 해상도다.

**배율 함정은 이번에 해소됐다**: 앱이 Per-Monitor V2 인식이라 **디스플레이 배율을 바꿔도 표면
면적이 변하지 않는다.** 따라서 "해상도를 바꿨더니 Windows가 배율도 같이 바꿔 면적이 그대로여서
효과가 안 보이는" 사고는 일어나지 않는다. (그래도 두 조건의 배율 %는 `Player.log`로 사후 확인한다.)

**예상 효과와 "성공"의 정의**(미리 못 박아 둔다):
- 픽셀이 75% 사라진다. macOS 실측 면적 귀속률(약 64%, BitBlt에서는 그 이상)을 적용하면
  **`dwm.exe` CPU가 앱 귀속분의 최소 약 절반 감소**해야 한다.
- **그런데 그 절대값은 작업관리자 CPU 열에서 여전히 안 보인다**(0.7% → 0.35% 수준).
  → **측정 3은 반드시 `measure-dwm.ps1`로 해야 한다.** 작업관리자로는 판정 불가.
- **정상 동작 확인용 대조 신호**: `StickMate.exe` **자기 CPU/GPU는 눈에 띄게 떨어져야 한다**
  (렌더 픽셀이 1/4). 이게 안 떨어지면 해상도 변경이 앱에 반영되지 않은 것이므로 그 회차는 폐기한다.

### (5) B-1 경제성 재계산 (4K 반영)
4K(8.29Mpx) 기준 640×640 창은 면적비 **20.2배**(이전 추정 14.5배보다 큼). 즉 **상한은 커졌다.**
그러나 6-2의 면적 귀속률(약 64%)과 매 프레임 추종 금지 제약(E3)은 그대로이므로 **최대 이득은
여전히 컴포지터 비용의 약 −64%**이고, 6-5의 권고(착수 보류, 선행 조건 5개)는 **변경 없다.**
다만 4K에서 **B-3(MSAA)이 B-1보다 큰 레버일 수 있다는 새 후보**가 생겼다 — `Player.log`가 답한다.

## 6-8. 해상도 4배 축소 실험 결과("체감 차이 없음") 해석 — **B-1 조건부 잠정 기각**

사용자 보고: 3840×2160 → 1920×1080으로 낮춰도 **체감상 차이 없음**. 정밀 측정(CSV) 여부는 미확인.

### (1) 이 보고를 "면적 비례성 반증"으로 쓰면 **안 된다** (6-6과 같은 함정)

나는 이 실험을 요청할 때 **미리** 못 박았다: 예상 효과는 작업관리자 CPU 열에서 0.7% → 0.35% 수준이라
**작업관리자로도 안 보이므로 반드시 스크립트로 재야 한다**고. 사람의 체감은 작업관리자보다 **더 거친**
계측기다. 따라서 "체감 차이 없음"은 **면적 비례성에 대해서는 여전히 무정보**다.
결론이 내 예상과 같은 방향이라고 해서 기준을 낮추면, 6-6에서 "00%는 반증이 아니다"라고 한 것과
모순된다. **면적 비례성 자체는 macOS 실측(6-2 E1)이 여전히 지지한다.**

### (2) 그런데 **다른 논증**으로는 기각이 성립한다 — 천장(ceiling) 논증

B-1의 이득은 제거한 면적에 비례한다. 그런데 사용자는 **B-1이 줄 수 있는 면적 이득의 대부분을
이미 무료로 획득했다**:

| | 픽셀 | 4K 대비 제거한 면적 |
|---|---|---|
| 3840×2160 (기존) | 8,294,400 | — |
| **1920×1080 (사용자가 방금 테스트)** | 2,073,600 | **75.0%** |
| 640×640 (B-1 목표) | 409,600 | 95.06% |

**75.0 / 95.06 = 78.9%.** 즉 사용자는 **설정 토글 한 번으로 B-1 상한의 78.9%를 이미 체험했고,
아무 변화도 느끼지 못했다.** B-1이 추가로 주는 몫은 **남은 21.1%**뿐이며, 그 21.1%를 사기 위해
치르는 값은 6-4의 특대 난제 전부(880×861 정보창 붕괴, `orthographicSize` 12곳, 다개체 컨셉 충돌,
매 프레임 추종 금지 제약)다.

**이 논증은 계측 정밀도와 무관하다.** 체감이 안 변한 이유가 "효과가 0이라서"가 아니라 "체감 임계
아래라서"라 해도, **남은 21.1%는 그보다 더 작다.** 어느 쪽이든 같은 결론이다:
**B-1은 사용자가 신고한 시스템 저하의 해법이 아니다.**

→ **B-1 잠정 기각을 권고한다.** 단, "면적 비례성이 틀렸다"가 아니라 **"면적 축소로는 이 신고를
못 고친다"**가 기각 사유다. 이 구분이 중요하다.

### (3) 【선행 확인 1건】 이 실험이 애초에 유효했는가 — **코드상 무효일 가능성이 실재한다**

`WindowsOverlayStateEnforcer.cs:201/239`(및 macOS 형제 `:313/385`):
```
if (_fullScreenBoundsApplied || _fullScreenApplyAttempts >= MaxFullScreenApplyAttempts) return;
...
if (ok) _fullScreenBoundsApplied = true;
```
**`_fullScreenBoundsApplied`는 한 번 true가 되면 다시 리셋되지 않는다.** 재적합 트리거도 없다
(`shouldFitMonitor=false`라 라이브러리의 `OnMonitorChanged` 경로도 no-op).

→ **앱을 켜 둔 채 해상도를 바꿨다면 우리 창은 3840×2160 크기 그대로 남는다.**
그 경우 **표면적이 전혀 줄지 않았고, 이 실험은 아무것도 측정하지 않은 것**이다.

**확인 방법(30초, 둘 중 아무거나):**
- ① 1920×1080으로 바꾼 뒤 **앱을 재시작**하고 `Player.log`에서 `Screen=(1920x1080)` 확인.
- ② 같은 상태에서 **`StickMate.exe` 자기 CPU**가 4K 때보다 눈에 띄게 낮은지 확인.
  앱 **자기** 렌더 비용은 면적에 정직하게 비례하고 픽셀이 1/4이므로 **이건 작업관리자로도 보인다.**
  **dwm뿐 아니라 StickMate 자기 CPU까지 그대로였다면 → 실험 무효(창이 안 줄었다)가 거의 확정**이다.

**★ 이건 별건의 실제 결함이기도 하다(리더 보고).** 해상도/모니터 변경 시 오버레이가 재적합하지
않는다 → 고해상도에서 저해상도로 바꾸면 창이 데스크톱 밖으로 넘치고, 반대 방향이면 화면을 덜 덮는다.
좌표계(`OverlayOriginOsScreen`)도 함께 어긋난다. **소형 창과 무관하게 존재하는 버그다.**

### (4) MSAA(B-3)로 방향을 옮기는 것이 맞는가 — **부분적으로만 맞다**

**주의: 해상도 4배 축소는 BitBlt 복사만 줄인 것이 아니다.** MSAA resolve도, 앱 자체 래스터화도
**똑같이 4배** 줄였다. 따라서 그 실험이 유효했는데 아무 변화가 없었다면, 그것은
**"픽셀 처리량" 가설 전체(B-1 · B-3 · B-4)를 한꺼번에 약화시킨다.** MSAA만 따로 살아남지 않는다.
→ **"B-1이 죽었으니 B-3으로"는 논리적으로 성립하지 않는다. 둘은 같은 축이다.**

### (5) 그러면 남는 축 — **면적에 의존하지 않는 비용**

1. **present 횟수**(면적 무관 고정비. 6-2에서 10.6%p 중 **3.8%p**로 이미 격리 측정됨).
2. **【신규 가설】 BitBlt 스왑체인의 이진(binary) 페널티.** flip model이면 DWM이 앱 버퍼를
   디스플레이 컨트롤러에 직접 넘기는 경로(direct flip / MPO)를 쓸 수 있다. 레거시 BitBlt +
   레이어드 창은 그 경로를 **무효화**하고 데스크톱 전체를 매 프레임 합성하게 만든다.
   **이 페널티는 우리 창 면적에 비례하지 않는다 — 켜지거나 꺼지거나다.**
   이 가설 하나가 관측 4개를 전부 설명한다:
   (a) 시스템 전체가 느려짐 (b) `dwm.exe` CPU는 0에 가까움(일이 GPU 합성 쪽) (c) 우리 창을
   4배 줄여도 무변화 (d) macOS에서는 면적 귀속률이 64%로 보였던 것(macOS엔 이 이진 페널티가 없다).
3. 메인스레드 스톨 / 창 열거 폴링 등.

**따라서 다음 조사 방향은 MSAA가 아니라 "면적이 아닌 축"이고, 그중 코드 변경 0으로 즉시 테스트
가능한 것이 present 횟수다.** `STICKMATE_FORCE_TIER` 환경변수가 **이미 구현돼 있다**
(`FramePacing.ReadEnvTier`, 미지정 시 제품 동작 영향 0, 적용에는 앱 재시작 필요).
등급을 강제하면 **면적을 고정한 채 present 횟수만** 바꾼다 — 남은 두 축을 가르는 유일한 무료 실험이다.
- present를 크게 줄여도 체감이 안 변하면 → **2번(이진 페널티)이 유력**해지고, 그러면 면적/프레임률
  최적화로는 못 고친다는 뜻이다. 남는 선택지는 (i) 수용 (ii) **사용자 토글**(업계 선례:
  Rusty's Retirement의 "Low Power Mode") (iii) 레이어드 창을 안 쓰는 근본 재설계뿐이다.
- 체감이 변하면 → **FramePacing 튜닝만으로 해결. 공사 0.**

### (6) 우선순위 (갱신)

| 순위 | 할 일 | 비용 | 무엇을 가르는가 |
|---|---|---|---|
| **1** | **`Player.log` 확보** | 0분 | ① 실험 유효성(`Screen=(WxH)`) ② **MSAA 실제 배수** ③ 디스플레이 배율 ④ GPU/`graphicsDeviceType` ⑤ FramePacing 등급 — **한 파일이 다섯 질문에 답한다** |
| **2** | 1080p + **앱 재시작** 상태에서 `StickMate.exe` 자기 CPU 하락 여부 | 1분 | (3)의 실험 유효성 확정 |
| **3** | `STICKMATE_FORCE_TIER` A/B (Active vs Away) | 5분, 코드 변경 0 | **면적이 아닌 축**(present 횟수 vs 이진 페널티) |
| 4 | `measure-dwm.ps1` CSV | 4분 | 위 셋이 정성적으로 갈리면 생략 가능 |

### (7) B-1 처분 (확정 문구)
**조건부 잠정 기각.** (2)의 천장 논증이 근거이며 계측 정밀도와 무관하게 성립한다.
**단 (3)에서 "실험 무효(창이 안 줄었음)"로 판명되면 이 기각을 철회하고 재실험한다.**
어느 쪽이든 **6-5의 "착수 보류" 권고는 그대로 유효**하며, 이제 그 위에 **"기각 권고"가 얹힌다.**

## 6-9. 해상도 변경 시 472ms/1091ms 프레임 스파이크 — 원인 규명

사용자 확증: 그 스파이크는 **앱 실행 중에 OS 디스플레이 해상도를 바꾼 시점**에 발생했다(재시작 없음).
(수치의 출처는 `FramePacing.FrameTimeStats` — **30초 구간의 최댓값**이다. 즉 30초 중 **한 프레임**이
1.09초였다는 뜻이지 지속 저하가 아니다.)

### (1) 가설 (a) "상시 재적용 루프가 동기 블로킹" — **코드로 반증됨**

| 로그 줄 | 실제 코드 | 판정 |
|---|---|---|
| `전체화면 확장 시도 N/6` | `TickFullScreenBounds()`. **유일하게 `Screen.SetResolution`(스왑체인 재생성)을 부르는 곳**(`:228`). 그런데 `:201`에서 `_fullScreenBoundsApplied \|\| _fullScreenApplyAttempts >= 6`으로 가드되고 `:239`에서 성공 시 sticky true. **리셋 경로 없음** | **재실행 불가.** 디스플레이 변경으로 다시 돌지 않는다 |
| `재적용 X/5` | 목표 상태 4개 대입 + 로그. **`_timer < ReapplyIntervalSeconds(0.5s)`면 즉시 return** | **구조적으로 한 프레임에 몰릴 수 없다.** 0.5초 간격 강제. 호출도 값 대입 4번(µs~ms) |
| `오버레이 창 원점/배율 갱신` | `CaptureOverlayOrigin()` — `GetWindowRect` + `GetDpiForWindow`. **원점이 움직였을 때만 로그** | 해상도가 바뀌면 원점이 바뀌므로 **이 줄이 뜨는 것은 정상**. 비용은 µs |

### (2) 가설 (b) "해상도 변경 이벤트를 감지해 재계산하는 별도 경로" — **그런 경로가 없다**

전수 확인 결과:
- `Screen.SetResolution`은 **프로젝트 전체에서 `TickFullScreenBounds` 한 곳**뿐이다(Windows `:228`, macOS `:371`).
- `UniWindowController.OnMonitorChanged` **구독자가 우리 코드에 하나도 없다**. 라이브러리 자신의
  `UpdateMonitorFitting()`은 `shouldFitMonitor=false`라 no-op다.
- `WM_DISPLAYCHANGE` 처리도 없다(창 프로시저를 UniWindowController 네이티브가 소유).

→ **우리 앱에는 디스플레이 변경에 반응하는 코드가 아예 존재하지 않는다.** (a)도 (b)도 아니다.

### (3) 그러면 그 1초는 무엇인가 (순위)

1. **엔진/드라이버 측 백버퍼·스왑체인 재생성.** `WM_DISPLAYCHANGE` → Unity가 4K 백버퍼를
   **레거시 BitBlt 스왑체인**(`boot.config: force-d3d11-bitblt-model=`)으로 재할당한다. 메인스레드
   동기 드라이버 할당이라 수백 ms는 정상 범위다. **모드 변경 순간에는 데스크톱 전체가 멈춘다 —
   모든 앱이 그렇다.**
2. **우리 쪽 uGUI 연쇄 리빌드(유일하게 우리 코드인 후보).** `Screen.width/height` 변경 →
   `ResolveCanvasScaleFactor` 값 변경 → **캔버스 4~5개가 같은 프레임에 `scaleFactor`를 갱신**
   (`DialogueBubbleRenderer:719`, `CharacterInfoWindow:1508`, `GearRadialMenuWidget:951`,
   `AppControlDirector:907`) → 전체 레이아웃 리빌드 + 동적 폰트 아틀라스 재래스터화.
   다만 이것도 **재적용 루프가 아니라 엔진의 화면 크기 변경이 촉발**한 것이다.
3. 초상화 `RenderTexture` 재할당(`CharacterPortraitStage:357`) — 정보창이 열려 있을 때만, 소액.

**1과 2를 로그 없이 분리할 수는 없다.** 가르는 방법: `Player.log`에서 `전체화면 확장 시도 1/6`이
**스파이크 시점에도** 찍혔는지 본다. 찍혔다면 앱이 재시작된 것이고(= 스파이크는 기동 비용, 완전 무해),
안 찍혔다면 1+2다.

### (4) 이것이 일상적 상황인가 — **아니다. 그리고 신고된 증상도 아니다**

- **멀티모니터 경계 넘기로는 이 경로를 타지 않는다.** 우리 창은 **기동 이후 한 번도 움직이지 않는다**
  (`windowPosition` 대입은 sticky 가드된 `TickFullScreenBounds` 안에만 있다). 카메라도 고정이다.
  애초에 캐릭터는 창이 덮은 **모니터 1개**를 벗어날 수 없다.
- 촉발 조건: 해상도 변경 / 모니터 추가·제거 / DPI 배율 변경 / **디스플레이 절전 복귀** / RDP /
  GPU 드라이버 리셋. 대부분 드물다. **다만 절전 복귀는 24시간 상주 앱에서 매일 일어날 수 있다** —
  유일하게 일상적인 경우이며 별도로 확인할 가치가 있다.
- **결정적으로, 1초짜리 단발 히치는 사용자가 신고한 "지속적 시스템 저하"와 증상 자체가 다르다.**
  → **이 스파이크는 내가 요청한 실험이 만들어낸 산물이지, 신고된 버그의 증거가 아니다.**
  (6-6의 `00%`, 6-8의 "체감 무변화"와 같은 규율을 여기에도 적용한다 — 눈에 띄는 숫자가 나왔다고
  조사 방향을 옮기지 않는다.)

### (5) 【진짜 버그】 스파이크가 아니라 **그 뒤에 영구히 남는 상태**다 — coder 이관

`_fullScreenBoundsApplied`가 sticky여서 **디스플레이가 바뀌어도 재적합이 영원히 일어나지 않는다.**
그 결과 재시작 전까지:
- 창 크기/위치가 **옛 화면 기준으로 고정**된다(4K→1080p면 폭 3840 창이 폭 1920 데스크톱에 남는다).
- `Screen.SetResolution`도 다시 안 불려 **Unity 백버퍼와 창이 어긋난다**.
- `CaptureOverlayOrigin`은 폴링으로 계속 돌아 **원점만 갱신**되므로, 크기 전제만 틀린 **반쪽 상태**가
  되어 커서↔월드 좌표가 어긋난다.
- **★ 그리고 이것이 6-8의 해상도 실험을 무효로 만든다 — 표면적이 실제로는 줄지 않았다.**
  "체감 차이 없음"의 가장 단순하고 유력한 설명이 바로 이것이다.

**이관 지점(정확한 위치)**
- `Assets/_Project/Scripts/Platform/Windows/WindowsOverlayStateEnforcer.cs`
  — 필드 `_fullScreenBoundsApplied`(`:65`), 가드(`:201`), 설정(`:239`), `TickFullScreenBounds()`.
- 형제 파일 `Assets/_Project/Scripts/Platform/MacOS/MacOverlayStateEnforcer.cs` — `:151 / :313 / :385`.
  **양쪽 동일 결함**이다(한쪽만 고치는 재발 경로를 만들지 말 것 — `VisibleTopEdgeSolver` 도입 교훈).

**설계 지침 (구현은 coder, 성능 요구는 perf-doc 담당)**
1. 디스플레이 토폴로지 변경을 감지하면 `_fullScreenBoundsApplied=false`, `_fullScreenApplyAttempts=0`
   으로 **re-arm**만 하면 된다. 감지 신호는 **이미 있는 API로 충분**하다:
   `UniWindowController.OnMonitorChanged`(라이브러리 네이티브 콜백) 또는
   `GetMonitorRect(i)`/`Screen.currentResolution` 폴링 비교. **신규 P/Invoke 불필요**
   (원칙: 네이티브는 `Platform/` 아래만).
2. **★ 재적합을 이벤트 프레임에 동기적으로 몰지 말 것.** 다행히 기존 구조가 이미 분산형이다 —
   `TickFullScreenBounds`는 0.5초 간격 최대 6회다. **리셋만 하면 자동으로 분산 재시도**가 된다.
3. **★ 디바운스 필수.** 모드 변경 1회에 이벤트가 연속으로 온다. 마지막 이벤트 후 **0.5~1.0초 안정**
   될 때까지 기다린 뒤 **1회만** 재적합할 것. 안 그러면 `Screen.SetResolution`(스왑체인 재생성)이
   연달아 불려 **지금 관측된 것보다 더 큰 히치를 우리가 직접 만들게 된다.**
4. 재적합 성공 직후 `ScreenCoordinateConverter.ReportOverlayWindowOsRect`를 **같은 프레임에 직접**
   호출(폴링 대기 금지) — 6-4 난제 3의 주의사항과 같은 이유.

### (6) 트랙 분리 권고
- **트랙 A(즉시, 작음)**: (5)의 재적합 버그 → coder. 구체적이고 즉시 고칠 수 있다.
- **트랙 B(계속)**: 신고된 지속적 시스템 저하 → **present 횟수 / BitBlt 이진 페널티**(6-8절 (5)).
  `STICKMATE_FORCE_TIER` A/B가 다음 실험이다. **트랙 A는 트랙 B의 답이 아니다.**
- **실험 재설계**: 해상도 실험을 다시 한다면 **반드시 해상도 변경 후 앱 재시작**. 트랙 A가 고쳐지기
  전까지는 그것이 유일한 유효 절차다.

## 6-10. 【확정】 Windows GPU 실측 — 비용의 위치가 처음으로 잡혔다

사용자 측정(작업관리자 자세히 탭, `dwm.exe` = "데스크톱 창 관리자" 행):

| | StickMate 종료 | StickMate 실행 | 차이 |
|---|---|---|---|
| **시스템 전체 GPU** | 1% | **54%** | +53%p |
| **`dwm.exe` GPU** | **0.6%** | **35.6%** | **+35.0%p (약 59배)** |
| `dwm.exe` CPU | 0.1% | 0.5% | +0.4%p |
| `dwm.exe` 메모리 | 335.0MB | 332.7MB | **−2.3MB** |
| GPU 엔진 | GPU 2 - 3D | GPU 2 - 3D | — |

**세 라운드 만에 처음으로 계측기가 신호를 분해했다.** 그리고 이 결과는 이전 라운드들의 판단을
사후적으로 검증한다.

### (1) 무엇이 확정됐고, 무엇은 아닌가

**확정된 것**
- **비용의 위치**: 우리 프로세스가 아니라 **컴포지터(`dwm.exe`)의 GPU 작업**이다. 이 앱의 비용 모델
  ("앱 CPU는 낮은데 시스템이 느려진다 = 비용이 프로세스 밖에 있다")이 **Windows에서 직접 확인됐다.**
- **크기**: `dwm.exe` 단독으로 **+35.0%p GPU**. 사용자가 신고한 "시스템 전체가 느려짐"의 실체다.
- **`dwm.exe` CPU가 쓸모없는 계측기였다는 것**: 같은 사건이 CPU 열에서는 0.1% → 0.5%다.
  6-6에서 "CPU 00~01%는 반증이 아니라 계측기 분해능 문제"라고 한 판단이 **실측으로 확증됐다.**

**확정되지 **않은** 것**
- 이 비용이 **BitBlt 고유**인지. 투명 항상위 창은 어떤 합성 모델에서도 DWM 일을 늘린다.
- **present 횟수와의 관계**(← 이것이 남은 유일한 실용적 질문이다. (4) 참고).
- 시스템 GPU 53%p 중 **`StickMate.exe` 자신의 몫**(추정 약 18%p, 미확인).

### (2) 메모리는 같은데 GPU만 다르다 — 두 가지를 뜻한다

1. **DWM의 합성 표면은 시스템 메모리 작업 집합에 잡히지 않는다.** GPU 전용/공유 메모리에 있다.
   → 6-7에서 메모리 열을 주 계측기에서 강등한 판단이 옳았다. **이제 완전히 폐기한다**:
   어제의 `+7.45MB`는 **노이즈였다**(오늘 같은 축이 **−2.3MB**로 부호가 반대다). 그 위에 세웠던
   "1920×1080 표면과 일치" 추론은 6-7에서 이미 철회했고, 이것으로 확인 사살됐다.
2. **더 중요한 함의 — 비용의 성격.** 메모리(=1회 할당)는 그대로인데 GPU(=반복 작업)만 59배다.
   즉 **비용은 "표면을 하나 더 들고 있는 것"이 아니라 "매 프레임 반복되는 합성 작업"**이다.
   → **present 횟수 축이 살아 있다.**

### (3) 【정정】 "Copy 엔진"이 아니라 "3D 엔진"이다 — 내 메커니즘 서술이 부정확했다

나는 6-6에서 "BitBlt 복사는 GPU **카피 엔진**이 수행한다"고 썼다. 실측은 **3D 엔진**이다. 정정한다.

실제로 일어나는 일:
- DWM의 데스크톱 합성은 원래 **3D 파이프라인의 셰이더 작업**이다(레이어를 텍스처로 블렌딩).
- 불투명 전체화면 앱만 있으면 DWM은 **direct flip / MPO**로 **합성 자체를 건너뛴다** — 그래서
  StickMate 종료 시 **0.6%**다(사실상 0).
- **레이어드/투명 창이 하나라도 있으면 그 경로가 깨지고, 데스크톱 전체를 매 프레임 3D로 재합성한다.**

→ **실측이 지지하는 것은 "복사 비용"이 아니라 6-8 (5)에서 세운 "이진(binary) 페널티" 가설이다.**

### (4) 세 관측이 한 가설로 수렴한다 — **B-1 정식 기각**

| 관측 | 출처 |
|---|---|
| (a) `dwm.exe` CPU ≈ 0인데 GPU 35.6% → 일이 **GPU 합성 파이프라인**에 있다 | 6-10 |
| (b) 우리 창 면적을 **4배 줄여도 변화 없음** → 비용이 **우리 창 면적에 비례하지 않는다** | 6-8 |
| (c) Copy가 아니라 **3D 엔진** → 복사가 아니라 **합성**이다 | 6-10 |

→ **비용 = (데스크톱 전체 재합성) × (present 횟수). 우리 창 크기는 부차적이다.**

**따라서 제안 B-1(소형 오버레이 창)을 6-8의 "조건부 잠정 기각"에서 정식 기각으로 격상한다.**
근거가 천장 논증(남는 몫 21.1%)에서 **메커니즘 논증**으로 바뀌었다:
**DWM이 재합성하는 것은 우리 창이 아니라 데스크톱 전체이므로, 우리 창을 640×640으로 줄여도
그 비용은 줄지 않는다. 구조적으로 줄일 수 없다.**

**B-3(MSAA) / B-4(렌더 해상도)의 처분**: 이들은 **우리 렌더 타깃**의 비용이므로 `dwm.exe`가 아니라
`StickMate.exe` 자기 GPU(추정 약 18%p)에만 듣는다. **지배적 항(35.0%p)이 아니므로 부차적**이지만,
18%p도 작지 않아 완전 폐기는 아니다. → `StickMate.exe` 행 수치 확인 후 재판정.

### (5) 【신규 가설, 중요】 크로스 어댑터 복사 — "GPU 2"라는 표기

`dwm.exe`가 **`GPU 2`**에서 돈다. 인덱스가 2라는 것은 이 PC에 **어댑터가 여러 개**(내장 + 외장 등)라는
뜻이다. 만약 `StickMate.exe`가 **다른 어댑터**에서 렌더한다면, 그 결과 표면을 매 프레임
**어댑터 간(PCIe) 복사**해야 한다. 하이브리드 그래픽 노트북에서 흔하고, **"시스템 전체가 느려진다"의
매우 유력한 설명**이며 지금 수치와도 모순되지 않는다.

- **확인(10초)**: 작업관리자 자세히 탭에서 **`StickMate.exe` 행의 "GPU 엔진" 열**을 본다.
  `GPU 2 - 3D`면 같은 어댑터(가설 기각). `GPU 0 - 3D` / `GPU 1 - 3D`면 **크로스 어댑터 확정**.
- **수정(코드 변경 0)**: 설정 > 시스템 > 디스플레이 > 그래픽 > `StickMate.exe` 추가 →
  `dwm.exe`와 **같은 GPU**로 지정. **사용자 설정 1회로 끝난다.**
  맞다면 이번 조사 전체에서 **가장 싼 해법**이다.

### (6) 트랙 B를 이 실측으로 끝낼 수 있는가 — **아니다. 그러나 훨씬 쉬워졌다**

남은 유일한 실용적 질문: **이 35.0%p가 present 횟수에 비례하는가?**
이진 페널티가 "켜짐/꺼짐"이라 해도, **켜진 상태의 비용은 우리가 새 프레임을 낼 때마다 발생**할
가능성이 높다(damage가 없으면 DWM이 재합성할 이유가 없다). 이것이 갈리면 처방이 완전히 달라진다.

**그런데 이제 계측기가 확보됐다.** 35.6% vs 0.6%는 거대한 신호라 **작업관리자 GPU 열만으로 판정
가능하다** — PowerShell CSV도, 페어드 설계도 필요 없다.

**★ 사전 예측 등록(결과가 나오기 전에 못 박는다 — 사후 합리화 방지)**

Windows에서는 `vSyncCount=0`이고 등급이 `targetFrameRate`를 나눈다(`FramePacingPolicy` `:276`,
`FramePacing.ApplyWindows`). 따라서 present 횟수가 등급에 정비례한다.

| 강제 등급 | present | `dwm.exe` GPU 예측 |
|---|---|---|
| `Active` (60fps) | 60/s | **35.6%** (관측됨) |
| `Calm` (30fps) | 30/s | **약 18%** |
| `Away` (15fps) | 15/s | **약 9%** |

- **예측대로 비례하면** → present 축 확정 → **`StickConfig.windowsTargetFrameRate`(`:2317`, 현재 60)
  튜닝만으로 해결된다. 신규 공사 0. 인프라는 이미 다 있다.**
- **등급을 낮춰도 30% 근처에 머물면** → 순수 이진 페널티 → 프레임률로는 못 고친다.
  남는 선택지는 (i) 수용 (ii) **사용자 토글**(업계 선례: Rusty's Retirement "Low Power Mode")
  (iii) 레이어드 창 탈피 재설계.

**실행 방법(재빌드 불필요 — 환경변수는 기동 시 1회만 읽는다)**
```
cmd:         set STICKMATE_FORCE_TIER=Away
             "C:\...\StickMate.exe"
PowerShell:  $env:STICKMATE_FORCE_TIER="Away"; & "C:\...\StickMate.exe"
```
등급 이름: `Active` / `Calm` / `Away` (대소문자 무관). 적용 확인은 `Player.log`의
`★ STICKMATE_FORCE_TIER=... 강제 지정됨(계측용)` 줄.

### (7) 측정 자체에 대한 주의 1건
두 스크린샷이 "실행 vs 종료"인지 "유휴 vs 상호작용"인지 사용자 메시지만으로는 불확실하다.
**다만 "종료"로 보는 것이 타당하다**: 유휴 상태여도 FramePacing이 최저 15fps로는 present하므로
`dwm.exe` GPU가 **0.6%까지 떨어질 수 없다**. 0.6%는 "합성할 투명 창이 아예 없다"의 값이다.
그래도 다음 측정 때 상태를 명시적으로 라벨링해 확인한다.

## 6-11. "유휴인데 35.6% → 69% → 75%로 상승" — 해석 및 6-10 예측표 **정정**

추가 관측: 사용자가 **아무 상호작용도 하지 않은 상태**에서 `dwm.exe` GPU가 35.6% → 69% → 75%로
**올라가는 것을 관찰**했다.

### (1) 누적/누수 경로 코드 감사 — **찾지 못했다**

리더가 지목한 "시간이 지날수록 뭔가 쌓인다" 후보를 전수 확인했다.

| 후보 | 확인 결과 |
|---|---|
| 재적용 루프가 반복 실행되며 창 스타일을 계속 다시 건다 | **아니다.** `MarkDirty()`를 부르는 것은 `SetClickThrough`/`SetAlwaysOnTop`/`SetTransparent` 3개뿐이고, `SetClickThrough`의 호출부는 `StickmanAgent`의 **기동 1회(`:478`)와 긴급 해제 키(`:508`)** 둘뿐이다. 주기 호출 경로가 없다. 재적용은 5회로 끝나고 그 뒤 `Update`는 early return 한다 |
| 서페이스 재생성이 반복된다 | **아니다.** `Screen.SetResolution`은 프로젝트 전체에서 sticky 가드된 `TickFullScreenBounds` 한 곳뿐(6-9). 초상화 `RenderTexture`도 크기가 같으면 재생성하지 않는다(`CharacterPortraitStage:355`) |
| 매 프레임 GPU 리소스를 새로 만든다 | **아니다.** `new Material` / `new Texture2D` / `Instantiate` 호출부가 **프로젝트 전체에 9곳**이고, **`Update`/`LateUpdate`/`FixedUpdate` 안에는 0곳**이다 |
| 창이 계속 늘어난다 | **불가.** Unity Player는 프로세스당 OS 창이 1개다 |

**그리고 원리적으로도 맞지 않는다**: `dwm.exe`의 GPU 비용은 **우리가 무엇을 그리는지와 무관**하다.
DWM은 (합성 레이어 수) × (데스크톱 면적) × (재합성 빈도)의 함수다. 우리 쪽에 리소스가 쌓여도
그것은 **`StickMate.exe`의 GPU**에 잡히지 `dwm.exe`에 잡히지 않는다.

### (2) 가장 유력한 설명 — **GPU 다운클럭에 의한 백분율 팽창(또 계측기 문제다)**

작업관리자의 GPU %는 **"엔진이 바빴던 시간 비율"**이지 절대 작업량이 아니다. 시스템이 유휴가 되면
GPU는 전력 절감을 위해 **클럭을 크게 낮춘다**. 그러면 **똑같은 절대 작업량이 훨씬 큰 시간 비율을
차지한다.**

이 설명은 관측과 정확히 맞는다:
- **"아무것도 안 했는데" 올라간다** — 아무것도 안 할수록 클럭이 더 내려가니까 %가 더 오른다.
  누수라면 유휴 여부와 무관하게 올라야 한다. **관측된 상관관계가 누수 가설과 반대 방향이다.**
- 35.6% → 69% → 75%는 클럭이 단계적으로 내려간 것과 부합한다(약 2배 = 클럭 절반).

**★ 그리고 이것은 위험한 함정이다**: present를 줄이면 GPU가 더 한가해져 클럭이 더 내려가고,
**절대 작업량은 줄었는데 백분율은 오히려 올라갈 수 있다.** 즉 **FramePacing이 제대로 동작해도
GPU %만 보면 "효과 없음"으로 오독하게 된다.**

**판별법(가장 싸고 결정적)**: `dwm.exe`가 오를 때 **`StickMate.exe`의 GPU %도 함께 오르는지** 본다.
- **함께 오른다** → 다운클럭(둘 다 같은 GPU를 쓰므로 같은 배수로 팽창). 절대 작업량은 그대로.
- **`dwm.exe`만 오른다** → 진짜 누적. 그때 다시 판다.

### (3) "유휴인데 높다"가 FramePacing의 반증인가 — **아니다. 등급이 애초에 안 내려갔을 것이다**

두 가지 이유가 **코드에 있다**:

1. **`Calm` 등급은 "사용자가 유휴"가 아니라 "캐릭터가 `Idle` 상태"를 요구한다.**
   `FramePacing.ResolveCharacterIdle()`은 `Machine.CurrentStateId == StickmanStateId.Idle`일 때만
   참이다. 그런데 **이 앱의 캐릭터는 스스로 돌아다닌다.** 사용자가 아무것도 안 해도 캐릭터가
   `Walk`/`Parkour` 중이면 등급은 **`Active`(60fps) 그대로**다.
   → **"사용자가 아무것도 안 했다"와 "앱이 유휴다"는 이 앱에서 같은 말이 아니다.**
2. **`Away` 등급은 사용자 무입력을 요구하는데, 작업관리자를 들여다보는 행위 자체가 입력이다.**
   (`WindowsViewerPresenceService`는 `GetLastInputInfo`로 판정한다.) 관찰하려면 마우스를 움직여야
   하고, 그러면 `Away`로 내려갈 수 없다. **관측 행위가 관측 대상을 바꾸는 전형적 함정이다.**

→ 그러므로 이 관측은 **"present 횟수 감소가 효과 없다"의 증거가 아니다.**
실제 등급이 무엇이었는지는 `Player.log`의 등급 전환 로그로만 알 수 있다.
**그리고 이것이 `STICKMATE_FORCE_TIER`가 반드시 필요한 이유다 — 등급을 강제하면 위 두 함정이
동시에 제거된다.**

### (4) 【정정】 6-10의 예측표는 GPU %로 판정할 수 없다

6-10에서 "`Calm` → 약 18%, `Away` → 약 9%"로 예측했다. **(2)의 다운클럭 효과 때문에 이 예측은
GPU % 지표로는 검증할 수 없다.** 정정한다. 판정 지표를 다음으로 바꾼다:

| 지표 | 쓰는 법 | 다운클럭에 안전한가 |
|---|---|---|
| **① 사용자 체감(엑셀 작업이 부드러워지는가)** | `FORCE_TIER=Away`로 띄우고 평소 작업을 해 본다 | **안전.** 신고된 증상 자체가 주관적이므로 주관 지표가 타당하다(6-8과 같은 논리) |
| **② `dwm.exe` GPU % 와 `StickMate.exe` GPU % 를 **함께** 기록** | 두 값의 **비율** 변화를 본다 | **안전.** 다운클럭은 둘을 같은 배수로 민다 |
| **③ 시스템 전체 GPU %: 앱 실행 vs 종료** | 이미 1% vs 54%로 관측됨 | 조건부 안전(둘 다 유휴 상태에서 재면) |
| ~~`dwm.exe` GPU % 단독 절대값~~ | — | **위험. 쓰지 말 것** |

### (5) 그래도 (2)가 바꾸지 않는 것 — 6-10의 핵심 결론은 그대로다

다운클럭 효과는 **"실행 vs 종료" 비교를 무너뜨리지 않는다.** 종료 상태에서도 시스템은 똑같이
유휴였고(오히려 더 유휴), 그때 `dwm.exe`는 **0.6%**였다. 즉:
- **비용의 위치(dwm GPU)와 존재는 여전히 확정**이다.
- **B-1 정식 기각도 그대로다**(근거는 메커니즘 논증이지 백분율 크기가 아니다).
- 바뀐 것은 **"present 축을 어떤 지표로 검증할 것인가"** 하나뿐이다.

## 6-12. 【중요】 "떨어졌다 올랐다 한다"(등락) — 누수 기각 확정, 그리고 **새로운 유력 해석**

사용자 정정: 상승이 **단조 증가가 아니라 등락(떨어졌다 올랐다)**이었다.

### (1) 누수/누적 가설 — **기각 확정. 이 선은 닫는다**

두 개의 독립 증거가 일치한다:
- **코드 감사**(6-11 (1)): 누적 경로가 없다(MarkDirty 주기 호출 없음, `Screen.SetResolution` sticky
  1곳, `new Material/Texture2D/Instantiate`가 `Update` 계열 안에 0곳, 창 1개 고정).
- **사용자 관측**: 등락한다. **누적이라면 단조 증가해야 한다.**

→ 더 이상 이 방향을 파지 않는다.

### (2) 등락 폭이 **약 2배**다 — 이것이 새 정보다

35.6% → 69%는 **1.94배**, 35.6% → 75%는 **2.11배**. **정확히 2배 근처**다.
그리고 `FramePacingPolicy`에서 **`Calm` 등급의 divisor가 정확히 2**다(60fps → 30fps).

후보가 두 개로 좁혀진다:

| 가설 | 기구 | 무엇과 상관되는가 |
|---|---|---|
| **A. GPU 다운클럭** | 절대 작업량은 그대로인데 클럭이 내려가 %가 팽창 | **시스템 전체 유휴도** |
| **B. FramePacing 등급 전환(신규·유력)** | 캐릭터 `Idle`↔`Walk`에 따라 등급이 `Calm`↔`Active`로 뒤집히며 **present가 30↔60fps로 실제로 두 배 변한다** | **캐릭터가 움직이는가** |

**B가 특히 유력한 이유 — `CalmDwellSeconds = 0.75f`(`FramePacing.cs:254`)**
`Calm`은 캐릭터가 **0.75초만** `Idle`이면 걸리고, 움직이는 순간 `_idleDwellSeconds = 0`으로 즉시
풀린다(`:380`). 즉 **자율 배회하는 이 캐릭터에서는 등급이 몇 초 단위로 계속 뒤집힌다.**
present가 60↔30으로 왕복하고, dwm GPU가 그것을 그대로 따라가면 **관측된 ~2배 등락이 정확히 설명된다.**

### (3) B가 맞다면 — **트랙 B는 이미 답이 나온 것이다**

그 경우 사용자는 **자기도 모르게 `FORCE_TIER` 실험을 이미 수행한 셈**이다.
앱이 스스로 60fps ↔ 30fps를 왕복했고 `dwm.exe` GPU가 그것을 **약 2배로 따라갔다** =
**present 횟수가 dwm 비용을 지배한다는 실사용 중 직접 증거.**

그러면 처방이 이미 손에 있다:
- **`StickConfig.windowsTargetFrameRate`(`:2317`, 현재 60)를 낮추면 즉시 비례해서 준다.**
  30이면 절반, 20이면 1/3. **신규 코드 0줄. 인프라는 이미 전부 있다.**
- 추가로 `Calm` 진입 조건을 넓히는 것(캐릭터 배회 자체를 덜 자주 만들기)도 같은 축이다.

**★ 단, 리더 결정이 필요하다 — 사용자 확정값과 충돌한다.**
`ViewerPresence.cs:73-75`에 **"2026-08-31 사용자 확정: 움직일 때는 60fps. 여기는 절대 건드리지
않는다"**가 명시돼 있다. `windowsTargetFrameRate`를 내리는 것은 **그 확정값을 바꾸는 일**이므로
perf-doc이 단독으로 권고·적용할 사안이 아니다. **사용자에게 되물어야 한다**:
"부드러움(60fps)과 시스템 부하 중 어느 쪽을 택하겠는가. 30fps로 내리면 dwm 부하가 절반이 된다."

### (4) A와 B를 가르는 결정적 관찰 (30초, 무료)

두 가설 모두 `dwm.exe`와 `StickMate.exe`가 **함께** 움직이므로 동반 상승 여부로는 못 가린다.
**상관 대상**이 다르다:

> **작업관리자와 캐릭터를 나란히 30초 본다.**
> · **캐릭터가 걸어다닐 때 높고, 가만히 서 있을 때 낮아진다** → **B 확정**(등급 전환).
> · **캐릭터 동작과 무관하고 시스템 유휴도를 따라간다** → **A**(다운클럭).

보조 확인: `Player.log`의 **등급 전환 로그 타임스탬프**와 대조하면 추측 없이 확정된다.
(이것이 `Player.log`를 요청하는 네 번째 이유다.)

### (5) 측정 위생 — 앞선 수치의 재해석

**35.6%가 어느 등급에서 찍힌 값인지 우리는 모른다.** 만약 그 스냅샷이 `Calm`(30fps) 순간이었다면,
`Active`(60fps)의 실제 값은 **약 70%**이고 6-10의 "+35.0%p"는 **과소평가**다.
→ **앞으로 모든 Windows 수치는 그 순간의 등급(또는 최소한 "캐릭터가 움직이고 있었는가")을
함께 기록한다.** 등급을 모르는 단일 스냅샷은 최대 2배의 불확실성을 갖는다.

### (6) 바뀌지 않는 것
6-10의 핵심 결론은 그대로다: 비용의 위치(`dwm.exe` GPU)는 확정이고, **B-1 정식 기각도 유효**하다
(근거가 메커니즘 논증이라 절대 수치에 의존하지 않는다). 오히려 (3)이 맞다면 **B-1이 더욱 불필요**해진다 —
같은 효과를 설정값 하나로 얻을 수 있기 때문이다.

## 6-13. 크로스 어댑터 기각 + 다음 실험 우선순위 **재조정**

사용자 확인: `StickMate.exe`와 `dwm.exe`가 **같은 엔진(`GPU 2 - 3D`)**을 쓴다.
→ **6-10 (5)의 크로스 어댑터 가설 기각.** 어댑터 간 PCIe 복사는 없다. 무료 해법 후보 하나가 사라졌다.

**다만 결론은 바뀌지 않고, 한 가지가 오히려 선명해진다**: 둘이 **같은 3D 엔진을 공유·경합**한다.
그래서 시스템 GPU가 54%(= 포화 아님)인데도 체감이 나빠진다 — 포화가 아니라 **직렬화/지연**이다.
그리고 present를 줄이면 **`StickMate.exe`의 렌더 패스와 `dwm.exe`의 재합성이 동시에** 줄어
같은 경합 큐에서 **두 번 이득**을 본다.

### 【자기 정정】 ②(StickMate GPU% 동반 상승)는 생각보다 판별력이 낮다

6-12 (4)에서 이미 지적했듯 **A(다운클럭)와 B(등급 전환)는 둘 다 동반 상승을 예측한다**
(A는 절대량 고정·%팽창, B는 절대량 2배 — 어느 쪽이든 두 프로세스가 같이 움직이고 비율도 보존된다).
따라서 ②는 "dwm만 단독으로 튀는 제3의 이상"을 배제하는 정도의 값어치다. 그 사전 확률은 낮다.

### 【권고】 ③을 먼저 한다 — ②를 흡수하고 더 잘 답한다

`STICKMATE_FORCE_TIER`로 등급을 **고정**하면 다음이 **한 번에** 갈린다:

| 관찰 | A(다운클럭) 예측 | B(등급 전환) 예측 |
|---|---|---|
| **~2배 등락이 사라지는가** | **지속된다**(클럭 변동은 등급과 무관) | **사라진다**(원인이 제거됨) |
| dwm GPU 절대 수준 | 크게 안 변함 | **뚜렷이 내려감** |
| 사용자 체감 | 변화 없음 | **개선** |

**"등락이 사라지는가"는 다운클럭에 강건한 질적 지표다** — 이 조사를 세 라운드 괴롭힌 계측기 문제
(정수 CPU%, 메모리 노이즈, GPU% 팽창)를 전부 우회한다. 그리고 **체감**은 신고된 증상 자체가
주관적이므로 그것에 대한 타당한 지표다(6-8과 같은 논리).

**★ 사전 예측 등록(결과 나오기 전에 못 박는다)**
- `FORCE_TIER=Away`(15fps, `Active`의 1/4): present 축이 지배적이라면
  **① ~2배 등락이 멈추고 ② dwm GPU가 `Active` 수준의 대략 1/4로 내려가고 ③ 체감이 개선**된다.
- 셋 중 하나라도 어긋나면 present 축은 지배적이지 않다 → **이진 페널티 엔드게임**
  (수용 / 사용자 토글 / 레이어드 창 탈피).

**실행(재빌드 불필요, `cmd`)**
```
set STICKMATE_FORCE_TIER=Away
"C:\...\StickMate.exe"
```
확인: `Player.log`에 `★ STICKMATE_FORCE_TIER=Away 강제 지정됨(계측용)`.
**주의**: 15fps라 캐릭터 움직임이 뚝뚝 끊겨 보이는 것은 **정상이며 의도된 계측 조건**이다
(제품 설정 제안이 아니다). `Away`가 효과가 있으면 그다음 `Calm`(30fps)로 올려 **수용 가능한
타협점**을 찾는다.

### 다음 단계 (최종)
1. **③ `FORCE_TIER=Away` 체감 + 등락 소멸 관찰** ← **최우선. ②를 흡수한다**
2. ③이 긍정이면 `Calm`(30fps)로 재시도해 타협점 탐색 → **처방은 `windowsTargetFrameRate` 한 값**
   (신규 코드 0). **단 `ViewerPresence.cs:73-75`의 사용자 확정값("움직일 때는 60fps")과 충돌하므로
   반드시 사용자에게 되물을 것**(6-12 (3)).
3. `Player.log`(여전히 미수령) — MSAA 실제 배수 + 등급 전환 타임스탬프로 위 결론을 추측 없이 확정.
4. ②는 ③이 애매할 때만 보조로.

---

## 6-14. 오늘 라운드 종료 — 확정 / 미확정 / 내일 할 일

Windows 실기 확인이 내일로 미뤄져 이 시점에서 라운드를 닫는다.

### (1) 확정된 것 — 더 이상 재론하지 않는다

1. **비용의 위치**: 우리 프로세스가 아니라 **`dwm.exe`의 GPU 합성**이다.
   실측 `dwm.exe` GPU **0.6%(앱 종료) → 35.6%(앱 실행)**, 시스템 전체 **1% → 54%**.
   같은 사건이 CPU 열에서는 0.1% → 0.5%로, **CPU 열은 이 신호를 분해하지 못한다**(6-6의 판단 확증).
2. **기구는 "복사"가 아니라 "데스크톱 전체 재합성"**이다. 실측 엔진이 Copy가 아니라 **3D**였고
   (내 초기 서술 정정), 불투명 전체화면만 있으면 DWM은 direct flip/MPO로 합성을 건너뛴다
   (= 종료 시 0.6%). 투명 레이어드 창이 하나라도 있으면 그 경로가 깨진다.
3. **【정식 기각】 제안 B-1(캐릭터 추종 소형 오버레이 창).**
   근거는 메커니즘이다: **DWM이 재합성하는 것은 우리 창이 아니라 데스크톱 전체**이므로 창을
   640×640으로 줄여도 이 비용은 **구조적으로 줄지 않는다.** 세 관측이 수렴한다 —
   (a) dwm CPU≈0인데 GPU 35.6% (b) 우리 창 면적을 4배 줄여도 무변화 (c) Copy 아닌 3D 엔진.
   부수적으로, 천장 논증도 같은 방향이었다(4K→1080p가 이미 B-1 상한의 78.9%를 무료로 제공).
4. **부수 기각**: 크로스 어댑터 PCIe 복사(둘 다 `GPU 2 - 3D`), 리소스 누수/누적(코드 감사 결과
   누적 경로 0 + 사용자 관측이 단조 증가가 아니라 **등락**), 메모리 열 계측(노이즈, 부호가 뒤집힘).
5. **【별건 실제 버그, coder 이관 대기】** `_fullScreenBoundsApplied` sticky로 **디스플레이 변경 시
   오버레이가 영원히 재적합하지 않는다**(Windows `:65/:201/:239`, macOS `:151/:313/:385` 양쪽).
   상세·설계 지침은 6-9 (5).

### (2) 미확정 — 내일 이것 하나만 갈리면 된다

> **이 35%p가 present 횟수에 비례하는가?**

| 갈래 | 처방 | 공사량 |
|---|---|---|
| **A. present 축이 지배적** | `StickConfig.windowsTargetFrameRate`(`:2317`, 현재 60)를 낮춘다 | **신규 코드 0줄.** 인프라 이미 존재 |
| **B. 순수 이진 페널티** | 프레임률로는 못 고친다 → (i) 수용 (ii) **사용자 토글** (iii) 레이어드 창 탈피 재설계 | 대~특대 |

**현재 정황은 A 쪽으로 기울어 있다(확정은 아니다)**: 관측된 등락 폭이 **약 2배**(35.6→69=1.94배,
→75=2.11배)인데 `Calm` 등급의 divisor가 **정확히 2**이고, `CalmDwellSeconds = 0.75f`라
자율 배회하는 캐릭터에서는 등급이 **몇 초마다 `Active`↔`Calm`을 왕복**한다(6-12). 즉 사용자는
자기도 모르게 present 60↔30 A/B를 이미 돌리고 있었을 수 있다. **정황이지 증거가 아니다.**

### (3) 【내일 요청할 절차】 — 5분, 이것 하나면 끝난다

**1단계. `cmd`(명령 프롬프트)에서 아래 두 줄** (경로는 실제 exe 위치로)
```
set STICKMATE_FORCE_TIER=Away
"C:\...\StickMate.exe"
```

**2단계. 3~5분 평소대로 작업(엑셀 등)하며 세 가지만 본다**
1. **시스템이 덜 버벅이는가?** ← 가장 중요
2. 작업관리자 `dwm.exe` GPU%가 **왔다갔다 하지 않고 한 값에 머무는가?**
3. 그 값이 이전(35~75%)보다 **뚜렷이 낮은가?**

> ※ 캐릭터가 뚝뚝 끊겨 보이는 것은 **정상**이다 — 15fps로 강제한 **계측 조건**이지 제품 설정 제안이
> 아니다. 원복은 그냥 평소처럼 실행하면 된다(환경변수는 그 `cmd` 창에서만 유효).

**3단계(1번이 좋았을 때만).** 같은 방법으로 `Calm`(30fps)로 다시 해 본다 → **수용 가능한 타협점**을 찾는다.
```
set STICKMATE_FORCE_TIER=Calm
```

**적용 확인(선택)**: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\StickMate\Player.log` 에
`★ STICKMATE_FORCE_TIER=Away 강제 지정됨(계측용)` 이 있으면 걸린 것이다.
(가능하면 이 파일도 함께 보내주면 **MSAA 실제 배수**와 **등급 전환 이력**까지 추측 없이 확정된다.)

**★ 사전 예측 등록(결과 나오기 전에 못 박는다 — 사후 합리화 방지)**
`Away`에서 **① 등락이 멈추고 ② dwm GPU가 `Active`의 대략 1/4로 내려가고 ③ 체감이 개선**되면 → **갈래 A**.
**셋 중 하나라도 어긋나면 → 갈래 B.**
"등락이 멈추는가"는 **GPU 다운클럭에 강건한 질적 지표**라, 이 조사를 세 라운드 괴롭힌 계측기 문제
(정수 CPU%, 메모리 노이즈, GPU% 팽창)를 전부 우회한다.

### (4) 이것이 최종 결정 관문인가 — **그렇다. 단, 무관하게 진행 가능한 것이 하나 있다**

**맞다.** 갈래 A/B는 처방이 완전히 다르고(설정값 하나 vs 재설계), 다른 어떤 관측으로도 가를 수 없다.
남은 실측이 이것 하나뿐이므로 **내일 이 테스트가 최종 관문이다.**

**다만 결과와 무관하게 공통인 처방이 하나 있다 — 사용자 토글.**
- 갈래 A면: "부드러움(60fps) ↔ 시스템 부하" 선택지를 **사용자에게 노출**하는 형태가 된다.
  `ViewerPresence.cs:73-75`에 **"2026-08-31 사용자 확정: 움직일 때는 60fps"**가 박혀 있어
  기본값을 우리가 임의로 내릴 수 없기 때문이다(→ **리더가 사용자에게 되물어야 할 결정**).
- 갈래 B면: 토글이 **유일한** 답이다(업계 선례: Rusty's Retirement의 "Low Power Mode").

→ **어느 쪽이든 필요하므로, 토글 UI/설정 배선은 내일 결과를 기다리지 않고 착수 가능한 유일한 항목이다.**
(착수 여부는 리더 판단. perf-doc은 코드를 건드리지 않았다.)

## 6-15. 【macOS 실측】 컴포지터 부하는 present 횟수에 **정확히 비례**한다 — 이진 페널티 없음

Windows에서 미확정으로 남은 질문("부하가 present 횟수에 비례하는가, 아니면 이진 페널티인가")을
**macOS에서는 답할 수 있었다.** 실제 `StickMate.app`(프로토타입이 아님)으로 측정했다.

### (1) 측정 조건
- 환경: **Apple M2 Pro / Metal**, 화면 **3024×1964**(5.94Mpx), **MSAA 4x**(요청·실측 일치),
  기준 `vSyncCount=2`, `targetFrameRate=-1` (전부 `Player.log` 실측).
- **등급을 양쪽 다 강제**했다(`STICKMATE_FORCE_TIER=Active` / `=Away`). 강제하지 않으면 무입력
  상태에서 거버너가 임의로 내려가 "ACTIVE 위상"이 실제로는 Active가 아니게 된다(1차 시도의 결함).
  `open -n -a ... --env` 로 LaunchServices 경유 실행 — 셸에서 직접 실행하면 백그라운드 QoS를
  상속해 페이싱이 왜곡된다(`OverlayBench`에서 실측한 함정).
- 설계: `OFF → ACTIVE → OFF → AWAY`를 **3사이클** 교차(총 12위상 × 20초). 배경 부하 급등 위상이
  섞이므로 **중앙값**으로 집계한다.
- 도구: `Tools/PerfProbe/measure-macos.py`. GPU는 `ioreg IOAccelerator`의 `Device Utilization %`
  (sudo 불필요, 시스템 전역 — Windows 작업관리자의 "시스템 전체 GPU"에 대응).

**present 비율(코드에서 유도)**: `FramePacingPolicy`에서 `Active`는 `vSync=2, interval=1`(= hz/2),
`Away`는 `divisor=4 → wanted=8 → vSync=4, interval=2`(= hz/8). 즉 **Away/Active present = 1/4**.

### (2) 결과 (중앙값)

| 지표 | OFF(앱 종료) | ACTIVE 강제 | AWAY 강제 |
|---|---|---|---|
| **WindowServer CPU%** (코어 1개 기준) | **2.57** | **14.66** | **5.63** |
| `StickMate` 자기 CPU% | — | 25.53 | 10.20 |
| **GPU 전역 사용률 %** | **1.30** | **30.90** | **12.20** |

- **`ACTIVE − OFF` = +12.09%p**, **`AWAY − OFF` = +3.06%p**
- → **비율 = 0.25.** **코드에서 유도한 present 비율 1/4과 정확히 일치한다.**

### (3) 선형 분해 — **present 무관 고정항은 검출되지 않는다(≈0)**

비용을 `B(이진·present 무관) + P·f(present 비례)`로 놓고 두 식을 푼다:
```
B + P·f       = 12.09
B + 0.25·P·f  =  3.06
→ 0.75·P·f = 9.03  →  P·f = 12.04  →  B = 0.05%p ≈ 0
```
**macOS 컴포지터 부하는 (측정 한계 내에서) 100% present 횟수에 비례한다. 이진 페널티 항이 없다.**

**보조 관측**
- **앱을 켜는 것만으로 GPU 전역 1.30% → 30.90%(+29.6%p).** Windows의 `1% → 54%`와 **같은 계열의
  크기**다 — 플랫폼이 달라도 "투명 전체화면 오버레이 1개"의 대가가 이 수준이라는 뜻이다.
- GPU 전역 비율은 0.37로 WindowServer의 0.25보다 높다. GPU 전역에는 **앱 자신의 렌더**가 포함되고,
  앱 CPU 비율이 0.40이기 때문이다(물리·AI·창 열거 같은 **비렌더 작업은 present를 줄여도 계속 돈다**).
- 즉 present 축은 **컴포지터에 가장 효율적으로 듣고**, 앱 자체 비용에는 그보다 덜 듣는다.

### (4) 6-2 E2와 모순되지 않는다 — 비용 모델이 정리됐다

6-2에서 "10.6%p 중 3.8%p는 고정비"라고 했는데, 그것은 **면적 무관 항**이지 **present 무관 항**이
아니었다. 오늘 결과와 합치면 모델이 하나로 정리된다:

> **컴포지터 비용 = (present 횟수) × (면적 항 + 창당 고정 항)**
> **present와 무관한 항은 존재하지 않는다.**

이 모델은 B-1 정식 기각과도 정합적이다 — 면적 항만 줄여봐야 **곱해지는 present 항을 못 건드린다.**

### (5) Windows 미확정 질문에 대한 함의 — **갈래 A 쪽으로 사전 확률이 크게 기운다**

이것이 Windows를 **증명하지는 않는다**(BitBlt는 다른 경로다). 그러나:
- **갈래 A(present 비례)가 최소한 한 플랫폼에서 완벽하게 성립**함을 실측으로 보였다.
- 메커니즘상으로도 같은 방향이다: **BitBlt 복사는 "present마다" 일어난다.** 이진 페널티가 성립하려면
  DWM이 앱의 present와 무관하게 **매 vblank 데스크톱을 재합성**해야 하는데, DWM은 damage 기반이라
  그럴 이유가 적다.
- → **내일 Windows `FORCE_TIER` 테스트는 갈래 A가 나올 가능성이 높다.** 그렇다면 처방은
  `windowsTargetFrameRate` 한 값 + 설정창 노출이고 **신규 코드는 0줄**이다.

### (6) 부수 확인 (macOS `Player.log`에서 회수)
- **MSAA 4x가 실제로 걸려 있다**(요청=실측=4x, `targetTexture=없음(백버퍼 직접)`). 3024×1964 기준
  MSAA 컬러버퍼 약 95MB, resolve 트래픽 프레임당 약 119MB — **B-3(MSAA)의 전제가 macOS에서 참으로
  확인**됐다. 다만 이 비용도 present마다 발생하므로 **present 축을 줄이면 함께 준다**(별도 공사보다
  저전력 렌더링이 먼저인 이유).
- **적응형 거버너가 실제로 동작 중**이다(300초 체류 로그: `활성 24% / 정적 34% / 자리비움 42%`).
  "구현은 됐는데 안 걸리는" 상태가 아니다.
- **[별건, 경미] 오버레이 창 원점이 세션 중 8회 바뀌었다**(`0 → -372 → -1576 → -785 → -156 → 0`,
  33분간). `-1576`은 폭 1512 화면에서 완전히 화면 밖이라 그 순간 커서↔월드 변환이 크게 틀어진다.
  **성능 영향은 없다**(6-2 E5 기준 2Hz 이하 이동은 무료, 여기서는 33분에 8회). **좌표 정확성 이슈로만**
  debugger에게 넘긴다.

## 6-16. 【macOS 실측】 "60fps는 유지하고 부하만 줄인다" — **MSAA는 부하 레버가 아니다. 메모리 레버다**

사용자 결정(원문): **"60을 유지하면서 시스템 부담을 줄일수 있는 방안을 마련해야지"**.
6-15가 "컴포지터 비용 = present 횟수 × (면적 항 + 창당 고정 항), present 무관 항 ≈ 0"을 확정했으므로,
present를 못 건드리면 남는 손잡이는 **괄호 안**뿐이다. 6-15(6)이 그 괄호 안의 최대 항으로 지목한 것이
**MSAA 4x**였다(컬러버퍼 95MB, "resolve 트래픽 프레임당 약 119MB"로 추정). 이번 라운드는 그것을 실측했다.

### (1) 측정 조건
- Apple M2 Pro / Metal, 3024×1964, **모든 위상에서 `STICKMATE_FORCE_TIER=Active`** →
  `vSyncCount=2`, 120Hz 패널 → **present 정확히 60fps로 고정**. 변수는 MSAA 배수 하나뿐이다.
- 같은 바이너리에서 `STICKMATE_FORCE_MSAA={0,2,4}`로만 갈랐다
  (신설 `Platform/RenderQualityTuner.cs`, 미지정 시 제품 동작 영향 0).
- 도구: `Tools/PerfProbe/measure-msaa.py`(4조건 순회), `measure-msaa-cold.py`(2조건 페어드).
- 계측 전용 빌드는 `Builds/PerfProbe/`에 따로 굽는다(`Assets/Editor/PerfProbeBuild.cs`) —
  같은 시각 다른 팀원이 쓰는 `Builds/macOS/`를 덮어쓰지 않기 위해서다.

### (2) 【방법론 사고 1건 — 먼저 읽을 것】 **런타임 MSAA 토글은 무효다. 그리고 API가 그것을 감춘다**

처음에는 6-2의 E1~E6이 성공한 **3초 교차 페어드**를 그대로 쓰려고, 앱을 재시작하지 않고 프로세스
안에서 MSAA만 토글하는 계측 모드를 만들었다. 결과는 "차이 없음"이었는데 — **그 실험이 애초에
아무것도 바꾸지 않고 있었다.**

| 확인 방법 | 런타임 토글(4x↔0x, 22초 간격) | 콜드 스타트(4x vs 0x) |
|---|---|---|
| `Screen.msaaSamples` | 4 ↔ 0 으로 **즉시 바뀐다** | 4 ↔ 0 |
| `vmmap` `owned unmapped (graphics)` | **99.5MB 고정, 1바이트도 안 움직임** | **98.3MB ↔ 5.3MB** |

→ 백버퍼는 시작할 때 한 번 만들어지고 그 뒤 바뀌지 않는다. **`Screen.msaaSamples`는 요청값을
앵무새처럼 되돌려줄 뿐이다.** 이 프로젝트는 이미 8x에서 같은 함정에 빠진 적이 있다(커밋 `39ab690`:
Apple GPU가 8x를 조용히 4x로 낮추는데도 API는 8을 보고). **같은 종류의 사고가 두 번째다.**

**규약으로 승격한다: MSAA A/B는 (a) 반드시 앱을 껐다 켜서 하고, (b) 유효성은 엔진 API가 아니라
그래픽 메모리 실측으로 확인한다.** 무효로 판명된 토글 코드는 삭제했고, 그 사실을
`RenderQualityTuner.cs`에 "닫힌 길" 주석으로 박아 재시도를 막았다.

### (3) 결과 — 콜드 스타트 페어드 (위상 55초, 6사이클, 사이클마다 순서 반전, n=6쌍)

| 지표 | 4x − 0x 중앙값 | 부호검정 | 판정 |
|---|---|---|---|
| **그래픽 메모리** (`owned unmapped (graphics)`) | **+92.99MB** | **6/6** (σ=0.43MB) | **확정** |
| **물리 풋프린트** | **+87.85MB** | **6/6** | **확정** |
| WindowServer CPU% | −2.83%p | 2/6 | **신호 없음** |
| StickMate 자기 CPU% | −1.11%p | 3/6 | **신호 없음** |
| GPU Device 사용률 %p | +0.50 | 3/6 | **신호 없음** |
| GPU Renderer %p | +0.00 | 2/6 | **신호 없음** |
| GPU Tiler %p | +1.00 | 4/6 | **신호 없음** |

**절대 수준(중앙값)**: 4x → 풋프린트 404.1MB / GPU 34.0% / WS 19.1%,
0x → 풋프린트 320.0MB / GPU 33.5% / WS 24.1%.

**배수별 그래픽 메모리(`vmmap`, 콜드 스타트 직접 확인)** — 샘플 수에 정확히 선형이다:

| MSAA | `owned unmapped (graphics)` | 물리 풋프린트 | 4x 대비 |
|---|---|---|---|
| 4x | **98.3MB** | 404.1MB | — |
| 2x | **52.3MB** | 364.3MB | **−46.0MB** |
| 0x | **5.3MB** | 320.0MB | −93.0MB |

- CPU/GPU 쪽은 **부호가 방향조차 일정하지 않다**(GPU Renderer는 2/6으로 오히려 반대 방향). 차분의
  표준편차 3.87%p / n=6 → 표준오차 1.6%p. **참값의 상한을 잡으면 약 ±3.6%p**이고, 앱이 올리는
  GPU 지분이 약 34%p이므로 **MSAA 4x의 GPU 몫은 아무리 커도 10% 미만**이다.
- 1차 측정(4조건 × 20초 × 3사이클)도 같은 결론이었다: GPU Device 중앙값 **4x 37.0 / 2x 34.0 / 0x 34.5**.
- **정직한 한계**: 측정 중 사용자가 실제로 앱을 조작한 구간이 있었다(정보창 열기/장비 교체).
  WindowServer·앱 CPU의 큰 분산은 대부분 그 오염이다. 방어는 **사이클마다 순서를 뒤집은 페어드
  부호검정**이고, 메모리 지표는 그 오염에 전혀 흔들리지 않았다(6/6, σ=0.43MB).

### (4) 해석 — **6-15(6)의 "resolve 트래픽 119MB/frame" 추정을 철회한다**

그 추정은 즉시 렌더링(immediate-mode) GPU의 모델이다. **Apple GPU는 TBDR이고, MSAA는 타일 메모리
안에서 resolve된다** — 4개의 서브샘플은 타일 밖으로 나가지 않고, 프레임버퍼에 저장되는 것은
resolve된 1x 이미지 하나뿐이다. 그래서 **대역폭이 4배가 되지 않는다.** 실측이 정확히 그것을 말한다:
샘플 수를 4배로 늘려도 GPU 사용률이 측정 한계 안에서 변하지 않는다.

남는 것은 **할당**뿐이다. Unity는 멀티샘플 컬러 어태치먼트를 memoryless가 아닌 실제 텍스처로 잡는다
→ 3024×1964×4B×4샘플 ≈ 95MB. 실측 93MB와 일치한다.

**비용 모델 갱신(6-15의 문장을 이어받아):**
> 컴포지터 비용 = present 횟수 × (면적 항 + 창당 고정 항)
> **괄호 안은 "출력 픽셀 수 × 포맷"이지 "샘플 수"가 아니다. 그래서 MSAA로는 괄호 안을 못 줄인다.**
> MSAA가 사는 곳은 부하 축이 아니라 **메모리 축**이다.

### (5) 화질 — 4x / 2x / 0x 실측 픽셀 비교

정지한 계측 표적으로 **우상단 톱니 아이콘 + 부채꼴 아이콘**을 썼다(월드 공간 `LineRenderer`라
백버퍼 MSAA를 받는다. ScreenSpaceOverlay UI는 MSAA를 받지 않아 표적이 될 수 없다).
콜드 스타트마다 **같은 화면 좌표**를 캡처해 픽셀 단위로 차분했다(배경·창 배치 동일).

| 비교 | 달라진 픽셀 | 최대 Δ휘도 | **평균 Δ휘도** |
|---|---|---|---|
| 4x vs **0x** | 1026 | 209 | **30.86** |
| 4x vs **2x** | 553 | 47 | **4.65** |
| 2x vs 0x | 757 | 209 | 41.13 |

- **0x는 명백한 회귀다.** 평균 오차 30.9/255(12%), 최대 209/255. 7배 확대 육안 비교에서도 곡선이
  계단으로 무너지는 것이 바로 보인다. 이것이 2026-08-28에 사용자가 신고한 그 증상
  ("캐릭터 주변으로 픽셀이 깨져보이는데")이고, MSAA 4x를 켠 이유 자체다.
- **2x는 4x와 거의 구분되지 않는다.** 평균 오차 4.65/255 = **1.8%**. 가는 곡선(작은 기어의 이빨)에서만
  약간 뭉치는 정도. 이론과도 맞는다 — 커버리지 단계가 4단계에서 2단계로 줄어 양자화 오차 상한이
  12.5% → 25%가 되지만, 획이 2.65~5.16 물리픽셀로 얇아 그 오차가 걸리는 픽셀 자체가 적다.
  (단, 이 비교는 **정지 표적**이다. 움직이는 획에서는 단계가 적을수록 가장자리가 "기어다니는" 것이
  더 보인다 — 그 축은 측정하지 않았다.)

### (6) 권고 — MSAA

1. **MSAA 0(끔)은 기각.** 부하 이득이 0인데 화질만 잃는다. 논쟁 종결.
2. **기본값 4x를 그대로 둔다(코드 변경 없음).** 사용자가 요구한 것은 **부하 감소**인데
   MSAA는 그 축에 아무것도 기여하지 않는다. 화질을 지불하고 살 것이 없다.
3. **다만 메모리가 의제가 되면 2x가 실질적 선택지다** — 풋프린트 **−46MB**(404MB의 11%),
   화질 평균 오차 1.8%. "상주 앱 메모리 185MB→543MB" 신고가 다시 올라오면 이 카드를 쓴다.
   판단은 리더/사용자 몫이며, 이 절의 (5) 표가 그 근거다.
4. 이미 끝난 것: 깊이/스텐실 121MB는 제거됨(`disableDepthAndStencilBuffers`), 오디오 스레드도 제거됨.
   MSAA 컬러버퍼 93MB가 **남아 있는 마지막 큰 그래픽 할당**이다.

### (7) 그러면 무엇이 남았나 — **고칠 "핫스팟"은 없다**

실행 중인 `.app`을 12초 `sample`로 떴다. 온-CPU 리프가 사실상 존재하지 않는다:

```
semaphore_wait_trap  396,095      <- 대기
__workq_kernreturn    40,342      <- 대기
mach_msg2_trap        27,615      <- 대기
objc_msgSend              20 / malloc 9 / __sysctl 9      <- 실제 일
```

**앱의 CPU는 특정 알고리즘이 아니라 프레임 루프 그 자체다.** 즉 우리 C# 코드를 아무리 미세
최적화해도 present를 고정한 이상 눈에 띄는 절감은 나오지 않는다. 이것이 이 라운드의 두 번째
핵심 결론이고, "캐싱/오브젝트 풀링/텍스처 아틀라스" 같은 일반 최적화 팁이 이 앱에서
헛도는 이유다(애초에 스프라이트 에셋이 0개라 아틀라스라는 개념 자체가 없다).

### (8) 남은 레버 우선순위 — **60fps를 안 건드리는 것만**

| 순위 | 레버 | 기대 효과 | 비용 | 판단 주체 |
|---|---|---|---|---|
| **1** | **Active 등급 체류 시간 단축** — `Calm`은 `캐릭터 Idle && 무입력 ≥ 2초`를 요구해서 **캐릭터가 걷는 동안은 무조건 60fps**다. 실측 체류 `활성 70% / 정적 30%`. 무입력이 길어지면(예: 30~60초) **캐릭터 상태와 무관하게** Calm으로 내리는 규칙을 추가한다 | present 비례라 절감이 정확히 예측된다(6-15). 활성 70%의 절반을 Calm으로 옮기면 컴포지터 비용 **−17%** | 상수 + 판정 한 줄 | **제품 판단(리더/사용자)** — "활성 60fps"는 지키되 "언제가 활성인가"를 좁히는 것 |
| **2** | **가려짐(occlusion) 기반 절감** — 캐릭터 바운딩 박스가 불투명 창에 완전히 덮이면 등급을 내린다. z-order와 창 사각형은 `CGWindowListCopyWindowInfo`로 **이미 매 폴링 읽고 있다**(`VisibleTopEdgeSolver`) | 전체화면 브라우저/에디터로 작업하는 시간대 전체 | 신규 데이터 소스 0, 판정 로직만 | 리더 |
| 3 | MSAA 4x → 2x | 메모리 −46MB, **부하 0** | 상수 1개 | 리더 (화질 트레이드오프) |
| 4 | Dock 메트릭 캐시 `0.75초 → 5~10초`(또는 NSWorkspace 알림 기반). 지금은 1.33Hz로 CFPreferences 8회 + 실행 앱 전수 열거 + `HashSet` 2개를 새로 만든다 | 프로파일에 잡히지 않을 만큼 작다. **GC 압박**은 확실히 준다 | 상수 1개 | coder |
| — | **닫힘**: 창 축소(B-1, 6-8), 프레임레이트 인하(사용자 거부), 깊이/스텐실(이미 제거), 오디오(이미 제거), MSAA 부하 절감(이 절) | | | |

**한 줄 요약(리더 보고용)**: *60fps를 유지한 채 "그리는 비용"을 줄이는 방법은 실측상 남아 있지 않다.
MSAA는 부하가 아니라 메모리를 먹는다. 남은 진짜 레버는 "얼마나 잘 그리느냐"가 아니라
**"언제 60fps여야 하는가"를 좁히는 것**(위 1·2번)이고, 그건 성능 문제가 아니라 제품 결정이다.*
