#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
활쏘기 사거리 하한 재설계 — 검산 스크립트 (design-motion, 2026-09-02)

이 파일은 docs/MOTION_SPEC.md 24절의 모든 표를 재생성한다. 프로덕션 값을 베끼지 않고
**로그 실측으로 검증된 관계식**만 쓴다:

  · pt/유닛 = 화면높이 982pt / (2 x orthographicSize 12) = 40.9167
      (실측 근거: [렌더품질] orthographicSize=12.00 -> 81.8 물리픽셀/유닛, Screen 3024x1964)
  · H(배율 s) = StickConfig.BaselineCharacterTotalHeight(2.2746944) x s  [월드유닛]
      (실측 근거: [활쏘기] "신장 1.36유닛" @ 배율 0.60 -> 2.2747*0.60 = 1.3648)
  · 쓸 수 있는 최대 사거리 S = W - 0.95H
      W = 발판폭(∩ 걸어다닐 수 있는 화면범위), 0.95 = charInset 0.35 + targetInset(0.40+0.20) 0.60
      (실측 근거: Dock W=1058pt, H=55.84pt -> S=1004.9pt=24.56유닛.
                  로그 "구간 폭이 허용한 최대 24.56유닛"과 소수 둘째 자리까지 일치)
  · 활 실루엣 앞끝(뽑은 화살촉) = root - 0.645H, 뒤끝 = root + 0.233H
      (실측 근거: design/motion/2026-09-02_활쏘기_활끝실측_확대.png 픽셀 계측)
