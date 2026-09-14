# 화면 변경(모니터 분리·디스플레이 토폴로지 변경) 경로 파일 목록 — 정본

작성: dev-platform / 2026-09-14 · 리더 채택 규칙 **N-8**(product-strategy 제안)의 입력.

> **N-8**: CW-7(모니터 분리 시 PC 전체 정지) 증거는 **출시 후보와 이 목록의 diff가 0인 빌드**에서 나온 E-3만 인정한다.
> 이 문서는 그 「목록」의 정본이다. 목록을 바꾸는 라운드는 ②의 썩음 검사를 같이 돌리고 결과를 보고한다.

**읽기 전에 — 이 목록이 말하는 것과 말하지 않는 것.** 이 목록은 「화면 변경에 **반응해 동작이 달라지는** 코드와 그 입력」이다.
정지 순간에 **돌고 있던 모든 코드**가 아니다(매 프레임 도는 캐릭터·렌더·트레이 코드도 그 순간 돈다). CW-7 원인은 미확정이고
(커널·Intel 드라이버 수준 추정, 우리 앱 기여 유력·미확인), 그래서 이 목록은 **「같은 반응 경로를 시험했는가」의 대리 지표**일 뿐이다 — ④ 한계.
더 엄격한 대안(런타임 어셈블리 `.cs` 전체 diff 0)은 리더 판단 사항이다.

---

## ① 목록

### 경계 규칙

**포함**(파일 단위 — 한 파일의 일부 함수만 경로여도 파일 전체를 넣는다. 같은 파일의 다른 기능 변경도 diff로 잡힌다):
1. **진입 신호를 받는 파일** — UniWindowController `OnMonitorChanged` 구독, 우리 토폴로지 감시기 관측.
2. **유예·재적합·해제 중 동기로 불리는 함수가 있는 파일** — 렌더 간격, `Screen.SetResolution`·창 리사이즈·이동, OS 모니터 열거, 목표 모니터 선택.
3. **유예 상태를 스스로 읽어 동작이 갈리는 파일**(소비자) — `DisplayChangeHoldStatus`·`CharacterPreservationFreeze`·`IsPreservationFrozen`을 코드에서 읽는 곳.
4. **경로 위에서 디스크·스레드 기록을 쓰는 계측**.
5. **그 코드가 읽는 입력** — 설정 에셋, 패키지 고정(UniWinC 커밋 = 네이티브 DLL 정체), 엔진 버전, 스왑체인·그래픽 설정.
6. **(E, 보수 포함)** 같은 `Update` 틱에서 **같은 오버레이 창의 상태(투명·크기·Z순서·Space)를 쓰는** 인접 코드 — 화면 변경 신호로 무장된다는 근거는 없지만(미확인),
   재적합과 같은 틱에 같은 창을 만지므로 증거 동일성에 넣는다. 가지치기는 리더 판단.

**제외**:
- 유예 상태를 읽지 않고 **잠금·서비스를 호출만 하는** 파일(예: `SpectacleEventLock.TryAcquire`를 부르는 연출 감독 14개 — 분기는 잠금 안에 있다).
- 매 프레임 발판·모니터 사각형을 **읽기만** 하는 일반 소비자(`FootholdPoller` 등).
- 테스트(`/Tests/`), 문서, 에디터 전용 코드.
- `Library/PackageCache/` 실물 — git에 없다. 대신 **`Packages/packages-lock.json`의 커밋 해시**가 대리한다(해시가 같으면 같은 소스·같은 네이티브).

### 목록 (기계 판독 — 이 블록의 형식을 바꾸지 마라: `등급 플랫폼 경로`, 공백 구분, `X` = 알려진 제외)

