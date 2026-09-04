# -*- coding: utf-8 -*-
"""인계본 아이템 아트 16종 파서 + 좌표계 변환식 (design-equipment, 2026-09-03).

출처: /Users/kjmoon/Downloads/design_handoff_equipment_window/reference/ItemIcon.dc.html
      (사본: docs/handoff/design_handoff_equipment_window/)

★ 좌표를 손으로 베끼지 않는다. HTML의 K = { ... } 블록을 직접 파싱한다.
  (손으로 옮긴 좌표표는 원본이 바뀌는 순간 조용히 거짓 초록을 낸다 — cards42.py가 같은 이유로
   .asset 직접 파싱으로 바뀌었다.)

좌표계
  인계본 아이콘 : viewBox 64x64, 원점 좌상단, y 아래로 증가, 획 stroke=2.2
  인계본 무대   : viewBox 200x240, 머리 circle(100,46) r=28, 획 6
  우리          : 머리 반경 R 배수, 원점 머리 중심, y 위로, +x 진행 방향
"""
import math, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
from rig import Shape

# ── 인계본 원본 위치 (사본 우선, 없으면 Downloads) ─────────────────────────────
_HERE = os.path.dirname(os.path.abspath(__file__))
_CANDIDATES = [
    os.path.join(_HERE, "../../../docs/handoff/design_handoff_equipment_window/reference/ItemIcon.dc.html"),
    os.path.expanduser("~/Downloads/design_handoff_equipment_window/reference/ItemIcon.dc.html"),
]

def source_path():
    for p in _CANDIDATES:
        if os.path.exists(p):
            return os.path.abspath(p)
    raise SystemExit("인계본 ItemIcon.dc.html 을 찾을 수 없다: %s" % _CANDIDATES)

# ============================================================================
# 1. 파서 — K = { kind: () => [ B(...), S(...), ... ] } 블록을 읽는다
# ============================================================================
CALLS = ("B", "S", "F", "H", "CB", "CF", "RB")

def _split_top(s):
    """괄호/중괄호/따옴표 깊이를 세며 최상위 콤마로 자른다."""
    out, buf, depth, q = [], [], 0, None
    for ch in s:
        if q:
            buf.append(ch)
            if ch == q: q = None
            continue
        if ch in "'\"":
            q = ch; buf.append(ch); continue
        if ch in "([{": depth += 1
        elif ch in ")]}": depth -= 1
        if ch == "," and depth == 0:
            out.append("".join(buf)); buf = []
        else:
            buf.append(ch)
    if "".join(buf).strip(): out.append("".join(buf))
    return [x.strip() for x in out]

def _parse_opts(txt):
    """{ fill: A, fillOpacity: 0.22, strokeWidth: w * 0.8 } → dict(문자열 값)."""
    txt = txt.strip()
    if not txt.startswith("{"): return {}
    inner = txt[1:-1]
    d = {}
    for part in _split_top(inner):
        if ":" not in part: continue
        k, v = part.split(":", 1)
        d[k.strip()] = v.strip()
    return d

def parse_items(path=None):
    """{kind: [ (call, args:list[str], opts:dict) ]}"""
    txt = open(path or source_path(), encoding="utf-8").read()
    i = txt.index("const K = {")
    # 중괄호 깊이로 K 블록 끝을 찾는다
    j = txt.index("{", i); depth = 0
    for k in range(j, len(txt)):
        if txt[k] == "{": depth += 1
        elif txt[k] == "}":
            depth -= 1
            if depth == 0: end = k; break
    body = txt[j + 1:end]

    items = {}
    for m in re.finditer(r"(\w+):\s*\(\)\s*=>\s*\[", body):
        name = m.group(1)
        s = m.end() - 1
        depth = 0
        for k in range(s, len(body)):
            if body[k] == "[": depth += 1
            elif body[k] == "]":
                depth -= 1
                if depth == 0: e = k; break
        pieces = []
        for call in _split_top(body[s + 1:e]):
            cm = re.match(r"^(CB|CF|RB|B|S|F|H)\s*\(", call)
            if not cm: continue
            fn = cm.group(1)
            args = _split_top(call[cm.end():call.rindex(")")])
            opts = {}
            if args and args[-1].strip().startswith("{"):
                opts = _parse_opts(args.pop())
            pieces.append((fn, args, opts))
        items[name] = pieces
    return items

