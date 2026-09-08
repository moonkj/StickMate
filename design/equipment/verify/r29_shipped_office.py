# -*- coding: utf-8 -*-
"""r29 — **출하 중인 `CostumeManifest_office.asset`의 실제 좌표**를 r28 프롭 게이트에 통과시킨다.

왜 새로 짜는가: `r28_props.py`는 **내가 제안한 C2**를 쟀다. 그런데 저장소에 굽혀 나가는 에셋은
**다른 도형**이다(선 10개 · 채움 0개 · 보조색 0개). **출하본은 한 번도 게이트를 통과한 적이 없다.**

이 스크립트는 에셋을 직접 파싱한다 — 좌표를 손으로 옮기지 않는다(그 옮김이 이 저장소가 반복해
당한 사고 형태다).
    python3 r29_shipped_office.py
"""
import math, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
import r28_props as P

ASSET = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                     "../../../Assets/_Project/Resources/Items/CostumeManifest_office.asset")

# ── 0. 에셋 파서 — AccessoryWornShapeReader.TryBuild 문법 그대로 ────────────────
BASIS_H = 8

def parse_asset(path):
    lines = open(path).read().split('\n')
    out, cur, in_terms, section, stage = [], None, False, None, None
    for ln in lines:
        s = ln.strip()
        ind = len(ln) - len(ln.lstrip())
        if re.match(r'^(propShapes|stageShapes|propFrames):', s):
            section = s.split(':')[0]; in_terms = False; continue
        if section and ind <= 2 and re.match(r'^[a-zA-Z_]+:', s) and not s.startswith('- '):
            section = None
        m = re.match(r'^- stage: *(\d+)', s)
        if m: stage = int(m.group(1)); continue
        if s.startswith('- name:'):
            cur = dict(section=section, stage=stage, name=s.split(':', 1)[1].strip(), terms=[])
            out.append(cur); in_terms = False; continue
        if s == 'terms:': in_terms = True; continue
        if in_terms:
            if s.startswith('- '):
                try: cur['terms'].append(float(s[2:])); continue
                except ValueError: in_terms = False
            else: in_terms = False
        if cur is not None and not in_terms and ':' in s and not s.startswith('- '):
            k, _, v = s.partition(':')
            if k in ('loop','filled','tone','surfaces','strokeInR','layer','noStroke','alpha','lineAlpha'):
                cur[k] = float(v.strip())
    return out

def read_sum(t, i):
    n = int(round(t[i])); i += 1
    acc, bases = 0.0, set()
    for _ in range(n):
        b, g, tr, nc = (int(round(t[i+k])) for k in range(4)); i += 4
        coefs = t[i:i+nc]; i += nc
        v = 1.0
        for c in coefs: v *= c
        acc += v; bases.add(b)
    return acc, bases, i

def decode(t):
    i = 0; cnt = int(round(t[i])); i += 1
    pts, bases = [], set()
    for _ in range(cnt):
        x, bx, i = read_sum(t, i)
        y, by, i = read_sum(t, i)
        pts.append((x, y)); bases |= bx | by
    assert i == len(t), "스트림 잔여 %d" % (len(t) - i)
    return pts, bases

raw = parse_asset(ASSET)
base_set, stage0 = [], []
for sh in raw:
    if not sh['terms']: continue
    pts, bases = decode(sh['terms'])
    assert bases == {BASIS_H}, "%s: 기저가 H(8)가 아니다 -> %s" % (sh['name'], bases)
    S = P.S(sh['name'], pts, loop=bool(sh.get('loop', 0)),
            filled=bool(sh.get('filled', 0)), tone=int(sh.get('tone', 0)), stage=0)
    S.surfaces = int(sh.get('surfaces', 0)); S.strokeInR = sh.get('strokeInR', 0)
    (stage0 if sh['section'] == 'stageShapes' else base_set).append(S)

W = P.WP
FILL_MULT = 2.2          # CostumePropRenderer.cs:278  stroke * 2.2f  (메시가 아니라 굵은 재묘사)

def wmul(sh):
    """그 조각이 화면에서 차지하는 획 배수. 채움은 2.2배로 한 번 더 그어진다."""
    return FILL_MULT if sh.filled else 1.0

