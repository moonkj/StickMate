# C층 엔타이틀먼트 계약 — 구현 규범 (security, 2026-09-02)

`docs/security/SECURITY_MODEL.md` §3·§5의 후속. **이 문서는 보고서가 아니라 계약이다.**
`coder-systems`가 C층을 배선할 때 이 절 번호를 그대로 참조한다.
프로덕션 `.cs` 수정 0건 — 구현은 리더가 배정한다.

전제(사용자 확정 2026-09-02): F2P · 스팀 단독 · 플러그인 봉인(1st-party) · DLC 1.0 포함 ·
기본 42종은 재화/현금 양쪽 · DLC 아이템은 DLC로만.
3층 구분과 세이브 필드는 `docs/GAME_ARCHITECTURE_REVIEW.md` §8-2가 확정했다. 여기서는 **C층 내부**만 정한다.

---

## E-1. 상태는 2개가 아니라 **3개**다 — 이 계약의 뿌리

```
enum EntitlementState { Owned, NotOwned, Unknown }
```

`bool`로 두면 **Unknown이 NotOwned로 붕괴**한다. 그 붕괴가 곧 「돈 낸 사람 잠그기」다.

> **E-1-a (금지):** 엔타이틀먼트 조회 결과를 `bool`로 반환하는 API를 만들지 않는다.
> **E-1-b (금지):** `Unknown`을 `NotOwned`와 같은 분기에서 처리하지 않는다.
>   `switch`에 `default:`를 두지 말고 세 갈래를 전부 명시한다(조용한 실패 금지 — `game-architect` 착수 2번과 같은 계열).

### 왜 이게 실제 위험인가 — 1차 출처

`SteamAPI_Init`은 **Steam 클라이언트가 안 떠 있으면 실패**한다(Steamworks *steam_api.h*:
*"A running Steam client is required"*). 실패 시 `SteamApps.BIsDlcInstalled`는 답을 주지 못한다.
소박한 구현 `bool owned = SteamApps.BIsDlcInstalled(id);`는 그 상황에서 **false**로 읽히고,
**정상 결제한 모든 사용자가 잠긴다.** 무단 사용 열 건보다 비싼 사고가 여기 한 줄에 있다.

---

## E-2. 조회 실패는 **회수하지 않는다**. 그리고 「복원」과 「신규 행사」를 가른다

| 스토어 응답 | 이미 착용/보유 중인 C층 | **새로** 착용·사용 |
|---|---|---|
| `Owned` | 유지 | 허용 |
| `NotOwned` | **해제**(그 슬롯 무료 rank0으로) | 거부 + 구매 안내 |
| `Unknown` | ★ **유지. 절대 회수하지 않는다** | ★ **거부**(단 문구는 "잠김"이 아니라 "지금 확인할 수 없음") |

`GAME_ARCHITECTURE_REVIEW` §8-6과 동일. 여기서 **문구 분리**를 계약으로 승격한다:

> **E-2-a:** UI에 노출되는 사유 문자열은 `NotOwned`와 `Unknown`이 **서로 달라야 한다.**
> 같은 문구를 쓰면 오프라인 유저에게 *"당신은 안 샀습니다"*라고 말하게 된다.

---

## E-3. ★ 신규 — **한 번 실패한 조회를 프로세스 수명 동안 붙들지 않는다** (24시간 상주 앱 고유 위험)

이 앱은 부팅 자동 실행 + 종일 상주가 정상 사용 형태다. 그러면 다음이 **고빈도로** 발생한다:

```
09:00:03  OS 로그인 → StickMate 자동 실행 → SteamAPI_Init 실패(스팀 아직 안 뜸) → Unknown
09:00:31  스팀 클라이언트 기동 완료 (라이선스 로컬 캐시 보유)
09:00:31 ~ 다음 날      ← 이 구간 내내 Unknown. 유저는 산 팩을 새로 못 입는다
```

