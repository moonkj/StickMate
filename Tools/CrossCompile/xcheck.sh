#!/usr/bin/env bash
# =============================================================================
# StickMate 크로스 컴파일 검사 — Unity 에디터를 띄우지 않고 win/osx 양쪽을 확인한다.
#
#   사용법:  Tools/CrossCompile/xcheck.sh <win|osx> [--selftest]
#            Tools/CrossCompile/xcheck.sh --selftest-only <win|osx>
#
# CLAUDE.md: "Windows 전용 파일은 이 개발 머신에서 한 번도 컴파일되지 않는다"가 갭이 조용히 쌓이는
# 근본 원인이다. 이 스크립트가 그 갭을 메우는 표준 수단이다.
#
# =============================================================================
# ★ 이 도구가 낸 "거짓 초록" 5종 — 전부 여기서 구조적으로 막는다
# =============================================================================
# 이 저장소는 크로스 컴파일 검사가 **실제로는 아무것도 확인하지 않고 "에러 0"을 보고한** 사고를
# 다섯 번 겪었다(1~3은 컴파일 누락, 4는 출시 경로 미검증, 5는 어셈블리 통째 누락).
# 다섯 번 다 "사람이 스크립트를 잘 읽으면 된다"로는 못 막혔다. 그래서 전부 자동 검사로 바꿨다.
#
#  (1) 깨진 csc 래퍼:  MonoBleedingEdge/bin/csc 는 내부 경로가 빌드 머신 절대경로로 박혀 있어
#      실행 자체가 실패하는데 grep "error CS" 는 0을 센다.
#      → 대응: Unity 동봉 dotnet + DotNetSdkRoslyn/csc.dll 만 쓴다. 그리고 **산출 DLL이 이번 실행에서
#              실제로 새로 생겼는지** 확인한다(assert_artifact). 컴파일러가 안 돌면 여기서 죽는다.
#
#  (2) 낡은 소스 목록:  rsp 안의 소스 목록은 "마지막 에디터 컴파일 시점"이라 신규 파일이 빠진다.
#      → 대응: 소스 목록은 항상 트리에서 find 로 재생성하고, **최소 개수**를 확인한다.
#
#  (3) ★ rsp에 이미 박혀 있는 플랫폼 정의:  이 프로젝트의 빌드 타깃이 Windows라, 에디터가 구운 rsp에는
#      이미 -define:UNITY_STANDALONE_WIN / PLATFORM_STANDALONE_WIN 이 들어 있다. 여기에 osx 정의를
#      "추가"만 하면 **둘 다 켜진 모순된 조합**이 되거나, 지우는 목록이 불완전하면 요청한 타깃이
#      실제로는 활성이 아니게 된다.
#      → 대응: 플랫폼 계열 정의를 **전부 제거한 뒤 재주입**하고, 그 결과가 맞는지를
#              **카나리아 소스 파일(#error)** 로 컴파일러에게 직접 물어본다. 정의가 틀리면
#              컴파일이 실패한다 — 사람이 확인할 필요가 없다.
#
# =============================================================================
# ★ 네 번째 구멍 — 에디터 rsp만 쓰면 #else(릴리스) 가지가 한 줄도 컴파일되지 않는다
# =============================================================================
# 에디터가 구운 rsp에는 -define:UNITY_EDITOR 가 들어 있다. 그것만 쓰면
# `#if UNITY_EDITOR ... #else ... #endif` 의 **#else 쪽이 영원히 컴파일되지 않는다**.
# 이 저장소에는 그 형태가 실제로 있다 — Core/EquipmentDebugUnlock.cs 의 릴리스 게이트가 그렇고,
# 그 가지가 깨지면 **사용자에게 나가는 빌드에서만** 터진다(가장 늦게, 가장 비싸게 발견되는 종류).
# 그래서 런타임은 두 번 컴파일한다:
#   · editor  = 1900b0aE.dag (UNITY_EDITOR 켜짐)  — 에디터/개발 빌드 경로
#   · player  = 1900b0aP.dag (UNITY_EDITOR 꺼짐)  — ★ 실제 출시 빌드 경로
# 둘 다 통과해야 초록이다.
#
# 그리고 카나리아 자체가 "포함되지 않아서" 침묵할 가능성까지 막는다:
#   · 항상: 생성된 rsp에 카나리아 경로가 실제로 들어갔는지 확인한다.
#   · --selftest: 일부러 **반대 타깃** 카나리아를 넣어 컴파일이 <b>반드시 실패</b>하는지 확인한다.
#     (실패해야 정상이다. 통과하면 카나리아가 물지 않는다는 뜻이므로 스크립트가 죽는다.)
#
# =============================================================================
# 정의 계열에 대하여 — UNITY_EDITOR_* 는 "호스트", UNITY_STANDALONE_* 는 "타깃"이다
# =============================================================================
# 원본 rsp에는 UNITY_EDITOR_OSX(이 개발 머신이 macOS) 와 UNITY_STANDALONE_WIN(빌드 타깃이 Windows)이
# **동시에** 들어 있다. 둘은 원래 다른 축이라 모순이 아니다.
# 다만 이 검사의 목적은 "그 플랫폼 개발자의 컴파일을 재현하는 것"이므로 둘을 함께 뒤집는다 —
# Windows 개발자의 에디터는 UNITY_EDITOR_WIN 이고, 그 조합에서만 깨지는 코드가 실제로 있다.
# =============================================================================
set -uo pipefail

