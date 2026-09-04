#!/usr/bin/env bash
# =============================================================================
# StickMate 전량 회귀 러너 — qa-regression 전용 (2026-09-02 신설)
#
#   사용법:
#     docs/verify/regress.sh edit  <라벨>          # EditMode 전량
#     docs/verify/regress.sh play  <라벨>          # PlayMode 전량
#     docs/verify/regress.sh report <결과.xml>     # 이미 있는 결과 파일 판독만
#     docs/verify/regress.sh compare <옛.xml> <새.xml>  # ★ 베이스라인 대조(귀속용)
#     docs/verify/regress.sh gate <log> <xml> <edit|play>   # ★ 세이브 격리 게이트 회계만 판독
#     docs/verify/regress.sh target                # 활성 빌드 타깃만 판정
#     docs/verify/regress.sh selfcheck             # ★ 가드가 실제로 무는지(음성 대조)
#
# 이 스크립트의 유일한 목적: **실패한 측정과 성공한 측정을 다르게 생기게 만드는 것.**
# 2026-09-02 하루에 거짓 통과 9건이 났고, 그중 5건이 "러너를 사람이 잘 쓰면 된다"로는 못 막혔다.
# 그래서 전부 자동 거부로 바꿨다 — 아래 가드 중 하나라도 걸리면 종료코드가 0이 아니다.
#
#  G1  -quit 금지            : -runTests와 함께 주면 0건 실행 + 종료코드 0이 된다.
#  G2  콤마 필터 금지         : -testFilter A,B 는 조용히 0건이 된다.
#  G3  결과 파일 선삭제 + mtime: 이틀 전 xml을 새 결과로 읽은 사고 재발 방지.
#  G4  testcasecount 하한     : 필터가 먹어 1건만 돌았는데 "전량 초록"이라고 말하지 못하게.
#  G5  total == testcasecount : 부분 실행(final3-play.xml: tcc=529 / total=106)을 전량으로 오인 금지.
#  G6  Library 락 선점 확인    : 다른 라운드의 Unity가 돌면 즉시 거부(컴파일 불가 트리 측정 방지).
#  G7  활성 빌드 타깃 기록     : 리플렉션 감사는 타깃에 종속이다. 어느 타깃에서 잰 것인지 남긴다.
#  G8  컴파일 실패 감지        : "Aborting batchmode due to failure: Scripts have compiler errors"
#                              가 로그에 있으면 결과 xml이 있어도 무효다.
#  G9  직전 실행 대비 건수 감소 : 정적 하한(G4)은 테스트가 늘어도 따라오지 않는다. 실제로 뚫린 구멍이다
#                              (2026-09-02: 하한 1390 vs 실제 1609 = 219건이 조용히 사라져도 통과).
#  G10 세이브 격리 게이트 회계   : ★ 2026-09-03 신설. 게이트의 **최종** 회계를 러너 xml과 **독립적으로**
#                              대조한다. 이게 없으면 게이트가 실행 도중에 죽어도 스위트가 초록이다.
#
# =============================================================================
# ★★ 2026-09-03 G10 — 「게이트 생존이 실행 중 한 시점에서만 단언된다」
# =============================================================================
# test-engineer 자기 반려 1번(가장 큰 항목). 무엇이 뚫려 있었나:
#
#   PlayMode의 `PlayModeSaveIsolationGate`는 **리프 테스트마다** 격리 저장 폴더를 비운다.
#   그것이 살아 있는지는 `PlayModeSaveIsolationGateTests`가 단언하는데,
#   그 테스트가 도는 시점의 리프 수는 **424/591**이었다(실측).
#   **최종 회계 591/591/0/0은 `Debug.Log`에만 있었고 어떤 테스트도 읽지 않았다.**
#   ⇒ **게이트가 500번째 리프에서 죽어도 스위트는 초록이다.**
#
# 러너가 끝난 뒤에 도는 단언은 NUnit 안에 둘 자리가 없다(그 콜백은 예외를 던지면 러너를 무너뜨린다).
# 그래서 **바깥**에서 읽는다 — 그게 G10이다.
#
# ★ 이 장치의 값어치는 **회계 항등식이 러너 xml과 독립적으로 일치하는 것**이다:
#     (가) `leaf == purge + skip`          ← 게이트 자신의 회계
#     (나) `fail == 0`                      ← 삼킨 예외 0건
#     (다) play: `skip == 0` / edit: `purge == 0`  ← 플랫폼별 설계 기대
#     (라) **`leaf == xml.total == xml.testcasecount`**  ← ★ 서로 다른 두 자로 같은 실행을 잰다
#          하나는 Unity 로그(우리 C# 코드가 센 것), 하나는 NUnit xml(러너가 센 것).
#          둘이 갈라지면 그 자체가 사건이다.
#
# ★ 정규형으로 못박는다(TEAM.md 거짓 통과 13번째: "형태만 세지 말고 값이 채워졌는가까지 봐라").
#   산문 줄이 아니라 `회계 leaf=<정수> purge=<정수> skip=<정수> removed=<정수> fail=<정수>`를 읽는다.
#   값이 빈 줄(`leaf= purge= …`)은 **매치되지 않고 실패**한다 — 형태만 맞고 값이 빈 채 통과하지 못한다.
#
# =============================================================================
# ★★ 2026-09-02 자기 감사 — 이 러너 자신에게서 거짓 통과 2건을 찾았다
# =============================================================================
# 리더 지시: "가드가 재는 것과 러너가 내는 것이 같은 코드에서 나오지는 않는가."
# 확인 결과 G7(활성 타깃 판정)이 **두 겹으로** 거짓말할 수 있는 상태였다.
#
#  (가) <b>죽은 마커</b> — OSX 판정에 `MacOverlayWindow`를 썼는데 그 타입은 이 저장소에
#       <b>한 곳에도 선언돼 있지 않다</b>(선언 파일 0개). 즉 G7의 OSX 분기는 구조적으로
#       <b>절대 참이 될 수 없었다</b>. 그런데도 양성 대조(NullPlatformWindowService)와
#       음성 대조(NoSuchTypeNameXYZ123)는 <b>둘 다 통과</b>했다 — 대조가 "바이트 검색이
#       동작하는가"만 봤고 "마커가 실재하는가"는 아무도 안 봤기 때문이다.
#       → 새 가드 <b>G7a(마커 실재)</b>: 마커 이름이 소스에 타입 선언으로 없으면 UNKNOWN.
#
#  (나) <b>문자열 오염</b> — 이 탐침은 DLL을 <b>원시 바이트</b>로 훑으므로 타입 메타데이터와
#       <b>문자열 리터럴</b>을 구분하지 못한다. 실측: 활성 타깃이 WIN이라 `MacWindowService`
#       타입은 컴파일되지 않았는데도 탐침은 <b>True</b>를 냈다 —
#       `Core/StickConfig.cs`의 `[Tooltip("... MacWindowService가 세어서 넘긴다.")]` 문자열이
#       DLL에 들어가 있기 때문이다. TEAM.md 거짓통과 4번(`strings`로 부재 판정)과 같은 형태다.
#       → 새 가드 <b>G7b(마커 오염)</b>: 마커 이름이 자기 선언 파일 <b>바깥의 코드 문자열</b>에
#         나타나면 그 마커로는 부재를 말할 자격이 없다 → UNKNOWN.
#
# ★ 규칙(TEAM.md "거짓 통과 신형")의 적용: 기준과 대상이 같은 코드에서 나오면 안 된다.
#   그래서 마커 검증은 <b>DLL이 아니라 소스 트리</b>에서 하고(다른 자), 타깃 판정은 DLL에서 한다.
#   selfcheck는 <b>일부러 죽은 마커</b>를 넣어 UNKNOWN이 나오는지 확인한다(진짜 음성 대조).
# =============================================================================
set -uo pipefail

REPO=/Users/kjmoon/App/StickMate
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity
OUTDIR="$REPO/docs/verify/runs"
SRCROOT="$REPO/Assets/_Project/Scripts"

