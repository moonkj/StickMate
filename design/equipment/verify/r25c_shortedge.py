#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R25c — 「최단 실제 변」 래칫 진단 (design-equipment, 2026-09-06)

AppearanceShapeBudgetTests.최단_실제_변_검사를_액세서리_30종으로_확장한다 가
14 -> 19 로 늘어난 원인을 <b>Unity 러너 없이</b> 재현/분해한다.

    design/equipment/verify/r25c_build.sh > /tmp/dump.txt
    python3 design/equipment/verify/r25c_shortedge.py /tmp/dump.txt

프로덕션 검사와 같은 자를 쓴다(AccessoryStrokeBudgetTests.DescribeShortestEdgeViolation /
DescribeRuleOneViolation 을 그대로 옮긴 것이고, 문턱값은 덤프가 찍어 준 @W 를 쓴다 —
여기 숫자를 손으로 적지 않는다).

★ 이 스크립트가 답하는 질문은 <b>세 개</b>다.
  (1) 지금 위반이 몇 건인가            -> --count
  (2) 어느 라운드가 넣었는가           -> 두 덤프를 --diff 로 비교
  (3) 그 위반이 <b>진짜 결함</b>인가   -> 아래 분류

★ 분류가 핵심이다. 37-6 규칙 1 의 실패 모드는 「짧은 변」이 아니라
  <b>양끝이 모두 꺾임인 짧은 변</b>(= 그리려다 만 점)이다.
  «최단 실제 변» 자는 그 꺾임 조건을 <b>일부러 뺀</b> 더 센 자이고,
  그래서 <b>매끄러운 곡선을 촘촘히 표본한 자리</b>를 함께 잡는다
  (AccessoryStrokeBudgetTests 머리말이 그 부작용을 명시한다:
   "곡선을 촘촘히 쪼갤수록 최단 선분은 짧아지지만 그림은 오히려 좋아진다").
  프로젝트는 그 부작용을 인계본 16종에는 이미 면제했다(계약 v2 · §14-6 #14).
  이 스크립트는 남은 위반이 그 부작용인지, 진짜 결함인지 <b>꺾임각으로</b> 가른다.
"""
import sys, math

CORNER_DEGREES = 45.0      # AccessoryStrokeBudgetTests.CornerDegrees
INK_RECT_MIN_W = 1.5       # 규칙 1-A 잉크 사각형 긴 변


def load(path):
    W = None
    items, cur = [], None
    for line in open(path, encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        if f[0] == "@ITEM":
            cur = dict(cat=f[1], idx=int(f[2]), name=f[3], shapes=[])
            items.append(cur)
        elif f[0] == "@SHAPE":
            cur["shapes"].append(dict(
                name=f[1], loop=f[2] == "1", handoff=f[3] == "1", filled=f[4] == "1",
                pts=[tuple(float(v) for v in t.split(",")) for t in f[5:]]))
        elif f[0] == "@W":
            W = float(f[1])
        elif f[0] == "@LOG" and (f[1] != "0" or f[2] != "0"):
            sys.exit("덤프가 Debug.LogError/Warning 을 냈다(%s/%s) — 이 실행의 숫자는 전부 무효다." % (f[1], f[2]))
    if W is None:
        sys.exit("@W 줄이 없다 — 덤프가 잘렸다. 판정 불능이다(통과가 아니다).")
    return W, items


def turn(a, b, c):
    v1 = (b[0] - a[0], b[1] - a[1]); v2 = (c[0] - b[0], c[1] - b[1])
    n1 = math.hypot(*v1); n2 = math.hypot(*v2)
    if n1 < 1e-9 or n2 < 1e-9:
        return 0.0
    d = max(-1.0, min(1.0, (v1[0] * v2[0] + v1[1] * v2[1]) / (n1 * n2)))
    return math.degrees(math.acos(d))


def corner_at(pts, loop, k):
    """열린 선의 양 끝점은 꺾임이 아니다(프로덕션 DescribeStubSegment 와 같은 규약). None = 끝점."""
    n = len(pts)
    if not loop and (k == 0 or k == n - 1):
        return None
    return turn(pts[(k - 1) % n], pts[k], pts[(k + 1) % n])


def shortest_edge_violation(shape, W):
    """DescribeShortestEdgeViolation 과 같은 자 — <b>첫</b> 위반 변 하나를 돌려준다."""
    p = shape["pts"]
    if len(p) < 2:
        return None
    n = len(p)
    segs = n if shape["loop"] else n - 1
    for i in range(segs):
        j = (i + 1) % n
        d = math.dist(p[i], p[j])
        if d < 1e-6 or d >= W:
            continue
        t1, t2 = corner_at(p, shape["loop"], i), corner_at(p, shape["loop"], j)
        stub = (t1 is not None and t1 >= CORNER_DEGREES) and (t2 is not None and t2 >= CORNER_DEGREES)
        return dict(i=i, j=j, w=d / W, t1=t1, t2=t2, stub=stub)
    return None


def rule_one_violation(shape, W):
    """DescribeRuleOneViolation — 1-A(잉크 사각형 긴 변) 다음 1-B(그리려다 만 점)."""
    p = shape["pts"]
    if len(p) < 2:
        return "점 2개 미만"
    xs = [q[0] for q in p]; ys = [q[1] for q in p]
    long_side = max(max(xs) - min(xs), max(ys) - min(ys))
    if long_side < W * INK_RECT_MIN_W:
        return "규칙 1-A 잉크 사각형 긴 변 %.2f획 < %.1f획" % (long_side / W, INK_RECT_MIN_W)
    n = len(p)
    segs = n if shape["loop"] else n - 1
    for i in range(segs):
        j = (i + 1) % n
        t1, t2 = corner_at(p, shape["loop"], i), corner_at(p, shape["loop"], j)
        if not (t1 is not None and t1 >= CORNER_DEGREES and t2 is not None and t2 >= CORNER_DEGREES):
            continue
        d = math.dist(p[i], p[j])
        if 1e-6 <= d < W:
            return "규칙 1-B 그리려다 만 점 %d->%d %.2f획" % (i, j, d / W)
    return None


def scan(path):
    W, items = load(path)
    rows, r1, shapes_checked = [], [], 0
    for it in items:
        for s in it["shapes"]:
            if s["handoff"]:              # 계약 v2 — 이 자의 대상이 아니다(§14-6 #14)
                continue
            shapes_checked += 1
            v = shortest_edge_violation(s, W)
            if v:
                rows.append((it, s, v))
            g = rule_one_violation(s, W)
            if g:
                r1.append((it, s, g))
    return W, items, shapes_checked, rows, r1


def key(it, s):
    return "%s %d(%s) '%s'" % (it["cat"], it["idx"], it["name"], s["name"])


def report(path, label):
    W, items, checked, rows, r1 = scan(path)
    print("=" * 96)
    print("%s   %s" % (label, path))
    print("  W = %.6f R (출하 배율 0.75 · 머리 지름 5.82 W)   아이템 %d종   비-인계본 도형 %d개"
          % (W, len(items), checked))
    print("  최단 실제 변 위반 %d건 / 규칙 1 위반 %d건" % (len(rows), len(r1)))
    print("-" * 96)
    stubs = 0
    for it, s, v in rows:
        t = lambda x: " 끝 " if x is None else "%5.1f°" % x
        verdict = "★ 그리려다 만 점(진짜 결함)" if v["stub"] else "곡선 표본 — 한쪽 끝 꺾임 < 45°"
        if v["stub"]:
            stubs += 1
        print("  %-34s %5s  %.2f획   시작 %s 끝 %s   %s"
              % (key(it, s), "%d->%d" % (v["i"], v["j"]), v["w"], t(v["t1"]), t(v["t2"]), verdict))
    print("-" * 96)
    print("  분해: 진짜 그리려다 만 점 %d건 / 곡선 표본 %d건" % (stubs, len(rows) - stubs))
    if r1:
        print("  ★ 규칙 1(하드 검사 — 래칫이 아니다) 위반:")
        for it, s, g in r1:
            print("     %-34s %s" % (key(it, s), g))
    return {key(it, s): v["w"] for it, s, v in rows}


def seg_dist(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    L2 = dx * dx + dy * dy
    if L2 < 1e-18:
        return math.dist(p, a)
    t = max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L2))
    return math.dist(p, (a[0] + t * dx, a[1] + t * dy))


def decimate(pts, loop, minlen, corner_deg=30.0):
    """꺾임(≥30°)은 반드시 남기고, 그 사이 <b>매끄러운 구간</b>만 균일 스트라이드로 솎는다.
    단순 그리디(앞에서부터 minlen 미만을 버림)는 꺾임을 통째로 지나쳐 이탈이 0.47획까지 튄다 —
    실측으로 확인했고, 그래서 앵커를 먼저 고정한다."""
    n = len(pts)
    anchors = set() if loop else {0, n - 1}
    for i in (range(n) if loop else range(1, n - 1)):
        if turn(pts[(i - 1) % n], pts[i], pts[(i + 1) % n]) >= corner_deg:
            anchors.add(i)
    anch = sorted(anchors) or [0]
    keep = set(anch)
    for a, b in zip(anch, anch[1:] + ([anch[0] + n] if loop else [])):
        span = b - a
        if span <= 1:
            continue
        L = sum(math.dist(pts[(a + k) % n], pts[(a + k + 1) % n]) for k in range(span))
        m = max(1, int(L // minlen))
        for j in range(1, m):
            keep.add((a + round(span * j / m)) % n)
    return [pts[i] for i in sorted(keep)]


def decimate_report(path, cats):
    """처방 (B) 의 값 — 「표본을 성기게 해서 위반을 닫으면 그림이 얼마나 달라지는가」.
    0.75 배율에서 R = 5.8 pt(머리 지름 11.6 pt)이므로 이탈 pt = 이탈R × 5.8."""
    W, items = load(path)
    R_PT = 5.8
    print("=" * 96)
    print("[감축 시뮬레이션] 꺾임 보존 + 매끄러운 구간만 1.03 W 스트라이드")
    print("  %-22s%6s%6s%8s%12s%9s%11s" % ("도형", "현재", "감축", "최단변", "최대이탈R", "획", "0.75배 pt"))
    print("-" * 96)
    old = new = 0
    for it in items:
        if (it["cat"], it["idx"]) not in cats:
            continue
        for s in it["shapes"]:
            if s["handoff"]:
                continue
            p = s["pts"]; old += len(p)
            if len(p) < 4:
                new += len(p); continue
            q = decimate(p, s["loop"], W * 1.03); new += len(q)
            n = len(q); segs = n if s["loop"] else n - 1
            mn = min(math.dist(q[i], q[(i + 1) % n]) for i in range(segs))
            dev = max(min(seg_dist(pt, q[i], q[(i + 1) % n]) for i in range(segs)) for pt in p)
            print("  %-22s%6d%6d%7.2f획%12.5f%9.3f%11.3f"
                  % (s["name"], len(p), n, mn / W, dev, dev / W, dev * R_PT))
    print("-" * 96)
    print("  점 합계 %d -> %d" % (old, new))
    print("  ※ 최단변이 아직 1.00획 미만인 줄이 남는다 — <b>꺾임 바로 다음 변</b>은 어떤 표본 밀도로도")
    print("     1획을 못 넘는다(넘기려면 꼭짓점을 한 획만큼 깎아야 한다). 이 자가 사실상 금지하는 것은")
    print("     「꼭짓점 뒤에 곡선이 이어지는 그림」 자체다 — 닫힌 윤곽에서는 흔하고 정상인 형태다.")


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    if sys.argv[1] == "--decimate":
        return decimate_report(sys.argv[2], {("HEAD", 4), ("HEAD", 5)})
    a = report(sys.argv[1], "[현재]")
    if len(sys.argv) >= 3:
        b = report(sys.argv[2], "[대조]")
        print("=" * 96)
        print("[차이] 대조 -> 현재")
        for k in sorted(set(a) - set(b)):
            print("  + %-34s %.2f획   ← 이 라운드가 넣었다" % (k, a[k]))
        for k in sorted(set(b) - set(a)):
            print("  - %-34s %.2f획   ← 이 라운드가 닫았다" % (k, b[k]))
        for k in sorted(set(a) & set(b)):
            if abs(a[k] - b[k]) > 1e-4:
                print("  ~ %-34s %.2f -> %.2f획" % (k, b[k], a[k]))
        print("  합계 %d -> %d건" % (len(b), len(a)))


if __name__ == "__main__":
    main()
