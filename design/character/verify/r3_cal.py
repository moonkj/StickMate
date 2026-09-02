# -*- coding: utf-8 -*-
"""R3 ① 교정 — 여기가 깨지면 뒤 숫자를 전부 폐기하고 종료한다.

R2 의 교정 31건을 그대로 물려받고(=r2_body), **프로덕션 실행 덤프**로 12건 + 대조 6건을 더한다.
★ R2 에서 8/19 가 깨졌던 원인(낡은 독스트링 표)을 되풀이하지 않으려고, 이번엔 자를
  「문서에 적힌 표」가 아니라 **지금 컴파일해 돌린 프로덕션 출력**에 맞춘다.
"""
import math, sys, os
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import rig, items, headroom
import r2_body as B
import r3_prod as P
import r3_raster as RS

CAL = []
def cal(name, got, want, tol, fmt="%.6f"):
    ok = abs(got - want) <= tol
    CAL.append((name, got, want, ok)); return ok

def calbool(name, ok, note=""):
    CAL.append((name + (" " + note if note else ""), 1.0 if ok else 0.0, 1.0, ok)); return ok


def run(verbose=True):
    # --- (a) R2 의 31건을 그대로 다시 (r2_body 가 깨지면 거기서 종료한다) -------------
    B.print_cal()
    n_r2 = len(B.CAL)

    cats, cover, Wdump, rarity, log = P.dump()

    # --- (b) 프로덕션 실행이 조용했는가 -------------------------------------------
    calbool("프로덕션 덤프 로그 0에러 0경고", log == (0, 0), str(log))

    # --- (c) 획 예산 W: 덤프 == rig == headroom ------------------------------------
    cal("W(덤프) vs rig.W", Wdump, rig.W, 1e-6)
    cal("W(덤프) vs headroom.stroke_in_R(0.75)", Wdump, headroom.stroke_in_R(0.75), 1e-6)

    # --- (d) 설계 거울(items.py)이 프로덕션과 같은가 — HEAD 6 + EYES 6 -------------
    def align_err(a, b, loop):
        if len(a) != len(b): return float("inf")
        n = len(a); cands = [b]
        if loop:
            cands = [b[k:] + b[:k] for k in range(n)]
            rb = list(reversed(b)); cands += [rb[k:] + rb[:k] for k in range(n)]
        return min(max(math.dist(a[i], c[i]) for i in range(n)) for c in cands)

    def mirror_err(cat, table):
        worst = 0.0; worst_nm = None
        for nm, mshapes in table.items():
            pshapes = cats[cat][nm]
            if len(pshapes) != len(mshapes): return float("inf"), nm
            for ps, ms in zip(pshapes, mshapes):
                e = align_err(ps.pts, ms.pts, ps.loop)
                if e > worst: worst, worst_nm = e, nm + "/" + ps.name
        return worst, worst_nm

    for cat, tab in (("HEAD", items.HEAD), ("EYES", items.EYES)):
        e, nm = mirror_err(cat, tab)
        cal("거울 일치 %s (최대 점오차)" % cat, e, 0.0, 1e-4)

    # 양성 대조 — 거울을 일부러 흔들면 위 검사가 **반드시 깨져야** 한다
    saved = items.EYES["동그란안경"][0].pts
    items.EYES["동그란안경"][0].pts = [(x + 0.01, y) for x, y in saved]
    e_bad, _ = mirror_err("EYES", items.EYES)
    items.EYES["동그란안경"][0].pts = saved
    calbool("양성 대조: 거울을 0.01R 흔들면 검사가 깨진다", e_bad > 1e-4, "(오차 %.4f)" % e_bad)

    # --- (e) 커버선: 덤프 == items.COVER ------------------------------------------
    idx = {"야구모자":0, "털모자":1, "중절모":2, "왕관":3, "베레모":4, "밀짚모자":5}
    for nm, i in idx.items():
        a, b = cover[i], items.COVER[nm]
        if math.isinf(a) or math.isinf(b):
            calbool("커버선 %s = +inf" % nm, math.isinf(a) and math.isinf(b))
        else:
            cal("커버선 %s" % nm, a, b, 1e-4)

    # --- (f) 두 자의 교차 검산: 래스터(점 소속) vs 구간 대수(스캔라인) ---------------
    #     같은 물건을 **다른 알고리즘**으로 재서 같은 답이 나오는가.
    h = 0.0015
    for nm in ("야구모자", "밀짚모자", "고글", "동그란안경"):
        cat = "HEAD" if nm in idx else "EYES"
        sh = cats[cat][nm]
        m, xs, ys = RS.mask_of(sh, Wdump, -2.6, 2.6, -1.6, 1.8, h)
        a_ras = m.sum() * h * h
        # 구간 대수로 같은 면적
        ys2 = np.linspace(-1.6, 1.8, 3401)
        a_sc = 0.0
        for y in ys2:
            a_sc += sum(b - a for a, b in headroom.ink_spans(sh, float(y), Wdump))
        a_sc *= (1.8 + 1.6) / 3400.0
        cal("면적 교차검산 %s (래스터 vs 구간대수, 상대)" % nm,
            abs(a_ras - a_sc) / a_sc, 0.0, 0.005)

    # --- (g) EDT 양성 대조 (알려진 도형) ------------------------------------------
    class _S:
        def __init__(s, pts, loop=True, filled=False): s.pts, s.loop, s.filled = pts, loop, filled
    disc = [(0.5*math.cos(2*math.pi*i/400), 0.5*math.sin(2*math.pi*i/400)) for i in range(400)]
    md, _, _ = RS.mask_of([_S(disc, filled=True)], 0.0, -0.8, 0.8, -0.8, 0.8, 0.001)
    cal("EDT 대조: 반지름 0.5 원반의 최대 내접 지름", RS.thickness_W(md, 0.001, 1.0), 1.0, 0.004)
    ms, _, _ = RS.mask_of([_S([(-0.6,-0.15),(0.6,-0.15),(0.6,0.15),(-0.6,0.15)], filled=True)],
                          0.0, -0.8, 0.8, -0.8, 0.8, 0.001)
    cal("EDT 대조: 폭 0.30 띠", RS.thickness_W(ms, 0.001, 1.0), 0.30, 0.004)
    calbool("EDT 대조: 빈 마스크는 0", RS.thickness_W(np.zeros((8,8), bool), 0.001, 1.0) == 0.0)

    # --- (h) 레이어 사실 — 모자가 안경 위인가 --------------------------------------
    hs = min(s.sort for nm in items.HEAD for s in cats["HEAD"][nm])
    es = max(s.sort for nm in items.EYES for s in cats["EYES"][nm])
    calbool("레이어: HEAD(%d) > EYES(%d)" % (hs, es), hs > es)

    bad = [c for c in CAL if not c[3]]
    if verbose:
        print("== R3 추가 교정 %d/%d (R2 %d건 위에) ==" % (len(CAL)-len(bad), len(CAL), n_r2))
        for nm, g, w, ok in CAL:
            print(("  %s %-46s got %.6f  want %.6f" % ("✓" if ok else "✗", nm, g, w)))
    if bad:
        print("★ 교정이 깨졌다 — 이 실행의 숫자를 전부 폐기한다."); sys.exit(1)
    if verbose:
        print("총 교정 %d건 전부 통과.\n" % (n_r2 + len(CAL)))
    return cats, cover, Wdump, rarity


if __name__ == "__main__":
    run()
