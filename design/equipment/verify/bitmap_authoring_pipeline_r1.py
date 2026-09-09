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

def px_of(x_r, y_r):
    return (ANCHOR[0] + x_r * K, ANCHOR[1] - y_r * K)

def r_of(px, py):
    return ((px - ANCHOR[0]) / K, (ANCHOR[1] - py) / K)


class Item:
    """아이템 하나의 저작 파라미터. 새 아이템은 여기 한 줄만 늘어난다."""
    def __init__(s, key, src, out_front, out_back, ink_box_r, palette, mix, back_wall,
                 band_top_r, back_bottom_r, smooth_px, min_patch_px, open_reach_px):
        s.key, s.src = key, src
        s.out_front, s.out_back = out_front, out_back
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
    # 규격서 §5-1: x ±1.310 R · y +0.280 .. +2.380 R
    ink_box_r=(-1.310, +1.310, +0.280, +2.380),
    # design-art §6-2 확정 5색
    palette={"W": "#BB8E1C", "M": "#9B7922", "MD": "#604B15", "SH": "#2B220A", "M2": "#C6443C"},
    # design-art §6-3 권고 B
    mix={"W": 0.21, "M": 0.58, "MD": 0.12, "SH": 0.02, "M2": 0.07},
    back_wall=("MD", "SH"),
    band_top_r=+0.840,      # §5-1 «앞판 밴드 윗선»
    back_bottom_r=+0.640,   # §5-1 «뒤판 아래끝» (= 0.840 − 0.200 겹침)
    smooth_px=9, min_patch_px=260,
    # 실측(1024² 캔버스): 안쪽 면 4조각은 위쪽 배경까지 14~17 px, 구슬 어두운 면은 40 px.
    open_reach_px=30,
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
def run(item, log=print):
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
    return dict(mix_worst=worst, front=fa, back=ba)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("item", nargs="?", default="crown", choices=sorted(ITEMS))
    args = ap.parse_args()
    run(ITEMS[args.item])
