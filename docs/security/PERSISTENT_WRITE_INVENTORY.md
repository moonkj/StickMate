# 앱이 사용자 PC에 남기는 쓰기 — 전수 인벤토리 (2026-09-15)

작성: `security` · 묶음 ⑨ · 문서만 썼다. 프로덕션 `.cs` 수정 0 · 앱·Unity·빌드 실행 0 · 실저장소 `git diff` 호출 0.
측정 기준: HEAD `a8c3723` 위 작업 트리. 작업 트리에서 바뀐 프로덕션 `.cs`는 `Interaction/WindowCrashDirector.cs` 1개이고, 아래 추출기에서 쓰기 니들 적중 0이다.
2판(verify-change 반려 뒤 수정, 2026-09-15): 고친 칸만 HEAD `76504c2` 위 작업 트리에서 다시 쟀다. `a8c3723..76504c2` 사이 프로덕션 `.cs` 변경은 `Interaction/WindowCrashDirector.cs` 1개(+25)이고, `Interaction/WindowCrashDirector.cs`에서 쓰기 니들(`PlayerPrefs`·`File.`·`FileStream`·`Directory.`·`Registry`) 적중은 0줄이다(전문 · 줄. 같은 니들의 양성: `Core/CharacterSaveStore.cs` 30줄). 작업 트리 대 HEAD 프로덕션 `.cs` 차분도 0이다(`diff-index HEAD --numstat` · 같은 출력에서 `.md` 23줄로 명령 생존). 1-2 추출기 계수(284파일 · 적중 110)는 초판 값이다. 2판 스크립트도 scratchpad에만 있다.
3판(verify-change 조건부 통과 뒤, 2026-09-15): 4절 원문 앵커 · 4-1 #1 앵커를 더 긴 고유 앵커로 바꿨고(동시 편집으로 짧은 앵커가 2회가 됨), R-1 줄 번호를 앵커로 바꿨고, 2-6에 안 A 착지 조건 · 5-3에 U-10을 넣었다(8절 정정 9). 관리자 권한 판정의 문서 정본은 `docs/security/SECURITY_MODEL.md` X-2절 앵커 `X-2-A. 관리자 권한(UAC) 판정`이다.
판정 스크립트(`extract.py`·`mdcheck.py`)는 scratchpad에만 있다(저장소 밖).
경로 표기: 코드는 `Assets/_Project/Scripts/` 아래 상대 경로. 사용자 경로는 `%USERPROFILE%`·`%TMP%`·`~` 자리표시자로만 쓴다.
발단: marketing 감사가 `docs/marketing/TRUST_AND_SIGNING.md` 5-2 답글의 「레지스트리에는 쓰지 않고」를 Windows에서 거짓으로 판정했다. 한 줄을 고치는 대신 앱이 무엇을 어디에 쓰는지 전부 확정한다.

---

## 0. 결론

| 분류 | 행 수 | 한 줄 |
|---|---:|---|
| 레지스트리(Windows) | **2** | 같은 키 한 곳 `HKCU\Software\Vibelab\StickMate`. 우리 코드 값 1개(R-1) + Unity 엔진이 스스로 쓰는 값들(R-2) |
| 파일 | **8** | 전부 `Application.persistentDataPath` 아래. 세이브 4종 · 작업표시줄 원복 흔적 1종(Windows) · 진단 기록 3종 |
| 로그(엔진) | **2** | Unity `Player.log`·`Player-prev.log`(L-1), Windows 충돌 보고 폴더(L-2, 충돌 때만) |
| OS 상태 | **2** | 작업표시줄 자동 숨김 비트(O-1, 원칙 3 승인 예외), 트레이 아이콘을 셸이 기억하는 기록(O-2) |
| 자동 실행 등록 | **0** | Run 키·LaunchAgent·로그인 항목·작업 스케줄러 전부 0(양성·합성 대조 포함, 1-4) |
| Steamworks | 휴면 | 해당 없음. `SECURITY_MODEL.md` X-1 판정(H1–H5 전부 참) |

- ★ **「레지스트리에 쓰지 않는다」는 Windows에서 거짓이다.** 우리 코드는 레지스트리 API를 직접 부르지 않는다(참). 그러나 `PlayerPrefs.SetInt`를 부르고, Unity 1차 문서상 Windows에서 PlayerPrefs는 `HKEY_CURRENT_USER\Software\<회사>\<제품>`에 저장된다.
- ★ **그 키에는 우리 값보다 엔진 값이 더 많다.** 개발 머신의 PlayerPrefs plist `com.Vibelab.StickMate.plist`에서 키 이름 18개를 실측했고 우리 것은 1개다. 이 plist는 **플레이어 또는 에디터 Play 모드**의 것이다(2-1 표 아래 — 두 경우 파일 이름이 같다). 나머지 17개는 창 크기·창 위치·전체화면 모드·모니터 선택·그래픽 품질·실행 횟수·설치 식별자·세션 식별자다(`unity_connect.*` 포함. 「실행 횟수」는 키 이름 `unity.player_session_count`를 읽은 추론). 이 17개 이름을 쓰는 우리 코드는 0이라, 우리 `PlayerPrefs` 호출을 지워도 이 이름들은 생길 것으로 판단한다(추론). 단 창 크기·품질 같은 **값**은 우리 호출이 정할 수 있다(R-2). Windows 레지스트리 실측은 0회다(이 머신에 Windows 없음).
- ★ **감사 범위 — 레지스트리 쪽 자동 감사는 둘이고, 남는 공백도 둘이다.**
  - 감사 ①: `UserAssetImmutabilityAuditTests.레지스트리_쓰기_API가_저장소_어디에도_없다`는 `RegSetValue` 같은 **API 이름**을 막는다. `UserAssetImmutabilityAuditTests.cs`에 `PlayerPrefs`는 0회다(전문 · 발생. 같은 계수의 양성: `UserAssetImmutabilityAuditTests.cs`에서 `RegSetValue` 4회).
  - 감사 ②: `GearMenuOnboardingSeenStoreTests.우회금지_프로덕션의_설정_저장소_호출은_실제_저장소의_싱크_안에만_있고_싱크는_계수를_거친_두_함수에서만_불린다`(HEAD `76504c2`에 있다. `GearMenuOnboardingSeenStoreTests.cs`의 마지막 변경 커밋은 `36e0a5e`)는 프로덕션의 `PlayerPrefs`를 `Interaction/PlayerPrefsGearMenuOnboardingSeenStore.cs` **한 파일**, 멤버 {`GetInt`·`SetInt`·`Save`}로 잠근다. 초판의 「PlayerPrefs 위치는 감사 밖」은 거짓이었다(8절 정정 1).
  - 감사 ②의 실행 결과: 커밋 `76504c2` 메시지가 인용한 EditMode 전량 `Logs/coderui-a1/full2_edit.xml`(추적 안 됨 · 수정 시각 2026-09-15 08:57로 커밋 10:04보다 앞)에 이 이름이 정확히 1건이고 `result="Passed"`다. 같은 xml 판정: test-run `Skipped:Ignored` · `failed="0"` · 3168 = `testcasecount` · `site`가 SetUp/TearDown인 스위트 0 · `Failed`로 시작하는 스위트 0(TEAM 「픽스처 끝에서 난 실패」 규칙 1). 이름 탐색 생존: 같은 픽스처 이름으로 9건 적중. `GearMenuOnboardingSeenStoreTests.cs`와 `PlayerPrefsGearMenuOnboardingSeenStore.cs`는 마지막 커밋이 둘 다 `36e0a5e`이고 지금 작업 트리 차분이 0이다. 08:57 시점의 작업 트리 상태 자체는 미확인이다.
  - **남는 공백**: (가) 감사 ②의 목적은 테스트 격리(계수를 거치지 않는 설정 저장소 호출 금지)이지 원칙 3·레지스트리가 아니다 — 새 저장소는 「허용 파일로 등재」하면 통과한다. (나) 감사 ②는 **키 개수를 세지 않는다**(싱크 `SetInt`는 어떤 키든 받는다). 그래서 「레지스트리에 안 씀 — 자동 감사가 지킨다」는 여전히 감사가 지키는 범위보다 넓은 주장이다(인계 ③).
- 원칙 3과의 관계: 파일 8종과 레지스트리 키는 **OS가 이 앱에 배정한 자기 자리**라 사용자 자산이 아니다(원칙 3 대상 아님). OS 상태 O-1만 사용자 설정을 바꾸고, 그것이 승인된 예외 1건이다.

---

## 1. 측정 방법 — 추출기와 대조

### 1-1. 대상

- 프로덕션: `Assets/_Project/Scripts` 아래 `.cs` **284개**(`Tests/` 제외). Windows 전용 파일은 `#if UNITY_STANDALONE_WIN` 안이라 활성 빌드 타깃과 무관하게 **소스 텍스트로** 읽었다. 적중마다 둘러싼 전처리 조건(`#if` 스택)을 기록했다.
- 에디터 전용 `Assets/Editor` 13개는 따로 셌다(1-5). 테스트는 제외했다.
- 네이티브: UniWinC 패키지 C# 소스, Windows `LibUniWinC.dll`(x64·x86·빌드 산출물) 임포트 표, macOS `LibUniWinC.bundle` 미정의 기호, Windows `UnityPlayer.dll`·`UnityCrashHandler64.exe` 임포트 표.

### 1-2. 니들 (고정 문자열, 셸을 거치지 않는 파이썬 인자)

| 계열 | 니들 |
|---|---|
| prefs | `PlayerPrefs.Set` · `PlayerPrefs.Save` · `PlayerPrefs.Delete` · `EditorPrefs.` |
| file | `File.WriteAll` · `File.AppendAll` · `File.Create` · `File.Open` · `File.Copy` · `File.Move` · `File.Replace` · `File.Delete` · `File.SetAttributes` · `File.SetLastWriteTime` · `new FileStream(` · `new StreamWriter(` · `new BinaryWriter(` · `Directory.CreateDirectory` · `Directory.Delete` · `Directory.Move` · `new FileInfo(` · `new DirectoryInfo(` · `.MoveTo(` · `ScreenCapture.` · `EncodeToPNG` · `EncodeToJPG` |
| registry | `Microsoft.Win32` · `Registry.` · `RegistryKey` · `RegSetValue` · `RegCreateKey` · `RegDeleteKey` · `RegDeleteValue` · `RegOpenKey` · `RegQueryValue` · `RegEnumKey` · `SHSetValue` · `SHRegSet` · `WritePrivateProfileString` · `WriteProfileString` |
| osstate | `SHAppBarMessage(` · `ABM_SETSTATE` · `Shell_NotifyIcon(` · `SystemParametersInfo` · `SPI_SET` · `ChangeDisplaySettings` · `CFPreferencesSet` · `NSUserDefaults` · `setObject:forKey:` · `defaults write` · `SetProcessShutdownParameters` · `RegisterApplicationRestart` · `SetCurrentProcessExplicitAppUserModelID` · `SHAddToRecentDocs` · `systemCopyBuffer` · `Environment.SetEnvironmentVariable` |
| autostart | C# 소스 표기 `CurrentVersion` + 이중 역슬래시 + `Run` · 단일 역슬래시 표기 · `LaunchAgents` · `SMAppService` · `SMLoginItemSetEnabled` · `LSSharedFileList` · `schtasks` · `ITaskService` · `loginwindow` · `Startup folder` · `StartupApproved` |
| proc · log · roots | `Process.Start(` · `Application.OpenURL(` / `Application.logMessageReceived` · `ILogHandler` · `logHandler` · `consoleLogPath` / `persistentDataPath` · `temporaryCachePath` · `Application.dataPath` · `streamingAssetsPath` · `GetFolderPath` · `GetTempPath` |

결과: 284파일 · 적중 110(주석 포함). 전부 줄 단위로 열어 분류했다.
- ★ `Registry.` 니들은 `PackRegistry`·`CharacterVisualRegistry` 같은 **우리 타입 이름**에 걸렸다(코드 5줄 · 주석 7줄). 레지스트리 호출이 아니다. 실제 레지스트리 API는 `Platform/Windows/WindowsGameProcessProbe.cs`의 **읽기** 3종뿐이다(`RegOpenKeyExW`·`RegEnumKeyExW`·`RegQueryValueExW`, `KEY_READ`). 쓰기 계열 이름 적중 4건은 전부 「선언조차 하지 않는다」는 주석이다.
- autostart · proc 계열 적중 **0**. prefs 계열 적중은 `PlayerPrefs.Set`·`PlayerPrefs.Save` 각 1(둘 다 `Interaction/PlayerPrefsGearMenuOnboardingSeenStore.cs`), `PlayerPrefs.Delete` 0.

