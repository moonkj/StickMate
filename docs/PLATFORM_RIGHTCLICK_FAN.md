# 캐릭터 우클릭 → 부채꼴 메뉴 : 플랫폼 층 가능성 판정

작성 2026-09-05 · `dev-platform` · **조사·설계 전용 라운드(프로덕션 `.cs` 수정 0줄)**
대상 지시: 사용자 2026-09-05 — *"지금은 메뉴 스크류모양이 따로 있는데 그냥 캐릭터에서 마우스 오른 쪽 버튼 누르면 촤르륵 펼쳐지게 변경"*

## 이 문서가 확인한 것 / 확인 못 한 것 (먼저 가른다)

| | 내용 |
|---|---|
| **소스 실측으로 확인** | 양 플랫폼 서비스 구현, UniWindowController 패키지 C# 원문, macOS 네이티브 번들 심볼(`strings`), 좌표 변환기, 프레임 페이싱 등급, 트레이/앱전환기 정책, git 이력의 구(舊) 우클릭 메뉴 구현 |
| **★ 이 머신에서 실행 실측(2026-09-05 후속)** | **macOS 보조 클릭 축** — 읽기 전용 CoreGraphics 프로브 2종, 2회 독립 실행, **누적 8/8 일치·반례 0건**, 양성 대조 2건 통과. **§9** |
| **문서 근거로 판단(실행 미확인)** | `GetAsyncKeyState`의 물리 버튼 규약(MS 문서), Windows 11 트레이 오버플로 기본 숨김, Unity `OnDemandRendering`에서 `WaitForEndOfFrame` 발화 빈도, **⌃+클릭이 `leftMouseDown`으로 남는다는 것**(115초 관측 창에 ⌃+클릭 0회 = **미관측**) |
| **확인 불가 — 실기가 필요** | Windows 실기 전부(이 머신에 Windows 없음), 히트테스트 지연의 실측 밀리초 |

**"고쳤다"고 쓸 수 있는 것이 이 문서에는 하나도 없다.** 전부 "이렇게 동작할 것으로 판단한다"이다.

---

## 0. 결론 요약 (여섯 줄 — 6번은 2026-09-05 후속 실측)

1. **우클릭 감지 채널은 이미 양 플랫폼에 다 있다.** 구현 3개(`MacWindowService` / `Win32WindowService` / `FallbackPlatformWindowService`)가 살아 있고 **소비자는 0명**이다 — 2026-08-31에 UI만 지우고 채널은 남겼다.
2. ★ **브리프의 전제 「우클릭이 아래 창으로 그대로 전달된다」는 커서가 캐릭터 위에 있는 동안에는 거짓이다.** 히트테스트는 **버튼을 구분하지 않는다**. 커서가 잡기영역(GrabArea) 안이면 OS 레벨 클릭관통이 이미 꺼져 있고, **지금도 우클릭은 우리 창이 먹고 있다.** 그러고 아무 일도 안 한다 — 이 앱에서 가장 나쁜 상태(먹기만 하고 안 주기)이고 정확히 사용자가 신고한 증상이다.
3. **따라서 부채꼴을 우클릭에 얹는 것은 비침해 비용을 새로 만들지 않는다.** 이미 치르고 있는 비용에 값을 붙이는 것이다. 다만 **한 프레임 경합**(히트테스트가 커서보다 1렌더프레임 늦다)에서만 「아래 메뉴 + 우리 메뉴」 이중 발생이 가능하고, 그것을 **정확히 닫는 방법이 있다**(§2-5 삼킴상태 게이트).
4. **2026-08-31 삭제 결정의 근거 2(「비침해가 실제로 개선된다」)는 구조적으로 성립하지 않았다.** 가로채는 주체는 우클릭 폴링이 아니라 **좌클릭 드래그용 잡기영역 히트테스트**이고 그건 그대로 남았다. `AppControlDirector.cs:174`의 기동 로그가 지금도 사용자에게 **거짓말**을 하고 있다.
5. **톱니를 없애면 macOS에는 OS 레벨 상시 진입점이 0개가 된다.** Windows는 트레이가 있지만 **Windows 11 기본값이 오버플로 숨김**이라 "보장"이라고 쓸 수 없다. 이건 리더 판단 항목이다.
6. ★ **(2026-09-05 후속 실측)** macOS `CGEventSourceButtonState`는 **논리(매핑 후) 버튼**을 보고한다 — 물리 오른쪽 버튼이 없는 트랙패드 두 손가락 제스처가 `button1=true`를 냈다(2회 독립 실행, 8/8). ⇒ **Windows(물리 버튼)와 축이 다르다는 것이 확정**됐고, **고칠 쪽은 Windows 하나(2줄)**다. 그리고 **보조 클릭을 끈 사용자에게는 그 값이 영원히 false**이므로 **그 사람은 부채꼴을 열 수 없다** — `ux-widgets` 미확정 10번의 답이자 새 리더 판단 **L-8**이다. **§9**

---

## 1. 우클릭 감지의 현재 실체

### 계약
`Platform/IGlobalPointerButtonService.cs:37,48` — `TryGetPrimaryButtonPressed` / `TryGetSecondaryButtonPressed`.
같은 파일 클래스 문서가 우클릭의 존재 이유를 이미 적어 두었다: *"오른쪽 버튼은 이 앱에서 아무도 쓰지 않으므로, 「캐릭터를 우클릭하면 제어 메뉴가 뜬다」는 데스크톱 앱의 관습적인 조작을 기존 상호작용과 충돌 없이 얹을 수 있다(Interaction/AppControlDirector.cs)."*
**그 참조처는 지금 그 기능이 없다** — 계약 문서가 이미 낡았다.

### macOS
```csharp
// Platform/MacOS/MacWindowService.cs:2004
public bool TryGetSecondaryButtonPressed(out bool pressed)
{
    pressed = CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, kCGMouseButtonRight);
    return true;
}
```
- 상수: `:119 kCGEventSourceStateCombinedSessionState = 0`, `:121 kCGMouseButtonRight = 1` (`:107` P/Invoke).
- **권한 불요** — `CGEventTap`이 아니라 조회 전용 공개 C ABI. 근거는 `Platform/IGlobalKeyStateService.cs:139-144`의 **실측 기록**(2026-08-28, 권한 없이 호출 → 크래시 없이 false, 실제 입력 시 true).
- **항상 `true` 반환**(= "이 플랫폼은 지원한다"). 실패 경로가 없다.
- 창 포커스 무관. `SetClickThrough`와 완전 독립(`:1947` 리전 주석).

### Windows
```csharp
// Platform/Windows/Win32WindowService.cs:1519,1527
private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & KeyDownMask) != 0;
public bool TryGetSecondaryButtonPressed(out bool pressed) { pressed = IsDown(VK_RBUTTON); return true; }
```
- 상수: `:377 VK_RBUTTON = 0x02`, `:383 KeyDownMask = 0x8000`, `:313` P/Invoke.
- **권한 불요** — `:306-310`이 `GetKeyState`가 아니라 `GetAsyncKeyState`를 쓰는 이유(포커스 없어도 갱신)와 "후킹·관리자 권한 불필요"를 명시.
- **항상 `true` 반환.**

### 데코레이터
`Platform/FallbackPlatformWindowService.cs:698` — `_innerButton`이 있으면 위임, 없으면 `pressed=false; return false`. `NullPlatformWindowService`는 이 인터페이스를 아예 구현하지 않으므로 소비자는 `as IGlobalPointerButtonService`가 null인지로 지원 여부를 본다.

### 폴링 주기 · 지연
채널 자체는 **폴링이 없다**(요청 시점 조회). 주기는 소비자가 정한다. 저장소의 기존 관례:

| 소비자 | 주기 | 근거 |
|---|---|---|
| 구(舊) 우클릭 메뉴 | 0.05초(20Hz) | `AppControlDirector.PollInterval`(git `767c985^`) |
| 톱니 | 평소 0.05초 / 누른 동안 매 프레임 | `InfoGearIconWidget.cs:333 ClickPollInterval = 0.05f`, `:1372-1380` |
| 드래그 히트박스 | **매 프레임** | `StickmanClickHitbox.cs:135` (Update마다 무조건 조회) |

⇒ 우클릭은 **누르는 순간의 상승 엣지 1회**만 필요하므로 20Hz면 충분하고, 최악 지연 50ms는 "메뉴가 뜨는 데 걸리는 시간"에 흡수된다(부채꼴 열림 애니메이션 예산이 이미 0.30초 — `GearRadialMenuWidget.ExpandTotalSeconds`).

### ★ 패리티 갭 — **양쪽이 같은 의미를 주지 않는다** (신규 발견)

**Windows는 「물리 버튼」을 읽는다.** Microsoft 문서가 명시한다:
> *"GetAsyncKeyState ... checks on the state of the physical mouse buttons, not on the logical mouse buttons ... `GetAsyncKeyState(VK_LBUTTON)` always returns the state of the left physical mouse button, regardless of whether it is mapped to the left or right logical mouse button. You can determine the system's current mapping by calling `GetSystemMetrics(SM_SWAPBUTTON)`."*

