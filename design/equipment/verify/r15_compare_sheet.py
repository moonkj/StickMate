# -*- coding: utf-8 -*-
"""R15 — ★ 나란히 비교 시트. 이것이 이 라운드의 진짜 산출물이다(리더가 사용자에게 보여 준다).

    열 A  인계본 카드 렌더     — ux-designer 재현기(render/icons.js)를 headless Chrome 으로 찍은 것.
                               원본 HTML 은 support.js 부재로 안 열린다 — 이 재현기가 유일한 렌더 경로다.
    열 B  우리 카드 설계 렌더   — r15_model.CARD_SET 을 안 D 모델(평면 사전합성 색 · 획 등급 · 톤 3)로
                               내 래스터로 그린 것. 같은 프레이밍(64 viewBox → 셀), 같은 팔레트 입력.
    열 C  인계본 착용 프리뷰    — 인계본 README 「장비 오버레이 위치」 그대로(스테이지 200×240 · 박스 70/48/54/88 ·
                               획 2.8/3.4/3.4/2 · 등 아이콘 40% · 망토 전용 뒤판). Chrome 으로 찍는다.
    열 D  우리 몸 부착 설계     — r15_model.BODY_SET 의 몸 생존 조각을 출하 배율 0.75 획 실폭으로,
                               같은 머리 반경(px)에서 그린 것. 밝은 바탕 · 검은 잉크(출하 기본).
    열 E  아직 다른 점 한 줄

★ 「같은 크기」: 열 A·B 는 같은 셀에서 같은 64→px 배율. 열 C·D 는 **머리 반경 R 이 같은 px**(R_PX).

    python3 r15_compare_sheet.py            # r15_compare_sheet.png · r15_compare_truesize.png · r15_compare_shared.png
"""
import math, os, subprocess, sys, json
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, handoff
import r15_model as M
import r15_raster as RZ
import r13_bodyocclusion as B

HERE = os.path.dirname(os.path.abspath(__file__))
RENDER = os.path.abspath(os.path.join(HERE, "../../../docs/handoff/design_handoff_equipment_window/render"))
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
import tempfile
SCRATCH = os.environ.get("R15_SCRATCH") or tempfile.mkdtemp(prefix="r15_")   # Chrome 중간 파일은 저장소 밖에
os.makedirs(SCRATCH, exist_ok=True)

CELL = 232                     # 카드 셀(58px 의 4배)
WORN_W, WORN_H = 232, 400      # 착용 셀
R_PX = 32.0                    # 착용 셀의 머리 반경(px) — 열 C·D 공통
NOTE_W = 330
GAP = 10
KINDS = M.KINDS

HANDOFF_BG = "#0F0D0C"
INK_HEX, BRASS_HEX = "#E8E2D6", "#C8A15A"
WEAR_HEX, WEAR_AC_HEX = "#D8B27A", "#8A8F98"        # 인계본 착용 오버레이(일반 등급): color / accent
DESK_BG = (0xEE, 0xEE, 0xEC)                        # 우리 몸: 밝은 바탕
INK = (0x10, 0x12, 0x16)

# ============================================================================
# 1. Chrome — 인계본 렌더
# ============================================================================
def chrome_shot(html_path, png_path, w, h):
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    "--window-size=%d,%d" % (w, h), "--screenshot=" + png_path,
                    "file://" + html_path], check=True, capture_output=True)
    im = Image.open(png_path).convert("RGB")
    if im.size != (w, h):
        raise SystemExit("Chrome 캡처 크기 %s ≠ %s" % (im.size, (w, h)))
    return im

def icons_js():
    return open(os.path.join(RENDER, "icons.js"), encoding="utf-8").read()

