#!/usr/bin/env bash
# StickMate 구동 하니스 (macOS).
#
# 이 앱은 투명/클릭관통 데스크톱 오버레이라 "창을 클릭하는" 전통적 GUI 조작이 통하지 않는다.
# 정상적인 조작 경로는 (1) 전역 단축키 주입 (2) Player 로그 관찰 (3) 화면 캡처 세 가지다.
# 자세한 배경과 함정은 같은 폴더의 SKILL.md 참고.
set -uo pipefail

REPO="${STICKMATE_REPO:-/Users/kjmoon/App/StickMate}"
APP="$REPO/Builds/macOS/StickMate.app"
BIN="$APP/Contents/MacOS/StickMate"
SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN_DIR="${STICKMATE_RUN_DIR:-/tmp/stickmate-run}"
PID_FILE="$RUN_DIR/stickmate.pid"
# ★ 2026-09-02 (디버거) — 로그를 **실행마다 새 파일**로 가른다.
#   왜: 종전에는 모든 인스턴스가 $RUN_DIR/stickmate.log 한 개를 공유했고 cmd_start가 그걸
#   `: >` 로 잘랐다. 앞 인스턴스가 살아 있으면 그 프로세스는 **자기 오프셋을 유지한 채** 계속
#   쓰므로, 잘린 지점과 그 오프셋 사이가 커널이 채우는 NUL 구멍이 된다.
#   실측(PID 30572, 2026-09-02): 2,547,334바이트 중 2,117,358바이트(83%)가 NUL이었다.
#   실제 피해는 **로그 내용의 소실**이다: 부팅 배너가 12번 찍혔는데 세션 경계
#   ("Initialize engine version")는 1개만 남았다 — 겹쳐 쓰기로 앞 세션 기록이 지워졌다.
#   그리고 $PID_FILE 이 없는 지금 상태에서 start 를 다시 하면 **같은 일이 또 벌어진다**
#   (our_pid 가 비어 is_alive 가 거짓 -> 살아 있는 30572 의 로그를 그대로 자른다).
#
#   ★ 정직성 기록 — 여기서 디버거가 세운 2차 가설 하나는 **반증됐다.**
#     가설: "NUL 때문에 grep 이 파일을 바이너리로 보고 이 스크립트의 로그 프로브가 전부 죽는다."
#     처음 실측이 그렇게 나왔으나(rc=1), 그건 **측정 도구가 틀린 것**이었다 — 조사자의 zsh
#     `grep` 이 ugrep 래퍼(-I = 바이너리 건너뜀)였다. 이 스크립트는 bash 로 돌고 거기서
#     `grep` 은 /usr/bin/grep(BSD grep 2.6.0-FreeBSD)이다. 같은 오염된 파일에 대해 BSD grep 은
#     -q / -m1 / -o / -cE / -E **다섯 곳 전부 -a 유무와 동일하게 동작한다**(실측).
#     즉 **드라이버의 판독 프로브는 이 오염으로 깨지지 않았다.**
#     아래 grep 들에 붙은 -a 는 그래서 "버그 수정"이 아니라 **이식성 보험**이다(GNU grep 이나
#     ugrep 처럼 바이너리를 다르게 다루는 구현에서 조용히 달라지지 않게 못 박는 것). 실측된
#     동작 변화는 0이다. 이 문단을 지우지 마라 — 반증된 가설을 남기는 것이 이 저장소의 규칙이다.
#   따라서 실제 처방은 하나다: **파일을 애초에 섞지 않는다**(아래).
LOG_DIR="$RUN_DIR/logs"
LOG_LINK="$RUN_DIR/current.log"
# 나머지 서브커맨드(log/key/shot/keys/stop)는 예전처럼 $LOG_FILE 하나만 본다. 그 이름이
# 심링크라 "가장 최근에 start한 인스턴스의 로그"를 자동으로 따라간다.
# 심링크가 아직 없으면 구판 단일 파일로 떨어져 기존 로그도 계속 읽힌다(하위 호환).
if [ -e "$LOG_LINK" ]; then LOG_FILE="$LOG_LINK"; else LOG_FILE="$RUN_DIR/stickmate.log"; fi
SHOT_DIR="${STICKMATE_SHOT_DIR:-$RUN_DIR/screenshots}"
# ★ 회사명은 ProjectSettings.asset 의 companyName 과 반드시 같아야 한다(2026-09-02: DefaultCompany
#   -> Vibelab). Unity 는 persistentDataPath / Player.log 를 companyName 으로 조립하므로, 이 값이
#   어긋나면 아래 두 경로가 **존재하지 않는 디렉터리**를 가리킨다. 그때 `[ -f ]` 는 조용히 거짓이
#   되고 세이브 백업이 건너뛰어지는데, 출력은 "백업할 게 없었다"와 **완전히 똑같이 생겼다**.
#   그래서 아래 cmd_start 는 파일이 없을 때 반드시 한 줄을 찍는다(침묵 금지).
STICKMATE_COMPANY="${STICKMATE_COMPANY:-Vibelab}"
SAVE_FILE="$HOME/Library/Application Support/$STICKMATE_COMPANY/StickMate/stickmate_character.json"
USER_LOG="$HOME/Library/Logs/$STICKMATE_COMPANY/StickMate/Player.log"
UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity}"

