# -*- coding: utf-8 -*-
"""R4 공통 계측 모듈 — 출하 조형과 팩 조형을 **같은 자**로 잰다.

원천 3개를 모두 «머리 중심 원점 · R 배수 · +x 진행방향» 좌표로 정규화한다:
  (1) 인계본 12종  : Assets/.../AccessoryShapeBuilder.Handoff.cs 의 float[] 를 직접 파싱
  (2) NECK 6종     : Assets/_Project/Resources/Items/equip_neck_*.asset 의 wornShapes(terms 스트림) 해독
  (3) 팩 12종      : design/equipment/pack_detail_r{2,3}/*.wornShapes.yaml (같은 terms 스트림)
즉 **손으로 베낀 사본이 하나도 없다**(사본은 원본이 바뀌는 순간 거짓 초록을 낸다 — 이 저장소 규칙).
"""
import os, re, sys, math, glob, json, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
import rig as RIG

W = RIG.W                                   # 0.343864 R  (획 폭)
R_PT_075 = 0.22 * 0.75 * RIG.PT_PER_UNIT    # 5.8163 pt   (배율 0.75 착용 머리 반경)
R_PT_100 = 0.22 * 1.00 * RIG.PT_PER_UNIT    # 7.755 pt

# ── terms 기저 → 머리 중심 기준 R 값 ────────────────────────────────────────────
NECK_R = RIG.SHOULDER_R + 0.04              # NeckCollarRiseRatio (AccessoryShapeBuilder.cs:201)
BASIS = {0: 1.0, 1: RIG.TORSO_R, 2: NECK_R, 3: RIG.SHOULDER_R, 4: 0.0,
         5: RIG.HIP_R, 8: RIG.BASELINE_TOTAL_H / RIG.BASELINE_HEAD_R}

class Cur:
    def __init__(self, v): self.v, self.i = v, 0
    def take(self, n=1):
        out = self.v[self.i:self.i + n]; self.i += n
        return out
    def one(self): 
        x = self.v[self.i]; self.i += 1; return x

def read_sum(c):
    tn = int(round(c.one())); acc = 0.0
    for _ in range(tn):
        basis = int(round(c.one())); gate = int(round(c.one())); trig = int(round(c.one()))
        cn = int(round(c.one())); coefs = [c.one() for _ in range(cn)]
        v = BASIS.get(basis, 0.0)
        for k, co in enumerate(coefs):
            if trig and k == cn - 1:
                co = math.cos(co) if trig == 1 else math.sin(co)
            v *= co
        acc += v
    return acc

def decode_terms(terms, swing):
    c = Cur(terms); n = int(round(c.one())); pts = []
    for _ in range(n):
        if swing:                                # 기울이기 전 로컬 벡터가 앞에 한 쌍 더 있다
            read_sum(c); read_sum(c)
        x = read_sum(c); y = read_sum(c)
        pts.append((x, y))
    return pts

# ── (1) 인계본 ───────────────────────────────────────────────────────────────
def load_handoff():
    path = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
    src = open(path, encoding="utf-8").read()
    arrays = {}
    for m in re.finditer(r"static readonly float\[\]\s+(Handoff_\w+)\s*=\s*\{(.*?)\};", src, re.S):
        nums = [float(v[:-1]) for v in re.findall(r"-?\d+(?:\.\d+)?f", m.group(2))]
        arrays[m.group(1)] = [(nums[i], nums[i + 1]) for i in range(0, len(nums) - 1, 2)]
    CASE = re.compile(r"case\s+(\w+):\s*//\s*(\S+)\s+(\w+)\s+—\s*조각\s*(\d+)")
    call = re.compile(
        r'HandoffPiece\(sink, rig, xf, (\w+), "(\w+)", (\w+), loop: (\w+), filled: (\w+), tone: (\d+), '
        r'surfaces: (\d+), strokeMult: ([\d.]+)f, strokeInR: ([\d.]+)f, noStroke: (\w+), alpha: ([\d.]+)f, '
        r'lineAlpha: ([\d.]+)f, underBack: (\d+), layer: (\d+), swayStart: (-?\d+), swayCount: (\d+), bodyFixed: (\w+)\)')
    out = {}
    marks = list(CASE.finditer(src))
    for i, m in enumerate(marks):
        blk = src[m.end(): marks[i + 1].start() if i + 1 < len(marks) else len(src)]
        pieces = []
        for c in call.finditer(blk):
            (_s, pname, arr, loop, filled, tone, surf, smult, sinr,
             nos, alpha, lalpha, _u, layer, _a, _b, bfix) = c.groups()
            pieces.append(dict(name=pname, pts=arrays[arr], loop=loop == "true", filled=filled == "true",
                               tone=int(tone), surfaces=int(surf), strokeMult=float(smult),
                               noStroke=nos == "true", alpha=float(alpha), lineAlpha=float(lalpha),
                               layer=int(layer), bodyFixed=bfix == "true"))
        out[m.group(3)] = dict(ko=m.group(2), slot=m.group(1), pieces=pieces)
    return out

