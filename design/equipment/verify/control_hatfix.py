# -*- coding: utf-8 -*-
"""★ 양성 대조 — hatfix.py의 게이트가 **실제로 빨간불을 내는가**.

2026-09-02 이 저장소에서 하룻밤에 거짓 통과 8건이 나왔다. 전부 같은 형태였다 —
**실패한 측정과 성공한 측정이 똑같이 생겼다.** 그래서 처방마다 대조군을 붙인다:
일부러 나쁜 좌표를 넣고 게이트가 **하나씩** 켜지는 것을 눈으로 확인한다.

    python3 control_hatfix.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, headroom as H
from rig import Shape
import hatfix as F

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)
ICON, FIT, IST = 44.0, 0.86, 1.7 * 44 / 40


def gates(HEADSET):
    r1 = si = 0
    for sc in (0.75, 1.00, 1.50):
        w = H.stroke_in_R(sc)
        for n, sh in HEADSET.items():
            for s in sh:
                if rig.rule_one(s, w): r1 += 1
    for n, sh in HEADSET.items():
        pts = [p for s in sh for p in s.pts]
        x0, y0, x1, y1 = rig.bounds(pts); k = ICON * FIT / max(x1 - x0, y1 - y0)
        for s in sh:
            if rig.rule_one(Shape(s.name, [(x*k, y*k) for x, y in s.pts], s.loop, s.filled, s.tone), IST): r1 += 1
            if s.loop and rig.self_intersects(s.pts): si += 1
    thick = area = 0
    for n, sh in HEADSET.items():
        for w in (W75, W60):
            m = H.measure(sh, w)
            if m['depth'] * 2.0 / w < H.HEADROOM_THICKNESS_FLOOR_W: thick += 1
            if m['area'] < H.HEADROOM_AREA_FLOOR: area += 1
    ks = list(HEADSET); pr = {k: rig.profile(HEADSET[k], 0.0) for k in ks}
    sil = min(rig.max_delta(pr[ks[i]], pr[ks[j]]) / W75
              for i in range(len(ks)) for j in range(i + 1, len(ks)))
    return dict(rule1=r1, selfint=si, thick=thick, area=area, sil=sil,
                wrap=len(F.wrap_strict(HEADSET)), c1=len(F.rule_1c(HEADSET, quiet=True)))


def show(label, HEADSET):
    g = gates(HEADSET)
    red = (g['rule1'] or g['selfint'] or g['thick'] or g['area'] or g['sil'] < 1.0 or g['wrap'] or g['c1'])
    print("  %-33s 규칙1 %2d | 자기교차 %d | 두께미달 %d | 면적미달 %d | 실루엣 %4.2f%s | 감쌈 %d | 1-C %d   %s"
          % (label, g['rule1'], g['selfint'], g['thick'], g['area'], g['sil'],
             "✗" if g['sil'] < 1.0 else " ", g['wrap'], g['c1'], "✗ 빨간불" if red else "초록"))


def head(sub):
    d = dict(items.HEAD); d.update(sub); return d


def cap(brim):     return [Shape("HatCrown", F.CAP_CROWN, filled=True), Shape("HatBrim", brim, filled=True, tone=1)]
def fedora(brim):  return [Shape("FedoraBrim", brim, filled=True), Shape("FedoraCrown", F.FED_CROWN, filled=True),
                           Shape("FedoraBand", [(-0.98, 0.10), (0.98, 0.06)], loop=False, tone=1)]
def straw(brim):   return [Shape("StrawBrim", brim, filled=True), Shape("StrawCrown", F.STR_CROWN, filled=True),
                           Shape("StrawBand", [(-0.86, 0.10), (0.86, 0.08)], loop=False, tone=1)]
def beanie(crown, fold=None):
    return [Shape("BeanieCrown", crown, filled=True),
            Shape("BeanieCuff", fold or F.BEA_FOLD, loop=False, tone=2),
            Shape("BeaniePom", F.BEA_POM, filled=True, tone=1)]


P = F.prescribed()
print("╔══ 양성 대조 — 게이트가 실제로 켜지는가 ══╗")
show("대조0 처방 좌표(초록이어야 함)", head(P))
show("NC1 현행 프로덕션 좌표", items.HEAD)

# NC2 — 털모자 밑단만 하한 바로 아래로(−0.26 → −0.36)
c = [(-0.56,-0.36), (-0.96,-0.06), (-1.06,0.52), (-0.62,1.16), (0.00,1.32),
     (0.62,1.14), (1.06,0.50), (0.96,-0.06), (0.56,-0.36)]
show("NC2 털모자 밑단 −0.26→−0.36", head(dict(P, 털모자=beanie(c))))

# NC3 — 중절 챙 밑면을 다시 내림(옛 "ㅁ자 창" 재현)
b = list(F.FED_BRIM); b[5] = (0.94,-0.40); b[6] = (-0.94,-0.42)
show("NC3 중절 챙 밑면 −0.25→−0.41", head(dict(P, 중절모=fedora(b))))

# NC4 — 규칙 1 위반 주입(챙 끝 짧은 변)
b = list(F.CAP_BRIM); b[3] = (1.44,-0.14)
show("NC4 야구 챙끝 짧은 변 주입", head(dict(P, 야구모자=cap(b))))

# NC5 — 자기교차 주입
b = list(F.CAP_BRIM); b[2], b[5] = b[5], b[2]
show("NC5 야구 챙 점 순서 교차", head(dict(P, 야구모자=cap(b))))

# NC6 — 감쌈 파괴(챙 뿌리를 |x| = 0.80으로)
b = list(F.FED_BRIM); b[5] = (0.80,-0.24); b[6] = (-0.80,-0.26)
show("NC6 중절 감쌈 파괴(|x|=0.80)", head(dict(P, 중절모=fedora(b))))

# NC7 — 실루엣 파괴. ★ 이 대조는 한 번 **거짓 초록**을 냈다(2026-09-02): 중절 챙 좌표를 손으로
#   베껴 두었더니 본안이 바뀌면서 둘이 다시 갈라져 게이트가 안 켜졌다. 그래서 이제 **F.FED_BRIM을
#   그대로 참조**한다 — 대조군이 본안을 따라 움직이지 않으면 대조군이 아니다.
show("NC7 밀짚 챙 = 중절 챙 그대로", head(dict(P, 밀짚모자=straw(list(F.FED_BRIM)))))

# NC8 — 두께는 통과하는데 **옆을 다 덮는** 관(면적 게이트 단독 확인)
c = [(-0.62,-0.95), (-0.22,-0.24), (0.22,-0.24), (0.62,-0.95), (0.96,-0.06), (1.06,0.50),
     (0.62,1.14), (0.00,1.32), (-0.62,1.16), (-1.06,0.52), (-0.96,-0.06)]
show("NC8 두께OK·옆을 다 덮음(면적 단독)", head(dict(P, 털모자=beanie(c))))

# NC9 — 규칙 1-C 단독: 챙을 균일하게 얇게(어디에도 두꺼운 자리가 없다)
b = [(-1.68, 0.18), (-0.98, 0.10), (0.98, 0.06), (2.06, 0.14),
     (1.30,-0.14), (0.94,-0.20), (-0.94,-0.22), (-1.30,-0.14)]
show("NC9 중절 챙 전 구간 얇게(1-C 단독)", head(dict(P, 중절모=fedora(b))))
print("╚════════════════════════════════════════════════════════════════════════════════╝")
