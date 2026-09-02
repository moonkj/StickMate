# -*- coding: utf-8 -*-
"""R3 ⑦ P-1 의 근거 — HEAD+EYES+HAIR **3중 착용** 남는 머리 예산 216조합(6x6x6).

지금 게이트는 아이템을 **하나씩** 잰다. headroom 하한(면적 12% / 두께 1.00획)은 **모자 단독**에만
걸려 있고, 안경과 머리카락이 같은 원반을 더 먹는 것은 아무도 안 본다.

★ 머리카락 자르기는 프로덕션 결과(@CLIP: 살아남은 도형 수 + 꼭대기 y)로 **교정한 뒤** 쓴다.
"""
import sys, os, subprocess, tempfile
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import headroom
import r3_prod as P, r3_raster as RS

CATS, COVER, W, RAR, LOG = P.dump()
HATS = ["야구모자", "털모자", "중절모", "왕관", "베레모", "밀짚모자"]
EYES = ["선글라스", "동그란안경", "고글", "외알안경", "뿔테안경", "안대"]
HAIR = ["삐친머리", "단정한머리", "곱슬머리", "민머리", "바가지머리", "포니테일"]
HIDX = {n: i for i, n in enumerate(HATS)}
AREA_FLOOR = headroom.HEADROOM_AREA_FLOOR          # 0.12
THICK_FLOOR = headroom.HEADROOM_THICKNESS_FLOOR_W  # 1.00

H = 0.0015
X0, X1, Y0, Y1 = -2.60, 2.60, -1.35, 2.20


def clipped_hair(hat, hair):
    out = []
    cov = COVER[HIDX[hat]]
    for s in CATS["HAIR"][hair]:
        pts = headroom.clip_below(s.pts, cov) if s.loop else [p for p in s.pts if p[1] <= cov]
        if len(pts) >= 3:
            out.append(P.PShape(s.name, s.loop, s.filled, s.tone, s.sort, pts))
    return out


# --------------------------------------------------------------- 교정: @CLIP 대조
def clip_calibration():
    env = dict(os.environ); env["SHAPEDUMP_OUT"] = tempfile.mkdtemp(prefix="sd_")
    raw = subprocess.run(["/Users/kjmoon/App/StickMate/Tools/ShapeDump/build.sh"],
                         capture_output=True, text=True, env=env).stdout
    want = {}
    for line in raw.splitlines():
        f = line.split("\t")
        if f[0] == "@CLIP":
            want[(f[1], f[2])] = (int(f[3]), None if f[4] == "-" else float(f[4]))
    bad = 0
    for hat in HATS:
        for hair in HAIR:
            got = clipped_hair(hat, hair)
            n_w, top_w = want[(hat, hair)]
            top_g = max((y for s in got for _, y in s.pts), default=None)
            okn = len(got) == n_w
            okt = top_g is not None and abs(top_g - top_w) <= 0.002
            if not (okn and okt):
                bad += 1
                print("  ✗ %s+%s 도형 %d(want %d) 꼭대기 %.3f(want %.3f)"
                      % (hat, hair, len(got), n_w, top_g if top_g is not None else -9, top_w))
    print("== 머리카락 자르기 교정 %d/36 (프로덕션 @CLIP 대조) ==" % (36 - bad))
    if bad:
        print("★ 교정이 깨졌다 — 종료."); sys.exit(1)


_M = {}
def M(key, shapes, w=W):
    if key not in _M:
        _M[key] = RS.mask_of(shapes, w, X0, X1, Y0, Y1, H)[0]
    return _M[key]

nx = int(round((X1 - X0) / H)) + 1
ny = int(round((Y1 - Y0) / H)) + 1
xs = X0 + H * np.arange(nx); ys = Y0 + H * np.arange(ny)
XX, YY = np.meshgrid(xs, ys)
DISC = (XX**2 + YY**2) <= 1.0
COL0 = int(round((0.0 - X0) / H))          # x=0 열


