#!/usr/bin/env bash
# =============================================================================
# 양성 대조 빌드 — 검사 코드도 shim 도 <b>한 줄도 복제하지 않는다</b>.
# Tools/ShapeDump/build.sh 를 그대로 부르고 <b>먹이는 빌더 파일만</b> 바꿔치기한다.
#
# 2026-09-02 이전에는 이 폴더가 CoreShim.cs/Shim.cs/Dump.cs/shimdrift.py/prodverify.py 를
# <b>바이트 단위로 복제</b>해 갖고 있었고 동기화 검사가 없었다 — 한쪽만 고치면
# 양성 대조가 <b>다른 물건</b>을 재게 된다. 그 상태의 양성 대조는 대조가 아니다.
# (shimdrift.py 가 이 폴더에 복제본이 되살아나는지 매번 확인한다.)
# =============================================================================
set -euo pipefail
SP="$(cd "$(dirname "$0")" && pwd)"
PARENT="$SP/AccessoryShapeBuilder.PARENT.cs"
if [ ! -f "$PARENT" ]; then
  echo "FATAL: $PARENT 가 없다. README 의 git show 한 줄을 먼저 돌려라." >&2
  exit 3
fi
export SHAPEDUMP_BUILDER="$PARENT"
export SHAPEDUMP_OUT="$SP"
exec "$SP/../ShapeDump/build.sh"
