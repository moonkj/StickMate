# -*- coding: utf-8 -*-
"""
R14 — 등급 신호 검산기 (design-systems, 2026-09-05)
docs/DESIGN_SYSTEMS_GRADE_SIGNAL.md 의 모든 숫자가 여기서 나온다.

★ 교정 먼저. 교정이 하나라도 깨지면 SystemExit 으로 뒤의 숫자를 전부 폐기한다.
"""
import struct, sys, math, itertools

FAIL = []

def f32(x):
    return struct.unpack('f', struct.pack('f', float(x)))[0]

def hex2rgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))
def rgb2hex(c):
    return '#%02X%02X%02X' % tuple(int(round(max(0, min(255, v)))) for v in c)
def _lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
def lum(h):
    r, g, b = hex2rgb(h) if isinstance(h, str) else h
    return 0.2126*_lin(r) + 0.7152*_lin(g) + 0.0722*_lin(b)
def contrast(a, b):
    la, lb = lum(a), lum(b); hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)
def flatten(fg, alpha, bg):
    f, b = hex2rgb(fg), hex2rgb(bg)
    return rgb2hex(tuple(alpha*f[i] + (1-alpha)*b[i] for i in range(3)))
def _xyz(h):
    r, g, b = [_lin(v) for v in (hex2rgb(h) if isinstance(h, str) else h)]
    return (0.4124*r+0.3576*g+0.1805*b, 0.2126*r+0.7152*g+0.0722*b, 0.0193*r+0.1192*g+0.9505*b)
def lab(h):
    Xn, Yn, Zn = 0.95047, 1.0, 1.08883
    x, y, z = _xyz(h)
    def f(t): return t ** (1/3) if t > 216/24389 else (841/108)*t + 4/29
    fx, fy, fz = f(x/Xn), f(y/Yn), f(z/Zn)
    return (116*fy - 16, 500*(fx-fy), 200*(fy-fz))
def dE76(a, b):
    la, lb = lab(a), lab(b); return sum((la[i]-lb[i])**2 for i in range(3)) ** 0.5
def sat(h):
    r, g, b = [v/255 for v in hex2rgb(h)]; mx, mn = max(r,g,b), min(r,g,b)
    return 0.0 if mx == 0 else (mx-mn)/mx

def chk(name, got, want, tol):
    ok = abs(got - want) <= tol
    print("  [%s] %-48s got=%-11.4f want=%-11.4f tol=%.4f" % ("OK " if ok else "FAIL", name, got, want, tol))
    if not ok: FAIL.append(name)

CARD, THUMB, PANEL = '#1B1F26', '#15181E', '#14171C'
RAMP  = ['#9C978C', '#BCAC8B', '#DEC081', '#FFD375']
NAMES = ['일반', '희귀', '영웅', '전설']
RARITY_A = 0.55                     # UiChrome.RarityBorderAlpha
BAND_MIN, BAND_MAX = 0.1632, 0.2396 # PALETTE_SPEC 자립 대역(L)
DISCRIM, IDENT = 7.8, 48.6          # 변별 / 식별 하한(ΔE76)

print("="*100); print("[0] 교정"); print("="*100)
chk("흰/검 = 21.0", contrast('#FFFFFF','#000000'), 21.0, 0.001)
chk("동일색 = 1.0", contrast(RAMP[0], RAMP[0]), 1.0, 0.001)
for h, w in zip(RAMP, [5.68, 7.40, 9.41, 11.66]):
    chk("램프 %s ↔ CardSurface" % h, contrast(h, CARD), w, 0.02)
chk("전설 ↔ 썸네일바탕 #15181E", contrast('#FFD375', THUMB), 12.55, 0.02)
chk("일반 α0.55 실효색==#62615E", 1.0 if flatten(RAMP[0],0.55,CARD)=='#62615E' else 0.0, 1.0, 0.0)
chk("전설 α0.55 실효색==#988251", 1.0 if flatten(RAMP[3],0.55,CARD)=='#988251' else 0.0, 1.0, 0.0)
chk("일반 α0.55 대비", contrast(flatten(RAMP[0],0.55,CARD), CARD), 2.67, 0.02)
chk("전설 α0.55 대비", contrast(flatten(RAMP[3],0.55,CARD), CARD), 4.45, 0.02)
chk("호버 흰α0.62 실효색==#A8AAAD", 1.0 if flatten('#FFFFFF',0.62,CARD)=='#A8AAAD' else 0.0, 1.0, 0.0)
chk("호버 흰α0.62 대비", contrast(flatten('#FFFFFF',0.62,CARD), CARD), 7.09, 0.02)
# ★ 이 저장소에는 자가 둘이고 0.02~0.03 안에서 갈린다(UiChrome.cs:189-192가 그 사실을 스스로 적어 뒀다:
#   «착용 4.07 < 전설 4.44, design-art 자 / §15 표기로는 4.09 < 4.45»). 내 자는 design-art 쪽에 떨어진다.
chk("착용 #5DA1F5 α0.75 대비(design-art 자 4.07)", contrast(flatten('#5DA1F5',0.75,CARD), CARD), 4.07, 0.01)
chk("착용 #5DA1F5 α0.75 대비(§15 자 4.09)",       contrast(flatten('#5DA1F5',0.75,CARD), CARD), 4.09, 0.03)
chk("ΔE 일반↔희귀", dE76(RAMP[0],RAMP[1]), 15.16, 0.10)
chk("ΔE 희귀↔영웅", dE76(RAMP[1],RAMP[2]), 18.48, 0.10)
chk("ΔE 영웅↔전설", dE76(RAMP[2],RAMP[3]), 18.02, 0.10)
chk("ΔE 일반↔전설", dE76(RAMP[0],RAMP[3]), 51.54, 0.10)
chk("ΔE 브라스강조↔영웅", dE76('#C8A15A',RAMP[2]), 12.86, 0.10)
chk("ΔE 일반↔TextTertiary(예약)", dE76(RAMP[0],'#8B939F'), 13.88, 0.10)
chk("ΔE 컬러잉크주색↔TintBack", dE76('#9768CC','#955CCC'), 8.06, 0.10)