우리 코드에 `SM_SWAPBUTTON`은 **0건**이다(전수 검색). 즉 **좌우 버튼을 바꾼 왼손잡이 Windows 사용자에게는:**
- 우클릭 부채꼴이 **주 클릭**에서 열리고,
- **드래그&던지기가 보조 클릭에 붙는다**(← 이건 우클릭 메뉴와 무관한 **기존 잠재 결함**이다. `TryGetPrimaryButtonPressed`가 `VK_LBUTTON`이므로 오늘도 그렇다).

**★ macOS는 2026-09-05에 실측으로 확정됐다 — §9 참고. 결론: macOS는 「논리(매핑 후) 버튼」을 보고한다.**
즉 **두 플랫폼은 실제로 서로 다른 축을 본다**(Windows=물리 / macOS=논리). 갭은 추정이 아니라 **확정**이다.

⇒ 이건 `PlatformParityAuditTests` 항목이다(§부록 A-1).

---

## 2. ★ 클릭관통과의 상호작용 — 이 라운드의 핵심

### 2-1. 클릭관통은 어떻게 켜지고 꺼지는가 (양 플랫폼)

이 앱은 **자기 창의 클릭관통 비트를 커서 위치에 따라 매 프레임 토글**하는 구조다. 그 토글의 주인은 우리 코드가 아니라 `UniWindowController`다.

```
UniWindowController.Update()                     :531
  └─ UpdateClickThrough()                        :547 → :626
       if (!isHitTestEnabled || hitTestType == None) return;
       bool hit = onObject;                      ← ★ 버튼을 구분하지 않는다
       if (_isClickThrough && hit)        SetClickThrough(false);   // 우리가 먹는다
       if (!_isClickThrough && isTransparent && !hit) SetClickThrough(true); // 관통

UniWindowController.HitTestCoroutine()           :657
  └─ yield return new WaitForEndOfFrame();       ← ★ 여기서 onObject가 갱신된다
  └─ HitTestByRaycast()                          :797
       EventSystem.RaycastAll  (uGUI)            :804
       Physics.Raycast / Physics2D.GetRayIntersection  :824,832
       레이어 마스크 = ~"Ignore Raycast"          :808
```

**네이티브 끝단:**

| | 기전 | 근거 |
|---|---|---|
| **macOS** | `-[NSWindow setIgnoresMouseEvents:]` | 번들 심볼 실측 — `strings LibUniWinC.bundle/Contents/MacOS/LibUniWinC \| grep setIgnoresMouseEvents` **히트**(양성대조 `NSWindow` 8건). 어댑터는 `MacWindowService.cs:1283 SetClickThrough` |
| **Windows** | `exstyle \|= WS_EX_TRANSPARENT \| WS_EX_LAYERED` / 해제 시 `&= ~WS_EX_TRANSPARENT`(레이어드는 일부러 안 지움) | `Platform/LayeredHybridPolicy.cs:18-21`이 패키지 C++ 원문(`libuniwinc.cpp`)을 인용. 어댑터는 `WindowsOverlayStateEnforcer.cs:441` |

**둘 다 창 단위 토글이고, 둘 다 「커서 아래에 우리 콜라이더/그래픽이 있는가」 하나로만 판정한다. 버튼 종류는 어디에도 들어가지 않는다.**

### 2-2. ⇒ 판정: **우클릭은 이미 삼켜지고 있다**

캐릭터에는 클릭 전용 트리거 콜라이더가 붙어 있다:
```
Assets/Editor/SceneBootstrapper.cs:867-871   GrabArea (CapsuleCollider2D, isTrigger)
  폭  = max(0.8유닛 × 배율, MinGrabAreaScreenPoints)   // :439 = 18pt 하한, 배율 1.0에서 ≈28pt
  높이 = 전신 + 상하 여백                              // :757
```
`Physics2DSettings.m_QueriesHitTriggers = 1`이라 트리거도 레이캐스트에 잡힌다(같은 파일 :858-860 주석이 명시).

⇒ **커서가 그 캡슐 안에 있으면 `onObject = true` → 클릭관통 OFF → 우리 전체화면 오버레이가 그 좌표의 모든 마우스 버튼을 받는다.**
⇒ **오늘, 부채꼴이 없는 지금도, 캐릭터 위 우클릭은 아래 앱에 도달하지 않는다.**

**그러므로 `Interaction/AppControlDirector.cs:174`의 기동 로그는 거짓이다:**
> `"★ 캐릭터 우클릭 메뉴는 2026-08-31에 폐지됐습니다 — 우클릭은 이제 밑에 있는 앱으로 그대로 관통합니다(비침해 개선, UX_FLOW 36-9)."`

그리고 `docs/UX_FLOW.md:4354-4356`의 삭제 근거 2도 같은 이유로 무너진다:
> *"우클릭을 잡으려면 그 순간 클릭관통을 부분 해제해야 하고 … 제공할 메뉴가 사라지면 그 비용만 남는다."*

**부분 해제는 우클릭 때문에 일어나는 것이 아니다. 좌클릭 드래그를 위해 상시 일어난다.** 메뉴를 지워도 비용은 1픽셀도 줄지 않았고, 남은 것은 **"먹고 아무것도 안 주기"** 하나뿐이었다.

⇒ **부채꼴 복원은 비침해를 악화시키지 않는다. 이미 치르는 비용에 대가를 지급하는 것이다.**

### 2-3. 그래도 남는 구멍 — **한 프레임 경합 (이중 메뉴가 실제로 가능한 유일한 경로)**

`onObject`는 `WaitForEndOfFrame`에서 갱신되고 `UpdateClickThrough`는 **다음** `Update`에서 그것을 적용한다(§2-1 코드 순서). 즉 **적용 상태가 커서보다 최소 1 렌더 프레임 늦다.**

그리고 이 앱은 **프레임 페이싱이 렌더 프레임을 솎아낸다**:

| 등급 | 조건 | `renderFrameInterval` | 히트테스트 갱신 주기(60Hz 기준) |
|---|---|---:|---:|
| Active | 기본 | 1 | 16.7ms |
| **Still** | **캐릭터가 1.6초 이상 서 있음. 입력 여부를 보지 않는다** | **4** | **66.7ms** |
| Calm | 캐릭터 Idle + 무입력 | 2 | 33.3ms |
| Away | 3분 무입력 + Idle | 4 | 66.7ms |
| DisplayOff | 화면 꺼짐 | — (4fps) | 250ms |

근거: `Platform/ViewerPresence.cs:475 DefaultStillDivisor = 4`, `:519`, `:429-436`(*"여기서 관측(presence)을 보지 않는다는 것이 이 줄의 전부다"* — **사용자가 마우스를 움직이고 있어도 Still로 내려간다**), `Platform/FramePacing.cs:351 StillDwellSeconds = 1.6f`, `:616-618` `OnDemandRendering.renderFrameInterval` 대입.

**사용자가 캐릭터를 우클릭하러 가는 순간은 정의상 「캐릭터가 가만히 서 있는」 순간이다 = Still 등급이 걸려 있을 확률이 높다.**

⇒ **최악 시나리오**: 커서가 캐릭터에 막 도착한 그 프레임에 우클릭 → 관통이 아직 ON → **아래 앱이 문맥 메뉴를 연다** + 우리 전역 폴링은 커서가 콜라이더 안임을 **실시간으로** 보므로(`StickmanBlackboard.TryGetCursorWorldPosition:320`이 매 호출마다 OS 커서를 다시 읽는다) **부채꼴도 연다** ⇒ **이중 메뉴.**
창(窓)은 최대 ~67ms(Still) / ~17ms(Active). 좁지만 **0이 아니다.**

> ★ `WaitForEndOfFrame`이 「렌더되지 않은 프레임」에서 발화하지 않는다는 것은 Unity `OnDemandRendering` 문서 근거이고 **이 저장소에서 실측된 적이 없다**(전수 검색: `WaitForEndOfFrame` 언급 1건, `LayeredHybridPolicy.cs:45`뿐). **구현 라운드는 이것을 먼저 재야 한다**(§8 L-3).

### 2-4. 「부채꼴 자체」의 삼킴 — 이미 검증된 패턴이 있다

부채꼴 버튼은 **캐릭터 콜라이더 밖**에 뜬다. 그러면 그 좌표는 `onObject=false` → 관통 ON → **버튼을 눌러도 클릭이 아래 앱으로 샌다.**
그리고 부채꼴 UI 이미지들은 전부 `raycastTarget = false`다(`GearRadialMenuWidget.cs:2011,2013,2045,2047,2309`) — uGUI 경로로도 안 잡힌다.

**해법은 이미 프로덕션에 있다 — 톱니가 쓰는 「차단막 BoxCollider2D」다:**
```csharp
// Interaction/InfoGearIconWidget.cs:983-999
// 차단막은 톱니 사각형이 아니라 <b>톱니 + 펼쳐진 버튼</b>의 합집합을 덮어야 한다 —
// 안 그러면 버튼을 눌러도 그 클릭이 밑의 앱으로 새어 나간다. 접히면 즉시 원래 크기다(비침해).
InteractiveScreenRect = _menu != null && _menu.IsVisible
    ? Union(IconScreenRect, _menu.UnionScreenRect)
    : IconScreenRect;
...
_clickTarget.transform.position = ...;  _clickTarget.size = ...;   // 씬 루트의 isTrigger BoxCollider2D (:1221-1223)
```

