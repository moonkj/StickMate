exec(open('sheet.py').read())
PAD=28; HEADINK=31.65
MON=Image.open('/Users/kjmoon/.claude/uploads/b03562ef-02d9-492a-a8ed-a33855111fff/8e41aa18-image.png')
def mc(b): return key_checker(MON.crop(b))

def flatten(im, k=5):
    """k-means 로 색면을 k개로 줄인다 = 평탄면/기울기 동시 하향. 테두리는 손대지 않는다."""
    a=np.asarray(im); al=a[:,:,3]; m=al>128
    px=a[:,:,:3][m].astype(float)
    if len(px)<k: return im
    rng=np.random.default_rng(0); C=px[rng.choice(len(px),k,replace=False)]
    for _ in range(24):
        d=((px[:,None,:]-C[None,:,:])**2).sum(-1); lab=d.argmin(1)
        for j in range(k):
            if (lab==j).any(): C[j]=px[lab==j].mean(0)
    out=a.copy(); q=C[lab].astype(np.uint8)
    idx=np.where(m); out[idx[0],idx[1],:3]=q
    return Image.fromarray(out)

def inner_keyline(im, w, rim=1.0, col=(0,0,0)):
    """★ design-art §3-(3) 대응 — 어두운 선을 실루엣 «안쪽»에 넣는다.
       바깥 rim 픽셀은 채움색 그대로 두어 «바깥 1 디바이스px 띠»가 대역 안에 남게 한다."""
    a=np.asarray(im).copy(); al=a[:,:,3]>128
    def er(n):
        if n<=0: return al
        k=int(np.ceil(n)); yy,xx=np.mgrid[-k:k+1,-k:k+1]
        return ndimage.binary_erosion(al,(np.hypot(xx,yy)<=n))
    ring = er(rim) & ~er(rim+w)
    a[ring,0],a[ring,1],a[ring,2]=col
    return Image.fromarray(a)

fed=mc((291,80,430,170)); helm=ART['dlc_helmet']
KW=0.09*51.0   # 착용 폭의 9%
def K(art,w): return keyline(art,w*art.size[0]/51.0)
def IK(art,w,rim): return inner_keyline(art,w*art.size[0]/51.0,rim*art.size[0]/51.0)

ROWS=[('a · as-is (DLC Fable)','',helm),
      ('b · 바깥 검정 키라인 9%\n★ design-art §3 이 금지','금지',K(helm,KW)),
      ('c · 색면 5개로 평탄화만\n(테두리 무변경 — 비색 축)','',flatten(helm,5)),
      ('d · 색면 5개 + 안쪽 키라인\n(바깥 1px는 채움색 유지)','',IK(flatten(helm,5),KW,1.0)),
      ('e · (대조) 승인 몽타주 as-is','',fed),
      ('f · (대조) 몽타주 + 색면 5개','',flatten(fed,5))]
COLS=[('MBP14·s=0.75',1.0),('1080p·s=0.75',SS_1080),('1080p·s=0.60',SS_1080*0.6/0.75),('1080p·s=0.35',SS_1080*0.35/0.75)]
BGS=[('어두운 바탕 #1c1c1c',(28,28,28,255),(235,235,235)),('밝은 바탕 #f2f2f2',(242,242,242,255),(25,25,25))]
def hc(im,k=3):
    c=im.crop((int(PAD+HCX-44),int(PAD+HCY-44),int(PAD+HCX+44),int(PAD+HCY+24)))
    return c.resize((c.size[0]*k,c.size[1]*k),Image.NEAREST)
LW=230; CW=165; ZW=280; RH=205; HEAD=110
SW=LW+CW*4+ZW; SH=HEAD+(RH*len(ROWS)+56)*2+30
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,10),'조건 ⑥ 「윤곽선이 아닌 축」으로 화법을 맞출 수 있는가 — design-art §3 제약 반영 재실험',font=F(19,True),fill=(0,0,0))
D.text((16,38),'design-art: 자립 대역 L∈[0.1632,0.2396] 안에서는 자기 윤곽선이 공집합(최대 대비 1.358<3.0)이고, 대역 밖 검정 윤곽은 어두운 바탕화면 대비가 무너진다 → b행 금지.',font=F(12),fill=(150,60,60))
D.text((16,56),'그래서 색이 아닌 축만 남겨 다시 쟀다: (c) 색면 수·기울기만 낮춘다 / (d) 어두운 선을 실루엣 «안쪽»에 넣어 바깥 1 디바이스px 띠는 채움색으로 남긴다.',font=F(12),fill=(70,70,70))
D.text((16,74),'판정 기준은 «몸과 같은 그림으로 읽히는가» 하나다. b행은 참고로만 남긴다(금지된 안이 실제로 어떻게 보이는지 기록).',font=F(12),fill=(70,70,70))
y=HEAD
for bn,bg,fg in BGS:
    D.rectangle([0,y,SW,y+RH*len(ROWS)+54],fill=tuple(bg[:3]))
    D.text((14,y+8),bn,font=F(15,True),fill=fg)
    for ci,(cn,f) in enumerate(COLS): D.text((LW+CW*ci+8,y+32),cn,font=F(11.5),fill=fg)
    D.text((LW+CW*4+8,y+32),'머리 3배 확대',font=F(11.5),fill=fg)
    yy=y+54
    for rn,tag,art in ROWS:
        img=wear(art,pad=PAD)
        D.multiline_text((10,yy+RH//2-26),rn,font=F(12.5),fill=fg,spacing=4)
        if tag:
            D.rectangle([10,yy+RH//2+22,10+50,yy+RH//2+42],fill=(190,40,40))
            D.text((16,yy+RH//2+24),tag,font=F(12,True),fill=(255,255,255))
        for ci,(cn,f) in enumerate(COLS):
            c=on_bg(rescale(img,f),bg); S.paste(c,(LW+CW*ci+(CW-c.size[0])//2,yy+(RH-c.size[1])//2))
        z=on_bg(hc(img),bg); S.paste(z,(LW+CW*4+(ZW-z.size[0])//2,yy+(RH-z.size[1])//2))
        yy+=RH; D.line([(8,yy-3),(SW-8,yy-3)],fill=fg,width=1)
    y=yy+6
S.save(OUT+'/06_nonoutline_axes.png'); print(S.size)
# 지표 재측정
def M(im):
    a=np.asarray(im).astype(float); al=a[:,:,3]>128; L=a[:,:,:3].mean(axis=2)
    er=ndimage.binary_erosion(al,np.ones((3,3)),iterations=3); band=al&~er
    gy,gx=np.gradient(L); g=np.hypot(gx,gy)
    h,_=np.histogram(L[er],bins=16,range=(0,256))
    return np.median(L[band]),100*np.mean(L[band]<60),int((h/max(er.sum(),1)>0.03).sum()),g[er].mean()*im.size[0]/100
print('%-34s %8s %10s %8s %10s'%('','테두리L','테두리<60%','평탄면','정규화기울기'))
for rn,tag,art in ROWS:
    m=M(art); print('%-34s %8.1f %10.1f %8d %10.1f'%(rn.split('\n')[0],m[0],m[1],m[2],m[3]))
