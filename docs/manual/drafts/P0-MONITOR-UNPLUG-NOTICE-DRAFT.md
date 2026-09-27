# [초안] 공개 프리뷰 경고 + 멈췄을 때 보고하는 법 — Windows 외장 모니터 분리

> ## ⛔ 이 문서는 게시하지 않았습니다
>
> **릴리즈 본문에 붙일지는 사용자 판단 대기입니다.** 이 파일은 그 판단이 나왔을 때 바로 쓸 수 있게
> 만든 준비물입니다. 릴리즈 편집·외부 게시는 하지 않았습니다.
>
> 대상 릴리즈: `windows-preview-20260909b` (코드 기준 `1283d74`)
> 작성 `manual-writer` / 2026-09-14
> 근거: `Tasklist.md` 「2026-09-14 (리더) — 【P0 · 출시 차단】」 절 + 「페르소나 검증 로그 (민지 / 초보 관점)」 +
> 코드 실측(부록 A)

---

## 0. 게시하는 사람에게 (게시할 때 이 절과 부록은 빼십시오)

**자리표시 — 게시 전에 채워야 합니다**

| 자리표시 | 위치 | 몇 곳 |
|---|---|---:|
| `[보낼 곳: 리더 확정 필요]` | 2-KO 5단계, 2-EN Step 5 | 2 |
| `[2절 위치: 리더 확정 필요]` | 1-KO 마지막 줄, 1-EN 마지막 줄 | 2 |

**붙이는 방법(안)**: 1절을 릴리즈 본문 **맨 위**에 둡니다. 2절은 같은 본문 아래에 이어 붙이거나
별도 페이지에 두고, 1절 마지막 줄의 자리표시를 그 위치로 바꿉니다.

**★ 영어판에서도 메뉴 이름은 한국어 그대로 인용합니다.** 이 프리뷰의 트레이 메뉴는
**Windows 표시 언어와 상관없이 한국어로만** 나옵니다(부록 A-4). 그래서 영어판은 한국어 글자를
그대로 보여 주고, 뜻과 **메뉴 안의 위치**(맨 아래, 구분선 아래)를 함께 적었습니다.

**이 초안이 맞는 빌드는 `1283d74` 기반 공개 프리뷰 하나입니다.** 다른 빌드를 내보내면
2절 3단계의 `FreezeForensics` 문장과 1절의 「이 빌드에는 수정이 없다」 문장을 다시 확인하십시오.

---

## 1. 릴리즈 본문 경고

### 1-KO

────────── 여기부터 복사 ──────────

### ⚠ 알려진 문제 (원인 확인 중): 외장 모니터를 뽑기 전에 StickMate를 종료해 주십시오

**보고된 내용.** Windows에서 StickMate가 실행되는 동안 외장 모니터 케이블을 뽑았습니다.
화면이 흰색으로 바뀌었고, 마우스와 키보드가 반응하지 않았습니다.
PC는 전원 버튼으로 다시 시작해야 했습니다.
StickMate를 먼저 종료한 뒤 케이블을 뽑았을 때는 이 문제가 생기지 않았습니다.

**원인은 아직 확인 중입니다.** 이 빌드에는 이 문제에 대한 수정이 들어 있지 않습니다.

**모니터를 뽑기 전에: StickMate 종료하기**

1. 작업 표시줄 오른쪽 끝, 시계 근처에서 StickMate 아이콘을 찾습니다.
   아이콘 위에 마우스를 올리면 `StickMate`로 시작하는 설명이 나타납니다.
   아이콘이 보이지 않으면 위쪽 화살표 `^`를 클릭합니다. 숨겨진 아이콘은 그 안에 있습니다.
   Windows 11에서는 StickMate 아이콘이 처음부터 그 안에 들어가 있을 수 있습니다.
2. StickMate 아이콘을 오른쪽 클릭합니다.
3. 메뉴 **맨 아래, 구분선 아래**에 있는 `StickMate 종료`를 클릭합니다.
   확인 창 없이 바로 종료됩니다. 종료되면 캐릭터와 StickMate 아이콘이 함께 사라집니다.

