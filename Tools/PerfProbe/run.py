#!/usr/bin/env python3
"""컴포지터(WindowServer) 부하 = f(합성 표면적) 실측 하네스.

각 조건마다: WindowServer의 누적 CPU 시간을 측정 구간 앞뒤로 읽어 델타를 구한다.
ps 의 cputime 은 1/100초 해상도라 15초 구간이면 오차 0.07%p 미만.
조건 순서를 섞어 실행해 배경 부하 드리프트의 영향을 줄인다.
"""
import subprocess, time, sys, re, os, statistics

BENCH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "OverlayBench")

def pid_of(name):
    out = subprocess.run(["pgrep", "-x", name], capture_output=True, text=True).stdout.split()
    return int(out[0]) if out else None

def cputime(pid):
    """누적 CPU 시간(초)."""
    out = subprocess.run(["ps", "-o", "cputime=", "-p", str(pid)],
                         capture_output=True, text=True).stdout.strip()
    if not out:
        return None
    parts = out.replace("-", ":").split(":")
    parts = [float(p) for p in parts]
    sec = 0.0
    for p in parts:
        sec = sec * 60 + p
    return sec

WS = pid_of("WindowServer")
if WS is None:
    print("WindowServer pid를 찾지 못함"); sys.exit(1)

def measure(w, h, move, dur, alpha="0.12"):
    proc = subprocess.Popen([BENCH, str(w), str(h), str(move), str(dur + 3), alpha],
                            stdout=subprocess.PIPE, text=True)
    header = proc.stdout.readline().strip()
    app_pid = int(header.split()[1])
    time.sleep(1.5)                     # 창 생성/워밍업 구간 제외
    ws0, ap0 = cputime(WS), cputime(app_pid)
    t0 = time.time()
    time.sleep(dur)
    ws1, ap1 = cputime(WS), cputime(app_pid)
    t1 = time.time()
    wall = t1 - t0
    res = ""
    try:
        proc.wait(timeout=8)
        res = (proc.stdout.read() or "").strip()
    except subprocess.TimeoutExpired:
        proc.kill()
    return {
        "label": f"{w}x{h}pt move={move}",
        "ws_pct": (ws1 - ws0) / wall * 100,
        "app_pct": (ap1 - ap0) / wall * 100 if (ap0 is not None and ap1 is not None) else float("nan"),
        "res": res,
    }

def baseline(dur):
    ws0 = cputime(WS); t0 = time.time()
    time.sleep(dur)
    ws1 = cputime(WS); t1 = time.time()
    return {"label": "baseline(오버레이 없음)", "ws_pct": (ws1 - ws0) / (t1 - t0) * 100,
            "app_pct": float("nan"), "res": ""}

DUR = float(sys.argv[1]) if len(sys.argv) > 1 else 12.0
plan = eval(sys.argv[2]) if len(sys.argv) > 2 else None

results = []
if plan is None:
    plan = [("base",), (1512, 982, 0), (640, 640, 0), (400, 400, 0),
            (640, 640, 1), ("base",), (1512, 982, 0), (640, 640, 0)]

for step in plan:
    if step[0] == "base":
        r = baseline(DUR)
    else:
        r = measure(step[0], step[1], step[2], DUR)
    results.append(r)
    print(f"{r['label']:28s} WindowServer={r['ws_pct']:6.2f}%  app={r['app_pct']:6.2f}%  {r['res']}")
    sys.stdout.flush()
    time.sleep(2)
