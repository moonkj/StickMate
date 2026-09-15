# 작업표시줄 원복 × 로그오프 순서 — 원칙 3 예외 성립 조건 판정 (ROADMAP e-2 메커니즘)

작성: `game-architect` · 2026-09-15 · 문서만 썼다(코드·테스트·체크표·ROADMAP 무수정, Unity·앱·빌드 미실행)
기준: HEAD `3751a10`. `docs/TASKBAR_REVEAL.md`는 작업 트리에 dev-platform 미커밋 변경(삽입 5·삭제 5, 줄 수 불변)이 있다. 읽기만 했고, 줄 번호는 두 판이 같다.
경로 표기: 코드는 `Assets/_Project/Scripts/` 아래 상대 경로다.
체크표 `docs/verify/WINDOWS_CHECK_SESSION.md`의 줄 번호는 **qa-regression이 동시에 편집 중인 작업 트리 판**이다(이 문서 저장 직후 재판독, `git diff --numstat` +197/−6). 다시 밀릴 수 있으니 인용할 때는 앵커 문자열로 찾는다.
정정(2026-09-15 2차 — `verify-change` 조건부 통과 반영): §6 H-2 줄 · §5-2(라) `-timestamps` · §1-1·§3-2·§3-4 XP 절 모호성 · §2-3·§3-2 F 셸 부재 전제(**e-5 신설**, §7) · §1-3 Chen 2012 원문 복원 · 경미 7건(레벨 2 표현 · e-2 한정 · ④(나) 1회 한정 재설계 · ROADMAP 경로 · `:293` · 추론 표기 · N-8 금지 위치). 코드 줄은 `761f5cb`에서도 같다(`3751a10..761f5cb` `Assets/` 변경 0 · 같은 명령의 `docs/` 변경 2건으로 대조).
정정(3차 — `verify-change` 2차 조건부 반영): ④(나) 파서 이름 · ④(나) 탐지력 한계와 「표지 판정 불가」 1줄 요구 · e-5 기동 가족에 「셸 재시작 도중 기동」 병기. 초판(미커밋) 문장이 본문에서 취소선 없이 바뀐 곳은 **문서 끝 부록 「초판(미커밋) 대비 정정 기록」**에 모았다(리더 판정 — 미커밋 파일이라 본문 인라인 취소선은 복원하지 않는다).
정정(4차 — `verify-change` 3차 조건부 반영): ④(나) 옛 표지 기준을 「표지 pid ≠ 흔적 pid ∧ 표지 utc < 흔적 writtenAtUtc」로 교정(같은 실행 표지 오판 제거) · 부록 #5를 5a·5b로 분리해 원문 그대로 · #7 원문 · #20 8곳 · #25·#5 「작성자 기록 기준, 검증자 미확인」 · §3-1 따옴표 행(#26)과 기준 교정 행(#27) 추가.
발단: 리더 배정. dev-platform이 찾은 Raymond Chen 2013 블로그 글이 단서다. 이 문서를 쓰다가 **같은 저자의 2012 글이 e-2를 직접 서술한다**는 것을 추가로 찾았다(§1-3).

---

## 0. 결론 먼저

| 질문 | 답 |
|---|---|
| **판정** | **(c) 판정 불가 — 실기가 필요하다.** 단 방향은 이렇다. ① **메커니즘은 서술이 있다.** Microsoft 직원 블로그(2012)의 요지: Explorer는 `WM_ENDSESSION`에 응답해 설정을 저장하고, 그 뒤 바뀐 설정은 다시 저장되지 않는다(원문 §1-3 · API 계약 아님). ② **리더 가설의 순서(셸 저장 → 우리 ① 원복)는 문서화된 종료 순서 모델에서는 일어나지 않는다.** 모델은 이렇다: 프로세스 단위 직렬 처리, 종료 레벨 높은 순, Explorer 레벨 2(시스템 예약 마지막 범위 — 작업 관리자 1이 그 뒤), 우리 기본 레벨 0x280. ③ **그 모델이 우리 환경에서 성립하는지는 미확인 3건**이다(§3-2 C·D·E). ★ 2차: 저장 순서와 별개로, 셸 부재 시 `ABM_GETSTATE` 반환값에 따라 같은 결과 모양의 경로 **e-5(가칭)**가 있다(§2-3 · §7) — 판정은 e-4와 함께 후속 |
| 셸 저장이 `WM_ENDSESSION` 이후면 안전한가 | **Explorer 자신의 `WM_ENDSESSION`에서 저장한다면 안전하다.** 문서 모델상 그 차례는 우리 프로세스 처리가 끝난 뒤다(§3-3) |
| 셸 저장이 우리보다 앞이면 `WM_QUERYENDSESSION` 원복으로 막히나 | **C·D에서는 막히지 않는다.** 직렬 모델에서 셸 차례가 우리보다 앞이면 우리 `WM_QUERYENDSESSION`도 그 뒤에 온다(C). 셸이 로그오프 개시 시점에 저장한다면(D) 개시가 모든 메시지보다 앞이다. 〔2차 정정: 「모두에게 QUERY → END 차례」 모델에서는 이긴다 — 같은 보관 문서 XP 절이 그렇게 읽히는 문장을 담고 있어 **배제하지 못한다.** ① 기각은 다른 근거로 유지된다(§3-4 · §4)〕 |
| 설계 권고 | **④ 진단 두 줄(쓰기 0) 먼저. ② 「세션 종료 흔적 검증 대기」는 예비안으로 둔다** — R-2에서 거짓 성공이 1회라도 나오면 채택하고 **N-9 토큰 라운드와 묶는다.** ①(QUERY 원복)·③(세션 종료 원복 생략)은 기각. ⑤(자기 종료 레벨 명시)는 ④가 레벨 ≤ 0x0FF를 보일 때만(§4) |
| 판별 실기 | **체크표 §R R-2는 이미 가르는 측정이다** — 2×2 「❌❌ 거짓 성공」 칸(`WINDOWS_CHECK_SESSION.md:379`)이 가설 형태 그대로이고, 로그인 뒤 ON 행(`:378` — 2026-09-15 qa-regression이 이름을 둘로 갈랐다)에는 **e-2(해제가 영속된 경우)**가 들어올 수 없다(「거짓 성공 + 재시작이 되돌림」 같은 다른 거짓 성공은 체크표 `:383`대로 이 행에도 들어올 수 있다). 보강은 셋이다. **개시 경로 분리**(현행 「또는 Ctrl+Alt+Del」이 D를 섞는다), **OS 빌드 기록**, **영속 시점 보조 판독**(선택·읽기 전용). 절차는 §5 |
| 출시 관문 | **새 게이트는 필요 없다** — H-2(출시 차단) = CW-2 안의 한 칸이다. **지금 사용자에게 올릴 사안은 아니다.** R-2/R-2b에서 거짓 성공이 1회라도 나오면 올린다. 그때 ② 채택의 대가(사용자가 직접 바꾼 값을 덮어쓰는 경로가 매일 밤으로 넓어짐)도 함께 선택지로 올린다(§6) |

---

## 1. 순서 사실 — 무엇이 문서에 있고 무엇이 없는가

### 1-1. Microsoft 1차 API 문서 (Learn)

| 문서 | 원문 | 이 판정에 주는 것 |
|---|---|---|
| `WM_QUERYENDSESSION` | *"After processing this message, the system sends the WM_ENDSESSION message with the wParam parameter set to the results of the WM_QUERYENDSESSION message."* | 한 앱 안에서는 QUERY 다음이 END다 |
| 〃 Remarks | *"Each application should return TRUE or FALSE immediately upon receiving this message, and defer any cleanup operations until it receives the WM_ENDSESSION message."* | **정리 작업은 END에서 하라** — 안 ①에 불리하다 |
| `WM_ENDSESSION` wParam | *"If the session is being ended, this parameter is TRUE; the session can end any time after all applications have returned from processing this message."* | 반환 전에 원복을 끝내야 한다(현행 설계와 같다) |
| Logging Off | *"When an application returns TRUE for WM_QUERYENDSESSION, it receives the WM_ENDSESSION message and it is terminated, regardless of how the other applications respond to the WM_QUERYENDSESSION message."* | QUERY에 TRUE를 준 앱은 **로그오프가 나중에 취소돼도** 끝난다 |
| Application Shutdown Changes in Windows Vista(보관 문서, 2011) — XP 절 | *"Once an application responds to WM_ENDSESSION, Windows closes it. Shutdown then continues, as Windows sends WM_QUERYENDSESSION to the remaining running applications in turn."* | 앱 단위 직렬(한 앱의 QUERY→END→닫기 뒤 다음 앱)로 읽힌다. ★ 2차 정정 — **같은 절에 반대로 읽히는 문장이 있다**: *"If every top level window returns TRUE to WM_QUERYENDSESSION, then each is sent WM_ENDSESSION with wParam == TRUE, in turn."*(모두 QUERY → END 차례). **이 페이지의 XP 서술은 모호하다** |
| 〃 Vista 절 | *"… even though the system sends WM_QUERYENDSESSION and WM_ENDSESSION serially to applications."* · *"… because it will not have been sent WM_QUERYENDSESSION yet."* | Vista에서도 직렬이다. 뒤 차례 앱은 **아직 QUERY도 못 받은** 상태다 |
| `SetProcessShutdownParameters` | *"The system shuts down processes from high dwLevel values to low."* · *"All processes start at shutdown level 0x280."* · 표 *"000-0FF — System reserved last shutdown range."* | 프로세스 간 순서는 **레벨 높은 순**이다. 우리는 기본값 0x280이다(우리 코드 호출 0건 — 아래 §2-4) |