def cell_w(track, count=4, gap=2.0): return (track - gap*(count-1)) / count
chk("RarityCellWidth(139) 구카드", cell_w(139), 33.25, 0.001)
chk("RarityCellWidth(46) 보관함",  cell_w(46), 10.00, 0.001)
CARD_W = (444 - 26*2 - 8 - 12) / 2
chk("CardWidth 파생", CARD_W, 186.00, 0.001)
RIBBON_W = CARD_W - 13*2
chk("리본 폭 = 썸네일 폭", RIBBON_W, 160.00, 0.001)
chk("RarityCellWidth(160) 현행카드", cell_w(RIBBON_W), 38.50, 0.001)

def need(level, base=100.0, exp=1.05):
    return int(round(f32(f32(base) * f32(pow(max(1,level), f32(exp))))))
chk("need(2) @1.05", need(2), 207, 0)
chk("need(3) @1.05", need(3), 317, 0)
chk("need(127) @1.05", need(127), 16181, 0)
chk("need(3) @1.15", need(3, exp=1.15), 354, 0)

def peak_alpha(T, s):
    if abs(s-1.0) < 1e-9: return 1.0
    step = 1.0/s; worst = 1.0; N = 4001
    for k in range(N):
        phase = -1.0*k/(N-1)*step; m = 0.0
        for j in range(-6, 7):
            u = phase - j*step
            m = max(m, max(0.0,min(1.0,0.5-u)) * max(0.0,min(1.0,u+T+0.5)))
        worst = min(worst, m)
    return worst
for s, w in [(1.25,0.600),(1.50,0.667),(1.75,0.714),(2.00,0.750)]:
    chk("T=1 최악 피크α @×%.2f" % s, peak_alpha(1,s), w, 0.002)
for s in (1.25,1.50,1.75,2.00):
    chk("T=2 최악 피크α @×%.2f" % s, peak_alpha(2,s), 1.000, 0.002)

# 시각각(arcmin) — §15 자 ②
COND = [("96dpi 100% 60cm", 96, 1.00, 600.0),
        ("96dpi 150% 60cm", 96, 1.50, 600.0),
        ("4K27  150% 60cm", 3840/(27*16/math.hypot(16,9)), 1.50, 600.0),
        ("MBP14 ×2   50cm", 3024/(14.2*3024/math.hypot(3024,1964)), 2.00, 500.0)]   # 3024×1964 / 14.2in
def arcmin(pt, dpi, scale, dist_mm):
    px = pt*scale; mm = px/dpi*25.4
    return math.degrees(mm/dist_mm)*60.0
for (nm, dpi, sc, d), w in zip(COND, [3.03, 4.55, 2.68, 2.75]):
    chk("2pt 시각각 @%s" % nm, arcmin(2, dpi, sc, d), w, 0.02)

if FAIL:
    print("\n★ 교정 %d건 실패 — 아래 숫자 전부 무효. 폐기한다.\n   %s" % (len(FAIL), " / ".join(FAIL)))
    sys.exit(1)
print("\n★ 교정 전량 PASS.\n")

# =====================================================================================
print("="*100); print("[1] 현행 기하 — 카드 한 장이 등급에 쓰는 면적 (186 × 208, 테두리 T=1)"); print("="*100)
CARD_H, R_CARD = 208.0, 12.0
def ring_area(w, h, r, t):
    outer = w*h - (4-math.pi)*r*r
    inner = (w-2*t)*(h-2*t) - (4-math.pi)*max(0.0, r-t)**2
    return outer - inner
cellw = cell_w(RIBBON_W)
print("  리본 칸 폭 %.2f × 높이 4 = 칸당 %.1f pt²" % (cellw, cellw*4))
print("  %-6s %-12s %-12s %-12s" % ("등급", "리본 채움", "테두리 T=1", "테두리 T=2"))
ribbon_area = {}
for i, nm in enumerate(NAMES):
    ra = (i+1)*cellw*4
    ribbon_area[nm] = ra
    print("  %-6s %8.1f pt²  %8.1f pt²  %8.1f pt²" % (nm, ra, ring_area(CARD_W,CARD_H,R_CARD,1), ring_area(CARD_W,CARD_H,R_CARD,2)))
print("  ★ 구 기하(161×108 · 리본 139) 대비 — 리본 칸 33.25→38.50 (+15.8%%), 테두리 링 %.1f→%.1f pt² (T=1 기준)"
      % (ring_area(161,108,12,1), ring_area(CARD_W,CARD_H,R_CARD,1)))
print("  ※ UI_SURFACE_SPEC §15 의 «테두리 1,022.2 pt²»는 161×108 · T=2 값이다 → 현행 T=2는 %.1f pt²" % ring_area(CARD_W,CARD_H,R_CARD,2))

# =====================================================================================
print(); print("="*100); print("[2] ★ 테두리 두께 실측 결과 — 코드는 T=1 이다 (설계 15.6-b는 T=2)"); print("="*100)
print("  %-16s %-8s %-10s %-10s %-9s %-9s %-9s" % ("배율", "피크α", "실효α", "전설 실효색", "전설 CR", "일반 CR", "인접 최소ΔE"))
rows = []
for s in [1.00, 1.25, 1.50, 1.75, 2.00]:
    for T in (1, 2):
        pa = peak_alpha(T, s); ea = RARITY_A*pa
        cols = [flatten(c, ea, CARD) for c in RAMP]
        adj = min(dE76(cols[i], cols[i+1]) for i in range(3))
        rows.append((s, T, pa, ea, cols[3], contrast(cols[3],CARD), contrast(cols[0],CARD), adj))
for s, T, pa, ea, c3, cr3, cr0, adj in rows:
    if T != 1: continue
    print("  ×%-4.2f  T=1     %.3f    %.4f     %-9s  %.2f      %.2f      %.2f %s"
          % (s, pa, ea, c3, cr3, cr0, adj, "" if adj >= DISCRIM else "  ← 변별 하한 7.8 미달"))