> **`캐릭터 숨기기`는 종료가 아닙니다.** 이 항목은 캐릭터만 가립니다.
> StickMate는 계속 실행되고, 아이콘도 그대로 남습니다.
> (캐릭터를 이미 숨겨 둔 상태라면 이 항목의 글자는 `캐릭터 다시 보이기`입니다.)

아이콘을 찾을 수 없으면 `Ctrl` + `Alt` + `Windows 로고 키` + `Q`를 동시에 누릅니다.
이 키도 StickMate를 종료합니다.

**이 문제가 어디까지 해당하는지**

| 상황 | 지금 알고 있는 것 |
|---|---|
| StickMate 실행 중에 외장 모니터 케이블을 뽑음 | 보고되었습니다 |
| 노트북 덮개를 닫음 | 아직 모릅니다 |
| 화면이 절전 상태에서 다시 켜짐 | 아직 모릅니다 |

「아직 모릅니다」는 확인하지 못했다는 뜻입니다. 확실하지 않으면 먼저 StickMate를 종료해 주십시오.

**PC가 멈춰서 다시 시작했다면:** StickMate를 다시 켜기 전에
「멈췄을 때 보고하는 법」(`[2절 위치: 리더 확정 필요]`)을 먼저 읽어 주십시오.

────────── 여기까지 복사 ──────────

### 1-EN

────────── copy from here ──────────

### ⚠ Known issue (cause under investigation): Quit StickMate before you unplug an external monitor

**What was reported.** On Windows, an external monitor cable was unplugged while StickMate was running.
The screen turned white, and the mouse and keyboard stopped responding.
The PC had to be restarted with the power button.
When StickMate was quit first and the cable was unplugged after that, the problem did not occur.

**The cause is still under investigation.** This build does not contain a fix for this problem.

**Before you unplug a monitor: quit StickMate**

1. Find the StickMate icon at the right end of the taskbar, near the clock.
   When you move the mouse over the icon, a tooltip that starts with `StickMate` appears.
   If you cannot see the icon, click the up arrow `^` (Show hidden icons). Hidden icons are inside it.
   On Windows 11, the StickMate icon may be inside it from the start.
2. Right-click the StickMate icon.
3. Click the item at the **bottom of the menu, below the divider line**: `StickMate 종료` (Quit StickMate).
   StickMate quits right away, without a confirmation box.
   When StickMate quits, the character and the StickMate icon both disappear.

> **In this build, the menu is shown in Korean for every Windows display language.**
> **The first item, `캐릭터 숨기기` (Hide character), does not quit StickMate.** It only hides the character.
> StickMate keeps running, and the icon stays.
> (If the character is already hidden, this item reads `캐릭터 다시 보이기` (Show character again).)

If you cannot find the icon, press `Ctrl` + `Alt` + `Windows logo key` + `Q` at the same time.
This key combination also quits StickMate.

**What we know about the scope**

| Situation | What we know now |
|---|---|
| Unplugging an external monitor cable while StickMate is running | Reported |
| Closing a laptop lid | Not known yet |
| A display waking up from sleep | Not known yet |

"Not known yet" means that we have not checked it. If you are not sure, quit StickMate first.

**If your PC froze and you restarted it:** before you start StickMate again, read
"How to report a freeze" (`[2절 위치: 리더 확정 필요]`).

────────── copy until here ──────────

---

## 2. 멈췄을 때 보고하는 법

### 2-KO

────────── 여기부터 복사 ──────────

### 멈췄을 때 보고하는 법

PC가 멈춰서 다시 시작한 분을 위한 순서입니다. **1단계부터 순서대로** 따라 주십시오.

#### 1단계. StickMate를 아직 켜지 마십시오

StickMate는 켤 때마다 기록 파일 `Player.log`를 새로 씁니다.

- **한 번 켜면:** 멈췄을 때의 기록이 `Player-prev.log`라는 이름으로 옮겨집니다.
- **두 번 켜면:** 멈췄을 때의 기록이 사라집니다.

이미 한 번 켰다면 괜찮습니다. 멈췄을 때의 기록은 `Player-prev.log`에 있습니다.
더 켜지 말고 2단계로 가십시오.

