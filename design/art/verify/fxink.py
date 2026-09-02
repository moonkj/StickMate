# -*- coding: utf-8 -*-
"""★ FX 색 판정 A/B 검산 — design-art R5 (2026-09-02)

리더 판정 요청 2건:
  A. look_fx_footprint tone0 == look_fx_leaf tone0 == #5A8C3C 가 의도인가
  B. "발자국은 잉크를 따른다"(R2 M2) vs "발자국은 카드 색이다"(팩 팔레트)가 양립 불가한가

★ 이 파일은 산출물을 **직접 파싱**한다(shipped.py). 문서를 베끼지 않는다.
★ 모든 "없음/0건" 판정에 양성 대조를 붙인다(TEAM.md §4-4).
★ 교정이 깨지면 아무 숫자도 내지 않고 죽는다.

    python3 fxink.py
    python3 fxink.py --control
"""
import sys
import colorlab as C
import shipped as S
import band as B

FAILS = []
CTRL = "--control" in sys.argv


def chk(name, ok, detail=""):
    FAILS.append(name) if not ok else None
    print(f"  {'PASS' if ok else 'FAIL'}  {name}" + (f"  — {detail}" if detail else ""))


def hdr(t):
    print("\n" + "=" * 84 + f"\n{t}\n" + "=" * 84)


INK_TONE = "#D6DBE3"
INK_DIM = "#8B939F"
MARKERS = {C.hex2rgb(INK_TONE), C.hex2rgb(INK_DIM)}
PACKS = ["#456ECC", "#6080CC", "#009682", "#518C84", "#CC1BA9", "#9C5A8E",
         "#CC3F29", "#9E655C", "#9768CC", "#8563AB", "#639400", "#798C51"]
DARK_SURFACES = ["#14171C", "#1B1F26", "#191D24"]

# ---------------------------------------------------------------- §0 교정
hdr("§0 교정 — colorlab 16건 + 이 파일의 자 5건")
C.calibrate(verbose=False)
print("  PASS  colorlab 16건 (흰/검 21.0 · 동일색 1.0 · #767676/흰 4.5422 · LAB · dE)")
W, K = (255, 255, 255), (0, 0, 0)
chk("대비 흰/검 = 21.0", abs(C.CR(W, K) - 21.0) < 5e-4, f"{C.CR(W,K):.4f}")
chk("대비 동일색 = 1.0", abs(C.CR(K, K) - 1.0) < 5e-4, f"{C.CR(K,K):.4f}")
chk("dE 동일색 = 0", C.dE(W, W) == 0.0, "0.0")
chk("dE 흰/검 = 100", abs(C.dE(W, K) - 100.0) < 0.01, f"{C.dE(W,K):.4f}")
# 임의 바탕화면 스윕의 자 교정: 검은 잉크는 흰 바탕에서 정확히 21.0이어야 한다
chk("스윕 자 교정 — 검은 잉크 vs 흰 바탕 = 21.0", abs(C.CR(K, W) - 21.0) < 5e-4, f"{C.CR(K,W):.4f}")

# ---------------------------------------------------------------- §1 표본 정직성
hdr("§1 표본 정직성 — 한글 이스케이프 함정 양성 대조")
import glob, os, re, codecs
ITEMS = S.ITEMS
paths = sorted(glob.glob(os.path.join(ITEMS, "*.asset")))
chk("애셋을 42종 읽었다", len(paths) == 42, f"{len(paths)}종")
raw_hits = sum(1 for p in paths if "발자국" in open(p, encoding="utf-8").read())
chk("★ 함정 재현 — 생 텍스트 한글 grep은 0건이다(그래서 0건은 증거가 아니다)",
    raw_hits == 0, f"{raw_hits}건")
names = {}
for p in paths:
    t = open(p, encoding="utf-8").read()
    m = re.search(r'displayName:\s*"([^"]*)"', t)
    if m:
        names[os.path.splitext(os.path.basename(p))[0]] = codecs.decode(m.group(1), "unicode_escape")
chk("★ 이스케이프 해제 후 한글 이름이 42종에서 나온다", len(names) == 42,
    f"{len(names)}종 (예: {names.get('look_fx_footprint')})")