# ── (2)(3) terms 스트림(YAML/에셋) ────────────────────────────────────────────
_NUM = re.compile(r"\s*-\s*(-?[\d.]+(?:[eE][-+]?\d+)?)\s*$")
def load_terms_yaml(path, key="wornShapes"):
    lines = open(path, encoding="utf-8").read().splitlines()
    try: start = next(i for i, l in enumerate(lines) if l.strip() == key + ":")
    except StopIteration: return []
    shapes, cur, terms = [], None, None
    for l in lines[start + 1:]:
        m = re.match(r"\s*- name:\s*(\S+)", l)
        if m:
            if cur: shapes.append(cur)
            cur, terms = dict(name=m.group(1)), None; continue
        if cur is None: continue
        if re.match(r"\s*\w+:", l) and not re.match(r"\s*terms:", l):
            k, v = l.split(":", 1); k = k.strip(); v = v.strip()
            if re.match(r"^-?[\d.]+$", v): cur[k] = float(v)
            continue
        if re.match(r"\s*terms:\s*$", l):
            terms = []; cur["_t"] = terms; continue
        m = _NUM.match(l)
        if m and terms is not None: terms.append(float(m.group(1)))
        elif terms is not None and l.strip() and not m: break
    if cur: shapes.append(cur)
    out = []
    for s in shapes:
        t = s.pop("_t", [])
        if not t: continue
        out.append(dict(name=s["name"], pts=decode_terms(t, s.get("swingDegrees", 0) != 0),
                        loop=bool(s.get("loop", 1)), filled=bool(s.get("filled", 0)),
                        tone=int(s.get("tone", 0)), surfaces=1, strokeMult=s.get("strokeMult", 1.0),
                        noStroke=bool(s.get("noStroke", 0)), alpha=s.get("alpha", 1.0),
                        lineAlpha=s.get("lineAlpha", 1.0), layer=int(s.get("layer", 0)),
                        bodyFixed=bool(s.get("bodyFixed", 0))))
    return out

def load_neck6():
    out = {}
    for p in sorted(glob.glob(os.path.join(ROOT, "Assets/_Project/Resources/Items/equip_neck_*.asset"))):
        key = os.path.basename(p).replace("equip_neck_", "").replace(".asset", "")
        out[key] = dict(ko=key, slot="NECK", pieces=load_terms_yaml(p))
    return out

def load_pack(dirname):
    out = {}
    for p in sorted(glob.glob(os.path.join(ROOT, "design/equipment", dirname, "*.wornShapes.yaml"))):
        key = os.path.basename(p).replace(".wornShapes.yaml", "")
        out[key] = dict(ko=key, slot=key.split("_")[2].upper(), pieces=load_terms_yaml(p))
    return out

# ── 계측 ─────────────────────────────────────────────────────────────────────
def seglens(pts, loop):
    n = len(pts); idx = range(n) if loop else range(n - 1)
    return [math.dist(pts[i], pts[(i + 1) % n]) for i in idx]

def turns(pts, loop):
    n = len(pts); out = []
    rng = range(n) if loop else range(1, n - 1)
    for i in rng:
        out.append((i, RIG.turn_deg(pts[(i - 1) % n], pts[i], pts[(i + 1) % n])))
    return out

def sagitta(pts, loop, corner_deg=RIG.CORNER_DEG):
    """면(faceting) 오차 — «부드러운 곡선을 몇 각형으로 깎았나».
    등각 다각형 근사에서 한 변이 이상 원호에서 벗어나는 최대 거리 s = (L/2)·tan(θ/4).
    θ >= corner_deg 인 꼭짓점은 **의도된 모서리**이므로 제외한다."""
    n = len(pts); L = seglens(pts, loop); out = []
    for i, th in turns(pts, loop):
        if th >= corner_deg or th < 1e-6: continue
        a = math.dist(pts[(i - 1) % n], pts[i]); b = math.dist(pts[i], pts[(i + 1) % n])
        out.append(((a + b) / 2.0 / 2.0) * math.tan(math.radians(th) / 4.0))
    return out

def bbox_span(pts):
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    return max(xs) - min(xs), max(ys) - min(ys)

def piece_metrics(p):
    pts, loop = p["pts"], p["loop"]
    L = seglens(pts, loop); T = [t for _, t in turns(pts, loop)]; S = sagitta(pts, loop)
    bw, bh = bbox_span(pts)
    return dict(n=len(pts), perim=sum(L), segmin=min(L) if L else 0.0,
                segmean=(sum(L) / len(L)) if L else 0.0,
                turnmax=max(T) if T else 0.0, corners=sum(1 for t in T if t >= RIG.CORNER_DEG),
                sagmax=max(S) if S else 0.0, sagmean=(sum(S) / len(S)) if S else 0.0,
                span=max(bw, bh), spanmin=min(bw, bh), bw=bw, bh=bh)

def body_pieces(item):
    return [p for p in item["pieces"] if p["surfaces"] == 0 or (p["surfaces"] & 1)]

def card_pieces(item):
    return [p for p in item["pieces"] if p["surfaces"] == 0 or (p["surfaces"] & 2)]
