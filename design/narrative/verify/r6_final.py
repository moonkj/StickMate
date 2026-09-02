# -*- coding: utf-8 -*-
"""R6 — 온보딩 최종본 / 영어 Walk 5줄 / 팩「야간 정비반」 어조 검산기.

TEAM.md 「거짓 통과 신형」 대응:
  기대값을 프로덕션 함수로 만들지 않는다. 계수는 Dialogue/DialogueKind.cs 의 리터럴을
  **손으로 옮겨 적은 뒤**, 디스크의 골든(Tests/EditMode/Golden/DialogueBudgetKoGolden.txt)의
  **IEEE754 비트**로 교정한다. 교정이 깨지면 아래 숫자를 전부 폐기한다.
"""
import struct, sys, os, re, glob

ROOT   = "/Users/kjmoon/App/StickMate"
GOLDEN = os.path.join(ROOT, "Assets/_Project/Scripts/Tests/EditMode/Golden/DialogueBudgetKoGolden.txt")
CFG_CS = os.path.join(ROOT, "Assets/_Project/Scripts/Core/StickConfig.cs")
CFG_AS = os.path.join(ROOT, "Assets/_Project/Data/DefaultStickConfig.asset")
REND   = os.path.join(ROOT, "Assets/_Project/Scripts/Dialogue/DialogueBubbleRenderer.cs")
CHAT   = os.path.join(ROOT, "Assets/_Project/Scripts/Dialogue/AmbientChatter.cs")
ITEMS  = os.path.join(ROOT, "Assets/_Project/Resources/Items")

def f32(x): return struct.unpack('>f', struct.pack('>f', x))[0]
def bits(x): return struct.pack('>f', x).hex().upper()
def H(s):  print("\n" + "=" * 86); print(s); print("=" * 86)

# ---- DialogueKind.cs / DialogueTiming 리터럴 (손으로 옮김) ----
BASE, PG, PL   = f32(0.28), f32(0.075), f32(0.0472)
MINS, MAXS     = f32(0.62), f32(2.20)
FADEIN, POPIN  = f32(0.06), f32(0.18)
FADEOUT, READS = f32(0.12), f32(2.0)
# DialogueBubbleRenderer.cs 팝인 곡선 리터럴
POP_START, POP_PEAK_SCALE, POP_PEAK_AT = 0.55, 1.12, 0.6

SYL = [(0xAC00,0xD7A3),(0x1100,0x11FF),(0x3130,0x318F),(0xA960,0xA97F),(0xD7B0,0xD7FF),
       (0x3040,0x30FF),(0x31F0,0x31FF),(0x3400,0x4DBF),(0x4E00,0x9FFF),(0xF900,0xFAFF)]
def is_syl(t):
    if not t: return True
    for ch in t:
        c = ord(ch)
        if c < 0x1100: continue
        for lo,hi in SYL:
            if lo <= c <= hi: return True
    return False
def reading(t, mode="op"):
    n = len(t) if t else 0
    w = PG if is_syl(t) else PL
    v = f32(f32(n*w)+BASE) if mode == "op" else f32(n*w+BASE)
    return f32(min(max(v, MINS), MAXS))

FAIL = []
def need(cond, msg):
    if not cond: FAIL.append(msg)
    return cond

# ============================================================================
H("C1 교정 — 골든 IEEE754 비트 대조")
# ============================================================================
gold = []
for ln in open(GOLDEN, encoding="utf-8"):
    if ln.startswith("#") or not ln.strip(): continue
    b, sec, txt = ln.rstrip("\n").split("\t")
    gold.append((b, float(sec), txt))
for mode in ("op", "once"):
    bad = [t for b,s,t in gold if bits(reading(t,mode)) != b]
    print("  라운딩 모드 %-5s : 불일치 %d건" % (mode, len(bad)))
MODE = "op" if not [1 for b,s,t in gold if bits(reading(t,"op")) != b] else "once"
ok = not [1 for b,s,t in gold if bits(reading(t,MODE)) != b]
print("  골든 %d줄 / 채택 모드 %s → %s" % (len(gold), MODE, "교정 통과" if ok else "★ 교정 실패"))
if not ok: sys.exit(1)

