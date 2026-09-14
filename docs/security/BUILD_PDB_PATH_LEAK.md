# 배포 DLL에 개발 PC 경로가 남는 문제 (PDB 경로) — 조사와 적용 제안

작성 `dev-platform` · 2026-09-14 · **읽기 전용 조사**: 빌드 0회 · Unity 실행 0회 · 프로덕션/테스트 `.cs` 수정 0건.
입력: `Tasklist.md` [security] 전수 점검 **L5**, 리더 판정 ⑤.
근거는 두 종류다. 하나는 이 날짜에 저장소, `Library/`, `Builds/`, 공개 zip에서 직접 잰 값이고, 다른 하나는 링크를 단 1차 문서다(§8).
**실측이 문서와 갈린 곳은 실측을 적었다**(§3 (h)). 사용자 계정명은 전부 `<이름>`으로 가렸다.

---

## 0. 결론 먼저

| 선택지 | 판정 | 한 줄 근거 |
|---|---|---|
| (a) Player Settings 「Use Deterministic Compilation」 | **불가** | 이미 켜져 있다(`useDeterministicCompilation: 1`). 그 상태로 절대경로가 나왔다 |
| (b) 비개발 빌드 = PDB 미동봉 | **불가**(이미 적용 중) | 배포물 `.pdb`는 0개다. 그래도 경로는 **DLL 안 CodeView 항목**에 있다 |
| (c) Development Build / Script Debugging 끄기 | **불가**(이미 꺼짐) | 플레이어 컴파일 입력이 `Debug=false`인데도 rsp에는 `/debug:portable`이 들어간다 |
| (d) `-pathmap`: 빌드 스크립트에서 `PlayerSettings.SetAdditionalCompilerArguments`로 **일시 설정** | **컴파일러 수준 가능(실측)** · **Unity 경유 적용은 미확인** | Unity 번들 csc에 Unity와 같은 인자 모양·같은 위치를 주면 경로가 사라진다. Unity가 이 값을 플레이어 컴파일에 넘기는지는 빌드 1회로만 닫힌다 |
| (e) `Assets/csc.rsp`에 `-pathmap` | 가능(추정) · **비권장** | 키가 기계별 절대경로다. 추적 파일에 넣으면 유출이 **공개 저장소로 옮겨 간다** |
| (f) `-debug-` 추가 | 컴파일러 수준 가능(실측) · Unity 경유 **미확인 · 비권장** | Bee는 `.pdb`를 산출물로 기대한다. 빌드 실패 위험이 있고, 에디터 줄번호를 잃을 수 있다 |
| (g) `/debug:embedded` | **비권장** | CodeView는 상대가 되지만 **PDB가 DLL 안에 실려 배포된다** |
| (h) `/debug:none` | **불가**(이 csc) | csc 4.3.1 명령줄이 CS1902로 거부했다(실측). MS 문서 표의 `none`은 MSBuild 속성값이다 |
| (i) IL2CPP 전환 | **미확인 · 범위 밖** | 조사하지 않았다. 경로 한 줄 문제에 비해 과대한 변경이다 |
| (j) 빌드 뒤 CodeView 경로 바이트 치환 | **가능(사본 실측)** · 플레이어 로드는 미확인 | 크기 불변, CodeView 문자열 구간 83바이트만 바뀐다. csc가 메타데이터를 정상으로 읽는다 |

- **원인 확정**: Unity는 `Packages/` 아래 어셈블리에만 `/pathmap:"<프로젝트 루트>"=.`를 자동으로 붙이고, `Assets/` 어셈블리에는 붙이지 않는다(§2). 같은 zip 안 패키지 DLL은 이미 상대경로다.
- **제안 1안 = (d)**: Unity가 **패키지 어셈블리에 이미 쓰는 것과 똑같은 인자**를 우리 어셈블리에도 준다. 빌드 동안만 걸고 `finally`로 원복한다(§4). 첫 빌드에서 §5로 판정한다. 효과가 없으면 **2안 = (j)**.

---

## 1. 무엇이, 어디까지 새는가 (실측)

### 1-1. `StickMate.Runtime.dll`의 CodeView(RSDS) PDB 경로

| 산출물 | CodeView PDB 경로 |
|---|---|
| 공개 릴리스 `windows-preview-20260909b` zip | `/Users/<이름>/App/StickMate/Library/Bee/artifacts/1900b0aP.dag/StickMate.Runtime.pdb` |
| 로컬 zip 5개(`20260830` · `20260902` · `20260902b` · `20260903` · `20260914-a6b3101`) | 위와 같음 |
| `Builds/Windows`(09-14 10:37) | 위와 같음 |
| `Builds/macOS`(09-09) | `/Users/<이름>/App/StickMate/Library/Bee/artifacts/200b0aP.dag/StickMate.Runtime.pdb` |

공개(비초안) 릴리스에 붙은 zip은 29개다. 그중 **검사한 것은 1개**다. 나머지 28개는 미검사지만, 같은 빌드 경로로 구웠으니 같을 것으로 추정한다.

### 1-2. 범위: 계정명 바이트 전수(UTF-8과 UTF-16LE 둘 다 셈)

