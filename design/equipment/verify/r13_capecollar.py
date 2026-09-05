# -*- coding: utf-8 -*-
"""R13 — 망토 칼라 ↔ NECK 6종 **점유 충돌** 실측과 처방  (design-equipment, 2026-09-05)

왜 이 파일이 생겼나
--------------------
`ux-designer` 가 인계본 프로토타입에서 *"긴망토 착용 시 넥타이가 칼라 뒤로 절반 들어간다
(z-order 문제로 보인다)"* 를 신고했다(`docs/UX_EQUIPMENT_WINDOW_3COL_PORT.md` §7-2 #4).
**우리 코드에서 그 가설은 반증된다.** 우리 레이어는 결정적이다:

    NECK  윤곽 7 / 채움 6        (AccessoryShapeBuilder.SortNeck = 7)
    BACK  윤곽 −1 / 채움 −2      (SortBack = −1)
    ⇒ 넥타이가 **항상** 칼라 위다. 동률도 미정도 없다.

실체는 **점유 충돌**이다 — 칼라(1.06 R × 0.44 R)가 NECK 6종의 봉투 안에 통째로 들어간다.
그 결과 덮이는 것은 넥타이가 아니라 **칼라**이고, 6종 중 3종에서 칼라가 사실상 사라진다.
칼라는 망토 3종(짧은망토·긴망토·판초)의 **유일한 보조색 조각**이라(규칙 3-2),
이 상태에서는 그 규칙이 조합에서 무효가 된다.

    python3 r13_capecollar.py               # 현행 실측 + 처방 후보 스윕
    python3 r13_capecollar.py --control     # 처방 깊이를 0으로 두면 현행과 같은가(발화 확인)

★ 교정 먼저: 칼라/NECK 좌표가 프로덕션 덤프(Tools/ShapeDump/build.sh)와 같은지를
  `mirrordrift.py` 가 이미 매 실행 확인한다(이 라운드 실측: 어긋남 0건).
  이 파일은 그 거울(items.py)만 읽는다.
"""
import math, sys
import rig, items
from rig import Shape, W

CONTROL = "--control" in sys.argv
GRID = 460           # 칼라 봉투 격자 한 변
HALF = W / 2.0       # 획 반폭(잉크 두께) — 낱선/윤곽선의 잉크를 세려면 필요하다


def bbox(p):
    return rig.bounds(p)


def seg_dist(p, a, b):
    ax, ay = a
    bx, by = b
    px, py = p
    dx, dy = bx - ax, by - ay
    L = dx * dx + dy * dy
    t = 0.0 if L == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def ink(p, shape):
    """그 점이 이 도형의 잉크(채움 + 자기 윤곽획)에 들어가는가."""
    pts = shape.pts
    n = len(pts)
    if shape.filled and rig.contains(pts, p):
        return True
    rngs = range(n) if shape.loop else range(n - 1)
    for i in rngs:
        if seg_dist(p, pts[i], pts[(i + 1) % n]) <= HALF:
            return True
    return False


def collar_band(bottom):
    """옛 CapeCollarBand. ★ 2026-09-05 오후 — 프로덕션에서 **내려갔다**(어깨 요크로 대체).
    이 파일은 「옛 상태가 무엇이었는가」를 재는 기록이므로 좌표를 여기 그대로 유지한다
    (`items.clasp()` 가 같은 이유로 남아 있다). bottom 만 처방 변수."""
    cy = items.COLLARY
    return [(0.40, cy + 0.10), (0.40, cy + bottom),
            (-0.66, cy + bottom - 0.04), (-0.66, cy + 0.10 - 0.04)]


