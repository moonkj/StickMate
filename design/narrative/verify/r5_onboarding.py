# -*- coding: utf-8 -*-
"""R5 온보딩 대사 — 가독예산 검산기.

★ TEAM.md 「거짓 통과 신형」 대응:
   기대값을 프로덕션 함수(ReadingSeconds)로 만들지 않는다. 이 파일의 계수는
   Dialogue/DialogueKind.cs의 리터럴을 **손으로 옮겨 적은 뒤**, 디스크의 골든
   (Tests/EditMode/Golden/DialogueBudgetKoGolden.txt)의 **IEEE754 비트**로 교정한다.
   교정이 깨지면 아래 숫자를 전부 폐기한다.
"""
import struct, sys, os

ROOT = "/Users/kjmoon/App/StickMate"
GOLDEN = os.path.join(ROOT, "Assets/_Project/Scripts/Tests/EditMode/Golden/DialogueBudgetKoGolden.txt")

def f32(x):
    return struct.unpack('>f', struct.pack('>f', x))[0]

def bits(x):
    return struct.pack('>f', x).hex().upper()

# ---- DialogueKind.cs 리터럴 (손으로 옮김) ----
BASE   = f32(0.28)
PG     = f32(0.075)    # PerGlyphSeconds     — 음절 문자체계
PL     = f32(0.0472)   # PerLatinGlyphSeconds
MINS   = f32(0.62)
MAXS   = f32(2.20)
FADEIN = f32(0.06)     # DialogueTiming.FadeInSeconds
POPIN  = f32(0.18)     # DialogueTiming.PopInSeconds  (= 채택된 마진)
FADEOUT= f32(0.12)     # DialogueTiming.FadeOutSeconds
READS  = f32(2.0)      # ReadsBeforeStale

SYLL_RANGES = [(0xAC00,0xD7A3),(0x1100,0x11FF),(0x3130,0x318F),(0xA960,0xA97F),
               (0xD7B0,0xD7FF),(0x3040,0x30FF),(0x31F0,0x31FF),(0x3400,0x4DBF),
               (0x4E00,0x9FFF),(0xF900,0xFAFF)]

def is_syllabic(t):
    if not t: return True
    for ch in t:
        c = ord(ch)
        if c < 0x1100: continue
        for lo,hi in SYLL_RANGES:
            if lo <= c <= hi: return True
    return False

def reading(t, mode="op"):
    """mode='op'  : 곱·합마다 float32 라운딩 (C# 단정도 SSE 산술)
       mode='once': double로 계산하고 마지막에 한 번만 라운딩"""
    n = len(t) if t else 0
    w = PG if is_syllabic(t) else PL
    if mode == "op":
        v = f32(f32(n * w) + BASE)
    else:
        v = f32(n * w + BASE)
    if v < MINS: v = MINS
    if v > MAXS: v = MAXS
    return f32(v)

# ============================================================
# C1 — 골든 33줄 비트 대조 (교정)
# ============================================================
gold = []
with open(GOLDEN, encoding="utf-8") as f:
    for ln in f:
        if ln.startswith("#") or not ln.strip(): continue
        b, sec, txt = ln.rstrip("\n").split("\t")
        gold.append((b, float(sec), txt))

print("="*78)
print("C1 교정 — 골든 %d줄 IEEE754 비트 대조" % len(gold))
print("="*78)
for mode in ("op","once"):
    bad = [(t,b,bits(reading(t,mode))) for b,s,t in gold if bits(reading(t,mode)) != b]
    print("  라운딩 모드 %-5s : 불일치 %d건" % (mode, len(bad)))
    for t,exp,got in bad[:6]:
        print("      %-18s 골든 %s / 계산 %s" % (t, exp, got))
