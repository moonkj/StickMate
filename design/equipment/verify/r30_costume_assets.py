# -*- coding: utf-8 -*-
"""R30 — 코스튬 매니페스트/키포즈 **에셋 생성기** (coder-systems, 2026-09-08).

  python3 r30_costume_assets.py --check     # 좌표 게이트만 (파일 안 씀)
  python3 r30_costume_assets.py --write     # Resources/Items 에 .asset/.meta 를 쓴다

좌표 출처(손으로 옮기지 않는다 — 하니스를 import 한다):
  · R28  `r28_props.py`            4프롭 x 4단계 좌표 전문
  · R29  `EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS_R29.md`
         3절  경로 B (오피스 접붙이기, 골든 5개 무손상)
         6-4절 채움 인지 게이트 수정 2건 (광부 Crack1 / 대마법사 Ring1~3)

★ 이 파일은 **생성기**다. 검사기는 Unity EditMode 테스트
  (`CostumePropForbiddenZoneTests` · `CostumeShippedSourceKindTests`)이고 **구현이 다르다** —
  TEAM.md 「생성기와 검사기가 같이 틀린다」를 피하려면 그 둘이 코드를 공유하면 안 된다.
  아래 --check 는 러너를 **대체하지 않는** 예행일 뿐이다.
"""
import sys, os, math

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig
import r28_props as R28

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ITEMS = os.path.join(REPO, "Assets", "_Project", "Resources", "Items")

WP = R28.WP                       # 0.0339 H — CostumePropRenderer.StrokeWidthRatio
MIN_GAP_1x1 = 2.00 * WP           # PR-4
MIN_GAP_FILL = 2.60 * WP          # R29 6-3 (채움 = 2.2배 재묘사)

# 스크립트 GUID — .cs.meta 에서 읽는다(베끼지 않는다).
def script_guid(name):
    path = os.path.join(REPO, "Assets", "_Project", "Scripts", "Core", name + ".cs.meta")
    with open(path) as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise SystemExit("스크립트 메타를 못 읽었다: " + path)


# ─────────────────────────────────────────────────────────────────────────────
# 1. 조형 — R28 좌표 + R29 수정
# ─────────────────────────────────────────────────────────────────────────────
class Sh:
    __slots__ = ("name", "pts", "loop", "filled", "tone", "stage")

    def __init__(self, name, pts, loop=False, filled=False, tone=0, stage=0):
        self.name, self.pts, self.loop = name, [(float(x), float(y)) for x, y in pts], loop
        self.filled, self.tone, self.stage = filled, tone, stage


def _from_r28(shapes):
    return [Sh(s.name, s.pts, s.loop, s.filled, s.tone, s.stage) for s in shapes]


def cyber():
    return _from_r28(R28.cyber())


def miner():
    out = _from_r28(R28.miner())
    # ★ R29 6-4 수정 — Vein1(채움) x Crack1 이 2.02 W_P 로 채움 하한(2.60)을 위반한다.
    for s in out:
        if s.name == "Crack1":
            s.pts = [(0.3900, 0.8550), (0.4500, 0.8550)]
    return out


def archmage():
    out = _from_r28(R28.archmage())
    # ★ R29 6-4 수정 — StaffMoon(채움) x Ring1 이 2.21 W_P 로 채움 하한(2.60)을 위반한다.
    #   링 3개를 등간격 0.170 으로 다시 깔아 초승달과 2.65 W_P 를 만든다.
    newy = {"Ring1": 0.7250, "Ring2": 0.5550, "Ring3": 0.3850}
    for s in out:
        if s.name in newy:
            s.pts = rig.poly(1.0400, newy[s.name], 0.0500, 12)
    return out


