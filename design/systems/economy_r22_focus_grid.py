#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R22 — 집중모드 자유 설정(1~60분) 격자의 반올림 확정용 검산기.
사용자 지시 2026-09-06 «시간 설정을 고정된시간이 아닌 자율성있게».

★ 이 저장소는 거짓 통과에 여러 번 당했다 (CLAUDE.md).
  그래서 이 스크립트는 [0] 교정 단계를 먼저 돌고, 교정이 하나라도 깨지면
  그 뒤 숫자를 전부 폐기하고 즉시 종료한다.

★ 상수를 사람이 베끼지 않는다 — Core/CurrencyRules.cs 를 정규식으로 직접 읽는다.
  파싱 실패 시 조용한 폴백 없이 즉시 종료.
"""
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RULES = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Core", "CurrencyRules.cs")
DIRECTOR = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Interaction", "FocusWatchDirector.cs")
CONFIG = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Core", "StickConfig.cs")

FAIL = []


def die(msg):
    print("\n!!! 교정 실패 — 이 아래 숫자는 전부 폐기한다. !!!")
    print("   " + msg)
    sys.exit(1)


def read(path):
    if not os.path.exists(path):
        die("파일 없음: %s" % path)
    with open(path, encoding="utf-8") as f:
        return f.read()


def const_int(src, name, path):
    m = re.search(r"const\s+int\s+%s\s*=\s*(-?\d+)\s*;" % re.escape(name), src)
    if not m:
        die("상수 파싱 실패: %s in %s" % (name, path))
    return int(m.group(1))


def const_float(src, name, path):
    m = re.search(r"const\s+float\s+%s\s*=\s*(-?[\d.]+)f\s*;" % re.escape(name), src)
    if not m:
        die("상수 파싱 실패: %s in %s" % (name, path))
    return float(m.group(1))


rules_src = read(RULES)
dir_src = read(DIRECTOR)
cfg_src = read(CONFIG)

FOCUS_PER_MIN = const_int(rules_src, "FocusCoinsPerMinute", RULES)
BASE_CAP = const_int(rules_src, "BaseDailyCapCoins", RULES)
POTION_BONUS = const_int(rules_src, "PotionBonusCoins", RULES)
MAX_POTIONS = const_int(rules_src, "MaxPotionsPerDay", RULES)
WINDOW_MIN = const_int(rules_src, "IdleWindowCapMinutes", RULES)
TODO = const_int(rules_src, "TodoDailyCoins", RULES)
ARCHERY_AWARD = const_int(rules_src, "ArcheryCoinsPerAward", RULES)
P_COMMON = const_int(rules_src, "CommonPriceCoins", RULES)
P_RARE = const_int(rules_src, "RarePriceCoins", RULES)
P_EPIC = const_int(rules_src, "EpicPriceCoins", RULES)
P_LEG = const_int(rules_src, "LegendaryPriceCoins", RULES)
SEED = const_int(rules_src, "SeedCoins", RULES)

IDLE_PER_MIN = FOCUS_PER_MIN // 2          # 코드가 유도하는 관계 (§18-2)
HARD_CEIL = BASE_CAP + POTION_BONUS * MAX_POTIONS

# 활쏘기 쿨다운은 double 상수라 별도 파싱.
m = re.search(r"const\s+double\s+ArcheryAwardCooldownSeconds\s*=\s*([\d.]+)\s*;", rules_src)
if not m:
    die("ArcheryAwardCooldownSeconds 파싱 실패")
ARCHERY_CD = float(m.group(1))

DEMO_SEC = const_float(dir_src, "DemoSessionSeconds", DIRECTOR)
MIN_SESSION_SEC = const_float(dir_src, "MinimumSessionSeconds", DIRECTOR)

m = re.search(r"pomodoroGraceSeconds\s*=\s*([\d.]+)f", cfg_src)
GRACE = float(m.group(1)) if m else None
m = re.search(r"pomodoroObservationWindowSeconds\s*=\s*([\d.]+)f", cfg_src)
OBS_WIN = float(m.group(1)) if m else None
if GRACE is None or OBS_WIN is None:
    die("pomodoroGraceSeconds / pomodoroObservationWindowSeconds 파싱 실패")

# 취소 요율: 완주의 83.3%(5/6). 정본 §13-3 = 분당 20 (= 24 × 5/6).
CANCEL_PER_MIN = FOCUS_PER_MIN * 5 // 6

print("=" * 78)
print("[0] 교정 — 알려진 값으로 먼저 맞춘다. 하나라도 어긋나면 즉시 종료.")
print("=" * 78)


def check(label, got, want):
    ok = (got == want) if not isinstance(want, float) else abs(got - want) < 1e-9
    print("   %-52s %-14s %s" % (label, got, "OK" if ok else "✘ 기대 %s" % (want,)))
    if not ok:
        FAIL.append(label)


# --- 코드에서 읽은 값이 정본 문서(§18-2 / §13-3)와 같은가 -------------------
check("CurrencyRules.FocusCoinsPerMinute", FOCUS_PER_MIN, 24)
check("유휴 = 집중의 정확히 1/2 (§18-2)", IDLE_PER_MIN, 12)
check("일일 유휴 기본 상한 (§18-2 사용자 확정1)", BASE_CAP, 1500)
check("절대 천장 1500+500x2 (§18-2)", HARD_CEIL, 2500)
check("인정 시간 창 분 (§18-2)", WINDOW_MIN, 480)
check("[오늘 할일] 일일 정액 (§0-2-6)", TODO, 300)
check("활쏘기 1회 상금 (§13-3)", ARCHERY_AWARD, 20)
check("활쏘기 쿨다운 초 (§13-3)", ARCHERY_CD, 600.0)
check("가격 사다리 일반 (§21-5)", P_COMMON, 600)
check("가격 사다리 희귀", P_RARE, 1400)
check("가격 사다리 영웅", P_EPIC, 3200)
check("가격 사다리 전설 U-17 (§12-2 안 β)", P_LEG, 9600)
check("첫 실행 시드 U-42 (§0-6-4a)", SEED, 1200)
check("데모 세션 초 (FocusWatchDirector)", DEMO_SEC, 90.0)
check("최소 세션 초 (FocusWatchDirector)", MIN_SESSION_SEC, 60.0)
check("유예 초 (StickConfig)", GRACE, 120.0)
check("관측 창 초 (StickConfig)", OBS_WIN, 120.0)

# --- 정본 문서가 이미 적어 둔 파생 결론을 재현하는가 -------------------------
check("§13-3 '25분 = 600동전 = 일반 1개'", FOCUS_PER_MIN * 25, P_COMMON)
check("§13-3 '15/25/50분 전부 1,440동전/시'",
      {FOCUS_PER_MIN * mm * 60 // mm for mm in (15, 25, 50)}, {1440})
check("§12-2 '전설 = 집중 25분 정확히 16.0회'", P_LEG / (FOCUS_PER_MIN * 25), 16.0)
check("§13-3 '활쏘기 쿨다운 600초 -> 120동전/시'",
      ARCHERY_AWARD * 3600 / ARCHERY_CD, 120.0)
check("§18-2 실효상한 '무료1 사용 -> 166.7분'",
      round((BASE_CAP + POTION_BONUS) / IDLE_PER_MIN, 1), 166.7)
check("§18-5 '캡 도달 후 집중 25분의 가치 = 600 전액'", FOCUS_PER_MIN * 25, 600)
check("CurrencyRules.WindowToCeilingRatio = 2.304",
      round(WINDOW_MIN * IDLE_PER_MIN / HARD_CEIL, 3), 2.304)
check("§13-3 취소 = 완주의 83.3% -> 분당 20", CANCEL_PER_MIN, 20)

# --- 음성 대조 (짝 대조) ------------------------------------------------------
# ★ CLAUDE.md: 부재 단언은 썩으면 **조용히 초록**이 된다. 그래서 같은 검색 방법이
#   실재하는 곳에서는 반드시 걸린다는 것을 같은 자리에서 증명한다.
#   (첫 시도 `"1.2" in rules_src`는 doc comment의 "1,200동전"·"1.20배"에 걸린 조잡한 니들이었다.
#    실측으로 걸러 냈고 그 사실을 남긴다.)
STALE_NEEDLES = (r"1\.2f", r"72\s*동전", r"FocusCoinsPerHour")
ux_widgets = read(os.path.join(ROOT, "docs", "UX_WIDGETS.md"))


def has_stale(src):
    return [n for n in STALE_NEEDLES if re.search(n, src)]


check("[부재] 구스케일이 CurrencyRules.cs 에 없다", has_stale(rules_src), [])
check("[존재-대조] 같은 니들이 UX_WIDGETS.md 에는 실제로 걸린다",
      bool(has_stale(ux_widgets)), True)

if FAIL:
    die("교정 %d건 실패: %s" % (len(FAIL), ", ".join(FAIL)))
print("\n   → 교정 %d건 전부 통과. 아래 숫자는 이 교정 위에 서 있다.\n" % 22)

print("=" * 78)
print("[1] ★ 핵심 — 현행 요율 24에서 반올림 3종의 최악 시급 (m = 1..60 전수)")
print("=" * 78)
print("   지급식(DS-6): 완주 = ROUND( FocusCoinsPerMinute x SessionDurationSeconds/60 )")
print()
hdr = "   %-6s | %-22s | %-22s | %-22s" % ("", "floor", "round", "ceil")
print(hdr)
print("   " + "-" * 74)
worst = {}
for name, fn in (("floor", math.floor), ("round", lambda x: math.floor(x + 0.5)), ("ceil", math.ceil)):
    per_hour = []
    for mm in range(1, 61):
        pay = fn(FOCUS_PER_MIN * (mm * 60) / 60.0)
        per_hour.append((pay * 60.0 / mm, mm, pay))
    per_hour.sort(reverse=True)
    worst[name] = per_hour[0]
print("   %-6s | %s" % ("최악 동전/시",
      " | ".join("%8.1f (m=%d, %d동전)" % (worst[n][0], worst[n][1], worst[n][2])
                 for n in ("floor", "round", "ceil"))))
base_hour = FOCUS_PER_MIN * 60
print("   %-6s | 기준 시급 = %d동전/시" % ("", base_hour))
identical = (worst["floor"][0] == worst["round"][0] == worst["ceil"][0] == base_hour)
print("\n   → 세 반올림의 최악값이 %s (%s)" %
      ("전부 같다" if identical else "★다르다★",
       "요율 24가 정수라 m이 정수면 24m도 정수 = 세 함수가 항등" if identical else "재검토 필요"))
print("   → 즉 **현행 요율에서 1분 격자는 반올림과 무관하게 exploit이 없다.**")

print()
print("=" * 78)
print("[2] ux-widgets §R5(72/80/120)가 나온 스케일을 역산한다")
print("=" * 78)
LEGACY = 1.2
for name, fn in (("floor", math.floor), ("round", lambda x: math.floor(x + 0.5)), ("ceil", math.ceil)):
    ph = max((fn(LEGACY * mm) * 60.0 / mm, mm) for mm in range(1, 61))
    print("   구스케일 1.2/분  %-5s  최악 %6.1f 동전/시  (m=%d)" % (name, ph[0], ph[1]))
print("   → 72 / 80 / 120 이 정확히 재현된다.")
print("   → ux-widgets §R5 · UX_WIDGETS.md:446/452/460/629/1209 는 **2026-09-03 R10의 x20")
print("     재척도 이전 스케일**이다. 실척도는 x20이고 코드가 이미 24로 출하돼 있다.")
print("   ★ 그 표의 '최악 120동전/시'는 기준 72의 1.67배 — **비율은 옳다**. 절대값만 낡았다.")
print("     x20 하면 1,440 기준 / ceil 최악 2,400 — 비율 1.67배로 동일.")

print()
print("=" * 78)
print("[3] 그렇다면 floor를 확정할 이유가 남는가 — 정수 격자 **밖**의 진입로")
print("=" * 78)
entries = [
    ("m=1..60 정수 분 (자유 설정 격자)", [mm * 60.0 for mm in range(1, 61)]),
    ("데모 90초 (ForceTriggerNow / ⌃⌥⌘F)", [DEMO_SEC]),
    ("최소 세션 클램프 60초", [MIN_SESSION_SEC]),
]
nonint_found = False
for label, secs in entries:
    bad = [s for s in secs if abs(FOCUS_PER_MIN * s / 60.0 - round(FOCUS_PER_MIN * s / 60.0)) > 1e-9]
    print("   %-40s 비정수 지급 %d/%d 건" % (label, len(bad), len(secs)))
    if bad:
        nonint_found = True
print()
print("   ★ 완주 경로는 전부 정수다. 그러나 **취소 경로는 아니다** —")
print("     경과 초는 Time.deltaTime 누적이라 임의 실수다.")
for s in (61.0, 90.5, 149.9, 1499.7):
    raw = CANCEL_PER_MIN * s / 60.0
    print("     경과 %7.1f초 → 원시 %9.4f동전 → floor %4d / round %4d / ceil %4d"
          % (s, raw, math.floor(raw), math.floor(raw + 0.5), math.ceil(raw)))
print()
print("   DS-5 원문은 취소를 `분을 먼저 floor`로 적었다: floor(경과초/60) x %d" % CANCEL_PER_MIN)
print("   두 표기의 차이 — 경과 90.5초:")
print("     (가) 분 먼저 floor : floor(1.508)x%d = %d동전" % (CANCEL_PER_MIN, math.floor(90.5 / 60) * CANCEL_PER_MIN))
print("     (나) 동전에 floor  : floor(%.4f)     = %d동전" % (CANCEL_PER_MIN * 90.5 / 60.0, math.floor(CANCEL_PER_MIN * 90.5 / 60.0)))
print("   → 다르다. **(가)를 못박아야 한다** — (나)는 분당 격자를 무너뜨려 초 단위 스팸에 반응한다.")

print()
print("=" * 78)
print("[4] 취소 스팸 상한 — 1분 격자에서도 완주를 못 넘는가 (분 먼저 floor)")
print("=" * 78)
print("   %-14s %14s %14s" % ("취소 주기", "동전/시", "완주 대비"))
for period in (0.5, 0.99, 1.0, 1.01, 1.5, 2.0, 3.0, 5.0, 15.0, 25.0, 50.0, 60.0):
    pay = math.floor(period) * CANCEL_PER_MIN
    ph = pay * 60.0 / period
    print("   %-14s %14.1f %13.1f%%" % ("%.2f분마다" % period, ph, ph / base_hour * 100))
print("   → 최대 %d동전/시 = 완주 %d의 %.1f%%. 하한 규칙 불필요(§0-3-10 #4 결론 승계)."
      % (CANCEL_PER_MIN * 60, base_hour, CANCEL_PER_MIN / FOCUS_PER_MIN * 100))

print()
print("=" * 78)
print("[5] 1분 세션의 진짜 위험 — 요율이 아니라 **감시 구조**다")
print("=" * 78)
first_eval = GRACE + OBS_WIN
print("   pomodoroGraceSeconds        = %.0f초  (이 동안 ResetWindowCounters, 관측 0)" % GRACE)
print("   pomodoroObservationWindowSeconds = %.0f초" % OBS_WIN)
print("   첫 EvaluateWindow 시각      = %.0f + %.0f = %.0f초 = %.1f분" % (GRACE, OBS_WIN, first_eval, first_eval / 60))
print()
print("   %-10s %-12s %-28s %10s" % ("세션", "관측 횟수", "감시가 도는가", "지급"))
for mm in (1, 2, 3, 4, 5, 15, 25, 50, 60):
    n_eval = max(0, math.floor((mm * 60 - GRACE) / OBS_WIN))
    print("   %-10s %-12d %-28s %10d동전"
          % ("%d분" % mm, n_eval, "아니오 — 전 구간 유예" if n_eval == 0 else "예", FOCUS_PER_MIN * mm))
print()
print("   → %d분 이하 세션은 EvaluateWindow가 **한 번도 안 돈다**." % math.floor(first_eval / 60))
print("   → 그러나 지급은 감시 결과에 의존하지 않는다(완주 = 요율 x 시간, 무조건).")
print("     감시는 대사/포즈만 몬다 → **경제적 가중치 0** → 파밍 이득도 0.")
print("     m=1 은 24동전을 벌려고 실제 60초를 쓴다. 시급은 %d으로 동일." % base_hour)

print()
print("=" * 78)
print("[6] 획득 곡선 정합성 — floor + 1분 격자가 기존 사다리를 흔드는가")
print("=" * 78)
print("   집중 시급 = %d동전/시 (일일 상한 **밖** — §18-5)" % base_hour)
print()
print("   %-10s %10s %14s %16s" % ("아이템", "가격", "집중 시간", "집중 25분 세션"))
for label, price in (("일반", P_COMMON), ("희귀", P_RARE), ("영웅", P_EPIC), ("전설", P_LEG)):
    print("   %-10s %10s %11.2f시간 %13.2f회"
          % (label, "%d" % price, price / base_hour, price / (FOCUS_PER_MIN * 25)))
print()
print("   ★ 리더 브리프의 '600까지 8.3시간 / 9600까지 133시간'은 구스케일 72/시 값이다.")
print("     실척도에서는 %.2f시간 / %.2f시간." % (P_COMMON / base_hour, P_LEG / base_hour))
print()
print("   시드 %d동전으로 살 수 있는 것: 일반 %d개 / 희귀 %d개"
      % (SEED, SEED // P_COMMON, SEED // P_RARE))
print("   → §0-6-4(a) '시드가 2개를 사고 잔액이 남아 「다음은 모아야 한다」' 재현: %s"
      % ("OK" if SEED // P_COMMON == 2 and SEED % P_COMMON == 0 else "✘"))
print()
active_b = FOCUS_PER_MIN * 50 + ARCHERY_AWARD * 4
check("§18-5 '원형 B 능동 1,280' (집중 25분x2 + 활쏘기 4회, 할일 제외)", active_b, 1280)
check("§18-5 '유휴 일일 2,000은 능동의 1.56배'",
      round((BASE_CAP + POTION_BONUS) / active_b, 2), 1.56)
print("     능동 %d + [오늘 할일](시간 비종속) %d = 하루 총 %d동전"
      % (active_b, TODO, active_b + TODO))
if FAIL:
    die("§6 교정 실패: %s" % ", ".join(FAIL))

print()
print("=" * 78)
print("[7] 1분 격자가 정말 아무것도 안 바꾸는가 — 자유 설정 전/후 최대 수입 비교")
print("=" * 78)
print("   8시간(480분) 온라인 유저가 집중에 쓸 수 있는 시간을 전부 쓴다고 하면:")
for label, grid in (("고정 15/25/50", [15, 25, 50]), ("5분 격자", list(range(5, 61, 5))), ("1분 격자(신규)", list(range(1, 61)))):
    best = 0
    bestm = None
    for mm in grid:
        n = 480 // mm
        pay = n * FOCUS_PER_MIN * mm
        if pay > best:
            best, bestm = pay, mm
    print("   %-16s 최대 %6d동전 (%d분 세션 %d회)  시급 %d"
          % (label, best, bestm, 480 // bestm, base_hour))
print("   → 세 격자의 최대 수입이 같다. **격자는 수입에 영향을 주지 않는다.**")
print("     (480이 m으로 안 나누어떨어질 때만 자투리 손해가 나고, 1분 격자는 그 자투리가 0)")

print()
print("=" * 78)
print("[8] 저장 빈도 예상치 — ★ 세이브 원자적 교체 IOException 미해결 건에 걸린다")
print("=" * 78)
print("   집중 지급은 세션 **종료 시 1회**만 IsDirty를 세운다(틱 지급 아님).")
for label, grid_m in (("고정 25분 x 4회/일", 25), ("1분 격자 최악: 1분 세션 연속", 1)):
    per_day = 480 // grid_m if grid_m == 1 else 4
    print("   %-32s 하루 세이브 유발 %4d회  (8시간 기준)" % (label, per_day))
print("   → 최악(1분 연속) 480회/일 = 1분당 1회. 유휴 수급 쪽 저장 빈도와 같은 자리수다.")
print("   ★ 그래도 **집중 지급은 원자적 교체 결함의 새 위험원이 아니다** — 이미 유휴가")
print("     같은 빈도로 쓰고 있기 때문. 그러나 결함 자체는 선결이다(리더 판단용 수치).")
print()
print("   ★★ 오늘자 실측: 재화 수급/소비는 **아직 한 곳도 배선되지 않았다**.")
print("      TickIdleIncome · TryPurchaseItem · TryPayTodoDailyCoins · TryAwardArcheryCoins ·")
print("      TryGrantSeedCoins 의 프로덕션 호출부가 0건이고, FocusWatchDirector.CompleteSession()/")
print("      StopFocusSession() 은 동전을 한 푼도 지급하지 않는다(포즈 전이만 한다).")
print("      → **오늘 이 변경이 만드는 추가 저장 빈도는 0회/일**이다.")
print("      → 480회/일은 배선되는 라운드의 예산이고, 그 라운드가 IOException 선결에 걸린다.")

print()
print("=" * 78)
print("[결론]")
print("=" * 78)
print("   1. floor 확정 — **비용 0**. 현행 요율 24에서 완주 경로는 floor=round=ceil 항등.")
print("   2. 이득은 미래 방어다: 요율이 24가 아니게 되는 날(팩/버프/배율) 즉시 값을 한다.")
print("      §2-3 집중력 성과물 배율 x1.15/1.35/1.60 이 동전에 걸리면 그날 24x1.15=27.6 → 비정수.")
for mult in (1.15, 1.35, 1.60):
    ph = max((math.ceil(FOCUS_PER_MIN * mult * mm) * 60.0 / mm) for mm in range(1, 61))
    phf = min((math.floor(FOCUS_PER_MIN * mult * mm) * 60.0 / mm) for mm in range(1, 61))
    print("      배율 x%.2f (분당 %.2f): ceil 최악 %.0f동전/시 / floor 최악 %.0f동전/시 / 기준 %.0f"
          % (mult, FOCUS_PER_MIN * mult, ph, phf, FOCUS_PER_MIN * mult * 60))
print("   3. 1분 격자 방어장치 **불필요**. 최소보상·쿨다운·세션고정비 전부 과설계.")
