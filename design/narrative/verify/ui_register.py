#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""출하된 **화면 문구**의 말투(종결형) 전수 조사 + ★ 낡음 가드.

문서가 아니라 코드에서 센다. 로그/주석은 화면 문구가 아니므로 수집 대상이 아니다.
   ★★ 위 문장의 뒷절반은 실재와 갈라져 있다 — 아래 「★ 2026-09-28 — 수집 범위 판정」 절을
      먼저 읽어라. 이 수집기가 뜻으로 버리는 것은 **주석 줄 하나**이고 로그는 걸러지지 않는다.

================================================================================
★ 2026-09-27 개정 — 이 스크립트는 「기준과 대상이 같이 낡은 스냅숏」 병에 걸려 있었다
================================================================================
구판의 산출물 `ui_register.out.txt`는 **커밋된 스냅숏**인데, 아무것도 그것이 낡았는지
검사하지 않았다. 그래서 설정창 캡션이 교체된 뒤에도 산출물 한 줄이 **화면에 없는 문장**을
계속 인용했다(진단: `docs/narrative/FULLSCREEN_AUTOHIDE_SWITCH_WORDING.md` §14).

구판이 실제로 무엇을 놓쳤는가(재생성 시점 실측):
  · 요약 숫자가 수집 454 -> 647 / 해요체 36 -> 48 / 합쇼체 37 -> 59 로 크게 움직여 있었다.
  · 그런데 **계수만 보면 안 보이는 변경**이 그 안에 있었다 — 1:1 교체는 개수를 한 톨도
    바꾸지 않는다(같은 분류, 같은 파일):
        정한 시간 동안 옆에서 지켜볼게요.  ->  정한 시간 동안 옆에 있을게요.
        켜면 캐릭터도 … 사라집니다. 끄면 …   ->  춤도 멈추고, … 숨어요. 끄면 …
    둘 다 종결이 그대로여서 분류도 계수도 불변이다. ⇒ **계수 옆에 「키 집합」을 함께 본다.**

그래서 이 판은 다음을 지킨다:
  (1) 원천에서 다시 계산한 결과가 **명부(registry)**와 다르면 **rc=2**.
  (2) 대조 니들·제외 목록을 **원천에서 읽는다.** 스크립트에 문장을 베끼지 않는다
      (베끼면 그 문장이 바뀔 때 기준과 대상이 같이 낡는다).
  (3) 키는 **줄 번호가 아니라** `(파일, 문자열 앵커, 기대 횟수)`다. 줄은 남의 편집에 밀린다.
  (4) 사라진 항목은 지우지 않고 **「기대 0 + 해시」**로 보존한다(부재 단언이 조용히 초록이 되는 것을 막는다).
  (5) selftest에 **변이 대조 3종** + 무손상 음성 대조.
  (6) ★ 계수와 **키 집합**을 함께 비교한다(사라짐·생김). 계수만 보는 가드는 1:1 교체에 눈이 없다.
  (7) ★ 출력에 **판 sha16**을 함께 찍는다 — 한 실행이 남의 편집을 가로지를 수 있다.

★ 판정 함정(반드시 지킬 것): **맨 텍스트 검색으로 「옛 문장 0건」을 주장하지 마라.**
   이 저장소는 사라진 문장을 **주석에 원문 그대로 보존**하는 관례가 있다(예: 지워진
   `ShopNotice`의 문장이 주석에 남아 있다). 그래서 이 수집기는 주석 줄을 먼저 버리고
   **문자열 리터럴만** 본다. 판정도 그 기준으로만 한다.

★ 산출물 `ui_register.out.txt`의 목록은 **사람이 읽는 표시용**이다. 명부로 쓰지 마라 —
   구판 목록은 56칸 패딩·잘림이 있어 **되읽으면 원문이 복원되지 않는다**(재생성 때 실측:
   되읽기로 대조하면 긴 문장에서 유령 「사라짐·생김」이 생긴다). 기계가 읽는 기준은
   `ui_register.registry.json` 하나다.

