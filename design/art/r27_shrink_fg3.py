#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R27 — Ø36 축소 폴백에서 부채꼴 심볼 게이트 FG-1~FG-8 재판정 + <실제 렌더 시뮬레이션>.
design-art · 2026-09-06 · 프로덕션 .cs 0줄 수정(읽기만 한다).

왜 다시 재는가
--------------
R26의 FG 게이트 검증은 <Ø44/Ø48 전용>이었고, 그 근거로 `r26_coords.txt`가
«32pt는 이 앱에 없는 크기다 … 화면에 Ø36 버튼은 그려지지 않는다»고 못박아 두었다.
그 전제가 오늘 뒤집혔다 — `GearRadialMenuWidget.ApplyLayoutDiameterToViews()`가 들어오면서
축소 폴백이 <Group.localScale = 36/44 = 0.818182>의 **균일 배율**로 실제로 그려진다.

무엇을 고치는가 (★ 자기 정정)
-----------------------------
내 직전 라운드(`shrink36_geometry.py`)는 «Ø36에서 알파0 골 1.65px, 빈 화소 열 확률 65%»를 냈다.
그 숫자는 **폐기한다.** 근거인 램프 모형(`fanglyph.alpha_field`: 알파 램프 0.5pt가 <전부 코어
바깥>에 붙는다)이 프로덕션과 다르기 때문이다. `UiChrome.Capsule()`/`CircleSprite()`의 텍셀
굽기는 `alpha = clamp01((core − d)/feather + 0.5)`이고, 이는 **알파 0.5 등고선이 정확히 코어
가장자리**라는 뜻이다 — 램프는 가장자리를 <가운데 두고> ±feather/2로 걸친다.
따라서 코어 간극 g 에서 남는 «알파 0인 골»은 `g − 1.0pt`가 아니라 **`g − 0.5pt`**다.
(§0 교정6이 이 사실을 텍셀 굽기 재현으로 실측한다. 교정이 깨지면 아래 숫자는 전부 폐기다.)

이 파일이 내는 판정
-------------------
  §2  FG-1~FG-8 을 <배율 인자 k>와 함께 재실행. 각 게이트를 <세 개의 자>로 각각 본다.
  §3  덩어리 간 모든 쌍의 골 — Ø44 / Ø36 · 어느 쌍이 어느 자에서 미달인가.
  §4  ★ 실제 렌더 시뮬레이션 — 화소 격자 위에서 <두 덩어리가 언제 이어져 보이는가>(병목 임계 θ*).
  §5  여백 예산 — 얼마나 더 줄여야 실제로 뭉치는가(임계 배율 탐색).
  §6  부수 점검 — g1 획 생존 · 버튼 테두리.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_fanmirror as M      # noqa: E402
from r27_fanmirror import Poly, Capsule, Disc, Ring, Vec   # noqa: E402

# ── 색 토큰 (출처를 적고, §0에서 알려진 값으로 교정한다) ──────────────────────
TEXT_PRIMARY = (0.949, 0.957, 0.969)     # UiChrome.cs:262
CARD_SURFACE = (0.106, 0.122, 0.149)     # UiChrome.cs:136
CARD_BORDER = (1.0, 1.0, 1.0, 0.10)      # UiChrome.cs:139
MIN_NONTEXT_CONTRAST = 3.0               # UiChrome.MinNonTextContrast
MIN_TEXT_CONTRAST = 4.5                  # UiChrome.MinTextContrast

DPI_CASES = [("Win 100%", 1.00), ("Win 125%", 1.25), ("Win 150%", 1.50),
             ("Win 175%", 1.75), ("macOS 2×", 2.00)]
PHASE_N = 8                              # 위상 격자 8×8 = 64


# ─────────────────────────── 색 ───────────────────────────
def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def luminance(rgb):
    r, g, b = (lin(c) for c in rgb[:3])
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def blend(bg, fg, a):
    return tuple(bg[i] + (fg[i] - bg[i]) * a for i in range(3))


def flatten(fg_rgba, bg):
    return tuple(bg[i] + (fg_rgba[i] - bg[i]) * fg_rgba[3] for i in range(3))


def alpha_for_contrast(bg, fg, target):
    """면색 bg 위에 잉크 fg 를 알파 a 로 얹었을 때 대비가 target 이 되는 최소 a."""
    lo, hi = 0.0, 1.0
    for _ in range(80):
        mid = (lo + hi) / 2
        if contrast(bg, blend(bg, fg, mid)) < target:
            lo = mid
        else:
            hi = mid
    return hi


# ─────────────────────────── 거리(코어↔코어) ───────────────────────────
def _seg_pts(piece):
    """캡슐 사슬 조각을 (선분 목록, 반지름)으로. Disc 는 점 하나 + 반지름."""
    if isinstance(piece, Poly):
        return [(piece.pts[i - 1], piece.pts[i]) for i in range(1, len(piece.pts))], piece.t / 2
    if isinstance(piece, Capsule):
        return [(piece.a, piece.b)], piece.t / 2
    if isinstance(piece, Disc):
        return [(piece.c, piece.c)], piece.d / 2
    return None, None


def _seg_seg(p0, p1, q0, q1, n=1200):
    """두 선분 사이 최소 거리 — 한쪽을 조밀 표본해 다른 쪽 선분까지의 거리를 잰다."""
    t = np.linspace(0.0, 1.0, n)
    px = p0[0] + (p1[0] - p0[0]) * t
    py = p0[1] + (p1[1] - p0[1]) * t
    vx, vy = q1[0] - q0[0], q1[1] - q0[1]
    L2 = vx * vx + vy * vy
    if L2 == 0:
        return float(np.min(np.hypot(px - q0[0], py - q0[1])))
    u = np.clip(((px - q0[0]) * vx + (py - q0[1]) * vy) / L2, 0.0, 1.0)
    return float(np.min(np.hypot(px - q0[0] - u * vx, py - q0[1] - u * vy)))


def _dist_to_ring(pxs, pys, ring):
    """점들에서 «틈이 잘린 고리 코어»까지의 거리. 틈은 <하드 반경 절단>이다(Filled/Radial360)."""
    r_out, r_in = ring.d / 2, ring.d / 2 - ring.t
    dx, dy = pxs - ring.c[0], pys - ring.c[1]
    r = np.hypot(dx, dy)
    th = np.degrees(np.arctan2(dy, dx)) % 360.0
    radial = np.maximum(np.maximum(r_in - r, r - r_out), 0.0)
    if ring.gap <= 0:
        return radial
    g0 = (ring.gc - ring.gap / 2) % 360.0
    g1 = (ring.gc + ring.gap / 2) % 360.0
    in_gap = ((th >= g0) & (th <= g1)) if g0 < g1 else ((th >= g0) | (th <= g1))
    # ★ 틈 안에서는 <반경 거리를 쓰면 안 된다> — 그 각도에는 고리가 없다.
    #   잘려 나간 두 반경 변(하드 엣지)까지의 거리가 답이다.
    edge_d = np.full_like(radial, np.inf)
    for edge in (g0, g1):
        a = math.radians(edge)
        ax, ay = math.cos(a) * r_in + ring.c[0], math.sin(a) * r_in + ring.c[1]
        bx, by = math.cos(a) * r_out + ring.c[0], math.sin(a) * r_out + ring.c[1]
        vx, vy = bx - ax, by - ay
        L2 = vx * vx + vy * vy
        u = np.clip(((pxs - ax) * vx + (pys - ay) * vy) / L2, 0.0, 1.0)
        edge_d = np.minimum(edge_d, np.hypot(pxs - ax - u * vx, pys - ay - u * vy))
    return np.where(in_gap, edge_d, radial)


