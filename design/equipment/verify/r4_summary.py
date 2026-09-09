# -*- coding: utf-8 -*-
"""R4 §4 최종 대조표 — 중절모 · 팩 R3 · 팩 R4 를 **같은 자**로 한 화면에."""
import sys, numpy as np
sys.path.insert(0, '.')
import r4_geom as G, r4_paint as P, r4_gate as GT, r4_stack as ST, r4_check
import pack_detail_r4 as R4

def metrics(pieces):
    lab = P.paint(pieces)
    ink = float((lab == -2).sum()) * P.PX2_TO_PT2
    vals = sorted((float((lab == i).sum()) * P.PX2_TO_PT2 for i, q in enumerate(pieces) if q["filled"]), reverse=True)
    tot = sum(vals)
    fs = [x for q in pieces for x in GT.facets(q)]
    return dict(fills=len(vals), color=tot, top=vals[0] if vals else 0.0, ink=ink,
                ratio=ink / max(1e-9, ink + tot), tiny=sum(1 for v in vals if v < 4.0),
                facet=(sum(fs) / len(fs)) if fs else 0.0,
                pts=sum(len(q["pts"]) for q in pieces), n=len(pieces))

def row(label, pieces):
    m = metrics(pieces)
    return (label, m["n"], m["fills"], m["pts"], m["color"], m["top"], m["ink"], m["ratio"], m["tiny"], m["facet"])

def table(rows, title):
    print("═" * 118); print(title); print("═" * 118)
    print(f"{'':<32}{'조각':>4}{'채움':>5}{'점':>5}{'색면합pt²':>11}{'최대색면':>9}{'잉크pt²':>9}{'잉크비율':>9}{'티끌':>5}{'면오차R':>9}")
    for r in rows:
        print(f"{r[0]:<32}{r[1]:>4}{r[2]:>5}{r[3]:>5}{r[4]:>11.1f}{r[5]:>9.1f}{r[6]:>9.1f}{r[7]:>9.3f}{r[8]:>5}{r[9]:>9.4f}")
    if len(rows) > 1:
        n = len(rows)
        print("─" * 118)
        print(f"{'평균':<32}{sum(r[1] for r in rows)/n:>4.1f}{sum(r[2] for r in rows)/n:>5.1f}{sum(r[3] for r in rows)/n:>5.0f}"
              f"{sum(r[4] for r in rows)/n:>11.1f}{sum(r[5] for r in rows)/n:>9.1f}{sum(r[6] for r in rows)/n:>9.1f}"
              f"{sum(r[7] for r in rows)/n:>9.3f}{sum(r[8] for r in rows)/n:>5.2f}{sum(r[9] for r in rows)/n:>9.4f}")
    print()

if __name__ == "__main__":
    print(f"# 자: 착용 배율 0.75 · R = {G.R_PT_075:.4f}pt · 머리 지름 {2*G.R_PT_075:.2f}pt · 획 W = 2.00pt")
    print(f"# 「색면」 = 화가 알고리즘으로 그린 뒤 그 조각의 재질색으로 **남아 있는** 화소(r4_paint.py). 근사 없음.\n")
    h = G.load_handoff()
    table([row("중절모 fedora (사용자 지목 기준)", G.body_pieces(h["fedora"])),
           row("야구모자 clothhat", G.body_pieces(h["clothhat"])),
           row("왕관 crown", G.body_pieces(h["crown"]))], "① 출하 HEAD 3종 — 기준선")
    p3 = G.load_pack("pack_detail_r3")
    table([row(k.replace("pack_", ""), G.body_pieces(v)) for k, v in sorted(p3.items())], "② 팩 12종 R3 (현행 출하 에셋)")
    b4 = r4_check.to_bank(R4.PACKS)
    table([row(k, G.body_pieces(v)) for k, v in sorted(b4.items())], "③ 팩 12종 R4 (이번 시안)")

    # 머리 원반 가림
    ppr = ST.PX_PER_R_DEVICE; x0, x1, y0, y1 = -5.0, 5.0, -6.5, 4.2
    w = int(round((x1-x0)*ppr)); hh = int(round((y1-y0)*ppr)); ox, oy = -x0*ppr, y1*ppr
    yy, xx = np.mgrid[0:hh, 0:w]; X = (xx-ox)/ppr; Y = (oy-yy)/ppr; head = (X**2+Y**2) <= 1.0
    def cov(pieces):
        m = np.array(ST.render_item(pieces, ppr, ox, oy, w, hh)) > 127
        return (m & head).sum()/head.sum()*100
    print("═" * 118); print("④ R4-5 머리 원반 가림 — 단품 (출하 최대: HEAD 46.6 · EYES 52.0 · NECK 1.6 · BACK 1.6)"); print("═" * 118)
    print(f"{'아이템':<32}{'슬롯':<6}{'R3':>8}{'R4':>8}")
    for k in sorted(b4):
        k3 = "pack_" + k
        c3 = cov(G.body_pieces(p3[k3])) if k3 in p3 else float('nan')
        print(f"{k:<32}{b4[k]['slot']:<6}{c3:>8.1f}{cov(G.body_pieces(b4[k])):>8.1f}")
    print()
    print("═" * 118); print("⑤ R4-5 4종 동시 착용 (출하 384조합 실측: 최소 59.7 · 중앙 70.2 · 최대 75.5 / 덩어리 2개가 336조합)"); print("═" * 118)
    for pk in ("cyber", "mine", "arcane"):
        out = []
        for bank, pre, lab in ((p3, "pack_", "R3"), (b4, "", "R4")):
            U = np.zeros((hh, w), bool)
            for k in [q for q in bank if q.startswith(pre + pk + "_")]:
                U |= np.array(ST.render_item(G.body_pieces(bank[k]), ppr, ox, oy, w, hh)) > 127
            sz = sorted(ST.components(U), reverse=True); ink = U.sum()
            out.append((lab, (U & head).sum()/head.sum()*100, len(sz), sz[0]/ink*100))
        print(f"  pack.{pk:<8}  R3 가림 {out[0][1]:5.1f}% 덩어리 {out[0][2]} (최대 {out[0][3]:.0f}%)"
              f"   ->   R4 가림 {out[1][1]:5.1f}% 덩어리 {out[1][2]} (최대 {out[1][3]:.0f}%)")