MODE = "op" if not [1 for b,s,t in gold if bits(reading(t,"op"))!=b] else "once"
ok = not [1 for b,s,t in gold if bits(reading(t,MODE))!=b]
print("  채택 모드: %s  →  %s" % (MODE, "교정 통과" if ok else "★ 교정 실패 — 이하 숫자 전부 폐기"))
if not ok: sys.exit(1)

# ============================================================
# 양성 대조 — 일부러 틀리면 반드시 빨개져야 한다
# ============================================================
print()
print("="*78); print("양성 대조 (전부 '검출됨'이어야 한다)"); print("="*78)
def perturbed(t, base=None, pg=None):
    n=len(t); w = (pg if pg is not None else PG) if is_syllabic(t) else PL
    b = base if base is not None else BASE
    v = f32(f32(n*w)+b)
    return f32(min(max(v,MINS),MAXS))
p1 = sum(1 for b,s,t in gold if bits(perturbed(t, pg=f32(0.0750001))) != b)
p2 = sum(1 for b,s,t in gold if bits(perturbed(t, base=f32(0.2800001))) != b)
p3 = sum(1 for b,s,t in gold if bits(f32(min(max(f32(len(t)*0.075+0.28),MINS),MAXS))) != b)  # double 미라운딩
print("  P1 음절계수만 1e-7 흔듦        : 불일치 %2d건  %s" % (p1, "검출됨" if p1 else "★ 못 잡음"))
print("  P2 기본시간만 1e-7 흔듦        : 불일치 %2d건  %s" % (p2, "검출됨" if p2 else "★ 못 잡음"))
print("  P3 상수 float32 미확장(double) : 불일치 %2d건  %s" % (p3, "검출됨" if p3 else "★ 못 잡음"))
p4 = is_syllabic("Wi-Fi 끊겼네"), is_syllabic("Nice spot."), is_syllabic("")
print("  P4 문자체계 판정 (혼합/라틴/빈): %s  %s" % (p4, "검출됨" if p4==(True,False,True) else "★ 이상"))
p5t = "Whoa, that's high"
p5 = (round(1.20 - FADEIN - reading(p5t,MODE),3), round(1.20 - FADEIN - reading(p5t,MODE) - POPIN,3))
print("  P5 R4의 %r Climb 1.20         : 마진0 여유 %+0.3f / 마진0.18 여유 %+0.3f  %s"
      % (p5t, p5[0], p5[1], "재현" if p5[0]>0 and p5[1]<0 else "★ 불일치"))

# ============================================================
# 산식 재현 — R4 §1-4 예산표를 독립 재계산
# ============================================================
print()
print("="*78); print("R4 §1-4 예산표 독립 재현 (분모 - 0.06 - R - 0.18 >= 0 인 최대 글자수)"); print("="*78)
def maxlen(den, latin):
    best=0
    for n in range(1,60):
        t = ("a"*n) if latin else ("가"*n)
        if den - FADEIN - reading(t,MODE) - POPIN >= 0: best=n
        else: break
    return best
for name,den,ref_en,ref_ko in [("Idle",1.65,23,15),("Walk",1.24,15,9),("ParkourClimb",1.20,14,9),("LedgeHang",1.12,12,7)]:
    en,ko = maxlen(den,True), maxlen(den,False)
    print("  %-13s 분모 %.2f  영어 %2d자(R4:%2d) %s   한국어 %2d자(R4:%2d) %s"
          % (name,den,en,ref_en,"OK" if en==ref_en else "★",ko,ref_ko,"OK" if ko==ref_ko else "★"))

# ============================================================
# 온보딩 — 후보 문안 계산
# ============================================================
print()
print("="*78); print("온보딩 후보 문안 — 슬롯 요구치"); print("="*78)
KO_CPS_SLOW, KO_CPS_FAST = 5.0, 6.7          # UX_FLOW 48-3-2 (가정, 측정 아님)
RATIO = 112/178                               # DialogueKind.cs PerLatinGlyphSeconds 유도에 쓰인 비율
EN_CPS_SLOW, EN_CPS_FAST = KO_CPS_SLOW/RATIO, KO_CPS_FAST/RATIO

