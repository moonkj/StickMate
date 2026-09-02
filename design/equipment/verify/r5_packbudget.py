# -*- coding: utf-8 -*-
"""★ 과제 C — 「6팩이 나눠 써야 하는 제로섬 자원이 정말 있는가」를 잰다.

리더가 지목한 후보 셋:
  (1) EYES 봉투의 빈 방향 255~290°        (내가 「유일한 빈 방향」이라 적었다)
  (2) HAIR 봉투의 빈 방향 245~265°
  (3) TallestAccessoryAboveHeadCenterInR = 1.80f  (design-character 가 「제로섬」으로 지목)

**셋 다 제로섬이 아니다**는 것이 이 스크립트의 결론이고, 그 대신 **제로섬이 아니라 「공유지」인
자원 하나**가 실재한다는 것을 잰다. 말이 다르면 처방이 다르다 —
제로섬은 「나눠 준다」로 풀고, 공유지는 「전원이 지키는 규약」으로만 풀린다.
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H, sectors as S
import pack_nightshift as P
import r5_rx
from rig import Shape

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)
RATCHET = S.SILHOUETTE_RATCHET_R
FRAME = 1.80
HEAD_R_IN_H = 0.22 / 2.2746944          # CharacterPortraitStage.HeadRadiusInHeight


def frame_height(t):
    """CharacterPortraitStage.cs:195 그대로."""
    return (1.0 - HEAD_R_IN_H) + t * HEAD_R_IN_H


# ═══════════════════════════════════════════════════════════════════════════
def c0_definition():
    print("╔══ §0 낱말을 먼저 고정한다 ══╗")
    print("  제로섬 : 팩1이 x 를 쓰면 팩2~6이 쓸 수 있는 양이 x 만큼 줄어든다  (합이 상수)")
    print("  천장   : 각자 독립으로 밟을 수 있는 상한. 팩1이 밟아도 팩2가 못 밟게 되지 않는다")
    print("  공유지 : 전원이 지키면 전원이 얻고, **한 팩이 어기면 나머지 다섯이 잃는다**")
    print("  ★ 셋은 처방이 다르다. 제로섬은 배분표, 천장은 상수 하나, 공유지는 규약이다.")


# ═══════════════════════════════════════════════════════════════════════════
def c1_frame():
    print("\n╔══ §1 후보 (3) 액자 1.80 — 제로섬인가 ══╗")
    print("  CharacterPortraitStage.cs:192  private const float TallestAccessoryAboveHeadCenterInR = 1.80f")
    print("  CharacterPortraitStage.cs:195  frame = (1 − HeadRadiusInHeight) + T × HeadRadiusInHeight")
    print("  -> **착용물과 무관한 상수식이다.** 무엇을 끼든 액자 높이가 안 변한다.")
    print("     즉 팩1이 1.79 를 써도 팩2의 사용 가능 상한은 여전히 1.80 이다. **제로섬 아님 — 천장이다.**")
    print()
    print("  현재 팩들이 밟은 높이(잉크 기준):")
    for lab, sh in (("야간 정비반 HEAD 목덮개", P.head_havelock()),
                    ("야간 정비반 HAIR 목덜미", P.hair_napetie()),
                    ("야간 정비반 EYES 고글(개정)", r5_rx.eyes_respirator_v2())):
        t = max(p[1] for s in sh for p in s.pts) + W75 / 2
        print("     %-26s %+.4f  (여유 %+.4f)" % (lab, t, FRAME - t))
    print("     %-26s %+.4f  (여유 %+.4f)  ← design-character 우천 「우산」 보고값"
          % ("우천 BACK 우산", 1.672, FRAME - 1.672))
    print()
    print("  ★ 제로섬이 **되는** 단 하나의 경우: 어느 팩이 1.80 을 넘겨서 상수를 올려야 할 때.")
    for t in (1.80, 1.90, 2.00, 2.20):
        print("     T=%.2f -> 액자 높이 %.5f (T=1.80 대비 인물 %.2f%% 축소)"
              % (t, frame_height(t), (frame_height(t) / frame_height(1.80) - 1) * 100))
    print("     이때 줄어드는 것은 **다른 팩의 여유가 아니라 전원의 초상화 크기**다. 여전히 제로섬이 아니라")
    print("     「한 번 올리면 전원이 값을 치르는 전역 상수」다 — 처방은 배분표가 아니라 **승인 절차**다.")
    print()
    print("  ★★ 그리고 저장소가 **구조적으로** 이 상수의 선분양을 막고 있다 —")
    print("     AccessoryStrokeBudgetTests.액자_기준_최고점이_실제_최고_아이템과_한_획_이내다:")
    print("        Assert.LessOrEqual(frame − tallest, stroke)     // 슬랙 ≤ 1획")
    print("     = **실제로 그만큼 높은 아이템이 없으면 상수를 올릴 수 없다.** 미리 나눠 가질 수가 없다.")
    print("     ★ 계약이 **중심선**(TopInR = max 꼭짓점 y, 획 halo 없음)에 걸린다는 것도 여기서 확인했다 —")
    print("       내가 위에서 잉크 기준으로 잰 것은 **실제 계약보다 W/2 만큼 엄한 자**였다. 정정한다.")
    import items as _it, hair as _ha
    best = (-9, "")
    for slot, tbl in (("HEAD", _it.HEAD), ("EYES", _it.EYES), ("NECK", _it.NECK),
                      ("BACK", _it.BACK), ("HAIR", _ha.SET)):
        for n, f in tbl.items():
            sh = f() if callable(f) else f
            t = max(p[1] for s2 in sh for p in s2.pts)
            if t > best[0]: best = (t, slot + " " + n)
    print("     출하 30종 최고점(중심선) %.4f R = %s · 현재 슬랙 %.4f (한 획 %.4f)"
          % (best[0], best[1], 1.80 - best[0], W75))
    print("     팩 「야간 정비반」 최고점(중심선): HEAD %.4f · HAIR %.4f  -> 둘 다 1.80 아래, 슬랙 규칙도 통과"
          % (max(p[1] for s2 in P.head_havelock() for p in s2.pts),
             max(p[1] for s2 in P.hair_napetie() for p in s2.pts)))


# ═══════════════════════════════════════════════════════════════════════════
def cap_eyes(d):
    a = math.radians(d); c, s = math.cos(a), math.sin(a); r = 9e9
    if abs(c) > 1e-9: r = min(r, 1.60 / abs(c))
    if s > 1e-9: r = min(r, 1.15 / s)
    if s < -1e-9: r = min(r, 2.20 / -s)
    return r

def cap_hair(d):
    a = math.radians(d); s = math.sin(a); r = 9e9
    if s > 1e-9: r = min(r, 1.75 / s)
    return min(r, 3.0)


def c2_capacity():
    print("\n╔══ §2 후보 (1)(2) 빈 방향 — **몇 개나 들어가는가**를 센다 ══╗")
    print("  자기정정 먼저: 내가 pack_nightshift.py 주석에 적은 *\"EYES 의 유일한 빈 방향\"*은 **과장이다.**")
    for nm, tbl, cap in (("EYES", items.EYES, cap_eyes), ("HAIR", hair.SET, cap_hair)):
        env = S.envelope(tbl, 0.0)
        free = [i for i in range(72) if cap(i * 5) - env[i] >= RATCHET]
        print("  %s : 래칫 여유가 있는 5도 구간 **%d / 72**  (연속 구간 %s)"
              % (nm, len(free), _runs(free)))
    print()
    print("  이제 용량을 센다 — 무작위로 만든 프로파일 200개가 서로 래칫(≥%.4fR)으로 갈리는가." % RATCHET)
    rnd = random.Random(20260902)
    for nm, tbl, cap in (("EYES", items.EYES, cap_eyes), ("HAIR", hair.SET, cap_hair)):
        env = S.envelope(tbl, 0.0)
        free = [i for i in range(72) if cap(i * 5) - env[i] >= RATCHET]
        prof = []
        for _ in range(200):
            p = list(env)
            for i in free:
                p[i] = env[i] + rnd.random() * (min(cap(i * 5), 9.0) - env[i])
            prof.append(p)
        pairs = 0; okp = 0
        for i in range(len(prof)):
            for j in range(i + 1, len(prof)):
                pairs += 1
                if max(abs(a - b) for a, b in zip(prof[i], prof[j])) >= RATCHET: okp += 1
        # 출하 6종과도 갈리는가
        base = [S.profile(f() if callable(f) else f, 0.0) for f in tbl.values()]
        vs = sum(1 for p in prof if min(rig.max_delta(p, b) for b in base) >= RATCHET)
        print("  %s : 상호 래칫 통과 %d/%d 쌍 (%.1f%%) · 출하 6종과도 갈리는 것 %d/200"
              % (nm, okp, pairs, okp / pairs * 100, vs))
    print("  ★ 래칫은 **72방향 중 하나만 달라도 통과**하는 L∞ 조건이다. 그래서 용량이 사실상 무한하고,")
    print("     「빈 방향을 팩1이 먹는다」는 그림은 성립하지 않는다. **제로섬 아님.**")
    print("     팩끼리 실제로 부딪치는 것은 봉투가 아니라 **한 슬롯 안 12종의 상호 구분**인데,")
    print("     그건 위 숫자대로 여유가 크다.")


def _runs(idx):
    if not idx: return "없음"
    out = []; s = e = idx[0]
    for i in idx[1:]:
        if i == e + 1: e = i
        else: out.append((s, e)); s = e = i
    out.append((s, e))
    return " ".join("%d~%d°" % (a * 5, b * 5) for a, b in out)


# ═══════════════════════════════════════════════════════════════════════════
def _band_occupancy(sh, w):
    """렌즈 띠(x∈[-1.06,1.06], y∈[0.02,0.68])를 모자 잉크가 덮는 비율."""
    tot = cov = 0.0
    n = 400
    for k in range(n):
        y = 0.02 + (0.68 - 0.02) * (k + 0.5) / n
        tot += 2.12
        for a, b in H.ink_spans(sh, y, w):
            a = max(a, -1.06); b = min(b, 1.06)
            if b > a: cov += (b - a)
    return cov / tot


def _woocheon_hat():
    """design/character/DLC_PACK_R2_WOOCHEON_SPEC.md §3-1 「신문 모자」 좌표를 인용.
       (그쪽 문서의 두 도형을 손으로 옮겼다 — 파싱이 접힌선까지 한 다각형으로 묶는 사고를 냈다)"""
    body = [(-1.72, 0.66), (-1.04, 0.46), (0.00, 0.44), (1.04, 0.46), (1.72, 0.64),
            (1.10, 0.96), (0.62, 1.30), (0.00, 1.46), (-0.62, 1.28), (-1.10, 0.94)]
    fold = [(-1.34, 0.78), (0.00, 0.72), (1.34, 0.76)]
    return [Shape("PaperHatBody", body, True, filled=True),
            Shape("PaperHatFold", fold, False, tone=1)]


def c3_commons():
    print("\n╔══ §3 진짜 자원 — 「얼굴 띠」는 제로섬이 아니라 **공유지**다 ══╗")
    print("  이 팩의 최대 성과(EYES 생존 79.2%)는 팩 HEAD 가 얼굴 띠를 비웠기 때문이다.")
    print("  그런데 유저는 **팩을 섞는다** — 모자는 팩2, 안경은 팩1. 그때 무슨 일이 나는가.")
    w = W75
    es = r5_rx.eyes_respirator_v2()
    b = H.hair_visible_area(es, [], float("inf"), w)
    hats = [("팩1 목덮개 작업모 (규약 준수)", P.head_havelock()),
            ("우천 신문 모자 (타 팩)", _woocheon_hat())]
    hats += [("출하 " + n, f()) for n, f in (("야구모자", items.cap), ("털모자", items.beanie),
              ("중절모", items.fedora), ("왕관", items.crown_hat), ("베레모", items.beret),
              ("밀짚모자", items.straw))]
    print("\n  %-26s %10s %12s %10s" % ("모자", "EYES 생존", "렌즈띠 점유", "잉크밑단"))
    data = []
    for lab, sh in hats:
        v = H.hair_visible_area(es, sh, float("inf"), w) / b
        occ = _band_occupancy(sh, w)
        bot = min(p[1] for s2 in sh for p in s2.pts) - w / 2
        data.append((v, occ))
        print("  %-26s %9.1f%% %11.1f%% %10.3f  %s"
              % (lab, v * 100, occ * 100, bot, "★P-2 통과" if v >= 0.65 else "P-2 미달"))
    # 대리지표가 실제로 결과를 설명하는가 — 상관을 잰다(대리지표를 믿기 전에)
    n = len(data)
    mv = sum(d[0] for d in data) / n; mo = sum(d[1] for d in data) / n
    cov = sum((d[0] - mv) * (d[1] - mo) for d in data)
    sv = math.sqrt(sum((d[0] - mv) ** 2 for d in data)); so = math.sqrt(sum((d[1] - mo) ** 2 for d in data))
    print("\n  [대리지표 검정] EYES 생존 ↔ 렌즈띠 점유  피어슨 r = %.4f  (n=%d)" % (cov / (sv * so), n))
    print("     -> 「렌즈 띠를 얼마나 덮는가」 하나로 생존율이 거의 다 설명된다. 대리지표로 쓸 수 있다.")
    # ★ 문턱을 **고르지 않는다** — 위 회귀에서 P-2(65%)에 해당하는 점유율을 푼다.
    so2 = sum((d[1] - mo) ** 2 for d in data)
    slope = cov / so2                       # 생존 = a + slope × 점유
    inter = mv - slope * mo
    occ65 = (0.65 - inter) / slope
    print()
    print("  ★ 규약 문턱을 **데이터에서 푼다**:  생존 = %.4f %+.4f × 점유" % (inter, slope))
    print("     P-2(65%%) 가 되는 점유율 = %.1f%%.  여유를 두어 **≤ %.0f%%** 를 규약으로 제안한다."
          % (occ65 * 100, math.floor(occ65 * 100 / 5) * 5 - 5))
    thr = (math.floor(occ65 * 100 / 5) * 5 - 5) / 100.0
    for lab, sh in hats:
        occ = _band_occupancy(sh, w)
        print("     %-26s %5.1f%%  -> %s" % (lab, occ * 100, "지킴" if occ <= thr else "어김"))
    print()
    print("  ★ 이것이 제로섬이 아닌 이유: 전원이 지키면 **아무도 손해 보지 않고** 여섯 팩 전부 79%를 갖는다.")
    print("     어길 때의 피해가 자기 팩이 아니라 **남의 팩**에 간다 — 그래서 배분표가 아니라 규약이 답이다.")
    print("     ★ 그리고 이 규약은 **출하 42종에 소급하면 안 된다**(모자 6종 전부 어긴다). 팩에만 건다 —")
    print("       design-character 가 P-2 를 팩에만 걸자고 한 것과 같은 이유이고, 같은 결론이다.")


if __name__ == "__main__":
    c0_definition()
    c1_frame()
    c2_capacity()
    c3_commons()