### 1-3. 대조 — 추출기가 살아 있는가

| 종류 | 무엇 | 결과 |
|---|---|---|
| 양성 | `PlayerPrefsGearMenuOnboardingSeenStore.cs`의 `PlayerPrefs.Set` | 98행 1건 |
| 양성 | `CharacterSaveStore.cs`의 `File.Replace` | 1390행 1건 |
| 양성 | `WindowsReservedBarAutoHideControl.cs`의 `ABM_SETSTATE` | 79·130행, 조건 `UNITY_STANDALONE_WIN` 기록됨 |
| 양성 | `WindowsGameProcessProbe.cs`의 `RegOpenKey` | 3건, 조건 `UNITY_STANDALONE_WIN` 기록됨 |
| 음성 | `Tests/` 경로 적중 | 0 |
| 합성 변이 1 | scratchpad 사본에 `#if UNITY_STANDALONE_WIN` 안 `Microsoft.Win32.Registry.SetValue`·`RegCreateKeyExW` + 조건 밖 `File.WriteAllText`, `Tests/` 안에 `PlayerPrefs.SetInt` | 앞 셋 적중(조건 기록 포함), `Tests/` 쪽은 0 — 제외 규칙이 동작 |
| 합성 변이 2 | Run 키 문자열 · `SMAppService` · `PlayerPrefs.DeleteAll` · `SystemParametersInfo(SPI_SET…)` · `Process.Start` · `logHandler` | 7건 전부 적중 — 0건 계열의 니들이 살아 있음 |

### 1-4. 네이티브 표면

- **P/Invoke 전수**: `extern` 선언 이름(중복 제거) **코드 기준 112 / 주석 포함 113**(2판 재계수. 대상은 프로덕션 284파일 전문이고, 기준은 `extern` 토큰이 코드에 있나 주석에 있나로 가른 선언 이름의 중복 제거 수다. 주석에만 있는 이름은 `IsTopmost` 1개로, `Platform/Windows/WindowsOverlayStateEnforcer.cs` 주석이 UniWinC 패키지의 선언을 옮겨 적은 줄이다. 도구는 자체 어휘기이고 합성 입력 자체 시험을 통과했다 — 코드 1 · 주석 2 · 문자열 속 선언과 `extern alias` 제외). 영속 쓰기가 가능한 표면은 `SHAppBarMessage`(O-1)와 `Shell_NotifyIcon`(O-2) 둘뿐이다. 나머지 창 조작(`SetWindowLong*`·`SetLayeredWindowAttributes`·`CreateWindowEx` 등)은 **우리 창**에 대한 프로세스 수명 상태다. macOS `objc_msgSend` 셀렉터 중 쓰기형은 `setActivationPolicy:`·`setCollectionBehavior:`(우리 앱·우리 창)뿐이고, `CFPreferencesCopyAppValue`는 읽기다. `[ComImport]` 5개(가상 데스크톱·작업표시줄 목록·오디오 3)도 런타임 호출이다. 이 「비영속」은 대상이 자기 창·자기 프로세스라는 코드 사실에 근거하며, 1차 문서로 따로 대조하지는 않았다.
- **UniWinC**(`Main.unity`에 컴포넌트 배치됨): 패키지 C# 소스에서 `PlayerPrefs`·`File.`·`Directory.`·`Registry`·`FileStream`·`StreamWriter`·`persistentDataPath` 적중 0(양성: `UniWinCore.cs`의 `DllImport` 44). Windows `LibUniWinC.dll` 임포트 표에 `Reg*` 0이다. 같은 PE 파서의 양성 대조로 `UnityPlayer.dll`에서 `Reg*` 9종이 잡혔다. 파일 API(`CreateFileW`·`WriteFile`·`GetSaveFileNameW`)는 임포트돼 있으나 **임포트는 호출이 아니다**. 우리 코드가 부르는 UniWinC 함수는 `SetTransparent`·`GetWindowHandle` 2개이고 `FilePanel` 호출은 0이다. 네이티브 내부 호출 경로는 미확인이다. macOS 번들 미정의 기호 233개 중 `NSUserDefaults`·`CFPreferencesSet`·`writeToFile`·`NSFileManager`·`_fopen`·`_unlink`·`_rename`은 0이다(양성: `NSWindow…` 기호 적중).
- **Unity 엔진 자체**: Windows `UnityPlayer.dll` 임포트에 `RegSetValueExW`·`RegCreateKeyExW`·`RegDeleteValueA`와 `SHGetKnownFolderPath`·`CreateFileW`·`DeleteFileW`·`MoveFileExW`가 있다. PlayerPrefs 저장소와 로그 교체의 엔진 쪽 손이다. 엔진 내부 경로를 전부 열거하지는 않았다. `Software\` 문자열이 UTF-8 3회 · UTF-16 1회 있어 **PlayerPrefs 키 말고 다른 레지스트리 경로를 엔진이 읽거나 쓰는지는 미확인**이다.
- **자동 실행 — 엔진 쪽 대조**: `UnityPlayer.dll`에서 `CurrentVersion\Run`은 UTF-8·UTF-16 모두 0이다(양성: 같은 호출에서 `HKEY_CURRENT_USER` UTF-16 1). macOS `UnityPlayer.dylib`에서 `LaunchAgents`·`SMAppService`는 0이다(양성: `Player-prev.log` 2). 로드맵 원문도 「자동 실행이 1.0 미구현」이다(`docs/strategy/ROADMAP.md`, 앵커 `자동 실행이 1.0 미구현`).

### 1-5. 범위 밖으로 판정한 것

- **`Assets/Editor` 13개**: 파일 쓰기 적중 22건은 전부 개발 머신의 빌드·임포트 시점이다. 플레이어에 들어가지 않는다 — `Builds/Windows/StickMate_Data/Managed`와 macOS 앱 `Contents/Resources/Data/Managed`에서 이름에 `editor`가 든 dll은 둘 다 0이다(양성: `StickMate.Runtime.dll` 각 1). ~~빌드 영수증 파일(`dgpu-export-patch.txt`·`mac-gpu-switching-plist.txt`)은 **배포물 안에 들어 있는 파일**이다.~~ 실행 중 사용자 PC에 새로 생기는 쓰기가 아니다.
  - ★ **정정 2026-09-28 (code-inspection) — 취소선 문장은 영수증을 옮기기 전에 참이었다**(as-of). **범위 밖 판정 자체는 바뀌지 않는다** — 뒤 문장(「실행 중 사용자 PC에 새로 생기는 쓰기가 아니다」)은 여전히 참이고, 오히려 Windows 쪽은 배포물에서 빠져 더 좁아졌다. 바뀐 것은 **어디에 쓰는가** 하나다.
  - **Windows**: 이제 산출물 폴더가 아니라 프로젝트 루트 아래 `Logs/BuildReceipts/`에 쓴다(`Assets/Editor/WindowsHybridGpuExportPostprocessor.cs` 앵커 `private static string ReceiptPath() => BuildStandalone.ResolveBuildReceiptPath(ReceiptFileName);` → `Assets/Editor/BuildStandalone.cs` 앵커 `public const string BuildReceiptSubFolder = "Logs/BuildReceipts";`). 그 폴더는 `.gitignore`의 `[Ll]ogs/`에 걸려 저장소로도 새지 않는다. ⇒ **배포물 안이 아니다.**
  - **macOS**: 옮기지 않았다. `.app` 번들 **옆**(번들의 부모 폴더)에 그대로 쓴다(`Assets/Editor/MacHybridGpuInfoPlistPostprocessor.cs`의 `WriteReceipt`, 앵커 `string dir = string.IsNullOrEmpty(appPath) ? null : Path.GetDirectoryName(appPath);`). 배포 단위가 `.app` 번들이고 영수증은 이미 그 밖이라 옮길 이유가 없었다 — 그 훅의 주석이 「위치는 옮기지 않는다」로 사유를 적고 있고 `MacHybridGpuPlistTests`가 그 위치를 소스로 잠근다. ⇒ **비대칭은 사고가 아니라 배포 단위 차이에서 나온 의도다.** 단 `Builds/macOS/` 폴더를 통째로 압축해 배포하면 이 파일은 그 zip 안에 든다 — 그쪽은 N-19(계정명) 축이고 이 절의 범위 밖 판정과 별건이다.
- **테스트 전용 경로가 프로덕션 파일 안에 있는 것**: `CharacterSaveStore.RedirectToTemporaryDirectoryForTesting`·`ReservedBarRestoreLedger.RedirectToTemporaryDirectoryForTesting`(`temporaryCachePath` 아래 폴더 생성)과 `SimulateDeathDuringOverwriteForTesting`(잘린 쓰기)은 프로덕션 호출이 0이다. 정의 자신과 같은 파일 안 테스트 전용 함수에서만 부르고, 호출하는 테스트 파일은 7개다(양성).
- **개발 머신에만 있는 파일**: macOS 세이브 폴더의 `stickmate_character.json.backup-<날짜>-<시각>` 2개는 앱 코드에 그 이름 패턴(`.backup-`)이 0건이다. 앱이 만든 것이 아니다(생성 주체는 이 라운드에서 특정하지 않았다).

---

## 2. 인벤토리

### 2-1. 레지스트리 (Windows) / 설정 저장소 (macOS·iOS)

| # | 쓰기 주체 | OS별 실제 위치 | 내용 | 언제 | 지우는 경로 | 원칙 3 |
|---|---|---|---|---|---|---|
| **R-1** | `Interaction/PlayerPrefsGearMenuOnboardingSeenStore.cs:98` 앵커 `public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);` · 키 상수 앵커 `public const string Key = "StickMate.GearMenu.OnboardingSeen.v1";` · 호출 `Interaction/GearRadialMenuWidget.cs:923` 앵커 `seenStore.MarkSeen();` | Windows: `HKEY_CURRENT_USER\Software\Vibelab\StickMate`(1차 문서 · `companyName: Vibelab` · `productName: StickMate`). 값 이름은 `StickMate.GearMenu.OnboardingSeen.v1_h<해시>` 형태일 것으로 **추론**한다. 근거는 코드 주석(`Interaction/PlayerPrefsGearMenuOnboardingSeenStore.cs` 앵커 `Unity가 <b>키 이름에서</b> 만들므로`)이다. Unity 1차 문서의 해시 꼬리 문장은 「In-Editor Play mode storage location」 절 안에 있어 플레이어를 직접 말하지 않는다(6절). macOS: `~/Library/Preferences/com.Vibelab.StickMate.plist`(1차 문서 + 빌드 번들 ID 실측). iOS: `NSUserDefaults`(1차 문서) | 정수 `1` 하나(「부채꼴 첫 안내를 봤다」) | 부채꼴을 **처음 펼친 순간** 1회. 직후 `PlayerPrefs.Save()` | 앱: 지우지 않는다(`PlayerPrefs.Delete*` 0). 사용자: 앱을 끈 뒤 Windows는 레지스트리 편집기에서 위 키 삭제, macOS는 plist 삭제. 지우면 안내가 한 번 더 뜰 뿐이다 | 대상 아님(앱 전용 키). ★ 단 「레지스트리 쓰기 0」 주장과 충돌 |
| **R-2** | Unity 엔진(`UnityPlayer.dll`·`UnityPlayer.dylib`). 키 **이름**이 생기는 것은 우리 `PlayerPrefs` 호출과 무관하다(추론 — 이 이름들을 쓰는 우리 코드 0). 그러나 **값**은 우리 호출이 정할 수 있다. 창 크기·창 모드는 `Screen.SetResolution(targetPixelW, targetPixelH, FullScreenMode.Windowed);`(`Platform/Windows/WindowsOverlayStateEnforcer.cs` 1회 · `Platform/MacOS/MacOverlayStateEnforcer.cs` 1회)가 정하고, 품질 레벨은 `Platform/RenderQualityTuner.cs`가 레벨마다 바꿨다가 `QualitySettings.SetQualityLevel(originalLevel, false);`로 되돌린다. 그 결과가 이 키에 **저장되는지는 미확인** | R-1과 **같은 키**(PlayerPrefs 저장소) | 개발 머신 `com.Vibelab.StickMate.plist`(플레이어 또는 에디터 Play 모드 — 표 아래) 실측 키 이름 17개: `Screenmanager Fullscreen mode`(+`Default`) · `Screenmanager Resolution Height`(+`Default`) · `Screenmanager Resolution Width`(+`Default`) · `Screenmanager Resolution Use Native`(+`Default`) · `Screenmanager Window Position X` · `Screenmanager Window Position Y` · `UnityGraphicsQuality` · `UnitySelectMonitor` · `unity.player_session_count` · `unity.player_sessionid` · `unity_connect.installation_id` · `unity_connect.mega_session_id` · `unity_connect.session_id`. 값은 식별자라 옮기지 않는다 | **미확인**(plist 수정 시각으로는 가를 수 없다) | R-1과 같다. 앱이 이 값을 지우는 경로는 없다 | 대상 아님. `unity_connect.*`는 `ProjectSettings/UnityConnectSettings.asset` 최상위 `m_Enabled: 0`인데도 생겼다(macOS 실측). 이 키들의 뜻을 설명한 Unity 1차 문서는 찾지 못했다(PlayerPrefs 문서 원문에 0회 · 웹 검색 결과 0건). 이름으로 Unity Connect 계열 기능과 잇는 것은 **추론**이다 |

- **R-2의 Windows 근거 수준 — 추정**: Windows 레지스트리 실측은 0회다. 근거는 셋이다. ① 1차 문서의 PlayerPrefs 위치 ② `UnityPlayer.dll`에 같은 키 이름 문자열 실재(`Screenmanager Window Position X`·`UnitySelectMonitor`·`unity.player_sessionid`·`unity_connect.installation_id` 각 UTF-8 1, 음성 대조 없는 이름 0) ③ 같은 dll의 `RegSetValueExW` 임포트.
- **개발 머신 macOS의 plist 두 개**: `com.Vibelab.StickMate.plist`(18키, `Screenmanager` 포함)와 `unity.Vibelab.StickMate.plist`(8키, `Screenmanager` 없음, `unity.cloud_userid` 있음)가 있다. 앞의 것의 출처는 **플레이어 또는 에디터 Play 모드**다. Unity 1차 문서상 플레이어는 `<BundleIdentifier>.plist`(빌드 번들 ID `com.Vibelab.StickMate`)이고 에디터 Play 모드는 `com.<회사>.<제품>.plist`라서, **이 프로젝트에서는 두 이름이 같다** — 파일 이름으로는 가를 수 없다. 뒤의 것의 생성 주체는 미확인이다(`docs/marketing/TRUTH_INVENTORY.md` R5-5는 에디터·배치모드로 추정한다). 사용자 기계에는 에디터가 없으므로 사용자 기계의 그 이름 plist는 플레이어 것으로 판단한다(1차 문서 위치 규칙에서 따라오는 판단, 사용자 기계 실측 0).
- **옛 회사명 흔적**: 회사명 변경 전 공개 빌드(`windows-preview-20260901b`·`20260901d` 릴리스 본문이 `LocalLow\DefaultCompany\StickMate` 경로를 안내)는 `HKCU\Software\DefaultCompany\StickMate`와 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\StickMate`를 썼을 것으로 판단한다(1차 문서 위치 규칙에서 따라오는 추론). 지금 코드는 옛 경로를 읽지도 쓰지도 않는다(`DefaultCompany` 프로덕션 적중 1 = 주석). 사용자 쪽 정리 명령은 `docs/COMPANY_RENAME_MIGRATION.md` 3절에 있다.

