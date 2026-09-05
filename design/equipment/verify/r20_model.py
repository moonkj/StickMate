# -*- coding: utf-8 -*-
"""R20 — 나머지 26종 전수 대조 · 격차 등급 · (가)군 좌표 (design-equipment, 2026-09-05)

인계본 16종의 조형 규칙(R15~R19 확정)에 비추어 v1 경로(ResolveToneColor)의 옛 도형 26종이 무엇이 미달인지 재고,
(가) 규칙만 적용하면 되는 것 / (나) 조형을 다시 그려야 하는 것 / (다) 인계본에 대응물이 없어 새로 창작해야 하는 것으로 나눈다.
(가)군은 이 파일이 좌표(층·색 역할 포함)까지 낸다. 현행 도형은 거울(items.py · hair.py · appearance.py)에서 읽는다 —
프로덕션 AccessoryShapeBuilder.cs 는 coder 가 쓰고 있어 읽지도 않았다(거울은 mirrordrift 로 잠긴 것, §12-5 참고).

규칙 목록(자)
  P  조각 수 4~9 · 역할(본체·강조·독립선·하이라이트)          S  획 배수 연속(0.7~1.5) + 1pt 하한
  H  하이라이트 흰 α0.42 ×0.75 1개                              C  채움 M/M2 불투명 + 잉크 윤곽 · 독립선 재질색 · 등급색 0
  G  정면 아이콘 기하(64u 슬롯)                                    L  H-1 2층 · H-2 착용선 폭 ≥ 머리 폭/꼭대기 < 2.551 · N-1 · E-1 · B-1~B-4
"""
import math, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, items, hair, appearance
import r16_model as M16
import r17_model as M17
import r19_model as M19
import palette_model as PM

SHIP = M16.SHIP
SH = rig.SHOULDER_R
HEAD_R_COVER, HEAD_R_SHIP = M17.HEAD_R_COVER, M17.HEAD_R_SHIP

# ============================================================================
# 1. 26종 목록 (id · 한글 · 슬롯 · 거울 키 · 거울 모듈)
# ============================================================================
REST = [
    ("equip.head.beret", "베레모", "HEAD", "베레모", "items"), ("equip.head.straw", "밀짚모자", "HEAD", "밀짚모자", "items"),
    ("equip.eyes.browline", "뿔테안경", "EYES", "뿔테안경", "items"), ("equip.eyes.patch", "안대", "EYES", "안대", "items"),
    ("equip.neck.pendant", "펜던트", "NECK", "펜던트", "items"), ("equip.neck.bandana", "반다나", "NECK", "반다나", "items"),
    ("equip.shoulders.poncho", "판초", "BACK", "판초", "items"), ("equip.shoulders.fairy_wings", "요정날개", "BACK", "요정날개", "items"),
    ("look.hair.cowlick", "삐친머리", "HAIR", "삐친머리", "hair"), ("look.hair.neat", "단정한머리", "HAIR", "단정한머리", "hair"),
    ("look.hair.curly", "곱슬", "HAIR", "곱슬머리", "hair"), ("look.hair.bald", "민머리", "HAIR", "민머리", "hair"),
    ("look.hair.bowl", "바가지머리", "HAIR", "바가지머리", "hair"), ("look.hair.ponytail", "포니테일", "HAIR", "포니테일", "hair"),
    ("look.fx.none", "없음", "FX", None, None), ("look.fx.footprint", "발자국", "FX", "발자국", "fx"), ("look.fx.sparkle", "반짝임", "FX", "반짝임", "fx"),
    ("look.fx.dust", "먼지구름", "FX", "먼지", "fx"), ("look.fx.bubble", "물방울", "FX", "물방울", "fx"), ("look.fx.leaf", "나뭇잎", "FX", "나뭇잎", "fx"),
    ("look.pet.ball", "작은공", "PET", "작은공", "pet"), ("look.pet.plane", "종이비행기", "PET", "종이비행기", "pet"),
    ("look.pet.mini", "리틀스틱메이트", "PET", "리틀스틱메이트", "pet"), ("look.pet.cursor", "커서친구", "PET", "커서친구", "pet"),
    ("look.pet.balloon", "풍선", "PET", "풍선", "pet"), ("look.pet.snail", "달팽이", "PET", "달팽이", "pet"),
]
assert len(REST) == 26
PAL = {it[0]: it for it in PM.ITEMS}
SETS = {"items": {**items.HEAD, **items.EYES, **items.NECK, **items.BACK}, "hair": hair.SET}

