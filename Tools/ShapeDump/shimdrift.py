# -*- coding: utf-8 -*-
"""shim 이 프로덕션과 어긋났는지 검사한다 — **상수뿐 아니라 타입까지**.

왜 있는가 (2026-09-02, 두 번 데였다)
------------------------------------
① 상수: StickConfig 에 MinFillOutlineScreenPoints 가 생겼는데 shim 에는 안 생겨 하니스가
   컴파일 단계에서 죽었다. 즉 **설계 좌표와 프로덕션이 일치하는지 보는 유일한 장치가 꺼져 있었다.**
② ★ 타입: NECK 6종의 좌표가 코드에서 에셋으로 내려가면서 Core 에 타입 5종이 새로 생겼고
   (AccessoryWornFrame / AccessoryWornShapeData / AccessoryWornShapeReader / ShapeCoverageGuard /
   ItemCatalog), shim 에는 없어서 또 죽었다. **이 파일은 상수만 봤기 때문에 ②를 구조적으로 못 봤다.**

같은 사고의 **다음 형태**는 더 조용하다
---------------------------------------
②는 그래도 컴파일 에러로 시끄러웠다. 진짜 위험한 것은 **컴파일은 되고 값만 틀리는** 형태다:

  · shim 이 흉내낸 **enum 에 값이 하나 늘어나지 않는다** — 8번째 슬롯이 생겨도 컴파일은 되고
    덤프만 그 슬롯을 조용히 빠뜨린다. 빨간불이 아니라 **침묵**이다.
  · shim 이 프로덕션의 **표(switch)를 베껴 두고** 프로덕션만 바뀐다.
  · shim 이 **프로덕션에 없는 타입**을 계속 들고 있다(유령) — 하니스가 실재하지 않는 것을 잰다.
  · 양성 대조 폴더가 shim 을 **복제**해 갖고 한쪽만 바뀐다(2026-09-02 이전의 ShapeDumpPC).

그래서 검사를 다섯으로 늘렸다. 규칙은 하나다:
**shim 이 흉내낸 StickMate 타입은 전부 아래 표에 이유와 함께 등록되고, 프로덕션과 대조된다.**
새로 흉내내고 싶으면 여기에 한 줄을 적어야 한다 — 그 한 줄이 사람의 판단을 강제한다.
"""
import pathlib, re, sys

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
SRC = REPO / "Assets/_Project/Scripts"
SHIM_FILES = [HERE / "CoreShim.cs"]          # StickMate.* 를 흉내내는 파일은 여기 하나뿐이어야 한다
PC = REPO / "Tools/ShapeDumpPC"

# 흉내낸 타입 -> (검사 방식, 프로덕션 파일, 왜 프로덕션 파일을 안 쓰고 흉내내는가)
#   consts : shim 이 선언한 const 가 프로덕션에 같은 값으로 있는가
#   enum   : 이름·값이 **완전히** 같은가 (빠진 값은 컴파일도 되고 조용히 틀린다)
#   table  : shim 이 베낀 switch 표가 프로덕션과 같은가
#   stub   : 값을 흉내내지 않는다(상수/표 없음). 실재 여부만 본다
SHIMMED = {
    "EquipmentSlot": ("enum", "Core/EquipmentModel.cs",
                      "EquipmentModel.cs 전체를 컴파일하면 StickmanEventBus/CharacterProgression 까지 끌려온다"),
    "EquipmentModel": ("table", "Core/EquipmentModel.cs",
                       "위와 같은 이유. 카테고리 단위 사실만 필요하다"),
    "StickConfig": ("consts", "Core/StickConfig.cs",
                    "ScriptableObject 필드 200여 개 중 상수 4개만 쓴다"),
    "EquipmentDebugUnlock": ("stub", "Core/EquipmentDebugUnlock.cs",
                             "빌드 구성으로 갈리는 QA 스위치. 오프라인은 릴리스와 같은 '닫힘'으로 둔다"),
    "CharacterProgressionModel": ("stub", "Core/CharacterProgressionModel.cs",
                                  "레벨은 보유 판정에만 쓰이고 좌표/등급에는 안 들어간다"),
}

# 양성 대조 폴더가 <b>복제</b>해서는 안 되는 파일. 같은 이름의 얇은 래퍼는 허용한다 —
# 판정 기준은 이름이 아니라 <b>겹치는 알맹이 줄 수</b>다(래퍼는 겹치지 않는다).
NO_CLONE = ["CoreShim.cs", "Shim.cs", "Dump.cs", "shimdrift.py", "prodverify.py", "build.sh"]
CLONE_LINE_LIMIT = 4        # 이만큼 겹치면 래퍼가 아니라 복제본이다

CONST = re.compile(r"public\s+const\s+(?:float|int)\s+(\w+)\s*=\s*([^;]+);")
DECL = re.compile(r"\b(?:public|internal|private|sealed|static|abstract|partial|\s)*"
                  r"\b(enum|class|struct|interface)\s+(\w+)")