### 2-2. 파일 — 전부 `Application.persistentDataPath` 아래

위치(1차 문서):
- Windows: `%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate`
- macOS: 문서 원문은 `~/Library/Application Support/unity.company name.product name`이고, 에디터 경로 `~/Library/Application Support/Vibelab/StickMate`가 이미 있으면 그쪽을 쓴다. 개발 머신은 에디터 경로가 있어 그쪽을 쓴다. ★ **에디터가 없는 사용자 맥에서 실제로 어느 쪽인지는 미확인**이다 — 매뉴얼 2장 3-1은 `Vibelab/StickMate`로 적고 있다(4절 목록 #9).
- iOS: `/var/mobile/Containers/Data/Application/<guid>/Documents`(문서). iPad·iPhone 빌드는 0이라 실측 불가다.

| # | 쓰기 주체 | 파일 | 내용 | 언제 | 지우는 경로 | 원칙 3 |
|---|---|---|---|---|---|---|
| **F-1** | `Core/CharacterSaveStore.cs` 이름 앵커 `private const string FileName = "stickmate_character.json";` · 교체 앵커 `File.Replace(temp, path, backup);` · 첫 저장 빈 파일 앵커 `if (firstSave) File.WriteAllText(path, string.Empty);` · 대체 경로(원자 교체 전부 실패) 대피 앵커 `bool sheltered = TryShelterCurrentGeneration(path, previous);` → 직접 덮어쓰기 앵커 `File.WriteAllText(path, json);` | `stickmate_character.json` | 레벨·장비·재화·설정·할일·창 위치(JSON, 평문). 할일 본문 = 사용자 입력 | 기동 `Load` 뒤. 더티일 때 주기 저장(`Interaction/CharacterProgressionDirector.cs` 앵커 `_config.progressionAutoSaveIntervalSeconds) : 60f;`) · 종료(앵커 `private void OnApplicationQuit()`) · 사용자 조작 즉시 저장(고정 문자열 `CharacterSaveStore.Save()` 기준 프로덕션 코드 호출 20자리 · 9개 파일). ★ **원자 교체가 재시도까지 전부 실패하면**(코드 주석상 Windows에서 가끔) 같은 저장이 직전 세대로 대피 복사한 뒤 **본체를 직접 덮어쓴다** — 원자성이 없는 유일한 경로다 | 앱: 지우지 않는다(프로덕션 `File.Delete` 0). 사용자: 앱을 끈 뒤 폴더 또는 파일 삭제(매뉴얼 2장 3-1) | 대상 아님(OS가 배정한 앱 자리) |
| **F-2** | `Core/CharacterSaveStore.cs` 앵커 `private const string PreviousFileName = "stickmate_character.prev.json";` · `File.Replace`의 백업 인자 · 대피 복사 앵커 `File.Copy(path, previous, true);` | `stickmate_character.prev.json` | 직전 세대 사본 | 두 번째 저장부터 매 저장. 원자 교체가 전부 실패한 저장에서는 본체가 온전할 때 대피 복사로 쓴다 | F-1과 같다 | 대상 아님 |
| **F-3** | `Core/CharacterSaveStore.cs` 앵커 `private const string TempFileSuffix = ".writing";` · 이름 앵커 `private static string TempFileName => FileName + "." + InstanceTag + TempFileSuffix;`(가운데 꼬리표는 프로세스 아이디, 못 얻으면 `r`와 난수 8자) · 쓰기 `new FileStream(temp, FileMode.Create, …)` + `Flush(true)` | `stickmate_character.json.<pid>.writing` | 쓰는 중인 전체 JSON | 매 저장의 첫 단계에서 생긴다. 정상이면 `File.Replace`가 본체로 옮겨 사라진다. ★ **크래시 없이도 남는다.** ① 교체 직전 재확인이 디스크에서 더 새 스키마 파일을 만나 물러설 때(앵커 `AbandonWriteToNewerFile(path, diskVersion);` — 임시 파일을 쓴 뒤 교체 없이 돌아가고, 그 실행은 저장을 보류한다) ② 원자 교체가 재시도까지 전부 실패해 직접 덮어쓰기(F-1 대체 경로)로 넘어갈 때(교체가 임시 파일을 옮기지 못한 채 실패한다는 전제 — 코드의 실패 주입이 이 모양이고, 실패 형태별 1차 문서 대조는 안 했다). 그리고 ③ 쓰는 도중 끊기면 남는다. 앱은 치우지 않는다(삭제 능력 0). 같은 실행의 다음 저장은 같은 이름을 다시 쓰지만, **이름에 pid가 들어가 실행이 바뀌면 이름도 바뀌므로 실행마다 쌓일 수 있다**(코드 주석은 OS의 pid 재사용으로 무한히 늘지 않는다고 적는다 — 개수 상한은 미확인) | 사용자만(앱을 끈 뒤) | 대상 아님 |
| **F-4** | `Core/CharacterSaveStore.cs` 앵커 `$"character_save.v{version}.backup.json"` · 앵커 `if (!File.Exists(backupPath)) File.Copy(path, backupPath);` | `character_save.v<N>.backup.json` | 이 빌드보다 **새 스키마** 세이브의 원본 사본(버전별 1회) | 새 스키마 세이브를 만난 두 자리에서 부른다. ① 기동 `Load`(앵커 `if (TryBackupOnce(path, fileVersion, out string backupPath, out string failure))`) ② ★ **저장 시점** — 교체 직전 재확인이 그 사이 더 새 스키마로 바뀐 디스크 파일을 발견했을 때(앵커 `bool backedUp = TryBackupOnce(path, diskVersion, out string backupPath, out string failure);`, 새 빌드 인스턴스가 같은 파일에 저장한 경우). 둘 다 사본이 이미 있으면 손대지 않는다 | 사용자만 | 대상 아님 |
| **F-5** | `Platform/ReservedBarRestoreLedger.cs` 앵커 `private const string FileName = "stickmate_reserved_bar_restore.json";` · 쓰기 앵커 `using (var fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None))` · 조건 `Platform/ReservedBarRevealDirector.cs` 앵커 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `stickmate_reserved_bar_restore.json` | `version`·`active`·`originalAutoHide`·`platform`·`writtenAtUtc`·`pid`. 코드 주석상 200바이트 미만(미실측) | **Windows에서 기동 시 자동 숨김이 켜져 있을 때만** 연다(시스템보다 먼저). 종료 원복 때 `active=false`로 닫는다. 크래시 뒤에는 다음 기동이 갚고 닫는다. 자동 숨김이 원래 꺼져 있으면 파일이 생기지 않는다(`docs/TASKBAR_REVEAL.md` 2-1) | 앱: 지우지 않고 닫기만. 사용자: 삭제 가능하나 **`active=true`일 때 지우면 원복 흔적을 잃는다** — 앱을 한 번 켰다 정상 종료한 뒤에 지운다 | O-1 예외의 안전장치. 파일 자체는 대상 아님 |
| **F-6** | `Platform/SessionExitMarker.cs` 앵커 `public const string MarkerFileName = "session-exit-marker.txt";` · 쓰기 앵커 `using (var stream = new FileStream(markerPath, FileMode.Create, FileAccess.Write, FileShare.Read))` · 폴더 `Platform/FreezeForensicsPolicy.cs` 앵커 `public const string DirectoryName = "FreezeForensics";` · 조건 앵커 `=> !isEditor && isDesktopPlayer;` | `FreezeForensics/session-exit-marker.txt` | 한 줄 `state=… pid=… trigger=… utc=…` | **데스크톱 플레이어의 모든 실행**: 기동(`running`) · 종료 시작(`exit-started`) · 정상 종료(`clean-exit`). 같은 이름으로 덮어쓴다 | 앱: 덮어쓰기만. 사용자: 앱을 끈 뒤 삭제 | 대상 아님 |
| **F-7** | `Platform/FreezeForensicsLog.cs` 새 슬롯 앵커 `using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))` · `Platform/FreezeForensicsPolicy.cs` 이름 앵커 `"freeze-watchdog-" : "freeze-forensics-"` · `Platform/FreezeForensicsPolicy.cs` 상한 앵커 `public const int SlotCount = 12;` · 앵커 `public const long MaxBytesPerFile = 512L * 1024L;` | `FreezeForensics/freeze-forensics-NN.log` · `FreezeForensics/freeze-watchdog-NN.log` | 멈춤·화면 구성 변화 진단 줄. 머리줄에 앱 버전·OS·GPU 이름·pid·UTC | **첫 사건 때** 생긴다(메인 스레드 3초 이상 정지, 화면 구성 변화 후 60초 하트비트 등). 정상 상주 중에는 쓰지 않는다 | 앱: 링 덮어쓰기(삭제 0). 상한 설계값 2채널 × 12슬롯 × 512 KiB = 12 MiB. 사용자: 앱을 끈 뒤 삭제 | 대상 아님 |
| **F-8** | `Platform/SessionExitMarker.cs` 앵커 `public const string CopyFilePrefix = "previous-abnormal-player-";` · 쓰기 앵커 `using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read))` · 상한 앵커 `public const int CopySlotCount = 3;` · `public const long MaxCopyBytes = 4L * 1024 * 1024;` | `FreezeForensics/previous-abnormal-player-NN.log` | 직전 실행 `Player-prev.log`의 끝부분 사본 + 머리줄. ★ 로그 원문이라 **사용자 이름이 든 경로가 섞일 수 있다**(매뉴얼 2장 3절이 이미 경고) | 직전 실행 판정이 `AbnormalExit`인 다음 기동에서만 | 앱: 3슬롯 링 덮어쓰기. 상한 3 × 4 MiB(+머리줄). 사용자: 앱을 끈 뒤 삭제 | 대상 아님. 원본 `Player-prev.log`는 읽기만(공유 모드 열기) |

폴더 생성: `persistentDataPath`(`CharacterSaveStore.cs`·`ReservedBarRestoreLedger.cs`의 `Directory.CreateDirectory`)와 `FreezeForensics`(`SessionExitMarker.cs`·`FreezeForensicsLog.cs`). 파일 쓰기 싱크는 위 4개 파일(`CharacterSaveStore` · `ReservedBarRestoreLedger` · `SessionExitMarker` · `FreezeForensicsLog`)이 전부다. PlayerPrefs 싱크는 1개 파일이다. 어느 세계의 쓰기든 이 닫힌 집합을 지난다(3절).

개발 머신 macOS 실측(파일 이름만): 세이브 폴더에 `stickmate_character.json` · `stickmate_character.prev.json` · `character_save.v8/v9/v11.backup.json`이 있다. `FreezeForensics` 폴더는 없다. 로컬 macOS 빌드의 `StickMate.Runtime.dll` 수정 시각이 09-09이고 기록 기능은 `ccfaef9`·`3cc6753`(둘 다 2026-09-14)에 들어왔으므로, 기능 도입 전 빌드이기 때문으로 판단한다. `stickmate_reserved_bar_restore.json`도 없다(Windows 전용이라 기대대로).

### 2-3. 로그 (엔진)

| # | 쓰기 주체 | 위치 | 내용 | 언제 | 지우는 경로 | 원칙 3 |
|---|---|---|---|---|---|---|
| **L-1** | Unity 플레이어. 켜는 설정 `ProjectSettings/ProjectSettings.asset` 앵커 `usePlayerLog: 1`. 우리 코드가 따로 쓰는 로그성 파일은 F-6–F-8(`FreezeForensics`)뿐이다. 그 밖에는 `Debug.Log`만 쓴다(`Platform/StallAttributionProbe.cs`의 로그 핸들러는 파일 쓰기 적중 0) | Windows `%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate\Player.log` · macOS `~/Library/Logs/Vibelab/StickMate/Player.log`(둘 다 1차 문서) + 같은 폴더 `Player-prev.log` | 우리 `Debug.Log` 전부. ★ 경로를 찍는 줄이 있다(예: `[작업표시줄]` 흔적 파일 경로) → **사용자 이름이 든 경로** | 매 실행. 새 실행이 이전 로그를 `Player-prev.log`로 민다(2세대) | 앱: 지우지 않는다. 사용자: 앱을 끈 뒤 삭제 | 대상 아님 |
| **L-2** | Unity 충돌 처리기 `UnityCrashHandler64.exe`(Windows 빌드에 포함) | `%TMP%\Vibelab\StickMate\Crashes`(1차 문서 `Windows.CrashReporting.crashReportFolder`) | 충돌 보고(덤프 등 — 내용 미확인) | **Windows에서 충돌했을 때만** | 앱: 없음. 사용자: 삭제. OS 임시 폴더 정리 도구가 치울 수 있음 | 대상 아님 |

- `Player-prev.log` 교체는 **1차 문서에서 찾지 못했다**(log-files 페이지에 `prev` 0회). 근거는 셋이다. 플레이어 바이너리에 문자열이 실재한다(Windows `UnityPlayer.dll` UTF-8 1 · macOS `UnityPlayer.dylib` UTF-8 2). 개발 머신 macOS 로그 폴더에 파일이 실재한다. `SessionExitMarker`가 그 이름을 전제한다.
- `Player.log` 크기 상한은 미확인이다.
- macOS 충돌 기록: Unity 문서의 충돌 폴더 항목은 「Windows only」다. macOS 자체 진단 보고서는 OS 기능이라 열거하지 않았다.
- 충돌 보고 **전송**: `enableCrashReportAPI: 0`이고 `UnityConnectSettings.asset`의 `m_EnableCloudDiagnosticsReporting: 0`이다. 로컬에 쓰는 것만 이 표의 대상이다. 아웃바운드 0은 여기서 주장하지 않는다(`SECURITY_MODEL.md` 1-2 미실측 그대로).
- ★ **2026-09-26 추가(`security` 로그 싱크 전수) — L-1의 「내용」 칸이 말하는 것보다 넓다.** 새 쓰기 싱크가 생긴 것이 아니다(파일 4 · PlayerPrefs 1 · OS 쓰기 P/Invoke 2는 그대로다). 확정된 것은 **이미 있는 L-1에 무엇이 실리는가**이고, 그래서 3절 세계 표의 L-1 칸은 「있음」 그대로 유효하다. 출시판이 **상시** 남기는 것 둘: macOS는 남의 앱 이름을 60초마다(`[발판리포트] 보이는 상단테두리` · 주기 `FootholdReportIntervalSecondsQuiet = 60f` · 배포 애셋 `verboseDiagnosticsLogging: 0`에서도 도달), 양 플랫폼은 사용자가 타이핑한 할일 본문을(`[할일패널] 추가 —` · `강조 할일` · `체크박스 클릭 — 항목`) 남긴다. 전수 표와 출시판 도달 여부는 `docs/security/SECURITY_MODEL.md` 앵커 `4-4-b. 2026-09-26 정정`에 있다. ⇒ 이 문서에서 바뀌는 것은 **5-1 판정 2의 「무엇이 남는가」 설명과 사용자 고지**이고, 2-1–2-5 표의 행 수는 바뀌지 않는다.

### 2-4. OS 상태 — 우리 호출로 OS·셸이 저장하는 것

| # | 쓰기 주체 | OS가 저장하는 곳 | 내용 | 언제 | 되돌리는 경로 | 원칙 3 |
|---|---|---|---|---|---|---|
| **O-1** | `Platform/Windows/WindowsReservedBarAutoHideControl.cs:130` 앵커 `SHAppBarMessage(ABM_SETSTATE, ref data);` · 정책 `Platform/ReservedBarRevealPolicy.cs` · 실행 `Platform/ReservedBarRevealDirector.cs` | 탐색기(셸)의 작업표시줄 설정. API 문서는 저장 여부를 말하지 않는다. 참고 서술(Microsoft 직원 블로그, API 계약 아님)은 로그오프 때 **그 순간의 값**을 셸이 쓴다고 한다 — `docs/TASKBAR_REVEAL.md` 2-3의 인용(앵커 `Explorer always writes that value when you log off`) 재인용이고, security는 원문을 다시 대조하지 않았다 | 자동 숨김 비트 1개 | 기동 시 원래 켜져 있으면 해제. 정상 종료·`WM_ENDSESSION`에서 원복. 크래시 뒤 다음 기동이 먼저 복구 | 앱이 원복(흔적 F-5). 사용자: 작업표시줄 설정에서 직접 | ★ **승인된 예외 1건**(CLAUDE.md 원칙 3, 사용자 확정 2026-09-02). 원복 전에 셸이 저장하면 바뀐 값이 영속될 수 있다 — `docs/systems/TASKBAR_LOGOFF_ORDERING.md` 판정 (c) 실기 필요 · e-5 |
| **O-2** | `Platform/Windows/WindowsSystemTrayIcon.cs:349` 앵커 `ok = Shell_NotifyIcon(SystemTrayPresencePolicy.NotifyIconAdd, ref data);` · 플래그 `Platform/SystemTrayPresencePolicy.cs` 앵커 `public const uint NotifyIconAddFlags =`(`NIF_MESSAGE`·`NIF_ICON`·`NIF_TIP`, `NIF_GUID` 안 씀 — `guidItem` 대입 0) | 셸의 알림 영역 아이콘 기록. 1차 문서(Microsoft Learn 「Notifications and the Notification Area」): 「When an application is uninstalled, its notification area icon can still appear to the user as an option in the Notification Area Icons page in the Control Panel for up to seven days.」 → 셸이 아이콘을 기억한다. **저장 위치·형식은 1차 문서에 없다 — 미확인**(비공식 자료의 레지스트리 키 이름은 옮기지 않는다) | 아이콘 식별(창 핸들 + ID 방식)·툴팁 등(셸 내부, 미확인) | 트레이를 켠 실행(기본). `STICKMATE_NO_TRAY_ICON`이면 아이콘을 추가하지 않는다(`_optOut = ResolveOptOut();` · `TASKBAR_REVEAL.md` 2-2 수신 창 표) | 앱: 종료 때 `NIM_DELETE`(크래시면 못 부름). 셸 기록 자체는 앱이 지우지 않는다. 사용자: Windows 설정의 알림 영역 아이콘 목록 | 대상 아님 — 우리 쓰기가 아니라 셸이 스스로 남기는 기록이다. 사용자 설정을 바꾸지 않는다 |

**영속하지 않는 것(확인 목록)**: `WS_EX_TOOLWINDOW`·`ITaskbarList::DeleteTab`(작업표시줄·Alt+Tab에서 우리 창 빼기), 레이어드·투명 창 스타일, macOS `setActivationPolicy:`·`setCollectionBehavior:`. 전부 **우리 창·우리 프로세스**의 런타임 상태다. 재실행하면 코드가 다시 건다. 영속 여부를 1차 문서로 따로 대조하지는 않았다.

**OS 일반 기록**: Windows·macOS가 「실행된 프로그램」에 대해 스스로 남기는 기록은 모든 앱에 공통이고 우리가 제어하지 않는다. 이 인벤토리에서 열거하지 않았다.

### 2-5. 휴면 · 해당 없음

| 항목 | 판정 | 근거 |
|---|---|---|
| Steamworks 활성 시 추가되는 쓰기 | **휴면 — 해당 없음** | `SECURITY_MODEL.md` X절 「X-1. 사실 확인」: 공개 빌드 19개 + 로컬 빌드에서 H1(스텁 타입 1) · H2(`steamworks` 어셈블리 참조 0) · H3(`Steamworks` 형 참조 0) · H4(Steam 문자열 0) · H5(`steam` 이름 파일 0) 전부 참. 정의 `STICKMATE_STEAMWORKS_INSTALLED`는 한 번도 켜진 기록이 없다. ★ **켜는 라운드에서 이 인벤토리를 다시 잰다** — `steam_api64.dll`이 무엇을 쓰는지는 미확인이다 |
| 자동 실행 등록 | **0** | 1-2 autostart 니들 0 + 합성 변이 2 적중 · 1-4 엔진 문자열 0 · 로드맵 「1.0 미구현」. 개발 머신 `~/Library/LaunchAgents`에 이 앱 항목 0(양성: 폴더 항목 3개 열거됨. 2판에서 폴더 존재를 먼저 판정한 뒤 다시 세어 3 · 이름에 `stickmate`·`vibelab` 0) |
| macOS 윈도 복원 상태(`~/Library/Saved Application State`) | 개발 머신에 **이 폴더 자체가 없다** → 이 앱 항목 0 | 존재를 판정하는 명령의 출력으로만 판단했다. `~/Library` 목록 89항목에 이 이름이 없다(같은 목록의 양성: `Preferences`·`Caches` 있음). `test -d`는 rc 1이다(같은 명령의 양성: `~/Library/Preferences` rc 0). 초판의 「양성: 같은 폴더 항목 1개」는 죽은 프로브였다(8절 정정 7). 사용자 기계는 미확인 |
| macOS 캐시(`~/Library/Caches`) | 개발 머신에 이 앱 항목 0 | 양성: 항목 100개, 그중 `unity` 이름 5(2판에서 폴더 존재를 먼저 판정한 뒤 다시 세어 같은 값). `temporaryCachePath`는 프로덕션에서 테스트 전용 함수만 쓴다 |

### 2-6. 예정 쓰기 — 명세만 있고 코드는 0줄

`docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md`가 `PlayerPrefsReservedBarOwnerTokenStore`를 명세한다. 작업표시줄 원복 흔적(F-5)이 이 설치의 것인지 알아보는 무작위 설치 토큰 저장소다. 설계 조건은 `docs/security/ENTITLEMENT_CONTRACT.md` S-7-7이다(순서 앵커 `**다시 읽어 같은지 확인** → 그 뒤에만 흔적`). **2-1–2-5 표는 착지 전 코드만 셌다.**

| 항목 | 착지 전(HEAD `76504c2`) | 착지 뒤(명세대로라면 — 코드가 아니다) |
|---|---|---|
| 코드 | `Assets/_Project/Scripts`에서 `PlayerPrefsReservedBarOwnerTokenStore` 이름이 든 파일 0(같은 `grep -rl` 형태의 양성: `PlayerPrefsGearMenuOnboardingSeenStore` 6파일) | Windows 전용 파일 1개. 생성은 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` 구간 안에서만(명세 감사 `macOS_경로에서_토큰_확보_호출이_없다`) |
| 무엇을 어디에 | 없음 | 무작위 토큰 문자열 1개. R-1과 **같은 키** `HKCU\Software\Vibelab\StickMate`에 쓴다(`SetString` → `Save` → 되읽어 확인). macOS plist에는 0 |
| 언제 | 해당 없음 | Windows에서 자동 숨김이 켜진 실행이 처음 해제할 때 1회(명세 해석 I-1 앵커 `자동 숨김이 켜진 첫 실행은 토큰을 1회 만들고`). 원래 자동 숨김이 꺼진 사용자에게는 생기지 않는 것으로 읽힌다(추론) |
| 지우는 경로 | 해당 없음 | 앱: 지우지 않는다(명세 감사 `프로덕션과_테스트_어디에도_PlayerPrefs_DeleteAll이_없다`). 사용자: 레지스트리 키 삭제. ★ **흔적 F-5가 열린 동안 토큰을 지우면 다음 실행이 자기 흔적을 알아보지 못해 원복하지 않는다**(명세 I-1 앵커 `저장소가 비어 있으면 **자기 흔적**을 갚지 않는다` · 판독 표 앵커 `실패 방향은 안전(남의 설정 불변)`). 앱을 정상 종료한 뒤에 지워야 한다 |
| 원칙 3 | 해당 없음 | 대상 아님(앱 전용 키). O-1 예외의 안전장치 쪽이다 |

착지하면 이 문서에서 바뀌는 곳:
- **R-1**: 행 내용(부채꼴 안내 값)은 그대로다. 그러나 「지우는 경로」의 「지우면 안내가 한 번 더 뜰 뿐이다」가 **착지 뒤에는 거짓이 된다** — 같은 키를 지우면 토큰도 지워지고, F-5가 열린 동안이면 원복을 잃는다. 토큰은 새 행 R-3(Windows 전용 · 자동 숨김 사용자만)으로 둔다. 0절 레지스트리 행 수는 2에서 3, 3절 싱크의 PlayerPrefs는 1에서 2가 되고, 3절 세계 표에 R-3 칸이 생긴다(macOS · iPad·iPhone은 없음).
- **인계 ③**: 감사 ②의 허용 파일이 단일 이름이라, 토큰 저장소 파일이 들어오는 순간 감사 ②가 빨개진다(명세 앵커 `coder 우회 금지 감사에 토큰 저장소 파일을 등재해야 빨강이 풀린다`). 그 등재가 (가) 「R 행 짝」을 적는 자리가 되고, (나)의 키 집합은 {온보딩 키, 토큰 키} 2개가 된다.
- **안 A**: 문안은 착지 뒤에도 참이도록 썼다. 앱 값을 개수 없이 「자기 상태 값(예: …)」으로 적었기 때문이다. ★ **착지 조건(3판)**: 토큰 저장소가 착지하면 앱이 무작위 값(토큰)을 그 키에 둔다. 착지하는 라운드 안에 안 A의 앱 값 예시에 그 값을 명시한다(예: 「작업표시줄 원복 기록을 알아보는 무작위 값」 / "a random value used to recognize its own taskbar-restore record"). 개수가 없는 문안이라 착지 뒤에도 문자 그대로 거짓은 아니지만, 식별자처럼 보이는 값을 엔진 쪽만 인정하고 앱 쪽을 빼면 회피로 읽힌다. ★ 착지 전후 모두 「앱이 두는 값은 하나」 같은 **개수 문장을 넣지 않는다.**
- **F-5 · 인계 ②(매뉴얼 제거 안내)**: 「정상 종료 뒤에 지운다」가 흔적 파일에 더해 레지스트리 키에도 걸린다.
- **인계 ④(Windows 체크표)**: 값 이름 목록에 토큰 키 이름이 더해진다. 토큰 값은 옮기지 않는다(명세 앵커 `이 파일에는 토큰 값이 들어간다`).

---

## 3. 세계별 — 전칭을 쓰기 전에

쓰기 싱크가 닫힌 집합이다(파일 4 · PlayerPrefs 1 · OS 쓰기 P/Invoke 2 — 착지 전 기준. 예정 쓰기가 착지하면 PlayerPrefs 2, 2-6). 그래서 세계가 바꾸는 것은 **어느 싱크가 도는가**와 **언제 도는가**뿐이다.

| 세계 | R-1 | R-2 | F-1–F-4 | F-5 | F-6–F-8 | L-1 | L-2 | O-1 | O-2 | 자동 실행 |
|---|---|---|---|---|---|---|---|---|---|---|
| macOS 플레이어 | plist | plist(실측) | 있음 | **없음**(`#if UNITY_STANDALONE_WIN`) | 있음 | 있음 | 해당 없음(Windows 전용) | 없음(Dock 미포함) | 없음 | 0 |
| Windows · 트레이 켬(기본) | 레지스트리 | 레지스트리(추정) | 있음 | 자동 숨김 사용자만 | 있음 | 있음 | 충돌 때만 | 자동 숨김 사용자만 | **있음** | 0 |
| Windows · 트레이 끔 | 레지스트리 | 레지스트리(추정) | 있음 | 자동 숨김 사용자만 | 있음 | 있음 | 충돌 때만 | 자동 숨김 사용자만 | **없음** | 0 |
| 전체화면(양 OS) | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 0 |
| 가출(양 OS) | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 위와 같음 | 0 |
| iPad·iPhone | `NSUserDefaults`(코드상) | 미확인 | 코드상 있음 | 없음 | **없음**(데스크톱 플레이어만) | 미확인 | 해당 없음 | 없음 | 없음 | 미확인 |

- 전체화면·가출 행의 「위와 같음」 근거: 두 세계의 코드는 새 쓰기 싱크를 갖지 않는다(1-2 전수에서 싱크 파일 밖 쓰기 적중 0). 저장 **시점**(예: 사용자 조작 즉시 저장의 빈도)만 달라진다.
- iPad·iPhone 행은 **빌드 0 · 실측 0**이다. 전부 코드 경로 추론이다. 모바일 빌드가 생기는 라운드에 다시 잰다.

---

## 4. `TRUST_AND_SIGNING.md` 5-2 대체 문구 제안 (수정하지 않았다)

**원문 앵커**: `레지스트리에는 쓰지 않고, 마우스` (5-2 인용 블록. 문장 전체는 「레지스트리에는 쓰지 않고, 마우스·키보드를 대신 눌러 주지 않습니다.」 짧은 형은 marketing R9 표지 안 인용에도 있어 2회라 쓰지 않는다)
**같은 절의 근거 줄**: `실호출 0건 + 양성 대조` (「레지스트리에 안 씀」 조건), `자동 감사가 지키고 있다` (네 사실이 전부 감사로 지켜진다는 문장)

### 안 A — 사실형 치환 (권장)

> 한국어: 「Windows 레지스트리에는 현재 사용자 영역의 이 앱 이름 키(`HKCU\Software\Vibelab\StickMate`)에 값을 저장합니다. 이 앱이 쓰는 게임 엔진(Unity)이 Windows에서 설정을 두는 기본 자리입니다. 그 키에는 엔진이 스스로 두는 창 크기와 위치, 전체화면 여부, 모니터 선택, 그래픽 품질, 실행 횟수, 설치 식별자와 세션 식별자 같은 값이 있고, 앱이 두는 자기 상태 값(예: 첫 안내를 봤는지)도 함께 있습니다. 마우스·키보드를 대신 눌러 주지 않습니다.」
>
> English: "On Windows, it stores values in the registry under the current user, in a key named after the app (`HKCU\Software\Vibelab\StickMate`). That key is where the Unity engine it is built on keeps its settings on Windows. The engine itself keeps values there such as window size and position, fullscreen mode, monitor selection, graphics quality, a launch count, and installation and session identifiers; the app keeps its own state values there too (for example, whether you have seen the first tip). It never presses keys or clicks for you."

- 이 문장이 주장하는 것: 위치(1차 문서 + 코드), 「현재 사용자 영역」(HKCU), 엔진 값의 **종류를 이름으로**, 앱 자기 값이 있다는 것(R-1). 종류는 macOS plist 18키 실측의 범주를 옮겼다 — 창 크기(`Screenmanager Resolution Width`·`Screenmanager Resolution Height`·`Screenmanager Resolution Use Native`), 창 위치(`Screenmanager Window Position X`·`Screenmanager Window Position Y`), 전체화면(`Screenmanager Fullscreen mode`), 모니터(`UnitySelectMonitor`), 품질(`UnityGraphicsQuality`), 실행 횟수(`unity.player_session_count`), 설치 식별자(`unity_connect.installation_id`), 세션 식별자(`unity.player_sessionid`·`unity_connect.session_id`·`unity_connect.mega_session_id`). 「실행 횟수」와 「설치 식별자」는 키 이름을 읽은 추론이다(R-2).
- 문장이 「같은 값」으로 끝나 목록이 전부라고 말하지 않는다. 예시 「첫 안내를 봤는지」도 앱 값의 전부라고 말하지 않는다. **개수를 적지 않아 예정 쓰기(2-6)가 착지해도 거짓이 되지 않는다.**
- **주장하지 않는 것**: 「그 키에만」(엔진의 다른 레지스트리 경로 미확인, 1-4), 「설정만」(식별자를 이름으로 인정했다), 개수, 값의 내용, 「이 PC 밖으로 나가지 않는다」(아웃바운드 미실측, 2-3 · `SECURITY_MODEL.md` 1-2).
- 쓸 수 있는 조건: 위치는 코드 사실과 1차 문서로 성립한다. ★ **엔진 값 종류는 macOS 실측을 옮긴 것이라, 게시 전에 Windows 실기에서 그 키의 값 이름 목록을 1회 확인하는 것이 조건이다**(인계 ④ · 값 이름만 보고 값은 옮기지 않는다). 목록의 범주가 문장과 다르면 문장을 실측대로 고친다. 이 답글은 의심하는 사람이 **자기 레지스트리 편집기를 열어 보는** 글이라, 편집기에 보이는 `unity_connect.installation_id` 같은 식별자 이름을 문장이 먼저 인정해야 회피로 읽히지 않는다.
- 편집기에서는 값 이름 끝에 `_h`와 숫자가 붙어 보일 수 있다(플레이어 적용은 추론, R-1). 문장에는 넣지 않았다. Windows 실기에서 보이면 「이름 끝의 `_h`와 숫자는 Unity가 붙이는 꼬리입니다 / The `_h` and digits at the end of each name are added by Unity」를 덧붙일 수 있다.
- 같이 바꿀 조건 줄: 「레지스트리에 안 씀 ✅ 실호출 0건 + 양성 대조」 → 「레지스트리 직접 쓰기 API 0건(감사 ①) · PlayerPrefs 호출은 1파일 · 멤버 3종(감사 ②, 키 개수는 안 셈) → Windows에서는 앱 이름 키에 저장(Unity 1차 문서) · 엔진 값 종류는 macOS 실측이고 Windows 확인 전」.
- 같이 바꿀 문장: 「그 넷은 전부 자동 감사가 지키고 있다」 → 레지스트리 항목에서 감사가 지키는 것은 「직접 API 0」(감사 ① `UserAssetImmutabilityAuditTests`)과 「PlayerPrefs 호출은 한 파일 · 멤버 3종」(감사 ② `GearMenuOnboardingSeenStoreTests` 우회 금지)이다. 앱 값 개수와 엔진 값은 감사 밖이다. 5-2 절이 괄호로 적은 감사 이름에는 감사 ②가 없다(0절 · 인계 ③).

### 안 B — 삭제 + 목록 가리키기형

> 「…창 제목은 읽지 않으며(…), 마우스·키보드를 대신 눌러 주지 않습니다. 앱이 파일과 레지스트리에 쓰는 자리는 매뉴얼 2장에 전부 적어 두었습니다.」

- 사실 넷을 셋으로 줄이고 목록으로 보낸다. 5-1 스토어 한 줄(「위 목록에 전부 적어 두었습니다」)과 같은 화법이다.
- ★ **지금은 쓸 수 없다.** 매뉴얼 2장 3-1에 레지스트리 키 · `Player-prev.log` · `.writing` 임시 파일 · Windows 충돌 보고 폴더가 없어서 「전부」가 거짓이다. 그 줄들이 먼저 들어가야 한다(4-1 목록 #8).

### 4-1. 전칭이 걸리는 곳 — 목록만 (수정하지 않았다)

| # | 파일 | 앵커(고정 문자열) | 무엇이 걸리나 |
|---|---|---|---|
| 1 | `docs/marketing/TRUST_AND_SIGNING.md` | `레지스트리에는 쓰지 않고, 마우스` | 5-2 답글. **Windows 거짓**(이 문서의 발단). marketing R9 표지가 같은 절에서 짧은 형을 인용한다 — 그 표지는 이미 거짓 판정을 적은 쪽이다 |
| 2 | 〃 | `실호출 0건 + 양성 대조` | 근거가 직접 API 호출 한정이다. 결론(「안 씀」)으로 옮기면 거짓 |
| 3 | 〃 | `자동 감사가 지키고 있다` | 레지스트리 감사는 API 이름만 본다. PlayerPrefs는 `UserAssetImmutabilityAuditTests.cs`에 0회. PlayerPrefs **위치**는 감사 ②(`GearMenuOnboardingSeenStoreTests` 우회 금지)가 한 파일로 잠그지만, 목적이 테스트 격리이고 키 개수를 세지 않는다(0절) |
| 4 | 〃 | `선언조차 하지 않는다` | 2-2 음성 대조 표의 레지스트리 쓰기 행. 표 자체는 API 기준으로 참이다. 「우리를 구한다」 맥락에서 「레지스트리에 안 쓴다」로 읽힐 수 있다 — 「직접 호출」 한정을 붙일지 판단 |
| 5 | `docs/marketing/TRUTH_INVENTORY.md` | `레지스트리 쓰기 API가 저장소 전체에` | 허용 목록 행. 문자 그대로는 참(API 이름). 게시 문장 「레지스트리에 안 씀」의 근거로 쓰이면 거짓이 된다 |
| 6 | `docs/marketing/personas/M5_신뢰구매.md` | `쓰기 API **0건**` | 레지스트리 행. #5와 같다 |
| 7 | `docs/marketing/STORE_PAGE.md` · `TRUTH_INVENTORY.md` | `컴퓨터에 흔적도 남지 않습니다` | 이미 금지·정정됨(「되돌리기 기록도」로 교체). 확인만 — 이 인벤토리와 일치 |
| 8 | `docs/manual/02-first-launch.md` | `이 폴더 안에서 보게 되는 파일입니다` | 3-1 표. F-3(`.writing` 고아)·`Player-prev.log`·레지스트리 키(R-1·R-2)·Windows 충돌 폴더(L-2)가 없다. 표가 「보게 되는 파일」이라 전칭은 약하지만, 안 B와 제거 안내가 이 표를 전수로 쓰려면 보강이 필요하다 |
| 9 | 〃 | `3-1. 저장 파일이 있는 곳` | 그 절 표의 macOS 경로. 1차 문서상 에디터 경로가 없는 사용자 맥은 `unity.Vibelab.StickMate`일 수 있다 — **미확인**(2-2 머리). macOS는 1.0 채널 밖이라 우선순위 낮음 |
| 10 | 〃 | `이 폴더를 지우면 캐릭터가 처음 상태로 돌아갑니다` | 참. 다만 「지우면 앱 흔적이 사라진다」로 읽히면 레지스트리 키(R-1·R-2)와 L-2가 남는다 |
| 11 | 〃 | `운영체제 설정 중 딱 하나만 예외입니다` | 문맥상 참(앱 전용 키는 운영체제 설정이 아니다). O-2 셸 기록은 우리가 바꾸는 설정이 아니므로 충돌 없음 — 확인만 |
| 12 | `docs/COMPANY_RENAME_MIGRATION.md` | `스스로 만든 세션 값들뿐이다` | 「뿐」 전칭. macOS 실측 엔진 키에는 창 크기·위치·모니터·그래픽 품질도 있다(R-2). Windows 실측 없이 쓴 문장 |
| 13 | `docs/marketing/CAPTURE_PROTOCOL.md` | `가 레지스트리에 있다` | **일치**(PlayerPrefs가 레지스트리에 있다고 이미 적었다). 확인만 |
| 14 | `Platform/ReservedBarRestoreLedger.cs` 클래스 문서(코드 주석 — 참고) | `이 저장소는 레지스트리 쓰기를` | 「감사로 금지하고 있고」 — 감사 범위는 API 이름이다. PlayerPrefs가 이미 같은 앱에서 레지스트리에 쓴다. 이 믿음의 출처로 보인다. 코드 주석이라 security는 고치지 않는다 |
| 15 | GitHub 릴리스 본문 29개(앱 빌드가 아닌 `design` 1개 포함, `gh release view`로 읽기만) | 고정 문자열 `레지스트리` · `registry` · `Registry` · `흔적` · `남기지` · `설치 없이` · `설치` · `아무것도` · `쓰지 않` · `nothing is written` · `no trace` | **해당 없음 확인.** 적중은 2건이고 둘 다 다른 맥락이다. `쓰지 않` 1건(`windows-preview-20260909b`, 자동 숨기기를 쓰지 않은 사용자 안내) · `아무것도` 1건(`windows-preview-20260901d`, CPU 설명). 나머지 0(양성: 같은 호출에서 `LocalLow` 18개 릴리스, `Player.log` 6개 릴리스 적중). 참고: `windows-preview-20260901b`·`20260901d` 본문은 옛 `DefaultCompany` 경로를 안내한다 |
| 16 | `docs/marketing/STORE_PAGE.md` · `docs/strategy/CHANNEL_PRICING_DECISIONS.md`의 `설치 없이` | — | **해당 없음** — 팩 구매·Smart App Control 맥락이다. 설치 흔적 주장이 아니다 |

- 이 목록이 **안 본 것**: `docs/marketing/` 밖의 초안 폴더 전체 문장 판독(고정 문자열 적중만 봤다), 스팀 스토어에 실제 게시된 문구(아직 없음), 매뉴얼 1장 6-1 제거 안내가 남는 파일을 전수로 말하는지(적중 줄만 봤다).

---

## 5. 판정 · 인계 · 미확인

### 5-1. 판정

1. 「레지스트리에 쓰지 않는다」류 전칭은 **Windows 공개 문구에서 쓰지 않는다.** 대체는 안 A.
2. 「아무것도 남기지 않는다 / 흔적이 없다」류는 **어느 세계에서도 쓸 수 없다.** 이미 금지 등재돼 있다(4-1 #7). 근거를 매 실행 / 조건부로 나눈다.
   - **매 실행**(데스크톱 플레이어): F-6(`FreezeForensics` 폴더 + `session-exit-marker.txt`) · L-1(`Player.log`, 두 번째 실행부터 `Player-prev.log`). 이 둘만으로 전칭이 거짓이다. Windows 트레이 켬(기본)에서는 O-2(셸의 아이콘 기록 — 우리 파일은 아니다)도 매 실행이다.
   - **보통 실행에서 걸리지만 조건이 있다**: F-1·F-2는 더티일 때만 저장한다(주기 · 종료 · 조작 즉시). 함께 있는 시간 누적(`Interaction/CharacterStatsDirector.cs` 앵커 `CharacterStatsModel.AddCompanionSeconds(_pendingSeconds);`)이 더티를 세우므로 보통 실행에서 걸린다고 판단한다(코드 추론, 실측 아님). R-2는 엔진이 쓰는 시점이 미확인이다(U-3).
   - **조건부**: F-3(물러섬 · 교체 전부 실패 · 쓰기 중 사망) · F-4(새 스키마 세이브) · F-5(Windows 자동 숨김 사용자) · F-7(첫 멈춤·화면 구성 사건) · F-8(직전 실행 비정상 종료) · L-2(Windows 충돌) · R-1(부채꼴 첫 펼침, 설치당 1회) · O-1(Windows 자동 숨김 사용자).
3. 「자동 실행 등록 0」은 **데스크톱 두 세계(macOS · Windows 트레이 켬/끔)에서 쓸 수 있다.** iPad·iPhone은 빌드가 없어 미확인이다.
4. 원칙 3: 새 위반 없음. 사용자 설정을 바꾸는 쓰기는 O-1 한 건이고 승인 범위 그대로다.

### 5-2. 인계 (리더 경유 — 담당자에게 직접 지시하지 않는다)

- ① `marketing`: 5-2 안 A 채택 여부 · 4-1 #1–#6·#12 정리.
- ② `perf-doc`(매뉴얼): 2장 3-1에 R-1·R-2(레지스트리 키) · `Player-prev.log` · F-3 고아 임시 파일 · L-2 충돌 폴더 줄 추가 검토. macOS 경로(#9)는 macOS 채널이 열릴 때 실기로 확정.
- ③ `test-engineer`(감사 설계, 제안만) — **남는 공백 기준으로 다시 썼다**(8절 정정 1). 파일 잠금은 이미 있다. 감사 ②(`GearMenuOnboardingSeenStoreTests` 우회 금지)가 프로덕션 `PlayerPrefs`를 한 파일 · 멤버 {`GetInt`·`SetInt`·`Save`}로 잠근다(0절). 새로 필요한 것은 감사 ②가 보지 않는 두 칸이다.
  - (가) **목적 연결**: 감사 ②의 허용 파일 추가는 테스트 격리 절차다(`GearMenuOnboardingSeenStoreTests.cs` 실패 메시지 앵커 `허용 파일로 등재하십시오(리더 경유)`). 그래서 새 `PlayerPrefs` 저장소가 등재돼도 「Windows 레지스트리에 새 값이 생긴다 → 이 인벤토리 R 행과 공개 문구 안 A를 다시 본다」는 신호가 나지 않는다. 제안: 허용 파일마다 이 인벤토리의 **R 행 번호를 짝으로** 적는 명부를 두고, 짝이 없는 허용 파일이 생기면 빨강으로 한다. 원칙 3 예외의 「파일 1개 · 형태 2개」와 같은 모양이다.
  - (나) **키 개수**: 싱크 `SetInt(string key, int value)`는 어떤 키든 받는다. 제안: 프로덕션에서 설정 저장소로 들어가는 키 상수의 **집합**을 잠근다. 지금은 {`PlayerPrefsGearMenuOnboardingSeenStore.Key`}다. 기대값은 상수 참조로 받고, 집합 밖 키 0(부재 단언)에는 같은 테스트 안의 존재 대조를 붙인다(CLAUDE.md 니들 규칙). 예정 토큰 저장소 명세의 A-1(`토큰_키를_쓰는_프로덕션_코드는_토큰_저장소_파일_한_곳뿐이다`)이 같은 자를 토큰 키 하나에 이미 건다. 두 감사를 한 키 집합으로 묶을지는 test-engineer 판단이다.
  - 감사 ② 파일은 coder 소유다. 리더가 승인하면 security가 `*AuditTests.cs`로 쓸 수 있다.
- ④ `qa-regression`(Windows 체크표): 레지스트리 판독 1줄 제안 — 앱을 한 번 켰다 끈 뒤 `reg query "HKCU\Software\Vibelab\StickMate"`의 **값 이름 목록**만 기록한다(값은 식별자라 옮기지 않는다). R-2 추정을 실측으로 바꾸는 유일한 칸이다. 같은 자리에서 `%TMP%\Vibelab\StickMate` 존재 여부도 기록한다.
- ⑤ `product-strategy`: 제거 안내 범위 — 스팀 제거는 우리 코드를 돌리지 않는다(`docs/strategy/ROADMAP.md` 앵커 `기본 동작으로는 참이지만 불완전했다` — 같은 자리에 스팀 제거 때 프로세스를 돌리는 설치 스크립트 옵션이 있다는 자진 정정이 함께 있다). 그래서 R 계열·F 계열·L 계열이 남는다. 매뉴얼에 「완전히 지우려면」을 넣을지 판단.

### 5-3. 미확인 (추측으로 메우지 않는다)

| # | 미확인 | 가르는 방법 |
|---|---|---|
| U-1 | Windows 레지스트리 키의 실제 값 목록(R-2 추정) | 인계 ④ |
| U-2 | 엔진이 PlayerPrefs 키 말고 다른 레지스트리 경로를 쓰는가 | Windows 실기에서 실행 전후 HKCU 내보내기 비교(값은 옮기지 않음) |
| U-3 | 엔진 키(R-2)를 쓰는 시점 | 같은 실기에서 기동 직후·종료 직후 비교 |
| U-4 | 셸이 자동 숨김 값을 언제 영속하는가(O-1) | `TASKBAR_LOGOFF_ORDERING.md` 판정 (c) — 체크표 §R |
| U-5 | 트레이 아이콘 기록의 저장 위치·형식(O-2) | 1차 문서 없음. 필요해지면 실기 |
| U-6 | 에디터 경로가 없는 사용자 맥의 `persistentDataPath` | macOS 채널이 열릴 때 깨끗한 계정에서 실기 |
| U-7 | `Player.log` 크기 상한 · 충돌 보고 내용 | 실기 |
| U-8 | iPad·iPhone 전부 | 모바일 빌드가 생기는 라운드 |
| U-9 | `LibUniWinC.dll` 파일 API의 실제 호출 경로 | 우리가 부르는 2함수 밖이라 우선순위 낮음 |
| U-10 | R-2 추가 관찰: `Builds/Windows/UnityPlayer.dll`과 0914 zip의 `UnityPlayer.dll`에 `unity.cloud_userid` 문자열이 있다(각 UTF-8 1 · UTF-16 0. 같은 계수의 양성 `unity.player_sessionid` 1 · 음성 없는 이름 0). 개발 머신 macOS에서는 `unity.Vibelab.StickMate.plist`에만 있고 `com.Vibelab.StickMate.plist` 18키에는 없다. 플레이어가 Windows 키에 쓰는지는 미확인 | 안 A 게시 조건(인계 ④ Windows 값 이름 목록)이 덮는다. 목록에 나오면 안 A의 식별자 범주에 이미 들어간다 |

---

## 6. 1차 출처

- Unity 6000.0 `PlayerPrefs`: 「Windows : `Computer\HKEY_CURRENT_USER\Software\ExampleCompanyName\ExampleProductName` in the Registry Editor.」 · 「macOS : `~/Library/Preferences/ExampleBundleIdentifier.plist` . The default value of ExampleBundleIdentifier is `com.ExampleCompanyName.ExampleProductName` .」 · 「iOS : … `[NSUserDefaults standardUserDefaults]` …」 · 「Unity stores PlayerPrefs in a local registry, without encryption.」 — https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerPrefs.html
  - 2판 보강(같은 페이지의 원문 HTML을 받아 태그를 걷은 본문의 위치로 확인): 「Standalone Player storage location」 절의 macOS는 `~/Library/Preferences/ExampleBundleIdentifier.plist`다. 「In-Editor Play mode storage location」 절의 macOS는 `~/Library/Preferences/com.ExampleCompanyName.ExampleProductName.plist`, Windows는 `Computer\HKEY_CURRENT_USER\Software\Unity\UnityEditor\ExampleCompanyName\ExampleProductName` key다. 해시 꼬리 문장 「Note that Windows uses the key names from the application's PlayerPrefs as a hashed identifier. For example, Unity adds a DeckBase string to the hashed key name (for example h3232628825) to create DeckBase_h3232628825.」는 **In-Editor 절 제목 뒤, 그 절의 Windows 줄 바로 뒤**에 있다(본문 위치: 절 제목 3080자 · 문장 3383자). 이 페이지 원문에서 `unity_connect`·`player_session`은 0회다(같은 본문에서 `ExampleBundleIdentifier.plist` 1회로 검색 생존).
- Unity 6000.0 `Application.persistentDataPath`: 「Windows Editor and Windows Player : Application.persistentDataPath usually points to `%userprofile%\AppData\LocalLow\<companyname>\<productname>` .」 · 「macOS Player : Application.persistentDataPath points to `~/Library/Application Support/unity.company name.product name` , but uses the Editor path if that directory already exists.」 · 「iOS : Application.persistentDataPath points to `/var/mobile/Containers/Data/Application/<guid>/Documents` .」 — https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-persistentDataPath.html
- Unity 6000.0 Log files: Windows `%USERPROFILE%\AppData\LocalLow\CompanyName\ProductName\Player.log` · macOS `~/Library/Logs/Company Name/Product Name/Player.log` · 「Player crash files (Windows only) … refer to CrashReporting.crashReportFolder .」 — https://docs.unity3d.com/6000.0/Documentation/Manual/log-files.html
- Unity 6000.0 `Windows.CrashReporting.crashReportFolder`: 「Crash reports are stored in the following location: `%TMP%\CompanyName\ProductName\Crashes`」 — https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Windows.CrashReporting-crashReportFolder.html
- Microsoft Learn, Notifications and the Notification Area: 「When an application is uninstalled, its notification area icon can still appear to the user as an option in the Notification Area Icons page in the Control Panel for up to seven days. However, any changes made there will have no effect.」 — https://learn.microsoft.com/en-us/windows/win32/shell/notification-area
- 인용문 속 경로의 `~`·`<…>`는 문서 원문 표기이고 우리 사용자 경로가 아니다. 경로 부분만 코드 서식으로 감쌌다(글자는 원문 그대로).

---

## 7. 자기 오류 기록 (이 문서를 만들며 난 죽은 프로브)

1. Unity 충돌 폴더 문서를 처음에 `ScriptReference/CrashReport-crashReportFolder.html`로 받았다. **rc 0 · 8,026바이트였지만 「page seems to be missing」 페이지**였다. 본문 추출이 0줄이라 알아챘고, `Windows.CrashReporting-crashReportFolder.html`로 다시 받아 원문을 확인했다.
2. zsh에서 구분선으로 `echo ====`를 넣었더니 `=` 확장으로 「not found」가 나며 **그 뒤 명령(로그 문서 판독)이 돌지 않았다.** 출력이 비어 재실행했다.
3. 표 검사기 첫 교정에서 **이스케이프한 `\|`를 코드 스팬 안 `|`로 오탐**했다(정상 입력에서 1건). 역슬래시 뒤 `|`를 제외하도록 고친 뒤, 정상 입력 0 · 불량 입력 9건으로 다시 교정하고 이 문서에 돌렸다.
4. `Registry.` 니들이 우리 타입 이름(`PackRegistry` 등)에 12줄 적중했다. 개수로 판정하지 않고 줄마다 열어 분류했다(1-2).
5. macOS 번들 미정의 기호 판독에서 넓은 정규식 계수가 1을 냈는데, 좁힌 목록 판독은 0줄이었다. 그 1건이 무엇인지 확인하지 않았다(`open`으로 끝나는 기호로 추정). 판정에는 좁힌 목록만 썼다.
6. 이 문서 첫 판의 인용 앵커 4개가 대상 파일에서 1회가 아니었다(`CharacterSaveStore.cs` 2회 · `TRUST_AND_SIGNING.md` 2회 · 매뉴얼 2장 3회 · `ROADMAP.md` 3회). 앵커 53개를 고정 문자열로 전수 계수해 찾았고, 1회인 앵커로 바꿨다. 같은 판을 형식 검사기에 돌려 코드 스팬 밖 물결표 5줄 · 단일 별표 2개를 잡아 고쳤다. 검사기에 HTML 태그 형태 검사를 더한 것도 이때다 — 인용문 속 꺾쇠 자리표시자가 코드 스팬 밖에 있었다.
7. (2판) 초판 2-5의 `~/Library/Saved Application State` 「양성: 같은 폴더 항목 1개 열거됨」은 **폴더가 없는데 나온 1**이었다. 초판 명령은 scratchpad에 남아 있지 않아 원인을 재현하지 못했다(오류 줄을 항목으로 센 것으로 추정). 2판은 존재 판정 명령(`~/Library` 목록 · `test -d`)의 출력으로만 판정했고, 같은 표의 `LaunchAgents` 3 · `Caches` 100도 폴더 존재를 먼저 확인한 뒤 다시 세어 유지했다.
8. (2판) 초판 0절은 「`PlayerPrefs`는 레지스트리 감사 파일에 0회」에서 「PlayerPrefs 위치는 감사 밖」으로 넘어갔다. **한 테스트 파일에서 센 0을 테스트 전체의 0으로 일반화했다.** 테스트 폴더 전체를 세지 않았다.
9. (2판) 초판 F-3·F-4의 「언제」는 클래스 주석(「쓰는 도중에 죽으면 그 PID의 임시 파일이 남는다」)과 로드 경로만 보고 적었다. 쓰기 함수와 백업 함수의 **호출자를 따라가지 않아** 물러섬 · 대체 경로 · 저장 시점 백업을 놓쳤다.
10. (2판) 초판 F-7은 한 행의 앵커를 모두 행 머리 파일 `FreezeForensicsLog.cs`로 적었는데 셋은 `FreezeForensicsPolicy.cs`에 있었다. 초판 6번의 「앵커 53개 전수 계수」가 이 셋을 어떻게 통과시켰는지는 그 스크립트가 scratchpad에 남아 있지 않아 **미확인**이다(인용 파일이 아닌 범위에서 셌을 가능성). 2판은 앵커마다 인용 파일 이름을 적고 그 파일 하나에서 셌다.
11. (2판) 초판 R-1은 `_h<해시>` 꼬리 근거로 「1차 문서의 해시 꼬리 설명」을 들면서 그 문장이 **어느 절 안인지 보지 않았다.** 2판의 첫 WebFetch 요약은 In-Editor Windows 줄을 「with hashed key names」로 옮겼는데, 원문 HTML에서 그 구절은 0회였다 — **요약 도구의 의역**이다. 절 배치는 원문 HTML의 위치로 확정했다.
12. (2판) extern 재계수 도구의 첫 자체 시험이 **FAIL**이었다 — 문자열 속 선언을 주석 칸에 넣었다. 자체 시험이 계수 실행 전에 멈춰 숫자가 나오지 않았다. 고친 뒤 PASS를 확인하고 셌다.
13. (2판) 초판 안 A의 「값 몇 가지 / a few values」는 18키 실측을 알고도 쓴 약한 표현이었다. 편집기를 여는 사람에게는 **회피로 읽힌다.**

---

## 8. 초판(미커밋) 대비 정정 기록

TEAM §5 규칙대로다. 이 문서는 미추적 신규 파일이라 옛 문장을 본문 취소선으로 되살리지 않고, 이 절에 **원문 그대로** 남긴다. 원문은 코드 블록에 넣었다 — 초판의 백틱 앵커가 산 앵커로 읽히지 않게 하려는 것이다(TEAM 「기준과 대상이 같이 낡은 스냅숏」 규칙 3). 원문은 초판(sha256 앞 16자 `ddbfb0e3f971aa40`)에서 치환 스크립트가 옮겼다(손으로 옮겨 적지 않았다). 근거 번호는 verify-change 반려 지적의 번호다(틀린 점 ①–⑤ · 6 미반영 예정 쓰기 · 7 경미).

덧붙이기만 한 곳(초판 글자는 순서대로 전부 남아 있다, 치환 스크립트가 글자 단위로 확인): 머리 측정 기준 · F-1 두 칸 · F-2 「언제」 · R-2 원칙 3 칸 · 2-5 `LaunchAgents`·`Caches` 행 · 2-6 신설 · 6절 1차 출처 · 7절 7–13 · 끝의 영향 두 줄.

### 정정 1 — PlayerPrefs 위치는 감사 안이다 (틀린 점 ①)

초판 요지: 레지스트리 감사 파일에 PlayerPrefs 0회 → PlayerPrefs 위치는 감사 밖 → 허용 명부 신설.

```text
- ★ **감사 공백**: `UserAssetImmutabilityAuditTests.레지스트리_쓰기_API가_저장소_어디에도_없다`는 `RegSetValue` 같은 **API 이름**을 막는다. `PlayerPrefs`는 그 테스트 파일에 0회 나온다. 그래서 「레지스트리에 안 씀 — 자동 감사가 지킨다」는 감사가 실제로 지키는 범위보다 넓은 주장이다.
```

```text
- 같이 바꿀 문장: 「그 넷은 전부 자동 감사가 지키고 있다」 → 레지스트리 항목은 「직접 API 0」만 감사가 지킨다. PlayerPrefs 위치는 감사 밖이다(5절 인계 ③).
```

```text
| 3 | 〃 | `자동 감사가 지키고 있다` | 레지스트리 감사는 API 이름만 본다. PlayerPrefs는 그 테스트 파일에 0회 |
```

```text
- ③ `test-engineer`(감사 설계, 제안만): `UserAssetImmutabilityAuditTests`에 **PlayerPrefs 허용 명부**를 둔다 — 「`PlayerPrefs.` 쓰기 호출은 파일 1개 · 키 1개」를 라인 단위로 잠가, 레지스트리로 가는 새 통로가 조용히 늘지 않게 한다(원칙 3 예외의 「파일 1개 · 형태 2개」와 같은 형태). 존재 단언과 부재 단언을 같은 테스트 안에 둔다(CLAUDE.md 니들 규칙). 리더가 승인하면 security가 `*AuditTests.cs`로 쓸 수 있다.
```

정정: HEAD `76504c2`의 감사 ②가 프로덕션 `PlayerPrefs`를 한 파일 · 멤버 3종으로 잠근다(`full2_edit.xml` 통과). 0절 · 4절 · 4-1 #3 · 인계 ③을 남는 공백 둘(목적 · 키 개수) 기준으로 다시 썼다. 4-1 #3의 「그 테스트 파일」은 파일 이름으로 바꿨다.

### 정정 2 — F-1 · F-3 · F-4의 「언제」 (틀린 점 ②)

```text
| **F-3** | 같은 파일 앵커 `private const string TempFileSuffix = ".writing";` · 쓰기 `new FileStream(temp, FileMode.Create, …)` + `Flush(true)` | `stickmate_character.json.<pid>.writing` | 쓰는 중인 전체 JSON | 매 저장. 정상이면 `File.Replace`가 본체로 옮겨 사라진다. **저장 도중 끊기면 고아로 남는다**(삭제 능력 0이라 앱이 치우지 않는다) | 사용자만(앱을 끈 뒤) | 대상 아님 |
```

```text
| **F-4** | 같은 파일 앵커 `$"character_save.v{version}.backup.json"` · 앵커 `if (!File.Exists(backupPath)) File.Copy(path, backupPath);` | `character_save.v<N>.backup.json` | 이 빌드보다 **새 스키마** 세이브의 원본 사본(버전별 1회) | 구버전 앱이 신버전 세이브를 읽을 때만 | 사용자만 | 대상 아님 |
```

정정: F-3은 크래시 없이도 남는다(물러섬 · 교체 전부 실패). 이름에 pid가 들어가 실행마다 쌓일 수 있다. F-4는 저장 시점에도 생긴다. F-1에는 직접 덮어쓰기 대체 경로를 덧붙였다(초판 글자 불변). 두 행의 「같은 파일」은 파일 이름으로 바꿨다.

### 정정 3 — F-7 앵커의 실재 파일 (틀린 점 ③)

```text
| **F-7** | `Platform/FreezeForensicsLog.cs` 새 슬롯 앵커 `using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))` · 이름 앵커 `"freeze-watchdog-" : "freeze-forensics-"` · 상한 앵커 `public const int SlotCount = 12;` · `public const long MaxBytesPerFile = 512L * 1024L;` |
```

정정: 이름 앵커와 SlotCount 상한 앵커는 `Platform/FreezeForensicsPolicy.cs`에서 각 1회이고 `Platform/FreezeForensicsLog.cs`에서는 각 0회다(음성 대조). 새 슬롯 앵커만 `Platform/FreezeForensicsLog.cs` 1회다. MaxBytesPerFile 상한 앵커도 `Platform/FreezeForensicsPolicy.cs` 1회다.

### 정정 4 — 5-1 판정 2 (틀린 점 ④)

```text
2. 「아무것도 남기지 않는다 / 흔적이 없다」류는 **어느 세계에서도 쓸 수 없다**(파일 8 · 로그 2가 모든 데스크톱 실행에 걸린다). 이미 금지 등재돼 있다(4-1 #7).
```

정정: 매 실행(F-6 · L-1, Windows 트레이 켬이면 O-2)과 조건부로 나눴다. 결론(흔적 없음류 금지)은 유지한다.

### 정정 5 — 안 A 문구 (틀린 점 ⑤)

```text
> 한국어: 「레지스트리에는 현재 사용자 영역에 있는 이 앱 이름의 키(`HKCU\Software\Vibelab\StickMate`)에 값 몇 가지를 저장합니다. 이 앱이 쓰는 게임 엔진(Unity)이 Windows에서 설정을 두는 기본 자리입니다. 마우스·키보드를 대신 눌러 주지 않습니다.」
```

```text
> English: "It stores a few values in the Windows registry under the current user, in a key named after the app (`HKCU\Software\Vibelab\StickMate`). That is where the Unity engine it is built on keeps its settings on Windows. It never presses keys or clicks for you."
```

```text
- 이 문장이 주장하는 것: 위치(1차 문서 + 코드), 「현재 사용자 영역」(HKCU), 「값 몇 가지」.
```

```text
- **주장하지 않는 것**: 「그 키에만」(엔진의 다른 레지스트리 경로 미확인, 1-4), 「설정만」(엔진이 세션 식별자도 쓴다, R-2), 개수.
```

```text
- 쓸 수 있는 조건: 코드 사실과 1차 문서로 성립한다. ★ 다만 이 답글은 의심하는 사람이 **자기 레지스트리 편집기를 열어 보는** 글이다. 게시 전에 Windows 실기에서 그 키가 있는지 1회 확인하기를 권한다(값 이름만 보고 값은 옮기지 않는다).
```

```text
- 같이 바꿀 조건 줄: 「레지스트리에 안 씀 ✅ 실호출 0건 + 양성 대조」 → 「레지스트리 직접 쓰기 API 0건(감사) · PlayerPrefs 쓰기 1자리 → Windows에서는 앱 이름 키에 저장(Unity 1차 문서)」.
```

정정: 엔진 값의 종류를 이름으로 인정했다. Windows 실기 값 이름 확인은 권고에서 게시 조건으로 올렸다. 개수를 적지 않아 예정 쓰기 착지 뒤에도 참이다.

### 정정 6 — 예정 쓰기 누락 (지적 6)

초판에는 이 항목이 없었다. 2-6을 새로 두고, 3절 싱크 수에 「착지 전 기준」을 붙였다.

```text
쓰기 싱크가 닫힌 집합이다(파일 4 · PlayerPrefs 1 · OS 쓰기 P/Invoke 2).
```

### 정정 7 — 경미 (지적 7)

7-a extern 수의 기준 → 코드 기준 112 / 주석 포함 113.

```text
- **P/Invoke 전수**: `extern` 선언 이름(중복 제거) **113개**.
```

7-b R-1 해시 꼬리 → 추론 표기, 근거는 코드 주석(앵커 인용, 3판에서 줄 번호 삭제 — 정정 9).

```text
값 이름은 `StickMate.GearMenu.OnboardingSeen.v1_h<해시>` 형태(코드 주석 · 1차 문서의 해시 꼬리 설명).
```

7-c plist 출처 · 0절 17키 요약 → 플레이어 또는 에디터 Play 모드로 약화, 요약에 창 위치 · 전체화면 모드 · 실행 횟수 추가.

```text
- ★ **그 키에는 우리 값보다 엔진 값이 더 많다.** 개발 머신 macOS 빌드의 PlayerPrefs plist에서 키 이름 18개를 실측했고 우리 것은 1개다. 나머지 17개는 창 크기·모니터·그래픽 품질·세션 식별자다(`unity_connect.*` 포함). 우리가 `PlayerPrefs` 호출을 지워도 이 키는 생긴다. Windows 레지스트리 실측은 0회다(이 머신에 Windows 없음).
```

```text
- **개발 머신 macOS의 plist 두 개**: `com.Vibelab.StickMate.plist`(18키, `Screenmanager` 포함 = 플레이어 창 값)와 `unity.Vibelab.StickMate.plist`(8키, `Screenmanager` 없음, `unity.cloud_userid` 있음)가 있다. 문서상 앱은 `<BundleIdentifier>.plist`이고 빌드 번들 ID는 `com.Vibelab.StickMate`이므로 앞의 것을 앱 파일로 판정했다. 뒤의 것은 에디터·배치모드 실행의 것으로 **추정**한다(`docs/marketing/TRUTH_INVENTORY.md` R5-5와 같은 판단). 사용자 기계에는 에디터가 없다.
```

7-d R-2 「우리 코드 호출과 무관」 → 키 이름 생성은 무관, 값은 우리 호출이 정할 수 있음(저장 여부 미확인). 같은 조각의 「macOS 빌드 plist」도 7-c대로 약화했다.

```text
| **R-2** | Unity 엔진(`UnityPlayer.dll`·`UnityPlayer.dylib`). 우리 코드 호출과 무관 | R-1과 **같은 키**(PlayerPrefs 저장소) | macOS 빌드 plist 실측 키 이름 17개:
```

7-e 2-5 Saved Application State → 폴더 없음, 초판 양성 대조는 죽은 프로브(7절 7번).

```text
| macOS 윈도 복원 상태(`~/Library/Saved Application State`) | 개발 머신에 이 앱 항목 0 | 양성: 같은 폴더 항목 1개 열거됨. 사용자 기계는 미확인 |
```

7-f `unity_connect.*` → 1차 문서 미발견과 추론 연결을 덧붙였다(초판 글자 불변, 원문은 아래).

```text
| 대상 아님. `unity_connect.*`는 `ProjectSettings/UnityConnectSettings.asset` 최상위 `m_Enabled: 0`인데도 생겼다(macOS 실측) |
```

### 정정 8 — 대명사 표기 (지적 밖, 자진)

TEAM 「12번째 형태」 같은 가족 보강 규칙 7(수치·앵커의 대상 파일을 대명사로 가리키지 않는다). F-3 · F-4는 정정 2, 4-1 #3은 정정 1에 원문이 있다.

```text
| **F-2** | 같은 파일 앵커
```

```text
prefs 계열 적중은 `PlayerPrefs.Set`·`PlayerPrefs.Save` 각 1(같은 파일), `PlayerPrefs.Delete` 0.
```

### 정정 9 — 3판: verify-change 조건부 통과 반영

이 절의 원문은 **2판**(sha256 앞 16자 `ce22a26940c20373`) 글자다. 2판도 커밋되지 않았다.

9-a 앵커 충돌: `docs/marketing/TRUST_AND_SIGNING.md`(판 `8811aef33d5f3ad9`)에서 짧은 형이 2회(249행 원래 대상 · 254행 marketing R9 표지 안 인용)였다. 더 긴 앵커 `레지스트리에는 쓰지 않고, 마우스`는 같은 판에서 1회이고 249행에 걸린다. TRUST 파일은 수정하지 않았다.

```text
**원문 앵커**: `레지스트리에는 쓰지 않고` (5-2 인용 블록. 문장 전체는 「레지스트리에는 쓰지 않고, 마우스·키보드를 대신 눌러 주지 않습니다.」)
```

```text
| 1 | `docs/marketing/TRUST_AND_SIGNING.md` | `레지스트리에는 쓰지 않고` | 5-2 답글. **Windows 거짓**(이 문서의 발단) |
```

9-b R-1 줄 번호: 그 앵커의 실제 줄은 16행이었다(2판은 15행). 줄 번호를 지우고 앵커만 남겼다.

```text
(`Interaction/PlayerPrefsGearMenuOnboardingSeenStore.cs` 15행, 앵커 `Unity가 <b>키 이름에서</b> 만들므로`)
```

```text
7-b R-1 해시 꼬리 → 추론 표기, 근거는 코드 주석 15행.
```

9-c 안 A 착지 조건: 「marketing 판단」을 착지 조건으로 올렸다(리더 권고).

```text
착지 뒤 예시에 「작업표시줄 원복용 무작위 값」을 더할지는 marketing 판단이다.
```

9-d 덧붙이기만 한 곳(2판 글자 불변): 머리 3판 줄 · 5-3 U-10.

---

**Windows 영향**: 레지스트리 2행(R-1 코드 사실 + 1차 문서, R-2 추정) · F-5 · L-2 · O-1 · O-2가 Windows 전용이다. 공개 5-2 문구가 Windows에서 거짓이다. Windows 실측 0 — 인계 ④가 첫 실측 칸이다. 2판: F-1 대체 경로와 F-3 ②는 코드 주석상 Windows에서 밟히는 경로다. 예정 토큰 저장소(2-6)도 Windows 전용이다. 안 A의 엔진 값 종류는 Windows 실기 확인이 게시 조건이다.
**macOS 영향**: PlayerPrefs는 plist로 같은 구조다(개발 머신 실측). 파일 F-1–F-4·F-6–F-8 · L-1이 걸린다. `persistentDataPath`가 사용자 맥에서 1차 문서대로 `unity.Vibelab.StickMate`인지는 미확인(U-6)이고, 매뉴얼 macOS 경로와 갈릴 수 있다. 2판: 개발 머신 `com.Vibelab.StickMate.plist`의 출처를 플레이어 또는 에디터 Play 모드로 약화했다(두 경우 이름이 같다). 예정 토큰 저장소는 macOS에서 0이다(명세 감사). `~/Library/Saved Application State`는 개발 머신에 폴더가 없다.