def mirror_shapes(entry):
    _, _, slot, key, mod = entry
    if mod is None: return []
    if mod in ("fx", "pet"):
        d = getattr(appearance, "FX_V2", None) or getattr(appearance, "FX", None)
        src = None
        for name in ("FX_V2", "PET_V2", "FX", "PET", "SET_V2", "SET"):
            s = getattr(appearance, name, None)
            if isinstance(s, dict) and key in s: src = s; break
        if src is None:
            for name in dir(appearance):
                s = getattr(appearance, name)
                if isinstance(s, dict) and key in s: src = s; break
        return list(src[key]) if src else []
    return list(SETS[mod][key])

# ============================================================================
# 2. 감사 — 조각·획·하이라이트·색·기하·층
# ============================================================================
def asym_index(shapes):
    pts = [q for s in shapes for q in s.pts]
    if not pts: return float("nan")
    x0, _, x1, _ = rig.bounds(pts)
    return abs(x0 + x1) / max(1e-9, x1 - x0)

def audit(entry):
    iid, ko, slot, key, mod = entry
    sh = mirror_shapes(entry)
    n = len(sh); nf = sum(1 for s in sh if s.filled); tones = sorted({s.tone for s in sh})
    it = PAL[iid]
    a = asym_index(sh)
    verdict = {}
    verdict["P"] = "%d조각(%s)" % (n, "인계본 4~9 미달" if n < 4 else "안") if n else "거울 없음"
    verdict["S"] = "전부 ×1.0(낱선 0.344R/채움윤곽 0.218R, 2pt 하한) — 배수 연속·1pt 하한 미적용"
    verdict["H"] = "없음(0/1)"
    verdict["C"] = "M %s M2 %s 있음 · v1 윤곽 = 채움×0.28(잉크 아님) · 독립선 톤색" % (it[4], it[5])
    verdict["G"] = ("정면(비대칭 %.2f)" % a if a < 0.15 else "3/4 재저작(비대칭 %.2f)" % a) if sh else "-"
    L = []
    if slot == "HEAD":
        L.append("H-1 2층 없음")
        f = fit_head(sh) if sh else None
        if f: L.append("H-2 현행 착용선 %s → 맞춤 필요(u %.2f dy %+.2f 착용선 %+.2f 꼭대기 %+.2f)" % (f["cur"], f["u"], f["dy"], f["wear"], f["top"]))
    if slot == "NECK":
        top = max(q[1] for s in sh for q in s.pts)
        anchor = [s for s in sh if s.name in ("Chain", "BandanaWrap")][0]; atop = max(q[1] for q in anchor.pts)
        L.append("N-1 착용선 %+.3f(어깨선 %+.3f · 머리 원반 밑 %+.3f) → %s" % (atop, atop - SH, atop + HEAD_R_SHIP, "통과" if (atop <= -HEAD_R_SHIP - 0.01 and abs(atop - SH) <= 0.20) else "★ 위반"))
    if slot == "EYES":
        if key == "안대":
            cov = [s for s in sh if s.name == "PatchCover"][0]; cx = sum(q[0] for q in cov.pts) / len(cov.pts)
            L.append("E-1 가리개 x %+.2f(%s) · 드러난 눈 r 0.33 @−0.62 → 규칙 r 0.172 @(−0.46,+0.107)" % (cx, "오른쪽 ✓" if cx > 0 else "왼쪽 ✗"))
        else:
            L.append("E-1 해당 없음(양안) · 렌즈 채움 불투명(동그란안경 규칙과 다름 — 뿔테는 투명 렌즈 후보)")
    if slot == "BACK":
        L.append("B-4 " + ("판초 = 망토 문법(무대 뒤판+칼라) 아님, 3/4 CapeOutline+주름+요크" if key == "판초" else "요정날개 = 날개와 같은 관점(깃 얇음) ✓") + " · B-1 길이 %s" % ("hem %.2f R" % min(q[1] for s in sh for q in s.pts)))
    if slot == "HAIR": L.append("모자 커버선(HatCoverLocalY)으로 잘린다 · 인계본에 머리카락 없음")
    if slot in ("FX", "PET"): L.append("착용 도형 아님(효과/독립 개체) · 카드 문법만 해당")
    verdict["L"] = " · ".join(L) if L else "-"
    return dict(id=iid, ko=ko, slot=slot, n=n, nf=nf, tones=tones, asym=a, verdict=verdict, m=it[4], m2=it[5])

