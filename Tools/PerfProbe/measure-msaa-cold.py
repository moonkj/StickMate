#!/usr/bin/env python3
"""MSAA 4x vs 0x — **콜드 스타트** A/B. present는 60fps 고정(FORCE_TIER=Active).

왜 콜드 스타트여야 하는가(2026-08-31 실측으로 확정):
  런타임에 QualitySettings.antiAliasing을 바꾸면 Screen.msaaSamples는 즉시 새 값을 보고하지만
  **프로세스의 그래픽 메모리(vmmap "owned unmapped (graphics)")는 1바이트도 변하지 않는다**
  (4x/2x/0x를 22초 간격으로 돌려도 99.5MB 고정). 반면 콜드 스타트로 배수를 바꾸면 GPU 메모리가
  실제로 움직인다. 즉 **런타임 토글은 백버퍼에 반영되지 않는다** — 그리고 Screen.msaaSamples는
  그 사실을 감춘다(이 프로젝트는 이미 8x에서 같은 함정을 겪었다, 커밋 39ab690).
  따라서 MSAA A/B는 반드시 앱을 껐다 켜서 해야 한다.

설계
  · 조건 2개(4x / 0x)만 쓴다. 사이클마다 순서를 뒤집어(4-0 / 0-4) 시간 드리프트를 상쇄하고,
    **인접한 두 위상의 차이**만 본다(6-2 E1~E6의 페어드 설계와 같은 원리, 시간 축만 늘렸다).
  · 위상을 길게(기본 55초) 잡는다 — WindowServer CPU는 3초 위상에서 SNR이 부족했다.
  · 각 위상에서 vmmap으로 그래픽 메모리를 함께 찍는다. 이 값이 조건별로 달라야 A/B가 유효하다.
"""
import subprocess, time, sys, os, re, statistics, json

APP = "/Users/kjmoon/App/StickMate/Builds/PerfProbe/StickMate.app"
APP_MATCH = "Builds/PerfProbe/StickMate.app"
PHASE = int(sys.argv[1]) if len(sys.argv) > 1 else 55
CYCLES = int(sys.argv[2]) if len(sys.argv) > 2 else 6
HI, LO = 4, 0
SETTLE = 15
QUIT_WAIT = 10


def sh(cmd):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True).stdout.strip()


def app_pid():
    out = sh(f"pgrep -f '{APP_MATCH}'")
    return int(out.split()[0]) if out else None


def pid_of(name):
    out = sh(f"pgrep -x {name}")
    return int(out.split()[0]) if out else None


def cputime(pid):
    if pid is None:
        return None
    out = sh(f"ps -o cputime= -p {pid}")
    if not out:
        return None
    sec = 0.0
    for p in out.replace("-", ":").split(":"):
        sec = sec * 60 + float(p)
    return sec


PERF_RE = re.compile(r'"PerformanceStatistics" = \{(.*?)\}')


def gpu_stats():
    raw = sh("ioreg -r -d 1 -w 0 -c IOAccelerator | grep 'PerformanceStatistics'")
    m = PERF_RE.search(raw)
    body = m.group(1) if m else ""
    out = {}
    for key, short in (("Device Utilization %", "dev"), ("Renderer Utilization %", "rend"),
                       ("Tiler Utilization %", "tiler")):
        mm = re.search(r'"%s"=(\d+)' % re.escape(key), body)
        out[short] = float(mm.group(1)) if mm else float("nan")
    return out


def graphics_mem(pid):
    """vmmap의 'owned unmapped (graphics)' dirty 크기(MB)와 물리 풋프린트(MB)."""
    raw = sh(f"vmmap --summary {pid} 2>/dev/null")
    gfx = float("nan")
    foot = float("nan")
    m = re.search(r"owned unmapped \(graphics\)\s+(\S+)", raw)
    if m:
        v = m.group(1)
        gfx = float(v[:-1]) * (1024 if v.endswith("G") else 1 if v.endswith("M") else 1 / 1024)
    m = re.search(r"Physical footprint:\s+(\S+)", raw)
    if m:
        v = m.group(1)
        foot = float(v[:-1]) * (1024 if v.endswith("G") else 1 if v.endswith("M") else 1 / 1024)
    return gfx, foot


