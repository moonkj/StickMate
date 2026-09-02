# -*- coding: utf-8 -*-
"""★ 천모자 챙 좌우 대칭화 — 좌표 처방 + 게이트 (2026-09-02, design-equipment)

사용자 결정: "좌우대칭으로 변경".
핸드오프 아트(ItemIcon.dc.html `clothhat`)를 **그대로는 못 쓴다** — 착용 오버레이 기하로 R 단위로
되돌리면 잉크가 전부 y >= +0.272R에 있어 규칙 4(감쌈: |x|>=0.85 & y<=0.05)를 통과하지 못한다.
아이콘을 박스로 얹은 그림이라 **머리를 감싸지 않는다.** 그래서 "대칭"이라는 결정만 받고 다시 앉힌다.

    python3 clothhat.py            # 처방 + 전수 게이트
    python3 clothhat.py --control  # 양성 대조
    python3 clothhat.py --dump     # 좌표 전문
"""
import sys, os, math, types, io, contextlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, headroom as H, sectors as S, hatfix
from rig import Shape

# ---------------------------------------------------------------------------
# 처방 좌표 (머리 중심 원점 · R 배수 · x 대칭)
# ---------------------------------------------------------------------------
#  관(dome): 카테고리에서 가장 낮은 관 중 하나(1.10R). 털모자(폼폼 1.72R)와 90° 구간에서 벌어진다.
#  챙(drooping ring): 도달 ±1.34R로 **좁고**(중절 2.06 / 밀짚 2.18과 벌어진다),
#                     끝이 아래로 처진다(-0.52R) — 베레모·왕관이 잉크가 전혀 없는 330~340° 구간을 문다.
CROWN = [(-0.98, 0.02), (-0.92, 0.66), (-0.50, 1.02), (0.00, 1.10),
         ( 0.50, 1.02), ( 0.92, 0.66), ( 0.98, 0.02)]
#  ★ 챙 끝 ±1.36 / -0.22 : 첫 시안 ±1.34 / -0.20 은 뿌리까지의 변이 0.4219R = **0.98획@0.60**으로
#     신규/재설계 전용 최단변 하한(1획@0.60)을 아슬하게 못 넘었다. 프로덕션 규칙 1(꺾임 45° 문턱)은
#     그냥 통과시킨다 — 그 사각지대를 믿지 않는다.
BRIM  = [(-1.36,-0.22), (-0.98, 0.02), (0.00, 0.22), (0.98, 0.02), (1.36,-0.22),
         ( 1.04,-0.52), (0.00,-0.24), (-1.04,-0.52)]

def prescribed():
    return [Shape("HatCrown", CROWN, filled=True),
            Shape("HatBrim",  BRIM,  filled=True, tone=1)]

COVER_NEW = 0.02      # HatCoverLocalY — 챙 뿌리선(현행 0.06에서 내려온다)


def install(shapes=None):
    shapes = shapes if shapes is not None else prescribed()
    import items as real, hair
    m = types.ModuleType("items")
    m.HEAD = dict(real.HEAD); m.HEAD["천모자"] = shapes
    if "야구모자" in m.HEAD: del m.HEAD["야구모자"]
    m.EYES, m.NECK, m.BACK = real.EYES, real.NECK, real.BACK
    m.EYE_FRONT_ONLY = real.EYE_FRONT_ONLY
    m.COVER = dict(real.COVER); m.COVER.pop("야구모자", None); m.COVER["천모자"] = COVER_NEW
    sys.modules["items"] = m
    return m


def baseline_head():
    """손대기 전 HEAD 표(프로덕션 = 거울). 래칫 기준선은 **재서** 얻는다."""
    sys.modules.pop("items", None)
    import importlib
    return importlib.import_module("items").HEAD


def quiet(fn, *a, **kw):
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf): r = fn(*a, **kw)
    return r, buf.getvalue()


# ---------------------------------------------------------------------------
def gate_verify(m):
    os.chdir(os.path.dirname(os.path.abspath(__file__)))
    g = {"__name__": "__main__"}
    exec(compile(open("verify.py", encoding="utf-8").read(), "verify.py", "exec"), g)
    return g.get("fail", 0)


def gate_ratchet(m, quiet_mode=False):
    base = baseline_head()
    b0 = S.pairwise_table(base, 0.0)[0][0]
    install_shapes = m.HEAD
    rows = S.pairwise_table(install_shapes, 0.0)
    d, x, y = rows[0]
    bad = []
    if d < b0 - 1e-9: bad.append("HEAD 래칫 %.4fR -> %.4fR (%s vs %s)" % (b0, d, x, y))
    if not quiet_mode:
        print("╔══ 실루엣 래칫 (기준선 = 손대기 전 HEAD 실측) ══╗")
        print("  기준선 %.4fR = %.2f획@0.75 = %.2f획@0.60" % (b0, b0/S.W075, b0/S.W060))
        print("  %s 처방 %.4fR = %.2f획@0.75 = %.2f획@0.60   (%s vs %s)"
              % ("OK " if not bad else "✗  ", d, d/S.W075, d/S.W060, x, y))
        print("  ── 천모자가 끼는 모든 쌍 (하위 6쌍) ──")
        n = 0
        for dd, a, b in rows:
            if "천모자" not in (a, b): continue
            print("     %.4fR = %.2f획@0.75 = %.2f획@0.60   %s vs %s" % (dd, dd/S.W075, dd/S.W060, a, b))
            n += 1
            if n >= 6: break
        print("╚══ 래칫 위반 %d건 ══╝" % len(bad))
    sys.modules["items"] = m
    return bad


