# -*- coding: utf-8 -*-
"""R3 전용 자 4개 + R2 게이트 재사용 드라이버. (pack_detail_r3.py 가 부른다)"""
import sys, os, math, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import rig
import pack78_shapes as P78
import pack_detail_r2 as R2
from pack_detail_r2 import W075, GAP, NOMINAL, SORT

# ── 비트맵 실측(pack_bitmap_r3_shape.py 가 낸 값 — 재현 명령이 문서에 있다) ────────────
#    아이템 -> (종횡 W/H, **암부/(전경-트림)**, 트림%, 발광%, 몸%)
#    2번째 칸은 «색면 안에서 어두운 몫»이다 — 벡터의 트림은 선이라 면적이 없으므로 분모에서 트림을 뺀다.
BITMAP = {
    ("cyber", "HEAD"):  (1.104, 0.1009, 0.217, 0.010, 0.694),
    ("cyber", "EYES"):  (2.029, 0.1060, 0.236, 0.035, 0.648),
    ("cyber", "NECK"):  (0.740, 0.4367, 0.258, 0.060, 0.358),
    ("cyber", "BACK"):  (1.038, 0.2249, 0.124, 0.015, 0.665),
    ("mine",  "HEAD"):  (1.203, 0.4470, 0.123, 0.259, 0.225),
    ("mine",  "EYES"):  (1.842, 0.4634, 0.072, 0.151, 0.347),
    ("mine",  "NECK"):  (1.393, 0.6663, 0.077, 0.129, 0.179),
    ("mine",  "BACK"):  (0.903, 0.4640, 0.112, 0.247, 0.229),
    ("arcane", "HEAD"): (1.143, 0.4737, 0.145, 0.053, 0.397),
    ("arcane", "EYES"): (1.048, 0.3176, 0.257, 0.099, 0.408),
    ("arcane", "NECK"): (1.002, 0.4388, 0.305, 0.038, 0.352),
    ("arcane", "BACK"): (1.032, 0.5618, 0.135, 0.013, 0.366),
}

TRIM_PERIM_FLOOR = 0.25      # (T-2)
DARK_LO, DARK_HI = 0.40, 1.70    # (T-3) 비트맵 암부 점유 대비 배수 대역
ASPECT_LO, ASPECT_HI = 0.60, 1.60  # (T-4)

def polylen(pts, loop=False):
    s = sum(math.hypot(pts[i+1][0]-pts[i][0], pts[i+1][1]-pts[i][1]) for i in range(len(pts)-1))
    if loop and len(pts) > 2: s += math.hypot(pts[0][0]-pts[-1][0], pts[0][1]-pts[-1][1])
    return s

def poly_area(pts):
    n = len(pts); s = 0.0
    for i in range(n):
        x1, y1 = pts[i]; x2, y2 = pts[(i+1) % n]
        s += x1*y2 - x2*y1
    return abs(s)/2

def raster_union(polys, grid=0.02, box=None):
    """여러 다각형의 합집합 면적(격자). box 를 주면 그 상자 안만 센다."""
    if not polys: return 0.0, None
    allp = [p for poly in polys for p in poly]
    x0, y0, x1, y1 = rig.bounds(allp) if box is None else box
    xs = np.arange(x0, x1 + grid, grid); ys = np.arange(y0, y1 + grid, grid)
    XX, YY = np.meshgrid(xs, ys)
    pts = np.column_stack([XX.ravel(), YY.ravel()])
    import r24_hats as R24
    cov = np.zeros(len(pts), bool)
    for poly in polys: cov |= R24.inside(poly, pts)
    return float(cov.sum())*grid*grid, cov.reshape(XX.shape)

