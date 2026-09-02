# -*- coding: utf-8 -*-
"""R3 — **프로덕션 좌표**를 직접 읽는다(설계 거울 items.py 가 아니라).

Tools/ShapeDump/build.sh 는 Unity 를 띄우지 않고 AccessoryShapeBuilder.cs 를 **그대로 컴파일해
실행**한다. 즉 이 모듈이 돌려주는 좌표는 화면에 나가는 것과 같은 코드가 만든 것이다.
설계 거울(items.py)은 여기서 **검증 대상**이지 입력이 아니다 — 거울이 낡아도 이 라운드의
숫자는 안 흔들린다(2026-09-02 mirrordrift 사고와 같은 형태를 구조적으로 차단).
"""
import os, subprocess, tempfile

ROOT = "/Users/kjmoon/App/StickMate"
BUILD = os.path.join(ROOT, "Tools", "ShapeDump", "build.sh")


class PShape:
    __slots__ = ("name", "loop", "filled", "tone", "sort", "pts")
    def __init__(self, name, loop, filled, tone, sort, pts):
        self.name, self.loop, self.filled = name, loop, filled
        self.tone, self.sort, self.pts = tone, sort, pts
    def shifted(self, dx, dy):
        return PShape(self.name, self.loop, self.filled, self.tone, self.sort,
                      [(x + dx, y + dy) for x, y in self.pts])
    def __repr__(self):
        return "PShape(%s,%d점,sort=%d)" % (self.name, len(self.pts), self.sort)


_CACHE = None

def dump(force=False):
    """(cats, cover, W, rarity, log) — cats[cat][name] = [PShape...]"""
    global _CACHE
    if _CACHE is not None and not force:
        return _CACHE
    env = dict(os.environ)
    env["SHAPEDUMP_OUT"] = tempfile.mkdtemp(prefix="shapedump_")
    r = subprocess.run([BUILD], capture_output=True, text=True, env=env)
    if r.returncode != 0:
        raise SystemExit("!! Tools/ShapeDump/build.sh 실패 — 프로덕션 좌표를 못 뽑는다.\n"
                         + r.stdout[-2000:] + "\n" + r.stderr[-2000:])
    cats, cover, W, rarity, log = {}, {}, None, [], None
    cat = name = None
    for line in r.stdout.splitlines():
        f = line.split("\t")
        if f[0] == "@ITEM":
            cat, name = f[1], f[2]; cats.setdefault(cat, {})[name] = []
        elif f[0] == "@SHAPE":
            cats[cat][name].append(PShape(
                f[1], f[2] == "1", f[3] == "1", int(f[4]), int(f[5]),
                [tuple(float(v) for v in p.split(",")) for p in f[6:]]))
        elif f[0] == "@COVER":
            cover[int(f[1])] = float("inf") if f[2] == "inf" else float(f[2])
        elif f[0] == "@W":
            W = float(f[1])
        elif f[0] == "@RARITY":
            rarity.append(tuple(f[1:]))
        elif f[0] == "@LOG":
            log = (int(f[1]), int(f[2]))
    _CACHE = (cats, cover, W, rarity, log)
    return _CACHE
