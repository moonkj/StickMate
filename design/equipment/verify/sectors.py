# -*- coding: utf-8 -*-
"""★ 실루엣 예약 대역(sector) — DLC 팩이 늘어도 쌍별 실루엣 차가 안 줄어들게 하는 장치.

문제 (리더 배정문, 2026-09-02)
------------------------------
HEAD 쌍별 최소 실루엣 차가 1.84 -> 1.80 -> 1.50획으로 두 라운드 연속 내려갔다. 그리고
DLC 6팩 x 4슬롯이 붙으면 각 카테고리가 6종 -> 12종이 된다. **쌍별 최소는 아이템이 늘수록
구조적으로 줄어든다**(같은 상자에 물건을 더 넣으면 서로 가까워진다).

이 파일이 하는 일
-----------------
쌍별 최소를 "다 그린 뒤에 재는 값"에서 **"그리기 전에 예약해 두는 값"**으로 바꾼다.

  프로파일은 72구간 x 5도, 값은 그 방향의 최대 반경이다(rig.profile).
  두 아이템의 차는 **어느 한 구간에서라도** 크면 크다(L-infinity).
  => 팩마다 구간 몇 개를 **예약**하고, 그 구간에서 그 팩만 멀리 나가면
     "그 팩 vs 나머지 전부"의 차는 **다른 아이템을 몇 개 더 넣든 그대로다.**

그래서 두 가지를 계산한다:
  (1) 빈 대역 지도 — 기존 6종이 아무도 안 쓰는 방향과 그 여유고도(headroom radius)
  (2) 예약 검사    — 예약한 팩 아이템이 자기 대역에서 실제로 하한만큼 튀어나오는가

배율에 대하여 (★ 이 라운드의 정정)
-----------------------------------
"1.50획"은 **배율 0.75 기준**이다. 좌표는 R 배수라 배율 불변이지만 W는 배율에 따라 커진다:
    Δ = 0.5159 R = 1.50획 @0.75 = **1.20획 @0.60**(사용자 실제 저장 배율)
즉 지금 래칫은 사용자 화면에서 이미 1.20획이다. 하한 1.00획을 **0.60에서** 지키려면
Δ >= 0.4298 R = **1.25획 @0.75**여야 한다. 아래 상수는 그래서 R로 적는다.
"""
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
from rig import BINS, BIN_DEG

# ---------------------------------------------------------------------------
# ★ 하한 상수 — 여기 한 곳에만 적는다.
# ---------------------------------------------------------------------------

def stroke_in_R(scale=0.75):
    return max(0.048 * scale, 2.0 / 35.25) / (0.22 * scale)

W075 = stroke_in_R(0.75)        # 0.343864 R
W060 = stroke_in_R(0.60)        # 0.429825 R

#: ★ 쌍별 실루엣 차 하한 — **R로 적는다**(획으로 적으면 배율마다 값이 달라진다).
#  = 1획 @ 배율 0.60. 사용자의 실제 저장 배율에서 획 하나를 보장한다는 뜻이다.
SILHOUETTE_FLOOR_R = W060                      # 0.42983 R = 1.25획 @0.75

#: 래칫(현행 유지선). 프로덕션 HEAD 현재값 1.50획@0.75 = 0.5158 R.
#  ★ 신규 24종은 이 값을 **내리지 않고** 통과해야 한다(리더 지시).
SILHOUETTE_RATCHET_R = 1.50 * W075             # 0.51580 R = 1.20획 @0.60

#: 예약 대역이 확보해야 하는 여유고도. 래칫과 같은 값을 쓴다 —
#  "예약 대역에서 그만큼 튀어나오면 나머지 전부와의 차가 자동으로 래칫 이상"이 되게.
SECTOR_CLEARANCE_R = SILHOUETTE_RATCHET_R

#: 예약 대역의 최소 폭(구간 수). 1구간(5도)짜리 예약은 프로파일 표본이 변에서만 나오는 탓에
#  좌표를 조금만 흔들어도 이웃 구간으로 새 나간다. 3구간(15도)이면 변 하나가 통째로 들어간다.
SECTOR_MIN_BINS = 3


def profile(shapes, anchor_y=0.0):
    return rig.profile(shapes, anchor_y)


def envelope(table, anchor_y=0.0, exclude=()):
    """카테고리 안 **모든 아이템의 프로파일 최대**(=봉투). 빈 대역은 이 봉투가 낮은 자리다."""
    env = [0.0] * BINS
    for n, sh in table.items():
        if n in exclude: continue
        p = profile(sh, anchor_y)
        for i in range(BINS):
            if p[i] > env[i]: env[i] = p[i]
    return env


