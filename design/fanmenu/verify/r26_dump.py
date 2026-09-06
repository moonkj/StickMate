"""R26 — 제안 좌표 전문 + 두 크기/DPI 표. 이 텍스트가 coder 인계 정본이다."""
import math
from fanglyph import *
from r26_new import *
from r26_new import _arc, HORN, HANDLE, WAVE_R, WAVE_SPAN, BOX, NEW_POWER_GAP_DEG, NEW_POWER_RING_D

GRADE = {round(G1, 3): "g1 ×0.75", round(G0, 3): "g0 ×1.00", round(G2, 3): "g2 ×1.50"}
NAMEMAP = {
    "① 집중(스톱워치)": "GearMenuButton.FocusMode → BuildStopwatchSymbol",
    "② 캐릭터(스틱맨)": "GearMenuButton.Character → BuildStickmanSymbol",
    "③ 할일(체크리스트)": "GearMenuButton.Todo → BuildChecklistSymbol",
    "④ 행동명령(확성기)": "GearMenuButton.Action → BuildMegaphoneSymbol",
    "⑤ 종료(전원)": "GearMenuButton.Quit → BuildPowerSymbol",
}

print("=" * 92)
print("R26 부채꼴 심볼 좌표 전문 (제안)   단위 pt · 원점 = Symbol 상자 중심 · y 위")
print(f"W = {W}pt(SymbolStroke) · 등급 g0 {G0} / g1 {G1} / g2 {G2}")
print("=" * 92)

for label, fn in NEW.items():
    print(f"\n■ {label}    [{NAMEMAP[label]}]")
    for p in fn():
        g = GRADE.get(round(p.t, 3), "채움")
        if p.kind == "poly":
            pts = " ".join(f"({x:+.3f},{y:+.3f})" for x, y in p.kw["points"])
            print(f"   {p.name:11s} polyline  획 {p.t:4.2f} [{g}]  점 {len(p.kw['points'])}")
            print(f"        {pts}")
        elif p.kind == "capsule":
            print(f"   {p.name:11s} capsule   획 {p.t:4.2f} [{g}]  길이 {p.kw['length']:.2f} "
                  f"각 {p.kw['angle']:+.1f}° 중심 ({p.kw['center'][0]:+.2f},{p.kw['center'][1]:+.2f})")
        elif p.kind == "ring":
            print(f"   {p.name:11s} ring      획 {p.t:4.2f} [{g}]  Ø{p.kw['diameter']:.1f} "
                  f"틈 {p.kw.get('gap_deg',0):.0f}° @위")
        elif p.kind == "disc":
            print(f"   {p.name:11s} disc(채움)              Ø{p.kw['diameter']:.1f} "
                  f"중심 ({p.kw['center'][0]:+.2f},{p.kw['center'][1]:+.2f})")

BUTTON_D = 44.0          # GearRadialMenuWidget.ButtonDiameterPoints
HOVER_D = 48.0           # 호버
SHRUNK_D = 36.0          # GearRadialMenuWidget.ShrunkDiameterPoints
K36 = SHRUNK_D / BUTTON_D
FEATHER = 0.5            # UiChrome.EdgeFeather (UiChrome.cs:847)
WORST_NOW = 0.43         # 현행 카탈로그 최악 간극(④ 나팔↔목)