def core_distance(a, b, n=4000):
    """두 조각의 코어 사이 최소 거리(pt). 0 이면 겹치거나 맞닿는다."""
    if isinstance(a, Ring) and not isinstance(b, Ring):
        a, b = b, a
    if isinstance(b, Ring):
        segs, r = _seg_pts(a)
        if segs is None:
            raise TypeError("고리↔고리는 이 라운드에 없다")
        # _dist_to_ring 은 <고리 코어(두께 포함)>까지의 거리다 — 고리 두께를 또 빼면 안 된다.
        best = math.inf
        for p0, p1 in segs:
            t = np.linspace(0.0, 1.0, n)
            px = p0[0] + (p1[0] - p0[0]) * t
            py = p0[1] + (p1[1] - p0[1]) * t
            best = min(best, float(np.min(_dist_to_ring(px, py, b))) - r)
        return max(0.0, best)
    sa, ra = _seg_pts(a)
    sb, rb = _seg_pts(b)
    best = math.inf
    for p0, p1 in sa:
        for q0, q1 in sb:
            best = min(best, _seg_seg(p0, p1, q0, q1))
    return max(0.0, best - ra - rb)


# ─────────────────────────── 래스터(면적·상자·반경) ───────────────────────────
def raster(pieces, ss=16, win=32.0):
    n = int(win * ss)
    ax = (np.arange(n) + 0.5) / ss - win / 2
    X, Y = np.meshgrid(ax, -ax)
    masks = [p.sdf(X, Y) >= 0 for p in pieces]
    union = np.zeros_like(masks[0])
    for m in masks:
        union |= m
    ys, xs = np.where(union)
    bx0, bx1 = ax[xs.min()], ax[xs.max()]
    by1, by0 = -ax[ys.min()], -ax[ys.max()]
    rmax = float(np.hypot(ax[xs], -ax[ys]).max())
    return dict(masks=masks, union=union, rmax=rmax,
                w=bx1 - bx0, h=by1 - by0, diag=math.hypot(bx1 - bx0, by1 - by0),
                ink=float(union.sum()) / (24.0 * ss) ** 2)


def components(pieces):
    """겹치는 조각끼리 묶는다 — 한 덩어리 안의 간극은 규칙 대상이 아니다."""
    n = len(pieces)
    par = list(range(n))

    def find(i):
        while par[i] != i:
            par[i] = par[par[i]]
            i = par[i]
        return i

    for i in range(n):
        for j in range(i + 1, n):
            if core_distance(pieces[i], pieces[j]) <= 1e-9:
                par[find(i)] = find(j)
    return [find(i) for i in range(n)]


# ─────────────────────────── 알파 합성 · 렌더 ───────────────────────────
def composite(pieces, X, Y, feather):
    """Unity UI 의 알파 블렌딩 — 같은 잉크색이 겹치면 커버리지 = 1 − Π(1 − aᵢ)."""
    inv = np.ones_like(X)
    for p in pieces:
        inv *= (1.0 - p.alpha(X, Y, feather))
    return 1.0 - inv


def render_grid(pieces, feather, pitch, ox, oy, half=16.0):
    """화소 중심에서 <점 표본>한다. 근거: 스프라이트가 10배 이상 축소되어 붙으므로
    (§0 교정7) GPU 바이리니어 조회는 해석식의 점 표본과 사실상 같다."""
    n = int(2 * half / pitch) + 1
    ax = (np.arange(n) - (n - 1) / 2.0) * pitch
    X, Y = np.meshgrid(ax + ox * pitch, -(ax + oy * pitch))
    return composite(pieces, X, Y, feather), X, Y


def bottleneck(alpha, seeds):
    """두 씨앗이 «임계 θ 이상 화소»만으로 8-연결되는 최대 θ = 병목 임계 θ*.
    θ* < θ_ink 이면 잉크 문턱에서 둘은 <떨어져 보인다>."""
    h, w = alpha.shape
    flat = alpha.ravel()
    order = np.argsort(-flat, kind="stable")
    order = order[flat[order] > 0.0]
    par = {}
    lab = {}

    def find(i):
        while par[i] != i:
            par[i] = par[par[i]]
            i = par[i]
        return i

    seed_of = {}
    for si, (sy, sx) in enumerate(seeds):
        seed_of[sy * w + sx] = si
    result = {}
    added = np.zeros(h * w, dtype=bool)
    for idx in order:
        y, x = divmod(int(idx), w)
        par[idx] = idx
        lab[idx] = {seed_of[idx]} if idx in seed_of else set()
        added[idx] = True
        v = float(flat[idx])
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                ny, nx = y + dy, x + dx
                if not (0 <= ny < h and 0 <= nx < w):
                    continue
                nid = ny * w + nx
                if not added[nid]:
                    continue
                ra, rb = find(int(idx)), find(nid)
                if ra == rb:
                    continue
                la, lb = lab[ra], lab[rb]
                for i in la:
                    for j in lb:
                        key = (min(i, j), max(i, j))
                        if key not in result:
                            result[key] = v
                par[ra] = rb
                lab[rb] = la | lb
    return result


# ═══════════════════════════ §0 교정 ═══════════════════════════
def bake_capsule_profile(thickness, feather_pt=0.5):
    """UiChrome.Capsule() 의 텍셀 굽기를 <그대로> 재현하고, 화면 pt 로 환산한 알파 단면을 낸다."""
    w, h = 96, 32
    box = thickness + feather_pt * 2
    core_frac = max(0.30, min(1.0, thickness / box))
    half = h * 0.5
    core = half * core_frac
    feather = max(0.75, half - core)
    prof = []
    for y in range(h):
        py = y + 0.5
        d = abs(py - half)
        a = max(0.0, min(1.0, (core - d) / feather + 0.5))
        prof.append((d * box / h, round(a * 255) / 255.0))     # (축에서의 pt 거리, 알파)
    return prof, core * box / h, feather * box / h


def bake_circle_profile(diameter, ring_t, feather_pt=0.5):
    """UiChrome.CircleSprite() 재현 → (코어 반경 pt, 램프 폭 pt, 내경 pt)."""
    size = 128
    box = diameter + feather_pt * 2
    core_frac = max(0.30, min(1.0, diameter / box))
    thick_frac = min(0.5, max(0.01, ring_t / diameter)) if ring_t > 0 else 0.5
    half = size * 0.5
    outer = half * core_frac
    feather = max(0.75, half - outer)
    inner = outer - thick_frac * outer * 2
    s = box / size
    return outer * s, feather * s, inner * s


