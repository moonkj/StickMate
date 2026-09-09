exec(open('sheet.py').read())
PAD=26
F075=SS_1080; F035=SS_1080*0.35/0.75
K=3.90*ART['mont_fedora'].size[0]/51.0
KH=3.90*ART['dlc_helmet'].size[0]/51.0
def headcrop(im,k=3):
    c=im.crop((int(PAD+HCX-42),int(PAD+HCY-46),int(PAD+HCX+42),int(PAD+HCY+26)))
    return c.resize((c.size[0]*k,c.size[1]*k),Image.NEAREST)

WHITE=(255,255,255)
CASES=[
 ('① 검정 잉크 몸 + 검정 키라인 (구운 값)\n→ 성립. 지금 상정하는 기본 조합.',
   wear(keyline(ART['mont_fedora'],K,(0,0,0)),pad=PAD,ink=(0,0,0)),(28,28,28,255),(235,235,235)),
 ('② 흰 잉크 몸 + 검정 키라인 (구운 값 그대로)\n→ 깨진다. 몸은 흰 선인데 모자만 검은 테두리다.',
   wear(keyline(ART['mont_fedora'],K,(0,0,0)),pad=PAD,ink=WHITE),(28,28,28,255),(235,235,235)),
 ('③ 흰 잉크 몸 + 잉크색 실루엣 언더레이(런타임 tint)\n→ 회복. 키라인을 굽지 말고 스프라이트 1장을 더 깔고 tint 한다.',
   wear(keyline(ART['mont_fedora'],K,WHITE),pad=PAD,ink=WHITE),(28,28,28,255),(235,235,235)),
 ('④ (대조) 현행 벡터는 이미 잉크색을 따라간다\n   CharacterAccessoryRenderer.cs:1011 «유저 잉크 윤곽»',
   None,(28,28,28,255),(235,235,235)),
]
COLS=[('MBP14 · s=0.75',1.0),('1080p · s=0.75',F075),('1080p · s=0.35',F035)]
LW=330; CW=190; ZW=270; RH=215; HEAD=104
SW=LW+CW*3+ZW
SH=HEAD+RH*len(CASES)+40+ (RH+80)*2 + 120
S=Image.new('RGB',(SW,SH),(255,255,255)); D=ImageDraw.Draw(S)
D.text((16,12),'조건 ② 잉크색 토글 — 구운 검정 키라인은 사용자 설정 하나에 깨진다',font=F(20,True),fill=(0,0,0))
D.text((16,40),'잉크색 흑/백은 설정창의 1급 토글이다(SettingsWindow.cs:1722 스와치 · AppControlDirector.cs:431 단축키 · StickConfig.SetRuntimeInkColor).',font=F(12.5),fill=(70,70,70))
D.text((16,58),'현행 벡터 장비는 이 값을 읽어 윤곽을 함께 뒤집는다. 비트맵은 구워지므로 못 따라간다 — §19-9-b 규격서 표에 이 항목이 아예 없다.',font=F(12.5),fill=(150,60,60))
D.text((16,76),'해법은 그림을 두 벌 그리는 게 아니라, 알파에서 자동으로 구운 «실루엣 1장»을 밑에 깔고 SpriteRenderer.color 로 잉크색을 칠하는 것이다(저작 비용 0).',font=F(12.5),fill=(70,70,70))
y=HEAD
bgd=(28,28,28,255); fgd=(235,235,235)
D.rectangle([0,y,SW,y+RH*len(CASES)+40],fill=bgd)
for ci,(cn,f) in enumerate(COLS): D.text((LW+CW*ci+8,y+12),cn,font=F(12),fill=fgd)
D.text((LW+CW*3+8,y+12),'머리 3배 확대',font=F(12),fill=fgd)
yy=y+40
for cap,img,bg,fg in CASES:
    D.multiline_text((12,yy+RH//2-26),cap,font=F(13),fill=fg,spacing=5)
    if img is not None:
        for ci,(cn,f) in enumerate(COLS):
            c=on_bg(rescale(img,f),bg); S.paste(c,(LW+CW*ci+(CW-c.size[0])//2,yy+(RH-c.size[1])//2))
        z=on_bg(headcrop(img),bg); S.paste(z,(LW+CW*3+(ZW-z.size[0])//2,yy+(RH-z.size[1])//2))
    else:
        vec=Image.open(ROOT+'/design/equipment/pack_detail_r4_live_mine.png').crop((286+140,100+90,286+225,100+270))
        S.paste(vec.resize((int(vec.size[0]*1.0),int(vec.size[1]*1.0))),(LW+8,yy+8))
        D.text((LW+120,yy+40),'← 실기 캡처(검정 잉크). 흰 잉크 캡처는\n   이번 라운드에서 확보하지 못했다 — 미확인.',font=F(12),fill=fg,spacing=4)
    yy+=RH; D.line([(8,yy-3),(SW-8,yy-3)],fill=fg,width=1)

# 좌우 반전 블록
y2=yy+16
D.text((16,y2),'조건 ③ 좌우 반전 — 엔진은 그림 한 장을 flipX 로 뒤집는다(§19-9-b 확정). 3/4 시점과 좌우 비대칭 하이라이트는 그 순간 깨진다.',font=F(17,True),fill=(0,0,0))
y2+=30
D.rectangle([0,y2,SW,y2+RH+40],fill=bgd)
pairs=[('DLC12 광부헬멧 (3/4 시점 · 우측 광원)',ART['dlc_helmet']),('승인 몽타주 중절모 (준정면 · 상단 광원)',ART['mont_fedora'])]
x=LW-300
D.text((14,y2+12),'원본 / 좌우 반전 나란히 (MBP14 3배 확대)',font=F(13),fill=fgd)
xx=250
for pn,art in pairs:
    a=wear(art,pad=PAD); b=wear(art.transpose(Image.FLIP_LEFT_RIGHT),pad=PAD)
    za=on_bg(headcrop(a,3),bgd); zb=on_bg(headcrop(b,3),bgd)
    S.paste(za,(xx,y2+40)); S.paste(zb,(xx+za.size[0]+10,y2+40))
    D.text((xx,y2+18),pn,font=F(12),fill=fgd)
    xx+=za.size[0]*2+40
S.save(OUT+'/03_ink_toggle_and_mirror.png'); print(S.size)
