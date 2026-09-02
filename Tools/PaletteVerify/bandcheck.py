#!/usr/bin/env python3
"""verify-change 독립 재측정기 — 아이템 팔레트 자립 대역.

프로덕션 테스트(ItemPaletteBandGateTests)와 **같은 명령을 다시 돌리지 않는다.**
애셋 YAML을 직접 파싱하고, WornColor / RelativeLuminance / ContrastRatio를
C# 원본을 보고 파이썬으로 다시 구현해 대조한다.

교정이 깨지면 이 스크립트가 낸 모든 숫자는 폐기한다(TEAM.md §4 공통 처방).
"""
import os, re, sys, math, collections

ITEMS = os.path.join(os.path.dirname(__file__), "../../Assets/_Project/Resources/Items")

# ---- 프로덕션에서 읽어 온 상수 (출처를 주석에 남긴다) ----
WORN_S_FLOOR = 0.42          # ItemCatalog.WornSaturationFloor
WORN_V_FLOOR = 0.55          # ItemCatalog.WornValueFloor
WORN_V_CEIL  = 0.80          # ItemCatalog.WornValueCeiling
MIN_CONTRAST = 3.0           # UiChrome.MinNonTextContrast
INK_TONE     = (0xD6/255, 0xDB/255, 0xE3/255)   # ItemCatalog.InkTone  #D6DBE3
INK_DIM      = (0x8B/255, 0x93/255, 0x9F/255)   # ItemCatalog.InkDimTone #8B939F
PAPER        = (0.914, 0.918, 0.902)            # UiChrome.PortraitSurface
CHARCOAL     = (0.145, 0.157, 0.180)            # CharacterPortraitStage 목탄
BACKDROPS = [("흰 바탕화면", (1.0,1.0,1.0)), ("검은 바탕화면", (0.0,0.0,0.0)),
             ("종이 무대", PAPER), ("목탄 무대", CHARCOAL)]

def lin(c):
    c = min(max(c, 0.0), 1.0)
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055) ** 2.4

def lum(c):  return 0.2126*lin(c[0]) + 0.7152*lin(c[1]) + 0.0722*lin(c[2])

def contrast(a, b):
    la, lb = lum(a), lum(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi+0.05)/(lo+0.05)

# ---- Unity Color.RGBToHSV / HSVToRGB 재구현 ----
def _hsv_helper(offset, dom, c1, c2):
    V = dom
    if V != 0.0:
        m = c2 if c1 > c2 else c1
        d = V - m
        if d != 0.0:
            S = d/V; H = offset + (c1-c2)/d
        else:
            S = 0.0; H = offset + (c1-c2)
        H /= 6.0
        if H < 0.0: H += 1.0
    else:
        S = 0.0; H = 0.0
    return H, S, V

def rgb2hsv(c):
    r, g, b = c
    if b > g and b > r: return _hsv_helper(4.0, b, r, g)
    if g > r:           return _hsv_helper(2.0, g, b, r)
    return _hsv_helper(0.0, r, g, b)

def hsv2rgb(H, S, V):
    if S == 0.0: return (V, V, V)
    if V == 0.0: return (0.0, 0.0, 0.0)
    n = H*6.0; i = int(math.floor(n)); f = n - i
    p = V*(1.0-S); q = V*(1.0-S*f); t = V*(1.0-S*(1.0-f))
    return {0:(V,t,p),1:(q,V,p),2:(p,V,t),3:(p,q,V),4:(t,p,V),
            5:(V,p,q),6:(V,t,p),-1:(V,p,q)}[i]

def same(a, b):
    return all(abs(a[i]-b[i]) < 0.004 for i in range(3))

def is_ink_marker(c):  return same(c, INK_TONE) or same(c, INK_DIM)

def worn(c):
    if is_ink_marker(c): return None          # 잉크로 칠해진다 — 면제
    H, S, V = rgb2hsv(c)
    S = max(S, WORN_S_FLOOR)
    V = min(max(V, WORN_V_FLOOR), WORN_V_CEIL)
    return hsv2rgb(H, S, V)

def hexs(c):  # Unity ColorUtility.ToHtmlStringRGB = round(v*255)
    return "#%02X%02X%02X" % tuple(int(round(min(max(v,0.0),1.0)*255)) for v in c)

# ---- 0. 교정 — 깨지면 아래 전부 폐기 ----
def calibrate():
    checks = [("흰/검", contrast((1,1,1),(0,0,0)), 21.0, 5e-4),
              ("동일색(흰)", contrast((1,1,1),(1,1,1)), 1.0, 5e-4),
              ("#767676/흰", contrast((0x76/255,)*3, (1,1,1)), 4.5422, 5e-4)]
    ok = True
    for name, got, want, tol in checks:
        good = abs(got-want) <= tol
        ok &= good
        print(f"  [{'OK ' if good else 'FAIL'}] 교정 {name}: {got:.4f} (기대 {want})")
    # 왕복 교정: RGB->HSV->RGB 항등
    worst = 0.0
    for r in range(0,256,17):
        for g in range(0,256,17):
            for b in range(0,256,17):
                c=(r/255,g/255,b/255); H,S,V=rgb2hsv(c); back=hsv2rgb(H,S,V)
                worst=max(worst, max(abs(back[i]-c[i]) for i in range(3)))
    good = worst < 1e-6; ok &= good
    print(f"  [{'OK ' if good else 'FAIL'}] 교정 HSV 왕복 최대오차 {worst:.2e}")
    return ok

