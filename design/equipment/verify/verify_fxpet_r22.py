# -*- coding: utf-8 -*-
"""FX 6 + PET 6 **재실측**(2026-09-06) — 2026-09-01 27건이 지금 몇 건 남았는가.

`verify_appearance.py`와 같은 자(尺)를 쓰되 대상만 오늘 코드 거울(`appearance_now.py`)로 바꾼다.
추가로 세 가지를 더 잰다:
  (b) 카드 12장 — 에셋 직접 파싱(cards12.py와 같은 규약)
  (c) 카드 ↔ 월드 실루엣 대조 — "한 아이템이 두 벌의 그림인가"
  (d) 획 하한 대조 — 2026-09-05 R16이 액세서리 하한을 2pt에서 1pt로 갈랐다.
      FX/PET은 여전히 낱선(2pt)이라 **같은 자로 잰다**는 옛 전제가 깨졌는지 본다.
"""
import sys, os, io, math, contextlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import rig
from rig import W, Shape, bounds, rule_one, self_intersects, profile, max_delta
import appearance_now as A

with contextlib.redirect_stdout(io.StringIO()):
    import cards42 as C          # 모듈 임포트만으로 42종 표를 찍으므로 삼킨다

ICON, FIT = 44.0, 0.86
IST = 1.7 * 44 / 40


def true_min_edge(sh):
    p, n, best = sh.pts, len(sh.pts), None
    for i in range(n if sh.loop else n - 1):
        L = math.dist(p[i], p[(i + 1) % n]) / W
        if L < 1e-9:
            continue
        best = L if best is None else min(best, L)
    return best


def audit(title, table, particle=False):
    print("\n╔══ %s ══╗" % title)
    fails = 0
    for name, sh in table.items():
        if not sh:
            print("  · %-10s (월드 도형 없음 — '없음' 자리)" % name)
            continue
        msgs = []
        if not particle and not (2 <= len(sh) <= 4):
            msgs.append("규칙5 정원 %d개(2~4 밖)" % len(sh))
        acc = sum(1 for s in sh if s.tone == 1)
        if not particle and acc != 1:
            msgs.append("규칙3-2 보조색 %d개(정확히 1개여야)" % acc)
        for s in sh:
            v = rule_one(s, W)
            if v:
                msgs.append("%s 규칙1: %s" % (s.name, v))
            if s.loop and self_intersects(s.pts):
                msgs.append("%s 자기교차" % s.name)
        for s in sh:
            t = true_min_edge(s)
            if t is not None and t < 1.0 and rule_one(s, W) is None:
                msgs.append("%s ★린트사각: 최단 실제 변 %.2f획(<1.0)" % (s.name, t))
        pts = [p for s in sh for p in s.pts]
        x0, y0, x1, y1 = bounds(pts)
        span = max(x1 - x0, y1 - y0)
        if span > 1e-9:
            k = ICON * FIT / span
            for s in sh:
                cs = Shape(s.name, [(x * k, y * k) for x, y in s.pts], s.loop, s.filled, s.tone)
                v = rule_one(cs, IST)
                if v:
                    msgs.append("카드44px %s: %s" % (s.name, v))
        mins = ["%s %.2f획" % (s.name, true_min_edge(s)) for s in sh if true_min_edge(s)]
        print("  %s %-10s 도형%d 보조색%d | 최단변: %s" %
              ("✗" if msgs else "✓", name, len(sh), acc, " · ".join(mins)))
        for m in msgs:
            print("      - " + m)
            fails += 1
        if not any(s.filled for s in sh):
            print("      · (참고) 채움 0개 — 전부 윤곽선")
    ks = [k for k in table if table[k]]
    if len(ks) >= 2:
        pr = {k: profile(table[k]) for k in ks}
        worst = (None, 99.0)
        for i in range(len(ks)):
            for j in range(i + 1, len(ks)):
                v = max_delta(pr[ks[i]], pr[ks[j]]) / W
                if v < worst[1]:
                    worst = ((ks[i], ks[j]), v)
        print("  쌍별 최소 실루엣 차 %.2f획 (%s vs %s)%s" %
              (worst[1], worst[0][0], worst[0][1], "" if worst[1] >= 1.0 else "  ✗ <1.0"))
    print("╚══ 위반 %d건 ══╝" % fails)
    return fails


print("W = %.6f R (배율 0.75, 낱선 하한 2.00pt) · 1.5W = %.6f R" % (W, 1.5 * W))
n = audit("(a) FX 6종 — 2026-09-06 현행", A.FX_NOW, particle=True)
n += audit("(a) PET 5종(리틀스틱메이트 제외 = design-character 소관) — 2026-09-06 현행", A.PET_NOW)
print("\n■ 월드 위반 합계 %d건  (2026-09-01 기준선: 27건)" % n)

