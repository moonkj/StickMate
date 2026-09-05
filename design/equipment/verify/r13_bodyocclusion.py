# -*- coding: utf-8 -*-
"""R13-b — **본체를 덮개로 넣은** 망토 옷깃/요크 생존율 재측정 (design-equipment, 2026-09-05)

왜 다시 재는가
--------------
`design-character`의 `design/character/BODY_ENVELOPE_FOR_EQUIPMENT.md` §4-2가 지적한 축을
**내 첫 측정이 통째로 빠뜨렸다** — 나는 NECK 아이템만 덮개로 넣고 **몸(머리·몸통·팔)을 안 넣었다.**
몸은 sortingOrder 0~4라 `SortBack(−1/−2)`보다 **전부 앞**이므로, 망토 도형은 몸에도 먹힌다.
⇒ 내 첫 요크 처방 수치(착용 최악 잔여 1.02~1.68획)는 **낙관적이었다.** 여기서 정정한다.

★ 교정 먼저 — `design-character`가 **다른 도구로** 낸 값을 재현하지 못하면 아래를 전부 폐기한다.

    X1  옷깃 잉크 면적            0.8312 R²   (그쪽 §1-2 교정 B)
    X2  머리 잉크 반경            1.17193 R   (그쪽 §2)
    X3  본체가 덮는 몫             63.1%      (그쪽 §4-2)
    X4  망토 단독 옷깃 생존율       36.9%      (〃)
    X5  NECK 동시착용 생존율 @0.75  나비 1.9 / 줄무늬 27.8 / 목도리 0.0 / 방울 11.4 / 펜던트 11.4 / 반다나 2.2 %

    python3 r13_bodyocclusion.py            # 교정 + 요크 깊이 스윕
    python3 r13_bodyocclusion.py --control  # 몸을 덮개에서 빼면 그쪽 값과 어긋나는가(발화 확인)

★ 획 두께는 **역할마다 다르다**(2026-09-02 M6 이후). 첫 측정에서 내가 이것도 틀렸다 —
  채운 도형의 경계선은 낱선의 **절반 하한**(1pt)이라 배율 0.75에서 0.21818 R이지 0.34386 R이 아니다.
"""
import math, sys
import rig, items
from rig import Shape

CONTROL = "--control" in sys.argv
S = 0.75                                  # 출하 배율
R_UNITS = 0.22 * S                        # 1 R 이 몇 베이스라인 유닛인가
PT = 1.0 / 35.25                          # 1 pt (베이스라인 유닛)


def w(base_ratio, min_pt):
    return max(base_ratio * S, min_pt * PT) / R_UNITS


W_LINE = w(0.048, 2.0)                    # 낱선            0.34386 R
W_FILL = w(0.048, 1.0)                    # 채움 경계선      0.21818 R
W_RING = w(0.04725, 2.0)                  # 머리 링          0.34386 R
W_TORSO = 0.11 * 1.045 * S / R_UNITS      # 몸통            0.52250 R
W_ARM = 0.10 * 1.045 * S / R_UNITS        # 팔              0.47500 R

HEAD_INK = 1.0 + W_RING / 2.0             # 1.17193 R
SH = rig.SHOULDER_R                       # −1.31818
HIP = rig.HIP_R                           # −5.09091
ARM_UP = 0.38 / 0.22                      # 1.72727 R
ARM_LO = 0.37 / 0.22                      # 1.68182 R
SPREAD = 40.0
ELBOW = 10.0


