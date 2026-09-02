# StickMate 보안 모델 — 1차 감사·판정 (2026-09-02)

담당: `security`(신설). 방어 전용 — 공격 도구·크랙·우회 코드는 만들지 않았다.
프로덕션 `.cs` **수정 0건**. 이 문서는 감사·판정이고 구현은 리더가 배정한다.

> 사용자 지시(2026-09-02): *"유료결제나 경험치 축적 등 아이템획득에 대한것들도 보안이 중요할거같아.
> 아무나 막 획득하면안되니까"*

---

## 0. 한 줄 결론

**리더의 위협 모델은 옳다 — 단, 두 군데를 정정한다. 그리고 진짜 문제는 "지금 뚫려 있는 것"이 아니라
「유료 경계」와 「성장 경계」가 **이미 같은 함수·같은 파일로 합류하도록 예약돼 있다**는 것이다.**

지금 뚫린 구멍은 없다. 팔고 있는 것이 없기 때문이다. 그러나 `game-architect`의 착수 순서 **4번(v10 스키마)**과
**5번(재화·상점)**을 그대로 실행하면, 그 두 라운드가 끝나는 순간 **환경변수 한 줄로 유료 팩이 열리고,
메모장으로 소유 플래그를 켤 수 있는 상태**가 된다. 막을 시점은 지금이고, 비용은 거의 0이다.

---

# 1. 위협 모델 — 검증 결과

## 1-1. 확인된 것 (리더 초안대로)

| 주장 | 판정 | 근거 |
|---|---|---|
| 네트워크 API 사용 0건 (**우리 코드**) | **참** | `UnityWebRequest`/`HttpClient`/`Socket`/`System.Net`/`WebSocket` 등 23개 패턴 전수 0건 |
| 리더보드·랭킹·소셜·공유 없음 | **참** | `Leaderboard`/`Ranking`/`Social`/`Achievement`/`Steamworks`/`Multiplayer` 전부 0건. `Share` 43건은 전부 `FileShare`·`SharedApplication` 오탐 |
| 세이브가 남에게 보이는 경로 없음 | **참** | 로컬 파일 1개, 전송 경로 0건, 클립보드·스크린샷 내보내기 0건 |
| DLC 6팩은 아직 코드에 없다 | **참** | `Rarity`/`SetBonus`/`coinBalance`/`Currency`/`Purchase`/`Entitle` **전부 0건**. `DLC` 18건은 **18건 전부 주석** |
| 남의 창 제목·파일명을 **그리지** 않는다 | **참** | `.text =` 대입 118건 중 외부 이름이 들어가는 것 0건 |

### 양성 대조 (이 저장소가 반복해 당한 함정이라 전부 붙였다)

- **grep 방식 검증**: 같은 명령이 `class`(234) `void`(1390) `File.`(32)를 찾는다. 추가로 스크래치에
  `UnityWebRequest`를 쓰는 가짜 파일을 만들어 **같은 명령으로 잡히는 것을 확인**했다.