★ **1차 API 문서에 없는 것**
- **Explorer(셸)가 작업표시줄 설정을 언제 저장하는지.** `ABM_SETSTATE` 문서는 *"Sets the autohide and always-on-top states of the Windows taskbar."*뿐이다(`TASKBAR_REVEAL.md:162` 인용과 같다).
- Explorer의 종료 레벨 값.
- Windows 10/11에서 위 직렬 모델이 그대로인지(Vista 문서는 보관 문서다). 그 보관 문서 안에서도 XP 절 서술이 두 방향으로 읽힌다(위 표).

### 1-2. Microsoft Press — *Windows Internals, Part 2, 6th Edition*(2012) 발췌 (API 계약 아님)

- *"Csrss … loops through all the processes in the logon session of the interactive user … in reverse order of their shutdown level."*
- *"Explorer, for example, sets its shutdown level to 2 and Task Manager specifies 1."*
- *"Csrss sends the WM_QUERYENDSESSION message to each thread in the process that has a Windows message loop. If the thread returns TRUE, the system shutdown can proceed. Csrss then sends the WM_ENDSESSION Windows message to the thread to request it to exit."*
- 인용은 WebFetch 요약 도구가 돌려준 원문 조각이다(생략 부호 포함). 7판(Windows 10)에 같은 서술이 있다는 것은 **검색 요약으로만 봤고 원문은 확인하지 못했다.**

### 1-3. Microsoft 직원 블로그 — Raymond Chen, *The Old New Thing* (참고 서술, API 계약 아님)

| 글 | 원문 조각 | 뜻 |
|---|---|---|
| **2012-07-05 「How your taskbar auto-hide settings can keep getting overwritten」** ★ 이번에 새로 찾음 | *"What if the application tries to restore the state after Explorer has already saved its settings? When the user logs off, all processes are told to clean up their toys and to go bed. In response to WM_ENDSESSION, Explorer saves out its settings and calls it a night. What if this happens before the application programmatically unchecks the box? Explorer says, “Okay, I unchecked the box.” But Explorer already saved out its settings; these updated settings aren’t going to be saved again."* | **ROADMAP e-2(`docs/strategy/ROADMAP.md:4412`)의 메커니즘을 거의 그대로 서술한다.** 저장 시점은 「Explorer가 받은 `WM_ENDSESSION`」이다. 순서가 보장되는지·가능한지는 **말하지 않고** 「What if」로만 둔다 |
| 2013-08-01 「The case of the auto-hide taskbar」(dev-platform 단서) | *"Explorer always writes that value when you log off, and the value written is the taskbar's current auto-hide state."* | 로그오프 때 **그 순간의 값**을 쓴다. 이 글의 원인 앱은 **원복을 아예 안 했다** |

- 두 글 모두 결론이 같다(요약, 원문 아님): 전역 설정을 프로그램이 바꾸지 말고 국지적 해법(전체화면 창)을 쓰라. 여러 앱이 같은 짓을 하면 원복끼리 충돌한다. ★ **국지적 해법은 우리 요구에 적용되지 않는다** — 사용자 지시는 「숨은 막대를 보이게」이고, 남의 막대를 보이게 하는 국지적 수단은 없다. 이 차이는 사실로만 적는다. 충돌 경고는 §7 e-4로 넘긴다.
- 2012 글 인용은 **원문 HTML을 받아 대조했다**(2차). 첫 판은 요약 도구가 돌려준 조각이라 「… and calls it a night」 생략과 안쪽 인용부호가 원문과 달랐다(`verify-change` 적발). 원문의 「to go bed」 오탈자와 굽은 따옴표는 그대로 옮겼다. 첫 문장이 우리 경우(「Explorer가 이미 저장한 뒤 원복하면?」)를 질문으로 세운다.

### 1-4. 조립 — 문서 모델대로라면 로그오프는 이렇게 흐른다

```
로그오프 개시
 └ Csrss: 레벨 높은 순으로 프로세스 하나씩
     ├ … 레벨 0x3FF~0x281 앱들 …
     ├ StickMate (0x280, 기본)      QUERY → TRUE(DefWindowProc)
     │                              END(wParam=TRUE) → ⓪ 표지 → ① 원복 W(되읽기 ON) → 흔적 닫기 → ②③④ → 반환 → 종료
     ├ … 같은 레벨·낮은 레벨 앱들 …
     ├ Explorer (2)                 QUERY → END → ★ 설정 저장 S (이 순간의 값 = W가 쓴 ON)
     └ 작업 관리자 (1)
```

**이 모델에서는 W < S다.** 우리가 되돌린 값을 셸이 저장하므로 가설의 거짓 성공은 **생기지 않는다.**
모델이 흔들리는 자리는 §3-2 표의 C·D·E뿐이다.

---

## 2. 우리 순서 사실 (코드 줄, HEAD `3751a10`)

### 2-1. 입구 — 무엇이 순서를 시작하나

| 신호 | 수신 | 줄 |
|---|---|---|
| **`WM_ENDSESSION`(wParam≠0)** | 트레이 **숨은 호스트 창**(부모 없는 `WS_POPUP`, `WS_VISIBLE` 없음 — `Platform/Windows/WindowsSystemTrayIcon.cs:491-497`)의 프로시저 `:525`. 맨 앞 `:535`가 `AppShutdownSequence.TryHandleSessionEndMessage(message, wParam, lParam)`를 부르고, 참이면 `:537`에서 0을 반환한다 | 판정 `Platform/AppShutdownSequence.cs:289-294` → `SessionEndPolicy.IsSessionEndingNow` `:457`(`message == WmEndSession && wParam != 0`) → `HandleSessionEnding` `:320` → `Run(SessionEnding)` `:322` → `Application.Quit()` `:336` |
| **`WM_QUERYENDSESSION`** | **처리하지 않는다.** 프로시저 끝 `DefWindowProc` `:560`이 TRUE(종료 허용)를 돌려준다(주석 `:531`). 상수 `WmQueryEndSession` `AppShutdownSequence.cs:421`은 **선언뿐이다** — 프로덕션 참조 1건이 그 선언 자신이다(grep 양성 대조: `WmEndSession` 계열은 판정식 `:457`에서 적중) | — |
| **`Application.quitting`** | `AppShutdownSequence.EnsureQuitHookInstalled` `:182-187` → `OnApplicationQuitting` `:189` → `Run(ApplicationQuitting)`. 설치 호출은 `ReservedBarRevealDirector.cs:322`와 `FreezeWatchdog.cs:257` | — |

- 로그오프에서 Unity가 `quitting`을 부르는지, Unity 자신의 창이 `WM_ENDSESSION`을 먼저 받아 프로세스를 끝내는지는 **실기 미확인**이다(`TASKBAR_REVEAL.md:81`). 어느 쪽이든 **원복이 안 돌면 흔적이 열린 채 남는다**(e-1 모양). 거짓 성공이 아니다.

### 2-2. 순서와 원복

- 순서 배열 `AppShutdownSequence.cs:136-143`: ⓪ `MarkExitStarted` → ① `RestoreReservedBar` → ② `StopFreezeWatchdog` → ③ `FlushProgressSave` → ④ `MarkCleanExit`. 실행 `Run` `:256-277`, ① 실행 `:352-354` → `ReservedBarRevealDirector.RunShutdown(trigger)`.
- `Platform/ReservedBarRevealDirector.cs` `RunShutdown` `:260-300`:
  - 조회 `:267`(`TryReadAutoHide`) → 판정 `:269-270`(`ResolveQuit`) → **시스템 쓰기 `:280`**(`TrySetAutoHide(원래 값)`) → 재진입 가드 `:285` → **흔적 닫기 `:287`**(`ReservedBarRestoreLedger.Close`) → 성공 로그 `:291-292`(「종료합니다 — … 원래대로 되돌렸습니다」, 문장 정본 `ReservedBarRevealPolicy.cs:214-215`) → `ChangedThisSession = false` `:293`.
- 흔적을 여는 쪽은 기동이다: `:204` `Open(observed)`(write-ahead, `Flush(true)`) → `:212` 해제.
- 흔적 닫기 `Platform/ReservedBarRestoreLedger.cs:198-199` → `TryWrite` `:201-246`(`active=false`, `writtenAtUtc` `:221`, `Flush(true)` `:234`).

