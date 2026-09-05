# -*- coding: utf-8 -*-
"""R13 — 「조잡함」 잔여 5건 처방 + 검산  (design-equipment, 2026-09-05)

무엇을 재는가
--------------
이 저장소는 **규칙 1-C(색면 폭 ρ_max ≥ 0.21818 R)** 를 2026-09-02에 신설하면서
못 고친 4건을 `AccessoryFillAreaRuleTests.Ledger` 에 **이름으로** 남겨 두었다
(*"좌표 작업은 장비 담당 소관(리더 경유)"*). 그 4건이 아직 그대로다.
같은 4건 중 3건이 **배율 0.60(사용자 저장 배율)의 규칙1 위반 9건**에도 겹쳐 있다.

R13은 그 대장을 닫는 처방 4건 + 카탈로그 최약체 1건을 낸다.

    P1  DrawnEye (외알안경 MonocleEye · 안대 PatchEye)  아몬드 -> 12각 원반
    P2  BandanaTail                                     밑변 0.48 -> 0.76 R
    P3  HairPart (단정한머리 가르마)                      폭 x1.40
    P4  RoundBridge (동그란안경 코다리)                   붙는 자리 30도 -> 60도
        ★ 2026-09-05 오후 — **P4는 coder 라운드가 프로덕션에 착지시켰다.** 이 파일에서는
          이제 항등 변환이고, 기준선(--control)도 이미 고쳐진 상태다.
        ★★ 2026-09-05 밤 — **P1·P2·P3도 전부 착지했다**(coder). 네 처방이 전부 항등이 됐으므로
          `--control` 과 기본 실행은 **이제 같은 도형을 만든다.** 처방 기록으로 남긴다.
            · P1 -> AccessoryShapeBuilder.DrawnEyeRadiusRatio = 0.33f + items.drawn_eye()
            · P2 -> Resources/Items/equip_neck_bandana.asset (BandanaTail 0번 점 x 0.04 -> -0.24)
                    + NeckWornShapeGolden.txt 20줄 + items.bandana()
            · P3 -> AccessoryShapeBuilder.NeatPart 4점 + hair.straight()
          ★ 착지본의 HairPart 는 hair_part_fixed() 의 정확값을 **소수 둘째 자리로 정리**한 것이라
            ρ 가 0.2627 -> 0.2631 로 0.0004 R 넉넉하다(C1 허용오차 1e-3 안).

★ 교정 먼저 (거짓 통과 방지 — 이 계산기를 믿기 전에 통과해야 하는 것)
    C1  현행 ρ_max 4건이 **처방 후 예상값**과 1e-3 R 안에서 같은가
        (0.3188 / 0.3188 / 0.2627 / 0.2728)
        ★ 이 넷은 **착지 전에** §12-3 표로 공개된 값이다. 착지한 코드에서 되읽은 것이 아니라
          「그때 이렇게 될 것이다」라고 미리 적힌 숫자이므로, 지금 대조는 여전히 독립적이다.
          착지 전 기준선(0.1855 / 0.1855 / 0.1938 / 0.1942)은 r13_polish.control.out.txt 에
          그대로 남아 있다 — **되돌아가 볼 수 있는 유일한 자리이므로 지우지 마라.**
    C2  현행 채움 도형 수가 66개인가 (프로덕션 덤프 실측 = 테스트 주석)
    C3  배율 0.75 규칙1 위반 0건 / 배율 0.60 위반 **5개 도형** 인가
        ★ 이력: 9개 -> 8개(P4 착지) -> **5개**(P1 착지로 MonocleEye·PatchEye,
          P3 착지로 HairPart 가 빠졌다). **이 숫자가 다시 어긋나면 기준선이 또 움직인 것이다** —
          그때는 무엇이 움직였는지 먼저 밝히고, 이 상수를 고치기 전에 그 이유를 여기 적어라.
    ★ 하나라도 깨지면 이 파일의 숫자를 전부 폐기한다.

    python3 r13_polish.py             # 처방 적용 후 검산
    python3 r13_polish.py --control   # 처방을 하나도 안 걸었을 때 (발화 확인)

★ 이 파일은 **제안**이다. `items.py`/`hair.py`는 프로덕션 거울이므로 건드리지 않는다
  (건드리면 `mirrordrift.py` 가 즉시 빨개진다 — 그것이 그 파일의 존재 이유다).
"""
import math, sys
import rig, items, hair
from rig import Shape, W