def render_handoff_cards(size, accent=BRASS_HEX, glow=True):
    """16종 카드 아이콘을 size px 셀로. 반환 {kind: PIL}"""
    cols = 4; rows = 4; pad = 8
    W = cols * (size + pad) + pad; H = rows * (size + pad) + pad
    cells = []
    for i, k in enumerate(KINDS):
        x = pad + (i % cols) * (size + pad); y = pad + (i // cols) * (size + pad)
        bg = ("radial-gradient(70%% 70%% at 50%% 42%%, %s1F, transparent 72%%), %s" % (accent, HANDOFF_BG)) if glow else HANDOFF_BG
        cells.append('<div style="position:absolute;left:%dpx;top:%dpx;width:%dpx;height:%dpx;background:%s;'
                     'display:flex;align-items:center;justify-content:center"><div style="width:%dpx;height:%dpx">'
                     "${ItemIcon('%s','%s','%s',2.2)}</div></div>" % (x, y, size, size, bg, size, size, k, INK_HEX, accent))
    html = ("<!DOCTYPE html><html><head><meta charset='utf-8'><style>html,body{margin:0;background:#202020}</style>"
            "</head><body><div id='g' style='position:relative;width:%dpx;height:%dpx'></div>"
            "<script>%s</script><script>document.getElementById('g').innerHTML=`%s`;</script></body></html>"
            % (W, H, icons_js(), "".join(cells)))
    hp = os.path.join(SCRATCH, "cards_%d.html" % size); open(hp, "w", encoding="utf-8").write(html)
    im = chrome_shot(hp, os.path.join(SCRATCH, "cards_%d.png" % size), W, H)
    out = {}
    for i, k in enumerate(KINDS):
        x = pad + (i % cols) * (size + pad); y = pad + (i // cols) * (size + pad)
        out[k] = im.crop((x, y, x + size, y + size))
    return out

def _stage_html(kind, zoom):
    """인계본 README 착용 프리뷰 1셀(스테이지 158×238 · zoom)."""
    slot = M.SLOT[kind]
    ic = lambda k, st: "${ItemIcon('%s','%s','%s',%s)}" % (k, WEAR_HEX, WEAR_AC_HEX, st)
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
    elif slot == "BACK":
        back = ('<div style="position:absolute;left:50%%;top:72px;transform:translateX(-50%%);width:88px;height:88px;opacity:0.4;z-index:0">%s</div>' % ic(kind, 2))
    if kind in ("longcape", "shortcape"):
        collar = ('<svg viewBox="0 0 200 240" style="position:absolute;left:50%;top:26px;transform:translateX(-50%);width:158px;height:auto;overflow:visible;z-index:4">'
                  '<g><path d="M74 72 Q100 88 126 72 L128 80 Q100 96 72 80 Z" fill="#8E241C" stroke="#7E1F17" stroke-width="2"></path>'
                  '<circle cx="100" cy="85" r="4.6" fill="#C8A15A" stroke="#7E1F17" stroke-width="1.8"></circle></g></svg>')
    if slot == "HEAD":
        head = '<div style="position:absolute;left:50%%;top:6px;transform:translateX(-50%%);width:70px;height:70px;z-index:5">%s</div>' % ic(kind, 2.8)
    if slot == "EYES":
        eyes = '<div style="position:absolute;left:50%%;top:38px;transform:translateX(-50%%);width:48px;height:48px;z-index:6">%s</div>' % ic(kind, 3.4)
    if slot == "NECK":
        neck = '<div style="position:absolute;left:50%%;top:78px;transform:translateX(-50%%);width:54px;height:54px;z-index:6">%s</div>' % ic(kind, 3.4)
    return ('<div style="zoom:%.5f;position:relative;width:158px;height:238px;overflow:hidden;'
            'background:radial-gradient(120%% 90%% at 50%% 12%%, #1C1815 0%%, #0F0D0C 62%%, #0B0A09 100%%)">'
            '<div style="position:absolute;left:0;right:0;bottom:0;height:74px;background:radial-gradient(60%% 100%% at 50%% 100%%, rgba(200,161,90,0.16), transparent 70%%)"></div>'
            '%s%s%s%s%s%s</div>' % (zoom, char, back, collar, head, eyes, neck))

def render_handoff_worn(r_px):
    """16종 착용 프리뷰. 스테이지를 머리 반경 r_px 가 되도록 zoom. 반환 {kind: PIL (WORN_W×WORN_H)}"""
    zoom = r_px / handoff.HEAD_R_PX
    sw, sh = int(round(158 * zoom)), int(round(238 * zoom))
    cols = 4; pad = 8
    W = cols * (WORN_W + pad) + pad; H = 4 * (WORN_H + pad) + pad
    cells = []
    for i, k in enumerate(KINDS):
        x = pad + (i % cols) * (WORN_W + pad); y = pad + (i // cols) * (WORN_H + pad)
        cells.append('<div style="position:absolute;left:%dpx;top:%dpx;width:%dpx;height:%dpx;background:#0F0D0C;overflow:hidden">'
                     '<div style="position:absolute;left:%dpx;top:0px">%s</div></div>'
                     % (x, y, WORN_W, WORN_H, (WORN_W - sw) // 2, _stage_html(k, zoom)))
    html = ("<!DOCTYPE html><html><head><meta charset='utf-8'><style>html,body{margin:0;background:#202020}</style></head>"
            "<body><div id='g' style='position:relative;width:%dpx;height:%dpx'></div><script>%s</script>"
            "<script>document.getElementById('g').innerHTML=`%s`;</script></body></html>" % (W, H, icons_js(), "".join(cells)))
    hp = os.path.join(SCRATCH, "worn_%d.html" % int(r_px * 10)); open(hp, "w", encoding="utf-8").write(html)
    im = chrome_shot(hp, os.path.join(SCRATCH, "worn_%d.png" % int(r_px * 10)), W, H)
    out = {}
    for i, k in enumerate(KINDS):
        x = pad + (i % cols) * (WORN_W + pad); y = pad + (i // cols) * (WORN_H + pad)
        out[k] = im.crop((x, y, x + WORN_W, y + WORN_H))
    # 스테이지 머리 중심 y(px) — 열 D 가 같은 자리에 머리를 둔다
    head_cy = handoff.HEAD_CY_PX * zoom
    return out, head_cy

# ============================================================================
# 2. 우리 렌더 — 카드
# ============================================================================
def card_to_px(kind, cell):
    """R → 카드 셀 px. 인계본과 같은 프레이밍(64 viewBox 가 셀 한 변)."""
    box, top = handoff.SLOT_BOX[handoff.ITEM_SLOT[kind]]
    u = box / 64.0; k = cell / 64.0
    def f(p):
        xi = p[0] / (u / handoff.HEAD_R_PX) + 32.0
        yi = (handoff.HEAD_CY_PX - p[1] * handoff.HEAD_R_PX - top) / u
        return (xi * k, yi * k)
    return f

def card_stroke_px(cell, grade):
    base = 2.2 * cell / 64.0            # 인계본 획 비율(제안: IconStroke = 2.2 × IconSize/64)
    return base * M.GRADE[grade][0]

def render_our_card(kind, cell, pieces=None, accent=M.BRASS, ink=M.HANDOFF_INK, bg=M.CARD_BG, fit=False):
    pieces = pieces if pieces is not None else M.CARD_SET[kind]
    mat = M.CAPE_MAT if kind in M.MAT else None
    colors = M.card_colors(pieces, accent=accent, ink=ink, bg=bg, mat=mat)
    cv = RZ.Canvas(cell, cell, bg)
    if fit:
        # AccessoryCardIcon.TryBuild 방식: 봉투 최대변을 cell×0.86 에 맞춘다 (공유 기하 변형용)
        pts = [q for p in pieces for q in p.pts]
        x0, y0, x1, y1 = rig.bounds(pts); span = max(x1 - x0, y1 - y0); s = cell * 0.86 / span
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        to_px = lambda p: (cell / 2 + (p[0] - cx) * s, cell / 2 - (p[1] - cy) * s)
    else:
        to_px = card_to_px(kind, cell)
    RZ.draw_pieces(cv, pieces, colors, to_px, lambda p: card_stroke_px(cell, p.grade), surf_bit=M.CARD)
    return cv.done()

# ============================================================================
# 3. 우리 렌더 — 몸 부착
# ============================================================================
def draw_figure(cv, ox, oy, r_px, ink=INK, bg=DESK_BG):
    P = lambda p: (ox + p[0] * r_px, oy - p[1] * r_px)
    for a, b in B.LEGS: cv.line(P(a), P(b), B.W_LEG * r_px, ink)
    cv.line(P((0, -1.0)), P((0, rig.HIP_R)), B.W_TORSO * r_px, ink)
    for a, b in B.ARMS: cv.line(P(a), P(b), B.W_ARM * r_px, ink)
    cv.ellipse(ox, oy, r_px, fill=bg, outline=ink, width=B.W_RING * r_px)
    for sx in (1, -1):
        cv.ellipse(ox + sx * rig.EYE_X * r_px, oy - rig.EYE_Y * r_px, rig.PUPIL_R * r_px, fill=ink)

def render_our_body(kind, w, h, head_cy, r_px, scale=M.SHIP, pieces=None, bg=DESK_BG, ink=INK):
    pieces = pieces if pieces is not None else M.BODY_SET[kind]
    pr, sc = M.hex_rgb(M.PROD_COLOR[kind][0]), M.hex_rgb(M.PROD_COLOR[kind][1])
    colors = M.body_colors(pieces, pr, sc)
    cv = RZ.Canvas(w, h, bg)
    ox, oy = w / 2.0, head_cy
    to_px = lambda p: (ox + p[0] * r_px, oy - p[1] * r_px)
    wpx = lambda p: p.width_R(scale) * r_px
    if M.SLOT[kind] == "BACK":
        RZ.draw_pieces(cv, pieces, colors, to_px, wpx, surf_bit=M.BODY)
        draw_figure(cv, ox, oy, r_px, ink, bg)
    else:
        draw_figure(cv, ox, oy, r_px, ink, bg)
        RZ.draw_pieces(cv, pieces, colors, to_px, wpx, surf_bit=M.BODY)
    return cv.done()

# ============================================================================
# 4. 아직 다른 점 — 아이템별 한 줄 (모델 실측에서 뽑는다 + 설계자 판정)
# ============================================================================
def diff_note(kind):
    card = M.CARD_SET[kind]; body = M.BODY_SET[kind]
    nb = sum(1 for p in body if p.on(M.BODY)); nc = len(card)
    dead = [p for p in card if not any(b.src == p.src and b.on(M.BODY) for b in body)]
    kinds = {}
    for p in dead:
        key = {"H": "하이라이트", "S": "낱선", "CF": "강조원", "CB": "원", "RB": "둥근사각", "F": "강조채움", "B": "채움"}[
            "".join(c for c in p.src if c.isalpha())]
        kinds[key] = kinds.get(key, 0) + 1
    lost = " · ".join("%s %d" % (k, v) for k, v in kinds.items())
    return nc, nb, lost

NOTES = {
    "clothhat":  "카드: 그라디언트→평면. 몸: 챙이 앞에만(3/4 재저작) — 인계본 좌우 대칭 챙과 다르다. 하이라이트 산다.",
    "furhat":    "카드: 그라디언트→평면. 몸: 털 결은 규칙을 통과하지만 진폭=획 폭이라 「굵은 물결선」으로 읽혀 눈으로 기각 — 카드 전용. 단은 보조색 띠로 산다.",
    "fedora":    "카드: 그라디언트→평면. 몸: 크리스(정수리 눌림)가 채움 윤곽 노치로만 남는다. 하이라이트 산다.",
    "crown":     "몸: 보석 3 · 봉우리 원 3 · 하이라이트 = 7조각 전부 소멸(0.19~0.25R). 카드에만 있다.",
    "sunglasses": "몸: 안경다리 2 · 하이라이트 소멸. 렌즈는 우리 4각 사다리꼴(인계본 곡선 아님).",
    "roundglasses": "몸: 렌즈가 **불투명**(사용자 제약 — 인계본은 α0.16 투명). 다리 2 · 하이라이트 2 소멸.",
    "goggles":   "몸: 렌즈 원반 2개를 새로 얹었다(§4-2-5). 브리지·끈 낱선·하이라이트 소멸. 판은 우리 8각.",
    "monocle":   "몸: 알 r 0.46R + 굵은 테(×1.5) + 줄(파선→실선). 끝 구슬·하이라이트 소멸. 드러난 눈은 몸 전용.",
    "bowtie":    "몸: 하이라이트 소멸(1.33 디테일획). 매듭은 우리 사각(인계본 둥근사각).",
    "stripedtie": "몸: 줄무늬 2개(0.48R). 하이라이트 소멸. 블레이드는 우리 5각 다트.",
    "scarf":     "몸: 술 3 소멸. 하이라이트 산다. 자락 앞뒤 2개(인계본 1개).",
    "bellnecklace": "몸: 방울을 실루엣 하나(몸+테 합침 0.84×0.80R)+추(r 0.26R)로 다시 세웠다. 구슬 3 · 테 낱개 · 하이라이트 소멸.",
    "shortcape": "몸: 하이라이트 산다. 주름은 우리 사선 2(인계본 세로 1). 옷깃=요크(보조색) 대응.",
    "longcape":  "몸: 걸쇠 r 0.26R(인계본 0.16R 확대) · 하이라이트 산다. 제비꼬리 밑단은 우리 것.",
    "wings":     "몸: 정맥 2 · 하이라이트 산다(그늘색). 척추는 우리 낱선(인계본 둥근사각 α0.4).",
    "backpack":  "몸: 손잡이 · 하이라이트 산다. 앞주머니 2(둥근사각) 소멸. 뚜껑=우리 PackLid 대응.",
}

# ============================================================================
# 5. 시트 조립
# ============================================================================
def label(d, xy, text, size=13, color=(40, 40, 40), bold=False):
    d.text(xy, text, fill=color, font=RZ.font(size, bold))

def build_sheet(cards_h, worn_h, head_cy):
    W = GAP + CELL + GAP + CELL + GAP + WORN_W + GAP + WORN_W + GAP + NOTE_W + GAP
    HEADER = 96; ROW = WORN_H + 34
    H = HEADER + ROW * len(KINDS) + GAP
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    label(d, (GAP, 10), "R15 인계본 ↔ 우리 설계 나란히 비교 (design-equipment, 2026-09-05) — 합격 기준은 「눈」이다(§13-0)", 16, bold=True)
    label(d, (GAP, 36), "A 인계본 카드(ux-designer 재현기·Chrome, 브라스 #C8A15A · 잉크 #E8E2D6, 64→232px)   "
                        "B 우리 카드 설계(안 D 모델: 평면 사전합성 · 획 등급 · 톤3, 같은 프레이밍·같은 팔레트, 바탕 #15181E)", 12, (70, 70, 70))
    label(d, (GAP, 54), "C 인계본 착용 프리뷰(README 오버레이 그대로, 스테이지 zoom, R=%.0fpx)   "
                        "D 우리 몸 부착 설계(출하 배율 0.75 획 실폭, 같은 R=%.0fpx, 밝은 바탕·검은 잉크·재질색)   E 아직 다른 점" % (R_PX, R_PX), 12, (70, 70, 70))
    label(d, (GAP, 72), "★ 오프라인 래스터(둥근 캡)다. 최종 판정은 실제 빌드 캡처로만. 열 D 의 획이 열 C 보다 굵은 것은 그림 오류가 아니라 2pt 하한(머리 지름 = 5.82 W)이다.", 12, (150, 40, 40))
    xA = GAP; xB = xA + CELL + GAP; xC = xB + CELL + GAP; xD = xC + WORN_W + GAP; xE = xD + WORN_W + GAP
    for i, k in enumerate(KINDS):
        y = HEADER + ROW * i
        label(d, (xA, y), "%s  %s / %s" % (M.KO[k], k, M.SLOT[k]), 14, bold=True)
        y0 = y + 22
        im.paste(cards_h[k], (xA, y0))
        im.paste(render_our_card(k, CELL), (xB, y0))
        im.paste(worn_h[k], (xC, y0))
        hc = head_cy if M.SLOT[k] != "BACK" else head_cy
        im.paste(render_our_body(k, WORN_W, WORN_H, hc, R_PX), (xD, y0))
        nc, nb, lost = diff_note(k)
        lines = ["인계본 %d조각 → 카드 %d(전량) · 몸 %d" % (nc, nc, nb),
                 ("몸에서 못 사는 것: " + lost) if lost else "몸에서 못 사는 것: 없음"]
        note = NOTES[k]
        # 줄바꿈: 24자부터 공백/구두점에서, 30자면 강제로
        buf = ""; wrapped = []
        for ch in note:
            buf += ch
            if (len(buf) >= 24 and ch in " ·,.)") or len(buf) >= 30:
                wrapped.append(buf.strip()); buf = ""
        if buf.strip(): wrapped.append(buf.strip())
        RZ.text_block(d, (xE, y0), lines + [""] + wrapped, size=12, color=(50, 50, 50))
        for x, lab in ((xA, "A"), (xB, "B"), (xC, "C"), (xD, "D")):
            label(d, (x + 4, y0 + 2), lab, 12, (200, 200, 200), bold=True)
    return im

def build_truesize(cards58_h, cards58_h2):
    """실제 크기 띠: 카드 58px(1×) · 116px(2×) / 착용 인계본 1×(R 22.12) · 우리 @0.75 1×(R 5.8) · 2×(R 11.6)"""
    r1, r2 = 5.8, 11.6
    cw = 58 + 6 + 116 + 6 + 58 + 6 + 116
    ww = 60 + 6 + 40 + 6 + 60
    colw = cw + 12 + ww + 20
    cols = 2; rows = 8
    W = GAP + cols * colw; H = 70 + rows * 150
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    label(d, (GAP, 8), "실제 크기 — 카드 58pt (1× / 2× Retina) : 인계본 | 우리     착용: 인계본 프리뷰 1×(R 22.12px) | 우리 @0.75 1×(R 5.8px) | 2×(R 11.6px)", 13, bold=True)
    label(d, (GAP, 30), "★ 인계본 프리뷰 머리는 우리 출하 머리의 3.8배다(22.12 vs 5.8 px). 인계본 획 0.109 R 은 우리 출하 크기에서 0.63pt — 2pt 하한이 3.2배로 키운다.", 12, (150, 40, 40))
    worn1, _ = render_handoff_worn(22.12)
    for i, k in enumerate(KINDS):
        cx = GAP + (i % cols) * colw; cy = 60 + (i // cols) * 150
        label(d, (cx, cy), "%s (%s)" % (M.KO[k], k), 12, bold=True)
        y = cy + 18; x = cx
        im.paste(cards58_h[k], (x, y)); x += 58 + 6
        im.paste(cards58_h2[k], (x, y)); x += 116 + 6
        im.paste(render_our_card(k, 58), (x, y)); x += 58 + 6
        im.paste(render_our_card(k, 116), (x, y)); x += 116 + 12
        # 착용
        w1 = worn1[k].crop((WORN_W // 2 - 30, 0, WORN_W // 2 + 30, 120))
        im.paste(w1, (x, y)); x += 60 + 6
        im.paste(render_our_body(k, 40, 120, 30, r1), (x, y)); x += 40 + 6
        im.paste(render_our_body(k, 60, 120, 40, r2), (x, y))
    return im

def build_shared():
    """「좌표 한 벌」 변형 — 카드가 BODY 기하(3/4 재저작)를 그대로 쓰면 어떻게 보이는가. HEAD 4 + EYES 4."""
    ks = [k for k in KINDS if M.SLOT[k] in ("HEAD", "EYES")]
    W = GAP + 3 * (CELL + GAP) + NOTE_W; H = 60 + len(ks) * (CELL + 30)
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    label(d, (GAP, 8), "데이터 계약 판정용 — A 인계본 카드 | B 카드 전용 기하(인계본 정면) | S 「좌표 한 벌」(몸 3/4 기하 + 카드 전용 조각, fit 0.86)", 14, bold=True)
    label(d, (GAP, 30), "S 가 A 처럼 보이면 「한 벌」이 성립한다. 아니면 카드 표면은 정면 기하 변형(surface-variant)이 필요하다. 눈으로 판정.", 12, (70, 70, 70))
    cards = render_handoff_cards(CELL)
    for i, k in enumerate(ks):
        y = 60 + i * (CELL + 30); label(d, (GAP, y), "%s (%s)" % (M.KO[k], k), 13, bold=True); y += 20
        im.paste(cards[k], (GAP, y))
        im.paste(render_our_card(k, CELL), (GAP + CELL + GAP, y))
        im.paste(render_our_card(k, CELL, pieces=M.SHARED[k], fit=True), (GAP + 2 * (CELL + GAP), y))
        nb = sum(1 for p in M.SHARED[k] if p.on(M.CARD)); nbody = sum(1 for p in M.BODY_SET[k] if p.on(M.BODY))
        RZ.text_block(d, (GAP + 3 * (CELL + GAP), y), ["S 조각 %d (몸 %d + 카드전용 %d)" % (nb, nbody, nb - nbody)], 12)
    return im

def main():
    cards = render_handoff_cards(CELL)
    worn, head_cy = render_handoff_worn(R_PX)
    sheet = build_sheet(cards, worn, head_cy)
    sheet.save(os.path.join(HERE, "r15_compare_sheet.png"))
    print("wrote r15_compare_sheet.png %dx%d" % sheet.size)
    c58 = render_handoff_cards(58); c116 = render_handoff_cards(116)
    ts = build_truesize(c58, c116); ts.save(os.path.join(HERE, "r15_compare_truesize.png"))
    print("wrote r15_compare_truesize.png %dx%d" % ts.size)
    sh = build_shared(); sh.save(os.path.join(HERE, "r15_compare_shared.png"))
    print("wrote r15_compare_shared.png %dx%d" % sh.size)

if __name__ == "__main__":
    main()