H("양성 대조 (전부 '검출됨'이어야 한다)")
def pert(t, base=None, pg=None):
    n = len(t); w = (pg if pg is not None else PG) if is_syl(t) else PL
    v = f32(f32(n*w) + (base if base is not None else BASE))
    return f32(min(max(v, MINS), MAXS))
p1 = sum(1 for b,s,t in gold if bits(pert(t, pg=f32(0.0750001))) != b)
p2 = sum(1 for b,s,t in gold if bits(pert(t, base=f32(0.2800001))) != b)
p3 = sum(1 for b,s,t in gold if bits(f32(min(max(f32(len(t)*0.075+0.28),MINS),MAXS))) != b)
p4 = (is_syl("Wi-Fi 끊겼네"), is_syl("Nice spot."), is_syl(""))
print("  P1 음절계수 1e-7 흔듦          : %2d건  %s" % (p1, "검출됨" if p1 else "★못잡음"))
print("  P2 기본시간 1e-7 흔듦          : %2d건  %s" % (p2, "검출됨" if p2 else "★못잡음"))
print("  P3 상수 float32 미확장         : %2d건  %s" % (p3, "검출됨" if p3 else "★못잡음"))
print("  P4 문자체계 판정(혼합/라틴/빈) : %s  %s" % (p4, "검출됨" if p4==(True,False,True) else "★이상"))
need(p1 and p2 and p3 and p4==(True,False,True), "양성 대조 실패")

# ============================================================================
H("C2 분모 재확인 — 코드와 애셋을 각각 읽고 대조 (거짓통과 #9: 애셋이 코드를 이긴다)")
# ============================================================================
cs = open(CFG_CS, encoding="utf-8").read()
asset = open(CFG_AS, encoding="utf-8").read()
def from_cs(name):
    m = re.search(r"public float %s\s*=\s*([0-9.]+)f;" % name, cs)
    return float(m.group(1)) if m else None
def from_asset(name):
    m = re.search(r"^\s*%s:\s*([0-9.]+)\s*$" % name, asset, re.M)
    return float(m.group(1)) if m else None
KEYS = ["wanderIdleDurationMin","wanderWalkDurationMin","wanderDurationJitterRatio",
        "parkourClimbDuration","ledgeHangGrabDuration","ledgeHangHoldDurationMin"]
vals = {}
print("  %-30s %10s %10s %s" % ("필드","StickConfig.cs","asset","일치"))
for k in KEYS:
    a, b = from_cs(k), from_asset(k)
    same = (a is not None and b is not None and abs(a-b) < 1e-9)
    print("  %-30s %10s %10s %s" % (k, a, b, "OK" if same else "★불일치"))
    need(same, "코드/애셋 불일치: %s" % k)
    vals[k] = a
# 양성 대조 — 존재하지 않는 필드는 반드시 None/None 이어야 한다(프로브가 죽지 않았음을 보인다)
print("  [양성대조] 없는 필드 zzzNoSuchField : cs=%s asset=%s  %s"
      % (from_cs("zzzNoSuchField"), from_asset("zzzNoSuchField"),
         "프로브 정상" if from_cs("zzzNoSuchField") is None else "★프로브 이상"))
need(from_cs("zzzNoSuchField") is None and from_asset("zzzNoSuchField") is None, "프로브 양성대조 실패")

J = vals["wanderDurationJitterRatio"]
DEN = {
  "Idle":        vals["wanderIdleDurationMin"] * (1.0 - J),
  "Walk":        vals["wanderWalkDurationMin"] * (1.0 - J),
  "ParkourClimb":vals["parkourClimbDuration"],
  "LedgeHang":   vals["ledgeHangGrabDuration"] + vals["ledgeHangHoldDurationMin"],
}
print()
print("  분모 = 지속 하한 × (1 − 지터 %.3f)   [Jitter(): AutoWanderController.cs:738-742]" % J)
for k,v in DEN.items(): print("    %-13s %.4f 초" % (k, v))
print("  ★ R4 표의 Walk 1.24 는 %.4f 의 반올림이다 — 0.0025초 낙관. 여유 < 0.0025 인 줄은 무효." % DEN["Walk"])

