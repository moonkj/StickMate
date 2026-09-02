# -*- coding: utf-8 -*-
"""색각 이상 시뮬레이터 — Viénot·Brettel·Mollon 1999 (design-art, 2026-09-02)

★ 이 파일도 **쓰기 전에 교정한다.** 교정이 하나라도 깨지면 아무 숫자도 내지 않고 죽는다.

왜 필요한가: 등급 4단계를 색으로만 나누면 색각 이상에서 몇 단이 남는가를 재야 한다.
문서에 "색맹 고려함"이라고 쓰는 것은 측정이 아니다.

교정에 외부 기준표를 못 구했다(ΔE와 같은 사정 — PALETTE_SPEC §8 "미확인").
그래서 **모델 자신이 반드시 만족해야 하는 성질 넷**으로 교정한다. 이건 취향이 아니라
이색각(dichromat) 사영의 정의에서 나오는 항등식이다.

  C1 무채축 불변    : 회색은 어떤 유형에서도 자기 자신으로 간다 (2색각도 회색은 회색으로 본다)
  C2 멱등           : sim(sim(c)) == sim(c)  — 사영이므로 두 번 해도 같다
  C3 혼동선 붕괴    : 한 원추 응답만 다른 두 색은 그 유형에서 **같은 색**이 된다
                      (protan은 L만 다른 쌍, deutan은 M만, tritan은 S만)
  C4 자유도 2       : 출력이 LMS 공간의 한 평면 위에 있다 (사영 행렬의 랭크 2)

행렬 출처: Viénot F., Brettel H., Mollon J.D. (1999), "Digital video colourmaps for
checking the legibility of displays by dichromats", Color Research & Application 24(4).
Hunt-Pointer-Estevez LMS. **선형 RGB에 적용한다**(논문 그대로 — 감마 보정된 값에 적용하는
구현이 흔한데 그건 논문과 다르다).
"""
import sys, os, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL

# 선형 RGB -> LMS
RGB2LMS = ((17.8824, 43.5161, 4.11935),
           (3.45565, 27.1554, 3.86714),
           (0.0299566, 0.184309, 1.46709))

# LMS -> 선형 RGB (위 행렬의 역행렬 — 아래 _inv3로 실제 계산해 확인한다)
def _inv3(m):
    a, b, c = m[0]; d, e, f = m[1]; g, h, i = m[2]
    det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g)
    return (((e * i - f * h) / det, (c * h - b * i) / det, (b * f - c * e) / det),
            ((f * g - d * i) / det, (a * i - c * g) / det, (c * d - a * f) / det),
            ((d * h - e * g) / det, (b * g - a * h) / det, (a * e - b * d) / det))

LMS2RGB = _inv3(RGB2LMS)

# 이색각 사영 (LMS 공간)
DICHROMAT = {
    "protan": ((0.0, 2.02344, -2.52581), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)),
    "deutan": ((1.0, 0.0, 0.0), (0.494207, 0.0, 1.24827), (0.0, 0.0, 1.0)),
    "tritan": ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (-0.395913, 0.801109, 0.0)),
}
TYPES = ("protan", "deutan", "tritan")
KOR = {"protan": "1형(적색맹)", "deutan": "2형(녹색맹)", "tritan": "3형(청색맹)"}


def _mul(m, v):
    return tuple(sum(m[i][j] * v[j] for j in range(3)) for i in range(3))


def _unlin(u):
    u = max(0.0, min(1.0, u))
    return 12.92 * u if u <= 0.0031308 else 1.055 * (u ** (1 / 2.4)) - 0.055


def rgb_to_lms(rgb):
    return _mul(RGB2LMS, tuple(CL.lin(v / 255.0) for v in rgb))


def lms_to_rgb(lms, quantize=True):
    linrgb = _mul(LMS2RGB, lms)
    out = tuple(_unlin(v) * 255.0 for v in linrgb)
    return tuple(int(round(max(0.0, min(255.0, v)))) for v in out) if quantize else out