REPO=/Users/kjmoon/App/StickMate
DAGE="$REPO/Library/Bee/artifacts/1900b0aE.dag"   # 에디터 컴파일(UNITY_EDITOR 켜짐)
DAGP="$REPO/Library/Bee/artifacts/1900b0aP.dag"   # 플레이어 컴파일(UNITY_EDITOR 꺼짐) — 아래 참고
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
DOTNET="$UNITY/NetCoreRuntime/dotnet"
CSCDLL="$UNITY/DotNetSdkRoslyn/csc.dll"

# 소스 목록이 이보다 적으면 트리를 잘못 보고 있는 것이다(함정 2).
MIN_RUNTIME_SOURCES=100
MIN_TEST_SOURCES=30
MIN_EDITOR_SOURCES=5

TARGET=""; SELFTEST=0; SELFTEST_ONLY=0
for a in "$@"; do
  case "$a" in
    win|osx) TARGET="$a" ;;
    --selftest) SELFTEST=1 ;;
    --selftest-only) SELFTEST_ONLY=1; SELFTEST=1 ;;
    *) echo "usage: xcheck.sh <win|osx> [--selftest]"; exit 2 ;;
  esac
done
[ -n "$TARGET" ] || { echo "usage: xcheck.sh <win|osx> [--selftest]"; exit 2; }

for f in "$DOTNET" "$CSCDLL" "$DAGE/StickMate.Runtime.rsp" "$DAGP/StickMate.Runtime.rsp"; do
  [ -e "$f" ] || { echo "FATAL: 필수 파일 없음 — $f"; exit 3; }
done

case "$TARGET" in
  win) DEFS=(UNITY_STANDALONE_WIN PLATFORM_STANDALONE_WIN UNITY_EDITOR_WIN); OTHER=OSX ;;
  osx) DEFS=(UNITY_STANDALONE_OSX PLATFORM_STANDALONE_OSX UNITY_EDITOR_OSX); OTHER=WIN ;;
esac
SELF=$(echo "$TARGET" | tr '[:lower:]' '[:upper:]')

OUT="$REPO/Library/xcheck/$TARGET"
rm -rf "$OUT"; mkdir -p "$OUT"
cd "$REPO"

# 제거할 플랫폼 계열 정의 — 하나라도 빠지면 함정 3이 되살아난다.
STRIP=(-e '^-out:' -e '^-refout:')
for fam in UNITY_STANDALONE PLATFORM_STANDALONE UNITY_EDITOR; do
  for p in WIN OSX; do STRIP+=(-e "^-define:${fam}_${p}\$"); done
done