def r3_metrics(pcs, slot):
    """R3 신규 4축의 측정값."""
    allp = [q for p in pcs for q in p.pts]
    x0, y0, x1, y1 = rig.bounds(allp)
    aspect = (x1-x0)/max(1e-9, (y1-y0))
    # 실루엣 둘레 = 주색/보조색 채움 조각들의 윤곽 길이 합
    body = [p for p in pcs if p.filled and not p.noStroke]
    perim = sum(polylen(p.pts, p.loop) for p in body)
    trim = sum(polylen(p.pts, p.loop) for p in pcs if p.kind == "trim")
    # 암부 = tone 2 채움 면적 + 잉크 솔기(tone 4)의 획 면적 + 채움 윤곽의 획 면적
    # 암부 = tone 2 채움 면적 / 전체 채움 면적. 잉크 «선»은 덩어리가 아니므로 세지 않는다
    # (첫 시안은 획 면적을 넣었다가 작은 아이템에서 6배로 부풀었다 — 자가 틀렸던 것이지 조형이 틀린 것이 아니었다).
    a_dark, _ = raster_union([p.pts for p in pcs if p.tone == 2 and p.filled])
    a_all, _ = raster_union([p.pts for p in pcs if p.filled])
    a_all = max(a_all, 1e-6)
    n_dark = sum(1 for p in pcs if p.tone == 2 and p.filled)
    return dict(aspect=aspect, perim=perim, trim=trim, trim_ratio=(trim/perim if perim > 0 else 0.0),
                dark=a_dark/a_all, area=a_all, dark_floor=n_dark*(1.5*W075)**2/a_all)

def trim_on_outline(pcs, tol=1e-9):
    """(T-1) 트림선의 모든 점이 어떤 채움 조각의 윤곽 점과 «정확히» 일치하는가.
    일치하면 규칙 4 거리 0(닿음)이 구조적으로 보장된다 — 부동소수 반올림에 기대지 않는다."""
    owned = set()
    for p in pcs:
        if p.filled: owned.update((round(x, 9), round(y, 9)) for x, y in p.pts)
    bad = []
    for p in pcs:
        if p.kind != "trim": continue
        for x, y in p.pts:
            if (round(x, 9), round(y, 9)) not in owned: bad.append((p.name, x, y))
    return bad

def install_calibration():
    """R2 가 착지한 뒤 옛 교정값(출하 후드 ↔ 베레모 = 0.100)은 **사라졌다** — 지금 트리의 후드는 R2 좌표다.
    그래서 교정을 둘로 바꾼다:
      (가) 출하(R2) 후드 ↔ 베레모 = **0.424** — R2 라운드가 «설계 좌표»에서 낸 값(R2 문서 §5-1)을,
           지금은 «에셋 파서 + 프로덕션 덤프»라는 **다른 경로**로 재현한다(삼자 일치).
      (나) ★ 팩과 무관한 순수 프로덕션 쌍 **베레모 ↔ 야구모자 = 0.298** — 내 작업이 아무리 흔들려도
           이 값은 움직이면 안 된다. (가)만 두면 «내가 만든 값으로 나를 교정»하는 꼴이 된다."""
    import legibility as LG, pack_assets as PA
    base = PA.prod_base(False)
    beret = [s for n, s in base["HEAD"] if n == "베레모"][0]
    cap = [s for n, s in base["HEAD"] if n == "야구모자"][0]
    d2 = LG.difference(LG.cells(beret), LG.cells(cap))
    okb = abs(d2 - 0.298) <= 0.02
    print("  자 교정(나) 프로덕션 베레모 ↔ 야구모자 = %.3f (기대 0.298 ± 0.02) -> %s" % (d2, "OK" if okb else "★ 실패"))
    if not okb: sys.exit(2)
    _orig = LG.calibrate
    LG.calibrate = lambda hood, beret, expect=0.424, tol=0.02: _orig(hood, beret, expect, tol)


