#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""FX/PET 12종의 **카드 아이콘**(64u) 조각 표를 굽는다.

정본
----
docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-12-3 (다)군 12종 · §14-12-5 #23/#26 (coder 인계 항목).
좌표 전문은 design/equipment/verify/r20_coords.txt 의 `-- CARD (64u 아이콘 단위 · 상자 전체)` 절
(그 파일을 굽는 모델은 design/equipment/verify/r21_model.py 의 `fxpet_cards()`).

왜 별도 생성기인가
------------------
Tools/CardShapeGen/gen_card_shapes.py 는 인계본 16종(r16/r19 모델)을 굽고 그 산출물
AccessoryShapeBuilder.Handoff.cs 는 골든(CardShapeGolden.txt)과 라인 단위로 대조된다.
(다)군 12종은 **골든에 없고 슬롯 박스도 없다**(§14-12-3: "64u 상자 전체"). 그 12종을 같은
생성기에 밀어 넣으면 골든/스탬프 계약이 통째로 흔들리므로, 표면(카드 전용)과 프레이밍
(전체 상자)이 다른 이 12종만 **자기 파일**로 굽는다.

★ 몸(월드)과의 관계
-------------------
FX/PET 의 **몸 도형은 여기서 한 점도 건드리지 않는다** — 그건 Interaction/AppearanceShapeBuilder.cs
소관이고 §14-12-5 #23 이 "카드만 갈라진다"고 못박았다. 다만 2026-09-06 R22 라운드가 월드에서
확정한 **색·위상 계약** 두 건은 카드가 따라가야 한다(안 따라가면 "카드에 없는 색이 착용하면
나타난다"는 이 저장소의 반복 결함이 된다). 그 둘만 아래 WORLD_ALIGN 이 기계적으로 고친다.
새 좌표를 만들지 않는다 — 있는 점을 나누거나 이미 있는 월드 상수를 대입할 뿐이다.

실행
----
    python3 Tools/CardShapeGen/gen_fxpet_card_shapes.py
검산(왕복: R 좌표 → 64u 아이콘 좌표가 전문과 같은가)을 표로 찍고 0 이 아니면 실패한다.
"""

import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
COORDS = os.path.join(ROOT, "design", "equipment", "verify", "r20_coords.txt")
OUT = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Interaction",
                   "AccessoryShapeBuilder.FxPetCard.cs")
GOLDEN = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Tests", "EditMode", "Golden",
                      "FxPetCardGolden.txt")

# ── 프레이밍 ────────────────────────────────────────────────────────────────
# §14-12-3 은 FX/PET 카드를 "64u 상자 전체"라고만 선언한다 — 인계본 슬롯 박스가 **없다**.
# 그래서 R 로 되돌릴 배율을 인계본이 직접 준 적이 없고, 지어내서도 안 된다. 같은 처지인
# 자리가 하나 더 있다: HAIR 도 슬롯 박스가 없고, §14-12-5 #22 가 그 자리를 위해 전체 상자
# 프레이밍을 **명시로** 선언했다 — "1 R = 16u · 머리 중심 (32, 30)". 그 선언을 그대로 쓴다.
UNITS_PER_R = 16.0
HEAD_CX, HEAD_CY = 32.0, 30.0
ICON_BOX = 64.0
ICON_STROKE = 2.2          # 인계본 아이콘 획(§14-12-5 #22 "카드 획 2.2u × 배수")

# ── 월드 정렬 2건(위 문단) ──────────────────────────────────────────────────
# (1) 반짝임 — Interaction/AppearanceShapeBuilder.SparkleConcaveRatio = 0.34.
#     그 상수 문서가 "0.34 는 카드 아이콘이 이미 쓰고 있는 값이라 카드와 착용 모습이 같은
#     그림이 된다"고 근거를 카드에 걸고 있다. (다)군 별의 오목비는 0.2448 이라 그대로 두면
#     그 문장이 거짓이 되고 두 별의 허리가 갈라진다. 바깥 정점은 손대지 않고 오목 4정점만
#     같은 각도 위에서 반지름만 0.34 배로 다시 놓는다.
SPARKLE_CONCAVE_RATIO = 0.34
# (2) 커서친구 — AppearanceShapeBuilder.CursorHead/CursorTail 이 2026-09-06 에 한 획을
#     머리(주색)+꼬리(보조색)로 쪼갰고, 쪼갠 자리를 "옛 배열의 2번·5번 점"이라고 못박았다.
#     (다)군 화살표는 **같은 7 점이 같은 순서**다(비율만 다르다). 같은 두 인덱스에서 나눈다.
CURSOR_SPLIT = ((0, 1, 2, 5, 6), (2, 3, 4, 5))

SLOT_OF = {"fx": 5, "pet": 6}        # Core/EquipmentModel.EquipmentSlot
# Interaction/AppearanceShapeBuilder 의 자리 상수와 같은 순서(카탈로그 표 순서).
ITEM_INDEX = {
    "look.fx.none": 0, "look.fx.footprint": 1, "look.fx.sparkle": 2, "look.fx.dust": 3,
    "look.fx.bubble": 4, "look.fx.leaf": 5,
    "look.pet.ball": 0, "look.pet.plane": 1, "look.pet.mini": 2, "look.pet.cursor": 3,
    "look.pet.balloon": 4, "look.pet.snail": 5,
}
CONST_OF = {
    "look.fx.none": "FxNone", "look.fx.footprint": "FxFootprint", "look.fx.sparkle": "FxSparkle",
    "look.fx.dust": "FxDust", "look.fx.bubble": "FxBubble", "look.fx.leaf": "FxLeaf",
    "look.pet.ball": "PetBall", "look.pet.plane": "PetPlane", "look.pet.mini": "PetMini",
    "look.pet.cursor": "PetCursor", "look.pet.balloon": "PetBalloon", "look.pet.snail": "PetSnail",
}
ORDER = ["look.fx.none", "look.fx.footprint", "look.fx.sparkle", "look.fx.dust",
         "look.fx.bubble", "look.fx.leaf",
         "look.pet.ball", "look.pet.plane", "look.pet.mini", "look.pet.cursor",
         "look.pet.balloon", "look.pet.snail"]

# Core/AccessoryShapeContract.AccessoryTone
TONE_PRIMARY, TONE_ACCENT, TONE_HIGHLIGHT, TONE_HEADINK = 0, 1, 3, 4

# §14-12-5 #27 의 채움 역할 INKF 는 우리 역할표에 대응이 없다 — AccessoryTone.HeadInk 는 채움일 때
# **카드 바탕**을 뜻한다고 계약(Core/AccessoryShapeContract.cs)이 정의하므로 그대로 쓰면 머리가
# 배경색으로 사라진다. 이 아이템의 재질색 M 자체가 잉크 톤이라 M 채움이 같은 그림을 낸다.
INKF_NOTE = "  ※ 전문의 채움 역할은 INKF(카드 잉크). 역할표에 그 칸이 없어 재질색 M(잉크 톤)으로 굽는다"

HEADER_RE = re.compile(r"^(look\.(fx|pet)\.\w+)\s+(\S+)\s+/\s+(FX|PET)\s")
PIECE_RE = re.compile(
    r"^\s{2}(\S+)\s+card\s+fill=(\S+)\s+line=(\S+)\s+\|.*?\|\s+(\S+)\s+"
    r"filled=(\d)\s+loop=(\d)\s+×([\d.]+)\s+선α\s+([\d.]+)\s+잉크\s+\S+\s*(.*)$")
PT_RE = re.compile(r"\(\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)")


def parse():
    items, cur, in_card = {}, None, False
    for raw in open(COORDS, encoding="utf-8").read().replace("\r\n", "\n").split("\n"):
        m = HEADER_RE.match(raw)
        if m:
            cur = m.group(1) if m.group(1) in ITEM_INDEX else None
            in_card = False
            if cur:
                items[cur] = {"ko": m.group(3), "slot": SLOT_OF[m.group(2)], "pieces": []}
            continue
        if cur is None:
            continue
        if raw.startswith("-- "):
            in_card = raw.startswith("-- CARD")
            continue
        if not in_card:
            continue
        m = PIECE_RE.match(raw)
        if m:
            items[cur]["pieces"].append(dict(
                name=m.group(1), fill=m.group(2), line=m.group(3), call=m.group(4),
                filled=m.group(5) == "1", loop=m.group(6) == "1",
                mult=float(m.group(7)), line_alpha=float(m.group(8)),
                role=m.group(9).strip(), pts=[]))
            continue
        if raw.startswith("      (") and items[cur]["pieces"]:
            items[cur]["pieces"][-1]["pts"] += [(float(a), float(b)) for a, b in PT_RE.findall(raw)]
    missing = [k for k in ORDER if k not in items or not items[k]["pieces"]]
    if missing:
        sys.exit("좌표 전문에서 못 찾은 아이템: %s" % missing)
    return items


def align_sparkle(pieces):
    """오목 4정점을 SPARKLE_CONCAVE_RATIO 로 다시 놓는다(바깥 정점·개수·순서 불변)."""
    for p in pieces:
        if not p["name"].startswith("sparkle.") or len(p["pts"]) != 8:
            continue
        xs = [q[0] for q in p["pts"]]
        ys = [q[1] for q in p["pts"]]
        cx, cy = (min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0
        r_out = max(math.hypot(q[0] - cx, q[1] - cy) for q in p["pts"])
        out = []
        for i, (x, y) in enumerate(p["pts"]):
            if i % 2 == 0:                       # 바깥 정점 — 그대로
                out.append((x, y))
                continue
            a = math.atan2(y - cy, x - cx)
            r = r_out * SPARKLE_CONCAVE_RATIO
            out.append((round(cx + math.cos(a) * r, 4), round(cy + math.sin(a) * r, 4)))
        p["pts"] = out
    return pieces


def align_cursor(pieces):
    """화살표 한 조각을 머리(주색)/꼬리(보조색)로 나눈다 — 월드와 같은 두 인덱스에서."""
    out = []
    for p in pieces:
        if p["name"] != "cursor.B0" or len(p["pts"]) != 7:
            out.append(p)
            continue
        head_i, tail_i = CURSOR_SPLIT
        head = dict(p); head["name"] = "cursor.B0head"
        head["pts"] = [p["pts"][i] for i in head_i]
        head["role"] = "화살표 머리(M Blue + 잉크) — 월드 CursorHead 와 같은 분할"
        tail = dict(p); tail["name"] = "cursor.B1tail"
        tail["pts"] = [p["pts"][i] for i in tail_i]
        tail["fill"] = "M2"
        tail["role"] = "화살표 꼬리(M2 — 월드 CursorTail 과 같은 보조색)"
        out += [head, tail]
    return out


def tone_of(p):
    if p["fill"] == "M":
        return TONE_PRIMARY
    if p["fill"] == "M2":
        return TONE_ACCENT
    if p["fill"] == "INKF":
        # §14-12-5 #27 의 「잉크 표식」. 우리 역할표(AccessoryTone.HeadInk)는 채움일 때
        # **카드 바탕**을 뜻하므로(계약 문서의 정의) 그대로 쓰면 머리가 배경색으로 사라진다.
        # 이 아이템의 재질색 M 자체가 잉크 톤(#D6DBE3)이라 M 채움이 같은 그림을 낸다.
        return TONE_PRIMARY
    if p["line"] == "M":
        return TONE_PRIMARY
    if p["line"] == "M2":
        return TONE_ACCENT
    if p["line"] == "W":
        return TONE_HIGHLIGHT
    return TONE_HEADINK          # 채움 없는 잉크 낱선 → HandoffLineBase 가 p.Ink 를 낸다


def contains(poly, pt):
    inside, n = False, len(poly)
    x, y = pt
    j = n - 1
    for i in range(n):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


def under_back(pieces, i):
    """얹히는 조각의 밑색 참조 — CardShapeContractTests 의 기하 되짚기와 **같은 규칙**
    (앞쪽 채운 조각 중 이 조각의 점을 90% 이상 품는 가장 가까운 것)."""
    best = 0
    for j in range(i):
        if not pieces[j]["filled"]:
            continue
        pts = pieces[i]["pts"]
        inside = sum(1 for q in pts if contains(pieces[j]["pts"], q))
        if inside / float(len(pts)) >= 0.9:
            best = i - j
    return best


def to_r(x, y):
    return ((x - HEAD_CX) / UNITS_PER_R, (HEAD_CY - y) / UNITS_PER_R)


def main():
    items = parse()
    rows, worst = [], 0.0
    body = []
    arrays = []
    golden = [
        "# StickMate (다)군 FX/PET 카드 아이콘 골든 — 좌표는 **64u 아이콘 단위 · y 아래**(설계 전문 그대로).",
        "# 정본 design/equipment/verify/r20_coords.txt (다)군 `-- CARD` 절 · 설계 docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-12-3.",
        "# 생성: python3 Tools/CardShapeGen/gen_fxpet_card_shapes.py — 손으로 고치지 마라.",
        "# 프로덕션은 같은 점을 **R 단위**로 들고 있다(AccessoryShapeBuilder.FxPetCard.cs). 이 파일과 대조하는",
        "# FxPetCardShapeTests 는 그 R 좌표를 AccessoryCardIcon.Frame.TryGetCardFrame 으로 되돌려 여기와 맞춘다 —",
        "# 즉 대조되는 것은 좌표만이 아니라 **카드 프레이밍 그 자체**다.",
        "# ★ 월드 정렬 2건(반짝임 오목비 · 커서친구 머리/꼬리 분할)은 이 골든에 이미 반영돼 있다.",
        "# PIECE\tslot\titem\tindex\tname\tloop\tfilled\ttone\tstrokeMult\tnoStroke\tlineAlpha\tunderBack\tpointCount\tx,y...",
        "# TOTAL\tpieces\titems",
    ]

    for key in ORDER:
        it = items[key]
        pieces = align_cursor(align_sparkle(it["pieces"]))
        ident = key.replace("look.", "").replace(".", "_")
        calls = []
        for i, p in enumerate(pieces):
            golden.append("PIECE\t%d\t%d\t%d\t%s\t%d\t%d\t%d\t%s\t%d\t%s\t%d\t%d\t%s" % (
                it["slot"], ITEM_INDEX[key], i, p["name"], 1 if p["loop"] else 0,
                1 if p["filled"] else 0, tone_of(p), fmt(p["mult"]),
                1 if p["line"] == "None" else 0, fmt(p["line_alpha"]), under_back(pieces, i),
                len(p["pts"]), "\t".join("%g,%g" % q for q in p["pts"])))
            arr = "FxPetCard_%s_%s" % (ident, p["name"].split(".")[1])
            xy = []
            for (x, y) in p["pts"]:
                rx, ry = to_r(x, y)
                xy += ["%.5ff" % rx, "%.5ff" % ry]
                # 왕복 검산: R → 아이콘 단위가 전문과 같은가(그리는 쪽이 하는 그 계산).
                bx = float("%.5f" % rx) * UNITS_PER_R + HEAD_CX
                by = HEAD_CY - float("%.5f" % ry) * UNITS_PER_R
                worst = max(worst, abs(bx - x), abs(by - y))
            arrays.append("        private static readonly float[] %s = {\n%s\n        };"
                          % (arr, wrap(xy)))
            calls.append(
                '                    FxPetCardPiece(sink, rig, "%s", %s, loop: %s, filled: %s, '
                'tone: %d, strokeMult: %sf, noStroke: %s, lineAlpha: %sf, underBack: %d);   // %s'
                % (p["name"], arr, "true" if p["loop"] else "false",
                   "true" if p["filled"] else "false", tone_of(p),
                   fmt(p["mult"]), "true" if p["line"] == "None" else "false",
                   fmt(p["line_alpha"]), under_back(pieces, i),
                   p["role"] + (INKF_NOTE if p["fill"] == "INKF" else "")))
            rows.append((key, p["name"], len(p["pts"]), tone_of(p), p["filled"],
                         p["mult"], p["line_alpha"], under_back(pieces, i)))
        body.append("                case AppearanceShapeBuilder.%s:   // %s — 조각 %d\n%s\n                    return true;"
                    % (CONST_OF[key], it["ko"], len(pieces), "\n".join(calls)))

    fx = [b for k, b in zip(ORDER, body) if k.startswith("look.fx.")]
    pet = [b for k, b in zip(ORDER, body) if k.startswith("look.pet.")]

    src = TEMPLATE % dict(
        fx="\n".join(fx), pet="\n".join(pet), arrays="\n\n".join(arrays),
        unitsPerR="%.1ff" % UNITS_PER_R, headCx="%.1ff" % HEAD_CX, headCy="%.1ff" % HEAD_CY,
        stroke="%.1ff" % ICON_STROKE)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(src)
    golden.append("TOTAL\t%d\t%d" % (len(rows), len(ORDER)))
    with open(GOLDEN, "w", encoding="utf-8") as f:
        f.write("\n".join(golden) + "\n")

    print("조각 %d개 / 아이템 %d종 → %s" % (len(rows), len(ORDER), os.path.relpath(OUT, ROOT)))
    print("골든 → %s" % os.path.relpath(GOLDEN, ROOT))
    print("왕복 최대 오차(64u 단위): %.6f" % worst)
    print("최다 점 조각: %d (AccessoryCardIcon._points 128 칸, 고리 닫기 +1)"
          % max(r[2] for r in rows))
    if worst > 1e-3:
        sys.exit("왕복 오차가 큽니다 — 프레이밍 유도가 틀렸습니다.")
    if max(r[2] for r in rows) + 1 > 128:
        sys.exit("점 수가 카드 버퍼(128)를 넘습니다.")


def fmt(v):
    s = ("%.2f" % v).rstrip("0").rstrip(".")
    return s if s else "0"


def wrap(vals, per=10):
    out = []
    for i in range(0, len(vals), per):
        out.append("            " + ", ".join(vals[i:i + per]) + ",")
    return "\n".join(out).rstrip(",")


TEMPLATE = '''// <auto-generated>
//   Tools/CardShapeGen/gen_fxpet_card_shapes.py 가
//   design/equipment/verify/r20_coords.txt 의 (다)군 12종 `-- CARD` 절에서 굽는다.
//   설계 정본: docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-12-3 · §14-12-5 #23/#26/#27.
//   ★ 손으로 고치지 마라 — 고칠 것은 좌표 전문이고, 생성기가 왕복 검산을 함께 찍는다.
//   ★ 좌표 단위: 머리 중심 원점 · R 배수 · y 위(전문은 64u 아이콘 단위 · y 아래).
//      되돌리는 프레이밍은 §14-12-5 #22 가 전체 상자 카드를 위해 선언한 1 R = %(unitsPerR)s u ·
//      머리 중심 (%(headCx)s, %(headCy)s) 이다(FX/PET 에는 인계본 슬롯 박스가 없다).
//   ★ 표면: **카드 전용**(AccessorySurface.Card). 몸(월드) FX/PET 도형은
//      Interaction/AppearanceShapeBuilder.cs 소관이고 이 파일은 거기에 한 점도 넣지 않는다.
// </auto-generated>
using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    internal static partial class AccessoryShapeBuilder
    {
        /// <summary>
        /// (다)군 12종의 <b>카드 아이콘</b> 조각. 사용자 신고 2026-09-06 <i>"펫들도 아직 업데이트가
        /// 안되어 있는데"</i> — 이펙트·펫 카드만 옛 40u 폴백(<c>AccessoryDefSO.icon</c>)으로 남아
        /// 나머지 30종과 <b>채움·잉크 윤곽·하이라이트·획 폭</b> 넷이 달랐다(§14-12-5 #23).
        ///
        /// <para>여기 있는 것은 <b>카드뿐</b>이다. 착용했을 때 화면에 뜨는 이펙트·펫 본체는 여전히
        /// <see cref="AppearanceShapeBuilder"/>가 만든다 — §14-12-5 #23 이 "몸 효과·펫은 현행
        /// <c>AppearanceShapeBuilder</c> 그대로, 카드만 갈라진다"고 못박았고, 그 근거는 리더 승인
        /// 규칙 39-P(입자의 정체는 한 알의 모양이 아니라 여럿이 만드는 무늬 —
        /// <see cref="AppearanceShapeBuilder"/> 문서)다.</para>
        ///
        /// <para>★ 그래도 <b>색과 위상</b>은 월드를 따른다. 2026-09-06 R22 가 월드에서 확정한 두 건
        /// (반짝임 오목비 <see cref="AppearanceShapeBuilder.SparkleConcaveRatio"/> · 커서친구
        /// 머리/꼬리 2색 분할)은 생성기가 기계적으로 카드에 옮긴다. 안 옮기면 "카드에 없는 색이
        /// 착용하면 나타난다"는 이 저장소의 반복 결함이 그 자리에서 다시 열린다.</para>
        /// </summary>
        private static bool AppendFxPetCard(List<Shape> sink, EquipmentSlot slot, int item,
            in Rig rig, AccessorySurface surface)
        {
            // 몸 표면에는 아무것도 넣지 않는다. 이 한 줄이 「FX/PET 은 몸 도형이 없다」는 사실을
            // 계속 참으로 유지한다 — 이 파일이 통째로 틀려도 캐릭터 위에는 아무 일도 일어나지 않는다.
            if (surface != AccessorySurface.Card) return false;

            switch (slot)
            {
                case EquipmentSlot.Fx: return AppendFxCard(sink, item, rig);
                case EquipmentSlot.Pet: return AppendPetCard(sink, item, rig);
                default: return false;
            }
        }

        private static bool AppendFxCard(List<Shape> sink, int item, in Rig rig)
        {
            switch (item)
            {
%(fx)s
                // ★ 위 여섯 자리가 카탈로그의 FX 전부다(0~5). 정상값은 여기 오지 않는다 —
                //   정상값을 default 로 흘리면 정상 사용자 전원에게 거짓 경보가 찍힌다(FX 0번 사고).
                default:
                    ShapeCoverageGuard.ReportMissingFxShape(item, "AccessoryShapeBuilder.FxPetCard(카드)");
                    return false;
            }
        }

        private static bool AppendPetCard(List<Shape> sink, int item, in Rig rig)
        {
            switch (item)
            {
%(pet)s
                default:
                    ShapeCoverageGuard.ReportMissingPetShape(item, "AccessoryShapeBuilder.FxPetCard(카드)");
                    return false;
            }
        }

        /// <summary>(다)군 카드 조각 하나. <see cref="HandoffPiece"/>와 같은 일을 하되
        /// <b>슬롯 박스도 몸 변형도 없다</b> — 좌표가 이미 전체 상자 프레이밍이라 그대로 놓는다.</summary>
        /// <param name="xyInR">머리 중심 원점 · R 배수 · y 위. 생성기가 64u 아이콘 좌표에서 되돌렸다.</param>
        private static void FxPetCardPiece(List<Shape> sink, in Rig rig, string name, float[] xyInR,
            bool loop, bool filled, byte tone, float strokeMult, bool noStroke, float lineAlpha, int underBack)
        {
            int n = xyInR.Length / 2;
            var pts = new Vector3[n];
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            for (int i = 0; i < n; i++)
            {
                pts[i] = rig.F(xyInR[i * 2] * r, hc + xyInR[i * 2 + 1] * r);
            }
            sink.Add(new Shape(name, pts, loop, SortHead, -1, 0, tone, filled,
                (byte)AccessorySurface.Card, strokeMult, FxPetCardStrokeInR * strokeMult,
                noStroke, filled ? 1f : 0f, lineAlpha, underBack, 0, true));
        }

        /// <summary>(다)군 카드 조각의 명목 획(R 배수) = 인계본 아이콘 획 %(stroke)s u ÷ 1 R 당 %(unitsPerR)s u.
        /// <para>이 값이 <b>화면 획을 정하지는 않는다</b> — 카드는 언제나
        /// <c>AccessoryCardIcon.Frame.StrokeFraction</c>(2.2/64) × 배수로 그린다. 여기 있는 이유는
        /// 두 가지다: (가) 0 보다 커야 <see cref="Shape.IsHandoff"/>가 참이 되어 계약 v2 색 문법
        /// (잉크 윤곽 + 재질 채움 + 흰 하이라이트)을 타고, (나) 그 값이 <b>임의의 표식이 아니라
        /// 같은 프레이밍에서 유도된 실제 폭</b>이어야 다음 사람이 숫자의 출처를 찾을 수 있다.
        /// 몸 표면에는 이 조각이 아예 가지 않으므로(카드 전용) 몸 획 계산에는 쓰이지 않는다.</para></summary>
        internal const float FxPetCardStrokeInR = %(stroke)s / %(unitsPerR)s;

%(arrays)s
    }
}
'''

if __name__ == "__main__":
    main()
