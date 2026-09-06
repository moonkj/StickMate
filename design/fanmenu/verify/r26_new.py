"""R26 — 부채꼴 심볼 5종 새 좌표(제안) + 게이트.

규칙 FG-1 ~ FG-7 은 docs/DESIGN_FAN_MENU_ICONS.md 와 같은 값이다.
"""
import math
from fanglyph import *

W = SYMBOL_STROKE          # 2.0pt = 펜 하나
G0, G1, G2 = 1.00 * W, 0.75 * W, 1.50 * W      # 2.00 / 1.50 / 3.00
FIELD_R = 14.0             # 광학 필드 반경(권고)
FIELD_R_HARD = 15.0        # 단일 돌출 절대 상한
GAP_MIN = 1.5 * W          # 3.00pt — 「떨어졌다」의 하한
FILL_MIN = 1.5 * W         # 3.00pt — 채움 덩어리/구멍의 최소 폭


def PL(name, pts, t):
    return Piece(name, "poly", t, points=pts)


def CAP(name, L, t, ang, cx, cy):
    return P(name, L, t, ang, cx, cy)


def DISC(name, d, cx, cy):
    return Piece(name, "disc", 0.0, diameter=d, center=(cx, cy))


# ───────────────────────── ① 집중 — 스톱워치 ─────────────────────────
def new_stopwatch():
    return [
        PL("CrownStem", [(0.0, 9.2), (0.0, 11.6)], G0),             # 링 코어 안(9.2)에서 시작 = 용접
        CAP("CrownCap", 6.0, G2, 0.0, 0.0, 12.4),                   # 누름 단추(g2) — 가로라 ⑤ 세로획과 갈린다
        Piece("Ring", "ring", G0, diameter=20.0),                   # RingTrack/RingFill 계약 — 불변
        PL("MinuteHand", [(0.0, 0.0), (0.0, 4.0)], G0),
        PL("HourHand", [(0.0, 0.0), (2.598, -1.500)], G0),
    ]


# ───────────────────────── ② 캐릭터 — 스틱맨 ─────────────────────────
HEAD_D = 7.0
SHOULDER_Y = 3.5
PELVIS_Y = -4.5
ARM_LEN = 7.0
ARM_DROP_DEG = 30.0
LEG_LEN = 6.0
LEG_SPREAD_DEG = 25.0


def new_stickman():
    ax = ARM_LEN * math.cos(math.radians(ARM_DROP_DEG))
    ay = SHOULDER_Y - ARM_LEN * math.sin(math.radians(ARM_DROP_DEG))
    lx = LEG_LEN * math.sin(math.radians(LEG_SPREAD_DEG))
    ly = PELVIS_Y - LEG_LEN * math.cos(math.radians(LEG_SPREAD_DEG))
    return [
        DISC("IconHead", HEAD_D, 0.0, 8.0),                          # ★ 링 → 채운 원반(본체와 같은 문법)
        PL("IconSpine", [(0.0, 4.5), (0.0, PELVIS_Y)], G0),
        PL("IconArmL", [(0.0, SHOULDER_Y), (-ax, ay)], G0),
        PL("IconArmR", [(0.0, SHOULDER_Y), (ax, ay)], G0),
        PL("IconLegL", [(0.0, PELVIS_Y), (-lx, ly)], G0),
        PL("IconLegR", [(0.0, PELVIS_Y), (lx, ly)], G0),
    ]


# ───────────────────────── ③ 할일 — 체크리스트 2행 ─────────────────────────
ROW_Y = 5.0
MARK_X = -7.75
MARK_CELL = 5.0    # 꺾은선 경로 한 변(획은 경로 위 중앙) → 바깥 6.5 · 구멍 3.5
LINE_X0, LINE_X1 = -0.5, 9.5


BOX = [(MARK_X - MARK_CELL / 2, ROW_Y - MARK_CELL / 2),
       (MARK_X + MARK_CELL / 2, ROW_Y - MARK_CELL / 2),
       (MARK_X + MARK_CELL / 2, ROW_Y + MARK_CELL / 2),
       (MARK_X - MARK_CELL / 2, ROW_Y + MARK_CELL / 2),
       (MARK_X - MARK_CELL / 2, ROW_Y - MARK_CELL / 2)]


def new_checklist():
    return [
        PL("Box", BOX, G1),          # 닫힌 꺾은선 — rectoutline 스프라이트(두께가 정수 pt 뿐)를 쓰지 않는다
        PL("Line0", [(LINE_X0, ROW_Y), (LINE_X1, ROW_Y)], G0),
        PL("Check", [(-9.6, -4.8), (-7.8, -6.6), (-5.6, -2.8)], G0),   # Accent 고정 · 1조각
        PL("Line1", [(LINE_X0, -ROW_Y), (LINE_X1, -ROW_Y)], G0),
    ]


NEW_ACCENT = {"③ 할일(체크리스트)": ("Check",)}


# ───────────────────────── ④ 행동 명령 — 지휘봉 ─────────────────────────
def _arc(name, cx, cy, r, a0, a1, t, seg=8):
    pts = []
    for i in range(seg + 1):
        a = math.radians(a0 + (a1 - a0) * i / seg)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return PL(name, pts, t)


HORN = [(-8.4, 2.6), (-8.4, -2.6), (5.0, -5.8), (5.0, 5.8), (-8.4, 2.6)]
HANDLE = [(-1.0, -4.37), (-2.2, -9.2)]
WAVE_R, WAVE_SPAN = 6.6, 38.0


def new_megaphone():
    """확성기 — 나팔을 <닫힌 한 조각>으로(현행은 낱획 4개가 0.43pt 간극으로 서로 붙어 있었다) +
    용접된 손잡이(현행은 <일부러 뺐다> — 그 결과 스피커/볼륨 아이콘과 같은 실루엣이 됐다) +
    소리선 1개(현행 2개는 입과 0.53pt 골로 붙는다).
    ★ 지휘봉(design-art F-L2) 후보 5안은 이 크기에서 전부 다른 물건으로 읽혔다 — r26_slot4.png."""
    return [
        PL("Horn", HORN, G0),
        PL("Handle", HANDLE, G0),
        _arc("Wave", 5.0, 0.0, WAVE_R, -WAVE_SPAN, WAVE_SPAN, G0),
    ]


# ───────────────────────── ⑤ 종료 — 전원 ─────────────────────────
NEW_POWER_GAP_DEG = 62.0
NEW_POWER_RING_D = 22.0        # 20 → 22: ①과의 실루엣 포함 관계를 끊는 유일한 상수


def new_power():
    return [
        Piece("PowerRing", "ring", G0, diameter=NEW_POWER_RING_D,
              gap_deg=NEW_POWER_GAP_DEG, gap_center_deg=90.0),
        PL("PowerStem", [(0.0, 1.0), (0.0, NEW_POWER_RING_D / 2)], G0),
    ]


NEW = {
    "① 집중(스톱워치)": new_stopwatch,
    "② 캐릭터(스틱맨)": new_stickman,
    "③ 할일(체크리스트)": new_checklist,
    "④ 행동명령(확성기)": new_megaphone,
    "⑤ 종료(전원)": new_power,
}
