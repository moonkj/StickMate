#!/usr/bin/env bash
# =============================================================================
# StickMate 전량 회귀 러너 — qa-regression 전용 (2026-09-02 신설)
#
#   사용법:
#     docs/verify/regress.sh edit  <라벨>      # EditMode 전량
#     docs/verify/regress.sh play  <라벨>      # PlayMode 전량
#     docs/verify/regress.sh report <결과.xml> # 이미 있는 결과 파일 판독만
#     docs/verify/regress.sh selfcheck         # ★ 가드가 실제로 무는지 확인(음성 대조)
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
# =============================================================================
set -uo pipefail

REPO=/Users/kjmoon/App/StickMate
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity
OUTDIR="$REPO/docs/verify/runs"

# 전량 기준선(2026-09-02 실측). 실제 건수가 이보다 적으면 "전량"이 아니다.
# 새 테스트가 늘면 올려도 되지만 **내리는 것은 금지** — 내리는 순간 이 가드가 죽는다.
MIN_EDIT_CASES=1390
MIN_PLAY_CASES=550

die() { echo "✗ $*" >&2; exit 1; }

active_target() {
  local rsp
  rsp=$(ls -t "$REPO"/Library/Bee/artifacts/*E.dag/StickMate.Runtime.rsp 2>/dev/null | head -1)
  [ -n "$rsp" ] || { echo "UNKNOWN"; return; }
  grep -o 'UNITY_STANDALONE_[A-Z]*' "$rsp" | sort -u | tr '\n' ' '
}

assert_no_unity_running() {
  local pids
  pids=$(pgrep -f "Unity.app/Contents/MacOS/Unity -batchmode" 2>/dev/null || true)
  [ -z "$pids" ] || die "G6: Unity 배치모드가 이미 돌고 있다(PID $pids). Library 락이 잡혀 있으므로 지금 재면 무효다. 리더에게 창(window)을 받아라."
}

# ---- 결과 판독 -------------------------------------------------------------
report() {   # $1=xml  $2=기대 최소 건수(선택)  $3=실행 시작 epoch(선택)
  local xml="$1" minc="${2:-0}" started="${3:-0}"
  [ -f "$xml" ] || die "G3: 결과 파일이 없다 — $xml. 테스트가 한 건도 돌지 않았다."
  local mt; mt=$(stat -f %m "$xml")
  if [ "$started" -gt 0 ] && [ "$mt" -lt "$started" ]; then
    die "G3: 결과 파일이 실행 시작($(date -r "$started" '+%H:%M:%S'))보다 오래됐다($(date -r "$mt" '+%H:%M:%S')) — 낡은 파일을 읽고 있다."
  fi
  python3 - "$xml" "$minc" <<'PY'
import sys, os, datetime
import xml.etree.ElementTree as ET
xml, minc = sys.argv[1], int(sys.argv[2])
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

  echo "── 음성 대조 2: 건수 미달 xml을 report가 거부하는가"
  cat > "$tmp/tiny.xml" <<'X'
<test-run id="2" testcasecount="3" total="3" passed="3" failed="0" skipped="0" inconclusive="0"></test-run>
X
  if ( report "$tmp/tiny.xml" 1390 0 ) >/dev/null 2>&1; then
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

  echo "── 음성 대조 5: 다른 Unity가 돌고 있으면 실행을 거부하는가"
  if ( assert_no_unity_running ) >/dev/null 2>&1; then
    echo "  · 지금은 도는 Unity가 없어 이 대조는 판정 불가(미확인). 배치모드가 돌 때 다시 확인할 것."
  else
    echo "  ✓ 거부했다(G6) — 실제로 도는 Unity를 봤다."
  fi

  echo "── 양성 대조: 정상 xml은 통과하는가(양성 대조 — 이게 빨간불이면 위 3건은 무의미)"
  cat > "$tmp/ok.xml" <<'X'
<test-run id="2" testcasecount="1400" total="1400" passed="1400" failed="0" skipped="0" inconclusive="0"></test-run>
X
  if ( report "$tmp/ok.xml" 1390 0 ) >/dev/null 2>&1; then
    echo "  ✓ 통과했다(양성 대조)"
  else echo "  ✗ 정상 파일을 거부했다 — 판독기가 고장났다."; rc=1; fi

  rm -rf "$tmp"
  [ "$rc" -eq 0 ] && echo "자기검사 통과 — 가드 5종 + 양성 대조 전부 제 일을 한다." || echo "자기검사 실패."
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
  echo " HEAD=$head  작업트리 변경 파일=$dirty개  활성 빌드 타깃=[$target]"
  echo " 시작 $(date '+%F %H:%M:%S')"
  echo "=========================================================="
  [ "$dirty" != "0" ] && echo "⚠ 작업 트리가 더럽다($dirty개). 이 측정은 **HEAD가 아니라 지금 트리**의 결과다."

  # ★ -quit 없음(G1). -testFilter 없음(G2). 두 줄을 지우지 마라.
  "$UNITY" -batchmode -nographics \
    -projectPath "$REPO" \
    -runTests -testPlatform "$platform" \
    -testResults "$xml" \
    -logFile "$log"
  local unity_rc=$?
  echo "unity 종료코드=$unity_rc"

  # G8 — 컴파일 실패는 결과 xml 유무와 무관하게 무효다.
  if grep -q "Aborting batchmode due to failure" "$log" 2>/dev/null; then
    grep -m5 "error CS" "$log" 2>/dev/null
    die "G8: 컴파일 실패로 배치모드가 거부됐다 — 이 트리에서 잰 어떤 숫자도 무효다."
  fi

  report "$xml" "$minc" "$started"
  local rrc=$?
  echo
  echo "측정 조건 요약: HEAD=$head / dirty=$dirty / 타깃=[$target] / 파일=$xml"
  return $rrc
}

case "${1:-}" in
  edit|play) [ $# -ge 2 ] || die "usage: regress.sh <edit|play> <label>"; run "$1" "$2" ;;
  report)    [ $# -ge 2 ] || die "usage: regress.sh report <xml>"; report "$2" 0 0 ;;
  selfcheck) selfcheck ;;
  *) echo "usage: regress.sh <edit|play> <label> | report <xml> | selfcheck"; exit 2 ;;
esac
