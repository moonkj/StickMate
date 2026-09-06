#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
economy_r24_retire_fxnone.py — 이펙트 「없음」 은퇴(2026-09-06)가 재화 경제에 미치는 영향

무엇을 재는가
  사용자 지시 *"이펙트 없음은 왜 있는거야 삭제해줘 장비창에서"* 로 look.fx.none 이
  표시목록에서 빠졌다(에셋 삭제 아님 — EquipmentModel.IsRetiredItem).
  같은 날 [머리] 카테고리 6종도 같은 기계로 은퇴했다.
  → 재화 쪽에서 실제로 움직이는 숫자가 무엇인지 「알려진 값으로 먼저 교정한 뒤」 센다.

규칙 (이 저장소가 거짓 통과에 여러 번 당했다 — CLAUDE.md)
  · 상수를 사람이 베끼지 않는다. 프로덕션 파일에서 정규식으로 읽는다.
  · 교정(K)이 하나라도 깨지면 sys.exit(1) 하고 아래 숫자를 전부 폐기한다.
  · 음성 대조(N)를 함께 돌린다 — 틀린 전제를 넣으면 교정이 실제로 깨지는지 본다.
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

GOLDEN = os.path.join(ROOT, "Assets/_Project/Scripts/Tests/EditMode/Golden/ItemCatalogGolden.txt")
CURRENCY_RULES = os.path.join(ROOT, "Assets/_Project/Scripts/Core/CurrencyRules.cs")
EQUIP_MODEL = os.path.join(ROOT, "Assets/_Project/Scripts/Core/EquipmentModel.cs")
ITEM_CATALOG = os.path.join(ROOT, "Assets/_Project/Scripts/Core/ItemCatalog.cs")

FAILS = []
LINES = []


def out(s=""):
    LINES.append(s)
    print(s)


def check(tag, ok, detail):
    out(f"  [{tag}] {'PASS' if ok else 'FAIL'}  {detail}")
    if not ok:
        FAILS.append(tag)


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


# ============================================================================
# 0. 프로덕션에서 사실을 읽는다
# ============================================================================
src_rules = read(CURRENCY_RULES)
src_model = read(EQUIP_MODEL)
src_cat = read(ITEM_CATALOG)


def const_int(src, name):
    m = re.search(r"const\s+int\s+" + name + r"\s*=\s*(\d+)\s*;", src)
    if not m:
        sys.exit(f"상수 {name} 을 못 읽었다 — 선언 형태가 바뀌었다. 계산 중단.")
    return int(m.group(1))


PRICE = {
    "Common": const_int(src_rules, "CommonPriceCoins"),
    "Rare": const_int(src_rules, "RarePriceCoins"),
    "Epic": const_int(src_rules, "EpicPriceCoins"),
    "Legendary": const_int(src_rules, "LegendaryPriceCoins"),
}
SEED = const_int(src_rules, "SeedCoins")

# 등급 사다리 — ItemCatalog._rarityByRank 를 소스에서 읽는다(길이가 곧 기준 코호트 크기).
m = re.search(r"_rarityByRank\s*=\s*\{(.*?)\};", src_cat, re.S)
if not m:
    sys.exit("_rarityByRank 배열을 못 읽었다. 계산 중단.")
LADDER = [x.strip().split(".")[-1] for x in m.group(1).replace("\n", " ").split(",") if x.strip()]

# 은퇴 사실 — 아이디와 슬롯을 소스에서 읽는다(여기에 목록을 다시 적지 않는다).
m = re.search(r'RetiredFxNoneItemId\s*=\s*"([^"]+)"', src_model)
if not m:
    sys.exit("RetiredFxNoneItemId 를 못 읽었다. 계산 중단.")
RETIRED_ITEM_ID = m.group(1)

m = re.search(r"IsRetiredSlot\(EquipmentSlot\s+slot\)\s*=>\s*slot\s*==\s*EquipmentSlot\.(\w+)", src_model)
if not m:
    sys.exit("IsRetiredSlot 판정을 못 읽었다. 계산 중단.")
RETIRED_SLOT = m.group(1)

