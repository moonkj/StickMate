# StickMate

바탕화면(그리고 iPad/iPhone 홈 화면)에서 자율적으로 돌아다니는 졸라맨(스틱맨) 데스크톱 펫 앱. 열려 있는 다른 창을 발판 삼아 걷고, 뛰고, 매달리고, 가끔 얻어맞고 널브러졌다가 다시 일어난다. Unity 6 LTS 기반.

## 핵심 컨셉

- **윈도우 = 지형**: 데스크톱에서는 실시간으로 열거한 다른 앱 창의 상단 Y좌표를 발판(foothold)으로 사용한다.
- **자율 배회 AI가 기본 행동**: 키보드 조작 없음. 유저가 아무것도 안 해도 알아서 걷고, 쉬고, 두리번거리고, 가끔 점프/파쿠르를 시도한다("지켜보기"가 코어 루프).
- **클릭 관통 기본 ON**: 평소엔 마우스 입력을 그대로 통과시켜 다른 작업을 방해하지 않는다. 전체화면 게임 감지 시 자동으로 숨는다.
- **절대 원칙 — 유저 자산 불변**: 실제 파일·아이콘·타 윈도우는 절대 이동·삭제·수정하지 않는다. 전부 읽기 전용 열거 + 시각적 복사본 연출이다(자세한 내용은 아래 [절대 불변 원칙](#절대-불변-원칙) 참고).

## 지원 플랫폼

| 플랫폼 | 방식 | 설명 |
|---|---|---|
| macOS | 데스크톱 오버레이 | `IPlatformWindowService`가 타 윈도우를 실시간 열거해 발판으로 사용(설계상 목표). **네이티브 구현체는 아직 없음** — `Platform/MacOS/`는 플레이스홀더뿐. |
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

| 폴더 | 파일 수 | 담당 |
|---|---|---|
| `Core/` | 7 | 진입점(`StickmanAgent`), 이벤트 버스/상태 ID(`StickmanEventBus`), 튜닝값(`StickConfig`), 스펙터클 상호배제 락(`SpectacleEventLock`), 투두/스트레스 모델 |
| `Platform/` | 11 (+ `Windows/`, `MacOS/`, `Mobile/`) | 창 열거·오버레이 인터페이스(`IPlatformWindowService`), Win32 구현체, 모바일 스크린샷 백드롭 구현체, Null/Fallback 폴백 |
| `States/` | 22 | 상태머신(`StickmanStateMachine`) + `IStickmanState` 구현 14종(Idle/Walk/Jump/Fall/ParkourClimb/Attack/Ragdoll/Getup/BattleMinigame/DragThrow/RodeoCursor/WindowTheft/TimedSpectacle/Runaway) + 지원 유틸(`GroundSensor`, `RagdollRig`, `AutoWanderController`) |
| `Interaction/` | 18 | 각 기능의 트리거 감시/대상 선정/락 획득을 전담하는 Director 컴포넌트(격파, 드래그&던지기, 로데오커서, 창도둑, 그라피티, 청소부/블랙홀, 크래시, 하드웨어반응, 라이벌 조우, 포모도로, 스트레스, 투두) |
| `Dialogue/` | 2 | 텍스트-액션 계약 핵심(`DialogueIntent`, `IHasDialogueParams`) |
| `Plugins/` | 2 | DLC 매니페스트(`MotionPluginSO`, `EffectPluginSO`) |
| `Tests/` | 2 | EditMode 회귀 테스트(아래 [테스트](#테스트) 참고) |

## 빌드/실행 방법

1. Unity Hub 설치 후 **Unity 6000.0.82f1 (6 LTS)** 에디터 설치(모듈: macOS/Windows Build Support, 모바일 타깃 시 iOS Build Support 추가).
2. Unity Hub → Add project from disk → 이 리포 루트(`/Users/kjmoon/App/StickMate`) 선택 → 열면 자동 임포트/컴파일.
3. **중요 — 씬/프리팹이 아직 없다.** `Assets/` 안에는 `.unity` 씬 파일도 `.prefab` 파일도 존재하지 않는다. Phase 0~6은 상태머신/플랫폼서비스/이벤트버스/텍스트-액션 계약 등 **코드 레이어만** 구현하고 EditMode 테스트로 검증했으며, 실제 스틱맨 캐릭터 프리팹(스프라이트, Rigidbody2D/HingeJoint2D 리그, 씬 배선)은 의도적으로 다음 단계로 남겨둔 작업이다. 지금 프로젝트를 열고 Play를 눌러도 빈 씬만 보이는 게 정상이다.

## 구현 현황

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 스캐폴딩(플랫폼서비스/이벤트버스/상태머신 골격/`DialogueIntent` 스캐폴딩) | 완료 |
| 1 | 코어 루프(중력·발판인식·화면이탈낙하, IDLE/WALK/JUMP/FALL, 자율 배회 AI, 전체화면 감지) | 완료 |
| 2 | Active Ragdoll(RAGDOLL/GETUP), 파쿠르(PARKOUR_CLIMB), `DialogueIntent` 파라미터 파이프라인 | 완료 |
| 3 | 전투(격파 미니게임/라이벌 AI), 커서 상호작용(드래그&던지기/로데오 커서), 부분적 클릭관통 해제 인프라 | 완료 |
| 4 | OS 장난(창도둑/청소부/그라피티/크래시/블랙홀), PC 하드웨어 반응(CPU/배터리/충전/네트워크) | 완료 |
| 5 | 생산성(투두 말풍선/포모도로 감시자), 반항·스트레스(스트레스 게이지/가출) | 완료 |
| 5 | 던전 파밍 / 세포분열·군대 | **보류 (P3)** — 스코프 아웃이 아니라 우선순위 최저로 의도적 연기(`ARCHITECTURE.md` 1절 근거) |
| 6 | 성능 점검, 최종 코드 리뷰(개선 R2까지), README/기술문서 | 완료 |

## 절대 불변 원칙

1. **행동-텍스트 싱크**: 말풍선 대사는 상태 전이가 확정된 뒤 그 상태로부터만 파생된다. 대사를 먼저 정하고 행동을 끼워 맞추지 않는다.
2. **비침해**: 클릭 관통 기본 ON, 전체화면 게임 감지 시 자동 숨김.
3. **유저 자산 불변**: 실제 파일/아이콘/타 윈도우는 절대 이동·삭제·수정하지 않는다. 전부 읽기 전용 열거 + 시각적 복사본 연출.
4. **플러그인 구조**: 신규 모션/이펙트(DLC)는 기본 로직 무수정으로 ScriptableObject 매니페스트를 통해 추가한다.

## 테스트

`Tests/EditMode/`에 EditMode 테스트 2종(총 13건), 프로덕션 코드는 `StickMate.Runtime.asmdef`로 승격되어 있고 테스트 어셈블리(`StickMate.Tests.EditMode.asmdef`)가 `InternalsVisibleTo`로 내부 API에 접근한다.

| 테스트 파일 | 건수 | 검증 대상 |
|---|---|---|
| `DialogueTextActionSyncTests.cs` | 8 | 텍스트-액션 싱크 계약 — 강제 취소 시 말풍선 자동 만료, 컨텍스트 위조/재사용 차단, 파라미터 스냅샷 불변 |
| `UserAssetImmutabilityAuditTests.cs` | 5 | 유저 자산 불변 원칙 — 소스코드 전수 스캔으로 금지 API(창/파일 이동·삭제) 호출 여부 감사 |

실행 명령어(예):

```bash
/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /Users/kjmoon/App/StickMate \
  -runTests -testPlatform EditMode \
  -testResults /Users/kjmoon/App/StickMate/testresults.xml
```

최근 확인 기준(개선 R2 재검증) 13/13 통과, 컴파일 에러/경고 0건.

## 알려진 한계 / 다음 단계

- **macOS 네이티브 미구현**: `Platform/MacOS/`는 플레이스홀더뿐, `NSWindow` 오버레이/`CGWindowListCopyWindowInfo` 실구현 없음.
- **진짜 분리 오버레이 미구현(BUG-B1)**: `Win32WindowService`가 아직 게임 자신의 창을 재사용하는 임시 스텁이라, `SetClickThrough`/`SetAlwaysOnTop` 호출 시 게임 창 자체가 파괴되는 것을 막기 위해 안전가드(`NotSupportedException`)로 차단해 둔 상태. 즉 Windows에서 클릭관통이 실제로는 아직 켜지지 않는다. 별도 `CreateWindowEx` 기반 오버레이 창 구현이 선행 과제.
- **씬/프리팹 배선 전무**: 캐릭터 스프라이트, Rigidbody2D/HingeJoint2D 리그, UI(투두 포스트잇 등) 실제 씬 오브젝트 구성이 필요.
- **던전 파밍 / 세포분열·군대 보류(P3)**: `RivalStickmanAgent`가 이미 독립된 상태머신 인스턴스를 여러 개 동시 운용하는 패턴을 실증해, 착수 시 기술적 난이도는 낮을 것으로 판단됨(최종 코드 리뷰 근거).
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
