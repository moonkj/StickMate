# -*- coding: utf-8 -*-
"""R13 — 망토 3종 **폴백 아이콘**(AccessoryDefSO.icon) 재굽기 좌표 산출.  §12-10

무엇을 재는가
--------------
카드 썸네일에는 경로가 둘이다:
  본경로  `AccessoryCardIcon.TryBuild`  — `AccessoryShapeBuilder` 도형을 그 자리에서 축소해 그린다.
  폴백    `CharacterInfoWindow.BuildIcon(entry.Icon)` — `AccessoryDefSO.icon` 의 40×40 viewBox 데이터.

망토 3종은 본경로가 성공하므로 **평소에는 폴백이 화면에 안 나온다.** 그래도 폴백 데이터가
옛 옷깃을 담고 있으면 (가) 본경로가 실패하는 순간 옛 그림이 나오고 (나) `ItemCatalogEntry`
생성자가 **이 데이터에서 주색/보조색을 뽑아** 본경로에 넘기므로 조각 구성이 색 유도에 걸린다.

변환식 (프로덕션 두 곳을 이어 붙인 것)
--------------------------------------
`AccessoryCardIcon.TryBuild`:  canvas = (p − center) × size × 0.86 / span
`CharacterInfoWindow.FromViewBox`: canvas = (X − 20) × size/40 ,  (20 − Y) × size/40
두 식을 같게 두면 **size 가 소거된다**:

    X = 20 + (px − cx) × 34.4 / span
    Y = 20 − (py − cy) × 34.4 / span          (34.4 = 40 × FitFraction 0.86)

⇒ 폴백을 이 좌표로 채우면 **폴백이 떠도 본경로와 같은 그림**이다. 지금은 그렇지 않다.

교정 (이 저장소 규칙: 계산기는 알려진 값으로 먼저 교정한다)
------------------------------------------------------------
출하 중인 `equip_shoulders_cape.asset` 의 **옛** 두 조각을 위 식으로 재현해 본다.
재현되면 식이 맞는 것이고, 안 되면 아래 신규 좌표를 **폐기**한다.

    python3 r13_fallback_icon.py
"""
import sys
import items
from rig import Shape

FIT = 0.86          # AccessoryCardIcon.FitFraction
VB = 40.0           # CharacterInfoWindow.BuildIcon 의 viewBox 변

CAPES = [("equip_shoulders_cape",      "equip.shoulders.cape",      "짧은망토", 0),
         ("equip_shoulders_long_cape", "equip.shoulders.long_cape", "긴망토",   1),
         ("equip_shoulders_poncho",    "equip.shoulders.poncho",    "판초",     4)]

# 출하 중인 에셋에 실제로 적혀 있는 값(손으로 옮긴 것이 아니라 파일에서 읽는다 — 아래 read_asset).
ASSET_DIR = "../../../Assets/_Project/Resources/Items"


def read_asset(stem):
    """`.asset` YAML 에서 icon 조각을 읽는다. PyYAML 없이 형식이 고정된 부분만 훑는다."""
    path = "%s/%s.asset" % (ASSET_DIR, stem)
    parts, cur = [], None
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
    in_icon = False
    for ln in lines:
        if ln.startswith("  icon:"):
            in_icon = True
            continue
        if not in_icon:
            continue
        if ln.startswith("  - kind:"):
            cur = {"kind": int(ln.split(":")[1]), "values": [], "tone": None, "color": None}
            parts.append(cur)
        elif cur is not None and ln.startswith("    - "):
            cur["values"].append(float(ln.strip()[2:]))
        elif cur is not None and ln.startswith("    tone:"):
            cur["tone"] = int(ln.split(":")[1])
        elif cur is not None and ln.startswith("    color:"):
            cur["color"] = ln.split("color:")[1].strip()
    return parts


