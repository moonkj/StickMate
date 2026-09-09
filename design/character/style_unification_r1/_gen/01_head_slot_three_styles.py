exec(open('sheet.py').read())

def wear_vector(pad=26, ink=(0,0,0)):
    bh,bw=B.shape
    canvas=Image.new('RGBA',(bw+pad*2,bh+pad*2),(0,0,0,0))
    canvas.alpha_composite(body_img(ink),(pad,pad))
    a=np.asarray(Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').convert('RGB')).astype(int)[100:400,286:546]
    hm=((a.max(axis=2)-a.min(axis=2))>10)&(a.max(axis=2)>50); hm[130:,:]=False
    ys,xs=np.where(hm); ox,oy=xs.min(),ys.min()
    canvas.alpha_composite(VEC_FEDORA,(int(round(pad+ox-(181.5-HCX))),int(round(pad+oy-(131.5-HCY)))))
    return canvas

PAD=26
ROWS=[('현행 · 벡터 중절모\n(실기 캡처 오림 — 합성 아님)', wear_vector(PAD)),
      ('A · 승인 몽타주 화풍\n중절모 비트맵 (as-is)',        wear(ART['mont_fedora'],pad=PAD)),
      ('B · 승인 몽타주 화풍\n왕관 비트맵 (as-is)',          wear(ART['mont_crown'],pad=PAD)),
      ('C · DLC12 Fable 화풍\n광부헬멧 (as-is)',            wear(ART['dlc_helmet'],pad=PAD)),
      ('D · DLC12 Fable 화풍\n마법사모자 (as-is)',          wear(ART['dlc_wiz'],pad=PAD))]
COLS=[('MBP14 레티나 · s=0.75\n캐릭터 139px · 모자 51px',1.0),
      ('1080p @100% · s=0.75\n캐릭터 77px · 모자 28px',SS_1080),
      ('1080p @100% · s=0.35\n캐릭터 36px · 모자 13px',SS_1080*0.35/0.75)]
BGS=[('어두운 바탕 #1c1c1c  (다크 월페이퍼 / 야간)',(28,28,28,255),(235,235,235)),
     ('밝은 바탕 #f2f2f2  (라이트 월페이퍼 / 문서창)',(242,242,242,255),(25,25,25))]

def headcrop(im,k=3):
    x0=int(PAD+HCX-42); x1=int(PAD+HCX+42); y0=int(PAD+HCY-46); y1=int(PAD+HCY+26)
    c=im.crop((max(x0,0),max(y0,0),x1,y1))
    return c.resize((c.size[0]*k,c.size[1]*k),Image.NEAREST)

LW=200; CW=210; ZW=270; RH=225; HEAD=104
SW=LW+CW*3+ZW
SH=HEAD+(RH*len(ROWS)+58)*2+30
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,12),'스틱메이트 화법 통일 판정 — 머리 슬롯 실측 합성  (design-character R1 · 2026-09-09)',font=F(21,True),fill=(0,0,0))
D.text((16,42),'몸통 = 2026-09-09 실기 빌드 캡처(MBP14 레티나 3024×1964 · characterScale 0.75)에서 잉크 알파를 추출한 실물. 모자만 합성했다. 오프라인으로 그린 몸이 아니다.',font=F(12.5),fill=(70,70,70))
D.text((16,60),'착용 폭 51.0px = 머리 잉크 지름 31.65px의 1.612배 — 현행 벡터 중절모 실측값을 모든 비트맵에 똑같이 적용했다(실루엣 봉투 동일, 화법만 다름).',font=F(12.5),fill=(70,70,70))
D.text((16,78),'C/D의 DLC 원본은 hasAlpha:no 라 배경을 다항식 적합으로 오려냈다. 헬멧 턱끈처럼 어두운 부품이 함께 잘려나간 것은 조작이 아니라 그 에셋의 실제 성질이다.',font=F(12.5),fill=(150,60,60))
y=HEAD
for bn,bg,fg in BGS:
    D.rectangle([0,y,SW,y+RH*len(ROWS)+56],fill=tuple(bg[:3]))
    D.text((14,y+12),bn,font=F(16,True),fill=fg)
    for ci,(cn,f) in enumerate(COLS): D.multiline_text((LW+CW*ci+8,y+10),cn,font=F(12),fill=fg,spacing=3)
    D.multiline_text((LW+CW*3+8,y+10),'머리 부분만 3배 확대 (MBP14 1:1 기준)\n— 축소 전에 무엇이 그려져 있는지',font=F(12),fill=fg,spacing=3)
    yy=y+56
    for rn,img in ROWS:
        D.multiline_text((12,yy+RH//2-22),rn,font=F(13.5),fill=fg,spacing=4)
        for ci,(cn,f) in enumerate(COLS):
            c=on_bg(rescale(img,f),bg)
            S.paste(c,(LW+CW*ci+(CW-c.size[0])//2, yy+(RH-c.size[1])//2))
        z=on_bg(headcrop(img),bg)
        S.paste(z,(LW+CW*3+(ZW-z.size[0])//2, yy+(RH-z.size[1])//2))
        yy+=RH; D.line([(8,yy-3),(SW-8,yy-3)],fill=fg,width=1)
    y=yy+6
S.save(OUT+'/01_head_slot_three_styles.png'); print(S.size)
