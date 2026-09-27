# -*- coding: utf-8 -*-
"""R1F(문서 진실화 라운드) 독립 계기 — 대비·회색조·ΔE를 처음부터 다시 짠다.

★ 왜 기존 `colorlab.py`를 import 하지 않는가: TEAM.md 「생성기와 검사기가 코드를 공유하면
  둘 다 같은 방향으로 틀린다」. 이 라운드는 **전달값을 다시 재는** 자리이므로 원시 구현을
  새로 쓰고, 교정을 **코드 주석에 커밋된 숫자**(내 자가 아니라 남의 자)로 건다.

교정(이게 깨지면 아래 숫자 전부 폐기):
  · CR(흰,검) = 21.0        · CR(같은색) = 1.0
  · 등급 램프 4색 ↔ CardSurface = 5.68 / 7.40 / 9.41 / 11.66  (UiChrome.cs 주석)
  · RarityTrack ↔ CardSurface = 1.58 · ↔ 일반 = 3.59          (UiChrome.cs 주석)
  · Accent ↔ CardSurface = 6.85 · TextPrimary ↔ CardSurface = 15.00
  · Flatten(AccentSurface, CardSurface) = #33312D
  · OnAccentSolid ↔ Accent = 7.91
"""

# ---------------------------------------------------------------- 원시 변환
def lin(c):
    """sRGB 성분(0..1) -> 선형."""
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def enc(c):
    """선형 -> sRGB 성분(0..1). lin()의 역함수."""
    return c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055


def relL(rgb):
    r, g, b = (lin(x) for x in rgb[:3])
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def CR(a, b):
    la, lb = relL(a), relL(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def flatten(over, onto):
    """UiChrome.Flatten — Mathf.Lerp(onto, over, over.a), 감마 공간 성분 보간."""
    a = max(0.0, min(1.0, over[3]))
    return q(tuple(onto[i] + (over[i] - onto[i]) * a for i in range(3)) + (1.0,))


def lerp(a, b, t):
    return q(tuple(a[i] + (b[i] - a[i]) * t for i in range(3)) + (1.0,))


def hx(v):
    return (((v >> 16) & 255) / 255.0, ((v >> 8) & 255) / 255.0, (v & 255) / 255.0, 1.0)


def tohex(c):
    return "#%02X%02X%02X" % tuple(int(round(max(0.0, min(1.0, x)) * 255)) for x in c[:3])


def b8(c):
    return tuple(int(round(max(0.0, min(1.0, x)) * 255)) for x in c[:3])


def q(c):
    """★ 8bit 스냅. 화면에 나가는 값은 8bit sRGB이므로 대비는 **양자화 뒤** 값으로 재야 한다.

    이 한 줄이 교정을 갈랐다: Unity 리터럴 `new Color(0.784f,0.631f,0.353f)`를 그대로 쓰면
    Accent↔CardSurface가 6.8349로 나오는데 커밋된 문서·주석은 **6.85**다. 리터럴은 hex를
    소수 3자리로 <b>깎아 적은 것</b>이고(round(0.784*255)=200=0xC8), 실제 렌더 픽셀은 hex다.
    그래서 토큰을 정의 시점에 8bit로 스냅한 뒤 계산한다 — 알파는 보존한다(합성에 쓰인다).
    """
    a = c[3] if len(c) > 3 else 1.0
    return tuple(v / 255.0 for v in b8(c)) + (a,)


# ---------------------------------------------------------------- Lab / dE76
def _f(t):
    return t ** (1 / 3) if t > 216 / 24389 else (841 / 108) * t + 4 / 29


def lab(rgb):
    r, g, b = (lin(x) for x in rgb[:3])
    X = 0.4124564 * r + 0.3575761 * g + 0.1804375 * b
    Y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b
    Z = 0.0193339 * r + 0.1191920 * g + 0.9503041 * b
    xr, yr, zr = X / 0.95047, Y / 1.0, Z / 1.08883
    fx, fy, fz = _f(xr), _f(yr), _f(zr)
    return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))


def dE76(a, b):
    la, lb = lab(a), lab(b)
    return sum((la[i] - lb[i]) ** 2 for i in range(3)) ** 0.5


