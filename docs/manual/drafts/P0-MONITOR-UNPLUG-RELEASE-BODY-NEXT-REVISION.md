# [개정 준비] P0 모니터 분리 안내 — 다음 게시본 개정 문안

> ## 이 문서는 게시하지 않습니다
>
> 이미 게시된 본문은 `drafts/P0-MONITOR-UNPLUG-RELEASE-BODY-FINAL.md`이고, **그 파일은 고치지 않았습니다**
> (리더 지시 2026-09-14: 공개 게시물 수정은 다음 게시본 개정에 묶어 보류).
> 이 문서는 **개정할 때 바꿔 넣을 문안과 그 근거**만 담습니다.
>
> 작성 `manual-writer` · 2026-09-14 · 인용 줄은 작업 트리 기준(2026-09-14 밤) — coder 온보딩 커밋 뒤 해시 확정

---

## 0. 먼저 — 어느 빌드와 묶이는가

| 개정 | 언제 반영하는가 | 이유 |
|---|---|---|
| **R1** 5단계 「원래」 | **다음 개정 아무 때나** | 빌드와 무관한 문장 오독이다 |
| **R2** 3단계 보낼 파일 | **`FreezeForensics`가 들어간 빌드를 공개할 때 함께** | 지금 공개 빌드에는 그 폴더가 없다 — 아래 근거 |
| **R3** 1단계 「두 번 켜면 사라집니다」 | **R2와 같은 때** | 같은 빌드부터 사본이 남도록 바뀌었다 |

**공개 빌드와 폴더의 관계**(git 조상 판정, 대조 포함):

| 커밋 | 무엇 | 공개 프리뷰 `1283d74` | 시험 빌드 `a6b3101` | HEAD `7900ad0` |
|---|---|---|---|---|
| `ccfaef9` | `FreezeForensics` 동결 계측 신설 | **없음** | 있음 | 있음 |
| `3cc6753` | 정상 종료 표지 + 비정상 종료 다음 실행의 이전 로그 사본 | **없음** | **없음** | 있음 |
| (대조) `0229f52` | 우클릭 부채꼴 | 있음 | 있음 | 있음 |

⇒ **지금 공개 빌드 사용자에게는 게시본의 「로그 두 파일」 절차가 정확하다.** 앱 로그의
「이 폴더를 통째로 보내 주세요」(`Platform/FreezeWatchdog.cs:266`)는 `ccfaef9` 이후 빌드에만 있다.
그래서 R2 · R3은 **그 빌드를 공개하는 날** 함께 바꿔야 하고, 먼저 바꾸면 지금 사용자에게 없는 폴더를 찾게 한다.

---

## R1. 5단계 — 「원래」 삽입 (KO · EN)

**문제**: 5단계를 읽는 사람은 PC가 멈춘 뒤 **자동 숨기기가 꺼진 채 남은** 사람일 수 있다.
그 사람은 지금 이 순간 「자동 숨기기를 쓰지 않는」 상태인데, StickMate를 켜면 남은 흔적을 보고
**원래 설정으로 되돌리도록** 만들어져 있다(`docs/TASKBAR_REVEAL.md` 2-1 단계 2) — 즉 설정이 바뀐다.
현재형 문장은 그 사람에게 거짓으로 읽힌다. 매뉴얼 1장 6-1은 같은 문장을 2026-09-14 (3)에서 고쳤다.

| | 게시본(현재) | 개정 문안 |
|---|---|---|
| KO `:89` | 자동 숨기기를 쓰지 않았다면 이 단계로 작업 표시줄 설정은 바뀌지 않습니다. | 자동 숨기기가 **원래** 꺼져 있었다면(StickMate 때문에 꺼진 것이 아니라면) 이 단계로 작업 표시줄 설정은 바뀌지 않습니다. |
| EN `:188` | If you do not use auto-hide, this step does not change your taskbar setting. | If auto-hide was **originally** off (not turned off by StickMate), this step does not change your taskbar setting. |

같은 단락의 **「잘 모르겠으면 그냥 해 주십시오」 · 「만들어져 있습니다」 헤지는 그대로 둔다**(Windows 실기 0회).

---