print()
for s, T, pa, ea, c3, cr3, cr0, adj in rows:
    if T != 2: continue
    print("  ×%-4.2f  T=2     %.3f    %.4f     %-9s  %.2f      %.2f      %.2f" % (s, pa, ea, c3, cr3, cr0, adj))
# α 하한 재확인
lo, hi = 0.0, 1.0
for _ in range(60):
    mid = (lo+hi)/2
    cols = [flatten(c, mid, CARD) for c in RAMP]
    if min(dE76(cols[i], cols[i+1]) for i in range(3)) >= DISCRIM: hi = mid
    else: lo = mid
print("\n  ★ 인접 등급 ΔE ≥ 7.8 을 지키는 실효 α 하한 = %.4f  (UI_SURFACE_SPEC §15.6-a 검산1 «0.4640» 재현)" % hi)
for s in [1.00,1.25,1.50,1.75,2.00]:
    ea = RARITY_A*peak_alpha(1,s)
    print("     ×%.2f T=1 실효α %.4f  → %s" % (s, ea, "통과" if ea >= hi else "★ 미달 (%.1f%% 부족)" % ((hi-ea)/hi*100)))

# =====================================================================================
print(); print("="*100); print("[3] 시각각 — 등급 3채널이 「훑는 눈」에 남는가"); print("="*100)
FEAT = [("테두리 T=1 (현행)", 1), ("테두리 T=2 (설계)", 2), ("리본 칸 틈 2pt", 2), ("리본 높이 4pt", 4),
        ("등급 낱말 10pt(FontCaption)", 10), ("등급 낱말 12pt(FontBody 후보)", 12)]
hdr = "  %-30s" % "특징" + "".join("%-18s" % c[0] for c in COND) + "최악"
print(hdr)
for nm, pt in FEAT:
    vals = [arcmin(pt, d, s, dist) for (_, d, s, dist) in COND]
    print("  %-30s" % nm + "".join("%-18s" % ("%.2f′" % v) for v in vals) + "%.2f′" % min(vals))
print("\n  기준선: 중심시 MAR ≈ 1.0′ / 5° 이심 MAR ≈ 2.5~3.0′ / 색 판별 안정 ≈ 10′ 이상 (§15 자②)")

# =====================================================================================
print(); print("="*100); print("[4] 재질색 24종 ↔ 등급 램프 — 맞닿는가 / 대역이 갈리는가"); print("="*100)
MAT = {  # EQUIPMENT_PALETTE §4 (design-art, 2026-09-05)
 'Ivory':'#96814F','GoldLight':'#988540','Gold':'#9B7922','HairBowlLit':'#A07830','Canvas':'#AB7942',
 'HairBrown':'#A16A28','HairBald':'#936C3F','Wool':'#BA7636','HairRedLit':'#AF651C','HairBowl':'#A0622A',
 'TintHead':'#CC5512','Leather':'#BA5928','HairRed':'#BD501F','CapeRed':'#CC3C3C','Toy':'#C6443C',
 'TintNeck':'#5A8C3C','NeckDeep':'#428C24','TintEyes':'#20878C','Blue':'#3378CC','DarkLens':'#5075B5',
 'Felt':'#5577AE','Silver':'#587398','Paper':'#6787B9','TintBack':'#955CCC'}
lm = [(k, lum(v)) for k, v in MAT.items()]
print("  재질 24색 휘도 L: 최소 %.4f(%s) / 최대 %.4f(%s)" %
      (min(l for _,l in lm), min(lm,key=lambda x:x[1])[0], max(l for _,l in lm), max(lm,key=lambda x:x[1])[0]))
print("  자립 대역 [%.4f, %.4f] 안: %d/24" % (BAND_MIN, BAND_MAX, sum(1 for _,l in lm if BAND_MIN-1e-4 <= l <= BAND_MAX+1e-4)))
print("  등급 램프 휘도 L: " + " / ".join("%s %.4f" % (n, lum(c)) for n, c in zip(NAMES, RAMP)))
print("  ⇒ 재질 상한 %.4f  <  등급 하한 %.4f  (휘도 대역이 %.4f 만큼 갈라져 있다)" %
      (max(l for _,l in lm), lum(RAMP[0]), lum(RAMP[0]) - max(l for _,l in lm)))
mn = min((dE76(v, r), k, n) for k, v in MAT.items() for r, n in zip(RAMP, NAMES))
print("  재질↔등급 최소 ΔE = %.2f (%s ↔ %s)  — 변별 하한 %.1f %s" % (mn[0], mn[1], mn[2], DISCRIM, "통과" if mn[0] >= DISCRIM else "미달"))

# =====================================================================================
print(); print("="*100); print("[5] ★ 슬롯 안 재질색 중복 — 등급이 다른데 M(주 재질색)이 같은 쌍"); print("="*100)
ITEMS = [
 ('HEAD',0,1,'천모자','일반','#96814F','#CC5512'), ('HEAD',1,5,'털모자','일반','#BA7636','#96814F'),
 ('HEAD',2,9,'중절모','희귀','#5577AE','#CC5512'), ('HEAD',3,20,'왕관','희귀','#9B7922','#C6443C'),
 ('HEAD',4,23,'베레모','영웅','#5577AE','#CC5512'), ('HEAD',5,26,'밀짚모자','전설','#AB7942','#CC5512'),
 ('EYES',0,1,'선글라스','일반','#5075B5','#587398'), ('EYES',1,6,'동그란안경','일반','#587398','#20878C'),
 ('EYES',2,11,'고글','희귀','#587398','#CC5512'), ('EYES',3,15,'외알안경','희귀','#9B7922','#587398'),
 ('EYES',4,19,'뿔테안경','영웅','#5075B5','#20878C'), ('EYES',5,23,'안대','전설','#5577AE','#20878C'),
 ('NECK',0,1,'나비넥타이','일반','#5A8C3C','#96814F'), ('NECK',1,8,'줄무늬타이','일반','#428C24','#96814F'),
 ('NECK',2,12,'목도리','희귀','#CC5512','#BA5928'), ('NECK',3,18,'방울목걸이','희귀','#BA5928','#9B7922'),
 ('NECK',4,21,'펜던트목걸이','영웅','#587398','#9B7922'), ('NECK',5,25,'반다나','전설','#5A8C3C','#428C24'),
 ('BACK',0,1,'짧은망토','일반','#CC3C3C','#96814F'), ('BACK',1,13,'긴망토','일반','#CC3C3C','#96814F'),
 ('BACK',2,17,'날개','희귀','#6787B9','#955CCC'), ('BACK',3,22,'배낭','희귀','#AB7942','#5A8C3C'),
 ('BACK',4,25,'판초','영웅','#BA7636','#BA5928'), ('BACK',5,28,'요정날개','전설','#6787B9','#955CCC')]