# 전량 기준선(2026-09-03 실측 갱신: edit 1800 / play 591). 실제 건수가 이보다 적으면 "전량"이 아니다.
# 새 테스트가 늘면 올려도 되지만 **내리는 것은 금지** — 내리는 순간 이 가드가 죽는다.
# ★ 이 정적 하한만으로는 부족하다(G9 문서 참고). 실제 방어선은 G9(직전 실행 대비)다.
#
# ★★ 2026-09-03 — 이 두 줄이 **낡아서 아무것도 못 재는 상태**로 하루를 넘겼다.
#    하한 1390 vs 실측 1800 = **410건이 조용히 사라져도 G4는 초록**이었다.
#    같은 병이 `.claude/agents/qa-regression.md`에도 있었다(«1,394 / 558» vs 실측 «1800 / 591»).
#    ⇒ 숫자를 손으로 관리하는 한 반드시 다시 낡는다. 그래서 **G4a(하한 노후 감지)**를 넣었다:
#      직전 최대치의 95%에 못 미치는 하한은 실행할 때마다 **시끄럽게** 자기 노후를 신고한다.
#      (초록을 빨갛게 만들지는 않는다 — 그러면 테스트를 정당하게 지운 라운드를 막는다.
#       대신 **조용한 채로 남지 못하게** 한다. 이 저장소의 병은 언제나 '조용함'이었다.)
#
# ★ 2026-09-05 qa-r9 갱신 — G4a가 두 모드 모두에서 노후를 신고해 그 자리에서 올렸다.
#   실측: edit 1982건 / play 638건. 갱신 전 하한(1780 / 585)으로는 각각 **202건 / 53건이
#   조용히 사라져도 G4가 초록**이었다. 하한은 «직전 최대의 95%» 이상으로 둔다(1882 / 606).
MIN_EDIT_CASES=1900
MIN_PLAY_CASES=610

# 하한 노후 판정 계수 — 직전 최대치의 이 비율보다 하한이 낮으면 노후로 본다.
STALE_FLOOR_RATIO_PCT=95

# ★ 플랫폼 마커. **반드시 실재하는 타입 이름**이라야 하고, 자기 선언 파일 바깥의
#   코드 문자열에 나오면 안 된다. 아래 두 조건은 G7a/G7b가 매 실행 자동 검증한다
#   — 손으로 지키는 규칙은 이 저장소에서 이미 아홉 번 실패했다.
MARKER_WIN=Win32WindowService
MARKER_OSX=MacSpaceBehaviorNative
MARKER_ALWAYS=NullPlatformWindowService   # 양성 대조: 플랫폼 무관하게 항상 컴파일된다
MARKER_NEVER=NoSuchTypeNameXYZ123         # 음성 대조: 존재할 리 없는 이름

die() { echo "✗ $*" >&2; exit 1; }

# ---- G7 활성 빌드 타깃 -------------------------------------------------------
# 실제로 컴파일된 어셈블리에게 묻는다 — 플랫폼 전용 타입이 그 안에 있는가.
# #if 로 잘려 나간 타입은 이름이 메타데이터에 존재하지 않는다. rsp 파일의 mtime보다 이것이 사실이다
# (2026-09-02 실측: 플레이어 빌드가 반대편 rsp를 더 새것으로 만들어 mtime 판정이 거짓말을 했다).
#
# ★ 단, 바이트 검색은 타입과 문자열을 구분하지 못한다 — 그래서 마커 자체를 먼저 검증한다.
#   G7a: 마커가 소스에 타입 선언으로 실재하는가 (죽은 마커 방지)
#   G7b: 마커가 자기 선언 파일 바깥의 코드 문자열에 오염돼 있지 않은가 (부재 판정 자격)
verify_marker() {   # $1=마커 이름 → 0=쓸 수 있다 / 1=못 쓴다(사유를 stdout에)
  local m="$1" decl gate polluted
  decl=$(grep -rlE "(class|struct|interface|enum)[[:space:]]+${m}\b" --include="*.cs" "$SRCROOT" 2>/dev/null | head -1)
  if [ -z "$decl" ]; then
    echo "G7a:마커 '${m}'가 소스에 타입 선언으로 없다(죽은 마커 — 이 분기는 절대 참이 될 수 없다)"
    return 1
  fi

  # ★ 이 마커가 걸려 있는 플랫폼 게이트. 같은 게이트 안의 파일은 **함께 잘려 나가므로**
  #   반대 타깃의 DLL에 그 문자열을 남기지 못한다 = 부재 판정을 오염시킬 수 없다.
  #   (2026-09-02 실측으로 이 구분을 추가했다: `WindowsTopmostWatchdog.cs`의
  #    `Debug.LogWarning("... Win32WindowService.CreateOverlayWindow()가 ...")`가 오염으로 잡혀
  #    양성 대조가 빨개졌는데, 그 파일은 자신도 `#if UNITY_STANDALONE_WIN` 안이라 무해했다.
  #    반대로 `Core/StickConfig.cs`는 **게이트가 없어** 모든 타깃에 컴파일된다 — 그쪽이 진짜다.)
  gate=$(grep -o "UNITY_STANDALONE_[A-Z]*" "$decl" 2>/dev/null | sort -u | head -1)

  # 자기 선언 파일이 아니고, 같은 게이트도 아닌 파일의 **코드 문자열**에 이름이 있는가.
  # (`//`·`///`·`*`로 시작하는 주석 줄은 컴파일되지 않으므로 제외한다.)
  polluted=$(grep -rn "\"[^\"]*${m}" --include="*.cs" "$SRCROOT" 2>/dev/null \
             | grep -v "/Tests/" \
             | grep -v "^${decl}:" \
             | awk -F: '{ line=$0; sub(/^[^:]*:[0-9]*:/, "", line);
                          gsub(/^[ \t]+/, "", line);
                          if (line !~ /^\/\// && line !~ /^\*/) print $1 ":" $2 }' \
             | while IFS=: read -r pf pl; do
                 if [ -n "$gate" ] && grep -q "$gate" "$pf" 2>/dev/null; then continue; fi
                 echo "${pf}:${pl}"; break
               done)
  if [ -n "$polluted" ]; then
    echo "G7b:마커 '${m}'가 게이트 밖 코드 문자열에 있다(${polluted}) — 이 이름으로는 부재를 말할 수 없다"
    return 1
  fi
  return 0
}

active_target() {
  local dll="$REPO/Library/ScriptAssemblies/StickMate.Runtime.dll"
  [ -f "$dll" ] && [ -s "$dll" ] || { echo "UNKNOWN(어셈블리없음)"; return; }

  local reason
  for m in "$MARKER_WIN" "$MARKER_OSX" "$MARKER_ALWAYS"; do
    reason=$(verify_marker "$m") || { echo "UNKNOWN(${reason})"; return; }
  done
  # 음성 대조 마커는 **없어야** 정상이다 — 실재하면 그것대로 탐침이 무의미해진다.
  if grep -rqE "(class|struct|interface|enum)[[:space:]]+${MARKER_NEVER}\b" --include="*.cs" "$SRCROOT" 2>/dev/null; then
    echo "UNKNOWN(음성대조 마커가 실재한다 — MARKER_NEVER를 바꿔라)"; return
  fi

  python3 - "$dll" "$MARKER_WIN" "$MARKER_OSX" "$MARKER_ALWAYS" "$MARKER_NEVER" <<'PYT'
import sys
dll, mwin, mosx, malways, mnever = sys.argv[1:6]
b = open(dll, 'rb').read()
def has(name):
    return b.count(name.encode('utf-8')) > 0 or b.count(name.encode('utf-16-le')) > 0
if not has(malways):
    print('UNKNOWN(양성대조실패)'); raise SystemExit
if has(mnever):
    print('UNKNOWN(음성대조실패)'); raise SystemExit
t = []
if has(mwin): t.append('UNITY_STANDALONE_WIN')
if has(mosx): t.append('UNITY_STANDALONE_OSX')
if len(t) == 2:
    print('UNKNOWN(양쪽 다 잡힘 — 마커가 오염됐다)'); raise SystemExit
print(t[0] if t else 'UNKNOWN(플랫폼타입없음)')
PYT
}

# ---- G1/G2 실행 인자 검사 ----------------------------------------------------
# ★ 2026-09-02 — 예전에는 이 두 가지가 **주석으로만** 존재했다("-quit 없음(G1). 두 줄을 지우지 마라").
#   주석은 아무것도 재지 않는다. 누가 인자를 고치면 헤더는 여전히 "G1~G8"이라고 적혀 있고
#   0건 실행 + 종료코드 0이 그대로 나온다 — 이 러너가 막으려던 바로 그 형태다. 이제 코드로 만든다.
assert_launch_args() {
  local prev="" a
  for a in "$@"; do
    [ "$a" = "-quit" ] && die "G1: 실행 인자에 -quit이 있다 — -runTests와 함께 주면 0건 실행 + 종료코드 0이 된다."
    if [ "$prev" = "-testFilter" ]; then
      case "$a" in
        *,*) die "G2: -testFilter 값에 콤마가 있다('$a') — 콤마 구분 필터는 조용히 0건이 된다." ;;
      esac
    fi
    prev="$a"
  done
  return 0
}

