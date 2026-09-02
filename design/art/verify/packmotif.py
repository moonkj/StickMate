# -*- coding: utf-8 -*-
"""★ 6팩 모티프 축 — 색이 다 떨어진 자리에서 팩을 가르는 것 (design-art, 2026-09-02 4차)

    python3 packmotif.py             # 분류 게이트
    python3 packmotif.py --control   # ★ 양성 대조 — 일부러 어긋난 조각을 넣어 게이트가 잡는가

왜 이 파일이 있는가
-------------------
PALETTE_SPEC §3-3 실측: 팩 12색의 최소 ΔE 29.8 < **식별 하한 48.6**.
즉 **색만 보고는 어느 팩인지 못 맞힌다.** 색은 "맞다"를 확인시킬 뿐이다.
그러면 "어느 팩인가"는 무엇이 지는가 — **보조색 조각의 모양**이다.

여섯 팩이 「한 세계」로 보이면서 서로 갈리려면, 모양도 색과 **같은 문법**이어야 한다:
  색  : 규칙 하나(채도 최대/최소) + 값 여섯(색상각)
  모양 : 규칙 하나(보조색 조각 1개) + 값 여섯(불변량)

★ 쓰는 자는 **크기 불변량**뿐이다. 카드는 아이템을 자기 경계상자에 맞춰 정규화하므로
  (design/equipment/verify/verify.py의 k = 44*0.86/최대변) "크다/작다"는 카드에서 사라진다.
  그래서 꼭짓점 수 · 종횡비 · 볼록결손 · 돌출 개수만 쓴다 — 전부 sectors.py에 이미 있는 자다.
"""
import sys, os, math

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "design", "equipment", "verify"))
import rig, sectors as S
from rig import Shape

FAIL = []
def mark(cond, msg):
    if not cond: FAIL.append(msg)
    return "OK " if cond else "✗  "


# ============================================================================
# 1. 모티프 규격 — 팩당 한 줄. 사람이 좌표를 고르는 자리가 아니라 **창(window)**이다.
# ============================================================================
#  (팩, 조각 이름, 꼭짓점 n, 종횡비 창, 볼록결손 창, 돌출 개수)
SPEC = [
    ("오피스 워커",       "납작한 4각 판",   (4, 4),   (1.50, 2.20), (0.00, 0.05), (0, 0)),
    ("사이버 아포칼립스", "모서리 잘린 6각", (6, 8),   (1.00, 1.70), (0.00, 0.05), (0, 0)),
    ("네온 낙서",         "비대칭 3각",      (3, 3),   (1.20, 2.10), (0.00, 0.05), (2, 3)),
    ("스포츠",            "둥근 다각",       (12, 24), (1.00, 1.25), (0.00, 0.05), (0, 0)),
    ("컬러 잉크",         "눈물방울",        (12, 24), (1.20, 1.90), (0.00, 0.12), (1, 1)),
    ("밀리터리",          "꺾인 띠",         (6, 8),   (1.00, 1.60), (0.15, 0.60), (0, 0)),
]
MOTIF_THICK = 0.46          # pack_office.py와 같은 값 — 규칙 1-C(ρ_max >= 0.43636R)의 여유분


SHARP_DEG = 70.0            # 이 각보다 뾰족한 내각을 「첨점」으로 센다


def classify(n, aspect, cdef, sharp):
    """★ 결정 6잎. 잎마다 조건 **하나** — 색 규칙(채도 최대/최소)과 같은 문법이다.

    쓰는 자는 셋뿐이고 전부 크기 불변량이다: 볼록결손 · 꼭짓점 수 · 첨점 개수."""
    if cdef >= 0.15:  return "밀리터리"           # 꺾인다
    if n <= 3:        return "네온 낙서"           # 튄다
    if n <= 4:        return "오피스 워커"         # 납작하다
    if n <= 8:        return "사이버 아포칼립스"   # 모서리가 잘렸다
    if sharp >= 1:    return "컬러 잉크"           # 둥근데 꼭지가 하나 있다
    return "스포츠"                                 # 둥글다