CASE = re.compile(r"case\s+EquipmentSlot\.(\w+)\s*:\s*return\s+\"([^\"]*)\"\s*;")


def strip_code(text: str) -> str:
    """주석과 문자열 리터럴을 지운다 — 중괄호 짝맞추기가 문자열 안의 `{}`에 속지 않게."""
    out, i, n = [], 0, len(text)
    while i < n:
        c = text[i]
        if c == '/' and i + 1 < n and text[i + 1] == '/':
            while i < n and text[i] != '\n':
                i += 1
        elif c == '/' and i + 1 < n and text[i + 1] == '*':
            i += 2
            while i + 1 < n and not (text[i] == '*' and text[i + 1] == '/'):
                i += 1
            i += 2
        elif c == '"':
            out.append('"')
            i += 1
            while i < n and text[i] != '"':
                i += 2 if text[i] == '\\' else 1
            out.append('"')
            i += 1
        else:
            out.append(c)
            i += 1
    return "".join(out)


def body_of(text: str, kind: str, name: str):
    """`kind name` 선언의 중괄호 본문. 문자열은 지워진 상태로 돌려준다(표 대조는 원문에서 따로 한다)."""
    m = re.search(r"\b%s\s+%s\b" % (kind, name), text)
    if not m:
        return None
    start = text.find("{", m.end())
    if start < 0:
        return None
    depth, i = 0, start
    while i < len(text):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[start + 1:i]
        i += 1
    return None


def declared_types(text: str):
    return {(k, n) for k, n in DECL.findall(text)}


def enum_members(body: str):
    """이름 -> 값. 값을 안 적은 항목은 직전 값 + 1(C# 규칙)."""
    out, nxt = {}, 0
    for part in body.split(","):
        part = part.strip()
        if not part:
            continue
        m = re.match(r"^(\w+)\s*(?:=\s*(-?\d+))?$", part)
        if not m:
            return None
        v = int(m.group(2)) if m.group(2) is not None else nxt
        out[m.group(1)] = v
        nxt = v + 1
    return out


def consts(body: str):
    out = {}
    for name, expr in CONST.findall(body):
        e = re.sub(r"(?<=[\d.])[fF]\b", "", expr).strip()
        if not re.fullmatch(r"[-+*/(). \d]+", e):
            continue                      # 다른 상수를 참조하는 식은 건너뛴다
        try:
            out[name] = float(eval(e, {"__builtins__": {}}))
        except Exception:
            pass
    return out


