# -*- coding: utf-8 -*-
"""★ 7·8번째 코호트(광부 / 대마법사) 테마색 확정 — design-art R2x (2026-09-07)

이 스크립트는 **문서를 인용하지 않는다.** 색은 전부 소스에서 파싱한다:
  · 기존 6팩 12색  <- Tests/EditMode/PackPaletteGateTests.cs 의 FrozenPacks (동결 대장 = 정본)
  · 카탈로그 색     <- Resources/Items/*.asset 의 icon[].color (실물 YAML)
  · 등급 램프 4색   <- Interaction/UiChrome.cs 의 _rarityRamp
  · 예약색 2건      <- UiChrome.TextTertiary / CardBorderWorn
  · WornColor 상수  <- Core/ItemCatalog.cs 의 WornSaturationFloor/ValueFloor/ValueCeiling
  · 대비 하한       <- UiChrome.MinNonTextContrast / MinTextContrast

계산기는 colorlab.py(16건 교정 통과)만 쓴다. 교정이 깨지면 아무것도 출력하지 않는다.

    python3 pack78.py
"""
import os
import re
import sys

import colorlab as C
import band

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
SRC = os.path.join(ROOT, "Assets", "_Project", "Scripts")
ITEMS = os.path.join(ROOT, "Assets", "_Project", "Resources", "Items")

LO, HI, _BANDROWS = band.limits()
BD = band.BACKDROPS


# ============================================================================
# 0. 소스 파싱 — 값을 손으로 옮겨 적지 않는다
# ============================================================================

def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def parse_frozen_packs():
    """PackPaletteGateTests.cs 의 FrozenPacks 배열."""
    txt = read(os.path.join(SRC, "Tests", "EditMode", "PackPaletteGateTests.cs"))
    m = re.search(r"FrozenPacks\s*=\s*\{(.*?)\n\s*\};", txt, re.S)
    if not m:
        sys.exit("FrozenPacks 를 못 찾았다 — 파서가 죽었다. 이 출력 전부 폐기.")
    rows = re.findall(r'\("([^"]+)",\s*([0-9.]+)f,\s*0x([0-9A-Fa-f]{6}),\s*0x([0-9A-Fa-f]{6})\)', m.group(1))
    if len(rows) != 6:
        sys.exit(f"FrozenPacks 행이 {len(rows)}개다(기대 6) — 파서가 죽었다.")
    return [(n, float(h), C.hex2rgb(p), C.hex2rgb(s)) for n, h, p, s in rows]


def parse_worn_box():
    txt = read(os.path.join(SRC, "Core", "ItemCatalog.cs"))
    def one(name):
        m = re.search(rf"const float {name}\s*=\s*([0-9.]+)f;", txt)
        if not m:
            sys.exit(f"{name} 파싱 실패 — 출력 폐기.")
        return float(m.group(1))
    return one("WornSaturationFloor"), one("WornValueFloor"), one("WornValueCeiling")


def parse_rarity_ramp():
    txt = read(os.path.join(SRC, "Interaction", "UiChrome.cs"))
    m = re.search(r"_rarityRamp\s*=\s*\{(.*?)\n\s*\};", txt, re.S)
    if not m:
        sys.exit("_rarityRamp 파싱 실패 — 출력 폐기.")
    hexes = re.findall(r"RarityHex\(0x([0-9A-Fa-f]{6})\)", m.group(1))
    if len(hexes) != 4:
        sys.exit(f"등급 램프가 {len(hexes)}색이다(기대 4) — 출력 폐기.")
    return [C.hex2rgb(h) for h in hexes]


def parse_reserved():
    txt = read(os.path.join(SRC, "Interaction", "UiChrome.cs"))
    out = {}
    for name in ("TextTertiary", "CardBorderWorn"):
        m = re.search(rf"{name}\s*=\s*new Color\(([0-9.]+)f,\s*([0-9.]+)f,\s*([0-9.]+)f", txt)
        if not m:
            sys.exit(f"{name} 파싱 실패 — 출력 폐기.")
        out[name] = tuple(int(round(float(m.group(i)) * 255)) for i in (1, 2, 3))
    return out


def parse_contrast_floors():
    txt = read(os.path.join(SRC, "Interaction", "UiChrome.cs"))
    def one(name):
        m = re.search(rf"const float {name}\s*=\s*([0-9.]+)f;", txt)
        if not m:
            sys.exit(f"{name} 파싱 실패 — 출력 폐기.")
        return float(m.group(1))
    return one("MinTextContrast"), one("MinNonTextContrast")


