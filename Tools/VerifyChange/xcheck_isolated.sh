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
#                                                       탐지 경로가 살아 있는지 증명한다.
#     rc=0 통과 / rc=4 탐지경로 죽음 / rc=5 판정불가(다른 에러가 섞였다)
#     ★ «에러가 났다»만 보면 안 된다 — 주입한 그 오류가 **유일한 에러**여야 한다(TEAM.md 격리 미러 규약).
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
  # ==========================================================================
  # ★★ 2026-09-03 qa-regression — 여기가 거짓 통과였다. 고쳤다.
  #
  # 옛 판정은 이것뿐이었다:
  #     runtime(editor)/runtime(player) 의 errors 가 **0이 아니면** 양성 대조 통과.
  # 그건 TEAM.md 「격리 미러 규약」이 이미 사고로 기록한 형태다 —
  #     미러에 Library/ 가 없어 참조 DLL 전부가 CS0006 이었는데도
  #     「errors ≠ 0」이라는 이유로 **양성 대조 통과**가 찍혔고,
  #     주입한 코드는 **컴파일러에 닿지도 못했다.**
  # 즉 옛 판정은 "탐지 경로가 살아 있다"와 "전부 무너졌다"를 구분하지 못한다.
  # 규약이 요구하는 것: **「고의로 심은 그 오류가 유일한 에러인가」까지 봐라.**
  #
  # 그래서 이제 세 갈래로 나눈다(전부 다르게 생기게):
  #   rc=0  통과      — 주입 심볼 오류가 검출됐고 **그것 말고 다른 에러가 없다**
  #   rc=4  실패      — 주입 심볼 오류가 **아예 없다**(탐지 경로가 죽었다)
  #   rc=5  판정불가  — 주입 오류는 있는데 **다른 에러가 섞였다**(트리가 이미 깨져 있다)
  #                    → 이때 "양성 대조 통과"라고 말할 자격이 없다. 미확인이다.
  # ==========================================================================
  POISON_SYM=XCHECK_THIS_SYMBOL_DOES_NOT_EXIST
  st_rc=0
  for u in "runtime(editor)|$OUT/runtime.log" "runtime(player)|$OUT/rp.log"; do
    lbl="${u%%|*}"; lg="${u#*|}"
    if [ ! -f "$lg" ]; then
      echo "[$TARGET] ★ 양성 대조 실패 — $lbl 로그가 없다($lg). 컴파일이 시작조차 못 했다." >&2
      st_rc=4; continue
    fi
    E=$(grep -c "error CS" "$lg" || true)
    K=$(grep "error CS" "$lg" | grep -c "$POISON_SYM" || true)
    C6=$(grep -c "error CS0006" "$lg" || true)
    echo "  [$TARGET/$lbl] 전체에러=$E  주입심볼에러=$K  CS0006(참조없음)=$C6"
    if [ "$K" -eq 0 ]; then
      echo "[$TARGET] ★ 양성 대조 실패 — $lbl 에서 주입한 $POISON_SYM 오류가 **한 건도 없다**." >&2
      echo "     탐지 경로가 죽었다는 뜻이다. 이 도구가 낸 모든 0건을 무효로 하라." >&2
      [ "$C6" -gt 0 ] && echo "     (CS0006 ${C6}건 — 참조 DLL이 없다. 미러에 Library/ 심링크를 걸어라.)" >&2
      grep "error CS" "$lg" | sed 's/^/       /' | head -5 >&2 || true
      st_rc=4
    elif [ "$E" -ne "$K" ]; then
      echo "[$TARGET] ★ 양성 대조 **판정 불가** — $lbl 에서 주입 오류 ${K}건 외에 다른 에러 $((E - K))건이 섞였다." >&2
      echo "     주입 오류를 잡은 것은 맞지만, 그 초록/빨강이 **주입 때문인지 원래 깨진 트리 때문인지 가를 수 없다.**" >&2
      echo "     이건 '통과'가 아니라 '미확인'이다. 깨끗한 미러(git archive HEAD + 자기 파일)에서 다시 돌려라." >&2
      grep "error CS" "$lg" | grep -v "$POISON_SYM" | sed 's/^/       /' | head -5 >&2 || true
      [ "$st_rc" -eq 0 ] && st_rc=5
    fi
  done
  if [ "$st_rc" -eq 0 ]; then
    echo "[$TARGET] 양성 대조 통과 — 주입한 $POISON_SYM 오류가 runtime 2개에서 검출됐고 **그것이 유일한 에러다**. OUT=$OUT"
  fi
  exit $st_rc
fi

echo "[$TARGET] RC=$RC  units=$WELL/$EXPECT_UNITS  OUT=$OUT"
exit $RC
