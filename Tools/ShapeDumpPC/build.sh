#!/usr/bin/env bash
# =============================================================================
# 프로덕션 액세서리 좌표 덤프 — Unity를 띄우지 않고 AccessoryShapeBuilder를 <b>실제로 실행</b>한다.
#
#   사용법:  Tools/ShapeDump/build.sh          # 탭 구분 좌표를 stdout으로
#            python3 Tools/ShapeDump/prodverify.py   # 그 좌표를 설계 하니스에 먹인다
#
# 왜 필요한가: design/equipment/verify/verify.py는 <b>설계자가 적은 좌표</b>(items.py/hair.py)를 검산한다.
# 그것이 통과해도 "프로덕션 C#이 그 좌표를 실제로 만드는가"는 아무도 확인하지 않는다 —
# 이 저장소가 반복해서 겪은 이중 정의 계열 실패가 정확히 그 틈에서 난다.
# 그래서 Unity 동봉 Roslyn으로 프로덕션 파일을 <b>그대로</b> 컴파일해 좌표를 뽑는다.
# UnityEngine/StickMate.Core는 Shim.cs / CoreShim.cs가 최소한만 흉내낸다(순수 수학뿐이라 안전하다).
# =============================================================================
set -euo pipefail
SP="$(cd "$(dirname "$0")" && pwd)"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
FW="$UNITY/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
ARGS=()
for f in "$FW"/*.dll; do ARGS+=("-r:$f"); done
"$UNITY/NetCoreRuntime/dotnet" "$UNITY/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -target:exe -langversion:9 \
  -out:"$SP/dump.dll" "${ARGS[@]}" \
  "$SP/Shim.cs" "$SP/CoreShim.cs" "$SP/Dump.cs" \
  "$SP/AccessoryShapeBuilder.PARENT.cs"
cat > "$SP/dump.runtimeconfig.json" <<'JSON'
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}
JSON
"$UNITY/NetCoreRuntime/dotnet" "$SP/dump.dll"