def remnant(bottom, neck_shapes, fill_only=False):
    """칼라 잉크 중 NECK 아이템에 덮이지 않고 남는 면적 · 최대 연결조각 잉크사각형.

    fill_only=True 면 **색면만** 센다 — 바깥 획 띠를 빼고 「덩어리가 남는가」를 묻는다.
    이 저장소의 규칙 1-C 가 재는 것이 그것이고, 획 하나보다 얇은 색면은 화면에 없다."""
    band = collar_band(bottom)
    x0, y0, x1, y1 = bbox(band)
    x0 -= HALF; y0 -= HALF; x1 += HALF; y1 += HALF
    dx = (x1 - x0) / GRID
    dy = (y1 - y0) / GRID
    shp = Shape("CapeCollar", band, filled=True, tone=1)
    grid = [[False] * GRID for _ in range(GRID)]
    tot = 0
    for i in range(GRID):
        px = x0 + (i + 0.5) * dx
        for j in range(GRID):
            py = y0 + (j + 0.5) * dy
            hit = rig.contains(band, (px, py)) if fill_only else ink((px, py), shp)
            if hit:
                grid[i][j] = True
                tot += 1
    vis = [row[:] for row in grid]
    for s in neck_shapes:
        sx0, sy0, sx1, sy1 = bbox(s.pts)
        for i in range(GRID):
            px = x0 + (i + 0.5) * dx
            if px < sx0 - HALF or px > sx1 + HALF:
                continue
            for j in range(GRID):
                if not vis[i][j]:
                    continue
                py = y0 + (j + 0.5) * dy
                if py < sy0 - HALF or py > sy1 + HALF:
                    continue
                if ink((px, py), s):
                    vis[i][j] = False
    rem = sum(1 for i in range(GRID) for j in range(GRID) if vis[i][j])
    # 최대 연결 조각
    seen = [[False] * GRID for _ in range(GRID)]
    best, bb = 0, None
    from collections import deque
    for i in range(GRID):
        for j in range(GRID):
            if not vis[i][j] or seen[i][j]:
                continue
            q = deque([(i, j)]); seen[i][j] = True; cnt = 0
            mi = Mi = i; mj = Mj = j
            while q:
                a, b = q.popleft(); cnt += 1
                mi = min(mi, a); Mi = max(Mi, a); mj = min(mj, b); Mj = max(Mj, b)
                for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    na, nb = a + da, b + db
                    if 0 <= na < GRID and 0 <= nb < GRID and vis[na][nb] and not seen[na][nb]:
                        seen[na][nb] = True; q.append((na, nb))
            if cnt > best:
                best = cnt; bb = ((Mi - mi + 1) * dx, (Mj - mj + 1) * dy)
    cell = dx * dy
    # 남은 색면의 최대 내접원 반경(격자 체비셰프->유클리드 근사: BFS 다중소스 거리변환)
    INF = 10**9
    dist = [[0 if not vis[i][j] else INF for j in range(GRID)] for i in range(GRID)]
    dq = deque()
    for i in range(GRID):
        for j in range(GRID):
            if not vis[i][j]:
                continue
            if (i == 0 or j == 0 or i == GRID - 1 or j == GRID - 1
                    or not vis[i-1][j] or not vis[i+1][j] or not vis[i][j-1] or not vis[i][j+1]):
                dist[i][j] = 1
                dq.append((i, j))
    while dq:
        a, b2 = dq.popleft()
        for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            na, nb = a + da, b2 + db
            if 0 <= na < GRID and 0 <= nb < GRID and dist[na][nb] > dist[a][b2] + 1:
                dist[na][nb] = dist[a][b2] + 1
                dq.append((na, nb))
    dmax = 0
    for i in range(GRID):
        for j in range(GRID):
            if vis[i][j] and dist[i][j] < INF:
                dmax = max(dmax, dist[i][j])
    rho = dmax * min(dx, dy)
    return tot * cell, rem * cell, (best * cell), (max(bb) if bb else 0.0), rho