# ============================================================================
H("C3 등장 연출 — 팝인과 페이드인은 동시인가 (design-motion 반박 재확인)")
# ============================================================================
rend = open(REND, encoding="utf-8").read().split("\n")
def lineno(needle, frm=0):
    for i in range(frm, len(rend)):
        if needle in rend[i]: return i+1
    return None
l_alpha0 = lineno("_alpha = 0f;")
l_pop0   = lineno("_popElapsed = 0f;")
l_popadd = lineno("_popElapsed += dt;")
l_alphaa = lineno("_alpha = Mathf.Min(1f, _alpha + dt /")
print("  ShowInternal  _alpha = 0f        : %s" % l_alpha0)
print("  ShowInternal  _popElapsed = 0f   : %s" % l_pop0)
print("  LateUpdate    _popElapsed += dt  : %s" % l_popadd)
print("  LateUpdate    _alpha += dt/FadeIn: %s" % l_alphaa)
same_call = (l_alpha0 is not None and l_pop0 is not None and l_pop0 - l_alpha0 < 30)
print("  → 같은 호출에서 둘 다 0으로 리셋되고 같은 dt로 굴러간다 : %s" % ("확인" if same_call else "★불확인"))
need(same_call, "팝인/페이드인 동시성 확인 실패")
print("  ⇒ 등장 연출 총 길이 = max(FadeIn %.2f, PopIn %.2f) = %.2f초  (0.24 아님)" % (FADEIN, POPIN, max(FADEIN,POPIN)))

def smoothstep(u): return u*u*(3.0-2.0*u)
def pop_scale(t_norm):
    if t_norm >= 1.0: return 1.0
    t = max(0.0, min(1.0, t_norm))
    if t < POP_PEAK_AT:
        s = smoothstep(t/POP_PEAK_AT);          return POP_START + (POP_PEAK_SCALE-POP_START)*s
    s = smoothstep((t-POP_PEAK_AT)/(1.0-POP_PEAK_AT)); return POP_PEAK_SCALE + (1.0-POP_PEAK_SCALE)*s
lo, hi = 0.0, POP_PEAK_AT
for _ in range(200):
    mid = (lo+hi)/2
    if pop_scale(mid) < 1.0: lo = mid
    else: hi = mid
t_full = hi * POPIN
print("  팝인 곡선 0.55 →(peak 0.6) 1.12 → 1.0 : 배율이 처음 1.00에 닿는 시각 = %.4f초" % t_full)
print("  (그 뒤 팝인 끝 %.2f초까지는 항상 100%% 이상 — 오버슈트 구간)" % POPIN)

# ============================================================================
H("C4 세 가지 하한 모델 — 1.60초 유지 슬롯 / 최소 슬롯")
# ============================================================================
def maxlen(avail_R, latin):
    best = 0
    for n in range(1, 80):
        t = ("a"*n) if latin else ("가"*n)
        if reading(t, MODE) <= avail_R: best = n
        else: break
    return best
SLOT = 1.60
models = [
  ("A 마진모델 (내 R5)   slot − FadeIn − R − 마진0.18 ≥ 0", SLOT-FADEIN-POPIN, FADEIN+MINS+POPIN),
  ("B 물리 하한 (motion) slot − PopIn  − R        ≥ 0", SLOT-POPIN,        POPIN+MINS),
  ("C 가독 개시 (곡선)   slot − %.4f − R  ≥ 0" % t_full, SLOT-t_full,       t_full+MINS),
]
print("  %-52s %6s %6s %10s" % ("모델", "한국어", "영어", "최소슬롯"))
res = {}
for name, availR, minslot in models:
    ko, en = maxlen(availR, False), maxlen(availR, True)
    res[name[0]] = (ko, en, minslot)
    print("  %-52s %5d자 %5d자 %9.3f초" % (name, ko, en, minslot))
