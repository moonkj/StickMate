# -*- coding: utf-8 -*-
"""R22 — FX/PET 12종 «카드 ↔ 월드» 실제 크기 비교 시트 (2026-09-06).

합격 판정은 게이트 통과가 아니라 **나란히 놓고 눈으로** 한다(사용자 확정 규약).
그래서 세 열을 같은 자로 그린다:

  [카드]   에셋 icon[] 을 44px 카드 그대로 (획 1.7 캔버스 유닛 = 1.87 px)
  [월드]   지금 몸에 그려지는 한 알 — 배율 0.75, 획 2.00pt, 머리 반경 5.816pt
  [제안]   미완 2건(발자국·커서친구)에 스펙 4-2 좌표를 넣었을 때

각 칸에 머리 원(지름 11.63pt)을 옅게 깔아 **자기 크기**를 함께 보인다 —
「조잡함」은 도형 자체가 아니라 도형 대비 획 두께에서 오기 때문이다.

출력: design/equipment/verify/r22_fxpet_sheet.png
"""
import os, re, math, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import appearance_now as A
from rig import BASELINE_HEAD_R, PT_PER_UNIT, SHIP_SCALE

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "..", "..", "..", "Assets", "_Project", "Resources", "Items")

SS = 3                      # 수퍼샘플 배수
ZOOM = 6                    # 1 pt -> 6 px (사람이 볼 수 있게). 카드 44pt = 264px, 머리 11.63pt = 70px
CELL = 300                  # 칸 크기(px, 확대 후) — 카드가 칸 안에 들어가야 한다
R_PT = BASELINE_HEAD_R * SHIP_SCALE * PT_PER_UNIT      # 5.816 pt
W_PT = 2.00                 # FX/PET 낱선 하한
W_ACC_PT = 1.00             # 인계본 착용 조각 하한(대조용)

BG = (24, 26, 30)
INK = (232, 236, 242)
GHOST = (70, 76, 86)
ACCENT = (120, 175, 240)


def parse_asset(path):
    txt = open(path, encoding="utf-8").read()
    slot = int(re.search(r"^  slot: (\d+)", txt, re.M).group(1))
    idx = int(re.search(r"^  itemIndex: (\d+)", txt, re.M).group(1))
    parts, cur = [], None
    for line in txt.splitlines():
        m = re.match(r"^  - kind: (\d+)", line)
        if m:
            cur = {"kind": int(m.group(1)), "values": [], "tone": 0}
            parts.append(cur)
            continue
        if cur is None:
            continue
        m = re.match(r"^    - (-?[\d.]+)", line)
        if m:
            cur["values"].append(float(m.group(1)))
            continue
        m = re.match(r"^    tone: (\d+)", line)
        if m:
            cur["tone"] = int(m.group(1))
    return os.path.basename(path)[:-6], slot, idx, parts


def rounded_polyline(d, pts, width, color, loop):
    if len(pts) < 2:
        return
    seq = list(pts) + ([pts[0]] if loop else [])
    d.line([tuple(p) for p in seq], fill=color, width=int(round(width)), joint="curve")
    r = width / 2.0
    for p in seq:
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=color)


def draw_card(d, parts, cx, cy, px_per_unit):
    """에셋 icon[]을 40 캔버스 기준으로 그린다. 카드 획 1.7 유닛."""
    w = 1.7 * px_per_unit
    for p in parts:
        col = ACCENT if p["tone"] == 1 else INK
        v = p["values"]
        if p["kind"] in (1, 2, 3):
            X, Y, r = v[0], v[1], v[2]
            X = cx + (X - 20) * px_per_unit
            Y = cy + (Y - 20) * px_per_unit
            rr = r * px_per_unit
            if p["kind"] == 3:
                d.ellipse([X - rr, Y - rr, X + rr, Y + rr], fill=col)
            else:
                d.ellipse([X - rr, Y - rr, X + rr, Y + rr], outline=col, width=max(1, int(round(w))))
            continue
        pts = [(cx + (v[j] - 20) * px_per_unit, cy + (v[j + 1] - 20) * px_per_unit)
               for j in range(0, len(v) - 1, 2)]
        loop = len(pts) > 3 and math.dist(pts[0], pts[-1]) < 1e-6
        if loop:
            pts = pts[:-1]
        rounded_polyline(d, pts, w, col, loop)


def draw_world(d, shapes, cx, cy, px_per_R, stroke_pt=W_PT):
    w = stroke_pt * ZOOM * SS
    for s in shapes:
        col = ACCENT if s.tone == 1 else INK
        pts = [(cx + x * px_per_R, cy - y * px_per_R) for x, y in s.pts]
        if s.filled:
            d.polygon(pts, fill=col)
        else:
            rounded_polyline(d, pts, w, col, s.loop)