def sim(rgb, kind):
    """(0..255)^3 -> 그 유형이 보는 색 (0..255)^3."""
    return lms_to_rgb(_mul(DICHROMAT[kind], rgb_to_lms(rgb)))


# ============================================================================
# 교정
# ============================================================================
def calibrate(verbose=True):
    rows, ok = [], True

    def chk(name, got, want, tol):
        nonlocal ok
        good = abs(got - want) <= tol
        ok = ok and good
        rows.append(("PASS" if good else "FAIL", name, got, want, tol))

    # 역행렬이 진짜 역행렬인가 (이걸 안 재면 아래 전부가 무의미하다)
    ident = [[sum(RGB2LMS[i][k] * LMS2RGB[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
    err = max(abs(ident[i][j] - (1.0 if i == j else 0.0)) for i in range(3) for j in range(3))
    chk("LMS 역행렬 오차", err, 0.0, 1e-9)

    # C1 무채축 불변 — 회색 0..255 전부
    for k in TYPES:
        worst = max(max(abs(a - b) for a, b in zip(sim((g, g, g), k), (g, g, g)))
                    for g in range(0, 256))
        chk(f"C1 무채축 불변 {KOR[k]} (최대 채널 오차)", worst, 0.0, 1.0)

    # C2 멱등 — 격자 색 전수
    grid = [(r, g, b) for r in (0, 51, 102, 153, 204, 255)
            for g in (0, 51, 102, 153, 204, 255) for b in (0, 51, 102, 153, 204, 255)]
    for k in TYPES:
        worst = 0.0
        for c in grid:
            s1 = sim(c, k)
            s2 = sim(s1, k)
            worst = max(worst, CL.dE(s1, s2))
        chk(f"C2 멱등 {KOR[k]} (최대 ΔE)", worst, 0.0, 1.0)

    # C3 혼동선 붕괴 — 잃은 원추만 흔들면 같은 색이 되어야 한다
    axis = {"protan": 0, "deutan": 1, "tritan": 2}
    for k in TYPES:
        worst = 0.0
        for base in [(0.35, 0.35, 0.35), (0.55, 0.40, 0.30), (0.25, 0.45, 0.60)]:
            lms0 = _mul(RGB2LMS, base)
            for f in (0.80, 0.90, 1.10, 1.25):
                lms1 = list(lms0); lms1[axis[k]] *= f
                a = lms_to_rgb(_mul(DICHROMAT[k], lms0))
                b = lms_to_rgb(_mul(DICHROMAT[k], tuple(lms1)))
                worst = max(worst, CL.dE(a, b))
        chk(f"C3 혼동선 붕괴 {KOR[k]} (최대 ΔE)", worst, 0.0, 1.0)

    # C4 사영 랭크 2 — 행렬식 0
    for k in TYPES:
        m = DICHROMAT[k]
        det = (m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1])
               - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
               + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]))
        chk(f"C4 사영 행렬식 {KOR[k]}", det, 0.0, 1e-12)

    if verbose:
        print("=== 색각 시뮬레이터 교정 ===")
        for st, nm, got, want, tol in rows:
            print(f"  {st}  {nm:42s} {got:12.6f}  (정답 {want}, 허용 {tol})")
        print(f"  교정 판정: {'유효' if ok else '무효 — 이 파일의 모든 숫자를 폐기하십시오'}")
        print()
    if not ok:
        raise SystemExit("cvd.py 교정 실패 — 숫자를 내지 않고 중단합니다.")
    return True


if __name__ == "__main__":
    CL.calibrate()
    calibrate()
    print("보기 — 등급 램프 후보 B가 각 유형에서 어떻게 보이는가")
    ramp = ["#9C978C", "#BCAC8B", "#DBBD7F", "#F9CB70"]
    print(f"{'유형':12s} " + " ".join(f"{h:>9s}" for h in ramp))
    print(f"{'정상':12s} " + " ".join(f"{h:>9s}" for h in ramp))
    for k in TYPES:
        print(f"{KOR[k]:12s} " + " ".join(f"{CL.rgb2hex(sim(CL.hex2rgb(h), k)):>9s}" for h in ramp))
