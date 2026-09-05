# qa-regression 정적 회귀 — 2026-09-05 (라운드 R-QA11)

작성 16:20 KST · 담당 `qa-regression` · **Unity 배치모드 미실행**(리더 지시: `coder`가 `Library/`를 쓰고 있다)

이 문서는 **정적 회귀**다. 새로 돌린 러너는 없고, 이미 디스크에 있는 결과 xml 6판과 소스 트리만 잰다.
숫자의 정본은 `docs/verify/BASELINE.md`(같은 시각 재생성, 87행)이고, 여기 적은 값은 그 표에서 복사했다.

---

## 0. 측정 환경 — 무엇을 재고 무엇을 못 쟀는가

### 락 프로브 (TEAM.md 3회차 정본 그대로)

```
16:04  ls Temp/UnityLockfile                              -> 없음
       ps -ax | grep "Unity.app/Contents/MacOS/Unity"     -> 0건   (grep -v Hub 안 붙임)
       양성 대조: ps -ax | grep -c zsh                    -> 3     (프로브 살아 있음)
16:12  같은 프로브 -> 여전히 0건 / 양성 대조 zsh 6
```

★ **그런데 락이 비어 보이는 동안 러너는 세 번 돌았다** — `card-r19`(16:06) · `card-r19b`(16:09) ·
`card-r19c`(16:15). 25초짜리 실행이라 내 프로브 두 번 사이를 그냥 통과했다.
⇒ **프로세스 프로브의 "0건"은 「지금 이 순간 안 돈다」일 뿐 「이 라운드 동안 안 돈다」가 아니다.**
TEAM.md가 이미 적은 결론(*"판정은 프로브가 아니라 산출물로 한다"*)이 오늘 또 한 번 맞았다 —
**내가 실제로 쓴 판정 근거는 전부 `docs/verify/runs/*.xml`의 mtime과 `start-time`이다.**

### 활성 빌드 타깃 (rsp 실측)

| dag | 타깃 | rsp mtime |
|---|---|---|
| `200b0aE.dag` (EditMode) | **UNITY_STANDALONE_OSX** | 09-05 12:35 |
| `1900b0aE.dag` (EditMode) | UNITY_STANDALONE_WIN | 09-05 06:20 |

⇒ **오늘 오후 실행 4판(`card-r16`~`r19c`)은 전부 macOS 타깃이다.** Windows 전용 파일은 이 실행들에서
**컴파일되지 않았다**(§5-D 참조).

### 이 라운드가 **재지 못한** 축 (정직하게 남긴다)

- **PlayMode 전량** — 마지막 실행이 `qa-r10`(09:56)이고 그 뒤 프로덕션 15개가 바뀌었다(§3).
- **Windows 타깃 EditMode** — 마지막이 `qa-r9`(05:37).
- **크로스컴파일** — 마지막이 06:01. 그 뒤 생긴 Windows 신규 301줄은 미검증(§5-D).

---

## 1. 오늘 하루의 실행 대장 — 한눈에

| 시각 | 라벨 | total | 통과 | **실패** | **건너뜀** | 비고 |
|---|---|---:|---:|---:|---:|---|
| 09:33 | `qa-r10` | 2016 | 1997 | **3** | **16** | 계약 v2 착지 **전** |
| 12:35 | `card-r16` | 2031 | 1935 | **80** | **16** | ← 계약 v2 착지. 실패 +77 |
| 15:49 | `card-r17` | 2042 | 1950 | **6** | **86** | ← 건너뜀 **+70** |
| 16:06 | `card-r19` | 2042 | 1949 | 5 | 88 | 래칫이 1건 잡아 빨감 |
| 16:09 | `card-r19b` | 2042 | 1951 | 1 | 90 | |
| 16:15 | `card-r19c` | 2042 | 1951 | **1** | **90** | 현재 |

**케이스 단위 전이 회계** (같은 이름이 여러 번 나오는 파라미터 케이스까지 센 값):

| 구간 | 실패 | 건너뜀 | 신규 건너뜀의 **직전 상태** |
|---|---|---|---|
| r16 → r17 | 80 → 6 | 16 → 86 | **빨강→건너뜀 61 · 초록→건너뜀 7** (계 70) |
| r17 → r19c | 6 → 1 | 86 → 90 | 빨강→건너뜀 3 · 초록→건너뜀 1 (계 4) |
| **qa-r10 → r19c** | **3 → 1** | **16 → 90** | **초록→건너뜀 73 · 빨강→건너뜀 1 (계 74)** |

> **오늘 아침 09:33에 초록이던 73개 케이스가 저녁에는 아무것도 재지 않는다.**
> 실패 수가 3 → 1로 줄어든 것은 **고쳐서가 아니라 대부분 꺼져서**다.

남은 유일한 빨강: `EyesVisorOpacityTests.여섯_종이_화면에서_서로_구분된다`
(마지막 초록 `card-r16` 12:35 → 처음 빨감 `card-r17` 15:49 → 연속 빨강 4회).

---

## 2. 🔴 P0 — **Ignore 래칫이 우회됐다.** 66건이 등록 없이 꺼졌고 감사는 초록이었다

### 사실

`TestClaimExpiryAuditTests.Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다`는
**테스트 메서드 본문 안의 `Assert.Ignore(` 문자열**을 세서 명부와 대조한다.

```csharp
// TestClaimExpiryAuditTests.cs — 수집부
string ignoreToken = "Assert" + ".Ignore(";
...
if (src.IndexOf(ignoreToken, StringComparison.Ordinal) < 0) continue;   // 파일 단위 1차 거름
foreach ((string method, string body) in TestMethods(src))              // ★ public void / public IEnumerator 만
    if (body.IndexOf(ignoreToken, StringComparison.Ordinal) >= 0) found.Add((fileName, method));
```

`MethodNameOrNull`이 인정하는 머리는 `"public void "` / `"public IEnumerator "` / `"public System.Collections.IEnumerator "` **셋뿐**이다.

오늘 신설된 `Assets/_Project/Scripts/Tests/EditMode/HandoffTestGate.cs`는 이렇게 생겼다:

```csharp
internal static void SkipIfHandoff(EquipmentSlot slot, int item, string lostRule)   // ← public void 가 아니다
{
    if (!IsHandoffItem(slot, item)) return;
    Assert.Ignore($"★ 인계본 교체(계약 v2, 2026-09-05) — ...");
}
```

- 이 파일은 `Assert.Ignore(`를 **갖고 있어서** `tokenHitFiles`는 늘지만,
  `TestMethods()`가 `public void`를 하나도 못 찾아 **`found`에는 0건**이 들어간다.