# ============================================================================
# 2. 원형(prototype) — 규격이 실제로 그릴 수 있는 값인가를 보이는 최소 예시
#    ★ 이것은 최종 좌표가 아니다. 최종 좌표는 design-equipment가 낸다.
# ============================================================================
def _plate(L, T, deg=0.0):
    a = math.radians(deg); ux, uy = math.cos(a), math.sin(a); nx, ny = -uy, ux
    hl, ht = L * .5, T * .5
    return [(ux*hl + nx*ht, uy*hl + ny*ht), (ux*hl - nx*ht, uy*hl - ny*ht),
            (-ux*hl - nx*ht, -uy*hl - ny*ht), (-ux*hl + nx*ht, -uy*hl + ny*ht)]


def proto_office():   return _plate(0.80, MOTIF_THICK)


def proto_cyber():
    """모서리 잘린 6각 — 커넥터 패드. 네 모서리를 45°로 자른다."""
    L, T, c = 0.66, MOTIF_THICK, 0.13
    hl, ht = L*.5, T*.5
    return [(-hl+c, ht), (hl-c, ht), (hl, ht-c), (hl, -ht+c), (hl-c, -ht), (-hl+c, -ht),
            (-hl, -ht+c), (-hl, ht-c)]


def proto_graffiti():
    """비대칭 3각 — 스프레이가 튄 조각. 이등변이 아니다.
    ★ 첫 시안(0.83배)은 색면 조건 ρ_max 0.1897R로 **깨졌다**. 삼각형은 내접원이 작다."""
    k = 1.22
    return [(-0.34*k, -0.20*k), (0.46*k, -0.08*k), (-0.02*k, 0.34*k)]


def proto_sports(n=16):
    r = 0.28
    return [(math.cos(2*math.pi*i/n)*r*1.10, math.sin(2*math.pi*i/n)*r) for i in range(n)]


def proto_ink(n=16):
    """눈물방울 — 원 + **첨점 하나**. 그 꼭지 하나가 이 팩의 표식이다.

    ★ 첫 시안은 반지름을 코사인으로 부풀린 「부푼 원」이었다. 그건 첨점을 만들지 못했다
      (표본 간격이 넓어 꼭짓점 내각이 70° 아래로 안 내려간다) — 게이트가 「스포츠」로
      잡아냈고, 그 판정이 옳았다. 첨점은 **부풀려서가 아니라 접선 두 개로** 만들어야 한다.
    꼭지 내각 = 2·asin(r/d). r/d = 0.4615 → 55.0°(< 70°)."""
    r, d = 0.24, 0.52
    th = math.acos(r / d)                                # 접점 각 62.5°
    pts = [(d, 0.0)]
    for i in range(n):                                   # 접점 → 뒤로 한 바퀴 → 반대 접점
        t = th + (2*math.pi - 2*th) * i / (n - 1)
        pts.append((math.cos(t)*r, math.sin(t)*r))
    return pts


def proto_military():
    """★ 꺾인 띠 — 웨빙/스트랩이 버클에서 직각으로 꺾인 자리. **볼록결손이 표식**이다.

    첫 시안은 「구멍 뚫린 띠」였는데 두 가지로 깨졌다:
      (1) 파임이 얕아 볼록결손 0.1203 < 0.15  → 잉크 잎으로 잘못 떨어졌다
      (2) 파임이 내접원을 먹어 ρ_max 0.2038R < 0.21818R (색면 조건 미달)
    꺾음(L)은 **결손을 벌면서 내접원을 안 먹는다** — 폭이 그대로 남기 때문이다."""
    a, w = 1.40, MOTIF_THICK
    return [(0, 0), (a, 0), (a, w), (w, w), (w, a), (0, a)]


PROTO = {"오피스 워커": proto_office, "사이버 아포칼립스": proto_cyber,
         "네온 낙서": proto_graffiti, "스포츠": proto_sports,
         "컬러 잉크": proto_ink, "밀리터리": proto_military}