⇒ **캐릭터 우클릭 부채꼴도 같은 방식**: 씬 루트에 `isTrigger BoxCollider2D` 하나를 두고, **펼쳐진 동안에만** `Union(GrabArea 화면사각형, 부채꼴 UnionScreenRect)`를 덮는다. 접히면 즉시 0으로 되돌린다.
- **씬 루트에 둔다**(캐릭터 자식 금지) — 캐릭터가 걷거나 랙돌로 회전할 때 차단막이 따라 도는 사고를 막는다(`CharacterInfoWindow.cs:1258-1264`가 같은 이유를 적어 두었다).
- **`isTrigger = true`** — 캐릭터가 차단막에 부딪혀 튕기면 안 된다(`InfoGearIconWidget.cs:1223`).
- **"Ignore Raycast"(레이어 2)에 두면 안 된다** — 그 레이어만 히트테스트에서 제외된다(`SceneBootstrapper.cs:1706-1712`, 물리 바닥이 그 레이어에 있는 이유).

**이건 양 플랫폼 공통이다 — 히트테스트가 같은 한 벌이므로 플랫폼 분기가 0줄이다.**

### 2-5. ★ 이중 메뉴를 정확히 닫는 방법 — **삼킴상태 게이트** (권고 설계)

경합(§2-3)의 본질은 *"우리가 「캐릭터 위다」라고 판단한 순간과 OS가 실제로 클릭을 우리에게 준 순간이 다를 수 있다"*이다.
그렇다면 **우리 판단이 아니라 OS에 실제로 걸린 상태를 물으면 된다.**

```
지금 이 클릭이 실제로 삼켜졌는가?  ==  지금 클릭관통이 꺼져 있는가?
```

두 플랫폼 모두 그 값이 `UniWindowController.isClickThrough`에 있다(라이브러리가 마지막으로 네이티브에 건 값의 캐시 — `WindowsOverlayStateEnforcer.cs:362`가 *"캐시된 C# 필드를 그대로 돌려준다"*고 명시). **이 용도에는 캐시가 오히려 정확하다**: 세터의 네이티브 끝단(`SetWindowLong` / `setIgnoresMouseEvents`)이 동기 호출이라 캐시 = OS 상태다. (이 캐시가 진실이 **아닌** 경우는 `isTopmost`처럼 **OS가 우리 몰래 값을 바꾸는** 축인데, 클릭관통 비트는 OS가 스스로 바꾸지 않는다.)

**권고 배선 (CLAUDE.md 「정책은 중립, 플랫폼은 사실 조회만」 규칙 준수):**

| 층 | 새로 두는 것 | 하는 일 |
|---|---|---|
| `Platform/` (중립) | `IPointerSwallowStateSource` (신규, 선택적 캐퍼빌리티) | `bool TryGetPointerSwallowedNow(out bool swallowed)` — 지원 안 하면 false |
| `Platform/` (중립) | `RightClickFanGatePolicy` (신규, OS 호출 0줄) | 「지금 부채꼴을 열어도 되는가」 순수 판정. EditMode가 전 분기를 실행 가능 |
| `Platform/MacOS/MacWindowService` | 인터페이스 추가 구현 | `Controller.isClickThrough` 되읽기 **한 줄** |
| `Platform/Windows/Win32WindowService` | 인터페이스 추가 구현 | 같은 한 줄. (원하면 `WindowsWindowStyleProbe.TryReadStyle` + `WS_EX_TRANSPARENT`로 **OS 실측 교차 확인**까지 가능 — Windows에만 있는 보너스) |

판정 규칙 초안:
```
열어도 되는가 =
    커서가 캐릭터 히트 영역 안이다        (기존 StickmanClickHitbox.IsCursorOverHitbox와 같은 콜라이더 집합)
 && 우클릭 상승 엣지다
 && 삼킴상태가 「삼켜짐」이다              ← 이 항이 경합을 닫는다
 && !ArePanelsSuppressed                  (§4-1)
 && 드래그 중이 아니다                     (§3)
```
**삼킴상태를 못 읽는 환경**(조회 미지원 / 캐시 없음)에서는 **열지 않는 쪽이 아니라 여는 쪽**으로 간다 — 그 경우 우리가 잃는 것은 "가끔 아래 메뉴도 같이 뜬다"이고, 반대로 하면 "우클릭이 아예 안 먹는다"가 되어 사용자가 신고한 바로 그 증상으로 돌아간다. **이 선택은 리더 판정 항목이다(§8 L-2).**

### 2-6. 「이벤트를 삼키는」 더 강한 수단은 있는가 — **없다. 그리고 필요 없다**

검토했고 **전부 기각**한다.

| 안 | 판정 | 사유 |
|---|---|---|
| macOS `ignoresMouseEvents`를 **부분** 해제 | **불가** | `NSWindow` 단위 플래그다. 영역 개념이 없다. 지금 방식(커서 위치로 창 전체를 토글)이 이 API가 줄 수 있는 최대다 |
| Windows `WM_NCHITTEST` 커스텀 | **불가(우리 창이 아님)** | 우리 HWND는 **Unity 플레이어가 만든 창**이다. 윈도우 프로시저를 서브클래싱하면 Unity 내부 메시지 처리와 다투게 되고, 이 저장소에는 **같은 종류의 충돌로 영구 비활성된 해소기가 이미 있다**(`WindowsLayeredHybridResolver` — `LayeredHybridPolicy.cs:29-46`) |
| Windows `WS_EX_TRANSPARENT` 단독 제어 | **이미 그것을 하고 있다** | 라이브러리가 정확히 그 비트를 토글한다. 우리가 덧붙일 것이 없다 |
| `SetWindowRgn`으로 캐릭터 모양 리전 | **기각** | 창 리전을 바꾸면 **클라이언트 영역이 바뀌어 스왑체인이 재생성**된다. 이 저장소는 그 부류의 사고를 이미 겪었다(`OverlayStateReapplyPolicy` — SetBorderless 1px 래칫). 커서가 움직일 때마다 리전을 다시 그리는 것은 24시간 상주 앱에서 불가 |
| CGEventTap / SetWindowsHookEx로 이벤트 **가로채기** | **원칙 위반 — 검토 즉시 기각** | 접근성 권한/전역 후킹이 필요하고, 그 순간 이 앱은 「조회 전용」이 아니게 된다. `IGlobalPointerButtonService` 클래스 문서가 *"이벤트 탭(CGEventTap)처럼 접근성 권한을 요구하지도 않는다 — 유저 자산 불변 원칙과 무관한 순수 read-only 채널"*이라고 못박은 선을 넘는다 |

⇒ **결론: 새 삼킴 수단은 만들지 않는다. 이미 있는 히트테스트 + 차단막 콜라이더가 정답이고, 남은 경합은 §2-5 게이트로 닫는다.**

### 2-7. 원칙 저촉 여부

| 원칙 | 판정 |
|---|---|
| **3. 유저 자산 불변** | **저촉 없음.** 우리 창의 비트 하나만 만진다. 남의 창·파일·아이콘을 읽지도 쓰지도 않는다. 앱바 메시지 5종(`UserAssetImmutabilityAuditTests` 금지 목록)과 무관하다 |
| **2. 비침해** | **순증가 없음**(§2-2). 차단막이 **부채꼴이 펼쳐진 동안에만** 존재하고 접히면 즉시 0으로 돌아가는 한, 톱니가 이미 통과한 것과 **같은 계약**이다. 오히려 **개선**이다 — 지금은 먹고 아무것도 안 준다 |
| **잔여 비용(정직하게)** | 우클릭이 우리 창에 닿으면 **양 플랫폼 모두 우리 앱이 활성화(포커스 획득)될 수 있다.** 우리 창에는 `WS_EX_NOACTIVATE`가 없고(전수 검색 0건), macOS도 비활성 창 클릭이 앱을 활성화한다. **다만 이것은 좌클릭 드래그로 이미 매일 일어나고 있는 일이라 신규 회귀가 아니다.** 매뉴얼에 적어야 할 사실이다 |

---

## 3. 좌클릭 드래그와의 충돌

### 3-1. 현재 좌클릭 경로 (양 플랫폼 공통, 분기 0줄)
```
StickmanClickHitbox.Update()                     :132  매 프레임
  TryGetPrimaryButtonPressed                     :135
  rising && !_pressed && IsCursorOverHitbox()    :146  → BeginPress
  !down && _pressed                              :151  → EndPress   ← 놓기는 「엣지」가 아니라 「현재 상태」로 판정
IsCursorOverHitbox()                             :155  캐릭터 전체 Collider2D + 등록된 임시 콜라이더
OnMouseUp()                                      :198  전역 폴링이 「아직 눌림」이라 하면 무시(캡처 유실 대응)
```
구독자: `DragThrowController`(:45-46) / `RunawayDirector` / `RunawayRenderer` / `ArcheryDirector` / `StressGaugeDirector`.
상호배제는 `SpectacleEventLock`(`DragThrowController.cs:113`).

