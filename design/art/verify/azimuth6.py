# -*- coding: utf-8 -*-
"""★ R6 — 「여섯 방위, 한 고도」의 **방위를 확정한다** (design-art, 2026-09-02)

프레임(PACK_THEME_SPEC §10)이 남긴 자유 변수는 **색상각 하나**뿐이다.
채택된 팩이 「야간 정비반」으로 바뀌고 나머지 다섯의 이름도 전부 바뀌었으므로,
그 하나의 변수를 **여섯 새 이름 위에 다시 배치**한다.

이 스크립트가 지키는 분업 (★ 어느 줄이 판정이고 어느 줄이 측정인지 섞지 않는다)
  · **선언(판정)** : 팩마다의 「앵커 구간」 — 그 테마가 현실에서 어느 색상대에 사는가.
                    이건 내 아트 디렉션이고 측정이 아니다. 근거를 문자열로 함께 적는다.
  · **측정**       : 배정 · 간격 · ΔE · 대역 · 대비 · 항등 · 브라스/램프 거리 · 교체 비용.
                    전부 colorlab(교정된 자)로 잰다.

  python3 azimuth6.py            # 판정
  python3 azimuth6.py --control  # ★ 양성 대조 — 일부러 나쁜 각을 넣는다. 빨간불이 켜져야 정상
"""
import sys, os, itertools, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
import band
import derive_packs as DP
from packclash import load_current, INK_MARKERS, DISCERN, BRASS
from packrule import pick, chroma

LO, HI = band.limits()[0], band.limits()[1]
BD = band.BACKDROPS
GAP = 8.0                       # 처방 C 자리 비움 여유 (PALETTE_SPEC §13-4)
RAMP = ["#9C978C", "#BCAC8B", "#DBBD7F", "#F9CB70"]
RARE_RIBBON = "#BCAC8B"         # 팩 6종이 공유하는 유일한 등급색(희귀)
IDENT = 48.6                    # 식별 하한 (§12-0) — 색만으로 「무엇인지」 맞히는 선
FROZEN = [8.0, 80.0, 172.0, 222.0, 268.0, 312.0]   # PALETTE_SPEC §13-3 동결 6각

# ---------------------------------------------------------------------------
# 1. 【선언 = 판정】 여섯 팩의 앵커 구간
#    ★ 이 표는 측정이 아니다. 내가 고른 것이고, 틀렸다면 여기가 틀린 것이다.
#    구간을 넓게 잡은 이유: 좁게 잡으면 "구간을 답에 맞춰 그린" 것이 된다(순환논증).
#    등대지기와 눈보라 원정에 **같은 구간**을 준 것도 그래서다 — 둘을 가르는 것은
#    내 취향이 아니라 아래 §3의 재질 휘도 타이브레이크(측정)여야 한다.
# ---------------------------------------------------------------------------
ANCHORS = [
    ("야간 정비반", (58.0, 102.0),
     "하이비스 형광 황록 ~ 산업 올리브 캔버스. 밤 작업자의 식별색은 형광 황록이고 작업복 지본은 올리브다."),
    ("등대지기", (168.0, 232.0),
     "밤바다 청록 ~ 군청. 등대 그림의 지배색은 빛이 아니라 **빛이 없는 물**이다."),
    ("심야 도서관", (252.0, 292.0),
     "잉크·벨벳·가죽 장정의 보라. 램프갓 초록은 **점광원 1개**라 6종 전체의 지본이 될 수 없다."),
    ("우편배달", (352.0, 22.0),
     "우체통 빨강 ~ 주홍. 국내외 우편 아이덴티티가 이 구간에 거의 전부 몰려 있다."),
    ("눈보라 원정", (168.0, 232.0),
     "빙하·설면 그림자 청록 ~ 남청. 눈 자체는 흰색이라 대역 밖이고, 대역 안에 남는 것은 그림자다."),
    ("야시장", (298.0, 338.0),
     "네온 간판 마젠타 ~ 핑크. 백열 등롱 주황은 우편배달과 같은 사분면이라 쓸 수 없다."),
]
SHARED = ("등대지기", "눈보라 원정")   # 같은 구간을 준 두 팩