def main(PACKS, FILES):
    R2.PACKS = PACKS; R2.FILES = FILES
    if "--verify-emit" in sys.argv:
        sys.exit(0 if verify_emit(PACKS, FILES) else 1)
    if "--dump" in sys.argv:
        for packname, key, motif, PACK in PACKS:
            print("\n##### %s" % packname)
            for slot in ("HEAD", "EYES", "NECK", "BACK"):
                disp, iid, fn = PACK[slot]
                pcs = fn()
                print("\n[%s] %s  %s   (앵커 y=%.5f)" % (slot, disp, iid, R2.ANCHOR[slot]))
                for p in pcs:
                    pr = p.params(slot)
                    print("  %-18s %-6s loop=%-5s filled=%-5s tone=%d layer=%d strokeMult=%.2f strokeInR=%.5f noStroke=%d lineAlpha=%.2f n=%d%s"
                          % (p.name, p.kind, p.loop, p.filled, p.tone, p.layer, pr["strokeMult"], pr["strokeInR"],
                             pr["noStroke"], pr["lineAlpha"], len(p.pts), ("" if not p.sway else "  sway=%d+%d" % p.sway)))
                    for x, y in p.pts: print("      (%+.4f, %+.4f)" % (x, y))
        sys.exit(0)
    if "--emit" in sys.argv:
        out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack_detail_r3")
        os.makedirs(out, exist_ok=True)
        for packname, key, motif, PACK in PACKS:
            for slot in ("HEAD", "EYES", "NECK", "BACK"):
                disp, iid, fn = PACK[slot]
                path = os.path.join(out, FILES[(key, slot)] + ".wornShapes.yaml")
                open(path, "w", encoding="utf-8").write(emit_yaml(fn(), slot))
                print("wrote", os.path.relpath(path))
        sys.exit(0)
    if "--scales" in sys.argv:
        scale_sweep(PACKS); sys.exit(0)
    if "--bitmap" in sys.argv:
        bitmap_table(PACKS); sys.exit(0)

    install_calibration()
    ctl = "--control" in sys.argv
    if ctl: print("★ 양성 대조 모드 — 나쁜 값을 넣는다. 빨간불이 켜져야 정상.\n")
    n = R2.gate(ctl)
    print("\n" + "="*104)
    print("  R3 신규 5축 —  (T-1) 트림선이 윤곽 위 · (T-2) 트림/둘레 >= %.2f · (T-3) 암부 배수 [%.2f,%.2f] · (T-4) 종횡 배수 [%.2f,%.2f] · (T-5) 흰 선 host"
          % (TRIM_PERIM_FLOOR, DARK_LO, DARK_HI, ASPECT_LO, ASPECT_HI))
    print("="*104)
    hdr = "  %-16s %-6s %8s %8s %7s %10s %8s %8s %7s %9s" % ("아이템", "슬롯", "종횡", "비트맵", "배수", "트림/둘레", "암부", "비트맵", "배수", "구조바닥")
    print(hdr)
    for packname, key, motif, PACK in PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = PACK[slot]
            pcs = fn()
            if ctl and (key, slot) == ("cyber", "HEAD"):
                pcs = [p for p in pcs if p.kind != "trim"]                       # 대조 ⑥ 트림 제거 -> T-2 위반
            if ctl and (key, slot) == ("arcane", "BACK"):
                pcs = list(pcs)
                for k, pp in enumerate(pcs):                                     # 대조 ⑧ host 를 지운다 -> T-5
                    if pp.tone == 3: pp.host = None
            if ctl and (key, slot) == ("mine", "NECK"):
                from pack_detail_r3 import Piece
                pcs = [p for p in pcs if p.tone != 2]                            # 대조 ⑦ 암부 제거 -> T-3 위반
            m = r3_metrics(pcs, slot)
            b = BITMAP[(key, slot)]
            ar = m["aspect"]/b[0]; dr = m["dark"]/b[1]
            bad = trim_on_outline(pcs)
            for nm, x, y in bad: R2.bad("%s 트림선 '%s' 점 (%.4f,%.4f) 이 윤곽 위가 아니다 (T-1)" % (disp, nm, x, y))
            for nm, why in host_report(pcs): R2.bad("%s 흰 선 '%s' %s (T-5 — 카드와 몸이 갈라진다)" % (disp, nm, why))
            if m["trim_ratio"] < TRIM_PERIM_FLOOR: R2.bad("%s 트림/둘레 %.3f < %.2f (T-2)" % (disp, m["trim_ratio"], TRIM_PERIM_FLOOR))
            # ★ 구조적 바닥: 규칙 1 이 채움 조각의 최소 크기를 1.5W x 1.5W 로 못박으므로, 작은 아이템에서는
            #   «어두운 조각 1개»만으로도 비트맵 점유를 넘어설 수 있다. 그 경우는 조형이 아니라 자의 문제다.
            floor = m["dark_floor"]
            hi_allowed = max(DARK_HI*b[1], floor)
            if not (DARK_LO*b[1] <= m["dark"] <= hi_allowed):
                R2.bad("%s 암부 점유 %.3f (비트맵 %.3f, 배수 %.2f) 대역 [%.3f,%.3f] 밖 (T-3, 구조바닥 %.3f)"
                       % (disp, m["dark"], b[1], dr, DARK_LO*b[1], hi_allowed, floor))
            if not (ASPECT_LO <= ar <= ASPECT_HI): R2.bad("%s 종횡 %.3f (비트맵 %.3f, 배수 %.2f) 대역 [%.2f,%.2f] 밖 (T-4)" % (disp, m["aspect"], b[0], ar, ASPECT_LO, ASPECT_HI))
            print("  %-16s %-6s %8.3f %8.3f %7.2f %10.3f %8.3f %8.3f %7.2f %9.3f" % (disp, slot, m["aspect"], b[0], ar, m["trim_ratio"], m["dark"], b[1], dr, m["dark_floor"]))
    total = len(R2._fail)
    print("\n결과(R2+R3 합계): %s" % ("전수 통과 (위반 0건)" if total == 0 else "위반 %d건" % total))
    if ctl:
        axes = {k: sum(1 for m in R2._fail if k in m) for k in ("T-1", "T-2", "T-3", "T-4", "T-5")}
        print("\n★ 양성 대조: 위반 %d건 · R3 축별 검출 %s" % (total, axes))
        okc = total >= 7 and all(v > 0 for v in axes.values())
        print("   -> %s" % ("OK — 5축이 전부 실제로 문다" if okc else "FAIL — 안 무는 축이 있다(그 축의 «위반 0건»은 무의미하다)"))
        sys.exit(0 if okc else 1)
    sys.exit(1 if total else 0)

