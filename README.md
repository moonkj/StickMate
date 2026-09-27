# StickMate

MAC/Windows 바탕화면(그리고 iPad/iPhone 홈 화면 - 추후 검토 예정)에서 자율적으로 돌아다니는 Stick mate 데스크톱 펫 앱. 열려 있는 다른 창을 발판 삼아 걷고, 뛰고, 매달리고, 같이 일하는 동료 컨셉 앱. Unity 6 LTS 기반.

## 핵심 컨셉

- **윈도우 = 지형**: 데스크톱에서는 실시간으로 열거한 다른 앱 창의 상단 Y좌표를 발판(foothold)으로 사용한다.
- **자율 배회 AI가 기본 행동**: 키보드 조작 없음. 유저가 아무것도 안 해도 알아서 걷고, 쉬고, 두리번거리고, 가끔 점프/파쿠르를 시도한다("지켜보기"가 코어 루프).
- **클릭 관통 기본 ON**: 평소엔 마우스 입력을 그대로 통과시켜 다른 작업을 방해하지 않는다. 전체화면 게임 감지 시 자동으로 숨는다.
- **절대 원칙 — 유저 자산 불변**: 실제 파일·아이콘·타 윈도우는 절대 이동·삭제·수정하지 않는다. 전부 읽기 전용 열거 + 시각적 복사본 연출이다(자세한 내용은 아래 [절대 불변 원칙](#절대-불변-원칙) 참고).

## 지원 플랫폼

| 플랫폼 | 방식 | 설명 |
|---|---|---|
| macOS | 데스크톱 오버레이 | CoreGraphics/CoreFoundation C ABI 직접 P/Invoke(`CGWindowListCopyWindowInfo`)로 창 열거. 클릭관통/항상위는 Windows와 동일하게 안전가드로 **현재 비활성화**(네이티브 Objective-C++ 플러그인 없이는 NSWindow 조작 불가 — 아래 [알려진 한계](#알려진-한계--다음-단계) 참고). |
| Windows | 데스크톱 오버레이 | Win32 P/Invoke(`EnumWindows`, `DwmGetWindowAttribute`)로 창 열거. 클릭관통/항상위는 안전가드로 **현재 비활성화**(아래 [알려진 한계](#알려진-한계--다음-단계) 참고). |
| iPad / iPhone | 스크린샷 백드롭 모드 | iOS 샌드박스 정책상 실제 오버레이가 불가능해, 유저가 캡처한 홈 화면 스크린샷을 정적 배경으로 쓰고 아이콘 줄/Dock 위치를 탭으로 지정해 발판 삼는 착시 연출. |

4개 플랫폼 모두 같은 상태머신/이펙트 코드를 공유하고, `IPlatformWindowService` 구현체만 플랫폼별로 교체한다.

## 기술 스택

| 항목 | 내용 |
|---|---|
| 엔진 | Unity 6000.0.82f1 (Unity 6 LTS) |
| 렌더 파이프라인 | **Built-in RP**(2026-08-28 확정). 초기 설계는 URP 2D를 전제했으나 URP 특화 기능에 의존하는 코드가 아직 없어 현재 상태를 공식 기준으로 채택 — 향후 비주얼 작업 착수 시 Package Manager로 언제든 전환 가능. |
| 물리/애니메이션 | Rigidbody2D + HingeJoint2D 기반 **Active Ragdoll + IK 하이브리드**. 능동 상태(IDLE/WALK/JUMP 등)는 모터가 목표 포즈로 힘을 가하고, RAGDOLL 상태는 전신 물리에 위임 후 자동으로 GETUP 복귀. |
| 상태 관리 | `IStickmanState` 명시적 상태 패턴 + `StickmanStateMachine`(원자적 전이, 토큰 기반 위조 방지). |
| 이벤트 버스 | `StickmanEventBus` — 입력/렌더/네이티브/AI 레이어 간 느슨한 결합 통신(상태전이/발판변경/대사요청 등). |
| 텍스트-액션 계약 | `DialogueIntent` + `StateTransitionContext`(1회용 소비 토큰) — 말풍선 대사가 상태 전이가 확정된 프레임에서만, 그 상태로부터만 파생되도록 구조적으로 강제. |
| 플랫폼 추상화 | `IPlatformWindowService` 필수 계약 + `ICursorPositionService`/`ILocalClickCaptureService`/`IDesktopIconLayoutService` 옵셔널 캐퍼빌리티(`as` 캐스팅 패턴)로 macOS 등 신규 플랫폼 추가 시 점진적 구현 가능. |
| DLC/플러그인 | `MotionPluginSO`/`EffectPluginSO` ScriptableObject 매니페스트 — 기본 로직 무수정으로 신규 모션/이펙트 추가. |

## 폴더 구조

`Assets/_Project/Scripts/` 하위:

> **파일 수는 2026-09-02 실측값이다.** 이 열은 오래 방치돼 실제와 크게 어긋나 있었다
> (`Core/` 7→실제 30, `Interaction/` 18→실제 50). 격파 놀이 삭제 라운드에서 두 칸을 고치려다
> 발견해 전 열을 다시 셌다 — 숫자를 손으로 적는 표는 이렇게 샌다.

| 폴더 | 파일 수 (2026-09-02 실측) | 담당 |
|---|---|---|
| `Core/` | 30 | 진입점(`StickmanAgent`), 이벤트 버스/상태 ID(`StickmanEventBus`), 튜닝값(`StickConfig`), 스펙터클 상호배제 락(`SpectacleEventLock`), 투두/스트레스/성장/기록 모델, 저장소(`CharacterSaveStore`) |
| `Platform/` | 44 (하위 `Windows/`, `MacOS/`, `Mobile/` 포함 시 60) | 창 열거·오버레이 인터페이스(`IPlatformWindowService`), Win32/macOS 구현체, 모바일 스크린샷 백드롭 구현체, Null/Fallback 폴백 |
| `States/` | 29 | 상태머신(`StickmanStateMachine`) + `IStickmanState` 구현 18종(Idle/Walk/Jump/Fall/LandingCrouch/ParkourClimb/LedgeHang/GroundLossHang/Attack/Ragdoll/Getup/ThrowTumble/DragThrow/RodeoCursor/WindowTheft/Archery/TimedSpectacle/Runaway) + 지원 유틸(`GroundSensor`, `RagdollRig`, `AutoWanderController`, `StickmanBlackboard`) |
| `Interaction/` | 50 | 각 기능의 트리거 감시/대상 선정/락 획득을 전담하는 Director + 그 시각 레이어 Renderer, 그리고 uGUI 표면(정보창/설정창/행동 명령창/부채꼴/포스트잇) |
| `Dialogue/` | 5 | 텍스트-액션 계약 핵심(`DialogueIntent`, `DialogueKind`, `AmbientChatter`, `DialogueBubbleRenderer`) |
| `Plugins/` | 2 | DLC 매니페스트(`MotionPluginSO`, `EffectPluginSO`) |
| `Tests/` | 217 | EditMode/PlayMode 회귀 테스트(아래 [테스트](#테스트) 참고) |

## 빌드/실행 방법

1. Unity Hub 설치 후 **Unity 6000.0.82f1 (6 LTS)** 에디터 설치(모듈: macOS/Windows Build Support, 모바일 타깃 시 iOS Build Support 추가).
2. Unity Hub → Add project from disk → 이 리포 루트(`<프로젝트 경로>`) 선택 → 열면 자동 임포트/컴파일.
3. `Assets/_Project/Scenes/Main.unity`를 열고 Play를 누르면 `Assets/_Project/Prefabs/Stickman.prefab`(플레이스홀더 스프라이트 리그)이 실제로 낙하→접지→자율 배회하는 모습을 볼 수 있다. 아트 에셋은 아직 없어 흰 사각형/원 스프라이트로만 구성돼 있다 — 프리팹/씬은 `Assets/Editor/SceneBootstrapper.cs`(메뉴: `StickMate/Build All` 또는 `Rebuild All`)로 재생성 가능(기존 에셋이 있으면 기본적으로 건드리지 않음).

## 구현 현황

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 스캐폴딩(플랫폼서비스/이벤트버스/상태머신 골격/`DialogueIntent` 스캐폴딩) | 완료 |
| 1 | 코어 루프(중력·발판인식·화면이탈낙하, IDLE/WALK/JUMP/FALL, 자율 배회 AI, 전체화면 감지) | 완료 |
| 2 | Active Ragdoll(RAGDOLL/GETUP), 파쿠르(PARKOUR_CLIMB), `DialogueIntent` 파라미터 파이프라인 | 완료 |
| 3 | 커서 상호작용(드래그&던지기/로데오 커서), 부분적 클릭관통 해제 인프라 | 완료 |<br>*(라이벌 AI는 2026-08-30, 격파 미니게임은 2026-09-02 사용자 지시로 전체 삭제 — docs/UX_FLOW.md 11절 / 10절)*
| 4 | OS 장난(창도둑/청소부/그라피티/크래시/블랙홀), PC 하드웨어 반응(CPU/배터리/충전/네트워크) | 완료 |
| 5 | 생산성(투두 말풍선/포모도로 감시자), 반항·스트레스(스트레스 게이지/가출) | 완료 |
| 5 | 던전 파밍 / 세포분열·군대 | **보류 (P3)** — 스코프 아웃이 아니라 우선순위 최저로 의도적 연기(`ARCHITECTURE.md` 1절 근거) |
| 6 | 성능 점검, 최종 코드 리뷰(개선 R2까지), README/기술문서 | 완료 |

## 절대 불변 원칙

1. **행동-텍스트 싱크**: 말풍선 대사는 상태 전이가 확정된 뒤 그 상태로부터만 파생된다. 대사를 먼저 정하고 행동을 끼워 맞추지 않는다.
2. **비침해**: 클릭 관통 기본 ON, 전체화면 게임 감지 시 자동 숨김.
   - ★ **정정(2026-09-27) — 이 줄의 「전체화면 게임」은 실재보다 좁다.** 구현은 **두 단계**다: **등급 1 이상의 전체화면 앱**에서는 **창·패널·부채꼴·클릭 차단막이 물러나고 자동 춤이 멈추며**, **게임으로 판정된 경우에만 캐릭터까지 숨는다**(`Platform/FullscreenSuspendPolicy.cs`의 `SuppressesAutoDance`는 `tier != None`이고 `SuspendsCharacter`는 등급 1에서 거짓이다). 화면 라벨은 이미 「게임」이 아니라 「앱」 기준으로 바뀌었다 — ★ **라벨 전문을 여기 베끼지 않는다**(이번 묶음에 두 번 바뀌었고 그때마다 이 문장이 거짓이 됐다). 라벨 글자의 정본은 `Interaction/SettingsWindow.cs`의 `display.AddToggle("general.autoHide", …)` 한 자리다.
   - ★★ **원문은 글자 그대로 보존한다** — 「게임 → 앱」 한 낱말 교체는 **「전체화면 앱이면 캐릭터가 숨는다」로 읽혀 2026-08-31 사용자 신고로 닫힌 문을 다시 여는 것**이 된다. 캐릭터 은신을 앱 전반으로 넓히는 것은 **사용자 확인 사안**이다.
   - ★ 이 문구는 `CLAUDE.md`·`README.md`·`docs/ARCHITECTURE.md` **세 곳에 각 1회** 있고 **글자가 같아야 한다** — 바꿀 때는 **같은 커밋에서 셋 다** 고친다.
     ★ **무엇을 세는지가 이 불변식의 절반이다**. 「각 1회」와 「글자가 같아야 한다」의 주어는
     **줄이 아니라 문장 하나**다 — 니들은 원칙 2 본문의
     `클릭 관통 기본 ON, 전체화면 게임 감지 시 자동 숨김` **한 문장 전체**이고 기대값은 **세 파일 각 1회**다.
     아래 두 니들을 이 정정 블록이 인용하고 있으므로 **이 블록을 계수 범위에서 빼고 센다**
     (`docs/TEAM.md` 규칙 18 ⑶ 「센티널은 적는 순간 센티널이 아니게 된다」).
     ⑴ **줄 전체로 비교하면 이미 거짓이다** — `docs/ARCHITECTURE.md` 쪽 줄에는 `**비침해**:` 접두와
     끝 마침표가 없다. 줄까지 같은 것은 `CLAUDE.md`와 `README.md` 둘뿐이다.
     ⑵ **짧게 자른 니들 `전체화면 게임 감지`로 세지 마라** — 본문 기준으로 1 · 2 · 3이 나오고 셋 다
     정상이다(`README.md`의 기능 소개문 1회, `docs/ARCHITECTURE.md`의 요구사항 표와 소형 창 절 2회가
     같은 말을 풀어 쓴 것이다).
     ★ **썩는 방향**: 이것은 **존재 단언**이라 원칙 문구가 바뀌면 적중 0으로 **시끄럽게 빨개진다**.
     조용히 초록이 되는 구멍은 하나뿐이고 **이미 열려 있다** — 세 파일 목록 **밖**의 사본은 세지지
     않는다(`.claude/agents/` 정의서와 `.cs` 주석에 같은 원칙 문구가 있다). 전수 배분표는
     `docs/GAME_ARCHITECTURE_REVIEW.md` §20-4-5이고, 원칙을 새 문서에 베끼면 **그 문서를 이 목록에
     먼저 추가**한다.
3. **유저 자산 불변**: 실제 파일/아이콘/타 윈도우는 절대 이동·삭제·수정하지 않는다. 전부 읽기 전용 열거 + 시각적 복사본 연출.
4. **플러그인 구조**: 신규 모션/이펙트(DLC)는 기본 로직 무수정으로 ScriptableObject 매니페스트를 통해 추가한다.

## 테스트

`Tests/EditMode/`(로직 단위)와 `Tests/PlayMode/`(씬 실배선 스모크/회귀) 두 어셈블리. 프로덕션 코드는 `StickMate.Runtime.asmdef`로 승격되어 있고 테스트 어셈블리들이 `InternalsVisibleTo`로 내부 API에 접근한다.

| 테스트 파일 | 건수 | 검증 대상 |
|---|---|---|
| `DialogueTextActionSyncTests.cs` (EditMode) | 8 | 텍스트-액션 싱크 계약 — 강제 취소 시 말풍선 자동 만료, 컨텍스트 위조/재사용 차단, 파라미터 스냅샷 불변 |
| `UserAssetImmutabilityAuditTests.cs` (EditMode) | 5 | 유저 자산 불변 원칙 — 소스코드 전수 스캔으로 금지 API(창/파일 이동·삭제) 호출 여부 감사 |
| `StickmanPlaytestSmokeTests.cs` (PlayMode) | 1 | `Main.unity`를 실제로 15초간 구동 — 무한낙하 없음, 자율배회 실이동, 종료시점 접지상태(Idle/Walk) |
| `StickmanRagdollRecoveryTests.cs` (PlayMode) | 1 | RAGDOLL 강제진입 → GETUP → Idle/Walk 복귀(정지/이동 중 피격 둘 다) — 15회 반복 실측 100% 통과 |

실행 명령어(예 — **`-runTests`와 `-quit`을 같이 쓰면 테스트가 끝나기 전에 조기 종료되니 함께 쓰지 말 것**):

```bash
/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$(git rev-parse --show-toplevel)" \
  -runTests -testPlatform EditMode \
  -testResults "$(git rev-parse --show-toplevel)/testresults.xml" \
  -logFile "$(git rev-parse --show-toplevel)/testresults.log"
```

(`-testPlatform PlayMode`로 바꾸면 씬 스모크/랙돌 복귀 테스트 실행.) 최근 확인 기준 EditMode 13/13, PlayMode 2/2(각각 다회 반복으로 재현성 확인), 컴파일 에러/경고 0건.

## 알려진 한계 / 다음 단계

- **macOS/Windows 둘 다 진짜 분리 오버레이/클릭관통 미구현**: `MacWindowService`(`CGWindowListCopyWindowInfo` 기반)/`Win32WindowService`(`EnumWindows` 기반) 둘 다 창 열거는 실제로 동작하지만, `SetClickThrough`/`SetAlwaysOnTop`은 안전가드(`NotSupportedException`)로 항상 거부한다 — 진짜 분리된 오버레이 창(macOS: Objective-C++ 네이티브 플러그인, Windows: `CreateWindowEx` 기반)이 있어야 클릭관통/항상위를 안전하게 켤 수 있다(이번 범위 밖, BUG-B1과 동일 계열). 두 플랫폼 다 에디터에서는 활성 빌드 타깃과 무관하게 `NullPlatformWindowService`를 쓰도록 `!UNITY_EDITOR` 가드가 걸려 있다(실측으로 필요성이 확인된 가드 — `Core/StickmanAgent.cs` 참고).
- **던전 파밍 / 세포분열·군대 보류(P3)**: 예전 `RivalStickmanAgent`(2026-08-30 삭제)가 독립된 `StickmanStateMachine` 인스턴스를 플레이어와 병렬로 운용하는 패턴을 실증했었다 — 코드는 사라졌지만 `StickmanStateMachine`/`StickmanBlackboard`가 여전히 인스턴스 단위라 착수 시 기술적 난이도는 낮을 것으로 판단됨(최종 코드 리뷰 근거).
- **Windows 데스크톱 아이콘 좌표 조회 스텁**: `IDesktopIconLayoutService`는 Windows 실기기 부재로 정직한 no-op으로 남아 있음(청소부/블랙홀 연출에 영향).
- **물리 갱신이 `Update()` 경로**: `Rigidbody2D` 속도/위치 설정이 `FixedUpdate()`가 아닌 `Tick()`(→`Update()`) 경로에서 이뤄짐. 성능 문제는 아니지만 프레임레이트 변동 시 물리 잔떨림 가능성이 있어, 렌더링/모터 레이어 착수 시 재검토 권고(`docs/PERFORMANCE_REPORT.md` 참고 사항).

## 더 읽을거리

- `docs/ARCHITECTURE.md` — 설계 요약, 기술 스택 결정 근거
- `docs/UX_FLOW.md` — UX 플로우 전체(화면/상태 흐름, 31개 절)
- `Tasklist.md` — Phase별 작업 트래커 + 교차 레이어 영향 로그
- `docs/BUG_REPORT_PHASE0~5.md` — Phase별 버그 리포트
- `docs/PERFORMANCE_REPORT.md` — Phase 6 성능 점검
- `docs/CODE_REVIEW_FINAL.md` — 최종 코드 리뷰(개선 R2 포함)
- `process.md` — 리더가 남긴 단계별 진행 로그