# ---- H-2 맞춤(R 좌표 다각형용) — M17.evaluate 와 같은 규칙, 입력만 R 다각형 ----
def _central_hw_R(polys, ys):
    out = np.zeros(len(ys))
    for j, y in enumerate(ys):
        iv = []
        for poly in polys: iv += M17._scan_intervals(poly, y)
        if not iv: continue
        iv.sort(); merged = [list(iv[0])]
        for a, b in iv[1:]:
            if a <= merged[-1][1] + 1e-9: merged[-1][1] = max(merged[-1][1], b)
            else: merged.append([a, b])
        for a, b in merged:
            if a <= 0.0 <= b: out[j] = min(-a, b); break
    return out

def fit_head(shapes, w_max=0.30):
    """현행 R 다각형(머리 중심 원점)을 균일 배율 u · 세로 dy 로 맞춘다: y' = dy + y·u, x' = x·u."""
    polys = [s.pts for s in shapes if s.filled]
    ys = np.arange(-1.5, 3.0, 0.02); chw = _central_hw_R(polys, ys)
    pts = [q for s in shapes for q in s.pts]; y0, y1 = min(q[1] for q in pts), max(q[1] for q in pts)
    # 현행 착용선(u=1, dy=0)
    def evaluate(u, dy):
        yR = dy + ys * u
        head_hw = np.sqrt(np.clip(HEAD_R_COVER ** 2 - yR ** 2, 0, None))
        need = (yR <= HEAD_R_COVER) & (yR >= -HEAD_R_COVER)
        ok = (chw * u >= head_hw + M17.COVER_MARGIN) | ~need
        order = np.argsort(-yR); wear = None; started = False
        for j in order:
            if yR[j] > HEAD_R_COVER: continue
            if not ok[j]: break
            wear = yR[j]; started = True
        covered = started and max(yR[(yR <= HEAD_R_COVER) & ok]) >= HEAD_R_COVER - 0.3
        top = dy + y1 * u; bottom = dy + y0 * u
        why = []
        if not covered: why.append("덮임 실패")
        elif wear > w_max: why.append("착용선 %.2f > %.2f" % (wear, w_max))
        if top >= M17.TOP_LIMIT: why.append("꼭대기 %.2f" % top)
        if bottom <= M17.CHIN: why.append("밑 %.2f" % bottom)
        return dict(u=u, dy=dy, wear=wear, top=top, bottom=bottom, ok=not why, why=" · ".join(why))
    cur = evaluate(1.0, 0.0)
    best = None
    for u in np.arange(1.0, 2.2, 0.01):
        cands = [evaluate(u, dy) for dy in np.arange(-1.0, 1.5, 0.02)]
        cands = [e for e in cands if e["ok"]]
        if cands: best = min(cands, key=lambda e: abs(e["wear"] - w_max)); break
    if best is None: return dict(cur=cur["wear"], u=float("nan"), dy=float("nan"), wear=float("nan"), top=float("nan"))
    best["cur"] = ("%+.2f" % cur["wear"]) if cur["wear"] is not None else "없음(덮임 실패)"
    return best