<!-- DCP-LIST BEGIN -->
```text
A both    Assets/_Project/Scripts/Platform/DisplayChangeHoldDriver.cs
A both    Assets/_Project/Scripts/Platform/DisplayChangeRenderHold.cs
A both    Assets/_Project/Scripts/Platform/DisplayTopologyWatcher.cs
A both    Assets/_Project/Scripts/Platform/FramePacing.cs
A both    Assets/_Project/Scripts/Platform/OverlayBoundsFitPolicy.cs
A both    Assets/_Project/Scripts/Platform/OverlayGeometryOscillationGuard.cs
A both    Assets/_Project/Scripts/Platform/WallClockIntervalGate.cs
A both    Assets/_Project/Scripts/Platform/OverlayMonitorDirectory.cs
A both    Assets/_Project/Scripts/Platform/OverlayMonitorChoicePolicy.cs
A both    Assets/_Project/Scripts/Platform/ScreenCoordinateConverter.cs
A win     Assets/_Project/Scripts/Platform/Windows/WindowsOverlayStateEnforcer.cs
A win     Assets/_Project/Scripts/Platform/Windows/Win32WindowService.cs
A mac     Assets/_Project/Scripts/Platform/MacOS/MacOverlayStateEnforcer.cs
A mac     Assets/_Project/Scripts/Platform/MacOS/MacWindowService.cs
A both    Packages/manifest.json
A both    Packages/packages-lock.json
B both    Assets/_Project/Scripts/Core/CharacterPreservationFreeze.cs
B both    Assets/_Project/Scripts/Core/StickmanAgent.cs
B both    Assets/_Project/Scripts/Core/SpectacleEventLock.cs
B both    Assets/_Project/Scripts/Dialogue/DialogueBubbleRenderer.cs
B both    Assets/_Project/Scripts/Interaction/DragThrowController.cs
B both    Assets/_Project/Scripts/Interaction/DanceEpisodeDirector.cs
B both    Assets/_Project/Scripts/Interaction/RunawayDirector.cs
B both    Assets/_Project/Scripts/Interaction/WindowTheftDirector.cs
C both    Assets/_Project/Scripts/Platform/FreezeForensicsLog.cs
C both    Assets/_Project/Scripts/Platform/FreezeForensicsPolicy.cs
C both    Assets/_Project/Scripts/Platform/FreezeWatchdog.cs
C both    Assets/_Project/Scripts/Platform/StallAttribution.cs
C both    Assets/_Project/Scripts/Platform/StallAttributionProbe.cs
C both    Assets/_Project/Scripts/Platform/MonitorTopologyReport.cs
D both    ProjectSettings/ProjectSettings.asset
D both    ProjectSettings/QualitySettings.asset
D both    ProjectSettings/GraphicsSettings.asset
D both    ProjectSettings/ProjectVersion.txt
D both    Assets/_Project/Data/DefaultStickConfig.asset
D both    Assets/_Project/Scripts/Core/StickConfig.cs
D both    Assets/_Project/Scripts/StickMate.Runtime.asmdef
E win     Assets/_Project/Scripts/Platform/OverlayStateReapplyPolicy.cs
E win     Assets/_Project/Scripts/Platform/Windows/UniWinCNativeHandle.cs
E win     Assets/_Project/Scripts/Platform/Windows/WindowsTopmostWatchdog.cs
E mac     Assets/_Project/Scripts/Platform/MacOS/MacSpaceBehaviorNative.cs
X both    Assets/_Project/Scripts/Platform/FootholdPoller.cs
X both    Assets/_Project/Scripts/Platform/AppShutdownSequence.cs
X both    Assets/_Project/Scripts/Platform/SessionExitMarker.cs
X win     Assets/_Project/Scripts/Platform/Windows/WindowsSystemTrayIcon.cs
X win     Assets/_Project/Scripts/Platform/Windows/WindowsCompositionProbe.cs
```
<!-- DCP-LIST END -->

(`.meta`는 따로 적지 않는다 — ②의 명령이 `경로.meta`를 같은 경로로 접어서 센다. 네이티브 플러그인의 가져오기 설정은 패키지 안에 있어 잠금 해시가 대리한다.)

**개수**: 목록 41(A 16 · B 8 · C 6 · D 7 · E 4) + 알려진 제외 X 5.
**플랫폼별**: Windows 빌드에 걸리는 것 = `both` + `win` = **38** / macOS 빌드 = `both` + `mac` = **36**(`both` 33 = A 12 · B 8 · C 6 · D 7, 플랫폼 전용 Windows 5 = A 2 · E 3 / macOS 3 = A 2 · E 1).
※ 첫 작성본은 37/35로 틀리게 적었다 — 손으로 더했고, 블록에서 기계로 다시 세어 고쳤다(`awk`로 등급·플랫폼 열 집계).

### 호출 그래프 근거 (진입점 → 파일, `파일:줄`은 2026-09-14 작업 트리 기준)

줄 번호는 편집으로 옮겨진다 — 판정은 줄이 아니라 ②의 명령과 썩음 검사로 한다. 아래는 「왜 이 파일이 목록에 있는가」의 기록이다.

**0. 소유·생성(두 플랫폼)**
- `Core/StickmanAgent.cs:669` `_platformService = CreatePlatformService()` → `:1970` `CreatePlatformService` — `:1972` `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` → `:1987` `new Win32WindowService()` / `:1988` `#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR` → `:2013` `new MacWindowService()`.
- Windows: `Platform/Windows/Win32WindowService.cs:1384` `CreateOverlayWindow()` → `:1406` `WindowsOverlayStateEnforcer.EnsureExists(controller)` → `:1416` `_enforcer.OsMonitorEnumerator = TryEnumerateOsMonitors`.
- macOS: `Platform/MacOS/MacWindowService.cs:1229` `CreateOverlayWindow()` → `:1253` `MacOverlayStateEnforcer.EnsureExists(controller)` → `:1264` `_enforcer.OsMonitorEnumerator = TryEnumerateOsMonitors`.