def free_sectors(table, anchor_y=0.0, clearance=SECTOR_CLEARANCE_R,
                 reach_cap=None, min_bins=SECTOR_MIN_BINS):
    """빈 대역 후보: 연속한 min_bins 구간에서 봉투가 (reach_cap − clearance) 이하인 곳.

    reach_cap = 그 슬롯이 물리적으로 도달할 수 있는 최대 반경(액자/린트가 정한 값).
    반환: [(시작각, 끝각, 봉투최대, 여유고도)] — 여유고도가 클수록 좋은 자리다."""
    env = envelope(table, anchor_y)
    if reach_cap is None: reach_cap = max(env) + clearance
    out = []
    for start in range(BINS):
        idx = [(start + k) % BINS for k in range(min_bins)]
        hi = max(env[i] for i in idx)
        if hi + clearance <= reach_cap:
            out.append((start * BIN_DEG, ((start + min_bins) % BINS) * BIN_DEG, hi, reach_cap - hi))
    return out


def merge_runs(secs):
    """연속한 후보를 하나의 구간으로 뭉친다(보고용)."""
    if not secs: return []
    starts = sorted(int(s[0] / BIN_DEG) for s in secs)
    runs = []
    cur = [starts[0], starts[0]]
    for b in starts[1:]:
        if b == cur[1] + 1: cur[1] = b
        else: runs.append(cur); cur = [b, b]
    runs.append(cur)
    if len(runs) > 1 and runs[0][0] == 0 and runs[-1][1] == BINS - 1:
        runs[0][0] = runs[-1][0] - BINS; runs.pop()
    return runs


def sector_check(item_shapes, table, sector_deg, anchor_y=0.0, clearance=SECTOR_CLEARANCE_R):
    """예약 검사. sector_deg = (시작각, 구간수).
    돌려주는 것: (내 최대반경, 남들 최대반경, 확보 여유, 통과여부)"""
    start, n = sector_deg
    idx = [(int(start / BIN_DEG) + k) % BINS for k in range(n)]
    mine = profile(item_shapes, anchor_y)
    env = envelope(table, anchor_y)
    a = max(mine[i] for i in idx)
    b = max(env[i] for i in idx)
    return a, b, a - b, (a - b) >= clearance


# ---------------------------------------------------------------------------
# ★ 등급 조형 지표 — 카드 44px에서도 살아남는 축만 쓴다.
#   카드는 아이템을 **자기 경계상자에 맞춰 정규화**한다(verify.py의 k = 44*0.86/최대변).
#   => "크다/작다"는 카드에서 사라진다. 크기에 의존하는 등급 규칙은 카드에서 무너진다.
#   그래서 등급은 **크기 불변량**으로만 만든다: 돌출 개수 / 볼록결손 / 종횡비.
# ---------------------------------------------------------------------------
def prongs(shapes, anchor_y=0.0, rel_floor=0.18, abs_floor_R=None):
    """프로파일의 국소 최대 중 '양옆 골 대비 솟은 높이'(prominence)가 기준을 넘는 것.

    rel_floor    : r_max 대비 비율 — **카드**에서 읽히는가(정규화 후에도 살아남는 축)
    abs_floor_R  : R 단위 절대 하한 — **착용 크기**에서 읽히는가(기본 1획 @0.60)
    둘 다 넘어야 돌출로 센다."""
    if abs_floor_R is None: abs_floor_R = W060
    p = profile(shapes, anchor_y)
    rmax = max(p)
    if rmax <= 0: return [], 0.0
    out = []
    for i in range(BINS):
        a, b, c = p[(i - 1) % BINS], p[i], p[(i + 1) % BINS]
        if not (b >= a and b >= c and b > 0): continue
        if b == a and b == c: continue
        # 좌우로 내려가면서 만나는 최저점(다시 올라가 나보다 높아지기 전까지)
        def valley(step):
            lo = b
            j = i
            for _ in range(BINS - 1):
                j = (j + step) % BINS
                if p[j] > b: break
                lo = min(lo, p[j])
            return lo
        prom = b - max(valley(1), valley(-1))
        if prom >= rel_floor * rmax and prom >= abs_floor_R:
            out.append((i * BIN_DEG, b, prom))
    # 같은 봉우리가 이웃 구간에서 두 번 잡히면 하나로
    out.sort(key=lambda t: -t[2])
    keep = []
    for d, r, pr in out:
        if all(min(abs(d - e[0]), 360 - abs(d - e[0])) > 10.0 for e in keep): keep.append((d, r, pr))
    return keep, rmax


def convex_deficiency(shapes):
    """1 − (실루엣 면적 / 볼록껍질 면적). 크기 불변량이라 카드 정규화에도 살아남는다."""
    pts = [q for s in shapes for q in s.pts]
    hull = _hull(pts)
    ha = _area(hull)
    # 실루엣 면적은 프로파일 부채꼴 근사가 아니라 채움 도형 합집합의 스캔라인으로 잰다.
    a = _union_area(shapes)
    return 0.0 if ha <= 0 else max(0.0, 1.0 - a / ha)