# ============================================================================
# 3. 재기 — 전부 크기 불변량
# ============================================================================
def sharp_corners(pts, deg=None):
    """내각이 `deg`보다 뾰족한 꼭짓점 수. **크기 불변량**(각도라서 정규화에 안 죽는다)."""
    if deg is None: deg = SHARP_DEG
    n = len(pts); c = 0
    for i in range(n):
        a, b, d = pts[(i-1) % n], pts[i], pts[(i+1) % n]
        v1 = (a[0]-b[0], a[1]-b[1]); v2 = (d[0]-b[0], d[1]-b[1])
        n1 = math.hypot(*v1); n2 = math.hypot(*v2)
        if n1 < 1e-12 or n2 < 1e-12: continue
        cos = max(-1.0, min(1.0, (v1[0]*v2[0] + v1[1]*v2[1]) / (n1*n2)))
        if math.degrees(math.acos(cos)) < deg: c += 1
    return c


def measure(pts):
    n = len(pts)
    sh = [Shape("M", list(pts), True, filled=True)]
    x0, y0, x1, y1 = rig.bounds(pts)
    w, h = x1 - x0, y1 - y0
    aspect = max(w, h) / max(1e-9, min(w, h))
    cdef = S.convex_deficiency(sh)
    return n, aspect, cdef, sharp_corners(pts)


def rho_max(pts):
    """도형 안에 들어가는 최대 원 반지름 — hatfix.py의 자를 그대로 쓴다(색면 조건)."""
    import hatfix
    return hatfix.rho_max(pts)


def calibrate():
    """★ 알려진 값으로 먼저 맞춘다. 깨지면 아무 숫자도 안 낸다."""
    print("=" * 96)
    print("§0. 교정 — 알려진 도형으로 자를 먼저 맞춘다")
    print("=" * 96)
    sq = [(-.5, -.5), (.5, -.5), (.5, .5), (-.5, .5)]
    n, a, c, p = measure(sq)
    print("  %s 정사각형      n=%d(4)  종횡비 %.4f(1.0)  볼록결손 %.4f(0)  첨점 %d(0, 내각 90°)"
          % (mark(n == 4 and abs(a-1) < 1e-9 and c < 1e-6 and p == 0, "교정: 정사각형"), n, a, c, p))
    tri = [(0, 0), (1, 0), (.5, math.sqrt(3)/2)]
    _, _, _, p2 = measure(tri)
    print("  %s 정삼각형      첨점 %d (정답 3 — 내각 60° < %.0f°)"
          % (mark(p2 == 3, "교정: 정삼각형 첨점 3"), p2, SHARP_DEG))
    rc = [(-1, -.5), (1, -.5), (1, .5), (-1, .5)]
    n2, a2, c2, _ = measure(rc)
    print("  %s 2:1 직사각형  n=%d(4)  종횡비 %.4f(2.0)  볼록결손 %.4f(0)"
          % (mark(abs(a2-2) < 1e-9 and c2 < 1e-6, "교정: 2:1 직사각형"), n2, a2, c2))
    # 볼록결손: 정사각형에서 한 귀퉁이를 삼각형으로 도려내면 결손 = 도려낸 면적 / 껍질 면적
    notch = [(-.5, -.5), (.5, -.5), (.5, .5), (0.0, 0.0), (-.5, .5)]
    _, _, c3, _ = measure(notch)
    print("  %s 귀퉁이 파임    볼록결손 %.4f (정답 0.2500 = 0.25/1.00)"
          % (mark(abs(c3 - 0.25) < 2e-3, "교정: 볼록결손 0.25"), c3))
    # ★ 실측 교정 — 출하 준비된 오피스 모티프 판(pack_office.py)이 오피스로 분류돼야 한다
    import importlib
    po = importlib.import_module("pack_office")
    acc = [s for s in po.PACK["NECK"][1] if s.tone == 1]
    got = classify(*measure(acc[0].pts)) if acc else "(없음)"
    print("  %s 실측: pack_office.py NECK 보조색 조각 → 분류 「%s」 (기대 오피스 워커)"
          % (mark(got == "오피스 워커", "교정: 출하 오피스 모티프 오분류"), got))
    if FAIL:
        print("\n★ 교정 실패 — 죽는다."); [print("   ·", f) for f in FAIL]; sys.exit(2)
    print("\n→ 교정 통과.\n")