def emit_yaml(pcs, slot):
    """R2.emit_yaml 과 같되 **underBack 을 실제로 적는다**(R2 는 12종 전부 0 이었다).
    카드 경로가 밑색을 «카드 바탕»으로 잡는 것을 막는 유일한 키다 — 이 키가 0 이면 카드와 몸이 갈라진다."""
    names = [p.name for p in pcs]
    lines = ["  wornShapes:"]
    for i, p in enumerate(pcs):
        pr = p.params(slot)
        sway = p.sway or (-1, 0)
        ub = 0 if getattr(p, "host", None) is None else i - names.index(p.host)
        lines += ["  - name: %s" % p.name, "    loop: %d" % (1 if p.loop else 0), "    filled: %d" % (1 if p.filled else 0),
                  "    tone: %d" % p.tone, "    swayStart: %d" % sway[0], "    swayCount: %d" % sway[1], "    swingDegrees: 0",
                  "    surfaces: 0", "    strokeMult: %s" % R2.fmt(pr["strokeMult"]), "    strokeInR: %s" % R2.fmt(pr["strokeInR"]),
                  "    noStroke: %d" % pr["noStroke"], "    alpha: %s" % R2.fmt(pr["alpha"]), "    lineAlpha: %s" % R2.fmt(pr["lineAlpha"]),
                  "    underBack: %d" % ub, "    layer: %d" % p.layer, "    bodyFixed: 0", "    terms:", "    - %d" % len(p.pts)]
        for x, y in p.pts:
            lines += ["    - 1", "    - 0", "    - 0", "    - 0", "    - 1", "    - %s" % R2.fmt(x),
                      "    - 2", "    - 4", "    - 0", "    - 0", "    - 0", "    - 0", "    - 0", "    - 0", "    - 1", "    - %s" % R2.fmt(y)]
    return "\n".join(lines) + "\n"


def host_report(pcs):
    """(T-5) tone 3(흰 선)은 반드시 **앞선 채움 조각**을 host 로 갖는다."""
    names = [p.name for p in pcs]
    out = []
    for i, p in enumerate(pcs):
        if p.tone != 3: continue
        h = getattr(p, "host", None)
        if h is None: out.append((p.name, "host 없음")); continue
        if h not in names: out.append((p.name, "host '%s' 없음" % h)); continue
        j = names.index(h)
        if j >= i: out.append((p.name, "host '%s' 가 뒤에 있다" % h))
        elif not pcs[j].filled: out.append((p.name, "host '%s' 가 채움이 아니다" % h))
    return out


