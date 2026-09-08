#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 — 카드 비트맵 12장의 실측 색 vs 현행 카탈로그 색(icon tone0/tone1) vs 몸에 칠해지는 WornColor.

  python3 pack_bitmap_r3_colors.py            # 표
  python3 pack_bitmap_r3_colors.py --dump     # 클러스터 전문
재현: PIL + numpy 만 쓴다. Unity 미실행.
"""
import os, re, sys, math, colorsys
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
ICONS = os.path.join(ROOT, 'Assets/_Project/Resources/Items/Icons')
ITEMS = os.path.join(ROOT, 'Assets/_Project/Resources/Items')

NAMES = [
    'pack_cyber_head_patched_hood', 'pack_cyber_eyes_slit_visor',
    'pack_cyber_neck_cable_collar', 'pack_cyber_back_tarp_cape',
    'pack_mine_head_miner_helmet', 'pack_mine_eyes_dust_goggles',
    'pack_mine_neck_mine_lamp', 'pack_mine_back_pick_harness',
    'pack_arcane_head_wizard_hat', 'pack_arcane_eyes_astro_lens',
    'pack_arcane_neck_moon_clasp', 'pack_arcane_back_moon_robe',
]

# ---------- 프로덕션 식 그대로 (ItemCatalog.WornColor) ----------
WORN_S_FLOOR = 0.42
WORN_V_FLOOR = 0.55
WORN_V_CEIL  = 0.80

def worn_color(rgb01):
    h, s, v = colorsys.rgb_to_hsv(*rgb01)
    s = max(s, WORN_S_FLOOR)
    v = min(max(v, WORN_V_FLOOR), WORN_V_CEIL)
    return colorsys.hsv_to_rgb(h, s, v)

def hexs(rgb01):
    return '#%02X%02X%02X' % tuple(int(round(max(0.0, min(1.0, c)) * 255)) for c in rgb01)

# ---------- CIE Lab / ΔE2000 ----------
def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def rgb_to_lab(rgb01):
    r, g, b = (srgb_to_lin(c) for c in rgb01)
    x = r*0.4124564 + g*0.3575761 + b*0.1804375
    y = r*0.2126729 + g*0.7151522 + b*0.0721750
    z = r*0.0193339 + g*0.1191920 + b*0.9503041
    xn, yn, zn = 0.95047, 1.0, 1.08883
    def f(t):
        return t ** (1.0/3.0) if t > 216/24389 else (841/108) * t + 4/29
    fx, fy, fz = f(x/xn), f(y/yn), f(z/zn)
    return (116*fy - 16, 500*(fx-fy), 200*(fy-fz))

def delta_e2000(lab1, lab2):
    L1, a1, b1 = lab1; L2, a2, b2 = lab2
    C1 = math.hypot(a1, b1); C2 = math.hypot(a2, b2)
    Cb = (C1 + C2) / 2
    G = 0.5 * (1 - math.sqrt(Cb**7 / (Cb**7 + 25**7))) if Cb > 0 else 0.5
    a1p = (1+G)*a1; a2p = (1+G)*a2
    C1p = math.hypot(a1p, b1); C2p = math.hypot(a2p, b2)
    h1p = math.degrees(math.atan2(b1, a1p)) % 360 if (a1p or b1) else 0.0
    h2p = math.degrees(math.atan2(b2, a2p)) % 360 if (a2p or b2) else 0.0
    dLp = L2 - L1
    dCp = C2p - C1p
    if C1p*C2p == 0: dhp = 0.0
    elif abs(h2p-h1p) <= 180: dhp = h2p - h1p
    elif h2p - h1p > 180: dhp = h2p - h1p - 360
    else: dhp = h2p - h1p + 360
    dHp = 2*math.sqrt(C1p*C2p)*math.sin(math.radians(dhp)/2)
    Lbp = (L1+L2)/2; Cbp = (C1p+C2p)/2
    if C1p*C2p == 0: hbp = h1p + h2p
    elif abs(h1p-h2p) <= 180: hbp = (h1p+h2p)/2
    elif h1p+h2p < 360: hbp = (h1p+h2p+360)/2
    else: hbp = (h1p+h2p-360)/2
    T = (1 - 0.17*math.cos(math.radians(hbp-30)) + 0.24*math.cos(math.radians(2*hbp))
         + 0.32*math.cos(math.radians(3*hbp+6)) - 0.20*math.cos(math.radians(4*hbp-63)))
    dTh = 30*math.exp(-(((hbp-275)/25)**2))
    Rc = 2*math.sqrt(Cbp**7/(Cbp**7 + 25**7)) if Cbp > 0 else 0
    Sl = 1 + (0.015*(Lbp-50)**2)/math.sqrt(20+(Lbp-50)**2)
    Sc = 1 + 0.045*Cbp
    Sh = 1 + 0.015*Cbp*T
    Rt = -math.sin(math.radians(2*dTh))*Rc
    return math.sqrt((dLp/Sl)**2 + (dCp/Sc)**2 + (dHp/Sh)**2 + Rt*(dCp/Sc)*(dHp/Sh))

# ---------- 에셋 파서: icon 파트의 tone -> color ----------
def asset_icon_colors(name):
    """반환 {tone: (r,g,b)} — 그 톤이 처음 나오는 파트의 색(= ItemCatalog 파생 규약)."""
    path = os.path.join(ITEMS, name + '.asset')
    txt = open(path, encoding='utf-8').read()
    m = re.search(r'^  icon:\n(.*?)^  (?:wornShapes|cardIconOverride|wornGroupAlpha):', txt, re.S | re.M)
    if not m:
        raise RuntimeError('icon block not found: ' + name)
    block = m.group(1)
    out = {}
    order = []
    for cm in re.finditer(r'color: \{r: ([-\d.]+), g: ([-\d.]+), b: ([-\d.]+), a: ([-\d.]+)\}\n\s*tone: (\d+)', block):
        r, g, b, a, tone = float(cm.group(1)), float(cm.group(2)), float(cm.group(3)), float(cm.group(4)), int(cm.group(5))
        order.append((tone, (r, g, b)))
        if tone not in out:
            out[tone] = (r, g, b)
    return out, order

# ---------- 비트맵 색 측정 ----------
def bg_mask(arr):
    """배경(어두운 남색 판) 마스크. 네 모서리 8x8 평균을 배경 기준색으로 잡고,
    거기서 Lab 거리 <= 12 인 픽셀 + 밝기가 매우 낮은 픽셀을 배경으로 본다."""
    h, w, _ = arr.shape
    corners = np.concatenate([
        arr[0:10, 0:10].reshape(-1, 3), arr[0:10, w-10:w].reshape(-1, 3),
        arr[h-10:h, 0:10].reshape(-1, 3), arr[h-10:h, w-10:w].reshape(-1, 3)])
    bg = corners.mean(axis=0) / 255.0
    bglab = np.array(rgb_to_lab(tuple(bg)))
    flat = arr.reshape(-1, 3) / 255.0
    lab = rgb_to_lab_array(flat)
    d = np.linalg.norm(lab - bglab, axis=1)
    return (d <= 16.0).reshape(h, w), bg

def rgb_to_lab_array(rgb):
    c = np.where(rgb <= 0.04045, rgb/12.92, ((rgb+0.055)/1.055)**2.4)
    M = np.array([[0.4124564, 0.3575761, 0.1804375],
                  [0.2126729, 0.7151522, 0.0721750],
                  [0.0193339, 0.1191920, 0.9503041]])
    xyz = c @ M.T
    xyz = xyz / np.array([0.95047, 1.0, 1.08883])
    e = 216/24389
    f = np.where(xyz > e, np.cbrt(xyz), (841/108)*xyz + 4/29)
    L = 116*f[:, 1] - 16
    a = 500*(f[:, 0] - f[:, 1])
    b = 200*(f[:, 1] - f[:, 2])
    return np.stack([L, a, b], axis=1)

def kmeans_lab(lab, k, iters=40, seed=7):
    rng = np.random.default_rng(seed)
    # k-means++ 초기화
    cent = [lab[rng.integers(len(lab))]]
    for _ in range(k-1):
        d = np.min(np.stack([np.linalg.norm(lab - c, axis=1) for c in cent]), axis=0) ** 2
        p = d / d.sum() if d.sum() > 0 else None
        cent.append(lab[rng.choice(len(lab), p=p)])
    cent = np.array(cent)
    for _ in range(iters):
        dist = np.linalg.norm(lab[:, None, :] - cent[None, :, :], axis=2)
        lbl = dist.argmin(axis=1)
        new = np.array([lab[lbl == i].mean(axis=0) if np.any(lbl == i) else cent[i] for i in range(k)])
        if np.allclose(new, cent, atol=1e-4): cent = new; break
        cent = new
    return cent, lbl

def lab_to_rgb(lab):
    L, a, b = lab
    fy = (L + 16) / 116; fx = fy + a/500; fz = fy - b/200
    def fi(t):
        return t**3 if t**3 > 216/24389 else (108/841)*(t - 4/29)
    x, y, z = fi(fx)*0.95047, fi(fy)*1.0, fi(fz)*1.08883
    r = x*3.2404542 + y*-1.5371385 + z*-0.4985314
    g = x*-0.9692660 + y*1.8760108 + z*0.0415560
    bb = x*0.0556434 + y*-0.2040259 + z*1.0572252
    def enc(c):
        c = max(0.0, min(1.0, c))
        return 12.92*c if c <= 0.0031308 else 1.055*(c**(1/2.4)) - 0.055
    return (enc(r), enc(g), enc(bb))

def measure(name, k=6, sub=2):
    im = Image.open(os.path.join(ICONS, name + '.png')).convert('RGB')
    arr = np.asarray(im)[::sub, ::sub]
    m, bg = bg_mask(arr)
    fg = arr[~m].reshape(-1, 3) / 255.0
    lab = rgb_to_lab_array(fg)
    cent, lbl = kmeans_lab(lab, k)
    total = len(lab)
    rows = []
    for i in range(k):
        n = int((lbl == i).sum())
        if n == 0: continue
        rgb = lab_to_rgb(tuple(cent[i]))
        rows.append(dict(rgb=rgb, lab=tuple(cent[i]), share=n/total, n=n))
    rows.sort(key=lambda r: -r['share'])
    return rows, bg, total

def chroma(lab): return math.hypot(lab[1], lab[2])

def main():
    dump = '--dump' in sys.argv
    print('배경 판정: 네 모서리 10x10 평균에서 Lab 거리 ≤ 16 인 픽셀을 배경으로 제외. 2px 서브샘플(256×256).')
    print('클러스터: Lab k-means (k=6, k-means++ seed 7, 40회).')
    print()
    hdr = f"{'아이템':<30}{'현행 P':<9}{'현행 S':<9}{'몸 P':<9}{'몸 S':<9}{'비트맵 최대':<11}{'점유':>6}  {'ΔE(몸P↔비트맵최대)':>10}"
    print(hdr); print('-' * len(hdr))
    allrows = {}
    for n in NAMES:
        tones, order = asset_icon_colors(n)
        prim = tones.get(0); sec = tones.get(1)
        wp = worn_color(prim); ws = worn_color(sec)
        rows, bg, total = measure(n)
        allrows[n] = (rows, prim, sec, wp, ws, bg, total)
        top = rows[0]
        de = delta_e2000(rgb_to_lab(wp), top['lab'])
        print(f"{n:<30}{hexs(prim):<9}{hexs(sec):<9}{hexs(wp):<9}{hexs(ws):<9}{hexs(top['rgb']):<11}{top['share']*100:>5.1f}%  {de:>10.1f}")
    print()
    print('=== 비트맵 클러스터 전문 (점유 내림차순) ===')
    for n in NAMES:
        rows, prim, sec, wp, ws, bg, total = allrows[n]
        print(f"\n-- {n}  (배경 {hexs(bg)} · 전경 픽셀 {total})")
        for r in rows:
            L, a, b = r['lab']
            h, s, v = colorsys.rgb_to_hsv(*r['rgb'])
            print(f"   {hexs(r['rgb'])}  점유 {r['share']*100:5.1f}%   L*{L:6.1f} C*{chroma(r['lab']):5.1f}"
                  f"   HSV({h*360:5.1f}, {s:.2f}, {v:.2f})"
                  f"   ΔE→몸P {delta_e2000(rgb_to_lab(wp), r['lab']):5.1f}"
                  f"   ΔE→몸S {delta_e2000(rgb_to_lab(ws), r['lab']):5.1f}")

if __name__ == '__main__':
    main()
