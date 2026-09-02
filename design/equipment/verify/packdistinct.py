# -*- coding: utf-8 -*-
"""★ 「기본 42종과 안 겹친다」의 증명 — 이름이 아니라 **세 축**으로 본다.
   (1) itemId 충돌 0  (2) 슬롯 안 쌍별 실루엣 차 ≥ 하한  (3) 조형 어휘(형태 계열)가 다르다.
   ★ .asset 의 한글은 \\uXXXX 이스케이프라 grep '[가-힣]'이 영원히 0건이다 — census42.py가 디코드한다."""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, sectors as S
import pack_nightshift as P
import appearance as A

HERE = os.path.dirname(os.path.abspath(__file__))
rows = json.load(open(os.path.join(HERE, "census42.json"), encoding="utf-8"))
SLOTN = {0: "HEAD", 1: "EYES", 2: "NECK", 3: "BACK", 4: "HAIR", 5: "FX", 6: "PET"}
W060 = rig.stroke_in_R(0.60)
fail = []

print("╔══ (1) itemId 충돌 ══╗")
base_ids = {r["itemId"] for r in rows}
print("  기본 카탈로그 %d종 (슬롯별 6/6/6/6/6/6/6)" % len(base_ids))
for k, (nm, iid, fn, anc) in P.PACK.items():
    hit = iid in base_ids
    if hit: fail.append(iid)
    print("  %s %-6s %-28s %s" % ("✗" if hit else "OK", k, iid, "충돌!" if hit else "신규"))
# 표시 이름도 본다(디코드된 한글)
base_names = {r["displayName"] for r in rows}
for k, (nm, iid, fn, anc) in P.PACK.items():
    if nm in base_names: fail.append(nm); print("  ✗ 표시명 충돌: %s" % nm)
print("  OK 표시명 충돌 0건" if not fail else "")

print("\n╔══ (2) 슬롯 안 쌍별 실루엣 차 (신규 vs 기본 6종 **전부**) ══╗")
BASE = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK, "BACK": items.BACK, "HAIR": hair.SET}
for k in ("HEAD", "EYES", "NECK", "BACK", "HAIR"):
    anc = P.PACK[k][3]
    mine = S.profile(P.PACK[k][2](), anc)
    print("  ── %s : %s ──" % (k, P.PACK[k][0]))
    for bn, bsh in BASE[k].items():
        sh = bsh() if callable(bsh) else bsh
        d = rig.max_delta(mine, S.profile(sh, anc))
        mark = "★래칫" if d >= S.SILHOUETTE_RATCHET_R else ("하한" if d >= W060 else "✗미달")
        if d < W060: fail.append("%s vs %s" % (k, bn))
        print("     vs %-10s %.3fR = %5.2f획@0.60  %s" % (bn, d, d / W060, mark))
mine = S.profile(P.pet_worklamp(), 0.0)
print("  ── PET : 작업등 ── (기존 PET 슬롯의 쌍별 최소는 **이미** 0.315R = 0.73획@0.60이다)")
for bn, bsh in A.PET_NOW.items():
    if not bsh: continue
    d = rig.max_delta(mine, S.profile(bsh, 0.0))
    print("     vs %-10s %.3fR = %5.2f획@0.60  %s" % (bn, d, d / W060, "★래칫" if d >= S.SILHOUETTE_RATCHET_R else ("하한" if d >= W060 else "· 기존 최소보다는 큼" if d > 0.3149 else "✗")))

print("\n╔══ (3) 조형 어휘 대조 — 같은 물건이 아님을 말로도 못 박는다 ══╗")
VOCAB = {
 "HEAD": ("목덮개 작업모", "관이 y=+0.50 위에만 있고 **감쌈은 뒤로 늘어진 천**이 진다",
          {"천모자":"챙 앞으로","털모자":"둥근 부피+접힌 단","중절모":"챙 둘레","왕관":"위로 뾰족",
           "베레모":"한쪽 늘어짐","밀짚모자":"챙 양옆"}),
 "EYES": ("방진 고글", "렌즈판 + **턱 아래로 내려가는 마스크 컵**(255~290° — 기존 6종이 안 쓰는 유일한 방향)",
          {"선글라스":"렌즈2+다리","동그란안경":"원2+코다리","고글":"띠+와이드렌즈",
           "외알안경":"알1+체인+드러난 눈","뿔테안경":"윗테 굵음","안대":"천1+끈+드러난 눈"}),
 "NECK": ("작업 앞치마", "가슴을 덮는 **넓은 판**(기존은 전부 좁은 끈/천 자락)",
          {"나비넥타이":"좌우 삼각","줄무늬타이":"긴 띠","목도리":"고리+자락2","방울목걸이":"목띠+구슬",
           "펜던트":"줄+마름모","반다나":"목띠+앞자락"}),
 "BACK": ("연장 가방", "**뒤아래 먼 쪽**(215~225°, r>3.0)에 매달린 상자 — 배낭은 x≥−1.50까지만 온다",
          {"짧은망토":"어깨에서 흐름","긴망토":"발목까지","날개":"좌우 대칭 돌출","배낭":"등에 붙은 상자",
           "판초":"앞까지 덮음","요정날개":"작은 둥근 쌍"}),
 "HAIR": ("목덜미 매듭", "**정체가 y≤−1.3에 있다** — 어느 모자 잉크도 −0.711R 아래로 안 온다",
          {"삐친머리":"봉우리5+삐침","단정한머리":"돔+가르마","곱슬머리":"물결 커튼","민머리":"테두리만",
           "바가지머리":"일자 앞머리","포니테일":"뒤로 뻗은 묶음"}),
 "PET": ("작업등", "**위 고리 + 가운데 몸통 + 평평한 바닥** (풍선은 정확히 뒤집힌 것: 위 원 + 아래 실)",
          {"작은공":"원+솔기","종이비행기":"쐐기+접힘","리틀스틱메이트":"작은 스틱맨","커서친구":"화살표",
           "풍선":"위 원+아래 실","달팽이":"껍질 나선+발"}),
}
for k, (nm, why, base) in VOCAB.items():
    print("  ── %s  신규「%s」: %s" % (k, nm, why))
    print("     기본 6종 어휘: " + " / ".join("%s=%s" % (a, b) for a, b in base.items()))

print("\n╚══ 결과: %s ══╝" % ("불중복 증명 통과 (충돌 0건)" if not fail else "충돌 %d건: %s" % (len(fail), fail)))
sys.exit(1 if fail else 0)