def pair_bound(a, b):
    """PR-4 (채움 인지판): d <= (w_a+w_b)/2 이면 **잉크가 합쳐진 것**(= 닿음, 합법),
       d >= (w_a+w_b)/2 + 1.0 W 이면 합법. 그 사이가 금지대다."""
    half = (wmul(a) + wmul(b)) / 2.0 * W
    return half, half + 1.0 * W

def pair_valley(a, b, d, scale, dpi=1.0):
    g_H = d - (wmul(a) + wmul(b)) / 2.0 * P.stroke_in_H(scale) / P.WP * P.WP
    # stroke_in_H 는 1배 획의 H 배수다. 배수는 그 값에 곱한다.
    g_H = d - (wmul(a) + wmul(b)) / 2.0 * P.stroke_in_H(scale)
    return (g_H * P.PT_PER_H * scale - 0.5) * dpi

fails = []
def bad(m): fails.append(m); print("  x  " + m)
def ok(m):  print("  OK " + m)

print("=" * 108)
print("  r29 — 출하 중인 CostumeManifest_office.asset 실좌표 게이트 (r28 규칙 그대로)")
print("=" * 108)
print("  propShapes(단계 오버라이드 없음일 때 그리는 것) %d조각 / stageShapes[0] %d조각"
      % (len(base_set), len(stage0)))
print()

