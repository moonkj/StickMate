---
name: run-stickmate
description: macOS에서 StickMate 데스크톱 오버레이 앱을 빌드/실행하고, 전역 단축키를 주입해 실제로 조작하고, Player 로그와 스크린샷으로 결과를 검증한다. 캐릭터가 화면에 안 보이거나, 단축키가 안 먹거나, 앱을 껐다 켜야 하거나, 연출(활쏘기/그라피티 등)을 실제로 발동시켜 눈으로 확인해야 할 때 쓴다.
---

# StickMate 구동 스킬 (macOS)

StickMate는 **투명 + 클릭관통 데스크톱 오버레이**다. 창을 클릭해 조작하는 보통의 GUI 앱이 아니다.
누를 버튼도, Dock 아이콘도, 메뉴바 아이콘도 없다(우클릭 메뉴도 폐지됐다). 그래서 조작 경로는 셋뿐이다.

| 하는 일 | 수단 |
|---|---|
| 조작 | **전역 단축키 주입** (Ctrl+Opt+Cmd + 글자) |
| 검증 | **Player 로그** — 상태 전이·물리 판정을 한국어로 극도로 상세히 남긴다. 스크린샷보다 정확하다 |
| 육안 확인 | `screencapture` |

전부 `driver.sh` 하나로 감싸 두었다. **맨손으로 `open` 하지 마라** — 그러면 단축키가 죽는다(아래 Gotchas 1번).

## 빠른 시작

```bash
# 1) 환경/권한/락 점검 — 문제가 생기면 항상 여기부터
.claude/skills/run-stickmate/driver.sh doctor
```

```bash
# 2) 실행 -> 단축키 주입 -> 캡처 -> 종료 전체 왕복
.claude/skills/run-stickmate/driver.sh demo
```

`demo` 가 성공하면 이런 출력이 나온다(실제 출력):

```
실행: PID 79998
로그: /tmp/stickmate-run/stickmate.log
부팅 완료 (7회 폴링)

-- 전역 단축키 B(말풍선 즉시 띄우기) 주입 --
주입: Ctrl+Opt+Cmd+B (키코드 11, 400ms 유지)
로그 확인:
  [앱제어] 말풍선 강제 발화(전역 단축키 Ctrl+Opt+Cmd+B) — Idle 재진입으로 대사를 파생시켰습니다.
```

## 하니스 명령

```bash
.claude/skills/run-stickmate/driver.sh
```

| 명령 | 설명 |
|---|---|
| `doctor` | 빌드 산출물 / 도구 / **입력 모니터링 권한** / 실행 중 인스턴스 / Unity 락 점검 |
| `start` | 인스턴스 실행. 셸에서 직접 exec 하고 `-logFile` 로 로그를 분리한다 |
| `stop` | **드라이버가 띄운 인스턴스만** 정상 종료(진행도 저장됨). 다른 인스턴스는 절대 안 건드림 |
| `status` | PID / 로그 경로 / 그 밖의 인스턴스 |
| `keys` | 앱이 부팅 배너에 찍은 **최신** 단축키 목록을 그대로 출력 (문서에 베끼지 말고 앱에게 물어라) |
| `key <글자> [ms]` | 전역 단축키 주입 후 로그로 발동 확인. 기본 400ms 유지 |
| `shot [이름]` | 전체 화면 + 캐릭터가 있는 가로 띠 캡처 |
| `log [-f\|줄수]` | 로그 보기 |
| `demo` | start → key B → 캡처 → stop |
| `build --force` | Unity 배치모드 macOS 빌드 (락 확인 후에만) |
| `test --force` | EditMode 테스트 (락 확인 후에만) |

개별로 쓸 때:

```bash
SK=/Users/kjmoon/App/StickMate/.claude/skills/run-stickmate
$SK/driver.sh start
$SK/driver.sh key A          # 활쏘기 — 과녁을 세우고 3발
$SK/driver.sh shot archery
$SK/driver.sh log 5
$SK/driver.sh stop
```

## 전역 단축키

