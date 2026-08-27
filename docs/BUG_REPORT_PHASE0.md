# StickMate — Phase 0 버그 리포트 (Debugger, Teammate2)
> 작성: Debugger · 작성일: 2026-08-27 · 대상: Coder Phase 0 산출물 (`Assets/_Project/Scripts/` 19개 C# 파일)
> 검증 스탠스: "돌아갈 것 같다"는 통과 사유가 아니다. 논리 오류·미처리 조건·향후 Phase에서 실제로 터질 조합을 적대적으로 찾는다.

## 결론 요약

**Coder로 반려 필요.**

Blocker 1건, Major 8건, Minor 8건 발견. Phase 1은 착수 가능한 상태가 아니다 — 특히 Phase 1의 첫 두 태스크(`클릭 관통 기본 ON`, `중력/발판 인식`)가 의존하는 인프라 자체에 결함/공백이 있다. 아래 순서로 먼저 수정 후 Phase 1을 시작할 것을 권고한다.

**권고 수정 순서**
1. **BUG-B1** (Win32 오버레이=게임 창 재사용, 클릭관통이 게임 창 자체를 망가뜨림) — Phase 1 "클릭 관통 기본 ON" 착수 전 반드시 해결.
2. **BUG-M2** (`ChangeState` 원자성 없음 → 상태머신 좀비화) — 값싼 방어 코드, 먼저 처리.
3. **BUG-M1** (`StateTransitionContext` 위조 가능 범위가 Coder 자체 진단보다 넓음) — 접근 제한자만 좁히면 되는 값싼 수정, 원칙 1 보호.
4. **BUG-M3** (`EnumerateFootholds` 폴링 계약을 강제하는 코드 전무) — Phase 1 "중력/발판 인식" 작업의 전제 인프라.
5. **BUG-M5** (좌표계 변환 공용 유틸 부재) — 역시 "중력/발판 인식" 전제 인프라.
6. **BUG-M4** (`SetBackdropScreenshot` 무조건 발판 초기화) — 모바일 영속화 붙기 전에 계약 정리.
7. **BUG-M6** (스크린샷 텍스처 파괴 누락) — 모바일 온보딩 구현 전.
8. **BUG-M7** (`DialogueIntent` 텍스트 함수가 상태 파라미터를 못 실음) — Phase 2 착수 시점에 재설계.
9. **BUG-M8** (전이/대사 이벤트에 캐릭터 식별자 없음) — Phase 3 라이벌 AI 착수 전 필드만 지금 예약.

Minor 8건은 즉시 반려 사유는 아니나 Phase 1 각 작업 착수 시 메모(아래 Tasklist 반영)로 함께 처리 권고.

---

## Blocker

### BUG-B1 — Win32 오버레이가 실제로는 게임 자신의 창이라 클릭관통/항상위가 게임 자체를 파괴함
- **파일:라인**: `Assets/_Project/Scripts/Platform/Windows/Win32WindowService.cs:112-137`
- **재현 시나리오**: Phase 1에서 "클릭 관통 기본 ON"(`StickConfig.clickThroughDefaultEnabled = true`)을 구현하며 부트스트랩 코드가 그대로 `platformService.CreateOverlayWindow(); platformService.SetClickThrough(true); platformService.SetAlwaysOnTop(true);` 순서로 호출한다고 가정하자.
  - `CreateOverlayWindow()`는 `Process.GetCurrentProcess().MainWindowHandle`, 즉 **Unity 플레이어 자신의 렌더링 창 핸들**을 그대로 오버레이로 채택한다(별도 오버레이 창을 만들지 않음).
  - `SetClickThrough(true)`는 그 핸들에 `WS_EX_LAYERED | WS_EX_TRANSPARENT`를 건다 → **게임 창 자체가 마우스 입력을 통과시켜 버려서, 이후 게임 내부에 만들 설정창/대결모드 클릭/드래그&던지기(Phase 3) 등 어떤 마우스 상호작용도 이 창으로는 절대 들어오지 않게 된다.** 즉 "클릭 관통 기본 ON"을 켜는 순간 앱 자신의 모든 마우스 입력 경로가 영구히 막힌다(트레이 아이콘 같은 OS 레벨 UI만 예외).
  - `SetAlwaysOnTop(true)`는 같은 핸들에 `HWND_TOPMOST`를 건다 → 실제로는 투명 오버레이가 아니라 **불투명한 일반 게임 창**이 항상 최상단에 고정되어 사용자 데스크톱 전체를 가릴 수 있다. "비침해 원칙"(원칙 2)과 정반대의 결과.
  - 추가로 `SetClickThrough`가 설정하는 확장 스타일에 `WS_EX_NOACTIVATE`가 빠져 있다(74번 줄 상수 목록에 정의조차 안 됨) — 클릭관통 창이라도 이 플래그 없이는 Alt-Tab/재생성 등의 계기로 간헐적으로 포그라운드 포커스를 가져갈 수 있어, 사용자가 다른 앱에 타이핑 중 포커스를 뺏기는 "침해" 사고가 날 수 있다.
  - 더 근본적으로, **GPU 스왑체인으로 프레젠테이션되는 살아있는 Unity 렌더링 창에 `WS_EX_LAYERED`를 직접 거는 것** 자체가 DWM 합성 방식과 충돌해 화면이 멈추거나 검게 나오는 사례가 실제로 보고되는 패턴이다(아래 과학적 토론 로그 가설 H1 참고, 미검증).
- **근본 원인**: `CreateOverlayWindow()`가 Phase 0 스텁이라는 사실 자체는 문제가 아니다. 문제는 `SetClickThrough`/`SetAlwaysOnTop`가 "진짜 오버레이가 만들어졌다"는 전제로 아무 가드 없이 그 스텁 핸들에 그대로 부작용을 가하도록 되어 있다는 것 — 스텁과 실제 오버레이 조작 API 사이에 안전장치가 전혀 없다.
- **판단**: Coder 질문("Phase 1로 미뤄도 되는 리스크인가")에 대한 답은 **아니오**다. Phase 1 첫 태스크가 바로 이 API를 호출하게 되어 있으므로 지금 손대지 않으면 Phase 1 산출물이 그대로 자기 파괴적으로 나온다.
- **수정 제안**: (a) Phase 1에서 `CreateOverlayWindow()`를 진짜 별도 HWND(`CreateWindowEx`로 생성한, 크기=가상 데스크톱 전체, `WS_EX_LAYERED|WS_EX_TRANSPARENT|WS_EX_TOPMOST|WS_EX_NOACTIVATE`를 처음부터 갖는 투명 자식 없는 창)를 만들도록 먼저 구현한다. (b) 그 전까지는 `SetClickThrough`/`SetAlwaysOnTop`가 `_overlayHwnd`가 "진짜 오버레이"인지 알 수 없으므로, 최소한 스텁 상태에서 이 메서드들이 실수로 호출되면 즉시 알아챌 수 있게 로그 경고나 `NotSupportedException`으로 막아두는 임시 가드를 추가한다. (c) `WS_EX_NOACTIVATE`를 상수 목록과 `SetClickThrough`의 스타일 조합에 추가한다.

---

## Major

### BUG-M1 — `StateTransitionContext` 위조 범위가 Coder 자체 진단보다 넓다 (원칙 1 방어선 실질 무력화 가능)
- **파일:라인**: `Assets/_Project/Scripts/States/IStickmanState.cs:20-49` (특히 41번 줄 public 생성자), `Assets/_Project/Scripts/States/StickmanStateMachine.cs:42` (`CurrentTransitionGeneration` public getter), `Assets/_Project/Scripts/Dialogue/DialogueIntent.cs:66-74`
- **재현 시나리오**: Coder는 `default(StateTransitionContext)`(→`OriginMachine == null`)만 위험 케이스로 보고 `ArgumentException` 가드를 넣었다. 그러나 실제 구멍은 더 넓다: `StateTransitionContext`의 생성자와 모든 필드가 `public`이고, `StickmanStateMachine.CurrentTransitionGeneration`도 `public`이다. 따라서 `Enter()` 밖의 **어떤 코드든** (AI 로직, 이후 Phase의 헬퍼 유틸, 실수로 캡처한 참조 등) 다음처럼 "진짜처럼 보이는" 컨텍스트를 손쉽게 위조할 수 있다:
  ```csharp
  var fake = new StateTransitionContext(
      StickmanStateId.Attack, StickmanStateId.Attack,
      Time.frameCount, someMachine.CurrentTransitionGeneration, someMachine);
  var bubble = new DialogueIntent(fake, id => "한 발 더"); // OriginMachine != null → 예외 없이 통과
  ```
  `OriginMachine`이 null이 아니고 `TransitionGeneration`이 머신의 실제 현재 세대와 일치하므로 `ArgumentException` 가드를 그대로 통과하고, `IsValid`도 다음 실제 전이가 일어나기 전까지 계속 `true`를 반환한다 — 즉 **실제 상태 전이 없이 "확정된 것처럼" 보이는 말풍선을 만들 수 있다.** 이는 CLAUDE.md 절대 불변 원칙 1("행동-텍스트 싱크")을 정면으로 깨는 경로이며, 사용자가 최우선으로 지목한 버그 유형(텍스트-액션 desync)의 정확한 근본 원인이 될 수 있다.
- **근본 원인**: 캡슐화 부재 — 발급 권한이 "관례(convention)"에만 의존하고 컴파일러/접근제한자로 강제되지 않는다.
- **판단**: Coder는 "Phase 2에서 클래스+1회용 토큰으로 강화 검토"라고 미뤘지만, 위조 난이도가 `default()` 한 줄이 아니라 실제 머신 참조만 있으면 되는 수준이라는 걸 감안하면 **지금(Phase 1 착수 전) 최소한의 접근 제한 강화가 필요**하다. Phase 2까지 기다리면 그 사이 Phase 1 코드가 "편의상 Enter() 밖에서도 컨텍스트를 만드는" 나쁜 패턴에 이미 의존해버릴 위험이 있다.
- **수정 제안 (최소 비용안, 지금 적용 가능)**: `StateTransitionContext`의 생성자를 `internal`로 좁히거나(같은 어셈블리 내에서만 발급 가능하게), `StickmanStateMachine.CurrentTransitionGeneration`을 `internal`로 낮춘다(단, `DialogueIntent.IsValid`가 같은 어셈블리에 있어야 함 — 현재 `StickMate.Dialogue`/`StickMate.States` 네임스페이스가 같은 asmdef인지 확인 필요). 더 강한 보증이 필요하면 Coder가 제안한 대로 "발급 1회용 토큰을 가진 sealed 클래스"로 전환하되, 그 작업을 Phase 2 시작 시점(실제 대사 콘텐츠 작성 직전)으로 못박아 반드시 선행시킨다.

### BUG-M2 — `ChangeState()`가 원자적이지 않아 미등록 상태로 전이 시 상태머신이 "좀비" 상태로 고착됨
- **파일:라인**: `Assets/_Project/Scripts/States/StickmanStateMachine.cs:62-74` (특히 66-70줄)
- **재현 시나리오**: `_states` 딕셔너리에 `StickmanStateId` 8종 전부가 등록되지 않은 상태(예: Phase 2 작업 중 `ParkourClimb`을 아직 등록 안 했는데 어떤 Tick() 로직이 실수로 `ChangeState(StickmanStateId.ParkourClimb)`를 호출)에서:
  1. `_current?.Exit()` 실행됨 (직전 상태 완전히 정리됨).
  2. `_transitionGeneration++` 실행됨 (직전 상태가 만든 `DialogueIntent`는 이미 구세대로 무효화됨).
  3. `_current = _states[next];` 에서 `KeyNotFoundException` 발생.
  4. Unity는 MonoBehaviour의 Update 콜스택에서 발생한 예외를 로그만 남기고 다음 프레임을 계속 실행하므로 **앱이 죽지는 않지만**, `_current`는 **한 줄 전에 이미 `Exit()`가 호출된 옛 상태 인스턴스를 계속 가리킨 채로 영구히 남는다.** 새 상태의 `Enter()`는 결코 호출되지 않았고, `StateTransitioned` 이벤트도 발행되지 않았다.
  5. 그 이후 매 프레임 `Tick()`은 이미 "퇴장 처리된" 옛 상태 인스턴스에 계속 호출된다 — 물리 모터/IK 목표가 이미 꺼진(Exit에서 정리된) 상태로 캐릭터가 그 자리에서 얼어붙거나 이상 동작을 무한히 반복할 수 있으며, `CurrentStateId`는 계속 옛 값을 보고해 다른 시스템이 실제 상황과 어긋난 판단을 하게 된다. 재시작 외에는 복구 경로가 없다.
- **근본 원인**: `next` 키 존재 검증이 실제 뮤테이션(Exit 호출, 세대 증가) **이후에** 이루어짐 — 순서가 뒤바뀌어 있어 실패 시 롤백이 불가능하다.
- **수정 제안**: `_states.TryGetValue(next, out var nextState)`로 먼저 조회해 실패 시 `Exit()`/세대 증가 이전에 명확한 예외를 던지거나(적어도 상태 변경 없이 실패), 최소한 실패를 로그로 크게 남기고 현재 상태를 유지하도록 가드를 `Exit()` 호출보다 앞에 둔다.

### BUG-M3 — `EnumerateFootholds()` "매 프레임 호출 금지" 계약을 강제하는 코드가 전혀 없음
- **파일:라인**: `Assets/_Project/Scripts/Platform/IPlatformWindowService.cs:48-54`, `Assets/_Project/Scripts/Core/StickConfig.cs:46`, `Assets/_Project/Scripts/Platform/Windows/Win32WindowService.cs:105-110`
- **재현 시나리오**: Phase 1 "중력/발판 인식" 구현 시 가장 자연스러운 실수 — `WalkState.Tick(deltaTime)`/`FallState.Tick(deltaTime)` 안에서 착지/발판 재탐지를 위해 `platformService.EnumerateFootholds()`를 직접 호출한다(타이머 없이). `IStickmanState.Tick`은 매 프레임(또는 매 물리 스텝) 호출되므로, 이렇게 짜면 즉시 초당 수십~수백 회 `EnumerateFootholds()`가 호출된다. Windows에서는 이것이 `EnumWindows` 전체 열거 + 창마다 `IsWindowVisible`/`GetWindowTextLength`/`GetWindowRect` 3회 P/Invoke를 의미하므로, 열린 창이 많을수록 24시간 상주 백그라운드 앱치고 눈에 띄게 CPU를 갉아먹는다. 이를 막을 어떤 어서션/레이트리미터/인터페이스 기본 구현도 존재하지 않으며, 계약은 오직 XML 주석뿐이다.
- **근본 원인**: 계약이 문서(주석)에만 존재하고 코드 레벨 강제 장치가 전혀 없음. "상위 레이어(FootholdWatcher 등)의 책임"이라고 명시했지만 그 유틸 자체가 아직 존재하지 않는다.
- **판단**: 인터페이스 계약만으로는 부족하다 — Phase 1의 첫 산출물이 바로 이 계약을 어길 가능성이 가장 높은 코드(발판 인식)이므로, 최소한의 폴링 래퍼를 Phase 0/Phase 1 착수 시점 산출물에 포함시켜야 한다.
- **수정 제안**: `StickConfig.footholdPollInterval`을 소비하는 얇은 `FootholdPoller`(또는 `FootholdWatcher`) 클래스를 Core에 추가 — 내부에 누적 타이머를 갖고 주기마다만 실제 `EnumerateFootholds()`를 호출, 결과가 이전과 달라졌을 때만 `StickmanEventBus.RaiseFootholdsChanged()`를 발행하고 나머지 시간엔 캐시된 리스트를 반환한다. 각 상태의 `Tick()`은 이 poller만 참조하고 `IPlatformWindowService`를 직접 호출하지 않도록 컨벤션화한다.

### BUG-M4 — `SetBackdropScreenshot()`이 호출마다 무조건 발판을 초기화 — 앱 재실행 시 매번 재온보딩을 강제할 위험
- **파일:라인**: `Assets/_Project/Scripts/Platform/Mobile/ScreenshotBackdropPlatformService.cs:51-55`
- **재현 시나리오**: 지금은 "유저가 새 스크린샷으로 배경을 바꿀 때 발판도 같이 초기화"라는 의도(UX_FLOW 3절/7절)로 맞게 동작한다. 문제는 이 메서드가 "배경이 실제로 바뀌었는지"를 전혀 구분하지 않는다는 점이다. 모바일 앱은 (데스크톱 상주 앱과 달리) 유저가 앱을 껐다 켤 때마다 이전 세션의 배경+발판 설정을 **영속화해서 복원**해야 하는 게 사실상 필수 요구사항이다(그러지 않으면 매번 "발판 지정 온보딩"을 다시 겪어야 하는데, 이는 UX_FLOW 7절 "배경 다시 찍기"가 유저의 **명시적** 재설정 행동으로 그려진 것과 모순된다). 앱 부트스트랩 코드가 저장된 이전 배경 텍스처를 복원하기 위해 자연스럽게 `SetBackdropScreenshot(savedTexture)`를 호출하면, 이 메서드는 그게 "새 배경으로의 변경"인지 "이전 상태 복원"인지 구분하지 못하고 **무조건 `ClearUserDefinedFootholds()`를 실행**해 방금 복원하려던 발판 목록을 스스로 지워버린다. 결과: 앱을 재실행할 때마다 유저가 이전에 지정한 발판이 사라지고 발판 탭 온보딩 화면이 매번 다시 뜬다.
- **근본 원인**: "배경 변경"과 "배경 상태 복원(재실행)"이라는 서로 다른 두 유스케이스가 같은 메서드로 뭉쳐져 있고, 구분할 방법(참조 동일성 체크, 별도 파라미터, 별도 메서드)이 없다.
- **수정 제안**: (a) `screenshot`이 현재 `BackdropScreenshot`과 참조가 같으면 초기화를 스킵하거나, (b) `SetBackdropScreenshot(Texture2D, bool invalidateFootholds = true)`처럼 명시적 플래그를 받거나, (c) 아예 "유저가 배경을 바꾼다"(`ReplaceBackdropScreenshot`, 발판 초기화 O)와 "저장된 상태를 그대로 복원한다"(`RestorePersistedState(screenshot, footholds)`, 발판 초기화 X)를 별도 public API로 분리한다. Phase 1에서 모바일 영속화를 붙이기 전에 반드시 정리해야 한다.

### BUG-M5 — 좌표계 변환 공용 유틸이 전혀 없음 (스크린/월드/DPI/멀티모니터 혼용 위험)
- **파일:라인**: `Assets/_Project/Scripts/Platform/IPlatformWindowService.cs:20-25` (문서화된 계약), 소비 측인 `States/*.cs` 전체(아직 미구현)
- **재현 시나리오**: `PlatformFoothold.ScreenRect`의 XML 주석은 "OS 네이티브 좌상단 원점, Unity Screen/GUI는 좌하단 원점 — 소비 측에서 명시적으로 변환해야 한다"고 명시하지만, 그 변환을 수행할 공용 유틸/헬퍼가 프로젝트 어디에도 없다. Phase 1에서 `WalkState`/`FallState`/`JumpState`가 각자 독립적으로 발판 Y좌표를 Unity 월드 좌표로 변환하는 코드를 작성하게 될 텐데, 세 상태가 서로 다른 사람(또는 서로 다른 시점)에 의해 작성되면 변환 공식이 미묘하게 어긋날 위험이 크다(예: 한 곳은 모니터 오프셋을 반영하고 다른 곳은 누락, 한 곳은 DPI 스케일 적용하고 다른 곳은 누락). 이는 사용자가 최우선 점검 대상으로 지목한 "좌표계 혼용" 버그의 전형적 발생 경로다. 특히 Win32의 `GetWindowRect`는 프로세스의 DPI 인식 설정에 따라 물리 픽셀/논리 픽셀이 달라질 수 있어(가설 H3 참고, 미검증) 멀티모니터+고DPI 조합에서 더 위험하다.
- **근본 원인**: 계약은 문서화됐지만 구현이 없어 각 소비자가 중복/분산 구현하게 되어 있음.
- **수정 제안**: Core에 `ScreenCoordinateConverter`(또는 유사) 정적 유틸을 두어, "OS 스크린 좌표 → Unity 월드 좌표" 변환을 한 곳에서만 구현하고 모든 상태가 이것만 사용하도록 강제한다. 멀티모니터 경계 정보(교차 레이어 로그에 이미 기록된 9절-5 미반영 항목)도 이 유틸과 함께 설계해야 앞뒤가 맞는다.

### BUG-M6 — `SetBackdropScreenshot()`이 교체되는 이전 `Texture2D`를 파괴하지 않음 (모바일 메모리 누수 위험)
- **파일:라인**: `Assets/_Project/Scripts/Platform/Mobile/ScreenshotBackdropPlatformService.cs:53`
- **재현 시나리오**: `BackdropScreenshot = screenshot;`은 이전 텍스처 참조를 그냥 덮어쓴다. 동적으로 생성된 `Texture2D`(사진첩에서 로드한 스크린샷, 보통 기기 해상도 크기라 수 MB)는 UnityEngine.Object 하위 타입이라 순수 C# GC만으로는 네이티브(GPU/이미지) 메모리가 즉시 해제되지 않는다 — 명시적으로 `Destroy()`하지 않으면 유저가 배경을 여러 번 재시도/재지정할 때마다(온보딩 재도전, 설정 탭 "배경 다시 찍기") 이전 텍스처들이 계속 쌓여 메모리를 잠식한다. 데스크톱보다 메모리가 훨씬 제한적인 iPad/iPhone에서 특히 위험하다.
- **근본 원인**: 텍스처 소유권/해제 책임이 정의되어 있지 않음.
- **수정 제안**: 이 서비스가 텍스처 소유권을 갖는다면 교체 전 이전 텍스처를 `Object.Destroy()`(또는 `DestroyImmediate` — 에디터 한정)하도록 명시하고, 소유권을 호출자가 갖는 구조로 간다면 그 계약을 XML 주석에 명확히 남긴다. Phase 1 모바일 온보딩 구현 착수 전에 결정할 것.

### BUG-M7 — `DialogueIntent`의 `Func<StickmanStateId,string>` 시그니처가 상태 파라미터를 못 실어 나름 (UX_FLOW 5절 규칙 #2 미충족 가능)
- **파일:라인**: `Assets/_Project/Scripts/Dialogue/DialogueIntent.cs:66-81`
- **재현 시나리오**: UX_FLOW.md 5절 규칙 #2는 "ATTACK 상태에 `shotsRemaining: 1` 파라미터가 있으면 말풍선은 그 값으로부터 파생되어야 하며, 텍스트가 상태 파라미터와 별개로 하드코딩되어 어긋날 수 있는 구조를 금지"한다고 명시한다. 그러나 `DialogueIntent` 생성자가 받는 `textFromState`는 `Func<StickmanStateId, string>` — **상태 ID만 받고 상태의 실제 파라미터(예: 남은 탄약 수)는 받을 방법이 없다.** Phase 2에서 `AttackState`가 이를 구현하려면 결국 `id => $"{_shotsRemaining}발 더"`처럼 지역 필드를 클로저로 캡처하는 임시방편을 쓸 수밖에 없는데, 이는 "enum+params → 텍스트 매핑 테이블 단방향"이라는 아키텍처 의도를 구조적으로 강제하지 못하고 다시 사람의 규율에만 의존하게 만든다(정확히 원칙 1이 막으려는 종류의 실수 재발 경로).
- **근본 원인**: API 시그니처가 UX 요구사항(파라미터 기반 텍스트 파생)을 표현할 수 있는 형태로 설계되지 않음.
- **수정 제안**: Phase 2 착수 시점에 `StateTransitionContext` 또는 별도 파라미터 객체를 통해 상태별 파라미터를 구조적으로 전달하는 형태로 재설계 검토(예: 상태가 `IHasDialogueParams`를 구현해 파라미터 구조체를 노출하고, 텍스트 매핑 함수가 이를 받도록 시그니처 확장). 지금 당장 Phase 1을 막을 사안은 아니므로 Phase 2 킥오프 시 필수 검토 항목으로 못박는다.

### BUG-M8 — `StateTransitionEvent`/`DialogueIntent`에 캐릭터(소스) 식별자가 없음 — 다중 캐릭터 시점에 필터링 불가
- **파일:라인**: `Assets/_Project/Scripts/Core/StickmanEventBus.cs:21-41`, `Assets/_Project/Scripts/Dialogue/DialogueIntent.cs:47` (`_originMachine`이 private)
- **재현 시나리오**: `StickmanEventBus`는 정적 클래스이므로 앱에 존재하는 **모든** `StickmanStateMachine` 인스턴스(플레이어 캐릭터 + Phase 3 라이벌 스틱맨들)가 같은 전역 이벤트를 공유한다. 그런데 `StateTransitionEvent`는 `From`/`To`/`IsForcedInterrupt`만 담고 "어느 캐릭터의 전이인가"를 담지 않는다. `DialogueIntent`도 `_originMachine`을 갖고 있지만 `private`이라 외부에서 "이 대사가 어느 캐릭터 것인지" 알 수 없다. Phase 3에서 "플레이어가 Ragdoll에 들어갈 때만 피격 효과음 재생"처럼 캐릭터별로 반응을 분기하려는 순간 이 설계로는 불가능해 이벤트 구조를 다시 뜯어고쳐야 한다.
- **근본 원인**: Phase 0 설계 시점에 단일 캐�릭터만 가정하고 캐릭터 식별자 필드를 예약해두지 않음.
- **판단**: Phase 1/2를 막을 사안은 아니다(아직 단일 캐릭터). 다만 나중에 struct에 필드를 추가하는 비용은 지금 추가하는 비용보다 훨씬 크므로(모든 구독자 코드가 이미 옛 시그니처에 맞춰 작성된 뒤 변경해야 함), Phase 3 착수 훨씬 전인 지금 필드만 예약해두는 것을 권고.
- **수정 제안**: `StateTransitionEvent`에 `object OwnerId`(또는 `StickmanStateMachine OwnerMachine`) 필드를 추가하고, `DialogueIntent`에 `public StickmanStateMachine OriginMachine`처럼 읽기 전용 공개 프로퍼티를 하나 노출한다(단, BUG-M1 수정과 함께 캡슐화 수준을 맞출 것).

---

## Minor

| # | 파일:라인 | 내용 | 권고 |
|---|---|---|---|
| m1 | `States/StickmanStateMachine.cs:39`, `States/IStickmanState.cs:22-23` | `CurrentStateId`가 미초기화 시 `default`(=`StickmanStateId.Idle`, enum 0번)를 반환 — "아직 상태 없음"과 "진짜 Idle 상태"가 구분 안 됨. 생성자의 최초 `ChangeState(initialState)` 호출도 `From: Idle`로 위조된 전이를 발행함(실제로는 이전 상태가 존재한 적이 없음). | enum에 `None`/`Uninitialized` 센티널 값 추가 검토, 또는 최초 전이는 별도 플래그로 구분. |
| m2 | `Platform/Windows/Win32WindowService.cs:105-110`, `Platform/Mobile/ScreenshotBackdropPlatformService.cs:93` | `EnumerateFootholds()`가 매 구현체에서 동일한 내부 mutable 리스트를 그대로 반환(캐스팅하면 `List<T>`로 되돌려 외부에서 직접 변형 가능 — `RaiseFootholdsChanged()` 우회). 또한 호출자가 참조를 프레임 너머로 캐싱하면 다음 폴링에 몰래 내용이 바뀜(스냅샷 아님). | `.AsReadOnly()` 래핑(1회 생성 후 캐시) 검토, 계약에 "스냅샷 아님"을 명시. |
| m3 | `States/StickmanStateMachine.cs:62-74` | 자기 자신으로의 전이(`ChangeState(current)`, 예: 유휴 잡담 갱신)도 `StateTransitioned` 이벤트를 "진짜 전이"인 것처럼 그대로 발행함. | 구독자가 `From==To`를 자연스레 다룰 수 있는지 Phase 2에서 확인, 필요시 별도 플래그. |
| m4 | `Core/StickmanEventBus.cs:58-95` | 정적 이벤트라 모든 `DialogueIntent`가 앱 내 **모든** 캐릭터의 전이 이벤트를 받아 자기 세대 비교를 수행(O(N×M)). 지금은 정확성엔 문제 없으나 라이벌 다수 생성 시(Phase 3) 성능 영향 가능. | Phase 6 성능 점검 항목에 포함. |
| m5 | `Platform/Windows/Win32WindowService.cs:78-79, 112-156` | `_overlayHwnd`를 최초 1회만 캡처하고 이후 재검증/재획득 로직이 없음 — 디스플레이/DPI 변경 등으로 창이 재생성되면 핸들이 stale해져 이후 모든 Win32 호출이 조용히 실패(예외 없이 무시)할 수 있음(네이티브 핸들 수명 문제). | `IsWindow(hwnd)` 유효성 체크 및 재획득 경로 추가 검토(Phase 1/4). |
| m6 | `Platform/Windows/Win32WindowService.cs:117` | `Process.GetCurrentProcess().MainWindowHandle`은 프로세스 시작 직후 호출 시 `IntPtr.Zero`를 반환하는 알려진 타이밍 이슈가 있음 — 재시도 로직이 없어 부트스트랩 타이밍에 따라 오버레이 기능이 조용히 영구 비활성화될 수 있음. | 짧은 재시도/폴링 루프 추가. |
| m7 | `Platform/Windows/Win32WindowService.cs:96` | `GetWindowTextLength(hWnd) == 0`으로 제목 없는 창을 발판에서 제외 — 의도된 휴리스틱이지만 제목 없는 정상 창(일부 유틸리티/오버레이 앱)도 배제될 수 있음. | 아키텍처 문서에 이미 "휴리스틱 개선 예정"으로 명시됨 — Phase 1 실기기 테스트 때 회귀 케이스로 등록 권고. |
| m8 | `Assets/_Project/Scripts/Platform/` 전체 | macOS용 `IPlatformWindowService` 구현체가 아직 없음(Win32 스텁 + Null 폴백만 존재). ARCHITECTURE.md는 macOS를 Windows와 동급 1차 타깃으로 명시. | 버그는 아니나 커버리지 공백 — Phase 1 계획에 "macOS 네이티브 플러그인" 태스크를 명시적으로 포함하거나, 의도적 후순위 결정을 Architect가 문서화할 것. |

---

## 과학적 토론 로그용 가설 (원인 불명, 실측 필요 — `Tasklist.md`에도 동일 내용 기록)

이 항목들은 실제로 관측된 버그가 아니라, 코드 검토만으로는 확답할 수 없어 **가설**로 남긴다. Windows 실빌드가 준비되면 검증 필요.

- **가설 H1**: `Win32WindowService`가 (BUG-B1 수정 전) Unity의 실제 렌더링 창(DXGI/OpenGL 스왑체인이 붙은 창)에 `WS_EX_LAYERED`를 직접 걸면, DWM 합성 방식과 충돌해 화면이 멈추거나 검게 나올 수 있다.
  - 검증 방법: Windows Standalone 빌드에서 `CreateOverlayWindow()` → `SetClickThrough(true)` 호출 후 게임 화면이 정상적으로 계속 렌더링되는지 육안 확인, Player.log에 DXGI/GL 관련 오류가 남는지 확인.
- **가설 H2**: `WS_EX_NOACTIVATE`가 빠져 있어, 클릭관통이 켜진 상태에서도 오버레이(현재는 게임 창 자체)가 간헐적으로 OS 포그라운드 포커스를 가져가 사용자가 다른 앱에 입력 중인 포커스를 뺏을 수 있다.
  - 검증 방법: 별도 텍스트 에디터에 타이핑하며 `SetClickThrough(true)`/`SetAlwaysOnTop(true)`를 반복 토글해, 타이핑이 끊기거나 포커스가 게임 창으로 전환되는지 확인.
- **가설 H3**: Windows의 프로세스 DPI 인식 설정에 따라 `GetWindowRect`가 반환하는 좌표가 물리 픽셀/논리(가상화된) 픽셀 중 무엇인지가 달라져, 고DPI 또는 배율이 다른 멀티모니터 환경에서 발판 좌표가 실제 픽셀 위치와 어긋날 수 있다.
  - 검증 방법: Unity 플레이어의 DPI 인식 설정(Player Settings/매니페스트)을 확인하고, 배율 150%/200% 모니터에서 알려진 위치의 창에 대해 `GetWindowRect` 반환값을 실측값과 비교.
- **가설 H4**: `CreateOverlayWindow()`가 앱 기동 직후(첫 프레임 이전) 호출되면 `MainWindowHandle`이 아직 OS에 등록되지 않아 `IntPtr.Zero`를 반환하고, 이후 재시도 없이 오버레이 관련 기능이 영구 비활성 상태로 남을 수 있다.
  - 검증 방법: 실제 기동 시퀀스에서 `CreateOverlayWindow()` 반환값을 로그로 남겨 항상 0이 아닌지, 호출 타이밍(Awake vs 첫 프레임 이후)에 따라 결과가 달라지는지 확인.

(결과/결론은 Windows 실빌드 검증 후 다음 라운드에서 채워질 수 있음.)
