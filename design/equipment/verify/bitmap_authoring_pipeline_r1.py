# -*- coding: utf-8 -*-
"""
착용 비트맵 저작 후처리 파이프라인 v1  (coder / 2026-09-09)

「AI가 만든 광택 3D 렌더 한 장」 →  「화법 조건을 통과하는 앞판/뒤판 2장」.

이 파일이 하는 일 (전부 **이미지 하나에 맞춘 상수 없이** 데이터에서 유도한다):

  1. 알파 이진화       — 저작 규격 §19-8-2 «실루엣 밖 0 / 안 255 / 중간 금지».
  2. 원치 않는 몸 제거 — AI가 요청하지 않은 «머리 구체»를 같이 그린다.
                         행별 평균 **채도** 프로파일의 급락 지점을 자동으로 찾고,
                         채도 히스토그램의 **골짜기**를 임계로 삼아 저채도 덩어리를 떼어낸다.
                         ★ y 좌표 하드코딩 없음 — 다른 아이템에도 그대로 쓴다.
  3. 헤이즈 인페인트   — AI가 얹은 반투명 하이라이트 구름은 채도가 낮아 2에서 구멍이 된다.
                         구멍은 «가장 가까운 유효 화소»로 메운다(색이 밖에서 새어 들지 않게).
  4. 기하 배치         — 규격서 §5-1의 목표 잉크 박스에 **균등 배율**로 앉힌다(세로만 늘리지 않는다).
  5. 팔레트 스냅       — ★ RGB 최근접이 아니라
                         (a) **색상(hue)** 으로 보석(M2)을 먼저 떼고
                         (b) 나머지를 **휘도 퍼센타일**로 W/M/MD/SH 에 배분한다.
                         RGB 최근접은 원본의 밝기 분포를 그대로 물려받아 목표 배분을 못 맞춘다
                         (실측: W 목표 21% → 68%).
  6. 좌우 대칭         — C-5(광원 위·좌우 대칭). 엔진이 flipX 하므로 비대칭 조명은 걸을 때 뒤집힌다.
  7. 앞/뒤 2겹 분리    — 머리 원반(반지름 1.17193 R)의 **경계를 가로지르는 어두운 안쪽 면**을 뒤판으로
                         보내고, 그 아래를 겹침 0.20 R 만큼 채워 내린다(규격서 §4-5).
  8. 검산             — 잉크박스 판정식 · 알파 이진성 · 좌우 조명 대칭 · 색 배분(목표 대비)을 인쇄한다.

돌리는 법:
    python3 design/equipment/verify/bitmap_authoring_pipeline_r1.py            # 왕관 파일럿
    python3 design/equipment/verify/bitmap_authoring_pipeline_r1.py --help
"""
import argparse, json, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

# ─────────────────────────────────────────────────────────────────────────────
# 0. 규격 상수 — 출처를 주석에 남긴다(숫자를 지어내지 않았다)
# ─────────────────────────────────────────────────────────────────────────────
AUTHOR_PX  = 1024                 # 저작 캔버스 한 변 (§19 확정)
K          = 180.0                # 캔버스 A(HEAD/EYES/HAIR) px per R (규격서 §2)
ANCHOR     = (512.0, 512.0)       # 머리 중심의 캔버스 좌표 (규격서 §2)
HEAD_VIS_R = 1.17193              # 머리의 **보이는** 바깥 반경 (규격서 §1-3, 프리팹 역산)
OVERLAP_R  = 0.20                 # 앞/뒤 판 세로 겹침 (규격서 §4-2 규칙 3)

# ─────────────────────────────────────────────────────────────────────────────
# 0-b. 카드 파생본 규격 — 규격서 §7-2 «잉크 박스로 크롭해 다시 굽는 파생본» 을 이행한다
# ─────────────────────────────────────────────────────────────────────────────
# ★ 왜 착용 PNG를 카드에 그대로 못 쓰는가 (규격서 §7-2 원문):
#     «왕관 잉크는 출하 256² 캔버스에서 118 텍셀인데 카드는 72pt(3× 폰에서 216px)다 → 1.8배 업샘플».
#   같은 그림에서 굽되 **카드 프레임에 맞춰 다시 크롭·리샘플**하면 그 업샘플이 사라지고,
#   «카드와 착용이 다른 물건으로 보인다»가 원리적으로 불가능해진다(같은 원본, 같은 팔레트).
#
# ★★ 카드 캔버스는 «적당한 여백»이 아니라 **옛 벡터 카드 프레임 그 자체**다.
#   그래서 이 파생본은 <b>제자리 교체</b>가 된다 — 왕관이 카드에서 그려지던 크기도 자리도
#   한 픽셀 안 움직이고 **화법만** 바뀐다. 한 번에 한 변수만 바꿔야 사용자가 판정할 수 있다.
#   아래 숫자는 전부 프로덕션에서 옮겨 온 것이고 출처를 한 줄씩 적었다. 베낀 값이 썩는 것을
#   막는 것은 이 주석이 아니라 EditMode 테스트다(PackCardBitmapIconTests — 같은 값을
#   프로덕션 코드에서 <b>직접 읽어</b> 이 PNG의 실측 잉크 박스와 대조한다).
CARD_PX = 256                     # Assets/Editor/PackCardIconImport.MaxTextureSize
                                  #   (근거: 카드 실그리기 72pt × Retina 2배 = 144 물리픽셀 이상인 첫 2의 거듭제곱)
_ICON_VIEWBOX  = 64.0             # AccessoryCardIcon.Frame.IconViewBox
_STAGE_VIEW_W  = 200.0            # AccessoryCardIcon.Frame.StageViewBoxWidth
_STAGE_HEAD_R  = 28.0             # AccessoryCardIcon.Frame.StageHeadRadius
_STAGE_HEAD_CY = 46.0             # AccessoryCardIcon.Frame.StageHeadCenterY
_STAGE_RENDER_W= 158.0            # AccessoryCardIcon.Frame.StageRenderWidth
_STAGE_TOP_PX  = 26.0             # AccessoryCardIcon.Frame.StageTopPx
CARD_SLOT_BOX = {                 # AccessoryCardIcon.Frame.TryGet 의 switch (box, top)
    "Head":      (70.0, 6.0),
    "Eyes":      (48.0, 38.0),
    "Neck":      (54.0, 78.0),
    "Shoulders": (88.0, 72.0),
}
VECTOR_ICON_PT = 58.0             # CharacterInfoWindow.IconSize      — 벡터 카드 아이콘 변
BITMAP_ICON_PT = 72.0             # CharacterInfoWindow.BitmapIconSize (= ThumbHeight 78 − BitmapIconInset 6)

