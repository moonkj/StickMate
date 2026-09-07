# 독립 재측정 — colorlab 을 import 하지 않는다. 식을 처음부터 다시 쓴다.
import math
def hx(h):
    h=h.lstrip('#'); return tuple(int(h[i:i+2],16) for i in (0,2,4))
def lin(u):
    u/=255.0
    return u/12.92 if u<=0.04045 else ((u+0.055)/1.055)**2.4
def Y(c): return 0.2126*lin(c[0])+0.7152*lin(c[1])+0.0722*lin(c[2])
def CR(a,b):
    ya,yb=Y(a),Y(b); hi,lo=max(ya,yb),min(ya,yb); return (hi+0.05)/(lo+0.05)
# XYZ D65 (sRGB 원색 행렬을 직접 적는다)
M=((0.4124564,0.3575761,0.1804375),(0.2126729,0.7151522,0.0721750),(0.0193339,0.1191920,0.9503041))
WP=(0.9504559,1.0,1.0890578)
def lab(c):
    r,g,b=lin(c[0]),lin(c[1]),lin(c[2])
    X=M[0][0]*r+M[0][1]*g+M[0][2]*b; YY=M[1][0]*r+M[1][1]*g+M[1][2]*b; Z=M[2][0]*r+M[2][1]*g+M[2][2]*b
    def f(t): return t**(1/3) if t>(6/29)**3 else t/(3*(6/29)**2)+4/29
    fx,fy,fz=f(X/WP[0]),f(YY/WP[1]),f(Z/WP[2])
    return (116*fy-16, 500*(fx-fy), 200*(fy-fz))
def dE(a,b):
    la,lb=lab(a),lab(b); return math.sqrt(sum((la[i]-lb[i])**2 for i in range(3)))
# --- 교정 (알려진 값) ---
assert abs(CR((255,255,255),(0,0,0))-21.0)<5e-4, "교정 실패 흰/검"
assert abs(CR((255,255,255),(255,255,255))-1.0)<5e-4, "교정 실패 동일색"
assert abs(CR(hx('#767676'),(255,255,255))-4.5422)<5e-4, "교정 실패 #767676"
assert abs(lab((255,255,255))[0]-100)<0.01 and abs(dE((255,255,255),(0,0,0))-100)<0.01, "교정 실패 LAB"
assert abs(lab(hx('#FF0000'))[0]-53.24)<0.02, "교정 실패 빨강"
print("독립 계산기 교정 5건 통과\n")

PACK={'오피스':('#456ECC','#6080CC'),'사이버':('#009682','#518C84'),'네온':('#CC1BA9','#9C5A8E'),
      '스포츠':('#CC3F29','#9E655C'),'잉크':('#9768CC','#8563AB'),'밀리':('#639400','#798C51'),
      '광부':('#C96F00','#8C7251'),'대마법사':('#00992E','#518C63')}
BD={'흰':'#FFFFFF','검':'#000000','종이':'#E9EAE6','목탄':'#25282E'}
print("1) 신규 4색 배경 4종 최악 대비")
for n in ('광부','대마법사'):
    for i,h in enumerate(PACK[n]):
        w=min((CR(hx(h),hx(b)),k) for k,b in BD.items())
        print(f"   {n} {'주' if i==0 else '보'} {h}  최악 {w[0]:.2f} ({w[1]})")
print("\n2) 8팩 28쌍 교차 최소 ΔE")
ks=list(PACK); worst=(999,'')
for i in range(len(ks)):
    for j in range(i+1,len(ks)):
        d=min(dE(hx(a),hx(b)) for a in PACK[ks[i]] for b in PACK[ks[j]])
        if d<worst[0]: worst=(d,f"{ks[i]}↔{ks[j]}")
print(f"   가족 하한 {worst[0]:.2f}  ({worst[1]})")
print("\n3) 245° 보라안 vs 컬러 잉크")
print(f"   #6D64CC/#6F69B5 ↔ #9768CC/#8563AB 최소 ΔE "
      f"{min(dE(hx(a),hx(b)) for a in ('#6D64CC','#6F69B5') for b in PACK['잉크']):.2f}")
print("\n4) 신규 4색 ↔ 등급 램프 4색 최소 ΔE")
R=['#9C978C','#BCAC8B','#DEC081','#FFD375']
print(f"   {min(dE(hx(a),hx(b)) for n in ('광부','대마법사') for a in PACK[n] for b in R):.2f}")
print("\n5) 등급 램프 인접 쌍 최소 ΔE")
print(f"   {min(dE(hx(R[i]),hx(R[i+1])) for i in range(3)):.2f}")
print("\n6) 16색 휘도 폭 대비")
ys=[Y(hx(h)) for n in PACK for h in PACK[n]]
print(f"   {(max(ys)+0.05)/(min(ys)+0.05):.4f}:1")
