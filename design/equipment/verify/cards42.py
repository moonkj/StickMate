# -*- coding: utf-8 -*-
"""42종 카드 폴백 아이콘(Resources/Items/*.asset) ↔ 몸 도형 전수 대조.

이 파일이 답하는 질문은 셋이다.
 1) 이 아이템의 카드에 **실제로 그려지는 것**이 폴백인가 몸 도형인가
    (CharacterInfoWindow.BuildCardArt → AccessoryCardIcon.TryBuild 분기를 그대로 옮겼다).
 2) 폴백과 몸 도형이 **같은 물건으로 읽히는가** (실루엣 프로파일 차 — 30종 하니스와 같은 자).
 3) 폴백이 40 viewBox 안에서 카드 규칙(획 1.7)을 지키는가.

좌표계: 폴백 = 40×40 viewBox(원점 좌상단, y 아래로). 몸 = 머리 반경 R 배수(원점 머리 중심, y 위로).
"""
import sys, os, math, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, appearance
from rig import Shape

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                      "../../../Assets/_Project/Resources/Items")

# ── 카드 규약 (CharacterInfoWindow / AccessoryCardIcon에서 그대로) ────────────
VIEW      = 40.0      # AccessoryDefSO 주석: "40×40 viewBox"
ICON      = 44.0      # CharacterInfoWindow.IconSize
STROKE_C  = 1.7 * ICON / VIEW      # IconStroke = 1.87 캔버스 유닛
STROKE_V  = 1.7                    # 같은 획을 viewBox 단위로 본 값(=STROKE_C * VIEW/ICON)
FIT       = 0.86                   # AccessoryCardIcon.FitFraction
FITBOX    = VIEW * FIT             # 34.4 — 몸 도형이 viewBox 안에서 차지하는 정사각

SLOT_NAME = {0:"HEAD",1:"EYES",2:"NECK",3:"BACK",4:"HAIR",5:"FX",6:"PET"}
# AccessoryShapeBuilder.Append의 switch가 아는 자리 = 카드가 몸 도형으로 그려지는 자리
BODY_SLOTS = {0,1,2,3,4}

# ── .asset 파서 ─────────────────────────────────────────────────────────────
def parse_asset(path):
    txt = open(path, encoding="utf-8").read()
    slot = int(re.search(r"^  slot: (\d+)", txt, re.M).group(1))
    idx  = int(re.search(r"^  itemIndex: (\d+)", txt, re.M).group(1))
    name = os.path.basename(path)[:-6]
    parts, cur = [], None
    for line in txt.splitlines():
        m = re.match(r"^  - kind: (\d+)", line)
        if m:
            cur = {"kind": int(m.group(1)), "values": [], "tone": 0}
            parts.append(cur); continue
        if cur is None: continue
        m = re.match(r"^    - (-?[\d.]+)", line)
        if m: cur["values"].append(float(m.group(1))); continue
        m = re.match(r"^    tone: (\d+)", line)
        if m: cur["tone"] = int(m.group(1))
    return name, slot, idx, parts

# ★ 2026-09-02: ItemIconPartKind에 Polygon(4)이 생겼다 — 폴백도 <b>채운 다각형</b>을 표현한다.
#   이 표에 없는 kind는 fallback_shapes가 통째로 버리므로, 빠뜨리면 실루엣 비교가 조용히 거짓말한다.
KIND = {0:"Polyline", 1:"Ring", 2:"DashedRing", 3:"Dot", 4:"Polygon"}

def ngon(cx, cy, r, n=24):
    return [(cx + math.cos(2*math.pi*i/n)*r, cy + math.sin(2*math.pi*i/n)*r) for i in range(n)]

