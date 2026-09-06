#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""design-art — 과제2 확정용. 설정창 컨트롤 면의 목표 대비를 「여유 최대화」로 푼다."""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from alphableed import (L, CR, flatten, hexc, de76, calibrate, load_tokens)

MIN_NONTEXT = 3.0
MIN_TEXT = 4.5


def main():
    calibrate()
    T = load_tokens()
    P = T["PanelSurface"][0]
    C = T["CardSurface"][0]
    M = T["CardSurfaceMuted"][0]
    ACC = T["Accent"][0]
    T1 = T["TextPrimary"][0]
    T2 = T["TextSecondary"][0]
    T3 = T["TextTertiary"][0]
    ONACC = T["OnAccentSolid"][0]
    NONTEXT = T["NonTextMuted"][0]
    DIS = T["DisabledControlInk"][0]

    def best_ink(face):
        cands = [("T1", T1), ("T2", T2), ("T3", T3), ("OnAccent", ONACC)]
        n, c = max(cands, key=lambda kv: CR(kv[1], face))
        return n, c, CR(c, face)

    print("=" * 84)
    print("E. 「면 위에 라벨이 얹히는 버튼」의 가능 구간 — 두 하한을 동시에 보는 스윕")
    print("=" * 84)
    for bn, br in (("CardSurface", C), ("PanelSurface", P)):
        print("\n  바탕 %s %s" % (bn, hexc(br)))
        print("  %-8s %-9s %-8s %-10s %-8s %-9s" % ("흰α", "면", "면대비", "최선잉크", "잉크대비", "최소여유"))
        best = None
        rows = []
        for i in range(0, 1025, 8):
            a = i / 1024.0
            f = flatten((1, 1, 1), a, br)
            fc = CR(f, br)
            n, c, ic = best_ink(f)
            margin = min(fc / MIN_NONTEXT, ic / MIN_TEXT)
            rows.append((a, f, fc, n, ic, margin))
            if fc >= MIN_NONTEXT and ic >= MIN_TEXT:
                if best is None or margin > best[5]:
                    best = (a, f, fc, n, ic, margin)
        # 사람이 읽을 요약: 하한을 넘는 구간의 경계와 최적점
        legal = [r for r in rows if r[2] >= MIN_NONTEXT and r[4] >= MIN_TEXT]
        if legal:
            print("      합법 구간: 면대비 %.2f ~ %.2f  (흰α %.3f ~ %.3f), 표본 %d/%d"
                  % (min(r[2] for r in legal), max(r[2] for r in legal),
                     min(r[0] for r in legal), max(r[0] for r in legal), len(legal), len(rows)))
            # 골짜기 확인: 합법 구간이 끊기는가
            legal_alphas = sorted(r[0] for r in legal)
            gaps = []
            for k in range(1, len(legal_alphas)):
                if legal_alphas[k] - legal_alphas[k - 1] > 0.02:
                    gaps.append((legal_alphas[k - 1], legal_alphas[k]))
            if gaps:
                for g in gaps:
                    lo_f = flatten((1, 1, 1), g[0], br)
                    hi_f = flatten((1, 1, 1), g[1], br)
                    print("      ★ 골짜기(합법 구간이 끊긴다): 흰α %.3f(면 %.2f) ~ %.3f(면 %.2f) 사이는 해가 없다"
                          % (g[0], CR(lo_f, br), g[1], CR(hi_f, br)))
        for r in rows:
            if r[0] in (0.25, 0.3125, 0.34375, 0.359375, 0.375, 0.390625, 0.421875, 0.4375, 0.453125,
                        0.46875, 0.5, 0.53125):
                flag = "합법" if (r[2] >= MIN_NONTEXT and r[4] >= MIN_TEXT) else "불가"
                print("      %-8.4f %-9s %-8.2f %-10s %-8.2f %-9.3f %s"
                      % (r[0], hexc(r[1]), r[2], r[3], r[4], r[5], flag))
        if best:
            print("      → 여유 최대점: 흰α %.4f  면 %s  면대비 %.2f  잉크 %s %.2f  최소여유 %.3f배"
                  % (best[0], hexc(best[1]), best[2], best[3], best[4], best[5]))

    print("\n" + "=" * 84)
    print("F. 채택안 적용 후 설정창 밝기 위계 (카드 바탕 #1B1F26 기준)")
    print("=" * 84)
    face_card = flatten((1, 1, 1), 0.4570, C)
    face_panel = flatten((1, 1, 1), 0.4756, P)
    edge_card = None
    for i in range(1025):
        f = flatten((1, 1, 1), i / 1024.0, C)
        if CR(f, C) >= MIN_NONTEXT:
            edge_card = (f, i / 1024.0, CR(f, C))
            break
    edge_panel = None
    for i in range(1025):
        f = flatten((1, 1, 1), i / 1024.0, P)
        if CR(f, P) >= MIN_NONTEXT:
            edge_panel = (f, i / 1024.0, CR(f, P))
            break
    print("  제안 테두리(카드) = %s (흰α %.4f) 대비 %.2f" % (hexc(edge_card[0]), edge_card[1], edge_card[2]))
    print("  제안 테두리(창)   = %s (흰α %.4f) 대비 %.2f" % (hexc(edge_panel[0]), edge_panel[1], edge_panel[2]))

    ladder = [
        ("행 라벨 / 스위치 손잡이 (TextPrimary)", T1, C),
        ("고른 세그먼트 면 (Accent)", ACC, C),
        ("본문 / 값 (TextSecondary)", T2, C),
        ("캡션 (TextTertiary)", T3, C),
        ("★ 제안 버튼 면 (ControlFaceOnSurface)", face_card, C),
        ("★ 제안 테두리 (입력칸·세그먼트·견본)", edge_card[0], C),
        ("스위치 트랙 OFF (TrackOnCard) — 유지", flatten((1, 1, 1), 0.09, C), C),
        ("행 구분선 (DividerOnCard) — 유지", flatten((1, 1, 1), 0.07, C), C),
        ("안 고른 세그먼트 면 (CardSurface) — 유지", C, C),
    ]
    print("\n  %-42s %-9s %-8s" % ("요소", "색", "카드대비"))
    for n, c, b in sorted(ladder, key=lambda r: -CR(r[1], r[2])):
        print("      %-42s %-9s %6.2f" % (n, hexc(c), CR(c, b)))

    print("\n  검산 — 「가장 튀는 요소」가 되는가:")
    print("      제안 버튼 면 4.49  <  고른 세그먼트 Accent %.2f  (%.2f배 아래)"
          % (CR(ACC, C), CR(ACC, C) / CR(face_card, C)))
    print("      제안 버튼 면 4.49  <  행 라벨 TextPrimary %.2f  (%.2f배 아래)"
          % (CR(T1, C), CR(T1, C) / CR(face_card, C)))
    print("      제안 버튼 면(창) %.2f  <  [✕] ChromeButtonSurface %.2f  ← 크롬이 아직 위"
          % (CR(face_panel, P), CR(flatten((1, 1, 1), 0.50, P), P)))

    print("\n  검산 — 잉크가 함께 안 바뀌면 무슨 일이 나는가(현 콜사이트가 쓰는 잉크):")
    for iname, ink in (("InkTitle(true)=TextPrimary", T1), ("InkTitle(false)=TextSecondary", T2),
                       ("InkMeta=TextTertiary(플레이스홀더)", T3), ("OnAccentSolid", ONACC)):
        print("      %-38s on %s = %5.2f  %s"
              % (iname, hexc(face_card), CR(ink, face_card),
                 "OK" if CR(ink, face_card) >= MIN_TEXT else "★ 붕괴 — 면과 잉크는 한 쌍으로 바꾼다"))

    print("\n  검산 — 입력칸을 밝은 면으로 올리면 안 되는 이유(플레이스홀더):")
    print("      TextTertiary on 제안 면 = %.2f  /  on 현재 면 #32353C = %.2f"
          % (CR(T3, face_card), CR(T3, flatten((1, 1, 1), 0.10, C))))
    print("      TextPrimary  on 현재 면 #32353C = %.2f  ← 현재 입력칸 글자는 이미 잘 읽힌다"
          % CR(T1, flatten((1, 1, 1), 0.10, C)))
    print("      → 입력칸에서 부족한 것은 <글자>가 아니라 <상자의 존재>다. 그건 테두리의 일이다.")

    print("\n  검산 — 세그먼트: 고른 칩 vs 안 고른 칩")
    print("      고른 면 Accent %s 대비 %.2f  /  안 고른 면 CardSurface 대비 1.00" % (hexc(ACC), CR(ACC, C)))
    print("      안 고른 칩 테두리 제안 %s 대비 %.2f  (지금 OutlineOnCard 1.66)"
          % (hexc(edge_card[0]), edge_card[2]))
    print("      ΔE(고른 면, 안 고른 테두리) = %.2f  — 두 상태가 색으로도 갈린다" % de76(ACC, edge_card[0]))
    print("      안 고른 칩 라벨 TextSecondary on CardSurface = %.2f (본문 하한 4.50)" % CR(T2, C))

    print("\n  검산 — 트랙을 올리면 안 되는 이유:")
    print("      OFF 트랙을 제안 면(4.49)까지 올리면 ON(Accent %.2f)과의 비가 %.2f배로 좁아진다"
          % (CR(ACC, C), CR(ACC, C) / CR(face_card, C)))
    print("      지금은 %.2f배 — 「꺼짐」이 밝기로 즉시 읽힌다. 어포던스는 손잡이(%.2f)가 이미 진다."
          % (CR(ACC, C) / CR(flatten((1, 1, 1), 0.09, C), C), CR(T1, C)))

    print("\n  검산 — 페이저 버튼(창 바탕 위 CardSurfaceMuted, 지금 %.2f):" % CR(M, P))
    print("      제안: ControlFaceOnSurface(PanelSurface) = %s 대비 %.2f" % (hexc(face_panel), CR(face_panel, P)))


if __name__ == "__main__":
    main()