### 2-3. 되읽기 — 무엇을 확인하고 무엇을 확인하지 않는가

- `Platform/Windows/WindowsReservedBarAutoHideControl.cs` `TrySetAutoHide` `:114-157`: `SHAppBarMessage(ABM_SETSTATE)` `:130` → **되읽기 `:139-156`** — `ABM_GETSTATE`로 다시 읽어 요청값과 다르면 false를 돌려준다.
- ★ **되읽기가 확인하는 것은 `ABM_GETSTATE`의 반환값이다.** 이것을 「Explorer가 지금 메모리에 든 상태」로 보는 것은 **추론**이다(1차 문서 없음). 「Explorer가 디스크(사용자 프로필)에 저장한 값」을 읽지 않는 것은 **코드 사실**이다(이 파일에 레지스트리 접근 0 — 파일 전체 판독). 흔적은 이 반환값 확인만으로 닫힌다(`Director:287`). **e-2가 끼어들 수 있는 틈이 이 자리다** — 반환값 = 원래 값, 영속 = 바뀐 값인 채로 흔적이 닫히는 경우다(e-5도 같은 자리 — 아래).
- 셸이 이미 없을 때 — ★ 2차 정정: 첫 판의 「셸 선종료가 거짓 성공을 만들지는 않는다」는 **전제(반환값 0)가 빠진 단정**이었다(`verify-change` 적발). `TryGetState` `:160-175`는 예외가 없으면 `SHAppBarMessage` 반환값을 그대로 상태로 쓴다(`:166`). **막대가 없을 때 `ABM_GETSTATE`가 무엇을 돌려주는지는 1차 문서가 없다.** 반환값에 따라 갈린다:
  - **AUTOHIDE 비트가 꺼진 값(예: 0)이면** → 「자동 숨김 꺼짐」으로 읽힌다. 원복 목표값은 항상 **켜짐**이라(기동 해제는 원래 켜짐일 때만 — `ReservedBarRevealPolicy.cs:168,171`) 되읽기가 어긋나 false → **흔적은 열린 채 남는다** → e-1.
  - **AUTOHIDE 비트가 선 값이면** → 종료 조회(`Director:267`)가 「이미 원래 값」으로 읽는다 → `ResolveQuit`가 `QuitAlreadyMatched`(`ReservedBarRevealPolicy.cs:188-192`) → **시스템 쓰기 없이** 흔적 닫기(`Director:287`) → **거짓 성공 모양.** 영속 값은 셸이 내려가며 저장한 해제 값일 수 있다 → **e-5(가칭)**. 원칙 3 성립 조건에 걸리는 새 경로이고, 판정은 e-4와 함께 후속으로 넘긴다(§7).
  - ⇒ **셸 선종료가 거짓 성공을 만드는지는 셸 부재 시 반환값에 달렸다 — 미확인.**

### 2-4. 종료 레벨 — 우리 코드는 건드리지 않는다

- `SetProcessShutdownParameters`·`GetProcessShutdownParameters` 호출은 `Assets/` 전체에서 **0건**이다. grep 형태의 양성 대조로 같은 명령이 `SHAppBarMessage`를 `Platform/`에서 7건 잡았다.
- ★ **Unity 플레이어(네이티브)가 자기 레벨을 바꾸는지는 알 수 없다**(비공개). 문서 모델의 전제 「우리 = 0x280」은 **우리 코드 기준으로만 참이다.**

---

## 3. 판정

### 3-1. 가설 형태가 성립하는 조건 (셋 다 필요)

1. Explorer의 **영속 저장 S**가 우리 **원복 쓰기 W**보다 앞이다.
2. W 뒤에 영속 저장이 **다시 없다**(2012 글: *"aren’t going to be saved again"*).
3. W의 되읽기가 성공한다(= Explorer가 W를 메모리에 반영할 만큼 살아 있다) → 흔적이 닫힌다.

### 3-2. 경우표

| # | 환경 | 근거 | S와 W | 결과 |
|---|---|---|---|---|
| A | 문서 모델 성립 — 직렬 · 레벨 높은 순 · Explorer 레벨 2 · 셸 저장 = Explorer 자신의 `WM_ENDSESSION` | §1-1 · §1-2 · §1-3 2012 | W < S | **안전.** 셸이 우리가 되돌린 값을 저장한다 |
| B | 설정 변경이 **즉시** 영속된다(Windows 10/11 Explorer가 바뀔 때마다 씀) | 1차 없음. 비1차 서술 1건(Windhawk PR 봇 코멘트 *"Explorer persists it (StuckRects3)"*, `docs/strategy/ROADMAP.md:4428`) | W 자체가 영속 | **안전.** 조건 3이 성립하는 한 W가 곧 영속이다 |
| **C** | **우리 프로세스 레벨 ≤ 2** (Unity 플레이어가 낮춤 등) | 미확인(§2-4) | S < W 가능 | **(a) 가능** |
| **D** | Explorer가 **로그오프 개시 순간**(시작 메뉴에서 「로그아웃」을 누를 때 — 개시자가 Explorer 자신)에 먼저 저장한다 | 1차 없음. 2012 글은 「`WM_ENDSESSION`에 응답해」라고 쓴다 — D의 **반대 방향**이지만 배제하지는 못한다 | S < 모든 메시지 | **(a) 가능.** QUERY 원복도 늦다 |
| **E** | Windows 10/11이 직렬 모델을 바꿨거나, 원래부터 「모두에게 QUERY → END 차례」였다(또는 병렬) | 1차는 Vista 보관 문서뿐이고, 그 문서 XP 절이 두 방향으로 읽힌다(§1-1) | 불명 | **불명** |
| F | 셸이 우리보다 먼저 끝났다 | §2-3(셸 부재 시 `ABM_GETSTATE` 반환값 — 1차 문서 없음) | 반환값에 AUTOHIDE 비트 없음 → W 되읽기 실패 / 비트 있음 → 쓰기 없이 `QuitAlreadyMatched` | 비트 없음: e-1(흔적 열림) · **비트 있음: 거짓 성공 모양 = e-5(가칭), 판정 후속(§7)** |
| G | 우리 수신 창이 `WM_ENDSESSION`을 못 받았다 | §2-1 | W 없음 | e-1 — **거짓 성공 아님** |

**⇒ (c) 판정 불가.** 문서 모델(A)과 즉시 영속(B)에서는 **(b) 불가**다. 우리 기계가 A·B 중 어디인지, C·D·E가 아닌지는 **Windows 실기로만 갈린다.**
★ e-2가 「조용히」 성립하는 길은 C·D·E 셋이다(같은 결과 모양의 별개 경로 e-5는 F행 — 저장 순서가 아니라 셸 부재 시 조회값 문제다). 셋 다 **환경·개시 경로 의존**이라, 실기 1회 통과는 **부재 증명이 아니다**(§5-1).

### 3-3. 「셸 저장이 `WM_ENDSESSION` 이후라면 안전한가」

- 「**Explorer가 받은** `WM_ENDSESSION`」 이후라면 → A → **안전하다.** 문서 모델에서 Explorer 차례는 우리 프로세스가 반환·종료된 뒤다.
- 「**우리가 받은** `WM_ENDSESSION`」 이후라면 → 정의상 W < S → **안전하다.** 단 W가 반환 전에 끝나야 한다 — 현행 설계가 그렇다(`TASKBAR_REVEAL.md` §2-2 우선순위 (1), `AppShutdownSequence.cs:108`).

### 3-4. 「셸 저장이 우리보다 앞이면 `WM_QUERYENDSESSION` 원복으로 막히나」

- **C(레벨 역전)**: 직렬 모델에서 Explorer 차례가 우리보다 앞이면 우리 QUERY도 Explorer 처리가 끝난 **뒤에** 온다 → **늦다.**
- **D(개시 시점 저장)**: 개시가 모든 메시지보다 앞이다 → **늦다.**
- **E의 한 변형**(모두에게 QUERY 선발송, END는 차례)에서는 QUERY 원복이 이긴다. ★ 2차 정정 — 첫 판은 이를 「Vista 문서 *"serially"*와 어긋나는 가정」이라 했는데 **과장이었다.** 같은 보관 페이지 XP 절에 *"If every top level window returns TRUE to WM_QUERYENDSESSION, then each is sent WM_ENDSESSION with wParam == TRUE, in turn."*이 있어 **페이지 안 서술이 모호하다.** Vista 절의 *"serially"*도 QUERY와 END를 앱마다 붙여 보내는지까지는 말하지 않는다.
- ⇒ **QUERY 원복이 가설의 거짓 성공을 막는다는 근거는 약하다** — C·D에서는 늦고, 모호한 E 변형에서만 이긴다. ①의 기각은 이 항목이 아니라 **다른 근거로 유지된다**: 1차 지침 *"defer any cleanup operations until it receives the WM_ENDSESSION message"* · 로그오프가 취소되면 실행 중에 자동 숨김이 돌아옴 · 테스트 충돌(§4 ①).