def report(tag, t):
    n=len(t); R=reading(t,MODE); syl=is_syllabic(t)
    need = f32(FADEIN + R)                    # RequiredDwellSeconds
    slot = f32(need + POPIN)                  # + 채택 마진
    maxvis = f32(POPIN + READS*R + FADEOUT)   # MaxVisibleSecondsFor(m=1.0)
    cs_slow, cs_fast = (KO_CPS_SLOW,KO_CPS_FAST) if syl else (EN_CPS_SLOW,EN_CPS_FAST)
    read_slow, read_fast = n/cs_slow, n/cs_fast
    ok_slow = maxvis >= read_slow
    print("  %-6s %-34s %2d자 R=%.3f 필요체류=%.3f 최소슬롯=%.3f 노출상한=%.3f 정독 %.2f~%.2f초 %s"
          % (tag, '"'+t+'"', n, R, need, slot, maxvis, read_fast, read_slow,
             "OK" if ok_slow else "★느린독자 초과"))
    return dict(tag=tag,text=t,n=n,R=R,need=need,slot=slot,maxvis=maxvis,
                read_slow=read_slow,read_fast=read_fast,ok_slow=ok_slow)

CAND = [
 ("a-KO","안녕, 나 여기 살아"),
 ("a-EN","Hi. I live here now."),
 ("b-KO","눌러 봤어. 이게 메뉴야"),
 ("b-EN","Pressed it. That's the menu."),
 ("c-KO","이렇게 옮길 수 있어"),
 ("c-EN","It moves. Put it anywhere."),
]
rows=[report(k,v) for k,v in CAND]

print()
print("="*78); print("온보딩 작성 상한 — 두 개의 천장"); print("="*78)
def sat_ceiling(latin):
    for n in range(1,80):
        t=("a"*n) if latin else ("가"*n)
        if reading(t,MODE) >= MAXS: return n-1
    return None
def comp_ceiling(latin):
    cps = EN_CPS_SLOW if latin else KO_CPS_SLOW
    best=0
    for n in range(1,80):
        t=("a"*n) if latin else ("가"*n)
        mv = f32(POPIN + READS*reading(t,MODE) + FADEOUT)
        if mv >= n/cps: best=n
        else: break
    return best
print("  ① 예산 포화 천장 (R이 %.2f초 상한에 붙어 더 안 늘어남)" % MAXS)
print("       한국어 %2d자 / 영어 %2d자" % (sat_ceiling(False), sat_ceiling(True)))
print("  ② 정독 천장 (노출상한 < 느린 독자의 정독 시간이 되는 지점)")
print("       한국어 %2d자 / 영어 %2d자   ← 이쪽이 먼저 걸린다" % (comp_ceiling(False), comp_ceiling(True)))
print("       가정: 한글 %.1f자/s (UX_FLOW 48-3-2, 측정 아님) / 영어 %.2f자/s (=%.1f÷%.4f)"
      % (KO_CPS_SLOW, EN_CPS_SLOW, KO_CPS_SLOW, RATIO))

print()
print("="*78); print("3발화 연속 — 총 소요 (마진 포함 최소 슬롯의 합)"); print("="*78)
ko = [r for r in rows if r["tag"].endswith("KO")]
en = [r for r in rows if r["tag"].endswith("EN")]
for lang,rs in (("한국어",ko),("영어",en)):
    tot=sum(r["slot"] for r in rs)
    print("  %s: %s = %.3f초" % (lang," + ".join("%.3f"%r["slot"] for r in rs), tot))

