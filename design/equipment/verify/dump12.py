# -*- coding: utf-8 -*-
"""제안 A의 좌표 전문(부록용). 단위 = 머리 반경 R, 원점 = 각 아이템 로컬 원점."""
import sys, math; sys.path.insert(0,'.')
import appearance as A
from rig import W
def show(title, table):
    print("\n### %s" % title)
    for n, sh in table.items():
        if not sh: print("  %s — 월드 도형 없음" % n); continue
        print("  %s" % n)
        for s in sh:
            tone = "보조색" if s.tone == 1 else "주색  "
            kind = "닫힘" if s.loop else "열림"
            pts = "  ".join("(%+.4f,%+.4f)" % p for p in s.pts)
            print("    %-6s %s %s  %s" % (s.name, kind, tone, pts))
show("FX", A.FX_A); show("PET", A.PET_A)
print("\nW = %.6f R (배율 0.75) · 1.5W = %.6f R · 3.0W = %.6f R" % (W, 1.5*W, 3*W))