---

## 4. 설계안 비교 — 문안만, 구현 금지

N-8 표시 변경 경로 목록(`docs/verify/DISPLAY_CHANGE_PATH_FILES.md`) 대조:
- `AppShutdownSequence.cs`·`SessionExitMarker.cs` = **X(알려진 제외)**(`:80-81`), `WindowsSystemTrayIcon.cs` = **X**(`:82`).
- `ReservedBarRevealDirector.cs`·`ReservedBarRestoreLedger.cs`·`ReservedBarRevealPolicy.cs`·`WindowsReservedBarAutoHideControl.cs` = **목록 밖**. 파일명 grep 0건이고, 같은 문서에서 `.cs` 93건이 적중해 grep이 살아 있음을 확인했다. `:315`도 Director를 「목록 밖」으로 적는다.
- ⇒ **아래 어느 안도 N-8 결속을 깨지 않는다.** 새 파일을 만들면 그 파일도 목록 밖이어야 한다(경계 규칙 `:17-32` — 화면 변경 경로·오버레이 창 상태와 무관해야 함).

| 안 | 무엇 | e-2(C·D·E)에 효과 | 원칙 3 | 사용자가 겪는 것 | 충돌 | 되돌릴 수 없는가 |
|---|---|---|---|---|---|---|
| **①** QUERY에서 원복 | `WM_QUERYENDSESSION` 수신 즉시 ① 원복 | C·D에서는 **없음**(늦다 §3-4). E 변형(모호 — 배제 못 함)에서만 효과 | 쓰기 형태는 불변. 단 **로그오프가 취소되면**(다른 앱이 FALSE) 실행 중에 자동 숨김이 돌아온다 → 「실행 중에만 해제」가 뒤집힌다. 재해제 경로는 4차가 기각했다(`TASKBAR_REVEAL.md:88`). 그리고 Logging Off 문서상 QUERY에 TRUE를 준 앱은 어차피 끝난다 | 취소된 로그오프 뒤에도 앱은 사라지고 막대는 숨는다 | 1차 지침 *"defer any cleanup … until … WM_ENDSESSION"* 위반. `TASKBAR_REVEAL.md:87`. 테스트 `SessionEndShutdownTests.cs:168` `세션_종료는_WM_ENDSESSION이고_wParam이_참일_때만이다` · `:198`·`:219` X1g · `:1396` 트레이 프로시저 모양 대조 | 아니다(코드) | 
| **②** 세션 종료 흔적을 「검증 대기」로 두고 다음 실행이 닫는다 | `SessionEnding` 경로에서만 W 성공 뒤 흔적을 닫지 않는다. 대신 「원복함·영속 미확인」 상태로 둔다. 다음 기동이 실제 값 = 원래 값을 확인하고 쓰기 없이 닫는다(현행 `LeftoverAlreadyMatched` `ReservedBarRevealPolicy.cs:143-147`의 확장). `quitting` 경로는 지금처럼 닫는다 — 셸이 살아서 계속 돌고 로그오프 저장 경합이 없다 | **흡수한다.** e-2가 나면 다음 기동에 값이 어긋나 `RecoverLeftover`로 갚는다 | **성립이 회복된다** — e-2가 크래시 복구 조항(CLAUDE.md 원칙 3 「다음 실행이 디스크 흔적을 보고 먼저 복구」) 안으로 들어온다. ★ **대가**: 「로그오프 → 다음 실행」 사이에 사용자가 **직접** 자동 숨김을 끄면 다음 실행이 그것을 **다시 켠다**(흔적만으로 구별 불가). 지금은 크래시 경로에만 있는 성질이 **앱을 켠 채 로그오프·재시작하는 매일 밤 경로로 넓어진다** | e-2 발생 시: 다음 로그인에 막대가 보인다 → 앱을 켜면 복구된다(e-1과 같은 모양). **앱을 다시 안 켜면 남는다**(`docs/strategy/ROADMAP.md` §60-3 근거 3과 같은 한계) | 흔적 의미 변경 → **흔적 형식**이 바뀐다. 공개 프리뷰가 v1 판독기를 이미 내보냈다(`docs/strategy/ROADMAP.md:4724`). 토큰 명세 **TK-41** `세션_종료_경로도_토큰_파생_흔적을_닫는다`(`RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md:144`). 테스트 `SessionEndShutdownTests.cs:849`·`:853`·`:911`(N3)·`:1315`·`:1354`(`Closed` 단언). 체크표 §R 2×2 의미(세션 종료 뒤 `active = True`가 정상 칸이 된다 — `:375-379`·`:448-449` 재작성). marketing N2 R13(`TRUTH_INVENTORY.md:109,370`). 기동 로그 문장(매일 아침 「흔적이 남아 있었지만…」이 찍히지 않도록 새 상태가 필요) | ★ **예 — 흔적 형식·의미.** v1 판독기가 새 형식을 어떻게 읽는지 먼저 정한다. `active = true`로 두면 v1은 `Open`으로 읽어 `LeftoverAlreadyMatched`/`RecoverLeftover`로 간다 — **안전한 방향**. 스키마를 올리면 v1이 `NewerSchema`로 보존하고 그 실행의 해제를 쉰다 — 기능이 꺼지는 방향. **N-9와 한 라운드로 한 번만 바꾼다** |
| **②-b** ②인데 불일치면 쓰지 않고 경고만 | 다음 기동이 값 불일치를 보면 쓰기 0, 로그만 | e-2를 **못 갚는다** | 성립 **미회복**. 사용자 변경 덮어쓰기는 없다 | e-2면 막대가 계속 보인다 | ②와 같은 형식 변경 | ②와 같다 |
| **③** 세션 종료에서는 원복하지 않고 흔적만 남긴다 | `SessionEnding`에서 ① 생략 | e-2는 없어진다. 대신 **A(문서 모델)에서 매 로그오프마다 셸이 우리 해제값을 저장한다** → 「가능성 미확인의 거짓 성공」이 **「매일 밤 확정 잔존」**으로 바뀐다 | 「종료 시 원복」 조건을 **일상 경로에서** 깬다 | 매일 아침 막대가 보이고, 앱을 켜야 돌아온다 | 5-b 우선순위 (1)과 정반대다. `SessionEndShutdownTests.cs:839`·`:892`(N3)·`:1212`(재진입 16건). TK-41 | 흔적 의미 변경(②와 같은 무게) |
| **④** ★ 진단만 — 행동 무변경 | (가) 기동 1회 `GetProcessShutdownParameters`로 **자기 프로세스 종료 레벨**을 한 줄 로그(Windows 사실 조회, 읽기). (나) 기동 때 **닫힌** 흔적의 `originalAutoHide` ≠ 지금 값이면 한 줄 경고: 「e-2·e-5 후보 **또는** 사용자가 직접 바꿈 — 쓰지 않음」. ★ **2차 재설계**(`verify-change` 적발): 프로덕션 흔적의 `originalAutoHide`는 **항상 참**이다. 사용자가 나중에 자동 숨김을 직접 끄면 기동은 `AlreadyVisible`로 흔적을 건드리지 않는다(`ReservedBarRevealDirector.cs:192-199`). 그래서 조건 없이 두면 **그 사용자에게 매 기동 영구 반복**되고 e-2와 정상 사용을 못 가른다. ⇒ **「흔적을 닫은 실행 바로 다음 기동 1회」로 한정한다**: 흔적 `pid`(닫기 쓰기가 그 실행의 pid를 적는다 — `ReservedBarRestoreLedger.cs:208-222`) == 직전 실행 표지의 `pid`(`SessionExitMarkerPolicy.TryParse` — `Platform/SessionExitMarker.cs` 클래스 `:60`, 메서드 `:94`)일 때만. 표지는 원복 판정(`BeforeSceneLoad`) 시점에 아직 직전 실행의 것이다(표지 기동은 `AfterSceneLoad` — `AppShutdownSequence.cs:497-498` 주석 기준, 구현 라운드가 확인). 그 뒤 기동은 표지 pid가 바뀌어 조용하다(pid 재사용으로 드물게 한 번 더 찍힐 수 있다. 또 pid 조회 API가 예외를 던져 흔적·표지 중 한쪽만 pid 0으로 기록되면 같은 실행의 표지도 「옛 표지 의심」으로 찍힐 수 있다 — `ReservedBarRestoreLedger.cs:213`·`FreezeWatchdog.cs:244`의 catch, 4차 검증). ★ **탐지력 한계**(3차 — `verify-change` 2차): 표지는 데스크톱 플레이어에서만 켜진다(`FreezeForensicsPolicy.cs:100` `ShouldActivate` — 에디터 제외). 그리고 직전 실행의 표지 기동이 예외로 실패하면 **더 옛 표지가 남아** pid가 어긋나고, 경고가 **조용히 0회**가 된다. ⇒ **구현 요구 추가: 표지 판정 불가(파일 없음 · 파싱 실패 · 옛 표지 의심)면 그 사실을 1줄 남긴다.** 옛 표지 의심의 기준: **표지 `pid` ≠ 흔적 `pid` ∧ 표지 `utc=`(쓰기 형식 `SessionExitMarker.cs:85-91`) < 흔적 `writtenAtUtc`**. 〔4차 정정(`verify-change` 3차) — 3차 기준 「utc < writtenAtUtc」 단독은 **같은 실행의 표지를 옛 표지로 오판했다.** ⓪ 종료 시작 표지는 ① 원복보다 먼저 쓰이고(`AppShutdownSequence.cs:138-139`) 디스크 동기화도 없다(`SessionExitMarker.cs:250` `flushToDisk: false`). 그래서 ①~④ 사이에서 끊기면 같은 pid의 `exit-started`가, ⓪이 유실되면 같은 pid의 기동 표지 `running`(`:224`)이 흔적보다 이른 utc로 남는다. 그 상태가 하필 체크표 「원복 뒤 끊김」 칸이다. 3차 근거 「①은 ④보다 앞」은 `clean-exit`(`:266`)에만 성립했다.〕 **두 후보 비교** — (i) 「`state=clean-exit`에만 utc 기준 적용」: 같은 실행 오판은 없다. 그러나 옛 표지가 `running`·`exit-started`로 남은 경우(더 옛 실행이 크래시했거나 도중에 끊김)를 못 잡아 **조용한 0회가 남는다.** (ii) 「pid 불일치 ∧ utc가 이르다」: 같은 실행이면 pid가 같으므로 **「원복 뒤 끊김」 오판이 구조적으로 0**이다. 옛 표지의 state와 무관하게 잡는다. 흔적을 닫은 실행이 표지를 하나도 남기지 못했다면(기동 쓰기 `:224` 실패는 `:232`에서 삼켜진다) 남은 표지는 그보다 앞선 **다른** 실행의 것이라, 이 조건이 정확히 그 경우다. 흔적을 직전 실행이 닫지 않은 정상 경우(더 옛 실행이 닫음)는 표지 utc가 흔적보다 늦어 걸리지 않는다. ⇒ **(ii)를 채택한다.** 남는 한계: 실행 사이에 시스템 시계가 뒤로 갔거나 pid가 재사용되면 틀릴 수 있다. 두 인스턴스가 겹치면 이 줄이 찍힐 수 있다(그때는 실제로 판정 불가다). 현재 파서는 `utc`를 읽지 않으므로 방법은 구현 라운드 몫이다(X 파일 수정 여부는 리더 확인). 표지가 계속 고장 난 환경에서는 이 줄이 매 기동 찍힌다 — 표지 고장 자체가 알릴 사실이다. `Read`는 닫힌 흔적도 내용을 돌려준다(`ReservedBarRestoreLedger.cs:169-171`) → **스키마 변경 0** | 막지 않는다. **C를 사실로 바꾸고(가), 현장 로그로 A·B/C·D·E를 가를 재료를 남긴다(나)** | 쓰기 0 → 무관. 원칙 3 감사의 금지 5종·허용 형태(`ABM_SETSTATE` 파일 1개·형태 2개)와 무관하다 | 없음. 24시간 상주 로그 규칙: (가) 기동 1줄, (나) 흔적을 닫은 실행 바로 다음 기동에서 불일치일 때만 1줄 | (가)는 `WindowsReservedBarAutoHideControl.cs`에 넣지 않기를 권한다 — 그 파일은 「시스템 전역 쓰기 유일 파일」(`:11`)이라 성격을 섞지 않는다. 판정 문장은 중립 `ReservedBarRevealPolicy`로(패리티 규칙). ★ **N-8 금지 위치**(2차): (가)·⑤를 `docs/verify/DISPLAY_CHANGE_PATH_FILES.md` 목록 등급 A~E 파일에 넣지 않는다 — 특히 기동·종료 계측이라 붙이기 쉬운 **C등급 `FreezeWatchdog.cs`·`FreezeForensicsLog.cs`·`FreezeForensicsPolicy.cs`**, A등급 `Win32WindowService.cs`·`WindowsOverlayStateEnforcer.cs`, B등급 `StickmanAgent.cs`. X(알려진 제외) `SessionExitMarker.cs`는 (나)가 공개 `TryParse`를 **호출만** 하고 수정하지 않는다. 표지 경로도 C등급 파일을 수정하지 않고 얻는다(방법은 구현 라운드 몫). 새 파일은 경계 규칙(`:17-32`)상 목록 밖이어야 한다. macOS는 제어기가 없어 (나)가 구조적으로 안 돈다 | 아니다 |
| **⑤** 자기 종료 레벨 명시 | `SetProcessShutdownParameters(0x280 이상)`을 기동 때 한 번 | **C만** 닫는다. D·E에는 효과 없음 | 우리 프로세스 속성이지 사용자 자산이 아니다. 단 새 Win32 쓰기 표면이다 | 없음(레벨이 원래 0x280이면 무변화). 레벨을 0x3FF처럼 올리면 로그오프 취소 전에 먼저 끝나는 경우가 조금 늘어난다(4차 X1 「앱이 조용히 사라진다」와 같은 가족) | 새 API — 감사 대장에 항목 추가 필요 | 아니다 |

