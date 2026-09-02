# -*- coding: utf-8 -*-
"""★ 슬롯 형상 예산(capacity) — "슬롯당 몇 종까지 실루엣 하한을 지키며 들어가는가".

왜 이 파일이 필요한가
---------------------
`product-strategy` 근사: *"슬롯당 12종이면 5/7 슬롯이 하한을 깬다"*
(HEAD 0.68 · EYES 0.76 · NECK 0.90 · FX 0.74 · **PET 0.58**, 하한 1.00).
그쪽이 스스로 **1차원 근사**라고 적었고 `game-architect`가 §8-8-d에서 **미확인**으로 남겼다.

그 근사의 형태는 이렇다(재현했다 — 아래 `oned_forecast`):
    "아이템이 2배가 되면 쌍별 최소가 절반이 된다"
이건 **아이템을 수직선 위의 점으로 볼 때만** 참이다. 그런데 이 저장소가 실제로 재는 것은

    프로파일 = 72구간 x 5도의 **최대 반경 벡터** (rig.profile)
    두 아이템의 차 = 그 벡터들의 **L∞ 거리** (rig.max_delta)

즉 **72차원 상자 안의 L∞ 패킹**이다. 1차원 직관은 여기서 성립하지 않는다.

이 파일이 재는 것
-----------------
(A) **구성적 하한** — 서로 겹치지 않는 예약 대역(sector)을 몇 개 놓을 수 있는가.
    아래 정리가 성립하면 **대역 수 = 추가 가능 종수**다.

    정리. 새 아이템 N개가 각각 자기 대역 s_k(서로 겹치지 않음)를 갖고
      (i) 자기 대역 밖에서는 **기존 봉투를 넘지 않는다**
      (ii) 자기 대역 안 어느 구간에서 **기존 봉투 + c** 이상 나간다
    이면 {기존 6종} ∪ {새 N종}의 **모든 쌍**이 L∞로 c 이상 떨어진다.
      · 새 A vs 기존 B : A의 대역 구간 i에서 p_A(i) ≥ env(i)+c ≥ p_B(i)+c
      · 새 A vs 새 B   : 같은 구간에서 (i)에 의해 p_B(i) ≤ env(i) 이므로 차 ≥ c
    (조건 (i)은 **충분조건**이다. 실제 아이템이 살짝 넘어도 직접 측정이 판정한다 — (B).)

(B) **직접 측정** — 실제 좌표를 표에 넣고 쌍별 최소를 다시 잰다(`sectors.pairwise_table`).
    (A)는 "적어도 몇 개"를 보증하고, (B)는 "지금 낸 것이 실제로 통과하는가"를 본다.

도달 상자(reach box)
--------------------
대역을 아무 반경까지나 쓸 수 있는 게 아니다. `verify.py`가 슬롯마다 거는 **경계 단언**이
그대로 상자다(턱 아래 금지 / 액자 / 목 아래 …). 그 상자를 방향별 반경 상한
r_cap(θ)로 옮겨서 쓴다. **내가 지어낸 값이 아니라 게이트가 이미 거는 값이다.**

    python3 packcap.py            # 전 슬롯 용량표 + 1차원 근사 대조
    python3 packcap.py --control  # ★ 양성 대조: 알려진 값으로 교정 + 일부러 틀린 입력
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, sectors as S, items, hair
from rig import BINS, BIN_DEG

W075, W060 = S.W075, S.W060
FLOOR = S.SILHOUETTE_FLOOR_R        # 0.42983 R = 1획 @0.60
RATCHET = S.SILHOUETTE_RATCHET_R    # 0.51580 R = 1.20획 @0.60


# ---------------------------------------------------------------------------
# 도달 상자 — verify.py의 슬롯 경계 단언을 그대로 옮긴 것
#   HEAD : 1.0 < top < 2.551 ,  bottom > -1.0
#   EYES : |x| < 1.6 , y < 1.15 , y > -2.2
#   NECK : y < 0.0 , y > HIP-0.517          (프로파일 원점은 어깨)
#   BACK : y < 1.0 , y > -9.3395            (프로파일 원점은 어깨)
#   HAIR : r ≤ 1.75(액자)  — verify.py의 top ≤ 1.75를 반경으로 보수적으로 옮겼다
# ---------------------------------------------------------------------------
SH = rig.SHOULDER_R
BOX = {   # (ymax, ymin, |x|max) — 프로파일 원점 기준
    "HEAD": (2.551,           -1.0,             None),
    "EYES": (1.15,            -2.2,             1.60),
    "NECK": (0.0 - SH,        rig.HIP_R - 0.517 - SH, None),
    "BACK": (1.0 - SH,        -9.3395 - SH,     None),
    # ★ HAIR는 verify.py가 **max y ≤ 1.75(초상화 액자)** 만 건다. 아래·좌우는 안 건다.
    "HAIR": (1.75,            None,             None),
    # FX/PET는 몸에 안 붙는다(각자 로컬 원점). 게이트가 거는 상자가 아예 없다 —
    # 그래서 이 둘은 **보수판(기존 r_max 이내)만** 뜻이 있다.
    "FX":   (None,            None,             None),
    "PET":  (None,            None,             None),
}
ANCHOR = {"HEAD": 0.0, "EYES": 0.0, "NECK": SH, "BACK": SH, "HAIR": 0.0,
          "FX": 0.0, "PET": 0.0}


def r_cap(slot, deg, hard_radius=None):
    """방향 deg에서 도달 가능한 최대 반경(R). 상자 + (선택) 반경 상한."""
    ymax, ymin, xmax = BOX[slot]
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    r = 1e9
    if ymax is not None and s > 1e-9:   r = min(r, ymax / s)
    if ymin is not None and s < -1e-9:  r = min(r, ymin / s)
    if xmax is not None and abs(c) > 1e-9: r = min(r, xmax / abs(c))
    if hard_radius is not None: r = min(r, hard_radius)
    return r


# ---------------------------------------------------------------------------
# (A) 구성적 하한 — 서로 겹치지 않는 예약 대역 세기
# ---------------------------------------------------------------------------
def sector_usable(env, slot, start_bin, nbins, clearance, hard_radius):
    """대역 [start_bin, +nbins)가 쓸 수 있는가:
       그 대역 안 **어느 한 구간**에서 r_cap ≥ (대역 봉투 최대 + clearance)."""
    idx = [(start_bin + k) % BINS for k in range(nbins)]
    need = max(env[i] for i in idx) + clearance
    return any(r_cap(slot, i * BIN_DEG, hard_radius) >= need for i in idx)


#: ★ 완충 구간(guard) — 대역 양옆에 비워 두는 구간 수.
#  왜 필요한가: `rig.profile`은 **변을 조밀 표본**한다. 반경 need인 꼭짓점과 반경 env인 이웃
#  꼭짓점 사이의 변은 그 사이 각도 전체에 잉크를 남기고, 그 표본은 **한 칸 앞 구간**에
#  배정된다(int(d/5)). 즉 뾰족점 하나가 **자기 구간 + 그 앞 구간** 둘을 들어 올린다.
#  완충 0으로 세면 이웃 대역끼리 서로의 값을 올려 쌍별 최소가 무너진다 —
#  ★ 첫 시안이 정확히 그렇게 깨졌고(NECK 0.2602R), **정리가 아니라 세는 방식이 틀렸다.**
SECTOR_GUARD_BINS = 1


def capacity(table, slot, clearance=FLOOR, nbins=S.SECTOR_MIN_BINS, hard_radius=None,
             guard=SECTOR_GUARD_BINS):
    """겹치지 않는 대역을 **탐욕적으로 최대 개수** 배치한다. 대역 하나가 실제로 먹는 폭은
    nbins + 2*guard 구간이다. 72구간 원형이라 시작 위치를 72번 다 돌려 최댓값을 취한다."""
    env = S.envelope(table, ANCHOR[slot])
    span = nbins + 2 * guard
    best, best_secs = 0, []
    for off in range(BINS):
        used = [False] * BINS
        secs = []
        b = 0
        while b < BINS:
            st = (off + b) % BINS                       # 먹는 폭의 시작
            core = (st + guard) % BINS                  # 실제 예약 대역의 시작
            idx = [(st + k) % BINS for k in range(span)]
            if any(used[i] for i in idx):
                b += 1; continue
            if sector_usable(env, slot, core, nbins, clearance, hard_radius):
                for i in idx: used[i] = True
                secs.append(core * BIN_DEG)
                b += span
            else:
                b += 1
        if len(secs) > best: best, best_secs = len(secs), secs
    return best, best_secs, env


# ---------------------------------------------------------------------------
# 1차원 근사 재현 — product-strategy 수치가 어떤 식에서 나오는가
# ---------------------------------------------------------------------------
def oned_forecast(d6_in_strokes, n_target=12, n_now=6):
    """'아이템이 k배가 되면 쌍별 최소가 1/k' 가정. 획 단위 입력/출력."""
    return d6_in_strokes * n_now / n_target


# ---------------------------------------------------------------------------
# 보고
# ---------------------------------------------------------------------------
def slot_row(name, table, slot, hard_radius=None):
    anchor = ANCHOR[slot]
    rows = S.pairwise_table(table, anchor)
    d6 = rows[0][0]
    env = S.envelope(table, anchor)
    cap_floor, secs_f, _ = capacity(table, slot, FLOOR, hard_radius=hard_radius)
    cap_ratch, secs_r, _ = capacity(table, slot, RATCHET, hard_radius=hard_radius)
    return dict(name=name, slot=slot, n=len(table), d6=d6, envmax=max(env),
                cap_floor=cap_floor, cap_ratch=cap_ratch,
                secs_f=secs_f, secs_r=secs_r, worst=rows[0][1:])


def main():
    print("╔══ 슬롯 형상 예산 (하한 %.5fR = 1획@0.60 · 래칫 %.5fR = 1.20획@0.60) ══╗"
          % (FLOOR, RATCHET))
    print("   프로파일 = 72구간 x 5도 최대반경 · 차 = L∞  (rig.profile / rig.max_delta)")
    print()
    import appearance as A
    FXT = {k: v for k, v in A.FX_NOW.items() if v}
    PETT = {k: v for k, v in A.PET_NOW.items() if v}
    TABLES = [("HEAD", items.HEAD), ("EYES", items.EYES),
              ("NECK", items.NECK), ("BACK", items.BACK), ("HAIR", hair.SET),
              ("FX", FXT), ("PET", PETT)]
    print("  ── (가) 게이트 상자만 (verify.py 경계 단언) ──")
    print("  %-5s %2s  %-22s %8s %8s %8s %8s" %
          ("슬롯", "현", "현재 최악 쌍", "d6(R)", "d6(획@60)", "대역@하한", "대역@래칫"))
    hard = {}
    for slot, t in TABLES:
        r = slot_row(slot, t, slot)
        hard[slot] = r
        print("  %-5s %2d  %-22s %8.4f %8.2f %8d %8d"
              % (slot, r['n'], "%s vs %s" % r['worst'], r['d6'], r['d6'] / W060,
                 r['cap_floor'], r['cap_ratch']))
    print()
    print("  ── (나) 보수판: **새 아이템이 기존 최대 반경을 넘지 않는다** ──")
    print("     (기존 6종보다 큰 물건을 안 만든다는 제약. 상자보다 훨씬 좁다)")
    print("  %-5s %10s %10s %10s" % ("슬롯", "기존 r_max", "대역@하한", "대역@래칫"))
    cons = {}
    for slot, t in TABLES:
        env = S.envelope(t, ANCHOR[slot]); rmax = max(env)
        r = slot_row(slot, t, slot, hard_radius=rmax)
        cons[slot] = r
        print("  %-5s %10.3f %10d %10d" % (slot, rmax, r['cap_floor'], r['cap_ratch']))
    print()
    print("  ── (다) 1차원 근사(product-strategy)와의 대조 ──")
    print("  %-5s %6s %14s %10s %14s %10s"
          % ("슬롯", "현재", "12종 1D 예보", "1D 판정", "보수 용량(래칫)", "실측 판정"))
    for slot, t in TABLES:
        r = hard[slot]; c = cons[slot]
        f = oned_forecast(r['d6'] / W060, 12, len(t))
        cap = len(t) + c['cap_ratch']
        print("  %-5s %6d %14.2f %10s %14d %10s"
              % (slot, len(t), f, "미달 예보" if f < 1.0 else "통과 예보", cap,
                 "12종 가능" if cap >= 12 else "★ 12종 불가"))
    print()
    print("  ── (라) ★ DLC 이전에 **이미** 하한을 깨고 있는 슬롯 ──")
    for slot, t in TABLES:
        d = hard[slot]['d6']
        flag = d < FLOOR
        print("  %s %-5s 쌍별 최소 %.4fR = %.2f획@0.75 = %.2f획@0.60  (%s vs %s)%s"
              % ("★✗" if flag else "  ", slot, d, d / W075, d / W060,
                 hard[slot]['worst'][0], hard[slot]['worst'][1],
                 "   ← 지금 이미 미달" if flag else ""))
    print()
    print("╚══ 끝 ══╝")
    return hard, cons


# ---------------------------------------------------------------------------
# ★ 교정 + 양성 대조 — 알려진 값으로 먼저 맞춘다(CLAUDE.md)
# ---------------------------------------------------------------------------
def control():
    ok = True
    def chk(label, got, want, tol=5e-4):
        nonlocal ok
        good = abs(got - want) <= tol
        ok = ok and good
        print("  %s %-52s 측정 %.4f / 알려진 값 %.4f" % ("OK " if good else "✗  ", label, got, want))

    print("╔══ 교정 — 이 저장소가 이미 알고 있는 값과 맞는가 ══╗")
    # 1) 기존 30종 쌍별 최소 (docs/EQUIPMENT_SHAPE_SPEC.md 2절 + sectors.py 출력)
    chk("HEAD 쌍별 최소 (털모자 vs 왕관)", S.pairwise_table(items.HEAD, 0.0)[0][0], 0.5149)
    chk("EYES 쌍별 최소 (선글라스 vs 뿔테)", S.pairwise_table(items.EYES, 0.0)[0][0], 0.5561)
    chk("NECK 쌍별 최소 (방울 vs 펜던트)", S.pairwise_table(items.NECK, SH)[0][0], 0.6800)
    chk("BACK 쌍별 최소 (날개 vs 요정날개)", S.pairwise_table(items.BACK, SH)[0][0], 0.7592)
    chk("W@0.75", W075, 0.343864)
    chk("W@0.60", W060, 0.429825)
    # 2) 도달 상자 — 손으로 검산 가능한 값
    chk("r_cap(HEAD, 90°) = 액자 상한", r_cap("HEAD", 90.0), 2.551)
    chk("r_cap(HEAD, 270°) = 턱선 1.0R", r_cap("HEAD", 270.0), 1.0)
    chk("r_cap(HEAD, 210°) = 1.0/sin30°", r_cap("HEAD", 210.0), 2.0)
    chk("r_cap(EYES, 0°) = |x| 상한", r_cap("EYES", 0.0), 1.60)
    # 3) 팩 하나를 실제로 얹었을 때 — pack_office 헤드셋의 알려진 여유 1.434R
    import pack_office as PO
    a, b, gap, okk = S.sector_check(PO.headset(), items.HEAD, PO.OFFICE_SECTORS["HEAD"], 0.0)
    chk("오피스 헤드셋 320° 대역 여유", gap, 1.434, tol=2e-3)
    print("╚══ 교정 %s ══╝" % ("통과" if ok else "★실패 — 아래 숫자를 전부 폐기하라"))
    if not ok:
        sys.exit(2)

    print()
    print("╔══ 양성 대조 — 일부러 틀린 입력에 빨간불이 켜지는가 ══╗")
    # (a) 같은 아이템을 복제해 넣으면 쌍별 최소가 0이어야 한다
    t = dict(items.HEAD); t["복제_왕관"] = items.HEAD["왕관"]
    d = S.pairwise_table(t, 0.0)[0][0]
    print("  %s 복제 아이템 삽입 -> 쌍별 최소 %.4fR (0이어야 정상)" % ("OK " if d < 1e-9 else "✗  ", d))
    ok2 = d < 1e-9
    # (b) 도달 상자를 0으로 조이면 대역이 하나도 안 나와야 한다
    n, _, _ = capacity(items.HEAD, "HEAD", FLOOR, hard_radius=0.01)
    print("  %s 반경 상한 0.01R -> 대역 %d개 (0이어야 정상)" % ("OK " if n == 0 else "✗  ", n))
    ok2 = ok2 and n == 0
    # (c) 여유(clearance)를 상자보다 크게 잡으면 대역이 0이어야 한다
    # ★ 첫 시안은 여기서 2개가 나와 빨간불이 났다. **도구가 아니라 내 기대가 틀렸다** —
    #   게이트는 HEAD의 **가로 뻗음을 아예 안 막는다**(0°/180°에서 r_cap = ∞).
    #   그래서 반경 상한을 같이 줘야 0이 된다. 이 사실 자체가 (가)열을 못 믿을 이유다.
    n2, _, _ = capacity(items.HEAD, "HEAD", 99.0, hard_radius=3.0)
    print("  %s 여유 99R + 반경상한 3R -> 대역 %d개 (0이어야 정상)" % ("OK " if n2 == 0 else "✗  ", n2))
    n2b, _, _ = capacity(items.HEAD, "HEAD", 99.0)
    print("  ·  (참고) 반경상한 없이 여유 99R -> %d개. 게이트가 가로를 안 막는 증거(0°/180°)" % n2b)
    ok2 = ok2 and n2 == 0
    # (d) 봉투가 0인 가상 슬롯이면 대역이 24개(72/3)여야 한다 — 세는 논리 자체의 상한
    empty = {"가상": [rig.Shape("pt", [(0.0, 0.0), (0.001, 0.0)], loop=False)]}
    n3, _, _ = capacity(empty, "HEAD", FLOOR)
    print("  %s 빈 슬롯 -> 대역 %d개 (72/(3+2*%d) = %d가 상한)"
          % ("OK " if n3 <= BINS // (S.SECTOR_MIN_BINS + 2 * SECTOR_GUARD_BINS) else "✗  ",
             n3, SECTOR_GUARD_BINS, BINS // (S.SECTOR_MIN_BINS + 2 * SECTOR_GUARD_BINS)))
    ok2 = ok2 and n3 <= BINS // (S.SECTOR_MIN_BINS + 2 * SECTOR_GUARD_BINS)
    print("╚══ 양성 대조 %s ══╝" % ("통과" if ok2 else "★실패"))
    return ok and ok2




# ---------------------------------------------------------------------------
# ★ 독립 교차확인 — (A)의 정리를 **다른 코드로** 다시 잰다.
#   TEAM.md §「생성기와 검사기가 같이 틀린다」: 용량을 세는 코드와 그 결과를 검증하는 코드가
#   같으면 둘이 같은 방향으로 틀린다. 그래서 여기서는
#     · 세는 쪽 = capacity()          (대역 개수)
#     · 재는 쪽 = sectors.pairwise_table()  (기존 30종 판정에 이미 쓰이는 그 함수)
#   두 경로가 다르다. 대역 개수만큼 **실제 도형을 합성해 표에 넣고** 쌍별 최소를 다시 잰다.
# ---------------------------------------------------------------------------
def _band(bins_run, radius_of, ay, name):
    """연속한 구간 묶음을 **띠 폴리곤**으로 만든다(바깥호 + 안쪽호).
    ★ 별 모양 닫힌 폴리곤으로 만들면 안 된다 — 잉크가 **없어야 할 방향**에도 원점 근처
    반경이 생겨 조건 (i)("자기 대역 밖에서는 봉투를 넘지 않는다")을 구조적으로 못 지킨다.
    실제 아이템(예: 민머리의 테 조각)은 쐐기 안에만 사는 띠다."""
    outer = [rig.polar(i * BIN_DEG, radius_of(i)) for i in bins_run]
    inner = [rig.polar(i * BIN_DEG, radius_of(i) * 0.45) for i in reversed(bins_run)]
    pts = [(x, y + ay) for x, y in outer + inner]
    return rig.Shape(name, pts, filled=True)


def _runs(pred):
    """pred(i)가 참인 구간의 **원형 연속 묶음** 목록."""
    out, cur = [], []
    for i in list(range(BINS)) + [0]:
        if pred(i % BINS): cur.append(i % BINS)
        else:
            if cur: out.append(cur); cur = []
    if cur: out.append(cur)
    # 0°에서 이어지는 묶음 병합
    if len(out) > 1 and out[0][0] == 0 and out[-1][-1] == BINS - 1:
        out[0] = out[-1] + out[0]; out.pop()
    return out


def synth_item(env, slot, sector_start_deg, clearance, hard_radius, name="synth"):
    """정리의 두 조건을 그대로 만족하는 합성 도형:
       자기 대역에서만 env+clearance, 나머지 구간은 env를 넘지 않고,
       **봉투가 0인 방향에는 잉크를 두지 않는다**(띠로 만드는 이유)."""
    idx = [(int(sector_start_deg / BIN_DEG) + k) % BINS for k in range(S.SECTOR_MIN_BINS)]
    need = max(env[i] for i in idx) + clearance
    ay = ANCHOR[slot]        # ★ 프로파일 원점(NECK/BACK은 어깨). 여기를 빠뜨리면 좌표가
                             #   머리 중심 기준으로 찍혀 프로파일이 통째로 어긋난다 —
                             #   첫 시안의 BACK "실패"는 정리가 아니라 **이 한 줄**이었다.
    shapes = []
    idxset = set(idx)
    EPS = 0.05
    for k, run in enumerate(_runs(lambda i: env[i] > EPS and i not in idxset)):
        if len(run) < 2: continue
        shapes.append(_band(run, lambda i: env[i] * 0.98, ay, "%sBody%d" % (name, k)))
    shapes.append(_band(idx, lambda i: min(need, r_cap(slot, i * BIN_DEG, hard_radius)),
                        ay, name + "Spike"))
    # ★ 여기에 "보조색 조각" 흉내로 원점의 작은 사각형을 넣었다가 HAIR가 계속 빨간불이었다.
    #   반지름 0.06짜리 그 사각형이 **모든 방향에서 프로파일을 0.046~0.06으로 들어 올려**
    #   봉투가 0인 방향(턱 아래)에서 차를 0.3835로 깎았다. 합성 도형에 보조색은 필요 없다 —
    #   여기서 재는 것은 실루엣 프로파일뿐이다. **작은 조각 하나가 L∞ 바닥을 올린다**는
    #   사실 자체는 실제 설계에도 그대로 적용된다(약점 절에 적었다).
    return shapes


def synth_check(table, slot, clearance, hard_radius=None):
    n, secs, env = capacity(table, slot, clearance, hard_radius=hard_radius)
    t = dict(table)
    for k, st in enumerate(secs):
        t["합성%02d" % k] = synth_item(env, slot, st, clearance, hard_radius, "S%02d" % k)
    if len(t) < 2: return n, None, None
    d, a, b = S.pairwise_table(t, ANCHOR[slot])[0]
    return n, d, (a, b)


def synth_report():
    import appearance as A
    TABLES = [("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
              ("BACK", items.BACK), ("HAIR", hair.SET),
              ("FX", {k: v for k, v in A.FX_NOW.items() if v}),
              ("PET", {k: v for k, v in A.PET_NOW.items() if v})]
    print("╔══ 독립 교차확인 — 대역 수만큼 실제로 합성해 넣고 쌍별 최소를 다시 잰다 ══╗")
    print("   (세는 코드 capacity() ≠ 재는 코드 sectors.pairwise_table())")
    bad = 0
    for cl, tag in ((FLOOR, "하한"), (RATCHET, "래칫")):
        print("  [여유 = %s %.5fR]" % (tag, cl))
        for slot, t in TABLES:
            rmax = max(S.envelope(t, ANCHOR[slot]))
            n, d, w = synth_check(t, slot, cl, hard_radius=rmax)
            base = S.pairwise_table(t, ANCHOR[slot])[0][0]
            # 판정: 합성 후 쌍별 최소가 min(기존 최소, 여유) 이상이면 정리가 성립한 것이다
            want = min(base, cl)
            ok = (d is None) or (d >= want - 1e-6)
            if not ok: bad += 1
            print("    %s %-4s 대역 %2d개 -> 총 %2d종 · 쌍별 최소 %s (기존 %.4f / 요구 %.4f)"
                  % ("OK " if ok else "✗  ", slot, n, len(t) + n,
                     "—" if d is None else "%.4fR" % d, base, want))
    print("╚══ 교차확인 위반 %d건 ══╝" % bad)
    return bad


if __name__ == "__main__":
    if "--control" in sys.argv:
        sys.exit(0 if control() else 1)
    control()
    print()
    main()
    print()
    synth_report()
