# -*- coding: utf-8 -*-
"""r31 공용 — **출하 `.asset`을 직접 읽는 파서** (design-equipment, 2026-09-08).

좌표를 손으로 옮기지 않는다. `Core/AccessoryDefSO.cs`의
`AccessoryWornShapeReader.TryBuild` / `ReadSum` / `TryBasis` 문법을 그대로 옮겨
`terms` 스트림을 푼다. 기저 `Height(8)`를 1.0으로 두면 좌표가 곧 **H 배수**로 나온다.

    from r31_assetparse import load_manifest
    m = load_manifest("cyber")     # m["propShapes"], m["stage"][0..3]

★ 이 파서가 옳은지는 **알려진 값**으로 먼저 맞춘다(`selftest()`).
  교정이 깨지면 이 파일을 쓰는 모든 숫자를 폐기한다.
"""
import os, re, math

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ITEMS = os.path.join(REPO, "Assets", "_Project", "Resources", "Items")

BASIS_HEIGHT = 8
GATE_ALWAYS, GATE_ON, GATE_OFF = 0, 1, 2
TRIG_NONE, TRIG_COS, TRIG_SIN = 0, 1, 2


class Piece:
    __slots__ = ("name", "pts", "loop", "filled", "tone", "noStroke")

    def __init__(self, name, pts, loop, filled, tone, noStroke=False):
        self.name, self.pts = name, pts
        self.loop, self.filled, self.tone, self.noStroke = loop, filled, tone, noStroke

    def __repr__(self):
        return "Piece(%s, n=%d, loop=%s, filled=%s, tone=%d)" % (
            self.name, len(self.pts), self.loop, self.filled, self.tone)


def _read_sum(t, i, height, state_on):
    """AccessoryWornShapeReader.ReadSum 그대로 (swung 항은 이 데이터에 없다)."""
    terms = int(t[i]); i += 1
    acc, any_ = 0.0, False
    for _ in range(terms):
        basis, gate, trig, ncoef = int(t[i]), int(t[i+1]), int(t[i+2]), int(t[i+3])
        i += 4
        if basis == BASIS_HEIGHT:
            v = height
        elif basis in (0, 1, 2, 3, 4, 5):
            raise SystemExit("이 파서는 H(8) 기저만 다룬다 — 기저 %d 가 나왔다." % basis)
        else:
            raise SystemExit("모르는 기저 %d" % basis)
        for c in range(ncoef):
            coef = t[i + c]
            if c == ncoef - 1:
                if trig == TRIG_COS: coef = math.cos(coef)
                elif trig == TRIG_SIN: coef = math.sin(coef)
            v *= coef
        i += ncoef
        if gate == GATE_ON and not state_on: continue
        if gate == GATE_OFF and state_on: continue
        acc = acc + v if any_ else v
        any_ = True
    return (acc if any_ else 0.0), i


def build(terms, height=1.0, facing=1.0, state_on=False):
    """AccessoryWornShapeReader.TryBuild 그대로."""
    i = 0
    count = int(terms[i]); i += 1
    pts = []
    for _ in range(count):
        x, i = _read_sum(terms, i, height, state_on)
        y, i = _read_sum(terms, i, height, state_on)
        pts.append((x * facing, y))
    if i != len(terms):
        raise SystemExit("스트림 %d칸 중 %d칸만 쓰였다 — 점 하나가 통째로 빠졌다." % (len(terms), i))
    return pts


_INT_KEYS = ("loop", "filled", "tone", "noStroke", "stage")