def gate(label, shapes):
    print("-" * 108)
    print("  [%s]  %d조각 : %s" % (label, len(shapes), ", ".join(s.name for s in shapes)))
    print("-" * 108)
    # PR-11 보조색 / 채움
    acc = [s for s in shapes if s.tone == 1]
    fil = [s for s in shapes if s.filled]
    (ok if len(acc) == 1 else bad)("%s 보조색(tone=1) 조각 %d개 (규칙: 정확히 1개)" % (label, len(acc)))
    (ok if len(fil) == 1 else bad)("%s 채움 조각 %d개 (규칙: 정확히 1개 = 팩 정체)" % (label, len(fil)))
    # surfaces — 프롭은 Body(1) 이어야 한다. 0이면 Effective(0)=Body|Card
    # ★ 위반이 아니다 — 실측으로 정정: `CostumePropRenderer`는 `surfaces`를 **읽지 않는다**
    #   (`grep -n surfaces CostumePropRenderer.cs` = 0건. 읽는 곳은 `AccessoryShapeBuilder` 하나이고
    #    프롭은 그 경로를 안 탄다). 그래서 오늘은 무해하고, 값싼 보험으로만 남긴다.
    zero_surf = [s.name for s in shapes if getattr(s, 'surfaces', 0) == 0]
    print("      [정보] surfaces=0 %d조각 — 프롭 렌더러가 이 필드를 안 읽으므로 **오늘은 무해**."
          " 나중에 프롭을 공용 빌더로 돌리면 Effective(0)=Body|Card 가 살아난다" % len(zero_surf))
    # PR-1 / PR-2
    for s in shapes:
        e, _ = P.corner_min_edge(s)
        if e < 9e8 and e < 1.0 * W:
            bad("%s PR-1 %s 꺾임-꺾임 변 %.4f H = %.2f W_P < 1.00" % (label, s.name, e, e / W))
        sp, _, _ = P.span_of(s)
        if sp < 1.5 * W:
            bad("%s PR-2 %s 잉크 사각형 %.4f H = %.2f W_P < 1.50" % (label, s.name, sp, sp / W))
    # PR-3
    for s in shapes:
        if s.loop and rig.self_intersects(s.pts):
            bad("%s PR-3 %s 자기교차" % (label, s.name))
    # PR-4 / PR-4b
    worstv, wpair = 9e9, None
    for i in range(len(shapes)):
        for j in range(i + 1, len(shapes)):
            A, B = shapes[i], shapes[j]
            d, _ = P.shape_dist(A, B)
            merge, need = pair_bound(A, B)
            if d <= 1e-9:
                a = P.junction_angle(A, B)
                if a is not None and a < 30.0:
                    bad("%s PR-4b %s x %s 접합각 %.1f도 < 30" % (label, A.name, B.name, a))
                continue
            if d <= merge:      # 잉크가 이미 합쳐졌다 = 닿은 것과 같다
                continue
            v = pair_valley(A, B, d, 0.75)
            if v < worstv: worstv, wpair = v, (A.name, B.name, d, need)
            if d < need:
                bad("%s PR-4 금지대 %s x %s d=%.4f H = %.2f W_P (하한 %.2f W_P) · 골 %.2f px @0.75/1x"
                    % (label, A.name, B.name, d, d / W, need / W, v))
    if wpair:
        print("      최악(떨어진) 쌍 %s x %s = %.4f H = %.2f W_P (하한 %.2f) · 골 %.2f px @0.75/1x"
              % (wpair[0], wpair[1], wpair[2], wpair[2] / W, wpair[3] / W, worstv))
        # PR-14 s_min — 그 쌍을 배율축으로 훑는다(채움 배수 반영)
        A = next(x for x in shapes if x.name == wpair[0]); B = next(x for x in shapes if x.name == wpair[1])
        for dpi, tag in ((1.0, "1x"), (2.0, "2x")):
            sc, smin = 0.35, None
            while sc <= 1.0001:
                if pair_valley(A, B, wpair[2], sc, dpi) >= 1.0: smin = round(sc, 3); break
                sc += 0.005
            print("      PR-14 s_min(%s) = %s" % (tag, ("%.3f" % smin) if smin else "1.00 초과(전 배율 실패)"))
    # BACK 6종 겹침 (r28 (8)과 같은 방법)
    import items as _it
    wb = (9e9, None)
    for bn, bfn in _it.BACK.items():
        bsh = bfn() if callable(bfn) else bfn
        for b in bsh:
            bH = rig.Shape(b.name, [(q[0] / P.H_IN_R,
                                     (q[1] + rig.HEAD_CENTER / rig.BASELINE_HEAD_R) / P.H_IN_R)
                                    for q in b.pts], b.loop, b.filled, b.tone)
            for s in shapes:
                d, _ = P.shape_dist(s, bH)
                if d < wb[0]: wb = (d, (bn, b.name, s.name))
    tag = "겹침" if wb[0] <= 1e-9 else ("금지대" if wb[0] < 2 * W else "안전")
    msg = ("%s BACK 최악 '%s/%s' x '%s' %.4f H = %.2f W_P · 골 %.2f px [%s]"
           % (label, wb[1][0], wb[1][1], wb[1][2], wb[0], wb[0] / W, P.valley_px(wb[0], 0.75), tag))
    (ok if wb[0] >= 2 * W else bad)(msg)
    # 봉투
    xs = [p[0] for s in shapes for p in s.pts]; ys = [p[1] for s in shapes for p in s.pts]
    print("      봉투 x [%.4f, %.4f] · y [%.4f, %.4f] · 필요폭 %.4f H = %.1f pt @0.75"
          % (min(xs), max(xs), min(ys), max(ys), max(xs), max(xs) * P.PT_PER_H * 0.75))
    print()

gate("stageShapes[0]  (= S0, 0~10h 에 실제로 그려지는 것)", stage0)
gate("propShapes      (= S1/S2/S3 폴백, 10h 이후 전부 이것)", base_set)

# ── 단계 진화가 실재하는가 ────────────────────────────────────────────────────
print("-" * 108)
print("  [진화] ResolveStageShapes 는 stage 와 정확히 같은 오버라이드만 쓰고, 없으면 propShapes 로 떨어진다")
print("-" * 108)
have = sorted({int(s['stage']) for s in raw if s['section'] == 'stageShapes' and s['terms']})
print("      오버라이드가 있는 단계: %s" % have)
for st in range(4):
    n = len(stage0) if st in have else len(base_set)
    src = "stageShapes[%d]" % st if st in have else "propShapes(폴백)"
    print("      S%d -> %-22s %2d조각" % (st, src, n))
distinct = len({len(stage0) if st in have else len(base_set) for st in range(4)})
(bad if distinct < 4 else ok)("진화 4단계 중 **서로 다른 그림은 %d개뿐**이다 (요구: 4)" % distinct)

print()
print("=" * 108)
print("결과: 위반 %d건" % len(fails))
for f in fails: print("   - " + f)


