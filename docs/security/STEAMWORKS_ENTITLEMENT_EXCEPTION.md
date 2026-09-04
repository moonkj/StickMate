# Steamworks 식별자 금지 니들 — 승인 요청 예외 설계 (I-3 / M-2)

작성 `security` · 2026-09-05 · **프로덕션 `.cs` 수정 0건 · 테스트 `.cs` 수정 0건**
근거는 전부 이 날짜에 저장소에서 직접 잰 것이거나 1차 출처를 열어 확인한 것이다.
문서에서 베낀 문장은 출처를 달았고, **실측이 문서와 갈린 곳은 실측을 적었다**(§2에 3건).

> **입력**: `docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md` §1 I-3 · §3 M-2.
> *"한 번 넓게 열면 되돌리기 어렵다 ⇒ `TASKBAR_REVEAL` 선례와 같은 형태로 열어라:
> 허용 파일 1개 · 라인 단위 재검증 · 그 밖 전면 금지."*
>
> **이 문서의 지위**: 설계이자 **승인 요청서**다. 리더 승인 전에는 아무것도 열리지 않는다.
> 구현(어댑터 `.cs` 1개 + 감사 갱신)은 다음 라운드에 `coder`에게 배정된다.

---

## 0. 결론 먼저 (한 화면)

| | |
|---|---|
| **허용 파일** | `Assets/_Project/Scripts/Store/SteamPackEntitlementSource.cs` — **딱 1개** |
| **허용 심볼** | `Steamworks`(using 1회) · `SteamAPI.Init` · `SteamAPI.Shutdown` · `SteamApps.BIsDlcInstalled` · `AppId_t` — **5개, 그 밖 전부 금지** |
| **노출 API 표면** | `PackEntitlementState Query(string packId)` **1개**(인터페이스 구현) + `internal` 자기 설치 1개 |
| **감사 방식** | 금지 니들(**열린 세계**)을 그 파일 안에서만 **허용 심볼 화이트리스트(닫힌 세계)**로 뒤집는다 |
| **되돌리기 비용** | **낮음** — 파일 1개 + asmdef 1줄 + 화이트리스트 1건 삭제. 다른 프로덕션 파일 0줄 |
| **되돌릴 수 없는 것** | **DLC appid 문자열 1개**(팩당). I-1(앱 신원)과 같은 등급으로 **동결 대상**이다 |
| **선 3(백신)** | 방향은 중립~감소로 본다. **순효과는 미확인** — 확인 방법을 §11에 적었다 |

---

## 1. 선례 대조 — 작업표시줄 예외는 정확히 무엇이었나

`docs/TASKBAR_REVEAL.md` + `Tests/EditMode/UserAssetImmutabilityAuditTests.cs:259-352, 444-489`
실측. 이번 설계는 그 형태를 **베끼는 것이 아니라 한 칸 더 조인다.**

| 축 | 작업표시줄 예외 (2026-09-02) | 트레이 호스트 창 (2026-09-03) | **이번 (스팀 어댑터)** |
|---|---|---|---|
| 허용 파일 | 1개 | 1개 | **1개** |
| 허용 형태 | **2개**(상수 선언 / 그 한 호출) | **6줄**(문자 하나까지 일치) | **심볼 5개 + 멤버 접근 형태 2종** |
| 판정 방식 | 접두+접미+**가운데**(식별자/리터럴 1개인가) | **완전 일치** | **닫힌 세계 토큰 스캔**(아래 §7-2) |
| 그 밖 파일 | 0건 등호 검사 | 0건 등호 검사 | **0건 등호 검사** |
| 죽은 예외 방지 | 대상 파일 부재 시 실패 | 대상 파일 부재 시 실패 | **대상 파일 부재 시 실패** |
| 네거티브 대조 | 5건(통과/불통과 양방향) | 4건 | **6건**(§7-4) |

### 1-1. 왜 「완전 일치 N줄」을 그대로 쓰지 않는가

트레이 예외는 **6줄이 전부**여서 문자 일치가 성립했다. 스팀 어댑터는 **함수 본문이 있는 클래스 하나**라
줄을 통째로 고정하면 주석 한 줄, 공백 하나에 감사가 빨개진다. **오탐은 감사의 죽음이다**
(이 저장소의 반복 교훈). 그래서 잠그는 대상을 **줄**이 아니라 **심볼 집합**으로 옮긴다.

### 1-2. ★ 그리고 그것이 이번 예외에서 더 강한 이유 — 금지 니들은 「열린 세계」다

작업표시줄 예외는 `ABM_*` **5종**이 이미 열거돼 있어 블랙리스트가 닫혀 있었다.
스팀은 다르다. `using Steamworks;` 한 줄이 통과하는 순간 **같은 네임스페이스의
`SteamRemoteStorage` · `SteamInventory` · `SteamUser` · `SteamFriends` ·
`SteamNetworkingSockets`가 전부 컴파일된다.** 그것들은 오늘 어떤 니들 표에도 없다.

> **⇒ 블랙리스트를 넓히는 방식으로는 이 문을 안전하게 열 수 없다.**
> 이 파일 안에서는 **허용 목록에 없는 `Steam*` 심볼이 하나라도 나오면 실패**여야 한다.
> 그것이 §7-2의 설계이고, 이번 예외가 선례보다 한 칸 더 조인 지점이다.

---

## 2. ★ 착수 전 실측 — 문서가 아니라 측정 (3건이 문서와 갈렸다)

### 2-1. ★★ asmdef 가드는 **스팀을 붙여도 계속 초록이다** (1차 출처로 확인)

`OfflineFirstNetworkAuditTests.cs:633`:
```csharp
string[] banned = { "Unity.Networking", "UnityEngine.Networking", "Unity.Netcode", "Mirror", "Steamworks" };
...
if (text.Contains(b))      // ← 대소문자 구분 Contains
```

1차 출처(`raw.githubusercontent.com/rlabrecque/Steamworks.NET/master/com.rlabrecque.steamworks.net/
Runtime/com.rlabrecque.steamworks.net.asmdef`)를 직접 열어 확인한 어셈블리 실명:

```
"name": "com.rlabrecque.steamworks.net"        ← 전부 소문자
includePlatforms: Android, Editor, LinuxStandalone64, macOSStandalone,
                  WindowsStandalone32, WindowsStandalone64      (★ iOS 없음)
autoReferenced: true
```

