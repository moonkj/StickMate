# -*- coding: utf-8 -*-
"""§27-10-1 재현 — 등급 램프의 **회색조 구멍**을 두 색으로 닫는다 (design-art, 2026-09-03)

PALETTE_SPEC §12-1 실측: 브라스 램프는 이색각 3유형에서는 안 무너지는데
**완전색맹/회색조에서 6쌍 중 2쌍이 변별 하한 7.8 아래**로 내려간다(최악 6.14).
이 스크립트는 그 구멍이 **같은 색상각 안에서 닫히는가**를 묻고, 닫는 값을 유도한다.

★ 이 파일이 왜 따로 있는가 (2026-09-03 자백):
  §27-10-1의 값 #DEC081 / #FFD375 를 처음에는 **일회성 heredoc**으로 뽑고 문서에만 적었다.
  r9exec.py 는 그 값을 **입력으로 하드코딩**해 쓰고 있어서, 유도는 어디에도 재현 가능한 형태로
  남지 않았다. 이 저장소가 반복해 당한 형태(기준과 대상이 갈라진다)라 스크립트로 옮긴다.

★ 그리고 이 라운드에서 **전수 탐색 실행 하나가 죽었다**(백그라운드 강제 종료, 출력 10바이트).
  죽은 실행과 성공한 실행의 출력은 겉으로 구별되지 않는다(TEAM.md §4-2 사고 2).
  그래서 이 스크립트는 **예산을 명시하고, 예산을 다 쓰면 「미완」이라고 크게 찍는다.**
  결론 줄은 탐색이 완주했을 때만 나온다.

    python3 rampfix.py
    python3 rampfix.py --control
"""
import sys, os, math, itertools

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
import cvd

D = 7.8                       # 변별 하한 (PALETTE_SPEC 3-3)
TEXT = 4.5
CARD = "#1B1F26"              # UiChrome.CardSurface
INKD = "#8B939F"              # ItemCatalog.InkDimTone  — 표식색, 붙으면 지뢰
INKT = "#D6DBE3"              # ItemCatalog.InkTone
BRASS = "#C8A15A"
PACK_C = ["#456ECC", "#6080CC", "#009682", "#518C84", "#CC1BA9", "#9C5A8E",
          "#CC3F29", "#9E655C", "#9768CC", "#8563AB", "#639400", "#798C51"]
PKC = [CL.hex2rgb(x) for x in PACK_C]
CUR = [("일반", "#9C978C"), ("희귀", "#BCAC8B"), ("영웅", "#DBBD7F"), ("전설", "#F9CB70")]
HUE_LO, HUE_HI = 36, 45       # 브라스 색상각 대역 (현행 램프 39.85~41.25 를 감싼다)
BUDGET = 4_000_000            # 전수 탐색 예산 (쌍 단위). 넘으면 「미완」


def gray(c):
    """휘도 보존 회색 — 흑백 프린트 · 저조도 · 스크린샷 압축의 대리 지표."""
    g = round(CL.L(c) ** (1 / 2.2) * 255)
    return (g, g, g)


def gL(c):
    return CL.lab(gray(c))[0]


def gates(c):
    """램프 한 색이 반드시 지켜야 하는 것 — 등급과 무관하게 색 하나에 거는 조건."""
    return (CL.L(c) > 0.30                                   # 크롬 대역
            and CL.CR(c, CL.hex2rgb(CARD)) >= TEXT           # 카드 위 글자로도 쓴다
            and CL.dE(c, CL.hex2rgb(INKD)) >= D              # 잉크 표식색 지뢰 회피
            and CL.dE(c, CL.hex2rgb(INKT)) >= D
            and min(CL.dE(c, p) for p in PKC) >= 20.0)       # 팩 12색과 안 섞인다


_SIM = {}


def sims(c):
    if c not in _SIM:
        _SIM[c] = tuple(cvd.sim(c, t) for t in cvd.TYPES) + (gray(c),)
    return _SIM[c]