# ★★ 배경 처리 — 리더 지시(«DLC 팩처럼 #15181E 불투명 배경을 깔아라»)에서 **실측으로 벗어난 자리**다.
#
#   지시의 근거는 CharacterInfoWindow.BuildBitmapCardArt 의 계약 «이미지 자체가 배경까지 갖고 온다»이고,
#   그 계약 자체는 맞다(UI 는 판을 더 깔지 않는다). 다만 **판 색이 상태마다 다르다**:
#
#       카드 썸네일 · 보유    UiChrome.CardSurfaceMuted                      #15181E
#       카드 썸네일 · 착용중  Flatten(AccentSurface, CardSurface)            #333129   ← 왕관은 지금 이 상태다
#       카드 썸네일 · 잠김    UiChrome.ThumbSurfaceLocked                    #101318
#       상세 패널 썸네일      UiChrome.CardSurface (보유) / ThumbSurfaceLocked(잠김)  #1B1F26
#
#   불투명 #15181E 는 이 넷 중 **첫 줄에서만** 이음매가 사라지고 나머지 셋에서는 네모 타일이 된다.
#   실측 대조 시트: design/equipment/pack_bitmap_pilot_crown/card_bg_compare.png (Retina 2× 실크기).
#   그래서 기본값을 «알파 보존»으로 둔다 — 네 상태 모두에서 이음매가 0이다.
#   지시대로 되돌리려면 인자 하나다:  --card-bg '#15181E'
#
#   ※ 팩 12종이 불투명인 것은 «그렇게 하기로 정해서»가 아니라 원본 AI 렌더가 비네트를 칠해 왔기
#     때문이고, 그 결과 mine 팩은 카드 안에 남색 타일이 보인다(BuildBitmapCardArt 문서가 실측으로
#     인정한 그 증상이다). 왕관은 우리가 굽는 그림이라 그 증상을 물려받을 이유가 없다.
CARD_BG_MUTED = None


def card_frame(slot):
    """옛 벡터 카드 프레임을 **R 단위 창**으로 되돌린다.

    AccessoryCardIcon.BuildFramed 가 쓰는 식을 그대로 옮긴 것이다(축소 shrink=1 인 경우):
        pxPerR = unitsPerR × (IconSize / IconViewBox)
    비트맵 카드는 상자가 IconSize(58)가 아니라 BitmapIconSize(72)이므로, **같은 pxPerR** 을
    유지하려면 창이 72/pxPerR R 만큼 넓어야 한다. 그러면 그 창에 그린 그림은 옛 벡터와
    같은 배율·같은 자리에 놓인다.

    반환: (span_r, center_y_r) — 캔버스 한 변이 몇 R 인가 / 캔버스 중심의 y(머리 중심 기준 R).
    """
    box, top = CARD_SLOT_BOX[slot]
    px_per_stage_unit = _STAGE_RENDER_W / _STAGE_VIEW_W
    head_radius_px = _STAGE_HEAD_R * px_per_stage_unit
    head_center_px = _STAGE_TOP_PX + _STAGE_HEAD_CY * px_per_stage_unit
    u = box / _ICON_VIEWBOX
    units_per_r = head_radius_px / u
    center_y_r = (head_center_px - top - _ICON_VIEWBOX * 0.5 * u) / head_radius_px
    px_per_r = units_per_r * (VECTOR_ICON_PT / _ICON_VIEWBOX)     # 벡터 카드의 pt/R
    span_r = BITMAP_ICON_PT / px_per_r
    return span_r, center_y_r, px_per_r

def px_of(x_r, y_r):
    return (ANCHOR[0] + x_r * K, ANCHOR[1] - y_r * K)

def r_of(px, py):
    return ((px - ANCHOR[0]) / K, (ANCHOR[1] - py) / K)


class Item:
    """아이템 하나의 저작 파라미터. 새 아이템은 여기 한 줄만 늘어난다."""
    def __init__(s, key, src, out_front, out_back, ink_box_r, palette, mix, back_wall,
                 band_top_r, back_bottom_r, smooth_px, min_patch_px, open_reach_px,
                 out_card, card_slot):
        s.key, s.src = key, src
        s.out_front, s.out_back = out_front, out_back
        s.out_card = out_card        # 카드 표면(cardIconOverride)이 읽는 PNG
        s.card_slot = card_slot      # 카드 프레임을 고르는 슬롯 이름 (CARD_SLOT_BOX 의 키)
        s.ink = ink_box_r          # (minX, maxX, minY, maxY) 목표 — 규격서 §5-1
        s.palette = palette        # 이름 -> hex (밝은 순으로 적지 않아도 된다, 코드가 정렬한다)
        s.mix = mix                # 이름 -> 목표 면적률(합 1.0)
        s.back_wall = back_wall    # 뒤판 후보 톤 이름들
        s.band_top_r = band_top_r        # 앞판 밴드 윗선 (규격서 §5-1)
        s.back_bottom_r = back_bottom_r  # 뒤판 아래끝 = 밴드 윗선 − 겹침 0.20 R (규격서 §5-1)
        # ★ 원본이 AI 렌더라 «그런지 텍스처»가 얹혀 있다. 그것을 그대로 양자화하면 5색이
        #   표범 무늬로 섞여 몸(45 텍셀/R)에서 회색 노이즈가 된다(C-6/C-7이 잡는 그 증상).
        s.smooth_px = smooth_px          # 휘도 중앙값 필터 창(캔버스 1024² 기준 px)
        s.min_patch_px = min_patch_px    # 이보다 작은 조각은 이웃 톤에 흡수
        s.open_reach_px = open_reach_px  # «위가 열려 있다»를 몇 px 까지 인정할 것인가(§4-4 판정)