def _hull(pts):
    p = sorted(set(pts))
    if len(p) < 3: return p
    def half(ps):
        st = []
        for q in ps:
            while len(st) >= 2 and (st[-1][0]-st[-2][0])*(q[1]-st[-2][1]) - (st[-1][1]-st[-2][1])*(q[0]-st[-2][0]) <= 0:
                st.pop()
            st.append(q)
        return st
    return half(p)[:-1] + half(p[::-1])[:-1]


def _area(poly):
    s = 0.0; n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        s += a[0]*b[1] - b[0]*a[1]
    return abs(s) * 0.5


def _union_area(shapes, lines=801):
    fills = [s for s in shapes if s.filled]
    if not fills: return 0.0
    ys = [q[1] for s in fills for q in s.pts]
    y0, y1 = min(ys), max(ys)
    if y1 - y0 < 1e-9: return 0.0
    tot = 0.0
    import headroom as H
    for k in range(lines):
        y = y0 + (y1 - y0) * k / (lines - 1)
        sp = []
        for s in fills: sp.extend(H._poly_spans(s.pts, y))
        tot += sum(b - a for a, b in H._merge(sp))
    return tot * (y1 - y0) / (lines - 1)


def aspect(shapes):
    x0, y0, x1, y1 = rig.bounds([q for s in shapes for q in s.pts])
    w, h = x1 - x0, y1 - y0
    return (w / h) if h > 0 else 0.0


def pairwise_table(table, anchor_y=0.0):
    ks = list(table)
    pr = {k: profile(table[k], anchor_y) for k in ks}
    rows = []
    for i in range(len(ks)):
        for j in range(i + 1, len(ks)):
            d = rig.max_delta(pr[ks[i]], pr[ks[j]])
            rows.append((d, ks[i], ks[j]))
    rows.sort()
    return rows


if __name__ == "__main__":
    import items, hair
    print("W@0.75 = %.5f R   W@0.60 = %.5f R" % (W075, W060))
    print("실루엣 하한 %.5f R (= %.2f획@0.75 = %.2f획@0.60)"
          % (SILHOUETTE_FLOOR_R, SILHOUETTE_FLOOR_R / W075, SILHOUETTE_FLOOR_R / W060))
    print("래칫       %.5f R (= %.2f획@0.75 = %.2f획@0.60)"
          % (SILHOUETTE_RATCHET_R, SILHOUETTE_RATCHET_R / W075, SILHOUETTE_RATCHET_R / W060))
    CAPS = {"HEAD": 2.551, "EYES": 1.60, "NECK": 2.40, "BACK": 3.60}
    for cat, table, anchor in (("HEAD", items.HEAD, 0.0), ("EYES", items.EYES, 0.0),
                               ("NECK", items.NECK, rig.SHOULDER_R), ("BACK", items.BACK, rig.SHOULDER_R)):
        env = envelope(table, anchor)
        print()
        print("── %s 봉투(72구간 최대반경 R) ──" % cat)
        for row in range(0, BINS, 12):
            print("   %3d°~ " % (row * BIN_DEG) + " ".join("%5.2f" % env[row + k] for k in range(12)))
        secs = free_sectors(table, anchor, reach_cap=CAPS[cat])
        runs = merge_runs(secs)
        print("   빈 대역(연속 %d구간 이상, 도달상한 %.2fR, 여유 %.3fR 확보 가능):"
              % (SECTOR_MIN_BINS, CAPS[cat], SECTOR_CLEARANCE_R))
        if not runs: print("      없음")
        for a, b in runs:
            lo = min(env[i % BINS] for i in range(a, b + SECTOR_MIN_BINS))
            hi = max(env[i % BINS] for i in range(a, b + SECTOR_MIN_BINS))
            print("      %+4d° ~ %+4d°  (폭 %2d구간)  봉투 %.2f~%.2f R  여유 %.2f R"
                  % (a * BIN_DEG, (b + SECTOR_MIN_BINS) * BIN_DEG, b - a + SECTOR_MIN_BINS,
                     lo, hi, CAPS[cat] - hi))
        print("   쌍별 최소 3쌍:")
        for d, a, b in pairwise_table(table, anchor)[:3]:
            print("      %.4f R = %.2f획@0.75 = %.2f획@0.60   %s vs %s" % (d, d / W075, d / W060, a, b))
        print("   등급 지표:")
        for n, sh in table.items():
            pg, rmax = prongs(sh, anchor)
            print("      %-6s 돌출 %d개%-22s r_max %.2fR  볼록결손 %.3f  종횡비 %.2f"
                  % (n, len(pg), " " + ",".join("%d°" % d for d, _, _ in pg),
                     rmax, convex_deficiency(sh), aspect(sh)))