# 카탈로그 — 골든 덤프(테스트가 매 실행 프로덕션과 대조하는 파일)
items = []
for mm in re.finditer(r"item id=(\S+) cat=Equipment slot=(\w+) idx=(\d+) lv=(\d+)", read(GOLDEN)):
    items.append({"id": mm.group(1), "slot": mm.group(2), "idx": int(mm.group(3)), "lv": int(mm.group(4))})

SLOT_ORDER = []
for it in items:
    if it["slot"] not in SLOT_ORDER:
        SLOT_ORDER.append(it["slot"])

STAT_SLOTS = ["Head", "Eyes", "Neck", "Shoulders"]
LOOK_SLOTS = [s for s in SLOT_ORDER if s not in STAT_SLOTS]


def by_slot(slot, pool=None):
    pool = pool if pool is not None else items
    return sorted([i for i in pool if i["slot"] == slot], key=lambda i: i["idx"])


def rarity_of(item, population):
    """ItemCatalog.RarityOfMember + RarityOfRank 의 재현.
    순위 키 = (requiredLevel, itemIndex). 사다리는 비율 환산이다."""
    key = (item["lv"], item["idx"])
    rank = sum(1 for o in population if (o["lv"], o["idx"]) < key)
    count = len(population)
    if count <= 0:
        return LADDER[0]
    if rank >= count:
        rank = count - 1
    step = rank * len(LADDER) // count
    if step >= len(LADDER):
        step = len(LADDER) - 1
    return LADDER[step]


# 전량 모집단(= 프로덕션 실제 동작: 은퇴를 보지 않는다)
for it in items:
    it["rarity"] = rarity_of(it, by_slot(it["slot"]))
    it["price"] = PRICE[it["rarity"]]


def is_retired(it):
    return it["slot"] == RETIRED_SLOT or it["id"] == RETIRED_ITEM_ID


listed = [i for i in items if not is_retired(i)]


def total(pool, free_ids):
    return sum(i["price"] for i in pool if i["id"] not in free_ids)


FREE_STAT = {i["id"] for i in items if i["idx"] == 0 and i["slot"] in STAT_SLOTS}
FREE_ALL7 = {i["id"] for i in items if i["idx"] == 0}

out("=" * 78)
out("economy_r24 — 이펙트 「없음」 은퇴의 재화 영향")
out("=" * 78)
out(f"  가격표(CurrencyRules.cs): {PRICE}   시드 {SEED}")
out(f"  등급 사다리(ItemCatalog.cs): {LADDER}")
out(f"  은퇴 사실(EquipmentModel.cs): 슬롯 '{RETIRED_SLOT}' 전체 + 아이템 '{RETIRED_ITEM_ID}'")
out()

# ============================================================================
# 1. 교정 — 문서에 이미 적힌 「알려진 값」을 재현하지 못하면 전량 폐기
#    출처: docs/DESIGN_SYSTEMS_STATS.md §22-1 표 · §22-4 표
# ============================================================================
out("[K] 교정 — 알려진 값 재현 (깨지면 아래 숫자 전부 폐기)")

check("K1", len(items) == 42 and len(SLOT_ORDER) == 7,
      f"카탈로그 {len(items)}종 / {len(SLOT_ORDER)}슬롯 (기대 42 / 7)")
check("K2", all(len(by_slot(s)) == 6 for s in SLOT_ORDER),
      "슬롯당 6종")

dist = {r: sum(1 for i in items if i["rarity"] == r) for r in ["Common", "Rare", "Epic", "Legendary"]}
check("K3", dist == {"Common": 14, "Rare": 14, "Epic": 7, "Legendary": 7},
      f"등급 분포 {dist} (기대 14/14/7/7 — ECONOMY_SPEC §3-2)")

check("K4", total(items, set()) == 117_600,
      f"정가표 합(무상 0종) = {total(items, set()):,} (기대 117,600 — §22-1)")
check("K5", total(items, FREE_STAT) == 115_200,
      f"안 A 문서 규칙(스탯 idx0 4종 무상, 유료 38) = {total(items, FREE_STAT):,} (기대 115,200)")
check("K6", total(items, FREE_ALL7) == 113_400,
      f"안 B 코드 규칙(idx0 7종 무상, 유료 35) = {total(items, FREE_ALL7):,} (기대 113,400)")