StickMate는 PC를 켤 때 저절로 실행되지 않습니다.
(시작 프로그램에 StickMate를 직접 넣어 두었다면 저절로 실행됩니다. 그 경우 이미 한 번 켠 것입니다.)

#### 2단계. 기록 폴더를 엽니다

1. 키보드에서 `Windows 로고 키`와 `R`을 함께 누릅니다. 입력 칸이 있는 작은 창이 열립니다.
2. 아래 한 줄을 복사해서 그 입력 칸에 붙여넣습니다.

   ```
   %USERPROFILE%\AppData\LocalLow\Vibelab\StickMate
   ```

3. `Enter`를 누릅니다. StickMate 기록 폴더가 열립니다.

오류가 나타나면, 위의 한 줄을 처음부터 끝까지 빠짐없이 복사했는지 확인하고 다시 시도하십시오.

#### 3단계. 파일을 다른 곳에 복사해 둡니다

폴더 안에서 아래 항목을 찾습니다.

| 이름 | 무엇인가 |
|---|---|
| `Player.log` | 가장 최근에 StickMate를 실행했을 때의 기록 |
| `Player-prev.log` | 그 이전 실행의 기록 |
| `FreezeForensics` (폴더) | 멈춤을 조사하기 위한 기록. **있을 때만** 복사합니다 |

- `windows-preview-20260909b`는 `FreezeForensics` 폴더를 만들지 않습니다.
  이 폴더가 없으면 **로그 파일 두 개만** 복사하면 됩니다.
- 파일 확장자를 숨기는 설정이면 `Player`, `Player-prev`로만 보일 수 있습니다. 같은 파일입니다.
- 폴더 안의 다른 파일은 복사하지 않아도 됩니다.

복사하는 방법:

1. `Ctrl` 키를 누른 채로 위 항목을 하나씩 클릭해서 모두 선택합니다.
2. `Ctrl` + `C`를 눌러 복사합니다.
3. 바탕 화면에 새 폴더를 만듭니다. 그 폴더를 열고 `Ctrl` + `V`를 눌러 붙여넣습니다.

복사가 끝났다면, 이제 StickMate를 켜도 멈췄을 때의 기록을 잃지 않습니다.

#### 4단계. 짧은 메모를 적습니다

- 멈춘 대략의 시각 (예: 오후 2시 20분쯤)
- 멈추기 바로 전에 한 일 (예: 외장 모니터 케이블을 뽑았다)

#### 5단계. 보냅니다

3단계에서 만든 폴더와 4단계의 메모를 아래로 보내 주십시오.

**`[보낼 곳: 리더 확정 필요]`**

#### 6단계. StickMate를 한 번 켰다가 종료합니다 (작업 표시줄 되돌리기)

작업 표시줄 **자동 숨기기**를 켜 두고 쓰던 분에게 필요한 단계입니다.
**잘 모르겠으면 그냥 해 주십시오.** 자동 숨기기를 쓰지 않았다면 이 단계는 아무것도 바꾸지 않습니다.

1. StickMate를 켭니다. StickMate가 켜져 있는 동안에는 모니터 케이블을 꽂거나 뽑지 마십시오.
   StickMate가 켜져 있는 동안 작업 표시줄이 계속 보이는 것은 정상입니다.
2. StickMate 아이콘을 오른쪽 클릭하고, 메뉴 맨 아래의 `StickMate 종료`를 클릭합니다.

**왜 필요한가요?** StickMate는 실행되는 동안 작업 표시줄 자동 숨기기를 잠시 끄고,
종료할 때 원래 설정으로 되돌리도록 만들어져 있습니다.
PC가 멈추면 이 되돌리기가 실행되지 못합니다.
StickMate를 한 번 켰다가 종료하면 이 되돌리기가 마저 실행됩니다.

이 단계를 마친 뒤에도 작업 표시줄이 원래대로 돌아오지 않으면, Windows 설정에서 직접 바꿀 수 있습니다.

- **Windows 11:** 작업 표시줄의 빈 곳을 오른쪽 클릭 → **작업 표시줄 설정** → **작업 표시줄 동작** →
  **자동으로 작업 표시줄 숨기기**
