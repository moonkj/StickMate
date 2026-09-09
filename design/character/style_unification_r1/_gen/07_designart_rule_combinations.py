exec(open('sheet.py').read())
PAD=28
MON=Image.open('/Users/kjmoon/.claude/uploads/b03562ef-02d9-492a-a8ed-a33855111fff/8e41aa18-image.png')
def mc(b): return key_checker(MON.crop(b))
def flatten(im,k=5):
    a=np.asarray(im); m=a[:,:,3]>128; px=a[:,:,:3][m].astype(float)
    rng=np.random.default_rng(0); C=px[rng.choice(len(px),k,replace=False)]
    for _ in range(24):
        lab=((px[:,None,:]-C[None,:,:])**2).sum(-1).argmin(1)
        for j in range(k):
            if (lab==j).any(): C[j]=px[lab==j].mean(0)
    out=a.copy(); idx=np.where(m); out[idx[0],idx[1],:3]=C[lab].astype(np.uint8)
    return Image.fromarray(out)
def inner_line(im,w,rim,col=(0,0,0)):
    a=np.asarray(im).copy(); al=a[:,:,3]>128
    def er(n):
        if n<=0: return al
        k=int(np.ceil(n)); yy,xx=np.mgrid[-k:k+1,-k:k+1]
        return ndimage.binary_erosion(al,(np.hypot(xx,yy)<=n))
    ring=er(rim)&~er(rim+w); a[ring,0],a[ring,1],a[ring,2]=col
    return Image.fromarray(a)
helm=ART['dlc_helmet']; fed=mc((291,80,430,170))
# 1 디바이스 px(1080p·0.75, 모자 28.0px) = 착용 폭의 3.571%
D1=0.03571*51.0
def IL(art,mult): return inner_line(flatten(art,5),D1*mult*art.size[0]/51.0,D1*0.6*art.size[0]/51.0)
ROWS=[('a · DLC · 색면5 (선 없음)\n= design-art 규칙 준수 기본형',flatten(helm,5),(0,0,0)),
      ('b · DLC · 색면5 + 안쪽 선 1 디바이스px\n(착용 폭 3.6%)',IL(helm,1.0),(0,0,0)),
      ('c · DLC · 색면5 + 안쪽 선 2 디바이스px',IL(helm,2.0),(0,0,0)),
      ('d · 몽타주 · 색면5 (선 없음)',flatten(fed,5),(0,0,0)),
      ('e · 몽타주 · 색면5 + 안쪽 선 1 디바이스px',IL(fed,1.0),(0,0,0)),
      ('f ★ 흰 잉크 몸 + 몽타주 색면5\n= design-art 규칙에서 실제로 나오는 조합',flatten(fed,5),(255,255,255)),
      ('g ★ 흰 잉크 몸 + DLC 색면5',flatten(helm,5),(255,255,255))]
COLS=[('MBP14·s=0.75',1.0),('1080p·s=0.75',SS_1080),('1080p·s=0.60',SS_1080*0.6/0.75),('1080p·s=0.35',SS_1080*0.35/0.75)]
BGS=[('어두운 바탕 #1c1c1c',(28,28,28,255),(235,235,235)),('밝은 바탕 #f2f2f2',(242,242,242,255),(25,25,25))]
def hc(im,k=3):
    c=im.crop((int(PAD+HCX-44),int(PAD+HCY-44),int(PAD+HCX+44),int(PAD+HCY+24)))
    return c.resize((c.size[0]*k,c.size[1]*k),Image.NEAREST)