- **`strings` 방식은 한 번 실패했고, 그 실패를 그대로 기록한다.** `Builds/macOS/StickMate.app/Contents/MacOS/StickMate`
  (132KB)에 `strings | grep Unity`가 **0건**을 냈다. 이건 "깨끗하다"가 아니라 **그 파일이 Unity 런처
  스텁**이라는 뜻이다(팀 거짓통과 사례 #7과 같은 함정). 실제 엔진은
  `Contents/Frameworks/UnityPlayer.dylib`(61MB)이고 거기서 `Unity` 21,746건이 나온다.
  **첫 0건을 그대로 보고했다면 그것이 오늘의 10번째 거짓 통과였다.**
- **이미 있는 자동 잠금**: `Tests/EditMode/OfflineFirstNetworkAuditTests.cs`가 소스 파일을 직접 읽어
  금지 API를 스캔하고, **네거티브 컨트롤**(가짜 위반 소스를 같은 스캔 함수에 흘려 잡히는지 확인)까지
  갖췄으며, 전송 계열 화이트리스트가 0건임을 별도 테스트로 고정한다. **좋은 감사다.** 아래 1-2가 그
  감사의 사각지대다.

## 1-2. ★ 정정 1 — "네트워크 0"은 **우리 코드**에 대해서만 참이다. 출하되는 플레이어는 미확인이다

```
ProjectSettings/ProjectSettings.asset:93:  submitAnalytics: 1      ← 켜져 있다
UnityPlayer.dylib                      :  "enableHWStatistics"    ← 엔진에 그 기능이 실재한다
```

`submitAnalytics`는 구 **"Disable HW Statistics"** 의 반대 스위치이고, Unity 스태프 설명으로는
*"deviceInfo(하드웨어 통계)를 보내는 단일 이벤트"*다. 우리 `UnityConnectSettings.asset`은
Analytics를 전부 `m_Enabled: 0`으로 꺼 두었고, 같은 스태프가 *"이제는 Analytics를 안 켜면 같은 효과"*
라고 답했다 — **그래서 안 보낼 가능성이 높다. 그러나 그건 포럼 답변이고 버전 의존적이다.**

**★ 여기서 중요한 것은 결론이 아니라 감사의 사각지대다.**
`OfflineFirstNetworkAuditTests`는 `Assets/_Project/Scripts/` 아래 **`*.cs`만** 읽는다
(`Directory.GetFiles(scriptsRoot, "*.cs", AllDirectories)`). `ProjectSettings.asset`도
`Packages/manifest.json`도 **한 번도 보지 않는다**. 즉 **우리 코드의 0건은 잠겨 있지만, 엔진이 켜 둔
스위치는 그 잠금 바깥에 있다.**

- **판정: 미확인.** 정적 증거만으로는 출하 빌드가 부팅 시 이벤트를 보내는지 확정할 수 없다.
- **확정 방법 1건**: 출하 빌드를 깨끗한 환경에서 첫 실행하며 패킷 캡처. **나는 돌리지 않았다**(앱 실행은
  리더 승인 사항이고 `dev-platform`이 Unity 배치모드를 잡고 있다).
- **왜 중요한가**: `MARKET_LANDSCAPE.md`가 **"네트워크 0 · 권한 0"을 이 앱의 최대 차별점**으로 세워 두었다.
  마케팅이 그 문장을 스토어 페이지에 쓰는 순간, 검증되지 않은 공개 주장이 된다. 경쟁자·리뷰어가
  패킷 한 번 잡으면 끝나는 종류의 주장이다.

## 1-3. ★ 정정 2 — 그리지는 않는다. 그러나 **로그에는 남는다** (출구가 두 개다)

리더가 확인된 사실로 적은 *"남의 창 제목·파일명을 한 글자도 그리지 않는다"* 는 **렌더에 대해서는 참**이다.
그러나 **렌더가 유일한 출구가 아니다.**

**(a) 세이브 경로가 Player.log에 그대로 찍힌다** — 리더의 브리핑대로였다. 실측 확인:
```
Player.log: [성장] 준비 완료 — 스틱메이트 Lv.4 (145/429 XP). 저장 파일=불러옴
            (/Users/kjmoon/Library/Application Support/DefaultCompany/StickMate/stickmate_character.json).
출처: Interaction/CharacterProgressionDirector.cs:82
```
(양성 대조: 같은 grep이 그 로그에서 `/Users/` 4건, `StickMate` 5건을 찾는다 — 탐지력 있음.)

**(b) ★ 더 중요한 것 — 남의 앱 신원이 로그에 들어간다. 양 플랫폼 모두.**

| 플랫폼 | 파일 | 로그에 들어가는 것 |
|---|---|---|
| Windows | `Platform/Windows/WindowsGameProcessProbe.cs:159` → `Win32WindowService.cs:1870` | **`전경 실행 파일={전체 경로}`** — 남의 exe **풀 패스**. `C:\Users\<실명>\...` 가 통째로 |
| macOS | `Platform/MacOS/MacWindowService.cs:1516,1534` → `:1403` | `판정 근거 창 = '{owner}'` — 남의 앱 **이름** |

둘 다 `Debug.Log($"[전체화면판정] ... — {reason}")`로 나간다. Windows 쪽이 더 나쁘다 —
경로에 **Windows 사용자 계정명**이 포함된다.

- **이건 원칙 3 위반이 아니다.** 읽기 전용이고, 최소 권한(`PROCESS_QUERY_LIMITED_INFORMATION`)이며,
  레지스트리는 `KEY_READ`만 쓴다. 그 파일은 **원칙 3을 매우 잘 지키고 있다**(쓰기 API를 선언조차 안 하고,
  그 사실을 테스트가 잠근다). 문제는 침해가 아니라 **로그 위생**이다.
- **왜 지금 말하는가**: 이 팀의 표준 절차가 *"릴리스 빌드를 켜고 Player.log로 확인한다"* 이고,
  버그 신고 때 사용자가 그 로그를 **개발자에게 보낸다**. 그리고 `TEAM.md`가 마케팅 캡처를
  *"깨끗한 테스트 환경에서 — 남의 창 제목·파일명이 찍힌다"* 며 이미 경계하고 있다. **그 경계가
  화면에 대해서만 걸려 있고 로그에는 안 걸려 있다.**

## 1-4. 정정 후의 위협 모델 — 이걸 기준으로 이하 전부를 판정했다

| 위협 | 누가 무엇을 잃는가 | 판정 |
|---|---|---|
| 세이브를 고쳐 레벨·XP·동전을 올린다 | **자기 자신뿐.** 리더보드·거래·멀티 전부 없음(실측) | **막지 않는다** (§4) |
| **유료 팩을 안 사고 쓴다** | **개발자 — 매출** | **★ 여기가 유일한 진짜 위협** (§2) |
| `STICKMATE_UNLOCK_ALL`이 유료 경계를 넘는다 | 오늘: 아무도. **DLC 도입 후: 개발자** | **예약된 사고** (§2-1) |
| `STICKMATE_DEVTOOLS`가 유료 경계를 넘는다 | **아무도** | **반증됨** (§2-3) |
| 로그에 남의 앱 신원·세이브 경로 | 사용자 — 프라이버시(소규모, 로컬) | **싸니까 고친다** (§5-3) |
| 출하 플레이어의 Unity HW 통계 | 사용자 — 프라이버시 / 우리 — 마케팅 주장의 신뢰 | **미확인, 측정 필요** (§5-2) |

---

# 2. 유료 경계 감사 — 이번 라운드의 본체

## 2-1. ★ `STICKMATE_UNLOCK_ALL` — 오늘은 안전하다. 그리고 그게 문제의 전부가 아니다

**릴리스 빌드에서 이 환경변수가 유료 경계를 넘는가? — 오늘은 "넘을 유료 경계가 없다".**

판정 규칙은 실제로 잘 짜여 있다(`Core/EquipmentDebugUnlock.cs`):

```csharp
public static bool ResolveUnlockAll(bool developmentConfiguration, string environmentRaw)
    => developmentConfiguration || StickMateDevTools.ResolveFromEnvironmentValue(environmentRaw);
```

- 릴리스 빌드(`UNITY_EDITOR`·`DEVELOPMENT_BUILD` 둘 다 없음) + 환경변수 미설정 → **닫힘**.
- 그 사실을 `EquipmentDebugUnlockReleaseGateTests`가 순수 함수로 잠근다(에디터에서도 릴리스 값을 재현).
- 사람이 출시 전에 상수를 되돌리는 구조가 아니다 — **컴파일 심볼이 보장한다.** 이건 잘한 설계다.
- 그래도 **릴리스 빌드 + `STICKMATE_UNLOCK_ALL=1`이면 열린다**(의도된 QA 탈출구).
  오늘 그것이 여는 것은 **레벨 게이트뿐**이고, 레벨 게이트는 무료 성장 요소다. **매출 피해 0.**

### 그런데 우회 지점이 하필 여기다

```csharp
// Core/ItemCatalog.cs:233
public bool IsOwned(StickConfig config)
    => !RequiredLevel.HasValue
       || EquipmentDebugUnlock.UnlockAll          // ← 무조건 단락시키는 OR
       || CharacterProgressionModel.Level >= RequiredLevel.Value;

// Core/EquipmentModel.cs:179
public static bool IsItemOwned(EquipmentSlot slot, int itemIndex)
    => EquipmentDebugUnlock.UnlockAll
       || CharacterProgressionModel.Level >= RequiredLevel(slot, itemIndex);
```

**이 두 함수는 "보유"라는 단어 하나에 성장·구매·결제를 전부 담게 되어 있다.** 그리고 그것이
추측이 아니라는 증거가 **코드 주석과 설계 문서 양쪽에 이미 적혀 있다**:

| 출처 | 적혀 있는 것 |
|---|---|
| `ItemCatalog.cs:244` (주석) | 상태 슬롯은 *"훗날 여기에 **가격표**가 들어와도 레이아웃을 두 번 고치지 않게"* |
| `ItemCatalog.cs` 클래스 주석 | *"훗날 판매를 얹을 때 데이터 모양이 이미 맞아 있게"* |
| `docs/UX_SHOP_AND_CURRENCY.md` §3-3 | 카드 상태표에 **S5 `DLC(백엔드 없음)` / S6 `DLC(스토어 배선 후)`** 가 이미 있다 |

**→ 유료 경계는 바로 이 `IsOwned`에 합류하도록 설계돼 있다.** 합류하는 순간
`EquipmentDebugUnlock.UnlockAll ||` 가 **그 위에 그대로 얹힌다.** 그러면
**환경변수 한 줄이 출하 빌드에서 유료 팩을 여는 크랙**이 된다 — 게다가 그 변수 이름은
**저장소 문서 9곳에 적혀 있고**, 형제 변수인 `STICKMATE_DEVTOOLS`는 **부팅 배너가 사용자에게 직접
알려준다**(`AppControlDirector.cs:187`).

> **판정: 오늘 고칠 버그는 없다. 그러나 이건 "나중에 비싸지는 것"의 교과서적 사례다.
> 지금은 `||` 하나를 나누는 일이고, DLC가 올라탄 뒤에는 매출 누수 + 스키마 마이그레이션이다.**

## 2-2. ★ 더 급한 것 — 설계 문서가 **소유 플래그를 평문 세이브에 넣기로** 이미 적어 두었다

`docs/UX_SHOP_AND_CURRENCY.md` **§9-3 (P0, 리더에게 이미 올라간 항목)**:

> *"세이브 — 재화 잔액 + **구매 소유 플래그** + 스탯별 최고 도달 단계 + 유예 기산점이 새 필드다
> → `CharacterSaveStore.CurrentVersion` 올림"*

그리고 §3-7 예외 상태표에 **`DLC 미구매 | DLC 플래그`** 가 있다.

**이 "구매 소유 플래그"가 DLC 소유까지 포함하면, DLC 소유가 평문 JSON 한 줄이 된다.** 실측한 현재 세이브:

```json
{ "version": 9, "level": 6, "currentXp": 433.89, ... "wornHead": "equip.head.fur" }
```
경로: `~/Library/Application Support/Vibelab/StickMate/stickmate_character.json`
(Windows: `AppData/LocalLow/Vibelab/StickMate/`) — **평문, 메모장으로 편집 가능, 경로는 로그에 인쇄됨.**

**두 문서가 각자 옳은 판단을 하면서 같은 지점으로 수렴하고 있다.** `ux-designer`는 "구매했으니 저장해야
한다"는 당연한 결론을 냈고, `EquipmentDebugUnlock`은 "보유 판정은 한 곳이어야 한다"는 옳은 결론을 냈다.
**둘 다 맞다. 합치면 틀린다.** — 이게 `game-architect`가 말한 *"개별이 다 옳아도 합이 어긋난다"* 의 실례다.

## 2-3. `STICKMATE_DEVTOOLS` — 유료 경계를 넘지 않는다 (반증 완료)

주석이 *"일부러 다른 변수로 뒀다"* 고 주장한다. **그 주장은 참이다 — 소비자 목록으로 확인했다.**

`StickMateDevTools.Enabled`를 읽는 프로덕션 코드는 4개뿐이다:
`RunawayDirector`(가출 강제) · `ActionCommandPopover` · `ShapeCoverageGuard`(폴백 표시) ·
`AppControlDirector`(단축키 폴링 게이트). **보유·레벨·아이템 판정 경로에 하나도 없다.**
`EquipmentDebugUnlock`이 `StickMateDevTools`에서 재사용하는 것은
`ResolveFromEnvironmentValue(string)` — **문자열 파서 순수 함수 하나**이고 게이트가 아니다.

DEVTOOLS가 여는 것: 진단 로그(D) / 하드웨어 반응 미리보기(H) / 스트레스 게이지 순환(S) /
할일 알림 데모(J) / 집중 90초 데모(F) / 가출 강제(N).
**XP·레벨을 주는 경로는 없다** — `AddXp` 호출자는 `CharacterProgressionDirector` 하나뿐이고
개발 단축키에서 도달하지 않는다. 즉 "DEVTOOLS로 레벨을 올려 아이템을 연다"는 **간접 경로도 없다.**

- **판정: 현행 유지.** 원칙 1(행동-텍스트 싱크)을 지키려고 만든 게이트지 유료 게이트가 아니다.
  배너가 사용자에게 변수명을 알려주는 것도 **오늘은 무해**하다(여는 것이 전부 연출 미리보기라서).
- **단, §3의 규칙이 적용된다**: 앞으로도 이 게이트에 **보유·결제 판정을 얹지 않는다.**

## 2-4. 지금 존재하는 유료/무료 경계 — 전수

| # | 경계 | 구현 | 우회 가능성 | 오늘의 매출 피해 |
|---|---|---|---|---|
| 1 | 장비 42종 ← `requiredLevel` | `ItemCatalog`/`EquipmentModel` | 세이브 `level` 평문 편집 / `STICKMATE_UNLOCK_ALL=1` | **0 — 무료 성장 요소** |
| 2 | 개발 연출 경로 ← `STICKMATE_DEVTOOLS` | `StickMateDevTools` | 환경변수 | **0 — 팔지 않음** |
| 3 | DLC 6팩 | **존재하지 않음** (18건 전부 주석) | — | **0** |
| 4 | 재화·상점 | **존재하지 않음** | — | **0** |

**결론: 오늘 유료 경계는 코드에 0개다. 그래서 이번 라운드는 "구멍 막기"가 아니라 "형태 정하기"다** —
리더의 브리핑대로다.

---

# 3. ★ 판정 — 유료 경계를 코드로 표현하는 방식 (이번 라운드의 산출물)

## 3-1. 규칙: **소유(entitlement)와 성장(progression)은 함수도 저장소도 공유하지 않는다**

보유를 **3층으로 분리**한다. 지금은 1층만 존재하므로, 지금 나누는 비용이 가장 싸다.

| 층 | 무엇 | 어디에 저장 | 위조되면 | `UNLOCK_ALL`이 건드려도 되는가 |
|---|---|---|---|---|
| **A. 성장 해금** | 레벨로 열리는 것 | 세이브(평문) | 자기 손해뿐 | **✅ 된다** (QA에 필요) |
| **B. 재화 구매** | 동전으로 앞당긴 것 | 세이브(평문) | 자기 손해뿐 | **✅ 된다** |
| **C. 유료 소유** | 실제 돈을 낸 DLC 팩 | **세이브에 절대 넣지 않는다** | **개발자 매출 손해** | **❌ 절대 안 된다** |

**C는 매 실행마다 스토어에 묻고, 메모리에만 둔다.** 파일에 쓰지 않으므로 **위조할 대상이 존재하지 않는다.**
이건 방어를 덧붙이는 게 아니라 **표적을 없애는 것**이고, 그래서 백신·권한·네트워크 어느 선도 건드리지 않는다.

### 코드 형태 (구현은 `coder` 배정 — 여기서는 형태만)

```
IsOwned(item)  →  item.Tier switch {
                      Progression => LevelGate(item) || UnlockAll,   // A·B — UNLOCK_ALL 여기까지만
                      Purchased   => SaveFlags.Has(item.Id) || UnlockAll,
                      Entitled    => Entitlement.Owns(item.PackId),  // C — UnlockAll 도달 불가
                  }
```

핵심은 **`UnlockAll`이 `Entitled` 가지에 문법적으로 나타나지 않는 것**이다. 주석으로 "넣지 마세요"가
아니라 **다른 함수라서 못 넣는 것**이어야 한다 — 이 저장소가 이미 배운 교훈이다
(`EquipmentDebugUnlock`이 상수에서 빌드 구성으로 옮겨간 이유가 정확히 "사람의 기억에 맡기지 않는다"였다).

## 3-2. 감사 테스트 2건 (리더 승인 시 내가 쓸 수 있다 — 정의서 예외 조항)

1. **`EntitlementNotInSaveAuditTests`** — `CharacterSaveStore`의 `SaveData` 필드 이름에
   엔타이틀먼트 계열 토큰(`dlc`/`pack`/`entitle`/`owned` + 팩 ID)이 **없음**을 소스 텍스트로 잠근다.
   ★ **양성 대조 필수**: 가짜 `SaveData`에 `public bool dlcOwnedPackA;`를 넣은 문자열을 **같은 스캔
   함수**에 흘려 잡히는지 확인한다(`OfflineFirstNetworkAuditTests`의 네거티브 컨트롤 방식을 그대로 복제).
2. **`UnlockSwitchScopeAuditTests`** — `EquipmentDebugUnlock.UnlockAll` 참조가 **A·B 계열 함수
   바깥에 나타나지 않음**을 잠근다. 지금 참조는 2곳(`ItemCatalog.cs:235`, `EquipmentModel.cs:180`)뿐이라
   **오늘 쓰면 기대값이 명확하고, DLC가 들어온 뒤엔 기대값 자체가 논쟁거리가 된다.**

---

# 4. 세이브 무결성 — 판정: **그냥 둔다** (감지도 하지 않는다)

**셋 중 "그냥 둔다"를 고른다.** 단, 그 판정은 §3의 C층 분리를 **전제로만** 성립한다.

## 4-1. 근거

1. **피해자가 가해자와 같은 사람이다.** 리더보드·거래·멀티·공유가 전부 **실측 0건**이다.
   세이브를 고쳐 Lv.99가 된 사용자가 빼앗는 것은 **자기 자신의 성장 경험**뿐이다.
   *"누가 무엇을 잃는가"를 붙일 수 없으면 그건 위협이 아니라 불안이다* — 정의서 규칙.
2. **막을 수 있다는 전제가 거짓이다.** 서명·암호화·HMAC 무엇을 쓰든 **키가 클라이언트에 들어간다.**
   서버가 없으면(있어서도 안 된다 — 선 1) 클라이언트 검증은 **속도 방지턱**이지 잠금이 아니다.
   Valve조차 자기 API에 같은 말을 적어 뒀다(§5-1).
3. **★ 백신 오탐이 이 앱에서는 실존 위험이다.** 시장 조사 결론이 *Shimeji를 죽인 건 기능이 아니라
   배포와 백신 경고*다. 난독화·패킹·안티디버그·무결성 자가검사는 **전부 백신이 싫어하는 형태**이고,
   상주 오버레이 앱이 그걸 하면 리뷰 1페이지에 *"이거 바이러스 아니냐"*가 박힌다.
   **보안이 유통을 죽이면 순손실이다.**
4. **★ 오탐 비용이 비대칭이다.** *정당한 유저를 한 명 잠그는 것이 무단 사용 열 건보다 비싸다.*
   이 저장소는 **오늘** `File.Replace` 실패로 세이브가 깨질 뻔했고(`game-architect` I-1, `design-systems`
   경고, Windows 실기 IOException 로그), `.writing` 임시 파일이 **지금 이 순간 디스크에 남아 있다**:
   ```
   stickmate_character.json.67542.writing   (9/2 02:00, 고아 상태로 잔존)
   ```
   **손상된 세이브를 "변조"로 판정하는 코드는, 이 저장소에서 치터보다 우리 버그를 먼저 만난다.**
   그때 사용자에게 *"당신은 치터입니다"* 라고 말하는 것은 우리가 낼 수 있는 최악의 오답이다.

## 4-2. 그래서 하지 않을 것 (명시적으로 기각)

| 조치 | 기각 사유 |
|---|---|
| 세이브 암호화 | 키가 클라이언트에 있다. 얻는 것 0, 잃는 것: 사용자가 자기 데이터를 못 읽음 + 복구 불가 |
| 체크섬/HMAC **+ 거부** | 우리 쓰기 버그 = 사용자 진행 소실. 4-1의 4번 |
| 변조 **감지 후 경고/제재** | 오탐 시 정당한 유저를 모욕한다. 감지해도 **할 수 있는 일이 없다**(피해자가 본인) |
| 난독화·패킹·안티디버그 | 선 3(백신) 정면 위반 |
| 온라인 검증 | 선 1(네트워크 0) 정면 위반. 이 앱의 최대 자산을 태운다 |

## 4-3. 대신 하는 것 (전부 이미 다른 담당이 하고 있거나, 비용 0)

- **저장 원자성**(`game-architect` 착수 1번) — 이건 **보안이 아니라 신뢰성**이고, 이미 최우선이다.
  내 판정은 그 순위를 **지지한다**: 세이브를 못 지키면 §4의 "그냥 둔다"가 성립하지 않는다
  (사용자가 잃은 진행을 되돌릴 방법이 손편집뿐이 되고, 그때 평문 JSON은 **기능이지 결함이 아니다**).
- **평문 유지가 오히려 옳다.** 사용자가 자기 세이브를 백업·복구·이전할 수 있다. 상주 동료 앱의 성격에 맞다.
- **C층(유료 소유)을 파일에 넣지 않는다** — §3. 이것 하나로 이 파일에 지킬 가치가 있는 것이 사라진다.

## 4-4. 개인정보 — 세이브에 사용자 입력이 들어간다 (참고, 조치 불요)

`SaveData.todos` / `todoArchive`는 **사용자가 타이핑한 할일 텍스트**를 평문 저장한다
(`TodoRecord.text`). 현재 실측 세이브는 `[]`로 비어 있다. 로컬 전용이고 전송 경로가 0건이므로
**위협은 아니다.** 다만 **§1-3(a)와 겹친다** — 세이브 경로가 로그에 찍히고, 사용자가 버그 신고 때
로그를 보낸다. 로그에 들어가는 건 경로뿐이고 할일 내용은 아니다. **조치 불요, 기록만.**

---

# 5. 스토어가 이미 해 주는 것 — 1차 출처 대조

## 5-1. Steam — Valve 자신이 "아이템 지급에 쓰지 말라"고 적어 두었다

`ISteamApps` 공식 문서 **직접 인용**:

| API | Valve의 문구 (원문) |
|---|---|
| `BIsDlcInstalled` | **"Should only be used for simple client side checks - not intended for granting in-game items."** |
| `BIsSubscribedApp` | "Only use this if you need to check ownership of another game related to yours, a demo for example." |
| `BIsSubscribed` | "This will always return **true** if you're using Steam DRM or calling `SteamAPI_RestartAppIfNecessary`." |
| `GetDLCCount` | "this value may max out at 64 ... you should set your own internal list of known DLC to check against." |

DLC 페이지는 안전한 확인 방법으로 **서버 + WebAPI 키 + `CheckAppOwnership`**을 제시한다 —
**우리가 쓸 수 없는 방법이다**(선 1).

- ★ **오프라인 동작은 Valve가 문서화하지 않았다.** DLC 문서에도 `ISteamApps` 문서에도
  오프라인 모드·Steam 클라이언트 미실행 시 동작에 대한 서술이 **없다**. → **미확인.**
  (`SteamEncryptedAppTicket`은 존재하나 *"신뢰할 수 있는 서버"*를 전제하므로 우리에게 해당 없음.)
- **그런데 이 미확인은 우리에게 치명적이지 않다.** §3의 C층을 쓰면 판정 실패 시 답이 정해져 있다 —
  **§5-4의 "실패는 관대하게" 규칙.**

## 5-2. ★ MS 스토어 — `docs/strategy/`의 **미확인 1건을 해소했다**

선행 조사에 *"MS 스토어에서 DLC를 팔 수 있는지 자체가 미확인"*으로 남아 있던 항목이다.
**규범 문서(learn.microsoft.com)의 공식 비교표로 확인했다:**

| 기능 | **Packaged (MSIX)** | **Unpackaged (Win32 EXE/MSI 링크 등록)** |
|---|---|---|
| Commerce Platform (payment, **in-apps**, subscriptions, licensing) | **MS 스토어 커머스 사용 가능** | **"Use your own or 3P commerce platform"** |
| Code signing | **MS가 무상 제공** | **개발자가 CA 인증서 구매·부담** |

**→ 답: 팔 수 있다. 단 MSIX로 낼 때만.** "기존 EXE를 링크로 등록"하는 경로는 **애드온을 팔 수 없다.**

### ★ 이게 `ROADMAP.md` 3절의 비용 계산을 뒤집는다

`MARKET_LANDSCAPE.md` 220행이 *"MS 스토어 MSI/EXE 경로의 코드서명 인증서 비용"*을 새로 발견한 비용으로
적어 두었다. **그 비용을 무는 바로 그 경로가, 동시에 DLC를 팔 수 없는 경로다.**
즉 언패키지 경로는 **두 번 손해**이고, MSIX 경로는 **서명 무료 + 애드온 판매 가능**이다.
→ `product-strategy`에 인계할 항목(리더 경유).

### 형태까지 정해 둔다 — **"durable add-on"이지 "DLC"가 아니다**

MS 문서 주의사항 인용: *"Other types of add-ons, such as **durable add-ons with packages (also known as
downloadable content or DLC)** are **only available to a restricted set of developers**."*

