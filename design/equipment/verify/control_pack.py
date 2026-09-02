# -*- coding: utf-8 -*-
"""★ 양성 대조 — 일부러 나쁜 값을 넣었을 때 게이트가 **실제로** 빨간불을 내는가.

왜 필요한가: 2026-09-01 라운드에서 이 담당자의 대조가 한 번 **거짓 초록**을 냈다
(변형이 좌표에 적용되지 않았는데 "게이트 통과"로 읽혔다). 그래서 이 파일은 매 대조마다
 ① 좌표가 **실제로 달라졌는지** 먼저 단언하고 (안 달라졌으면 그 자체가 실패다)
 ② 그다음에 목표 게이트가 빨간불인지 본다.

    python3 pack_office.py --control
"""
import sys, os, io, copy, math, contextlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, sectors as S, headroom as H, hatfix
from rig import Shape
import pack_office as P


def clone(pack=None):
    pack = pack or P.PACK
    return {k: (n, [Shape(s.name, [tuple(p) for p in s.pts], s.loop, s.filled, s.tone) for s in sh])
            for k, (n, sh) in pack.items()}


def quiet(fn, *a, **kw):
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        r = fn(*a, **kw)
    return r, buf.getvalue()


def coords(pack):
    return [tuple(p) for _, sh in pack.values() for s in sh for p in s.pts]


# ---------------------------------------------------------------------------
CASES = []
def case(name, gate):
    def deco(f):
        CASES.append((name, gate, f)); return f
    return deco


@case("최단 변 (0.32R = 0.74획@0.60)", "minedge")
def c_rule1(pk):
    _, sh = pk["HEAD"]
    cup = [s for s in sh if s.name == "HeadsetCup"][0]
    cup.pts[2] = (1.22, 0.02)          # 세로변 0.44R -> 0.32R


@case("규칙 1 — 배율 0.60 전용(0.75는 통과)", "scale60")
def c_rule1_60(pk):
    _, sh = pk["HEAD"]
    cup = [s for s in sh if s.name == "HeadsetCup"][0]
    cup.pts[2] = (1.22, 0.10); cup.pts[3] = (1.22, -0.26)   # 세로변 0.36R: 1.05획@0.75 / 0.84획@0.60


@case("규칙 1-C (색면이 획보다 얇다)", "1c")
def c_rule1c(pk):
    _, sh = pk["HEAD"]
    mic = [s for s in sh if s.name == "HeadsetMic"][0]
    c = (sum(p[0] for p in mic.pts)/4, sum(p[1] for p in mic.pts)/4)
    mic.pts[:] = P.plate(c[0], c[1], 0.72, 0.24, 0.0)        # 두께 0.46 -> 0.24


@case("예약 대역 (돌출이 봉투 안으로 들어감)", "sector")
def c_sector(pk):
    _, sh = pk["BACK"]
    for s in sh:
        if s.name in ("BlazerSleeve", "BlazerCuff"):
            s.pts[:] = [(x*0.42, y) for x, y in s.pts]        # 소매를 앞으로 못 나가게 눌러 붙인다


@case("실루엣 래칫 (기존 아이템의 복제)", "ratchet")
def c_ratchet(pk):
    import items
    pk["HEAD"] = ("헤드셋", [Shape(s.name, [(x*1.01, y*1.01) for x, y in s.pts],
                                  s.loop, s.filled, s.tone) for s in items.HEAD["야구모자"]])


@case("세트 모티프 (판이 팩 부류를 벗어남)", "motif")
def c_motif(pk):
    _, sh = pk["NECK"]
    card = [s for s in sh if s.name == "BadgeCard"][0]
    card.pts[:] = [(-0.34,-2.02), (0.34,-2.02), (0.34,-2.70), (-0.34,-2.70)]   # 종횡비 1.0


@case("모티프 두께 (착용 크기에서 획보다 얇음)", "motif")
def c_motif_thin(pk):
    _, sh = pk["EYES"]
    g = [s for s in sh if s.name == "OfficeGlare"][0]
    g.pts[:] = [(0.24, 0.04), (0.24,-0.36), (1.10,-0.36), (1.10, 0.04)]        # 두께 0.40R


