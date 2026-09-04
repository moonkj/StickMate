# -*- coding: utf-8 -*-
"""⑦ 1차 기계 이식 — 인계본 16종을 R 좌표로 옮겨 덤프한다 (design-equipment, 2026-09-03).

★ 이것은 **미조정 기계 변환**이다. 그대로 프로덕션에 넣지 마라.
  docs/EQUIPMENT_HANDOFF_PORT_SPEC.md 6절의 조정 5건이 적용돼야 출하 가능하다.
  (액자 초과 2건 · 감쌈 4건 · 렌즈 합침 1건 · 몸통 단위 재표현 8건 · 소멸 조각 44건)

★★ R12 결함 수정 (2026-09-03) — **이 파일이 조용한 거짓 초록을 내고 있었다.**
  구판 `simplify()`는 **정점당 회전각 8도** 문턱으로 점을 솎았다. 세밀 표본 곡선(16분할 호)은
  정점당 회전이 5.6도라 **호 전체가 문턱 아래로 내려가 통째로 지워진다.** 결과:
      선글라스 렌즈  raw y[-0.2559,+0.2866] 높이 0.5425 R  →  덤프 0.0093 R (69점 중 12점, 전부 윗변)
  덤프의 잉크 판정이 max(가로,세로)라 폭 0.7844 R 이 살아남아 **판정은 계속 「존재」**였다.
  47조각 중 **15조각**이 경계 손실 > 0.01 R 이었다(최악 0.5330 R).
  → 각도 문턱을 버리고 **Douglas–Peucker(수직 편차 R 단위)**로 바꾸고,
    **경계 극점 4개를 강제 보존**해 잉크 사각형이 구조적으로 불변이 되게 했다.
  → 그리고 **솎기 결과를 매번 자가 검산**한다(경계 일치 + 원점→간선 최대편차 ≤ tol).
    어긋나면 조용히 넘어가지 않고 **SystemExit로 죽는다.**
  대조 하니스: `python3 handoff_dumpfix.py`  (양성·음성 대조 · 합성 도형 해석해 대조)

★★ 그리고 **판정 자체를 고쳤다.**
  구판은 `max(가로,세로)`(= 프로덕션 규칙 1-A의 「긴 변」) 하나만 보고 존재/소멸을 갈랐다.
  프로덕션은 그 구멍을 알고 있고 **규칙 1-C(ρ_max, 최대 내접원)**로 두께를 따로 잡는다
  (`AccessoryStrokeBudgetTests.DescribeRuleOneViolation` 주석 · `AccessoryFillAreaRuleTests`).
  이 덤프에는 1-C가 **없었다.** 그래서 「길이 5W · 두께 0.1W 실오라기」가 「존재」로 찍혔다.
  ⇒ 이제 긴변 / 짧은변 / ρ_max 를 함께 찍고, 판정을 네 단계로 나눈다.
    ※ 규칙 1-A의 max()는 **프로덕션과 같아야 하므로 바꾸지 않는다**(rig.rule_one 그대로).
       바꾼 것은 **이 덤프의 존폐 판정**이지 규칙 1-A가 아니다.

    python3 handoff_dump.py            # 요약
    python3 handoff_dump.py --full     # 점 좌표 전문
    python3 handoff_dump.py --full > handoff_ported_coords.txt
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, items, hatfix
from handoff import parse_items, build, to_R_shape, OUR_NAME

W75 = rig.stroke_in_R(0.75)
FULL = "--full" in sys.argv
SIMPLIFY_TOL_R = 0.010          # 수직 편차 상한. 0.010 R = 0.029 W — 획의 3%다.
FILL_PEN = hatfix.FILL_OUTLINE_PEN_IN_R   # 0.21818 R = 규칙 1-C의 1획


# ── 솎기 ────────────────────────────────────────────────────────────────────
def _dp(pts, i, j, tol, keep):
    """Douglas–Peucker 재귀 — 현(弦) i..j 에서 가장 먼 점을 남긴다."""
    ax, ay = pts[i]; bx, by = pts[j]
    dx, dy = bx - ax, by - ay
    L = math.hypot(dx, dy)
    best, bi = -1.0, -1
    for k in range(i + 1, j):
        px, py = pts[k]
        d = (math.hypot(px - ax, py - ay) if L < 1e-12
             else abs(dx * (py - ay) - dy * (px - ax)) / L)
        if d > best: best, bi = d, k
    if bi >= 0 and best > tol:
        _dp(pts, i, bi, tol, keep); keep.add(bi); _dp(pts, bi, j, tol, keep)


def _extreme_idx(pts):
    """잉크 사각형을 정하는 극점 4개. 이것을 강제로 남기면 경계가 구조적으로 보존된다."""
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    return {xs.index(min(xs)), xs.index(max(xs)), ys.index(min(ys)), ys.index(max(ys))}


def simplify(pts, loop, tol_R=SIMPLIFY_TOL_R):
    n = len(pts)
    if n <= 3: return list(pts)
    keep = set(_extreme_idx(pts))
    if loop:
        # 닫힌 곡선은 앵커 2개가 필요하다(1개면 현이 길이 0이 되어 퇴화한다).
        a = min(keep)
        far = max(range(n), key=lambda k: math.dist(pts[a], pts[k]))
        keep.add(a); keep.add(far)
        anchors = sorted(keep)
        ext = pts + [pts[0]]
        ring = anchors + [anchors[0] + n]
        for s, e in zip(ring, ring[1:]):
            sub = [ext[t % n] if t < n + 1 else ext[t - n] for t in range(s, e + 1)]
            sk = set()
            _dp(sub, 0, len(sub) - 1, tol_R, sk)
            for t in sk: keep.add((s + t) % n)
    else:
        keep.add(0); keep.add(n - 1)
        anchors = sorted(keep)
        for s, e in zip(anchors, anchors[1:]):
            sk = set()
            _dp(pts[s:e + 1], 0, e - s, tol_R, sk)
            for t in sk: keep.add(s + t)
    return [pts[i] for i in sorted(keep)]


# ── 자가 검산 — 솎기가 형태를 잃으면 **죽는다** ─────────────────────────────
def _pt_seg(q, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    L2 = dx * dx + dy * dy
    t = 0.0 if L2 < 1e-15 else max(0.0, min(1.0, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / L2))
    return math.hypot(q[0] - (a[0] + dx * t), q[1] - (a[1] + dy * t))


def deviation(raw, simp, loop):
    """원본 점 전부에서 솎은 간선까지의 최대 거리(R)."""
    m = len(simp)
    segs = m if loop else m - 1
    worst = 0.0
    for q in raw:
        d = min(_pt_seg(q, simp[i], simp[(i + 1) % m]) for i in range(segs))
        if d > worst: worst = d
    return worst


def audit_simplify(name, raw, simp, loop, tol=SIMPLIFY_TOL_R):
    br, bs = rig.bounds(raw), rig.bounds(simp)
    berr = max(abs(a - b) for a, b in zip(br, bs))
    dev = deviation(raw, simp, loop)
    if berr > 1e-9 or dev > tol * 1.001:
        raise SystemExit(
            "★ 솎기 자가검산 실패 — %s: 경계오차 %.6f R (상한 0) · 최대편차 %.6f R (상한 %.4f R)\n"
            "   솎기가 형태를 잃었다. 이 좌표표를 쓰지 마라." % (name, berr, dev, tol))
    return berr, dev


# ── 조각 지표 ───────────────────────────────────────────────────────────────
def piece_metrics(s):
    x0, y0, x1, y1 = rig.bounds(s.pts)
    long_w = max(x1 - x0, y1 - y0) / W75          # 규칙 1-A (프로덕션과 동일한 자)
    short_w = min(x1 - x0, y1 - y0) / W75
    # 격자 0.02 R + 국소정밀화 9회 → 오차 < 3e-4 R (0.004 격자 대비 2.5e-4 차, 실측).
    # 0.004 격자는 조각 하나에 40초가 걸려 하니스를 못 돌린다.
    rho = hatfix.rho_max(s.pts, coarse=0.02, refine=9) if (s.loop and len(s.pts) >= 3) else 0.0
    return long_w, short_w, rho, rho / FILL_PEN


def verdict(s, long_w, short_w, rho_k):
    """네 단계. 구판은 첫 줄 하나만 보고 「존재/소멸」로 갈랐다."""
    if long_w < 1.5:            return "★소멸"          # 규칙 1-A 탈락
    if s.filled and rho_k < 1.0: return "★실오라기"      # 규칙 1-C 탈락 — 색면이 자기 윤곽에 먹힌다
    if short_w < 0.5:            return "△납작"          # 낱선이라도 화면에서 선 한 줄로 읽힌다
    return "존재"


# ── 본문 ────────────────────────────────────────────────────────────────────
IT = parse_items()
print("# 인계본 16종 · 1차 기계 이식 (머리 중심 원점 · R 배수 · +x 진행 방향 · y 위로)")
print("# 변환식 교정: design/equipment/verify/handoff_cal.py  (±1.0R 앵커 4건 · 최악 3.4%%)")
print("# W(배율 0.75) = %.6f R · 규칙1-A 긴변 하한 1.5 W = %.6f R · 규칙1-C ρ 하한 = %.6f R"
      % (W75, 1.5 * W75, FILL_PEN))
print("# 솎기 = Douglas-Peucker 수직편차 %.3f R (= %.3f W) · 경계 극점 4개 강제 보존 · 매 조각 자가검산"
      % (SIMPLIFY_TOL_R, SIMPLIFY_TOL_R / W75))
print("# ★ 미조정본이다. 조정 사항은 docs/EQUIPMENT_HANDOFF_PORT_SPEC.md 6절.")
print()
tot_dev = 0.0
for kind, pieces in IT.items():
    P = build(kind, pieces)
    slot, nm = OUR_NAME[kind]
    print("=" * 78)
    print("%s  →  %s %s   (인계본 %d조각)" % (kind, slot, nm, len(P)))
    print("=" * 78)
    live = []
    for i, p in enumerate(P):
        s = to_R_shape(kind, p, "%s%d" % (p.call, i))
        long_w, short_w, rho, rho_k = piece_metrics(s)
        v = verdict(s, long_w, short_w, rho_k)
        if v == "존재": live.append(i)
        role = {"B": "채움+외곽", "S": "낱선", "F": "강조채움", "H": "하이라이트",
                "CB": "원", "CF": "강조원", "RB": "둥근사각"}[p.call]
        alpha = ("grad" if p.filled and p.fill_opacity is None
                 else ("%.2f" % p.fill_opacity if p.fill_opacity is not None else "—"))
        print("  [%d] %-6s %-11s 긴변 %5.2f W  짧변 %5.2f W  ρ %5.2f획  알파 %-5s  tone=%d  %s" %
              (i, p.call, role, long_w, short_w, rho_k, alpha, s.tone, v))
        if FULL and long_w >= 1.5:
            sp = simplify(s.pts, s.loop)
            berr, dev = audit_simplify("%s %s" % (kind, s.name), s.pts, sp, s.loop)
            tot_dev = max(tot_dev, dev)
            print("      loop=%s filled=%s  (%d점 → %d점, DP %.3fR · 경계오차 %.1e · 최대편차 %.5fR)"
                  % (s.loop, s.filled, len(s.pts), len(sp), SIMPLIFY_TOL_R, berr, dev))
            for j in range(0, len(sp), 4):
                print("        " + "  ".join("(%+.4f,%+.4f)" % q for q in sp[j:j + 4]))
    print("  ⇒ 생존 %d조각 %s · 정원(2~4) %s" %
          (len(live), live, "OK" if 2 <= len(live) <= 4 else "★ 밖"))
    print()
if FULL:
    print("# 솎기 자가검산 전량 통과 — 경계오차 0 · 최대편차 %.5f R (= %.3f W, 상한 %.3f R)"
          % (tot_dev, tot_dev / W75, SIMPLIFY_TOL_R))