**→ 패키지가 딸린 진짜 "DLC"는 우리가 못 쓴다.** 우리에게 맞는 것은 **durable add-on(기본 수명 Forever)**
= **라이선스만 사고 콘텐츠는 본편에 들어 있는 형태**다. 6팩 콘텐츠가 어차피 앱 안에 있으므로 **이게 정확히
우리 모양이다.** 우연이 아니라 §3의 C층 설계와 같은 구조다.

### 확인된 제약 2건

- **"In-app purchase functionality is not currently supported in elevated applications."**
  → StickMate는 승격 실행이 아니다(권한 0). **문제 없음.** 다만 **작업표시줄 원복 예외**(`ABM_SETSTATE`)가
  혹시라도 승격을 요구하게 바뀌면 **결제가 함께 죽는다** — `dev-platform`에 전달할 제약(리더 경유).
- **`Windows.Services.Store`에는 시뮬레이터가 없다.** 문서 명시: 테스트하려면 **먼저 스토어에 게시하고
  기기에 설치해 라이선스를 받아야** 한다. → 결제 배선 라운드의 일정에 이 왕복이 들어간다.

### 남은 미확인 (정직하게 남긴다)

MS 직원 Q&A 답변은 *"MSIX packaged **from an exe file**이면 스토어 기능을 못 쓸 수 있다"* 고 말한다.
규범 표는 "MSIX면 된다"고 하고, Desktop Bridge 앱이 `StoreContext`를 쓰는 방법도 문서화돼 있다.
**Unity가 만드는 exe → MSIX Packaging Tool 경로가 애드온을 지원하는지는 두 출처가 어긋난다 — 미확인.**
결제 채널을 확정하기 전에 **실물로 확인해야 한다.** 나는 Windows 실기가 없어 재지 못했다.