### 3-2. 세 가지 경우

| 경우 | 권고 동작 | 근거 |
|---|---|---|
| **좌우 동시 누름** | **먼저 잡은 쪽이 이긴다.** 드래그가 이미 시작됐으면(= `SpectacleEventLock`이 `DragAndThrow`로 잡혀 있으면) 우클릭 엣지를 **무시**한다. 반대로 부채꼴이 펼쳐져 있으면 좌클릭은 **부채꼴 히트테스트가 먼저 소비**한다 | 톱니가 이미 이 순서를 쓴다(`InfoGearIconWidget.BeginPress:1409-1440` — *"부채꼴이 펼쳐져 있으면 그쪽이 먼저다"*) |
| **우클릭 드래그**(누르고 끌기) | **끌기를 추적하지 않는다.** 우클릭은 **상승 엣지 1회**만 의미를 갖는다. 뗄 때(하강 엣지)는 아무 일도 하지 않는다 | 구(舊) 구현이 정확히 그랬다(git `767c985^:AppControlDirector.cs:288-301` — 상승 엣지만 봄). 우클릭 드래그에 의미를 주면 「데스크톱 러버밴드 선택」 관습과 충돌한다 |
| **우클릭 후 커서 이동** | 부채꼴은 **연 순간의 캐릭터 위치에 고정**한다. 캐릭터가 걸어가도 따라가지 않는다 | 따라가면 (a) 차단막이 매 프레임 화면을 훑고 다녀 비침해가 나빠지고, (b) 버튼이 커서 아래에서 도망간다. 톱니 부채꼴도 앵커 고정이다. **자동 접힘**(`AutoCollapseIdleSeconds = 6f`)과 **바깥 클릭 접힘**이 이미 있는 탈출구다 |

### 3-3. ★ 반드시 지킬 것 — **우클릭이 좌클릭 경로를 건드리지 않는다**
`StickmanClickHitbox`는 **한 글자도 바뀌면 안 된다.** 우클릭 소비자는 **별도 컴포넌트**로 두고 같은 콜라이더 집합을 **읽기만** 한다(구 구현의 `IsCursorOverCharacter()`가 그렇게 했다 — git `767c985^:339-353`).
이유: `_pressed` 플래그 하나로 이중 입력 경로를 엣지 트리거하는 구조라, 여기에 두 번째 버튼을 얹으면 **좌클릭 드래그가 조용히 죽는 회귀**가 난다. 그건 이 저장소가 이미 여러 번 당한 형태다.

---

## 4. 전체화면 감지 · 예약 띠 · DPI · 다중 모니터

### 4-1. 전체화면 감지 자동 숨김 — **부채꼴은 「표면」이다. 축을 틀리면 회귀한다**

`StickmanAgent`에는 숨김 축이 **네 개**이고 읽는 창구가 **셋**이다:

| 창구 | 뜻 | 부채꼴이 읽을 것인가 |
|---|---|---|
| `IsSuspended` (:172) | 축 1(전체화면 **게임**) + 축 2(사용자 숨김) — **캐릭터 축** | ✗ |
| **`ArePanelsSuppressed`** (:218) | 위 + **등급 1**(게임이 아닌 전체화면 앱) + 사용자 소환 임대 | **★ 이것** |
| `HidesScreenSurfaces` | "우리 흔적을 화면에서 지우는가" | 톱니/창 계열이 읽음 |

`:174-177`이 못박는다: *"화면 표면을 걷을지를 묻는 소비자는 이 값이 아니라 `ArePanelsSuppressed`를 읽어야 한다."*
**부채꼴은 창·팝오버와 같은 급의 화면 고정 표면이고, 차단막을 갖는다.** 페르소나 `재현`이 실기에서 잡은 사고(정보창 877×853이 전체화면 앱 위에 면적 50.38%를 덮은 채 **그 사각형의 클릭까지 먹었다**)가 정확히 이 축을 잘못 읽었을 때 나는 증상이다.

⇒ **구현 계약: `ArePanelsSuppressed`가 true가 되는 프레임에 부채꼴을 접고 차단막을 0으로 되돌린다.** 매 프레임 폴링(캐시 금지 — `:191-196`이 이유를 적어 두었다: *"캐시하면 「숨었는데 차단막은 남은」 한 프레임이 구조적으로 가능해지고, 그 한 프레임이 정확히 이 앱에서 가장 나쁜 상태다"*).

**macOS/Windows 차이 없음** — 판정은 `FullscreenSuspendPolicy`(중립)가 하고 플랫폼은 사실만 준다.

### 4-2. 프레임 페이싱 홀드 — **빠뜨리면 부채꼴이 15fps로 열린다**

부채꼴 열림은 0.30초짜리 애니메이션이다(`GearRadialMenuWidget.ExpandTotalSeconds`). Still 등급(§2-3)에서 열면 **제출이 15fps**라 "촤르륵"이 계단으로 보인다.
`FramePacing.HoldActiveForInteraction()`을 **부채꼴을 여는 그 자리에서** 불러야 한다. 톱니가 그렇게 한다(`InfoGearIconWidget.cs:1045`).
**이건 감사가 소스 텍스트로 잠근다** — `UiInteractionFramePacingHoldTests.cs:437` / `StillTierCompositorBudgetTests.cs:423`이 `"FramePacing.HoldActiveForInteraction("` 호출을 니들로 찾는다. **새 컴포넌트를 그 감사 목록에 추가해야 한다**(안 하면 조용히 빠진 채 초록이 된다 — CLAUDE.md가 경고한 「부재 단언은 썩으면 조용히 초록」의 사촌).

### 4-3. 좌표 변환 함정 — **여기가 이 절에서 가장 위험하다**

부채꼴은 **캐릭터(월드 좌표)에 앵커**되고 **화면 포인트 단위 치수**(궤도 111pt, 버튼 Ø44pt)를 갖는다. 이 저장소는 그 두 단위를 **일부러 분리**해 두었고, 섞으면 Windows에서 깨진다.

```
ScreenCoordinateConverter.cs:479-486
  · AutoDpiScale       — 좌표 변환용. "OS 좌표 1 = Unity 픽셀 몇 개인가"의 역수.
                          Windows에서 1.0인 것이 맞다.
  · AutoUiDensityScale — UI 크기 전용. "논리 포인트 1개 = 물리 픽셀 몇 개인가".
                          Windows는 GetDpiForWindow/96으로 OS에서 직접 읽어 보고한다.
```

| 함정 | macOS | Windows |
|---|---|---|
| **(가) 포인트 ↔ 픽셀** | `AutoDpiScale`(창 사각형/`Screen.width`)에 Retina 배율이 **실려 온다**(0.5). 그래서 예전 정의 `1/AutoDpiScale`이 우연히 맞는다 | **이 비가 항상 1.0이다** — `GetWindowRect`도 `Screen.width`도 둘 다 물리 픽셀. 150% 배율이 **어디에도 실리지 않는다.** 2026-08-31 사용자 신고 *"캐릭터창 해상도도 엄청 낮아서 글씨도 잘 안보임"*의 진원지 |
| **처방** | 부채꼴의 **치수·궤도·히트반경**은 반드시 `CanvasToUnityScreen(pt, config)`(`:556`)를 지난다. `ResolveDpiScale`로 곱하면 안 된다 | 동일 — 대신 **여기서 실제 값이 갈린다**(1.0 vs 1.5). 톱니가 `:965`에서 정확히 이 함수를 쓴다 |
| **(나) 앵커** | 캐릭터 위치는 `Camera.WorldToScreenPoint`로 바로 Unity 픽셀이 나온다. **DPI가 개입하지 않는다** | 동일 |
| **(다) 원점 오프셋** | `OverlayOriginOsScreen`(`:587`)이 창 좌상단. **OS 커서 좌표를 쓸 때만** 필요하다. `WorldToOsScreen`/`OsScreenToUnityScreen`(`:596`,`:618`) 한 벌만 쓰고 자기 식을 다시 쓰지 않는다(BUG-M5 컨벤션) | 동일 |
| **(라) 음수 좌표 모니터** | 주 디스플레이 왼쪽/위 모니터에서 `OverlayOriginOsScreen`이 음수. 변환식은 뺄셈이라 **정상 동작**한다 | 동일. **다만** `IsOverlayRectPlausible(:400)`이 「데스크톱 밖 판정」과 「원점 튐 판정」으로 보고를 **거부**할 수 있고, 거부되면 **직전 원점이 유지**된다(`:462-465`). 그 사이 커서→월드 변환이 통째로 어긋난다 — 부채꼴이 캐릭터에서 떨어진 자리에 뜬다 |
| **(마) 상단/하단 예약 띠** | 메뉴바(≈33pt) / Dock. `MacReservedScreenEdgeService`(`CGDisplayBounds` ↔ `visibleFrame`) | 도킹 작업표시줄(상/하/좌/우 전부 가능). `Win32WindowService.cs:1793 TryGetReservedEdgeInsetsPoints`(`GetMonitorInfo`의 `rcWork`−`rcMonitor`) |
| **처방** | 부채꼴은 **캐릭터 위치를 따라가므로 화면 가장자리·예약 띠와 충돌한다**(톱니는 우상단 고정이라 한 번만 계산하면 됐다). `SurfaceSafeAreaPolicy`의 `ClampCenterX`/`ClampTopDownCenterY`(`:164`,`:214`)를 **네 방향 다** 통과시킨다. 인셋은 `ReservedEdgeProbe.Insets(agent.PlatformService)` 한 창구로만 받는다 | 동일. **양 플랫폼 다 배선되어 있다**(패리티 갭 없음) |

