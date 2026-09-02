# -*- coding: utf-8 -*-
"""★ 처방 R5 — 「야간 정비반」 EYES 개정 좌표 한 벌.

리더가 고른 레버 (b) 렌즈 2분할은 **기하학적으로 구멍을 못 만든다**(r5_lens.py §1·§2).
같은 목표(6/6 머리 노출을 기본 중앙값 위로)를 **다른 한 개의 스칼라**로 달성한다:

    방진 마스크 컵을 얼굴에서 **떼어 가슴께로 내린다** (Δy = MASK_DROP).
    「작업 중 마스크를 턱 밑으로 내려 건 상태」 — 정비공의 실제 착용 상태다.

바뀌는 것은 EYES 1종뿐이다. HEAD·NECK·BACK·HAIR·PET 좌표는 **한 글자도 안 바꾼다.**
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
from rig import Shape

MASK_DROP = -0.78          # ★ 이 라운드가 바꾸는 단 하나의 수

def eyes_respirator_v2():
    # 렌즈 — v1 그대로. 세로 0.66R 은 잉크 사각형 하한(1.5획@0.60 = 0.6447R)에서 온 값이다.
    lens = [(-1.06, 0.68), (1.06, 0.68), (1.02, 0.02), (-1.02, 0.02)]
    # 마스크 컵(★보조색) — v1 과 **모양이 같고 위치만 내려간다**. 카드 아이콘이 안 바뀐다.
    cup = [(0.06, 0.00), (0.70, 0.00), (1.04, -0.38), (0.96, -0.92), (0.42, -1.16), (0.02, -0.76)]
    cup = [(x, y + MASK_DROP) for x, y in cup]
    # 걸이 끈 — 렌즈 앞아래 모서리에서 컵 테두리로. **머리 원반 밖**(r ≥ 1.02)만 지난다.
    strap = [(1.02, 0.02), (1.28, -0.46), (0.87, -1.04 + (MASK_DROP + 0.85))]
    return [Shape("GoggleLens", lens, True, filled=True),
            Shape("GoggleMaskCup", cup, True, filled=True, tone=1),
            Shape("GoggleHangStrap", strap, False)]


# ═══════════════════════════════════════════════════════════════════════════
# ★ 과제 D 처방 — 「보조색 도형이 색을 지워도 형태로 남는가」
#   r5_mono.py 실측: 앞치마 주머니 0.0% · 가방 덮개 0.0% (= 부모 안에 완전히 잠김).
#   출하 42종에도 같은 것이 5건 있다(긴망토·배낭·짧은망토·판초·단정한머리) — 즉 신형 결함은
#   아니지만, design-art 「색만으로는 팩을 못 맞힌다(ΔE 24.31 < 48.6)」와 정면으로 부딪친다.
#   그래서 **부모 실루엣을 깨는 최소 이동**만 준다. 카드 span 은 둘 다 안 바뀐다.
# ═══════════════════════════════════════════════════════════════════════════
def neck_apronbib_v2():
    panel = [(-0.52, -1.34), (0.62, -1.34), (1.06, -2.16), (1.34, -3.40),
             (0.46, -3.76), (-0.38, -3.38), (-0.44, -2.22)]
    # 주머니를 **옆으로 내보낸다** — 목수 앞치마의 측면 공구집. 오른쪽 두 점이 패널 밖이다.
    # ★ 1차 시안(우측 1.24/1.40)은 **삐져나온 양이 W/2 보다 작아** 패널 윤곽 획에 통째로 먹혔다
    #   (r5_mono 자유 윤곽 0.0%). 삐져나옴 ≥ W/2@0.60 = 0.215 R 이 되도록 다시 밀었다.
    pocket = [(0.16, -2.30), (1.42, -2.30), (1.58, -3.10), (0.22, -3.10)]
    strap = [(0.66, -1.30), (0.16, -0.86)]
    return [Shape("BibPanel", panel, True, filled=True),
            Shape("BibPocket", pocket, True, filled=True, tone=1),
            Shape("BibNeckStrap", strap, False)]


def back_toolbag_v2():
    body = [(-1.34, -2.34), (-2.62, -2.62), (-2.78, -3.72), (-1.52, -4.02), (-1.28, -3.20)]
    # 덮개를 가방 아가리 **위로** 접어 올린다(0.50 R = 1.16획@0.60 만큼). y 최대 −1.84 로 여전히
    # BACK 천장(§B) 아래이고, 카드 span 은 멜빵이 정하므로 2.98 그대로다.
    # ★ 1차 시안(-1.26,-1.84)은 덮개 위쪽 모서리가 **머리카락 뒤커튼 밑으로 들어가** 보조색 생존이
    #   93.0%/91.0% 로 떨어졌다(하한 95%). 안쪽 모서리를 −1.50 으로 물리고 0.29 만 올린다 —
    #   그래도 몸통 윗변보다 0.29 R 위이므로 W/2@0.60(0.215) 을 넘어 자유 윤곽이 남는다.
    flap = [(-1.50, -2.05), (-2.68, -2.33), (-2.72, -3.16), (-1.32, -2.92)]
    strap = [(-1.40, -2.40), (-0.70, -1.55), (0.20, -1.10)]
    return [Shape("BagBody", body, True, filled=True),
            Shape("BagFlap", flap, True, filled=True, tone=1),
            Shape("BagStrap", strap, False)]


def hair_napetie_v2():
    import math
    def a(r, d0, d1, n): return [rig.polar(d0 + (d1 - d0) * i / (n - 1), r) for i in range(n)]
    dome = a(1.56, 12, 202, 9)
    back = [(-1.44, -1.14), (-1.24, -1.98), (-0.80, -2.16), (-0.34, -2.30), (-0.66, -1.50)]
    inn = a(0.58, 196, 16, 5)
    front = [(0.92, -0.30), (1.22, -0.66)]
    # 매듭 띠를 덩어리 **밖으로** 조금 내민다 — 묶은 자리가 튀어나온 그림.
    band = [(-1.66, -1.28), (-0.78, -1.46), (-0.86, -2.06), (-1.74, -1.86)]
    return [Shape("HairMass", dome + back + inn + front, True, filled=True),
            Shape("HairTieBand", band, True, filled=True, tone=1)]


def install():
    """pack_nightshift / pack_fit / inkload 이 보는 EYES 를 개정판으로 바꾼다."""
    import pack_nightshift as P
    P.eyes_respirator = eyes_respirator_v2
    P.neck_apronbib = neck_apronbib_v2
    P.back_toolbag = back_toolbag_v2
    P.hair_napetie = hair_napetie_v2
    P.PACK["EYES"] = ("방진 고글", "equip.eyes.respirator", eyes_respirator_v2, 0.0)
    P.PACK["NECK"] = ("작업 앞치마", "equip.neck.apronbib", neck_apronbib_v2, rig.SHOULDER_R)
    P.PACK["BACK"] = ("연장 가방", "equip.shoulders.toolbag", back_toolbag_v2, rig.SHOULDER_R)
    P.PACK["HAIR"] = ("목덜미 매듭", "look.hair.napetie", hair_napetie_v2, 0.0)
    return P


if __name__ == "__main__":
    for s in eyes_respirator_v2():
        print("%-16s loop=%-5s filled=%-5s tone=%d" % (s.name, s.loop, s.filled, s.tone))
        for x, y in s.pts: print("    (%+.4f, %+.4f)" % (x, y))