일반 게임은 실행이 짧아 "다시 켜면 됩니다"가 답이 되지만, **상주 앱에서 "다시 켜세요"는 답이 아니다.**

> **E-3-a (필수):** 상태가 `Unknown`인 동안 **주기적으로 재시도**한다. 권고 간격 60초,
>   상한 없음(비용은 로컬 IPC 1회). `Owned`/`NotOwned` 확답을 받으면 재시도를 멈춘다.
> **E-3-b (필수):** `Owned` → `Unknown` 전이는 **표시·권한에 아무 영향을 주지 않는다.**
>   한 번 확인된 소유는 그 프로세스 안에서 **단조(monotonic)**다.
> **E-3-c (금지):** `Owned` → `NotOwned` 전이를 프로세스 수명 중에 **적용하지 않는다.**
>   (환불·가족공유 회수는 실재하지만, 상주 중 회수는 오탐 비용이 회수 이익보다 크다. 다음 실행에서 정리된다.)

---

## E-4. 실행 간 캐시를 **만들지 않는다** — 그리고 그 이유는 성능이 아니다

> **E-4-a (금지):** "마지막 성공 판정"을 파일·PlayerPrefs·레지스트리 어디에도 쓰지 않는다.

C층을 세이브에서 뺀 목적은 방어 추가가 아니라 **표적 제거**였다. 캐시 파일을 만들면 표적이 되돌아온다.
E-3의 재시도가 이 규칙의 대가(오프라인 재시작 시 잠깐 Unknown)를 실무상 0에 가깝게 만든다.

★ `PlayerPrefs`는 Windows에서 **레지스트리에 쓴다** — 이 저장소는 레지스트리 쓰기를 금지하고 있다
(`Platform/ReservedBarRestoreLedger.cs` 주석에 이미 명시). 두 규칙이 같은 방향을 가리킨다.

---

## E-5. 우리가 만들지 않는 것 — **플랫폼이 이미 하는 것을 다시 만들지 마라**

| 하지 않는다 | 1차 출처 / 이유 |
|---|---|
| 소유 정보의 자체 서명·암호화·HMAC | 키가 클라이언트에 있다. 스팀이 이미 라이선스를 들고 있다 |
| 자체 라이선스 파일 | E-4 |
| Steam DRM 래퍼 | **F2P다 — 실행 파일 자체가 무료라 보호할 대상이 없다.** 게다가 래퍼가 exe를 다시 쓰므로 **Authenticode 서명이 무효화**된다(S-4 참조) |
| `SteamAPI_RestartAppIfNecessary` | ★ **판정 보류 → 리더/`dev-platform`.** 유일한 이득은 직접 실행 시 스팀 컨텍스트 확보인데, **상주 앱을 사용자가 재시작시키는 부작용**이 있다. 쓰지 않고 E-1/E-3으로 흡수하는 쪽을 권고한다 |
| 난독화·패킹·안티디버그·무결성 자가검사 | 선 3(백신 오탐). `MARKET_LANDSCAPE`: 이 카테고리를 죽인 건 기능이 아니라 배포 신뢰다 |
| 서버 검증(`CheckAppOwnership`) | 선 1(네트워크 0). Valve가 제시하는 안전한 방법이지만 **우리는 쓸 수 없다** |

### Valve 자신의 경고를 계약에 그대로 박는다

`ISteamApps` 문서 원문: `BIsDlcInstalled` — *"Should only be used for simple client side checks -
**not intended for granting in-game items**."*

> **E-5-a:** 이 문장은 "쓰지 마라"가 아니라 **"이걸로 못 막는 것을 막으려 하지 마라"**로 읽는다.
> 우리 목표는 「무단 사용 불가능」이 아니라 **「정직한 사용자가 정직하게 살 이유가 있고, 실수로 잠기지 않는 것」**이다.

---

## E-6. C층 게이트의 **문법적 격리** — 주석이 아니라 타입으로

