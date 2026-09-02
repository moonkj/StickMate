# 회사명 이사 안내 — `DefaultCompany` → `Vibelab` (2026-09-02)

사용자에게 넘기는 정리 안내다. **이 문서의 작업은 사용자가 한다.** 팀은 구경로를 지우지 않는다.

무엇이 바뀌었나:

| 항목 | 전 | 후 |
|---|---|---|
| `companyName` | `DefaultCompany` | **`Vibelab`** |
| 번들 ID | (자동 파생) `com.DefaultCompany.StickMate` | **`com.Vibelab.StickMate`** (명시 고정) |
| `overrideDefaultApplicationIdentifier` | `0` | **`1`** |
| `productName` | `StickMate` | `StickMate` (**안 바꿈**) |

`overrideDefaultApplicationIdentifier: 1`이 핵심이다. 0이면 `companyName` 하나가 **서로 독립인 두 계약**
(스토어 영구 번들 ID + 유저 데이터 경로)을 동시에 지배해서, 누가 회사 표기를 미관상 손보면 번들 ID가
부작용으로 함께 움직인다. 이제 둘은 분리됐다 — 회사 표기를 또 바꿔도 번들 ID는 고정이다.

---

## ★★ 0. 가장 먼저 — Windows 작업표시줄 원복 원장 (이것만은 순서를 지켜라)

**Windows에서 Vibelab 빌드를 처음 실행하기 _전에_ 이 절을 끝내라.** 나머지는 나중에 해도 되지만
이건 아니다.

### 왜 이게 1번인가

CLAUDE.md 원칙 3(유저 자산 불변)에는 **승인된 예외가 딱 하나** 있다 — 실행 중 작업표시줄 자동 숨김
해제, 종료 시 원복. 이 예외가 성립하는 유일한 근거는 **"크래시로 원복 훅이 못 돌아도 다음 실행이
디스크 흔적을 보고 먼저 복구한다"**는 장치다. 그 흔적이 이 파일이다:

```
%USERPROFILE%\AppData\LocalLow\<회사>\StickMate\stickmate_reserved_bar_restore.json
```

경로에 **회사명이 들어간다.** 그래서 이런 일이 벌어질 수 있다:

1. `DefaultCompany` 빌드가 작업표시줄 자동 숨김을 해제하고 흔적을 남긴다 (`active: true`)
2. 그 빌드가 크래시하거나 강제 종료된다 → 작업표시줄은 **자동 숨김이 꺼진 채** 남는다 (여기까진 설계대로)
3. 다음에 실행하는 것이 `Vibelab` 빌드다 → **`Vibelab\StickMate\`를 본다. 거기엔 흔적이 없다.**
4. **빚은 영원히 안 갚아진다.** 사용자 작업표시줄은 자동 숨김이 꺼진 채로 복구 수단 없이 남는다.

> 이것이 **원칙 3의 승인된 예외가 사후에 무너지는 유일한 경로**다.
> 흔적 파일은 앱이 스스로 지우지 않고 `active: false`로 내려서 닫는다 —
> 즉 **파일이 남아 있는 것 자체는 정상**이고, 위험한 것은 `active: true`인 채 고아가 되는 것이다.

### 할 일 (Windows, Vibelab 빌드 첫 실행 전)

```powershell
# (1) 흔적이 있는지, 있다면 아직 안 갚은 빚인지 본다
$old = "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\StickMate\stickmate_reserved_bar_restore.json"
if (Test-Path $old) { Get-Content $old } else { "흔적 없음 — 갚을 빚이 없다" }
```

- **파일이 없다** → 할 일 없다. 0절 끝. (자동 숨김을 안 쓰는 사용자면 앱이 애초에 흔적을 안 만든다.)
- **`"active": false`** → 이미 갚았다. 그대로 두거나 지워도 된다.
- **★ `"active": true`** → **안 갚은 빚이 있다.** 둘 중 하나를 해라:
  - **(권장) 흔적을 새 경로로 옮긴다.** 그러면 Vibelab 빌드가 첫 실행에서 정상적으로 갚는다:
    ```powershell
    $new = "$env:USERPROFILE\AppData\LocalLow\Vibelab\StickMate"
    New-Item -ItemType Directory -Force -Path $new | Out-Null
    Move-Item $old "$new\stickmate_reserved_bar_restore.json"
    ```
    실행 후 로그에 `★ 복구 —` 줄이 뜨고 자동 숨김이 돌아오는지 확인한다.
  - **(대안) 손으로 되돌린다.** `originalAutoHide` 값이 원래 설정이다. [설정 > 개인 설정 > 작업 표시줄]에서
    그 값대로 직접 맞춘 뒤 파일을 지운다.

**macOS는 이 절과 무관하다.** 이 기능은 `UNITY_STANDALONE_WIN && !UNITY_EDITOR`로 막혀 있어 macOS는
디스크에도 시스템에도 한 바이트도 쓰지 않는다(macOS Dock은 이번 예외에 포함되지 않았다). 실제로
확인했다 — 구·신 경로 어디에도 흔적 파일이 없다.

---

## 1. macOS — 이미 복사돼 있다

리더가 원본을 보존한 채 복사해 뒀고, 새 경로에 파일이 실재하는 것을 확인했다:

| 무엇 | 새 경로 |
|---|---|
| 세이브 4개 | `~/Library/Application Support/Vibelab/StickMate/` |
| 로그 2개 | `~/Library/Logs/Vibelab/StickMate/` |
| 온보딩 "봤음" 플래그 | `~/Library/Preferences/unity.Vibelab.StickMate.plist` |

**구경로는 그대로 살아 있다.** 되돌릴 여지를 남겨 둔 것이다.

### 일부러 복사하지 않은 것 1개

```
stickmate_character.json.67542.writing
```

중단된 원자 쓰기의 잔재(고아 임시 파일)다. 정상 세이브가 아니므로 옮기지 않았다. 구경로를 지울 때
같이 사라진다. **따로 손댈 필요 없다.**

---

## 2. Windows — ★ 같은 이사를 사용자가 직접 해야 한다

이 개발 머신에 Windows가 없어서 **팀이 복사하지 못했다.** Windows에서 진행도를 이어가려면 아래를
사용자가 해야 한다. 안 하면 **레벨/XP/장비/할일 목록이 초기화된 것처럼 보인다**(데이터가 지워진 게
아니라 앱이 새 폴더를 보는 것이다).

**0절을 먼저 끝냈는지 확인하고 시작하라.**

```powershell
# (1) 세이브 + 로그 — 폴더 통째로 복사 (원본은 남긴다)
$src = "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\StickMate"
$dst = "$env:USERPROFILE\AppData\LocalLow\Vibelab\StickMate"
if (Test-Path $src) {
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    Copy-Item "$src\*" $dst -Recurse -Force
    Get-ChildItem $dst        # ← 실제로 들어왔는지 눈으로 확인. 빈 목록이면 실패다.
} else { "구경로 없음 — Windows에서 실행한 적이 없다" }