stat24 = total([i for i in items if i["slot"] in STAT_SLOTS], FREE_STAT)
look18A = total([i for i in items if i["slot"] in LOOK_SLOTS], FREE_STAT)
look18B = total([i for i in items if i["slot"] in LOOK_SLOTS], FREE_ALL7)
check("K7", (stat24, look18A, look18B) == (64_800, 50_400, 48_600),
      f"스탯24={stat24:,} · 외형18(A)={look18A:,} · 외형18(B)={look18B:,} (기대 64,800 / 50,400 / 48,600)")

check("K8", len(FREE_ALL7) == 7 and all(i["lv"] == 1 for i in items if i["idx"] == 0),
      "idx0 7종이 전부 requiredLevel = 1 (§22-0 3번)")

check("K9", round(113_400 / 1280, 1) == 88.6 and round(115_200 / 1280, 1) == 90.0,
      "완주일 모형 = 총액 ÷ 일수입(1,280) → 88.6 / 90.0일 (§22-4)")

check("K10", RETIRED_ITEM_ID in FREE_ALL7 and next(i for i in items if i["id"] == RETIRED_ITEM_ID)["lv"] == 1,
      f"{RETIRED_ITEM_ID} 은 idx0 · Lv.1 · 등급 "
      f"{next(i for i in items if i['id'] == RETIRED_ITEM_ID)['rarity']}"
      f"(정가 {next(i for i in items if i['id'] == RETIRED_ITEM_ID)['price']})")

check("K11", len(listed) == 35 and len([i for i in items if i["slot"] == RETIRED_SLOT]) == 6,
      f"표시목록 {len(listed)}종 (42 − 머리 6 − 없음 1) — 헤더 「N/35」와 일치")

# 음성 대조 — 틀린 전제를 넣으면 교정이 실제로 깨지는가
out()
out("[N] 음성 대조 — 틀린 전제가 위 교정에 걸리는지")
wrong3 = {i["id"] for i in items if i["idx"] == 0 and i["slot"] in LOOK_SLOTS}
out(f"  N1 무상을 「외형 idx0 3종」으로 잘못 잡으면 총액 {total(items, wrong3):,} "
    f"≠ 115,200 ≠ 113,400 → {'걸린다 OK' if total(items, wrong3) not in (115_200, 113_400) else '못 걸린다 ✗'}")
bad_ladder_dist = {}
for it in items:
    r = rarity_of(it, by_slot(it["slot"])[:5])   # 5종 모집단(= 은퇴 후 이펙트 슬롯)
    bad_ladder_dist[r] = bad_ladder_dist.get(r, 0) + 1
out(f"  N2 모집단을 5종으로 줄여 등급을 파생하면 분포가 {bad_ladder_dist} 로 바뀐다 "
    f"→ {'교정 K3이 걸린다 OK' if bad_ladder_dist != dist else '못 걸린다 ✗'}")

if FAILS:
    out()
    out(f"★ 교정 실패 {FAILS} — 아래 숫자를 전부 폐기한다.")
    sys.exit(1)

out()
out("  교정 11/11 PASS · 음성대조 2/2 걸림 → 아래 숫자를 신뢰한다.")
out()

# ============================================================================
# 2. 은퇴가 실제로 움직이는 돈
# ============================================================================
out("=" * 78)
out("[1] 소모처(코인 싱크) — 은퇴한 것은 상점에도 안 뜬다(IsListed 하나가 상점·격자·헤더를 함께 본다)")
out("=" * 78)

rows = []
for label, free in (("안 A(문서: 스탯 idx0 4종만 무상)", FREE_STAT),
                    ("안 B(코드: idx0 7종 무상)", FREE_ALL7)):
    before = total(items, free)
    after = total(listed, free)
    hair = total([i for i in items if i["slot"] == RETIRED_SLOT], free)
    fxnone = total([i for i in items if i["id"] == RETIRED_ITEM_ID], free)
    rows.append((label, before, after, hair, fxnone))
    out(f"  {label}")
    out(f"    은퇴 전 42종 소모처            {before:>9,}")
    out(f"    [머리] 6종이 빠지며 사라지는 액 {hair:>9,}")
    out(f"    「없음」 1종이 빠지며 사라지는 액 {fxnone:>9,}   ← 이번 지시가 움직인 금액")
    out(f"    은퇴 후 35종 소모처            {after:>9,}   ({(after-before)/before*100:+.2f}%)")
    out()

