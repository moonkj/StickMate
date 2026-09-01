# -*- coding: utf-8 -*-
import sys, math; sys.path.insert(0,'.')
import rig, items, hair
from rig import W
CATS=[("Hair",hair.SET),("Head",items.HEAD),("Eyes",items.EYES),("Neck",items.NECK),("Shoulders",items.BACK)]
def fmt(p): return "(%+.2f, %+.2f)"%p
for cat,d in CATS:
    print("### %s"%cat)
    for n,sh in d.items():
        print("  %s"%n)
        for s in sh:
            tone={0:"주색",1:"보조색",2:"그늘"}[s.tone]
            kind=("닫힘·채움" if (s.loop and s.filled) else "닫힘" if s.loop else "열린 선")
            m=rig.min_corner_seg(s,W)
            print("    %-16s %-9s %-4s %2d점  최단변 %s"
                  % (s.name, kind, tone, len(s.pts), ("%.2f획"%m) if m else "—"))
            body=", ".join(fmt(p) for p in s.pts)
            for i in range(0,len(body),108):
                print("        "+body[i:i+108])
