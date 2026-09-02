# -*- coding: utf-8 -*-
"""★ 팩 「야간 정비반」 — **6/6 동시 착용 상태**의 검산.

개별 아이템이 규칙을 다 지켜도, 팩이 파는 것은 「6개를 한꺼번에 입은 그림」이다.
기본 42종은 그 상태를 **아무도 잰 적이 없고**, 그래서 결함이 남아 있다 —
모자를 쓰면 안경이 평균 6.4%(@0.75)만 남는다(③의 대조군).

★ 이 파일은 두 번 고쳤다. 폐기한 것을 지우지 않고 적어 둔다:
  · 폐기 1 — 「HAIR 면적 생존율 ≥ 90%」. **내 기준이 틀렸다.** 머리카락 면적의 대부분은
    돔(정수리)이고 그건 모자가 가리는 것이 **옳다**. 살아야 하는 것은 면적이 아니라
    **정체를 만드는 부분**(보조색 도형 + 예약 대역의 프로파일)이다. ②를 그렇게 바꿨다.
  · 폐기 2 — 「연결도(인접 잉크 간격 ≤ 1.5획)」. **반증됐다.** 대조군(기본 42종 임의 한 벌)도
    4/4로 나왔다. 캐릭터가 작아서 무엇을 걸치든 서로 닿는다 = **변별력 0인 자**였다.
    대신 ⑤에 「한계 가림률」을 넣었다 — 그 자는 대조군과 갈린다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H, sectors as S
from rig import Shape
import pack_nightshift as P
INF = float("inf")


def _base_eyes_avg(w):
    """출하 모자6 x 안경6 = **36조합** 평균 생존율. 상수를 베끼지 않고 그 자리에서 잰다."""
    cells = []
    for en, ef in items.EYES.items():
        es = ef() if callable(ef) else ef
        b = vis(es, [], w)
        if b <= 1e-9: continue
        for hn, hf in HATS.items():
            cells.append(vis(es, hf(), w) / b)
    return sum(cells) / len(cells)


HATS = {"야구모자": items.cap, "털모자": items.beanie, "중절모": items.fedora,
        "왕관": items.crown_hat, "베레모": items.beret, "밀짚모자": items.straw}
PACK_COVER = 0.50          # 팩 모자의 커버선 = 관의 밑변(다른 모자와 같은 정의)
fail = []
def bad(m): fail.append(m); print("  ✗ " + m)
def ok(m): print("  OK " + m)


def vis(shapes, occluders, w, cover=INF):
    return H.hair_visible_area(shapes, occluders, cover, w)


def occluded_profile(shapes, occluders, w, cover=INF, bins=72):
    """가려지고 **남은 잉크**의 프로파일(72구간 x 5도 최대반경). 실루엣이 살았는지를 본다."""
    out = [0.0] * bins
    for s in shapes:
        if not s.filled: continue
        pts = H.clip_below(s.pts, cover)
        if len(pts) < 3: continue
        ys = [p[1] for p in pts]; y0, y1 = min(ys), max(ys)
        N = 400
        for k in range(N + 1):
            y = y0 + (y1 - y0) * k / N
            sp = H._merge(H._poly_spans(pts, y))
            cov = H._merge(H.ink_spans(occluders, y, w)) if occluders else []
            for a, b in sp:
                cur = [(a, b)]
                for ca, cb in cov:
                    nxt = []
                    for x0, x1 in cur:
                        if cb <= x0 or ca >= x1: nxt.append((x0, x1)); continue
                        if ca > x0: nxt.append((x0, ca))
                        if cb < x1: nxt.append((cb, x1))
                    cur = nxt
                for x0, x1 in cur:
                    for t in range(9):
                        x = x0 + (x1 - x0) * t / 8
                        r = math.hypot(x, y)
                        i = int((math.degrees(math.atan2(y, x)) % 360) / 5) % bins
                        if r > out[i]: out[i] = r
    return out


def run(scale):
    w = H.stroke_in_R(scale)
    print("\n╔══════════════ 배율 %.2f  (W = %.4f R) ══════════════╗" % (scale, w))

    # ── ① 남는 머리 ──
    print("  ── ① 남는 머리 (하한 두께 %.2f획 / 면적 %.0f%% — headroom.py 소유) ──"
          % (H.HEADROOM_THICKNESS_FLOOR_W, H.HEADROOM_AREA_FLOOR * 100))
    m = H.measure(P.head_havelock(), w)
    th = m['depth'] * 2.0 / w
    good = th >= H.HEADROOM_THICKNESS_FLOOR_W and m['area'] >= H.HEADROOM_AREA_FLOOR
    (ok if good else bad)("목덮개 작업모  두께 %5.2f획  면적 %5.1f%%  외곽호 %5.1f°  잉크밑단 %+.3fR"
                          % (th, m['area']*100, m['arc'], m['ink_bottom']))
    print("     대조 기본 6종 면적: " + " ".join("%s %.0f%%" % (n, H.measure(f(), w)['area']*100)
                                              for n, f in HATS.items()))

    # ── ② 팩 HAIR — **정체를 만드는 부분**이 모자 밑에서 살아남는가 ──
    print("\n  ── ② 팩 HAIR 정체 생존 (면적이 아니라 보조색 + 예약대역) ──")
    hs = P.hair_napetie()
    band = [s for s in hs if s.tone == 1]
    far = [Shape(s.name, [(x, y+40) for x, y in s.pts], s.loop, s.filled) for s in P.head_havelock()]
    cal = vis(band, far, w) / vis(band, [], w)
    print("     [교정] 멀리 치운 모자 -> 보조색 생존 %.6f (기대 1.000000)  %s"
          % (cal, "OK" if abs(cal-1) < 1e-6 else "FAIL"))
    if abs(cal-1) > 1e-6: sys.exit(1)
    a = vis(band, P.head_havelock(), w, PACK_COVER) / vis(band, [], w)
    (ok if a >= 0.95 else bad)("보조색(HairTieBand) 생존 %.1f%%  — 팩 모자 밑에서" % (a*100))
    mixed = [(n, vis(band, f(), w, items.COVER[n]) / vis(band, [], w)) for n, f in HATS.items()]
    mn = min(r for _, r in mixed)
    (ok if mn >= 0.95 else bad)("기본 모자 6종과 혼용 최악 %.1f%% (%s)"
                                % (mn*100, min(mixed, key=lambda t: t[1])[0]))
    # 예약 대역 245~265°의 프로파일이 가려진 뒤에도 남는가
    pr = occluded_profile(hs, P.head_havelock(), w, PACK_COVER)
    env = S.envelope(hair.SET, 0.0)
    idx = [49, 50, 51, 52, 53]      # 245,250,255,260,265도
    mine = max(pr[i] for i in idx); base = max(env[i] for i in idx)
    sep = mine - base
    (ok if sep >= S.SILHOUETTE_RATCHET_R else bad)(
        "예약대역 245~265° 가려진 뒤 반경 %.3fR · 기존 봉투 %.3fR · 확보 %.3fR = %.2f획@0.60"
        % (mine, base, sep, sep / H.stroke_in_R(0.60)))
    # 면적도 참고로 남긴다(판정 기준은 아니다)
    fa = vis(hs, P.head_havelock(), w, PACK_COVER) / vis(hs, [], w)
    print("     (참고) 전체 면적 생존 %.1f%% — 기본 머리6 x 기본 모자6 평균 22.8%%. 돔이 가려지는 것은 의도다"
          % (fa*100))

    # ── ③ 팩 EYES ──
    print("\n  ── ③ 팩 EYES 생존 (모자 잉크에 가려지는 것만. 안경은 잘리지 않는다) ──")
    es = P.eyes_respirator()
    b1 = vis(es, [], w)
    v2 = vis(es, P.head_havelock(), w) / b1
    # ★ R5 자기정정: 여기 박혀 있던 {0.75:0.064, 0.60:0.039} 는 **36조합 평균이 아니라
    #   「선글라스 행」 평균**이었다(eyesunderhat.py 표의 한 줄을 손으로 베낀 값).
    #   상수를 베끼지 말라는 규칙(CLAUDE.md)에 정면으로 걸린다 — 그 자리에서 다시 계산한다.
    BASE_EYES_AVG = _base_eyes_avg(w)
    # 문턱은 내가 고른 배수가 아니라 **다른 팀이 독립으로 제안한 값**을 쓴다 —
    # design/character/DLC_PACK_R2_WOOCHEON_SPEC.md §6 P-2 「모자 밑 안경 생존율 ≥ 65%」.
    P2_FLOOR = 0.65
    (ok if v2 >= P2_FLOOR else bad)(
        "팩 EYES + 팩 HEAD 생존 %.1f%% (P-2 문턱 %.0f%%)  = 출하 36조합 평균 %.1f%%의 %.1f배"
        % (v2*100, P2_FLOOR*100, BASE_EYES_AVG*100, v2/BASE_EYES_AVG))
    print("     기본 모자와 혼용: " + " ".join("%s %.0f%%" % (n, vis(es, f(), w)/b1*100) for n, f in HATS.items()))

    # ── ④ 팩 안 상호 가림 ──
    print("\n  ── ④ 팩 안 상호 가림 (레이어 BACK −1 < HAIR 6 < NECK 7 < EYES 8 < HEAD 10) ──")
    print("     ★ 판정은 **면적이 아니라 보조색 도형**으로 한다. 근거는 내가 정한 것이 아니라")
    print("        37-6 규칙 3-2 = 「보조색 도형 정확히 1개 = 형제와 나를 가르는 단 하나」다.")
    print("        면적으로 재면 '모자가 머리 정수리를 가린다'가 위반이 되는데, 그건 **의도**다.")
    order = [("BACK 연장가방", P.back_toolbag(), INF), ("HAIR 목덜미", P.hair_napetie(), PACK_COVER),
             ("NECK 앞치마", P.neck_apronbib(), INF), ("EYES 고글", P.eyes_respirator(), INF),
             ("HEAD 목덮개", P.head_havelock(), INF)]
    worst = (1.1, None)
    for i, (ni, si, ci) in enumerate(order):
        above = [s for _, sh, _ in order[i+1:] for s in sh]
        if not above: continue
        acc = [s for s in si if s.tone == 1]
        ba = vis(acc, [], w, ci)
        av = vis(acc, above, w, ci) / ba if ba > 1e-9 else 1.0
        bf = vis(si, [], w, ci)
        fv = vis(si, above, w, ci) / bf if bf > 1e-9 else 1.0
        if av < worst[0]: worst = (av, ni)
        (ok if av >= 0.95 else bad)("%-12s 보조색 생존 %5.1f%%   (참고: 전체 면적 %5.1f%%)"
                                    % (ni, av*100, fv*100))
    print("     ★ 팩 안 보조색 최악 %s %.1f%%  (하한 95%% — '산 것의 정체가 안 보인다'를 막는 선)"
          % (worst[1], worst[0]*100))
    return w


# ── ⑤ 한계 가림률 — 등급 축 없이 기대를 만드는 자 ──────────────────────────
def marginal(scale):
    """한 벌을 **한 개씩 껴 나갈 때**, 새로 끼는 물건이 **이미 낀 것들을 얼마나 지우는가**.
       등급이 금지된 팩에서 유일하게 남은 기대 축이 이것이다:
         기본 카탈로그는 다 낄수록 **보이는 것이 줄어든다**(모자가 안경을 먹는다).
         팩은 다 껴야 다 보이게 설계한다. 그 차이를 재는 자.
       ★ 앞서 쓴 「연결도」는 대조군도 4/4로 나와 **변별력 0**이었다. 그래서 이걸로 바꿨다."""
    w = H.stroke_in_R(scale)
    print("\n╔══ ⑤ 한계 가림률 (배율 %.2f) ══╗" % scale)

    def sweep(seq, label):
        worn, prev, rows = [], 0.0, []
        for name, sh, cover in seq:
            before = sum(vis(s[1], [x for _, ss, _ in worn for x in ss[0]] if False else
                             [x for _, sx, _ in worn for x in sx], w) for s in []) # placeholder
            # 이전까지의 가시 총량
            prev_vis = 0.0
            for j, (nj, sj, cj) in enumerate(worn):
                above = [x for _, sk, _ in worn[j+1:] for x in sk]
                prev_vis += vis(sj, above, w, cj)
            worn.append((name, sh, cover))
            new_vis = 0.0
            for j, (nj, sj, cj) in enumerate(worn):
                above = [x for _, sk, _ in worn[j+1:] for x in sk]
                new_vis += vis(sj, above, w, cj)
            own = vis(sh, [], w, cover)
            # 새로 낀 것이 기존을 지운 양 = (이전 가시) - (지금 가시 중 이전 것들의 몫)
            kept = 0.0
            for j, (nj, sj, cj) in enumerate(worn[:-1]):
                above = [x for _, sk, _ in worn[j+1:] for x in sk]
                kept += vis(sj, above, w, cj)
            loss = (prev_vis - kept) / prev_vis if prev_vis > 1e-9 else 0.0
            rows.append((name, own, new_vis, loss))
            prev = new_vis
        print("  [%s]  (착용 순서대로)" % label)
        print("   %-14s %10s %12s %12s" % ("추가한 것", "자기 잉크", "가시 총량", "기존을 지운 비율"))
        tot = 0.0
        for name, own, nv, loss in rows:
            print("   %-14s %10.3f %12.3f %11.1f%%" % (name, own, nv, loss*100))
            tot = max(tot, loss)
        print("   ★ 최악 한계 가림률 %.1f%%" % (tot*100))
        return tot, rows[-1][2]

    packseq = [("HAIR 목덜미", P.hair_napetie(), PACK_COVER),
               ("EYES 고글", P.eyes_respirator(), INF),
               ("NECK 앞치마", P.neck_apronbib(), INF),
               ("BACK 연장가방", P.back_toolbag(), INF),
               ("HEAD 목덮개", P.head_havelock(), INF)]
    baseseq = [("HAIR 단정한머리", hair.SET["단정한머리"], items.COVER["중절모"]),
               ("EYES 동그란안경", items.EYES["동그란안경"], INF),
               ("NECK 나비넥타이", items.NECK["나비넥타이"], INF),
               ("BACK 요정날개", items.BACK["요정날개"], INF),
               ("HEAD 중절모", items.fedora(), INF)]
    pw, pv = sweep(packseq, "팩 「야간 정비반」")
    print()
    bw, bv = sweep(baseseq, "대조군 기본 42종 (단정한머리·동그란안경·나비넥타이·요정날개·중절모)")
    print("\n  ★ 판정: 팩 최악 %.1f%%  vs  기본 최악 %.1f%%   (낮을수록 '다 껴야 다 보인다')"
          % (pw*100, bw*100))
    if pw >= bw: bad("한계 가림률이 기본 대조군보다 나쁘다 — 이 팩의 기대 축이 성립하지 않는다")
    else: ok("팩이 대조군보다 %.1f%%p 덜 지운다" % ((bw - pw) * 100))
    return pw, bw


for s in (0.75, 0.60):
    run(s)
marginal(0.75)
marginal(0.60)
print("\n╚══ 결과: %s ══╝" % ("전수 통과 (위반 0건)" if not fail else "위반 %d건" % len(fail)))
sys.exit(1 if fail else 0)