### 4-1. 권고와 순서 — 근거와 함께

1. **④를 먼저 한다.**
   - 되돌릴 수 없는 것이 하나도 없다.
   - C(레벨 역전)는 로그 한 줄로 **사실이 된다** — 지금은 추측이다.
   - (나)는 CW-2 낮 세션 1~2회가 못 잡는 환경 의존(C·D·E와 e-5)을 **공개 프리뷰·출시 후 사용자 로그**로 계속 잰다(흔적을 닫은 실행 바로 다음 기동 1회만 찍는다).
   - ★ 넣을 자리: N-9 토큰 라운드 빌드에 같이 싣기를 권한다. CW-2는 **토큰 뒤 빌드에서만 닫힌다**(`docs/strategy/ROADMAP.md:4724,4749`). 따로 빌드하면 §R 판독 대상 빌드가 하나 더 늘어난다.
2. **②는 예비안으로만 설계해 둔다.**
   - 채택 조건: **R-2/R-2b에서 「❌❌ 거짓 성공」이 1회라도 나오거나, ④(가)가 레벨 ≤ 0x0FF를 보이거나, ④(나) 경고가 현장 로그(서로 다른 사용자·세션)에서 반복 보고된다.** (한 사용자에게 기동마다 반복되는 형태는 재설계로 없앴다.)
   - 선제 채택하지 않는 이유: 문서 모델(A)에서 e-2 확률은 낮다. 반면 ②의 대가(사용자 직접 변경 덮어쓰기 확대)는 **확정적으로 매일 밤 경로에 생긴다.**
   - 채택하면 **N-9와 같은 라운드**에서 흔적 형식을 한 번만 바꾼다. 두 번 바꾸면 §R 판독이 두 번 무효가 된다.
3. **⑤는 ④(가) 결과에 달렸다.** 레벨이 기본값이면 할 일이 없다.
4. **①·③은 기각한다**(위 표).

---

## 5. 판별 실기

### 5-1. §R R-2가 이미 가르는 측정인가 — **그렇다. 단 1회 통과는 부재 증명이 아니다**

- 2×2(`WINDOWS_CHECK_SESSION.md:375-379`):
  - 「자동 숨김 **OFF** × `active = False`」 = **❌❌ 거짓 성공**(`:379`) — 리더 가설의 결과 형태(로그인 뒤 OFF + 흔적 닫힘) **그대로다.**
  - 「✅ 원복 성공」 행은 **로그인 뒤 ON**(`:378`)이다. ⇒ **e-2(해제가 영속된 경우)는 이 행에 들어올 수 없다** — 체크표 `:383`이 적듯 「거짓 성공 + 재시작이 되돌림」 같은 다른 거짓 성공은 이 행에도 들어올 수 있다. 다음 실행 기대 줄도 갈린다(`:449` 거짓 성공 → 「자동 숨김이 원래 꺼져 있습니다」).
  - ★ 2026-09-15 qa-regression 동시 편집: ON 행을 「원복 성공 — 원복 줄 확인」/「원래대로 보임 — 원복 주체 미확정」으로 갈랐다(`:378`, 판독 `:381` 이하). R-2는 「우리 원복」 확정에 쓰지 않는다고도 적었다(`:456`). **이 변경은 e-2 판별을 건드리지 않는다** — e-2는 OFF 행(`:379`)에만 들어가고, 그 칸은 그대로다.
