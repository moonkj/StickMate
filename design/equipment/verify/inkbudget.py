# -*- coding: utf-8 -*-
"""★ 색면 생존 / 커버선 정직성 — 2026-09-02.

사용자 신고 "각 장비별 리디자인 한거 맞아? 아직도 전부다 조잡한데" / "별로 변경된거 같지가 않아"에
대한 계량자. **규칙(verify.py)은 전부 통과했는데 왜 조잡한가**를 숫자로 답한다.

────────────────────────────────────────────────────────────────────────────
축 ① 색면 생존율 — 채운 도형이 **자기 윤곽선**에 먹히고 남는 색면
────────────────────────────────────────────────────────────────────────────
채운 도형은 채움 + 윤곽선 두 번 그려진다(AccessoryCardIcon.TryBuild / CharacterAccessoryRenderer).
윤곽선 색은 채움 x 0.62(AccessoryShapeBuilder.FillOutlineColor)라 **눈에 보이는 다른 색**이고,
폭 W인 선이 경계 위에 중심을 두고 얹히므로 도형은 안쪽으로 W/2를 잃는다.

  생존율 = 면적(도형을 W/2만큼 안으로 침식) / 면적(도형)

규칙 1은 "잉크 사각형 >= 1.5W"만 본다. 폭 1.5W인 도형에 폭 1W 펜으로 윤곽을 그리면 색면은
0.5W(양쪽 0.25W씩)만 남는다 — **규칙을 지키면서 색이 사라지는 구간이 통째로 열려 있다.**
그 구간이 지금 이 세트의 절반이다.

────────────────────────────────────────────────────────────────────────────
축 ② 커버선 정직성 — 선언한 커버선과 실제 잉크 밑단의 차
────────────────────────────────────────────────────────────────────────────
모자는 HatCoverLocalY로 "내가 덮는 아래 한계선"을 **스스로 선언**하고, 머리카락은 그 선을 믿고
잘린다. 그런데 챙/단은 **채운 다각형**이라 선언선보다 훨씬 아래까지 내려간다. 그 차이가
"머리카락은 잘렸는데 정작 모자가 다 덮는" 그림을 만든다.

    python3 inkbudget.py
"""
import sys, math
sys.path.insert(0, '.')
import rig, items, hair, headroom as H

#: ★ 하한 — 채운 도형이 자기 윤곽선에 먹히고도 이만큼은 색면이 남아야 한다.
#  근거: 카드 44px에서 이 세트의 중앙값이 72.5%이고 카드는 사용자가 "색이 보인다"고 인정한 화면이다.
#  착용 그림의 중앙값은 배율 0.60에서 14.6%다. 그 사이에서, **색면 폭이 최소 펜 하나**가 되는
#  지점을 잡는다: 폭 t인 띠는 t >= 2W일 때 색면 폭이 W 이상 남는다 → 면적 기준으로는 대략 30%.
FILL_SURVIVAL_FLOOR = 0.30

#: 커버선과 실제 잉크 밑단의 허용 차(R). 0.5W(배율 0.60에서 0.215R) = 획 반 개.
#  선언선 아래로 획 반 개까지는 "선 두께 때문"이라고 설명되지만 그 이상은 거짓말이다.
COVER_HONESTY_TOLERANCE_W = 0.5


def _erode_ratio(pts, w, step=0.006):
    """다각형을 W/2만큼 안으로 침식하고 남는 면적 비율. numpy 없이 스캔라인으로."""
    x0 = min(p[0] for p in pts) - 0.02; x1 = max(p[0] for p in pts) + 0.02
    y0 = min(p[1] for p in pts) - 0.02; y1 = max(p[1] for p in pts) + 0.02
    n = len(pts); tot = sur = 0
    y = y0
    while y <= y1:
        spans = H._merge(H._poly_spans(pts, y))
        for a, b in spans:
            x = a
            while x <= b:
                tot += 1
                d = 1e9
                for k in range(n):
                    p, q = pts[k], pts[(k + 1) % n]
                    dx, dy = q[0] - p[0], q[1] - p[1]
                    L2 = dx * dx + dy * dy
                    t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, ((x - p[0]) * dx + (y - p[1]) * dy) / L2))
                    d = min(d, math.hypot(x - (p[0] + dx * t), y - (p[1] + dy * t)))
                    if d <= w * 0.5: break
                if d > w * 0.5: sur += 1
                x += step
        y += step
    return sur / tot if tot else 0.0


ALL = [("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
       ("BACK", items.BACK), ("HAIR", hair.SET)]


def survival_table(scale):
    w = H.stroke_in_R(scale)
    out = []
    for cat, d in ALL:
        for n, sh in d.items():
            for s in sh:
                if not s.filled: continue
                out.append((_erode_ratio(s.pts, w), cat, n, s.name))
    out.sort()
    return w, out


def report():
    print("╔══ 축 ① 색면 생존율 (하한 %.0f%%) ══╗" % (FILL_SURVIVAL_FLOOR * 100))
    for sc in (0.60, 0.75, 1.00):
        w, t = survival_table(sc)
        med = t[len(t) // 2][0]
        bad = sum(1 for x in t if x[0] < FILL_SURVIVAL_FLOOR)
        tag = "  ← 사용자 실제 저장 배율" if abs(sc - 0.60) < 1e-9 else ("  ← 출하 기본" if abs(sc - 0.75) < 1e-9 else "")
        print("  배율 %.2f W=%.4fR  중앙값 %5.1f%%   하한 미달 %2d/%d%s"
              % (sc, w, med * 100, bad, len(t), tag))
        if abs(sc - 0.60) < 1e-9:
            for r, cat, n, nm in t[:8]:
                print("      %5.1f%%  %-5s %-7s %s" % (r * 100, cat, n, nm))
    print("╚══════════════════════════════╝")
    print()
    print("╔══ 축 ② 커버선 정직성 (허용 %.1f획) ══╗" % COVER_HONESTY_TOLERANCE_W)
    for sc in (0.60, 0.75):
        w = H.stroke_in_R(sc)
        print("  [배율 %.2f]" % sc)
        for n, sh in items.HEAD.items():
            cov = items.COVER[n]
            m = H.measure(sh, w)
            if cov == float('inf'):
                print("    -   %-6s 커버선 없음(왕관) — 잉크 밑단 %+.3fR" % (n, m['ink_bottom']))
                continue
            gap = cov - m['ink_bottom']
            ok = gap <= COVER_HONESTY_TOLERANCE_W * w
            print("    %s %-6s 선언 %+.2fR  실제 잉크 %+.3fR  차 %.3fR = %.2f획"
                  % ("OK " if ok else "✗  ", n, cov, m['ink_bottom'], gap, gap / w))
    print("╚══════════════════════════════════╝")


if __name__ == "__main__":
    report()