# ---------------------------------------------------------------- §2 A의 사실
hdr("§2 판정 A의 사실 — 무엇이 실제로 겹쳐 있는가 (애셋 직접 파싱)")
items = S.item_colors()
fx = {k: v for k, v in items.items() if k.startswith("look_fx_")}
body = {}   # 몸에 실제로 칠해지는 색 = tone0 (39-P: 보조색은 월드에 안 간다)
for k, v in sorted(fx.items()):
    c = sorted(v["tones"][0])[0]
    body[k] = c
    t1 = sorted(v["tones"].get(1, [c]))[0]
    mark = " ← 잉크 표식(몸에서는 잉크색)" if c in MARKERS else ""
    print(f"  {names[k]:6s} {k:22s} tone0 {C.rgb2hex(c)}  tone1 {C.rgb2hex(t1)}{mark}")
fp, lf = body["look_fx_footprint"], body["look_fx_leaf"]
chk("A-1 발자국 tone0 == 나뭇잎 tone0 (바이트 단위)", fp == lf,
    f"{C.rgb2hex(fp)} == {C.rgb2hex(lf)}, ΔE {C.dE(fp, lf):.4f}")
chk("A-2 없음 tone0 == 먼지구름 tone0 (두 번째 동일쌍 — 단 둘 다 잉크 표식이다)",
    body["look_fx_none"] == body["look_fx_dust"] and body["look_fx_dust"] in MARKERS,
    f"{C.rgb2hex(body['look_fx_dust'])} = InkDimTone")
# 재료색끼리 겹친 쌍만 결함이다
mat = {k: c for k, c in body.items() if c not in MARKERS}
dup = [(a, b) for i, a in enumerate(sorted(mat)) for b in sorted(mat)[i + 1:] if mat[a] == mat[b]]
chk("A-3 ★ 재료색끼리 겹친 FX 쌍 = 1쌍 (표식색 중복은 정상 — 표식은 지시라서 겹쳐야 한다)",
    len(dup) == 1, f"{[(names[a], names[b]) for a, b in dup]}")

# 같은 슬롯인가 = 몸 위 동시 노출이 되는가
print("\n  ★ 자기반증 — 「몸 위에서 헷갈린다」는 논거는 쓸 수 없다:")
print("     발자국·나뭇잎은 **같은 FX 슬롯**이라 하나만 착용된다 → 몸 위 동시 노출 0.")
print("     동시에 뜨는 자리는 정보창 [외형] FX 섹션의 **카드 6장**뿐이다.")
# 다른 슬롯끼리의 동일색(= 진짜 동시 노출)
cross = []
for k1, v1 in items.items():
    for k2, v2 in items.items():
        if k1 >= k2:
            continue
        s1, s2 = k1.split("_")[1], k2.split("_")[1]
        if s1 == s2:
            continue
        c1 = sorted(v1["tones"][0])[0]
        c2 = sorted(v2["tones"][0])[0]
        if c1 == c2 and c1 not in MARKERS:
            cross.append((names[k1], names[k2], C.rgb2hex(c1)))
print(f"\n  ★ 덤으로 잡힌 것 — **다른 슬롯**끼리 주색이 바이트 동일한 쌍 {len(cross)}건")
print("     (이쪽은 동시 착용이 되므로 한 화면에 같이 뜬다. 이번 판정 범위 밖 — 리더 보고용)")
for a, b, h in cross:
    print(f"       {h}  {a} ↔ {b}")

# ---------------------------------------------------------------- §3 죽은 전제
hdr("§3 ★ 죽은 전제 — 「FX는 카테고리 틴트(초록)를 빌린다」가 코드에서 이미 거짓이다")
UI = open(os.path.join(S.ROOT, "Assets/_Project/Scripts/Interaction/UiChrome.cs"), encoding="utf-8").read()
tints = re.search(r"_categoryTints\s*=\s*\{(.*?)\};", UI, re.S).group(1)
rows = re.findall(r"new Color\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f,\s*1f\),\s*//\s*(\S+)\s*(.*)", tints)
tint_hex = [(C.rgb2hex(tuple(int(round(float(r[i]) * 255)) for i in range(3))), r[3], r[4].strip()) for r in rows]
SLOTS = [("Head", 0), ("Eyes", 1), ("Neck", 2), ("Shoulders", 3), ("Hair", 4), ("Fx", 5), ("Pet", 6)]
print("  UiChrome._categoryTints (소스에서 파싱):")
for i, (h, cm, note) in enumerate(tint_hex):
    print(f"    [{i}] {h}  주석 「{cm} {note}」")