# ============================================================================
# 3. 격차 등급
# ============================================================================
GRADE = {
    "equip.eyes.patch": ("가", "가리개 이미 오른쪽 · 정면 기하 · E-1 눈 크기/위치와 C 규칙·하이라이트만"),
    "equip.head.straw": ("나", "★ 첫 판 (가)로 봤으나 시트에서 기각: 3/4 챙 다각형은 H-1 수평 분할에서 찢어진다 → 정면 렌즈꼴 챙(중절모 문법)으로 다시 그린다. 임시 좌표(H-2 맞춤 + C·H, 챙 분할 없음)는 r20_coords.txt 에 있다"),
    "equip.neck.pendant": ("가", "목줄+펜던트 = 방울목걸이 문법 · 정면 대칭 · N-1 dy·C·H"),
    "equip.neck.bandana": ("가", "띠+자락 = 목도리 문법(자락 한쪽) · N-1 dy·C·H"),
    "equip.shoulders.fairy_wings": ("가", "날개 문법 그대로(깃 2·척추) · C·H · 그룹 α 1.0"),
    "equip.eyes.browline": ("나", "3/4 렌즈 2·바 1(3조각) — 안경 문법(정면 64u · 테 선 M2 · 투명 렌즈 · 다리·코다리·하이라이트 7조각)으로 다시 그린다"),
    "equip.head.beret": ("나", "3/4 뒤로 처진 덩어리(2조각) — 정면 기하로 다시 그리고 H-2 맞춤·H-1(먼 쪽 없음)"),
    "equip.shoulders.poncho": ("나", "3/4 CapeOutline 파생(4조각) — 망토 무대 문법(뒤판+칼라+걸쇠)로 다시 그리되 새 짧은망토(밑단 −5.09)와 실루엣이 갈리게(폭·단 술)"),
}
for iid, ko, slot, key, mod in REST:
    if slot == "HAIR": GRADE[iid] = ("다", "인계본에 머리카락 없음 — 3/4 덩어리 6종은 별도 가족. 방침: 정면 기하 4~5조각(덩어리 M + 잉크 윤곽 + 결 낱선 + 하이라이트)으로 재창작, 모자 커버선 규약 유지")
    if slot == "FX": GRADE[iid] = ("다", "효과 — 착용 문법 밖. 카드 아이콘만 카드 문법(M 채움 + 카드 잉크 윤곽 + 하이라이트)으로 재창작, 몸 효과 기하는 그대로")
    if slot == "PET": GRADE[iid] = ("다", "독립 개체 — 인계본 없음. 카드 문법 재창작 + 펫 본체는 잉크 표식/재질색 규칙만(리틀스틱메이트는 잉크)")
EFFORT = {"가": "4종 — 이번 라운드 좌표 완료(r20_coords.txt) + 밀짚모자 임시 좌표", "나": "4종 — 종당 R15 급(6~9조각 정면 기하 + 게이트) ≈ 각 0.5일, 합 2일(밀짚모자는 챙만 재저작이라 0.3일)",
          "다": "18종 — 머리카락 6(각 0.5일) · FX/PET 12 카드 문법(각 0.2일) ≈ 5.4일; 몸 효과·펫 기하는 손대지 않는다"}

# ============================================================================
# 4. (가)군 좌표 — 층 · 색 역할 · 하이라이트 · 규칙 적용
# ============================================================================
Piece = M19.Piece
def P(kind, name, src, pts, *, loop=True, filled=False, crole=(None, "INK"), layer="front", mult=1.0, slot="head",
      role="", note="", conditional=None, transform=""):
    base = M16.SLOT_STROKE_R[slot]
    return Piece(kind, name, src, "V1", pts, loop, filled, mult, base * mult, None, ("ink", 1.0), None, 1.0, layer, role, note,
                 False, conditional, transform, None, crole=crole)

def highlight_arc(cx, cy, r, a0=120, a1=160, n=6):
    """좌상단 광택 한 획(인계본 H 문법: 흰 α0.42, ×0.75) — 채움 조각 안쪽 위-왼쪽 호."""
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / (n - 1))), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / (n - 1)))) for i in range(n)]