def seg_d(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax, by - ay
    L = dx * dx + dy * dy
    t = 0.0 if L == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def arm_chain(sign):
    """어깨 점에서 벌림 40°, 팔꿈치 10° 더 벌어지는 두 마디. 중립(Idle) 자세."""
    a0 = (0.0, SH)
    th1 = math.radians(SPREAD)
    a1 = (a0[0] + sign * ARM_UP * math.sin(th1), a0[1] - ARM_UP * math.cos(th1))
    th2 = math.radians(SPREAD + ELBOW)
    a2 = (a1[0] + sign * ARM_LO * math.sin(th2), a1[1] - ARM_LO * math.cos(th2))
    return [(a0, a1), (a1, a2)]


ARMS = arm_chain(+1) + arm_chain(-1)

# 팔이 끝나는 깊이 — 그 아래는 몸통(반폭 0.261)과 다리만 앞에 있다. 망토 밑단이 사는 이유다.
WRIST_DEPTH = ARM_UP * math.cos(math.radians(SPREAD)) + ARM_LO * math.cos(math.radians(SPREAD + ELBOW))

LEG_UP = 0.50 / 0.22
LEG_LO = 0.45 / 0.22
W_LEG = 0.57                      # design-character §2 실측(R). 반폭 0.285
LEG_SPREAD, KNEE = 12.0, 4.0      # SceneBootstrapper IdleLegSpreadDegrees / IdleKneeBendDegrees(부호 −)


def leg_chain(sign):
    a0 = (0.0, HIP)
    t1 = math.radians(LEG_SPREAD)
    a1 = (a0[0] + sign * LEG_UP * math.sin(t1), a0[1] - LEG_UP * math.cos(t1))
    t2 = math.radians(LEG_SPREAD - KNEE)
    a2 = (a1[0] + sign * LEG_LO * math.sin(t2), a1[1] - LEG_LO * math.cos(t2))
    return [(a0, a1), (a1, a2)]


LEGS = leg_chain(+1) + leg_chain(-1)


def in_body(p):
    if math.hypot(p[0], p[1]) <= HEAD_INK:                      # 머리 채움(3) + 링(4)
        return True
    if seg_d(p, (0.0, -1.0), (0.0, HIP)) <= W_TORSO / 2.0:      # 몸통(1)
        return True
    for a, b in ARMS:                                            # 팔(2)
        if seg_d(p, a, b) <= W_ARM / 2.0:
            return True
    for a, b in LEGS:                                            # 다리(0) — 밑단 대역에서만 의미가 있다
        if seg_d(p, a, b) <= W_LEG / 2.0:
            return True
    return False


def ink_of(shape, p):
    """도형 잉크 = 채움 ∪ 자기 경계선. 경계선 두께는 **역할**이 정한다."""
    half = (W_FILL if shape.filled else W_LINE) / 2.0
    pts = shape.pts
    n = len(pts)
    if shape.filled and rig.contains(pts, p):
        return True
    rng = range(n) if shape.loop else range(n - 1)
    for i in rng:
        if seg_d(p, pts[i], pts[(i + 1) % n]) <= half:
            return True
    return False


def collar():
    cy = items.COLLARY
    return Shape("CapeCollar",
                 [(0.40, cy + 0.10), (0.40, cy - 0.34), (-0.66, cy - 0.38), (-0.66, cy + 0.06)],
                 filled=True, tone=1)


def cape_halfwidths(name, y):
    """★ 2026-09-05 자기 정정 — 처음에 「x<0 점들을 y 내림차순으로 정렬해 보간」했다.
    긴망토는 **제비꼬리 노치** 때문에 그 정렬이 뒤판 바깥변이 아니라 **노치 꼭짓점**을 골랐고,
    뒤 도달이 −1.6215 대신 −0.9530(0.67 R 좁게)으로 나왔다. 프로덕션
    `AccessoryShapeBuilder.CapeShoulderYoke` 와 `items.yoke` 는 **인접 변**을 자른다 —
    `outline[1]→outline[2]`(뒤) · `outline[0]→outline[-1]`(앞). 그 규칙을 그대로 쓴다."""
    o = [s for s in items.BACK[name] if s.name == "CapeOutline"][0].pts
    def cut(top, bot):
        span = top[1] - bot[1]
        t = 0.0 if span <= 1e-6 else max(0.0, min(1.0, (top[1] - y) / span))
        return top[0] + (bot[0] - top[0]) * t
    return cut(o[1], o[2]), cut(o[0], o[-1])


def yoke(name, depth):
    cy = items.COLLARY
    yb = cy - depth
    bw, fw = cape_halfwidths(name, yb)
    return Shape("CapeYoke", [(0.40, cy), (-0.62, cy + 0.04), (bw, yb), (fw, yb)],
                 filled=True, tone=1)


STEP = 0.004     # R. design-character 는 0.002 를 썼다 — 여기서는 0.004(면적 오차 < 0.3%)


def survive(target, occluders, use_body=True):
    x0, y0, x1, y1 = rig.bounds(target.pts)
    m = (W_FILL if target.filled else W_LINE) / 2.0 + STEP
    x0 -= m; y0 -= m; x1 += m; y1 += m
    nx = int((x1 - x0) / STEP) + 1
    ny = int((y1 - y0) / STEP) + 1
    tot = 0
    live = []
    for i in range(nx):
        px = x0 + i * STEP
        col = []
        for j in range(ny):
            py = y0 + j * STEP
            if not ink_of(target, (px, py)):
                col.append(False); continue
            tot += 1
            hidden = (use_body and in_body((px, py)))
            if not hidden:
                for s in occluders:
                    if ink_of(s, (px, py)):
                        hidden = True; break
            col.append(not hidden)
        live.append(col)
    rem = sum(1 for c in live for v in c if v)
    cell = STEP * STEP
    # 남은 잉크의 최대 내접원(격자 BFS 거리변환) — 「덩어리로 남는가」
    from collections import deque
    INF = 10 ** 9
    dist = [[INF if live[i][j] else 0 for j in range(ny)] for i in range(nx)]
    dq = deque()
    for i in range(nx):
        for j in range(ny):
            if not live[i][j]:
                continue
            if (i == 0 or j == 0 or i == nx - 1 or j == ny - 1
                    or not live[i-1][j] or not live[i+1][j] or not live[i][j-1] or not live[i][j+1]):
                dist[i][j] = 1; dq.append((i, j))
    while dq:
        a, b = dq.popleft()
        for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            na, nb = a + da, b + db
            if 0 <= na < nx and 0 <= nb < ny and dist[na][nb] > dist[a][b] + 1:
                dist[na][nb] = dist[a][b] + 1; dq.append((na, nb))
    dmax = 0
    for i in range(nx):
        for j in range(ny):
            if live[i][j] and dist[i][j] < INF:
                dmax = max(dmax, dist[i][j])
    return tot * cell, rem * cell, dmax * STEP


def main():
    print("╔══ R13-b · 본체 포함 재측정 (배율 %.2f) %s ══╗"
          % (S, "  [--control: 몸을 뺐다]" if CONTROL else ""))
    print("   낱선 %.5f R · 채움경계선 %.5f R · 머리잉크 %.5f R · 몸통 %.5f · 팔 %.5f"
          % (W_LINE, W_FILL, HEAD_INK, W_TORSO, W_ARM))

    c = collar()
    tot, rem, _ = survive(c, [], use_body=not CONTROL)
    print("\n[교정] design-character 값 재현")
    print("   X1 옷깃 잉크 면적   목표 0.8312 R²   실측 %.4f   %s"
          % (tot, "OK" if abs(tot - 0.8312) <= 0.010 else "!! 어긋남"))
    print("   X2 머리 잉크 반경   목표 1.17193 R   실측 %.5f   %s"
          % (HEAD_INK, "OK" if abs(HEAD_INK - 1.17193) < 1e-4 else "!!"))
    print("   X3/X4 본체 덮는 몫 %.1f%% · 망토 단독 생존율 %.1f%%  목표 63.1 / 36.9  %s"
          % (100 * (1 - rem / tot), 100 * rem / tot,
             "OK" if abs(100 * rem / tot - 36.9) <= 1.5 else "!! 어긋남"))

    want = {"나비넥타이": 1.9, "줄무늬타이": 27.8, "목도리": 0.0,
            "방울목걸이": 11.4, "펜던트": 11.4, "반다나": 2.2}
    print("   X5 NECK 동시착용 생존율")
    bad = 0
    for it, shapes in items.NECK.items():
        t2, r2, rho = survive(c, shapes, use_body=not CONTROL)
        got = 100 * r2 / t2
        ok = abs(got - want[it]) <= 1.5
        bad += 0 if ok else 1
        print("      %-10s 목표 %5.1f%%  실측 %5.1f%%  잔여ρ %.2f획  %s"
              % (it, want[it], got, rho / 0.2181818, "OK" if ok else "!! 어긋남"))
    if CONTROL:
        print("\n   ⇒ --control(몸 제외)에서 어긋남 %d건. 몸을 넣어야 그쪽 값이 재현된다." % bad)
        return 0
    if bad:
        raise SystemExit("!! 교정 실패 — 아래 숫자를 폐기한다.")
    print("   ⇒ 교정 전량 통과. 두 팀이 서로 다른 도구로 같은 값을 냈다.")

    global STEP
    STEP = 0.008     # 교정은 0.004로 끝났다. 스윕은 면적이 20배라 0.008로 내린다(ρ 해상도 ±0.04획)
    print("\n[0] ★ 구조 사실 — 팔이 끝나는 깊이 d = %.3f R → y = %.3f R" % (WRIST_DEPTH, SH - WRIST_DEPTH))
    print("   그 아래에서 망토 앞에 있는 것은 몸통(반폭 %.3f R)과 다리뿐이다." % (W_TORSO / 2.0))
    print("   세 망토의 밑단은 전부 그 아래다: 짧은망토 −6.39 / 긴망토 −8.29 / 판초 −5.22 R")

    print("\n[1] 처방 A(어깨 요크) 재평가 — **몸 포함**")
    print("   %-8s %6s %10s %10s %10s"
          % ("망토", "깊이", "요크잉크R²", "최악생존%", "최악잔여ρ"))
    for name in ("짧은망토", "긴망토", "판초"):
        for d in (1.6, 2.0, 2.4, 2.8, 3.2):
            y = yoke(name, d)
            worst, witem, wrho = 1e9, None, 1e9
            base = None
            for it, shapes in items.NECK.items():
                t2, r2, rho = survive(y, shapes)
                base = t2
                if 100 * r2 / t2 < worst:
                    worst, witem = 100 * r2 / t2, it
                wrho = min(wrho, rho)
            print("   %-8s %6.1f %10.4f %9.1f%% %8.2f획 (%s)"
                  % (name, d, base, worst, wrho / 0.2181818, witem))

    print("\n[2] 처방 B(밑단 안단) 재평가 — **몸 + 다리 포함**")
    import r13_capecollar as C
    import r13_polish as P
    ICON, FIT = 44.0, 0.86
    NEED = 2 * 1.7 * (ICON / 40.0)
    print("   %-8s %5s %10s %10s %10s %8s"
          % ("망토", "t", "안단잉크R²", "최악생존%", "최악잔여ρ", "카드px"))
    for name in ("짧은망토", "긴망토", "판초"):
        pts = [p for s2 in items.BACK[name] for p in s2.pts]
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        E = max(max(xs) - min(xs), max(ys) - min(ys))
        for t in (0.54, 0.70, 0.86):
            band = C.hem_facing(name, t)
            sh = Shape("CapeHemFacing", band, filled=True, tone=1)
            worst, witem, wrho, base = 1e9, None, 1e9, None
            for it, shapes in items.NECK.items():
                a2, r2, rho = survive(sh, shapes)
                base = a2
                if 100 * r2 / a2 < worst:
                    worst, witem = 100 * r2 / a2, it
                wrho = min(wrho, rho)
            card = P.rho_max(band) * 2 / E * ICON * FIT
            print("   %-8s %5.2f %10.4f %9.1f%% %8.2f획 %7.2f%s"
                  % (name, t, base, worst, wrho / 0.2181818, card, "" if card >= NEED else " ✗"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