def ramp_ok(q):
    """4색이 램프로 성립하는가 — 정상 + 1·2·3형 + 회색조 전부에서 변별, 그리고 휘도 단조."""
    for i, j in itertools.combinations(range(4), 2):
        if CL.dE(q[i], q[j]) < D:
            return False
        a, b = sims(q[i]), sims(q[j])
        for k in range(4):
            if CL.dE(a[k], b[k]) < D:
                return False
    return all(CL.L(q[i]) < CL.L(q[i + 1]) for i in range(3))


def build_pool():
    """색상각 36~45°에서 gates를 통과하는 색.

    ★ 2026-09-03 자백 — 처음 이 함수는 (L*, C*) 정수 격자로 후보를 **축약**했다.
      그 축약이 각 칸에서 «먼저 나온 것»만 남기는 바람에 **현행 램프에 더 가까운 해가 버려졌고**,
      영웅 값이 #DEC081(이동 4.14가 아니라 1.15)에서 #E3BE86 으로 **나빠졌다.**
      축약은 속도를 위한 근사였는데 **답을 바꿨다** — 이 저장소가 반복해 당한 형태다
      (생성기의 근사가 결과를 바꾸는데 두 결과가 똑같이 그럴듯하게 생겼다).
      ⇒ **축약을 제거했다.** 대신 §4를 분기한정으로 바꿔 전수를 그대로 감당한다.
      중복 제거는 **같은 8비트 색**에 대해서만 한다(무손실)."""
    seen = {}
    for h in range(HUE_LO, HUE_HI + 1):
        for si in range(0, 101):
            for vi in range(30, 101):
                c = CL.hsv_to_rgb(h / 360.0, si / 100.0, vi / 100.0)
                if c in seen or not gates(c):
                    continue
                seen[c] = True
    return sorted(seen, key=gL)


def report(label, q):
    g = [gL(x) for x in q]
    print(f"  {label}")
    print(f"    hex        : {[CL.rgb2hex(x) for x in q]}")
    print(f"    색상각     : {[f'{CL.hue_deg(x):.1f}' for x in q]}")
    print(f"    회색 L*    : {[f'{v:.2f}' for v in g]}   인접 {[f'{g[i+1]-g[i]:.2f}' for i in range(3)]}")
    print(f"    카드 대비  : {[f'{CL.CR(x, CL.hex2rgb(CARD)):.2f}' for x in q]}")
    print(f"    정상 인접  : {[f'{CL.dE(q[i], q[i+1]):.2f}' for i in range(3)]}"
          f"   모든쌍 최소 {min(CL.dE(q[i], q[j]) for i, j in itertools.combinations(range(4), 2)):.2f}")
    print(f"    회색조     : 모든쌍 최소 "
          f"{min(abs(g[i]-g[j]) for i, j in itertools.combinations(range(4), 2)):.2f}"
          f"   (하한 {D})")
    for t in cvd.TYPES:
        print(f"    {t:8s}   : 모든쌍 최소 "
              f"{min(CL.dE(cvd.sim(q[i], t), cvd.sim(q[j], t)) for i, j in itertools.combinations(range(4), 2)):.2f}")
    print(f"    팩12 최근접 {min(min(CL.dE(x, p) for p in PKC) for x in q):.2f}"
          f" · InkDim 최소 {min(CL.dE(x, CL.hex2rgb(INKD)) for x in q):.2f}"
          f" · 브라스 최근접 {min(CL.dE(x, CL.hex2rgb(BRASS)) for x in q):.2f}")
    print(f"    램프 성립  : {'✔' if ramp_ok(q) else '✘'}")