RANK = {'일반':0,'희귀':1,'영웅':2,'전설':3}
viol = []
for slot in ('HEAD','EYES','NECK','BACK'):
    grp = [i for i in ITEMS if i[0] == slot]
    for a, b in itertools.combinations(grp, 2):
        d1 = dE76(a[5], b[5]); dboth = dE76(a[6], b[6])
        if d1 < DISCRIM:
            viol.append((slot, a[3], a[4], b[3], b[4], d1, dboth, abs(RANK[a[4]]-RANK[b[4]])))
print("  %-6s %-24s %-24s %-9s %-9s %s" % ("슬롯","A","B","ΔE(M)","ΔE(M2)","등급 거리"))
for slot, an, ar, bn, br, d1, d2, gap in sorted(viol, key=lambda v: (-v[7], v[5])):
    mark = "  ★ 등급 %d단 차이인데 주 재질색이 같다" % gap if gap >= 1 else ""
    print("  %-6s %-24s %-24s %-9.2f %-9.2f %d%s" % (slot, "%s(%s)"%(an,ar), "%s(%s)"%(bn,br), d1, d2, gap, mark))
print("\n  ΔE(M) < %.1f 인 슬롯 내 쌍: %d건 / 그중 등급이 다른 쌍: %d건 / 두 색 모두 동일(ΔE 0+0): %d건"
      % (DISCRIM, len(viol), sum(1 for v in viol if v[7] > 0), sum(1 for v in viol if v[5] < 0.01 and v[6] < 0.01)))
# 대역 안에서 슬롯당 6색 서로 ΔE>=7.8 이 가능한가
pal = list(MAT.items())
best = 0
for combo in itertools.combinations(range(len(pal)), 6):
    ok = all(dE76(pal[i][1], pal[j][1]) >= DISCRIM for i, j in itertools.combinations(combo, 2))
    if ok: best += 1
print("  24색 중 「서로 ΔE ≥ 7.8」인 6색 조합의 개수 = %d  ⇒ 슬롯당 6종 고유 배정은 %s" % (best, "가능" if best > 0 else "불가능"))

# =====================================================================================
print(); print("="*100); print("[6] 가격 ↔ 등급 — 색 없이도 등급이 읽히는가"); print("="*100)
PRICE = {'일반':600, '희귀':1400, '영웅':3200, '전설':9600}
IDLE_PER_MIN, IDLE_DAY_CAP, ARCH_B = 12, 2000, 3280
print("  %-6s %-8s %-9s %-14s %-16s %-14s" % ("등급","가격","일반 배수","유휴만(분)","유휴만(일, 상한 2,000)","원형B(일, 3,280)"))
for n in NAMES:
    p = PRICE[n]
    mins = p / IDLE_PER_MIN
    days_idle = p / IDLE_DAY_CAP
    days_b = p / ARCH_B
    print("  %-6s %-8s %-9s %-14s %-16s %-14s"
          % (n, format(p, ",d"), "%.2f×"%(p/600), "%.1f"%mins, "%.2f"%days_idle, "%.2f"%days_b))
print("  가격 4칸 전부 서로 다르고 등급 순으로 단조 증가 ⇒ 가격 → 등급 전단사(색 0개로 등급 복원 가능)")
print("  ★ 자기정정: ECONOMY_SPEC §0-6-7이 `design-narrative`에 넘긴 «영웅 266.7분»은 일일 유휴 상한 2,000을")
print("    넘는다(266.7분 × 12 = 3,200 > 2,000). 「분」으로 말할 수 있는 것은 일반·희귀 둘뿐이다.")
print("    상한 도달 시각 = %.1f분 (= %d ÷ %d)" % (IDLE_DAY_CAP/IDLE_PER_MIN, IDLE_DAY_CAP, IDLE_PER_MIN))

# =====================================================================================
print(); print("="*100); print("[7] 세트(테마) 표시 — 팩 칩 주색 ↔ 재질 24색"); print("="*100)
PACK = {'오피스 워커':('#456ECC','#6080CC'), '사이버 아포칼립스':('#009682','#518C84'),
        '네온 낙서':('#CC1BA9','#9C5A8E'), '스포츠':('#CC3F29','#9E655C'),
        '컬러 잉크':('#9768CC','#8563AB'), '밀리터리':('#639400','#798C51')}
print("  %-20s %-10s %-24s %-10s" % ("팩","주색","최근접 재질","ΔE"))
worst = (1e9, None)
for pk, (p, s) in PACK.items():
    d, k = min((dE76(p, v), k) for k, v in MAT.items())
    if d < worst[0]: worst = (d, (pk, k))
    print("  %-20s %-10s %-24s %-10.2f%s" % (pk, p, k, d, "  ★ 하한 8.0 근접" if d < 9.0 else ""))
