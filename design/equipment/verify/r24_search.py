# -*- coding: utf-8 -*-
"""R24 탐색 — 천모자·중절모의 「앞층만」 H-2 재맞춤 (u, ky, dy) 격자 탐색. 설계 탐색 전용.
설치 뒤 판정은 r24_hats.py / r23_crown.py (프로덕션 직접 파싱) 가 한다.
"""
import math, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r17_model as M17

HEAD_R_COVER = M17.HEAD_R_COVER
MARGIN = M17.COVER_MARGIN
TOP_LIMIT = M17.TOP_LIMIT
CHIN = M17.CHIN
FURHAT_TOP = 2.5439


def lens_split_icon(pts, y_cut):
    """렌즈꼴(33점: 왼끝0 · 위호1..15 · 오른끝16 · 아래호17..31 · 왼끝32중복)을 아이콘 y=y_cut 에서 자른다.
    아이콘 y 는 아래로 커진다 → y ≤ y_cut = 위(먼 쪽) / y ≥ y_cut = 아래(가까운 쪽)."""
    p = pts[:32] if len(pts) == 33 else list(pts)
    eps = 1e-9
    up = [i for i in range(0, 17) if p[i][1] <= y_cut + eps]
    if not up:
        raise SystemExit("자르는 선이 렌즈 위쪽 밖이다")
    a, b = up[0], up[-1]

    def cross(i, j):
        (x1, y1), (x2, y2) = p[i], p[j]
        if abs(y2 - y1) < 1e-12:
            return (x1, y_cut)
        t = (y_cut - y1) / (y2 - y1)
        return (x1 + (x2 - x1) * t, y_cut)

    x1 = p[a] if a == 0 else cross(a - 1, a)
    x2 = p[b] if b == 16 else cross(b, b + 1)
    far = [x1] + p[a:b + 1] + [x2]
    near = [x2] + p[b + 1:32] + [p[0]] + [x1] if b < 16 else [p[16]] + p[17:32] + [p[0]]
    # 중복 점 제거
    def dedup(q):
        o = []
        for pt in q:
            if not o or (abs(pt[0] - o[-1][0]) > 1e-9 or abs(pt[1] - o[-1][1]) > 1e-9):
                o.append(pt)
        return o
    return dedup(far), dedup(near)


def front_polys_icon(kind):
    """앞층에 남는 불투명 아이콘 조각: B0(관) · F1(그늘/띠) · 챙의 가까운 쪽."""
    icon = M17._icon_polys(kind)
    srcs = ["%s%d" % (pc.call, i) for i, pc in enumerate(icon)]
    by = dict(zip(srcs, icon))
    crown_bottom = max(q[1] for q in by["B0"].pts)          # 관 밑변(= 관·챙 이음선)
    far, near = lens_split_icon(by["B2"].pts, crown_bottom)
    out = [by["B0"].pts, by["F1"].pts, near]
    return [q for q in out], far, crown_bottom


def central_hw(polys, ys):
    out = np.zeros(len(ys))
    for j, y in enumerate(ys):
        iv = []
        for poly in polys:
            iv += M17._scan_intervals(poly, y)
        if not iv:
            continue
        iv.sort(); merged = [list(iv[0])]
        for a, b in iv[1:]:
            if a <= merged[-1][1] + 1e-9: merged[-1][1] = max(merged[-1][1], b)
            else: merged.append([a, b])
        for a, b in merged:
            if a <= 32.0 <= b:
                out[j] = min(32.0 - a, b - 32.0); break
    return out