def load_manifest(short):
    """CostumeManifest_<short>.asset → {'key','propShapes':[Piece],'stage':{n:[Piece]}}"""
    path = os.path.join(ITEMS, "CostumeManifest_%s.asset" % short)
    lines = open(path, encoding="utf-8").read().splitlines()

    out = {"path": path, "key": None, "propShapes": [], "stage": {}}
    cur_list = None          # 지금 조각을 담는 리스트
    cur = None               # 지금 읽는 조각 dict
    terms = None             # terms 수집 중이면 list
    seen_stage_header = False

    def flush():
        nonlocal cur, terms
        if cur is None: return
        pts = build(terms if terms is not None else [])
        cur_list.append(Piece(cur.get("name", "?"), pts, bool(cur.get("loop", 0)),
                              bool(cur.get("filled", 0)), int(cur.get("tone", 0)),
                              bool(cur.get("noStroke", 0))))
        cur, terms = None, None

    i = 0
    pending_stage = None
    while i < len(lines):
        ln = lines[i]
        s = ln.strip()
        if s.startswith("costumeKey:"):
            out["key"] = s.split(":", 1)[1].strip()
        elif s == "propShapes:":
            flush(); cur_list = out["propShapes"]; terms = None
        elif s == "stageShapes:":
            flush(); seen_stage_header = True; cur_list = None
        elif seen_stage_header and re.match(r"^- stage: \d+$", s):
            flush()
            pending_stage = int(s.split(":", 1)[1])
            out["stage"][pending_stage] = []
            cur_list = out["stage"][pending_stage]
        elif s == "shapes:":
            pass
        elif re.match(r"^-? ?name: ", s) and cur_list is not None:
            flush()
            cur = {"name": s.split(":", 1)[1].strip()}
            terms = None
        elif cur is not None and s == "terms:":
            terms = []
        elif cur is not None and terms is not None and s.startswith("- "):
            terms.append(float(s[2:]))
        elif cur is not None:
            m = re.match(r"^([A-Za-z]+): (-?[\d.]+)$", s)
            if m and m.group(1) in _INT_KEYS:
                cur[m.group(1)] = float(m.group(2))
        elif s.startswith("keyposes:"):
            flush(); cur_list = None
        i += 1
    flush()
    return out


def selftest(verbose=True):
    """★ 교정 — 프로덕션 테스트/문서가 이미 못박은 값으로 파서를 먼저 맞춘다."""
    checks = []
    off = load_manifest("office")
    # CostumeOfficeAssetTests 의 골든 5개 (남이 쓴 숫자다)
    checks.append(("office propShapes 조각 수", len(off["propShapes"]), 10))
    checks.append(("office stage0 조각 수", len(off["stage"][0]), 4))
    xs = [p[0] for sh in off["propShapes"] for p in sh.pts]
    ys = [p[1] for sh in off["propShapes"] for p in sh.pts]
    checks.append(("office 근단 X", round(min(xs), 4), 0.26))
    checks.append(("office 원단 X", round(max(xs), 4), 1.00))
    checks.append(("office 상단 Y", round(max(ys), 4), 1.02))
    # CostumePackCostumeAssetTests.GoldenStageCounts
    for short, want in (("cyber", (3, 6, 8, 11)), ("mine", (3, 8, 11, 13)), ("arcane", (5, 9, 13, 17))):
        m = load_manifest(short)
        got = tuple(len(m["stage"][s]) for s in range(4))
        checks.append(("%s 단계별 조각 수" % short, got, want))
    bad = [c for c in checks if c[1] != c[2]]
    if verbose:
        for nm, got, want in checks:
            print("   %s %-24s 파서=%s  기대=%s" % ("OK" if got == want else "x ", nm, got, want))
    if bad:
        raise SystemExit("★ 파서 교정 실패 %d건 — 이 파서를 쓰는 숫자는 전부 폐기한다." % len(bad))
    return True


if __name__ == "__main__":
    print("=" * 92)
    print("  r31 파서 교정 — 알려진 값으로 먼저 맞춘다")
    print("=" * 92)
    selftest()
    print("\n  교정 통과. 아래는 채움(filled) 조각 전수:")
    for short in ("office", "cyber", "mine", "arcane"):
        m = load_manifest(short)
        for grp, pieces in [("base", m["propShapes"])] + [("S%d" % k, v) for k, v in sorted(m["stage"].items())]:
            for p in pieces:
                if p.filled:
                    print("   %-8s %-5s %-10s n=%d loop=%s tone=%d" % (short, grp, p.name, len(p.pts), p.loop, p.tone))
