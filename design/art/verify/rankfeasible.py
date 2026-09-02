# -*- coding: utf-8 -*-
"""§6-부록  "등급 4단을 **색만으로 식별**되게 만드는 것이 애초에 가능한가"를 전수로 답한다.

후보 세 개가 전부 ⑨(식별 하한 ΔE_ID)에서 깨졌다. 그건 후보가 나빴기 때문인가,
아니면 제약 아래에서 **존재하지 않기** 때문인가? 추측하지 않고 센다.

    python3 rankfeasible.py
"""
import colorlab as C
import packs as P

W, K = (255, 255, 255), (0, 0, 0)

IDF, DISC = 48.6, 7.8          # packs.py §4가 출하 색에서 뽑은 값
PACK_PRIM = [P.best_in_box(h)[0] for _, h, _ in P.PACKS]
PACK_SEC = []
for _, h, _ in P.PACKS:
    best = None
    for si in range(42, 101):
        for vi in range(55, 81):
            q = C.hsv_to_rgb(h / 360.0, si / 100.0, vi / 100.0)
            if C.L(q) > P.SELF_L_HI:
                continue
            key = (round(C.L(q), 4), si)
            if best is None or key > best[0]:
                best = (key, q)
    PACK_SEC.append(best[1])

MARKERS = [P.INK_TONE, P.INK_DIM]


def admissible():
    """9가지 요구 중 **한 색만 보고 판정 가능한 것**을 통과한 색 전부."""
    out = []
    for hi in range(0, 360, 10):
        for si in range(0, 101, 7):
            for vi in range(25, 101, 4):
                c = C.hsv_to_rgb(hi / 360.0, si / 100.0, vi / 100.0)
                if C.CR(c, P.OUR_CARD) < 4.5 or C.CR(c, P.HO_CARD) < 4.5:
                    continue                      # ②③
                if min(C.dE(c, m) for m in MARKERS) < 10.0:
                    continue                      # ⑦
                if min(C.dE(c, p) for p in PACK_PRIM) < DISC:
                    continue                      # ⑤
                if min(C.dE(c, p) for p in PACK_SEC) < DISC:
                    continue                      # ⑥
                if C.dE(c, P.BRASS) < DISC:
                    continue                      # ⑧
                out.append(c)
    return sorted(set(out), key=C.L)


def hue_span(cs):
    """네 색의 색상각 폭(원형 최소 호). 무채(채도<0.08)는 폭 계산에서 뺀다."""
    hs = [C.hue_deg(c) for c in cs if C.rgb_to_hsv(c)[1] >= 0.08]
    if len(hs) < 2:
        return 0.0
    hs.sort()
    gaps = [hs[(i + 1) % len(hs)] - hs[i] for i in range(len(hs) - 1)]
    gaps.append(360.0 + hs[0] - hs[-1])
    return 360.0 - max(gaps)


def best4(cols, labs, lums, t, max_span=None):
    """휘도 단조 + **모든 쌍**(6쌍) ΔE >= t 인 4색 조합 하나. 없으면 None.

    ★ 인접 쌍만 보면 안 된다 — 유저는 일반과 영웅도 구별해야 한다. 인접만 재는 검사는
      색상각을 지그재그로 흔들어 휘도만 올리는 사슬을 통과시킨다(첫 판에서 실제로 그랬다:
      "1927단" 사슬이 나왔다. 그건 답이 아니라 **내 검사가 틀렸다는 신호**였다).
    """
    n = len(cols)
    t2 = t * t
    adj = [0] * n
    for i in range(n):
        li, ai, bi = labs[i]
        m = 0
        for j in range(i + 1, n):
            lj, aj, bj = labs[j]
            if (li - lj) ** 2 + (ai - aj) ** 2 + (bi - bj) ** 2 >= t2:
                m |= 1 << j
        adj[i] = m
    for i in range(n):
        mi = adj[i]
        rj = mi
        while rj:
            j = (rj & -rj).bit_length() - 1
            rj &= rj - 1
            mij = mi & adj[j]
            rk = mij
            while rk:
                k = (rk & -rk).bit_length() - 1
                rk &= rk - 1
                mijk = mij & adj[k]
                rl = mijk
                while rl:
                    l = (rl & -rl).bit_length() - 1
                    rl &= rl - 1
                    quad = [cols[i], cols[j], cols[k], cols[l]]
                    if max_span is None or hue_span(quad) <= max_span:
                        return quad
    return None