def ga_patch():
    kind = "patch"; sh = {s.name: s for s in items.EYES["안대"]}
    cov = sh["PatchCover"].pts; strap = sh["PatchStrap"].pts
    out = [P(kind, "patch.Cover", "COV", cov, filled=True, crole=("M", "INK"), slot="eyes", role="가리개(M Felt + 잉크 윤곽)"),
           P(kind, "patch.Strap", "STR", strap, loop=False, crole=(None, "M2"), slot="eyes", role="끈(독립선 M2)", mult=1.0),
           P(kind, "patch.H", "H", highlight_arc(0.62, 0.02, 0.30, 95, 175, 8), loop=False, crole=(None, "W"), slot="eyes", mult=0.75, role="하이라이트"),
           P(kind, "patch.ExposedEye", "EYE", rig.poly(M17.EYE_X, M17.EYE_Y, M17.EYE_R, 12), filled=True, crole=("EYE", None), slot="eyes",
             role="반대쪽 눈(조건부)", conditional="patch worn", note="E-1: r 1pt @(−0.46,+0.107) — 현행 DrawnEye r 0.33 @−0.62 대체")]
    return out

def ga_straw():
    """★ R20 시트 판정: 3/4 챙 다각형(앞끝 +2.59 R · 뒤끝 −2.45 R, 위·아래 변 높이가 다름)을 수평선으로 가르면 챙이 찢어진 모양이 된다
    (첫 판 실물 확인). H-1 분할은 정면 렌즈꼴 챙으로 다시 그려야 가능 → 밀짚모자는 (나). 여기 좌표는 **임시**: H-2 맞춤 + C·H 규칙만, 챙은 앞층 단일 조각."""
    kind = "straw"; sh = {s.name: s for s in items.HEAD["밀짚모자"]}
    f = fit_head(items.HEAD["밀짚모자"]); u, dy = f["u"], f["dy"]
    T = lambda pts: [(x * u, dy + y * u) for x, y in pts]
    brim, crown, band = T(sh["StrawBrim"].pts), T(sh["StrawCrown"].pts), T(sh["StrawBand"].pts)
    tr = "HAT_FIT u=%.3f dy=%+.3f (H-2) · H-1 미적용(임시 — 챙 재저작 필요)" % (u, dy)
    out = [P(kind, "straw.Brim", "BR", brim, filled=True, crole=("M", "INK"), role="챙(임시 단일 앞층 — H-1 은 재저작 뒤)", transform=tr),
           P(kind, "straw.Crown", "CR", crown, filled=True, crole=("M", "INK"), role="관(M Canvas)", transform=tr),
           P(kind, "straw.Band", "BD", band, filled=True, crole=("M2", None), role="띠(M2, 윤곽 없음 — 인계본 F 문법)", transform=tr),
           P(kind, "straw.H", "H", highlight_arc(-0.10 * u, dy + 0.50 * u, 0.62 * u, 105, 165, 8), loop=False, crole=(None, "W"), mult=0.75, role="하이라이트", transform=tr)]
    return out, f

def ga_pendant():
    kind = "pendant"; sh = {s.name: s for s in items.NECK["펜던트"]}
    top = max(q[1] for q in sh["Chain"].pts); dy = min(0.0, (-HEAD_R_SHIP - 0.03) - top)     # N-1: 머리 원반 밑 아래로
    T = lambda pts: [(x, y + dy) for x, y in pts]
    pend = T(sh["Pendant"].pts); cx = sum(q[0] for q in pend) / 4; cy = sum(q[1] for q in pend) / 4
    return [P(kind, "pendant.Chain", "CH", T(sh["Chain"].pts), loop=False, crole=(None, "M"), slot="neck", role="목줄(독립선 M Silver)", transform="SHIFT dy=%+.3f (N-1)" % dy),
            P(kind, "pendant.Stone", "ST", pend, filled=True, crole=("M2", "INK"), slot="neck", role="펜던트(M2 Gold + 잉크 윤곽)", transform="SHIFT dy=%+.3f" % dy),
            P(kind, "pendant.H", "H", [(cx - 0.16, cy + 0.42), (cx - 0.05, cy + 0.10)], loop=False, crole=(None, "W"), slot="neck", mult=0.75, role="하이라이트")], dy

