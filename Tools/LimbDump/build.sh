#!/usr/bin/env bash
# 프로덕션 States/LimbCurveRenderer.cs 를 <b>그대로</b> 컴파일해 팔다리 폴리라인을 뽑는다.
# 흉내내는 것은 UnityEngine(Shim.cs)뿐이다 — 기하 수식은 한 줄도 베끼지 않는다.
set -Eeuo pipefail
SP="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$SP/../.." && pwd)"
SRC="${LIMBDUMP_SRC:-$REPO/Assets/_Project/Scripts}"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
FW="$UNITY/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
OUT="${LIMBDUMP_OUT:-$SP}"
mkdir -p "$OUT"
ARGS=(); for f in "$FW"/*.dll; do ARGS+=("-r:$f"); done
"$UNITY/NetCoreRuntime/dotnet" "$UNITY/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -target:exe -langversion:9 \
  -out:"$OUT/limbdump.dll" "${ARGS[@]}" \
  "$SP/Shim.cs" "$SP/Dump.cs" "$SRC/States/LimbCurveRenderer.cs"
cat > "$OUT/limbdump.runtimeconfig.json" <<'JSON'
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}
JSON
exec "$UNITY/NetCoreRuntime/dotnet" "$OUT/limbdump.dll"
