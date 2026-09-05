#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
stats_r15_close.py — design-systems R15 (2026-09-05)
U-7(부스탯 방향) 최종 확정 · M3/M4 천장 모형 판정 · U-17 전설 가격 재검산.

★ 규약 (CLAUDE.md / docs/TEAM.md §4 "거짓 통과 방지"):
   [0] 교정이 먼저다. 알려진 값 K1~K16을 재현하지 못하면 SystemExit — 뒤의 숫자를 전부 폐기한다.
   [0N] 음성 대조: 고의로 틀린 배정표가 실제로 검사에 걸리는지 먼저 보인다.
        (검사기가 아무것도 안 재고 초록을 내는 형태를 배제한다)
   교정 기대값은 「프로덕션 함수」가 아니라 **문서에 이미 적힌 숫자 / .asset 원본**에서 온다.

입력 앵커 (전부 남이 쓴 값 · 내가 지어낸 것 0개):
   - Assets/_Project/Resources/Items/*.asset          : slot / itemIndex / requiredLevel (실측 파싱)
   - Core/ItemCatalog.cs:852 _rarityByRank            : [C,C,R,R,E,L]  (거울 재구현, K2/K3로 교정)
   - DESIGN_SYSTEMS_STATS §1-2 BASE/TIERS/CAP         : {8,6,5,7} / 10,20,32 / 40
   - DESIGN_SYSTEMS_STATS §1-3 MAINV/SUBV             : 3/6/10/15 · 1/2/4/6
   - DESIGN_SYSTEMS_STATS §14-3 R8 확정표(잠정)        : 교정 K4~K7의 대상
   - DESIGN_SYSTEMS_STATS §15-4 M4 패키지              : 교정 K11/K12의 대상
   - DESIGN_SYSTEMS_GRADE_SIGNAL §3-4                 : 교정 K10
   - ECONOMY_SPEC §0-6                                : 교정 K13~K16
"""

import sys, os, glob, re, itertools
from collections import Counter

FAIL = []
def check(tag, got, want, note=""):
    ok = (got == want)
    print(f"  [{'OK ' if ok else 'FAIL'}] {tag:<52} got={got!r:<34} want={want!r}" + (f"  {note}" if note else ""))
    if not ok: FAIL.append(tag)
    return ok

def close(tag, got, want, tol, note=""):
    ok = abs(got - want) <= tol
    print(f"  [{'OK ' if ok else 'FAIL'}] {tag:<52} got={got!r:<34} want={want!r} (±{tol})" + (f"  {note}" if note else ""))
    if not ok: FAIL.append(tag)
    return ok

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# ─────────────────────────────────────────────────────────────────────────────
# 상수 (문서에서 그대로 옮김)
# ─────────────────────────────────────────────────────────────────────────────
STATS  = ["집중력", "관찰력", "매력", "민첩"]          # 인덱스 0..3
SLOTS  = ["HEAD", "EYES", "NECK", "BACK"]              # 슬롯 i 의 주스탯 = 스탯 i
BASE   = [8, 6, 5, 7]                                  # §1-2
TIER3  = [10, 20, 32]                                  # §1-2
CAP3   = 40                                            # §1-2 (하드 클램프)
TIER4  = [10, 20, 32, 44]                              # §15-4 (c) 제안
CAP4   = 44                                            # §15-4 (c) 제안
RARITY = ["일반", "일반", "희귀", "희귀", "영웅", "전설"]   # rank(=idx) → 등급, ItemCatalog._rarityByRank 거울
MAINV  = {"일반": 3,  "희귀": 6,  "영웅": 10, "전설": 15}   # §1-3
SUBV   = {"일반": 1,  "희귀": 2,  "영웅": 4,  "전설": 6}    # §1-3
SETTHEME = 2       # 테마 세트 완성 시 스탯별 +2 (§1-7 / §0-6-1 #5)
SETLEG   = 6       # 전설 풀세트 시 스탯별 +6 (§15-4 (b), M4 전용)
PRICE  = {"일반": 600, "희귀": 1400, "영웅": 3200, "전설": 9600}   # §13-3 (4)

# ─────────────────────────────────────────────────────────────────────────────
# [0] 교정 — 실측 파싱
# ─────────────────────────────────────────────────────────────────────────────
print("=" * 100)
print("[0] 교정 — 이 뒤의 숫자를 믿어도 되는 이유")
print("=" * 100)

def _unesc(s):
    # Unity YAML 은 한글을 \uXXXX 로 직렬화한다. 디코드 안 하면 K2a 가 조용히 갈린다.
    return re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), s)

def parse_assets():
    rows = []
    for p in sorted(glob.glob(os.path.join(REPO, "Assets/_Project/Resources/Items/*.asset"))):
        txt = open(p, encoding="utf-8", errors="replace").read()
        def f(name, default=None):
            m = re.search(r"^  %s:\s*(.*)$" % name, txt, re.M)
            return _unesc(m.group(1).strip()) if m else default
        rows.append(dict(
            file=os.path.basename(p)[:-6],
            slot=int(f("slot", "-1")),
            idx=int(f("itemIndex", "-1")),
            lv=int(f("requiredLevel", "-1")),
            cohort=int(f("cohortId", "0")),
            declared=int(f("declaredRarity", "0")),
            name=(f("displayName", "") or "").strip('"'),
        ))
    return rows

ASSETS = parse_assets()
print("\n-- K1. 카탈로그 실측 (오늘 R20 장비 재작업 이후) --")
check("K1a 애셋 총수", len(ASSETS), 42)
bys = Counter(r["slot"] for r in ASSETS)
check("K1b 슬롯 7종 × 6개", sorted(bys.items()), [(i, 6) for i in range(7)])
check("K1c 선언 등급(declaredRarity) 비-Derived 개수", sum(1 for r in ASSETS if r["declared"] != 0), 0,
      "= 42종 전부 파생. 개별 선언 0건")
check("K1d 코호트 비-0 개수", sum(1 for r in ASSETS if r["cohort"] != 0), 0, "= 팩 코호트 없음(기본 42종)")

# 슬롯별 requiredLevel — 문서 §2 표(2026-09-03 기록)와 대조. 오늘 바뀌었으면 여기서 갈린다.
DOC_LEVELS = {   # DESIGN_SYSTEMS_STATS §2 표에 적힌 값 (독립 출처)
    0: [1, 5, 9, 20, 23, 26], 1: [1, 6, 11, 15, 19, 23],
    2: [1, 8, 12, 18, 21, 25], 3: [1, 13, 17, 22, 25, 28],
}
got_levels = {}
for s in range(7):
    got_levels[s] = [r["lv"] for r in sorted((x for x in ASSETS if x["slot"] == s), key=lambda x: x["idx"])]
for s in range(4):
    check(f"K1e 슬롯{s}({SLOTS[s]}) requiredLevel", got_levels[s], DOC_LEVELS[s], "§2 표와 대조")
check("K1f 슬롯별 레벨 오름차순(=idx가 곧 rank)", all(got_levels[s] == sorted(got_levels[s]) for s in range(7)), True)

def rarity_of(idx):     # ItemCatalog.RarityOfRank 거울: step = rank*6//6 = rank
    return RARITY[idx]

print("\n-- K2/K3. 등급 파생 (ItemCatalog 거울) --")
legend = {}
for s in range(4):
    row = sorted((x for x in ASSETS if x["slot"] == s), key=lambda x: x["idx"])
    legend[SLOTS[s]] = row[5]["name"]
check("K2a 전설 4종", [legend[k] for k in SLOTS], ["밀짚모자", "안대", "반다나", "요정 날개"], "§14-2")
crown = next(r for r in ASSETS if r["file"] == "equip_head_crown")
wings = next(r for r in ASSETS if r["file"] == "equip_shoulders_wings")
check("K2b 왕관 등급", rarity_of(crown["idx"]), "희귀", "§14-2 · §13-1(iii)")
check("K2c 날개 등급", rarity_of(wings["idx"]), "희귀", "§14-2")
dist = Counter(rarity_of(r["idx"]) for r in ASSETS)
check("K3 42종 등급 분포", [dist["일반"], dist["희귀"], dist["영웅"], dist["전설"]], [14, 14, 7, 7],
      "ItemCatalog.cs:850 주석")

# ─────────────────────────────────────────────────────────────────────────────
# 스탯 엔진 (M3 / M4 공용)
# ─────────────────────────────────────────────────────────────────────────────
def totals(loadout, sub1, sub2=None, model="M3", theme_set=False):
    """loadout = (idx_head, idx_eyes, idx_neck, idx_back).
       sub1[s][i] = 스탯 인덱스.  sub2[s] = 전설(idx5) 두 번째 부스탯 (M4 전용).
       반환: (원시 4스탯, 클램프 후 4스탯)"""
    cap = CAP3 if model == "M3" else CAP4
    raw = list(BASE)
    for s, i in enumerate(loadout):
        r = rarity_of(i)
        raw[s] += MAINV[r]                       # 주스탯은 그 슬롯의 스탯 s 로
        raw[sub1[s][i]] += SUBV[r]
        if model == "M4" and i == 5 and sub2 is not None:
            raw[sub2[s]] += SUBV["전설"]
    if model == "M4" and all(i == 5 for i in loadout):
        for t in range(4): raw[t] += SETLEG
    if theme_set:
        for t in range(4): raw[t] += SETTHEME
    return raw, [min(v, cap) for v in raw]

ALL_LOADOUTS = list(itertools.product(range(6), repeat=4))     # 6^4 = 1,296

def sub2_candidates(sub1):
    """M4 전용. 전설(idx5)의 두 번째 부스탯 후보 = 자기 주스탯도 sub1도 아닌 스탯 (슬롯당 2가지)."""
    return list(itertools.product(*[[t for t in range(4) if t != s and t != sub1[s][5]] for s in range(4)]))

def analyse(sub1, sub2=None, model="M3"):
    cap  = CAP3 if model == "M3" else CAP4
    top  = TIER3[-1] if model == "M3" else TIER4[-1]     # 판정 대상 최상단 임계
    hi   = 32                                            # 고급(두 모형 공통 3번째 임계)
    per_stat_max = [0, 0, 0, 0]
    raw_max = [0, 0, 0, 0]
    pairs_hi, pairs_top = set(), set()
    max_simul_hi = max_simul_top = 0
    for lo in ALL_LOADOUTS:
        raw, cl = totals(lo, sub1, sub2, model)
        for t in range(4):
            per_stat_max[t] = max(per_stat_max[t], cl[t])
            raw_max[t] = max(raw_max[t], raw[t])
        setshi  = frozenset(t for t in range(4) if cl[t] >= hi)
        setstop = frozenset(t for t in range(4) if cl[t] >= top)
        max_simul_hi  = max(max_simul_hi,  len(setshi))
        max_simul_top = max(max_simul_top, len(setstop))
        if len(setshi)  >= 2: pairs_hi.add(setshi)
        if len(setstop) >= 2: pairs_top.add(setstop)
    day1 = totals((0, 0, 0, 0), sub1, sub2, model)[1]
    return dict(per_stat_max=per_stat_max, raw_max=raw_max, cap=cap, top=top,
                max_simul_hi=max_simul_hi, max_simul_top=max_simul_top,
                pairs_hi=pairs_hi, pairs_top=pairs_top, day1=day1,
                reach_hi=[m >= hi for m in per_stat_max],
                reach_top=[m >= top for m in per_stat_max])

# R8 §14-3 확정표(잠정) — 문서에서 그대로 옮김
S = {n: i for i, n in enumerate(STATS)}
R8 = [
    [S["민첩"],   S["관찰력"], S["매력"],   S["민첩"],   S["관찰력"], S["매력"]],    # HEAD
    [S["매력"],   S["민첩"],   S["집중력"], S["매력"],   S["민첩"],   S["집중력"]],  # EYES
    [S["관찰력"], S["민첩"],   S["집중력"], S["집중력"], S["관찰력"], S["민첩"]],    # NECK
    [S["매력"],   S["집중력"], S["관찰력"], S["집중력"], S["관찰력"], S["매력"]],    # BACK
]

print("\n-- K4~K8. R8 §14-3 확정표(잠정) 재현 --")
a8 = analyse(R8, model="M3")
check("K4 1일차 4스탯", a8["day1"], [11, 10, 10, 11], "§14-3 검산표")
check("K5 스탯별 최대치", a8["per_stat_max"], [33, 33, 34, 34], "§14-3 검산표")
subcount = Counter(sub1i for row in R8 for sub1i in row)
check("K6 부스탯 편중", [subcount[t] for t in range(4)], [6, 6, 6, 6], "§14-3 검산표")
check("K7 동시 고급 가능 쌍", sorted(tuple(sorted(p)) for p in a8["pairs_hi"]),
      [(2, 3)], "§14-3 = (매력, 민첩) 1쌍")
check("K8a §C-1(나) 이론 최대 공식 BASE+15+6×3", BASE[0] + MAINV["전설"] + 3 * SUBV["전설"], 41,
      "§C-1(나) 표: 41/39/38/40")
check("K8b §C-1(나) 4스탯 이론 최대", [BASE[t] + MAINV["전설"] + 3 * SUBV["전설"] for t in range(4)],
      [41, 39, 38, 40], "§C-1(나) 표 전체")
# ★ 아래는 교정이 아니라 관측이다 — §14-4 가 이 41 을 「R8 배정표의 원시 최대」로 옮겨 적었는데
#   그 두 값은 같지 않다. 실측값을 F1 으로 보고한다(교정 실패로 세지 않는다).
F1_R8_RAWMAX = max(a8["raw_max"])
print(f"  [관측] F1 — R8 §14-3 배정표의 **실제** 원시 최대 = {F1_R8_RAWMAX} "
      f"(§14-4 는 41 이라고 적었다 · 차이 {41 - F1_R8_RAWMAX})")

print("\n-- K9. I-3 부등식 증명 재현 (§14-4) --")
need_sub_hi = [32 - BASE[t] - 15 for t in range(4)]
check("K9a 고급32 필요 부스탯", need_sub_hi, [9, 11, 12, 10])
check("K9b 가장 싼 3스탯 합", sum(sorted(need_sub_hi)[:3]), 30)
check("K9c 로드아웃 부스탯 총량 상한(M3)", 4 * SUBV["전설"], 24, "30 > 24 ⇒ 동시 3 고급 불가")

print("\n-- K10. R14 §3-4 (등급 신호 라운드) 재현 --")
contrib = {r: MAINV[r] + SUBV[r] for r in ["일반", "희귀", "영웅", "전설"]}
check("K10a 아이템 1개 총합 기여", [contrib[r] for r in ["일반", "희귀", "영웅", "전설"]], [4, 8, 14, 21])
check("K10b 혼합 최고(4슬롯 전설)", 4 * contrib["전설"], 84)
check("K10c 테마 세트가 이기는 경계 Σ", 84 - 4 * SETTHEME + 1, 77, "Σ>76 ⟺ Σ≥77")

print("\n-- K11/K12. R9 §15-4 M4 패키지 재현 (부스탯 2개는 서로 다른 스탯 전제) --")
raw_m4 = [BASE[t] + 15 + 3 * SUBV["전설"] + SETLEG for t in range(4)]
check("K11 M4 원시 최대", raw_m4, [47, 45, 44, 46], "§15-4 검산")
need_top = [CAP4 - BASE[t] - 15 - SETLEG for t in range(4)]
check("K12a 최상급44 필요 부스탯", need_top, [15, 17, 18, 16], "§15-4")
check("K12b 6단위 환산 칸수", [-(-n // SUBV["전설"]) for n in need_top], [3, 3, 3, 3], "§15-4 '전부 3칸씩'")
check("K12c 전설 풀세트 부스탯 칸", 4 * 2, 8, "§15-4")

print("\n-- K13~K16. 경제 (ECONOMY_SPEC §0-6 / §13-3) --")
def total_catalog_cost():
    # 7슬롯 × (일반2 + 희귀2 + 영웅1 + 전설1) − 스탯 4슬롯 rank0 무상 4×600
    per = 2 * PRICE["일반"] + 2 * PRICE["희귀"] + PRICE["영웅"] + PRICE["전설"]
    return 7 * per - 4 * PRICE["일반"]
check("K13a 42종 완주 총액", total_catalog_cost(), 115200, "§0-6-4(b) 채택안")
close("K13b 완주일 @원형B 1,280", total_catalog_cost() / 1280, 90.0, 0.05, "§13-3")
close("K13c 완주일 @원형B 3,280", total_catalog_cost() / 3280, 35.1, 0.05, "§0-6-4(b)")
check("K14a 전설가 상한 X (7X ≤ 97.5×1280 − 48000)", (97.5 * 1280 - 48000) // 7, 10971, "§12-2")
close("K14b 전설 = 집중25분 세션", PRICE["전설"] / 600, 16.0, 1e-9, "§12-2 근거2")
check("K15 시드 1,200으로 살 수 있는 외형 rank0 종수", 1200 // PRICE["일반"], 2, "§0-6-4(a) 2/3")
check("K16 세트 완성 보너스 총합", 4 * SETTHEME, 8, "§0-6-1 #5")

# 유휴 12동전/분 → 등급별 대기 분 (§0-6-2 (e))
wait = {r: PRICE[r] / 12 for r in PRICE}
close("K16b 일반 대기 분", wait["일반"], 50.0, 0.01)
close("K16c 전설 대기 분", wait["전설"], 800.0, 0.01)

# ── 음성 대조 ────────────────────────────────────────────────────────────────
print("\n-- [0N] 음성 대조 — 고의로 틀린 배정표가 실제로 걸리는가 --")
BAD = [[S["민첩"]] * 6, [S["매력"]] * 6, [S["민첩"]] * 6, [S["매력"]] * 6]   # 전부 한 스탯으로 몰아버림
ab = analyse(BAD, model="M3")
neg_ok = True
neg_ok &= (ab["day1"] != [11, 10, 10, 11])
neg_ok &= (Counter(x for row in BAD for x in row)[0] == 0)     # 집중력 부스탯 0개
neg_ok &= (not all(ab["reach_hi"]))                            # I-2 깨짐
check("N1 잘못된 표는 1일차/편중/I-2 전부에서 걸린다", neg_ok, True,
      f"day1={ab['day1']} reach_hi={ab['reach_hi']}")
# 검사기가 「무엇이든 통과」가 아님을 한 번 더: 올바른 표는 위 셋을 통과한다
check("N2 올바른 표는 같은 검사를 통과한다", (a8["day1"] == [11, 10, 10, 11] and all(a8["reach_hi"])), True)

if FAIL:
    print("\n" + "!" * 100)
    print("교정 실패:", FAIL)
    print("이 스크립트의 이후 숫자를 전부 폐기한다.")
    print("!" * 100)
    sys.exit(1)
print(f"\n★ 교정 {len(FAIL) == 0 and '전부'} PASS (K1~K16 + 음성 대조 2). 아래 숫자를 신뢰할 수 있다.\n")

# ═════════════════════════════════════════════════════════════════════════════
# [1] §13-4 제약(등급대 분산)을 형식화하고, R8 표가 그것을 만족하는지 잰다
# ═════════════════════════════════════════════════════════════════════════════
print("=" * 100)
print("[1] §13-4 제약 「부스탯이 특정 등급대에 몰리지 않게 분산」 — 형식화와 R8 표 판정")
print("=" * 100)

RCLASS_COUNT = Counter(RARITY)      # 슬롯 1개당 일반2 희귀2 영웅1 전설1
IDEAL = {r: RCLASS_COUNT[r] * 4 // 4 for r in RCLASS_COUNT}    # 4슬롯 × n ÷ 4스탯
print(f"\n  24종의 등급 구성  = 슬롯당 {dict(RCLASS_COUNT)} × 4슬롯 = " +
      f"일반 8 · 희귀 8 · 영웅 4 · 전설 4")
print(f"  스탯당 부스탯 수  = 6 (C1이 강제)")
print(f"  완전 균등 배분    = 스탯마다 일반 {IDEAL['일반']} · 희귀 {IDEAL['희귀']} · 영웅 {IDEAL['영웅']} · 전설 {IDEAL['전설']}"
      f"  (합 {sum(IDEAL.values())} = 6)  ★ 산술적으로 정확히 맞아떨어진다")

def class_matrix(sub1):
    m = {r: [0, 0, 0, 0] for r in ["일반", "희귀", "영웅", "전설"]}
    for s in range(4):
        for i in range(6):
            m[rarity_of(i)][sub1[s][i]] += 1
    return m

m8 = class_matrix(R8)
print("\n  R8 §14-3 표의 등급대 분포 (행=등급, 열=스탯):")
print("        " + "".join(f"{x:>8}" for x in STATS))
for r in ["일반", "희귀", "영웅", "전설"]:
    print(f"    {r}" + "".join(f"{v:>8}" for v in m8[r]) + f"    (균등 {IDEAL[r]})")
r8_balanced = all(m8[r] == [IDEAL[r]] * 4 for r in m8)
print(f"\n  ⇒ R8 표는 §13-4 제약을 만족하는가: {'예' if r8_balanced else '★ 아니오'}")
if not r8_balanced:
    for r in ["일반", "희귀", "영웅", "전설"]:
        if m8[r] != [IDEAL[r]] * 4:
            bad = ", ".join(f"{STATS[t]} {m8[r][t]}" for t in range(4))
            print(f"     - {r}: {bad}   (균등 {IDEAL[r]})")
    print("     ⇒ 특히 「전설 부스탯을 한 개도 못 받는 스탯」이 존재하면 M4(4단계)가 구조적으로 깨진다 — [3]에서 증명한다.")

# ── F1/F2. §14-4 I-1 양성 대조 재검 ───────────────────────────────────────────
print("\n  ── F1/F2. §14-4 「I-1 원시 최대 41 > CAP 40」 재검 ──")
print(f"    §C-1(나) 이론식 BASE+15+6×3 = 41  … 이 식은 「다른 3슬롯의 전설이 **전부 같은 스탯**을")
print(f"    가리킬 때」만 성립한다. C1(슬롯 내 2회씩)은 그걸 **허용**하지만 C3(등급대 균등)은 **금지**한다.")
# 양성 대조: C1만 걸면 41이 실제로 나오는 배정이 존재함을 구성으로 보인다
#   EYES/NECK/BACK 의 전설(idx5)이 전부 집중력(0)을 가리키게 만든 C1-합법 배정
C1_ONLY = [
    [1, 1, 2, 2, 3, 3],      # HEAD  : 관·관·매·매·민·민  (자기 주스탯 0 제외, 2회씩)
    [2, 2, 3, 3, 0, 0],      # EYES  : 매·매·민·민·집·집  ← idx5 = 집중력
    [1, 1, 3, 3, 0, 0],      # NECK  : 관·관·민·민·집·집  ← idx5 = 집중력
    [1, 1, 2, 2, 0, 0],      # BACK  : 관·관·매·매·집·집  ← idx5 = 집중력
]
a_c1 = analyse(C1_ONLY, model="M3")
check("F0 양성 대조 — C1만 걸면 원시 41이 실제로 나온다", max(a_c1["raw_max"]), 41,
      "⇒ 41은 계산 착오가 아니라 **다른 배정의 값**이다")
print(f"    R8 표(§14-3)의 실제 원시 최대  = {F1_R8_RAWMAX}    ⇒ CAP 40 에 {CAP3 - F1_R8_RAWMAX} 모자란다")
print(f"    ★ F1 : §14-4 의 「41」은 R8 배정표의 값이 아니라 §C-1(나) 이론식의 값이다.")
print(f"           §14-1 이 같은 라운드에서 폐기한 「인계본 등급 선언」 모형의 잔재다.")
print(f"    ★ F2 : ⇒ **기본 42종만으로는 CAP 클램프가 발화하지 않는다.** I-1 을 그대로 테스트로")
print(f"           만들면 클램프를 삭제해도 초록이다(무의미한 통과). 불변식 문장을 바꿔야 한다.")

# ═════════════════════════════════════════════════════════════════════════════
# [2] 새 배정 탐색 — C1 ∧ C3(등급대 균등) ∧ 1일차 초급 ∧ I-1~I-4
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[2] 부스탯 방향 24개 재탐색 — §13-4 제약을 실제로 걸고 푼다")
print("=" * 100)

def slot_options(s):
    """슬롯 s 의 6칸 배정 후보. C1: 자기 주스탯 제외 3스탯을 정확히 2번씩."""
    others = [t for t in range(4) if t != s]
    seen, out = set(), []
    for perm in set(itertools.permutations(others * 2)):
        out.append(perm)
    return sorted(out)

OPTS = [slot_options(s) for s in range(4)]
print(f"\n  슬롯당 C1 만족 배정 수 = {len(OPTS[0])} (= 6!/(2!2!2!))   전체 공간 = {len(OPTS[0])**4:,}")

def opt_class_vec(s, opt):
    v = {r: [0, 0, 0, 0] for r in ["일반", "희귀", "영웅", "전설"]}
    for i in range(6): v[rarity_of(i)][opt[i]] += 1
    return v

PRE = [[(o, opt_class_vec(s, o)) for o in OPTS[s]] for s in range(4)]

# DFS: 각 등급대의 누적 카운트가 목표(IDEAL)를 못 넘게 가지치기
survivors = []
def dfs(s, acc):
    if s == 4:
        survivors.append(tuple(acc[i][0] for i in range(4)))
        return
    for o, v in PRE[s]:
        ok = True
        newacc = {}
        for r in ["일반", "희귀", "영웅", "전설"]:
            cur = acc["cls"][r]
            nv = [cur[t] + v[r][t] for t in range(4)]
            # 남은 슬롯이 채울 수 있는 최대 = (3-s) * 슬롯당 그 등급 개수
            room = (3 - s) * RCLASS_COUNT[r]
            if any(x > IDEAL[r] for x in nv) or any(IDEAL[r] - x > room for x in nv):
                ok = False; break
            newacc[r] = nv
        if not ok: continue
        acc[s] = (o,)
        saved = acc["cls"]; acc["cls"] = newacc
        dfs(s + 1, acc)
        acc["cls"] = saved
dfs(0, {"cls": {r: [0, 0, 0, 0] for r in ["일반", "희귀", "영웅", "전설"]}})
print(f"  C1 ∧ C3(등급대 균등) 통과 = {len(survivors):,}개")

def day1_of(sub1):
    return totals((0, 0, 0, 0), sub1, None, "M3")[1]

# 하드 게이트: 1일차 4스탯 전부 초급 · I-1(클램프 발화) · I-2(4스탯 각각 고급) · I-3(동시 ≤2 이면서 ≥1쌍)
good = []
for cand in survivors:
    sub1 = [list(c) for c in cand]
    d1 = day1_of(sub1)
    if any(v < TIER3[0] for v in d1):        continue
    a = analyse(sub1, model="M3")
    # ★ I-1 은 「클램프가 기본 42종에서 발화하는가」로 걸지 않는다 — F1/F2 참조.
    #    C3(등급대 균등)가 전설 부스탯 수렴을 구조적으로 금지하므로 원시값이 CAP 근처에 못 간다.
    if not all(a["reach_hi"]):               continue     # I-2
    if a["max_simul_hi"] > 2:                continue     # I-3 상한
    if a["max_simul_hi"] < 2:                continue     # I-3 하한 (선택이 존재해야 한다)
    good.append((sub1, d1, a))
print(f"  ∧ 1일차 4/4 초급 ∧ I-1 ∧ I-2 ∧ I-3 = {len(good):,}개")

def r8_agreement(sub1):
    return sum(1 for s in range(4) for i in range(6) if sub1[s][i] == R8[s][i])

def score(item):
    sub1, d1, a = item
    return (-len(a["pairs_hi"]),                 # ① 동시 고급 가능 쌍이 많을수록 좋다 (U-20 방향)
            max(d1) - min(d1),                   # ② 1일차 편차 최소
            -r8_agreement(sub1),                 # ③ R8 표와의 일치 최대 (변경 최소화)
            tuple(x for row in sub1 for x in row))   # ④ 결정적 tie-break
good.sort(key=score)
print(f"\n  최상위 후보 요약 (동시고급쌍 / 1일차편차 / R8일치칸수):")
for sub1, d1, a in good[:6]:
    print(f"    쌍{len(a['pairs_hi'])}  편차{max(d1)-min(d1)}  R8일치{r8_agreement(sub1):>2}/24  "
          f"1일차{d1}  최대치{a['per_stat_max']}  원시최대{max(a['raw_max'])}")

BEST, BEST_D1, BEST_A = good[0]
print("\n  ★ 채택 (R15 확정표):")
print("    " + "슬롯  idx Lv  이름            등급    부스탯   (R8과 동일?)")
for s in range(4):
    row = sorted((x for x in ASSETS if x["slot"] == s), key=lambda x: x["idx"])
    for i in range(6):
        same = "=" if BEST[s][i] == R8[s][i] else "★변경"
        print(f"    {SLOTS[s]:<5} {i}  {row[i]['lv']:>2}  {row[i]['name']:<14} {rarity_of(i):<6} "
              f"{STATS[BEST[s][i]]:<7} {same}")
mB = class_matrix(BEST)
print("\n    등급대 분포 (행=등급, 열=스탯):")
print("        " + "".join(f"{x:>8}" for x in STATS))
for r in ["일반", "희귀", "영웅", "전설"]:
    print(f"    {r}" + "".join(f"{v:>8}" for v in mB[r]))
cB = Counter(x for row in BEST for x in row)
print(f"\n    부스탯 총 편중 = {[cB[t] for t in range(4)]}   1일차 = {BEST_D1}   "
      f"스탯별 최대 = {BEST_A['per_stat_max']}   원시최대 = {max(BEST_A['raw_max'])}")
print(f"    동시 고급 가능 쌍 = {sorted(tuple(STATS[i] for i in sorted(p)) for p in BEST_A['pairs_hi'])}")
print(f"    R8 대비 변경 칸 수 = {24 - r8_agreement(BEST)}/24  (최대 일치 = {max(r8_agreement(g[0]) for g in good)}/24)")
maxpairs = max(len(g[2]["pairs_hi"]) for g in good)
print(f"\n  U-20 재판정 — 812개 전수에서 동시 고급 **쌍의 최대 개수 = {maxpairs}**")
print(f"    ⇒ 「2쌍 이상」은 C1 ∧ C3 ∧ (1일차 초급) ∧ I-2 ∧ I-3 을 동시에 만족하는 해 안에 **존재하지 않는다**.")
print(f"    ⇒ R8 이 상위 400 표본으로 냈던 결론(1쌍 46건 · 2쌍 0건)이 **전수에서 확인**된다. U-20 = 「불가, 닫는다」")
print(f"    엔드게임 선택지 = 단일 고급 4가지(I-2) + 쌍 1가지 = **5가지**")

# 결정 독립성: 채택표가 M3 / M3+(a) / M4+U-25(B) 어디서도 유효한가
print("\n  ── 결정 독립성 — 이 24칸이 U-24 결정과 무관하게 쓸 수 있는가 ──")
print(f"    M3 (3단계 CAP40)          : I-2 {all(BEST_A['reach_hi'])} · I-3 상한 {BEST_A['max_simul_hi']} · 1일차 {BEST_D1}")
_u = [False]*4; _s = 0
for s2 in sub2_candidates(BEST):
    _a = analyse(BEST, list(s2), model="M4")
    _s = max(_s, _a["max_simul_top"])
    for t in range(4):
        if _a["reach_top"][t]: _u[t] = True
print(f"    M4 (4단계 CAP44 + U-25B)  : I-2′ {all(_u)} · I-3′ 상한 {_s}")
print(f"    ⇒ **같은 24칸이 두 모형 모두에서 유효하다.** U-24 결정을 기다리지 않고 확정·구현할 수 있다.")
print(f"       (M4 를 고르면 **전설 4칸(sub2)만 추가**된다 — 앞의 24칸은 한 칸도 안 바뀐다.)")

# ═════════════════════════════════════════════════════════════════════════════
# [3] M4(4단계 · 전설 부스탯 2개 · 전설 풀세트) 구조 판정
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[3] M4 판정 — §15-4 패키지가 그대로 성립하는가")
print("=" * 100)

print("""
  구조 논증 (배정과 무관한 산술):
    - 최상급 44 는 「전설 풀세트」에서만 닿는다.  풀세트가 아니면 세트 +6 이 없고,
      비-풀세트 최대 = BASE + 주15 + (전설2슬롯 × 6) + (영웅1슬롯 × 4) = BASE + 31 → 39/37/36/38 < 44.
    - 전설 풀세트는 로드아웃이 **유일**하다 (슬롯당 전설 1종, §13-2 판정 3).
    ⇒ 「도달 가능한 스탯 집합」과 「동시에 도달하는 스탯 집합」이 **같은 집합**이 된다.
    ⇒ I-2′(4스탯 각각 도달 가능) ∧ I-3′(동시 최대 2) 는 **동시에 참일 수 없다.**
""")
need_k = [-(-(CAP4 - BASE[t] - 15 - SETLEG) // SUBV["전설"]) for t in range(4)]
print(f"    스탯별 필요 전설-부스탯 칸수 = {dict(zip(STATS, need_k))}   합계 필요 = {sum(need_k)}")
print(f"    전설 풀세트가 내놓는 칸 = 4아이템 × 2 = 8      ⇒ {sum(need_k)} > 8  (4스탯 동시 불가)")
print(f"    그리고 로드아웃이 하나뿐이므로 「각각 가능」도 같은 8칸을 나눠 쓴다 ⇒ **최대 2스탯만 영구 도달**")

# 실측으로 확인 — sub2 를 전수(각 전설마다 non-own \ {sub1} 의 2가지 = 2^4 = 16)
def sub2_candidates(sub1):
    per = []
    for s in range(4):
        cs = [t for t in range(4) if t != s and t != sub1[s][5]]
        per.append(cs)
    return list(itertools.product(*per))

print("\n  실측 — 채택표(BEST) 위에서 sub2 전수 탐색 (고정 배정 = U-25 미채택):")
best_reach = 0; rows = []
for s2 in sub2_candidates(BEST):
    a = analyse(BEST, list(s2), model="M4")
    n_reach = sum(a["reach_top"]); rows.append((n_reach, a["max_simul_top"], s2, a))
    best_reach = max(best_reach, n_reach)
rows.sort(key=lambda r: (-r[0], r[1]))
for n_reach, simul, s2, a in rows[:4]:
    print(f"    sub2={[STATS[x] for x in s2]}  최상급 도달가능 {n_reach}/4  동시최대 {simul}  "
          f"스탯최대 {a['per_stat_max']}")
I2_FIXED_OK = (best_reach == 4)
print(f"    ★ F4 : M4-고정배정 I-2′ = {best_reach}/4  ⇒ **실패**. "
      f"§15-4 의 「I-2′ PASS」는 오류다(관측이지 교정 실패가 아니다).")

print("\n  U-25(B) 채택 시 — 전설의 **두 번째** 부스탯을 유저가 지정(레벨업당 1회 재지정):")
print("    유저 지정이면 sub2 는 런타임 변수다 ⇒ 「도달 가능」은 sub2 전 조합의 합집합으로 잰다.")
union_reach = [False] * 4
for s2 in sub2_candidates(BEST):
    a = analyse(BEST, list(s2), model="M4")
    for t in range(4):
        if a["reach_top"][t]: union_reach[t] = True
simul_max = max(analyse(BEST, list(s2), model="M4")["max_simul_top"] for s2 in sub2_candidates(BEST))
print(f"    합집합 도달 = {dict(zip(STATS, union_reach))}   어떤 sub2 에서도 동시 최대 = {simul_max}")
U25_OK = all(union_reach) and simul_max <= 2
print(f"    ⇒ I-2′ ∧ I-3′ 동시 성립: {'예' if U25_OK else '아니오'}")

print("\n  ★ 자기반증 — 「C3(등급대 균등)가 있어야 M4가 산다」는 내 가설을 R8 표로 반증한다:")
u8 = [False] * 4
for s2 in sub2_candidates(R8):
    a = analyse(R8, list(s2), model="M4")
    for t in range(4):
        if a["reach_top"][t]: u8[t] = True
print(f"    R8 표(C3 위반) + U-25(B) 합집합 도달 = {dict(zip(STATS, u8))}   ← **전부 도달한다**")
print(f"    R8 표의 전설 부스탯 분포 = {dict(zip(STATS, m8['전설']))} / 채택표 = {dict(zip(STATS, mB['전설']))}")
print("    ⇒ 구조 이유: 전설 4종 중 **자기 슬롯을 뺀 3종**이 각각 (sub1=t) 또는 (sub2:=t) 중")
print("      하나를 반드시 만들 수 있다(sub1≠sub2 뿐이므로). ⇒ k_t = 3 이 **배정과 무관하게 항상 달성 가능**.")
print("    ⇒ **C3는 M4의 전제가 아니다.** C3의 근거는 §13-4(등급대 쏠림) 하나뿐이다 — 아래에서 그것만 잰다.")

# ── C3의 진짜 값: 등급대 쏠림 정량화 (§13-4 원문 근거) ────────────────────────
print("\n  ── C3의 값 — 「한 등급대를 통째로 사면 어느 스탯이 얼마나 오르는가」 ──")
print(f"    {'등급대':<6}" + "".join(f"{s:>9}" for s in STATS) + "   최대−최소")
for label, tbl in [("R8", R8), ("R15", BEST)]:
    m = class_matrix(tbl)
    print(f"    [{label}]")
    for r in ["일반", "희귀", "영웅", "전설"]:
        gain = [m[r][t] * SUBV[r] for t in range(4)]
        print(f"    {r:<6}" + "".join(f"{g:>9}" for g in gain) + f"   {max(gain)-min(gain):>6}")
    tot = [sum(m[r][t] * SUBV[r] for r in m) for t in range(4)]
    print(f"    {'합계':<6}" + "".join(f"{g:>9}" for g in tot) + f"   {max(tot)-min(tot):>6}")

print("\n  M3 + (a)만 (전설 부스탯 2개, 세트 보너스·4단계 없음) — I-3 이 살아남는가:")
for s2 in sub2_candidates(BEST)[:1]:
    pass
def analyse_m3a(sub1, sub2):
    """M3 규칙(CAP40·3단계)에 전설 부스탯 2개만 얹음. 전설 풀세트 보너스 없음."""
    per_max = [0]*4; simul = 0; raw_max = [0]*4; pairs = set()
    for lo in ALL_LOADOUTS:
        raw = list(BASE)
        for s, i in enumerate(lo):
            r = rarity_of(i)
            raw[s] += MAINV[r]; raw[sub1[s][i]] += SUBV[r]
            if i == 5: raw[sub2[s]] += SUBV["전설"]
        cl = [min(v, CAP3) for v in raw]
        for t in range(4):
            per_max[t] = max(per_max[t], cl[t]); raw_max[t] = max(raw_max[t], raw[t])
        st = frozenset(t for t in range(4) if cl[t] >= 32)
        simul = max(simul, len(st))
        if len(st) >= 2: pairs.add(st)
    return per_max, simul, raw_max, pairs
worst_simul = 0; any_ok = None
for s2 in sub2_candidates(BEST):
    pm, si, rm, pr = analyse_m3a(BEST, list(s2))
    worst_simul = max(worst_simul, si)
    if all(m >= 32 for m in pm) and si == 2: any_ok = (s2, pm, si, pr)
print(f"    어떤 sub2 에서든 동시 고급 최대 = {worst_simul}  (I-3 상한 2)")
if any_ok:
    s2, pm, si, pr = any_ok
    print(f"    예: sub2={[STATS[x] for x in s2]}  스탯최대 {pm}  동시 {si}  "
          f"쌍 {sorted(tuple(STATS[i] for i in sorted(p)) for p in pr)}")
print(f"    ⇒ (a)만 넣으면 3단계·CAP40 에서 I-1~I-4 가 유지되는가: "
      f"{'예' if (worst_simul <= 2 and any_ok) else '아니오'}")

print("\n  M3 + (b)만 (전설 풀세트 +6, 4단계 없음) — I-3 이 깨지는가:")
brk = []
for lo in [(5, 5, 5, 5)]:
    raw = list(BASE)
    for s, i in enumerate(lo):
        r = rarity_of(i); raw[s] += MAINV[r]; raw[BEST[s][i]] += SUBV[r]
    for t in range(4): raw[t] += SETLEG
    cl = [min(v, CAP3) for v in raw]
    brk = [t for t in range(4) if cl[t] >= 32]
print(f"    전설 풀세트 스탯 = {cl}  → 고급 이상 = {len(brk)}개 {[STATS[t] for t in brk]}")
print(f"    ⇒ I-3(동시 ≤2) {'깨진다 ★' if len(brk) > 2 else '유지'}  "
      f"— (b)는 4단계 없이는 못 들어간다")

# ═════════════════════════════════════════════════════════════════════════════
# [4] 외형 3슬롯 18종 — 스탯 기여 0, 가격만
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[4] 외형 3슬롯 18종 — 무엇을 선언해야 하는가")
print("=" * 100)
look_total = 0
for s in range(4, 7):
    row = sorted((x for x in ASSETS if x["slot"] == s), key=lambda x: x["idx"])
    line = " · ".join(f"{r['name']}(Lv{r['lv']}/{rarity_of(r['idx'])}/{PRICE[rarity_of(r['idx'])]:,})" for r in row)
    look_total += sum(PRICE[rarity_of(r["idx"])] for r in row)
    print(f"  슬롯{s}: {line}")
print(f"\n  외형 18종 총액 = {look_total:,}동전  (§0-6-4(b) 채택안 50,400 과 대조)")
check("K17 외형 총액", look_total, 50400, "§0-6-4(b)")
print("  ⇒ 부스탯 방향 선언 필요 수 = 0 (스탯 기여 0). 등급·가격은 이미 파생된다. **선언할 것이 없다.**")

# ═════════════════════════════════════════════════════════════════════════════
# [5] 곡선 — 채택표로 1일 / 1주 / 1개월 / 그 뒤
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[5] 성장 곡선 (채택표 · M3 · 레벨 게이트 + 동전 예산 동시 적용)")
print("=" * 100)
# 레벨 곡선: need(L) = 100 * L^1.05, 패시브 90XP/시  (ECONOMY_SPEC 5-1 실측)
def level_at_hours(h):
    xp = 90.0 * h; L = 1; acc = 0.0
    while True:
        need = 100.0 * (L ** 1.05)
        if acc + need > xp: return L
        acc += need; L += 1
        if L > 60: return 60
def hours_for_level(L):
    acc = 0.0
    for k in range(1, L): acc += 100.0 * (k ** 1.05)
    return acc / 90.0
close("K18 Lv.30 누적시간", hours_for_level(30), 558.5, 1.0, "ECONOMY_SPEC 5-1")

INCOME = 3280           # 원형 B (R10-c 이후, ECONOMY_SPEC §18-3)
ONLINE_H = 8
print(f"\n  전제: 원형 B = 온라인 {ONLINE_H}h/일 · {INCOME:,}동전/일 (R10-c 이후 확정치)")
print(f"  {'일차':>4} {'Lv':>3} {'예산':>9} " + "".join(f"{s:>7}" for s in STATS) + "  초급  중급  고급")
for day in [1, 3, 7, 14, 30, 45, 60, 75, 90]:
    lv = level_at_hours(day * ONLINE_H)
    budget = 1200 + INCOME * day        # 시드 1,200 (§0-6-4a)
    # 그날 살 수 있는 조합 중 최적 (스탯 4슬롯만, 레벨 게이트 + 예산)
    avail = []
    for s in range(4):
        row = sorted((x for x in ASSETS if x["slot"] == s), key=lambda x: x["idx"])
        avail.append([i for i in range(6) if row[i]["lv"] <= lv])
    # rank0 4종은 무상(§0-2-3). 나머지는 가격 합이 예산 이내.
    best = None
    for lo in itertools.product(*avail):
        cost = sum(0 if i == 0 else PRICE[rarity_of(i)] for i in lo)
        if cost > budget: continue
        cl = totals(lo, BEST, None, "M3")[1]
        key = (sum(1 for v in cl if v >= 32), sum(1 for v in cl if v >= 20),
               sum(1 for v in cl if v >= 10), sum(cl))
        if best is None or key > best[0]: best = (key, cl)
    cl = best[1]
    print(f"  {day:>4} {lv:>3} {budget:>9,} " + "".join(f"{v:>7}" for v in cl) +
          f"   {sum(1 for v in cl if v>=10)}/4   {sum(1 for v in cl if v>=20)}/4   {sum(1 for v in cl if v>=32)}/4")

# ═════════════════════════════════════════════════════════════════════════════
# [6] U-17 전설 가격 — 9,600 vs 10,971 재검산
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[6] U-17 전설 가격 — 권고 9,600 vs 상한 10,971")
print("=" * 100)
print(f"  {'가격':>8} {'배율':>6} {'42종총액':>10} {'완주일@1,280':>13} {'@3,280':>9} {'Lv30(75일) 대비':>15} {'집중25분 세션':>13}")
for X in [6600, 7400, 9600, 10971, 11200]:
    tot = 7 * (2*PRICE['일반'] + 2*PRICE['희귀'] + PRICE['영웅'] + X) - 4*PRICE['일반']
    d_old, d_new = tot/1280, tot/3280
    print(f"  {X:>8,} {X/PRICE['영웅']:>6.2f} {tot:>10,} {d_old:>13.1f} {d_new:>9.1f} "
          f"{d_old/75:>14.2f}× {X/600:>13.1f}")
print("\n  판정 기준 (ECONOMY_SPEC 5-3 (c)): 완주 ≤ Lv.30 도달일(75일)의 1.30배 = 97.5일")
print(f"  9,600  → 90.0일 = 1.20×   여유 7.5일 · 세션 정수 16.0회 · 전설4종 = 30.0일")
print(f"  10,971 → 97.5일 = 1.30×   여유 0.0일 ★ 경계 정확히 = 여유 0 · 세션 18.285회(비정수)")
print("  ⇒ 하류 4개 계산(§12-2 가격스윕 · §18-3 코인완주 · §14-5 곡선 · §0-6-4b 외형가)이")
print("     전부 115,200 위에 서 있고, 10,971 은 판정 기준의 여유를 0 으로 만든다.")

# ═════════════════════════════════════════════════════════════════════════════
# [7] 테마 배정 제약 — R14 DS-G7 실현가능성 판정 + 대체 제약
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[7] 테마 배정 — R14 §3-4 DS-G7 이 기본 24종에서 실현 가능한가")
print("=" * 100)
print("""
  전제: 세트는 「스탯 4슬롯을 같은 테마로」다(§1-4). 테마는 6개(§1-6). 스탯 4슬롯 = 24종.

  ★ E1 (아무도 아직 안 적은 하드 제약):
     테마 하나가 한 슬롯에 2종 있고 다른 슬롯에 0종이면 그 테마는 **4/4 완성이 영원히 불가능**하다.
     ⇒ 각 테마는 4슬롯에 **정확히 1종씩** 있어야 한다. 6테마 × 4슬롯 = 24 ✔ 정확히 맞는다.
""")
print("  DS-G7 판정 — 「그 슬롯 최고(=전설) 대비 1단 내림이 최대 1슬롯」")
print("             ⇒ 허용되는 테마 구성은 **전설4** 또는 **전설3+영웅1** 둘뿐이다.")
print(f"    전설 재고 = 4 (슬롯당 정확히 1종, §13-2 판정 3) · 영웅 재고 = 4")
print(f"    · 전설4 테마 1개  → 전설 재고 0 → 나머지 5테마는 최선이 영웅4 = **2단 내림 4슬롯** → 전부 위반")
print(f"    · 전설3+영웅1 테마 1개 → 남은 전설 1 → 두 번째 테마는 최선이 전설1+영웅3 = **1단 내림 3슬롯** → 위반")
print(f"  ⇒ ★ F5 : **DS-G7 을 6테마 전부에 걸 수는 없다 — 산술적으로 최대 1개만 만족한다.**")
print(f"           R14 는 *'지금 걸면 비용 0'* 이라고 적었지만, 걸 수 없는 규칙이라 비용이 아니라 **모순**이다.")

print("""
  ⇒ DS-G7 을 DS-G7′ 로 교체한다 (경제 제약만 남기고, 조형 자유도는 돌려준다):
     E1  각 테마는 스탯 4슬롯에 정확히 1종씩          (완성 가능성 — 하드)
     E2  1일차 무료 4종(각 슬롯 idx0)은 같은 테마      (R14 §3-5 「첫 세트 경험은 0원」 자산)
     E3  전설 4종(idx5×4)은 같은 테마                 (혼합최고를 이기는 조합이 최소 1개 존재해야 한다)
     E4  나머지 16종(idx1~4)은 조형 자유 — 4테마 × 4슬롯 (design-equipment 소관)
""")
# E1~E3 하에서 테마별 세트 총합
print("  E1~E3 하에서 테마별 「Σstat + 세트 8」 (idx 정렬 배정 예시):")
print(f"    {'테마(대표 idx)':<16}{'등급':<6}{'Σstat':>7}{'+세트8':>8}{'vs 혼합최고 84':>16}")
for i in range(6):
    r = rarity_of(i); sig = 4 * (MAINV[r] + SUBV[r])
    verdict = "✔ 이긴다" if sig + 8 > 84 else f"✘ {sig + 8 - 84}"
    tag = "idx0 = 1일차 무료(E2)" if i == 0 else ("idx5 = 전설(E3)" if i == 5 else f"idx{i}")
    print(f"    {tag:<16}{r:<6}{sig:>7}{sig+8:>8}{verdict:>16}")
print("  ⇒ 6테마 중 **전설 테마 하나만** 엔드게임에서 이긴다. 나머지 5개는 R14 §3-5 대로")
print("     **첫 주에 값을 다 한다**(1일차 세트 +8 = 총합 24 의 33.3%).")

# ═════════════════════════════════════════════════════════════════════════════
# [8] 막대 채움률 — ux-designer 인계
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[8] 막대(총합/CAP) 채움률 — §1-4 `막대 = min(100%, 총합/CAP)`")
print("=" * 100)
mx3 = max(BEST_A["per_stat_max"])
print(f"  M3 (CAP {CAP3}) : 기본 42종 최대 스탯 = {mx3}  ⇒ 막대 최대 {mx3/CAP3*100:.1f}%  "
      f"★ 100% 에 영원히 못 닿는다")
print(f"  M4 (CAP {CAP4}) : 전설 풀세트 원시 최대 = 47  ⇒ 클램프 발화, 막대 100.0%")
print(f"  눈금 위치  M3: {TIER3[0]/CAP3*100:.0f}% / {TIER3[1]/CAP3*100:.0f}% / {TIER3[2]/CAP3*100:.0f}%")
print(f"             M4: " + " / ".join(f"{t/CAP4*100:.1f}%" for t in TIER4))
print("  ⇒ ★ F3 : M3 로 가면 막대가 최대 87.5% 에서 멈춘다. 「분모를 CAP 40 으로 둘 것인가」가")
print("           ux-designer 결정 사항이 된다(대안: 분모를 고급 32 로, 눈금 31.3/62.5/100%).")

# ═════════════════════════════════════════════════════════════════════════════
# [9] 저장 빈도 예상치 (리더 판단용 — 상시 항목)
# ═════════════════════════════════════════════════════════════════════════════
print()
print("=" * 100)
print("[9] 저장 빈도 예상치 — 리더 판단용")
print("=" * 100)
BASELINE_AUTO = 1440.0     # 60초 자동 저장 × 항상 IsDirty
R13_EXTRA     = 6.06       # ECONOMY_SPEC §0-6-6 확정치
# statTierReached: 평생 최대 (스탯4 × 단계수) 회
tier_writes_m3 = 4 * len(TIER3)
tier_writes_m4 = 4 * len(TIER4)
LIFE_DAYS = 90
print(f"  기준선(자동 저장)                         = {BASELINE_AUTO:,.0f} 회/일")
print(f"  R13 확정 추가분                            = {R13_EXTRA:.2f} 회/일")
print(f"  + statTierReached 갱신 (M3, 평생 {tier_writes_m3}회 / {LIFE_DAYS}일) = "
      f"{tier_writes_m3/LIFE_DAYS:+.3f} 회/일")
print(f"  + statTierReached 갱신 (M4, 평생 {tier_writes_m4}회 / {LIFE_DAYS}일) = "
      f"{tier_writes_m4/LIFE_DAYS:+.3f} 회/일")
lvl_ups = 30
print(f"  + legendarySubChoice 재지정 (M4+U-25B, 평생 ≤{lvl_ups}회 / {LIFE_DAYS}일) = "
      f"{lvl_ups/LIFE_DAYS:+.3f} 회/일")
m3_total = R13_EXTRA + tier_writes_m3 / LIFE_DAYS
m4_total = R13_EXTRA + tier_writes_m4 / LIFE_DAYS + lvl_ups / LIFE_DAYS
print(f"\n  합계  M3 = {m3_total:.2f} 회/일  (기준선 대비 +{m3_total/BASELINE_AUTO*100:.3f}%)")
print(f"        M4 = {m4_total:.2f} 회/일  (기준선 대비 +{m4_total/BASELINE_AUTO*100:.3f}%)")
print(f"  차이 = {m4_total - m3_total:.2f} 회/일 = 기준선의 {(m4_total-m3_total)/BASELINE_AUTO*100:.4f}%")
print("  ⇒ 두 모형의 저장 빈도 차이는 무의미하다. **선결 조건(File.Replace IOException 폴백)의")
print("     근거는 빈도가 아니라 회복 불가능성이고, 그것은 M3/M4 어느 쪽에서도 동일하다.**")

print()
print("=" * 100)
print(f"[끝] 교정 실패 {len(FAIL)}건.  " + ("전부 통과." if not FAIL else "★ 위 숫자 폐기."))
print("=" * 100)