`EquipmentDebugUnlock.UnlockAll`은 오늘 정확히 2곳에서 참조된다
(`Core/ItemCatalog.cs:262`, `Core/EquipmentModel.cs`의 `IsItemOwned`). **DLC가 합류하면 그 위에 그대로 얹힌다.**

> **E-6-a (필수):** C층 판정 함수의 **본문과 호출 그래프 어디에도** `EquipmentDebugUnlock`이
>   나타나지 않는다. `A(item) || B(item) || C(item)` 합집합에서 `UnlockAll`은 **A·B 가지 안**에만 있다.
> **E-6-b (필수):** C층 판정 타입은 `EquipmentDebugUnlock`을 **참조조차 하지 않는다**(어셈블리 참조가
>   아니라 타입 참조 수준). 감사가 "이 타입이 그 타입을 모른다"를 잴 수 있어야 한다.
> **E-6-c (필수):** C층에는 **테스트 오버라이드를 두지 않는다.** 두어야 한다면 `internal`이고,
>   그 사실을 감사가 잠근다.
>   근거(실측, 출하 어셈블리 IL): `EquipmentDebugUnlock::SetTestOverride`는 `.method assembly`(internal)인데
>   `StickMateDevTools::SetTestOverride`는 **`.method public`**이다 — **같은 집에 두 관례가 공존한다.**
>   C층이 후자를 따라가면 리플렉션 한 줄이 크랙이 된다.

---

## E-7. ★ 신규 — 「두 지갑에서 같은 아이템을 판다」의 **환불 창이 이 앱에서는 구조적으로 닫혀 있다**

사용자 확정으로 기본 42종 일부가 **동전으로도 현금으로도** 팔린다.
`GAME_ARCHITECTURE_REVIEW` §8-3-b는 겹 2의 3번(스팀 스토어 페이지에서 이미 가진 것을 또 사는 경로)을
**막을 수 없다**고 정확히 판정했다. **그 판정에 새 사실 하나를 더한다.**

Steam 환불 정책 원문:
> *"DLC purchased from the Steam store is refundable within fourteen days of purchase, and
> **if the underlying title has been played for less than two hours since the DLC was purchased**…"*

**StickMate는 종일 상주한다. 구매 2시간 뒤면 유저가 아무것도 안 해도 「2시간 플레이」가 채워진다.**

> ### → **자동 환불 경로가 구매 약 2시간 후 구조적으로 닫힌다.** 남는 것은 수동 지원 티켓뿐이다.
> 이 사실이 겹 2 3번의 **피해 크기를 바꾼다**: 「환불하면 되는 실수」가 아니라
> 「환불이 자동으로 안 되는 실수」다. 그리고 상주 앱에 대한 가장 큰 불신
> (`MARKET_LANDSCAPE` 3-1의 Desktop Mate 사례)이 정확히 이 지점이다.

**보안 관점의 권고(판정은 리더·`product-strategy`):**

| 안 | 내용 | 네트워크 0 | 비용 |
|---|---|---|---|
| **(가)** | 개별 현금 SKU를 **만들지 않는다**(C층 = 팩 전용). 겹침 자체가 소멸 | ✅ | 사용자 확정 사항의 축소 — 리더가 사용자에게 물어야 한다 |
| **(나)** | §8-3-b (ㄱ) — 개별 DLC를 스토어에서 숨기고 앱 안에서만 진입 | ✅ | ★ **가능 여부 미확인**(Valve 문서에 숨김 옵션 서술 없음). SKU 등록 전 확인 필수 |
| **(다)** | C가 확인되면 그 아이템에 쓴 **동전을 그 아이템 값만큼 되돌려준다** | ✅ (전부 로컬) | 「이중 지불」이 「동전 환급」이 된다. **단 현금→시간 단축이 되살아나므로** `design-systems`의 페이투윈 검산(§3-6)을 다시 돌려야 한다 |
| **(라)** | DLC 설명문 첫 줄 경고 | ✅ | 판매 저해. 그러나 불투명성보다 싸다 |