"""
import math

PT   = 982 / 24.0          # 40.9167 pt / 월드유닛
BASE = 2.2746944           # StickConfig.BaselineCharacterTotalHeight
SCREEN_W_PT = 1512.0

# --- 현행 프로덕션 값 (읽기만 함) ---
M_ABS  = 2.6   # archeryMinTargetDistanceRatio
M_MAX  = 6.6   # archeryMaxTargetDistanceRatio
REF    = 4.6   # archeryTargetDistanceRatio (비행시간 기준 사거리)
RAD    = 0.40  # archeryTargetRadiusRatio
FLIGHT = 0.62  # archeryArrowFlightSeconds
FLIGHT_MAX = 1.25
MISS_RADII = 1.5   # archeryMissShortfallRadii
APEX_FLOOR = 0.38  # archeryArrowArcApexRatio
APEX_DIST  = 0.18  # archeryArrowArcApexDistanceRatio
CHAR_INSET, TARGET_INSET = 0.35, RAD + 0.20

# --- 실측 실루엣 ---
FRONT = 0.645   # root -> 뽑은 화살촉 (H배수)
BACK  = 0.233
DIA   = 2 * RAD # 과녁 지름 0.80H

# --- 제안 ---
FLOOR_FRACTION = 0.55   # 새 필드 archeryMinDistanceSpanFraction

def H_pt(s): return BASE * s * PT
def span_available(W_pt, s): return W_pt - (CHAR_INSET + TARGET_INSET) * H_pt(s)

def flight(d_H):
    """정중앙/명중 사거리 d(H배수)에서의 비행 시간(초). ArcheryState.ResolveFlightSeconds 그대로."""
    scale = math.sqrt(max(0.25, d_H / REF))
    return min(FLIGHT_MAX, max(FLIGHT * 0.6, FLIGHT * scale))

def apex_ratio(d_H):
    """궤적 볼록함 / 궤적 수평폭. 0.18에서 포화하면 '모양이 사거리와 무관'하다는 뜻."""
    span = d_H - FRONT
    return max(APEX_FLOOR, APEX_DIST * span) / span

def clear_air(d_H):
    """활끝과 과녁 앞 테두리 사이 빈 공간, 과녁 지름 배수."""
    return (d_H - FRONT - RAD) / DIA

def band(W_pt, s, floor_fraction=FLOOR_FRACTION, m_abs=M_ABS, m_max=M_MAX):
    """제안 알고리즘. 반환 (lo_pt, hi_pt) 또는 None(=포기)."""
    Hp = H_pt(s); S = span_available(W_pt, s)
    if S < m_abs * Hp: return None
    hi = min(m_max * Hp, S)
    lo = max(m_abs * Hp, floor_fraction * hi)
    return lo, hi

if __name__ == "__main__":
    scales = [0.35, 0.60, 0.75, 1.00]
    print("== 관계식 검증 ==")
    print("  Dock W=1058pt, s=0.60 -> S=%.1fpt = %.2f유닛 (로그: 24.56유닛)"
          % (span_available(1058, 0.60), span_available(1058, 0.60) / PT))

    print("\n[A] 배율별 사거리 (pt)")
    print(f"{'배율':>5}{'H(pt)':>8}{'현행 2.60H':>11}{'제안 3.63H':>11}{'상한 6.60H':>11}{'상한/화면폭':>12}")
    for s in scales:
        Hp = H_pt(s)
        print(f"{s:>5.2f}{Hp:>8.2f}{M_ABS*Hp:>11.1f}{FLOOR_FRACTION*M_MAX*Hp:>11.1f}"
              f"{M_MAX*Hp:>11.1f}{100*M_MAX*Hp/SCREEN_W_PT:>11.1f}%")

    print("\n[B] 발동 최소 발판 폭 W >= (m+0.95)H  (pt)")
    print(f"{'m':>6}" + "".join(f"{('s='+format(s,'.2f')):>9}" for s in scales))
    for m in [2.6, 3.0, 3.63, 4.6, 6.0]:
        print(f"{m:>6.2f}" + "".join(f"{(m+0.95)*H_pt(s):>9.1f}" for s in scales))

    print("\n[C] 사거리별 연출 지표")
    print(f"{'d(H)':>6}{'빈공간(과녁지름)':>17}{'d/과녁지름':>11}{'비행 정중앙':>12}{'비행 빗나감':>12}{'apex/궤적폭':>12}")
    for d in [2.60, 3.05, 3.52, 3.63, 4.60, 4.89, 6.60]:
        print(f"{d:>6.2f}{clear_air(d):>17.2f}{d/DIA:>11.2f}{flight(d):>12.3f}"
              f"{flight(d - MISS_RADII*RAD):>12.3f}{apex_ratio(d):>12.3f}")

    print("\n[D] floor_fraction 감도 (배율 0.60, 넓은 발판)")
    for f in [0.45, 0.50, 0.55, 0.554, 0.60, 0.65]:
        lo, hi = band(1058, 0.60, floor_fraction=f)
        Hp = H_pt(0.60)
        print(f"  f={f:.3f}  밴드 {lo:6.1f}~{hi:.1f}pt ({lo/Hp:.2f}~{hi/Hp:.2f}H)"
              f"  연속2회 평균차 {(hi-lo)/3:5.1f}pt = {(hi-lo)/3/Hp:.2f}H"
              f"  빈공간 {clear_air(lo/Hp):.2f}지름")

    print("\n[E] 발판 폭별 실효 밴드 (제안, f=0.55)")
    for s in [0.60, 0.75]:
        print(f"  --- 배율 {s} (H={H_pt(s):.1f}pt), 절대밴드 포화 폭 = {(M_MAX+0.95)*H_pt(s):.0f}pt")
        for W in [227, 300, 400, 500, 600, 1058, 1512]:
            b = band(W, s)
            if b is None:
                print(f"    W={W:>5}pt -> 포기(현행과 동일)"); continue
            lo, hi = b; Hp = H_pt(s)
            print(f"    W={W:>5}pt -> {lo:6.1f}~{hi:6.1f}pt ({lo/Hp:.2f}~{hi/Hp:.2f}H)"
                  f"  평균차 {(hi-lo)/3:5.1f}pt")

    print("\n[F] 포기 빈도 — 실측 발판 표본(macOS, 2026-09-02 05:2x)")
    foot = {"Dock":1058, "안전망L":227, "안전망R":227,
            "Chrome":1490, "UnityHub":1280, "Claude":1512, "캘린더":935, "메모":501, "새메모":500}
    for s in scales:
        row = f"  s={s:.2f}: "
        for m in [2.6, 3.63, 4.6]:
            need = (m + 0.95) * H_pt(s)
            fail = [k for k, w in foot.items() if w < need]
            row += f"m_abs={m}: {len(fail)}/{len(foot)}  "
        print(row + "   (★ 제안은 m_abs를 건드리지 않으므로 m_abs=2.6 열이 그대로 유지된다)")


# ============================================================================
# 25절 추가 — 로그에서 사거리 분포를 직접 재는 검산기 (리더 지적 반영)
#   실행: python3 이_파일.py --dist
# ============================================================================
import glob, re, sys

_ROLL = re.compile(r"사거리 추첨 ([0-9.]+)유닛 \(밴드 ([0-9.]+)~([0-9.]+)유닛, "
                   r"구간 폭이 허용한 최대 ([0-9.]+)유닛")
_FIRE = re.compile(r"쪽 ([0-9.]+)유닛 앞의 과녁 .*?신장 ([0-9.]+)유닛 기준")
_LOGS = ["/tmp/stickmate-run/*.log", "/Users/kjmoon/App/StickMate/Logs/*.log"]


def _chi2(bins):
    exp = sum(bins) / len(bins)
    return sum((b - exp) ** 2 / exp for b in bins), exp


def distribution_report():
    files = sorted({p for pat in _LOGS for p in glob.glob(pat)})
    rolls, placed = [], []
    for f in files:
        try:
            txt = open(f, encoding="utf-8", errors="ignore").read()
        except OSError:
            continue
        for d, a, b, c in _ROLL.findall(txt):
            d, a, b, c = map(float, (d, a, b, c))
            hi = min(b, c)
            if hi - a > 1e-6:
                rolls.append((d - a) / (hi - a))
        for d, Hh in _FIRE.findall(txt):
            placed.append(float(d) / float(Hh))

    print("=== ① 폴링이 뽑아 버린 값 (roll01 정규화) ===")
    n = len(rolls); bins = [0] * 10
    for r in rolls: bins[min(9, int(r * 10))] += 1
    chi, exp = _chi2(bins)
    rolls.sort()
    D = max(max(abs((i + 1) / n - r), abs(r - i / n)) for i, r in enumerate(rolls))
    print(f"  N={n}  히스토그램={bins} (기대 {exp:.1f})")
    print(f"  카이제곱 {chi:.2f} (df=9, 0.05 임계 16.92) / KS D {D:.4f} (임계 {1.358/n**0.5:.4f})")
    print(f"  평균 {sum(rolls)/n:.4f} (기대 0.5)")

    print("\n=== ② 실제로 배치된 사거리 (신장 배수) ===")
    band = sorted(x for x in placed if 2.55 <= x <= 6.65)
    out = len(placed) - len(band)
    n2 = len(band); b5 = [0] * 5
    for x in band: b5[min(4, int((x - 2.6) / 4.0 * 5))] += 1
    chi2v, exp2 = _chi2(b5)
    low = sum(1 for x in band if x < FLOOR_FRACTION * M_MAX)
    print(f"  N={n2} (밴드 도입 이전 결정론적 표본 {out}개는 자동 배제)")
    print(f"  5구간={b5} (기대 {exp2:.1f})  카이제곱 {chi2v:.2f} (df=4, 0.05 임계 9.49)")
    print(f"  평균 {sum(band)/n2:.3f}H (기대 4.60), 범위 {band[0]:.3f}~{band[-1]:.3f}H")
    print(f"  제안이 잘라내는 구간(<{FLOOR_FRACTION*M_MAX:.2f}H): {low}/{n2} = {100*low/n2:.1f}% "
          f"(이론 기대 {100*(FLOOR_FRACTION*M_MAX-M_ABS)/(M_MAX-M_ABS):.1f}%)")


# 25-5 계통 편향: 도착 허용 오차가 항상 과녁 쪽으로만 쌓인다
ARRIVE_TOL = 0.12   # ArcheryState.ArriveToleranceRatio
OBSERVED_BIAS_H = 0.095   # 실기 4회 관측 평균 (-0.13유닛 / H=1.3648)

def realized(d_H):
    """실현 사거리(H배수) = 뽑은 값 − 계통 편향."""
    return d_H - OBSERVED_BIAS_H

# 25-6 세로 봉투
BOW_ORIGIN_H = 0.877       # 실측(활끝실측_확대.png)
TARGET_CENTER_H = 1 - RAD  # 0.60H — 로그 도달점(로컬) 0.82유닛/1.3648 로 교차 확인

def peak_above_ground(d_H):
    """궤적 최고점(발판 위, H배수)."""
    chord_mid = (BOW_ORIGIN_H + TARGET_CENTER_H) / 2
    span = d_H - FRONT
    return chord_mid + max(APEX_FLOOR, APEX_DIST * span)

if "--dist" in sys.argv:
    distribution_report()
    print("\n=== ③ 계통 편향(25-5) ===")
    for d in [M_ABS, FLOOR_FRACTION * M_MAX]:
        print(f"  명목 {d:.2f}H -> 실현 {realized(d):.3f}H, 빈공간 {clear_air(realized(d)):.2f}지름")
    print("\n=== ④ 궤적 최고점(25-6, 발판 위 pt) ===")
    for d in [M_ABS, FLOOR_FRACTION * M_MAX, M_MAX]:
        row = f"  d={d:.2f}H  {peak_above_ground(d):.3f}H : "
        row += "  ".join(f"s={s:.2f} {peak_above_ground(d)*H_pt(s):6.1f}pt" for s in [0.35,0.60,0.75,1.00])
        print(row)