CONTROL = "--control" in sys.argv
GATE_RHO = 0.2181818          # AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii(>=0.509)
TARGET_RHO = GATE_RHO * 1.20  # AccessoryFillAreaRuleTests.TargetStrokes
SCALES = (0.75, 0.60)


def w_at(scale):
    return rig.stroke_in_R(scale)


# ---------------------------------------------------------------- ρ_max (최대 내접원)
def _dist_to_edges(p, poly):
    px, py = p
    best = 1e9
    n = len(poly)
    for i in range(n):
        ax, ay = poly[i]
        bx, by = poly[(i + 1) % n]
        dx, dy = bx - ax, by - ay
        L = dx * dx + dy * dy
        t = 0.0 if L == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L))
        best = min(best, math.hypot(px - (ax + t * dx), py - (ay + t * dy)))
    return best


def rho_max(poly, coarse=170, refine=3):
    x0, y0, x1, y1 = rig.bounds(poly)
    best, bp = 0.0, None
    for i in range(coarse):
        px = x0 + (i + 0.5) * (x1 - x0) / coarse
        for j in range(coarse):
            py = y0 + (j + 0.5) * (y1 - y0) / coarse
            if not rig.contains(poly, (px, py)):
                continue
            d = _dist_to_edges((px, py), poly)
            if d > best:
                best, bp = d, (px, py)
    if bp is None:
        return 0.0
    step = max(x1 - x0, y1 - y0) / coarse
    for _ in range(refine):
        cx, cy = bp
        step /= 4
        for i in range(-6, 7):
            for j in range(-6, 7):
                px, py = cx + i * step, cy + j * step
                if not rig.contains(poly, (px, py)):
                    continue
                d = _dist_to_edges((px, py), poly)
                if d > best:
                    best, bp = d, (px, py)
    return best


# ---------------------------------------------------------------- 처방
EYE_DX = items.EYE_DX          # 0.62 — 옮기지 않는다(눈 자리는 리그의 사실이다)
EYE_DISC_R = 0.33              # ★ P1
BANDANA_BASE_LEFT = -0.24      # ★ P2 (현행 +0.04)
HAIR_PART_WIDEN = 1.40         # ★ P3
# ★ 2026-09-05 — P4는 **프로덕션에 착지했다**(coder). items.py 거울도 (2,4)이므로
#   round_glasses_fixed() 는 이제 현행과 같은 도형을 만든다(무해한 항등). 처방 기록으로 남긴다.
ROUND_BRIDGE_INDEX = (2, 4)    # ★ P4 — 착지 완료(옛 현행 (1, 5))


def drawn_eye_disc(sx):
    """P1 — 드러난 눈. 마름모(꼭짓점 4개)는 이 배율에서 두 결함을 동시에 낸다:
    (가) 반대칭 마름모의 ρ_max 는 짧은 반축을 못 넘는다 → 반높이 0.24 R 에서는
         **어떤 폭으로도** 1-C(0.21818 R)를 통과할 수 없다(상한이 0.24 R 이다).
    (나) 꼭짓점 4개가 전부 45도 이상 꺾여 배율 0.60에서 몽당변 2건을 낸다.
    12각 원반은 둘을 한 번에 없앤다 — 꼭짓점 회전이 30도라 **꺾임이 하나도 아니고**,
    ρ = r·cos15도 라 같은 ρ 를 마름모보다 작은 발자국으로 얻는다."""
    return rig.poly(sx * EYE_DX, 0.0, EYE_DISC_R, 12)


def bandana_fixed():
    ty = items.NECKY + 0.06
    wrap = [(-0.84, ty + 0.22), (0.0, ty + 0.10), (0.84, ty + 0.22),
            (0.84, ty - 0.22), (0.0, ty - 0.42), (-0.84, ty - 0.22)]
    # P2 — 밑변만 **뒤쪽으로** 넓힌다. 앞끝(0.52)과 꼭짓점(0.22)은 그대로라
    # 「자락이 한쪽에만 있다」는 비대칭(A = 1.219)이 보존된다.
    tail = [(BANDANA_BASE_LEFT, ty - 0.30), (0.52, ty - 0.30),
            (0.22, ty - 0.30 - rig.TORSO_R * 0.30)]
    return [Shape("BandanaWrap", wrap, filled=True),
            Shape("BandanaTail", tail, filled=True, tone=1)]


