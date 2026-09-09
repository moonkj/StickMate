exec(open('sheet.py').read())
MON=Image.open('/Users/kjmoon/.claude/uploads/b03562ef-02d9-492a-a8ed-a33855111fff/8e41aa18-image.png')
def mc(b): return key_checker(MON.crop(b))
def M(im):
    a=np.asarray(im).astype(float); al=a[:,:,3]>128; L=a[:,:,:3].mean(axis=2)
    mx=a[:,:,:3].max(axis=2); mn=a[:,:,:3].min(axis=2)
    S=np.where(mx>0,(mx-mn)/np.maximum(mx,1),0)
    band=al&~ndimage.binary_erosion(al,np.ones((3,3)),iterations=3); inner=ndimage.binary_erosion(al,np.ones((3,3)),iterations=3)
    gy,gx=np.gradient(L); g=np.hypot(gx,gy)
    hist,_=np.histogram(L[inner],bins=16,range=(0,256)); flats=(hist/max(inner.sum(),1)>0.03).sum()
    wpx=im.size[0]
    return (np.median(L[band]), 100*np.mean(L[band]<60), 100*S[inner].mean(), flats, g[inner].mean()*wpx/100.0)
groups={'승인 몽타주 (8종)':[mc(b) for b in [(13,78,146,181),(291,80,430,170),(442,72,566,190),(715,42,873,213),(311,305,445,388),(752,516,847,632),(13,784,164,892),(321,1005,442,1144)]],
        'DLC12 Fable (8종)':[key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/%s.png'%f) for f in
            ['pack_mine_head_miner_helmet','pack_mine_eyes_dust_goggles','pack_mine_neck_mine_lamp','pack_mine_back_pick_harness',
             'pack_arcane_head_wizard_hat','pack_arcane_back_moon_robe','pack_cyber_head_patched_hood','pack_cyber_back_tarp_cape']]}
print('%-20s %9s %11s %8s %8s %14s'%('','테두리L','테두리<60%','채도%','평탄면','정규화기울기'))
for g,ims in groups.items():
    v=np.array([M(i) for i in ims])
    print('%-20s %9.1f %11.1f %8.1f %8.1f %14.1f'%(g,v[:,0].mean(),v[:,1].mean(),v[:,2].mean(),v[:,3].mean(),v[:,4].mean()))
# 현행 벡터(실기 캡처, 착용 크기)
A=np.asarray(Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').convert('RGB')).astype(float)
for x0,y0,y1,nm,wpx in [(286,90,130,'현행 벡터 중절모(착용)',51),(1898,80,256,'현행 벡터 광부4종(착용)',51)]:
    P=A[100:400,x0:x0+260]; sat=P.max(axis=2)-P.min(axis=2); lum=P.mean(axis=2)
    m=(sat>10)&(lum>12); m[:y0]=False; m[y1:]=False
    fp=ndimage.binary_fill_holes(ndimage.binary_closing(m,np.ones((5,5))))
    band=fp&~ndimage.binary_erosion(fp,np.ones((3,3)))
    S=np.where(P.max(axis=2)>0,(P.max(axis=2)-P.min(axis=2))/np.maximum(P.max(axis=2),1),0)
    gy,gx=np.gradient(lum); g=np.hypot(gx,gy)
    hist,_=np.histogram(lum[m],bins=16,range=(0,256)); flats=(hist/max(m.sum(),1)>0.03).sum()
    print('%-20s %9.1f %11.1f %8.1f %8.1f %14.1f'%(nm,np.median(lum[band]),100*np.mean(lum[band]<60),100*S[m].mean(),flats,g[m].mean()*wpx/100.0))
print('\n몸통(선화)          테두리L 1.0 · 테두리<60 100.0%% · 채도 0.0%% · 평탄면 1 · 정규화기울기 ~0 (2치)')
