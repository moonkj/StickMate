# -*- coding: utf-8 -*-
"""우클릭 부채꼴 메뉴 시각 언어 — 검산 (design-art R14, 2026-09-05)

사용자 신규 지시: 우상단 상시 톱니를 없애고 캐릭터 우클릭으로 부채꼴이 펼쳐진다. 부채꼴은 이제 임의의 바탕화면 + 유저 잉크 선화 위에 놓인다.
이 파일이 재는 것(전부 숫자):
  §1 현행 스택(GearRadialMenuWidget: 원판 CardSurface · 테 흰 α0.10 1.2pt · 기호 TextPrimary)의 회색 0~255 전 구간 보장 대비
  §2 제안 = 2톤 테두리(밝은 링 + 어두운 면) — 톱니 헤일로와 같은 관용구(InfoGearIconWidget.ResolveHaloColor)를 면에 적용. 후보 쌍 전수
  §3 캐릭터 분리 — 잉크가 어떤 휘도든(검/흰/임의) 2톤 가장자리와 ≥ 보장값 · 장비 재질색과의 대비
  §4 호버·무장 면(Flatten) · 기호·배지·카운트다운 링 대비
  §5 글리프 5종의 획 여유(W = 2pt 단위) — 코드 기하에서 계산
  §6 이름표·온보딩 알약의 같은 문제
  §7 DLC 6팩 재점검(재질 팔레트 R13 이후) — 대역·ΔE·그늘 배수 드리프트
★ 계산기 = colorlab.py, 쓰기 전에 교정. 회색 스윕은 Tests/EditMode/InfoGearHaloContrastTests.WorstGuaranteedContrast 와 같은 합성(감마 공간 lerp).
★ 프로덕션 0줄. 오프라인 계산 — 최종 판정은 실기 캡처.
"""
import os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
EQ = os.path.abspath(os.path.join(HERE, "../../equipment/verify")); sys.path.insert(0, EQ)
import colorlab as CL

# ---- UiChrome 토큰 (hex/255 그대로) ----
TOK = {
    "PanelSurface": "#14171C", "CardSurface": "#1B1F26", "CardSurfaceMuted": "#15181E", "SubtleSurface": "#191D24",
    "TextPrimary": "#F2F4F7", "TextSecondary": "#AEB4BF", "OnAccentSolid": "#0B1016", "Accent": "#C8A15A",
    "IconInk": "#D6DBE3", "InkContrastCharcoal": "#252829",
}
ALPHA = {"CardBorder": 0.10, "PanelBorder": 0.16, "AccentSurface": 0.14, "AccentBorder": 0.55, "TrackBackground": 0.09, "PanelHighlight": 0.30}
INK_BLACK, INK_WHITE = "#111111", "#FFFFFF"
W_PEN = 2.0   # UiChrome.PenPoints / GearRadialMenuWidget.SymbolStroke

rgb = CL.hex2rgb; hx = CL.rgb2hex
def cr(a, b): return CL.CR(rgb(a), rgb(b))
def L(h): return CL.L(rgb(h))
def lerp(a, b, t): return hx(tuple(a[i] + (b[i] - a[i]) * t for i in range(3)))
def flatten(over_hex, alpha, onto_hex): return lerp(rgb(onto_hex), rgb(over_hex), alpha)     # UiChrome.Flatten (감마 공간)
def grey(level): return hx((level, level, level))

def sweep_two_tone(dark, light, alpha=1.0):
    """회색 0~255 배경 위에서 max(어두운 톤↔배경, 밝은 톤↔배경)의 최소값 (InfoGearHaloContrastTests 와 같은 식, 알파 합성 포함)."""
    worst, at = 99.0, 0
    for g in range(256):
        bg = grey(g)
        d = lerp(rgb(bg), rgb(dark), alpha); l = lerp(rgb(bg), rgb(light), alpha)
        best = max(cr(d, bg), cr(l, bg))
        if best < worst: worst, at = best, g
    return worst, at

def sweep_single(color, alpha=1.0):
    worst, at = 99.0, 0
    for g in range(256):
        bg = grey(g); c = lerp(rgb(bg), rgb(color), alpha); v = cr(c, bg)
        if v < worst: worst, at = v, g
    return worst, at