# ============================================================================
# 2. SVG path → 점열 (2차 베지에 평탄화)
# ============================================================================
def flatten_path(d, seg=16):
    """M/L/H/V/Q/Z 만 쓰인다(인계본 실측). 반환 (점열, 닫힘여부)."""
    toks = re.findall(r"[MLHVQZmlhvqz]|-?\d*\.?\d+", d)
    pts, i, cur, start, closed = [], 0, (0.0, 0.0), (0.0, 0.0), False
    cmd = None
    while i < len(toks):
        t = toks[i]
        if t.isalpha():
            cmd = t; i += 1
            if cmd in "Zz":
                closed = True
                continue
        n = lambda k: float(toks[i + k])
        if cmd in "Mm":
            cur = (n(0), n(1)); start = cur; pts.append(cur); i += 2
        elif cmd in "Ll":
            cur = (n(0), n(1)); pts.append(cur); i += 2
        elif cmd in "Hh":
            cur = (n(0), cur[1]); pts.append(cur); i += 1
        elif cmd in "Vv":
            cur = (cur[0], n(0)); pts.append(cur); i += 1
        elif cmd in "Qq":
            c = (n(0), n(1)); p = (n(2), n(3))
            for s in range(1, seg + 1):
                u = s / seg; v = 1 - u
                pts.append((v * v * cur[0] + 2 * u * v * c[0] + u * u * p[0],
                            v * v * cur[1] + 2 * u * v * c[1] + u * u * p[1]))
            cur = p; i += 4
        else:
            raise ValueError("미지원 명령 %r in %r" % (cmd, d))
    return pts, closed

def circle_pts(cx, cy, r, n=24):
    return [(cx + math.cos(2 * math.pi * i / n) * r, cy + math.sin(2 * math.pi * i / n) * r)
            for i in range(n)]

def rect_pts(x, y, w, h, rr, n=6):
    """둥근 사각. 모서리마다 n분할."""
    rr = min(rr, w / 2, h / 2)
    out = []
    corners = [(x + w - rr, y + rr, -90, 0), (x + w - rr, y + h - rr, 0, 90),
               (x + rr, y + h - rr, 90, 180), (x + rr, y + rr, 180, 270)]
    for cx, cy, a0, a1 in corners:
        for k in range(n + 1):
            a = math.radians(a0 + (a1 - a0) * k / n)
            out.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return out

# ============================================================================
# 3. 조각 → 기하 + 역할
#    B  = 채움(등급색 그라디언트) + 아이템색 외곽선
#    S  = 낱선(아이템색)
#    F  = 강조색 채움 fillOpacity 0.55 (외곽선 없음)
#    H  = 흰색 하이라이트 opacity .42, 폭 w*0.75, 좌상단 고정
#    CB = 원(채움+외곽선)  CF = 강조원(채움만)  RB = 둥근사각(채움+외곽선)
# ============================================================================
STROKE = 2.2          # 인계본 기본 획 (viewBox 64 단위)

def _num(s):
    """'w * 0.8' 같은 식을 STROKE 기준으로 평가. 그 외는 float."""
    s = s.strip()
    try: return float(s)
    except ValueError: pass
    return eval(s, {"__builtins__": {}}, {"w": STROKE})

class Piece:
    __slots__ = ("call", "pts", "closed", "opts", "d")
    def __init__(self, call, pts, closed, opts, d=None):
        self.call, self.pts, self.closed, self.opts, self.d = call, pts, closed, opts, d

    # ── 역할 판정 ──
    @property
    def filled(self):   return self.call in ("B", "F", "CB", "CF", "RB")
    @property
    def outlined(self): return self.call in ("B", "CB", "RB", "S", "H")
    @property
    def is_accent(self):
        """강조색(A)을 쓰는가 = 우리 tone==Accent 후보."""
        if self.call in ("F", "CF"): return True
        return self.opts.get("fill", "").strip() == "A"
    @property
    def is_highlight(self): return self.call == "H"
    @property
    def stroke_w(self):
        if self.call == "H": return STROKE * 0.75
        return _num(self.opts.get("strokeWidth", str(STROKE)))
    @property
    def fill_opacity(self):
        """이 조각의 채움 알파(그라디언트 채움은 위 0.34 → 아래 0.08)."""
        if not self.filled: return None
        if "fillOpacity" in self.opts: return _num(self.opts["fillOpacity"])
        if self.call == "F": return 0.55
        if self.call == "CF": return 1.0
        return None   # 그라디언트 (0.34 → 0.08 / 망토류 1.00 → 0.62)

