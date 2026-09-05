# -*- coding: utf-8 -*-
"""R15 — 두 표면 좌표 전문 + 데이터 계약 통계 → r15_coords.txt / 표준출력.
    python3 r15_dump.py            # 통계(표준출력) + r15_coords.txt(좌표 전문, R 단위 4자리)
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
import r15_model as M

HERE = os.path.dirname(os.path.abspath(__file__))
CARD_POINT_BUFFER = 128     # AccessoryCardIcon._points 길이(프로덕션 실측 :52)

def stats(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 데이터 계약 통계 (r15_model 실측) ==")
    w("   %-13s 카드조각 몸조각 | 카드 보조색 몸 보조색 | 카드 최대점수 몸 최대점수 | 등급 사용(몸) | 톤3 몸/카드" % "kind")
    maxc = maxb = 0; acc_c = acc_b = 0; over128 = []
    for k in M.KINDS:
        cs = M.CARD_SET[k]; bs = [p for p in M.BODY_SET[k] if p.on(M.BODY)]
        nc, nb = len(cs), len(bs)
        ac = sum(1 for p in cs if p.tone == 1); ab = sum(1 for p in bs if p.tone == 1)
        mpc = max(len(p.pts) for p in cs); mpb = max(len(p.pts) for p in bs)
        grades = sorted({p.grade for p in bs})
        h3b = sum(1 for p in bs if p.tone == 3); h3c = sum(1 for p in cs if p.tone == 3)
        for p in cs:
            if len(p.pts) > CARD_POINT_BUFFER: over128.append((k, p.src, len(p.pts)))
        maxc, maxb = max(maxc, nc), max(maxb, nb); acc_c, acc_b = max(acc_c, ac), max(acc_b, ab)
        w("   %-13s %6d %6d | %8d %8d | %10d %10d | %-12s | %d/%d" % (k, nc, nb, ac, ab, mpc, mpb, ",".join(map(str, grades)), h3b, h3c))
    w("   → 표면별 정원(실측 최대): Card ≤ %d · Body ≤ %d.  보조색 최대: Card %d · Body %d" % (maxc, maxb, acc_c, acc_b))
    w("   → 카드 점 버퍼 %d 초과 조각: %s" % (CARD_POINT_BUFFER, over128 if over128 else "없음"))
    # 몸 톤 4(보조색 위 하이라이트) 필요 여부
    need4 = [(k, p.name, p.under) for k in M.KINDS for p in M.BODY_SET[k]
             if p.tone == 3 and p.on(M.BODY) and isinstance(p.under, str)
             and any(q.name == p.under and q.tone == 1 for q in M.BODY_SET[k])]
    w("   → 보조색 채움 위에 놓인 몸 하이라이트(톤 3 만으로는 색이 틀리는 것): %s" % (need4 if need4 else "없음"))
    # 카드 알파 스펙트럼
    alphas = sorted({p.alpha for k in M.KINDS for p in M.CARD_SET[k] if p.filled and p.alpha is not None})
    lal = sorted({p.line_alpha for k in M.KINDS for p in M.CARD_SET[k] if not p.filled and p.line_alpha < 1.0})
    w("   → 카드 사전합성이 읽는 채움 알파 값: %s (None=그라디언트→%.2f)" % (alphas, M.GRAD_MEAN))
    w("   → 카드 사전합성이 읽는 낱선 알파 값: %s" % lal)

def dump(path):
    with open(path, "w", encoding="utf-8") as f:
        f.write("# R15 두 표면 좌표 전문 — R 단위(머리 중심 원점 · y 위로 · +x 진행 방향), 소수 4자리\n")
        f.write("# 생성: python3 r15_dump.py   (r15_model.py 가 정본이다 — 이 파일을 손으로 고치지 마라)\n")
        f.write("# 필드: name tone grade surfaces(B/C/BC) filled loop alpha line_alpha src note\n\n")
        for k in M.KINDS:
            f.write("=" * 78 + "\n%s  %s / %s\n" % (k, M.KO[k], M.SLOT[k]))
            for title, ps in (("CARD (인계본 정면 기하, 레이어 전부)", M.CARD_SET[k]),
                              ("BODY (3/4 기하 + 레이어; 몸 생존 조각만 B 비트)", M.BODY_SET[k])):
                f.write("-- %s\n" % title)
                for p in ps:
                    x0, y0, x1, y1 = rig.bounds(p.pts)
                    f.write("  %-22s t%d g%d %-2s filled=%d loop=%d alpha=%s lalpha=%.2f src=%-4s 잉크 %.3f×%.3fR  %s\n" %
                            (p.name, p.tone, p.grade, {1: "B", 2: "C", 3: "BC"}[p.surfaces], int(p.filled), int(p.loop),
                             ("%.2f" % p.alpha) if p.alpha is not None else "grad", p.line_alpha, p.src, x1 - x0, y1 - y0, p.note))
                    f.write("      " + " ".join("(%.4f,%.4f)" % q for q in p.pts) + "\n")
            f.write("\n")

if __name__ == "__main__":
    stats()
    dump(os.path.join(HERE, "r15_coords.txt"))
    print("wrote r15_coords.txt")
