# -*- coding: utf-8 -*-
"""★ 과제 A — 리더가 고른 레버 (b) **렌즈 2분할**을 재고 판정한다.

내가 낸 약점: 「팩 6/6 머리 노출 7.4%@0.75 < 기본 24조합 중앙 12.6%」.
리더 지시: 2분할로 얼굴 띠에 구멍을 내라. 반론이 있으면 숫자로 대라.

  §1  산술 — 2분할이 **구멍을 만들 수 있는가** (39-5와 같은 형태의 증명)
  §2  독립 확인 — 잉크 구간 개수를 눈높이에서 센다(산술과 다른 자)
  §3  상한 — 2분할이 **원리적으로** 되찾을 수 있는 머리 면적의 최댓값
  §4  귀속 — 6/6에서 머리를 덮는 것이 실제로 무엇인가 (단독 / 한계)
  §5  대안 레버 스윕 — 규칙을 하나도 안 깨고 노출을 올릴 수 있는가
  §6  선택된 처방의 6/6 재측정

  python3 r5_lens.py            # 전문
  python3 r5_lens.py --control  # 양성 대조
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H
import pack_nightshift as P
from rig import Shape
INF = float("inf")

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)
EYE_X, PUP = rig.EYE_X, rig.PUPIL_R
FAIL = []
def bad(m): FAIL.append(m); print("  ✗ " + m)


# ═══════════════════════════════════════════════════════════════════════════
def calib():
    print("╔══ §0 교정 (깨지면 아래 숫자 전부 폐기) ══╗")
    ok = True
    for w, nm in ((W75, "0.75"), (W60, "0.60")):
        e0 = H.head_area_ratio([], w)
        e1 = H.head_area_ratio([Shape("Big", [(-9, -9), (9, -9), (9, 9), (-9, 9)], True, True)], w)
        good = abs(e0 - 1) < 1e-4 and e1 < 1e-6
        ok &= good
        print("  W@%s  빈 것 %.6f(기대 1)  전부덮음 %.6f(기대 0)  %s" % (nm, e0, e1, "OK" if good else "FAIL"))
    # 세 번째 교정: 반지름 0.5 원판은 머리의 정확히 25%를 덮는다 (면적비 0.25)
    disc = [Shape("D", rig.arc(0.5, 0, 360, 721)[:-1], True, True)]
    e2 = H.head_area_ratio(disc, 1e-9)      # 획 0 -> 순수 다각형
    good = abs((1 - e2) - 0.25) < 2e-3
    ok &= good
    print("  r=0.5 원판 덮음 %.4f (기대 0.2500)  %s" % (1 - e2, "OK" if good else "FAIL"))
    print("╚═══════════════════════════════════════╝")
    if not ok: sys.exit("교정 실패")


# ═══════════════════════════════════════════════════════════════════════════
def s1_arith():
    print("\n╔══ §1 산술 — 2분할이 구멍을 만들 수 있는가 ══╗")
    print("  전제 3개는 전부 이 저장소에서 이미 확정된 것이다:")
    print("   (가) 두 눈 간격 2·EYE_X = %.4f R      (EyeOffsetXInHeadRadii)" % (2 * EYE_X))
    print("   (나) 눈동자 반경 PUPIL = %.4f R        (39-5 「눈은 가리개 옆에만」)" % PUP)
    print("   (다) 채움 다각형은 윤곽 획으로 **양쪽 W/2씩 부푼다** -> 보이는 틈 = 좌표틈 − W")
    print()
    print("  %-46s %10s %10s" % ("", "@0.75", "@0.60"))
    print("  %-46s %10.4f %10.4f" % ("획 W", W75, W60))
    gmax_p = 2 * (EYE_X - PUP)
    gmax_c = 2 * EYE_X
    print("  %-46s %10.4f %10.4f" % ("좌표틈 상한 g_max — 눈동자까지 덮으려면", gmax_p, gmax_p))
    print("  %-46s %10.4f %10.4f" % ("  (느슨한 판: 눈 **중심**만 덮으면 된다면)", gmax_c, gmax_c))
    print("  %-46s %10.4f %10.4f" % ("틈 하한 g_min — 틈이 획 1개보다 얇으면 틈이 아니다", 2 * W75, 2 * W60))
    print("      ↑ 「보이는 틈 ≥ 1획」 = 좌표틈 ≥ 2W.  이 판정선은 내가 새로 만든 게 아니라")
    print("        머리 5종 '뚜껑' 결함(두피 링 0.41~0.81획이 링 획과 한 줄로 합쳐짐)의 그 선이다.")
    print()
    for nm, w in (("0.75", W75), ("0.60", W60)):
        for lab, gmax in (("눈동자 덮음(정석)", gmax_p), ("눈 중심만 덮음(느슨)", gmax_c)):
            vis = gmax - w
            verdict = "★불가 — 잉크가 겹친다(한 장이 된다)" if vis <= 0 else (
                      "불가 — 보이는 틈 %.2f획 < 1획" % (vis / w) if vis < w else "가능")
            print("  @%s %-22s g_max %.4f -> 보이는 틈 %+.4f R = %+.2f획 · %+.2f pt   %s"
                  % (nm, lab, gmax, vis, vis / w, vis * (0.22 * float(nm)) * rig.PT_PER_UNIT, verdict))
    print()
    print("  ★ 결론(산술): 눈동자를 덮으면서 2분할하면 @0.60에서 두 렌즈의 잉크가 **%.4f R 겹친다.**"
          % (W60 - gmax_p))
    print("     @0.75에서도 보이는 틈은 %.4f R = %.2f획 = %.2f pt (머리 지름 11.63pt) 뿐이다."
          % (gmax_p - W75, (gmax_p - W75) / W75, (gmax_p - W75) * 0.22 * 0.75 * rig.PT_PER_UNIT))
    return gmax_p, gmax_c


# ═══════════════════════════════════════════════════════════════════════════
def split_lens(g, top=0.68, bot=0.02, out=1.06):
    """좌표틈 g 로 2분할한 렌즈 2장."""
    i = g / 2.0
    L = [(-out, top), (-i, top), (-i, bot), (-out, bot)]
    Rr = [(i, top), (out, top), (out, bot), (i, bot)]
    return [Shape("LensBack", L, True, filled=True), Shape("LensFront", Rr, True, filled=True)]


def s2_spans(gmax_p, gmax_c):
    print("\n╔══ §2 독립 확인 — 눈높이에서 잉크 구간을 **센다**(산술과 다른 자) ══╗")
    print("  눈 y = %.4f. 그 수평선에서 잉크가 몇 조각인가. 2조각이라야 '2분할'이다." % rig.EYE_Y)
    print("  %-30s %8s %8s   %s" % ("변형", "@0.75", "@0.60", "비고"))
    cases = [("현행 한 장 바이저", None),
             ("2분할 g=g_max(눈동자)  %.4f" % gmax_p, gmax_p),
             ("2분할 g=g_max(눈중심)  %.4f" % gmax_c, gmax_c),
             ("2분할 g=2W@0.60        %.4f" % (2 * W60), 2 * W60)]
    for lab, g in cases:
        cells = []
        for w in (W75, W60):
            sh = P.eyes_respirator()[:1] if g is None else split_lens(g)
            sp = H.ink_spans(sh, rig.EYE_Y, w)
            cells.append(len(sp))
        note = ""
        if g is not None:
            covp = all(any(x0 - 1e-9 <= s * EYE_X - PUP and s * EYE_X + PUP <= x1 + 1e-9
                           for x0, x1 in H.ink_spans(split_lens(g), rig.EYE_Y, 1e-9)) for s in (1, -1))
            note = "눈동자 덮음 " + ("OK" if covp else "✗ 눈동자가 렌즈 밖으로 나온다")
        print("  %-30s %8d %8d   %s" % (lab, cells[0], cells[1], note))
    print("  ★ 산술과 일치: 눈동자를 덮는 어떤 g 로도 @0.60에서 조각이 2개가 되지 않는다.")


# ═══════════════════════════════════════════════════════════════════════════
def pack_order(eyes=None, head=None, hairsh=None):
    return [(P.back_toolbag(), INF), (hairsh or P.hair_napetie(), 0.50),
            (P.neck_apronbib(), INF), (eyes or P.eyes_respirator(), INF),
            (head or P.head_havelock(), INF)]


def expose(order, w):
    return H.head_area_ratio([x for s, _ in order for x in s], w)


def s3_upper():
    print("\n╔══ §3 상한 — 2분할이 **원리적으로** 되찾는 머리 면적 ══╗")
    print("  획 팽창이 없다고 **가정**한 가짜 세계에서 재서, 레버의 천장을 구한다.")
    base = P.eyes_respirator()
    for w, nm in ((W75, "0.75"), (W60, "0.60")):
        e0 = expose(pack_order(), w)
        rows = []
        for g in (0.0, gmax_p, gmax_c, 0.90):
            sh = split_lens(g) + base[1:] if g > 0 else base
            e = expose(pack_order(eyes=sh), w)
            rows.append((g, e))
        print("  @%s 현행 %.1f%%" % (nm, e0 * 100)
              + "".join("   g=%.3f -> %.1f%%" % (g, e * 100) for g, e in rows[1:]))
    print("  ★ 위 숫자는 **실제 획을 포함한 것**이다. g=%.3f(눈동자 상한)이 사실상 0 이득인 이유가 §1이다."
          % gmax_p)


# ═══════════════════════════════════════════════════════════════════════════
def s4_attrib():
    print("\n╔══ §4 귀속 — 6/6에서 머리를 덮는 것은 무엇인가 ══╗")
    names = ["BACK 연장가방", "HAIR 목덜미", "NECK 앞치마", "EYES 고글", "HEAD 목덮개"]
    for w, nm in ((W75, "0.75"), (W60, "0.60")):
        o = pack_order()
        full = expose(o, w)
        print("  @%s  6/6 머리 노출 %.1f%%" % (nm, full * 100))
        print("     %-14s %10s %10s" % ("", "단독 덮음", "한계 기여"))
        for i, (sh, cv) in enumerate(o):
            solo = 1 - H.head_area_ratio(sh, w)
            rest = [x for j, (s2, _) in enumerate(o) if j != i for x in s2]
            marg = H.head_area_ratio(rest, w) - full
            print("     %-14s %9.1f%% %9.1f%%" % (names[i], solo * 100, marg * 100))
        # 기본 머리 6종 단독 덮음 — 공정 비교
        print("     [대조] 기본 머리 6종 단독 덮음: " +
              " ".join("%s %.0f%%" % (n, (1 - H.head_area_ratio(f() if callable(f) else f, w)) * 100)
                       for n, f in hair.SET.items()))


# ═══════════════════════════════════════════════════════════════════════════
def hair_variant(inner):
    CAP = P.CAP
    def a(r, d0, d1, n): return [rig.polar(d0 + (d1 - d0) * i / (n - 1), r) for i in range(n)]
    dome = a(CAP, 12, 202, 9)
    back = [(-1.44, -1.14), (-1.24, -1.98), (-0.80, -2.16), (-0.34, -2.30), (-0.66, -1.50)]
    inn = a(inner, 196, 16, 5)
    front = [(0.92, -0.30), (1.22, -0.66)]
    band = [(-1.50, -1.32), (-0.80, -1.46), (-0.88, -2.04), (-1.58, -1.90)]
    return [Shape("HairMass", dome + back + inn + front, True, filled=True),
            Shape("HairTieBand", band, True, filled=True, tone=1)]


def eyes_variant(out=1.06, top=0.68, bot=0.02, cup_dy=0.0, cup_x0=0.02):
    lens = [(-out, top), (out, top), (out - 0.04, bot), (-out + 0.04, bot)]
    cup = [(0.06, 0.00), (0.70, 0.00), (1.04, -0.38), (0.96, -0.92), (0.42, -1.16), (0.02, -0.76)]
    cup = [(x + (cup_x0 - 0.02), y + cup_dy) for x, y in cup]
    strap = [(-out + 0.02, 0.56), (-1.52, 0.92)]
    return [Shape("GoggleLens", lens, True, filled=True),
            Shape("GoggleMaskCup", cup, True, filled=True, tone=1),
            Shape("GoggleStrap", strap, False)]


def head_variant(bot=0.72):
    crown = [(0.80, bot), (1.02, 1.16), (0.50, 1.54), (-0.50, 1.54), (-1.02, 1.16), (-0.80, bot)]
    cloth = [(-0.80, bot), (-1.24, 0.34), (-1.48, -0.34), (-1.28, -0.98), (-0.82, -0.90), (-0.80, -0.16)]
    seam = [(-0.78, bot + 0.14), (0.78, bot + 0.10)]
    return [Shape("CapCrown", crown, True, filled=True),
            Shape("CapNeckCloth", cloth, True, filled=True, tone=1),
            Shape("CapSeam", seam, False)]


def s5_sweep():
    print("\n╔══ §5 대안 레버 스윕 — 규칙을 하나도 안 깨고 노출을 올릴 수 있는가 ══╗")
    print("  규칙: 잉크사각형 ≥ 1.5획@0.60 = %.4f · 변 ≥ 1획@0.60 = %.4f ·" % (1.5 * W60, W60))
    print("        HAIR 부착 r_min ≤ 1−W@0.75 = %.4f · 눈동자 덮음 · 액자 잉크 ≤ 1.80" % (1 - W75))
    w = W75
    base = expose(pack_order(), w)
    print("\n  [L1] 렌즈 바깥 |x| (현행 1.06)  — 눈동자 덮으려면 ≥ %.4f" % (EYE_X + PUP))
    for out in (1.06, 0.98, 0.90, 0.82, 0.74):
        e = expose(pack_order(eyes=eyes_variant(out=out)), w)
        rect = 2 * out
        print("     out=%.2f  노출 %.1f%% (%+.1f%%p)  잉크사각형 %.3f×0.66  %s"
              % (out, e * 100, (e - base) * 100, rect, "OK" if rect >= 1.5 * W60 else "✗"))
    print("\n  [L2] 마스크 컵 세로 이동 (현행 0)")
    for dy in (0.0, -0.10, -0.18):
        sh = eyes_variant(cup_dy=dy)
        e = expose(pack_order(eyes=sh), w)
        bot = min(p[1] for p in sh[1].pts)
        print("     dy=%+.2f  노출 %.1f%% (%+.1f%%p)  컵 밑단 %.2f (EYES 하한 −2.20)"
              % (dy, e * 100, (e - base) * 100, bot))
    print("\n  [L3] HAIR 두피 안쪽 반경 (현행 0.58, 상한 %.4f)" % (1 - W75))
    for inn in (0.58, 0.61, 0.64):
        e = expose(pack_order(hairsh=hair_variant(inn)), w)
        print("     inner=%.2f 노출 %.1f%% (%+.1f%%p)  규칙4 %s"
              % (inn, e * 100, (e - base) * 100, "OK" if inn <= 1 - W75 else "✗"))
    print("\n  [L4] 관 밑변 (현행 0.72)")
    for b in (0.72, 0.80, 0.86):
        sh = head_variant(b)
        e = expose(pack_order(head=sh), w)
        hgt = 1.54 - b
        print("     bot=%.2f  노출 %.1f%% (%+.1f%%p)  관 세로 %.3f  %s"
              % (b, e * 100, (e - base) * 100, hgt, "OK" if hgt >= 1.5 * W60 else "✗ 잉크사각형 미달"))


# ═══════════════════════════════════════════════════════════════════════════
# ═══════════════════════════════════════════════════════════════════════════
# ★ §6 — §5 의 네 레버는 전부 1%p 안쪽이었다. 그래서 **다른 레버**로 갔다:
#   마스크 컵을 얼굴에서 떼어 가슴께로 내린다(r5_rx.MASK_DROP = −0.78 R).
#   §4 가 그 이유를 이미 말하고 있었다 — 6/6 에서 머리를 덮는 것의 대부분이 EYES 이고,
#   그중 렌즈 띠는 머리카락 돔이 어차피 덮는 자리라 줄여도 노출이 안 오른다.
#   **머리카락이 안 닿는 곳을 덮고 있던 것은 마스크 컵 하나였다.**
# ═══════════════════════════════════════════════════════════════════════════
def s6_final():
    import random, r5_rx
    print("\n╔══ §6 처방 — 마스크 컵을 %+.2f R 내린다 (바꾸는 수는 이것 하나) ══╗" % r5_rx.MASK_DROP)
    HN, EN, NN, BN, RN = list(items.HEAD), list(items.EYES), list(items.NECK), list(items.BACK), list(hair.SET)
    rnd = random.Random(20260902)
    combos = [(rnd.choice(HN), rnd.choice(EN), rnd.choice(NN), rnd.choice(BN), rnd.choice(RN)) for _ in range(24)]
    for w, nm in ((W75, "0.75"), (W60, "0.60")):
        b = expose(pack_order(), w)
        e = expose(pack_order(eyes=r5_rx.eyes_respirator_v2()), w)
        exps = sorted(expose([(items.BACK[bn], INF), (hair.SET[rn], items.COVER[hn]),
                              (items.NECK[nn], INF), (items.EYES[en], INF), (items.HEAD[hn], INF)], w)
                      for hn, en, nn, bn, rn in combos)
        med = exps[12]
        rank = sum(1 for x in exps if x < e)
        v = e >= med
        (print if v else bad)("  @%s  머리 노출 %.1f%% -> **%.1f%%**  (기본 24조합 중앙 %.1f%% · 대역 %.1f~%.1f%% · %d/24 분위)  %s"
                              % (nm, b * 100, e * 100, med * 100, exps[0] * 100, exps[-1] * 100, rank,
                                 "★ 넘었다" if v else "여전히 낮다"))


if __name__ == "__main__":
    calib()
    gmax_p, gmax_c = s1_arith()
    s2_spans(gmax_p, gmax_c)
    s3_upper()
    s4_attrib()
    s5_sweep()
    s6_final()
    print("\n╚══ 위반 %d건 ══╝" % len(FAIL))