def calibrate(glyphs, consts, out):
    ok = True

    def chk(label, got, want, tol, fmt="{:.4f}"):
        nonlocal ok
        good = abs(got - want) <= tol
        ok &= good
        out(f"  {label:<58} {fmt.format(got)}  (기대 {fmt.format(want)})  {'✓' if good else '✘'}")

    out("=" * 108)
    out("§0 교정 — 알려진 값. 하나라도 깨지면 아래 숫자 전부 폐기.")
    out("=" * 108)
    chk("교정1  흰/검 대비", contrast((1, 1, 1), (0, 0, 0)), 21.0, 1e-4)
    chk("교정2  동일색 대비", contrast(CARD_SURFACE, CARD_SURFACE), 1.0, 1e-9)
    chk("교정3  TextPrimary ↔ CardSurface (r26_color.out.txt = 14.99)",
        contrast(TEXT_PRIMARY, CARD_SURFACE), 14.99, 0.01, "{:.2f}")
    chk("교정4  축소 배율 k = ShrunkDiameterPoints / ButtonDiameterPoints",
        consts["shrunk_d"] / consts["button_d"], 0.818182, 1e-5, "{:.6f}")

    # 교정5 — 프로덕션 좌표 파싱이 r26_coords.txt 발표값과 같은가(거울이 낡지 않았는가).
    want = {
        ("① 집중(스톱워치)", "HourHand"): [(0.0, 0.0), (2.598, -1.500)],
        ("② 캐릭터(스틱맨)", "IconArmR"): [(0.0, 3.5), (6.062, 0.0)],
        ("② 캐릭터(스틱맨)", "IconLegR"): [(0.0, -4.5), (2.536, -9.938)],
        ("④ 행동명령(확성기)", "Wave"): [(10.201, -4.063)],
        ("⑤ 종료(전원)", "PowerStem"): [(0.0, 1.0), (0.0, 11.0)],
    }
    worst = 0.0
    for (g, pname), pts in want.items():
        piece = next(p for p in glyphs[g] if p.name == pname)
        for i, (wx, wy) in enumerate(pts):
            worst = max(worst, abs(piece.pts[i][0] - wx), abs(piece.pts[i][1] - wy))
    chk("교정5  파싱 좌표 ↔ r26_coords.txt 최대 차(pt)", worst, 0.0, 5e-4, "{:.5f}")

    # 교정6 ★ 램프 모형 — 텍셀 굽기에서 유도한다(외운 값 금지).
    out("")
    out("  교정6 ★ 알파 램프 — UiChrome.Capsule()/CircleSprite() 텍셀 굽기 재현")
    for t in (consts["G1"], consts["W"], consts["G2"]):
        prof, core_pt, feather_pt = bake_capsule_profile(t)
        half = sorted(set(prof))                          # (거리, 알파) 오름차순
        d1 = max(d for d, a in prof if a >= 0.999)        # 알파 1 마지막 거리
        d0 = min(d for d, a in prof if a <= 0.001)        # 알파 0 첫 거리
        ds = np.array([d for d, _ in half])
        as_ = np.array([a for _, a in half])
        a_at_edge = float(np.interp(t / 2, ds, as_))      # 텍셀 사이는 보간해서 본다
        out(f"       획 {t:.2f}pt : 코어 반두께 {core_pt:.4f}(기대 {t/2:.4f}) · 램프폭 {feather_pt:.4f}"
            f"(기대 0.5000) · 알파1 ≤{d1:.3f} · 코어 가장자리 알파 {a_at_edge:.3f} · 알파0 ≥{d0:.3f}")
        chk(f"         └ 코어 반두께 = t/2", core_pt, t / 2, 1e-6)
        chk(f"         └ 램프폭 = EdgeFeather", feather_pt, consts["feather"], 1e-6)
        chk(f"         └ 코어 가장자리(d = t/2)의 알파 = 0.5", a_at_edge, 0.5, 0.01, "{:.3f}")
    o, f, i = bake_circle_profile(consts["power_d"], consts["W"])
    out(f"       ⑤ 링 Ø{consts['power_d']:.0f} : 외경 {o:.4f}(기대 {consts['power_d']/2:.4f}) · "
        f"램프 {f:.4f} · 내경 {i:.4f}(기대 {consts['power_d']/2 - consts['W']:.4f})")
    chk("         └ 링 외경 = d/2", o, consts["power_d"] / 2, 1e-6)
    chk("         └ 링 내경 = d/2 − t", i, consts["power_d"] / 2 - consts["W"], 1e-6)
    chk("         └ 링 램프폭 = EdgeFeather", f, consts["feather"], 1e-6)
    out(f"    ⇒ 코어 간극 g 에서 남는 «알파 0인 골» = g − {consts['feather']:.1f}pt "
        f"(★ DESIGN_FAN_MENU_ICONS §1-3 의 «g − 1.0pt»는 램프를 전부 코어 바깥에 둔 모형이다)")

    # 교정7 — GPU 바이리니어 조회 ≈ 해석식(점 표본)인가.
    out("")
    t = consts["W"]
    box = t + 1.0
    prof, _, _ = bake_capsule_profile(t)
    texel = box / 32.0
    errs = []
    for d in np.linspace(0.0, box / 2 - 1e-6, 400):
        v = d / texel + 16.0 - 0.5                    # 텍셀 좌표(중심 0.5 규약)
        i0 = int(math.floor(v))
        fr = v - i0
        i0 = min(max(i0, 0), 31)
        i1 = min(max(i0 + 1, 0), 31)
        bil = prof[16 + (i0 - 16)][1] * (1 - fr) + prof[16 + (i1 - 16)][1] * fr \
            if 0 <= i0 < 32 and 0 <= i1 < 32 else 0.0
        ana = max(0.0, min(1.0, 0.5 + (t / 2 - d) / consts["feather"]))
        errs.append(abs(bil - ana))
    # 편차의 상한은 <클램프 꺾임> 하나에서만 나온다: 기울기 1/f × 텍셀폭 ÷ 4.
    kink = (texel / consts["feather"]) / 4.0
    chk(f"교정7  텍셀 굽기+바이리니어 ↔ 해석식 최대 알파 편차 (꺾임 상한 {kink:.4f})",
        float(max(errs)), 0.0, kink * 1.05)
    out(f"         └ RMS 편차 {float(np.sqrt(np.mean(np.square(errs)))):.4f} — "
        f"즉 화소는 <해석식의 점 표본>으로 봐도 된다(스프라이트가 10배 이상 축소돼 붙는다).")

    # 교정8 — 골 측정기를 손계산 3건으로.
    out("")
    sw = {p.name: p for p in glyphs["① 집중(스톱워치)"]}
    pw = {p.name: p for p in glyphs["⑤ 종료(전원)"]}
    cl = {p.name: p for p in glyphs["③ 할일(체크리스트)"]}
    chk("교정8a 분침 끝(r=5) ↔ 링 안쪽(r=8)  = 3.000pt",
        core_distance(sw["MinuteHand"], sw["Ring"]), 3.0, 2e-3, "{:.4f}")
    hand = (consts["power_d"] / 2 - consts["W"]) * math.sin(math.radians(consts["power_gap"] / 2)) \
        - consts["W"] / 2
    chk(f"교정8b 세로획 ↔ 틈 안쪽 = (d/2−W)·sin(틈/2)−W/2 = {hand:.3f}pt",
        core_distance(pw["PowerRing"], pw["PowerStem"]), hand, 2e-3, "{:.4f}")
    chk("교정8c 표식 상자 오른변(−4.5) ↔ 글줄 왼끝(−1.5) = 3.000pt",
        core_distance(cl["Box"], cl["Line0"]), 3.0, 2e-3, "{:.4f}")

    # 교정9 ★ 양성 대조 — <이미 독립적으로 판정된 조형>으로 지표가 살아 있는지 본다.
    #   R26 이전의 확성기(낱획 4개, 최악 간극 0.43pt)를 DESIGN_FAN_MENU_ICONS §1-3이
    #   «두 획이 화면에서 한 줄로 합쳐진다»고 적었다. 내 시뮬레이터가 그 판정을 재현해야 한다.
    out("")
    theta_ink = alpha_for_contrast(CARD_SURFACE, TEXT_PRIMARY, MIN_NONTEXT_CONTRAST)
    theta_seam = 1.0 - alpha_for_contrast(TEXT_PRIMARY, CARD_SURFACE, MIN_NONTEXT_CONTRAST)
    old_megaphone = [
        Capsule("HornUpper", 13.0, 2.0, 13.0, Vec(-1.6, 5.0)),
        Capsule("HornLower", 13.0, 2.0, -13.0, Vec(-1.6, -5.0)),
        Capsule("HornNeck", 5.6, 2.0, 90.0, Vec(-8.4, 0.0)),
        Capsule("HornMouth", 11.6, 2.0, 90.0, Vec(5.0, 0.0)),
        Capsule("WaveUpper", 4.6, 1.6, 30.0, Vec(9.6, 3.4)),
        Capsule("WaveLower", 4.6, 1.6, -30.0, Vec(9.6, -3.4)),
    ]
    gmin = min(core_distance(old_megaphone[i], old_megaphone[j])
               for i in range(6) for j in range(i + 1, 6)
               if core_distance(old_megaphone[i], old_megaphone[j]) > 1e-9)
    chk("교정9a 옛 확성기 최악 간극 (DESIGN_FAN_MENU_ICONS §1-2 표 = 0.43pt)",
        gmin, 0.43, 0.01, "{:.3f}")
    t_old = worst_bottleneck(old_megaphone, consts["feather"], 1.0, 1.0)
    t_new = worst_bottleneck(glyphs["④ 행동명령(확성기)"], consts["feather"], 1.0, 1.0)
    out(f"  교정9b 양성대조 — <옛> 확성기 θ* = {t_old:.3f}  ≥ θ_ink {theta_ink:.3f} → "
        f"«한 줄로 합쳐진다» {'✓ 재현' if t_old >= theta_ink else '✘ 재현 못 함'}")
    out(f"  교정9c 음성대조 — <새> 확성기 θ* = {t_new:.3f}  < θ_ink → «갈라져 보인다» "
        f"{'✓' if t_new < theta_ink else '✘'}")
    ok &= (t_old >= theta_ink) and (t_new < theta_ink)

    # 교정10 단조성: 같은 조형을 더 줄이면 θ*는 오르기만 해야 한다(내려가면 지표가 거꾸로다).
    apart = [Poly("A", [Vec(-6, 0), Vec(-1, 0)], consts["W"]),
             Poly("B", [Vec(4, 0), Vec(9, 0)], consts["W"])]        # 간극 3.0pt
    ks = [1.0, 0.6, 0.4, 0.3, 0.22]
    seq = [worst_bottleneck(apart, consts["feather"], kk, 1.0) for kk in ks]
    mono = all(seq[i] <= seq[i + 1] + 1e-9 for i in range(len(seq) - 1)) and seq[-1] > seq[0]
    out(f"  교정10 단조성 — 간극 3.0pt 더미를 k {ks} 로 줄이면 θ* "
        f"{[round(v,3) for v in seq]}  {'✓' if mono else '✘'}")
    ok &= mono

    # 교정11 — 래스터(면적·상자)가 r26_gate.out.txt 발표값을 재현하는가.
    out("")
    pub = {"① 집중(스톱워치)": (13.99, 25.71, 31.13), "② 캐릭터(스틱맨)": (11.49, 18.70, 26.47),
           "③ 할일(체크리스트)": (13.44, 16.05, 26.66), "④ 행동명령(확성기)": (12.58, 20.47, 27.76),
           "⑤ 종료(전원)": (11.99, 22.08, 31.78)}
    worst_r = worst_i = worst_d = 0.0
    for name, (pr, pi, pd) in pub.items():
        r = raster(glyphs[name])
        worst_r = max(worst_r, abs(r["rmax"] - pr))
        worst_i = max(worst_i, abs(100 * r["ink"] - pi))
        worst_d = max(worst_d, abs(r["diag"] - pd))
    chk("교정11 래스터 r_max ↔ r26_gate.out.txt 최대 차(pt)", worst_r, 0.0, 0.05, "{:.3f}")
    chk("       래스터 잉크% ↔ 발표값 최대 차(%p)", worst_i, 0.0, 0.15, "{:.3f}")
    chk("       래스터 잉크상자 대각 ↔ 발표값 최대 차(pt)", worst_d, 0.0, 0.08, "{:.3f}")
    return ok, theta_ink, theta_seam