CROWN = Item(
    key="equip_head_crown",
    src="design/equipment/pack_bitmap_pilot_crown/crown_source_attempt2.png",
    out_front="Assets/_Project/Art/WornSprites/equip_head_crown.png",
    out_back="Assets/_Project/Art/WornSprites/equip_head_crown_back.png",
    # ★★ 2026-09-09 R5 — 사용자 신고 «머리사이즈와 왕관사이즈가 안맞음» 으로 <b>다시 잰 값</b>.
    #
    #   규격서 §5-1의 원래 값은 x ±1.310 R · y +0.280 .. +2.380 R 이었다. 배선은 정상이었다
    #   (실측: 렌더러가 캔버스 rect 5.688889 R ↔ 1024 px = 180 px/R 로 정확히 놓고,
    #    잉크는 선언대로 ±1.3111 R 에 떨어진다 — 실기 픽셀 36 px / 머리 32 px = 1.125,
    #    규격 계산 1.1188 과 0.5% 안에서 일치). <b>버그가 아니라 목표치 자체가 컸다.</b>
    #
    #   무엇이 바뀌었는지는 «색이 칠해진 덩어리»를 재면 보인다(실기 캡처 실측):
    #     · 옛 벡터 왕관 : 금색 본체 폭 = 머리 보이는 지름 × <b>1.0009</b>
    #                      (실루엣 전체는 ×1.1326 이지만 그 차이는 <b>잉크색 윤곽선</b>이라
    #                       검은 머리와 시각적으로 하나로 붙어 «크다»고 읽히지 않는다)
    #     · 새 비트맵    : 윤곽선이 없어 실루엣 = 금색 = 머리 × <b>1.1250</b>
    #   즉 «칠해진 왕관»이 머리 대비 12.4% 커졌다. 그것이 사용자가 본 것이다.
    #
    #   그래서 목표를 <b>잉크 반폭 = 머리의 보이는 반경</b>으로 잡는다(= 사용자가 받아들였던
    #   벡터 왕관의 색 본체 비율 1.0009 와 0.1% 안에서 같다). 균등 배율이므로 높이도 함께
    #   ×0.8947 되어 머리 대비 0.8125 → 0.727 (벡터 0.777 보다 조금 낮다).
    #     반폭   = HEAD_VIS_R = 1.17193 R          (폭 2.34386 R)
    #     밑선   = +0.34444 R  <b>그대로 둔다</b>  (밴드가 머리에 얹힌 깊이를 안 바꾼다)
    #     높이   = 2.34386 / 1.32959(원본 잉크 가로세로비) = 1.76288 R → 위끝 +2.10732 R
    #   상자 높이에 여유(1.86)를 줘서 place() 의 min() 이 <b>가로</b>로 결정되게 하고,
    #   상자 중심 y = 0.34444 + 1.76288/2 = 1.22588 로 두면 세로 가운데 맞춤이 밑선을 지킨다.
    #   목업 대조: design/equipment/pack_bitmap_pilot_crown/crown_width_mock.png
    ink_box_r=(-HEAD_VIS_R, +HEAD_VIS_R, 1.22588 - 0.93, 1.22588 + 0.93),
    # design-art §6-2 확정 5색
    palette={"W": "#BB8E1C", "M": "#9B7922", "MD": "#604B15", "SH": "#2B220A", "M2": "#C6443C"},
    # design-art §6-3 권고 B
    mix={"W": 0.21, "M": 0.58, "MD": 0.12, "SH": 0.02, "M2": 0.07},
    back_wall=("MD", "SH"),
    # ★ 밴드 윗선은 <b>그림의 특징</b>이라 그림과 같은 비율로 함께 줄어든다.
    #   옛 값 +0.840 은 잉크 밑선(+0.34444)에서 0.49556 R 위 = 잉크 높이(1.9722)의 25.13%.
    #   새 잉크 높이 1.76288 × 0.2513 = 0.44300 → +0.34444 + 0.44300 = +0.78744.
    band_top_r=+0.78744,    # §5-1 «앞판 밴드 윗선» (R5 재유도)
    back_bottom_r=+0.58744, # §5-1 «뒤판 아래끝» (= 밴드 윗선 − 겹침 0.200 R, 겹침은 절대값이라 안 줄인다)
    smooth_px=9, min_patch_px=260,
    # 실측(1024² 캔버스): 안쪽 면 4조각은 위쪽 배경까지 14~17 px, 구슬 어두운 면은 40 px.
    open_reach_px=30,
    out_card="Assets/_Project/Resources/Items/Icons/equip_head_crown.png",
    card_slot="Head",
)

ITEMS = {"crown": CROWN}


# ─────────────────────────────────────────────────────────────────────────────
# 1. 색 공간 도우미
# ─────────────────────────────────────────────────────────────────────────────
def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i+2], 16) for i in (0, 2, 4)], dtype=float)

def hsv_parts(rgb01):
    mx = rgb01.max(axis=-1); mn = rgb01.min(axis=-1); d = mx - mn
    sat = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0.0)
    dd = np.maximum(d, 1e-6)
    r, g, b = rgb01[..., 0], rgb01[..., 1], rgb01[..., 2]
    hue = np.where(mx == r, ((g - b) / dd) % 6,
          np.where(mx == g, (b - r) / dd + 2, (r - g) / dd + 4)) * 60.0
    hue = np.where(d < 1e-6, 0.0, hue)
    return hue, sat, mx

def luma(rgb01):
    """Rec.709 상대휘도. 배분의 **순서**만 쓰므로 모델 선택이 결과를 바꾸지 않는다."""
    return rgb01 @ np.array([0.2126, 0.7152, 0.0722])

def hue_dist(hue, target):
    d = np.abs((hue - target + 180.0) % 360.0 - 180.0)
    return d


# ─────────────────────────────────────────────────────────────────────────────
# 2. 알파 이진화
# ─────────────────────────────────────────────────────────────────────────────
def binarize_alpha(a, thr=128, log=print):
    al = a[..., 3]
    mid = int(((al > 7) & (al < 248)).sum()); op = int((al >= 248).sum())
    log(f"  [알파] 원본: 불투명(≥248) {op}  중간(8~247) {mid} = {mid/max(op,1)*100:.1f}%  "
        f"→ 임계 {thr}로 이진화한다(§19-8-2)")
    m = al >= thr
    # 글로우 잔여물(1~2px 점)은 열기 연산으로 떨군다. 3×3 한 번이면 «점»만 사라지고 형태는 남는다.
    m = ndimage.binary_opening(m, np.ones((3, 3)))
    return m


# ─────────────────────────────────────────────────────────────────────────────
# 3. 원치 않는 몸(머리 구체) 제거 — 채도 프로파일 자동 탐지
# ─────────────────────────────────────────────────────────────────────────────
def saturation_valley(sat, mask, log=print):
    """채도 히스토그램의 **두 봉우리 사이 골짜기**. 두 봉우리가 없으면 None(= 제거 안 함)."""
    h, e = np.histogram(sat[mask], bins=100, range=(0, 1))
    ctr = (e[:-1] + e[1:]) / 2
    sm = ndimage.uniform_filter1d(h.astype(float), 5)
    # 봉우리 = 전체의 5% 이상을 담는 국소 최대
    peaks = [i for i in range(1, 99)
             if sm[i] >= sm[i-1] and sm[i] >= sm[i+1] and h[max(0,i-2):i+3].sum() > 0.05*mask.sum()]
    if len(peaks) < 2:
        log("  [몸제거] 채도 봉우리가 하나뿐이다 — 떼어낼 저채도 덩어리가 없다고 본다.")
        return None
    lo, hi = peaks[0], peaks[-1]
    v = lo + int(np.argmin(sm[lo:hi+1]))
    log(f"  [몸제거] 채도 봉우리 {ctr[lo]:.2f} / {ctr[hi]:.2f} · 골짜기 **{ctr[v]:.2f}** "
        f"(골 화소 {h[v]}개 = 전체의 {100*h[v]/mask.sum():.3f}%)")
    return float(ctr[v])

def find_saturation_drop_row(sat, mask, log=print, rel_start=0.30, slope=-0.004):
    """행별 평균 채도가 **급락**하는 첫 행. 이미지 전용 상수를 쓰지 않기 위한 자리."""
    n = mask.sum(axis=1)
    prof = np.array([sat[y][mask[y]].mean() if n[y] > 20 else np.nan for y in range(mask.shape[0])])
    idx = np.nonzero(~np.isnan(prof))[0]
    if idx.size < 20: return None
    sm = prof.copy(); sm[idx] = ndimage.uniform_filter1d(prof[idx], 9)
    d = np.full_like(prof, np.nan); d[idx[1:]] = np.diff(sm[idx])
    y0, y1 = idx.min(), idx.max()
    for y in idx:
        if y > y0 + rel_start * (y1 - y0) and d[y] < slope:
            log(f"  [몸제거] 채도 급락 첫 행 py={y}  (변화율 {d[y]:+.4f}/행 < {slope})  "
                f"위쪽 평균 {np.nanmean(prof[y0:y]):.3f} → 아래쪽 평균 {np.nanmean(prof[y:y1]):.3f}")
            return int(y)
    log("  [몸제거] 급락 행 없음 — 몸이 같이 그려지지 않았다고 본다.")
    return None