# ★ 2026-09-03 교체 — 옛 구현은 `pgrep -f "Unity.app/Contents/MacOS/Unity -batchmode"`였다.
#   `-f`는 **명령줄 전체**를 본다 = 그 문자열을 담은 스크립트·에디터·grep 자신이 걸린다.
#   앞 라운드가 정확히 그렇게 **자기 자신을 잡고** 50분을 헛기다린 뒤 "포기"를 뱉었고,
#   그 출력은 「정말로 락이 안 풀렸다」와 **똑같이 생겼다**.
#   그래서 **실행파일 경로(comm)** 로 후보를 고르고, 그 PID의 args 에서 -batchmode 를 확인한다.
#   내 셸의 comm 은 /bin/bash 라 구조적으로 자기 자신을 셀 수 없다.
#
# ★ 그리고 「0건」이 「비었다」인지 「프로브가 죽었다」인지 구분되게 **양성 대조**를 붙인다.
#   (실측 2026-09-03: 임시 확인에 `grep -v Hub`를 붙였다가 도는 Unity를 지웠다 —
#    에디터 경로가 `/Applications/Unity/**Hub**/Editor/.../Unity` 라서 필터에 자기가 걸린다.)
UNITY_BIN_PATH="$UNITY"
assert_no_unity_running() {
  local pid pids="" probe
  probe=$(ps -axo pid=,comm= | wc -l | tr -d ' ')
  [ "${probe:-0}" -gt 5 ] || die "G6: 프로세스 탐침이 죽었다(ps가 ${probe}줄). 선점 여부를 판정할 수 없다 — 재면 안 된다."
  while read -r pid; do
    [ -n "$pid" ] || continue
    ps -p "$pid" -o args= 2>/dev/null | grep -q -- "-batchmode" && pids="$pids $pid"
  done < <(ps -axo pid=,comm= | awk -v b="$UNITY_BIN_PATH" 'index($0, b) > 0 {print $1}')
  [ -z "$pids" ] || die "G6: Unity 배치모드가 이미 돌고 있다(PID$pids). Library 락이 잡혀 있으므로 지금 재면 무효다. 리더에게 창(window)을 받아라."
}

# ---- G9 직전 실행 대비 건수 --------------------------------------------------
# 정적 하한(G4)은 테스트가 늘어도 따라오지 않는다. 2026-09-02 실측: 하한 1390 / 실제 1609
# → **219건이 조용히 사라져도 G4는 초록**이었다. 어셈블리 하나가 컴파일에서 빠지면 딱 그 형태로 사라진다.
prev_best() {   # $1=mode(edit|play)  $2=이번 실행이 쓸 xml(제외)  → 최대 total 과 그 파일명
  python3 - "$OUTDIR" "$1" "${2:-}" <<'PY'
import sys, os, glob
import xml.etree.ElementTree as ET
outdir, mode, skip = sys.argv[1], sys.argv[2], sys.argv[3]
best, who = 0, ''
for p in glob.glob(os.path.join(outdir, f'*_{mode}.xml')):
    if skip and os.path.abspath(p) == os.path.abspath(skip):
        continue
    try:
        r = ET.parse(p).getroot()
    except Exception:
        continue
    tot = int(r.get('total') or 0)
    if tot > best:
        best, who = tot, os.path.basename(p)
print(f"{best} {who}")
PY
}

# ---- G8 컴파일 실패 감지 -----------------------------------------------------
# ★★ 2026-09-03 — 이 가드가 **죽은 프로브**였다. 실측으로 잡았다.
#   원래 마커는 "Aborting batchmode due to failure"였는데 그 문장은 Unity의 **stdout에만** 나오고
#   `-logFile`이 받는 로그 파일에는 **한 번도 안 찍힌다**. 실제 컴파일 실패 로그
#   (qa-r7_edit.log, 12×CS0103)에서 그 마커의 등장 횟수는 **0**이었다.
#   ⇒ G8은 "컴파일 실패를 못 봤다"와 "컴파일이 성공했다"를 구분하지 못한 채 통과시키고 있었다.
#     이번엔 G3(결과 파일 부재)가 대신 물어서 사고가 안 났지만, G3는 다른 것을 재는 가드다.
#     둘 중 하나라도 순서가 바뀌면 그대로 거짓 통과다.
#
#   지금 마커 3종은 **실측으로 교정**했다:
#     · 실패 로그 qa-r7_edit.log      → "Scripts have compiler errors"=1, "## Script Compilation Error for"=1
#     · 정상 로그 5종(te-r2_edit/play, qa-r6_edit, coder-grabline_edit, qa-r5_play) → 3종 전부 0
#   (옛 마커는 남겨 둔다 — Unity 버전에 따라 로그로 갈 수 있다. 늘리는 것은 안전하다.)
COMPILE_FAIL_MARKERS=(
  "Scripts have compiler errors"
  "## Script Compilation Error for"
  "Aborting batchmode due to failure"
)
log_compile_failed() {   # $1=로그 경로 → 0=컴파일 실패다 / 1=아니다
  local log="$1" m
  [ -f "$log" ] || return 1
  for m in "${COMPILE_FAIL_MARKERS[@]}"; do
    grep -qF "$m" "$log" 2>/dev/null && return 0
  done
  return 1
}

# ---- G4a 정적 하한 노후 감지 -------------------------------------------------
# 하한은 사람이 손으로 올린다 = 반드시 낡는다. 낡은 하한은 **아무 소리도 내지 않는다** —
# 이 저장소 거짓 통과의 표준형("실패한 측정과 성공한 측정이 똑같이 생겼다")이다.
# 그래서 하한 자신에게 자기 노후를 신고시킨다. 판정은 하지 않고(정당한 삭제를 막지 않는다)
# **반드시 화면에 뜨게** 한다.
stale_floor_report() {   # $1=현재 하한 $2=직전 최대 $3=그 파일 $4=mode
  local minc="$1" prevn="$2" prevwho="$3" mode="$4"
  [ "${prevn:-0}" -gt 0 ] || { echo "G4a: 직전 실행이 없어 하한 노후를 판정할 수 없다(미확인)."; return 0; }
  local thresh=$(( prevn * STALE_FLOOR_RATIO_PCT / 100 ))
  if [ "$minc" -lt "$thresh" ]; then
    echo "⚠⚠ G4a: 정적 하한이 낡았다 — ${mode} 하한=${minc} / 직전 최대=${prevn}(${prevwho})."
    echo "     지금 하한으로는 **$(( prevn - minc ))건이 조용히 사라져도 G4가 초록**이다."
    echo "     regress.sh의 MIN_$(echo "$mode" | tr a-z A-Z)_CASES를 ${thresh} 이상으로 올려라."
  else
    echo "G4a: 하한 ${minc} / 직전 최대 ${prevn} — 노후 아님(기준 ${thresh})."
  fi
  return 0
}

