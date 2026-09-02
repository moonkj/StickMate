# -*- coding: utf-8 -*-
"""R8 — 애셋 색 게이트. design-art §24-7 / §24-10 을 **내 자로 다시 잰다**(같은 명령 재실행이 아니다).

  · 고유색 / 아트 고유색 / 새 hex / 사라진 hex / 대역 밖 / WornColor 바이트 항등
  · 아이템별 주(tone0) ↔ 보조(tone1) ΔE
  · ★ 금지 검사 : 어떤 애셋 색도 「카테고리 틴트 4색」으로 **새로** 칠해지지 않았는가
        (리더 판정 「하지 마라」 — 규칙 (2) 폐지)

    python3 r8_colorgate.py             # 지금 트리
    python3 r8_colorgate.py --control   # 양성 대조: 일부러 나쁜 값 -> 게이트가 빨개지는가
    python3 r8_colorgate.py --baseline out.json / --against out.json
"""
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "design", "art", "verify"))
import colorlab as C, band, shipped

TINTS = {"#CC5512": "TintHead", "#20878C": "TintEyes", "#5A8C3C": "TintNeck", "#955CCC": "TintBack"}
INK_MARKS = {"#D6DBE3", "#8B939F"}     # 잉크 표식 — 대역/항등 면제 (§24-7 먼지 t1)
LO, HI, _ = band.limits()
DE_DISC = 7.8


def census():
    ic = shipped.item_colors()
    rows, uniq = [], {}
    for name in sorted(ic):
        tones = ic[name]["tones"]
        got = {}
        for t in sorted(tones):
            for rgb in sorted(tones[t]):
                h = C.rgb2hex(rgb)
                uniq.setdefault(h, []).append((name, t))
                got.setdefault(t, []).append(h)
        rows.append((name, got))
    return rows, uniq


def measure(rows, uniq):
    art = {h for h in uniq if h not in INK_MARKS}
    out_of_band = []
    for h in sorted(art):
        l = C.L(C.hex2rgb(h))
        if not (LO - 1e-9 <= l <= HI + 1e-9):
            out_of_band.append((h, round(l, 4)))
    nonident = []
    for h in sorted(art):
        if not C.is_worn_fixed(C.hex2rgb(h)):
            nonident.append((h, C.rgb2hex(C.worn(C.hex2rgb(h)))))
    de = []
    for name, got in rows:
        if 0 in got and 1 in got:
            for a in got[0]:
                for b in got[1]:
                    de.append((round(C.dE(C.hex2rgb(a), C.hex2rgb(b)), 2), name, a, b))
    de.sort()
    return dict(uniq_n=len(uniq), art_n=len(art), art=sorted(art),
                uniq=sorted(uniq), out_of_band=out_of_band, nonident=nonident, de=de)


def report(m, label):
    print("╔══ %s ══╗" % label)
    print("  고유색(잉크 표식 포함) %d · 아트 고유색 %d" % (m["uniq_n"], m["art_n"]))
    print("  대역 밖 [%.4f, %.4f]      %d건  %s" % (LO, HI, len(m["out_of_band"]), m["out_of_band"] or ""))
    print("  WornColor 바이트 비항등    %d건  %s" % (len(m["nonident"]), m["nonident"] or ""))
    bad = [d for d in m["de"] if d[0] < DE_DISC]
    print("  주↔보조 ΔE 쌍 %d개 · 변별 하한 %.1f 미달 %d건" % (len(m["de"]), DE_DISC, len(bad)))
    for d in bad:
        print("     ★ %-22s %s / %s  ΔE %.2f" % (d[1], d[2], d[3], d[0]))
    print("  최소 3쌍: " + " · ".join("%s %.2f" % (d[1], d[0]) for d in m["de"][:3]))
    return len(m["out_of_band"]) == 0 and len(m["nonident"]) == 0


def tint_guard(rows):
    """금지 검사 — 틴트 4색이 **FX/PET tone1** 에 칠해져 있으면 규칙 (2) 부활이다."""
    hits = []
    for name, got in rows:
        if not (name.startswith("look_fx_") or name.startswith("look_pet_")): continue
        for t, hs in got.items():
            for h in hs:
                if h in TINTS: hits.append((name, t, h, TINTS[h]))
    return hits