## 5-3. 맥 앱스토어 — 조사 안 함

1차 출시 대상 제외(우선순위 최하, 리더 지시). 참고로 `ProjectSettings.asset:
useMacAppStoreValidation: 0`. 착수 시 영수증 검증이 별도 주제가 된다.

## 5-4. ★ 핵심 질문에 대한 답

> **오프라인 상주 앱에서 "이 사람이 이 팩을 샀는가"를 어디까지 믿을 수 있는가?**

**답: 끝까지는 못 믿는다. 그리고 믿으려고 애쓰면 안 된다.**

- Valve가 자기 API에 *"아이템 지급용이 아니다"* 라고 적어 두었다. 그 이상의 보증은 **서버로만** 살 수 있고,
  서버는 이 앱이 팔지 않기로 한 것이다.
- 따라서 **정직한 목표는 "무단 사용 불가능"이 아니라 "정직한 사용자가 정직하게 살 이유가 있고,
  실수로 잠기지 않는 것"**이다.

### 실패는 **관대하게** — 이 규칙이 §4-1의 오탐 비대칭을 상속한다

| 스토어 조회 결과 | 우리 동작 |
|---|---|
| "샀다" | 연다 |
| "안 샀다" | 잠근다 + 구매 안내(S6) |
| **조회 실패 / 오프라인 / 스토어 미실행** | **★ 직전에 성공했던 판정을 그대로 유지한다. 절대 회수하지 않는다** |

