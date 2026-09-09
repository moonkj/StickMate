# -*- coding: utf-8 -*-
import numpy as np, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from scipy import ndimage

SC='/private/tmp/claude-501/-Users-kjmoon-App-StickMate/b03562ef-02d9-492a-a8ed-a33855111fff/scratchpad'
ROOT='/Users/kjmoon/App/StickMate'
OUT=ROOT+'/design/character/style_unification_r1'
os.makedirs(OUT,exist_ok=True)
B=np.load(SC+'/body.npy'); HCX,HCY,FEET,ROUT=np.load(SC+'/bodymeta.npy')

def F(sz,bold=False):
    for p in ['/System/Library/Fonts/AppleSDGothicNeo.ttc','/System/Library/Fonts/Supplemental/AppleGothic.ttf']:
        if os.path.exists(p):
            try: return ImageFont.truetype(p, sz, index=(2 if bold and p.endswith('ttc') else 0))
            except Exception: pass
    return ImageFont.load_default()

def body_img(ink=(0,0,0)):
    h,w=B.shape; a=np.zeros((h,w,4),np.uint8)
    a[:,:,0],a[:,:,1],a[:,:,2]=ink; a[:,:,3]=(B*255).astype(np.uint8)
    return Image.fromarray(a)

def tight(im):
    a=np.asarray(im); ys,xs=np.where(a[:,:,3]>10)
    return im.crop((int(xs.min()),int(ys.min()),int(xs.max())+1,int(ys.max())+1))

def key_checker(im):
    a=np.asarray(im.convert('RGB')).astype(int)
    mx=a.max(axis=2); mn=a.min(axis=2)
    ch=(mn>228)&((mx-mn)<=8)
    al=ndimage.binary_fill_holes(~ch).astype(np.uint8)*255
    al=ndimage.median_filter(al,3)
    return tight(Image.fromarray(np.dstack([a.astype(np.uint8),al])))

def key_navy(path, thr=34):
    """DLC 12종은 hasAlpha:no 다. 배경(네이비 그라데이션)을 3차 다항식으로 적합해 잔차로 오려낸다.
       ★ 어두운 부품(헬멧 턱끈 등)은 배경과 구별되지 않아 함께 잘려나간다 — 그 자체가 실측 사실이다."""
    a=np.asarray(Image.open(path).convert('RGB')).astype(float)
    h,w,_=a.shape; Y,X=np.mgrid[0:h,0:w]; Yn=Y/h; Xn=X/w
    bd=np.zeros((h,w),bool); k=14; bd[:k,:]=bd[-k:,:]=bd[:,:k]=bd[:,-k:]=True
    A=np.stack([np.ones_like(Xn),Xn,Yn,Xn**2,Yn**2,Xn*Yn,Xn**3,Yn**3,Xn**2*Yn,Xn*Yn**2],-1)
    Ab=A[bd]; fit=np.zeros_like(a)
    for c in range(3):
        coef,*_=np.linalg.lstsq(Ab,a[bd][:,c],rcond=None); fit[:,:,c]=A@coef
    m=np.sqrt(((a-fit)**2).sum(axis=2))>thr
    m=ndimage.binary_fill_holes(ndimage.binary_closing(ndimage.binary_opening(m,np.ones((3,3))),np.ones((9,9))))
    lab,n=ndimage.label(m)
    if n: m=(lab==(np.argmax(ndimage.sum(m,lab,range(1,n+1)))+1))
    al=(ndimage.gaussian_filter(m.astype(float),0.8)*255).astype(np.uint8)
    return tight(Image.fromarray(np.dstack([a.astype(np.uint8),al])))