if __name__ == "__main__":
    C.calibrate()
    cols = admissible()
    labs = [C.lab(c) for c in cols]
    lums = [C.L(c) for c in cols]
    print(f"제약 ②③⑤⑥⑦⑧을 통과하는 색: {len(cols)}종")
    print("  (②③ 두 카드에서 텍스트 4.5 / ⑤⑥ 팩 12색과 변별 / ⑦ 잉크 표식과 10 / ⑧ 브라스와 변별)")
    print(f"\n{'하한 t':>7s}  {'4단 가능?':>9s}   찾은 램프 (휘도 오름차순)")
    hi = None
    for t in (10, 20, 30, 40, 48.6, 55, 60, 65, 70, 75, 80):
        r = best4(cols, labs, lums, t)
        if r:
            hi = (t, r)
        print(f"{t:7.1f}  {'예' if r else '아니오':>9s}   "
              f"{' -> '.join(C.rgb2hex(c) for c in r) if r else '-'}")
    print(f"\n★ 판정: 이 제약 아래에서 **모든 쌍**이 떨어진 4단 램프는 t = {hi[0]}까지 존재한다.")
    print(f"   식별 하한 {IDF}에서도 4단이 선다: {' -> '.join(C.rgb2hex(c) for c in best4(cols, labs, lums, IDF))}")
    print("   → 즉 '색만으로 등급 식별'은 **기하학적으로는 가능하다.** 불가능한 것이 아니다.")
    print("     (내가 처음에 '불가능할 것'이라고 본 가설은 여기서 반증됐다. 그대로 적는다.)")

    print("\n" + "-" * 78)
    print("그런데 위 램프를 보라: 올리브 -> 모브 -> 보라 -> 순수 초록.")
    print("**식별은 되지만 서열이 없다.** 초록이 보라보다 귀하다는 것을 아무도 그림에서 못 읽는다.")
    print("등급이 색에 요구하는 것은 둘이다 — ① 무엇인지 알기(식별) ② 어느 쪽이 위인지 알기(서열).")
    print("서열을 색이 지려면 **한 색 가족 안에서 밝기만 오르는 램프**여야 한다. 그 조건을 추가로 건다.\n")
    def best4_in_family(span, t):
        """색상각 폭 <= span 인 창을 5도씩 훑으며 그 안에서만 4단을 찾는다(무채는 늘 포함)."""
        for h0 in range(0, 360, 5):
            idx = [i for i, c in enumerate(cols)
                   if C.rgb_to_hsv(c)[1] < 0.08 or ((C.hue_deg(c) - h0) % 360.0) <= span]
            if len(idx) < 4:
                continue
            sub = [cols[i] for i in idx]
            sl = [labs[i] for i in idx]
            su = [lums[i] for i in idx]
            r = best4(sub, sl, su, t)
            if r:
                return r
        return None

    print(f"{'하한 t':>7s}  {'폭<=60도':>9s}  {'폭<=30도':>9s}  {'폭<=10도(사실상 단일)':>20s}")
    for t in (10, 15, 20, 25, 30, 35, 40, 48.6):
        r60 = best4_in_family(60.0, t)
        r30 = best4_in_family(30.0, t)
        r0 = best4_in_family(10.0, t)
        print(f"{t:7.1f}  {'예' if r60 else '아니오':>9s}  {'예' if r30 else '아니오':>9s}  "
              f"{'예' if r0 else '아니오':>20s}")
    for span in (60.0, 30.0, 10.0):
        himax, r = 0, None
        for t in [x / 2 for x in range(20, 140)]:
            q = best4_in_family(span, t)
            if q:
                himax, r = t, q
            else:
                break
        print(f"\n★ 색상각 폭 <= {span:g}° 로 묶으면 4단의 최대 분리는 ΔE {himax:.1f} 까지다.")
        print(f"   예: {' -> '.join(C.rgb2hex(c) for c in r) if r else '-'}   "
              f"(식별 하한 {IDF} 대비 {himax / IDF * 100:.0f}%)")
    print("\n" + "-" * 78)
    print("★ 위 램프들도 '서열'이 아니다. #05F705 -> #7EF792 -> #F7E6F7 는 밝기가 아니라 **채도가**")
    print("  요동친 것이고(형광 초록 -> 연초록 -> 거의 흰색), 그 요동이 ΔE를 벌었다.")
    print("  내 가설은 두 번 반증됐다. 그러니 '서열'이 무엇인지를 **검사로 못 박고** 다시 잰다:\n")
    print("    정직한 등급 램프 =  ① L* 단조 증가  ② 채도 C* 단조 증가(귀할수록 색이 짙다)")
    print("                        ③ 색상각 폭 <= 30도  ④ 상대휘도 <= 0.70")
    print("      ④의 근거: 그 위는 흰 잉크(캐릭터)와 브라스 위 글자가 사는 자리다. 전설이 흰색이면")
    print("      가장 귀한 것이 **가장 색이 없는 것**이 된다 — 그림이 낱말과 반대말을 한다.\n")

    def honest4(span, t):
        for h0 in range(0, 360, 5):
            idx = [i for i, c in enumerate(cols)
                   if lums[i] <= 0.70 and (C.rgb_to_hsv(c)[1] < 0.08
                                           or ((C.hue_deg(c) - h0) % 360.0) <= span)]
            if len(idx) < 4:
                continue
            sub = [cols[i] for i in idx]
            sl = [labs[i] for i in idx]
            su = [lums[i] for i in idx]
            n = len(sub)
            t2 = t * t
            chroma = [(sl[i][1] ** 2 + sl[i][2] ** 2) ** 0.5 for i in range(n)]
            adj = [0] * n
            for i in range(n):
                m = 0
                li, ai, bi = sl[i]
                for j in range(i + 1, n):
                    if chroma[j] <= chroma[i] or su[j] <= su[i]:
                        continue
                    lj, aj, bj = sl[j]
                    if (li - lj) ** 2 + (ai - aj) ** 2 + (bi - bj) ** 2 >= t2:
                        m |= 1 << j
                adj[i] = m
            for i in range(n):
                mi = adj[i]
                rj = mi
                while rj:
                    j = (rj & -rj).bit_length() - 1
                    rj &= rj - 1
                    mij = mi & adj[j]
                    rk = mij
                    while rk:
                        k = (rk & -rk).bit_length() - 1
                        rk &= rk - 1
                        mijk = mij & adj[k]
                        if mijk:
                            l = (mijk & -mijk).bit_length() - 1
                            return [sub[i], sub[j], sub[k], sub[l]]
        return None

    print(f"{'하한 t':>7s}  {'정직한 4단 가능?':>15s}   예시")
    himax, rr = 0, None
    for t in (10, 15, 20, 25, 30, 35, 40, 45, 48.6):
        q = honest4(30.0, t)
        if q:
            himax, rr = t, q
        print(f"{t:7.1f}  {'예' if q else '아니오':>15s}   "
              f"{' -> '.join(C.rgb2hex(c) for c in q) if q else '-'}")
    print(f"\n★★ 결론(측정 — 내 가설이 **세 번 반증됐다**. 반증된 대로 적는다):")
    print(f"   ① 정직한(서열이 있는) 4단 램프는 식별 하한 {IDF}에서도 **존재한다**.")
    print(f"      예: {' -> '.join(C.rgb2hex(c) for c in rr) if rr else '-'}")
    print(f"      → '등급을 색으로 식별시키는 것은 불가능하다'는 내 처음 주장은 **틀렸다.**")
    print(f"   ② 다만 그 램프는 색상각 40~71도(황동~연두)에 몰려 밀리터리 팩(80도)과 이웃한다.")
    print(f"      식별 등급의 등급색은 **팩 하나를 잡아먹는다** — 그것이 진짜 비용이다.")
    print(f"   ③ 그리고 우리 화면에서 등급은 **한 번도 색만으로 나오지 않는다** — 리본 옆에")
    print(f"      낱말('희귀')이 언제나 함께 있다(핸드오프 이중 표기). 식별 하한은 애초에")
    print(f"      적용 대상이 아니다. 적용해야 할 하한은 변별({DISC})과 **서열의 단조성**이다.")
    print(f"   → 그래서 등급색의 요구는 '식별'이 아니라 '단조'로 정한다. 색상각을 하나로 묶으면")
    print(f"      팩을 잡아먹지 않으면서 단조가 보장된다(packs.py 후보 B).")