def fallback_shapes(parts):
    """폴백 조각 → Shape(뷰박스 좌표, y는 아래로인 채 그대로 둔다 — 상하 반전은 비교에 영향 없다)."""
    out = []
    for i, p in enumerate(parts):
        v, k = p["values"], p["kind"]
        if k in (0, 4):
            pts = [(v[j], v[j+1]) for j in range(0, len(v)-1, 2)]
            loop = len(pts) > 3 and abs(pts[0][0]-pts[-1][0]) < 1e-6 and abs(pts[0][1]-pts[-1][1]) < 1e-6
            if loop: pts = pts[:-1]
            out.append(Shape("p%d"%i, pts, loop=loop or k == 4, filled=(k == 4), tone=p["tone"]))
        elif k in (1, 2):
            out.append(Shape("p%d"%i, ngon(v[0], v[1], v[2]), loop=True, filled=False, tone=p["tone"]))
        elif k == 3:
            out.append(Shape("p%d"%i, ngon(v[0], v[1], max(v[2], 0.35)), loop=True, filled=True, tone=p["tone"]))
    return out

# ── 몸 도형 → viewBox 투영 (AccessoryCardIcon.TryBuild와 같은 식) ─────────────
def to_viewbox(shapes):
    """몸 도형(R 단위) → 40 viewBox. 44는 약분돼 사라진다: (p−c)·(44·0.86/span)·(40/44)."""
    pts = [q for s in shapes for q in s.pts]
    if not pts: return []
    x0, y0, x1, y1 = rig.bounds(pts)
    span = max(x1-x0, y1-y0)
    k = FITBOX / span
    cx, cy = (x0+x1)/2.0, (y0+y1)/2.0
    return [Shape(s.name, [((x-cx)*k + VIEW/2.0, VIEW/2.0 - (y-cy)*k) for x, y in s.pts],
                  loop=s.loop, filled=s.filled, tone=s.tone) for s in shapes]

def refit(shapes):
    """제 잉크 사각형을 FITBOX에 맞춰 다시 담는다 — '크기/위치 말고 **형태**가 같은가'를 물을 때."""
    pts = [q for s in shapes for q in s.pts]
    if not pts: return []
    x0, y0, x1, y1 = rig.bounds(pts)
    span = max(x1-x0, y1-y0)
    if span < 1e-9: return []
    k = FITBOX / span
    cx, cy = (x0+x1)/2.0, (y0+y1)/2.0
    return [Shape(s.name, [((x-cx)*k + VIEW/2.0, (y-cy)*k + VIEW/2.0) for x, y in s.pts],
                  loop=s.loop, filled=s.filled, tone=s.tone) for s in shapes]

def profile_at_center(shapes):
    return rig.profile([Shape(s.name, [(x-VIEW/2.0, y-VIEW/2.0) for x, y in s.pts],
                              loop=s.loop, filled=s.filled) for s in shapes])

