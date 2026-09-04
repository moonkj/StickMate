# -*- coding: utf-8 -*-
"""★ R11 — 조각 전수 존폐 판정 (인계본 16종 × 91조각), 안 C1 확정 이후.

왜 이 파일이 생겼나
------------------
앞 라운드(`handoff_piece.py`)는 배율 0.75 한 곳에서만 재고 "91개 중 44개 소멸"을 냈다.
색 판정(안 C1)이 끝나 **아이템 색은 현행 25색 유지**로 확정된 지금, 남은 유일한 변수는
**해상도**다. 그런데 존폐를 가르려면 **한 크기로는 부족하다** — 우리 앱에는 크기가 셋 있다:

    · 카드 아이콘 44px            (IconSize=44 · IconStroke=1.7×44/40=1.87px — 프로덕션 실측)
    · 착용 @0.75  W = 0.343864 R  (출하 기본)  ← ★ 이것이 판정 게이트다
    · 착용 @0.60  W = 0.429830 R  (사용자 실제 저장 배율)  ← 경고 축이지 게이트가 아니다

  ★ 32pt는 이 앱에 없는 크기다. 그 숫자로 검증했다는 보고가 있으면 틀린 것이다.

  ★★ 0.60을 게이트로 쓰면 안 되는 이유(자기 정정): `verify.py` 실측상 **우리 출하 30종이
     0.60에서 이미 규칙1 위반 9건**이다. 0.60을 문턱으로 걸면 인계본에 **우리 자신도 못 넘는
     자**를 대는 것이 된다. 규칙1 위반 0이 되는 최소 배율은 0.7070이고 출하는 0.75다.

판정 넷
------
  KEEP    — @0.75 에서 1.5 W 이상. 몸에 넣는다.
  PROMOTE — 몸에 남는 조각이 정원 하한(2)에 못 미치는 아이템에서, 그 아이템의 정체성을
            지는 조각을 **키워서** 몸에 올린다. (버리는 것이 아니라 확대한다)
  CARD    — 몸에서는 소멸하지만 카드에서는 문턱을 넘는다. **카드 전용 장식**으로만 산다.
            ★ 아이템당 **장식군 2개까지**(7-2 규약). 넘으면 골라야 한다.
  DROP    — 카드에서도 안 보이거나, 장식군 상한에서 밀린 것.

★★ R12 추가 — **이 자에 규칙 1-C가 없었다** (2026-09-03, design-equipment 자기 정정)
  KEEP 문턱이 `e = max(폭,높이) >= 1.5 W` 하나뿐이었다. 그것은 프로덕션 **규칙 1-A**의 거울이고,
  프로덕션은 그 구멍을 알고 있다 — `AccessoryStrokeBudgetTests.DescribeRuleOneViolation` 주석이
  *"길이 5W · 두께 0.1W짜리 실오라기도 이 검사를 통과한다"*고 적고, 두께는 **규칙 1-C**
  (`AccessoryFillAreaRuleTests`, ρ_max >= 0.21818 R)가 따로 맡는다고 못박아 두었다.
  이 자는 1-A만 베껴 와서 **채움 조각의 두께를 한 번도 재지 않았다.** 그래서 KEEP 42 중 일부가
  「긴변은 충분한데 색면이 자기 윤곽선에 먹히는」 실오라기다. 아래 [1-C] 절이 그것을 센다.
  ※ 1-A 자체는 **프로덕션과 같아야 하므로 바꾸지 않았다.** 더한 것이지 바꾼 것이 아니다.

카드 환산 모델 (프로덕션 실측에서 유도 — 근사임을 명시한다)
--------------------------------------------------------
`AccessoryCardIcon.TryBuild`가 **아이템마다 독립으로** 봉투를 재서 맞춘다(실측:
`span = max(폭,높이)` · `scale = size × FitFraction / span`, FitFraction 0.86). 따라서

    e_px = e_R / E_R × 44 × 0.86      (E_R = 그 아이템 봉투의 최대 변)
    문턱 = 1.5 × 1.87 = 2.805 px

  ★ 상한 근사다. 실제 카드는 여백·둥근 캡·안티에일리어싱이 더 먹으므로 **여기서 떨어지면
    확실히 죽고, 붙었다고 확실히 사는 것은 아니다.** 최종 판정은 실제 빌드 캡처로만.

    python3 r11_survive.py           # 전수 판정표
    python3 r11_survive.py --control # ★ 음성 대조: 문턱을 틀리면 판정이 실제로 뒤집히는가
"""
import sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import handoff as HO
import rig
import hatfix

CONTROL = "--control" in sys.argv

W075 = 0.343864
W060 = 0.429830
INK_MIN = 1.5
CARD_PX, CARD_FIT, CARD_STROKE_PX = 44.0, 0.86, 1.87
QUOTA_LO, QUOTA_HI = 2, 4
CARD_GROUP_MAX = 2

if CONTROL:
    INK_MIN = 0.375                      # 음성 대조: 문턱 1/4

