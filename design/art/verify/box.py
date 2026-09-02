# -*- coding: utf-8 -*-
"""§1~§3  "몸 위의 색 상자"가 무엇을 지우는가 / 무엇을 남기는가.

    python3 box.py

상자 = ItemCatalog.WornColor 가 강제하는 (S >= 0.42, V in [0.55, 0.80]).
이 파일은 **의견을 내지 않는다.** 상자의 기하를 재고, 그 안에서 무엇이 가능한지만 센다.
"""
import colorlab as C
import shipped as S

W, K = (255, 255, 255), (0, 0, 0)
BAR = "-" * 78


def sec(t):
    print("\n" + "=" * 78 + f"\n{t}\n" + "=" * 78)


# ---------------------------------------------------------------------------
def s1_handoff_ramps():
    sec("§1-1  핸드오프 등급 램프 2종을 상자에 통과시킨다")
    ramps = {
        "카드 등급색": [("일반", "#8A8F98"), ("희귀", "#6E9BE8"), ("영웅", "#B07BE0"), ("전설", "#E0B24A")],
        "착용 오버레이": [("일반", "#D8B27A"), ("희귀", "#7FB0F2"), ("영웅", "#C08FEC"), ("전설", "#F0C25C")],
    }
    for label, ramp in ramps.items():
        print(f"\n[{label}]")
        print(f"  {'등급':6s} {'카탈로그':9s} {'H':>6s} {'L':>7s} | {'몸(worn)':9s} {'H':>6s} {'L':>7s} "
              f"{'ΔE(카드↔몸)':>11s}")
        wr = []
        for name, hx in ramp:
            c = C.hex2rgb(hx)
            w = C.worn(c)
            wr.append((name, w))
            print(f"  {name:6s} {hx:9s} {C.hue_deg(c):6.1f} {C.L(c):7.4f} | "
                  f"{C.rgb2hex(w):9s} {C.hue_deg(w):6.1f} {C.L(w):7.4f} {C.dE(c, w):11.1f}")
        print(f"  -- 몸 위 인접 등급 분리 --")
        for i in range(3):
            a, b = wr[i][1], wr[i + 1][1]
            ch = max(abs(a[j] - b[j]) for j in range(3))
            print(f"     {wr[i][0]} ↔ {wr[i+1][0]:6s} CR {C.CR(a, b):5.2f}:1  ΔE {C.dE(a, b):5.1f}  "
                  f"최대채널차 {ch:3d}/255")
        a, b = wr[0][1], wr[3][1]
        ch = max(abs(a[j] - b[j]) for j in range(3))
        print(f"     ★ 일반 ↔ 전설  CR {C.CR(a, b):5.2f}:1  ΔE {C.dE(a, b):5.1f}  최대채널차 {ch:3d}/255")
        # 단조성
        ls = [C.L(w) for _, w in wr]
        mono = all(ls[i] < ls[i + 1] for i in range(3))
        print(f"     휘도 단조 증가: {'예' if mono else '★ 아니오 — 서열이 뒤집힌다'}  "
              f"L = {' < '.join(f'{v:.4f}' for v in ls)}")


def s1_r_pinning():
    sec("§1-2  왜 무너지는가 — 따뜻한 색은 상자 안에서 R 채널이 **한 값에 못 박힌다**")
    print("  V가 0.80으로 눌리면 색상각 0~60°(빨강~노랑) 구간에서 R = round(255 x 0.80) = 204 (0xCC)로 고정된다.")
    print("  (그 구간에서 R = c + m = v·s + v(1−s) = v 이기 때문이다. S가 무엇이든 상관없다.)\n")
    print(f"  {'H':>5s} {'S=0.42':>9s} {'S=0.70':>9s} {'S=1.00':>9s}")
    for h in range(0, 61, 10):
        row = [C.rgb2hex(C.hsv_to_rgb(h / 360.0, s, 0.80)) for s in (0.42, 0.70, 1.00)]
        print(f"  {h:5d} {row[0]:>9s} {row[1]:>9s} {row[2]:>9s}")
    n = sum(1 for h in range(0, 60) for s in (0.42, 0.7, 1.0)
            if C.hsv_to_rgb(h / 360.0, s, 0.80)[0] == 204)
    print(f"\n  검산: 0~59° x S 3종 = {60*3}건 중 R==204 인 것 {n}건")