# --------------------------------------------------------------------------
# 카나리아 — 컴파일러에게 "지금 정의가 정말 맞느냐"를 직접 묻는다.
# 타입을 선언하지 않는다(전처리기 지시문만) — 어셈블리 3개에 같은 파일을 넣어도 충돌이 없다.
# --------------------------------------------------------------------------
write_canary() {   # $1=경로  $2=기대 플랫폼(WIN|OSX)  $3=반대 플랫폼
  cat > "$1" <<EOF
// 자동 생성 — Tools/CrossCompile/xcheck.sh 가 매 실행마다 새로 쓴다. 편집하지 말 것.
// 이 파일의 유일한 임무: 요청한 플랫폼 정의가 **실제로 활성인지** 컴파일러에게 확인받는 것.
#if !UNITY_STANDALONE_$2
#error XCHECK_CANARY: UNITY_STANDALONE_$2 가 활성이 아니다 — 요청한 타깃으로 컴파일되지 않았다(거짓 초록).
#endif
#if UNITY_STANDALONE_$3
#error XCHECK_CANARY: UNITY_STANDALONE_$3 가 함께 활성이다 — 플랫폼 정의 제거가 불완전하다.
#endif
#if PLATFORM_STANDALONE_$3
#error XCHECK_CANARY: PLATFORM_STANDALONE_$3 가 남아 있다 — 모순된 정의 조합이다.
#endif
EOF
}

assert_artifact() {  # $1=dll 경로  $2=라벨
  if [ ! -f "$1" ]; then
    echo "  ✗ FATAL: [$2] 컴파일러가 산출물을 만들지 않았다 — '에러 0'은 무의미하다(함정 1)."
    return 1
  fi
  return 0
}

compile() {  # $1=라벨 $2=rsp경로 $3=로그 $4=dll $5=최소소스수  -> 0=성공
  "$DOTNET" "$CSCDLL" "@$2" > "$3" 2>&1
  local n e
  n=$(grep -c '^"' "$2")
  e=$(grep -c "error CS" "$3")
  if [ "$n" -lt "$5" ]; then
    echo "  ✗ FATAL: [$1] 소스가 ${n}개뿐이다(최소 $5) — 트리를 잘못 보고 있다(함정 2)."
    return 1
  fi
  if ! grep -q "xcheck_canary.cs" "$2"; then
    echo "  ✗ FATAL: [$1] 카나리아가 소스 목록에 없다 — 정의 검사가 침묵한다."
    return 1
  fi
  grep -E "error CS" "$3" | head -20
  echo "  [$TARGET/$1] errors=$e sources=$n"
  [ "$e" -ne 0 ] && return 1
  assert_artifact "$4" "$1" || return 1
  return 0
}

build_rsp() {  # $1=원본rsp $2=대상rsp $3=소스디렉토리조건 $4=출력dll $5=카나리아
  grep '^-' "$1" | grep -v "${STRIP[@]}" -e '-r:.*StickMate\.' > "$2"
  for d in "${DEFS[@]}"; do echo "-define:$d" >> "$2"; done
  echo "-out:\"$4\"" >> "$2"
  echo "\"$5\"" >> "$2"
}

RC=0

# ---------- 1) 런타임 ----------
CANARY="$OUT/xcheck_canary.cs"
if [ "$SELFTEST_ONLY" = "1" ]; then write_canary "$CANARY" "$OTHER" "$SELF"; else write_canary "$CANARY" "$SELF" "$OTHER"; fi
# editor 판 — 테스트 어셈블리가 참조할 DLL이기도 하다.
R="$OUT/runtime.rsp"
build_rsp "$DAGE/StickMate.Runtime.rsp" "$R" "" "$OUT/StickMate.Runtime.dll" "$CANARY"
find Assets/_Project/Scripts -name '*.cs' -not -path '*/Tests/*' | sort | sed 's/^/"/;s/$/"/' >> "$R"
compile "runtime(editor)" "$R" "$OUT/runtime.log" "$OUT/StickMate.Runtime.dll" "$MIN_RUNTIME_SOURCES" || RC=1

# ★ player 판 — UNITY_EDITOR 가 없는, 실제 출시 빌드와 같은 조합. #else 가지가 여기서만 컴파일된다.
RP="$OUT/runtime_player.rsp"
build_rsp "$DAGP/StickMate.Runtime.rsp" "$RP" "" "$OUT/StickMate.Runtime.player.dll" "$CANARY"
find Assets/_Project/Scripts -name '*.cs' -not -path '*/Tests/*' | sort | sed 's/^/"/;s/$/"/' >> "$RP"
compile "runtime(player/릴리스)" "$RP" "$OUT/runtime_player.log" "$OUT/StickMate.Runtime.player.dll" "$MIN_RUNTIME_SOURCES" || RC=1