need(res["A"][:2] == (14,22), "A 모델이 14/22 가 아니다: %s" % (res["A"][:2],))
need(res["B"][:2] == (15,24), "B 모델이 15/24 가 아니다: %s" % (res["B"][:2],))
need(abs(res["A"][2]-0.860) < 5e-4, "A 최소슬롯이 0.860 이 아니다")
need(abs(res["B"][2]-0.800) < 5e-4, "B 최소슬롯이 0.800 이 아니다")
print("  ⇒ 리더가 전달한 물리 하한 15/24 · 최소슬롯 0.800초 : 재확인 통과")
print("  ⇒ 채택 14/22 · 0.860초는 B 위에 얹은 마진이다. 마진은 하드 경계가 아니다(차이 %.2f초)."
      % (res["A"][2]-res["B"][2]))

# ============================================================================
H("C5 R4 §1-4 예산표 독립 재현 (분모는 C2에서 재유도한 값)")
# ============================================================================
print("  %-13s %8s %22s %22s" % ("상태","분모","영어(R4)","한국어(R4)"))
REF = {"Idle":(23,15), "Walk":(15,9), "ParkourClimb":(14,9), "LedgeHang":(12,7)}
for k,(re_en, re_ko) in REF.items():
    availR = DEN[k] - FADEIN - POPIN
    en, ko = maxlen(availR, True), maxlen(availR, False)
    print("  %-13s %8.4f %14d자 (R4:%2d) %s %12d자 (R4:%2d) %s"
          % (k, DEN[k], en, re_en, "OK" if en==re_en else "★", ko, re_ko, "OK" if ko==re_ko else "★"))
    need(en==re_en and ko==re_ko, "%s 예산 불일치" % k)

# ============================================================================
H("C6 ★ 자기 정정 — 「정독 천장」은 팝인을 읽는 시간으로 세고 있었다")
# ============================================================================
KO_CPS, RATIO = 5.0, 112/178          # UX_FLOW 48-3-2 (가정, 측정 아님)
EN_CPS = KO_CPS / RATIO
def maxvis(t): return f32(POPIN + READS*reading(t,MODE) + FADEOUT)
def ceil_uncut(latin, count_popin_as_reading):
    cps = EN_CPS if latin else KO_CPS
    best = 0
    for n in range(1, 90):
        t = ("a"*n) if latin else ("가"*n)
        win = maxvis(t) - (0.0 if count_popin_as_reading else POPIN)
        if win >= n/cps: best = n
        else: break
    return best
print("  느린 독자 가정: 한글 %.1f자/s / 영어 %.3f자/s (=%.1f ÷ %.4f)" % (KO_CPS, EN_CPS, KO_CPS, RATIO))
print("  R5가 쓴 천장 (팝인을 읽기 시간에 포함) : 한국어 %2d자 / 영어 %2d자   ← R5 §6이 적은 17 / 27"
      % (ceil_uncut(False,True), ceil_uncut(True,True)))
print("  R6 정정      (팝인은 읽기 시간이 아님) : 한국어 %2d자 / 영어 %2d자   ← 이번 라운드 채택"
      % (ceil_uncut(False,False), ceil_uncut(True,False)))
need(ceil_uncut(False,True)==17 and ceil_uncut(True,True)==27, "R5 천장 재현 실패")
CEIL_KO, CEIL_EN = ceil_uncut(False,False), ceil_uncut(True,False)
print("  ⇒ R5의 17/27은 「팝인 0.18초 동안에도 읽는다」를 전제했다. 리더 답2가 그 전제를 부정했으므로")
print("     같은 부정을 여기에도 적용해야 일관된다. 내 R5 §6은 그만큼 낙관이었다(자기 정정).")

# ============================================================================
H("C7 읽기창 모델 — 「무엇이 언제 이 줄을 교체하는가」")
# ============================================================================
print("  읽기창(초) = min(다음 대사까지의 시간 D, 노출상한 MaxVisible) − 팝인 0.18")
print("  두 개의 천장이 동시에 걸린다:")
print("    ① 잘림   : D ≥ FadeIn 0.06 + R + 마진 0.18      (규칙 8 + 채택 마진)")
print("    ② 정독   : 글자수 ≤ cps × 읽기창")
def cap_replaced(D, latin):
    cps = EN_CPS if latin else KO_CPS
    best = 0
    for n in range(1, 90):
        t = ("a"*n) if latin else ("가"*n)
        win = min(D, maxvis(t)) - POPIN
        okr = win >= n/cps
        okc = D >= FADEIN + reading(t,MODE) + POPIN
        if okr and okc: best = n
        else: break
    return best