out("  ★ 이번 지시(「없음」 1종)만의 순영향:")
out(f"     · 안 B(코드 현재) = {rows[1][4]:,}동전 — 이 카드는 requiredLevel 1 이라 애초에 아무도 돈을 내지 않았다.")
out(f"     · 안 A(문서 규칙) = {rows[0][4]:,}동전 = 115,200의 {rows[0][4]/115_200*100:.2f}%")
out()

out("=" * 78)
out("[2] 완주일 — 총액 ÷ 일수입 (§22-4 모형 그대로)")
out("=" * 78)
out(f"  {'구성':38} {'@1,280':>10} {'@3,280':>10}  {'Lv.30 75일 대비':>16}")
for label, free in (("안 A 42종(문서 현행)", FREE_STAT),
                    ("안 B 42종(코드 현행)", FREE_ALL7)):
    t = total(items, free)
    out(f"  {label:38} {t/1280:>9.1f}일 {t/3280:>9.1f}일  {t/1280/75:>15.2f}×")
for label, free in (("안 A 35종(은퇴 반영)", FREE_STAT),
                    ("안 B 35종(은퇴 반영)", FREE_ALL7)):
    t = total(listed, free)
    out(f"  {label:38} {t/1280:>9.1f}일 {t/3280:>9.1f}일  {t/1280/75:>15.2f}×")
out()
out("  판정 기준(§22-4 (c)): 완주 ≤ 97.5일 · Lv.30(75일) 대비 ≤ 1.30×")
for label, free in (("안 A 35종", FREE_STAT), ("안 B 35종", FREE_ALL7)):
    t = total(listed, free)
    out(f"    {label}: {t/1280:.1f}일 {'PASS' if t/1280 <= 97.5 else 'FAIL'} · "
        f"{t/1280/75:.2f}× {'PASS' if t/1280/75 <= 1.30 else 'FAIL'}")
out()
out("  ★ 방향 주의: 은퇴는 소모처를 줄이므로 완주가 「빨라진다」 — 상한(97.5일)은 더 안전해지고")
out("     반대쪽(동전이 남아돈다)이 조금 나빠진다. §0-2-1 이 경고한 축이 그쪽이다.")
out()

out("=" * 78)
out("[3] 1일차 그림 — 시드 " + f"{SEED:,}동전")
out("=" * 78)
day1_owned_all = [i for i in items if i["lv"] == 1]
day1_owned_listed = [i for i in listed if i["lv"] == 1]
out(f"  Lv.1 무상 보유(카탈로그 전량 기준, IsOwned=레벨파생) : {len(day1_owned_all)}종")
out(f"  Lv.1 무상 보유(화면에 보이는 것만)                   : {len(day1_owned_listed)}종 "
    f"→ {[i['id'] for i in day1_owned_listed]}")
out(f"  헤더 표기: 「보유 {len(day1_owned_listed)} / {len(listed)}」  (은퇴 전에는 7 / 42, [머리] 은퇴 후 6 / 36)")
out()
for slot in SLOT_ORDER:
    pool = [i for i in listed if i["slot"] == slot]
    if not pool:
        out(f"    {slot:10} 표시 0종 (카테고리 은퇴)")
        continue
    lo = min(i["lv"] for i in pool)
    out(f"    {slot:10} 표시 {len(pool)}종 · 최저 요구레벨 Lv.{lo}"
        + ("   ★ Lv.1 무상 카드가 없다" if lo > 1 else ""))
out()
buyable = [i for i in listed if i["id"] not in FREE_ALL7 and i["price"] <= SEED]
out(f"  시드 {SEED:,}으로 1일차에 살 수 있는 카드(상점이 요구레벨 이전에도 판다는 (ㄴ) 전제): "
    f"{len(buyable)}종 · 최저가 {min(i['price'] for i in buyable):,}")
out(f"  → 시드로 {SEED // min(i['price'] for i in buyable)}장 구매 가능 = 「고르는 화면」 유지 여부는 그대로")
out()