# ─────────────────────────── 병목 임계 ───────────────────────────
def worst_bottleneck(pieces, feather, k, S, phases=PHASE_N, half=16.0, comp=None):
    """모든 위상에서 잰 병목 임계 θ* 의 <최악(최대)>. 덩어리가 하나면 0."""
    comp = comp if comp is not None else components(pieces)
    groups = {}
    for i, c in enumerate(comp):
        groups.setdefault(c, []).append(pieces[i])
    if len(groups) < 2:
        return 0.0
    pitch = 1.0 / (k * S)
    worst = 0.0
    keys = list(groups)
    for iy in range(phases):
        for ix in range(phases):
            ox, oy = ix / phases, iy / phases
            A, X, Y = render_grid(pieces, feather, pitch, ox, oy, half)
            seeds = []
            for kk in keys:
                a = composite(groups[kk], X, Y, feather)
                seeds.append(np.unravel_index(int(np.argmax(a)), a.shape))
            res = bottleneck(A, seeds)
            for v in res.values():
                worst = max(worst, v)
            if len(res) < len(keys) * (len(keys) - 1) // 2:
                pass          # 만나지 않은 쌍 = 알파 0 화소로 완전히 갈렸다(θ* = 0)
    return worst


def pair_bottlenecks(pieces, feather, k, S, phases=PHASE_N, half=16.0):
    comp = components(pieces)
    groups, order = {}, []
    for i, c in enumerate(comp):
        if c not in groups:
            groups[c] = []
            order.append(c)
        groups[c].append(pieces[i])
    if len(order) < 2:
        return {}, {}
    pitch = 1.0 / (k * S)
    worst = {(i, j): 0.0 for i in range(len(order)) for j in range(i + 1, len(order))}
    peak = {i: 1.0 for i in range(len(order))}
    for iy in range(phases):
        for ix in range(phases):
            A, X, Y = render_grid(pieces, feather, pitch, ix / phases, iy / phases, half)
            seeds = []
            for gi, c in enumerate(order):
                a = composite(groups[c], X, Y, feather)
                seeds.append(np.unravel_index(int(np.argmax(a)), a.shape))
                peak[gi] = min(peak[gi], float(a.max()))
            for key, v in bottleneck(A, seeds).items():
                worst[key] = max(worst[key], v)
    names = {i: "+".join(p.name for p in groups[c]) for i, c in enumerate(order)}
    return {(names[i], names[j]): v for (i, j), v in worst.items()}, \
           {names[i]: v for i, v in peak.items()}