def main() -> int:
    bad = []
    checked = {"타입": 0, "enum 값": 0, "상수": 0, "표 항목": 0, "복제본": 0}

    shim_src = ""
    for f in SHIM_FILES:
        if not f.exists():
            print("!! shim 파일 없음: %s" % f)
            return 1
        shim_src += f.read_text(encoding="utf-8") + "\n"
    shim_clean = strip_code(shim_src)

    # ── (1) shim 이 흉내낸 타입 집합 == 등록표 ───────────────────────────────
    declared = {n for _, n in declared_types(shim_clean)}
    unregistered = sorted(declared - set(SHIMMED))
    phantom_rows = sorted(set(SHIMMED) - declared)
    for n in unregistered:
        print("  !! %-28s shim 이 흉내내는데 등록표에 없다 — 왜 흉내내는지 SHIMMED 에 적어라" % n)
        bad.append(n)
    for n in phantom_rows:
        print("  !! %-28s 등록표에만 있고 shim 에는 없다 — 죽은 항목" % n)
        bad.append(n)

    print("╔══ shim ↔ 프로덕션 대조 ══╗")
    for name, (mode, rel, why) in sorted(SHIMMED.items()):
        if name in phantom_rows:
            continue
        prod_path = SRC / rel
        if not prod_path.exists():
            print("  !! %-28s 프로덕션 파일 없음: %s" % (name, rel))
            bad.append(name)
            continue
        prod_clean = strip_code(prod_path.read_text(encoding="utf-8"))
        kinds = {k for k, n in declared_types(prod_clean) if n == name}
        if not kinds:
            print("  !! %-28s 프로덕션에 그런 타입이 없다(유령) — %s" % (name, rel))
            bad.append(name)
            continue
        checked["타입"] += 1

        if mode == "enum":
            s = enum_members(body_of(shim_clean, "enum", name) or "")
            p = enum_members(body_of(prod_clean, "enum", name) or "")
            if not s or not p:
                print("  !! %-28s enum 본문을 못 읽었다 — 파서 고장. 판정 무효." % name)
                bad.append(name)
                continue
            if s != p:
                only_p = {k: v for k, v in p.items() if s.get(k) != v}
                only_s = {k: v for k, v in s.items() if p.get(k) != v}
                print("  !! %-28s enum 이 갈렸다. 프로덕션에만/다름 %r · shim 에만/다름 %r" % (name, only_p, only_s))
                print("     ★ 컴파일은 되고 덤프만 조용히 틀린다 — 이것이 이번 사고의 다음 형태다.")
                bad.append(name)
            else:
                checked["enum 값"] += len(p)
                print("  OK %-28s enum %d개 값 일치" % (name, len(p)))

        elif mode == "consts":
            s = consts(body_of(shim_clean, "class", name) or "")
            p = consts(body_of(prod_clean, "class", name) or "")
            if not s:
                print("  !! %-28s shim 에서 상수를 하나도 못 읽었다 — 파서 고장. 판정 무효." % name)
                bad.append(name)
                continue
            for k in sorted(s):
                if k not in p:
                    print("  !! %-28s %s 가 프로덕션에 없다(유령 상수)" % (name, k))
                    bad.append(name)
                elif abs(p[k] - s[k]) > 1e-9:
                    print("  !! %-28s %s shim=%r 프로덕션=%r" % (name, k, s[k], p[k]))
                    bad.append(name)
                else:
                    checked["상수"] += 1
            print("  OK %-28s const %d개 일치" % (name, len(s)))

        elif mode == "table":
            # 문자열이 필요하므로 <b>원문</b>에서 본문을 뜬다(주석만 지운다).
            s_raw = body_of(re.sub(r"//[^\n]*", "", shim_src), "class", name) or ""
            p_raw = body_of(re.sub(r"//[^\n]*", "", prod_path.read_text(encoding="utf-8")), "class", name) or ""
            s_tab, p_tab = dict(CASE.findall(s_raw)), dict(CASE.findall(p_raw))
            if not s_tab:
                print("  !! %-28s shim 에서 switch 표를 하나도 못 읽었다 — 파서 고장. 판정 무효." % name)
                bad.append(name)
                continue
            diff = {k: (v, p_tab.get(k)) for k, v in s_tab.items() if p_tab.get(k) != v}
            if diff:
                print("  !! %-28s 베낀 표가 갈렸다: %r" % (name, diff))
                bad.append(name)
            else:
                checked["표 항목"] += len(s_tab)
                print("  OK %-28s switch 표 %d항목 일치" % (name, len(s_tab)))
            s_const, p_const = consts(body_of(shim_clean, "class", name) or ""), consts(body_of(prod_clean, "class", name) or "")
            for k in sorted(s_const):
                if k not in p_const or abs(p_const[k] - s_const[k]) > 1e-9:
                    print("  !! %-28s %s shim=%r 프로덕션=%r" % (name, k, s_const[k], p_const.get(k)))
                    bad.append(name)
                else:
                    checked["상수"] += 1
        else:
            print("  OK %-28s 실재 확인(값은 흉내내지 않는다: %s)" % (name, why))

    # ── (2) build.sh 가 컴파일한다고 적은 프로덕션 파일이 실재하는가 ─────────
    build = (HERE / "build.sh").read_text(encoding="utf-8")
    listed = re.findall(r'"\$SRC/([^"]+)"', build)
    if not listed:
        print("  !! build.sh 에서 프로덕션 파일 목록을 하나도 못 읽었다 — 파서 고장. 판정 무효.")
        bad.append("build.sh")
    for rel in listed:
        if not (SRC / rel).exists():
            print("  !! build.sh 가 없는 파일을 컴파일한다: %s" % rel)
            bad.append(rel)

    # ── (3) 흉내와 실물을 <b>동시에</b> 들고 있지 않은가 ────────────────────
    #    (컴파일 에러로도 잡히지만, 목록에서 빠진 채 shim 이 대신 서 있는 상태를 여기서 이름으로 본다)
    for rel in listed:
        for _, n in declared_types(strip_code((SRC / rel).read_text(encoding="utf-8"))):
            if n in SHIMMED:
                print("  !! %s 가 %s 를 선언하는데 shim 도 흉내낸다 — 둘 중 하나를 지워라" % (rel, n))
                bad.append(n)

    # ── (4) 양성 대조 폴더에 복제본이 되살아났는가 ──────────────────────────
    def meat(path):
        return {l.strip() for l in path.read_text(encoding="utf-8").splitlines()
                if len(l.strip()) > 20 and not l.strip().startswith("#")}

    for f in NO_CLONE:
        pc, mine = PC / f, HERE / f
        if not pc.exists():
            checked["복제본"] += 1
            continue
        shared = len(meat(pc) & meat(mine)) if mine.exists() else 0
        if shared > CLONE_LINE_LIMIT:
            print("  !! Tools/ShapeDumpPC/%s 가 본체와 알맹이 %d줄을 공유한다(복제본)." % (f, shared))
            print("     한쪽만 고쳐지면 양성 대조가 본 하니스와 <b>다른 물건</b>을 재게 된다.")
            bad.append(f)
            continue
        if "ShapeDump" not in pc.read_text(encoding="utf-8"):
            print("  !! Tools/ShapeDumpPC/%s 가 본체를 부르지 않는다 — 래퍼가 아니라 독립 구현이다." % f)
            bad.append(f)
            continue
        checked["복제본"] += 1

    print("╚══ 대조 %s — %s ══╝" % (
        " · ".join("%s %d" % (k, v) for k, v in checked.items()),
        ("어긋남 %d건" % len(set(bad))) if bad else "전부 일치"))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