- **Windows 10:** 작업 표시줄의 빈 곳을 오른쪽 클릭 → **작업 표시줄 설정** →
  **데스크톱 모드에서 자동으로 작업 표시줄을 숨기기**

────────── 여기까지 복사 ──────────

### 2-EN

────────── copy from here ──────────

### How to report a freeze

These steps are for you if your PC froze and you restarted it. **Follow the steps in order, starting at Step 1.**

#### Step 1. Do not start StickMate yet

Each time StickMate starts, it writes a new log file named `Player.log`.

- **If you start it once:** the log from the freeze is moved to a file named `Player-prev.log`.
- **If you start it twice:** the log from the freeze is lost.

If you already started StickMate once, that is fine. The log from the freeze is in `Player-prev.log`.
Do not start it again. Go to Step 2.

StickMate does not start by itself when your PC starts.
(If you added StickMate to your startup programs yourself, it does start by itself. In that case, it has already started once.)

#### Step 2. Open the log folder

1. On the keyboard, press the `Windows logo key` and `R` together. A small box with a text field opens.
2. Copy the line below and paste it into the text field.

   ```
   %USERPROFILE%\AppData\LocalLow\Vibelab\StickMate
   ```

3. Press `Enter`. The StickMate log folder opens.

If an error appears, check that you copied the whole line, from start to end, and try again.

#### Step 3. Copy the files to another place

In the folder, find these items.

| Name | What it is |
|---|---|
| `Player.log` | The log from the most recent time StickMate ran |
| `Player-prev.log` | The log from the run before that |
| `FreezeForensics` (folder) | Records for investigating freezes. Copy it **only if it exists** |

- `windows-preview-20260909b` does not create the `FreezeForensics` folder.
  If this folder is not there, copy **only the two log files**.
- If your PC hides file extensions, the files may appear as `Player` and `Player-prev`. They are the same files.
- You do not need to copy any other file in the folder.

How to copy:

1. Hold down the `Ctrl` key and click each item above, so that all of them are selected.
2. Press `Ctrl` + `C` to copy.
3. Create a new folder on the desktop. Open that folder and press `Ctrl` + `V` to paste.

After the copy is done, starting StickMate will not remove the log from the freeze.

#### Step 4. Write a short note

- About what time the PC froze (for example: around 2:20 PM)
- What you did just before it froze (for example: unplugged an external monitor cable)

#### Step 5. Send

Send the folder from Step 3 and the note from Step 4 to:

**`[보낼 곳: 리더 확정 필요]`**

#### Step 6. Start StickMate once, then quit it (restore the taskbar)

This step is needed if you use taskbar **auto-hide**.
**If you are not sure, do this step anyway.** If you do not use auto-hide, this step changes nothing.

1. Start StickMate. While StickMate is running, do not plug in or unplug any monitor cable.
   While StickMate is running, it is normal that the taskbar stays visible.
2. Right-click the StickMate icon, and click the item at the bottom of the menu: `StickMate 종료` (Quit StickMate).

**Why is this needed?** StickMate is designed to turn off taskbar auto-hide while it runs,
and to restore your original setting when it quits.
When the PC freezes, this restore cannot run.
When you start StickMate once and then quit it, the restore runs.

If the taskbar does not go back to your original setting after this step, you can change it in Windows Settings.

- **Windows 11:** Right-click an empty area of the taskbar → **Taskbar settings** → **Taskbar behaviors** →
  **Automatically hide the taskbar**
- **Windows 10:** Right-click an empty area of the taskbar → **Taskbar settings** →
  **Automatically hide the taskbar in desktop mode**

────────── copy until here ──────────

---

## 부록 A. 실측 확인 목록

★ 전부 **읽기 전용**으로 쟀다. 앱 실행 0회 · 빌드 0회. **Windows 실기는 이 머신에 없어 한 건도 확인하지 못했다.**
`1283d74` = 공개 프리뷰 코드 기준. `Builds/Windows` = 리더가 `Tasklist.md`에 적은 사용자 빌드
(Runtime.dll 2026-09-09 00:34). **공개 zip 자체는 열어 보지 않았다** — 두 산출물이 같다는 것은 리더 기록에 기댄다.

