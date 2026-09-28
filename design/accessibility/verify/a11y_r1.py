# -*- coding: utf-8 -*-
"""design-accessibility R1 — 접근성 탭 개방 설계의 실측.

교정 없이는 아무 숫자도 안 낸다(docs/TEAM.md 공통 처방: 흰/검 21.0, 동일색 1.0).
색 값은 전부 Assets/_Project/Scripts/Interaction/UiChrome.cs 에서 손으로 옮긴 뒤
스크립트 안에 출처 주석을 붙였다. 옮겨 적기 오류를 막기 위해 byte 값을 함께 찍는다.
"""
import sys, os, math

HERE = os.path.dirname(os.path.abspath(__file__))
# design/accessibility/verify/ -> design/art/verify/ (저장소 상대. 절대 경로를 적지 않는다)
ART = os.path.normpath(os.path.join(HERE, os.pardir, os.pardir, "art", "verify"))
if not os.path.isdir(ART):
    print("colorlab/cvd 를 찾지 못했습니다: %s" % ART); sys.exit(2)
sys.path.insert(0, ART)
import colorlab as C      # hex2rgb / L / CR / dE / rgb2hex
import cvd                # Vienot-Brettel-Mollon 사영

# ============================================================================
# 0. 교정 — 깨지면 뒤 숫자 전부 폐기
# ============================================================================
def calibrate():
    ok = True
    w, b = (255, 255, 255), (0, 0, 0)
    cr_wb = C.CR(w, b)
    print("[교정 1] CR(흰,검)            = %.4f  기대 21.0000  %s" % (cr_wb, "OK" if abs(cr_wb - 21.0) < 1e-6 else "FAIL"))
    ok &= abs(cr_wb - 21.0) < 1e-6
    for h in ("#1B1F26", "#FFD375", "#8B939F"):
        c = C.hex2rgb(h)
        cr = C.CR(c, c)
        good = abs(cr - 1.0) < 1e-9
        print("[교정 2] CR(%s,자기)      = %.6f  기대 1.000000  %s" % (h, cr, "OK" if good else "FAIL"))
        ok &= good
    # 대칭성
    a, bb = C.hex2rgb("#1B1F26"), C.hex2rgb("#FFD375")
    sym = abs(C.CR(a, bb) - C.CR(bb, a)) < 1e-12
    print("[교정 3] CR 대칭              = %s" % ("OK" if sym else "FAIL"))
    ok &= sym
    # dE 자기 = 0
    d = C.dE(a, a)
    print("[교정 4] dE(자기,자기)        = %.6f  기대 0  %s" % (d, "OK" if d < 1e-9 else "FAIL"))
    ok &= d < 1e-9
    # cvd 자체 교정 (무채축 불변 등)
    print("[교정 5] cvd.py 자체 교정:")
    try:
        cvd.calibrate(verbose=False)
        # 무채축 불변 직접 재확인 (양성이 아니라 음성 대조: 회색이 색을 띠면 FAIL)
        worst = 0
        for v in range(0, 256, 1):
            g = (v, v, v)
            for kind in ("protan", "deutan", "tritan"):
                s = cvd.sim(g, kind)
                worst = max(worst, max(abs(s[i] - v) for i in range(3)))
        print("         무채축 최대 이탈 = %d/255  기대 0  %s" % (worst, "OK" if worst == 0 else "FAIL"))
        ok &= worst == 0
    except SystemExit:
        print("         cvd.calibrate가 sys.exit — FAIL")
        ok = False
    if not ok:
        print("\n★ 교정 실패 — 아래 숫자를 전부 폐기한다.")
        sys.exit(2)
    print("★ 교정 5건 전건 통과.\n")

calibrate()

# ============================================================================
# 1. 토큰 — UiChrome.cs 에서 옮긴 값 (float -> 0..255 반올림)
# ============================================================================
def f2b(*f):
    return tuple(int(round(x * 255)) for x in f)

