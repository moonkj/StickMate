import numpy as np
from PIL import Image
from scipy import ndimage
ROOT='/Users/kjmoon/App/StickMate'
A=np.asarray(Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').convert('RGB')).astype(float)
P=A[700:900,440:700]
sat=P.max(axis=2)-P.min(axis=2); lum=P.mean(axis=2)
m=(sat>10)&(lum>12)
lab,n=ndimage.label(ndimage.binary_dilation(m,np.ones((5,5))))
sz=ndimage.sum(m,lab,range(1,n+1)); k=int(np.argmax(sz))+1
comp=(lab==k); mm=m&comp
ys,xs=np.where(mm); w=xs.max()-xs.min()+1
print('bbox y',ys.min(),ys.max(),'x',xs.min(),xs.max(),'width',w)
fp=ndimage.binary_fill_holes(ndimage.binary_closing(comp,np.ones((9,9))))
er=ndimage.binary_erosion(fp,np.ones((3,3)),iterations=3)
band=fp&~er; inner=er
print('edge3px medianL %.1f  L<60 %.1f%%'%(np.median(lum[band]),100*np.mean(lum[band]<60)))
hist,_=np.histogram(lum[inner],bins=16,range=(0,256)); print('flats',int((hist/inner.sum()>0.03).sum()))
gy,gx=np.gradient(lum); g=np.hypot(gx,gy); print('normgrad %.1f'%(g[inner].mean()*w/100.0))
S=np.where(P.max(axis=2)>0,(P.max(axis=2)-P.min(axis=2))/np.maximum(P.max(axis=2),1),0)
print('sat %.1f%%'%(100*S[inner].mean()))
u,c=np.unique(P[inner].astype(int).reshape(-1,3),axis=0,return_counts=True); o=np.argsort(-c)[:6]
for i in o: print('  ',u[i],c[i],'L=%.0f'%u[i].mean())
