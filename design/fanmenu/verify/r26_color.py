"""R26 §4-2 — 「왜 잉크 윤곽·흰 광택이 이 크기로 이식되지 않는가」의 대비 근거.
토큰 값은 Interaction/UiChrome.cs 거울."""


def lin(c):
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4


def L(rgb):
    r, g, b = [lin(c) for c in rgb]
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def CR(a, b):
    la, lb = L(a), L(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


TextPrimary = (0.949, 0.957, 0.969)          # UiChrome.cs:262
CardSurface = (0.106, 0.122, 0.149)          # :136
Accent = (0.784, 0.631, 0.353)               # :227
CardSurfaceMuted = (0.082, 0.094, 0.118)     # :142
CardIconInk = (0xE8 / 255, 0xE2 / 255, 0xD6 / 255)   # EQUIPMENT_HANDOFF_PORT_SPEC §13-3-1
cap = (0x96 / 255, 0x81 / 255, 0x4F / 255)   # 야구모자 재질색 tone0 (§13-3-2)
gloss = tuple(0.58 * c + 0.42 for c in cap)  # 흰 42 % over 재질색 = 인계본 톤 3

print("부채꼴 심볼(TextPrimary) ↔ 원판 면(CardSurface)          = {:.2f}:1".format(CR(TextPrimary, CardSurface)))
print("체크(Accent)             ↔ 원판 면                       = {:.2f}:1".format(CR(Accent, CardSurface)))
print("장비 카드 잉크(CardIconInk) ↔ 카드 바탕(CardSurfaceMuted)   = {:.2f}:1".format(CR(CardIconInk, CardSurfaceMuted)))
print("장비 카드 재질색(#96814F)  ↔ 카드 바탕                     = {:.2f}:1  ← 이게 낮아서 윤곽이 필요했다".format(CR(cap, CardSurfaceMuted)))
print("장비 카드 하이라이트(흰42%) ↔ 그 재질색                     = {:.2f}:1".format(CR(gloss, cap)))
print()
print("⇒ 카드가 <잉크 윤곽>을 쓰는 이유 = 채움이 중간 명도 재질색이라 어두운 바탕에서 형태가 안 선다({:.2f}:1).".format(CR(cap, CardSurfaceMuted)))
print("  부채꼴 심볼의 잉크는 TextPrimary({:.2f}:1)라 그 이유가 <존재하지 않는다>. 윤곽을 이식하면 조각만 2배가 된다.".format(CR(TextPrimary, CardSurface)))
print("  하이라이트 1.88:1 은 <넓은 면 위의 결>이라 성립한다 — 1.5pt 획으로는 보이지 않는다.")