print()
print("  %8s %10s %10s   %s" % ("D(초)","한국어","영어","비고"))
for D, note in [(1.60,"⑦-④ 유지 · (b)"), (2.25,"⑦-⑤ 흔들기+내림 · (c)"),
                (2.60,"② INTRO_GREET"), (2.00,"③ DEMO_WALK 최소 요구"),
                (3.00,"⑤ DEMO_PASSTHRU (억제 없음)")]:
    print("  %8.2f %9d자 %9d자   %s" % (D, cap_replaced(D,False), cap_replaced(D,True), note))
print("  %8s %9d자 %9d자   교체되지 않는 줄(잡담 억제 시)" % ("∞", CEIL_KO, CEIL_EN))

# ============================================================================
H("C8 온보딩 최종 문안 — 자 → 초 → 그 구간 지속시간")
# ============================================================================
ONB = [
 ("② GREET/Dock",   2.60, "안녕. 여기 서도 돼?",  "Hi. Room for me?"),
 ("② GREET/창",     2.60, "안녕. 이 창 빌릴게",   "Hi. Good ledge."),
 ("② GREET/바닥",   2.60, "안녕. 음, 바닥이네",   "Hi. Floor it is."),
 ("③ DEMO_WALK",    2.00, "이렇게 돌아다녀",      "I get around."),
 ("④ CLIMB 실패",   1.60, "못 올라갔네",          "No luck."),
 ("⑤ PASSTHRU",     3.00, "나만 빼고 다 눌려",    "Only I take clicks."),
 ("⑦-④ (b) 메뉴",   1.60, "이게 메뉴야",          "The menu."),
 ("⑦-⑤ (c) 위치",   2.25, "이렇게 옮겨도 돼",     "See? It moves."),
]
print("  %-14s %6s %-20s %3s %6s %7s %7s %7s %6s %s"
      % ("슬롯","D","문안","자","R","필요체류","읽기창","정독","여유","판정"))
for tag, D, ko, en in ONB:
    for lang, t in (("KO",ko), ("EN",en)):
        n = len(t); R = reading(t,MODE)
        needd = f32(FADEIN + R); slack = D - needd - POPIN
        win = min(D, maxvis(t)) - POPIN
        cps = KO_CPS if is_syl(t) else EN_CPS
        rd = n/cps
        okc, okr = slack >= 0, win >= rd
        verdict = "OK" if (okc and okr) else ("★잘림" if not okc else "★정독초과")
        print("  %-14s %6.2f %-20s %3d %6.3f %7.3f %7.3f %7.3f %+6.3f %s"
              % (tag if lang=="KO" else "", D, '"'+t+'"'+("" if lang=="KO" else " (EN)"),
                 n, R, needd, win, rd, slack, verdict))
        need(okc and okr, "온보딩 탈락: %s %s %r" % (tag, lang, t))
    print()

# ============================================================================
H("C9 영어 Walk 5줄 — 프로덕션 풀과 1:1 (분모 %.4f)" % DEN["Walk"])
# ============================================================================
src = open(CHAT, encoding="utf-8").read()
def pool(name):
    m = re.search(name + r"\s*=\s*\{(.*?)\n\s*\};", src, re.S)
    return re.findall(r'^\s*"((?:[^"\\]|\\.)*)",?\s*$', m.group(1), re.M)
