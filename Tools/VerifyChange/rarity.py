#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify-change 독립 재현 — ItemCatalog.RarityOfMember / RarityOfRank 를 파이썬으로 다시 쓴다.
★ 규칙: 계산기는 알려진 값으로 먼저 교정한다. 교정이 깨지면 그 뒤 숫자는 전부 폐기.
   교정 표본은 저장소가 아니라 여기 문자열로 박는다(생성기·검사기 동시 오류 방지).
"""
import os, re, sys, glob

LADDER = ["Common","Common","Rare","Rare","Epic","Legendary"]   # _rarityByRank (길이 6)
BASE_COHORT = 0

def rarity_of_rank(rank, count):
    if count <= 0: return "Common"
    ladder = len(LADDER)
    if rank < 0: rank = 0
    if rank >= count: rank = count - 1
    step = rank * ladder // count          # C# 정수 나눗셈 (양수뿐이라 동일)
    if step >= ladder: step = ladder - 1
    return LADDER[step]

def rarity_of_member(pop, index):
    """pop = [ (cohort, requiredLevel|None) 또는 None ]"""
    if pop is None or index < 0 or index >= len(pop): return "Common"
    mine = pop[index]
    if mine is None: return "Common"
    cohort, _ = mine
    key = pop[index][1] or 0
    rank = 0; counted = 0
    for i, other in enumerate(pop):
        if other is None or other[0] != cohort: continue
        counted += 1
        if i == index: continue
        okey = other[1] or 0
        if okey < key or (okey == key and i < index): rank += 1
    return rarity_of_rank(rank, counted)

# ---------------- 교정 (알려진 값) ----------------
def calibrate():
    fails = []
    # (1) 사다리 그 자체: 6종 코호트에서 rank 0..5 -> 일반/일반/희귀/희귀/영웅/전설 (ECONOMY_SPEC §3-2)
    want = ["Common","Common","Rare","Rare","Epic","Legendary"]
    got  = [rarity_of_rank(r, 6) for r in range(6)]
    if got != want: fails.append(f"6종 사다리 {got} != {want}")
    # (2) 코호트 1종이면 무조건 일반(rank 0)
    if rarity_of_rank(0,1) != "Common": fails.append("1종 코호트")
    # (3) 12종이면 비율 환산 — rank 8 -> step 8*6//12 = 4 -> Epic
    if rarity_of_rank(8,12) != "Epic": fails.append("12종 rank8")
    # (4) 범위 밖 rank 는 마지막으로 클램프
    if rarity_of_rank(99,6) != "Legendary": fails.append("클램프")
    return fails

# ---------------- 애셋 읽기 ----------------
def load_assets(folder):
    items = []
    for p in sorted(glob.glob(os.path.join(folder, "*.asset"))):
        src = open(p, encoding="utf-8", errors="replace").read()
        def g(k, cast=str):
            m = re.search(rf"^  {k}: (.*)$", src, re.M)
            return None if m is None else cast(m.group(1).strip())
        slot = g("slot", int); idx = g("itemIndex", int)
        lvl  = g("requiredLevel", int); iid = g("itemId")
        if slot is None or idx is None: continue
        items.append(dict(file=os.path.basename(p), id=iid, slot=slot, index=idx, level=lvl))
    return items

SLOTNAME = {0:"Head",1:"Eyes",2:"Neck",3:"Shoulders",4:"Hair",5:"Fx",6:"Pet"}  # ★ 2026-09-02 수정: EquipmentModel.cs 실측상 Fx=5, Pet=6 (구판은 뒤집혀 있었다)

def build_populations(items, extra=None):
    """slot -> [ (cohort, level) ... ] index 순"""
    pops = {}
    allitems = items + (extra or [])
    for it in allitems:
        pops.setdefault(it["slot"], {})[it["index"]] = (it.get("cohort", BASE_COHORT), it["level"])
    out = {}
    for s, d in pops.items():
        n = max(d)+1
        out[s] = [d.get(i) for i in range(n)]
    return out

def report(items, extra=None, title=""):
    pops = build_populations(items, extra)
    res = {}
    for it in items:
        r = rarity_of_member(pops[it["slot"]], it["index"])
        res[it["id"]] = r
    from collections import Counter
    c = Counter(res.values())
    print(f"--- {title}: 기본 42종 등급 분포 {dict(c)}")
    return res

DEFAULT_FOLDER = "Assets/_Project/Resources/Items"

def main(argv):
    # ---- 인자 검증 (2026-09-02 수리: 구판은 argv[1] 을 그냥 읽어 IndexError 로 죽었다.
    #      단독 실행 rc 는 1이었지만 파이프에 물리면 rc=0 이 되어 "돌았는데 조용했다"로 읽힌다.) ----
    if len(argv) > 1 or (argv and argv[0] in ("-h", "--help")):
        print(f"usage: rarity.py [아이템 애셋 폴더]   (기본값 {DEFAULT_FOLDER})", file=sys.stderr)
        return 2
    folder = argv[0] if argv else DEFAULT_FOLDER
    if not os.path.isdir(folder):
        print(f"★ 폴더가 없다: {folder} — 측정 무효", file=sys.stderr); return 2

    fails = calibrate()
    if fails:
        print("교정 실패 — 이후 숫자 전부 폐기:")
        for f in fails: print("   ", f)
        return 2
    print("교정 통과: 6종 사다리 / 1종 / 12종 비율환산 / 클램프")

    items = load_assets(folder)
    # ★ 0건은 "깨끗하다"가 아니라 "프로브가 죽었다"일 수 있다. 여기서 끊는다.
    if not items:
        print(f"★ {folder} 에서 파싱된 아이템 0건 — 정규식이 죽었을 수 있다. 측정 무효", file=sys.stderr)
        return 3
    print(f"애셋 {len(items)}건, 슬롯 {sorted(set(i['slot'] for i in items))}")
    base = report(items, None, "코호트 고정 (지금 트리)")
    for s in sorted(set(i["slot"] for i in items)):
        row = sorted([i for i in items if i["slot"]==s], key=lambda x:x["index"])
        print(f"  slot {s} {SLOTNAME.get(s,'?'):9s} " +
              " ".join(f"{i['index']}:{(i['level'] or 0):>2}->{base[i['id']][:4]}" for i in row))

    # ---- C-2 지뢰 실증: 팩 6종을 '코호트 필드 없이' 같은 슬롯에 넣으면? ----
    pack = [dict(file="pack", id=f"pack.head.{k}", slot=0, index=6+k, level=lv)
            for k, lv in enumerate([2,4,6,8,10,12])]
    after = report(items, pack, "팩 6종 추가 + cohortId 미전달(현재 코드 경로)")
    moved = [i for i in items if base[i["id"]] != after[i["id"]]]
    print(f"  ★ 등급이 움직인 기본 아이템: {len(moved)}건")
    for i in moved: print(f"      {i['id']:26s} {base[i['id']]:9s} -> {after[i['id']]}")

    # ---- 대조: 팩이 제 코호트(1)를 받았다면? ----
    pack2 = [dict(**p, cohort=1) for p in pack]
    after2 = report(items, pack2, "팩 6종 추가 + cohortId=1 (설계 의도)")
    moved2 = [i for i in items if base[i["id"]] != after2[i["id"]]]
    print(f"  ★ 등급이 움직인 기본 아이템: {len(moved2)}건")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
