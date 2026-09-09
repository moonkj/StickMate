# -*- coding: utf-8 -*-
"""R4 정본 자 — 「읽히는 색면」을 **화가 알고리즘으로 직접 그려서** 잰다.

왜 다시 만드는가(자기 교정):
  · 1차 자(r4_ink.engulf)는 «둘레 × W/2 ÷ 면적» 이라 오목 도형에서 과대평가했다.
  · 2차 자(r4_survive.survive)는 조각을 **자기 윤곽으로만** 침식했다. 그런데 출하 선글라스는
    렌즈 채움이 noStroke 이고 **형제 조각 `Piece_B0rim`(채움 없는 같은 경로)** 이 테를 그린다 —
    화면에서는 똑같이 먹히는데 2차 자는 «생존 100%» 라고 답한다. **거짓 초록이다.**
  ⇒ 3차 자는 근사를 버린다. 조각 순서대로 채움을 칠하고 그 위에 획을 칠한 뒤,
    **마지막에 그 조각의 재질색으로 남아 있는 화소**를 센다. 화면이 하는 일 그대로다.

해상도는 **착용 장치 해상도의 4배**(46.53 px/R)로 잡고 면적을 pt² 로 환산한다
(장치 해상도로 바로 재면 1픽셀 반올림이 작은 조각에서 ±40% 를 흔든다).
"""
import math, numpy as np
from PIL import Image, ImageDraw
import r4_geom as G

SS = 4.0
PPR = SS * 2.0 * G.R_PT_075          # 46.53 px/R  (= 착용 2x 장치 해상도 × 4)
PX2_TO_PT2 = (1.0 / (SS * 2.0)) ** 2  # 화소 -> pt²  (2x 장치라 1pt = 2 device px, 여기선 8 px)

def _order(pieces):
    """그리는 순서 — layer 1(Back)이 먼저, 나머지는 목록 순서(프로덕션 LayerOrder 와 같은 뜻)."""
    return sorted(range(len(pieces)), key=lambda i: (0 if pieces[i]["layer"] == 1 else 1, i))

def paint(pieces, ppr=PPR, pad=0.6):
    allp = [q for p in pieces for q in p["pts"]]
    xs = [q[0] for q in allp]; ys = [q[1] for q in allp]
    x0, x1, y0, y1 = min(xs) - pad, max(xs) + pad, min(ys) - pad, max(ys) + pad
    w = int((x1 - x0) * ppr) + 2; h = int((y1 - y0) * ppr) + 2
    ox, oy = -x0 * ppr, y1 * ppr
    lab = np.full((h, w), -1, np.int16)          # -1 배경 · >=0 그 조각의 재질색 · -2 잉크/획
    for i in _order(pieces):
        p = pieces[i]
        pts = [(ox + x * ppr, oy - y * ppr) for x, y in p["pts"]]
        if len(pts) < 2: continue
        if p["filled"] and len(pts) >= 3:
            m = Image.new("L", (w, h), 0); ImageDraw.Draw(m).polygon(pts, fill=255)
            lab[np.array(m) > 127] = i
        # 획 — 불투명하게 덮는 것만(알파 0.5 미만은 밑색이 비친다)
        if not p["noStroke"] and p.get("lineAlpha", 1.0) >= 0.5:
            wpx = max(1.0, G.W * p["strokeMult"] * ppr)
            seq = pts + [pts[0]] if p["loop"] else pts
            m = Image.new("L", (w, h), 0)
            ImageDraw.Draw(m).line(seq, fill=255, width=int(round(wpx)), joint="curve")
            lab[np.array(m) > 127] = -2
    return lab

def readable(pieces):
    """조각별 «읽히는 색면»(pt²). 채움이 아닌 조각은 None."""
    lab = paint(pieces)
    out = []
    for i, p in enumerate(pieces):
        out.append((p["name"], p["tone"], float((lab == i).sum()) * PX2_TO_PT2) if p["filled"] else (p["name"], p["tone"], None))
    return out

if __name__ == "__main__":
    import sys
    print(f"# 래스터 {PPR:.2f} px/R · 1 화소 = {PX2_TO_PT2:.5f} pt² · 획 W = 2.00pt\n")
    for label, bank in (("① 출하 인계본 12", G.load_handoff()),
                        ("② 출하 NECK 6", {("neck_"+k): v for k, v in G.load_neck6().items()}),
                        ("③ 팩 R3(현행)", G.load_pack("pack_detail_r3"))):
        print("═" * 100); print(label); print("═" * 100)
        tot = bad = 0
        for k, it in sorted(bank.items()):
            ps = G.body_pieces(it)
            r = [(n, t, v) for n, t, v in readable(ps) if v is not None]
            tot += len(r); b = [n for n, t, v in r if v < 4.0]; bad += len(b)
            print(f"{k[:30]:<30} 채움{len(r)}  색면 {sorted(('%.1f'%v for n,t,v in r), key=lambda s:-float(s))}   못읽힘 {b if b else '—'}")
        print(f"  ⇒ 채움 {tot} · 4pt² 미만 {bad} · 아이템당 {bad/len(bank):.2f}\n")


def occluded_frac(pieces, i, ppr=PPR, pad=0.6):
    """조각 i 의 다각형 중 **뒤에 그려지는 다른 채움**에 덮이는 비율. 가림은 설계이고 잉크 잠식은 결함이다 —
    둘을 갈라야 중절모의 «먼 쪽 챙 반쪽»(정면에서 100% 가려지는 것이 정상)을 결함으로 오인하지 않는다."""
    p = pieces[i]
    if not p["filled"]: return 0.0
    allp = [q for q in p["pts"]]
    xs = [q[0] for q in allp]; ys = [q[1] for q in allp]
    x0, x1, y0, y1 = min(xs) - pad, max(xs) + pad, min(ys) - pad, max(ys) + pad
    w = int((x1 - x0) * ppr) + 2; h = int((y1 - y0) * ppr) + 2
    ox, oy = -x0 * ppr, y1 * ppr
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).polygon([(ox + x * ppr, oy - y * ppr) for x, y in p["pts"]], fill=255)
    A = np.array(m) > 127
    if A.sum() == 0: return 1.0
    order = _order(pieces); pos = order.index(i)
    cov = np.zeros_like(A)
    for j in order[pos + 1:]:
        q = pieces[j]
        if not q["filled"] or len(q["pts"]) < 3: continue
        mm = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mm).polygon([(ox + x * ppr, oy - y * ppr) for x, y in q["pts"]], fill=255)
        cov |= np.array(mm) > 127
    return float((A & cov).sum()) / float(A.sum())