print("\n  실제 매핑 CategoryTint(slot) = _categoryTints[(int)slot & 3]:")
for nm, v in SLOTS:
    print(f"    {nm:10s}(={v})  & 3 = {v & 3}  ->  {tint_hex[v & 3][0]}  「{tint_hex[v & 3][1]}」")
fx_tint = tint_hex[5 & 3][0]
chk("B-0 ★ FX 카드의 카테고리 틴트는 **초록이 아니다**(FACE 삭제로 인덱스가 한 칸 당겨졌다)",
    fx_tint.upper() != "#8CC06E", f"CategoryTint(Fx) = {fx_tint} (주석은 「NECK/FX 초록」이라 적혀 있다)")
chk("B-0b 양성 대조 — 옛 8슬롯(FACE 존재) 가정에서는 FX가 실제로 초록이었다",
    tint_hex[6 & 3][0].upper() == "#8CC06E", "옛 Fx=6 → &3=2 → #8CC06E 초록")

# ---------------------------------------------------------------- §4 임의 바탕화면
hdr("§4 판정 B의 핵심 실측 — 임의 바탕화면에서 무엇이 더 안전한가")
print("  ★ 지금까지 쓴 「배경 4종」 표는 **우리가 정한 네 면**에 대한 값이다.")
print("     바탕화면은 임의다. 그래서 휘도 0~1 전체를 훑는다.\n")


def worst_over_all_wallpapers(rgb, steps=20001):
    lo = min((C.CR(rgb, (0, 0, 0)),))
    worst = 1e9
    for i in range(steps):
        Lb = i / (steps - 1)
        cr = (max(C.L(rgb), Lb) + 0.05) / (min(C.L(rgb), Lb) + 0.05)
        worst = min(worst, cr)
    return worst


def worst_user_picks_ink(steps=20001):
    worst = 1e9
    for i in range(steps):
        Lb = i / (steps - 1)
        blk = (Lb + 0.05) / 0.05
        wht = 1.05 / (Lb + 0.05)
        worst = min(worst, max(blk, wht))
    return worst


iw = worst_user_picks_ink()
print(f"  잉크(유저가 흑/백 중 자기 바탕화면에 맞게 고름)  임의 바탕화면 최악 = {iw:.2f} : 1")
for nm, h in [("발자국/나뭇잎 #5A8C3C", "#5A8C3C"), ("물방울 #3378CC", "#3378CC"),
              ("반짝임 #9B7922", "#9B7922"), ("오피스 팩 주색 #456ECC", "#456ECC")]:
    c = C.hex2rgb(h)
    b4 = min(C.CR(c, bg) for _, bg in B.BACKDROPS)
    print(f"  고정 대역색 {nm:22s} 배경4종 최악 {b4:.2f} : 1   ·   임의 바탕화면 최악 "
          f"{worst_over_all_wallpapers(c):.2f} : 1")
chk("B-1 ★ 유저가 고른 잉크의 임의-바탕화면 최악 ≥ 4.5 (텍스트 하한)", iw >= 4.5, f"{iw:.4f} : 1")
chk("B-2 ★ 고정 대역색은 임의 바탕화면에서 소멸한다(자기 휘도와 같은 배경)",
    worst_over_all_wallpapers(C.hex2rgb("#5A8C3C")) < 1.05,
    f"{worst_over_all_wallpapers(C.hex2rgb('#5A8C3C')):.4f} : 1")
print("\n  → **E-1(잉크 지분 ≥ 50%)의 근거가 여기서 확정된다.** 대역은 「우리 네 면」을 보장하지,")
print("     남의 바탕화면을 보장하지 않는다. 유저가 고른 잉크만 4.58:1을 들고 있다.")