**돈을 낸 사용자에게서 비행기 안에서 팩을 빼앗는 것**은, 안 낸 사용자 열 명이 쓰는 것보다 비싸다.
이건 §4-1의 4번과 **같은 원칙의 다른 적용**이다. 그리고 이 규칙 덕분에 §5-1의 "Steam 오프라인 동작 미확인"이
**설계상 무해**해진다 — 어느 쪽이든 우리 동작이 정해져 있다.

(마지막 성공 판정을 **어디에** 기억할 것인가는 §3의 C층과 충돌한다 — 파일에 쓰면 위조 대상이 생긴다.
**권고: 프로세스 메모리 + "이번 실행에서 한 번이라도 성공했으면 유지". 실행 간 캐시는 만들지 않는다.**
사용자가 오프라인으로 앱을 재시작하면 잠기지만, 스토어 클라이언트가 로컬 라이선스를 들고 있는 것이
정상 경로라 실무상 거의 발생하지 않는다. **이건 판정이 아니라 권고다 — 실기 확인 후 확정할 것.**)

---

# 6. 지금 당장 고칠 것 — **3건** (전부 "지금 안 하면 나중에 비싸진다"에 해당)

## ★ 1순위 — 소유 3층 분리를 **v10 스키마에 못 박는다. ★ v10은 지금 열려 있다**

