# -*- coding: utf-8 -*-
"""활쏘기 박자 — 페이즈 기계를 그대로 따라 적분한다.
ArcheryState 페이즈 순서(소스 확인, 수정 없음):
  Approach -> Intro -> Draw -> Aim ->(Release)-> Recover -> Draw ... -> Recover -> Outro
  Outro 종료 조건: _timer >= archeryArrowFlightSeconds + archeryOutroSeconds  (Outro 진입 시 _timer=0)
"""
V=1.5; H=1.36; PT=1004.9/24.56
c=dict(intro=0.55, draw=0.42, aim=0.30, recover=0.34, outro=0.55, flight=0.62)

def total(approach, draw, aim1, aim23, gate12, gate3_tail, label, beat=None):
    t = approach + c['intro']
    t += draw + aim1 + gate12
    t += draw + aim23 + gate12
    t += draw + aim23 + gate3_tail
    print(f"  {label:<42} {t:6.3f} s")
    return t

print("[현행]")
now = total(H/V, c['draw'], c['aim'], c['aim'], c['recover'],
            c['recover'] + c['flight'] + c['outro'], "접근1.00H + 3발(recover 0.34) + outro")
print(f"    내역: 접근 {H/V:.3f} + intro {c['intro']} + 3x(draw {c['draw']} + aim {c['aim']}) "
      f"+ 3x recover {c['recover']} + (flight {c['flight']} + outro {c['outro']})")
print(f"    ★ 착탄은 release+{c['flight']:.2f}s. 다음 Draw는 release+{c['recover']:.2f}s에 시작"
      f" -> 착탄이 다음 Draw의 {c['flight']-c['recover']:.2f}s 지점에 떨어진다.")
print()
print("[제안 — 착탄 비트만 넣고 나머지 무변경]")
beat=0.26; gate=max(c['recover'], c['flight']+beat)
a1 = total(H/V, c['draw'], c['aim'], c['aim'], gate, c['flight']+c['outro'], "비트 0.26, 조준유지 무변경")
print(f"    gate = max(recover {c['recover']}, flight {c['flight']} + beat {beat}) = {gate:.2f}s")
print(f"    3발째 꼬리 = flight {c['flight']} + outro {c['outro']} = {c['flight']+c['outro']:.2f}"
      f"  (비트 {beat}는 outro {c['outro']} 안에 흡수 — {c['outro']} > {beat} 이므로 성립)")
print(f"    차이 = {a1-now:+.3f} s ({(a1-now)/now:+.1%})")
print()
print("[제안 — 위 + 2·3발째 조준유지 0.30 -> 0.22로 지불]")
a2 = total(H/V, c['draw'], c['aim'], 0.22, gate, c['flight']+c['outro'], "비트 0.26 + aim23 0.22")
print(f"    차이 = {a2-now:+.3f} s ({(a2-now)/now:+.1%})   지불액 2x{c['aim']-0.22:.2f} = {2*(c['aim']-0.22):.2f}s")
print()
print("[추가 지불 선택지 — 리더 결정용 메뉴]")
opts=[("비트 0.26 -> 0.20", 2*0.06, "반응이 얇아진다. 15fps에서 0.20s = 3.0프레젠트프레임(하한)"),
      ("2·3발째 draw 0.42 -> 0.36", 2*0.06, "당기는 동작은 1발째만 온전히 보여도 읽힌다"),
      ("후퇴 0.60H -> 0.45H", (0.60-0.45)*H/0.9, "뒷걸음 2.3보 -> 1.7보. 2보 미만은 '한 발 삐끗'으로 읽힐 위험"),
      ("intro 0.55 -> 0.45", 0.10, "과녁 팝인. ArcheryRenderer가 같은 값을 읽으므로 동시 수정 필요")]
run=a2
for name,save,note in opts:
    run-=save
    print(f"    {name:<28} -{save:.3f}s -> 누적 {run:.3f}s (현행 대비 {run-now:+.3f}s)   {note}")
print()
print(f"[정직한 결론] 접근을 지울 수 없다면(사용자 확정 순서) 착탄 비트 3회의 실비는")
print(f"   최소 {a2-now:+.3f}s ({(a2-now)/now:+.1%})다. '공짜'가 아니다.")
print(f"   접근을 지우면 {a2-H/V-now:+.3f}s가 되지만 그건 사용자에게 물어야 하는 변경이다.")