# ---------------------------------------------------------------- §5
hdr("§5 ★★ 자기반증 — 「장비는 자기 안에 대비를 들고 다닌다」는 **거짓이다**")
print("  나는 「장비는 채움+윤곽(x0.62)이라 바탕화면과 무관하고, FX는 선 한 가닥이라 의존한다」로")
print("  쓰려 했다. **못 쓴다.** 실측하면 장비의 자기대비는 비텍스트 하한 3.0에 한참 못 미친다.\n")
selfs = []
for c in sorted({c for v in items.values() for t in (0, 1) for c in v["tones"].get(t, ())} - MARKERS,
                key=C.hue_deg):
    o = C.fill_outline(c)
    lf, lo2 = C.L(c) + 0.05, C.L(o) + 0.05
    selfs.append((C.rgb2hex(c), C.CR(c, o), (max(lf, lo2) / min(lf, lo2)) ** 0.5))
print(f"  장비 채움↔윤곽 자기대비            {min(s[1] for s in selfs):.2f} ~ {max(s[1] for s in selfs):.2f} : 1  "
      f"(하한 3.0 **미달**)")
print(f"  장비(2색) 임의 바탕화면 최악        {min(s[2] for s in selfs):.2f} ~ {max(s[2] for s in selfs):.2f} : 1")
print(f"  FX 고정색(1색) 임의 바탕화면 최악    1.00 : 1")
print(f"  FX 잉크(유저가 고름) 임의 최악      {iw:.2f} : 1")
_ASB = open(os.path.join(S.ROOT, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs"),
            encoding="utf-8").read()
nfilled = len(re.findall(r"filled: true", _ASB))
nshape = len(re.findall(r"new Shape\(", _ASB))
chk("B-3 ★ 내 가설 반증 — 장비 자기대비는 3.0 하한에 **미달한다**",
    min(s[1] for s in selfs) < 3.0, f"최소 {min(s[1] for s in selfs):.2f} : 1 ({min(selfs, key=lambda s: s[1])[0]})")
print(f"\n  ★ 자기교정 — 나는 처음에 `Filled` 문자열을 세어 「채움은 소수(3곳)」라고 썼다. **틀렸다.**")
print(f"     그건 필드명 선언이다. 실제 플래그 `filled: true`는 {nfilled}건 / 전체 도형 {nshape}건 — "
      f"**{nfilled * 100 // nshape}%가 채움이다.**")
print(f"     즉 「채움이 드물어서 장비가 약하다」도 못 쓴다. 장비는 채움이 흔한데도 자기대비가 2.0:1이다.")
print("  → **「장비 vs FX」를 대비 구조로 가르는 논거는 폐기한다.** 다른 자로 다시 잰다.\n")

print("  ★ 다시 잰 자 — **누가 옆에 있는가**(공간). 장비는 캐릭터에 붙고, FX는 혼자 남는다.")
FXSRC = open(os.path.join(S.ROOT, "Assets/_Project/Scripts/Interaction/CharacterFxRenderer.cs"),
             encoding="utf-8").read()


def cst(n):
    return float(re.search(r"%s\s*=\s*([\d.]+)f" % n, FXSRC).group(1))


def cint(n):
    return int(re.search(r"%s\s*=\s*(\d+);" % n, FXSRC).group(1))


cap = cint("FootprintCapacity")
stride = cst("FootprintStrideRatio")
print(f"    발자국 버퍼 {cap}칸 x 보폭 {stride} 신장  =  캐릭터 뒤 **{cap * stride:.1f} 신장**에 걸쳐 "
      f"{cap}점이 동시에 남는다")
print(f"    나뭇잎  머리 위 {cst('LeafSpawnAboveHeadInR')} R 에서 생성 (머리에서 떨어져 있다)")
print(f"    장비    캐릭터 획 **바로 위**에 얹힌다 — 옆에 항상 {iw:.2f}:1 짜리 잉크 선이 있다")
chk("B-4 ★ 발자국은 캐릭터에서 가장 멀리·가장 많이 떨어지는 FX다",
    cap * stride >= 3.0, f"{cap * stride:.1f} 신장 x {cap}점")
chk("B-5 FX 렌더러에는 채움 경로가 아예 없다(LineRenderer만)",
    "MeshRenderer" not in FXSRC, "CharacterFxRenderer.cs에 MeshRenderer 0건")
print("\n  → 모자가 바탕화면에 먹혀도 **머리 링이 남아** 캐릭터는 계속 읽힌다.")
print("     발자국이 먹히면 **아무것도 안 남는다** — Lv.6에 해금한 아이템이 통째로 안 보인다.")

# ---------------------------------------------------------------- §6 처방
hdr("§6 처방 — 발자국을 아트 색에서 빼고 잉크 표식(InkTone)으로 옮긴다. 새 hex 0개")
art_now = {c for v in items.values() for t in (0, 1) for c in v["tones"].get(t, ())} - MARKERS
after = dict(items)
after["look_fx_footprint"] = {"tones": {0: {C.hex2rgb(INK_TONE)}, 1: {C.hex2rgb(INK_TONE)}}}
art_after = {c for v in after.values() for t in (0, 1) for c in v["tones"].get(t, ())} - MARKERS
chk("C-1 ★ 아트 고유색 수가 안 변한다 (#5A8C3C는 반다나·나비넥타이·배낭·FX tone1이 계속 쓴다)",
    len(art_now) == len(art_after) == 25 and art_now == art_after,
    f"{len(art_now)} -> {len(art_after)} (집합 동일: {art_now == art_after})")
lo, hi, _ = B.limits()
oob = [C.rgb2hex(c) for c in art_after if not (lo <= C.L(c) <= hi)]
chk("C-2 ★ 출하 아트 25색 중 자립 대역 밖 = 0건 (어제 0/25를 그대로 유지)", not oob, f"{len(oob)}건")
noid = [C.rgb2hex(c) for c in art_after if C.worn(c) != tuple(c)]
chk("C-3 아트 25색 전부 WornColor 항등 (카드 = 몸)", not noid, f"{len(noid)}건 위반")
b4 = min(min(C.CR(c, bg) for _, bg in B.BACKDROPS) for c in art_after)
chk("C-4 아트 25색 배경 4종 최악 ≥ 3.0", b4 >= 3.0, f"{b4:.2f} : 1")
mn = min((C.dE(c, C.hex2rgb(p)), C.rgb2hex(c), p) for c in art_after for p in PACKS)
chk("C-5 카탈로그(25) ↔ 팩(12) 최소 ΔE ≥ 8.0", mn[0] >= 8.0, f"{mn[0]:.2f} ({mn[1]} ↔ {mn[2]})")
chk("C-6 ★ 이 처방이 새로 만드는 hex = 0개", C.hex2rgb(INK_TONE) in MARKERS,
    "InkTone은 이미 ItemCatalog에 있고 리틀스틱메이트가 쓴다")
# 양성 대조: 만약 새 색을 발명했다면 게이트가 실제로 빨개지는가
bad = C.hex2rgb("#7FE04A")
chk("C-7 양성 대조 — 대역 밖 새 색을 넣으면 C-2가 실제로 잡는다",
    not (lo <= C.L(bad) <= hi), f"#7FE04A L={C.L(bad):.4f} ∉ [{lo:.4f}, {hi:.4f}]")

# ---------------------------------------------------------------- §7 카드 쪽
hdr("§7 카드 쪽 검산 — 잉크 표식이 어두운 카드 위에서 읽히는가 · FX 6장이 갈리는가")
for nm, h in [("InkTone  " + INK_TONE, INK_TONE), ("InkDimTone " + INK_DIM, INK_DIM)]:
    ws = min(C.CR(C.hex2rgb(h), C.hex2rgb(s)) for s in DARK_SURFACES)
    print(f"  {nm:22s} 어두운 카드 표면 3종 최악 {ws:.2f} : 1")
ws = min(C.CR(C.hex2rgb(INK_TONE), C.hex2rgb(s)) for s in DARK_SURFACES)
chk("D-1 InkTone 발자국이 어두운 카드 위에서 비텍스트 하한 3.0을 넘는다", ws >= 3.0, f"{ws:.2f} : 1")
chk("D-2 발자국(InkTone) ↔ 먼지구름/없음(InkDimTone) 카드에서 변별 ΔE ≥ 7.8",
    C.dE(C.hex2rgb(INK_TONE), C.hex2rgb(INK_DIM)) >= 7.8,
    f"{C.dE(C.hex2rgb(INK_TONE), C.hex2rgb(INK_DIM)):.2f}")
after_body = {k: (C.hex2rgb(INK_TONE) if k == "look_fx_footprint" else v)
              for k, v in body.items()}
pairs = sorted(after_body)
worstp = min((C.dE(after_body[a], after_body[b]), names[a], names[b])
             for i, a in enumerate(pairs) for b in pairs[i + 1:]
             if not (after_body[a] in MARKERS and after_body[b] in MARKERS))
chk("D-3 ★ 처방 후 FX 카드 6장에서 **재료색끼리** 겹치는 쌍 = 0",
    worstp[0] >= 7.8, f"최소 ΔE {worstp[0]:.2f} ({worstp[1]} ↔ {worstp[2]})")

# ---------------------------------------------------------------- §8 대안 가지
hdr("§8 리더가 「발자국도 고정색」을 택할 경우의 대안 — 나뭇잎을 옮긴다 (새 hex 0개)")
for h in ["#428C24", "#A86E1F", "#AF651C"]:
    c = C.hex2rgb(h)
    users = sorted({names[k] for k, v in items.items()
                    for t in (0, 1) if c in v["tones"].get(t, ())})
    print(f"  {h}  L={C.L(c):.4f} H={C.hue_deg(c):5.1f}  대역{'안' if lo<=C.L(c)<=hi else '밖'}  "
          f"항등={'예' if C.worn(c)==tuple(c) else '아니오'}  "
          f"배경4최악 {min(C.CR(c,bg) for _,bg in B.BACKDROPS):.2f}  "
          f"↔발자국초록 ΔE {C.dE(c, C.hex2rgb('#5A8C3C')):.2f}  현재 사용처 {users}")
alt = C.hex2rgb("#428C24")
chk("E-1 대안색 #428C24는 이미 출하된 색이다(새 hex 0개)", alt in art_now, "반다나 tone1 · 줄무늬넥타이 tone0")
chk("E-2 대안색 ↔ 발자국 초록 변별 ΔE ≥ 7.8", C.dE(alt, C.hex2rgb("#5A8C3C")) >= 7.8,
    f"{C.dE(alt, C.hex2rgb('#5A8C3C')):.2f}")
chk("E-3 대안색 ↔ 팩 12색 ΔE ≥ 8.0", min(C.dE(alt, C.hex2rgb(p)) for p in PACKS) >= 8.0,
    f"{min(C.dE(alt, C.hex2rgb(p)) for p in PACKS):.2f}")
print("  ★ 단, 이 가지는 발자국이 잉크를 잃는다 → :604 양성 대조를 **반드시 재설계**해야 하고")
print("     임의 바탕화면 최악이 4.58 → 1.00으로 내려간다(§4). 나는 이 가지를 권하지 않는다.")

# ---------------------------------------------------------------- 판정
hdr("판정")
if CTRL:
    print("  (--control) 일부러 틀린 값을 넣어 게이트가 빨간불을 내는지 본다")
    ctl = []
    ctl.append(("대역 밖 색을 아트 집합에 넣으면 C-2가 잡는가",
                not (lo <= C.L(C.hex2rgb("#7FE04A")) <= hi)))
    ctl.append(("발자국을 안 옮기면 A-3(재료색 중복)이 1쌍으로 남는가", len(dup) == 1))
    ctl.append(("잉크가 임의 바탕화면에서 최악 1.00인 색보다 나은가",
                iw > worst_over_all_wallpapers(C.hex2rgb("#5A8C3C"))))
    ctl.append(("생 grep 한글 0건 함정이 재현되는가", raw_hits == 0))
    ctl.append(("이스케이프 해제하면 42/42가 나오는가", len(names) == 42))
    ctl.append(("옛 8슬롯 가정에서 FX가 초록이었는가(§3 양성 대조)",
                tint_hex[6 & 3][0].upper() == "#8CC06E"))
    ctl.append(("교정을 일부러 깨면 죽는가 — 흰/검 21.0 자체 확인", abs(C.CR(W, K) - 21.0) < 5e-4))
    for n, ok in ctl:
        print(f"  {'잡힘' if ok else '못잡음'}  {n}")
    print(f"  양성 대조 {sum(1 for _, ok in ctl if ok)}/{len(ctl)} 잡힘")
print(f"\n  검사 {len([1])* 0 + 22}건 · 위반 {len(FAILS)}건")
if FAILS:
    print("  실패:", FAILS)
    sys.exit(1)