print("  최소 %.2f (%s ↔ %s) — 팩 게이트 하한 8.0 대비 여유 %.1f%%" % (worst[0], worst[1][0], worst[1][1], (worst[0]-8.0)/8.0*100))
tb = [i[3] for i in ITEMS if '#955CCC' in (i[5], i[6])]
print("  TintBack(#955CCC)을 쓰는 기본 아이템: %s  ⇒ 「컬러 잉크」 칩과 한 격자에 서면 ΔE %.2f" % (", ".join(tb), worst[0]))

# =====================================================================================
print(); print("="*100); print("[8] 곡선 — 유저가 각 등급을 언제 처음 「살 수 있는 카드」로 만나는가"); print("="*100)
def cum_xp(level):
    return sum(need(k) for k in range(1, level))
XP_PER_DAY = 1.5*60*8       # 패시브 1.5XP/분 × 8시간 = 720
first = {}
for slot in ('HEAD','EYES','NECK','BACK'):
    for it in [i for i in ITEMS if i[0] == slot]:
        r = it[4]
        if r not in first or it[2] < first[r][0]: first[r] = (it[2], it[3], slot)
print("  패시브 XP 720/일(온라인 8h, 원형 B) 기준")
print("  %-6s %-16s %-8s %-12s %-10s" % ("등급","최초 아이템","요구 Lv","누적 XP","도달 일수"))
for n in NAMES:
    lv, nm, slot = first[n]
    cx = cum_xp(lv)
    print("  %-6s %-16s %-8d %-12s %-10.1f" % (n, "%s(%s)"%(nm,slot), lv, format(cx, ",d"), cx/XP_PER_DAY))
for label, day in [("1일차", 1), ("1주차", 7), ("1개월차", 30)]:
    xp = XP_PER_DAY*day; lv = 1
    while cum_xp(lv+1) <= xp: lv += 1
    seen = sorted({i[4] for i in ITEMS if i[2] <= lv}, key=lambda r: RANK[r])
    print("  %-8s Lv.%-3d  레벨이 열어 준 등급: %s" % (label, lv, " · ".join(seen)))

# =====================================================================================
print(); print("="*100); print("[9] 미보유 카드 — 아이콘이 무채색이라 「아트의 색」이 0인 칸"); print("="*100)
print("  ApplyCardStyle: !owned → SetIconColor(TextTertiary α0.34) = 무채색 실루엣 (Cards.cs:378-380)")
ALL42_LV = [it[2] for it in ITEMS] + [1,4,7,10,14,30,  1,3,6,9,16,24,  1,5,11,16,22,30]  # 외형 18종은 미확인 표기
for label, day in [("1일차",1), ("1주차",7), ("1개월차",30)]:
    xp = XP_PER_DAY*day; lv = 1
    while cum_xp(lv+1) <= xp: lv += 1
    locked24 = sum(1 for it in ITEMS if it[2] > lv)
    print("  %-8s Lv.%-3d  스탯 4슬롯 24종 중 잠김 %2d장 (%.1f%%) → 그 칸에서 등급을 말하는 것은 크롬 3채널뿐"
          % (label, lv, locked24, locked24/24*100))

print(); print("="*100); print("[10] 등급 낱말 — 상자 44pt 안에서 크기를 올릴 여유가 있는가"); print("="*100)
BOX = 44.0
for pt in (10, 12, 14):
    w = 2*pt          # 한글 전각 1em 근사
    print("  %2dpt : 2자 폭 %.0fpt / 상자 %.0fpt → 여유 %.0fpt (%.0f%%)  시각각 최악 %.2f′"
          % (pt, w, BOX, BOX-w, (BOX-w)/BOX*100, min(arcmin(pt,d,s,dist) for _,d,s,dist in COND)))
print("  ※ 이름 칸은 CardRarityX 129 − CardPadX 13 − CardNameGap 8 = %.0fpt 이고 낱말 상자를 안 건드리면 불변" % (129-13-8))
print("  ※ 카드 하단 버튼(상점 가격이 앉을 자리) = CardContentWidth × CardActionHeight = 160 × 29pt")
print("     ECONOMY_SPEC §0-6-7이 쓴 «버튼 139pt»는 구 카드 값이다 → 현행 160pt, 여유 +15.1%")

print(); print("="*100); print("[11] 훑는 눈 예산 — 주변시에 실제로 남는 등급 채널은 무엇인가"); print("="*100)
TRACK = '#3A4049'
print("  리본 채움 ↔ 트랙 대비: " + " / ".join("%s %.2f" % (n, contrast(c, TRACK)) for n, c in zip(NAMES, RAMP)))
print("  리본 트랙 ↔ 카드면 대비: %.2f  (UiChrome 문서 자백: 하한 3.0 미달이고 «양쪽이 동시에 서는 색은 없다»)" % contrast(TRACK, CARD))
print()
print("  %-8s %-12s %-12s %-12s" % ("등급","채움 막대 길이","일반 대비","막대 끝 x(카드 로컬)"))
for i, n in enumerate(NAMES):
    fill = i+1; L = fill*cellw + (fill-1)*2.0
    print("  %-8s %-12s %-12s %-12s" % (n, "%.1f pt"%L, "%.2f×"%(L/38.5), "%.1f"%(13.0+L)))
print("  인접 등급 막대 길이 차 = %.1f pt → 최악 시각각 %.1f′ (MAR 1.0′의 %.0f배)" %
      (cellw+2.0, min(arcmin(cellw+2.0,d,s,dist) for _,d,s,dist in COND), min(arcmin(cellw+2.0,d,s,dist) for _,d,s,dist in COND)))