def ga_bandana():
    kind = "bandana"; sh = {s.name: s for s in items.NECK["반다나"]}
    top = max(q[1] for q in sh["BandanaWrap"].pts); dy = min(0.0, (-HEAD_R_SHIP - 0.03) - top)
    T = lambda pts: [(x, y + dy) for x, y in pts]
    wrap = T(sh["BandanaWrap"].pts)
    return [P(kind, "bandana.Wrap", "WR", wrap, filled=True, crole=("M", "INK"), slot="neck", role="띠(M TintNeck + 잉크)", transform="SHIFT dy=%+.3f (N-1)" % dy),
            P(kind, "bandana.Tail", "TL", T(sh["BandanaTail"].pts), filled=True, crole=("M2", "INK"), slot="neck", role="자락(M2 NeckDeep + 잉크)", transform="SHIFT dy=%+.3f" % dy),
            P(kind, "bandana.H", "H", highlight_arc(-0.30, wrap[0][1] - 0.30, 0.48, 100, 160, 8), loop=False, crole=(None, "W"), slot="neck", mult=0.75, role="하이라이트")], dy

def ga_fairy():
    kind = "fairywings"; sh = {s.name: s for s in items.BACK["요정날개"]}
    fa, fb, sp = sh["WingFeatherA"].pts, sh["WingFeatherB"].pts, sh["WingSpine"].pts
    return [P(kind, "fairywings.FeatherA", "FA", fa, filled=True, crole=("M", "INK"), layer="back", slot="back", role="깃 A(M Paper + 잉크)"),
            P(kind, "fairywings.FeatherB", "FB", fb, filled=True, crole=("M", "INK"), layer="back", slot="back", role="깃 B"),
            P(kind, "fairywings.Spine", "SP", sp, loop=False, crole=(None, "M2"), layer="back", slot="back", role="척추(독립선 M2 TintBack — 몸통 뒤에 숨는다)"),
            P(kind, "fairywings.H", "H", highlight_arc(-0.90, SH + 0.25, 0.70, 95, 160, 8), loop=False, crole=(None, "W"), layer="back", slot="back", mult=0.75, role="하이라이트(깃 A)")]

GA_KIND = {"equip.eyes.patch": "patch", "equip.head.straw": "straw", "equip.neck.pendant": "pendant", "equip.neck.bandana": "bandana", "equip.shoulders.fairy_wings": "fairywings"}   # straw 는 (나)이지만 임시 좌표를 함께 낸다
GA_ID = {v: k for k, v in GA_KIND.items()}
GA_KO = {"patch": "안대", "straw": "밀짚모자", "pendant": "펜던트", "bandana": "반다나", "fairywings": "요정날개"}
GA_SLOT = {"patch": "EYES", "straw": "HEAD", "pendant": "NECK", "bandana": "NECK", "fairywings": "BACK"}
_straw, STRAW_FIT = ga_straw(); _pend, PENDANT_DY = ga_pendant(); _band, BANDANA_DY = ga_bandana()
GA = {"patch": ga_patch(), "straw": _straw, "pendant": _pend, "bandana": _band, "fairywings": ga_fairy()}

def resolve(kind, crole, surface, ink_hex):
    """M19.resolve 와 같은 뜻 — 팔레트 항목을 26종 id 로 찾는다."""
    it = PAL[GA_ID[kind]]; m, m2 = it[4], it[5]
    f, l = crole
    fill, fa = None, 1.0
    if f == "M": fill = m
    elif f == "M2": fill = m2
    elif f == "SH": fill = PM.shaded(m)
    elif f == "EYE": fill = PM.CHARCOAL if PM.L(ink_hex) >= 0.22 else PM.INK_WHITE
    line = None
    if l == "INK": line = PM.CARD_INK if surface == "card" else ink_hex
    elif l == "M": line = m
    elif l == "M2": line = m2
    elif l == "W": line = PM.INK_WHITE
    return fill, fa, line