def cape_outline_halfwidths(y):
    """★ 2026-09-05 자기 정정 — 처음에 「x<0 점들을 y 내림차순으로 정렬해 보간」했다.
    긴망토는 **제비꼬리 노치** 때문에 그 정렬이 뒤판 바깥변이 아니라 **노치 꼭짓점**을 골랐고,
    뒤 도달이 −1.6215 대신 −0.9530(0.67 R 좁게)으로 나왔다. 프로덕션
    `AccessoryShapeBuilder.CapeShoulderYoke` 와 `items.yoke` 는 **인접 변**을 자른다 —
    `outline[1]→outline[2]`(뒤) · `outline[0]→outline[-1]`(앞). 그 규칙을 그대로 쓴다."""
    out = {}
    for name in ("짧은망토", "긴망토", "판초"):
        o = [s for s in items.BACK[name] if s.name == "CapeOutline"][0].pts
        def cut(top, bot):
            span = top[1] - bot[1]
            t = 0.0 if span <= 1e-6 else max(0.0, min(1.0, (top[1] - y) / span))
            return top[0] + (bot[0] - top[0]) * t
        out[name] = (cut(o[1], o[2]), cut(o[0], o[-1]))
    return out


# ---------------------------------------------------------------- 처방 B: 밑단 안단
BAND_T = 0.46          # AccessoryShapeBuilder.AccentBandThicknessRatio (곧은 띠 규약) — 스윕 기본값