> **(다)는 파밍 불가다** — 되돌려주는 동전은 **그 아이템에 이미 쓴 액수 상한**이고 아이템당 1회다.
> 새 수도꼭지가 아니라 「두 구매 순서를 동치로 만드는 것」이다. 그래도 **경제 판정은 내 소관이 아니다.**

---

## E-8. B층 위조 — **여전히 「그냥 둔다」. 사용자 확정으로도 바뀌지 않는다**

리더 브리핑의 우려(*"같은 아이템이 두 지갑에서 팔리는 순간 B층 위조는 C층 매출을 갉는다"*)를 정면으로 본다.

**갉지 않는다. 이유는 셋이다.**

1. **B층을 위조할 수 있는 사람은 C층을 살 사람이 아니다.** 평문 JSON에 아이템 ID를 적어 넣을 줄 아는
   사람에게 대안은 "$1.99 결제"가 아니라 "**Mono 어셈블리를 dnSpy로 고치기**"다. 실측: 출하 빌드는
   Mono 백엔드이고 `StickMate_Data/Managed/StickMate.Runtime.dll`이 그대로 실려 있다(IL2CPP 아님).
   **B층을 잠가도 그 사람은 C층으로 못 온다 — 옆문이 더 넓다.**
2. **잠그는 비용이 매출보다 크다.** 세이브 무결성 검증은 **우리 쓰기 버그를 치터보다 먼저 만난다**
   (`SECURITY_MODEL` §4-1-4: 고아 `.writing` 파일이 실제로 디스크에 남아 있었다).
   그때 정직한 유저에게 "당신은 치터입니다"를 말하게 된다.
3. **★ 위조가 갉는 것은 C층이 아니라 B층 자신이다.** 겹 2가 성립하려면 그 아이템이
   **B층에도 C층에도** 있어야 하는데, 세이브를 고칠 줄 아는 사용자는 애초에 **B층 경로가 공짜**다.
   그가 포기하는 매출은 「그가 살 뻔했던 $1.99」이고, 그 반사실은 **관측 불가**하며 1번에 의해 거의 0이다.

> **E-8-a (재확인):** 암호화·HMAC·변조 감지·경고 **전부 기각.** 평문 유지가 옳다.
> **E-8-b (유일한 실질 대책):** C층을 세이브에서 빼는 것. 그건 이미 §8-2가 계약으로 못박았다.
> **E-8-c:** `purchasedItemIds`(B층)에 **C층 아이템 ID가 들어가는 코드 경로가 존재하지 않을 것.**
>   세이브 → 엔타이틀먼트 방향의 화살표가 코드에 있으면 그것이 곧 크랙이다(§8-2 재확인).

---

## E-9. 감사 테스트 3건 (리더 승인 시 `security`가 작성 — 정의서 예외 조항)

전부 **소스 텍스트 스캔**이다(활성 빌드 타깃 사각지대 회피 — CLAUDE.md 규칙).
**세 건 모두 네거티브 컨트롤 필수**(가짜 위반 문자열을 같은 스캔 함수에 흘려 잡히는지 확인).

| # | 테스트 | 잠그는 것 | 오늘의 기대값 |
|---:|---|---|---|
| 1 | `EntitlementNotInSaveAuditTests` | `CharacterSaveStore.SaveData`의 **필드 이름**에 `dlc`/`pack`/`entitle`/`owned` 4토큰 없음. ★ **값은 검사하지 않는다**(`wornHead = "pack.office.fedora"`는 정상 — §8-2-c) | 현재 필드 43개 중 위반 0 |
| 2 | `UnlockSwitchScopeAuditTests` | `EquipmentDebugUnlock` 참조가 A·B 계열 파일 밖에 없음 | 참조 **정확히 2곳**(`ItemCatalog.cs` · `EquipmentModel.cs`). 이 숫자가 기대값 |
| 3 | `EntitlementFailOpenAuditTests` | ★ **신규.** C층 조회 결과 타입에 `Unknown`이 실재하고, `NotOwned`와 **다른 분기**에서 처리됨. `bool` 반환 API 부재 | 오늘 C층 코드 0줄 → **"타입이 없으면 Ignore(사유 포함)"**로 시작해 배선 라운드에 활성화 |

