# -*- coding: utf-8 -*-
"""R4 — 조각 하나하나를 «착용 크기 pt»로 편다. 어떤 조각이 잉크 문턱 아래인지 눈이 아니라 숫자로."""
import sys, math
import r4_geom as G

def table(bank, keys, title):
    print("═" * 118); print(title); print("═" * 118)
    print(f"{'아이템':<24}{'조각':<14}{'t':>2}{'채':>3}{'층':>3}{'n':>4}{'긴변pt':>8}{'짧은변pt':>9}"
          f"{'둘레pt':>8}{'평균변pt':>9}{'최대꺾임°':>10}{'면오차pt':>9}")
    print("─" * 118)
    for k in keys:
        it = bank[k]
        for p in G.body_pieces(it):
            m = G.piece_metrics(p)
            bw, bh = sorted((m["bw"], m["bh"]), reverse=True)
            mark = ""
            if bh * G.R_PT_075 < 2.0: mark += " ▲짧은변<1획"
            if bw * G.R_PT_075 < 3.0: mark += " ▲긴변<1.5획"
            print(f"{k[:24]:<24}{p['name'][:14]:<14}{p['tone']:>2}{'●' if p['filled'] else '○':>3}"
                  f"{p['layer']:>3}{m['n']:>4}{bw*G.R_PT_075:>8.2f}{bh*G.R_PT_075:>9.2f}"
                  f"{m['perim']*G.R_PT_075:>8.1f}{m['segmean']*G.R_PT_075:>9.2f}{m['turnmax']:>10.1f}"
                  f"{m['sagmax']*G.R_PT_075:>9.3f}{mark}")
        print("─" * 118)

if __name__ == "__main__":
    print(f"# 착용(배율 0.75) R = {G.R_PT_075:.3f} pt · 획 W = 2.00 pt · 머리 지름 = {2*G.R_PT_075:.2f} pt")
    print("# ▲ = 그 조각이 스스로 획 하나(2.00pt)보다 얇거나(짧은변) 1.5획(3.00pt)보다 좁다(긴변) = 규칙1 잉크 사각형 하한 미만\n")
    h = G.load_handoff()
    table(h, ["fedora", "clothhat", "crown"], "① 출하 — 사용자가 지목한 중절모 + 같은 슬롯 2종")
    p3 = G.load_pack("pack_detail_r3")
    table(p3, ["pack_arcane_head_wizard_hat", "pack_cyber_head_patched_hood", "pack_mine_head_miner_helmet"],
          "② 팩 R3 — HEAD 3종 (중절모와 같은 자리)")
    table(p3, ["pack_arcane_eyes_astro_lens", "pack_arcane_neck_moon_clasp", "pack_arcane_back_moon_robe"],
          "③ 팩 R3 — 대마법사 나머지 3종 (동시 착용 케이스)")
