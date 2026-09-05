# -*- coding: utf-8 -*-
"""R16 — ★ 착용 비교 시트. 이것이 이 라운드의 산출물이다(리더가 사용자에게 보여 준다).

    열 C   인계본 착용 프리뷰 — ux-designer 재현기(render/icons.js)를 Chrome 으로 찍은 것(R15 C열과 같은 경로, R = 32 px).
           ★ 이 재현기는 SVG 속성을 두 번 내고(HTML 파서는 첫 것만 남긴다) 원문의 `fill: A` 21건 · 획 배수 12건을 버린다 —
           사용자가 지금까지 본 그림이라 그대로 둔다.
    열 C*  같은 Chrome, **원문 의미**(React: 나중 속성이 이긴다)로 고친 재현기 — 사용자가 준 디자인본이 실제로 그리는 것.
    열 D′  우리 착용 설계(R16) — 인계본 무대 + **인계본 캐릭터**(흰 선 · 검은 머리 · 흰 눈) 위에 우리 착용 기하를
           **1pt 하한(출하 배율 0.75 실폭) · 브라스 단색**으로 얹은 것. 장비 말고는 아무것도 다르지 않다 — 장비만 비교하는 열.
    열 D″  같은 설계를 **출하 화면**(밝은 바탕 · 검은 잉크 · 잉크로 꽉 찬 머리 · 눈 없음 · 우리 비율)에 놓은 것.
    열 E   잔여 차이 — 모델 실측(조각 생존) + 설계자 판정.

EYES 4행이 맨 위다(사용자: "특히 안경은 심함").

보조 시트
    r16_worn_truesize.png     실제 크기 — @0.75 1×(R 5.8px, 1pt=1px) · 2× · @1.00 1× · 2×, 출하 화면 / 어두운 바탕
    r16_raster_control.png    ★ 도구 교정 — Chrome(C, C*) vs 내 래스터(각 의미로, 명목 획·하한 없음), 장비 픽셀만 평균 |Δ|.

    python3 r16_worn_sheet.py
"""
import math, os, subprocess, sys, tempfile
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig, handoff
import r16_model as M
import r16_raster as RZ
import r13_bodyocclusion as B

RENDER = os.path.abspath(os.path.join(HERE, "../../../docs/handoff/design_handoff_equipment_window/render"))
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
SCRATCH = os.environ.get("R16_SCRATCH") or tempfile.mkdtemp(prefix="r16_")
os.makedirs(SCRATCH, exist_ok=True)

WORN_W, WORN_H = 232, 400
R_PX = 32.0
NOTE_W = 360
GAP = 10
KINDS = M.KINDS
STAGE_BG = "#0F0D0C"
DESK_BG = (0xEE, 0xEE, 0xEC)
DARK_DESK = (0x2A, 0x2A, 0x2E)
INK_DARK = (0x10, 0x12, 0x16)
INK_WHITE = (0xFF, 0xFF, 0xFF)
ZOOM = R_PX / handoff.HEAD_R_PX
STAGE_W, STAGE_H = int(round(158 * ZOOM)), int(round(238 * ZOOM))
STAGE_X = (WORN_W - STAGE_W) // 2                  # Chrome 셀 안의 스테이지 좌측(px) — 우리 머리 중심도 여기서 잰다
HEAD_CX = STAGE_X + STAGE_W / 2.0
HEAD_CY = handoff.HEAD_CY_PX * ZOOM

# ============================================================================
# 1. Chrome — 인계본 착용 프리뷰 (재현기 그대로 / 원문 의미로 고친 재현기)
# ============================================================================
def chrome_shot(html_path, png_path, w, h):
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    "--window-size=%d,%d" % (w, h), "--screenshot=" + png_path, "file://" + html_path],
                   check=True, capture_output=True)
    im = Image.open(png_path).convert("RGB")
    if im.size != (w, h): raise SystemExit("Chrome 캡처 크기 %s ≠ %s" % (im.size, (w, h)))
    return im