> **`"com.rlabrecque.steamworks.net".Contains("Steamworks")` == false.**
> ⇒ **이 감사의 「Steamworks 참조 0건」은 오늘도 앞으로도 「없다」가 아니라 「못 본다」다.**
> 이 저장소의 4번 거짓 통과 형태(*strings|grep이 UTF-16에 대해 탐지력이 애초에 0*)와 같은 가족이다.

**두 번째 구멍**: Unity asmdef 참조는 GUID 형태(`"GUID:…"`)로 저장될 수 있다.
이 저장소는 현재 이름 형태(`"Kirurobo.UniWindowController"`)지만 그건 에디터 체크박스 하나에 달려 있다.
**GUID로 저장되면 이름 스캔은 영원히 0건이다.**

**세 번째 구멍**: `autoReferenced: true` + `Assets/Editor/`에는 asmdef가 없다
(⇒ 예약 어셈블리 `Assembly-CSharp-Editor`). **스팀 패키지를 들이는 순간 에디터 스크립트 8개는
asmdef 한 줄도 안 고치고 스팀 API를 부를 수 있다.**

### 2-2. ★ 세 감사가 **같은 사각지대**를 공유한다 — `Assets/Editor/` 8파일

| 감사 | 스캔 루트 | `Assets/Editor/` |
|---|---|---|
| `OfflineFirstNetworkAuditTests` | `Assets/_Project/Scripts` | **안 봄** |
| `EntitlementAuditSource`(감사 3종 공용) | `Assets/_Project/Scripts` | **안 봄** |
| `UserAssetImmutabilityAuditTests` | `Assets/_Project/Scripts` | **안 봄** |

실측: `Assets/` 아래 `_Project/Scripts` **밖** `.cs`는 **8개**
(`BuildStandalone.cs` · `WindowsHybridGpuExportPostprocessor.cs` · `SceneBootstrapper.cs` ·
`PerfProbeBuild.cs` · `MacWindowEnumerationDiagnostic.cs` · `AccessoryDefMigration.cs` ·
`AutoPlayOnLaunch.cs` · `_TempLineUvProbe.cs`).

출하 빌드에 안 나가므로 **매출 위험은 낮다.** 그러나 **빌드 후처리가 엔타이틀먼트를 흉내 내는 경로**가
아무 저항 없이 열리고, 그 사실을 아무 감사도 말해 주지 않는다. **스캔 루트에 8파일을 더하는 비용은 0에 가깝다.**

### 2-3. ★ `EntitlementFailOpenAuditTests`의 보류는 **이미 풀렸는데 명부의 사유가 낡았다**

`TestClaimExpiryAuditTests.cs:703-716`의 항목은 *"유료 권한(C층) 코드가 **오늘 0줄**"*이라고 적는다.
**거짓이다.** `Core/PackEntitlement.cs`에 `PackEntitlementState`(enum) · `IPackEntitlementSource` ·
`NullPackEntitlementSource` · `PackEntitlements`가 실재하고, `Core/StickPackManifestSO.cs`에
`PackEntitlementRef`가 있다. ⇒ `DetectPolicyTypes`가 **0건이 아니므로 `Assert.Ignore` 분기는 이미 안 돈다.**
(파일 자신의 클래스 문서 27-42행이 2026-09-03에 그 사실을 이미 기록했다. **명부만 안 따라왔다.**)

명부의 대조 방향은 `actual ⊆ expected` 한쪽뿐이라(`:932-938`) **이 낡음은 조용하다.**
⇒ **이번 라운드에 함께 고칠 항목**(§7-5).

### 2-4. 오늘의 배선 현황 (기대값의 출처)

```
PackEntitlements 계열을 언급하는 프로덕션 파일 = 2개
  Core/PackEntitlement.cs      (선언)
  Core/PackRegistry.cs         (TryFindPackOfItem 문서 + PackEntitlementRef 보관)
_source 를 바꾸는 창구 = 1개, internal      (SetTestOverride / ClearTestOverride)
InternalsVisibleTo = 1건       (StickMate.Tests.EditMode — Scripts/AssemblyInfo.cs:8)
StickMate.Runtime.asmdef references = 1개   ("Kirurobo.UniWindowController")
Assets 아래 asmdef = 3개
```

---

## 3. 허용 파일 — 1개

```
Assets/_Project/Scripts/Store/SteamPackEntitlementSource.cs
```

### 3-1. 왜 이 경로인가 (셋 다 이유가 있다)

1. **`Assets/_Project/Scripts/` 아래여야 한다 — 협상 불가.**
   §2-2가 보인 대로 세 감사 전부 이 루트만 본다.
   `Assets/Plugins/`나 새 루트에 두면 **세 감사가 동시에 눈이 멀고, 그 0건은 성공한 0건과 똑같이 생겼다.**
2. **`Platform/Windows/`가 아니다.** `ENTITLEMENT_CONTRACT §E-11`: *"엔타이틀먼트 조회 자체는 플랫폼
   분기가 없다(스팀이 양 플랫폼에 같은 API를 준다)."* 스팀은 OS가 아니라 **판매 채널**이고,
   `PackStoreChannel`은 이미 채널 4종을 열거한다. 두 번째 채널(MS 스토어) 어댑터가 옆에 앉을 자리다.
3. **`Core/`가 아니다.** `Core/PackEntitlement.cs`는 **정책**(3상태·단조·부정 캐시 없음)이고
   어댑터는 **사실 조회**다. 둘을 한 파일에 두면 `FullscreenSuspendPolicy.cs` 사고
   (정책이 플랫폼 전용 폴더에 갇혀 반대편이 물리적으로 호출 불가)의 거울상이 된다.

### 3-2. 플랫폼 게이트 — `#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX`

**Windows만 열지 마라.** 근거 둘:

- **(가) 측정이 사라진다.** `Tools/CrossCompile/xcheck.sh`는 `StickMate.Runtime.rsp`를 win·osx
  **양쪽**으로 컴파일한다. `UNITY_STANDALONE_WIN`만 걸면 `xcheck.sh osx`가 이 파일을 **한 줄도 안 본다** —
  활성 타깃이 Windows인 지금(§`CRITICAL_PATH` §0-3 (2)) macOS 회귀가 조용히 쌓이는 M-12를 그대로 밟는다.
- **(나) macOS도 같은 API를 쓴다.** Steamworks.NET asmdef가 `macOSStandalone`을 포함한다(§2-1 실측).
  1.0 출하는 Windows 단독이지만 **코드 경로는 지금 열어 두고 macOS는 「미검증」으로 표기**하는 것이
  「나중에 맞추자」보다 싸다.