**★ 「모른다」와 「0」을 구분하는 계약을 깨지 마라**: `ReservedEdgeInsets.MeasuredEdges` 비트가 없는 변은 **"없다"가 아니라 "모른다"**이고, 그 경우 **아무것도 바꾸지 않는다**(`IReservedScreenEdgeService.cs:30-34`). 화면 폭에서 역산해 *"아마 여기쯤 작업표시줄이 있겠지"* 하는 추정은 이 계약의 정면 위반이다.

### 4-4. 다중 모니터
오버레이 창은 **한 모니터만** 덮는다(`OverlayMonitorChoicePolicy` / `OverlayBoundsFitPolicy`). 캐릭터가 그 창 안에 있으므로 부채꼴도 그 창 안이다 — **모니터 경계를 넘어가는 부채꼴은 구조적으로 없다.** 다만 §4-3(라)의 원점 거부 상태에서는 좌표가 어긋나므로, **부채꼴을 여는 순간 `ScreenCoordinateConverter.RejectedOverlayRectCount`(`:355`)가 증가 중이면 로그를 한 줄 남기는 것**을 권고한다(원인 규명용 — 다음 신고에서 "부채꼴이 엉뚱한 데 뜬다"를 좌표계 문제로 즉시 가를 수 있다).

### 4-5. 스테일 문서 2건 (구현 라운드가 함께 고칠 것)
```
Platform/ScreenCoordinateConverter.cs:541   "히트테스트(AppControlDirector.HitTestMenuRow / ...)"
Platform/ScreenCoordinateConverter.cs:611   "(Interaction/AppControlDirector.cs의 우클릭 메뉴 — ...)"
```
둘 다 **2026-08-31에 삭제된 코드를 가리킨다.** 부채꼴이 들어오면 이 참조가 다시 유효해지므로 **새 컴포넌트 이름으로 갱신**한다. (`IGlobalPointerButtonService.cs:48`의 `AppControlDirector.cs` 참조도 같다.)

---

## 5. 모바일(iPad/iPhone) — 선택지와 비용만

**"우클릭"은 iOS에 없다.** 스크린샷 백드롭 모드(`docs/ARCHITECTURE.md` 0-1)에서는 클릭관통 개념 자체가 없고(`Platform/Mobile/ScreenshotBackdropPlatformService.cs:101` — `SetClickThrough`가 no-op), **앱 자신이 포그라운드라 "아래 앱의 문맥 메뉴"라는 경쟁자도 없다.** 즉 §2의 삼킴 문제가 통째로 사라지는 대신, **제스처 선택이 순수 UX 문제로 남는다.** 후보는 셋이고 비용이 뚜렷이 다르다. **(가) 길게 누르기(long press, 0.4~0.5초)** — iOS의 문맥 메뉴 관습과 정확히 같아 학습 비용 0이고 `UIContextMenuInteraction`을 안 써도 Unity `Touch` 지속시간으로 구현 가능하지만, **드래그&던지기와 같은 손가락·같은 시작점을 공유**하므로 "누르고 끌면 드래그 / 누르고 가만히 있으면 메뉴"라는 **분기 임계값**을 정해야 한다(데스크톱에는 없던 새 판정이고, 임계 이동거리·시간 두 상수가 늘어난다). **(나) 두 손가락 탭** — 드래그와 완전히 분리되어 오판이 0이지만 iPad에서는 자연스럽고 **iPhone 한 손 조작에서는 사실상 불가능**하다. **(다) 탭 → 부채꼴 즉시(우클릭 대응물 없이 좌/우 구분 폐기)** — 가장 단순하고 발견성이 최고지만 **드래그&던지기의 진입 트리거와 정면 충돌**한다(탭과 드래그 시작이 같은 이벤트). 데스크톱 코드와의 공유 관점에서는 **(가)가 유일하게 「같은 상태 기계 + 트리거만 교체」로 끝난다** — `IGlobalPointerButtonService`가 애초에 모바일에 구현되지 않는 선택적 캐퍼빌리티이므로, 모바일은 `IFanOpenTrigger` 같은 **트리거 추상화 하나만** 갈아 끼우면 되고 부채꼴 위젯·차단막·좌표 변환은 한 줄도 바뀌지 않는다(차단막은 모바일에서 아예 필요 없으므로 `ILocalClickCaptureService`처럼 **미구현으로 두고 소비 측이 `as`로 판정**한다). **지금 정하지 않는다** — 모바일은 여전히 미착수이고(`PlatformParityAuditTests:820` 항목이 러너에 건너뜀으로 떠 있다), 이 문단의 목적은 **데스크톱 구현이 트리거를 하드코딩하지 않게 하는 것** 하나다.

---

## 6. 톱니 제거 시 플랫폼 층에 남는 것

### 6-1. 지금 OS 레벨 존재 증거

| | macOS | Windows |
|---|---|---|
| Dock / 작업표시줄 버튼 | **없음** — `NSApplicationActivationPolicyAccessory`로 시작 시 내려간다(`AppSwitcherPresencePolicy.cs:80`, 실행 `MacSpaceBehaviorNative.ApplyAccessoryActivationPolicyOnce`) | **없음** — `ITaskbarList::DeleteTab`(`WindowsTaskbarButtonRemover`) |
| ⌘Tab / Alt+Tab | **없음**(같은 정책) | **없음** — `WS_EX_TOOLWINDOW`(`AppSwitcherPresencePolicy.cs:71`) |
| 메뉴바 / 트레이 아이콘 | ★ **없음. 구현 자체가 없다** | **있음** — `Shell_NotifyIcon`(`WindowsSystemTrayIcon.cs`) |
| 상시 화면 진입점 | **톱니뿐** | 톱니 + 트레이 |

macOS 트레이 갭의 공식 사유는 코드 상수로 박혀 있다:
```
Platform/SystemTrayPresencePolicy.cs:63-66  MacOsGapReason
  "별도 배정 필요 — NSStatusItem은 AppKit Objective-C API라 자체 네이티브 플러그인이 필요한데,
   이 프로젝트는 자체 Objective-C 플러그인이 반복 실패해 전부 제거한 이력이 있다.
   2026-09-03 실측: 검증된 대안인 UniWindowController 패키지에 NSStatusItem/NSMenu 관련 코드가
   0건이라 기각 사유가 아직 살아 있다."
```
그리고 `PlatformParityAuditTests:695 미해결_상시표시영역_아이콘이_macOS에는_없다`가 이 갭을 러너에 계속 띄우고 있다.

### 6-2. ⇒ 톱니를 없애면

**macOS: OS 레벨 상시 진입점이 0개가 된다.**
남는 것은 (a) **캐릭터 우클릭**(캐릭터가 보일 때만), (b) **전역 단축키 ⌃⌥⌘Q/P/I/K**(창을 한 번도 열어 본 적 없는 사용자에게는 **발견 불가능** — `AppControlDirector.cs:53-60`이 스스로 그렇게 적어 두었다).
**그리고 사용자 명시 숨김(⌃⌥⌘K) 중에는 캐릭터도 없다** ⇒ **마우스 경로가 정확히 0개.** 되돌리는 유일한 방법이 같은 단축키다.
※ 이 상태는 톱니가 있는 **지금도** 참이다(숨는 동안 톱니도 함께 사라진다 — `AppControlDirector.cs:191-193`). 톱니 제거가 **새로 만드는** 것이 아니라, **평상시에도 그 상태가 된다**는 점이 달라진다.

**Windows: 트레이가 유일한 상시 진입점이 된다. 그런데 「보장」이라고 쓸 수 없다.**
- **Windows 11 기본값은 오버플로(숨김)다.** 새로 등록된 트레이 아이콘은 셰브런(∧) 뒤로 들어가고, 사용자가 끌어내거나 설정에서 켜야 보인다. **승격을 강제하는 지원 API는 없다**(설계상 사용자 통제 대상이다).
- 게다가 셸이 **유휴 아이콘을 다시 오버플로로 강등**하는 동작이 보고된다.
- 코드에 이 사실이 **어디에도 적혀 있지 않다** — `WindowsSystemTrayIcon.cs`에 오버플로/숨김에 대한 언급 0건(전수 검색). **트레이가 「보인다」는 가정이 문서화되지 않은 채 서 있다.**
- 셸 재시작 복구는 되어 있다(`SystemTrayPresencePolicy.cs:133 TaskbarCreated`).

**트레이 메뉴가 실제로 주는 것은 3개뿐이다**(`SystemTrayPresencePolicy.cs:150-155`): `캐릭터 숨기기/보이기` · `설정 열기` · `StickMate 종료`.
**톱니 부채꼴이 주던 ①집중 모드 ②오늘 할일 ③(팝오버들) ④행동 명령 ⑤앱 종료는 트레이에 없다.** 즉 **트레이는 톱니의 대체재가 아니다.**