모두 **Ctrl+Opt+Cmd** 조합이다. 아래 표는 편의용 스냅샷일 뿐이고, **정확한 최신 목록은 앱에게 직접 물어라**:

```bash
.claude/skills/run-stickmate/driver.sh keys
```

| 키 | 동작 | 비고 |
|---|---|---|
| `Q` | 앱 종료 | **전역이다. 권한을 가진 모든 인스턴스가 함께 죽는다** — 사용자 인스턴스가 떠 있으면 쓰지 마라 |
| `B` | 말풍선 즉시 띄우기 | 가장 안전한 검증용. Idle/Walk일 때만 발동 |
| `A` | 활쏘기 (과녁 + 3발) | 눈에 잘 띄어 스크린샷 검증에 좋다 |
| `C` | 잉크색 전환 | |
| `R` | 로데오 커서 on/off | |
| `G` `T` `X` | 그라피티 / 창 도둑 / 윈도우 크래시 | 평소엔 확률이 낮아 강제 발동용 |
| ~~`K`~~ | — | **2026-09-02 격파 놀이 삭제.** 눌러도 아무 일도 일어나지 않는다(`GlobalKey.K`는 예약으로만 남음) |
| `N` | 가출 발동 / 부르기 | |
| `I` | 캐릭터 정보 창 | |
| `P` | 설정창 열기/닫기 | `driver.sh key P`(kVK_ANSI_P=35). **2026-09-01 `,`에서 옮겼다** |
| `D` `H` `S` `J` `F` | 진단로그 / 하드웨어반응 / 스트레스 / 할일 / 집중모드 | **`StickMateDevTools` 게이트가 열려야 동작** |

> **★ `⌃⌥⌘` + `8` / `,` / `.` 는 절대 주입하지 마라.** macOS 접근성 시스템 단축키다
> (색 반전 / 대비 늘리기 / 대비 줄이기 — symbolic hotkey 21 / 25 / 26). 주입하면
> `com.apple.universalaccess`의 `contrast` 값이 **실제로 바뀐다** = 유저 자산 변경(원칙 3).
> 설정창 단축키가 원래 `,`였다가 P로 옮겨진 이유가 바로 이것이다. `driver.sh`의 `keycode_for`가
> 이 셋을 사유와 함께 거부하므로, 실수로 눌러도 주입되지 않는다.
> 확인 명령: `defaults read com.apple.universalaccess contrast`

## 로그 읽기

로그가 이 앱의 진짜 계기판이다. 드라이버가 띄운 인스턴스는 `/tmp/stickmate-run/stickmate.log`,
사용자 인스턴스는 `~/Library/Logs/Vibelab/StickMate/Player.log`.

```bash
grep -E "\[앱제어\]|\[발판리포트\]|\[말풍선\]" /tmp/stickmate-run/stickmate.log | tail -20
```

자주 쓰는 태그:

| 태그 | 알려주는 것 |
|---|---|
| `[앱제어]` | 단축키가 실제로 먹었는가 (부팅 배너도 이 태그) |
| `[발판리포트]` | **60초 심장박동.** 창 발판 목록 + `캐릭터OS=(x,y)` 좌표 + 현재 상태 |
| `[말풍선]` | 어떤 상태에서 무슨 대사가 파생됐는가 |
| `[발판변경]` `[FallState]` `[벽타기]` | 이동/낙하/등반 판정 |
| `[FramePacing]` `[프레임시간]` | 성능. `무입력=N초` 로 입력이 관측됐는지도 알 수 있다 |
| `[MacOverlayStateEnforcer]` | 투명/항상위/클릭관통이 실제로 적용됐는가 |

## 스크린샷

```bash
.claude/skills/run-stickmate/driver.sh shot mytest
```

전체 화면과, 캐릭터가 돌아다니는 **가로 띠**를 함께 찍는다. 캐릭터는 화면 대비 아주 작아서
전체 캡처만으로는 알아보기 어렵다 — 띠 쪽을 봐라.

이번 세션에 실제로 찍은 것들이 `screenshots/` 에 있다:

