#!/usr/bin/env python3
"""R9 — 참고 이미지 팔다리 말단 캡 판정 (수동 지정 말단 + 자동 축/반폭).
교정: 합성 둥근/각진 대조군이 각각 86.6% / 100.0% 부근을 내야 한다."""
import sys, math, numpy as np
from PIL import Image
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/character/verify")
from r3_raster import edt as exact_edt

SRC="/Users/kjmoon/.claude/uploads/bf3ed972-ae20-4c9c-abed-8d989e6b94d7/8875f6ce-image.png"
img=np.array(Image.open(SRC).convert("RGB")).astype(int)
H,W,_=img.shape

def mask_of(c, tol=70):
    return np.sqrt(((img-np.array(c))**2).sum(2))<tol

def wprofile(mask, tip, axis, rho, fmax=2.0, step=0.05):
    ux,uy=axis; nx,ny=-uy,ux
    out={}
    for f in np.arange(step, fmax+1e-9, step):
        d=f*rho; cx,cy=tip[0]+ux*d, tip[1]+uy*d
        s=np.arange(-3*rho,3*rho,0.2)
        xs=np.rint(cx+nx*s).astype(int); ys=np.rint(cy+ny*s).astype(int)
        ok=(xs>=0)&(ys>=0)&(xs<mask.shape[1])&(ys<mask.shape[0])
        v=np.zeros(len(s),bool); v[ok]=mask[ys[ok],xs[ok]]
        if not v.any(): out[round(f,2)]=0.0; continue
        c=len(s)//2
        if not v[c]:
            idx=np.where(v)[0]; c=idx[np.argmin(np.abs(idx-c))]
        lo=c
        while lo-1>=0 and v[lo-1]: lo-=1
        hi=c
        while hi+1<len(s) and v[hi+1]: hi+=1
        out[round(f,2)]=(hi-lo+1)*0.2
    return out

def analyze(mask, tipxy, label, R=70):
    x0,y0=tipxy
    ys,xs=np.nonzero(mask)
    sel=(np.abs(xs-x0)<R)&(np.abs(ys-y0)<R)
    if sel.sum()<50: return None
    # 국소 EDT 로 rho
    a,b=max(0,y0-R),min(H,y0+R); c,d=max(0,x0-R),min(W,x0+R)
    sub=mask[a:b,c:d]
    de=exact_edt(sub,1.0)
    rho=float(de.max())
    # 정확한 말단점: 안쪽 방향의 반대 극단
    cx,cy=xs[sel].mean(), ys[sel].mean()
    ax=np.array([cx-x0, cy-y0],float); ax/=np.linalg.norm(ax)
    proj=(xs[sel]-x0)*ax[0]+(ys[sel]-y0)*ax[1]
    k=int(np.argmin(proj))
    tip=(float(xs[sel][k]), float(ys[sel][k]))
    # 축 재추정
    sel2=sel&(((xs-tip[0])**2+(ys-tip[1])**2)<(3*rho)**2)
    ax=np.array([xs[sel2].mean()-tip[0], ys[sel2].mean()-tip[1]],float); ax/=np.linalg.norm(ax)
    pr=wprofile(mask,tip,tuple(ax),rho)
    s=pr[0.5]/pr[1.5]*100 if pr[1.5]>0 else float('nan')
    s10=pr[1.0]/pr[1.5]*100 if pr[1.5]>0 else float('nan')
    return rho,s,s10,tip,ax

# ---- 교정
def synth(cap,rho=18.0,L=240):
    Hh,Ww=200,400; yy,xx=np.mgrid[0:Hh,0:Ww]
    px=xx-60.0; py=yy-100.0; vx,vy=L,0.0
    t=(px*vx+py*vy)/(vx*vx+vy*vy); perp=np.abs(py)
    if cap=="round":
        tc=np.clip(t,0,1); return np.hypot(px-tc*vx,py-tc*vy)<=rho
    return (t>=0)&(t<=1)&(perp<=rho)
print("=== 교정 ===")
for cap in ("round","butt"):
    m=synth(cap)
    de=exact_edt(m,1.0); rho=de.max()
    pr=wprofile(m,(60-rho if cap=="round" else 60.0,100.0),(1.0,0.0),rho)
    print(f"  {cap:6s} rho={rho:5.2f}  w(.5r)/w(1.5r)={pr[0.5]/pr[1.5]*100:6.1f}%  w(1r)/w(1.5r)={pr[1.0]/pr[1.5]*100:6.1f}%"
          f"   [이론: 둥근 86.6/100.0, 각진 100.0/100.0]")

# ---- 실측
TIPS=[("노랑 오른손",(248,192,0),(1017,1182)),
      ("노랑 왼손",  (248,192,0),(566,1200)),
      ("노랑 왼발",  (248,192,0),(722,1690)),
      ("노랑 오른발",(248,192,0),(1000,1690)),
      ("파랑 앞손",  (60,175,240),(402,1787)),
      ("파랑 뒷손",  (60,175,240),(208,1812)),
      ("주황 손",    (240,90,20),(140,1836))]
print("\n=== 참고 말단 실측 ===")
print(f"{'말단':12s} {'rho(px)':>8s} {'획폭':>7s} {'w(.5r)/w(1.5r)':>15s} {'w(1r)/w(1.5r)':>14s}  판정")
for name,col,tp in TIPS:
    m=mask_of(col)
    r=analyze(m,tp,name)
    if r is None: print(f"{name:12s}  (마스크 부족)"); continue
    rho,s,s10,tip,ax=r
    verdict="둥근 캡" if s<93 else ("각진 캡" if s>97 else "판정보류")
    print(f"{name:12s} {rho:8.2f} {2*rho:7.1f} {s:14.1f}% {s10:13.1f}%  {verdict}   말단=({tip[0]:.0f},{tip[1]:.0f})")