# ---------------------------------------------------------------- 토큰 (UiChrome.cs 실값)
T = {
    "ScreenScrim":       (0.039, 0.047, 0.063, 1.0),
    "PanelSurface":      (0.078, 0.090, 0.110, 1.0),
    "PanelBorder":       (1.0, 1.0, 1.0, 0.16),
    "PanelHighlight":    (1.0, 1.0, 1.0, 0.30),
    "CardSurface":       (0.106, 0.122, 0.149, 1.0),
    "CardBorder":        (1.0, 1.0, 1.0, 0.10),
    "CardSurfaceMuted":  (0.082, 0.094, 0.118, 1.0),
    "SubtleSurface":     (0.098, 0.114, 0.141, 1.0),
    "ThumbSurfaceLocked": (0.063, 0.075, 0.094, 1.0),
    "Accent":            (0.784, 0.631, 0.353, 1.0),
    "AccentSurface":     (0.784, 0.631, 0.353, 0.14),
    "AccentBorder":      (0.784, 0.631, 0.353, 0.55),
    "WarmAccent":        (0.784, 0.631, 0.353, 1.0),
    "TextPrimary":       (0.949, 0.957, 0.969, 1.0),
    "TextSecondary":     (0.682, 0.706, 0.749, 1.0),
    "TextTertiary":      (0.545, 0.576, 0.624, 1.0),
    "NonTextMuted":      (0.424, 0.455, 0.502, 1.0),
    "DisabledControlInk": (0.294, 0.322, 0.361, 1.0),
    "IconInk":           (0.839, 0.859, 0.890, 1.0),
    "OnAccentSolid":     (0.043, 0.063, 0.086, 1.0),
    "TrackBackground":   (1.0, 1.0, 1.0, 0.09),
    "Divider":           (1.0, 1.0, 1.0, 0.07),
}
T = {k: q(v) for k, v in T.items()}          # ★ 8bit 스냅 후 계산 (q() 주석 참조)
WHITE = (1.0, 1.0, 1.0, 1.0)
BLACK = (0.0, 0.0, 0.0, 1.0)

RAMP_SHIPPED = [hx(0x9C978C), hx(0xBCAC8B), hx(0xDEC081), hx(0xFFD375)]
RAMP_PRE_R9 = [hx(0x9C978C), hx(0xBCAC8B), hx(0xDBBD7F), hx(0xF9CB70)]
RAMP_HANDOFF = [hx(0x8A8F98), hx(0x6E9BE8), hx(0xB07BE0), hx(0xE0B24A)]
RARITY_TRACK = hx(0x3A4049)
TINTS = [q(c) for c in [(0.910, 0.514, 0.290, 1.0), (0.310, 0.753, 0.776, 1.0),
                        (0.549, 0.753, 0.431, 1.0), (0.690, 0.561, 0.816, 1.0)]]
TINT_NAME = ["주황 HEAD", "청록 EYES/PET", "초록 NECK/HAIR", "라벤더 BACK/FX"]
RNAME = ["일반", "희귀", "영웅", "전설"]

FAIL = []


def chk(label, got, want, tol):
    ok = abs(got - want) <= tol
    if not ok:
        FAIL.append(label)
    print("  %-52s %9.4f  기대 %-9s %s" % (label, got, want, "OK" if ok else "**깨짐**"))


def chk_s(label, got, want):
    ok = got == want
    if not ok:
        FAIL.append(label)
    print("  %-52s %9s  기대 %-9s %s" % (label, got, want, "OK" if ok else "**깨짐**"))


print("=" * 86)
print("[교정] 알려진 값으로 계기를 먼저 맞춘다 — 하나라도 깨지면 아래 숫자 전부 폐기")
print("=" * 86)
chk("CR(흰, 검)", CR(WHITE, BLACK), 21.0, 1e-9)
chk("CR(같은색: CardSurface, CardSurface)", CR(T["CardSurface"], T["CardSurface"]), 1.0, 1e-12)
chk("CR(같은색: Accent, Accent)", CR(T["Accent"], T["Accent"]), 1.0, 1e-12)
chk("CR(같은색: 흰, 흰)", CR(WHITE, WHITE), 1.0, 1e-12)
for i, c in enumerate(RAMP_SHIPPED):
    # ★ 기대값 출처는 PALETTE_SPEC §27-10-1 / glow.out.txt:394 의 「5.68 / 7.41 / 9.42 / 11.67」이다.
    #   UiChrome.cs 주석은 같은 자리를 「7.40 / 9.41 / 11.66」으로 적고 있다 — 마지막 자리가
    #   내림된 값이라 2dp 반올림과 1 어긋난다(코드 주석 쪽 드리프트, 리더 보고 항목).
    chk("등급 %s ↔ CardSurface" % RNAME[i], CR(c, T["CardSurface"]),
        [5.68, 7.41, 9.42, 11.67][i], 0.006)
