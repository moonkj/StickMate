# -*- coding: utf-8 -*-
"""★ 양성 대조 — wornfix.py의 게이트가 **실제로 빨간불을 내는가**.

이 저장소에서 하루에 거짓 통과 9건이 나왔고 전부 같은 형태였다:
**실패한 측정과 성공한 측정이 똑같이 생겼다.** 그래서 게이트마다 일부러 나쁜 좌표를 넣고
**하나씩** 켜지는 것을 확인한다. 그리고 그 전에 **실기 캡처와의 교정**을 먼저 돌린다 —
교정이 깨지면 그 뒤 숫자는 전부 폐기다.

    python3 control_wornfix.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, wornread as WR, wornfix as F
from rig import Shape

# ═══════════════════════════════════════════════════════════════════════════
# 0. 교정 — 실기 캡처(z_head.png) 픽셀 실측과 이 모델이 같은 답을 내는가
#    캡처: 머리 원 맞춤 중심 (223.5, 300.5) 반지름 131.5px. 4px 블록 양자화 = 0.030 R.
#    ★ 머리 반경 자체가 교정의 알려진 값이다 — 캡처의 검은 원반 반경이 200/220/320/340도에서
#      1.002 / 0.992 / 0.992 / 1.002 R 로 나왔다(원 = 1.000 R).
# ═══════════════════════════════════════════════════════════════════════════
CAPTURE = {0: -0.028, 10: +0.018, 20: +0.132, 30: +0.158, 40: +0.146,
           50: +0.182, 60: +0.272, 70: +0.238}     # 관(crown) 구간만. 폼폼(80/90도)은 아래 주석.
CAL_TOL = 0.035                                    # 캡처 양자화 0.030R + 1px


def calibrate():
    print("╔══ 0. 교정 — 실기 캡처 vs 모델 (현행 털모자 좌표) ══╗")
    b = items.HEAD["털모자"]
    worst, ok = 0.0, True
    for d, m in sorted(CAPTURE.items()):
        v = WR.relief(b, d, 0.0)
        e = abs(v - m); worst = max(worst, e)
        if e > CAL_TOL: ok = False
        print("   %3d도  캡처 %+.3fR   모델 %+.3fR   차 %.3fR  %s"
              % (d, m, v, e, "OK" if e <= CAL_TOL else "✗"))
    print("   최대 오차 %.4f R (허용 %.3f)  ->  %s" % (worst, CAL_TOL, "교정 통과" if ok else "★교정 실패"))
    print("   ※ 80/90도(폼폼)는 제외한다 — 캡처의 폼폼 잉크가 모델보다 넓고(+0.12R) 낮다(-0.07R).")
    print("     초상화 렌더러의 폼폼 획 처리를 재현하지 못했다: **미확인**. 그래서 폼폼 처방의")
    print("     근거는 '실측 링 두께가 모델보다 두껍다'는 **방향**이지 정확한 수치가 아니다.")
    print("╚══════════════════════════════════════════════════╝")
    return ok


# ═══════════════════════════════════════════════════════════════════════════
# 1. 게이트가 켜지는가
# ═══════════════════════════════════════════════════════════════════════════
def snapshot(head, eyes, back):
    items.HEAD, items.EYES, items.BACK = head, eyes, back
    ALL = [("HEAD", head), ("EYES", eyes), ("NECK", items.NECK), ("BACK", back), ("HAIR", hair.SET)]
    return (len(F.gate_relief(head, True)), len(F.gate_ring(ALL, True)),
            len(F.gate_occlusion(ALL, True)), len(F.gate_minedge(ALL, True)))


#: ★ 이 게이트들은 **30종 전체의 절대 건수**를 센다. 범위 밖(예: 나비넥타이 매듭)의 기존 위반이
#   계속 잡히므로 "처방 = 0건"이 될 수 없다. 그래서 대조는 **처방 대비 증분**으로 읽는다 —
#   나쁜 좌표를 하나 넣으면 **해당 게이트만** 올라가야 한다.
BASELINE = None


def show(label, head, eyes, back, expect=None):
    global BASELINE
    got = snapshot(head, eyes, back)
    names = ("부조", "고리", "몸가림", "최단변")
    if BASELINE is None:
        BASELINE = got
        print("  %-38s 부조 %d | 고리 %2d | 몸가림 %2d | 최단변 %2d   <= 기준선"
              % (label, *got))
        return got
    d = [got[i] - BASELINE[i] for i in range(4)]
    lit = [names[i] for i in range(4) if d[i] > 0]
    ok = (set(lit) == set(expect)) if expect is not None else None
    print("  %-38s 부조 %+d | 고리 %+d | 몸가림 %+d | 최단변 %+d   켜짐: %-14s%s"
          % (label, *d, ",".join(lit) if lit else "없음",
             "" if ok is None else ("  <= 기대대로" if ok else "  ★기대(%s)와 다름" % ",".join(expect))))
    return got


BASE_H, BASE_E, BASE_B = dict(items.HEAD), dict(items.EYES), dict(items.BACK)
OLD_BEANIE, OLD_SUN, OLD_RND = BASE_H["털모자"], BASE_E["선글라스"], BASE_E["동그란안경"]
OLD_CAPES = {k: BASE_B[k] for k in ("짧은망토", "긴망토", "판초")}

if __name__ == "__main__":
    okcal = calibrate()
    print()
    print("╔══ 1. 양성 대조 — 게이트가 하나씩 켜지는가 ══╗")
    P_H, P_E, P_B = F.prescribed()
    show("대조0  처방 전체 (= 기준선)", dict(P_H), dict(P_E), dict(P_B))
    show("NC1    현행 프로덕션 좌표 전부", dict(BASE_H), dict(BASE_E), dict(BASE_B),
         ["부조", "고리", "몸가림", "최단변"])

    # NC2 — 털모자 밑단 반폭만 되돌린다(1.28 -> 0.56). 단차만 켜져야 한다.
    c = list(F.BEA_CROWN); c[0] = (-0.56, F.BEA_HEM_Y); c[-1] = (0.56, F.BEA_HEM_Y)
    h = dict(P_H); h["털모자"] = [Shape("BeanieCrown", c, filled=True),
                               Shape("BeaniePom", F.BEA_POM, filled=True, tone=1)]
    show("NC2    털모자 밑단 반폭 1.28 -> 0.56", h, dict(P_E), dict(P_B), ["부조"])

    # NC3 — 망토 보조색만 옛 옷깃 띠로 되돌린다. 몸가림만 켜져야 한다.
    cy = rig.SHOULDER_R + 0.10
    clasp = [(0.40, cy + 0.10), (0.40, cy - 0.34), (-0.66, cy - 0.38), (-0.66, cy + 0.06)]
    b = dict(P_B)
    for k in ("짧은망토", "긴망토", "판초"):
        b[k] = [s for s in P_B[k] if s.name != "CapeHemBand"] + \
               [Shape("CapeCollar", clasp, filled=True, tone=1)]
    show("NC3    망토 보조색 -> 옛 옷깃 띠(목 위)", dict(P_H), dict(P_E), b, ["몸가림"])

    # NC4 — 폼폼만 옛 10각형 r=0.28로. 고리화 + 최단변이 켜져야 한다.
    h = dict(P_H); h["털모자"] = [Shape("BeanieCrown", F.BEA_CROWN, filled=True),
                                Shape("BeaniePom", rig.poly(-0.10, 1.44, 0.28, 10, 90.0),
                                      filled=True, tone=1)]
    show("NC4    폼폼 -> 옛 10각형 r=0.28", h, dict(P_E), dict(P_B), ["고리"])   # 최단변은 원 근사 면제라 안 켜진다(의도)

    # NC5 — 동그란안경만 옛 12각형 r=0.40 + 아치 코다리로. 최단변이 켜져야 한다.
    e = dict(P_E); e["동그란안경"] = OLD_RND
    show("NC5    동그란안경 -> 옛 12각형+아치", dict(P_H), e, dict(P_B), ["최단변"])

    # NC6 — 선글라스만 옛 아치 코다리(보조색)로. **부조/고리/몸가림/최단변은 안 켜진다** —
    #       '깃발로 읽힌다'는 기하 게이트로 잡히지 않는다. 그것을 여기 명시한다.
    e = dict(P_E); e["선글라스"] = OLD_SUN
    show("NC6    선글라스 -> 옛 아치 코다리", dict(P_H), e, dict(P_B), ["최단변"])
    print("     ★ NC6에서 켜지는 것은 **최단변 하나뿐**이다(아치 꼭짓점의 짧은 변). 부조/고리/몸가림은")
    print("       꿈쩍도 안 한다 — '밝은 ∧가 깃발/새로 읽힌다'는 **형태 판정**이지 기하 하한이 아니다.")
    print("       즉 선글라스 처방의 근거는 이 하니스가 아니라 실기 캡처(z_eyes.png)와 페르소나 진술이고,")
    print("       판정도 **다시 실기 캡처로만** 내려야 한다. 여기서 초록을 받았다고 통과가 아니다.")
    print("╚═══════════════════════════════════════════════════╝")

    items.HEAD, items.EYES, items.BACK = BASE_H, BASE_E, BASE_B
    print()
    print("교정 %s" % ("통과" if okcal else "★실패 — 이 파일의 다른 숫자를 쓰지 마라"))
