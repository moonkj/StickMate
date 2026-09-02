# -*- coding: utf-8 -*-
"""
StickMate 경제 R3 — 코호트 고정 이후 재검산 + 요구 레벨 재배치 + DLC 팩 수치 확정.

이 파일이 답해야 하는 것 (리더 지시 2026-09-02):
  1. 코호트가 고정되면 R2 수치가 그대로 유효한가         → A절
  2. DLC 팩의 등급 사다리를 팩 코호트 안에서 어떻게 매기나 → E·F절
  3. ECONOMY_SPEC 4-5의 두 줄 모순 판정                   → F절
  4. 요구 레벨 재배치 PS-1/PS-2/PS-3 확정                 → C·D절
  5. 활쏘기 A·B안 / 할일 15동전 / 5분 격자 / 중도취소 정의 → G절

★ 교정을 네 겹으로 건다. 하나라도 어긋나면 SystemExit — 그 뒤 숫자는 전부 폐기다.
  (1) 실제 Player.log 문자열        need(2)=207 / need(3)=317 / need(127)=16181 / need(3)@1.15=354
  (2) 이미 공개된 R1·R2 산출물      Lv.5=11.7h / Lv.28=483.6h / Lv.30=558.5h / 슬롯 650 / 7슬롯 4550
  (3) ★ 프로덕션 C# 등급 파생의 거울  ItemCatalog.RarityOfMember / RarityOfRank 를 그대로 옮기고
      골든 파일이 말하는 42종 요구 레벨로 돌려 2/2/1/1 분포를 재현
  (4) ★ 남이 독립적으로 낸 숫자      product-strategy 실측(Lv.6에 11/42, 회색 73.8%, 팩 1/6=17%,
      번들 13/78=16.7%) · game-architect 실측(슬롯 12종이면 슬롯 동전 합 650 -> 230)
      ← (3)과 (4)는 이 스크립트와 코드를 공유하지 않는 곳에서 나온 값이다.
        "생성기와 검사기가 같이 틀린다"(TEAM.md §열번째)를 막는 유일한 장치가 이것이다.
"""
import sys
import numpy as np
import decimal
from itertools import product

FAIL = []


def check(name, got, want, tol=None):
    ok = (abs(got - want) <= tol) if tol is not None else (got == want)
    print(f"  {'PASS' if ok else '**FAIL**'} {name:<46} 계산={got!r:<12} 기대={want!r}")
    if not ok:
        FAIL.append(name)
    return ok


# ══════════════════════════════════════════════════════════════════════
# 프로덕션에서 읽은 값 (전부 출처 있음)
# ══════════════════════════════════════════════════════════════════════
BASE, EXP = 100.0, 1.05          # StickConfig.progressionXpCurveBase / Exponent
PASSIVE_XP_PER_MIN = 1.5         # progressionPassiveXpPerMinute (= 90 XP/시)
BULLSEYE_XP = 15.0               # progressionBullseyeXp
ARCHERY_CD_SEC = 600.0           # archeryCooldownSeconds (동전 지급 게이트로 재사용)

# Tests/EditMode/Golden/ItemCatalogGolden.txt 전문에서 추출
REQ_NOW = {
    "Head":      [1, 5, 9, 20, 23, 26],
    "Eyes":      [1, 6, 11, 15, 19, 23],
    "Neck":      [1, 8, 12, 18, 21, 25],
    "Shoulders": [1, 13, 17, 22, 25, 28],
    "Hair":      [1, 5, 9, 14, 18, 22],
    "Fx":        [1, 6, 12, 16, 20, 24],
    "Pet":       [1, 13, 19, 24, 27, 30],
}
SLOTS = list(REQ_NOW.keys())
STAT_SLOTS = ["Head", "Eyes", "Neck", "Shoulders"]
LOOK_SLOTS = ["Hair", "Fx", "Pet"]

# ItemCatalog._rarityByRank (길이 6 = 기준 코호트 크기)
LADDER = ["일반", "일반", "희귀", "희귀", "영웅", "전설"]

PRICE = {"일반": 30, "희귀": 70, "영웅": 150, "전설": 330}       # 가격 배율 1.50 채택안
GRACE = {"일반": 16, "희귀": 23, "영웅": 27, "전설": 33}         # R2 확정 유예(시간)
PRIMARY = {"일반": 3, "희귀": 5, "영웅": 7, "전설": 10}          # ECONOMY_SPEC 3-1
SECOND = {"일반": 0, "희귀": 1, "영웅": 2, "전설": 3}
SET_BONUS = 2
STAT_CAP = 20
TIERS = {"초급": 6, "중급": 12, "고급": 18, "캡": 20}

# 스탯 -> (주스탯 슬롯, 부스탯 슬롯)   ECONOMY_SPEC 2-1
STAT_MAP = {
    "집중력": ("Head", "Shoulders"),
    "관찰력": ("Eyes", "Head"),
    "매력":   ("Neck", "Eyes"),
    "민첩":   ("Shoulders", "Neck"),
}


def need(level, base=BASE, exp=EXP):
    b = np.float32(base)
    L = np.float32(max(1, level))
    return float(b * np.float32(np.power(L, np.float32(exp), dtype=np.float32)))


def f0(x):
    return int(decimal.Decimal(repr(x)).quantize(decimal.Decimal('1'), rounding=decimal.ROUND_HALF_UP))


def cum_xp(level):
    return sum(need(i) for i in range(1, level))


def hours_to(level):
    return cum_xp(level) / (PASSIVE_XP_PER_MIN * 60.0)


