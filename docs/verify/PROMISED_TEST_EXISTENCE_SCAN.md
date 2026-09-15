# 문서가 약속한 테스트 — 실존 전수 대조

작성: qa-regression / 2026-09-15 · **2판(묶음 ⑧)** · 기준 **HEAD `ad49497` — 깨끗한 로컬 복제본에서 생성**(작업 트리의 다른 라운드 수정은 섞지 않았다) · `.cs`·Unity·커밋 0
도구: `docs/verify/promised_tests_scan.py`(표준 라이브러리만) · 이 문서 A절 이하는 그 도구의 `--markdown` 출력을 **손대지 않고** 붙였다.

★ **인용 전에 신선도 가드를 먼저 돌려라** — `python3 docs/verify/promised_tests_scan.py --check docs/verify/PROMISED_TEST_EXISTENCE_SCAN.md`
(rc 0 = I절 스냅숏이 원천 재계산과 같다 / rc 2 = 낡음, 이 문서 숫자 인용 금지). 키는 `(분류, 이름, 파일, 문자열 앵커, 앵커 총수, 분류 출현 수)`이고 줄 번호는 정보용이다.
★ **이 판은 `ad49497` 기준이다.** 작업 트리에서 돌리면 rc 2가 정상이다 — 이 문서를 만드는 동안 `Tasklist.md` · `docs/GAME_ARCHITECTURE_REVIEW.md` · `docs/TEAM.md` · `docs/manual/01·03` · `docs/marketing/CLAIM_AUDIT_R5.md`·`STORE_PAGE.md`·`TRUTH_INVENTORY.md` · `docs/security/SECURITY_MODEL.md` · `docs/strategy/CHANNEL_PRICING_DECISIONS.md`·`ROADMAP.md` · `docs/systems/AUTO_SURFACE_LEASE_AXIS.md`·`TASKBAR_LOGOFF_ORDERING.md`가 수정 중이었고 `docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md`는 미추적이었다. 같은 `ad49497` 복제본에서 `--check`를 돌려야 재현된다.
★ **개수는 B절이 정본이다.** 이 앞머리에는 숫자를 베끼지 않았다(기준과 대상이 같이 낡는 병 — `docs/TEAM.md` 해당 절).
★ **1판(HEAD `761f5cb` 작업 트리)에서 바뀐 것**: git 호출 인덱스 무쓰기(`--no-optional-locks`) · (라) 규칙 V1·V3 · 약한 표기(V2 목록화) · 커밋 앵커 실재 확인 · 잘린 이름 (나) 규칙 · 파일 줄기 대조 · 합성 교정 이름 제외 · 숨김 탐침 4종 교정 · 거짓 서술 F-7~F-10 추가 · F-2 비고 정정. 1판 숫자는 이 판과 비교하지 마라(기준 커밋이 다르다).

---

## 0. 사람 판정 — 무엇이 실제로 문제인가

기계 분류(C·D·E·F절)는 **이름이 선언으로 있느냐**와 **표기가 곁에 있느냐**만 잰다. 아래는 문장과 코드를 열어 다시 판정한 것이다.
줄 번호는 전부 `ad49497`이다(작업 트리에서는 달라질 수 있다).

### 0-1. 거짓 서술 — 주장 자체가 `ad49497`에서 틀리다 (10)

| # | 문서 위치 | 문서가 하는 말 | 실제 | 스캐너 분류 · 이름 이력 | 심각도 |
|---|---|---|---|---|---|
| F-1 | `docs/manual/03-disappeared.md:321` 「★ 아직 안 되는 것 — ⌃⌥⌘I」 상자 | 정보창 단축키는 등급 1에서 아직 안 되고, 저장소가 `미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다 (Assert.Ignore)`로 갭을 등록해 두었다 | `CharacterInfoWindow.cs:945`가 `TryGrantUserSummon`을 부른다. 갭 테스트는 `FullscreenPanelRetreatTests.cs:790` `등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다`로 **반전됐다** | (다) 도입 `1eb0e2b` → 소멸 `eb4670d` | **높음(수동 상향)** — 사용자 매뉴얼 · 원칙 2 탈출구 |
| F-2 | `docs/manual/03-disappeared.md:608` · **`docs/manual/01-where-to-click.md:55-57`(같은 거짓 문장)** | `PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다`가 Assert.Ignore로 러너에 뜬다 · (01) 「선택지 3개가 그 사유문에 있고 리더 판정 대기」 | 그런 Ignore 테스트는 **한 번도 없었다**. 같은 사실을 재는 선언은 `PlatformParityAuditTests.cs:775` `작업표시줄_버튼_제거는_창_상태를_건드리지_않는_경로로만_한다`(정식 검사) · `AppSwitcherPresenceTests.cs:158` `A4_작업표시줄_버튼은_스타일만으로_사라지지_않는다는_사실이_코드에_박혀_있다` | **(나)** `.cs` pickaxe 0커밋. ★ **1판 비고 정정**: 1판은 「`manual/01:56`은 「사라진」 표기가 있어 (라)」라고 적었으나 **틀렸다** — 그 「사라진」은 `:58-59` 브리프 인용 문장의 것이고 이 이름의 부재를 말하지 않는다(verify-change 적발). 이 판은 그것을 **약한 표기** (라)로 F절 사람 확인 목록에 올리고, 여기서 거짓 서술로 판정한다 | **높음(수동 상향)** — 사용자 매뉴얼 두 장 |
| F-3 | `docs/strategy/CHANNEL_PRICING_DECISIONS.md:1678` | 전송계열 화이트리스트는 **0건**으로 못박혀 있고 `전송계열_화이트리스트는_현재_비어_있다()`가 잠근다 | `OfflineFirstNetworkAuditTests.cs:530` `전송계열_화이트리스트는_승인된_예외_1건과_정확히_같다` — 0건이 아니라 승인된 1건 | (다) 소멸 `1f7e139` · 줄도 `1f7e139` — 쓰인 커밋에서 이미 틀렸다. ★ 작업 트리에서 `product-strategy`가 정정 중(묶음 ⑧) | 높음(수동 상향) — 보안(오프라인 우선) 사실 |
| F-4 | `docs/marketing/CLAIM_AUDIT_R5.md:93` | 「실제로 **잰다**」 근거 = `FullscreenPanelRetreatTests` `등급1에서_톱니를_누르면_설정창까지_도달한다()` | 그 이름은 사라졌고(개명 후보 1순위 `등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다`), 옛 판은 톱니 우회 세계의 **거짓 통과**였다(`docs/TEAM.md` 「스위트 전역 격리」 절) | (다) `1eb0e2b` → `eb4670d` | 높음(출시 게이트 — 홍보 문장 근거) |
| F-5 | `docs/strategy/ROADMAP.md:3738-3741` 「나는 그 테스트 파일을 직접 열어 읽었다」 표 | 줄 298·555·714의 테스트 이름과 그것이 못박는 것 | 4행 중 **3행 이름이 사라졌다**. 555행이 불변식 R1-I의 근거로 인용된다 | (다) 셋 다 소멸 `eb4670d` | 낮음(키워드) — R1-I 근거라 출시 판단 인용 시 높음 |
| F-6 | `docs/GAME_ARCHITECTURE_REVIEW.md:2041` | 누군가 B를 고치면 `등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다`가 빨개지고, 이 문단이 그 되돌림을 막는다 | 그 테스트는 사라졌다. 후신(`…우클릭_입구는_남긴다`)은 단언 방향이 반대다. ★ 작업 트리에서 `game-architect`가 경고문 폐기 반영 중 | (다) `7ed996d` → `eb4670d` | 낮음 |
| **F-7** | `docs/manual/03-disappeared.md:352` | 러너 `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` Passed는 톱니 게이트를 전역 우회한 채 잰 것이다 | 문장 뜻(옛 판은 우회 세계의 초록)은 참이지만 **이름이 사라졌다는 표기가 없다**. ★ 1판 스캐너는 같은 절에 매뉴얼 라운드가 넣은 「안쪽이 미구현이면」(절 기능 낱말) 때문에 이 줄을 **(라)로 조용히 숨겼다**(verify-change 적발 → V3). 작업 트리에서는 매뉴얼 라운드가 「(사라짐 — `eb4670d`)」 표기를 넣는 중 | (다) `1eb0e2b` → `eb4670d` | 낮음(사용자 매뉴얼 부록) |
| **F-8** | `docs/EQUIPMENT_SHAPE_SPEC.md:79` | `EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다` / `AccessoryStrokeBudgetTests.머리카락이_…` **둘 다 `Is.InRange(2, 4)`로 잠근다**(현재형) | 앞 이름은 `0229f52`에서 사라졌다 — 지금은 그 이름으로 잠그는 테스트가 없다 | (다). 스캐너는 같은 인용문의 「브리핑 **정정** 1」(이 테스트와 무관한 정정)에 걸려 **약한 표기** (라)로 F절에 올렸다 | 낮음 |
| **F-9** | `docs/UI_SURFACE_SPEC.md:880` | 포스트잇은 「앱에서 유일하게 시스템 밖. **이미 폐기된 알파 유리 규약이 여기만 살아 있다**」 · 검증은 「기존 `PopoverAndHoverPanelOpacityTests` 패턴 재사용」 | 메모 카드는 이미 `UiChrome.AddOpaquePanel`(본체 α1) 구조다(`TodoPostItWidget.cs:88` 설명 · `:1127` 코드). 인용한 테스트는 **파일**만 그 이름이고 안의 형식명은 `PopoverPanelOpacityTests`(`PopoverAndHoverPanelOpacityTests.cs:207`) | (가) — **파일 줄기 일치**(2판 신설 대조). 1판은 「폐기」 표기로 (라)였다 | 낮음 |
| **F-10** | `docs/marketing/CLAIM_AUDIT_R5.md:91` | 허가를 **낼 수 있는 곳이 딱 둘** — `InfoGearIconWidget` · `SettingsWindow`, 전수 grep 2건 | `ad49497` 프로덕션 발급은 **4곳**: `AppControlDirector.cs:985` 「캐릭터 우클릭」 · `CharacterInfoWindow.cs:945` 「정보창 열기」 · `InfoGearIconWidget.cs:1791` 「톱니 클릭」 · `SettingsWindow.cs:652` 「설정창 열기」. 같은 문서 `:116`은 괄호에 「지금 발급 4곳」을 병기해 **한 문서 안에서 서로 반대 말**을 한다 | 테스트 이름이 없는 약속이라 **스캐너 사각지대**(0-6) — verify-change 문맥 읽기로 발견 | 높음(출시 게이트 — 홍보 조건 근거) |

### 0-2. 정정은 병기돼 있고 이름만 낡음 — 실질 참 (거짓 서술 아님)

- `docs/marketing/TRUTH_INVENTORY.md:98` · `:1005` — 사라진 `등급1에서_톱니를_누르면_설정창까지_도달한다`를 인용하지만 다음 항목·같은 칸에 「**R12-7 자진 정정: 틀렸다 / 거짓 통과**」가 붙어 있다(표기어가 사전 밖이라 기계는 (다)). `:144`는 2판에서 「정정」 사전으로 (라).
- `docs/marketing/CLAIM_AUDIT_R5.md:116` — C3 칸 본문은 여전히 「⌃⌥⌘I는 **아직 안 된다**」인데 같은 칸 괄호에 「지금 `e6b14c2` 발급 4곳 … 옛 Ignore 테스트는 … 승격」이 병기돼 있다. ★ 한 칸 안에서 본문과 병기가 반대 말을 한다 — marketing 확인 권고(F-10과 같은 문서).
- `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:552` — 「자기모순(자백)」 절의 **과거 결함 서술**. 테스트는 `RightClickFanGateTests.cs:161` `F18_사용자_소환이_막힌_상태에서는_어떤_조합도_열지_않는다`로 고쳐졌다. 인용 줄 `:103-125`만 낡았다.

### 0-3. 심각도 높음 (나) — 실질 부재인가, 이름만 없는가

| 문서 위치 | 약속한 이름 | 판정 | 근거 |
|---|---|---|---|
| `docs/security/SECURITY_MODEL.md:1093` | `Tests/EditMode/WallClockReadScopeAuditTests.cs` | **실질 부재 — 이미 알려진 T-11-a** | 인계 명세(「이 파일을 만든다」). 같은 이름의 표 행은 「명세만」으로 (라) |
| `docs/security/SECURITY_MODEL.md:1682` · `:1693` · `:1696` | `IdleWindowRatchetTests` · `창_480분이면_하루_5760에서_잘린다` · `네거티브컨트롤_창을_없애면_실제로_17280까지_샌다` | 이름만 부재 | 바로 뒤 `:1684`가 「다른 이름으로 실재한다」고 스스로 적었다(`CurrencyRulesTests` 창 테스트) |
| `docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md:426` | `다른_asmdef는_스팀을_언급하지_않는다` | 이름만 부재 | 같은 검사가 `OfflineFirstNetworkAuditTests.cs:668-694` 안에 있다(런타임 asmdef만 「steam」 면제) |
| `docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md:411` | `CostumeSaveIsNotEntitlementTests` | 이름만 부재 | `CostumeEntitlementSingleGateTests.cs:491`이 `CharacterSaveStore`·`CostumeProgressModel` 참조를 금지한다 |
| `docs/strategy/ROADMAP.md:4844` · `docs/systems/TASKBAR_LOGOFF_ORDERING.md:164` | 토큰 명세 TK-07 · TK-41 이름 | 부재 정당 | 문서 머리가 「명세만 있다」인 `RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md`의 사례 이름을 인용한다(사전 밖 표기 「명세」) |
| `docs/manual/03-disappeared.md:608` | `PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_…` | **거짓 서술 F-2** | 위 0-1 |

⇒ **심각도 높음 (나) 중 T-11-a 같은 새 실질 부재는 0건**(연 9건 기준). 심각도 낮음 (나)는 **문장만 봤고 코드 대조는 안 했다(미확인)** — C절 목록.

### 0-4. 담당 제안 (배정은 리더)

| 항목 | 제안 담당 | 비고 |
|---|---|---|
| F-1 · F-2(03·01 두 장) · F-7 | 매뉴얼 라운드(`manual-writer`) | 03은 작업 트리에서 이미 F-7 표기 중 — 같은 라운드에 합류가 싸다 |
| F-3 | `product-strategy`(작업 트리 정정 중) · 사실 확인 `security` | |
| F-4 · F-10 · 0-2 `CLAIM_AUDIT_R5:116` | `marketing` | 한 문서 안의 「딱 둘」↔「4곳」 모순을 같이 닫는다 |
| F-5 | `product-strategy` | R1-I 근거 인용 교체 |
| F-6 | `game-architect`(작업 트리 반영 중) | |
| F-8 | `design-equipment` | 현재형 「잠근다」 → 소멸 병기 또는 현재 잠금 이름 |
| F-9 | `ux-designer` | 백로그 2번 행이 이미 끝났는지 판정 + 테스트 인용을 형식명으로 |
| F절 약한 표기 (라) 사람 확인 · 사전 밖 후보 편입 | `qa-regression` | 도구 소유 |

### 0-5. 판정 규칙 — 1판과 달라진 곳 (verify-change 실측 기준 · 리더 지시 2026-09-15)

1. **인덱스 무쓰기** — 모든 git 호출이 `git --no-optional-locks`(전역 옵션 위치)이고, 환경 변수 `GIT_OPTIONAL_LOCKS=0`도 함께 넘긴다. 1판 머리의 「파일을 쓰지 않는다」는 **사실과 달랐다**(plain `git status`가 `.git/index`를 고쳐 썼다). 스캐너가 부르는 하위 명령(`status` · `ls-files` · `ls-tree` · `log` · `show` · `blame` · `cat-file` · `rev-parse` · `merge-base`) 중 인덱스를 고쳐 쓰는 것은 `status`이고 porcelain `diff`는 부르지 않는다 — 그래서 옵션과 env는 사실상 `status` 계열에만 의미가 있다. 대조(로컬 복제본 · 매번 추적 파일 stat만 바꾼 뒤 `--selftest`): 수정본 인덱스 **불변** 97/97 · 옵션만 뺀 변이 **불변** 97/97(env가 따로 막는다) · env만 뺀 변이 **불변** 97/97 · ★ **옵션과 env를 둘 다 뺀 변이는 바뀜** — C10 빨강 96/97 · rc 2 · plain `git status` 바뀜. ★ **정정(2판 첫 제출 · verify-change 재현 실패)**: 여기 적었던 「옵션 뺀 변이 스캐너 바뀜」은 틀렸다 — 그 측정의 변이는 옵션을 빼면서 env를 `GIT_OPTIONAL_LOCKS="1"`(켬)로 **바꾼** 것이었는데 「옵션 뺀 변이」라고 적었다. 실저장소에서는 수정 뒤 `--selftest`를 한 번 돌렸고(인덱스 불변) 그 뒤 대조는 복제본에서만 했다. 교정 C10이 매 실행 잰다.
2. **V1** — 표기어를 찾기 전에 식별자 토큰(밑줄 이음 토큰 · `.cs` 경로)을 같은 길이로 가린다. 가리기만 하면 `TASKBAR_REVEAL.md` 「5차 이름 `…`에서 … **정정**」이 표기를 잃어서 「정정」과 정규식 「N차 이름」(`5차 이름` · `5-b 이름`)을 사전에 넣었다. ★ `5-b 이름` 형태는 리더 지시(「N차 이름」)를 넓힌 해석이다 — 같은 병기가 `TASKBAR_REVEAL.md`에 두 형태로 있어서다.
3. **V2는 하드 규칙이 아니다** — 표기가 이름과 **다른 문장·표 셀**에 있으면 (라)로 두되 **약한 표기**로 표시하고 F절 사람 확인 목록에 올린다. 스냅숏 행에도 「라약」으로 구분한다(약한 표기 전환도 가드가 잡는다).
4. **V3** — 표 머리·절·문서 머리의 좁은 사전에서 「미구현」·「구현 예정」을 뺐다.
5. **커밋 앵커** — `` `토큰` 기준/판/시점 ``은 토큰이 **실재 커밋일 때만**(`git cat-file -e <토큰>^{commit}`) 인정한다. ★ 리더 지시의 두 선택지 중 **「[a-f] 1자 이상」은 쓰지 않았다** — 숫자만인 실재 해시 `2051739`(`CornerHoverPanelTests` 소멸 커밋)를 잃는다. 교정: `20260915`·`2746944` 거부 / `2051739` 인정.
6. **잘린 이름** — 앞 조각이 8자 이상인데 현재·이력 선언 어디에도 없으면 (나)다. ★ 리더 지시는 「접두로 없으면」인데, **이름 안 어디에든 없으면**으로 좁혔다 — 실재 이름의 **가운데**를 인용한 `«사용자숨김_세계에서_톱니…»`(실제 `등급1_사용자숨김_세계에서_톱니를_…`)를 (나)로 오판하지 않게. 그 경우는 (마)로 남는다.
7. **파일 줄기 대조** — 형식 선언이 없어도 같은 이름의 테스트 파일이 있으면 (가)(비고 「파일 줄기 일치」), 파일이 사라졌으면 (다).
8. **합성 교정 이름 제외** — 스캐너 자신의 교정 이름(`ZzNoSuchPromiseTests` 등)이 보고 요약을 거쳐 실문서에 들어오면 제외한다. 교정: 실데이터의 다른 Zz 이름(`ZzVc5ReentrancyProbeTests`)은 제외하지 않는다.
9. **숨김 탐침 4종 교정**(합성) — 이름 속 「미구현」 → (나) · 300자 안 무관한 「삭제」(다른 문장) → 약한 표기 (라) · 8자리 날짜 앵커 → (다) · 절 제목 기능 낱말 → (나). 같은 형태 실데이터 3건은 `ad49497` 커밋판 문서로 교정(GAR `:2740` (다) · TASKBAR_REVEAL 「5차 이름 … 정정」 (라) · 매뉴얼 03 절 기능 낱말로 (라) 아님). 교정 C5 blob 문턱은 1000 → 900(현재값과 같던 문턱에 여유).
10. 1판에서 유지: (라) 넓은 범위(표 머리·절·문서 머리 좁은 사전) · (다) 커밋 기준 병기 · `renames.tsv` 옛 이름 (다) · 제외 칸(`…ForTests` 훅 · 약한 영문 밑줄 토큰 · 프로덕션 선언·문자열).

### 0-6. 한계 (못 보는 것)