# ============================================================
# 확정 문안 — 최종 6줄 + 연쇄 타임라인
# ============================================================
print()
print("="*78); print("확정 문안 6줄"); print("="*78)
FINAL = [
 ("a 인사",   "안녕, 나 여기 살아",  "Hi. I live here."),
 ("b 메뉴",   "이게 메뉴야",        "This is the menu."),
 ("c 위치",   "이렇게 옮겨도 돼",    "Like this. Drag it."),
]
tot_ko = tot_en = 0.0
print("  %-8s %-24s %3s %7s %8s %8s %8s  %s" % ("슬롯","문안","자","R","필요체류","최소슬롯","노출상한","정독(느림)"))
for tag, ko, en in FINAL:
    for lang, t in (("KO",ko),("EN",en)):
        n=len(t); R=reading(t,MODE); syl=is_syllabic(t)
        need=f32(FADEIN+R); slot=f32(need+POPIN); mv=f32(POPIN+READS*R+FADEOUT)
        cps = KO_CPS_SLOW if syl else EN_CPS_SLOW
        ceil = 17 if syl else 27
        flag = "OK" if (n<=ceil and mv>=n/cps) else "★"
        print("  %-8s %-24s %3d %7.3f %8.3f %8.3f %8.3f  %5.2f초  %s"
              % (tag+"/"+lang, '"'+t+'"', n, R, need, slot, mv, n/cps, flag))
        if lang=="KO": tot_ko += slot
        else: tot_en += slot
print("  ── 3발화 최소 소요: 한국어 %.3f초 / 영어 %.3f초 (발화 구간만. 낙하·착지·손올리기 제외)"
      % (tot_ko, tot_en))

print()
print("="*78); print("충돌 검산 — 부채꼴 온보딩 알약(4.5초)과의 겹침"); print("="*78)
PILL = 4.5; PILL_FADE = 0.09
b_ko = f32(FADEIN + reading(FINAL[1][1],MODE) + POPIN)
c_ko = f32(FADEIN + reading(FINAL[2][1],MODE) + POPIN)
print("  부채꼴 Expand 시각을 t=0으로 두면(UX_FLOW 48-3-1):")
print("    알약 노출 구간            t = 0.000 ~ %.3f초" % (PILL+PILL_FADE))
print("    말풍선 (b) 최소 구간      t = 0.301 ~ %.3f초   ← Open 확정 시점부터" % (0.301+b_ko))
print("    말풍선 (c) 최소 구간      t = %.3f ~ %.3f초" % (0.301+b_ko, 0.301+b_ko+c_ko))
print("    → 화면에 글자 두 덩어리가 동시에 있는 시간 = %.3f초" % min(PILL, 0.301+b_ko+c_ko))
print("    → 알약은 뜨는 순간 PlayerPrefs에 '봤다'로 기록된다(GearRadialMenuWidget.TryStartOnboardingHint)")

print()
print("="*78); print("잡담 침범 검산 — (c) 직후 Idle 복귀"); print("="*78)
print("  Idle.Enter가 AmbientChatter.TryRollChatter를 굴린다. Reaction인 (c)는 IsEligible을 통과시키지")
print("  않고(반응은 무조건 통과), 교체 가드는 팝인 %.2f초만 본다." % POPIN)
print("  → (c)가 %.3f초 만에 잡담으로 덮일 수 있다. 요구: Idle 복귀 시점에" % POPIN)
print("     NextChatterAllowedUnscaledTime >= now + %.3f초 (= (c) 최소슬롯)" % c_ko)

# ============================================================
# R5-b — design-motion 확정 박자에서 예산을 다시 판다
#   올림 0.80 → 유지 1.60 → 흔들기 1.60 → 내림 0.65  = 4.65초
# ============================================================
print()
print("="*78); print("R5-b  확정 박자별 슬롯 수용력 (독립 재계산)"); print("="*78)
BEAT = [("올림",0.80),("유지",1.60),("흔들기",1.60),("내림",0.65)]
def cap(den, latin, margin):
    best=0
    for n in range(1,80):
        t=("a"*n) if latin else ("가"*n)
        if den - FADEIN - reading(t,MODE) - margin >= 0: best=n
        else: break
    return best
