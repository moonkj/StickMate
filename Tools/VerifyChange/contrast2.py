#!/usr/bin/env python3
"""verify-change 독립 대비 계산기 (2026-09-02).

★ 규칙: 교정이 깨지면 그 뒤 숫자는 전부 폐기한다.
   교정값 3종 — 흰/검 21.0, 동일색 1.0, #767676/흰 4.5422 (WCAG 기준점).
Tools/ContrastProbe 와 코드를 공유하지 않는다(생성기·검사기가 같이 틀리는 형태를 피한다).
"""
import sys

def srgb_to_lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4

def lum(hexstr):
    h = hexstr.strip().lstrip('#')
    if len(h) != 6:
        raise ValueError("hex must be 6 digits: " + hexstr)
    r, g, b = (int(h[i:i+2], 16) for i in (0, 2, 4))
    return 0.2126*srgb_to_lin(r) + 0.7152*srgb_to_lin(g) + 0.0722*srgb_to_lin(b)

def ratio(a, b):
    la, lb = lum(a), lum(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)

CAL = [("FFFFFF", "000000", 21.0, 1e-9),
       ("D6DBE3", "D6DBE3", 1.0, 1e-9),
       ("767676", "FFFFFF", 4.5422, 5e-5)]

def calibrate(verbose=True):
    ok = True
    for a, b, want, tol in CAL:
        got = ratio(a, b)
        good = abs(got - want) <= tol
        ok = ok and good
        if verbose:
            print(f"  [CAL] #{a} vs #{b}: got={got:.6f} want={want} -> {'OK' if good else 'FAIL'}")
    return ok

if __name__ == "__main__":
    print("교정:")
    if not calibrate():
        print("!! 교정 실패 — 이 계산기의 모든 수치를 폐기한다"); sys.exit(2)
    args = sys.argv[1:]
    if not args:
        sys.exit(0)
    if len(args) % 2 != 0:
        print("usage: contrast2.py <hexA> <hexB> [<hexA> <hexB> ...]"); sys.exit(2)
    print("측정:")
    for i in range(0, len(args), 2):
        a, b = args[i], args[i+1]
        print(f"  #{a.lstrip('#').upper()} vs #{b.lstrip('#').upper()}: {ratio(a,b):.4f}:1   (L_a={lum(a):.6f} L_b={lum(b):.6f})")