- 선언 판정은 **글자 스캐너**다(파서 아님). 보증은 교정 C5(모든 테스트 `.cs`가 코드 상태로 끝나고 중괄호 균형, 까다로운 리터럴 합성)뿐이다. 테스트 트리의 `#if`는 지금 0건이다.
- **이름 없는 약속**(「테스트가 잠근다」 · 「허가 낼 곳 딱 둘」처럼 이름을 안 적은 문장)은 구조적으로 못 본다 — F-10이 그 예다.
- **파일 줄기 인용**: 파일이 있으면 안의 형식명이 달라도 (가)다 — 문장이 **형식**을 가리키면 실재로 오판할 수 있다(F-9가 그 경우 — 사람 판정으로 올렸다).
- **이름 속 표기어**: V1은 밑줄 이음 토큰과 `.cs` 경로만 가린다 — 밑줄 없는 식별자(`Foo미구현`) 안의 표기어는 못 가린다.
- **무관한 300자 표기**: 다른 문장이면 약한 표기로 목록에 오르지만, **같은 문장 안의 무관한 표기**는 여전히 (라)로 숨는다. 「정정」은 흔한 낱말이라 (라)를 늘린다(F-8은 약한 표기로 잡혔다).
- **절 기능 낱말**: V3는 두 낱말만 뺐다 — 남은 좁은 사전 낱말(「명세만」 · 「이름 제안」 등)이 절 제목에서 기능 설명으로 쓰이면 그 절의 사라진 테스트를 숨길 수 있다.
- **(마) 흡수**: 잘린 이름의 앞 조각이 어느 선언 안에라도 있으면 (마)다 — 실재 이름 일부를 인용한 **부재** 약속은 판정 불가로 흡수된다. 런타임 조립 규칙 `runtime_suffixes`는 `nameof(X) + "…"`도 흡수한다(`ad49497` (마) 영향 0).
- **커밋 앵커**: 숫자 7자리가 우연히 실재 커밋 접두와 같으면 앵커로 인정한다.
- **한글 부분 일치**: 8자 이상 부분 일치·조사 1자 제거까지 (가)로 센다 — 다른 테스트 이름 일부를 인용한 문장을 실재로 오판할 수 있다(비고 「부분 일치 →」).
- **영문 밑줄 이름**: 테스트 형태(`NegativeControl…` · `T1_…` · 낙타 3마디_낙타)일 때만 약속으로 본다. 그 밖 모양의 부재는 제외로 빠진다.
- **신선도 가드**는 (가)·제외 행을 스냅숏에 넣지 않는다 — B절 「미커밋」 목록이 낡아도 rc 0이다.
- **`--json`**: 저장소 안 경로만 거부한다. 그 밖 경로는 제한하지 않는다.
- **C10**은 같은 시각 다른 프로세스가 인덱스를 바꿔도 빨개진다(그때는 다시 돌린다). C10의 양성 대조는 매 실행이 아니라 복제본에서 한 번 했다(0-5 1번).
- 「거짓 서술」은 **사람 판정**이다. E절은 후보이고 과거형 기록과 현재형 주장을 기계가 가르지 못한다. `Tasklist.md`·`BASELINE.md`·`QA_REGRESSION_*` 같은 기록 문서의 (다)는 대부분 「그때 참」이다. `Tasklist.md`의 (라)는 개수만 봤다(개별 미확인).

### 0-7. Windows 영향 / macOS 영향

- **Windows 영향: 없음(문서·스크립트만)** — 스캐너는 소스 텍스트를 읽으므로 활성 빌드 타깃과 무관하게 Windows 전용 테스트 선언도 센다.
- **macOS 영향: 없음(문서·스크립트만)** — `.cs`·Unity 실행 0.

---

## A. 교정 결과 — 97/97 통과

- ✓ C6 입력 대조: glob = git ls-files(+미추적 −삭제) 문서·테스트
- ✓ C6 문서 집합 비공허(≥100 · CLAUDE.md · Tasklist.md · .claude/agents 포함) — 실제 183
- ✓ C6 테스트 파일 해석 수 ≥400 — 실제 431
- ✓ C6 프로덕션 파일 해석 수 ≥200 — 실제 297 (실패 0)
- ✓ C6 개명 대장 로드 ≥5 — 실제 7
- ✓ C5 작업 트리 테스트 .cs 해석 실패 0 — 실제 []
- ✓ C5 이력 blob 해석 실패 0 — 실제 0 []
- ✓ C5 이력 blob 해석 수 ≥900(09-15 첫 실측 1000에 여유 — 문턱이 현재값과 같으면 1개만 줄어도 빨갛다) — 실제 1003
- ✓ C5 [Test]류 어트리뷰트 달린 메서드 선언 ≥3000 — 실제 3569
- ✓ C7 사전 개수 = 명시 기대값 (단위 24+16 · 넓은 범위 8 · 비표기 복합어 8 · 사전밖 26 · 심각도 30+5)
- ✓ C1 실재 표본 AudioReactiveDanceGateAuditTests → (가) — 실제 가 []
- ✓ C1 실재 표본 UserAssetImmutabilityAuditTests → (가) — 실제 가 []
- ✓ C1 실재 표본 PlatformParityAuditTests → (가) — 실제 가 []
- ✓ C1 실재 표본 IncomeTimeSourceAuditTests → (가) — 실제 가 []
- ✓ C1 실재 표본 AudioReactiveDanceGateAuditTests/집중_모드_판정은_상태ID가_아니라_세션_플래그를 → (가) — 실제 가 []
- ✓ C1 실재 표본 숨김_판정은_기존_술어를_재사용하고_다시_구현하지_않는다 → (가) — 실제 가 []
- ✓ C1 실재 표본 Tests/EditMode/PlatformParityAuditTests.cs → (가) — 실제 가 []
- ✓ C1 실재 표본 AccessoryHatBandAndBellTests.NetInk.cs → (가) — 실제 가 []
- ✓ C1 실재 표본 AccessoryRuleOneCoverageTests/Waivers → (가) — 실제 가 []
- ✓ C1 실재 표본 LineRendererUvBandProbeTests/T2 → (가) — 실제 가 ['ID 접두 인용 → T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다']
- ✓ C2 WallClockReadScopeAuditTests → 이름 (나) — 실제 나
- ✓ C2 WallClockReadScopeAuditTests `.cs` pickaxe 0커밋 — 실제 0
- ✓ C2 양성: 같은 이름 문서 pickaxe ≥1커밋(프로브 생존) — 실제 2
- ✓ C2 1f7e139판 표 행 2곳(표기 없음) → 출현 (나) — 실제 [(997, '나', None, None), (1328, '나', None, None)]
- ✓ C2 1f7e139판 997행의 「부재 단언」(354자 뒤)은 표기로 세지 않음 — 실제 [(None, False)]
- ✓ C2 먼 표기(300자 밖 「명세만」) → (라) 아님 · 먼 표기 비고
- ✓ C2 HEAD판 표 행 2곳(「명세만」) → 출현 (라) — 실제 [(1045, '라', '명세만'), (1441, '라', '명세만')]
- ✓ C3 AudioActivationPolicyTests(`git log --diff-filter=D`) → (다) 1eb0e2b→1f7e139 — 실제 다 1eb0e2b 2026-09-03→1f7e139 2026-09-05
- ✓ C3 양성: CornerHoverPanelTests 가 HEAD 테스트 원문(주석)에 실재 ≥2파일 — 실제 2
- ✓ C3 CornerHoverPanelTests(주석에만 남음) → (다) …→2051739 — 실제 다 2051739 2026-09-02
- ✓ C3 GAME_ARCHITECTURE_REVIEW 인용 → 이름 (다) — 실제 [(2747, '다', '라', '삭제')]
- ✓ C3 개명 소멸 → (다) eb4670d + 개명 후보에 새 이름 — 실제 다 eb4670d 2026-09-14 ['소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다 · 등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다 · 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_우클릭_입구는_남긴다']
- ✓ C3 커밋 안 된 중간 이름(renames.tsv) → (다) 개명 대장 — 실제 다 개명 대장 → CardShapeContractTests.아이템은_한_벌이거나_두_벌이고_두_벌은_표면과_층이_명시된다
- ✓ C1 파일 줄기만 실재(`PopoverAndHoverPanelOpacityTests.cs` 안 형식명 다름) → (가) — 실제 가 ['파일 줄기 일치 — 파일은 실재, 안의 형식명은 다름']
- ✓ C4 숨김 탐침 실데이터(ad49497) — 이름 속 「미구현」 GAR `미해결_오디오_…_미구현이다` → (다) — 실제 [(2740, '다', '다', None)]
- ✓ C4 V1 뒤 실데이터(ad49497) — TASKBAR_REVEAL 「5차 이름 `…`에서 … 정정」 → (라) 정정/N차 이름 — 실제 [(150, '다', '라', 'N차 이름')]
- ✓ C4 숨김 탐침 실데이터(ad49497) — 매뉴얼 03 사라진 테스트가 절 기능 낱말로 (라) 아님 — 실제 [(352, '다', '다', '옛 인용', None)]
- ✓ C2' 문서 머리 「명세만」 문서의 사례 이름 → (라) — 실제 [(100, '나', '라', '문서 머리')]
- ✓ C2' 절 머리 「이름 제안」 아래 사례 이름 → (라) — 실제 [(2118, '나', '라', '절')]
- ✓ C4' `-runTests` 플래그를 테스트 클래스로 뽑지 않음 — 실제 0
- ✓ C4 합성 .cs 해석(까다로운 리터럴 포함) — 실제 []
- ✓ C4 합성 비약속 토큰 미추출: ZzWildAuditTests
- ✓ C4 합성 비약속 토큰 미추출: 1_수정전_합성
- ✓ C4 합성 비약속 토큰 미추출: ZZ_ENV_VAR로
- ✓ C4 합성 비약속 토큰 미추출: _합성_강조_
- ✓ C4 합성 ZzNoSuchPromiseTests → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 ZzPlannedPromiseTests → 이름 (나) 출현 (라) — 실제 [('나', '라', '단위')]
- ✓ C4 합성 ZzRealSynthTests.Zz_진짜_선언 → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 ZzCommentOnlyTests → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 ZzStringOnlyTests → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 ZzDocCommentTests → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 ZzBlockCommentTests → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 Zz_주석_안_이름 → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 Zz_문자열_안_이름 → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 Zz_런타임_조립_이름 → 이름 (마) 출현 (마) — 실제 [('마', '마', None)]
- ✓ C4 합성 ZzRealSynthTests.ZzWaivers → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 ZzRealSynthTests.ZzProp → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 ZzRealSynthTests.zzLocal → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 ZzRealSynthTests.T3z → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 ZzRealSynthTests.Q7_Q8_…중간_생략_끝 → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 합성_부분_일치_긴_이름 → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 Zz_진짜_선언은 → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 ResetForTests → 이름 (제외) 출현 (제외) — 실제 [('제외', '제외', None)]
- ✓ C4 합성 SM_ConfigureOverlayWindow → 이름 (제외) 출현 (제외) — 실제 [('제외', '제외', None)]
- ✓ C4 합성 ZzAscendCancelWhenWall_GoesToFall → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 Zz_절_제안_이름_하나 → 이름 (나) 출현 (라) — 실제 [('나', '라', '절')]
- ✓ C4 합성 AudioActivationPolicyTests → 이름 (다) 출현 (다) — 실제 [('다', '다', None)]
- ✓ C4 합성 CornerHoverPanelTests → 이름 (다) 출현 (라) — 실제 [('다', '라', '단위')]
- ✓ C4 합성 ZzAbsentMarkTests → 이름 (나) 출현 (라) — 실제 [('나', '라', '단위')]
- ✓ C4 합성 이_합성_템플릿_꼬리 → 이름 (마) 출현 (마) — 실제 [('마', '마', None)]
- ✓ C4 합성 Zz_합성_기능이_미구현이다 → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 BattleRetryDialogueSyncTests → 이름 (다) 출현 (다) — 실제 [('다', '다', None)]
- ✓ C4 합성 Zz_기능절_아래_이름_하나 → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 ZzStemFileTests → 이름 (가) 출현 (가) — 실제 [('가', '가', None)]
- ✓ C4 합성 ZzRealSynthTests.Zz_아무데도_없는_앞조각…꼬리 → 이름 (나) 출현 (나) — 실제 [('나', '나', None)]
- ✓ C4 합성 ZzRealSynthTests.Zz…없는꼬리 → 이름 (마) 출현 (마) — 실제 [('마', '마', None)]
- ✓ C4 합성 중괄호 전개 Zz{RealSynth,NoSuchBrace}Tests → (가)+(나) — 실제 ['가', '나']
- ✓ C4 합성 `ZzRealSynthTests.cs가` → 클래스 (가), 메서드 「cs가」로 뽑지 않음
- ✓ C4 합성 `-runTests` 미추출
- ✓ C4 숨김 탐침 ② 300자 안 무관한 「삭제」(다른 문장) → (라)이되 **약한 표기**로 F절 사람 확인 목록(V2는 하드 규칙 아님) — 실제 [('다', '라', '삭제', True)]
- ✓ C4 커밋 앵커 실재 확인 — 날짜 `20260915`·비객체 `2746944` 거부 / 숫자만인 실재 해시 `2051739` 인정
- ✓ C4 합성 교정 이름 제외 규칙 — 양성 `ZzNoSuchPromiseTests` 제외 · 음성 실데이터 Zz 이름 `ZzVc5ReentrancyProbeTests`는 제외 아님
- ✓ C4 실문서에 되먹인 합성 교정 이름 1출현 → 전부 제외
- ✓ C4 합성 심각도: 원칙 3 · 세이브 문맥 → 높음
- ✓ C4 합성 문서 머리 「명세만」(120줄 아래 이름) → (라) 문서 머리 — 실제 [('라', '문서 머리')]
- ✓ C5 합성 선언 Zz_유니티_선언 추출
- ✓ C5 합성 선언 Zz_파라미터 추출
- ✓ C5 합성 선언 Zz_람다아님 추출
- ✓ C5 합성 선언 Zz_호출만 추출
- ✓ C5 합성 주석·문자열 안 class 선언 0
- ✓ C5 합성 호출(`=> Zz_호출만()`)을 선언으로 세지 않음 — 실제 선언 1
- ✓ C5 합성 메서드 안 지역 변수를 멤버로 세지 않음
- ✓ C8① 줄만 밀림 → 스냅숏 행 불변
- ✓ C8② 앵커 문자열 변경 → 신선도 가드 불일치(rc=2)
- ✓ C8③ 항목 삭제 → (라) 정확히 -1 · 나머지 불변 · 가드 불일치 — 실제 5→4
- ✓ C9 누출 가드 양성 대조(홈 경로를 잡고, 가린 뒤 통과)
- ✓ C10 `.git/index` 무쓰기 — 실행 전후 mtime·크기·내용 동일(모든 git 호출 `--no-optional-locks`). ★ 같은 시각 다른 프로세스(리더 커밋)가 인덱스를 바꿔도 여기서 빨개진다 — 그때는 다시 돌린다

## B. 분류 개수

- 대상: 문서 183 · 테스트 `.cs` 431 · 이력 blob 1003 · HEAD 1부모 커밋(Assets 변경) 148 · 제외 문서: `docs/verify/PROMISED_TEST_EXISTENCE_SCAN.md`(결과 문서 자신(자기 인용 순환))
- **출현 단위**: (가) 2768 · (나) 39 · (다) 75 · (라) 152 · (마) 7 · 제외 214 · 패턴 표기(와일드카드, 판정 안 함) 6
- **이름 단위(고유 키)**: (가) 975 · (나) 122 · (다) 72 · (마) 7 · (제외) 108
- (라) 표기 범위별: 단위 82 · 표 머리 6 · 절 19 · 문서 머리 45
- (라) 중 **약한 표기**(표기가 이름과 다른 문장·표 셀 — F절 사람 확인 목록) 38
- (라) 표기 적중 낱말: 「명세만」 49 · 「삭제」 26 · 「이름 제안」 17 · 「개명」 9 · 「부재」 9 · 「정정」 7 · 「이름(안)」 6 · 「개작」 4 · 「존재한 적 없」 3 · 「N차 이름」 3 · 「옛 인용」 3 · 「존재하지 않」 2 · 「사라졌」 2 · 「추가 권고」 2 · 「한 번도 없」 2 · 「사라진」 2 · 「예정」 1 · 「사라짐」 1 · 「폐기」 1 · 「착수 전」 1 · 「계획」 1 · 「이관」 1
- 표기 사전(단위, 40): 「명세만」 · 「미작성」 · 「작성 예정」 · 「미착수」 · 「착수 전」 · 「미구현」 · 「구현 예정」 · 「추가 예정」 · 「신설 예정」 · 「만들 예정」 · 「작성할 것」 · 「계획」 · 「예정」 · 「TODO」 · 「아직 없」 · 「테스트 없음」 · 「테스트가 없」 · 「존재한 적 없」 · 「부재」 · 「한 번도 없」 · 「존재하지 않」 · 「이름 제안」 · 「이름(안)」 · 「추가 권고」 · 「삭제」 · 「제거됨」 · 「제거했」 · 「제거된」 · 「폐기」 · 「개명」 · 「이름이 바뀌」 · 「사라졌」 · 「사라짐」 · 「사라진」 · 「대체됐」 · 「대체됨」 · 「개작」 · 「옛 인용」 · 「이관」 · 「정정」
- 표기 사전(표 머리·절·문서 머리, 8): 「명세만」 · 「미작성」 · 「작성 예정」 · 「미착수」 · 「이름 제안」 · 「이름(안)」 · 「추가 권고」 · 「존재한 적 없」
- 표기로 세지 않는 복합어(8): 「부재 단언」 · 「부재단언」 · 「부재 대조」 · 「이동·삭제」 · 「삭제·수정」 · 「삭제하지 않」 · 「삭제 금지」 · 「삭제되지 않」
- 사전 밖 표기 후보 낱말(26): 「없다」 · 「없음」 · 「없는」 · 「필요」 · 「제안」 · 「후보」 · 「신규」 · 「신설」 · 「추가」 · 「만들」 · 「후속」 · 「인계」 · 「백로그」 · 「보류」 · 「가칭」 · 「예시」 · 「예:」 · 「가정」 · 「권고」 · 「쓸 것」 · 「쓴다」 · 「거짓」 · 「옛 이름」 · 「옛 판」 · 「명세」 · 「승인 후」
- (가) 중 「작업 트리 실재 · 미커밋」 이름 0: 
- 제외 사유별: 약한 영문 밑줄 토큰(네이티브·직렬화 필드·수식 기호 등) — 테스트 이름 근거 없음 162 · 테스트 훅(…ForTests) — 테스트 클래스 약속 아님 51 · 스캐너 합성 교정 이름(보고 요약이 실데이터로 되먹인 것) — 테스트 약속 아님 1

## C. (나) 한 번도 존재한 적 없음 — 미표기 — 설계·명세·체크표 문서 20건

