# E-4 보존 동결 창 클릭 가설 판독 (debugger, 2026-09-15)

- 기준 커밋: `e6b14c2` (E-4 숨긴 캐릭터 입력 차단). 경로는 `Assets/_Project/Scripts/` 기준.
- 방법: 코드 판독 + Unity 문서 원문 + **이미 있는 러너 결과 xml 재판독**. Unity·앱·빌드는 실행하지 않았다. `.cs`는 고치지 않았다.
- 판정 등급: **확정**(코드 + 러너 실측 또는 문서 원문) / **기각** / **판정 불가**(탐침 필요) / **가설**(검증 방법 병기).

---

## 0. 주장 (persona-stress 「재현」, 코드상·실기 미확인)

E-4 게이트는 `IsSuspended`만 본다. 좌클릭 게이트는 `Interaction/StickmanClickHitbox.cs:354`, 우클릭 게이트는 `Interaction/AppControlDirector.cs:946-948`의 여섯째 항이다.
보존 동결(`CharacterPreservationFreeze`)은 `IsSuspended`를 건드리지 않고 물리만 끈다. 주장 내용은 이렇다.
동결 중에는 **OS 히트테스트가 몸 자리 클릭을 아래 앱으로 넘기는데, 우리 전역 폴링의 `OverlapPoint`는 여전히 참**이다.
그래서 아래 앱으로 간 클릭이 우리 상태기계를 움직인다.
- 좌클릭: 드래그는 보류된다. 그러나 활쏘기 강제 Idle, 가출 찾기 신호, 스트레스 리셋은 실행된다.
- 우클릭: 허가가 발급되고 부채꼴이 펼쳐진다.

---

## 1. 가설 표