> ### ★★ 시급도 정정 — 이건 "다음 라운드"가 아니라 **지금 이 순간**이다
>
> 보고서를 쓰는 중에 작업 트리를 다시 쟀다. **v10 스키마 라운드가 이미 돌고 있다:**
> ```
> git diff Core/CharacterSaveStore.cs
>   + internal const int CurrentVersion = 10;      ← 이미 올라가 있다 (커밋 전)
>   + public bool   preferredMonitorSaved;
>   + public string preferredMonitorKey;
> ```
> 지금까지 v10에 들어간 새 필드는 **모니터 선택 2개뿐 — 경제·소유 필드는 아직 0개다.**
> 즉 **아직 안 늦었고, 여유도 없다.** `UX_SHOP_AND_CURRENCY.md` §9-3이 요구하는
> "재화 잔액 + **구매 소유 플래그**"가 이 v10에 추가되는 순간 결정이 굳는다.
>
> **리더에게: 이 항목만은 다른 순위와 달리 "다음에"가 성립하지 않는다.**
> v10을 커밋하기 전에 판정이 필요하다.

- **무엇**: §3-1의 규칙을 확정하고, `UX_SHOP_AND_CURRENCY.md` §9-3의 **"구매 소유 플래그"에서
  DLC 소유(C층)를 제외**한다. **재화 구매 플래그(B층)는 v10에 넣어도 된다** — 위조돼도 자기 손해뿐이다.
  구분선은 *"돈이 오갔는가"* 하나다.
- **왜 지금**: v10 스키마는 `game-architect`가 *"지금 정하지 않으면 v11·v12를 연달아 올리게 된다"*고
  적은 **되돌리기 어려운 결정**이다. DLC 플래그가 v10에 들어가면 빼는 데 **v11 + 하위호환 테스트 1건**이
  든다(CLAUDE.md 명시 규칙). 그리고 그 사이에 나간 빌드의 세이브에는 **위조 가능한 소유 플래그가 남는다.**
- **비용**: 지금 = **필드 하나를 안 만드는 것(0)**. 나중 = 스키마 마이그레이션 + 매출 누수.
- **차단 관계**: 착수 **4번을 막는다.** **새 단계를 추가하지 않는다 — 이미 열려 있는 4번에 제약 한 줄을
  넣는 것이다.**
- **좋은 소식 1건**: `CharacterInfoWindow.Shop.cs`(신규, 66줄)는 **의도적 빈 껍데기**다 —
  *"지금 가격표를 그리면 화면이 아직 정해지지 않은 값을 주장하게 된다"*는 이유로 비워 두었다.
  **상점 표면은 아직 아무 실수도 저지르지 않았다.** 위험은 화면이 아니라 **스키마 쪽에 있다.**

## ★ 2순위 — `submitAnalytics: 1` → `0`, 그리고 오프라인 감사에 `ProjectSettings.asset`을 넣는다

- **무엇**: (a) 한 줄 변경. (b) `OfflineFirstNetworkAuditTests`가 `.cs` 말고
  `ProjectSettings/ProjectSettings.asset`의 `submitAnalytics`와 `UnityConnectSettings.asset`의
  `m_Enabled`도 검사하게 확장. **★ 양성 대조**: 값을 1로 바꾼 가짜 문자열을 같은 검사에 흘려 잡히는지 확인.
- **왜 지금**: (i) 우리 마케팅이 **"네트워크 0 · 권한 0"을 공개 주장**하려 한다. 검증 안 된 공개 주장은
  가장 싸게 반박당한다. (ii) 지금 감사는 이 스위치를 **구조적으로 볼 수 없다** — 누가 나중에 켜도
  아무도 모른다. (iii) 비용이 **한 줄 + 테스트 한 건**이다.