def strip_unwanted_body(rgb01, mask, log=print):
    hue, sat, val = hsv_parts(rgb01)
    row = find_saturation_drop_row(sat, mask, log)
    if row is None:
        return mask, None
    thr = saturation_valley(sat, mask, log)
    if thr is None:
        return mask, None
    low = mask & (sat < thr)
    # 급락 행 **아래**에서 시작하는 저채도 덩어리만 떼어낸다 —
    # 위쪽의 저채도 하이라이트(헤이즈)는 3에서 인페인트로 처리한다.
    lab, n = ndimage.label(low)
    drop = np.zeros(n + 1, bool)
    for i in range(1, n + 1):
        ys = np.nonzero((lab == i).any(axis=1))[0]
        if ys.size and ys.max() > row and (lab == i).sum() > 0.01 * mask.sum():
            drop[i] = True
    body = drop[lab]
    log(f"  [몸제거] 저채도 덩어리 {n}개 중 {int(drop.sum())}개 제거 = {body.sum()} px "
        f"({100*body.sum()/mask.sum():.1f}% of 실루엣)")
    kept = mask & ~body
    kept = ndimage.binary_opening(kept, np.ones((3, 3)))
    lab, n = ndimage.label(kept)
    if n > 1:
        sizes = ndimage.sum(kept, lab, range(1, n + 1))
        kept = lab == (1 + int(np.argmax(sizes)))
        log(f"  [몸제거] 잔여 조각 {n-1}개를 버리고 가장 큰 덩어리만 남긴다.")
    kept = ndimage.binary_fill_holes(kept)
    return kept, thr


# ─────────────────────────────────────────────────────────────────────────────
# 4. 헤이즈 인페인트 — 저채도 구멍을 «가장 가까운 유효 화소»로 메운다
# ─────────────────────────────────────────────────────────────────────────────
def inpaint_low_saturation(rgb01, mask, thr, log=print):
    if thr is None: return rgb01
    _, sat, _ = hsv_parts(rgb01)
    bad = mask & (sat < thr)
    if not bad.any():
        return rgb01
    src = mask & ~bad
    if not src.any(): return rgb01
    _, ind = ndimage.distance_transform_edt(~src, return_indices=True)
    out = rgb01.copy()
    out[bad] = rgb01[ind[0][bad], ind[1][bad]]
    log(f"  [헤이즈] 실루엣 안 저채도 {bad.sum()} px "
        f"({100*bad.sum()/mask.sum():.1f}%)를 최근접 유효 화소로 메웠다.")
    return out


# ─────────────────────────────────────────────────────────────────────────────
# 5. 기하 배치 — 목표 잉크 박스에 균등 배율
# ─────────────────────────────────────────────────────────────────────────────
def place(rgb01, mask, ink_r, log=print):
    ys, xs = np.nonzero(mask)
    sx0, sx1, sy0, sy1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    sw, sh = sx1 - sx0, sy1 - sy0
    tx0, _ = px_of(ink_r[0], 0); tx1, _ = px_of(ink_r[1], 0)
    _, ty1 = px_of(0, ink_r[2]); _, ty0 = px_of(0, ink_r[3])
    tw, th = tx1 - tx0, ty1 - ty0
    s = min(tw / sw, th / sh)          # ★ 균등 — 세로만 늘리지 않는다
    dw, dh = int(round(sw * s)), int(round(sh * s))
    ox = int(round(tx0 + (tw - dw) / 2.0))
    oy = int(round(ty0 + (th - dh) / 2.0))
    log(f"  [배치] 원본 잉크 {sw}×{sh} (비 {sw/sh:.3f}) → 목표 {tw:.0f}×{th:.0f} (비 {tw/th:.3f}) "
        f"균등 배율 {s:.4f} → {dw}×{dh}, 좌상단 ({ox},{oy})")

    # 알파 프리멀티플로 줄인다 — 안 그러면 실루엣 밖 색이 가장자리로 새어 든다.
    m = mask.astype(np.float64)
    pm = rgb01 * m[..., None]
    def rs(arr):
        cropped = (np.clip(arr, 0, 1) * 255.0 + 0.5).astype(np.uint8)[sy0:sy1, sx0:sx1]
        img = Image.fromarray(cropped)
        return np.asarray(img.resize((dw, dh), Image.LANCZOS)).astype(np.float64) / 255.0
    pm_s = rs(pm); m_s = rs(m)
    rgb_s = np.where(m_s[..., None] > 1e-3, pm_s / np.maximum(m_s[..., None], 1e-3), 0.0)
    mask_s = m_s >= 0.5

    C = np.zeros((AUTHOR_PX, AUTHOR_PX, 3)); Cm = np.zeros((AUTHOR_PX, AUTHOR_PX), bool)
    C[oy:oy+dh, ox:ox+dw] = np.clip(rgb_s, 0, 1)
    Cm[oy:oy+dh, ox:ox+dw] = mask_s
    return C, Cm