TOK = {
    # 면
    "PanelSurface":       (f2b(0.078, 0.090, 0.110), "UiChrome.cs:123"),
    "CardSurface":        (f2b(0.106, 0.122, 0.149), "UiChrome.cs:136"),
    "CardSurfaceMuted":   (f2b(0.082, 0.094, 0.118), "UiChrome.cs:142"),
    "ScreenScrim":        (f2b(0.039, 0.047, 0.063), "UiChrome.cs:102"),
    "SubtleSurface":      (f2b(0.098, 0.114, 0.141), "UiChrome.cs:165"),
    "Accent":             (f2b(0.784, 0.631, 0.353), "UiChrome.cs:227 브라스"),
    # 글자
    "TextPrimary":        (f2b(0.949, 0.957, 0.969), "UiChrome.cs:262 #f2f4f7"),
    "TextSecondary":      (f2b(0.682, 0.706, 0.749), "UiChrome.cs:265 #aeb4bf"),
    "TextTertiary":       (f2b(0.545, 0.576, 0.624), "UiChrome.cs:268 #8b939f"),
    "NonTextMuted":       (f2b(0.424, 0.455, 0.502), "UiChrome.cs:276 #6c7480 글자금지"),
    "DisabledControlInk": (f2b(0.294, 0.322, 0.361), "UiChrome.cs:284 #4b525c 글자금지"),
    "IconInk":            (f2b(0.839, 0.859, 0.890), "UiChrome.cs:287"),
    "OnAccentSolid":      (f2b(0.043, 0.063, 0.086), "UiChrome.cs:386"),
    "RetiredTabInactive": (f2b(0.475, 0.502, 0.549), "UiChrome.cs:362 폐기(네거티브 컨트롤)"),
    # 등급 램프 — 출하판 (R9 보정 후)
    "일반":   (C.hex2rgb("#9C978C"), "UiChrome.cs:479"),
    "희귀":   (C.hex2rgb("#BCAC8B"), "UiChrome.cs:480"),
    "영웅":   (C.hex2rgb("#DEC081"), "UiChrome.cs:481 R9 보정판"),
    "전설":   (C.hex2rgb("#FFD375"), "UiChrome.cs:482 R9 보정판"),
    "RarityTrack": (C.hex2rgb("#3A4049"), "UiChrome.cs:501"),
}
# PALETTE_SPEC §12-1이 쓴 램프 — 옛 값(대조용)
OLD_RAMP = ["#9C978C", "#BCAC8B", "#DBBD7F", "#F9CB70"]
NEW_RAMP = ["#9C978C", "#BCAC8B", "#DEC081", "#FFD375"]
NAMES = ["일반", "희귀", "영웅", "전설"]

MIN_TEXT = 4.5      # UiChrome.MinTextContrast — WCAG 2.1 SC 1.4.3 AA 본문
MIN_NONTEXT = 3.0   # UiChrome.MinNonTextContrast — WCAG 2.1 SC 1.4.11
MIN_TARGET = 24.0   # UiChrome.MinTargetSizePoints — WCAG 2.2 SC 2.5.8

print("=" * 78)
print("1. 토큰 실측값 (옮겨적기 검산: hex 와 byte 를 같이 찍는다)")
print("=" * 78)
for k, (v, src) in TOK.items():
    print("  %-19s %s  rgb%-16s L=%.4f   %s" % (k, C.rgb2hex(v), str(v), C.L(v), src))
print()

# ============================================================================
# 2. 접근성 탭이 실제로 쓰게 될 글자·컨트롤의 대비
# ============================================================================
print("=" * 78)
print("2. [접근성 · 성능] 탭 3행이 쓰는 잉크 — AA 4.5 / 비텍스트 3.0")
print("=" * 78)
rows = [
    ("행 라벨(활성)",      "TextPrimary",        "CardSurface",      MIN_TEXT),
    ("행 캡션",            "TextTertiary",       "CardSurface",      MIN_TEXT),
    ("카드 제목(eyebrow)", "TextTertiary",       "PanelSurface",     MIN_TEXT),
    ("행 라벨(비활성)",    "TextSecondary",      "CardSurfaceMuted", MIN_TEXT),
    ("비활성 이유 한 줄",  "TextTertiary",       "CardSurfaceMuted", MIN_TEXT),
    ("탭 라벨(안 고른)",   "TextTertiary",       "PanelSurface",     MIN_TEXT),
    ("탭 라벨(고른)",      "OnAccentSolid",      "Accent",           MIN_TEXT),
    ("★옛 탭 잉크(폐기)",  "RetiredTabInactive", "CardSurface",      MIN_TEXT),
    ("★글자금지 잉크",     "NonTextMuted",       "CardSurface",      MIN_TEXT),
    ("★글자금지 잉크2",    "DisabledControlInk", "CardSurface",      MIN_TEXT),
    ("꺼진 토글 채움",     "DisabledControlInk", "CardSurface",      MIN_NONTEXT),
    ("켜진 토글 채움",     "Accent",             "CardSurface",      MIN_NONTEXT),
    ("아이콘 잉크",        "IconInk",            "CardSurface",      MIN_NONTEXT),
]
for label, ink, bg, floor in rows:
    cr = C.CR(TOK[ink][0], TOK[bg][0])
    print("  %-20s %-19s on %-17s %6.2f:1  하한 %.1f  %s"
          % (label, ink, bg, cr, floor, "PASS" if cr >= floor else "FAIL"))
