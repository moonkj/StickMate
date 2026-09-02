# -*- coding: utf-8 -*-
"""★ 불투명 불변식 게이트 — "장비는 전부 불투명" (사용자 확정, 2026-09-02)

    "모자는 투명하지 않게 장비들은 전부다 투명하면 안됨"

왜 게이트가 필요한가
--------------------
지금 이 불변식은 **우연히** 지켜지고 있다 — `AccessoryShapeBuilder.Shape`에 알파 필드가 없고
에셋 색이 전부 a=1이기 때문이다. 즉 **방어선이 "아직 아무도 안 만들었다"뿐**이다.
2026-09-02 핸드오프(reference/ItemIcon.dc.html)가 알파 0.08~0.80 채움과 세로 그라디언트를 들고 왔고,
그건 임의의 바탕화면 위에서 대비 1.1:1 미만이라 2026-08-30 신고("모자가 투명해보임")를 재현한다.
다음 핸드오프/DLC가 같은 것을 또 들고 오면 막을 것이 없다. 그래서 규칙을 **실행 가능한 검사**로 세운다.

무엇을 재는가 (전부 실제 산출물에서 — 문서를 읽지 않는다)
    O-1  에셋 색 알파       Resources/Items/*.asset 의 모든 color 가 a == 1
    O-2  몸 도형 정의       AccessoryShapeBuilder.Shape 필드에 알파/투명도가 없다
    O-3  카드 조각 정의     ItemIconPart / AccessoryIconPartData 필드에 알파가 없다
    O-4  페이드는 전역 1개  ApplyAlpha 가 _alpha 하나를 모든 색에 균일하게 쓴다(도형별 알파 소스 없음)
    O-5  데이터화 제안      AccessoryShapePartData(5-3 제안)의 필드 목록에 알파를 넣지 않는다
    O-6  팔레트 생성자      ItemCatalog.Rgb(...) 가 알파 1을 준다
    O-7  파생 색은 알파를 물려받는다   FillOutlineColor(그늘색)가 밝기만 낮추고 알파는 fill.a 그대로
    O-8  착용 색 알파 = 잉크 알파      WornColor 가 result.a = ink.a (아이템이 알파를 발명하지 않는다)

    ★ O-7 / O-8 은 본안을 짜고 나서 **내 게이트에 난 구멍을 스스로 찾아** 뒤에 붙인 것이다.
      O-1~O-6만 있으면 `FillOutlineColor`를 `fill.a * 0.62f`로 한 글자 고치는 것만으로 모든 채움
      윤곽선이 반투명해지는데 게이트는 초록을 낸다(색을 **발명**하지 않고 **곱하는** 경로라서).

    python3 opacity_gate.py            # 본안
    python3 opacity_gate.py --control  # 양성 대조
"""
import os, re, sys, glob

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ITEMS_DIR = os.path.join(ROOT, "Assets/_Project/Resources/Items")
BUILDER = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs")
RENDERER = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/CharacterAccessoryRenderer.cs")
CATALOG = os.path.join(ROOT, "Assets/_Project/Scripts/Core/ItemCatalog.cs")
DEFSO = os.path.join(ROOT, "Assets/_Project/Scripts/Core/AccessoryDefSO.cs")

#: ★ 알파를 뜻하는 이름들. 새 이름이 생기면 여기 더한다(검사가 이름을 모르면 통과시켜 버린다).
ALPHA_WORDS = ("alpha", "opacity", "fillopacity", "strokeopacity", "translucen", "transparen", "gradient")

#: ★ 5-3 데이터화 제안의 필드 목록 — **여기에 알파를 넣지 마라.**
#   리더 지시(2026-09-02): "넣는 순간 방어선이 사라진다."
#   불투명은 렌더러의 사고(事故)가 아니라 **형식이 표현할 수 없어서** 지켜져야 한다.
PROPOSED_SHAPE_PART_FIELDS = (
    "points",        # R 배수, 머리중심 원점
    "loop", "filled",
    "tone",          # 0 주색 / 1 보조색 / 2 그늘색  ← 색 *역할*이지 투명도가 아니다
    "sortRole",
    "swayStart", "swayCount",
    "hatCoverLocalY",
    "pointRef",
)


def read(p):
    return open(p, encoding="utf-8").read()


def _fields_of(src, decl):
    """`decl`(구조체/클래스 선언 줄의 일부)이 나오는 블록의 필드 이름 목록."""
    i = src.find(decl)
    if i < 0: return None
    j = src.find("{", i)
    depth, k = 0, j
    while k < len(src):
        if src[k] == "{": depth += 1
        elif src[k] == "}":
            depth -= 1
            if depth == 0: break
        k += 1
    body = src[j:k]
    return re.findall(r"public\s+readonly\s+[\w\[\]<>\.]+\s+(\w+)\s*;", body) + \
           re.findall(r"public\s+[\w\[\]<>\.]+\s+(\w+)\s*;", body)