def s1_shipped():
    sec("§1-3  출하된 42종 — 카드 색과 몸 색은 지금 **얼마나 다른가**")
    items = S.item_colors()
    prim = sorted({c for v in items.values() for c in v["tones"].get(0, ())}, key=C.hue_deg)
    sec_ = sorted({c for v in items.values() for c in v["tones"].get(1, ())}, key=C.hue_deg)
    allc = sorted(set(prim) | set(sec_), key=C.hue_deg)
    moved = [(c, C.worn(c)) for c in allc if C.worn(c) != c]
    print(f"  고유 색 {len(allc)}종 (주 {len(prim)} / 보 {len(sec_)})")
    print(f"  WornColor가 **바꾸는** 색 {len(moved)}종 / 그대로 두는 색 {len(allc)-len(moved)}종")
    if moved:
        worst = max(moved, key=lambda p: C.dE(*p))
        print(f"  카드↔몸 ΔE 최악: {C.rgb2hex(worst[0])} -> {C.rgb2hex(worst[1])}  ΔE {C.dE(*worst):.1f}")
        print(f"  카드↔몸 ΔE 중앙 {sorted(C.dE(*p) for p in moved)[len(moved)//2]:.1f} / "
              f"평균 {sum(C.dE(*p) for p in moved)/len(moved):.1f}")
    wornset = [C.worn(c) for c in allc]
    uniq = len(set(wornset))
    print(f"  몸 위 고유 색: {uniq}종 (카탈로그 {len(allc)}종에서 {len(allc)-uniq}종이 합쳐진다)")
    # 최소 쌍 거리
    pairs = []
    for i in range(len(wornset)):
        for j in range(i + 1, len(wornset)):
            pairs.append((C.dE(wornset[i], wornset[j]), allc[i], allc[j]))
    pairs.sort()
    print("  몸 위에서 가장 가까운 5쌍 (ΔE):")
    for d, a, b in pairs[:5]:
        print(f"    ΔE {d:5.1f}  {C.rgb2hex(a)}->{C.rgb2hex(C.worn(a))}   "
              f"{C.rgb2hex(b)}->{C.rgb2hex(C.worn(b))}")
    # R=204 로 못박힌 수
    pinned = [c for c in allc if C.worn(c)[0] == 204]
    print(f"  ★ 몸 위에서 R==204(0xCC)로 못박힌 색: {len(pinned)}/{len(allc)}종")


def s1_span():
    sec("§1-4  상자가 한 색상각에 허용하는 **최대 명도 폭**")
    print("  가장 어두운 in-box = (S=1.00, V=0.55) / 가장 밝은 in-box = (S=0.42, V=0.80)")
    print(f"  {'H':>5s} {'어두운 끝':>10s} {'L':>7s} {'밝은 끝':>10s} {'L':>7s} {'CR':>6s}")
    worst = (99, None)
    best = (0, None)
    for h in range(0, 360, 30):
        d = C.hsv_to_rgb(h / 360.0, 1.00, 0.55)
        b = C.hsv_to_rgb(h / 360.0, 0.42, 0.80)
        cr = C.CR(d, b)
        worst = min(worst, (cr, h))
        best = max(best, (cr, h))
        print(f"  {h:5d} {C.rgb2hex(d):>10s} {C.L(d):7.4f} {C.rgb2hex(b):>10s} {C.L(b):7.4f} {cr:6.2f}")
    print(f"\n  상자 안 같은 색상각 최대 대비: 최악 {worst[0]:.2f}:1 (H={worst[1]}) / "
          f"최고 {best[0]:.2f}:1 (H={best[1]})")
    print("  → 4단 램프를 이 폭에 넣으면 인접 단 대비는 그 네제곱근 수준이다:")
    for cr, h in (worst, best):
        print(f"     H={h:3d}: 전체 {cr:.2f} → 인접 단 {cr ** (1/3):.2f}:1")


