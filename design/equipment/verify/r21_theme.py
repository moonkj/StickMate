# -*- coding: utf-8 -*-
"""R21 — 테마 배정 (design-equipment, 2026-09-05). 코디네이터 추가 과제: 사용자 확정 「세트 추가 능력 1.0 포함」.

  · 인계본 16종의 theme 은 원문(docs/handoff/.../equipment-screen.dc.html CATS 블록)을 **파싱**한다 — 손으로 옮기지 않는다.
  · 제약은 design-systems R15 §21-4-c(DS-G7′): E1 각 테마 = 스탯 4슬롯에 정확히 1종 · E2 1일차 무료 4종(각 슬롯 idx0) 같은 테마 ·
    E3 전설 4종(idx5: 밀짚모자·안대·반다나·요정날개) 같은 테마 · E4 나머지 16종(idx1~4) 조형 자유.
  · 슬롯 idx/전설 판정은 §21-2-d 표(requiredLevel 순, 슬롯당 일반2·희귀2·영웅1·전설1) — 여기서는 idx 를 그 표 그대로 적고
    r20_model.REST / palette ITEMS 의 id 와 대조해 오타를 막는다.

    python3 r21_theme.py > r21_theme.out.txt
"""
import os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import handoff
import palette_model as PM

THEMES = ["ink", "sport", "office", "cyber", "mil", "neon"]
THEME_KO = {"office": "오피스 워커", "cyber": "사이버 아포칼립스", "neon": "네온 낙서", "sport": "스포츠 이펙트", "ink": "컬러 잉크", "mil": "밀리터리"}
SLOTS = ["HEAD", "EYES", "NECK", "BACK"]

# ---- 1. 인계본 원문 파싱 ------------------------------------------------------------------
HTML = os.path.abspath(os.path.join(HERE, "../../../docs/handoff/design_handoff_equipment_window/reference/equipment-screen.dc.html"))
KIND_TO_ID = {"clothhat": "equip.head.cap", "furhat": "equip.head.fur", "fedora": "equip.head.fedora", "crown": "equip.head.crown",
              "sunglasses": "equip.eyes.sunglasses", "roundglasses": "equip.eyes.round", "goggles": "equip.eyes.goggles", "monocle": "equip.eyes.monocle",
              "bowtie": "equip.neck.bowtie", "stripedtie": "equip.neck.striped", "scarf": "equip.neck.scarf", "bellnecklace": "equip.neck.bell",
              "shortcape": "equip.shoulders.cape", "longcape": "equip.shoulders.long_cape", "wings": "equip.shoulders.wings", "backpack": "equip.shoulders.backpack"}
def parse_handoff():
    txt = open(HTML, encoding="utf-8").read()
    i = txt.index("const CATS = ["); j = txt.index("const RAR_A", i)
    out = {}
    for m in re.finditer(r"\{\s*id:\s*'(\w+)',\s*name:\s*'([^']+)',\s*kind:\s*'(\w+)'(.*?)\}", txt[i:j]):
        hid, name, kind, rest = m.groups()
        theme = re.search(r"theme:\s*'(\w+)'", rest).group(1)
        dlc = "dlc: true" in rest
        out[KIND_TO_ID[kind]] = dict(hid=hid, name=name, kind=kind, theme=theme, dlc=dlc)
    assert len(out) == 16, len(out)
    return out
HANDOFF = parse_handoff()

# ---- 2. 스탯 4슬롯 24종 — idx(§21-2-d) · 등급(파생) ---------------------------------------------
STAT_ITEMS = [  # (id, 한글, 슬롯, idx, 등급)
    ("equip.head.cap", "천모자", "HEAD", 0, "일반"), ("equip.head.fur", "털모자", "HEAD", 1, "일반"), ("equip.head.fedora", "중절모", "HEAD", 2, "희귀"),
    ("equip.head.crown", "왕관", "HEAD", 3, "희귀"), ("equip.head.beret", "베레모", "HEAD", 4, "영웅"), ("equip.head.straw", "밀짚모자", "HEAD", 5, "전설"),
    ("equip.eyes.sunglasses", "선글라스", "EYES", 0, "일반"), ("equip.eyes.round", "동그란안경", "EYES", 1, "일반"), ("equip.eyes.goggles", "고글", "EYES", 2, "희귀"),
    ("equip.eyes.monocle", "외알안경", "EYES", 3, "희귀"), ("equip.eyes.browline", "뿔테안경", "EYES", 4, "영웅"), ("equip.eyes.patch", "안대", "EYES", 5, "전설"),
    ("equip.neck.bowtie", "나비넥타이", "NECK", 0, "일반"), ("equip.neck.striped", "줄무늬타이", "NECK", 1, "일반"), ("equip.neck.scarf", "목도리", "NECK", 2, "희귀"),
    ("equip.neck.bell", "방울목걸이", "NECK", 3, "희귀"), ("equip.neck.pendant", "펜던트", "NECK", 4, "영웅"), ("equip.neck.bandana", "반다나", "NECK", 5, "전설"),
    ("equip.shoulders.cape", "짧은망토", "BACK", 0, "일반"), ("equip.shoulders.long_cape", "긴망토", "BACK", 1, "일반"), ("equip.shoulders.wings", "날개", "BACK", 2, "희귀"),
    ("equip.shoulders.backpack", "배낭", "BACK", 3, "희귀"), ("equip.shoulders.poncho", "판초", "BACK", 4, "영웅"), ("equip.shoulders.fairy_wings", "요정날개", "BACK", 5, "전설"),
]
_pal_ids = {it[0] for it in PM.ITEMS}
for iid, *_ in STAT_ITEMS: assert iid in _pal_ids, iid
SLOT_OF = {iid: s for iid, _, s, _, _ in STAT_ITEMS}; KO = {iid: k for iid, k, _, _, _ in STAT_ITEMS}; IDX = {iid: i for iid, _, _, i, _ in STAT_ITEMS}
LOOK_ITEMS = [it[0] for it in PM.ITEMS if it[2] in ("HAIR", "FX", "PET")]
assert len(LOOK_ITEMS) == 18