# ---- G10 세이브 격리 게이트 회계 --------------------------------------------
# 읽는 것: Unity 로그의 정규형 한 줄
#   [세이브게이트] 회계 leaf=<n> purge=<n> skip=<n> removed=<n> fail=<n>
# 그 줄을 내는 곳: Assets/_Project/Scripts/Tests/PlayMode/PlayModeSaveIsolationGate.cs 의 RunFinished.
#
# ★ 기준과 대상이 같은 코드에서 나오지 않는다(TEAM.md "거짓 통과 신형"):
#   게이트 회계는 **우리 C# 콜백**이 세고, total/testcasecount는 **NUnit 러너**가 센다.
#   둘을 대조하는 이 함수는 어느 쪽 코드도 공유하지 않는다.
gate_audit() {   # $1=로그  $2=결과 xml  $3=mode(edit|play)  → 0=통과 / 그 외=위반
  local log="$1" xml="$2" mode="$3"
  python3 - "$log" "$xml" "$mode" <<'PY'
import os, re, sys
import xml.etree.ElementTree as ET

log, xml, mode = sys.argv[1], sys.argv[2], sys.argv[3]
bad = []

if not os.path.isfile(log):
    print(f"✗ G10: 로그 파일이 없다 — {log}. 게이트 회계를 잴 방법이 없다(측정 무효).")
    sys.exit(1)
if not os.path.isfile(xml):
    print(f"✗ G10: 결과 xml이 없다 — {xml}.")
    sys.exit(1)

text = open(log, encoding='utf-8', errors='replace').read()

# 느슨한 니들(줄이 있기는 한가) / 엄격한 정규형(값이 채워졌는가) — 둘을 나눠 센다.
loose  = re.findall(r'\[세이브게이트\] 회계.*', text)
strict = re.findall(
    r'\[세이브게이트\] 회계 leaf=(\d+) purge=(\d+) skip=(\d+) removed=(\d+) fail=(\d+)\s*$',
    text, re.MULTILINE)

if not strict:
    if loose:
        print(f"✗ G10: 정규형이 깨졌다 — '회계' 줄은 {len(loose)}개 있는데 "
              f"`leaf=<정수> purge=<정수> skip=<정수> removed=<정수> fail=<정수>` 형태가 0개다.")
        print(f"       첫 줄: {loose[0][:200]}")
        print("       값이 비어도 줄 수는 맞는다 — 형태만 세면 통과한다(TEAM.md 거짓 통과 13번째). "
              "PlayModeSaveIsolationGate.RunFinished와 이 정규식을 같은 커밋에서 맞춰라.")
    else:
        print("✗ G10: 세이브 격리 게이트의 회계 줄이 로그에 **한 건도 없다**.")
        print("       (가) 어셈블리 콜백이 더는 안 붙는다(Unity Test Framework 변경), 또는")
        print("       (나) PlayModeSaveIsolationGate.RunFinished의 로그 줄이 지워졌다.")
        print("       어느 쪽이든 **게이트가 살아 있다는 근거가 이번 실행에 하나도 없다**.")
    sys.exit(1)

if len(strict) != 1:
    print(f"✗ G10: 회계 줄이 {len(strict)}개다 — 한 로그에 여러 실행이 섞였다.")
    print("       리프 수가 어느 실행의 것인지 특정할 수 없으므로 이 측정은 무효다.")
    sys.exit(1)

leaf, purge, skip, removed, fail = (int(x) for x in strict[0])

r = ET.parse(xml).getroot()
tcc = int(r.get('testcasecount') or 0)
tot = int(r.get('total') or 0)

print(f"G10 게이트 회계: leaf={leaf} purge={purge} skip={skip} removed={removed} fail={fail}")
print(f"    러너 xml   : testcasecount={tcc} total={tot}   (mode={mode})")

# (가) 게이트 자신의 회계 항등식
if leaf != purge + skip:
    bad.append(f"회계 항등식이 깨졌다 — leaf({leaf}) != purge({purge}) + skip({skip}). "
               "게이트가 어떤 경로로 조용히 빠져나가고 있다.")

# (나) 삼킨 예외
if fail != 0:
    bad.append(f"게이트가 정리 중 예외를 {fail}건 삼켰다. 그 리프들은 앞 테스트의 저장 파일을 "
               "물려받은 채 돌았다(로그에서 '[세이브게이트] ★ 정리 실패'를 찾아라).")

# (다) 플랫폼별 설계 기대
if mode == 'play':
    if skip != 0:
        bad.append(f"PlayMode 실행인데 게이트가 {skip}회 건너뛰었다 — 그 리프는 격리되지 않았다.")
    if purge != leaf:
        bad.append(f"PlayMode 실행인데 정리 시도({purge})가 리프({leaf})와 다르다.")
elif mode == 'edit':
    # EditMode는 **일부러 전량 건너뛴다**(씬을 안 띄우므로 축적 경로가 없다).
    # 그 설계가 실제로 도는지를 여기서 못박는다 — 주석이 아니라 숫자로.
    if purge != 0:
        bad.append(f"EditMode 실행인데 게이트가 {purge}회 실제로 지웠다. 설계상 EditMode는 "
                   "전량 건너뛴다(PlayModeSaveIsolationGate 클래스 문서). 사정거리가 넓어졌다.")
    if skip != leaf:
        bad.append(f"EditMode 실행인데 건너뜀({skip})이 리프({leaf})와 다르다.")
else:
    bad.append(f"알 수 없는 mode '{mode}' — edit/play만 판정할 수 있다.")

# (라) ★ 독립 대조 — 다른 자로 잰 같은 실행
if tot != leaf:
    bad.append(f"★ 게이트가 센 리프({leaf})와 러너 xml의 total({tot})이 다르다. "
               "게이트 콜백이 일부 리프에 안 붙었거나, 실행이 도중에 갈라졌다. "
               "둘은 같은 실행을 서로 다른 자로 센 값이므로 반드시 같아야 한다.")
if tcc != leaf:
    bad.append(f"★ 게이트가 센 리프({leaf})와 러너 xml의 testcasecount({tcc})가 다르다.")

for b in bad:
    print("\n✗ G10: " + b)
if not bad:
    print("G10: 통과 — 게이트 회계가 항등식과 러너 xml 양쪽에 모두 맞는다(실행 끝까지 살아 있었다).")
sys.exit(1 if bad else 0)
PY
}

# ---- 결과 판독 -------------------------------------------------------------
report() {   # $1=xml  $2=기대 최소 건수(선택)  $3=실행 시작 epoch(선택)  $4=직전 최대(선택) $5=그 파일(선택)
  local xml="$1" minc="${2:-0}" started="${3:-0}" prevn="${4:-0}" prevwho="${5:-}"
  [ -f "$xml" ] || die "G3: 결과 파일이 없다 — $xml. 테스트가 한 건도 돌지 않았다."
  local mt; mt=$(stat -f %m "$xml")
  if [ "$started" -gt 0 ] && [ "$mt" -lt "$started" ]; then
    die "G3: 결과 파일이 실행 시작($(date -r "$started" '+%H:%M:%S'))보다 오래됐다($(date -r "$mt" '+%H:%M:%S')) — 낡은 파일을 읽고 있다."
  fi
  python3 - "$xml" "$minc" "$prevn" "$prevwho" <<'PY'
import sys, os, datetime
import xml.etree.ElementTree as ET
xml, minc = sys.argv[1], int(sys.argv[2])
prevn, prevwho = int(sys.argv[3]), sys.argv[4]
r = ET.parse(xml).getroot()
tcc = int(r.get('testcasecount') or 0)
tot = int(r.get('total') or 0)
fa  = int(r.get('failed') or 0)
sk  = int(r.get('skipped') or 0)
inc = int(r.get('inconclusive') or 0)
pa  = int(r.get('passed') or 0)
mt  = datetime.datetime.fromtimestamp(os.stat(xml).st_mtime).strftime('%m-%d %H:%M:%S')
print(f"결과파일 {xml}  (mtime {mt})")
print(f"  testcasecount={tcc} total={tot} passed={pa} failed={fa} skipped={sk} inconclusive={inc}")
bad = []
if tcc != tot:
    bad.append(f"G5: testcasecount({tcc}) != total({tot}) — 부분 실행이다. '전량'이라고 말할 수 없다.")
if minc and tot < minc:
    bad.append(f"G4: {tot}건만 돌았다(하한 {minc}) — 필터/컴파일 실패로 대부분이 실행되지 않았다.")
if prevn and tot < prevn:
    bad.append(f"G9: 직전 최대 {prevn}건({prevwho})보다 {prevn - tot}건 줄었다. "
               "어셈블리 하나가 컴파일에서 빠지면 정확히 이 형태로 사라진다 — "
               "테스트를 실제로 지운 라운드가 있으면 그 사실을 보고에 적고 넘어가라.")
fails = [tc for tc in r.iter('test-case') if tc.get('result') == 'Failed']
skips = [tc for tc in r.iter('test-case') if tc.get('result') == 'Skipped']
if fails:
    print(f"\n  ── 실패 {len(fails)}건 ──")
    for tc in fails:
        m = tc.find('./failure/message')
        msg = ' '.join((m.text or '').split())[:220] if m is not None else ''
        print(f"   ✗ {tc.get('fullname')}\n       {msg}")
if skips:
    print(f"\n  ── 건너뜀 {len(skips)}건 ──")
    for tc in skips:
        print(f"   · {tc.get('fullname')}")
for b in bad:
    print("\n✗ " + b)
sys.exit(1 if bad else 0)
PY
}

