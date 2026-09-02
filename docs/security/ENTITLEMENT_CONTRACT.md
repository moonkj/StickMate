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

### ★ 착지 기록 (2026-09-02, 리더 승인 후 `security`가 작성)

| 파일 | 내용 |
|---|---|
| `Tests/EditMode/EntitlementAuditSource.cs` | 3종 공용 스캐너(주석 제거 · 낱말 경계 · 직렬화 타입 발견 · 낱말 조각 분해). **정규식 0줄 · 리플렉션 0줄** |
| `Tests/EditMode/EntitlementNotInSaveAuditTests.cs` | #1. 직렬화 필드 **이름**만 검사(값은 안 본다). 면제표는 비었고 **비었음을 단언**한다 |
| `Tests/EditMode/UnlockSwitchScopeAuditTests.cs` | #2. 선언 파일 밖 참조 **파일 집합 등호** + 멤버는 `UnlockAll`만 + **public 테스트 강제값 래칫(≤1)** |
| `Tests/EditMode/EntitlementFailOpenAuditTests.cs` | #3. **`Assert.Ignore` 1건 + 항상 도는 동반 경보 1건.** 명부(`TestClaimExpiryAuditTests`)에 등록 완료 |

**오프라인 실측(러너 아님)**: 프로덕션 199파일 기준 — 직렬화 필드 66개 중 위반 **0**,
해금 스위치 선언 **1파일**·외부 참조 **정확히 2파일**(`EquipmentModel.cs:180` / `ItemCatalog.cs:286`, 둘 다 `UnlockAll`),
테스트 강제값 선언 **2건 중 public 1건**, C층 표면 **0건**(⇒ #3은 Ignore로 간다).
★ **이 숫자들은 러너 결과가 아니다.** E-12-6을 읽어라.

---

## E-10. 서명 — **macOS는 1.0 하드 블로커, Windows는 「첫 업데이트 전」이 마지노선** (S절)

★ **2026-09-02 2차 개정.** 초판은 *"1.0에 필요하다"*로 양 플랫폼을 묶었다. 1차 출처 재확인(S-4·S-6) 결과 **Windows만 완화**됐다. 근거는 「OV가 첫 경고를 없애 준다」가 **아니라**(그건 거짓이다) 「이월할 평판이 아직 0이라 지금 서명하지 않아 잃는 것이 없다」이다.

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

### S-4. 무엇을 사고 무엇을 사지 않는가 — ★ 2026-09-02 **2차 정정**(1차 출처 직접 확인)

> **초판은 틀렸다.** 초판은 「OV / Azure Artifact Signing」을 **한 칸에 묶어** *"채택 권고 · 월 $9.99부터 ·
> 하드웨어 토큰 불필요"*라고 적었다. 둘은 **다른 상품**이고, **한쪽은 우리에게 물리적으로 닫혀 있다.**
> `product-strategy`가 실측으로 잡아냈고, 아래는 내가 1차 출처 두 개를 직접 열어 재확인한 결과다.

#### S-4-1. 개인 개발자에게 Azure Artifact Signing 경로는 **없다** — 두 문서가 이 점에서는 일치한다

**문서 A** — MS Learn `windows/apps/package-and-deploy/code-signing-options` (갱신 2026-08-29):
> **Geographic limitation:** Azure Artifact Signing is available to organizations in the USA, Canada,
> the European Union, and the United Kingdom. **Individual developers are currently limited to the USA
> and Canada.** If you are an individual developer outside those regions, see OV certificates below.

**문서 B** — MS Learn `azure/artifact-signing/quickstart` § Prerequisites (갱신 2026-08-11):
> Public Trust certificates are available to organizations in the United States, Canada, the European
> Union, the United Kingdom, Australia, New Zealand, Japan, **South Korea**, Singapore, Switzerland,
> Norway, and Israel. **Individual developers must be located in the United States or Canada.**
> These geographic restrictions do not apply to Private Trust certificates.

★ **두 문서는 「개인」에 대해 글자 하나까지 같다** — 미국·캐나다뿐. **한국 개인 개발자에게 월 $9.99 경로는 없다. 확정.**

#### S-4-2. ★ 조직 목록에서만 두 문서가 어긋난다 — 그리고 그 차이가 우리에게 **정확히** 걸린다

| | 조직 허용 국가 | 한국 포함? |
|---|---|---|
| 문서 A (2026-08-29) | USA · Canada · EU · UK | ❌ |
| 문서 B (2026-08-11) | USA · Canada · EU · UK · 호주 · 뉴질랜드 · 일본 · **한국** · 싱가포르 · 스위스 · 노르웨이 · 이스라엘 | ✅ |

**즉 「사업자등록이 있는가」가 연 $150~300 + 하드웨어 토큰과 월 $9.99를 가른다 — 만약 문서 B가 맞다면.**
★ **미확인.** 어느 쪽이 현행인지 문서만으로는 못 가린다. FAQ 자신이 그 해소법을 지정한다:
> *"What if my country/region isn't listed in country/region drop-down list on the Identity validation page?
> Check Pre-requisites to get the list of supported countries/regions for onboarding."*
⇒ **Azure 포털의 국가 드롭다운이 최종 판정자다.** 이건 계정을 만들어 봐야 알 수 있고, 그건 사용자 결정이다.

**그리고 조직 경로는 「$9.99만 내면 되는 문」이 아니다** (전부 문서 B 원문):
- `Website url` — *"the website that belongs to the legal business entity"*
- `Primary Email` — *"a monitored email address **on a domain owned by the legal business entity**"*
- `Business Identifier` — 사업자 식별자
- 처리 기간 *"from 1 to 20 business days"*
- FAQ: *"Artifact Signing doesn't support free, trial, or sponsored Azure subscriptions … you must have a
  **paid Azure subscription**."*

⇒ **사업자등록 + 회사 소유 도메인 + 유료 Azure 구독**이 한 세트다. 이걸 서명 하나 때문에 만들 것인지는
**내 소관이 아니라 `product-strategy`·리더·사용자의 결정**이다. 나는 "닫혀 있는 문을 열려 있다고 적었던 것"을
정정할 뿐이다.

#### S-4-3. 정정된 판정표

| | 판정 | 근거(1차 출처) |
|---|---|---|
| **Azure Artifact Signing** (개인 자격) | ★ **불가** | S-4-1. 미국·캐나다 한정 |
| **Azure Artifact Signing** (사업자 자격) | ★ **가능성 있음 — 미확인** | S-4-2. 문서 두 개가 어긋난다. 포털 드롭다운으로 확인 |
| **OV 인증서** | **현실적으로 유일한 경로** | 문서 A: *"Cost: Typically **$150–300/year** … "* / *"**HSM requirement:** As of June 2023, the CA/Browser Forum requires private keys for OV certificates to be stored on a **hardware security module (HSM) or hardware token**."* |
| **EV 인증서** | **사지 않는다** | 문서 A: *"That behavior was removed in 2024."* / *"Paying the EV premium (**$400+/year**) solely to avoid SmartScreen warnings is **no longer justified**"* |
| **Steam DRM 래퍼** | **쓰지 않는다** | ① F2P라 보호할 exe가 없다 ② 래퍼가 `.bind` 섹션을 추가하고 체크섬을 다시 계산 → **먼저 한 서명이 무효화**된다. 굳이 쓴다면 **래핑 후 서명** |
| **자체 서명(self-signed)** | **기각** | 문서 A: *"Blocks installation for public users"* — 무서명보다 나쁘다 |
| **난독화·패킹** | **기각** | 선 3 정면 위반. §E-8-1에 의해 얻는 것도 없다 |

#### S-4-4. ★ HSM/토큰이 개인 개발자에게 갖는 진짜 비용은 **돈이 아니라 절차다**

CA/B Forum(2023-06) 이후 OV 개인키는 **물리 토큰 또는 클라우드 HSM**에 있어야 한다.
**물리 토큰은 CI에서 서명할 수 없다** — 릴리스마다 사람이 토큰을 꽂고 서명해야 한다.
그 마찰이 하는 일은 하나다: **「이번 핫픽스는 그냥 무서명으로 낸다」를 만든다.**
그리고 S-2가 방금 확인한 대로 **평판은 서명된 릴리스에만 이월된다** — 건너뛴 릴리스는 그 사슬을 끊는다.

> **S-4-a (구매 시 조건):** OV를 살 때 **클라우드 HSM 서명 옵션이 있는 CA**를 고른다.
> 이건 편의가 아니라 **평판 사슬을 사람 손에 맡기지 않기 위한 요구사항**이다.

---

### S-5. 비용표 (정의서 요구 형식) — ★ 정정판

| 조치 | 구현 비용 | 백신 위험 | 유저 마찰 | 오탐 시 피해 |
|---|---|---|---|---|
| macOS 서명+공증 | Apple **$99/년**(iOS 타깃에 어차피 필요 — 신규 비용이 아니라 선지출) + 파이프라인 1회 + 엔타이틀먼트 3종 확인 | **감소** | **감소**(현재 Gatekeeper `rejected` 실측) | 없음 |
| **Windows OV 서명** | ~~$120/년~~ → **$150~300/년** + **HSM/토큰**(S-4-4) + 서명 단계 1개 | **감소** | **감소** | 없음 |
| Azure Artifact Signing | 월 $9.99 · 토큰 불필요 · CI 연동 | 감소 | 감소 | — → ★ **개인 자격 불가**(S-4-1). 사업자면 재검토 |
| EV 인증서 | ~~$300~600/년~~ → **$400+/년** | 변화 없음 | 변화 없음 | — → **기각** |
| Steam DRM | 빌드 단계 1개 | 증가(패커 유사) | 스팀 미실행 시 실행 불가 | **높음** → **기각** |
| 세이브 무결성 검증 | 중 | 증가 | — | ★ **정당한 유저 잠금** → **기각**(§E-8) |

---

### S-6. ★ 리더 질의에 대한 판정 — **「Windows 데드라인 = 첫 업데이트 전」에 동의한다. 단 근거를 바꿔라.**

`product-strategy`가 문서 A의 같은 페이지에서 찾아낸 줄이 맞다. 1차 출처 표 원문:

| Option | SmartScreen behavior |
|---|---|
| Azure Artifact Signing | ⚠️ *Reputation builds over time; **initial warnings expected*** |
| **OV certificate** | ⚠️ ***Same as Azure Artifact Signing** — reputation builds over time* |
| EV certificate | ⚠️ *Same as OV since 2024 — **no longer instant bypass*** |
| **No signature** | ❌ ***Strong SmartScreen block**; enterprises may block entirely* |

본문:
> New files can show a SmartScreen warning until they accumulate sufficient reputation. Azure Artifact
> Signing does **not** provide instant SmartScreen trust, but **signing consecutive releases with a
> consistent publisher/signing identity lets publisher reputation build over time, so later releases can
> inherit trust.**

> (OV) As with any trusted certificate, signing releases with a consistent identity lets publisher
> reputation accumulate **across versions, rather than starting from zero each time.**

#### 동의하는 부분

1. **서명이 사는 것은 「첫 경고 제거」가 아니라 「평판의 이월」이다** — 1차 출처가 정확히 그렇게 말한다.
   ★ 초판 S-4 마지막 문단이 **이미 그렇게 적혀 있었다**(*"서명의 진짜 값어치는 「최초 경고 회피」가 아니라
   「평판의 이월」이다"*). 이번 실측은 그 문장을 **뒤집은 것이 아니라 굳혔다.**
2. **사용자 확정 「외부 배포 이력 0」이 결정적이다.** 이월할 평판이 **아직 존재하지 않는다.**
   ⇒ 지금 서명하지 않아서 **잃는 것이 0이다.** 1.0을 무서명으로 내도 **되돌릴 수 없는 손실이 없다.**
   (이건 강한 논거다. 평판은 시간이 만들지만, 없는 평판은 잃을 수도 없다.)

#### 동의하지 않는 부분 — **근거를 SmartScreen에 두면 안 된다**

초판 S-2의 제목이 *"서명한다. 단 SmartScreen 때문이 아니다"*였다. **SmartScreen 평판은 원래 우리 결론의
근거가 아니었으므로, 그 사실이 바뀌어도 결론이 자동으로 따라 움직이지 않는다.** 남은 두 관문은 이렇다:

| 관문 | 평판으로 해결되는가 | 왜 |
|---|---|---|
| **Smart App Control (Win11)** | ★ **사실상 아니다** | MS 원문은 *"blocks unsigned files **unless the file has a positive reputation**"*이다. 즉 이론상 무서명도 평판을 쌓을 수 있다. 그러나 **무서명 평판은 파일 해시 단위**라 **빌드할 때마다 0으로 돌아간다.** 자주 업데이트하는 상주 앱에서 그건 "영원히 못 쌓는다"와 같다 |
| **백신 휴리스틱(사용자 실기 = AhnLab V3)** | **아니다** | 평판 모델이 다르다. **릴리스마다 새로 판정된다.** 그리고 우리 Win32 표면(S-3)이 정확히 애드웨어/키로거 모양이다 |

★ 그리고 **되돌릴 수 없는 손실은 기술이 아니라 사람 쪽에 있다.**
`MARKET_LANDSCAPE`: **Shimeji를 죽인 건 기능이 아니라 백신 경고였다.**
첫 리뷰나 커뮤니티 글에 *"이거 바이러스 아니냐"*가 한 번 박히면 **나중에 서명해도 그 글은 지워지지 않는다.**
평판은 이월되지만 **불신도 이월된다.**

#### 최종 판정 (리더 결재용)

| 플랫폼 | 데드라인 | 성격 |
|---|---|---|
| **macOS** | **1.0** | ★ **하드 블로커, 변화 없음.** 실측: 애드혹 서명 + `spctl` **rejected**(양성 대조 Calculator.app = accepted). 이 상태로는 공증 자체가 불가능하다. 비용 $99/년은 iPad/iPhone 타깃에 **어차피 필요한 선지출** |
| **Windows** | **목표 1.0 / 마지노선 「첫 업데이트 전」** | **연기는 허용, 무기한은 불가** |

- **연기해도 되는 이유**: 외부 배포 이력 0 → 이월할 평판 0. 스팀 단독 → MOTW가 대부분 비켜진다(★ 이 줄은
  여전히 **1차 출처 없음 · 업계 통념**이다. E-12-3 참조).
- **마지노선이 「첫 업데이트 전」인 이유**: 평판은 **서명을 켠 시점부터** 쌓인다. 업데이트를 여러 번 낸 뒤
  켜면 그 사이 릴리스가 전부 평판 0으로 소모되고, 그 기간의 백신 오탐도 그대로 누적된다.
- ★ **데드라인이 즉시 1.0으로 앞당겨지는 조건 (이건 조건부가 아니라 자동이다)**:
  **스팀 밖 직배포를 하는 순간**(itch.io · 홈페이지 zip · 디스코드 첨부 등). 그때부터 MOTW가 붙고,
  무서명은 표 마지막 행 그대로 ***"Strong SmartScreen block"***이 된다.
  `product-strategy`가 채널을 늘리는 판단을 할 때 **이 줄을 함께 올려야 한다.**

---

### S-7. ★ 신규 — **앱 신원 동결**: 서명 평판과 세이브 경로는 **같은 문자열**이 결정한다

`companyName`/`productName`/`applicationIdentifier`는 서명·평판의 열쇠이면서 **동시에**
`Application.persistentDataPath`를 결정한다. 그래서 이 절은 S절(서명)에 속한다.

#### S-7-1. 실측 (2026-09-02)

| 항목 | 값 |
|---|---|
| `ProjectSettings.asset` | `companyName: DefaultCompany` → **`Vibelab`** / `applicationIdentifier: {}` → **`com.Vibelab.StickMate`**(Standalone·iPhone) / `overrideDefaultApplicationIdentifier: 0 → 1` |
| macOS 세이브 실경로 형태 | **`~/Library/Application Support/<companyName>/<productName>`** — ★ 디스크로 확인. `DefaultCompany/StickMate/`가 실재하고 **`com.DefaultCompany.StickMate/`는 없다**(번들 ID 형태가 아니다) |
| 이관 상태(macOS) | 리더가 `Vibelab/StickMate/`로 **복사 완료, 원본 보존**. `stickmate_character.json`(1279B)·`.prev.json`·v8/v9 백업 확인. ★ 고아 `stickmate_character.json.67542.writing`은 **이관되지 않았고 그게 맞다** |
| PlayerPrefs | ★ **프로덕션에 실재한다** — `Interaction/GearRadialMenuWidget.cs`의 `StickMate.GearMenu.OnboardingSeen.v1`. macOS `~/Library/Preferences/unity.<회사>.<제품>.plist` / **Windows `HKCU\Software\<회사>\<제품>`(레지스트리)** |
| 작업표시줄 원복 원장 | **`stickmate_reserved_bar_restore.json`이 양쪽 디렉터리 어디에도 없다** |

#### S-7-2. ★ 원장 위험 판정 — **오늘은 0이다. 그런데 이유가 중요하다**

리더 질의: *"원장이 안 읽히면 사용자 작업표시줄 자동 숨김이 꺼진 채 복구 불가 = 원칙 3의 승인된 예외가
사후에 무너지는 유일한 경로."* **논리는 정확하다. 그런데 오늘 그 경로는 열려 있지 않다.**

**실측**: `ReservedBarRevealDirector.RunStartup` / `RunShutdown`의 **프로덕션 호출처가 0건**이다.
`Platform/` 밖의 어떤 프로덕션 파일도 `ReservedBar`를 **한 글자도 언급하지 않는다.**
> **양성 대조**(0건이 「없다」인지 「못 본다」인지 가른다): 같은 명령으로 `.Save(` **22건**, `.Load(` **1건**이
> 잡힌다. 스캐너는 살아 있고, `RunStartup`은 정말로 0건이다.

⇒ **승인된 예외(CLAUDE.md 원칙 3)는 지금 한 줄도 실행되지 않는다.** 원장이 만들어진 적이 없으니
**이름이 바뀌어도 고아가 될 원장이 없다.** 디스크 실측(파일 부재)이 이 결론과 일치한다 — 두 축이 같은 답을 냈다.

#### S-7-3. 그러므로 위험은 **미래형**이다 — 그리고 배선 라운드가 그 문을 연다

> **S-7-a (필수 · 배선 라운드 선행조건):** `RunStartup`을 배선하기 **전에**
> `companyName` · `productName` · `applicationIdentifier`를 **동결**한다.
> 배선 **후**의 이름 변경은 **열린 원장을 고아로 만들고**, 그 결과가 정확히
> 「사용자 작업표시줄 자동 숨김이 꺼진 채 복구 불가」다.
> ★ **세이브와는 성질이 다르다**: 세이브가 안 옮겨지면 사용자가 **즉시 알아챈다**(캐릭터가 초기화된 것처럼 보인다).
> **고아 원장은 사용자가 영원히 모른다** — 작업표시줄이 왜 안 숨겨지는지 우리 앱과 연결짓지 못한다.
> **조용한 실패이므로 더 비싸다.**

> **S-7-b (유지):** 원장이 **없을 때의 동작이 「아무것도 하지 않는다」**임을 유지한다.
> 현재 그렇다(`ReservedBarLedgerState.None` → 시스템을 바꾸지 않는다).
> **이 성질이 신원 이사에 대한 유일한 구조적 방어**다 — 경로가 바뀌어 원장을 못 읽으면
> 앱은 아무 일도 하지 않고, 최악이 「기능이 안 켜진다」이지 「사용자 설정 파괴」가 아니다.

> **S-7-c (조건부):** 배선 후에 이름 변경이 불가피해지면, **그 라운드 안에서** 구 경로의 원장을 읽어
> 갚는 **1회성 회수**를 넣고 **다음 릴리스에서 지운다**. 오늘은 불필요하다(원장이 존재하지 않는다).

#### S-7-4. ★ 정정 — 원장 주석이 실재보다 넓다 (`game-architect` 지적 확인. **그리고 지적보다 더 나쁘다**)

`Platform/ReservedBarRestoreLedger.cs:70-73`:
> *"(1) Windows에서 PlayerPrefs는 **레지스트리**에 쓴다 — 이 저장소는 레지스트리 쓰기를 감사로 금지하고
> 있고(`UserAssetImmutabilityAuditTests`), 그 금지를 이 기능 때문에 흐리게 만들 이유가 없다."*

**두 군데가 사실과 다르다.**

1. 그 감사가 금지하는 것은 **Win32 레지스트리 쓰기 API 선언 11종**
   (`RegSetValue`·`RegCreateKey`·`RegDeleteKey`·`RegDeleteValue`·`RegDeleteTree`·`RegSaveKey`·
   `RegRestoreKey`·`RegLoadKey`·`RegReplaceKey`·`RegSetKeySecurity`·`RegUnLoadKey`)이다.
   ★ 실측: 그 감사 파일 전체에 **`PlayerPrefs`가 0건**이다. PlayerPrefs 경유 레지스트리 쓰기는 **사거리 밖**이다.
2. ★ 더 중요한 것: **이 저장소는 이미 레지스트리에 쓰고 있다.**
   `GearRadialMenuWidget.cs`의 `PlayerPrefs.SetInt`가 Windows에서 `HKCU\Software\<회사>\<제품>`을 만든다.
   그러므로 *"이 저장소는 레지스트리 쓰기를 금지하고 있다"*는 **문장 자체가 거짓**이다.

**판정: 원칙 3 위반은 아니다.** `HKCU\Software\<우리 회사>\<우리 제품>`은 OS가 이 앱에 배정한 **자기 자리**이고
남의 자산이 아니다(`persistentDataPath`와 같은 논리). **고쳐야 할 것은 코드가 아니라 문장이다.**

★ **왜 문장 하나를 굳이 고치라고 하는가**: 그 파일은 CLAUDE.md가
*"원칙 3 위반으로 오해해 되돌리기 전에 먼저 읽으라"*고 지정한 문서군의 일부다.
**되돌리기 전에 읽을 문서에 과장이 있으면, 다음 사람이 "감사가 막아 주고 있다"고 믿고 검사를 생략한다.**
그 사람은 PlayerPrefs 한 줄을 추가하면서 아무 저항도 만나지 않는다.

> **권고 문구**(프로덕션 `.cs`는 내가 못 고친다 — `coder`/`dev-platform` 배정 요청):
> *"(1) Windows에서 PlayerPrefs는 레지스트리(`HKCU\Software\<회사>\<제품>`)에 쓴다. 원칙 3 위반은
> 아니지만(우리에게 배정된 자리다) **신원 문자열이 바뀌면 함께 이사한다** — 크래시 복구용 흔적을 그런
> 저장소에 둘 수는 없다. ★ 참고: `UserAssetImmutabilityAuditTests`가 막는 것은 **Win32 레지스트리 쓰기 API
> 선언**이지 PlayerPrefs가 아니다. 이 저장소에는 실제로 PlayerPrefs 사용처가 1곳 있다
> (`Interaction/GearRadialMenuWidget`)."*

#### S-7-5. `CFBundleVersion = "0"` — 공증·업데이트 식별 (실측 확인)

`Builds/macOS/StickMate.app/Contents/Info.plist` 실측(양성 대조: 없는 키 조회 시 정상적으로 오류가 난다):

| 키 | 값 |
|---|---|
| `CFBundleVersion` | **`0`** ← 출처는 `ProjectSettings.asset`의 `buildNumber: Standalone: 0` |
| `CFBundleShortVersionString` | `1.0` (마케팅 버전, `bundleVersion: 1.0`) |
| `CFBundleIdentifier` | `com.DefaultCompany.StickMate` ← ★ **이 빌드는 개명 전 것이다**(08-31) |

- **"0" 때문에 공증이 지금 당장 거절되는지는 미확인이다.** Apple이 `CFBundleVersion`을 요구하는 것은 확실하지만,
  **"0"이 위법 형식이라는 1차 근거를 찾지 못했다.** 지어내지 않고 미확인으로 둔다.
- **그러나 단조 증가는 우리가 지켜야 하는 쪽이다.** 업데이트 판별 · 크래시 리포트 그룹핑 ·
  (향후 iPad/iPhone) App Store 제출이 전부 이 값으로 릴리스를 구분한다.
  **1.0을 `0`으로 내면 1.0.1을 무엇으로 부를지 그때 급하게 정해야 한다.**

> **S-7-d (권고, 판정은 리더 · 구현은 `dev-platform`):** 1.0 **이전에**
> `buildNumber.Standalone`을 **1에서 시작해 릴리스마다 +1**로 고정하고 **되돌리지 않는다.**
> `bundleVersion`(1.0)은 마케팅 버전이라 별개 축이다.
> ★ **서명·공증 파이프라인과 같은 라운드에 처리한다.** 따로 하면 두 번째 릴리스에서
> "같은 버전 두 개"가 생기고, 그건 공증 이력에서 되돌릴 수 없다.

#### S-7-6. 사용자 안내문 — **무엇이 들어가야 하는가** (리더 요청)

★ **Windows 쪽은 사용자가 직접 해야 한다**(이 머신에 Windows가 없어 리더가 복사하지 못했다).
아래는 문안 초안이 아니라 **들어가야 하는 것과 그 근거**다. 문구 다듬기는 `ux-designer` 소관이다.

**반드시 들어가야 하는 것 5가지 — 각각 보안·무결성 근거가 있다**

| # | 들어갈 것 | 왜 (보안·무결성 근거) |
|---:|---|---|
| 1 | **"복사"라고 쓴다. "이동/잘라내기"라고 쓰지 않는다** | 원칙 3의 정신은 「되돌릴 수 있음」이다. **우리가 시키는 조작도 되돌릴 수 있어야 한다.** 잘라내기를 시키면 사용자가 중간에 실수했을 때 복구 지점이 사라진다 |
| 2 | **옛 폴더를 지우지 말라고 명시** | 위와 같은 이유. 리더도 macOS에서 원본을 보존했다 — 사용자에게 다른 기준을 요구하지 않는다 |
| 3 | ★ **`*.json.<숫자>.writing` 파일은 복사하지 말라고 명시** | 중단된 저장의 잔해다. 실제로 디스크에 남아 있었다(`stickmate_character.json.67542.writing`, `SECURITY_MODEL` §4-1-4). 이걸 옮기면 새 폴더가 첫날부터 오염된 상태로 시작한다 |
| 4 | **안 옮겼을 때 무슨 일이 생기는지** — *"새 캐릭터로 시작합니다. 예전 진행도는 지워지지 않고 옛 폴더에 그대로 있습니다"* | ★ **이게 가장 중요하다.** 이 안내를 놓친 사용자가 앱을 켜면 **"내 캐릭터가 사라졌다"**로 읽는다. 그 순간의 공포가 실제 피해보다 크고, 그때 사용자가 하는 행동(재설치·폴더 삭제)이 **진짜 데이터 손실을 만든다** |
| 5 | ★ **레지스트리는 건드리지 말라고 명시** | Windows에서 톱니 메뉴 첫 사용 힌트(`StickMate.GearMenu.OnboardingSeen.v1`)가 `HKCU\Software\<회사>\<제품>`에 있고, 이건 **함께 옮겨지지 않는다.** 결과는 **힌트가 한 번 더 뜨는 것**뿐이다. **얻는 것이 힌트 한 번인데 잃을 수 있는 것이 사용자의 레지스트리다** — 시키면 안 된다. "한 번 보고 넘기면 끝"이라고 적는다 |

**넣지 말아야 할 것**
- 명령줄·`reg` 편집·스크립트. **이 앱은 사용자에게 그런 것을 시키는 앱이 아니다**(비침해 원칙의 연장선).
- "보안상 필요합니다" 같은 설명. 사실이 아니다 — 이건 **회사명 설정 변경의 부수 효과**이지 보안 조치가 아니다.
  거짓 근거를 대면 다음에 진짜 보안 안내를 할 때 신뢰가 없다.

**경로 (실측 확인)**
- macOS: `~/Library/Application Support/DefaultCompany/StickMate/` → `.../Vibelab/StickMate/` — **완료됨**
- Windows: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\StickMate\` → `...\Vibelab\StickMate\` — **사용자 조치 필요**
- 옮길 파일: `stickmate_character.json`(진행도 본체) · `stickmate_character.prev.json` ·
  `character_save.v8.backup.json` · `character_save.v9.backup.json` — **안전망까지 전부**

**★ 리더가 다음 macOS 빌드 첫 실행 뒤 반드시 확인할 것 (거짓 통과 방지)**
`~/Library/Application Support/Vibelab/StickMate/stickmate_character.json`의 **mtime이 갱신되는가.**
갱신되지 않고 **다른 디렉터리가 새로 생기면**(예: `com.Vibelab.StickMate/`) **복사 대상이 틀린 것이다.**
오늘 실측으로는 `<회사>/<제품>` 형태가 맞다 — 디스크에 `DefaultCompany/StickMate/`가 실재하고
`com.DefaultCompany.StickMate/`는 **없다**. 그러나 그건 **개명 전 빌드로 잰 값**이고,
`overrideDefaultApplicationIdentifier`가 `0 → 1`로 바뀌었다. **한 번은 실물로 확인해야 한다.**

★ **부수 관측(리더 장부와 대조 필요)**: `~/Library/Preferences/unity.Vibelab.StickMate.plist`가
2026-09-02 23:50에 생겼고 `unity.player_session_count`가 **325 → 326**이다.
즉 **새 이름으로 Unity 세션이 한 번 돌았다.** 플레이어 빌드인지 배치모드 에디터인지는 **미확정**이다
(양쪽 다 이 파일을 건드린다). 확인 시점에 실행 중인 인스턴스는 **0개**였다
(`pgrep` 실측, 양성 대조로 없는 이름이 0을 내는 것 확인).

---

## E-11. 플랫폼 영향

- **Windows 영향: 함께 검토함.** S-2·S-3·S-4·S-6이 Windows 전용 판정이다.
  실측은 **출하 zip(`Builds/StickMate-Windows-20260902b.zip`, 09-02 12:42)의 IL을 직접 디스어셈블**해
  이뤄졌다 — 활성 빌드 타깃(macOS) 사각지대를 우회한 측정이다.
  ★ **이번 라운드 추가**: ① 세이브·PlayerPrefs 경로가 **회사명 변경으로 함께 이사한다**
  (`AppData\LocalLow\<회사>\<제품>` · `HKCU\Software\<회사>\<제품>`) — **사용자 수동 조치 필요**(S-7-6).
  ② 작업표시줄 원복 원장은 **아직 한 번도 쓰인 적이 없다**(배선 0건, S-7-2) — 오늘 고아 위험 0.
  ★ 미해결 이월: `Platform/Windows/WindowsGameProcessProbe.cs:159`가 **남의 exe 풀 경로**(계정 실명 포함)를
  로그에 인쇄한다(`SECURITY_MODEL` §1-3-b, **아직 미수정**).
- **macOS 영향: 함께 검토함.** S-1·S-7-5가 macOS 전용이고 **S-1은 1.0 차단 후보**다.
  ★ **이번 라운드 추가**: 세이브 이관은 **완료**(원본 보존). `CFBundleVersion = 0` 확인(S-7-5).
  ★ 미해결 이월: `Platform/MacOS/MacWindowService.cs:1516,1534`가 남의 앱 이름을 로그에 인쇄한다(미수정).
- **E-9 감사 3건은 플랫폼 중립이다.** 전부 **소스 텍스트 스캔**이라 활성 빌드 타깃과 무관하게 같은 것을 본다
  (리플렉션이면 반대편 타깃의 절반을 구조적으로 못 본다 — CLAUDE.md 활성 빌드 타깃 규칙).
  win·osx **양쪽 크로스 컴파일 0에러** 확인(각 5/5 유닛).
- **엔타이틀먼트 조회 자체는 플랫폼 분기가 없다**(스팀이 양 플랫폼에 같은 API를 준다).
  단 **정책 판정(E-1~E-3)은 반드시 `Platform/` 중립 위치**에 둔다 — `FullscreenSuspendPolicy.cs` 사고 재발 자리.
- **모바일**: 1.0 밖. E-1~E-4는 StoreKit에 그대로 재사용된다(C층 저장소만 바뀐다).
  ★ 단 S-7-d(빌드 번호 단조 증가)는 **App Store 제출에서 강제**되므로 모바일 라운드 전에 정해져 있어야 한다.

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
6. **테스트를 돌리지 않았다**(Unity 배치모드가 다른 라운드 대기열에 있다).
   E-9 감사 3건은 **Unity 동봉 Roslyn 크로스 컴파일로 win·osx 양쪽 0에러**를 확인했고
   (`StickMate.Tests.EditMode` 143소스, 5/5 유닛, 신규 4파일이 소스 목록에 실제로 포함됨을
   rsp에서 대조 — 양성/음성 대조 포함), **스캔 결과는 파이썬 독립 재구현으로 예측**했다.
   ★ **그 예측은 러너를 대체하지 않는다.** 재구현과 본 구현이 같은 머릿속에서 나왔으므로
   TEAM.md가 기록한 열 번째 형태(생성기와 검사기가 같은 함정에 같이 빠진다)에 노출돼 있다.
   **러너를 반드시 돌려라.**
7. ★ **S-4-2의 조직 국가 목록 충돌은 문서로 해소되지 않는다.** Azure 포털 국가 드롭다운이 최종 판정자이고,
   그건 계정 생성이 필요하다 — **사용자 결정 사항**이다.
8. **"0"인 `CFBundleVersion`이 공증을 실제로 막는지 미확인**(S-7-5). 결론(단조 증가로 고정)은
   그 미확인에 의존하지 않는다.
