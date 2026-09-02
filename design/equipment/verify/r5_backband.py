# -*- coding: utf-8 -*-
"""★ 과제 B — 「BACK 층의 어깨 위가 통째로 비어 있다」를 **내 자로 다시 잰다.**

design-character 실측(DLC_PACK_R2_WOOCHEON_SPEC): 출하 6종 BACK 잉크 천장 −0.186 R(날개),
나머지는 −0.95 이하. 그쪽 숫자를 그대로 믿지 않고 **세 가지 방법으로** 다시 잰다.
그리고 그 대역을 내 BACK(연장 가방)이 쓸 수 있는지 / 쓰면 무엇이 좋아지는지 잰다.

  §1 천장 재측정 (꼭짓점식 / 주사선식 / 래스터식 — 서로 대조)
  §2 그 천장을 **무엇이** 만들었는가 (프로덕션 계약인가, 내 설계 관례인가)
  §3 대역의 **실제로 보이는** 넓이 — 머리 원반·몸통·팔·같이 쓰는 HAIR/HEAD 를 빼고
  §4 대역을 쓰는 BACK v2 를 만들어 이득과 대가를 잰다
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H, sectors as S
import pack_nightshift as P
from rig import Shape

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)
FRAME = 1.80          # CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR (초상화 액자)

# ── 본체 잉크 모형 — **프로덕션 상수에서 내가 직접 유도한다** ────────────────────
LWS = 1.045
TOTAL_H = 2.2746944
R0 = 0.22
TORSO_W = 0.11 * LWS / R0          # R 배수
ARM_W = 0.10 * LWS / R0
ARM_UP = 0.38 / R0
ARM_LO = 0.37 / R0
SPREAD, ELBOW = 40.0, 10.0         # CharacterPortraitStage.IdleArmSpreadDegrees / IdleElbowBend
SH = rig.SHOULDER_R
HIP = rig.HIP_R
TORSO_TOP = -1.0                   # 몸통 선은 턱까지


def _arm(sign):
    a = math.radians(SPREAD * sign); b = math.radians((SPREAD + ELBOW) * sign)
    ex, ey = ARM_UP * math.sin(a), SH - ARM_UP * math.cos(a)
    return [(0.0, SH), (ex, ey), (ex + ARM_LO * math.sin(b), ey - ARM_LO * math.cos(b))]
ARMS = [_arm(+1), _arm(-1)]


def body_shapes():
    """머리 원반 + 몸통 + 팔 2개. BACK(sort −1)은 이 전부의 **뒤**에 그려진다."""
    return ([Shape("Head", rig.arc(1.0, 0, 360, 361)[:-1], True, filled=True)]
            + [Shape("Torso", [(0.0, TORSO_TOP), (0.0, HIP)], False)]
            + [Shape("Arm%d" % i, a, False) for i, a in enumerate(ARMS)])


def body_ink_spans(y, w):
    """본체 잉크가 수평선 y 에서 차지하는 구간. 획 폭은 부위마다 다르다."""
    out = []
    for a, b in ((0.0, TORSO_TOP), (0.0, HIP)):
        pass
    sp = H._capsule_span((0.0, TORSO_TOP), (0.0, HIP), TORSO_W / 2, y)
    if sp: out.append(sp)
    for arm in ARMS:
        for i in range(len(arm) - 1):
            sp = H._capsule_span(arm[i], arm[i + 1], ARM_W / 2, y)
            if sp: out.append(sp)
    if abs(y) < 1.0:
        hw = math.sqrt(1 - y * y); out.append((-hw, hw))
    return H._merge(out)


# ═══════════════════════════════════════════════════════════════════════════
def s1_ceiling():
    print("╔══ §1 BACK 잉크 천장 — 세 방법으로 다시 잰다 ══╗")
    print("  방법1 꼭짓점식 : max(y) + W/2                (그쪽이 쓴 것으로 **추정**되는 식)")
    print("  방법2 주사선식 : ink_spans 가 비지 않는 최상단 y (이분법)")
    print("  방법3 래스터식 : 0.002R 격자에서 잉크가 찍힌 최상단 행")
    print("  %-8s %10s %10s %10s   %s" % ("아이템", "방법1", "방법2", "방법3", "일치"))
    tops = {}
    for n, f in items.BACK.items():
        sh = f() if callable(f) else f
        m1 = max(p[1] for s in sh for p in s.pts) + W75 / 2
        # 방법2 — **다른 코드 경로**를 쓴다: 구간대수(ink_spans)가 아니라 점 포함(_covered).
        # ★ 처음엔 이분법을 썼는데 날개·요정날개에서 −12(바닥)를 냈다. 잉크에 **세로 구멍**이
        #   있으면 이분법의 단조 가정이 깨진다 — 내 자의 결함이었고 여기 남긴다.
        m2 = None
        yy = 4.0
        while yy > -12.0 and m2 is None:
            for k in range(161):
                x = -4.0 + 8.0 * k / 160.0
                if H._covered(sh, (x, yy), W75): m2 = yy; break
            yy -= 0.002
        m3 = None
        y = 4.0
        while y > -12.0:
            if H.ink_spans(sh, y, W75): m3 = y; break
            y -= 0.002
        agree = "OK" if max(abs(m1 - (m2 or -99)), abs(m1 - (m3 or -99))) < 0.006 else "★어긋남"
        tops[n] = m1
        print("  %-8s %10.4f %10.4f %10.4f   %s" % (n, m1, m2 if m2 is not None else -99, m3, agree))
    top = max(tops.values()); who = max(tops, key=tops.get)
    second = sorted(tops.values())[-2]
    print("  ★ 출하 6종 천장 = %+.4f R (%s).  두 번째로 높은 것 %+.4f R" % (top, who, second))
    print("  ★ design-character 보고값 −0.186 R  ->  내 측정 %+.4f R  차 %.4f R  %s"
          % (top, abs(top + 0.186), "일치" if abs(top + 0.186) < 0.002 else "불일치"))
    return top


# ═══════════════════════════════════════════════════════════════════════════
def s2_source(top):
    print("\n╔══ §2 그 천장을 만든 것은 무엇인가 ══╗")
    print("  (a) 프로덕션 테스트에 BACK 의 y 상한을 거는 단언이 **있는가**")
    print("      -> Assets/.../Tests/EditMode 전수 grep 결과 **없다**. 확인된 상한은 하나뿐이다:")
    print("         CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR = %.2f R (초상화 액자)" % FRAME)
    print("  (b) 그럼 무엇이 %.2f 을(를) 막고 있었나 -> **내 설계 게이트다.**" % 1.0)
    print("      design/equipment/verify/verify.py:61  `if max(y) >= 1.0: bad(\"BACK 정수리 위\")`")
    print("      이 1.0 은 프로덕션 계약이 아니라 **내가 넣은 관례**다. 출하 6종이 −0.186 이하라")
    print("      한 번도 밟히지 않았고, 그래서 아무도 그것이 관례인 줄 몰랐다.")
    print("  ★ 즉 「어깨 위가 비어 있다」는 그쪽 발견은 참이고, **비운 것은 나다.**")
    print("     쓸 수 있는 여유 = %.4f  ..  %.2f  =  %.4f R (%.2f획@0.60)"
          % (top, FRAME, FRAME - top, (FRAME - top) / W60))


# ═══════════════════════════════════════════════════════════════════════════
def visible_area(sh, occ_spans_fn, w, y0, y1, n=1400):
    """sh 잉크 중 occ 에 안 가려진 면적(R²). occ_spans_fn(y) -> 구간 목록."""
    tot = vis = 0.0
    dy = (y1 - y0) / n
    for k in range(n):
        y = y0 + (k + 0.5) * dy
        sp = H.ink_spans(sh, y, w)
        if not sp: continue
        cov = occ_spans_fn(y)
        for a, b in sp:
            tot += (b - a) * dy
            cur = [(a, b)]
            for ca, cb in cov:
                nx = []
                for p, q in cur:
                    if cb <= p or ca >= q: nx.append((p, q)); continue
                    if ca > p: nx.append((p, ca))
                    if cb < q: nx.append((cb, q))
                cur = nx
            vis += sum(q - p for p, q in cur) * dy
    return tot, vis


def s3_band(top):
    print("\n╔══ §3 대역 y ∈ (%+.3f, %+.2f) 에서 **실제로 보이는** 넓이 ══╗" % (top, FRAME))
    w = W75
    band = Shape("Band", [(-3.2, top), (3.2, top), (3.2, FRAME), (-3.2, FRAME)], True, filled=True)
    layers = [("아무것도 안 씀", []),
              ("본체(머리·몸통·팔)", None),
              ("본체 + 팩 HAIR·HEAD", None),
              ("본체 + 출하 최악(털모자+바가지머리)", None)]
    def mk(extra):
        def f(y):
            sp = body_ink_spans(y, w)
            if extra: sp = H._merge(sp + H.ink_spans(extra, y, w))
            return sp
        return f
    ex2 = P.hair_napetie() + P.head_havelock()
    ex3 = (hair.SET["바가지머리"]() if callable(hair.SET["바가지머리"]) else hair.SET["바가지머리"]) + items.beanie()
    fns = [lambda y: [], mk(None), mk(ex2), mk(ex3)]
    base = None
    for (lab, _), f in zip(layers, fns):
        tot, vis = visible_area([band], f, w, top, FRAME)
        if base is None: base = tot
        print("  %-34s 보이는 넓이 %6.3f R²  (대역 전체 %6.3f R² 의 %5.1f%%)" % (lab, vis, tot, vis / tot * 100))
    print("  ★ 대역은 **가로로 넓다** — 막는 것은 머리 원반(반경 1.0)과 머리카락 돔(반경 1.56)뿐이고")
    print("     그 바깥(|x| ≳ 1.6)은 어떤 층도 쓰지 않는다.")


# ═══════════════════════════════════════════════════════════════════════════
def bag_raised(dy):
    body = [(-1.34, -2.34 + dy), (-2.62, -2.62 + dy), (-2.78, -3.72 + dy), (-1.52, -4.02 + dy), (-1.28, -3.20 + dy)]
    flap = [(-1.30, -2.30 + dy), (-2.64, -2.58 + dy), (-2.72, -3.16 + dy), (-1.32, -2.92 + dy)]
    strap = [(-1.40, -2.40 + dy), (-0.70, -1.55), (0.20, -1.10)]
    return [Shape("BagBody", body, True, filled=True),
            Shape("BagFlap", flap, True, filled=True, tone=1),
            Shape("BagStrap", strap, False)]


def bag_ladder(top_y):
    body = [(-1.34, -2.34), (-2.62, -2.62), (-2.78, -3.72), (-1.52, -4.02), (-1.28, -3.20)]
    flap = [(-1.30, -2.30), (-2.64, -2.58), (-2.72, -3.16), (-1.32, -2.92)]
    dx = (top_y + 2.20) * (-0.64 / 3.54)
    rail = [(-1.62, -2.20), (-1.62 + dx, top_y), (-2.08 + dx, top_y - 0.08), (-2.10, -2.28)]
    return [Shape("BagBody", body, True, filled=True),
            Shape("BagFlap", flap, True, filled=True, tone=1),
            Shape("BagLadder", rail, True, filled=True)]


def metrics(sh, top, w=W75):
    pts = [p for s in sh for p in s.pts]
    x0, y0, x1, y1 = rig.bounds(pts)
    span = max(x1 - x0, y1 - y0)
    tot, vis = visible_area(sh, lambda y: body_ink_spans(y, w), w, y0 - 0.4, y1 + 0.4)
    ex = P.hair_napetie() + P.head_havelock()
    tot2, vis2 = visible_area(sh, lambda y: H._merge(body_ink_spans(y, w) + H.ink_spans(ex, y, w)),
                              w, y0 - 0.4, y1 + 0.4)
    _, above = visible_area(sh, lambda y: H._merge(body_ink_spans(y, w) + H.ink_spans(ex, y, w)), w, top, FRAME)
    prof = S.profile(sh, rig.SHOULDER_R)
    worst = min((rig.max_delta(prof, S.profile(f() if callable(f) else f, rig.SHOULDER_R)), n)
                for n, f in items.BACK.items())
    e = 9e9
    for s in sh:
        p = s.pts + ([s.pts[0]] if s.loop else [])
        for a, b in zip(p, p[1:]): e = min(e, math.hypot(b[0] - a[0], b[1] - a[1]))
    kk = 44 * 0.86 / span; IST = 1.7 * 44 / 40
    return dict(span=span, vis=vis / tot, vis2=vis2 / tot2, above=above, worst=worst,
                card=e * kk / IST, y0=y0, y1=y1, edge=e)


def s4_v2(top):
    print("\n╔══ §4 대역을 쓰는 판들 — 이득과 대가의 교환비 ══╗")
    print("  규칙: 최단변 ≥ 1획@0.60 = %.4f · 카드 최단변 ≥ 1획 · y 상한 = 액자 %.2f" % (W60, FRAME))
    print("  %-22s %6s %7s %7s %8s %7s %7s" % ("판", "span", "본체뒤", "팩6/6", "어깨위잉크", "쌍별획", "카드획"))
    rows = [("v1 연장 가방(현행)", P.back_toolbag())]
    rows += [("사다리 top=%+.2f" % t, bag_ladder(t)) for t in (-0.10, 0.40, 0.90, 1.34)]
    rows += [("가방 통째로 +%.2f" % d, bag_raised(d)) for d in (1.60, 2.00, 2.30)]
    for lab, sh in rows:
        m = metrics(sh, top)
        flag = "" if m["edge"] >= W60 and m["card"] >= 1.0 and m["y1"] <= FRAME else "  ✗규칙"
        print("  %-22s %6.2f %6.1f%% %6.1f%% %8.3f %7.2f %7.2f%s"
              % (lab, m["span"], m["vis"] * 100, m["vis2"] * 100, m["above"],
                 m["worst"][0] / W60, m["card"], flag))
    print("\n  [대조] 출하 6종 본체 가림 뒤 가시율")
    for n, f in items.BACK.items():
        sh = f() if callable(f) else f
        pts = [p for s in sh for p in s.pts]
        x0, y0, x1, y1 = rig.bounds(pts)
        tot, vis = visible_area(sh, lambda y: body_ink_spans(y, W75), W75, y0 - 0.4, y1 + 0.4)
        print("     %-8s %5.1f%%" % (n, vis / tot * 100))


if __name__ == "__main__":
    top = s1_ceiling()
    s2_source(top)
    s3_band(top)
    s4_v2(top)