### 6-3. ⇒ 플랫폼 층 권고
1. **톱니를 지우기 전에, 캐릭터 우클릭이 「캐릭터가 보이는 모든 상태에서」 실제로 부채꼴을 여는지 확인한다.** 그러지 못하는 구간(전체화면 등급 2 / 사용자 숨김 / 다른 가상 데스크톱)에서는 **마우스 경로가 0**이고, macOS에는 되돌릴 OS 표면이 없다.
2. **macOS `NSStatusItem` 갭의 우선순위를 재평가해야 한다.** 톱니가 있는 동안에는 "있으면 좋은 것"이었지만, 톱니가 사라지면 **탈출구 설계의 필수 요소**로 성격이 바뀐다. → §8 L-5.
3. **Windows 트레이의 오버플로 기본 숨김을 `SystemTrayPresencePolicy` 문서에 사실로 적는다.** 지금은 아무 데도 없다. 적지 않으면 다음 사람이 "Windows는 트레이가 있으니 괜찮다"고 **틀린 전제 위에서** 톱니를 지운다.

---

## 7. 구현 난이도 · 위험 표

| # | 항목 | 난이도 | 위험 | 사유 |
|---|---|:---:|:---:|---|
| 1 | 우클릭 감지 배선(양 플랫폼) | **하** | **하** | 구현 3개가 이미 있고 **소비자만 0명**이다. git `767c985^`에 동작하던 폴링 원문이 남아 있다. 플랫폼 분기 0줄 |
| 2 | 캐릭터 히트 판정 | **하** | **하** | `StickmanClickHitbox.IsCursorOverHitbox`와 **같은 콜라이더 집합을 읽기만** 하면 된다. 구 구현이 그렇게 했다 |
| 3 | 부채꼴 차단막(펼침 시 Union, 접힘 시 0) | **중** | **중** | 톱니(`InfoGearIconWidget:983-999`)의 **검증된 전례**가 있다. 위험은 **접힘 시 0 복귀를 빠뜨리는 것** — 그러면 화면 한복판에 영구 클릭 흡수 구역이 생긴다(원칙 2 정면 위반). 씬 루트 배치 + `isTrigger` + 레이어 2 금지 세 가지를 동시에 지켜야 함 |
| 4 | **삼킴상태 게이트**(§2-5, 이중 메뉴 봉쇄) | **중** | **중** | 새 인터페이스 1개 + 중립 정책 1개 + 양 플랫폼 한 줄 구현. **실행 검증이 불가능한 것이 위험의 본체다** — 경합 창이 17~67ms라 실기에서도 재현이 어렵다. 게이트가 **과하게** 닫히면 "우클릭이 가끔 안 먹는다"가 되고, 그건 원래 증상으로의 회귀다 |
| 5 | `ArePanelsSuppressed` 폴링 배선 | **하** | **상** | 코드는 한 줄인데 **틀린 축을 읽으면 릴리즈 블로커**다(`IsSuspended`를 읽으면 등급 1에서 부채꼴+차단막이 전체화면 앱 위에 남는다 — 페르소나 `재현`이 실기에서 재현한 그 사고) |
| 6 | `FramePacing.HoldActiveForInteraction()` | **하** | **중** | 호출은 한 줄. **감사 니들 목록에 새 파일을 등록하지 않으면 조용히 빠진 채 초록**이 된다(`UiInteractionFramePacingHoldTests.cs:437`) |
| 7 | 좌표 변환(치수는 `CanvasToUnityScreen`, 앵커는 카메라) | **중** | **상** | **Windows에서만 깨진다** = 이 머신에서 절대 안 보인다. `AutoDpiScale`(Win=1.0)과 `AutoUiDensityScale`(Win=1.5)을 섞으면 150% 배율에서 궤도·히트반경이 2/3로 쪼그라든다. 2026-08-31 신고와 같은 병 |
| 8 | 네 방향 예약 띠 클램프 | **중** | **중** | 정책·프로브 모두 양 플랫폼 배선 완료(패리티 갭 없음). 위험은 **부채꼴이 움직이는 앵커를 갖는다**는 점 — 톱니는 한 곳에 고정이라 클램프가 한 번이었다. 매 열림마다 네 방향을 다시 봐야 한다 |
| 9 | 버튼 좌우 반전(`SM_SWAPBUTTON`) 대응 | **하**(하향) | **중** | ★ 2026-09-05 재평가. macOS는 **실측 확정**(§9 — 논리 버튼, 고칠 것 없음). Windows만 고치면 되고 **`GetSystemMetrics`가 이미 `:346`에 임포트돼 있다**(SM_ 상수 4개도 `:365-368`에 있음) ⇒ **상수 1줄 + 분기 1줄.** 신규 P/Invoke 0개. **기존 좌클릭 드래그의 같은 결함도 같은 줄에서 함께 고쳐진다** |
| 9b | ★ **보조 클릭 OFF 사용자의 폴백(⌃+클릭)** | **중** | **중** | §9-4. `GlobalKey.Control` 채널이 **양 플랫폼에 이미 있다**(Mac `:2027` / Win `:1563`) ⇒ 신규 플랫폼 API 0개. 위험은 **macOS 전용 분기**여야 한다는 점(Windows에서 Ctrl+좌클릭은 다중선택 관습)과 **드래그와 동시 발동**(§9-5) |
| 10 | Windows 실기 확인 | — | **상** | **이 머신에 Windows가 없다.** §7의 1~9 중 Windows에서만 드러나는 것이 3·4·7·9다. 사용자 실기 확인 없이는 어느 것도 "확인됨"이 될 수 없다 |
| 11 | macOS `NSStatusItem` 신설 | **상** | **상** | 자체 Objective-C 플러그인 필요. 이 프로젝트가 **반복 실패해 전부 제거한 이력**이 있다(`SystemTrayPresencePolicy.cs:63-66`). 톱니 제거의 **전제 조건이 되면** 이 라운드의 크리티컬 패스가 여기로 옮겨간다 |
| 12 | 이벤트를 **가로채는** 방식(CGEventTap/후킹/`WM_NCHITTEST`) | — | **금지** | §2-6. 검토 완료, 전부 기각. 되살리려는 사람이 있으면 §2-6 표를 먼저 읽을 것 |

---

## 8. 리더 판단 필요 항목

| # | 항목 | 무엇을 정해야 하는가 |
|---|---|---|
| **L-1** | ★ **2026-08-31 삭제 결정의 근거 2가 틀렸다는 사실을 어떻게 처리할 것인가** | `docs/UX_FLOW.md:4354-4356`(36-9)과 `Interaction/AppControlDirector.cs:174` 기동 로그가 **지금 사용자에게 거짓말을 하고 있다**(§2-2). 부채꼴 복원과 **별개로** 정정이 필요하다. 지금 고칠 것인가, 구현 라운드에 묶을 것인가 |
| **L-2** | **삼킴상태를 못 읽을 때 부채꼴을 열 것인가 말 것인가** | §2-5. 열면 「가끔 아래 메뉴도 같이 뜬다」(비침해 흠집), 안 열면 「우클릭이 가끔 안 먹는다」(사용자가 신고한 증상으로 회귀). `dev-platform` 의견은 **여는 쪽**이지만 원칙 2가 걸린 판정이라 리더가 정해야 한다 |
| **L-3** | **경합 창의 실측을 요구할 것인가** | `OnDemandRendering.renderFrameInterval > 1`에서 `WaitForEndOfFrame`이 솎이는지가 **문서 근거뿐**이다(§2-3). 재는 법: 부채꼴 열림 로그에 `Time.frameCount` / `Time.renderedFrameCount` / `OnDemandRendering.renderFrameInterval`을 함께 찍어 **히트테스트 갱신 간격을 역산**한다. 이걸 구현 라운드의 필수 산출물로 요구할 것인가 |
| **L-4** | **버튼 좌우 반전 대응 범위** | ★ 2026-09-05 갱신 — **macOS 축은 실측으로 닫혔다**(§9). 남은 선택은 (가) 갭만 등재 / (나) **Windows에 `SM_SWAPBUTTON` 2줄 추가**뿐이고, (나)는 **좌클릭 드래그의 기존 결함까지 같은 줄에서 고친다**. `dev-platform` 의견: **(나)**(비용이 2줄로 내려갔다) |
| **L-8** | ★ **보조 클릭을 끈 macOS 사용자를 어떻게 할 것인가** (ux-widgets 미확정 10번) | §9-3~9-5. 그 사용자에게 `TryGetSecondaryButtonPressed`는 **영원히 false**이고 **부채꼴을 열 수단이 0이다.** (가) ⌃+클릭 폴백을 넣는다(macOS 전용 분기 + `StickmanClickHitbox` 억제 1줄 필요 ⇒ `coder` 파일 침범) / (나) 폴백 없이 **문구만** 바꾼다(*"오른쪽 클릭"* → 없는 문 광고 문제는 남음) / (다) 톱니를 남겨 그 사용자의 경로를 보존한다(이번 라운드는 톱니 유지이므로 **지금은 (다)가 이미 성립**) |
| **L-9** | **⌃+클릭 폴백이 `Interaction/StickmanClickHitbox.cs`를 건드려야 한다** | §9-5. macOS에서 ⌃+좌클릭은 `TryGetPrimaryButtonPressed`도 true라 **드래그와 부채꼴이 동시에 발동**한다. 억제는 `BeginPress` 단일 깔때기에 한 줄이면 되지만 **그 파일은 `coder` 소유**다. 파일을 누구에게 줄 것인가 |
| **L-5** | ★ **톱니 제거의 선행 조건** | §6-3. 「캐릭터 우클릭 = 유일한 마우스 진입점」이 성립하는 상태 집합이 **캐릭터가 보이는 동안뿐**이다. macOS에는 되돌릴 OS 표면이 0개다. (가) 그대로 진행 / (나) `NSStatusItem`을 선행 배정 / (다) 톱니를 **완전 제거하지 않고** 「기본 숨김 + 설정으로 되살리기」로 남긴다 — 셋 중 하나를 정해야 `dev-platform`이 구현 순서를 짤 수 있다 |
| **L-6** | **Windows 트레이 「보인다」 가정의 문서화** | §6-2. Windows 11 오버플로 기본 숨김 사실을 `SystemTrayPresencePolicy` 문서(또는 `docs/`)에 적는 것을 이번 라운드 산출물에 넣을 것인가 |
| **L-7** | **파일 소유 배분** | 구현 라운드에서 `Interaction/`(부채꼴 위젯·차단막)은 `coder`/`ux-widgets`, `Platform/`(새 인터페이스·중립 정책·양 서비스 한 줄)은 `dev-platform`이 맡는 것이 자연스럽다. **`ScreenCoordinateConverter.cs`의 스테일 주석 2건(§4-5)은 누가 고칠 것인가** — 지금 다른 라운드가 만지고 있지 않은지 확인 필요 |