# ---- 베이스라인 대조(귀속용) -------------------------------------------------
# ★ 이 역할의 존재 이유는 "누구 라운드가 무엇을 깼는지"를 그 라운드에 알리는 것이다.
#   전량 결과 두 개를 놓고 **새로 빨개진 것 / 초록으로 돌아온 것 / 사라진 것 / 새로 생긴 것**을 가른다.
compare() {   # $1=옛 xml  $2=새 xml
  [ -f "$1" ] || die "compare: 옛 결과 파일이 없다 — $1"
  [ -f "$2" ] || die "compare: 새 결과 파일이 없다 — $2"
  python3 - "$1" "$2" <<'PY'
import sys, os, datetime
import xml.etree.ElementTree as ET

# ★ 개명 흡수(2026-09-03 신설). 등록·검증된 개명만 «같은 테스트»로 본다.
#   등록되지 않은 이름 변경은 여전히 «삭제 1 + 신설 1»로 뜬다 — 그게 맞다.
sys.path.insert(0, os.path.join('/Users/kjmoon/App/StickMate', 'docs/verify'))
try:
    import renames as _rn
    canon_full, _canon_short, _ok, _rej = _rn.load()
    _rn.banner(_ok, _rej)
except Exception as e:                      # 대장이 깨져도 대조 자체는 돌아야 한다
    print(f"\n⚠ 개명 대장을 읽지 못했다({e}) — 개명이 삭제+신설로 보일 것이다.")
    canon_full = lambda n: n
    _ok, _rej = [], []

def load(p):
    r = ET.parse(p).getroot()
    d = {}
    for tc in r.iter('test-case'):
        fn = tc.get('fullname')
        if fn: d[canon_full(fn)] = tc.get('result')
    mt = datetime.datetime.fromtimestamp(os.stat(p).st_mtime).strftime('%m-%d %H:%M')
    return d, mt, int(r.get('total') or 0)
a, mta, ta = load(sys.argv[1])
b, mtb, tb = load(sys.argv[2])
print(f"옛: {os.path.basename(sys.argv[1])} ({mta})  {ta}건")
print(f"새: {os.path.basename(sys.argv[2])} ({mtb})  {tb}건   Δ{tb - ta:+d}")
newred  = sorted(n for n in b if b[n] == 'Failed' and a.get(n) not in (None, 'Failed'))
fixed   = sorted(n for n in a if a[n] == 'Failed' and b.get(n) == 'Passed')
stayred = sorted(n for n in b if b[n] == 'Failed' and a.get(n) == 'Failed')
gone    = sorted(n for n in a if n not in b)
added   = sorted(n for n in b if n not in a)
addred  = [n for n in added if b[n] == 'Failed']
def show(title, items, limit=40):
    print(f"\n── {title} {len(items)}건 ──")
    for n in items[:limit]: print(f"   {n}")
    if len(items) > limit: print(f"   … 외 {len(items)-limit}건")
# ★ 2026-09-03 신설 — «초록 → 건너뜀»은 지금까지 이 대조에서 **보이지 않았다.**
#   CLAUDE.md가 경고한 조건부 Ignore(`ItemRarityDerivationTests.감사_코호트_안_등급_혼재를_잡는다`)가
#   조건이 참이 되는 날 조용히 건너뜀으로 바뀌는데, 실패 목록에도 신설 목록에도 안 뜬다.
#   **커버리지가 사라진 것인데 러너 요약은 「실패 0」이라 아무도 모른다.**
green2skip = sorted(n for n in b if b[n] == 'Skipped' and a.get(n) == 'Passed')
skip2green = sorted(n for n in b if b[n] == 'Passed' and a.get(n) == 'Skipped')

show("★ 새로 빨개짐(이번 라운드가 깼다)", newred)
show("★★ 초록 → 건너뜀 (조용한 커버리지 상실 — 실패 0에 가려진다)", green2skip)
show("건너뜀 → 초록 (되살아난 검사)", skip2green)
show("초록으로 돌아옴", fixed)
show("계속 빨감(이전부터)", stayred)
show("새로 생긴 테스트 중 빨감(신규 결함)", addred)

# ★ 2026-09-03 신설 — 「사라졌다」의 대부분은 사라진 게 아니다.
#   실측: PackPaletteGateTests의 [TestCase] **설명 문자열**이 한 글자 바뀌자
#   («요정날개·날개·종이비행기» → «요정날개·날개») fullname이 달라져 삭제 1 + 신설 1로 떴다.
#   이걸 조용히 흡수하면 진짜 삭제를 놓친다. 그래서 **흡수하지 않고 짝을 지어 이름을 붙인다.**
#     · 같은 메서드 + 같은 선두 인자  → 인자 설명만 바뀜(회귀 아님)
#     · 같은 클래스에서 1:1          → 개명 후보(renames.tsv에 등록하면 다음부터 흡수된다)
def head_of(n):
    return n.split('(', 1)[0]
def firstarg(n):
    if '(' not in n: return None
    inner = n[n.index('(') + 1:]
    return inner.split(',', 1)[0].strip()
pairs_arg, pairs_rename, gone_left, added_left = [], [], list(gone), list(added)
for g in list(gone_left):
    for a in list(added_left):
        if '(' in g and '(' in a and head_of(g) == head_of(a) and firstarg(g) == firstarg(a):
            pairs_arg.append((g, a)); gone_left.remove(g); added_left.remove(a); break
for g in list(gone_left):
    gcls = head_of(g).rsplit('.', 1)[0]
    cands = [a for a in added_left if head_of(a).rsplit('.', 1)[0] == gcls]
    if len(cands) == 1:
        pairs_rename.append((g, cands[0])); gone_left.remove(g); added_left.remove(cands[0])

show("사라진 테스트(지워졌거나 컴파일 안 됨)", gone)
if pairs_arg:
    print(f"\n── 그중 ⟨인자 설명만 바뀜⟩ {len(pairs_arg)}건 — 회귀 아님 ──")
    for g, a in pairs_arg[:20]:
        print(f"   {g}\n → {a}")
if pairs_rename:
    print(f"\n── 그중 ★⟨개명 후보⟩ {len(pairs_rename)}건 — 같은 클래스 1:1. "
          f"진짜 개명이면 docs/verify/renames.tsv에 등록해라 ──")
    for g, a in pairs_rename[:20]:
        print(f"   {g}\n → {a}")
if gone_left:
    print(f"\n── ★★ 짝이 없는 진짜 소멸 {len(gone_left)}건 — 여기만 보면 된다 ──")
    for n in gone_left[:40]:
        print(f"   {n}")

print(f"\n새로 생긴 테스트 총 {len(added)}건 / 사라진 테스트 총 {len(gone)}건")
print(f"  사라진 {len(gone)}건 내역: 인자설명변경 {len(pairs_arg)} / 개명후보 {len(pairs_rename)} "
      f"/ **짝없는 소멸 {len(gone_left)}**   (등록된 개명 {len(_ok)}건은 애초에 여기 세지 않는다)")
PY
}

