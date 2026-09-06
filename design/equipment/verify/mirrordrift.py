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

    python3 mirrordrift.py --selftest   # ★ 탐지 경로 생존 + 양성/음성 대조 3종

★ --selftest 가 왜 필요한가 (2026-09-03, docs/TEAM.md 「미러 검증 3종」)
------------------------------------------------------------------
이 저장소는 **초록이 「일치한다」가 아니라 「아무것도 안 쟀다」였던 사고**를 여러 번 냈다.
같은 가족의 실패가 최소 셋이다 — `--selftest`가 `errors != 0`만 보고 통과를 찍은 건,
`grep -v Hub`가 도는 Unity를 지운 건, `pgrep -f`가 자기 형제 셸을 잡은 건.
**「어긋남 0건」은 그 자체로는 아무것도 증명하지 않는다.** 아래 셋을 같이 봐야 한다:

  1. 탐지 경로 생존 — 설계 거울에 고의로 어긋남을 심으면 **실제로 빨개지는가**
  2. 양성 대조     — 심은 **그 도형이** 지목되는가 (에러가 났다는 것만으로는 부족하다)
  3. 음성 대조     — 안 심은 도형은 **조용한가** (전부 빨개지는 자도 아무것도 안 잰다)

★ 덤프가 **비어 있어도** main()은 카테고리마다 ✗를 찍어 빨개진다. 그 형태를 「탐지됨」으로
  오해하지 않도록 selftest는 **도형 개수 census부터** 찍는다 — 0이면 그 뒤는 전부 무의미하다.