**모바일**: `#if`가 막는다. 다만 Steamworks.NET asmdef의 `includePlatforms`에 **iOS가 없다**(§2-1) —
iOS 타깃에서 참조 미해소가 컴파일 오류가 되는지는 **미확인**(1.0 범위 밖, 기록만).
회피책이 필요해지면 `versionDefines`로 `STEAMWORKS_NET`을 얻어 `#if STEAMWORKS_NET && (...)`로 이중 게이트.

### 3-3. 어댑터를 **직접 P/Invoke로 손수 만들지 않는 이유** (검토했고 기각했다)

`[DllImport("steam_api64")]`로 플랫 C API(`SteamAPI_ISteamApps_BIsDlcInstalled` 등)를 직접 부르면
문자열 `Steamworks`가 **한 번도 등장하지 않아** 니들 예외 자체가 필요 없어진다. **그래서 기각한다.**

1. **그건 예외를 없애는 것이 아니라 감시를 없애는 것이다.** 작업표시줄 예외가 상수 선언
   `private const uint ABM_SETSTATE = …`를 **일부러 허용**한 이유가 정확히 이것이다
   (`UserAssetImmutabilityAuditTests.cs:279-280`): *"Win32 이름을 그대로 쓰지 않고 다른 이름으로 감추면
   이 감사가 물 대상 자체가 사라진다."*
2. **조용한 실패 표면이 크다.** 플랫 API는 `SteamAPI_SteamApps_v008` 같은 **버전 접미사**를 요구하고,
   SDK 판이 바뀌면 접미사가 바뀐다. 틀리면 널 인터페이스를 받고 → **정상 구매자 전원이 NotOwned**가 된다.
   이 설계 전체가 막으려는 사고를 우리 손으로 만드는 것이다.
3. 정의서: **플랫폼이 이미 해 주는 것을 우리가 다시 만들지 마라.**

---

## 4. 허용 심볼 — 닫힌 세계 5개

```
Steamworks            네임스페이스.  허용 형태: "using Steamworks;"  ← 정확히 이 한 줄, 파일당 1회
SteamAPI              허용 멤버: Init, Shutdown                     ← 그 둘 외 전부 금지
SteamApps             허용 멤버: BIsDlcInstalled                    ← 그 하나 외 전부 금지
AppId_t               DLC appid 값 타입 (new AppId_t(uint))
SteamPackEntitlementSource   우리 타입 자신의 이름(Steam으로 시작하므로 목록에 명시해야 한다)
```

### 4-1. 이 파일 **안에서도** 금지되는 것 — 화이트리스트는 파일 통행권이 아니다

| 금지 | 왜 |
|---|---|
| `SteamAPI.RestartAppIfNecessary` | `E-5`가 판정 보류로 남긴 항목. ★ **여기서 판정한다: 쓰지 않는다.** 이유가 둘로 늘었다 — (ㄱ) 상주 앱을 재시작시킨다(E-5 원문), (ㄴ) **사용자가 안 켠 스팀을 우리가 켜는 행위**라 **원칙 2(비침해)** 위반이다. E-5는 (ㄴ)를 안 적었다 |
| `SteamAPI.RunCallbacks` / `Callback<>` / `CallResult<>` | `BIsDlcInstalled`는 동기 접근자라 콜백 펌프가 **필요 없다**. 펌프를 도입하면 매 프레임 도는 코드가 하나 늘고, 상주 앱에서 그건 공짜가 아니다 |
| `SteamApps.BIsSubscribedApp` / `BIsSubscribed` / `GetDLCCount` / `BGetDLCDataByIndex` | Valve 자신의 경고(`SECURITY_MODEL §5-1`): `GetDLCCount`는 **64에서 상한**, `BIsSubscribed`는 DRM/재시작 사용 시 **항상 true**. ★ `BIsSubscribedApp`은 §6-4의 실기 결과에 따라 **한 심볼만** 열 수 있다 — 그 절차를 미리 적어 둔다 |
| `SteamRemoteStorage` 일체 | `I-6`: Steam Cloud는 **이미 결정된 항목**(Auto-Cloud, 코드 0줄, 1.0 밖). **다시 열지 마라** |
| `SteamInventory` 일체 | `E-13`: 소모품(회복제)은 재고형으로 사용자 확정됐고 **당일 소멸**이라 스토어 재고 API가 필요 없다 |
| `SteamUser` · `SteamFriends` · `SteamUserStats` · `SteamNetworking*` · `SteamHTTP` · `SteamEncryptedAppTicket` | 전부 이 앱 범위 밖. 특히 `SteamFriends`/`SteamUser`는 **개인정보 표면**을 새로 만든다 |
| `Steam DRM` 래퍼 | `E-5`·`S-4-3`에서 이미 기각(F2P라 보호할 exe가 없고, 래퍼가 Authenticode를 무효화한다) |
| **나머지 금지 니들 24종 전부**(`UnityWebRequest`·`System.Net`·`HttpClient`·…) | 화이트리스트는 **니들 단위**다. 실측: `ForbiddenNeedles`는 **25종**(전송 23 · 상태조회 2)이고, 이 파일에 허용되는 것은 `Steamworks` **1종뿐**이라 **나머지 24종은 예외 없이 잡힌다**(그 성질은 `NegativeControl_라인_검증자는_같은_파일의_다른_위반을_숨기지_않는다`가 이미 증명한다) |
| 문자열 리터럴 `"pack."` 또는 팩 아이디 | **원칙 4.** 어댑터에 팩 이름이 하나라도 박히면 7번째 팩이 `.cs` 수정을 요구하게 된다 |
| `File.` 쓰기 계열 / `PlayerPrefs` | **`E-4-a`.** 어댑터는 디스크에 **아무것도 쓰지 않는다**. 캐시 파일 하나가 §E-4가 제거한 표적을 되살린다 |

---

## 5. 노출 API 표면 — 정확히 1개 + 설치 창구 1개

### 5-1. 어댑터가 밖에 보이는 것

```csharp
public sealed class SteamPackEntitlementSource : IPackEntitlementSource
{
    public PackEntitlementState Query(string packId);      // ← 유일한 public 멤버
}
```

**`bool`을 돌려주는 멤버를 하나도 만들지 않는다**(`§E-1-a`). 내부 지역변수로 `bool installed = …`를
받는 것은 위반이 아니다 — `EntitlementFailOpenAuditTests`의 V2는 **접근제한자로 시작하는 선언 줄**만 본다.
★ 그래서 **private 헬퍼 이름에 `Entitle`/`Owned`/`Dlc`/`Ownership`을 쓰면 안 된다**
(예: `private static bool TryQueryDlc(…)`는 잡힌다). **그게 맞는 동작이다** — 그 이름의 bool 함수는
이 계약이 막으려는 형태 그 자체다.