**1. 진입 신호 (두 갈래)**
- (가) 라이브러리 신호 — UniWindowController(패키지 `com.kirurobo.uniwinc` `0.9.8`, 잠금 해시 `304f9ba2aa4a8fae7f3c71f38118c44722a2f6cc`):
  네이티브 `LibUniWinC`가 모니터 변경 콜백을 부른다(`Runtime/Scripts/LowLevel/UniWinCore.cs:486` `RegisterMonitorChangedCallback(_monitorChangedCallback)` → `:341` 콜백이 플래그를 세움) →
  `Runtime/Scripts/UniWindowController.cs` `UpdateEvents`의 `:562` `ObserveMonitorChanged()` → `:564` `OnMonitorChanged?.Invoke()`.
  ★ 창을 붙잡는 순간에도 같은 이벤트를 쏜다: `:887` `OnMonitorChanged?.Invoke()`(“ウィンドウ取得時にはモニタ変更と同等の処理”).
  네이티브 실물: Windows `Runtime/Plugins/Windows/x64/LibUniWinC.dll`(sha256 앞 16자 `a0bb5193e9c24693`), macOS `Runtime/Plugins/MacOS/LibUniWinC.bundle/Contents/MacOS/LibUniWinC`(`c081a53f78cdd686`) — 이 PC `Library/PackageCache/com.kirurobo.uniwinc@304f9ba2aa4a` 실측.
  구독: Windows `WindowsOverlayStateEnforcer.cs:233` `Update` → `:251` `TickDisplayChangeHold()` → `:982` → `:991` `_controller.OnMonitorChanged += _onLibraryMonitorChanged` → `:997` `OnLibraryMonitorChanged` → `DisplayChangeHoldDriver.cs:68` `OnLibraryMonitorChanged` → `DisplayChangeRenderHold.cs:194`.
  macOS: `MacOverlayStateEnforcer.cs:234` `Update` → `:248` → `:891` → `:900` 구독 → `:906` → 같은 구동기.
- (나) 우리 토폴로지 감시 — Windows `WindowsOverlayStateEnforcer.cs:315` `TickDisplayTopology()` → `:789` → `:842` `SampleTopology()`(`:849` `DisplayTopologySignature.Create`, `:851` `ScreenCoordinateConverter.AutoUiDensityScale`) →
  `:817` `_topologyWatcher.Observe`(`DisplayTopologyWatcher.cs:105`) → `:818` `FreezeForensics.ObserveTopologyTransition`(`FreezeForensicsLog.cs:354` → `:366` `FreezeWatchdog.MarkEpisodeStart()` → `FreezeWatchdog.cs:77`) →
  `:821` `DisplayChangeHold.OnTopologyTransition(FreezeForensicsPolicy.ClassifyTopologyTransition(...))`(`FreezeForensicsPolicy.cs:180`) → `DisplayChangeHoldDriver.cs:70` → `DisplayChangeRenderHold.cs:213`.
  macOS: `MacOverlayStateEnforcer.cs:306` → `:652` → `:711`(`:724` `Create`, `:725` 밀도) → `:679` `Observe` → `:680` 원장 → `:683` 구동기.

**2. 유예 시작 → 렌더 간격**
- `DisplayChangeHoldDriver.cs:103` `OnStarted` → `:111` `_hooks.SetRenderHold(true)` = 훅 주입 Windows `WindowsOverlayStateEnforcer.cs:967` 구동기 생성 · `:973` `SetRenderHold = FramePacing.SetDisplayChangeHold` / macOS `:876` · `:882` →
  `FramePacing.cs:274` `SetDisplayChangeHold` → `:287` `DisplayChangeHoldPolicy.ResolveRenderFrameInterval`(`DisplayChangeRenderHold.cs:81`) → `:290` `OnDemandRendering.renderFrameInterval = effective`(프로덕션에서 렌더 간격을 쓰는 유일한 자리).
- `:113` `DisplayChangeHoldStatus.PublishStarted`(`DisplayChangeRenderHold.cs:329`) — 소비자 게시(아래 4).
- `:119` `FreezeForensics.Record(RenderHoldStarted)` — 계측.
- 매 프레임: `TickDisplayChangeHold` → `:994`(macOS `:903`) `hold.Tick` → `DisplayChangeHoldDriver.cs:74` → `:77` `IsFitPending` 훅(Windows `:1001`, macOS `:910`) → `DisplayChangeRenderHold.cs:250` — 벽시계 상한 15초.

**3. 재적합(조용한 구간 보류 → 허용)**
- Windows `WindowsOverlayStateEnforcer.cs:316` `TickFullScreenBounds()` → `:552` → `:555` `_boundsOscillation.IsOscillating`(`OverlayGeometryOscillationGuard`) → `:558` `DisplayChangeHold.ShouldDeferFit` → `:560` `NotifyFitDeferred`
  → (허용 뒤) `:601` `ScreenCoordinateConverter.ResolveDpiScale(ResolveConfig())`(설정 에셋 입력) → `:651`·`:655` `OverlayBoundsFitPolicy.Within`·`ShouldSetResolution` → `:669` **`Screen.SetResolution(targetPixelW, targetPixelH, FullScreenMode.Windowed)`**(스왑체인·백버퍼 재할당)
  → `:680`·`:683` 리사이즈·이동 판정 → `:739` `_fullScreenFitLatch.Evaluate`(`OverlayBoundsFitPolicy.cs:259` `FullScreenFitLatchSignal` — 유예 무장의 유일한 원천).
  목표 모니터: `:878` `TryGetTargetMonitorRect` → `:1040` `TryResolveChosenMonitorRect` → `:1046` `RefreshOsMonitorList()` · `:1052` `OverlayMonitorDirectory.Resolve()` · `:1054` `OverlayMonitorChoicePolicy.LibraryIndexForOsMonitor`.
  OS 목록 갱신 게이트: `:953` `WallClockIntervalGate`.