- **주의**: 이 조치로 §1-2의 미확인이 **해소되지는 않는다**(엔진이 이미 보내는지는 여전히 측정 대상).
  다만 **양쪽 스위치가 다 꺼진 상태**가 되어 이후 측정의 기대값이 명확해진다.
- **담당 경계**: `ProjectSettings.asset`은 프로덕션 `.cs`가 아니지만 내 소유도 아니다 — **리더 배정 필요.**

## ★ 3순위 — 전체화면 판정 로그에서 **남의 앱 신원**을 뺀다 (양 플랫폼)

- **무엇**: Windows는 풀 경로 → **파일명만**(또는 해시). macOS는 owner 이름 → **길이/카테고리만**.
  판정 근거로서의 유용성은 유지된다(게임인지 아닌지가 요점이지 어느 게임인지가 아니다).
- **왜 지금**: 사용자가 **버그 신고 때 이 로그를 보낸다.** Windows 경로에는 **계정 실명**이 들어간다.
  나중에 고치면 이미 나간 로그는 회수할 수 없다. 비용은 문자열 두 곳.
- **왜 3순위**: 피해 규모가 작고 로컬이다. 1·2순위가 매출·공개주장에 걸린 반면 이건 위생 문제다.

### 4순위 이하로 내린 것 (지금 하지 않는다)

- `STICKMATE_UNLOCK_ALL` 자체 제거 — **하지 않는다.** QA 절차가 실제로 쓰고 있고, §3의 분리가 끝나면
  A·B층에 남아도 안전하다. **기능을 줄이는 게 아니라 도달 범위를 줄이는 것이 답이다.**
- 부팅 배너의 `STICKMATE_DEVTOOLS` 안내 — **유지.** 여는 것이 전부 연출 미리보기라 오늘 무해하고,
  숨기는 것은 보안이 아니라 은폐다(그리고 저장소 문서 9곳에 이미 적혀 있다).
- 세이브 무결성 조치 일체 — **§4에서 기각.**

---

# 7. `game-architect` 산출물과 맞물리는 지점

`docs/GAME_ARCHITECTURE_REVIEW.md`를 읽고 대조했다. **순서를 바꿀 필요는 없다 — 제약 2개를 끼워 넣는다.**

| 그쪽 항목 | 내 결론이 무는 지점 |
|---|---|
| **§3-2 착수 4번 — v10 스키마** | ★★ **여기가 임계점이고, 이미 열려 있다.** 작업 트리에 `CurrentVersion = 10`이 **커밋 전 상태로 존재한다**(경제 필드는 아직 0개). §6-1의 규칙은 "다음 라운드 전"이 아니라 **이 v10 커밋 전**에 확정돼야 한다. 새 단계가 아니라 **4번의 입력 제약**이다 |
| **§3-2 착수 5번 — 재화·상점** | `UnlockAll` 분리(§3-1)가 **이 라운드까지** 끝나 있어야 한다. 상점이 붙는 순간 `IsOwned`가 결제 판정을 겸한다 |
| **§3-2 착수 1번 — 저장 원자성** | **내 §4 판정의 전제다.** "세이브를 그냥 둔다"는 *세이브가 안 깨진다*를 가정한다. 1번이 미뤄지면 §4를 재검토해야 한다. → **1순위 유지에 찬성** |
| **§1-D — 원칙 4 인프라가 0이다** (`MotionPluginSO`/`EffectPluginSO` 소비자 0곳) | ★ §8의 결정이 **바로 여기에 떨어진다.** 레지스트리를 **지금 새로 짓는다** = 로딩 출처를 정할 **가장 싼 순간**. 임의 경로를 받게 지으면 나중에 닫는 것이 파괴적 변경이다 |
| **§2 되돌릴 수 없는 결정 8건** | 여기에 **9번째**를 추가 제안한다: **"유료 소유를 세이브 스키마에 넣는가"**. 넣으면 되돌리는 데 스키마 버전이 든다 — 그쪽 I-2와 정확히 같은 성격이다 |
| **§4-2 의존 방향이 이미 순환한다** | 엔타이틀먼트 조회는 **플랫폼 전용**(Steam/MS)이다. CLAUDE.md의 *"정책은 플랫폼 중립 위치에"* 규칙이 그대로 적용된다 — **판정 로직은 `Platform/`에, 스토어 SDK 호출은 `Platform/Windows`·`Platform/MacOS`에.** `FullscreenSuspendPolicy.cs` 사고의 재발 자리다 |

**`ux-designer`와의 인계**: §2-2에서 지적한 `UX_SHOP_AND_CURRENCY.md` §9-3은 **그쪽 P0 항목**이라
이미 리더에게 올라가 있다. **그 항목의 "구매 소유 플래그"를 A/B층(재화 구매)과 C층(유료 소유)으로
쪼개 달라는 요구**를 리더 경유로 전달한다. 화면 쪽 영향은 없다 — S5/S6 카드 상태표는 그대로 쓸 수 있다.

---

# 8. 원칙 4 플러그인 통로 — **사용자 결정 대기. 답을 가정하지 않고 두 갈래로 쓴다**

★ 어느 갈래든 공통: **`game-architect` §1-D에 따라 레지스트리 자체가 아직 없다. 지금이 가장 싸다.**

## (가) 봉인 — 매니페스트는 우리만 만든다

- **서명 검증 불필요.** 매니페스트가 **서명된 우리 앱 번들 안에서만** 로드되면 OS 코드서명이 이미 무결성을 보장한다.
- **필요한 것 1가지**: 레지스트리를 **`Resources`/번들 내부에서만** 로드하도록 짓는다. 임의 경로 인자를
  받지 않는다. **비용 0(지금) / 파괴적 변경(나중).**
- **매출 영향 없음.** DLC 팩도 우리 것이므로 §3의 C층으로 충분하다.

## (나) 제작 개방 · 판매 봉인 — 남이 만들되 아무도 팔지 않는다

- **매출 위협 없음** → **서명 검증(코드 신뢰) 여전히 불필요.** 여기가 흔한 오해다: 서명은 **돈이 걸릴 때**
  필요하지, 제작 개방 자체가 서명을 요구하지 않는다.