def control():
    """양성 대조 — 게이트가 실제로 빨개지는지. 합성 데이터로 각 축을 하나씩 깨뜨린다."""
    print("╔══ 양성 대조 — 게이트가 실제로 잡는가 ══╗")
    ok = True
    C.calibrate(verbose=False)
    print("  [0] colorlab 교정 통과")

    r = [("x", {0: ["#FFFFFF"], 1: ["#000000"]})]
    u = {"#FFFFFF": [("x", 0)], "#000000": [("x", 1)]}
    m = measure(r, u)
    c1 = len(m["out_of_band"]) == 2
    print("  [1] 대역 밖 흰/검 2건 잡힘        %-5s (%d건)" % (c1, len(m["out_of_band"])))

    r = [("y", {0: ["#3378CC"], 1: ["#3378CC"]})]
    u = {"#3378CC": [("y", 0), ("y", 1)]}
    m = measure(r, u)
    c2 = m["de"] and abs(m["de"][0][0]) < 1e-9
    print("  [2] 동일색 쌍 ΔE 0.00 잡힘        %-5s (%.2f)" % (c2, m["de"][0][0]))

    r = [("look_fx_zz", {0: ["#9B7922"], 1: ["#5A8C3C"]})]
    c3 = len(tint_guard(r)) == 1
    print("  [3] FX tone1 틴트색 금지 검사 잡힘 %-5s" % c3)
    c3b = len(tint_guard([("look_fx_zz", {0: ["#9B7922"], 1: ["#988540"]})])) == 0
    print("  [4] 틴트 아닌 색은 안 잡힘(음성)   %-5s" % c3b)

    # WornColor 비항등 검사가 살아 있는가 — 대역 밖의 밝은 색은 반드시 잡혀야 한다.
    # ★ 첫 시안은 #5A8C3C(V=140/255 < 0.55)를 비항등 기대값으로 썼는데 **틀렸다**:
    #   클램프 0.55 -> 140.25 -> 8bit 반올림 140 이라 바이트가 같다(PALETTE_SPEC §25-2).
    #   기대값을 실측에 맞춰 고쳤다. 대조가 먼저 빨개졌고 그게 대조의 일이다.
    c4 = not C.is_worn_fixed(C.hex2rgb("#F0C25C"))          # -> #CCA54E
    c5 = C.is_worn_fixed(C.hex2rgb("#9B7922"))
    print("  [5] #F0C25C 비항등 / #9B7922 항등  %-5s / %-5s" % (c4, c5))
    ok = all([c1, c2, c3, c3b, c4, c5])
    print("  ⇒ %s" % ("대조 6/6 통과" if ok else "★ 대조 실패 — 판정 폐기"))
    return ok


if __name__ == "__main__":
    if "--control" in sys.argv:
        sys.exit(0 if control() else 1)
    C.calibrate(verbose=False)
    rows, uniq = census()
    m = measure(rows, uniq)
    ok = report(m, "지금 트리")
    hits = tint_guard(rows)
    print("  ★ FX/PET 조각에 남은 카테고리 틴트색 %d건  %s" % (len(hits), hits or "(없음)"))
    if "--baseline" in sys.argv:
        p = sys.argv[sys.argv.index("--baseline") + 1]
        json.dump(m, open(p, "w"), ensure_ascii=False, indent=1)
        print("  기준선 저장 -> " + p)
    if "--against" in sys.argv:
        base = json.load(open(sys.argv[sys.argv.index("--against") + 1]))
        new = set(m["uniq"]) - set(base["uniq"]); gone = set(base["uniq"]) - set(m["uniq"])
        print("\n╔══ 기준선 대조 ══╗")
        print("  새로 생긴 hex %d개 %s" % (len(new), sorted(new) or ""))
        print("  사라진 hex    %d개 %s" % (len(gone), sorted(gone) or ""))
        print("  고유색 %d -> %d · 아트 %d -> %d"
              % (base["uniq_n"], m["uniq_n"], base["art_n"], m["art_n"]))
    sys.exit(0 if ok else 1)
