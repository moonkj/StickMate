# -*- coding: utf-8 -*-
"""등급 4단계 시각 언어 — 실측 (design-art, 2026-09-02 3차 라운드)

리더 질의: "등급 체계가 코드에 0건이다. 그 시각 언어를 지금 만들어라."
  · 운반 후보를 전부 적고 각각의 대가를 잰다
  · 44px 카드(=IconSize)와 161x108 카드에서 4단이 갈리는가
  · 색각 이상에서 무너지는가

  python3 rarity.py

교정: colorlab(16건) + cvd(13건). 하나라도 깨지면 숫자를 내지 않고 죽는다.
"""
import sys, os, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
import cvd

# ---------------------------------------------------------------- 프로덕션에서 읽은 값
# CharacterInfoWindow.cs (상수) — 숫자를 발명하지 않는다
CARD_W, CARD_H = 161.0, 108.0
THUMB_X, THUMB_Y, THUMB_W, THUMB_H = 11.0, -8.0, 139.0, 54.0
ICON = 44.0
LOCK_W, LOCK_H = 18.0, 17.0
NAME_Y, TEXT_H, NAME_W = -64.0, 14.0, 90.0
META_X, META_W = 109.0, 41.0
ACTION_Y, ACTION_H = -80.0, 24.0     # UiChrome.MinTargetSizePoints

# UiChrome.cs (색 토큰) — float -> 8bit 반올림은 Unity와 같은 규칙
def u(r, g, b): return tuple(int(round(v * 255)) for v in (r, g, b))
CARD_SURFACE   = u(0.106, 0.122, 0.149)   # #1B1F26
CARD_MUTED     = u(0.082, 0.094, 0.118)   # #15181E  썸네일 바탕
THUMB_LOCKED   = u(0.063, 0.075, 0.094)   # #101318
PANEL_SURFACE  = u(0.078, 0.090, 0.110)   # #14171C
TEXT_PRIMARY   = u(0.949, 0.957, 0.969)   # #F2F4F7
ICON_INK       = u(0.839, 0.859, 0.890)   # #D6DBE3
ACCENT_BLUE    = u(0.365, 0.631, 0.961)   # #5DA1F5  (지금 코드값)
BRASS          = CL.hex2rgb("#C8A15A")    # 리더 판정 eca8c58 — 코드에는 아직 없다

CARD_BGS = [("카드 바탕 CardSurface #1B1F26", CARD_SURFACE),
            ("썸네일 바탕 CardSurfaceMuted #15181E", CARD_MUTED),
            ("잠긴 썸네일 ThumbSurfaceLocked #101318", THUMB_LOCKED),
            ("패널 바탕 PanelSurface #14171C", PANEL_SURFACE)]

# 장비가 실제로 놓이는 배경 넷 (ItemPaletteBandGateTests와 같은 목록)
ART_BGS = [("흰 바탕화면", (255, 255, 255)), ("검은 바탕화면", (0, 0, 0)),
           ("종이 무대 #E9EAE6", CL.hex2rgb("#E9EAE6")), ("목탄 무대 #25282E", CL.hex2rgb("#25282E"))]

NONTEXT, TEXT = 3.0, 4.5
DISCERN = 7.8            # 변별 하한 (PALETTE_SPEC §3-3, 출하색에서 뽑은 값)

# 리더 판정 eca8c58 "등급 램프 채택" = PALETTE_SPEC §4 후보 B
RAMP = [("일반", "#9C978C", 1), ("희귀", "#BCAC8B", 2),
        ("영웅", "#DBBD7F", 3), ("전설", "#F9CB70", 4)]


def gray(hx):
    """흑백 근사 — 상대휘도를 8bit sRGB 회색으로 되돌린 값."""
    l = CL.L(CL.hex2rgb(hx))
    lo, hi = 0, 255
    for _ in range(40):
        mid = (lo + hi) / 2.0
        if CL.L((mid, mid, mid)) < l: lo = mid
        else: hi = mid
    return int(round((lo + hi) / 2.0))


def sec(t):
    print("\n" + "=" * 78); print(t); print("=" * 78)