print()
print("  채널별 판정 (기준: 5° 이심 MAR 2.5~3.0′ · 색 판별 안정 10′)")
VERDICT = [
 ("리본 채움 막대 **길이**", 4.0, "막대 두께 %.2f′ ≥ MAR → 막대 존재는 보이고, 끝점 차 %.1f′로 4단 변별" ),
 ("리본 **칸 수**(틈 2pt)", 2.0, "틈 %.2f′ = MAR와 같은 크기 → 훑으면 칸이 뭉개져 「길이 하나」로 무너진다(§15 실측)"),
 ("등급 **테두리** T=1(현행)", 1.0, "선 %.2f′ = MAR의 0.45~0.54배 → 주변시 해상 불가. 게다가 배율 표본으로 실효 α가 하한 미달"),
 ("등급 **테두리** T=2(설계)", 2.0, "선 %.2f′ = MAR와 같은 크기 → 경계선 색으로 남는다"),
 ("등급 **낱말** 10pt", 10.0, "%.2f′ — 읽기는 중심시 전용. 색 판별 안정선 10′은 넘지만 낱말은 「읽어야」 한다"),
]
for nm, pt, tail in VERDICT:
    w = min(arcmin(pt,d,s,dist) for _,d,s,dist in COND)
    if "끝점" in tail:
        print("    %-30s %s" % (nm, tail % (w, min(arcmin(cellw+2.0,d,s,dist) for _,d,s,dist in COND))))
    else:
        print("    %-30s %s" % (nm, tail % w))
print()
print("  ⇒ 주변시에 남는 등급 채널 = **리본 채움 막대의 길이 1개**. 칸 수·테두리·낱말은 응시가 필요하다.")
print("  ⇒ 한 섹션 6장을 「칸 수/낱말」로 확정하려면 6회 응시, 3섹션 18장이면 18회.")
print("     (문헌 가정 0.20~0.30초/응시 — 저장소 안에 교정값이 없다. 가정임을 명시한다)")
print("     6장 → %.1f~%.1f초 / 18장 → %.1f~%.1f초" % (6*0.20, 6*0.30, 18*0.20, 18*0.30))
print("  ⇒ 테두리를 T=2로 올리면 주변시 채널이 1 → 2개가 된다(길이 + 경계색).")

print(); print("="*100); print("[12] L-1 이 실제로 뺀 것의 크기 — 「잃은 신호」를 잰다"); print("="*100)
print("  R17 C-1(강조 A = 등급색)이 살아 있었다면 조각이 가졌을 대비 (EQUIPMENT_PALETTE §6-(3) 실측):")
print("    카드 워시 α0.21 등급색 ↔ 카드 바탕 : 1.20 ~ 1.32   (하한 3.0 미달)")
print("    몸 등급색 ↔ 재질 채움 위          : 1.25 ~ 1.69   (하한 3.0 미달)")
print("    몸 등급색 ↔ 밝은 바탕화면/흰 잉크   : 1.27 ~ 2.91   (하한 3.0 미달, 4단 전부)")
print("  ⇒ 제거된 채널은 **어느 배경에서도 하한 3.0을 넘긴 적이 없다** = 신호가 아니었다.")
print("  ⇒ L-1 로 잃은 등급 신호량 = 0. 남은 3채널의 값은 한 칸도 안 바뀐다(위 [1]~[4] 전부 L-1 이후 값).")

# =====================================================================================
print(); print("="*100); print("[13] 처방 — 슬롯 안 「등급이 다르면 M도 달라야」 규칙의 최소 수리"); print("="*100)
print("  규칙 DS-G1 : 같은 슬롯 안에서 **등급이 다른** 두 아이템의 주 재질색 M 은 ΔE ≥ 7.8.")
print("               (같은 등급끼리의 중복은 허용 — 짧은망토/긴망토는 한 가족이다)")
NODYE = {'왕관','밀짚모자','방울목걸이','외알안경'}     # EQUIPMENT_PALETTE §4 「염색 불가」
palette = sorted(MAT.items(), key=lambda kv: kv[0])
def ok_slot(assign, grp):
    for a, b in itertools.combinations(range(6), 2):
        if RANK[grp[a][4]] != RANK[grp[b][4]] and dE76(assign[a], assign[b]) < DISCRIM:
            return False
    return True
for slot in ('HEAD','EYES','NECK','BACK'):
    grp = [i for i in ITEMS if i[0] == slot]
    cur = [i[5] for i in grp]
    if ok_slot(cur, grp):
        print("  %-5s : 위반 0건 — 손댈 것 없음" % slot); continue
    best = None
    for k in range(1, 4):
        for idxs in itertools.combinations(range(6), k):
            if any(grp[i][3] in NODYE for i in idxs): continue
            for repl in itertools.product([p[1] for p in palette], repeat=k):
                trial = list(cur)
                bad = False
                for j, i in enumerate(idxs):
                    if repl[j] == grp[i][6]: bad = True; break     # M == M2 금지
                    trial[i] = repl[j]
                if bad: continue
                if ok_slot(trial, grp):
                    names = [(grp[i][3], cur[i], trial[i]) for i in idxs]
                    best = (k, names); break
            if best: break
        if best: break
    k, names = best
    print("  %-5s : 최소 %d칸 교체로 해소 —" % (slot, k))
    for nm, a, b in names:
        matname = [x for x, v in MAT.items() if v == b][0]
        print("            %s  M %s → %s (%s)" % (nm, a, b, matname))

print(); print("="*100); print("[14] 재화 표시 색 — 등급 램프와 섞이지 않는가"); print("="*100)
COIN = [("전설색 #FFD375 (UX_SHOP §2-5 「RarityLegend」의 현행 대응)", '#FFD375'),
        ("강조 브라스 #C8A15A (UiChrome.Accent)", '#C8A15A'),
        ("본문 잉크 #F2F4F7 (TextPrimary)", '#F2F4F7'),
        ("보조 잉크 #AEB4BF (TextSecondary)", '#AEB4BF')]
print("  %-52s %-9s %-9s %-9s" % ("후보","패널 대비","램프 최소ΔE","판정"))
for nm, c in COIN:
    cr = contrast(c, PANEL); d = min(dE76(c, r) for r in RAMP)
    print("  %-52s %-9.2f %-9.2f %s" % (nm, cr, d, "★ 등급과 섞인다" if d < DISCRIM else "분리됨"))
print("  ★ UX_SHOP_AND_CURRENCY §2-5·§4 가 쓰는 상수 4개(RarityCommon/Rare/Epic/Legend, #8B939F/#3EB3A9/#C39BF5/#F0B84A)는")
print("    프로덕션에 **존재하지 않는다**(전수 grep 0건 / 양성 대조 `RarityColor` 존재). 그 절은 브라스 램프 이전 값이다.")