================================================================================
★ 2026-09-28 — 수집 범위 판정: **유지한다(좁히지 않는다)**
================================================================================
위 5행의 뒷절반(「로그 … 수집 대상이 아니다」)은 **실재와 갈라져 있다.** 같은 파일의 PATS
주석은 이미 「(c) 삼항·인자·반환 리터럴」이라고 적고 있으므로 **이 파일 안에서 두 문장이
부딪히고 있었다.** 아래는 조사(2026-09-27, `design/narrative/2026-09-27_집중모드_desc_톱니링_오명_교체표.md`
§9-5)와 독립 재측정(2026-09-28)이 **같은 값으로** 낸 것이다.

실측 — as-of 2026-09-28 · `Assets/_Project/Scripts` 밑 비테스트 `.cs` 288~289파일 · 수집 651건:
  · PATS 단독 적중 분포: `[3]` 346 · `[4]` 240 · `[1]` 31 · `[0]` 23 · 겹침 11.
  · 즉 `return "리터럴"`(PATS `[4]`) 하나만으로 들어온 항목이 **240건 = 37%다**.
  · 적중한 줄이 **전부 로그 호출인 항목이 16건**이고 **전부 PATS `[3]`으로 들어왔다** —
    로그 문장 안의 삼항(`… ? "켬" : "끔"` 꼴)이 그 자리다. 일부 줄만 로그인 항목이 8건 더 있다.
  · 이 수집기가 **뜻으로 버리는 것은 주석 줄 하나**다. 위 CONTROLS의 음성 대조 니들이
    안 걸리는 이유는 그것이 **위치 인수**라서이고, 로그라서가 아니다.

⇒ 따라오는 성질: **진단 문구를 추가하는 라운드도 명부를 rc=2로 낡게 만든다.**

★ 그리고 **그보다 넓다**(2026-09-28 합성 표본으로 증명, 양성·음성 대조 동반):
  `collect()`은 주석 줄을 버리지만 `build_entries()`의 기대 횟수는 **파일 전체 텍스트의
  부분문자열 수**(`tree[rel].count(text)`)다 — **주석에 그 낱말을 한 번 더 적기만 해도** 값이
  1 늘어 rc=2가 된다(합성 표본: 「실패」 1 → 2, 무손상 음성 대조는 문제 0). 짧은 중립 낱말이
  특히 약하다. 이 개정 시점의 실제 rc=2 세 건이 전부 그 형태였다 —
  `ItemCatalog.cs`의 「행동」(명부 27 대 원천 28) · `WindowsOverlayStateEnforcer.cs`의 「실패」와
  「실행」(명부 16·21 대 원천 22·22). 문안은 한 글자도 바뀌지 않았고, 그 시각 **다른 라운드가
  고치던 파일**이었다. ★ 그 원천 값은 **몇 분 안에 또 움직였다**(「실패」 22 → 25) — 그래서
  이 괄호 안 숫자는 인용하지 말고 **직접 돌려서 읽어라.** 인용해도 되는 것은 **형태**뿐이다.
  ⇒ **rc=2를 보면 먼저 「문안이 바뀐 것인가, 그 낱말이 딴 줄에서 한 번 더 나온 것인가」를 가른다.**
  (로직은 고치지 않았다 — 이 개정은 주석만이다. 고칠지 여부는 별건 판정 사안이다.)