## R2. 3단계 — 「로그 두 파일」과 앱 로그 「폴더를 통째로」의 불일치

### 사실

| 무엇 | 내용 | 근거 |
|---|---|---|
| 게시본 3단계 | `Player.log` · `Player-prev.log` **두 파일**을 바탕 화면 새 폴더에 복사 | FINAL `:65-73` |
| 앱 로그 한 줄 | 「앱이나 컴퓨터가 멈추면 **이 폴더를 통째로** 보내 주세요」 — 이 폴더 = 저장 폴더 아래 `FreezeForensics` | `FreezeWatchdog.cs:263-266` |
| 폴더 위치 | 게시본 2단계가 여는 폴더(`%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate`) **안의** `FreezeForensics` | `FreezeWatchdog.cs:242`(`persistentDataPath` 아래) |
| 폴더 안 ① | `session-exit-marker.txt` — 실행 중 / 종료 시작 / 정상 종료 한 줄 | `SessionExitMarker.cs:63-67` |
| 폴더 안 ② | `freeze-forensics-NN.log` · `freeze-watchdog-NN.log` — 사건이 있을 때만(링 12칸). 머리줄은 앱 버전 · OS · GPU · pid · 시작 시각 UTC | `FreezeForensicsPolicy.cs:89 · :103-105` · `FreezeWatchdog.cs:246-252`(머리줄) → `:254` `FreezeForensicsLog.Activate` |
| 폴더 안 ③ | `previous-abnormal-player-NN.log` — 직전 실행이 정상 종료 표지 없이 끝났을 때 `Player-prev.log` 끝부분 사본(최대 3개, 4MB) | `SessionExitMarker.cs:70-82` |

### 개인정보 관점

- **③은 `Player.log`와 같은 종류의 파일이다.** `Player.log`에는 우리 앱이 스스로 찍는 줄에도
  저장 폴더의 전체 경로가 들어간다(예: `FreezeWatchdog.cs:263` 「폴더 {directory}」, `directory` = `persistentDataPath` 아래).
  그 경로에는 **Windows 사용자 이름**이 들어간다. ⇒ ③도 사용자 이름을 담을 수 있다.
- **②는 머리줄에 사용자 이름이 없다**(버전 · OS · GPU · pid · 시각). 다만 사건 줄의 상세 문자열 전부를
  이 라운드에서 감사하지는 않았다 — **폴더 전체를 `Player.log`와 같은 등급으로 취급한다.**
- ⇒ 폴더를 보내게 해도 **새로 늘어나는 민감도는 없다**(가장 민감한 것이 이미 보내는 두 파일과 같은 종류다).
  늘어나는 것은 **공개 게시판에 올릴 위험의 크기**뿐이므로 「올리지 마십시오」 경고를 폴더에도 그대로 건다.

### ★ 추천 1안 — 「두 파일 + 폴더 하나」를 복사하게 하고, 앱 로그 문구를 게시본에 맞춘다

**채택 이유**
1. **폴더 안에만 있는 것이 이 문제의 핵심 증거다.** ②는 멈춘 순간의 UTC 시각 기록이고(이벤트 뷰어와 맞출 수 있음),
   ③은 앱을 여러 번 다시 켜도 남는 사본이다. 두 파일만 받으면 `ccfaef9` · `3cc6753`을 넣은 이유가 사라진다.
2. **폴더만으로는 부족하다.** 이번 실행의 `Player.log`는 폴더에 없다.
3. **복사 조작이 하나 늘 뿐이다.** 같은 2단계 폴더 안에서 `Ctrl`을 누른 채 하나 더 고르면 된다.

**기각한 안**
- (나) 두 파일만 유지하고 앱 로그를 「두 파일」로 고친다 → 폴더의 증거(①②③)를 버린다.
- (다) 폴더만 보내게 한다 → 이번 실행의 `Player.log`가 빠지고, 사본은 비정상 종료 뒤에만 생긴다.

**게시본 3단계 개정 문안 (R2 빌드 공개 때)**