- macOS `MacOverlayStateEnforcer.cs:307` → `:404` → `:407`·`:409`·`:411` → `:483` DPI → `:518`·`:524` → `:540` **`Screen.SetResolution`** → `:605` 래치 / 목표 `:783` → `:947` → `:954` · `:960` · `:962` / 게이트 `:836`.

**4. 유예 해제 → 소비자**
- `DisplayChangeHoldDriver.cs:131` `OnReleased` → `:136` `ForceRefreshOsMonitors`(Windows `:1009` `RefreshOsMonitorList` → `:1014` `OsMonitorEnumerator(_osMonitors)` = `Win32WindowService.cs:1643` `TryEnumerateOsMonitors` → `:1653` `EnumDisplayMonitors`(extern `:285`) → `:1018` `OverlayMonitorDirectory.Publish` /
  macOS `:918` → `:923` → `MacWindowService.cs:1173` → `:1180` `CGGetActiveDisplayList` · `:1187` `CGDisplayBounds`) → `:139` `SetRenderHold(false)` → `:140` `DisplayChangeHoldStatus.PublishReleased`.
- 소비자(유예 상태를 코드에서 읽음 — ②의 썩음 검사가 식별자로 다시 뽑는다):
  `Core/CharacterPreservationFreeze.cs:41` `IsDisplayChangeHoldActive => DisplayChangeHoldStatus.IsActive` · `:44` `BlocksNewSpectacle`;
  `Core/StickmanAgent.cs:937` `Update` → `:961` `TickPreservationFreeze()` → `:1728` → `:1730`·`:1731` `_preservationFreeze.Step(CharacterPreservationFreeze.IsDisplayChangeHoldActive, ...)`, 동결 중 조기 반환 `:578`·`:593`·`:605`·`:933`·`:968`;
  `Core/SpectacleEventLock.cs:93` `TryAcquire` → `:100` `CharacterPreservationFreeze.BlocksNewSpectacle`;
  `Dialogue/DialogueBubbleRenderer.cs:933` `IsPreservationFreezeActive()` → `:934`·`:935`;
  `Interaction/DragThrowController.cs:98` `OnMouseDown` → `:117`;
  `Interaction/DanceEpisodeDirector.cs:173` · `Interaction/RunawayDirector.cs:62` · `Interaction/WindowTheftDirector.cs:219` `if (_player.IsPreservationFrozen) return;`.

**5. 계측(경로 위의 쓰기·스레드)**
- `Platform/FreezeWatchdog.cs:231` `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → `:232` `AutoInstall` → `:254` `FreezeForensicsLog.Activate` · `:255` `FreezeWatchdog.Start()`(워치독 스레드) — 사건 창은 위 1(나)의 `MarkEpisodeStart`가 연다.
- `Platform/StallAttributionProbe.cs:32`·`:33` `AutoInstall` → `:154` `FreezeWatchdog.PublishMainFrame`(워치독 하트비트) / `Platform/StallAttribution.cs` — 두 Enforcer `Update` 첫 줄(Windows `:235`, macOS `:236`) `StallAttribution.Section(PlatformEnforcer)`, 워치독 줄 `FreezeWatchdog.cs:174` `ProbeOpenSectionForWatchdog`.
- `Platform/MonitorTopologyReport.cs:330` `EmitOnce` ← Windows `:297` / macOS `:288`(부착 뒤 한 번, 같은 OS 열거기 `:1200`·`:759` 사용).

**6. 입력(D)**
- `ProjectSettings/ProjectSettings.asset`: `useFlipModelSwapchain: 0`(`:98`) · `fullscreenMode: 1`(`:111`) · `resizableWindow: 0`(`:99`) · `allowFullscreenSwitch: 1`(`:110`) · `runInBackground: 1`(`:86`) · `visibleInBackground: 1`(`:109`) ·
  `macRetinaSupport: 1`(`:85`) · `preserveFramebufferAlpha: 0`(`:68`) · `m_MTRendering: 1`(`:54`) · `m_BuildTargetGraphicsAPIs`(`:398`) · `enableFrameTimingStats: 1`(`:155`). 파일 전체를 목록에 넣는다(한 키만 골라 볼 수단이 git에 없다).
- `ProjectSettings/QualitySettings.asset`: `m_CurrentQuality: 5`(`:7`, `Ultra` `vSyncCount: 1` `:292`).
- `ProjectSettings/ProjectVersion.txt`: `6000.0.82f1 (2fb0dae735e1)`.
- `Assets/_Project/Data/DefaultStickConfig.asset` + `Assets/_Project/Scripts/Core/StickConfig.cs` — `FramePacing.ApplyOnce(ResolveConfig())`(Windows `:244`, macOS `:241`)와 `ScreenCoordinateConverter.ResolveDpiScale(ResolveConfig())`의 입력.
- `Assets/_Project/Scripts/StickMate.Runtime.asmdef` — 위 코드 전부가 들어가는 어셈블리(정의·플랫폼 제약).

**7. E(보수 포함 — 같은 틱·같은 창)**
- Windows: `WindowsOverlayStateEnforcer.cs:418` `UniWinCNativeHandle.TryGetNative()` · `:423` `OverlayStateReapplyPolicy.DecideTransparencyReapply` · `:430` `UniWinCNativeHandle.TrySetTransparent` · `:448` `CausesWindowResize`(투명 재대입이 창 크기를 바꾼다) / `:317` `TickTopmostWatchdog()` → `:501` → `:527` `ReassertTopmost`(`WindowsTopmostWatchdog`, 필드 `:158`).
- macOS: `MacOverlayStateEnforcer.cs:279` `MacSpaceBehaviorNative.ApplyAccessoryActivationPolicyOnce()` · `:353`·`:396` `EnsureAllSpacesBehavior`.

**X(알려진 제외 — 썩음 검사가 「새로 편입」으로 오판하지 않게 적는다)**
- `FootholdPoller.cs` — macOS 진단 `TickFootholdReport`(`MacOverlayStateEnforcer.cs:1034`)가 캐시를 읽기만 한다.
- `AppShutdownSequence.cs`·`SessionExitMarker.cs` — `FreezeWatchdog.cs`가 기동·종료 경로에서 참조(`:257` 종료 훅 설치 등). 화면 변경에서 실행되지 않는다.
- `WindowsSystemTrayIcon.cs` — Enforcer `Update`에서 `Tick`되지만 모니터·창 크기와 무관한 트레이 호스트 창.
- `WindowsCompositionProbe.cs` — 합성 상태 **조회**(`:184`·`:1217` `EnsureExists`).

---

## ② 검사 방법 (저장소 루트에서 복붙)

### (가) 두 커밋 사이 교차 — 판정 명령

```bash
# 사용법: BASE와 TARGET을 고친다. TARGET을 비우면 "BASE..작업 트리(추적 변경 + 추적 안 된 새 파일)"를 본다.
BASE=a6b3101
TARGET=7900ad0
DOC=docs/verify/DISPLAY_CHANGE_PATH_FILES.md
LIST=$(mktemp); CHANGED=$(mktemp)
awk '/<!-- DCP-LIST BEGIN -->/{f=1;next} /<!-- DCP-LIST END -->/{f=0} f && $1 ~ /^[ABCDE]$/ && NF>=3 {print $3}' "$DOC" | sort -u > "$LIST"
N=$(wc -l < "$LIST" | tr -d ' ')
[ "$N" = 41 ] || { echo "★ 목록 추출 실패(개수 $N, 기대 41) — 판정 무효"; }
if [ -n "$TARGET" ]; then git diff --name-only "$BASE" "$TARGET"; else { git diff --name-only "$BASE"; git ls-files --others --exclude-standard; }; fi \
  | sed 's/\.meta$//' | sort -u > "$CHANGED"