chk("RarityTrack ↔ CardSurface", CR(RARITY_TRACK, T["CardSurface"]), 1.58, 0.005)
chk("RarityTrack ↔ 일반(채움 최악)", CR(RARITY_TRACK, RAMP_SHIPPED[0]), 3.59, 0.005)
chk("Accent ↔ CardSurface", CR(T["Accent"], T["CardSurface"]), 6.85, 0.005)
chk("TextPrimary ↔ CardSurface", CR(T["TextPrimary"], T["CardSurface"]), 15.00, 0.005)
ACC_FACE = flatten(T["AccentSurface"], T["CardSurface"])
chk_s("Flatten(AccentSurface, CardSurface) hex", tohex(ACC_FACE), "#33312D")
chk("Accent ↔ 호버면", CR(T["Accent"], ACC_FACE), 5.38, 0.005)
chk("OnAccentSolid ↔ Accent", CR(T["OnAccentSolid"], T["Accent"]), 7.91, 0.005)
chk("CardSurface ↔ 검은 잉크 #111111", CR(T["CardSurface"], hx(0x111111)), 1.14, 0.005)
chk("CardSurface ↔ 흰 잉크", CR(T["CardSurface"], WHITE), 16.53, 0.005)
print()
print("  교정 결과: %s" % ("전건 통과" if not FAIL else "깨진 항목 %d건 -> 이하 폐기" % len(FAIL)))
if FAIL:
    raise SystemExit("교정 실패: " + ", ".join(FAIL))

print()
print("=" * 86)
print("[①] Accent 와 WarmAccent 는 같은 색인가 — 토큰 이름 거짓의 실제 피해 범위")
print("=" * 86)
print("  Accent     = %s  8bit %s" % (tohex(T["Accent"]), b8(T["Accent"])))
print("  WarmAccent = %s  8bit %s" % (tohex(T["WarmAccent"]), b8(T["WarmAccent"])))
print("  성분 동일 : %s" % (T["Accent"][:3] == T["WarmAccent"][:3]))
print("  ΔE76      : %.6f   CR : %.6f" % (dE76(T["Accent"], T["WarmAccent"]),
                                          CR(T["Accent"], T["WarmAccent"])))
print("  ⇒ 픽셀은 한 값도 안 바뀐다. 거짓은 **토큰 이름**이고 피해는 「어느 상수를 고치면")
print("     그 자리가 바뀌는가」를 코더가 잘못 찾는 것이다(WarmAccent 소비처 vs Accent 소비처).")

print()
print("=" * 86)
print("[③] 대비 미달 목록 — 전달값을 내 자로 다시 잰다 (하한: 비텍스트 3.0)")
print("=" * 86)
DIAL_TRACK = flatten(T["TrackBackground"], T["PanelSurface"])
FAN_BORDER_PLAIN = flatten(T["CardBorder"], T["CardSurface"])
FAN_BORDER_ONACC = flatten(T["AccentBorder"], ACC_FACE)
QUIT_ARMED_FACE = ACC_FACE
QUIT_BORDER_ARMED = flatten(T["AccentBorder"], QUIT_ARMED_FACE)
QUIT_BORDER_PLAIN = flatten(T["CardBorder"], T["SubtleSurface"])
RING_TRACK_ON_CARD = flatten(T["TrackBackground"], T["CardSurface"])
TRACK_ON_MUTED = flatten(T["TrackBackground"], T["CardSurfaceMuted"])

