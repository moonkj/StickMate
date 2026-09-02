# -*- coding: utf-8 -*-
"""R3 ⑤ 판정 기준 개정 + 처방의 **부작용**.

★ ③-2 의 판정에는 구멍이 있었다 — 내가 만든 게이트가 스스로 거짓 통과를 낸다.
   「남은 잉크 전체의 최대 내접 지름」은 **끈 하나만 살아도 정확히 1.00획**을 준다.
   안대(PatchStrap)와 외알안경(MonocleChain)이 그 형태로 통과했다:
     안대 @중절모  = 조합 1.00획 통과인데 간판(PatchCover)은 12% / 0.56획
     외알안경@야구  = 조합 1.07획 통과인데 간판(MonoclePod)은  3% / 0.22획
   그래서 판정을 **면(채움)에서 온 잉크만**으로 바꾼다. 선은 그 아이템이 아니다.
"""
import math, sys, os
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import headroom, rig
import r3_prod as P, r3_raster as RS
from r3_combo import CATS, COVER, W, HATS, EYES, H, X0, X1, Y0, Y1

FLOOR = headroom.HEADROOM_THICKNESS_FLOOR_W
CEIL  = 1.80                              # CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR
R_INK = 1.171932                          # 머리 잉크 바깥 반경
LEVEL_HEAD = {"야구모자":1, "털모자":5, "중절모":9, "왕관":20, "베레모":23, "밀짚모자":26}
LEVEL_EYES = {"선글라스":1, "동그란안경":6, "고글":11, "외알안경":15, "뿔테안경":19, "안대":23}

PAD = 500
YY0, YY1 = Y0 - PAD * H, Y1 + PAD * H

_M = {}
def M(key, shapes):
    if key not in _M:
        _M[key] = RS.mask_of(shapes, W, X0, X1, YY0, YY1, H)[0]
    return _M[key]

def hat_m(h):  return M(("H", h), CATS["HEAD"][h])
def eye_m(e):  return M(("E", e), CATS["EYES"][e])
def eyefill_m(e):
    return M(("EF", e), [s for s in CATS["EYES"][e] if s.filled])

def shift(m, k):
    if k == 0: return m
    o = np.zeros_like(m)
    if k > 0: o[k:, :] = m[:-k, :]
    else:     o[:k, :] = m[-k:, :]
    return o

def thick_fill(h, e, kh=0, ke=0):
    return RS.thickness_W(shift(eyefill_m(e), ke) & ~shift(hat_m(h), kh), H, W)

def vis_fill(h, e, kh=0, ke=0):
    ms = shift(eyefill_m(e), ke); mv = ms & ~shift(hat_m(h), kh)
    return mv.sum() / ms.sum()

def needed(h, e, kmax=470):
    if thick_fill(h, e) >= FLOOR: return 0
    if thick_fill(h, e, ke=-kmax) < FLOOR: return None
    lo, hi = 0, kmax
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if thick_fill(h, e, ke=-mid) >= FLOOR: hi = mid
        else: lo = mid
    return hi