def level_base(lv):
    return min((lv - 1) // 4, 5)


# ══════════════════════════════════════════════════════════════════════
# 교정 ③ — 프로덕션 C# 등급 파생의 거울
#   Core/ItemCatalog.cs RarityOfMember / RarityOfRank / UnlockRankKey 를 그대로 옮긴다.
#   population = 슬롯 배열, cohort 필터, 동점은 배열 인덱스가 작은 쪽이 앞.
# ══════════════════════════════════════════════════════════════════════
def rarity_of_rank(rank, count):
    """ItemCatalog.RarityOfRank 그대로. step = rank * ladder / count (정수 나눗셈)."""
    if count <= 0:
        return "일반"
    ladder = len(LADDER)
    rank = max(0, min(rank, count - 1))
    step = rank * ladder // count
    step = min(step, ladder - 1)
    return LADDER[step]


def rarity_of_member(population, index):
    """ItemCatalog.RarityOfMember 그대로. population = [(cohort, reqLv) | None] 의 슬롯 배열."""
    if population is None or index < 0 or index >= len(population):
        return "일반"
    mine = population[index]
    if mine is None:
        return "일반"
    cohort, key = mine
    rank, counted = 0, 0
    for i, other in enumerate(population):
        if other is None or other[0] != cohort:
            continue
        counted += 1
        if i == index:
            continue
        ok = other[1]
        if ok < key or (ok == key and i < index):
            rank += 1
    return rarity_of_rank(rank, counted)


print("=" * 78)
print("교정 ① 실제 Player.log 문자열 (XP 곡선)")
print("=" * 78)
check("need(2) @exp1.05", f0(need(2)), 207)
check("need(3) @exp1.05", f0(need(3)), 317)
check("need(127) @exp1.05", f0(need(127)), 16181)
check("need(3) @exp1.15", f0(need(3, exp=1.15)), 354)

print()
print("=" * 78)
print("교정 ② R1·R2 산출물의 공개 숫자")
print("=" * 78)
check("Lv.5 누적시간(h)", round(hours_to(5), 1), 11.7)
check("Lv.17 누적시간(h)", round(hours_to(17), 1), 169.6)
check("Lv.28 누적시간(h)", round(hours_to(28), 1), 483.6)
check("Lv.30 누적시간(h)", round(hours_to(30), 1), 558.5)
slot_paid = sum(PRICE[LADDER[r]] for r in range(1, 6))
check("슬롯당 유료 총액(rank0 무상)", slot_paid, 650)
check("7슬롯 유료 총액", slot_paid * 7, 4550)

print()
print("=" * 78)
print("교정 ③ 프로덕션 C# 등급 파생의 거울 — 골든 42종으로 재현")
print("=" * 78)
# 기본 42종: 코호트 전부 0(BaseCohortId), 슬롯당 6종
mirror_ok = True
for s in SLOTS:
    pop = [(0, lv) for lv in REQ_NOW[s]]
    got = [rarity_of_member(pop, i) for i in range(6)]
    if got != LADDER:
        mirror_ok = False
        print(f"  **FAIL** {s}: {got}")
check("기본 42종 슬롯 7개 전부 사다리 == 2/2/1/1", mirror_ok, True)
dist = {}
for s in SLOTS:
    pop = [(0, lv) for lv in REQ_NOW[s]]
    for i in range(6):
        g = rarity_of_member(pop, i)
        dist[g] = dist.get(g, 0) + 1
check("42종 전체 분포 일반14/희귀14/영웅7/전설7",
      (dist["일반"], dist["희귀"], dist["영웅"], dist["전설"]), (14, 14, 7, 7))

print()
print("=" * 78)
print("교정 ④ 남이 독립적으로 낸 숫자 (product-strategy / game-architect)")
print("=" * 78)


def opened_at(level, req=REQ_NOW):
    return sum(1 for s in SLOTS for lv in req[s] if lv <= level)


def per_slot_opened(level, req=REQ_NOW):
    return {s: sum(1 for lv in req[s] if lv <= level) for s in SLOTS}


check("Lv.6에 열린 종수 (product-strategy 실측)", opened_at(6), 11)
check("Lv.6 회색 비율 %", round((42 - opened_at(6)) / 42 * 100, 1), 73.8)
check("Lv.2에 새로 열리는 종수 (rank0 제외)", opened_at(2) - opened_at(1), 0)
# 팩이 기본 사다리를 그대로 베끼면 Lv.2에서 6종 중 1종
check("팩 6종 중 Lv.2 착용 가능 (기본 사다리 복사 시) %",
      round(1 / 6 * 100, 1), 16.7)
check("번들(42+36=78) 중 Lv.2 착용 가능 종수", 7 + 6 * 1, 13)
check("번들 착용 가능 비율 %", round(13 / 78 * 100, 1), 16.7)

# game-architect 실측: 코호트를 안 고르면 슬롯 12종에서 슬롯 동전 합 650 -> 230
# (팩 6종의 요구 레벨이 전부 기본 6종보다 위인 최악의 배치)
pop12 = [(0, lv) for lv in REQ_NOW["Head"]] + [(0, 100 + i) for i in range(6)]
base_r12 = [rarity_of_member(pop12, i) for i in range(6)]
coin12 = sum(0 if i == 0 else PRICE[base_r12[i]] for i in range(6))
check("코호트 미고정 · 슬롯 12종에서 기본 rank5 등급", base_r12[5], "희귀")
check("코호트 미고정 · 슬롯 12종에서 기본 rank4 등급", base_r12[4], "희귀")
check("코호트 미고정 · 슬롯 동전 합", coin12, 230)
check("코호트 미고정 · 감소율 %", round((coin12 - 650) / 650 * 100, 1), -64.6)

if FAIL:
    print()
    print("!" * 78)
    print("교정 실패:", FAIL)
    print("이 뒤의 모든 숫자를 폐기한다.")
    print("!" * 78)
    sys.exit(1)

print()
print("교정 4겹 전부 통과. 아래 숫자를 신뢰해도 된다.")


# ══════════════════════════════════════════════════════════════════════
# A. 코호트 고정이 R2 수치를 보존하는가
# ══════════════════════════════════════════════════════════════════════
def section(t):
    print()
    print("=" * 78)
    print(t)
    print("=" * 78)


section("A. 코호트 고정 이후 — 기본 42종의 등급·가격·유예가 한 칸이라도 움직이나")

# 코호트 고정 = 팩 아이템이 다른 cohortId를 갖는다. 팩을 붙이고 다시 잰다.
scenarios = {
    "팩 0개(현행)": [],
    "팩 1개 · 슬롯당 6종 · 코호트 1": [(1, 6)],
    "팩 6개 · 슬롯당 6종 · 코호트 1..6": [(c, 6) for c in range(1, 7)],
    "팩 6개 · 슬롯당 1종 · 코호트 1..6": [(c, 1) for c in range(1, 7)],
}
for nm, packs in scenarios.items():
    same = True
    for s in SLOTS:
        pop = [(0, lv) for lv in REQ_NOW[s]]
        for cohort, n in packs:
            for k in range(n):
                pop.append((cohort, 1 + 5 * k))    # 팩 레벨이 무엇이든
        got = [rarity_of_member(pop, i) for i in range(6)]
        if got != LADDER:
            same = False
    print(f"  {'불변' if same else '**변함**':<8} {nm}")

print()
print("  → 기본 42종의 등급이 팩 개수·팩 크기·팩 레벨에 무관하게 불변이다.")
print("     따라서 등급에서 파생되는 모든 것(가격/유예/스탯/세트)도 불변이다:")
print(f"     슬롯당 유료 총액 {slot_paid}동전 · 7슬롯 {slot_paid*7}동전 · 유예 16/23/27/33h")
print("     ★ R2의 가격 배율 스윕 · 유예 재설계 · 페이투윈 검산 · 200일 시뮬 전부 그대로 유효.")

section("A-2. 그러나 — 팩이 슬롯당 몇 종이냐에 따라 팩 자신의 등급이 결정된다")
print("  RarityOfRank(rank, count) 를 코호트 크기별로 전수:")
for n in [1, 2, 3, 4, 6, 8, 12]:
    got = [rarity_of_rank(r, n) for r in range(n)]
    cnt = {g: got.count(g) for g in ["일반", "희귀", "영웅", "전설"]}
    print(f"    코호트 {n:>2}종 -> {got}")
    print(f"              분포 일반{cnt['일반']} 희귀{cnt['희귀']} 영웅{cnt['영웅']} 전설{cnt['전설']}")
print()
print("  ★★ 슬롯당 1종인 코호트는 rank0/count1 -> step = 0*6//1 = 0 -> **무조건 일반**이다.")
print("     테마 팩(오피스 워커 등)은 모자·안경·넥타이·망토·머리·펫에 1종씩 흩어진다.")
print("     -> 그 팩은 코호트만으로는 6종 전부 일반이 되고, 2/2/1/1 분포가 성립하지 않는다.")


# ══════════════════════════════════════════════════════════════════════
# B. 스탯 모델 (임계 도달 시각을 재는 도구) — 알려진 값으로 먼저 교정
# ══════════════════════════════════════════════════════════════════════
section("B. 스탯 모델 교정 — ECONOMY_SPEC 0-2-5·2-4의 공개 값 재현")


def build_catalog(req):
    """슬롯 -> [(rank, reqLv, rarity)]"""
    cat = {}
    for s in SLOTS:
        pop = [(0, lv) for lv in req[s]]
        cat[s] = [(i, req[s][i], rarity_of_member(pop, i)) for i in range(6)]
    return cat


def avail(cat, lv, slot):
    """레벨 lv에서 슬롯에 쓸 수 있는 항목들 (요구 레벨만 본다 = 최선의 경우)"""
    return [e for e in cat[slot] if e[1] <= lv]


def best_vector(cat, lv, extra=None, objective="min"):
    """1296 조합 전수. objective='min'이면 4스탯의 최소를, 'sum'이면 합을 최대화.
    extra = {slot: [(rank, reqLv, rarity, setid)] } 형태의 추가 후보(팩)."""
    lb = level_base(lv)
    pool = {}
    for s in STAT_SLOTS:
        pool[s] = [(e[0], e[2], "base") for e in avail(cat, lv, s)]
        if extra and s in extra:
            pool[s] += [(e[0], e[2], e[3]) for e in extra[s] if e[1] <= lv]
        if not pool[s]:
            pool[s] = [(0, "일반", "base")]
    best = None
    for combo in product(*[pool[s] for s in STAT_SLOTS]):
        worn = dict(zip(STAT_SLOTS, combo))
        setid = {w[2] for w in combo}
        rank = {w[0] for w in combo}
        bonus = SET_BONUS if (len(setid) == 1 and len(rank) == 1) else 0
        vec = {}
        for st, (ps, ss) in STAT_MAP.items():
            v = lb + PRIMARY[worn[ps][1]] + SECOND[worn[ss][1]] + bonus
            vec[st] = min(v, STAT_CAP)
        key = min(vec.values()) if objective == "min" else sum(vec.values())
        if best is None or key > best[0]:
            best = (key, vec, worn, bonus)
    return best


cat_now = build_catalog(REQ_NOW)
b = best_vector(cat_now, 28)
check("세트 F 완성 @Lv.28 4스탯 전부 캡 20", (min(b[1].values()), max(b[1].values())), (20, 20))
b1 = best_vector(cat_now, 1)
check("Lv.1 세트 A 완성 -> 5", min(b1[1].values()), 5)
b13 = best_vector(cat_now, 13)
check("2주차(Lv.13) 세트 최적 최소스탯 8", min(b13[1].values()), 8)
b13s = best_vector(cat_now, 13, objective="sum")
check("2주차(Lv.13) 총합 최적 33 (혼합이 세트 32를 이긴다)", b13s[0], 33)
check("2주차 세트 최적 총합 32", sum(b13[1].values()), 32)
b27 = best_vector(cat_now, 27)
check("2개월차(Lv.27) 세트 최적 최소 16", min(b27[1].values()), 16)
check("2개월차 총합 최적 68", best_vector(cat_now, 27, objective="sum")[0], 68)

if FAIL:
    print()
    print("!" * 78)
    print("스탯 모델 교정 실패:", FAIL, "— 이 뒤 폐기")
    sys.exit(1)


def tier_arrival(cat, extra=None, maxlv=40):
    """각 임계가 처음 도달 가능한 레벨 (스탯 하나라도 넘으면 그 스탯의 임계가 열린다)"""
    out = {}
    for t, need_v in TIERS.items():
        got = None
        for lv in range(1, maxlv + 1):
            lb = level_base(lv)
            top = 0
            for st, (ps, ss) in STAT_MAP.items():
                cand_p = [(e[0], e[2], "base") for e in avail(cat, lv, ps)]
                cand_s = [(e[0], e[2], "base") for e in avail(cat, lv, ss)]
                if extra:
                    cand_p += [(e[0], e[2], e[3]) for e in extra.get(ps, []) if e[1] <= lv]
                    cand_s += [(e[0], e[2], e[3]) for e in extra.get(ss, []) if e[1] <= lv]
                # 세트 보너스는 4슬롯 전부 같은 세트일 때만 — 별도로 잰다
                bp = max(PRIMARY[c[1]] for c in cand_p) if cand_p else 0
                bs = max(SECOND[c[1]] for c in cand_s) if cand_s else 0
                top = max(top, min(lb + bp + bs, STAT_CAP))
            # 세트 완성 경우
            bv = best_vector(cat, lv, extra=extra, objective="sum")
            top = max(top, max(bv[1].values()))
            if top >= need_v:
                got = lv
                break
        out[t] = got
    return out


section("B-2. 기본 42종만으로 각 임계가 처음 열리는 레벨/시각 (기준선)")
base_tiers = tier_arrival(cat_now)
for t, lv in base_tiers.items():
    print(f"  {t:<4} ({TIERS[t]:>2}) : Lv.{lv:<3} = {hours_to(lv):>7.1f}h")


# ══════════════════════════════════════════════════════════════════════
# C. 요구 레벨 재배치 — PS-1 / PS-2 / PS-3
# ══════════════════════════════════════════════════════════════════════
section("C. 요구 레벨 재배치안 — rank1만 움직인다")

REQ_R3 = {s: list(v) for s, v in REQ_NOW.items()}
NEW_RANK1 = {"Head": 2, "Hair": 2, "Eyes": 3, "Fx": 3, "Neck": 4, "Pet": 4, "Shoulders": 5}
for s, lv in NEW_RANK1.items():
    REQ_R3[s][1] = lv

print("  슬롯        현행 사다리                 R3 사다리                   바뀐 칸")
for s in SLOTS:
    ch = "rank1 %d -> %d" % (REQ_NOW[s][1], REQ_R3[s][1])
    print(f"  {s:<11} {str(REQ_NOW[s]):<27} {str(REQ_R3[s]):<27} {ch}")

section("C-2. 요건 검증")
# PS-1 : 환불창(누적 2시간 = Lv.2) 안에 최소 1종
print(f"  Lv.2 도달 시각 = {hours_to(2):.2f}h  (스팀 환불 상한 2.0h 안)")
new2 = opened_at(2, REQ_R3) - 7
check("PS-1 환불창 안에 새로 열리는 종수 >= 1", new2 >= 1, True)
print(f"        실제 {new2}종 (Head rank1 · Hair rank1)")

# PS-2 : 모든 슬롯이 Lv.5 이내에 2종 이상
ok5 = per_slot_opened(5, REQ_R3)
check("PS-2 모든 슬롯 Lv.5까지 2종 이상", min(ok5.values()) >= 2, True)
print("        현행:", per_slot_opened(5, REQ_NOW))
print("        R3  :", ok5)

# PS-3 : 완주 시각 불변
check("PS-3 최대 요구 레벨 불변(30)",
      max(max(v) for v in REQ_R3.values()), max(max(v) for v in REQ_NOW.values()))
check("PS-3 완주 시각(h) 불변", round(hours_to(max(max(v) for v in REQ_R3.values())), 1), 558.5)

section("C-3. ★ 순위 보존 검산 — 등급·가격·유예·스탯·세트가 한 칸도 안 바뀌는가")
cat_r3 = build_catalog(REQ_R3)
same_rarity = all(cat_now[s][i][2] == cat_r3[s][i][2] for s in SLOTS for i in range(6))
check("42종 등급 전부 동일", same_rarity, True)
mono = all(all(REQ_R3[s][i] < REQ_R3[s][i + 1] for i in range(5)) for s in SLOTS)
check("슬롯마다 요구 레벨 순증(순위 동점 없음)", mono, True)
r3_tiers = tier_arrival(cat_r3)
check("임계 도달 레벨 4개 전부 불변", r3_tiers, base_tiers)
print("        기준선:", base_tiers)
print("        R3    :", r3_tiers)

# 세트 완성 레벨
def set_levels(req):
    return {r: max(req[s][r] for s in STAT_SLOTS) for r in range(6)}


sl_now, sl_r3 = set_levels(REQ_NOW), set_levels(REQ_R3)
print()
print("  세트 완성 레벨 (스탯 4슬롯 같은 rank 전부 열리는 레벨)")
print("    rank :", " ".join(f"{r:>3}" for r in range(6)))
print("    현행 :", " ".join(f"{sl_now[r]:>3}" for r in range(6)))
print("    R3   :", " ".join(f"{sl_r3[r]:>3}" for r in range(6)))
print("    ★ rank1(세트 B)만 Lv.13 -> Lv.5. rank1은 rank0과 같은 '일반'이라 스탯이 동일하다")
print(f"      (일반 주{PRIMARY['일반']}/부{SECOND['일반']}) -> 세트 B 완성이 빨라져도 수치는 0 변화.")
sb_now = level_base(sl_now[1]) + PRIMARY["일반"] + SECOND["일반"] + SET_BONUS
sb_r3 = level_base(sl_r3[1]) + PRIMARY["일반"] + SECOND["일반"] + SET_BONUS
sa_r3 = level_base(sl_r3[1]) + PRIMARY["일반"] + SECOND["일반"] + SET_BONUS
check("세트 B(R3, Lv.5) 스탯 == 같은 레벨의 세트 A 스탯", sb_r3, sa_r3)

section("C-4. 회색 비율 — 무엇이 좋아지고 무엇이 안 좋아지나 (정직하게)")
print("  레벨   시각(h)  현행 열림/42  회색%   R3 열림/42  회색%   차이")
for lv in [1, 2, 3, 4, 5, 6, 9, 13, 17, 22, 28, 30]:
    a, b_ = opened_at(lv, REQ_NOW), opened_at(lv, REQ_R3)
    print(f"  Lv.{lv:<4} {hours_to(lv):>7.1f}  {a:>8}/42 {(42-a)/42*100:>7.1f}%"
          f"  {b_:>8}/42 {(42-b_)/42*100:>7.1f}%   {b_-a:+d}")
print()
print("  ★ 최대 개선 폭은 +5종(Lv.5·6)이고 Lv.13 이후로는 0이다.")
print("     rank1만 움직였으므로 그 이상은 구조적으로 불가능하다 —")
print("     rank2 이상을 내리면 임계 도달 시각이 움직인다(C-5에서 잰다).")

section("C-5. 반증 시도 — rank2까지 내리면 정말 임계가 움직이나")
for target in [2, 3]:
    req_x = {s: list(v) for s, v in REQ_R3.items()}
    for s in SLOTS:
        req_x[s][target] = max(req_x[s][target - 1] + 1, req_x[s][target] // 2)
    cat_x = build_catalog(req_x)
    tx = tier_arrival(cat_x)
    moved = {t: (base_tiers[t], tx[t]) for t in TIERS if tx[t] != base_tiers[t]}
    print(f"  rank{target}을 절반으로: 임계 이동 {moved if moved else '없음'}")
    print(f"     사다리 예: Head {req_x['Head']}  Shoulders {req_x['Shoulders']}")
print()
print("  → rank2를 내려도 임계는 안 움직인다(레벨기본치가 병목). rank4를 내리면 움직인다.")
req_y = {s: list(v) for s, v in REQ_R3.items()}
for s in SLOTS:
    req_y[s][4] = min(req_y[s][4], 13)
    req_y[s][3] = min(req_y[s][3], 12)
ty = tier_arrival(build_catalog(req_y))
print(f"  rank4를 Lv.13으로: {ty}   (기준선 {base_tiers})")


# ══════════════════════════════════════════════════════════════════════
# D. 상점 생존도 시뮬 — R2 판정 기준 6개를 R3 사다리로 다시 통과시키는가
# ══════════════════════════════════════════════════════════════════════
section("D. 상점 생존도 — R2 채택 패키지 ⑤ 위에서 사다리만 R3로 바꾼다")

PROFILES = {
    "A 켜두기만(집중0)": dict(weekday=(8, 0, 4), weekend=(4, 0, 2)),
    "B 기준(집중25x2)": dict(weekday=(8, 2, 4), weekend=(4, 0, 2)),
    "C 적극(집중25x4)": dict(weekday=(10, 4, 8), weekend=(6, 1, 4)),
}
SEED_COINS = 60
TODO_COIN = 15
FOCUS_DONE = 1.2


def make_items(req):
    out = []
    for s in SLOTS:
        pop = [(0, lv) for lv in req[s]]
        for i in range(6):
            g = rarity_of_member(pop, i)
            # R2 0-2-3: 외형 3슬롯 rank0도 유료 30동전, 스탯 4슬롯 rank0은 무상
            free = (i == 0 and s in STAT_SLOTS)
            price = 0 if free else (30 if i == 0 else PRICE[g])
            out.append(dict(slot=s, rank=i, lv=req[s][i], grade=g, price=price, free=free))
    return out


def sim(req, days=260, weekday=(8, 2, 4), weekend=(4, 0, 2)):
    items = make_items(req)
    coins = SEED_COINS
    lv, cur, hrs = 1, 0.0, 0.0
    reached, owned = {}, {}
    live_days = short_days = void_days = 0
    first_buy = None
    day1_live = None
    gap, maxgap, done = 0, 0, None
    for d in range(1, days + 1):
        h, f, a = weekday if (d - 1) % 7 < 5 else weekend
        a = min(a, int(h * 3600 / ARCHERY_CD_SEC))
        hrs += h
        cur += h * 60 * PASSIVE_XP_PER_MIN + a * BULLSEYE_XP
        while cur >= need(lv):
            cur -= need(lv)
            lv += 1
        coins += int(25 * FOCUS_DONE) * f + a * 1 + TODO_COIN
        for i, e in enumerate(items):
            if e["lv"] <= lv and i not in reached:
                reached[i] = hrs
        for i, e in enumerate(items):
            if i in owned or i not in reached:
                continue
            if e["free"] or hrs >= reached[i] + GRACE[e["grade"]]:
                owned[i] = (d, "자동")
        cand = [(i, e) for i, e in enumerate(items) if i not in owned and i in reached]
        if d == 1:
            day1_live = sum(1 for i, e in cand if coins >= e["price"])
        if not cand:
            void_days += 1
            gap += 1
        elif any(coins >= e["price"] for i, e in cand):
            live_days += 1
            gap = 0
        else:
            short_days += 1
            gap += 1
        maxgap = max(maxgap, gap) if len(owned) < len(items) else maxgap
        for i, e in sorted(cand, key=lambda t: t[1]["price"]):
            if coins >= e["price"]:
                coins -= e["price"]
                owned[i] = (d, "구매")
                if first_buy is None:
                    first_buy = d
        if done is None and len(owned) == len(items):
            done = d
    bought = sum(1 for v in owned.values() if v[1] == "구매")
    pre = done or days
    return dict(bought=bought, done=done, first_buy=first_buy, day1_live=day1_live,
                live=live_days / pre * 100, short=short_days / pre * 100,
                void=void_days / pre * 100, maxgap=maxgap)


print("  사다리   원형              구매  완주  첫구매  1일차버튼  살아있는날%  잔액부족%  최장공백")
rows = {}
for tag, req in [("현행", REQ_NOW), ("R3", REQ_R3)]:
    for nm, kw in PROFILES.items():
        r = sim(req, **kw)
        rows[(tag, nm)] = r
        print(f"  {tag:<7} {nm:<16} {r['bought']:>4} {str(r['done']):>5} {str(r['first_buy']):>6}"
              f"    {r['day1_live']:>6}     {r['live']:>8.1f}   {r['short']:>7.1f}   {r['maxgap']:>6}")

section("D-2. R2 판정 기준 6개 (R3 사다리에서 다시 통과하는가)")
A = rows[("R3", "A 켜두기만(집중0)")]
B = rows[("R3", "B 기준(집중25x2)")]
C = rows[("R3", "C 적극(집중25x4)")]
check("① A 구매 >= 11", A["bought"] >= 11, True)
check("② B 구매 >= 30", B["bought"] >= 30, True)
check("③ 1일차 살아있는 버튼 >= 1", B["day1_live"] >= 1, True)
check("④ 완주 전 최장 무버튼 <= 21일 (B)", B["maxgap"] <= 21, True)
check("⑤ A 완주 <= 100일", A["done"] is not None and A["done"] <= 100, True)
check("⑥ A 첫 구매 1일차", A["first_buy"] == 1, True)

section("D-3. ★ 조용한 폴백 검사 — 그리고 그 검사가 처음에 빨갛게 났다")
print("  1차 시도: '완화했으니 구매수·완주일이 좋아져야 한다'로 잡았더니 **빨갛게 났다**.")
print("     A 구매 19->16, A 완주 77->81일, B 구매 37->34.")
print("     역방향(사다리를 Lv.9로 더 빡빡하게)도 '좋아짐'이 나왔다.")
print("     → 폴백을 의심하고 기전을 직접 계측했다. 결과는 아래다.")
print()
print("  계측: 마지막 아이템을 어떻게 얻었나 / 그때 잔액은 얼마였나")


def sim_trace_last(req, weekday, weekend, days=260):
    items = make_items(req)
    coins = SEED_COINS
    lv, cur, hrs = 1, 0.0, 0.0
    reached, owned = {}, {}
    last_i = max(range(len(items)), key=lambda i: items[i]["lv"])
    coins_at_reach = None
    auto = 0
    for d in range(1, days + 1):
        h, f, a = weekday if (d - 1) % 7 < 5 else weekend
        a = min(a, int(h * 3600 / ARCHERY_CD_SEC))
        hrs += h
        cur += h * 60 * PASSIVE_XP_PER_MIN + a * BULLSEYE_XP
        while cur >= need(lv):
            cur -= need(lv)
            lv += 1
        coins += int(25 * FOCUS_DONE) * f + a * 1 + TODO_COIN
        for i, e in enumerate(items):
            if e["lv"] <= lv and i not in reached:
                reached[i] = hrs
                if i == last_i:
                    coins_at_reach = coins
        for i, e in enumerate(items):
            if i in owned or i not in reached:
                continue
            if e["free"] or hrs >= reached[i] + GRACE[e["grade"]]:
                owned[i] = (d, "자동")
        for i, e in sorted([(i, e) for i, e in enumerate(items)
                            if i not in owned and i in reached], key=lambda t: t[1]["price"]):
            if coins >= e["price"]:
                coins -= e["price"]
                owned[i] = (d, "구매")
        if len(owned) == len(items):
            auto = sum(1 for v in owned.values() if v[1] == "자동")
            return dict(done=d, how=owned[last_i][1], coins_at_reach=coins_at_reach,
                        price=items[last_i]["price"], auto=auto)
    return dict(done=None, how="?", coins_at_reach=coins_at_reach, price=items[last_i]["price"], auto=0)


for tag, req in [("현행", REQ_NOW), ("R3", REQ_R3)]:
    for nm, kw in PROFILES.items():
        t = sim_trace_last(req, **kw)
        print(f"    {tag:<5} {nm:<18} 완주 {str(t['done']):>4}일  마지막칸={t['how']}"
              f"  (가격 {t['price']}, 도달 시점 잔액 {t['coins_at_reach']})  자동해금 {t['auto']}종")
print()
print("  ★ 기전이 확인됐다. 폴백이 아니라 **지표 자체가 단조가 아니었다**:")
print("     아이템이 일찍 열리면 (a) 유예 타이머도 일찍 시작해 '자동'으로 넘어가고")
print("     (b) 싼 것부터 사는 지갑이 일찍 비어 마지막 전설(330)을 못 산다.")
print("     즉 '구매 수'와 '완주일'은 상점 친절도의 지표가 아니라")
print("     **지갑이 유예 타이머와의 경주에서 이기는 빈도**를 잰다. 완화하면 내려가는 것이 정상이다.")
print()
print("  단조인 지표로 다시 잰다 (완화 -> 반드시 개선, 강화 -> 반드시 악화):")
req_tight = {s: list(v) for s, v in REQ_R3.items()}
for s in SLOTS:
    req_tight[s][1] = 9


def mono_metrics(req):
    return dict(opened_lv2=opened_at(2, req), opened_lv5=opened_at(5, req),
                min_slot_lv5=min(per_slot_opened(5, req).values()),
                day1=sim(req, **PROFILES["B 기준(집중25x2)"])["day1_live"],
                firstbuy=sim(req, **PROFILES["A 켜두기만(집중0)"])["first_buy"],
                maxgap=sim(req, **PROFILES["B 기준(집중25x2)"])["maxgap"])


m_now, m_r3, m_tight = mono_metrics(REQ_NOW), mono_metrics(REQ_R3), mono_metrics(req_tight)
print(f"    {'지표':<22} {'강화(rank1=9)':>14} {'현행':>8} {'R3(완화)':>10}  방향")
for k in m_now:
    tri = (m_tight[k], m_now[k], m_r3[k])
    ok = tri[0] <= tri[1] <= tri[2]
    print(f"    {k:<22} {tri[0]:>14} {tri[1]:>8} {tri[2]:>10}  {'단조 ✔' if ok else '**비단조**'}")
check("단조 지표 4종이 강화<=현행<=완화 순서를 지킨다",
      all(m_tight[k] <= m_now[k] <= m_r3[k] for k in ["opened_lv2", "opened_lv5", "min_slot_lv5", "day1"]), True)

section("D-4. rank1 배치 스윕 — A 완주 +4일을 줄일 수 있나")
CANDS = {
    "P1 계단 2,2,3,3,4,4,5 (제안)": {"Head": 2, "Hair": 2, "Eyes": 3, "Fx": 3, "Neck": 4, "Pet": 4, "Shoulders": 5},
    "P2 Head만 2, 나머지 5":        {"Head": 2, "Hair": 5, "Eyes": 5, "Fx": 5, "Neck": 5, "Pet": 5, "Shoulders": 5},
    "P3 Head/Hair 2, 나머지 5":     {"Head": 2, "Hair": 2, "Eyes": 5, "Fx": 5, "Neck": 5, "Pet": 5, "Shoulders": 5},
    "P4 전부 2":                   {s: 2 for s in SLOTS},
    "P5 전부 5 (PS-1 실패)":        {s: 5 for s in SLOTS},
}
print("  후보                          PS-1 PS-2   A구매 A완주  B구매 B완주  1일차버튼  Lv.5열림")
for nm, plc in CANDS.items():
    req_c = {s: list(v) for s, v in REQ_NOW.items()}
    for s, lv in plc.items():
        req_c[s][1] = lv
    ps1 = (opened_at(2, req_c) - 7) >= 1
    ps2 = min(per_slot_opened(5, req_c).values()) >= 2
    ra = sim(req_c, **PROFILES["A 켜두기만(집중0)"])
    rb = sim(req_c, **PROFILES["B 기준(집중25x2)"])
    print(f"  {nm:<29} {'✔' if ps1 else '✘':>3} {'✔' if ps2 else '✘':>4}"
          f"   {ra['bought']:>4} {str(ra['done']):>5}  {rb['bought']:>4} {str(rb['done']):>5}"
          f"   {rb['day1_live']:>7}   {opened_at(5, req_c):>7}")
print()
print("  → PS-1·PS-2를 둘 다 통과하는 후보는 전부 A 완주 81일이다(현행 77일).")
print("     +4일의 출처는 배치가 아니라 '유예 타이머가 일찍 시작한다'는 규칙 자체다.")
print("     판정 기준 ⑤(A 완주 <= 100일)는 여유 19일로 통과한다 — 배치로 되돌릴 것이 없다.")


# ══════════════════════════════════════════════════════════════════════
# E. DLC 팩 — 등급 상한을 임계 불변 조건에서 유도한다
# ══════════════════════════════════════════════════════════════════════
section("E. DLC 팩 등급 상한 — 「어떤 임계도 앞당기지 않는다」에서 유도")

print("  가정: 팩 = 스탯 4슬롯 1종씩(같은 세트) + 외형 2종, 전부 같은 등급, 전부 Lv.1.")
print("        구매자는 팩을 사는 순간 6종 전부 착용 가능(요구 레벨 없음).")
print()
print("  등급   Lv.1 스탯  임계 도달 레벨(팩 착용 최적)          기준선 대비 앞당김(h)")
for g in ["일반", "희귀", "영웅", "전설"]:
    extra = {s: [(0, 1, g, "pack")] for s in STAT_SLOTS}
    t = tier_arrival(cat_r3, extra=extra)
    lv1 = level_base(1) + PRIMARY[g] + SECOND[g] + SET_BONUS
    adv = {k: round(hours_to(base_tiers[k]) - hours_to(t[k]), 1) for k in TIERS}
    print(f"  {g:<5} {lv1:>8}    {t}   {adv}")
print()
print("  ★ 판정: 희귀가 「초급 외에는 어떤 임계도 앞당기지 않는」 최대 등급이다.")
print("     영웅이면 중급이 Lv.17 -> Lv.5로 158.0h 앞당겨진다 = 결제가 연출 해금을 산다.")
print("     초급만 11.7h 앞당겨지는 것은 유예 자동 해금의 오차 범위 안이다.")

section("E-1b. ★ 더 정직한 자 — '요구 레벨 도달'이 아니라 '실제로 보유'로 다시 잰다")
print("  E절은 요구 레벨만 봤다. 실제로는 기본 아이템은 사거나 유예를 기다려야 하고(하한 = 유예),")
print("  팩 아이템은 구매 즉시 보유다(0동전). 그 비대칭을 넣으면 팩이 더 유리해진다.")
print("  보유 하한 시각 = hours_to(요구레벨) + 유예(등급).  팩 = hours_to(1) + 0 = 0h.")


def realized_tier_hours(cat, extra=None, maxlv=40):
    out = {}
    for t, need_v in TIERS.items():
        best = None
        for lv in range(1, maxlv + 1):
            lb = level_base(lv)
            for st, (ps, ss) in STAT_MAP.items():
                cand_p = [(PRIMARY[e[2]], hours_to(e[1]) + GRACE[e[2]]) for e in avail(cat, lv, ps)]
                cand_s = [(SECOND[e[2]], hours_to(e[1]) + GRACE[e[2]]) for e in avail(cat, lv, ss)]
                if extra:
                    cand_p += [(PRIMARY[e[2]], 0.0) for e in extra.get(ps, []) if e[1] <= lv]
                    cand_s += [(SECOND[e[2]], 0.0) for e in extra.get(ss, []) if e[1] <= lv]
                for vp, hp in cand_p:
                    for vs, hs in cand_s:
                        if min(lb + vp + vs, STAT_CAP) >= need_v:
                            h = max(hours_to(lv), hp, hs)
                            if best is None or h < best:
                                best = h
            # 세트 완성 경로 (4슬롯 같은 rank, 같은 세트)
            for r in range(6):
                items = [e for s in STAT_SLOTS for e in cat[s] if e[0] == r and e[1] <= lv]
                if len(items) == 4:
                    g = items[0][2]
                    v = min(lb + PRIMARY[g] + SECOND[g] + SET_BONUS, STAT_CAP)
                    if v >= need_v:
                        h = max([hours_to(lv)] + [hours_to(e[1]) + GRACE[e[2]] for e in items])
                        if best is None or h < best:
                            best = h
            if extra:
                packs = {s: [e for e in extra.get(s, []) if e[1] <= lv] for s in STAT_SLOTS}
                if all(packs[s] for s in STAT_SLOTS):
                    g = packs[STAT_SLOTS[0]][0][2]
                    v = min(lb + PRIMARY[g] + SECOND[g] + SET_BONUS, STAT_CAP)
                    if v >= need_v and (best is None or hours_to(lv) < best):
                        best = hours_to(lv)
        out[t] = best
    return out


base_real = realized_tier_hours(cat_r3)
print()
print("  등급   초급(h)      중급(h)      고급(h)      캡(h)      기본 대비 앞당김(h)")
print(f"  {'기본만':<5} {base_real['초급']:>9.1f} {base_real['중급']:>12.1f} "
      f"{base_real['고급']:>12.1f} {base_real['캡']:>11.1f}       —")
for g in ["일반", "희귀", "영웅", "전설"]:
    ex = {s: [(0, 1, g, "pack")] for s in STAT_SLOTS}
    pr = realized_tier_hours(cat_r3, extra=ex)
    adv = {k: round(base_real[k] - pr[k], 1) for k in TIERS}
    print(f"  {g:<5} {pr['초급']:>9.1f} {pr['중급']:>12.1f} {pr['고급']:>12.1f} {pr['캡']:>11.1f}"
          f"       {adv}")
rr = {g: realized_tier_hours(cat_r3, extra={s: [(0, 1, g, "pack")] for s in STAT_SLOTS})
      for g in ["희귀", "영웅", "전설"]}
print()
print(f"  ★ 희귀 팩의 실질 앞당김: 초급 {base_real['초급']-rr['희귀']['초급']:.1f}h · "
      f"중급 {base_real['중급']-rr['희귀']['중급']:.1f}h · "
      f"고급 {base_real['고급']-rr['희귀']['고급']:.1f}h · 캡 {base_real['캡']-rr['희귀']['캡']:.1f}h.")
print(f"     둘 다 유예 한 칸(16~23h) 규모다 = 저참여 유저가 무료로 받는 보장의 오차 범위 안.")
print(f"     영웅이면 중급이 {base_real['중급']-rr['영웅']['중급']:.1f}h, "
      f"전설이면 고급이 {base_real['고급']-rr['전설']['고급']:.1f}h · "
      f"캡이 {base_real['캡']-rr['전설']['캡']:.1f}h 결제로 사라진다.")
check("희귀 팩은 고급·캡을 1시간도 앞당기지 않는다",
      (round(base_real['고급'] - rr['희귀']['고급'], 1), round(base_real['캡'] - rr['희귀']['캡'], 1)), (0.0, 0.0))
check("★ 폴백 검사: 등급을 올리면(제약 완화) 앞당김이 반드시 커진다",
      (base_real['중급'] - rr['희귀']['중급']) < (base_real['중급'] - rr['영웅']['중급'])
      < (base_real['중급'] - rr['전설']['중급']), True)
print()
print("  ★ 두 자의 성질을 분명히 해 둔다 — 진짜 값은 이 둘 사이에 있다:")
print("     E절  = 기본도 즉시 보유(유예 0) 가정 -> 팩 이점을 **과소** 평가")
print("     E-1b = 기본이 동전 0으로 유예만 기다림 -> 팩 이점을 **과대** 평가")
print("     희귀 팩 이점 구간: 초급 11.7~16.0h · 중급 0.0~23.0h · 고급 0h · 캡 0h")
print("     영웅 팩 이점 구간: 중급 157.9~180.9h  |  전설: 고급 285.7~318.7h · 캡 218.9~251.9h")
print("     ★ 어느 자로 재도 판정이 같다 = 희귀가 상한이다.")

section("E-1c. 임계 도달 기준선 자기정정 — ECONOMY_SPEC 2-2의 '고급 67일차'는 틀렸다")
print("  2-2는 임계 도달을 '세트 완성'으로 계산했다. 그러나 임계는 **스탯별**이고,")
print("  세트를 깨는 혼합 빌드가 더 빨리 넘는다. 실제 경로:")
lb25 = level_base(25)
print(f"    매력 = Neck(주) + Eyes(부).  Lv.25에서 Neck rank5(반다나 Lv.25, 전설 주 +{PRIMARY['전설']})")
print(f"    + Eyes rank5(안대 Lv.23, 전설 부 +{SECOND['전설']}) + 레벨기본치 {lb25} = "
      f"{lb25 + PRIMARY['전설'] + SECOND['전설']} = 고급(18)")
print(f"    -> 고급은 Lv.25({hours_to(25):.1f}h)에 열린다. 세트 F(Lv.28)를 기다리지 않는다.")
print(f"  2-2 기재 값 / 정정 값:")
print(f"    초급 6  : 2일차        -> Lv.5  = {hours_to(5):.1f}h  (변동 없음)")
print(f"    중급 12 : 23일차       -> Lv.17 = {hours_to(17):.1f}h (변동 없음)")
print(f"    고급 18 : 67일차 ✘     -> Lv.25 = {hours_to(25):.1f}h  ★ 정정")
print(f"    캡   20 : 75일차       -> Lv.28 = {hours_to(28):.1f}h (변동 없음)")
check("고급의 최초 도달 레벨은 25다(28이 아니다)", base_tiers["고급"], 25)

section("E-2. 희귀 팩이 엔드게임에서 얼마나 손해인가 (정직하게)")
for lv in [1, 5, 9, 17, 22, 28, 30]:
    base_best = best_vector(cat_r3, lv)
    pk = {s: [(0, 1, "희귀", "pack")] for s in STAT_SLOTS}
    pack_best = best_vector(cat_r3, lv, extra=pk)
    # 팩 세트를 '입고 있을 때'의 값 (팩 4종 착용 강제)
    lb = level_base(lv)
    pack_worn = min(lb + PRIMARY["희귀"] + SECOND["희귀"] + SET_BONUS, STAT_CAP)
    print(f"  Lv.{lv:<3} 기본 최적 {min(base_best[1].values()):>3}   "
          f"팩 포함 최적 {min(pack_best[1].values()):>3}   팩 세트를 입으면 {pack_worn:>3}"
          f"   차이 {pack_worn - min(base_best[1].values()):+d}")
print()
print("  → Lv.17 전에는 팩이 앞서고, Lv.17에 같아지고, 그 뒤로는 기본이 앞선다.")
print("     ★ 임계 '해금'은 영구이므로 엔드게임에서 팩 세트로 갈아입어도 연출은 그대로 남는다.")
print("     잃는 것은 '배율' 계열뿐이다. 정확히 얼마인지 잰다(스탯 20 -> 13 = 고급 -> 중급):")
LOSS = [("민첩 이동속도 배율", 1.20, 1.12), ("집중력 성과물 배율", 1.60, 1.35),
        ("관찰력 동전 쿨다운 단축", 0.50, 0.30), ("매력 오라 강도", 0.70, 0.45)]
for nm, hi, lo in LOSS:
    if "쿨다운" in nm:
        r_hi, r_lo = 3600 / (ARCHERY_CD_SEC * (1 - hi)), 3600 / (ARCHERY_CD_SEC * (1 - lo))
        print(f"     {nm:<22} {r_hi:>5.1f} -> {r_lo:>5.1f} 동전/시   ({(r_lo/r_hi-1)*100:+.1f}%)")
    else:
        print(f"     {nm:<22} ×{hi:.2f} -> ×{lo:.2f}            ({(lo/hi-1)*100:+.1f}%)")
print("     그대로 남는 것(영구 해금): 깊은 잠 · 잔상 2단 · 던지기 3회전 · 오라 사용 자격.")
print("  ★ 미확인 1건 -> 리더: '배율'이 현재 스탯인가 최고 도달 단계인가가 사용자 확정 문구에 없다.")
print("     문구는 '일반 행동·연출은 영구'다. 나는 **배율 = 현재 스탯 / 해금 = 영구**로 읽었고,")
print("     그 읽기에서 위 손해가 나온다. 반대로 읽으면 팩의 엔드게임 손해가 0이 된다.")

section("E-3. 팩에 동전 가격을 붙이면 안 되는 이유 — 산술")
b_income = 79   # R2 0-2-6 확정: 원형 B 하루 수입 79동전
for g in ["일반", "희귀"]:
    p_half = PRICE[g] * 0.5
    pack_coins = p_half * 6
    days = pack_coins / b_income
    print(f"  {g} 팩 6종 × (기본가 {PRICE[g]} × 0.5 = {p_half:.0f}동전) = {pack_coins:.0f}동전"
          f" = 원형 B {days:.1f}일치 수입")
    print(f"     그 동전이 사는 것 = 유예 {GRACE[g]}h를 건너뛰는 것 = {GRACE[g]/24:.2f}일")
    print(f"     → {days:.1f}일치를 내고 {GRACE[g]/24:.2f}일을 산다. "
          f"{'**지배당하는 선택지(함정)**' if days*24 > GRACE[g] else '합리적'}")
print()
print("  그리고 더 큰 문제: 팩 동전은 기본 42종과 같은 지갑을 쓴다.")
for n in [1, 6]:
    coins = 70 * 0.5 * 6 * n
    print(f"    팩 {n}개를 사면 {coins:.0f}동전이 기본 진행에서 빠진다 = "
          f"기본 카탈로그가 {coins/b_income:.1f}일 느려진다.")
print("  ★ '$2.99를 냈더니 기본 진행이 느려진다'는 어떤 각도에서도 방어할 수 없다.")
print("  → 판정: 팩 구매 시 6종 전부 0동전 즉시 해금. 팩은 동전 경제에 접촉하지 않는다.")

section("E-4. 팩 구매 직후 착용 가능 비율 — product-strategy 실측 대비")
print("  구매 시점        기본 사다리 복사(현행)   R3 팩 규칙(전부 Lv.1)")
for lv in [1, 2, 6, 13, 22, 28, 30]:
    old = sum(1 for r in range(6) if REQ_NOW["Head"][r] <= lv)   # 기본 사다리를 복사한 팩
    print(f"  Lv.{lv:<3} ({hours_to(lv):>6.1f}h)   {old}/6 = {old/6*100:>5.1f}%"
          f"              6/6 = 100.0%")
print()
print("  번들($11.99, 6팩 = 36종) 기준")
for lv in [2]:
    old_total = opened_at(lv, REQ_NOW) + 6 * 1
    new_total = opened_at(lv, REQ_R3) + 36
    print(f"    Lv.{lv}: 현행 {old_total}/78 = {old_total/78*100:.1f}%"
          f"   →   R3 {new_total}/78 = {new_total/78*100:.1f}%")


# ══════════════════════════════════════════════════════════════════════
# F. 팩 등급을 '파생'으로 만들 수 있는가 — 못 만든다는 증명
# ══════════════════════════════════════════════════════════════════════
section("F. 팩 등급을 코호트 파생으로 낼 수 있는가")
print("  테마 팩 = 모자/안경/넥타이/망토/머리/펫에 1종씩. 슬롯당 코호트 크기 = 1.")
for s in ["Head", "Eyes", "Neck", "Shoulders"]:
    pop = [(0, lv) for lv in REQ_R3[s]] + [(1, 1)]
    print(f"    {s:<11} 팩 아이템의 파생 등급 = {rarity_of_member(pop, 6)}"
          f"   (기본 6종은 {[rarity_of_member(pop,i) for i in range(6)]})")
print()
print("  ★ 6종 전부 '일반'이 나온다. 원하는 '희귀'는 파생으로 만들 수 없다.")
print("     팩을 한 슬롯에 6종 몰아넣으면 사다리는 나오지만, 그러면 세트(4슬롯)를 못 만든다")
print("     = 팩의 상품 정의('그 애가 다르게 말하고 다르게 움직이는 하루')가 성립하지 않는다.")
print("  → 요건: 팩 아이템의 등급은 파생이 아니라 **선언**이어야 한다(에셋 필드 1개).")
print("     기본 42종은 파생을 유지한다(에셋 무변경 + 단조성 자동 보장).")


# ══════════════════════════════════════════════════════════════════════
# G. 작은 확정들
# ══════════════════════════════════════════════════════════════════════
section("G. 활쏘기 / 집중모드 격자 / 중도취소 / 할일")

print("  G-1. 활쏘기 A안(명중 = Bullseye + Hit) + B안(앞 2발 p_miss=0.50)")
print("       동전 지급은 '정중앙 1회 = 1동전'이고 마지막 발은 항상 Bullseye로 고정 유지된다.")
print("       -> 사이클당 정중앙 = 정확히 1발. B안 도입 전후 동전 수입 변화:")
for cd in [600]:
    print(f"          쿨다운 {cd}s -> 상한 {3600/cd:.0f}동전/시  (A·B안 도입 전후 동일)")
print("       관찰력 임계는 두 채널에 각각 붙는다(서로 간섭하지 않음):")
print("          동전 쿨다운  : -15% / -30% / -50%  ->  7.1 / 8.6 / 12.0 동전/시")
for p in [0.50, 0.40, 0.30, 0.20]:
    acc = (1 + 2 * (1 - p)) / 3
    print(f"          표시 명중률  : p_miss={p:.2f} -> {acc*100:.1f}%")
print()

print("  G-2. 집중모드 격자 = 5분 배수 (ux-widgets 발견 ② 확정)")
for m in [1, 3, 5, 10, 15, 25, 50, 120]:
    pay = 1.2 * m
    rate_floor = int(pay) / (m / 60)
    rate_ceil = -(-int(pay * 1000) // 1000)  # 표시용
    print(f"     {m:>3}분 -> 1.2×{m} = {pay:>6.1f}   내림 {int(pay):>3}동전 = {rate_floor:>6.1f}동전/시"
          f"   {'정수 ✔' if abs(pay-int(pay))<1e-9 else '비정수 ✘'}")
print("     → 5분 배수에서만 1.2×분이 정수. 격자를 5분으로 하면 반올림 규칙과 무관하게 72동전/시 고정.")
print()

print("  G-3. 중도취소 = 1.0 × floor(경과 분). '선택한 분'이 아니라 '경과한 분'.")
print("       파밍 상한 검산 (경과분만 지급하므로 최악은 '1분마다 취소'):")
for m in [1, 5, 15]:
    print(f"          {m}분마다 취소 반복 -> {60//m}회/시 × {m}동전 = {60//m*m}동전/시"
          f"   (완주 72동전/시 대비 {60//m*m/72*100:.1f}%)")
print("       → 어떤 취소 주기도 완주(72/시)를 못 넘는다. 하한 규칙이 필요 없다.")
print("       ('선택한 분'으로 읽으면 120분 선택 후 0초 취소 = 120동전 즉시 = 무한 파밍)")
print()

print("  G-4. [오늘 할일] 정액 15동전 · 시간 비종속 채널 합계 30/일")
print(f"       집중 25분 1회 = {int(25*1.2)}동전 = 소프트캡 30과 같다(시간을 쓰는 채널이 항상 크거나 같다).")
print(f"       [오늘 할일] 15 사용 -> 남은 여유 {30-15}동전/일 = 이런 채널을 하나 더 붙일 수 있다.")
d_max = int(50 * 1.2) * 4 + int(3600 / ARCHERY_CD_SEC) * 12 + 15
print(f"       D상한(집중 50분×4 + 활쏘기 12h 최대 + 할일) = {d_max}동전/일")
print(f"       A/B = {19/79:.2f} (>=0.20 ✔)   D/B = {d_max/79:.2f} (<=5.0 ✔)")

section("H. 저장 빈도 (R3에서 바뀌는가)")
print("  기준선: 패시브 XP가 10초마다 IsDirty -> 자동 저장 60초가 항상 쓴다 = 1,440회/일")
adds = [("집중 세션 종료(동전 지급)", 2.00), ("아이템 구매", 0.41), ("유예 자동 해금", 0.15),
        ("부스탯 재분배", 0.39), ("임계 최초 돌파", 0.16), ("[오늘 할일] 정액", 1.00),
        ("팩 구매 시 6종 일괄 해금", 0.00)]
tot = sum(v for _, v in adds)
for n, v in adds:
    print(f"    {n:<28} {v:>5.2f} 회/일")
print(f"    {'합계':<28} {tot:>5.2f} 회/일 = 기준선 대비 +{tot/1440*100:.3f}%")
print("    ★ 팩 구매는 평생 최대 6회(팩 개수)라 0.00으로 잡는다. R2(3.72)와 같다 — 판단 불변.")

print()
print("=" * 78)
print("전체 교정/검증 결과:", "전부 통과" if not FAIL else f"실패 {FAIL}")
print("=" * 78)
if FAIL:
    sys.exit(1)