### 5-2. 설치 창구 — `SetTestOverride`를 재사용하지 마라

`PackEntitlements._source`를 바꾸는 창구는 오늘 `internal SetTestOverride` 하나다.
프로덕션 어댑터가 같은 어셈블리라 **부를 수는 있다.** 그러나 부르면 안 된다 —
`UnlockSwitchScopeAuditTests`의 실패 메시지가 그 형태를 이름으로 경고한다
(*"특히 테스트 강제값(SetTestOverride 계열)을 프로덕션이 부르면…"*).

**신설**(`Core/PackEntitlement.cs`, `coder` 배정):

```csharp
internal static void UseSource(IPackEntitlementSource source)
```
- **`internal`**(§E-6-c). 어셈블리 밖에서 못 부른다.
- **1회 성공.** 이미 기본값(`NullPackEntitlementSource`)이 아니면 **무시하고 `Debug.LogError`**.
  출처를 갈아끼우는 경로를 없앤다.
- **`null` 거부.**
- **단조 래치를 비우지 않는다**(`SetTestOverride`와 다른 점). 기동 1회뿐이라 비울 것이 없고,
  비우는 능력을 프로덕션에 두면 「확인된 보유를 지우는 함수」가 존재하게 된다.

**호출자**: 어댑터 자신이 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`로
**스스로 설치한다**(이 저장소 선례 3건: `ReservedBarRevealDirector` · `RenderQualityTuner` · `PlayerLogPolicy`).
`#if` 밖 플랫폼에서는 파일이 존재하지 않으므로 `NullPackEntitlementSource`가 그대로 남는다 = **전부 Unknown**.

> ★ **감사 항목**: `_source`에 대입하는 창구가 **정확히 2개**(`SetTestOverride`, `UseSource`)이고
> **둘 다 `internal`**인가. 새 창구는 이름에 `Test`가 없어 §E-6-c의 public 래칫에 **안 걸린다** —
> 그래서 별도 항목이 필요하다. 이 문단이 없으면 탐지 밖의 설치 창구가 생긴다.

---

## 6. 3상태 계약과 어떻게 맞물리는가

`game-architect`가 *"계약 자체는 잘 서 있다"*고 판정했다. **동의한다. 어댑터는 계약을 고치지 않고 채운다.**

### 6-1. 상태 매핑 — NotOwned를 낳는 자리는 **한 곳뿐**이다

| 상황 | 답 | 근거 |
|---|---|---|
| `packId`가 비었다 | `Unknown` | 기존 `PackEntitlements.StateOf` 첫 줄과 같은 논리(물을 대상이 없다) |
| `PackRegistry.Find(packId)` == null | `Unknown` + `LogError` | 우리 데이터 결함이지 사용자 미보유가 아니다 |
| Steam 채널 `entitlementId` 항목이 **없다** | `Unknown` | 매니페스트 주석: *"비면 그 채널에서는 팔지 않는다 — 빈 문자열을 「무료」로 읽지 않는다"* |
| `uint.TryParse(entitlementId)` 실패 | `Unknown` + `LogError` | 같은 이유. **조용히 실패하지 않는다** |
| `SteamAPI.Init()` == false | ★ **`Unknown`** | **§E-1의 심장.** 스팀 미기동은 「안 샀다」가 아니다 |
| 예외(`DllNotFoundException` 등) | `Unknown` | 스팀 밖 직접 실행 = **부팅 자동 실행**에서 실재하는 경로 |
| `BIsDlcInstalled` == **true** | `Owned` | |
| `BIsDlcInstalled` == **false** | **`NotOwned`** | ★ **NotOwned를 낳는 유일한 자리.** Init 성공 뒤에만 신뢰한다 |

**`Query`는 절대 예외를 던지지 않는다.** 전체를 `try/catch`로 감싸고 어떤 예외도 `Unknown`으로 접는다.

### 6-2. E-3(재시도) — 어댑터가 기억하는 것은 **Init 성공 여부 하나뿐**이다

- `Init()`이 성공했으면 다시 부르지 않는다.
- 실패했으면 **최소 60초 뒤** 다음 조회에서 재시도(§E-3-a 권고 간격과 같은 값).
  **타이머를 돌리지 않는다** — 묻는 쪽이 물을 때 그 자리에서 판단한다(상주 앱에 도는 코드를 안 늘린다).
- **답(Owned/NotOwned/Unknown)은 어댑터가 기억하지 않는다.** 단조 래치는 `PackEntitlements` 쪽에만 있고
  그것이 §E-3-b(단조) · §E-3-a(부정 캐시 없음)를 이미 구조로 보장한다.
- **`_initialized` 플래그는 §E-4 위반이 아니다** — 소유 판정이 아니고 디스크에 나가지 않는다.

### 6-3. E-2-a(문구 분리) — 어댑터가 건드리지 않는다

사유 키는 `PackEntitlements.ReasonKey`가 이미 3갈래로 돌려준다
(`pack.state.owned` / `pack.state.notowned` / `pack.state.unverified`).
**어댑터는 문자열을 하나도 만들지 않는다**(로그 제외). 화면 문구는 `ux-designer` 소관.

### 6-4. ★ 이 설계에서 가장 비싼 미확인 — 「installed」와 「owned」는 같지 않다

Valve 문서 원문(`SECURITY_MODEL §5-1` 인용): `BIsDlcInstalled` —
*"Should only be used for **simple client side checks** — not intended for granting in-game items."*
이름이 말하는 것은 **설치**이지 **소유**가 아니다.

우리 6팩은 **콘텐츠가 본편에 이미 들어 있는 「라이선스만」 DLC**다
(MS 스토어의 durable add-on과 같은 모양 — `SECURITY_MODEL §5-2`가 *"이게 정확히 우리 모양"*이라고 확인).
즉 **depot이 비어 있다.**

> **미확인**: 빈 depot을 가진 DLC를 `BIsDlcInstalled`가 `true`로 보고하는가?
> **`false`라면 정상 구매자 전원이 `NotOwned`가 된다** — 이 문서 전체가 막으려는 사고가
> 정확히 그 형태로 발생한다. 소스 대조로는 못 가른다. **스팀 실기가 필요하다.**

