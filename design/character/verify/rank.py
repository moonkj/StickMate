# -*- coding: utf-8 -*-
"""팩 6종의 슬롯 분포가 등급 사다리에 무엇을 하는가.
구현 원본: Core/ItemCatalog.RarityOfRank / RarityOfMember (그대로 옮김, 값 베끼기 아님)
  step = rank * ladder / count   (정수 나눗셈)   ladder = 6
  모집단 = 같은 슬롯 AND 같은 코호트
"""
from itertools import product
LADDER = ['일반','일반','희귀','희귀','영웅','전설']
def rarity_of_rank(rank, count):
    if count <= 0: return '일반'
    rank = max(0, min(rank, count-1))
    return LADDER[min(rank*len(LADDER)//count, len(LADDER)-1)]

# 교정: 기본 42종은 슬롯당 6종 코호트 -> 2/2/1/1
base = [rarity_of_rank(r,6) for r in range(6)]
assert base == LADDER
def dist(cnt_list):
    out = []
    for c in cnt_list:
        for r in range(c): out.append(rarity_of_rank(r,c))
    d = {k: out.count(k) for k in ['일반','희귀','영웅','전설']}
    return d
assert dist([6]) == {'일반':2,'희귀':2,'영웅':1,'전설':1}
print("교정 통과 (슬롯당 6종 = 2/2/1/1)\n")

# 6을 슬롯들에 나누는 모든 방법(순서 무관 분할)
def partitions(n, maxpart=None):
    if maxpart is None: maxpart = n
    if n == 0: yield []
    for k in range(min(n,maxpart), 0, -1):
        for rest in partitions(n-k, k): yield [k]+rest

print(f"{'슬롯별 개수':<16}{'슬롯수':>5}  {'일반':>4}{'희귀':>4}{'영웅':>4}{'전설':>4}   2/2/1/1?")
best = []
for p in partitions(6):
    d = dist(p)
    ok = (d == {'일반':2,'희귀':2,'영웅':1,'전설':1})
    print(f"{str(p):<16}{len(p):>5}  {d['일반']:>4}{d['희귀']:>4}{d['영웅']:>4}{d['전설']:>4}   {'★ 예' if ok else '아니오'}")
    if ok: best.append(p)
print(f"\n2/2/1/1을 내는 분포: {best}")

# 4종 팩(ARCHITECTURE 5-3-2)이면?
print("\n== ARCHITECTURE 5-3-2 '모자/타이/안경/망토 4종' ==")
for p in ([4],[1,1,1,1],[2,1,1],[2,2]):
    d = dist(p); print(f"{str(p):<12} 일반{d['일반']} 희귀{d['희귀']} 영웅{d['영웅']} 전설{d['전설']}")
print("★ 4종은 어떤 배치로도 전설이 0개다(한 슬롯에 4종이어도 rank3 -> step=4 -> 영웅).")
