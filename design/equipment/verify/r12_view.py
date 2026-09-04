# -*- coding: utf-8 -*-
"""★ R12 ② — **시점(view) 축**. 인계본은 정면 도형이고 우리는 3/4 도형이다.

리더 판정(2026-09-03): 「좌표 이식」 기각 · 「3/4 재해석 이식」 채택.
이 파일은 그 판정의 **자(尺)**다. 사양서 §1-4가 여기서 나온다.

세 성분으로 나눈다 — 셋의 성질이 완전히 다르기 때문이다
------------------------------------------------------
  c (덩어리 오프셋)  = (F - B) / 2 · R
      아이템 전체를 진행축으로 민 양. **평행이동**이라 규칙 1-A/1-C/자기교차가 전부 불변이고,
      **정확히 복원 가능**하다. 인계본 좌표를 c 만큼 밀면 끝난다. 잃는 것이 없다.

  A (거울 잔차 지수) = max|prof(θ) - prof_mirror(θ)| / 반폭      (c 제거 후 72구간 실루엣)
      c 를 뺀 뒤에도 남는 **진짜 형태 비대칭**. 좌우 대칭 원본에서 **유도 불가능**하다 —
      재저작해야 한다.
      ★ 리더가 쓴 F/B 는 이 성분을 **놓친다**: 목도리(인계본)는 F/B = 1.000 인데 A = 1.134 다.
        우리 쪽도 F/B 로는 12종인데 A 로는 14종이다(털모자·반다나가 더 걸린다).

  k (부위 단축)      = 앞 조각 폭 / 뒤 조각 폭
      같은 부위가 앞뒤로 쌍을 이룰 때 앞쪽을 키워 단축을 만든다.
      우리 프로덕션에 **이름 붙은 상수로 이미 있다**: `SunglassFrontBiasRatio = 1.05f`.

  python3 r12_view.py
  python3 r12_view.py --control   # 음성 대조: 우리 도형을 좌우 대칭화하면 A 가 0으로 죽는가
"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig, items, handoff
from rig import Shape
from handoff import parse_items, build, to_R_shape, OUR_NAME

CONTROL = "--control" in sys.argv
A_GATE = 0.06                    # 이 위면 「3/4 도형」으로 센다. 반폭의 6% = 획 0.2개 수준.

OURS = {}
for grp, gn in ((items.HEAD, "HEAD"), (items.EYES, "EYES"),
                (items.NECK, "NECK"), (items.BACK, "BACK")):
    for nm, S in grp.items(): OURS[nm] = (gn, S)


def _map(S, f):
    return [Shape(s.name, [f(x, y) for x, y in s.pts], s.loop, s.filled, s.tone, s.sort) for s in S]

def symmetrize(S):
    """음성 대조용 — c 를 뺀 뒤 **자기 거울상을 합집합**해 강제 좌우 대칭으로 만든다.
    (|x| 로 접는 것은 대칭화가 아니라 반쪽 내기다 — 그러면 A 가 오히려 커진다. 실측으로 확인함.)
    A 가 0 으로 죽어야 이 자가 형태를 재고 있는 것이다."""
    xs = [p[0] for s_ in S for p in s_.pts]
    c = (max(xs) + min(xs)) / 2.0
    C = _map(S, lambda x, y: (x - c, y))
    return C + _map(C, lambda x, y: (-x, y))

def measure(S):
    xs = [p[0] for s in S for p in s.pts]; ys = [p[1] for s in S for p in s.pts]
    F, B = max(xs), -min(xs)
    c, half = (F - B) / 2.0, (F + B) / 2.0
    Sc = _map(S, lambda x, y: (x - c, y))
    pr = rig.profile(Sc); pm = rig.profile(_map(Sc, lambda x, y: (-x, y)))
    res = rig.max_delta(pr, pm)
    return dict(F=F, B=B, fb=(F / B if B > 1e-9 else float("inf")), c=c, half=half,
                res=res, A=(res / half if half > 1e-9 else 0.0),
                w=max(xs) - min(xs), h=max(ys) - min(ys))


def main():
    IT = parse_items()
    H = {}
    for kind, pieces in IT.items():
        P = build(kind, pieces)
        H[kind] = ([to_R_shape(kind, p, "%s%d" % (p.call, i)) for i, p in enumerate(P)])

    print("╔══ ② 시점 축 — 인계본(정면) vs 우리(3/4) ══╗")
    print("  A 문턱 %.2f (반폭 대비). c 는 평행이동이라 무손실 · A 는 재저작 대상.\n" % A_GATE)
    print("  %-13s %-8s %6s %8s %8s %6s   %s" % ("인계본", "→ 우리", "F/B", "c(R)", "잔차(R)", "A", "판정"))
    hn = on = 0
    rows = []
    for kind, S in H.items():
        m = measure(S)
        slot, nm = OUR_NAME[kind]
        gn, OS = OURS[nm]
        mo = measure(symmetrize(OS) if CONTROL else OS)
        if m["A"] >= A_GATE: hn += 1
        if mo["A"] >= A_GATE: on += 1
        rows.append((kind, nm, m, mo))
        print("  %-13s %-8s %6.3f %+8.4f %8.4f %6.3f   %s" %
              (kind, "", m["fb"], m["c"], m["res"], m["A"],
               "★3/4" if m["A"] >= A_GATE else "정면"))
        print("  %-13s %-8s %6.3f %+8.4f %8.4f %6.3f   %s" %
              ("", "└ " + nm, mo["fb"], mo["c"], mo["res"], mo["A"],
               "★3/4" if mo["A"] >= A_GATE else "정면"))
    print("\n  ⇒ 인계본 16종 중 3/4 도형 %d종 · 대응하는 우리 16종 중 %d종" % (hn, on))

    # 우리 전 24종
    print("\n╔══ 우리 24종 전수 — 보존해야 할 자산 ══╗")
    n34 = 0
    for nm, (gn, S) in OURS.items():
        m = measure(symmetrize(S) if CONTROL else S)
        if m["A"] >= A_GATE: n34 += 1
        print("  %-4s %-8s F/B %6.3f  c %+7.4f  A %6.3f  %s" %
              (gn, nm, m["fb"], m["c"], m["A"], "★3/4" if m["A"] >= A_GATE else ""))
    print("  ⇒ 3/4 도형 %d / 24" % n34)
    if CONTROL:
        print("\n  ★ 대조 판정: 강제 대칭화했으므로 **A 는 전부 0, 3/4 는 0종**이어야 한다.")
        print("     0종이 아니면 A 를 재는 자가 형태가 아니라 잡음을 재고 있는 것이다.")
        return 0 if n34 == 0 else 1

    # ── 상자 정합 배율 σ ────────────────────────────────────────────────────
    print("\n╔══ ③ 상자 정합 — 인계본 봉투를 우리 봉투에 맞추는 배율 ══╗")
    print("  §4-3의 「폭 차」는 크기 차가 아니라 **앞뒤 도달 차**가 섞인 값이다.")
    print("  c 를 뺀 뒤 재면 무엇이 진짜 크기 차인지 갈라진다.\n")
    print("  %-13s %7s %7s %7s   %7s %7s   %s" %
          ("아이템", "σx", "σy", "비등방", "우리c", "인계c", "판정"))
    for kind, nm, m, mo in rows:
        sx = mo["w"] / m["w"]; sy = mo["h"] / m["h"]
        an = max(sx, sy) / min(sx, sy)
        tag = "균일(σ %.3f)" % ((sx + sy) / 2) if an < 1.08 else "★비등방 %.0f%%" % ((an - 1) * 100)
        print("  %-13s %7.3f %7.3f %7.3f   %+7.4f %+7.4f   %s" %
              (kind, sx, sy, an, mo["c"], m["c"], tag))

    # ── 야구모자 처방 검산 ──────────────────────────────────────────────────
    print("\n╔══ ④ 야구모자(clothhat) — 「감쌈은 평행이동으로 못 산다」 검산 ══╗")
    cb = [s for s in H["clothhat"]][0]              # B0 = 관
    x0, y0, x1, y1 = rig.bounds(cb.pts)
    ours = dict((s.name, rig.bounds(s.pts)) for s in OURS["야구모자"][1])
    tx0, ty0, tx1, ty1 = ours["HatCrown"]
    print("  인계본 관 B0   y[%+.4f, %+.4f]  높이 %.4f R  x ±%.4f" % (y0, y1, y1 - y0, x1))
    print("  우리 HatCrown  y[%+.4f, %+.4f]  높이 %.4f R  x[%+.4f,%+.4f]" % (ty0, ty1, ty1 - ty0, tx0, tx1))
    shift = ty0 - y0
    print("  (가) 순수 평행이동으로 밑변을 %+.4f R 에 맞추면 → 꼭대기 %+.4f R" % (ty0, y1 + shift))
    print("       머리 꼭대기 +1.0000 R 보다 %+.4f R  ⇒ %s" %
          (y1 + shift - 1.0, "머리 안에 파묻힌다 — **불가**" if y1 + shift < 1.0 else "여유 있음"))
    sy = (ty1 - ty0) / (y1 - y0); sx = (tx1 - tx0) / (x1 - x0)
    print("  (나) 밑변·꼭대기를 둘 다 맞추는 배율  σy %.4f · σx %.4f · 비등방 %.1f%%"
          % (sy, sx, (max(sx, sy) / min(sx, sy) - 1) * 100))
    print("       ⇒ **균일 배율 σ = %.3f 로 충분하다**(비등방 %.1f%%는 획의 %.2f배 미만)."
          % ((sx + sy) / 2, (max(sx, sy) / min(sx, sy) - 1) * 100,
             abs(sx - sy) * (x1 - x0) / rig.W))
    print("       기준점 = 관 밑변. 배율 뒤 밑변을 %+.4f R 로 옮긴다." % ty0)
    # 챙
    brim = H["clothhat"][2]
    bx0, by0, bx1, by1 = rig.bounds(brim.pts)
    s = (sx + sy) / 2
    ny0 = (by0 - y0) * s + ty0; ny1 = (by1 - y0) * s + ty0
    obx0, oby0, obx1, oby1 = ours["HatBrim"]
    print("  (다) 같은 변환을 챙 B2 에 걸면  y[%+.4f,%+.4f]  (우리 HatBrim y[%+.4f,%+.4f])"
          % (ny0, ny1, oby0, oby1))
    print("       세로는 %.4f R 안에서 맞는다. 가로는 인계본 ±%.4f(대칭) vs 우리 [%+.4f,%+.4f](앞 챙)"
          % (max(abs(ny0 - oby0), abs(ny1 - oby1)), bx1 * s, obx0, obx1))
    print("       ⇒ 챙은 **A 성분**이다. 대칭 원본에서 유도 불가 — 재저작한다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