---

## 9. ★ 실측 부록 — macOS 「보조 클릭」 축 확정 (2026-09-05, 후속 라운드)

> `ux-widgets` 최종 스펙(`docs/UX_RIGHTCLICK_FAN_MENU.md` §12 미확정 **10번**)이 `dev-platform` 몫으로 남긴 항목:
> *"macOS에서 「보조 클릭」을 시스템 설정으로 끈 사용자에게 `CGEventSourceButtonState(…, kCGMouseButtonRight)`가 무엇을 보고하는가."*
> **이 절이 그 답이다. 추론이 아니라 이 머신에서 실제로 쟀다.**

### 9-1. 측정 방법 (재현 가능)

읽기 전용 C 프로브 2종. **`CGEventPost` 계열 0줄 — 어떤 입력도 주입하지 않는다.**
호출은 프로덕션 `MacWindowService.cs:1997-2007`과 **글자 그대로 같은 것**을 쓴다.

| 프로브 | 하는 일 |
|---|---|
| `probe.c` | `CGEventSourceButtonState` 3버튼 + `CGEventSourceCounterForEventType`(세션 누적) + `SecondsSinceLastEventType` |
| `corr.c` | **500Hz(2ms) 샘플링**으로 「`kCGEventRightMouseDown` 카운터가 증가하는 그 순간 `button1`이 true인가」를 상관 |

**양성 대조**(이 저장소 규칙: *"모든 「없음」 판정에 양성 대조"*):
`LeftMouseDown = 206`, `KeyDown = 28` — 둘 다 0이 아니므로 **카운터 조회 경로가 살아 있다.**
(이 대조가 없으면 `RightMouseDown = 0`이 「우클릭이 없었다」인지 「API가 죽었다」인지 구분되지 않는다.)

### 9-2. 결과 — **두 번 독립 실행, 누적 8/8, 반례 0건**

```
실행 1 (25초 / 12,500표본)   Right 이벤트 +2   button1 상승엣지 2   RightMouseDown 순간 button1=2 button0=0
실행 2 (90초 / 45,000표본)   Right 이벤트 +6   button1 상승엣지 6   RightMouseDown 순간 button1=6 button0=0
                                                      누적 8/8 일치 · button0 동반 0건 · 놓침 0건
```

⇒ **`CGEventSourceButtonState(…, kCGMouseButtonRight)`는 `kCGEventRightMouseDown`과 정확히 같은 축이다.**

### 9-3. ★ 결정적 관찰 — **macOS는 「논리(매핑 후)」 버튼을 보고한다**

이 머신에 연결된 포인팅 장치는 `ioreg -c IOHIDDevice`로 확인한 결과 **내장 트랙패드 하나뿐**이다
(`"Apple Internal Keyboard / Trackpad"`. Magic Mouse 미연결).

**트랙패드에는 물리적인 오른쪽 버튼이 존재하지 않는다.**
그런데 설정은 `com.apple.AppleMultitouchTrackpad` → `TrackpadRightClick = 1`(두 손가락 보조 클릭 ON),
`TrackpadCornerSecondaryClick = 0`이고, **그 두 손가락 제스처가 `button1 = true`로 보고됐다**(위 8건).

> **물리 버튼 1번이 없는 장치가 `CGEventSourceButtonState(button 1) == true`를 냈다.**
> ⇒ macOS는 보조 클릭 매핑을 **CGEvent 층보다 아래(드라이버/윈도우서버)에서** 적용한다.
> ⇒ 이 API가 돌려주는 것은 **물리 버튼이 아니라 논리 버튼**이다.

**이것이 §1 패리티 갭을 확정한다:**

| | 무엇을 읽는가 | 근거 |
|---|---|---|
| **Windows** `GetAsyncKeyState(VK_RBUTTON)` | **물리 버튼** | MS 문서 명시 (*"checks on the state of the physical mouse buttons, not on the logical … use `GetSystemMetrics(SM_SWAPBUTTON)`"*) |
| **macOS** `CGEventSourceButtonState(kCGMouseButtonRight)` | **논리 버튼** | ★ 위 실측 (물리 버튼 없는 제스처가 button1로 나옴) |

**두 플랫폼이 같은 이름의 API로 다른 것을 읽고 있다.** 갭은 이제 추정이 아니다.
그리고 **고칠 쪽은 Windows 하나다**(macOS는 이미 사용자가 기대하는 대로 동작한다).

### 9-4. ⇒ ux-widgets 미확정 10번에 대한 답

**「보조 클릭」을 끈 사용자에게 `CGEventSourceButtonState(…, kCGMouseButtonRight)`는 언제나 `false`다.**

근거 사슬:
1. 그 설정은 **드라이버 도메인**에 있다 — `com.apple.AppleMultitouchTrackpad`(트랙패드) /
   `com.apple.driver.AppleBluetoothMultitouch.mouse`(Magic Mouse, 값 `MouseButtonMode`).
2. 매핑이 CGEvent **아래**에서 일어난다(§9-3 실측). 드라이버가 button1을 안 내면 그 위에는 아무것도 안 올라온다.
3. ⇒ `kCGEventRightMouseDown`이 **생성되지 않고**, 따라서 button1 상태도 **영원히 false**다.

> **우리 코드의 버그가 아니다. 그 사용자에게는 보조 버튼이 존재하지 않는다.**
> **그리고 그 사용자는 캐릭터 우클릭 부채꼴을 영원히 열 수 없다.**

**꺼져 있는 조합 예시**: 트랙패드 `TrackpadRightClick=0` **그리고** `TrackpadCornerSecondaryClick=0` /
Magic Mouse `MouseButtonMode = OneButton`.

★ **이 머신에 `MouseButtonMode = OneButton`이 실제로 저장되어 있다.**
**다만 그 Magic Mouse는 지금 연결돼 있지 않으므로**(§9-3의 `ioreg`) *"이 사용자는 지금 우클릭을 못 한다"*고
쓰면 **거짓이다** — 실제로 우클릭을 8번 했다. 정확히 말할 수 있는 것은
**"이 설정 조합이 실사용 머신에 실재한다"**까지다. (이 문장을 과장할 뻔했고, 그래서 여기 적어 둔다.)

### 9-5. 폴백 후보 — ⌃+클릭. **비용은 낮지만 공짜는 아니다**

macOS 관습상 보조 클릭의 대체는 **Control+클릭**이다. 그런데:

- **이벤트 층에서 ⌃+클릭은 여전히 `leftMouseDown`이다**(Control 플래그가 붙을 뿐).
  AppKit이 `menuForEvent:` 경로로 라우팅하는 것이지 윈도우서버가 우버튼으로 바꾸지 않는다.
  ⇒ **`TryGetSecondaryButtonPressed`는 ⌃+클릭을 절대 못 본다.**
- **그런데 필요한 두 번째 채널이 이미 양 플랫폼에 있다**:
  `IGlobalKeyStateService.TryGetKeyPressed(GlobalKey.Control, …)`
  → `MacWindowService.cs:2027` (`kVK_Control`) / `Win32WindowService.cs:1563` (`VK_CONTROL`).
  ⇒ **신규 플랫폼 API 0개.** 판정식만 넓히면 된다:
  ```
  보조클릭이다 = TryGetSecondaryButtonPressed()
              || (macOS && TryGetPrimaryButtonPressed() && GlobalKey.Control 눌림)
  ```