def verify_emit(PACKS, FILES):
    """방출한 YAML 을 «에셋 파서»(pack_assets.parse_asset — 생성기와 다른 경로)로 되읽어 설계 좌표와 대조한다."""
    import pack_assets as PA
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack_detail_r3")
    bad_n = pieces = pts = 0
    for packname, key, motif, PACK in PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = PACK[slot]
            path = os.path.join(out, FILES[(key, slot)] + ".wornShapes.yaml")
            txt = ("  displayName: %s\n  slot: %d\n" % (disp, {"HEAD": 0, "EYES": 1, "NECK": 2, "BACK": 3}[slot])
                   + open(path, encoding="utf-8").read() + "  wornGroupAlpha: 0\n")
            tmp = os.path.join(out, "_roundtrip.asset")
            open(tmp, "w", encoding="utf-8").write(txt)
            _, _, shapes = PA.parse_asset(tmp); os.remove(tmp)
            design = fn()
            # underBack 은 파서가 안 뽑으므로 방출 텍스트에서 직접 센다(존재 단언)
            ubs = [int(m) for m in __import__("re").findall(r"^    underBack: (-?\d+)$", open(path, encoding="utf-8").read(), __import__("re").M)]
            names = [p.name for p in design]
            want = [0 if getattr(p, "host", None) is None else k - names.index(p.host) for k, p in enumerate(design)]
            if ubs != want:
                bad_n += 1; print("  x underBack 불일치:", disp, ubs, want)
            if [s["name"] for s in shapes] != [p.name for p in design]:
                bad_n += 1; print("  x 조각 이름/순서 불일치:", disp); continue
            for s, p in zip(shapes, design):
                pieces += 1
                pr = p.params(slot)
                if (s["loop"], s["filled"], s["tone"], s["noStroke"], s["layer"]) != (p.loop, p.filled, p.tone, p.noStroke, p.layer) \
                   or abs(s["strokeInR"] - pr["strokeInR"]) > 1e-9 or abs(s["strokeMult"] - pr["strokeMult"]) > 1e-9 \
                   or abs(s["lineAlpha"] - pr["lineAlpha"]) > 1e-9:
                    bad_n += 1; print("  x 속성 불일치:", disp, p.name)
                if len(s["pts"]) != len(p.pts):
                    bad_n += 1; print("  x 점 수 불일치:", disp, p.name, len(s["pts"]), len(p.pts)); continue
                for (x1, y1), (x2, y2) in zip(s["pts"], p.pts):
                    pts += 1
                    if abs(x1 - x2) > 5e-5 or abs(y1 - y2) > 5e-5:
                        bad_n += 1; print("  x 좌표 불일치: %s %s (%.5f,%.5f) vs (%.5f,%.5f)" % (disp, p.name, x1, y1, x2, y2))
    print("왕복 검산: 조각 %d · 점 %d · 불일치 %d건 -> %s" % (pieces, pts, bad_n, "OK" if bad_n == 0 else "FAIL"))
    # 양성 대조 — 좌표 한 칸을 흔든 사본이 실제로 빨개지는가
    path = os.path.join(out, FILES[("cyber", "HEAD")] + ".wornShapes.yaml")
    txt = open(path, encoding="utf-8").read().replace("    - 1.6\n", "    - 1.61\n", 1)
    tmp = os.path.join(out, "_roundtrip.asset")
    open(tmp, "w", encoding="utf-8").write("  displayName: x\n  slot: 0\n" + txt + "  wornGroupAlpha: 0\n")
    _, _, shapes = PA.parse_asset(tmp); os.remove(tmp)
    d = PACKS[0][3]["HEAD"][2]()
    moved = any(abs(x1 - x2) > 5e-5 for (x1, _), (x2, _) in zip(shapes[0]["pts"], d[0].pts))
    print("왕복 양성 대조(좌표 한 칸 +0.01): %s" % ("잡는다" if moved else "★ 못 잡는다 — 위 OK 를 믿지 마라"))
    return bad_n == 0 and moved