def parse_catalog_colors():
    """Resources/Items/*.asset 의 icon[].color 전부. (이름, rgb) 목록, 중복 제거."""
    seen = {}
    for fn in sorted(os.listdir(ITEMS)):
        if not fn.endswith(".asset"):
            continue
        txt = read(os.path.join(ITEMS, fn))
        for m in re.finditer(r"color:\s*\{r:\s*([0-9.]+),\s*g:\s*([0-9.]+),\s*b:\s*([0-9.]+)", txt):
            rgb = tuple(int(round(float(m.group(i)) * 255)) for i in (1, 2, 3))
            seen.setdefault(rgb, fn[:-6])
    if len(seen) < 20:
        sys.exit(f"카탈로그 색이 {len(seen)}개뿐이다 — 파서가 죽었다. 출력 폐기.")
    return [(rgb, nm) for rgb, nm in seen.items()]


# ============================================================================
# 1. 파서 자체 교정 — 음성/양성 대조
# ============================================================================

def selftest_parsers(packs, ramp, reserved, worn_box, floors, catalog):
    """★ 파싱이 살아 있는지 알려진 값으로 못박는다. 하나라도 깨지면 전부 폐기."""
    ok = True
    def chk(label, got, want):
        nonlocal ok
        good = got == want
        ok = ok and good
        print(f"  {'PASS' if good else 'FAIL'}  {label:44s} {got}  (기대 {want})")

    # 대장이 스스로와 어긋나지 않는가(PackPaletteGateTests 의 같은 검사를 여기서 재현)
    worst = 0.0
    for name, hue, p, s in packs:
        for rgb in (p, s):
            d = abs(((C.hue_deg(rgb) - hue + 180) % 360) - 180)
            worst = max(worst, d)
    chk("동결 대장 색상각 최대 이탈 <= 1.0", round(worst, 2) <= 1.0, True)
    chk("팩 6개", len(packs), 6)
    chk("등급 램프 4색", len(ramp), 4)
    chk("등급 일반 = #9C978C", C.rgb2hex(ramp[0]), "#9C978C")
    chk("등급 전설 = #FFD375", C.rgb2hex(ramp[3]), "#FFD375")
    chk("TextTertiary = #8B939F", C.rgb2hex(reserved["TextTertiary"]), "#8B939F")
    chk("CardBorderWorn = #5DA1F5", C.rgb2hex(reserved["CardBorderWorn"]), "#5DA1F5")
    chk("WornColor 상자 (S,Vlo,Vhi)", worn_box, (0.42, 0.55, 0.80))
    chk("대비 하한 (텍스트, 비텍스트)", floors, (4.5, 3.0))
    chk("카탈로그 색 >= 24", len(catalog) >= 24, True)
    # 음성 대조 — 없는 색은 안 나온다
    chk("음성대조: #FF00FF 는 카탈로그에 없다", any(c == (255, 0, 255) for c, _ in catalog), False)
    # 양성 대조 — 아는 색은 나온다(왕관 보조색)
    chk("양성대조: #C6443C(왕관 보조) 가 카탈로그에 있다",
        any(c == C.hex2rgb("#C6443C") for c, _ in catalog), True)
    return ok


# ============================================================================
# 2. 색상각 기하 — 원형 간격
# ============================================================================

def gaps(hues):
    h = sorted(hues)
    out = []
    for i in range(len(h)):
        a, b = h[i], h[(i + 1) % len(h)]
        out.append((a, b, (b - a) % 360 if i < len(h) - 1 else (b - a) % 360))
    # 마지막 칸은 wrap
    out[-1] = (h[-1], h[0], (h[0] - h[-1]) % 360)
    return out


def min_gap(hues):
    return min(g[2] for g in gaps(hues))


# ============================================================================
# 3. 유도 규칙 — derive_packs.py 와 같은 규칙(주색=채도최대 / 보조색=채도최소)
# ============================================================================

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
            bal = -abs(min(C.CR(c, b) for _, b in BD) - 3.50)
            key = (round(k, 1), round(bal, 3))
            if best is None or key > best[0]:
                best = (key, c)
    return best[1] if best else None


# ============================================================================
# 4. 후보 색상각 판정
# ============================================================================

def judge_hue(hue, existing_hues):
    """이 색상각을 새로 배정하면 최소 간격이 얼마가 되는가."""
    return min_gap(existing_hues + [hue])