def to_viewbox(shapes):
    """도형 목록 → 40×40 viewBox 점열. 반환은 [(name, tone, filled, [(X,Y)...])]."""
    xs = [p[0] for s in shapes for p in s.pts]
    ys = [p[1] for s in shapes for p in s.pts]
    cx, cy = (min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0
    span = max(max(xs) - min(xs), max(ys) - min(ys))
    k = VB * FIT / span
    out = []
    for s in shapes:
        pts = [(VB / 2 + (x - cx) * k, VB / 2 - (y - cy) * k) for x, y in s.pts]
        if s.loop:
            pts = pts + [pts[0]]          # 규약: 닫힌 도형은 마지막 점 = 첫 점
        out.append((s.name, s.tone, s.filled, pts))
    return out, span


def old_set(idx_name):
    """착지 **전** 도형 한 벌 — 윤곽 + 옛 옷깃 띠. 교정용."""
    base = [s for s in items.BACK[idx_name] if s.name != "CapeYoke"]
    return base + [Shape("CapeCollar", items.clasp(), filled=True, tone=1)]


def fmt(pts):
    return "  ".join("(%.2f, %.2f)" % p for p in pts)


def main():
    print("=" * 96)
    print("[0] 교정 — 출하 중인 에셋의 **옛** 조각을 내 변환식으로 재현할 수 있는가")
    print("    (재현 못 하면 아래 [2]의 신규 좌표를 전부 폐기한다)")
    print("=" * 96)
    worst = 0.0
    for stem, _id, kname, _i in CAPES:
        parts = read_asset(stem)
        shipped = []
        for p in parts:
            v = p["values"]
            shipped.append((p["tone"], [(v[i], v[i + 1]) for i in range(0, len(v), 2)]))
        # 옛 한 벌에서 윤곽·옷깃만 남기고 접힘 2개는 제외 — 에셋에도 접힘이 없다.
        old = [s for s in old_set(kname) if s.name in ("CapeOutline", "CapeCollar")]
        mine, span = to_viewbox(old)
        print("\n  %s   (span = %.4f R)" % (stem, span))
        for (name, tone, _f, pts), (stone, spts) in zip(mine, shipped):
            n = min(len(pts), len(spts))
            d = max(max(abs(pts[i][0] - spts[i][0]), abs(pts[i][1] - spts[i][1])) for i in range(n))
            worst = max(worst, d)
            print("    %-11s tone %d/%d  점 %d/%d  최대차 %.4f  %s"
                  % (name, tone, stone, len(pts), len(spts), d,
                     "✅" if (d <= 0.01 and len(pts) == len(spts) and tone == stone) else "✗"))
            if d > 0.01:
                print("       내값 %s" % fmt(pts))
                print("       에셋 %s" % fmt(spts))
    print("\n  교정 최대차 = %.4f  →  %s" % (worst, "식 유효" if worst <= 0.01 else "★ 식 무효. 아래 값 폐기"))
    if worst > 0.01:
        print("\n  ⇒ 교정이 깨졌다. 신규 좌표를 내지 않는다(이 저장소 규칙).")
        return 2

    print("\n" + "=" * 96)
    print("[1] 착지본이 폴백 형식에 담기는가 — 조각 구성 점검")
    print("=" * 96)
    print("    폴백 형식(ItemIconPartKind)에는 tone 이 0(주색)/1(보조색) 둘뿐이다.")
    print("    착지 한 벌은 CapeOutline(tone0 채움) · CapeFold/CapeFold2(tone2 = Shade 선) · CapeYoke(tone1 채움).")
    print("    ★ tone2(Shade)는 폴백이 표현할 수 없다 — ItemIconPart.Tinted() 가 0 이 아니면 전부 보조색으로 칠한다.")
    print("      그래서 접힘 2줄은 **폴백에 넣지 않는다**(넣으면 보조색 선 2개가 생겨 「보조색 1개」 규칙도 깨진다).")
    print("    ⇒ 폴백 조각 구성은 지금과 같은 **2개**: 윤곽(kind 4, tone 0) + 요크(kind 4, tone 1).")

    print("\n" + "=" * 96)
    print("[2] ★ 재굽기 좌표 — 40×40 viewBox, 원점 좌상단, y 아래로 (에셋에 그대로 적는 값)")
    print("=" * 96)
    for stem, iid, kname, _i in CAPES:
        landed = [s for s in items.BACK[kname] if s.name in ("CapeOutline", "CapeYoke")]
        assert len(landed) == 2, landed
        # ★ bbox 는 **그리는 한 벌 전체**(접힘 포함)로 잡아야 본경로와 같은 배율이 된다.
        mine, span = to_viewbox(items.BACK[kname])
        keep = [m for m in mine if m[0] in ("CapeOutline", "CapeYoke")]
        print("\n  %s   (%s)   span = %.4f R" % (stem, iid, span))
        for name, tone, filled, pts in keep:
            print("    - kind: 4        # Polygon(채움)   %s   tone %d" % (name, tone))
            print("      values: %s" % ", ".join("%.2f" % c for p in pts for c in p))
            xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
            print("      # 잉크 상자 %.2f × %.2f (viewBox 단위 40)" % (max(xs) - min(xs), max(ys) - min(ys)))

    print("\n" + "=" * 96)
    print("[3] 옛 값 대비 무엇이 바뀌는가")
    print("=" * 96)
    print("  %-28s %-9s %10s %10s" % ("에셋", "조각", "옛 잉크상자", "새 잉크상자"))
    for stem, _iid, kname, _i in CAPES:
        parts = read_asset(stem)
        v = parts[1]["values"]
        oxs = v[0::2]; oys = v[1::2]
        mine, _ = to_viewbox(items.BACK[kname])
        yk = [m for m in mine if m[0] == "CapeYoke"][0]
        nxs = [p[0] for p in yk[3]]; nys = [p[1] for p in yk[3]]
        print("  %-28s %-9s %5.2f×%-5.2f %5.2f×%-5.2f"
              % (stem, "보조색", max(oxs) - min(oxs), max(oys) - min(oys),
                 max(nxs) - min(nxs), max(nys) - min(nys)))
    print("\n  ※ 윤곽(tone 0)도 값이 바뀐다 — bbox 에 옛 옷깃(x −0.66, y +0.10)이 빠지고")
    print("    요크·접힘이 들어가 span 과 center 가 달라지기 때문이다. 두 조각을 **함께** 갈아야 한다.")

    print("\n" + "=" * 96)
    print("[4] ★ 붙여 넣을 YAML — `icon:` 블록 통째로 (색·tone·kind 는 현행 그대로 둔다)")
    print("=" * 96)
    for stem, _iid, kname, _i in CAPES:
        parts = read_asset(stem)          # 색은 손으로 옮기지 않고 **파일에서 읽어** 되쓴다
        mine, _ = to_viewbox(items.BACK[kname])
        keep = [m for m in mine if m[0] in ("CapeOutline", "CapeYoke")]
        print("\n# ---- Assets/_Project/Resources/Items/%s.asset ----" % stem)
        print("  icon:")
        for (name, tone, _f, pts), old in zip(keep, parts):
            print("  - kind: 4")
            print("    values:")
            for x, y in pts:
                print("    - %.2f" % x)
                print("    - %.2f" % y)
            print("    color: %s" % old["color"])
            print("    tone: %d" % tone)

    print("\n" + "=" * 96)
    print("[5] 회귀 영향 — 이 편집이 무엇을 건드리는가")
    print("=" * 96)
    print("  · ItemCatalogGolden.txt        재생성 필요(값이 그대로 박제돼 있다)")
    print("  · AccessoryFallbackBodyParity  대장 무변경 — 두 축(보조색 조각 수 1, 꼭짓점 수 4)이")
    print("                                 옛 옷깃과 새 요크에서 **똑같다**. 이 테스트는 이 드리프트를")
    print("                                 구조적으로 못 본다(그 파일 주석 L147-149가 예고한 사각지대다).")
    print("  · PrimaryColor/SecondaryColor  무변경 — 조각 수·tone·color 를 그대로 두므로 유도값이 같다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
