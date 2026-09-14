### ⚠ 알려진 문제 (원인 확인 중): 외장 모니터를 뽑기 전에 StickMate를 종료해 주십시오

**보고된 내용.** Windows에서 StickMate가 실행되는 동안 외장 모니터 케이블을 뽑았습니다.
화면이 흰색으로 바뀌었고, 마우스와 키보드가 반응하지 않았습니다.
PC를 다시 시작해야 했습니다.
StickMate를 끈 상태에서 케이블을 뽑았을 때는 이 문제가 생기지 않았다고 보고되었습니다.

**원인은 아직 확인 중입니다.** 이 빌드에는 이 문제에 대한 수정이 들어 있지 않습니다.

**모니터를 뽑기 전에: StickMate 종료하기**

1. 작업 표시줄 오른쪽 끝, 시계 근처에서 StickMate 아이콘을 찾습니다.
   아이콘 위에 마우스를 올리면 `StickMate`로 시작하는 설명이 나타납니다.
   아이콘이 보이지 않으면 위쪽 화살표 `^`를 클릭합니다. 숨겨진 아이콘은 그 안에 있습니다.
2. StickMate 아이콘을 오른쪽 클릭합니다.
3. 메뉴 **맨 아래, 구분선 아래**에 있는 `StickMate 종료`를 클릭합니다.
   확인 창 없이 바로 종료됩니다. 종료되면 캐릭터와 StickMate 아이콘이 함께 사라집니다.

> **`캐릭터 숨기기`는 종료가 아닙니다.** 이 항목은 캐릭터만 가립니다.
> StickMate는 계속 실행되고, 아이콘도 그대로 남습니다.
> (캐릭터를 이미 숨겨 둔 상태라면 이 항목의 글자는 `캐릭터 다시 보이기`입니다.)

아이콘을 찾을 수 없으면 `Ctrl` + `Alt` + `Windows 로고 키` + `Q`를 동시에 눌러 보십시오.
그래도 종료되지 않으면 `Ctrl` + `Shift` + `Esc`로 작업 관리자를 열고 `StickMate`를 선택해 **작업 끝내기**를 누릅니다.
작업 관리자로 끝낸 경우에는 아래 「멈췄을 때 보고하는 법」의 **5단계(작업 표시줄 되돌리기)**도 해 주십시오.

**이 문제가 어디까지 해당하는지**

| 상황 | 지금 알고 있는 것 |
|---|---|
| StickMate 실행 중에 외장 모니터 케이블을 뽑음 | 보고되었습니다 |
| 노트북 덮개를 닫음 | 아직 모릅니다 |
| 화면이 절전 상태에서 다시 켜짐 | 아직 모릅니다 |

「아직 모릅니다」는 확인하지 못했다는 뜻입니다. 확실하지 않으면 먼저 StickMate를 종료해 주십시오.

**PC가 멈춰서 다시 시작했다면:** StickMate를 다시 켜기 전에 아래 「멈췄을 때 보고하는 법」을 먼저 읽어 주십시오.

---

### 멈췄을 때 보고하는 법

PC가 멈춰서 다시 시작한 분을 위한 순서입니다. **1단계부터 순서대로** 따라 주십시오.

#### 1단계. StickMate를 아직 켜지 마십시오

StickMate는 켤 때마다 기록 파일 `Player.log`를 새로 씁니다.

- **한 번 켜면:** 멈췄을 때의 기록이 `Player-prev.log`라는 이름으로 옮겨집니다.
- **두 번 켜면:** 멈췄을 때의 기록이 사라집니다.

이미 한 번 켰다면 괜찮습니다. 멈췄을 때의 기록은 `Player-prev.log`에 있습니다. 더 켜지 말고 2단계로 가십시오.

#### 2단계. 기록 폴더를 엽니다