rows = [
    ("팝오버 다이얼 트랙 ↔ 패널면", DIAL_TRACK, T["PanelSurface"], 1.28),
    ("일시정지 호(NonTextMuted) ↔ 다이얼 트랙", T["NonTextMuted"], DIAL_TRACK, 2.98),
    ("일시정지 호(NonTextMuted) ↔ 패널면", T["NonTextMuted"], T["PanelSurface"], None),
    ("정상 호(WarmAccent) ↔ 다이얼 트랙", T["WarmAccent"], DIAL_TRACK, None),
    ("카드 기본 테두리 ↔ 카드면", FAN_BORDER_PLAIN, T["CardSurface"], 1.35),
    ("부채꼴 평상 테두리 ↔ 버튼면(CardSurface)", FAN_BORDER_PLAIN, T["CardSurface"], None),
    ("부채꼴 세션중 링트랙 ↔ 버튼면", RING_TRACK_ON_CARD, T["CardSurface"], 1.31),
    ("부채꼴 호버 면 변화(CardSurface -> 호버면)", ACC_FACE, T["CardSurface"], 1.28),
    ("종료 호버 면 변화(SubtleSurface -> CardSurface)", T["CardSurface"], T["SubtleSurface"], 1.02),
    ("종료 무장 면 ↔ 평상 면(SubtleSurface)", QUIT_ARMED_FACE, T["SubtleSurface"], None),
    ("종료 무장 테두리 ↔ 무장 면", QUIT_BORDER_ARMED, QUIT_ARMED_FACE, None),
    ("종료 평상 테두리 ↔ 평상 면", QUIT_BORDER_PLAIN, T["SubtleSurface"], None),
    ("부채꼴 호버 테두리 ↔ 호버 면", FAN_BORDER_ONACC, ACC_FACE, None),
    ("트랙 ↔ CardSurfaceMuted", TRACK_ON_MUTED, T["CardSurfaceMuted"], None),
]
print("  %-48s %7s %7s %s" % ("자리", "내 실측", "전달값", "3.0"))
for name, a, b, claimed in rows:
    v = CR(a, b)
    mark = "미달" if v < 3.0 else "통과"
    cl = "%7.2f" % claimed if claimed is not None else "      —"
    agree = ""
    if claimed is not None:
        agree = " 일치" if abs(v - claimed) <= 0.005 else " ★어긋남(%+.3f)" % (v - claimed)
    print("  %-48s %7.4f %s  %s%s" % (name, v, cl, mark, agree))

print()
print("  호 vs 트랙 / 트랙 vs 바탕 — 「분모 없는 분자」의 실체:")
print("    정상 호 ↔ 트랙 = %.4f (통과)   트랙 ↔ 패널 = %.4f (미달)" %
      (CR(T["WarmAccent"], DIAL_TRACK), CR(DIAL_TRACK, T["PanelSurface"])))
print("    일시정지 호 ↔ 트랙 = %.4f          트랙 ↔ 패널 = %.4f" %
      (CR(T["NonTextMuted"], DIAL_TRACK), CR(DIAL_TRACK, T["PanelSurface"])))

print()
print("  [미착지 처방] TrackBackground α를 올리면 트랙↔패널이 언제 3.0을 넘는가:")
for a in (0.09, 0.12, 0.16, 0.20, 0.24, 0.28, 0.32, 0.36, 0.40):
    tb = (1.0, 1.0, 1.0, a)
    onp = flatten(tb, T["PanelSurface"])
    onc = flatten(tb, T["CardSurface"])
    print("    α%.2f  트랙↔패널 %5.2f  트랙↔카드 %5.2f  정상호↔트랙(패널) %5.2f  "
          "일시정지호↔트랙 %5.2f" % (a, CR(onp, T["PanelSurface"]), CR(onc, T["CardSurface"]),
                                  CR(T["WarmAccent"], onp), CR(T["NonTextMuted"], onp)))

print()
print("=" * 86)
print("[②] 출하 램프 회색조 — 양자화 변형별 최소 대비 (램프는 건드리지 않는다)")
print("=" * 86)


def gray_pow22(c):
    """완전색맹 대리: 휘도 보존 회색 L^(1/2.2) — PALETTE_SPEC §27-13이 쓴 모델."""
    return relL(c) ** (1 / 2.2)


def gray_srgb(c):
    """휘도 보존 회색, 정확한 sRGB 역전달."""
    return enc(relL(c))


def gray_luma709(c):
    """감마 인코딩 값에 Rec.709 계수를 그대로 (선형화 없음)."""
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


VARIANTS = [("L^(1/2.2) 후 8bit 반올림", gray_pow22, True),
            ("sRGB 역전달 후 8bit 반올림", gray_srgb, True),
            ("감마값 Rec.709 luma 후 8bit 반올림", gray_luma709, True),
            ("양자화 없음(상대휘도 직접)", None, False)]