def survival_rows():
    rows = []
    for k, ps in GA.items():
        for p in ps:
            ok, why = M16.survival(p, SHIP) if p.src not in ("EYE",) else (True, "OK")
            rows.append((k, p, ok, why))
    return rows

# ============================================================================
# 5. 보고
# ============================================================================
def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 1. 26종 전수 대조 (자: P 조각 · S 획 · H 하이라이트 · C 색 · G 기하 · L 층/착용 규칙) ==")
    auds = [audit(e) for e in REST]
    for a in auds:
        w("   %-28s %-8s %-5s 조각 %d(채움 %d, 톤 %s) 비대칭 %.2f  M %s M2 %s" % (a["id"], a["ko"], a["slot"], a["n"], a["nf"], a["tones"], a["asym"], a["m"], a["m2"]))
        for kk in ("P", "S", "H", "C", "G", "L"): w("        %s: %s" % (kk, a["verdict"][kk]))
    w()
    w("== 2. 격차 등급 ==")
    for g in ("가", "나", "다"):
        ids = [iid for iid, _, _, _, _ in REST if GRADE[iid][0] == g]
        w("   (%s) %d종 — %s" % (g, len(ids), EFFORT[g]))
        for iid in ids: w("        %-28s %s" % (iid, GRADE[iid][1]))
    w()
    w("== 3. (가)군 좌표 — 적용한 규칙과 숫자 ==")
    f = STRAW_FIT
    w("   밀짚모자 H-2 맞춤: u %.3f dy %+.3f · 착용선 %+.3f · 꼭대기 %+.3f · 밑 %+.3f (현행 착용선 %s)" % (f["u"], f["dy"], f["wear"], f["top"], f["bottom"], f["cur"]))
    w("   펜던트 N-1 dy %+.3f · 반다나 N-1 dy %+.3f (착용 조각 윗변을 머리 원반 밑 −%.3f 아래 0.03 R 로)" % (PENDANT_DY, BANDANA_DY, HEAD_R_SHIP))
    w("   안대 E-1: 가리개 오른쪽 그대로 · 반대쪽 눈 r %.4f @(%+.2f,%+.3f) 조건부 · 끈 = M2 독립선" % (M17.EYE_R, M17.EYE_X, M17.EYE_Y))
    for k, ps in GA.items():
        w("   -- %s (%s) M %s M2 %s" % (k, GA_KO[k], PAL[GA_ID[k]][4], PAL[GA_ID[k]][5]))
        for p in ps:
            x0, y0, x1, y1 = rig.bounds(p.pts)
            fb, _, lb = resolve(k, p.crole, "body", PM.INK_BLACK)
            w("      %-24s %-5s %-14s 검 %s/%s  ×%.2f 실폭 %.4f 잉크 %.2f×%.2fR %s" % (p.name, p.layer, str(p.crole), fb, lb, p.mult, p.width_R(), x1 - x0, y1 - y0, p.transform))
    w()
    w("== 4. (가)군 1pt 생존(@0.75) ==")
    for k, p, ok, why in survival_rows():
        w("   %-11s %-24s %s %s" % (k, p.name, "생존" if ok else "소멸", "" if ok else why))
    w()
    w("== 5. 팔레트 — 26종 재질색 존재 여부 (design-art ITEMS) ==")
    missing = [iid for iid, *_ in REST if iid not in PAL]
    w("   없음: %s" % (missing if missing else "0 (26/26 있음)"))
    inkmark = [iid for iid, *_ in REST if PAL[iid][4].upper() in PM.INK_MARKS or PAL[iid][5].upper() in PM.INK_MARKS]
    w("   잉크 표식(재질색이 아니라 유저 잉크 지시): %s" % inkmark)

if __name__ == "__main__":
    report()