def hem_facing(cape_name, t=BAND_T):
    """밑단 안단 — 밑단 점열(윤곽 인덱스 2..6, 흔들 구간과 **같은 점들**)을 안쪽 법선으로 t 만큼
    밀어 만든 띠. 좌표를 새로 적지 않는다(규칙 4-a: 이웃 점을 공유한다)."""
    o = [s for s in items.BACK[cape_name] if s.name == "CapeOutline"][0].pts
    hem = o[2:7]
    inner = []
    for i, (x, y) in enumerate(hem):
        a = hem[max(0, i - 1)]
        b = hem[min(len(hem) - 1, i + 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / L, tx / L            # 왼쪽 법선
        # 도형 안쪽을 고르는 부호: 옷깃 중심(0, COLLARY) 쪽
        if (nx * (0.0 - x) + ny * (items.COLLARY - y)) < 0:
            nx, ny = -nx, -ny
        inner.append((x + nx * t, y + ny * t))
    return hem + inner[::-1]


def section4():
    print("\n[4] 처방 A — 보조색을 **밑단 안단**으로 옮긴다 (칼라는 NECK 영역을 비운다)")
    print("   %-8s %10s %10s %12s %10s %10s"
          % ("망토", "안단ρ(획)", "규칙1", "자기교차", "NECK겹침", "밑단 y"))
    import r13_polish as P
    for name in ("짧은망토", "긴망토", "판초"):
        band = hem_facing(name)
        shp = Shape("CapeHemFacing", band, filled=True, tone=1)
        r = P.rho_max(band)
        v = rig.rule_one(shp, W)
        si = rig.self_intersects(band)
        # NECK 6종 중 하나라도 이 띠의 봉투에 닿는가
        bx0, by0, bx1, by1 = bbox(band)
        touch = []
        for it, shapes in items.NECK.items():
            for sh in shapes:
                sx0, sy0, sx1, sy1 = bbox(sh.pts)
                if not (sx1 + HALF < bx0 or sx0 - HALF > bx1 or sy1 + HALF < by0 or sy0 - HALF > by1):
                    touch.append(it)
        print("   %-8s %9.2f획 %10s %12s %10s %10.3f"
              % (name, r / 0.2181818, (v or "OK"), (si or "없음"),
                 (",".join(sorted(set(touch))) or "0건"), by0))


def main():
    NECK = items.NECK
    print("╔══ R13 — 망토 칼라 ↔ NECK 점유 충돌 ══╗")
    band = collar_band(-0.34)
    print("   현행 CapeCollarBand  x[%.2f, %.2f] y[%.4f, %.4f]  (짧은망토·긴망토·판초 공용)"
          % (bbox(band)[0], bbox(band)[2], bbox(band)[1], bbox(band)[3]))
    print("   레이어: NECK 윤곽 7 / 채움 6   vs   BACK 윤곽 -1 / 채움 -2")
    print("   ⇒ 동률 0. 그리기 순서는 **결정적**이고 넥타이가 위다. z-order 결함이 아니다.")

    print("\n[1] 현행 — 칼라 잉크 중 남는 것")
    print("   %-10s %12s %10s %14s %12s %8s" % ("NECK", "남는색면R²", "남는%", "최대조각R²", "잉크사각형", "잔여ρ"))
    base = None
    for item, shapes in NECK.items():
        tot, rem, bestA, span, rho = remnant(-0.34, shapes, fill_only=True)
        base = tot
        print("   %-10s %12.4f %9.1f%% %14.4f %9.2f획 %8.2f획"
              % (item, rem, 100.0 * rem / tot, bestA, span / W, rho / 0.2181818))
    print("   (칼라 잉크 총면적 %.4f R²)" % base)

    print("\n[2] 처방 스윕 — 칼라 밑변을 내려 「요크」로 만든다")
    print("   판정자: **남은 색면의 ρ ≥ 1.00획**(규칙 1-C). 획 하나보다 얇은 색면은 화면에 없다.")
    print("   %-8s %12s %14s %10s" % ("밑변", "최악 남는%", "최악 잔여ρ", "판정"))
    cand = [-0.34, -0.60, -0.80, -1.00, -1.20, -1.40, -1.60, -1.80]
    if CONTROL:
        cand = [-0.34]
    ok_bottom = None
    for b in cand:
        worst_pct, worst_span, worst_item = 1e9, 1e9, None
        for item, shapes in NECK.items():
            tot, rem, bestA, span, rho = remnant(b, shapes, fill_only=True)
            k = rho / 0.2181818
            if k < worst_span:
                worst_span = k; worst_item = item
            worst_pct = min(worst_pct, 100.0 * rem / tot)
        good = worst_span >= 1.0
        if good and ok_bottom is None:
            ok_bottom = b
        print("   %8.2f %11.1f%% %9.2f획(%s) %8s"
              % (b, worst_pct, worst_span, worst_item, "OK" if good else "미달"))

    if ok_bottom is not None:
        print("\n[3] 채택 후보 밑변 = %.2f (현행 -0.34)" % ok_bottom)
        cy = items.COLLARY
        depth_y = cy + ok_bottom
        hw = cape_outline_halfwidths(depth_y)
        print("   그 깊이(y=%.4f)에서 망토 윤곽 폭:" % depth_y)
        for k, (b, f) in hw.items():
            fits = (b <= -0.66 + 1e-6) and (f >= 0.40 - 1e-6)
            print("      %-6s 뒤 %.3f  앞 %.3f   칼라 x[-0.66,0.40] 수용: %s"
                  % (k, b, f, "OK" if fits else "✗ 넘친다"))
        band2 = collar_band(ok_bottom)
        shp = Shape("CapeCollar", band2, filled=True, tone=1)
        v = rig.rule_one(shp, W)
        print("   규칙1: %s" % (v or "OK"))
        print("   자기교차: %s" % (rig.self_intersects(band2) or "없음"))
        # ρ_max
        import r13_polish as P
        print("   ρ_max = %.4f R = %.2f획 (게이트 1.00 / 권장 1.20)"
              % (P.rho_max(band2), P.rho_max(band2) / P.GATE_RHO))
    else:
        print("\n[3] ★ 후보 없음 — 밑변을 -1.80 R 까지 내려도 최악 잔여 ρ 가 0.48획이다.")
        print("      이유: 목도리 자락·타이 blade 가 **세로로** 망토 앞을 가로질러 남는 색면을 쪼갠다.")
        print("      ⇒ 「칼라를 깊게」는 처방이 아니다. 보조색이 NECK 영역을 떠나야 한다.")
    section4()
    return 0


if __name__ == "__main__":
    sys.exit(main())
