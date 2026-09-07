#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
활쏘기 — 화면 폭 비례 최소 사거리(s) + 좌우 방향 추첨  검산기 (2026-09-06, coder 라운드 R25)

사용자 재신고(PART1-4, 세 번째):
  "활쏘기 과녁이 캐릭터와 너무 가까운 위치에 생김"
  · 캐릭터로부터 최소 거리 확보 — **화면 폭 비율 기준**으로 구체 수치
  · 과녁 생성 위치를 매번 랜덤화 — **거리 + 좌우 방향 모두**

★ 이 파일은 프로덕션(C#)과 **코드를 공유하지 않는 독립 구현**이다(TEAM.md: 생성기와 검사기가
  코드를 공유하면 둘 다 같은 방향으로 틀린다). 아래 «교정» 절이 깨지면 그 뒤 숫자는 전부 폐기한다.
"""
import random
import sys

# ---------------------------------------------------------------- 상수 (프로덕션과 대조할 값)
H1 = 2.2746944          # StickConfig.BaselineCharacterTotalHeight
CHAR_INSET_R = 0.35     # ArcheryDirector.CharacterEdgeInsetRatio
TARGET_INSET_R = 0.20   # ArcheryDirector.TargetEdgeInsetRatio
RADIUS_R = 0.40         # archeryTargetRadiusRatio
BACKSTEP_R = 1.00       # ArcheryDirector.BackStepRatio
MIN_R = 2.6             # archeryMinTargetDistanceRatio
U0_R = 6.6              # archeryMaxTargetDistanceRatio
F = 0.55                # archeryMinDistanceSpanFraction
G = 0.45                # ★신설 archeryMaxDistanceSpanFraction
UCAP_R = 13.4           # ★신설 archeryMaxDistanceHardCapRatio
S = 0.22                # ★신설 archeryMinTargetDistanceScreenFraction
EPS = 0.001             # ArcheryDirector.EdgeEpsilon

# 실행 환경 실측: 1512x982pt, orthographicSize 12, 종횡비 1.54 -> 가시 반폭 18.48유닛
VIS_HALF = 18.48
PT_PER_UNIT = 1512.0 / (2.0 * VIS_HALF)   # ≈ 40.91 pt/유닛
SCREEN_W = 2.0 * VIS_HALF                 # 걸어다닐 수 있는 폭의 근사(패드 무시)

WALK_H_PER_SEC = 2.5 / H1                 # walkSpeed 2.5 x 배율 / (H1 x 배율) — 배율 무관


def band(span, h, f=F, g=G, ucap_r=UCAP_R, s=S, screen_w=SCREEN_W):
    """밴드 [lo, hi] 와 하한 근거. 프로덕션 식의 **말로 쓴 정의**를 독립 구현한 것."""
    u0 = U0_R * h
    lo_abs = MIN_R * h
    hard = max(u0, ucap_r * h)
    hi = min(span, max(u0, min(g * span, hard)))
    span_floor = f * min(hi, u0)
    screen_floor = min(s * screen_w, f * hi)
    lo = max(lo_abs, span_floor, screen_floor)
    if lo <= lo_abs + EPS:
        src = "절대"
    elif screen_floor > span_floor + EPS:   # ★ 동률은 «화면»이 아니다 — 프로덕션과 같은 엄격 비교
        src = "화면"
    else:
        src = "폭"
    return lo, hi, src


def place(foot_x, lo_x, hi_x, h, roll, dir_roll, f=F, g=G, ucap_r=UCAP_R, s=S, screen_w=SCREEN_W):
    """ResolvePlacement 독립 재구현. (ok, standX, targetX, facing, distance, travel, pinned)"""
    ci = CHAR_INSET_R * h
    ti = RADIUS_R * h + TARGET_INSET_R * h
    bs = BACKSTEP_R * h
    stand_lo, stand_hi = lo_x + ci, hi_x - ci
    tgt_lo, tgt_hi = lo_x + ti, hi_x - ti
    if not (hi_x > lo_x) or stand_hi < stand_lo or tgt_hi < tgt_lo:
        return None
    span = tgt_hi - stand_lo
    if span < MIN_R * h:
        return None
    b_lo, b_hi, src = band(span, h, f, g, ucap_r, s, screen_w)
    d = b_lo + (b_hi - b_lo) * min(1.0, max(0.0, roll))

    def stand(facing):
        if facing > 0:
            s_lo, s_hi = stand_lo, min(stand_hi, tgt_hi - d)
        else:
            s_lo, s_hi = max(stand_lo, tgt_lo + d), stand_hi
        if s_hi < s_lo:
            if s_lo - s_hi > EPS:
                return None
            s_lo = s_hi = (s_lo + s_hi) * 0.5
        x = min(max(foot_x - facing * bs, s_lo), s_hi)
        return x, abs(x - foot_x)

    r, l = stand(1.0), stand(-1.0)
    if r is None and l is None:
        return None
    free = bs + EPS
    fr = r is not None and r[1] <= free
    fl = l is not None and l[1] <= free
    if fr and fl:
        to_right = dir_roll < 0.5
    elif fr:
        to_right = True
    elif fl:
        to_right = False
    elif l is None:
        to_right = True
    elif r is None:
        to_right = False
    elif abs(r[1] - l[1]) <= EPS:
        to_right = dir_roll < 0.5
    else:
        to_right = r[1] < l[1]
    facing = 1.0 if to_right else -1.0
    x, travel = (r if to_right else l)
    tx = x + facing * d
    pinned = (tgt_hi - tx < 0.01 * h) if to_right else (tx - tgt_lo < 0.01 * h)
    return dict(stand=x, target=tx, facing=facing, distance=d, travel=travel,
                pinned=pinned, band=(b_lo, b_hi), src=src, span=span)


# ---------------------------------------------------------------- 교정 (여기가 깨지면 전부 폐기)
def calib():
    bad = 0

    def chk(label, got, want, tol):
        nonlocal bad
        ok = abs(got - want) <= tol
        if not ok:
            bad += 1
        print(f"  [{'OK ' if ok else 'FAIL'}] {label}: 계산 {got:.4f} / 기대 {want} (허용 ±{tol})")

    chk("신장(배율0.45)", H1 * 0.45, 1.024, 0.001)
    chk("pt/유닛", PT_PER_UNIT, 40.91, 0.01)
    # 현행(신고 상태) 실효 하한 = f x U0 = 3.63H. 배율 0.45에서 화면 폭의 몇 %인가.
    h45 = H1 * 0.45
    chk("신고 상태 하한(H배수)", F * U0_R, 3.63, 0.005)
    # ★ 리더 보고 전 자기 정정: 처음에 9.1%로 적었는데 그건 **다른 화면**의 값이었다.
    #   9.08% = 152pt / (40.92유닛 외장 16:9 화면). 1512pt 내장 화면에서는 152/1512 = 10.05%다.
    #   두 값 다 참이고 분모가 다르다 — 이 파일은 1512pt 내장 화면을 정본 분모로 쓴다.
    chk("신고 상태 하한 화면비(배율0.45, 1512pt)", F * U0_R * h45 / SCREEN_W, 0.1005, 0.0006)
    chk("신고 상태 하한 화면비(배율0.45, 외장 span 40.92)", F * U0_R * h45 / 40.92, 0.0908, 0.0006)
    chk("신고 상태 하한 pt(배율0.45)", F * U0_R * h45 * PT_PER_UNIT, 152.0, 1.0)
    # design-systems 설계서 3-3: Ucap 임계 = 4.6 x (1.06/0.62)^2
    chk("Ucap 임계(H배수)", 4.6 * (1.06 / 0.62) ** 2, 13.45, 0.02)
    # 보행 속도는 배율 무관
    chk("보행속도(H/s)", WALK_H_PER_SEC, 1.0990, 0.001)
    print("== 교정 " + ("통과 ==" if bad == 0 else f"실패 {bad}건 — 아래 숫자 전부 폐기 =="))
    return bad == 0


# ---------------------------------------------------------------- [A] 배율별 밴드 (바탕 전폭)
def table_a():
    print("\n" + "=" * 78)
    print("[A] 바탕 전폭에서 밴드 — 신고 상태(현행) vs 처방. 단위: 화면 폭 대비 %")
    print("=" * 78)
    print(f"  화면 폭 {SCREEN_W:.2f}유닛 = 1512pt.  s={S}  g={G}  Ucap={UCAP_R}H  f={F}")
    print("   배율     H(pt)   현행 하한~상한        처방 하한~상한        하한근거   하한 배수")
    for sc in (0.35, 0.45, 0.60, 0.75, 1.00):
        h = H1 * sc
        ci, ti = CHAR_INSET_R * h, (RADIUS_R + TARGET_INSET_R) * h
        span = SCREEN_W - ci - ti
        cur_lo, cur_hi, _ = band(span, h, g=0.0, s=0.0)
        new_lo, new_hi, src = band(span, h)
        print(f"  {sc:4.2f}  {h * PT_PER_UNIT:6.1f}   "
              f"{cur_lo / SCREEN_W:5.1%}~{cur_hi / SCREEN_W:5.1%}  "
              f"({cur_lo * PT_PER_UNIT:5.0f}~{cur_hi * PT_PER_UNIT:5.0f}pt)   "
              f"{new_lo / SCREEN_W:5.1%}~{new_hi / SCREEN_W:5.1%}  "
              f"({new_lo * PT_PER_UNIT:5.0f}~{new_hi * PT_PER_UNIT:5.0f}pt)   "
              f"{src:4s}   x{new_lo / cur_lo:4.2f}")


# ---------------------------------------------------------------- [B] 창(발판) 폭별
def table_b():
    print("\n" + "=" * 78)
    print("[B] 창 위 — 폭이 좁아지면 화면 비례 바닥이 저절로 내려가는가(사용자 09-02 양보절)")
    print("=" * 78)
    print("   배율 0.75(출하 기본). 화면 폭은 그대로 1512pt.")
    print("   발판폭    현행 하한~상한        처방 하한~상한        하한근거  밴드폭/상한")
    h = H1 * 0.75
    ci, ti = CHAR_INSET_R * h, (RADIUS_R + TARGET_INSET_R) * h
    for wpt in (227, 300, 400, 500, 600, 800, 1058, 1280, 1512):
        w = wpt / PT_PER_UNIT
        span = w - ci - ti
        if span < MIN_R * h:
            print(f"   {wpt:5d}pt   (포기 — 절대 하한 {MIN_R}H 미달, 현행/처방 동일)")
            continue
        c_lo, c_hi, _ = band(span, h, g=0.0, s=0.0)
        n_lo, n_hi, src = band(span, h)
        print(f"   {wpt:5d}pt   {c_lo * PT_PER_UNIT:5.0f}~{c_hi * PT_PER_UNIT:5.0f}pt          "
              f"{n_lo * PT_PER_UNIT:5.0f}~{n_hi * PT_PER_UNIT:5.0f}pt          "
              f"{src:4s}      {(n_hi - n_lo) / n_hi:5.1%}")


# ---------------------------------------------------------------- [C] 불변식 몬테카를로
def table_c():
    print("\n" + "=" * 78)
    print("[C] 불변식 — 현행 대비 악화 여부(몬테카를로). ★ 절대치가 아니라 '나빠졌는가'를 본다")
    print("=" * 78)
    rng = random.Random(20260906)
    N = 300000
    giveup_c = giveup_n = 0
    pinned_c = pinned_n = 0
    trav_c = trav_n = 0.0
    below_abs = collapse_c = collapse_n = inverted = 0
    right_n = both_n = 0
    edge20_c = edge20_n = 0     # 과녁이 구간 바깥 20% 대역에 놓인 비율(설계서 5절과 같은 지표)
    for _ in range(N):
        sc = rng.choice((0.35, 0.45, 0.60, 0.75, 1.00))
        h = H1 * sc
        w = rng.uniform(2.0 * h, 90.0 * h)
        lo_x, hi_x = -w / 2, w / 2
        ci = CHAR_INSET_R * h
        fx = rng.uniform(lo_x + ci, hi_x - ci)
        roll, dr = rng.random(), rng.random()
        c = place(fx, lo_x, hi_x, h, roll, dr, g=0.0, s=0.0)
        n = place(fx, lo_x, hi_x, h, roll, dr)
        if c is None:
            giveup_c += 1
        if n is None:
            giveup_n += 1
        if (c is None) != (n is None):
            inverted += 1
        def edge20(p):
            return abs(p["target"]) > 0.5 * w * 0.8

        if c is not None:
            pinned_c += c["pinned"]
            trav_c = max(trav_c, c["travel"] / h)
            b_lo, b_hi = c["band"]
            if b_hi - b_lo < 0.10 * b_hi:
                collapse_c += 1
            edge20_c += edge20(c)
        if n is not None:
            pinned_n += n["pinned"]
            trav_n = max(trav_n, n["travel"] / h)
            if n["distance"] < MIN_R * h - EPS:
                below_abs += 1
            b_lo, b_hi = n["band"]
            if b_hi - b_lo < 0.10 * b_hi:
                collapse_n += 1
            if b_lo > b_hi + EPS:
                inverted += 1
            edge20_n += edge20(n)
            right_n += (n["facing"] > 0)
            both_n += 1
    print(f"  표본 {N}")
    print(f"  포기            현행 {giveup_c:6d} / 처방 {giveup_n:6d}  "
          f"-> {'OK(동일)' if giveup_c == giveup_n else '★변했다'}")
    print(f"  가부 뒤집힘                       {inverted:6d}  "
          f"-> {'OK' if inverted == 0 else '★위반'}")
    print(f"  과녁 구간 끝 못박힘  현행 {pinned_c:6d} / 처방 {pinned_n:6d}  "
          f"-> {'OK(개선/동일)' if pinned_n <= pinned_c else '★악화'}")
    print(f"  최대 접근 도보(H)   현행 {trav_c:6.3f} / 처방 {trav_n:6.3f}  "
          f"-> {'OK' if trav_n <= trav_c + 1e-6 else '★악화'}   "
          f"(시간 {trav_n / WALK_H_PER_SEC:.2f}초, 타임아웃 12초)")
    print(f"  절대 하한 미만 추첨               {below_abs:6d}  -> {'OK' if below_abs == 0 else '★위반'}")
    print(f"  밴드 붕괴(폭<10%상한) 현행 {collapse_c:6d} / 처방 {collapse_n:6d}  "
          f"-> {'OK(동일/개선)' if collapse_n <= collapse_c else '★악화'}")
    print(f"  과녁이 구간 바깥 20%대  현행 {edge20_c:6d} / 처방 {edge20_n:6d}  "
          f"-> {'OK(동일/개선)' if edge20_n <= edge20_c else '★악화(화면 끝으로 갔다)'}")
    print(f"  좌우 균형(오른쪽 비율)            {right_n / max(1, both_n):6.1%}  "
          f"-> {'OK' if 0.35 <= right_n / max(1, both_n) <= 0.65 else '★편향'}")


# ---------------------------------------------------------------- [D] 방향 다양성 (실기 대조용)
def table_d():
    print("\n" + "=" * 78)
    print("[D] 실기 대조용 — 배율 0.75 바탕에서 6회 연속 발동 예상(리더/실기 로그와 같은 형식)")
    print("=" * 78)
    h = H1 * 0.75
    lo_x, hi_x = -VIS_HALF, VIS_HALF
    rng = random.Random(6)
    print("   #   방향   사거리(유닛)   화면대비   서는자리x   과녁x   하한근거")
    seen_dir = set()
    ds = []
    for i in range(6):
        fx = rng.uniform(-8, 8)
        p = place(fx, lo_x, hi_x, h, rng.random(), rng.random())
        seen_dir.add(p["facing"])
        ds.append(p["distance"])
        print(f"  {i + 1:2d}   {'오른' if p['facing'] > 0 else '왼 '}   "
              f"{p['distance']:8.2f}     {p['distance'] / SCREEN_W:6.1%}    "
              f"{p['stand']:7.2f}   {p['target']:7.2f}   {p['src']}")
    print(f"  -> 방향 종류 {len(seen_dir)}가지 / 사거리 폭 {max(ds) - min(ds):.2f}유닛 "
          f"({(max(ds) - min(ds)) / h:.2f}H)")


# ---------------------------------------------------------------- [E] s 스윕 — 왜 0.18인가
def table_e():
    print("\n" + "=" * 78)
    print("[E] s 스윕 — 채택값의 근거(위에서 f x 상한이 자동으로 천장을 친다)")
    print("=" * 78)
    print("   배율 0.75 바탕 전폭.  실효 하한 = min(s x 화면폭, f x 밴드상한)")
    h = H1 * 0.75
    ci, ti = CHAR_INSET_R * h, (RADIUS_R + TARGET_INSET_R) * h
    span = SCREEN_W - ci - ti
    _, b_hi, _ = band(span, h)
    ceil_ = F * b_hi
    print(f"   밴드 상한 {b_hi / SCREEN_W:.1%}W -> 화면 비례 바닥의 구조적 천장 = f x 상한 = "
          f"{ceil_ / SCREEN_W:.1%}W")
    for s in (0.10, 0.14, 0.15, 0.18, 0.20, 0.25, 0.30):
        lo, hi, src = band(span, h, s=s)
        print(f"   s={s:4.2f} -> 실효 하한 {lo / SCREEN_W:5.1%}W ({lo * PT_PER_UNIT:5.0f}pt) "
              f"근거 {src:4s}  밴드폭 {(hi - lo) / hi:5.1%}  "
              f"연속2회 사거리차 E={(hi - lo) / 3 / h:4.2f}H "
              f"{'' if (hi - lo) / 3 / h >= 1.0 else '★<1H(랜덤이 눈에 안 보인다)'}"
              f"{'   <- 채택' if abs(s - S) < 1e-9 else ''}")


def table_f():
    """g 스윕 — 상한을 얼마나 열어야 s가 실제로 걸리는가 + 연출 제약(비행/박자) 확인."""
    print("\n" + "=" * 78)
    print("[F] g 스윕 — 상한을 열지 않으면 s가 구조적으로 무효다(하한 ≤ f x 상한)")
    print("=" * 78)
    h = H1 * 0.75
    ci, ti = CHAR_INSET_R * h, (RADIUS_R + TARGET_INSET_R) * h
    span = SCREEN_W - ci - ti
    print(f"   배율 0.75 바탕 전폭.  요구 하한 s={S} -> {S * SCREEN_W * PT_PER_UNIT:.0f}pt")
    print("     g     상한(%W)  f x 상한(%W)  실효하한(%W)  밴드폭  E(연속2회)  상한 비행(초)  박자")
    for g in (0.00, 0.20, 0.30, 0.35, 0.40, 0.45, 0.50):
        lo, hi, src = band(span, h, g=g)
        # States/ArcheryState.ResolveFlightSeconds: clamp(0.62 x sqrt(d / 4.6H), 0.372, 1.25)
        flight = min(1.25, max(0.372, 0.62 * (hi / (4.6 * h)) ** 0.5))
        beat = "OK" if flight <= 1.06 else "★2발 동시 체공"
        print(f"   {g:4.2f}   {hi / SCREEN_W:6.1%}     {F * hi / SCREEN_W:6.1%}      "
              f"{lo / SCREEN_W:6.1%}({src})   {(hi - lo) / hi:5.1%}   "
              f"{(hi - lo) / 3 / h:4.2f}H      {flight:5.3f}      {beat}"
              f"{'   <- 채택' if abs(g - G) < 1e-9 else ''}")
    print(f"   ※ Ucap {UCAP_R}H가 별도로 천장을 친다(배율 0.35에서 먼저 걸린다).")


def table_g():
    """★ 2026-09-06 실기 실측 대조 + Dock에서의 구조적 한계.

    실기(빌드 09:48, 배율 0.75, 화면 3024x1964 물리 = 1512pt, ⌃⌥⌘A 14회):
      · 14회 전부 발판 span = 22.94유닛 (= Dock. 창/Dock 발판 라벨)
      · 밴드 6.19~11.26유닛 = 17.4%~31.7%W  ← **변경 전과 한 톨도 다르지 않다**
      · 방향 왼7 / 오른7, 사거리 6.37~10.51유닛(17.9%~29.4%W)
    """
    print("\n" + "=" * 78)
    print("[G] ★ 실기 실측과 Dock 한계 — 처방이 «어디서» 걸리고 «어디서» 안 걸리는가")
    print("=" * 78)
    h = H1 * 0.75
    dock_span = 22.94                      # 실기 로그 실측값
    u0, ucap = U0_R * h, UCAP_R * h
    print(f"  배율 0.75, H={h:.4f}유닛, U0={u0:.2f}, Ucap={ucap:.2f}, 화면폭={SCREEN_W:.2f}")
    print(f"  · 처방이 걸리는 조건 = 폭 비례 상한이 U0를 넘을 것 = span > U0/g = "
          f"{u0 / G:.2f}유닛 ({u0 / G * PT_PER_UNIT:.0f}pt)")
    print(f"  · 실기 Dock span = {dock_span:.2f}유닛 ({dock_span * PT_PER_UNIT:.0f}pt) -> "
          f"{'걸린다' if dock_span > u0 / G else '★안 걸린다'}")
    for name, sp in (("Dock(실기)", dock_span), ("바탕 전폭", SCREEN_W - (CHAR_INSET_R + RADIUS_R + TARGET_INSET_R) * h)):
        lo, hi, src = band(sp, h)
        clo, chi, _ = band(sp, h, g=0.0, s=0.0)
        print(f"    {name:12s} span {sp:6.2f}: 구 {clo / SCREEN_W:5.1%}~{chi / SCREEN_W:5.1%}"
              f"  ->  신 {lo / SCREEN_W:5.1%}~{hi / SCREEN_W:5.1%} ({src})")
    print()
    print("  ── Dock에서 22%W를 채우려면 무엇이 필요한가 (선택지, 리더/design-systems 판단) ──")
    want = S * SCREEN_W
    print(f"     요구 하한 {want:.2f}유닛({want * PT_PER_UNIT:.0f}pt) = {S:.0%}W")
    need_hi = want / F
    guard = 0.5 * dock_span + 0.875 * h    # 설계서 3-2 못박힘 방어선
    print(f"     (가) f=0.55 유지 -> 상한이 {need_hi:.2f}유닛({need_hi / dock_span:.1%} of span) 필요. "
          f"못박힘 방어선 {guard:.2f}유닛({guard / dock_span:.0%} of span)을 **넘는다** -> 8-31 재발 위험")
    print(f"     (나) 방어선 안에서 최대치 -> 상한 {guard:.2f} -> 하한 {F * guard:.2f}유닛 = "
          f"{F * guard / SCREEN_W:.1%}W  (지금 {band(dock_span, h)[0] / SCREEN_W:.1%}W)")
    for fcap in (0.55, 0.65, 0.70, 0.75):
        hi2 = min(dock_span, max(u0, min(0.5 * dock_span, ucap)))
        lo2 = max(MIN_R * h, min(want, fcap * hi2))
        e = (hi2 - lo2) / 3 / h
        print(f"     (다) 붕괴 클램프 {fcap:.2f} (밴드폭 >= {1 - fcap:.0%}) -> 하한 {lo2 / SCREEN_W:5.1%}W "
              f"({lo2 * PT_PER_UNIT:5.0f}pt)  밴드폭 {(hi2 - lo2) / hi2:5.1%}  연속2회차 E={e:4.2f}H"
              f"{'  ★E<1H(랜덤이 눈에 안 보인다)' if e < 1.0 else ''}")
    print("     ※ (다)는 «거리»와 «랜덤»의 맞교환이다. 좌우 방향 추첨이 이미 위치 다양성을 주므로")
    print("        E<1H를 감수할 여지가 생겼지만, 그 판단은 design-systems + 리더 소관이다.")


if __name__ == "__main__":
    print("== 교정 ==")
    if not calib():
        sys.exit(1)
    table_a()
    table_b()
    table_c()
    table_d()
    table_e()
    table_f()
    table_g()