def office():
    """★ R29 3절 「경로 B」 — 출하 에셋(motion 5-2 포팅)에 접붙인다.
    R28 C2 설계본이 아니다. 골든 5개(10 / 0.26 / 1.00 / 1.02 / 4)를 하나도 안 건드린다."""
    return [
        # ---- S0 (4선) : 출하 좌표 + R29 3-1 수정 3건 ----
        Sh("Partition", [(0.7200, 0.1000), (1.0000, 0.1000), (1.0000, 0.8800), (0.7200, 0.8800)],
           loop=True, stage=0),
        Sh("DeskTop", [(0.4000, 0.6200), (1.0000, 0.6200)], stage=0),          # 근단 0.26 -> 0.40
        Sh("DeskLeg", [(0.3200, 0.0000), (0.3200, 0.5520)], stage=0),          # 상단 0.62 -> 0.552
        Sh("DeskLip", [(0.2600, 0.5520), (0.4000, 0.5520), (0.4000, 0.6200), (0.2600, 0.6200)],
           loop=True, filled=True, tone=1, stage=0),                            # 선 2점 -> 채움 4각
        # ---- S1 (+6 = 10선) : 출하 좌표 그대로 ----
        Sh("LampPost", [(0.9200, 0.8800), (0.9200, 1.0200)], stage=1),
        Sh("LampShade", [(0.9200, 1.0200), (0.7800, 0.9700)], stage=1),
        Sh("LampRay1", [(0.7800, 0.9700), (0.7200, 0.8800), (0.5400, 0.6200)], stage=1),
        Sh("LampRay2", [(0.7800, 0.9700), (0.8600, 0.6200)], stage=1),
        Sh("PartitionSlot1", [(0.7900, 0.3000), (0.9300, 0.3000)], stage=1),
        Sh("PartitionSlot2", [(0.7900, 0.4400), (0.9300, 0.4400)], stage=1),
        # ---- S2 (+4 = 14선) : R29 3-2 「짐이 쌓이기 시작했다」 ----
        Sh("Shelf", [(0.3200, 0.3000), (0.7200, 0.3000)], stage=2),
        Sh("Box", [(0.3900, 0.3000), (0.3900, 0.3800), (0.4900, 0.3800), (0.4900, 0.3000)], stage=2),
        Sh("Book", [(0.5600, 0.3000), (0.5600, 0.3650), (0.6500, 0.3650), (0.6500, 0.3000)], stage=2),
        Sh("Mat", [(0.2600, 0.0000), (0.7200, 0.0000)], stage=2),
        # ---- S3 (+4 = 18선) : R29 3-3 「선반이 3층이 되고 바닥에도 쌓였다」 ----
        Sh("Shelf2", [(0.3200, 0.4550), (0.7200, 0.4550)], stage=3),
        Sh("LowRail", [(0.3200, 0.1600), (0.7200, 0.1600)], stage=3),
        Sh("FloorBox", [(0.3900, 0.0000), (0.3900, 0.0900), (0.5100, 0.0900), (0.5100, 0.0000)], stage=3),
        Sh("FloorBin", [(0.5800, 0.0000), (0.5800, 0.0700), (0.6400, 0.0700), (0.6400, 0.0000)], stage=3),
    ]


# 코스튬 등록 — 파일명 / costumeKey / sourceKind / sourceId
COSTUMES = [
    ("office", office, "costume.office", 0, "office",     (4, 10, 14, 18)),
    ("cyber",  cyber,  "costume.cyber",  1, "pack.cyber", (3, 6, 8, 11)),
    ("mine",   miner,  "costume.mine",   1, "pack.mine",  (3, 8, 11, 13)),
    ("arcane", archmage, "costume.arcane", 1, "pack.arcane", (5, 9, 13, 17)),
]

# 키포즈 — design-motion `docs/UX_MOTION_COSTUME_FOCUS.md` 16-3절 전사용 표 그대로.
#   필드 순서: (leftUpperArm=B어깨, leftLowerArm=B팔꿈치, rightUpperArm=A어깨,
#              rightLowerArm=A팔꿈치, leanDegrees, bodyOffsetY, propFrame)
KEYPOSES = {
    "office": [   # 출하본(5-2 「서류 넘기기」 3키) — 값 변경 0. 골든이 잠그고 있다.
        (-26.0, 22.0, 46.0, 66.0, -2.0,  0.000, 0),
        (-26.0, 22.0, 47.5, 27.5,  2.5, -0.018, 0),
        (-22.0, 26.0, 40.0, 78.0,  1.0,  0.000, 0),
    ],
    "cyber": [    # 16-3 「홀로 콘솔 조작」
        (48.0, 68.0, 62.0, 56.0, -2.0,  0.000, 0),
        (44.0, 72.0, 80.0, 36.5, -1.0,  0.000, 0),
        (63.0, 34.5, 58.0, 60.0, -1.0,  0.000, 0),
        (56.0, 58.0, 70.0, 44.0,  2.0, -0.018, 0),
    ],
    "mine": [     # 16-3 「광맥 두드리기」 (R2-1 · R2-2 반영)
        (-24.0, 28.0,  86.0, 34.0, -1.0,  0.000, 0),
        (-30.0, 24.0, 142.0, 52.0, -4.0,  0.018, 0),
        (-18.0, 34.0,  78.5, 70.0,  5.0, -0.030, 1),
        (-20.0, 30.0,  39.5, 58.5,  2.0, -0.020, 0),
    ],
    "arcane": [   # 16-3 「영창」 (R2-3 반영)
        (-34.0, 96.0, -30.0, 92.0, -2.0, 0.000, 0),
        (-32.0, 92.0, -34.0, 84.0,  1.5, 0.000, 0),
        (-40.0, 100.0, -26.0, 88.0, 1.0, 0.000, 0),
        (-28.0, 88.0,  16.0, 84.0, -3.0, 0.020, 0),
    ],
}