mkdir -p "$RUN_DIR" "$SHOT_DIR" "$SKILL_DIR/bin" "$LOG_DIR"

say() { printf '%s\n' "$*"; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

# ------------------------------------------------------- 런타임 플래그 신고/되읽기
# ★ 2026-09-02 (dev-platform). persona-stress 신고:
#   "driver.sh에 STICKMATE_* 설정이 한 줄도 없다 — 기본값(켬)으로 도는 것으로 보이지만
#    드라이버가 명시적으로 켜 주지 않으므로 우리 실기 측정이 적응형 페이싱 게이트를
#    실제로 통과했는지 보증되지 않는다."
#
#   ★ 사실 정정: 이 파일에는 STICKMATE_ 가 3건 있다(REPO/RUN_DIR/SHOT_DIR). 다만 그건 전부
#     **드라이버 자신의 경로 오버라이드**이고, **런타임 기능 플래그는 정말로 0건**이다.
#     신고의 실질은 옳다.
#
#   ★ 그런데 처방은 "드라이버가 =1 로 켠다"가 **아니다.** 그건 두 가지를 동시에 망친다:
#     (a) 드라이버가 **프로덕션 기본값을 베끼는 것**이 된다. 코드 기본값이 언젠가 꺼지는 쪽으로
#         바뀌어도 드라이버가 계속 켜 주므로 러너는 그 회귀를 **영원히 못 본다**
#         (CLAUDE.md: 기준과 대상이 함께 움직이면 아무것도 못 잰다).
#     (b) 이 변수들의 존재 이유가 재빌드 없는 A/B인데, 드라이버가 값을 고정하면 A/B가 막힌다.
#
#   ★ 진짜 문제는 "안 켜 준다"가 아니라 **"무엇으로 돌았는지 기록이 없다"**이다.
#     앱은 셸 환경을 그대로 상속하므로, 어떤 라운드가 STICKMATE_FORCE_TIER=Active 를 export 한
#     셸에서 start 를 부르면 **다음 측정이 조용히 다른 구성으로 돈다** — 이 저장소의 서명 사고
#     ("실패한 측정과 성공한 측정이 똑같이 생겼다")가 그대로 재현되는 자리다.
#
#   그래서 하는 일은 값 설정이 아니라 둘이다:
#     1) declare_runtime_flags — 지금 셸에서 **무엇이 자식에게 상속되는가**를 찍는다.
#        목록은 **소스 트리에서 뽑는다.** 손으로 적으면 변수가 늘어난 날 목록만 낡는다.
#     2) report_resolved_gates — 부팅 뒤 **앱 자신의 로그에서 해석된 상태를 되읽는다.**
#        "설정했다"가 아니라 "그렇게 해석됐다"가 측정의 근거다.
declare_runtime_flags() {
  local names n v set_count=0
  names="$(grep -rhoE 'STICKMATE_[A-Z_]+' "$REPO/Assets/_Project/Scripts" 2>/dev/null \
           | sort -u | grep -vE '^STICKMATE_(REPO|RUN_DIR|SHOT_DIR|COMPANY)$')"
  if [ -z "$names" ]; then
    # 침묵 금지 — "변수가 없다"와 "소스를 못 읽었다"를 구분해 준다(이 파일의 기존 규약).
    say "런타임 플래그: ★ 소스에서 STICKMATE_* 를 한 개도 못 뽑았다 — $REPO/Assets 경로를 확인하라."
    say "               (이 상태에서는 아래 '상속되는 플래그' 목록이 비어도 아무 뜻이 없다.)"
    return 0
  fi
  say "런타임 플래그(자식에게 그대로 상속된다. 드라이버는 값을 정하지 않는다):"
  while read -r n; do
    [ -n "$n" ] || continue
    v="$(printenv "$n" 2>/dev/null)"
    if [ -n "${v:-}" ]; then
      say "  · $n=$v   ← ★ 이 셸에 설정돼 있다. 이번 측정은 이 구성이다."
      set_count=$((set_count+1))
    fi
  done <<< "$names"
  [ "$set_count" -eq 0 ] && say "  · (설정된 것 없음 — 전부 코드 기본값으로 해석된다. 그 해석 결과는 아래에서 되읽는다.)"
  return 0
}

# 앱이 **스스로 찍은** 해석 결과를 읽는다. 드라이버의 가정이 아니라 앱의 사실이다.
# 세 갈래로 갈린다: 활성 / 비활성 / 못 찾음. 셋을 구분하지 않으면 '못 찾음'이 '활성'으로 읽힌다.
report_resolved_gates() {
  local line
  line="$(grep -am1 '\[FramePacing/적응형\]' "$LOG_FILE" 2>/dev/null)"
  if [ -z "$line" ]; then
    say "해석된 게이트: ★ 못 찾음 — 로그에 [FramePacing/적응형] 줄이 없다."
    say "               이 인스턴스로 잰 관측 의존 수치(발판 스캔 억제 등)는 구성 미상이다."
    return 0
  fi
  case "$line" in
    *"적응형] 활성"*)
      say "해석된 게이트: 적응형 페이싱 = 활성 (관측 갱신됨 → FramePacing.LastPresence 유효)" ;;
    *"적응형] 비활성"*)
      say "해석된 게이트: ★ 적응형 페이싱 = **비활성**."
      say "               이때 FramePacing.LastPresence 는 Valid=false 로 남고, 그것을 읽는"
      say "               게이트(FootholdPoller 의 세션 가시성 억제 등)는 **영원히 거짓**이 된다."
      say "               기능은 안전한 쪽으로 꺼질 뿐이지만, 그 게이트를 잰다고 주장하면 그건 거짓 측정이다." ;;
    *)
      say "해석된 게이트: ★ 판독 실패 — 줄은 찾았으나 활성/비활성을 못 갈랐다: $line" ;;
  esac
  return 0
}