| 파일 | 내용 |
|---|---|
| `hotkey-bubble.png` | `key B` 로 띄운 말풍선 — 진행 방향 반대쪽 대각선 위에 "하나 둘 하나 둘" |
| `archery.png` | `key A` 로 발동한 활쏘기 — 과녁 정중앙에 화살이 꽂혀 있고 오른쪽에 활 당기는 캐릭터 |
| `two-instances.png` | 사용자 인스턴스와 테스트 인스턴스가 동시에 떠 있는 모습(정상) |

## Gotchas (전부 이번 세션에 직접 부딪힌 것)

### 1. ★ `open` 으로 띄우면 전역 단축키가 통째로 죽는다

이 스킬에서 제일 중요한 항목이다.

앱은 `CGEventSourceKeyState` 로 키를 읽는다. macOS는 **조합키(Ctrl/Opt/Cmd)는 아무에게나** 보여주지만,
**일반 키(B, Q, A...)는 Input Monitoring(`kTCCServiceListenEvent`) 권한이 있는 프로세스에만** 보여준다.
권한이 없으면 조합키는 True인데 동작키가 영원히 False라서 — **아무 에러도 없이** 단축키가 조용히 죽는다.

TCC 권한은 "책임 프로세스" 단위로 붙는다. 같은 바이너리로 실측한 결과:

| 실행 방법 | `IOHIDCheckAccess` | 일반키 관측 | 단축키 |
|---|---|---|---|
| `open -a StickMate.app` | unknown(미결정) | **False** | **죽음** |
| 셸에서 직접 exec | granted(허용) | True | **동작** |

그래서 `driver.sh start` 는 `open` 을 쓰지 않고 셸에서 바로 exec 한다. 에이전트 셸이 이미 가진
권한을 그대로 물려받게 하는 것이 요점이다. 지금 사용자가 쓰고 있는 인스턴스가 `open` 으로
떠 있다면 그 인스턴스는 단축키가 안 먹는다 — 정상이 아니라 **알려진 제약**이다.

`driver.sh doctor` 의 "전역 단축키 가능 여부" 항목이 이걸 미리 판정해 준다.

### 2. 키는 "눌러 두어야" 한다 — `osascript` 로는 안 된다

앱은 키 상태를 **20Hz(50ms)로 폴링**하고 조합키 3개 + 동작키가 **같은 샘플 순간**에 눌려 있어야
발동한다. `osascript -e 'tell application "System Events" to key code 11 using {...}'` 는
down/up이 수 ms 안에 끝나 폴링 사이로 빠져나간다 — 실제로 시도했고 **한 번도 발동하지 않았다**
(exit 0이라 성공한 것처럼 보이는 게 함정이다).

`src/keyhold.swift` 가 `CGEventPost` 로 키를 눌러 두었다가 떼는 이유가 이것이다. 기본 400ms.

### 3. `-logFile` 없이 띄우면 사용자 로그를 밀어낸다

Unity 플레이어는 시작할 때 `Player.log` 를 `Player-prev.log` 로 밀고 새로 만든다. 사용자 인스턴스가
돌고 있는데 그냥 띄우면 그쪽 로그 연속성이 깨진다. `driver.sh start` 는 항상 `-logFile` 로 분리한다.

### 4. `kill`(SIGTERM)로 끄면 진행도가 저장되지 않는다

macOS Unity 6000.0.82f1에서 **SIGTERM 경로로는 `OnApplicationQuit`이 호출되지 않는다**
(디버거가 12회 실측으로 확정, Unity 이슈트래커 등록 건). 즉 `kill <PID>` 는 진행도를 통째로 날린다.

**정상 종료 여부의 판별 기준은 "저장 로그의 유무"가 아니다** — 저장에 성공해도 앱은 저장 로그를
남기지 않기 때문에 그걸로는 구분이 안 된다. 정확한 기준은 **Unity 종료 시퀀스 3줄의 존재**다:

```
[Physics::Module] Cleanup current backned.
Input System module state changed to: ShutdownInProgress.
Input System module state changed to: Shutdown.
```