판정 — **범위를 유지한다.** 근거 넷:
  ① 아래 「표면을 갈라서 다시 센다」의 버킷 ③이 처음부터 **「진단·시스템 보고」 칸**이다.
     진단 문구를 수집에서 빼면 그 칸이 빈다.
  ② 로그는 **사용자가 읽는다.** 24시간 상주 앱이고, 신고에 붙어 오는 것이 그 문장이다.
     어조가 갈리면 같은 앱이 두 인격으로 말한다.
  ③ 「모든 로그 추가 라운드가 명부를 건드린다」는 **비용이 아니라 계기다.** 이 가드는 rc=2로
     시끄럽게 빨개지는 **존재 단언**이고, 조용히 초록이 되는 부재 단언 쪽이 아니다.
  ④ 대안(수집에서 로그 호출 줄 제외)을 **기각한다.** 지금 걸리는 16건에 「숨김」·「보임」·
     「다시 보이기」가 각 1건씩 있고 이 낱말들은 **화면에도 쓰인다.** 줄 단위로 로그를 거르면
     같은 글자가 화면에 뜨는 경우를 **함께 잃는다** — 조용히 놓치는 방향이라 금지 방향이다.

★ 이 절의 숫자는 as-of다. `.cs`가 바뀌면 움직인다(실측: 이 라운드 안에서 스캔 파일 수가
   288 → 289로 움직였다 — 병렬 라운드가 `.cs`를 더했다). **판정은 숫자가 아니라 위 근거 넷에
   걸려 있으므로 숫자가 움직여도 판정은 그대로다.**
★ 이 주석의 숫자·낱말은 **이 수집기의 계수를 오염시키지 않는다** — `read_tree()`가 읽는 것은
   `Assets/_Project/Scripts` 밑 `.cs`뿐이고 이 `.py`는 그 범위 밖이다.
★ ⚠ 다만 이 개정은 출력의 **「스크립트 sha16」을 바꾼다**(구판 `1860afbca7bf8f30`). 그 값을
   인용하는 자리가 둘 있다 — `ui_register.out.txt` 3행과
   `docs/narrative/FULLSCREEN_AUTOHIDE_SWITCH_WORDING.md`의 표. **낡음 가드는 명부만 보므로
   이 두 자리는 조용히 낡는다.** 재생성·문서 갱신은 리더 소관이다.
★ 이 개정은 **주석만 바꿨다** — PATS · CONTROLS · 함수 본문은 한 글자도 건드리지 않았다.

사용법:
    python3 ui_register.py            # 검사 + 보고 (명부와 다르면 rc=2)
    python3 ui_register.py --seed     # 명부를 현재 트리로 갱신(사라진 항목은 기대 0으로 보존)
    python3 ui_register.py --selftest # 변이 대조 3종 + 음성 대조