# ═══════════════════════════════════════════════════════════════════════════════
#  경로 B — 「출하본 접붙이기」: 골든 테스트를 하나도 안 깨고 결함 3건을 고친다
#
#  CostumeOfficeAssetTests 가 잠그는 것은 정확히 이 넷뿐이다(실측, :85-131):
#     propShapes.Count = 10 · minX = 0.26 · maxX = 1.00 · maxY = 1.02
#     stage==0 오버라이드의 shapes.Length = 4        (stage 1~3 은 **안 본다**)
#  아래 설계는 그 넷을 전부 보존한다.
# ═══════════════════════════════════════════════════════════════════════════════
print()
print("=" * 108)
print("  경로 B — 출하본 접붙이기 (골든 4개 보존 · 보조색 신설 · S2/S3 신설)")
print("=" * 108)

L = lambda n, pts, **k: P.S(n, pts, **k)

# --- 좌표 수정 3건 (S0·S1 공통) : 보조색 채움을 「없던 자리」가 아니라 기존 조각에서 만든다
FIX = [
    L("DeskLip",  [(0.2600, 0.5520), (0.4000, 0.5520), (0.4000, 0.6200), (0.2600, 0.6200)],
      loop=True, filled=True, tone=1),          # 선 -> 채움 4각. 팩 정체 + 손이 짚는 자리
    L("DeskTop",  [(0.4000, 0.6200), (1.0000, 0.6200)]),   # 근단 0.26 -> 0.40 (앞턱과 공선중복 회피)
    L("DeskLeg",  [(0.3200, 0.0000), (0.3200, 0.5520)]),   # 상단 0.62 -> 0.552 (앞턱 밑면에 붙인다)
]
KEEP = [s for s in base_set if s.name not in {"DeskLip", "DeskTop", "DeskLeg"}]
KEEP0 = [s for s in stage0 if s.name not in {"DeskLip", "DeskTop", "DeskLeg"}]

B_S0 = KEEP0 + FIX                                     # 4조각
B_S1 = KEEP + FIX                                      # 10조각
B_ADD2 = [
    L("Shelf",   [(0.3200, 0.3000), (0.7200, 0.3000)]),                     # 다리<->칸막이에 붙는다(PR-B)
    L("Box",     [(0.3900, 0.3000), (0.3900, 0.3800), (0.4900, 0.3800), (0.4900, 0.3000)]),
    L("Book",    [(0.5600, 0.3000), (0.5600, 0.3650), (0.6500, 0.3650), (0.6500, 0.3000)]),
    L("Mat",     [(0.2600, 0.0000), (0.7200, 0.0000)]),
]
# ★ 초안의 X 트러스(칸막이 안)는 게이트가 3건으로 **잡아냈다** — 슬롯2와 0.0540 H(1.59 W_P),
#   Shelf2 와 0.0400 H(1.18 W_P). 그리고 칸막이 위 띠도 재 보니 **아무것도 안 들어간다**:
#   출하본은 스탠드를 칸막이 상단에 얹어서 빛살 LampRay2 가 칸막이 안쪽을 (0.8006,0.88)~(0.86,0.62)
#   으로 가로지른다. 왼쪽 잔여폭 0.08 H · 오른쪽 0.14 H 인데 금지대 두 번이 0.1356 H 를 먹는다.
#   ⇒ **S3 증분은 책상 밑으로 갈 수밖에 없다.** (설계 문서 5-4-C 가 스탠드를 상판으로 옮긴 이유를
#      출하본 좌표에서 독립적으로 재현한 것이다.)
B_ADD3 = [
    L("Shelf2",  [(0.3200, 0.4550), (0.7200, 0.4550)]),
    L("LowRail", [(0.3200, 0.1600), (0.7200, 0.1600)]),
    L("FloorBox",[(0.3900, 0.0000), (0.3900, 0.0900), (0.5100, 0.0900), (0.5100, 0.0000)]),
    L("FloorBin",[(0.5800, 0.0000), (0.5800, 0.0700), (0.6400, 0.0700), (0.6400, 0.0000)]),
]
B_S2 = B_S1 + B_ADD2
B_S3 = B_S2 + B_ADD3

for nm, sh in (("경로B S0", B_S0), ("경로B S1", B_S1), ("경로B S2", B_S2), ("경로B S3", B_S3)):
    gate(nm, sh)

