# -*- coding: utf-8 -*-
"""R26d — 관망 자세의 «상시 기울임»이 긴망토 밑단–발목선 여유에 미치는 영향.

왜 이걸 재는가: CharacterAccessoryRenderer.TickHemMotion 의 주석이
  「서 있으면(b = 0 · 피치 0) 밑단(−9.055 R)은 발목선 위(+0.285 R)라 아무것도 안 바뀐다」
라고 못박고 있는데, 2026-09-06 관망 자세는 **세션 25분 내내 피치가 0이 아니다**
(뒷짐 −2.5°±2.0 / 팔짱 −4.0°±2.0). 즉 그 주석의 전제가 이번 라운드에 깨졌다.
`needsFloor` 가 상시 참이 되므로 PressHemToFloor 가 매 프레임 돈다 — 그 자체는 무해하지만,
**밑단이 실제로 발목선에 닿는가**는 숫자로 확인해야 한다(닿으면 천이 상시로 눌려 퍼진다).

식은 AccessoryShapeBuilder.HemFloorLine 을 그대로 옮긴다(사본 아님 — 같은 유도).
"""
import math, os, sys, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r26_capestance as B

cfg = B.read(B.CONFIG)
BREATH_AMP = B.field_f(cfg, "idleBreathAmplitude")          # 0.012 월드유닛(배율 1.0)
HIP_ROOT_R = 0.9346944 / B.R                                # 발바닥 원점 기준 엉덩이 높이(R)
FOOT_R     = 0.0

def hem_clearance(cape_pts, lean_deg, breath_offset_R):
    """밑단 점들과 «컨테이너 안의 기운 발목선» 사이의 최소 여유(R). 음수면 눌린다."""
    alpha = math.radians(-lean_deg)                         # 컨테이너 회전(= Euler z = -lean)
    c = max(0.05, math.cos(alpha)); s = math.sin(alpha)
    intercept = HIP_ROOT_R + (-HIP_ROOT_R - breath_offset_R) / c
    slope = -s / c
    worst = 1e9; wx = None
    for (x, y) in cape_pts:
        yr = y - B.FOOT_R                                   # 머리중심 기준 → 발바닥 기준
        gap = yr - (intercept + slope * x)
        if gap < worst: worst, wx = gap, (x, yr)
    return worst, wx

def main():
    print("="*100)
    print("R26d — 관망 자세 상시 기울임 × 긴망토 밑단–발목선 여유   (design-equipment / 2026-09-06)")
    print("="*100)
    print("엉덩이 %.4f R(발바닥 기준) · 호흡 진폭 %.4f 유닛 = %.4f R · 기울임 부호 규약 + = 앞"
          % (HIP_ROOT_R, BREATH_AMP, BREATH_AMP / B.R))
    breath_R = BREATH_AMP / B.R                             # 배율 무관(둘 다 배율에 비례)
    cases = [
        ("피치 0 · 호흡 0 (주석이 전제한 상태)", 0.0, 0.0),
        ("P2 뒷짐 기준 −2.5°",                  -2.5, 0.0),
        ("P2 뒷짐 최대 −4.5° + 호흡 최저",       -4.5, -breath_R),
        ("P1 팔짱 기준 −4.0°",                  -4.0, 0.0),
        ("P1 팔짱 최대 −6.0° + 호흡 최저",       -6.0, -breath_R),
        ("G2 링 확인 +6.5° + 호흡 최저",         +6.5, -breath_R),
    ]
    for nm, cape in B.CAPES.items():
        print("\n■ %s (밑단 최저 y = %.4f R = 발바닥 위 %.4f R)"
              % (nm, min(p[1] for p in cape["back"]),
                 min(p[1] for p in cape["back"]) - B.FOOT_R))
        for label, lean, bo in cases:
            g, at = hem_clearance(cape["back"], lean, bo)
            for scale in (0.75, 1.00):
                pass
            print("   %-34s 여유 %+.4f R (%+.2f pt @0.75 · %+.2f pt @1.00) 최악점 x=%+.3f y=%+.3f  %s"
                  % (label, g, g*B.R_to_pt(0.75), g*B.R_to_pt(1.00), at[0], at[1],
                     "OK" if g > 0 else "★ 눌림"))

if __name__ == "__main__":
    main()
