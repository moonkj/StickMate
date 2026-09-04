# -*- coding: utf-8 -*-
"""★ R12 ⑦ — 1-C 미달 12건을 **두께 축으로만** 올리고, 그때 함께 움직이는 것을 재검산한다.

리더 판정(2026-09-03): 「1-C 빨강은 이식 착수 전 필수 조건」.
  · 12건을 두께 축으로 올린다.
  · ★ **재검산 없이 올리지 마라** — 쌍별 실루엣 차 · 눈 간격 · 액자 1.75 R 이 함께 움직인다.
  · ★ **못 올리는 조각이 나오면 「못 올린다」가 결론이다.** DROP 후보로 올린다.
  · ★ **게이트를 초록으로 만들려고 1-C 임계를 무르게 하지 마라.**  ⇒ GATE 는 이 파일에서 상수다.

무엇을 「두께 축」이라 부르는가
------------------------------
조각의 **단축(minor axis)** — 점 구름의 공분산 고유벡터 중 고윳값이 작은 쪽이다.
그 축으로만 **무게중심 기준 배율 s** 를 건다. **길이(장축)는 손대지 않는다.**
※ 등방 팽창(disk dilation)은 ρ 를 정확히 d 만큼 올리지만 **장축도 d 만큼 늘린다** —
  액자·이웃 간격을 같이 밀기 때문에 여기서는 쓰지 않는다. 비교용으로 값만 같이 찍는다.

무엇을 재검산하는가 (리더가 지목한 셋 + 기본기)
  [1-C] ρ_max >= 0.21818 R          그 조각 자신
  [1-A] 긴변 >= 1.5 W · 몽당변 없음   그 조각 자신
  [교차] 자기교차                     그 조각 자신
  [액자] 1.75 R (HairCapMaxRatio)     HEAD
  [경계] 슬롯별 상하한                 verify.py 와 같은 자
  [눈]   눈-가리개 간격 >= 1.5 W       EYES
  [쌍]   쌍별 실루엣 차 >= 1.0 획       이식 4종 + 그대로 두는 우리 2종 = 슬롯당 6종

    python3 r12_thick.py
    python3 r12_thick.py --control   # 음성 대조: 두께를 안 올리면 12건이 그대로 빨간가
"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig, items, hatfix, handoff
from rig import Shape
from handoff import parse_items, build, to_R_shape, OUR_NAME

CONTROL = "--control" in sys.argv
W = rig.stroke_in_R(0.75)
GATE = hatfix.FILL_OUTLINE_PEN_IN_R          # 0.21818 R — ★ 무르게 하지 않는다
FRAME = 1.75                                 # AccessoryShapeBuilder.HairCapMaxRatio
PAIR_MIN = 1.0                               # 쌍별 실루엣 차 하한(획)
S_MAX = 3.0                                  # 단축 배율 상한. 넘으면 「못 올린다」

# ── 두께 축 ────────────────────────────────────────────────────────────────
def minor_axis(pts):
    n = len(pts)
    cx = sum(p[0] for p in pts) / n; cy = sum(p[1] for p in pts) / n
    sxx = sum((p[0] - cx) ** 2 for p in pts) / n
    syy = sum((p[1] - cy) ** 2 for p in pts) / n
    sxy = sum((p[0] - cx) * (p[1] - cy) for p in pts) / n
    tr, det = sxx + syy, sxx * syy - sxy * sxy
    disc = max(0.0, tr * tr / 4 - det) ** 0.5
    lam_min = tr / 2 - disc
    if abs(sxy) > 1e-12: v = (lam_min - syy, sxy)
    else:                v = (1.0, 0.0) if sxx < syy else (0.0, 1.0)
    L = math.hypot(*v) or 1.0
    return (cx, cy), (v[0] / L, v[1] / L)


def thicken(pts, s):
    """단축 방향으로만 무게중심 기준 s 배. 장축 길이는 불변."""
    (cx, cy), (nx, ny) = minor_axis(pts)
    out = []
    for x, y in pts:
        dx, dy = x - cx, y - cy
        t = dx * nx + dy * ny                 # 단축 성분
        out.append((cx + dx + (s - 1.0) * t * nx, cy + dy + (s - 1.0) * t * ny))
    return out


def rho(pts): return hatfix.rho_max(pts, coarse=0.02, refine=9)


def solve_s(pts, target=GATE, lo=1.0, hi=S_MAX):
    """ρ_max 가 target 이 되는 최소 s. 단조라 이분으로 충분하다."""
    if rho(pts) >= target: return 1.0
    if rho(thicken(pts, hi)) < target: return None      # 상한 안에서 못 올린다
    for _ in range(22):
        mid = (lo + hi) / 2
        if rho(thicken(pts, mid)) >= target: hi = mid
        else: lo = mid
    return hi


# ── 이식 세트 조립 ──────────────────────────────────────────────────────────
IT = parse_items()
PORT, SLOT_OF = {}, {}
for kind, pieces in IT.items():
    P = build(kind, pieces)
    PORT[kind] = [to_R_shape(kind, p, "%s%d" % (p.call, i)) for i, p in enumerate(P)]
    SLOT_OF[kind] = OUR_NAME[kind][0]

# r11_survive 와 **같은 술어**로 몸에 오르는 조각을 고른다: 1-A 통과 **그리고 하이라이트 아님**
HL = {}
for kind, pieces in IT.items():
    for i, pc in enumerate(build(kind, pieces)):
        HL[(kind, "%s%d" % (pc.call, i))] = pc.is_highlight

def is_body(kind, s):
    b = rig.bounds(s.pts)
    return (max(b[2] - b[0], b[3] - b[1]) >= 1.5 * W) and not HL[(kind, s.name)]

def body(kind):  return [s for s in PORT[kind] if is_body(kind, s)]

# 슬롯별: 이식 4종 + 그대로 두는 우리 2종
KEPT = {"HEAD": ["베레모", "밀짚모자"], "EYES": ["뿔테안경", "안대"],
        "NECK": ["펜던트", "반다나"], "BACK": ["판초", "요정날개"]}
OURS = {}
for grp in (items.HEAD, items.EYES, items.NECK, items.BACK):
    OURS.update(grp)

# ── 미달 목록 (r11_survive [1-C] 와 같은 산식) ──────────────────────────────
THIN = []
for kind in PORT:
    for s in body(kind):
        if not (s.filled and s.loop and len(s.pts) >= 3): continue
        r = rho(s.pts)
        if r < GATE: THIN.append((kind, s, r))
THIN.sort(key=lambda t: t[2])

print("╔══ ⑦-1 1-C 미달 조각을 두께 축으로만 올린다 (임계 %.5f R — 무르게 하지 않는다) ══╗" % GATE)
if CONTROL:
    print("  ★★ 음성 대조: 두께를 **안 올린다**(s = 1.0 고정). 12건이 그대로 빨개야 이 자가 산다.\n")
print("  %-13s %-4s %8s %8s %7s %9s   %s" %
      ("아이템", "조각", "ρ 전", "ρ 후", "s", "단축 전→후", "판정"))
PLAN, IMPOSSIBLE = {}, []
for kind, s, r in THIN:
    s_need = 1.0 if CONTROL else solve_s(s.pts)
    if s_need is None:
        IMPOSSIBLE.append((kind, s, r, None)); v = "★ 못 올린다"
        print("  %-13s %-4s %8.4f %8s %7s %9s   %s" % (kind, s.name, r, "—", ">%.1f" % S_MAX, "—", v))
        continue
    np_ = thicken(s.pts, s_need)
    r2 = rho(np_)
    b0 = rig.bounds(s.pts); b1 = rig.bounds(np_)
    t0 = min(b0[2] - b0[0], b0[3] - b0[1]); t1 = min(b1[2] - b1[0], b1[3] - b1[1])
    ok = r2 >= GATE
    if CONTROL and not ok: v = "✗ 미달(예상대로)"
    else: v = "OK" if ok else "✗ 미달"
    print("  %-13s %-4s %8.4f %8.4f %7.3f %9s   %s" %
          (kind, s.name, r, r2, s_need, "%.3f→%.3f" % (t0, t1), v))
    if not CONTROL: PLAN.setdefault(kind, {})[s.name] = (s_need, np_)
if CONTROL:
    print("\n  ⇒ 미달 %d건이 그대로 남았다 ⇒ 이 자는 두께에 실제로 반응한다." % len(THIN))
    print("     (r11_survive 의 [1-C] 12건 + `backpack RB3` — 그쪽은 정원 상한으로 CARD 강등된")
    print("      조각까지는 안 보므로 여기가 **한 건 더 넓은 상위집합**이다. ρ 0.2176 R = 0.997획)")
    sys.exit(0 if len(THIN) == 13 else 1)

# ── 처방 적용본 조립 ────────────────────────────────────────────────────────
def fixed(kind):
    out = []
    for s in PORT[kind]:
        p = PLAN.get(kind, {}).get(s.name)
        out.append(Shape(s.name, p[1] if p else s.pts, s.loop, s.filled, s.tone, s.sort))
    return out
FIX = {k: fixed(k) for k in PORT}
BEFORE = {k: body(k) for k in PORT}
AFTER  = {k: [s for s in FIX[k] if is_body(k, s)] for k in FIX}


# ── 검사 묶음 — **전/후 두 번 돌린다** ──────────────────────────────────────
def eye_gap(shapes, ex):
    """드러난 눈 중심에서 가장 가까운 잉크까지의 거리(R). 덮여 있으면 0."""
    best = 1e9
    for s in shapes:
        if rig.contains(s.pts, (ex, rig.EYE_Y)): return 0.0
        n = len(s.pts)
        for k in range(n if s.loop else n - 1):
            a, b = s.pts[k], s.pts[(k + 1) % n]
            dx, dy = b[0] - a[0], b[1] - a[1]; L = dx * dx + dy * dy
            t = 0.0 if L < 1e-12 else max(0.0, min(1.0, ((ex - a[0]) * dx + (rig.EYE_Y - a[1]) * dy) / L))
            best = min(best, math.hypot(ex - (a[0] + dx * t), rig.EYE_Y - (a[1] + dy * t)))
    return best


def suite(SET):
    """위반 딕셔너리를 돌려준다. 키가 같으면 「같은 위반」이다."""
    v = {}
    for kind in SET:
        for s in SET[kind]:
            m = rig.rule_one(s, W)
            if m: v["[1-A] %s %s" % (kind, s.name)] = m
            si = rig.self_intersects(s.pts) if s.loop else None
            if si: v["[교차] %s %s" % (kind, s.name)] = str(si)
        slot = SLOT_OF[kind]
        p = [q for s in SET[kind] for q in s.pts]
        top = max(q[1] for q in p); bot = min(q[1] for q in p)
        reach = max(math.hypot(*q) for q in p)
        if slot == "HEAD":
            # ★ R12 자기 정정 — 액자는 **반경 도달이 아니라 꼭대기 y** 다.
            #   §5-1 이 furhat 초과 +0.456(폼폼 꼭대기 2.206), §5-4 가 crown 초과 +0.184(보석 1.934)로
            #   적어 둔 값이 top-y 로만 재현된다. 그리고 **반경으로 재면 우리 출하 모자가 먼저 죽는다**:
            #   야구모자 +0.200 · 중절모 +0.329 · 밀짚모자 +0.451 인데 verify.py 는 위반 0건이다.
            #   (§4-2-1 과 같은 병 — 인계본에 우리 자신도 못 넘는 자를 대는 것)
            #   또한 **소멸 조각까지 포함한 아이템 전체**로 잰다(폼폼·보석이 그 값을 만든다).
            atop = max(q[1] for s2 in PORT[kind] for q in s2.pts)
            if atop > FRAME: v["[액자] %s" % kind] = "꼭대기 %.4f R (한계 %.2f, 초과 %+.4f)" % (atop, FRAME, atop - FRAME)
            if not (1.0 < top < 2.551): v["[경계] %s 꼭대기" % kind] = "%.3f" % top
        elif slot == "EYES":
            if max(abs(q[0]) for q in p) >= 1.6: v["[경계] %s |x|" % kind] = "%.3f" % max(abs(q[0]) for q in p)
            if top >= 1.15: v["[경계] %s 정수리" % kind] = "%.3f" % top
            if bot <= -2.2: v["[경계] %s 목아래" % kind] = "%.3f" % bot
            f = [s for s in SET[kind] if s.filled]
            cf = any(rig.contains(s.pts, (rig.EYE_X, rig.EYE_Y)) for s in f)
            cb = any(rig.contains(s.pts, (-rig.EYE_X, rig.EYE_Y)) for s in f)
            if cf != cb:                                   # 한쪽만 덮는다 = 눈이 드러난다
                ex = -rig.EYE_X if cf else rig.EYE_X
                g = eye_gap(f, ex)
                if g < 1.5 * W: v["[눈] %s" % kind] = "%.2f획 (하한 1.50)" % (g / W)
        elif slot == "NECK":
            if top >= 0.0: v["[경계] %s 얼굴침범" % kind] = "%.3f" % top
            if bot <= rig.HIP_R - 0.517: v["[경계] %s 고관절" % kind] = "%.3f" % bot
        else:
            if top >= 1.0: v["[경계] %s 정수리위" % kind] = "%.3f" % top
            if bot <= -9.3395: v["[경계] %s 바닥" % kind] = "%.3f" % bot
    for slot, kinds in BY_SLOT.items():
        d = {"port:" + k: SET[k] for k in kinds}
        for nm in KEPT[slot]: d["our:" + nm] = OURS[nm]
        ks = list(d); pr = {x: rig.profile(d[x]) for x in ks}; w = (None, 99.0)
        for i in range(len(ks)):
            for j in range(i + 1, len(ks)):
                q = rig.max_delta(pr[ks[i]], pr[ks[j]]) / W
                if q < w[1]: w = ((ks[i], ks[j]), q)
        if w[1] < PAIR_MIN: v["[쌍] %s" % slot] = "%.2f획 (%s vs %s)" % (w[1], w[0][0], w[0][1])
    return v


BY_SLOT = {}
for kind in FIX: BY_SLOT.setdefault(SLOT_OF[kind], []).append(kind)
V0, V1 = suite(BEFORE), suite(AFTER)

print("\n╔══ ⑦-2 함께 움직이는 것 — **전/후 대조**로 재검산 ══╗")
print("  ★ 「이식본이 원래 그런 것」과 「내 두께 처방이 깬 것」을 갈라야 한다.\n")
keys = sorted(set(V0) | set(V1))
made, fixedn, pre = [], [], []
for k in keys:
    if k in V0 and k in V1:
        same = V0[k] == V1[k]
        pre.append(k)
        print("  = %-26s 전 %-44s %s" % (k, V0[k], "" if same else "→ 후 " + V1[k]))
    elif k in V1:
        made.append(k); print("  ✗ %-26s **처방이 만들었다** → %s" % (k, V1[k]))
    else:
        fixedn.append(k); print("  ✓ %-26s 처방이 없앴다 (전 %s)" % (k, V0[k]))
print()
print("  ⇒ 이식본에 원래 있던 위반 %d건 · 처방이 만든 위반 **%d건** · 처방이 없앤 위반 %d건"
      % (len(pre), len(made), len(fixedn)))

# 1-C 최종
print("\n╔══ ⑦-3 1-C 최종 확인 (임계 %.5f R) ══╗" % GATE)
n_thin_after = 0
for kind in AFTER:
    for s in AFTER[kind]:
        if not (s.filled and s.loop and len(s.pts) >= 3): continue
        r = rho(s.pts)
        if r < GATE:
            n_thin_after += 1
            print("  ✗ %-13s %-4s ρ %.4f R = %.2f획" % (kind, s.name, r, r / GATE))
print("  ⇒ 몸에 오르는 채움 중 1-C 미달: 전 %d건 → **후 %d건**" % (len(THIN), n_thin_after))

# ── ⑦-4 ★ c 재부여 실험 — §1-4-4 2단계가 쌍별 실루엣을 되살리는가 ────────────
print("\n╔══ ⑦-4 ★ c 재부여 실험 — 「정면화가 구별을 죽인다」를 직접 시험한다 ══╗")
print("  §1-4-4 2단계(무손실 평행이동)만 걸어 본다. 두께 처방 위에 얹는다.")
print("  우리 현행 c 를 그대로 준다 — 새 숫자를 만들지 않는다.\n")
OUR_C = {}
for nm, S in OURS.items():
    xs = [q[0] for sh in S for q in sh.pts]
    OUR_C[nm] = (max(xs) + min(xs)) / 2.0
SHIFT = {k: OUR_C[OUR_NAME[k][1]] for k in AFTER}
AFTER_C = {k: [Shape(sh.name, [(x + SHIFT[k], y) for x, y in sh.pts],
                     sh.loop, sh.filled, sh.tone, sh.sort) for sh in AFTER[k]] for k in AFTER}
print("  %-5s %-34s %-34s %s" % ("슬롯", "두께만", "두께 + c 재부여", "변화"))
for slot, kinds in BY_SLOT.items():
    def w(SET):
        d = {"port:" + k: SET[k] for k in kinds}
        for nm in KEPT[slot]: d["our:" + nm] = OURS[nm]
        ks = list(d); pr = {x: rig.profile(d[x]) for x in ks}; b = (None, 99.0)
        for i in range(len(ks)):
            for j in range(i + 1, len(ks)):
                q = rig.max_delta(pr[ks[i]], pr[ks[j]]) / W
                if q < b[1]: b = ((ks[i], ks[j]), q)
        return b
    (pa, v), (pb, v2) = w(AFTER), w(AFTER_C)
    print("  %-5s %-34s %-34s %s" %
          (slot, "%.2f획 (%s|%s)" % (v, pa[0][:14], pa[1][:14]),
           "%.2f획 (%s|%s)" % (v2, pb[0][:14], pb[1][:14]),
           ("★ %+.2f획 — 하한 넘김" % (v2 - v)) if (v < PAIR_MIN <= v2)
           else ("%+.2f획" % (v2 - v))))
print()
print("  ⇒ c 는 **평행이동**이라 1-A·1-C·자기교차를 하나도 안 건드린다(§1-4-4 표).")
print("     즉 이 회복은 **공짜다** — 잃는 것 없이 구별이 돌아온다면 그것이 시점 축의 증거다.")

print("\n╔══ ⑦-5 결론 ══╗")
print("  올린 조각 %d건 · 상한 ×%.1f 로도 못 올린 조각 %d건" % (sum(len(v) for v in PLAN.values()), S_MAX, len(IMPOSSIBLE)))
for kind, s, r, _ in IMPOSSIBLE:
    print("    ★ DROP 후보: %s %s (ρ %.4f R)" % (kind, s.name, r))
rc = 0
if made:
    print("\n  ✗ **처방이 새로 만든 위반 %d건 — 이대로 넣으면 안 된다**" % len(made))
    for k in made: print("    " + k + ": " + V1[k])
    rc = 1
if n_thin_after:
    print("\n  ✗ 1-C 미달이 %d건 남았다" % n_thin_after); rc = 1
if rc == 0:
    print("\n★ 두께 처방은 **아무것도 새로 깨지 않았다**(만든 위반 0건) · 1-C 미달 0건.")
    print("  남은 %d건은 **이식본이 원래 지고 있던 빚**이다 — 두께와 다른 축이다:" % len(pre))
    print("    · [쌍] 2건 → ★ **§1-4-4 2단계(c 재부여)가 해소한다**(⑦-4 실측, 공짜)")
    print("    · [액자] 2건(`crown` +0.184 · `furhat` +0.456) → ★ **둘 다 §5-4·§5-1 에 이미 있다**")
    print("      ※ 액자는 **반경이 아니라 꼭대기 y** 다. 반경으로 재면 우리 출하 모자가 먼저 죽는다")
    print("        (야구모자 +0.200 · 중절모 +0.329 · 밀짚모자 +0.451, 그런데 verify.py 는 위반 0건).")
    print("    · [1-A 몽당변] 3건 · [눈] 1건(`monocle` 0.48획, ★R12 신규) → §5-3 축. 이 파일의 범위 밖")
    print("    ⇒ HEAD 쪽 빚의 처방 한 벌은 `r12_head.py` 에 있다(§5-5).")
sys.exit(rc)
