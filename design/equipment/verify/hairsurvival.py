# -*- coding: utf-8 -*-
"""★ 「모자 밑에서 살아남는 머리카락 영역」 — HAIR를 팩에 넣을 수 있게 만드는 설계 조건.

hairunderhat.py 가 잰 것: 현행 머리 6종은 모자와 함께 쓰면 평균 22.8%만 남는다.
여기서 재는 것: **그럼 어디에 그리면 안 지워지는가.**

생존 영역 S = { (x,y) : y < 커버선  ∧  모자 잉크 밖 }   — 모자 6종 전부에 대해 교집합.
그 영역의 세로 폭이 획(W)보다 두꺼운 x 구간이 있어야 "머리카락 덩어리"가 성립한다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, headroom as H

HATS = {"야구모자": items.cap, "털모자": items.beanie, "중절모": items.fedora,
        "왕관": items.crown_hat, "베레모": items.beret, "밀짚모자": items.straw}


def survive_top(x, hat, cover, w, ylo=-2.6, yhi=1.9, n=1400):
    """이 x에서, 모자에 안 먹히고 커버선 아래인 y의 **가장 높은 값**. 없으면 None."""
    best = None
    for k in range(n):
        y = yhi - (yhi - ylo) * k / (n - 1)
        if y >= cover: continue
        sp = H._merge(H.ink_spans(hat, y, w))
        if any(a <= x <= b for a, b in sp): continue
        best = y; break
    return best


def run(scale):
    w = H.stroke_in_R(scale)
    print("\n╔══ 배율 %.2f  W=%.4f R ══╗" % (scale, w))
    # 교정: 모자가 없으면 생존 상단 = yhi (아무것도 안 막는다)
    t = survive_top(0.0, [], float("inf"), w)
    print("  [교정] 모자 없음 x=0 -> 생존상단 %.3f (기대 1.900 = 스캔 상단)  %s"
          % (t, "OK" if abs(t - 1.9) < 0.01 else "FAIL"))
    t2 = survive_top(0.0, [rig.Shape("Big", [(-40,-40),(40,-40),(40,40),(-40,40)], True, True)],
                     float("inf"), w)
    print("  [교정] 전부 덮는 채움 x=0 -> 생존상단 %s (기대 None)  %s"
          % (t2, "OK" if t2 is None else "FAIL"))
    if abs(t - 1.9) > 0.01 or t2 is not None:
        print("  !! 교정 실패 — 폐기"); sys.exit(1)

    xs = [i * 0.10 for i in range(-26, 27)]
    print("\n  x별 「생존 상단 y」 — 이 선보다 **아래**에 그린 머리카락 잉크는 그 모자에 안 지워진다")
    print("   x     " + "".join("%9s" % h for h in HATS) + "   ★교집합(최저)")
    env = {}
    for x in xs:
        row = []
        for h, f in HATS.items():
            v = survive_top(x, f(), items.COVER[h], w)
            row.append(v)
        vals = [v for v in row if v is not None]
        mn = min(vals) if len(vals) == len(row) else None
        env[round(x, 2)] = mn
        if abs(x * 10 - round(x * 10)) < 1e-9 and round(x * 10) % 2 == 0:
            print("  %5.1f " % x + "".join(("%9.2f" % v) if v is not None else "%9s" % "—"
                                            for v in row)
                  + ("   %8.2f" % mn if mn is not None else "   %8s" % "—"))
    # 목/어깨선
    print("\n  참고선: 턱 −1.00R · 목 부착선 %.2fR · 망토 옷깃 %.2fR · 어깨 %.2fR"
          % (rig.SHOULDER_R + 0.04, rig.SHOULDER_R + 0.10, rig.SHOULDER_R))

    # 「1획보다 두꺼운 덩어리」가 들어가는 x 구간
    print("\n  ── 살아남는 덩어리가 실제로 들어가는가 (세로 여유 = 생존상단 − 턱선 −1.0R) ──")
    okx = [x for x, v in env.items() if v is not None and (v - (-1.0)) >= w]
    if okx:
        print("     |x| ≥ %.2f 부터 턱선 위에도 1획 이상 여유가 생긴다 (구간 %d칸)"
              % (min(abs(x) for x in okx), len(okx)))
    else:
        print("     턱선(−1.0R) 위에는 어느 x에서도 1획 두께가 안 남는다")
    below = [x for x, v in env.items() if v is not None and v <= -1.0]
    print("     턱선 아래에서만 완전 자유로운 x: %s"
          % ("전 구간" if len(below) == len(env) else
             ("|x| ≥ %.2f" % min(abs(x) for x in below) if below else "없음")))
    # 턱 아래는 항상 자유인가? (모자 잉크 최저 밑단)
    lo = min(min(y for _, y in H._merge([(0,0)]) ) if False else 0 for _ in [0])
    bots = {}
    for h, f in HATS.items():
        sh = f()
        b = 9e9
        for k in range(2000):
            y = 1.9 - 4.5 * k / 1999
            if H._merge(H.ink_spans(sh, y, w)): b = min(b, y)
        bots[h] = b
    print("\n  모자별 잉크 최저점: " + "  ".join("%s %.3fR" % (h, v) for h, v in bots.items()))
    print("  ★ 가장 낮은 모자 잉크 = %.3fR — 이보다 **아래**는 어떤 모자와도 절대 안 겹친다"
          % min(bots.values()))
    return min(bots.values())


for s in (0.75, 0.60):
    b = run(s)