print()
print("=" * 92)
print("두 크기 검산 — 이 앱에 실재하는 크기만 쓴다")
print("=" * 92)
print("  ★ 2026-09-06 정정 — 이 자리에 있던 전제가 <사실이 바뀌어> 폐기됐다.")
print("    옛 전제: «부채꼴 버튼은 Ø44(평상)·Ø48(호버) 두 개뿐이고, 레이아웃 사다리의 Ø36 은")
print("             히트/배치 계산에만 쓰인다 — BuildButton 이 원을 ButtonDiameterPoints(44) 로")
print("             굳혀 만들고 다시 그리지 않으므로 화면에 Ø36 버튼은 그려지지 않는다.»")
print("    R26 시점의 사실로는 맞았다. 그러나 2026-09-06 축소 폴백 배선 결함이 수정되면서")
print("    GearRadialMenuWidget.ApplyLayoutDiameterToViews() 가 <사다리가 정한 지름을 화면에")
print("    옮기는 유일한 자리>로 들어왔다 — ButtonView.Group.localScale =")
print(f"    _diameterPoints / ButtonDiameterPoints 의 <균일 배율>이라")
print(f"    ★ Ø{SHRUNK_D:.0f} 버튼은 이제 실제로 그려진다(k = {SHRUNK_D:.0f}/{BUTTON_D:.0f} = {K36:.6f}).")
print("    (그 전까지는 <판정 지름 36 · 렌더 지름 44>였고, 그 어긋남 자체가 그날 고쳐진 결함이다.")
print("     가장 날카로웠던 것은 호버의 −2.00pt — 보이는 원이 눌리는 원보다 컸다.)")
print(f"    ※ 32pt 는 여전히 이 앱에 없는 크기다 — 실재하는 버튼 지름은")
print(f"      Ø{BUTTON_D:.0f}(평상) · Ø{HOVER_D:.0f}(호버) · Ø{SHRUNK_D:.0f}(축소 폴백) 셋이다.")
print("      Ø36 열은 <균일 배율>이므로 아래 표의 Ø44 열에 k 를 곱한 것과 같다.")
print()
print("  ※ 아래 두 열은 <코어 간극>이다 — 램프를 뺀 <알파 0 인 골>은 그 다음 절이다(둘은 다른 값이다).")
hdr = (f"  {'배율':>6s} {'버튼 Ø44':>9s} {'호버 Ø48':>9s} {'축소 Ø36':>9s} {'심볼필드Ø28':>10s} "
       f"{'W':>6s} {'g1':>6s} {'g2':>6s} {'간극3.0pt':>9s} {'Ø36 간극':>8s}")
print(hdr)
for s in (1.0, 1.25, 1.5, 1.75, 2.0):
    print(f"  {s:6.2f} {BUTTON_D*s:8.1f}px {HOVER_D*s:8.1f}px {SHRUNK_D*s:8.1f}px {28*s:9.1f}px "
          f"{G0*s:5.2f}px {G1*s:5.2f}px {G2*s:5.2f}px {3.0*s:8.2f}px {3.0*K36*s:7.2f}px")
print()
print("  램프(UiChrome.EdgeFeather 0.5pt/변)를 뺀 <알파 0 인 골>:")
print(f"    ★ 2026-09-06 정정 — 램프는 코어 <바깥>이 아니라 코어 가장자리를 <가운데 두고>")
print(f"      ±EdgeFeather/2 로 걸친다(alpha = clamp01((core − d)/feather + 0.5),")
print(f"      UiChrome.cs:948 캡슐 · :892 원). 즉 골 = g − {2*(FEATHER/2):.1f}pt 이지 g − {2*FEATHER:.1f}pt 가 아니다.")
print(f"      옛 식으로 낸 «3.0pt = 1×에서 2.00px · 0.43pt = −0.57px»는 폐기한다.")
for s in (1.0, 1.5, 2.0):
    print(f"    배율 {s:.2f} → 골 3.0pt 는 Ø44 {(3.0-FEATHER)*s:.2f}px / Ø36 {(3.0-FEATHER)*K36*s:.2f}px, "
          f"현행 최악 {WORST_NOW}pt 는 {(WORST_NOW-FEATHER)*s:+.2f}px (음수 = 골이 없다 = 두 획이 한 줄로 붙는다)")
print(f"    ⇒ 1×에서 골이 1px 남을 조건: Ø44 는 g ≥ {1.0+FEATHER:.1f}pt · "
      f"Ø36 은 g ≥ {1.0/K36+FEATHER:.3f}pt (일반형 (g − {FEATHER}pt)·k·S ≥ 1.0px)")
