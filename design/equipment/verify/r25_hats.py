# -*- coding: utf-8 -*-
"""R25 — 모자 6종 H-2 목표 +0.30 → **+0.45 전면 상향** + 「앞층 밑단 하한」 신설(H-2b).  python3 r25_hats.py

사용자 지적(2026-09-06): *"다른 모자들도 너무 가리지 않는지 확인, 안경착용시 모자들이 가리는 현상이 많았음."*
리더 판정: **안 B(목표 상향)**. 왕관과 같은 기준(+0.45)으로 6종 통일한다.

★ 자(尺)는 r24_hats.py 와 **한 줄도 다르지 않다** — 그 모듈을 import 해서 쓴다(프로덕션 소스 직접 파싱).
  베레모·밀짚모자만 R21 재저작 좌표(r21_model.BODY)를 같은 형식으로 옮겨 함께 잰다.

여기서 새로 하는 것
  ① **H-2 는 상한만 있고 하한이 없다** — 그것이 털모자를 놓친 이유다. 실측으로 그 사실을 못박는다.
  ② 몸 표면 y 아핀 (s, t) + 관 x 배수 kx 를 격자 탐색해 6종 전부를 왕관 대역으로 수렴시킨다.
  ③ (s, t) → 프로덕션 손잡이 역산: 인계본 3종은 r17_model.HAT_FIT(ky, dy), 털모자는 AccessoryWornTransform.
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r24_hats as R24                      # noqa: E402  — 자(尺) 원본
import r17_model as M17                     # noqa: E402
import r19_model as M19                     # noqa: E402

PTS = R24.PTS
SORT_EYES, SORT_HEAD, SORT_BACK = R24.SORT_EYES, R24.SORT_HEAD, R24.SORT_BACK
HEAD_R_COVER, COVER_MARGIN = R24.HEAD_R_COVER, R24.COVER_MARGIN
TOP_LIMIT, CHIN = R24.TOP_LIMIT, R24.CHIN

# ── R25 규칙 ────────────────────────────────────────────────────────────────
H2_TARGET_NEW = 0.45          # 6종 공통(구: 감싸는 모자 0.30 · 털모자 0.00 · 왕관 0.45)
H2B_FLOOR = 0.28              # ★ 신설 — 앞층 채움 잉크 밑단(머리 폭 안) 하한
H2B_AIM = 0.315               # 수렴 목표 = 왕관 실측 밑단(머리 폭 안) +0.3152. 여기에 **가장 가깝게** 맞춘다
#   ★ 하한만 걸면 탐색이 「가능한 한 높이」로 도망간다(베레모가 밑단 +0.4355 로 떠서 가려짐 2.3%가 됐다).
#     사용자가 요구한 것은 「덜 가리는 것」이지 「안 쓰는 것」이 아니다 — 그래서 하한이 아니라 **목표값**을 쓴다.
TOP_CAP = 2.5439              # 현행 최고 아이템(털모자)을 넘지 않는다 → 액자 상수 2.551 을 못 건드린다
CROWN_WIDEN_OLD = dict(M19.CROWN_WIDEN)          # {"clothhat":1.12,"fedora":1.18} — 이미 프로덕션에 구워져 있다
CROWN_WIDEN_SRC = set(M19.CROWN_WIDEN_SRC)       # ("B0","F1","H3")

KIND_OF = {"천모자": "clothhat", "털모자": "furhat", "중절모": "fedora", "왕관": "crown"}


# ============================================================================
# 0. 조각 로드 — 인계본 4종(프로덕션 파싱) + 베레모·밀짚모자(v1 현행 / R21 안)
# ============================================================================
def _r21_pieces(body):
    out = []
    for p in body:
        out.append(dict(name=p.name, src=p.src, pts=list(p.pts), filled=bool(p.filled), loop=bool(p.loop),
                        tone=0, sort=(SORT_BACK if p.layer == "back" else SORT_HEAD)))
    return out


def load():
    hats = {k: R24.HATS[k]() for k in R24.HATS}          # 6종 현행(인계본 4 + v1 베레모·밀짚모자)
    import r21_model as M21
    hats21 = {"베레모": _r21_pieces(M21.BERET_BODY), "밀짚모자": _r21_pieces(M21.STRAW_BODY)}
    eyes = {k: f() for k, f in R24.EYES.items()}
    return hats, hats21, eyes


# ============================================================================
# 1. 몸 표면 변형 — y 아핀 (s, t) · 관 x 추가배수 r
# ============================================================================
def _crown_widened(src):
    """이 조각이 CROWN_WIDEN 의 x 배수를 받는 조각인가.
    ★ 2026-09-06 R27 — 관(B0)이 «채움 B0 + 열린 호 B0a» 두 조각이 됐다. r19_model 은 **분할 전에**
      배수를 걸므로 둘 다 이미 같은 배수를 갖는데, 이 파일의 재시뮬레이션은 이름으로 고르므로
      «a» 접미사를 안 벗기면 **호만 옛 배수에 남아** 실루엣이 가짜로 움직인다(실측 천모자↔중절모 1.43 → 1.39획).
      그 거짓값은 프로덕션과 무관하다 — 출하 좌표의 72×5° 프로필은 R27 전후로 소수점 아래 10자리까지 같다."""
    return src in CROWN_WIDEN_SRC or (src.endswith("a") and src[:-1] in CROWN_WIDEN_SRC)


def xform(pieces, s=1.0, t=0.0, kx_ratio=1.0, ux=1.0):
    """s = 세로 배율 · t = 세로 평행이동 · kx_ratio = 관 조각 x 추가배수 · ux = 전체 x 배율(균일 축소용)."""
    out = []
    for p in pieces:
        r = (kx_ratio if _crown_widened(p["src"]) else 1.0) * ux
        q = dict(p)
        q["pts"] = [(x * r, y * s + t) for x, y in p["pts"]]
        out.append(q)
    return out


def top_of(pieces):
    return max(y for p in pieces for _, y in p["pts"])


def bottom_front(pieces):
    fr = [p for p in pieces if p["filled"] and p["sort"] >= SORT_EYES]
    return min(y for p in fr for _, y in p["pts"]) if fr else None


def bottom_all(pieces):
    return min(y for p in pieces for _, y in p["pts"])


def h2(pieces, with_stroke=False):
    """(착용선, 머리 꼭대기부터 덮는가). r24.coverage_runs 와 같은 자 — 첫 구간이 머리 꼭대기에서 시작해야 한다."""
    runs = R24.coverage_runs(pieces, with_stroke, step=0.004)
    if not runs:
        return None, False
    top_ok = runs[0][0] >= HEAD_R_COVER - 0.02
    return runs[0][1], top_ok


# ── 빠른 자: 래스터 대신 **정확 스캔라인**. r24 의 hw_at 과 같은 값인지는 ⓪-a 대조로 못박는다 ────
YG = np.arange(-1.4, 2.8, 0.004)


def _hw_exact(polys, y):
    iv = []
    for poly in polys:
        iv += M17._scan_intervals(poly, y)
    if not iv:
        return 0.0
    iv.sort()
    merged = [list(iv[0])]
    for a, b in iv[1:]:
        if a <= merged[-1][1] + 1e-9:
            merged[-1][1] = max(merged[-1][1], b)
        else:
            merged.append([a, b])
    for a, b in merged:
        if a <= 0.0 <= b:
            return min(-a, b)
    return 0.0


def hw_profile(pieces):
    """앞층 채움의 중앙 반폭 프로필(모자 자기 좌표계). 평행이동 t 는 이 배열의 색인만 옮긴다."""
    polys = [p["pts"] for p in pieces if p["filled"] and p["sort"] >= SORT_EYES]
    return np.array([_hw_exact(polys, float(y)) for y in YG])


NEED = np.sqrt(np.clip(HEAD_R_COVER ** 2 - YG ** 2, 0.0, None)) + COVER_MARGIN
IN_HEAD = YG <= HEAD_R_COVER


def h2_fast(prof, t):
    """머리 y 격자 YG 에서, 모자를 t 만큼 올렸을 때의 (착용선, 꼭대기부터 덮는가).
    모자 자기 좌표 y_loc = Y − t → prof 를 t/0.004 칸 옮겨 읽는다."""
    sh = int(round(t / 0.004))
    hw = np.zeros_like(prof)
    if sh >= 0:
        if sh < len(prof):
            hw[sh:] = prof[:len(prof) - sh]
    else:
        hw[:len(prof) + sh] = prof[-sh:]
    ok = (hw >= NEED) & IN_HEAD
    idx = np.where(IN_HEAD)[0]
    idx = idx[np.argsort(-YG[idx])]
    wear, started = None, False
    for j in idx:
        if not ok[j]:
            break
        wear, started = float(YG[j]), True
    if not started:
        return None, False
    return wear, bool(YG[idx[0]] >= HEAD_R_COVER - 0.02)


def occl(pieces, eyes_ink):
    hm = R24.front_fill(pieces)
    return {e: 100.0 * (m & hm).sum() / m.sum() for e, m in eyes_ink.items()}


def half_width_profile(pieces, ys):
    fr = [p for p in pieces if p["filled"] and p["sort"] >= SORT_EYES]
    return [R24.hw_at(fr, float(y)) for y in ys]


# ============================================================================
# 2. 탐색 — s 는 1.0 에 가장 가깝게(형태 왜곡 최소), t 는 착용선 상한에 붙인다
# ============================================================================
def _fit_t(pieces, s, kr, ux, target, floor):
    base = xform(pieces, s, 0.0, kr, ux)
    ys = [y for p in base for _, y in p["pts"]]
    if min(ys) < YG[0] + 1e-9 or max(ys) > YG[-1] - 1e-9:
        # ★ 자기 버그 2건째 — 조각이 프로필 격자(YG) 밖이면 h2_fast 가 조용히 hw=0 을 읽는다.
        raise SystemExit("조각 y 범위 [%.3f,%.3f] 가 프로필 격자 [%.3f,%.3f] 밖이다 — 좌표계를 맞춰라"
                         % (min(ys), max(ys), YG[0], YG[-1]))
    prof = hw_profile(base)
    b0, t0, ba0 = bottom_in_head(base), top_of(base), bottom_all(base)      # ★ 밑단은 **머리 폭 안**으로 잰다
    if b0 is None:
        return None
    # t 의 상한 3개: (ㄱ) 액자  (ㄴ) 착용선 ≤ target  (ㄷ) 밑단 목표 H2B_AIM
    t_frame = TOP_CAP - t0
    t = t_frame
    while t > -1.2:
        w, ok = h2_fast(prof, t)
        if ok and w is not None and w <= target + 1e-9:
            break
        t -= 0.004
    else:
        return None
    t_wear = t
    t_aim = H2B_AIM - b0
    t_raw = min(t_frame, t_wear, t_aim)
    bind = "액자" if abs(t_raw - t_frame) < 1e-9 else ("착용선" if abs(t_raw - t_wear) < 1e-9 else "밑단목표")
    t = math.floor(t_raw / 0.004) * 0.004      # ★ 내림 — 반올림하면 액자 상한을 0.0017 R 넘길 수 있다
    if abs(t) <= 0.006 and abs(b0 - H2B_AIM) <= 0.010:
        t, bind = 0.0, "무변경(이미 목표 안)"    # 왕관이 여기 든다 — 0.004 격자 때문에 흔들리지 않게 못박는다
    w, ok = h2_fast(prof, t)
    if not ok or w is None or w > target + 1e-9:
        return None
    if b0 + t < floor - 1e-9 or ba0 + t <= CHIN:
        return None
    return dict(s=s, t=t, kx_ratio=kr, ux=ux, wear=w, bottom=b0 + t, top=t0 + t, bind=bind,
                pieces=xform(pieces, s, t, kr, ux))


def search(pieces, kx_ratios=(1.0,), s_list=None, floor=None, target=None, uniform=False):
    """제약: 착용선 ≤ target(+0.45) (머리 꼭대기부터 연속) · 앞층 밑단 ≥ floor(+0.28) · 꼭대기 ≤ 2.5439 · 밑 > −1.0.
    목적: (ㄱ) s 최대(세로 압축 최소) → (ㄴ) 관 배수 최소 → (ㄷ) t 최대(밑단 최대 = 가려짐 최소).
    uniform=True 면 x 도 s 만큼 줄인다(균일 축소 — 세로만 누르는 것과의 대안)."""
    floor = H2B_FLOOR if floor is None else floor
    target = H2_TARGET_NEW if target is None else target
    s_list = s_list if s_list is not None else [round(1.0 - 0.005 * i, 3) for i in range(0, 81)]
    for s in s_list:
        for kr in kx_ratios:
            r = _fit_t(pieces, s, kr, s if uniform else 1.0, target, floor)
            if r is not None:
                return r
    return None


def sweep(pieces, kx_ratio=1.0, s=1.0, ts=None):
    """진단용 — t 를 훑으며 (착용선, 밑단, 꼭대기) 를 낸다. 「불가능」 주장을 숫자로 못박는 자."""
    base = xform(pieces, s, 0.0, kx_ratio)
    prof = hw_profile(base)
    b0, t0 = bottom_front(base), top_of(base)
    out = []
    for t in (ts if ts is not None else np.arange(0.0, 0.46, 0.02)):
        w, ok = h2_fast(prof, float(t))
        out.append(dict(t=float(t), wear=w, top_ok=ok, bottom=b0 + t, top=t0 + t))
    return out


# ============================================================================
# 2-b. 베레모 조형 변주 — 밑변 기울기 τ (아이콘 단위, R21 원안 = 5.0)
# ============================================================================
#   R21 베레모 밑변은 왼끝 아이콘 y37 · 오른끝 y42 로 **5u 기울어져 있다**(= 0.35 R @u 0.07).
#   그 기울기가 그대로 「꼬리」(착용선 − 앞층 밑단 = 0.336 R)다 — 평행이동으로 절대 안 줄어든다.
#   실물 베레모에서 기울어지는 것은 **판(원반)** 이지 **머리띠(밴드)** 가 아니다. 밴드를 수평에 가깝게
#   되돌리고 원반의 처짐(오른쪽 x +1.615 vs 왼쪽 −1.433)은 그대로 두는 것이 조형적으로도 옳다.
BERET_DY0 = 2.88          # R21 원안의 dy — 변주는 여기서 출발하고 탐색이 내는 t 는 그 **증분**이다


def _icon_to_R(spec, u, dy):
    import r21_model as M21
    out = []
    for pc in M21.icon_polys(spec):
        out.append(dict(name="beret." + pc["src"], src=pc["src"],
                        pts=[((x - 32.0) * u, dy - y * u) for x, y in pc["pts"]],
                        filled=pc["call"] in ("B", "F", "CB"), loop=pc["closed"], tone=0, sort=SORT_HEAD))
    return out


def beret_variant(mode, tau=5.0, u=0.07, dy=BERET_DY0):
    """R21 베레모의 밑변 처리 세 가지. 반환은 R 좌표(dy = 0).
      'tilt' : R21 원안 — 밴드까지 통째로 τ 만큼 기울어진다(원안 τ=5.0 = 0.350 R)
      'level': 밴드·원반 밑변 모두 수평(아이콘 37) — 처짐이 사라진다
      'sag'  : ★ 밴드는 수평(37) · 원반만 **밴드 오른끝(아이콘 48u = +1.12 R) 밖**에서 처진다
               → 처짐(베레모의 정체)은 남기고, 처지는 자리를 **머리·안경 밖**으로 옮긴다"""
    body = {
        "tilt": "M13 37 Q8 27 20 19 Q32 13 44 17 Q56 22 55 33 Q54 %.3f 48 %.3f L13 37 Z"
                % (33.0 + 1.4 * tau, 37.0 + tau),
        "level": "M13 37 Q8 27 20 19 Q32 13 44 17 Q56 22 55 33 Q54 35 48 37 L13 37 Z",
        "sag": "M13 37 Q8 27 20 19 Q32 13 44 17 Q56 22 55 33 Q58 45 48 37 L13 37 Z",
    }[mode]
    band = ("M13 37 L48 %.3f L48.5 %.3f L13.5 32 Z" % (37.0 + tau, 32.0 + tau)) if mode == "tilt" \
        else "M13 37 L48 37 L48.5 32 L13.5 32 Z"
    spec = [
        ("B", "Body", body, ("M", "INK"), 1.0, "몸(M Felt + 잉크) — 오른쪽으로 처진 원반"),
        ("F", "Band", band, ("M2", None), 1.0, "띠(M2, 윤곽 없음)"),
        ("S", "Stem", "M32 14.5 L32.5 10", (None, "M"), 1.2, "꼭지(독립선 M ×1.2)"),
        ("H", "H", "M17 25 Q20 20 26 18", (None, "W"), 0.75, "하이라이트"),
    ]
    return _icon_to_R(spec, u, dy)


def bottom_in_head(pieces, xlim=None):
    """★ 얼굴을 가리는 것은 **머리 폭 안**의 밑단이다. 챙 끝(±1.8 R)이나 원반의 옆 처짐은 얼굴을 못 가린다.
    xlim 기본 = 머리 잉크 원반 반경(1.1842 R).
    ★ 훑는 범위는 **조각의 실제 y 범위**에서 뽑는다 — 고정 범위(1.30~−1.20)를 썼다가 dy=0 좌표계의
      변주를 통째로 놓쳤다(자기 버그 1건, 2026-09-06)."""
    xlim = HEAD_R_COVER if xlim is None else xlim
    polys = [p["pts"] for p in pieces if p["filled"] and p["sort"] >= SORT_EYES]
    if not polys:
        return None
    ylo = min(y for q in polys for _, y in q) - 0.01
    yhi = max(y for q in polys for _, y in q) + 0.01
    lo = None
    for y in np.arange(yhi, ylo, -0.004):
        hit = False
        for poly in polys:
            for a, b in M17._scan_intervals(poly, float(y)):
                if b >= -xlim and a <= xlim:
                    hit = True
                    break
            if hit:
                break
        if hit:
            lo = float(y)
    return lo


# ============================================================================
# 3. 손잡이 역산
# ============================================================================
def back_out_handoff(kind, s, t):
    """인계본 3종(clothhat/fedora/crown): y_R = dy − y_icon·u·ky 라 ky' = s·ky, dy' = s·dy + t."""
    f = M17.HAT_FIT[kind]
    return dict(u=f["u"], ky=f["ky"] * s, dy=f["dy"] * s + t, ky_old=f["ky"], dy_old=f["dy"])


def back_out_furhat(s, t):
    """털모자: y_R = y_base·U·KY + DY (AccessoryWornTransform) → KY' = s·KY, DY' = s·DY + t."""
    return dict(scale=R24.FUR_U, scale_y=s * R24.FUR_KY, dy=s * R24.FUR_DY + t,
                scale_y_old=R24.FUR_KY, dy_old=R24.FUR_DY)


# ============================================================================
# 4. 실루엣 차(72×5° 프로필) — 무회귀 확인용
# ============================================================================
def sil_matrix(hatmap):
    """★ 자는 `verify.py` 의 것 그대로 — `rig.max_delta(프로필a, 프로필b) / W`(구간별 **최대** 차).
    (첫 판은 평균을 썼다가 §14-14-7 의 1.69획을 0.31획으로 냈다 — 자가 달랐던 것이다.)"""
    ks = list(hatmap)
    pr = {k: R24.profile(hatmap[k]) for k in ks}
    out = {}
    for i in range(len(ks)):
        for j in range(i + 1, len(ks)):
            a, b = pr[ks[i]], pr[ks[j]]
            out[(ks[i], ks[j])] = max(abs(x - y) for x, y in zip(a, b)) / R24.W
    return out


# ============================================================================
# 5. 본문
# ============================================================================
def main():
    hats, hats21, eyes = load()
    ei = {k: R24.ink(v) for k, v in eyes.items()}
    print("R25 — 자: r24_hats 그대로(프로덕션 직접 파싱). 획 W = %.5f R · 머리 %.4f R · 여유 %.2f R"
          % (R24.W, HEAD_R_COVER, COVER_MARGIN))
    print("     새 규칙: H-2 착용선 ≤ +%.2f (6종 공통) · ★신설 H-2b 앞층 채움 밑단 ≥ +%.2f · 꼭대기 ≤ %.4f"
          % (H2_TARGET_NEW, H2B_FLOOR, TOP_CAP))

    # ── ⓪-a 자 대조: 정확 스캔라인 hw ↔ r24 래스터 hw_at ────────────────────
    print("\n⓪-a 자 대조 — 새로 쓴 정확 스캔라인 반폭 vs r24 래스터 hw_at (같은 자여야 한다)")
    worst = 0.0
    for k in hats:
        polys = [p["pts"] for p in hats[k] if p["filled"] and p["sort"] >= SORT_EYES]
        fr = [p for p in hats[k] if p["filled"] and p["sort"] >= SORT_EYES]
        for y in (1.10, 0.90, 0.70, 0.50, 0.30, 0.10, -0.10):
            d = abs(_hw_exact(polys, y) - R24.hw_at(fr, y))
            worst = max(worst, d)
    print("   최대 불일치 %.5f R (래스터 격자 0.002 R 이내면 같은 자) → %s"
          % (worst, "일치" if worst <= 0.0025 else "★ 불일치 — 아래 값을 믿지 마라"))

    # ── ⓪ H-2 판정식이 털모자를 왜 놓쳤는가 — 상한만 있고 하한이 없다 ──────────
    print("\n⓪ 왜 털모자가 「통과」였나 — H-2 는 **상한만** 있다")
    print("   %-8s %-10s %-8s %-10s %-9s %s" % ("모자", "착용선", "옛목표", "옛판정", "앞층밑단", "가려짐 최대"))
    cur = dict(hats)
    for k in cur:
        w, ok = h2(cur[k])
        o = occl(cur[k], ei)
        print("   %-8s %-10s %+.2f    %-10s %+.4f    %.1f%%"
              % (k, ("%+.4f" % w) if w is not None else "덮임실패", R24.H2_TARGET[k],
                 ("통과" if (w is not None and ok and w <= R24.H2_TARGET[k] + 1e-6) else "실패"),
                 bottom_front(cur[k]), max(o.values())))
    print("   → 착용선은 「어디까지 내려와 덮는가」의 **하한**을 재는 자다. 그 아래로 얼마나 더 내려오는지는")
    print("     한 번도 재지 않았다. 가려짐을 정하는 것은 **앞층 채움 밑단**이고, 그 자가 규칙에 없었다.")
    print("   상관: 밑단 ↔ 최대 가려짐")
    for k in sorted(cur, key=lambda x: -bottom_front(cur[x])):
        print("     %-8s 밑단 %+.4f → %.1f%%" % (k, bottom_front(cur[k]), max(occl(cur[k], ei).values())))

    # ── ⓪-b 「꼬리 길이」 — 착용선과 앞층 밑단의 간격. 평행이동으로 안 변하는 조형 상수 ──
    print("\n⓪-b 꼬리 길이 = 착용선 − 앞층 밑단 (모자를 통째로 올려도 **안 변한다**)")
    print("   ★ 밑단은 **머리 폭 안(|x| ≤ %.4f R)** 에서 잰다 — 챙 끝(±1.8 R)이나 원반 옆 처짐은 얼굴을 못 가린다."
          % HEAD_R_COVER)
    print("   목표: 밑단 ≥ +%.2f 이고 착용선 ≤ +%.2f → 꼬리 ≤ %.3f R 이어야 산술적으로 가능하다"
          % (H2B_FLOOR, H2_TARGET_NEW, H2_TARGET_NEW - H2B_FLOOR))
    allh = dict(cur)
    allh["베레모(R21)"] = hats21["베레모"]
    allh["밀짚모자(R21)"] = hats21["밀짚모자"]
    print("   %-14s %-11s %-11s %-11s %s" % ("", "착용선", "밑단(전체)", "밑단(머리폭안)", "꼬리"))
    for k in allh:
        w, ok = h2(allh[k])
        b, bi = bottom_front(allh[k]), bottom_in_head(allh[k])
        print("   %-14s %-11s %+.4f     %+.4f     %s"
              % (k, ("%+.4f" % w) if (w is not None and ok) else "덮임실패", b, bi,
                 ("%.3f R %s" % (w - bi, "  ★ 꼬리가 길다" if (w - bi) > (H2_TARGET_NEW - H2B_FLOOR) else ""))
                 if (w is not None and ok) else "—"))

    # ── ① 6종 재맞춤 ────────────────────────────────────────────────────────
    print("\n① 재맞춤 — s(세로 배율) 는 1.0 에 가장 가깝게, 관 배수는 가장 작게")
    KX_TRY = [round(1.00 + 0.02 * i, 2) for i in range(0, 11)]      # 절대 배수 후보
    fixed, plan = {}, {}
    for k in ("천모자", "중절모", "왕관", "털모자"):
        if k in ("천모자", "중절모"):
            ratios = [round(a / CROWN_WIDEN_OLD[KIND_OF[k]], 6) for a in KX_TRY]
            abs_kx = KX_TRY
        else:
            ratios, abs_kx = [1.0], [1.0]
        b = search(hats[k], kx_ratios=ratios)
        if b is None:
            print("   %-8s ★ 해 없음" % k)
            continue
        kx_abs = abs_kx[ratios.index(b["kx_ratio"])]
        fixed[k] = b["pieces"]
        plan[k] = dict(b, kx_abs=kx_abs)
        print("   %-8s s %.3f · t %+.4f · 관배수 %.2f(현행 %.2f) → 착용선 %+.4f · 밑단 %+.4f · 꼭대기 %+.4f  [묶는 것: %s]"
              % (k, b["s"], b["t"], kx_abs, CROWN_WIDEN_OLD.get(KIND_OF[k], 1.0), b["wear"], b["bottom"], b["top"], b["bind"]))
    for k in ("베레모", "밀짚모자"):
        b = search(hats21[k])
        if b is None:
            print("   %-8s ★ 해 없음 — R21 좌표를 **평행이동만으로는** 못 맞춘다(꼬리 %.3f R > %.3f R)"
                  % (k + "(R21)", 0.336 if k == "베레모" else 0.0, H2_TARGET_NEW - H2B_FLOOR))
            continue
        fixed[k] = b["pieces"]
        plan[k] = dict(b, kx_abs=1.0)
        print("   %-8s s %.3f · t %+.4f (R21 좌표 위) → 착용선 %+.4f · 밑단 %+.4f · 꼭대기 %+.4f  [묶는 것: %s]"
              % (k + "(R21)", b["s"], b["t"], b["wear"], b["bottom"], b["top"], b["bind"]))

    # ── ①-b 털모자 대안: 세로압축 vs 균일축소 ────────────────────────────────
    print("\n①-b 털모자 — 세로만 누르기 vs 균일 축소(폭도 함께 줄인다)")
    for label, uni in (("세로압축(ky)", False), ("균일축소(u)", True)):
        r = search(hats["털모자"], uniform=uni)
        if r is None:
            print("   %-12s 해 없음" % label)
            continue
        o = occl(r["pieces"], ei)
        xs = [x for p in r["pieces"] for x, _ in p["pts"]]
        print("   %-12s s %.3f · t %+.4f → 착용선 %+.4f · 밑단 %+.4f · 꼭대기 %+.4f · 폭 %.3f R · 가려짐 %.1f~%.1f%%"
              % (label, r["s"], r["t"], r["wear"], r["bottom"], r["top"], max(xs) - min(xs),
                 min(o.values()), max(o.values())))

    # ── ①-c 천모자·중절모: 관 배수를 현행(1.12/1.18) 그대로 둘 때 ────────────
    print("\n①-c 천모자·중절모 — 관 배수를 **현행 그대로** 둘 때(배수를 안 건드리는 안)")
    for k in ("천모자", "중절모"):
        r = search(hats[k], kx_ratios=[1.0])
        if r is None:
            print("   %-8s 해 없음" % k)
            continue
        o = occl(r["pieces"], ei)
        print("   %-8s 관배수 %.2f 유지 · s %.3f · t %+.4f → 착용선 %+.4f · 밑단 %+.4f · 꼭대기 %+.4f · 가려짐 %.1f~%.1f%%"
              % (k, CROWN_WIDEN_OLD[KIND_OF[k]], r["s"], r["t"], r["wear"], r["bottom"], r["top"],
                 min(o.values()), max(o.values())))

    # ── ①-d 베레모 조형 처방 — 밑변 기울기 τ ────────────────────────────────
    print("\n①-d ★ 베레모 — 평행이동으로 못 푸는 이유와, 조형을 바꾸면 풀리는가")
    print("   R21 원안을 그냥 올렸을 때(τ=5.0 고정) — 「착용선 ≤ +0.45」와 「밑단 ≥ +0.28」이 동시에 성립하나:")
    for row in sweep(hats21["베레모"], ts=np.arange(0.0, 0.46, 0.04)):
        print("      t %+.3f → 착용선 %-10s 밑단 %+.4f · 꼭대기 %+.4f %s"
              % (row["t"], ("%+.4f" % row["wear"]) if (row["wear"] is not None and row["top_ok"]) else "덮임실패",
                 row["bottom"], row["top"],
                 "" if (row["wear"] is not None and row["top_ok"] and row["wear"] <= H2_TARGET_NEW
                        and row["bottom"] >= H2B_FLOOR) else "  ✗"))
    print("   ★ 세로 압축(s)으로도 못 산다 — 꼬리도 s 배로만 줄어서 0.332×s ≤ 0.170 이려면 s ≤ 0.512,")
    print("     즉 베레모를 **절반으로 납작하게** 눌러야 한다. 조형이 무너진다. 아래는 s=1.00 고정 탐색이다.")
    print("   ★ 꼬리의 정체는 **밑변 기울기**다 — R21 밑변은 왼끝 아이콘 y37 · 오른끝 y42 로 5u 기울어 있고")
    print("     그 5u × u 0.07 = 0.350 R 이 그대로 꼬리(0.332 R)다. 실물에서 기울어지는 것은 **판**이지")
    print("     **머리띠**가 아니다 — 띠를 수평으로 되돌리고 처짐은 띠 **바깥**으로 옮긴다.")
    print("   %-26s %-6s %-10s %-10s %-10s %-9s %s" % ("변주", "u", "dy(t)", "착용선", "밑단", "꼭대기", "가려짐"))
    beret_cand = []
    for mode, label in (("tilt", "(가) R21 원안 τ=5.0"), ("tilt3", "(가) 기울기 τ=3.0"),
                        ("level", "(나) 밴드·원반 수평"), ("sag", "(다) 밴드 수평+옆 처짐")):
        for u in (0.070, 0.072, 0.074, 0.076, 0.078, 0.080):
            pv = beret_variant("tilt" if mode == "tilt3" else mode, 3.0 if mode == "tilt3" else 5.0, u)
            r = search(pv, s_list=[1.0])
            if r is None:
                continue
            o = occl(r["pieces"], ei)
            xs = [x for p in r["pieces"] for x, _ in p["pts"]]
            print("   %-26s %.4f %+.4f    %+.4f    %+.4f    %+.4f   %.1f~%.1f%%  (폭 %.3f R)"
                  % (label, u, BERET_DY0 + r["t"], r["wear"], r["bottom"], r["top"],
                     min(o.values()), max(o.values()), max(xs) - min(xs)))
            beret_cand.append((mode, u, r, o))
            break
        else:
            print("   %-26s u 0.070~0.080 전 구간 해 없음" % label)
    pick = next((c for c in beret_cand if c[0] == "sag"), None) or (beret_cand[0] if beret_cand else None)
    if pick is not None:
        mode, u, r, o = pick
        fixed["베레모"] = r["pieces"]
        plan["베레모"] = dict(r, kx_abs=1.0, mode=mode, u=u)
        print("   → 채택 **%s · u %.4f · dy %+.4f** — 처짐(베레모의 정체)은 남기되 처지는 자리를"
              % (mode, u, BERET_DY0 + r["t"]))
        print("     머리·안경 밖(x ≥ +%.2f R)으로 옮긴다. 밑단(머리폭 안) %+.4f · 밑단(전체) %+.4f"
              % (16.0 * u, bottom_in_head(r["pieces"]), bottom_front(r["pieces"])))

    # ── ② 전/후 표 ──────────────────────────────────────────────────────────
    print("\n② 수정 전/후 — 착용선 · 앞층 밑단 · 꼭대기")
    print("   %-10s %-22s %-22s %s" % ("모자", "착용선 전 → 후", "앞층밑단 전 → 후", "꼭대기 전 → 후"))
    for k in ("천모자", "중절모", "털모자", "왕관", "베레모", "밀짚모자"):
        was = hats21[k] if k in hats21 and k in ("베레모", "밀짚모자") else hats[k]
        w0, ok0 = h2(hats[k])
        wr0 = ("%+.4f" % w0) if (w0 is not None and ok0) else "덮임실패"
        w21, ok21 = (h2(was) if k in ("베레모", "밀짚모자") else (None, None))
        w1 = plan[k]["wear"] if k in plan else None
        print("   %-10s %-22s %-22s %s"
              % (k, "%s → %+.4f" % (wr0, w1) if w1 is not None else "%s → —" % wr0,
                 "%+.4f → %+.4f" % (bottom_front(hats[k]), plan[k]["bottom"]) if k in plan else "—",
                 "%+.4f → %+.4f" % (top_of(hats[k]), plan[k]["top"]) if k in plan else "—"))
        if k in ("베레모", "밀짚모자"):
            print("       └ (경유) R21 원안: 착용선 %s · 밑단 %+.4f · 꼭대기 %+.4f"
                  % (("%+.4f" % w21) if (w21 is not None and ok21) else "덮임실패",
                     bottom_front(was), top_of(was)))

    # ── ③ 가려짐 6×6 ────────────────────────────────────────────────────────
    print("\n③ 가려짐 % — 안경 6종 × 모자 6종 (안경 잉크 면적 중 모자 앞층 채움에 덮인 비율)")
    print("   %-14s" % "" + "".join("%-11s" % e for e in eyes) + "범위")
    for k in ("천모자", "중절모", "털모자", "왕관", "베레모", "밀짚모자"):
        o0 = occl(hats[k], ei)
        print("   %-14s" % (k + " 전") + "".join("%-11s" % ("%.1f" % o0[e]) for e in eyes)
              + "%.1f~%.1f" % (min(o0.values()), max(o0.values())))
    print()
    for k in ("천모자", "중절모", "털모자", "왕관", "베레모", "밀짚모자"):
        if k not in plan:
            continue
        o1 = occl(fixed[k], ei)
        print("   %-14s" % (k + " 후") + "".join("%-11s" % ("%.1f" % o1[e]) for e in eyes)
              + "%.1f~%.1f" % (min(o1.values()), max(o1.values())))

    # ── ④ 손잡이 역산 ───────────────────────────────────────────────────────
    print("\n④ 프로덕션 손잡이 — 역산값")
    for k in ("천모자", "중절모", "왕관"):
        if k not in plan:
            continue
        b = plan[k]
        h = back_out_handoff(KIND_OF[k], b["s"], b["t"])
        print("   %-6s r17_model.HAT_FIT[%-9s] ky %.4f → **%.4f** · dy %.4f → **%.4f** (u %.4f 무변경)"
              % (k, '"%s"' % KIND_OF[k], h["ky_old"], h["ky"], h["dy_old"], h["dy"], h["u"]))
        if KIND_OF[k] in CROWN_WIDEN_OLD:
            print("          r19_model.CROWN_WIDEN[%-11s] %.2f → **%.2f**"
                  % ('"%s"' % KIND_OF[k], CROWN_WIDEN_OLD[KIND_OF[k]], b["kx_abs"]))
    if "털모자" in plan:
        b = plan["털모자"]
        f = back_out_furhat(b["s"], b["t"])
        print("   털모자   AccessoryShapeBuilder.Handoff.cs `case HeadBeanie`:")
        print("          new AccessoryWornTransform(0f, %.4ff, **%.4f**f, **%.5f**f, false)   (구: %.1f / %.5f)"
              % (f["scale"], f["scale_y"], f["dy"], f["scale_y_old"], f["dy_old"]))

    # ── ⑤ 실루엣 차 무회귀 ──────────────────────────────────────────────────
    print("\n⑤ 모자 6종 쌍별 실루엣 차(72×5° 프로필, 문턱 1.00획)")
    before = {k: (hats21[k] if k in hats21 else hats[k]) for k in R24.HATS}
    after = {k: fixed.get(k, before[k]) for k in R24.HATS}
    sb, sa = sil_matrix(before), sil_matrix(after)
    worst = sorted(sa.items(), key=lambda kv: kv[1])[:6]
    for pair, v in worst:
        print("   %-8s ↔ %-8s  %.2f → %.2f 획  %s"
              % (pair[0], pair[1], sb[pair], v, "★ 문턱 아래" if v < 1.0 else ""))
    print("   최소 %.2f 획 · 위반 %d 쌍" % (min(sa.values()), sum(1 for v in sa.values() if v < 1.0)))

    # ── ⑥ 액자·턱 ───────────────────────────────────────────────────────────
    print("\n⑥ 초상화 액자 · 턱")
    mx = max(top_of(after[k]) for k in after)
    who = max(after, key=lambda k: top_of(after[k]))
    print("   최고 아이템 %s %.4f R < 액자 상수 %.3f (여유 %+.4f) — 상수 무변경"
          % (who, mx, TOP_LIMIT, TOP_LIMIT - mx))
    for k in after:
        print("   %-8s 꼭대기 %+.4f · 전층 밑 %+.4f (턱 %.1f)" % (k, top_of(after[k]), bottom_all(after[k]), CHIN))

    # ── ⑥-b 규칙 1(잉크 사각형 최소변) · 자기교차 ──────────────────────────
    print("\n⑥-b 규칙 1 — 잉크 사각형 최소변 ≥ 1.5 × 명목 획. 세로 압축(중절모 0.965 · 털모자 0.845)이 여기 걸린다")
    import rig
    NOM = 0.13845                      # 슬롯 명목 획(HEAD)
    LIM = 1.5 * NOM
    for k in ("천모자", "중절모", "털모자", "왕관", "베레모", "밀짚모자"):
        rows = []
        for src, lab in ((before[k], "전"), (after[k], "후")):
            worst, who = 1e9, ""
            for p in src:
                if not p["filled"]:
                    continue
                xs = [q[0] for q in p["pts"]]
                ys = [q[1] for q in p["pts"]]
                m = min(max(xs) - min(xs), max(ys) - min(ys))
                if m < worst:
                    worst, who = m, p["name"]
            rows.append((lab, worst, who))
        (_, w0, n0), (_, w1, n1) = rows
        print("   %-8s 전 %.4f(%s) → 후 %.4f(%s)  하한 %.5f  %s"
              % (k, w0, n0, w1, n1, LIM, "통과" if w1 >= LIM else "★ 위반"))
    print("   자기교차: ", end="")
    bad = []
    for k in after:
        for p in after[k]:
            if p["loop"] and rig.self_intersects(p["pts"]):
                bad.append("%s/%s" % (k, p["name"]))
    print("없음" if not bad else "★ " + ", ".join(bad))

    # ── ⑥-c 카드 좌표 불변 증명 ────────────────────────────────────────────
    print("\n⑥-c 카드 좌표 — 이 라운드의 변경은 전부 **몸 표면 전용**인가")
    print("   인계본 4종: 카드 배열은 `surfaces: 2`(clothhat/fedora/crown) · `surfaces: 0`(furhat) 이고")
    print("   바꾸는 손잡이는 HAT_FIT(dy·ky) → `*_worn` 배열, CROWN_WIDEN → `*_worn` 배열,")
    print("   털모자는 `WornTransformCode` (Body 표면에서만 적용, Card 는 AccessoryWornTransform.None).")
    print("   ⇒ 카드 좌표를 만드는 경로에 이 세 손잡이가 **들어가지 않는다** — 값 대조는 생성 후 diff 로 한다(coder).")
    print("   베레모·밀짚모자: R21 안 자체가 미이식이라 프로덕션 카드 좌표는 v1 그대로다(이 라운드 변경 0).")

    # ── ⑦ 반폭 프로필(후) ───────────────────────────────────────────────────
    print("\n⑦ 재맞춤 뒤 앞층 채움 반폭 vs 머리 현 — 목표 대역(+1.18 → +0.45)")
    ys = [1.15, 1.05, 0.95, 0.85, 0.75, 0.65, 0.55, 0.45, 0.40, 0.35, 0.30]
    print("   %-8s" % "y" + "".join("%8.2f" % y for y in ys))
    need = [math.sqrt(max(HEAD_R_COVER ** 2 - y * y, 0.0)) + COVER_MARGIN for y in ys]
    print("   %-8s" % "필요" + "".join("%8.3f" % n for n in need))
    for k in ("천모자", "중절모", "털모자", "왕관", "베레모", "밀짚모자"):
        hw = half_width_profile(after[k], ys)
        print("   %-8s" % k + "".join("%8.3f" % h for h in hw))


if __name__ == "__main__":
    main()