def s2_ink():
    sec("§2  잉크 분리 — 상자 안의 색이 흰 잉크/검은 잉크와 3.0을 넘는가")
    print("  장비는 캐릭터 선 **위에** 얹힌다. 잉크와 안 벌어지면 장비가 몸에 녹는다.")
    print("  (WornColor의 V창 0.55~0.80이 존재하는 이유가 정확히 이것이다 — 실제로 되는지 잰다.)\n")
    print(f"  {'H':>5s} | {'가장 밝은 in-box':>16s} {'vs 흰':>7s} | {'가장 어두운 in-box':>18s} {'vs 검':>7s}")
    fw, fb = [], []
    for h in range(0, 360, 15):
        b = C.hsv_to_rgb(h / 360.0, 0.42, 0.80)     # 흰 잉크와 가장 붙는 최악
        d = C.hsv_to_rgb(h / 360.0, 1.00, 0.55)     # 검은 잉크와 가장 붙는 최악
        cw, cb = C.CR(b, W), C.CR(d, K)
        fw.append((cw, h, b))
        fb.append((cb, h, d))
        flagw = "  ✘" if cw < 3.0 else ""
        flagb = "  ✘" if cb < 3.0 else ""
        print(f"  {h:5d} | {C.rgb2hex(b):>16s} {cw:7.2f}{flagw:3s} | {C.rgb2hex(d):>18s} {cb:7.2f}{flagb}")
    fw.sort(); fb.sort()
    nw = sum(1 for c, _, _ in fw if c < 3.0)
    nb = sum(1 for c, _, _ in fb if c < 3.0)
    print(f"\n  ★ 흰 잉크 대비 3.0 미달: {nw}/{len(fw)} 색상각. 최악 {fw[0][0]:.2f}:1 "
          f"(H={fw[0][1]}, {C.rgb2hex(fw[0][2])})")
    print(f"  ★ 검은 잉크 대비 3.0 미달: {nb}/{len(fb)} 색상각. 최악 {fb[0][0]:.2f}:1 "
          f"(H={fb[0][1]}, {C.rgb2hex(fb[0][2])})")


def s3_selfsufficient():
    sec("§3  임의 바탕화면 — 흑/백 양 끝에서 동시에 3.0을 넘는 색이 상자 안에 있는가")
    print("  흰 바탕에서 CR>=3.0  ⇔  L <= 1.05/3 − 0.05 = 0.3000")
    print("  검은 바탕에서 CR>=3.0 ⇔  L >= 3(0.05) − 0.05 = 0.1000")
    print("  → 자립 대역 = L ∈ [0.1000, 0.3000]. 상자와 겹치는가?\n")
    print(f"  {'H':>5s} {'in-box L 최소':>13s} {'in-box L 최대':>13s} {'자립 교집합':>12s} {'예시색':>9s} "
          f"{'vs흰':>6s} {'vs검':>6s}")
    ok_hues = 0
    for h in range(0, 360, 15):
        best = None
        lmin, lmax = 9, -1
        for si in range(42, 101, 2):
            for vi in range(55, 81):
                c = C.hsv_to_rgb(h / 360.0, si / 100.0, vi / 100.0)
                l = C.L(c)
                lmin, lmax = min(lmin, l), max(lmax, l)
                m = min(C.CR(c, W), C.CR(c, K))
                if best is None or m > best[0]:
                    best = (m, c)
        inter = 0.1000 <= lmax and lmin <= 0.3000
        if best[0] >= 3.0:
            ok_hues += 1
        print(f"  {h:5d} {lmin:13.4f} {lmax:13.4f} {'있음' if inter else '없음':>12s} "
              f"{C.rgb2hex(best[1]):>9s} {C.CR(best[1], W):6.2f} {C.CR(best[1], K):6.2f}")
    print(f"\n  ★ 흑·백 양 끝 동시 3.0을 만족하는 색이 상자 안에 존재하는 색상각: {ok_hues}/24")


if __name__ == "__main__":
    C.calibrate()
    s1_handoff_ramps()
    s1_r_pinning()
    s1_shipped()
    s1_span()
    s2_ink()
    s3_selfsufficient()