def main():
    CL.calibrate()
    cvd.calibrate()
    Q = [CL.hex2rgb(h) for _, h in CUR]

    print("=" * 92)
    print("§1. 현행 램프의 구멍 — 회색조에서만 무너진다")
    print("=" * 92)
    report("현행 (PALETTE_SPEC §4-1 · UiChrome._rarityRamp)", Q)
    g0 = gL(Q[0])
    print(f"\n  ★ 4단이 회색조에서 서려면  전설 회색 L* >= 일반 {g0:.2f} + 3 x {D} = {g0 + 3*D:.2f}")
    print(f"     현행 전설 회색 L* = {gL(Q[3]):.2f}   →  부족 {g0 + 3*D - gL(Q[3]):+.2f}")

    print("\n" + "=" * 92)
    print("§2. 자리가 있는가 — 같은 색상각 대역의 회색 L* 폭")
    print("=" * 92)
    pool = build_pool()
    print(f"  색상각 {HUE_LO}~{HUE_HI}° · gates 통과 · (L*,C*) 격자 축약 후보: {len(pool)}")
    print(f"  회색 L* 범위 {gL(pool[0]):.2f} ~ {gL(pool[-1]):.2f}  (폭 {gL(pool[-1])-gL(pool[0]):.2f})")
    print(f"  4단에 필요한 폭 {3*D:.2f}  →  여유 {gL(pool[-1])-gL(pool[0]) - 3*D:+.2f}")

    print("\n" + "=" * 92)
    print("§3. 탐욕 해 — 일반·희귀를 **그대로 두고** 영웅·전설만 가장 가까운 통과 색으로")
    print("=" * 92)
    print("  ★ 이건 순차 탐욕이다. 최적해라고 주장하지 않는다 — §4에서 전수로 확인한다.")

    def nearest(target, lo):
        best = None
        for c in pool:
            if gL(c) < lo:
                continue
            d = CL.dE(c, target)
            if best is None or d < best[0]:
                best = (d, c)
        return best

    h2 = nearest(Q[2], gL(Q[1]) + D)
    h3 = nearest(Q[3], gL(h2[1]) + D)
    greedy = [Q[0], Q[1], h2[1], h3[1]]
    print()
    report("탐욕 해", greedy)
    print(f"    이동 ΔE    : {[f'{CL.dE(greedy[i], Q[i]):.2f}' for i in range(4)]}"
          f"   총 {sum(CL.dE(greedy[i], Q[i]) for i in range(4)):.2f}")

    print("\n" + "=" * 92)
    print("§4. 전수 확인 — 분기한정(branch & bound). 근사 없음, 예산 명시")
    print("=" * 92)
    cand2 = sorted((c for c in pool if gL(c) >= gL(Q[1]) + D), key=lambda c: CL.dE(c, Q[2]))
    cand3 = sorted((c for c in pool if gL(c) >= gL(Q[1]) + 2 * D), key=lambda c: CL.dE(c, Q[3]))
    print(f"  영웅 후보 {len(cand2):,} · 전설 후보 {len(cand3):,}"
          f"  (곱 {len(cand2)*len(cand3):,} — 분기한정으로 실제 검사는 훨씬 적다)")
    print(f"  이동 오름차순 정렬 + 상계 가지치기라 **첫 해가 곧 하계**이고, 완주 시 최적이 증명된다.")
    best, seen, cut = None, 0, 0
    for c in cand2:
        m2 = CL.dE(c, Q[2])
        if best is not None and m2 >= best[0]:
            cut += 1
            break                       # 이후 전부 m2 가 더 크다
        gc = gL(c)
        for d in cand3:
            m3 = CL.dE(d, Q[3])
            if best is not None and m2 + m3 >= best[0]:
                break                   # 이후 전부 m3 가 더 크다
            if gL(d) - gc < D:
                continue
            seen += 1
            if seen > BUDGET:
                break
            q = (Q[0], Q[1], c, d)
            if ramp_ok(q):
                best = (m2 + m3, q)
        if seen > BUDGET:
            break
    if seen > BUDGET:
        print(f"  ★★ 예산 {BUDGET:,} 초과 (검사 {seen:,}) — **이 절은 미완이다.**")
        print("     탐욕 해는 유효하지만 최소 이동임은 증명되지 않았다.")
        best = None
    else:
        print(f"  실제로 검사한 쌍 {seen:,}  ·  상계로 잘라낸 가지 {cut}  →  **완주(최적 증명)**")
        if best:
            print()
            report("전수 최소 이동 해", list(best[1]))
            print(f"    이동 ΔE    : {[f'{CL.dE(best[1][i], Q[i]):.2f}' for i in range(4)]}"
                  f"   총 {best[0]:.2f}")
            same = [CL.rgb2hex(x) for x in best[1]] == [CL.rgb2hex(x) for x in greedy]
            print(f"\n  ★ 탐욕 해 == 전수 최소 이동 해 ?  **{'예' if same else '아니오'}**")
            if not same:
                print(f"     탐욕 총이동 {sum(CL.dE(greedy[i], Q[i]) for i in range(4)):.2f}"
                      f" vs 전수 {best[0]:.2f}  → **전수 해를 채택한다**")
        else:
            print("  ★ 해 없음 — 일반·희귀를 고정하고는 회색조 4단이 안 선다")

    # ---- §4-1. 상계가 답을 감추지 않았는가 (분기한정의 양성 대조) ----
    print("\n" + "-" * 92)
    print("§4-1. ★ 분기한정 검증 — 「검사 1건」이 거짓 통과가 아님을 증명한다")
    print("-" * 92)
    print("  분기한정은 논리로 가지를 자른다. 그런데 **잘린 가지 안에 더 좋은 해가 없다**는 것은")
    print("  가지치기 논리 자체가 옳을 때만 참이다. 그래서 **잘린 영역을 직접 다 열어본다** —")
    print("  총이동이 최적해보다 **작은** 모든 쌍을 빠짐없이 세고, 그 중 성립하는 것이 0인지 본다.")
    if best is None:
        print("  (최적해가 없어 이 검증은 건너뛴다)")
    else:
        lim = best[0]
        below, feas, viol = 0, 0, {}
        for c in cand2:
            m2 = CL.dE(c, Q[2])
            if m2 >= lim:
                break
            gc = gL(c)
            for d in cand3:
                m3 = CL.dE(d, Q[3])
                if m2 + m3 >= lim:
                    break
                below += 1
                q = (Q[0], Q[1], c, d)
                if gL(d) - gc < D:
                    viol["회색 간격"] = viol.get("회색 간격", 0) + 1
                elif ramp_ok(q):
                    feas += 1
                else:
                    viol["램프 판정"] = viol.get("램프 판정", 0) + 1
        print(f"  총이동 < {lim:.2f} 인 쌍: **{below:,}개** — 전부 열어봤다")
        print(f"    성립한 것: {feas}   기각 사유: "
              + (", ".join(f"{k} {v:,}" for k, v in sorted(viol.items())) or "(없음)"))
        print(f"  → {'PASS — 더 좋은 해가 없다. 최적이 확정된다.' if feas == 0 else '★ FAIL — 상계가 해를 감췄다. 결과 폐기.'}")
        print(f"  ★ 그리고 이 수가 0이 아니라 {below:,}이므로 «빈 목록을 돌아 초록»이 아니다"
              f"(TEAM.md 사고 5).")

    print("\n" + "=" * 92)
    print("§5. 결론")
    print("=" * 92)
    final = list(best[1]) if best else greedy
    src = "전수" if best else "탐욕(전수 미완)"
    print(f"  채택 후보({src}): "
          + " / ".join(f"{n} {CL.rgb2hex(c)}" for (n, _), c in zip(CUR, final)))
    print(f"  현행 대비 바뀌는 색: "
          + ", ".join(f"{n} {h}→{CL.rgb2hex(c)}"
                      for (n, h), c in zip(CUR, final) if CL.rgb2hex(c) != h.upper()))
    gg = [gL(x) for x in final]
    print(f"  회색조 모든쌍 최소 {min(abs(gg[i]-gg[j]) for i, j in itertools.combinations(range(4), 2)):.2f}"
          f"  (현행 {min(abs(gL(Q[i])-gL(Q[j])) for i, j in itertools.combinations(range(4), 2)):.2f}"
          f" · 하한 {D})")