★ 3번은 **지금 `Assert.Ignore`로 넣어 둔다**(CLAUDE.md의 "못 고친 갭은 Fail이 아니라 Ignore" 규칙).
그래야 C층 배선 라운드가 이 테스트를 **켜는 것을 잊을 수 없다.**

---

## E-10. 서명 — **1.0에 필요하다. 근거는 SmartScreen이 아니다** (S절)

### S-1. macOS: **선택이 아니다 — 채널 요구이자 OS 요구다**

실측(2026-09-02, `Builds/macOS/StickMate.app`):
```
CodeDirectory flags=0x2(adhoc)   TeamIdentifier=not set
spctl --assess --type execute → rejected      (양성대조: Calculator.app → accepted)
```
**애드혹 서명 + Gatekeeper 거부.** 이 상태로는 **공증(notarization)이 불가능**하다 —
공증은 Developer ID Application 인증서와 하드닝 런타임을 요구한다.

- Steamworks 문서: 2019-10-14부로 신규 macOS 앱은 **64비트 + Apple 공증**. 파트너 사이트에
  *"App Bundles Are Notarized"* 체크박스가 존재한다.
  ★ **미확인/충돌**: 같은 스레드의 Valve 모더레이터 답변은 *"we will allow your game to ship on Steam
  without notarization at this time"*(2019년 시점)이라 **문서와 운영이 어긋난다.** 2026년 현재 강제 여부는
  **Steamworks 로그인 후 확인 필요.**
- **그러나 Apple 쪽은 어긋나지 않는다.** 브라우저·압축 해제기 등 `com.apple.quarantine`을 붙이는 경로로
  받은 애드혹 앱은 Gatekeeper가 막는다. 스팀 경유는 격리 속성을 붙이지 않아 오늘은 통과하지만,
  **그 통과는 스팀 클라이언트 동작에 의존하는 우연이지 우리가 가진 보증이 아니다.**
- **비용**: Apple Developer Program **$99/년**. ★ 이 비용은 CLAUDE.md가 명시한 **iPad/iPhone 타깃에
  어차피 필요하다.** 즉 신규 비용이 아니라 **선지출**이다.
- **★ `dev-platform`에 넘길 제약 2건**(Steamworks 문서 명시):
  - 하드닝 런타임에 `com.apple.security.cs.disable-library-validation` +
    `com.apple.security.cs.allow-dyld-environment-variables`가 필요하다(스팀 오버레이 주입 때문).
  - **`com.apple.security.app-sandbox`를 켜면 스팀과 호환되지 않는다.** 맥 앱스토어와 같은 빌드를 쓸 수 없다.
  - 추가 확인 필요: Mono 백엔드는 JIT을 쓰므로 `allow-jit` 계열 엔타이틀먼트가 필요할 수 있다 — **미확인.**

### S-2. Windows: **서명한다. 단 SmartScreen 때문이 아니다**

`product-strategy`의 기존 판정(*"스팀 단일 채널이니 코드서명은 선택"*)은 **절반만 유효하다.**

| 관문 | 스팀 경유로 비켜지는가 | 근거 |
|---|---|---|
| SmartScreen **애플리케이션 평판** | **대체로 비켜진다** | MOTW가 붙은 파일에 발동한다. 스팀은 게임 파일에 MOTW를 붙이지 않는다. ★ **1차 출처 없음 — 업계 통념. 미확인으로 남긴다** |
| **Smart App Control (Win11)** | ★ **비켜지지 않는다** | MS 1차 출처 원문: *"Smart App Control will block execution of **unsigned files** unless the file has a positive reputation. Smart App Control signature checks apply to **all executable files, not just those downloaded from the Internet**."* |
| **백신 휴리스틱** | 비켜지지 않는다 | 아래 S-3 |