| 파일:줄 | 이름 | 형태 | 약속 문장 요지 | `git log -S`·선언 이력 | 심각도 | 비고 |
|---|---|---|---|---|---|---|
| `docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md:411` | `CostumeSaveIsNotEntitlementTests` | class | 다** — C층을 세이브에서 뺀 목적이 **표적 제거**였다. > 감사 1건: 'CostumeSaveIsNotEntitlementTests' — 'Core/Cos | `.cs` 0커밋 | 높음(세이브) |  |
| `docs/manual/03-disappeared.md:608` | `PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다` | cm | 실을 **실행 가능한 형태로** 박아 두었고, PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼 | `.cs` 0커밋 | 높음(세이브) | 클래스는 실재, 멤버만 없음; 먼 표기「사라진」388자 밖(불인정); 사전 밖 표기 후보「없다」 |
| `docs/security/SECURITY_MODEL.md:1093` | `WallClockReadScopeAuditTests.cs` | path | 1. **파일**: 'Tests/EditMode/WallClockReadScopeAuditTests.c | `.cs` 0커밋 | 높음(보안) |  |
| `docs/security/SECURITY_MODEL.md:1682` | `IdleWindowRatchetTests` | class | ### (B) ★ 신규 'IdleWindowRatchetTests' — **순수 함수 동작 + 네거티브 컨 | `.cs` 0커밋 | 높음(보안) | 사전 밖 표기 후보「신규」 |
| `docs/security/SECURITY_MODEL.md:1693` | `창_480분이면_하루_5760에서_잘린다` | ko | econds) // T-15-1-b의 순수 함수를 그대로 호출 [Test] 창_480분이면_하루_5760에서_잘린다() => Assert.AreEqu | `.cs` 0커밋 | 높음(보안) | 사전 밖 표기 후보「가정」 |
| `docs/security/SECURITY_MODEL.md:1696` | `네거티브컨트롤_창을_없애면_실제로_17280까지_샌다` | ko | nds)); // ★ 상수 참조, 숫자 안 베낌 [Test] ★네거티브컨트롤_창을_없애면_실제로_17280까지_샌다() => Assert | `.cs` 0커밋 | 높음(보안) | 사전 밖 표기 후보「가정」 |
| `docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md:426` | `다른_asmdef는_스팀을_언급하지_않는다` | ko | 로 시작)가 하나라도 있으면 실패 + "이름 형태로 저장하라" 안내 [Test] 다른_asmdef는_스팀을_언급하지_않는다() Assets 아래 asm | `.cs` 0커밋 | 높음(보안) | 사전 밖 표기 후보「승인 후」 |
| `docs/strategy/ROADMAP.md:4844` | `잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다` | ko | 설치**가 늘어난다. 이것이 레거시 분기가 영구히 떠안는 잔여위험(TK-07 '잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다')의 모수다. v1이 | `.cs` 0커밋 | 높음(출시 게이트) | 사전 밖 표기 후보「없는」 |
| `docs/systems/TASKBAR_LOGOFF_ORDERING.md:164` | `세션_종료_경로도_토큰_파생_흔적을_닫는다` | ko | strategy/ROADMAP.md:4724'). 토큰 명세 **TK-41** '세션_종료_경로도_토큰_파생_흔적을_닫는다'('RESERVED_BAR_OWNER_ | `.cs` 0커밋 | 높음(출시 게이트) | 사전 밖 표기 후보「명세」 |
| `docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md:366` | `CostumeAccrualGridTests` | class | **남는 테스트 1건**: 'Tests/EditMode/CostumeAccrualGridTests' — 세 종료 경로(M-21)에서 누적 | `.cs` 0커밋 | 낮음 | 사전 밖 표기 후보「만들」 |
| `docs/SCREEN_SHARE_DETECTION.md:331` | `ForeignFullscreenTierTests` | class | ¦ EditMode 'ForeignFullscreenTierTests' ¦ 'Resolve()' 4분기 | `.cs` 0커밋 | 낮음 |  |
| `docs/SCREEN_SHARE_DETECTION.md:333` | `PanelRetreatOnForeignFullscreenTests` | class | ¦ PlayMode 'PanelRetreatOnForeignFullscreenTests' ¦ 등급 1에 | `.cs` 0커밋 | 낮음 |  |
| `docs/UI_SURFACE_SPEC.md:72` | `UiSpacingTokenAuditTests` | class | - 못 박는 법: 'Tests/EditMode/UiSpacingTokenAuditTests' — 'UiGlyphExactness | `.cs` 0커밋 | 낮음 |  |
| `docs/UX_CHARACTER_WINDOW_REFINE.md:1693` | `CostumeProgressReadoutTests.코스튬_3행이_켜졌을_때의_세로_예산` | cm | 1. 'Tests/EditMode/CostumeProgressReadoutTests.코스튬_3행이_켜졌을_때의_세로 | `.cs` 0커밋 | 낮음 | 클래스는 실재, 멤버만 없음 |
| `docs/inspection/R1_거짓주석_수정배정.md:19` | `VisibleTopEdgeSolverTests.cs` | path | a ¦ 'Platform/VisibleTopEdgeSolver.cs:25' ¦ 'Tests/EditMode/VisibleTopEdgeSolverTests.cs'가 | `.cs` 2커밋 (7ed996d, c256a58) | 낮음 | 사전 밖 표기 후보「없음」 |
| `docs/inspection/R2_거짓주석_전수조사.md:310` | `주석이_지목한_우리_타입의_멤버가_실재한다` | ko | > '주석이_지목한_우리_타입의_멤버가_실재한다' > — 주석의 'Type.Member | `.cs` 0커밋 | 낮음 |  |
| `docs/localization/PLAN_1.0.md:481` | `LocalizationDebtCeilingAuditTests.cs` | path | ### 4-2. 설계 — 'Tests/EditMode/LocalizationDebtCeilingAuditTe | `.cs` 0커밋 | 낮음 | 사전 밖 표기 후보「신규」 |
| `docs/verify/BASELINE.md:49` | `전체화면_판정_한_줄에_사용자숨김을_얹지_않는다` | ko | ¦ 1595 ¦ 2 ¦ 12 ¦ 목_형상은_데이터화_전후로_비트까지_같다<br>전체화면_판정_한_줄에_사용자숨김을_얹지_않는다 ¦ | `.cs` 0커밋 | 낮음 |  |
| `docs/verify/QA_REGRESSION_2026-09-05.md:402` | `실기미확인_착지티어_교차배율이_Windows에서…` | ko | ¦ 09-02 02:39 ¦ '…실기미확인_착지티어_교차배율이_Windows에서…' ¦ 실기 하드웨어 필요 ¦ 자 | `.cs` 0커밋 | 낮음 | 잘린 이름 — 앞 조각 「실기미확인_착지티어_교차배율이_Windows에서」이 현재·이력 선언 어디에도 없다; 사전 밖 표기 후보「필요」 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:604` | `VisibleTopEdgeSolverTests.cs` | path | ¦ 'Platform/VisibleTopEdgeSolver.cs:25' ¦ 'Tests/EditMode/VisibleTopEdgeSolverTests.cs' | `.cs` 2커밋 (7ed996d, c256a58) | 낮음 |  |

## C. (나) 한 번도 존재한 적 없음 — 미표기 — `Tasklist.md`(날짜별 기록) 19건

| 파일:줄 | 이름 | 형태 | 약속 문장 요지 | `git log -S`·선언 이력 | 심각도 | 비고 |
|---|---|---|---|---|---|---|
| `Tasklist.md:15056` | `WindowsFullscreenGamePolicyTests.Windows_구현에는_레지스트리_쓰기_API가_한_건도_없다` | cm | 리더 확인 요청**: 레지스트리 쓰기 API 금지 스캔을 지금은 새 테스트 'WindowsFullscreenGamePolicyTests.Windows_구현에는 | `.cs` 1커밋 (767c985) | 높음(출시 게이트,원칙) | 클래스는 실재, 멤버만 없음; `.cs` pickaxe 1커밋 — 주석·문자열·부분 문자열에만 등장(선언 이력 0); 사전 밖 표기 후보「없다」 |
| `Tasklist.md:6545` | `ZZGetupFloorDiagnostic.cs` | path | - 'Tests/PlayMode/ZZGetupFloorDiagnostic.cs' — 랙 | `.cs` 0커밋 | 낮음 |  |
| `Tasklist.md:6547` | `ZZGetupAngleSweep.cs` | path | - 'Tests/PlayMode/ZZGetupAngleSweep.cs' — 정착각 강제 | `.cs` 1커밋 (0dd904f) | 낮음 |  |
| `Tasklist.md:6719` | `ZZCornerPanelCaptureHarness.cs` | path | 령('-batchmode -nographics -runTests')으로 돌리면 'Tests/PlayMode/ZZCornerPanelCaptureHarness.cs | `.cs` 0커밋 | 낮음 | 먼 표기「삭제」449자 밖(불인정) |
| `Tasklist.md:8645` | `ZZExploratorySweepTests` | class | - 'ZZExploratorySweepTests.cs:367' — 'for (int i | `.cs` 0커밋 | 낮음 | 임시 탐침 이름 형태(Zz 접두) |
| `Tasklist.md:8646` | `ZZExploratorySweep3Tests` | class | - 'ZZExploratorySweep3Tests.cs:257' — 같은 방식으로 40 | `.cs` 0커밋 | 낮음 | 임시 탐침 이름 형태(Zz 접두) |
| `Tasklist.md:9663` | `VisibleTopEdgeSolverTests.cs` | path | - 'VisibleTopEdgeSolver.cs' 클래스 문서가 'Tests/EditMode/VisibleTopEdgeSolverTests.cs'를 | `.cs` 2커밋 (7ed996d, c256a58) | 낮음 | 사전 밖 표기 후보「없다」 |
| `Tasklist.md:12165` | `머리_4종이_서로_구분된다` | ko | 2. **바가지머리 vs 단정한머리**는 '머리_4종이_서로_구분된다'의 지표(각도별 최대 반경)로는 0.20R 차이라 | `.cs` 0커밋 | 낮음 |  |
| `Tasklist.md:12248` | `InkColorPersistenceTests.구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_...` | cm | ¦ 2 ¦ 'InkColorPersistenceTests.구버전_파일을_읽은_뒤_저장하면_v7 | `.cs` 0커밋 | 낮음 | 잘린 이름 — 앞 조각 「구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_」이 현재·이력 선언 어디에도 없다 |
| `Tasklist.md:13181` | `AccessoryStrokeBudgetTests.머리_4종이_서로_구분된다` | cm | 3. **기존 검사 확장** — 'AccessoryStrokeBudgetTests.머리_4종이_서로_구분된다' → | `.cs` 0커밋 | 낮음 | 클래스는 실재, 멤버만 없음; 사전 밖 표기 후보「쓴다」 |
| `Tasklist.md:21824` | `DialogueComicTextPlacementTests.TiltFollowsTheConfig` | cm | ¦ 'DialogueComicTextPlacementTests.TiltFollowsTh | `.cs` 1커밋 (767c985) | 낮음 | 클래스는 실재, 멤버만 없음; `.cs` pickaxe 1커밋 — 주석·문자열·부분 문자열에만 등장(선언 이력 0) |
| `Tasklist.md:22043` | `미해결_Windows_알파필터가_WS_EX_TRANSPARENT_단독창을_놓친다` | ko | - 지시: '미해결_Windows_알파필터가_WS_EX_TRANSPARENT_단독창을_놓친다' | `.cs` 1커밋 (7ed996d) | 낮음 | `.cs` pickaxe 1커밋 — 주석·문자열·부분 문자열에만 등장(선언 이력 0); 사전 밖 표기 후보「추가」 |
| `Tasklist.md:22433` | `CategoryTintAssetAlignmentAuditTests` | class | - ★ **리더가 승인한 'CategoryTintAssetAlignmentAuditTests'는 전제가 틀렸 | `.cs` 0커밋 | 낮음 |  |
| `Tasklist.md:22434` | `ItemColorClosedSetAuditTests` | class | 참이면 왕관(Gold) vs HEAD 주황도 위반이어야 일관된다. 대체안 'ItemColorClosedSetAuditTests'(닫힌 색 집합 + 분모 하한 | `.cs` 0커밋 | 낮음 |  |
| `Tasklist.md:24863` | `DockLandingSilhouetteTests` | class | trokeParityTests(주석 경로 오타, 편집권한 있는 라운드 필요) / DockLandingSilhouetteTests(PlayMode 리그 필요). | `.cs` 3커밋 (349048f, 512bec2, a1c64b5) | 낮음 | `.cs` pickaxe 3커밋 — 주석·문자열·부분 문자열에만 등장(선언 이력 0); 사전 밖 표기 후보「필요」 |
| `Tasklist.md:25339` | `축소폴백_원도_같이_줄어든다` | ko | rMenuShrinkFallbackRenderTests(동반, Companion=축소폴백_원도_같이_줄어든다 검사·2/2 Passed 확인) 2건 등재. 검증: | `.cs` 0커밋 | 낮음 | 사전 밖 표기 후보「거짓」 |
| `Tasklist.md:26337` | `CostumeAccrualGridTests` | class | . **P1 통과조건 4항에는 없으나 게이트의 실제 입구**라 배정 권고. / 'CostumeAccrualGridTests'(3종료 경로 격자 동일성)는 'Foc | `.cs` 0커밋 | 낮음 | 사전 밖 표기 후보「필요」 |
| `Tasklist.md:27621` | `VcE12ProbeTests` | class | '로)가 EditMode 전량 + PlayMode 전량 787을 통과하고 탐침('VcE12ProbeTests')에만 빨강 → 생존. F18e 명부는 7개 파일 중 | `.cs` 0커밋 | 낮음 | 먼 표기「정정」732자 밖(불인정) |
| `Tasklist.md:27747` | `IdleWindowRatchetTests` | class | 1 #2·T-14-9 #3은 '349048f' 실재 표시, T-15-7 (B) 'IdleWindowRatchetTests'는 그 이름으론 없으나 'Currency | `.cs` 0커밋 | 낮음 | 먼 표기「존재한 적 없」1084자 밖(불인정) |

## D. (다) 있었다가 사라짐 — 미표기 — 설계·명세·체크표 문서 40건

| 파일:줄 | 이름 | 형태 | 약속 문장 요지 | `git log -S`·선언 이력 | 심각도 | 비고 |
|---|---|---|---|---|---|---|
| `docs/marketing/CLAIM_AUDIT_R5.md:93` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | ko | sts/PlayMode/FullscreenPanelRetreatTests' — '등급1에서_톱니를_누르면_설정창까지_도달한다()' ¦ | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 높음(출시 게이트) |  |
| `docs/marketing/CLAIM_AUDIT_R5.md:116` | `FullscreenPanelRetreatTests.미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | cm | 창을 열면 **열자마자 닫힌다**. 저장소가 스스로 갭이라고 선언해 두었다 ¦ 'FullscreenPanelRetreatTests.미해결_등급1에서_정보창_단축키 | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 높음(출시 게이트) | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다 · 등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다 · 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_우클릭_입구는_남긴다; 사전 밖 표기 후보「없다」 |
| `docs/marketing/TRUTH_INVENTORY.md:98` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | cm | - R5-1 근거 'FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 높음(출시 게이트) | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다 · 등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다 · 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_우클릭_입구는_남긴다 |
| `docs/marketing/TRUTH_INVENTORY.md:1005` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | cm | faceSummonPolicy'(임대 0.5초) + 'Tests/PlayMode/FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 높음(출시 게이트) | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다 · 등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다 · 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_우클릭_입구는_남긴다; 사전 밖 표기 후보「거짓」 |
| `docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md:400` | `전송계열_화이트리스트는_현재_비어_있다` | ko | ### 7-6. ★ '전송계열_화이트리스트는_현재_비어_있다'를 **니들 종류를 바꿔서 초록으로 만들지 | 도입 767c985 2026-09-01 → 소멸 1f7e139 2026-09-05; `.cs` 2커밋 (1f7e139, 767c985) | 높음(보안) | 사전 밖 표기 후보「만들」 |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:552` | `F18_전체화면_억제_중에는_어떤_조합도_열지_않는다` | ko | - EditMode 'F18_전체화면_억제_중에는_어떤_조합도_열지_않는다'('RightClickFan | 도입 0229f52 2026-09-06 → 소멸 eb4670d 2026-09-14; `.cs` 1커밋 (0229f52) | 높음(원칙) |  |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:408` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | ko | lscreenPanelRetreatTests.cs' ¦ 6 ¦ 등급 1 탈출구('등급1에서_톱니를_누르면_설정창까지_도달한다') ¦ | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 |  |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:502` | `FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다` | cm | - 'FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다 | 도입 7ed996d 2026-09-02 → 소멸 eb4670d 2026-09-14; `.cs` 4커밋 (eb4670d, 349048f, 1f7e139) | 낮음 | 이력 일치 → 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다; 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_우클릭_입구는_남긴다 · 등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다 · 등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다; 사전 밖 표기 후보「신규」 |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:502` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | ko | creenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다' / '등급1에서_톱니를_누르면_설정창까지_도달한다' → ★ **실패한다.** 등급 1에 | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 | 사전 밖 표기 후보「신규」 |
| `docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md:66` | `착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다` | ko | - 테스트 2 '착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다': 최대 세기 | 도입 0229f52 2026-09-06 → 소멸 a1294a9 2026-09-06; `.cs` 2커밋 (a1294a9, 0229f52) | 낮음 |  |
| `docs/EQUIPMENT_SHAPE_SPEC.md:291` | `EyesVisorOpacityTests.채움이_눈_자리를_덮는다` | cm | 'EyesVisorOpacityTests.채움이_눈_자리를_덮는다'는 외알/안대에 | 도입 767c985 2026-09-01 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 767c985) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 가리개_채움이_눈_자리를_덮는다 · 한쪽만_가리는_물건만_반대쪽_눈을_보여준다 · IsDrawnEye |
| `docs/GAME_ARCHITECTURE_REVIEW.md:2041` | `등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다` | ko | ★ **실무상 위험**: 누군가 B를 고치면 '등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다'가 **빨개진다.** | 도입 7ed996d 2026-09-02 → 소멸 eb4670d 2026-09-14; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/GAME_ARCHITECTURE_REVIEW.md:2740` | `미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다` | ko | 코드는 여전히 0줄이다.** 'PlatformParityAuditTests'의 '미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다'가 'Assert.Ign | 도입 1eb0e2b 2026-09-03 → 소멸 1f7e139 2026-09-05; `.cs` 2커밋 (1f7e139, 1eb0e2b) | 낮음 | 사전 밖 표기 후보「없다」 |
| `docs/MOTION_SPEC.md:1966` | `LandingCrouchTests.DockStepDropAbsorbsSoftlyWithoutKneeling` | cm | 1. 'LandingCrouchTests.DockStepDropAbsorbsSoftlyW | 도입 2051739 2026-09-02 → 소멸 a1c64b5 2026-09-02; `.cs` 2커밋 (a1c64b5, 2051739) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): DockStepDropNeverLooksLikeKneelingAtAnySelectableScale · _restoreScale · ApplyScaleAndRecapture |
| `docs/UX_CHARACTER_WINDOW_REFINE.md:1694` | `CharacterStatColumnLayoutTests.설계_크기에서_컬럼2가_본문_높이_안에_들어간다` | cm | 2. 'Tests/EditMode/CharacterStatColumnLayoutTests.설계_크기에서_컬럼2가_본 | 도입 0229f52 2026-09-06 → 소멸 6156c73 2026-09-08; `.cs` 2커밋 (6156c73, 0229f52) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 최악_상태_설계_크기에서_컬럼2_잉크가_한_픽셀도_잘리지_않는다 · 최악_상태에서도_잉크_아래로_한_칸_여백이_남는다 · InkBottom |
| `docs/UX_FLOW.md:6524` | `EyesVisorOpacityTests.채움이_눈_자리를_덮는다` | cm | ¦ 'EyesVisorOpacityTests.채움이_눈_자리를_덮는다' ¦ **수정 없 | 도입 767c985 2026-09-01 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 767c985) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 가리개_채움이_눈_자리를_덮는다 · 한쪽만_가리는_물건만_반대쪽_눈을_보여준다 · IsDrawnEye; 사전 밖 표기 후보「권고」 |
| `docs/UX_FLOW.md:9080` | `ComicFontFloorOutlineRingTests.배율1에서는_두_요구가_양립하지_않는다_보류` | cm | **★ 미해결 갭의 재작성** — 'ComicFontFloorOutlineRingTests.배율1에서는_두_요구가_양 | 도입 2051739 2026-09-02 → 소멸 89de9de 2026-09-02; `.cs` 3커밋 (89de9de, 512bec2, 2051739) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 배율1에서는_속공간이_검증_운용점에_못_미친다_보류; 사전 밖 표기 후보「필요」 |
| `docs/manual/03-disappeared.md:321` | `미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | ko | yMode/FullscreenPanelRetreatTests 미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다 (Assert. | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 | 먼 표기「정정」424자 밖(불인정) |
| `docs/manual/03-disappeared.md:352` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | cm | 없음, InfoGearIconWidget.cs:933). · 러너 FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다 · 등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다 · 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_우클릭_입구는_남긴다; 먼 표기「옛 인용」1232자 밖(불인정); 사전 밖 표기 후보「없다」 |
| `docs/strategy/CHANNEL_PRICING_DECISIONS.md:1678` | `전송계열_화이트리스트는_현재_비어_있다` | ko | 그리고 **전송계열 화이트리스트는 0건으로 못박혀 있다** — 같은 파일의 '전송계열_화이트리스트는_현재_비어_있다()' 테스트가 그것을 잠근다. | 도입 767c985 2026-09-01 → 소멸 1f7e139 2026-09-05; `.cs` 2커밋 (1f7e139, 767c985) | 낮음 |  |
| `docs/strategy/ROADMAP.md:3738` | `등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다` | ko | ¦ 298 ¦ '등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다()' ¦ ★★ **등 | 도입 7ed996d 2026-09-02 → 소멸 eb4670d 2026-09-14; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/strategy/ROADMAP.md:3740` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | ko | ¦ 555 ¦ '등급1에서_톱니를_누르면_설정창까지_도달한다()' ¦ **불변식 R1-I** — | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 |  |
| `docs/strategy/ROADMAP.md:3741` | `미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | ko | ¦ 714 ¦ '미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다()' ¦ ★ ** | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 |  |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:557` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | cm | - 대상: - 'FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다 · 등급1에서_정보창_단축키_경로도_허가를_받아_열린_채_머문다 · 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_우클릭_입구는_남긴다 |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:558` | `등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다` | ko | 등급 1에서 톱니 가시성을 **「탈출구의 첫 홉」**으로 단언한다 - 형제 '등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다' | 도입 7ed996d 2026-09-02 → 소멸 eb4670d 2026-09-14; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:782` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | ko | ¦ T-3 ¦ **거짓 통과 처리**. '등급1에서_톱니를_누르면_설정창까지_도달한다'와 '등급1은_…톱니는_남긴다'의 전 | 도입 1eb0e2b 2026-09-03 → 소멸 eb4670d 2026-09-14; `.cs` 2커밋 (eb4670d, 1eb0e2b) | 낮음 | 사전 밖 표기 후보「거짓」 |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:782` | `등급1은_…톱니는_남긴다` | ko | ¦ **거짓 통과 처리**. '등급1에서_톱니를_누르면_설정창까지_도달한다'와 '등급1은_…톱니는_남긴다'의 전제를 **「톱니가 보이는 세계(사용자 숨김·가출)」 | 도입 7ed996d 2026-09-02 → 소멸 eb4670d 2026-09-14; `.cs` 4커밋 (eb4670d, 349048f, 1f7e139) | 낮음 | 이력 부분 일치 → 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다; 사전 밖 표기 후보「거짓」 |
| `docs/verify/BASELINE.md:43` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | ko | mallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니 | 도입 7ed996d 2026-09-02 → 소멸 1f7e139 2026-09-05; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/verify/BASELINE.md:50` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | ko | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 ¦ | 도입 7ed996d 2026-09-02 → 소멸 1f7e139 2026-09-05; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/verify/BASELINE.md:54` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | ko | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 ¦ | 도입 7ed996d 2026-09-02 → 소멸 1f7e139 2026-09-05; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/verify/BASELINE.md:56` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | ko | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 ¦ | 도입 7ed996d 2026-09-02 → 소멸 1f7e139 2026-09-05; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/verify/BASELINE.md:58` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | ko | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>설정창_톱니_위치_행은_옮 | 도입 7ed996d 2026-09-02 → 소멸 1f7e139 2026-09-05; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/verify/BASELINE.md:60` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | ko | mallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니 | 도입 7ed996d 2026-09-02 → 소멸 1f7e139 2026-09-05; `.cs` 1커밋 (7ed996d) | 낮음 |  |
| `docs/verify/BASELINE.md:95` | `대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다` | ko | 0e2b ¦ ? ¦ **~WIN** ¦ 1914 ¦ 1894 ¦ 2 ¦ 18 ¦ 대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다<br>상태_테두 | 도입 1f7e139 2026-09-05 → 소멸 a1294a9 2026-09-06; `.cs` 1커밋 (1f7e139) | 낮음 |  |
| `docs/verify/QA_REGRESSION_2026-09-05.md:304` | `WornShapeDataGoldenTests.월요일_회전은_옛_산술과_비트까지_같다` | cm | ¦ 'WornShapeDataGoldenTests.월요일_회전은_옛_산술과_비트까지_같 | 도입 7ed996d 2026-09-02 → 소멸 0229f52 2026-09-06; `.cs` 1커밋 (7ed996d) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): v1_조각에도_아이템_세로_오프셋이_걸린다 · AppendFromStreams · IsHandoffItem |
| `docs/verify/QA_REGRESSION_2026-09-05.md:305` | `WornShapeDataGoldenTests.방울_10각형은_옛_산술과_비트까지_같다` | cm | ¦ 'WornShapeDataGoldenTests.방울_10각형은_옛_산술과_비트까지_ | 도입 7ed996d 2026-09-02 → 소멸 0229f52 2026-09-06; `.cs` 1커밋 (7ed996d) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): v1_조각에도_아이템_세로_오프셋이_걸린다 · AppendFromStreams · IsHandoffItem |
| `docs/verify/QA_REGRESSION_2026-09-05.md:306` | `EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다` | cm | ¦ 'EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다' (6 | 도입 767c985 2026-09-01 → 소멸 0229f52 2026-09-06; `.cs` 1커밋 (767c985) | 낮음 |  |
| `docs/verify/QA_REGRESSION_2026-09-05.md:320` | `CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다` | cm | ¦ 'CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다' ¦ '…망 | 도입 767c985 2026-09-01 → 소멸 0229f52 2026-09-06; `.cs` 2커밋 (0229f52, 767c985) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 망토_뒤판의_흔들_구간은_밑단이다 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:321` | `CardShapeContractTests.열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다` | cm | ¦ 'CardShapeContractTests.열세_종은_몸과_카드가_같은_목록이고_망 | 도입 (커밋 안 됨 — 러너 xml에만) → 소멸 개명 대장 → CardShapeContractTests.아이템은_한_벌이거나_두_벌이고_두_벌은_표면과_층이_명시된다; `.cs` 0커밋 | 낮음 | renames.tsv 옛 이름 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:410` | `미해결_하이브리드GPU_선택이_macOS에는…` | ko | ¦ 09-03 01:28 ¦ '…미해결_하이브리드GPU_선택이_macOS에는…' ¦ 착수 미배정 ¦ 자기 대장 ¦ | 도입 1f7e139 2026-09-05 → 소멸 da71068 2026-09-07; `.cs` 1커밋 (1f7e139) | 낮음 | 이력 부분 일치 → 미해결_하이브리드GPU_선택이_macOS에는_배선되지_않았다 |