prod_walk, prod_idle = pool("WalkLines"), pool("IdleLines")
print("  프로덕션 WalkLines %d줄 / IdleLines %d줄 (AmbientChatter.cs 실측)" % (len(prod_walk), len(prod_idle)))
need(len(prod_walk) == 5, "프로덕션 Walk 풀이 5줄이 아니다")
EN_WALK = ["Out walking.", "Off that way.", "Left, right.", "Loosening up.", "Good stride."]
SLOTS   = ["MOVE", "DIR", "RHYTHM", "LEGS", "FLOURISH"]
DW = DEN["Walk"]
print("  %-9s %-13s %3s %6s %8s | %-15s %3s %6s %8s %8s"
      % ("슬롯","한국어(현행)","자","R","여유","English","자","R","여유","작업상한"))
for slot, ko, en in zip(SLOTS, prod_walk, EN_WALK):
    rk, re_ = reading(ko,MODE), reading(en,MODE)
    sk = DW - FADEIN - rk - POPIN
    se = DW - FADEIN - re_ - POPIN
    lim = "OK(≤14)" if len(en) <= 14 else "★초과"
    print("  %-9s %-13s %3d %6.3f %+8.3f | %-15s %3d %6.3f %+8.3f %8s"
          % (slot, ko, len(ko), rk, sk, en, len(en), re_, se, lim))
    need(sk > 0 and se > 0 and len(en) <= 14, "Walk 줄 탈락: %s / %s" % (ko, en))
print("  ★ 여유는 전부 0.0025초(1.24 반올림 낙관분)보다 크다 : %s"
      % ("확인" if min(DW-FADEIN-reading(x,MODE)-POPIN for x in prod_walk+EN_WALK) > 0.0025 else "★위험"))
print("  ★ 정독 — 교체되지 않을 때 : 한국어 최장 %d자(천장 %d) / 영어 최장 %d자(천장 %d)"
      % (max(len(x) for x in prod_walk), CEIL_KO, max(len(x) for x in EN_WALK), CEIL_EN))
print("  ★ 최악(분모 %.4f에서 상태 종료 즉시 컷, 서술 대사)에서는 느린 독자가 완독 못 한다 —" % DW)
for ko, en in zip(prod_walk, EN_WALK):
    for t in (ko, en):
        cps = KO_CPS if is_syl(t) else EN_CPS
        w = DW - POPIN
        if w < len(t)/cps:
            print("      %-16s 읽기창 %.3f < 정독 %.3f  (앰비언트는 반복 노출이라 치명적이지 않다)"
                  % ('"'+t+'"', w, len(t)/cps))

# ============================================================================
H("C10 팩「야간 정비반」 세트 G — 10슬롯 · 예산 · 어조 분리도")
# ============================================================================
# 격식: 0 무종결(명사·부사) / 1 해체(-네·-지·-해) / 2 평서(-다) / 3 격식(-군·-겠다)
# 시선: 사물 주어 = 1  (중립 풀로 교정: 여기/발밑/다리 = 3/10 = 30%)
G = [
 ("PAUSE",   "Idle", "자...",          "Mm...",          0, 0),
 ("PLACE",   "Idle", "이 구역 이상 무", "Sector clear.",  0, 1),
 ("REST",    "Idle", "정비 대기",       "On standby.",    0, 0),
 ("GROUND",  "Idle", "바닥 단단함",     "Floor's firm.",  0, 1),
 ("SELF",    "Idle", "난 이게 편해",    "This suits me.", 1, 0),
 ("MOVE",    "Walk", "순찰 중",         "On rounds.",     0, 0),
 ("DIR",     "Walk", "다음 구역으로",   "Next section.",  0, 0),
 ("RHYTHM",  "Walk", "보폭 일정",       "Pace holding.",  0, 1),
 ("LEGS",    "Walk", "보행 정상",       "Gait normal.",   0, 1),
 ("FLOURISH","Walk", "앞치마 흔들림",   "Bib swaying.",   0, 1),
]
LIMKO = {"Idle":15, "Walk":9}; LIMEN = {"Idle":21, "Walk":14}
print("  %-9s %-5s %-14s %3s %6s %+8s %-16s %3s %6s %+8s"
      % ("슬롯","상태","한국어","자","R","여유","English","자","R","여유"))
