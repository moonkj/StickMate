#!/usr/bin/env bash
# =============================================================================
# 프로덕션 액세서리 좌표 + 등급 덤프 — Unity를 띄우지 않고 프로덕션 코드를 <b>실제로 실행</b>한다.
#
#   사용법:  Tools/ShapeDump/build.sh              # 탭 구분 좌표/등급을 stdout으로
#            python3 Tools/ShapeDump/prodverify.py # 그 좌표를 설계 하니스에 먹인다(종료코드 = 위반 유무)
#
# 왜 필요한가: design/equipment/verify/verify.py는 <b>설계자가 적은 좌표</b>(items.py/hair.py)를 검산한다.
# 그것이 통과해도 "프로덕션 C#이 그 좌표를 실제로 만드는가"는 아무도 확인하지 않는다 —
# 이 저장소가 반복해서 겪은 이중 정의 계열 실패가 정확히 그 틈에서 난다.
# 그래서 Unity 동봉 Roslyn으로 프로덕션 파일을 <b>그대로</b> 컴파일해 좌표를 뽑는다.
#
# ★ 2026-09-02 — 컴파일 목록이 늘었다. NECK 6종의 좌표가 코드에서 <b>에셋</b>으로 내려가면서
#   (B-2 파일럿) 형상을 얻으려면 ItemCatalog/AccessoryDefSO 가 실제로 돌아야 한다.
#   흉내내는 것은 UnityEngine(Shim.cs) · 카테고리 사실(CoreShim.cs) · 직렬화기(AssetShim.cs)뿐이고,
#   <b>형상 문법도 등급 파생도 프로덕션 파일 그대로</b>다. shimdrift.py 가 그 경계를 감시한다.
#
# 환경변수(양성 대조용 — Tools/ShapeDumpPC 가 쓴다):
#   SHAPEDUMP_BUILDER   AccessoryShapeBuilder.cs 대신 쓸 파일
#   SHAPEDUMP_OUT       dump.dll 출력 폴더(같은 폴더에 두 빌드를 겹치지 않게)
# =============================================================================
set -euo pipefail
SP="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$SP/../.." && pwd)"
SRC="$REPO/Assets/_Project/Scripts"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
FW="$UNITY/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"

BUILDER="${SHAPEDUMP_BUILDER:-$SRC/Interaction/AccessoryShapeBuilder.cs}"
OUT="${SHAPEDUMP_OUT:-$SP}"
mkdir -p "$OUT"

ARGS=()
for f in "$FW"/*.dll; do ARGS+=("-r:$f"); done

# 흉내(3) + 프로덕션 그대로(6). 이 목록이 늘어나는 것은 좋은 방향이다 — 흉내가 줄어든다는 뜻이다.
"$UNITY/NetCoreRuntime/dotnet" "$UNITY/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -target:exe -langversion:9 \
  -out:"$OUT/dump.dll" "${ARGS[@]}" \
  "$SP/Shim.cs" "$SP/CoreShim.cs" "$SP/AssetShim.cs" "$SP/Dump.cs" \
  "$SRC/Core/ItemRarity.cs" \
  "$SRC/Core/ShortcutLabel.cs" \
  "$SRC/Core/AccessoryDefSO.cs" \
  "$SRC/Core/ItemCatalog.cs" \
  "$SRC/Core/StickMateDevTools.cs" \
  "$SRC/Interaction/ShapeCoverageGuard.cs" \
  "$BUILDER"

cat > "$OUT/dump.runtimeconfig.json" <<'JSON'
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}
JSON
export STICKMATE_RESOURCES="${STICKMATE_RESOURCES:-$REPO/Assets/_Project/Resources}"
"$UNITY/NetCoreRuntime/dotnet" "$OUT/dump.dll"