def control():
    """양성 대조 — 이 스크립트의 판정기가 실제로 무엇을 잡는지 먼저 보인다."""
    CL.calibrate(verbose=False)
    cvd.calibrate(verbose=False)
    Q = [CL.hex2rgb(h) for _, h in CUR]
    ok = 0
    print("=" * 92)
    print("양성 대조 — 판정기가 죽어 있지 않다는 증거")
    print("=" * 92)

    # 1. ramp_ok 가 현행 램프를 **떨어뜨려야** 한다 (회색조 6.14 < 7.8)
    r = ramp_ok(Q)
    print(f"  1. ramp_ok(현행 램프) = {r}  → {'게이트가 잡았다' if not r else 'FAIL — 게이트가 죽었다'}")
    ok += 0 if r else 1

    # 2. 그리고 회색조만 뺀 판정에서는 **통과해야** 한다 (음성 대조: 항상 ✘가 아니다)
    def ramp_ok_nogray(q):
        for i, j in itertools.combinations(range(4), 2):
            if CL.dE(q[i], q[j]) < D:
                return False
            for t in cvd.TYPES:
                if CL.dE(cvd.sim(q[i], t), cvd.sim(q[j], t)) < D:
                    return False
        return all(CL.L(q[i]) < CL.L(q[i + 1]) for i in range(3))
    r2 = ramp_ok_nogray(Q)
    print(f"  2. 회색조 제외 판정(현행) = {r2}  → "
          f"{'통과(음성 대조 성립 — 실패 원인이 회색조 하나임이 확정)' if r2 else 'FAIL'}")
    ok += 1 if r2 else 0

    # 3. gates 가 잉크 표식색을 잡는가
    g = gates(CL.hex2rgb(INKD))
    print(f"  3. gates(InkDimTone {INKD}) = {g}  → {'게이트가 잡았다' if not g else 'FAIL'}")
    ok += 0 if g else 1

    # 4. gates 가 현행 램프 4색은 통과시키는가 (음성 대조)
    passed = sum(1 for c in Q if gates(c))
    print(f"  4. gates(현행 램프 4색) 통과 {passed}/4  → "
          f"{'통과(자가 항상 ✘가 아니다)' if passed == 4 else 'FAIL'}")
    ok += 1 if passed == 4 else 0

    # 5. gray/gL 이 알려진 값을 내는가
    w, k = gL((255, 255, 255)), gL((0, 0, 0))
    print(f"  5. gL(흰) = {w:.2f} (정답 100) · gL(검) = {k:.2f} (정답 0)  → "
          f"{'PASS' if abs(w-100) < 0.01 and abs(k) < 0.01 else 'FAIL'}")
    ok += 1 if abs(w - 100) < 0.01 and abs(k) < 0.01 else 0

    # 6. 회색끼리의 ΔE 가 L* 차와 같은가 (gray 경로가 색을 안 만든다)
    d = CL.dE(gray(CL.hex2rgb("#F9CB70")), gray(CL.hex2rgb("#DBBD7F")))
    l = abs(gL(CL.hex2rgb("#F9CB70")) - gL(CL.hex2rgb("#DBBD7F")))
    print(f"  6. ΔE(회색,회색) {d:.4f} == |ΔL*| {l:.4f}  → "
          f"{'PASS' if abs(d-l) < 1e-9 else 'FAIL — 회색화가 채도를 남긴다'}")
    ok += 1 if abs(d - l) < 1e-9 else 0

    # 7. 후보 풀이 비어 있지 않은가 (비면 §3·§4의 모든 '없음'이 무효)
    n = len(build_pool())
    print(f"  7. 후보 풀 {n}개  → {'PASS(풀이 살아 있음)' if n > 20 else 'FAIL — 0이면 전부 무효'}")
    ok += 1 if n > 20 else 0

    print(f"\n  대조 {ok} / 7 통과")
    return ok == 7


if __name__ == "__main__":
    if "--control" in sys.argv:
        control()
    else:
        main()