- 호출부 **30곳**(테스트 파일 10개)은 `HandoffTestGate.SkipIfHandoff(`만 쓴다 —
  그 문자열에는 `Assert.Ignore(`가 없으므로 **파일 단위 1차 거름에서 아예 탈락**한다.

```
grep -rn --include='*.cs' 'HandoffTestGate.SkipIfHandoff' Tests/  ->  30건 (10파일)
그 10개 파일의 Assert.Ignore( 직접 사용                          ->  전부 0건
grep -n 'HandoffTestGate\|SkipIfHandoff' TestClaimExpiryAuditTests.cs -> 0건 (래칫은 이 우회를 모른다)
```

⇒ **런타임 66개 케이스가 「건너뜀」이 되었고, 명부 등록은 0건이며, 래칫은 통과했다.**

### ★ 같은 날 같은 파일이 만들어 준 **양성 대조** — 이게 결정적이다

16:06 `card-r19`에서 그 래칫이 **실제로 빨개졌다**:

```
[주장만료] Ignore 명부가 실물과 어긋났습니다(1건):
  · EyesVisorOpacityTests.cs::채움_메시가_실제로_만들어진다
      명부에 없는 Ignore가 새로 생겼습니다. …
```

`EyesVisorOpacityTests.cs`는 `Assert.Ignore(`를 **직접** 썼고 → 래칫이 **한 건도 안 놓치고 잡았다**.
`coder`는 16:09에 명부에 등록했고 초록이 됐다(`IgnoreEntry` 2건 추가, 미커밋 diff에서 실측).

> **같은 실행에서 직접 호출 1건은 잡히고 간접 호출 66건은 안 잡혔다.**
> 「감사가 죽었다」가 아니라 **「감사는 살아 있는데 새 경로가 그 옆으로 지나갔다」**이고,
> 그래서 **출력이 정상과 완전히 똑같이 생겼다** — 이 저장소 거짓 통과의 표준형이다.

### 처방 (coder/test-engineer 몫 — 나는 테스트를 고치지 않았다)

1. `TestClaimExpiryAuditTests`의 수집부에 **간접 게이트 토큰**을 더한다.
   토큰은 하드코딩하지 말고 `nameof(HandoffTestGate.SkipIfHandoff)`로 프로덕션(=테스트 헬퍼)에서 가져온다 —
   헬퍼 이름이 바뀌면 **컴파일이 깨지는 쪽**이 조용히 0건이 되는 쪽보다 낫다
   (`UiInteractionFramePacingHoldTests`가 이미 세운 본. §4-C 참조).