# ─────────────────────────────────────────────────────────────────────────────
# 6. 팔레트 스냅 — 색상으로 보석, 휘도 퍼센타일로 나머지
# ─────────────────────────────────────────────────────────────────────────────
def snap_palette(rgb01, mask, item, log=print):
    """반환: 톤 인덱스 맵(-1 = 실루엣 밖), 톤 이름 목록.

    ★ **RGB 최근접 스냅을 쓰지 않는다.** 최근접은 원본의 밝기 분포를 그대로 물려받아
    목표 배분을 못 맞춘다(리더 급조본 실측: W 목표 21% → **68%**).
    대신 두 축을 나눈다 —

      (a) **색상(hue)** : 보석(M2)은 밝기가 아니라 색이 정체다. 목표 5색 중 보석과 주색의
          **색상각 중점**을 경계로 삼는다(이미지 전용 상수가 아니다).
      (b) **휘도 순위** : 나머지를 목표 면적률 그대로 «가장 어두운 2%=SH … 가장 밝은 21%=W»로
          **개수를 세어** 배분한다. 퍼센타일 «값»으로 자르면 원본의 평탄면에서 동률이 무더기로
          생겨 한쪽으로 쏠린다(실측: 그 쏠림만으로 W가 +6.8%p 튀었다).

    동률은 **크게 흐린 휘도**로 깬다. 평탄면 안에서 임의로 자르면 소금후추 노이즈가 되는데,
    흐린 값은 공간적으로 매끄러워서 경계가 **선**으로 떨어진다.
    """
    names_dark_to_light = sorted(item.palette, key=lambda k: luma(hex_rgb(item.palette[k]) / 255.0))
    hue, sat, val = hsv_parts(rgb01)

    # (a) 보석 — 색상으로 먼저 뗀다.
    gem_name = "M2"
    gem_hue = float(hsv_parts(hex_rgb(item.palette[gem_name])[None, None, :] / 255.0)[0][0, 0])
    gold_hue = float(hsv_parts(hex_rgb(item.palette["M"])[None, None, :] / 255.0)[0][0, 0])
    half = float(hue_dist(np.array(gem_hue), gold_hue)) / 2.0
    gem = mask & (hue_dist(hue, gem_hue) < half)
    # 보석 안의 점 노이즈 제거 — 열고 닫아 «면»만 남긴다.
    gem = ndimage.binary_closing(ndimage.binary_opening(gem, np.ones((3, 3))), np.ones((3, 3))) & mask
    log(f"  [스냅] 보석 색상각 {gem_hue:.1f}° · 금 색상각 {gold_hue:.1f}° → 경계 ±{half:.1f}° "
        f"⇒ 보석 {gem.sum()} px = 잉크의 {100*gem.sum()/mask.sum():.2f}% (목표 {100*item.mix[gem_name]:.0f}%)")

    # (b) 휘도 — 잡음을 먼저 죽인다(원본은 AI 렌더라 미세 노이즈가 있고, 그것이 그대로
    #     소금후추가 된다). 마스크 밖이 새어 들지 않게 프리멀티플 평균으로 흐린다.
    L = luma(rgb01)
    mf = mask.astype(float)
    Lm = ndimage.median_filter(np.where(mask, L, 0.0), size=item.smooth_px)
    Lmed = np.where(mask, Lm, 0.0)
    num = ndimage.gaussian_filter(L * mf, 12.0); den = ndimage.gaussian_filter(mf, 12.0)
    Lblur = np.where(den > 1e-6, num / np.maximum(den, 1e-6), 0.0)

    rest = mask & ~gem
    order = [n for n in names_dark_to_light if n != gem_name]     # SH, MD, M, W (어두운 순)
    w = np.array([item.mix[n] for n in order], float); w = w / w.sum()
    ry, rx = np.nonzero(rest)
    n = ry.size
    key1 = Lmed[ry, rx]; key2 = Lblur[ry, rx]
    srt = np.lexsort((key2, key1))                 # 1차 = 중앙값 휘도, 2차 = 흐린 휘도
    bounds = np.round(np.cumsum(w) * n).astype(int)
    idx = np.full(mask.shape, -1, np.int8)
    start = 0
    for i, nm in enumerate(order):
        sel = srt[start:bounds[i]]
        idx[ry[sel], rx[sel]] = i
        log(f"    {nm:<3} 개수 배정 {bounds[i]-start:7d} px = 잉크의 {100*(bounds[i]-start)/mask.sum():5.2f}% "
            f"(휘도 구간 {key1[srt[start]]:.4f} ~ {key1[srt[bounds[i]-1]]:.4f})")
        start = bounds[i]
    idx[gem] = len(order)

    idx = despeckle(idx, len(order) + 1, item.min_patch_px, log=log)
    return idx, order + [gem_name]


def despeckle(idx, ntones, min_px, log=print):
    """★ 작은 조각을 **이웃 최빈 톤**으로 흡수한다.

    화법 조건(C-6/C-7)은 «값의 개수»가 아니라 «면이 면으로 읽히는가»를 본다. 12px짜리 점 100개는
    히스토그램상 같은 값이지만 화면에서는 얼룩이고, 몸(≈45 텍셀/R)에서는 **회색 노이즈**가 된다.
    """
    out = idx.copy()
    moved = 0
    for t in range(ntones):
        m = out == t
        lab, n = ndimage.label(m, np.ones((3, 3)))
        if n == 0: continue
        sizes = ndimage.sum(m, lab, range(1, n + 1))
        small = np.isin(lab, 1 + np.nonzero(sizes < min_px)[0]) & m
        if not small.any(): continue
        moved += int(small.sum())
        out[small] = -2                                    # 일단 비우고
    holes = out == -2
    if holes.any():
        src = out >= 0
        _, ind = ndimage.distance_transform_edt(~src, return_indices=True)
        out[holes] = out[ind[0][holes], ind[1][holes]]
    log(f"  [정리] {min_px}px 미만 조각 {moved} px 를 이웃 톤으로 흡수했다.")
    return out


# ─────────────────────────────────────────────────────────────────────────────
# 7. 좌우 대칭 (C-5)
# ─────────────────────────────────────────────────────────────────────────────
def mirror_asymmetry(idx, names, item):
    rgb = tone_rgb(idx, names, item)
    a = (idx >= 0)
    L = rgb.mean(axis=2) * a
    return float(np.abs(L - L[:, ::-1]).mean())

def symmetrize(rgb01, mask, log=print):
    """★ 앵커 세로축(x = ANCHOR_X)을 기준으로 **잉크가 많은 쪽 반**을 반사한다.
    엔진이 flipX 하므로(§19-9-b) 비대칭 조명은 걸을 때마다 좌우로 뒤집힌다 — C-5가 그것을 막는다.
    <b>양자화 전</b>에 접는다: 뒤에 접으면 개수 배정이 반쪽 분포로 어긋난다."""
    w = mask.shape[1]
    cx = int(round(ANCHOR[0]))
    assert cx * 2 == w, f"앵커가 캔버스 중앙이 아니다: {cx}*2 != {w}"
    lit_l = float(mask[:, :cx].sum()); lit_r = float(mask[:, cx:].sum())
    if lit_l >= lit_r:
        km, kc = mask[:, :cx], rgb01[:, :cx]
        side = "왼쪽"
    else:
        km, kc = mask[:, cx:][:, ::-1], rgb01[:, cx:][:, ::-1]
        side = "오른쪽"
    m2 = np.concatenate([km, km[:, ::-1]], axis=1)
    c2 = np.concatenate([kc, kc[:, ::-1]], axis=1)
    log(f"  [대칭] 왼쪽 잉크 {lit_l:.0f} px / 오른쪽 {lit_r:.0f} px → {side} 반을 반사했다(C-5).")
    return c2, m2