def ramp_report(title, ramp):
    print()
    print("  ── %s" % title)
    for vname, fn, quant in VARIANTS:
        if quant:
            g8 = [int(round(255 * max(0.0, min(1.0, fn(c))))) for c in ramp]
            grays = [(v / 255.0,) * 3 + (1.0,) for v in g8]
            label = "/".join(str(v) for v in g8)
        else:
            grays = ramp
            label = "—"
        pairs = [(i, j) for i in range(4) for j in range(i + 1, 4)]
        crs = [(CR(grays[i], grays[j]), i, j) for i, j in pairs]
        adj = [CR(grays[i], grays[i + 1]) for i in range(3)]
        mn, mi, mj = min(crs)
        mono = all(relL(grays[i]) < relL(grays[i + 1]) for i in range(3))
        print("     %-38s 회색 %-19s 최소CR %.4f (%s↔%s) 인접 %s 단조 %s"
              % (vname, label, mn, RNAME[mi], RNAME[mj],
                 "/".join("%.3f" % x for x in adj), "✔" if mono else "✘"))


ramp_report("출하 램프 #9C978C / #BCAC8B / #DEC081 / #FFD375 (R9 보정판)", RAMP_SHIPPED)
ramp_report("개선 전(R9 이전) #9C978C / #BCAC8B / #DBBD7F / #F9CB70", RAMP_PRE_R9)
ramp_report("인계본 RAR_A #8A8F98 / #6E9BE8 / #B07BE0 / #E0B24A", RAMP_HANDOFF)

print()
print("  회색조 ΔE76 최소(모든쌍) — 문서의 6.14 / 7.91 과 대조:")
for title, ramp in (("출하(R9)", RAMP_SHIPPED), ("개선 전", RAMP_PRE_R9)):
    for vname, fn, quant in VARIANTS:
        if quant:
            g8 = [int(round(255 * max(0.0, min(1.0, fn(c))))) for c in ramp]
            grays = [(v / 255.0,) * 3 + (1.0,) for v in g8]
        else:
            grays = ramp
            g8 = None
        des = [dE76(grays[i], grays[j]) for i in range(4) for j in range(i + 1, 4)]
        print("    %-10s %-38s 최소ΔE %.4f  미달(7.8)쌍 %d/6" %
              (title, vname, min(des), sum(1 for d in des if d < 7.8)))

print()
print("=" * 86)
print("[④] 카테고리 틴트 4색 — 색만으로 카테고리를 말할 수 있는가")
print("=" * 86)
for i, c in enumerate(TINTS):
    print("  %-16s %s  L* %6.2f" % (TINT_NAME[i], tohex(c), lab(c)[0]))
print()
print("  정상 시각 ΔE76 (6쌍):")
pn = [(i, j) for i in range(4) for j in range(i + 1, 4)]
dn = [(dE76(TINTS[i], TINTS[j]), i, j) for i, j in pn]
for d, i, j in sorted(dn):
    print("    %-16s ↔ %-16s %7.2f" % (TINT_NAME[i], TINT_NAME[j], d))
print("  최소 %.2f / 최대 %.2f" % (min(d for d, _, _ in dn), max(d for d, _, _ in dn)))
print()
print("  완전색맹(명도만) 변형별 최악쌍:")
for vname, fn, quant in VARIANTS:
    if quant:
        g8 = [int(round(255 * max(0.0, min(1.0, fn(c))))) for c in TINTS]
        grays = [(v / 255.0,) * 3 + (1.0,) for v in g8]
        label = "/".join(str(v) for v in g8)
    else:
        grays = TINTS
        label = "—"
    dg = [(dE76(grays[i], grays[j]), i, j) for i, j in pn]
    mn, mi, mj = min(dg)
    print("    %-38s 회색 %-19s 최악쌍 ΔE %.4f (%s ↔ %s)"
          % (vname, label, mn, TINT_NAME[mi], TINT_NAME[mj]))

print()
print("  등급 4단은 왜 모범형인가 — 채널 수:")
print("    ① 칸 수  = (int)rarity + 1  (색·색맹·흑백·저해상도 어느 축에도 안 걸린다)")
print("    ② 낱말   = 등급 이름(확정)")
print("    ③ 색     = 램프 4색(보조)  ← 이것만으로는 식별 하한 48.6을 못 넘는다")
print("    카테고리 틴트는 ③만 있고 ①②에 해당하는 것이 「동반 텍스트」 하나다.")
print()
print("=" * 86)
print("끝 — 교정 통과 후 산출. 토큰 값으로 계산한 대비는 합성·알파·배경 텍스처를 통과한")
print("     뒤의 실제 대비가 아니다. 최종 판정은 실기 캡처.")
print("=" * 86)