허용 오차는 dump가 float32를 거치며 생기는 반올림뿐(1e-4 R = 배율 0.75에서 0.0006pt).
"""
import os, subprocess, sys, math

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
BUILD = os.path.join(ROOT, "Tools", "ShapeDump", "build.sh")
TOL = 1e-4
VERBOSE = "-v" in sys.argv
SELFTEST = "--selftest" in sys.argv


def production():
    """AccessoryShapeBuilder.cs가 실제로 만드는 좌표 (머리 중심 원점 · R 배수)."""
    raw = subprocess.run([BUILD], capture_output=True, text=True)
    if raw.returncode != 0:
        print(raw.stdout); print(raw.stderr)
        raise SystemExit("!! Tools/ShapeDump/build.sh 실패 — 프로덕션 좌표를 뽑을 수 없다.")
    # ★ 2026-09-06 — 성공했을 때의 stderr 를 <b>버리지 않는다</b>. build.sh 는 컴파일 목록이
    #   의존성에 뒤처지면 자동 보강 후 «★★★» 배너를 stderr 로 낸다. 그것을 여기서 삼키면
    #   "덤프가 성공했다"와 "덤프가 겨우 살아났다"가 화면에서 똑같아진다 — 이 저장소의 표준 병이다.
    #   (prodverify.py 는 이미 같은 이유로 stderr 를 찍는다.)
    if raw.stderr.strip():
        print("── 덤프 경고 (stderr) ──"); print(raw.stderr.rstrip())
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


def compare(design, prod, quiet=False):
    """설계 거울 ↔ 프로덕션 대조. (어긋남 건수, 지목된 태그 집합)을 돌려준다."""
    bad, hits = 0, set()

    def say(msg):
        if not quiet: print(msg)

    for cat, table in design.items():
        if cat not in prod:
            say("  ✗ %s 카테고리가 덤프에 없다" % cat); bad += 1; hits.add(cat); continue
        for item, shapes in table.items():
            ps = prod[cat].get(item)
            if ps is None:
                say("  ✗ %s %s 이(가) 프로덕션에 없다" % (cat, item))
                bad += 1; hits.add("%s/%s" % (cat, item)); continue
            if len(ps) != len(shapes):
                say("  ✗ %s %s 도형 수 설계 %d ≠ 프로덕션 %d" % (cat, item, len(shapes), len(ps)))
                bad += 1; hits.add("%s/%s" % (cat, item)); continue
            for d, p in zip(shapes, ps):
                tag = "%s/%s/%s" % (cat, item, d.name)
                label = "%s %s '%s'" % (cat, item, d.name)
                if d.name != p["name"]:
                    say("  ✗ %s 이름 ≠ '%s'" % (label, p["name"])); bad += 1; hits.add(tag); continue
                if d.loop != p["loop"] or bool(d.filled) != p["filled"] or d.tone != p["tone"]:
                    say("  ✗ %s 속성 설계(loop=%s,fill=%s,tone=%d) ≠ 프로덕션(loop=%s,fill=%s,tone=%d)"
                        % (label, d.loop, bool(d.filled), d.tone, p["loop"], p["filled"], p["tone"]))
                    bad += 1; hits.add(tag); continue
                e = _align(d.pts, p["pts"], d.loop)
                if e is None:
                    say("  ✗ %s 점 수 설계 %d ≠ 프로덕션 %d" % (label, len(d.pts), len(p["pts"])))
                    bad += 1; hits.add(tag); continue
                if e > TOL:
                    say("  ✗ %s 최대 점오차 %.4f R (= %.2f획 @0.75)" % (label, e, e / 0.343864))
                    bad += 1; hits.add(tag)
                    if VERBOSE and not quiet:
                        for i, (u, v) in enumerate(zip(d.pts, p["pts"])):
                            if math.dist(u, v) > TOL:
                                print("        %2d  설계 (%+.3f,%+.3f)  프로덕션 (%+.3f,%+.3f)"
                                      % (i, u[0], u[1], v[0], v[1]))
    return bad, hits


def selftest():
    """★ 미러 검증 3종. 하나라도 빠지면 이 자의 초록은 아무것도 증명하지 않는다."""
    sys.path.insert(0, HERE)
    import copy
    import items, hair
    prod = production()
    design = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK,
              "BACK": items.BACK, "HAIR": hair.SET}

    print("╔══ mirrordrift --selftest ══╗")

    # ── 0. census — 덤프가 비어 있으면 아래 전부가 무의미하다 ──
    n_prod = sum(len(v) for cat in prod.values() for v in cat.values())
    n_design = sum(len(v) for cat in design.values() for v in cat.values())
    print("  [0] census  프로덕션 도형 %d개 · 설계 거울 도형 %d개" % (n_prod, n_design))
    if n_prod == 0 or n_design == 0:
        print("  ✗✗ 한쪽이 비어 있다. 「어긋남 0」이든 「전부 어긋남」이든 아무 뜻이 없다.")
        return 1
    ok = True

    # ── 1. 음성 대조 — 손대지 않은 상태는 조용해야 한다 ──
    base_bad, base_hits = compare(design, prod, quiet=True)
    print("  [1] 음성 대조 (원본 그대로)                       어긋남 %d건 %s"
          % (base_bad, "OK" if base_bad == 0 else "✗ 원본부터 어긋나 있다"))
    if base_bad: ok = False

    # ── 2. 탐지 경로 생존 + 양성 대조 — 심은 그 도형«만» 지목되는가 ──
    victim_cat, victim_item = "HEAD", next(iter(design["HEAD"]))
    victim = design[victim_cat][victim_item][0]
    tag = "%s/%s/%s" % (victim_cat, victim_item, victim.name)
    hurt = copy.deepcopy(victim)
    hurt.pts = [(x + TOL * 10, y) for x, y in victim.pts]      # 허용오차의 10배
    poisoned = dict(design)
    poisoned[victim_cat] = dict(design[victim_cat])
    poisoned[victim_cat][victim_item] = [hurt] + list(design[victim_cat][victim_item][1:])

    bad, hits = compare(poisoned, prod, quiet=True)
    alive = bad > 0
    named = tag in hits
    only = hits == {tag}
    print("  [2] 탐지 경로 생존 (%s 를 %.0e R 밀었다)" % (tag, TOL * 10))
    print("        → 어긋남 %d건 %s"
          % (bad, "OK" if alive else "✗✗ 안 빨개졌다 — 이 자는 죽어 있다"))
    print("  [3] 양성 대조 — 심은 그 도형이 지목됐는가          %s"
          % ("OK" if named else "✗ 다른 것만 지목됐다: %s" % sorted(hits)))
    print("  [4] 음성 대조 — 안 심은 도형은 조용한가            %s"
          % ("OK (지목 1건뿐)" if only else "✗ 부수 지목 %d건: %s"
             % (len(hits) - 1, sorted(hits - {tag}))))
    if not (alive and named and only): ok = False

    print("╚══ selftest %s ══╝" % ("통과 — 위 「어긋남 0건」은 신뢰할 수 있다"
                                   if ok else "★ 실패 — 이 자의 초록을 믿지 마라"))
    return 0 if ok else 1


def main():
    if SELFTEST:
        return selftest()
    sys.path.insert(0, HERE)
    import items, hair
    prod = production()
    design = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK,
              "BACK": items.BACK, "HAIR": hair.SET}
    print("╔══ 설계 거울 ↔ 프로덕션 좌표 대조 (허용 %.0e R) ══╗" % TOL)
    bad, _ = compare(design, prod)
    print("╚══ 어긋남 %d건 ══╝" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