이번 세션 A/B 실측: JXA 정상 종료 → **3/3줄**, `kill -TERM` → **0/3줄**(로그가 활동 중간에 그냥 끊김).

`driver.sh stop` 은 `kill` 대신 `NSRunningApplication.terminate` 로 정상 종료를 요청하고,
끝난 뒤 위 3줄을 세어 결과를 보고한다. PID를 지정하므로 **사용자의 다른 인스턴스는 건드리지 않는다**.
(앱 자신의 `Ctrl+Opt+Cmd+Q` 도 정상 종료지만 **전역**이라 권한을 가진 모든 인스턴스를 한꺼번에
죽인다 — 자동화 드라이버용으로는 부적합하다.)

드라이버 없이 손으로 끌 때 쓰는 형태(실측 확인함):

```bash
osascript -l JavaScript -e "ObjC.import('AppKit'); \
  var a=\$.NSRunningApplication.runningApplicationWithProcessIdentifier($(cat /tmp/stickmate-run/stickmate.pid)); \
  a.isNil() ? 'NO_SUCH_PID' : String(a.terminate)"
```

**JXA 함정**: 인자 없는 ObjC 메서드는 *프로퍼티*로 브리지된다. `a.terminate()` 처럼 괄호를 붙이면
프로퍼티를 읽는 시점에 종료가 이미 일어난 뒤
`TypeError: a.terminate is not a function ('a.terminate' is true)` 로 **exit 1** 이 된다
(앱은 정상 종료됐는데 스크립트만 실패로 보이는 함정 — 실제로 겪었다). 괄호 없는 `a.terminate` 가 맞다.

응답이 없으면 `driver.sh stop --force` 가 SIGTERM 폴백을 쓰지만, 그 경로는 **저장을 건너뛴다**고
명시적으로 알려 준다. 기본값은 강제로 죽이지 않고 실패를 보고하는 쪽이다.

### 5. 사용자 인스턴스를 죽이지 마라 / 세이브를 공유한다

`doctor` 와 `status` 가 "그 밖의 인스턴스"를 알려준다. 그건 사용자가 실제로 쓰는 것일 수 있다.
그리고 **모든 인스턴스가 세이브 파일 하나를 공유한다**:
`~/Library/Application Support/Vibelab/StickMate/stickmate_character.json`

즉 `key A`(활쏘기) 같은 걸 쏘면 사용자 캐릭터의 XP/전적이 실제로 올라간다(이번 세션에도
`[성장] 보너스 +15 XP` 가 찍혔다). `start` 가 매번 `/tmp/stickmate-run/save-backup-*.json` 으로
백업은 해 두지만 **자동 복원은 하지 않는다** — 사용자 인스턴스가 동시에 진행도를 쓰고 있어서,
되돌리면 오히려 사용자의 진짜 진행을 지우게 되기 때문이다. 필요하면 사람이 판단해서 복원해라.

### 6. 단축키가 "먹었는데 아무 일도 안 일어나는" 정상 동작

`B`(말풍선)는 캐릭터가 **Idle/Walk일 때만** 발동한다. 낙하/활쏘기 중이면 이렇게 찍고 건너뛴다:

```
[앱제어] 말풍선 요청(...) — 지금은 Fall 중이라 건너뜁니다(진행 중인 행동을 대사 때문에
중단시키지 않는다 — UX_FLOW.md 5절).
```

버그가 아니다(원칙 1: 행동-텍스트 싱크). 몇 초 뒤 다시 쏴라.

### 7. 모든 동작이 `[앱제어]` 로 찍히지는 않는다

`A/K/G/T/X/H/S/N/J/F` 는 각 연출 디렉터에게 위임되어 `[활쏘기]` 처럼 **고유 태그**로 찍힌다.
`[앱제어]` 만 grep하다가 "반응 없음"으로 오판하기 쉽다(실제로 한 번 그랬다). `driver.sh key` 는
넓은 패턴으로 본다.

### 8. `screencapture -R` 은 픽셀이 아니라 **포인트**다

