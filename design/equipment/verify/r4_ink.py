# -*- coding: utf-8 -*-
"""R4 §2 핵심 — 「잉크 잠식률」. 조각이 **자기 윤곽선에 먹히는가**.

배경(프로덕션 사실):
  · 채움 조각의 윤곽색 = 캐릭터 잉크(HandoffLineBase: 채움이 있으면 p.Ink).
  · 획은 윤곽 위에 **중심 정렬**로 얹히므로 안쪽으로 W/2 만큼 먹는다.
  · 착용 배율 0.75 에서 W = 2.00 pt 이고 머리 지름은 11.63 pt 다.
  ⇒ 폭 4pt 짜리 보석에 2pt 윤곽을 두르면 **남는 재질색이 절반 이하**다. 그 조각은 화면에서 «검은 점»이다.

잠식률 = min(1, (둘레 × W/2) / 면적).  볼록 도형에서 정확하고, 오목에서는 과대평가(보수적)다.
★ 이 자는 «중절모가 왜 깔끔한가»를 한 줄로 설명한다: 중절모의 얇은 조각(챙 2장, 띠)은
  **noStroke=1(윤곽 없음)** 이고, 잉크는 «닫힌 윤곽»이 아니라 **열린 낱선 한 줄**(tone 4)로 바깥 가장자리에만 그어진다.
"""
import math, sys, collections
import r4_geom as G

def area(pts):
    s = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i]; x1, y1 = pts[(i + 1) % len(pts)]
        s += x0 * y1 - x1 * y0
    return abs(s) / 2.0

def engulf(p):
    """이 조각이 자기 윤곽선에 먹히는 비율. 윤곽이 없으면 0."""
    if not p["filled"] or p["noStroke"] or p.get("lineAlpha", 1.0) <= 0.0: return 0.0
    A = area(p["pts"])
    if A <= 0: return 1.0
    P = sum(G.seglens(p["pts"], True))
    return min(1.0, P * (G.W * p["strokeMult"]) / 2.0 / A)

def openink(pieces):
    """열린 낱선 잉크(tone 4 · filled=0 · loop=0) 개수 — 중절모 문법의 표식."""
    return sum(1 for p in pieces if p["tone"] == 4 and not p["filled"] and not p["loop"])

def report(bank, label, only=None):
    print("═" * 112); print(label); print("═" * 112)
    print(f"{'아이템':<30}{'채움':>4}{'윤곽有':>7}{'잠식>50%':>9}{'잠식>75%':>9}{'최악':>7}"
          f"{'평균(윤곽有)':>13}{'열린잉크선':>10}{'noStroke채움':>12}")
    print("─" * 112)
    agg = []
    for k, it in sorted(bank.items()):
        if only and k not in only: continue
        ps = G.body_pieces(it)
        fills = [p for p in ps if p["filled"]]
        outl = [p for p in fills if not p["noStroke"] and p.get("lineAlpha", 1.0) > 0]
        es = [engulf(p) for p in outl]
        worst = max(es) if es else 0.0
        mean = sum(es) / len(es) if es else 0.0
        row = (k, len(fills), len(outl), sum(1 for e in es if e > 0.5), sum(1 for e in es if e > 0.75),
               worst, mean, openink(ps), sum(1 for p in fills if p["noStroke"]))
        agg.append(row)
        print(f"{k[:30]:<30}{row[1]:>4}{row[2]:>7}{row[3]:>9}{row[4]:>9}{row[5]:>7.2f}{row[6]:>13.2f}{row[7]:>10}{row[8]:>12}")
    print("─" * 112)
    N = len(agg)
    print(f"{'평균':<30}{sum(r[1] for r in agg)/N:>4.1f}{sum(r[2] for r in agg)/N:>7.1f}"
          f"{sum(r[3] for r in agg)/N:>9.2f}{sum(r[4] for r in agg)/N:>9.2f}{sum(r[5] for r in agg)/N:>7.2f}"
          f"{sum(r[6] for r in agg)/N:>13.2f}{sum(r[7] for r in agg)/N:>10.2f}{sum(r[8] for r in agg)/N:>12.2f}")
    print()
    return agg

if __name__ == "__main__":
    print(f"# W = {G.W:.4f} R = 2.00 pt @ 배율0.75 · 머리 지름 11.63 pt\n")
    h = G.load_handoff(); n = G.load_neck6()
    report(h, "① 출하 인계본 12종")
    report({("neck_" + k): v for k, v in n.items()}, "② 출하 NECK 6종")
    report(G.load_pack("pack_detail_r2"), "③ 팩 R2")
    report(G.load_pack("pack_detail_r3"), "④ 팩 R3 (현행 · 출하 에셋과 동일 확인됨)")

    print("═" * 112); print("★ 조각 단위 — 중절모 vs 위저드햇 (같은 슬롯)"); print("═" * 112)
    for bank, k in ((h, "fedora"), (G.load_pack("pack_detail_r3"), "pack_arcane_head_wizard_hat"),
                    (G.load_pack("pack_detail_r3"), "pack_mine_head_miner_helmet")):
        print(f"── {k}")
        for p in G.body_pieces(bank[k]):
            A = area(p["pts"]) * G.R_PT_075**2
            e = engulf(p)
            tag = "  ★ 윤곽이 재질색을 절반 넘게 먹는다" if e > 0.5 else ""
            print(f"   {p['name']:<16} t{p['tone']} {'채움' if p['filled'] else '선  '} "
                  f"{'윤곽X' if p['noStroke'] else '윤곽O'}  면적 {A:>6.1f}pt²  잠식 {e*100:>5.1f}%{tag}")