def main():
    CL.calibrate()
    cvd.calibrate()

    # ---------------------------------------------------------------- §1
    sec("§1. 램프 자체 — 정상 시각")
    print(f"{'등급':6s} {'색':>9s} {'L(상대휘도)':>11s} {'L*':>7s} {'C*':>6s} {'흑백':>5s} {'칸':>3s}")
    for nm, hx, seg in RAMP:
        c = CL.hex2rgb(hx); a = CL.lab(c)
        print(f"{nm:6s} {hx:>9s} {CL.L(c):11.4f} {a[0]:7.2f} "
              f"{(a[1]**2+a[2]**2)**0.5:6.1f} {gray(hx):5d} {seg:3d}")
    print("\n인접 단 / 모든 쌍")
    for (n1, h1, _), (n2, h2, _) in itertools.combinations(RAMP, 2):
        a, b = CL.hex2rgb(h1), CL.hex2rgb(h2)
        adj = "인접" if abs([r[0] for r in RAMP].index(n1) - [r[0] for r in RAMP].index(n2)) == 1 else "    "
        print(f"  {adj} {n1}↔{n2}: ΔE {CL.dE(a,b):6.2f}  CR {CL.CR(a,b):5.2f}  "
              f"흑백차 {abs(gray(h1)-gray(h2)):3d}/255  {'' if CL.dE(a,b)>=DISCERN else '← 변별 하한 7.8 미달'}")

    # ---------------------------------------------------------------- §2
    sec("§2. 색각 이상 — 색만으로 4단이 남는가")
    for k in cvd.TYPES:
        print(f"\n[{cvd.KOR[k]}]")
        simd = [(nm, CL.rgb2hex(cvd.sim(CL.hex2rgb(hx), k))) for nm, hx, _ in RAMP]
        print("  보이는 색: " + "  ".join(f"{nm} {h}" for nm, h in simd))
        worst = 999; bad = 0
        for (n1, h1), (n2, h2) in itertools.combinations(simd, 2):
            d = CL.dE(CL.hex2rgb(h1), CL.hex2rgb(h2))
            i1 = [r[0] for r in RAMP].index(n1); i2 = [r[0] for r in RAMP].index(n2)
            adj = abs(i1 - i2) == 1
            if d < DISCERN: bad += 1
            if adj: worst = min(worst, d)
            print(f"    {'인접' if adj else '    '} {n1}↔{n2}: ΔE {d:6.2f} "
                  f"CR {CL.CR(CL.hex2rgb(h1), CL.hex2rgb(h2)):5.2f}"
                  f"{'  ← 하한 미달' if d < DISCERN else ''}")
        print(f"  인접 최악 ΔE {worst:.2f} / 하한 미달 쌍 {bad} / 6")

    sec("§2-b. 완전색맹(1색각·명도만) — 흑백 근사")
    gs = [(nm, gray(hx)) for nm, hx, _ in RAMP]
    print("  " + "  ".join(f"{nm} {g}" for nm, g in gs))
    for (n1, g1), (n2, g2) in itertools.combinations(gs, 2):
        c1, c2 = (g1,)*3, (g2,)*3
        adj = abs([r[0] for r in RAMP].index(n1) - [r[0] for r in RAMP].index(n2)) == 1
        print(f"    {'인접' if adj else '    '} {n1}↔{n2}: 채널차 {abs(g1-g2):3d}/255  "
              f"CR {CL.CR(c1,c2):5.2f}  ΔE {CL.dE(c1,c2):6.2f}")

    # ---------------------------------------------------------------- §3
    sec("§3. 등급색이 놓일 수 있는 배경 — 창 안(카드) vs 창 밖(바탕화면)")
    print("[창 안 — 카드 표면 4종]  면 하한 3.0 / 글자 하한 4.5")
    print(f"{'등급':6s} " + " ".join(f"{n.split()[0]:>10s}" for n, _ in CARD_BGS) + "   최악  판정")
    for nm, hx, _ in RAMP:
        c = CL.hex2rgb(hx)
        vs = [CL.CR(c, b) for _, b in CARD_BGS]
        w = min(vs)
        print(f"{nm:6s} " + " ".join(f"{v:10.2f}" for v in vs) +
              f" {w:6.2f}  면{'O' if w>=NONTEXT else 'X'} 글자{'O' if w>=TEXT else 'X'}")
    print("\n[창 밖 — 장비가 실제로 놓이는 배경 4종] (등급색을 몸/데스크톱에 내보내면)")
    print(f"{'등급':6s} " + " ".join(f"{n.split()[0]:>10s}" for n, _ in ART_BGS) + "   최악  판정")
    for nm, hx, _ in RAMP:
        c = CL.hex2rgb(hx)
        vs = [CL.CR(c, b) for _, b in ART_BGS]
        w = min(vs)
        print(f"{nm:6s} " + " ".join(f"{v:10.2f}" for v in vs) +
              f" {w:6.2f}  {'통과' if w>=NONTEXT else '★미달'}")
    print("\n[창 밖 · WornColor 통과 후 = 몸에 칠했을 때]")
    print(f"{'등급':6s} {'몸 색':>9s} " + " ".join(f"{n.split()[0]:>10s}" for n, _ in ART_BGS) + "   최악")
    worn = []
    for nm, hx, _ in RAMP:
        w = CL.worn(CL.hex2rgb(hx)); worn.append((nm, w))
        vs = [CL.CR(w, b) for _, b in ART_BGS]
        print(f"{nm:6s} {CL.rgb2hex(w):>9s} " + " ".join(f"{v:10.2f}" for v in vs) + f" {min(vs):6.2f}")
    print("  몸 위 인접/전 쌍:")
    for (n1, c1), (n2, c2) in itertools.combinations(worn, 2):
        print(f"    {n1}↔{n2}: ΔE {CL.dE(c1,c2):6.2f} CR {CL.CR(c1,c2):5.2f} "
              f"최대채널차 {max(abs(a-b) for a,b in zip(c1,c2)):3d}/255")

    # ---------------------------------------------------------------- §4
    sec("§4. 글자로서의 등급색 — 카드 위 낱말/이름에 쓸 수 있는가")
    for nm, hx, _ in RAMP:
        c = CL.hex2rgb(hx)
        print(f"  {nm:4s} {hx} on CardSurface {CL.CR(c, CARD_SURFACE):5.2f}:1  "
              f"on PanelSurface {CL.CR(c, PANEL_SURFACE):5.2f}:1  "
              f"{'글자 가능' if min(CL.CR(c,CARD_SURFACE), CL.CR(c,PANEL_SURFACE)) >= TEXT else '★글자 불가'}")
    print("\n  반대 방향 — 등급색을 카드 바탕으로 쓰고 그 위에 기존 잉크를 얹으면")
    for nm, hx, _ in RAMP:
        c = CL.hex2rgb(hx)
        print(f"  {nm:4s} 바탕 {hx}: TextPrimary {CL.CR(TEXT_PRIMARY, c):5.2f}:1  "
              f"IconInk {CL.CR(ICON_INK, c):5.2f}:1  → {'가능' if CL.CR(TEXT_PRIMARY,c)>=TEXT else '★ 카드 잉크 전량 반전 필요'}")

    # ---------------------------------------------------------------- §5
    sec("§5. 강조색·기존 상태색과의 충돌")
    others = [("Accent 파랑(현행 코드)", ACCENT_BLUE), ("브라스(리더 판정)", BRASS),
              ("TextPrimary", TEXT_PRIMARY), ("IconInk", ICON_INK)]
    for onm, oc in others:
        ds = [(CL.dE(CL.hex2rgb(hx), oc), nm) for nm, hx, _ in RAMP]
        d, nm = min(ds)
        print(f"  {onm:22s} 최근접 등급 = {nm} ΔE {d:6.2f} {'← 같은 색으로 읽힌다' if d < DISCERN else ''}")

    # ---------------------------------------------------------------- §6
    sec("§6. 칸(개수) 축 — 161pt 카드에서 4칸이 그려지는가")
    for inset in (11.0, 14.0):
        track = CARD_W - inset * 2
        for gap in (2.0, 3.0, 4.0):
            segw = (track - gap * 3) / 4.0
            print(f"  인셋 {inset:4.1f} · 간격 {gap:.0f} → 트랙 {track:5.1f}pt · 칸 폭 {segw:5.2f}pt "
                  f"({'OK' if segw >= 12 else '좁다'})  1칸/4칸 채움 폭 {segw:5.2f} / {track:5.1f}")
    print("\n  ★ 리더가 말한 '44px 카드'를 가정하면 (실제로는 44pt 카드가 없다 — IconSize다)")
    MIN_SEG_PX, MIN_GAP_PX = 3.0, 1.0
    need = MIN_SEG_PX * 4 + MIN_GAP_PX * 3
    for w, inset, gap in ((CARD_W, 11.0, 2.0), (ICON, 4.0, 1.0), (ICON, 2.0, 1.0)):
        track = w - inset * 2
        segw = (track - gap * 3) / 4.0
        print(f"    폭 {w:5.1f}pt · 인셋 {inset:4.1f} · 간격 {gap:.0f} → 칸 폭 {segw:5.2f}pt "
              f"· 최소 필요 {need:.0f}pt · 여유 {track/need:4.1f}x "
              f"{'OK' if track >= need else '★불가'}")

    print(f"\n  카드 세로 예산: 썸네일 {abs(THUMB_Y):.0f}~{abs(THUMB_Y)+THUMB_H:.0f} · "
          f"이름 {abs(NAME_Y):.0f}~{abs(NAME_Y)+TEXT_H:.0f} · "
          f"버튼 {abs(ACTION_Y):.0f}~{abs(ACTION_Y)+ACTION_H:.0f} / 카드 {CARD_H:.0f}")
    print(f"  → 상단 여백 0~{abs(THUMB_Y):.0f}pt({abs(THUMB_Y):.0f}pt) · "
          f"이름↔버튼 사이 {abs(ACTION_Y)-(abs(NAME_Y)+TEXT_H):.0f}pt · "
          f"하단 {CARD_H-(abs(ACTION_Y)+ACTION_H):.0f}pt")

    # ---------------------------------------------------------------- §7
    sec("§7. 44pt 아이콘 — 등급을 아이콘 안에 넣을 수 있는가")
    print(f"  아이콘 {ICON:.0f}x{ICON:.0f}pt = {ICON*ICON:.0f}pt² · 썸네일 {THUMB_W:.0f}x{THUMB_H:.0f} "
          f"= {THUMB_W*THUMB_H:.0f}pt² · 자물쇠 배지 {LOCK_W:.0f}x{LOCK_H:.0f} = {LOCK_W*LOCK_H:.0f}pt²")
    for t in (1.0, 1.5, 2.0, 3.0):
        ring = ICON * ICON - (ICON - 2 * t) ** 2
        print(f"  아이콘 둘레 {t:.1f}pt 테두리 = {ring:6.1f}pt² (아이콘 면적의 {100*ring/(ICON*ICON):4.1f}%)")
    for t in (1.0, 2.0, 3.0):
        ring = THUMB_W * THUMB_H - (THUMB_W - 2*t) * (THUMB_H - 2*t)
        print(f"  썸네일 둘레 {t:.1f}pt 테두리 = {ring:6.1f}pt² (썸네일 면적의 {100*ring/(THUMB_W*THUMB_H):4.1f}%)")

    # ---------------------------------------------------------------- §8
    sec("§8. 채움 트랙 — 빈 칸을 보이게 둘 것인가")
    for track_hx in ("#1B1F26", "#15181E", "#2A2F38", "#3A4049", "#4A515C"):
        t = CL.hex2rgb(track_hx)
        vs = [(nm, CL.CR(CL.hex2rgb(hx), t)) for nm, hx, _ in RAMP]
        print(f"  트랙 {track_hx}: 카드바탕 대비 {CL.CR(t, CARD_SURFACE):5.2f}  "
              f"채움 대비 " + " ".join(f"{nm} {v:5.2f}" for nm, v in vs) +
              f"  최악 {min(v for _, v in vs):5.2f} {'OK' if min(v for _,v in vs)>=NONTEXT else '★미달'}")


if __name__ == "__main__":
    main()