# ─────────────────────────────────────────────────────────────────────────────
# 8. 앞/뒤 2겹 분리
# ─────────────────────────────────────────────────────────────────────────────
def split_layers(idx, names, item, log=print):
    """앞/뒤 2겹 분리 — 규격서 §4-4 / §4-5.

    ★ **뒤판에 가는 것은 「뿔 사이로 보이는 어두운 안쪽 면」 하나다.** 밴드·뿔·보석·하이라이트는
    전부 앞판이다(§4-4 표). 그것을 코드로 옮기면 두 조건의 교집합이다 —

      (1) **어두운 톤**(MD·SH)이고,
      (2) **위가 배경으로 열려 있다** — 조각의 각 열에서 맨 윗 화소 바로 위가 투명이면 그 열은
          «틈으로 보이는» 것이다(금색 테를 넘도록 open_reach_px 까지 본다). 절반 이상의 열이 그러해야 한다. ★ 이 판정이 «배경에 닿는가»보다
          좁아야 한다는 것은 실측으로 알았다: «닿는가»만 보면 **뿔 끝 구슬의 어두운 면**이 같이
          뽑혀 앞판에 구멍이 뚫린다(구슬 아래가 배경이라 «닿는다»가 참이 된다).
      (3) **앞판 밴드 윗선(+0.840 R)보다 위**에 있다. 밴드 밑테의 그늘도 배경에 닿지만
          그것은 밴드의 일부이고 앞판이다.

    ★ **머리 원반 경계(1.17193 R)를 가로지르는가»로 고르면 이 그림에서는 <u>틀린 것</u>을 고른다** —
    실측으로 확인했다: 이 왕관에서 경계를 실제로 가로지르는 어두운 덩어리는 **밴드 양끝의 그늘**
    (x ±1.02, y +0.45..+0.71)이고, 그것을 뒤판으로 보내면 **밴드 끝이 머리 뒤로 사라진다.**
    반대로 진짜 안쪽 면 8조각은 전부 원반 <b>바깥</b>(r 1.28~1.42)에 있어 그 판정에 안 걸린다.
    ⇒ 판정은 «머리를 가리는가»가 아니라 «무엇인가»로 한다. §4-4가 처음부터 그렇게 적혀 있다.

    겹침(§4-2 규칙 3): 뒤판을 각 열에서 **밑으로 채워 내려** 앞판 밴드 뒤로 0.20 R 이상 물리게 한다.
    앞판은 **원래 조각 자리만** 비운다 — 채워 내린 부분까지 비우면 밴드에 구멍이 뚫린다.
    """
    h, w = idx.shape
    yy, xx = np.mgrid[0:h, 0:w]
    y_r = (ANCHOR[1] - (yy + 0.5)) / K
    lit = idx >= 0

    dark = np.zeros_like(lit)
    for n in item.back_wall:
        dark |= (idx == names.index(n))
    bg = ~lit
    # «위가 열려 있는가» — 각 열에서 그 조각의 **맨 윗 화소 바로 위**가 배경이면 그 열은 열린 것이다.
    # ★ 「바로 위가 배경인가」로는 못 고른다 — 이 그림의 안쪽 면은 **금색 아치 테로 액자처럼
    #   둘러싸여** 있어서 위로 17px(0.09 R)쯤 금을 지나야 배경이 나온다. 반대로 뿔 끝 구슬의
    #   어두운 면은 40px 이상이다. 그 사이에 선을 긋는다(실측표는 완료 보고에 있다).
    REACH = item.open_reach_px

    lab, n = ndimage.label(dark, np.ones((3, 3)))
    core = np.zeros_like(dark)
    kept = 0
    for i in range(1, n + 1):
        c = lab == i
        if c.sum() < 100: continue
        if np.mean(y_r[c] > item.band_top_r) < 0.9: continue        # (3) 밴드 윗선 위인가
        cols = np.nonzero(c.any(axis=0))[0]
        open_cols = 0
        for x in cols:
            top = np.nonzero(c[:, x])[0].min()
            if bg[max(0, top - REACH):top, x].any(): open_cols += 1
        ratio = open_cols / max(len(cols), 1)
        if ratio < 0.5: continue                                    # (2) 틈으로 «보이는» 면인가
        core |= c; kept += 1
    log(f"  [2겹] 어두운 덩어리 {n}개 중 «위가 배경으로 열려 있고(≥50% 열) 밴드 윗선(+{item.band_top_r:.3f} R) 위» "
        f"= {kept}개 · {core.sum()} px (잉크의 {100*core.sum()/max(lit.sum(),1):.2f}%)")

    front = idx.copy()
    front[core] = -1                    # ★ 원래 조각 자리만 비운다(§4-5 4단계)

    # 겹침 채우기. ★ **앞판이나 머리 원반이 가리는 데까지만** 채운다 —
    #   규격서 §4-5 는 «py 397 수평선까지»라고 적지만 그것은 안쪽 면이 **가운데 한 덩어리**인
    #   현행 벡터 왕관을 전제한 문장이다. 이 그림처럼 안쪽 면이 뿔마다 갈라져 있으면 바깥쪽
    #   조각의 수직 연장이 **실루엣 밖으로 튀어나와** 바탕화면에 검은 가시로 보인다(실측 10,834 px).
    #   채워 내리는 목적은 «이음매의 1px 틈 막기» 하나이므로, 가려지는 데까지가 필요충분이다.
    rr = np.hypot((xx + 0.5 - ANCHOR[0]) / K, (ANCHOR[1] - (yy + 0.5)) / K)
    disc = rr <= HEAD_VIS_R
    covered = (front >= 0) | disc
    back = core.copy()
    _, py_bottom = px_of(0, item.back_bottom_r)
    py_bottom = int(round(py_bottom))
    added = 0
    seam_ov = []
    for x in range(w):
        col = np.nonzero(core[:, x])[0]
        if col.size == 0: continue
        bot = col.max()
        run = 0
        for y in range(bot + 1, py_bottom + 1):
            if not covered[y, x]: break
            back[y, x] = True; added += 1; run += 1
        if front[bot + 1, x] >= 0:                 # 앞판과 실제로 맞닿는 이음매만 센다
            seam_ov.append(run / K)
    if kept:
        ov = (ANCHOR[1] - py_bottom) / K
        log(f"  [2겹] 겹침 채우기: 목표 아래끝 py {py_bottom} (= {ov:+.3f} R) — {added} px 추가")
        if seam_ov:
            log(f"  [2겹] 앞판과 맞닿는 이음매 {len(seam_ov)}열 · 겹침 최소 {min(seam_ov):.3f} R / "
                f"중앙값 {float(np.median(seam_ov)):.3f} R  (규칙 3: ≥ {OVERLAP_R:.2f} R)")
    backidx = np.full_like(idx, -1)
    for x in range(w):
        col = np.nonzero(back[:, x])[0]
        if col.size == 0: continue
        last = -1
        for y in col:
            if core[y, x] and idx[y, x] >= 0: last = idx[y, x]
            backidx[y, x] = last if last >= 0 else names.index(item.back_wall[-1])

    # ★ 자기 검사 — 뒤판이 «앞판에도 머리 원반에도 안 덮인 채» 튀어나오면 화면에 검은 가시가 된다.
    exposed = (backidx >= 0) & ~(front >= 0) & ~disc & ~core
    log(f"  [2겹] 자기검사: 앞판·머리 어디에도 안 가린 채 드러나는 뒤판 화소 {int(exposed.sum())} px "
        f"(0이어야 «채워 내린 벽»이 화면에 안 보인다)")
    return front, backidx


# ─────────────────────────────────────────────────────────────────────────────
# 9. 출력 · 검산
# ─────────────────────────────────────────────────────────────────────────────
def tone_rgb(idx, names, item):
    lut = np.zeros((len(names) + 1, 3), np.uint8)
    for i, n in enumerate(names): lut[i] = hex_rgb(item.palette[n]).astype(np.uint8)
    return lut[np.where(idx >= 0, idx, len(names))]

def to_rgba(idx, names, item):
    out = np.zeros(idx.shape + (4,), np.uint8)
    out[..., :3] = tone_rgb(idx, names, item)
    out[..., 3] = np.where(idx >= 0, 255, 0)
    out[idx < 0, :3] = 0          # 투명 화소의 RGB 는 0 — 밉맵에서 섞여도 색이 안 번지게
    return out

def report_mix(idx, names, item, log=print):
    tot = int((idx >= 0).sum())
    log(f"  [배분] 잉크 {tot} px")
    log(f"    {'톤':<4}{'hex':<10}{'목표':>8}{'실측':>8}{'차':>8}")
    worst = 0.0
    for i, n in enumerate(names):
        c = int((idx == i).sum()); a = 100.0 * c / max(tot, 1); t = 100.0 * item.mix[n]
        worst = max(worst, abs(a - t))
        log(f"    {n:<4}{item.palette[n]:<10}{t:7.1f}%{a:7.1f}%{a-t:+7.1f}%p")
    return worst