2. 헬퍼가 늘어날 것을 전제로, **「테스트를 건너뛰게 만드는 헬퍼」 목록 자체를 명부화**하고
   그 목록이 비면 실패시킨다(빈 목록 = 거짓 통과 형태 #5).
3. 66건은 **아이템 단위 사실**이라 메서드 단위 명부에 그대로 못 담는다 —
   `RatchetKind.자동` + `HandoffTestGate` 한 줄로 묶어 등록하고, **되살림 장치**(`IsHandoff`가 false가 되면
   스스로 열린다)가 실제로 동작하는지 **양성 대조 테스트 1건**을 붙여라
   (지금은 「스스로 열린다」가 주석에만 있고 아무도 재지 않는다).

---

## 3. 🔴 P1 — PlayMode가 계약 v2를 **한 번도 못 봤다**

- PlayMode 마지막 전량: **`qa-r10` 09:56** (641 / 633 통과 / 0 실패 / 8 건너뜀).
- 그 뒤 바뀐 **프로덕션** `.cs` 15개 (`find -newermt "2026-09-05 09:56"` 실측):

```
Core/AccessoryDefSO.cs          Core/AccessoryShapeContract.cs   Core/AccessoryStroke.cs
Core/EquipmentModel.cs          Core/ItemCatalog.cs              Core/StickConfig.cs
Core/StickPackManifestSO.cs     Core/StickmanAgent.cs            Interaction/AccessoryCardIcon.cs
Interaction/AccessoryHandoffPalette.cs                           Interaction/AccessoryShapeBuilder.cs
Interaction/AccessoryShapeBuilder.Handoff.cs                     Interaction/CharacterAccessoryRenderer.cs
Interaction/CharacterPortraitStage.cs                            Interaction/UiChrome.cs
```

- 그 표면을 실제로 만지는 PlayMode 클래스 **24개 / 케이스 124건 = 전체 641건의 19.3%**
  (`AccessoryFillRenderingTests` · `CharacterPortraitStageTests` · `PortraitPaperDollTests` ·
   `CapeFallFlutterTests` · `AccessoryFacingFlipFillTests` · `CharacterAppearanceLayerTests` 등).

★ EditMode는 **도형 좌표와 계약**을 재고, PlayMode는 **실제 LineRenderer/메시가 화면에 서는가**를 잰다.
계약 v2는 `alpha` · `noStroke` · `layer` · `bodyAlpha` · `MirrorX` 같은 **렌더 시점 속성**을 새로 만들었다 —
**EditMode가 구조적으로 못 보는 축이 바로 그것들이다.**

⇒ **리더 승인 요청: 커밋 전에 PlayMode 전량 1회.** (락은 `coder`와 조율 필요, 실행 ~23분)

---

## 4. 니들 썩음 감사

### A. 방법론 정정 — **이전의 «존재 62 / 부재 61»은 재현 불가다**

앞 라운드가 남긴 62/61은 **집계 방법이 어디에도 기록돼 있지 않다.** 스크립트도 없다
(`docs/verify/`에 `baseline.py` · `renames.py` · `regress.sh` 셋뿐).
같은 방법을 복원하지 못한 채 새 숫자를 그 옆에 적으면 **증감이 측정처럼 보이지만 사실이 아니다** —
이 저장소가 «분모와 기준선을 잘못 잡아» 3/37 → 1/37 개선으로 보고했다가 실측이 0/38 → 1/38 회귀였던
사고와 같은 형태다(TEAM.md §리더 상시 의무).

**그래서 증감을 적지 않는다.** 대신 **구문으로 정의된 재현 가능한 스캐너**를 오늘 기준선으로 세운다.

| 축 | 정의(구문) | 오늘 실측 |
|---|---|---:|
| **존재 단언 니들** | `StringAssert.Contains("N",…)` · `Assert.IsTrue(x.Contains("N"))` · `Assert.Greater(x.IndexOf("N"),…)` · `IndexOfOrFail(x,"N",…)` · `Assert.AreNotEqual(-1, x.IndexOf("N"))` | **372** 표현 |
| **부재 단언 니들** | `Assert.IsFalse(x.Contains("N"))` · `StringAssert.DoesNotContain("N",…)` · `Assert.AreEqual(-1, x.IndexOf("N"))` · `Assert.Less(x.IndexOf("N"),0)` · `!x.Contains("N")` · `‖ x.Contains("N")` 연쇄 | **139** 표현 |

스캐너 양성/음성 대조: 프로덕션 `.cs` 수집 >100파일 · `StickConfig` 히트 617 · 존재하지 않는 식별자 0건
(하나라도 깨지면 아래 모든 "0건"을 폐기한다).

### B. ★ 부재 니들 — **「대상이 사라져서」 죽은 것은 0건.** 위험은 다른 데 있었다

정의서가 지목한 형태(*"부재 니들 중 대상이 이미 사라진 것"*)를 전수했다.

- 니들 안 식별자가 **프로덕션 0건**인 부재 단언: **9건**.
- **9건 전부 정상이다** — 되짚어 확인했다:
  `MoveWindowToDesktop` · `SetWindowDesktopId`(호출하면 안 되는 Win32 API) ·
  `UnityWebRequest` · `HttpClient`(오프라인 원칙) ·
  `_mainDisplayResolved` · `_hadFile` · `_backup` · `secret`(전부 **네거티브 컨트롤 픽스처**의 일부러 없는 이름).
  **9건 모두 같은 테스트 안에 양성 대조가 붙어 있다.**

> **이번 라운드에 「대상이 사라져 조용히 초록이 된 부재 니들」은 찾지 못했다.**
> 지난 라운드들이 `nameof` 전환·양성 대조 부착으로 실제로 이 구멍을 메웠다.

### C. ★ 대신 나온 것 — **「형제 이름 우회」.** 오늘 하나가 새로 태어났다

부재 니들이 특정 식별자를 겨눌 때, 프로덕션에 **꼬리를 공유하는 다른 식별자**가 있으면
회귀가 그 형제 이름으로 들어와 가드를 **조용히 지나간다**. 16건이 걸렸고 **1건이 오늘 생겼다.**

#### 🔴 P1 — `FillOutlineStrokeFloorTests.인벤토리_카드_아이콘에는_여전히_화면상_하한이_없다`

```csharp
// 이 A/B 대조군 가드가 아는 이름은 둘뿐이다
Assert.IsFalse(src.Contains("MinStrokeScreenPoints"),      …);
Assert.IsFalse(src.Contains("MinFillOutlineScreenPoints"), …);
```

**오늘 세 번째 하한이 태어났다:**

```
git show HEAD:…/StickConfig.cs | grep -c MinAccessoryStrokeScreenPoints   -> 0   (어제까진 없었다)
grep -c MinAccessoryStrokeScreenPoints …/StickConfig.cs                   -> 1   (StickConfig.cs:1982 = 1f)
```

문자열 포함 관계 실측:

```
"MinStrokeScreenPoints"      in "MinAccessoryStrokeScreenPoints"  -> False
"MinFillOutlineScreenPoints" in "MinAccessoryStrokeScreenPoints"  -> False
```

⇒ **카드 아이콘이 `MinAccessoryStrokeScreenPoints`를 참조하기 시작하면 이 가드는 초록인 채로 통과한다.**
지금은 `AccessoryCardIcon.cs`에 그 참조가 0건이라 **결론은 아직 참**이다. 하지만 **가드가 참을 지키고 있는 게 아니라
운이 좋은 것**이고, 그 사이 카드 획을 결정하는 코드는 1개 파일 → **4개 파일**로 흩어졌다
(`AccessoryCardIcon.cs` · `AccessoryShapeBuilder.Handoff.cs` · `AccessoryShapeContract.cs` · `AccessoryShapeBuilder.cs`).
**가드의 스코프(1파일)가 지키려는 대상(4파일)보다 좁다.**

처방: 니들 3개로 늘리고(세 번째는 `nameof(StickConfig.MinAccessoryStrokeScreenPoints)`),
스코프를 카드 획을 만드는 4개 파일로 넓히고, **「이 목록이 비면 실패」**를 붙인다.

#### 나머지 15건 (전부 지금은 참, 형제 이름으로 우회 가능)

| 테스트 | 니들 | 프로덕션의 형제 |
|---|---|---|
| `OverlayTransparencyReapplyTests.cs:413` | `IsBorderless` | `SetBorderless` · `_setWindowBorderless` · `bIsBorderless_` · `osBorderless` (6종) |
| `TopmostBandOcclusionTests.cs:361` | `DwmSetWindowAttribute` | `SetLayeredWindowAttributes` · `GetLayeredWindowAttributes` |
| `PlatformParityAuditTests.cs:1628` | `_setResolutionCalls` | `MaxSetResolutionCalls` · `DefaultMaxSetResolutionCalls` |
| `PlatformParityAuditTests.cs:1631` | `_windowResizeCalls` | `DefaultMaxWindowResizeCalls` |
| `PlatformParityAuditTests.cs:238` | `IReservedTopBarService` | `MacReservedTopBarService` |
| `ManualHideAxisSeparationAuditTests.cs:104` | `_userHidden` | `userHidden` (밑줄 없는 판) |
| `SystemAudioActivityProbeAuditTests.cs:189` | `IAudioClient` | `IID_IAudioClient` |
| `SystemAudioActivityProbeAuditTests.cs:125` | `ScopeInput` | `kAudioObjectPropertyScopeInput` |
| `SystemAudioActivityProbeAuditTests.cs:211` | `Threshold` | 6종 이상(너무 일반적인 니들) |
| `InfoGearRadialMenuTests.cs:509` | `Popover` / `Blocker` | 각각 6종 이상 |
| `AwayTierMotionGuardTests.cs:363` | `characterIdle` | `ResolveCharacterIdle` |
| `SpikeInstrumentContextTests.cs:108` | `renderFrameInterval` | `RenderFrameInterval` (대문자) |
| `VirtualDesktopProbeAuditTests.cs:166` | `SetWindowDesktopId` | `GetWindowDesktopId` |
| `AppSwitcherPresenceTests.cs:188` | `UNITY_STANDALONE_` | `UNITY_STANDALONE_OSX/WIN` (의도된 접두 매칭 — 정상) |

이 중 **P2**는 `_userHidden`↔`userHidden`과 `renderFrameInterval`↔`RenderFrameInterval` —
**대소문자·밑줄 하나로 갈리는 짝**이고, 이 저장소는 이미 대소문자 때문에 프로브가 죽은 사고를 겪었다(TEAM.md 12번째 형태).

### D. 양성 대조 없는 부재 단언 — 24건 / 139건

스캐너 판정이므로 **오탐이 섞여 있다**(자백): `UiInteractionFramePacingHoldTests.cs:461`은
「양성 대조 없음」으로 분류됐지만 실제로는 `IndexOfOrFail(...)`로 **존재 단언이 같은 테스트 안에 있다** —
내 휴리스틱이 `Assert.IsTrue`/`Greater` 문자열만 봐서 놓쳤다. 24건은 **상한**으로 읽어라.

### E. 🟡 P3 — 테스트가 아이템 표시명을 문자열로 베낀 자리 6건, 그중 1건은 **이미 틀렸다**

애셋 42종의 `displayName`을 정본으로 잡고 테스트의 `SetName`/`TestName` 라벨 50종과 대조:

| 테스트가 적은 이름 | 애셋 정본 | 판정 |
|---|---|---|
| **`HEAD 야구모자`** (`AccessoryStrokeBudgetTests.cs:526`) | **`천모자`** (`equip_head_cap.asset`) | 🔴 **틀렸다** |
| `EYES 뿔테안경` ×2 | `뿔테 안경` | 공백 |
| `BACK 요정날개` ×3 | `요정 날개` | 공백 |
| `NECK 펜던트` | `펜던트 목걸이` | 잘림 |

★ **같은 파일 `:124`는 같은 아이템을 `"HEAD 천모자"`로 적는다.** 한 파일 안에서 Head 0번이 두 이름이다.
그래서 오늘 러너가 **`AccessoryStrokeBudgetTests.HEAD 야구모자`를 「건너뜀」으로 찍으면서
사유에는 `Head 0번(천모자)`**라고 적었다 — 사유 문자열은 `ItemCatalog.…DisplayName`에서 오기 때문이다.
**이름으로 대조하는 사람은 이 둘을 다른 아이템으로 읽는다.**

---

## 5. 오늘 라운드가 깬 것 / 끈 것 / 지운 것 — coder 인계 목록

### A. 🔴 지금 빨간 것 (1건)

| 테스트 | 실패 내용 | 마지막 초록 | 연속 빨강 |
|---|---|---|---:|
| `EyesVisorOpacityTests.여섯_종이_화면에서_서로_구분된다` | `EYES 1번(동그란안경)의 채움이 격자를 하나도 덮지 않습니다 — 판이 획보다 작습니다. Expected: greater than 0 / But was: 0` | `card-r16` 12:35 | 4 |

★ 같은 라운드가 `가리개_채움이_눈_자리를_덮는다(1)`·`모든_가리개가_채움_실루엣을_갖는다(1)`은
「투명 렌즈(팔레트 L-4)」로 건너뛰게 만들었는데 **이 한 건만 안 껐다.**
둘 중 하나다 — (가) 이것도 같은 사유로 꺼야 하는데 빠졌다, (나) 투명 렌즈 판정이 이 검사에서는 안 맞는다.
**어느 쪽인지 결정하지 않은 채 남겨 두면 「원래 빨간 것」이 된다.**

### B. 🟠 지운 테스트 4종 (Ignore가 아니라 **삭제**)

| 지워진 테스트 | r10 상태 | 묘비 | 판정 |
|---|---|---|---|
| `WornShapeDataGoldenTests.월요일_회전은_옛_산술과_비트까지_같다` | Passed | ✅ `WornShapeDataGoldenTests.cs:182` 주석 | 사유 기록됨 |
| `WornShapeDataGoldenTests.방울_10각형은_옛_산술과_비트까지_같다` | Passed | ✅ 같은 주석 | 사유 기록됨 |
| `EyesVisorOpacityTests.구성_정원과_보조색_개수를_지킨다` (6 케이스) | Passed | ✅ `EyesVisorOpacityTests.cs:441` | «조각 정원 ≤ 2~4 게이트 폐지» |
| `CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다` | Passed | ❌ **묘비 0건** | **개명이었다**(아래) |

★ 삭제된 두 비트 대조(`월요일 회전`·`방울 10각형`)는 **오프라인 하니스와 Mono 런타임의 ULP 차이를 잡으려고
일부러 엔진 안에 둔 것**이었다(주석이 그 사고를 기록하고 있다: cos 6 ULP · sin 4 ULP).
지금은 **인계본 스트림에 삼각항이 없어 그 축이 사라졌다**는 것이 삭제 근거인데,
⇒ **「삼각항이 정말로 0개인가」를 재는 검사가 없다.** 누가 스트림에 `Sin/Cos`를 다시 넣으면
그 축은 다시 살아나는데 **그때 이 안전망은 이미 없다.** (제안: 스트림 문법에 삼각 연산자가
0개임을 잠그는 단언 1건. 그게 이 삭제의 성립 조건이다.)

### C. 🟢 개명 2건 — **대장에 등재했다** (`docs/verify/renames.tsv`, 4 → 6건)

| 옛 이름 | 새 이름 | R1 | R2 |
|---|---|---|---|
| `CapeAirFlutterTests.망토_윤곽선은_밑단_5점을_흔든다` | `…망토_뒤판의_흔들_구간은_밑단이다` | 선언 `:249` | 소스 0건 |
| `CardShapeContractTests.열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다` | `…아이템은_한_벌이거나_두_벌이고_두_벌은_표면과_층이_명시된다` | 선언 `:365` | 소스 0건 |

`renames.py --check` 재검증: **적용 6 / 거부 0**, 자기검사 8종(양성 5 · 음성 3) 전부 통과.
두 개명 다 **검사가 좁아진 게 아니라 넓어졌다**(후자는 표면 명시·층/정렬번호·`bodyFixed`·유리 워시 α<1이 새로 들어왔다).

### D. 🟠 P1 — Windows 신규 301줄이 **어디에서도 컴파일되지 않았다**

```
Assets/_Project/Scripts/Platform/Windows/WindowsVirtualDesktopProbe.cs   301줄, 생성 09-05 09:03, 미추적(??)
  1행: #if UNITY_STANDALONE_WIN        ← macOS 타깃 실행에서는 타입이 존재하지 않는다
마지막 크로스컴파일 기록: docs/verify/runs/qa-r9_xcheck_win.txt (06:01, runtime sources=226)
```

**06:01 < 09:03** — 이 파일이 태어나기 **전**의 기록이다. 오늘 오후 러너 4판은 전부 macOS 타깃.
⇒ **P/Invoke 301줄이 한 번도 컴파일러를 통과하지 않았다.**

🟢 다만 짝 테스트 `VirtualDesktopProbeAuditTests`는 **타입이 아니라 소스 텍스트를 읽는다**
(`File.ReadAllText(ProbePath)`, `:40`) — CLAUDE.md의 «플랫폼 감사는 타입이 아니라 소스 파일을 읽도록 짠다»를
정확히 지켰다. 그래서 macOS 타깃에서도 **감사는 공허하지 않다.** 남은 것은 **컴파일 확인 하나**다.

⇒ 처방: `Tools/CrossCompile/xcheck.sh win` (+ `osx` 회귀) 1회. Unity 락과 무관하다.

### E. 🟡 P2 — 비공허성 게이트가 **50 → 20으로 내려갔다**(실제 34)

`AccessoryRuleOneCoverageTests.면제되지_않은_모든_도형이_획_예산을_지킨다`:

```diff
-            Assert.Greater(checkedShapes, 50,
+            Assert.Greater(checkedShapes, 20,
```

🟢 좋은 점: 같은 diff에 `Assert.Greater(handoffShapes, 0, "인계본 조각이 하나도 안 잡혔습니다 …")`
**양성 대조가 함께 들어왔다.** 「인계본 제외가 전부를 삼켰는가」를 막는다.

🟡 남는 문제: 실측 34 vs 문턱 20 → **여유 14.** 도형이 13개 더 조용히 사라져도 초록이다.
계약 v2로 「몇 개여야 하는가」가 확정됐으니 `Assert.AreEqual(34, checkedShapes)` 또는
`Greater(checkedShapes, 30)`이 래칫으로 맞다. **비공허성 게이트는 «0이 아님»이 아니라 «줄지 않음»을 재야 한다.**

### F. 🟡 P2 — 골든이 **240줄 줄었다**(NECK 0~3의 몸 표면 비트 잠금 이관)

`Golden/NeckWornShapeGolden.txt` 321행 → 120행. 나비넥타이·줄무늬타이·목도리·방울목걸이 4종이 빠졌다.

🟢 좋은 점 셋 (전부 실측 확인):
1. 헤더에 **무엇을·왜·어떻게 구웠는지**가 남았고, 남은 20줄 개정은 **오프라인 하니스를 286줄 비트 동일로 먼저 교정**한 뒤 바꿨다.
2. `WornShapeDataGoldenTests`에 **공허 방지**가 새로 들어왔다 —
   `Assert.Greater(v1Items, 0, "v1 파일럿 아이템이 하나도 안 남았습니다 — 이 골든 대조는 공허합니다")`.
3. 대체 자 `CardShapeContractTests`도 공허 방지가 있다 —
   `ReadGolden()` 안 `Assert.Greater(g.Pieces.Count, 0)` + `ByItem()` 안 `Assert.AreEqual(16, byItem.Count)`.

🟡 남는 문제: 이관된 축이 **완전히 같지 않다.** 옛 골든은 «에셋→런타임 몸 좌표»의 **비트**를 잠갔고,
새 골든은 «설계 모델→조각»을 잠근다. **에셋 YAML이 몸 좌표로 풀리는 구간**의 비트 잠금이 NECK 0~3에서 사라졌다.
`WornShapeContractCompatTests` 8건이 그 자리를 일부 메우는지 **확인이 필요하다**(나는 못 쟀다 — 미확인).

### G. 🟢 스키마 v2 — 규칙대로 됐다 (확인함)

- `StickPackManifestSO.SchemaVersion` **1 → 2**.
- CLAUDE.md 요구(«vN-1 구버전 파일이 안전한 기본값으로 채워지는지 검증하는 하위 호환 테스트 1건»)
  → `Tests/EditMode/WornShapeContractCompatTests.cs` **실재하고 8건 전부 통과**(`card-r19c`).
- 🟡 다만 그 근거 주석이 **없는 필드를 인용한다**:
  `StickPackManifestSO.cs`가 «`strokeGrade / surfaces / alpha / underBack`이 생겼다»라고 적었는데
  `strokeGrade`는 **저장소 전체에서 이 주석 1건뿐**(실제 이름은 `strokeMult` 121건 + `strokeInR`).
  ★ **정정**: 처음에 *"`CommentReferenceAuditTests`가 잡는 자리인데 꺼져 있다"*라고 적었으나 **틀렸다** —
  그 감사는 주석 속 **`.cs` 경로**만 보고 **식별자 참조는 어느 축에도 없다**(§6 자기 정정). 이건 감사의
  방치가 아니라 **없는 축**이다.

---

## 6. Assert.Ignore 명부 — 전수 90건과 **언제부터**인가

「언제부터」는 **결과 xml 68판(total ≥ 100인 전량 실행만)에서 그 이름이 처음 Skipped로 나온 실행**으로 쟀다.
부분 실행(필터)은 「없다」의 근거가 못 되므로 뺐다.

### 잊힐 위험이 있는 오래된 것 — 3일 (09-02부터)

| 처음 건너뜀 | 테스트 | 사유 요지 | 되살림 장치 |
|---|---|---|---|
| 09-02 02:39 | `AppearanceShapeBudgetTests.PET 커서친구` | 정원 1개/보조색 0개(2~4·1이어야) | 명부 등록됨 |
| 09-02 02:39 | `AppearanceShapeBudgetTests.최단_실제_변_검사를…30종으로_확장한다` | 위반 9건 실재, 래칫 상한 14 이하 | 상한 래칫 |
| 09-02 02:39 | `ComicFontFloorOutlineRingTests.배율1에서는…보류` | 배율 1.00에서 속공간 0.411물리픽셀 | 명부 등록됨 |
| 09-02 02:39 | `PlatformParityAuditTests.미해결_macOS_Dock_자동숨김…` | 판정 끝, **실행 보류** | 자기 대장 |
| 09-02 02:39 | `…실기미확인_매달리기가_Windows_작업표시줄에서…` | 실기 하드웨어 필요 | 자기 대장 |
| 09-02 02:39 | `…실기미확인_착지티어_교차배율이_Windows에서…` | 실기 하드웨어 필요 | 자기 대장 |
| 09-02 02:39 | `…실기미확인_획_하한의_월드_환산이_Windows_DPI에서…` | 실기 하드웨어 필요 | 자기 대장 |
| 09-02 04:21 | `…미해결_모바일_스크린샷_백드롭_모드가…` | 4플랫폼 목표의 모바일 축 미배선 | 자기 대장 |
| **09-02 07:46** | **`CommentReferenceAuditTests.이미_알려진_깨진_참조가_고쳐졌으면_명부에서_지운다`** | **거짓 참조 3건 미수정** | R1 래칫 |
| 09-02 10:27 | `FacingFlipBodySplitTests.무릎앉아_착지는_실기_미재현이라_보류한다` | debugger 실기 재현 실패 | 명부 등록됨 |
| 09-02 10:27 | `…미해결_Windows에는_z밴드_필터가_없어_잠금화면_유령발판…` | Windows 미구현 | 자기 대장 |
| 09-02 12:37 | `…실기미확인_세션잠금_게이트가_Windows_실기에서…` | 실기 하드웨어 필요 | 자기 대장 |
| 09-02 16:59 | `ReservedScreenEdgeContractTests.팝오버와_정보창의_가로축은…미해결` | 가로축 잔여 2곳 | 자기 대장 |
| 09-03 01:28 | `…미해결_하이브리드GPU_선택이_macOS에는…` | 착수 미배정 | 자기 대장 |
| 09-03 03:44 | `…미해결_상시표시영역_아이콘이_macOS에는_없다` | NSStatusItem 네이티브 플러그인 필요 | 자기 대장 |

★ **★★ 자기 정정 (20:5x, 리더 지시로 재확인하다 발견) — 위 문단의 앞 판이 틀렸다.**

원래 이렇게 적었다: *"`CommentReferenceAuditTests`가 3일째 꺼져 있고, 그 사이 태어난 죽은 참조가
`strokeGrade`다."* **두 절 다 거짓이다.** 실측:

| 확인한 것 | 실제 |
|---|---|
| 이 감사가 꺼져 있는가 | ❌ **7건 중 6건이 살아서 초록**이다(`card-r19c` 실측). 꺼진 것은 **명부 만료 검사 1건**뿐 |
| 그 1건조차 아무것도 안 재는가 | ❌ `Assert.Ignore`가 **실제 단언들 뒤**에 있다 — 「고쳐졌는데 명부에 남음」은 **여전히 잡는다** |
| `strokeGrade`가 그 결과인가 | ❌ **사정거리 밖**이다. 이 감사의 정규식은 `.cs` 경로·문서 경로·`Foo.cs:123` 뿐이고,<br>`<c>strokeGrade</c>` 같은 **식별자 참조는 어느 축에도 없다** |

⇒ **감사는 제 일을 하고 있었고, 내가 그것을 「꺼져 있다」로 읽었다.** 이 저장소의 표준 병(«실패한
측정과 성공한 측정이 똑같이 생겼다»)을 **감사 자신이 아니라 내가** 저지른 형태다 — 러너 표의
`Skipped` 한 글자만 보고 그 안의 단언 순서를 안 읽었다.

**남는 사실 두 개는 그대로 유효하다:**
1. **`KnownBroken` 3건은 전부 프로덕션 파일에 있다** — 내 권한 밖이라 못 고친다(§8 참조).
2. ★ **진짜 구멍은 다른 것이다 — 「주석 속 식별자 참조」를 재는 축이 저장소에 없다.**
   `strokeGrade`가 그 증거다(저장소 전체 1건 = 그 주석 자신). `<see cref=...>`는 컴파일러가
   경고라도 내지만 `<c>X</c>`는 **아무도 안 본다.** 이건 신설 제안이지 회귀가 아니다(§8-6).

### 어제~오늘 (09-04 ~ 09-05)

| 처음 건너뜀 | 테스트 | 사유 |
|---|---|---|
| 09-04 20:37 | `…미해결_시스템_오디오_감지는_Windows_실기와_푸시_배선이_미확인이다` | 부분 착지 |
| 09-05 00:32 | `…미해결_스토어_제출물_결손_3건이_남아_있다` | M-5, 재료·판정 대기 |
| **09-05 15:49** | **인계본 계약 v2 — 66건** | **§2 (명부 미등록)** |
| 09-05 16:06 | `EyesVisorOpacityTests.가리개_채움이_눈_자리를_덮는다(1)`·`모든_가리개가…(1)` | 투명 렌즈 L-4 — **명부 등록됨** |
| 09-05 16:09 | 같은 검사 `(3)` 2건 | 같음 — **명부 등록됨** |

**나이 분포**: 3일 15건 · 2일 3건(위 표에 포함) · 1일 이하 **72건**(그중 66건이 §2).

---

## 7. 이 라운드가 확인한 **좋았던 것** (되돌리지 마라)

정직성 규칙상 이것도 함께 적는다. 오늘 `coder`가 세운 본이 세 개 있다.

1. **`HandoffRarityAbsenceTests`가 CLAUDE.md 니들 규칙을 교과서대로 지켰다** —
   부재 단언(`조각이 등급색을 참조하지 않는다`) + **양성 대조**(`재질색 자리에 등급색을 넣으면 비교기가 잡는다`)
   + **존재 대조**(`등급색은 카드 크롬에 살아 있고 재질색과 겹치지 않는다`) **3종 세트**.
   이것이 §4-B에서 「죽은 부재 니들 0건」이 나온 이유다.
2. **`UiInteractionFramePacingHoldTests:461`의 `nameof` 전환** — 옛 니들 `"_agent.IsSuspended"`를
   존재 단언에서 걷고 `nameof(StickmanAgent.ArePanelsSuppressed)`로 바꾸되, **옛 이름을 부재 단언으로 남겼다.**
   *«좁은 판정으로 되돌아가면 그것도 회귀다»* — 개명 대응의 정본 형태다.
3. **`ByItem()`의 `Assert.AreEqual(16, byItem.Count)`** — 새 골든 대조가 통째로 공허해지는 문(형태 #5)을 처음부터 막았다.

---

## 8. 리더에게 — 승인·판단이 필요한 것

| # | 요청 | 사유 |
|---|---|---|
| 1 | **PlayMode 전량 1회**(락 조율 필요, ~23분) | §3 — 124건이 계약 v2를 못 봤다. **EditMode가 구조적으로 못 보는 축**이다 |
| 2 | `Tools/CrossCompile/xcheck.sh win` + `osx` 1회 | §5-D — Windows 신규 301줄 미컴파일. **락과 무관** |
| 3 | **§2 래칫 우회를 커밋 전에 막을 것인가** | 지금 커밋하면 «66건이 꺼졌는데 감사는 초록»이 **정상 상태로 굳는다** |
| 4 | `CommentReferenceAuditTests` 담당 배정 | §6 — 3일 방치, 그 사이 죽은 참조가 새로 태어났다(`strokeGrade`) |
| 5 | §5-A 빨강 1건의 방향 결정 | 「끄는 게 맞다」인지 「고쳐야 한다」인지 |

---

## 9. Windows 영향 / macOS 영향

- **Windows 영향: 별도 배정 필요.** 이유 셋 —
  (가) 오늘 오후 러너 4판이 **전부 macOS 타깃**이라 `#if UNITY_STANDALONE_WIN` 코드는 한 번도 컴파일되지 않았다.
  (나) `WindowsVirtualDesktopProbe.cs` **신규 301줄**이 마지막 크로스컴파일(06:01)보다 **뒤(09:03)**에 태어나
       어떤 컴파일러도 통과한 적이 없다.
  (다) §4-C 형제 우회 16건 중 **6건이 Windows 전용 표면**(`DwmSetWindowAttribute` · `IsBorderless` ·
       `SetWindowDesktopId` · `IAudioClient` · `_setResolutionCalls` · `_windowResizeCalls`)이고,
       이들은 **macOS 타깃 러너가 원리상 검증할 수 없다** — 소스 텍스트 감사만이 유일한 방어선이다.
  → 처방은 §8-2 한 줄(`xcheck.sh win`)이고 Unity 락과 무관하다.
- **macOS 영향: 함께 검토함.** 오늘 실행 4판이 macOS 타깃이므로 §1·§5의 모든 수치가 macOS 실측이다.
  macOS 전용 회귀는 **새로 발견되지 않았다.** `PlatformParityAuditTests`의 macOS 갭 2건
  (`Dock 자동숨김 판단`·`상시표시영역 아이콘`)은 09-02/09-03부터 「건너뜀」 그대로이고 **악화되지 않았다.**
- **모바일(iPad/iPhone) 영향: 없음.** 계약 v2는 형상 데이터 계층이라 백드롭 모드와 무관하다.
  단 `PlatformParityAuditTests.미해결_모바일_스크린샷_백드롭_모드가…`는 **09-02부터 건너뜀 유지**(4플랫폼 목표의 미착수 축).

---

## 부록 — 재현 절차

```bash
# 락 (TEAM.md 3회차 정본)
ls Temp/UnityLockfile ; ps -ax | grep "Unity.app/Contents/MacOS/Unity" | grep -v grep
ps -ax | grep -c zsh                                   # 양성 대조

# 실행 대장 재생성 + 자기검사
python3 docs/verify/baseline.py --check && python3 docs/verify/baseline.py
python3 docs/verify/renames.py --check                 # 적용 6 / 거부 0

# 래칫 우회 (§2)
grep -rn --include='*.cs' 'HandoffTestGate.SkipIfHandoff' Assets/_Project/Scripts/Tests/ | wc -l   # 30
grep -n  'HandoffTestGate\|SkipIfHandoff' Assets/_Project/Scripts/Tests/EditMode/TestClaimExpiryAuditTests.cs  # 0

# 형제 이름 우회 (§4-C)
git show HEAD:Assets/_Project/Scripts/Core/StickConfig.cs | grep -c MinAccessoryStrokeScreenPoints  # 0
grep -c MinAccessoryStrokeScreenPoints Assets/_Project/Scripts/Core/StickConfig.cs                  # 1

# 거짓 통과 스캐너 (교정 먼저)
python3 Tools/FalsePassScan/falsepass.py --selftest                      # 교정 14종 통과
python3 Tools/FalsePassScan/falsepass.py Assets/_Project/Scripts/Tests --since-mtime "2026-09-05 09:00"
```

★ `grep --include`는 **반드시 따옴표**로 감싼다(`--include='*.cs'`). zsh 글롭이 먹으면
**양성 대조까지 0건**이 되어 「깨끗함」과 구별되지 않는다 — 이 라운드에서 실제로 한 번 당했다.


---

# 부록 A — 21:0x 후속 라운드 (리더 배정 3건 처리)

## A-1. 🔴 PlayMode 전량 = **측정 실패.** 숫자를 하나도 보고하지 않는다

| | |
|---|---|
| 시작 | 16:32 (`regress.sh play qa-r11`) |
| 로그가 마지막으로 자란 시각 | **19:56** (3시간 24분) |
| **결과 xml** | **없음** (`qa-r11_play.xml` 미생성) |
| 진행률 | 테스트 셋업 **476 / 641 = 74%** |
| 속도 | **25.7초/테스트** — 기준 `qa-r10_play`의 2.2초/테스트 대비 **11.7배 느림** |

★ **판정: 무효.** TEAM.md 락 프로브 정본 —
*"판정은 프로브가 아니라 산출물로 한다 — 결과 xml이 실제로 생성됐는지 먼저 보라."*
xml이 없으므로 **PlayMode에 대해서는 아무 숫자도 말할 자격이 없다.** §3의 미측정 124건은 **여전히 미측정**이다.

### 락 충돌 — 리더 질문에 대한 답: **있었고, 피해자는 내가 아니라 coder다**

| 확인 | 실측 |
|---|---|
| 내 로그의 `another Unity instance` | **0건** |
| 내 로그의 `Fatal Error` / `Aborting batchmode` / `compiler errors` | **각 0건** |
| 16:32~20:00 사이 `runs/`에 쓰인 파일 | **내 로그 1개뿐** |
| coder의 EditMode 산출물 | `card-r20` 20:33 · `esc-gate` 20:39 · `n1-dy` 20:57 — **전부 내 로그가 죽은 19:56 이후** |

⇒ **내가 `Library/` 락을 3시간 24분 붙잡고 있었고, 그동안 coder의 EditMode는 xml을 하나도 못 남겼다.**
그리고 **나는 아무것도 못 얻었다.** 순손실이다.

### 왜 11.7배 느렸는가 — **미확인.** 가설 2개를 구분하지 못했다

1. **CPU 경합** — 내 PlayMode + coder의 반복 EditMode 시도가 같은 8코어를 나눠 썼다.
2. **계약 v2로 PlayMode가 실제로 무거워졌다** — 조각 수·알파 합성·층이 늘었다.

가르려면 **락을 독점한 상태에서 1회** 재보면 된다. 그 전까지는 «느려졌다»의 원인을 말하지 않는다.

### 재시도 제안 (리더 판단 요청)
- **직렬 슬롯**을 하나 잡아 달라 — coder가 러너를 쓰지 않는 창. 지금처럼 6분 간격으로 EditMode가 도는 동안에는 또 같은 일이 난다.
- 그리고 **백그라운드 셸로 띄우지 않는다.** 이번 실패의 직접 원인 중 하나는 내 백그라운드 작업이 회수되면서 자식 Unity가 함께 죽은 것이다(로그에 종료 사유가 없는 이유).

## A-2. 🟢 P0 래칫 우회 — **막았다.** 음성 대조 2종으로 증명

`TestClaimExpiryAuditTests.cs`에 **간접 Ignore 게이트** 축을 신설했다(+241줄).

**설계의 핵심은 「이름을 먼저 적지 않는 것」이다.** 게이트 이름을 명부에 적어 두고 그것만 찾으면
**다음 헬퍼는 또 조용히 지나간다**(니들이 대상보다 늦는 병 — §4-C에서 잰 바로 그 형태).
그래서 **실물에서 먼저 찾는다**: 「테스트 메서드 **바깥**에 `Assert.Ignore(`가 있는 파일」을 전수하고
명부와 **양방향**으로 맞댄다.

- 탐지기는 위 감사와 **같은 자**(`TestMethods`)를 쓴다 — 그래야 「위가 못 본 것」이 정확히 여기로 넘어온다.
- 게이트마다 **되살림 장치**·**사유**가 비면 실패한다.
- **호출부 수(30)가 래칫**이다 — 늘어도(더 껐다) 줄어도(되살아났다) 빨개진다.
- **호출부마다 사유 문자열**을 요구한다(호출식 끝까지 최대 4줄을 본다 — 1곳이 줄바꿈해서 넘긴다).
- 게이트 메서드 이름이 **선언으로 실재하는지** 먼저 잰다 — 개명되면 호출부가 조용히 0이 되고
  「아무도 안 끈다」로 읽히는 것을 막는다.

### 실행 대조 — 초록 → 빨강 → 빨강 → 초록

| 실행 | 상태 | total | 실패 | 무엇을 증명했나 |
|---|---|---:|---:|---|
| `qa-r12` 21:00 | 기준 | 2059 | **0** | 신설 2건이 통과 |
| `qa-r12-M1-EXPECTED-RED` 21:01 | **M1** 명부에서 게이트를 지움 | 2059 | **1** | ★ **양방향 동시 발화** — 「`HandoffTestGate.cs` 미등록」 + 「명부 항목 자동 만료」 |
| `qa-r12-M2-EXPECTED-RED` 21:03 | **M2** `CallSites` 30→29 | 2059 | **1** | ★ 래칫 발화 — `Expected: 29 / But was: 30` |
| `qa-r12-RESTORED-GREEN` 21:04 | 원복 | 2059 | **0** | 돌연변이 잔존 0 |
| `qa-r12b` 21:07 | 최종(숫자 정정 반영) | 2059 | **0** | 현재 트리 |

두 돌연변이 모두 **정확히 1건**만 빨개졌다 — 부수 피해 없음.
그리고 `대조_간접_게이트_탐지기가_바깥과_안을_실제로_가른다`가 **러너 안에서** 알려진 값 4개로 탐지기를 매 실행 교정한다.

### ★ 자기 정정 — 66이 아니라 **69**다
본문 §2에 「66건」이라고 적었는데 **틀렸다.** 내 파이썬 집계가 `fullname`을 dict 키로 써서
**같은 이름의 파라미터 케이스가 합쳐졌다**(`card-r17`에 중복 이름 24종). 사유 문자열로 다시 세면
**69건**이고, `card-r17`(15:49)부터 지금까지 **69로 고정**이다. 코드 주석·실패 메시지도 69로 고쳤다.
⇒ **집계 도구가 중복 이름을 삼키는 것 자체가 이 저장소의 표준 병**이다(자기 자백).

## A-3. 🟡 `CommentReferenceAuditTests` — 내가 고칠 수 없다(권한 밖)

§6 자기 정정대로 **이 감사는 꺼져 있지 않다**(7건 중 6건 초록). 남은 `KnownBroken` 3건은
**전부 프로덕션 파일**에 있어 내 권한(테스트만 수정) 밖이다. **3줄이면 끝나는 수정**이라 그대로 넘긴다:

| 파일:줄 | 지금 문구 | 고쳐야 할 것 |
|---|---|---|
| `Platform/VisibleTopEdgeSolver.cs:25` | `Tests/EditMode/VisibleTopEdgeSolverTests.cs` | `Tests/EditMode/VisibleTopEdgeOcclusionTests.cs` |
| `States/IdleState.cs:59` · `States/WalkState.cs:60` | `States/IPlannedDwellSource.cs` | `States/IMovementIntentSource.cs`(그 안에 선언된 인터페이스다) |
| `Core/CharacterSaveStore.cs:439` | `Tests/*/GlobalTestIsolation.cs` | `Tests/EditMode/GlobalEditModeTestIsolation.cs` · `Tests/PlayMode/GlobalPlayModeTestIsolation.cs` |

셋을 고치면 `이미_알려진_깨진_참조가_고쳐졌으면_명부에서_지운다`가 **빨개진다**(설계대로 — 「축하할 실패」).
그때 `KnownBroken`을 비우면 그 검사도 초록으로 살아난다.

★ **진짜 구멍은 따로 있다(신설 제안, 이번 라운드 미착수)**: 주석 속 **식별자 참조**를 재는 축이 없다.
`strokeGrade`가 그 증거다. 다만 `<c>float[]</c>` 같은 정상 용례가 많아 **오탐 설계가 본체**이고,
coder가 계약 v2를 쓰는 중에 새 게이트를 켜면 무관한 빨강이 쏟아진다 — **다음 라운드 과제로 남긴다.**

## A-4. 🟡 내 락 프로브가 자기 자신을 세고 있었다 (자기 자백)

`ps -ax | grep <문자열>`은 **그 명령을 감싼 `zsh -c`의 인자에 그 문자열이 들어 있어 자기 자신을 잡는다.**
실측: 존재할 리 없는 이름으로 음성 대조를 했더니 **0이 아니라 3**이 나왔다.

TEAM.md 정본(`ps -ax | grep "Unity.app/..." | grep -v grep`)이 지금까지 안 터진 이유는
**`grep -v grep`이 그 zsh 줄에 들어 있는 "grep"까지 우연히 걸러 냈기 때문**이다 — 설계가 아니라 운이다.

**구조적으로 안전한 형태(4회차 제안):**
```bash
ps -axo comm= | grep -c 'Unity.app/Contents/MacOS/Unity'   # comm 은 인자를 안 담는다 = 자기매칭 불가
ps -axo comm= | grep -c 'ZzzNoSuchProcZzz'                 # 음성 대조 -> 반드시 0
ps -axo comm= | grep -c 'zsh'                              # 양성 대조 -> >0
```
★ `regress.sh`의 G6는 **이미 comm 기반**이라 이 문제가 없다(215~231행). 병든 것은 **손으로 치는 프로브**다.