def icons_js(faithful=False):
    src = open(os.path.join(RENDER, "icons.js"), encoding="utf-8").read()
    if not faithful: return src
    # 원문(React) 의미: 옵션 속성이 기본 속성을 덮어쓴다. HTML 파서는 첫 속성을 남기므로 옵션을 **앞에** 낸다.
    reps = [('<path d="${d}" fill="url(#${G})"', '<path ${at(o || {})} d="${d}" fill="url(#${G})"'),
            ('<path d="${d}" fill="none" stroke="${C}"', '<path ${at(o || {})} d="${d}" fill="none" stroke="${C}"'),
            ('<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${G})"', '<circle ${at(o || {})} cx="${cx}" cy="${cy}" r="${r}" fill="url(#${G})"'),
            ('<rect x="${x}"', '<rect ${at(o || {})} x="${x}"')]
    for a, b in reps:
        if src.count(a) != 1: raise SystemExit("icons.js 패치 실패(거짓 통과 방지): %r 가 %d번" % (a, src.count(a)))
        src = src.replace(a, b)
    return src

def _stage_html(kind, zoom, line_hex, accent_hex, with_char=True, with_bg=True):
    slot = M.ISLOT[kind]
    ic = lambda k, st: "${ItemIcon('%s','%s','%s',%s)}" % (k, line_hex, accent_hex, st)
    char = ""
    if with_char:
        char = ('<svg viewBox="0 0 200 240" style="position:absolute;left:50%;top:26px;transform:translateX(-50%);'
                'width:158px;height:auto;overflow:visible;z-index:1">'
                '<ellipse cx="100" cy="218" rx="44" ry="6" fill="#000" opacity="0.55"></ellipse>'
                '<g fill="none" stroke="#FFFFFF" stroke-width="6" stroke-linecap="round" stroke-linejoin="round">'
                '<circle cx="100" cy="46" r="28" fill="#111111"></circle><path d="M100 74 V140"></path>'
                '<path d="M52 138 L100 88 L148 138"></path><path d="M100 140 L66 212"></path><path d="M100 140 L134 212"></path></g>'
                '<circle cx="90" cy="43" r="3.4" fill="#FFFFFF"></circle><circle cx="110" cy="43" r="3.4" fill="#FFFFFF"></circle></svg>')
    back = collar = head = eyes = neck = ""
    if kind == "longcape":
        back = ('<svg viewBox="0 0 200 240" style="position:absolute;left:50%;top:26px;transform:translateX(-50%);width:158px;height:auto;overflow:visible;z-index:0">'
                '<path d="M86 78 Q68 138 70 184 Q100 193 130 184 Q132 138 114 78 Z" fill="#A8332A" stroke="#7E1F17" stroke-width="2"></path></svg>')
    elif kind == "shortcape":
        back = ('<svg viewBox="0 0 200 240" style="position:absolute;left:50%;top:26px;transform:translateX(-50%);width:158px;height:auto;overflow:visible;z-index:0">'
                '<path d="M87 78 Q74 106 74 132 Q100 140 126 132 Q126 106 113 78 Z" fill="#A8332A" stroke="#7E1F17" stroke-width="2"></path></svg>')
    elif slot == "back":
        back = ('<div style="position:absolute;left:50%%;top:72px;transform:translateX(-50%%);width:88px;height:88px;opacity:0.4;z-index:0">%s</div>' % ic(kind, 2))
    if kind in M.CAPES:
        collar = ('<svg viewBox="0 0 200 240" style="position:absolute;left:50%;top:26px;transform:translateX(-50%);width:158px;height:auto;overflow:visible;z-index:4">'
                  '<g><path d="M74 72 Q100 88 126 72 L128 80 Q100 96 72 80 Z" fill="#8E241C" stroke="#7E1F17" stroke-width="2"></path>'
                  '<circle cx="100" cy="85" r="4.6" fill="#C8A15A" stroke="#7E1F17" stroke-width="1.8"></circle></g></svg>')
    if slot == "head":
        head = '<div style="position:absolute;left:50%%;top:6px;transform:translateX(-50%%);width:70px;height:70px;z-index:5">%s</div>' % ic(kind, 2.8)
    if slot == "eyes":
        eyes = '<div style="position:absolute;left:50%%;top:38px;transform:translateX(-50%%);width:48px;height:48px;z-index:6">%s</div>' % ic(kind, 3.4)
    if slot == "neck":
        neck = '<div style="position:absolute;left:50%%;top:78px;transform:translateX(-50%%);width:54px;height:54px;z-index:6">%s</div>' % ic(kind, 3.4)
    bg = ('background:radial-gradient(120%% 90%% at 50%% 12%%, #1C1815 0%%, #0F0D0C 62%%, #0B0A09 100%%)' if with_bg
          else 'background:%s' % STAGE_BG)
    glow = ('<div style="position:absolute;left:0;right:0;bottom:0;height:74px;background:radial-gradient(60%% 100%% at 50%% 100%%, rgba(200,161,90,0.16), transparent 70%%)"></div>' if with_bg else "")
    return ('<div style="zoom:%.5f;position:relative;width:158px;height:238px;overflow:hidden;%s">%s%s%s%s%s%s%s</div>'
            % (zoom, bg, glow, char, back, collar, head, eyes, neck))