# ---------------------------------------------------------------- 헬퍼 빌드
# swiftc는 Xcode Command Line Tools에 포함된다. 소스가 더 새로우면 다시 컴파일한다.
build_helper() {
  local name="$1" src="$SKILL_DIR/src/$1.swift" out="$SKILL_DIR/bin/$1"
  [ -f "$src" ] || die "소스 없음: $src"
  if [ ! -x "$out" ] || [ "$src" -nt "$out" ]; then
    command -v swiftc >/dev/null || die "swiftc 없음 — Xcode Command Line Tools 필요"
    swiftc -O -o "$out" "$src" || die "$name 컴파일 실패"
  fi
  printf '%s' "$out"
}

# macOS ANSI 가상 키코드. 앱이 쓰는 동작키 17종만 담는다(GlobalKey 열거형과 1:1).
# 그 아래는 '주입하면 안 되는' 조합의 명시적 거부 목록이다 — 이유는 그 자리에 적혀 있다.
keycode_for() {
  case "$(printf '%s' "$1" | tr '[:lower:]' '[:upper:]')" in
    A) echo 0;;  S) echo 1;;  D) echo 2;;  F) echo 3;;  H) echo 4;;  G) echo 5;;
    X) echo 7;;  C) echo 8;;  B) echo 11;; Q) echo 12;; R) echo 15;; T) echo 17;;
    I) echo 34;; J) echo 38;; K) echo 40;; N) echo 45;;
    P) echo 35;;  # kVK_ANSI_P — 설정창 열기/닫기(Preferences)

    # ★ ⌃⌥⌘ + 8 / , / . 는 macOS 접근성 시스템 단축키다(색 반전 / 대비 늘리기 / 대비 줄이기,
    #   symbolic hotkey 21 / 25 / 26). 주입하면 **사용자의 OS 대비 설정이 실제로 바뀐다** —
    #   설정창 단축키가 원래 쉼표였다가 2026-09-01에 P로 옮겨진 이유가 바로 이것이다.
    #   실수로 다시 넣지 못하게 여기서 명시적으로 막는다(조용한 미지원이 아니라 사유를 말한다).
    "8"|","|".")
      say "거부: ⌃⌥⌘$1 는 macOS 접근성 예약 조합이다(색반전/대비±). 주입하면 사용자 OS 설정이" >&2
      say "      실제로 바뀐다. 설정창은 이제 P다 -> driver.sh key P" >&2
      return 1;;
    *) return 1;;
  esac
}

our_pid() { [ -f "$PID_FILE" ] && cat "$PID_FILE" || true; }
is_alive() { [ -n "${1:-}" ] && kill -0 "$1" 2>/dev/null; }