| # | 가설 | 검증 방법 | 결과(근거 줄) | 결론 |
|---|---|---|---|---|
| **H-a** | 동결의 「물리 끔」은 무엇인가 | `EnterPreservationFreeze` 판독, 운영 코드에서 `.simulated =`를 쓰는 곳 전수 조사, 콜라이더 소속 판독 | `Core/StickmanAgent.cs:1767-1774`: 숨김 중이 아니면 `SetBodiesSimulated(false)`(`:1773`)를 부른다. 이 함수는 `:1942-1948`이고 **Suspend가 쓰는 함수와 같다**(`:1862`). 대상은 `_allBodies = GetComponentsInChildren<Rigidbody2D>(true)`(`:647`), 곧 루트와 팔다리 전부다. 바꾸는 것은 `Rigidbody2D.simulated` 한 가지뿐이다. `Collider2D.enabled`, 레이어, `bodyType`, 렌더러는 **그대로**다(렌더러가 남는다는 사실은 `:1779` 로그 문구와 `CharacterPreservationFreeze.cs:13-16`에 적혀 있다). 운영 코드에서 `.simulated =`를 쓰는 곳은 **`StickmanAgent.cs:1947` 한 곳뿐**이다. 클릭 판정 콜라이더는 `StickmanClickHitbox.Awake`(`:116`)의 `GetComponentsInChildren<Collider2D>(true)`로 모은다. 여기에는 루트의 물리 캡슐, 루트의 GrabArea 트리거 캡슐(`Assets/Editor/SceneBootstrapper.cs:995-999`, 루트 `Rigidbody2D`에 붙음), 머리·팔다리 콜라이더가 들어간다. 모두 `_allBodies` 중 어느 한 몸에 붙어 있다. ※ 브리프는 이 위치를 `:1765-1768`로 적었으나 실제는 `:1767-1774`다. | **확정.** 동결 시 콜라이더의 물리 상태는 사용자 숨김·등급 2 Suspend와 **같다**. 차이는 렌더러(동결은 켜진 채)와 `IsSuspended`(동결은 거짓)뿐이다. |
| **H-b** | 우리 판정 API는 무엇이고, H-a 상태에서 참인가 | 판정 코드 판독 + Unity 문서 + 러너 xml 재판독 | API는 두 입구 모두 **콜라이더 단위 `Collider2D.OverlapPoint`**다. 좌클릭은 `StickmanClickHitbox.cs:262`(그리고 `:272`의 임시 콜라이더), 우클릭은 `AppControlDirector.cs:1049`다. `Physics2D.*` 월드 질의는 쓰지 않는다. **문서**: [`Collider2D.OverlapPoint`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider2D.OverlapPoint.html)는 *"Check if a collider overlaps a point in space."*와 EdgeCollider2D 예외만 적는다. 시뮬레이션되지 않는 몸에 대해서는 **말이 없다.** **러너 실측**: `Logs/vc-e4/P-full.xml`(2026-09-14 15:24Z~16:00Z, 커밋 14분 전 작업 트리. 커밋 후 해당 파일은 `git status` 기준 무변경. 802건 중 실패 6건, 모두 무관한 픽스처. SetUp·TearDown 실패 스위트 0). 이 실행에서 `HiddenCharacterLeftClickTests` **L1·L2·L3·L6이 Passed**이고 각 케이스의 마지막 「확인」 로그 줄까지 출력됐다. 네 케이스는 모두 `AssertInvisibleBodyTrap`(`Tests/PlayMode/HiddenCharacterLeftClickTests.cs:268-276`)을 거친다. 이 단언은 `simulated=false` 상태에서 **`OverlapPoint` 참**을 요구한다. 같은 실행의 `PanelsOnlyTierMouseEntryTests` B7 진단 줄도 `등급 2에서 … 콜라이더에 걸리는가=True`다. 양성 대조로 L0(`:259-265`)가 보이는 몸에서 기하 판정 참을 요구하며 Passed였다. | **확정(러너 실측).** 문서에는 근거가 없다. 이 판정은 Unity `6000.0.82f1`(`ProjectSettings/ProjectVersion.txt`) 기준이다. |
| **H-c** | OS 클릭 관통은 무엇으로 정하는가. H-b와 같은 질의인가 | 라이브러리 원문, 씬 직렬화 값, 우리 설정 경로, Unity 매뉴얼, 러너 xml | **라이브러리**: `Library/PackageCache/com.kirurobo.uniwinc@304f9ba2aa4a/Runtime/Scripts/UniWindowController.cs`. `HitTestCoroutine`(`:657-686`)은 `WaitForEndOfFrame` 뒤 `HitTestByRaycast`(`:797-849`)를 부른다. 이 함수는 uGUI `EventSystem.RaycastAll` → 3D `Physics.Raycast` → **2D `Physics2D.GetRayIntersection(ray)`**(`:834`) 순으로 보고, 하나라도 맞으면 `onObject=true`다. `Update`(`:532-548`) 안의 `UpdateClickThrough`(`:626-650`)가 `isTransparent && !hit`일 때 `SetClickThrough(true)`로 네이티브 관통을 켠다. **설정**: `Assets/_Project/Scenes/Main.unity`에 `hitTestType: 2`(Raycast, `:434-436`), `_isTransparent: 1`(`:446-448`). `isHitTestEnabled`는 직렬화 값이 0이지만 시작 안전 지연 뒤 `SetClickThrough(true)`가 켠다(macOS `Platform/MacOS/MacWindowService.cs:1299-1300`, Windows `Platform/Windows/Win32WindowService.cs:1451-1452`). `transparentType`은 라이브러리 기본값 `Alpha`(`:272`)이고 씬 덮어쓰기도 운영 코드 대입도 0건이다. 따라서 Windows의 `ColorKey` 분기(`:664-668`, 항상 hit)는 **타지 않는다.** **문서**: [Unity 6 매뉴얼 Rigidbody 2D Simulated](https://docs.unity3d.com/6000.0/Documentation/Manual/2d-physics/rigidbody/rigidbody-2d-simulated-property.html) 원문은 *"When you disable a Rigidbody 2D's Simulated option, any attached Collider 2D is effectively 'invisible' and can't be detected by any physics queries"*다. 같은 페이지에 *"All internal physics objects … remain in memory."*도 있다. 콜라이더 단위 `OverlapPoint`가 참으로 남는 것과 모순되지 않는다(월드 질의에서만 빠진다). **러너 실측**: 위 L1·L2·L3·L6이 같은 상태에서 `Physics2D.GetRayIntersection(cam.ScreenPointToRay(...))` **없음**을 요구하며 Passed였다(`:272-275`). 라이브러리와 같은 오버로드다. 양성 대조 L0는 보이는 몸에서 레이캐스트 맞음을 요구하며 Passed였다. 반대편 대조로 `Phase5VisualLayerTests.HiddenRunawayCharacterStaysClickable…`는 Kinematic이고 `simulated=true`인 은신 몸이 `Physics2D.OverlapPoint`에 **잡힘**을 요구하며 Passed였다. ※ `MacWindowService.cs:1280-1281` 주석의 「픽셀 알파 기반 히트테스트」는 낡은 서술이다(`MacOverlayStateEnforcer.cs:68`이 Raycast로 바뀐 사실을 적고 있다). 판정 근거로 쓰지 않았다. | **확정: 서로 다른 질의이고, 동결 상태에서 갈라진다.** OS 쪽(월드 질의)은 **없음 → 관통 켬 → 클릭은 아래 앱**, 우리 쪽(콜라이더 기하)은 **참**이다. 남은 차이는 두 가지다. 탐침 하네스의 커서 좌표 출처(`WorldToScreenPoint`)가 라이브러리(`GetClientCursorPosition`)와 다르다(몸 중심에서는 결과 동일). 그리고 네이티브 관통이 실제로 아래 앱에 클릭을 넘기는지는 **실기 미확인**이다. |
| **H-d** | 동결 시작 프레임에 두 판정이 같은 상태를 보는가 | 실행 순서 속성 조사, 코루틴 시점 판독, OnDemandRendering 문서 | `[DefaultExecutionOrder]`는 운영 코드에 `StallAttributionProbe` 두 곳뿐이다. 에이전트·히트박스·`AppControlDirector`·라이브러리 사이의 Update 순서는 **정의되지 않았다.** 흐름: 프레임 N에서 `StickmanAgent.Update`(`:978`)가 동결에 들어가며 `simulated=false`가 즉시 적용된다. N의 프레임 끝에서 `onObject=false`, N+1의 `UpdateClickThrough`에서 관통이 켜진다(약 1프레임 지연). 해제 쪽도 대칭이다. ★ 유예 중에는 `DisplayChangeRenderHold`가 렌더 간격을 30 이상으로 올린다(`Platform/DisplayChangeRenderHold.cs`, `SuppressedRenderFrameInterval = 30`). [OnDemandRendering](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.OnDemandRendering.html) 원문: *"render-specific events, including those for … End of frame sections, don't occur during frames that Unity doesn't render"*. 따라서 `WaitForEndOfFrame` 코루틴이 렌더 프레임에서만 돌아 `onObject` 갱신이 **최대 약 29 루프 프레임(60Hz면 약 0.48초) 늦을 수 있다**(가설: 문서가 코루틴을 이름으로 적지는 않는다). 이 경우 동결 시작 뒤 짧은 동안은 OS가 아직 몸 자리 클릭을 **받는다**. 그 구간 클릭은 우리 창으로 오므로 결함이 아니다. | **판정: 순서와 무관하다.** 어긋남은 한 프레임짜리 경합이 아니라 **동결 구간 전체에 걸쳐 지속**된다(시작 쪽 0~0.5초만 예외일 수 있음). 가장자리 1프레임 차이만 순서에 의존하며 결론을 바꾸지 않는다. |
| **H-e** | 우클릭 경로(그리고 좌클릭 입구·받는 곳)에 동결 가드가 정말 0인가 | 파일별 grep. 무장 문자열 `IsPreservationFrozen\|BlocksNewSpectacle\|CharacterPreservationFreeze\|DisplayChangeHold`, 넓은 식 `-i 'freez\|frozen\|동결'`. 양성·음성 대조 | 무장 문자열 줄 수: `AppControlDirector` **0**, `GearRadialMenuWidget` **0**, `StickmanClickHitbox` **0**. `ArcheryDirector`·`StressGaugeDirector`는 0이다. `RunawayDirector`는 `:62`(`Update`) 1건뿐이고 `OnHitboxMouseDown`(`:216-221`)에는 없다. **양성 대조**: `DragThrowController` 1줄(`:117`), `RunawayDirector:62`. **음성 대조**: 없는 이름 `IsPreservationFrozenXYZ`는 0이다. **넓은 식**: `AppControlDirector` 2건. `:824`는 문서로 「보존 동결은 **넣지 않는다** — 그동안 캐릭터는 보인다」이고, `:990`은 앵커 「동결」이라는 다른 뜻이다. 가드는 0이다. `GearRadialMenuWidget`·`StickmanClickHitbox`는 0, 양성 대조 `RunawayDirector`는 2다. 받는 곳(`MouseDown +=` 운영 코드 5곳): `DragThrowController:45`(보류 가드 있음), `RunawayDirector:36`, `RunawayRenderer:267`, `ArcheryDirector:106`, `StressGaugeDirector:46`. 우클릭 여섯 항은 동결 중에 이렇게 된다. 0항 참(H-b). 2항은 `SwallowStateProvider`가 비어 fail-open으로 참(`:795`, `:905`). 4항 거짓(등급 2·가상 데스크톱이 아닐 때). 5항 거짓. 6항 참(`:830-831`에 동결이 없다). 결과는 **열림** → `TryGrantUserSummon`(`:985`) → `BeginReactionHold`(`:988`) → `ExpandOrReanchor`(`:991`). ※ 자기 신고: 첫 grep에서 `--include=*.cs`를 따옴표 없이 써 zsh가 `no matches found`를 냈다(죽은 프로브). 따옴표를 붙여 다시 돌렸고, 위 수치는 모두 명시 파일 경로로 잰 값이다. | **확정: 가드 0.** 이는 누락이 아니라 **문서화된 설계 선택**(`:824`)이다. 그 선택은 「보인다 = 클릭이 우리에게 온다」를 전제하는데, H-c가 이 전제를 **반증**한다. |

---

## 2. 종합 판정

**결함 확정(코드 경로 + Unity 의미론은 러너 실측과 매뉴얼 원문으로 확정). 단, 동결 상태 자체를 직접 실측한 것은 0건이고 OS 라우팅은 실기 미확인이다.**

### 근본 원인
세 상태에서 입력 게이트가 보는 축이 OS의 입력 수신 축과 어떻게 맞물리는지 비교하면 이렇다.

| 상태 | 렌더러 | 몸 `simulated` | OS 히트테스트(월드 질의) | 우리 기하(`OverlapPoint`) | E-4 게이트(`IsSuspended` / 6항) | 결과 |
|---|---|---|---|---|---|---|
| Suspend(숨김·등급 2·가상 데스크톱) | 끔 | 거짓 | 없음 → 아래 앱 | 참 | 닫힘 | 일치(E-4가 막음) |
| 가출 은신 | 끔 | **참**(Kinematic) | 맞음 → 우리 창 | 참 | 좌클릭 열림 / 우클릭 6항 닫힘 | 일치 |
| **보존 동결** | **켬** | **거짓** | **없음 → 아래 앱** | **참** | **열림** | **어긋남** |

E-4는 좌클릭 게이트를 캐릭터 축(`IsSuspended`)에, 우클릭 게이트를 「보이는가」에 걸었다. 그런데 OS가 클릭을 누구에게 줄지는 **몸이 물리 질의에 참여하는가**(`simulated`)가 정한다. Suspend와 가출 은신에서는 우연히 이 축과 게이트가 같이 움직였지만, 동결에서는 처음으로 갈라졌다.
`DragThrowController.cs:116`의 주석 「클릭 관통·히트테스트는 건드리지 않으므로(원칙 2) … «안 잡힌다»로 보인다」도 **거짓 전제**다. 동결은 몸을 물리 질의에서 빼므로 OS 히트테스트를 **바꾼다.** 보이는 캐릭터가 동결 구간 동안 통째로 클릭 관통 상태가 된다.

### 재현 시나리오(구체)
- **S1(모니터 변경, 캐릭터 보임)**
  - 조건: 창이 떠 있는 상태에서 외부 모니터를 분리·연결하거나 해상도를 바꾼다(`DisplayChangeRenderHold.OnTopologyTransition` `ChangeDetected`, 또는 macOS 라이브러리 신호). 유예는 정상 수 초이고 상한 15초(`MaxHoldSeconds`)다.
  - 입력: 이 구간에 보이는 캐릭터 몸을 **우클릭**한다.
  - 잘못된 결과(코드상): 아래 앱이 우클릭을 받아 **그 앱의 컨텍스트 메뉴**가 뜨고, 동시에 우리 부채꼴도 펼쳐진다. 부채꼴 캔버스에는 `GraphicRaycaster`가 있어(`GearRadialMenuWidget.cs:2348`) 버튼 자리는 라이브러리 `EventSystem.RaycastAll`로 **우리 창이 받는다.** 메뉴 두 개가 겹친다.
  - 이것은 원칙 2 위반이다. 아래 앱에 간 입력을 우리도 소비했고, 우리 표면이 사용자 앱 메뉴 위에 뜬다.
- **S1′(같은 조건, 좌클릭)**
  - 아래 앱이 클릭을 받는다(캐릭터 밑의 창·아이콘이 눌린다).
  - 우리 쪽은 드래그만 보류된다(`DragThrowController.cs:117`). 나머지는 실행된다.
    - 활쏘기 중이면 **동결 중에 상태 전이**가 난다: `ArcheryDirector.cs:131` 강제 Idle, 과녁·활·화살 제거, 락 반납.
    - 가출 중이면 `RunawayFoundSignaled`가 래치되고(`RunawayDirector.cs:220`) 해제 직후 「발견됨」으로 넘어간다.
    - 스트레스 방치 타이머가 리셋된다(`StressGaugeDirector.cs:112`, 보이지 않음).
    - 과자 클릭 판정(`RunawayRenderer.cs:368-378`)도 돈다.
- **S2(전용 전체화면 게임 실행 직후)**
  - 가설: 게임이 **디스플레이 모드를 바꿀 때만** 유예가 켜진다. 보더리스 전체화면은 토폴로지가 바뀌지 않아 유예가 없다.
  - 검증 방법: 실기 로그에서 `[보존동결] 시작`과 `[전체화면판정]`의 순서를 확인한다.
  - 약점 창은 [유예 시작, 등급 2 `Suspend`]다. `Suspend`가 걸리면 E-4 게이트가 닫는다. 브리프가 적은 「약 1.5~3초」는 **미검증 수치**다.
  - 이 창에서의 우클릭 결과는 S1과 같다. 등급 2가 붙기 전까지 부채꼴 버튼 영역이 게임 클릭을 받고, `Suspend` → `ArePanelsSuppressed` 순서로 접힌다.

### 심각도
**Major 유지를 권고한다.** 결함 형태가 E-4 본건과 같다(아래 앱의 클릭을 우리가 소비, 원칙 2). S1 우클릭은 눈에 보이는 이중 메뉴를 만든다. 다만 **발생 창이 유예 구간으로 한정되고 빈도가 낮다**는 점을 함께 적는다. 등급 결정은 리더가 한다.

---

## 3. PlayMode 탐침 설계 (구현하지 않음. 동결 상태에서 직접 박제하고, 수정 후 회귀를 잠그는 용도)

### 3-1. 러너 탐침 — L5 형태 + 우클릭 표본 (가칭 `L5b` / `B12`)
전제: `HiddenCharacterLeftClickTests`와 같은 하네스를 쓴다. `ProcessGlobalButtonSample`(좌클릭), 가짜 버튼 서비스의 `Secondary`(`PanelsOnlyTierMouseEntryTests.cs:294-304`의 `RightDown`/`RightUp` 형태), 몸 중심에 꽂은 에이전트 커서 공급자, `DisplayChangeHoldStatus.PublishStarted/PublishReleased` 리플렉션을 사용한다. **시간은 모두 벽시계**로 잰다(`TestClock.WaitUntil`/`SampleForSeconds`, `[Timeout(180000)]`, 프레임 수로 기다리기 금지).

1. **양성 대조(동결 전)**
   - `AnyColliderContains`가 참이고 `RaycastAtCursor().collider`가 null이 아닌지 본다(라이브러리 `:834`와 같은 오버로드 `Physics2D.GetRayIntersection(ray)`).
   - 우클릭 1회 뒤 `ExpandTotalSeconds + ObserveSlackSeconds` 동안 `_menu.IsVisible`이 참이 되는지 본다. 확인 뒤 접는다.
2. **동결 진입**
   - `PublishStarted`를 부른 뒤 `WaitUntil(IsPreservationFrozen, 2s)`로 기다린다. 전제로 `!IsSuspended`를 단언한다.
   - 기록할 값:
     - `GetComponentsInChildren<Rigidbody2D>(true)` 전부의 `simulated`(기대: 모두 거짓)
     - `AnyColliderContains`
     - `RaycastAtCursor().collider`
     - 몸 중심 화면 좌표에서의 `EventSystem.RaycastAll` 결과 수
3. **좌클릭 표본**
   - 기록 대상: `_mouseDownCount` 증가량, `SuspendedPressIgnoredCount` 증가량, 전이 수 증가량, `SpectacleEventLock.IsActive`.
   - 선택 변형: 동결 **전에** 활쏘기에 진입시켜 두고 동결 중 누른다. 이 경우 전이 수 증가량과 `CurrentStateId`를 기록한다.
4. **우클릭 표본**
   - `RightClickGateEvaluationCount` 증가량은 **반드시 1**이어야 한다. `LastRightClickGateSample`은 평가 전 기본값이 전부 거짓이라, 이 확인이 없으면 `Opened=false`가 **죽은 프로브**가 된다.
   - `LastRightClickGateSample`의 여섯 항과 `Opened`를 모두 기록한다.
   - 관측 창(벽시계 `ExpandTotalSeconds + ObserveSlackSeconds`) 동안 기록할 값:
     - `_menu.IsVisible`이 한 번이라도 참이었는지
     - `IsUserSummonGrantActive`
     - `IsReactionHoldActive`
   - 펼쳐졌다면 버튼 한 개 중심에서 `EventSystem.RaycastAll` 결과 수를 센다(부채꼴 표면이 OS 클릭을 받는지).
5. **해제와 반대편 대조**
   - `PublishReleased`를 부른 뒤 `WaitUntil(!IsPreservationFrozen, 2s)`로 기다린다.
   - 같은 자리에서 레이캐스트가 맞는지, 우클릭이 열리는지 다시 본다.
   - **각 시점에 기록할 값은 모두 `Debug.Log` 한 줄에 남긴다.** 그래야 xml `output`에서 수치로 읽힌다.

**판정표**(동결 중 값)

| `simulated` 전부 | 기하 | 레이캐스트 | 좌 `MouseDown` 증가량 | 우 `Opened` | 판정 |
|---|---|---|---|---|---|
| 거짓 | 참 | 없음 | 1 | 참 | **결함 박제(현 코드 기대값)** |
| 거짓 | 참 | 없음 | 0 | 거짓 | 수정 후 기대값 |
| — | — | **맞음** | — | — | 기각: OS가 받으므로 반응은 정당 |
| — | **거짓** | 없음 | 0 | 거짓 | 기각: 우리 폴링도 거짓(H-b 반증) |
| 하나라도 참 | — | — | — | — | 측정 무효: 동결이 물리를 안 껐다(H-a 반증) |

### 3-2. 실기 탐침 (OS 라우팅, 러너로는 원리상 볼 수 없음)
- 리더 승인 후 **인스턴스 1개**로, macOS와 Windows 각각에서 한다.
- 절차: 해상도를 바꾸거나 외부 모니터를 분리한 직후, `[보존동결] 시작` 줄이 찍힌 뒤 보이는 캐릭터를 우클릭한다.
- 관측할 것:
  - 아래 앱(Finder/탐색기/바탕화면) 컨텍스트 메뉴와 우리 부채꼴이 **동시에** 뜨는가.
  - Windows는 `LayeredHybridTimeline`의 「라이브러리 관통 ON」 줄이 동결 시작 뒤에 찍히는가.
- 좌클릭 판: 캐릭터 밑에 창을 두고 누른 뒤, 그 창이 활성화되는가를 본다.

---

## 4. 수정 방향 후보와 N-8 목록 영향

N-8 목록은 `docs/verify/DISPLAY_CHANGE_PATH_FILES.md`의 `DCP-LIST` 블록(A~E)이다. 목록 **안**에 있는 파일: `Core/StickmanAgent.cs`(B), `Interaction/DragThrowController.cs`(B), `Interaction/RunawayDirector.cs`(B), `Core/CharacterPreservationFreeze.cs`(B), `Platform/MacOS/MacWindowService.cs`·`Platform/Windows/Win32WindowService.cs`·두 Enforcer(A).
목록 **밖**(DCP 목록 줄 0건 — ★ 2026-09-15 verify-change 정정: 문서 전체로는 `GearRadialMenuWidget.cs`가 `DISPLAY_CHANGE_PATH_FILES.md:316` 설명 줄에 1건 있으나 목록 밖 언급이라 결론 불변. 양성 대조로 `DragThrowController.cs`는 목록 줄에서 잡힌다): `Interaction/StickmanClickHitbox.cs`, `Interaction/AppControlDirector.cs`, `Interaction/GearRadialMenuWidget.cs`, `Interaction/ArcheryDirector.cs`, `Interaction/StressGaugeDirector.cs`, `Interaction/RunawayRenderer.cs`.

| 안 | 내용 | 수정 파일 | N-8 | 평가 |
|---|---|---|---|---|
| **A(권고)** | 입력 게이트 축을 **「몸이 물리 질의에 참여하는가」**로 바꾼다(`Blackboard.Body.simulated`를 읽기만 함). 좌클릭은 `BeginPress` 첫 게이트에 `IsSuspended`와 합성하고, 우클릭은 여섯째 항 입력에 합성한다. OS 판정(H-c)과 **같은 축**이라 Suspend·동결·가출 은신 세 경우를 한 식으로 설명한다. 가출 은신은 `simulated=true`라 L4 「찾기」가 보존된다. 앞으로 `simulated=false`를 쓰는 곳이 생겨도 자동으로 따라간다. | `StickmanClickHitbox.cs`, `AppControlDirector.cs`(`RightClickFanGatePolicy` 입력) | **목록 밖** | 가장 좁다. 주의 1: 임시 콜라이더(과자)는 캐릭터 몸과 별개라서 게이트를 **캐릭터 콜라이더 명중에만** 걸어야 한다. 그렇지 않으면 동결 중 과자 클릭을 OS는 받는데 우리는 무시하는 역방향 어긋남이 생긴다. 주의 2: `AppControlDirector.cs:824` 문서의 설계 문장을 뒤집는 결정이므로 `ux-designer` 확인이 필요하다. |
| B | `IsPreservationFrozen`을 두 입구에 추가한다. | 같음 | 목록 밖 | 단순하다. 그러나 `:824`의 설계를 뒤집고 축이 임시방편이다. 다음에 `simulated=false`를 쓰는 곳에서 같은 결함이 재발한다. |
| C | OS 판정을 되읽는다. `SwallowStateProvider`에 `UniWindowController.isClickThrough` 되읽기를 꽂는다(`AppControlDirector.cs:894-905`가 이미 예고한 자리). 좌클릭에도 같은 창구를 쓴다. | 새 중립 `Platform/` 파일 + 두 입구 | 새 파일은 목록 밖. **`MacWindowService`/`Win32WindowService` 안에 구현하면 목록 A** | 구조적으로 가장 정확하다(OS가 실제로 한 일을 본다). 비용: 렌더 유예 중 코루틴 지연(H-d)으로 동결 시작 쪽 0~0.5초 판정이 늦을 수 있다. 가장자리 경합(기존 문서의 최악 약 67ms)도 남는다. dev-platform 배정이 필요하다. |
| D | 동결에서 `simulated=false` 대신 Kinematic과 속도 0을 쓴다. | `StickmanAgent.cs` | **목록 B**. 1.0 출시 후보의 E-3 증거가 떨어진다 | 비권고. 관절·랙돌·접촉 영향이 넓다. |

추가로 함께 고칠 것:
- **`DragThrowController.cs:116` 주석의 거짓 전제**: 이 파일은 목록 B라서 주석만 바꿔도 목록 diff가 생긴다. 1.0 후보 이후로 미루고, 그때까지 이 문서를 반증 기록으로 둔다.
- **`HiddenCharacterLeftClickTests.cs:483`**의 「공통 입구는 통과하고(MouseDown)」: A·B·C 중 무엇을 택하든 기대가 바뀐다. 테스트 문구와 L5를 함께 갱신해야 한다(L5의 현 단언은 `MouseDown` 개수를 보지 않으므로 초록은 유지된다. 그래서 **조용히 낡는다**).

**coder 단계로 복귀 필요**(안 선택은 리더와 `game-architect`, 6항 설계 문장은 `ux-designer`).

---

## 5. 백로그 ③ — 숨김 해제 경계 한 프레임 누름

**주장**: 숨김 해제 직전에 한 프레임 누른 것이 Resume 뒤 상승 엣지로 잡혀 게이트를 통과한다(`StickmanClickHitbox.cs:165-170`).

**판독**
- 엣지는 매 폴링마다 `_globalPressedPrev`를 갱신한다(`:165-166`). 게이트에서 막혀도 prev는 참으로 남는다.
  - 따라서 **숨은 동안 표본에 이미 눌림으로 잡힌 누름**은 Resume 뒤 다시 엣지를 만들지 않는다.
- 통과하는 경우는 하나다. 물리적 누름이 **숨김 마지막 폴링과 해제 뒤 첫 폴링 사이**에 일어나고, 첫 「눌림」 표본이 `IsSuspended=false`인 뒤에 읽힐 때다.
  - 창 폭은 숨김 중 루프 한 프레임 간격이다. `Platform/FramePacing.cs:306` 기준 macOS는 vSync 4이므로 60Hz 화면에서 약 67ms, 120Hz에서 약 33ms이고, Windows는 30fps로 약 33ms다.
  - 해제 프레임 안에서 `StickmanAgent.Update`와 히트박스 `Update`의 순서가 정의되지 않았으므로(H-d) 해제 프레임 자체도 둘 중 하나로 갈린다.
- 우리 쪽 결과는 **`MouseDown` 1회**다. 받는 곳은 기존 평상시 경로 그대로다.
  - Idle·Walk면 `DragThrowController`의 일반 드래그 1회(상태·락·클릭 캡처 검사 모두 평소대로).
  - 활쏘기면 취소 1회, 가출이면 찾기 1회, 스트레스 리셋.
  - 새 상태, 잠금 누수, 되돌릴 수 없는 효과는 없다. 우클릭도 같은 구조다(`AppControlDirector.cs:927` 엣지 + 평가 시점 `IsSuspended`). 결과는 부채꼴 1회다.
- **가설(실기 미확인)**: 누름 순간에는 관통이 아직 켜져 있으므로(렌더러·물리 복귀 뒤 첫 프레임 끝에 `onObject` 갱신) mouse-down은 **아래 앱에도** 간다. 그 제스처의 드래그·업도 OS 캡처상 아래 앱이 받을 수 있다. 제스처 1회가 이중으로 전달된다.
  - 검증 방법: 실기에서 단축키로 숨김을 해제하는 것과 동시에 캐릭터 자리 창 위를 누르고 끌어, 아래 창의 선택·이동 여부를 본다.

**판정: 우리 쪽 결과는 평범한 입력 1회(드래그 등)뿐이다. Minor.** 사용자 행위로 해제하는 경로(설정창 [보이기] 등 uGUI 버튼)는 클릭이 뗄 때 발화하고 누름은 숨김 중 표본에 이미 잡혀 있으므로 이 엣지를 만들지 않는다. 남는 경로는 키보드 해제와 자동 해제(등급 2 종료)와 겹친 우연한 누름뿐이다. 별도 수정은 권고하지 않는다. §4의 안 A를 채택하면 해제 프레임에 `simulated`와 `IsSuspended`가 같은 줄(`StickmanAgent.cs:1896-1906`)에서 함께 풀리므로 이 판정도 바뀌지 않는다.

---

## 6. 플랫폼 영향
- **Windows**: 같은 결함이다. 코드가 공유된다: `UniWindowController` Raycast 히트테스트, `transparentType=Alpha`(`ColorKey` 분기 미사용), 플랫폼 중립 `StickmanAgent` 동결, Physics2D 의미론. 유예 시작 신호는 토폴로지 감시기(`WindowsOverlayStateEnforcer.cs:821`)에서 온다. Enforcer 재적용(`:472`)이 `isClickThrough=DesiredClickThrough(true)`를 대입하는 방향은 동결 중 라이브러리 판정과 같으므로 결론이 바뀌지 않는다.
- **macOS**: 같은 결함이다. 유예 신호는 토폴로지(`MacOverlayStateEnforcer.cs:683`)와 라이브러리 모니터 변경(`:906-907`) 두 경로다.
- 이 라운드는 문서 1개만 추가했고 코드 변경은 0이다.