def gate():
    print("=" * 96)
    print("§1. 여섯 모티프 — 규칙 하나(보조색 조각 1개) + 값 여섯(불변량)")
    print("=" * 96)
    print("  %-20s %-14s %4s %8s %9s %5s  %-20s" %
          ("팩", "조각", "n", "종횡비", "볼록결손", "첨점", "분류 결과"))
    rows = []
    for name, label, (n0, n1), (a0, a1), (c0, c1), (p0, p1) in SPEC:
        pts = PROTO[name]()
        n, a, c, p = measure(pts)
        got = classify(n, a, c, p)
        inwin = (n0 <= n <= n1) and (a0 <= a <= a1) and (c0 <= c <= c1) and (p0 <= p <= p1)
        ok = inwin and got == name
        rows.append((name, pts, n, a, c, p, got))
        print("  %s%-19s %-14s %4d %8.3f %9.4f %5d  %-20s" %
              (mark(ok, "%s 규격창 밖 또는 오분류(→%s)" % (name, got)), name, label, n, a, c, p, got))
    # 분류가 여섯 잎을 **전부** 쓰는가 (한 잎에 둘이 앉으면 그 축은 죽은 축이다)
    got = [r[6] for r in rows]
    print("\n  %s 여섯 잎이 전부 쓰인다 (중복 %d건)" %
          (mark(len(set(got)) == 6, "분류 잎 중복 — 축이 죽었다"), 6 - len(set(got))))
    # 색면 조건 — 모티프 조각은 채움이므로 규칙 1-C를 지켜야 한다
    import hatfix
    print("\n  색면 조건 (ρ_max ≥ %.5fR = 채움 윤곽 펜 하나)" % hatfix.FILL_OUTLINE_PEN_IN_R)
    for name, pts, *_ in rows:
        r = rho_max(pts)
        print("  %s%-19s ρ_max %.4fR = %.2f획" %
              (mark(r >= hatfix.FILL_OUTLINE_PEN_IN_R, "%s 색면 조건 미달" % name),
               name, r, r / hatfix.FILL_OUTLINE_PEN_IN_R))
    return rows


def control():
    print("=" * 96)
    print("★ 양성 대조 — 어긋난 조각을 넣으면 게이트가 **빨간불을 내는가**")
    print("=" * 96)
    hit = 0
    cases = [
        ("오피스 판을 정사각으로(종횡비 1.0)", _plate(MOTIF_THICK, MOTIF_THICK), "오피스 워커", "창"),
        ("네온 3각을 4각으로",                 _plate(0.70, MOTIF_THICK),        "네온 낙서", "잎"),
        ("밀리터리 꺾음을 폄",                 _plate(1.40, MOTIF_THICK),        "밀리터리", "잎"),
        ("잉크 방울에서 꼭지를 없앰",          proto_sports(18),                  "컬러 잉크", "잎"),
        ("사이버 6각을 원으로",                proto_sports(20),                  "사이버 아포칼립스", "잎"),
    ]
    for label, pts, want, kind in cases:
        n, a, c, p = measure(pts)
        got = classify(n, a, c, p)
        if kind == "잎":
            caught = got != want
        else:
            sp = [x for x in SPEC if x[0] == want][0]
            caught = not (sp[3][0] <= a <= sp[3][1])      # 규격창 밖이어야 잡힌 것
        hit += caught
        print("  %s %-32s n=%2d 종횡비 %.2f 결손 %.3f 첨점 %d → 「%s」 (기대 팩 %s · %s 검사)"
              % ("OK " if caught else "✗  ", label, n, a, c, p, got, want, kind))
    print("\n  게이트가 잡은 것 %d/5. **5/5가 아니면 §1의 모든 통과를 폐기한다.**" % hit)
    if hit != 5: sys.exit(3)


if __name__ == "__main__":
    calibrate()
    if "--control" in sys.argv:
        control()
    else:
        gate()
        print("\n" + "=" * 96)
        print("판정 — 위반 %d건" % len(FAIL))
        for f in FAIL: print("   ·", f)
        sys.exit(1 if FAIL else 0)
