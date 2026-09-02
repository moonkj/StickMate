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
LOG_FILE="$RUN_DIR/stickmate.log"
SHOT_DIR="${STICKMATE_SHOT_DIR:-$RUN_DIR/screenshots}"
SAVE_FILE="$HOME/Library/Application Support/DefaultCompany/StickMate/stickmate_character.json"
USER_LOG="$HOME/Library/Logs/DefaultCompany/StickMate/Player.log"
UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity}"

mkdir -p "$RUN_DIR" "$SHOT_DIR" "$SKILL_DIR/bin"

say() { printf '%s\n' "$*"; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

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
  fi

  : > "$LOG_FILE"
  # ★ `open` 이 아니라 셸에서 직접 exec 한다. 그래야 이 셸의 Input Monitoring 권한을 물려받아
  #   전역 단축키가 동작한다(SKILL.md "왜 open 을 쓰면 안 되는가" 참고).
  # ★ -logFile 로 로그를 분리한다. 그러지 않으면 Unity가 사용자 인스턴스의 Player.log를
  #   Player-prev.log로 밀어내며 로그 연속성을 망가뜨린다.
  nohup "$BIN" -logFile "$LOG_FILE" >/dev/null 2>&1 &
  local pid=$!
  disown "$pid" 2>/dev/null || true   # 나중에 kill 할 때 bash가 "Terminated: 15" 잡 제어 잡음을 찍지 않게
  echo "$pid" > "$PID_FILE"
  say "실행: PID $pid"
  say "로그: $LOG_FILE"

  # 부팅 배너가 찍힐 때까지 최대 40초 기다린다.
  local i
  for i in $(seq 1 80); do
    if grep -q "앱제어] 준비 완료" "$LOG_FILE" 2>/dev/null; then
      say "부팅 완료 (${i}회 폴링)"
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
    | grep -cE "Physics::Module\] Cleanup|ShutdownInProgress|state changed to: Shutdown\.")
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
    local new; new="$(tail -c +$((base+1)) "$LOG_FILE" | grep -E "$pat" || true)"
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
    local y; y="$(grep -o '발판상단OS y=[0-9.]*' "$LOG_FILE" | tail -1 | sed 's/[^0-9.]*//')"
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

cmd_log() { [ -f "$LOG_FILE" ] || die "로그 없음: $LOG_FILE"; if [ "${1:-}" = "-f" ]; then tail -f "$LOG_FILE"; else tail -"${1:-40}" "$LOG_FILE"; fi; }

# 배너에서 최신 단축키 목록을 그대로 뽑는다(문서에 베껴 적지 않고 앱에게 직접 묻는다).
cmd_keys() {
  local src="$LOG_FILE"; [ -s "$src" ] || src="$USER_LOG"
  [ -f "$src" ] || die "로그가 없다. driver.sh start 를 먼저 실행하라."
  grep -m1 "앱제어] 준비 완료" "$src" || die "배너를 찾지 못했다."
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
  demo)   shift; cmd_demo "$@";;
  build)  shift; cmd_build "$@";;
  test)   shift; cmd_test "$@";;
  *) usage; [ -z "${1:-}" ] && exit 1 || exit 0;;
esac