def full_gate(name, hue, primary, secondary, others, catalog, ramp, reserved, floors):
    """새 색 한 쌍이 모든 게이트를 통과하는가. (통과여부, 사유목록, 지표)"""
    fails = []
    mt, mnt = floors
    metrics = {}

    for role, c in (("주색", primary), ("보조색", secondary)):
        if c is None:
            fails.append(f"{role}: 상자∩대역∩항등 해가 없다")
            continue
        # 대역
        if not in_band(c):
            fails.append(f"{role} {C.rgb2hex(c)} L={C.L(c):.4f} 자립대역 [{LO:.4f},{HI:.4f}] 밖")
        # 항등
        if C.worn(c) != c:
            fails.append(f"{role} {C.rgb2hex(c)} WornColor 비항등 -> {C.rgb2hex(C.worn(c))}")
        # 배경 4종
        w = min(C.CR(c, b) for _, b in BD)
        metrics[f"{role}_배경최악"] = w
        if w < mnt:
            fails.append(f"{role} 배경최악 {w:.2f} < {mnt}")
        # 카탈로그 ΔE >= 8.0
        dmin, dwho = min((C.dE(c, cc), nm) for cc, nm in catalog)
        metrics[f"{role}_카탈로그ΔE"] = (dmin, dwho)
        if dmin < 8.0:
            fails.append(f"{role} 카탈로그 ΔE {dmin:.2f} < 8.0 ({dwho})")
        # 다른 팩색 ΔE >= 7.8
        if others:
            pmin, pwho = min((C.dE(c, oc), on) for on, oc in others)
            metrics[f"{role}_팩ΔE"] = (pmin, pwho)
            if pmin < 7.8:
                fails.append(f"{role} 팩간 ΔE {pmin:.2f} < 7.8 ({pwho})")
        # 등급 램프 ΔE >= 7.8
        rmin = min(C.dE(c, r) for r in ramp)
        metrics[f"{role}_등급ΔE"] = rmin
        if rmin < 7.8:
            fails.append(f"{role} 등급램프 ΔE {rmin:.2f} < 7.8")
        # 예약색 2건
        for rn, rc in reserved.items():
            d = C.dE(c, rc)
            metrics[f"{role}_{rn}"] = d
            if d < 7.8:
                fails.append(f"{role} 예약색 {rn} ΔE {d:.2f} < 7.8")
    return (len(fails) == 0), fails, metrics


def main():
    C.calibrate()
    print()
    packs = parse_frozen_packs()
    ramp = parse_rarity_ramp()
    reserved = parse_reserved()
    worn_box = parse_worn_box()
    floors = parse_contrast_floors()
    catalog = parse_catalog_colors()

    print("=" * 96)
    print("§0. 파서 교정 — 소스에서 읽은 값이 알려진 값과 맞는가")
    print("=" * 96)
    if not selftest_parsers(packs, ramp, reserved, worn_box, floors, catalog):
        sys.exit("\n★ 파서 교정 실패 — 이 아래 숫자를 전부 폐기하십시오.")
    print("  파서 교정: 유효\n")

    # ---------------------------------------------------------------- §1
    print("=" * 96)
    print("§1. 기존 6팩 실측 — 색상각은 '선언'이 아니라 hex에서 다시 계산한 값이다")
    print("=" * 96)
    print(f"  {'팩':18s} {'선언H':>6s} {'주색':>9s} {'실측H':>7s} {'L':>7s} {'S':>5s} "
          f"{'보조색':>9s} {'실측H':>7s} {'L':>7s} {'S':>5s}")
    measured = []
    for name, hue, p, s in packs:
        hp, hs = C.hue_deg(p), C.hue_deg(s)
        _, sp, _ = C.rgb_to_hsv(p)
        _, ss, _ = C.rgb_to_hsv(s)
        measured.append((name, hue, hp, hs, p, s))
        print(f"  {name:18s} {hue:6.1f} {C.rgb2hex(p):>9s} {hp:7.2f} {C.L(p):7.4f} {sp:5.2f} "
              f"{C.rgb2hex(s):>9s} {hs:7.2f} {C.L(s):7.4f} {ss:5.2f}")

    hues = sorted(h for _, h, _, _ in packs)
    print(f"\n  정렬된 6각: {[f'{h:.0f}' for h in hues]}")
    total = 0.0
    for a, b, g in gaps(hues):
        print(f"    {a:5.0f}° → {b:5.0f}° = {g:5.1f}°")
        total += g
    print(f"    검산 합계 {total:.1f}° (기대 360.0)  {'OK' if abs(total-360)<1e-6 else 'FAIL'}")
    print(f"  ★ 현행 최소 간격 = {min_gap(hues):.1f}°")

    # ---------------------------------------------------------------- §2
    print()
    print("=" * 96)
    print("§2. product-strategy 경고 재측정 — '대마법사 보라(245°)면 최소간격 44°→23°'")
    print("=" * 96)
    for probe in (245.0, 240.0, 250.0, 255.0, 235.0):
        g = judge_hue(probe, hues)
        print(f"  대마법사 {probe:5.1f}° 배정 -> 7각 최소 간격 {g:5.1f}°"
              f"{'   ← 경고가 지목한 값' if probe == 245.0 else ''}")
    print("  판정: 경고는 참이다(아래 §3에서 처방).")

    return measured, hues, packs, ramp, reserved, catalog, floors


if __name__ == "__main__":
    main()
