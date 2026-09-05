#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
인계본 ItemIcon.dc.html 조각 전수 -> "우리 렌더 경로가 그릴 수 있는가" 능력 감사.

원칙: HTML을 직접 파싱한다(손으로 베끼지 않는다).
교정: 조각 총수 91 / 아이템별 조각 수 / 하이라이트 17 을 doc 기재값과 대조한다.
      교정이 깨지면 그 뒤 숫자는 전부 폐기.
"""
import re, sys, json, os

SRC = "/Users/kjmoon/App/StickMate/docs/handoff/design_handoff_equipment_window/reference/ItemIcon.dc.html"
src = open(SRC, encoding="utf-8").read()

# ---- K = { ... } 블록만 잘라낸다 (중괄호 균형) -------------------------------
i = src.index("const K = {")
j = src.index("{", i)
depth = 0
for k in range(j, len(src)):
    if src[k] == "{": depth += 1
    elif src[k] == "}":
        depth -= 1
        if depth == 0:
            end = k
            break
block = src[j+1:end]

# ---- 아이템별 배열 분리 -----------------------------------------------------
items = {}
pos = 0
name_re = re.compile(r"(\w+)\s*:\s*\(\)\s*=>\s*\[")
while True:
    m = name_re.search(block, pos)
    if not m: break
    name = m.group(1)
    s = m.end() - 1          # '[' 위치
    d = 0
    for k in range(s, len(block)):
        if block[k] == "[": d += 1
        elif block[k] == "]":
            d -= 1
            if d == 0:
                e = k
                break
    items[name] = block[s+1:e]
    pos = e

# ---- 각 아이템 안에서 최상위 호출 조각 분리 ---------------------------------
CALL = re.compile(r"\b(B|S|F|H|CB|CF|RB)\s*\(")

def split_calls(body):
    out = []
    for m in CALL.finditer(body):
        # 최상위인지 확인: 여는 위치까지의 괄호/대괄호/중괄호 깊이가 0이어야 한다
        pre = body[:m.start()]
        if pre.count("(") != pre.count(")"): continue
        if pre.count("{") != pre.count("}"): continue
        s = m.end() - 1
        d = 0
        for k in range(s, len(body)):
            if body[k] == "(": d += 1
            elif body[k] == ")":
                d -= 1
                if d == 0:
                    e = k; break
        out.append((m.group(1), body[s+1:e]))
    return out

# ---- 조각 하나의 "요구 렌더 기능" 판정 --------------------------------------
# 우리 Shape 구조체가 나를 수 있는 것: 점열 / Loop / SortingOrder / Sway / Tone(0,1,2) / Filled(bool)
# 그 외 전부 데이터 모델에 자리가 없다.
def features(kind, args):
    f = set()
    opt = args
    # ---- 채움 ----
    if kind in ("B", "CB", "RB"):
        f.add("fill:gradient")                     # 기본이 url(#G) 세로 선형 그라디언트
        if "fill:" in opt.replace(" ", "") or re.search(r"fill\s*:", opt):
            f.discard("fill:gradient")
            f.add("fill:flatAccent")               # { fill: A } 로 덮어씀
    if kind in ("F", "CF"):
        f.add("fill:flatAccent")
    if kind == "S":
        f.add("fill:none")
    if kind == "H":
        f.add("fill:none")
    # ---- 부분 투명도 ----
    mo = re.search(r"fillOpacity\s*:\s*([0-9.]+)", opt)
    if mo: f.add("alpha:fill=%s" % mo.group(1))
    elif kind == "F": f.add("alpha:fill=0.55")     # F 기본값
    elif kind in ("B", "CB", "RB") and "fill:gradient" in f:
        f.add("alpha:fill=grad0.34->0.08")         # defs 의 stop opacity
    mo = re.search(r"strokeOpacity\s*:\s*([0-9.]+)", opt)
    if mo: f.add("alpha:stroke=%s" % mo.group(1))
    elif kind == "H": f.add("alpha:stroke=0.42")   # H 기본값
    # ---- 획 두께 배수 ----
    mo = re.search(r"strokeWidth\s*:\s*w\s*\*\s*([0-9.]+)", opt)
    if mo: f.add("strokeW:x%s" % mo.group(1))
    elif kind == "H": f.add("strokeW:x0.75")       # H 기본값
    elif kind in ("B", "S", "CB", "RB"): f.add("strokeW:x1.0")
    # ---- 파선 ----
    if "strokeDasharray" in opt: f.add("dash")
    # ---- 획 색 ----
    if kind == "H": f.add("strokeColor:WHITE")     # 제3색 — 우리 Tone 0/1/2 밖
    elif kind in ("B", "S", "CB", "RB"): f.add("strokeColor:C(주색)")
    # ---- 프리미티브 ----
    if kind in ("CB", "CF"): f.add("prim:circle")
    if kind == "RB": f.add("prim:roundrect")
    if kind in ("B", "S", "F", "H"): f.add("prim:path")
    # ---- 열림/닫힘 ----
    if kind in ("S", "H"): f.add("open")
    else: f.add("closed")
    return f

rows = []
for name, body in items.items():
    for idx, (kind, args) in enumerate(split_calls(body)):
        rows.append(dict(item=name, i=idx, kind=kind, feat=sorted(features(kind, args)), raw=args.strip()[:70]))

# ---- 교정 -------------------------------------------------------------------
EXPECT = dict(clothhat=4, furhat=5, fedora=4, crown=9, sunglasses=6, roundglasses=7,
              goggles=7, monocle=4, bowtie=4, stripedtie=5, scarf=6, bellnecklace=8,
              shortcape=4, longcape=6, wings=6, backpack=6)
got = {}
for r in rows: got[r["item"]] = got.get(r["item"], 0) + 1
bad = [(k, EXPECT[k], got.get(k, 0)) for k in EXPECT if EXPECT[k] != got.get(k, 0)]
print("== 교정 (docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §4-1 원본 조각 수 표) ==")
print("  아이템 수      : %d (기대 16)" % len(items))
print("  조각 총수      : %d (기대 91)" % len(rows))
nH = sum(1 for r in rows if r["kind"] == "H")
print("  하이라이트(H)  : %d (기대 17)" % nH)
if bad or len(rows) != 91 or len(items) != 16 or nH != 17:
    print("  !! 교정 실패:", bad); sys.exit(2)
print("  -> 교정 통과. 아래 숫자를 신뢰한다.\n")

# ---- 능력 매트릭스 ----------------------------------------------------------
# 우리가 실제로 그릴 수 있는 것 (실측 근거는 보고서 본문)
CAN = {
    "prim:path", "prim:circle", "prim:roundrect",   # 전부 다각형 근사로 표현 가능(점열)
    "open", "closed",
    "fill:none", "fill:flatAccent",                 # Tone=1(보조색) 평면 채움
    "strokeColor:C(주색)",
    "strokeW:x1.0",
}
def why(feat):
    miss = [f for f in feat if f not in CAN
            and not f.startswith("alpha:")
            and not (f.startswith("strokeW:") and f != "strokeW:x1.0")]
    return miss

# 결핍 축별 집계
axes = {
    "A. 부분 투명도(alpha) — fill/stroke opacity": lambda f: any(x.startswith("alpha:") and not x.startswith("alpha:fill=grad") for x in f),
    "B. 세로 그라디언트 채움(url(#G))":            lambda f: "fill:gradient" in f,
    "C. 조각별 획 두께 배수(w*k, k!=1)":           lambda f: any(x.startswith("strokeW:x") and x != "strokeW:x1.0" for x in f),
    "D. 흰색 하이라이트 획(제3색)":                lambda f: "strokeColor:WHITE" in f,
    "E. 파선(strokeDasharray)":                    lambda f: "dash" in f,
}
print("== 조각 91개가 요구하는 렌더 기능 중, 우리 데이터 모델/렌더러에 자리가 없는 것 ==")
for label, pred in axes.items():
    hit = [r for r in rows if pred(r["feat"])]
    per = {}
    for r in hit: per[r["item"]] = per.get(r["item"], 0) + 1
    print("  %-42s %2d조각 / %2d종" % (label, len(hit), len(per)))

# 조각별 "모든 요구 기능을 지금 그대로 그릴 수 있는가"
def fully(f):
    for label, pred in axes.items():
        if pred(f): return False
    return True
ok = [r for r in rows if fully(r["feat"])]
print("\n  ★ 지금 구조로 '보이는 그대로' 재현 가능한 조각: %d / 91 (%.0f%%)" % (len(ok), 100*len(ok)/91))
print("  ★ 표현 기능이 없어서 재현 불가한 조각          : %d / 91 (%.0f%%)" % (91-len(ok), 100*(91-len(ok))/91))

# 아이템별
print("\n== 아이템별 (재현가능/총) ==")
for name in EXPECT:
    tot = got[name]; good = sum(1 for r in rows if r["item"] == name and fully(r["feat"]))
    print("  %-14s %d/%d" % (name, good, tot))

# 획 두께 배수 스펙트럼
mult = {}
for r in rows:
    for f in r["feat"]:
        if f.startswith("strokeW:x"):
            mult[f[9:]] = mult.get(f[9:], 0) + 1
print("\n== 인계본이 쓰는 획 두께 배수 스펙트럼(우리는 2단계뿐: 낱선 / 채움윤곽) ==")
for k in sorted(mult, key=float): print("   w x%-5s : %2d조각" % (k, mult[k]))

# 투명도 값 스펙트럼
alp = {}
for r in rows:
    for f in r["feat"]:
        if f.startswith("alpha:"): alp[f] = alp.get(f, 0) + 1
print("\n== 인계본이 쓰는 투명도 값 스펙트럼 ==")
for k in sorted(alp): print("   %-28s : %2d조각" % (k, alp[k]))

json.dump(rows, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "pieces.json"), "w"),
          ensure_ascii=False, indent=1)
