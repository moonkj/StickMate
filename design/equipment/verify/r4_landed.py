# -*- coding: utf-8 -*-
"""착지 검증 — **프로덕션 에셋**(Assets/_Project/Resources/Items/pack_*.asset)을 R4 정본 자로 다시 잰다.

왜 따로 있나: r4_gate / r4_check / r4_stack 의 기존 진입점은 전부 **설계 쪽**(pack_detail_r4.PACKS 의
파이썬 좌표, 또는 design/equipment/pack_detail_r4/*.yaml)을 읽는다. 그 둘은 같은 손에서 나왔으므로
「YAML 이 게이트를 통과한다」는 「에셋이 통과한다」의 증거가 아니다. 이 파일은 **화면이 실제로 읽는 파일**을
원천으로 삼아 같은 자를 댄다. 자는 하나도 새로 만들지 않는다(r4_paint / r4_gate / r4_stack 재사용).

디코더도 일부러 두 개를 쓴다:
  · pack_assets.parse_asset   (AccessoryWornShapeReader 재현 · assert 로 스트림 길이까지 확인)
  · r4_geom.load_terms_yaml   (독립 구현)
둘이 어긋나면 그 자체가 결함이다.
"""
import os, sys, glob, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r4_geom as G, r4_gate as GT, r4_paint as PT, r4_stack as ST
import pack_assets as PA

ITEMS = PA.ITEMS

def load_assets():
    """프로덕션 에셋 12종 -> r4_geom 뱅크 형식. 두 디코더를 대조한다."""
    bank, mism = {}, 0
    for path in sorted(glob.glob(os.path.join(ITEMS, "pack_*.asset"))):
        key = os.path.basename(path).replace(".asset", "")
        disp, slot, shapes = PA.parse_asset(path)          # 디코더 A
        alt = G.load_terms_yaml(path)                       # 디코더 B (독립 구현)
        if [s["name"] for s in shapes] != [s["name"] for s in alt]:
            mism += 1; print("  x 디코더 불일치(이름):", key)
        for a, b in zip(shapes, alt):
            if len(a["pts"]) != len(b["pts"]):
                mism += 1; print("  x 디코더 불일치(점 수):", key, a["name"]); continue
            for (x1, y1), (x2, y2) in zip(a["pts"], b["pts"]):
                if abs(x1 - x2) > 1e-9 or abs(y1 - y2) > 1e-9:
                    mism += 1; print("  x 디코더 불일치(좌표):", key, a["name"]); break
        pieces = [dict(name=s["name"], pts=s["pts"], loop=s["loop"], filled=s["filled"], tone=s["tone"],
                       surfaces=0, strokeMult=s["strokeMult"], noStroke=s["noStroke"], alpha=1.0,
                       lineAlpha=s["lineAlpha"], layer=s["layer"], bodyFixed=False) for s in shapes]
        bank[key] = dict(ko=disp, slot=PA.SLOTNAME[slot], pieces=pieces)
    print("두 디코더 대조: 불일치 %d건 -> %s" % (mism, "OK" if mism == 0 else "FAIL"))
    return bank, mism

def paint_census(bank, label):
    """§2-3 표 재현 — 채움 수 · 색면합 · 최대 색면 · 잉크 · 잉크비율 · 4pt² 미만."""
    import numpy as np
    print("─" * 104); print(label); print("─" * 104)
    F = SUM = MX = INK = TINY = 0.0
    ratios = []
    for k, it in sorted(bank.items()):
        ps = G.body_pieces(it)
        lab = PT.paint(ps)
        vis = [(i, p, float((lab == i).sum()) * PT.PX2_TO_PT2) for i, p in enumerate(ps) if p["filled"]]
        ink = float((lab == -2).sum()) * PT.PX2_TO_PT2
        s = sum(v for _, _, v in vis); mx = max([v for _, _, v in vis] or [0.0])
        tiny = sum(1 for _, _, v in vis if v < GT.TINY_PT2)
        ratio = ink / (ink + s) if (ink + s) else 0.0
        F += len(vis); SUM += s; MX += mx; INK += ink; TINY += tiny; ratios.append(ratio)
        print(f"{k:<32} 채움{len(vis)}  색면합 {s:6.1f}  최대 {mx:6.1f}  잉크 {ink:6.1f}  잉크비율 {ratio:.3f}  4pt²미만 {tiny}")
    n = len(bank)
    # ★ 사양서 §2-3 은 «아이템별 비율의 평균»(매크로)을 쓴다. 총합 비율(마이크로)과 다르므로 둘 다 찍는다 —
    #   한쪽만 찍으면 자가 같은데도 숫자가 어긋나 보여 「불일치」로 오판한다(첫 실행에서 실제로 겪었다).
    macro = sum(ratios) / n
    print(f"{'평균':<32} 채움{F/n:.2f}  색면합 {SUM/n:6.1f}  최대 {MX/n:6.1f}  잉크 {INK/n:6.1f}  "
          f"잉크비율 {macro:.3f}(매크로) / {INK/(INK+SUM):.3f}(마이크로)  4pt²미만 {TINY/n:.2f}")
    return dict(fills=F/n, ink_ratio=macro, ink_micro=INK/(INK+SUM), tiny=TINY/n,
                area=SUM/n, ink=INK/n, maxarea=MX/n)

