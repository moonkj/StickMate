# -*- coding: utf-8 -*-
"""R8 과제 1 — design-art §23-3 처방(그늘 배수 ×0.62 → ×0.35)의 **성립 조건** 실측.

처방이 듣는 통로는 프로덕션 코드상 정확히 두 개다:
  (가) CharacterAccessoryRenderer.AddShape :  if (shape.Filled) outline = FillOutlineColor(color);
      → **Filled == false 면 윤곽선이 자기 색 그대로 그려지고 배수는 아예 호출되지 않는다.**
  (나) *.ToneColor : if (tone == Shade) return FillOutlineColor(primary);
      → tone==2(그늘) 도형은 Filled 와 무관하게 배수를 탄다.

그래서 「13종의 보조색(tone==1) 조각이 filled 인가」를 두 출처에서 각각 읽는다.
  · NECK 6종  = **애셋**이 형상을 갖는다(AccessoryShapeBuilder.Append : case Neck → AppendWorn)
  · 나머지 36 = **AccessoryShapeBuilder.cs** 가 좌표를 만든다
독립 3번째 출처로 설계 거울(items.py / hair.py)과 대조한다.

★ 판정 전에 교정(양성 대조)을 먼저 통과시킨다. 깨지면 sys.exit — 이 저장소의 사고 형태
  (죽은 프로브의 출력이 성공한 프로브와 똑같이 생겼다)를 막기 위해서다.
"""
import os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ITEMS_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "Items")
BUILDER = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Interaction", "AccessoryShapeBuilder.cs")

# ---------------------------------------------------------------- 애셋 파서
def parse_worn_shapes(text):
    """AccessoryDefSO.wornShapes 배열 → [(name, tone, filled)]  (filled/tone 은 없으면 None)"""
    m = re.search(r"^  wornShapes:\s*$", text, re.M)
    if not m:
        return None                      # 키 자체가 없다 = 코드가 형상을 만든다
    body = text[m.end():]
    nxt = re.search(r"^  [A-Za-z_]", body, re.M)
    if nxt:
        body = body[:nxt.start()]
    out = []
    for blk in re.split(r"^  - name: ", body, flags=re.M)[1:]:
        name = blk.split("\n", 1)[0].strip()
        f = re.search(r"^    filled: (\d+)\s*$", blk, re.M)
        t = re.search(r"^    tone: (-?\d+)\s*$", blk, re.M)
        out.append((name,
                    int(t.group(1)) if t else None,
                    (int(f.group(1)) != 0) if f else None))
    return out

# ---------------------------------------------------------------- C# 파서
CASE_NAME = {
    "HeadCap": ("HEAD", 0, "야구모자"), "HeadBeanie": ("HEAD", 1, "털모자"),
    "HeadFedora": ("HEAD", 2, "중절모"), "HeadCrown": ("HEAD", 3, "왕관"),
    "HeadBeret": ("HEAD", 4, "베레모"), "HeadStraw": ("HEAD", 5, "밀짚모자"),
    "EyesSunglasses": ("EYES", 0, "선글라스"), "EyesRound": ("EYES", 1, "동그란안경"),
    "EyesGoggles": ("EYES", 2, "고글"), "EyesMonocle": ("EYES", 3, "외알안경"),
    "EyesBrowline": ("EYES", 4, "뿔테안경"), "EyesPatch": ("EYES", 5, "안대"),
    "BackCape": ("BACK", 0, "짧은망토"), "BackLongCape": ("BACK", 1, "긴망토"),
    "BackWings": ("BACK", 2, "날개"), "BackBackpack": ("BACK", 3, "배낭"),
    "BackPoncho": ("BACK", 4, "판초"), "BackFairyWings": ("BACK", 5, "요정날개"),
    "HairCowlick": ("HAIR", 0, "삐침머리"), "HairNeat": ("HAIR", 1, "단정한머리"),
    "HairCurly": ("HAIR", 2, "곱슬머리"), "HairBald": ("HAIR", 3, "민머리"),
    "HairBowl": ("HAIR", 4, "바가지머리"), "HairPonytail": ("HAIR", 5, "묶은머리"),
}

def strip_comments(src):
    """// 주석과 /* */ 주석을 지운다. 문자열 리터럴 안의 // 는 이 파일에 없다(도형 이름뿐)."""
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return re.sub(r"//[^\n]*", "", src)

def new_shape_calls(src):
    """`new Shape(` 부터 괄호 균형이 맞는 지점까지를 통째로 돌려준다."""
    for m in re.finditer(r"new Shape\(", src):
        i, depth = m.end(), 1
        while i < len(src) and depth:
            if src[i] == "(": depth += 1
            elif src[i] == ")": depth -= 1
            i += 1
        yield m.start(), src[m.end():i - 1]

TONE_MAP = {"Accent": 1, "Shade": 2}