**미리 정해 두는 분기**(그날 급하게 정하지 않기 위해):
- `true`로 보고된다 → 설계 그대로. 심볼 4개 유지.
- `false`로 보고된다 → **선택지 (ㄱ)** DLC에 **아주 작은 더미 depot 파일 1개**를 넣어 실제로 설치되게 한다
  (코드 0줄, 파트너 사이트 작업. **이쪽을 먼저 검토한다**) /
  **(ㄴ)** 허용 심볼에 `SteamApps.BIsSubscribedApp` **하나만** 추가한다.
  (ㄴ)를 고르면 이 문서 §4와 감사 목록을 **같은 diff에서** 갱신하고, Valve의 경고
  (*"Only use this if you need to check ownership of another game related to yours"*)에 대한 반박을
  그 자리에 적는다 — **DLC appid는 「우리와 관련된 다른 앱」의 정의에 들어간다**는 것이 그 반박이다.

---

## 7. 감사 설계 — 무엇을 어떻게 다시 재는가

> **이번 라운드는 설계만 쓴다.** 아래는 `coder`가 구현할 **명세**다.
> 테스트 이름과 단언 내용을 그대로 적어 두는 이유: 다음 사람이 「대충 비슷하게」 구현하면
> 이 문서의 보장이 통째로 사라지기 때문이다.

### 7-1. 신규 파일 1개

`Assets/_Project/Scripts/Tests/EditMode/SteamEntitlementAdapterAuditTests.cs`

리플렉션 0줄 · 정규식 0줄(활성 빌드 타깃 사각지대 / .NET `\b`가 한글을 낱말로 세는 함정 —
`EntitlementAuditSource`의 관례를 그대로 따른다).

### 7-2. ★ 본체 — 닫힌 세계 토큰 스캔

```
[Test] 스팀_어댑터는_정확히_한_파일이고_승인된_심볼만_쓴다()

(0) 비공허성:  프로덕션 .cs >= EntitlementAuditSource.MinProductionFileCount
(1) 파일 집합 등호:
      프로덕션 전량에서 「낱말 단위로 Steam 으로 시작하는 식별자 또는 AppId_t」를 가진 파일 집합
      ==  { "SteamPackEntitlementSource.cs" }
      · 많으면  → 예외가 번졌다
      · 적으면(0개) → 예외가 죽었다.  그러면 화이트리스트·이 테스트·asmdef 참조를 함께 지워라
        (2026-08-30 SetWindowPos 처리와 같다)
(2) 심볼 화이트리스트(그 파일 안):
      Steam 으로 시작하는 모든 낱말 ∪ {AppId_t}  ⊆
        { Steamworks, SteamAPI, SteamApps, AppId_t, SteamPackEntitlementSource }
      목록 밖 심볼 1개라도 → 실패.  ← RestartAppIfNecessary·RemoteStorage·Inventory가 여기서 죽는다
(3) 멤버 접근 형태:
      "SteamAPI."  뒤의 식별자 ⊆ { Init, Shutdown }
      "SteamApps." 뒤의 식별자 ⊆ { BIsDlcInstalled }
(4) using 형태:  "using Steamworks;" 가 정확히 1회, 그리고 Steamworks 라는 낱말의 총 등장 횟수도 1
      (→ Steamworks.SteamUser.… 같은 정규화 우회를 막는다)
(5) 원칙 4:  그 파일에 "pack." 리터럴 0건
(6) E-4  :  그 파일에 File. 쓰기 계열 / PlayerPrefs 0건
```

**주석은 제거한 뒤 본다**(`EntitlementAuditSource.StripComments`). 이 문서와 어댑터 클래스 문서가
금지 심볼을 **설명하려고** 인용할 것이기 때문이다 — 정직하게 적을수록 빨개지는 감사는 곧 꺼진다
(`UserAssetImmutabilityAuditTests.cs:380-395`가 같은 이유로 이미 같은 결정을 내렸다).

### 7-3. 설치 창구 잠금

```
[Test] 소유_출처를_바꾸는_창구는_정확히_둘이고_둘_다_internal이다()
      Core/PackEntitlement.cs 에서 "_source =" 에 대입하는 메서드 선언 집합
      == { SetTestOverride, UseSource },  둘 다 internal 로 시작
      그리고 UseSource 를 부르는 프로덕션 파일 == { SteamPackEntitlementSource.cs }
```

### 7-4. 네거티브 대조 — 6건. **하나라도 빠지면 위 초록은 아무것도 증명하지 않는다**

| # | 방향 | 가짜 입력 | 기대 |
|---|---|---|---|
| 1 | 양성 | `SteamRemoteStorage.FileWrite(...)` | **잡힌다**(허용 목록 밖 심볼) |
| 2 | 양성 | `SteamAPI.RestartAppIfNecessary(appId);` | **잡힌다**(허용 멤버 밖) |
| 3 | 양성 | 어댑터 **밖** 파일에 `using Steamworks;` | **잡힌다**(파일 집합 등호) |
| 4 | 양성 | `Steamworks.SteamUser.GetSteamID()` (정규화 회피) | **잡힌다**(4번 규칙) |
| 5 | 음성 | 실제 허용 4형태 | **통과한다**(오탐이면 감사가 꺼진다) |
| 6 | 음성 | 주석 안의 `SteamInventory` 언급 | **통과한다**(주석은 배선이 아니다) |

### 7-5. 기존 감사 4곳의 변경 — **니들을 지우지 마라**

| 파일 | 무엇을 | 왜 |
|---|---|---|
| `EntitlementFailOpenAuditTests` | `스토어_SDK가_들어오면_이_경보가_먼저_울린다`를 **지우지 말고 개작한다** → `스토어_SDK는_승인된_어댑터_한_파일에서_승인된_심볼만_쓴다`(§7-2를 호출) | ★ **원 작성자의 지시 ②(*"이 경보 메서드를 지우세요"*)를 여기서 뒤집는다.** 지우면 `SteamRemoteStorage`·`SteamInventory`·`SteamUser`를 지키는 것이 **하나도 남지 않는다.** 작업표시줄 선례도 `ABM_*` 금지를 지우지 않고 예외를 옆에 붙였다. **이름을 바꾸면 명부(`TestClaimExpiryAuditTests`)의 `Companion`이 시끄럽게 빨개진다 — 같은 diff에서 갱신한다** |
| `TestClaimExpiryAuditTests` | `EntitlementFailOpenAuditTests` 항목의 `Companion` 갱신 + `Why`의 *"C층 코드가 오늘 0줄"* **정정**(§2-3: 이미 거짓) | 명부 대조가 `actual ⊆ expected` 한 방향뿐이라 이 낡음은 조용하다 |
| `OfflineFirstNetworkAuditTests` | ① 화이트리스트 1건 추가(아래) ② `전송계열_화이트리스트는_현재_비어_있다`를 **「승인된 1건 등호」로 개작** ③ asmdef 스캔을 **대소문자 무시**로 + 양성 대조 추가 ④ `StickMate.Runtime.asmdef`의 `references` **배열 등호** 검사 추가 ⑤ 스캔 루트에 `Assets/Editor/` 추가 | ②는 §7-6, ③④는 §2-1, ⑤는 §2-2 |
| `UnlockSwitchScopeAuditTests` | **변경 없음**(확인만) | 어댑터는 `EquipmentDebugUnlock`을 **한 글자도 참조하지 않는다**(§E-6-b). 그 성질은 §7-2 (2)의 화이트리스트가 자동으로 강제한다 |