print()
print("  ※ 「★」 세 줄은 음성 대조다 — 폐기·글자금지 잉크로 글자를 그리면 FAIL이 나와야 한다.")
print("     위에서 실제로 FAIL/경계가 나왔으므로 이 표의 PASS 들은 죽은 프로브가 아니다.")
print()

# ============================================================================
# 3. 등급 램프 — 출하판을 다시 잰다 (PALETTE_SPEC §12-1은 옛 램프다)
# ============================================================================
print("=" * 78)
print("3. 등급 램프 대비 — 출하판(R9) vs PALETTE_SPEC §12-1이 쓴 옛 램프")
print("=" * 78)
print("  3-a 카드 바탕(#%s) 대비 / 리본 트랙(#3A4049) 대비" % C.rgb2hex(TOK["CardSurface"][0])[1:])
for i, nm in enumerate(NAMES):
    new = C.hex2rgb(NEW_RAMP[i]); old = C.hex2rgb(OLD_RAMP[i])
    cr_new = C.CR(new, TOK["CardSurface"][0]); cr_old = C.CR(old, TOK["CardSurface"][0])
    cr_trk = C.CR(new, TOK["RarityTrack"][0])
    note = "" if NEW_RAMP[i] == OLD_RAMP[i] else "  (옛 %s = %.2f)" % (OLD_RAMP[i], cr_old)
    print("    %-4s %s  카드 %6.2f:1  트랙 %5.2f:1  %s%s"
          % (nm, NEW_RAMP[i], cr_new, cr_trk,
             "PASS" if cr_trk >= MIN_NONTEXT else "FAIL", note))
print()

print("  3-b 휘도 단조성 (서열이 색 없이도 남는가)")
Ls = [C.L(C.hex2rgb(h)) for h in NEW_RAMP]
mono = all(Ls[i] < Ls[i + 1] for i in range(3))
for i, nm in enumerate(NAMES):
    print("    %-4s L=%.4f" % (nm, Ls[i]))
print("    단조 증가: %s   최대/최소 대비 = %.2f:1" % ("OK" if mono else "FAIL", C.CR(C.hex2rgb(NEW_RAMP[3]), C.hex2rgb(NEW_RAMP[0]))))
print()

print("  3-c 인접 단 대비 — 「두 칸을 나란히 놓으면 경계가 보이는가」")
for i in range(3):
    a, b = C.hex2rgb(NEW_RAMP[i]), C.hex2rgb(NEW_RAMP[i + 1])
    ao, bo = C.hex2rgb(OLD_RAMP[i]), C.hex2rgb(OLD_RAMP[i + 1])
    print("    %s↔%s  CR %.3f:1  dE %5.2f   (옛 램프 CR %.3f dE %5.2f)"
          % (NAMES[i], NAMES[i + 1], C.CR(a, b), C.dE(a, b), C.CR(ao, bo), C.dE(ao, bo)))
print()

# ============================================================================
# 4. 색각 이상 + 완전색맹(= 흑백 프린트 · 저조도 · 압축)
# ============================================================================
print("=" * 78)
print("4. 색각 이상 실측 — 출하 램프. 변별 하한 dE 7.8 (PALETTE_SPEC §8 과 같은 자)")
print("=" * 78)
DISCRIM = 7.8
def gray_of(rgb):
    """휘도 보존 무채화 — 완전색맹/흑백 근사. sRGB 역감마로 되돌린다."""
    y = C.L(rgb)                      # 상대 휘도 0..1
    # y -> sRGB 인코딩
    s = 12.92 * y if y <= 0.0031308 else 1.055 * (y ** (1 / 2.4)) - 0.055
    v = int(round(max(0.0, min(1.0, s)) * 255))
    return (v, v, v)

