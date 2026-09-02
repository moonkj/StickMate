# -*- coding: utf-8 -*-
"""팩 12색을 **규칙 하나로** 유도한다. 사람이 hex를 고르지 않는다.

  주색  = 상자 안(S>=0.42, V 0.55~0.80) ∩ 자립 대역(band.py) 에서 **채도 최대**
  보조색 = 같은 색상각 · 같은 대역 · **채도 최소(S=0.42 쪽)** — 같은 재질의 다른 부품
  그늘색 = FillOutlineColor(주색) x0.62 파생 — 값을 적지 않는다

    python3 derive_packs.py
"""
import colorlab as C
import band

LO, HI, _ = band.limits()
BD = band.BACKDROPS

PACK_HUES = [("오피스 워커", 222.0, "군청 — 사무용 잉크/정장"),
             ("사이버 아포칼립스", 172.0, "독성 청록 — 형광 배선"),
             ("네온 낙서", 312.0, "마젠타 — 스프레이"),
             ("스포츠 이펙트", 8.0, "주홍 — 유니폼/트랙"),
             ("컬러 잉크", 268.0, "보라 잉크 (유저가 각도를 돌린다 · 상점 표시 기본값)"),
             ("밀리터리", 80.0, "올리브 — 캔버스/야전")]


def in_band(c):
    return LO <= C.L(c) <= HI


def chroma(c):
    a = C.lab(c)
    return (a[1] ** 2 + a[2] ** 2) ** 0.5


def pick(hue, want_max_chroma):
    best = None
    for si in range(42, 101):
        for vi in range(55, 81):
            c = C.hsv_to_rgb(hue / 360.0, si / 100.0, vi / 100.0)
            if not in_band(c) or C.worn(c) != c:
                continue
            k = chroma(c) if want_max_chroma else -chroma(c)
            # 동률이면 두 무대 대비가 균형에 가까운 쪽
            bal = -abs(min(C.CR(c, b) for _, b in BD) - 3.50)
            key = (round(k, 1), round(bal, 3))
            if best is None or key > best[0]:
                best = (key, c)
    return best[1] if best else None


if __name__ == "__main__":
    C.calibrate()
    print(f"자립 대역 L ∈ [{LO:.4f}, {HI:.4f}] · 상자 S>=0.42 V 0.55~0.80 · WornColor 항등\n")
    print(f"{'팩':16s} {'H':>6s} {'주색':>9s} {'L':>7s} {'C*':>5s} {'최악':>5s} | "
          f"{'보조색':>9s} {'L':>7s} {'C*':>5s} {'최악':>5s} | {'ΔE':>5s} {'그늘':>9s}")
    rows = []
    for name, h, why in PACK_HUES:
        p = pick(h, True)
        s = pick(h, False)
        wp = min(C.CR(p, b) for _, b in BD)
        ws = min(C.CR(s, b) for _, b in BD)
        rows.append((name, h, p, s))
        print(f"{name:16s} {h:6.1f} {C.rgb2hex(p):>9s} {C.L(p):7.4f} {chroma(p):5.1f} {wp:5.2f} | "
              f"{C.rgb2hex(s):>9s} {C.L(s):7.4f} {chroma(s):5.1f} {ws:5.2f} | "
              f"{C.dE(p, s):5.1f} {C.rgb2hex(C.fill_outline(p)):>9s}")
    print()
    print("배경 4종별 최악 (12색 전체)")
    for nm, b in BD:
        w = min((min(C.CR(p, b), C.CR(s, b)), n) for n, h, p, s in rows)
        print(f"  {nm:22s} 최악 {w[0]:5.2f}:1  ({w[1]})")
    print()
    print("PACKS = [")
    for name, h, p, s in rows:
        print(f'    ("{name}", {h}, "{C.rgb2hex(p)}", "{C.rgb2hex(s)}"),')
    print("]")
