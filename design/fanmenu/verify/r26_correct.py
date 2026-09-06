"""R26 §0-2 — design-art R14 §5 표의 캡 규약 오류를 재현하고 정정한다."""
import math
from fanglyph import *

print("=" * 92)
print("§0-2  ART_FAN_MENU_LANGUAGE §5 표 정정 — UiChrome.AddStroke 의 캡 규약")
print("=" * 92)
print("""
  AddStroke(길이 L, 두께 t) 은 <보이는 총 길이>가 L 인 캡슐이다.
  즉 둥근 캡의 <중심>은 축 위 ±(L/2 − t/2) 에 있고, 코어의 끝점이 ±L/2 다.
  R14 는 캡 중심을 ±L/2 로 잡은 뒤 반지름 t/2 를 <또> 뺐다 —
  두 캡슐이 마주 보는 쌍마다 여유를 (t_a + t_b)/2 만큼 <작게> 본다.
""")


def cap_centers(L, t, ang, cx, cy):
    h = max(0.0, L / 2 - t / 2)
    a = math.radians(ang)
    return ((cx - math.cos(a) * h, cy - math.sin(a) * h),
            (cx + math.cos(a) * h, cy + math.sin(a) * h))


def seg_pt_dist(p, a, b):
    vx, vy = b[0] - a[0], b[1] - a[1]
    wx, wy = p[0] - a[0], p[1] - a[1]
    L2 = vx * vx + vy * vy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, (wx * vx + wy * vy) / L2))
    return math.hypot(wx - t * vx, wy - t * vy)


def cap_gap(A, B):
    """두 캡슐(코어) 사이 최소 간극 — 해석해."""
    (a0, a1), ta = A
    (b0, b1), tb = B
    d = min(seg_pt_dist(a0, b0, b1), seg_pt_dist(a1, b0, b1),
            seg_pt_dist(b0, a0, a1), seg_pt_dist(b1, a0, a1))
    return d - ta / 2 - tb / 2


rows = []

# ① 스톱워치 — 분침 끝 ↔ 링 안쪽 (여기는 규약이 우연히 일치한다)
tip = 3.25 + (6.5 / 2 - 1.0) + 1.0          # = 6.5
rows.append(("① 스톱워치", "분침 끝 ↔ 링 안쪽", 1.50, 8.0 - tip))
# ★ R14 가 못 본 쌍
rows.append(("① 스톱워치", "★ 용두 ↔ 링 (R14 미측정)", None,
             (11.5 - 4.0 / 2) - 10.0))

# ② 스틱맨 — 다리 <끝> 사이 (실루엣 벌어짐)
pe = (0.0, -4.5)
LL = cap_centers(7.0, 1.8, -106.0, pe[0] + polar(-106, 3.5)[0], pe[1] + polar(-106, 3.5)[1])
LR = cap_centers(7.0, 1.8, -74.0, pe[0] + polar(-74, 3.5)[0], pe[1] + polar(-74, 3.5)[1])
tipgap = math.hypot(LL[1][0] - LR[1][0], LL[1][1] - LR[1][1]) - 1.8
rows.append(("② 스틱맨", "다리 끝 사이", 2.06, tipgap))
rows.append(("② 스틱맨", "★ 팔 끝 ↔ 다리 (R14 미측정)", None,
             cap_gap((cap_centers(6.0, 1.8, -140.0,
                                  polar(-140, 3)[0], 3.5 + polar(-140, 3)[1]), 1.8),
                     (LL, 1.8))))

# ③ 체크리스트 — 박스 구멍
rows.append(("③ 체크리스트", "빈 박스 구멍", 2.50, 4.5 - 2 * 1.0))
# ★ 박스는 캡슐이 아니라 <두께 1.0 의 사각 띠>라 cap_gap 을 쓸 수 없다 —
#   래스터(measure) 값을 그대로 인용한다.
_m3 = measure(cur_checklist())
_d3 = dict(((a, b), c) for a, b, c in _m3["clears"])
rows.append(("③ 체크리스트", "★ 체크 ↔ 아래 박스 (R14 미측정)", None, _d3[("Box1", "CheckLong")]))

# ④ 확성기 — 입 ↔ 소리선  /  ★ 진짜 최악
mouth = (cap_centers(11.6, 2.0, 90.0, 5.0, 0.0), 2.0)
wave = (cap_centers(4.6, 1.6, 30.0, 9.6, 3.4), 1.6)
neck = (cap_centers(5.6, 2.0, 90.0, -8.4, 0.0), 2.0)
horn = (cap_centers(13.0, 2.0, 13.0, -1.6, 5.0), 2.0)
rows.append(("④ 확성기", "입 ↔ 소리선", 0.81, cap_gap(mouth, wave)))
rows.append(("④ 확성기", "★ 나팔 ↔ 목 (R14 미측정 · 진짜 최악)", None, cap_gap(horn, neck)))

# ⑤ 전원 — 세로획 ↔ 틈 가장자리
stem = 20.0 * 0.55
gap_edge_in = (8.0 * math.cos(math.radians(90 - POWER_GAP_DEG / 2)),
               8.0 * math.sin(math.radians(90 - POWER_GAP_DEG / 2)))
rows.append(("⑤ 전원", "세로획 ↔ 틈 가장자리", 3.23,
             gap_edge_in[0] - 1.0))

print(f"  {'기호':13s} {'가장 좁은 곳':38s} {'R14':>6s} {'정정':>7s} {'차':>7s}  {'W배':>5s} 판정")
print("  " + "-" * 88)
for g, what, old, new in rows:
    W = new / SYMBOL_STROKE
    verdict = "통과" if new >= 1.5 * SYMBOL_STROKE else "미달"
    o = f"{old:6.2f}" if old is not None else "     —"
    d = f"{new-old:+7.2f}" if old is not None else "      —"
    print(f"  {g:13s} {what:38s} {o} {new:7.2f} {d}  {W:5.2f} {verdict}")

print()
print("  ⇒ R14 의 <판정>(4/5 미달 · ④가 최악 · 지휘봉 교체)은 살아남는다.")
print("    바뀌는 것은 <숫자와 처방 지점>이다: ④의 병은 소리선이 아니라 <목>이고,")
print("    ①에는 R14 가 측정하지 않은 <용두 ↔ 링 겹침(−0.5pt)>이 있다.")