KO:
> #### 3단계. 파일을 바탕 화면에 복사해 둡니다
>
> 폴더 안에서 `Player.log`, `Player-prev.log` 두 파일과 `FreezeForensics` 폴더를 찾습니다.
> (파일 확장자를 숨기는 설정이면 `Player`, `Player-prev`로만 보일 수 있습니다. 같은 파일입니다.)
>
> 1. `Ctrl` 키를 누른 채로 세 개를 클릭해 모두 선택하고 `Ctrl` + `C`를 누릅니다.
> 2. 바탕 화면에 새 폴더를 만들고, 그 폴더를 열어 `Ctrl` + `V`를 누릅니다.
>
> 복사가 끝났다면, 이제 StickMate를 켜도 멈췄을 때의 기록을 잃지 않습니다.

EN:
> #### Step 3. Copy the files to your desktop
>
> In the folder, find the two files `Player.log` and `Player-prev.log`, and the folder `FreezeForensics`.
> (If your PC hides file extensions, the files may appear as `Player` and `Player-prev`. They are the same files.)
>
> 1. Hold down `Ctrl`, click all three to select them, and press `Ctrl` + `C`.
> 2. Create a new folder on the desktop, open it, and press `Ctrl` + `V`.
>
> After the copy is done, starting StickMate will not remove the records from the freeze.

**4단계 경고 개정 문안** (범위를 폴더까지 넓힌다)

KO:
> **로그 파일과 `FreezeForensics` 폴더는 이 공개 게시판에 올리지 마십시오.** 이 파일들에는 Windows 사용자 이름이 들어간 폴더 경로가 들어 있을 수 있습니다.

EN:
> **Do not upload the log files or the `FreezeForensics` folder to this public issue board.** They can contain a folder path with your Windows user name.

**앱 쪽 제안 (문서 담당 밖 — `coder` 배정은 리더 판단)**: `FreezeWatchdog.cs:266`의
「이 폴더를 통째로 보내 주세요」에 **「공개 게시판에는 올리지 마세요」**를 붙인다.
`Player.log`를 여는 사람이 이 줄만 보고 공개 이슈에 폴더를 끌어다 놓는 경로를 막기 위해서다.
※ 문구 확정은 `design-narrative`와 리더 소관이다. 이 문서는 방향만 적는다.

---

## R3. 1단계 — 「두 번 켜면 기록이 사라집니다」

**문제**: `3cc6753` 이후 빌드는 직전 실행이 정상 종료 표지 없이 끝났으면 `Player-prev.log` 끝부분을
`FreezeForensics`로 **복사해 두도록** 만들어져 있다(`SessionExitMarker.cs:213-217`, 사본 3개 링 `:76`).
그 빌드에서 「두 번 켜면 사라집니다」는 넓다. **Windows 실기 0회**라 「남습니다」로 단정하지 않는다.

| | 게시본(현재) | 개정 문안 (R2 빌드 공개 때) |
|---|---|---|
| KO | **두 번 켜면:** 멈췄을 때의 기록이 사라집니다. | **두 번 켜면:** 멈췄을 때의 기록이 `Player-prev.log`에서는 사라집니다. 이 빌드는 그 기록의 사본을 `FreezeForensics` 폴더에 남기도록 만들어져 있지만, 확실하게 하려면 켜기 전에 3단계를 먼저 해 주십시오. |
| EN | **If you start it twice:** the log from the freeze is lost. | **If you start it twice:** the log from the freeze is gone from `Player-prev.log`. This build is designed to keep a copy in the `FreezeForensics` folder, but to be safe, do Step 3 before you start StickMate. |

「1단계. StickMate를 아직 켜지 마십시오」라는 **지시 자체는 그대로 둔다** — 사본이 실패하는 경로
(로그가 꺼진 실행 · 복사 실패)가 코드에 있다(`SessionExitMarker.cs:354`).

---

## 갱신 이력

| 날짜 | 내용 |
|---|---|
| 2026-09-14 | 첫 작성(리더 배정). R1 「원래」(KO · EN), R2 보낼 파일 불일치 — 추천 1안 「두 파일 + 폴더」와 기각 2안 · 개인정보 판단, R3 1단계 과장(같은 빌드부터). 게시본 FINAL은 **수정하지 않았다**. 공개 빌드 `1283d74`에는 `FreezeForensics`가 없음을 git 조상 판정으로 확인해 R2 · R3을 빌드 공개와 묶었다. |
