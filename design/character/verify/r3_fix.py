# -*- coding: utf-8 -*-
"""R3 ③ 36조합을 **고칠 수 있는 형태**로 — 간판 도형 생존 / 필요한 이동량 / 무엇이 깨지는가.

정의(전부 계산으로 유도. 취향으로 고른 수치가 아니다):
 · 「간판 도형」 = 그 안경의 **잉크 면적이 가장 큰 채운 도형**. 코다리/줄/체인이 아니라
   그 아이템을 그 아이템이게 하는 판. 어느 것인지도 여기서 **재서** 고른다.
 · 「존재한다」  = 남은 잉크의 최대 내접 지름 >= 1.00 획.
   ★ 이 문턱은 내가 만든 것이 아니다 — headroom.HEADROOM_THICKNESS_FLOOR_W 그대로다.
     그 상수의 교정은 **사용자 신고**다(털모자 0.55획 신고 / 야구 1.19획 미신고).

이동은 **격자 행 단위 정수 이동**으로 한다(δ = k*h). 다시 래스터화하지 않으므로 빠르고,
같은 마스크를 미끄러뜨리는 것이라 이동 전후가 정확히 같은 자로 재진다.
"""
import math, sys, os
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import headroom, rig
import r3_prod as P, r3_raster as RS
from r3_combo import CATS, COVER, W, HATS, EYES, H, X0, X1, Y0, Y1

FLOOR = headroom.HEADROOM_THICKNESS_FLOOR_W          # 1.00 — 남의 상수, 내가 안 정했다
LEVEL_HEAD = {"야구모자":1, "털모자":5, "중절모":9, "왕관":20, "베레모":23, "밀짚모자":26}
LEVEL_EYES = {"선글라스":1, "동그란안경":6, "고글":11, "외알안경":15, "뿔테안경":19, "안대":23}

PAD = 500                                            # 위아래 여유 행(이동용)
YY0, YY1 = Y0 - PAD * H, Y1 + PAD * H

_M = {}
def mask(cat, name, shapes=None):
    if (cat, name) not in _M:
        sh = shapes if shapes is not None else CATS[cat][name]
        m, xs, ys = RS.mask_of(sh, W, X0, X1, YY0, YY1, H)
        _M[(cat, name)] = (m, xs, ys)
    return _M[(cat, name)]


def shift_rows(m, k):
    """마스크를 k 행 위로(양수) 미끄러뜨린다. 밖은 False."""
    if k == 0: return m
    out = np.zeros_like(m)
    if k > 0: out[k:, :] = m[:-k, :]
    else:     out[:k, :] = m[-k:, :]
    return out


def remainder(hat, eye, kh=0, ke=0, sub=None):
    mh, _, _ = mask("HEAD", hat)
    if sub is None:
        me, _, _ = mask("EYES", eye)
    else:
        me, _, _ = mask("SIGN", eye)
    return shift_rows(me, ke) & ~shift_rows(mh, kh)


def thick(hat, eye, kh=0, ke=0, sub=None):
    return RS.thickness_W(remainder(hat, eye, kh, ke, sub), H, W)


# ---------------------------------------------------------------- 간판 도형
def signboard(eye):
    best = (0.0, None, None)
    for s in CATS["EYES"][eye]:
        if not s.filled: continue
        m, _, _ = RS.mask_of([s], W, X0, X1, YY0, YY1, H)
        a = m.sum() * H * H
        if a > best[0]: best = (a, s.name, m)
    return best

SIGN = {}
for e in EYES:
    a, nm, m = signboard(e)
    SIGN[e] = (nm, a); _M[("SIGN", e)] = (m, None, None)


def sign_vis(hat, eye, kh=0, ke=0):
    ms, _, _ = mask("SIGN", eye)
    mv = shift_rows(ms, ke) & ~shift_rows(mask("HEAD", hat)[0], kh)
    tot = ms.sum()
    return (mv.sum() / tot if tot else 0.0), RS.thickness_W(mv, H, W)