1. 키보드에서 `Windows 로고 키`와 `R`을 함께 누릅니다. 입력 칸이 있는 작은 창이 열립니다.
2. 아래 한 줄을 복사해서 그 입력 칸에 붙여넣습니다.

   ```
   %USERPROFILE%\AppData\LocalLow\Vibelab\StickMate
   ```

3. `Enter`를 누릅니다. StickMate 기록 폴더가 열립니다.

#### 3단계. 파일을 바탕 화면에 복사해 둡니다

폴더 안에서 `Player.log`와 `Player-prev.log`를 찾습니다.
(파일 확장자를 숨기는 설정이면 `Player`, `Player-prev`로만 보일 수 있습니다. 같은 파일입니다.)

1. `Ctrl` 키를 누른 채로 두 파일을 클릭해 모두 선택하고 `Ctrl` + `C`를 누릅니다.
2. 바탕 화면에 새 폴더를 만들고, 그 폴더를 열어 `Ctrl` + `V`를 누릅니다.

복사가 끝났다면, 이제 StickMate를 켜도 멈췄을 때의 기록을 잃지 않습니다.

#### 4단계. 알려 주십시오

[GitHub Issues에 새 글](https://github.com/moonkj/StickMate/issues/new)을 열고 아래를 적어 주십시오.

- 멈춘 대략의 시각 (예: 오후 2시 20분쯤)
- 멈추기 바로 전에 한 일 (예: 외장 모니터 케이블을 뽑았다)
- 3단계에서 로그 파일을 복사해 두었는지

> **로그 파일은 이 공개 게시판에 올리지 마십시오.** 로그에는 Windows 사용자 이름이 들어간 폴더 경로가 들어 있을 수 있습니다.
> 파일을 받는 방법은 글에 답글로 따로 안내드립니다.

#### 5단계. StickMate를 한 번 켰다가 종료합니다 (작업 표시줄 되돌리기)

작업 표시줄 **자동 숨기기**를 켜 두고 쓰던 분에게 필요한 단계입니다.
**잘 모르겠으면 그냥 해 주십시오.** 자동 숨기기를 쓰지 않았다면 이 단계로 작업 표시줄 설정은 바뀌지 않습니다.

1. StickMate를 켭니다. StickMate가 켜져 있는 동안에는 모니터 케이블을 꽂거나 뽑지 마십시오.
   StickMate가 켜져 있는 동안 작업 표시줄이 계속 보이는 것은 정상입니다.
2. StickMate 아이콘을 오른쪽 클릭하고, 메뉴 맨 아래의 `StickMate 종료`를 클릭합니다.

**왜 필요한가요?** StickMate는 실행되는 동안 작업 표시줄 자동 숨기기를 잠시 끄고, 종료할 때 원래 설정으로 되돌리도록 만들어져 있습니다.
PC가 멈추면 이 되돌리기가 실행되지 못합니다. StickMate를 한 번 켰다가 종료하면 이 되돌리기가 마저 실행되도록 만들어져 있습니다.
그래도 돌아오지 않으면 작업 표시줄 빈 곳을 오른쪽 클릭 → **작업 표시줄 설정**에서 자동 숨기기 항목을 직접 바꿀 수 있습니다.

---

### ⚠ Known issue (cause under investigation): Quit StickMate before you unplug an external monitor

**What was reported.** On Windows, an external monitor cable was unplugged while StickMate was running.
The screen turned white, and the mouse and keyboard stopped responding.
The PC had to be restarted.
It was also reported that the problem did not occur when the cable was unplugged while StickMate was not running.

**The cause is still under investigation.** This build does not contain a fix for this problem.

**Before you unplug a monitor: quit StickMate**

1. Find the StickMate icon at the right end of the taskbar, near the clock.
   When you move the mouse over the icon, a tooltip that starts with `StickMate` appears.
   If you cannot see the icon, click the up arrow `^` (Show hidden icons). Hidden icons are inside it.
2. Right-click the StickMate icon.
3. Click the item at the **bottom of the menu, below the divider line**: `StickMate 종료` (Quit StickMate).
   StickMate quits right away, without a confirmation box. The character and the StickMate icon both disappear.

> **In this build, the menu is shown in Korean for every Windows display language.**
> **The first item, `캐릭터 숨기기` (Hide character), does not quit StickMate.** It only hides the character.
> StickMate keeps running, and the icon stays.
> (If the character is already hidden, this item reads `캐릭터 다시 보이기` (Show character again).)

If you cannot find the icon, try pressing `Ctrl` + `Alt` + `Windows logo key` + `Q` at the same time.
If StickMate still does not quit, press `Ctrl` + `Shift` + `Esc` to open Task Manager, select `StickMate`, and click **End task**.
If you ended it from Task Manager, also do **Step 5 (restore the taskbar)** of "How to report a freeze" below.

**What we know about the scope**

| Situation | What we know now |
|---|---|
| Unplugging an external monitor cable while StickMate is running | Reported |
| Closing a laptop lid | Not known yet |
| A display waking up from sleep | Not known yet |

"Not known yet" means that we have not checked it. If you are not sure, quit StickMate first.

**If your PC froze and you restarted it:** before you start StickMate again, read "How to report a freeze" below.

### How to report a freeze

These steps are for you if your PC froze and you restarted it. **Follow the steps in order, starting at Step 1.**

#### Step 1. Do not start StickMate yet

Each time StickMate starts, it writes a new log file named `Player.log`.

- **If you start it once:** the log from the freeze is moved to a file named `Player-prev.log`.
- **If you start it twice:** the log from the freeze is lost.

If you already started StickMate once, that is fine. The log from the freeze is in `Player-prev.log`. Do not start it again. Go to Step 2.

#### Step 2. Open the log folder

1. Press the `Windows logo key` and `R` together. A small box with a text field opens.
2. Copy the line below and paste it into the text field.

   ```
   %USERPROFILE%\AppData\LocalLow\Vibelab\StickMate
   ```

3. Press `Enter`. The StickMate log folder opens.

#### Step 3. Copy the files to your desktop

In the folder, find `Player.log` and `Player-prev.log`.
(If your PC hides file extensions, they may appear as `Player` and `Player-prev`. They are the same files.)

1. Hold down `Ctrl`, click both files to select them, and press `Ctrl` + `C`.
2. Create a new folder on the desktop, open it, and press `Ctrl` + `V`.

After the copy is done, starting StickMate will not remove the log from the freeze.

#### Step 4. Tell us

Open a [new GitHub issue](https://github.com/moonkj/StickMate/issues/new) and write:

- About what time the PC froze (for example: around 2:20 PM)
- What you did just before it froze (for example: unplugged an external monitor cable)
- Whether you copied the log files in Step 3

> **Do not upload the log files to this public issue board.** The logs can contain a folder path with your Windows user name.
> We will reply in the issue with a way to send the files.

#### Step 5. Start StickMate once, then quit it (restore the taskbar)

This step is needed if you use taskbar **auto-hide**.
**If you are not sure, do this step anyway.** If you do not use auto-hide, this step does not change your taskbar setting.

1. Start StickMate. While StickMate is running, do not plug in or unplug any monitor cable.
   While StickMate is running, it is normal that the taskbar stays visible.
2. Right-click the StickMate icon, and click the item at the bottom of the menu: `StickMate 종료` (Quit StickMate).

**Why is this needed?** StickMate is designed to turn off taskbar auto-hide while it runs, and to restore your original setting when it quits.
When the PC freezes, this restore cannot run. StickMate is designed so that starting it once and then quitting it runs this restore.
If the taskbar still does not go back, right-click an empty area of the taskbar → **Taskbar settings**, and change the auto-hide option yourself.

---

DLC 팩 12종 착용 조형 재작업(R4) 반영. 코드기준 1283d74.