| # | 문장 속 사실 | 근거 | 측정 · 대조 |
|---:|---|---|---|
| A-1 | 폴더 이름 `Vibelab\StickMate` | `ProjectSettings/ProjectSettings.asset:15-16` (`companyName: Vibelab` / `productName: StickMate`) — HEAD와 `1283d74` 둘 다 | `Builds/Windows/StickMate_Data/app.info` 바이트 = `Vibelab\nStickMate` |
| A-2 | 메뉴 글자 `캐릭터 숨기기` / `캐릭터 다시 보이기` / `설정 열기` / `StickMate 종료` | `Platform/SystemTrayPresencePolicy.cs:178,180,182` (`1283d74`에서 같은 줄) | 출하 dll UTF-16 검색: 각각 2 / 1 / 1 / 1건. 음성 대조(없는 문자열) 0건 |
| A-3 | 종료가 **맨 아래, 구분선 아래** | 같은 파일 `:149-154` `MenuOrder` = 숨기기 → 설정 → 종료, `:158-159` 구분선은 종료 앞에만 | — |
| A-4 | **메뉴는 한국어로만 나온다** | `LabelFor`가 한국어 리터럴만 돌려준다. 호출자는 `1283d74:Platform/Windows/WindowsSystemTrayIcon.cs:504` 한 곳(`AppendMenuW`) | dll UTF-16: `Quit StickMate` · `Hide character` · `Show character` · `Open settings` 전부 0건. 양성 대조 `Settings` 7건 |
| A-5 | 마우스를 올리면 `StickMate`로 시작하는 설명 | `SystemTrayPresencePolicy.cs:75` `Tooltip` | dll UTF-16 2건 |
| A-6 | 트레이 종료는 **확인 창 없이** 바로 종료 | `Interaction/SystemTrayCommandBridge.cs:99-100` → `Interaction/AppControlDirector.cs:406-411` `Application.Quit()` 직접 호출 | `git diff --stat 1283d74 HEAD`로 두 파일 변경 0. 양성 대조: 같은 명령이 `FreezeWatchdog.cs`에 +268을 출력 |
| A-7 | 종료하면 아이콘이 사라진다 | `1283d74:WindowsSystemTrayIcon.cs:331` `NIM_DELETE`, `:348` `Application.quitting` 구독 | **코드 판독만.** 실기에서 셸이 아이콘을 바로 지우는지는 미확인 |
| A-8 | `Ctrl`+`Alt`+`Windows 로고 키`+`Q`로 종료 | `1283d74:AppControlDirector.cs:288,358`. 개발 게이트 밖(`:73`, `:422`). 표기 `Core/ShortcutLabel.cs:41` `Ctrl+Alt+Win+`. 매뉴얼 1장 6절과 일치 | **실기 미확인.** `ShortcutLabel.cs:100`이 MS 문서의 「Windows 키 조합은 OS 예약」 경고를 인용하고 있다 |
| A-9 | 기록 폴더 `%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate` | Unity `persistentDataPath` 규칙 + A-1. 매뉴얼 2장 `:159`와 같은 경로 | **Windows 실기 미확인**(2장 부록 메모와 같은 조건) |
| A-10 | `Player-prev.log`로 밀리고, 두 번 켜면 사라진다 | Unity 표준 동작. `docs/verify/WINDOWS_CHECK_SESSION.md` §H-3 1번 · §Z-1 주석과 같은 서술 | `Builds/Windows/UnityPlayer.dll` ASCII `Player-prev.log` 1건 · `Player.log` 1건 · 음성 대조 0건. **회전 동작 자체는 실기 미확인** |
| A-11 | `FreezeForensics` 폴더 — 있을 때만 / 이 프리뷰에는 없다 | **HEAD `ccfaef9` 기준** 이름: `Platform/FreezeForensicsPolicy.cs:45` `DirectoryName`, 위치: `Platform/FreezeWatchdog.cs:234` `Path.Combine(Application.persistentDataPath, …)`. ★ 작성 중에 다른 라운드가 이 파일들을 수정하고 있었다(작업 트리에서는 `:242`). 이름 `"FreezeForensics"`는 HEAD·작업 트리 둘 다 같다 | `git ls-tree -r 1283d74`에서 `FreezeForensics*`·`FreezeWatchdog` 0개(양성 대조: 같은 명령이 `SystemTrayPresencePolicy.cs` 1개를 찾음, HEAD에서는 프로덕션 3개). 출하 dll UTF-16 `FreezeForensics` 0건(양성 대조: `stickmate_reserved_bar_restore.json` 2건) |
| A-12 | PC를 켤 때 저절로 실행되지 않는다 | `Assets/_Project/Scripts` 전체에서 `CurrentVersion\Run` · `LaunchAgent` · `SMAppService` · `autostart` · `AutoStart` · `RunAtLogin` · `StartupApproved` 0건 | 같은 grep 형태로 `Shell_NotifyIcon`은 3개 파일을 찾음. `Tasklist.md` marketing R9의 「로그인 자동 실행 미구현」과 일치. **패턴 기반 부재 판정의 한계가 있다** |
| A-13 | 켰다가 종료하면 작업 표시줄 자동 숨기기가 원래대로 돌아온다 | `docs/TASKBAR_REVEAL.md` 2-1~2-3. `Platform/ReservedBarRevealPolicy.cs:129-196` (`ResolveRecovery` / `ResolveStartup` / `ResolveQuit`), `Platform/ReservedBarRevealDirector.cs:298` `Application.quitting` | 네 파일 모두 `1283d74`에 실재하고 그 뒤 변경 0. 출하 dll에 흔적 파일 이름 2건. **Windows 실기 미확인**(`TASKBAR_REVEAL.md` 7절 ❌ 3줄) — 그래서 본문을 「그렇게 **만들어져 있습니다**」로 썼다 |
| A-13a | 재부팅이 설정을 되돌렸든 안 되돌렸든 「켰다 → 종료」의 끝 상태는 같다 | 유지된 경우: `ResolveRecovery`가 원래 값으로 먼저 되돌림(`RecoverLeftover`). 풀린 경우: `LeftoverAlreadyMatched`로 흔적만 닫고, 이번 실행이 다시 해제했다가 종료할 때 원복 | `debugger`가 「재부팅 뒤에도 유지되는가」를 **미확인**으로 남겼다. 이 문장은 그 답에 기대지 않도록 썼다 |
| A-14 | 자동 숨기기를 안 쓰면 아무것도 안 바뀐다 | `TASKBAR_REVEAL.md` 2-1 · 2-4, `ResolveStartup` → `AlreadyVisible` | 실기 미확인 |
| A-15 | `^` 안에 숨겨진 아이콘이 있다 / 영어 이름 Show hidden icons | Microsoft 지원 문서(ko-kr: 「오버플로 영역」), Eleven Forum 등 | ★ **한국어 Windows에서 `^`의 툴팁 글자는 1차 출처로 확인하지 못해 한국어판에 인용하지 않았다** |
| A-16 | 자동 숨기기 설정 경로와 항목 이름(Win 11 / Win 10, 한·영) | Microsoft 지원 「작업 표시줄 사용자 지정」 ko-kr · en-us | ★ **요약 도구를 거쳐 받은 인용이다.** 게시 전에 페이지를 눈으로 한 번 확인할 것. 특히 Win 10 한국어 「…작업 표시줄을 숨기기」의 조사 |