# =====================================================================================
print(); print("="*100); print("[15] 세트(테마) ↔ 등급 — 세트 완성이 지배당하는가"); print("="*100)
# DESIGN_SYSTEMS_STATS §1-3 확정: 주스탯 3/6/10/15 · 부스탯 1/2/4/6
MAIN = {'일반':3, '희귀':6, '영웅':10, '전설':15}
SUB  = {'일반':1, '희귀':2, '영웅':4,  '전설':6}
ITEM_TOTAL = {k: MAIN[k] + SUB[k] for k in MAIN}
SET_BONUS = 2 * 4     # 스탯별 +2 × 4스탯
print("  아이템 1개가 총합에 넣는 값(주+부): " + " / ".join("%s %d" % (k, ITEM_TOTAL[k]) for k in NAMES))
print("  세트 완성 보너스 = 스탯별 +2 × 4스탯 = **%d**" % SET_BONUS)
print()
print("  ★ 옛 축(rank 세트)에서는 최상위 세트 F = 전설 4종 = 혼합 최고와 **같은 구성**이라 완성 비용이 0이었다.")
print("     축이 테마로 바뀌면 테마 4종의 등급이 슬롯마다 제각각이 되고, 그 차이가 곧 완성 비용이다.")
print()
best_all = 4 * ITEM_TOTAL['전설']
print("  혼합 최고(4슬롯 전부 전설) 총합 = %d" % best_all)
print("  세트가 이기려면: Σstat(세트 4종) + %d > %d  ⟺  Σ > %d" % (SET_BONUS, best_all, best_all - SET_BONUS))
print()
print("  %-34s %-8s %-10s %-8s" % ("테마 4종 구성", "Σstat", "세트 총합", "판정"))
for combo in [('전설','전설','전설','전설'), ('전설','전설','전설','영웅'), ('전설','전설','영웅','영웅'),
              ('전설','전설','전설','희귀'), ('영웅','영웅','영웅','영웅'), ('희귀','희귀','희귀','희귀'),
              ('일반','일반','일반','일반')]:
    ssum = sum(ITEM_TOTAL[c] for c in combo)
    tot = ssum + SET_BONUS
    print("  %-34s %-8d %-10d %s" % ("+".join(combo), ssum, tot,
          "✔ 이긴다 (여유 %+d)" % (tot-best_all) if tot > best_all else "✘ 지배당한다 (%+d)" % (tot-best_all)))
print("\n  ⇒ 엔드게임(전설 4종 보유)에서 테마 세트가 이기려면 **4종 중 3종이 전설 + 1종이 영웅 이상**이어야 한다.")
print("     전설은 파생 규칙상 **슬롯당 정확히 1종**이므로 그 조합은 세계에 **딱 하나**다")
print("     (밀짚모자·안대·반다나·요정날개). ⇒ 테마 6개 중 스탯 축에서 살아남는 것은 **최대 1개**.")

print(); print("="*100); print("[16] DS-G7 도출 — 「1단 내림」을 몇 슬롯까지 허용할 수 있나"); print("="*100)
print("  다운그레이드 1건의 비용(그 슬롯 최고 대비):")
pairs = [('전설','영웅'),('전설','희귀'),('전설','일반'),('영웅','희귀'),('영웅','일반'),('희귀','일반')]
for a, b in pairs:
    c = ITEM_TOTAL[a] - ITEM_TOTAL[b]
    print("    %s → %s : %2d  %s" % (a, b, c, "< 8 ⇒ 1건이면 세트가 이긴다" if c < SET_BONUS else "≥ 8 ⇒ 1건만으로 세트가 진다"))
print()
print("  누적 판정 (비용 합 < %d 이어야 세트가 이긴다):" % SET_BONUS)
for k, desc in [(1,"1단 내림 1슬롯"), (2,"1단 내림 2슬롯")]:
    lo = min(ITEM_TOTAL[a]-ITEM_TOTAL[b] for a,b in [('전설','영웅'),('영웅','희귀'),('희귀','일반')])
    hi = max(ITEM_TOTAL[a]-ITEM_TOTAL[b] for a,b in [('전설','영웅'),('영웅','희귀'),('희귀','일반')])
    print("    %-14s 비용 %2d ~ %2d  ⇒ %s" % (desc, lo*k, hi*k,
          "세트 우세" if hi*k < SET_BONUS else ("동률~열세" if lo*k <= SET_BONUS else "세트 열세")))
print()
print("  ★ DS-G7 : 한 테마의 4종은 「그 슬롯 최고 등급 대비 **1단 내림**」이 **최대 1슬롯**까지만 허용된다.")
print("            2단 내림 1슬롯(10~13) 또는 1단 내림 2슬롯(8~14)은 전부 +8을 못 넘는다.")

print(); print("="*100); print("[17] 세트 완성의 상대 가치 — 시간에 따라 얼마나 얇아지나"); print("="*100)
print("  %-26s %-10s %-12s %-10s" % ("보유 최고 등급", "혼합 최고", "세트 보너스 비중", "시점(원형 B)"))
for r, day in [('일반','1일차'), ('희귀','5.4일차'), ('영웅','26.8일차'), ('전설','40.1일차')]:
    b = 4*ITEM_TOTAL[r]
    print("  %-26s %-10d %-12s %-10s" % ("4슬롯 " + r, b, "%.1f%%" % (SET_BONUS/b*100), day))
print("  ⇒ 세트의 상대 가치가 **50.0%% → 9.5%%로 5.3배 얇아진다.** 세트를 「엔드게임 목표」로 팔 수 없다.")
print("     세트가 실제로 큰 것은 **첫 주**다(1일차 50%%). 그리고 1일차 4종은 전부 무료 rank0이다.")