**SAC의 모집단은 제한적이다**(MS 지원 문서: **클린 설치** Win11 + 선택적 진단 데이터, 평가 모드가
스스로 끄기도 하고, 한 번 끄면 재설치 없이 못 켠다). **작지만 0이 아니고, 스팀이 못 막는 유일한 Windows 관문이다.**

### S-3. ★ 진짜 이유 — 우리 Win32 표면이 **정확히 애드웨어/키로거 휴리스틱 모양**이다

프로덕션 `DllImport` 전수(실측, 테스트 제외). **전부 읽기 전용이지만 조합이 문제다:**
```
EnumWindows · GetWindowTextW · InternalGetWindowText · GetForegroundWindow · WindowFromPoint
GetAsyncKeyState · GetCursorPos · GetLastInputInfo
SetLayeredWindowAttributes · SetWindowLongPtr64 · SHAppBarMessage
RegOpenKeyExW · RegEnumKeyExW · RegQueryValueExW · OpenProcess · QueryFullProcessImageNameW
```
= **종일 상주하며 남의 창 제목을 훑고, 키 상태를 폴링하고, 레지스트리를 읽고, 작업표시줄을 건드리는 프로세스.**
`MARKET_LANDSCAPE`의 결론이 여기 정확히 꽂힌다: **Shimeji를 죽인 건 기능이 아니라 백신 경고였다.**
그리고 사용자 실기 환경은 **AhnLab V3**다(커밋 `aaac7b2` 기록) — 국내 백신은 무명 무서명 바이너리에 특히 보수적이다.

**반가운 실측 3건**(오해를 미리 막는다):
- `WriteProcessMemory`/`ReadProcessMemory`/`VirtualAllocEx`/`SetWindowsHookEx`/`CreateRemoteThread`
  **P/Invoke 선언 0건.** 히트는 전부 *"우리는 이걸 안 한다"*는 **주석**이고,
  `UserAssetImmutabilityAuditTests`가 이미 잠그고 있다.