LW=250; CW=160; ZW=280; RH=200; HEAD=100
SW=LW+CW*4+ZW; SH=HEAD+(RH*len(ROWS)+54)*2+30
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,10),'조건 ⑦ design-art 규칙(바깥 윤곽 금지) 아래에서 실제로 나오는 조합들',font=F(19,True),fill=(0,0,0))
D.text((16,38),'안쪽 선의 단위를 «착용 폭의 %»가 아니라 «화면 1 디바이스 픽셀»로 잡았다 — 1080p·0.75에서 모자 28.0px 이므로 착용 폭의 3.571%다(1024² 캔버스 37px).',font=F(12),fill=(70,70,70))
D.text((16,56),'f·g행이 핵심이다. 비트맵은 잉크를 안 따라가므로(sr.color=white 확정) 흰 잉크 사용자에게는 «흰 막대 인형 + 자기 색을 가진 장비»가 실제 출하 조합이다.',font=F(12),fill=(150,60,60))
y=HEAD
for bn,bg,fg in BGS:
    D.rectangle([0,y,SW,y+RH*len(ROWS)+52],fill=tuple(bg[:3]))
    D.text((14,y+8),bn,font=F(15,True),fill=fg)
    for ci,(cn,f) in enumerate(COLS): D.text((LW+CW*ci+8,y+30),cn,font=F(11.5),fill=fg)
    D.text((LW+CW*4+8,y+30),'머리 3배 확대',font=F(11.5),fill=fg)
    yy=y+52
    for rn,art,ink in ROWS:
        img=wear(art,pad=PAD,ink=ink)
        D.multiline_text((10,yy+RH//2-24),rn,font=F(12),fill=fg,spacing=4)
        for ci,(cn,f) in enumerate(COLS):
            c=on_bg(rescale(img,f),bg); S.paste(c,(LW+CW*ci+(CW-c.size[0])//2,yy+(RH-c.size[1])//2))
        z=on_bg(hc(img),bg); S.paste(z,(LW+CW*4+(ZW-z.size[0])//2,yy+(RH-z.size[1])//2))
        yy+=RH; D.line([(8,yy-3),(SW-8,yy-3)],fill=fg,width=1)
    y=yy+6
S.save(OUT+'/07_designart_rule_combinations.png'); print(S.size)

# ── 그라데이션 면적률 (평탄화에 견디는 지표) ──
def grad_area(im):
    a=np.asarray(im).astype(float); al=a[:,:,3]>128; L=a[:,:,:3].mean(axis=2)
    er=ndimage.binary_erosion(al,np.ones((3,3)),iterations=3)
    rng=ndimage.maximum_filter(L,3)-ndimage.minimum_filter(L,3)
    return 100*np.mean(((rng>=2)&(rng<=20))[er])
DL=[key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/%s.png'%f) for f in
    ['pack_mine_head_miner_helmet','pack_mine_eyes_dust_goggles','pack_mine_neck_mine_lamp','pack_mine_back_pick_harness',
     'pack_arcane_head_wizard_hat','pack_arcane_back_moon_robe','pack_cyber_head_patched_hood','pack_cyber_back_tarp_cape']]
MT=[mc(b) for b in [(13,78,146,181),(291,80,430,170),(442,72,566,190),(715,42,873,213),(311,305,445,388),(752,516,847,632),(13,784,164,892),(321,1005,442,1144)]]
print('그라데이션 면적률 (3x3 밝기폭이 2~20인 내부 화소 비율)')
print('  DLC12 Fable 8종 평균 %.1f%%'%np.mean([grad_area(i) for i in DL]))
print('  승인 몽타주 8종 평균 %.1f%%'%np.mean([grad_area(i) for i in MT]))
print('  DLC 색면5 평탄화 후 %.1f%%'%grad_area(flatten(helm,5)))
print('  몽타주 색면5 평탄화 후 %.1f%%'%grad_area(flatten(fed,5)))
A=np.asarray(Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').convert('RGB')).astype(float)
P=A[700:900,440:700]; sat=P.max(axis=2)-P.min(axis=2); lum=P.mean(axis=2)
m=(sat>10)&(lum>12)
lab,n=ndimage.label(ndimage.binary_dilation(m,np.ones((5,5)))); k=int(np.argmax(ndimage.sum(m,lab,range(1,n+1))))+1
fp=ndimage.binary_fill_holes(ndimage.binary_closing(lab==k,np.ones((9,9))))
er=ndimage.binary_erosion(fp,np.ones((3,3)),iterations=3)
rng=ndimage.maximum_filter(lum,3)-ndimage.minimum_filter(lum,3)
print('  현행 벡터 중절모(3배 패널 실측) %.1f%%'%(100*np.mean(((rng>=2)&(rng<=20))[er])))
