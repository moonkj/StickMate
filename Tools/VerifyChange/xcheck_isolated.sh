#!/usr/bin/env bash
# verify-change 격리판 — Tools/CrossCompile/xcheck.sh 와 같은 원리지만 출력 폴더에 PID를 붙여
# Library/xcheck/<target> 을 건드리지 않는다(code-inspection이 실측한 경합 결함을 피한다).
set -uo pipefail
REPO=/Users/kjmoon/App/StickMate
DAGE="$REPO/Library/Bee/artifacts/1900b0aE.dag"
DAGP="$REPO/Library/Bee/artifacts/1900b0aP.dag"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
DOTNET="$UNITY/NetCoreRuntime/dotnet"; CSCDLL="$UNITY/DotNetSdkRoslyn/csc.dll"
TARGET="$1"
case "$TARGET" in
  win) DEFS=(UNITY_STANDALONE_WIN PLATFORM_STANDALONE_WIN UNITY_EDITOR_WIN); SELF=WIN; OTHER=OSX ;;
  osx) DEFS=(UNITY_STANDALONE_OSX PLATFORM_STANDALONE_OSX UNITY_EDITOR_OSX); SELF=OSX; OTHER=WIN ;;
  *) echo usage; exit 2 ;;
esac
OUT="$2/$TARGET.$$"; rm -rf "$OUT"; mkdir -p "$OUT"; cd "$REPO"
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
mk() { grep '^-' "$1" | grep -v "${STRIP[@]}" -e '-r:.*StickMate\.' > "$2"
       for d in "${DEFS[@]}"; do echo "-define:$d" >> "$2"; done
       echo "-out:\"$3\"" >> "$2"; echo "\"$CAN\"" >> "$2"; }
RC=0
run() { # 라벨 rsp 로그 dll 최소소스
  "$DOTNET" "$CSCDLL" "@$2" > "$3" 2>&1
  n=$(grep -c '^"' "$2"); e=$(grep -c "error CS" "$3")
  grep -E "error CS" "$3" | head -8
  echo "  [$TARGET/$1] errors=$e sources=$n dll=$([ -f "$4" ] && echo OK || echo MISSING)"
  { [ "$e" -ne 0 ] || [ ! -f "$4" ]; } && RC=1
}
R="$OUT/runtime.rsp"; mk "$DAGE/StickMate.Runtime.rsp" "$R" "$OUT/R.dll"
find Assets/_Project/Scripts -name '*.cs' -not -path '*/Tests/*' | sort | sed 's/^/"/;s/$/"/' >> "$R"
run "runtime(editor)" "$R" "$OUT/runtime.log" "$OUT/R.dll"
RP="$OUT/rp.rsp"; mk "$DAGP/StickMate.Runtime.rsp" "$RP" "$OUT/RP.dll"
find Assets/_Project/Scripts -name '*.cs' -not -path '*/Tests/*' | sort | sed 's/^/"/;s/$/"/' >> "$RP"
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
echo "[$TARGET] RC=$RC  OUT=$OUT"
exit $RC
