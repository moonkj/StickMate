#!/usr/bin/env python3
"""페어드(교차) 설계: 한 프로세스가 큰 창/작은 창을 3초마다 번갈아 만들고,
각 페이즈마다 WindowServer 누적 CPU 시간을 잰다. 배경 부하가 시간에 따라 흔들려도
인접한 BIG/SMALL 쌍의 차이는 그 드리프트를 공통모드로 상쇄한다.
"""
import subprocess, time, os, sys, statistics

BENCH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "OverlayBench")

def pid_of(name):
    out = subprocess.run(["pgrep", "-x", name], capture_output=True, text=True).stdout.split()
    return int(out[0]) if out else None

def cputime(pid):
    out = subprocess.run(["ps", "-o", "cputime=", "-p", str(pid)],
                         capture_output=True, text=True).stdout.strip()
    if not out: return None
    sec = 0.0
    for p in out.replace("-", ":").split(":"):
        sec = sec * 60 + float(p)
    return sec

WS = pid_of("WindowServer")
w1, h1, w2, h2 = [int(x) for x in sys.argv[1:5]]
phase = float(sys.argv[5]); cycles = int(sys.argv[6])
move = sys.argv[7] if len(sys.argv) > 7 else "0"
dur = phase * cycles * 2 + 2

proc = subprocess.Popen([BENCH, "alt", str(w1), str(h1), move, str(dur), "0.12",
                         str(w2), str(h2), str(phase),
                         (sys.argv[8] if len(sys.argv) > 8 else "1")],
                        stdout=subprocess.PIPE, text=True, bufsize=1)
print(proc.stdout.readline().strip())

samples = {"BIG": [], "SMALL": []}
settle = 0.6
window = phase - settle - 0.15
try:
    while True:
        line = proc.stdout.readline()
        if not line: break
        line = line.strip()
        if not line.startswith("PHASE"):
            print(line); break
        label = line.split()[1]
        time.sleep(settle)
        a = cputime(WS); t0 = time.time()
        time.sleep(window)
        b = cputime(WS); t1 = time.time()
        if a is not None and b is not None:
            samples[label].append((b - a) / (t1 - t0) * 100)
finally:
    try: proc.wait(timeout=5)
    except Exception: proc.kill()

big, small = samples["BIG"], samples["SMALL"]
n = min(len(big), len(small))
pairs = [big[i] - small[i] for i in range(n)]
print(f"\n조건: BIG={w1}x{h1}pt  SMALL={w2}x{h2}pt  move={move}  페이즈={phase}s  표본={n}쌍")
print(f"BIG   WindowServer 평균 {statistics.mean(big[:n]):6.2f}%  중앙값 {statistics.median(big[:n]):6.2f}%")
print(f"SMALL WindowServer 평균 {statistics.mean(small[:n]):6.2f}%  중앙값 {statistics.median(small[:n]):6.2f}%")
print(f"쌍별 차이(BIG-SMALL) 평균 {statistics.mean(pairs):+6.2f}%p  중앙값 {statistics.median(pairs):+6.2f}%p"
      f"  표준편차 {statistics.pstdev(pairs):5.2f}")
pos = sum(1 for p in pairs if p > 0)
print(f"차이가 양수인 쌍: {pos}/{n}  (부호검정)")
print("BIG   raw:", " ".join(f"{v:.1f}" for v in big[:n]))
print("SMALL raw:", " ".join(f"{v:.1f}" for v in small[:n]))