- `GetAsyncKeyState`의 **문자키 조회는 조합키 3개가 눌린 동안에만** 일어난다
  (`AppControlDirector.TickHotkeys`: `chord && IsKeyDown(letter)` — C# `&&` 단락).
  **키로거 혐의에 대한 검증 가능한 반박**이고, 감사로 잠글 가치가 있다.
- 개발 게이트가 닫히면 (다) 계열 6키는 **조회조차 하지 않는다**(같은 단락 성질).

→ **서명은 이 앱에서 백신 오탐을 「늘리는」 조치가 아니라 「줄이는」 유일한 조치다.**
선 3(백신 오탐 금지)과 **충돌하지 않고 오히려 같은 편**이다.

### S-4. 무엇을 사고 무엇을 사지 않는가

| | 판정 | 근거 |
|---|---|---|
| **EV 인증서** | ★ **사지 않는다** | MS 1차 출처: *"EV certificates no longer bypass SmartScreen … Paying a premium for EV solely to avoid SmartScreen warnings is no longer justified."* `tft-competitive` R1 확인 사항이 **1차 출처로 재확인됨** |
| **OV / Azure Artifact Signing** | **채택 권고** | MS 문서가 비스토어 배포에 권장. **월 $9.99부터**, 하드웨어 토큰 불필요, CI 연동 |
| **Steam DRM 래퍼** | **쓰지 않는다** | ① F2P라 보호할 exe가 없다 ② 래퍼가 `.bind` 섹션을 추가하고 체크섬을 다시 계산하므로 **먼저 한 서명이 무효화**된다. 굳이 쓴다면 **래핑 후 서명**이어야 한다 |
| **난독화·패킹** | **기각** | 선 3 정면 위반. 그리고 §E-8-1에 의해 얻는 것도 없다 |

> **서명의 진짜 값어치는 「최초 경고 회피」가 아니라 「평판의 이월」이다.**
> MS 문서: *"Unsigned files must build reputation anew with **every update**."*
> 상주 앱은 업데이트가 잦다. **무서명이면 릴리스마다 평판이 0으로 리셋된다.**

### S-5. 비용표 (정의서 요구 형식)

| 조치 | 구현 비용 | 백신 위험 | 유저 마찰 | 오탐 시 피해 |
|---|---|---|---|---|
| macOS 서명+공증 | Apple $99/년(iOS에 어차피 필요) + 빌드 파이프라인 1회 + 엔타이틀먼트 3종 확인 | **감소** | **감소**(현재 Gatekeeper 거부 상태) | 없음 |
| Windows OV 서명 | ~$120/년 + CI 서명 단계 1개 | **감소** | **감소** | 없음 |
| EV 인증서 | $300~600/년 | 변화 없음 | 변화 없음 | — → **기각** |
| Steam DRM | 빌드 단계 1개 | 증가(패커 유사) | 스팀 미실행 시 실행 불가 | **높음** → **기각** |
| 세이브 무결성 검증 | 중 | 증가 | — | ★ **정당한 유저 잠금** → **기각**(§E-8) |

---

## E-11. 플랫폼 영향

- **Windows 영향: 함께 검토함.** S-2·S-3·S-4가 Windows 전용 판정이다.
  실측은 **출하 zip(`Builds/StickMate-Windows-20260902b.zip`, 09-02 12:42)의 IL을 직접 디스어셈블**해
  이뤄졌다 — 활성 빌드 타깃(macOS) 사각지대를 우회한 측정이다.
  ★ 미해결 이월: `Platform/Windows/WindowsGameProcessProbe.cs:159`가 **남의 exe 풀 경로**(계정 실명 포함)를
  로그에 인쇄한다(`SECURITY_MODEL` §1-3-b, **아직 미수정**).
- **macOS 영향: 함께 검토함.** S-1이 macOS 전용이고 **1.0 차단 후보**다.
  ★ 미해결 이월: `Platform/MacOS/MacWindowService.cs:1516,1534`가 남의 앱 이름을 로그에 인쇄한다(미수정).
- **엔타이틀먼트 조회 자체는 플랫폼 분기가 없다**(스팀이 양 플랫폼에 같은 API를 준다).
  단 **정책 판정(E-1~E-3)은 반드시 `Platform/` 중립 위치**에 둔다 — `FullscreenSuspendPolicy.cs` 사고 재발 자리.
- **모바일**: 1.0 밖. E-1~E-4는 StoreKit에 그대로 재사용된다(C층 저장소만 바뀐다).

---

## E-12. 이 문서가 보장하지 않는 것 (정직성 규칙)

1. **앱을 실행하지 않았고 빌드하지 않았다.** 위 결론은 소스 · 출하 어셈블리 IL · 빌드 산출물 ·
   `ProjectSettings` · 1차 출처 문서의 **정적 실측**이다.
2. **스팀 실기 없음.** 오프라인 모드에서 `BIsDlcInstalled`가 무엇을 답하는지는 **Valve 미문서화 + 미측정.**
   E-1~E-3이 그 미확인을 **설계상 무해**하게 만들지만, 무해화한 것이지 확인한 것이 아니다.
3. **"스팀은 MOTW를 붙이지 않는다"에 1차 출처가 없다.** S-2의 첫 행은 **미확인**이다.
   그러나 결론(서명한다)은 그 행에 의존하지 않는다 — SAC와 백신만으로 성립한다.
4. **2026년 현재 Valve의 macOS 공증 강제 여부 미확인**(문서와 모더레이터 답변이 어긋난다).
   Apple 쪽 요구는 어긋나지 않으므로 결론은 바뀌지 않는다.
5. **Mono JIT × 하드닝 런타임 엔타이틀먼트 미확인** — `dev-platform` 확인 필요.
6. **테스트를 돌리지 않았다**(`qa-regression`이 배치모드 사용 중).
