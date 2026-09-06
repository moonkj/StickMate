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
#   SHAPEDUMP_NO_AUTODEPS=1  자동 보강을 끈다(음성 대조 전용 — 목록만으로 도는지 보고 싶을 때)
# =============================================================================
set -euo pipefail
SP="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$SP/../.." && pwd)"
SRC="$REPO/Assets/_Project/Scripts"
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents
FW="$UNITY/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"

# ★ 2026-09-06 — SHAPEDUMP_BUILDER 는 <b>빌더 partial 가족 전체</b>를 통째로 갈아 끼운다
#   (공백 구분 여러 파일 가능). 예전에는 AccessoryShapeBuilder.cs 한 개만 바꿔 끼우고
#   AccessoryShapeBuilder.Handoff.cs 는 목록에 남겨 뒀는데, 둘은 <b>같은 partial 클래스</b>라
#   옛 빌더(partial 이전 스냅숏)와 겹치는 순간 CS0260/CS0103 으로 죽는다.
#   그 상태로 Tools/ShapeDumpPC 의 <b>양성 대조 두 개가 조용히 멎어 있었다</b> —
#   즉 "mirrordrift 의 0건을 믿어도 되는가"를 증명하는 자가 없는 채로 며칠이 갔다.
BUILDER_SET=(${SHAPEDUMP_BUILDER:-"$SRC/Interaction/AccessoryShapeBuilder.cs" "$SRC/Interaction/AccessoryShapeBuilder.Handoff.cs"})
OUT="${SHAPEDUMP_OUT:-$SP}"
mkdir -p "$OUT"

