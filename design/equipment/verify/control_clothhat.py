# -*- coding: utf-8 -*-
"""★ 천모자 대칭화 — 양성 대조. 나쁜 값을 넣으면 게이트가 실제로 빨간불을 내는가.

매 대조마다 ① 좌표가 정말 달라졌는지 먼저 단언하고 ② 목표 게이트만 본다.
(2026-09-01에 이 담당자의 대조가 한 번 거짓 초록을 냈다 — 변형이 적용되지 않았는데 통과로 읽혔다.)
"""
import sys, os, math, io, contextlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, sectors as S, clothhat as C
from rig import Shape

def base():
    return [Shape(s.name, [tuple(p) for p in s.pts], s.loop, s.filled, s.tone) for s in C.prescribed()]

def pts(sh): return [tuple(p) for s in sh for p in s.pts]

CASES = []
def case(name, gate):
    def deco(f): CASES.append((name, gate, f)); return f
    return deco


@case("좌우 비대칭 (챙 앞끝만 1.92R)", "sym")
def c_sym(sh):
    b = [s for s in sh if s.name == "HatBrim"][0]
    b.pts[4] = (1.92, -0.22); b.pts[5] = (1.44, -0.52)


@case("★ 핸드오프 아트 그대로 (감쌈 실패)", "wrap")
def c_handoff(sh):
    """ItemIcon.dc.html `clothhat`을 착용 오버레이 기하로 R 단위로 되돌린 것 — 잉크가 전부
    y >= +0.272R 에 있어 규칙 4(감쌈)를 못 넘는다. 재앉힘이 필요했던 이유가 이것이다."""
    K = (70/64) / (28*158/200)
    def cv(ix, iy): return (K*(ix-32), ((26+46*0.79) - (6+iy*(70/64))) / (28*0.79))
    def bez(p0, p1, p2, n=9):
        return [((1-t)**2*p0[0]+2*(1-t)*t*p1[0]+t*t*p2[0],
                 (1-t)**2*p0[1]+2*(1-t)*t*p1[1]+t*t*p2[1]) for t in [i/(n-1) for i in range(n)]]
    crown = bez(cv(18,41), cv(16.5,21), cv(32,21)) + bez(cv(32,21), cv(47.5,21), cv(46,41))[1:]
    brim  = bez(cv(7,41), cv(32,33.5), cv(57,41)) + bez(cv(57,41), cv(32,51), cv(7,41))[1:]
    sh[0].pts[:] = crown; sh[1].pts[:] = brim


@case("남는 머리 (챙 밑을 -0.70R까지 내림)", "headroom")
def c_headroom(sh):
    b = [s for s in sh if s.name == "HatBrim"][0]
    b.pts[5] = (1.04, -0.86); b.pts[6] = (0.00, -0.70); b.pts[7] = (-1.04, -0.86)


@case("규칙 1-C (챙 전체를 얇은 판으로)", "1c")
def c_1c(sh):
    """★ 첫 대조는 **가운데만** 얇게 했는데 초록이 나왔다. 게이트가 샌 게 아니라 대조가 틀렸다 —
    rho_max는 최대 내접원이라 끝이 두꺼우면 통과한다. 1-C의 이 성질 자체는 결함이고,
    그래서 clothhat.gate_fillwaist(색면 허리)를 따로 세웠다."""
    b = [s for s in sh if s.name == "HatBrim"][0]
    b.pts[:] = [(x, (y + 0.14) * 0.35 - 0.14) for x, y in b.pts]


@case("색면 허리 (챙 가운데만 얇게 — 1-C는 못 잡는다)", "waist")
def c_waist(sh):
    b = [s for s in sh if s.name == "HatBrim"][0]
    b.pts[2] = (0.00, 0.06); b.pts[6] = (0.00, -0.08)


@case("최단 변 (챙 끝을 뿌리에 붙임)", "minedge")
def c_minedge(sh):
    b = [s for s in sh if s.name == "HatBrim"][0]
    b.pts[0] = (-1.20, -0.10); b.pts[4] = (1.20, -0.10)


@case("실루엣 래칫 (이웃 털모자의 1.01배 복제)", "ratchet")
def c_ratchet(sh):
    """★ 첫 대조는 챙을 밀짚 폭(±2.10)까지 넓히는 것이었는데 **초록이 나왔다.**
    이유가 유익하다 — 밀짚의 챙 끝은 **위로 들리고**(+0.30) 이 처방은 **아래로 처져서**(−0.22),
    폭이 같아져도 +5° 방향에서 1.22R이나 벌어진다. 폭이 아니라 **처짐**이 구분을 만들고 있었다.
    그래서 대조를 "가장 가까운 이웃(털모자)의 복제"로 바꾼다 — 이건 반드시 충돌해야 한다."""
    import items as _it
    src = _it.HEAD["털모자"]
    sh[0].pts[:] = [(x*1.01, y*1.01) for x, y in src[0].pts]
    sh[1].pts[:] = [(x*1.01, y*1.01) for x, y in src[-1].pts]


@case("액자 (관이 초상화 상한 2.551R 초과)", "verify")
def c_frame(sh):
    c = [s for s in sh if s.name == "HatCrown"][0]
    c.pts[3] = (0.00, 2.60)


def quiet(fn, *a, **kw):
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf): r = fn(*a, **kw)
    return r


def run_gate(gate, sh):
    if gate == "sym":      return len(quiet(C.gate_symmetry, sh, True))
    if gate == "wrap":     return len(quiet(C.gate_wrap, sh, True))
    if gate == "minedge":  return len(quiet(C.gate_minedge, sh, True))
    if gate == "1c":       return len(quiet(C.gate_1c, sh, True))
    if gate == "waist":    return len(quiet(C.gate_fillwaist, sh, True))
    if gate == "headroom": return len(quiet(C.gate_headroom, sh, "천모자", True))
    if gate == "ratchet":
        m = C.install(sh); return len(quiet(C.gate_ratchet, m, True))
    if gate == "verify":
        m = C.install(sh); return quiet(C.gate_verify, m)
    raise KeyError(gate)


if __name__ == "__main__":
    print("╔══ 양성 대조 — 천모자 좌우대칭 처방 ══╗")
    print("  (본안이 초록인지 먼저 확인한다)")
    for g in ("sym", "wrap", "minedge", "1c", "waist", "headroom", "ratchet", "verify"):
        n = run_gate(g, base())
        print("   본안 %-9s -> %d건 %s" % (g, n, "OK" if n == 0 else "✗ 본안이 이미 빨갛다!"))
    print()
    b0 = pts(base()); bad = 0
    for name, gate, mut in CASES:
        sh = base(); mut(sh)
        if pts(sh) == b0:
            print("   ✗ %-38s 변형이 좌표에 적용되지 않았다(거짓 초록 위험)" % name); bad += 1; continue
        n = run_gate(gate, sh)
        ok = n > 0
        if not ok: bad += 1
        print("   %s %-38s [%-8s] 빨간불 %d건" % ("OK " if ok else "✗  ", name, gate, n))
    print("╚══ 대조 실패 %d건 ══╝" % bad)
    sys.exit(1 if bad else 0)
