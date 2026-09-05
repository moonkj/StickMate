#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
stats_r16_total_fix.py — design-systems R16 (2026-09-05)
42종 총액 산수 불일치 정정 — 117,600(사다리×42) vs 115,200(§21-5) vs 113,400(프로덕션 IsOwned 의미론).

★ 규약 (CLAUDE.md / docs/TEAM.md §4 "거짓 통과 방지"):
   [0]  교정이 먼저다. 알려진 값 K1~K11 을 재현하지 못하면 SystemExit — 뒤의 숫자를 전부 폐기한다.
   [0N] 음성 대조: 고의로 틀린 「무상 종수」가 실제로 걸리는가(검사기가 아무것도 안 재고 초록을 내는 형태 배제).
   [0P] 양성 대조: .asset 파서에 가짜 행을 흘려 「idx0 전부 Lv.1」 프로브가 실제로 뒤집히는가.
   교정 기대값은 이 스크립트의 함수가 아니라 **문서에 이미 적힌 숫자 / .asset 원본**에서 온다.

입력 앵커 (전부 남이 쓴 값 · 내가 지어낸 것 0개):
   - Assets/_Project/Resources/Items/*.asset            : slot / itemIndex / requiredLevel / displayName (실측 파싱)
   - Core/ItemCatalog.cs _rarityByRank                  : [C,C,R,R,E,L]
   - Core/CurrencyRules.cs                              : 600 / 1,400 / 3,200 / 9,600 · SeedCoins 1,200
   - ECONOMY_SPEC §0-2-1 (R1)                           : 유료 35종 = 4,550 (구단위 30/70/150/330)
   - ECONOMY_SPEC §0-2-3 (R2) 패키지 ⑤                  : 외형 rank0 유료화 → 유료 38종
   - DESIGN_SYSTEMS_STATS §12-2 (R7)                    : 7×사다리 − 4×600 = 115,200 · 영웅 3,000 교정 92,800 · 상한 10,971
   - ECONOMY_SPEC §0-6-4 (R13)                          : 외형 50,400 · 완주 35.1일 @3,280 · 시드 1,200 → 2종
   - DESIGN_SYSTEMS_STATS §21-2-e (R15)                 : 1일차 무료 4종 착용 = 11/10/10/11
"""

import os, re, glob, struct, sys

FAIL = []
def check(tag, got, want, note=""):
    ok = (got == want)
    print(f"  [{'OK ' if ok else 'FAIL'}] {tag:<58} got={got!r:<28} want={want!r}" + (f"  {note}" if note else ""))
    if not ok: FAIL.append(tag)
    return ok

def close(tag, got, want, tol, note=""):
    ok = abs(got - want) <= tol
    print(f"  [{'OK ' if ok else 'FAIL'}] {tag:<58} got={got!r:<28} want={want!r} (±{tol})" + (f"  {note}" if note else ""))
    if not ok: FAIL.append(tag)
    return ok

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# ── 상수 (문서/코드에서 그대로 옮김) ─────────────────────────────────────────
RARITY = ["일반", "일반", "희귀", "희귀", "영웅", "전설"]              # rank(=idx) → 등급
PRICE  = {"일반": 600, "희귀": 1400, "영웅": 3200, "전설": 9600}      # CurrencyRules.cs
PRICE_R7_CAL = {"일반": 600, "희귀": 1400, "영웅": 3000, "전설": 6600} # §12-2 교정용(【우】×20)
OLD    = {"일반": 30,  "희귀": 70,   "영웅": 150,  "전설": 330}       # ECONOMY_SPEC 구단위
SEED   = 1200
DAY_B_OLD, DAY_B_NOW = 1280, 3280                                    # 원형 B 일일 수입 (R7 / R10-c 이후)
LV30_DAYS, RATIO_CAP = 75.0, 1.30                                    # §12-2 판정 기준 (c)
SLOT_NAMES = ["Head", "Eyes", "Neck", "Shoulders", "Hair", "Fx", "Pet"]
STAT_SLOTS = set(range(0, 4)); LOOK_SLOTS = set(range(4, 7))

def ladder(price): return sum(price[r] for r in RARITY)              # 슬롯당 6종 합

# ── [0] 교정 ─────────────────────────────────────────────────────────────────
print("=" * 104)
print("[0] 교정 — 이 뒤의 숫자를 믿어도 되는 이유")
print("=" * 104)

def _unesc(s):  # Unity YAML 은 한글을 \uXXXX 로 직렬화한다
    return re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), s)

def parse_assets(extra_rows=None):
    rows = []
    for p in sorted(glob.glob(os.path.join(REPO, "Assets/_Project/Resources/Items/*.asset"))):
        txt = open(p, encoding="utf-8", errors="replace").read()
        def f(name, default=None):
            m = re.search(r"^  %s:\s*(.*)$" % name, txt, re.M)
            return _unesc(m.group(1).strip().strip('"')) if m else default
        rows.append(dict(file=os.path.basename(p)[:-6], slot=int(f("slot", "-1")),
                         idx=int(f("itemIndex", "-1")), lv=int(f("requiredLevel", "-1")),
                         name=f("displayName", "?")))
    if extra_rows: rows += extra_rows
    return rows

ASSETS = parse_assets()
IDX0 = sorted((a for a in ASSETS if a["idx"] == 0), key=lambda a: a["slot"])

print("\n-- K1/K2. 카탈로그 실측 --")
check("K1  슬롯당 사다리 합", ladder(PRICE), 16800, "§12-2")
check("K2a .asset 총수", len(ASSETS), 42)
check("K2b 슬롯 × 종", sorted(set((a["slot"]) for a in ASSETS)), list(range(7)))
check("K2c idx0 종수(7슬롯)", len(IDX0), 7)
check("K2d idx0 전부 requiredLevel 1", all(a["lv"] == 1 for a in IDX0), True,
      "IsOwned 레벨 파생 항이 Lv.1부터 참")
print("      idx0 =", " · ".join(f"{a['file']}({a['name']})" for a in IDX0))

def total(price, free_slots_idx0):
    """유료 총액. free_slots_idx0 = idx0 이 무상인 슬롯 집합."""
    return 7 * ladder(price) - sum(price[RARITY[0]] for s in range(7) if s in free_slots_idx0)

ALL7 = set(range(7)); STAT4 = STAT_SLOTS; NONE = set()

print("\n-- K3/K4. 계보 — R1(7종 무상) → R2(외형 유료화) --")
check("K3  R1 §0-2-1 유료 35종 구단위 총액", total(OLD, ALL7), 4550, "= 650 × 7슬롯")
check("K4a R2 §0-2-3 ⑤ 유료 38종 구단위 총액", total(OLD, STAT4), 4640, "§12-2 교정 문장의 4,640")
check("K4b §12-2 교정: 영웅 3,000·전설 6,600 이면 92,800", total(PRICE_R7_CAL, STAT4), 92800, "= 4,640 × 20")
check("K4c 유료 종수 R1 / R2", (42 - len(ALL7), 42 - len(STAT4)), (35, 38), "economy_r2_calc :487 「38」")

print("\n-- K5~K8. R7 §12-2 · R13 §0-6-4 · R15 §21-5 --")
A_TOTAL = total(PRICE, STAT4)
check("K5  §12-2 식 7×16,800 − 4×600", A_TOTAL, 115200)
check("K6  §0-6-4(b) 외형 18종 총액(전부 유료)", 3 * ladder(PRICE), 50400)
check("K6b 스탯 24종 유료(idx0 4종 제외)", 4 * ladder(PRICE) - 4 * PRICE["일반"], 64800, "economy_r13_shop.out :68")
close("K7a 완주일 @1,280", A_TOTAL / DAY_B_OLD, 90.0, 0.05, "§12-2")
close("K7b 완주일 @3,280", A_TOTAL / DAY_B_NOW, 35.1, 0.05, "§0-6-4(b)")
NONLEG_A = 7 * (2 * PRICE["일반"] + 2 * PRICE["희귀"] + PRICE["영웅"]) - 4 * PRICE["일반"]
check("K8a §12-2 상한식의 비전설 유료분 48,000", NONLEG_A, 48000)
check("K8b 전설 상한 X ≤ (97.5×1,280 − 48,000)/7", int((LV30_DAYS * RATIO_CAP * DAY_B_OLD - NONLEG_A) // 7), 10971)
check("K9  §21-5 표 4행 총액", [total({**PRICE, "전설": x}, STAT4) for x in (6600, 7400, 9600, 11200)],
      [94200, 99800, 115200, 126400])
check("K10 §0-6-4(a) 시드 1,200 → 외형 idx0 600 짜리 몇 종", SEED // PRICE["일반"], 2)

# 1일차 스탯 (R15 §21-2-d 확정표의 idx0 행만)
BASE = [8, 6, 5, 7]; MAIN_C, SUB_C = 3, 1
IDX0_SUB = {0: 2, 1: 3, 2: 1, 3: 2}   # HEAD→매력, EYES→민첩, NECK→관찰력, BACK→매력 (§21-2-d idx0 행)
def day1(worn):
    v = BASE[:]
    for s in worn: v[s] += MAIN_C; v[IDX0_SUB[s]] += SUB_C
    return v
check("K11 1일차 무료 4종 전부 착용", day1([0, 1, 2, 3]), [11, 10, 10, 11], "§21-2-e")

# ── [0N] 음성 대조 ───────────────────────────────────────────────────────────
print("\n-- [0N] 음성 대조 — 틀린 무상 규칙이 실제로 걸리는가 --")
wrong = {"무상 0종": total(PRICE, NONE), "무상 6종(고의 오류)": total(PRICE, set(range(6))), "무상 7종": total(PRICE, ALL7)}
for k, v in wrong.items():
    print(f"      {k:<16} → {v:,}  {'≠ 115,200 ✔ 걸린다' if v != 115200 else '== 115,200 ✘ 검사기가 죽었다'}")
    if v == 115200: FAIL.append("0N " + k)
check("0N  세 값이 전부 서로 다르다", len({wrong[k] for k in wrong} | {A_TOTAL}), 4)

# ── [0P] 양성 대조 ───────────────────────────────────────────────────────────
print("\n-- [0P] 양성 대조 — 가짜 idx0(Lv.5) 를 흘리면 K2d 프로브가 뒤집히는가 --")
FAKE = parse_assets([dict(file="FAKE_idx0_lv5", slot=5, idx=0, lv=5, name="가짜")])
fake_idx0 = [a for a in FAKE if a["idx"] == 0]
check("0P  가짜 행 포함 시 「idx0 전부 Lv.1」", all(a["lv"] == 1 for a in fake_idx0), False, "뒤집혀야 정상")
check("0P  가짜 행 포함 시 idx0 종수", len(fake_idx0), 8)

if FAIL:
    print("\n!!! 교정/대조 실패 — 아래 숫자를 전부 폐기한다:", FAIL); raise SystemExit(1)
print("\n  ★ 교정 K1~K11 + 음성 대조 + 양성 대조 = 전부 PASS\n")

# ── [1] 세 총액이 각각 무엇을 세는가 ─────────────────────────────────────────
print("=" * 104)
print("[1] 세 총액 — 같은 42종을 세 규칙으로 센 것이다. 산수 오류는 0건")
print("=" * 104)
G = total(PRICE, NONE); A = total(PRICE, STAT4); B = total(PRICE, ALL7)
print(f"  총액(사다리×42, 무상 0종)                 = {G:>8,}   ← coder-systems 가 낸 117,600. 「정가표 합」이지 소모처가 아니다")
print(f"  안 A 문서 규칙(스탯 idx0 4종 무상, 유료 38) = {A:>8,}   ← R2 §0-2-3 ⑤ → §12-2 → §0-6-4 → §21-5")
print(f"  안 B 코드 규칙(idx0 7종 무상, 유료 35)     = {B:>8,}   ← ItemCatalogEntry.IsOwned: Lv ≥ requiredLevel(=1) ∪ 구매")
print(f"  G − A = {G - A:,} (= 스탯 idx0 4 × 600) · A − B = {A - B:,} (= 외형 idx0 3 × 600) · G − B = {G - B:,}")
print("\n  슬롯별 (정가 / 안A 유료 / 안B 유료):")
for s in range(7):
    g = ladder(PRICE); a = g - (PRICE["일반"] if s in STAT4 else 0); b = g - PRICE["일반"]
    print(f"    {SLOT_NAMES[s]:<10} {g:>7,} / {a:>7,} / {b:>7,}   idx0 = {IDX0[s]['name']}")
print(f"    {'스탯 4슬롯':<10} {4*ladder(PRICE):>7,} / {4*ladder(PRICE)-4*600:>7,} / {4*ladder(PRICE)-4*600:>7,}")
print(f"    {'외형 3슬롯':<10} {3*ladder(PRICE):>7,} / {3*ladder(PRICE):>7,} / {3*ladder(PRICE)-3*600:>7,}   ← §21-2-g 의 50,400 은 안 A 값")

# ── [2] 안 A / 안 B 하류 수치 ────────────────────────────────────────────────
print()
print("=" * 104)
print("[2] 안 A(유료 38) vs 안 B(유료 35) — 하류 수치와 판정 기준 재통과")
print("=" * 104)
def nonleg(free_slots): return 7 * (2 * PRICE["일반"] + 2 * PRICE["희귀"] + PRICE["영웅"]) - len(free_slots) * PRICE["일반"]
rows = []
for name, fs in (("A 문서", STAT4), ("B 코드", ALL7)):
    t = total(PRICE, fs)
    x_cap = int((LV30_DAYS * RATIO_CAP * DAY_B_OLD - nonleg(fs)) // 7)
    rows.append((name, t, 4 * ladder(PRICE) - 4 * 600, 3 * ladder(PRICE) - (3 * 600 if fs == ALL7 else 0),
                 t / DAY_B_OLD, t / DAY_B_NOW, t / DAY_B_OLD / LV30_DAYS, x_cap))
print(f"  {'안':<6} {'42종 소모액':>10} {'스탯24':>8} {'외형18':>8} {'완주@1280':>9} {'완주@3280':>9} {'/75일':>6} {'전설상한X':>9}  판정(c) ≤97.5일 · ≤1.30×")
for r in rows:
    ok = r[4] <= LV30_DAYS * RATIO_CAP and r[6] <= RATIO_CAP
    print(f"  {r[0]:<6} {r[1]:>10,} {r[2]:>8,} {r[3]:>8,} {r[4]:>9.1f} {r[5]:>9.1f} {r[6]:>6.2f} {r[7]:>9,}  {'PASS' if ok else 'FAIL'}")
dA, dB = rows[0], rows[1]
print(f"\n  B − A: 소모액 {dB[1]-dA[1]:+,} ({(dB[1]-dA[1])/dA[1]*100:+.2f}%) · 완주 {dB[4]-dA[4]:+.1f}일 @1,280 · {dB[5]-dA[5]:+.2f}일 @3,280 · 전설 상한 {dB[7]-dA[7]:+,}")
print(f"  ⇒ 두 안 모두 판정 기준 (c) 통과. 9,600 의 여유: A {97.5-dA[4]:.1f}일 / B {97.5-dB[4]:.1f}일")
print(f"  전설 = 집중 25분 세션 {PRICE['전설']/600:.1f}회 — 안과 무관 (가격표가 안 바뀐다)")

# ── [3] 1일차 그림 ───────────────────────────────────────────────────────────
print()
print("=" * 104)
print("[3] 1일차 — 무상 규칙 × 상점 게이트 가정. R2 가 외형을 유료화한 이유가 지금도 성립하는가")
print("=" * 104)
def f32(x): return struct.unpack("f", struct.pack("f", x))[0]
def need(L): return f32(f32(100.0) * f32(pow(max(1, L), f32(1.05))))
def hours_to(L): return sum(need(k) for k in range(1, L)) / 90.0
close("K12 Lv.5 누적시간 (클래스 주석 11.7h)", round(hours_to(5), 1), 11.7, 0.05)
LV1_COMMON_ABOVE = sorted((a for a in ASSETS if a["idx"] in (0, 1) and a["lv"] > 1), key=lambda a: a["lv"])
print(f"\n  Lv.1 초과 일반(600) 아이템 = {len(LV1_COMMON_ABOVE)}종: " +
      " · ".join(f"{a['name']}(Lv{a['lv']})" for a in LV1_COMMON_ABOVE))
print("\n  가정 ㄱ) 상점이 요구 레벨 도달 이후에만 판다 (§0-6-2 (c) 시뮬 모형 · §4-1 1항)")
print(f"    안 A: 1일차 살아있는 버튼 = 외형 idx0 3종 (1,800). 시드 {SEED:,} 로 2종 → 그날 유휴 2,000 로 3번째 → 잔액 {SEED - 1200 + 2000 - 600:,}  (§0-6-4(a) 재현)")
print(f"         ★ 그 3종 중 하나가 look.fx.none 「없음」이다 — 600동전짜리 「없음」이 첫 화면에 선다")
print(f"    안 B: 1일차 살아있는 버튼 = 0. 첫 버튼 = Lv.5 = {hours_to(5):.1f}h 온라인 → 8h/일이면 {int(hours_to(5)//8)+1}일차 · 2h/일이면 {int(hours_to(5)//2)+1}일차")
print(f"         ⇒ R2 §0-2-3 의 「1일차 버튼 0」 결함이 그대로 돌아온다")
print("\n  가정 ㄴ) 상점이 요구 레벨 이전에도 판다 = 동전이 「요구 레벨을 앞당긴다」(§0-6-2 DS-9 문장 · 결제=시간 단축 원칙)")
print(f"    안 A: 1일차 버튼 = 38 (외형 idx0 3 + 나머지 35). 시드로 600 짜리 {SEED//600}종")
print(f"    안 B: 1일차 버튼 = 35. 시드로 600 짜리 {SEED//600}종 / 후보 {len(LV1_COMMON_ABOVE)}종 → 「고르는 화면」 성질 유지(2/{len(LV1_COMMON_ABOVE)})")
print("\n  ★ 어느 안이 맞는지는 「상점 게이트」가 정한다 — 그건 이 라운드 소관이 아니다(배선 라운드 · game-architect).")

# ── [4] 보유 ≠ 착용 관측 ─────────────────────────────────────────────────────
print()
print("=" * 104)
print("[4] 관측 — 「무상 보유」와 「착용」은 다르다 (EquipmentModel.CreateDefaultWorn: Head·Eyes 만 착용)")
print("=" * 104)
print(f"  기본 착용(모자·안경)만        = {day1([0, 1])}  → 초급(10) 도달 {sum(1 for v in day1([0,1]) if v >= 10)}/4")
print(f"  무료 4종 전부 착용(사용자 2클릭) = {day1([0, 1, 2, 3])}  → 초급 도달 {sum(1 for v in day1([0,1,2,3]) if v >= 10)}/4  (U-5)")
print("  ⇒ U-5 「1일차 4스탯 초급」은 0원이지만 자동이 아니다 — 나비넥타이·짧은망토를 입혀야 한다(→ ux-designer 관측 1건).")

print("\n  저장 빈도: 무상 보유는 카탈로그 사실(requiredLevel=1)이라 저장 사건 0회/일. 안 A 채택 시 v9 마이그레이션 1회(평생).")
print("\n  DONE — FAIL 0건" if not FAIL else f"\n  FAIL {len(FAIL)}건: {FAIL}")