# ---- 자기검사: 가드가 실제로 무는가 -----------------------------------------
selfcheck() {
  local tmp; tmp=$(mktemp -d)
  local rc=0
  echo "── 음성 대조 1: 부분 실행 xml(tcc != total)을 report가 거부하는가"
  cat > "$tmp/partial.xml" <<'X'
<test-run id="2" testcasecount="529" total="106" passed="103" failed="0" skipped="3" inconclusive="0"></test-run>
X
  if ( report "$tmp/partial.xml" 0 0 ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — G5가 물지 않는다."; rc=1
  else echo "  ✓ 거부했다(G5)"; fi

  # ★ 하한 숫자를 **여기에 다시 베끼지 않는다**(CLAUDE.md: 테스트에 프로덕션 상수를 베끼지 마라).
  #   실제 상수를 참조한다 — 안 그러면 하한을 올릴 때 이 대조만 낡는다.
  echo "── 음성 대조 2: 건수 미달 xml을 report가 거부하는가(하한 $MIN_EDIT_CASES 참조)"
  cat > "$tmp/tiny.xml" <<'X'
<test-run id="2" testcasecount="3" total="3" passed="3" failed="0" skipped="0" inconclusive="0"></test-run>
X
  if ( report "$tmp/tiny.xml" "$MIN_EDIT_CASES" 0 ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — G4가 물지 않는다."; rc=1
  else echo "  ✓ 거부했다(G4)"; fi

  echo "── 음성 대조 3: 낡은 파일(실행 시작보다 오래됨)을 거부하는가"
  cat > "$tmp/old.xml" <<'X'
<test-run id="2" testcasecount="1400" total="1400" passed="1400" failed="0" skipped="0" inconclusive="0"></test-run>
X
  local future=$(( $(date +%s) + 3600 ))
  if ( report "$tmp/old.xml" 0 "$future" ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — G3가 물지 않는다."; rc=1
  else echo "  ✓ 거부했다(G3)"; fi

  echo "── 음성 대조 4: 결과 파일이 아예 없으면 거부하는가"
  if ( report "$tmp/does-not-exist.xml" 0 0 ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 없는 파일을 초록으로 읽었다."; rc=1
  else echo "  ✓ 거부했다(G3/부재)"; fi

  echo "── 음성 대조 5: 다른 Unity가 돌고 있으면 실행을 거부하는가(G6)"
  if ( assert_no_unity_running ) >/dev/null 2>&1; then
    echo "  · 지금은 도는 Unity가 없어 이 대조는 판정 불가(미확인). 배치모드가 돌 때 다시 확인할 것."
  else
    echo "  ✓ 거부했다(G6) — 실제로 도는 Unity를 봤다."
  fi
  echo "── 음성 대조 5b: ★ G6 탐침이 «자기 자신»을 잡지 않는가(옛 pgrep -f 가 이걸로 죽었다)"
  # 자기 명령줄에 그 문자열을 넣고도 잡히면 안 된다.
  if ( UNITY_BIN_PATH="/bin/echo" assert_no_unity_running ) >/dev/null 2>&1; then
    echo "  · /bin/echo 로 바꿔도 -batchmode 인자를 가진 것이 없다 — 자기참조 없음(정상)"
  else
    echo "  ✗ 실행파일을 /bin/echo 로 바꿨는데 선점으로 판정했다 — 탐침이 엉뚱한 것을 센다."; rc=1
  fi
  echo "── 양성 대조 5c: ★ G6 탐침이 실제로 프로세스를 «셀 수» 있는가(0건이 죽은 프로브가 아님)"
  # 지금 확실히 도는 실행파일(로그인 셸)로 바꿔 세어 본다. args 조건을 -batchmode 대신 흔한 문자열로 둘 수 없으므로
  # 여기서는 후보 수집 단계만 검증한다.
  local shellhits
  shellhits=$(ps -axo pid=,comm= | awk '$0 ~ /\/bin\/(ba|z)sh/ {print $1}' | wc -l | tr -d ' ')
  if [ "$shellhits" -gt 0 ]; then
    echo "  ✓ comm 기반 수집이 셸 ${shellhits}개를 센다 — 이 탐침의 0건은 '비었다'는 뜻이다"
  else
    echo "  ✗ comm 기반 수집이 아무것도 못 센다 — G6의 0건은 '프로브가 죽었다'와 구분되지 않는다."; rc=1
  fi

  # ★ 2026-09-02 신설 — 여기부터가 이번에 뚫려 있던 자리다.
  echo "── 음성 대조 6: -quit이 인자에 있으면 거부하는가(G1이 주석이 아니라 코드인가)"
  if ( assert_launch_args -batchmode -runTests -quit ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — G1은 여전히 주석일 뿐이다."; rc=1
  else echo "  ✓ 거부했다(G1)"; fi

  echo "── 음성 대조 7: -testFilter 값에 콤마가 있으면 거부하는가(G2)"
  if ( assert_launch_args -batchmode -runTests -testFilter "A,B" ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — G2는 여전히 주석일 뿐이다."; rc=1
  else echo "  ✓ 거부했다(G2)"; fi

  echo "── 양성 대조 A: 정상 인자는 통과하는가"
  if ( assert_launch_args -batchmode -nographics -runTests -testPlatform EditMode ) >/dev/null 2>&1; then
    echo "  ✓ 통과했다(G1/G2 양성 대조)"
  else echo "  ✗ 정상 인자를 거부했다 — G1/G2가 과잉이다."; rc=1; fi

  # ★★ 이 두 대조는 "UNKNOWN이면 통과"로 짜면 **거짓 통과한다**. 실제로 그렇게 짰다가 잡혔다:
  #    시험 대상(OSX 마커)과 무관하게 WIN 마커가 먼저 걸려 UNKNOWN이 나왔는데도 ✓가 찍혔다.
  #    그래서 **사유 문자열이 시험 중인 마커 이름과 기대 가드 번호를 담고 있는지**까지 본다.
  echo "── 음성 대조 8: ★ 죽은 마커를 쓰면 UNKNOWN이 나오는가(G7a — 이번에 실제로 뚫려 있던 구멍)"
  local saved="$MARKER_OSX"
  MARKER_OSX=MacOverlayWindow   # 실재하지 않는 옛 마커. 예전 구현은 이걸로 조용히 초록이었다.
  local t; t=$(active_target)
  MARKER_OSX="$saved"
  case "$t" in
    *"G7a"*"MacOverlayWindow"*) echo "  ✓ 거부했다(G7a, 사유가 그 마커를 지목한다) — [$t]" ;;
    UNKNOWN*) echo "  ✗ UNKNOWN이지만 사유가 MacOverlayWindow가 아니다 — 다른 이유로 빨개진 것이다: [$t]"; rc=1 ;;
    *) echo "  ✗ 죽은 마커인데 [$t]라고 단정했다 — G7a가 물지 않는다."; rc=1 ;;
  esac

  echo "── 음성 대조 9: 게이트 밖 문자열에 오염된 마커를 쓰면 UNKNOWN이 나오는가(G7b)"
  saved="$MARKER_OSX"
  MARKER_OSX=MacWindowService   # Core/StickConfig.cs(게이트 없음)의 [Tooltip] 문자열에 이름이 있다.
  t=$(active_target)
  MARKER_OSX="$saved"
  case "$t" in
    *"G7b"*"MacWindowService"*) echo "  ✓ 거부했다(G7b, 사유가 그 마커를 지목한다) — [$t]" ;;
    UNKNOWN*) echo "  ✗ UNKNOWN이지만 사유가 MacWindowService가 아니다 — 다른 이유로 빨개진 것이다: [$t]"; rc=1 ;;
    *) echo "  ✗ 오염된 마커인데 [$t]라고 단정했다 — G7b가 물지 않는다."; rc=1 ;;
  esac

  echo "── 양성 대조 B: 지금 마커로는 실제 타깃이 나오는가(위 두 대조가 '항상 UNKNOWN'이 아님을 증명)"
  t=$(active_target)
  case "$t" in
    UNITY_STANDALONE_*) echo "  ✓ 판정됐다 — [$t]" ;;
    *) echo "  ✗ [$t] — 지금 마커로도 판정이 안 된다. G7 전체가 무의미하다."; rc=1 ;;
  esac

  echo "── 음성 대조 10: G9(직전 실행 대비 감소)가 무는가"
  cat > "$tmp/shrunk.xml" <<'X'
<test-run id="2" testcasecount="1400" total="1400" passed="1400" failed="0" skipped="0" inconclusive="0"></test-run>
X
  if ( report "$tmp/shrunk.xml" 0 0 1609 "b2-bake_edit.xml" ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 209건이 사라졌는데 G9가 물지 않는다."; rc=1
  else echo "  ✓ 거부했다(G9)"; fi

  echo "── 음성 대조 11: ★ G4a가 낡은 하한을 신고하는가(2026-09-03 신설 — 실제로 하루 낡아 있었다)"
  local g4a
  g4a=$(stale_floor_report 1390 1800 "가상_직전.xml" edit)
  case "$g4a" in
    *"G4a: 정적 하한이 낡았다"*) echo "  ✓ 신고했다 — $(echo "$g4a" | head -1)" ;;
    *) echo "  ✗ 하한 1390 / 직전 1800인데 신고하지 않았다: [$g4a]"; rc=1 ;;
  esac
  echo "── 양성 대조 D: G4a가 «항상 신고»가 아닌가(지금 하한으로는 조용해야 한다)"
  g4a=$(stale_floor_report "$MIN_EDIT_CASES" 1800 "가상_직전.xml" edit)
  case "$g4a" in
    *"노후 아님"*) echo "  ✓ 조용하다 — $g4a" ;;
    *) echo "  ✗ 지금 하한($MIN_EDIT_CASES)도 노후로 신고한다 — 하한을 올려라: [$g4a]"; rc=1 ;;
  esac

  echo "── 음성 대조 12: ★ G8이 실제 컴파일 실패 로그를 잡는가(2026-09-03: 옛 마커는 죽어 있었다)"
  cat > "$tmp/fail.log" <<'X'