def build(kind, pieces):
    out = []
    for fn, args, opts in pieces:
        if fn in ("B", "S", "F", "H"):
            d = args[0].strip().strip("'\"")
            pts, closed = flatten_path(d)
            out.append(Piece(fn, pts, closed or fn in ("B", "F"), opts, d))
        elif fn in ("CB", "CF"):
            cx, cy, r = (_num(a) for a in args[:3])
            out.append(Piece(fn, circle_pts(cx, cy, r), True, opts,
                             "circle(%g,%g,%g)" % (cx, cy, r)))
        elif fn == "RB":
            x, y, w, h, r = (_num(a) for a in args[:5])
            out.append(Piece(fn, rect_pts(x, y, w, h, r), True, opts,
                             "rect(%g,%g,%g,%g,r%g)" % (x, y, w, h, r)))
    return out

# ============================================================================
# 4. 좌표계 변환 — 인계본이 스스로 선언한 값에서만 유도한다
# ============================================================================
# 무대(README '졸라맨 캐릭터'): viewBox 200x240, 머리 circle(100,46) r=28, 렌더 폭 158px,
#   스테이지 상단에서 26px.
STAGE_VB_W   = 200.0
STAGE_HEAD_CX, STAGE_HEAD_CY, STAGE_HEAD_R = 100.0, 46.0, 28.0
STAGE_RENDER_W = 158.0
STAGE_TOP_PX   = 26.0
K_PX = STAGE_RENDER_W / STAGE_VB_W        # 0.79 px per stage unit
HEAD_R_PX      = STAGE_HEAD_R * K_PX      # 22.12 px
HEAD_CY_PX     = STAGE_TOP_PX + STAGE_HEAD_CY * K_PX   # 62.34 px (스테이지 상단 기준)

# README '장비 오버레이 위치' — (박스 한 변 px, 스테이지 top px)
SLOT_BOX = {
    "head":  (70.0,  6.0),
    "eyes":  (48.0, 38.0),
    "neck":  (54.0, 78.0),
    "back":  (88.0, 72.0),
}
ITEM_SLOT = {
    "clothhat": "head", "furhat": "head", "fedora": "head", "crown": "head",
    "sunglasses": "eyes", "roundglasses": "eyes", "goggles": "eyes", "monocle": "eyes",
    "bowtie": "neck", "stripedtie": "neck", "scarf": "neck", "bellnecklace": "neck",
    "shortcape": "back", "longcape": "back", "wings": "back", "backpack": "back",
}
# 우리 30종 이름 대응 (리더 확인분)
OUR_NAME = {
    "clothhat": ("HEAD", "야구모자"), "furhat": ("HEAD", "털모자"),
    "fedora": ("HEAD", "중절모"), "crown": ("HEAD", "왕관"),
    "sunglasses": ("EYES", "선글라스"), "roundglasses": ("EYES", "동그란안경"),
    "goggles": ("EYES", "고글"), "monocle": ("EYES", "외알안경"),
    "bowtie": ("NECK", "나비넥타이"), "stripedtie": ("NECK", "줄무늬타이"),
    "scarf": ("NECK", "목도리"), "bellnecklace": ("NECK", "방울목걸이"),
    "shortcape": ("BACK", "짧은망토"), "longcape": ("BACK", "긴망토"),
    "wings": ("BACK", "날개"), "backpack": ("BACK", "배낭"),
}

def icon_to_R(kind):
    """아이콘 viewBox 64 좌표 → 우리 R 좌표(머리 중심 원점, y 위로).
    반환 (fx, fy) 두 함수. 인계본이 선언한 박스 크기/위치에서만 유도한다."""
    box, top = SLOT_BOX[ITEM_SLOT[kind]]
    u = box / 64.0                              # px per icon unit
    sx = u / HEAD_R_PX                          # R per icon unit
    def fx(x): return (x - 32.0) * sx
    def fy(y): return (HEAD_CY_PX - (top + y * u)) / HEAD_R_PX
    return fx, fy, sx

def stage_to_R(x, y):
    """무대 viewBox 200x240 → R 좌표."""
    return ((x - STAGE_HEAD_CX) / STAGE_HEAD_R, (STAGE_HEAD_CY - y) / STAGE_HEAD_R)

def to_R_shape(kind, piece, name):
    fx, fy, _ = icon_to_R(kind)
    pts = [(fx(x), fy(y)) for x, y in piece.pts]
    return Shape(name, pts, loop=piece.closed, filled=piece.filled,
                 tone=1 if piece.is_accent else 0)

def stroke_R(kind):
    """인계본 획 2.2 아이콘단위를 R로. 우리 W(0.343864 R)와 비교하는 값."""
    _, _, sx = icon_to_R(kind)
    return STROKE * sx

# ============================================================================
if __name__ == "__main__":
    items = parse_items()
    print("파싱 %d종: %s" % (len(items), " ".join(items)))
    for k, v in items.items():
        print("  %-13s %d조각  %s" % (k, len(v), " ".join(f for f, _, _ in v)))