@case("등급 (전설인데 그늘선을 뺌)", "grade")
def c_grade(pk):
    n, sh = pk["BACK"]
    pk["BACK"] = (n, [s for s in sh if s.name != "BlazerFold"])


@case("남는 머리 (밴드가 머리를 덮는 뚜껑이 됨)", "headroom")
def c_headroom(pk):
    _, sh = pk["HEAD"]
    band = [s for s in sh if s.name == "HeadsetBand"][0]
    # ★ 첫 대조는 밴드를 1.05R 내리기만 했는데 **초록이 나왔다** — 잉크 밑단이 −0.385R라
    #   하한(−0.570R)을 아직 안 넘었기 때문이다. 대조가 약했던 것이지 게이트가 샌 게 아니다.
    band.pts[:] = [(-1.10, 0.60), (0.0, 1.35), (1.10, 0.60),
                   (1.10,-0.55), (0.0,-0.75), (-1.10,-0.55)]


@case("눈 커버 (앞눈을 덮는 채움이 없어짐)", "verify")
def c_eye(pk):
    _, sh = pk["EYES"]
    for s in sh:
        if s.name in ("OfficeLensFront", "OfficeGlare"):
            s.pts[:] = [(x + 1.4, y) for x, y in s.pts]


# ---------------------------------------------------------------------------
def run_gate(gate, pk):
    """그 게이트만 돌려 '빨간불 개수'를 돌려준다."""
    m = P.install(pk)
    if gate == "verify":
        os.chdir(os.path.dirname(os.path.abspath(__file__)))
        g = {"__name__": "__main__"}
        _, out = quiet(lambda: exec(compile(open("verify.py", encoding="utf-8").read(),
                                            "verify.py", "exec"), g))
        return g.get("fail", 0)
    if gate == "scale60":
        def Wf(s): return max(0.048*s, 2.0/35.25) / (0.22*s)
        base = 9          # 기존 30종이 배율 0.60에서 이미 갖고 있는 위반 수(실측)
        n = sum(1 for cat in ("HEAD", "EYES", "NECK", "BACK")
                for _, sh in getattr(m, cat).items() for x in sh if rig.rule_one(x, Wf(0.60)))
        n75 = sum(1 for cat in ("HEAD", "EYES", "NECK", "BACK")
                  for _, sh in getattr(m, cat).items() for x in sh if rig.rule_one(x, Wf(0.75)))
        return max(0, n - (base - 1)) if n75 == 0 else 0     # 0.75는 통과인데 0.60만 늘었는가
    if gate == "minedge":  return len(quiet(P.min_edge_report, pk)[0])
    if gate == "1c":       return len(quiet(P.rule_1c, pk)[0])
    if gate == "sector":   return len(quiet(P.sector_report, m, pk)[0])
    if gate == "ratchet":  return len(quiet(P.ratchet_report, m)[0])
    if gate == "motif":    return len(quiet(P.motif_report, pk)[0])
    if gate == "grade":    return len(quiet(P.grade_report, m, pk)[0])
    if gate == "headroom": return len(quiet(P.headroom_report, pk)[0])
    raise KeyError(gate)


if __name__ == "__main__":
    base_coords = coords(P.PACK)
    print("╔══ 양성 대조 — 나쁜 값을 넣으면 게이트가 빨간불을 내는가 ══╗")
    print("  (먼저 본안이 초록인지 확인한다 — 그래야 대조의 빨간불이 대조 때문이다)")
    for gate in ("verify", "minedge", "1c", "sector", "ratchet", "motif", "grade", "headroom"):
        n = run_gate(gate, clone())
        print("   본안 %-9s -> %d건 %s" % (gate, n, "OK" if n == 0 else "✗ 본안이 이미 빨갛다!"))
    print()
    bad = 0
    for name, gate, mut in CASES:
        pk = clone(); mut(pk)
        if coords(pk) == base_coords and gate != "grade":
            print("   ✗ %-38s 변형이 좌표에 적용되지 않았다(거짓 초록 위험)" % name); bad += 1; continue
        n = run_gate(gate, pk)
        ok = n > 0
        if not ok: bad += 1
        print("   %s %-38s [%-8s] 빨간불 %d건" % ("OK " if ok else "✗  ", name, gate, n))
    print("╚══ 대조 실패 %d건 ══╝" % bad)
    sys.exit(1 if bad else 0)
