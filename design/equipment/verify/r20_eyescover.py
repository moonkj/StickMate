# -*- coding: utf-8 -*-
"""R20 — EyesVisorOpacityTests 의 두 계측을 복제한다(파이썬, 우리 좌표 R 단위):
  (A) 여섯_종이_화면에서_서로_구분된다: 획 반폭(W/2) 격자(머리 중심 ±1.3 R)에서 「Filled 도형이 덮는 칸」 집합 → 종별 칸 수 > 0, 쌍별 |A△B|/|A∪B| ≥ 0.20
  (B) 한쪽만_가리는_물건만_반대쪽_눈을_보여준다: (가리개 채움 최소 x − 드러난 눈 최대 x) / w ≥ 1.5  (w 후보: W, W/2 둘 다 찍는다)
  (C) 가리개_채움이_눈_자리를_덮는다: 앞눈 (+0.341,+0.091) 덮음 · 뒤눈 (−0.341,+0.091) — 양안 가리개는 덮고 한쪽 가리개는 안 덮음
★ 이 복제는 그 파일을 대신하지 않는다(EditMode 실측은 coder). 변형 후보를 미리 거르는 자다."""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, items
import r16_model as M16
import r17_model as M17
import r19_model as M19
import r20_model as M20

W = rig.W                      # ShippingStrokeBudgetInHeadRadii 0.3439 R
CELL = W * 0.5; SPAN = 1.3; N = math.ceil(SPAN * 2 / CELL)
EYE_F, EYE_B = (rig.EYE_X, rig.EYE_Y), (-rig.EYE_X, rig.EYE_Y)

def cells(fills):
    s = set()
    for i in range(N):
        for j in range(N):
            p = (-SPAN + CELL * (i + 0.5), -SPAN + CELL * (j + 0.5))
            if any(rig.contains(poly, p) for poly in fills): s.add((i, j))
    return s

def variant(monocle_cx=0.46, plates=True):
    """6종의 Filled 다각형 목록(몸 표면). monocle_cx: 알 중심 x(= 눈 x 의 부호 반전). plates: 유리 렌즈에 불투명 판(=렌즈 다각형)."""
    V = {}
    V["sunglasses"] = [p.pts for p in M19.WORN["sunglasses"] if p.filled]
    V["goggles"] = [p.pts for p in M19.WORN["goggles"] if p.filled]
    rg = [p for p in M19.WORN["roundglasses"] if p.src in ("CB0", "CB1")]
    V["roundglasses"] = [p.pts for p in rg] if plates else []
    # 외알안경: R17c 기하를 dx 만큼 옮긴다(알·사슬·구슬 +dx, 눈 −dx)
    dx = monocle_cx - 0.46
    mono = []
    for p in M19.WORN["monocle"]:
        if p.src == "CB0" and plates: mono.append([(x + dx, y) for x, y in p.pts])
        elif p.src == "CB3": mono.append([(x + dx, y) for x, y in p.pts])
    eye = [(x - dx, y) for x, y in rig.poly(M17.EYE_X, M17.EYE_Y, M17.EYE_R, 12)]
    V["monocle"] = mono + [eye]; V["_monocle_eye"] = eye; V["_monocle_visor"] = mono
    V["browline"] = [s.pts for s in items.EYES["뿔테안경"] if s.filled]
    pt = M20.GA["patch"]
    V["patch"] = [p.pts for p in pt if p.filled]; V["_patch_eye"] = [p for p in pt if p.src == "EYE"][0].pts
    V["_patch_visor"] = [p.pts for p in pt if p.filled and p.src != "EYE"]
    return V