STEPS_PER_SECOND = 3


# ─────────────────────────────────────────────────────────────────────────────
# 2. 예행 게이트 (러너를 대체하지 않는다)
# ─────────────────────────────────────────────────────────────────────────────
def segs(sh):
    p, n = sh.pts, len(sh.pts)
    rng = range(n) if sh.loop else range(n - 1)
    return [(p[i], p[(i + 1) % n]) for i in rng]


def _pt_seg(p, a, b):
    vx, vy = b[0] - a[0], b[1] - a[1]
    L2 = vx * vx + vy * vy
    t = 0.0 if L2 < 1e-15 else max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / L2))
    return math.hypot(p[0] - (a[0] + vx * t), p[1] - (a[1] + vy * t))


def _seg_seg(a, b, c, d):
    if rig.seg_int(a, b, c, d):
        return 0.0
    return min(_pt_seg(a, c, d), _pt_seg(b, c, d), _pt_seg(c, a, b), _pt_seg(d, a, b))


def shape_dist(s1, s2):
    best = 9e9
    for a, b in segs(s1):
        for c, d in segs(s2):
            best = min(best, _seg_seg(a, b, c, d))
    return best


def stage_set(shapes, st):
    return [s for s in shapes if s.stage <= st]


_fail = []
_fillgap = []   # 채움 하한(2.60 W_P) 미달 — 프로덕션 규칙(2.00)은 통과한다


