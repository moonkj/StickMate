#!/usr/bin/env python3
"""verify-change 독립 재측정 — 아이템 <b>내부</b> 주↔보조 변별(ΔE*ab CIE76).

coder 라운드 주장 "주↔보조 37쌍 중 미달 1/37"을 애셋에서 직접 다시 잰다.
주/보조 유도 규칙은 ItemCatalog.ItemCatalogEntry 생성자를 그대로 옮겼다:
  주   = tone==0 인 <b>첫</b> 조각
  보조 = tone!=0 인 <b>첫</b> 조각 (없으면 주와 같다)
"""
import os, re, sys, math

ITEMS   = os.path.join(os.path.dirname(__file__), "../../Assets/_Project/Resources/Items")
DISCERN = 7.8                                    # design/art/verify/lumorder.py DISCERN
INK_TONE = (0xD6, 0xDB, 0xE3); INK_DIM = (0x8B, 0x93, 0x9F)
_WP = (0.95047, 1.0, 1.08883)

def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def rgb2xyz(rgb):
    r,g,b = (lin(v/255.0) for v in rgb)
    return (0.4124564*r+0.3575761*g+0.1804375*b,
            0.2126729*r+0.7151522*g+0.0721750*b,
            0.0193339*r+0.1191920*g+0.9503041*b)
def _f(t): return t**(1/3) if t > 216/24389 else (841/108)*t + 4/29
def lab(rgb):
    x,y,z = rgb2xyz(rgb)
    fx,fy,fz = _f(x/_WP[0]), _f(y/_WP[1]), _f(z/_WP[2])
    return (116*fy-16, 500*(fx-fy), 200*(fy-fz))
def dE(a,b):
    la,lb = lab(a), lab(b)
    return math.sqrt(sum((la[i]-lb[i])**2 for i in range(3)))

def calibrate():
    """알려진 값으로 먼저 교정한다. 깨지면 아래 숫자 전부 폐기."""
    ok = True
    for name, got, want, tol in [
        ("L*(흰)",   lab((255,255,255))[0], 100.0, 1e-3),
        ("L*(검)",   lab((0,0,0))[0],         0.0, 1e-3),
        ("a*b*(회)", abs(lab((128,128,128))[1])+abs(lab((128,128,128))[2]), 0.0, 1e-3),
        ("ΔE(흰,검)", dE((255,255,255),(0,0,0)), 100.0, 1e-3),
        ("ΔE(동일)",  dE((160,98,42),(160,98,42)),  0.0, 1e-9),
        # CIE76은 L*만 다른 두 색에서 ΔE == ΔL*
        ("ΔE==ΔL*",  abs(dE((0,0,0),(255,255,255)) - (lab((255,255,255))[0]-lab((0,0,0))[0])), 0.0, 1e-9),
    ]:
        good = abs(got-want) <= tol; ok &= good
        print(f"  [{'OK ' if good else 'FAIL'}] 교정 {name} = {got:.6f} (기대 {want})")
    return ok

def hexs(c): return "#%02X%02X%02X" % c
def to255(f): return tuple(int(round(v*255)) for v in f)

def main():
    print("=== 0. ΔE 계산기 교정 ===")
    if not calibrate():
        print("교정 실패 — 모든 숫자 폐기."); return 2

    rows = []
    for f in sorted(os.listdir(ITEMS)):
        if not f.endswith(".asset"): continue
        t = open(os.path.join(ITEMS,f), encoding="utf-8", errors="replace").read()
        iid = re.search(r'^\s*itemId:\s*(\S+)', t, re.M)
        if not iid: continue
        pieces = [ (to255((float(m.group(1)),float(m.group(2)),float(m.group(3)))), int(m.group(5)))
                   for m in re.finditer(
                     r'^\s*color:\s*\{r:\s*([-\d.eE]+),\s*g:\s*([-\d.eE]+),\s*b:\s*([-\d.eE]+),\s*a:\s*([-\d.eE]+)\}'
                     r'\s*\n\s*tone:\s*(\d+)', t, re.M) ]
        pri = next((c for c,tn in pieces if tn == 0), None)
        sec = next((c for c,tn in pieces if tn != 0), None)
        has_sec = sec is not None
        if pri is None: pri = INK_TONE
        if sec is None: sec = pri
        rows.append((iid.group(1), pri, sec, has_sec, len(pieces)))

    print(f"\n=== 1. 열거 === 아이템 {len(rows)}개")
    if not rows: print("열거가 비었다 — 무효."); return 2

    pairs = [r for r in rows if r[3]]              # 보조 조각이 실제로 있는 것
    distinct = [r for r in pairs if r[1] != r[2]]  # 주≠보조
    print(f"  보조 조각이 있는 아이템: {len(pairs)}")
    print(f"  그중 주≠보조(색이 실제로 다른 쌍): {len(distinct)}")
    print(f"  보조가 없어 주=보조인 아이템: {len(rows)-len(pairs)}")

    print(f"\n=== 2. 주↔보조 ΔE (하한 {DISCERN}) ===")
    under = []
    for iid, pri, sec, hs, n in sorted(distinct, key=lambda r: dE(r[1], r[2])):
        d = dE(pri, sec)
        mark = "★미달" if d < DISCERN else "     "
        ink = ""
        if pri in (INK_TONE, INK_DIM) or sec in (INK_TONE, INK_DIM): ink = "  (잉크표식 포함)"
        print(f"  {mark} {iid:34s} 주 {hexs(pri)} | 보 {hexs(sec)}  ΔE {d:6.2f}{ink}")
        if d < DISCERN: under.append((iid, d))
    print(f"\n  ★ 미달 {len(under)} / {len(distinct)}")
    for iid, d in under: print(f"     {iid}  ΔE {d:.2f}")

    # 양성 대조 — 일부러 가까운 쌍을 넣으면 같은 판정이 잡는가
    print("\n=== 3. 양성 대조 ===")
    for a, b, why in [((160,98,42),(161,99,43), "1비트 차 — 반드시 미달로 잡혀야"),
                      ((0,0,0),(255,255,255),   "흰/검 — 절대 미달이면 안 됨")]:
        d = dE(a,b); caught = d < DISCERN
        print(f"  {hexs(a)} vs {hexs(b)}  ΔE {d:.2f}  판정={'미달' if caught else '통과'}   [{why}]")
    return 0

if __name__ == "__main__":
    sys.exit(main())
