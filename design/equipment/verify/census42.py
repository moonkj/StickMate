# -*- coding: utf-8 -*-
"""현행 42종 전수 — .asset 직접 파싱 + \\uXXXX 디코드.
★ 함정: .asset의 한글은 \\uXXXX 이스케이프라 grep '[가-힣]'이 영원히 0건이다.
   그래서 YAML 더블쿼트 문자열을 codecs로 되돌려서 읽는다. 양성 대조 포함."""
import os, re, sys, json

DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "..", "..", "Assets", "_Project", "Resources", "Items")
DIR = os.path.abspath(DIR)

SLOT = {0: "HEAD", 1: "EYES", 2: "NECK", 3: "BACK(shoulders)",
        4: "HAIR", 5: "FX", 6: "PET"}

def unesc(s):
    # YAML 더블쿼트 안의 \uXXXX 를 되돌린다. 줄바꿈+들여쓰기로 접힌 것도 편다.
    s = re.sub(r"\s*\n\s*", " ", s.strip())
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1]
    return s.encode("ascii", "backslashreplace").decode("unicode_escape")

def parse(path):
    raw = open(path, encoding="utf-8").read()
    d = {}
    for key in ("itemId", "slot", "itemIndex", "requiredLevel", "hidesHair", "m_Name"):
        m = re.search(r"^\s*%s:\s*(.+)$" % key, raw, re.M)
        d[key] = m.group(1).strip() if m else None
    # displayName / description 는 여러 줄로 접힐 수 있다
    for key in ("displayName", "description"):
        m = re.search(r"^  %s:\s*(\".*?\"|\S.*?)$(?:\n(?=    \S)((?:    .*\n?)*))?"
                      % key, raw, re.M)
        if not m:
            d[key] = None; continue
        body = m.group(1) + (("\n" + m.group(2)) if m.group(2) else "")
        d[key] = unesc(body)
    d["iconPieces"] = len(re.findall(r"^  - kind:", raw, re.M))
    d["file"] = os.path.basename(path)
    return d

def main():
    files = sorted(f for f in os.listdir(DIR) if f.endswith(".asset"))
    rows = [parse(os.path.join(DIR, f)) for f in files]

    # ── 눈금 교정(양성 대조) — 디코더가 실제로 한글을 뱉는가 ──────────────
    probe = unesc('"\\uCC9C\\uBAA8\\uC790"')
    ok = (probe == "천모자")
    print("[교정] \\uCC9C\\uBAA8\\uC790 -> %r  기대 '천모자'  => %s" % (probe, "OK" if ok else "FAIL"))
    neg = unesc('"\\u0041\\u0042"')
    print("[교정] \\u0041\\u0042 -> %r  기대 'AB'  => %s" % (neg, "OK" if neg == "AB" else "FAIL"))
    if not ok or neg != "AB":
        print("!! 교정 실패 — 아래 숫자를 전부 폐기한다"); sys.exit(1)
    # 대조군: 순진한 grep 이 정말 0건인가
    import subprocess
    n = subprocess.run(["grep", "-l", "[가-힣]"] + [os.path.join(DIR, f) for f in files],
                       capture_output=True, text=True).stdout.strip()
    print("[대조] grep '[가-힣]' 매칭 파일 수 = %d  (0이라야 브리핑의 함정이 재현된 것)"
          % (0 if not n else len(n.splitlines())))
    print()

    bys = {}
    for r in rows:
        bys.setdefault(int(r["slot"]), []).append(r)
    print("총 %d종" % len(rows))
    for s in sorted(bys):
        lst = sorted(bys[s], key=lambda r: int(r["itemIndex"]))
        print("\n── slot %d = %s (%d종) ──" % (s, SLOT[s], len(lst)))
        for r in lst:
            print("  [%d] %-28s %-8s lv=%-2s hidesHair=%-2s icon조각=%d  %s"
                  % (int(r["itemIndex"]), r["itemId"], r["displayName"],
                     r["requiredLevel"], r["hidesHair"], r["iconPieces"], r["description"]))
    print("\n[슬롯별 개수] " + "  ".join("%s=%d" % (SLOT[s], len(bys[s])) for s in sorted(bys)))
    ids = [r["itemId"] for r in rows]
    print("[중복 itemId] %s" % ("없음" if len(set(ids)) == len(ids) else "있음!"))
    json.dump(rows, open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                      "census42.json"), "w"), ensure_ascii=False, indent=1)

main()
