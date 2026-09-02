# 영어 출시 1.0 — 로컬라이제이션 구현 계획

`localization` / 2026-09-02 / 사용자 확정 *"영어 출시 1.0 넣어야함"* 이후 첫 라운드
검산 스크립트: `docs/localization/verify/{census,ship,gate,dryrun,golden_gen}.py` · 원장: `docs/localization/DEBT_BASELINE.tsv`

> **상태(2026-09-02 갱신)** — 리더가 계획을 승인하고 **L0(게이트) 구현을 이 담당에게 배정**했다.
> **§9에 구현 기록이 있다.** 계획 본문(§1~§8)은 계획 시점 그대로 둔다 — 나중에 "무엇을 예측했고
> 무엇이 실제로 일어났는가"를 대조할 수 있어야 한다.

---

## 0. 리더가 먼저 읽을 12줄

1. **규모를 다시 쟀다. 정의서의 "~1,073건"도, `product-strategy`의 "7,968건"도 둘 다 아니다.**
   **출하되는 한국어 문자열은 `.cs` 401건 + `.asset` 84건 = 485건**(고유 340 + 84). 파일 77개.
2. `grep`은 지금 **8,270건**을 낸다(3시간 전 7,968 → +302). **2.1배 과대다** — 주석 안의 인용 부호까지 센다.
   실제 한국어 **문자열 리터럴**은 3,972건이고 그중 **3,571건(90%)이 출하되지 않는다**(인스펙터 1,662 / 로그 1,185 / 로그메서드 225 / 로그파일 478 / 예외 21).
3. `StickConfig.cs` **1,637건은 전수(1,637/1,637)가 `[Tooltip]`/`[Header]`다.** 정의서 3번 항목 **확인**.
4. `.asset` 42개 **84건**: raw `grep '[가-힣]'` = **0건**, 디코드 후 = **84건**. 정의서 2번 항목 **재현 확인**(양성 대조 붙임).
5. `KoreanParticle` 프로덕션 호출부 **4곳 전부 `Debug.Log`** — 전수 확인. **조사 함수에 언어 분기를 넣을 이유가 없다.** 정의서 4번 항목 **확인**.
6. ★ **게이트는 "언어"를 알 필요가 없다. "그 문자열의 문자체계"만 알면 된다.**
   그래서 게이트 수정은 **언어 인프라(로케일·설정·저장)에 의존하지 않는다.** 오늘 바로 착수 가능하고, `ReadingSeconds`의 **순수 함수 성질도 그대로 보존**된다.