**추가할 화이트리스트 항목**(형식은 기존 `HardwareReactionDirector` 항목과 같다):
```
FileName        = "SteamPackEntitlementSource.cs"
AllowedNeedles  = { "Steamworks" }              ← 이 하나뿐. 나머지 24종은 이 파일에도 그대로 적용
RequiresConsentGate = false
LineVerifier    = line => line.Trim() == "using Steamworks;"    ← 완전 일치
Reason          = 유료 6팩 엔타이틀먼트 조회. 허용 심볼 5개는 docs/security/
                  STEAMWORKS_ENTITLEMENT_EXCEPTION.md §4, 라인 단위 재검증은
                  SteamEntitlementAdapterAuditTests 가 맡는다.
```

### 7-6. ★ `전송계열_화이트리스트는_현재_비어_있다`를 **니들 종류를 바꿔서 초록으로 만들지 마라**

가장 편한 길은 `Steamworks`를 `NeedleKind.Transmission`에서 새 종류(`StoreEntitlement` 같은)로 옮겨
그 테스트를 **건드리지 않고 초록으로 유지**하는 것이다. **기각한다.**

그건 **지표를 우리가 원하는 답으로 고치는 것**이고, 이 저장소가 반복해 당한 형태다.
그리고 정직하게 말하면 **오프라인 보장의 모양이 실제로 바뀐다**:

| | 전환 전 | 전환 후 |
|---|---|---|
| 우리 **매니지드** 코드에 네트워크 API | 0건 | **0건 (변화 없음 — 허용 심볼 5개에 네트워크 API가 없다)** |
| 우리가 링크하는 **네이티브 모듈** | 없음 | `steam_api64.dll` — **내부를 우리 스캐너가 볼 수 없다** |
| 방화벽 권한 요구 | 0 | **미확인**(§11-3에 확인 방법) |

⇒ **테스트를 「비어 있다」에서 「승인된 정확히 1건」으로 개작하고**, 실패 메시지에 위 표를 그대로 적는다.
그러면 두 번째 전송 예외가 들어오는 날 **여전히 한 줄로 빨개진다.** 그것이 그 테스트의 값이다.

### 7-7. asmdef — 이름 스캔을 믿지 말고 **등호**로 잠근다

§2-1의 구멍 둘(대소문자 · GUID) 때문에 「금지 문자열 부재」로는 못 잰다.

```
[Test] 런타임_어셈블리의_참조는_알려진_목록과_정확히_같다()
      StickMate.Runtime.asmdef 의 references 배열
      ==  { "Kirurobo.UniWindowController", "com.rlabrecque.steamworks.net" }   (승인 후)
      · GUID 형태("GUID:"로 시작)가 하나라도 있으면 실패 + "이름 형태로 저장하라" 안내
[Test] 다른_asmdef는_스팀을_언급하지_않는다()
      Assets 아래 asmdef 중 StickMate.Runtime.asmdef 를 뺀 전부에서
      "steam"(대소문자 무시) 0건
      · 이것이 Steamworks.NET 을 Assets/ 안에 푸는 설치 형태를 막는다
        (그 패키지 자신의 asmdef 이름이 com.rlabrecque.steamworks.net 이다)
[Test] NegativeControl_asmdef_스캐너는_소문자_이름을_실제로_찾아낸다()
      가짜 텍스트 "com.rlabrecque.steamworks.net" 을 흘려 잡히는지  ← 오늘 이게 없어서 못 봤다
```

**설치 형태 권고: UPM(`Packages/`) + 태그/커밋 고정.** 근거는 위 두 번째 테스트가 강제하는 형태이고,
서드파티 소스 200여 개가 우리 트리에 들어오지 않는다는 부수 이득이 있다.
**미확인**: UPM 패키지에 네이티브 재배포 바이너리(`steam_api64.dll`)가 포함되는지 —
`dev-platform` 확인 필요. 포함되지 않으면 `Assets/Plugins/`에 바이너리만 놓는다(`.cs` 0개, `.asmdef` 0개라
위 감사에 영향 없음).

---

## 8. 원칙 3 · 원칙 4와 충돌하는가

### 8-1. 원칙 3(유저 자산 불변) — **충돌하지 않는다.** 그리고 그 이유가 작업표시줄 예외와 다르다

작업표시줄 예외는 **실제로 시스템 상태를 썼다**(그래서 예외 승인이 필요했다).
스팀 어댑터는 **아무것도 쓰지 않는다**:

| 축 | 어댑터 |
|---|---|
| 남의 파일/창/아이콘 | **0회 접촉** |
| 시스템 전역 설정 | **0회 쓰기** |
| 우리 자신의 디스크 | **0회 쓰기**(§E-4-a, §7-2 (6)이 감사한다) |
| 레지스트리 | 0회 (PlayerPrefs 금지) |
| 다른 프로세스를 띄우는가 | **아니다** — `RestartAppIfNecessary`를 금지했다(§4-1). `Init()`은 이미 도는 클라이언트를 요구할 뿐 띄우지 않는다 |
| `steam_appid.txt` | **개발용 파일이고 출하 빌드에 넣지 않는다**(넣으면 그 appid로 뜬다). 우리가 쓰지도 만들지도 않는다 |

> **⇒ 이 예외는 원칙 3의 예외가 아니다.** 원칙 3의 승인된 예외는 여전히 **작업표시줄 1건뿐**이고,
> 이 문서가 그 숫자를 늘리지 않는다. **여는 것은 「감사 니들」이지 「원칙」이 아니다.**
> 이 구분이 흐려지면 다음 사람이 「원칙 3 예외 2건」이라고 읽고 세 번째를 쉽게 연다.