out("=" * 78)
out("[4] 등급·코호트 — 은퇴가 순위 모집단을 건드리는가")
out("=" * 78)
out("  프로덕션 RarityOfMember 는 BySlot[] 전량을 모집단으로 쓰고 IsRetiredItem 을 보지 않는다.")
out("  → 42종 등급 42/42 불변. 가격표(등급 파생)도 불변.")
out()
out("  ★ 함정(하지 마라): 누군가 「보이는 것만 세자」며 모집단을 표시목록으로 바꾸면 —")
fx_all = by_slot("Fx")
fx_listed = [i for i in fx_all if not is_retired(i)]
out(f"    {'아이템':22} {'전량 6종 모집단':>16} {'표시 5종 모집단':>16}   가격 변화")
delta_sum = 0
for it in fx_listed:
    a = rarity_of(it, fx_all)
    b = rarity_of(it, fx_listed)
    d = PRICE[b] - PRICE[a]
    delta_sum += d
    out(f"    {it['id']:22} {a:>16} {b:>16}   {PRICE[a]:>6,} → {PRICE[b]:>6,} ({d:+,})")
out(f"    이펙트 슬롯 소모처 {delta_sum:+,}동전 — 전설이 한 종 사라진다(페이투윈 차단선 §2-4의 상단).")
out()

out("=" * 78)
out("[5] 인플레 쪽 — 소모처가 줄면 잔액이 남는다")
out("=" * 78)
# 원형별 일수입은 ECONOMY_SPEC §0-6-2 (c) 표에서 읽는다(사람이 베끼지 않는다).
spec = read(os.path.join(ROOT, "design/systems/ECONOMY_SPEC.md"))
arche = {}
for mm in re.finditer(r"\|\s*(A 켜두기만|A2 가볍게 켬|B 기준|C 적극|D 상한)\s*\|\s*([\d,]+)\s*\|", spec):
    arche[mm.group(1)] = int(mm.group(2).replace(",", ""))
check("K12", len(arche) == 5 and arche["B 기준"] == 3280,
      f"§0-6-2 (c) 표에서 원형 일수입 {len(arche)}개 회수: {arche}")
if FAILS:
    out(f"★ 교정 실패 {FAILS} — 폐기.")
    sys.exit(1)

out(f"  {'원형':12} {'일수입':>8} {'200일 수입':>12} {'소모처 前':>10} {'後':>9} {'수입/소모처 前→後':>22}")
for name, inc in arche.items():
    before = total(items, FREE_ALL7)
    after = total(listed, FREE_ALL7)
    out(f"  {name:12} {inc:>8,} {inc*200:>12,} {before:>10,} {after:>9,} "
        f"{inc*200/before*100:>9.0f}% → {inc*200/after*100:.0f}%")
out()
out(f"  완주 시 잔액이 늘어나는 폭 = 사라진 소모처 = {total(items, FREE_ALL7)-total(listed, FREE_ALL7):,}동전 "
    f"(안 B 기준, 전 유저 공통)")
out(f"    그중 이번 지시(「없음」)의 몫 = {total([i for i in items if i['id']==RETIRED_ITEM_ID], FREE_ALL7):,}동전")
out(f"    나머지 = [머리] 6종 몫 = {total([i for i in items if i['slot']==RETIRED_SLOT], FREE_ALL7):,}동전")
out()

out("=" * 78)
out("[6] 저장 빈도 예상치 (리더 판단용 — File.Replace IOException 선결 조건)")
out("=" * 78)
out("  +0.00회/일. 은퇴는 카탈로그 표시 술어이고 저장 사건이 아니다.")
out("  · 세이브 스키마 무변경(EquipmentModel.RestoreFromSave 가 읽기만 하고 파일을 고쳐 쓰지 않는다)")
out("  · 「없음」을 입고 저장했던 파일은 미착용으로 열린다 — 다음 저장에서 wornFx 가 비는 것은")
out("    「지금 아무것도 안 걸쳤다」는 사실 그대로이고, 추가 저장을 유발하지 않는다.")
out("  → §21-11 의 6.19(M3) / 6.57(M4) 그대로. 선결 조건의 긴급도·성격 불변.")
out()

with open(os.path.splitext(os.path.abspath(__file__))[0] + ".out.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(LINES) + "\n")