7. ★ **게이트가 먼저다.** 테이블이 먼저 오면 `design-narrative`가 **깨진 게이트를 분모로** 영어를 쓰게 된다 — 교정된 Walk 예산이 라틴 **11자**가 되어 그건 영어가 아니라 전보문이다.
8. ★★ **새 결함 1건 — 영어 풀 24줄이 `design-motion` 교정 분모에서 전부 통과하지 않는다.**
   `design-narrative`의 "탈락 0줄"은 분모 2.00/1.50 기준이다. 교정 분모 1.65/**1.24**로 다시 풀면
   **여유 요구 0.18초(§7-3 팝인 마진)에서 Walk 영어 5줄 탈락, 마진 없이도 1줄(`Stretching the legs.` 20자) 탈락**한다.
   **한국어는 같은 조건에서 0줄 탈락**이다 — 정확히 "영어만 조용히 죽는" 그 형태다.
9. 한국어 비트 불변은 **현행 대사 33줄 전수에서 최대 차이 0.0**으로 확인했다(양성 대조 4종 포함).
   단 **딱 한 곳에서 한국어 사용자도 값이 바뀐다**: 할일 리마인더 대사는 **사용자가 직접 입력한 문자열**이라, 영어로 적은 할일은 예산이 줄어든다. **그게 이번 수정의 의도된 결과다**(§2-7).
10. 문자열 테이블은 **`StickMateDisplayNames`의 어법을 확장**한다(새 런타임 로더 도입 없음). 다만 그 파일의 `BuildBusyTexts`가 쓰는 **문자열 연결(concat) 조립은 폐기**해야 한다 — 어순이 언어마다 다르다. **연결 조각 의심 113건 / 보간 포함 35건**이 그 대상이다.
11. **UI 문자열과 대사는 저장소를 분리한다.** 대사는 번역이 아니라 재창작이고 **풀 길이가 언어마다 다를 수 있어서**, 인덱스를 뽑는 코드가 활성 풀을 봐야 한다(§3-4).
12. **언어 설정은 따로 올리지 않는다.** `coder-systems`의 다음 스키마 묶음에 `languageSaved`/`languageName` 2필드로 실으면 되고, **버전 분기가 필요 없는 형태**다(§3-6).

---

## 1. 규모 — 직접 셌다

### 1-1. 방법과 양성 대조

`grep`으로는 못 센다. 두 가지가 동시에 틀린다.

| 함정 | 증상 | 대응 |
|---|---|---|
| `.asset`의 `\uXXXX` 이스케이프 | `grep '[가-힣]'`이 **영원히 0건** | **디코드한 뒤** 센다 |
| 주석·XML doc 안의 인용 부호 | `grep -o '"[^"]*[가-힣][^"]*"'`가 주석 조각까지 센다 | **C# 어휘 분석**으로 주석/문자열을 가른다 |

`docs/localization/verify/census.py --selftest` — **10/10 통과**:

```
  PASS  주석 안 한글은 리터럴로 세지 않는다
  PASS  [Tooltip]/[Header] 2건 = INSPECTOR
  PASS  Debug.Log/LogWarning 2건 = DEBUG
  PASS  일반 리터럴 4건(일반/보간/축자/이스케이프) = OTHER
  PASS  \uXXXX 이스케이프가 디코드된다
  PASS  영어 전용 리터럴은 안 잡힌다
  PASS  생(raw) 바이트에서 grep [가-힣]은 0건 = 이것이 함정이다      ← ★ 0건 판정의 양성 대조
  PASS  디코드하면 같은 파일에서 2건이 나온다                        ← ★ 그 짝
  PASS  주입한 한글 리터럴 1건을 찾는다                              ← ★ 스캐너가 실제로 세는가
  PASS  그리고 OTHER로 분류된다
```

`docs/localization/verify/ship.py --selftest` — **10/10 통과**(분류기 자체의 양성/음성 대조).

### 1-2. 결과

| 분류 | 건수 | 출하? |
|---|--:|---|
| `INSPECTOR` — `[Tooltip]`/`[Header]`/`[CreateAssetMenu]` 등 | **1,662** | ✗ 인스펙터 전용 |
| `DEBUG` — `Debug.Log*` 직접 인자 | **1,185** | ✗ |
| `LOGM` — 로그 조립 메서드 안 (`sb.Append` → `Debug.Log`, `Build*Diagnostic()`) | **225** | ✗ |
| `LOGF` — 화면 싱크가 **파일 전체에 하나도 없는** 파일 | **478** | ✗ |
| `EXC` — `throw new *Exception("…")` | **21** | ✗ 개발자용 |
| ★ **`SHIP`** | **401** | **✓ 번역 대상** |
| (합계) | **3,972** | |
| ★ `.asset` `displayName`/`description`(디코드 후) | **84** | **✓** |
| ★★ **1.0 번역 대상 합계** | **485** | (고유 340 + 84) |

**화면 싱크의 정의**(추측이 아니라 실측한 3종):

1. `\.text =` — uGUI 텍스트 대입
2. `DialogueIntent` / `DialogueLine.Say|React` / `new TimedSpectacleState` — 말풍선 파이프라인
3. `CommandAvailability.Blocked(reason)` — `Reason` 필드 문서가 스스로 *"불가/부재일 때 **사용자에게 그대로 보여줄** 한 줄"*이라고 적어 두었다(`Core/CommandAvailability.cs`)

### 1-3. 파일별 (상위)

| 건수 | 파일 | 성격 |
|--:|---|---|
| 70 | `Interaction/SettingsWindow.cs` | 설정창 전체 |
| 50 | `Interaction/CharacterInfoWindow.cs` | 정보창 골격 |
| 49 | `Core/StickMateDisplayNames.cs` | ★ 이미 문자열 테이블 |
| 29 | `Core/ItemCatalog.cs` | 행동 12종의 이름+설명(**마케팅급 문안**) |
| 28 | `Interaction/ActionCommandPopover.cs` | 행동 명령창 |
| 25 | `Interaction/FocusSessionPopover.cs` | 집중 모드 |
| 18 | `Interaction/GearRadialMenuWidget.cs` / `Interaction/TodoBoardPopover.cs` | |
| 13 | `Dialogue/AmbientChatter.cs` | ★ 대사 |
| 13 | `Dialogue/DialogueBubbleRenderer.cs` | 일부는 진단 잔재(§1-4) |
| … | (전 77파일은 `DEBT_BASELINE.tsv`) | |

| 축 | 건수 |
|---|--:|
| **대사(말풍선)** | **36** = 대사 34건(고유 **33**, `"어... 알았어, 갈게"`가 2곳) + 발판 리포트 라벨 2건 |
| **UI/데이터** | **365** |
| 중괄호 보간을 포함 = **템플릿화 필수** | **35** |
| 4자 이하 = **연결(concat) 조립 의심** | **113** |

### 1-4. 남아 있는 과대/과소 (정직하게)

- **과대 추정 ≤ 20건**: `Dialogue/DialogueBubbleRenderer.cs`의 13건 중 상당수(`"지정됨"`, `"미지정(모든 대사 수신)"`)는
  진단 라벨이다. 파일에 싱크가 있어 `SHIP`으로 남았다. **안전측으로 남겨 뒀다** — 번역 대상을 빠뜨리는 것보다 낫다.
- **과소 추정 위험**: `LOGF` 478건은 "파일 전체에 싱크가 없다"로 배제했다. 어떤 파일이 나중에 문자열을
  UI로 넘기기 시작하면 이 배제가 조용히 틀린다. → **§4의 감사가 이 경계를 매 라운드 다시 잰다.**
- **`Tests/` 232파일은 세지 않았다.** 출하되지 않는다.

---

## 2. ★ 1순위 — 발화 게이트 단위 오류

### 2-1. 무엇이 틀렸는가 (독립 재계산으로 확인)

`docs/localization/verify/gate.py` 교정 먼저 — **깨지면 그 아래 숫자를 전부 폐기한다**:

```
PASS  10자 -> 1.090   PASS  8자 -> 0.940   PASS  7자 -> 0.865
```

반증 재현:

```
헉... 높다        음절3 자 7  필요체류 0.865 | Whoa, that's high   음절3 자17  현행 1.615 -> 개정 1.142  (분모 1.20)
    현행: 침묵 / 개정: 발화
어우... 꽤 깊네   음절5 자10  필요체류 1.090 | Whoa... that's deep 음절3 자19  현행 1.765 -> 개정 1.237  (분모 1.12)
    현행: 침묵 / 개정: 침묵  ← 게이트 수정은 필요조건이지 충분조건이 아니다
```

`PerGlyphSeconds = 0.075f`는 실제로 **초/음절**인데 `int glyphs = text.Length`가 **초/글자**로 읽는다.
한글에서만 글자 = 음절이라 지금까지 안 들켰다.

### 2-2. ★★ 언어를 어떻게 아는가 — **알 필요가 없다**

리더가 "이게 핵심"이라고 짚은 지점이다. **답은 로케일이 아니다.**

> **가독예산은 "이 사용자의 언어"가 아니라 **"내가 지금 받은 이 문자열의 문자체계"**의 함수다.**

근거 셋:

1. **`ReadingSeconds`는 순수 함수라고 스스로 문서에 적어 두었다** — *"순수 함수다(시간/난수/전역 상태를 읽지 않는다)"*.
   여기에 로케일(전역 상태)을 주입하면 그 계약을 우리가 먼저 깬다. 그리고 이 저장소는
   **"받을 자리를 만들어 두면 언젠가 누가 채운다"**를 이미 한 번 명시적으로 거부했다
   (`RequiredDwellSeconds`가 노출 배율을 **인자로도 받지 않는** 이유). 같은 판단을 여기 적용한다.
2. **혼합 문자열이 실재한다** — `"Wi-Fi 끊겼네"`. 로케일 기준으로 갈랐다면 이 줄은 라틴 계수를 맞아
   **과소 청구**된다(= 조기 소멸). 문자열 기준으로 갈랐으면 **비싼 쪽**으로 간다(= 침묵. 침묵은 거짓말이 아니다).
3. **사용자 입력 대사가 실재한다** — 할일 리마인더는 `TodoListModel.ConsumePendingReminderText`가
   **사용자가 타이핑한 원문**을 그대로 말풍선에 싣는다(`TodoReminderDirector.cs:62/126`).
   한국어 UI를 쓰는 사용자가 영어로 적은 할일이 **로케일=Korean이라는 이유로 0.075/글자를 청구받으면 침묵한다.**
   문자열 기준이면 정확히 옳게 동작한다.

**결과: 게이트 수정은 로케일·설정·저장 스키마 어느 것도 기다리지 않는다. 오늘 착수 가능하다.**

> 로케일(§3-6)은 **문자열 테이블이 어느 배열을 고르는가**에만 쓰인다. 두 축을 절대 섞지 마라.

### 2-3. `coder`가 바로 착수할 수 있는 형태

**파일: `Assets/_Project/Scripts/Dialogue/DialogueKind.cs` — `DialogueBudget` 한 클래스, 함수 하나.**

```csharp
/// <summary>한 글자당 추가 가독 시간(초) — <b>글자 하나가 곧 음절 하나</b>인 문자체계용
/// (한글 음절 / CJK 통합한자 / 가나). 이 값은 원래부터 "초/음절"이었다.</summary>
private const float PerGlyphSeconds = 0.075f;

/// <summary>라틴 글자 한 자당 추가 가독 시간(초) = <b>0.0472</b>.
/// 고른 값이 아니라 <c>0.28 + w·G_en = 0.28 + 0.075·G_kr</c>를 실재 대사 17줄 + 그 영어
/// 재창작으로 푼 결과다(한글 112자 / 영어 178자 = 0.629 × 0.075).
/// 근거: design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md §5-3,
///       design/narrative/verify/en_budget.out.txt, docs/localization/verify/gate.out.txt.
/// ★ "안전하게" 더 낮추지 마라 — 침묵은 줄지만 <b>글자가 읽기 전에 사라진다</b>. 규칙 8이
///   없애려던 결함 그 자체다. 이 저장소에서 안전한 실패는 침묵이지 조기 소멸이 아니다.</summary>
internal const float PerLatinGlyphSeconds = 0.0472f;

/// <summary>이 문자열에 <b>글자=음절</b>인 문자체계가 하나라도 섞였는가(순수 함수).
/// 섞였으면 비싼 쪽으로 청구한다 — 과다 청구의 결과는 침묵이고, 침묵은 거짓말이 아니다.
/// <para>★ 로케일을 보지 않는다. 이 판정의 대상은 <b>사용자의 언어</b>가 아니라
/// <b>이 문자열</b>이다(사용자가 직접 입력한 할일 대사가 이 파이프라인에 실린다).</para></summary>
internal static bool IsSyllabicScript(string text)
{
    if (string.IsNullOrEmpty(text)) return true;   // 빈 문자열은 어차피 MinSeconds로 잘린다
    for (int i = 0; i < text.Length; i++)
    {
        char c = text[i];
        if (c >= '가' && c <= '힣') return true;                 // 한글 음절
        if (c >= 'ᄀ' && c <= 'ᇿ') return true;                 // 한글 자모
        if (c >= '㄰' && c <= '㆏') return true;                 // 호환 자모
        if (c >= '぀' && c <= 'ヿ') return true;                 // 히라가나/가타카나
        if (c >= '一' && c <= '鿿') return true;                 // CJK 통합한자
        if (c >= '㐀' && c <= '䶿') return true;                 // CJK 확장 A
    }
    return false;
}

public static float ReadingSeconds(string text)
{
    int glyphs = string.IsNullOrEmpty(text) ? 0 : text.Length;
    float perGlyph = IsSyllabicScript(text) ? PerGlyphSeconds : PerLatinGlyphSeconds;
    return Mathf.Clamp(BaseSeconds + glyphs * perGlyph, MinSeconds, MaxSeconds);
}
```

**바뀌지 않는 것(하나도 건드리지 마라)**: `BaseSeconds` 0.28 / `MinSeconds` 0.62 / `MaxSeconds` 2.20 /
`ReadsBeforeStale` 2 / `MinVisibleScale` / `MaxVisibleScale` / `RequiredDwellSeconds`의 형태 /
`IsEligible`의 형태 / `CanReplaceVisible` / `VisibleIsDoomedByIncoming` / `DialogueTiming` 3상수.

**`internal`인 이유**: 회귀 테스트가 `PerLatinGlyphSeconds`를 **숫자로 베끼지 않고 참조**해야 한다
(CLAUDE.md). `InternalsVisibleTo`는 `Scripts/AssemblyInfo.cs`에 이미 있다.

### 2-4. 파급 — `ReadingSeconds`를 쓰는 세 곳이 전부 함께 언어 인식형이 된다

| 소비자 | 영어에서 어떻게 되는가 | 판정 |
|---|---|---|
| `RequiredDwellSeconds` / `IsEligible` | 필요 체류가 짧아진다 → **영어가 말할 수 있게 된다** | ★ 이번 수정의 목적 |
| `MinVisibleSecondsFor` | 반응 대사의 화면 최소 노출이 짧아진다 | 옳다 — 실제로 더 빨리 읽힌다 |
| `MaxVisibleSecondsFor` | 노출 **상한**도 함께 짧아진다 | 옳다. **"영어가 너무 빨리 사라진다"고 나중에 누가 되돌리지 못하게 못 박아 둔다** |

> 17자 영어 대사가 10자 한국어 대사보다 **짧게** 떠 있는 것은 버그가 아니다.
> 상한 식은 `팝인 + 2·m·가독예산 + 페이드아웃`이고 가독예산은 **읽는 데 걸리는 시간**이다.
> 라틴 17자는 한글 10자보다 빨리 읽힌다. 글자수 대비 단조 비감소 성질은 **각 언어 안에서** 그대로 유지된다.

### 2-5. ★ 한국어 비트 불변을 무엇이 잠그는가

**신규: `Tests/EditMode/DialogueLanguageBudgetTests.cs`** — 잠금 장치 넷을 한 파일에 둔다.

**(1) 골든 동결 — 절대 기준**
`Tests/EditMode/Golden/DialogueBudgetKoGolden.txt` (신규). 한 줄 = `대사<TAB>ReadingSeconds`,
값은 `float.ToString("R")`(왕복 정확). 테스트는 **소스에서 훑은 대사 전량**에 대해 골든과 **완전 일치**를 요구한다.
`Golden/ItemCatalogGolden.txt`가 이미 쓰는 어법이다.

**(2) 소스 스캔 — 표본이 쪼그라들면 실패**
현행 스캐너(`DialogueVisibleScaleContractTests.AllLines()`)는 **26줄만 훑는다**. 실제는 **33줄**이다 —
사각지대 7줄을 여기서 덮는다(전수 확인함):

| 사각지대 | 줄 | 왜 안 잡히나 |
|---|--:|---|
| `Core/StickmanAgent.cs:435~443` | 5 | `States/` 밖 + `cfg => "좋아, 감시 시작"` 람다라 `DialogueLine.Say(` 정규식에 안 걸린다 |
| `States/RunawayState.cs:137,215` | 2 | `TriggerSelfReturn("어... 알았어, 갈게")` |

그리고 `Assert.Greater(lines.Count, 20)`을 **정확한 수 33**으로 바꾼다(§7-4가 지적한 그 구멍 —
지금은 5줄이 사라져도 초록이다).

**(3) 구조 동치 — 하드코딩 없이**
`SourceConstantReader.TryReadFloat("Dialogue/DialogueKind.cs", "PerGlyphSeconds"/"BaseSeconds"/…)`로
상수를 **소스에서 읽어** 구식 `clamp(Base + N·PerGlyph, Min, Max)`를 재구성하고, 33줄 전부에 대해
`DialogueBudget.ReadingSeconds`와 **완전 일치**를 요구한다.
(`SourceConstantReader`는 못 찾으면 `false`를 돌려주므로, 상수 이름이 바뀌면 **조용히 통과하지 않고 실패**한다.)

**(4) 혼합 문자열**
`ReadingSeconds("Wi-Fi 끊겼네")`가 구식 값과 **완전히 같음**. 실측: 현행 0.9550 / 개정 0.9550.

### 2-6. ★ 양성 대조 — 계수를 틀리게 넣으면 **실제로 빨개지는가**

`gate.py`가 이미 넷 다 돌렸다(**전부 PASS**). C# 테스트에 그대로 옮긴다.

| # | 대조 | 실측 결과 |
|---|---|---|
| 1 | **개정 쪽 한국어 계수만** 0.075 → 0.0751로 틀리면 불변 검사가 깨지는가 | **PASS — 33줄 중 28줄이 달라진다** |
| 2 | 분기를 **지우면**(영어도 0.075) 한국어는 그대로인데 영어만 달라지는가 | **PASS — 한국어 동일 / 영어 24/24줄 변화** |
| 3 | 계수를 **고치지 않으면** 영어 풀이 무너지는가 | **PASS — 24줄 중 12줄 탈락** |
| 4 | 계수를 임의로 **낮추면**(0.030) 조기 소멸 방향으로 가는가 | **PASS — 16줄의 예산이 0.15초 이상 감소** |

> ★ #1은 처음에 **FAIL**이었다. `PER_KR`을 전역으로 바꿨더니 **현행 식과 개정 식이 같이 움직여** 차이가 0으로 나왔다 —
> **"실패한 측정과 성공한 측정이 똑같이 생겼다"의 교과서적 형태**다. 개정 쪽만 변이시키도록 고쳐 재측정했다.
> **C# 테스트에도 반드시 이 형태(한쪽만 변이)로 옮겨라.**

### 2-7. ★ 한국어 사용자도 값이 바뀌는 **딱 한 곳** — 숨기지 않는다

`ReadingSeconds`가 인자로 받는 문자열 중 **저작물이 아닌 것**이 하나 있다:
할일 리마인더 대사 = **사용자가 타이핑한 할일 원문**(`TodoReminderDirector.cs:62/126` → `TodoListModel`).

- 순수 라틴 **5자 이상**부터 값이 갈린다(4자 이하는 `MinSeconds` 0.62가 흡수한다).
- 예: 할일 `"Review PR"`(9자) — 현행 0.955초 / 개정 0.705초.
- **이건 회귀가 아니라 이번 수정의 본체다.** 한국어 UI 사용자가 영어로 적은 할일이 지금은 과다 청구를 맞고 침묵한다.
- **골든에는 넣지 않는다**(사용자 데이터라 고정할 수 없다). 대신 **명시적 케이스 테스트 1건**으로 이 동작을 못 박는다.

### 2-8. ★★ 새 결함 — 영어 풀 24줄이 교정 분모에서 전부 통과하지 않는다

리더 브리핑대로 `design-motion` 교정 분모(**Idle 1.65 / Walk 1.24**)를 반영해 다시 풀었다.

| 분모 | 여유 요구 | 한글 탈락 | **영어 탈락** |
|---|--:|--:|--:|
| narrative 2.00/1.50 | 0.18(팝인) | 0 | **0** ← 원 산출물의 판정 |
| **motion 1.65/1.24** | **0.18(팝인, §7-3)** | **0** | ★ **5** |
| **motion 1.65/1.24** | 0(§7-3 미도입) | 0 | ★ **1** |

탈락 목록(motion 분모 + 팝인 마진):

| # | 상태 | English | 자 | 여유 |
|--:|---|---|--:|--:|
| 7 | Walk | `Let's go that way.` | 18 | +0.050 |
| 8 | Walk | `Left, right, left.` | 18 | +0.050 |
| 9 | Walk | `Stretching the legs.` | 20 | **−0.044** ← 마진 없이도 탈락 |
| 10 | Walk | `Good stride today.` | 18 | +0.050 |
| 17 | Walk | `Weekend's close.` | 16 | +0.145 |

**교정 분모에서의 실제 예산**(참고: 리더 브리핑의 27자/19자는 **여유 요구 0** 기준이다):

| 상태 | 분모 | 한글(여유0 / 여유0.18) | 영어(여유0 / 여유0.18) |
|---|--:|---|---|
| Idle | 1.65 | 17자 / 15자 | **27자** / 23자 |
| Walk | **1.24** | 12자 / 9자 | **19자** / **15자** |
| ParkourClimb | 1.20 | 11자 / 9자 | **18자** / 14자 |
| LedgeHang | 1.12 | 10자 / 8자 | **16자** / 12자 |

> **판정이 필요하다(리더).** 셋 중 하나다.
> **(가)** §7-3(팝인 마진)을 **Walk에는 적용하지 않는다** — 그러면 영어 탈락은 #9 한 줄뿐이고 그 줄만 다시 쓰면 된다.
> **(나)** §7-3을 그대로 적용하고 **`design-narrative`가 Walk 영어 5줄을 15자 이하로 재창작**한다
>   (`Left, right.` 12자 / `Nice stride.` 12자 / `Weekend soon.` 13자 급).
> **(다)** `design-motion`이 §3-5에서 제안한 **지형까지 본 잔여**를 게이트에 주면 분모 자체가 바뀐다 — 그때 다시 푼다.
>
> ★ 어느 쪽이든 **한국어는 세 조건 전부에서 탈락 0줄**이다. 이 비대칭 자체가 이번 라운드의 논거다:
> **분모가 조금만 조여도 영어만 먼저 죽고, 증상은 로그 한 줄뿐이다.**

**그리고 게이트 수정만으로는 부족하다**는 `design-narrative` §5-4의 결론도 재확인했다 —
`Whoa... that's deep`(19자 @LedgeHang 1.12)은 개정 후에도 침묵한다.
**영어 대사는 번역이 아니라 재창작이어야 한다.**(문안은 `design-narrative` 소관. 나는 예산만 준다.)

---

## 3. 문자열 테이블 — 구조

### 3-1. ★ UI와 대사는 저장소를 나눈다

| | UI 문자열 (365 + 84) | 대사 (36) |
|---|---|---|
| 관계 | 한↔영 **1:1 대응**. 번역 | **1:1이 아니다.** 재창작이고 줄 수도 다를 수 있다 |
| 원칙 1 | 무관 | **정면으로 걸린다** — 상태에서만 파생 |
| 게이트 | 안 탄다 | **탄다.** 게이트가 언어 인식형이 된 뒤에만 안전 |
| 실패 모드 | 안 보이는 글자 / 잘린 글자 | **침묵**(로그 한 줄뿐, 화면에 흔적 없음) |
| 선행 조건 | 없음 | **§2가 먼저** |

**섞으면**: 같은 인덱스로 두 언어를 뽑는 순간 영어 풀이 한국어 풀과 **같은 길이여야 한다**는 제약이 생기고,
그게 곧 "영어는 번역이다"라는 강제다. 그 강제가 §2-8의 20자 문제를 **구조적으로 못 풀게** 만든다.

### 3-2. UI 테이블 — `StickMateDisplayNames`의 어법을 확장한다

**새 런타임 로더(JSON/CSV/Addressables)를 도입하지 않는다.** 이유 넷:

1. `Core/StickMateDisplayNames.cs`가 이미 정확히 이 물건이다 — **enum → 완성 문장 정적 배열, 무할당**.
   그 파일의 문서가 이유까지 적어 뒀다(*"행동 명령창은 0.25초마다 6개 타일의 가용성을 다시 묻는다 …
   초당 24개의 쓰레기 문자열"*). 24시간 상주 앱에서 이 제약은 그대로다.
2. **키를 컴파일러가 검사한다.** 런타임 키 조회는 "키 없음"이라는 새 실패 모드를 만들고, 그 증상은
   화면의 `##MISSING##`이다. enum 인덱싱은 **키 오타가 빌드 에러**다.
3. **`Resources.LoadAll`은 이미 위험 지점이다** — `ItemCatalog.EnsureLoaded`가 도메인 리로드 문제 때문에
   지연 로드로 우회하고 있다. 문자열까지 그 경로에 태우면 실패 지점이 하나 는다.
4. 감사(§4)와 골든 테스트가 **소스를 읽는다** — 활성 빌드 타깃과 무관하게 동작해야 하므로(CLAUDE.md)
   테이블이 소스에 있는 편이 검사가 쉽다.

**신규 파일 4개 (`Core/Loc/`)**:

```
Core/Loc/StringId.cs        enum StringId { … }              — 키. 유일한 진실
Core/Loc/StringTableKo.cs   static readonly string[] Table   — (int)StringId 인덱싱
Core/Loc/StringTableEn.cs   static readonly string[] Table
Core/Loc/L.cs               static string T(StringId id)     — 활성 테이블에서 하나 꺼낸다
                            static string T(StringId, arg0…) — string.Format 1회
```

`L.T(id)`는 **인덱싱 한 번**이라 할당이 0이다(`StickMateDisplayNames.Of()`와 동일한 비용).
서식 인자가 있는 35건만 `string.Format`을 탄다 — 지금도 보간으로 매번 새 문자열을 만들고 있으므로 비용은 동일하다.

### 3-3. ★★ 문자열 연결(concat) 조립을 폐기한다 — 이게 어법 확장의 핵심 변경점

`StickMateDisplayNames`의 어법 중 **하나는 그대로 못 쓴다**:

```csharp
// 현행 — 앞/뒤 조각을 붙여 문장을 만든다
BuildBusyTexts(SpectacleNames, "지금 ", " 중이에요")   // → "지금 활쏘기 중이에요"
```

영어는 **어순이 다르다**. `"Busy: " + name + ""`처럼 억지로 맞출 수는 있지만, 그건 어순이 같은 언어에서만
버티는 구조이고 다음 언어에서 반드시 깨진다. **조각이 아니라 템플릿을 테이블에 둔다**:

```csharp
BuildBusyTexts(SpectacleNames, StringId.BusySpectacleFormat)   // ko: "지금 {0} 중이에요"
                                                              // en: "Busy with {0} right now"
```

같은 처방이 필요한 실측 대상:

| 형태 | 건수 | 예 |
|---|--:|---|
| 중괄호 보간을 포함한 문자열 | **35** | `ItemCatalog.cs:248` `"Lv.{ResolveUnlockLevel(config)}에 열림"` → en `"Unlocks at Lv.{0}"` (**어순 반전**) |
| 4자 이하 조각(연결 조립 의심) | **113** | `StickMateDisplayNames` `"지금 "` + `" 중이에요"` |

**규칙 3개 (`coder`/`coder-ui`에게 그대로 넘겨라)**
1. **테이블 항목은 언제나 완성된 문장이다.** 조각을 넣지 않는다.
2. **가변부는 `{0}`,`{1}`.** 번호를 쓴다(순서를 바꿀 수 있어야 한다).
3. **테이블 항목끼리 `+`로 붙이지 않는다.** 붙이고 싶으면 그건 한 항목이어야 한다.
   → §4의 감사가 `L.T(…) +` 패턴을 금지 항목으로 잡는다.

### 3-4. 대사 저장소 — 별도

```
Dialogue/Pools/AmbientPoolKo.cs   string[] IdleLines / WalkLines / …
Dialogue/Pools/AmbientPoolEn.cs   (길이가 달라도 된다)
```

**`coder`에게 넘길 제약 2개**:

1. **인덱스는 활성 풀의 길이에 대해 뽑아야 한다.** 지금 `AmbientChatter`는
   `Enter()`에서 난수를 소진하고 `ChatterParams.LineIndex` 스냅샷을 남긴다(원칙 1을 지키는 그 구조).
   풀을 언어별로 가르면 **인덱스를 뽑는 그 자리에서 활성 풀 길이를 봐야 한다.** 스냅샷 구조 자체는 바뀌지 않는다.
2. **`Resolve`는 여전히 순수 함수다** — (상태 ID, 파라미터) → 문자열. 활성 언어는 **뽑을 때 이미 결정된 것**이고
   `Resolve` 안에서 다시 묻지 않는다. 그래야 31절 계약이 유지된다.

`Core/StickmanAgent.cs`의 집중 모드 5줄(`cfg => "좋아, 감시 시작"`)도 같은 풀로 옮긴다 —
지금 위치는 대사가 **상태 등록표 한복판**에 박혀 있는 형태라 스캐너 사각지대의 원인이기도 하다.

### 3-5. `.asset` 84건

`AccessoryDefSO`에 필드 **2개 추가**:

```csharp
public string displayName;      public string displayNameEn;
public string description;      public string descriptionEn;
```

- **소비 지점은 한 곳뿐이다** — `ItemCatalog.EnsureLoaded()`의 `ItemCatalogEntry.ForEquipment(..., def.displayName, def.description, ...)`.
  거기서 활성 언어로 고른다. **폴백: 비어 있으면 한국어**(빈 카드보다 낫다).
- ScriptableObject 직렬화는 **필드 추가에 마이그레이션이 필요 없다**. 저장 스키마와 무관하다.
- 42개 `.asset` × 2필드 = **84칸**. 값은 `\uXXXX` 이스케이프가 아니라 **평문 ASCII**로 들어가므로
  이후 diff가 사람 눈에 보인다(현행 한국어 필드의 가독성 문제는 이 라운드에서 건드리지 않는다 — Unity가 다시 이스케이프한다).
- **문안 소관**: 장비 42종 이름/설명은 `design-equipment` + `design-narrative`, 행동 12종(`ItemCatalog` 29건)은
  `design-narrative` + `marketing`(스토어 문안과 같은 어휘를 써야 한다).

### 3-6. 로케일 결정과 저장

**`Core/Loc/AppLocale.cs` (플랫폼 중립 — `Platform/`에 두지 않는다. `FullscreenSuspendPolicy` 사고와 같은 이유)**

결정 순서(위가 이긴다):

| # | 출처 | 근거 |
|--:|---|---|
| 1 | 저장된 사용자 선택 (`languageSaved == true`) | 사용자가 고른 것이 언제나 이긴다 |
| 2 | 실행 인자 `-language <ko\|en>` | **스팀이 이 형태로 넘긴다.** Steamworks 통합 없이도 동작 |
| 3 | `Application.systemLanguage == SystemLanguage.Korean` → ko | OS 언어 |
| 4 | 그 밖 → **en** | ★ `product-strategy`: *"영어가 스팀의 폴백 언어"* |

**저장 (★ 따로 올리지 마라)**
`languageSaved`(bool) + `languageName`(string) 2필드. `preferredMonitorSaved`/`inkColorSaved`와 **완전히 같은 형태**라
**버전 분기가 필요 없다** — 옛 파일에는 키가 없어 `false`/`null`로 채워지고, 그 `false`는
*"아직 고른 적 없다 = 자동 판정"*이라는 **정확한 사실**이다.
값은 숫자가 아니라 **이름 문자열**로 적는다(`DialogueVisibleLengthSaveName`이 쓰는 그 관례 —
열거형 순서가 바뀌어도 파일이 안 밀린다).

> ★ 트리 실측: `CharacterSaveStore.CurrentVersion`은 **지금 10**이다(`preferredMonitorSaved`/`preferredMonitorKey`, `dev-platform`).
> 리더 브리핑의 "9로 되돌렸다"와 다르다 — **확인 필요**. 어느 쪽이든 언어 2필드는 **다음 묶음에 얹으면 되고 단독 인상은 필요 없다.**

**언어 전환 UI**: 설정창 [일반]에 3단 세그먼트(`자동 / 한국어 / English`). `ux-designer` 소관.
전환 시 이미 그려진 라벨을 다시 칠해야 하므로 `L.LanguageChanged` 이벤트가 필요하다 —
**또는 1.0은 "다시 시작하면 적용됩니다" 한 줄로 끝낸다**(권고: 후자. 표면 35개를 다시 칠하는 경로가 새 버그 표면이다).

### 3-7. `KoreanParticle` — 손대지 않는다

프로덕션 호출부 **4곳 전수 확인, 전부 `Debug.Log`**:
`Platform/Windows/WindowsTopmostWatchdog.cs:291` / `Interaction/CharacterInfoWindow.Cards.cs:410,427` / `Interaction/GraffitiRenderer.cs:181`.

- **유저 노출 0곳.** `product-strategy`가 *"조립 경로에 언어 분기 필요"*를 R-2의 구조적 장애물로 올려 뒀는데
  (`ROADMAP.md` 3-3절, `MARKET_PERSONAS_PURCHASE.md` F3), **그 장애물은 실재하지 않는다.** 규모 산정에서 뺀다.
- 조사 토큰 10개(은/는·이/가·을/를·과/와·으로/로)는 **번역 대상이 아니라 한국어 문법 장치**다. 테이블에 넣지 않는다.
- ★ **조사 함수에 언어 분기를 넣지 마라.** 영어에 조사 개념이 없다. 분기는 **문장 템플릿 단위**(§3-3)에서 일어난다.
- 나중에 `KoreanParticle`이 UI 경로로 올라오면(예: `"{장비}을(를) 착용했습니다"`), 그 순간
  그 문장은 **템플릿 2벌**(ko/en)로 갈라야지 조사 함수가 갈라지면 안 된다.

---

## 4. 부채 상한 감사

### 4-1. 왜 필요한가 (실측)

`product-strategy`가 문서를 쓰는 30분 사이에 리터럴이 +22건 늘었다. **내가 재는 사이에도 늘었다** —
`grep` 기준 7,968(3시간 전) → **8,270**(지금). 문자열 테이블이 없으면 한국어 리터럴이 **유일하게 가능한 선택**이다.
**그래서 원장이 먼저다.**

### 4-2. 설계 — `Tests/EditMode/LocalizationDebtCeilingAuditTests.cs` (신규)

**원장**: `docs/localization/DEBT_BASELINE.tsv` (이번 라운드에 생성 완료. 77파일 / 485건)

```
Assets/_Project/Scripts/Interaction/SettingsWindow.cs    70
…
Assets/_Project/Resources/Items/equip_head_beret.asset    2
```

**규칙 4개**
1. 파일별 실측 건수 **≤ 원장값**. 넘으면 실패하고 **어느 파일에서 몇 건 늘었는지** 찍는다.
2. **원장에 없는 파일의 기준선은 0.** 새 파일에 출하 한국어 리터럴이 하나만 들어와도 즉시 빨개진다.
3. 총합 **≤ TOTAL(485)**.
4. 원장값을 **올리려면 리더 승인**이 필요하다(주석으로 그 사실을 파일에 박아 둔다). 내리는 건 자유.

**무엇을 세는가 — ★ `OTHER`가 아니라 `SHIP`만**
- `[Tooltip]`/`[Header]`류 → **세지 않는다**(출하 안 됨). 지금 1,662건.
- `Debug.Log*` 직접 인자 → **세지 않는다**. 지금 1,185건.
- 로그 조립 메서드 / 화면 싱크 없는 파일 / 예외 메시지 → **세지 않는다**. 지금 724건.
- ★ `.asset`은 **`\uXXXX`를 디코드한 뒤** 센다. 안 그러면 42개 파일에 대해 **영원히 0건 초록**이다.
- **주석 안의 한글은 리터럴이 아니다** — 반드시 어휘 분석으로 가른다. `grep`은 2.1배 과대다.

**어디에 두는가**: `Tests/EditMode/`. **소스 파일을 읽는다**(타입 리플렉션 금지) — 활성 빌드 타깃 반대편
플랫폼 파일도 세야 하기 때문이다(CLAUDE.md의 그 사각지대).

### 4-3. ★★ 양성 대조 — "0건"을 절대 믿지 않는다

이 저장소가 겪은 거짓 통과 #4가 정확히 이 형태다(*"`strings`로 UTF-16 문자열을 못 찾고 0건 = 깨끗"*).
**감사 테스트는 진짜 트리를 재기 전에 자기 자신을 먼저 검사한다.** 넷 다 통과해야 본 검사가 유효하다.

| # | 대조 | 기대 |
|--:|---|---|
| **A** | 테스트에 박아 둔 **합성 C# 소스**를 훑는다. `[Tooltip("툴팁 한글")]` / `Debug.Log("로그 한글")` / `var s = "유저 노출 한글"` / `"한글"` / **주석 속 한글** | INSPECTOR 1, DEBUG 1, **SHIP 2**(평문+이스케이프), 주석 **0** |
| **B** | 테스트에 박아 둔 **합성 `.asset` YAML 조각**(`displayName: "베레모"`) | ① raw 정규식 `[가-힣]` = **0건**(← 함정이 실재함을 증명) ② 디코드 후 = **1건**(← 우리는 안 빠졌음을 증명) |
| **C** | 원장이 **비어 있지 않고** 파일 수 ≥ 1, TOTAL > 0 | 거짓 통과 #5(빈 목록 순회) 차단 |
| **D** | 실측 스캔이 **실제 파일을 하나 이상** 읽었고 총 리터럴 > 0 | 경로가 틀려 0개 파일을 훑는 경우 차단 |

**A와 B가 하나라도 깨지면 그 실행의 모든 "0건" 판정을 폐기한다** — 테스트 메시지에 그 문장을 그대로 적는다.

### 4-4. 추가 감사 3건 (테이블 도입 이후)

| 감사 | 무엇을 막나 |
|---|---|
| `StringTableKo/En.Table.Length == enum StringId 개수`, 항목에 null/빈 문자열 없음 | 번역 누락이 화면에 도달하기 전에 잡힌다. **양성 대조**: 알려진 키 하나가 ko/en에서 **서로 다름**을 단언(같으면 테이블이 복사본이다) |
| `L.T(...) +` 및 `L.T(...) + L.T(...)` 패턴 소스 금지 | §3-3 규칙 3. 어순을 못 바꾸는 조립을 원천 차단 |
| `.asset` 42개 전부 `displayNameEn`/`descriptionEn`이 비어 있지 않음 | `.asset`은 컴파일러가 안 봐 준다. **양성 대조**: 한 개를 비우면 실패하는지 |

---

## 5. 순서와 규모 — 1.0에 무엇이 언제 필요한가

### 5-1. ★ 게이트가 먼저다 (테이블보다)

| 근거 | |
|---|---|
| **의존성 0** | §2-2. 로케일·설정·저장 어느 것도 안 기다린다. 순수 함수 안에서 끝난다 |
| **선행 없이 하면 되돌릴 수 없다** | 테이블이 먼저 오면 `design-narrative`가 **깨진 게이트를 분모로** 영어를 쓴다. 교정 Walk 예산이 라틴 **11자**가 되고, 그건 영어가 아니다. 그렇게 쓴 문안은 게이트를 고친 뒤 **전부 다시 써야 한다** |
| **양성 대조가 지금 가장 싸다** | 잠가야 할 한국어 대사가 **지금 33줄**이다. 매 라운드 늘어난다 |
| **증상이 안 보인다** | 잘못 출하되면 흔적은 로그 한 줄뿐이다. 영어권 사용자는 "이 캐릭터는 말을 안 하네"라고 생각하고 환불한다 |

### 5-2. 착수 순서

| 단계 | 내용 | 소관 | 규모 | 선행 |
|---|---|---|---|---|
| **L0** | **게이트 언어 인식화** — `DialogueKind.cs` 상수 1 + 함수 1 + 분기 1행 | ~~`coder`~~ → `localization` | **작음** | **없음** — ★ **완료(§9)** |
| **L0-T** | 회귀 + 양성 대조 + 골든 + 스캐너 사각지대 7줄 | ~~`test-engineer`~~ → `localization` | 중간 | ★ **완료(§9)** |
| **L1** | **부채 상한 감사 + 원장** (원장은 이번 라운드에 이미 생성) | `test-engineer` | 작음 | 없음. **L0와 병렬 가능** |
| **L2** | `Core/Loc/` 골격 + `AppLocale` + **표면 1개 시범 이관**(권고: `GearRadialMenuWidget` 18건 — 작고 자기완결적) | `coder-ui` | 중간 | L1(원장이 있어야 이관 진척을 측정) |
| **L3a** | **UI 365건 이관** (설정창 70 / 정보창 71 / 행동창 28 / 집중 25 / 할일 18 …) | `coder-ui` | **큼** | L2 |
| **L3b** | **`.asset` 84칸** — 필드 2개 + 42파일 | `coder` + `design-equipment` | 중간 | L2 |
| **L4** | **영어 대사 풀** 배선 | `coder` | 작음 | **L0 + 문안 도착** |
| **L5** | 설정창 언어 전환 + 저장 2필드 | `coder-ui` + `coder-systems` | 작음 | L2 · **다음 스키마 묶음에 편승** |

**병렬 가능**: L0 ∥ L1. L3a ∥ L3b. L4는 L0 이후 언제든.

### 5-3. 영어 문안 — **언제까지 필요한가** (나는 쓰지 않는다)

| 문안 | 건수 | 소관 | 필요 시점 | 비고 |
|---|--:|---|---|---|
| **앰비언트 대사 24줄** | 24 | `design-narrative` | **L0 완료 직후 / L4 시작 전** | ★ §2-8의 판정이 먼저 나와야 한다. Walk 5줄이 재창작 대상이 될 수 있다 |
| 사건 대사(등반·매달림·랙돌·가출·창도둑·활쏘기·집중) | ~15 | `design-narrative` | L4 전 | LedgeHang 16자 / Climb 18자 상한(여유 0 기준) |
| **행동 12종 이름+설명** (`ItemCatalog` 29건) | 29 | `design-narrative` + `marketing` | L3a 전 | ★ **스토어 페이지 기능 설명과 같은 문장이 된다** |
| 장비/외형 42종 이름+설명 | 84 | `design-equipment` + `design-narrative` | L3b 전 | |
| 설정창·정보창 UI 문안 | ~250 | `ux-designer` + `design-narrative` | L3a와 동시 | 대부분 기계적 번역으로 충분하지만 **캡션은 재창작**(원래 문장이 길다) |

### 5-4. 스토어 페이지는 이 일정에 물리는가 — **부분적으로 그렇다**

`product-strategy` `ROADMAP.md` **CS-4**(*영문 스토어 문안 — 영어가 스팀의 폴백 언어이고 내용이 있어야 한다*, `marketing` + 리더, 미착수).

| 산출물 | 우리 일정에 물리나 | 왜 |
|---|---|---|
| 스토어 **텍스트** 문안 | **아니다 — 병렬 가능** | 앱 빌드에 의존하지 않는다. 지금 시작할 수 있다 |
| ★ 스토어 **스크린샷 / GIF** | **그렇다 — L3a 완료에 물린다** | `marketing` 규칙: *"거짓 소재 금지 / 캡처는 실제 빌드에서"*. 한국어 UI가 찍힌 화면을 영어 스토어에 올릴 수 없다 |
| ★ **기능 설명 문장** | **그렇다 — 어휘를 공유해야 한다** | `ItemCatalog`의 행동 12종 설명이 곧 스토어의 기능 목록이다. 앱과 스토어가 **다른 이름**을 쓰면 그 자체로 신뢰가 깨진다. → `marketing`은 §5-3의 행동 12종 문안을 **받아서** 쓰고, 먼저 쓰지 않는다 |

**권고**: `marketing`에 지금 배정할 것은 **"영어 UI가 완성되기 전에 확정할 수 있는 것"**뿐이다 —
장문 설명·태그·카테고리. **스크린샷 촬영은 L3a 이후로 못 박아라.**

---

## 6. 교차 검토 — 어느 산출물의 어느 지점과 맞물리나

| 상대 | 맞물리는 지점 | 내가 한 것 |
|---|---|---|
| `design-narrative` §5-3 | 계수 `w = 0.0472` | **독립 재계산해 채택.** 유도 논거(같은 발화 = 같은 예산)도 그대로 승계 |
| `design-narrative` §3-4 | 영어 풀 24줄 "탈락 0줄" | ★ **분모를 교정하면 성립하지 않는다**(§2-8). 5줄/1줄 탈락. **반증해서 돌려보낸다** |
| `design-narrative` §5-5 | 회귀 방어선의 구멍 | **확인.** 스캐너는 26줄만 훑고 실제는 33줄. 사각지대 7줄을 **실명으로 특정**(§2-5) |
| `design-motion` §3-3 | 분모 교정 1.65 / 1.24 | **채택.** 그 결과가 §2-8의 새 결함을 낳았다 |
| `design-motion` §3-4 | Getup 0.60 / Attack 0.40은 대사 금지 | **영어에서도 같다.** 라틴 최소 발화도 `MinSeconds` 0.62 + 페이드인 0.06 = **0.68초**로 동일(하한이 계수와 무관하므로) |
| `design-motion` §3-5 | 지형까지 본 잔여 | 도입되면 §2-8의 분모가 다시 바뀐다. **그때 재계산 필요**(gate.py에 분모 표만 갈아 끼우면 된다) |
| `product-strategy` 3-3절 | 규모 표 | ★ **세 줄이 틀렸다**: 리터럴 7,968(실제 문자열 리터럴 3,972 / 출하 485) / `KoreanParticle` 구조적 장애물(**실재하지 않음**) / `CurrentVersion` 9(**트리는 10**) |
| `product-strategy` CS-4 | 영문 스토어 문안 | §5-4. **텍스트는 병렬, 스크린샷은 L3a에 물린다** |
| `ux-designer` | 설정창 언어 전환 UI + 문자열 길이 | 영어는 같은 뜻에 **~1.6배 글자**가 든다(말뭉치 실측 112:178). **버튼/칩 폭이 고정이면 잘린다.** 별도 배정 필요 |
| `coder-systems` | 저장 스키마 | 언어 2필드는 **버전 분기 불필요**. 다음 묶음에 편승(§3-6) |
| `test-engineer` | L0-T, L1 | 양성 대조 8종을 §2-6·§4-3에 명세해 뒀다 |

---

## 7. 플랫폼 영향

**Windows 영향: 없음(이번 라운드) / L0에서도 없음.**
`DialogueKind.cs`는 `#if` 하나 없는 **완전한 플랫폼 중립**(`Mathf` + `string`)이다.
문자열 테이블도 `Core/Loc/`에 두므로 마찬가지다. 다만 **감사 테스트는 소스 파일을 읽도록** 짠다 —
그래야 macOS 타깃에서 돌려도 `Platform/Windows/*.cs`의 리터럴을 함께 센다(CLAUDE.md 활성 빌드 타깃 사각지대).
프로덕션 `.cs`를 건드리는 라운드(L0~L5)에서는 `Tools/CrossCompile/xcheck.sh win`/`osx` 양쪽 errors=0을 요구한다.

**macOS 영향: 없음(이번 라운드).**
★ 단, **폰트에 실측 위험이 하나 있다** — `DialogueBubbleRenderer.ResolveFont()`는 후보 폰트를
`CanRenderKorean(f)`(`'한'` 글리프 실측)으로 **거른다**. 한국어 폰트가 없는 영어권 머신에서는 후보가 전부
탈락해 내장 `LegacyRuntime.ttf`로 폴백하고, 그때 **`_cachedFontIsRealBold` 경로(진짜 Bold 페이스)를 잃는다** —
`design-art`/`design-motion`이 맞춰 둔 만화 레터링의 굵기가 그 환경에서만 달라진다.
macOS는 `Apple SD Gothic Neo`가 항상 있어 안전하지만 **Windows 영어권은 미확인**이다.
→ **영어 모드에서는 라틴 글리프만 검증하도록 조건을 갈라야 한다.** 별도 배정 필요(`ux-designer` + `coder-ui`, L3a 근처).

---

## 8. 미확인 · 열린 질문 (거짓 통과 방지)

1. **§2-8의 판정** — 팝인 마진(§7-3)을 Walk에 적용할 것인가. **리더 판정 대기.** 답에 따라
   `design-narrative`가 Walk 영어 5줄을 다시 쓴다.
2. **`CurrentVersion`** — 트리는 **10**인데 브리핑은 9라고 했다. 어느 쪽이 맞는지 확인 필요.
   (어느 쪽이든 언어 2필드 계획은 안 바뀐다.)
3. **`Dialogue/DialogueBubbleRenderer.cs` 13건** 중 진단 라벨이 몇 건인지 리터럴 단위로 안 갈랐다.
   **안전측(SHIP)으로 남겼다** — 최대 과대 추정 폭이다.
4. **`LOGF` 478건**은 "파일에 화면 싱크가 없다"로 배제했다. **파일 단위 판정이라 언젠가 틀릴 수 있다.**
   §4의 감사가 매 라운드 다시 잰다.
5. **영어권 Windows의 폰트 폴백** — 실기 미확인(§7).
6. **Unity 배치모드를 돌리지 않았다.** `debugger`가 쓰는 중이라 순서 대기 중이다. 이 문서의 숫자는 전부
   **소스/에셋 정적 분석과 순수 함수 재계산**이며, 그 범위 안에서만 유효하다.
   L0 착수 시 EditMode 실행이 필요하고 그때 리더에게 알린다.
7. **앱 실행·빌드를 하지 않았다.** 프로덕션 `.cs`도 수정하지 않았다.

---

## 부록 — 이 라운드의 산출물

| 경로 | 무엇 |
|---|---|
| `docs/localization/PLAN_1.0.md` | 이 문서 |
| `docs/localization/DEBT_BASELINE.tsv` | 부채 상한 원장 (77파일 / 485건) |
| `docs/localization/verify/census.py` (+`--selftest`) | C# 어휘 분석 + `.asset` 디코드 인구조사. **양성 대조 10종** |
| `docs/localization/verify/ship.py` (+`--selftest`) | 출하 여부 리터럴 단위 재분류. **양성/음성 대조 10종** |
| `docs/localization/verify/tier.py` | 파일 단위 도달성(**폐기된 접근** — 왜 부족한지가 파일 머리말에 있다) |
| `docs/localization/verify/gate.py` | 게이트 재계산 + 한국어 불변 + **양성/음성 대조 4종** |
| `*.out.txt` / `census.json` / `ship.json` | 위 실행 결과 |


---

## 9. 이번 라운드 구현 기록 — L0 (게이트) 완료

리더 배정(2026-09-02): *"계획 승인. 게이트 수정을 네가 직접 구현해라."*

### 9-1. 리더 판정 반영 — §2-8은 **(나) 재창작**

| 선택지 | 판정 | 이유(리더) |
|---|---|---|
| (가) Walk에 §7-3 마진 미적용 | **기각** | 그 0.18초는 근거가 있다 — `Calm` 등급 30fps에서 여유가 **한 프레임보다 작았다**. 예외를 뚫으면 근거가 죽는다 |
| **(나) 영어 5줄 재창작** | **채택** | 문안만 바뀌고 인프라 0 |
| (다) `design-motion` §3-5 지형 잔여 | **기각(지금은)** | 새 인프라이고 §3-5 자체가 제안 단계 |

### 9-2. ★ `design-narrative`에 넘기는 예산 (문안은 내 소관이 아니다)

분모는 `design-motion` 2026-09-02 R4 §3-3 보증값(지터 반영).
검산: `docs/localization/verify/gate.out.txt`

| 상태 | 분모 | 여유 0 (**현행 게이트**) | 여유 0.18 (**§7-3 도입 시**) |
|---|--:|---|---|
| Idle | 1.65초 | 한글 17 / **영어 27** | 한글 15 / **영어 23** |
| Walk | 1.24초 | 한글 12 / **영어 19** | 한글 9 / **영어 15** |
| ParkourClimb | 1.20초 | 한글 11 / **영어 18** | 한글 9 / **영어 14** |
| LedgeHang | 1.12초 | 한글 10 / **영어 16** | 한글 8 / **영어 12** |

> ★★ **리더가 놓칠 수 있는 파급 1건 — 마진은 Walk 5줄만 건드리지 않는다.**
> §7-3(팝인 마진)을 도입하면 **사건 대사도 함께 조인다**:
>
> | 상태 | 문안 | 자 | 여유 | 마진 0 | 마진 0.18 |
> |---|---|--:|--:|---|---|
> | ParkourClimb | `Whoa, that's high` | 17 | **+0.058** | 통과 | ★ **탈락** |
> | LedgeHang | `Whoa... that's deep` | 19 | −0.117 | 탈락 | 탈락 |
> | Walk | `Stretching the legs.` | 20 | −0.044 | 탈락 | 탈락 |
>
> 즉 **§7-3이 들어오는 시점에 영어 문안 예산이 한 번 더 줄어든다.** `design-narrative`가
> 지금 (나)로 다시 쓸 때 **오른쪽 열(마진 0.18)을 기준으로 쓰는 편이 안전하다** —
> 그러면 §7-3 도입 여부와 무관하게 살아남는다. **판단은 리더 몫이고 나는 숫자만 넘긴다.**

### 9-3. 실제로 바꾼 것

| 파일 | 무엇 |
|---|---|
| `Assets/_Project/Scripts/Dialogue/DialogueKind.cs` | `PerLatinGlyphSeconds = 0.0472f` 신설 / `IsSyllabicScript(string)` 신설 / `ReadingSeconds` 한 행 분기. `BaseSeconds`·`PerGlyphSeconds`를 `private` → `internal`(테스트가 **참조**하도록. 숫자 베끼기 금지) |
| `Tests/EditMode/DialogueCorpus.cs` (신규) | 대사 수집기 **단일화** + 사각지대 2종 추출기. 추출기는 **문자열 인자**를 받는 순수 함수 → 합성 소스로 양성 대조 가능 |
| `Tests/EditMode/DialogueLanguageBudgetTests.cs` (신규) | 테스트 메서드 **16개** / NUnit 케이스 **33건**(`[Test]` 14 + `[TestCase]` 19) |
| `Tests/EditMode/Golden/DialogueBudgetKoGolden.txt` (신규) | 한국어 33줄의 **개정 전** 가독예산을 IEEE754 32비트로 동결 |
| `Tests/EditMode/DialogueVisibleScaleContractTests.cs` | 인라인 스캐너 제거 → `DialogueCorpus` 위임. `Assert.Greater(count, 20)` → **골든 줄 수와 정확히 일치** |
| `docs/localization/verify/{golden_gen,dryrun}.py` (신규) | 골든 생성기 / 오프라인 예행 |

**바꾸지 않은 것**: `BaseSeconds` 0.28 · `MinSeconds` 0.62 · `MaxSeconds` 2.20 ·
`RequiredDwellSeconds`/`IsEligible`/`CanReplaceVisible`/`VisibleIsDoomedByIncoming`의 형태 ·
`DialogueTiming` 3상수 · 사용자 노출 배율 관련 전부.
**`Core/StickmanAgent.cs`는 읽기만 했다**(테스트 스캐너가 소스를 읽을 뿐, 수정 0).

### 9-4. ★ 양성 대조가 어떤 형태로 코드에 들어갔는가

리더 지시: *"양성 대조를 네가 겪은 그 형태로 만들어라. 개정 쪽만 변이시켜야 한다."*

`DialogueLanguageBudgetTests`의 모사판 `ReadingWith(text, perSyllabic, perLatin)`은 **계수 둘을
인자로 받는다.** 기준은 **디스크의 골든 파일**이라 **구조적으로 함께 움직일 수 없다.**

| 테스트 | 무엇을 증명하나 | 실측 |
|---|---|--:|
| `교정_모사판이_프로덕션과_같은_값을_낸다` | 모사판이 진짜와 동일 → 아래 대조가 의미를 갖는다 | 43줄 전수 일치 |
| `양성대조_개정쪽_음절계수만_틀리면_골든과_어긋난다` | ★ **한쪽만 변이**. 0.075 → 0.0751 | **28/33줄 어긋남** |
| `양성대조_분기를_지우면_한국어는_그대로이고_영어만_달라진다` | 분기가 **영어에만** 걸린다(양방향) | 한글 33 동일 / 영어 10 전부 변화 |
| `양성대조_계수를_고치지_않으면_영어가_침묵한다` | 이 라운드의 존재 이유 자체 | 전 1.615(침묵) → 후 1.142(발화), 분모 1.20 |
| `양성대조_수집기가_세_형태를_모두_찾고_없으면_0이다` | "0건"과 "패턴 불일치로 0건"을 가른다 | 합성 소스 양/음 대조 |
| `혼합_문자열은_음절_계수로_청구된다` 안의 `AreNotEqual` | 두 계수가 **실제로 다른 값**을 낸다 → 단언이 공허하지 않다 | — |
| `골든과_소스_말뭉치가_양방향으로_일치한다` | 대사 **추가**와 **삭제**를 각각 잡는다 | 33 ↔ 33 |
| `사각지대_대사가_말뭉치에_들어와_있다` (7 케이스) | 스캐너를 옛 형태로 되돌리면 빨개진다 | 7/7 |

**기대값을 골든 비트에서 되살린다**(`FromBits`) — 프로덕션 함수로 기대값을 만들면
그 함수가 틀어질 때 기대값도 함께 틀어져 아무것도 재지 못한다.

### 9-5. 검증 결과

| 무엇 | 결과 |
|---|---|
| `Tools/CrossCompile/xcheck.sh osx` | **5개 어셈블리 errors=0** |
| `Tools/CrossCompile/xcheck.sh win` | **5개 어셈블리 errors=0** |
| `docs/verify/regress.sh selfcheck` (러너 가드 자기검사) | 음성 대조 4 + 양성 대조 1 **전부 통과** |
| **Unity EditMode 전량** `loc-gate-2_edit.xml` (16:08:05) | **tcc 1602 / total 1602 / passed 1589 / failed 1 / skipped 12** |
| — `DialogueLanguageBudgetTests` | **33/33 Passed** |
| — `DialogueVisibleScaleContractTests` | **11/11 Passed** |
| — 기존 대사 테스트 3종(Exposure 16 / TextActionSync 8 / OutlineCounter 3) | **27/27 Passed** |
| — 남은 실패 1건 | ★ **내 것이 아니다**(9-5-2) |
| `docs/localization/verify/dryrun.py` (오프라인 예행 39종) | 통과 (**단, 9-5-1 참고**) |
| `docs/localization/verify/gate.py` (양성/음성 대조 4종) | 전부 통과 |

#### 9-5-1. ★★★ 내 오프라인 예행이 <b>거짓 통과</b>였다 — 러너가 잡았다

**첫 배치모드 실행(`loc-gate_edit.xml`, 16:04)에서 내 테스트 3건이 실패했다.**
골든과 실제 값이 **13줄에서 1 ULP** 어긋났다:

```
"그래 쉬자"     골든 3F27AE14(0.655000)  vs  현재 3F27AE15(0.655000)
"다리 좀 풀자"  골든 3F4E147C(0.805000)  vs  현재 3F4E147B(0.805000)
```

**원인**: `golden_gen.py`가 float32 산술을 **연산마다 라운딩**으로 흉내 냈다.
Unity Mono는 `float` 식을 **double로 계산하고 결과를 쓸 때 한 번만** 라운딩한다
(ECMA가 허용하는 "더 넓은 정밀도"). 두 방식은 글자수 **5·7·15·18·20·25**에서 정확히 1 ULP 갈린다.

> ★★ **더 중요한 것은 왜 오프라인에서 못 잡았는가다.**
> `dryrun.py`(검사기)와 `golden_gen.py`(생성기)가 **같은 잘못된 `f32` 흉내를 공유**했다.
> **둘이 같은 함정에 같이 빠졌으므로 서로를 확인해 주지 못했고, 38종 전부 초록이었다.**
> 이것이 이 저장소가 하루에 아홉 번 당한 형태의 또 다른 얼굴이다 —
> 이번에는 "기준과 대상"이 아니라 **"생성기와 검사기"**가 같이 움직였다.
>
> **처방(적용 완료)**: 두 스크립트 모두 맨 앞에 `CALIBRATION`을 뒀다 —
> **러너가 실제로 뱉은 비트**(5자→`3F27AE15`, 7자→`3F4E147B` 등 5개 표본)로 계산기를 먼저 교정하고,
> **교정이 깨지면 굽기/판정을 거부**한다(TEAM.md 공통 처방).

**★ 프로덕션은 바뀌지 않았다 — 그 증거는 같은 실행 안에 있다.**
`한국어는_구식_단일계수_식과_구조적으로_동치다`가 **첫 실행에서도 Passed**였다.
이 테스트는 런타임 안에서 구식 식 `clamp(Base + N·PerGlyph, Min, Max)`를 직접 계산해
`ReadingSeconds`와 비트 비교한다 — **33줄 전수 일치**.
즉 틀린 것은 **내 파이썬 골든**이었고, **프로덕션의 한국어 경로는 비트까지 그대로**다.
골든을 러너 실측 기준으로 다시 구운 뒤 재실행(`loc-gate-2`)에서 **33/33 Passed**.

#### 9-5-2. 남은 실패 1건 — `coder-systems` B-2 소관

```
✗ WornShapeDataGoldenTests.목_형상은_데이터화_전후로_비트까지_같다
  NECK 몸 도형이 골든과 다릅니다 — 형상이 움직였습니다. 11번째 줄이 다릅니다 …
```

**내 것이 아니라는 근거 3개**:
1. 베이스라인(`dbg-fix_edit.xml`, 15:22)에 **그 테스트 클래스 자체가 없었다** — 이번에 새로 생긴 9건 중 하나다.
2. 내 변경 파일은 `Dialogue/DialogueKind.cs`와 대사 테스트 3개뿐이고 `AccessoryShapeBuilder`/`Golden/NeckWornShapeGolden.txt`를 건드리지 않았다(둘 다 `coder-systems` 작업 트리에 있다).
3. 내 골든을 고치기 **전과 후 모두 동일하게** 실패했다(`loc-gate` / `loc-gate-2`).

**신규 케이스 85건 귀속**: 내 `DialogueLanguageBudgetTests` **33** /
`PackPaletteGateTests` 17 · `RarityColorSingleSourceTests` 13 · `ItemRarityDerivationTests` 10 ·
`WornShapeDataGoldenTests` 9 · `TodoPostItExpansionAuditTests` 3 (= 다른 라운드 52).
**사라진 케이스 0건.**

★ **첫 `xcheck.sh osx`는 실패했다(errors=3).** 원인은 내 변경이 아니라 `coder-systems` B-2 라운드가
`AccessoryShapeBuilder` 멤버를 개명하는 **도중의 트리**를 잡은 것이다
(`AccessoryFallbackIconParityTests` 2건 / `AccessoryShapeCatalogTests` 1건 — 전부 `CS0117`).
**두 파일 모두 내가 건드리지 않았다.** 그 사이 상대 라운드가 마무리해 재실행에서 양쪽 초록이 됐다.
확인차 그 두 파일만 제외하고 EditMode 어셈블리를 격리 컴파일해 **errors=0**을 먼저 확인했다.
→ **CLAUDE.md "동시 진행" 규칙의 실제 사례다**: 남의 라운드 중간 트리에서 검증하면 내 결과가 아니다.

### 9-6. 남은 것 (이 라운드 범위 밖)

1. ~~Unity EditMode 러너 실행~~ — ★ **완료**(§9-5). 1602건 / 내 33건 전부 통과 / 잔여 실패 1건은 타 라운드.
2. **영어 문안** — `design-narrative`. §9-2 예산 전달 완료.
3. **§7-3 팝인 마진** — 아직 코드에 없다. 도입되면 §9-2 오른쪽 열이 발효된다.
4. **L1 부채 상한 감사** — 원장(`DEBT_BASELINE.tsv`)은 있고 테스트는 아직 없다.
5. **폰트 폴백**(§7) — 리더가 별건으로 기록. 이 라운드에서 건드리지 않았다.
