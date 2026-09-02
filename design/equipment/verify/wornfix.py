# -*- coding: utf-8 -*-
"""★ 2026-09-02 처방 — "착용하면 물건으로 안 읽힌다" 3건.  design-equipment

  ① 털모자가 '머리 윗부분 색칠'로 읽힌다            (소은 실기 + 민지 독립 확인)
  ② 카드 아트 != 착용 아트 2건 (폼폼 / 망토 클래스프)
  ③ EYES 2종이 서로 다른 시각 언어를 쓴다

재현:  python3 wornfix.py          (본안)
       python3 control_wornfix.py  (양성 대조 — 게이트가 실제로 켜지는가)

수치 근거와 실기 실측 교정은 wornread.py 머리말에 있다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H, wornread as WR
from rig import Shape

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)      # 낱선
F75, F60 = WR.w_fill(0.75), WR.w_fill(0.60)              # 채움 경계선
SH, COLLARY, TL = rig.SHOULDER_R, rig.SHOULDER_R + 0.10, rig.TORSO_R
ICON, FIT, IST = 44.0, 0.86, 1.7 * 44 / 40

# ═══════════════════════════════════════════════════════════════════════════
# ① 털모자 v3 — 부피를 머리 원 **밖**으로. 밑단이 머리 밖에서 끝나 단차를 만든다.
# ═══════════════════════════════════════════════════════════════════════════
BEA_HEM_Y        = -0.26     # 잉크 밑단(=남는 머리 예산) — 현행 그대로 손대지 않는다
BEA_HEM_HALF     =  1.28     # ← 0.56.  밑단 꼭짓점이 머리 원(1.0R) **밖**
# ★★ 접힘선(BeanieCuff)을 **없앤다.** 취향이 아니라 산술이다.
#   Shade 톤 = FillOutlineColor(주색) 이고(CharacterAccessoryRenderer.ToneColor) 관의 채움
#   경계선도 **같은 그 색**이다. 두 줄 사이에 남는 채움 색 띠가 1획 미만이면 화면에서 한 줄로
#   합쳐진다 — 머리카락 5종을 '뚜껑'으로 만들었던 바로 그 산술이다.
#       필요한 단 깊이 >= W_line + W_line/2 + W_fill/2
#                      = 0.4298 + 0.2149 + 0.1091 = 0.7539 R   (배율 0.60 기준)
#   그만큼 깊게 잡으면 관이 눌려 **양동이**가 된다(오프라인 A/B 시안에서 눈으로 확인 —
#   design/equipment/wornfix-ab-0.60.png 왼쪽). 그래서 37-6 규칙 5의 선례를 따른다:
#   "예산을 못 지키는 [선택] 디테일은 넣지 않는다"(중절모 크리스를 지운 그 조항).
#   단은 이제 **실루엣 단차**가 말한다 — 밑단이 머리 밖에서 끝나고 옆면이 곧게 선다.
BEA_CUFF_TOP_Y   =  0.38     # 관 옆면이 곧게 서다 꺾이는 높이(= 이 모자의 최대폭 지점)
BEA_CUFF_HALF    =  1.32     # ← 0.96.  이 모자의 최대폭
BEA_COVER_Y      = -0.06     # HatCoverLocalY — **바뀌지 않는다**(머리카락 자르기)
BEA_APEX_Y       =  1.16
BEA_POM_R        =  0.44     # ← 0.28.  고리화(테두리가 반지름에서 먹는 비율) 0.41 -> 0.26
BEA_POM_N        =  10       # ← 10 그대로. 각수는 문제가 아니었다(아래 gate_minedge 주석)
BEA_POM_TOP      =  1.72     # ★ 오늘과 **같은 값**(액자 상한 1.80R 미변경)
BEA_POM_BACK     =  0.10

BEA_CROWN = [(-BEA_HEM_HALF, BEA_HEM_Y), (-BEA_CUFF_HALF, BEA_CUFF_TOP_Y),
             (-1.10, 0.82), (-0.68, 1.06), (-0.24, BEA_APEX_Y),
             ( 0.24, 1.14), ( 0.68, 1.04), ( 1.10, 0.80),
             ( BEA_CUFF_HALF, 0.36), ( BEA_HEM_HALF, BEA_HEM_Y)]
BEA_POM  = rig.poly(-BEA_POM_BACK, BEA_POM_TOP - BEA_POM_R, BEA_POM_R, BEA_POM_N, 90.0)


def beanie_v3():
    return [Shape("BeanieCrown", BEA_CROWN, filled=True),
            Shape("BeaniePom",   BEA_POM,  filled=True, tone=1)]


# ═══════════════════════════════════════════════════════════════════════════
# ③ EYES — 같은 부품은 같은 톤. 보조색은 **앞 렌즈**(면적이 있는 결정적 특징).
# ═══════════════════════════════════════════════════════════════════════════
SUN_BACK  = [(-0.28, 0.34), (-0.96, 0.30), (-1.02, -0.16), (-0.32, -0.44)]
SUN_FRONT = [( 0.322, -0.44), (1.057, -0.16), (0.994, 0.30), (0.28, 0.34)]
#: 코다리 — 옛 3점 아치(꼭대기 0.46R)가 렌즈 **위로** 솟아 밝은 ∧를 만들었다(깃발/새).
#  곧게 눕히고 톤을 **주색**으로 내린다. 양끝이 렌즈 안으로 0.08R씩 들어가 세 도형이 한 덩어리가
#  된다(틈 0은 규칙 4가 허용한다 — 금지되는 것은 0 < 틈 < 1획이다).
SUN_BRIDGE = [(-0.36, 0.32), (0.36, 0.32)]


def sunglasses_v2():
    return [Shape("SunglassLensBack",  SUN_BACK,   filled=True),
            Shape("SunglassBridge",    SUN_BRIDGE, loop=False),
            Shape("SunglassLensFront", SUN_FRONT,  filled=True, tone=1)]


# ★ 렌즈는 **손대지 않는다**(중심 ±0.62, r 0.40, 12각). 이유는 gate_minedge 주석 참고 —
#   12각형의 새그(원과의 최대 편차)는 0.0136R = 0.03획이라 화면에서 원과 구분되지 않는다.
#   '동그란'이라는 이름을 지키면서 회귀 위험이 0인 쪽을 고른다.
RND_OFF, RND_R, RND_N = 0.62, 0.40, 12
RND_CY = 0.02
_rb = rig.poly(-RND_OFF, RND_CY, RND_R, RND_N, 0.0)
_rf = rig.poly( RND_OFF, RND_CY, RND_R, RND_N, 0.0)
#: 코다리 — 렌즈 **윗** 꼭짓점(60도 / 120도, 인덱스 2와 4)을 곧게 잇는다.
#  ① 옛 아치는 꼭대기 0.50R로 렌즈 **위로 솟아** 밝은 ∧를 만들었다(선글라스와 같은 그림).
#  ② 톤을 보조색 -> **주색**으로 내린다. 같은 슬롯의 같은 부품이 두 색이던 것이 여기서 닫힌다.
#  ③ 잉크 사각형이 0.5472R -> 0.84R로 늘어 규칙 1의 마지막 위반(최소 배율 0.7070)이 풀린다.
RND_BRIDGE = [_rb[2], _rf[4]]


def round_glasses_v2():
    return [Shape("RoundLensBack",  _rb, filled=True),
            Shape("RoundBridge",    RND_BRIDGE, loop=False),
            Shape("RoundLensFront", _rf, filled=True, tone=1)]


# ═══════════════════════════════════════════════════════════════════════════
# ② 망토 클래스프 — 목 획이 정확히 가운데를 먹는다. 보조색을 **밑단 단**으로 옮긴다.
# ═══════════════════════════════════════════════════════════════════════════
HEM_BAND_H = 0.52


def hem_band(outline_pts, h=HEM_BAND_H):
    """망토 밑단(뒤끝~앞끝 5점)을 h 만큼 위로 민 띠. 밑단 사슬은 x 단조라 자기교차가 없다."""
    hem = outline_pts[2:]
    top = [(x, y + h) for x, y in reversed(hem)]
    return hem + top


def cape_v2(length, spread, front_spread, wave, notch=0.0):
    o = items.cape_outline(length, spread, front_spread, wave, notch)
    return [Shape("CapeOutline", o, filled=True),
            Shape("CapeFold",  items.cape_fold(length, spread, 0.35, 0.80 if notch else 0.0), loop=False, tone=2),
            Shape("CapeFold2", items.cape_fold(length, spread, 0.72, 0.96 if notch else 0.0), loop=False, tone=2),
            Shape("CapeHemBand", hem_band(o), filled=True, tone=1)]


# ── (시험만 함, 처방 아님) 왕관 — 게이트가 **예측**하는 다음 신고. LV.20 잠김이라 실착용 미확인.
#    ★ 이 좌표를 함께 넣으면 HEAD 쌍별 최소 실루엣 차가 1.80 -> 1.42획으로 **떨어진다**:
#      털모자와 왕관이 둘 다 반폭 1.26~1.32R로 옮겨 가면서 다시 붙는다. 즉 왕관의 단차는
#      털모자와 **다른 기하 수단**으로 벌어야 한다. 이번 라운드 범위가 아니므로 기본값은 끈다.
CROWN_HALF = 1.26          # <- 0.98
CROWN_BODY = [(-CROWN_HALF, 0.02), (-1.10, 1.28), (-0.58, 0.62), (0.00, 1.52),
              ( 0.58, 0.62), ( 1.10, 1.28), (CROWN_HALF, 0.02),
              ( 0.76, -0.16), (-0.76, -0.16)]
CROWN_RIM  = [(-CROWN_HALF, 0.02), (-0.76, -0.16), (0.76, -0.16), (CROWN_HALF, 0.02)]


def crown_v2():
    return [Shape("CrownBody", CROWN_BODY, filled=True),
            Shape("CrownRim",  CROWN_RIM,  loop=False, tone=1)]


def prescribed(with_crown=False):
    head = dict(items.HEAD);  head["털모자"] = beanie_v3()
    if with_crown: head["왕관"] = crown_v2()
    eyes = dict(items.EYES);  eyes["선글라스"] = sunglasses_v2(); eyes["동그란안경"] = round_glasses_v2()
    back = dict(items.BACK)
    back["짧은망토"] = cape_v2(1.35, 2.45, 0.85, 0.22)
    back["긴망토"]   = cape_v2(1.85, 3.10, 1.05, 0.30, 0.42)
    back["판초"]     = cape_v2(1.05, 1.95, 1.55, 0.12)
    return head, eyes, back


def install(head, eyes, back):
    items.HEAD, items.EYES, items.BACK = head, eyes, back


# ═══════════════════════════════════════════════════════════════════════════
# 게이트
# ═══════════════════════════════════════════════════════════════════════════
# ★ 하한은 **단차 하나**다. 옆 부조는 보고만 한다 — 근거:
#   6종 실측에서 옆 부조는 0.09~0.47획으로 **전부** 1획 미만인데, 그중 사용자가 신고한 것은
#   털모자 하나뿐이다. 즉 옆 부조에 하한을 걸면 **아무 불만도 없는 모자 4종이 같이 빨간불**이 된다.
#   그건 게이트가 아니라 발명이다. 반면 밑단 단차는 신고/무신고를 정확히 가른다:
#       털모자 -0.17획(신고)  |  왕관 -0.09획(LV.20 잠김·미착용)
#       야구 4.35 / 밀짚 2.80 / 중절 2.35 / 베레 2.12  (무신고)
#   빈 창이 -0.17 ~ +2.12 이므로 하한 1.00획이 그 안에 든다(headroom.py와 같은 교정 구조).
STEP_FLOOR    = 1.00        # 밑단 단차 **하한**(획, 채움 경계선 기준)
LATERAL_TARGET = 0.75       # 옆 부조 **목표**(하한 아님). 사용자 신고 교정 없음 — 설계 지향값.


def gate_relief(HEAD, quiet=False):
    bad = []
    if not quiet:
        print("╔══ ① 실루엣 부조 — 모자가 머리 원(1.0R) 밖으로 나가는가 (W_fill=%.4fR) ══╗" % F75)
        print("     밑단 바깥 단차 = 하한 / 옆띠 %d~%d도 최소부조 = 목표" % (WR.LATERAL_LO, WR.LATERAL_HI))
    for n, sh in HEAD.items():
        lat = WR.lateral_relief(sh); step = WR.hem_step(sh)
        ok = step >= STEP_FLOOR * F75 - 1e-9
        if not ok: bad.append("%s 단차 %.2f획" % (n, step / F75))
        if not quiet:
            print("  %s %-6s 밑단단차 %+.3fR = %5.2f획 [하한 %.2f]   옆부조 %+.3fR = %5.2f획 [목표 %.2f]%s"
                  % ("OK " if ok else "✗  ", n, step, step / F75, STEP_FLOOR, lat, lat / F75,
                     LATERAL_TARGET, "" if lat / F75 >= LATERAL_TARGET else "  (목표 미달)"))
    if not quiet: print("╚══ 부조 위반 %d건 ══╝" % len(bad))
    return bad


def gate_ring(ALL, quiet=False):
    """채운 도형이 테두리에 먹혀 '반지'가 되는가. 카드와 착용을 **같이** 잰다."""
    bad = []
    if not quiet:
        print("╔══ ③ 고리화 — 테두리가 반지름에서 먹는 비율 (상한 %.2f) ══╗" % WR.RIM_FRACTION_CEIL)
    for cat, d in ALL:
        for n, sh in d.items():
            span = max(rig.bounds([p for s in sh for p in s.pts])[2] - rig.bounds([p for s in sh for p in s.pts])[0],
                       rig.bounds([p for s in sh for p in s.pts])[3] - rig.bounds([p for s in sh for p in s.pts])[1])
            wc = WR.w_card(span)
            for s in sh:
                if not s.filled: continue
                # ★ 고리로 읽히는 것은 **뭉툭한** 도형뿐이다. 긴 띠는 테두리가 있어도 띠로 읽힌다
                #   (짧은 변이 테두리에 먹히면 그건 '고리'가 아니라 '가는 선'이고 규칙 1이 이미 본다).
                rho = WR.inradius(s.pts)
                # 뭉툭함 = 면적 / (pi * 내접원 면적). 원 1.00 / 정사각 1.27 / 가는 띠는 커진다.
                # 긴 띠는 테두리가 있어도 '고리'로 안 보인다 — 그건 규칙 1이 보는 다른 문제다.
                if rho < 1e-9 or WR.poly_area(s.pts) > 2.0 * math.pi * rho * rho: continue
                worn, card = (F75 * .5) / rho, (wc * .5) / rho
                ok = worn <= WR.RIM_FRACTION_CEIL + 1e-9
                # 카드와 착용이 문턱을 사이에 두고 **갈리는** 것이 '카드는 공, 착용은 반지'다
                split = (card <= WR.RIM_FRACTION_CEIL) and (worn > WR.RIM_FRACTION_CEIL)
                if not ok: bad.append("%s %s %s 착용 %.2f%s" % (cat, n, s.name, worn, " ★카드↔착용 갈림" if split else ""))
                if not quiet and (not ok or split):
                    print("  ✗   %-5s %-6s %-16s 내접 %.3fR  카드 %.2f  착용 %.2f%s"
                          % (cat, n, s.name, rho, card, worn, "  ★카드는 공/착용은 반지" if split else ""))
    if not quiet: print("╚══ 고리화 위반 %d건 ══╝" % len(bad))
    return bad


def gate_occlusion(ALL, quiet=False):
    """몸(목/몸통) 세로 획이 자르고 남은 조각이 규칙 1의 잉크 사각형(1.5획)을 지키는가.
    ★ 카드는 몸 없이 그린다 — 이 게이트가 없으면 '카드는 막대 하나, 착용하면 얼룩 둘'이 그대로 나간다."""
    bad = []
    band = W60 * 0.5          # 배율 0.60(사용자 저장 배율)에서 가장 두껍다
    floor = 1.5 * W60
    if not quiet:
        print("╔══ ④ 몸가림 잔존 — 몸 획(|x|<=%.3fR)이 자른 뒤 남는 조각 (하한 %.3fR = 1.5획@0.60) ══╗" % (band, floor))
    for cat, d in ALL:
        for n, sh in d.items():
            for s in sh:
                if not s.filled: continue
                # ★ 세로 몸 획은 **머리 아래**에만 있다. 얼굴 위 아이템(HEAD/EYES/HAIR)은 대상이 아니다
                #   — 머리는 채운 원반이지 획이 아니고, 모자/안경은 그 위에 그려진다.
                if min(q[1] for q in s.pts) > -1.0: continue
                for w, h in WR.occlusion_pieces(s.pts, band):
                    if max(w, h) < floor - 1e-9:
                        bad.append("%s %s %s 조각 %.3fx%.3fR" % (cat, n, s.name, w, h))
                        if not quiet:
                            print("  ✗   %-5s %-6s %-16s 남은 조각 %.3f x %.3f R (최대변 %.2f획)"
                                  % (cat, n, s.name, w, h, max(w, h) / W60))
    if not quiet: print("╚══ 몸가림 위반 %d건 ══╝" % len(bad))
    return bad


def is_circle_poly(s):
    """정n각형(원 근사)인가 — 모든 꼭짓점이 중심에서 같은 거리이고 등간격인가."""
    if not s.loop or len(s.pts) < 5: return False
    cx = sum(p[0] for p in s.pts) / len(s.pts)
    cy = sum(p[1] for p in s.pts) / len(s.pts)
    rr = [math.hypot(p[0] - cx, p[1] - cy) for p in s.pts]
    return max(rr) - min(rr) < 1e-6 * max(1.0, max(rr))


def sagitta(s):
    """정n각형이 이상적인 원에서 벗어나는 최대량 = r(1 - cos(pi/n))."""
    cx = sum(p[0] for p in s.pts) / len(s.pts)
    cy = sum(p[1] for p in s.pts) / len(s.pts)
    r = math.hypot(s.pts[0][0] - cx, s.pts[0][1] - cy)
    return r * (1.0 - math.cos(math.pi / len(s.pts)))


#: 원 근사 도형이 '진짜 원'으로 통하는 새그 상한. 1/4획이면 안티에일리어싱 한 칸 안이다.
SAGITTA_CEIL_W = 0.25


def gate_minedge(ALL, quiet=False):
    """★ 원 근사는 면제한다 — 대신 **새그**로 잰다.
    최단 변 하한은 '너무 짧아 꺾임이 뭉갠다'를 잡으려는 자다. 그런데 정n각형으로 그린 **원**은
    꺾임이 뭉개지는 것이 **목적**이다(그래야 원으로 보인다). 그 둘을 같은 자로 재면
    폼폼·렌즈·방울·외알 테처럼 '원이라서 짧은' 변이 전부 위반으로 잡힌다 — 실제로 지금
    되살아난 14건의 상당수가 그것이다. 원 근사에는 새그(원과의 최대 편차)를 쓴다:
        12각 r=0.40 -> 0.0136R = 0.032획   |   10각 r=0.44 -> 0.0215R = 0.050획
    둘 다 화면에서 원과 구분되지 않는다. **면제한 건수는 따로 세어 보고한다**(숨기지 않는다)."""
    floor = W60
    bad, exempt = [], []
    if not quiet: print("╔══ 최단 실제 변 (하한 %.4fR = 1획@0.60 · 원 근사는 새그로) ══╗" % floor)
    for cat, d in ALL:
        for n, sh in d.items():
            for s in sh:
                m = len(s.pts)
                best = min(math.dist(s.pts[i], s.pts[(i + 1) % m])
                           for i in range(m if s.loop else m - 1))
                if best < floor - 1e-9 and is_circle_poly(s):
                    sg = sagitta(s)
                    if sg <= SAGITTA_CEIL_W * W60:
                        exempt.append("%s %s %s 새그 %.4fR=%.3f획" % (cat, n, s.name, sg, sg / W60))
                        continue
                if best < floor - 1e-9:
                    bad.append("%s %s %s %.4fR" % (cat, n, s.name, best))
                    if not quiet:
                        print("  ✗   %-5s %-6s %-16s 최단 %.4fR = %.2f획" % (cat, n, s.name, best, best / floor))
    if not quiet:
        for e in exempt: print("  면제  %s (원 근사)" % e)
        print("╚══ 최단 변 위반 %d건 (원 근사 면제 %d건) ══╝" % (len(bad), len(exempt)))
    return bad


def summary(tag):
    ALL = [("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
           ("BACK", items.BACK), ("HAIR", hair.SET)]
    return dict(relief=len(gate_relief(items.HEAD, True)),
                ring=len(gate_ring(ALL, True)),
                occl=len(gate_occlusion(ALL, True)),
                edge=len(gate_minedge(ALL, True)))


if __name__ == "__main__":
    print("── 착용 판독성 처방 (털모자 / EYES 2종 / 망토 클래스프) ──")
    print("   W_line@0.75 %.5fR   W_fill@0.75 %.5fR   W_line@0.60 %.5fR   W_fill@0.60 %.5fR\n"
          % (W75, F75, W60, F60))
    base = summary("현행")
    h, e, b = prescribed(); install(h, e, b)
    print("★ 현행 대비 (처방 설치 후 아래 표가 나온다)\n")
    gate_relief(items.HEAD); print()
    ALL = [("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
           ("BACK", items.BACK), ("HAIR", hair.SET)]
    gate_ring(ALL); print()
    gate_occlusion(ALL); print()
    gate_minedge(ALL); print()
    now = summary("처방")
    print("┌─ 요약 ─────────────────────────────────────────────┐")
    for k, label in (("relief", "① 부조"), ("ring", "③ 고리화"), ("occl", "④ 몸가림"), ("edge", "최단 변")):
        print("│ %-10s 현행 %3d건  ->  처방 %3d건%s" % (label, base[k], now[k],
              "  ★" if now[k] < base[k] else ("" if now[k] == base[k] else "  ✗ 악화")))
    print("└────────────────────────────────────────────────────┘")