# ---- 3. 안 ----------------------------------------------------------------------------------------
FIXED = {iid: d["theme"] for iid, d in HANDOFF.items()}          # 인계본 원문 16종
MINE_STAT = [iid for iid, *_ in STAT_ITEMS if iid not in FIXED]   # 내가 배정하는 스탯 슬롯 8종
assert len(MINE_STAT) == 8

# 안 0 — 인계본 16 그대로 + 8종을 빈칸에 최대한 채운 것(참고용: E1/E2/E3 위반의 실체를 숫자로 보인다)
OPT0 = dict(FIXED); OPT0.update({"equip.head.straw": "mil", "equip.eyes.patch": "sport", "equip.neck.bandana": "ink", "equip.shoulders.fairy_wings": "ink",
                                 "equip.head.beret": "neon", "equip.eyes.browline": "neon", "equip.neck.pendant": "cyber", "equip.shoulders.poncho": "sport"})
# 안 A — 최소 변경 3건: 천모자 ink→mil · 나비넥타이 office→mil · 외알안경 ink→neon
OPT_A = dict(FIXED); OPT_A.update({"equip.head.cap": "mil", "equip.neck.bowtie": "mil", "equip.eyes.monocle": "neon"})
OPT_A.update({"equip.head.straw": "ink", "equip.eyes.patch": "ink", "equip.neck.bandana": "ink", "equip.shoulders.fairy_wings": "ink",   # E3 전설 = ink
              "equip.head.beret": "neon", "equip.eyes.browline": "sport", "equip.neck.pendant": "cyber", "equip.shoulders.poncho": "sport"})
# 안 B(권고) — 4건: A + 고글 cyber→sport(스키 고글) · 외알안경 → cyber(HUD 렌즈) · 뿔테 → neon(스트리트)
OPT_B = dict(OPT_A); OPT_B.update({"equip.eyes.goggles": "sport", "equip.eyes.monocle": "cyber", "equip.eyes.browline": "neon"})
OPTIONS = [("안 0 (인계본 16 그대로 + 8종 최선 채움 — 참고)", OPT0), ("안 A (최소 변경 3건)", OPT_A), ("안 B (권고 · 변경 4건)", OPT_B)]

def check(assign):
    """E1~E3 검산. 반환 (표, 위반 목록, 인계본 대비 변경 목록)."""
    table = {t: {s: [] for s in SLOTS} for t in THEMES}
    for iid, *_ in STAT_ITEMS: table[assign[iid]][SLOT_OF[iid]].append(KO[iid])
    viol = []
    for t in THEMES:
        for s in SLOTS:
            n = len(table[t][s])
            if n != 1: viol.append("E1 %s/%s = %d종 (%s)" % (t, s, n, ", ".join(table[t][s]) or "빈칸"))
    day1 = {assign[iid] for iid, *_ in STAT_ITEMS if IDX[iid] == 0}
    if len(day1) != 1: viol.append("E2 1일차 무료 4종 테마 %s (같아야 한다)" % sorted(day1))
    leg = {assign[iid] for iid, *_ in STAT_ITEMS if IDX[iid] == 5}
    if len(leg) != 1: viol.append("E3 전설 4종 테마 %s (같아야 한다)" % sorted(leg))
    changes = [(KO[iid], FIXED[iid], assign[iid]) for iid in FIXED if assign[iid] != FIXED[iid]]
    return table, viol, changes, (sorted(day1), sorted(leg))

CHOSEN = OPT_B
def theme_of(iid):
    """coords 헤더용: 스탯 24종은 안 B 값, 외형 18종은 무소속(세트 계산 밖)."""
    return CHOSEN.get(iid, "none")