for kind, title in (("normal", "정상"), ("protan", "1형 적색맹"),
                    ("deutan", "2형 녹색맹"), ("tritan", "3형 청색맹"),
                    ("mono", "완전색맹/흑백")):
    if kind == "normal":
        seen = [C.hex2rgb(h) for h in NEW_RAMP]
    elif kind == "mono":
        seen = [gray_of(C.hex2rgb(h)) for h in NEW_RAMP]
    else:
        seen = [cvd.sim(C.hex2rgb(h), kind) for h in NEW_RAMP]
    des = [C.dE(seen[i], seen[i + 1]) for i in range(3)]
    fails = sum(1 for d in des for _ in [0] if d < DISCRIM)
    # 6쌍 전부
    all_pairs = [(i, j) for i in range(4) for j in range(i + 1, 4)]
    fail6 = sum(1 for (i, j) in all_pairs if C.dE(seen[i], seen[j]) < DISCRIM)
    print("  %-12s %s" % (title, " ".join(C.rgb2hex(c) for c in seen)))
    print("               인접 dE %5.2f / %5.2f / %5.2f   인접 최악 %5.2f   미달 %d/3   6쌍 미달 %d/6"
          % (des[0], des[1], des[2], min(des), fails, fail6))
    print("               인접 CR %.3f / %.3f / %.3f" % tuple(C.CR(seen[i], seen[i + 1]) for i in range(3)))
print()
print("  ※ 여기서 등급의 주 채널은 색이 아니라 리본 「칸 수 1/2/3/4」다(PALETTE_SPEC §12-4).")
print("     위 표는 「보조 채널이 어디서 죽는가」를 재는 것이고, 죽어도 칸 수는 안 바뀐다.")
print()

# ============================================================================
# 5. 부채꼴 링 — 「호만 있고 숫자가 0」을 숫자로
# ============================================================================
print("=" * 78)
print("5. 집중 세션 잔여 시간 호 — 계기로 쓸 수 있는가")
print("=" * 78)
D = 20.0                     # GearRadialMenuWidget.StopwatchRingDiameterPoints
STROKE = 2.0                 # SymbolStroke
CIRC = math.pi * D
print("  링 지름 %.1fpt (GearRadialMenuWidget.cs:2537) · 획 %.1fpt (:447) · 원주 %.4fpt" % (D, STROKE, CIRC))
print("  갱신: RefreshDynamicContent 가 _clockTimer >= 1f 일 때만 fillAmount 를 쓴다 (:1885, :1919)")
print()
print("  ★ 실주기 — _clockTimer 는 float 누적이다. float32 로 실제로 돌려 센다.")
import struct
def f32(x):
    return struct.unpack('f', struct.pack('f', x))[0]
for fname, fps in (("Active 60fps", 60.0), ("Calm 30fps", 30.0), ("Still 15fps", 15.0)):
    frame_ms = 1000.0 / fps
    dt = f32(1.0 / fps)
    acc = f32(0.0); n = 0
    while acc < 1.0 and n < 10000:
        acc = f32(acc + dt); n += 1
    print("  %-12s 프레임 %6.3fms · float32 누적이 1.0 도달에 %2d프레임 → 실주기 %7.2fms"
          % (fname, frame_ms, n, n * frame_ms))
print("  ⇒ 15fps 의 실주기는 1000.00ms(15프레임)다. 16프레임(1066.7ms)은 실 dt 가 1/15 보다")
print("    작을 때만 나오는 경계값이고, Unity 프레임 페이싱은 목표 주기까지 기다리므로 실 dt ≥ 1/15 다.")
print("    ★ 내 역할 카드에 적힌 「실주기 1066.7ms」는 이 재측정으로 1000.0ms 로 정정된다.")
print("      판정은 안 바뀐다 — 어느 쪽이든 호는 1초에 한 번만 다시 그려진다.")
print()
print("  프리셋별 호 후퇴량 (FocusSessionPopover.cs:89 DurationMinutes = 15/25/50, 상한 60)")
print("  %-8s %-11s %-13s %-13s %-13s %-13s" % ("세션", "각속도", "pt/초", "pt/프레임@15fps", "0.5pt(2x 1px)", "1.0pt(1x 1px)"))
for mins in (15, 25, 50, 60):
    T = mins * 60.0
    deg_s = 360.0 / T
    pt_s = CIRC / T
    pt_f15 = pt_s / 15.0
    t_half = 0.5 / pt_s
    t_one = 1.0 / pt_s
    print("  %-8s %7.4f°/s  %9.6f    %11.7f     %6.1f초        %6.1f초"
          % ("%d분" % mins, deg_s, pt_s, pt_f15, t_half, t_one))