def gate_headroom(shapes, name="천모자", quiet_mode=False):
    bad = []
    if not quiet_mode: print("╔══ 남는 머리 (하한 1.00획 / 목표 1.20획 / 면적 12%%) ══╗")
    for sc in H.HEADROOM_GATE_SCALES:
        w = H.stroke_in_R(sc); mm = H.measure(shapes, w); th = mm['depth']*2.0/w
        ok = th >= H.HEADROOM_THICKNESS_FLOOR_W and mm['area'] >= H.HEADROOM_AREA_FLOOR
        if not ok: bad.append("%s @%.2f 두께 %.2f획 면적 %.1f%%" % (name, sc, th, mm['area']*100))
        if not quiet_mode:
            print("  %s @%.2f  두께 %5.2f획%s  면적 %5.1f%%  외곽호 %5.1f°  잉크밑단 %+.3fR"
                  % ("OK " if ok else "✗  ", sc, th,
                     "" if th >= H.HEADROOM_THICKNESS_TARGET_W else " ←목표 1.20 미달",
                     mm['area']*100, mm['arc'], mm['ink_bottom']))
    if not quiet_mode: print("╚══ 남는 머리 위반 %d건 ══╝" % len(bad))
    return bad


def gate_1c(shapes, quiet_mode=False):
    bad = []
    if not quiet_mode: print("╔══ 규칙 1-C 색면 조건 (ρ_max >= %.5fR) ══╗" % hatfix.FILL_OUTLINE_PEN_IN_R)
    for s in shapes:
        if not s.filled: continue
        r = hatfix.rho_max(s.pts); k = r / hatfix.FILL_OUTLINE_PEN_IN_R
        if k < 1.0: bad.append("%s ρ_max %.4fR" % (s.name, r))
        if not quiet_mode:
            print("  %s %-10s ρ_max %.4fR = %.2f획" % ("OK " if k >= 1.0 else "✗  ", s.name, r, k))
    if not quiet_mode: print("╚══ 1-C 위반 %d건 ══╝" % len(bad))
    return bad


def gate_wrap(shapes, quiet_mode=False):
    """규칙 4 감쌈. 엄격판(r <= 1.05)까지 본다 — 핸드오프 아트가 떨어진 자리가 여기다."""
    loose = [q for s in shapes for q in s.pts if abs(q[0]) >= 0.85 and q[1] <= 0.05]
    strict = [q for q in loose if math.hypot(*q) <= hatfix.WRAP_MAX_RADIUS]
    bad = []
    if not loose: bad.append("감쌈(느슨) 실패")
    if not strict: bad.append("감쌈(엄격 r<=1.05) 실패")
    if not quiet_mode:
        print("╔══ 규칙 4 감쌈 ══╗")
        print("  %s 느슨(|x|>=0.85 & y<=0.05): %s" % ("OK " if loose else "✗  ", loose or "없음"))
        print("  %s 엄격(+ r<=1.05)          : %s" % ("OK " if strict else "✗  ",
              ["(%+.2f,%+.2f) r=%.3f" % (q[0], q[1], math.hypot(*q)) for q in strict] or "없음"))
        print("╚══ 감쌈 위반 %d건 ══╝" % len(bad))
    return bad


#: ★ 색면 허리 하한 — 규칙 1-C가 **못 잡는 것**을 잡는다.
#   1-C는 rho_max(최대 내접원)라 "어딘가 한 곳이 두꺼우면" 통과한다. 즉 챙 가운데가 획보다 얇아도
#   끝이 두꺼우면 초록이 나온다 — 그게 바로 "뚜껑" 실패 모드다(양성 대조 4번이 이 구멍으로 빠져나갔다).
#   그래서 도달의 0~75% 구간에서 **세로 두께가 매 지점 1획@0.60 이상**인지 따로 잰다.
#   75% 밖은 원칙 6("끝은 점으로 수렴")이 지배하는 구간이라 제외한다.
FILL_WAIST_FLOOR_R = S.W060
FILL_WAIST_SPAN = 0.75