def needed(hat, eye, axis, kmax=470):
    """thickness >= FLOOR 를 만족하는 최소 이동 행수 k (δ = k*H). 없으면 None."""
    def ok(k):
        return thick(hat, eye, kh=(k if axis == "hat" else 0),
                              ke=(-k if axis == "eye" else 0)) >= FLOOR
    if ok(0): return 0
    if not ok(kmax): return None
    lo, hi = 0, kmax
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if ok(mid): hi = mid
        else: lo = mid
    return hi


if __name__ == "__main__":
    print("=" * 100)
    print("③-1 간판 도형 — 그 안경을 그 안경이게 하는 판이 살아남는가")
    print("=" * 100)
    for e in EYES:
        print("  %-10s 간판 = %-18s 잉크면적 %.4f R^2" % (e, SIGN[e][0], SIGN[e][1]))
    print("\n-- 간판 도형 가시율 % (괄호 = 남은 두께 획) --")
    print("%-10s" % "" + "".join("%15s" % e for e in EYES))
    for hn in HATS:
        row = "%-10s" % hn
        for en in EYES:
            v, t = sign_vis(hn, en)
            row += "%10.0f%%(%4.2f)" % (v * 100, t)
        print(row)

    print("\n" + "=" * 100)
    print("③-2 판정 — 「존재하는가」 (남은 잉크 최대 내접 지름 >= %.2f 획)" % FLOOR)
    print("=" * 100)
    fail = []
    print("%-10s" % "" + "".join("%9s" % e for e in EYES))
    T = {}
    for hn in HATS:
        row = "%-10s" % hn
        for en in EYES:
            t = thick(hn, en); T[(hn, en)] = t
            if t < FLOOR: fail.append((hn, en, t))
            row += "%9s" % ("✗%.2f" % t if t < FLOOR else "  %.2f" % t)
        print(row)
    print("\n  ★ 미달 %d / 36 조합" % len(fail))
    for hn, en, t in sorted(fail, key=lambda r: max(LEVEL_HEAD[r[0]], LEVEL_EYES[r[1]])):
        lv = max(LEVEL_HEAD[hn], LEVEL_EYES[en])
        print("   %-20s %5.2f획   Lv%-3d 에서 처음 만난다 (모자 Lv%d / 안경 Lv%d)"
              % (hn + "+" + en, t, lv, LEVEL_HEAD[hn], LEVEL_EYES[en]))

    print("\n" + "=" * 100)
    print("③-3 어느 쪽을 고치면 몇 조합이 살아나는가 — 필요한 최소 이동량")
    print("=" * 100)
    n1, n2 = {}, {}
    print("%-22s %14s %14s" % ("조합", "모자↑ δ(R/획)", "안경↓ δ(R/획)"))
    for hn, en, t in fail:
        a = needed(hn, en, "hat"); b = needed(hn, en, "eye")
        n1[(hn, en)] = a; n2[(hn, en)] = b
        f = lambda k: "불가" if k is None else "%.3f/%.2f획" % (k * H, k * H / W)
        print("%-22s %14s %14s" % (hn + "+" + en, f(a), f(b)))

    print("\n-- 아이템 하나를 고치면 그 행/열 6조합이 전부 낫는가 --")
    print("  [L1] 모자를 위로:")
    for hn in HATS:
        ds = [n1[(hn, en)] for en in EYES if (hn, en) in n1]
        if not ds: print("    %-10s 손댈 필요 없음" % hn); continue
        if any(d is None for d in ds): print("    %-10s 세로 이동으로는 불가" % hn); continue
        d = max(ds) * H
        print("    %-10s δ = %.3f R (%.2f획) → %d조합 해소" % (hn, d, d / W, len(ds)))
    print("  [L2] 안경을 아래로:")
    for en in EYES:
        ds = [n2[(hn, en)] for hn in HATS if (hn, en) in n2]
        if not ds: print("    %-10s 손댈 필요 없음" % en); continue
        if any(d is None for d in ds): print("    %-10s 세로 이동으로는 불가" % en); continue
        d = max(ds) * H
        print("    %-10s δ = %.3f R (%.2f획) → %d조합 해소" % (en, d, d / W, len(ds)))
