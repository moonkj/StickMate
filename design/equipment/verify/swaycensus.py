# -*- coding: utf-8 -*-
"""흔들림(sway) 소유 전수 — .cs case블록 + **.asset 데이터** 양쪽을 센다.
★ 리더 실측 "sway를 가진 것은 망토 계열뿐"은 .cs만 본 결과다.
   NECK은 B-2 파일럿으로 형상이 에셋에 있고 swayStart도 거기 있다 — .cs grep이 구조적으로 못 본다."""
import os, re

ROOT = "/Users/kjmoon/App/StickMate/Assets/_Project"
CS = os.path.join(ROOT, "Scripts", "Interaction", "AccessoryShapeBuilder.cs")
ITEMS = os.path.join(ROOT, "Resources", "Items")

# ── (A) .cs 안의 case 블록별 sway ─────────────────────────────────────────
src = open(CS, encoding="utf-8").read().splitlines()
cur, csown = None, {}
for i, ln in enumerate(src):
    m = re.match(r"\s*case (Head|Eyes|Neck|Back|Hair)(\w+):", ln)
    if m: cur = m.group(1) + m.group(2)
    if "swayStart:" in ln and cur:
        csown.setdefault(cur, 0)
        csown[cur] += 1
print("── (A) AccessoryShapeBuilder.cs 의 case블록별 swayStart 인자 ──")
for k, v in csown.items(): print("   %-16s %d개 도형" % (k, v))
print("   → .cs 기준 sway 보유: %s" % (", ".join(csown) or "없음"))

# ── (B) .asset 의 wornShapes swayStart ────────────────────────────────────
def unesc(s):
    s = re.sub(r"\s*\n\s*", " ", s.strip())
    if s.startswith('"') and s.endswith('"'): s = s[1:-1]
    return s.encode("ascii", "backslashreplace").decode("unicode_escape")

print("\n── (B) .asset wornShapes 의 swayStart (에셋 데이터) ──")
found = {}
for f in sorted(os.listdir(ITEMS)):
    if not f.endswith(".asset"): continue
    raw = open(os.path.join(ITEMS, f), encoding="utf-8").read()
    m = re.search(r"^  wornShapes:\s*$\n((?:  [ -].*\n)*)", raw, re.M)
    if not m: continue
    body = m.group(1)
    names = re.findall(r"name:\s*(\S+)", body)
    sways = [int(x) for x in re.findall(r"swayStart:\s*(-?\d+)", body)]
    cnts  = [int(x) for x in re.findall(r"swayCount:\s*(-?\d+)", body)]
    swing = [float(x) for x in re.findall(r"swingDegrees:\s*(-?[\d.]+)", body)]
    dn = unesc(re.search(r"^  displayName:\s*(.+)$", raw, re.M).group(1))
    act = [(n, s, c) for n, s, c in zip(names, sways, cnts) if s >= 0 and c > 0]
    found[f] = (dn, len(names), act, [d for d in swing if d != 0])
    print("   %-32s %-8s 도형%d개  sway보유 %d개 %s  swing≠0 %d개"
          % (f[:-6], dn, len(names), len(act),
             [a[0] for a in act] if act else "", len(swing) and len([d for d in swing if d])))
if not found:
    print("   (wornShapes를 가진 에셋이 없다)")

print("\n── (C) 결론 ──")
csset = set(k for k in csown)
assetset = set(v[0] for k, v in found.items() if v[2])
print("   .cs 로만 세면      : %s" % (", ".join(sorted(csset))))
print("   .asset 까지 세면 +  : %s" % (", ".join(sorted(assetset)) or "(없음)"))
print("   ★ .cs grep 이 구조적으로 못 보는 sway 보유 아이템 = %d종" % len(assetset))
