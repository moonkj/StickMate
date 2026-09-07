# -*- coding: utf-8 -*-
"""§7 — 결선(안1 사파이어 199 vs 안2 에메랄드 138) 가름자 3개 + 3형 0.00 규명."""
import sys
import colorlab as C
import cvd
import pack78 as P

C.calibrate(); cvd.calibrate(); print()
packs = P.parse_frozen_packs()
hues6 = sorted(h for _, h, _, _ in packs)

# ---------------------------------------------------------------- 3형 0.00 규명
print("=" * 100)
print("§7-0. ★ 3형(청색맹) 교차 ΔE 0.00 은 진짜 붕괴인가, 게이머(클리핑) 착시인가")
print("=" * 100)
neon = C.hex2rgb("#CC1BA9")
neon2 = C.hex2rgb("#9C5A8E")
miner = P.pick(33.0, True)
miner2 = P.pick(33.0, False)
for nm, a in (("네온 낙서 주 #CC1BA9", neon), ("네온 낙서 보 #9C5A8E", neon2),
              ("광부 주 " + C.rgb2hex(miner), miner), ("광부 보 " + C.rgb2hex(miner2), miner2)):
    t = cvd.sim(a, "tritan")
    clipped = any(v <= 0 or v >= 255 for v in t)
    print(f"  {nm:28s} -> 3형 {C.rgb2hex(t)}  {'★채널 포화(클리핑 의심)' if clipped else ''}")
print("  ⇒ 두 색이 같은 포화점으로 잘려 나가면 ΔE 0.00 은 '같아 보인다'가 아니라 '모델이 잘랐다'다.")
print("    판정: 아래 미포화 경로로 다시 잰다(선형 LMS 평면 위 거리, 클램프 이전).")

def tritan_unclipped(rgb):
    return cvd.lms_to_rgb(cvd._mul(cvd.DICHROMAT["tritan"], cvd.rgb_to_lms(rgb)), quantize=False)

for nm, a, b in (("네온주↔광부주", neon, miner), ("네온보↔광부보", neon2, miner2),
                 ("네온주↔광부보", neon, miner2), ("네온보↔광부주", neon2, miner)):
    ta, tb = tritan_unclipped(a), tritan_unclipped(b)
    print(f"  {nm:14s} 클램프 전 3형 선형RGB 차 "
          f"({ta[0]-tb[0]:+.4f}, {ta[1]-tb[1]:+.4f}, {ta[2]-tb[2]:+.4f})")

# ---------------------------------------------------------------- 가름자
print()
print("=" * 100)
print("§7-1. 가름자 (가) 나침반 균등도 — 8각 간격의 표준편차")
print("=" * 100)
def gapstats(extra):
    hs = sorted(hues6 + [float(x) for x in extra])
    gs = [ (hs[(i+1) % len(hs)] - hs[i]) % 360 for i in range(len(hs)) ]
    m = sum(gs) / len(gs)
    var = sum((g - m) ** 2 for g in gs) / len(gs)
    return gs, m, var ** 0.5
for label, extra in (("현행 6팩", []), ("안1 33+199", [33, 199]), ("안2 33+138", [33, 138])):
    gs, m, sd = gapstats(extra)
    print(f"  {label:14s} 간격 {[f'{g:.0f}' for g in gs]}  평균 {m:.1f}° 표준편차 {sd:5.2f}°  "
          f"최소 {min(gs):.0f}° 최대 {max(gs):.0f}°")

print()
print("=" * 100)
print("§7-2. 가름자 (나) 2형(녹색맹) — 가장 흔한 색각이상에서 새 팩이 무엇과 붙는가")
print("=" * 100)
def plan_pairs(extra_defs, kind=None):
    ps = [(n, p, s) for n, h, p, s in packs] + extra_defs
    def T(c):
        return c if kind is None else cvd.sim(c, kind)
    rows = []
    for i in range(len(ps)):
        for j in range(i + 1, len(ps)):
            d = min(C.dE(T(a), T(b)) for a in (ps[i][1], ps[i][2]) for b in (ps[j][1], ps[j][2]))
            rows.append((d, ps[i][0], ps[j][0]))
    rows.sort()
    return rows

for label, h2 in (("안1 대마법사 199°", 199), ("안2 대마법사 138°", 138)):
    ext = [("광부", P.pick(33.0, True), P.pick(33.0, False)),
           ("대마법사", P.pick(float(h2), True), P.pick(float(h2), False))]
    for kind, kname in ((None, "정상"), ("deutan", "2형(녹색맹)"), ("protan", "1형(적색맹)")):
        rows = plan_pairs(ext, kind)
        mine = [r for r in rows if "대마법사" in (r[1], r[2])][:3]
        print(f"  {label} · {kname:10s} 대마법사가 가장 가까운 3쌍: "
              + " | ".join(f"{a if b=='대마법사' else b} ΔE {d:.2f}" for d, a, b in mine))
    print()

print("=" * 100)
print("§7-3. 가름자 (다) 이미 파랑을 쓰는 크롬 토큰과의 거리 (예약색 CardBorderWorn #5DA1F5)")
print("=" * 100)
res = P.parse_reserved()
for label, h2 in (("안1 199° 사파이어", 199), ("안2 138° 에메랄드", 138)):
    p, s = P.pick(float(h2), True), P.pick(float(h2), False)
    for rn, rc in res.items():
        print(f"  {label:16s} 주 {C.rgb2hex(p)} ↔ {rn} {C.rgb2hex(rc)} ΔE {C.dE(p, rc):6.2f}  ·  "
              f"보 {C.rgb2hex(s)} ↔ ΔE {C.dE(s, rc):6.2f}")
    # 오피스 주색과의 거리(같은 파랑 가족인가)
    off = [pp for n, h, pp, ss in packs if n == "오피스 워커"][0]
    print(f"  {label:16s} ↔ 오피스 주색 #456ECC ΔE {C.dE(p, off):6.2f}")