print("  구간      지속    [마진 0.18 포함]        [마진 없음 = 참고]")
print("  ------    ----    한국어   영어           한국어   영어")
for name,d in BEAT:
    a,b = cap(d,False,POPIN), cap(d,True,POPIN)
    c,e = cap(d,False,0.0),  cap(d,True,0.0)
    print("  %-8s %.2f초  %5s %6s        %5s %6s" %
          (name, d, (str(a)+"자" if a else "불가"), (str(b)+"자" if b else "불가"),
                    (str(c)+"자" if c else "불가"), (str(e)+"자" if e else "불가")))
floor = f32(FADEIN + MINS + POPIN)
print("  ★ 어떤 문장이든 필요한 최소 구간 = 페이드인 0.06 + 예산하한 %.2f + 마진 %.2f = %.3f초"
      % (MINS, POPIN, floor))
print("     → 올림 0.80초 · 내림 0.65초는 %.3f초에 못 미친다. **가장 짧은 감탄사도 못 넣는다.**" % floor)
print("     → 발화 가능 구간은 「유지」와 「흔들기」 둘뿐이다.  발화 슬롯 = 2개")

print()
print("="*78); print("R5-b  UX_FLOW 35-2-2 ② INTRO_GREET (2.6초) 수용력"); print("="*78)
for den in (2.6,):
    print("  분모 %.2f초 → 한국어 %d자 / 영어 %d자 (예산 기준)" % (den, cap(den,False,POPIN), cap(den,True,POPIN)))
    print("  ★ 단 §6 정독 천장(한국어 17자 / 영어 27자)이 먼저 걸린다 — 실효 상한은 17 / 27자다.")

print()
print("="*78); print("R5-b  (a) 인사 — ②의 착지 대상 분기를 유지하는 통합형"); print("="*78)
GREET = [
 ("Dock/작업표시줄", "안녕, 여기 서 있어도 되지?", "Hi. Mind if I stand here?"),
 ("일반 창 상단",   "안녕, 이 창 잠깐 빌릴게",   "Hi. Borrowing this window."),
 ("합성 바닥 발판", "안녕, 바닥이네. 여기도 좋고", "Hi. The floor. Works for me."),
 ("(분기 없음 단독)","안녕, 나 여기 살아",        "Hi. I live here."),
]
for tag,ko,en in GREET:
    for lang,t in (("KO",ko),("EN",en)):
        n=len(t); R=reading(t,MODE); syl=is_syllabic(t)
        slot=f32(FADEIN+R+POPIN); ceil=17 if syl else 27
        cps=KO_CPS_SLOW if syl else EN_CPS_SLOW
        mv=f32(POPIN+READS*R+FADEOUT)
        ok = (n<=ceil) and (2.6-FADEIN-R-POPIN>=0) and (mv>=n/cps)
        print("  %-14s %-30s %2d자 R=%.3f 최소슬롯=%.3f ②여유=%+.3f %s"
              % (tag if lang=="KO" else "", '"'+t+'"', n, R, slot, 2.6-FADEIN-R-POPIN, "OK" if ok else "★"))

print()
print("="*78); print("R5-b  (c) 위치 — 「흔들기」의 대상이 무엇이냐로 문안이 갈린다"); print("="*78)
for tag,ko,en in [("톱니가 흔들린다(지시 대상 있음)","이렇게 옮겨도 돼","Like this. Drag it."),
                  ("손만 흔들린다(지시 대상 없음)","잡아 끌면 옮겨져","Drag it anywhere.")]:
    for lang,t in (("KO",ko),("EN",en)):
        n=len(t); R=reading(t,MODE); slot=f32(FADEIN+R+POPIN)
        print("  %-30s %-24s %2d자 R=%.3f 최소슬롯=%.3f 흔들기1.60여유=%+.3f"
              % (tag if lang=="KO" else "", '"'+t+'"', n, R, slot, 1.60-FADEIN-R-POPIN))