def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 0. 인계본 16종 theme — 원문 파싱(%s) ==" % os.path.relpath(HTML, os.path.dirname(HERE)))
    for iid, d in HANDOFF.items(): w("   %-30s %-3s %-8s %-12s %s%s" % (iid, d["hid"], d["name"], d["kind"], d["theme"], "  (DLC)" if d["dlc"] else ""))
    w()
    w("== 1. 인계본 16만으로 본 6×4 표 — 채워진 칸과 빈칸 ==")
    table = {t: {s: [] for s in SLOTS} for t in THEMES}
    for iid, t in FIXED.items(): table[t][SLOT_OF[iid]].append(KO[iid])
    for t in THEMES:
        w("   %-6s %s" % (t, " | ".join("%s %-12s" % (s, ",".join(n for n in table[t][s]) if table[t][s] else "빈칸") for s in SLOTS)))
    empties = sum(1 for t in THEMES for s in SLOTS if not table[t][s]); dup = sum(1 for t in THEMES for s in SLOTS if len(table[t][s]) > 1)
    w("   빈칸 %d · 2종 겹침 칸 %d (office/NECK: 나비넥타이·줄무늬타이) · 내가 배정할 스탯 슬롯 아이템 %d — 빈칸보다 1개 적다" % (empties, dup, len(MINE_STAT)))
    d1 = [(KO[iid], FIXED[iid]) for iid, *_ in STAT_ITEMS if IDX[iid] == 0]
    w("   1일차 무료 idx0 4종의 인계본 테마: %s → E2(같은 테마) 를 인계본 자신이 만족하지 않는다" % ", ".join("%s=%s" % p for p in d1))
    w("   전설 idx5 4종(밀짚모자·안대·반다나·요정날개)은 전부 내 몫 — E3 는 「4슬롯이 전부 빈 테마」가 있어야 성립하는데, 16종이 6테마 전부에 ≥2칸을 점유해 그런 테마가 없다")
    w("   ⇒ ★ 인계본 16종을 한 종도 안 바꾸면 E1·E2·E3 를 동시에 만족하는 배정은 존재하지 않는다(아래 안 0 이 그 실체).")
    w()
    for label, opt in OPTIONS:
        table, viol, changes, (day1, leg) = check(opt)
        w("== %s ==" % label)
        for t in THEMES:
            w("   %-6s %-10s %s" % (t, THEME_KO[t], " | ".join("%s %-12s" % (s, ",".join(table[t][s]) if table[t][s] else "빈칸") for s in SLOTS)))
        w("   인계본 대비 변경 %d건: %s" % (len(changes), "; ".join("%s %s→%s" % c for c in changes) or "없음"))
        w("   1일차 무료 테마 %s · 전설 테마 %s" % (day1, leg))
        w("   위반 %d건%s" % (len(viol), (": " + " / ".join(viol)) if viol else " — E1(24/24 정확히 1종) · E2 · E3 전부 통과"))
        w()
    w("== 4. 채택 후보 = 안 B. 근거(조형·미감) ==")
    w("   ink(컬러 잉크) = 전설 세트(E3): 밀짚모자·안대·반다나·요정날개 — 「색」이 주제라 어떤 조형도 품는 테마. 재질색: Canvas/Felt/TintNeck/Paper(따뜻 2 + 파랑 2)")
    w("   mil(밀리터리) = 1일차 무료 세트(E2): 천모자(필드캡)·선글라스(항공)·나비넥타이(정복 나비타이 — ★ 가장 어색한 1종, E2 의 대가)·짧은망토(망토)")
    w("   sport = 털모자(비니)·고글(스키)·목도리·판초(야외 우비)   |   office = 중절모·동그란안경·줄무늬타이·배낭 (인계본 그대로)")
    w("   cyber = 왕관(DLC)·외알안경(HUD 단안 렌즈)·펜던트(데이터 펜던트)·긴망토   |   neon(네온 낙서) = 베레모(화가)·뿔테안경(스트리트)·방울목걸이·날개(DLC)")
    w("   안 A 는 변경 1건 적지만 외알안경이 neon, 뿔테가 sport, 고글이 cyber 에 남아 3종의 미감이 어긋난다 — 리더 판단.")
    w()
    w("== 5. 외형 18종(HAIR/FX/PET) — theme = none(무소속, 세트 계산 밖) ==")
    w("   근거: E1 의 24칸은 스탯 4슬롯뿐이고(§21-2-g 외형 18종 스탯 기여 0), DS-4′-b 가 「무소속」을 실재 테마와 다른 값으로 요구한다. 팩 색 연동(파티클) 용 flavour 는 design-art/product-strategy 와 함께 — 여기서 정하지 않는다.")
    for iid in LOOK_ITEMS: w("   %-24s none" % iid)
    w()
    w("== 6. 42종 theme 값(안 B) — coder-systems 전사용 ==")
    for iid, ko, s, idx, rar in STAT_ITEMS: w("   %-30s %-6s idx%d %-3s %-8s %s" % (iid, s, idx, rar, ko, CHOSEN[iid]))
    for iid in LOOK_ITEMS: w("   %-30s %-6s      -   %-8s none" % (iid, [it for it in PM.ITEMS if it[0] == iid][0][2], [it for it in PM.ITEMS if it[0] == iid][0][1]))

if __name__ == "__main__":
    report()