for slot, st, ko, en, fm, gz in G:
    D = DEN[st]
    rk, re_ = reading(ko,MODE), reading(en,MODE)
    sk, se = D-FADEIN-rk-POPIN, D-FADEIN-re_-POPIN
    print("  %-9s %-5s %-14s %3d %6.3f %+8.3f %-16s %3d %6.3f %+8.3f"
          % (slot, st, ko, len(ko), rk, sk, en, len(en), re_, se))
    need(sk > 0 and se > 0, "G 예산 탈락: %s" % slot)
    need(len(ko) <= LIMKO[st], "G 작업상한 초과(KO): %s %d자" % (slot, len(ko)))
    need(len(en) <= LIMEN[st], "G 작업상한 초과(EN): %s %d자" % (slot, len(en)))
tempo = sum(len(x[2]) for x in G)/10.0
formal = sum(x[4] for x in G)/10.0
gaze = sum(x[5] for x in G)/10.0
print("\n  측정: 템포 %.1f자 / 격식 %.2f / 시선(사물) %.0f%%" % (tempo, formal, gaze*100))
POOLS = {"중립":(6.7,0.40,0.30), "A 첫세트":(7.0,0.90,0.30), "B 겨울":(7.0,1.60,0.30),
         "C 탐험가":(5.8,1.00,0.40), "D 왕족":(6.9,2.50,0.40), "E 예술가":(7.1,1.90,0.40),
         "F 모험":(5.8,1.40,0.30)}
print("  분리도 = |Δ템포|/2 + |Δ격식| + 3·|Δ시선|   (기준 0.60)")
worst = (999, "")
for k,(tp,fo,gz) in POOLS.items():
    d = abs(tp-tempo)/2 + abs(fo-formal) + 3*abs(gz-gaze)
    if d < worst[0]: worst = (d, k)
    print("    G vs %-10s = %.2f  %s" % (k, d, "OK" if d >= 0.60 else "★기준미달"))
    need(d >= 0.60, "분리도 미달: G vs %s = %.2f" % (k, d))
print("  최근접 쌍 : G vs %s = %.2f  (R3의 종전 최약 B–E 0.65 대비 %.1f배)" % (worst[1], worst[0], worst[0]/0.65))

H("C11 문자열 충돌 — 기존 풀(중립/A~F, R3 §7-7 + R4 §3-2)과 겹치는가")
EXISTING = set("""음... 여기 좋네 잠깐 쉬는 중 발밑이 단단해 오늘 뭐 하지 산책 중 저쪽으로 가볼까
하나 둘 하나 둘 다리 좀 풀자 다리가 잘 나가네 오... 여기 딱 좋네 잠깐 쉬어가지 발밑 단단하네
다 갖춰 입었다 산책 나왔지 저쪽 가볼까 발걸음이 가볍네 망토가 따라온다 저쪽으로 간다
한 걸음 한 걸음 발끝이 가볍다 망토가 길다 발이 알아서 간다 목도리가 날린다 저쪽으로 가겠다
보폭은 넉넉히 걸음이 당당하다 방울이 흔들린다 저쪽을 향한다 걸음을 센다 판초가 흔들린다
다리가 안 멈춘다 반다나가 날린다""".split("\n"))
EXISTING = set(x.strip() for x in """음...|여기 좋네|잠깐 쉬는 중|발밑이 단단해|오늘 뭐 하지|산책 중|저쪽으로 가볼까|하나 둘 하나 둘|다리 좀 풀자|다리가 잘 나가네|오...|여기 딱 좋네|잠깐 쉬어가지|발밑 단단하네|다 갖춰 입었다|산책 나왔지|저쪽 가볼까|발걸음이 가볍네|망토가 따라온다|저쪽으로 간다|한 걸음 한 걸음|발끝이 가볍다|망토가 길다|발이 알아서 간다|목도리가 날린다|저쪽으로 가겠다|보폭은 넉넉히|걸음이 당당하다|방울이 흔들린다|저쪽을 향한다|걸음을 센다|판초가 흔들린다|다리가 안 멈춘다|반다나가 날린다|Hmm...|Nice spot.|Taking a break.|Solid footing.|What now?|Out walking.|Off that way.|Left, right.|Loosening up.|Good stride.|Oh.|This spot's great.|Break time, then.|Nice firm floor.|All dressed up.|Off on a walk!|Heading over!|One, two, one!|Light-footed!|Cape follows.|Over that way.|Step by step.|Light steps.|A long cape.|That way.|Steady pace.|Legs know it.|Scarf flying.|We proceed.|Onward we go.|Measured pace.|Stately steps.|My bell sways.|I head there.|I count steps.|An even gait.|Moving slowly.|Poncho stirs.|Legs keep on.|Bandana flies.""".split("|"))
clash = [(s, t) for s, st, ko, en, f_, g_ in G for t in (ko, en) if t in EXISTING for s in [s]]
print("  기존 문자열 %d개와 대조 → 충돌 %d건 %s" % (len(EXISTING), len(clash), clash if clash else ""))
need(not clash, "문자열 충돌: %s" % clash)
print("  [양성대조] 일부러 기존 문자열을 넣어 본다 : \"Step by step.\" in EXISTING = %s"
      % ("Step by step." in EXISTING))
