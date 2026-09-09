# -*- coding: utf-8 -*-
"""R4 게이트 5축 — 「중절모급」을 숫자로 못박은 것.

문턱은 발명하지 않았다. **출하 중절모(사용자가 지목한 그 물건)를 재서** 뽑았고,
야구모자·왕관을 같은 자로 재어 «통과/불통과»가 눈 판정과 일치하는지 대조했다
(왕관은 R23 에 A/B 라운드가 따로 있었던 아이템이다 — 이 자가 그것을 스스로 찍어내면 자가 살아 있는 것이다).

  R4-1 티끌 0        : 채움 조각의 「읽히는 색면」 >= 4.0 pt²  (윤곽 침식 뒤 남는 재질색 면적)
  R4-2 채움 <= 4     : 색면을 내는 조각 수 (중절모 4 · 야구모자 4 · 후드 3 · 왕관 9)
  R4-3 얇은판 규율    : 잉크 사각형 짧은 변 < 2W(4.0pt) 인 채움은 noStroke=1 (잉크는 열린 낱선으로만)
  R4-4 곡선 해상도    : 아이템의 «꼭짓점 면오차» 평균 <= 0.0060R  (출하 인계본 12종 중앙값 0.0009R · 최대 0.0025R)
       ★ 이 축은 **보조 축**이다. 면오차 0.0060R 은 착용 크기에서 0.035pt = 0.07 device px 이고
         팩 R3 의 최악값(0.0654R)조차 0.38pt = 0.76 device px 다 — **눈으로 보이는 차이가 아니다**.
         그래도 두는 이유는 «중절모와 같은 방식으로 그렸는가»의 흔적이기 때문이고,
         「조잡함」을 이 축으로 설명하지 않는다(§2 에 산술이 있다).
  R4-5 동시 착용      : 팩 4종 동시 = 연결성분 >= 2 · 머리 원반 가림 <= 80%   (r4_stack.py)
"""
import math, sys
import r4_geom as G, r4_ink, r4_survive as S, r4_paint as PT

TINY_PT2   = 4.0
SOLID_PT2  = 12.0               # 참고 지표(게이트 아님)
FILL_MAX   = 4
THIN_W     = 2.0 * G.W          # (R4-3) 닫힌 윤곽 금지선 = 2획 = 0.6877R = 4.00pt
SMALL_SPAN = 3.0 * G.W          # (R4-1 면제 a) 「원래 작은 부품」의 긴 변 = 3획 = 1.0316R = 6.00pt
#   ★ 둘은 다른 질문이다. THIN_W 는 «윤곽을 두르면 색이 절반도 안 남는가»(물리),
#     SMALL_SPAN 은 «이 조각이 원래 작은가»(이 게임의 실제 모습 — 출하 선글라스 렌즈 색면은 2.6pt² 다).
#   ★ 물리적 근거: 획은 윤곽 위에 **중심 정렬**이라 안쪽으로 W/2 를 먹는다. 짧은 변 d 인 채움에서
#     재질색이 남는 두께는 d - W 다. d = 2W 가 「재질색이 절반은 남는다」의 경계이고, 그 아래는
#     «색면»이 아니라 «잉크 막대»다. 중절모는 이 규율을 지킨다(관 10.73pt 는 윤곽 · 챙 1.64/2.41pt 와
#     띠 3.06pt 는 noStroke + 열린 잉크 낱선).
FACET_R    = 0.0060             # R — 꼭짓점 면오차 평균 상한(보조 축)