echo "목록 $N · 변경 파일 $(wc -l < "$CHANGED" | tr -d ' ') · 범위 ${BASE}..${TARGET:-작업트리}"
HIT=$(comm -12 "$LIST" "$CHANGED")
if [ -z "$HIT" ]; then echo "교차 0 — N-8 동일 경로"; else echo "교차 $(printf '%s\n' "$HIT" | wc -l | tr -d ' ')건 — N-8 다른 경로:"; printf '%s\n' "$HIT" | sed 's/^/  /'; fi
rm -f "$LIST" "$CHANGED"
```

**교정(명령이 살아 있는가 — 매번 같이 돌린다)**: `BASE=a6b3101 TARGET=3cc6753`은 **반드시 교차가 있어야 한다**(`3cc6753`이 `DisplayChangeRenderHold.cs`를 바꿨다).
그 범위에서 교차 0이 나오면 명령이 죽은 것이다(경로 오타·목록 추출 실패) — 그 뒤 모든 「교차 0」을 폐기한다. 목록 개수 41도 같은 이유로 찍는다.

**E-3 빌드 식별 주의**: 교차는 **커밋** 사이의 비교다. E-3 zip이 어느 커밋에서, **깨끗한 트리**에서 빌드됐는지는 이 명령이 모른다 —
빌드 차수 판독은 `docs/verify/WINDOWS_CHECK_SESSION.md` §R(dll 안 이름으로 차수 판독)을 따른다. 미커밋 변경을 얹은 빌드의 E-3은 N-8 판정 대상이 아니다.

### (나) 목록 갱신 규칙

1. **경로에 새 파일이 편입되면 그 라운드가 이 목록을 고친다** — 예: Enforcer 재적합 함수가 새 정책 파일을 부르기 시작함, 새 소비자가 `IsPreservationFrozen`을 읽음,
   새 진입 신호(예: `WM_DISPLAYCHANGE` 직접 구독) 추가, 패키지 교체.
2. 고치는 라운드는 아래 (다)를 돌려 `ROT-CHECK OK`를 보고에 붙인다.
3. **목록을 바꾼 커밋 자체는 N-8 기준을 바꾼다** — 옛 E-3을 새 목록으로 재판정할 때는 **새 목록**을 쓴다(목록이 넓어졌으면 옛 증거가 떨어질 수 있다).

### (다) 썩음 검사 — 목록이 코드와 갈라졌는가

```bash
python3 - <<'PY'
import os, re, sys, json, collections
DOC = 'docs/verify/DISPLAY_CHANGE_PATH_FILES.md'
ROOT = 'Assets/_Project/Scripts'
LOCK_HASH = '304f9ba2aa4a8fae7f3c71f38118c44722a2f6cc'
UNITY = '6000.0.82f1'
text = open(DOC, encoding='utf-8').read()
block = text.split('<!-- DCP-LIST BEGIN -->')[1].split('<!-- DCP-LIST END -->')[0]
rows = [l.split() for l in block.splitlines() if l.split() and l.split()[0] in list('ABCDEX')]
listed = {r[2] for r in rows if r[0] != 'X'}; known = {r[2] for r in rows}
issues = []