"""
import re, os, sys, io, glob, json, hashlib

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, os.pardir, os.pardir, os.pardir))  # design/narrative/verify -> 저장소 뿌리
SRC_REL = os.path.join("Assets", "_Project", "Scripts")
SRC = os.path.join(REPO, SRC_REL)
REGISTRY = os.path.join(HERE, "ui_register.registry.json")

# ★ 화면에 닿는 경로만: (a) `.text = "..."` (b) 불가 사유/안내 상수 (c) 삼항·인자·반환 리터럴
# ★ 보간 문자열($"...")도 포함한다 — 첫 판에서 이걸 빼먹어 상세 패널 문구를 통째로 놓쳤다.
PATS = [re.compile(r'\.text\s*=\s*\$?"([^"\\\n]{2,90})"'),
        re.compile(r'const string \w*(?:Reason|Notice|Text|Title|Body|Caption)\w*\s*=\s*"([^"\\\n]{2,90})"'),
        re.compile(r'(?:notice|disabledNote|reason)\s*:\s*\$?"([^"\\\n]{2,90})"'),
        re.compile(r'[?:]\s*\$?"([^"\\\n]{2,90})"'),
        re.compile(r'return\s+\$?"([^"\\\n]{2,90})"')]

END_HAEYO = re.compile(r"요[.!?]?$")
END_HAPSYO = re.compile(r"니다[.!?]?$")


def sha16(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


def classify(text):
    if END_HAEYO.search(text):
        return "해요체"
    if END_HAPSYO.search(text):
        return "합쇼체"
    return "중립"


def collect(tree):
    """{저장소상대경로: 소스텍스트} -> {문구: 파일이름}. 순수 함수(합성 입력으로 대조 가능)."""
    found = {}
    for rel in sorted(tree):
        for line in tree[rel].split("\n"):
            s = line.strip()
            if s.startswith("//") or s.startswith("*"):
                continue          # 주석은 화면 문구가 아니다(사라진 문장이 주석에 보존된다).
            for p in PATS:
                for m in p.finditer(line):
                    for g in m.groups():
                        if g and re.search(r"[가-힣]", g):
                            found[g] = os.path.basename(rel)
    return found


def read_tree():
    tree = {}
    for f in glob.glob(os.path.join(SRC, "**", "*.cs"), recursive=True):
        if os.sep + "Tests" + os.sep in f:
            continue
        tree[os.path.relpath(f, REPO)] = open(f, encoding="utf-8").read()
    return tree


# ================================================================================
# 대조 — ★ 니들을 원천에서 읽는다(문장을 여기 베끼지 않는다)
# ================================================================================
# (이름, 파일(저장소 상대), 그 파일에서 니들을 고르는 앵커 정규식, 기대 분류, 수집돼야 하는가)
CONTROLS = [
    ("양성 해요체", os.path.join(SRC_REL, "Interaction", "GraffitiDirector.cs"),
     r'const string NoEmptyRegionReason\s*=\s*"([^"\n]+)"', "해요체", True),
    ("양성 합쇼체", os.path.join(SRC_REL, "Interaction", "CharacterInfoWindow.Cards.cs"),
     r'\$"(레벨 \{entry\.RequiredLevel\}[^"\n]+)"', "합쇼체", True),
    ("음성 로그제외", os.path.join(SRC_REL, "Dialogue", "AmbientChatter.cs"),
     r'"(규칙 8 —[^"\n]+)"', None, False),
]


def run_controls(tree, found):
    """대조가 살아 있는지 먼저 증명한다. 하나라도 깨지면 아래 숫자를 전부 폐기한다."""
    lines, ok = [], True
    for name, rel, anchor, want_class, want_collected in CONTROLS:
        text = tree.get(rel)
        if text is None:
            lines.append("   %s: **원천 파일이 없다** (%s) -> 판정 불가" % (name, rel))
            ok = False
            continue
        m = re.search(anchor, text)
        if not m:
            lines.append("   %s: **앵커가 원천에서 사라졌다** (%s) -> 판정 불가" % (name, rel))
            ok = False
            continue
        needle = m.group(1)
        got_collected = needle in found
        good = (got_collected == want_collected)
        if want_class is not None:
            good = good and classify(needle) == want_class
        lines.append("   %s: 원천에서 읽은 니들 sha=%s · 수집 %s(기대 %s)%s -> %s"
                     % (name, sha16(needle), got_collected, want_collected,
                        (" · 분류 %s(기대 %s)" % (classify(needle), want_class)) if want_class else "",
                        "OK" if good else "**실패**"))
        ok = ok and good
    return ok, lines


# ================================================================================
# 명부(registry) — 키는 (파일, 문자열 앵커), 값은 기대 횟수와 분류
# ================================================================================
def build_entries(found, tree):
    """각 문구의 기대 횟수를 원천에서 센다(줄 번호를 쓰지 않는다)."""
    entries = {}
    for text, base in found.items():
        count = 0
        for rel in tree:
            if os.path.basename(rel) == base:
                count += tree[rel].count(text)
        entries["%s\t%s" % (base, text)] = {"file": base, "text": text,
                                            "count": count, "class": classify(text)}
    return entries


def load_registry():
    if not os.path.exists(REGISTRY):
        return None
    with open(REGISTRY, encoding="utf-8") as fh:
        return json.load(fh)


def save_registry(entries, retired):
    payload = {
        "note": "기계가 읽는 기준. out.txt의 목록은 표시용이라 되읽지 마라(패딩·잘림).",
        "entries": [entries[k] for k in sorted(entries)],
        "retired": retired,
    }
    with open(REGISTRY, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=1, sort_keys=True)
        fh.write("\n")


def compare(entries, reg):
    """계수 + ★ 키 집합을 함께 본다. 반환: (문제목록, 사라짐, 생김)"""
    old = {"%s\t%s" % (e["file"], e["text"]): e for e in reg.get("entries", [])}
    gone = sorted(set(old) - set(entries))
    born = sorted(set(entries) - set(old))
    problems = []
    for k in sorted(set(old) & set(entries)):
        if old[k]["count"] != entries[k]["count"]:
            problems.append("기대 횟수 불일치 %s: 명부 %d != 원천 %d"
                            % (old[k]["text"][:40], old[k]["count"], entries[k]["count"]))
        if old[k]["class"] != entries[k]["class"]:
            problems.append("분류 변경 %s: 명부 %s != 원천 %s"
                            % (old[k]["text"][:40], old[k]["class"], entries[k]["class"]))
    for r in reg.get("retired", []):
        for k in entries:
            if sha16(entries[k]["text"]) == r.get("text_sha"):
                problems.append("기대 0으로 보존한 항목이 되살아났다: sha=%s" % r.get("text_sha"))
    return problems, gone, born


# ================================================================================
# selftest — 변이 대조 3종 + 무손상 음성 대조
# ================================================================================
def selftest():
    print("=== selftest — 가드가 살아 있는지 증명한다 ===")
    tree = {os.path.join(SRC_REL, "Fake", "A.cs"):
            'x.text = "오늘은 아직 비어 있어요";\ny.text = "지금은 못 합니다.";\n'}
    found = collect(tree)
    base = build_entries(found, tree)
    reg = {"entries": [base[k] for k in sorted(base)], "retired": []}
    fails = 0

    p, g, b = compare(base, reg)
    print("   음성 대조(무손상): 문제 %d · 사라짐 %d · 생김 %d -> %s"
          % (len(p), len(g), len(b), "OK" if not (p or g or b) else "**오탐**"))
    fails += 1 if (p or g or b) else 0

    # 변이 ① 1:1 교체 — ★ 계수와 분류가 **불변**이다. 키 집합만이 잡는다.
    t1 = {k: v.replace("오늘은 아직 비어 있어요", "오늘은 적어둔 게 없어요") for k, v in tree.items()}
    e1 = build_entries(collect(t1), t1)
    p1, g1, b1 = compare(e1, reg)
    same_counts = (len(e1) == len(base))
    print("   변이① 1:1 교체(계수 불변=%s): 사라짐 %d · 생김 %d -> %s"
          % (same_counts, len(g1), len(b1), "잡힘 OK" if (g1 and b1) else "**놓침**"))
    fails += 0 if (g1 and b1) else 1

    # 변이 ② 항목 삭제
    t2 = {k: v.replace('y.text = "지금은 못 합니다.";\n', "") for k, v in tree.items()}
    e2 = build_entries(collect(t2), t2)
    p2, g2, b2 = compare(e2, reg)
    print("   변이② 항목 삭제: 사라짐 %d -> %s" % (len(g2), "잡힘 OK" if g2 else "**놓침**"))
    fails += 0 if g2 else 1

    # 변이 ③ 분류 불일치 — 명부 쪽 분류를 흔들어 분류 비교 자체를 시험한다.
    reg3 = {"entries": [dict(e) for e in reg["entries"]], "retired": []}
    reg3["entries"][0]["class"] = "합쇼체" if reg3["entries"][0]["class"] != "합쇼체" else "해요체"
    p3, _, _ = compare(base, reg3)
    print("   변이③ 분류 불일치: 문제 %d -> %s" % (len(p3), "잡힘 OK" if p3 else "**놓침**"))
    fails += 0 if p3 else 1

    # 변이 ④(덤) 기대 0 보존 항목의 부활
    reg4 = {"entries": reg["entries"], "retired": [{"text_sha": sha16("오늘은 아직 비어 있어요"), "count": 0}]}
    p4, _, _ = compare(base, reg4)
    print("   변이④ 기대 0 항목 부활: 문제 %d -> %s" % (len(p4), "잡힘 OK" if p4 else "**놓침**"))
    fails += 0 if p4 else 1

    print("   selftest 실패 %d건" % fails)
    return 0 if fails == 0 else 2


# ================================================================================
def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    if mode == "--selftest":
        return selftest()

    tree = read_tree()
    found = collect(tree)
    entries = build_entries(found, tree)

    # ★ 판 sha16 (규칙 29) — 한 실행이 남의 편집을 가로지를 수 있다.
    corpus = "\n".join("%s|%s|%d" % (entries[k]["file"], entries[k]["text"], entries[k]["count"])
                       for k in sorted(entries))
    print("=== 판 (이 실행이 무엇을 읽었는가) ===")
    print("   스캔 파일 %d개 · 수집 %d건 · 말뭉치 digest sha16=%s" % (len(tree), len(found), sha16(corpus)))
    print("   스크립트 sha16=%s" % sha16(open(os.path.abspath(__file__), encoding="utf-8").read()))

    ok, lines = run_controls(tree, found)
    print("\n=== 대조 (니들을 원천에서 읽었다 — 스크립트에 문장을 베끼지 않는다) ===")
    for l in lines:
        print(l)
    if not ok:
        print("\n★ 대조가 깨졌다 — 아래 숫자를 신뢰하지 마라. rc=2")
        return 2

    reg = load_registry()
    hae = {t for t in found if classify(t) == "해요체"}
    hap = {t for t in found if classify(t) == "합쇼체"}
    print("\n=== 화면 문구 종결형 전수 (수집 %d건) ===" % len(found))
    print("   해요체 %d건 / 합쇼체 %d건 / 종결형 없음(라벨·명사구) %d건"
          % (len(hae), len(hap), len(found) - len(hae) - len(hap)))
    print("   문장으로 끝나는 것만 세면: 해요체 %d / 합쇼체 %d  ->  해요체 %.0f%%"
          % (len(hae), len(hap), 100.0 * len(hae) / max(1, len(hae) + len(hap))))

    rc = 0
    if reg is None:
        print("\n★ 명부가 없다 — `--seed`로 만들어라(이 실행은 검사하지 않았다). rc=2")
        rc = 2
    else:
        problems, gone, born = compare(entries, reg)
        print("\n=== ★ 낡음 가드 — 계수와 **키 집합**을 함께 본다(계수만 보면 1:1 교체가 통과한다) ===")
        print("   명부 %d항목 / 원천 %d항목 · 사라짐 %d · 생김 %d · 불일치 %d"
              % (len(reg.get("entries", [])), len(entries), len(gone), len(born), len(problems)))
        for k in gone:
            print("   [사라짐] %s  sha=%s" % (k.split("\t", 1)[1], sha16(k.split("\t", 1)[1])))
        for k in born:
            print("   [생김]   %s  (%s)" % (entries[k]["text"], entries[k]["file"]))
        for p in problems:
            print("   [불일치] %s" % p)
        if gone or born or problems:
            print("   ⇒ 원천이 명부와 다르다. 문안이 바뀐 것이 맞으면 `--seed`로 갱신하고 "
                  "인용 문서를 함께 고쳐라. rc=2")
            rc = 2
        else:
            print("   ⇒ 원천과 명부가 같다.")

    print("\n   [합쇼체 전량 — 소수파라 전부 나열한다]  ※ 목록은 표시용이다(되읽어 명부로 쓰지 마라)")
    for t in sorted(hap):
        print("     %s\t(%s)" % (t, found[t]))
    print("\n   [해요체 전량]")
    for t in sorted(hae):
        print("     %s\t(%s)" % (t, found[t]))

    print("\n=== 가설 검정 ===")
    road = [t for t in hap if "업데이트" in t]
    print("   H1 '합쇼체는 제품 로드맵 전용이다' : 합쇼체 %d건 중 '다음 업데이트' 계열 %d건 (%.0f%%)"
          % (len(hap), len(road), 100.0 * len(road) / max(1, len(hap))))
    for t in sorted(t for t in hap if t not in road)[:8]:
        print("      반례: %s  (%s)" % (t, found[t]))

    print("\n=== ★ 표면을 갈라서 다시 센다 — '어디에 쓰인 말투인가' ===")
    FACE = {"TodoBoardPopover.cs", "FocusSessionPopover.cs", "ActionCommandPopover.cs", "CharacterInfoWindow.cs",
            "CharacterInfoWindow.Cards.cs", "CharacterInfoWindow.Tabs.cs", "CharacterInfoWindow.Inventory.cs",
            "CharacterInfoWindow.Shop.cs", "CharacterInfoWindow.Dlc.cs",
            "GraffitiDirector.cs", "ArcheryDirector.cs", "WindowTheftDirector.cs", "WindowCrashDirector.cs",
            "TodoReminderDirector.cs", "CommandAvailability.cs", "StickMateDisplayNames.cs",
            "GearRadialMenuWidget.cs", "RunawayDirector.cs", "ItemCatalog.cs",
            "HiddenCharacterCommandGate.cs", "UnsummonedSurfaceCommandReason.cs"}
    SETTINGS = {"SettingsWindow.cs", "SettingsControls.cs", "InfoGearIconWidget.cs", "AppControlDirector.cs"}

    def bucket(f):
        if f in FACE:
            return "① 캐릭터 표면"
        if f in SETTINGS:
            return "② 설정창"
        return "③ 진단·시스템 보고"

    tab = {}
    for t in found:
        c = classify(t)
        if c == "중립":
            continue
        tab.setdefault(bucket(found[t]), {"해요체": 0, "합쇼체": 0})[c] += 1
    print("   표면                 | 해요체 | 합쇼체 | 해요체 비율")
    for b in sorted(tab):
        h, p_ = tab[b]["해요체"], tab[b]["합쇼체"]
        print("   %-20s | %5d | %5d | %.0f%%" % (b, h, p_, 100.0 * h / max(1, h + p_)))
    print("\n   ★ ① 캐릭터 표면의 합쇼체 반례 전량 — 상점 문구가 바로 이 표면에 들어간다:")
    for t in sorted(t for t in hap if bucket(found[t]) == "① 캐릭터 표면"):
        print("     %s\t(%s)" % (t, found[t]))

    if mode == "--seed":
        old = load_registry() or {"entries": [], "retired": []}
        old_keys = {"%s\t%s" % (e["file"], e["text"]): e for e in old.get("entries", [])}
        retired = list(old.get("retired", []))
        seen = {r.get("text_sha") for r in retired}
        for k in sorted(set(old_keys) - set(entries)):
            t = old_keys[k]["text"]
            if sha16(t) not in seen:
                retired.append({"text_sha": sha16(t), "count": 0, "file": old_keys[k]["file"],
                                "note": "사라짐 — 기대 0으로 보존(부활하면 rc=2)"})
                seen.add(sha16(t))
        save_registry(entries, retired)
        print("\n★ 명부를 갱신했다: 항목 %d · 기대 0 보존 %d · 명부 sha16=%s"
              % (len(entries), len(retired), sha16(open(REGISTRY, encoding="utf-8").read())))
        return 0

    if reg is not None:
        print("\n   명부 sha16=%s" % sha16(open(REGISTRY, encoding="utf-8").read()))
    return rc


if __name__ == "__main__":
    sys.exit(main())