def facets(p):
    """꼭짓점 단위 면오차 s = (min(a,b)/2)·tan(θ/4) — 등각 다각형 근사에서 한 변이 이상 곡선에서
    벗어나는 최대 거리. θ >= 45°(의도된 모서리)와 θ ≈ 0(직선)은 제외한다.
    ★ 첫 시안은 «곡선 구간 변 길이»였는데 그 자는 **둥근 사각형의 긴 직선변**을 곡선으로 오인했다
      (모서리 호의 끝점 꺾임이 15° 라 «곡선 꼭짓점»으로 분류된다). 직선은 아무리 길어도 면이 안 생긴다."""
    pts, loop = p["pts"], p["loop"]; n = len(pts)
    tv = dict(G.turns(pts, loop))
    curve = {i: (1.0 <= th < 45.0) for i, th in tv.items()}
    out = []
    for i, th in tv.items():
        if not curve.get(i): continue
        # ★ **고립된 얕은 모서리는 곡선이 아니다.** 양옆도 곡선 꼭짓점일 때만 «곡선 위의 점»으로 센다.
        #   (실측 사고: 고글 렌즈의 의도된 얕은 모서리 5개가 0.0298R 로 찍혀 아이템 전체를 빨갛게 만들었다.
        #    그 자리는 폴리곤화 오차가 아니라 **설계된 각**이다.)
        if not (curve.get((i - 1) % n) and curve.get((i + 1) % n)): continue
        a = math.dist(pts[(i - 1) % n], pts[i]); b = math.dist(pts[i], pts[(i + 1) % n])
        out.append((min(a, b) / 2.0) * math.tan(math.radians(th) / 4.0))
    return out

def check(item):
    ps = G.body_pieces(item)
    lab = PT.paint(ps)
    fills = [p for p in ps if p["filled"]]
    vis = []
    for i, p in enumerate(ps):
        if not p["filled"]: continue
        vis.append((i, p, float((lab == i).sum()) * PT.PX2_TO_PT2))
    f = []
    for i, p, v in vis:
        if v >= TINY_PT2: continue
        m = G.piece_metrics(p)
        if max(m["bw"], m["bh"]) < SMALL_SPAN:        # 면제 (a) — 원래 작은 부품(선글라스 렌즈 2.6pt²)
            continue
        if p["layer"] == 1 and PT.occluded_frac(ps, i) >= 0.5:
            continue                                   # 면제 (b) — **뒤층** 조각은 정면에서 안 보이는 것이 설계다
                                                       #   (중절모 Piece_B2far 가림 81% · 야구모자 86% · 왕관 뒷벽 88%)
        f.append(("R4-1", f"{p['name']} 읽히는 색면 {v:.1f}pt² < {TINY_PT2} (긴 변 {max(m['bw'],m['bh'])*G.R_PT_075:.1f}pt · 가림 {PT.occluded_frac(ps,i)*100:.0f}%)"))
    if len(fills) > FILL_MAX: f.append(("R4-2", f"채움 조각 {len(fills)}개 > {FILL_MAX}"))
    solid = sum(1 for _, _, v in vis if v >= SOLID_PT2)
    for p in fills:
        m = G.piece_metrics(p)
        short = min(m["bw"], m["bh"])
        if short < THIN_W and not p["noStroke"] and p.get("lineAlpha", 1.0) > 0:
            f.append(("R4-3", f"{p['name']} 짧은변 {short*G.R_PT_075:.2f}pt < 3획인데 닫힌 윤곽"))
    fs = [x for p in ps for x in facets(p)]
    if fs:
        m = sum(fs) / len(fs)
        if m > FACET_R: f.append(("R4-4", f"면오차 평균 {m:.4f}R = {m*G.R_PT_075:.3f}pt > {FACET_R}R"))
    return f, solid, sorted((v for _, _, v in vis), reverse=True)

def run(bank, label, expect=None):
    print("═" * 106); print(label); print("═" * 106)
    tot = 0
    for k, it in sorted(bank.items()):
        f, solid, vis = check(it)
        tot += len(f)
        axes = sorted(set(a for a, _ in f))
        head = f"{k[:32]:<32} 확실색면 {solid}  색면 {['%.0f'%v for v in vis[:5]]}"
        print(f"{head}   {'통과' if not f else '위반 %d %s' % (len(f), axes)}")
        for a, m in f: print(f"      x {a}  {m}")
    print("─" * 106); print(f"합계 위반 {tot}건\n")
    return tot

if __name__ == "__main__":
    print(f"# 문턱: 티끌 {TINY_PT2}pt² · 얇은판 {THIN_W:.4f}R={THIN_W*G.R_PT_075:.2f}pt · 면오차 {FACET_R}R\n")
    run(G.load_handoff(), "① 출하 인계본 12종 — 자 교정용(중절모/야구모자는 통과해야 하고, 왕관은 걸려야 한다)")
    run({("neck_"+k): v for k, v in G.load_neck6().items()}, "② 출하 NECK 6종")
    run(G.load_pack("pack_detail_r3"), "③ 팩 R3 (현행)")