ARGS=()
for f in "$FW"/*.dll; do ARGS+=("-r:$f"); done

# ── 흉내(4) — 이 넷만 우리가 쓴다. 나머지는 전부 프로덕션 파일 그대로다 ──────────
SHIMS=("$SP/Shim.cs" "$SP/CoreShim.cs" "$SP/AssetShim.cs" "$SP/Dump.cs")

# ── 프로덕션 파일(그대로 컴파일) ────────────────────────────────────────────────
# 이 목록이 늘어나는 것은 좋은 방향이다 — 흉내가 줄어든다는 뜻이다.
#
# ★ 2026-09-05 — 목록이 <b>의존성 폐포가 아니라 손으로 적은 집합</b>이라 조용히 깨진다.
#   실제 사고: ItemCatalog.IsOwned 가 CurrencyModel.IsPurchasedItem 을 부르기 시작하자
#   (커밋 1f7e139) 이 목록에 CurrencyModel.cs 가 없어 CS0103 하나로 빌드가 죽었고,
#   그 위에 얹힌 상시 게이트 둘(mirrordrift.py · prodverify.py)이 함께 멈췄다.
#
# ★ 2026-09-06 — <b>바로 위 문단이 경고한 그 형태가 그대로 재발했다.</b> ItemCatalog 에
#   SubStatTable 이 생기면서 CharacterStat 을 부르자 CS0246 하나로 또 죽었고, 게이트 둘이
#   또 멎었다. 주석은 사람에게 말하고 사람은 이 파일을 안 읽는다 —
#   그래서 아래 <b>자동 보강 루프</b>를 붙였다(deps.py). 이제 목록이 뒤처져도 게이트는 돌고,
#   뒤처졌다는 사실은 stderr 배너로 <b>시끄럽게</b> 남는다.
#   (이 저장소가 반복해 당하는 형태는 «게이트가 빨간 상태»가 아니라 «게이트가 안 도는 상태»다.)
SOURCES=(
  "$SRC/Core/ItemRarity.cs"
  "$SRC/Core/ShortcutLabel.cs"
  "$SRC/Core/AccessoryShapeContract.cs"
  "$SRC/Core/AccessoryDefSO.cs"
  "$SRC/Core/ItemCatalog.cs"
  "$SRC/Core/CurrencyModel.cs"
  "$SRC/Core/CurrencyRules.cs"
  "$SRC/Core/DanceIds.cs"
  "$SRC/Core/EquipmentStatRules.cs"
  "$SRC/Core/StickMateDevTools.cs"
  "$SRC/Interaction/ShapeCoverageGuard.cs"
)
# ★ 2026-09-05 — AccessoryShapeBuilder 가 partial 이 됐다(인계본 조각은 .Handoff.cs 에 있다).
# 그래서 「빌더」는 파일 하나가 아니라 <b>가족</b>이고, A/B 로 바꿔 끼울 때도 가족째 바뀐다(위 BUILDER_SET).
SOURCES+=("${BUILDER_SET[@]}")

compile() {
  "$UNITY/NetCoreRuntime/dotnet" "$UNITY/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -target:exe -langversion:9 \
    -out:"$OUT/dump.dll" "${ARGS[@]}" "${SHIMS[@]}" "${SOURCES[@]}"
}

# ── 자동 보강 루프 ────────────────────────────────────────────────────────────
# 컴파일러가 «못 찾았다»고 말한 이름만 따라간다(의존성을 추측하지 않는다 — 추측은 틀리고
# 컴파일러는 안 틀린다). CoreShim 이 일부러 흉내내는 타입은 deps.py 가 스스로 차단한다.
ADDED=()
ERRLOG=""
for _round in 1 2 3 4 5 6 7 8; do
  if ERRLOG="$(compile 2>&1)"; then
    break
  fi
  if [ "${SHAPEDUMP_NO_AUTODEPS:-0}" = "1" ]; then
    printf '%s\n' "$ERRLOG" >&2
    echo "!! 컴파일 실패 (SHAPEDUMP_NO_AUTODEPS=1 — 자동 보강 꺼짐)" >&2
    exit 1
  fi
  NEW="$(printf '%s\n' "$ERRLOG" | python3 "$SP/deps.py" --known "${SOURCES[@]}" 2>/dev/null || true)"
  if [ -z "$NEW" ]; then
    printf '%s\n' "$ERRLOG" >&2
    echo "!! 컴파일 실패 — 자동 보강으로 살릴 수 없다(빠진 파일이 아니라 shim 결손/문법 오류다)." >&2
    echo "   진단: printf '%s' \"\$ERR\" | python3 $SP/deps.py --known ..." >&2
    exit 1
  fi
  while IFS= read -r line; do
    [ -n "$line" ] && { SOURCES+=("$line"); ADDED+=("$line"); }
  done <<< "$NEW"
done

if [ "${#ADDED[@]}" -gt 0 ]; then
  {
    echo "★★★ [ShapeDump] build.sh 의 컴파일 목록이 의존성을 못 따라갔다 — 자동 보강 ${#ADDED[@]}개로 게이트를 살렸다."
    for a in "${ADDED[@]}"; do echo "       \"\$SRC/${a#$SRC/}\""; done
    echo "    -> 위 줄을 build.sh 의 SOURCES 에 넣어라. 안 넣으면 매 실행 이 배너가 다시 뜬다."
    echo "    -> 이 배너를 «경고»로 넘기지 마라. 자동 보강이 없던 어제까지는 같은 상황에서"
    echo "       prodverify.py / mirrordrift.py 두 상시 게이트가 통째로 멎었다(2026-09-05 · 09-06 실제 사고)."
  } >&2
fi

# 마지막 컴파일이 경고를 냈으면 버리지 않는다.
if [ -n "$ERRLOG" ]; then printf '%s\n' "$ERRLOG" >&2; fi

cat > "$OUT/dump.runtimeconfig.json" <<'JSON'
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}
JSON
export STICKMATE_RESOURCES="${STICKMATE_RESOURCES:-$REPO/Assets/_Project/Resources}"
"$UNITY/NetCoreRuntime/dotnet" "$OUT/dump.dll"