MONT=Image.open('/Users/kjmoon/.claude/uploads/b03562ef-02d9-492a-a8ed-a33855111fff/8e41aa18-image.png')
ART={
 'mont_fedora' : key_checker(MONT.crop((291,80,431,171))),
 'mont_crown'  : key_checker(MONT.crop((442,72,567,191))),
 'mont_wiz'    : key_checker(MONT.crop((715,42,874,214))),
 'dlc_helmet'  : key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/pack_mine_head_miner_helmet.png'),
 'dlc_wiz'     : key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/pack_arcane_head_wizard_hat.png'),
}
# 현행 벡터 중절모 — 실기 캡처에서 그대로 오려낸다(합성 아님)
P=np.asarray(Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').convert('RGB')).astype(int)[100:400,286:546]
hm=((P.max(axis=2)-P.min(axis=2))>10)&(P.max(axis=2)>50)
hm[130:,:]=False
al=(ndimage.binary_closing(hm,np.ones((3,3)))).astype(np.uint8)*255
VEC_FEDORA=tight(Image.fromarray(np.dstack([P.astype(np.uint8),al])))
VEC_ANCHOR=(182.0-0, 126.0)     # 패널좌표 중심x / 아래끝y
VEC_W=51.0

# ── 스타일 조건화 연산 ────────────────────────────────────────────────
def keyline(im, w, col=(0,0,0)):
    a=np.asarray(im); al=a[:,:,3]>96
    pad=int(np.ceil(w))+2
    al=np.pad(al,pad); rgba=np.pad(a,((pad,pad),(pad,pad),(0,0)))
    yy,xx=np.mgrid[-pad:pad+1,-pad:pad+1]; se=(np.hypot(xx,yy)<=w)
    d=ndimage.binary_dilation(al,se)
    out=rgba.copy()
    ring=d&(rgba[:,:,3]<250)
    out[ring,0],out[ring,1],out[ring,2],out[ring,3]=col[0],col[1],col[2],255
    return Image.fromarray(out)

def posterize(im, n):
    a=np.asarray(im).copy(); al=a[:,:,3]
    rgb=Image.fromarray(a[:,:,:3]).quantize(colors=n,method=Image.MEDIANCUT,dither=Image.Dither.NONE).convert('RGB')
    o=np.dstack([np.asarray(rgb),al]); return Image.fromarray(o)

def desat(im, f):
    a=np.asarray(im).astype(float); al=a[:,:,3]
    g=a[:,:,:3].mean(axis=2,keepdims=True)
    rgb=np.clip(g+(a[:,:,:3]-g)*f,0,255)
    return Image.fromarray(np.dstack([rgb.astype(np.uint8),al.astype(np.uint8)]))

def dropshadow_off(im):  # 반투명 금지 규약 검사용: 알파 이진화
    a=np.asarray(im).copy(); a[:,:,3]=np.where(a[:,:,3]>128,255,0); return Image.fromarray(a)

# ── 조립 ─────────────────────────────────────────────────────────────
def wear(hat, hat_w=51.0, bottom_y=15.5, ink=(0,0,0), pad=26):
    """몸통 판(1:1 MBP14 네이티브) 위에 모자를 얹은 RGBA를 만든다."""
    bh,bw=B.shape
    W=bw+pad*2; H=bh+pad*2
    canvas=Image.new('RGBA',(W,H),(0,0,0,0))
    canvas.alpha_composite(body_img(ink),(pad,pad))
    if hat is not None:
        k=hat_w/hat.size[0]
        nh=max(1,int(round(hat.size[1]*k)))
        h2=hat.resize((int(round(hat_w)),nh),Image.LANCZOS)
        x=int(round(pad+HCX-hat_w/2)); y=int(round(pad+bottom_y-nh))
        canvas.alpha_composite(h2,(x,y))
    return canvas

SS_MBP=1.0                 # MBP14 레티나 네이티브
SS_1080=1080.0/1964.0      # 1080p @100%
def rescale(im,f):
    if abs(f-1.0)<1e-6: return im
    return im.resize((max(1,int(round(im.size[0]*f))),max(1,int(round(im.size[1]*f)))),Image.LANCZOS)

def on_bg(im,bg):
    c=Image.new('RGBA',im.size,bg); c.alpha_composite(im); return c.convert('RGB')

def zoom(im,k): return im.resize((im.size[0]*k,im.size[1]*k),Image.NEAREST)
