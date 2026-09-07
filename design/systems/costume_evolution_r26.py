#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R26 — 코스튬 진화(PART2) 임계·회계·표시 검산기.
사용자 지시 2026-09-07 «세트별 입은채 누적집중시간에 따른 3단계 시각적진화(0~10h/50h/100h)».

★ 이 저장소는 거짓 통과에 여러 번 당했다 (CLAUDE.md / TEAM.md §4).
  그래서 [0] 교정 단계를 먼저 돌고, 교정이 하나라도 깨지면 그 뒤 숫자를 전부 폐기하고 즉시 종료한다.
★ 상수를 사람이 베끼지 않는다 — 프로덕션 .cs 를 정규식으로 직접 읽는다. 파싱 실패 = 즉시 종료.
"""
import math, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RULES = os.path.join(ROOT, "Assets/_Project/Scripts/Core/CurrencyRules.cs")
DIRECTOR = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/FocusWatchDirector.cs")
CATALOG = os.path.join(ROOT, "Assets/_Project/Scripts/Core/ItemCatalog.cs")
STATS_UI = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/CharacterInfoWindow.Stats.cs")
INFO_UI = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/CharacterInfoWindow.cs")
STORE = os.path.join(ROOT, "Assets/_Project/Scripts/Core/CharacterSaveStore.cs")

FAIL = []
def die(msg):
    print("\n!!! 교정 실패 — 이 아래 숫자는 전부 폐기한다. !!!\n   " + msg); sys.exit(1)
def read(p):
    if not os.path.exists(p): die("파일 없음: %s" % p)
    return open(p, encoding="utf-8").read()
def cint(src, name, p):
    m = re.search(r"const\s+int\s+%s\s*=\s*(-?\d+)\s*;" % re.escape(name), src)
    if not m: die("상수 파싱 실패: %s in %s" % (name, p))
    return int(m.group(1))
def cfloat(src, name, p):
    m = re.search(r"const\s+float\s+%s\s*=\s*(-?[\d.]+)f\s*;" % re.escape(name), src)
    if not m: die("상수 파싱 실패: %s in %s" % (name, p))
    return float(m.group(1))

rules, drv, cat, sui, iui, store = (read(x) for x in (RULES, DIRECTOR, CATALOG, STATS_UI, INFO_UI, STORE))

FOCUS_PM   = cint(rules, "FocusCoinsPerMinute", RULES)
CANCEL_PM  = cint(rules, "FocusCancelCoinsPerMinute", RULES)
BASE_CAP   = cint(rules, "BaseDailyCapCoins", RULES)
POTION     = cint(rules, "PotionBonusCoins", RULES)
MAXPOT     = cint(rules, "MaxPotionsPerDay", RULES)
WINDOW_MIN = cint(rules, "IdleWindowCapMinutes", RULES)
ARCH_AWARD = cint(rules, "ArcheryCoinsPerAward", RULES)
ARCH_LIMIT = cint(rules, "ArcheryDailyAwardLimit", RULES)
P_COMMON   = cint(rules, "CommonPriceCoins", RULES)
P_RARE     = cint(rules, "RarePriceCoins", RULES)
P_EPIC     = cint(rules, "EpicPriceCoins", RULES)
P_LEG      = cint(rules, "LegendaryPriceCoins", RULES)
SEED       = cint(rules, "SeedCoins", RULES)
XP_PM      = cint(rules, "FocusXpPerMinute", RULES)
XP_CAP     = cint(rules, "FocusXpDailyCap", RULES)
IDLE_PM    = FOCUS_PM // 2
HARD_CEIL  = BASE_CAP + POTION * MAXPOT
MIN_SESS   = cfloat(drv, "MinimumSessionSeconds", DIRECTOR)
DEMO_SESS  = cfloat(drv, "DemoSessionSeconds", DIRECTOR)
SAVE_VER   = int(re.search(r"const\s+int\s+CurrentVersion\s*=\s*(\d+)\s*;", store).group(1))

m = re.search(r"AllThemes\(\)\s*=>\s*new\[\]\s*\{([^}]*)\}", cat)
if not m: die("AllThemes() 파싱 실패")
THEME_N = len([t for t in m.group(1).split(",") if t.strip()])

def uiconst(src, name, path):
    m = re.search(r"const\s+float\s+%s\s*=\s*([\d.]+)f" % re.escape(name), src)
    if not m: die("UI 상수 파싱 실패: %s in %s" % (name, path))
    return float(m.group(1))
COL2_W    = uiconst(iui, "Col2Width", INFO_UI)
COL2_PADX = uiconst(iui, "Col2PadX", INFO_UI)
COL2_CW   = COL2_W - COL2_PADX * 2
CARD_PADX = uiconst(sui, "StatCardPadX", STATS_UI)
CARD_PADY = uiconst(sui, "StatCardPadY", STATS_UI)
SET_PANEL = uiconst(sui, "SetPanelHeight", STATS_UI)
SET_PROG  = uiconst(sui, "SetProgressHeight", STATS_UI)
SET_DET   = uiconst(sui, "SetDetailHeight", STATS_UI)
SET_GAP   = uiconst(sui, "SetRowGap", STATS_UI)

print("="*78); print("[0] 교정 — 알려진 값으로 먼저 맞춘다. 하나라도 어긋나면 전부 폐기."); print("="*78)
def check(label, got, want):
    ok = (got == want) if not isinstance(want, float) else abs(got-want) < 1e-9
    print("   %-56s %-16s %s" % (label, got, "OK" if ok else "X 기대 %s" % (want,)))
    if not ok: FAIL.append(label)

# --- 코드값 == 정본 문서값 -----------------------------------------------
check("CurrencyRules.FocusCoinsPerMinute (§18-2)", FOCUS_PM, 24)
check("FocusCancelCoinsPerMinute (§13-3)", CANCEL_PM, 20)
check("취소:완주 = 5:6 항등 (CurrencyRules 문서)", CANCEL_PM*6, FOCUS_PM*5)
check("유휴 = 집중의 정확히 1/2 (§18-2)", IDLE_PM, 12)
check("일일 유휴 기본 상한 (§18-2 사용자확정1)", BASE_CAP, 1500)
check("절대 천장 1500+500x2 (§18-2)", HARD_CEIL, 2500)
check("인정 시간 창 분 (§18-2)", WINDOW_MIN, 480)
check("활쏘기 1회 상금 (§13-3)", ARCH_AWARD, 20)
check("활쏘기 하루 횟수 상한 (U-41 임시)", ARCH_LIMIT, 72)
check("가격 사다리 600/1400/3200/9600 (§21-5)", (P_COMMON,P_RARE,P_EPIC,P_LEG), (600,1400,3200,9600))
check("첫 실행 시드 (U-42)", SEED, 1200)
check("집중 XP 분당 (§15-3)", XP_PM, 6)
check("집중 XP 일일 상한 (§15-4-3)", XP_CAP, 1080)
check("최소 세션 초 (FocusWatchDirector)", MIN_SESS, 60.0)
check("데모 세션 초 (FocusWatchDirector)", DEMO_SESS, 90.0)
check("실재 테마 수 (ItemCatalog.AllThemes)", THEME_N, 6)
check("세이브 스키마 현재 버전 (R26 예고 v11->v12가 착지함)", SAVE_VER, 12)
check("정보창 Col2 콘텐츠 폭", COL2_CW, 256.0)
check("세트 패널 높이 = 11+16+6+14+9 (Stats.cs 문서 검산)",
      CARD_PADY + SET_PROG + SET_GAP + SET_DET + 9.0, SET_PANEL)

# --- 정본이 이미 적어 둔 파생 결론을 재현하는가 ---------------------------
check("§13-3 '25분 = 600동전 = 일반 1개'", FOCUS_PM*25, P_COMMON)
check("§12-2 '전설 = 집중 25분 정확히 16.0회'", P_LEG/(FOCUS_PM*25), 16.0)
check("§18-2 '무료1 사용 -> 캡도달 166.7분'", round((BASE_CAP+POTION)/IDLE_PM,1), 166.7)
check("WindowToCeilingRatio = 2.304", round(WINDOW_MIN*IDLE_PM/HARD_CEIL,3), 2.304)

# --- ★ 원형표 역산 (§18-3 능동 수입에서 「집중 분/일」을 되찾는다) --------
#     능동 = 집중분×24 + 활쏘기횟수×20 이라는 구조가 맞는지 원형 4개로 대조.
ARCH = {"A":3.4, "B":4, "C":8, "D":60}          # §13-3 계보(R1 값)의 활쏘기 횟수
FOCUS_MIN = {"A":0, "A2":0, "B":50, "C":100, "D":200}
ACTIVE_DOC = {"A":68, "B":1280, "C":2560, "D":6000}
for k in ("A","B","C","D"):
    check("§18-3 원형 %s 능동 = 집중%d분x24 + 활쏘기%sx20" % (k, FOCUS_MIN[k], ARCH[k]),
          int(round(FOCUS_MIN[k]*FOCUS_PM + ARCH[k]*ARCH_AWARD)), ACTIVE_DOC[k])
IDLE_DOC = 2000                                  # §18-3 시나리오②(무료 회복제 1개)
DAY_B = ACTIVE_DOC["B"] + IDLE_DOC
check("§18-3 원형 B 일일 합계 = 3,280", DAY_B, 3280)
check("§18-3 원형 D 일일 합계 = 8,000", ACTIVE_DOC["D"]+IDLE_DOC, 8000)
CATALOG_TOTAL = 115200                           # §22-1 안 A
check("§18-3 '원형 B 코인완주 35.1일'", round(CATALOG_TOTAL/DAY_B,1), 35.1)
check("§13-3 '전설 = 기준유저 7.50일'(유휴 이전 1,280)", round(P_LEG/ACTIVE_DOC["B"],2), 7.50)

# --- 음성 대조: 같은 파싱 방법이 없는 상수에서는 반드시 실패하는가 --------
neg = re.search(r"const\s+int\s+ThisConstantDoesNotExist\s*=\s*(-?\d+)\s*;", rules)
check("[음성대조] 존재하지 않는 상수는 파싱되지 않는다", neg is None, True)
pos = re.search(r"const\s+int\s+FocusCoinsPerMinute\s*=\s*(-?\d+)\s*;", rules)
check("[양성대조] 존재하는 상수는 같은 방법으로 잡힌다", pos is not None, True)

if FAIL:
    die("교정 %d건 실패: %s" % (len(FAIL), ", ".join(FAIL)))
print("\n   교정 %d항목 전부 통과. 아래 숫자를 신뢰한다.\n" % 0 if False else "\n   교정 전부 통과. 아래 숫자를 신뢰한다.\n")

# ============================================================================
print("="*78); print("[1] U-1 — 사용자 제시 임계 10h / 50h / 100h 의 실제 소요 일수"); print("="*78)
H = 60  # 분/시
THRESH = [("10h", 10*H), ("50h", 50*H), ("100h", 100*H)]
print("   원형별 「집중 분/일」은 §18-3 능동 수입에서 역산한 값이다(위 교정에서 4/4 일치).")
print("   %-30s %8s %10s %10s %10s" % ("원형", "집중분/일", "10h", "50h", "100h"))
for k in ("A","A2","B","C","D"):
    fm = FOCUS_MIN[k]
    cells = []
    for _, mins in THRESH:
        cells.append("도달불가" if fm == 0 else "%.1f일" % (mins/fm))
    print("   %-30s %8d %10s %10s %10s" % (k, fm, *cells))
print()
print("   ★ 세트별 분리 회계 — 코스튬 N종을 고루 돌려 입으면 각 세트의 소요는 N배가 된다.")
for n in (1,2,4,8):
    print("      원형 B가 %d종을 균등 순환: 100h 마스터 = %.0f일 (= %.1f년)"
          % (n, 6000/50*n, 6000/50*n/365))

print()
print("="*78); print("[2] U-1 — 그 시점의 재화 상태 (진화가 「마지막 목표」인가)"); print("="*78)
for k in ("B","C","D"):
    fm = FOCUS_MIN[k]; day = ACTIVE_DOC[k] + IDLE_DOC
    d100 = 6000/fm
    total = day*d100 + SEED
    focus_only = 6000*FOCUS_PM
    print("   원형 %s: 100h 도달 = %.1f일" % (k, d100))
    print("      그동안 총수입          = %,d동전".replace(",d","d") % 0 if False else
          "      그동안 총수입          = {:,}동전".format(int(total)))
    print("      집중 채널만            = {:,}동전  (= 전설 {:.1f}개 · 카탈로그 {:.2f}벌)"
          .format(focus_only, focus_only/P_LEG, focus_only/CATALOG_TOTAL))
    print("      카탈로그 완주(동전만)  = {:.1f}일   → 100h보다 {:.1f}일 **앞선다**"
          .format(CATALOG_TOTAL/day, d100 - CATALOG_TOTAL/day))
    print("      총수입 / 카탈로그      = {:.2f}배   (100h 시점에 살 것 남음: {})"
          .format(total/CATALOG_TOTAL, "있음" if total < CATALOG_TOTAL else "없음"))
    print("      전설(9,600) 대비 총수입= {:.1f}배".format(total/P_LEG))
    print()
print("   레벨 게이트까지 본 재화곡선의 끝 = 90일차(§21-9 '90일차에 완전히 멈춘다', 요구레벨 최대 30).")
print("   ⇒ 원형 B 기준 순서:  동전완주 35.1일  <  레벨천장 90일  <  100h 마스터 120일")

print()
print("="*78); print("[3] 어뷰징 — 1분 세션 반복 vs 세션 방치"); print("="*78)
print("   (가) 1분 세션 반복: 누적은 「시간」이므로 격자 이득이 구조적으로 0이다.")
print("        1분세션 %d회 = %d분 = 8시간.  60분세션 8회 = 480분 = 8시간.  동일." % (480,480))
print("        ※ §22-14가 동전에 대해 내린 판정(방어 불필요)과 같은 이유·같은 결론.")
print("   (나) 세션 방치(60분 상한 체인): 하루 %d분이 물리 상한 → 100h = %.2f일" % (24*60, 6000/(24*60)))
for cap in (120, 180, 240, 300, 360, 480):
    print("        일일 누적 소프트캡 %3d분 → 100h 최소 %6.1f일 · 원형D(200분)와 여유 %5.2f배 · 원형B와 격차 %.1f배"
          % (cap, 6000/cap, cap/FOCUS_MIN["D"], (6000/FOCUS_MIN["B"])/(6000/cap)))
print("   ★ 활쏘기 선례: ArcheryDailyAwardLimit 은 정상 상한에 **1.20배** 여유를 얹어 정했다.")
print("      원형 D(집중 200분/일) x 1.20 = %.0f분" % (FOCUS_MIN["D"]*1.20))

print()
print("="*78); print("[4] 표시 — 퍼센트 소수 자리 / 막대 1pt / 경계 표기"); print("="*78)
BAR = COL2_CW - CARD_PADX*2
print("   막대 폭 = Col2ContentWidth(%.0f) - StatCardPadX(%.0f)x2 = %.0f pt" % (COL2_CW, CARD_PADX, BAR))
SEG4 = [("0->10h", 600), ("10h->50h", 2400), ("50h->100h", 3000)]
SEG3 = [("0->50h", 3000), ("50h->100h", 3000)]
WHOLE = [("0->100h 통짜", 6000)]
for name, segs in (("S4 4상태(경계 10/50/100h)", SEG4), ("S3' 3상태(경계 50/100h)", SEG3), ("통짜 100h 대비", WHOLE)):
    print("\n   [%s]" % name)
    for lbl, mins in segs:
        pt_per_min = BAR/mins
        print("      %-12s 구간 %5d분 | 25분세션 1회 = %5.2f pt (%s) · %5.3f %%p → 소수1자리 %4.0f틱"
              % (lbl, mins, 25*pt_per_min, "보임" if 25*pt_per_min >= 1.0 else "★안보임",
                 25/mins*100, 25/mins*100*10))
print()
print("   ★ 판정: 25분 완주 1회가 막대에서 1pt를 넘겨야 '했다'가 보인다(UX_WIDGETS R3-2-3 선례).")
print("   반올림 방향 검산 — 100h 임계 직전:")
for mins in (5990, 5997, 5999, 6000, 6001):
    pct = mins/6000*100
    fl = math.floor(pct*10)/10; rd = round(pct*10)/10
    print("      누적 %5d분 = %8.4f%% | floor1 %5.1f%% | round1 %5.1f%% | 실제 마스터 %s"
          % (mins, pct, fl, rd, "YES" if mins >= 6000 else "no"))
print("   ⇒ round는 %d분(=%d분 미달)부터 100.0%%로 보이는데 마스터가 아니다 → 거짓 표시 창 %d분."
      % (5997, 3, 3))
print("   ⇒ floor는 100.0% 표시와 마스터 도달이 정확히 동치다(창 0분).")

print()
print("="*78); print("[5] 저장 — 필드·정밀도·빈도"); print("="*78)
print("   ★ 스키마 v%d — R26이 예고한 v11->v12가 **착지 확인됨**(코스튬 필드 포함)." % SAVE_VER)
print("   테마 수: 현재 %d + 신규 코호트 2(광부·대마법사) = %d" % (THEME_N, THEME_N+2))
print("   가변 병렬 배열 2개(키 string[] + 분 int[]) + 일일 누적 int 1개 = 필드 3개")
print("   최악 크기: %d테마 x (키 ~8B + int 4B) = %dB (JSON 직렬화라 실제는 수백 B)"
      % (THEME_N+2, (THEME_N+2)*12))
print("\n   ★ float32 초 누적의 함정 (CharacterStatsModel.TotalCompanionSeconds 형태를 베끼면 안 되는 이유):")
import struct
def f32(x): return struct.unpack('f', struct.pack('f', x))[0]
for tot_h in (10, 50, 100):
    v = f32(tot_h*3600.0)
    ulp = f32(struct.unpack('f', struct.pack('I', struct.unpack('I', struct.pack('f', v))[0] + 1))[0]) - v
    dt = 1/60
    after = f32(v + dt)
    print("      누적 %3dh = %9.1f초 | float32 ULP = %.5f초 | 60fps dt(%.5f) 1회 더하면 실제 증가 %.5f초 (%.1f배)"
          % (tot_h, v, ulp, dt, after-v, (after-v)/dt if dt else 0))
print("   ⇒ float32 프레임 누적은 10h/50h에서 -6.25%%(적게), 100h에서 +87.5%%(많이) 어긋난다 — 방향이 둘 다 틀린다.")
print("      정수 「분」으로 세션 종료 시 1회만 더하면 이 축이 통째로 사라진다(6,000은 int에서 정확).")
print("\n   추가 저장 빈도: 세션 종료 시 1회 — 그 자리는 코인·XP가 **이미** IsDirty를 세운다.")
print("      ⇒ 신규 유발 저장 = +0.00회/일. 주기 저장은 progressionAutoSaveIntervalSeconds=60초 그대로.")

print()
print("="*78); print("[6] P-12 파생 비용 — 확인만 (상품 판단 아님)"); print("="*78)
packs_free, packs_paid = 1, 7
print("   팩 구성(전략 §42): 무료 %d(office) + 유료 %d(cyber·neon·mil·sport·ink·광부·대마법사) = %d"
      % (packs_free, packs_paid, packs_free+packs_paid))
print("   PART2 사용자 예시가 이미 덮는 팩: office·cyber·광부·대마법사 = 4")
print("   P-12가 **추가로** 강제하는 팩: neon·mil·sport·ink = 4")
print("   소환 오브젝트 총 개수 = %d (전략문서의 '기존 5팩'은 cyber를 미계획으로 세어 5)" % (packs_free+packs_paid))
for name, states in (("S4(4상태)", 4), ("S3'(3상태)", 3)):
    print("   조형 상태 총량 %-10s = %d팩 x %d상태 = %d" % (name, packs_free+packs_paid, states, (packs_free+packs_paid)*states))
print("   ⇒ S4 채택의 조형 대가 = %d - %d = **%d 상태**" % (8*4, 8*3, 8))

print()
print("="*78); print("[7] 진입 비용 — 「입은 채」를 성립시키는 데 드는 동전 (세트 4/4 완성가)"); print("="*78)
# ItemCatalog 테마 배정표 + 가격 사다리(rank 파생)에서 직접 읽는다.
# rank -> 가격: 슬롯 내 requiredLevel 오름차순 순위. 아래 주석의 등급은 카탈로그 주석 그대로.
SETS = {
    "mil   (밀리터리·1일차 무상 4종)": [("천모자",0),("선글라스",0),("나비넥타이",0),("짧은망토",0)],
    "office(오피스 워커·무료 팩)":     [("중절모",P_RARE),("동그란안경",P_COMMON),("줄무늬타이",P_COMMON),("배낭",P_RARE)],
    "cyber (사이버 아포칼립스)":        [("왕관",P_RARE),("외알안경",P_RARE),("펜던트",P_EPIC),("긴망토",P_COMMON)],
}
DAY_INCOME = {"A":2068, "A2":1508, "B":3280, "C":4560, "D":8000}   # §18-3 시나리오② 합계
for name, items in SETS.items():
    tot = sum(p for _, p in items)
    line = "   %-32s 합계 %6d동전" % (name, tot)
    if tot == 0:
        line += "   (요구레벨 Lv.1 — 1일차 무상, U-5 사용자확정)"
    else:
        days = []
        for k in ("A2","B"):
            need = max(0, tot - SEED)
            days.append("%s %.1f일" % (k, math.ceil(need/DAY_INCOME[k]*10)/10))
        line += "   시드 %d 차감 후: %s" % (SEED, " / ".join(days))
    print(line)
print("   ※ 구매에 레벨 관문이 없다(CurrencyModel.TryPurchaseItem 실측: 잔액·중복만 본다).")
print("      ⇒ 요구레벨 Lv.22(배낭)·Lv.21(펜던트)는 동전으로 앞당겨진다(§14-1 「앞당기기」).")
print("   ★ P-11의 최악 사례는 cyber가 아니라 **mil**이다 — 1일차 무상 4종이 그대로 mil 4/4다.")
print("      연출 게이트를 「세트 완성」에 걸면 mil 연출이 0원·0일차에 샌다.")

print()
print("="*78); print("[8] [DS-구멍 2] — 25분 내 3구간 세 상수 확인 (game-architect 8-2 모형)"); print("="*78)
MIN_EDGE, MAX_EDGE = 60.0, 300.0
def edge(D, f):  # game-architect 8-2: min(f*D, clamp(0.2D, 60, 300))
    return min(f*D, min(max(0.2*D, MIN_EDGE), MAX_EDGE))
# --- 교정: game-architect 표(f=0.4)를 그대로 재현하는가 ---------------------
GA = {60:24.0, 120:48.0, 150:60.0, 180:60.0, 300:60.0, 900:180.0, 1500:300.0, 3000:300.0, 3600:300.0}
bad = [(D, edge(D,0.4), v) for D, v in GA.items() if abs(edge(D,0.4)-v) > 1e-9]
print("   [교정] game-architect 8-2 표 %d행 재현: %s" % (len(GA), "전부 일치" if not bad else "X %s" % bad))
if bad: die("8-2 모형 재현 실패 — 아래 비교 무효")
print("   ⇒ 모형은 EdgeSeconds(D) = min(f*D, clamp(0.2D, 60, 300)) 이고 f=0.4가 그 표를 낸다.\n")
print("   %-7s | %-22s | %-22s" % ("세션 D", "f = 0.40 (제안값)", "f = 1/3 (내 대안)"))
print("   %-7s | %-22s | %-22s" % ("", "적응/몰입/한계", "적응/몰입/한계"))
for D in (60, 90, 120, 150, 180, 300, 900, 1500, 3000, 3600):
    row = []
    for f in (0.4, 1/3):
        e = edge(D, f); row.append("%5.1f /%6.1f /%5.1f" % (e, D-2*e, e))
    print("   %5ds  | %-22s | %-22s" % (D, row[0], row[1]))
print()
for f, nm in ((0.4,"0.40"), (1/3,"1/3 ")):
    cross = MIN_EDGE/f
    print("   f=%s → MinEdgeSeconds가 물기 시작하는 세션 길이 D* = 60/f = %6.1f초" % (nm, cross))
print("   ★ 코스튬 최소 세션 길이(몰입기 >= 60초) = %.0f초 — f와 무관하게 같다(D=180에서 두 f가 같은 60을 낸다)."
      % 180)
print("   ⇒ f = 1/3 일 때만  D* == 코스튬 최소 세션 길이 == 180초  로 **두 상수가 서로를 유도한다.**")
print("   ⇒ 그리고 D <= 180 구간이 **정확한 3등분**이 되어 설명이 한 문장으로 닫힌다.")
print("   대가 확인: f를 0.4 -> 1/3 로 내리면 짧은 세션의 몰입기가 늘어난다(코스튬이 더 오래 보인다):")
for D in (60, 90, 120, 150):
    print("      D=%3ds  몰입기 %5.1f초 -> %5.1f초 (+%.0f%%)"
          % (D, D-2*edge(D,0.4), D-2*edge(D,1/3), (D-2*edge(D,1/3))/(D-2*edge(D,0.4))*100-100))
print("   사용자 제시 25분 예시는 두 f에서 **동일**하다: 적응 %.0f / 몰입 %.0f / 한계 %.0f초 (= 0~5 / 5~20 / 20~25분)"
      % (edge(1500,1/3), 1500-2*edge(1500,1/3), edge(1500,1/3)))

print()
print("="*78); print("[9] U-1 원문 전제 검증 — 「유휴 수급만으로 144,000」"); print("="*78)
print("   원문: 100h 누적이면 **유휴 수급만으로** 144,000동전이 쌓인다 (DESIGN_COSTUME_FOCUS_ARCHITECTURE :668)")
print("   (a) 144,000 = 6,000분 x %d  ← 이건 **집중 완주 요율**이다 (유휴는 %d)" % (FOCUS_PM, IDLE_PM))
print("       유휴 요율로 같은 6,000분을 세면 %d동전 = 실제의 %.0f%%" % (6000*IDLE_PM, 6000*IDLE_PM/144000*100))
print("   (b) 그런데 그것도 못 번다 — 집중 세션 중 유휴 수급은 **정확히 0**이다")
print("       (CharacterProgressionDirector:374  isIdleEarning = !_focusWatch.IsSessionActive)")
print("   (c) 게다가 유휴에는 일일 상한이 있다(기본 %d / 최대 %d) — 요율만으로는 어차피 못 곱한다" % (BASE_CAP, HARD_CEIL))
print("   ⇒ **숫자 144,000은 참이고 재현된다. 다만 그것은 집중 채널의 수입이지 유휴 수입이 아니다.**")

print()
print("="*78); print("[10] ★ 정정 — §7-3 표는 「통짜」로 계산돼 §7-2 문장과 어긋났다 (리더 지적)"); print("="*78)
STAGES = [0, 600, 3000, 6000]          # §3-3 S4 경계(분). 마지막이 마스터 임계.
def stage_of(m):
    s = 0
    for i, b in enumerate(STAGES):
        if m >= b: s = i
    return min(s, len(STAGES)-1)
def seg(m):                            # (start, end) — 마스터면 None
    st = stage_of(m)
    if st >= len(STAGES)-1: return None
    return STAGES[st], STAGES[st+1]
def tenths_floor(m):                   # 프로덕션 PercentTenthsToNextBoundary 와 같은 정수식
    sg = seg(m)
    if sg is None: return -1
    start, end = sg; span = end - start; done = m - start
    if done <= 0: return 0
    t = done * 1000 // span
    return 999 if t > 999 else t
def tenths_round(m):                   # 기각된 대안(반올림) — 클램프 없이
    sg = seg(m)
    if sg is None: return -1
    start, end = sg; span = end - start; done = m - start
    return int(round(done * 1000 / span))

# --- 교정: 리더가 보고한 두 값을 재현하는가 --------------------------------
lead_whole = round(5990/6000*100, 4)
lead_seg   = tenths_floor(5990)/10
print("   [교정] 리더 보고값 재현")
print("      5,990분 통짜 = %.4f%%  (리더: 99.8333%%)   %s" % (lead_whole, "OK" if abs(lead_whole-99.8333)<1e-4 else "X"))
print("      5,990분 구간 = %.1f%%     (리더: 99.6%%)      %s" % (lead_seg, "OK" if abs(lead_seg-99.6)<1e-9 else "X"))
if abs(lead_whole-99.8333)>1e-4 or abs(lead_seg-99.6)>1e-9: die("리더 보고값 재현 실패 — 아래 정정표 무효")

print("\n   [정정된 §7-3 표] 기준 = **구간** [50h,100h) = 3,000분 span")
print("   %8s %10s %10s %10s %8s" % ("누적(분)", "구간 진행", "floor 1자리", "round 1자리", "마스터?"))
for m in (5990, 5997, 5998, 5999, 6000, 6001):
    sg = seg(m)
    if sg is None:
        print("   %8d %10s %10s %10s %8s" % (m, "—", "그리지 않음", "그리지 않음", "YES"))
        continue
    start, end = sg
    print("   %8d %6d/%-4d %9.1f%% %9.1f%% %8s"
          % (m, m-start, end-start, tenths_floor(m)/10, tenths_round(m)/10, "no"))

# round 의 거짓 창을 구간 기준으로 전수 측정
false_round = [m for m in range(STAGES[-2], STAGES[-1]) if tenths_round(m) >= 1000]
print("\n   round(클램프 없음)가 「100.0%%인데 마스터 아님」을 내는 분: %s → **%d분**"
      % (false_round if false_round else "없음", len(false_round)))
print("   ※ 이 값이 구 문서의 «3분»을 대체한다 — 3분은 **통짜 기준**이었다.")

# --- floor 는 애초에 1000에 못 닿는가? 전수 확인(0..5999) -------------------
mx = max(tenths_floor(m) for m in range(0, STAGES[-1]))
hit = [m for m in range(0, STAGES[-1]) if tenths_floor(m) >= 1000]
print("\n   [전수 0..5,999분] floor 천분율 최댓값 = %d · 1000 도달 = %d건" % (mx, len(hit)))
print("   ⇒ **내림만으로도 1000에 구조적으로 못 닿는다** ⇒ 프로덕션의 `if (tenths > 999)` 클램프는")
print("      오늘 경로에서는 **도달 불가(unreachable)**이고, 그 값은 «미래 변경 방어»에 있다:")
print("      같은 자리에 round 를 넣으면 %d건이 1000을 내는데, 클램프가 그것을 999로 잡는다." % len(false_round))
seg_max = {}
for m in range(0, STAGES[-1]):
    seg_max.setdefault(stage_of(m), 0)
    seg_max[stage_of(m)] = max(seg_max[stage_of(m)], tenths_floor(m))
for st, v in sorted(seg_max.items()):
    print("      구간 %d [%d,%d) 최댓값 %d (= %.1f%%)" % (st, STAGES[st], STAGES[st+1], v, v/10))

print()
print("="*78); print("[11] 소수 자리 재논증 — 「2.4회」도 통짜 잔재였다"); print("="*78)
for lbl, span in (("구간0 [0,10h)",600), ("구간1 [10h,50h)",2400), ("구간2 [50h,100h)",3000), ("(기각) 통짜 100h",6000)):
    per = 25/span*100
    print("   %-18s span %4d분 | 25분세션 1회 = %6.3f%%p | 1%%p 움직이는 데 %4.1f회 | 소수1자리 %3.0f틱"
          % (lbl, span, per, 1/per, per*10))
print("\n   ★ 「0자리면 N회 세션마다 1%p」의 정답: 구간2에서 **1.2회**(초판의 2.4회는 통짜 값).")
print("   그러면 「0자리로도 충분한가?」를 다시 재야 한다 — 전수로 센다.")
for lbl, span in (("구간2 [50h,100h)",3000), ("구간1 [10h,50h)",2400), ("구간0 [0,10h)",600)):
    per = 25/span*100
    n = int(span//25)                      # 그 구간을 25분 세션으로 채우는 횟수
    stall0 = sum(1 for k in range(1, n+1) if int(per*k) == int(per*(k-1)))
    stall1 = sum(1 for k in range(1, n+1) if int(per*k*10) == int(per*(k-1)*10))
    print("   %-18s 완주 %3d회 중 | 소수0자리로 **숫자가 안 바뀌는** 세션 %3d회(%4.1f%%) | 소수1자리 %d회(%.1f%%)"
          % (lbl, n, stall0, stall0/n*100, stall1, stall1/n*100))
DAYB = 50
print("\n   원형 B 하루(집중 %d분) 기준 구간2에서의 하루 진행 = %.3f%%p" % (DAYB, DAYB/3000*100))
per_day = DAYB/3000*100
stall_day = sum(1 for k in range(1,61) if int(per_day*k) == int(per_day*(k-1)))
print("   ⇒ **하루 단위로는 0자리도 안 멈춘다**(60일 중 %d일) — 초판의 「하루에 안 움직이는 날이 생긴다」는" % stall_day)
print("      구간 기준에서 **성립하지 않는다. 논거를 「세션 단위」로 바꾼다.**")