# 우리가 띄운 것 말고 다른 StickMate 인스턴스(대개 사용자 본인이 쓰는 것)의 PID.
other_pids() {
  local mine; mine="$(our_pid)"
  pgrep -f "StickMate.app/Contents/MacOS/StickMate" 2>/dev/null | grep -v -x "${mine:-__none__}" || true
}

# ---------------------------------------------------------------- doctor
cmd_doctor() {
  local rc=0
  say "== StickMate 구동 환경 점검 =="
  say ""
  say "[빌드 산출물]"
  if [ -x "$BIN" ]; then
    say "  OK   $BIN"
    say "       빌드 시각: $(stat -f '%Sm' -t '%Y-%m-%d %H:%M' "$BIN")"
  else
    say "  없음 $BIN  -> driver.sh build 필요"; rc=1
  fi
  say ""
  say "[필수 도구]"
  for t in swiftc screencapture osascript pgrep; do
    if command -v "$t" >/dev/null; then say "  OK   $t"; else say "  없음 $t"; rc=1; fi
  done
  say ""
  say "[전역 단축키 가능 여부] ★ 이 스킬에서 가장 중요한 항목"
  local kc; kc="$(build_helper keycheck)"
  "$kc" | sed 's/^/  /'
  local krc=$?
  [ $krc -ne 0 ] && rc=1
  say ""
  say "[실행 중인 인스턴스]"
  local mine others; mine="$(our_pid)"; others="$(other_pids)"
  if is_alive "$mine"; then say "  드라이버가 띄운 인스턴스: PID $mine (로그 $LOG_FILE)"
  else say "  드라이버가 띄운 인스턴스: 없음"; fi
  if [ -n "$others" ]; then
    say "  그 밖의 인스턴스: $(echo "$others" | tr '\n' ' ')"
    say "  ※ 사용자가 직접 쓰고 있는 인스턴스일 수 있다 — 절대 kill 하지 말 것."
  else
    say "  그 밖의 인스턴스: 없음"
  fi
  say ""
  say "[Unity 배치모드 락] 다른 에이전트가 Library/를 쓰고 있으면 빌드/테스트가 깨진다"
  if pgrep -f "Unity.app/Contents/MacOS/Unity .*-projectPath.*StickMate" >/dev/null 2>&1; then
    say "  사용 중 — 지금 build/test 하지 말 것"; rc=1
  elif [ -f "$REPO/Temp/UnityLockfile" ]; then
    say "  Temp/UnityLockfile 존재 — 에디터가 열려 있을 수 있음"; rc=1
  else
    say "  비어 있음(배치모드 실행 가능)"
  fi
  return $rc
}

# ---------------------------------------------------------------- start
cmd_start() {
  [ -x "$BIN" ] || die "빌드 산출물이 없다: $BIN  (driver.sh build 참고)"
  local mine; mine="$(our_pid)"
  if is_alive "$mine"; then say "이미 실행 중: PID $mine"; return 0; fi

  local others; others="$(other_pids)"
  if [ -n "$others" ]; then
    say "주의: 다른 StickMate 인스턴스가 이미 있다(PID $(echo "$others" | tr '\n' ' '))."
    say "      사용자 본인의 것일 수 있으니 건드리지 않는다. 화면에 캐릭터가 2명 보이는 것은 정상이며,"
    say "      두 인스턴스가 같은 세이브 파일을 공유한다는 점만 유의(SKILL.md Gotchas 참고)."
  fi

  # 세이브 백업. 자동 복원은 하지 않는다 — 사용자 인스턴스가 동시에 진행도를 쓰고 있을 수 있어
  # 되돌리면 오히려 사용자의 실제 진행을 지우게 된다.
  if [ -f "$SAVE_FILE" ]; then
    local bak="$RUN_DIR/save-backup-$(date +%Y%m%d-%H%M%S).json"
    cp "$SAVE_FILE" "$bak" && say "세이브 백업: $bak"
  else
    # 침묵 금지. "세이브가 원래 없다"와 "회사명이 어긋나 엉뚱한 곳을 봤다"를 구분해 준다.
    say "세이브 백업 건너뜀 — 파일이 없다: $SAVE_FILE"
    say "      (회사명=$STICKMATE_COMPANY. ProjectSettings.asset 의 companyName 과 다르면 이 경로가 틀린 것이다.)"
  fi

  # ★ 이번 실행 전용 로그. 기존 파일을 자르지 않으므로 **살아 있는 다른 인스턴스의 로그를
  #   절대 훼손하지 않는다**(위 LOG_FILE 주석의 NUL 구멍 사고 처방).
  local run_log="$LOG_DIR/run-$(date +%Y%m%d-%H%M%S)-$$.log"
  : > "$run_log"
  ln -sfn "$run_log" "$LOG_LINK"
  LOG_FILE="$LOG_LINK"
  declare_runtime_flags

  # ★ `open` 이 아니라 셸에서 직접 exec 한다. 그래야 이 셸의 Input Monitoring 권한을 물려받아
  #   전역 단축키가 동작한다(SKILL.md "왜 open 을 쓰면 안 되는가" 참고).
  # ★ -logFile 로 로그를 분리한다. 그러지 않으면 Unity가 사용자 인스턴스의 Player.log를
  #   Player-prev.log로 밀어내며 로그 연속성을 망가뜨린다.
  nohup "$BIN" -logFile "$run_log" >/dev/null 2>&1 &
  local pid=$!
  disown "$pid" 2>/dev/null || true   # 나중에 kill 할 때 bash가 "Terminated: 15" 잡 제어 잡음을 찍지 않게
  echo "$pid" > "$PID_FILE"
  say "실행: PID $pid"
  say "로그: $run_log  (안정 이름 $LOG_LINK 이 여기를 가리킨다)"

  # 부팅 배너가 찍힐 때까지 최대 40초 기다린다.
  local i
  for i in $(seq 1 80); do
    if grep -aq "앱제어] 준비 완료" "$LOG_FILE" 2>/dev/null; then
      say "부팅 완료 (${i}회 폴링)"
      report_resolved_gates
      return 0
    fi
    is_alive "$pid" || { say "프로세스가 죽었다. 로그 마지막:"; tail -20 "$LOG_FILE"; return 1; }
    sleep 0.5
  done
  say "경고: 40초 안에 부팅 배너를 못 찾았다. 로그 마지막:"; tail -20 "$LOG_FILE"; return 1
}

