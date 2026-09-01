#!/usr/bin/env bash
# ============================================================================
# Assembly-CSharp-Editor(= Assets/Editor/*.cs) 크로스 컴파일 검사
# ============================================================================
# 왜 별도 파일인가:
#   같은 폴더의 xcheck.sh는 **런타임 + 테스트 어셈블리**만 검사한다. Assets/Editor는 빠져 있고,
#   그 갭이 실제로 사고를 냈다(2026-09-01: 다른 라운드가 Interaction/CornerHoverPanel.cs를 지웠는데
#   Editor/SceneBootstrapper.cs의 EnsureComponent<CornerHoverPanel>() 두 줄이 남아 있었다.
#   런타임/테스트는 전부 초록이었고 xcheck.sh도 통과했지만, 그 상태로는 **Unity가 프리팹을 굽지도
#   앱을 빌드하지도 못한다** — 에디터 어셈블리가 깨져 있기 때문이다).
#
#   SceneBootstrapper는 프리팹/씬을 굽는 유일한 경로라 여기가 깨지면 자산 파이프라인 전체가 멈춘다.
#   그래서 "런타임만 초록이면 됐다"가 성립하지 않는다.
#
# 사용:  xcheck-editor.sh <win|osx> [--selftest]
#   --selftest : 일부러 컴파일 에러를 주입해 이 검사가 실제로 잡는지 확인한다(거짓 초록 방지).
#
# 방식은 xcheck.sh와 같다: Unity 동봉 dotnet + DotNetSdkRoslyn/csc.dll,
# 소스 목록은 rsp가 아니라 **트리에서 재생성**(rsp의 목록은 마지막 에디터 컴파일 시점이라 낡는다),
# 플랫폼 정의는 rsp에서 제거 후 명시적으로 재주입(rsp에 이미 박힌 정의로 인한 거짓 초록 방지).
set -uo pipefail
REPO="${STICKMATE_REPO:-/Users/kjmoon/App/StickMate}"
DAGE="$REPO/Library/Bee/artifacts/1900b0aE.dag"
UNITY="${UNITY_ROOT:-/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents}"
DOTNET="$UNITY/NetCoreRuntime/dotnet"
CSCDLL="$UNITY/DotNetSdkRoslyn/csc.dll"

T="${1:-}"; SELFTEST="${2:-}"
case "$T" in
  win) DEFS=(-define:UNITY_STANDALONE_WIN -define:UNITY_EDITOR_WIN -define:PLATFORM_STANDALONE_WIN) ;;
  osx) DEFS=(-define:UNITY_STANDALONE_OSX -define:UNITY_EDITOR_OSX -define:PLATFORM_STANDALONE_OSX) ;;
  *) echo "usage: xcheck-editor.sh <win|osx> [--selftest]"; exit 2 ;;
esac
[ -f "$CSCDLL" ] || { echo "Roslyn을 찾지 못했습니다: $CSCDLL"; exit 2; }
[ -d "$DAGE" ] || { echo "Bee 아티팩트가 없습니다($DAGE) — Unity 에디터를 한 번 열어 컴파일하세요."; exit 2; }

OUT="$(mktemp -d)"; trap 'rm -rf "$OUT"' EXIT
cd "$REPO"
STRIP='-e ^-out: -e ^-refout: -e ^-define:UNITY_STANDALONE_WIN$ -e ^-define:UNITY_STANDALONE_OSX$'
STRIP="$STRIP -e ^-define:UNITY_EDITOR_WIN$ -e ^-define:UNITY_EDITOR_OSX$"
STRIP="$STRIP -e ^-define:PLATFORM_STANDALONE_WIN$ -e ^-define:PLATFORM_STANDALONE_OSX$"

# (1) 런타임을 먼저 빌드해 에디터가 참조할 실제 산출물을 만든다(낡은 DLL을 물면 갭이 숨는다).
R="$OUT/rt.rsp"
grep '^-' "$DAGE/StickMate.Runtime.rsp" | grep -v $STRIP > "$R"
printf '%s\n' "${DEFS[@]}" >> "$R"
echo "-out:\"$OUT/StickMate.Runtime.dll\"" >> "$R"
find Assets/_Project/Scripts -name '*.cs' -not -path '*/Tests/*' | sort | sed 's/^/"/;s/$/"/' >> "$R"
"$DOTNET" "$CSCDLL" "@$R" > "$OUT/rt.log" 2>&1
RE=$(grep -c "error CS" "$OUT/rt.log")
grep -E "error CS" "$OUT/rt.log" | head -20
echo "  [$T/runtime] errors=$RE sources=$(grep -c '^\"' "$R")"
[ "$RE" -ne 0 ] && { echo "[$T] 런타임이 먼저 깨져 있어 에디터 검사를 진행할 수 없습니다."; exit 1; }

# (2) 에디터 어셈블리.
run_editor() {
  local extra_src="${1:-}"
  local P="$OUT/ed.rsp"
  grep '^-' "$DAGE/Assembly-CSharp-Editor.rsp" | grep -v $STRIP -e '-r:.*StickMate\.Runtime' > "$P"
  printf '%s\n' "${DEFS[@]}" >> "$P"
  echo "-r:\"$OUT/StickMate.Runtime.dll\"" >> "$P"
  echo "-out:\"$OUT/Assembly-CSharp-Editor.dll\"" >> "$P"
  find Assets/Editor -name '*.cs' | sort | sed 's/^/"/;s/$/"/' >> "$P"
  [ -n "$extra_src" ] && echo "\"$extra_src\"" >> "$P"
  "$DOTNET" "$CSCDLL" "@$P" > "$OUT/ed.log" 2>&1
  EDSRC=$(grep -c '^\"' "$P")
}
run_editor
EE=$(grep -c "error CS" "$OUT/ed.log")
grep -E "error CS" "$OUT/ed.log" | head -30
echo "  [$T/Assembly-CSharp-Editor] errors=$EE sources=$EDSRC"

RC=0; [ "$EE" -ne 0 ] && RC=1

if [ "$SELFTEST" = "--selftest" ]; then
  echo "  --- 자기검사: 일부러 깨진 파일을 넣으면 반드시 실패해야 한다 ---"
  CANARY="$OUT/__Canary.cs"
  printf 'class __XCheckEditorCanary { void f() { __no_such_symbol__(); } }\n' > "$CANARY"
  run_editor "$CANARY"
  CE=$(grep -c "error CS" "$OUT/ed.log")
  if [ "$CE" -gt "$EE" ]; then
    echo "  ✓ 자기검사 통과 — 이 검사는 실제로 에디터 소스를 컴파일한다."
  else
    echo "  ✗ 자기검사 실패 — 카나리아를 넣었는데도 에러가 늘지 않았습니다(거짓 초록)."; RC=1
  fi
fi

[ "$RC" -eq 0 ] && echo "[$T] 에디터 어셈블리 통과" || echo "[$T] 에디터 어셈블리 실패"
exit $RC