- marketing N2 R13의 ⓐ~ⓓ(`TRUTH_INVENTORY.md:109`)는 「로그인 뒤 ON이 **우리 원복 덕인가, 로그오프가 되돌린 덕인가**」를 가르는 **귀속** 질문이다. e-2 배제에는 「로그인 뒤 ON + `active = False`」면 그 회차에 한해 충분하다. 갈린 두 이름은 모두 ① ON을 전제로 한다(`:378`) — **e-2 배제에 필요한 조건(로그인 뒤 ON)은 남아 있다.** R-2를 「우리 원복」 확정에 쓰지 않는다는 `:456`과도 충돌하지 않는다(e-2 배제는 귀속이 아니라 OFF 행의 부재만 본다).
- ★ **한계**: C·D·E는 환경·개시 경로에 의존한다. 한 기계·한 개시 경로의 ✅ 1회는 「그 조건에서는 안 났다」까지만 말한다.

### 5-2. 보강 문안 — 체크표 소유는 qa-regression이다. 리더 경유로 전달하고, 문구는 그쪽이 정본이다

**(가) R-2 개시 경로를 둘로 가른다.**
현행 R-2는 *"[시작 > 계정 아이콘 > 로그아웃](또는 `Ctrl+Alt+Del` → 로그아웃)"*(`:454`)이다. **D가 참이면 두 개시 경로의 결과가 갈리는데, 지금은 한 칸에 섞인다.**
- **R-2 = 시작 메뉴 로그아웃**(개시자가 Explorer — D를 겨누는 쪽)으로 고정한다.
- **R-2b(선택) = `Ctrl+Alt+Del` → 로그아웃**으로 분리한다.
- 칸 이름에 개시 경로를 붙인다(예: `R-2 시작메뉴 · 원복 성공`). 두 결과가 다르면 **그 차이 자체가 D의 증거다.**

**(나) OS 빌드를 적는다**(읽기 전용). 예:
```powershell
$v = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'; "OS = $($v.ProductName) $($v.DisplayVersion) build $($v.CurrentBuild).$($v.UBR)"
```
E는 OS 빌드에 의존할 수 있다. 결과를 빌드 없이 적으면 다음 기계의 결과와 합칠 수 없다.

**(다) (선택 · 읽기 전용 · 판정 근거 아님) 영속 시점 보조 판독 — A/B를 가른다**
- **키 경로와 값 이름은 1차 문서가 아니다**(커뮤니티 서술). Windows 11 일부 빌드에서 다를 수 있다. **바이트 뜻을 해석하지 않고 같음/다름만 본다.** ★ **이 값을 쓰거나 지우는 절차는 절대 넣지 않는다**(원칙 3, 레지스트리 쓰기 감사).
```powershell
# P0: 앱 켜기 전(자동 숨김 ON) · P1: 앱 실행 중(막대 보임) · P3: R-2 로그인 뒤, 앱을 켜기 전
$k = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3'
if (Test-Path $k) { "P? = " + ((Get-ItemProperty $k).Settings -join ',') } else { "키 없음 — 이 보조 판독만 무효" }
```
(Windows 미검증 스니펫이다. 체크표로 옮길 때 qa-regression이 교정한다.)

| 관측 | 뜻 |
|---|---|
| **P1 ≠ P0** | 해제가 **즉시** 영속됐다(B) → 원복 W도 셸이 살아 있는 동안 즉시 영속된다 → **그 기계에서는 저장 순서형 e-2가 닫힌다** |
| **P1 = P0** | 영속이 **지연된다**(2012 글의 모델) → 순서가 결과를 가른다 → **R-2 2×2가 판정한다** |
| P3 = P1 · ① OFF · `active = False` | **e-2의 직접 증거**(거짓 성공 칸 + 영속값도 해제값) |
| P3 = P0 · ① ON | 영속값도 원래 값 — 원복이 저장됐거나 로그오프가 되돌렸다(5-d 귀속은 여기서 안 갈린다) |

**(라) 시각 판독 — 기본 실행의 `Player.log`로는 불가하다. `-timestamps`로 기동하면 가능성이 있다(Windows 실기 미확인).**
- 기본 실행: 우리 로그 줄에 시각이 없다(`docs/strategy/ROADMAP.md:4381` *"`Player.log` 줄에 시각이 없으므로"*).
- ★ 2차 정정(`verify-change` 적발 — 첫 판 「`Player.log`로는 불가」는 과장): Unity 6000.0 매뉴얼 Player command line arguments에 `-timestamps` — *"Prefixes every Player log message with the current timestamp and thread ID."* — 가 있다(웹 제외 standalone). 우리 Windows 빌드에서 실제로 붙는지, 시각 기준(현지/UTC)·정밀도는 **미확인**이다.
- **R-2 판별 수단이 되는가 — 단독으로는 아니다.** 붙는 시각은 **우리 쪽 사건**(⓪ 표지 · ① 원복 성공 줄 · `[종료순서] Windows 세션 종료 통보` 줄)의 순간이다. W 순간은 흔적 `writtenAtUtc`로 이미 있고, e-2를 가르는 **셸 저장 S 순간은 `Player.log`에 구조적으로 없다.** 보탬은 둘이다: ① 끊긴 프로세스의 마지막 줄 시각(⓪과 ① 사이 어디서 끊겼는지의 폭) ② 세션 종료 통보부터 원복 줄까지의 경과(5초 예산 대비). 셸 저장 시각원이 따로 생기면 그때 비교 축이 된다. ★ `-timestamps`를 쓰려면 **기동 방법이 바뀐다**(인자 추가) — §R R-2 선택 항목으로만 제안하고 **E-3 세션 사용자 단계는 바꾸지 않는다.** 끊긴 프로세스는 마지막 줄을 못 남길 수 있다는 기존 규칙(§R 머리)도 그대로다.
- 쓸 수 있는 시각은 흔적의 `writtenAtUtc`(W 직후 닫기 시각, UTC — `ReservedBarRestoreLedger.cs:221`)와 표지 파일 `LastWriteTime`이다.
- 셸 저장 시각에는 레지스트리 키의 마지막 쓰기 시각이 필요하다. 그런데 PowerShell 기본 명령으로는 나오지 않고 네이티브 호출이 필요하다 → **사용자 세션에는 넣지 않기를 권한다**(복잡·미검증). **(다)가 같은 질문을 더 싸게 답한다.**

**(마) ④가 들어간 빌드라면** 기동 로그의 종료 레벨 줄을 적는다 → C를 판정한다.

### 5-3. 결과 → 조치

| R-2(·R-2b) 결과 | 조치 |
|---|---|
| ✅ 원복 성공(두 개시 경로 모두) + (다) P1 ≠ P0 | 그 기계에서 e-2 닫힘(B). ④(나)로 현장 감시만 이어 간다 |
| ✅(두 개시 경로 모두) + (다) P1 = P0 | A가 성립했다는 증거 1건. ②는 예비안으로 유지, ④(나)로 감시 |
| ✅ 시작 메뉴 / ❌❌ Ctrl+Alt+Del(또는 반대) | 개시 경로 의존 — D 또는 그 변형이다. **② 채택 + 사용자 보고**(§6) |
| ❌❌ 한 번이라도 | **CW-2 실패**(`:379`·`:452`). ② 채택 + 사용자 보고. ④(가) 레벨이 ≤ 0x0FF면 ⑤를 먼저 검토한다 |
| ◐ 세션 한정 / ❌ 셸 선종료 실패 | e-2가 아니다(흔적이 열려 있다). 기존 판독대로 처리한다 |

---

## 6. 출시 관문 성격

- **새 게이트는 필요 없다.** e-2는 이미 **H-2**(`docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md:408` — `761f5cb`에서 확인, `:406`은 H-0이다, ★ 출시 차단) = **CW-2**(`docs/strategy/ROADMAP.md:1307`) 안의 「❌❌ 거짓 성공」 칸이다. 체크표도 그 칸을 *"R-2를 하지 말고 … 즉시 리더에게"*(`:379`), *"원칙 3 예외 철회 검토 대상"*(`:452`)으로 이미 올려 두었다.
- **지금 사용자에게 올릴 사안인가 — 아니다.** 판정이 (c)이고 문서 모델은 안전한 방향이다. 성립하지 않을 경우를 흡수할 설계안(②)도 준비돼 있다. 「사용자 확정 결정의 전제가 깨졌다」고 쓸 근거가 아직 없다.
- **올려야 할 때**:
  1. R-2/R-2b에서 ❌❌가 1회라도 나올 때. 알릴 내용: 「실행 중에만 + 종료 시 원복」이 **앱을 켠 채 로그오프·재시작하는 경로에서는 종료 시점만으로 보장되지 않는다**는 사실. 그리고 ② 채택 시 대가 — 로그오프 뒤 사용자가 직접 바꾼 자동 숨김을 다음 실행이 되돌릴 수 있다 — 를 **②/②-b 선택지**로 올린다.
  2. ④(가)가 레벨 ≤ 0x0FF를 보일 때(⑤로 설계 흡수 가능 — 보고만).