| 대상 | 파일 수 | 계정명 바이트가 든 파일 |
|---|---|---|
| `Builds/Windows` | 143 | `StickMate_Data/Managed/StickMate.Runtime.dll` 1건, `dgpu-export-patch.txt` 1건 |
| `Builds/macOS` | 149 | `…/Data/Managed/StickMate.Runtime.dll` 1건, `mac-gpu-switching-plist.txt` 4건 |
| 공개 `20260909b` zip | 143 | `StickMate.Runtime.dll` 1건, `dgpu-export-patch.txt` 1건 |

- DLL 안의 1건은 CodeView 1건이다. `[CallerFilePath]`를 쓰는 소스가 0개라 IL 문자열에 들어간 경로는 없다.
  pathmap을 쓰면 앞으로 누가 `[CallerFilePath]`를 쓰더라도 함께 가려진다(Roslyn 문서, §3 (d)).
- 영수증 텍스트는 **L6**이다(리더 판정 ⑤ 「zip에 빌드 메모 동봉 금지」). 이 문서의 범위 밖이다.
  덧붙일 사실은 두 가지다. 첫째, **09-14 `a6b3101` zip에도 동봉돼 있다**(`dgpu-export-patch.txt:3` `대상: /Users/<이름>/…/StickMate.exe`).
  둘째, 경로를 쓰는 곳은 `Assets/Editor/WindowsHybridGpuExportPostprocessor.cs:105`와 `MacHybridGpuInfoPlistPostprocessor.cs:144`다.
- 다른 DLL에 보이는 절대경로는 **Unity 빌드 머신의 것**이다.
  - `UnityEngine.dll`: Windows는 `C:\build\output\unity\…`, macOS는 `/Users/<Unity 쪽 계정>/build/output/unity/…`
  - `Mono.Security.dll`, `System.ServiceModel.Internals.dll`: `/Users/<Unity 쪽 계정>/build/output/Unity-Technologies/mono/…`
  - 우리 계정명은 0바이트이고, 모든 Unity 사용자의 빌드에 같은 값이 들어간다. **조치 대상이 아니다.**
- 동봉된 `.pdb`는 0개다(`Builds/Windows`, `Builds/macOS`).
- **드러나는 것**: 개발 맥의 로그인 계정명과 저장소 위치(`~/App/StickMate`)다. 비밀값이 아니므로 등급은 보안 점검 분류대로 **낮음**이다.

---

## 2. 왜 우리 어셈블리만 절대경로인가 (원인)

**같은 zip 안 패키지 DLL은 이미 상대경로다.** 예를 들어 `Kirurobo.UniWindowController.dll`의 CodeView는 `./Library/Bee/artifacts/1900b0aP.dag/Kirurobo.UniWindowController.pdb`다.
Windows 빌드는 관리 DLL 103개, CodeView 84개이고, 그중 80개가 상대경로다(§5-3).

**Bee의 csc 작업**(`Library/Bee/1900b0aP.dag.json`, 노드 `Csc Library/Bee/artifacts/1900b0aP.dag/StickMate.Runtime.dll (+2 others)`):

```
…/NetCoreRuntime/dotnet exec …/DotNetSdkRoslyn/csc.dll /nostdlib /noconfig /shared "@…/StickMate.Runtime.rsp" "@…/StickMate.Runtime.rsp2"
```

- `.rsp`는 두 어셈블리가 같은 모양이다.
  - `-out:`은 상대경로다.
  - `StickMate.Runtime.rsp:607` `/deterministic`, `:608` `/optimize+`, `:609` `/debug:portable`
  - 이어서 `:612-616`에 `/nowarn:*` 5줄(입력 데이터의 `CustomCompilerOptions`)이 온다.
- **`.rsp2`에서 갈린다.**

| 어셈블리 | asmdef 위치 | `.rsp2` 내용 |
|---|---|---|
| `Kirurobo.UniWindowController` 등 패키지 | `Packages/…` | `/pathmap:"/Users/<이름>/App/StickMate"=.` (40바이트) |
| `StickMate.Runtime` | `Assets/…` | **0바이트** |

- 전수 결과
  - 플레이어 dag(`1900b0aP`): 패키지 **4/4** pathmap, Assets **1/1** 빈 파일
  - 에디터 dag(`1900b0aE`): 패키지 **11/11** pathmap, Assets **4/4** 빈 파일(`Assembly-CSharp-Editor`, `StickMate.Runtime`, `StickMate.Tests.EditMode`, `StickMate.Tests.PlayMode`)
  - macOS dag(`200b0aP`/`200b0aE`): 두 어셈블리 모두 같은 결과
- 컴파일 입력(`Library/Bee/1900b0aP-inputdata.json`)
  - 두 어셈블리는 `UseDeterministicCompilation=true`, `CustomCompilerOptions`, `Path`(프로젝트 루트)가 같다. **`Asmdef` 경로만 `Assets/`와 `Packages/`로 다르다.**
  - 최상위 값은 `Debug=false`, `BuildTarget=StandaloneWindows64`, `OutputDirectory=Library/Bee/PlayerScriptAssemblies`다.
