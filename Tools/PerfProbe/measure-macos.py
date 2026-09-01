#!/usr/bin/env python3
"""macOS 컴포지터(WindowServer) 부하 실측 — Windows 측정과 같은 구조.

Windows에서 던진 질문을 macOS에서 그대로 묻는다:
  Q1. StickMate 실행 자체가 컴포지터 부하를 얼마나 올리는가? (실행 vs 종료)
  Q2. 그 부하가 present 횟수에 비례하는가?      (Active 60fps vs FORCE_TIER=Away 15fps)

지표
  · WindowServer CPU%   — 누적 CPU 시간 델타 / 실시간. **코어 1개 기준**(Windows 스크립트와 같은 단위).
  · StickMate CPU%      — 같은 방식.
  · GPU Device Util %   — ioreg IOAccelerator(시스템 전역, sudo 불필요).
                          Windows 작업관리자의 "시스템 전체 GPU"에 대응한다.

설계: 조건을 번갈아(OFF/ON/OFF/ON...) 배치해 배경 부하 드리프트를 공통모드로 상쇄한다.
      앱 재시작이 필요해 3초 교차는 불가능하므로 위상을 길게 잡고 반복 횟수로 보상한다.
"""
import subprocess, time, sys, os, statistics

APP = "/Users/kjmoon/App/StickMate/Builds/macOS/StickMate.app"
PHASE = int(sys.argv[1]) if len(sys.argv) > 1 else 25
SETTLE = 12          # 창 부착 + 전체화면 확장(0.5s x 6) 완료 대기
QUIT_WAIT = 10

def sh(cmd):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True).stdout.strip()

def pid_of(name):
    out = sh(f"pgrep -x {name}")
    return int(out.split()[0]) if out else None

def cputime(pid):
    if pid is None: return None
    out = sh(f"ps -o cputime= -p {pid}")
    if not out: return None
    sec = 0.0
    for p in out.replace("-", ":").split(":"):
        sec = sec * 60 + float(p)
    return sec

def gpu_util():
    out = sh("ioreg -r -d 1 -w 0 -c IOAccelerator | grep -o '\"Device Utilization %\"=[0-9]*' | head -1")
    try:
        return float(out.split("=")[1])
    except Exception:
        return float("nan")

def quit_app():
    pid = pid_of("StickMate")
    if pid is None: return
    os.kill(pid, 15)                      # SIGTERM -> Unity OnApplicationQuit(저장 수행)
    for _ in range(QUIT_WAIT * 2):
        if pid_of("StickMate") is None: return
        time.sleep(0.5)
    try: os.kill(pid, 9)
    except Exception: pass
    time.sleep(1)

def launch_app(env_tier=None):
    # LaunchServices 경유(open)로 띄운다 — 셸에서 직접 exec 하면 백그라운드 QoS를 상속해
    # 프레임 페이싱이 왜곡된다(OverlayBench에서 실측으로 확인한 함정).
    cmd = f'open -n -a "{APP}"'
    if env_tier:
        cmd += f' --env STICKMATE_FORCE_TIER={env_tier}'
    sh(cmd)
    time.sleep(SETTLE)

WS = pid_of("WindowServer")

def sample(label, seconds):
    app_pid = pid_of("StickMate")
    ws0, ap0 = cputime(WS), cputime(app_pid)
    t0 = time.time()
    gpu = []
    while time.time() - t0 < seconds:
        gpu.append(gpu_util())
        time.sleep(1.0)
    ws1, ap1 = cputime(WS), cputime(app_pid)
    wall = time.time() - t0
    ws_pct = (ws1 - ws0) / wall * 100
    ap_pct = ((ap1 - ap0) / wall * 100) if (ap0 is not None and ap1 is not None) else float("nan")
    gpu_avg = statistics.mean([g for g in gpu if g == g]) if gpu else float("nan")
    r = {"label": label, "ws": ws_pct, "app": ap_pct, "gpu": gpu_avg}
    print(f"  {label:12s} WindowServer={ws_pct:6.2f}%(1코어)  StickMate={ap_pct:6.2f}%  GPU전역={gpu_avg:5.1f}%")
    sys.stdout.flush()
    return r

# 등급을 **양쪽 다 강제**한다. 강제하지 않으면 적응형 거버너가 무입력 상태에서 임의로
# Calm/Away로 내려가 "ACTIVE 위상"이 실제로는 Active가 아니게 된다(1차 측정의 결함).
CYCLES = 3
plan = []
for _ in range(CYCLES):
    plan += [("OFF", None), ("ACTIVE", "Active"), ("OFF", None), ("AWAY", "Away")]

print(f"=== macOS 컴포지터 실측 (위상 {PHASE}초 x {len(plan)}) ===")
print(f"WindowServer pid={WS} / 앱={APP}")
results = []
for label, tier in plan:
    if tier is None:
        quit_app()
        time.sleep(2)
    else:
        quit_app()
        launch_app(tier)
    results.append(sample(label, PHASE))

print()
def agg(lbl, key):
    vals = [r[key] for r in results if r["label"] == lbl and r[key] == r[key]]
    return (statistics.mean(vals), statistics.median(vals), len(vals)) if vals else (float("nan"),)*2 + (0,)

for key, unit in [("ws", "WindowServer CPU%(1코어)"), ("app", "StickMate CPU%"), ("gpu", "GPU 전역 사용률%")]:
    print(f"--- {unit} ---")
    for lbl in ("OFF", "ACTIVE", "AWAY"):
        m, md, n = agg(lbl, key)
        print(f"    {lbl:7s} 평균 {m:6.2f}  중앙값 {md:6.2f}  (n={n})")
    off_m = agg("OFF", key)[1]; act_m = agg("ACTIVE", key)[1]; awy_m = agg("AWAY", key)[1]
    print("    (아래 차이는 **중앙값** 기준 — 배경 부하 급등 위상의 오염을 줄인다)")
    print(f"    ACTIVE-OFF = {act_m-off_m:+6.2f}   AWAY-OFF = {awy_m-off_m:+6.2f}"
          f"   비율(AWAY-OFF)/(ACTIVE-OFF) = {((awy_m-off_m)/(act_m-off_m)) if (act_m-off_m)!=0 else float('nan'):.2f}")
