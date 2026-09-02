#!/usr/bin/env python3
"""MSAA 배수별 부하 실측 — **present 횟수는 60fps로 고정한 채** MSAA만 바꾼다.

배경(docs/ARCHITECTURE.md 6-15): macOS 컴포지터 비용은
    present 횟수 x (면적 항 + 창당 고정 항)
으로 확정됐고 present 무관 고정항은 검출되지 않았다. 사용자 결정으로 **활성 60fps는 유지**해야
하므로 남은 손잡이는 괄호 안뿐이다. MSAA는 그 괄호 안에서 가장 큰 항으로 지목됐다
(3024x1964 x 4샘플 컬러버퍼 실측 95MB).

설계
  · 모든 조건에서 STICKMATE_FORCE_TIER=Active -> vSyncCount=2 -> 120Hz 패널에서 정확히 60fps.
    present 횟수가 고정되므로 조건 간 차이는 **오직 MSAA 배수**다.
  · STICKMATE_FORCE_MSAA={0,2,4}로 같은 바이너리에서 MSAA만 바꾼다(재빌드 없음 = 바이너리 동일).
  · OFF(앱 종료) 위상을 섞어 절대 증분을 잡는다.
  · 사이클마다 순서를 뒤집어(정방향/역방향) 배경 부하 드리프트가 특정 조건에 몰리는 것을 막는다.
  · 집계는 **중앙값**(배경 급등 위상 방어).

지표
  · WindowServer CPU%  — 컴포지터. 코어 1개 기준.
  · StickMate CPU%     — 앱 자신(렌더 커맨드 생성 + 물리/AI/창열거).
  · GPU Device / Renderer / Tiler Utilization %  — ioreg IOAccelerator, sudo 불필요.
    ★ Apple GPU는 TBDR이라 MSAA 비용이 주로 **Renderer(타일 처리)** 쪽에 실린다.
      Device만 보면 신호가 뭉개지므로 셋 다 따로 본다.
  · GPU "In use system memory" — MSAA 버퍼가 실제로 줄었는지 확인하는 대조 신호.
"""
import subprocess, time, sys, os, re, statistics, json

APP = "/Users/kjmoon/App/StickMate/Builds/PerfProbe/StickMate.app"
APP_MATCH = "Builds/PerfProbe/StickMate.app"
PHASE = int(sys.argv[1]) if len(sys.argv) > 1 else 20
CYCLES = int(sys.argv[2]) if len(sys.argv) > 2 else 3
SETTLE = 15          # 창 부착 + 전체화면 확장(0.5s x 6) + 첫 MSAA 재생성 완료 대기
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
    for key, short in (("Device Utilization %", "dev"),
                       ("Renderer Utilization %", "rend"),
                       ("Tiler Utilization %", "tiler"),
                       ("In use system memory", "gpumem")):
        mm = re.search(r'"%s"=(\d+)' % re.escape(key), body)
        out[short] = float(mm.group(1)) if mm else float("nan")
    return out


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


def launch_app(msaa):
    cmd = (f'open -n -a "{APP}" --env STICKMATE_FORCE_TIER=Active '
           f'--env STICKMATE_FORCE_MSAA={msaa}')
    sh(cmd)
    time.sleep(SETTLE)


WS = pid_of("WindowServer")


def _player_log_path():
    """Player.log 경로. 회사명/제품명을 **여기에 베끼지 않고 ProjectSettings.asset에서 읽는다.**

    Unity는 ~/Library/Logs/<companyName>/<productName>/Player.log 로 조립한다. 이 값을 상수로
    복사해 두면 companyName이 바뀔 때(2026-09-02: DefaultCompany -> Vibelab) 이 스크립트만
    조용히 옛 경로를 보게 되고, `except OSError: return None`이 그것을 "로그에 MSAA 줄이 없다"와
    **똑같은 출력으로** 뭉갠다. 그래서 기준을 하나로 유지한다.
    """
    settings = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.abspath(__file__)))), "ProjectSettings", "ProjectSettings.asset")
    company, product = None, None
    with open(settings, encoding="utf-8") as f:
        for line in f:
            if line.startswith("  companyName: "):
                company = line[len("  companyName: "):].strip()
            elif line.startswith("  productName: "):
                product = line[len("  productName: "):].strip()
            if company and product:
                break
    if not company or not product:
        raise RuntimeError(f"ProjectSettings.asset에서 companyName/productName을 못 읽었다: {settings}")
    return os.path.expanduser(f"~/Library/Logs/{company}/{product}/Player.log")


LOG = _player_log_path()


