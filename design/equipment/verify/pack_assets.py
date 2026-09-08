# -*- coding: utf-8 -*-
"""프로덕션 «있는 그대로» 읽기 — 손으로 베끼지 않는다 (cards42.py 가 .asset 을 직접 파싱하는 것과 같은 이유).

  · parse_asset(path)      Resources/Items/*.asset 의 wornShapes 를 AccessoryWornShapeReader.TryBuild 와 같은
                           문법으로 풀어 머리 중심 원점 · R 배수 좌표로 돌려준다(기저: 프레임을 R 단위로 둔다).
  · pack_items()           pack_*.asset 12종 → {(슬롯, 팩): (표시명, [piece dict])}
  · prod_base()            Tools/ShapeDump/build.sh 가 뽑은 프로덕션 좌표(기본 24종 + 머리 6종).
                           캐시 파일 pack_detail_r2.prod.tsv 를 읽고, --refresh-prod 로 다시 뽑는다.
piece dict = name · loop · filled · tone · strokeInR · strokeMult · noStroke · lineAlpha · layer · pts[(x,y)]
"""
import os, re, glob, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ITEMS = os.path.join(REPO, "Assets", "_Project", "Resources", "Items")
PROD_TSV = os.path.join(HERE, "pack_detail_r2.prod.tsv")
SLOTNAME = {0: "HEAD", 1: "EYES", 2: "NECK", 3: "BACK"}

# 머리 중심 원점 · R 단위 프레임 (rig.py 의 상수와 같은 값)
FRAME_R = {0: 1.0, 1: 3.7727273, 2: -1.3181818 + 0.04, 3: -1.3181818, 4: 0.0, 5: -5.0909091, 8: 10.33952}


def decode_terms(t):
    """AccessoryWornShapeReader.TryBuild 재현 (swing 없음 · gate Always 만 — 팩 12종 실측이 그렇다)."""
    i = 0
    n = int(t[i]); i += 1
    pts = []
    for _ in range(n):
        xy = []
        for _c in range(2):
            terms = int(t[i]); i += 1
            acc = 0.0
            for _k in range(terms):
                basis, gate, trig, nc = int(t[i]), int(t[i+1]), int(t[i+2]), int(t[i+3]); i += 4
                assert gate == 0 and trig == 0 and basis in FRAME_R, ("지원하지 않는 항", basis, gate, trig)
                v = FRAME_R[basis]
                for c in range(nc): v *= t[i+c]
                i += nc
                acc += v
            xy.append(acc)
        pts.append((xy[0], xy[1]))
    assert i == len(t), ("스트림 길이 불일치", i, len(t))
    return pts


def parse_asset(path):
    txt = open(path, encoding="utf-8").read()
    disp = re.search(r"^  displayName: (.*)$", txt, re.M).group(1).strip()
    slot = int(re.search(r"^  slot: (\d+)$", txt, re.M).group(1))
    body = txt.split("  wornShapes:")[1].split("  wornGroupAlpha:")[0] if "  wornShapes:" in txt else ""
    shapes = []
    for blk in re.split(r"^  - name: ", body, flags=re.M)[1:]:
        name = blk.split("\n")[0].strip()
        g = lambda k: float(re.search(r"^    %s: ([-\d.e]+)$" % k, blk, re.M).group(1))
        terms = [float(v) for v in re.findall(r"^    - ([-\d.e]+)$", blk.split("terms:")[1], re.M)]
        shapes.append(dict(name=name, loop=bool(int(g("loop"))), filled=bool(int(g("filled"))), tone=int(g("tone")),
                           strokeInR=g("strokeInR"), strokeMult=g("strokeMult"), noStroke=bool(int(g("noStroke"))),
                           lineAlpha=g("lineAlpha"), layer=int(g("layer")), pts=decode_terms(terms)))
    return disp, slot, shapes


def pack_items():
    out = {}
    for f in sorted(glob.glob(os.path.join(ITEMS, "pack_*.asset"))):
        disp, slot, shapes = parse_asset(f)
        pack = os.path.basename(f).split("_")[1]
        out[(SLOTNAME[slot], pack)] = (disp, shapes)
    return out


def _parse_dump(path):
    items = {}; cur = None
    for line in open(path, encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        if f[0] == "@ITEM":
            cur = (f[1], f[2]); items[cur] = []
        elif f[0] == "@SHAPE" and cur:
            pts = [tuple(float(v) for v in p.split(",")) for p in f[6:]]
            items[cur].append(dict(name=f[1], loop=bool(int(f[2])), filled=bool(int(f[3])), tone=int(f[4]),
                                   sort=int(f[5]), strokeInR=0.0, strokeMult=1.0, noStroke=False, lineAlpha=1.0,
                                   layer=(1 if int(f[5]) < 0 else 0), pts=pts))
    return items


def prod_base(refresh=False):
    """기본 24종(+머리 6종) 프로덕션 좌표. 캐시가 없거나 refresh 면 Tools/ShapeDump/build.sh 를 돌린다
    (Unity 를 띄우지 않는다 — 번들 Roslyn 으로 콘솔 도구만 컴파일한다)."""
    if refresh or not os.path.exists(PROD_TSV):
        out = subprocess.run([os.path.join(REPO, "Tools", "ShapeDump", "build.sh")], capture_output=True, text=True)
        if out.returncode != 0:
            sys.exit("ShapeDump 실패 rc=%d\n%s" % (out.returncode, out.stderr[-2000:]))
        open(PROD_TSV, "w", encoding="utf-8").write(out.stdout)
    items = _parse_dump(PROD_TSV)
    base = {}
    for (cat, name), shapes in items.items():
        base.setdefault(cat, []).append((name, shapes))
    return base


if __name__ == "__main__":
    for k, (disp, sh) in pack_items().items():
        print(k, disp, [(s["name"], len(s["pts"])) for s in sh])
    b = prod_base("--refresh-prod" in sys.argv)
    for cat, L in b.items():
        print(cat, [(n, len(s)) for n, s in L])
