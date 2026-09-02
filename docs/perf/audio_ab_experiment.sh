#!/bin/bash
D=/private/tmp/claude-501/-Users-kjmoon-App-StickMate/bf3ed972-ae20-4c9c-abed-8d989e6b94d7/scratchpad/audioprobe
CA=$(pgrep -x coreaudiod | head -1)
OUT=$D/exp.out
: > "$OUT"
cpu() { ps -o time= -p "$1" 2>/dev/null | tr -d ' ' | awk '{n=split($0,a,":"); if(n==3){s=a[1]*3600+a[2]*60+a[3]} else if(n==2){s=a[1]*60+a[2]} else {s=$0+0}; printf "%.2f", s}'; }
thr() { ps -M -p "$1" 2>/dev/null | tail -n +2 | wc -l | tr -d ' '; }
loadnow() { ps -A -o %cpu= | awk '{s+=$1} END {printf "%.0f", s}'; }

win() { # win <label> <sec>
  local L="$1" S="$2"
  local a=$(cpu $CA) l0=$(loadnow) t0=$(date +%s)
  sleep "$S"
  local b=$(cpu $CA) l1=$(loadnow) t1=$(date +%s)
  local w=$((t1-t0))
  echo "$L wall=${w}s coreaudiod_cpu_delta=$(echo "$b - $a" | bc)s  coreaudiod_pct=$(echo "scale=3; 100*($b-$a)/$w" | bc)%  ca_threads=$(thr $CA)  machineload=${l0}%->${l1}%" >> "$OUT"
}

echo "coreaudiod pid=$CA  start=$(date +%H:%M:%S)" >> "$OUT"
echo "--- assertions BEFORE ---" >> "$OUT"; pmset -g assertions | grep -iE "coreaudiod|audio|probe" >> "$OUT" 2>&1; echo "(위가 비었으면 오디오發 어서션 0건)" >> "$OUT"

win OFF-1 90

"$D/probe" 512 400 > "$D/cb-512.csv" 2> "$D/probe-512.err" &
PP=$!
sleep 3
echo "--- probe(512) pid=$PP threads=$(thr $PP) ---" >> "$OUT"
echo "--- assertions DURING(512) ---" >> "$OUT"; pmset -g assertions | grep -iE "coreaudiod|audio|probe" >> "$OUT" 2>&1; echo "(끝)" >> "$OUT"
win ON512-1 90
echo "probe_cpu_after_1st=$(cpu $PP)s  probe_threads=$(thr $PP)" >> "$OUT"
win ON512-2 90
echo "probe_cpu_after_2nd=$(cpu $PP)s" >> "$OUT"
kill $PP 2>/dev/null; wait $PP 2>/dev/null
sleep 3

win OFF-2 90

"$D/probe" 4096 200 > "$D/cb-4096.csv" 2> "$D/probe-4096.err" &
PQ=$!
sleep 3
echo "--- probe(4096) pid=$PQ threads=$(thr $PQ) ---" >> "$OUT"
win ON4096-1 90
echo "probe4096_cpu=$(cpu $PQ)s" >> "$OUT"
kill $PQ 2>/dev/null; wait $PQ 2>/dev/null
sleep 3
win OFF-3 90
echo "done=$(date +%H:%M:%S)" >> "$OUT"