- **ROADMAP e-2 행 문구 갱신 제안**(`docs/strategy/ROADMAP.md:4412`는 읽기만 했다 — 리더 판정):
  - 지금: 「가능성 **미확인**(셸 저장 순서의 1차 문서를 못 찾았다)」
  - 제안: 「**메커니즘**: Microsoft 직원 블로그 2012가 서술(API 계약 아님) / **순서**: 1차 API 문서 없음. Windows Internals의 직렬·레벨 모델에서는 발생하지 않음 / **남는 조건** C(레벨 역전)·D(개시 시점 저장)·E(Windows 10/11 모델 변경) — 실기 R-2·R-2b와 진단 ④로 판정 / 같은 결과 모양의 별개 경로 e-5(셸 부재 시 조회값) — e-4와 함께 후속」
- **`TASKBAR_REVEAL.md` 2-3**(dev-platform 미커밋 편집 중)은 2013 글만 인용한다. 2012 글이 **저장 시점(`WM_ENDSESSION`)과 「그 뒤 변경은 저장 안 됨」**을 직접 적으므로 추가 인용을 제안한다(리더 경유). 두 글 모두 「로그오프 때 **현재 값**을 저장」 방향이다. 5-d의 「로그오프가 설정을 되돌림」 가설(`:345-347`)과는 **반대 방향이지만 배제하지는 않는다** — 5-d 단서 문장은 그대로 유효하다.

---

## 7. 범위 밖 관찰 (리더 판단)

- **e-4 다중 앱 경합** — Chen 두 글이 명시적으로 경고한다. 다른 앱도 자동 숨김을 바꾸면, 우리 종료 원복이 **기동 때 기록한 원래 값**으로 그 앱의 변경을 덮는다(`ResolveQuit`는 지금 값 ≠ 원래 값이면 쓴다, `ReservedBarRevealPolicy.cs:188-195`). 원칙 3 관점의 새 갈래이고, 빈도는 미확인이다.
- **e-5(가칭) 셸 부재 시 조회값 거짓 성공** — ★ 2차 신설(`verify-change` 적발). 셸이 우리보다 먼저 내려간 뒤 `ABM_GETSTATE`가 AUTOHIDE 비트가 선 값을 돌려주면, 종료 원복이 `QuitAlreadyMatched`(`ReservedBarRevealPolicy.cs:188-192`)로 **쓰기 없이** 흔적을 닫는다(`ReservedBarRevealDirector.cs:287`) — §2-3. 같은 가족: 기동 때 열린 흔적 + 셸 부재 + 비트 선 값이면 `LeftoverAlreadyMatched`(`ReservedBarRevealPolicy.cs:143-147`)로 빚이 쓰기 없이 사라진다(셸보다 먼저 뜨는 기동 — 1.0에는 자동 실행이 없다 — 또는 **셸 재시작 도중 기동**: `ReservedBarRevealPolicy.cs:136` 주석 「셸 재시작 중」). 셸 부재 시 반환값은 1차 문서가 없다. 원칙 3 성립 조건에 걸리는 새 경로다 → **판정은 e-4와 함께 후속 배정**(리더). 판별 재료는 ④(나)가 함께 남긴다.
- **후속 판정 대기 — e-4 · e-5의 범위**(2026-09-15 리더 배정. 판정은 이번에 하지 않는다). **e-4**: 실행 중에 **사용자**(설정 앱 토글) 또는 **다른 앱**(`ABM_SETSTATE`)이 자동 숨김을 바꾼 뒤, 종료 원복이 기동 때 기록한 원래 값으로 그 변경을 덮는 경로다(원복 판정은 지금 값 ≠ 원래 값이면 쓴다 — `ReservedBarRevealPolicy.cs:188-195`). 위 e-4 항목은 다른 앱만 적었으므로 사용자 변경을 범위에 더한다. **e-5**: 셸 부재 시 `ABM_GETSTATE` 반환값에 따라 쓰기 없이 흔적을 닫는 두 자리 — 종료 `QuitAlreadyMatched`(`:188-192`)와 기동 `LeftoverAlreadyMatched`(`:143-147`)다. 둘 다 원칙 3 예외의 성립 조건에 걸린다. 판정 입력(셸 부재 시 반환값 · 사용자 변경과 우리 기록을 가르는 수단)은 실기가 필요하다.
- **실행 중 셸 재시작** — Director에는 셸 재시작 뒤 해제를 다시 거는 경로가 없다(파일 전체 판독. 트레이의 `_shellRestartPending`은 아이콘 재설치용이다). 셸이 저장 없이 죽었다 살아나면 해제가 풀릴 수 있다. 원칙 3에는 무해하고 기능 회귀는 가능하다 — 미확인.
- **macOS** — Dock은 이번 예외에 없다(`TASKBAR_REVEAL.md` §3). Dock 판단이 채택되면 **같은 질문(로그아웃 때 시스템이 설정을 저장하는 시점 vs 우리 원복)**이 macOS 쪽에서 다시 나온다 — 그때 이 문서의 §3 틀을 쓴다.

---

## 플랫폼 영향

- **Windows 영향: 없음(이 라운드는 문서만).** 권고 ④·⑤와 예비안 ②는 `Platform/Windows/`의 사실 조회 한 벌과 중립 정책·흔적 계층에 걸친다. 구현 배정 시 크로스 컴파일(`-define:UNITY_STANDALONE_WIN`/`OSX`) 대상이다.
- **macOS 영향: 없음.** 자동 숨김 제어기가 배선되지 않아(`ReservedBarRevealDirector.cs:74-81`) ④(나)·②는 구조적으로 돌지 않는다. Dock 판단 시 §7 마지막 항목을 적용한다.

---

## 출처