if __name__ == "__main__":
    print("=" * 100)
    print("⑤-1 개정 판정 — **면에서 온 잉크만**의 최대 내접 지름 >= %.2f 획" % FLOOR)
    print("=" * 100)
    print("%-10s" % "" + "".join("%11s" % e for e in EYES))
    fail = []
    for h in HATS:
        row = "%-10s" % h
        for e in EYES:
            t = thick_fill(h, e); v = vis_fill(h, e)
            if t < FLOOR: fail.append((h, e, t, v))
            row += "%11s" % (("✗%.2f" % t) if t < FLOOR else ("  %.2f" % t))
        print(row)
    print("\n  ★ 미달 %d / 36 조합 (전체 판정 기준으로는 12건이었다)" % len(fail))
    print("  -- 미달 조합 · 면 가시율 · 해금 레벨 --")
    for h, e, t, v in sorted(fail, key=lambda r: max(LEVEL_HEAD[r[0]], LEVEL_EYES[r[1]])):
        print("   %-20s %5.2f획  면 가시율 %4.0f%%   Lv%-3d"
              % (h + "+" + e, t, v * 100, max(LEVEL_HEAD[h], LEVEL_EYES[e])))

    print("\n" + "=" * 100)
    print("⑤-2 처방 — 안경을 아래로 δ. 아이템별 필요량")
    print("=" * 100)
    need = {}
    for h, e, t, v in fail:
        need[(h, e)] = needed(h, e)
    per_eye = {}
    for e in EYES:
        ds = [need[(h, e)] for h in HATS if (h, e) in need]
        if not ds: per_eye[e] = 0; print("    %-10s 손댈 필요 없음" % e); continue
        if any(d is None for d in ds): per_eye[e] = None; print("    %-10s 이동으로는 불가" % e); continue
        per_eye[e] = max(ds)
        print("    %-10s δ = %.3f R (%.2f획) → %d조합 해소" % (e, max(ds)*H, max(ds)*H/W, len(ds)))
    per_hat = {}
    print("  [대조] 모자를 위로 δ (같은 조합을 반대쪽에서):")
    for h in HATS:
        ds = [need[(hh, e)] for (hh, e) in need if hh == h]
        if not ds: per_hat[h] = 0; print("    %-10s 손댈 필요 없음" % h); continue
        if any(d is None for d in ds): per_hat[h] = None; print("    %-10s 불가" % h); continue
        per_hat[h] = max(ds)
        print("    %-10s δ = %.3f R (%.2f획) → %d조합 해소" % (h, max(ds)*H, max(ds)*H/W, len(ds)))

    print("\n" + "=" * 100)
    print("⑤-3 ★ 고치면 무엇이 깨지는가")
    print("=" * 100)

    # (1) 안경을 내렸을 때 — 머리 잉크 원반(반경 %.4f R) 밖으로 얼마나 나가는가
    print("\n(1) 안경을 δ 내리면 머리 잉크 원반(반경 %.4f R) 밖으로 나가는 잉크 비율" % R_INK)
    nx = eyefill_m(EYES[0]).shape[1]
    xs = X0 + H * np.arange(nx)
    ys = YY0 + H * np.arange(eyefill_m(EYES[0]).shape[0])
    XX, YY = np.meshgrid(xs, ys)
    outside = (XX**2 + YY**2) > R_INK**2
    for e in EYES:
        k = per_eye.get(e, 0)
        if not k: continue
        m0 = eye_m(e); m1 = shift(m0, -k)
        o0 = (m0 & outside).sum() / m0.sum(); o1 = (m1 & outside).sum() / m1.sum()
        ybot0 = ys[np.nonzero(m0)[0].min()]; ybot1 = ys[np.nonzero(m1)[0].min()]
        print("    %-10s δ=%.3f R  머리 밖 잉크 %4.1f%% → %4.1f%%   잉크 밑단 %+.3f → %+.3f R (머리 밑단 %.3f)"
              % (e, k*H, o0*100, o1*100, ybot0, ybot1, -R_INK))

    # (2) 규칙 4 금지 구간 — 모자 잉크와 안경 잉크 사이 간격이 0 < gap < 1획 이면 최악
    print("\n(2) 규칙 4(0 < 간격 < 1획 = 금지) — 처방 후 모자↔안경 잉크 간격")
    print("    %-10s" % "" + "".join("%11s" % e for e in EYES))
    for h in HATS:
        row = "    %-10s" % h
        for e in EYES:
            k = per_eye.get(e, 0) or 0
            mh = hat_m(h); me = shift(eye_m(e), -k)
            if (mh & me).any():
                row += "%11s" % "겹침"
            else:
                d = RS.edt(~mh, H)
                g = float(d[me].min())
                row += "%11s" % (("★%.2f획" % (g/W)) if g < W else ("%.2f획" % (g/W)))
        print(row)

    # (3) 실루엣 구분(프로덕션 자 = 중심선 프로파일, anchor 0) — 처방 전/후
    print("\n(3) EYES 슬롯 내 실루엣 차 (프로덕션 AccessorySilhouetteDistinctionTests 와 같은 자, 문턱 >1.00획)")
    def prof(e, dy):
        return rig.profile([rig.Shape(s.name, [(x, y + dy) for x, y in s.pts], loop=s.loop,
                                      filled=s.filled) for s in CATS["EYES"][e]], 0.0)
    for tag, dys in (("처방 전", {e: 0.0 for e in EYES}),
                     ("처방 후", {e: -(per_eye.get(e, 0) or 0) * H for e in EYES})):
        ps = {e: prof(e, dys[e]) for e in EYES}
        worst = (9e9, None)
        for i in range(6):
            for j in range(i+1, 6):
                d = rig.max_delta(ps[EYES[i]], ps[EYES[j]])
                if d < worst[0]: worst = (d, (EYES[i], EYES[j]))
        print("    %-6s 최소차 %.2f획  (%s~%s)  %s" % (tag, worst[0]/W, worst[1][0], worst[1][1],
                                                  "OK" if worst[0] > W else "★ 미달"))

    # (4) 모자를 올렸을 때 — 천장 / 남는 머리(HAT_HEADROOM_PRESCRIPTION §4 after 열이 흔들린다)
    print("\n(4) [대조] 모자를 δ 올리면 — 천장 %.2f R 과 남는 머리(내 교정 24값의 출처)" % CEIL)
    for h in HATS:
        k = per_hat.get(h, 0)
        top0 = max(y for s in CATS["HEAD"][h] for _, y in s.pts) + W/2
        sh0 = CATS["HEAD"][h]
        m0 = headroom.measure(sh0, W)
        if k:
            sh1 = [s.shifted(0, k*H) for s in sh0]
            m1 = headroom.measure(sh1, W)
            print("    %-10s δ=%.3f R  천장 %.3f→%.3f (한계 %.2f)  면적 %.1f%%→%.1f%%  두께 %.2f→%.2f획"
                  % (h, k*H, top0, top0+k*H, CEIL, m0['area']*100, m1['area']*100,
                     m0['depth']*2/W, m1['depth']*2/W))
        else:
            print("    %-10s 손댈 필요 없음  (천장 %.3f, 면적 %.1f%%, 두께 %.2f획)"
                  % (h, top0, m0['area']*100, m0['depth']*2/W))
