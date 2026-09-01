# -*- coding: utf-8 -*-
"""도형을 ASCII로 래스터해 눈으로 확인한다(획 두께 W를 실제로 얹는다)."""
import math, rig
from rig import W

def dist_seg(p,a,b):
    ax,ay=a; bx,by=b; px,py=p
    dx,dy=bx-ax,by-ay
    L=dx*dx+dy*dy
    t=0.0 if L<1e-12 else max(0.0,min(1.0,((px-ax)*dx+(py-ay)*dy)/L))
    return math.hypot(px-(ax+dx*t), py-(ay+dy*t))

def render(shapes, x0=-2.6,x1=2.6,y0=-2.4,y1=2.1, cols=78, head=True, stroke=W):
    rows=int(cols*(y1-y0)/(x1-x0)/2.1)
    out=[]
    for r in range(rows):
        line=''
        for c in range(cols):
            x=x0+(x1-x0)*c/(cols-1); y=y1-(y1-y0)*r/(rows-1)
            ch=' '
            if head and math.hypot(x,y)<=1.0: ch='.'
            for s in shapes:
                p=s.pts; n=len(p)
                if s.filled and rig.contains(p,(x,y)):
                    ch='#' if s.tone==0 else '%'
                segs=n if s.loop else n-1
                for i in range(segs):
                    if dist_seg((x,y),p[i],p[(i+1)%n])<=stroke*0.5:
                        # 채운 도형의 윤곽선은 FillOutlineColor(주색x0.62) — 화면에서는 같은 덩어리다.
                        ch=('#' if s.tone==0 else '%') if s.filled else ('O' if s.tone==0 else '*')
                        break
            line+=ch
        out.append(line)
    return '\n'.join(out)