def head_ghost(d, cx, cy, px_per_R):
    d.ellipse([cx - px_per_R, cy - px_per_R, cx + px_per_R, cy + px_per_R],
              outline=GHOST, width=max(1, int(round(W_ACC_PT * ZOOM * SS))))


# ── 제안(미완 2건) ────────────────────────────────────────────────────────────
from rig import Shape
PROPOSAL = {
    "발자국": [Shape("Sole", [(-0.40, 0.10), (-0.02, 0.00), (0.56, 0.04)], False)],
    "커서친구": [Shape("Head", [(0, 0), (0, -1.40), (0.364, -1.036), (0.70, -0.896),
                               (1.092, -0.868)], True),
                 Shape("Tail", [(0.364, -1.036), (0.588, -1.484), (0.924, -1.344),
                                (0.70, -0.896)], True, tone=1)],
}

FX_ORDER = ["없음", "발자국", "반짝임", "먼지", "물방울", "나뭇잎"]
PET_ORDER = ["작은공", "종이비행기", "리틀스틱메이트", "커서친구", "풍선", "달팽이"]
FILES = {(5, i): n for i, n in enumerate(
    ["look_fx_none", "look_fx_footprint", "look_fx_sparkle", "look_fx_dust",
     "look_fx_bubble", "look_fx_leaf"])}
FILES.update({(6, i): n for i, n in enumerate(
    ["look_pet_ball", "look_pet_plane", "look_pet_mini", "look_pet_cursor",
     "look_pet_balloon", "look_pet_snail"])})

SHEETS = [("fx", [(5, i, FX_ORDER[i]) for i in range(6)]),
          ("pet", [(6, i, PET_ORDER[i]) for i in range(6)])]
COLS = 3
px_per_R = R_PT * ZOOM * SS                       # 월드 1R -> px
px_per_unit_card = (44.0 / 40.0) * ZOOM * SS      # 카드 1 캔버스 유닛 -> px (카드는 44pt)

for tag, rows in SHEETS:
  Wpx, Hpx = CELL * COLS, CELL * len(rows)
  img = Image.new("RGB", (Wpx * SS, Hpx * SS), BG)
  d = ImageDraw.Draw(img)
  for r, (slot, idx, name) in enumerate(rows):
    cy = int((r + 0.5) * CELL * SS)
    # [0] 카드
    fn = os.path.join(ASSETS, FILES[(slot, idx)] + ".asset")
    _, _, _, parts = parse_asset(fn)
    draw_card(d, parts, int(0.5 * CELL * SS), cy, px_per_unit_card)
    # [1] 월드
    body = (A.FX_NOW if slot == 5 else A.PET_NOW).get(name, [])
    cx1 = int(1.5 * CELL * SS)
    head_ghost(d, cx1, cy, px_per_R)
    if body:
        # 도형의 잉크 중심을 칸 중심에 맞춘다
        xs = [x for s in body for x, _ in s.pts]
        ys = [y for s in body for _, y in s.pts]
        ox, oy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        moved = [Shape(s.name, [(x - ox, y - oy) for x, y in s.pts], s.loop, s.filled, s.tone)
                 for s in body]
        draw_world(d, moved, cx1, cy, px_per_R)
    # [2] 제안
    cx2 = int(2.5 * CELL * SS)
    if name in PROPOSAL:
        head_ghost(d, cx2, cy, px_per_R)
        p = PROPOSAL[name]
        xs = [x for s in p for x, _ in s.pts]
        ys = [y for s in p for _, y in s.pts]
        ox, oy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        moved = [Shape(s.name, [(x - ox, y - oy) for x, y in s.pts], s.loop, s.filled, s.tone)
                 for s in p]
        draw_world(d, moved, cx2, cy, px_per_R)

  for c in range(1, COLS):
    d.line([(c * CELL * SS, 0), (c * CELL * SS, Hpx * SS)], fill=(48, 52, 60), width=SS)
  for r in range(1, len(rows)):
    d.line([(0, r * CELL * SS), (Wpx * SS, r * CELL * SS)], fill=(48, 52, 60), width=SS)

  img = img.resize((Wpx, Hpx), Image.LANCZOS)
  out = os.path.join(HERE, "r22_fxpet_%s.png" % tag)
  img.save(out)
  print("saved", out)
print("열: [카드 44px] · [월드 한 알 @0.75, 획 2.00pt] · [제안(미완 2건만)]")
print("행: FX 없음/발자국/반짝임/먼지/물방울/나뭇잎 · PET 공/비행기/미니/커서/풍선/달팽이")
print("옅은 원 = 머리(지름 %.2f pt), 원의 선 두께 = 인계본 장비 획 1.00pt" % (2 * R_PT))