def gate_fillwaist(shapes, quiet_mode=False):
    bad = []
    if not quiet_mode:
        print("╔══ 색면 허리 (도달 0~%.0f%% 구간의 세로 두께 · 하한 %.4fR = 1획@0.60) ══╗"
              % (FILL_WAIST_SPAN*100, FILL_WAIST_FLOOR_R))
    for sp in shapes:
        if not sp.filled: continue
        reach = max(abs(q[0]) for q in sp.pts)
        row = []
        for k in range(5):
            x = reach * FILL_WAIST_SPAN * k / 4.0
            sp2 = H._merge(H._poly_spans(sp.pts, 0.0)) if False else None
            # 수직 두께 = 그 x 에서 다각형 내부의 y 구간 길이 합
            ys = []
            n = len(sp.pts)
            for i in range(n):
                a, b = sp.pts[i], sp.pts[(i+1) % n]
                if (a[0] > x) != (b[0] > x):
                    ys.append(a[1] + (x - a[0]) * (b[1] - a[1]) / (b[0] - a[0]))
            ys.sort()
            t = sum(ys[i+1] - ys[i] for i in range(0, len(ys)-1, 2)) if len(ys) >= 2 else 0.0
            row.append((x, t))
        worst = min(row, key=lambda r: r[1])
        ok = worst[1] >= FILL_WAIST_FLOOR_R - 1e-9
        if not ok: bad.append("%s x=%.2f 두께 %.4fR" % (sp.name, worst[0], worst[1]))
        if not quiet_mode:
            print("  %s %-10s %s   최악 %.2f획@0.60"
                  % ("OK " if ok else "✗  ", sp.name,
                     " ".join("x%+.2f:%.2f획" % (x, t/S.W060) for x, t in row), worst[1]/S.W060))
    if not quiet_mode: print("╚══ 색면 허리 위반 %d건 ══╝" % len(bad))
    return bad


MIN_EDGE_FLOOR_R = S.W060
def gate_minedge(shapes, quiet_mode=False):
    bad = []
    if not quiet_mode:
        print("╔══ 최단 실제 변 (꺾임 문턱 무관 · 하한 %.4fR = 1획@0.60) ══╗" % MIN_EDGE_FLOOR_R)
    for s in shapes:
        n = len(s.pts); best = None; where = None
        for i in range(n if s.loop else n-1):
            L = math.dist(s.pts[i], s.pts[(i+1) % n])
            if best is None or L < best: best, where = L, i
        ok = best >= MIN_EDGE_FLOOR_R - 1e-9
        if not ok: bad.append("%s 변%d %.4fR" % (s.name, where, best))
        if not quiet_mode:
            print("  %s %-10s 최단 %.4fR = %.2f획@0.60 (변 %d)"
                  % ("OK " if ok else "✗  ", s.name, best, best/S.W060, where))
    if not quiet_mode: print("╚══ 최단 변 위반 %d건 ══╝" % len(bad))
    return bad


def gate_symmetry(shapes, quiet_mode=False):
    """★ 결정 ①의 본문 그 자체 — 좌우 대칭인가. x -> -x 로 뒤집어 같은 점집합이 나오는가."""
    bad = []
    if not quiet_mode: print("╔══ 좌우 대칭 (결정 ①) ══╗")
    for s in shapes:
        pts = s.pts
        flip = sorted(((-x, y) for x, y in pts))
        orig = sorted(pts)
        e = max(math.dist(a, b) for a, b in zip(orig, flip)) if len(orig) == len(flip) else 9.9
        ok = e < 1e-9
        if not ok: bad.append("%s 비대칭 %.4fR" % (s.name, e))
        if not quiet_mode:
            print("  %s %-10s 반전 후 최대 점오차 %.6fR" % ("OK " if ok else "✗  ", s.name, e))
    if not quiet_mode: print("╚══ 대칭 위반 %d건 ══╝" % len(bad))
    return bad


def main(shapes=None):
    shapes = shapes if shapes is not None else prescribed()
    m = install(shapes)
    print("── 대상: 천모자 좌우대칭 처방 + 나머지 29종 ──")
    fail = gate_verify(m)
    print()
    bad = []
    bad += gate_symmetry(shapes); print()
    bad += gate_wrap(shapes); print()
    bad += gate_minedge(shapes); print()
    bad += gate_1c(shapes); print()
    bad += gate_fillwaist(shapes); print()
    bad += gate_headroom(shapes); print()
    bad += gate_ratchet(m)
    print()
    print("★ verify.py 위반 %d건 + 천모자 전용 게이트 위반 %d건 = 총 %d건" % (fail, len(bad), fail+len(bad)))
    for b in bad: print("   · %s" % b)
    return fail + len(bad)


if __name__ == "__main__":
    if "--dump" in sys.argv:
        for s in prescribed():
            print("%-10s loop=%d fill=%d tone=%d  %s" % (s.name, s.loop, s.filled, s.tone,
                  " ".join("(%+.4f,%+.4f)" % p for p in s.pts)))
        print("HatCoverLocalY = %+.4f" % COVER_NEW)
    elif "--control" in sys.argv:
        # ★ import 로 부르면 __name__ != "__main__" 이라 **아무것도 안 돌고 조용히 끝난다**
        #   (지난 라운드 거짓 초록의 정체). exec 하되 __file__ 도 함께 넘긴다.
        _cp = os.path.join(os.path.dirname(os.path.abspath(__file__)), "control_clothhat.py")
        exec(compile(open(_cp, encoding="utf-8").read(), _cp, "exec"),
             {"__name__": "__main__", "__file__": _cp})
    else:
        sys.exit(1 if main() else 0)