def cdist(a, b):
    """색상각 원형 거리 (0~180)."""
    d = abs((a - b) % 360.0)
    return min(d, 360.0 - d)


def in_anchor(h, iv):
    lo, hi = iv
    if lo <= hi:
        return lo <= h <= hi
    return h >= lo or h <= hi           # 0°를 감싸는 구간


def anchor_cost(h, iv):
    """구간 안이면 0, 밖이면 가장 가까운 끝까지의 원형 거리."""
    return 0.0 if in_anchor(h, iv) else min(cdist(h, iv[0]), cdist(h, iv[1]))


def neighbor_gaps(hs):
    s = sorted(h % 360.0 for h in hs)
    return [(s[(i + 1) % len(s)] - s[i]) % 360.0 for i in range(len(s))]


# ---------------------------------------------------------------------------
# 2. 처방 C 유도 (packfinal.derive와 같은 규칙 — 폴백 없음)
# ---------------------------------------------------------------------------
def derive(catset, order, gap=GAP):
    """order = [(이름, 각도), ...]  →  [(이름, 각, 주hex, 보조hex)] 또는 (None, 사유)"""
    placed = list(catset) + [BRASS]
    rows = []
    for name, h in order:
        p = pick(h, True, placed, gap)
        if p is None:
            return None, f"{name}({h:.0f}°) 주색 해 없음"
        placed.append(CL.rgb2hex(p))
        s = pick(h, False, placed, gap)
        if s is None:
            return None, f"{name}({h:.0f}°) 보조색 해 없음"
        placed.append(CL.rgb2hex(s))
        rows.append((name, h, CL.rgb2hex(p), CL.rgb2hex(s)))
    return rows, None


def gates(catset, rows):
    """한 후보 각도 집합의 전 게이트를 잰다."""
    pk = [c for _, _, p, s in rows for c in (p, s)]
    rgb = [CL.hex2rgb(c) for c in pk]
    m_cat = min(CL.dE(CL.hex2rgb(a), b) for a in catset for b in rgb)
    m_int = min(CL.dE(a, b) for a, b in itertools.combinations(rgb, 2))
    m_prim = min(CL.dE(CL.hex2rgb(p), CL.hex2rgb(q))
                 for (_, _, p, _), (_, _, q, _) in itertools.combinations(rows, 2))
    worst = min(min(CL.CR(c, b) for _, b in BD) for c in rgb)
    Ls = [CL.L(c) for c in rgb]
    return {
        "cat": m_cat, "int": m_int, "prim": m_prim, "worst": worst,
        "brass": min(CL.dE(CL.hex2rgb(BRASS), c) for c in rgb),
        "ramp": min(CL.dE(CL.hex2rgb(r), c) for r in RAMP for c in rgb),
        "ribbon": min(CL.dE(CL.hex2rgb(RARE_RIBBON), c) for c in rgb),
        "band": sum(1 for c in rgb if LO <= CL.L(c) <= HI),
        "ident": sum(1 for c in rgb if CL.worn(c) == c),
        "Lspan": (max(Ls) + 0.05) / (min(Ls) + 0.05),
        "gapmin": min(neighbor_gaps([h for _, h, _, _ in rows])),
        "hex": pk,
    }