### 8-2. 원칙 4(플러그인 구조) — **충돌하지 않는다. 오히려 강화한다**

7번째 팩을 추가하는 데 필요한 것:
```
① Resources/Items 에 매니페스트 에셋 1개 (entitlements[Steam].entitlementId = 새 DLC appid)
② 스팀 파트너 사이트에 DLC SKU 1개
③ 프로덕션 .cs  ← 0줄
```
`PackRegistry`가 폴더를 훑고, 어댑터는 `packId`로 매니페스트가 선언한 값을 읽을 뿐이다.
**어댑터에 팩 이름이 하나라도 박히면 그 순간 원칙 4가 깨지므로 §7-2 (5)가 그것을 감사한다.**

★ 남은 원칙 4의 진짜 구멍은 **M-1(조형 층 `AccessoryShapeBuilder`의 `case` 43개)**이지 여기가 아니다.
**이 예외가 M-1을 더 어렵게 만들지 않는다** — 두 층이 만나지 않는다.

### 8-3. 선 1(네트워크 0) — **형태가 바뀐다. 숨기지 않는다**

§7-6의 표가 그 전부다. 요약: **우리 매니지드 코드의 네트워크 API는 여전히 0건**이고,
**서버 검증(`CheckAppOwnership`)은 여전히 하지 않는다**(§E-5). 바뀌는 것은
「우리가 링크하는 네이티브 모듈이 하나 늘고 그 내부를 우리가 못 본다」이며, 그 사실을
**테스트 실패 메시지에 적는다**(§7-6).

---

## 9. 비용표 (정의서 요구 형식)

| 항목 | 구현 비용 | 백신 위험 | 유저 마찰 | 오탐 시 피해 |
|---|---|---|---|---|
| **이 예외 채택** | 어댑터 1파일(~150줄) + `UseSource` 1개 + 감사 1파일 + 기존 감사 4곳 갱신 | **미확인 · 방향은 중립~감소**(§9-1) | **낮음**(§9-2) | ★ **높음** — `Unknown`이 `NotOwned`로 붕괴하면 **정상 구매자 전원 잠김**. 그 붕괴를 §6-1이 구조로 막는다 |
| 예외를 안 열고 출시 | 0 | 0 | — | **6팩을 팔 수 없다 = 출시 자체가 없다**(R-5-min이 지배항) |
| 니들만 지우고 연다 | 0 | 0 | — | ★ **가장 비싸다** — `SteamRemoteStorage`·`SteamInventory`·`SteamUser`가 아무 저항 없이 들어오고, 그때 아무도 안 본다. `EntitlementFailOpenAuditTests` 클래스 문서가 이 형태를 이름으로 경고한다 |
| 손수 P/Invoke(§3-3) | 중~상 | 미확인 | — | **버전 접미사 오류 → 전원 NotOwned**. 그리고 감시가 사라진다 |

### 9-1. 백신(선 3) — 정직한 판정

| 방향 | 근거 |
|---|---|
| **위험 증가 쪽** | 네이티브 DLL 1개 추가, P/Invoke 수 급증(서드파티 래퍼 내부) |
| **위험 감소 쪽** | `steam_api64.dll`은 수만 개 상용 게임이 싣는 **가장 흔한 서드파티 네이티브 모듈**이다. `SECURITY_MODEL §S-3`이 지적한 우리 형상(*"종일 상주하며 남의 창 제목을 훑고 키 상태를 폴링하고 레지스트리를 읽는 프로세스"* = 애드웨어/키로거 모양)에서 **「평범한 스팀 게임」 쪽으로 이동**한다 |

> **순효과 미확인.** 그러나 **선 3을 근거로 이 예외를 막을 이유는 없다** — 확실히 나쁜 것
> (DRM 래퍼 · 패킹 · 난독화)은 이미 §E-5·§S-4-3에서 전부 기각돼 있고, 이 예외는 그중 하나도 도입하지 않는다.
> 확인 방법은 §11-2.

### 9-2. 유저 마찰 — 매일 아침 발생하는 구간이 있다

이 앱은 **부팅 자동 실행 + 종일 상주**가 정상 형태다(§E-3).
```
09:00:03  로그인 → StickMate 자동 실행 → SteamAPI.Init() 실패 → 전 팩 Unknown
09:00:31  스팀 기동 완료
09:01:00+ 다음 조회에서 재시도 성공 → Owned
```
그 사이 **입고 있던 것은 그대로 유지되고**(§E-3-b 단조), **새로 착용만 거부**되며,
문구는 *"안 샀습니다"*가 아니라 *"지금 확인할 수 없습니다"*다(§E-2-a).

> **정직하게 남기는 마찰 1건**: 사용자가 **스팀을 아예 안 켜는 날**은 그날 하루 새 팩을 못 입는다.
> 로컬에 소유를 캐시하는 것은 §E-4가 금지했고(표적 제거), 그 판단은 여전히 옳다.
> 완화 후보(**판정은 리더 · 설계는 `ux-designer`**): `Unknown`이 N분 이상 지속되면
> 보관함에 *"스팀을 켜면 팩을 쓸 수 있습니다"* 한 줄. **이건 내 소관이 아니다.**

---

## 10. 되돌리기 비용 — 「한 번 열면 못 닫는다」에 대한 답

| 되돌릴 상황 | 지워야 하는 것 | 다른 프로덕션 파일 영향 | 판정 |
|---|---|---|---|
| **스팀 철수** | 어댑터 1파일 · asmdef 참조 1줄 · 화이트리스트 1건 · 감사 1파일 · `UseSource` 1개 | **0줄** | **낮음.** `PackEntitlements`가 `NullPackEntitlementSource`로 되돌아가 **전부 Unknown** = 회수 없음 · 잠금 없음 |
| **채널 교체(MS 스토어)** | 두 번째 어댑터 1파일 + 같은 형태의 니들 예외 1건 | **0줄** — `PackStoreChannel.MicrosoftStore`가 이미 있고 매니페스트도 이미 채널별이다 | **낮음** |
| **감사를 다시 조이기** | 화이트리스트가 **닫힌 세계**라 넓히려면 심볼을 목록에 추가해야 하고 **그 diff가 리뷰다** | — | **번지지 않는다** |
| ★ **DLC appid를 바꾸기** | — | — | ★★ **불가에 가깝다 — §10-1** |

### 10-1. ★ 되돌릴 수 없는 것은 코드가 아니라 문자열 1개다