print()
print("-" * 92)
print("R27 재검증 — <Ø36 이 실제로 그려지게 된 뒤> FG-3 를 다시 걸었다 (design-art, 2026-09-06)")
print("  전문: design/art/r27_shrink_fg3.py → r27_shrink_fg3.out.txt")
print("-" * 92)
print(f"  결론: 위 좌표에 <결함 0건>. 축소 폴백에서도 다섯 칸 전부 통과한다.")
print(f"   1) 조형(상대자) 1.5W  — <다른 덩어리에 속한 조각> 15쌍 전부 통과. 배율 불변이라 Ø36도 같은 판정.")
print(f"      ★ 다만 ①③의 최소 골은 <정확히 1.5W>(3.000pt)다 — 여유 0.000pt, 하한에 <붙여> 설계됐다.")
print(f"        (r26_gate.out.txt 의 3.02/3.03 은 SS=32 래스터 격자 편의 +1/32pt. 해석 해가 정본이다.)")
print(f"   2) 화소자 (g − {FEATHER}pt)·k·S ≥ 1.0px — 미달 0쌍. 최악 2.05px @Ø36·Win 100%(하한의 2.05배).")
print(f"   3) 렌더 시뮬(화소 격자 위 8-연결성) — 위상 8×8=64 × DPI 5종(Win 100/125/150/175 %, macOS 2×)")
print(f"      × <덩어리 쌍 9쌍> 전부에서 <병목 임계 θ* = 0.000>. 잉크 문턱 θ_ink = 0.3476 이므로 어느 위상에서도")
print(f"      두 덩어리가 이어지지 않는다 = <뭉쳐 보이지 않는다>. Ø44 대조군도 같은 0.000.")
print(f"   4) 뭉침 임계 k* — 가장 빠듯한 ① 스톱워치가 k* = 0.38(Ø16.7 상당).")
print(f"      Ø36(k = {K36:.3f})은 그보다 <2.15배> 크다 = 여유 115 %.")
print(f"   5) 진짜 하한은 안착값이 아니라 <펼침 첫 프레임>이다 — Group 배치 배율 × Root 애니메이션")
print(f"      배율(StartScale 0.620)이 곱해져 실효 0.5073(Ø22.3 상당). 거기서도 θ* = 0.000이나,")
print(f"      k* 대비 여유가 2.15배 → 1.33배로 좁아진다. 더 줄이는 변경은 이 지점을 먼저 봐야 한다.")
print(f"  주의: <절대 pt 자>(3.0pt를 그대로 고정)로 읽으면 Ø36에서 7쌍이 «미달»로 뜬다. 그것은 자의")
print(f"      문제다 — 3.0pt 는 W=2.0·k=1 일 때의 파생값이지 독립 기준이 아니다. 좌표를 벌려 그 숫자를")
print(f"      지키려면 ①에서 <긴 바늘/짧은 바늘>이 사라지거나(반경 차 1.00→0.33pt) 링을 키워 ①↔⑤")
print(f"      IoU 가 0.317→0.557 로 무너진다(R27 §7). 그래서 좌표는 그대로 두고 <규칙의 단위>를 고쳤다.")
print(f"  부수: g1 획(1.5pt)은 Ø36 @1× 최악 위상에서도 최고 알파 0.778 · 면색 대비 9.51:1 로 살아남고,")
print(f"      ③ 표식 상자 구멍은 Ø44 @1× 어두운 화소 9개 → Ø36 @1× 4개 → 펼침 전이 1개로 줄어든다.")
print(f"      버튼 테두리(1.2pt)는 Ø36 @1× 최악 위상 알파 0.478 이나 설계 대비가 1.35:1 뿐이라")
print(f"      원래 대비를 나르는 부품이 아니다 — 게이트가 아니라 관측이다.")

print()
print("=" * 92)
print("현행 → 제안 상수 대조 (coder 인계)")
print("=" * 92)
rows = [
    ("SymbolStroke", "2.0f", "2.0f (불변)", "GearRadialMenuWidget.cs:420"),
    ("SymbolBoxPoints", "24f", "24f (불변 — 레이아웃 사각형)", ":421 · :2308"),
    ("(신설) SymbolFieldRadius", "없음", "14f 권고 / 15f 상한", "FG-1"),
    ("(신설) 등급 g0/g1/g2", "없음", "1.00W / 0.75W / 1.50W", "FG-2"),
    ("PowerGapDegrees", "50f", "62f", ":2456"),
    ("PowerRingDiameterPoints", "20f", "22f  ★리더 판정", ":2462"),
    ("AddSmallBox", "쓰임 2", "삭제(꺾은선 사각형으로 대체)", ":2513"),
    ("Strike(취소선)", "1조각", "삭제 — 100% 가려져 있었다", ":2418"),
    ("SymbolFixedParts", "2조각", "1조각", ":2431"),
]
for a, b, c, d in rows:
    print(f"  {a:26s} {b:12s} → {c:32s} {d}")