1차 API 문서(Microsoft Learn):
- [WM_QUERYENDSESSION message](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-queryendsession)
- [WM_ENDSESSION message](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-endsession)
- [Logging Off](https://learn.microsoft.com/en-us/windows/win32/shutdown/logging-off)
- [Shutting Down](https://learn.microsoft.com/en-us/windows/win32/shutdown/shutting-down)
- [Application Shutdown Changes in Windows Vista (보관 문서)](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms700677(v=vs.85))
- [SetProcessShutdownParameters function](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessshutdownparameters)

Microsoft Press(API 계약 아님):
- [Windows Internals, Part 2, 6th Ed. — Troubleshooting Windows Startup and Shutdown Problems (발췌)](https://www.microsoftpressstore.com/articles/article.aspx?p=2201310&seqNum=3)

Microsoft 직원 블로그(참고 서술, API 계약 아님):
- [How your taskbar auto-hide settings can keep getting overwritten (2012-07-05)](https://devblogs.microsoft.com/oldnewthing/?p=7203)
- [The case of the auto-hide taskbar (2013-08-01)](https://devblogs.microsoft.com/oldnewthing/20130801-00/?p=3643)

---

## 부록 — 초판(미커밋) 대비 정정 기록

- 이 파일은 한 번도 커밋되지 않았다. 본문은 인라인 취소선 없이 고쳤고, 바뀐 판정 문장을 여기 모았다(리더 판정).
- **초판**은 이 문서 최초 저장본이다. 문장은 작성 세션에 남은 원문 그대로 옮겼다(재구성 아님).
- **근거 번호**: `vc1-①~⑤` = `verify-change` 1차 조건 ①~⑤(① H-2 줄, ② `-timestamps`, ③ XP 절 모호성, ④ 셸 부재 전제, ⑤ Chen 2012 인용). `vc1-경미` = 1차 경미 항목. `vc2-①~④` = 2차 조건. `vc3-①~④` = 3차 조건. `자체` = 작성자가 스스로 찾은 것.

### 리더가 지목한 8문장

| # | 자리 | 초판 문장 | 정정 | 근거 |
|---|---|---|---|---|
| 1 | §0 「셸 저장이 우리보다 앞이면 …」 칸 | 「**막히지 않는다.** 직렬 모델에서 셸 차례가 우리보다 앞이면 우리 `WM_QUERYENDSESSION`도 그 뒤에 온다. …」 | 「**C·D에서는** 막히지 않는다」 + 「모두에게 QUERY → END 차례」 모델에서는 이기며 배제하지 못한다 | vc1-③ |
| 2 | §3-2 F행 | 「W 되읽기 실패 \| e-1(흔적 열림) — **거짓 성공 아님**」 | 셸 부재 시 반환값의 AUTOHIDE 비트 유무로 갈린다. 비트가 서면 쓰기 없이 `QuitAlreadyMatched` → 거짓 성공 모양 = e-5 | vc1-④ |
| 3 | §3-4 결론 | 「⇒ **QUERY 원복은 가설의 거짓 성공을 막는 수단이 아니다**(§4 ①).」 | 「막는다는 **근거가 약하다**」로 한 단계 낮춤. ① 기각은 1차 지침 · 취소 경로 · 테스트 충돌로 유지 | vc1-③ |
| 4 | §0 판정 칸 | 「Explorer 레벨 2 = 사용자 프로세스 중 마지막」 | 「시스템 예약 마지막 범위 — 작업 관리자 1이 그 뒤」 | vc1-경미 |
| 5a | §0 판별 실기 칸 | 「로그인 뒤 ON 행(`:378` — 2026-09-15 qa-regression이 이름을 둘로 갈랐다)에는 거짓 성공이 들어올 수 없다.」 ※ 이보다 앞선 판(**작성자 기록 기준, 검증자 미확인**): 「「✅ 원복 성공」은 로그인 뒤 ON을 요구한다(`:373`).」 | 「로그인 뒤 ON 행(…)에는 **e-2(해제가 영속된 경우)**가 들어올 수 없다(「거짓 성공 + 재시작이 되돌림」 같은 다른 거짓 성공은 체크표 `:383`대로 이 행에도 들어올 수 있다).」 | vc1-경미(체크표 `:383` 용어 충돌) |
| 5b | §5-1 ON 행 | 「⇒ **거짓 성공은 이 행에 들어올 수 없다.**」 ※ 이보다 앞선 판(**작성자 기록 기준, 검증자 미확인**): 「⇒ **거짓 성공은 이 칸을 통과할 수 없다.**」 | 「⇒ **e-2(해제가 영속된 경우)는 이 행에 들어올 수 없다** — 체크표 `:383`이 적듯 「거짓 성공 + 재시작이 되돌림」 같은 다른 거짓 성공은 이 행에도 들어올 수 있다.」 | vc1-경미 |
| 6 | §4 ④(나) 원안 | 「(나) 기동 때 **닫힌** 흔적의 `originalAutoHide` ≠ 지금 값이면 한 줄 경고: 「e-2 후보 **또는** 사용자가 직접 바꿈 — 쓰지 않음」」 · 같은 행 「(나) 불일치일 때만 1줄」 | 「흔적을 닫은 실행 바로 다음 기동 1회」(흔적 pid == 직전 표지 pid). 3차: 탐지력 한계 + 「표지 판정 불가」 1줄 | vc1-경미(매 기동 영구 반복) · vc2-②③ |
| 7 | §4-1 ② 채택 조건 | 「채택 조건: **R-2/R-2b에서 「❌❌ 거짓 성공」이 1회라도 나오거나, ④(가)가 레벨 ≤ 0x0FF를 보이거나, ④(나)가 현장에서 반복된다.**」 | 「④(나) 경고가 현장 로그(서로 다른 사용자·세션)에서 반복 보고된다」 | vc1-경미(6의 결과) |
| 8 | §0 판정 칸의 2012 요약 | 「Microsoft 직원 블로그(2012)가 *"Explorer는 `WM_ENDSESSION`에 응답해 설정을 저장하고, 그 뒤 바뀐 설정은 다시 저장되지 않는다"*고 쓴다」 — 한국어 요지를 인용부호로 씀 | 「… 블로그(2012)의 **요지**: …(원문 §1-3)」 — 인용부호 제거 | vc1-⑤ |

### 본문에 「첫 판」으로 이미 적힌 4곳 (가리키기만 한다)

| # | 초판 요지 | 정정이 적힌 자리 | 근거 |
|---|---|---|---|
| 9 | §2-3 「**셸 선종료가 거짓 성공을 만들지는 않는다**(코드 판독)」 | §2-3 「셸이 이미 없을 때 — ★ 2차 정정」 · §7 e-5 | vc1-④ |
| 10 | §3-4 「… 이것은 Vista 문서 *"serially"*와 어긋나는 가정이다」 | §3-4 「★ 2차 정정 — … 과장이었다」 | vc1-③ |
| 11 | §5-2(라) 제목 「… `Player.log`로는 불가하다」 | §5-2(라) 「★ 2차 정정」 | vc1-② |
| 12 | §6 H-2 `WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md:406` | §6 「`:408` — `761f5cb`에서 확인」 | vc1-① |

### 그 밖에 바뀐 곳 (빠짐없이)

| # | 자리 | 초판 | 정정 | 근거 |
|---|---|---|---|---|
| 13 | §1-3 Chen 2012 인용 | 「… Explorer saves out its settings."」에서 끊김 · 홑따옴표 `'Okay, I unchecked the box.'` · `aren't` · 첫 문장 없음 | 원문 HTML 대조 복원(「and calls it a night」 · 굽은 따옴표 · `aren’t` · 첫 문장 · 오탈자 「to go bed」 보존) | vc1-⑤ |
| 14 | §1-3 끝 줄 | 「2012 글의 인용도 WebFetch 요약 도구가 돌려준 짧은 인용이다. **리더 인용 전에 브라우저로 원문 1회 대조를 권한다**」 | 「원문 HTML을 받아 대조했다」 | vc1-⑤ |
| 15 | §1-1 XP 행 해석 | 「**앱 단위 직렬** — 한 앱의 QUERY→END→닫기가 끝나야 다음 앱으로 간다」 | 같은 절의 반대 문장 병기 · 「이 페이지의 XP 서술은 모호하다」 | vc1-③ |
| 16 | §3-2 E행 | 「Windows 10/11이 직렬 모델을 바꿨다(예: 모두에게 QUERY 선발송 → END 직렬, 또는 병렬) \| 1차는 Vista 보관 문서뿐이다」 | 「원래부터 「모두에게 QUERY → END 차례」였을 수도」 · 「XP 절이 두 방향으로 읽힌다」 | vc1-③ |
| 17 | §4 ① 효과 칸 | 「**없음**(C·D에서 늦다 §3-4). E 한 변형에서만 효과」 | 「C·D에서는 없음 · E 변형(모호 — 배제 못 함)에서만 효과」 | vc1-③ |
| 18 | §2-3 되읽기 | 「되읽기가 확인하는 것은 「Explorer가 지금 메모리에 든 상태」다」 · 「e-2가 끼어들 수 있는 틈은 이 한 곳뿐이다」 | 반환값 = 메모리 상태는 **추론** 표기 · 영속 값을 안 읽는 것은 코드 사실 · 「이 자리다(e-5도 같은 자리)」 | vc1-경미 · vc1-④ |
| 19 | §2-2 | 「성공 로그 `:291-293`」 | 「`:291-292` → `ChangedThisSession = false` `:293`」 | vc1-경미 |
| 20 | 인용 경로 8곳(본문 전체 — 8곳 모두 경로 치환. §5-2(라)의 `ROADMAP.md:4381`도 1차 판에 옛 표기로 있었다 — 4차 검증 정정) | 「`ROADMAP.md:…`」 | 「`docs/strategy/ROADMAP.md:…`」 | vc1-경미 |
| 21 | §4 ④ 충돌 칸 | N-8 금지 위치 서술 없음 | (가)·⑤를 목록 A~E 파일(특히 C등급 `FreezeWatchdog.cs` · `FreezeForensicsLog.cs` · `FreezeForensicsPolicy.cs`)에 넣지 않는다 | vc1-경미 |
| 22 | §0 판정 칸 끝 · §3-2 결론 줄 · §6 ROADMAP 제안 | e-5 없음 · 「C·D·E 셋뿐이다」 | e-5(가칭) 병기 · 「셋이다(별개 경로 e-5는 F행)」 | vc1-④ |
| 23 | §4 ④(나) 파서 | 「`SessionExitMarker.TryParse` `:94`」(2차 도입) | 「`SessionExitMarkerPolicy.TryParse`(클래스 `:60`, 메서드 `:94`)」 | vc2-② |
| 24 | §7 e-5 기동 가족 괄호 | 「(셸보다 먼저 뜨는 기동 — 1.0에는 자동 실행이 없다)」(2차 도입) | + 「또는 셸 재시작 도중 기동(`ReservedBarRevealPolicy.cs:136`)」 | vc2-④ |
| 25 | 체크표 줄 인용 | 초판(**작성자 기록 기준, 검증자 미확인**) `:370-374` · `:373` · `:374` · `:417-418` · `:418` · `:421` · `:423` | `:375-379` · `:378` · `:379` · `:448-449` · `:449` · `:452` · `:454` — 저장 직후 qa-regression 동시 편집(+197/−6)으로 줄이 밀림 | 자체 |
| 26 | §3-1 조건 2 인용 | 「(2012 글: *"aren't going to be saved again"*)」 — 곧은 따옴표 | 「*"aren’t going to be saved again"*」 — 원문 굽은 따옴표 | vc1-⑤ |
| 27 | §4 ④(나) 옛 표지 기준(3차 도입) | 「옛 표지 의심의 한 기준: 표지 `utc=`(…)가 흔적 `writtenAtUtc`보다 이르다 — 원복 흔적 닫기(①)는 정상 종료 표지(④)보다 앞이고, …」 | 「표지 `pid` ≠ 흔적 `pid` ∧ 표지 `utc=` < 흔적 `writtenAtUtc`」 — 같은 실행 표지(`exit-started`·`running`) 오판 제거, (i) clean-exit 한정안과 비교 뒤 (ii) 채택 | vc3-① |