매니페스트의 `entitlements[Steam].entitlementId`(= 스팀 파트너에 등록한 **DLC appid**)는
**출시 후 바꾸면 이미 산 사람이 그 팩을 못 쓰게 된다.** 되돌릴 방법이 없다.

> **성질이 `I-1`(`companyName`/번들 ID 동결)과 정확히 같다.**
> `CRITICAL_PATH` §1이 I-1을 *"출시 후 재변경 = 유저 진행도 전량 소실"*로 못박은 것과 같은 등급으로,
> **`entitlementId`도 동결 대상 명부에 올려야 한다.**
> `StickPackManifestSO.packId` 툴팁에는 *"절대 바꾸지 말 것"*이 이미 적혀 있는데,
> **`entitlementId`에는 그 문장이 없다.** 그 한 줄을 더하는 것이 이 예외에 딸린 유일한 「기존 파일 수정」이다.

---

## 11. 미확인 — 추측으로 메우지 않는다

1. **★ 빈 depot DLC를 `BIsDlcInstalled`가 true로 보고하는가**(§6-4). **가장 비싸다.** 스팀 실기 필요.
   틀리면 정상 구매자 전원이 잠긴다. 분기는 §6-4에 미리 적어 뒀다.
2. **`steam_api64.dll`이 소켓을 여는가 / 방화벽 프롬프트가 뜨는가.**
   **확인 방법**: 낮 세션에서 어댑터 포함 빌드를 **처음** 실행할 때 Windows 방화벽 대화상자가 뜨는지 관측.
   뜨면 「권한 0 요구」 자산이 실제로 손상된 것이고, 그때는 이 문서를 다시 연다.
3. **AhnLab V3 판정 변화**(사용자 실기 환경, 커밋 `aaac7b2`). CW-5/H-6 세션에
   **어댑터 포함 빌드 1회 스캔**을 항목으로 추가해야 한다.
4. **스팀 오버레이 주입 × 우리 레이어드 창**(`CRITICAL_PATH` §7-1, 저장소 어디에도 서술 0건).
   ★ 다행히 **엔타이틀먼트 계약은 이 경우를 이미 견딘다** — 스팀 밖 실행이면 `Unknown`이고
   그건 `NotOwned`가 아니다. **설계는 맞다. 확인이 없을 뿐이다.**
5. **Steamworks.NET UPM 패키지에 네이티브 재배포 바이너리가 포함되는가**(§7-7). `dev-platform`.
6. **iOS 타깃에서 미포함 어셈블리 참조가 컴파일 오류인가**(§3-2). 1.0 범위 밖.
7. **테스트를 돌리지 않았다.** 이 문서는 소스 · asmdef · 1차 출처(Steamworks.NET 저장소 원본 asmdef)의
   **정적 실측**이다. Unity 배치모드는 다른 라운드가 잡고 있었다.

---

## 12. 플랫폼 영향 (CLAUDE.md 상시 지시)

**Windows 영향: 이 설계의 1차 대상이다.** 프로덕션 `.cs` 수정 0건 — 설계·감사 명세만.
구현 라운드에는 `Tools/CrossCompile/xcheck.sh win` 0에러가 의무다.
★ 그리고 이번 라운드가 발견한 **Windows 고유 위험 1건**: `SECURITY_MODEL §5-2`가 기록한
*"In-app purchase functionality is not currently supported in elevated applications"*는 MS 스토어 항목이지만,
**스팀 경로에서도 승격 실행은 피해야 한다** — 작업표시줄 예외(`ABM_SETSTATE`)가 언젠가 승격을 요구하게 바뀌면
두 기능이 함께 죽는다. 오늘은 승격하지 않으므로 문제 없다(권한 0).

**macOS 영향: 함께 검토함 — 코드 경로를 지금 같이 연다.**
1. `#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX`로 여는 것이 §3-2의 판정이다.
   Windows만 열면 `xcheck.sh osx`가 이 파일을 **구조적으로 못 보고**, 그게 M-12를 그대로 밟는 길이다.
2. **macOS 경로는 「코드는 있고 검증은 없다」로 표기한다.** 1.0 출하 depot에 macOS가 없으므로
   실기 확인 대상이 아니고, 그 사실을 감사에 `Assert.Ignore`(사유 포함)로 남겨 러너에 계속 보이게 한다 —
   `PlatformParityAuditTests`에 *"미해결: 스팀 엔타이틀먼트 조회의 macOS 실기 미확인
   (Windows 출시 국면에서 강등, 2026-09-05)"*.
3. `S-1`(macOS 서명·공증)의 하드닝 런타임 엔타이틀먼트 2종
   (`disable-library-validation` · `allow-dyld-environment-variables`)은 **스팀 오버레이 주입 때문에 필요하다**고
   Steamworks 문서가 명시한다. **이 어댑터가 그 요구를 새로 만드는 것은 아니지만**,
   macOS depot을 추가하는 날 `steam_api.bundle` 로딩이 그 엔타이틀먼트에 의존한다는 점은 기록해 둔다.

---

## 13. 리더 결재가 필요한 것 (3건)

| # | 질문 | 내 권고 |
|---|---|---|
| **결재-1** | **니들 예외를 이 형태로 승인하는가** — 허용 파일 1개 · 허용 심볼 5개 · 닫힌 세계 감사 | **승인 권고.** R-5-min이 출시일 지배항이고, 이보다 좁게 열 방법을 나는 찾지 못했다 |
| **결재-2** | `전송계열_화이트리스트는_현재_비어_있다`를 **니들 종류 변경으로 초록 유지**할 것인가, **「승인된 1건 등호」로 개작**할 것인가 | ★ **개작.** 종류 변경은 지표를 우리가 원하는 답으로 고치는 것이다(§7-6) |
| **결재-3** | `entitlementId`를 **I-1과 같은 등급의 동결 대상**으로 승격하고, `StickPackManifestSO`의 툴팁에 그 문장을 넣을 것인가 | **승격.** 이 예외에 딸린 **되돌릴 수 없는 유일한 것**이다(§10-1) |

**그리고 승인과 무관하게 지금 고쳐야 하는 것 2건**(스팀을 안 붙여도 오늘 이미 거짓이다):
- §2-1 — asmdef 가드의 대소문자 구멍 + 양성 대조 부재. **지금 그 감사는 아무것도 못 본다.**
- §2-3 — `TestClaimExpiryAuditTests` 명부의 *"C층 코드가 오늘 0줄"*. **이미 거짓이다.**