# ── (b) 카드 12장 ─────────────────────────────────────────────────────────────
print("\n╔══ (b) 카드 12장 — 에셋 직접 파싱 (40 캔버스, 카드 획 %.1f) ══╗" % C.STROKE_V)
WCv = C.STROKE_V
LO, HI = WCv / 2.0, C.VIEW - WCv / 2.0
cbad = 0
for f in sorted(os.listdir(C.ASSETS)):
    if not f.endswith(".asset"):
        continue
    name, slot, idx, parts = C.parse_asset(os.path.join(C.ASSETS, f))
    if slot not in (5, 6):
        continue
    msgs = []
    if not (2 <= len(parts) <= 4):
        msgs.append("정원 %d개" % len(parts))
    acc = sum(1 for p in parts if p["tone"] == 1)
    if acc != 1:
        msgs.append("보조색 %d개" % acc)
    for i, p in enumerate(parts):
        v = p["values"]
        if p["kind"] in (1, 2, 3):
            cx, cy, r = v[0], v[1], v[2]
            if 2 * r < 1.5 * WCv:
                msgs.append("p%d 지름 %.2f획 < 1.5" % (i, 2 * r / WCv))
            if cx - r < LO or cy - r < LO or cx + r > HI or cy + r > HI:
                msgs.append("p%d 상자 밖" % i)
            continue
        pts = [(v[j], v[j + 1]) for j in range(0, len(v) - 1, 2)]
        loop = (len(pts) > 3 and abs(pts[0][0] - pts[-1][0]) < 1e-6
                and abs(pts[0][1] - pts[-1][1]) < 1e-6)
        if loop:
            pts = pts[:-1]
        n_ = len(pts)
        tm = min((math.dist(pts[k], pts[(k + 1) % n_])
                  for k in range(n_ if loop else n_ - 1)), default=None)
        if tm is not None and tm < WCv:
            msgs.append("p%d 최단 실제 변 %.2f획 < 1.0" % (i, tm / WCv))
        x0, y0, x1, y1 = bounds(pts)
        if max(x1 - x0, y1 - y0) < 1.5 * WCv:
            msgs.append("p%d 잉크 사각형 %.2f획 < 1.5" % (i, max(x1 - x0, y1 - y0) / WCv))
        if x0 < LO or y0 < LO or x1 > HI or y1 > HI:
            msgs.append("p%d 상자 밖" % i)
    print("  %s %-22s 도형%d 보조색%d" % ("✗" if msgs else "✓", name, len(parts), acc))
    for m in msgs:
        print("      - " + m)
        cbad += 1
print("╚══ 카드 위반 %d건  (2026-09-01 기준선: 6장 위반) ══╝" % cbad)

# ── (c) 카드 ↔ 월드 대조 ──────────────────────────────────────────────────────
FX_ORDER = ["없음", "발자국", "반짝임", "먼지", "물방울", "나뭇잎"]
PET_ORDER = ["작은공", "종이비행기", "리틀스틱메이트", "커서친구", "풍선", "달팽이"]
print("\n╔══ (c) 카드 ↔ 월드: 같은 물건으로 읽히는가 (형태만 재담기, 획 %.1f) ══╗" % C.STROKE_V)
print("  %-14s %-10s %-10s %s" % ("아이템", "카드조각", "월드조각", "실루엣 차 / 판정"))
for f in sorted(os.listdir(C.ASSETS)):
    if not f.endswith(".asset"):
        continue
    name, slot, idx, parts = C.parse_asset(os.path.join(C.ASSETS, f))
    if slot not in (5, 6):
        continue
    key = (FX_ORDER if slot == 5 else PET_ORDER)[idx]
    body = (A.FX_NOW if slot == 5 else A.PET_NOW).get(key, [])
    if not body:
        print("  %-14s %-10s %-10s (월드 도형 없음 — 대조 대상 아님)"
              % (key, "%d개" % len(parts), "0개"))
        continue
    fb = C.fallback_shapes(parts)
    d = max_delta(C.profile_at_center(C.refit(C.to_viewbox(body))),
                  C.profile_at_center(C.refit(fb))) / C.STROKE_V
    tag = "같은 물건" if d < 1.0 else ("다른 물건" if d >= 2.0 else "경계")
    print("  %-14s %-10s %-10s %.2f획  %s"
          % (key, "%d개" % len(parts), "%d개" % len(body), d, tag))
print("╚══════╝")

# ── (d) 획 하한 대조 ─────────────────────────────────────────────────────────
MIN_ACC_PT = 1.0        # StickConfig.MinAccessoryStrokeScreenPoints (2026-09-05 R16 신설)
w_acc = (MIN_ACC_PT / rig.PT_PER_UNIT) / (rig.BASELINE_HEAD_R * rig.SHIP_SCALE)
print("\n╔══ (d) 획 하한 — 2026-09-05 R16 이후 두 자(尺)가 갈라졌는가 ══╗")
print("  FX/PET(낱선, MinStrokeScreenPoints 2.00pt) W = %.6f R = %.2f pt" % (W, W * rig.BASELINE_HEAD_R * rig.SHIP_SCALE * rig.PT_PER_UNIT))
print("  인계본 착용 조각(MinAccessoryStrokeScreenPoints 1.00pt) W = %.6f R = %.2f pt"
      % (w_acc, MIN_ACC_PT))
print("  비 = %.2f배 — 같은 캐릭터 위에서 FX/PET 획이 장비 획의 %.0f%%로 굵다" % (W / w_acc, 100 * W / w_acc))
print("╚══════╝")