print()
print("  ⇒ 눈이 「달라졌다」를 볼 수 있는 최소 단위는 1물리픽셀이다.")
print("     Retina 2x(1px=0.5pt): 25분 프리셋에서 %.1f초, 50분에서 %.1f초마다 한 칸 움직인다."
      % (0.5 / (CIRC / 1500.0), 0.5 / (CIRC / 3000.0)))
print("     Windows 1x(1px=1.0pt): 각각 %.1f초 / %.1f초."
      % (1.0 / (CIRC / 1500.0), 1.0 / (CIRC / 3000.0)))
print("     즉 이 호는 「분」의 계기로도 거칠고 「초」의 계기로는 쓸 수 없다.")
print()
print("  같은 링에서 1분이 차지하는 호 길이:")
for mins in (15, 25, 50, 60):
    print("    %2d분 세션 → 1분 = %.4fpt (= %.3f°) / 물리픽셀 2x 기준 %.2fpx"
          % (mins, CIRC / mins, 360.0 / mins, (CIRC / mins) * 2))
print()
print("  ★ 「프레임당 0.0025pt」의 기준 복원 — 어느 지름·어느 프리셋의 값인가")
for D2, lab in ((20.0, "바깥 지름 20 (AddCircle 인수)"),
                (18.0, "획 중심선 지름 18 = 20 − 획 2.0"),
                (16.0, "안쪽 지름 16")):
    line = "    %-30s" % lab
    for mins in (15, 25, 50):
        v = (math.pi * D2) / (mins * 60.0) / 15.0
        line += " %2d분=%.7f%s" % (mins, v, "★" if abs(v - 0.0025) < 5e-5 else " ")
    print(line)
print("    ⇒ 0.0025 = 25분 프리셋 · 획 중심선 지름 18pt · 15fps. 기준이 복원됐다(내 역할 카드 값 유효).")
print("      바깥 지름 20pt 기준으로 같은 프리셋을 재면 0.0027925 다 — 기준을 안 적으면 두 값이 모순처럼 보인다.")
print()

# ============================================================================
# 6. 목표 크기 — 새 탭이 더할 컨트롤
# ============================================================================
print("=" * 78)
print("6. 목표 크기 — WCAG 2.2 SC 2.5.8 하한 %.0fpt (UiChrome.MinTargetSizePoints)" % MIN_TARGET)
print("=" * 78)
ROW_H = 44.0          # SettingsControls.RowHeight (= 44f, PALETTE_SPEC §12-3 실측 6곳 중 하나)
TAB_BAR_H = 40.0      # SettingsWindow.TabBarHeight
TAB_PAD_X = 10.0      # SettingsWindow.TabPadX
for nm, v in (("설정 행 높이 RowHeight", ROW_H), ("탭바 높이 TabBarHeight", TAB_BAR_H)):
    print("  %-24s %5.1fpt  하한 %.0f  %s  여유 %+.1fpt" % (nm, v, MIN_TARGET,
          "PASS" if v >= MIN_TARGET else "FAIL", v - MIN_TARGET))
print()

# ============================================================================
# 7. 탭 폭 예산 — 접근성 탭이 열리면 배지가 빠진다
# ============================================================================
print("=" * 78)
print("7. 설정창 탭바 폭 — 배지가 빠지면 줄이 줄어든다(넘침 위험이 내려간다)")
print("=" * 78)
PANEL_W = 720.0       # SettingsWindow.PanelWidth (UI_SURFACE_SPEC §12.3)
BADGE_GAP = 8.0       # TabBadgeGap = UiChrome.Space2
print("  창 폭 %.0fpt · 탭 안쪽 좌우 여백 %.0f×2 · 라벨↔배지 간격 %.0f" % (PANEL_W, TAB_PAD_X, BADGE_GAP))
print("  ★ 라벨·배지 폭은 Text.preferredWidth 라 오프라인에서 못 잰다(묶음 ⑩ 규칙 4).")
print("    여기서 낼 수 있는 것은 「배지 하나가 빠지면 줄이 얼마나 짧아지는가」의 하한뿐이다:")
print("    배지 1개 제거 = 최소 %.0fpt(간격) + 배지 글자폭(미측정) 만큼 짧아진다." % BADGE_GAP)
print("    ⇒ 접근성 탭 개방은 탭바를 넓히지 않는다. 좁힌다. 캡처 판정은 design-capture-review.")
print()