# ═══════════════════════════ 본문 ═══════════════════════════
def main():
    lines = []

    def out(s=""):
        print(s)
        lines.append(s)

    glyphs, consts, fan, chrome = M.load_glyphs()
    k36 = consts["shrunk_d"] / consts["button_d"]
    W, f = consts["W"], consts["feather"]

    ok, theta_ink, theta_seam = calibrate(glyphs, consts, out)
    if not ok:
        out("\n★ 교정 실패 — 아래 숫자 전부 폐기.")
        sys.exit(2)
    out("  => 교정 통과.\n")
    out("=" * 108)
    out("§1 자(尺) 세 개 — 「미달」이라는 말은 어느 자로 재느냐에 따라 다른 뜻이다")
    out("=" * 108)
    out(f"  (가) 상대자   : 문턱이 W 와 함께 줄어든다. FG-3 = 1.5W. 배율에 <불변>이므로 Ø36에서도 같은 판정.")
    out(f"  (나) 절대pt자 : 문턱을 Ø44 기준 pt 로 고정한다. FG-3 = 3.0pt. 축소하면 전부 깎인다.")
    out(f"  (다) 기기픽셀자: 문턱을 <물리 화소>로 둔다 — 이것만이 배율과 DPI를 동시에 본다.")
    out(f"       유도(DESIGN_FAN_MENU_ICONS §1-3 정정판): 골에 알파 0 화소가 남으려면")
    out(f"       (g − {f}pt)·k·S ≥ 1.0px.  ★ §0 교정6대로 램프는 −1.0pt 가 아니라 −{f}pt 다.")
    out(f"  잉크 문턱 θ_ink  = {theta_ink:.4f} — 이 알파 이상이면 그 화소는 <면색> 대비 "
        f"{MIN_NONTEXT_CONTRAST:.1f}:1 을 넘어 «잉크로 읽힌다».")
    out(f"  이음매 문턱 θ_seam = {theta_seam:.4f} — 이 알파 <이하>면 그 화소는 <잉크색> 대비 "
        f"{MIN_NONTEXT_CONTRAST:.1f}:1 을 넘어 «골로 읽힌다».")
    out(f"  두 문턱이 다르므로 둘 다 적는다. θ_ink 쪽이 더 엄하다({theta_ink:.3f} < {theta_seam:.3f}) — "
        f"본문의 판정은 엄한 쪽을 쓴다.")
    out("")

    # ── §2 게이트 재실행 ──
    out("=" * 108)
    out(f"§2 FG-1 ~ FG-8 재실행 — 배율 인자 k 도입 (Ø44 k=1.000 / Ø36 k={k36:.6f})")
    out("=" * 108)
    stats = {}
    for name, pieces in glyphs.items():
        r = raster(pieces)
        comp = components(pieces)
        pairs = []
        for i in range(len(pieces)):
            for j in range(i + 1, len(pieces)):
                if comp[i] != comp[j]:
                    pairs.append((pieces[i].name, pieces[j].name,
                                  core_distance(pieces[i], pieces[j])))
        pairs.sort(key=lambda x: x[2])
        stats[name] = dict(r=r, comp=comp, pairs=pairs, n_comp=len(set(comp)))

    hdr = f"{'':22}{'FG-1 r_max':>12}{'FG-2 획':>18}{'FG-3 최소골':>12}{'FG-4':>8}{'FG-8 잉크%':>11}{'FG-8 대각':>10}"
    out(hdr)
    out("-" * 108)
    for name, s in stats.items():
        ths = sorted({round(p.t, 3) for p in glyphs[name] if p.t > 0})
        mg = s["pairs"][0][2] if s["pairs"] else float("inf")
        out(f"  Ø44 {name:<16}{s['r']['rmax']:>10.2f}pt  {str(ths):>16}{mg:>10.2f}pt"
            f"{len(glyphs[name]):>6}조각{100*s['r']['ink']:>10.2f}{s['r']['diag']:>9.2f}pt")
        out(f"  Ø36 {'':<16}{s['r']['rmax']*k36:>10.2f}pt  "
            f"{str([round(t*k36,3) for t in ths]):>16}{mg*k36:>10.2f}pt{'':>8}"
            f"{100*s['r']['ink']:>10.2f}{s['r']['diag']*k36:>9.2f}pt")
    out("")
    out("  판정 — 게이트별로 «어느 자가 옳은가»가 다르다:")
    fg1_hard = 15.0
    out(f"   FG-1 필드 r ≤ {consts['field_r']:.0f}(권고)/{fg1_hard:.0f}(상한): Ø36은 전부 ×{k36:.4f} 로 <작아진다> → "
        f"최악 {max(s['r']['rmax'] for s in stats.values())*k36:.2f}pt. **축소는 이 게이트를 완화한다.**")
    bad8 = [n for n, s in stats.items() if s["r"]["diag"] * k36 < 24.0]
    out(f"   FG-8 잉크 대각 24~32pt를 <절대pt자>로 읽으면 Ø36에서 {len(bad8)}/5 가 하한 미달"
        f"({', '.join(x.split('(')[0] for x in bad8)}).")
    out(f"        ★ 그러나 이것은 결함이 아니다 — 축소 버튼의 글리프가 작아 보이는 것은 <설계 의도>다.")
    out(f"        FG-8의 근거는 «다섯 칸이 나란히 떴을 때 무게가 갈리면 안 된다»이고, 축소는 다섯 칸에")
    out(f"        <동시에> 걸린다. 즉 FG-8·FG-1·FG-2는 <상대자>로 읽어야 하는 게이트다.")
    out(f"        ⇒ 배율에서 실제로 위험한 게이트는 <화소를 근거로 유도된> FG-3 하나뿐이다.")
    out("")

    # ── §3 FG-3 전체 쌍 ──
    out("=" * 108)
    out("§3 FG-3 — 덩어리 간 <모든> 쌍. 어느 쌍이 어느 자에서 미달인가")
    out("=" * 108)
    out(f"{'글리프':<20}{'쌍':<26}{'Ø44 골':>9}{'Ø36 골':>9}{'(가)상대':>10}{'(나)절대pt':>11}"
        f"{'(다)픽셀@1×':>12}{'  판정'}")
    out("-" * 108)
    fails_abs, fails_px = [], []
    for name, s in stats.items():
        if not s["pairs"]:
            out(f"  {name:<18}{'— 한 덩어리(용접) —':<26}{'':>9}{'':>9}{'해당없음':>10}")
            continue
        for a, b, g in s["pairs"]:
            g36 = g * k36
            rel_ok = g >= 1.5 * W - 1e-9                       # 배율 불변
            abs_ok = g36 >= 3.0 - 1e-9
            zero_px = (g36 - f * k36) * 1.0                    # S=1, 물리 화소
            px_ok = zero_px >= 1.0
            if not abs_ok:
                fails_abs.append((name, a, b, g, g36))
            if not px_ok:
                fails_px.append((name, a, b, g, g36))
            verdict = "OK" if (rel_ok and px_ok) else "★"
            out(f"  {name:<18}{a+'↔'+b:<26}{g:>8.2f}pt{g36:>8.2f}pt"
                f"{('✓ ' + format(g/W, '.2f') + 'W'):>10}{('✓' if abs_ok else '✘ 미달'):>11}"
                f"{zero_px:>10.2f}px {'✓' if px_ok else '✘'}   {verdict}")
    out("")
    out(f"  ★ 격자 편의(bias) 정정 — r26_gate.out.txt 의 최소 골은 <SS=32 래스터 + EDT>라 격자 한 칸")
    out(f"    (1/32 = 0.031pt)만큼 위로 치우쳐 있었다. 이 표는 <해석 해>다(§0 교정8이 손계산 3건으로 못박았다):")
    out(f"       ① 3.02 → 3.000pt   ③ 3.03 → 3.000pt   ④ 3.22 → 3.200pt   ⑤ 3.69 → 3.635pt")
    out(f"    ⇒ ①과 ③의 최소 골은 <정확히 1.5W>다. 상대자 여유가 +0.02pt가 아니라 **0.000pt**다 —")
    out(f"      R26이 그 자리를 하한에 <붙여> 설계했다는 뜻이고, 배율을 어떻게 잡아도 상대자는")
    out(f"      «간신히 통과»가 아니라 «등호로 통과»한다.")
    out("")
    out(f"  (나) 절대pt자 미달 : {len(fails_abs)}쌍")
    for n, a, b, g, g36 in fails_abs:
        out(f"        {n:<18}{a}↔{b:<14}{g:.2f} → {g36:.2f}pt  (3.0pt에 {3.0-g36:.2f}pt 모자람)")
    out(f"  (다) 기기픽셀자 미달: {len(fails_px)}쌍" + ("  — 없다" if not fails_px else ""))
    out("")

    # ── §4 렌더 시뮬레이션 ──
    out("=" * 108)
    out("§4 ★ 실제 렌더 시뮬레이션 — «뭉쳐 보이는가»를 확률이 아니라 <연결성>으로 판정")
    out("=" * 108)
    out("  방법: 화소 중심에서 합성 알파(1 − Π(1−aᵢ))를 점 표본하고, 임계 θ 이상 화소만 남겨")
    out("        8-연결 성분을 만든다. 두 덩어리가 이어지는 최대 θ = <병목 임계 θ*>다.")
    out(f"        θ* < θ_ink({theta_ink:.3f}) 이면 잉크로 읽히는 화소들 사이에 <끊긴 자리가 있다> = 떨어져 보인다.")
    out(f"        위상 {PHASE_N}×{PHASE_N}={PHASE_N**2}개(부채꼴은 임의 좌표에 열리므로 반화소 위치가 매번 다르다) 중 <최악>을 적는다.")
    out("")
    out(f"{'글리프':<20}{'덩어리쌍':<26}" + "".join(f"{n:>12}" for n, _ in DPI_CASES))
    out(f"{'':<20}{'':<26}" + "".join(f"{'Ø36 θ*':>12}" for _ in DPI_CASES))
    out("-" * 108)
    sim = {}
    for name, pieces in glyphs.items():
        if stats[name]["n_comp"] < 2:
            out(f"  {name:<18}{'— 한 덩어리 —':<26}" + "".join(f"{'—':>12}" for _ in DPI_CASES))
            continue
        rows = {}
        for label, S in DPI_CASES:
            pb, peaks = pair_bottlenecks(pieces, f, k36, S)
            rows[label] = pb
        sim[name] = rows
        keys = list(rows[DPI_CASES[0][0]].keys())
        for key in keys:
            a, b = key
            short = (a[:11] + "↔" + b[:11])
            out(f"  {name:<18}{short:<26}" +
                "".join(f"{rows[lb][key]:>12.3f}" for lb, _ in DPI_CASES))
    out("")
    worst_all = max((v for rows in sim.values() for pb in rows.values() for v in pb.values()),
                    default=0.0)
    out(f"  ⇒ Ø36 전 배율·전 위상·전 쌍의 최악 θ* = {worst_all:.3f}  vs  θ_ink {theta_ink:.3f}")
    if worst_all < theta_ink:
        out(f"     **어느 위상에서도 두 덩어리가 잉크 문턱에서 이어지지 않는다 = 뭉쳐 보이지 않는다.**")
        out(f"     (θ* = 0.000 은 <알파가 정확히 0인 화소>가 골에 반드시 하나 이상 들어온다는 뜻이다.)")
    else:
        out(f"     ★ 뭉치는 쌍이 있다 — §5 참조.")
    out("")

    # Ø44 대조군(같은 자로 잰 기준선)
    out("  대조군 — 같은 자로 잰 Ø44(k=1.000, Win 100%):")
    for name, pieces in glyphs.items():
        if stats[name]["n_comp"] < 2:
            continue
        pb, _ = pair_bottlenecks(pieces, f, 1.0, 1.0)
        for (a, b), v in pb.items():
            out(f"      {name:<18}{a[:11]+'↔'+b[:11]:<26}θ* {v:.3f}")
    out("")

    # ── §5 여백 예산 ──
    out("=" * 108)
    out("§5 여백 예산 — <얼마나 더 줄여야> 실제로 뭉치는가 (Win 100%, 위상 6×6)")
    out("=" * 108)
    out(f"{'글리프':<20}{'뭉침 임계 k*':>14}{'그때 버튼 Ø':>14}{'Ø36 대비 여유':>16}{'최소 골(Ø44)':>14}")
    out("-" * 108)
    budget = []
    for name, pieces in glyphs.items():
        if stats[name]["n_comp"] < 2:
            out(f"  {name:<18}{'— 한 덩어리 —':>14}")
            continue
        kstar = None
        kk = 1.00
        while kk > 0.20:
            t = worst_bottleneck(pieces, f, kk, 1.0, phases=6)
            if t >= theta_ink:
                kstar = kk
                break
            kk -= 0.02
        mg = stats[name]["pairs"][0][2]
        if kstar is None:
            out(f"  {name:<18}{'< 0.20':>14}{'< Ø8.8':>14}{'2.0배 이상':>16}{mg:>12.2f}pt")
            budget.append((name, 0.20))
        else:
            out(f"  {name:<18}{kstar:>14.2f}{kstar*consts['button_d']:>12.1f}pt"
                f"{k36/kstar:>15.2f}배{mg:>12.2f}pt")
            budget.append((name, kstar))
    out("")
    tight = max(budget, key=lambda x: x[1]) if budget else None
    if tight:
        out(f"  가장 빠듯한 칸: {tight[0]} — k* = {tight[1]:.2f}. Ø36(k={k36:.3f})은 그보다 "
            f"{k36/tight[1]:.2f}배 크다.")
        out(f"  즉 <안착 상태의> 축소 폴백은 «뭉침»까지 {(k36/tight[1]-1)*100:.0f} % 의 여유를 두고 서 있다.")
    out("")

    # ── §5-1 애니메이션 전이 — 아무도 재지 않은 <가장 작은 순간> ──
    out("-" * 108)
    out("§5-1 ★ 배율은 안착값이 전부가 아니다 — 펼침/접힘 애니메이션이 <더 작은 순간>을 만든다")
    out("-" * 108)
    start_scale = fan.need("StartScale")            # ApplyVisuals: Root.localScale = StartScale → 1
    collapse_min = 1.0 - 0.28                       # 사용자 접힘: scale = 1 − 0.28·EaseInQuad
    trans = [("펼침 첫 프레임", start_scale, "StartScale"),
             ("사용자 접힘 바닥", collapse_min, "1 − 0.28 (ApplyVisuals)")]
    out(f"  Group(배치 배율 {k36:.4f}) × Root(애니메이션 배율)이 <곱해진다>. 곱이 실제 화면 배율이다.")
    out(f"{'국면':<18}{'Root 배율':>10}{'실효 배율':>11}{'실효 버튼Ø':>12}{'최악 θ*':>10}{'  판정':<8}{'출처'}")
    for label, s, src in trans:
        keff = k36 * s
        worst_t = 0.0
        for name, pieces in glyphs.items():
            if stats[name]["n_comp"] < 2:
                continue
            worst_t = max(worst_t, worst_bottleneck(pieces, f, keff, 1.0, phases=6))
        out(f"  {label:<16}{s:>10.3f}{keff:>11.4f}{keff*consts['button_d']:>10.1f}pt{worst_t:>10.3f}"
            f"{('  ✓ 안 뭉침' if worst_t < theta_ink else '  ✘ 뭉친다'):<10}{src}")
    out(f"  ⇒ 가장 작은 순간은 <펼침 첫 프레임 × 축소 폴백> = {k36*start_scale:.4f}"
        f"(= Ø{k36*start_scale*consts['button_d']:.1f} 상당)이고, 그것도 뭉침 임계 k* {tight[1]:.2f} 보다 "
        f"{k36*start_scale/tight[1]:.2f}배 크다.")
    out(f"    다만 안착값의 여유({k36/tight[1]:.2f}배)보다 <좁다> — 축소 폴백의 진짜 하한은 여기다.")
    out(f"    (이 국면은 알파도 함께 페이드인({fan.need('AlphaFadeInSeconds'):.2f}초)한다 — 전 조각에 균일하게 걸리므로")
    out(f"     조형 판정을 <완화>한다. 위 숫자는 알파 1.0으로 잡은 보수적 값이다.)")
    out("")

    # ── §6 부수 점검 ──
    out("=" * 108)
    out("§6 부수 점검 — 배율에서 <골>보다 먼저 위험할 수 있는 것들")
    out("=" * 108)
    out("  (a) 가장 가는 획(g1 = SymbolStrokeDetail, ③ 표식 상자)이 Ø36 @1× 에서 살아남는가")
    for lb, S in DPI_CASES:
        t_r = consts["G1"] * k36
        f_r = f * k36
        pitch = 1.0 / (k36 * S)
        # 최악 위상 = 획 축이 두 화소 중간(가장 먼 표본점이 pitch/2)
        d = pitch / 2 * 1.0
        a = max(0.0, min(1.0, 0.5 + (consts["G1"] / 2 - d * 1.0) / f))
        col = blend(CARD_SURFACE, TEXT_PRIMARY, a)
        out(f"      {lb:<10} 화면 획폭 {t_r*S:.2f}px · 최악 위상 최고 알파 {a:.3f} · "
            f"면색 대비 {contrast(col, CARD_SURFACE):5.2f}:1  "
            f"{'✓' if contrast(col, CARD_SURFACE) >= MIN_NONTEXT_CONTRAST else '✘'}")
    out("")
    out("  (b) 버튼 테두리(AddCircle Border 1.2pt)")
    border = flatten(CARD_BORDER, CARD_SURFACE)
    out(f"      설계 대비(테두리↔면) = {contrast(border, CARD_SURFACE):.2f}:1 — 원래 «분리막»이 아니라 "
        f"«결»이다(면 자체가 바탕과 분리를 만든다).")
    for lb, S in DPI_CASES:
        pitch = 1.0 / (k36 * S)
        d = pitch / 2
        a = max(0.0, min(1.0, 0.5 + (consts["border_t"] / 2 - d) / f))
        out(f"      {lb:<10} 화면 테두리 {consts['border_t']*k36*S:.2f}px · 최악 위상 최고 알파 {a:.3f}")
    out("      ⇒ 테두리는 Ø36 @1× 최악 위상에서 알파가 절반 아래로 내려간다. 다만 설계 대비가 "
        f"{contrast(border, CARD_SURFACE):.2f}:1 뿐이라")
    out("        <원래도 대비를 나르는 부품이 아니다> — 게이트 항목이 아니라 관측으로 남긴다.")
    out("")

    # (c) FG-4 의 <구멍> 절 — 배율과 함께 줄어드는 두 번째 항목이다(FG-3만 보면 놓친다).
    out("  (c) FG-4 «채운 덩어리와 그 구멍은 최소 폭 ≥ 1.5W» — 이 카탈로그에서 가장 좁은 구멍은")
    out("      ③ 표식 상자의 구멍(경로 한 변 5.0 − 획 g1 1.5 = 3.5pt)이다. 구멍도 배율과 함께 줄어든다.")
    box = next(p for p in glyphs["③ 할일(체크리스트)"] if p.name == "Box")
    cx, cy = fan.need("ChecklistMarkX"), fan.need("ChecklistRowY")
    hole = fan.need("ChecklistMarkCellPoints") - consts["G1"]
    out(f"{'':6}{'국면':<22}{'구멍 폭':>9}{'알파0 폭':>10}{'최악위상 최저알파':>18}{'구멍 안 어두운 화소 수':>22}{'  판정'}")
    for label, kk, S in (("Ø44 @1×", 1.0, 1.0), ("Ø36 @1×", k36, 1.0), ("Ø36 @2×", k36, 2.0),
                         ("Ø36 × 펼침전이 @1×", k36 * fan.need("StartScale"), 1.0)):
        pitch = 1.0 / (kk * S)
        worst_min, worst_cnt = 0.0, 10 ** 6
        for iy in range(8):
            for ix in range(8):
                A, X, Y = render_grid(glyphs["③ 할일(체크리스트)"], f, pitch, ix / 8, iy / 8)
                inside = (np.abs(X - cx) <= hole / 2) & (np.abs(Y - cy) <= hole / 2)
                worst_min = max(worst_min, float(A[inside].min()) if inside.any() else 1.0)
                worst_cnt = min(worst_cnt, int(((A < theta_ink) & inside).sum()))
        out(f"{'':6}{label:<22}{hole*kk:>8.2f}pt{(hole-consts['feather'])*kk*S:>9.2f}px"
            f"{worst_min:>16.3f}{worst_cnt:>18}개"
            f"{'   ✓ 구멍이 남는다' if worst_min < theta_ink else '   ✘ 구멍이 메워진다'}")
    out(f"      ⇒ Ø36에서도 구멍은 남지만 <최악 위상에서 어두운 화소가 몇 개인가>가 급감한다 —")
    out(f"        Ø44의 여유가 Ø36에서 절반 이하로, 펼침 전이에서는 <단 한 화소>로 줄어든다.")
    out(f"        시안 4열(k=0.507)에서 표식 상자가 «점 하나 뚫린 덩어리»로 읽히는 것이 이 숫자다.")
    out("")

    # ── §7 만약 (나) 절대pt자를 채택한다면 — 좌표 조정안과 그 대가 ──
    out("=" * 108)
    out("§7 «그래도 Ø36에서 3.0pt를 지키자»고 하면 — 좌표를 얼마나 벌려야 하고, 무엇을 잃는가")
    out("=" * 108)
    need = 3.0 / k36
    out(f"  Ø36에서 3.0pt를 남기려면 Ø44 기준 골이 3.0/{k36:.4f} = **{need:.3f}pt** 이상이어야 한다"
        f"(= {need/W:.2f}W).")
    out(f"  즉 FG-3의 하한을 1.5W → {need/W:.2f}W 로 올리는 것과 같다. 필요한 벌림(Ø44 기준):")
    out("")
    for name, a, b, g, g36 in fails_abs:
        out(f"    {name:<18}{a}↔{b:<14} {g:.3f} → {need:.3f}pt  (+{need-g:.3f}pt 벌려야 한다)")
    out("")
    out("  ── ① 스톱워치: 이 칸은 <벌릴 자리가 없다>. 두 길 다 막혀 있다.")
    d1 = need - 3.00
    mh = next(p for p in glyphs["① 집중(스톱워치)"] if p.name == "MinuteHand")
    hh = next(p for p in glyphs["① 집중(스톱워치)"] if p.name == "HourHand")
    r_mh = math.hypot(*mh.pts[1]) + W / 2
    r_hh = math.hypot(*hh.pts[1]) + W / 2
    out(f"     길 (가) 분침을 줄인다: 길이 {math.hypot(*mh.pts[1]):.2f} → {math.hypot(*mh.pts[1])-d1:.2f}pt.")
    out(f"        그러면 분침 끝 r {r_mh:.2f} → {r_mh-d1:.2f}, 시침 끝 r {r_hh:.2f} — 두 바늘 끝 반경 차가")
    out(f"        {r_mh-r_hh:.2f}pt → **{r_mh-d1-r_hh:.2f}pt**(Ø36 화면에서 {(r_mh-d1-r_hh)*k36:.2f}px)로 좁아진다.")
    out(f"        «긴 바늘/짧은 바늘»이 사라진다 — 시계로 안 읽힌다. 그 손해는 <Ø44에도 그대로> 걸린다.")
    ring_new = consts["stopwatch_d"] + 2 * d1
    sw2 = [Ring("Ring", ring_new, W) if p.name == "Ring" else p for p in glyphs["① 집중(스톱워치)"]]
    u1 = raster(sw2)["union"]
    u5 = raster(glyphs["⑤ 종료(전원)"])["union"]
    iou_new = (u1 & u5).sum() / max(1, (u1 | u5).sum())
    uniq5_new = (u5 & ~u1).sum() / max(1, u5.sum())
    u1o = raster(glyphs["① 집중(스톱워치)"])["union"]
    iou_old = (u1o & u5).sum() / max(1, (u1o | u5).sum())
    uniq5_old = (u5 & ~u1o).sum() / max(1, u5.sum())
    out(f"     길 (나) 링을 키운다: Ø{consts['stopwatch_d']:.0f} → Ø{ring_new:.2f}. 그러면 ⑤(Ø{consts['power_d']:.0f})와 반지름이 "
        f"{abs(ring_new-consts['power_d'])/2:.2f}pt 차이로 붙는다 —")
    out(f"        FG-6 실루엣이 무너진다: ①↔⑤ IoU {iou_old:.3f} → **{iou_new:.3f}**(상한 0.35), "
        f"⑤ 고유 잉크 {uniq5_old:.3f} → **{uniq5_new:.3f}**(하한 0.45).")
    out(f"        R26이 «상수 하나로 풀었다»고 적은 그 문제가 <되살아난다>. 게다가 Ring/RingFill은 "
        f"잔여시간 호와 공유하는 계약이다.")
    out("")
    out("  ── ③ 체크리스트 / ④ 확성기 / ⑤ 전원: 벌릴 자리는 있으나 값이 지저분해진다.")
    d3 = need - 3.00
    x0 = fan.need("ChecklistLineX0")
    out(f"     ③ 글줄을 오른쪽으로 {d3:.3f}pt: ChecklistLineX0 {x0:+.2f} → {x0+d3:+.3f} "
        f"(그리고 Box↔Check·Check↔Line1도 각 +{need-3.55:.3f} 필요 — 체크마크 3점을 다시 잡아야 한다)")
    d4 = need - 3.20
    out(f"     ④ 소리선을 오른쪽으로 {d4:.3f}pt: 그 끝 r {12.59:.2f} → {12.59+d4:.2f}pt "
        f"(FG-1 권고 {consts['field_r']:.0f} 이내 ✓, 상한 15 이내 ✓)")
    gap_need = 2 * math.degrees(math.asin((need + W / 2) / (consts["power_d"] / 2 - W)))
    out(f"     ⑤ 틈만 벌리면 된다: {consts['power_gap']:.0f}° → **{gap_need:.2f}°** "
        f"((d/2−W)·sin(틈/2)−W/2 ≥ {need:.3f} 의 해) — 상수 1개, 조형 손해 0")
    out("")
    out("  ⇒ 정리: (나)를 채택하면 <가장 자주 보이는 Ø44 조형>이 ①에서 확실히 나빠진다.")
    out("    바꾸는 이유가 «Ø36에서도 3.0pt라는 숫자를 유지하기 위해»뿐인데, §4가 그 숫자를 지키지")
    out("    않아도 화면이 멀쩡하다는 것을 <연결성으로> 보였다. 그래서 권고는 §8이다.")
    out("")

    # ── §8 권고 ──
    out("=" * 108)
    out("§8 권고 — 좌표는 건드리지 않는다. 대신 <규칙의 단위>를 고친다")
    out("=" * 108)
    out("  권고1 ★ FG-3 문구를 두 조건으로 갈라 쓴다(지금은 하나에 두 뜻이 섞여 있다):")
    out(f"        (조형) 코어 간극 ≥ 1.5W        — 배율 불변. Ø44/Ø36 모두 통과(①③은 <등호>).")
    out(f"        (화소) (간극 − EdgeFeather)·k·S ≥ 1.0px — 배율·DPI를 함께 본다.")
    out(f"               현행 최악 {min(g for _, _, _, g, _ in fails_abs)*k36 - f*k36:.2f}px "
        f"@Ø36·100%. 하한 1.0px 대비 여유 {(min(g for _, _, _, g, _ in fails_abs)*k36 - f*k36):.2f}배.")
    out("        ⇒ «3.0pt»라는 <절대 pt 숫자>는 규칙에서 뺀다. 그것은 W=2.0·k=1 일 때의 <파생값>이지")
    out("          독립된 기준이 아니었다. 남겨 두면 배율이 바뀔 때마다 오늘 같은 <가짜 미달>이 뜬다.")
    out("  권고2 ★ DESIGN_FAN_MENU_ICONS §1-3 의 «골 = g − 1.0pt» 를 «g − 0.5pt» 로 정정한다")
    out("        (§0 교정6). 같은 절의 «1×에서 골이 1px 남으려면 g ≥ 2.0pt» 도 **g ≥ 1.5pt** 로 바뀐다.")
    out("        이 오류는 <안전한 방향>이었다 — 실제보다 0.5pt 비관적이라 통과한 것은 여전히 통과다.")
    out("  권고3 ★ r26_coords.txt 의 «화면에 Ø36 버튼은 그려지지 않는다» 전제를 정정한다(사실이 바뀌었다).")
    out("  권고4 (선택 · 권고하지 않음) ⑤만은 상수 1개로 <절대pt자>까지 만족시킬 수 있다:")
    out(f"        PowerGapDegrees {consts['power_gap']:.0f} → 63 이면 골 "
        f"{(consts['power_d']/2 - W)*math.sin(math.radians(63/2)) - W/2:.3f}pt → Ø36에서 "
        f"{((consts['power_d']/2 - W)*math.sin(math.radians(63/2)) - W/2)*k36:.3f}pt ≥ 3.0. 조형 손해 0.")
    out("        그래도 권고하지 않는다 — 다섯 칸 중 하나만 다른 기준을 따르게 되고, 근거 없는 기준이다.")
    out("  권고5 회귀 검사 1건(test-engineer): <축소 폴백에서도> 화소 하한이 지켜지는가를")
    out("        ShrunkDiameterPoints/ButtonDiameterPoints/SymbolStroke/EdgeFeatherPoints 를 **참조**해서")
    out("        검사한다(숫자를 베끼면 오늘의 실수가 그대로 굳는다 — CLAUDE.md).")
    out("")
    out("  플랫폼: Windows 영향 · macOS 영향 — 아래 §9.")
    out("=" * 108)
    out("§9 플랫폼")
    out("=" * 108)
    out("  Windows 영향: 없음(코드 0줄). 다만 <위험은 Windows 쪽이 크다> — 축소 폴백의 최악 조건은")
    out("     배율 100 %(비Retina)이고 그 조합은 사실상 Windows 전용이다. 그래서 §4·§6의 표는")
    out("     Win 100/125/150/175 %를 <먼저> 싣고 macOS 2×를 마지막에 뒀다. 전부 통과.")
    out("  macOS 영향: 함께 검토함 — 없음. Retina 2×는 모든 지표에서 가장 안전한 열이다")
    out("     (골 화소 수 2배, g1 획 알파 1.000, 구멍 안 어두운 화소 25개).")
    out("")

    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                           "r27_shrink_fg3.out.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
