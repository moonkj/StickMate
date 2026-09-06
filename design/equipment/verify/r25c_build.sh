#!/usr/bin/env bash
# =============================================================================
# R25c — 「최단 실제 변」 진단용 프로덕션 좌표 덤프 (design-equipment, 2026-09-06)
#
#   design/equipment/verify/r25c_build.sh > dump.txt
#   python3 design/equipment/verify/r25c_shortedge.py dump.txt
#
# Tools/ShapeDump/build.sh 와 같은 원리다(Unity 를 띄우지 않고 Unity 동봉 Roslyn 으로
# 프로덕션 파일을 그대로 컴파일해 AccessoryShapeBuilder.Append 를 실제로 실행한다).
# 다른 점은 <b>딱 하나</b>: 조각마다 IsHandoff 를 함께 찍는다.
# AppearanceShapeBudgetTests.최단_실제_변_검사를_액세서리_30종으로_확장한다 가
# `if (sink[i].IsHandoff) continue;` 로 인계본을 건너뛰기 때문에, 그 플래그가 없으면
# 그 검사의 숫자를 오프라인에서 재현할 수 없다(Tools/ShapeDump/Dump.cs 는 안 찍는다).
#
# ★ 왜 Tools/ShapeDump 를 그냥 쓰지 않는가 (2026-09-06 실측)
#   `Tools/ShapeDump/build.sh` 는 지금 <b>컴파일이 깨져 있다</b>:
#     Assets/_Project/Scripts/Core/EquipmentStatRules.cs(582): CS0117
#     'EquipmentModel' 에 'IsEquipped' / 'IsUnlocked' 정의가 없다
#   = `Tools/ShapeDump/CoreShim.cs` 의 EquipmentModel 흉내가 프로덕션을 못 따라갔다.
#   그 결과 그 위에 얹힌 상시 게이트 둘(prodverify.py · mirrordrift.py)이 <b>안 돌고 있다</b>
#   — README 가 «게이트가 빨간 상태가 아니라 게이트가 안 도는 상태» 라고 경고한 그 형태이고,
#   같은 형태가 이번이 세 번째다(1f7e139 CurrencyModel · 0229f52 CharacterStat · 오늘 EquipmentModel).
#   이 스크립트는 그 shim 을 <b>고치지 않는다</b>(흉내의 반환값을 정하는 것은 하니스 주인의 판단이다).
#   대신 스크래치 사본에 두 술어만 얹어 진단을 돌린다. 좌표는 그 두 술어에 무관하다
#   (Append 는 착용 여부를 안 본다 — 부르는 쪽이 자리/번호를 직접 준다).
#   ⇒ 하니스 주인(qa-regression / verify-change)에게 <b>CoreShim 보강</b>을 올릴 것.
# =============================================================================
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
SP="$REPO/Tools/ShapeDump"
SRC="$REPO/Assets/_Project/Scripts"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
FW="$UNITY/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
OUT="${R25C_OUT:-${TMPDIR:-/tmp}/r25c.$$}"
mkdir -p "$OUT"

# CoreShim 스크래치 사본 — 위 문단이 말한 두 술어만 얹는다(원본은 안 건드린다).
python3 - "$SP/CoreShim.cs" "$OUT/CoreShim.cs" <<'PY'
import sys
src, dst = sys.argv[1], sys.argv[2]
s = open(src).read()
needle = "        public static int WornIndex(EquipmentSlot slot) => NotWorn;"
if needle not in s:
    sys.exit("CoreShim.cs 에서 WornIndex 앵커를 못 찾았다 — 하니스가 바뀌었다. 이 스크립트를 갱신하라.")
s = s.replace(needle, needle + """
        // [R25c 진단 전용] 좌표 덤프는 착용 상태와 무관하다 — Append 는 자리/번호를 인자로 받는다.
        public static bool IsEquipped(EquipmentSlot slot) => false;
        public static bool IsUnlocked(EquipmentSlot slot) => true;""")
open(dst, "w").write(s)
PY

ARGS=(); for f in "$FW"/*.dll; do ARGS+=("-r:$f"); done
SHIMS=("$SP/Shim.cs" "$OUT/CoreShim.cs" "$SP/AssetShim.cs" "$HERE/r25c_dump.cs")
SOURCES=(
  "$SRC/Core/ItemRarity.cs" "$SRC/Core/ShortcutLabel.cs" "$SRC/Core/AccessoryShapeContract.cs"
  "$SRC/Core/AccessoryDefSO.cs" "$SRC/Core/ItemCatalog.cs" "$SRC/Core/CurrencyModel.cs"
  "$SRC/Core/CurrencyRules.cs" "$SRC/Core/DanceIds.cs" "$SRC/Core/StickMateDevTools.cs"
  "$SRC/Interaction/ShapeCoverageGuard.cs"
  "$SRC/Interaction/AccessoryShapeBuilder.cs" "$SRC/Interaction/AccessoryShapeBuilder.Handoff.cs"
)
compile() {
  "$UNITY/NetCoreRuntime/dotnet" "$UNITY/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -target:exe -langversion:9 \
    -out:"$OUT/r25c.dll" "${ARGS[@]}" "${SHIMS[@]}" "${SOURCES[@]}"
}
ERR=""
for _r in 1 2 3 4 5 6 7 8; do
  if ERR="$(compile 2>&1)"; then break; fi
  NEW="$(printf '%s\n' "$ERR" | python3 "$SP/deps.py" --known "${SOURCES[@]}" 2>/dev/null || true)"
  [ -n "$NEW" ] || { printf '%s\n' "$ERR" >&2; echo "!! 컴파일 실패 — 자동 보강으로 못 살린다." >&2; exit 1; }
  while IFS= read -r l; do [ -n "$l" ] && SOURCES+=("$l"); done <<< "$NEW"
done
cat > "$OUT/r25c.runtimeconfig.json" <<JSON
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}
JSON
STICKMATE_RESOURCES="${STICKMATE_RESOURCES:-$REPO/Assets/_Project/Resources}" \
  "$UNITY/NetCoreRuntime/dotnet" "$OUT/r25c.dll"