def audit(path, log=print):
    a = np.asarray(Image.open(path).convert("RGBA"))
    al = a[..., 3].astype(int)
    ys, xs = np.nonzero(al >= 128)
    if ys.size == 0:
        log(f"  {os.path.basename(path)}  ★ 잉크 0 px — 이 판은 비어 있다.")
        return None
    x0, y0 = r_of(xs.min(), ys.max() + 1); x1, y1 = r_of(xs.max() + 1, ys.min())
    op = int((al >= 248).sum()); mid = int(((al > 7) & (al < 248)).sum())
    crop = a[ys.min():ys.max()+1, xs.min():xs.max()+1]
    L = crop[..., :3].astype(float).mean(axis=2) * (crop[..., 3] / 255.0)
    asym = float(np.abs(L - L[:, ::-1]).mean())
    log(f"  {os.path.basename(path)}")
    log(f"    잉크박스(R)  minX {x0:+.4f}  maxX {x1:+.4f}  minY {y0:+.4f}  maxY {y1:+.4f}")
    log(f"    좌우 대칭    |minX+maxX| = {abs(x0+x1):.4f} R  (≤ 0.06)")
    log(f"    중간 알파    {mid} px = 불투명({op})의 {mid/max(op,1)*100:.2f}%  (≤ 10%)")
    log(f"    좌우 조명    반전 밝기 평균차 {asym:.2f}  (≤ 6)")
    return dict(minX=x0, maxX=x1, minY=y0, maxY=y1, mid=mid / max(op, 1), asym=asym)

def style_metrics(path, log=print):
    """design-character 계기 재사용 — m1_style_metrics.py 의 M() 과 **같은 식**.
    (테두리 L / 테두리<60% / 채도% / **평탄면** / 정규화 기울기)"""
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(float)
    al = a[:, :, 3] > 128
    L = a[:, :, :3].mean(axis=2)
    mx = a[:, :, :3].max(axis=2); mn = a[:, :, :3].min(axis=2)
    S = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1), 0)
    band = al & ~ndimage.binary_erosion(al, np.ones((3, 3)), iterations=3)
    inner = ndimage.binary_erosion(al, np.ones((3, 3)), iterations=3)
    gy, gx = np.gradient(L); g = np.hypot(gx, gy)
    hist, _ = np.histogram(L[inner], bins=16, range=(0, 256))
    flats = int((hist / max(inner.sum(), 1) > 0.03).sum())
    wpx = im.size[0]
    v = (float(np.median(L[band])), 100 * float(np.mean(L[band] < 60)),
         100 * float(S[inner].mean()), flats, float(g[inner].mean()) * wpx / 100.0)
    log(f"  {os.path.basename(path)}  테두리L {v[0]:.1f} · 테두리<60 {v[1]:.1f}% · 채도 {v[2]:.1f}% · "
        f"**평탄면 {v[3]}** (C-6 ≤ 6) · 정규화기울기 {v[4]:.1f}")
    # C-7 그라데이션 면적률: 3×3 밝기폭 2~20 인 내부 화소 비율
    mxf = ndimage.maximum_filter(L, 3); mnf = ndimage.minimum_filter(L, 3)
    rng = mxf - mnf
    grad = float(np.mean(((rng >= 2) & (rng <= 20))[inner]) * 100) if inner.sum() else 0.0
    log(f"    C-7 그라데이션 면적률 {grad:.1f}% (≤ 15)")
    return v, grad


# ─────────────────────────────────────────────────────────────────────────────
# 10. 카드 파생본 — **같은 그림**을 카드 프레임에 다시 앉힌다 (규격서 §7-2)
# ─────────────────────────────────────────────────────────────────────────────
def bake_card(front, back, names, item, bg_hex, log=print):
    """앞판+뒤판을 합쳐 카드 캔버스(CARD_PX²)에 굽는다.

    카드에는 **머리가 없다** — 그래서 뒤판(머리 뒤로 보내는 안쪽 면)을 앞판 밑에 깔아
    «물건 전체»를 보여 준다. 착용 표면에서 뒤판이 안 보이는 것은 머리가 가리기 때문이지
    그 조각이 물건의 일부가 아니어서가 아니다.
    """
    span_r, center_y_r, px_per_r = card_frame(item.card_slot)
    log(f"  [카드] 프레임 = 옛 벡터 카드와 같은 배율: {px_per_r:.3f} pt/R "
        f"→ 캔버스 {span_r:.4f} R 창, 중심 y {center_y_r:+.4f} R")

    idx = np.where(front >= 0, front, back)
    rgba = to_rgba(idx, names, item)

    ys, xs = np.nonzero(rgba[..., 3] >= 128)
    ix0, ix1 = r_of(xs.min(), 0)[0], r_of(xs.max() + 1, 0)[0]
    iy1, iy0 = r_of(0, ys.min())[1], r_of(0, ys.max() + 1)[1]
    log(f"  [카드] 합본 잉크(R)  x {ix0:+.4f}..{ix1:+.4f} ({ix1-ix0:.4f})  "
        f"y {iy0:+.4f}..{iy1:+.4f} ({iy1-iy0:.4f})")
    over = max((ix1 - ix0) / span_r, (iy1 - iy0) / span_r)
    if over > 1.0:
        # 벡터 쪽 BoxFit.Shrink 와 같은 처방 — 줄이기만 한다. 여기 걸리면 «자리 바뀜»이 생기므로
        # 조용히 넘어가지 않고 크게 찍는다.
        log(f"  [카드] ★ 잉크가 창을 {over:.3f}배 넘는다 — 벡터와 같은 규칙으로 축소한다(자리 이동 발생).")
        span_r *= over

    # 창(R) → 원본 캔버스 px. 창 위쪽이 캔버스 밖으로 나가는 것이 정상이다(머리 위 여백).
    half = span_r * 0.5
    wx0, wy1 = px_of(-half, center_y_r + half)      # 좌상단
    wx1, wy0 = px_of(+half, center_y_r - half)      # 우하단
    log(f"  [카드] 창(원본px) x {wx0:.1f}..{wx1:.1f}  y {wy1:.1f}..{wy0:.1f}  "
        f"(변 {wx1-wx0:.1f}px → {CARD_PX}px, 축소 {(wx1-wx0)/CARD_PX:.2f}배)")

    # 알파 프리멀티플로 축소한다 — place() 와 같은 이유(실루엣 밖 0색이 가장자리로 새어 든다).
    a = rgba[..., 3:4].astype(np.float64) / 255.0
    pm = np.concatenate([rgba[..., :3].astype(np.float64) / 255.0 * a, a], axis=2)
    pad = int(np.ceil(max(0.0, -min(wx0, wy1), max(wx1, wy0) - AUTHOR_PX))) + 2
    big = np.zeros((AUTHOR_PX + 2 * pad, AUTHOR_PX + 2 * pad, 4))
    big[pad:pad + AUTHOR_PX, pad:pad + AUTHOR_PX] = pm
    src = Image.fromarray((np.clip(big, 0, 1) * 255.0 + 0.5).astype(np.uint8), "RGBA")
    win = src.crop((int(round(wx0)) + pad, int(round(wy1)) + pad,
                    int(round(wx1)) + pad, int(round(wy0)) + pad))
    small = np.asarray(win.resize((CARD_PX, CARD_PX), Image.LANCZOS)).astype(np.float64) / 255.0

    sa = small[..., 3:4]
    rgb = np.where(sa > 1e-3, small[..., :3] / np.maximum(sa, 1e-3), 0.0)
    out = np.zeros((CARD_PX, CARD_PX, 4), np.uint8)
    if bg_hex:
        # 「이미지 자체가 배경까지 갖고 온다」 — 팩 12종과 같은 형태(알파 없음).
        bg = hex_rgb(bg_hex) / 255.0
        out[..., :3] = (np.clip(rgb * sa + bg * (1.0 - sa), 0, 1) * 255.0 + 0.5).astype(np.uint8)
        out[..., 3] = 255
    else:
        out[..., :3] = (np.clip(rgb, 0, 1) * 255.0 + 0.5).astype(np.uint8)
        out[..., 3] = (np.clip(sa[..., 0], 0, 1) * 255.0 + 0.5).astype(np.uint8)

    path = os.path.join(ROOT, item.out_card)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(out, "RGBA").save(path)

    # ---- 검산: 이 PNG가 정말 «옛 벡터와 같은 자리·같은 크기»인가 -----------------------
    aa = np.asarray(Image.open(path).convert("RGBA")).astype(int)
    if bg_hex:
        d = np.abs(aa[..., :3] - hex_rgb(bg_hex).astype(int)).sum(axis=2)
        m = d > 24
    else:
        m = aa[..., 3] >= 128
    cy, cx = np.nonzero(m)
    fx0, fx1 = cx.min() / CARD_PX, (cx.max() + 1) / CARD_PX
    fy0, fy1 = cy.min() / CARD_PX, (cy.max() + 1) / CARD_PX
    log(f"  [출력] {item.out_card}  {CARD_PX}×{CARD_PX} "
        f"{'불투명(배경 ' + bg_hex + ')' if bg_hex else '알파 보존(배경 없음)'}")
    log(f"    잉크 프레임비  x {fx0:.4f}..{fx1:.4f} (폭 {fx1-fx0:.4f})  "
        f"y {fy0:.4f}..{fy1:.4f} (높이 {fy1-fy0:.4f})")
    # ★ 「기대」는 <b>같은 잉크를 옛 벡터 카드의 배율로 그렸을 때</b>의 폭이다(옛 벡터 «왕관»의
    #   폭이 아니다 — 그 물건은 조형이 달라 폭도 다르다). 둘이 같아야 «제자리 교체»가 성립한다.
    log(f"    카드 실그리기  폭 {(fx1-fx0)*BITMAP_ICON_PT:.2f} pt · 높이 {(fy1-fy0)*BITMAP_ICON_PT:.2f} pt "
        f"(기대 = 잉크 {(ix1-ix0):.4f} R × 벡터 배율 {px_per_r:.3f} pt/R = {(ix1-ix0)*px_per_r:.2f} pt)")
    return dict(fx0=fx0, fx1=fx1, fy0=fy0, fy1=fy1, span_r=span_r, center_y_r=center_y_r)


