H=2.2746944; R=0.22; RING=0.063; FLOOR=2.0/35.25
LW=0.11*1.045; LEG=0.12*1.045; ARM=0.10*1.045; ACC=0.048
HATK=0.83094   # 착용 모자 폭(월드유닛) = k*s, 실측 51px@1964@0.75 에서 유도
def row(s):
    ring=max(RING*s,FLOOR); ink=2*R*s+ring
    return dict(s=s,ch=H*s/24*100, ink=ink/24*100, hat=HATK*s/24*100,
                torso=max(LW*s,FLOOR)/24*100, arm=max(ARM*s,FLOOR)/24*100,
                leg=max(LEG*s,FLOOR)/24*100, ring=ring/24*100,
                accW=max(ACC*s,FLOOR)/24*100)
print('배율별 화면세로 대비 비율(%)')
print('%5s %8s %9s %8s %8s %8s %8s %8s'%('배율','캐릭터','머리잉크','모자폭','몸통획','팔획','다리획','장비획W'))
for s in [1.00,0.75,0.60,0.35]:
    r=row(s); print('%5.2f %8.4f %9.4f %8.4f %8.4f %8.4f %8.4f %8.4f'%(s,r['ch'],r['ink'],r['hat'],r['torso'],r['arm'],r['leg'],r['accW']))
print()
disp=[('1920×1080 @100%',1080),('2560×1440',1440),('3840×2160 4K',2160),('MBP14 3024×1964',1964),
      ('MBA13 M2 2560×1664',1664),('iPhone15Pro 1179×2556',2556),('iPad Pro12.9 2048×2732',2732)]
for s in [0.75,0.60,0.35]:
    r=row(s); print('== 배율 %.2f =='%s)
    print('%-24s %8s %9s %8s %9s %9s %9s'%('디스플레이','캐릭터','머리잉크','모자폭','장비획W','키라인8%','256²배율'))
    for n,px in disp:
        hat=r['hat']/100*px
        print('%-24s %8.1f %9.1f %8.1f %9.2f %9.2f %8.1f×'%(n,r['ch']/100*px,r['ink']/100*px,hat,r['accW']/100*px,0.08*hat,256/hat))
    print()
print('검산: 모자폭/장비획W 배수')
for s in [1.00,0.75,0.60,0.35]:
    r=row(s); print('  배율 %.2f : %.2f W (키라인 8%%는 %.2f W)'%(s,r['hat']/r['accW'],0.08*r['hat']/r['accW']))
print()
print('획/머리잉크 비율 검산 (팀 확정 22.3%)')
for s in [1.00,0.75,0.60,0.35]:
    r=row(s); m=(r['torso']+r['arm']+r['leg'])/3
    print('  배율 %.2f : 몸통 %.2f%% 팔 %.2f%% 다리 %.2f%% 평균 %.2f%%'%(s,100*r['torso']/r['ink'],100*r['arm']/r['ink'],100*r['leg']/r['ink'],100*m/r['ink']))