def evaluate(kind, polys, chw, ys, u, dy, ky, w_max=0.30):
    y_R = dy - ys * u * ky
    allpts = [q for poly in polys for q in poly]
    x0 = min(q[0] for q in allpts); x1 = max(q[0] for q in allpts)
    icon_all = [q for pc in M17._icon_polys(kind) for q in pc.pts]
    top = dy - min(q[1] for q in icon_all) * u * ky
    bottom = dy - max(q[1] for q in icon_all) * u * ky
    head_hw = np.sqrt(np.clip(HEAD_R_COVER ** 2 - y_R ** 2, 0.0, None))
    need = (y_R <= HEAD_R_COVER) & (y_R >= -HEAD_R_COVER)
    ok_line = (chw * u >= head_hw + MARGIN) | ~need
    order = np.argsort(-y_R)
    wear, started = None, False
    for j in order:
        if y_R[j] > HEAD_R_COVER: continue
        if not ok_line[j]: break
        wear = y_R[j]; started = True
    covered_top = started and (max(y_R[(y_R <= HEAD_R_COVER) & ok_line]) >= HEAD_R_COVER - 0.3)
    why = []
    if wear is None or not covered_top: why.append("덮임 실패")
    elif wear > w_max: why.append("착용선 %.3f > %.2f" % (wear, w_max))
    if top >= TOP_LIMIT: why.append("꼭대기 %.3f ≥ %.3f" % (top, TOP_LIMIT))
    if bottom <= CHIN: why.append("밑 %.3f ≤ %.1f" % (bottom, CHIN))
    return dict(u=u, dy=dy, ky=ky, wear=wear, top=top, bottom=bottom,
                width=(x1 - x0) * u, ok=not why, why=" · ".join(why))


def fit(kind, w_max=0.30, kys=(1.0,), top_cap=None, us=(0.045, 0.150, 0.001)):
    """목적함수 = **앞층 잉크 최하단(bottom) 최대** — 그것이 안경 가려짐을 직접 정한다.
    동률(0.005 R 안)이면 더 작은 u(더 작은 모자)."""
    polys, far, cb = front_polys_icon(kind)
    ys = np.arange(0.0, 64.0, 0.25)
    chw = central_hw(polys, ys)
    cands = []
    for ky in kys:
        for u in np.arange(*us):
            for dy in np.arange(0.0, 4.6, 0.02):
                e = evaluate(kind, polys, chw, ys, float(u), float(dy), ky, w_max)
                if e["ok"] and (top_cap is None or e["top"] <= top_cap):
                    cands.append(e)
    if not cands:
        return None, cb
    bmax = max(e["bottom"] for e in cands)
    near = [e for e in cands if e["bottom"] >= bmax - 0.005]
    return min(near, key=lambda e: (round(e["u"], 4), -e["wear"])), cb


if __name__ == "__main__":
    # ★ 이 탐색은 **채택되지 않았다** — 기록용이다. 결과 요약(2026-09-06):
    #     천모자 u 0.0890 ky 1.00 dy 4.260 → 착용선 +0.299 · 밑 +0.166 · **폭 4.45 R**(현행 3.65)
    #     중절모 ky=1 로는 밑단이 **−0.484** 까지 내려가 안경을 지운다(u 0.0990 · 폭 5.15 R).
    #            ky 0.80·u 0.0930 이라야 밑 +0.200 인데 **폭 4.84 R**(현행 4.06) + 높은 관을 세로로
    #            0.8 배 눌러야 한다 — 중절모의 정체(높은 관)가 무너진다.
    #   그래서 u·dy 는 손대지 않고 **관 x 배수만** 거는 안을 채택했다(§14-14-2). 이 파일은 그 판단의 근거다.
    for k in ("clothhat", "fedora"):
        f = M17.HAT_FIT[k]
        print("%-9s 현행 u %.4f ky %.2f dy %.3f (전층 착용선 %+.3f · 꼭대기 %+.3f · 밑 %+.3f · 폭 %.3f)"
              % (k, f["u"], f["ky"], f["dy"], f["wear"], f["top"], f["bottom"], f["width"]))
        for label, kys, cap in (("ky=1 · 털모자 아래", (1.0,), FURHAT_TOP),
                                ("ky 0.80~1.0 · 털모자 아래", (1.0, 0.95, 0.90, 0.85, 0.80), FURHAT_TOP)):
            b, cb = fit(k, kys=kys, top_cap=cap, us=(0.075, 0.101, 0.002))
            if b:
                print("   %-24s u %.4f (×%.3f) ky %.2f dy %.3f · 착용선 %+.3f · 꼭대기 %+.3f · 밑 %+.3f · 폭 %.3f"
                      % (label, b["u"], b["u"] / f["u"], b["ky"], b["dy"], b["wear"], b["top"], b["bottom"], b["width"]))
            else:
                print("   %-24s 실패" % label)
        print("          관 밑변 아이콘 y = %.2f" % cb)
