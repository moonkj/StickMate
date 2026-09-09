# -*- coding: utf-8 -*-
"""R4 게이트 후보 — 「읽히는 색면」. 조각이 윤곽에 먹히고 **남는 재질색 면적**(pt², 배율0.75).

왜 비율이 아니라 면적인가: 비율 6% 라도 원 면적이 크면 읽힐 수 있고, 비율 100% 라도 3pt² 면 안 보인다.
문턱 후보 = **4.0 pt²** (= 2×2 pt = 착용 장치에서 4×4 device px). 이 아래는 «색이 아니라 점»이다.
"""
import numpy as np, r4_geom as G, r4_ink, r4_survive as S

def rows(bank):
    out = []
    for k, it in sorted(bank.items()):
        ps = [p for p in G.body_pieces(it) if p["filled"]]
        rec = []
        for p in ps:
            s = S.survive(p)
            A = r4_ink.area(p["pts"]) * G.R_PT_075 ** 2
            rec.append((p["name"], p["tone"], A, s, A * s))
        out.append((k, rec))
    return out

def show(bank, label, thr=4.0):
    print("═" * 108); print(label + f"   (문턱 {thr:.1f} pt²)"); print("═" * 108)
    print(f"{'아이템':<30}{'채움':>4}{'못읽힘':>7}{'읽힘':>5}{'최대색면pt²':>12}{'2위':>8}{'색면합':>9}  못 읽히는 조각")
    print("─" * 108)
    tot_bad = tot = 0
    for k, rec in rows(bank):
        vis = sorted((r[4] for r in rec), reverse=True)
        bad = [r[0] for r in rec if r[4] < thr]
        tot_bad += len(bad); tot += len(rec)
        print(f"{k[:30]:<30}{len(rec):>4}{len(bad):>7}{len(rec)-len(bad):>5}{vis[0]:>12.1f}"
              f"{(vis[1] if len(vis)>1 else 0):>8.1f}{sum(vis):>9.1f}  {','.join(bad) if bad else '—'}")
    print("─" * 108)
    print(f"{'합계':<30}{tot:>4}{tot_bad:>7}{tot-tot_bad:>5}   · 아이템당 못 읽히는 채움 = {tot_bad/len(bank):.2f}개\n")
    return tot_bad / len(bank)

if __name__ == "__main__":
    a = show(G.load_handoff(), "① 출하 인계본 12종")
    b = show({("neck_"+k): v for k, v in G.load_neck6().items()}, "② 출하 NECK 6종")
    c = show(G.load_pack("pack_detail_r2"), "③ 팩 R2")
    d = show(G.load_pack("pack_detail_r3"), "④ 팩 R3 (현행)")
    print(f"요약 — 아이템당 «못 읽히는 채움 조각» 수:  출하 인계본 {a:.2f} · 출하 NECK {b:.2f} · 팩 R2 {c:.2f} · 팩 R3 {d:.2f}")
    print()
    print("★ 조각 단위 — 중절모 vs 위저드햇")
    h = G.load_handoff(); p = G.load_pack("pack_detail_r3")
    for bank, k in ((h, "fedora"), (p, "pack_arcane_head_wizard_hat"), (p, "pack_arcane_neck_moon_clasp")):
        print(f"── {k}")
        for name, tone, A, s, v in dict(rows(bank))[k]:
            print(f"   {name:<18} t{tone}  면적 {A:>6.1f}pt²  생존 {s*100:>5.1f}%  ⇒ 읽히는 색면 {v:>6.1f}pt²"
                  + ("   ★ 4pt² 미만 = 색이 아니라 점" if v < 4.0 else ""))
