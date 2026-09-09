exec(open('sheet.py').read())
PAD=30
PXU=13.503/0.165; SHOULDER=HCY+(1.5410-1.3235)*PXU; HEADINK=31.65
MON=Image.open('/Users/kjmoon/.claude/uploads/b03562ef-02d9-492a-a8ed-a33855111fff/8e41aa18-image.png')
def mc(b): return key_checker(MON.crop(b))
SPEC={'head':(1.612,'bottom',15.5),'eyes':(0.95,'center',HCY-1.0),'neck':(0.80,'top',HCY+ROUT-2.0),'back':(1.30,'top',SHOULDER-1.0)}
def wear1(art,slot,keyw=0.0,ink=(0,0,0),pad=PAD):
    k,mode,ref=SPEC[slot]; w=k*HEADINK
    a=art if keyw<=0 else keyline(art,keyw*art.size[0]/w,ink)
    bh,bw=B.shape; c=Image.new('RGBA',(bw+pad*2,bh+pad*2),(0,0,0,0))
    s=w/a.size[0]; nh=max(1,int(round(a.size[1]*s))); im=a.resize((int(round(w)),nh),Image.LANCZOS)
    x=int(round(pad+HCX-w/2)); y=int(round(pad+({'bottom':ref-nh,'center':ref-nh/2,'top':ref}[mode])))
    if slot=='back': c.alpha_composite(im,(x,y)); c.alpha_composite(body_img(ink),(pad,pad))
    else: c.alpha_composite(body_img(ink),(pad,pad)); c.alpha_composite(im,(x,y))
    return c
SUB=[('목걸이 (가는 체인 — 위험)', mc((752,516,847,632)),'neck'),
     ('군번줄 목걸이 (가는 체인)',   mc((1057,516,1137,637)),'neck'),
     ('둥근 안경 (가는 테)',        mc((172,316,302,377)),'eyes'),
     ('왕관 (굵은 부재 — 안전)',    mc((442,72,566,190)),'head')]
LADS=[('as-is',0.0),('언더레이 0.32×획',2.27),('언더레이 0.55×획',3.90)]
COLS=[('MBP14·s=0.75',1.0),('1080p·s=0.75',SS_1080),('1080p·s=0.35',SS_1080*0.35/0.75)]
def hc(im,k=3):
    c=im.crop((int(PAD+HCX-46),int(PAD+HCY-40),int(PAD+HCX+46),int(PAD+HCY+52)))
    return c.resize((c.size[0]*k,c.size[1]*k),Image.NEAREST)
LW=190; CW=160; ZW=290; RH=200; HEAD=90
SW=LW+CW*3+ZW; SH=HEAD+sum(RH*len(LADS)+52 for _ in SUB)+30
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,12),'조건 ⑤ 잉크 실루엣 언더레이의 부작용 — 가는 부재는 뭉친다',font=F(19,True),fill=(0,0,0))
D.text((16,40),'언더레이는 아이템 알파를 팽창시킨 실루엣이다. 체인·안경테처럼 부재가 팽창폭의 2배보다 얇으면 구멍이 메워져 «검은 덩어리»가 된다.',font=F(12),fill=(70,70,70))
D.text((16,58),'→ 규격서 항목: 아이템 안의 어떤 빈 구멍도 착용 폭의 18%(=팽창 2×7.6%+여유) 이상이어야 한다. 못 지키면 그 아이템은 언더레이 대신 색을 어둡게 굽는다.',font=F(12),fill=(150,60,60))
y=HEAD; bgd=(28,28,28,255); fgd=(235,235,235)
for sn,art,slot in SUB:
    D.rectangle([0,y,SW,y+RH*len(LADS)+50],fill=(242,242,242)); fg=(25,25,25); bg=(242,242,242,255)
    D.text((14,y+10),sn,font=F(15,True),fill=fg)
    for ci,(cn,f) in enumerate(COLS): D.text((LW+CW*ci+8,y+30),cn,font=F(11.5),fill=fg)
    D.text((LW+CW*3+8,y+30),'3배 확대',font=F(11.5),fill=fg)
    yy=y+50
    for ln,w in LADS:
        img=wear1(art,slot,w)
        D.text((12,yy+RH//2-8),ln,font=F(13),fill=fg)
        for ci,(cn,f) in enumerate(COLS):
            c=on_bg(rescale(img,f),bg); S.paste(c,(LW+CW*ci+(CW-c.size[0])//2,yy+(RH-c.size[1])//2))
        z=on_bg(hc(img),bg); S.paste(z,(LW+CW*3+(ZW-z.size[0])//2,yy+(RH-z.size[1])//2))
        yy+=RH; D.line([(8,yy-3),(SW-8,yy-3)],fill=fg,width=1)
    y=yy+4
S.save(OUT+'/05_underlay_side_effects.png'); print(S.size)