## D. (다) 있었다가 사라짐 — 미표기 — `Tasklist.md`(날짜별 기록) 35건

| 파일:줄 | 이름 | 형태 | 약속 문장 요지 | `git log -S`·선언 이력 | 심각도 | 비고 |
|---|---|---|---|---|---|---|
| `Tasklist.md:6429` | `v5_파일을_읽어도_구석_패널이_꺼지지_않는다` | ko | 없으면 기본값"이 저절로 성립하지 **않는다** → 버전 분기 + 회귀 테스트 'v5_파일을_읽어도_구석_패널이_꺼지지_않는다' 신설 ¦ | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 0dd904f) | 높음(세이브) | 사전 밖 표기 후보「신설」 |
| `Tasklist.md:7473` | `카드_하단과_다이얼_원환_상단이_정확히_맞닿는다` | ko | Ring Assets/' = 구현 1곳뿐). 같은 라운드의 M2는 잠금 테스트 '카드_하단과_다이얼_원환_상단이_정확히_맞닿는다'를 제대로 받았는데 M1만 빠졌다 | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 0dd904f) | 높음(출시 게이트) | 사전 밖 표기 후보「없는」 |
| `Tasklist.md:26612` | `출하_전량이_착용_비트맵_칸을_비웠다` | ko | ¦ 42종 필드 ¦ **전량 빈 칸**(테스트 '출하_전량이_착용_비트맵_칸을_비웠다'가 잠금 + 같은 실행에서 계수기 생존을 양 | 도입 c227134 2026-09-09 → 소멸 7f41eef 2026-09-09; `.cs` 2커밋 (7f41eef, c227134) | 높음(출시 게이트) |  |
| `Tasklist.md:27168` | `SessionExitMarkerTests.원칙3_원본과_우리_폴더_밖은_한_바이트도_바뀌지_않고_아무것도_지워지지_않는다` | cm | 보고의 "컴파일 에러 없음"은 참이었으나 **테스트는 빨강**: 1. ★ 'SessionExitMarkerTests.원칙3_원본과_우리_폴더_밖은_한_바이트 | 도입 3cc6753 2026-09-14 → 소멸 17f6f38 2026-09-14; `.cs` 2커밋 (17f6f38, 3cc6753) | 높음(원칙) | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 원칙3_우리_폴더_밖_파일의_내용_크기_수정시각_속성이_그대로이고_지워지거나_생기지_않는다 · 원칙3_원본을_읽는_동안_다른_핸들의_읽기_쓰기_이름바꾸기를_막지_않는다 · 기동이_표지를_켜지_않았으면_종료_표지도_쓰지_않는다; 사전 밖 표기 후보「없음」 |
| `Tasklist.md:3612` | `LandingCrouchTests.DockStepDropDoesNotTriggerCrouch` | cm | - 'LandingCrouchTests.DockStepDropDoesNotTrigger | 도입 9ad6279 2026-08-29 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 9ad6279) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): DockStepDropAbsorbsSoftlyWithoutKneeling · ResolveLandingCrouchState; 사전 밖 표기 후보「없다」 |
| `Tasklist.md:4503` | `획_두께를_빼면_천모자_민머리_조합이_실제로_깨진다` | ko | - 네거티브 컨트롤: '획_두께를_빼면_천모자_민머리_조합이_실제로_깨진다' — 획 두께를 0으로 두면 | 도입 b6755f4 2026-08-30 → 소멸 767c985 2026-09-01; `.cs` 2커밋 (767c985, b6755f4) | 낮음 |  |
| `Tasklist.md:5363` | `맨틀_인셋이_버티는_배율_천장을_기록한다` | ko | - '맨틀_인셋이_버티는_배율_천장을_기록한다' — 아래 "정직한 한계" 참고. 실측 | 도입 b6755f4 2026-08-30 → 소멸 0dd904f 2026-08-31; `.cs` 2커밋 (0dd904f, b6755f4) | 낮음 |  |
| `Tasklist.md:5365` | `맨틀_인셋은_경계_판정_거리보다_커야_한다` | ko | ode 'WanderEdgeConfigInvariantTests'** — 기존 '맨틀_인셋은_경계_판정_거리보다_커야_한다'가 **설정값(0.300)과만 비교** | 도입 7128f87 2026-08-29 → 소멸 b6755f4 2026-08-30; `.cs` 2커밋 (b6755f4, 7128f87) | 낮음 | 사전 밖 표기 후보「추가」 |
| `Tasklist.md:6319` | `네거티브컨트롤_유도를_끄면_큰_배율에서_맨틀_인셋이_경계에_먹힌다` | ko | .맨틀_인셋이_모든_배율에서_경계_밴드를_넘어서야_한다'(8배율) + 신규 '네거티브컨트롤_유도를_끄면_큰_배율에서_맨틀_인셋이_경계에_먹힌다' (옛 "천 | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 0dd904f) | 낮음 | 사전 밖 표기 후보「신규」 |
| `Tasklist.md:7240` | `CornerHoverPanelTests` | class | : 신설 2종 + 'InfoWindowExclusiveModalTests' / 'CornerHoverPanelTests' / 'RetinaDpiCoordinate | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 3커밋 (2051739, 767c985, 0dd904f) | 낮음 | 사전 밖 표기 후보「신설」 |
| `Tasklist.md:7342` | `CornerHoverPanelTests` | class | ¦ 'CornerHoverPanelTests' (다이얼 M1/M2 무회귀) ¦ **6 | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 3커밋 (2051739, 767c985, 0dd904f) | 낮음 |  |
| `Tasklist.md:7542` | `SizeDialWidgetHitTestTests` | class | 2. 'SizeDialWidgetHitTestTests.cs' 신설(EditMode 3건 | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 3커밋 (2051739, 767c985, 0dd904f) | 낮음 | 사전 밖 표기 후보「신설」 |
| `Tasklist.md:10892` | `내용물_게이트에서_다이얼이_상자_안에_완전히_들어간다` | ko | - **회귀**: '내용물_게이트에서_다이얼이_상자_안에_완전히_들어간다'(정적 기하), '상자가_다 | 도입 767c985 2026-09-01 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 767c985) | 낮음 |  |
| `Tasklist.md:10892` | `상자가_다_자란_뒤에_다이얼이_나타난다` | ko | **: '내용물_게이트에서_다이얼이_상자_안에_완전히_들어간다'(정적 기하), '상자가_다_자란_뒤에_다이얼이_나타난다' (시간축 PlayMode, 열림/닫힘 | 도입 767c985 2026-09-01 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 767c985) | 낮음 |  |
| `Tasklist.md:11372` | `CornerHoverPanelTests` | class | 'DeployedConfigAssetImmutabilityTests' 1건 · 'CornerHoverPanelTests' 1건(넷 다 "런타임 배율 경로가 죽었다 | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 3커밋 (2051739, 767c985, 0dd904f) | 낮음 |  |
| `Tasklist.md:11457` | `CharacterAppearanceLayerTests.모자를_쓰면_머리가_숨고...` | cm | 건을 자르기 의미로 재작성(+네거티브 컨트롤 2건 추가), PlayMode 'CharacterAppearanceLayerTests.모자를_쓰면_머리가_숨고.. | 도입 b6755f4 2026-08-30 → 소멸 767c985 2026-09-01; `.cs` 2커밋 (767c985, b6755f4) | 낮음 | 이력 일치 → 모자를_쓰면_머리가_숨고_왕관만_예외다; 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 모자를_쓰면_머리카락이_커버선_아래로_잘리고_왕관만_예외다 · _tripAgent · _tripCloneConfig; 사전 밖 표기 후보「추가」 |
| `Tasklist.md:11576` | `AdaptiveFramePacingPolicyTests.오래_무입력이면_자리비움등급이다` | cm | - 기존 테스트 1개 갱신: 'AdaptiveFramePacingPolicyTests.오래_무입력이면_자리비움등 | 도입 c256a58 2026-08-31 → 소멸 767c985 2026-09-01; `.cs` 2커밋 (767c985, c256a58) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 오래_무입력이고_캐릭터도_서있으면_자리비움등급이다 |
| `Tasklist.md:11747` | `CornerHoverPanelTests` | class | DeployedConfigAssetImmutabilityTests' 1건) + 'CornerHoverPanelTests' 1건. - 1건은 **플레이키**: | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 3커밋 (2051739, 767c985, 0dd904f) | 낮음 |  |
| `Tasklist.md:12230` | `CornerHoverPanelTests.상자가_다_자란_뒤에_다이얼이_나타난다` | cm | ¦ C ¦ 'CornerHoverPanelTests.상자가_다_자란_뒤에_다이얼이_나타난다' | 도입 767c985 2026-09-01 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 767c985) | 낮음 |  |
| `Tasklist.md:13235` | `CornerHoverPanelTests` | class | **B**('CharacterScaleRuntimeTests')와 **C**('CornerHoverPanelTests'). | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 3커밋 (2051739, 767c985, 0dd904f) | 낮음 |  |
| `Tasklist.md:13265` | `CornerHoverPanelTests.상자가_다_자란_뒤에_다이얼이_나타난다` | cm | ### C. 'CornerHoverPanelTests.상자가_다_자란_뒤에_다이얼이_나타난다' | 도입 767c985 2026-09-01 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 767c985) | 낮음 |  |
| `Tasklist.md:13278` | `패널_컴포넌트가_...붙어_있다` | ko | ¦ '패널_컴포넌트가_...붙어_있다' ¦ 0 ¦ 1.099720s ¦ | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 0dd904f) | 낮음 | 이력 부분 일치 → 패널_컴포넌트가_캐릭터_프리팹에_실제로_붙어_있다 |
| `Tasklist.md:13279` | `숨어_있는_동안_클릭_차단막이_꺼져_있다` | ko | ¦ '숨어_있는_동안_클릭_차단막이_꺼져_있다' ¦ 120 ¦ 1.113230s ¦ | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 0dd904f) | 낮음 |  |
| `Tasklist.md:13280` | `상자가_다_자란_뒤에_다이얼이_나타난다` | ko | ¦ '상자가_다_자란_뒤에_다이얼이_나타난다' ¦ 180 ¦ 1.140439s ¦ | 도입 767c985 2026-09-01 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 767c985) | 낮음 |  |
| `Tasklist.md:13571` | `백업이_성공했으면_저장은_정상_진행된다` | ko | - 기존 'SaveDowngradeGuardTests': '백업이_성공했으면_저장은_정상_진행된다' → '백업에_성공해도_신버전_원본은_ | 도입 5524506 2026-08-30 → 소멸 767c985 2026-09-01; `.cs` 1커밋 (5524506) | 낮음 |  |
| `Tasklist.md:14120` | `CornerHoverPanelTests.숨어_있는_동안_클릭_차단막이_꺼져_있다` | cm | ¦ 2 ¦ 'CornerHoverPanelTests.숨어_있는_동안_클릭_차단막이_꺼져_있다' | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 2커밋 (2051739, 0dd904f) | 낮음 | 사전 밖 표기 후보「거짓」 |
| `Tasklist.md:14162` | `CornerHoverPanelTests` | class | - 'CornerHoverPanelTests': 120f → **2초**(폴링 40회) | 도입 0dd904f 2026-08-31 → 소멸 2051739 2026-09-02; `.cs` 3커밋 (2051739, 767c985, 0dd904f) | 낮음 |  |
| `Tasklist.md:17094` | `WindowsMsaaDefaultTests.Windows_기본_MSAA는_2x다` | cm | 1. **'WindowsMsaaDefaultTests.Windows_기본_MSAA는_2x다' | 도입 ae6b001 2026-09-01 → 소멸 d84732b 2026-09-01; `.cs` 2커밋 (d84732b, ae6b001) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): Windows_기본_MSAA는_macOS와_같은_4x다; 사전 밖 표기 후보「없다」 |
| `Tasklist.md:17420` | `PlatformParityAuditTests.미해결_Windows에는_가상데스크톱_동행_배선이_없다` | cm | 장해 자기 스캔에 걸렸다. 내 변경과 무관(그 파일을 읽지도 않았다). · 'PlatformParityAuditTests.미해결_Windows에는_가상데스크톱 | 도입 767c985 2026-09-01 → 소멸 0229f52 2026-09-06; `.cs` 3커밋 (0229f52, 512bec2, 767c985) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 결정_하단_예약띠는_Windows에서만_강제한다 · 결정_가상데스크톱은_Windows에서_따라붙지_않고_스스로_숨는다; 사전 밖 표기 후보「없다」 |
| `Tasklist.md:20169` | `LandingCrouchTests.DockStepDropAbsorbsSoftlyWithoutKneeling` | cm | 'LandingCrouchTests.DockStepDropAbsorbsSoftlyW | 도입 2051739 2026-09-02 → 소멸 a1c64b5 2026-09-02; `.cs` 2커밋 (a1c64b5, 2051739) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): DockStepDropNeverLooksLikeKneelingAtAnySelectableScale · _restoreScale · ApplyScaleAndRecapture |
| `Tasklist.md:20525` | `미해결_Windows에는_상단_예약띠_조회가_없다` | ko | '미해결_Windows에는_상단_예약띠_조회가_없다' → **정식 검사로 승격**( | 도입 a1c64b5 2026-09-02 → 소멸 512bec2 2026-09-02; `.cs` 2커밋 (512bec2, a1c64b5) | 낮음 | 사전 밖 표기 후보「없다」 |
| `Tasklist.md:20528` | `미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다` | ko | 아직 판단이 안 난 하단 문제**를 새 'Assert.Ignore'로 넣었다 ('미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다') — ★ ** | 도입 a1c64b5 2026-09-02 → 소멸 0229f52 2026-09-06; `.cs` 1커밋 (a1c64b5) | 낮음 | 사전 밖 표기 후보「없다」 |
| `Tasklist.md:20749` | `AccessoryBeaniePomTests.모자_6종_실루엣_차이가_2_95획_아래로_내려가지_않는다` | cm | 1. **'AccessoryBeaniePomTests.모자_6종_실루엣_차이가_2_95획_아 | 도입 767c985 2026-09-01 → 소멸 7ab0468 2026-09-02; `.cs` 1커밋 (767c985) | 낮음 | 소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): 모자_6종_실루엣_차이가_직전_실측_아래로_내려가지_않는다 |
| `Tasklist.md:22789` | `아직_미완_커서친구는_머리와_꼬리로_안_쪼개졌다` | ko | 그 예산 검사는 계속 꺼져 있다. 명부상 'Kind=동반'이고 동반 테스트가 '아직_미완_커서친구는_머리와_꼬리로_안_쪼개졌다'다 — **조형이 안 쪼개져서 꺼 | 도입 2051739 2026-09-02 → 소멸 912fd8c 2026-09-06; `.cs` 3커밋 (912fd8c, 7ed996d, 2051739) | 낮음 |  |
| `Tasklist.md:24560` | `FocusRingPoseSyncTests.cs` | path | medSpectacleState.cs'('Progress01' 노출) + 신규 'Tests/PlayMode/FocusRingPoseSyncTests.cs'(3건) | 도입 349048f 2026-09-06 → 소멸 912fd8c 2026-09-06; `.cs` 2커밋 (912fd8c, 349048f) | 낮음 | 사전 밖 표기 후보「신규」 |

## E. 거짓 서술 후보 — (다) 미표기 · 기록 문서 제외 40건

문서가 사라진 테스트를 표기 없이 이름으로 인용한다. 과거형 기록일 수 있어 **후보**다 — 문장을 읽고 판정한다(사람 판정은 이 문서 앞머리).

| 파일:줄 | 이름 | 심각도 | blame(줄 작성 시점 vs 소멸) | 요지 |
|---|---|---|---|---|
| `docs/marketing/CLAIM_AUDIT_R5.md:93` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | 높음 | 소멸(eb4670d) 전에 쓴 줄 1f7e139 — 방치 | sts/PlayMode/FullscreenPanelRetreatTests' — '등급1에서_톱니를_누르면_설정창까지_도달한다()' ¦ |
| `docs/marketing/CLAIM_AUDIT_R5.md:116` | `FullscreenPanelRetreatTests.미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | 높음 | 소멸(eb4670d) 뒤에 쓰거나 고친 줄 5912e33 | 창을 열면 **열자마자 닫힌다**. 저장소가 스스로 갭이라고 선언해 두었다 ¦ 'FullscreenPanelRetreatTests.미해결_등급1에서_정보창_단축키 |
| `docs/marketing/TRUTH_INVENTORY.md:98` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | 높음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | - R5-1 근거 'FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 |
| `docs/marketing/TRUTH_INVENTORY.md:1005` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | 높음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | faceSummonPolicy'(임대 0.5초) + 'Tests/PlayMode/FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 |
| `docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md:400` | `전송계열_화이트리스트는_현재_비어_있다` | 높음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | ### 7-6. ★ '전송계열_화이트리스트는_현재_비어_있다'를 **니들 종류를 바꿔서 초록으로 만들지 |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:552` | `F18_전체화면_억제_중에는_어떤_조합도_열지_않는다` | 높음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | - EditMode 'F18_전체화면_억제_중에는_어떤_조합도_열지_않는다'('RightClickFan |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:408` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 0229f52 — 방치 | lscreenPanelRetreatTests.cs' ¦ 6 ¦ 등급 1 탈출구('등급1에서_톱니를_누르면_설정창까지_도달한다') ¦ |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:502` | `FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 0229f52 — 방치 | - 'FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다 |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:502` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 0229f52 — 방치 | creenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다' / '등급1에서_톱니를_누르면_설정창까지_도달한다' → ★ **실패한다.** 등급 1에 |
| `docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md:66` | `착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다` | 낮음 | 소멸(a1294a9) 전에 쓴 줄 0229f52 — 방치 | - 테스트 2 '착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다': 최대 세기 |
| `docs/EQUIPMENT_SHAPE_SPEC.md:291` | `EyesVisorOpacityTests.채움이_눈_자리를_덮는다` | 낮음 | 소멸(2051739) 뒤에 쓰거나 고친 줄 2051739 | 'EyesVisorOpacityTests.채움이_눈_자리를_덮는다'는 외알/안대에 |
| `docs/GAME_ARCHITECTURE_REVIEW.md:2041` | `등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 1eb0e2b — 방치 | ★ **실무상 위험**: 누군가 B를 고치면 '등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다'가 **빨개진다.** |
| `docs/GAME_ARCHITECTURE_REVIEW.md:2740` | `미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | 코드는 여전히 0줄이다.** 'PlatformParityAuditTests'의 '미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다'가 'Assert.Ign |
| `docs/MOTION_SPEC.md:1966` | `LandingCrouchTests.DockStepDropAbsorbsSoftlyWithoutKneeling` | 낮음 | 소멸(a1c64b5) 뒤에 쓰거나 고친 줄 a1c64b5 | 1. 'LandingCrouchTests.DockStepDropAbsorbsSoftlyW |
| `docs/UX_CHARACTER_WINDOW_REFINE.md:1694` | `CharacterStatColumnLayoutTests.설계_크기에서_컬럼2가_본문_높이_안에_들어간다` | 낮음 | 소멸(6156c73) 뒤에 쓰거나 고친 줄 6156c73 | 2. 'Tests/EditMode/CharacterStatColumnLayoutTests.설계_크기에서_컬럼2가_본 |
| `docs/UX_FLOW.md:6524` | `EyesVisorOpacityTests.채움이_눈_자리를_덮는다` | 낮음 | 소멸(2051739) 뒤에 쓰거나 고친 줄 2051739 | ¦ 'EyesVisorOpacityTests.채움이_눈_자리를_덮는다' ¦ **수정 없 |
| `docs/UX_FLOW.md:9080` | `ComicFontFloorOutlineRingTests.배율1에서는_두_요구가_양립하지_않는다_보류` | 낮음 | 소멸(89de9de) 전에 쓴 줄 512bec2 — 방치 | **★ 미해결 갭의 재작성** — 'ComicFontFloorOutlineRingTests.배율1에서는_두_요구가_양 |
| `docs/manual/03-disappeared.md:321` | `미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 1f7e139 — 방치 | yMode/FullscreenPanelRetreatTests 미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다 (Assert. |
| `docs/manual/03-disappeared.md:352` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | 없음, InfoGearIconWidget.cs:933). · 러너 FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 |
| `docs/strategy/CHANNEL_PRICING_DECISIONS.md:1678` | `전송계열_화이트리스트는_현재_비어_있다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | 그리고 **전송계열 화이트리스트는 0건으로 못박혀 있다** — 같은 파일의 '전송계열_화이트리스트는_현재_비어_있다()' 테스트가 그것을 잠근다. |
| `docs/strategy/ROADMAP.md:3738` | `등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 0229f52 — 방치 | ¦ 298 ¦ '등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다()' ¦ ★★ **등 |
| `docs/strategy/ROADMAP.md:3740` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 0229f52 — 방치 | ¦ 555 ¦ '등급1에서_톱니를_누르면_설정창까지_도달한다()' ¦ **불변식 R1-I** — |
| `docs/strategy/ROADMAP.md:3741` | `미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 0229f52 — 방치 | ¦ 714 ¦ '미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다()' ¦ ★ ** |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:557` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | - 대상: - 'FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:558` | `등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | 등급 1에서 톱니 가시성을 **「탈출구의 첫 홉」**으로 단언한다 - 형제 '등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다' |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:782` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | ¦ T-3 ¦ **거짓 통과 처리**. '등급1에서_톱니를_누르면_설정창까지_도달한다'와 '등급1은_…톱니는_남긴다'의 전 |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:782` | `등급1은_…톱니는_남긴다` | 낮음 | 소멸(eb4670d) 전에 쓴 줄 6173b6e — 방치 | ¦ **거짓 통과 처리**. '등급1에서_톱니를_누르면_설정창까지_도달한다'와 '등급1은_…톱니는_남긴다'의 전제를 **「톱니가 보이는 세계(사용자 숨김·가출)」 |
| `docs/verify/BASELINE.md:43` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | mallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니 |
| `docs/verify/BASELINE.md:50` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 ¦ |
| `docs/verify/BASELINE.md:54` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 ¦ |
| `docs/verify/BASELINE.md:56` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 ¦ |
| `docs/verify/BASELINE.md:58` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | ndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>설정창_톱니_위치_행은_옮 |
| `docs/verify/BASELINE.md:60` | `사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다` | 낮음 | 소멸(1f7e139) 뒤에 쓰거나 고친 줄 1f7e139 | mallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니 |
| `docs/verify/BASELINE.md:95` | `대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다` | 낮음 | 소멸(a1294a9) 전에 쓴 줄 1f7e139 — 방치 | 0e2b ¦ ? ¦ **~WIN** ¦ 1914 ¦ 1894 ¦ 2 ¦ 18 ¦ 대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다<br>상태_테두 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:304` | `WornShapeDataGoldenTests.월요일_회전은_옛_산술과_비트까지_같다` | 낮음 | 소멸(0229f52) 뒤에 쓰거나 고친 줄 0229f52 | ¦ 'WornShapeDataGoldenTests.월요일_회전은_옛_산술과_비트까지_같 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:305` | `WornShapeDataGoldenTests.방울_10각형은_옛_산술과_비트까지_같다` | 낮음 | 소멸(0229f52) 뒤에 쓰거나 고친 줄 0229f52 | ¦ 'WornShapeDataGoldenTests.방울_10각형은_옛_산술과_비트까지_ |
| `docs/verify/QA_REGRESSION_2026-09-05.md:306` | `EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다` | 낮음 | 소멸(0229f52) 뒤에 쓰거나 고친 줄 0229f52 | ¦ 'EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다' (6 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:320` | `CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다` | 낮음 | 소멸(0229f52) 뒤에 쓰거나 고친 줄 0229f52 | ¦ 'CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다' ¦ '…망 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:321` | `CardShapeContractTests.열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다` | 낮음 | 줄 0229f52 · 소멸 커밋 불명 | ¦ 'CardShapeContractTests.열세_종은_몸과_카드가_같은_목록이고_망 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:410` | `미해결_하이브리드GPU_선택이_macOS에는…` | 낮음 | 소멸(da71068) 전에 쓴 줄 0229f52 — 방치 | ¦ 09-03 01:28 ¦ '…미해결_하이브리드GPU_선택이_macOS에는…' ¦ 착수 미배정 ¦ 자기 대장 ¦ |

## F. 사람 확인 목록 — 약한 표기 (라) · 사전 밖 표기 후보 (나)(다) 86건

① **약한 표기**: (라)로 셌지만 표기가 이름과 **다른 문장·표 셀**에 있다(V2는 하드 규칙이 아니라 목록으로 둔다 — verify-change 실측 뒤집힘 26 중 옳음 12·틀림 14). 읽고 그 표기가 이 이름의 부재를 말하지 않으면 실제로는 (나)(다)다. ② **사전 밖 후보**: 사전 표기가 아니라 (라)로 세지 않았다. 「스스로 부재를 밝힌 문장」이면 사전에 넣을 후보다.

| 파일:줄 | 이름 | 분류 | 낱말 | 요지 |
|---|---|---|---|---|
| `Tasklist.md:4854` | `기록_여섯_값이…` | 라(약) · 이름 (다) | 표기「삭제」 | RivalDuel'만 임시 복구)에서 실행했다. **EditMode 1건 실패('기록_여섯_값이…' "대결 승리 횟수가 복원되지 않았습니다")는 그 임시 복구의 |
| `Tasklist.md:5587` | `ZZDebuggerOverlapDiagnostic.cs` | 라(약) · 이름 (나) | 표기「삭제」 | 관측 도구: 임시 진단 하네스 'Assets/_Project/Scripts/Tests/PlayMode/ZZDebu |
| `Tasklist.md:5957` | `맨틀_인셋이_버티는_배율_천장을_기록한다` | 라(약) · 이름 (다) | 표기「삭제」 | / '설정_절대값_단독으로는_벽_이격을_못_덮는다는_사실을_기록한다' / '맨틀_인셋이_버티는_배율_천장을_기록한다' + 'WanderEdgeConfig |
| `Tasklist.md:6409` | `ZZCornerPanelCaptureHarness.cs` | 라(약) · 이름 (나) | 표기「삭제」 | 같은 캔버스를 같은 코드로 만든 뒤** 카메라로 찍는 PlayMode 하네스 ('Tests/PlayMode/ZZCornerPanelCaptureHarness.cs |
| `Tasklist.md:6922` | `CornerHoverPanelTests` | 라(약) · 이름 (다) | 표기「삭제」 | 문서화, 신규 잠금 테스트 '카드_하단과_다이얼_원환_상단이_정확히_맞닿는다'(CornerHoverPanelTests.cs)로 등식 (392−12−212 == |
| `Tasklist.md:6922` | `카드_하단과_다이얼_원환_상단이_정확히_맞닿는다` | 라(약) · 이름 (다) | 표기「삭제」 | Widget)를 public으로 노출하고 상호 참조 문서화, 신규 잠금 테스트 '카드_하단과_다이얼_원환_상단이_정확히_맞닿는다'(CornerHoverPanelT |
| `Tasklist.md:6923` | `CornerHoverPanelTests` | 라(약) · 이름 (다) | 표기「삭제」 | 12 == 78+90)을 실행으로 고정. 직접 실행 검증: 신규 테스트 통과, 'CornerHoverPanelTests' 전체 6/6 무회귀. (주의: 초안에 C |
| `Tasklist.md:8382` | `ZZExploratorySweepTests` | 라(약) · 이름 (나) | 표기「삭제」 | 'ZZExploratorySweepTests'/'3Tests'가 'TodoListM |
| `Tasklist.md:8386` | `ZZExploratorySweep5Tests` | 라(약) · 이름 (나) | 표기「삭제」 | 로(임시 격리 폴더) 그것도 함께 삭제됐다. 남은 하네스는 잔존 렌더러 식별용 'ZZExploratorySweep5Tests' 하나뿐이며 이것도 보고 직후 삭제 |
| `Tasklist.md:13939` | `CornerHoverPanelTests` | 라(약) · 이름 (다) | 표기「예정」 | 1. **"프레임 수 = 시간" 함정, 오늘 세 번째 발생.** 'CornerHoverPanelTests'(180프레임=0.014~0.082초), |
| `Tasklist.md:15626` | `PlatformParityAuditTests.미해결_Windows에는_가상데스크톱_동행_배선이_없다` | 라(약) · 이름 (다) | 표기「사라졌」 | der_final_edit_targeted.xml'). 건너뜀 1건은 'PlatformParityAuditTests.미해결_Windows에는_가상데스크톱 |
| `Tasklist.md:25481` | `FocusRingPoseSyncTests` | 라(약) · 이름 (다) | 표기「사라짐」 | 지후 재배정 필요**(FocusWatchStancePersistenceTests/FocusRingPoseSyncTests/Phase5VisualLayerTests |
| `Tasklist.md:27727` | `WallClockReadScopeAuditTests` | 라(약) · 이름 (나) | 표기「폐기」 | 로 0. 범위 밖 발견: 'SECURITY_MODEL.md' T-11이 명세한 'WallClockReadScopeAuditTests'가 저장소에 없음, T-1 표 |
| `Tasklist.md:27748` | `WallClockReadScopeAuditTests` | 라(약) · 이름 (나) | 표기「명세만」 | macOS 영향: 문서만. **리더 판정**: 수용. ① **T-11-a 'WallClockReadScopeAuditTests' 구현 = test-engin |
| `docs/EQUIPMENT_SHAPE_SPEC.md:79` | `EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다` | 라(약) · 이름 (다) | 표기「정정」 | _FLOW.md' 37-6 규칙 5 원문은 > **"합계 2~4개"** 이고, 'EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다' / > |
| `docs/GAME_ARCHITECTURE_REVIEW.md:2747` | `AudioActivationPolicyTests.cs` | 라(약) · 이름 (다) | 표기「삭제」 | ¦ 'Tests/EditMode/AudioActivationPolicyTests.cs' |
| `docs/PLATFORM_RIGHTCLICK_FAN.md:556` | `미해결_Windows_보조버튼_조회가_물리버튼이라_반전설정을_따르지_않는다` | 라(약) · 이름 (나) | 표기「개명」 | ¦ **A-1** ¦ '미해결_Windows_보조버튼_조회가_물리버튼이라_반전설정을_따르지_않는다' ¦ |
| `docs/UX_RIGHTCLICK_FAN_MENU.md:1343` | `RightClickFanGateTests.F18_전체화면_억제_중에는_어떤_조합도_열지_않는다` | 라(약) · 이름 (다) | 표기「계획」 | d:153'·':230'이 같은 식을 옮겨 F18을 정의했다. EditMode 'RightClickFanGateTests.F18_전체화면_억제_중에는_어떤_조합도 |
| `docs/inspection/R1_거짓주석_수정배정.md:68` | `VisibleTopEdgeSolverTests` | 라(약) · 이름 (나) | 표기「사라진」 | 46%)을 놓쳤고**, 놓친 것 안에 이 라운드가 손으로 찾은 진짜 위반('...VisibleTopEdgeSolverTests.cs<b>가</b> 그 실측이다') |
| `docs/manual/01-where-to-click.md:56` | `PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다` | 라(약) · 이름 (나) | 표기「사라진」 | . · 남은 갭은 저장소가 **스스로 선언**해 뒀다: PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼 |
| `docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md:77` | `미해결_하이브리드GPU_선택이_macOS에는_배선되지_않았다` | 라(약) · 이름 (다) | 표기「옛 인용」 | 커밋은 0개다.** '1f7e139' 기준 실제 위치는 ':3752' (메서드 '미해결_하이브리드GPU_선택이_macOS에는_배선되지_않았다')이고, 원문은 *" |
| `docs/verify/BASELINE.md:26` | `실제_베레모_폴백_테는_이제_몸과_같은_점수다` | 라(약) · 이름 (다) | 표기「사라졌」 | ¦ '실제_베레모_폴백_테는_이제_몸과_같은_점수다' ¦ '실제_베레모_폴백_테는_몸_ |
| `docs/verify/BASELINE.md:27` | `T2_실제_프리팹의_11개_선이_같은_규칙을_따른다` | 라(약) · 이름 (다) | 표기「정정」 | ¦ 'T2_실제_프리팹의_11개_선이_같은_규칙을_따른다' ¦ 'T2_실제_리그의_몸선 |
| `docs/verify/BASELINE.md:28` | `T2_실제_프리팹의_본체_선이_같은_규칙을_따른다` | 라(약) · 이름 (다) | 표기「개명」 | ¦ 'T2_실제_프리팹의_본체_선이_같은_규칙을_따른다' ¦ 'T2_실제_리그의_몸선과 |
| `docs/verify/BASELINE.md:29` | `스토어_SDK가_들어오면_이_경보가_먼저_울린다` | 라(약) · 이름 (다) | 표기「개명」 | ¦ '스토어_SDK가_들어오면_이_경보가_먼저_울린다' ¦ '스토어_SDK는_승인된_어 |
| `docs/verify/BASELINE.md:30` | `망토_윤곽선은_밑단_5점을_흔든다` | 라(약) · 이름 (다) | 표기「개명」 | ¦ '망토_윤곽선은_밑단_5점을_흔든다' ¦ '망토_뒤판의_흔들_구간은_밑단이다' ¦ |
| `docs/verify/BASELINE.md:31` | `열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다` | 라(약) · 이름 (다) | 표기「개명」 | ¦ '열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다' ¦ '아이템은_한_벌이거 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:307` | `CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다` | 라(약) · 이름 (다) | 표기「개명」 | ¦ 'CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다' ¦ Pas |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:129` | `고정_이름의_토큰_없는_v2는_갚지_않는다` | 라(약) · 이름 (나) | 표기「부재」 | ¦ TK-23 ¦ '고정_이름의_토큰_없는_v2는_갚지_않는다' ¦ E / T (매개 2) ¦ 'Fx |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:176` | `토큰이_지워진_뒤_해제하면_새_토큰과_새_파일이_생기고_옛_닫힌_파일은_남는다` | 라(약) · 이름 (나) | 표기「삭제」 | ¦ 키 삭제 ¦ 닫힘 ¦ **TK-70** '토큰이_지워진_뒤_해제하면_새_토큰과_새_파일이_생기고_옛_닫힌_파일은_남는다' |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:177` | `토큰이_지워지고_열린_흔적이_남으면_갚지_않고_보존한다` | 라(약) · 이름 (나) | 표기「삭제」 | ¦ 키 삭제 ¦ 열림 ¦ **TK-71** '토큰이_지워지고_열린_흔적이_남으면_갚지_않고_보존한다' — 'Own(T)o' + |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:253` | `읽을_수_없는_흔적_경고는_실제로_한_일과_어긋나지_않는다` | 라(약) · 이름 (나) | 표기「정정」 | ¦ TK-81나 ¦ '읽을_수_없는_흔적_경고는_실제로_한_일과_어긋나지_않는다' ¦ **(나)만의 합 |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:258` | `쉬기를_택하면_같은_흔적에서_다음_실행도_계속_쉰다_비용_박제` | 라(약) · 이름 (나) | 표기「삭제」 | ¦ TK-86 ¦ '쉬기를_택하면_같은_흔적에서_다음_실행도_계속_쉰다_비용_박제' ¦ (가)·(다) |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:268` | `토큰_키를_쓰는_프로덕션_코드는_토큰_저장소_파일_한_곳뿐이다` | 라(약) · 이름 (나) | 표기「부재」 | ¦ A-1 ¦ '토큰_키를_쓰는_프로덕션_코드는_토큰_저장소_파일_한_곳뿐이다' ¦ 존재 1 + |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:269` | `프로덕션과_테스트_어디에도_PlayerPrefs_DeleteAll이_없다` | 라(약) · 이름 (나) | 표기「부재」 | ¦ A-2 ¦ '프로덕션과_테스트_어디에도_PlayerPrefs_DeleteAll이_없다' ¦ 부 |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:270` | `macOS_경로에서_토큰_확보_호출이_없다` | 라(약) · 이름 (나) | 표기「부재」 | ¦ A-3 ¦ 'macOS_경로에서_토큰_확보_호출이_없다' ¦ 부재 + 구조 ¦ (a) 실행: |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:272` | `토큰_테스트는_실제_PlayerPrefs를_부르지_않는다` | 라(약) · 이름 (나) | 표기「부재」 | ¦ A-5 ¦ '토큰_테스트는_실제_PlayerPrefs를_부르지_않는다' ¦ 부재 ¦ 'Test |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:274` | `흔적_원자화는_삭제_이동_금지_감사_안에_머문다` | 라(약) · 이름 (나) | 표기「부재」 | ¦ A-7 ¦ '흔적_원자화는_삭제_이동_금지_감사_안에_머문다' ¦ 부재 + 한정 ¦ (설계 ( |
| `Tasklist.md:3612` | `LandingCrouchTests.DockStepDropDoesNotTriggerCrouch` | 다 | 「없다」 | - 'LandingCrouchTests.DockStepDropDoesNotTrigger |
| `Tasklist.md:5365` | `맨틀_인셋은_경계_판정_거리보다_커야_한다` | 다 | 「추가」 | ode 'WanderEdgeConfigInvariantTests'** — 기존 '맨틀_인셋은_경계_판정_거리보다_커야_한다'가 **설정값(0.300)과만 비교** |
| `Tasklist.md:6319` | `네거티브컨트롤_유도를_끄면_큰_배율에서_맨틀_인셋이_경계에_먹힌다` | 다 | 「신규」 | .맨틀_인셋이_모든_배율에서_경계_밴드를_넘어서야_한다'(8배율) + 신규 '네거티브컨트롤_유도를_끄면_큰_배율에서_맨틀_인셋이_경계에_먹힌다' (옛 "천 |
| `Tasklist.md:6429` | `v5_파일을_읽어도_구석_패널이_꺼지지_않는다` | 다 | 「신설」 | 없으면 기본값"이 저절로 성립하지 **않는다** → 버전 분기 + 회귀 테스트 'v5_파일을_읽어도_구석_패널이_꺼지지_않는다' 신설 ¦ |
| `Tasklist.md:6719` | `ZZCornerPanelCaptureHarness.cs` | 나 |  먼 표기「삭제」449자 | 령('-batchmode -nographics -runTests')으로 돌리면 'Tests/PlayMode/ZZCornerPanelCaptureHarness.cs |
| `Tasklist.md:7240` | `CornerHoverPanelTests` | 다 | 「신설」 | : 신설 2종 + 'InfoWindowExclusiveModalTests' / 'CornerHoverPanelTests' / 'RetinaDpiCoordinate |
| `Tasklist.md:7473` | `카드_하단과_다이얼_원환_상단이_정확히_맞닿는다` | 다 | 「없는」 | Ring Assets/' = 구현 1곳뿐). 같은 라운드의 M2는 잠금 테스트 '카드_하단과_다이얼_원환_상단이_정확히_맞닿는다'를 제대로 받았는데 M1만 빠졌다 |
| `Tasklist.md:7542` | `SizeDialWidgetHitTestTests` | 다 | 「신설」 | 2. 'SizeDialWidgetHitTestTests.cs' 신설(EditMode 3건 |
| `Tasklist.md:9663` | `VisibleTopEdgeSolverTests.cs` | 나 | 「없다」 | - 'VisibleTopEdgeSolver.cs' 클래스 문서가 'Tests/EditMode/VisibleTopEdgeSolverTests.cs'를 |
| `Tasklist.md:11457` | `CharacterAppearanceLayerTests.모자를_쓰면_머리가_숨고...` | 다 | 「추가」 | 건을 자르기 의미로 재작성(+네거티브 컨트롤 2건 추가), PlayMode 'CharacterAppearanceLayerTests.모자를_쓰면_머리가_숨고.. |
| `Tasklist.md:13181` | `AccessoryStrokeBudgetTests.머리_4종이_서로_구분된다` | 나 | 「쓴다」 | 3. **기존 검사 확장** — 'AccessoryStrokeBudgetTests.머리_4종이_서로_구분된다' → |
| `Tasklist.md:14120` | `CornerHoverPanelTests.숨어_있는_동안_클릭_차단막이_꺼져_있다` | 다 | 「거짓」 | ¦ 2 ¦ 'CornerHoverPanelTests.숨어_있는_동안_클릭_차단막이_꺼져_있다' |
| `Tasklist.md:15056` | `WindowsFullscreenGamePolicyTests.Windows_구현에는_레지스트리_쓰기_API가_한_건도_없다` | 나 | 「없다」 | 리더 확인 요청**: 레지스트리 쓰기 API 금지 스캔을 지금은 새 테스트 'WindowsFullscreenGamePolicyTests.Windows_구현에는 |
| `Tasklist.md:17094` | `WindowsMsaaDefaultTests.Windows_기본_MSAA는_2x다` | 다 | 「없다」 | 1. **'WindowsMsaaDefaultTests.Windows_기본_MSAA는_2x다' |
| `Tasklist.md:17420` | `PlatformParityAuditTests.미해결_Windows에는_가상데스크톱_동행_배선이_없다` | 다 | 「없다」 | 장해 자기 스캔에 걸렸다. 내 변경과 무관(그 파일을 읽지도 않았다). · 'PlatformParityAuditTests.미해결_Windows에는_가상데스크톱 |
| `Tasklist.md:20525` | `미해결_Windows에는_상단_예약띠_조회가_없다` | 다 | 「없다」 | '미해결_Windows에는_상단_예약띠_조회가_없다' → **정식 검사로 승격**( |
| `Tasklist.md:20528` | `미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다` | 다 | 「없다」 | 아직 판단이 안 난 하단 문제**를 새 'Assert.Ignore'로 넣었다 ('미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다') — ★ ** |
| `Tasklist.md:22043` | `미해결_Windows_알파필터가_WS_EX_TRANSPARENT_단독창을_놓친다` | 나 | 「추가」 | - 지시: '미해결_Windows_알파필터가_WS_EX_TRANSPARENT_단독창을_놓친다' |
| `Tasklist.md:24560` | `FocusRingPoseSyncTests.cs` | 다 | 「신규」 | medSpectacleState.cs'('Progress01' 노출) + 신규 'Tests/PlayMode/FocusRingPoseSyncTests.cs'(3건) |
| `Tasklist.md:24863` | `DockLandingSilhouetteTests` | 나 | 「필요」 | trokeParityTests(주석 경로 오타, 편집권한 있는 라운드 필요) / DockLandingSilhouetteTests(PlayMode 리그 필요). |
| `Tasklist.md:25339` | `축소폴백_원도_같이_줄어든다` | 나 | 「거짓」 | rMenuShrinkFallbackRenderTests(동반, Companion=축소폴백_원도_같이_줄어든다 검사·2/2 Passed 확인) 2건 등재. 검증: |
| `Tasklist.md:26337` | `CostumeAccrualGridTests` | 나 | 「필요」 | . **P1 통과조건 4항에는 없으나 게이트의 실제 입구**라 배정 권고. / 'CostumeAccrualGridTests'(3종료 경로 격자 동일성)는 'Foc |
| `Tasklist.md:27168` | `SessionExitMarkerTests.원칙3_원본과_우리_폴더_밖은_한_바이트도_바뀌지_않고_아무것도_지워지지_않는다` | 다 | 「없음」 | 보고의 "컴파일 에러 없음"은 참이었으나 **테스트는 빨강**: 1. ★ 'SessionExitMarkerTests.원칙3_원본과_우리_폴더_밖은_한_바이트 |
| `Tasklist.md:27621` | `VcE12ProbeTests` | 나 |  먼 표기「정정」732자 | '로)가 EditMode 전량 + PlayMode 전량 787을 통과하고 탐침('VcE12ProbeTests')에만 빨강 → 생존. F18e 명부는 7개 파일 중 |
| `Tasklist.md:27747` | `IdleWindowRatchetTests` | 나 |  먼 표기「존재한 적 없」1084자 | 1 #2·T-14-9 #3은 '349048f' 실재 표시, T-15-7 (B) 'IdleWindowRatchetTests'는 그 이름으론 없으나 'Currency |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:502` | `FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다` | 다 | 「신규」 | - 'FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다 |
| `docs/CODER_UI_GEAR_REMOVAL_MAP.md:502` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | 다 | 「신규」 | creenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다' / '등급1에서_톱니를_누르면_설정창까지_도달한다' → ★ **실패한다.** 등급 1에 |
| `docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md:366` | `CostumeAccrualGridTests` | 나 | 「만들」 | **남는 테스트 1건**: 'Tests/EditMode/CostumeAccrualGridTests' — 세 종료 경로(M-21)에서 누적 |
| `docs/GAME_ARCHITECTURE_REVIEW.md:2740` | `미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다` | 다 | 「없다」 | 코드는 여전히 0줄이다.** 'PlatformParityAuditTests'의 '미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다'가 'Assert.Ign |
| `docs/UX_FLOW.md:6524` | `EyesVisorOpacityTests.채움이_눈_자리를_덮는다` | 다 | 「권고」 | ¦ 'EyesVisorOpacityTests.채움이_눈_자리를_덮는다' ¦ **수정 없 |
| `docs/UX_FLOW.md:9080` | `ComicFontFloorOutlineRingTests.배율1에서는_두_요구가_양립하지_않는다_보류` | 다 | 「필요」 | **★ 미해결 갭의 재작성** — 'ComicFontFloorOutlineRingTests.배율1에서는_두_요구가_양 |
| `docs/inspection/R1_거짓주석_수정배정.md:19` | `VisibleTopEdgeSolverTests.cs` | 나 | 「없음」 | a ¦ 'Platform/VisibleTopEdgeSolver.cs:25' ¦ 'Tests/EditMode/VisibleTopEdgeSolverTests.cs'가 |
| `docs/localization/PLAN_1.0.md:481` | `LocalizationDebtCeilingAuditTests.cs` | 나 | 「신규」 | ### 4-2. 설계 — 'Tests/EditMode/LocalizationDebtCeilingAuditTe |
| `docs/manual/03-disappeared.md:321` | `미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | 다 |  먼 표기「정정」424자 | yMode/FullscreenPanelRetreatTests 미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다 (Assert. |
| `docs/manual/03-disappeared.md:352` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | 다 | 「없다」 먼 표기「옛 인용」1232자 | 없음, InfoGearIconWidget.cs:933). · 러너 FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 |
| `docs/manual/03-disappeared.md:608` | `PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다` | 나 | 「없다」 먼 표기「사라진」388자 | 실을 **실행 가능한 형태로** 박아 두었고, PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼 |
| `docs/marketing/CLAIM_AUDIT_R5.md:116` | `FullscreenPanelRetreatTests.미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다` | 다 | 「없다」 | 창을 열면 **열자마자 닫힌다**. 저장소가 스스로 갭이라고 선언해 두었다 ¦ 'FullscreenPanelRetreatTests.미해결_등급1에서_정보창_단축키 |
| `docs/marketing/TRUTH_INVENTORY.md:1005` | `FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다` | 다 | 「거짓」 | faceSummonPolicy'(임대 0.5초) + 'Tests/PlayMode/FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창 |
| `docs/security/SECURITY_MODEL.md:1682` | `IdleWindowRatchetTests` | 나 | 「신규」 | ### (B) ★ 신규 'IdleWindowRatchetTests' — **순수 함수 동작 + 네거티브 컨 |
| `docs/security/SECURITY_MODEL.md:1693` | `창_480분이면_하루_5760에서_잘린다` | 나 | 「가정」 | econds) // T-15-1-b의 순수 함수를 그대로 호출 [Test] 창_480분이면_하루_5760에서_잘린다() => Assert.AreEqu |
| `docs/security/SECURITY_MODEL.md:1696` | `네거티브컨트롤_창을_없애면_실제로_17280까지_샌다` | 나 | 「가정」 | nds)); // ★ 상수 참조, 숫자 안 베낌 [Test] ★네거티브컨트롤_창을_없애면_실제로_17280까지_샌다() => Assert |
| `docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md:400` | `전송계열_화이트리스트는_현재_비어_있다` | 다 | 「만들」 | ### 7-6. ★ '전송계열_화이트리스트는_현재_비어_있다'를 **니들 종류를 바꿔서 초록으로 만들지 |
| `docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md:426` | `다른_asmdef는_스팀을_언급하지_않는다` | 나 | 「승인 후」 | 로 시작)가 하나라도 있으면 실패 + "이름 형태로 저장하라" 안내 [Test] 다른_asmdef는_스팀을_언급하지_않는다() Assets 아래 asm |
| `docs/strategy/ROADMAP.md:4844` | `잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다` | 나 | 「없는」 | 설치**가 늘어난다. 이것이 레거시 분기가 영구히 떠안는 잔여위험(TK-07 '잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다')의 모수다. v1이 |
| `docs/systems/TASKBAR_LOGOFF_ORDERING.md:164` | `세션_종료_경로도_토큰_파생_흔적을_닫는다` | 나 | 「명세」 | strategy/ROADMAP.md:4724'). 토큰 명세 **TK-41** '세션_종료_경로도_토큰_파생_흔적을_닫는다'('RESERVED_BAR_OWNER_ |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:782` | `등급1에서_톱니를_누르면_설정창까지_도달한다` | 다 | 「거짓」 | ¦ T-3 ¦ **거짓 통과 처리**. '등급1에서_톱니를_누르면_설정창까지_도달한다'와 '등급1은_…톱니는_남긴다'의 전 |
| `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md:782` | `등급1은_…톱니는_남긴다` | 다 | 「거짓」 | ¦ **거짓 통과 처리**. '등급1에서_톱니를_누르면_설정창까지_도달한다'와 '등급1은_…톱니는_남긴다'의 전제를 **「톱니가 보이는 세계(사용자 숨김·가출)」 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:402` | `실기미확인_착지티어_교차배율이_Windows에서…` | 나 | 「필요」 | ¦ 09-02 02:39 ¦ '…실기미확인_착지티어_교차배율이_Windows에서…' ¦ 실기 하드웨어 필요 ¦ 자 |

## G. (마) 판정 불가 — 7건

| 파일:줄 | 이름 | 사유 |
|---|---|---|
| `Tasklist.md:8783` | `던져서_공중회전..._모자` | 잘린 이름이 어느 선언(현재·이력)과도 일치 안 함 — 앞 조각 8자 미만이거나 조각은 어딘가에 실재 |
| `Tasklist.md:25875` | `FocusNodBeatTests.끄덕임은..._2박이_L1보다크다` | 잘린 이름이 어느 선언(현재·이력)과도 일치 안 함 — 앞 조각 8자 미만이거나 조각은 어딘가에 실재 |
| `Tasklist.md:27571` | `사용자숨김_세계에서_톱니…` | 잘린 이름이 어느 선언(현재·이력)과도 일치 안 함 — 앞 조각 8자 미만이거나 조각은 어딘가에 실재 |
| `Tasklist.md:27823` | `미해결_…정보창_단축키` | 잘린 이름이 어느 선언(현재·이력)과도 일치 안 함 — 앞 조각 8자 미만이거나 조각은 어딘가에 실재 |
| `docs/SYSTEMS_EQUIPMENT_SCHEMA_IMPACT.md:151` | `이_안전한_기본값이_되고_가진_것을_빼앗지_않는다` | 잘린 이름이 어느 선언(현재·이력)과도 일치 안 함 — 앞 조각 8자 미만이거나 조각은 어딘가에 실재 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:281` | `AccessoryStrokeBudgetTests.HEAD` | 클래스의 TestName/SetName 런타임 이름에 이 낱말이 있다 |
| `docs/verify/QA_REGRESSION_2026-09-05.md:397` | `AppearanceShapeBudgetTests.PET` | 클래스의 TestName/SetName 런타임 이름에 이 낱말이 있다 |

## H. (라) 문서가 스스로 표기 — 152건 (범위·낱말만 목록, 전수는 `--json`)

| 문서 | 범위 | 표기 | 출현 |
|---|---|---|---|
| `Tasklist.md` | 단위 | 「개명」 | 3 |
| `Tasklist.md` | 단위 | 「개작」 | 1 |
| `Tasklist.md` | 단위 | 「명세만」 | 2 |
| `Tasklist.md` | 단위 | 「부재」 | 3 |
| `Tasklist.md` | 단위 | 「사라졌」 | 1 |
| `Tasklist.md` | 단위 | 「사라짐」 | 1 |
| `Tasklist.md` | 단위 | 「삭제」 | 22 |
| `Tasklist.md` | 단위 | 「예정」 | 1 |
| `Tasklist.md` | 단위 | 「정정」 | 1 |
| `Tasklist.md` | 단위 | 「존재하지 않」 | 1 |
| `Tasklist.md` | 단위 | 「존재한 적 없」 | 2 |
| `Tasklist.md` | 단위 | 「착수 전」 | 1 |
| `Tasklist.md` | 단위 | 「폐기」 | 1 |
| `Tasklist.md` | 단위 | 「한 번도 없」 | 1 |
| `Tasklist.md` | 절 | 「추가 권고」 | 2 |
| `docs/EQUIPMENT_SHAPE_SPEC.md` | 단위 | 「정정」 | 1 |
| `docs/GAME_ARCHITECTURE_REVIEW.md` | 단위 | 「삭제」 | 1 |
| `docs/PLATFORM_RIGHTCLICK_FAN.md` | 단위 | 「개명」 | 1 |
| `docs/PLATFORM_RIGHTCLICK_FAN.md` | 표 머리 | 「이름(안)」 | 6 |
| `docs/TASKBAR_REVEAL.md` | 단위 | 「N차 이름」 | 3 |
| `docs/UX_RIGHTCLICK_FAN_MENU.md` | 단위 | 「계획」 | 1 |
| `docs/UX_RIGHTCLICK_FAN_MENU.md` | 절 | 「이름 제안」 | 17 |
| `docs/inspection/R1_거짓주석_수정배정.md` | 단위 | 「사라진」 | 1 |
| `docs/localization/PLAN_1.0.md` | 단위 | 「존재하지 않」 | 1 |
| `docs/manual/01-where-to-click.md` | 단위 | 「사라진」 | 1 |
| `docs/marketing/TRUTH_INVENTORY.md` | 단위 | 「정정」 | 2 |
| `docs/security/SECURITY_MODEL.md` | 단위 | 「명세만」 | 2 |
| `docs/security/SECURITY_MODEL.md` | 단위 | 「존재한 적 없」 | 1 |
| `docs/security/SECURITY_MODEL.md` | 단위 | 「한 번도 없」 | 1 |
| `docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md` | 단위 | 「개작」 | 3 |
| `docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md` | 단위 | 「옛 인용」 | 3 |
| `docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md` | 단위 | 「이관」 | 1 |
| `docs/verify/BASELINE.md` | 단위 | 「개명」 | 4 |
| `docs/verify/BASELINE.md` | 단위 | 「사라졌」 | 1 |
| `docs/verify/BASELINE.md` | 단위 | 「정정」 | 2 |
| `docs/verify/QA_REGRESSION_2026-09-05.md` | 단위 | 「개명」 | 1 |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md` | 단위 | 「부재」 | 6 |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md` | 단위 | 「삭제」 | 3 |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md` | 단위 | 「정정」 | 1 |
| `docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md` | 문서 머리 | 「명세만」 | 45 |

## I. 스냅숏(신선도 가드 입력 — 손으로 고치지 말 것)

<!-- PROMISED-SCAN-SNAPSHOT v1
counts	가=2768	나=39	다=75	라=152	마=7	제외=214
row	나	AccessoryStrokeBudgetTests/머리_4종이_서로_구분된다	Tasklist.md	AccessoryStrokeBudgetTests.머리_4종이_서로_구분된다	1	1
row	나	CategoryTintAssetAlignmentAuditTests	Tasklist.md	CategoryTintAssetAlignmentAuditTests	1	1
row	나	CostumeAccrualGridTests	Tasklist.md	CostumeAccrualGridTests	1	1
row	나	CostumeAccrualGridTests	docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md	CostumeAccrualGridTests	1	1
row	나	CostumeProgressReadoutTests/코스튬_3행이_켜졌을_때의_세로_예산	docs/UX_CHARACTER_WINDOW_REFINE.md	CostumeProgressReadoutTests.코스튬_3행이_켜졌을_때의_세로_예산	1	1
row	나	CostumeSaveIsNotEntitlementTests	docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md	CostumeSaveIsNotEntitlementTests	1	1
row	나	DialogueComicTextPlacementTests/TiltFollowsTheConfig	Tasklist.md	DialogueComicTextPlacementTests.TiltFollowsTheConfig	2	1
row	나	DockLandingSilhouetteTests	Tasklist.md	DockLandingSilhouetteTests	2	1
row	나	ForeignFullscreenTierTests	docs/SCREEN_SHARE_DETECTION.md	ForeignFullscreenTierTests	1	1
row	나	IdleWindowRatchetTests	Tasklist.md	IdleWindowRatchetTests	1	1
row	나	IdleWindowRatchetTests	docs/security/SECURITY_MODEL.md	IdleWindowRatchetTests	2	1
row	나	InkColorPersistenceTests/구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_…	Tasklist.md	InkColorPersistenceTests.구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_...	1	1
row	나	ItemColorClosedSetAuditTests	Tasklist.md	ItemColorClosedSetAuditTests	1	1
row	나	PanelRetreatOnForeignFullscreenTests	docs/SCREEN_SHARE_DETECTION.md	PanelRetreatOnForeignFullscreenTests	1	1
row	나	PlatformParityAuditTests/미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다	docs/manual/03-disappeared.md	PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다	1	1
row	나	Tests/EditMode/LocalizationDebtCeilingAuditTests.cs	docs/localization/PLAN_1.0.md	LocalizationDebtCeilingAuditTests.cs	2	1
row	나	Tests/EditMode/VisibleTopEdgeSolverTests.cs	Tasklist.md	VisibleTopEdgeSolverTests.cs	1	1
row	나	Tests/EditMode/VisibleTopEdgeSolverTests.cs	docs/inspection/R1_거짓주석_수정배정.md	VisibleTopEdgeSolverTests.cs	2	1
row	나	Tests/EditMode/VisibleTopEdgeSolverTests.cs	docs/verify/QA_REGRESSION_2026-09-05.md	VisibleTopEdgeSolverTests.cs	1	1
row	나	Tests/EditMode/WallClockReadScopeAuditTests.cs	docs/security/SECURITY_MODEL.md	WallClockReadScopeAuditTests.cs	1	1
row	나	Tests/PlayMode/ZZCornerPanelCaptureHarness.cs	Tasklist.md	ZZCornerPanelCaptureHarness.cs	4	1
row	나	Tests/PlayMode/ZZGetupAngleSweep.cs	Tasklist.md	ZZGetupAngleSweep.cs	2	1
row	나	Tests/PlayMode/ZZGetupFloorDiagnostic.cs	Tasklist.md	ZZGetupFloorDiagnostic.cs	2	1
row	나	UiSpacingTokenAuditTests	docs/UI_SURFACE_SPEC.md	UiSpacingTokenAuditTests	1	1
row	나	VcE12ProbeTests	Tasklist.md	VcE12ProbeTests	1	1
row	나	WindowsFullscreenGamePolicyTests/Windows_구현에는_레지스트리_쓰기_API가_한_건도_없다	Tasklist.md	WindowsFullscreenGamePolicyTests.Windows_구현에는_레지스트리_쓰기_API가_한_건도_없다	1	1
row	나	ZZExploratorySweep3Tests	Tasklist.md	ZZExploratorySweep3Tests	1	1
row	나	ZZExploratorySweepTests	Tasklist.md	ZZExploratorySweepTests	2	1
row	나	네거티브컨트롤_창을_없애면_실제로_17280까지_샌다	docs/security/SECURITY_MODEL.md	네거티브컨트롤_창을_없애면_실제로_17280까지_샌다	1	1
row	나	다른_asmdef는_스팀을_언급하지_않는다	docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md	다른_asmdef는_스팀을_언급하지_않는다	1	1
row	나	머리_4종이_서로_구분된다	Tasklist.md	머리_4종이_서로_구분된다	2	1
row	나	미해결_Windows_알파필터가_WS_EX_TRANSPARENT_단독창을_놓친다	Tasklist.md	미해결_Windows_알파필터가_WS_EX_TRANSPARENT_단독창을_놓친다	1	1
row	나	세션_종료_경로도_토큰_파생_흔적을_닫는다	docs/systems/TASKBAR_LOGOFF_ORDERING.md	세션_종료_경로도_토큰_파생_흔적을_닫는다	1	1
row	나	실기미확인_착지티어_교차배율이_Windows에서…	docs/verify/QA_REGRESSION_2026-09-05.md	실기미확인_착지티어_교차배율이_Windows에서…	1	1
row	나	잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다	docs/strategy/ROADMAP.md	잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다	1	1
row	나	전체화면_판정_한_줄에_사용자숨김을_얹지_않는다	docs/verify/BASELINE.md	전체화면_판정_한_줄에_사용자숨김을_얹지_않는다	1	1
row	나	주석이_지목한_우리_타입의_멤버가_실재한다	docs/inspection/R2_거짓주석_전수조사.md	주석이_지목한_우리_타입의_멤버가_실재한다	1	1
row	나	창_480분이면_하루_5760에서_잘린다	docs/security/SECURITY_MODEL.md	창_480분이면_하루_5760에서_잘린다	1	1
row	나	축소폴백_원도_같이_줄어든다	Tasklist.md	축소폴백_원도_같이_줄어든다	1	1
row	다	AccessoryBeaniePomTests/모자_6종_실루엣_차이가_2_95획_아래로_내려가지_않는다	Tasklist.md	AccessoryBeaniePomTests.모자_6종_실루엣_차이가_2_95획_아래로_내려가지_않는다	1	1
row	다	AdaptiveFramePacingPolicyTests/오래_무입력이면_자리비움등급이다	Tasklist.md	AdaptiveFramePacingPolicyTests.오래_무입력이면_자리비움등급이다	1	1
row	다	CapeAirFlutterTests/망토_윤곽선은_밑단_5점을_흔든다	docs/verify/QA_REGRESSION_2026-09-05.md	CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다	2	1
row	다	CardShapeContractTests/열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다	docs/verify/QA_REGRESSION_2026-09-05.md	CardShapeContractTests.열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다	1	1
row	다	CharacterAppearanceLayerTests/모자를_쓰면_머리가_숨고…	Tasklist.md	CharacterAppearanceLayerTests.모자를_쓰면_머리가_숨고...	1	1
row	다	CharacterStatColumnLayoutTests/설계_크기에서_컬럼2가_본문_높이_안에_들어간다	docs/UX_CHARACTER_WINDOW_REFINE.md	CharacterStatColumnLayoutTests.설계_크기에서_컬럼2가_본문_높이_안에_들어간다	1	1
row	다	ComicFontFloorOutlineRingTests/배율1에서는_두_요구가_양립하지_않는다_보류	docs/UX_FLOW.md	ComicFontFloorOutlineRingTests.배율1에서는_두_요구가_양립하지_않는다_보류	1	1
row	다	CornerHoverPanelTests	Tasklist.md	CornerHoverPanelTests	14	6
row	다	CornerHoverPanelTests/상자가_다_자란_뒤에_다이얼이_나타난다	Tasklist.md	CornerHoverPanelTests.상자가_다_자란_뒤에_다이얼이_나타난다	2	2
row	다	CornerHoverPanelTests/숨어_있는_동안_클릭_차단막이_꺼져_있다	Tasklist.md	CornerHoverPanelTests.숨어_있는_동안_클릭_차단막이_꺼져_있다	1	1
row	다	EyesVisorOpacityTests/구성_정원과_보조색_개수를_지킨다	docs/verify/QA_REGRESSION_2026-09-05.md	EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다	1	1
row	다	EyesVisorOpacityTests/채움이_눈_자리를_덮는다	docs/EQUIPMENT_SHAPE_SPEC.md	EyesVisorOpacityTests.채움이_눈_자리를_덮는다	1	1
row	다	EyesVisorOpacityTests/채움이_눈_자리를_덮는다	docs/UX_FLOW.md	EyesVisorOpacityTests.채움이_눈_자리를_덮는다	1	1
row	다	F18_전체화면_억제_중에는_어떤_조합도_열지_않는다	docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md	F18_전체화면_억제_중에는_어떤_조합도_열지_않는다	1	1
row	다	FullscreenPanelRetreatTests/등급1에서_톱니를_누르면_설정창까지_도달한다	docs/manual/03-disappeared.md	FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다	1	1
row	다	FullscreenPanelRetreatTests/등급1에서_톱니를_누르면_설정창까지_도달한다	docs/marketing/TRUTH_INVENTORY.md	FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다	3	2
row	다	FullscreenPanelRetreatTests/등급1에서_톱니를_누르면_설정창까지_도달한다	docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md	FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다	1	1
row	다	FullscreenPanelRetreatTests/등급1은…캐릭터와_톱니는_남긴다	docs/CODER_UI_GEAR_REMOVAL_MAP.md	FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다	1	1
row	다	FullscreenPanelRetreatTests/미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다	docs/marketing/CLAIM_AUDIT_R5.md	FullscreenPanelRetreatTests.미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다	1	1
row	다	LandingCrouchTests/DockStepDropAbsorbsSoftlyWithoutKneeling	Tasklist.md	LandingCrouchTests.DockStepDropAbsorbsSoftlyWithoutKneeling	1	1
row	다	LandingCrouchTests/DockStepDropAbsorbsSoftlyWithoutKneeling	docs/MOTION_SPEC.md	LandingCrouchTests.DockStepDropAbsorbsSoftlyWithoutKneeling	1	1
row	다	LandingCrouchTests/DockStepDropDoesNotTriggerCrouch	Tasklist.md	LandingCrouchTests.DockStepDropDoesNotTriggerCrouch	1	1
row	다	PlatformParityAuditTests/미해결_Windows에는_가상데스크톱_동행_배선이_없다	Tasklist.md	PlatformParityAuditTests.미해결_Windows에는_가상데스크톱_동행_배선이_없다	2	1
row	다	SessionExitMarkerTests/원칙3_원본과_우리_폴더_밖은_한_바이트도_바뀌지_않고_아무것도_지워지지_않는다	Tasklist.md	SessionExitMarkerTests.원칙3_원본과_우리_폴더_밖은_한_바이트도_바뀌지_않고_아무것도_지워지지_않는다	1	1
row	다	SizeDialWidgetHitTestTests	Tasklist.md	SizeDialWidgetHitTestTests	2	1
row	다	Tests/PlayMode/FocusRingPoseSyncTests.cs	Tasklist.md	FocusRingPoseSyncTests.cs	1	1
row	다	WindowsMsaaDefaultTests/Windows_기본_MSAA는_2x다	Tasklist.md	WindowsMsaaDefaultTests.Windows_기본_MSAA는_2x다	1	1
row	다	WornShapeDataGoldenTests/방울_10각형은_옛_산술과_비트까지_같다	docs/verify/QA_REGRESSION_2026-09-05.md	WornShapeDataGoldenTests.방울_10각형은_옛_산술과_비트까지_같다	1	1
row	다	WornShapeDataGoldenTests/월요일_회전은_옛_산술과_비트까지_같다	docs/verify/QA_REGRESSION_2026-09-05.md	WornShapeDataGoldenTests.월요일_회전은_옛_산술과_비트까지_같다	1	1
row	다	v5_파일을_읽어도_구석_패널이_꺼지지_않는다	Tasklist.md	v5_파일을_읽어도_구석_패널이_꺼지지_않는다	1	1
row	다	내용물_게이트에서_다이얼이_상자_안에_완전히_들어간다	Tasklist.md	내용물_게이트에서_다이얼이_상자_안에_완전히_들어간다	1	1
row	다	네거티브컨트롤_유도를_끄면_큰_배율에서_맨틀_인셋이_경계에_먹힌다	Tasklist.md	네거티브컨트롤_유도를_끄면_큰_배율에서_맨틀_인셋이_경계에_먹힌다	1	1
row	다	대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다	docs/verify/BASELINE.md	대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다	1	1
row	다	등급1에서_톱니를_누르면_설정창까지_도달한다	docs/CODER_UI_GEAR_REMOVAL_MAP.md	등급1에서_톱니를_누르면_설정창까지_도달한다	2	2
row	다	등급1에서_톱니를_누르면_설정창까지_도달한다	docs/marketing/CLAIM_AUDIT_R5.md	등급1에서_톱니를_누르면_설정창까지_도달한다	1	1
row	다	등급1에서_톱니를_누르면_설정창까지_도달한다	docs/strategy/ROADMAP.md	등급1에서_톱니를_누르면_설정창까지_도달한다	1	1
row	다	등급1에서_톱니를_누르면_설정창까지_도달한다	docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md	등급1에서_톱니를_누르면_설정창까지_도달한다	2	1
row	다	등급1은_…톱니는_남긴다	docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md	등급1은_…톱니는_남긴다	1	1
row	다	등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다	docs/GAME_ARCHITECTURE_REVIEW.md	등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다	1	1
row	다	등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다	docs/strategy/ROADMAP.md	등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다	1	1
row	다	등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다	docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md	등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다	1	1
row	다	맨틀_인셋은_경계_판정_거리보다_커야_한다	Tasklist.md	맨틀_인셋은_경계_판정_거리보다_커야_한다	1	1
row	다	맨틀_인셋이_버티는_배율_천장을_기록한다	Tasklist.md	맨틀_인셋이_버티는_배율_천장을_기록한다	2	1
row	다	미해결_Windows에는_상단_예약띠_조회가_없다	Tasklist.md	미해결_Windows에는_상단_예약띠_조회가_없다	1	1
row	다	미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다	docs/manual/03-disappeared.md	미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다	1	1
row	다	미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다	docs/strategy/ROADMAP.md	미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다	1	1
row	다	미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다	docs/GAME_ARCHITECTURE_REVIEW.md	미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다	1	1
row	다	미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다	Tasklist.md	미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다	1	1
row	다	미해결_하이브리드GPU_선택이_macOS에는…	docs/verify/QA_REGRESSION_2026-09-05.md	미해결_하이브리드GPU_선택이_macOS에는…	1	1
row	다	백업이_성공했으면_저장은_정상_진행된다	Tasklist.md	백업이_성공했으면_저장은_정상_진행된다	1	1
row	다	사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다	docs/verify/BASELINE.md	사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다	6	6
row	다	상자가_다_자란_뒤에_다이얼이_나타난다	Tasklist.md	상자가_다_자란_뒤에_다이얼이_나타난다	4	2
row	다	숨어_있는_동안_클릭_차단막이_꺼져_있다	Tasklist.md	숨어_있는_동안_클릭_차단막이_꺼져_있다	2	1
row	다	아직_미완_커서친구는_머리와_꼬리로_안_쪼개졌다	Tasklist.md	아직_미완_커서친구는_머리와_꼬리로_안_쪼개졌다	1	1
row	다	전송계열_화이트리스트는_현재_비어_있다	docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md	전송계열_화이트리스트는_현재_비어_있다	3	1
row	다	전송계열_화이트리스트는_현재_비어_있다	docs/strategy/CHANNEL_PRICING_DECISIONS.md	전송계열_화이트리스트는_현재_비어_있다	1	1
row	다	착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다	docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md	착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다	1	1
row	다	출하_전량이_착용_비트맵_칸을_비웠다	Tasklist.md	출하_전량이_착용_비트맵_칸을_비웠다	1	1
row	다	카드_하단과_다이얼_원환_상단이_정확히_맞닿는다	Tasklist.md	카드_하단과_다이얼_원환_상단이_정확히_맞닿는다	2	1
row	다	패널_컴포넌트가_…붙어_있다	Tasklist.md	패널_컴포넌트가_...붙어_있다	1	1
row	다	획_두께를_빼면_천모자_민머리_조합이_실제로_깨진다	Tasklist.md	획_두께를_빼면_천모자_민머리_조합이_실제로_깨진다	1	1
row	라	AudioActivationPolicyTests	Tasklist.md	AudioActivationPolicyTests	4	4
row	라	B13_사용자_숨김_중_캐릭터_앵커_부채꼴은_몸_자리_우클릭으로_접히지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	B13_사용자_숨김_중_캐릭터_앵커_부채꼴은_몸_자리_우클릭으로_접히지_않는다	1	1
row	라	B14_사용자_숨김_중_정보창은_창_밖_몸_자리_우클릭으로_닫히지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	B14_사용자_숨김_중_정보창은_창_밖_몸_자리_우클릭으로_닫히지_않는다	1	1
row	라	B15_등급1_더하기_사용자_숨김에서도_B13_B14와_같고_허가가_나지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	B15_등급1_더하기_사용자_숨김에서도_B13_B14와_같고_허가가_나지_않는다	1	1
row	라	B16_가출_은신_자리_우클릭은_현행_찾기_신호를_내지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	B16_가출_은신_자리_우클릭은_현행_찾기_신호를_내지_않는다	1	1
row	라	C10_가림_숨김_동결_무반응은_안내를_영구_중단하지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	C10_가림_숨김_동결_무반응은_안내를_영구_중단하지_않는다	1	1
row	라	C11_톱니_앵커_부채꼴에서_캐릭터_우클릭_재앵커는_성공으로_기록된다	docs/UX_RIGHTCLICK_FAN_MENU.md	C11_톱니_앵커_부채꼴에서_캐릭터_우클릭_재앵커는_성공으로_기록된다	1	1
row	라	C12_가출_비은신_구간에는_안내_알약이_자발도_호버도_뜨지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	C12_가출_비은신_구간에는_안내_알약이_자발도_호버도_뜨지_않는다	1	1
row	라	C13_보존_동결_중에는_안내_알약이_뜨지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	C13_보존_동결_중에는_안내_알약이_뜨지_않는다	1	1
row	라	C8_정보창이_열린_채_보이는_몸_우클릭_한_번에_창이_닫히고_부채꼴이_열리고_안내가_영구_중단된다	docs/UX_RIGHTCLICK_FAN_MENU.md	C8_정보창이_열린_채_보이는_몸_우클릭_한_번에_창이_닫히고_부채꼴이_열리고_안내가_영구_중단된다	1	1
row	라	C9_캐릭터_앵커_부채꼴_토글_닫기는_안내를_영구_중단하지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	C9_캐릭터_앵커_부채꼴_토글_닫기는_안내를_영구_중단하지_않는다	1	1
row	라	C_세션_종료_뒤_quitting이_다시_돌면_진행_저장_처리기는_두_번_불린다_멱등은_처리기_책임이다	docs/TASKBAR_REVEAL.md	C_세션_종료_뒤_quitting이_다시_돌면_진행_저장_처리기는_두_번_불린다_멱등은_처리기_책임이다	1	1
row	라	CornerHoverPanelTests	Tasklist.md	CornerHoverPanelTests	14	2
row	라	DockGeometryInvariantTests/설정_절대값_커버리지_교차점은_tilesize_80과_81_사이다	Tasklist.md	DockGeometryInvariantTests.설정_절대값_커버리지_교차점은_tilesize_80과_81_사이다	1	1
row	라	DockLandingSilhouetteTests	Tasklist.md	DockLandingSilhouetteTests	2	1
row	라	F28_게이트_결정행_전수는_독립_구현표와_같다	docs/UX_RIGHTCLICK_FAN_MENU.md	F28_게이트_결정행_전수는_독립_구현표와_같다	1	1
row	라	F29_입력_소유가_없으면_어떤_열린_표면도_닫지_않고_허가도_없다	docs/UX_RIGHTCLICK_FAN_MENU.md	F29_입력_소유가_없으면_어떤_열린_표면도_닫지_않고_허가도_없다	1	1
row	라	F30_창을_닫는_결정행은_반드시_펼침을_동반한다	docs/UX_RIGHTCLICK_FAN_MENU.md	F30_창을_닫는_결정행은_반드시_펼침을_동반한다	1	1
row	라	F31_앵커를_못_구하면_아무것도_닫지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	F31_앵커를_못_구하면_아무것도_닫지_않는다	1	1
row	라	F32_가출_은신_행은_부채꼴을_열지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	F32_가출_은신_행은_부채꼴을_열지_않는다	1	1
row	라	F33_사전_안내_영구_중단은_R9_R11_R12에서만_기록된다	docs/UX_RIGHTCLICK_FAN_MENU.md	F33_사전_안내_영구_중단은_R9_R11_R12에서만_기록된다	1	1
row	라	F34_좌클릭_입구는_입력_소유만_보고_은신을_곱하지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	F34_좌클릭_입구는_입력_소유만_보고_은신을_곱하지_않는다	1	1
row	라	FocusRingPoseSyncTests	Tasklist.md	FocusRingPoseSyncTests	3	1
row	라	FullscreenPanelRetreatTests/등급1에서_톱니를_누르면_설정창까지_도달한다	docs/marketing/TRUTH_INVENTORY.md	FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다	3	1
row	라	FullscreenPanelRetreatTests/등급1에서_톱니를…	Tasklist.md	FullscreenPanelRetreatTests.등급1에서_톱니를…	1	1
row	라	IdleWindowRatchetTests	docs/security/SECURITY_MODEL.md	IdleWindowRatchetTests	2	1
row	라	PlatformParityAuditTests/미해결_Windows_작업표시줄_버튼은_…	Tasklist.md	PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_…	1	1
row	라	R7_재실행_가드는_표지_두_단계에만_걸린다	docs/TASKBAR_REVEAL.md	R7_재실행_가드는_표지_두_단계에만_걸린다	1	1
row	라	R7_정상_종료_기록은_실제로_쓴_뒤에만_서고_기동이_되돌린다	docs/TASKBAR_REVEAL.md	R7_정상_종료_기록은_실제로_쓴_뒤에만_서고_기동이_되돌린다	1	1
row	라	R7_정상_종료_기록은_실제로_쓴_뒤에만_서고…	Tasklist.md	R7_정상_종료_기록은_실제로_쓴_뒤에만_서고…	1	1
row	라	SizeDialWidgetHitTestTests	Tasklist.md	SizeDialWidgetHitTestTests	2	1
row	라	T2_실제_프리팹의_본체_선이_같은_규칙을_따른다	docs/verify/BASELINE.md	T2_실제_프리팹의_본체_선이_같은_규칙을_따른다	2	1
row	라	TeSmallScreenProbeTests	Tasklist.md	TeSmallScreenProbeTests	1	1
row	라	TeVisualAuditTests	Tasklist.md	TeVisualAuditTests	1	1
row	라	TempFloorDiagTests	Tasklist.md	TempFloorDiagTests	1	1
row	라	Tests/EditMode/LocalizationDebtCeilingAuditTests.cs	docs/localization/PLAN_1.0.md	LocalizationDebtCeilingAuditTests.cs	2	1
row	라	WallClockReadScopeAuditTests	Tasklist.md	WallClockReadScopeAuditTests	8	6
row	라	WallClockReadScopeAuditTests	docs/security/SECURITY_MODEL.md	WallClockReadScopeAuditTests	4	3
row	라	ZzVc5ReentrancyProbeTests	Tasklist.md	ZzVc5ReentrancyProbeTests	1	1
row	라	같은_토큰은_같은_꼬리를_낸다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	같은_토큰은_같은_꼬리를_낸다	1	1
row	라	결정_보조클릭_폴백은_macOS에만_건다	docs/PLATFORM_RIGHTCLICK_FAN.md	결정_보조클릭_폴백은_macOS에만_건다	1	1
row	라	고정_이름의_새로운_스키마는_보존하되_이번_실행을_멈추지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	고정_이름의_새로운_스키마는_보존하되_이번_실행을_멈추지_않는다	1	1
row	라	교체가_실패하면_흔적_경로에_직접_쓰지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	교체가_실패하면_흔적_경로에_직접_쓰지_않는다	1	1
row	라	기동해제_후_정상종료하면_자기_흔적을_닫고_파일은_하나다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	기동해제_후_정상종료하면_자기_흔적을_닫고_파일은_하나다	1	1
row	라	기록_다섯_값…	Tasklist.md	기록_다섯_값…	1	1
row	라	기록_여섯_값…	Tasklist.md	기록_여섯_값…	1	1
row	라	꼬리는_파일명으로_안전하다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	꼬리는_파일명으로_안전하다	1	1
row	라	꼬리에는_토큰의_부분_문자열이_없다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	꼬리에는_토큰의_부분_문자열이_없다	1	1
row	라	남의_토큰_흔적만_있고_자동숨김이_꺼져_있으면_아무것도_쓰지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	남의_토큰_흔적만_있고_자동숨김이_꺼져_있으면_아무것도_쓰지_않는다	1	1
row	라	남의_흔적이_있어도_자동숨김이_켜져_있으면_이번_해제는_정상이다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	남의_흔적이_있어도_자동숨김이_켜져_있으면_이번_해제는_정상이다	1	1
row	라	다른_토큰은_다른_꼬리를_낸다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	다른_토큰은_다른_꼬리를_낸다	1	1
row	라	두_설치의_흔적은_서로_다른_파일이다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	두_설치의_흔적은_서로_다른_파일이다	1	1
row	라	등급1은_…톱니는_남긴다	docs/marketing/TRUTH_INVENTORY.md	등급1은_…톱니는_남긴다	1	1
row	라	레거시_v1_닫힌_흔적은_시스템에_쓰지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	레거시_v1_닫힌_흔적은_시스템에_쓰지_않는다	1	1
row	라	레거시_v1_열린_흔적은_한_번_갚고_v1_형식으로_닫는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	레거시_v1_열린_흔적은_한_번_갚고_v1_형식으로_닫는다	1	1
row	라	레거시_v1_열림인데_이미_원래값이면_쓰지_않고_v1로_닫는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	레거시_v1_열림인데_이미_원래값이면_쓰지_않고_v1로_닫는다	1	1
row	라	레거시_열림인데_상태를_못_읽으면_닫지_않고_남긴다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	레거시_열림인데_상태를_못_읽으면_닫지_않고_남긴다	1	1
row	라	레거시를_닫은_파일은_구_프리뷰가_새로운_스키마로_읽지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	레거시를_닫은_파일은_구_프리뷰가_새로운_스키마로_읽지_않는다	1	1
row	라	로그에_흔적_경로와_토큰_원시값이_없다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	로그에_흔적_경로와_토큰_원시값이_없다	1	1
row	라	미해결_Windows_우리창이_전경이면_전체화면_등급이_None으로_떨어진다	Tasklist.md	미해결_Windows_우리창이_전경이면_전체화면_등급이_None으로_떨어진다	1	1
row	라	미해결_Windows에는_가상데스크톱_동행_배선이_없다	docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md	미해결_Windows에는_가상데스크톱_동행_배선이_없다	1	1
row	라	미해결_온보딩_봤음_기록이_PlayMode에서_실제_PlayerPrefs에_쓰인다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	미해결_온보딩_봤음_기록이_PlayMode에서_실제_PlayerPrefs에_쓰인다	1	1
row	라	미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다	docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md	미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다	1	1
row	라	미해결_하이브리드GPU_…	docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md	미해결_하이브리드GPU_…	1	1
row	라	미해결_하이브리드GPU_선택이_macOS에는_배선되지_않았다	Tasklist.md	미해결_하이브리드GPU_선택이_macOS에는_배선되지_않았다	1	1
row	라	반쯤_쓰인_레거시_v1은_갚지도_덮지도_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	반쯤_쓰인_레거시_v1은_갚지도_덮지도_않는다	1	1
row	라	반쯤_쓰인_자기_흔적은_어느_길이에서도_빚을_지우거나_거짓으로_만들지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	반쯤_쓰인_자기_흔적은_어느_길이에서도_빚을_지우거나_거짓으로_만들지_않는다	1	1
row	라	본문_토큰이_일치해도_이름이_다르면_자기_흔적으로_보지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	본문_토큰이_일치해도_이름이_다르면_자기_흔적으로_보지_않는다	1	1
row	라	삼킴상태_조회가_양_플랫폼에_모두_배선되어_있다	docs/PLATFORM_RIGHTCLICK_FAN.md	삼킴상태_조회가_양_플랫폼에_모두_배선되어_있다	1	1
row	라	세션_종료_경로도_토큰_파생_흔적을_닫는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	세션_종료_경로도_토큰_파생_흔적을_닫는다	1	1
row	라	소유_분류_진리표	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	소유_분류_진리표	1	1
row	라	스토어_SDK가_들어오면_이_경보가_먼저_울린다	docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md	스토어_SDK가_들어오면_이_경보가_먼저_울린다	1	1
row	라	실기미확인_우클릭_삼킴이_Windows_실기에서_확인되지_않았다	docs/PLATFORM_RIGHTCLICK_FAN.md	실기미확인_우클릭_삼킴이_Windows_실기에서_확인되지_않았다	1	1
row	라	실제_베레모_폴백_테는_이제_몸과_같은_점수다	Tasklist.md	실제_베레모_폴백_테는_이제_몸과_같은_점수다	1	1
row	라	어떤_토큰도_레거시_파일명을_만들지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	어떤_토큰도_레거시_파일명을_만들지_않는다	1	1
row	라	우클릭_부채꼴_차단막은_접히면_반드시_0으로_돌아간다	docs/PLATFORM_RIGHTCLICK_FAN.md	우클릭_부채꼴_차단막은_접히면_반드시_0으로_돌아간다	1	1
row	라	우클릭_부채꼴이_양_플랫폼_모두_같은_히트테스트를_쓴다	docs/PLATFORM_RIGHTCLICK_FAN.md	우클릭_부채꼴이_양_플랫폼_모두_같은_히트테스트를_쓴다	1	1
row	라	우클릭_부채꼴이_프레임페이싱_홀드를_부른다	docs/PLATFORM_RIGHTCLICK_FAN.md	우클릭_부채꼴이_프레임페이싱_홀드를_부른다	1	1
row	라	원자적_쓰기는_어느_바이트에서_죽어도_옛_흔적_아니면_새_흔적만_남긴다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	원자적_쓰기는_어느_바이트에서_죽어도_옛_흔적_아니면_새_흔적만_남긴다	1	1
row	라	원칙_3_감사의_쓰기_형태_수가_변하지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	원칙_3_감사의_쓰기_형태_수가_변하지_않는다	1	1
row	라	음성대조_남의_흔적과_같은_준비물에서_토큰만_일치하면_갚는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	음성대조_남의_흔적과_같은_준비물에서_토큰만_일치하면_갚는다	1	1
row	라	음성대조_로그_검사기는_경로와_토큰을_실제로_잡는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	음성대조_로그_검사기는_경로와_토큰을_실제로_잡는다	1	1
row	라	이름만_바꾼_남의_파일은_보존한다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	이름만_바꾼_남의_파일은_보존한다	1	1
row	라	읽기_잠금으로_못_읽은_흔적의_거동	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	읽기_잠금으로_못_읽은_흔적의_거동	1	1
row	라	읽을_수_없는_고정_이름_파일은_덮어쓰지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	읽을_수_없는_고정_이름_파일은_덮어쓰지_않는다	1	1
row	라	자기_흔적과_레거시가_둘_다_열려_있으면_복구_쓰기는_한_번이고_둘_다_닫는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	자기_흔적과_레거시가_둘_다_열려_있으면_복구_쓰기는_한_번이고_둘_다_닫는다	1	1
row	라	자기_흔적을_갚은_뒤_다시_읽은_값으로_이번_실행을_판정한다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	자기_흔적을_갚은_뒤_다시_읽은_값으로_이번_실행을_판정한다	1	1
row	라	자기_흔적이_새로운_스키마면_이번_실행은_쉰다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	자기_흔적이_새로운_스키마면_이번_실행은_쉰다	1	1
row	라	자동숨김이_꺼진_사용자는_토큰도_흔적도_만들지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	자동숨김이_꺼진_사용자는_토큰도_흔적도_만들지_않는다	1	1
row	라	잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	잔여위험_다른_PC에서_복사된_레거시_v1도_한_번_갚는다	1	1
row	라	잔여위험_토큰과_폴더가_함께_옮겨지면_일치로_보고_갚는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	잔여위험_토큰과_폴더가_함께_옮겨지면_일치로_보고_갚는다	1	1
row	라	전송계열_화이트리스트는_현재_비어_있다	Tasklist.md	전송계열_화이트리스트는_현재_비어_있다	1	1
row	라	전송계열_화이트리스트는_현재_비어_있다	docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md	전송계열_화이트리스트는_현재_비어_있다	3	2
row	라	제어기가_없으면_토큰_저장소를_한_번도_부르지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	제어기가_없으면_토큰_저장소를_한_번도_부르지_않는다	1	1
row	라	종료는_토큰을_새로_만들거나_확보하지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	종료는_토큰을_새로_만들거나_확보하지_않는다	1	1
row	라	토큰_구조는_중립_위치에_있고_파생_함수는_순수하다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰_구조는_중립_위치에_있고_파생_함수는_순수하다	1	1
row	라	토큰_확보_뒤에_흔적을_열고_그_뒤에_시스템을_바꾼다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰_확보_뒤에_흔적을_열고_그_뒤에_시스템을_바꾼다	1	1
row	라	토큰_확보가_실패하면_흔적도_시스템도_바꾸지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰_확보가_실패하면_흔적도_시스템도_바꾸지_않는다	1	1
row	라	토큰은_두_번째_해제에서_재사용되고_파일이_늘지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰은_두_번째_해제에서_재사용되고_파일이_늘지_않는다	1	1
row	라	파생_꼬리는_버전을_넘어_변하지_않는다_골든	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	파생_꼬리는_버전을_넘어_변하지_않는다_골든	1	1
row	라	회귀대조_지금은_읽을_수_없는_흔적에서_해제가_진행된다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	회귀대조_지금은_읽을_수_없는_흔적에서_해제가_진행된다	1	1
row	라	흔적_본문에_토큰_원시값이_없다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	흔적_본문에_토큰_원시값이_없다	1	1
row	라약	CapeAirFlutterTests/망토_윤곽선은_밑단_5점을_흔든다	docs/verify/QA_REGRESSION_2026-09-05.md	CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다	2	1
row	라약	CornerHoverPanelTests	Tasklist.md	CornerHoverPanelTests	14	3
row	라약	EyesVisorOpacityTests/구성_정원과_보조색_개수를_지킨다	docs/EQUIPMENT_SHAPE_SPEC.md	EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다	1	1
row	라약	FocusRingPoseSyncTests	Tasklist.md	FocusRingPoseSyncTests	3	1
row	라약	PlatformParityAuditTests/미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다	docs/manual/01-where-to-click.md	PlatformParityAuditTests.미해결_Windows_작업표시줄_버튼은_스타일_변경만으로_사라지지_않는다	1	1
row	라약	PlatformParityAuditTests/미해결_Windows에는_가상데스크톱_동행_배선이_없다	Tasklist.md	PlatformParityAuditTests.미해결_Windows에는_가상데스크톱_동행_배선이_없다	2	1
row	라약	RightClickFanGateTests/F18_전체화면_억제_중에는_어떤_조합도_열지_않는다	docs/UX_RIGHTCLICK_FAN_MENU.md	RightClickFanGateTests.F18_전체화면_억제_중에는_어떤_조합도_열지_않는다	1	1
row	라약	T2_실제_프리팹의_11개_선이_같은_규칙을_따른다	docs/verify/BASELINE.md	T2_실제_프리팹의_11개_선이_같은_규칙을_따른다	1	1
row	라약	T2_실제_프리팹의_본체_선이_같은_규칙을_따른다	docs/verify/BASELINE.md	T2_실제_프리팹의_본체_선이_같은_규칙을_따른다	2	1
row	라약	Tests/EditMode/AudioActivationPolicyTests.cs	docs/GAME_ARCHITECTURE_REVIEW.md	AudioActivationPolicyTests.cs	1	1
row	라약	Tests/PlayMode/ZZCornerPanelCaptureHarness.cs	Tasklist.md	ZZCornerPanelCaptureHarness.cs	4	1
row	라약	Tests/PlayMode/ZZDebuggerOverlapDiagnostic.cs	Tasklist.md	ZZDebuggerOverlapDiagnostic.cs	2	1
row	라약	VisibleTopEdgeSolverTests	docs/inspection/R1_거짓주석_수정배정.md	VisibleTopEdgeSolverTests	2	1
row	라약	WallClockReadScopeAuditTests	Tasklist.md	WallClockReadScopeAuditTests	8	2
row	라약	ZZExploratorySweep5Tests	Tasklist.md	ZZExploratorySweep5Tests	1	1
row	라약	ZZExploratorySweepTests	Tasklist.md	ZZExploratorySweepTests	2	1
row	라약	macOS_경로에서_토큰_확보_호출이_없다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	macOS_경로에서_토큰_확보_호출이_없다	1	1
row	라약	고정_이름의_토큰_없는_v2는_갚지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	고정_이름의_토큰_없는_v2는_갚지_않는다	1	1
row	라약	기록_여섯_값이…	Tasklist.md	기록_여섯_값이…	1	1
row	라약	망토_윤곽선은_밑단_5점을_흔든다	docs/verify/BASELINE.md	망토_윤곽선은_밑단_5점을_흔든다	1	1
row	라약	맨틀_인셋이_버티는_배율_천장을_기록한다	Tasklist.md	맨틀_인셋이_버티는_배율_천장을_기록한다	2	1
row	라약	미해결_Windows_보조버튼_조회가_물리버튼이라_반전설정을_따르지_않는다	docs/PLATFORM_RIGHTCLICK_FAN.md	미해결_Windows_보조버튼_조회가_물리버튼이라_반전설정을_따르지_않는다	1	1
row	라약	미해결_하이브리드GPU_선택이_macOS에는_배선되지_않았다	docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md	미해결_하이브리드GPU_선택이_macOS에는_배선되지_않았다	1	1
row	라약	쉬기를_택하면_같은_흔적에서_다음_실행도_계속_쉰다_비용_박제	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	쉬기를_택하면_같은_흔적에서_다음_실행도_계속_쉰다_비용_박제	1	1
row	라약	스토어_SDK가_들어오면_이_경보가_먼저_울린다	docs/verify/BASELINE.md	스토어_SDK가_들어오면_이_경보가_먼저_울린다	1	1
row	라약	실제_베레모_폴백_테는_이제_몸과_같은_점수다	docs/verify/BASELINE.md	실제_베레모_폴백_테는_이제_몸과_같은_점수다	1	1
row	라약	열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다	docs/verify/BASELINE.md	열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다	1	1
row	라약	읽을_수_없는_흔적_경고는_실제로_한_일과_어긋나지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	읽을_수_없는_흔적_경고는_실제로_한_일과_어긋나지_않는다	1	1
row	라약	카드_하단과_다이얼_원환_상단이_정확히_맞닿는다	Tasklist.md	카드_하단과_다이얼_원환_상단이_정확히_맞닿는다	2	1
row	라약	토큰_키를_쓰는_프로덕션_코드는_토큰_저장소_파일_한_곳뿐이다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰_키를_쓰는_프로덕션_코드는_토큰_저장소_파일_한_곳뿐이다	1	1
row	라약	토큰_테스트는_실제_PlayerPrefs를_부르지_않는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰_테스트는_실제_PlayerPrefs를_부르지_않는다	1	1
row	라약	토큰이_지워지고_열린_흔적이_남으면_갚지_않고_보존한다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰이_지워지고_열린_흔적이_남으면_갚지_않고_보존한다	1	1
row	라약	토큰이_지워진_뒤_해제하면_새_토큰과_새_파일이_생기고_옛_닫힌_파일은_남는다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	토큰이_지워진_뒤_해제하면_새_토큰과_새_파일이_생기고_옛_닫힌_파일은_남는다	1	1
row	라약	프로덕션과_테스트_어디에도_PlayerPrefs_DeleteAll이_없다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	프로덕션과_테스트_어디에도_PlayerPrefs_DeleteAll이_없다	1	1
row	라약	흔적_원자화는_삭제_이동_금지_감사_안에_머문다	docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md	흔적_원자화는_삭제_이동_금지_감사_안에_머문다	1	1
row	마	AccessoryStrokeBudgetTests/HEAD	docs/verify/QA_REGRESSION_2026-09-05.md	AccessoryStrokeBudgetTests.HEAD	1	1
row	마	AppearanceShapeBudgetTests/PET	docs/verify/QA_REGRESSION_2026-09-05.md	AppearanceShapeBudgetTests.PET	1	1
row	마	FocusNodBeatTests/끄덕임은…_2박이_L1보다크다	Tasklist.md	FocusNodBeatTests.끄덕임은..._2박이_L1보다크다	1	1
row	마	…이_안전한_기본값이_되고_가진_것을_빼앗지_않는다	docs/SYSTEMS_EQUIPMENT_SCHEMA_IMPACT.md	이_안전한_기본값이_되고_가진_것을_빼앗지_않는다	1	1
row	마	던져서_공중회전…_모자	Tasklist.md	던져서_공중회전..._모자	1	1
row	마	미해결_…정보창_단축키	Tasklist.md	미해결_…정보창_단축키	1	1
row	마	사용자숨김_세계에서_톱니…	Tasklist.md	사용자숨김_세계에서_톱니…	1	1
PROMISED-SCAN-SNAPSHOT-END -->