# --- 골든 4개가 살아 있는가 (경로 B 의 존재 이유) ---
print("-" * 108)
print("  [골든] CostumeOfficeAssetTests 가 잠그는 4개 값이 경로 B 에서 그대로인가")
print("-" * 108)
xs = [p[0] for s in B_S1 for p in s.pts]; ys = [p[1] for s in B_S1 for p in s.pts]
for label, got, want in (("propShapes.Count", len(B_S1), 10),
                         ("minX", round(min(xs), 4), 0.26),
                         ("maxX", round(max(xs), 4), 1.00),
                         ("maxY", round(max(ys), 4), 1.02),
                         ("stage0.shapes.Length", len(B_S0), 4)):
    (ok if got == want else bad)("골든 %-22s = %s (기대 %s)" % (label, got, want))
print("      단계별 선 수 %d / %d / %d / %d  (S0->S3 %.1f배)"
      % (len(B_S0), len(B_S1), len(B_S2), len(B_S3), len(B_S3) / len(B_S0)))

# ── 양성 대조 — 이 게이트가 잠들어 있지 않은가 ────────────────────────────────
#   ★ 대조는 「이미 잡힌 적이 있다」로 대신하지 않는다. 초안의 X 트러스 3건을 실제로 잡았지만,
#     그건 우연히 나쁜 값이었을 뿐이다. 여기서는 **계통마다 하나씩** 일부러 심는다.
if "--control" in sys.argv:
    print()
    print("=" * 108)
    print("  양성 대조 — 경로B S3 를 일부러 망가뜨린다")
    print("=" * 108)
    before = len(fails)
    BADSET = [s for s in B_S3 if s.name != "DeskLip"] + [
        L("DeskLip",  [(0.2600, 0.5520), (0.4000, 0.5520), (0.4000, 0.6200), (0.2600, 0.6200)],
          loop=True, filled=True, tone=1),
        L("BadAccent", [(0.8200, 0.7000), (0.8600, 0.7000), (0.8600, 0.7400), (0.8200, 0.7400)],
          loop=True, filled=True, tone=1),            # 보조색 2개 · 채움 2개
        L("BadPar",   [(0.3200, 0.3200), (0.7200, 0.3200)]),   # Shelf 와 0.02 H -> PR-4 금지대
        L("BadDup",   [(0.4000, 0.6200), (0.7000, 0.6200)]),   # DeskTop 과 공선 중복 -> PR-4b
        L("BadTiny",  [(0.6600, 0.5200), (0.6600, 0.5400)]),   # 0.02 H -> PR-2 잉크 사각형
    ]
    gate("대조 S3(불량)", BADSET)
    got = len(fails) - before
    print("  %s 양성 대조: %s — 검출 %d건" % ("OK" if got >= 4 else " x ",
          "실제로 잡는다" if got >= 4 else "**게이트가 잠들어 있다**", got))
    fails[:] = fails[:before]      # 대조 검출은 최종 집계에 넣지 않는다

# ── PR-8 작업점 접지 — 손이 짚는 자리에 실제로 잉크가 있는가 ─────────────────
#   오피스 작업점은 모션 5-2 가 못박은 (0.28, 0.62) H 하나다.
print("-" * 108)
print("  [PR-8] 작업점 (0.28, 0.62) H 에서 가장 가까운 프롭 잉크까지의 거리 (하한 0.5 W_P = %.4f H)"
      % (0.5 * W))
print("-" * 108)
WPT = (0.2800, 0.6200)
for nm, sh in (("출하 S0", stage0), ("출하 S1", base_set),
               ("경로B S0", B_S0), ("경로B S1", B_S1), ("경로B S2", B_S2), ("경로B S3", B_S3)):
    best, who = 9e9, None
    for x in sh:
        for a, b in P.segs(x):
            d = P._pt_seg(WPT, a, b)
            if d < best: best, who = d, x.name
    (ok if best <= 0.5 * W else bad)("%-8s 작업점 -> '%s' %.4f H = %.2f W_P" % (nm, who, best, best / W))

print()
print("=" * 108)
print("최종: 위반 %d건" % len(fails))
for f in fails: print("   - " + f)