- Unity 빌드 프로그램(`Unity.app/Contents/Tools/BuildPipeline/ScriptCompilationBuildProgram.exe`)
  - 사용자 문자열 힙(#US)에 `Packages`, `Assets/csc.rsp`, `.rsp2`, `,`, `,,`, `==`, `/pathmap:`, `=.`가 있다.
  - `,`→`,,`와 `=`→`==`는 pathmap 키 이스케이프 모양이다.
  - 디버그 관련 문자열은 `/debug:portable` 하나뿐이다.
- **판정**: 「Unity는 `Packages/` 아래 어셈블리에만 pathmap을 붙인다」는 **관찰 규칙**이다(15/15 + 5/5 일치).
  조건 코드 자체는 이 머신에 디컴파일 도구가 없어 **미확인**이다.
  Roslyn 문서는 pathmap이 로컬 디버그 중단점을 깨뜨린다고 경고한다. Unity가 사용자 코드의 에디터 디버깅을 지키려고 Assets를 빼는 것으로 보이지만 **추정**이다.

---

## 3. 선택지별 근거

### (a) Deterministic Compilation: 불가
- 1차 문서(MS Deterministic 절): 결정성은 *"byte-for-byte output is identical across compilations for identical inputs"*다. 입력 목록에는 *"The sequence of command-line parameters"*와 *"The current directory path"*가 들어 있다.
  즉 결정성은 **같은 입력이면 같은 바이트**를 보장할 뿐, 경로를 지우지 않는다.
- 실측
  - `ProjectSettings/ProjectSettings.asset:696` `useDeterministicCompilation: 1`
  - rsp에 `/deterministic`이 있다.
  - 배포 DLL 디버그 디렉터리에 `Repro(deterministic)` 항목이 있다.
  - 그런데도 CodeView는 절대경로다.

### (b) PDB 미동봉: 불가(이미 적용 중)
- `BuildStandalone.cs:85`와 `:515`는 `BuildOptions.None`(비개발 빌드)이고, 배포물 `.pdb`는 0개다.
- 1차 문서(Roslyn PathMap 절)는 경로가 기록되는 곳 셋 중 하나로 *"3. The path of the PDB file is embedded into a PE (portable executable) file."*을 든다. PDB를 안 실어도 **DLL 안에 경로가 남는다.**

### (c) Development Build / Script Debugging: 불가(이미 꺼짐)
- 입력 데이터가 `Debug=false`인데도 rsp는 `/debug:portable`이다. 빌드 프로그램 문자열에서 디버그 형식은 이것 하나다.
- 이 스위치로는 PDB 생성, 즉 CodeView가 꺼지지 않는다(관찰).

### (d) `-pathmap` 일시 설정: 컴파일러 수준 가능 · Unity 경유 미확인

**1차 문서**
- Unity `PlayerSettings.SetAdditionalCompilerArguments(NamedBuildTarget buildTarget, string[] additionalCompilerArguments)`: *"Sets additional compiler arguments for a build target."*
- Unity `PlayerSettings.GetAdditionalCompilerArguments(NamedBuildTarget)`는 `string[]`을 돌려준다.
- `ScriptCompilerOptions.AdditionalCompilerArguments`: *"Compiler arguments must be preceded by a dash (-) or a slash (/)."*
- Roslyn PathMap 절: *"This option maps each physical path on the machine where the compiler runs to a corresponding path that should be written in the output files."*
  경로가 기록되는 곳 셋은 `CallerFilePathAttribute`, PDB 안 소스 경로, PE 안 PDB 경로다.
  경고: *"Specifying PathMap prevents breakpoints from working in local debug builds. Only set PathMap for production or continuous integration builds."*

**실측**
- Unity 번들 csc(`4.3.1-3.22526.13`)를 Bee와 같은 방식(`@rsp @rsp2`)으로 직접 돌렸다. 탐침 소스 1파일, 세션 scratchpad, Unity 미실행이다.

| 변형 | CodeView | DLL 안 루트 바이트 |
|---|---|---|
| 기준(인자 없음) | 절대경로 `…/Library/Bee/artifacts/y.dag/base.pdb` | 있음 |
| Unity 모양 `/pathmap:"<루트>"=.`를 **rsp2**에 | `./Library/Bee/artifacts/y.dag/unityform.pdb` | **0** |
| 같은 인자 `-pathmap:"<루트>"=.`를 **rsp 안 `/debug:portable` 뒤**(`/nowarn` 자리)에 | `./Library/Bee/artifacts/y.dag/inrsp.pdb` | **0** |
| `-pathmap:<루트>/=/_/` | `/_/Library/Bee/artifacts/x.dag/pathmap.pdb` | 0 |

- 위치와 순서에 상관없이 결과는 **같은 zip 안 패키지 DLL과 같은 모양**(`./Library/…`)이다.

**미확인 셋**
1. `SetAdditionalCompilerArguments`로 넣은 값이 `BuildPipeline.BuildPlayer`의 플레이어 컴파일 rsp에 들어가는가.
   현재 값이 `{}`(`ProjectSettings.asset:686`)라 rsp에서 관찰할 수 없다. 입력 데이터 `CustomCompilerOptions`로 합류할 것으로 보이지만 추정이다.
2. 설정 변경이 배치 빌드 도중 에디터 재컴파일을 일으키는가.
3. Windows 개발 머신에서 키(드라이브 문자, 구분자)가 매칭되는가.

**판정**: 컴파일러 수준에서는 가능하다. Unity 경유는 미확인이며, 빌드 1회와 §5로 닫힌다.

### (e) `Assets/csc.rsp`: 가능(추정) · 비권장
- 1차 문서(Unity 6000.0 *Custom scripting symbols*)
  - *"place it in the root of your project's Assets folder"*
  - *"for symbols that apply for all Editor and Player code in the project"*
  - *"Unity reads this file at startup and applies it before compiling any code."*
  - *"When the Editor runs in batch mode, there's no mechanism to trigger recompilation of scripts … they must be in place from startup using a csc.rsp asset file."*
- 빌드 프로그램에 `Assets/csc.rsp` 문자열이 있다. `git check-ignore Assets/csc.rsp` 결과는 무시되지 않음, 즉 **추적 대상**이다.
- 키가 기계 절대경로이므로 **계정명이 공개 저장소에 커밋된다**. 에디터 어셈블리에도 걸려 중단점이 깨진다(Roslyn 경고).
- 생성·삭제 래퍼와 `.gitignore`를 더하면 우회할 수 있다. 다만 「배치 모드는 시작 시점에 있어야 한다」는 제약 때문에 Unity를 띄우기 전에 파일을 써야 해 부품이 늘어난다. 1안과 2안이 모두 막힐 때만 검토한다.

### (f) `-debug-`: 컴파일러 수준 가능 · 비권장
- 실측: `/debug:portable` **뒤**에 `-debug-`를 주면 CodeView 항목이 사라지고 `Repro`만 남는다.
- 위험
  - Bee 노드는 `StickMate.Runtime.dll (+2 others)`이고, 같은 폴더에 `.dll`, `.ref.dll`, `.pdb`가 있다. `.pdb`가 안 생기면 Bee가 어떻게 판정하는지는 **미확인**이다(빌드 실패 가능).
  - 정의 심볼은 `NamedBuildTarget.Standalone`에 걸린 값이 에디터 컴파일에도 적용된다(`BuildStandalone.cs:689-691` 기록). 추가 인자도 같다면 에디터 콘솔 줄번호와 중단점을 잃는다.

### (g) `/debug:embedded`: 비권장
- 1차 문서(MS DebugType 표): *"Emit debugging information into the .dll/.exe itself (.pdb file is not produced)"*
- PathMap 절은 *"The source path is embedded in a PDB file."*이라고 한다.
- 실측: CodeView는 상대 파일명이 되고 `EmbeddedPortablePdb` 항목이 추가된다.
- 결과적으로 **PDB 자체가 배포물에 실린다**(소스 문서 경로 표와 줄번호 표 포함). 지금보다 나빠진다.

### (h) `/debug:none`: 불가(이 csc)
- 실측: csc 4.3.1이 `error CS1902: /debug에 대해 잘못된 'none' 옵션입니다. 'portable', 'embedded', 'full' 또는 'pdbonly'여야 합니다.`로 거부했다.
- MS 문서 표에 있는 `none`은 MSBuild `DebugType` 속성값이다. **문서와 명령줄 실측이 갈린다.**

### (i) IL2CPP: 미확인 · 범위 밖
- `ProjectSettings.asset:688` `scriptingBackend: {}`는 기본값인 Mono이고, 배포물에 `Managed/` DLL 103개가 있는 것과 일치한다.
- IL2CPP 산출물에 경로가 남는지는 조사하지 않았다.
- 백엔드 교체는 네이티브 플러그인(UniWinC), P/Invoke, 빌드 시간 전반을 흔든다.

### (j) 빌드 뒤 CodeView 바이트 치환: 가능(사본 실측) · 플레이어 로드 미확인
- 실측(`Builds/Windows/…/StickMate.Runtime.dll`의 **사본**)
  - CodeView 경로 문자열(84바이트)을 `StickMate.Runtime.pdb` 뒤에 NUL을 채운 값으로 바꿨다.
  - 파일 크기는 같고, 달라진 바이트는 **83개로 전부 CodeView 문자열 구간**이다. GUID, age, `PdbChecksum`, `Repro` 항목은 그대로다.
  - csc가 사본을 참조로 읽었다. 공개 타입 `StickMate.Platform.AppShutdownSequence` 해석 결과는 rc=0으로 원본과 같다.
  - 계기 대조: 없는 타입은 CS0234, 참조를 빼면 CS0246이 나와 판별이 살아 있다.
- 미확인: Mono 플레이어가 치환본을 정상 로드하는가(실행 금지라 돌리지 않았다).
- 주의: macOS는 `MacHybridGpuInfoPlistPostprocessor`가 애드혹 재서명을 한다. 치환은 반드시 **서명보다 먼저** 해야 한다. `Resources/` 아래 DLL은 서명에 봉인되는 자원이다.

---

## 4. 적용 제안: `Assets/Editor/BuildStandalone.cs`

> 구현은 이 문서의 범위 밖이다. 편집 담당은 리더가 배정한다.

### 4-1. 위치
- `PerformBuild`(`:47`, macOS)와 `PerformBuildWindows`(`:484`) **양쪽 모두**에 둔다.
- 순서는 `VerifySteamEntitlementWiring()`과 `Configure*` 호출 **뒤**, 그리고 `BuildPipeline.BuildPlayer`(`:89` / `:520`)를 감싸는 `try/finally` 안이다.
- `Assets/Editor/PerfProbeBuild.cs`는 계측 전용(`Builds/PerfProbe`, 배포하지 않음)이라 필수가 아니다. 넣지 않으면 「계측 빌드와 제품 빌드의 컴파일 인자가 1개 다르다」는 사실만 적어 둔다.

### 4-2. 스케치(컴파일 미확인, 모양만)

```csharp
// 기계별 절대경로라 저장소에 남으면 안 된다 — 빌드 동안만 걸고 반드시 원복한다.
// 모양은 Unity가 패키지 어셈블리 rsp2에 쓰는 것과 같다: /pathmap:"<루트>"=.
static string[] WithProjectPathMap(string[] current, string projectRoot)
{
    string key = projectRoot.Replace(",", ",,").Replace("=", "==");   // pathmap 키 이스케이프
    var list = new List<string>(current ?? new string[0]);
    list.RemoveAll(a => a.StartsWith("-pathmap:", StringComparison.Ordinal)
                     || a.StartsWith("/pathmap:", StringComparison.Ordinal));
    list.Add("-pathmap:\"" + key + "\"=.");
    return list.ToArray();
}

// PerformBuild / PerformBuildWindows 안, BuildPlayer 자리:
NamedBuildTarget nbt = NamedBuildTarget.Standalone;
string[] before = PlayerSettings.GetAdditionalCompilerArguments(nbt);
BuildReport report;
try
{
    PlayerSettings.SetAdditionalCompilerArguments(nbt, WithProjectPathMap(before, projectRoot));
    report = BuildPipeline.BuildPlayer(options);
}
finally
{
    PlayerSettings.SetAdditionalCompilerArguments(nbt, before);
}
```

- `projectRoot`는 두 진입점이 이미 쓰는 `Directory.GetParent(Application.dataPath).FullName`이다. 뒤에 구분자가 없어서 실측한 모양 그대로 `./Library/…`가 나온다.
- 빌드 로그에는 **계정명 없이** 「pathmap 1건 적용 / 원복 완료」만 남긴다. 인자 원문을 찍으면 로그 파일로 새 유출이 생긴다.

### 4-3. 지켜야 할 것
- **원복 누락은 곧 공개 저장소 유출이다.** `ProjectSettings/ProjectSettings.asset`은 추적 파일이다(`:686 additionalCompilerArguments: {}`). 에디터가 중간에 죽어 원복을 못 한 채 설정이 저장되면 계정명이 커밋된다.
  - 제안: EditMode 감사 1건. 담당은 리더가 배정한다(test-engineer 권장).
  - 먼저 **존재 대조**로 그 파일에 `additionalCompilerArguments` 키 줄이 있음을 단언한다.
  - 그다음 같은 테스트 안에서 그 줄에 `pathmap`이 없음을 단언한다.
  - 부재 단언만 두면 조용히 초록이 되는 방향이다(CLAUDE.md 규칙).
- `SteamBuildSymbolGateAuditTests.cs:159-178`은 진입점마다 게이트 호출 위치가 `BuildPipeline.BuildPlayer` 텍스트보다 앞인지 본다(`IndexOf`). 진입점 본문 안, **게이트 호출보다 앞**에 `BuildPipeline.BuildPlayer` 문자열(주석 포함)을 쓰지 말 것. 헬퍼를 별도 메서드로 두면 영향이 없다(코드 판독, 미실행).
- macOS와 Windows 두 진입점에 **같은 헬퍼 하나**를 쓴다. 한쪽만 넣으면 다른 쪽 배포물에 경로가 남는다.

### 4-4. 2안 (j): 1안이 §5에서 「효과 없음」으로 판정될 때만
- `IPostprocessBuildWithReport`에서 `…/Managed/StickMate.Runtime.dll`의 CodeView 경로를 파일명만 남기고 NUL로 채운다.
- macOS는 `MacHybridGpuInfoPlistPostprocessor`의 재서명보다 `callbackOrder`가 **앞**이어야 한다.
- Mono 플레이어 로드를 실기에서 확인해야 한다(Windows·macOS 각 1회).

---

## 5. 확인 방법

### 5-1. 도구: 배포물 CodeView 경로 전수 계수기(읽기 전용)

아래 스크립트는 세션 scratchpad에서 교정했다(§5-3). 이 라운드 산출물은 문서 1개뿐이라 파일로 두지 않았다.
저장소에 두려면 `Tools/BuildVerify/`로 옮기는 일을 리더가 배정한다. `verify-build.py`의 「양성 대조 강제」 원칙과 같은 계열이다.

```python
#!/usr/bin/env python3
"""배포물 안 .NET DLL의 PE 디버그 디렉터리(CodeView RSDS) PDB 경로를 센다. 읽기 전용.
usage: pdb_path_census.py <빌드폴더|zip|dll> [...]
출력의 사용자 이름은 <이름>으로 가린다. 종료코드는 판정에 쓰지 않는다 — 마지막 줄들을 읽어라."""
import os, re, struct, sys, zipfile

BEE = re.compile(r'[\\/]Library[\\/]Bee[\\/]artifacts[\\/]')   # 이 프로젝트의 Bee 산출 경로(기계 무관 표지)
USER_ABS = re.compile(r'^(/Users/[^/]+/|/home/[^/]+/|[A-Za-z]:[\\/]+Users[\\/]+[^\\/]+[\\/])')
def mask(p): return re.sub(r'^(/Users/|/home/|[A-Za-z]:[\\/]+Users[\\/]+)[^\\/]+', r'\1<이름>', p)

def codeview_paths(b):
    if len(b) < 0x40 or b[:2] != b'MZ': return None
    pe = struct.unpack_from('<I', b, 0x3c)[0]
    if b[pe:pe+4] != b'PE\0\0': return None
    nsec = struct.unpack_from('<H', b, pe+6)[0]; optsz = struct.unpack_from('<H', b, pe+20)[0]; opt = pe + 24
    dd = opt + (96 if struct.unpack_from('<H', b, opt)[0] == 0x10b else 112)
    rva, size = struct.unpack_from('<II', b, dd + 6*8)
    if not rva or not size: return []
    secs, s = [], opt + optsz
    for _ in range(nsec):
        vsz, va, rsz, rp = struct.unpack_from('<IIII', b, s+8); secs.append((va, max(vsz, rsz), rp)); s += 40
    o = next((rp + rva - va for va, sz, rp in secs if va <= rva < va + sz), None)
    out = []
    for i in range((size // 28) if o is not None else 0):
        typ, sz, _, ptr = struct.unpack_from('<IIII', b, o + i*28 + 12)
        if typ == 2 and b[ptr:ptr+4] == b'RSDS':
            out.append(b[ptr+24:ptr+sz].split(b'\0')[0].decode('utf-8', 'replace'))
    return out

def items(arg):
    if not os.path.exists(arg):
        print(f'  ✗ 계기 무효: {os.path.basename(arg)} 없음'); return
    if arg.endswith('.zip'):
        with zipfile.ZipFile(arg) as z:
            for n in z.namelist():
                if n.endswith('.dll') and '/Managed/' in '/' + n: yield n, z.read(n)
    elif os.path.isdir(arg):
        for dp, _, fn in os.walk(arg):
            for f in fn:
                p = os.path.join(dp, f)
                if f.endswith('.dll') and '/Managed' in dp: yield os.path.relpath(p, arg), open(p, 'rb').read()
    else:
        yield os.path.basename(arg), open(arg, 'rb').read()

for arg in sys.argv[1:]:
    dlls = cv = ours = unity_abs = other_abs = rel = 0; hits = []
    for name, b in items(arg):
        paths = codeview_paths(b)
        if paths is None: continue
        dlls += 1
        for p in paths:
            cv += 1
            absolute = p.startswith('/') or bool(re.match(r'^[A-Za-z]:', p))
            if absolute and BEE.search(p): ours += 1; hits.append(('우리', name, mask(p)))
            elif absolute and USER_ABS.match(p): unity_abs += 1; hits.append(('Unity원본', name, mask(p)))
            elif absolute: other_abs += 1; hits.append(('기타절대', name, mask(p)))
            else: rel += 1
    label = mask(os.path.abspath(arg)).split('/StickMate/')[-1] if '/StickMate/' in arg else os.path.basename(arg)
    for k, n, p in hits: print(f'  [{k}] {n} -> {p}')
    print(f'{label}: DLL={dlls} CodeView={cv} 우리_절대경로={ours} Unity원본_사용자경로={unity_abs} 기타절대={other_abs} 상대={rel}')
    if dlls == 0 or cv == 0: print(f'  ✗ 계기 무효: DLL이나 CodeView를 하나도 못 읽었다 — 경로/형식을 잘못 준 것이다(0건을 초록으로 읽지 마라).')
```

- 「우리」의 표지는 **절대경로이면서 `Library/Bee/artifacts/`를 포함하는가**다. 계정명이나 머신에 의존하지 않는다.
  제안 모양(`=.`)을 쓰면 `./Library/…`가 되어 「상대」로 셈된다. 다른 매핑(예: `/_/`)을 쓰면 「우리」로 셈되는 거짓 빨강이 나오니, 그때는 규칙을 고친다.

### 5-2. 판정 규칙(적용 뒤 첫 빌드)
1. **본 판정**: 새 빌드 폴더(또는 zip)의 요약 줄이 `우리_절대경로=0`이고 `[우리]` 줄이 0개여야 한다.
   같은 줄에서 **`DLL≥100`과 `CodeView≥80`**으로 계기가 살아 있음을 함께 확인한다.
2. **양성 대조(같은 실행에서)**: 적용 **전** 산출물 하나를 함께 넘긴다(오늘 `Builds/Windows`의 사본 폴더, 또는 공개 `20260909b` zip).
   그쪽이 `우리_절대경로=1`이어야 한다. 1이 아니면 그 실행의 0은 **무효**다.
3. **불변 대조**: `Unity원본_사용자경로`(Windows 2 / macOS 3)와 `기타절대`(Windows 1)는 Unity 배포 원본이라 **그대로여야 정상**이다. 줄었다면 다른 DLL을 읽고 있다는 뜻이다.
4. **원인 층 확인**(§3 (d) 미확인 1을 닫는다): 빌드 직후 `grep -c pathmap Library/Bee/artifacts/1900b0aP.dag/StickMate.Runtime.rsp`가 1 이상이어야 한다(macOS는 `200b0aP.dag`).
   0이면 Unity가 인자를 플레이어 컴파일에 넘기지 않은 것이므로 **2안으로 간다**.
5. **원복 확인**: 빌드 뒤 `grep -n additionalCompilerArguments ProjectSettings/ProjectSettings.asset`가 `{}` 그대로이고, `git diff --stat -- ProjectSettings/`가 0줄이어야 한다.
6. **계정명 바이트 전수**(§1-2 방식): 계정명을 문서나 명령에 직접 쓰지 말고 `$(basename "$HOME")`로 넣는다.
   기대값은 DLL 0건이다. 남는 것은 L6 영수증뿐이어야 하고, L6 처리 뒤에는 0건이다.
   ```bash
   N="$(basename "$HOME")" python3 -c '
   import os,sys,zipfile; n=os.environ["N"]; ks=[n.encode(), n.encode("utf-16-le")]
   for a in sys.argv[1:]:
       it = ((m, zipfile.ZipFile(a).read(m)) for m in zipfile.ZipFile(a).namelist() if not m.endswith("/")) if a.endswith(".zip") else \
            ((os.path.relpath(os.path.join(d,f),a), open(os.path.join(d,f),"rb").read()) for d,_,fs in os.walk(a) for f in fs)
       tot=0; hit=[]
       for m,b in it:
           tot+=1; c=sum(b.count(k) for k in ks)
           if c: hit.append((m,c))
       print(f"{os.path.basename(a)}: 파일 {tot}개 중 계정명 포함 {len(hit)}개 {hit}")
   ' <빌드폴더|zip> ...
   ```
   파일 수(`tot`)가 0이거나 양성 대조(적용 전 산출물)가 0이면 그 결과는 무효다.

### 5-3. 오늘의 교정 결과(실측)

| 입력 | DLL | CodeView | 우리 | Unity원본 | 기타절대 | 상대 |
|---|---|---|---|---|---|---|
| `Builds/Windows` | 103 | 84 | **1** | 2 | 1 | 80 |
| `Builds/macOS` | 101 | 82 | **1** | 3 | 0 | 78 |
| 공개 `20260909b` zip | 103 | 84 | **1** | 2 | 1 | 80 |
| `StickMate.Runtime.dll` 사본(원본) | 1 | 1 | **1** | 0 | 0 | 0 |
| 같은 사본(CodeView 치환, §3 (j)) | 1 | 1 | **0** | 0 | 0 | 1 |
| 없는 경로 / 빈 폴더 | 0 | 0 | — | — | — | — (`✗ 계기 무효` 출력) |

---

## 6. 부작용

| 항목 | 판정 | 근거 |
|---|---|---|
| 크래시 스택 줄번호 | **영향 없음으로 판단 · 관측은 미확인** | 배포물 `.pdb`가 0개다. 플레이어 프레임의 파일:줄은 심볼이 있어야 나온다. pathmap은 IL, 메타데이터, 메서드 이름을 바꾸지 않는다(Roslyn 문서의 영향 3곳에 없다). `PlayerLogPolicy.cs:134-136`의 Error/Assert/Exception=ScriptOnly 프레임은 메서드 이름 기반이다. 다만 로컬 Player.log 4개에 예외 프레임이 0줄이라 「지금도 줄번호가 없다」를 로그로 직접 보지는 못했다. Mono가 CodeView 경로로 PDB를 찾는다면 **개발 맥에서 돌린 로컬 빌드**만 줄번호를 잃을 수 있다(미확인, 배포물과는 무관) |
| FreezeForensics 로그 가독성 | **영향 없음**(코드 판독) | `FreezeForensicsPolicy.FormatLine`(`:219~`)의 필드는 시각, 종류, pid, rt, frame, monitors, rect, detail이다. 스택 프레임도 소스 경로도 싣지 않는다 |
| 에디터 디버깅(중단점, 콘솔 줄 링크) | **빌드 중에만 영향 가능 · 미확인** | Roslyn 경고. 추가 인자가 에디터 컴파일에도 걸리는 구조이고 설정 중 재컴파일이 일어나면, 그동안 `Assets` 프레임 경로가 `./Assets/…` 모양으로 바뀐다. 패키지 프레임은 이미 `./Library/PackageCache/…` 모양이다(`docs/verify/runs/b2-probe_edit.log:529`). `finally` 원복 뒤 재컴파일로 돌아온다 |
| 결정성·재현 비교 | 적용 시점에 **바이트가 한 번 바뀐다** | 명령줄 인자는 결정성 입력이다(MS 문서). 적용 전과 후 빌드끼리는 MVID와 `PdbChecksum`을 비교할 수 없다. 적용 후 빌드끼리는 다시 결정적이다 |
| `Tools/CrossCompile/xcheck.sh` · `xcheck-editor.sh` · `Tools/VerifyChange/xcheck_isolated.sh` | **영향 없음**(코드 판독) | 세 스크립트 모두 원본 rsp에서 **`-`로 시작하는 줄만** 복사한다(`xcheck.sh:153`, `xcheck_isolated.sh:83`, `xcheck-editor.sh:45·59`). `/debug:portable`과 `/deterministic`은 지금도 넘어가지 않는다. 인자가 `-pathmap:`으로 플레이어 rsp(DAGP)에 남으면 복사되지만, 스크래치 DLL의 CodeView만 바뀔 뿐 에러 계수와는 무관하다. 일시 설정이라 다음 컴파일 때 rsp가 다시 구워진다 |
| `SteamBuildSymbolGateAuditTests` 순서 감사 | **영향 없음**(코드 판독, 미실행) | §4-3 |
| `Tools/BuildVerify/verify-build.py` 마커 검사 | **영향 없음** | UTF-8/UTF-16 문자열 마커를 센다. CodeView 문자열 변경과 겹치지 않는다 |

---

## 7. 미확인 목록과 닫는 방법

| # | 미확인 | 닫는 방법 |
|---|---|---|
| 1 | `SetAdditionalCompilerArguments` 값이 플레이어 컴파일 rsp에 들어가는가 | 빌드 1회 + §5-2의 4 |
| 2 | 설정 변경이 배치 빌드 도중 에디터 재컴파일이나 도메인 리로드를 일으키는가 | 그 빌드의 로그 |
| 3 | Windows 개발 머신에서 pathmap 키 매칭(드라이브 문자, 구분자) | Windows에서 빌드하게 되면 §5 |
| 4 | `Packages/`만 pathmap을 받는 조건 코드 | 관찰 규칙(20/20)으로 충분하다. 디컴파일하지 않음 |
| 5 | 치환본(2안) DLL을 Mono 플레이어가 로드하는가 | 2안을 채택할 때만 실기 확인 |
| 6 | Mono가 CodeView 경로로 PDB를 찾는가 | 영향은 개발 PC 로컬 빌드뿐이라 필요할 때만 |
| 7 | IL2CPP 산출물의 경로 잔존 | 조사하지 않음(범위 밖) |
| 8 | 공개 릴리스 zip 28개 | 미검사(같은 경로로 추정). 리더 판정 ①(이력 재작성 안 함)에 따라 회수 대상이 아니다 |

---

## 8. 플랫폼 영향

- **Windows 영향**: 공개 배포물(zip)이 Windows다. 적용하면 `StickMate_Data/Managed/StickMate.Runtime.dll`의 경로가 `./Library/…`가 된다.
  실행 동작은 바뀌지 않는 것으로 판단한다(IL 불변). 이 머신에서 Windows 실행 검증은 불가하며, 확인은 §5의 파일 판독으로 한다.
- **macOS 영향**: 같은 누출이 `Builds/macOS`에도 있다(`200b0aP.dag`). 같은 헬퍼로 `PerformBuild`에도 적용한다.
  2안을 쓸 경우에만 재서명 순서 제약이 생긴다(§3 (j)).

---

## 9. 1차 출처

- Unity 6000.0 `PlayerSettings.SetAdditionalCompilerArguments`: <https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.SetAdditionalCompilerArguments.html>
- Unity 6000.0 `PlayerSettings.GetAdditionalCompilerArguments`: <https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.GetAdditionalCompilerArguments.html>
- Unity 6000.0 `ScriptCompilerOptions.AdditionalCompilerArguments`: <https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Compilation.ScriptCompilerOptions.AdditionalCompilerArguments.html>
- Unity 6000.0 Manual, Custom scripting symbols(`csc.rsp` 위치, 적용 범위, 배치 모드): <https://docs.unity3d.com/6000.0/Documentation/Manual/custom-scripting-symbols.html>
- Unity 6000.0 Manual, Additional class library assemblies(`Assets/csc.rsp`): <https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-assemblies.html>
- C# compiler options, advanced(PathMap): <https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/advanced>
- C# compiler options, code generation(DebugType, Deterministic): <https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/code-generation>
- 저장소 실측 파일
  - `Library/Bee/1900b0aP.dag.json`, `Library/Bee/1900b0aP-inputdata.json`
  - `Library/Bee/artifacts/{1900b0aP,1900b0aE,200b0aP,200b0aE}.dag/*.rsp2`, `…/1900b0aP.dag/StickMate.Runtime.rsp`
  - `ProjectSettings/ProjectSettings.asset:686·688·696`
  - `Assets/Editor/BuildStandalone.cs:47·85·89·484·515·520·689-691`
  - `Unity.app/Contents/Tools/BuildPipeline/ScriptCompilationBuildProgram.exe`(#US 힙)