# ---------------------------------------------------------------------------
def o1_asset_alpha(files=None):
    bad = []
    files = files or sorted(glob.glob(os.path.join(ITEMS_DIR, "*.asset")))
    n = 0
    for f in files:
        for m in re.finditer(r"\{r:\s*[\d.eE+-]+,\s*g:\s*[\d.eE+-]+,\s*b:\s*[\d.eE+-]+,\s*a:\s*([\d.eE+-]+)\}", read(f)):
            n += 1
            if abs(float(m.group(1)) - 1.0) > 1e-6:
                bad.append("%s a=%s" % (os.path.basename(f), m.group(1)))
    return bad, "색 %d개 검사" % n


def o2_shape_fields(src=None):
    src = src if src is not None else read(BUILDER)
    fs = _fields_of(src, "internal readonly struct Shape")
    if fs is None: return ["Shape 구조체를 못 찾았다(검사 무효)"], ""
    bad = [f for f in fs if any(w in f.lower() for w in ALPHA_WORDS)]
    return bad, "필드 %s" % ", ".join(fs)


def o3_iconpart_fields(src=None, src2=None):
    src = src if src is not None else read(CATALOG)
    src2 = src2 if src2 is not None else read(DEFSO)
    bad = []
    fs1 = _fields_of(src, "public readonly struct ItemIconPart") or []
    fs2 = _fields_of(src2, "public struct AccessoryIconPartData") or []
    for f in fs1 + fs2:
        if any(w in f.lower() for w in ALPHA_WORDS): bad.append(f)
    return bad, "ItemIconPart[%s] / AccessoryIconPartData[%s]" % (", ".join(fs1), ", ".join(fs2))


def o4_global_fade(src=None):
    """ApplyAlpha 본문에서 색의 a 에 대입되는 값이 `_alpha` 하나뿐인가."""
    src = src if src is not None else read(RENDERER)
    i = src.find("private void ApplyAlpha()")
    if i < 0: return ["ApplyAlpha를 못 찾았다(검사 무효)"], ""
    j = src.find("{", i); depth, k = 0, j
    while k < len(src):
        if src[k] == "{": depth += 1
        elif src[k] == "}":
            depth -= 1
            if depth == 0: break
        k += 1
    body = src[j:k]
    rhs = re.findall(r"\.a\s*=\s*([^;]+);", body)
    bad = [r.strip() for r in rhs if r.strip() != "_alpha"]
    return bad, "a 대입 %d곳, 우변 = %s" % (len(rhs), set(r.strip() for r in rhs) or "없음")


def o5_proposed_format(fields=None):
    fields = fields if fields is not None else PROPOSED_SHAPE_PART_FIELDS
    bad = [f for f in fields if any(w in f.lower() for w in ALPHA_WORDS)]
    return bad, "제안 필드 %d개" % len(fields)


def o6_palette_ctor(src=None):
    src = src if src is not None else read(CATALOG)
    m = re.search(r"private static Color Rgb\(int hex\)\s*=>\s*new Color\(([^;]+)\);", src)
    if not m: return ["ItemCatalog.Rgb를 못 찾았다(검사 무효)"], ""
    args = m.group(1)
    tail = args.rsplit(",", 1)[-1].strip()
    return ([] if tail in ("1f", "1.0f", "1") else ["알파 인자 = %s" % tail]), "Rgb(...) 마지막 인자 = %s" % tail


def o7_derived_alpha(src=None):
    """파생 색(그늘색)이 알파를 **곱하지 않고 물려받는가**."""
    src = src if src is not None else read(BUILDER)
    m = re.search(r"internal static Color FillOutlineColor\(Color fill\)\s*=>\s*new Color\(([^;]+)\);", src)
    if not m: return ["FillOutlineColor를 못 찾았다(검사 무효)"], ""
    tail = m.group(1).rsplit(",", 1)[-1].strip()
    return ([] if tail == "fill.a" else ["알파 인자 = %s" % tail]), "FillOutlineColor 알파 인자 = %s" % tail


def o8_worn_alpha(src=None):
    """착용 색이 아이템 알파가 아니라 **캐릭터 잉크 알파**를 따르는가."""
    src = src if src is not None else read(CATALOG)
    i = src.find("public static Color WornColor(")
    if i < 0: return ["WornColor를 못 찾았다(검사 무효)"], ""
    j = src.find("{", i); depth, k = 0, j
    while k < len(src):
        if src[k] == "{": depth += 1
        elif src[k] == "}":
            depth -= 1
            if depth == 0: break
        k += 1
    body = src[j:k]
    rhs = [r.strip() for r in re.findall(r"\.a\s*=\s*([^;]+);", body)]
    return ([] if rhs == ["ink.a"] else ["a 대입 = %s" % rhs]), "WornColor a 대입 = %s" % rhs