def budget(ink):
    """남는 머리 (면적비, x=0 두께 획)."""
    free = DISC & ~ink
    area = free.sum() / DISC.sum()
    col = ink[:, COL0] & DISC[:, COL0]
    if not col.any():
        return area, 2.0 / W * 1.0     # 잉크가 x=0을 안 지나면 지름 전체가 남는다
    lo = ys[np.nonzero(col)[0].min()]
    return area, (lo - (-1.0)) * 2.0 / (2.0) * 2.0 / W * 1.0 if False else (lo + 1.0) / W


if __name__ == "__main__":
    clip_calibration()
    # 교정: 모자 단독 6종이 HAT_HEADROOM_PRESCRIPTION §4 after 열과 맞는가
    WANT = {"야구모자": (29.2, 2.01), "털모자": (25.2, 1.65), "중절모": (24.0, 1.68),
            "왕관": (34.1, 2.12), "베레모": (38.2, 2.38), "밀짚모자": (24.0, 1.68)}
    bad = 0
    for h in HATS:
        a, t = budget(M(("H", h), CATS["HEAD"][h]))
        aw, tw = WANT[h]
        ok = abs(a * 100 - aw) <= 0.4 and abs(t - tw) <= 0.03
        if not ok: bad += 1; print("  ✗ %s 면적 %.1f(want %.1f) 두께 %.2f(want %.2f)" % (h, a*100, aw, t, tw))
    print("== 래스터 headroom 교정 %d/6 (처방 §4 after 열) ==" % (6 - bad))
    if bad: print("★ 교정이 깨졌다 — 종료."); sys.exit(1)

    print("\n" + "=" * 88)
    print("⑦ HEAD+EYES+HAIR 216조합 남는 머리 (하한 면적 %.0f%% / 두께 %.2f획)"
          % (AREA_FLOOR * 100, THICK_FLOOR))
    print("=" * 88)
    rows = []
    for h in HATS:
        mh = M(("H", h), CATS["HEAD"][h])
        for e in EYES:
            me = M(("E", e), CATS["EYES"][e])
            for r in HAIR:
                mr = M(("R", h, r), clipped_hair(h, r))
                a, t = budget(mh | me | mr)
                rows.append((a, t, h, e, r))
    fa = [x for x in rows if x[0] < AREA_FLOOR]
    ft = [x for x in rows if x[1] < THICK_FLOOR]
    both = [x for x in rows if x[0] < AREA_FLOOR or x[1] < THICK_FLOOR]
    print("  면적 미달 %d/216   두께 미달 %d/216   둘 중 하나라도 미달 %d/216"
          % (len(fa), len(ft), len(both)))
    rows.sort()
    print("\n  -- 최악 12 --")
    for a, t, h, e, r in rows[:12]:
        print("   %-28s 면적 %5.1f%%  두께 %5.2f획  %s"
              % (h + "+" + e + "+" + r, a * 100, t,
                 "✗" if (a < AREA_FLOOR or t < THICK_FLOOR) else ""))
    print("\n  -- 최고 5 --")
    for a, t, h, e, r in rows[-5:]:
        print("   %-28s 면적 %5.1f%%  두께 %5.2f획" % (h + "+" + e + "+" + r, a * 100, t))
    # 모자별 요약
    print("\n  -- 모자별 (36조합씩) --")
    for h in HATS:
        sub = [x for x in rows if x[2] == h]
        n = sum(1 for x in sub if x[0] < AREA_FLOOR or x[1] < THICK_FLOOR)
        print("   %-10s 미달 %2d/36   면적 %5.1f~%5.1f%%   두께 %4.2f~%4.2f획"
              % (h, n, min(x[0] for x in sub)*100, max(x[0] for x in sub)*100,
                 min(x[1] for x in sub), max(x[1] for x in sub)))
