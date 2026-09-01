# -*- coding: utf-8 -*-
"""배율 0.60 실루엣 코너 붕괴 2건 수정안 + **배율 축 상시 검산**.

★ items.py / hair.py 는 **프로덕션 거울**이라 손대지 않는다(coder가 넣기 전에 하니스가
   '통과'라고 거짓말하면 안 된다). 여기서 사본에 패치를 얹어 재검산한다.

고치는 것 (리더 최우선 배정, 2026-09-01):
   BeanieCuff 1→2 / 5→0  0.3622 R = 0.84획@0.60   → 필요 ≥ 0.4298 R
   BeretBody  4→5        0.4243 R = 0.99획@0.60   → 필요 ≥ 0.4298 R
"""
import sys, os, math, copy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair
from rig import Shape

def W(s): return max(0.048*s, 2.0/35.25)/(0.22*s)
SCALES = [0.35, 0.50, 0.60, 0.75, 1.00, 1.50]

# ── 수정안 ────────────────────────────────────────────────────────────────
#  털모자 접힌 단: 옆벽을 **더 벌리고 더 내린다**. 관 밑변(±0.96,−0.06)은 손대지 않는다 —
#  그 두 점이 곧 커버선(BeanieBandTopRatio)이고, 건드리면 머리카락 자르기가 따라 움직인다(9-1절).
BEANIE_CUFF_NEW = [(-0.96,-0.06), (0.96,-0.06), (1.04,-0.54), (0.64,-0.64),
                   (-0.64,-0.64), (-1.04,-0.54)]
#  베레모: 앞 어깨점만 0.44 → 0.54로 올린다(한 점, y만). 뒤로 처진 비대칭(뒤 끝 −1.46)은 그대로.
BERET_BODY_NEW  = [(-1.46,-0.10), (-1.02, 0.62), (-0.20, 1.06), (0.62, 0.90),
                   ( 0.98, 0.54), ( 0.92, 0.02), (-0.30,-0.02)]

def patched():
    H = {k: [Shape(s.name, s.pts, s.loop, s.filled, s.tone) for s in v] for k, v in items.HEAD.items()}
    for s in H["털모자"]:
        if s.name == "BeanieCuff": s.pts = [(float(a), float(b)) for a, b in BEANIE_CUFF_NEW]
    for s in H["베레모"]:
        if s.name == "BeretBody": s.pts = [(float(a), float(b)) for a, b in BERET_BODY_NEW]
    #  테는 몸의 실재 꼭짓점을 받는다(좌표를 새로 적지 않는다 — 현행 규약).
    body = [s for s in H["베레모"] if s.name == "BeretBody"][0]
    for s in H["베레모"]:
        if s.name == "BeretRim": s.pts = [body.pts[5], body.pts[6], body.pts[0]]
    return H

