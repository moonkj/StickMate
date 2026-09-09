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
def wear_vec(pad=PAD):
    bh,bw=B.shape; c=Image.new('RGBA',(bw+pad*2,bh+pad*2),(0,0,0,0)); c.alpha_composite(body_img(),(pad,pad))
    a=np.asarray(Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').convert('RGB')).astype(int)[100:400,286:546]
    hm=((a.max(axis=2)-a.min(axis=2))>10)&(a.max(axis=2)>50); hm[130:,:]=False
    ys,xs=np.where(hm); c.alpha_composite(VEC_FEDORA,(int(round(pad+xs.min()-(181.5-HCX))),int(round(pad+ys.min()-(131.5-HCY)))))
    return c
fed=mc((291,80,430,170)); helm=ART['dlc_helmet']
D1=0.03571*51.0
def IL(a): return inner_line(flatten(a,5),D1*a.size[0]/51.0,D1*0.6*a.size[0]/51.0)
CASES=[('①  현행 · 벡터 중절모 (실기 캡처)\n테두리 잉크 · 평탄면 5 · 그라데이션 면적 8.7%','기준',wear_vec(),(0,0,0)),
       ('②  DLC12 Fable 화풍 · 조건 미적용\n3/4 시점 · 평탄면 10.1 · 그라데이션 면적 40%','안 성립',wear(helm,pad=PAD),(0,0,0)),
       ('③  승인 몽타주 화풍 · 조건 미적용\n준정면 · 평탄면 8.6 · 그라데이션 면적 42%','경계',wear(fed,pad=PAD),(0,0,0)),
       ('④  몽타주 + 색면 5 + 안쪽 선 1 디바이스px\n바깥 윤곽 없음(design-art §3 준수)','성립',wear(IL(fed),pad=PAD),(0,0,0)),
       ('⑤  ④와 같은 그림 · 흰 잉크 몸\n비트맵은 잉크를 안 따라간다 — 그래도 성립','성립',wear(IL(fed),pad=PAD,ink=(255,255,255)),(255,255,255))]
COLS=[('MBP14 · s=0.75 (1:1)',1.0),('1080p · s=0.75',SS_1080),('1080p · s=0.60',SS_1080*0.6/0.75),('1080p · s=0.35',SS_1080*0.35/0.75)]
BGS=[((28,28,28,255),(235,235,235)),((242,242,242,255),(25,25,25))]
CW=175; RH=225; LW=300; HEAD=118
SW=LW+CW*8+20; SH=HEAD+RH*len(CASES)+40
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,10),'판정 요약 — 선화 몸 + 채색 장비는 «조건부로 성립한다»',font=F(22,True),fill=(0,0,0))
D.text((16,42),'같은 몸(2026-09-09 실기 캡처) · 같은 착용 폭(머리 잉크 지름의 1.612배) · 같은 앵커. 다른 것은 화풍 하나뿐이다.',font=F(13),fill=(70,70,70))
D.text((16,62),'②가 깨지는 이유는 «디테일이 많아서»가 아니다 — 시점이 3/4이고, 값 단계가 10개고, 내부의 40%가 그라데이션이기 때문이다.',font=F(13),fill=(150,60,60))
D.text((16,82),'★ 검정 바깥 윤곽선은 해법이 아니다(design-art §3 · 자립 대역). 통일은 시점·색면 수·그라데이션 면적 세 축으로 한다. ④가 그 결과다.',font=F(13),fill=(20,90,150))
x0=LW
D.rectangle([x0,HEAD-24,x0+CW*4,SH-24],fill=(28,28,28))
D.rectangle([x0+CW*4+20,HEAD-24,SW,SH-24],fill=(242,242,242))
for ci,(cn,f) in enumerate(COLS):
    D.text((x0+CW*ci+8,HEAD-20),cn,font=F(11.5),fill=(235,235,235))
    D.text((x0+CW*4+20+CW*ci+8,HEAD-20),cn,font=F(11.5),fill=(25,25,25))
y=HEAD+4
for cap,verdict,img,ink in CASES:
    D.multiline_text((12,y+RH//2-32),cap,font=F(13),fill=(0,0,0),spacing=5)
    col={'성립':(20,120,50),'경계':(190,130,0),'안 성립':(190,40,40),'기준':(70,90,150)}[verdict]
    D.rectangle([12,y+RH//2+26,12+66,y+RH//2+48],fill=col)
    D.text((20,y+RH//2+29),verdict,font=F(13,True),fill=(255,255,255))
    for bi,(bg,fg) in enumerate(BGS):
        bx=x0 if bi==0 else x0+CW*4+20
        for ci,(cn,f) in enumerate(COLS):
            c=on_bg(rescale(img,f),bg); S.paste(c,(bx+CW*ci+(CW-c.size[0])//2,y+(RH-c.size[1])//2))
    y+=RH; D.line([(8,y-2),(SW-8,y-2)],fill=(120,120,120),width=1)
S.save(OUT+'/00_verdict_side_by_side.png'); print(S.size)