def strip(s):
    s = re.sub(r'/\*.*?\*/', ' ', s, flags=re.S); s = re.sub(r'//[^\n]*', ' ', s)
    s = re.sub(r'@"(?:[^"]|"")*"', '""', s); return re.sub(r'"(?:\\.|[^"\\])*"', '""', s)
files = [os.path.join(d, f) for d, _, fs in os.walk(ROOT) for f in fs if f.endswith('.cs') and '/Tests/' not in os.path.join(d, f)]
src = {f: strip(open(f, encoding='utf-8', errors='replace').read()) for f in files}

# 1) 목록 파일 실재
for p in sorted(listed):
    if not os.path.exists(p): issues.append('GONE-FILE ' + p)

# 2) 소비자 — 유예·보존 동결 식별자를 코드에서 읽는 파일은 전부 목록(또는 X)에 있어야 한다
CONSUMER = re.compile(r'\b(DisplayChangeHold\w*|DisplayChangeRenderHold|CharacterPreservationFreeze|IsPreservationFrozen|BlocksNewSpectacle|IsDisplayChangeHoldActive|FullScreenFitLatchSignal|TopologyForensicsTransition|DisplayTopologyWatcher|OnMonitorChanged)\b')
def consumers(srcmap):
    return {f for f, s in srcmap.items() if CONSUMER.search(s)}
for f in sorted(consumers(src)):
    if f not in known: issues.append('NEW-CONSUMER ' + f)

# 3) 재적합·유예 함수 본문이 부르는 우리 타입 → 정의 파일이 목록(또는 X)에 있어야 한다
defs = collections.defaultdict(set)
for f, s in src.items():
    for m in re.finditer(r'\b(?:class|struct|interface|enum)\s+([A-Z]\w*)', s): defs[m.group(1)].add(f)
METHODS = ['TickDisplayChangeHold', 'OnLibraryMonitorChanged', 'TickDisplayTopology', 'SampleTopology', 'TickFullScreenBounds', 'TryGetTargetMonitorRect',
           'TryResolveChosenMonitorRect', 'RefreshOsMonitorList', 'IsFullScreenFitPending', 'ReArmFullScreenFitForNewTarget']
def body(s, name):
    m = re.search(r'\b' + name + r'\s*\([^;{]*\)\s*(=>|\{)', s)
    if not m: return None
    if m.group(1) == '=>': return s[m.end():s.find(';', m.end())]
    i, depth = m.end(), 1
    while i < len(s) and depth:
        depth += {'{': 1, '}': -1}.get(s[i], 0); i += 1
    return s[m.end():i]
for enf in ['Assets/_Project/Scripts/Platform/Windows/WindowsOverlayStateEnforcer.cs', 'Assets/_Project/Scripts/Platform/MacOS/MacOverlayStateEnforcer.cs']:
    for name in METHODS:
        b = body(src[enf], name)
        if b is None: issues.append(f'GONE-METHOD {os.path.basename(enf)}:{name}'); continue
        for t in set(re.findall(r'\b([A-Z]\w*)\s*\.', b)) | set(re.findall(r'\bnew\s+([A-Z]\w*)', b)):
            for df in defs.get(t, ()):
                if df not in known: issues.append(f'NEW-CALLEE {os.path.basename(enf)}:{name} -> {t} ({df})')

# 4) 진입점 대조(개수가 바뀌면 그래프가 바뀐 것 — 사람이 다시 본다)
def count(path, pat): return len(re.findall(pat, src.get(path, '')))
W, M = 'Assets/_Project/Scripts/Platform/Windows/', 'Assets/_Project/Scripts/Platform/MacOS/'
expect = {
    (W + 'WindowsOverlayStateEnforcer.cs', r'OnMonitorChanged\s*\+='): 1, (M + 'MacOverlayStateEnforcer.cs', r'OnMonitorChanged\s*\+='): 1,
    (W + 'WindowsOverlayStateEnforcer.cs', r'new\s+DisplayTopologyWatcher\b'): 1, (M + 'MacOverlayStateEnforcer.cs', r'new\s+DisplayTopologyWatcher\b'): 1,
    (W + 'Win32WindowService.cs', r'OsMonitorEnumerator\s*=\s*TryEnumerateOsMonitors'): 1, (M + 'MacWindowService.cs', r'OsMonitorEnumerator\s*=\s*TryEnumerateOsMonitors'): 1,
    (W + 'WindowsOverlayStateEnforcer.cs', r'Screen\s*\.\s*SetResolution\s*\('): 1, (M + 'MacOverlayStateEnforcer.cs', r'Screen\s*\.\s*SetResolution\s*\('): 1,
    ('Assets/_Project/Scripts/Platform/FramePacing.cs', r'OnDemandRendering\s*\.\s*renderFrameInterval\s*='): 1,
}
for (p, pat), n in expect.items():
    got = count(p, pat)
    if got != n: issues.append(f'ENTRY-COUNT {os.path.basename(p)} /{pat}/ = {got} (기록 {n})')