# ---------------------------------------------------------------- stop
# ★ SIGTERM(kill)을 쓰지 않는다. macOS Unity 6000.0.82f1에서는 SIGTERM 경로로 OnApplicationQuit이
#   아예 호출되지 않아 진행도가 저장되지 않는다(Unity 이슈트래커 등록 건). 대신
#   NSRunningApplication.terminate 로 "정상 종료"를 요청한다. PID를 지정하므로 사용자의 다른
#   인스턴스는 건드리지 않는다 — 전역 단축키 Ctrl+Opt+Cmd+Q는 권한을 가진 모든 인스턴스를 한꺼번에
#   죽여서 자동화 드라이버용으로는 부적합하다.
#
#   ※ JXA 함정: 인자 없는 ObjC 메서드는 '프로퍼티'로 브리지된다. `a.terminate()` 처럼 괄호를 붙이면
#     프로퍼티 접근 시점에 종료가 이미 일어난 뒤 "terminate is not a function" 에러가 나서 exit 1이
#     된다(종료는 됐는데 스크립트만 실패로 보인다). 괄호 없는 `a.terminate` 가 맞다.
cmd_stop() {
  local pid; pid="$(our_pid)"
  if ! is_alive "$pid"; then say "드라이버가 띄운 인스턴스가 없다."; rm -f "$PID_FILE"; return 0; fi
  local base=0; [ -f "$LOG_FILE" ] && base=$(wc -c < "$LOG_FILE" | tr -d ' ')

  local out
  out="$(osascript -l JavaScript -e "ObjC.import('AppKit'); var a=\$.NSRunningApplication.runningApplicationWithProcessIdentifier($pid); a.isNil() ? 'NO_SUCH_PID' : String(a.terminate)" 2>&1)"
  if [ "$out" = "NO_SUCH_PID" ]; then say "PID $pid 를 찾지 못했다(이미 종료된 듯)."; rm -f "$PID_FILE"; return 0; fi
  [ "$out" = "true" ] || say "경고: 종료 요청 반환값이 예상과 다르다: $out"

  local i
  for i in $(seq 1 20); do is_alive "$pid" || break; sleep 0.5; done
  if is_alive "$pid"; then
    say "경고: 정상 종료 요청에 10초 동안 반응이 없다(PID $pid)."
    if [ "${1:-}" = "--force" ]; then
      say "--force 지정됨 -> SIGTERM으로 강제 종료한다. ★ 이 경로는 진행도 저장을 건너뛴다."
      kill -TERM "$pid" 2>/dev/null; sleep 2
    else
      say "진행도를 잃지 않으려면 사람이 확인하는 편이 낫다. 정말 죽이려면: driver.sh stop --force"
      return 1
    fi
  fi
  rm -f "$PID_FILE"

  # 정상 종료 판별 기준 = Unity 종료 시퀀스 3줄의 존재. "저장 로그가 없다"로는 판별할 수 없다 —
  # 저장에 성공해도 앱은 저장 로그를 남기지 않기 때문이다.
  local n=0
  [ -f "$LOG_FILE" ] && n=$(tail -c +$((base+1)) "$LOG_FILE" \
    | grep -acE "Physics::Module\] Cleanup|ShutdownInProgress|state changed to: Shutdown\.")
  if [ "$n" -ge 3 ]; then
    say "종료됨: PID $pid — Unity 종료 시퀀스 ${n}/3줄 확인(정상 종료, 진행도 저장 경로 실행됨)."
  else
    say "종료됨: PID $pid — ★ 종료 시퀀스가 ${n}/3줄뿐이다. 정상 종료가 아니었을 수 있다(저장 유실 의심)."
    return 1
  fi
}

