# -*- coding: utf-8 -*-
"""R2 ⑦ 「형태 위계」의 계량자 — 각 아이템이 **전신 실루엣**을 얼마나 바꾸는가.
등급 축이 없으므로(단일 등급 희귀) 위계는 크기가 아니라 **축**으로 만든다는 주장의 근거."""
import math, sys
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/character/verify")
import rig, items, headroom, r2_body as B, r2_pack as P, r2_worn as Wn
from rig import Shape
W=P.W75
BODY = Wn.HEAD_DISC + Wn.TORSO + Wn.ARMS + Wn.LEGS
ANCH = (0 + rig.HIP_R)/2.0     # 머리 중심과 엉덩이의 중간 — 전신 프로파일의 기준점
base = rig.profile(BODY, ANCH)
def delta(sh): return rig.max_delta(base, rig.profile(BODY+list(sh), ANCH))
def bins_changed(sh, thr):
    p=rig.profile(BODY+list(sh), ANCH)
    return sum(1 for a,b in zip(base,p) if b-a > thr)
print("== ⑦ 전신 실루엣 변화량 (72구간 x 5°, 기준 = 알몸 스틱메이트) ==")
print(f"{'아이템':16s}{'최대 반경 증가':>14s}{'획 배수':>9s}{'바뀐 구간(>1획)':>16s}")
rows=[]
for slot,(dn,sh) in P.PACK.items():
    d=delta(sh); n=bins_changed(sh, W)
    rows.append((dn,d,n)); print(f"{slot+' '+dn:16s}{d:14.4f}{d/W:9.2f}{n:16d} / 72")
print("\n  [대조] 출하 42종 중 각 슬롯 최대치:")
for lbl,tab in (("HEAD",items.HEAD),("EYES",items.EYES),("NECK",items.NECK),("BACK",items.BACK)):
    best=max(((delta(s),n) for n,s in tab.items()))
    print(f"    {lbl}: 최대 {best[0]/W:.2f}획 ({best[1]})")
print("\n★ 팩 안에서 「새 축」을 만드는 것은 우산 하나뿐이고, 나머지 셋은 기존 축 안의 변형이다.")
print("  그래서 간판은 생기지만 **약한 아이템이 생기지 않는다** — 셋 다 슬롯 내 실루엣 차 문턱을 넘는다.")