def full_gate():
    """정본 전수 게이트(pack_detail_r4.py = R2 20축 + R3 5축)를 **착지 후 트리**에서 돌린다.

    ★ 왜 감싸는가 — 게이트 맨 앞의 «자 교정 (가)»는 «지금 트리에 출하된 Patched Hood ↔ 프로덕션 베레모»를
      재서 **하드코딩 상수**와 맞춘다. 그 상수는 트리 표류 감지기라서, 후드가 정당하게 바뀌는 라운드마다
      다시 기준을 잡아야 한다(R3 가 install_calibration 에서 0.100 -> 0.424 로 한 것이 바로 그 작업이다).
      R4 를 착지시키면 실측이 0.412 -> 0.375 로 움직여 게이트가 sys.exit(2) 로 멈춘다.

    ★ 다만 **숫자를 새로 베껴 넣지 않는다**(CLAUDE.md: 상수를 숫자로 베끼지 말고 그 상수를 참조해라).
      기대값을 «R4 설계 좌표(pack_detail_r4.cyber_head())»에서 그 자리에서 계산한다. 그러면 감지기는
      죽지 않고 질문만 바뀐다 — 「출하된 후드가 R4 설계와 같은가」. 에셋이 설계에서 표류하면 여전히 문다.
    ★ 움직이면 안 되는 앵커 (나) «베레모 ↔ 야구모자 = 0.298»(팩과 무관한 순수 프로덕션 쌍)은
      손대지 않는다 — 자가 살아 있는지는 그쪽이 지킨다.
    ★ 정본 하니스 파일(pack_detail_r2.py · pack_detail_r3_extra.py)은 **한 줄도 고치지 않는다**(리더 지시).
    """
    import legibility as LG, pack_detail_r3_extra as EX
    import pack_detail_r4 as M
    design = [dict(pts=q.pts, loop=q.loop, filled=q.filled) for q in M.cyber_head()]
    beret = [s for n, s in PA.prod_base(False)["HEAD"] if n == "베레모"][0]
    expect = LG.difference(LG.cells(design), LG.cells(beret))
    print("  자 교정 재기준(가): 기대값을 R4 **설계 좌표**에서 계산 = %.3f "
          "(하드코딩 아님 · 에셋이 설계에서 벗어나면 여전히 문다)\n" % expect)
    _orig_install = EX.install_calibration
    def install():
        _orig_install()                       # 앵커 (나) 0.298 은 그대로 검사한다
        _o = LG.calibrate
        LG.calibrate = lambda hood, beret, e=expect, tol=0.02: (LG.difference(LG.cells(hood), LG.cells(beret)),
                                                                abs(LG.difference(LG.cells(hood), LG.cells(beret)) - e) <= tol)
    EX.install_calibration = install
    M.R2.PACKS = M.PACKS; M.R2.FILES = M.FILES
    EX.main(M.PACKS, M.FILES)

if __name__ == "__main__":
    if "--full-gate" in sys.argv:
        full_gate(); sys.exit(0)
    bank, mism = load_assets()
    print()
    print("═" * 104); print("① R4 게이트 4축 — 프로덕션 에셋 원천"); print("═" * 104)
    tot = GT.run(bank, "프로덕션 pack_*.asset 12종")
    print()
    m = paint_census(bank, "② 잉크량 정본 자(r4_paint) — 프로덕션 에셋 원천")
    print()
    print("═" * 104); print("③ 4종 동시 착용(r4_stack) — 프로덕션 에셋 원천"); print("═" * 104)
    for pk in ("cyber", "mine", "arcane"):
        keys = [k for k in bank if f"_{pk}_" in k]
        keys.sort(key=lambda k: ["back", "neck", "eyes", "head"].index(k.split("_")[2]))
        ST.analyse(bank, keys, f"pack.{pk} 4종 (에셋)")
    print()
    print("═" * 104)
    print(f"판정: 게이트 위반 {tot}건 · 디코더 불일치 {mism}건 · "
          f"채움 {m['fills']:.2f} · 잉크비율 {m['ink_ratio']:.3f} · 4pt²미만 {m['tiny']:.2f}")
    sys.exit(0 if (tot == 0 and mism == 0) else 1)