# ---------------------------------------------------------------- key
cmd_key() {
  local letter="${1:-}" hold="${2:-400}"
  [ -n "$letter" ] || die "사용법: driver.sh key <글자> [홀드ms]"
  local code; code="$(keycode_for "$letter")" || die "지원하지 않는 키: $letter"
  local pid; pid="$(our_pid)"
  is_alive "$pid" || say "경고: 드라이버 인스턴스가 실행 중이 아니다. 그래도 키는 전역으로 주입한다."

  local kh; kh="$(build_helper keyhold)"
  local base=0; [ -f "$LOG_FILE" ] && base=$(wc -c < "$LOG_FILE" | tr -d ' ')
  # 59=Control 58=Option 55=Command, 그 다음이 동작키.
  "$kh" "$hold" 59 58 55 "$code" || die "키 주입 실패"
  sleep 1.2
  say "주입: Ctrl+Opt+Cmd+$(printf '%s' "$letter" | tr '[:lower:]' '[:upper:]') (키코드 $code, ${hold}ms 유지)"
  if [ -f "$LOG_FILE" ]; then
    # 주의: 모든 동작이 [앱제어] 로 찍히지는 않는다. AppControlDirector가 각 연출 디렉터에게
    # 넘기는 항목(A/K/G/T/X/H/S/N/J/F)은 그 디렉터 고유 태그로 찍힌다 — 실제로 A(활쏘기)를
    # [앱제어] 로만 찾다가 "반응 없음"으로 오판했다.
    local pat="\[앱제어\]|\[활쏘기\]|\[그라피티\]|\[창도둑\]|\[창 도둑\]|\[윈도우크래시\]|\[하드웨어|\[스트레스|\[가출\]|\[투두\]|\[집중|\[정보창\]|\[설정창\]|\[성장\]|\[기록\]"
    local new; new="$(tail -c +$((base+1)) "$LOG_FILE" | grep -aE "$pat" || true)"
    if [ -n "$new" ]; then
      say "로그 확인:"; printf '%s\n' "$new" | head -6 | cut -c1-220 | sed 's/^/  /'
    else
      say "로그에 아무 반응이 없다 -> driver.sh doctor 로 입력 모니터링 권한을 확인하라."
      say "(캐릭터가 Idle/Walk가 아니면 B 같은 항목은 의도적으로 건너뛴다 — 몇 초 뒤 재시도)"
      return 3
    fi
  fi
}

# ---------------------------------------------------------------- shot
# 캐릭터는 창 위쪽 테두리(대개 Dock 상단)를 따라 돌아다닌다. 로그의 60초 심장박동
# [발판리포트]가 알려주는 발판 y를 기준으로 가로 전체 띠를 찍는 것이 가장 확실하다.
cmd_shot() {
  local name="${1:-shot-$(date +%H%M%S)}"
  local band_y=780
  if [ -f "$LOG_FILE" ]; then
    local y; y="$(grep -ao '발판상단OS y=[0-9.]*' "$LOG_FILE" | tail -1 | sed 's/[^0-9.]*//')"
    [ -n "$y" ] && band_y=$(printf '%.0f' "$(echo "$y" | awk '{print $1-125}')")
  fi
  local full="$SHOT_DIR/$name-full.png" band="$SHOT_DIR/$name-band.png"
  screencapture -x "$full" || die "전체 캡처 실패(화면 기록 권한 확인)"
  # -R 은 물리 픽셀이 아니라 '포인트' 단위다. Retina에서는 결과 PNG가 2배 크기로 나온다.
  screencapture -x -R 0,"$band_y",1512,180 "$band" || die "띠 캡처 실패"
  say "전체 화면: $full"
  say "캐릭터 띠: $band   (y=$band_y pt 부터 180pt, 캐릭터는 이 띠 안에 있다)"
}