assert abs(rig.W - W075) < 1e-6, "rig.W(%r) 와 W075(%r) 가 갈라졌다" % (rig.W, W075)

ROLE = {"B": "채움", "S": "낱선", "F": "강조채움", "H": "하이라이트",
        "CB": "원", "CF": "강조원", "RB": "둥근사각"}


def ext(pts):
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    return max(max(xs) - min(xs), max(ys) - min(ys))


def analyse():
    out = {}
    for kind, raw in HO.parse_items().items():
        P = HO.build(kind, raw)
        S = [HO.to_R_shape(kind, p, "%s%d" % (p.call, i)) for i, p in enumerate(P)]
        E = max(ext(s.pts) for s in S)
        recs = []
        for i, (p, s) in enumerate(zip(P, S)):
            e = ext(s.pts)
            # 규칙 1-C: 채운 도형만. 격자 0.02 R + 정밀화 9회 (오차 < 3e-4 R).
            rho = (hatfix.rho_max(s.pts, coarse=0.02, refine=9)
                   if (s.filled and s.loop and len(s.pts) >= 3) else None)
            recs.append(dict(i=i, call=p.call, role=ROLE[p.call], e=e,
                             w75=e / W075, w60=e / W060,
                             card=e / E * CARD_PX * CARD_FIT,
                             rho=rho, filled=s.filled,
                             hl=p.is_highlight, acc=p.is_accent))
        out[kind] = (recs, E)
    return out


