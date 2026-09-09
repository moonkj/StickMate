# -*- coding: utf-8 -*-
"""R4 — 「획 생존율」. 조각을 자기 윤곽 두께의 절반만큼 안쪽으로 침식하면 재질색이 남는가.

r4_ink.py 의 잠식률(둘레×W/2 ÷ 면적)은 볼록 근사라 초승달·고리처럼 오목한 도형을 과대평가한다.
여기서는 **직접 래스터해 침식**한다 — 근사 없이. 두 자가 같은 방향을 가리키면 판정이 살아 있는 것이다.
  생존율 = erode(조각, W/2) 면적 / 조각 면적.
  생존율 0 = 이 조각은 **화면에서 자기 색을 한 픽셀도 못 낸다**(순수 잉크 덩어리).
윤곽이 없는 조각(noStroke)은 애초에 먹히지 않으므로 1.0 으로 둔다.
"""
import math, numpy as np
from PIL import Image, ImageDraw
import r4_geom as G

PPR = 48.0  # 래스터 해상도 px/R (착용 장치 해상도 11.6 px/R 의 4.1배 — 침식 반경을 정수로 못 박기 위함)

def survive(p):
    if not p["filled"]: return None
    if p["noStroke"] or p.get("lineAlpha", 1.0) <= 0.0: return 1.0
    pts = p["pts"]
    xs = [q[0] for q in pts]; ys = [q[1] for q in pts]
    pad = G.W + 0.2
    w = int((max(xs) - min(xs) + 2 * pad) * PPR) + 2
    h = int((max(ys) - min(ys) + 2 * pad) * PPR) + 2
    img = Image.new("L", (w, h), 0)
    ImageDraw.Draw(img).polygon([((x - min(xs) + pad) * PPR, (max(ys) - y + pad) * PPR) for x, y in pts], fill=255)
    A = np.array(img) > 127
    a0 = A.sum()
    if a0 == 0: return 0.0
    r = int(round(G.W * p["strokeMult"] / 2.0 * PPR))       # 침식 반경 = 획 절반
    if r <= 0: return 1.0
    E = A.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy > r * r: continue
            E &= np.roll(np.roll(A, dy, 0), dx, 1)
    return float(E.sum()) / a0

if __name__ == "__main__":
    print(f"# 침식 반경 = W/2 = {G.W/2:.4f} R = 1.00 pt · 래스터 {PPR:.0f} px/R\n")
    banks = [("① 출하 인계본 12", G.load_handoff()),
             ("② 출하 NECK 6", {("neck_"+k): v for k, v in G.load_neck6().items()}),
             ("③ 팩 R3(현행)", G.load_pack("pack_detail_r3"))]
    for label, bank in banks:
        print("═" * 96); print(label); print("═" * 96)
        print(f"{'아이템':<30}{'채움':>4}{'생존0':>7}{'생존<0.25':>10}{'최악':>7}{'평균':>7}  0인 조각")
        print("─" * 96)
        tot = [0, 0, 0]
        for k, it in sorted(bank.items()):
            ps = [p for p in G.body_pieces(it) if p["filled"]]
            ss = [(p["name"], survive(p)) for p in ps]
            zero = [n for n, s in ss if s <= 0.0005]
            low = [n for n, s in ss if s < 0.25]
            vals = [s for _, s in ss]
            tot[0] += len(ps); tot[1] += len(zero); tot[2] += len(low)
            print(f"{k[:30]:<30}{len(ps):>4}{len(zero):>7}{len(low):>10}{min(vals):>7.2f}"
                  f"{sum(vals)/len(vals):>7.2f}  {','.join(zero) if zero else '—'}")
        print("─" * 96)
        print(f"{'합계':<30}{tot[0]:>4}{tot[1]:>7}{tot[2]:>10}"
              f"    · 아이템당 생존0 = {tot[1]/len(bank):.2f}개\n")

# ── 절대 면적판 (아래는 `python3 r4_survive.py --abs`) ──────────────────────────
def survive_area_pt2(p):
    s = survive(p)
    if s is None: return None
    import r4_ink
    return s * r4_ink.area(p["pts"]) * G.R_PT_075 ** 2, s
