#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
인계본 91조각 기하 실측 + 「하이라이트는 무엇 위에 그려지는가」 반증 시험.

교정(알려진 값으로 먼저 맞춘다 — 깨지면 그 뒤 숫자 전부 폐기):
  docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §4-2-7 의 잉크값 6건과 §4-2-1 의 W 값.
"""
import re, sys, math, json

SRC = "/Users/kjmoon/App/StickMate/docs/handoff/design_handoff_equipment_window/reference/ItemIcon.dc.html"
src = open(SRC, encoding="utf-8").read()

# ---------- 프로덕션 상수 (식으로 옮긴다) ----------
BASE_STROKE      = 0.048
MIN_STROKE_PT    = 2.0
MIN_FILLOUT_PT   = 1.0
PT_PER_UNIT      = 846.0 / (2.0 * 12.0)      # StickConfig.ReferencePointsPerWorldUnitApprox
HEAD_R           = 0.22
def W(scale, minpt):
    return max(BASE_STROKE * scale, minpt / PT_PER_UNIT) / (HEAD_R * scale)
W075     = W(0.75, MIN_STROKE_PT)
WOUT075  = W(0.75, MIN_FILLOUT_PT)
W060     = W(0.60, MIN_STROKE_PT)

# ---------- 슬롯 박스 배율 (§1-1) ----------
# ★ 음성 대조: --control 은 슬롯 박스를 2배로 틀어 교정이 실제로 빨개지는지 본다
CONTROL = "--control" in sys.argv
_c = 2.0 if CONTROL else 1.0
S = {"HEAD": _c*70/64/22.12, "EYES": _c*48/64/22.12, "NECK": _c*54/64/22.12, "BACK": _c*88/64/22.12}
SLOT = dict(clothhat="HEAD", furhat="HEAD", fedora="HEAD", crown="HEAD",
            sunglasses="EYES", roundglasses="EYES", goggles="EYES", monocle="EYES",
            bowtie="NECK", stripedtie="NECK", scarf="NECK", bellnecklace="NECK",
            shortcape="BACK", longcape="BACK", wings="BACK", backpack="BACK")

# ---------- K 블록 파싱 (feat.py 와 같은 방법) ----------
i = src.index("const K = {"); j = src.index("{", i); d = 0
for k in range(j, len(src)):
    if src[k] == "{": d += 1
    elif src[k] == "}":
        d -= 1
        if d == 0: end = k; break
block = src[j+1:end]
items = {}
pos = 0
for m in re.finditer(r"(\w+)\s*:\s*\(\)\s*=>\s*\[", block):
    s0 = m.end() - 1; dd = 0
    for k in range(s0, len(block)):
        if block[k] == "[": dd += 1
        elif block[k] == "]":
            dd -= 1
            if dd == 0: e0 = k; break
    items[m.group(1)] = block[s0+1:e0]

CALL = re.compile(r"\b(B|S|F|H|CB|CF|RB)\s*\(")
def split_calls(body):
    out = []
    for m in CALL.finditer(body):
        pre = body[:m.start()]
        if pre.count("(") != pre.count(")") or pre.count("{") != pre.count("}"): continue
        s0 = m.end() - 1; dd = 0
        for k in range(s0, len(body)):
            if body[k] == "(": dd += 1
            elif body[k] == ")":
                dd -= 1
                if dd == 0: e0 = k; break
        out.append((m.group(1), body[s0+1:e0]))
    return out

# ---------- SVG path -> 점열 (M L H V Q Z 만 쓰인다) ----------
TOK = re.compile(r"([MLHVQZmlhvqz])|(-?\d*\.?\d+)")
def path_points(d, seg=24):
    toks = [(a or b) for a, b in TOK.findall(d)]
    pts = []; i = 0; cur = (0.0, 0.0); start = None; cmd = None
    def num():
        nonlocal i
        v = float(toks[i]); i += 1; return v
    while i < len(toks):
        t = toks[i]
        if t.isalpha():
            cmd = t; i += 1
            if cmd in "Zz":
                if start: pts.append(start); cur = start
                continue
        if cmd in ("M", "L"):
            x = num(); y = num(); cur = (x, y); pts.append(cur)
            if cmd == "M" and start is None: start = cur
            if cmd == "M": cmd = "L"
        elif cmd == "H":
            x = num(); cur = (x, cur[1]); pts.append(cur)
        elif cmd == "V":
            y = num(); cur = (cur[0], y); pts.append(cur)
        elif cmd == "Q":
            cx = num(); cy = num(); x = num(); y = num()
            x0, y0 = cur
            for n in range(1, seg + 1):
                u = n / seg; v = 1 - u
                pts.append((v*v*x0 + 2*v*u*cx + u*u*x, v*v*y0 + 2*v*u*cy + u*u*y))
            cur = (x, y)
        else:
            i += 1
    return pts

def circle_points(cx, cy, r, seg=64):
    return [(cx + r*math.cos(2*math.pi*k/seg), cy + r*math.sin(2*math.pi*k/seg)) for k in range(seg)]

def rect_points(x, y, w, h, r):
    # rx 를 4분원으로 (근사 아님 — 실제 rounded rect)
    p = []; seg = 8
    corners = [(x+w-r, y+r, -math.pi/2, 0), (x+w-r, y+h-r, 0, math.pi/2),
               (x+r, y+h-r, math.pi/2, math.pi), (x+r, y+r, math.pi, 3*math.pi/2)]
    for cx, cy, a0, a1 in corners:
        for k in range(seg+1):
            a = a0 + (a1-a0)*k/seg
            p.append((cx + r*math.cos(a), cy + r*math.sin(a)))
    return p

NUMS = re.compile(r"^\s*([0-9.\-]+)\s*,\s*([0-9.\-]+)\s*,\s*([0-9.\-]+)(?:\s*,\s*([0-9.\-]+))?")
def piece_points(kind, args):
    if kind in ("B", "S", "F", "H"):
        m = re.match(r"\s*'([^']*)'", args)
        return path_points(m.group(1))
    a = [x for x in re.split(r",", args.split("{")[0]) if x.strip()]
    v = [float(x.strip()) for x in a if re.fullmatch(r"\s*-?[\d.]+\s*", x)]
    if kind in ("CB", "CF"): return circle_points(v[0], v[1], v[2])
    if kind == "RB":         return rect_points(v[0], v[1], v[2], v[3], v[4])
    return []

def bbox(p):
    xs = [q[0] for q in p]; ys = [q[1] for q in p]
    return min(xs), min(ys), max(xs), max(ys)

def inside(pt, poly):
    x, y = pt; n = len(poly); c = False
    j = n - 1
    for i in range(n):
        xi, yi = poly[i]; xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj-xi)*(y-yi)/(yj-yi+1e-15) + xi: c = not c
        j = i
    return c

rows = []
for name, body in items.items():
    s = S[SLOT[name]]
    calls = split_calls(body)
    for idx, (kind, args) in enumerate(calls):
        p = piece_points(kind, args)
        if not p: print("!! 점열 실패", name, idx, kind); sys.exit(2)
        x0, y0, x1, y1 = bbox(p)
        ink = max(x1-x0, y1-y0) * s
        rows.append(dict(item=name, i=idx, kind=kind, ink_R=ink, pts=p, slot=SLOT[name]))

# ================= 교정 =================
print("== 교정 1: 획 예산 (프로덕션 식 재현) ==")
print("   W@0.75      = %.6f R   (문서 0.343864)" % W075)
print("   W_out@0.75  = %.6f R   (문서 0.21818)" % WOUT075)
print("   W@0.60      = %.6f R   (문서 0.429830)" % W060)
assert abs(W075-0.343864) < 1e-5 and abs(WOUT075-0.21818) < 1e-4 and abs(W060-0.429830) < 1e-5

CAL = [("crown", 3, 0.1879), ("crown", 6, 0.2077), ("sunglasses", 3, 0.1390),
       ("roundglasses", 2, 0.1763), ("goggles", 3, 0.1356), ("bellnecklace", 1, 0.1297),
       ("goggles", 1, 0.4747)]
print("\n== 교정 2: §4-2-7 잉크값 7건 ==")
bad = 0
for it, ix, want in CAL:
    r = [x for x in rows if x["item"] == it and x["i"] == ix][0]
    ok = abs(r["ink_R"] - want) < 0.0006
    print("   %-13s[%d] %-3s  실측 %.4f R  문서 %.4f R  %s" % (it, ix, r["kind"], r["ink_R"], want, "OK" if ok else "!!"))
    if not ok: bad += 1
if bad:
    print("교정 실패(%d건) — 이 뒤 숫자 폐기.%s" % (bad, "  <- --control 은 여기서 빨개져야 정상이다" if CONTROL else ""))
    sys.exit(2)
print("   -> 교정 통과.\n")

# ================= 본 측정 A: 배율별 소멸 =================
print("== A. 물리적 픽셀 축 — 조각 91개 잉크 사각형이 1획(W)에 미치는가 ==")
for label, w in (("@0.75 출하", W075), ("@0.60 사용자저장", W060), ("@1.00 최대", W(1.0, MIN_STROKE_PT))):
    dead15 = sum(1 for r in rows if r["ink_R"] < 1.5*w)
    dead10 = sum(1 for r in rows if r["ink_R"] < 1.0*w)
    print("   %-16s  1.5W미만 %2d/91 (%2.0f%%)   1.0W미만 %2d/91 (%2.0f%%)"
          % (label, dead15, 100*dead15/91, dead10, 100*dead10/91))

# ================= 본 측정 B: 하이라이트는 무엇 위에 있는가 =================
print("\n== B. ★ 반증 시험 — 하이라이트 17개는 「바탕화면 위」인가 「자기 아이템 위」인가 ==")
print("   (문서 §3-2-1 은 흰 하이라이트를 '몸(흰 바탕화면) 위 CR 1.00:1'로 판정해 17개 전부 몸에서 배제했다)")
on_body_piece = 0; tot_h = 0
for name, body in items.items():
    calls = split_calls(body)
    rs = [r for r in rows if r["item"] == name]
    for idx, (kind, args) in enumerate(calls):
        if kind != "H": continue
        tot_h += 1
        hp = rs[idx]["pts"]
        # 이 하이라이트보다 앞서(아래에) 그려진 채움 조각들
        under = [rs[k]["pts"] for k, (kk, _) in enumerate(calls)
                 if k < idx and kk in ("B", "CB", "RB", "F", "CF")]
        cov = sum(1 for q in hp if any(inside(q, u) for u in under))
        frac = cov / len(hp)
        mark = "덮임" if frac >= 0.9 else ("부분" if frac > 0.1 else "★노출")
        if frac >= 0.9: on_body_piece += 1
        print("   %-13s H  자기 아이템 채움 위 점 비율 %5.1f%%  -> %s" % (name, 100*frac, mark))
print("   ---------------------------------------------------------------")
print("   ★ 하이라이트 %d개 중 %d개(%.0f%%)가 '자기 아이템의 불투명 채움' 위에 그려진다."
      % (tot_h, on_body_piece, 100*on_body_piece/tot_h))
print("   ⇒ 바탕화면과 접하는 하이라이트는 %d개다. 'CR 1.00:1(흰 바탕화면 위)'은 이 조각들의 실제 배경이 아니다."
      % (tot_h - on_body_piece))

# ================= 본 측정 C: 알파 조각의 배경도 같은 질문 =================
print("\n== C. 부분 투명 조각 47개의 배경 — 평탄화(pre-flatten)로 불투명하게 바꿀 수 있는가 ==")
flat_ok = 0; flat_no = 0; detail = []
for name, body in items.items():
    calls = split_calls(body)
    rs = [r for r in rows if r["item"] == name]
    for idx, (kind, args) in enumerate(calls):
        has_alpha = ("fillOpacity" in args or "strokeOpacity" in args or kind in ("F", "H")
                     or (kind in ("B", "CB", "RB") and "fill:" not in args))
        if not has_alpha: continue
        p = rs[idx]["pts"]
        under = [rs[k]["pts"] for k, (kk, aa) in enumerate(calls)
                 if k < idx and kk in ("B", "CB", "RB", "F", "CF")]
        cov = sum(1 for q in p if any(inside(q, u) for u in under)) / len(p)
        if cov >= 0.9: flat_ok += 1
        else:
            flat_no += 1
            detail.append((name, idx, kind, round(100*cov)))
print("   배경이 '자기 아이템의 다른 조각'이라 평탄화 가능 : %d" % flat_ok)
print("   배경이 바탕화면이라 평탄화 불가(= 알파를 버려야) : %d" % flat_no)
for x in detail: print("       %-13s[%d] %-3s 덮임 %d%%" % x)