cmd_status() {
  local pid; pid="$(our_pid)"
  if is_alive "$pid"; then say "실행 중: PID $pid"; else say "드라이버 인스턴스: 없음"; fi
  say "로그: $LOG_FILE"
  local others; others="$(other_pids)"
  [ -n "$others" ] && say "그 밖의 인스턴스(건드리지 말 것): $(echo "$others" | tr '\n' ' ')"
  return 0
}

# ---------------------------------------------------------------- orphans
# ★ 2026-09-02 (디버거) 신설 — **보고 전용. 어떤 프로세스도 죽이지 않는다.**
#   계기: PID 30572가 13:18부터 5시간 49분 동안 PPID=1로 살아 있었는데 $PID_FILE이 없어서
#   `status`/`doctor` 어디에도 "고아"라는 말이 뜨지 않았다. 그 상태에서 start를 또 하면
#   공유 로그가 잘려 NUL 구멍이 생긴다(위 LOG_FILE 주석).
#   판정 근거를 사람이 읽을 수 있게 **그대로 늘어놓기만** 한다 — 드라이버가 띄운 것인지
#   사용자 것인지는 사람이 정한다. 자동 정리는 하지 않는다(사용자 인스턴스를 죽일 위험).
cmd_orphans() {
  local mine; mine="$(our_pid)"
  say "== StickMate 인스턴스 전수 =="
  say "PID 파일: ${PID_FILE}$([ -f "$PID_FILE" ] && echo " (내용 $mine)" || echo " (없음)")"
  say ""
  local any=0 pid
  while read -r pid; do
    [ -n "$pid" ] || continue
    any=1
    local ppid args started etime
    ppid="$(ps -p "$pid" -o ppid= 2>/dev/null | tr -d ' ')"
    started="$(ps -p "$pid" -o lstart= 2>/dev/null | sed 's/^ *//;s/ *$//')"
    etime="$(ps -p "$pid" -o etime= 2>/dev/null | tr -d ' ')"
    args="$(ps -p "$pid" -o command= 2>/dev/null)"
    say "  PID $pid  (부모 ${ppid:-?}, 시작 ${started:-?}, 경과 ${etime:-?})"
    say "    인자: $args"
    # ★ PPID=1 자체는 신호가 아니다. 이 하니스는 `nohup ... &` 로 띄우고 호출한 셸이 곧
    #   끝나므로 **정상적으로 띄운 인스턴스도 즉시 PPID=1이 된다**(2026-09-02 실측: 동시에
    #   돌던 페르소나 라운드 3개가 전부 PPID=1이었다). 그걸 고아로 부르면 오탐만 쌓인다.
    #   실제로 위험한 조합은 이것이다: **PID 파일에 없는데 + 드라이버 기본 로그 경로를 쓴다.**
    #   그 둘이 겹치면 다음 start 가 그 파일을 잘라 NUL 구멍을 만든다.
    if [ "$pid" = "${mine:-__none__}" ]; then
      say "    판정: 드라이버가 띄운 것(PID 파일과 일치). stop 으로 정리된다."
    else
      case "$args" in
        *"-logFile $RUN_DIR/"*)
          say "    판정: ★★충돌 위험 — PID 파일에 없는데 **드라이버 기본 로그 경로**($RUN_DIR)를 쓴다."
          say "         다음 start 가 이 로그를 자르면 이 프로세스의 쓰기가 NUL 구멍을 만든다."
          say "         (이 스크립트의 최신판은 실행마다 로그를 갈라 더는 이 경로를 자르지 않는다.)";;
        *"-logFile "*)
          say "    판정: 자기 전용 로그를 쓰는 별도 인스턴스. 로그 충돌 없음.";;
        *)
          say "    판정: -logFile 인자가 없다 -> 사용자가 GUI로 띄운 것일 가능성이 높다.";;
      esac
      say "         ※ 어느 경우에도 여기서 죽이지 않는다. 사람이 확인하고 결정한다."
    fi
    say ""
  done <<< "$(pgrep -f "StickMate.app/Contents/MacOS/StickMate" 2>/dev/null)"
  [ "$any" = "1" ] || say "  실행 중인 인스턴스 없음."
  say "로그 파일 목록(실행마다 분리):"
  ls -1t "$LOG_DIR" 2>/dev/null | head -10 | sed 's/^/  /' || say "  (없음)"
  return 0
}

cmd_log() { [ -f "$LOG_FILE" ] || die "로그 없음: $LOG_FILE"; if [ "${1:-}" = "-f" ]; then tail -f "$LOG_FILE"; else tail -"${1:-40}" "$LOG_FILE"; fi; }

