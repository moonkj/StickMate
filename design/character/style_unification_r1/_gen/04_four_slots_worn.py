exec(open('sheet.py').read())
PAD=30
PXU=13.503/0.165          # 81.84 px per world unit (머리 경로 반경으로 교정)
SHOULDER=HCY+(1.5410-1.3235)*PXU
HEADINK=31.65
MON=Image.open('/Users/kjmoon/.claude/uploads/b03562ef-02d9-492a-a8ed-a33855111fff/8e41aa18-image.png')
def mc(b): return key_checker(MON.crop((b[0],b[1],b[2],b[3])))
MSET={'head':mc((1036,63,1166,184)),'eyes':mc((311,305,445,388)),
      'neck':mc((752,516,847,632)),'back':mc((321,1005,442,1144))}
DSET={'head':key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/pack_mine_head_miner_helmet.png'),
      'eyes':key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/pack_mine_eyes_dust_goggles.png'),
      'neck':key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/pack_mine_neck_mine_lamp.png'),
      'back':key_navy(ROOT+'/Assets/_Project/Resources/Items/Icons/pack_mine_back_pick_harness.png')}
# 착용 치수(머리 잉크 지름 배수) — 현행 벡터 실측 51/31.65 = 1.612 를 HEAD 기준으로 잡고 나머지는 슬롯 규약 근사
SPEC={'head':(1.612,'bottom',15.5),'eyes':(0.95,'center',HCY-1.0),
      'neck':(0.80,'top',HCY+ROUT-2.0),'back':(1.30,'top',SHOULDER-1.0)}
def place(canvas,art,slot,pad):
    k,mode,ref=SPEC[slot]; w=k*HEADINK
    s=w/art.size[0]; nh=max(1,int(round(art.size[1]*s)))
    im=art.resize((int(round(w)),nh),Image.LANCZOS)
    x=int(round(pad+HCX-w/2))
    y={'bottom':ref-nh,'center':ref-nh/2,'top':ref}[mode]
    canvas.alpha_composite(im,(x,int(round(y+pad))if mode!='bottom' else int(round(pad+y))))
def wear4(S,keyw=0.0,ink=(0,0,0),pad=PAD):
    bh,bw=B.shape
    c=Image.new('RGBA',(bw+pad*2,bh+pad*2),(0,0,0,0))
    back=S['back']
    if keyw>0: back=keyline(back,keyw*back.size[0]/(SPEC['back'][0]*HEADINK),ink)
    place(c,back,'back',pad)
    c.alpha_composite(body_img(ink),(pad,pad))
    for slot in ['neck','head','eyes']:
        art=S[slot]
        if keyw>0: art=keyline(art,keyw*art.size[0]/(SPEC[slot][0]*HEADINK),ink)
        place(c,art,slot,pad)
    return c
def headcrop(im,k=2):
    c=im.crop((int(PAD+HCX-52),int(PAD+HCY-46),int(PAD+HCX+52),int(PAD+HCY+80)))
    return c.resize((c.size[0]*k,c.size[1]*k),Image.NEAREST)
F075=SS_1080; F060=SS_1080*0.6/0.75; F035=SS_1080*0.35/0.75
ROWS=[('현행 · 벡터 4종 동시 착용\n(실기 캡처 그대로 — 대조군)',None),
      ('A · 승인 몽타주 화풍 4종 (as-is)\n파일럿모자·고글·목걸이·등산가방',wear4(MSET)),
      ('B · 승인 몽타주 화풍 4종\n+ 잉크 키라인 0.55×획',wear4(MSET,3.90)),
      ('C · DLC12 Fable 화풍 4종 (as-is)\n광부헬멧·고글·랜턴·곡괭이하네스',wear4(DSET)),
      ('D · DLC12 Fable 화풍 4종\n+ 잉크 키라인 0.55×획',wear4(DSET,3.90))]
COLS=[('MBP14 · s=0.75',1.0),('1080p · s=0.75',F075),('1080p · s=0.60',F060),('1080p · s=0.35',F035)]
BGS=[('어두운 바탕 #1c1c1c',(28,28,28,255),(235,235,235)),('밝은 바탕 #f2f2f2',(242,242,242,255),(25,25,25))]
LW=210; CW=165; ZW=245; RH=230; HEAD=100
SW=LW+CW*4+ZW; SH=HEAD+(RH*len(ROWS)+60)*2+30
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,12),'조건 ④ 동시 착용 4종 — 한 몸에 채색 그림이 4장 붙었을 때 (R4 실기 캡처와 같은 조건)',font=F(19,True),fill=(0,0,0))
D.text((16,40),'머리/눈/목/등 4슬롯. 착용 치수는 HEAD만 실측(모자 폭 = 머리 잉크 지름 1.612배)이고 나머지 3슬롯은 슬롯 규약 근사다 — 정확한 값은 design-equipment 소관(미확인).',font=F(12),fill=(150,60,60))
D.text((16,58),'키라인은 몸 잉크색으로 칠했다. 배율이 내려갈수록 그림 안쪽 정보는 사라지고 실루엣과 색면 2~3개만 남는다 — 그 잔존물이 몸과 같은 그림으로 읽히는지가 판정 대상.',font=F(12),fill=(70,70,70))
y=HEAD
for bn,bg,fg in BGS:
    D.rectangle([0,y,SW,y+RH*len(ROWS)+58],fill=tuple(bg[:3]))
    D.text((14,y+10),bn,font=F(16,True),fill=fg)
    for ci,(cn,f) in enumerate(COLS): D.text((LW+CW*ci+8,y+36),cn,font=F(12),fill=fg)
    D.text((LW+CW*4+8,y+36),'MBP14 2배 확대',font=F(12),fill=fg)
    yy=y+58
    for rn,img in ROWS:
        D.multiline_text((10,yy+RH//2-26),rn,font=F(12.5),fill=fg,spacing=4)
        if img is None:
            v=Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').crop((1898+60,100+80,1898+200,100+270))
            S.paste(v,(LW+6,yy+10))
            D.multiline_text((LW+150,yy+40),'← 실기 캡처 원본(광부 pack.mine 4종, 벡터).\n   같은 화면·같은 배율. 이 줄이 «지금 어떻게\n   보이는가»의 기준이다.',font=F(11.5),fill=fg,spacing=4)
        else:
            for ci,(cn,f) in enumerate(COLS):
                c=on_bg(rescale(img,f),bg); S.paste(c,(LW+CW*ci+(CW-c.size[0])//2,yy+(RH-c.size[1])//2))
            z=on_bg(headcrop(img),bg); S.paste(z,(LW+CW*4+(ZW-z.size[0])//2,yy+(RH-z.size[1])//2))
        yy+=RH; D.line([(8,yy-3),(SW-8,yy-3)],fill=fg,width=1)
    y=yy+6
S.save(OUT+'/04_four_slots_worn.png'); print(S.size)