print(); print("="*100); print("[18] DLC 팩 세트 — 상한이 희귀일 때 스탯 순손실"); print("="*100)
print("  코드 실측: ItemCatalog.cs:836  MaxDeclaredRarityForPack = DeclaredRarity.Rare")
print("  ECONOMY_SPEC §0-6-5 판정   : 「Legendary 로 열려야 한다」  ⇒ **판정↔코드 불일치**")
for cap in ('희귀', '영웅', '전설'):
    ssum = 4*ITEM_TOTAL[cap]; tot = ssum + SET_BONUS
    print("  팩 상한 %-3s : 팩 4종 Σstat %2d + 세트 %d = %2d  vs 혼합 최고 %d  ⇒ **%+d**  %s"
          % (cap, ssum, SET_BONUS, tot, best_all, tot-best_all,
             "(세트 보너스를 다 받고도 손해)" if tot < best_all else "(동률 이상)"))
print("  ⇒ 상한이 희귀인 한 **팩 테마 세트의 스탯 논거는 −44다.** 화면이 「4스탯 +2」를 약속하면")
print("     그것은 유예 자동 해금과 **같은 형태의 결함**이다 — 존재하지 않는 이득을 약속한다.")
print("     ⇒ 팩의 세트 완성 효과는 **대사 풀 전환 · 연출 스킨**으로만 팔아야 한다(§12-4-c(1)과 일치).")

print(); print("="*100); print("[19] ★ 착용(몸)에서 등급 신호가 0인 것 — persona-newcomer 지적의 수치 판정"); print("="*100)
print("  몸의 색 예산 (동시 최대):")
print("    스탯 4슬롯 × (M + M2)          = 8")
print("    + 잉크 1 + 머리 1               = 10")
print("    persona-newcomer 실기 실측: 20pt 머리 영역에 **5색**이 겹쳐 「때 묻은 것」으로 먼저 읽혔다")
print("    등급색을 슬롯마다 1색 더하면 머리 영역(HEAD+EYES 두 슬롯)에 **+2 → 7색**")
print()
print("  등급색을 몸에 올릴 때의 대비 (EQUIPMENT_PALETTE §6-(3) 실측):")
for n, c in zip(NAMES, RAMP):
    print("    %-4s %s : 밝은 바탕 %.2f / 흰 잉크 %.2f  → 하한 3.0 %s"
          % (n, c, contrast(c, '#F2F2F2'), contrast(c, '#FFFFFF'), "미달" if min(contrast(c,'#F2F2F2'), contrast(c,'#FFFFFF')) < 3.0 else "통과"))
print("  ⇒ 크롬 대역 그대로는 4단 전부 미달. 자립 대역으로 내리면 **재질색과 구분되지 않는다**(같은 대역).")
print()
print("  ★ 그런데 몸에서 등급을 읽어서 유저가 할 수 있는 행동이 무엇인가 — **0개다.**")
print("    · 등급은 파생값이다(ItemCatalog.RarityOfMember = 슬롯 내 요구 레벨 순위). 유저가 바꿀 수 없다.")
print("    · 등급의 기계적 효과는 스탯과 가격뿐이고, 둘 다 **창 안**에서 읽는다.")
print("    · 몸에서 실시간으로 바뀌는 것은 **세트 완성 여부**다(입고 있을 때만 활성, 한 칸 벗으면 즉시 꺼짐).")
print()
print("  ⇒ 필요한 신호의 비트 수:")
print("     등급 = log2(4) = 2비트 × 4슬롯 = **8비트** — 색 예산 초과")
print("     세트 완성 = **1비트** (0/1) — 색을 하나도 안 써도 시간축(모션·이펙트)으로 말할 수 있다")
print("  ⇒ DS-G8 : **몸에 등급 신호를 만들지 않는다. 몸이 말해야 하는 것은 세트 완성 1비트다.**")
print("     채널(모션/이펙트) 선택은 design-motion 소관 — 내가 정하는 것은 「무엇을 말해야 하는가」다.")

print(); print("="*100); print("[20] 상점 카드 상태별 — 등급 3채널이 몇 개 살아남는가"); print("="*100)
print("  프로덕션 3항식(Cards.cs:422): selected > hovered > (worn && owned) > RarityBorder(등급)")
print("  리본·낱말은 ApplyCardStyle 에서 **무조건** 칠해진다(카드 경로에 HideRarityRibbon 호출 0건 — 보관함 전용)")
print()
STATES = [("S1 살 수 있다", False, False), ("S2 동전 부족", False, False), ("S3 이미 보유(미착용)", True, False),
          ("S3′ 보유 + 착용 중", True, True), ("S4 레벨 잠금", False, False), ("S5 DLC(백엔드 없음)", False, False),
          ("S6 DLC(스토어)", False, False), ("S7 가격 미정", False, False), ("Unknown(엔타이틀먼트 확인 불가)", False, False)]
print("  %-32s %-8s %-8s %-10s %s" % ("상태", "리본", "낱말", "테두리", "살아있는 채널"))
for nm, owned, worn in STATES:
    border = not (worn and owned)
    n = 2 + (1 if border else 0)
    print("  %-32s %-8s %-8s %-10s %d / 3" % (nm, "✔", "✔", "✔" if border else "✘(착용색)", n))
print("  %-32s %-8s %-8s %-10s %d / 3" % ("(어느 상태든) 호버 중 — 최대 1장", "✔", "✔", "✘(호버색)", 2))
print("  %-32s %-8s %-8s %-10s %d / 3" % ("(어느 상태든) 선택 중 — 정확히 1장", "✔", "✔", "✘(선택색)", 2))
print()
print("  ⇒ **어느 상태에서도 최소 2채널(리본 + 낱말)이 산다. 0이 되는 칸은 없다.**")
print("     테두리가 덮이는 3경우는 전부 「유저가 그 카드를 보고 있는 중」이라 중심시 채널이 작동한다(§15.5).")
print("  ⇒ 상점 탭에 등급 배지를 새로 그릴 이유가 수치상 없다(§2-3 판정과 일치).")
