# -*- coding: utf-8 -*-
"""★ 거울 어긋남 감시 — items.py / hair.py(설계 거울) vs AccessoryShapeBuilder.cs(프로덕션).

왜 이 파일이 생겼나 (2026-09-02, design-equipment)
--------------------------------------------------
모자 처방이 프로덕션에 들어간 뒤에도 `items.py`에는 **옛 좌표**가 남아 있었다. 그 상태에서
  · `Tools/ShapeDump/prodverify.py`  -> 위반 0건  (프로덕션을 잰다)
  · `design/equipment/verify/verify.py` -> 위반 4건 (설계 거울을 잰다)
두 자가 서로 다른 물건을 재고 있었고, **신규 아이템을 설계 거울 위에서 검산하면 그 숫자는
프로덕션에 대한 보장이 아니다**(쌍별 실루엣 차는 이웃 6종의 좌표에 직접 의존한다).

이 저장소가 "Dock 낙차 4:2 갈림"으로 이미 크게 데인 그 실패 패턴이라 감시자를 둔다.

    python3 mirrordrift.py          # 어긋난 도형 목록 (종료코드 1이면 어긋남)
    python3 mirrordrift.py -v       # 점 단위 차이까지

허용 오차는 dump가 float32를 거치며 생기는 반올림뿐(1e-4 R = 배율 0.75에서 0.0006pt).
"""
import os, subprocess, sys, math

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
BUILD = os.path.join(ROOT, "Tools", "ShapeDump", "build.sh")
TOL = 1e-4
VERBOSE = "-v" in sys.argv


def production():
    """AccessoryShapeBuilder.cs가 실제로 만드는 좌표 (머리 중심 원점 · R 배수)."""
    raw = subprocess.run([BUILD], capture_output=True, text=True)
    if raw.returncode != 0:
        print(raw.stdout); print(raw.stderr)
        raise SystemExit("!! Tools/ShapeDump/build.sh 실패 — 프로덕션 좌표를 뽑을 수 없다.")
    cats, cat, name = {}, None, None
    for line in raw.stdout.splitlines():
        f = line.split("\t")
        if f[0] == "@ITEM":
            cat, name = f[1], f[2]
            cats.setdefault(cat, {})[name] = []
        elif f[0] == "@SHAPE":
            cats[cat][name].append(dict(
                name=f[1], loop=f[2] == "1", filled=f[3] == "1", tone=int(f[4]),
                pts=[tuple(float(v) for v in p.split(",")) for p in f[6:]]))
    return cats


def _align(a, b, loop):
    """루프는 시작점/방향이 달라도 같은 도형이다. 가장 잘 맞는 정렬의 최대 점오차를 돌려준다."""
    if len(a) != len(b): return None
    n = len(a)
    cands = [b]
    if loop:
        cands = [b[k:] + b[:k] for k in range(n)]
        rb = list(reversed(b))
        cands += [rb[k:] + rb[:k] for k in range(n)]
    best = None
    for c in cands:
        e = max(math.dist(a[i], c[i]) for i in range(n))
        if best is None or e < best: best = e
    return best


def main():
    sys.path.insert(0, HERE)
    import items, hair
    prod = production()
    design = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK,
              "BACK": items.BACK, "HAIR": hair.SET}
    bad = 0
    print("╔══ 설계 거울 ↔ 프로덕션 좌표 대조 (허용 %.0e R) ══╗" % TOL)
    for cat, table in design.items():
        if cat not in prod:
            print("  ✗ %s 카테고리가 덤프에 없다" % cat); bad += 1; continue
        for item, shapes in table.items():
            ps = prod[cat].get(item)
            if ps is None:
                print("  ✗ %s %s 이(가) 프로덕션에 없다" % (cat, item)); bad += 1; continue
            if len(ps) != len(shapes):
                print("  ✗ %s %s 도형 수 설계 %d ≠ 프로덕션 %d" % (cat, item, len(shapes), len(ps)))
                bad += 1; continue
            for d, p in zip(shapes, ps):
                tag = "%s %s '%s'" % (cat, item, d.name)
                if d.name != p["name"]:
                    print("  ✗ %s 이름 ≠ '%s'" % (tag, p["name"])); bad += 1; continue
                if d.loop != p["loop"] or bool(d.filled) != p["filled"] or d.tone != p["tone"]:
                    print("  ✗ %s 속성 설계(loop=%s,fill=%s,tone=%d) ≠ 프로덕션(loop=%s,fill=%s,tone=%d)"
                          % (tag, d.loop, bool(d.filled), d.tone, p["loop"], p["filled"], p["tone"]))
                    bad += 1; continue
                e = _align(d.pts, p["pts"], d.loop)
                if e is None:
                    print("  ✗ %s 점 수 설계 %d ≠ 프로덕션 %d" % (tag, len(d.pts), len(p["pts"])))
                    bad += 1; continue
                if e > TOL:
                    print("  ✗ %s 최대 점오차 %.4f R (= %.2f획 @0.75)" % (tag, e, e / 0.343864))
                    bad += 1
                    if VERBOSE:
                        for i, (u, v) in enumerate(zip(d.pts, p["pts"])):
                            if math.dist(u, v) > TOL:
                                print("        %2d  설계 (%+.3f,%+.3f)  프로덕션 (%+.3f,%+.3f)"
                                      % (i, u[0], u[1], v[0], v[1]))
    print("╚══ 어긋남 %d건 ══╝" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