# ── 몸 도형 표 ───────────────────────────────────────────────────────────────
BODY = {}
for cat, table in (("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
                   ("BACK", items.BACK), ("HAIR", hair.SET)):
    for i, (nm, sh) in enumerate(table.items()):
        BODY[(cat, i)] = (nm, sh)
for i, (nm, sh) in enumerate(appearance.FX_NOW.items()):  BODY[("FX", i)]  = (nm, sh)
for i, (nm, sh) in enumerate(appearance.PET_NOW.items()):
    # PET 카탈로그 순서: 0 작은공 / 1 종이비행기 / 2 리틀스틱메이트 / 3 커서친구 / 4 풍선 / 5 달팽이
    pass
PET_ORDER = ["작은공", "종이비행기", "리틀스틱메이트", "커서친구", "풍선", "달팽이"]
for i, nm in enumerate(PET_ORDER):
    BODY[("PET", i)] = (nm, appearance.PET_NOW.get(nm, []))

def true_min_edge(s):
    n = len(s.pts); best = None
    for i in range(n if s.loop else n-1):
        L = math.dist(s.pts[i], s.pts[(i+1) % n])
        if L < 1e-9: continue
        best = L if best is None else min(best, L)
    return best

# ── 본론 ────────────────────────────────────────────────────────────────────
rows = []
for f in sorted(os.listdir(ASSETS)):
    if not f.endswith(".asset"): continue
    name, slot, idx, parts = parse_asset(os.path.join(ASSETS, f))
    cat = SLOT_NAME[slot]
    live = slot not in BODY_SLOTS            # 카드에 실제로 그려지는가
    fb = fallback_shapes(parts)
    body_nm, body = BODY.get((cat, idx), ("?", []))
    rows.append(dict(file=f, name=name, cat=cat, idx=idx, live=live,
                     parts=parts, fb=fb, body_nm=body_nm, body=body))

print("╔══ 42종 카드 폴백 ↔ 몸 도형 전수 대조 ══╗")
print("  카드 44px · 획 %.2f 캔버스 유닛(= viewBox %.1f) · 몸 도형은 FIT %.2f로 담긴다"
      % (STROKE_C, STROKE_V, FIT))
print()
print("%-30s %-5s %-6s %-9s %-9s %-7s %s" %
      ("파일", "자리", "카드", "폴백조각", "몸조각", "보조색", "판정"))
print("-"*104)

defects, ok_simplify, live_items = [], [], []
for r in rows:
    fb_n, body_n = len(r["fb"]), len(r["body"])
    acc_fb = sum(1 for s in r["fb"] if s.tone == 1)
    acc_bd = sum(1 for s in r["body"] if s.tone == 1)
    drawn = "폴백" if r["live"] else "몸"
    verdict = ""
    if not r["live"]:
        verdict = "폴백 미사용(사문)"
    else:
        verdict = "폴백이 유일한 그림"
        live_items.append(r)
    print("%-30s %-5s %-6s %-9s %-9s %-7s %s" %
          (r["file"][:-6], r["cat"], drawn, "%d개" % fb_n,
           "%d개" % body_n if body_n else "—",
           "%d/%d" % (acc_fb, acc_bd), verdict))
print()

# ── (2) 같은 물건으로 읽히는가 — 실루엣 프로파일 차 ──────────────────────────
print("── 폴백 vs 몸: 같은 물건으로 읽히는가 (viewBox 단위, 획 %.1f) ──" % STROKE_V)
print("%-30s %-12s %-12s %s" % ("아이템", "그린 그대로", "형태만(재담기)", "판정"))
print("-"*104)
for r in rows:
    if not r["body"]: 
        continue
    body_v = to_viewbox(r["body"])
    d_as = rig.max_delta(profile_at_center(body_v), profile_at_center(r["fb"])) / STROKE_V
    # ★ 2026-09-02 정정 — 예전엔 refit(몸)을 <b>R 공간(y 위로)</b> 그대로 넣고 폴백은
    #   viewBox 공간(y 아래로)이라, 이 열이 늘 <b>상하 뒤집힌 쌍</b>을 재고 있었다(모든 항목이
    #   "다른 물건"으로 나오던 원인). 몸도 같은 공간으로 옮긴 뒤 다시 담는다.
    d_sh = rig.max_delta(profile_at_center(refit(to_viewbox(r["body"]))),
                         profile_at_center(refit(r["fb"]))) / STROKE_V
    tag = "같은 물건" if d_sh < 1.0 else ("다른 물건" if d_sh >= 2.0 else "경계")
    if r["live"]:
        tag += " ★카드에 보임"
    print("%-30s %-12s %-12s %s" % (r["name"], "%.2f획" % d_as, "%.2f획" % d_sh, tag))

# ── 눈금 교정 — 이 지표에서 '다른 물건'이 대체 얼마인가 ────────────────────────
print()
print("── 눈금 교정: 같은 카테고리 안 **서로 다른 6종**을 같은 자로 잰 값 ──")
print("   (둘 다 34.4 상자에 다시 담고 중심에서 프로파일 → 획 1.7로 나눔)")
CAL = [("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
       ("BACK", items.BACK), ("HAIR", hair.SET),
       ("FX",  {k: v for k, v in appearance.FX_NOW.items() if v}),
       ("PET", {k: v for k, v in appearance.PET_NOW.items() if v})]
for cat, table in CAL:
    ks = list(table); pr = {k: profile_at_center(refit(table[k])) for k in ks}
    ds = []
    for i in range(len(ks)):
        for j in range(i+1, len(ks)):
            ds.append((rig.max_delta(pr[ks[i]], pr[ks[j]]) / STROKE_V, ks[i], ks[j]))
    ds.sort()
    print("   %-5s 서로 다른 두 아이템 차: 최소 %.2f획(%s vs %s) · 중앙값 %.2f획 · 최대 %.2f획"
          % (cat, ds[0][0], ds[0][1], ds[0][2], ds[len(ds)//2][0], ds[-1][0]))