def shape_meta(call):
    name = re.match(r'\s*"([^"]*)"', call)
    tone = re.search(r"\btone:\s*([A-Za-z0-9_]+)", call)
    fill = re.search(r"\bfilled:\s*(true|false)", call)
    t = 0
    if tone:
        g = tone.group(1)
        t = TONE_MAP.get(g, int(g) if g.isdigit() else -99)
    return (name.group(1) if name else "?", t, (fill.group(1) == "true") if fill else False, fill is not None)

def parse_builder(src):
    """{(slot,index): [(name,tone,filled,explicit)]}  — case 라벨로 구간을 가른다."""
    src = strip_comments(src)
    labels = [(m.start(), m.group(1)) for m in re.finditer(r"\bcase\s+([A-Za-z][A-Za-z0-9_]*)\s*:", src)
              if m.group(1) in CASE_NAME]
    calls = list(new_shape_calls(src))
    out = {}
    for k, (pos, lab) in enumerate(labels):
        end = labels[k + 1][0] if k + 1 < len(labels) else len(src)
        out[CASE_NAME[lab]] = [shape_meta(c) for p, c in calls if pos < p < end]
    return out

# ---------------------------------------------------------------- 교정 (양성 대조)
def calibrate():
    print("╔══ 교정 — 프로브가 살아 있는가 (깨지면 뒤 숫자를 전부 폐기한다) ══╗")
    ok = True

    # A. 애셋 파서 : 두 값이 다 나와야 한다
    synth = ("  wornShapes:\n"
             "  - name: Yes\n    loop: 1\n    filled: 1\n    tone: 1\n"
             "  - name: No\n    loop: 0\n    filled: 0\n    tone: 0\n"
             "  m_Next: 0\n")
    got = parse_worn_shapes(synth)
    a1 = got == [("Yes", 1, True), ("No", 0, False)]
    print("  [A1] 합성 YAML 두 값 판별      %-5s  %s" % (a1, got))
    a2 = parse_worn_shapes("  icon:\n  - kind: 4\n") is None
    print("  [A2] wornShapes 없음 -> None   %-5s" % a2)
    ok &= a1 and a2

    # B. C# 파서 : 명시 true / 생략(=false) / Shade 를 각각 잡아야 한다
    snip = ('sink.Add(new Shape("T", A(rig), true, SortHead, tone: Accent, filled: true));\n'
            'sink.Add(new Shape("F", new[] { a, b }, false, SortHead, tone: Accent));\n'
            'sink.Add(new Shape("S", C(rig), false, SortHead, tone: Shade));\n'
            'sink.Add(new Shape("P", D(rig), true, SortHead, filled: true));\n')
    got = [shape_meta(c) for _, c in new_shape_calls(snip)]
    b1 = got == [("T", 1, True, True), ("F", 1, False, False), ("S", 2, False, False), ("P", 0, True, True)]
    print("  [B1] 합성 C# 4형태 판별        %-5s  %s" % (b1, got))
    b2 = strip_comments('a; // filled: true\nb;').find("filled") < 0
    print("  [B2] 주석 안 filled 는 무시     %-5s" % b2)
    ok &= b1 and b2
    if not ok:
        sys.exit("★ 교정 실패 — 이 스크립트의 모든 판정을 폐기한다")
    print("  교정 OK\n")

# ---------------------------------------------------------------- 본체
FAIL13 = [  # design-art §23 / r5_mono.out.txt 배율 0.60 기준 「자유 윤곽 < 1획」 13종
    ("BACK", 1, "긴망토", 0.00), ("BACK", 3, "배낭", 0.00), ("BACK", 0, "짧은망토", 0.00),
    ("BACK", 4, "판초", 0.00), ("HAIR", 1, "단정한머리", 0.00), ("HAIR", 4, "바가지머리", 0.00),
    ("HEAD", 5, "밀짚모자", 0.00), ("HEAD", 4, "베레모", 0.00), ("HEAD", 3, "왕관", 0.00),
    ("HEAD", 2, "중절모", 0.00), ("NECK", 1, "줄무늬타이", 0.00),
    ("EYES", 0, "선글라스", 0.43), ("EYES", 1, "동그란안경", 0.82),
]
ASSET_OF = {("NECK", 0): "equip_neck_bowtie", ("NECK", 1): "equip_neck_striped",
            ("NECK", 2): "equip_neck_scarf", ("NECK", 3): "equip_neck_bell",
            ("NECK", 4): "equip_neck_pendant", ("NECK", 5): "equip_neck_bandana"}

