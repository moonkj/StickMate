#!/usr/bin/env bash
# =============================================================================
# StickMate 전량 회귀 러너 — qa-regression 전용 (2026-09-02 신설)
#
#   사용법:
#     docs/verify/regress.sh edit  <라벨>          # EditMode 전량
#     docs/verify/regress.sh play  <라벨>          # PlayMode 전량
#     docs/verify/regress.sh report <결과.xml>     # 이미 있는 결과 파일 판독만
#     docs/verify/regress.sh compare <옛.xml> <새.xml>  # ★ 베이스라인 대조(귀속용)
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

# 전량 기준선(2026-09-02 실측). 실제 건수가 이보다 적으면 "전량"이 아니다.
# 새 테스트가 늘면 올려도 되지만 **내리는 것은 금지** — 내리는 순간 이 가드가 죽는다.
# ★ 이 정적 하한만으로는 부족하다(G9 문서 참고). 실제 방어선은 G9(직전 실행 대비)다.
MIN_EDIT_CASES=1390
MIN_PLAY_CASES=550

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

assert_no_unity_running() {
  local pids
  pids=$(pgrep -f "Unity.app/Contents/MacOS/Unity -batchmode" 2>/dev/null || true)
  [ -z "$pids" ] || die "G6: Unity 배치모드가 이미 돌고 있다(PID $pids). Library 락이 잡혀 있으므로 지금 재면 무효다. 리더에게 창(window)을 받아라."
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
def load(p):
    r = ET.parse(p).getroot()
    d = {}
    for tc in r.iter('test-case'):
        fn = tc.get('fullname')
        if fn: d[fn] = tc.get('result')
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
show("★ 새로 빨개짐(이번 라운드가 깼다)", newred)
show("초록으로 돌아옴", fixed)
show("계속 빨감(이전부터)", stayred)
show("새로 생긴 테스트 중 빨감(신규 결함)", addred)
show("사라진 테스트(지워졌거나 컴파일 안 됨)", gone)
print(f"\n새로 생긴 테스트 총 {len(added)}건 / 사라진 테스트 총 {len(gone)}건")
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

  echo "── 양성 대조 C: 정상 xml은 통과하는가(이게 빨간불이면 위 대조는 전부 무의미)"
  cat > "$tmp/ok.xml" <<'X'
<test-run id="2" testcasecount="1400" total="1400" passed="1400" failed="0" skipped="0" inconclusive="0"></test-run>
X
  if ( report "$tmp/ok.xml" 1390 0 1400 "prev.xml" ) >/dev/null 2>&1; then
    echo "  ✓ 통과했다(양성 대조)"
  else echo "  ✗ 정상 파일을 거부했다 — 판독기가 고장났다."; rc=1; fi

  rm -rf "$tmp"
  [ "$rc" -eq 0 ] && echo "자기검사 통과 — 가드 9종 + 양성 대조 3종 전부 제 일을 한다." || echo "자기검사 실패."
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
  if grep -q "Aborting batchmode due to failure" "$log" 2>/dev/null; then
    grep -m5 "error CS" "$log" 2>/dev/null
    die "G8: 컴파일 실패로 배치모드가 거부됐다 — 이 트리에서 잰 어떤 숫자도 무효다."
  fi

  # ★ 타깃을 **실행 뒤에 다시** 읽는다. 배치모드는 시작할 때 재컴파일할 수 있어,
  #   실행 전에 읽은 값은 '직전 컴파일'의 타깃일 수 있다. 두 값이 다르면 그 자체가 사건이다.
  local target_after; target_after=$(active_target)
  [ "$target_after" != "$target" ] && \
    echo "⚠ 활성 타깃이 실행 전후로 달라졌다: [$target] -> [$target_after]. 이 실행 중에 재컴파일이 있었다."

  report "$xml" "$minc" "$started" "$prevn" "$prevwho"
  local rrc=$?
  echo
  echo "측정 조건 요약: HEAD=$head / dirty=$dirty / 타깃(후)=[$target_after] / 파일=$xml"
  return $rrc
}

case "${1:-}" in
  edit|play) [ $# -ge 2 ] || die "usage: regress.sh <edit|play> <label>"; run "$1" "$2" ;;
  report)    [ $# -ge 2 ] || die "usage: regress.sh report <xml>"; report "$2" 0 0 ;;
  compare)   [ $# -ge 3 ] || die "usage: regress.sh compare <옛.xml> <새.xml>"; compare "$2" "$3" ;;
  target)    echo "활성 빌드 타깃=[$(active_target)]" ;;
  selfcheck) selfcheck ;;
  *) echo "usage: regress.sh <edit|play> <label> | report <xml> | compare <a.xml> <b.xml> | target | selfcheck"; exit 2 ;;
esac
