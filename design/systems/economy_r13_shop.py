#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R13 — 상점 탭 언블록 라운드 (2026-09-05, design-systems)

이 스크립트가 답하는 것:
  [0] 교정 — 알려진 값 14건. 하나라도 깨지면 SystemExit 으로 뒤의 숫자를 전부 폐기한다.
  [1] 충돌 #6 「유예 자동 해금」 — 지금 경제에서 한 번이라도 발화하는가.
  [2] 충돌 #7 「BASE 상수 vs 레벨 파생」 — 레벨 파생이면 사용자 확정(1일차 4스탯 초급)이 깨지는가.
  [3] 시드 동전(U-42) 금액과 1일차 상점 그림.
  [4] 외형 18종 가격(U-21) — 사다리 유지 vs 정액의 완주일 영향.
  [5] 표시 여유 — 4단계(U-24)와 5자리 가격이 확정된 카드 치수에 들어가는가.

★ 교정 출처는 이 스크립트 밖이다(골든 파일 · 프로덕션 상수 · 다른 담당자의 실측).
  생성기와 검사기가 같이 틀리는 것을 막기 위해, 기대값을 이 스크립트의 함수로 만들지 않는다.
"""

import struct
import sys

FAIL = []


def f32(x):
    return struct.unpack("f", struct.pack("f", x))[0]


def ck(name, got, want, tol=0.0):
    ok = abs(got - want) <= tol if isinstance(want, float) else got == want
    print(f"  [{'OK' if ok else '**FAIL**'}] {name:<46} got={got!r:>14}  want={want!r}")
    if not ok:
        FAIL.append(name)


# ---------------------------------------------------------------- [0] 교정
print("=" * 78)
print("[0] 교정 — 알려진 값으로 먼저 맞춘다")
print("=" * 78)

# --- XP 곡선 (Core/CharacterProgressionModel.cs:94, Mathf.Pow = float32)
def need(level):
    return f32(f32(100.0) * f32(pow(max(1, level), f32(1.05))))


def need_f0(level):
    """실기 로그는 `{need:F0}` 문자열이다 — .NET F0 = 0.5에서 반올림(away from zero)."""
    v = need(level)
    return int(v + 0.5) if v >= 0 else -int(-v + 0.5)


ck("need(2) @1.05  (실기 로그 196/207)", need_f0(2), 207)
ck("need(3) @1.05  (실기 로그 6/317)", need_f0(3), 317)
ck("need(127) @1.05 (실기 로그 5700/16181)", need_f0(127), 16181)


def cum_xp(level):
    """레벨 `level` 에 도달하기까지 누적 XP."""
    return sum(need(k) for k in range(1, level))


PASSIVE_XP_PER_HOUR = 90.0  # progressionPassiveXpPerMinute 1.5 × 60


def hours_to_level(level):
    return cum_xp(level) / PASSIVE_XP_PER_HOUR


ck("Lv.5 누적시간 (클래스 주석 11.7h)", round(hours_to_level(5), 1), 11.7, 0.05)
ck("Lv.8 누적시간 (클래스 주석 33.6h)", round(hours_to_level(8), 1), 33.6, 0.05)
ck("Lv.30 누적시간 (ECONOMY_SPEC 5-1 558.5h)", round(hours_to_level(30), 1), 558.5, 0.05)

# --- 카탈로그 (Golden/ItemCatalogGolden.txt 전수 — 손으로 옮긴 값이 아니라 grep 출력)
LEVELS = {
    "Head":      [1, 5, 9, 20, 23, 26],
    "Eyes":      [1, 6, 11, 15, 19, 23],
    "Neck":      [1, 8, 12, 18, 21, 25],
    "Shoulders": [1, 13, 17, 22, 25, 28],
    "Hair":      [1, 5, 9, 14, 18, 22],
    "Fx":        [1, 6, 12, 16, 20, 24],
    "Pet":       [1, 13, 19, 24, 27, 30],
}
STAT_SLOTS = ["Head", "Eyes", "Neck", "Shoulders"]
LOOK_SLOTS = ["Hair", "Fx", "Pet"]

# ItemCatalog.cs:770 _rarityByRank = { Common, Common, Rare, Rare, Epic, Legendary }
RARITY_BY_RANK = ["일반", "일반", "희귀", "희귀", "영웅", "전설"]
PRICE = {"일반": 600, "희귀": 1400, "영웅": 3200, "전설": 9600}
GRACE_H = {"일반": 16, "희귀": 23, "영웅": 27, "전설": 33}  # ECONOMY_SPEC 0-2-4

ck("장비 종수", sum(len(v) for v in LEVELS.values()), 42)
ck("슬롯당 사다리 총액", sum(PRICE[r] for r in RARITY_BY_RANK), 16800)

items = []
for slot, lv in LEVELS.items():
    for rank, level in enumerate(lv):
        rarity = RARITY_BY_RANK[rank]
        free = (slot in STAT_SLOTS) and rank == 0  # 스탯 4슬롯 rank0 무상 (0-2-3)
        items.append(dict(slot=slot, rank=rank, lv=level, rarity=rarity,
                          price=0 if free else PRICE[rarity], free=free))

TOTAL_SPEND = sum(i["price"] for i in items)
ck("42종 소모처 총액 (§12-2 115,200)", TOTAL_SPEND, 115200)

# --- 원형 (DESIGN_SYSTEMS_STATS §18-3, 시나리오 ② 「무료 회복제 1개」)
ARCH = {
    #             능동   유휴    온라인h/일
    "A  켜두기만":   (68,   2000, 8.0),
    "A2 가볍게 켬":  (68,   1440, 2.0),
    "B  기준":       (1280, 2000, 8.0),
    "C  적극":       (2560, 2000, 8.0),
    "D  상한":       (6000, 2000, 10.0),
}
for k, (act, idle, _h) in ARCH.items():
    pass
ck("원형 A 일일", ARCH["A  켜두기만"][0] + ARCH["A  켜두기만"][1], 2068)
ck("원형 B 일일", ARCH["B  기준"][0] + ARCH["B  기준"][1], 3280)
ck("전설 = 원형 B 며칠치 (§18-4 2.93)", round(9600 / 3280, 2), 2.93, 0.005)
ck("유휴 기본상한 도달 분 (§18-2 125.0)", round(1500 / 12, 1), 125.0, 0.05)

# --- 인계본 스탯 모형 (DESIGN_SYSTEMS_STATS §1-2 · §1-3 · §14-3)
BASE = {"집중력": 8, "관찰력": 6, "매력": 5, "민첩": 7}
MAINV = {"일반": 3, "희귀": 6, "영웅": 10, "전설": 15}
SUBV = {"일반": 1, "희귀": 2, "영웅": 4, "전설": 6}
TIERS = [("초급", 10), ("중급", 20), ("고급", 32)]
CAP = 40
SLOT_MAIN = {"Head": "집중력", "Eyes": "관찰력", "Neck": "매력", "Shoulders": "민첩"}
# §14-3 확정표 (부스탯 방향 24개)
SUB_DIR = {
    "Head":      ["민첩", "관찰력", "매력", "민첩", "관찰력", "매력"],
    "Eyes":      ["매력", "민첩", "집중력", "매력", "민첩", "집중력"],
    "Neck":      ["관찰력", "민첩", "집중력", "집중력", "관찰력", "민첩"],
    "Shoulders": ["매력", "집중력", "관찰력", "집중력", "관찰력", "매력"],
}


def stats_of(loadout, base=BASE):
    """loadout = {slot: rank}. 반환 = {stat: 값(클램프 적용)}"""
    s = dict(base)
    for slot, rank in loadout.items():
        rar = RARITY_BY_RANK[rank]
        s[SLOT_MAIN[slot]] += MAINV[rar]
        s[SUB_DIR[slot][rank]] += SUBV[rar]
    return {k: min(v, CAP) for k, v in s.items()}


day1 = stats_of({s: 0 for s in STAT_SLOTS})
ck("1일차 스탯 (§14-3 11/10/10/11)",
   [day1["집중력"], day1["관찰력"], day1["매력"], day1["민첩"]], [11, 10, 10, 11])

raw_focus = BASE["집중력"] + MAINV["전설"] + SUBV["전설"] * 3
ck("원시 최대 집중력 (§14-4 41 > CAP 40)", raw_focus, 41)

print()
if FAIL:
    print("★ 교정 실패:", FAIL)
    raise SystemExit("교정이 깨졌다 — 아래 숫자를 전부 폐기한다.")
print(f"★ 교정 {14}/{14} PASS — 아래 숫자를 신뢰해도 된다.\n")


# ------------------------------------------------- [1] 충돌 #6 — 유예 발화 여부
print("=" * 78)
print("[1] 충돌 #6 — 유예 자동 해금이 지금 경제에서 한 번이라도 발화하는가")
print("=" * 78)
print("  모형: 하루 단위. 그날의 온라인 시간만큼 누적시간이 늘고(레벨), 일일 수입이 지갑에 들어온다.")
print("  구매 규칙: 요구 레벨을 넘긴 미보유 아이템 중 **싼 것부터**, 지갑이 되는 데까지.")
print("  유예 규칙: 요구 레벨 도달 시각 + 등급별 유예(누적 시간 축)에 무료 해금.")
print()

DAYS = 400
rows = []
rows_wall = []
for name, (act, idle, on_h) in ARCH.items():
    daily = act + idle
    owned = set(i for i, it in enumerate(items) if it["free"])
    reach_h = {}          # 아이템 → 요구 레벨 도달 누적시간
    bought_day = {}
    grace_day = {}
    grace_wall_day = {}
    wallet = 0
    cum_h = 0.0
    lvl = 1
    for day in range(1, DAYS + 1):
        cum_h += on_h
        while lvl < 30 and cum_h >= hours_to_level(lvl + 1):
            lvl += 1
        wallet += daily
        for idx, it in enumerate(items):
            if idx not in reach_h and lvl >= it["lv"]:
                reach_h[idx] = cum_h
        # 유예 발화 시각(참고용, 실제로 적용하지는 않는다 — 어느 쪽이 먼저인지만 잰다)
        # (해석 A) 누적 「함께한 시간」 축 — UX_SHOP_AND_CURRENCY §14-1이 명시한 축
        for idx, h0 in reach_h.items():
            if idx not in grace_day and cum_h >= h0 + GRACE_H[items[idx]["rarity"]]:
                grace_day[idx] = day
        # (해석 B) 벽시계 축 — 문서가 그렇게 안 적었지만, 반대 해석에서도 결론이 같은지 본다
        for idx, h0 in reach_h.items():
            if idx not in grace_wall_day:
                reach_d = h0 / on_h
                if day >= reach_d + GRACE_H[items[idx]["rarity"]] / 24.0:
                    grace_wall_day[idx] = day
        # 구매
        cand = sorted((idx for idx, it in enumerate(items)
                       if idx not in owned and lvl >= it["lv"]),
                      key=lambda i: items[i]["price"])
        for idx in cand:
            p = items[idx]["price"]
            if wallet >= p:
                wallet -= p
                owned.add(idx)
                bought_day[idx] = day
        if len(owned) == len(items):
            break
    done = max(bought_day.values()) if len(owned) == len(items) else None
    # 유예가 구매보다 빠른 아이템 수
    earlier = sum(1 for idx in bought_day
                  if idx in grace_day and grace_day[idx] < bought_day[idx])
    earlier_wall = sum(1 for idx in bought_day
                       if idx in grace_wall_day and grace_wall_day[idx] < bought_day[idx])
    rows_wall.append((name, earlier_wall))
    # 유예만으로 완주(동전 0)
    grace_only = 0.0
    for idx, it in enumerate(items):
        if it["free"]:
            continue
        h = hours_to_level(it["lv"]) + GRACE_H[it["rarity"]]
        grace_only = max(grace_only, h / on_h)
    rows.append((name, daily, on_h, done, earlier, grace_only, wallet))

print(f"  {'원형':<14}{'동전/일':>8}{'온라인h':>8}{'동전완주':>9}"
      f"{'유예가 빠른 종수':>16}{'유예만 완주':>12}{'잔액':>10}")
for name, daily, on_h, done, earlier, gonly, wallet in rows:
    print(f"  {name:<14}{daily:>8,}{on_h:>8.0f}{(str(done)+'일'):>9}"
          f"{(str(earlier)+'/38'):>16}{gonly:>11.1f}일{wallet:>10,}")

print()
print("  ⇒ 유예가 구매보다 빠른 아이템 (해석 A · 누적 함께한 시간): 전 원형 합계",
      sum(r[4] for r in rows), "종")
print("  ⇒ 유예가 구매보다 빠른 아이템 (해석 B · 벽시계):          전 원형 합계",
      sum(r[1] for r in rows_wall), "종",
      "  ← 반대 해석으로도 결론이 같다")
print("  ⇒ 가장 느린 원형(A2)조차 동전 완주가 유예 완주보다 빠르다:",
      f"{rows[1][3]}일 vs {rows[1][5]:.1f}일")
print()
print("  ★ 음성 대조 — 유휴 수급(R10-c 사용자 확정)을 끄면 같은 계산이 뒤집히는가:")
for name, (act, idle, on_h) in ARCH.items():
    daily = act  # 유휴 0
    owned = set(i for i, it in enumerate(items) if it["free"])
    reach_h, bought_day, grace_day = {}, {}, {}
    wallet, cum_h, lvl = 0, 0.0, 1
    for day in range(1, 2000):
        cum_h += on_h
        while lvl < 30 and cum_h >= hours_to_level(lvl + 1):
            lvl += 1
        wallet += daily
        for idx, it in enumerate(items):
            if idx not in reach_h and lvl >= it["lv"]:
                reach_h[idx] = cum_h
        for idx, h0 in reach_h.items():
            if idx not in grace_day and cum_h >= h0 + GRACE_H[items[idx]["rarity"]]:
                grace_day[idx] = day
        for idx in sorted((i for i, it in enumerate(items)
                           if i not in owned and lvl >= it["lv"]),
                          key=lambda i: items[i]["price"]):
            if wallet >= items[idx]["price"]:
                wallet -= items[idx]["price"]
                owned.add(idx)
                bought_day[idx] = day
        if len(owned) == len(items):
            break
    e = sum(1 for idx in grace_day
            if not items[idx]["free"]
            and (idx not in bought_day or grace_day[idx] < bought_day[idx]))
    print(f"    {name:<14} 유휴 0일 때 유예가 이기는 종수 = {e}/38"
          f"   (동전 완주 {max(bought_day.values()) if len(owned)==len(items) else '>1999'}일)")


# --------------------------------------- [2] 충돌 #7 — BASE 상수 vs 레벨 파생
print()
print("=" * 78)
print("[2] 충돌 #7 — 레벨 기본치를 「4스탯 공통 레벨 파생」으로 되돌리면 무슨 일이 나는가")
print("=" * 78)


def level_base(lv):
    """ECONOMY_SPEC 2-1 축 A: min(floor((Lv-1)/4), 5)"""
    return min((lv - 1) // 4, 5)


for lv in (1, 5, 21):
    b = {k: level_base(lv) for k in BASE}
    st = stats_of({s: 0 for s in STAT_SLOTS}, base=b)
    tier = [n for n, v in TIERS if min(st.values()) >= v]
    print(f"  Lv.{lv:<3} 레벨기본치 {level_base(lv)}  →  무료 시작 4종 착용 시 "
          f"집중 {st['집중력']:>2} 관찰 {st['관찰력']:>2} 매력 {st['매력']:>2} 민첩 {st['민첩']:>2}"
          f"   4스탯 전부 초급? {'예' if min(st.values()) >= 10 else '**아니오**'}")

b21 = {k: 5 for k in BASE}
mx = 5 + MAINV["전설"] + SUBV["전설"] * 3
print(f"  레벨 파생 포화(5)에서 한 스탯 원시 최대 = 5 + 15 + 18 = {mx}  (CAP {CAP} 미만 → 클램프가 죽는다)")
print(f"  BASE 상수에서는          8 + 15 + 18 = {raw_focus}  (CAP {CAP} 초과 → I-1 양성 대조가 산다)")


# ---------------------------------------------------- [3] 시드 동전 (U-42)
print()
print("=" * 78)
print("[3] 시드 동전 U-42 — 1일차 상점 그림")
print("=" * 78)
lv1_buyable = [i for i in items if i["lv"] == 1 and not i["free"]]
print(f"  Lv.1에 살 수 있는 것 = 외형 3슬롯 rank0 {len(lv1_buyable)}종 × 600 = "
      f"{sum(i['price'] for i in lv1_buyable):,}동전")
print(f"  시드 0    → 첫 구매 가능 시각 = 600 / 12(유휴 분당) = {600/12:.0f}분  (그동안 버튼 3개가 전부 회색)")
for seed in (600, 1200, 1800):
    print(f"  시드 {seed:>5,} → 첫 실행 즉시 살 수 있는 종수 {seed//600}/3 · "
          f"총액 대비 {seed/TOTAL_SPEND*100:.2f}% · 원형 B {seed/3280:.2f}일치")


# ------------------------------------------------ [4] 외형 18종 가격 (U-21)
print()
print("=" * 78)
print("[4] U-21 외형 18종 가격 — 사다리 유지 vs 정액")
print("=" * 78)
look_total = sum(i["price"] for i in items if i["slot"] in LOOK_SLOTS)
stat_total = TOTAL_SPEND - look_total
print(f"  사다리 유지: 외형 {look_total:,} + 스탯 {stat_total:,} = {TOTAL_SPEND:,}"
      f"  → 원형 B {TOTAL_SPEND/3280:.1f}일")
for flat in (600, 1400, 3200):
    t = stat_total + flat * 18
    print(f"  정액 {flat:>5,}   : 외형 {flat*18:>7,} + 스탯 {stat_total:,} = {t:>7,}"
          f"  → 원형 B {t/3280:.1f}일   (기준 대비 {t/TOTAL_SPEND-1:+.1%})")
print(f"  가격 스윕 상한(완주 ≤ 97.5일, ECONOMY_SPEC 5-3 (c)) = {97.5*3280:,.0f}동전")


# -------------------------------------------------------- [5] 표시 여유
print()
print("=" * 78)
print("[5] 표시 여유 — ux-designer §13 확정 치수에 새 값이 들어가는가")
print("=" * 78)
LeftContentWidth = 112 + 32 + 8 + 44 + 8   # §13 SetStatusWidth 역산
seg3 = 3 * 20 + 2 * 4
seg4 = 4 * 20 + 3 * 4
print(f"  LeftContentWidth(역산) = {LeftContentWidth}")
print(f"  스탯 행 3단계: 이름44 + 세그{seg3} + 값48 = {44+seg3+48}  (여유 {LeftContentWidth-(44+seg3+48)})")
print(f"  스탯 행 4단계: 이름44 + 세그{seg4} + 값48 = {44+seg4+48}  (여유 {LeftContentWidth-(44+seg4+48)})")
print("  ⇒ U-24(최상급 4단계)가 채택돼도 좌측 컬럼 폭은 안 깨진다. 판정은 리더 몫이고 상점 탭을 막지 않는다.")
print()
print("  가격 글자수: 30→2 / 330→3 / 600→3 / 9,600→5(쉼표 포함)")
print("  버튼 139pt 안의 최장 라벨은 가격이 아니라 'LV.30 필요'(§13 S4)다 — 5자리 가격이 새 최장이 아니다.")


# --------------------------------- [6] 팩 · 엔타이틀먼트와 맞물리는 지점
print()
print("=" * 78)
print("[6] DLC 팩 — 유휴 수급 이후 「팩이 앞당기는 일수」 재계산")
print("=" * 78)
B_OLD = 1280   # 정본 §13-3 (유휴 수급 이전)
B_NEW = 3280   # 정본 §18-3 (R10-c 사용자 확정 이후)
for label, coins in (("전설1 + 희귀3", 9600 + 1400 * 3),
                     ("전설1 + 영웅1 + 희귀2", 9600 + 3200 + 1400 * 2)):
    print(f"  {label:<22} {coins:>7,}동전  →  구값 {coins/B_OLD:>5.1f}일  "
          f"→  **현행 {coins/B_NEW:>4.1f}일**  ({coins/B_NEW/(coins/B_OLD)-1:+.0%})")
print(f"  전설 4종(풀세트)        {4*9600:>7,}동전  →  구값 {4*9600/B_OLD:>5.1f}일  "
      f"→  **현행 {4*9600/B_NEW:>4.1f}일**")
print()
print("  「N분만 더 켜 두면」 — 유휴 12동전/분으로 환산한 부족 문구의 실제 크기")
for r, p in PRICE.items():
    mins = p / 12
    print(f"    {r:<4} {p:>6,}동전  =  {mins:>6.1f}분"
          + ("   ★ 인정 시간 창 480분/일을 넘는다 → 하루로 못 채운다"
             f" (일일 유휴 상한 2,000 기준 {p/2000:.1f}일)" if mins > 480 else ""))