## 부록 B. 모르는 것 · 일부러 쓰지 않은 것

**모르는 것 (본문에 「아직 모릅니다」로 적었거나 적지 않은 것)**

- **노트북 덮개 닫기 · 디스플레이 절전 복귀** — 같은 경로인지 아무도 확인하지 않았다(CW-7 §G-1은 수정 빌드 지정 때만 수행).
- **macOS** — 사용자가 확인하지 않았다고 명시했다. **이 릴리즈는 Windows 전용이라 본문에서 뺐다**(쓰면 소음만 늘어난다).
- **보고 건수와 신고자** — 본문은 「보고된 내용」으로만 적었다. 건수를 적을지는 리더 판단.
- **「보고되었습니다」이지 「확인되었습니다」가 아니다** — 브리프는 「확인됨」이었으나 우리가 재현한 적이 없어서 낮춰 적었다.
- **켜 둔 채 로그아웃 · PC 종료** — `debugger` 실측상 원복 경로가 돌지 않을 수 있다(`WM_ENDSESSION` 미처리, 미확인).
  **이번 경고의 범위가 아니라서 본문에 넣지 않았다.** 다만 같은 사용자가 같은 작업 표시줄 문제를 겪을 수 있으니 리더 판단 항목으로 올린다.

**일부러 쓰지 않은 것**