- **대신 다른 3가지가 필요해진다** (위협의 성격이 매출 → 안전·평판으로 바뀐다):
  1. **매니페스트를 끝까지 선언적으로 유지한다.** 코드·스크립트 참조·파일 경로·URL을 **필드로 두지 않는다.**
     두는 순간 데이터가 아니라 **실행 가능 표면**이 되고, 그때부터 백신이 우리를 본다(선 3).
  2. **로드 시 엄격 검증 + 실패는 조용히 건너뛴다.** 남의 팩 하나가 앱 전체를 죽이면 그 신고는 우리에게 온다.
     (`game-architect`가 지적한 `Append*`에 `default:`가 없어 **조용히 안 그려지는** 문제와 같은 계열 —
     그쪽 착수 2번과 함께 보면 좋다.)
  3. **출처를 화면에 표시한다.** 제3자 콘텐츠가 1st-party로 읽히면 **남이 쓴 대사·조형이 우리 평판이 된다.**
     원칙 1(행동-텍스트 싱크)의 책임 주체가 흐려지는 문제이기도 하다 — `design-narrative` 소관과 겹친다.
- **판정 필요 항목**: 팩이 **대사**를 넣을 수 있는가. 넣을 수 있으면 검열·신고 경로가 필요해지고,
  그건 상주 동료 앱에서 무거운 주제다. **나는 이 질문을 열어 둔다 — `design-narrative`·`product-strategy` 사안.**

## (다) 완전 개방 (판매까지) — ★ 이 갈래만 선 1을 건드린다

**사용자가 이걸 고르면, 그건 보안 결정이 아니라 사업 모델 결정이고 리더가 그렇게 보고해야 한다.**
제3자 판매는 결제 중개·정산·환불·분쟁을 낳고, 그중 무엇도 **네트워크 0으로는 불가능하다.**
`MARKET_LANDSCAPE.md`가 최대 차별점으로 세운 자산을 태우는 선택이다.

- **덜 파괴적인 대안**: 제3자 팩을 **각 스토어의 애드온으로만** 팔게 하고(우리는 중개하지 않는다),
  우리 앱은 §5-4의 스토어 조회만 한다. 이러면 네트워크 0이 유지된다. 다만 스토어 정책상
  제3자가 우리 앱의 애드온을 등록할 수 있는지는 **미확인**이다.

---

# 9. 측정하지 않은 것 (= 이 문서가 보장하지 않는 것)

정직성 규칙에 따라 명시한다.

1. **앱을 실행하지 않았고 빌드하지 않았다.** 리더 승인 사항이고 `dev-platform`이 Unity 배치모드를 잡고 있다.
   → §1-2의 패킷 캡처를 **못 했다.** `submitAnalytics` 실제 송신 여부는 **미확인.**
2. **Steam 오프라인 엔타이틀먼트 실동작 미확인.** Valve가 문서화하지 않았고, 실기 Steam 빌드가 없다.
   (§5-4의 설계가 이 미확인을 무해화하지만, 무해화한 것이지 확인한 것이 아니다.)
3. **MS 스토어 MSIX(Unity exe 경유) 애드온 지원 미확인.** 규범 표와 MS 직원 Q&A 답변이 **어긋난다.**
   Windows 실기가 없어 재지 못했다. **결제 채널 확정 전 실물 확인 필요.**
4. **`design/` 미검토** — 3인이 진행 중이라 읽지 않았다.
5. **EditMode/PlayMode 테스트를 돌리지 않았다.** 배치모드가 잡혀 있다. 위 결론은 전부
   **소스·설정·빌드 산출물·실제 세이브 파일·실제 Player.log의 정적 실측**이다.
6. **모바일(iOS) 경로 미검토.** StoreKit 영수증 검증은 §3의 C층에 네 번째 스토어로 붙는 주제다.
7. ★ **작업 트리가 살아 움직이는 중에 쟀다.** 다른 라운드들이 동시에 돌고 있어 커밋되지 않은 변경이
   **90여 개 파일**에 있다(신규 `CharacterInfoWindow.*.cs` 7개, `CurrentVersion = 10` 등).
   위 실측은 **2026-09-02 이 라운드 시점의 작업 트리 상태**이고, `HEAD`가 아니다.
   특히 §6-1의 v10 관찰은 **다음 커밋에 따라 바뀔 수 있다** — 리더는 판정 직전에 다시 확인할 것.

---

# 10. 플랫폼 영향

- **Windows 영향: 함께 검토함.** §1-3(b) 로그 누출은 **Windows가 더 심각**하다(풀 경로 + 계정명).
  §5-2 MS 스토어 결론은 **Windows 전용**이고 `ROADMAP.md`의 비용 계산을 뒤집는다.
  승격 실행 시 IAP 불가 제약은 **작업표시줄 예외**(`ABM_SETSTATE`)와 인접한다.
  `WindowsGameProcessProbe.cs`의 원칙 3 준수는 **모범 사례**로 확인했다(쓰기 API 미선언 + 테스트 잠금).
- **macOS 영향: 함께 검토함.** §1-3(b)는 macOS에도 있다(앱 이름, 경로보다는 덜 민감).
  §1-2 `submitAnalytics`·§1-1 네트워크 0은 **플랫폼 무관**(프로젝트 설정·공용 코드).
  맥 앱스토어는 1차 출시 제외라 조사하지 않았다(§5-3).
- **★ 신규 코드 없음** — 이번 라운드는 감사·판정이라 프로덕션 `.cs` **수정 0건**, 신규 플랫폼 분기 0건.
  따라서 `PlatformParityAuditTests`에 추가할 항목도 없다.

---

## 부록 — 이 문서가 근거로 삼은 1차 출처

- [ISteamApps Interface (Steamworks Documentation)](https://partner.steamgames.com/doc/api/ISteamApps)
- [Downloadable Content (DLC) (Steamworks Documentation)](https://partner.steamgames.com/doc/store/application/dlc?l=english)
- [How to distribute your Win32 application through Microsoft Store](https://learn.microsoft.com/en-us/windows/apps/distribute-through-store/how-to-distribute-your-win32-app-through-microsoft-store) — 규범 비교표
- [In-app purchases and trials (Windows.Services.Store)](https://learn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials)
- [Create an add-on submission (Partner Center)](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/add-on/what-are-add-ons)
- [Win32 Desktop app - how to use AddOn Subscription (Microsoft Q&A)](https://learn.microsoft.com/en-us/answers/questions/1280704/win32-desktop-app-how-to-use-addon-subscription-(n) — 규범 표와 어긋나는 출처
- [What happened to Disable HW Statistics? (Unity Discussions, Unity 스태프 답변)](https://discussions.unity.com/t/what-happened-to-disable-hw-statistics/806662)