need("Step by step." in EXISTING, "충돌 검사 프로브가 죽었다")

# ============================================================================
H("C12 .asset 표기 게이트 — 한글은 \\uXXXX (양성 대조 포함)")
# ============================================================================
def ascii_only(p):
    try:
        open(p, encoding="ascii").read(); return True
    except UnicodeDecodeError:
        return False
files = sorted(glob.glob(os.path.join(ITEMS, "*.asset")))
bad = [os.path.basename(p) for p in files if not ascii_only(p)]
print("  기존 %d개 .asset 전수 : 비ASCII 포함 %d건 %s" % (len(files), len(bad), bad if bad else ""))
need(not bad, "기존 .asset 에 비ASCII")
tmp = "/tmp/_r6_positive_control.asset"
open(tmp, "w", encoding="utf-8").write('  displayName: "정비모"\n')
print("  [양성대조] 한글을 그대로 쓴 파일 : ascii_only = %s  %s"
      % (ascii_only(tmp), "게이트 정상" if not ascii_only(tmp) else "★게이트가 죽었다"))
need(not ascii_only(tmp), "ASCII 게이트 양성대조 실패")
os.remove(tmp)
def esc(s): return "".join(ch if ord(ch) < 128 else "\\u%04X" % ord(ch) for ch in s)
NAMES = [("equip.head.havelock",   "정비모",    "Crew Cap",     "뒤로 늘어진 천이 뒷목을 덮는다."),
         ("equip.eyes.respirator", "방진 고글",  "Dust Goggles", "렌즈가 한 장으로 이어져 있다."),
         ("equip.neck.apronbib",   "앞치마",    "Apron",        "주머니가 하나 달려 있다."),
         ("equip.shoulders.toolbag","연장 가방", "Tool Bag",     "덮개가 위로 접힌다."),
         ("look.hair.napetie",     "낮은 묶음",  "Low Tie",      "목덜미 아래에서 한 번 묶었다."),
         ("look.pet.worklamp",     "등불",      "Lantern",      "옆에서 조용히 따라온다.")]
print("\n  이름 자수 / .asset 표기")
for iid, ko, en, desc in NAMES:
    print('    %-26s %-6s(%d자) %-13s  displayName: "%s"' % (iid, ko, len(ko), en, esc(ko)))
    need(len(ko) <= 7, "이름 %s 가 기존 최장 7자를 넘는다" % ko)
    print('    %-26s %s' % ("", 'description: "%s"' % esc(desc)))
tok_ko = {}
for _, ko, en, _ in NAMES:
    for w in ko.split(): tok_ko[w] = tok_ko.get(w,0)+1
rep = {k:v for k,v in tok_ko.items() if v >= 2}
print("  팩 6종 안에서 2회 이상 반복되는 한국어 토큰 : %s" % (rep if rep else "없음"))
need(not rep, "이름 토큰 반복: %s" % rep)

# ============================================================================
H("결과")
# ============================================================================
if FAIL:
    print("  ★ 실패 %d건" % len(FAIL))
    for f in FAIL: print("    - " + f)
    sys.exit(1)
print("  전 항목 통과.")