others = [f for f in files if re.search(r'Screen\s*\.\s*SetResolution\s*\(|OnDemandRendering\s*\.\s*renderFrameInterval\s*=|WM_DISPLAYCHANGE|CGDisplayRegisterReconfigurationCallback', src[f]) and f not in known]
for f in others: issues.append('NEW-ENTRY ' + f)

# 5) 패키지·엔진 고정
lock = json.load(open('Packages/packages-lock.json'))['dependencies'].get('com.kirurobo.uniwinc', {})
if lock.get('hash') != LOCK_HASH: issues.append(f"UNIWINC-HASH {lock.get('hash')} (기록 {LOCK_HASH})")
if UNITY not in open('ProjectSettings/ProjectVersion.txt').read(): issues.append('UNITY-VERSION changed')

# 6) 교정 — 검사기가 살아 있는가(합성 입력으로 새 소비자를 꼭 잡아야 한다)
fake = dict(src); fake['Assets/_Project/Scripts/Zz/FakeConsumer.cs'] = 'class Z { void F(){ if (x.IsPreservationFrozen) return; } }'
calib_ok = 'Assets/_Project/Scripts/Zz/FakeConsumer.cs' in consumers(fake) and len(rows) >= 46 and len(files) > 100
print(f'목록 {len(listed)} · 알려진 제외 {len(known) - len(listed)} · 스캔 {len(files)}파일 · 교정={"통과" if calib_ok else "★실패"}')
for i in issues: print('  ' + i)
print('ROT-CHECK OK' if calib_ok and not issues else 'ROT-CHECK FAIL — 목록 갱신 또는 경계 판단 필요')
sys.exit(0 if calib_ok and not issues else 1)
PY
```

판독: `NEW-CONSUMER`·`NEW-CALLEE`·`NEW-ENTRY` = 경로에 새로 편입된 후보(목록에 넣거나 X로 사유와 함께 적는다) / `GONE-*` = 목록이 없어진 것을 가리킨다 /
`ENTRY-COUNT` = 진입점 모양이 바뀌었다(구독이 늘거나 사라짐 — 사람이 그래프를 다시 본다) / `UNIWINC-HASH`·`UNITY-VERSION` = 네이티브·엔진이 바뀌었다(**D·A 등급 변경 — 그 자체로 N-8 다른 경로**).
`교정=★실패`는 검사기 자체가 죽었다는 뜻이다(합성 소비자를 못 잡음, 목록 행이 46 미만으로 줄어듦, 스캔 파일 100 이하) — 그때는 `ROT-CHECK OK`가 나와도 믿지 않는다.
목록을 줄이는 라운드는 행 수 하한(46)도 같이 고친다.

---

## ③ 오늘 적용 (2026-09-14)

(아래는 ②의 두 코드 블록을 이 문서에서 **그대로 뽑아**(`BASE=`·`TARGET=` 두 줄만 바꿔) 실행한 출력이다. 작업 트리 = 2026-09-14 저녁, 5-c 적용·coder 온보딩 미커밋·다른 라운드 문서 미커밋 상태.)

**교정 — 먼저**
- (가) `a6b3101..3cc6753`: 목록 41 · 변경 파일 30 · **교차 8** → 0이 아니므로 명령이 살아 있다.
- (다) 썩음 검사 검출기 양성 대조 — 이 문서의 **사본**에서 한 줄씩 뺐다(원본 무변경):
  `OverlayMonitorDirectory.cs`(A)를 빼면 `NEW-CALLEE` 4줄(두 Enforcer × `TryResolveChosenMonitorRect`·`RefreshOsMonitorList`), `RunawayDirector.cs`(B)를 빼면 `NEW-CONSUMER` 1줄 — 둘 다 종료코드 1.
  (그 두 실행의 `교정=★실패`는 행 수 하한 46에 걸린 것 — 한 줄 뺀 사본이라 정상.)

**교차 결과**

| 범위 | 목록 | 변경 파일(.meta 접음) | 교차 | N-8 |
|---|---:|---:|---:|---|
| `a6b3101..7900ad0` (사용자 시험 빌드 → HEAD) | 41 | 42 | **8** | 다른 경로 |
| `3cc6753..7900ad0` (4차·5차만) | 41 | 29 | 1 | 다른 경로 |
| `7900ad0..작업 트리` (5-b·5-c·coder 온보딩·다른 라운드 문서) | 41 | 36 | **0** | 동일 경로 |
| `a6b3101..작업 트리` | 41 | 64 | 8 | 다른 경로 |

- `a6b3101..7900ad0` 교차 8건: `Core/CharacterPreservationFreeze.cs` · `Core/SpectacleEventLock.cs` · `Platform/DisplayChangeHoldDriver.cs` · `Platform/DisplayChangeRenderHold.cs` · `Platform/FreezeWatchdog.cs` ·
  `Platform/MacOS/MacOverlayStateEnforcer.cs` · `Platform/OverlayBoundsFitPolicy.cs` · `Platform/Windows/WindowsOverlayStateEnforcer.cs`.
  **Windows 빌드 기준 7건**(`MacOverlayStateEnforcer.cs`는 macOS 전용) — 리더 메시지의 「7파일」과 Windows 빌드 기준으로 일치한다.
- 커밋별(각 커밋이 바꾼 파일 ∩ 목록): `3cc6753` **8** / `17f6f38` **1**(`FreezeWatchdog.cs`) / `7900ad0` **0**.
- `7900ad0..작업 트리` 교차 0의 내역: 5-b·5-c 코드(`AppShutdownSequence.cs`·`SessionExitMarker.cs` = X 알려진 제외, `ReservedBarRevealDirector.cs` = 목록 밖), 테스트 파일, coder 온보딩
  (`Interaction/GearRadialMenuWidget.cs`·`GearMenuOnboarding*` 4파일·PlayMode 테스트 = 목록 밖), 다른 라운드의 문서·도구·에이전트 정의 = 목록 밖. **실측으로 겹치지 않는다.**
  ★ 작업 트리는 커밋이 아니다 — 출시 후보 커밋이 정해지면 그 커밋으로 다시 잰다.

**N-8 오늘의 판정(사실만)**: 사용자가 대기 중인 E-3 시험 zip은 `a6b3101`(2차) 빌드다(`WINDOWS_CHECK_SESSION.md` §R 판독). HEAD(`7900ad0`) 계열 출시 후보와는 목록 교차가 **8**이므로,
그 zip의 E-3 결과는 N-8상 **HEAD 계열 출시 후보의 CW-7 증거로 인정되지 않는다**. `7900ad0` 이후 작업 트리 변경은 목록과 겹치지 않으므로, `7900ad0` 또는 그 뒤 목록 무변경 커밋으로 새로 만든 빌드의 E-3은 인정 후보다.

**썩음 검사(원본 문서)**: `목록 41 · 알려진 제외 5 · 스캔 284파일 · 교정=통과` → `ROT-CHECK OK`(종료코드 0).

---

## ④ 한계 — 텍스트·호출 그래프로 못 보는 것

1. **리플렉션·문자열 조립 호출** — `GetMethod("Tick" + ...)`, `SendMessage` 계열. 식별자 검색이 원리상 못 본다(이 저장소 텍스트 감사들의 공통 한계와 같다).
2. **Unity 메시지 함수** — `Update`/`LateUpdate`/`OnApplicationFocus`/`OnRectTransformDimensionsChange`/`OnGUI`는 엔진이 부른다. 화면 크기가 바뀌면 UI 캔버스·카메라가 스스로 반응하는데,
   그 반응 코드(uGUI 캔버스 스케일러, 카메라 종횡비)는 엔진·패키지 안에 있어 **엔진 버전(D)으로만** 대리된다. 우리 코드 중 `Screen.width/height`를 매 프레임 읽는 곳도 목록에 없다(규칙 제외 — 읽기).
3. **네이티브 콜백** — `LibUniWinC`가 OS에서 받는 모니터 변경 통지와 그 스레드·타이밍, Win32 `EnumDisplayMonitors` 콜백, macOS `CGDisplay*`. 소스가 없거나(DLL) 패키지 안이다 — **잠금 해시**가 정체만 보장한다.
4. **엔진 내부 스왑체인·GPU 드라이버** — `Screen.SetResolution` 뒤의 DXGI/Metal 재할당, D3D11 플립 모델(`useFlipModelSwapchain: 0`), Intel 드라이버. CW-7의 유력 지점이지만 코드 목록 밖이다 — 엔진 버전·`ProjectSettings`로만 대리.
5. **이름 충돌** — 썩음 검사 3)은 짧은 타입 이름(예: 중첩 `enum Phase`)이 겹치면 실제로 부르지 않는 파일을 후보로 낸다(과잉 — 시끄러운 쪽). 2)의 소비자 식별자는 긴 고유 이름이라 충돌이 드물지만 원리상 가능하다.
6. **간접 호출** — 훅 대리자(`Hooks.SetRenderHold = FramePacing.SetDisplayChangeHold`)를 다른 함수로 바꿔 끼우면 3)의 메서드 본문 검사가 못 본다. 4)의 진입점 개수 대조가 일부만 막는다.
7. **파일 단위 과잉** — `StickmanAgent.cs`(2천 줄대)·`Win32WindowService.cs`·`FramePacing.cs`처럼 큰 파일은 경로와 무관한 한 줄 변경도 「다른 경로」로 판정한다. 의도된 보수성이며 대가는 E-3 재시험이다.
8. **빌드 식별** — ②(가)는 커밋 비교다. E-3 zip이 미커밋 변경을 얹은 빌드인지는 판정하지 못한다(§R 차수 판독에 의존).
9. **에디터·테스트 경로** — 이 목록은 플레이어 빌드 경로다. EditMode·PlayMode 테스트는 목록에서 뺐다 — 테스트 변경은 증거 동일성에 영향이 없다고 본다(판단).