def frac_below(color, floor=3.0):
    n = sum(1 for g in range(256) if cr(color, grey(g)) < floor)
    return n / 256.0

def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    CL.calibrate(verbose=False)
    w("=== 0. 자 교정 === colorlab.calibrate() 통과. 추가: 톱니 헤일로 쌍(#0B1016 / #F2F4F7) 회색 스윕 = %.2f (InfoGearIconWidget 문서값 4.37, α=1 기준) · 같은 쌍 α0.80 = %.2f" % (
        sweep_two_tone(TOK["OnAccentSolid"], TOK["TextPrimary"])[0], sweep_two_tone(TOK["OnAccentSolid"], TOK["TextPrimary"], 0.80)[0]))
    w("   (4.37 은 잉크 #000000 기준: 검정/#F2F4F7 스윕 = %.2f — 자는 일치한다)" % sweep_two_tone("#000000", TOK["TextPrimary"])[0])
    w()
    # ---- §1 현행 ----
    w("=== 1. 현행 부채꼴 스택 — 임의의 바탕화면 위에서 ===")
    face = TOK["CardSurface"]; border = flatten("#FFFFFF", ALPHA["CardBorder"], face)
    ws, wg = sweep_single(face); wb, wbg = sweep_single(border)
    w("   원판 CardSurface %s(L %.4f): 회색 스윕 최악 %.2f (배경 %s) · 3.0 미달 구간 %.0f%% of 256" % (face, L(face), ws, grey(wg), 100 * frac_below(face)))
    w("   테 흰 α0.10 → 실효 %s(L %.4f): 최악 %.2f (배경 %s) · 미달 %.0f%%" % (border, L(border), wb, grey(wbg), 100 * frac_below(border)))
    w2, g2 = sweep_two_tone(face, border)
    w("   원판+테 2톤으로 봐도: 최악 %.2f (배경 %s) — 테가 어두워서(L %.4f) 밝은 톤 노릇을 못 한다" % (w2, grey(g2), L(border)))
    w("   기호 TextPrimary ↔ 원판 %.2f (원판이 불투명이라 배경 무관) · 기호↔배경(원판이 사라진 경우) 최악 %.2f" % (cr(TOK["TextPrimary"], face), sweep_single(TOK["TextPrimary"])[0]))
    w("   ★ 캐릭터: 원판↔검은 잉크 #111 %.2f · 테↔검은 잉크 %.2f · 원판↔흰 잉크 %.2f — 검은 잉크 팔다리가 원판 가장자리를 지나면 한 덩어리(%.2f)" % (cr(face, INK_BLACK), cr(border, INK_BLACK), cr(face, INK_WHITE), cr(face, INK_BLACK)))
    w("   호버 표면 Lerp(CardSurface, AccentSurface α0.14) → 알파 0.14 = 86% 투명(UI_SURFACE_SPEC §6.4 지적 그대로) · 무장 표면 AccentSurface 그대로 = 같은 결함")
    w()
    # ---- §2 제안: 2톤 테두리 ----
    w("=== 2. 제안 — 2톤 가장자리(밝은 링 + 어두운 면) 후보 전수, 회색 스윕 보장값 ===")
    cands = [("면 CardSurface + 링 TextPrimary", TOK["CardSurface"], TOK["TextPrimary"]),
             ("면 CardSurface + 링 흰 #FFFFFF", TOK["CardSurface"], "#FFFFFF"),
             ("면 OnAccentSolid + 링 TextPrimary (톱니 쌍)", TOK["OnAccentSolid"], TOK["TextPrimary"]),
             ("면 PanelSurface + 링 TextPrimary", TOK["PanelSurface"], TOK["TextPrimary"]),
             ("면 CardSurface + 링 IconInk", TOK["CardSurface"], TOK["IconInk"]),
             ("면 CardSurface + 링 Accent 브라스", TOK["CardSurface"], TOK["Accent"]),
             ("면 CardSurface + 링 TextSecondary", TOK["CardSurface"], TOK["TextSecondary"])]
    for nm, d, l in cands:
        v, g = sweep_two_tone(d, l)
        v95, _ = sweep_two_tone(d, l, 0.95)
        w("   %-40s 최악 %.2f (배경 %s, L %.4f) · α0.95 %.2f · 이론값 sqrt((Ld+.05)(Ll+.05))/... = %.2f" % (
            nm, v, grey(g), L(grey(g)), v95, math.sqrt((L(l) + 0.05) / (L(d) + 0.05))))
    w("   판정: 면 = CardSurface(창 카드와 같은 토큰, 정합성) · 링 = TextPrimary(창 글자·톱니 헤일로와 같은 토큰). 보장 %.2f ≥ 3.0. 브라스 링은 %.2f 로 미달(밝은 바탕에서 죽는다)." % (
        sweep_two_tone(TOK["CardSurface"], TOK["TextPrimary"])[0], sweep_two_tone(TOK["CardSurface"], TOK["Accent"])[0]))
    w("   링 폭: 2.0pt(= 펜 하나 규칙 UiChrome.PenPoints) — 톱니 헤일로 실효 폭 3.74pt 는 획 위에 얹는 값이고, 면의 테두리는 획 하나로 충분(원판 지름 44 안쪽 2pt 링 + 면 Ø40 → 기하 불변)")
    w()
    # ---- §3 캐릭터 분리 ----
    w("=== 3. 캐릭터 분리 — 잉크 휘도 전 구간 · 장비 재질색 ===")
    face, ring = TOK["CardSurface"], TOK["TextPrimary"]
    v, g = sweep_two_tone(face, ring)
    w("   잉크가 회색 어느 값이든(유저 잉크 임의): 2톤 가장자리 ↔ 잉크 최악 %.2f (잉크 L %.4f 근처) — 바탕화면 스윕과 같은 식이다" % (v, L(grey(g))))
    w("   검은 잉크 #111: 링↔잉크 %.2f · 면↔잉크 %.2f  |  흰 잉크: 링↔잉크 %.2f · 면↔잉크 %.2f" % (cr(ring, INK_BLACK), cr(face, INK_BLACK), cr(ring, INK_WHITE), cr(face, INK_WHITE)))
    mats = ["#96814F", "#BA7636", "#5577AE", "#9B7922", "#C6443C", "#5075B5", "#587398", "#20878C", "#CC5512", "#5A8C3C", "#428C24", "#BA5928", "#CC3C3C", "#6787B9", "#955CCC", "#AB7942", "#3378CC", "#988540"]
    w("   장비 재질색(R13 팔레트 18색 표본) ↔ 면 최소 %.2f · ↔ 링 최소 %.2f — 캐릭터가 장비를 입고 있어도 가장자리가 선다" % (min(cr(m, face) for m in mats), min(cr(m, ring) for m in mats)))
    w("   ★ 부채꼴은 잉크색을 쓰지 않는다 — 캐릭터(잉크)와 메뉴(크롬 토큰)는 색의 출처가 다르다. 유저가 잉크를 어떤 색으로 바꿔도 메뉴는 그대로다.")
    w()
    # ---- §4 상태 면 ----
    w("=== 4. 호버·무장·배지·링 — 불투명 합성(UiChrome.Flatten) ===")
    hover_face = flatten(TOK["Accent"], ALPHA["AccentSurface"], TOK["CardSurface"])
    hover_border = flatten(TOK["Accent"], ALPHA["AccentBorder"], TOK["CardSurface"])
    w("   호버 면 = Flatten(AccentSurface α0.14, CardSurface) = %s (L %.4f) · 기호 Accent↔호버면 %.2f · TextPrimary↔호버면 %.2f · 호버면↔평면 %.2f" % (
        hover_face, L(hover_face), cr(TOK["Accent"], hover_face), cr(TOK["TextPrimary"], hover_face), cr(hover_face, TOK["CardSurface"])))
    w("   호버 링 = TextPrimary 유지(가장자리 보장은 상태와 무관해야 한다) — 브라스 링(Flatten AccentBorder = %s)은 밝은 바탕 %.2f 로 미달" % (hover_border, cr(hover_border, "#F2F2F2")))
    w("   무장([앱 종료] 1차 클릭) 면 = 같은 호버 면 %s · 기호/카운트다운 링 WarmAccent %s ↔ 면 %.2f · 이름표 「정말 종료?」 TextPrimary↔PanelSurface %.2f" % (
        hover_face, TOK["Accent"], cr(TOK["Accent"], hover_face), cr(TOK["TextPrimary"], TOK["PanelSurface"])))
    w("   할일 배지 Accent 면 + OnAccentSolid 글자 %.2f (텍스트 4.5 통과) · 배지↔원판 %.2f · 배지↔링 %.2f" % (cr(TOK["OnAccentSolid"], TOK["Accent"]), cr(TOK["Accent"], TOK["CardSurface"]), cr(TOK["Accent"], TOK["TextPrimary"])))
    w("   집중 링 트랙 TextPrimary(현행) → 세션 중 잔여 호 WarmAccent↔원판 %.2f · 체크마크 Accent↔원판 %.2f" % (cr(TOK["Accent"], TOK["CardSurface"]), cr(TOK["Accent"], TOK["CardSurface"])))
    w()
    # ---- §5 글리프 여유 ----
    w("=== 5. 글리프 5종 — 24pt 상자 · 획 %.1fpt(W) · 최소 여유(코드 기하, 1.5W = %.1fpt 규칙) ===" % (W_PEN, 1.5 * W_PEN))
    d = math.radians
    rows = []
    # ① 스톱워치: 링 Ø20/획2 → 안지름 16(안반지름 8.0 = 9−1). 분침 (0,3.25) 길이 6.5 세로 → y 0..6.5; 끝↔링 안쪽 = 8 − 6.5 − 1(획 반) = 1.5? 정확히: 링 안쪽 가장자리 반지름 9, 분침 끝 y 6.5 + 1.0(획 반) = 7.5 → 여유 1.5
    rows.append(("① 스톱워치", "분침 끝 ↔ 링 안쪽", 9.0 - (6.5 + 1.0), "시침 끝 ↔ 링 안쪽 %.2f" % (9.0 - (math.hypot(math.cos(d(-30)) * 2.5, math.sin(d(-30)) * 2.5) + 2.5 + 1.0))))
    # ② 스틱맨: 다리 2개 −106°/−74°, 길이 7, 골반 (0,−4.5), 획 1.8 → 끝점 간격
    tipL = (math.cos(d(-106)) * 7, math.sin(d(-106)) * 7); tipR = (math.cos(d(-74)) * 7, math.sin(d(-74)) * 7)
    leg_gap = math.hypot(tipR[0] - tipL[0], tipR[1] - tipL[1]) - 1.8
    armL = (math.cos(d(-140)) * 6, 3.5 + math.sin(d(-140)) * 6); head_bottom = 8 - 3.5
    rows.append(("② 스틱맨", "다리 끝 사이", leg_gap, "머리 밑↔어깨 %.1f(의도한 접합) · 획 1.8 ≠ 펜 2.0" % (head_bottom - 3.5)))
    # ③ 체크리스트: 줄 y 7/0/−7 획2 → 틈 5; 박스 4.5 획1 → 구멍 2.5; 박스↔줄 4.75
    rows.append(("③ 체크리스트", "빈 박스 구멍(4.5−2×1)", 2.5, "줄 사이 5.0(2.5W) · 박스↔줄 4.75 · 체크 획 1.6"))
    # ④ 확성기: 입(x 5, 획2 → 4..6) ↔ 소리선 시작(중심 9.6 길이 4.6 ±30° → x 9.6−2.3cos30=7.6)
    wave_x0 = 9.6 - 2.3 * math.cos(d(30)); mouth_x1 = 5 + 1.0
    rows.append(("④ 확성기", "입 ↔ 소리선", wave_x0 - mouth_x1 - 0.8, "목 부분 나팔 사이 %.2f · 소리선 획 1.6" % ((5 - 6.8 * math.tan(d(13))) * 2 - 2)))
    # ⑤ 전원: 링 Ø20 틈 50° → 틈 반폭 @r10 = 10·sin25 = 4.23; 세로획 반폭 1 → 여유 3.23
    rows.append(("⑤ 전원", "세로획 ↔ 틈 가장자리", 10 * math.sin(d(25)) - 1.0, "세로획 길이 11 · 링 안쪽 9 → 획 끝은 틈 밖(y 11 > 10)"))
    for nm, what, val, note in rows:
        w("   %-8s %-16s %5.2fpt = %.2fW %s  | %s" % (nm, what, val, val / W_PEN, "통과" if val >= 1.5 * W_PEN else "★ 1.5W 미달", note))
    w("   → 1.5W 미달 3건(스톱워치 분침·스틱맨 다리·확성기 소리선) + 체크박스 구멍 1.25W. 44px 1×에서 2px 획 사이 1.5~2.4px 틈 — 실기 미확인.")
    w()
    # ---- §6 알약 ----
    w("=== 6. 이름표·온보딩 알약 ===")
    pill_border = flatten("#FFFFFF", ALPHA["PanelBorder"], TOK["PanelSurface"])
    w("   PanelSurface %s 스윕 최악 %.2f · PanelBorder α0.16 실효 %s 최악 %.2f → 알약도 어두운 바탕에서 사라진다(글자 TextPrimary는 %.2f 로 읽힌다)" % (
        TOK["PanelSurface"], sweep_single(TOK["PanelSurface"])[0], pill_border, sweep_single(pill_border)[0], cr(TOK["TextPrimary"], TOK["PanelSurface"])))
    w("   처방: 알약 테두리 = TextPrimary 1pt(같은 2톤) → 보장 %.2f. 온보딩 알약의 AccentBorder(브라스 α0.55 실효 %s)는 밝은 바탕 %.2f — 같은 처방" % (
        sweep_two_tone(TOK["PanelSurface"], TOK["TextPrimary"])[0], flatten(TOK["Accent"], ALPHA["AccentBorder"], TOK["PanelSurface"]), cr(flatten(TOK["Accent"], ALPHA["AccentBorder"], TOK["PanelSurface"]), "#F2F2F2")))
    w()
    # ---- §7 팩 재점검 ----
    w("=== 7. DLC 6팩 재점검 (R13 재질 팔레트 확정 이후) ===")
    packs = [("오피스 워커", "#456ECC", "#6080CC"), ("사이버 아포칼립스", "#009682", "#518C84"), ("네온 낙서", "#CC1BA9", "#9C5A8E"),
             ("스포츠", "#CC3F29", "#9E655C"), ("컬러 잉크", "#9768CC", "#8563AB"), ("밀리터리", "#639400", "#798C51")]
    import palette_model as PM
    mats24 = PM.used_colors()
    for nm, p, s in packs:
        dmin = min(CL.dE(rgb(c), rgb(m)) for c in (p, s) for m in mats24)
        sh62 = hx(tuple(int(round(v * 0.62)) for v in rgb(p))); sh28 = hx(tuple(int(round(v * 0.28)) for v in rgb(p)))
        w("   %-10s 주 %s L %.4f · 보 %s L %.4f · 재질24 최근접 ΔE %5.2f · 면(#1B1F26)↔주색 %.2f 링↔주색 %.2f · 그늘 ×0.62 %s / ×0.28(코드 ShadeFactor) %s" % (
            nm, p, L(p), s, L(s), dmin, cr(TOK["CardSurface"], p), cr(TOK["TextPrimary"], p), sh62, sh28))
    w("   → 대역·ΔE 하한 변화 없음(재질 hex 0 변경). ★ 드리프트 1건: PACK_THEME_SPEC §5·§10-8 「그늘 = FillOutlineColor ×0.62」는 코드 AccessoryTone.ShadeFactor 0.28 과 다르다 — 값 정정 필요(리더 경유).")
    w("   부채꼴 크롬(면 CardSurface·링 TextPrimary·강조 브라스)은 팩 12색과 독립 — 팩 아이템을 입은 캐릭터 위에서 면↔팩색 최소 %.2f · 링↔팩색 최소 %.2f" % (
        min(cr(TOK["CardSurface"], c) for _, p, s in packs for c in (p, s)), min(cr(TOK["TextPrimary"], c) for _, p, s in packs for c in (p, s))))

if __name__ == "__main__":
    report()