def quit_app():
    pid = app_pid()
    if pid is None:
        return
    os.kill(pid, 15)
    for _ in range(QUIT_WAIT * 2):
        if app_pid() is None:
            return
        time.sleep(0.5)
    try:
        os.kill(pid, 9)
    except Exception:
        pass
    time.sleep(1)


def run_phase(ws, msaa, seconds):
    quit_app()
    time.sleep(2)
    sh(f'open -n -a "{APP}" --env STICKMATE_FORCE_TIER=Active --env STICKMATE_FORCE_MSAA={msaa}')
    time.sleep(SETTLE)
    ap = app_pid()
    gfx, foot = graphics_mem(ap)
    ws0, ap0 = cputime(ws), cputime(ap)
    t0 = time.time()
    acc = {"dev": [], "rend": [], "tiler": []}
    while time.time() - t0 < seconds:
        g = gpu_stats()
        for k in acc:
            acc[k].append(g[k])
        time.sleep(1.0)
    ws1, ap1 = cputime(ws), cputime(ap)
    wall = time.time() - t0
    row = {"msaa": msaa, "gfxMB": gfx, "footMB": foot,
           "ws": (ws1 - ws0) / wall * 100,
           "app": ((ap1 - ap0) / wall * 100) if (ap0 is not None and ap1 is not None) else float("nan")}
    for k, v in acc.items():
        row[k] = statistics.median(v)
    return row


def main():
    ws = pid_of("WindowServer")
    print(f"위상 {PHASE}초, {CYCLES}사이클(사이클마다 순서 반전). WindowServer pid={ws}")
    pairs = []
    for c in range(CYCLES):
        order = (HI, LO) if c % 2 == 0 else (LO, HI)
        p = {}
        for v in order:
            p[v] = run_phase(ws, v, PHASE)
            r = p[v]
            print(f"  c{c} {v}x  WS={r['ws']:6.2f} app={r['app']:6.2f} dev={r['dev']:5.1f} "
                  f"rend={r['rend']:5.1f} gfx={r['gfxMB']:6.1f}MB foot={r['footMB']:6.1f}MB", flush=True)
        pairs.append(p)
        print(f"   -> 쌍 차이 dWS={p[HI]['ws'] - p[LO]['ws']:+6.2f} "
              f"dapp={p[HI]['app'] - p[LO]['app']:+6.2f} ddev={p[HI]['dev'] - p[LO]['dev']:+5.1f} "
              f"dgfx={p[HI]['gfxMB'] - p[LO]['gfxMB']:+6.1f}MB", flush=True)
    quit_app()

    print(f"\n=== 페어드 차이 ({HI}x - {LO}x), n={len(pairs)} ===")
    for k in ("ws", "app", "dev", "rend", "tiler", "gfxMB", "footMB"):
        d = [p[HI][k] - p[LO][k] for p in pairs]
        pos = sum(1 for x in d if x > 0)
        print(f"{k:7s} 중앙값 {statistics.median(d):+8.2f}  평균 {statistics.mean(d):+8.2f}  "
              f"부호 {pos}/{len(d)}  표준편차 {statistics.pstdev(d):6.2f}")

    print("\n=== 절대 수준(중앙값) ===")
    for v in (HI, LO):
        m = {k: statistics.median([p[v][k] for p in pairs]) for k in ("ws", "app", "dev", "rend", "tiler", "gfxMB", "footMB")}
        print(f"MSAA {v}x: WS={m['ws']:6.2f}%  app={m['app']:6.2f}%  dev={m['dev']:5.1f}%  "
              f"rend={m['rend']:5.1f}%  gfx={m['gfxMB']:6.1f}MB  footprint={m['footMB']:6.1f}MB")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "msaa-cold-result.json")
    with open(out, "w") as f:
        json.dump([{str(k): v for k, v in p.items()} for p in pairs], f, ensure_ascii=False, indent=1)
    print("\nraw -> " + out)


if __name__ == "__main__":
    main()
