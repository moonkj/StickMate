import numpy as np
from PIL import Image
from scipy import ndimage
ROOT='/Users/kjmoon/App/StickMate'
A=np.asarray(Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').convert('RGB')).astype(float)
P=A[100:400,1898:2158]        # 광부 4종 동시착용 1:1 패널
sat=P.max(axis=2)-P.min(axis=2); lum=P.mean(axis=2)
m=(sat>10)&(lum>12); m[250:,:]=False
for y in range(70,250):
    xs=np.where(m[y])[0]
    if len(xs)==0: continue
    runs=[];s=xs[0];p=xs[0]
    for x in xs[1:]:
        if x-p>2: runs.append((s,p,p-s+1)); s=x
        p=x
    runs.append((s,p,p-s+1))
    print(y,[(int(a),int(b),int(c)) for a,b,c in runs])
