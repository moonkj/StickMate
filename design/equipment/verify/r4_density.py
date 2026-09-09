# -*- coding: utf-8 -*-
"""R4 §1 — «중절모 급 퀄리티»를 숫자로 정의하기 위한 전수 실측.
출하 18종(인계본 12 + NECK 6) vs 팩 12종(R2/R3) — 몸 조각만, 같은 자."""
import sys, math, collections
import r4_geom as G

def rows(bank, label):
    out = []
    for key, it in bank.items():
        ps = G.body_pieces(it)
        if not ps: continue
        ms = [G.piece_metrics(p) for p in ps]
        n_pts = sum(m["n"] for m in ms)
        spans = sorted((m["span"] for m in ms), reverse=True)
        # 「잉크 문턱 조각」 = 최소 변(span 짧은 축)이 1.5W 미만 → 규칙 1의 잉크 사각형 하한 미만
        tiny = sum(1 for m in ms if m["span"] < 1.5 * G.W * 2)      # 최장축조차 3W(=1.03R) 미만
        sag = max(m["sagmax"] for m in ms) * G.R_PT_075
        tones = collections.Counter(p["tone"] for p in ps)
        out.append(dict(key=key, ko=it["ko"], slot=it["slot"], n=len(ps), pts=n_pts,
                        ppp=n_pts / len(ps), span1=spans[0], span2=spans[1] if len(spans) > 1 else 0,
                        spanmed=spans[len(spans)//2], tiny=tiny, sag=sag, tones=tones,
                        big=sum(1 for m in ms if m["span"] >= 2.0),
                        ink=sum(1 for p in ps if p["tone"] == 4),
                        acc=sum(1 for p in ps if p["tone"] == 1)))
    return out

def show(rs, label):
    print("═" * 122)
    print(label)
    print("═" * 122)
    print(f"{'아이템':<26}{'슬롯':<7}{'조각':>4}{'점':>5}{'점/조각':>8}{'최대span':>9}{'2위span':>8}{'중앙span':>9}"
          f"{'≥2R':>5}{'<1R':>5}{'면오차pt':>9}{'잉크t4':>7}{'보조t1':>7}")
    print("─" * 122)
    for r in sorted(rs, key=lambda r: r["slot"]):
        print(f"{r['key'][:26]:<26}{r['slot']:<7}{r['n']:>4}{r['pts']:>5}{r['ppp']:>8.1f}{r['span1']:>9.2f}"
              f"{r['span2']:>8.2f}{r['spanmed']:>9.2f}{r['big']:>5}{r['tiny']:>5}{r['sag']:>9.3f}{r['ink']:>7}{r['acc']:>7}")
    print("─" * 122)
    N = len(rs)
    print(f"{'평균':<33}{sum(r['n'] for r in rs)/N:>4.1f}{sum(r['pts'] for r in rs)/N:>5.0f}"
          f"{sum(r['pts'] for r in rs)/sum(r['n'] for r in rs):>8.1f}{sum(r['span1'] for r in rs)/N:>9.2f}"
          f"{sum(r['span2'] for r in rs)/N:>8.2f}{sum(r['spanmed'] for r in rs)/N:>9.2f}"
          f"{sum(r['big'] for r in rs)/N:>5.1f}{sum(r['tiny'] for r in rs)/N:>5.1f}"
          f"{sum(r['sag'] for r in rs)/N:>9.3f}{sum(r['ink'] for r in rs)/N:>7.1f}{sum(r['acc'] for r in rs)/N:>7.1f}")
    print()
    return rs

if __name__ == "__main__":
    print(f"# 자: W = {G.W:.6f} R = 2.00pt / 착용 R(배율0.75) = {G.R_PT_075:.4f} pt / 머리 지름 = {2*G.R_PT_075:.2f} pt")
    print("# span = 조각 잉크 사각형의 긴 변(R). 면오차 = 곡선 구간 최대 사지타(폴리곤화 오차, pt@0.75).")
    print()
    a = show(rows(G.load_handoff(), "① 출하 인계본 12종 — 몸 조각"), "① 출하 인계본 12종 (사용자가 지목한 «중절모»가 여기 있다)")
    b = show(rows(G.load_neck6(), "② 출하 NECK 6종"), "② 출하 NECK 6종 (팩과 **완전히 같은** wornShapes 표현)")
    c = show(rows(G.load_pack("pack_detail_r2"), "③ 팩 R2"), "③ 팩 12종 R2")
    d = show(rows(G.load_pack("pack_detail_r3"), "④ 팩 R3 (현행)")," ④ 팩 12종 R3 (현행 — 사용자가 «아직도 차이가 큼»이라 한 그것)")

    def agg(rs, name):
        N = len(rs); P = sum(r["n"] for r in rs)
        return (name, sum(r['n'] for r in rs)/N, sum(r['pts'] for r in rs)/P,
                sum(r['span1'] for r in rs)/N, sum(r['span2'] for r in rs)/N,
                sum(r['big'] for r in rs)/N, sum(r['tiny'] for r in rs)/N,
                sum(r['sag'] for r in rs)/N, sum(r['ink'] for r in rs)/N)
    print("═" * 100); print("요약 — 가설(«팩이 조각이 너무 많다») 판정"); print("═" * 100)
    print(f"{'집단':<22}{'조각/아이템':>11}{'점/조각':>9}{'최대span':>10}{'2위span':>9}{'≥2R조각':>9}{'<1R조각':>9}{'면오차pt':>10}{'잉크t4':>8}")
    for rs, nm in ((a, "출하 인계본 12"), (b, "출하 NECK 6"), (a + b, "출하 18 (합)"), (c, "팩 R2 12"), (d, "팩 R3 12")):
        g = agg(rs, nm)
        print(f"{g[0]:<22}{g[1]:>11.2f}{g[2]:>9.1f}{g[3]:>10.2f}{g[4]:>9.2f}{g[5]:>9.2f}{g[6]:>9.2f}{g[7]:>10.3f}{g[8]:>8.2f}")