CHECKS = [
    ("O-1 에셋 색 알파 == 1", o1_asset_alpha),
    ("O-2 Shape 필드에 알파 없음", o2_shape_fields),
    ("O-3 카드 조각 필드에 알파 없음", o3_iconpart_fields),
    ("O-4 페이드는 전역 _alpha 하나", o4_global_fade),
    ("O-5 데이터화 제안에 알파 없음", o5_proposed_format),
    ("O-6 팔레트 생성자 알파 1", o6_palette_ctor),
    ("O-7 파생 색이 알파를 물려받음", o7_derived_alpha),
    ("O-8 착용 알파 = 잉크 알파", o8_worn_alpha),
]


def run(quiet=False):
    total = 0
    if not quiet: print("╔══ 불투명 불변식 게이트 (장비는 전부 불투명) ══╗")
    for name, fn in CHECKS:
        bad, note = fn()
        total += len(bad)
        if not quiet:
            print("  %s %-30s %s" % ("OK " if not bad else "✗  ", name, note))
            for b in bad: print("        · %s" % b)
    if not quiet: print("╚══ 위반 %d건 ══╝" % total)
    return total


if __name__ == "__main__":
    if "--control" in sys.argv:
        print("╔══ 양성 대조 — 나쁜 값을 넣으면 빨간불인가 ══╗")
        print("   본안 -> %d건 %s" % (run(quiet=True), "OK" if run(quiet=True) == 0 else "✗ 본안이 이미 빨갛다!"))
        cases = []
        # (a) 에셋 알파 0.34 (핸드오프 그라디언트 상단값 그대로)
        import tempfile, shutil
        d = tempfile.mkdtemp()
        f = os.path.join(d, "fake.asset")
        open(f, "w", encoding="utf-8").write("  color: {r: 0.5, g: 0.5, b: 0.5, a: 0.34}\n")
        cases.append(("에셋 색 알파 0.34", lambda: o1_asset_alpha([f])[0]))
        # (b) Shape 에 알파 필드 추가
        s2 = read(BUILDER).replace("public readonly bool Filled;",
                                   "public readonly float FillAlpha;\n            public readonly bool Filled;", 1)
        cases.append(("Shape 에 FillAlpha 필드", lambda: o2_shape_fields(s2)[0]))
        # (c) 카드 조각에 opacity 추가
        c3 = read(CATALOG).replace("public readonly byte Tone;",
                                   "public readonly float Opacity;\n        public readonly byte Tone;", 1)
        cases.append(("ItemIconPart 에 Opacity 필드", lambda: o3_iconpart_fields(c3)[0]))
        # (d) 도형별 알파 소스
        r4 = read(RENDERER).replace("c.a = _alpha;", "c.a = _alpha * shape.FillAlpha;", 1)
        cases.append(("ApplyAlpha 에 도형별 알파", lambda: o4_global_fade(r4)[0]))
        # (e) 제안 형식에 알파
        cases.append(("제안 형식에 fillAlpha", lambda: o5_proposed_format(
            PROPOSED_SHAPE_PART_FIELDS + ("fillAlpha",))[0]))
        # (f) 팔레트 생성자 알파 0.6
        c6 = read(CATALOG).replace("(hex & 0xFF) / 255f, 1f)", "(hex & 0xFF) / 255f, 0.6f)", 1)
        cases.append(("Rgb(...) 알파 0.6", lambda: o6_palette_ctor(c6)[0]))
        # (g) 그늘색이 알파까지 곱한다 (★ O-1~O-6만으로는 못 잡던 구멍)
        b7 = read(BUILDER).replace("fill.b * 0.62f, fill.a)", "fill.b * 0.62f, fill.a * 0.62f)", 1)
        cases.append(("FillOutlineColor 가 알파도 곱함", lambda: o7_derived_alpha(b7)[0]))
        # (h) 착용 색이 아이템 알파를 발명한다
        c8 = read(CATALOG).replace("result.a = ink.a;", "result.a = ink.a * 0.5f;", 1)
        cases.append(("WornColor 가 알파를 0.5배", lambda: o8_worn_alpha(c8)[0]))
        fails = 0
        for name, fn in cases:
            bad = fn()
            ok = len(bad) > 0
            if not ok: fails += 1
            print("   %s %-32s 빨간불 %d건" % ("OK " if ok else "✗  ", name, len(bad)))
        shutil.rmtree(d, ignore_errors=True)
        print("╚══ 대조 실패 %d건 ══╝" % fails)
        sys.exit(1 if fails else 0)
    sys.exit(1 if run() else 0)