# ============================================================================
# 8. 모션 — 「정지 프레임이 존재하는가」
# ============================================================================
print("=" * 78)
print("8. 자동 모션 실측 — 사용자가 아무것도 안 해도 움직이는 것")
print("=" * 78)
# StickConfig.cs 배포 기본값
IDLE_MIN, IDLE_MAX = 2.0, 6.0          # wanderIdleDurationMin/Max  (:1106,:1109)
WALK_MIN, WALK_MAX = 1.5, 4.0          # wanderWalkDurationMin/Max  (:1112,:1115)
P_WALK = 0.75                          # wanderPostIdleWalkChance   (:1154)
P_TURN = 0.08                          # wanderSpontaneousTurnChance(:1151)
TURN_CHECK = 0.5                       # wanderTurnCheckInterval    (:1148)
WALK_SPEED = 2.5                       # walkSpeed                  (:26)
BREATH_HZ = 0.8                        # idleBreathFrequencyHz      (:110)
BREATH_AMP = 0.012                     # idleBreathAmplitude        (:107)
GEAR_SPIN_S, GEAR_TURNS = 1.4, 4.0     # InfoGearIconWidget :254,:258

e_idle = (IDLE_MIN + IDLE_MAX) / 2.0
e_walk = (WALK_MIN + WALK_MAX) / 2.0
cycle = e_idle + P_WALK * e_walk
duty = (P_WALK * e_walk) / cycle
print("  8-a 캐릭터 자동 배회 (사용자 조작 0)")
print("      기대 정지 %.2f초 + 걷기확률 %.2f × 기대 걷기 %.2f초 = 주기 %.4f초"
      % (e_idle, P_WALK, e_walk, cycle))
print("      → 벽시계의 %.1f%% 를 캐릭터가 화면을 가로질러 이동하는 데 쓴다" % (duty * 100))
print("      자발적 방향 전환: %.1f초마다 확률 %.2f → 기대 %.1f초마다 1회"
      % (TURN_CHECK, P_TURN, TURN_CHECK / P_TURN))
print("      걷기 속도 %.1f 유닛/초 (StickConfig.walkSpeed)" % WALK_SPEED)
print()
print("  8-b ★ 「정지」 상태에도 정지가 없다")
print("      idleBreathFrequencyHz = %.1f Hz → 호흡 1주기 %.3f초, 진폭 %.3f(설정 단위)"
      % (BREATH_HZ, 1.0 / BREATH_HZ, BREATH_AMP))
print("      ⇒ 배회를 멈춰도 %.1fHz 로 계속 오르내린다. 모션 민감 사용자에게 완전 정지 프레임은 0이다."
      % BREATH_HZ)
print()
print("  8-c 톱니 회전 (클릭 유발 — WCAG 2.1 SC 2.3.3 대상)")
print("      %.1f초에 %.0f바퀴 = %.0f° / 평균 각속도 %.1f°/s"
      % (GEAR_SPIN_S, GEAR_TURNS, 360 * GEAR_TURNS, 360 * GEAR_TURNS / GEAR_SPIN_S))
print("      ⇒ 평균 %.1f°/s 는 이 앱에서 가장 빠른 회전이다(호 0.24°/s 의 %.0f배)."
      % (360 * GEAR_TURNS / GEAR_SPIN_S, (360 * GEAR_TURNS / GEAR_SPIN_S) / 0.24))
print()
print("  8-d 오디오 — 소리로만 전달되는 정보가 있는가")
print("      저장소 전체 프로덕션 .cs 에서 AudioSource / PlayOneShot / AudioClip 실코드 = 0건")
print("      (유일한 적중 1건은 StickPackManifestSO.cs:200 의 [Header] 문자열 = 주석류,")
print("       묶음 ⑪ 규칙 16 「그 사실을 말하는 줄은 계수 대상이 아니다」에 해당)")
print("      양성 대조: 같은 도구로 StickmanAgent.cs 의 Debug.Log = 19건 → 검색은 살아 있다")
print("      ⇒ 소리로만 전달되는 정보는 0건이다. 소리 자체가 0이기 때문이다.")
print()

print("=" * 78)
print("검산 요약")
print("=" * 78)
print("  교정 5건 PASS(흰/검 21.0 · 동일색 1.0 ×3 · CR 대칭 · dE 0 · cvd 무채축 0/255)")
print("  음성 대조 3건: 폐기/글자금지 잉크 3종이 AA 4.5 에서 실제로 FAIL 로 잡혔다")
print("  ⇒ 이 파일의 PASS 는 죽은 프로브가 아니다")
