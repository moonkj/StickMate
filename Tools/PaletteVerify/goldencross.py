#!/usr/bin/env python3
"""verify-change 독립 대조 — 골든(ItemCatalogGolden.txt) vs 애셋 YAML.

골든을 만든 것과 <b>다른 경로</b>로 애셋을 읽어 값을 다시 유도한 뒤 대조한다.
(골든은 Unity가 ItemCatalogDigest로 굽고, 이쪽은 파이썬이 YAML을 직접 읽는다.)
"""
import os, re, sys, collections

ROOT   = os.path.join(os.path.dirname(__file__), "../..")
GOLDEN = os.path.join(ROOT, "Assets/_Project/Scripts/Tests/EditMode/Golden/ItemCatalogGolden.txt")
ITEMS  = os.path.join(ROOT, "Assets/_Project/Resources/Items")

def f5(v): return "%.5f" % v

# ---- 애셋에서 유도 ----
def from_assets():
    out = {}
    for f in sorted(os.listdir(ITEMS)):
        if not f.endswith(".asset"): continue
        t = open(os.path.join(ITEMS,f), encoding="utf-8", errors="replace").read()
        iid = re.search(r'^\s*itemId:\s*(\S+)', t, re.M)
        if not iid: continue
        pieces = []
        for m in re.finditer(r'^\s*color:\s*\{r:\s*([-\d.eE]+),\s*g:\s*([-\d.eE]+),'
                             r'\s*b:\s*([-\d.eE]+),\s*a:\s*([-\d.eE]+)\}\s*\n\s*tone:\s*(\d+)', t, re.M):
            pieces.append((tuple(float(m.group(i)) for i in (1,2,3,4)), int(m.group(5))))
        out[iid.group(1)] = pieces
    return out

# ---- 골든에서 파싱 ----
def from_golden(path=None):
    path = path or GOLDEN
    items = collections.OrderedDict()
    cur = None
    for line in open(path, encoding="utf-8"):
        m = re.match(r'\s*item id=(\S+)', line)
        if m: cur = m.group(1); items[cur] = {"pieces": [], "primary": None, "secondary": None}; continue
        if cur is None: continue
        m = re.match(r'\s*primary=\(([^)]*)\)\s*secondary=\(([^)]*)\)', line)
        if m:
            items[cur]["primary"]   = tuple(x.strip() for x in m.group(1).split(","))
            items[cur]["secondary"] = tuple(x.strip() for x in m.group(2).split(","))
            continue
        m = re.match(r'\s*p\d+ kind=\S+ tone=(\d+) color=\(([^)]*)\)', line)
        if m:
            items[cur]["pieces"].append((tuple(x.strip() for x in m.group(2).split(",")), int(m.group(1))))
    return items

def main():
    a, g = from_assets(), from_golden()
    print(f"애셋 아이템 {len(a)}개 / 골든 아이템 {len(g)}개")
    equip_g = {k: v for k, v in g.items() if v["pieces"]}
    print(f"골든 중 아이콘 조각이 있는 항목 {len(equip_g)}개")
    if not equip_g:
        print("골든 열거가 비었다 — 아래 '불일치 0'은 무의미."); return 2

    cmps = 0; bad = []
    for iid, gv in equip_g.items():
        if iid not in a: bad.append(f"{iid}: 애셋에 없음"); continue
        ap = a[iid]
        # (1) 조각 수
        cmps += 1
        if len(ap) != len(gv["pieces"]):
            bad.append(f"{iid}: 조각 수 애셋 {len(ap)} vs 골든 {len(gv['pieces'])}")
            continue
        # (2) 조각별 색 + tone
        for i, ((ac, at), (gc, gt)) in enumerate(zip(ap, gv["pieces"])):
            cmps += 1
            if at != gt: bad.append(f"{iid}#p{i}: tone 애셋 {at} vs 골든 {gt}")
            cmps += 1
            if tuple(f5(x) for x in ac) != gc:
                bad.append(f"{iid}#p{i}: 색 애셋 {tuple(f5(x) for x in ac)} vs 골든 {gc}")
        # (3) 주/보조 — ItemCatalogEntry 규칙을 다시 적용해 골든의 값과 맞추기
        pri = next((c for c,tn in ap if tn == 0), None)
        sec = next((c for c,tn in ap if tn != 0), None)
        if pri is None: pri = (0xD6/255,0xDB/255,0xE3/255,1.0)
        if sec is None: sec = pri
        cmps += 1
        if tuple(f5(x) for x in pri) != gv["primary"]:
            bad.append(f"{iid}: primary 유도 {tuple(f5(x) for x in pri)} vs 골든 {gv['primary']}")
        cmps += 1
        if tuple(f5(x) for x in sec) != gv["secondary"]:
            bad.append(f"{iid}: secondary 유도 {tuple(f5(x) for x in sec)} vs 골든 {gv['secondary']}")

    print(f"\n대조 건수 {cmps}건 / 불일치 {len(bad)}건")
    for b in bad[:40]: print("  ", b)

    # 양성 대조 — 골든 한 글자를 바꾼 사본이 실제로 걸리는가
    print("\n=== 양성 대조 (탐지력) ===")
    txt = open(GOLDEN, encoding="utf-8").read()
    m = re.search(r'(primary=\(0\.)(\d)', txt)
    mutated = txt[:m.start(2)] + str((int(m.group(2))+1) % 10) + txt[m.end(2):]
    import tempfile
    with tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False, encoding="utf-8") as fh:
        fh.write(mutated); tmp = fh.name
    g2 = from_golden(tmp)
    caught = 0
    for iid, gv in g2.items():
        if not gv["pieces"] or iid not in a: continue
        ap = a[iid]
        pri = next((c for c,tn in ap if tn == 0), None) or (0,0,0,1)
        if tuple(f5(x) for x in pri) != gv["primary"]: caught += 1
    os.unlink(tmp)
    print(f"  골든 숫자 한 자리를 바꾼 사본 -> 불일치 {caught}건 검출 "
          f"({'OK — 탐지력 있음' if caught > 0 else '★FAIL — 대조가 아무것도 안 본다'})")
    return 0 if not bad and caught > 0 else 1

if __name__ == "__main__":
    sys.exit(main())
