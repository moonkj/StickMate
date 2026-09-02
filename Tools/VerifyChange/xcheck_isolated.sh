#!/usr/bin/env bash
# verify-change 격리판 — Tools/CrossCompile/xcheck.sh 와 같은 원리지만 출력 폴더에 PID를 붙여
# Library/xcheck/<target> 을 건드리지 않는다(code-inspection이 실측한 경합 결함을 피한다).
#
# ★ 2026-09-02 verify-change 자체 수리 (거짓 통과 13번째 형태)
#   구판은 `set -uo pipefail`에 -e 가 없어 인자가 모자라면 아무것도 컴파일하지 않고 끝났다.
#   실측하니 인자 1개일 때 rc 는 1이었지만(보고된 0은 재현되지 않음), **인자 2번을 빈 문자열로**
#   주면 set -u 가 아예 안 터지고 `errors= sources= dll=MISSING` 이 5줄 나온다 —
#   즉 "errors= 줄이 5개면 통과"라는 형태 검사도 그대로 속는다.
#   그래서 판정을 **`errors=<숫자>` + `sources=<숫자>` + `dll=OK` 가 정확히 EXPECT_UNITS 줄**로 못박는다.
#
# 사용법:
#   xcheck_isolated.sh <win|osx> <출력루트>            0에러면 rc=0
#   xcheck_isolated.sh <win|osx> <출력루트> --selftest  ★ 양성 대조: 일부러 깨진 소스를 넣어
#                                                       탐지 경로가 살아 있는지 증명한다(에러 검출=성공)
set -Eeuo pipefail

REPO=/Users/kjmoon/App/StickMate
DAGE="$REPO/Library/Bee/artifacts/1900b0aE.dag"
DAGP="$REPO/Library/Bee/artifacts/1900b0aP.dag"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
DOTNET="$UNITY/NetCoreRuntime/dotnet"; CSCDLL="$UNITY/DotNetSdkRoslyn/csc.dll"
EXPECT_UNITS=5      # runtime(editor) runtime(player) EditMode PlayMode Assembly-CSharp-Editor

die() { echo "xcheck_isolated: $*" >&2; exit 2; }

usage() {
  echo "usage: $0 <win|osx> <출력루트> [--selftest]" >&2
  echo "  인자가 모자라면 rc=2 로 끝난다. 구판은 여기서 아무것도 안 하고 끝났다." >&2
}

# ---------- 인자 검증 (구판이 없던 부분) ----------
[ "$#" -ge 2 ] || { usage; exit 2; }
TARGET="${1:-}"; OUTROOT="${2:-}"; MODE="${3:-normal}"
[ -n "$TARGET"  ] || { usage; die "타깃이 비었다"; }
[ -n "$OUTROOT" ] || { usage; die "출력루트가 비었다 (빈 문자열은 구판에서 / 밑에 쓰려 했다)"; }
case "$MODE" in normal|--selftest) ;; *) usage; die "알 수 없는 모드 $MODE" ;; esac
case "$TARGET" in
  win) DEFS=(UNITY_STANDALONE_WIN PLATFORM_STANDALONE_WIN UNITY_EDITOR_WIN); SELF=WIN; OTHER=OSX ;;
  osx) DEFS=(UNITY_STANDALONE_OSX PLATFORM_STANDALONE_OSX UNITY_EDITOR_OSX); SELF=OSX; OTHER=WIN ;;
  *) usage; die "타깃은 win|osx" ;;
esac

# ---------- 전제 조건 검증 (없으면 조용히 0건이 아니라 rc=2) ----------
[ -x "$DOTNET" ]  || die "dotnet 없음: $DOTNET"
[ -f "$CSCDLL" ]  || die "csc.dll 없음: $CSCDLL"
[ -d "$DAGE" ]    || die "Bee 산출물(E) 없음: $DAGE"
[ -d "$DAGP" ]    || die "Bee 산출물(P) 없음: $DAGP"
mkdir -p "$OUTROOT" || die "출력루트를 만들 수 없다: $OUTROOT"
case "$OUTROOT" in /) die "출력루트가 / 다" ;; esac

OUT="$OUTROOT/$TARGET.$$"; rm -rf "$OUT"; mkdir -p "$OUT"; cd "$REPO"
SUMMARY="$OUT/summary.txt"; : > "$SUMMARY"

STRIP=(-e '^-out:' -e '^-refout:')
for fam in UNITY_STANDALONE PLATFORM_STANDALONE UNITY_EDITOR; do
  for p in WIN OSX; do STRIP+=(-e "^-define:${fam}_${p}\$"); done
done

CAN="$OUT/xcheck_canary.cs"
cat > "$CAN" <<EOF
#if !UNITY_STANDALONE_$SELF
#error XCHECK_CANARY: UNITY_STANDALONE_$SELF 비활성
#endif
#if UNITY_STANDALONE_$OTHER
#error XCHECK_CANARY: 반대 타깃도 활성
#endif
EOF

# ★ 양성 대조용 고의 오류 소스 — --selftest 일 때만 runtime 에 섞는다.
POISON="$OUT/xcheck_poison.cs"
cat > "$POISON" <<'EOF'
namespace XCheckSelfTest {
    internal static class Poison {
        // CS0103: 존재하지 않는 이름 — 탐지 경로가 살아 있으면 반드시 걸린다.
        internal static int Boom() { return XCHECK_THIS_SYMBOL_DOES_NOT_EXIST; }
    }
}
EOF