def main():
    calibrate()
    src = open(BUILDER, encoding="utf-8").read()
    built = parse_builder(src)

    # 전수 census — 값이 실제로 두 종류 다 나오는지 (죽은 프로브 방지)
    print("╔══ census — tone==1(보조색) 조각의 filled 분포 ══╗")
    code_rows, asset_rows = [], []
    for (slot, idx, kr), shapes in sorted(built.items(), key=lambda kv: (kv[0][0], kv[0][1])):
        for nm, t, f, ex in shapes:
            if t == 1:
                code_rows.append((slot, idx, kr, nm, f, ex))
    for (slot, idx), stem in sorted(ASSET_OF.items()):
        path = os.path.join(ITEMS_DIR, stem + ".asset")
        ws = parse_worn_shapes(open(path, encoding="utf-8").read())
        for nm, t, f in (ws or []):
            if t == 1:
                asset_rows.append((slot, idx, stem, nm, f))
    tv = sorted({r[4] for r in code_rows}); av = sorted({r[4] for r in asset_rows})
    print("  코드(.cs)  보조색 조각 %d개 · filled 값 종류 %s" % (len(code_rows), tv))
    print("  애셋       보조색 조각 %d개 · filled 값 종류 %s" % (len(asset_rows), av))
    # 애셋 전체(tone 무관)에서도 두 값이 다 나오는지 — 양성 대조
    allf = set()
    for stem in sorted(set(ASSET_OF.values())):
        for nm, t, f in (parse_worn_shapes(open(os.path.join(ITEMS_DIR, stem + ".asset"),
                                                encoding="utf-8").read()) or []):
            allf.add(f)
    print("  애셋 전체(tone 무관) filled 값 종류 %s" % sorted(allf))
    if len(tv) < 2:
        sys.exit("★ 코드 파서가 한 값만 낸다 — 양성 대조 실패, 판정 폐기")
    if len(allf) < 2:
        sys.exit("★ 애셋 파서가 한 값만 낸다 — 양성 대조 실패, 판정 폐기")
    print("  ⇒ 두 파서 모두 실제로 두 값을 낸다. 판정으로 넘어간다.\n")

    print("╔══ 판정 — 13종의 보조색 조각 ══╗")
    print("  %-4s %-2s %-10s %-8s %-16s %-6s %-8s %s" %
          ("슬롯", "#", "이름", "자유윤곽", "보조색 조각", "filled", "출처", "처방 ×0.35"))
    yes = no = 0
    for slot, idx, kr, free in FAIL13:
        if slot == "NECK":
            stem = ASSET_OF[(slot, idx)]
            ws = parse_worn_shapes(open(os.path.join(ITEMS_DIR, stem + ".asset"),
                                        encoding="utf-8").read())
            accs = [(nm, f, "애셋") for nm, t, f in ws if t == 1]
        else:
            accs = [(nm, f, "코드" + ("" if ex else "(생략=기본 false)"))
                    for nm, t, f, ex in built[(slot, idx, kr)] if t == 1]
        for nm, f, srcn in accs:
            eff = "듣는다" if f else "★ 안 듣는다"
            yes, no = (yes + 1, no) if f else (yes, no + 1)
            print("  %-4s %-2d %-10s %6.2f획  %-16s %-6s %-8s %s" %
                  (slot, idx, kr, free, nm, "true" if f else "false", srcn, eff))
    print("\n  filled=true %d건 (처방이 듣는다) · filled=false %d건 (★ 처방이 안 듣는다)" % (yes, no))

    # 대조군 — 자유 윤곽 ≥ 1획인 나머지 아이템의 보조색은 어떤가
    print("\n╔══ 대조군 — 자유 윤곽 ≥ 1획인 아이템의 보조색 조각 ══╗")
    fail_keys = {(s, i) for s, i, _, _ in FAIL13}
    for slot, idx, kr, nm, f, ex in code_rows:
        if (slot, idx) in fail_keys: continue
        print("  %-4s %-2d %-10s %-16s filled=%s" % (slot, idx, kr, nm, "true" if f else "false"))
    for slot, idx, stem, nm, f in asset_rows:
        if (slot, idx) in fail_keys: continue
        print("  %-4s %-2d %-10s %-16s filled=%s" % (slot, idx, stem.replace("equip_neck_", ""), nm,
                                                     "true" if f else "false"))

    # tone==2(그늘) — 처방의 두 번째 통로. Filled 와 무관하게 배수를 탄다
    print("\n╔══ 부수 영향 — tone==2(Shade) 조각은 filled 와 무관하게 ×0.35를 탄다 ══╗")
    n2 = 0
    for (slot, idx, kr), shapes in sorted(built.items(), key=lambda kv: (kv[0][0], kv[0][1])):
        for nm, t, f, ex in shapes:
            if t == 2:
                n2 += 1
                print("  %-4s %-2d %-10s %-16s filled=%s" % (slot, idx, kr, nm, "true" if f else "false"))
    print("  계 %d건 — design-art §23-3 이 안 적은 두 번째 소비처다(ToneColor)." % n2)

if __name__ == "__main__":
    main()
