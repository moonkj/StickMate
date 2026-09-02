#!/usr/bin/env bash
# 부하 감시 — 리더가 라운드 회수마다 돌린다(사용자 지시 2026-09-02 "부하감시도 주기적으로해").
# 종료코드로 판정하지 않는다. 출력 줄 수로 판정한다(거짓 통과 13번째 형태).
set -uo pipefail
PAT='StickMate.app/Contents/MacOS/StickMate'

ours=$(pgrep -f "$PAT" 2>/dev/null | wc -l | tr -d ' ')
unity=$(pgrep -f 'Unity.*batchmode' 2>/dev/null | wc -l | tr -d ' ')
csc=$(pgrep -f 'csc.dll' 2>/dev/null | wc -l | tr -d ' ')
control=$(pgrep -f 'ZZZNotRealProc_positive_control' 2>/dev/null | wc -l | tr -d ' ')

echo "== 부하 감시 $(date '+%H:%M:%S') =="
uptime | sed 's/.*load/  load/'
echo "  우리 인스턴스: ${ours}개   Unity: ${unity}개   csc: ${csc}개"
echo "  [양성대조] 없는 이름 매칭: ${control}  (0이 아니면 이 측정 전부 무효)"

# 1시간 넘게 산 인스턴스 = 고아 후보
if [ "$ours" -gt 0 ]; then
  now=$(date +%s)
  pgrep -f "$PAT" | while read -r p; do
    st=$(ps -p "$p" -o lstart= 2>/dev/null)
    [ -z "$st" ] && continue
    age=$(( now - $(date -j -f "%a %b %d %T %Y" "$st" +%s 2>/dev/null || echo "$now") ))
    [ "$age" -gt 3600 ] && echo "  ★ 고아 후보 PID $p — ${age}초 (1시간 초과)"
  done
fi

# ★ 오늘 실제 사고: 새벽 05:27부터 14시간째 8코어를 태우던 셸 8개를 아무도 못 봤다.
echo "  -- CPU 상위 5 --"
ps -eo pcpu,pid,comm | sort -rn | head -5 | awk '{printf "    %6s%%  %-7s %s\n",$1,$2,$3}'
exit 0