# ---- 애셋 파싱 ----
def load():
    """[(파일, itemId, slot, itemIndex, [(색, tone), ...])]"""
    out = []
    for f in sorted(os.listdir(ITEMS)):
        if not f.endswith(".asset"): continue
        t = open(os.path.join(ITEMS, f), encoding="utf-8", errors="replace").read()
        iid = re.search(r'^\s*itemId:\s*(\S+)', t, re.M)
        slot = re.search(r'^\s*slot:\s*(-?\d+)', t, re.M)
        idx  = re.search(r'^\s*itemIndex:\s*(-?\d+)', t, re.M)
        if not (iid and slot and idx): continue
        pieces = []
        # color 줄과 그 바로 뒤 tone 줄을 쌍으로 읽는다
        for m in re.finditer(
            r'^\s*color:\s*\{r:\s*([-\d.eE]+),\s*g:\s*([-\d.eE]+),\s*b:\s*([-\d.eE]+),\s*a:\s*([-\d.eE]+)\}'
            r'\s*\n\s*tone:\s*(\d+)', t, re.M):
            pieces.append(((float(m.group(1)), float(m.group(2)), float(m.group(3))), int(m.group(5))))
        out.append((f, iid.group(1), int(slot.group(1)), int(idx.group(1)), pieces))
    return out

def main():
    print("=== 0. 계산기 교정 ===")
    if not calibrate():
        print("교정 실패 — 이 스크립트가 낸 모든 숫자를 폐기하십시오."); return 2

    entries = load()
    print(f"\n=== 1. 열거 === 애셋 {len(entries)}개")
    npieces = sum(len(e[4]) for e in entries)
    withicon = sum(1 for e in entries if e[4])
    print(f"  아이콘 조각 총 {npieces}건 / 아이콘을 가진 애셋 {withicon}개")
    if withicon == 0:
        print("  열거가 비었다 — 아래 '0건'은 전부 무효."); return 2

    # 색 다중집합
    allcols = collections.OrderedDict()
    marker_pieces = 0
    for f, iid, s, i, pieces in entries:
        for c, tone in pieces:
            if is_ink_marker(c): marker_pieces += 1; continue
            allcols.setdefault(hexs(c), []).append((iid, tone))
    print(f"  잉크 표식 면제 조각 {marker_pieces}건")
    print(f"  ★ 면제 뒤 남은 <b>서로 다른</b> 색: {len(allcols)}개")

    print("\n=== 2. 본안 — 몸에 칠한 뒤 배경 넷 ===")
    bad = []
    worstcr, worstwho = 99.0, None
    for hx, owners in allcols.items():
        c = tuple(int(hx[1+2*k:3+2*k],16)/255 for k in range(3))
        w = worn(c)
        ident = same(w, c)
        rows = [(name, contrast(w, bg)) for name, bg in BACKDROPS]
        lo = min(rows, key=lambda r: r[1])
        if lo[1] < worstcr: worstcr, worstwho = lo[1], (hx, hexs(w), lo[0])
        if lo[1] < MIN_CONTRAST:
            bad.append((hx, hexs(w), lo[0], lo[1], ident, owners))
    print(f"  대역 밖: {len(bad)}/{len(allcols)}")
    for hx, wh, bgn, cr, ident, owners in bad:
        print(f"    {hx} -> 몸 {wh} / {bgn} {cr:.2f}:1  항등={ident}  쓰는곳={sorted(set(o[0] for o in owners))}")
    print(f"  ★ 최악 대비: {worstwho[0]} -> 몸 {worstwho[1]} / {worstwho[2]} = {worstcr:.2f}:1")

    print("\n=== 3. WornColor 항등 (카드색 == 몸색, 0.004 이내) ===")
    nonident = [(hx, hexs(worn(tuple(int(hx[1+2*k:3+2*k],16)/255 for k in range(3)))))
                for hx in allcols
                if not same(worn(tuple(int(hx[1+2*k:3+2*k],16)/255 for k in range(3))),
                            tuple(int(hx[1+2*k:3+2*k],16)/255 for k in range(3)))]
    print(f"  항등 아님: {len(nonident)}/{len(allcols)}")
    for hx, wh in nonident: print(f"    {hx} -> {wh}")

    print("\n=== 4. 양성 대조 — 알려진 불량색을 같은 판정기가 잡는가 ===")
    for hx, why in [("#FFF0B8","옛 최악(금빛)"), ("#E8E2D4","옛 Ivory"),
                    ("#7690CC","흑백만 본 1차 유도"), ("#A6532E","너무 어두움"),
                    ("#000000","완전 검정"), ("#FFFFFF","완전 흰색")]:
        c = tuple(int(hx[1+2*k:3+2*k],16)/255 for k in range(3))
        w = worn(c)
        rows = [(n, contrast(w, bg)) for n, bg in BACKDROPS]
        lo = min(rows, key=lambda r: r[1])
        caught = lo[1] < MIN_CONTRAST
        print(f"  [{'잡음' if caught else '놓침★'}] {hx}({why}) -> 몸 {hexs(w)} / {lo[0]} {lo[1]:.2f}:1")
        if not caught:
            print("   ★ 양성 대조 실패 — 위의 모든 '0건'을 폐기하십시오."); return 2
    return 0

if __name__ == "__main__":
    sys.exit(main())