def check():
    print("=" * 100)
    print("  R30 예행 게이트 — 프롭 4종 x 4단계 · 금지대(2.00 W_P) + 채움 하한(2.60 W_P)")
    print("=" * 100)
    for fname, fn, key, kind, sid, counts in COSTUMES:
        shapes = fn()
        for st in range(4):
            grp = stage_set(shapes, st)
            if len(grp) != counts[st]:
                _fail.append("%s S%d 조각 %d개 (선언 %d)" % (key, st, len(grp), counts[st]))
            worst, worstlab, touching = 9e9, "", 0
            for i in range(len(grp)):
                for j in range(i + 1, len(grp)):
                    d = shape_dist(grp[i], grp[j])
                    if d <= 1e-9:
                        touching += 1
                        continue
                    if d < MIN_GAP_1x1 - 1e-9:
                        _fail.append("%s S%d 금지대 %s x %s  d=%.4f H = %.2f W_P (하한 2.00)"
                                     % (key, st, grp[i].name, grp[j].name, d, d / WP))
                    elif (grp[i].filled or grp[j].filled) and d < MIN_GAP_FILL - 1e-9:
                        pair = (key, grp[i].name, grp[j].name, round(d, 4))
                        if pair not in _fillgap:
                            _fillgap.append(pair)
                    if d < worst:
                        worst, worstlab = d, "%s x %s" % (grp[i].name, grp[j].name)
            print("  %-16s S%d  조각 %2d  닿음 %2d  최악(비접촉) %.4f H = %.2f W_P  [%s]"
                  % (key, st, len(grp), touching, worst, worst / WP, worstlab))
        # 단계별 그림이 실제로 달라야 한다(R29 0-1 R29-2)
        sig = [tuple(sorted(s.name for s in stage_set(shapes, st))) for st in range(4)]
        if len(set(sig)) != 4:
            _fail.append("%s 진화 4단계 중 서로 다른 그림이 %d개뿐이다" % (key, len(set(sig))))
        # 보조색/채움 각 1개
        for st in range(4):
            grp = stage_set(shapes, st)
            if sum(1 for s in grp if s.tone == 1) != 1:
                _fail.append("%s S%d 보조색 조각이 %d개" % (key, st, sum(1 for s in grp if s.tone == 1)))
            if sum(1 for s in grp if s.filled) != 1:
                _fail.append("%s S%d 채움 조각이 %d개" % (key, st, sum(1 for s in grp if s.filled)))

    # 오피스 골든 5개 — 테스트가 실제로 잠그는 값
    off = office()
    s1 = stage_set(off, 1)
    xs = [p[0] for s in s1 for p in s.pts]
    ys = [p[1] for s in s1 for p in s.pts]
    golden = [("propShapes.Count", len(s1), 10), ("minX", round(min(xs), 4), 0.26),
              ("maxX", round(max(xs), 4), 1.00), ("maxY", round(max(ys), 4), 1.02),
              ("stage0.Length", len(stage_set(off, 0)), 4)]
    print("\n  --- CostumeOfficeAssetTests 골든 5개 ---")
    for label, got, want in golden:
        ok = abs(got - want) < 5e-4
        print("    %-18s 실측 %-8s 골든 %-8s %s" % (label, got, want, "OK" if ok else "★ 깨짐"))
        if not ok:
            _fail.append("오피스 골든 %s: %s != %s" % (label, got, want))

    # 키포즈 안전 점검(motion 16-3 표 그대로)
    print("\n  --- 키포즈 안전 점검 ---")
    for name, keys in KEYPOSES.items():
        for k, (lu, ll, ru, rl, lean, dy, pf) in enumerate(keys):
            for lab, v in (("어깨B", lu), ("어깨A", ru)):
                if not (-60.0 <= v <= 150.0):
                    _fail.append("%s K%d %s=%s 가 [-60,150] 밖" % (name, k, lab, v))
            for lab, v in (("팔꿈치B", ll), ("팔꿈치A", rl)):
                if not (0.0 <= v <= 116.55):
                    _fail.append("%s K%d %s=%s 가 [0,116.55] 밖" % (name, k, lab, v))
            if dy != 0.0 and not (0.017 - 1e-9 <= abs(dy) <= 0.032 + 1e-9):
                _fail.append("%s K%d bodyOffsetY=%s 가 대역 밖" % (name, k, dy))
        n = len(keys)
        if not (2 <= n <= 8):
            _fail.append("%s 키 수 %d" % (name, n))
        print("    %-8s %d키  어깨 최대 %.1f  팔꿈치 최대 %.1f  |기울임| 최대 %.1f"
              % (name, n, max(max(k[0], k[2]) for k in keys),
                 max(max(k[1], k[3]) for k in keys), max(abs(k[4]) for k in keys)))

    if _fillgap:
        print("\n  --- ★ 채움 인지 하한(2.60 W_P = %.4f H) 미달 — 2.00 W_P 규칙은 통과한다 ---" % MIN_GAP_FILL)
        for key, a, b, d in _fillgap:
            print("    %-16s %-10s x %-10s d=%.4f H = %.2f W_P" % (key, a, b, d, d / WP))
        print("    ⇒ R29 6-3 이 「프롭당 최악 쌍 하나」만 새 하한으로 재서 남은 것이다(조형 축 = design-equipment).")

    print("\n" + "=" * 100)
    if _fail:
        print("결과: 위반 %d건" % len(_fail))
        for m in _fail:
            print("  - " + m)
        return 1
    print("결과: 전수 통과 (위반 0건)")
    return 0


# ─────────────────────────────────────────────────────────────────────────────
# 3. YAML 쓰기
# ─────────────────────────────────────────────────────────────────────────────
def f2s(v):
    if abs(v) < 5e-8:
        return "0"
    s = "%.7f" % v
    s = s.rstrip("0").rstrip(".")
    return s if s not in ("-0", "") else "0"


def terms_of(sh):
    out = ["%d" % len(sh.pts)]
    for x, y in sh.pts:
        for v in (x, y):
            out += ["1", "8", "0", "0", "1", f2s(v)]
    return out


