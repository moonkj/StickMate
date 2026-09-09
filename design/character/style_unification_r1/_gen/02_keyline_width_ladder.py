exec(open('sheet.py').read())
PAD=26
TORSO_MBP=0.114950*0.75/24*1964   # 7.054 px
HEADINK=31.65
F1080_075=SS_1080; F1080_060=SS_1080*0.60/0.75; F1080_035=SS_1080*0.35/0.75
print('torso@MBP14 %.3f  head ink %.2f'%(TORSO_MBP,HEADINK))
print('factors', F1080_075, F1080_060, F1080_035)

LAD=[('없음 (as-is)',0.0),
     ('0.21×획 = 1.5px\n(몽타주 원본 굵기 상당)',1.5),
     ('0.32×획 = 2.27px\n(s=0.60에서 1px)',2.27),
     ('0.55×획 = 3.90px\n(s=0.35에서 1px)',3.90),
     ('0.80×획 = 5.64px\n(과잉 대조군)',5.64)]
COLS=[('MBP14 · s=0.75\n모자 51px',1.0),('1080p · s=0.75\n모자 28px',F1080_075),
      ('1080p · s=0.60\n모자 22px',F1080_060),('1080p · s=0.35\n모자 13px',F1080_035)]
SUBJ=[('DLC12 Fable 광부헬멧',ART['dlc_helmet']),('승인 몽타주 중절모',ART['mont_fedora'])]
BGS=[('어두운 바탕',(28,28,28,255),(235,235,235)),('밝은 바탕',(242,242,242,255),(25,25,25))]

def headcrop(im,k=3):
    c=im.crop((int(PAD+HCX-42),int(PAD+HCY-46),int(PAD+HCX+42),int(PAD+HCY+26)))
    return c.resize((c.size[0]*k,c.size[1]*k),Image.NEAREST)

LW=180; CW=175; ZW=270; RH=205; HEAD=96
SW=LW+CW*4+ZW
blocks=len(SUBJ)*len(BGS)
SH=HEAD+(RH*len(LAD)+66)*blocks+30
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,12),'조건 ① 검정 테두리(키라인) 굵기 사다리 — 얼마나 굵어야 몸과 같은 그림으로 읽히나',font=F(20,True),fill=(0,0,0))
D.text((16,40),'굵기 기준: 몸통 획(MBP14 s=0.75에서 7.054px = 머리 잉크 지름의 22.3%). 키라인은 텍스처에 구워지므로 배율과 함께 줄어든다 — 하한 배율에서 1px을 넘기는지가 관문이다.',font=F(12.5),fill=(70,70,70))
D.text((16,58),'키라인 색 = 몸 잉크색(0,0,0). 반투명 없음(§19-8-2). 같은 모자·같은 착용 폭·같은 앵커.',font=F(12.5),fill=(70,70,70))
y=HEAD
for sn,art in SUBJ:
    for bn,bg,fg in BGS:
        D.rectangle([0,y,SW,y+RH*len(LAD)+64],fill=tuple(bg[:3]))
        D.text((14,y+10),'%s  —  %s'%(sn,bn),font=F(16,True),fill=fg)
        for ci,(cn,f) in enumerate(COLS): D.multiline_text((LW+CW*ci+8,y+34),cn,font=F(12),fill=fg,spacing=3)
        D.text((LW+CW*4+8,y+34),'머리 3배 확대',font=F(12),fill=fg)
        yy=y+64
        for ln,w in LAD:
            hat = art if w<=0 else keyline(art, w*art.size[0]/51.0)
            img = wear(hat, pad=PAD)
            D.multiline_text((12,yy+RH//2-24),ln,font=F(13),fill=fg,spacing=4)
            for ci,(cn,f) in enumerate(COLS):
                c=on_bg(rescale(img,f),bg); S.paste(c,(LW+CW*ci+(CW-c.size[0])//2, yy+(RH-c.size[1])//2))
            z=on_bg(headcrop(img),bg); S.paste(z,(LW+CW*4+(ZW-z.size[0])//2, yy+(RH-z.size[1])//2))
            yy+=RH; D.line([(8,yy-3),(SW-8,yy-3)],fill=fg,width=1)
        y=yy+6
S.save(OUT+'/02_keyline_width_ladder.png'); print(S.size)