def hair_part_fixed():
    """P3 — 가르마 폭만 x1.35. 길이축(위->아래)은 건드리지 않는다."""
    src = [(-0.14, 1.56), (0.26, 1.54), (0.44, 0.60), (0.04, 0.64)]
    # 긴축 방향과 그 법선
    ax = ((src[1][0] + src[2][0]) / 2 - (src[0][0] + src[3][0]) / 2,
          (src[1][1] + src[2][1]) / 2 - (src[0][1] + src[3][1]) / 2)
    L = math.hypot(*ax)
    ux, uy = ax[0] / L, ax[1] / L          # 긴축 단위벡터(= 폭 방향)
    cx = sum(p[0] for p in src) / 4.0
    cy = sum(p[1] for p in src) / 4.0
    out = []
    for x, y in src:
        vx, vy = x - cx, y - cy
        s = vx * ux + vy * uy              # 폭 성분
        out.append((x + ux * s * (HAIR_PART_WIDEN - 1.0),
                    y + uy * s * (HAIR_PART_WIDEN - 1.0)))
    return out


def round_glasses_fixed():
    b = rig.poly(-0.62, 0.02, 0.40, 12)
    f = rig.poly(0.62, 0.02, 0.40, 12)
    i, j = ROUND_BRIDGE_INDEX
    bridge = [b[i], (0.0, 0.50), f[j]]
    return [Shape("RoundLensBack", b, filled=True),
            Shape("RoundLensFront", f, filled=True),
            Shape("RoundBridge", bridge, loop=False, tone=1)]


def build(apply_fix):
    """현행 카탈로그 사본 + (처방)."""
    HEAD = {k: list(v) for k, v in items.HEAD.items()}
    EYES = {k: list(v) for k, v in items.EYES.items()}
    NECK = {k: list(v) for k, v in items.NECK.items()}
    BACK = {k: list(v) for k, v in items.BACK.items()}
    HAIR = {k: list(v) for k, v in hair.SET.items()}
    if apply_fix:
        EYES["동그란안경"] = round_glasses_fixed()
        for name, shp in (("외알안경", "MonocleEye"), ("안대", "PatchEye")):
            EYES[name] = [Shape(s.name, drawn_eye_disc(-1), filled=True, tone=1)
                          if s.name == shp else s for s in EYES[name]]
        NECK["반다나"] = bandana_fixed()
        HAIR["단정한머리"] = [Shape(s.name, hair_part_fixed(), filled=True, tone=1)
                          if s.name == "HairPart" else s for s in HAIR["단정한머리"]]
    return dict(HEAD=HEAD, EYES=EYES, NECK=NECK, BACK=BACK, HAIR=HAIR)


# ---------------------------------------------------------------- 검산
def rule1_hits(cat, scale):
    w = w_at(scale)
    out = []
    for item, shapes in cat.items():
        for s in shapes:
            v = rig.rule_one(s, w)
            if v:
                out.append((item, s.name, v))
    return out