# 배너에서 최신 단축키 목록을 그대로 뽑는다(문서에 베껴 적지 않고 앱에게 직접 묻는다).
cmd_keys() {
  local src="$LOG_FILE"; [ -s "$src" ] || src="$USER_LOG"
  [ -f "$src" ] || die "로그가 없다. driver.sh start 를 먼저 실행하라."
  grep -am1 "앱제어] 준비 완료" "$src" || die "배너를 찾지 못했다."
}

# ---------------------------------------------------------------- demo
cmd_demo() {
  cmd_start || return 1
  say ""; say "-- 전역 단축키 B(말풍선 즉시 띄우기) 주입 --"
  cmd_key B || { say "단축키가 먹지 않았다. doctor 를 확인하라."; cmd_stop; return 3; }
  say ""; say "-- 말풍선이 떠 있는 동안 화면 캡처 --"
  # 키를 백그라운드로 길게 눌러 둔 채 캡처한다. `wait` 는 반드시 이 키 주입 PID만 기다린다 —
  # 인자 없는 `wait` 는 nohup 으로 띄운 StickMate 본체까지 기다려 영원히 멈춘다(실제로 겪은 함정).
  local kh; kh="$(build_helper keyhold)"
  "$kh" 700 59 58 55 11 &
  local kpid=$!
  sleep 0.6
  cmd_shot demo
  wait "$kpid" 2>/dev/null || true
  say ""; say "-- 종료 --"
  cmd_stop
}

cmd_build() {
  [ "${1:-}" = "--force" ] || die "빌드는 다른 에이전트의 Unity 배치모드와 충돌할 수 있다. 확인 후 --force 로 실행하라 (driver.sh doctor 로 락 상태 먼저 확인)."
  [ -x "$UNITY" ] || die "Unity 없음: $UNITY"
  "$UNITY" -batchmode -nographics -quit -projectPath "$REPO" \
    -executeMethod StickMate.EditorTools.BuildStandalone.PerformBuild \
    -logFile "$RUN_DIR/build.log"
  local rc=$?; say "빌드 종료코드 $rc, 로그 $RUN_DIR/build.log"; return $rc
}

cmd_test() {
  [ "${1:-}" = "--force" ] || die "테스트도 Library/ 락을 잡는다. --force 로 실행하라."
  [ -x "$UNITY" ] || die "Unity 없음: $UNITY"
  "$UNITY" -batchmode -nographics -runTests -projectPath "$REPO" \
    -testPlatform EditMode -testResults "$RUN_DIR/editmode-results.xml" \
    -logFile "$RUN_DIR/test.log"
  local rc=$?; say "테스트 종료코드 $rc, 결과 $RUN_DIR/editmode-results.xml"; return $rc
}

usage() {
  cat <<'USAGE'
사용법: driver.sh <명령>

  doctor            환경/권한/락 점검 (문제 생기면 여기부터)
  start             인스턴스 실행 (셸에서 직접 exec — 단축키가 동작하는 유일한 방법)
  stop              드라이버가 띄운 인스턴스만 SIGTERM
  status            PID/로그 위치
  keys              앱이 배너에 찍은 최신 단축키 목록 그대로 출력
  key <글자> [ms]   전역 단축키 주입 후 [앱제어] 로그로 발동 확인 (기본 400ms 유지)
  shot [이름]       전체 화면 + 캐릭터가 있는 가로 띠 캡처
  log [-f|줄수]     로그 보기
  orphans           실행 중인 인스턴스 전수 + 고아 판정 (★보고 전용, 절대 kill 하지 않는다)
  demo              start -> key B -> 캡처 -> stop 전체 왕복
  build --force     Unity 배치모드 macOS 빌드 (락 확인 후에만)
  test --force      EditMode 테스트 (락 확인 후에만)
USAGE
}

case "${1:-}" in
  doctor) shift; cmd_doctor "$@";;
  start)  shift; cmd_start "$@";;
  stop)   shift; cmd_stop "$@";;
  status) shift; cmd_status "$@";;
  keys)   shift; cmd_keys "$@";;
  key)    shift; cmd_key "$@";;
  shot)   shift; cmd_shot "$@";;
  log)    shift; cmd_log "$@";;
  orphans) shift; cmd_orphans "$@";;
  demo)   shift; cmd_demo "$@";;
  build)  shift; cmd_build "$@";;
  test)   shift; cmd_test "$@";;
  *) usage; [ -z "${1:-}" ] && exit 1 || exit 0;;
esac
