#!/usr/bin/env python3
# design-character R9 — perf-doc T7 그림의 독립 재측정 (조형 축)
# 프로덕션 코드 수정 없음. PNG만 읽는다.
import os, math, numpy as np
from PIL import Image

DIR = "/Users/kjmoon/App/StickMate/docs/perf/uvprobe"
BG, MEM, INK, OTHER = 0, 1, 2, 3
NAMES = {BG:"BG", MEM:"MEM", INK:"INK", OTHER:"OTHER"}

def classify(rgb):
    r = rgb[...,0].astype(int); g = rgb[...,1].astype(int); b = rgb[...,2].astype(int)
    out = np.full(r.shape, OTHER, dtype=np.uint8)
    out[(b>80)&(r<60)&(g<60)] = BG
    out[(r>180)&(g>180)&(b>180)] = MEM
    out[(r<60)&(g<60)&(b<60)] = INK
    return out

def load(name):
    im = Image.open(os.path.join(DIR, name)).convert("RGB")
    a = np.array(im)
    return classify(a), a

# ---- 교정: 알려진 색으로 분류기를 먼저 검증한다 (양성/음성 대조)
def selftest():
    probe = np.array([[[0,0,0],[255,255,255],[0,0,128],[128,128,128]]], dtype=np.uint8)
    got = classify(probe)[0].tolist()
    want = [INK, MEM, BG, OTHER]
    ok = got == want
    print(f"[교정] 분류기 {[NAMES[x] for x in got]} 기대 {[NAMES[x] for x in want]} -> {'통과' if ok else '실패'}")
    assert ok, "분류기 교정 실패 — 아래 숫자 전부 폐기"
selftest()

def exposure(c):
    """C# Shot.Exposure() 재현: INK/MEM 픽셀 중 4-이웃에 BG가 있는 것."""
    H, W = c.shape
    pad = np.full((H+2, W+2), OTHER, dtype=np.uint8); pad[1:-1,1:-1] = c
    nb = ((pad[1:-1,2:]==BG)|(pad[1:-1,:-2]==BG)|(pad[2:,1:-1]==BG)|(pad[:-2,1:-1]==BG))
    ink = int(np.count_nonzero((c==INK)&nb)); mem = int(np.count_nonzero((c==MEM)&nb))
    t = ink+mem
    return ink, mem, (ink/t if t else 0.0)

print("\n=== 1. 픽셀 순도 (MSAA 껐다는 주장의 검증) ===")
for f in sorted(os.listdir(DIR)):
    if not f.endswith(".png"): continue
    c,_ = load(f)
    n_other = int(np.count_nonzero(c==OTHER))
    tot = c.size
    flag = "" if n_other==0 else "  <-- 중간색 있음"
    print(f"{f:34s} {c.shape[1]}x{c.shape[0]}  BG={np.count_nonzero(c==BG):7d} MEM={np.count_nonzero(c==MEM):7d} INK={np.count_nonzero(c==INK):7d} OTHER={n_other}{flag}")

print("\n=== 2. 잉크 노출 재측정 ===")
for f in ["T7_end_A_cap8.png","T7_end_Aprime_cap0.png","T7_Aprime_arm_elbow.png",
          "T7_Aprime_single_elbow.png","T3_cap_mode2_win075.png","T3_cap_mode2_mac075.png","T3_cap_mode2_win035.png"]:
    c,_ = load(f)
    ink, mem, r = exposure(c)
    print(f"{f:34s} 잉크노출 {r*100:6.2f}%   (ink={ink} mem={mem})")

print("\n=== 3. 관절 구멍 / 깊이 재측정 (조인트 원 = 중심, 반경 rho) ===")
# T7: width=0.10 half=0.05, cam ortho 0.10, rt 1024 -> ppu = 1024/(2*0.10) = 5120
PPU = 1024/(2*0.10)
RPX = 0.05*PPU
print(f"  ppu={PPU:.0f}  rho={RPX:.0f}px  (획 폭 = {0.10*PPU:.0f}px)")
yy, xx = np.mgrid[0:1024, 0:1024]
cx = cy = 512.0
rr = np.sqrt((xx-cx)**2 + (yy-cy)**2)
disc = rr <= RPX
for f in ["T7_jointSolid_cap8.png","T7_jointSolid_cap0.png","T7_jointSolid_single_cap0.png"]:
    c,_ = load(f)
    holes = disc & (c==BG)
    frac = holes.sum()/disc.sum()
    deepest = rr[holes].min() if holes.any() else RPX
    print(f"{f:34s} 구멍 {frac*100:6.2f}%  최심 {deepest:6.1f}px = 반폭의 {deepest/RPX*100:5.1f}%")

print("\n=== 4. 이론값 대조 ===")
turn = 54.0
print(f"  두 렌더러 캡0 쐐기 이론 = 꺾임각/360 = {turn}/360 = {turn/360*100:.1f}%")
print(f"  캡8 사그타 깊이 이론 = cos(180/(2*8)/2 deg)... = {math.cos(math.radians(180/8/2))*100:.2f}%  (8정점 캡, 코드당 22.5도)")
d = 0.87
print(f"  병합 3.6% <-> 깊이 {d}: 0.15*(1-{d}^2) = {0.15*(1-d*d)*100:.2f}%  (자기정합 검산)")