def main():
    cats = build(apply_fix=not CONTROL)
    tag = "--control (처방 없음)" if CONTROL else "R13 처방 적용"
    print("╔══ R13 — 조잡함 잔여 처방 검산 · %s ══╗" % tag)
    print("   W@0.75 = %.6f R   1-C 게이트 %.5f R (1.00획) / 권장 %.5f R (1.20획)"
          % (w_at(0.75), GATE_RHO, TARGET_RHO))

    # ---- 교정 --------------------------------------------------------------
    base = build(apply_fix=False)
    LEDGER = {("EYES", "안대", "PatchEye"): 0.3188,
              ("EYES", "외알안경", "MonocleEye"): 0.3188,
              ("HAIR", "단정한머리", "HairPart"): 0.2627,
              ("NECK", "반다나", "BandanaTail"): 0.2728}
    print("\n[교정 C1] 옛 면제 대장 4건이 §12-3 예고값에 닿았는가 (전부 착지 완료)")
    bad = 0
    for (cat, item, name), want in LEDGER.items():
        got = None
        for s in base[cat][item]:
            if s.name == name:
                got = rho_max(s.pts)
        ok = got is not None and abs(got - want) <= 1e-3
        bad += 0 if ok else 1
        print("   %s %-10s %-14s 대장 %.4f  실측 %.4f  %s"
              % ("OK " if ok else "!! ", item, name, want, got or -1, "" if ok else "← 어긋남"))
    nfill = sum(1 for c in base.values() for v in c.values() for s in v if s.filled)
    print("[교정 C2] 채움 도형 수 = %d (기대 66)  %s" % (nfill, "OK" if nfill == 66 else "!!"))
    if nfill != 66:
        bad += 1
    EXPECT_060 = 5   # ← 위 C3 주석의 이력을 반드시 함께 갱신할 것
    n075 = sum(len(rule1_hits(base[c], 0.75)) for c in base)
    shapes060 = set()
    for c in base:
        for item, nm, _ in rule1_hits(base[c], 0.60):
            shapes060.add((c, item, nm))
    print("[교정 C3] 현행 규칙1 위반: 배율 0.75 = %d건 (기대 0) / 배율 0.60 = %d개 도형 (기대 %d)  %s"
          % (n075, len(shapes060), EXPECT_060, "OK" if (n075 == 0 and len(shapes060) == EXPECT_060) else "!!"))
    if n075 != 0 or len(shapes060) != EXPECT_060:
        bad += 1
    if bad:
        raise SystemExit("!! 교정 실패 %d건 — 이 파일의 뒤 숫자를 전부 폐기한다." % bad)
    print("   ⇒ 교정 3종 통과. 아래 숫자를 읽어도 된다.")

    # ---- 1. 규칙 1-C -------------------------------------------------------
    print("\n[1] 규칙 1-C (색면 폭) — 처방 대상 5조각")
    print("   %-6s %-10s %-16s %9s %8s %8s" % ("슬롯", "아이템", "조각", "ρ_max", "획", "판정"))
    watch = [("EYES", "외알안경", "MonocleEye"), ("EYES", "안대", "PatchEye"),
             ("HAIR", "단정한머리", "HairPart"), ("NECK", "반다나", "BandanaTail"),
             ("NECK", "반다나", "BandanaWrap")]
    fails = 0
    for cat, item, name in watch:
        for s in cats[cat][item]:
            if s.name != name:
                continue
            r = rho_max(s.pts)
            k = r / GATE_RHO
            verdict = "✗ 게이트" if k < 1.0 - 1e-4 else ("△ 권장미달" if k < 1.20 else "OK")
            if k < 1.0 - 1e-4:
                fails += 1
            print("   %-6s %-10s %-16s %9.4f %8.2f %8s" % (cat, item, name, r, k, verdict))

    print("\n[2] 채움 전수 1-C 미달 (게이트 1.00획)")
    allbad = []
    for cat in ("HEAD", "EYES", "NECK", "BACK", "HAIR"):
        for item, shapes in cats[cat].items():
            for s in shapes:
                if not s.filled:
                    continue
                r = rho_max(s.pts)
                if r < GATE_RHO - 1e-4:
                    allbad.append((cat, item, s.name, r / GATE_RHO))
    print("   미달 %d건%s" % (len(allbad), "" if not allbad else ":"))
    for cat, item, name, k in sorted(allbad, key=lambda t: t[3]):
        print("      %-5s %-10s %-16s %.2f획" % (cat, item, name, k))

    # ---- 3. 규칙 1 (배율 축) -----------------------------------------------
    print("\n[3] 규칙 1 (잉크 사각형 · 몽당변)")
    for sc in SCALES:
        hits = []
        for cat in ("HEAD", "EYES", "NECK", "BACK", "HAIR"):
            for item, nm, v in rule1_hits(cats[cat], sc):
                hits.append((cat, item, nm, v))
        uniq = {(a, b, c) for a, b, c, _ in hits}
        print("   배율 %.2f  W=%.4f R  위반 %d건 / %d개 도형" % (sc, w_at(sc), len(hits), len(uniq)))
        for a, b, c, v in hits:
            print("      %-5s %-10s %-16s %s" % (a, b, c, v))

    # ---- 4. 자기교차 -------------------------------------------------------
    print("\n[4] 자기교차")
    xs = []
    for cat in ("HEAD", "EYES", "NECK", "BACK", "HAIR"):
        for item, shapes in cats[cat].items():
            for s in shapes:
                if s.loop and rig.self_intersects(s.pts):
                    xs.append((cat, item, s.name))
    print("   %d건 %s" % (len(xs), xs if xs else ""))

    # ---- 5. 쌍별 실루엣 차 -------------------------------------------------
    print("\n[5] 쌍별 실루엣 차 (하한 1.00획)")
    for cat in ("HEAD", "EYES", "NECK", "BACK", "HAIR"):
        names = list(cats[cat].keys())
        worst, pair = 1e9, None
        for i in range(len(names)):
            for j in range(i + 1, len(names)):
                a = rig.profile(cats[cat][names[i]])
                b = rig.profile(cats[cat][names[j]])
                d = rig.max_delta(a, b) / W
                if d < worst:
                    worst, pair = d, (names[i], names[j])
        flag = "OK" if worst >= 1.0 else "✗"
        print("   %-5s 최소 %.2f획 (%s vs %s)  %s" % (cat, worst, pair[0], pair[1], flag))
        if worst < 1.0:
            fails += 1

    # ---- 6. 규칙 4 — 처방이 만드는 새 간격 --------------------------------
    print("\n[6] 규칙 4 (간격 0 또는 ≥1획) — 처방이 건드린 이웃만")
    def gap(line_pts, poly):
        return rig.stroke_gap(line_pts, poly)
    eye = [s for s in cats["EYES"]["외알안경"] if s.name == "MonocleEye"][0]
    pod = [s for s in cats["EYES"]["외알안경"] if s.name == "MonoclePod"][0]
    d = min(math.dist(p, q) for p in eye.pts for q in pod.pts)
    print("   외알안경  눈 ↔ 알   최근접 %.4f R = %.2f획  %s"
          % (d, d / W, "OK" if (d < 1e-6 or d >= W) else "✗ 최악구간"))
    eye2 = [s for s in cats["EYES"]["안대"] if s.name == "PatchEye"][0]
    cov = [s for s in cats["EYES"]["안대"] if s.name == "PatchCover"][0]
    d2 = min(math.dist(p, q) for p in eye2.pts for q in cov.pts)
    print("   안대     눈 ↔ 천   최근접 %.4f R = %.2f획  %s"
          % (d2, d2 / W, "OK" if (d2 < 1e-6 or d2 >= W) else "✗ 최악구간"))
    tail = [s for s in cats["NECK"]["반다나"] if s.name == "BandanaTail"][0]
    wrap = [s for s in cats["NECK"]["반다나"] if s.name == "BandanaWrap"][0]
    ov = rig.contains(wrap.pts, tail.pts[0]) or rig.contains(wrap.pts, tail.pts[1])
    print("   반다나   자락 밑변이 띠 안에서 시작하는가: %s" % ("OK 겹침" if ov else "✗ 떠 있다"))

    # ---- 7. 액자 1.75 R (HEAD/HAIR 만) ------------------------------------
    # ---- 6b. 최소 배율 --------------------------------------------------
    lo, hi = 0.30, 1.00
    for _ in range(40):
        mid = (lo + hi) / 2
        n = sum(len(rule1_hits(cats[c], mid)) for c in cats)
        if n:
            lo = mid
        else:
            hi = mid
    print("\n[6b] 규칙1 위반 0이 되는 최소 배율 = %.4f  (출하 0.75까지 여유 %.4f)" % (hi, 0.75 - hi))
    for c in cats:
        for it, nm, v in rule1_hits(cats[c], hi + 1e-4):
            pass
    print("\n[7] 초상화 액자 (꼭대기 y ≤ 1.75 R)")
    over = []
    for cat in ("HEAD", "HAIR"):
        for item, shapes in cats[cat].items():
            top = max(p[1] for s in shapes for p in s.pts)
            if top > 1.75 + 1e-6:
                over.append((cat, item, top))
    print("   초과 %d건 %s" % (len(over), over if over else ""))

    print("\n╚══ 게이트 위반 %d건 ══╝" % fails)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