def scale_sweep(PACKS):
    """배율별 규칙 1 / 규칙 4 — **출하(R2 에셋)와 나란히**. 게이트 자체는 출하 기본 0.75 로 판정하지만,
    사용자 저장 배율이 0.60 이라는 실측(EQUIPMENT_SHAPE_SPEC §12)이 있으므로 회귀 여부를 여기서 본다.

    ★★ 2026-09-09 착지 이후 이 표의 「출하」 열은 **죽었다**(coder-systems 실측).
    R3 좌표가 pack_*.asset 에 들어간 순간 PA.pack_items() 가 읽는 것이 곧 R3 라서
    두 열이 6개 배율 전부에서 같은 값(67/40 · 35/15 · 23/11 · 0/0 · 0/0 · 0/0)을 낸다.
    비교 기준으로 쓰지 마라 — 착지 전 기록은 pack_detail_r3_scales.out.txt 에만 남아 있다
    (57/39 · 36/23 · 28/13 ...). R3 사양서 §7 F-4 가 R2 하니스에서 지적한 구조가
    한 세대 뒤 여기서 똑같이 일어난 것이다. 되살리려면 출하 좌표를 git 에서 읽어야 한다.
    ★ 다만 두 열이 «같아진 것» 자체는 착지가 충실했다는 독립 확인이기도 하다
      (에셋 파싱 경로와 설계 함수 경로가 규칙1/규칙4 위반 수까지 일치한다)."""
    import pack_assets as PA
    from rig import Shape
    sh = PA.pack_items()
    class Adapter:
        def __init__(s, d):
            s.pts, s.loop, s.filled, s.tone, s.noStroke, s.layer, s.name = \
                d["pts"], d["loop"], d["filled"], d["tone"], d["noStroke"], d["layer"], d["name"]
    D = {}
    for pn, k, mo, P in PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"): D[(k, slot)] = P[slot][2]()
    def count(get, sc):
        W = rig.stroke_in_R(sc); GAP = 1.5*W; v1 = v4 = 0
        for k in ("cyber", "mine", "arcane"):
            for slot in ("HEAD", "EYES", "NECK", "BACK"):
                pcs = get(k, slot)
                for p in pcs:
                    if rig.rule_one(Shape(p.name, p.pts, p.loop, p.filled, p.tone, 0), W): v1 += 1
                st = [p for p in pcs if not p.noStroke]
                for a in range(len(st)):
                    for b in range(a+1, len(st)):
                        d = R2.piece_dist(st[a], st[b])
                        if 1e-6 < d < GAP - 1e-6: v4 += 1
        return v1, v4
    print("배율별 규칙1 / 규칙4 — 출하(R2 에셋) vs R3")
    print("  배율    W(R)     출하 규칙1  규칙4      R3 규칙1  규칙4")
    for sc in (0.35, 0.50, 0.60, 0.75, 1.00, 1.50):
        a = count(lambda k, s: [Adapter(d) for d in sh[(s, k)][1]], sc)
        b = count(lambda k, s: D[(k, s)], sc)
        tag = "  <- 출하 기본" if sc == 0.75 else ("  <- 사용자 저장 배율" if sc == 0.60 else
              ("  <- 다이얼 최소(실루엣 전용 구간, 스펙 9-5 면제)" if sc == 0.35 else ""))
        print("  %.2f   %.4f      %5d %6d      %5d %6d%s" % (sc, rig.stroke_in_R(sc), a[0], a[1], b[0], b[1], tag))


def bitmap_table(PACKS):
    print("아이템별 R3 구조 vs 비트맵 실측")
    for packname, key, motif, PACK in PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = PACK[slot]
            m = r3_metrics(fn(), slot); b = BITMAP[(key, slot)]
            print("  %-16s 종횡 %.3f/%.3f  트림/둘레 %.3f  암부 %.3f/%.3f" % (disp, m["aspect"], b[0], m["trim_ratio"], m["dark"], b[1]))