**공짜가 아닌 이유 둘:**

1. **macOS 전용 분기여야 한다.** Windows에서 Ctrl+좌클릭은 **다중 선택** 관습이라 그쪽에 얹으면
   남의 앱 위에서 사용자가 하려던 일을 가로챈다(원칙 2). ⇒ 중립 정책의 `RuntimePlatform` 분기로 두고
   **`결정_` 접두사 감사**로 잠근다. **전례가 이미 있다** — `SurfaceSafeAreaPolicy.EnforcesBottomReservedBand`
   (`:91`, Windows만 true)와 그것을 지키는 `결정_하단_예약띠는_Windows에서만_강제한다`(`:289`).
2. ★ **드래그와 동시에 발동한다.** ⌃+좌클릭은 `TryGetPrimaryButtonPressed`도 true라
   **`StickmanClickHitbox`가 같은 프레임에 드래그를 시작한다.** 억제가 필요하고, 그 자리는
   `BeginPress` **단일 깔때기 한 줄**이다 — 그런데 **그 파일은 `coder` 소유**다. ⇒ 리더 판단 **L-9**.
   (설계상으로는 억제가 **옳다**: macOS에서 ⌃+클릭은 관습상 보조 클릭이지 「집어서 끌기」가 아니다.)

**미관측으로 남은 것**: 두 실행(합계 115초) 동안 **Control 동반 좌클릭 상승엣지가 0회**였다
(`b0riseWithCtrl = 0`). 즉 ⌃+클릭의 CGEvent 거동은 **문서 근거이고 이 머신에서 재현되지 않았다.**
입증도 반증도 아니다 — 실기 라운드가 같은 프로브로 ⌃+클릭을 10회 눌러 확인해야 한다.

### 9-6. 이 절이 바꾸는 판정

| 이전 | 지금 |
|---|---|
| §1 *"macOS는 미확인이다"* | **확정 — 논리 버튼.** 고칠 쪽은 Windows 하나 |
| §7 #9 난이도 **중** | **하** — `GetSystemMetrics`가 이미 `:346`에 있고 SM_ 상수도 `:365-368`에 있다. `SM_SWAPBUTTON = 23` 1줄 + 분기 1줄 |
| §8 L-4 *"(다) macOS 실기 확인 후 양쪽 동시 수정"* | **그 확인이 끝났다.** 남은 선택은 (가) 갭 등재 / (나) Windows 2줄 |
| ux-widgets 미확정 10번 | **답 나옴**(§9-4). 새 리더 판단 **L-8**로 승격 |

### 9-7. 재현 절차 (실기 라운드가 그대로 쓸 것)

프로브 원본: `<scratchpad>/cgprobe/{probe.c, corr.c}`. **저장소에 커밋하지 않았다**(진단 도구이지 산출물이 아니다).
필요하면 이 절의 명세로 30줄 안에 다시 만들 수 있다.
```
clang -O0 -o corr corr.c -framework ApplicationServices && ./corr 25
```
확인 순서: ① 양성 대조(LeftMouseDown > 0) → ② Right 이벤트 증가 > 0(아니면 **측정 무효, 재실행**) →
③ `RightMouseDown 순간 button1` 대 `button0` 비교. **②를 건너뛰고 ③이 0인 것을 「반증」으로 읽지 마라 —
그건 「우클릭을 안 했다」와 똑같이 생겼다.**

---

## 부록 A. `Tests/EditMode/PlatformParityAuditTests.cs` 추가 권고

접두사 규칙은 그 파일 `:43-58`(미해결_ / 실기미확인_ / 결정_ / 역방향_ / 해당없음_ / 갭추적_)을 따른다.
**사유에는 반드시 날짜를 단다**(대장 검사가 날짜 없는 사유를 실패로 잡는다).

| # | 이름(안) | 종류 | 무엇을 잠그는가 |
|---|---|---|---|
| **A-1** | `미해결_Windows_보조버튼_조회가_물리버튼이라_반전설정을_따르지_않는다` | `Assert.Ignore` | ★ 2026-09-05 개명·구체화. **갭은 확정됐고 한쪽만 남았다**: macOS는 **논리** 버튼(§9-3 실측 8/8), Windows는 **물리** 버튼(MS 문서). `SM_SWAPBUTTON` 참조 0건(양성 대조: `GetSystemMetrics` 5건 · `VK_RBUTTON` 2건 히트 ⇒ 프로브 생존 확인). 사유에 **「좌클릭 드래그&던지기에도 같은 결함이 오늘부터가 아니라 처음부터 있다」**를 함께 적는다 |
| **A-1b** | `결정_보조클릭_폴백은_macOS에만_건다` | 정식 검사 | ⌃+클릭 폴백을 넣기로 **판정한 경우에만** 추가. Windows에서 Ctrl+좌클릭은 다중선택 관습이라 **의도된 차이**다. `결정_하단_예약띠는_Windows에서만_강제한다:289`와 같은 형태로 **되돌리면 실패하게** 잠근다 |
| **A-2** | `우클릭_부채꼴이_양_플랫폼_모두_같은_히트테스트를_쓴다` | 정식 검사 | 새 위젯이 **자체 히트테스트를 만들지 않고** `hitTestType=Raycast` + 차단막 콜라이더 경로를 쓰는지 소스로 확인. 플랫폼 분기가 **생기면** 실패해야 한다 |
| **A-3** | `우클릭_부채꼴_차단막은_접히면_반드시_0으로_돌아간다` | 정식 검사 | 원칙 2. 소스에서 「접힘 경로에 차단막 해제가 있는가」를 본다(톱니의 `_clickTarget.enabled=false` 대응물) |
| **A-4** | `삼킴상태_조회가_양_플랫폼에_모두_배선되어_있다` | 정식 검사(또는 초기엔 Ignore) | `IPointerSwallowStateSource`가 두 서비스의 **기반 목록**(`class X :`과 여는 중괄호 **사이**)에 있는지 — `상단_예약띠_조회가_양_플랫폼에_모두_배선되어_있다:119`가 쓰는 3겹 검사와 같은 방식. **주석에 이름이 있는 것으로 통과하면 안 된다** |
| **A-5** | `우클릭_부채꼴이_프레임페이싱_홀드를_부른다` | 정식 검사 | `UiInteractionFramePacingHoldTests.cs:437`의 니들 목록에 새 파일을 등록. **등록을 빠뜨리면 조용히 초록이 되는 부류**라 이 항목이 그 등록 자체를 잠근다 |
| **A-6** | `실기미확인_우클릭_삼킴이_Windows_실기에서_확인되지_않았다` | `Assert.Ignore` | Windows에서 (a) 캐릭터 위 우클릭이 아래 앱에 도달하지 않는지, (b) 부채꼴 버튼 클릭이 새지 않는지, (c) 150% 배율에서 궤도/히트반경이 맞는지 — 세 항목을 사유에 나열 |

## 부록 B. 이 라운드가 실행하지 않은 것 (정직하게)

- **크로스컴파일을 돌리지 않았다.** 프로덕션 `.cs`를 한 줄도 고치지 않았고, 지금 트리는 다른 라운드가 `Platform/Windows/WindowsSystemTrayIcon.cs`(09-05 08:58) · `WindowsVirtualDesktopProbe.cs`(09:03) 등을 **작업 중**이다. 지금 컴파일을 재면 **내 변경이 아니라 남의 진행 중 상태**를 재게 되므로 의미가 없다. **구현 라운드는 반드시 격리 미러 + `xcheck_isolated.sh osx|win` 양쪽 0에러 + 타깃 교차 대조를 붙여야 한다.**
- **StickMate 앱을 띄우지 않았다.** 우리 빌드의 실기 확인은 여전히 0건이다.
- **`Tasklist.md`를 수정하지 않았다.** 등재 문장은 완료 보고에 있다.

### ★ 2026-09-05 후속 라운드에서 **실제로 실행한 것**(위 목록의 예외 — 정직하게 적는다)
- **읽기 전용 CoreGraphics 프로브 2개를 컴파일해 돌렸다**(§9). 스크래치패드 안에서만 살고,
  저장소에 커밋하지 않았으며, **입력을 한 건도 주입하지 않았다**(`CGEventPost` 계열 0줄).
  프로덕션이 이미 매 프레임 부르고 있는 조회와 **같은 함수**만 호출한다.
- **시스템 설정을 `defaults read`로 읽었다.** ★ **쓴 적 없다** — `defaults write`는 0회다.
  원칙 3(유저 자산 불변)상 사용자의 마우스/트랙패드 설정은 **읽기만** 한다.
  그래서 「보조 클릭을 **끈** 상태」는 **직접 만들어 재지 못했고**, §9-4는 그 설정을 끄고 잰 것이 아니라
  **매핑이 일어나는 층을 실측으로 확정한 뒤 추론한 것**이다. 이 구분을 흐리지 마라.
- 총 관측 115초(실행 2회, 57,500표본). **사용자가 그 사이 실제로 8번 우클릭했다** — 그 8건이 이 절의 근거다.