def verify_msaa(expected):
    """Player.log에서 Screen.msaaSamples 실측치를 회수한다 — 요청과 실측이 다를 수 있다."""
    if not os.path.exists(LOG):
        # "파일이 없다"와 "변화가 없다"는 다르다. 뭉개면 실측이 조용히 무효가 된다.
        print(f"  ! Player.log가 없다: {LOG} "
              f"(ProjectSettings.asset의 companyName/productName과 실제 빌드가 어긋났을 수 있다)",
              flush=True)
        return None
    try:
        with open(LOG, encoding="utf-8", errors="ignore") as f:
            txt = f.read()
    except OSError as e:
        print(f"  ! Player.log를 못 읽었다: {LOG} ({e})", flush=True)
        return None
    hits = re.findall(r"실측 Screen\.msaaSamples=(\d+)x", txt)
    return int(hits[-1]) if hits else None


def sample(label, seconds):
    pid = app_pid()
    ws0, ap0 = cputime(WS), cputime(pid)
    t0 = time.time()
    acc = {"dev": [], "rend": [], "tiler": [], "gpumem": []}
    while time.time() - t0 < seconds:
        g = gpu_stats()
        for k in acc:
            acc[k].append(g[k])
        time.sleep(1.0)
    ws1, ap1 = cputime(WS), cputime(pid)
    wall = time.time() - t0
    row = {
        "label": label,
        "ws": (ws1 - ws0) / wall * 100,
        "app": ((ap1 - ap0) / wall * 100) if (ap0 is not None and ap1 is not None) else float("nan"),
    }
    for k, v in acc.items():
        row[k] = statistics.median(v)
    return row


def run():
    rows = []
    order = ["OFF", "M4", "M2", "M0"]
    for c in range(CYCLES):
        seq = order if c % 2 == 0 else list(reversed(order))
        for cond in seq:
            quit_app()
            time.sleep(2)
            if cond != "OFF":
                launch_app(cond[1:])
                actual = verify_msaa(cond[1:])
                if actual is None:
                    print(f"  ! {cond}: Player.log에서 MSAA 실측치를 못 읽었다", flush=True)
                elif actual != int(cond[1:]):
                    print(f"  ! {cond}: 요청 {cond[1:]}x != 실측 {actual}x — 이 위상은 폐기 대상", flush=True)
            r = sample(cond, PHASE)
            r["cycle"] = c
            rows.append(r)
            print(f"  c{c} {cond:4s} WS={r['ws']:6.2f}  app={r['app']:6.2f}  "
                  f"dev={r['dev']:5.1f} rend={r['rend']:5.1f} tiler={r['tiler']:5.1f} "
                  f"gpumem={r['gpumem']/1e6:7.1f}MB", flush=True)
    quit_app()

    print("\n=== 중앙값 ===")
    print(f"{'조건':6s} {'WS CPU%':>9s} {'app CPU%':>9s} {'GPU dev%':>9s} {'rend%':>7s} {'tiler%':>7s} {'GPUmem MB':>10s}")
    med = {}
    for cond in order:
        sel = [r for r in rows if r["label"] == cond]
        med[cond] = {k: statistics.median([s[k] for s in sel]) for k in ("ws", "app", "dev", "rend", "tiler", "gpumem")}
        m = med[cond]
        print(f"{cond:6s} {m['ws']:9.2f} {m['app']:9.2f} {m['dev']:9.1f} {m['rend']:7.1f} {m['tiler']:7.1f} {m['gpumem']/1e6:10.1f}")

    print("\n=== OFF 대비 증분 ===")
    for cond in ("M4", "M2", "M0"):
        d = {k: med[cond][k] - med["OFF"][k] for k in ("ws", "app", "dev", "rend", "tiler")}
        print(f"{cond:6s} dWS={d['ws']:+7.2f}%p  dapp={d['app']:+7.2f}%p  "
              f"dGPUdev={d['dev']:+6.1f}%p  drend={d['rend']:+6.1f}%p  dtiler={d['tiler']:+6.1f}%p")

    print("\n=== M4 대비 절감률(증분 기준) ===")
    base = {k: med["M4"][k] - med["OFF"][k] for k in ("ws", "app", "dev", "rend", "tiler")}
    for cond in ("M2", "M0"):
        d = {k: med[cond][k] - med["OFF"][k] for k in ("ws", "app", "dev", "rend", "tiler")}
        parts = []
        for k in ("ws", "app", "dev", "rend", "tiler"):
            parts.append(f"{k}={(1 - d[k] / base[k]) * 100:+5.1f}%" if abs(base[k]) > 1e-6 else f"{k}=n/a")
        print(f"{cond:6s} " + "  ".join(parts))

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "msaa-result.json")
    with open(out, "w") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print("\nraw -> " + out)


if __name__ == "__main__":
    print(f"위상 {PHASE}초 x 4조건 x {CYCLES}사이클. WindowServer pid={WS}")
    run()