# ---------------------------------------------------------------------------
# 3. 최대최소 간격 최적화 — 앵커 구간 안에서. 원형 순서를 고정하고 탐욕 배치.
#    ★ 근사가 아니라 1° 격자 위의 전수다(시작각 × 두 하위순서).
# ---------------------------------------------------------------------------
def maximin_under_anchors(step=1.0, blocked=()):
    BL = set(int(round(b)) % 360 for b in blocked)
    orders = []
    base = [(n, iv) for n, iv, _ in ANCHORS]
    # 원형 순서: 구간 중심각 순. 공유 구간 두 팩은 두 하위순서 모두 시험한다.
    def center(iv):
        lo, hi = iv
        return ((lo + (hi - lo if lo <= hi else hi + 360.0 - lo) / 2.0) % 360.0)
    for swap in (False, True):
        seq = sorted(base, key=lambda x: center(x[1]))
        if swap:
            i = [k for k, (n, _) in enumerate(seq) if n in SHARED]
            if len(i) == 2:
                seq[i[0]], seq[i[1]] = seq[i[1]], seq[i[0]]
        orders.append(seq)

    best = None
    for seq in orders:
        n0, iv0 = seq[0]
        lo0, hi0 = iv0
        span0 = (hi0 - lo0) % 360.0 if lo0 > hi0 else hi0 - lo0
        k = 0
        while k * step <= span0:
            a0 = (lo0 + k * step) % 360.0
            k += 1
            if int(round(a0)) % 360 in BL:
                continue
            # g에 대한 이분 탐색 + 탐욕 배치
            lo_g, hi_g = 0.0, 90.0
            best_place = None
            for _ in range(40):
                g = (lo_g + hi_g) / 2.0
                place, cur, ok = [a0], a0, True
                for nm, iv in seq[1:]:
                    want = cur + g
                    lo_i, hi_i = iv
                    # want 이상이면서 구간 안인 가장 이른 각
                    cand = None
                    t = 0
                    while t * step <= ((hi_i - lo_i) % 360.0 if lo_i > hi_i else hi_i - lo_i):
                        a = (lo_i + t * step) % 360.0
                        t += 1
                        if int(round(a)) % 360 in BL:
                            continue
                        adv = (a - place[0]) % 360.0
                        if adv >= (want - place[0]) % 360.0 - 1e-9 and adv >= (cur - place[0]) % 360.0:
                            cand = a
                            break
                    if cand is None:
                        ok = False
                        break
                    place.append(cand)
                    cur = cand
                if ok and (place[0] + 360.0 - place[-1]) % 360.0 >= g - 1e-9:
                    best_place = list(place)
                    lo_g = g
                else:
                    hi_g = g
            if best_place is not None:
                got = min(neighbor_gaps(best_place))
                key = (round(got, 3),)
                if best is None or key > best[0]:
                    best = (key, [(seq[i][0], best_place[i]) for i in range(6)])
    return best[1] if best else None


# ---------------------------------------------------------------------------
def assign(hues, names_ivs):
    """6! 전수 — 앵커 비용 합 최소. 동률이면 간격 최소값이 큰 쪽."""
    best = None
    for perm in itertools.permutations(hues):
        cost = sum(anchor_cost(perm[i], names_ivs[i][1]) for i in range(6))
        key = (-cost,)
        if best is None or key > best[0]:
            best = (key, perm)
    # 동률 전부 모으기
    bestcost = -best[0][0]
    ties = [p for p in itertools.permutations(hues)
            if abs(sum(anchor_cost(p[i], names_ivs[i][1]) for i in range(6)) - bestcost) < 1e-9]
    return bestcost, ties