# ─────────────────────────────────────────────────────────────────────────────
def run(item, log=print, card_bg=CARD_BG_MUTED):
    log(f"╔══ 착용 비트맵 후처리 — {item.key} ══╗")
    src = os.path.join(ROOT, item.src)
    a = np.asarray(Image.open(src).convert("RGBA")).astype(np.uint8)
    log(f"  원본 {os.path.basename(src)} {a.shape[1]}×{a.shape[0]}")
    rgb01 = a[..., :3].astype(np.float64) / 255.0

    mask = binarize_alpha(a, log=log)
    mask, satthr = strip_unwanted_body(rgb01, mask, log=log)
    rgb01 = inpaint_low_saturation(rgb01, mask, satthr, log=log)
    C, Cm = place(rgb01, mask, item.ink, log=log)
    C, Cm = symmetrize(C, Cm, log=log)
    idx, names = snap_palette(C, Cm, item, log=log)
    worst = report_mix(idx, names, item, log=log)
    front, back = split_layers(idx, names, item, log=log)

    for path, m in ((item.out_front, front), (item.out_back, back)):
        p = os.path.join(ROOT, path)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        Image.fromarray(to_rgba(m, names, item)).save(p)
        log(f"  [출력] {path}  잉크 {int((m>=0).sum())} px")

    log("╠══ 카드 파생본 (규격서 §7-2) ══╣")
    card = bake_card(front, back, names, item, card_bg, log=log)

    log("╠══ 검산 ══╣")
    fa = audit(os.path.join(ROOT, item.out_front), log=log)
    ba = audit(os.path.join(ROOT, item.out_back), log=log)
    # §4-2 규칙 1 — 두 판은 같은 캔버스·같은 앵커여야 한다. 「같다」를 <b>실제로 잰다</b>:
    #   두 PNG의 픽셀 치수가 같으면 <c>wornSpriteRectInR</c> 한 칸을 두 판이 공유하므로(렌더러가
    #   그렇게 놓는다) 정합 오차가 구조적으로 0이다. 크기가 다르면 그 전제가 깨진다.
    fs = Image.open(os.path.join(ROOT, item.out_front)).size
    bs = Image.open(os.path.join(ROOT, item.out_back)).size
    log(f"  두 판 캔버스: 앞 {fs[0]}×{fs[1]} · 뒤 {bs[0]}×{bs[1]} → "
        f"{'같다 ✓ (rect 한 칸을 공유하므로 정합 오차 0, §4-2 규칙 1)' if fs == bs else '★ 다르다 — 즉시 반려'}")

    # C-11(design-character) — 머리 잉크 원반이 얼마나 남는가. 앞판만 머리를 덮는다(뒤판은 뒤에 있다).
    fa_img = np.asarray(Image.open(os.path.join(ROOT, item.out_front)).convert("RGBA"))
    hh, ww = fa_img.shape[:2]
    yy2, xx2 = np.mgrid[0:hh, 0:ww]
    disc = np.hypot((xx2 + 0.5 - ANCHOR[0]) / K, (ANCHOR[1] - (yy2 + 0.5)) / K) <= HEAD_VIS_R
    cover = ((fa_img[..., 3] > 0) & disc).sum() / max(disc.sum(), 1)
    log(f"  C-11 머리 원반 노출 {100*(1-cover):.1f}%  (≥ 65 — 현행 벡터 중절모 68.7%)")
    style_metrics(os.path.join(ROOT, item.out_front), log=log)
    log("╚═══════════════════════════════════╝")
    return dict(mix_worst=worst, front=fa, back=ba, card=card)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("item", nargs="?", default="crown", choices=sorted(ITEMS))
    ap.add_argument("--card-bg", default="none",
                    help="카드 파생본의 배경색 hex(예: '#15181E'). 기본 'none' = 알파 보존(판 색을 그대로 비춘다).")
    args = ap.parse_args()
    run(ITEMS[args.item], card_bg=None if args.card_bg.lower() == "none" else args.card_bg)