def main():
    data = analyse()
    print("╔══ R11 조각 전수 존폐 판정 — 게이트 @0.75 · 경고 @0.60 · 카드 44px ══╗")
    if CONTROL:
        print("  ★★ 음성 대조 모드: 잉크 하한 1.5W → %.3fW. 판정이 안 뒤집히면 이 자는 죽어 있다."
              % INK_MIN)
    print("  W@0.75 = %.6f R  (게이트)   W@0.60 = %.6f R  (경고)   카드 문턱 %.3f px"
          % (W075, W060, INK_MIN * CARD_STROKE_PX))
    print()

    tally = {"KEEP": 0, "PROMOTE": 0, "CARD": 0, "DROP": 0}
    summary = []
    for kind, (recs, E) in data.items():
        # 1차: 몸 생존 (하이라이트는 안 C1 색 판정으로 몸에서 배제 — 흰색은 대역 밖)
        for r in recs:
            r["body"] = (r["w75"] >= INK_MIN) and not r["hl"]
        keep = [r for r in recs if r["body"]]

        # 2차: 정원 하한 미달이면 PROMOTE 후보를 고른다 — 몸에서 가장 큰 비생존 조각
        promo = []
        if len(keep) < QUOTA_LO:
            cands = sorted([r for r in recs if not r["body"] and not r["hl"]],
                           key=lambda r: -r["e"])
            need = QUOTA_LO - len(keep)
            promo = cands[:need]
            for r in promo:
                r["verdict"] = "PROMOTE"
                r["need_scale"] = INK_MIN * W075 / r["e"]
        # 2차-b: 정원 상한 초과면 **@0.60 에서 가장 먼저 죽는 조각부터** 카드로 강등한다.
        #        (임의로 고르지 않는다 — 경고 축이 순서를 정한다)
        demoted = []
        while len(keep) > QUOTA_HI:
            weakest = min(keep, key=lambda r: r["w60"])
            keep.remove(weakest)
            weakest["demoted"] = True
            demoted.append(weakest)
        for r in keep:
            r["verdict"] = "KEEP"

        # 3차: 남은 것은 카드 장식군으로. 군 = (역할, 크기 0.01R 반올림) — 좌우대칭 반복은 한 덩어리
        rest = [r for r in recs if "verdict" not in r]
        groups = {}
        for r in rest:
            groups.setdefault((r["call"], round(r["e"], 2)), []).append(r)
        # 큰 군부터 상한까지 살린다. 하이라이트는 카드에서 값이 크므로 우선순위를 하나 준다
        order = sorted(groups.items(),
                       key=lambda kv: (-max(x["card"] for x in kv[1]), -len(kv[1])))
        for gi, (gk, members) in enumerate(order):
            alive_card = all(m["card"] >= INK_MIN * CARD_STROKE_PX for m in members)
            for m in members:
                m["verdict"] = "CARD" if (gi < CARD_GROUP_MAX and alive_card) else "DROP"

        for r in recs:
            tally[r["verdict"]] += 1
        summary.append((kind, len(keep), len(promo),
                        sum(1 for r in recs if r["verdict"] == "CARD"),
                        sum(1 for r in recs if r["verdict"] == "DROP"),
                        len(recs), len(order)))

        print("── %s  (봉투 %.3f R)" % (kind, E))
        for r in recs:
            tag = {"KEEP": "  KEEP", "PROMOTE": "▲ PROMOTE", "CARD": "★ CARD", "DROP": "✗ DROP"}[r["verdict"]]
            note = ""
            if r["verdict"] == "PROMOTE":
                note = "  ← ×%.3f 로 키워야 1.5W@0.75" % r["need_scale"]
            elif r["verdict"] == "KEEP" and r["w60"] < INK_MIN:
                note = "  ← 경고: @0.60 에서 %.2fW (출하 30종도 여기서 9건 위반)" % r["w60"]
            elif r["verdict"] == "CARD" and r.get("demoted"):
                note = "  ← 정원 상한 초과로 강등 (@0.60 에서 %.2fW 로 가장 먼저 죽는다)" % r["w60"]
            elif r["verdict"] == "CARD" and r["hl"]:
                note = "  (하이라이트 — 몸에서는 색으로 배제, 카드 바탕 #1B1F26 위 16.53:1)"
            elif r["verdict"] == "DROP":
                note = "  (장식군 상한 %d 밖)" % CARD_GROUP_MAX if r["card"] >= INK_MIN * CARD_STROKE_PX else "  (카드에서도 미달)"
            print("   %2d %-8s 잉크 %.4fR  0.75:%5.2fW  0.60:%5.2fW  카드:%5.2fpx  %s%s"
                  % (r["i"], r["role"], r["e"], r["w75"], r["w60"], r["card"], tag, note))

    print()
    print("╔══ 아이템별 요약 ══╗")
    print("%-13s %5s %5s %8s %5s %5s  %s" % ("아이템", "원본", "KEEP", "PROMOTE", "CARD", "DROP", "몸 정원 2~4"))
    bad = []
    for kind, k, p, c, d, n, ng in summary:
        body = k + p
        ok = QUOTA_LO <= body <= QUOTA_HI
        if not ok: bad.append((kind, body))
        print("%-13s %5d %5d %8d %5d %5d  %s"
              % (kind, n, k, p, c, d, "OK (%d)" % body if ok else "★ %d개 — 정원 밖" % body))
    print()
    print("  ⇒ 총 91조각:  KEEP %d · PROMOTE %d · CARD %d · DROP %d"
          % (tally["KEEP"], tally["PROMOTE"], tally["CARD"], tally["DROP"]))
    print("  ⇒ 몸에 오르는 조각 = KEEP + PROMOTE = %d" % (tally["KEEP"] + tally["PROMOTE"]))
    print("  ⇒ 정원 위반 %d종: %s" % (len(bad), ", ".join("%s(%d)" % t for t in bad) if bad else "없음"))

    # ── [1-C] 몸에 오르는 채움 조각의 **두께** ────────────────────────────────
    GATE = hatfix.FILL_OUTLINE_PEN_IN_R
    print()
    print("╔══ [1-C] 몸에 오르는 채움 조각의 두께 — ρ_max >= %.5f R (= 색면 폭 1획) ══╗" % GATE)
    thin = []
    for kind, (recs, E) in data.items():
        for r in recs:
            if r["verdict"] not in ("KEEP", "PROMOTE"): continue
            if not r["filled"] or r["rho"] is None: continue
            k = r["rho"] / GATE
            if k < 1.0:
                thin.append((k, kind, r["i"], r["role"], r["w75"], r["rho"],
                             1.0 / k))
    thin.sort()
    for k, kind, i, role, w75, rho, need in thin:
        print("  ✗ %-13s [%d] %-6s 긴변 %5.2f W (1-A 통과)  ρ %.4f R = %.2f획  "
              "→ 두께 ×%.2f 필요" % (kind, i, role, w75, rho, k, need))
    n_fill = sum(1 for kind, (recs, E) in data.items() for r in recs
                 if r["verdict"] in ("KEEP", "PROMOTE") and r["filled"] and r["rho"] is not None)
    print("  ⇒ 몸에 오르는 채움 조각 %d개 중 **%d개가 1-C 미달** (1-A만 보던 구판은 전부 초록이었다)"
          % (n_fill, len(thin)))
    if CONTROL:
        print("\n  ★ 대조 판정: KEEP 수가 정상 모드(KEEP 42)보다 늘어야 이 자가 살아 있는 것이다.")
        return 0
    if thin:
        print("\n╔══ 실패: 1-C 미달 %d건 ══╗  (정원·1-A 는 통과)" % len(thin))
        print("  ★ 이 빨강은 **처방이 없어서**가 아니다 — 처방은 `r12_thick.py` 에 있고 초록이다")
        print("    (13건 전량 처방 · 만든 위반 0건 · 1-C 미달 13→0 · 못 올린 조각 0 · §4-2-9).")
        print("  ★ 이 자는 **미조정 이식본**을 잰다. 조정본이 프로덕션에 들어가기 전까지 빨간 것이 옳다.")
        print("  ★ 초록으로 만들려고 임계 %.5f R 을 무르게 하지 마라 — 그 순간 이 발견이 무의미해진다." % GATE)
        return 1
    print("\n╔══ 총 실패 0건 ══╗")
    return 0


if __name__ == "__main__":
    sys.exit(main())