def shape_yaml(sh, indent):
    p = " " * indent
    L = ["%s- name: %s" % (p, sh.name),
         "%s  loop: %d" % (p, 1 if sh.loop else 0),
         "%s  filled: %d" % (p, 1 if sh.filled else 0),
         "%s  tone: %d" % (p, sh.tone),
         "%s  swayStart: -1" % p,
         "%s  swayCount: 0" % p,
         "%s  swingDegrees: 0" % p,
         "%s  surfaces: 0" % p,
         "%s  strokeMult: 1" % p,
         "%s  strokeInR: 0" % p,
         "%s  noStroke: 0" % p,
         "%s  alpha: 0" % p,
         "%s  lineAlpha: 0" % p,
         "%s  underBack: 0" % p,
         "%s  layer: 0" % p,
         "%s  bodyFixed: 0" % p,
         "%s  terms:" % p]
    for t in terms_of(sh):
        L.append("%s  - %s" % (p, t))
    return L


HEAD = ("%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: @GUID@, type: 3}\n"
        "  m_Name: @NAME@\n"
        "  m_EditorClassIdentifier: \n")


def head(guid, name):
    return HEAD.replace("@GUID@", guid).replace("@NAME@", name)


def manifest_yaml(fname, shapes, key, kind, sid, kp_guid, mguid):
    L = [head(mguid, "CostumeManifest_" + fname)]
    body = ["  costumeKey: %s" % key,
            "  requiresSchemaVersion: 2",
            "  sourceKind: %d" % kind,
            "  sourceId: %s" % sid,
            "  displayNameKey: %s.name" % key,
            "  propShapes:"]
    for sh in stage_set(shapes, 1):
        body += shape_yaml(sh, 2)
    body.append("  propAnchorOffsetXInH: 0")
    body.append("  stageShapes:")
    for st in range(4):
        body.append("  - stage: %d" % st)
        body.append("    shapes:")
        for sh in stage_set(shapes, st):
            body += shape_yaml(sh, 4)
    body.append("  keyposes: {fileID: 11400000, guid: %s, type: 2}" % kp_guid)
    return L[0] + "\n".join(body) + "\n"


def keypose_yaml(fname, keys, sguid):
    L = [head(sguid, "CostumeKeyposeTable_" + fname)]
    body = ["  stepsPerSecond: %d" % STEPS_PER_SECOND, "  keyposes:"]
    for lu, ll, ru, rl, lean, dy, pf in keys:
        body += ["  - leftUpperArm: %s" % f2s(lu),
                 "    leftLowerArm: %s" % f2s(ll),
                 "    rightUpperArm: %s" % f2s(ru),
                 "    rightLowerArm: %s" % f2s(rl),
                 "    leanDegrees: %s" % f2s(lean),
                 "    bodyOffsetY: %s" % f2s(dy),
                 "    propFrame: %d" % pf]
    body.append("  stageKeyposeStart: []")
    return L[0] + "\n".join(body) + "\n"


META = ("fileFormatVersion: 2\n"
        "guid: %s\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n")


def existing_guid(path):
    if not os.path.exists(path):
        return None
    with open(path) as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return None


def new_guid(seed):
    import hashlib
    return hashlib.md5(("stickmate.r30." + seed).encode()).hexdigest()


def write():
    mscript = script_guid("CostumeManifestSO")
    kscript = script_guid("CostumeKeyposeTableSO")
    print("스크립트 GUID — 매니페스트 %s / 키포즈 %s" % (mscript, kscript))
    for fname, fn, key, kind, sid, counts in COSTUMES:
        mp = os.path.join(ITEMS, "CostumeManifest_%s.asset" % fname)
        kp = os.path.join(ITEMS, "CostumeKeyposeTable_%s.asset" % fname)
        mg = existing_guid(mp + ".meta") or new_guid("manifest." + fname)
        kg = existing_guid(kp + ".meta") or new_guid("keypose." + fname)

        with open(kp, "w") as f:
            f.write(keypose_yaml(fname, KEYPOSES[fname], kscript))
        with open(kp + ".meta", "w") as f:
            f.write(META % kg)
        with open(mp, "w") as f:
            f.write(manifest_yaml(fname, fn(), key, kind, sid, kg, mscript))
        with open(mp + ".meta", "w") as f:
            f.write(META % mg)
        print("  썼다 %-38s (guid %s)  sourceKind=%s sourceId=%s"
              % (os.path.basename(mp), mg, "Pack" if kind == 1 else "BaseTheme", sid))
        print("  썼다 %-38s (guid %s)  %d키" % (os.path.basename(kp), kg, len(KEYPOSES[fname])))


if __name__ == "__main__":
    rc = check()
    if "--write" in sys.argv:
        if rc != 0:
            raise SystemExit("★ 게이트가 빨간데 쓰지 않는다.")
        write()
    raise SystemExit(rc)