Assets/_Project/Scripts/States/ThrowTumbleState.cs(247,27): error CS0103: The name 'ResolveThrowStrength01' does not exist in the current context
## Script Compilation Error for: Csc Library/Bee/artifacts/200b0aE.dag/StickMate.Runtime.dll (+2 others)
AssetDatabase: script compilation time: 2.129521s
Scripts have compiler errors.
X
  if log_compile_failed "$tmp/fail.log"; then echo "  ✓ 잡았다(G8)"
  else echo "  ✗ 실제 실패 로그의 문장을 그대로 넣었는데 못 잡았다 — G8이 죽은 프로브다."; rc=1; fi

  echo "── 양성 대조 F: G8이 «항상 실패»가 아닌가 — 정상 로그를 실패로 오인하지 않는가"
  local goodlog cleanhits=0 goodn=0
  for goodlog in "$OUTDIR"/te-r2_edit.log "$OUTDIR"/qa-r6_edit.log "$OUTDIR"/te-r2_play.log; do
    [ -f "$goodlog" ] || continue
    goodn=$((goodn+1))
    log_compile_failed "$goodlog" || cleanhits=$((cleanhits+1))
  done
  if [ "$goodn" -eq 0 ]; then
    echo "  · 대조할 과거 정상 로그가 디스크에 없다 — 판정 불가(미확인)."
  elif [ "$cleanhits" -eq "$goodn" ]; then
    echo "  ✓ 과거 정상 로그 ${goodn}건 전부 '실패 아님'으로 읽었다"
  else
    echo "  ✗ 정상 로그 $((goodn - cleanhits))건을 컴파일 실패로 오인했다 — 마커가 과잉이다."; rc=1
  fi

  echo "── 음성 대조 13: 존재하지 않는 로그는 '실패'로 단정하지 않는가"
  if log_compile_failed "$tmp/no-such.log"; then
    echo "  ✗ 없는 파일을 컴파일 실패로 읽었다."; rc=1
  else echo "  ✓ 단정하지 않는다"; fi

  echo "── 양성 대조 E: 개명 대장 검증기가 스스로 초록인가"
  if python3 "$REPO/docs/verify/renames.py" --check >/dev/null 2>&1; then
    echo "  ✓ renames.py --check 통과"
  else
    echo "  ✗ renames.py --check 실패 — 개명 흡수가 검증되지 않은 채 돌고 있다."; rc=1
  fi

  # ==========================================================================
  # ★★ G10 대조 (2026-09-03 신설) — 세이브 격리 게이트 회계
  # ==========================================================================
  # 이 가드는 **"게이트가 실행 끝까지 살아 있었는가"**를 유일하게 재는 자리다.
  # 그래서 대조를 넉넉히 붙인다 — 여기가 조용히 죽으면 아무도 모른다.
  gate_probe_xml() {   # $1=경로 $2=건수  (tcc=total=건수)
    cat > "$1" <<X
<test-run id="2" testcasecount="$2" total="$2" passed="$2" failed="0" skipped="0" inconclusive="0"></test-run>
X
  }
  gate_probe_log() {   # $1=경로 $2=leaf $3=purge $4=skip $5=removed $6=fail
    printf '%s\n' \
      "[테스트격리] 아무 줄" \
      "[세이브게이트] 실행 종료 — 리프 테스트 $2건, 정리 시도 $3회, 삭제 $5개, 건너뜀 $4회(마지막 사유: 없음), 실패 $6건." \
      "[세이브게이트] 회계 leaf=$2 purge=$3 skip=$4 removed=$5 fail=$6" > "$1"
  }

  gate_probe_xml "$tmp/g10.xml" 591

  echo "── 양성 대조 G: G10이 **정상 PlayMode 쌍**을 통과시키는가(이게 빨강이면 아래 음성 대조는 전부 무의미)"
  gate_probe_log "$tmp/g10_ok.log" 591 591 0 77 0
  if gate_audit "$tmp/g10_ok.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✓ 통과했다"
  else echo "  ✗ 정상 쌍을 거부했다 — G10이 과잉이다."; gate_audit "$tmp/g10_ok.log" "$tmp/g10.xml" play; rc=1; fi

  echo "── 양성 대조 H: G10이 **정상 EditMode 쌍**(전량 건너뜀)을 통과시키는가"
  gate_probe_xml "$tmp/g10e.xml" 1800
  gate_probe_log "$tmp/g10e_ok.log" 1800 0 1800 0 0
  if gate_audit "$tmp/g10e_ok.log" "$tmp/g10e.xml" edit >/dev/null 2>&1; then
    echo "  ✓ 통과했다"
  else echo "  ✗ 정상 EditMode 쌍을 거부했다."; gate_audit "$tmp/g10e_ok.log" "$tmp/g10e.xml" edit; rc=1; fi

  echo "── 음성 대조 14: 회계 항등식이 깨지면(leaf != purge+skip) 거부하는가"
  gate_probe_log "$tmp/g10_id.log" 591 500 0 77 0
  if gate_audit "$tmp/g10_id.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 항등식 가드가 없다."; rc=1
  else echo "  ✓ 거부했다(항등식)"; fi

  echo "── 음성 대조 15: ★ 게이트가 도중에 죽은 형태(leaf 591인데 정리 500회 + 건너뜀 91회)를 거부하는가"
  # 이것이 **자기 반려 1번이 말한 바로 그 실패**다 — 항등식은 맞지만 play에서 skip>0이면
  # 그 91개 리프는 앞 테스트의 저장 파일을 물려받은 채 돌았다.
  gate_probe_log "$tmp/g10_die.log" 591 500 91 77 0
  if gate_audit "$tmp/g10_die.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — **게이트가 500번째 리프에서 죽어도 초록**이던 그 상태 그대로다."; rc=1
  else echo "  ✓ 거부했다(play는 skip==0)"; fi

  echo "── 음성 대조 16: 삼킨 예외(fail>0)를 거부하는가"
  gate_probe_log "$tmp/g10_fail.log" 591 591 0 77 3
  if gate_audit "$tmp/g10_fail.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 예외 3건을 삼켰는데 초록이다."; rc=1
  else echo "  ✓ 거부했다(fail==0)"; fi

  echo "── 음성 대조 17: ★ 러너 xml과 어긋나면(leaf != total) 거부하는가 — **이 대조가 G10의 값어치다**"
  gate_probe_log "$tmp/g10_drift.log" 424 424 0 40 0   # 424 = 게이트 테스트가 도는 시점의 실측 중간값
  if gate_audit "$tmp/g10_drift.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 게이트 424 vs 러너 591인데 초록이다(독립 대조가 죽었다)."; rc=1
  else echo "  ✓ 거부했다(leaf != total/testcasecount)"; fi

  echo "── 음성 대조 18: 회계 줄이 아예 없으면 거부하는가(콜백이 안 붙은 형태)"
  printf '%s\n' "[테스트격리] 아무 줄" "다른 로그" > "$tmp/g10_none.log"
  if gate_audit "$tmp/g10_none.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 줄이 없는데 초록이다(0건을 '깨끗'으로 읽었다)."; rc=1
  else echo "  ✓ 거부했다(줄 부재)"; fi

  echo "── 음성 대조 19: ★ 값이 빈 정규형(leaf= purge= …)을 거부하는가 — TEAM.md 거짓 통과 13번째"
  printf '%s\n' "[세이브게이트] 회계 leaf= purge= skip= removed= fail=" > "$tmp/g10_empty.log"
  if gate_audit "$tmp/g10_empty.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — **형태만 맞고 값이 비었는데** 초록이다."; rc=1
  else echo "  ✓ 거부했다(정규형/값 채움)"; fi

  echo "── 음성 대조 20: EditMode인데 게이트가 실제로 지웠으면(purge>0) 거부하는가"
  gate_probe_log "$tmp/g10e_bad.log" 1800 5 1795 5 0
  if gate_audit "$tmp/g10e_bad.log" "$tmp/g10e.xml" edit >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — EditMode 전량 건너뜀 설계가 지켜지지 않는데 초록이다."; rc=1
  else echo "  ✓ 거부했다(edit는 purge==0)"; fi

  echo "── 음성 대조 21: 로그가 없으면 '통과'로 읽지 않는가"
  if gate_audit "$tmp/no-such-gate.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 없는 로그를 초록으로 읽었다."; rc=1
  else echo "  ✓ 거부했다(로그 부재)"; fi

  # ★ 실측 교정 — 합성만으로는 "실제 러너가 뱉는 줄"을 읽는지 알 수 없다.
  #   정규형 줄이 들어 있는 **진짜 실행 기록**이 디스크에 있으면 그것으로 교정한다.
  #   없으면 **미확인**이라고 쓴다("통과"라고 쓰지 않는다).
  echo "── 실측 교정: 정규형 줄이 들어 있는 실제 실행 기록으로 G10을 교정한다"
  local realn=0 realok=0 rl rx rmode
  for rl in "$OUTDIR"/*_play.log "$OUTDIR"/*_edit.log; do
    [ -f "$rl" ] || continue
    grep -qE '\[세이브게이트\] 회계 leaf=[0-9]+ purge=[0-9]+ skip=[0-9]+ removed=[0-9]+ fail=[0-9]+' "$rl" || continue
    rx="${rl%.log}.xml"; [ -f "$rx" ] || continue
    case "$rl" in *_play.log) rmode=play ;; *) rmode=edit ;; esac
    realn=$((realn+1))
    if gate_audit "$rl" "$rx" "$rmode" >/dev/null 2>&1; then realok=$((realok+1))
    else echo "  ✗ 실제 기록을 거부했다: $(basename "$rl")"; gate_audit "$rl" "$rx" "$rmode"; rc=1; fi
  done
  if [ "$realn" -eq 0 ]; then
    echo "  · 정규형 줄이 든 실제 기록이 아직 없다 — **미확인**(러너를 한 번 돌린 뒤 다시 확인할 것)."
  else
    echo "  ✓ 실제 기록 ${realok}/${realn}건이 G10을 통과했다(합성이 아니라 러너가 뱉은 줄로 교정)."
  fi

  echo "── 양성 대조 C: 정상 xml은 통과하는가(이게 빨간불이면 위 대조는 전부 무의미)"
  local okn=$(( MIN_EDIT_CASES + 10 ))
  cat > "$tmp/ok.xml" <<X
<test-run id="2" testcasecount="$okn" total="$okn" passed="$okn" failed="0" skipped="0" inconclusive="0"></test-run>
X
  if ( report "$tmp/ok.xml" "$MIN_EDIT_CASES" 0 "$okn" "prev.xml" ) >/dev/null 2>&1; then
    echo "  ✓ 통과했다(양성 대조)"
  else echo "  ✗ 정상 파일을 거부했다 — 판독기가 고장났다."; rc=1; fi

  rm -rf "$tmp"
  [ "$rc" -eq 0 ] && echo "자기검사 통과 — 가드 10종(G10 포함) + 양성 대조 전부 제 일을 한다." || echo "자기검사 실패."
  return $rc
}

# ---- 본 실행 ---------------------------------------------------------------
run() {   # $1=edit|play  $2=라벨
  local mode="$1" label="$2"
  local platform minc
  case "$mode" in
    edit) platform=EditMode; minc=$MIN_EDIT_CASES ;;
    play) platform=PlayMode; minc=$MIN_PLAY_CASES ;;
    *) die "usage: regress.sh <edit|play> <label>" ;;
  esac
  mkdir -p "$OUTDIR"
  local xml="$OUTDIR/${label}_${mode}.xml"
  local log="$OUTDIR/${label}_${mode}.log"

  assert_no_unity_running

  # G9 — 이번 실행 파일을 지우기 **전에** 직전 최대치를 잡아 둔다(같은 라벨 재실행 대비 제외).
  local pb prevn prevwho
  pb=$(prev_best "$mode" "$xml"); prevn=${pb%% *}; prevwho=${pb#* }

  # G4a — 정적 하한이 낡았는가. 이 저장소가 실제로 당한 형태다(하한 1390 vs 실측 1800).
  stale_floor_report "$minc" "$prevn" "$prevwho" "$mode"

  # G3 — 먼저 지운다. 지워지지 않으면 그 자체가 실패다.
  rm -f "$xml" "$log"
  [ -f "$xml" ] && die "G3: 이전 결과 파일을 지우지 못했다 — $xml"

  local head dirty target started
  head=$(git -C "$REPO" rev-parse --short HEAD)
  dirty=$(git -C "$REPO" status --porcelain | wc -l | tr -d ' ')
  target=$(active_target)
  started=$(date +%s)

  echo "=========================================================="
  echo " 전량 회귀 — $platform / 라벨 '$label'"
  echo " HEAD=$head  작업트리 변경 파일=${dirty}개  활성 빌드 타깃=[$target]"
  echo " 직전 최대 ${prevn}건 (${prevwho:-없음}) — 이보다 줄면 G9가 문다"
  echo " 시작 $(date '+%F %H:%M:%S')"
  echo "=========================================================="
  [ "$dirty" != "0" ] && echo "⚠ 작업 트리가 더럽다(${dirty}개). 이 측정은 **HEAD가 아니라 지금 트리**의 결과다."
  case "$target" in
    UNKNOWN*) echo "⚠ 활성 타깃을 판정하지 못했다($target). 리플렉션/타입 기반 감사의 결과를 신뢰하지 마라." ;;
  esac

  # ★ G1/G2는 이제 주석이 아니라 코드다 — 인자 배열을 만들고 검사한 뒤에 넘긴다.
  local -a args
  args=( -batchmode -nographics
         -projectPath "$REPO"
         -runTests -testPlatform "$platform"
         -testResults "$xml"
         -logFile "$log" )
  assert_launch_args "${args[@]}"

  "$UNITY" "${args[@]}"
  local unity_rc=$?
  echo "unity 종료코드=$unity_rc"

  # G8 — 컴파일 실패는 결과 xml 유무와 무관하게 무효다.
  if log_compile_failed "$log"; then
    echo "── 컴파일 에러(중복 제거, 최대 10건) ──"
    grep -o "Assets/[^ ]*([0-9]*,[0-9]*): error CS[0-9]*: .*" "$log" 2>/dev/null | sort -u | head -10
    die "G8: 컴파일 실패로 배치모드가 거부됐다 — 이 트리에서 잰 어떤 숫자도 무효다."
  fi

  # ★ 타깃을 **실행 뒤에 다시** 읽는다. 배치모드는 시작할 때 재컴파일할 수 있어,
  #   실행 전에 읽은 값은 '직전 컴파일'의 타깃일 수 있다. 두 값이 다르면 그 자체가 사건이다.
  local target_after; target_after=$(active_target)
  [ "$target_after" != "$target" ] && \
    echo "⚠ 활성 타깃이 실행 전후로 달라졌다: [$target] -> [$target_after]. 이 실행 중에 재컴파일이 있었다."

  # ---- .meta 사이드카 -------------------------------------------------------
  # ★ 2026-09-02 qa-regression 신설. BASELINE.md 생성기(docs/verify/baseline.py)가 이 파일을
  #   **잰 값**으로 읽는다. 없으면 생성기는 로그의 Bee dag 해시로 **추론**하고 `~`를 붙인다.
  #
  #   왜 필요한가: 리더가 19:07 빌드로 활성 타깃을 OSX로 바꿔 놓고 그 뒤로도 「WIN이다」라고
  #   계속 알렸다. 원인은 **실행마다 타깃을 적어 두는 칸이 없었던 것**이다.
  #
  # ★ target= 에는 **실행 후(target_after)** 값을 적는다. 실행 전 값은 '직전 컴파일'의 산물이라
  #   재부팅 직후·타깃 전환 직후에 구조적으로 거짓말을 한다(실측). 실행 전 값은 별도 칸으로
  #   남겨 두 값이 갈렸다는 **사실 자체**가 대장에 보이게 한다.
  {
    echo "label=$label"
    echo "mode=$mode"
    echo "head=$head"
    echo "dirty=$dirty"
    echo "target=$target_after"
    echo "target_before=$target"
    echo "target_shifted=$([ "$target_after" = "$target" ] && echo 0 || echo 1)"
    echo "started=$started"
    echo "finished=$(date +%s)"
    echo "unity_rc=$unity_rc"
  } > "$OUTDIR/${label}_${mode}.meta"

  report "$xml" "$minc" "$started" "$prevn" "$prevwho"
  local rrc=$?

  # ---- G10 세이브 격리 게이트 회계 -----------------------------------------
  # ★ report와 **따로** 돈다. report는 러너 xml만 보고, 이쪽은 로그와 xml을 **대조**한다.
  #   둘의 rc를 합친다 — 어느 하나만 빨개져도 이 실행은 통과가 아니다.
  echo
  echo "── G10 세이브 격리 게이트 회계 ──"
  gate_audit "$log" "$xml" "$mode" || rrc=1

  echo
  echo "측정 조건 요약: HEAD=$head / dirty=$dirty / 타깃(후)=[$target_after] / 파일=$xml"
  return $rrc
}

case "${1:-}" in
  edit|play) [ $# -ge 2 ] || die "usage: regress.sh <edit|play> <label>"; run "$1" "$2" ;;
  report)    [ $# -ge 2 ] || die "usage: regress.sh report <xml>"; report "$2" 0 0 ;;
  compare)   [ $# -ge 3 ] || die "usage: regress.sh compare <옛.xml> <새.xml>"; compare "$2" "$3" ;;
  gate)      [ $# -ge 4 ] || die "usage: regress.sh gate <로그> <결과.xml> <edit|play>"
             gate_audit "$2" "$3" "$4" ;;
  target)    echo "활성 빌드 타깃=[$(active_target)]" ;;
  selfcheck) selfcheck ;;
  *) echo "usage: regress.sh <edit|play> <label> | report <xml> | compare <a.xml> <b.xml> | gate <log> <xml> <edit|play> | target | selfcheck"; exit 2 ;;
esac