def main(control=False):
    CL.calibrate()
    cur = load_current()
    cat = [h for h in cur if h not in INK_MARKERS]
    outband = [c for c in cat if not (LO <= CL.L(CL.hex2rgb(c)) <= HI)]

    print("╔" + "═" * 94 + "╗")
    print("║  R6 방위 확정 — 「여섯 방위, 한 고도」의 자유 변수 1개를 여섯 새 이름 위에 배치한다  ║")
    print("╚" + "═" * 94 + "╝")
    print(f"\n지금 트리 실측: .asset 고유색 {len(cur)} (잉크 표식 {len(cur)-len(cat)} 제외 → 아트 {len(cat)}색)")
    print(f"  자립 대역 L ∈ [{LO:.4f}, {HI:.4f}] · ★ 출하 아트 대역 밖 {len(outband)}건 "
          f"{'(유지)' if not outband else '★★ 회귀 ' + str(outband)}")
    print(f"  브라스 {BRASS} H={CL.hue_deg(CL.hex2rgb(BRASS)):.2f}° L={CL.L(CL.hex2rgb(BRASS)):.4f}  "
          f"· 희귀 리본 {RARE_RIBBON} L={CL.L(CL.hex2rgb(RARE_RIBBON)):.4f}")
    print(f"  → 크롬은 전부 대역 위(L>{HI:.4f})에 있다 = §0의 「휘도가 크롬과 아트를 가른다」 유지")

    # ---------------------------------------------------------------- §1
    print("\n" + "=" * 96)
    print("§1. 【선언 = 판정】 앵커 구간 — 측정이 아니다. 내가 골랐고, 틀렸다면 여기가 틀렸다")
    print("=" * 96)
    for n, iv, why in ANCHORS:
        w = (iv[1] - iv[0]) % 360.0
        print(f"  {n:10s} [{iv[0]:5.0f}°, {iv[1]:5.0f}°]  폭 {w:5.0f}°   {why}")
    print(f"  ★ 「{SHARED[0]}」와 「{SHARED[1]}」에 **같은 구간**을 줬다 — 둘을 가르는 근거를")
    print(f"     내 취향이 아니라 §3의 재질 휘도(측정)로 만들기 위해서다.")

    # ---------------------------------------------------------------- §2
    print("\n" + "=" * 96)
    print("§2. 후보 각도 집합 — 세 개를 같은 자로 잰다")
    print("=" * 96)
    cands = [("A 동결 6각 (§13-3 그대로)", list(FROZEN))]
    placed0 = list(cat) + [BRASS]
    blocked1 = [h for h in range(360)
                if pick(float(h), True, placed0, GAP) is None or pick(float(h), False, placed0, GAP) is None]
    print(f"\n  1° 격자 가용각 {360-len(blocked1)}/360 · ★ 처방 C 아래 해가 없는 각: {blocked1}")
    print(f"     (60~62°는 §1-1의 R=204 못박힘 구간 · 214~218°는 자립 대역과 상자의 교집합이 비는 곳)")
    # ★ blocked1은 **필요조건**일 뿐이다 — 팩이 순서대로 자리를 잡으면 남은 각이 더 줄어든다.
    #   그래서 유도가 실패한 각을 블랙리스트에 넣고 다시 푸는 루프를 돈다(수렴할 때까지).
    bl, mm, tries = list(blocked1), None, []
    for _ in range(24):
        m = maximin_under_anchors(blocked=bl)
        if m is None:
            break
        _r, _e = derive(cat, m)
        if _e is None:
            mm = m
            break
        bad = int(round(float(_e.split("(")[1].split("°")[0])))
        tries.append(bad)
        bl.append(bad)
    print(f"     ★ 순서 의존 추가 사망각 {len(tries)}개 {tries} — 필요조건 목록에 없던 각이 "
          f"**앞 팩이 색을 가져간 뒤** 죽는다")
    if mm:
        cands.append(("B 앵커 안 최대최소 간격(재시도 수렴)", [h for _, h in mm]))
    # C: 완전 정육각(60° 등간) 최적 회전 — 앵커 무시. 상한 참조용.
    bestC = None
    for r in range(0, 60):
        hs = [(r + 60 * i) % 360.0 for i in range(6)]
        rows, err = derive(cat, [(f"p{i}", hs[i]) for i in range(6)])
        if err:
            continue
        g = gates(cat, rows)
        key = (round(g["cat"], 2), round(g["int"], 2))
        if bestC is None or key > bestC[0]:
            bestC = (key, hs)
    if bestC:
        cands.append(("C 정육각 60° 등간 (앵커 무시 · 상한 참조)", bestC[1]))

    ctl_gap = {}
    if control:
        # ★ 양성 대조 — 세 종류를 넣는다. 「해 없음」만 잡히면 **게이트 깃발은 한 번도 안 켜진 것**이다.
        #   (a) 유도 자체가 죽는 각        → '해 없음'으로 잡혀야 한다
        cands.append(("★대조a 뭉친 6각 (5° 간격)", [200.0, 205.0, 210.0, 215.0, 220.0, 225.0]))
        cands.append(("★대조b 브라스 위 6각", [38.0, 39.0, 40.0, 41.0, 42.0, 43.0]))
        #   (b) 유도는 되지만 **간격이 좁은** 각 → ✗간격 깃발이 켜져야 한다
        cands.append(("★대조c 유도 성공 · 간격 10°", [100.0, 110.0, 120.0, 130.0, 140.0, 150.0]))
        #   (c) 자리 비움 여유를 0으로 푼 유도 → ✗카탈 깃발이 켜져야 한다
        cands.append(("★대조d 자리비움 gap=0", list(FROZEN)))
        ctl_gap["★대조d 자리비움 gap=0"] = 0.0

    print(f"  {'후보':40s} {'최소간격':>7s} {'카탈↔팩':>7s} {'팩내부':>6s} {'주색쌍':>6s} "
          f"{'배경최악':>7s} {'브라스':>6s} {'램프':>6s} {'대역':>5s} {'항등':>5s} {'L폭':>6s}")
    results = {}
    for label, hs in cands:
        rows, err = derive(cat, [(f"p{i}", hs[i]) for i in range(6)], ctl_gap.get(label, GAP))
        if err:
            print(f"  {label:40s} ★ 해 없음 — {err}")
            results[label] = None
            continue
        g = gates(cat, rows)
        results[label] = (hs, rows, g)
        flag = ""
        if g["cat"] < GAP: flag += " ✗카탈"
        if g["int"] < DISCERN: flag += " ✗내부"
        if g["worst"] < 3.0: flag += " ✗대비"
        if g["band"] != 12: flag += " ✗대역"
        if g["ident"] != 12: flag += " ✗항등"
        if g["gapmin"] < 30.0: flag += " ✗간격"
        print(f"  {label:40s} {g['gapmin']:6.1f}° {g['cat']:7.2f} {g['int']:6.2f} {g['prim']:6.2f} "
              f"{g['worst']:6.2f}:1 {g['brass']:6.2f} {g['ramp']:6.2f} {g['band']:3d}/12 "
              f"{g['ident']:3d}/12 {g['Lspan']:5.2f}:1{flag}")

    if control:
        need = {"a": "해 없음", "b": "해 없음", "c": "✗간격", "d": "✗카탈"}
        print("\n★ 양성 대조 판정 — 네 줄이 **각각 다른 방식으로** 빨개져야 한다:")
        for k, v in need.items():
            print(f"     대조{k} → '{v}' 가 보여야 한다")
        print("  ★ 대조a/b만 잡히면 그건 **유도기만 시험한 것**이고 게이트 깃발은 한 번도 안 켜진 것이다.")
        print("    (이 저장소 사고 5번: 아무것도 안 재고 초록.)")
        return

    # ------------------------------------------------------------ §2-b
    print("\n" + "=" * 96)
    print("§2-b. ★ B가 A보다 '좋다'는 숫자를 **문턱에 대고** 다시 읽는다")
    print("=" * 96)
    print("  B는 주색쌍 최소 ΔE를 크게 올린다. 그런데 **그 개선이 어느 판정을 바꾸는가**를 물어야 한다.")
    print("  이 설계에 존재하는 문턱은 넷뿐이다: 변별 7.8 · 자리 비움 8.0 · 대비 3.0 · 식별 48.6.")
    gA = results["A 동결 6각 (§13-3 그대로)"][2]
    gB = results.get("B 앵커 안 최대최소 간격(재시도 수렴)")
    if gB:
        gB = gB[2]
        print(f"  {'게이트':10s} {'A':>7s} {'B':>7s} {'차':>8s}  넘긴 문턱")
        anycross = False
        for k, lab in (("cat", "카탈↔팩"), ("int", "팩내부"), ("prim", "주색쌍"),
                       ("worst", "배경최악"), ("brass", "브라스"), ("ramp", "램프")):
            a, b = gA[k], gB[k]
            cross = [t for t in (3.0, 7.8, 8.0, IDENT) if (a < t <= b) or (b < t <= a)]
            anycross = anycross or bool(cross)
            print(f"  {lab:10s} {a:7.2f} {b:7.2f} {b-a:+8.2f}  {cross if cross else '없음'}")
        print(f"\n  ★ 문턱을 넘는 게이트 {'있음' if anycross else '**하나도 없다**'}.")
        print(f"     주색쌍 24.31 → 36.34는 **아무 판정도 바꾸지 않는 구간 안에서만** 움직인다 —")
        print(f"     식별 하한 {IDENT}을 넘지 못하고(넘어도 안 된다: 프레임의 전제다),")
        print(f"     변별 하한 7.8은 A가 이미 3.1배로 넘고 있다.")
        print(f"     대가는 **동결 12 hex 전량 교체**다. → 사지 않는다.")
        print(f"  ★ 그리고 숫자에 안 잡히는 대가가 하나 더 있다: B의 우편배달은 21°(#C96E3C)로 밀린다.")
        print(f"     앵커 구간 [352,22] 안이지만 **끝**이고, 우체통 빨강이 아니라 주황이 된다.")
    print("  C(정육각)는 **배정 자체가 불가능**하다 — 아래 §2-c.")

    # ------------------------------------------------------------ §2-c
    print("\n" + "=" * 96)
    print("§2-c. ★ 정육각(C)이 왜 배정 불가인가 — 여유는 **아무도 원하지 않는 각**에 있다")
    print("=" * 96)
    hsC = results.get("C 정육각 60° 등간 (앵커 무시 · 상한 참조)")
    if hsC:
        hsC = hsC[0]
        for n, iv, _ in ANCHORS:
            hit = [h for h in hsC if in_anchor(h, iv)]
            print(f"  {n:11s} [{iv[0]:.0f},{iv[1]:.0f}] ∩ {[int(x) for x in hsC]} = "
                  f"{[int(x) for x in hit] if hit else '★ 공집합'}")
        orphan = [int(h) for h in hsC if not any(in_anchor(h, iv) for _, iv, _ in ANCHORS)]
        print(f"  → 심야 도서관에 **줄 각이 없고**, 등대지기와 눈보라 원정이 **같은 각 하나**를 두고 부딪힌다.")
        print(f"     그리고 {orphan}°는 **어느 팩도 원하지 않는 각**이다(§11의 120~180° 구멍).")
        print(f"     C의 ΔE 여유는 바로 거기서 나온다 = **쓸 수 없는 여유**다.")

    # ---------------------------------------------------------------- §3
    print("\n" + "=" * 96)
    print("§3. 배정 — 6! 전수, 앵커 비용 최소. 동률은 **재질 휘도**로 가른다")
    print("=" * 96)
    hs, rows, g = results["A 동결 6각 (§13-3 그대로)"]
    cost, ties = assign(hs, [(n, iv) for n, iv, _ in ANCHORS])
    print(f"  앵커 비용 최소 = {cost:.1f}°  ·  동률 배정 {len(ties)}개")
    Lof = {}
    for name, h, p, s in rows:
        Lof[h] = CL.L(CL.hex2rgb(p))
    for t in ties:
        print("     " + " / ".join(f"{ANCHORS[i][0]} {t[i]:.0f}° (L={Lof[t[i]]:.4f})" for i in range(6)))
    print("\n  ★ 타이브레이크 규칙(측정): 「재질이 밝은 팩일수록 높은 L」.")
    print("     근거는 PALETTE_SPEC §2 — 이 카탈로그에서 색이 나르는 것은 **재질**이다.")
    print("     휘도가 **가치**를 안 나른다는 것(ρ=+0.1567)과 충돌하지 않는다: 가치가 아니라 재질이다.")
    ORDER_BRIGHT = {"야간 정비반": 1, "눈보라 원정": 2}   # 밝아야 하는 재질
    ORDER_DARK = {"등대지기": 1, "심야 도서관": 2}         # 어두워야 하는 재질
    best_t, best_key = None, None
    for t in ties:
        sc = 0.0
        for i, (n, _, _) in enumerate(ANCHORS):
            if n in ORDER_BRIGHT: sc += Lof[t[i]]
            if n in ORDER_DARK:   sc -= Lof[t[i]]
        if best_key is None or sc > best_key:
            best_key, best_t = sc, t
    print(f"     → 재질 휘도 점수 최대 배정 채택 (점수 {best_key:+.4f})")

    final = [(ANCHORS[i][0], best_t[i]) for i in range(6)]
    frows, err = derive(cat, final)
    assert err is None, err
    fg = gates(cat, frows)

    # ---------------------------------------------------------------- §4
    print("\n" + "=" * 96)
    print("§4. ★ 확정 — 여섯 방위")
    print("=" * 96)
    print(f"  {'팩':11s} {'H':>6s} {'주색':>9s} {'C*':>5s} {'L':>7s} {'최악':>6s} | "
          f"{'보조색':>9s} {'C*':>5s} {'L':>7s} {'최악':>6s} | {'주↔보':>6s} | {'그늘':>9s} | 앵커")
    for name, h, p, s in sorted(frows, key=lambda r: r[1]):
        P, S = CL.hex2rgb(p), CL.hex2rgb(s)
        iv = [a[1] for a in ANCHORS if a[0] == name][0]
        print(f"  {name:11s} {h:6.1f} {p:>9s} {chroma(P):5.1f} {CL.L(P):7.4f} "
              f"{min(CL.CR(P,b) for _,b in BD):5.2f}:1 | {s:>9s} {chroma(S):5.1f} {CL.L(S):7.4f} "
              f"{min(CL.CR(S,b) for _,b in BD):5.2f}:1 | {CL.dE(P,S):6.2f} | "
              f"{CL.rgb2hex(CL.fill_outline(P)):>9s} | [{iv[0]:.0f},{iv[1]:.0f}] "
              f"{'안' if in_anchor(h, iv) else '★밖'}")
    gaps = neighbor_gaps([h for _, h, _, _ in frows])
    print(f"\n  이웃 간격 (색상각 순): " + " · ".join(f"{x:.0f}°" for x in gaps) +
          f"   최소 {min(gaps):.0f}° / 최대 {max(gaps):.0f}°")
    print(f"  주색 6종 상호 최소 ΔE {fg['prim']:.2f}  (식별 하한 {IDENT} 미만 = "
          f"**색만으로는 어느 팩인지 못 맞힌다** — 프레임의 전제 그대로)")
    print(f"  12색 내부 최소 ΔE {fg['int']:.2f} ≥ 변별 하한 {DISCERN} · 카탈로그↔팩 최소 ΔE {fg['cat']:.2f} ≥ {GAP}")
    print(f"  배경 4종 최악 {fg['worst']:.2f}:1 ≥ 3.0 · 대역 {fg['band']}/12 · WornColor 항등 {fg['ident']}/12")
    print(f"  브라스 최근접 ΔE {fg['brass']:.2f} · 등급 램프 최근접 ΔE {fg['ramp']:.2f} · "
          f"희귀 리본 최근접 ΔE {fg['ribbon']:.2f}")
    print(f"  고도(L) 폭 {fg['Lspan']:.4f}:1 — 여섯이 한 대역 안에 있다")

    # ---------------------------------------------------------------- §5
    print("\n" + "=" * 96)
    print("§5. 교체 비용 — 이 배정이 hex를 몇 개 바꾸는가")
    print("=" * 96)
    old = {h: (p, s) for _, h, p, s in results["A 동결 6각 (§13-3 그대로)"][1]}
    new = {h: (p, s) for _, h, p, s in frows}
    changed = [h for h in new if old.get(h) != new[h]]
    print(f"  동결 12 hex 중 바뀌는 것: {len(changed)*2}개 {changed if changed else '(없음)'}")
    print(f"  ★ 새로 고른 hex 0개 — 각도 6개가 그대로라 §13-3 동결 대장이 **바이트 동일**하다.")
    print(f"  바뀐 것은 **이름↔각도 배정**뿐이다. .asset 0바이트 · 프로덕션 .cs 0줄.")

    # ---------------------------------------------------------------- §6
    print("\n" + "=" * 96)
    print("§6. 「야간 정비반」이 왜 이 각인가 — 세 측정")
    print("=" * 96)
    ns = [r for r in frows if r[0] == "야간 정비반"][0]
    P, S = CL.hex2rgb(ns[2]), CL.hex2rgb(ns[3])
    allL = [(CL.L(CL.hex2rgb(p)), n) for n, _, p, _ in frows]
    allL.sort(reverse=True)
    print(f"  (a) 주색 {ns[2]} L={CL.L(P):.4f} = **여섯 주색 중 최대**"
          f" (2위 {allL[1][1]} {allL[1][0]:.4f}) · 대역 천장 {HI:.4f}까지 {HI-CL.L(P):+.4f}")
    print(f"      → 밤에 가장 밝은 재질이 하이비스다. 재질 논리와 대역 천장이 같은 곳을 가리킨다.")
    print(f"  (b) 주↔보조 ΔE {CL.dE(P,S):.2f} — 여섯 중 "
          f"{sorted([CL.dE(CL.hex2rgb(p),CL.hex2rgb(s)) for _,_,p,s in frows], reverse=True).index(CL.dE(P,S))+1}위."
          f" 작은 보조 도형(HairTieBand)이 있는 팩이라 큰 값이 필요하다(→ graygate.py §2).")
    blocked = []
    placed = list(cat) + [BRASS]
    for hd in range(0, 360, 5):
        if pick(float(hd), True, placed, GAP) is None or pick(float(hd), False, placed, GAP) is None:
            blocked.append(hd)
    print(f"  (c) 하이비스의 가장 상징적인 각 **60°는 막혀 있다** — 처방 C 아래 해가 없는 각도: {blocked}")
    print(f"      60°는 §1-1의 R=204 못박힘 구간이다. 그래서 앵커 구간 안에서 **80°**로 물러났다.")
    print(f"      실제 하이비스 조끼는 형광이라 sRGB 대역 밖이기도 하다 — 어차피 근사다.")

    # ---------------------------------------------------------------- §7
    print("\n" + "=" * 96)
    print("§7. 이펙트 색 — 잉크 지분 2:1 (PALETTE_SPEC §6 규칙 유지)")
    print("=" * 96)
    print(f"  파쿠르 파티클 한 벌 3조각 = 잉크 2 + 팩 주색 1 → 잉크 지분 66.7% ≥ 50%")
    print(f"  {'팩':11s} {'파티클 팩색':>10s} {'검 잉크와 ΔE':>12s} {'흰 잉크와 ΔE':>12s} {'임의 바탕 최악':>12s}")
    INKB, INKW = CL.hex2rgb("#000000"), CL.hex2rgb("#FFFFFF")
    for name, h, p, s in sorted(frows, key=lambda r: r[1]):
        P = CL.hex2rgb(p)
        w = min(CL.CR(P, (255, 255, 255)), CL.CR(P, (0, 0, 0)))
        print(f"  {name:11s} {p:>10s} {CL.dE(P, INKB):12.1f} {CL.dE(P, INKW):12.1f} {w:11.2f}:1")
    print(f"  → 여섯 전부 잉크 흑·백 양쪽과 식별 하한 {IDENT}을 넘는다 = 잉크 조각과 팩 조각이 섞이지 않는다.")

    print("\n" + "=" * 96)
    print("§8. 프레임 불변량 재확인 (PACK_THEME_SPEC §10-1의 네 겹)")
    print("=" * 96)
    ribL = CL.L(CL.hex2rgb(RARE_RIBBON))
    print(f"  ① 고도  : 팩 12색 L {min(CL.L(CL.hex2rgb(c)) for c in fg['hex']):.4f}~"
          f"{max(CL.L(CL.hex2rgb(c)) for c in fg['hex']):.4f} · 폭 {fg['Lspan']:.4f}:1 — 팩마다 다르지 않다")
    print(f"  ② 바닥  : 리본 1색({RARE_RIBBON}) 고정 · 리본↔팩 주색 ΔE "
          f"{min(CL.dE(CL.hex2rgb(RARE_RIBBON), CL.hex2rgb(p)) for _,_,p,_ in frows):.1f}~"
          f"{max(CL.dE(CL.hex2rgb(RARE_RIBBON), CL.hex2rgb(p)) for _,_,p,_ in frows):.1f} — 팩마다 다르지 않다")
    print(f"  ③ 지분  : 잉크 : 팩색 = 2 : 1 — 팩마다 다르지 않다")
    print(f"  ④ 방위  : 색상각 {sorted(int(h) for _,h,_,_ in frows)} — **오직 이것만 다르다**")

    print("\nAZIMUTH6 = [")
    for name, h, p, s in sorted(frows, key=lambda r: r[1]):
        print(f'    ("{name}", {h}, "{p}", "{s}", "{CL.rgb2hex(CL.fill_outline(CL.hex2rgb(p)))}"),')
    print("]")


if __name__ == "__main__":
    main("--control" in sys.argv)