# (2) 온보딩 "봤음" 플래그 — 레지스트리
#     macOS의 unity.<회사>.<제품>.plist에 해당한다. 안 옮기면 첫 실행 안내가 다시 뜬다(그뿐이다).
$rsrc = "HKCU:\Software\DefaultCompany\StickMate"
$rdst = "HKCU:\Software\Vibelab"
if (Test-Path $rsrc) {
    New-Item -Path $rdst -Force | Out-Null
    Copy-Item -Path $rsrc -Destination $rdst -Recurse -Force
    Get-ItemProperty "HKCU:\Software\Vibelab\StickMate"   # ← 확인
} else { "레지스트리 키 없음 — 옮길 것이 없다" }
```

> 레지스트리에 들어 있는 것은 온보딩 플래그(`StickMate.GearMenu.OnboardingSeen.v1`)와 Unity가
> 스스로 만든 세션 값들뿐이다. **진행도는 레지스트리가 아니라 (1)의 JSON에 있다.**
> 이 앱은 작업표시줄 원복에 PlayerPrefs를 쓰지 않는다(레지스트리 쓰기를 피하려고 일부러 파일로 갔다).

---

## 3. 구경로를 언제 지워도 되나

**지금은 아니다. 아래 세 개가 전부 참이 된 뒤에 지워라.**

1. **새 경로로 실제 실행이 한 번 성공했다.** 앱을 띄우고 로그에서 세이브를 새 경로에서 **불러왔다**는
   줄을 확인한다:
   ```
   [성장] 준비 완료 — … 저장 파일=불러옴 (…/Vibelab/StickMate/stickmate_character.json)
   ```
   ★ `저장 파일=없음 — 새 캐릭터로 시작`이 뜨면 **이사가 안 된 것이다.** 여기서 멈추고 구경로를
   절대 지우지 마라.
2. **레벨/XP/장비/할일이 이사 전과 같다.** 화면에서 눈으로 확인한다.
3. **새 경로 세이브의 mtime이 갱신됐다** = 앱이 새 경로에 **쓰고도 있다**(읽기만 하는 게 아니다).
   자동 저장 주기가 60초라 1~2분 켜 두면 갱신된다.

세 개가 다 참이면 구경로를 지워도 된다.

```bash
# macOS — 사용자가 직접. 팀은 지우지 않는다.
rm -rf "$HOME/Library/Application Support/DefaultCompany/StickMate"
rm -rf "$HOME/Library/Logs/DefaultCompany/StickMate"
rm -f  "$HOME/Library/Preferences/com.DefaultCompany.StickMate.plist"
rm -f  "$HOME/Library/Preferences/unity.DefaultCompany.StickMate.plist"
```

★ 같은 폴더에 `StickMateDbg` / `StickMateDbg2` / `StickMateSkeleton`도 있다. 개발 중 만든 별도
`productName` 빌드의 것이고 **이번 이사와 무관하다.** 필요 없으면 같이 지워도 되지만, 과거 실측
로그가 이 폴더들을 근거로 인용하고 있으니 급하지 않으면 남겨 두는 편이 낫다.

```powershell
# Windows — 위 3개 확인 뒤
Remove-Item "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\StickMate" -Recurse -Force
Remove-Item "HKCU:\Software\DefaultCompany\StickMate" -Recurse -Force
```

### 지우기 전에 한 번 더

- **0절의 흔적 파일을 아직 안 갚았다면 구경로를 지우는 순간 원래 설정값이 사라진다.**
  `originalAutoHide`가 유일한 복구 근거다. 0절을 끝냈는지 확인하라.
- 외부 배포 이력이 0이라 **세이브 이행(migration) 코드는 신설하지 않았다.** 즉 앱은 구경로를
  **찾아보지 않는다.** 이 문서의 수동 이사가 유일한 경로다.

---

## 4. 팀 쪽에서 같이 고친 것 (사용자가 할 일 없음)

경로를 하드코딩하고 있던 도구들. 안 고치면 **이후 모든 실기 라운드가 「없는 파일」을 읽고,
"파일이 없다"와 "변화가 없다"의 출력이 똑같이 생겨서** 세이브 검증이 초록으로 통과하고 로그 기반
신고 재현이 "재현 안 됨"으로 닫힌다.

| 파일 | 조치 |
|---|---|
| `.claude/skills/run-stickmate/driver.sh` | `STICKMATE_COMPANY` 변수로 분리(기본 `Vibelab`). **세이브 백업을 건너뛸 때 반드시 한 줄을 찍게 했다** — 침묵 금지 |
| `Tools/PerfProbe/measure-msaa.py` | 회사명을 베끼지 않고 **`ProjectSettings.asset`에서 읽는다**. 로그 파일 부재를 "MSAA 줄 없음"과 구분해 출력 |

문서·주석 중 **현재형으로 경로를 안내하던 것**만 고쳤다(`ARCHITECTURE.md`, `PERFORMANCE_NOTES.md`,
`SECURITY_MODEL.md`, `CAPTURE_PROTOCOL.md`, `SKILL.md`, `WindowsCompositionProbe.cs`).
**과거 실측·결정 기록은 그대로 뒀다** — 그때 실제로 그랬던 사실이라 고치면 기록이 거짓이 된다.