# ---------- 2) 테스트 어셈블리 2종 ----------
if [ "$RC" -eq 0 ]; then
  for A in StickMate.Tests.EditMode StickMate.Tests.PlayMode; do
    D=EditMode; [ "$A" = "StickMate.Tests.PlayMode" ] && D=PlayMode
    P="$OUT/$A.rsp"
    build_rsp "$DAGE/$A.rsp" "$P" "" "$OUT/$A.dll" "$CANARY"
    echo "-r:\"$OUT/StickMate.Runtime.dll\"" >> "$P"
    find "Assets/_Project/Scripts/Tests/$D" -name '*.cs' | sort | sed 's/^/"/;s/$/"/' >> "$P"
    compile "$A" "$P" "$OUT/$A.log" "$OUT/$A.dll" "$MIN_TEST_SOURCES" || RC=1
  done
fi

# ---------- 3) Editor 어셈블리 (Assembly-CSharp-Editor) ----------
# ★★ 다섯 번째 거짓 초록 (2026-09-01 실측) — asmdef이 없는 **기본 Editor 어셈블리**다.
#    Assets/Editor/ 아래 SceneBootstrapper.cs(프리팹/씬을 굽는 15만 자, 활발히 편집됨)가 여기 있는데
#    asmdef 기반 목록에는 잡히지 않아 이 스크립트가 통째로 건너뛰고 있었다.
#    실제 사고: 이 스크립트가 win/osx 모두 "전부 통과"를 냈는데 같은 시각 Unity 배치모드는
#    `Aborting batchmode due to failure: Scripts have compiler errors`로 거부했다
#    (SceneBootstrapper.cs의 CornerHoverPanel 잔존 참조 2건). 테스트를 한 줄도 못 돌리는 상태를
#    "초록"이라고 말한 것이다.
#    이 어셈블리는 Runtime + Tests 2종을 전부 참조하므로 반드시 **마지막에** 컴파일한다.
if [ "$RC" -eq 0 ]; then
  A=Assembly-CSharp-Editor
  P="$OUT/$A.rsp"
  build_rsp "$DAGE/$A.rsp" "$P" "" "$OUT/$A.dll" "$CANARY"
  echo "-r:\"$OUT/StickMate.Runtime.dll\"" >> "$P"
  echo "-r:\"$OUT/StickMate.Tests.EditMode.dll\"" >> "$P"
  echo "-r:\"$OUT/StickMate.Tests.PlayMode.dll\"" >> "$P"
  find Assets/Editor -name '*.cs' | sort | sed 's/^/"/;s/$/"/' >> "$P"
  compile "$A" "$P" "$OUT/$A.log" "$OUT/$A.dll" "$MIN_EDITOR_SOURCES" || RC=1
fi

# ---------- 4) 자기검사 — 카나리아가 실제로 무는가 ----------
if [ "$SELFTEST" = "1" ] && [ "$SELFTEST_ONLY" != "1" ]; then
  echo "  --- 자기검사: 반대 타깃 카나리아를 넣으면 반드시 실패해야 한다 ---"
  SOUT="$OUT/selftest"; mkdir -p "$SOUT"
  SC="$SOUT/xcheck_canary.cs"; write_canary "$SC" "$OTHER" "$SELF"
  SR="$SOUT/runtime.rsp"
  build_rsp "$DAGE/StickMate.Runtime.rsp" "$SR" "" "$SOUT/probe.dll" "$SC"
  find Assets/_Project/Scripts -name '*.cs' -not -path '*/Tests/*' | sort | sed 's/^/"/;s/$/"/' >> "$SR"
  "$DOTNET" "$CSCDLL" "@$SR" > "$SOUT/runtime.log" 2>&1
  if grep -q "XCHECK_CANARY" "$SOUT/runtime.log"; then
    echo "  ✓ 자기검사 통과 — 카나리아가 정의 불일치를 실제로 잡는다."
  else
    echo "  ✗ FATAL: 자기검사 실패 — 반대 타깃 카나리아인데도 컴파일이 통과했다."
    echo "           카나리아가 물지 않는다 = 이 스크립트의 '에러 0'은 신뢰할 수 없다."
    RC=1
  fi
fi

[ "$RC" -eq 0 ] && echo "[$TARGET] 전부 통과" || echo "[$TARGET] 실패"
exit $RC