def run(label, V):
    print("== %s ==" % label)
    keys = ["sunglasses", "roundglasses", "goggles", "monocle", "browline", "patch"]
    C = {k: cells(V[k]) for k in keys}
    for k in keys: print("   %-13s 칸 %3d %s" % (k, len(C[k]), "" if C[k] else "★ 0 — 「채움이 격자를 하나도 덮지 않습니다」"))
    worst = 9
    for a in range(6):
        for b in range(a + 1, 6):
            A, B = C[keys[a]], C[keys[b]]
            d = len(A ^ B) / max(1, len(A | B)); worst = min(worst, d)
            flag = "" if d >= 0.20 else " ★ < 0.20"
            print("   %-13s vs %-13s |A△B|/|A∪B| = %.2f%s" % (keys[a], keys[b], d, flag))
    print("   최소 %.2f (문턱 0.20)" % worst)
    for k, ek, vk in (("monocle", "_monocle_eye", "_monocle_visor"), ("patch", "_patch_eye", "_patch_visor")):
        eye_right = max(x for x, y in V[ek]); visor_left = min(x for poly in V[vk] for x, y in poly) if V[vk] else float("inf")
        gap = visor_left - eye_right
        print("   %-8s 눈 오른끝 %+.3f · 가리개 채움 왼끝 %+.3f · 간격 %.3f R = %.2f W (w=W) / %.2f (w=W/2)  %s" % (
            k, eye_right, visor_left, gap, gap / W, gap / (W / 2), "통과(≥1.5W)" if gap / W >= 1.5 else "★ 미달(w=W 가정)"))
        front = any(rig.contains(poly, EYE_F) for poly in V[vk]); back = any(rig.contains(poly, EYE_B) for poly in V[vk])
        print("   %-8s 앞눈 덮음 %s · 뒤눈 덮음 %s (한쪽 가리개: 앞 True/뒤 False 여야)" % (k, front, back))
    for k in ("sunglasses", "roundglasses", "goggles", "browline"):
        front = any(rig.contains(poly, EYE_F) for poly in V[k]); back = any(rig.contains(poly, EYE_B) for poly in V[k])
        print("   %-13s 앞눈 %s 뒤눈 %s (양안: 둘 다 True)" % (k, front, back))

if __name__ == "__main__":
    print("격자: 셀 %.4f R (W/2) · 범위 ±%.1f R · %d×%d" % (CELL, SPAN, N, N))
    run("현행 L-4 (유리 렌즈 판 없음, 외알안경 +0.46)", variant(0.46, plates=False))
    run("판 도입 (동그란안경·외알안경 판 = 렌즈 다각형, 외알안경 +0.46)", variant(0.46, plates=True))
    for cx in (0.50, 0.54, 0.56, 0.58, 0.60):
        V = variant(cx, plates=True)
        eye_right = max(x for x, y in V["_monocle_eye"]); visor_left = min(x for poly in V["_monocle_visor"] for x, y in poly)
        lens_right = max(x for poly in V["_monocle_visor"][:1] for x, y in poly)
        print("   외알안경 알·눈 ±%.2f: 간격 %.3f R = %.2f W · 알 오른끝 %+.3f(+테 0.086 = %+.3f, 머리 현 @y=.185: r1.0 0.983 / 출하 1.128)" % (
            cx, visor_left - eye_right, (visor_left - eye_right) / W, lens_right, lens_right + 0.0865))
    run("판 도입 + 외알안경 알·눈 ±0.56 (변형 계산)", variant(0.56, plates=True))
    # ★ 최종 정본(r19_model 그대로, 변형 없음): GLASS 판이 Filled 이고 외알안경이 ±0.56 인 상태
    V = {k: [p.pts for p in M19.WORN[k] if p.filled] for k in ("sunglasses", "roundglasses", "goggles", "monocle")}
    V["_monocle_eye"] = [p for p in M19.WORN["monocle"] if p.src == "EYE"][0].pts
    V["_monocle_visor"] = [p.pts for p in M19.WORN["monocle"] if p.filled and p.src != "EYE"]
    V["browline"] = [s.pts for s in items.EYES["뿔테안경"] if s.filled]
    pt = M20.GA["patch"]; V["patch"] = [p.pts for p in pt if p.filled]; V["_patch_eye"] = [p for p in pt if p.src == "EYE"][0].pts
    V["_patch_visor"] = [p.pts for p in pt if p.filled and p.src != "EYE"]
    run("★ 최종 정본 r19_model.WORN (GLASS 판 · 외알안경 ±%.2f)" % M19.MONOCLE_CX_R20, V)