| 쓰지 않은 것 | 이유 |
|---|---|
| 「전원 버튼을 길게 눌러 끄십시오」 같은 지시 | 강제 종료를 **시키는** 문장은 저장하지 않은 작업을 잃게 할 수 있다. 「다시 시작했다면」이라는 조건으로만 연결했다 |
| `Ctrl+Alt+Win+K` · 드라이버 재시작 등 멈춤 진단 사다리(§H-2) | 개발 확인 세션용이다. `K`는 **숨기기**라 초보에게 「숨기기 = 종료」 혼동을 오히려 키운다 |
| 「다음 빌드에서 고칩니다」 · 「다음 빌드부터 `FreezeForensics`가 생깁니다」 | 규칙: 지금 없는 것은 쓰지 않는다. 대신 「이 빌드에는 수정이 없다」 · 「이 빌드는 그 폴더를 만들지 않는다」처럼 **이 빌드에 대한 현재 사실**로만 적었다 |
| ZIP 압축 메뉴 이름(「ZIP 파일로 압축」 등) | Windows 버전마다 글자가 다르고 1차 출처로 확인하지 못했다. `Ctrl`+`C` → 새 폴더 → `Ctrl`+`V`로 바꿨다 |
| 「실행」 창 이름 | 1차 출처 미확인. 「입력 칸이 있는 작은 창」으로 묘사했다 |
| 겁주는 형용사(「심각한」 · 「치명적인」 · 「완전히」) | 브리프 금지. 증상은 사용자 진술 그대로의 사실(흰 화면 · 입력 무반응 · 전원 버튼 재시작)로만 적었다 |

## 갱신 이력

| 날짜 | 내용 |
|---|---|
| 2026-09-27 | **깨진 굵게 1곳 정정(렌더 결함 · 마커만 이동).** 아래 2026-09-14 행의 `**「내용」**를` 형태에서 **마커를 낫표 안으로** 옮겼다(`「**내용**」를`). 닫는 별표 바로 앞이 낫표이고 바로 뒤가 한글이면 굵게가 닫히지 않아 **화면에 별표가 글자로 뜬다.** TEAM §5 렌더 정정 예외 조건 셋: ⑴ 바뀐 글자는 별표 마커뿐 ⑵ 별표를 뺀 본문이 글자 단위로 같다 ⑶ 사유는 화면에 별표가 글자로 떴다는 것이다. 계기는 markdown_it `commonmark` 프리셋으로 렌더해 살아남은 리터럴 별표를 센 것이고, 이 문서는 본문 1자리 → **0자리**가 됐다. 문장은 한 글자도 바뀌지 않았다. |
| 2026-09-14 | 첫 작성. 리더 브리프(P0 · 공개 프리뷰 경고 초안) + `persona-newcomer`(민지) 지적 5건을 전부 반영: ① 「숨기기 ≠ 종료」를 메뉴 **위치**까지 붙여 분리 ② `^` 안에서 아이콘 찾기 + 툴팁으로 식별 ③ 범위를 「보고됨 / 아직 모름」 표로 ④ `FreezeForensics` 「없으면 로그 두 개만」 분기 ⑤ 마지막 단계로 작업 표시줄 되돌리기. 추가로 코드 실측 중 찾은 「**이 프리뷰의 트레이 메뉴는 한국어로만 나온다**」를 영어판에 반영했다. 게시 안 함. |