Retina에서 `-R 0,780,1512,180` 은 3024x360 PNG를 만든다(2배). 그리고 캐릭터 좌표는
`[발판리포트]` **60초 심장박동에서만** 나오므로 이미 낡았다 — 그 좌표로 좁게 크롭하면 십중팔구
빗나간다(실제로 첫 시도에 놓쳤다). 그래서 `shot` 은 좁은 사각형 대신 **가로 전체 띠**를 찍는다.

### 9. Unity 빌드/테스트는 다른 에이전트와 충돌한다

여러 에이전트가 같은 `Library/` 를 배치모드로 동시에 쓰면 깨진다. 게다가 빌드는
`Builds/macOS/StickMate.app` 을 **덮어쓴다** — 지금 사용자가 그걸 실행 중이고, 다른 에이전트가
편집 중인 코드가 컴파일에 실패하면 멀쩡하던 산출물을 잃는다.

그래서 `build` / `test` 는 `--force` 없이는 실행을 거부한다. 먼저 `doctor` 의 락 항목을 봐라.

```bash
.claude/skills/run-stickmate/driver.sh build   # 가드가 막는 것을 확인
```

정말 필요할 때만(락이 비어 있고, 산출물을 덮어써도 될 때):

> **이번 세션에서는 실행하지 않았다.** 다른 에이전트들이 캐릭터 리디자인 작업 중이어서
> 산출물을 덮어쓸 위험이 있었다. 아래 두 줄은 `driver.sh` 안에 구현되어 있고 가드 동작까지만 검증했다.
>
> - `driver.sh build --force` → `Unity -batchmode -nographics -quit -projectPath <repo> -executeMethod StickMate.EditorTools.BuildStandalone.PerformBuild`
> - `driver.sh test --force` → `Unity -batchmode -nographics -runTests -testPlatform EditMode`
>
> 윈도우 빌드는 `PerformBuildWindows`. 둘 다 `Assets/Editor/BuildStandalone.cs` 에 있다.

### 10. 재빌드하면 `open` 경로의 권한이 또 풀린다

앱은 **ad-hoc 서명**이라 빌드할 때마다 cdhash가 바뀐다. 사용자가 Input Monitoring을 한 번
허용해 줬더라도 재빌드하면 그 승인이 무효가 된다. 이 스킬이 `open` 대신 셸 exec를 쓰는 또 다른 이유다.

## 이번 세션 검증 상태

실제로 실행해서 성공을 확인한 것:

| 명령 | 결과 |
|---|---|
| `driver.sh doctor` | exit 0, 권한 판정 OK |
| `driver.sh start` / `status` | OK (사용자 인스턴스 PID 78429는 끝까지 무사) |
| `driver.sh stop` | `Unity 종료 시퀀스 3/3줄 확인(정상 종료, 진행도 저장 경로 실행됨)` |
| `driver.sh keys` | 배너 파싱 OK |
| `driver.sh key B` | `[앱제어] 말풍선 강제 발화(전역 단축키 Ctrl+Opt+Cmd+B) — Idle 재진입으로 대사를 파생시켰습니다.` |
| `driver.sh key A` | `[활쏘기] 발동(전역 단축키 Ctrl+Opt+Cmd+A)` + 3발 명중 로그 |
| `driver.sh shot` | `screenshots/*.png` 생성 |
| `driver.sh log` / `demo` | exit 0 |
| `driver.sh build` / `test` (가드) | `--force` 없이 거부됨 확인 |

실행하지 않은 것: `build --force`, `test --force` (Gotcha 9 참고).

## 파일

```
.claude/skills/run-stickmate/
├── SKILL.md
├── driver.sh              # 하니스 본체
├── src/keyhold.swift      # 전역 단축키 주입기 (키를 눌러 둔다)
├── src/keycheck.swift     # 입력 모니터링 권한 사전 판정
├── bin/                   # driver.sh 가 필요할 때 컴파일 (gitignore)
└── screenshots/           # 이번 세션에 실제로 찍은 증거
```

환경 변수: `STICKMATE_REPO`, `STICKMATE_RUN_DIR`(기본 `/tmp/stickmate-run`),
`STICKMATE_SHOT_DIR`, `UNITY_BIN`.