def sweep(HEAD, label):
    print("── %s ──" % label)
    CATS = [("HEAD", HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
            ("BACK", items.BACK), ("HAIR", hair.SET)]
    for s in SCALES:
        w = W(s); v = []
        for cat, t in CATS:
            for nm, sh in t.items():
                for x in sh:
                    m = rig.rule_one(x, w)
                    if m: v.append("%s %s %s" % (cat, nm, x.name))
        print("   배율 %.2f  W=%.4f R  위반 %3d건%s" % (s, w, len(v), ("   마지막: " + v[-1]) if v else ""))
    lo, hi = 0.30, 1.00
    for _ in range(46):
        m = (lo + hi) / 2
        bad = any(rig.rule_one(x, W(m)) for cat, t in CATS for nm, sh in t.items() for x in sh)
        if bad: lo = m
        else: hi = m
    last = [(cat, nm, x.name, rig.rule_one(x, W(lo)))
            for cat, t in CATS for nm, sh in t.items() for x in sh if rig.rule_one(x, W(lo))]
    print("   ★ 규칙 1 위반 0이 되는 **최소 배율 = %.4f**  (출하 0.75까지 여유 %.4f)" % (hi, 0.75 - hi))
    print("     그 바로 아래에서 마지막까지 남는 것: %s" % (", ".join("%s %s %s" % (a, b, c) for a, b, c, d in last[:3])))
    return hi

sweep(items.HEAD, "현행")
print()
NEW = patched()
th = sweep(NEW, "수정안 (BeanieCuff 6점 · BeretBody 앞 어깨 1점)")

# ── 수정안이 다른 계약을 깨지 않는가 ────────────────────────────────────────
print()
print("── 수정안 부작용 점검 ──")
bad = 0
def chk(ok, msg):
    global bad
    print("   %s %s" % ("✓" if ok else "✗", msg))
    if not ok: bad += 1
for nm in ("털모자", "베레모"):
    sh = NEW[nm]; p = [q for s in sh for q in s.pts]
    t = max(q[1] for q in p); b = min(q[1] for q in p)
    chk(1.0 < t < 2.551, "%s 꼭대기 %.2f R (1.0 < y < 2.551)" % (nm, t))
    chk(b > -1.0, "%s 최저 %.2f R (턱 −1.0 위)" % (nm, b))
    chk(any(abs(q[0]) >= 0.85 and q[1] <= 0.05 for q in p), "%s 감쌈(|x|≥0.85 & y≤0.05)" % nm)
    for s in sh:
        chk(not (s.loop and rig.self_intersects(s.pts)), "%s %s 자기교차 없음" % (nm, s.name))
    chk(sum(1 for s in sh if s.tone == 1) == 1, "%s 보조색 정확히 1개" % nm)
    chk(2 <= len(sh) <= 4, "%s 정원 %d" % (nm, len(sh)))
# 커버선(관 밑변 두 점)이 안 움직였는가
cuff = [s for s in NEW["털모자"] if s.name == "BeanieCuff"][0]
crown = [s for s in NEW["털모자"] if s.name == "BeanieCrown"][0]
chk(cuff.pts[0] == crown.pts[0] and cuff.pts[1] == crown.pts[-1],
    "털모자 커버선(관 밑변 = 단 윗변) 두 점 그대로: %s / %s" % (cuff.pts[0], cuff.pts[1]))
brim = [s for s in NEW["베레모"] if s.name == "BeretBody"][0]
rim = [s for s in NEW["베레모"] if s.name == "BeretRim"][0]
chk(rim.pts[0] == brim.pts[5] and rim.pts[2] == brim.pts[0], "베레모 테 = 몸의 실재 꼭짓점(좌표 새로 안 적음)")
chk(abs(brim.pts[5][1] - 0.02) < 1e-9, "베레모 커버선 BeretBrimLineRatio +0.02 그대로")

# 쌍별 실루엣 차
pr = {k: rig.profile(NEW[k]) for k in NEW}
ks = list(NEW); worst = (None, 99)
for i in range(len(ks)):
    for j in range(i+1, len(ks)):
        v = rig.max_delta(pr[ks[i]], pr[ks[j]]) / rig.W
        if v < worst[1]: worst = ((ks[i], ks[j]), v)
chk(worst[1] > 1.0, "HEAD 쌍별 최소 실루엣 차 %.2f획 (%s vs %s) > 1.0" % (worst[1], *worst[0]))
# 카드 44px
ICON, FIT, IST = 44.0, 0.86, 1.7*44/40
for nm in ("털모자", "베레모"):
    pts = [q for s in NEW[nm] for q in s.pts]
    x0, y0, x1, y1 = rig.bounds(pts); k = ICON*FIT/max(x1-x0, y1-y0)
    for s in NEW[nm]:
        m = rig.rule_one(Shape(s.name, [(x*k, y*k) for x, y in s.pts], s.loop, s.filled, s.tone), IST)
        chk(m is None, "카드 44px %s %s%s" % (nm, s.name, "" if m is None else " — " + m))
print("   → 부작용 %d건" % bad)

# ── 수정 뒤 0.60에 남는 것이 전부 '장식'인가 ────────────────────────────────
print()
print("── 수정안 · 배율 0.60에 남는 9건 (전부 [선택] 디테일이어야 LOD로 끌 수 있다) ──")
CATS2 = [("HEAD", NEW), ("EYES", items.EYES), ("NECK", items.NECK), ("BACK", items.BACK), ("HAIR", hair.SET)]
ROLE = {"BeaniePom":"장식(폼폼)","SunglassBridge":"장식(코다리)","RoundBridge":"장식(코다리)",
        "MonocleEye":"장식(드러난 눈)","PatchEye":"장식(드러난 눈)","BowTieKnot":"장식(매듭)",
        "Bell":"장식(방울)","PackBuckle":"장식(버클)","HairPart":"장식(가르마)"}
for cat, t in CATS2:
    for nm, sh in t.items():
        for x in sh:
            m = rig.rule_one(x, W(0.60))
            if m:
                print("   %-5s %-8s %-16s %-14s %s" % (cat, nm, x.name, ROLE.get(x.name, "★ 실루엣!"), m))

print()
print("── 새 좌표의 변 길이 (배율별 획 배수) ──")
for nm, pts, tag in (("털모자 BeanieCuff", BEANIE_CUFF_NEW, "1→2 / 5→0"),
                     ("베레모 BeretBody",  BERET_BODY_NEW,  "4→5")):
    s = Shape("x", pts, True); p = s.pts; n = len(p)
    corner = [rig.turn_deg(p[(i-1) % n], p[i], p[(i+1) % n]) >= 45 for i in range(n)]
    print("   %s (%s가 문제였던 변)" % (nm, tag))
    for i in range(n):
        j = (i+1) % n; Ln = math.dist(p[i], p[j])
        if not (corner[i] and corner[j]): continue
        print("      %d→%d L=%.4f R  " % (i, j, Ln) + " · ".join("%.2f획@%.2f" % (Ln/W(sc), sc) for sc in SCALES))

# ── LOD를 넣었을 때의 진짜 바닥 — [선택] 디테일 9개를 뺀 '실루엣 전용' 최소 배율 ──
OPTIONAL = {"BeaniePom","SunglassBridge","RoundBridge","MonocleEye","PatchEye",
            "BowTieKnot","Bell","PackBuckle","HairPart","FedoraBand","StrawBand",
            "CrownRim","BeretRim","HairFringe","MonocleChain","WingSpine","PackStrap",
            "TieStripe","CapeFold","CapeFold2"}
def floor_scale(HEAD, skip=frozenset()):
    C = [("HEAD",HEAD),("EYES",items.EYES),("NECK",items.NECK),("BACK",items.BACK),("HAIR",hair.SET)]
    lo, hi = 0.20, 1.20
    for _ in range(48):
        m=(lo+hi)/2
        bad=any(rig.rule_one(x, W(m)) for c,t in C for nm,sh in t.items() for x in sh if x.name not in skip)
        if bad: lo=m
        else: hi=m
    last=[(c,nm,x.name,rig.rule_one(x,W(lo))) for c,t in C for nm,sh in t.items()
          for x in sh if x.name not in skip and rig.rule_one(x,W(lo))]
    return hi, last
print()
print("── 위반 0 최소 배율 (4가지 조건) ──")
for lbl, H, sk in (("현행 · 전부", items.HEAD, frozenset()),
                   ("수정안 · 전부", NEW, frozenset()),
                   ("현행 · [선택] 디테일 제외(LOD 가정)", items.HEAD, OPTIONAL),
                   ("수정안 · [선택] 디테일 제외(LOD 가정)", NEW, OPTIONAL)):
    th, last = floor_scale(H, sk)
    print("   %-38s %.4f   마지막: %s" % (lbl, th, ", ".join("%s %s %s"%(a,b,c) for a,b,c,d in last[:2])))
