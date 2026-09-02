#!/usr/bin/env bash
set -euo pipefail
SP="$(cd "$(dirname "$0")" && pwd)"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
FW="$UNITY/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
ARGS=(); for f in "$FW"/*.dll; do ARGS+=("-r:$f"); done
"$UNITY/NetCoreRuntime/dotnet" "$UNITY/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -target:exe -langversion:9 \
  -out:"$SP/policy.dll" "${ARGS[@]}" "$SP/Driver.cs" \
  /Users/kjmoon/App/StickMate/Assets/_Project/Scripts/Platform/ReservedBarRevealPolicy.cs
cat > "$SP/policy.runtimeconfig.json" <<'JSON'
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}
JSON
"$UNITY/NetCoreRuntime/dotnet" "$SP/policy.dll"