def render_handoff_worn(r_px, line_hex=M.WEAR_COMMON, accent_hex=M.RAR_COMMON, with_char=True, with_bg=True,
                        faithful=False, tag="c"):
    zoom = r_px / handoff.HEAD_R_PX
    sw = int(round(158 * zoom))
    cols = 4; pad = 8
    W = cols * (WORN_W + pad) + pad; H = 4 * (WORN_H + pad) + pad
    cells = []
    for i, k in enumerate(KINDS):
        x = pad + (i % cols) * (WORN_W + pad); y = pad + (i // cols) * (WORN_H + pad)
        cells.append('<div style="position:absolute;left:%dpx;top:%dpx;width:%dpx;height:%dpx;background:%s;overflow:hidden">'
                     '<div style="position:absolute;left:%dpx;top:0px">%s</div></div>'
                     % (x, y, WORN_W, WORN_H, STAGE_BG, (WORN_W - sw) // 2,
                        _stage_html(k, zoom, line_hex, accent_hex, with_char, with_bg)))
    html = ("<!DOCTYPE html><html><head><meta charset='utf-8'><style>html,body{margin:0;background:#202020}</style></head>"
            "<body><div id='g' style='position:relative;width:%dpx;height:%dpx'></div><script>%s</script>"
            "<script>document.getElementById('g').innerHTML=`%s`;</script></body></html>" % (W, H, icons_js(faithful), "".join(cells)))
    hp = os.path.join(SCRATCH, "worn_%s_%d.html" % (tag, int(r_px * 10))); open(hp, "w", encoding="utf-8").write(html)
    im = chrome_shot(hp, os.path.join(SCRATCH, "worn_%s_%d.png" % (tag, int(r_px * 10))), W, H)
    out = {}
    for i, k in enumerate(KINDS):
        x = pad + (i % cols) * (WORN_W + pad); y = pad + (i // cols) * (WORN_H + pad)
        out[k] = im.crop((x, y, x + WORN_W, y + WORN_H))
    return out

# ============================================================================
# 2. 우리 렌더 — 무대 배경 · 본체(둘) · 착용 조각
# ============================================================================
def stage_background(w, h, r_px, stage_x=None):
    """인계본 스테이지 배경 근사(radial #1C1815→#0F0D0C(62%)→#0B0A09 + 바닥 브라스 광원)."""
    zoom = r_px / handoff.HEAD_R_PX
    sw, sh = int(round(158 * zoom)), int(round(238 * zoom)); ox = (w - sw) // 2 if stage_x is None else stage_x
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (xx - ox) / sw; v = yy / sh
    t = np.sqrt(((u - 0.5) / 0.60) ** 2 + ((v - 0.12) / 0.45) ** 2)
    c0, c1, c2 = (np.array(RZ._rgb(c), np.float32) for c in ("#1C1815", "#0F0D0C", "#0B0A09"))
    t1 = np.clip(t / 0.62, 0, 1)[..., None]; t2 = np.clip((t - 0.62) / 0.38, 0, 1)[..., None]
    img = np.where(t[..., None] <= 0.62, c0 + (c1 - c0) * t1, c1 + (c2 - c1) * t2)
    g = np.sqrt(((u - 0.5) / 0.60) ** 2 + ((v - 1.0) * (238.0 / 74.0)) ** 2)
    ga = np.clip(1.0 - g / 0.70, 0, 1) * 0.16 * (v >= 1.0 - 74.0 / 238.0)
    img = img * (1 - ga[..., None]) + np.array(RZ._rgb("#C8A15A"), np.float32) * ga[..., None]
    outside = (xx < ox) | (xx >= ox + sw) | (yy >= sh)
    img[outside] = np.array(RZ._rgb(STAGE_BG), np.float32)
    return np.clip(img + 0.5, 0, 255).astype(np.uint8)

def draw_handoff_figure(cv, ox, oy, r_px):
    """인계본 무대 캐릭터(README): 흰 선 6/28 R · 머리 r 28 채움 #111 · 흰 눈 r 3.4 · 발밑 그림자."""
    S = handoff.stage_to_R
    P = lambda p: (ox + p[0] * r_px, oy - p[1] * r_px)
    w = 6.0 / 28.0 * r_px
    sh = [S(100 + 44 * math.cos(2 * math.pi * i / 40), 218 + 6 * math.sin(2 * math.pi * i / 40)) for i in range(40)]
    cv.fill([P(q) for q in sh], (0, 0, 0), alpha=0.55)
    cv.disc(ox, oy, r_px, RZ._rgb("#111111"))
    ring = [S(100 + 28 * math.cos(2 * math.pi * i / 48), 46 + 28 * math.sin(2 * math.pi * i / 48)) for i in range(48)]
    cv.stroke([P(q) for q in ring], w, INK_WHITE, 1.0, loop=True)
    cv.stroke([P(S(100, 74)), P(S(100, 140))], w, INK_WHITE)
    cv.stroke([P(S(52, 138)), P(S(100, 88)), P(S(148, 138))], w, INK_WHITE)
    cv.stroke([P(S(100, 140)), P(S(66, 212))], w, INK_WHITE)
    cv.stroke([P(S(100, 140)), P(S(134, 212))], w, INK_WHITE)
    for ex in (90, 110):
        c = P(S(ex, 43)); cv.disc(c[0], c[1], 3.4 / 28.0 * r_px, INK_WHITE)

def draw_our_figure(cv, ox, oy, r_px, ink):
    """프로덕션 본체(2026-09-01 P1 이후): 뒤팔다리 → 몸통 → 앞팔다리 → 잉크로 꽉 찬 머리 원반(링 포함 · 눈 없음)."""
    P = lambda p: (ox + p[0] * r_px, oy - p[1] * r_px)
    for a, b in B.LEGS: cv.line(P(a), P(b), M.W_LEG_R * r_px, ink)
    cv.line(P((0, -1.0)), P((0, rig.HIP_R)), M.W_TORSO_R * r_px, ink)
    for a, b in B.ARMS: cv.line(P(a), P(b), M.W_ARM_R * r_px, ink)
    cv.disc(ox, oy, M.HEAD_OUTER_R * r_px, ink)

def draw_pieces(cv, pieces, to_px, width_px, palette, layer=None):
    ps = [p for p in pieces if layer is None or p.layer == layer]
    if not ps: return
    ga = ps[0].group_alpha
    g = cv.group() if ga < 1.0 else cv
    for p in ps:
        pts = [to_px(q) for q in p.pts]
        if p.fill is not None:
            if p.fill[0] == "grad": g.fill(pts, RZ._rgb(palette["accent"]), grad=(p.fill[1], p.fill[2]))
            elif p.fill[0] == "accent": g.fill(pts, RZ._rgb(palette["accent"]), alpha=p.fill[1])
            else: g.fill(pts, RZ._rgb(p.fill[1]), alpha=1.0)
        if p.line is not None:
            if p.line[0] == "solid": col, a = RZ._rgb(p.line[1]), p.line[2]
            else: col, a = RZ._rgb(palette[p.line[0]]), p.line[1]
            dash = None
            if p.dash is not None:
                k = width_px(p) / p.width_R()      # R→px 배율(같은 셀 안에서 상수)
                dash = (p.dash[0] * k, p.dash[1] * k)
            g.stroke(pts, width_px(p), col, a, p.loop, dash=dash)
    if ga < 1.0: cv.composite(g, ga)

def render_ours(kind, w, h, head_cx, head_cy, r_px, scale=M.SHIP, floor_pt=M.FLOOR_PT, palette=M.PALETTE_BRASS,
                figure="handoff", bg=None, ink=INK_DARK, pieces=None, stage_x=None, nominal=False):
    """figure: "handoff"(인계본 무대+캐릭터) | "ours"(출하 본체, bg 위) | None(장비만, bg 위)."""
    cv = RZ.Canvas(w, h, RZ._rgb(STAGE_BG) if bg is None else bg)
    if figure == "handoff": cv.paste_background(stage_background(w, h, r_px, stage_x))
    to_px = lambda p: (head_cx + p[0] * r_px, head_cy - p[1] * r_px)
    wpx = (lambda p: p.nominal_R * r_px) if nominal else (lambda p: p.width_R(scale, floor_pt) * r_px)
    ps = (pieces or M.WORN)[kind]
    draw_pieces(cv, ps, to_px, wpx, palette, layer="back")
    if figure == "handoff": draw_handoff_figure(cv, head_cx, head_cy, r_px)
    elif figure == "ours": draw_our_figure(cv, head_cx, head_cy, r_px, ink)
    draw_pieces(cv, ps, to_px, wpx, palette, layer="front")
    return cv.done()

# ============================================================================
# 3. 잔여 차이 — 아이템별 (모델 실측 + 설계자 판정)
# ============================================================================
_ROWS = None
def diff_lines(kind):
    global _ROWS
    if _ROWS is None: _ROWS = M.classify()
    rows = [r for r in _ROWS if r["kind"] == kind]
    n = len(rows); alive = sum(1 for r in rows if r["ok75"]); alive100 = sum(1 for r in rows if r["ok100"]); alive35 = sum(1 for r in rows if r["ok35"])
    dead = [r for r in rows if not r["ok75"]]
    l1 = "조각 %d · 1pt 생존 @0.75 %d · @1.0 %d · @0.35 %d" % (n, alive, alive100, alive35)
    l2 = ("@0.75 규칙 미달: " + " ".join("%s" % r["p"].src for r in dead)) if dead else "@0.75 규칙 미달: 없음"
    return [l1, l2]

NOTES = {
    "sunglasses":  "C*와 같은 물건. 테·코다리·렌즈(브라스 α0.6)가 산다. 다리 2 는 0.8획 길이라 테 끝의 점(1×). 하이라이트 1.3획 점. 획은 C의 1.49배. C의 렌즈는 회색(일반 등급색)·C*는 그 색 α0.6 — 우리는 브라스 α0.6(리더 결정).",
    "roundglasses": "C*와 같은 물건. 테 2 원·코다리(두 테가 맞닿는다: 간격 0.99pt − 획 1pt)·다리(1.7획 짧은 획). 렌즈 α0.16 은 검은 머리 위에서 C처럼 거의 안 보인다. 눈은 D″ 본체에 없다(BakeEyes=false). 안쪽 맑은 지름 3.1pt(1× 에서 3px).",
    "goggles":     "C*와 같은 물건. 판·렌즈 원 2(브라스 α0.6)·끈 2 산다. 브리지(0.8획)는 두 원 사이에 묻힌다. 하이라이트는 점. C의 렌즈는 워시라 어둡고 C*/우리는 α0.6 채움.",
    "monocle":     "C*와 같은 물건. 알(테 ×1.5 = 1.006pt — 유일하게 하한을 안 받는다)·줄·끝 구슬(C*에서 솔리드). 파선은 점 지름 1pt·주기 0.81pt 라 실선으로 붙는다(배율 1.5 부터 점열). C 는 테 ×1.5 와 구슬 솔리드를 버린 그림이다.",
    "clothhat":    "C*와 같은 물건. 관·띠(강조 α0.5)·챙·하이라이트. 띠는 1-C 미달(ρ 0.137 < 0.172R) — 워시가 얇아 선 사이 색면이 약해질 뿐 형태는 남는다. 머리 링이 챙 뒤로 비친다(C와 같다).",
    "furhat":      "C*와 같은 물건. 폼폼·관·단(α0.22)·결(×0.8→1pt, 1.58R)·하이라이트 전부 산다. R15 가 눈으로 기각한 결은 C에도 있는 선이다 — C의 결(×1.0)과 같은 굵기로 그려진다.",
    "fedora":      "C*와 같은 물건. 관·띠(1-C 미달, 위와 같은 뜻)·챙·하이라이트. 크리스는 정면 기하 그대로.",
    "crown":       "몸·테·봉우리 원 3(C*: 솔리드 브라스)·보석 3·하이라이트. 보석 CF3/CF4(지름 0.19R = 1.1pt)·봉우리 CB6/CB7(0.21R = 1.2pt)은 1× 에서 1px 점. R=32 시트에서는 보인다. 1-A 미달 6.",
    "bowtie":      "C*와 같은 물건. 날개 2·매듭(α0.65 브라스)·하이라이트(1.3획 점).",
    "stripedtie":  "C*와 같은 물건. 매듭·블레이드·줄무늬 2(α0.55, 1-A 몽당변 0.87획 — 모서리가 뭉개질 뿐 띠는 남는다)·하이라이트.",
    "scarf":       "C*와 같은 물건. 감은 띠(ρ 0.161 < 0.172R: 워시 대신 선이 채운다)·자락·술 3(1.0획 → 점)·하이라이트. 자락은 인계본 그대로 1개.",
    "bellnecklace": "C*와 같은 물건. 목줄·구슬 3(지름 0.13R = 0.75pt → 1× 에서 1px 점, 시트에서는 보인다)·방울 몸(α0.4)·테(ρ 0.08R: 선으로 뭉친다)·추(C*: 솔리드)·하이라이트.",
    "shortcape":   "무대 도형 그대로: 뒤판 #A8332A + 칼라 #8E241C + 걸쇠(브라스). 아이콘은 안 얹는다(인계본 eq.back=null). 무대 획 0.42pt → 1pt(×2.4) 라 윤곽이 C보다 굵다. 선 #7E1F17 은 WornColor 가 #8C231A 로 바꾼다 → 우회 필요.",
    "longcape":    "짧은망토와 같은 문법, 뒤판만 길다. 칼라(ρ 0.142)·걸쇠(ρ 0.157)는 1-C 미달 — 채움 대신 선이 채운다(걸쇠 지름 0.33R = 1.9pt).",
    "wings":       "등 아이콘 40 % 그대로 몸 뒤에(정맥 2 ×0.7·척추 둥근사각 α0.4·하이라이트). C*와 같은 물건. 브라스 40 % 는 어두운 무대에서 C와 같고 밝은 바탕(D″)에서는 더 약하다.",
    "backpack":    "등 아이콘 40 %. 손잡이(×1.1)·몸·뚜껑(α0.28)·버클(α0.7)·큰 주머니(α0.14)·하이라이트 전부 규칙 통과. C*와 같은 물건. C 는 손잡이 ×1.1 과 뚜껑·주머니 α 를 버린 그림.",
}

# ============================================================================
# 4. 시트 조립
# ============================================================================
def label(d, xy, text, size=13, color=(40, 40, 40), bold=False):
    d.text(xy, RZ.sanitize(text), fill=color, font=RZ.font(size, bold))

def build_sheet(worn_c, worn_cf, ctrl_summary):
    W = GAP + 4 * (WORN_W + GAP) + NOTE_W + GAP
    HEADER = 136; ROW = WORN_H + 34
    H = HEADER + ROW * len(KINDS) + GAP
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    label(d, (GAP, 10), "R16 착용 표면 재설계 — C 인계본 착용(재현기 그대로) | C* 인계본 착용(원문 의미) | D′ 우리 착용(1pt 하한 · 브라스 단색 · 인계본 기하, 인계본 무대+캐릭터) | D″ 출하 화면 | E 잔여 차이", 15, bold=True)
    label(d, (GAP, 34), "같은 크기: 머리 반경 R = %.0f px · 같은 머리 중심. D′/D″ 획 = 출하 배율 0.75 의 실폭(1pt 하한 = 0.1719 R) × R. 인계본 획 0.67~0.81pt → 1pt 가 ×1.24~1.49 (2pt 하한은 ×2.5~3.0 이었다)." % R_PX, 12, (70, 70, 70))
    label(d, (GAP, 52), "★ C 는 ux-designer 재현기(icons.js)가 SVG 속성을 두 번 내서 원문의 `fill: A` 채움 23건·획 배수 11건(조각 33/91)을 버린 그림이다(HTML 파서는 첫 속성만 남긴다 — r16_dupattr.py). C* 가 사용자가 준 디자인본이 그리는 것. D′ 는 C* 를 따른다.", 12, (150, 40, 40))
    label(d, (GAP, 70), "D′ 는 인계본 캐릭터(흰 선·검은 머리·흰 눈) 위에 우리 장비만 얹었다 — 장비 말고 다른 변수가 없다. D″ 는 출하 본체(잉크로 꽉 찬 머리·눈 없음(BakeEyes=false, 사용자 지시 2026-09-01)·우리 비율·검은 잉크·밝은 바탕).", 12, (70, 70, 70))
    label(d, (GAP, 88), "EYES 4행이 맨 위(사용자: \"특히 안경은 심함\"). 합격선 = 「같은 물건으로 읽힌다」. 색: 선·강조 = 브라스 #C8A15A(UiChrome.Accent, WornColor 항등) · 하이라이트 흰 42 % · 망토 무대색 그대로.", 12, (70, 70, 70))
    label(d, (GAP, 106), "★ 오프라인 래스터다(둥근 캡). 최종 판정은 실제 빌드 캡처로만. 도구 교정(장비 픽셀만 평균 |Δ|): %s. 1pt 가 Windows 1× 에서 어떻게 보이는지는 실기 미확인." % ctrl_summary, 12, (150, 40, 40))
    xs = [GAP + i * (WORN_W + GAP) for i in range(4)]; xE = xs[3] + WORN_W + GAP
    for i, k in enumerate(KINDS):
        y = HEADER + ROW * i
        label(d, (xs[0], y), "%s  %s / %s" % (M.KO[k], k, M.SLOT[k]), 14, bold=True)
        y0 = y + 22
        im.paste(worn_c[k], (xs[0], y0))
        im.paste(worn_cf[k], (xs[1], y0))
        im.paste(render_ours(k, WORN_W, WORN_H, HEAD_CX, HEAD_CY, R_PX, figure="handoff", stage_x=STAGE_X), (xs[2], y0))
        im.paste(render_ours(k, WORN_W, WORN_H, HEAD_CX, HEAD_CY, R_PX, figure="ours", bg=DESK_BG, ink=INK_DARK), (xs[3], y0))
        RZ.text_block(d, (xE, y0), diff_lines(k) + [""] + RZ.wrap(NOTES[k], 28, 33), size=11, color=(50, 50, 50), lh=15)
        for x, lab in zip(xs, ("C", "C*", "D′", "D″")):
            label(d, (x + 4, y0 + 2), lab, 12, (200, 200, 200), bold=True)
    return im

def build_truesize():
    """실제 크기 — 인계본 프리뷰 1×(R 22.12) | 출하 화면 @0.75 1×(R 5.82) · 2×(11.63) · @1.00 1×(7.76) · 2×(15.51) | 어두운 바탕·흰 잉크 @0.75 2×"""
    cols = 2; rows = 8
    cellw = 60 + 6 + 40 + 6 + 60 + 6 + 48 + 6 + 72 + 6 + 60 + 20
    W = GAP + cols * (cellw + 10); H = 70 + rows * 150
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    label(d, (GAP, 8), "실제 크기 — 인계본 프리뷰 1×(R 22.12px) | 출하 화면(검은 잉크·밝은 바탕) @0.75 1×(R 5.8px, 1pt=1px) · 2×(1pt=2px) · @1.00 1×(R 7.8px) · 2× | 어두운 바탕·흰 잉크 @0.75 2×", 13, bold=True)
    label(d, (GAP, 30), "★ 인계본 프리뷰 머리는 우리 출하 머리의 3.8배다. 1× Windows 에서 1pt 획 = 1 픽셀. 여기서 안 보이는 것은 출하 배율 1× 화면에 없는 것이다(오프라인 래스터 — 실기 미확인).", 12, (150, 40, 40))
    worn1 = render_handoff_worn(22.12, faithful=True, tag="ts")
    for i, k in enumerate(KINDS):
        cx = GAP + (i % cols) * (cellw + 10); cy = 60 + (i // cols) * 150
        label(d, (cx, cy), "%s (%s)" % (M.KO[k], k), 12, bold=True)
        y = cy + 18; x = cx
        w1 = worn1[k].crop((WORN_W // 2 - 30, 0, WORN_W // 2 + 30, 120)); im.paste(w1, (x, y)); x += 60 + 6
        for r_px, scale, cw in ((5.816, 0.75, 40), (11.632, 0.75, 60), (7.755, 1.0, 48), (15.51, 1.0, 72)):
            hcy = r_px * HEAD_CY / R_PX
            im.paste(render_ours(k, cw, 120, cw / 2.0, hcy, r_px, scale=scale, figure="ours", bg=DESK_BG, ink=INK_DARK), (x, y)); x += cw + 6
        hcy = 11.632 * HEAD_CY / R_PX
        im.paste(render_ours(k, 60, 120, 30, hcy, 11.632, scale=0.75, figure="ours", bg=DARK_DESK, ink=INK_WHITE), (x, y))
    return im

def build_control(noc_c, noc_cf):
    """도구 교정 — 장비만(캐릭터·배경 없음): Chrome C vs 내 래스터(icons_js 의미) · Chrome C* vs 내 래스터(원문 의미).
    장비 픽셀(배경이 아닌 픽셀)만의 평균 |Δ| 와 |Δ|>32 비율을 돌려준다."""
    sets = {"icons_js": M.worn_set("icons_js"), "original": M.worn_set("original")}
    W = GAP + 6 * (WORN_W + GAP); H = 50 + len(KINDS) * (WORN_H + 26)
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    label(d, (GAP, 8), "도구 교정 — [C Chrome | 내 래스터(icons_js 의미) | 차 ×4]   [C* Chrome | 내 래스터(원문 의미) | 차 ×4]  — 명목 획·하한 없음·인계본 색", 13, bold=True)
    bg = np.array(RZ._rgb(STAGE_BG))
    res = {"icons_js": [], "original": []}
    for i, k in enumerate(KINDS):
        y = 50 + i * (WORN_H + 26); label(d, (GAP, y), "%s (%s)" % (M.KO[k], k), 12, bold=True); y += 18
        for j, (sem, chrome_im) in enumerate((("icons_js", noc_c[k]), ("original", noc_cf[k]))):
            ours = render_ours(k, WORN_W, WORN_H, HEAD_CX, HEAD_CY, R_PX, figure=None, palette=M.PALETTE_HANDOFF,
                               pieces=sets[sem], nominal=True)
            A = np.asarray(chrome_im, np.int16); Bm = np.asarray(ours, np.int16)
            m = (np.abs(A - bg).sum(axis=2) > 24) | (np.abs(Bm - bg).sum(axis=2) > 24)
            dd = np.abs(A - Bm).mean(axis=2)
            mean = float(dd[m].mean()) if m.any() else 0.0; big = float((dd[m] > 32).mean()) if m.any() else 0.0
            res[sem].append((k, mean, big))
            x = GAP + (3 * j) * (WORN_W + GAP)
            im.paste(chrome_im, (x, y)); im.paste(ours, (x + WORN_W + GAP, y))
            im.paste(Image.fromarray(np.clip(np.abs(A - Bm) * 4, 0, 255).astype(np.uint8)), (x + 2 * (WORN_W + GAP), y))
            label(d, (x + 2 * (WORN_W + GAP) + 4, y + 2), "장비픽셀 평균|Δ| %.1f/255 · >32: %.0f%%" % (mean, 100 * big), 11, (120, 30, 30))
    return im, res

def main():
    worn_c = render_handoff_worn(R_PX, tag="c")
    worn_cf = render_handoff_worn(R_PX, faithful=True, tag="cf")
    noc_c = render_handoff_worn(R_PX, with_char=False, with_bg=False, tag="noc")
    noc_cf = render_handoff_worn(R_PX, with_char=False, with_bg=False, faithful=True, tag="nocf")
    ctrl, res = build_control(noc_c, noc_cf)
    ctrl.save(os.path.join(HERE, "r16_raster_control.png"))
    summ = []
    for sem in ("icons_js", "original"):
        rows = res[sem]; mean = sum(r[1] for r in rows) / len(rows); worst = max(rows, key=lambda r: r[1])
        summ.append("%s 평균 %.1f · 최악 %s %.1f" % ("C↔래스터" if sem == "icons_js" else "C*↔래스터", mean, worst[0], worst[1]))
        print("control %-9s" % sem, " | ".join("%s %.1f/%.0f%%" % (k, m, 100 * b) for k, m, b in rows))
    summary = " / ".join(summ)
    sheet = build_sheet(worn_c, worn_cf, summary)
    sheet.save(os.path.join(HERE, "r16_worn_sheet.png"))
    print("wrote r16_worn_sheet.png %dx%d" % sheet.size)
    ts = build_truesize()
    ts.save(os.path.join(HERE, "r16_worn_truesize.png"))
    print("wrote r16_worn_truesize.png %dx%d" % ts.size)

if __name__ == "__main__":
    main()
