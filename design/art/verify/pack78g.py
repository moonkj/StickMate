# -*- coding: utf-8 -*-
"""§8 — 확정안 미세조정 + 최종 전량 검산."""
import sys
import colorlab as C
import cvd
import band
import pack78 as P

C.calibrate(); cvd.calibrate(); print()
packs = P.parse_frozen_packs(); ramp = P.parse_rarity_ramp()
reserved = P.parse_reserved(); catalog = P.parse_catalog_colors(); floors = P.parse_contrast_floors()
if not P.selftest_parsers(packs, ramp, reserved, P.parse_worn_box(), floors, catalog):
    sys.exit("파서 교정 실패")
print()
existing = [(f"{n}주", p) for n, h, p, s in packs] + [(f"{n}보", s) for n, h, p, s in packs]
hues6 = sorted(h for _, h, _, _ in packs)
LO, HI, _ = band.limits()

def margins(hd):
    pr, se = P.pick(float(hd), True), P.pick(float(hd), False)
    if pr is None or se is None:
        return None
    ok, fails, met = P.full_gate("c", float(hd), pr, se, existing, catalog, ramp, reserved, floors)
    return (ok, pr, se,
            min(met["주색_카탈로그ΔE"][0], met["보조색_카탈로그ΔE"][0]),
            min(met["주색_팩ΔE"][0], met["보조색_팩ΔE"][0]),
            min(met["주색_등급ΔE"], met["보조색_등급ΔE"]),
            min(met["주색_배경최악"], met["보조색_배경최악"]))

print("=" * 100)
print("§8-1. 광부 미세조정 (호박 대역 18~36°) — 목적: min(카탈로그ΔE, 팩ΔE) 최대")
print("=" * 100)
print(f"  {'H':>4s} {'주색':>9s} {'보조색':>9s} {'카탈로그ΔE':>10s} {'팩ΔE':>8s} {'등급ΔE':>7s} {'배경최악':>7s} {'여유':>7s}")
bestm = None
for hd in range(18, 37):
    m = margins(hd)
    if m is None or not m[0]:
        continue
    ok, pr, se, dc, dp, dr, wb = m
    slack = min(dc - 8.0, dp - 7.8)
    print(f"  {hd:4d} {C.rgb2hex(pr):>9s} {C.rgb2hex(se):>9s} {dc:10.2f} {dp:8.2f} {dr:7.2f} {wb:7.2f} {slack:7.2f}")
    if bestm is None or slack > bestm[0]:
        bestm = (slack, hd, pr, se)
print(f"  ★ 최대 여유 = {bestm[1]}° (여유 {bestm[0]:.2f})")

print()
print("=" * 100)
print("§8-2. 대마법사 미세조정 (에메랄드 대역 120~157°) — 같은 목적함수")
print("=" * 100)
print(f"  {'H':>4s} {'주색':>9s} {'보조색':>9s} {'카탈로그ΔE':>10s} {'팩ΔE':>8s} {'등급ΔE':>7s} {'배경최악':>7s} {'여유':>7s}")
besta = None
for hd in range(120, 158):
    m = margins(hd)
    if m is None or not m[0]:
        continue
    ok, pr, se, dc, dp, dr, wb = m
    slack = min(dc - 8.0, dp - 7.8)
    print(f"  {hd:4d} {C.rgb2hex(pr):>9s} {C.rgb2hex(se):>9s} {dc:10.2f} {dp:8.2f} {dr:7.2f} {wb:7.2f} {slack:7.2f}")
    if besta is None or slack > besta[0]:
        besta = (slack, hd, pr, se)
print(f"  ★ 최대 여유 = {besta[1]}° (여유 {besta[0]:.2f})")