mk() { grep '^-' "$1" | grep -v "${STRIP[@]}" -e '-r:.*StickMate\.' > "$2" || true
       for d in "${DEFS[@]}"; do echo "-define:$d" >> "$2"; done
       echo "-out:\"$3\"" >> "$2"; echo "\"$CAN\"" >> "$2"; }

RC=0
run() { # $1=라벨 $2=rsp $3=로그 $4=dll
  local label="$1" rsp="$2" log="$3" dll="$4" n e
  set +e; "$DOTNET" "$CSCDLL" "@$rsp" > "$log" 2>&1; set -e
  n=$(grep -c '^"' "$rsp" || true)
  e=$(grep -c "error CS" "$log" || true)
  grep -E "error CS" "$log" | head -8 || true
  local line="  [$TARGET/$label] errors=$e sources=$n dll=$([ -f "$dll" ] && echo OK || echo MISSING)"
  echo "$line"; echo "$line" >> "$SUMMARY"
  if [ "$e" -ne 0 ] || [ ! -f "$dll" ]; then RC=1; fi
}

SRC_LIST="$OUT/runtime_sources.txt"
find Assets/_Project/Scripts -name '*.cs' -not -path '*/Tests/*' | sort | sed 's/^/"/;s/$/"/' > "$SRC_LIST"
[ -s "$SRC_LIST" ] || die "런타임 소스가 0건 — find 가 죽었다"
if [ "$MODE" = "--selftest" ]; then echo "\"$POISON\"" >> "$SRC_LIST"; fi

R="$OUT/runtime.rsp";  mk "$DAGE/StickMate.Runtime.rsp" "$R"  "$OUT/R.dll";  cat "$SRC_LIST" >> "$R"
run "runtime(editor)" "$R" "$OUT/runtime.log" "$OUT/R.dll"
RP="$OUT/rp.rsp";      mk "$DAGP/StickMate.Runtime.rsp" "$RP" "$OUT/RP.dll"; cat "$SRC_LIST" >> "$RP"
run "runtime(player)" "$RP" "$OUT/rp.log" "$OUT/RP.dll"

for A in StickMate.Tests.EditMode StickMate.Tests.PlayMode; do
  D=EditMode; [ "$A" = "StickMate.Tests.PlayMode" ] && D=PlayMode
  P="$OUT/$A.rsp"; mk "$DAGE/$A.rsp" "$P" "$OUT/$A.dll"
  echo "-r:\"$OUT/R.dll\"" >> "$P"
  find "Assets/_Project/Scripts/Tests/$D" -name '*.cs' | sort | sed 's/^/"/;s/$/"/' >> "$P"
  run "$A" "$P" "$OUT/$A.log" "$OUT/$A.dll"
done

A=Assembly-CSharp-Editor; P="$OUT/$A.rsp"; mk "$DAGE/$A.rsp" "$P" "$OUT/$A.dll"
echo "-r:\"$OUT/R.dll\"" >> "$P"
echo "-r:\"$OUT/StickMate.Tests.EditMode.dll\"" >> "$P"
echo "-r:\"$OUT/StickMate.Tests.PlayMode.dll\"" >> "$P"
find Assets/Editor -name '*.cs' | sort | sed 's/^/"/;s/$/"/' >> "$P"
run "$A" "$P" "$OUT/$A.log" "$OUT/$A.dll"

# ---------- ★ 형태 검사: 종료코드가 아니라 산출물 개수·모양으로 판정 ----------
# `errors=` 만 세면 빈 값(errors= sources= dll=MISSING)도 5줄로 통과한다 — 실측으로 확인했다.
WELL=$(grep -c -E 'errors=[0-9]+ sources=[0-9]+ dll=(OK|MISSING)' "$SUMMARY" || true)
if [ "$WELL" -ne "$EXPECT_UNITS" ]; then
  echo "[$TARGET] ★ 측정 무효 — 정상 형태 줄 $WELL/$EXPECT_UNITS. 이 실행의 숫자는 전부 폐기해라." >&2
  exit 3
fi

if [ "$MODE" = "--selftest" ]; then
  # 양성 대조: 고의 오류가 runtime 2개에서 검출되어야 한다. 검출되면 rc=0(대조 성공).
  P1=$(grep -c 'runtime(editor)] errors=0 ' "$SUMMARY" || true)
  P2=$(grep -c 'runtime(player)] errors=0 ' "$SUMMARY" || true)
  if [ "$P1" -ne 0 ] || [ "$P2" -ne 0 ]; then
    echo "[$TARGET] ★ 양성 대조 실패 — 고의 CS0103 을 못 잡았다. 이 도구의 모든 0건을 무효로 하라." >&2
    exit 4
  fi
  echo "[$TARGET] 양성 대조 통과 — 고의 오류를 runtime 2개에서 검출. OUT=$OUT"
  exit 0
fi

echo "[$TARGET] RC=$RC  units=$WELL/$EXPECT_UNITS  OUT=$OUT"
exit $RC
