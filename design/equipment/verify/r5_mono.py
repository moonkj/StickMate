# -*- coding: utf-8 -*-
"""★ 과제 D — design-art 「여섯 방위, 한 고도」와 맞물린다.

그쪽 실측(PACK_THEME_SPEC §10-3 (나)): **팩 주색 6종 상호 최소 ΔE = 24.31 < 식별 하한 48.6.**
= **색만으로는 어느 팩인지 못 맞힌다.** 그러면 정체는 형태가 져야 한다.

그래서 두 가지를 잰다.
  §1 팩 밖 : 6종이 **색을 완전히 지운 상태**에서도 서로/출하 42종과 갈리는가
  §2 팩 안 : 보조색 도형이 **색을 지우면 사라지는가** — 「자유 윤곽 비율」
             (보조색 도형의 테두리 중 부모 도형 잉크 **밖**에 있는 비율)
             0% 면 그 도형은 mono 에서 존재하지 않는다 = 색이 정체를 지고 있다는 뜻이다.
  §3 카드 44px 에서 같은 것 (착용보다 더 작다)
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H, sectors as S
import pack_nightshift as P, r5_rx
from rig import Shape

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)
RATCHET = S.SILHOUETTE_RATCHET_R
FAIL = []
def bad(m): FAIL.append(m); print("  ✗ " + m)


def strip(sh):
    return [Shape(s.name, s.pts, s.loop, s.filled, 0) for s in sh]


def free_outline(sh, w, samples=1600):
    """보조색(tone=1) 도형의 테두리 중 **다른 도형 잉크 밖**에 있는 비율.
       색을 지웠을 때 그 도형이 자기 형태로 남는가를 재는 자."""
    acc = [s for s in sh if s.tone == 1]
    if not acc: return None
    others = [s for s in sh if s.tone != 1]
    tot = free = 0.0
    for a in acc:
        p = a.pts + ([a.pts[0]] if a.loop else [])
        for u, v in zip(p, p[1:]):
            L = math.hypot(v[0] - u[0], v[1] - u[1])
            n = max(2, int(samples * L / 8.0))
            for k in range(n):
                t = (k + 0.5) / n
                q = (u[0] + (v[0] - u[0]) * t, u[1] + (v[1] - u[1]) * t)
                tot += L / n
                if not H._covered(others, q, w): free += L / n
    return (free / tot, free) if tot > 0 else (0.0, 0.0)


def run(scale):
    w = H.stroke_in_R(scale)
    print("\n╔══ 배율 %.2f  (W = %.4f R) ══╗" % (scale, w))
    PACK = {"HEAD 목덮개 작업모": (P.head_havelock(), items.HEAD, 0.0),
            "EYES 방진 고글": (r5_rx.eyes_respirator_v2(), items.EYES, 0.0),
            "NECK 작업 앞치마": (r5_rx.neck_apronbib_v2(), items.NECK, rig.SHOULDER_R),
            "BACK 연장 가방": (r5_rx.back_toolbag_v2(), items.BACK, rig.SHOULDER_R),
            "HAIR 목덜미 매듭": (r5_rx.hair_napetie_v2(), hair.SET, 0.0)}

    print("\n  ── §1 색을 지운 뒤에도 갈리는가 (실루엣은 애초에 색맹이다 — 확인만 한다) ──")
    for n, (sh, base, anc) in PACK.items():
        p0 = S.profile(sh, anc); p1 = S.profile(strip(sh), anc)
        same = max(abs(a - b) for a, b in zip(p0, p1)) < 1e-12
        worst = min((rig.max_delta(p1, S.profile(f() if callable(f) else f, anc)), bn)
                    for bn, f in base.items())
        (print if worst[0] >= RATCHET and same else bad)(
            "  OK %-16s 색 제거 전후 프로파일 동일 %s · 출하 최악쌍 %.3fR = %.2f획"
            % (n, same, worst[0], worst[0] / w))

    print("\n  ── §2 보조색 도형의 자유 윤곽 비율 (0%% = mono 에서 사라진다) ──")
    print("     [교정] 부모 안에 완전히 잠긴 도형 -> 0.0%% / 부모 밖 도형 -> 100.0%%")
    parent = Shape("P", [(-2, -2), (2, -2), (2, 2), (-2, 2)], True, filled=True)
    inside = Shape("A", [(-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)], True, filled=True, tone=1)
    outside = Shape("A", [(5, 5), (6, 5), (6, 6), (5, 6)], True, filled=True, tone=1)
    c0 = free_outline([parent, inside], w)[0]; c1 = free_outline([parent, outside], w)[0]
    okc = abs(c0) < 1e-9 and abs(c1 - 1) < 1e-9
    print("     [교정] %.4f / %.4f  %s" % (c0, c1, "OK" if okc else "FAIL"))
    if not okc: sys.exit("교정 실패")
    print("     판정선은 비율이 아니라 **길이**로 잡는다 — 자유 윤곽 길이 ≥ 1획(%.4f R)." % w)
    print("     비율은 부모가 크면 저절로 작아진다. 화면에서 보이느냐는 길이가 정한다.")
    for n, (sh, _, _) in PACK.items():
        f, L = free_outline(sh, w)
        (print if L >= w else bad)("  OK %-16s 자유 윤곽 %5.1f%%  길이 %.3f R = %.2f획" % (n, f * 100, L, L / w))
    print("\n     [대조] 출하 42종 중 보조색을 가진 것들")
    vals = []
    for slot, tbl in (("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
                      ("BACK", items.BACK), ("HAIR", hair.SET)):
        for bn, bf in tbl.items():
            sh = bf() if callable(bf) else bf
            r = free_outline(sh, w)
            if r is None: continue
            vals.append((r[1] / w, slot, bn))
    vals.sort()
    print("     자유 윤곽 **길이**(획 배수) n=%d  최소 %.2f(%s)  중앙 %.2f  최대 %.2f(%s)"
          % (len(vals), vals[0][0], vals[0][2], vals[len(vals) // 2][0], vals[-1][0], vals[-1][2]))
    print("     1획 미달 = " + (", ".join("%s %.2f획" % (v[2], v[0]) for v in vals if v[0] < 1.0) or "없음"))


def cards():
    print("\n╔══ §3 카드 44px 에서 같은 자 ══╗")
    IST = 1.7 * 44 / 40
    for n, sh in (("HEAD 목덮개 작업모", P.head_havelock()), ("EYES 방진 고글", r5_rx.eyes_respirator_v2()),
                  ("NECK 작업 앞치마", r5_rx.neck_apronbib_v2()), ("BACK 연장 가방", r5_rx.back_toolbag_v2()),
                  ("HAIR 목덜미 매듭", r5_rx.hair_napetie_v2())):
        pts = [p for s in sh for p in s.pts]
        x0, y0, x1, y1 = rig.bounds(pts)
        k = 44 * 0.86 / max(x1 - x0, y1 - y0)
        big = [Shape(s.name, [(x * k, y * k) for x, y in s.pts], s.loop, s.filled, s.tone) for s in sh]
        f, L = free_outline(big, IST)
        (print if L >= IST else bad)("  OK %-16s 카드 자유 윤곽 %5.1f%%  길이 %.2f px = %.2f카드획"
                                     % (n, f * 100, L, L / IST))


if __name__ == "__main__":
    run(0.75); run(0.60); cards()
    print("\n╚══ 위반 %d건 ══╝" % len(FAIL))
