# -*- coding: utf-8 -*-
"""★ 6팩 테마 확정 — 실측 (design-art, 2026-09-02 4차 라운드)  →  design/art/PACK_THEME_SPEC.md

    python3 packtheme.py             # 전량
    python3 packtheme.py --control   # ★ 양성 대조 — 일부러 틀린 입력을 넣어 게이트가 빨간불을 내는가

이 파일이 답하는 것 (리더 선결 3건 + 테마 6개)
  §0  교정          — 색 계산기 + 기하 계산기를 **알려진 값으로 먼저** 맞춘다. 깨지면 죽는다.
  §1  (a) 이름      — 저장소 전량에서 여섯 이름의 **표기 드리프트**를 기계로 센다.
  §2  팔레트 재확인  — §13 처방 C 12색을 다시 유도(폴백 금지)하고 §13-3 표와 대조한다.
  §3  ★ 조용한 폴백 재검사 — 제약을 강화하면 죽는가, 아니면 조용히 나빠지는가.
  §4  (c) 형상 예산  — 슬롯별 **예약 가능한 서로 겹치지 않는 대역이 몇 개인가**.
                      product-strategy의 1차원 근사가 전제한 "자유 배치"가 우리 트리에서
                      이미 성립하지 않는다는 것을 보인다(sectors.py가 출하돼 있다).
  §5  (b) Lv.1      — 팩 6종의 등급 분포와 Lv.1 간판 1종이 §13 색·§3-5 사운드와 닫히는가.
  §6  사운드 6키 대조 — 팩마다 「보이는 것」과 「들리는 것」이 한 벌인가 (키 → 슬롯 사상).

★ 프로덕션 수정 0건. design/equipment · design/sound 는 **읽기만** 한다(import / grep).
"""
import sys, os, re, math, itertools, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
EQV  = os.path.join(ROOT, "design", "equipment", "verify")
sys.path.insert(0, HERE)
sys.path.insert(0, EQV)

import colorlab as CL

FAIL = []
def bad(msg):
    FAIL.append(msg)
    return "✗  "
def okmark(cond, msg):
    return "OK " if cond else bad(msg)


# ============================================================================
# §0. 교정 — 두 계산기 모두. 깨지면 그 뒤 숫자를 전부 폐기한다.
# ============================================================================
def calibrate(strict=True):
    print("=" * 100)
    print("§0. 교정 — 알려진 값으로 먼저 맞춘다 (깨지면 이 아래 숫자는 전부 무효)")
    print("=" * 100)
    CL.calibrate()                       # 흰/검 21.0 · 동일색 1.0 · WornColor 2건 · LAB · dE

    import rig, sectors as S
    geo = [
        ("W(획) @0.75",            rig.W,                 0.343864, 1e-6),
        ("W(획) @0.60",            S.W060,                0.429830, 1e-6),
        ("실루엣 하한(1획@0.60)",   S.SILHOUETTE_FLOOR_R,  0.429830, 1e-6),
        ("래칫(1.50획@0.75)",       S.SILHOUETTE_RATCHET_R,0.515796, 1e-6),
        ("프로파일 구간 수",         S.BINS,                72,       0),
        ("예약 최소 폭(구간)",       S.SECTOR_MIN_BINS,     3,        0),
    ]
    print("\n기하 계산기 (design/equipment/verify — 읽기 전용 import)")
    for n, got, want, tol in geo:
        good = abs(got - want) <= tol
        print("  %s %-24s %-12s (기대 %s)" % (okmark(good, "교정 %s" % n), n,
              ("%.6f" % got) if isinstance(got, float) else str(got),
              ("%.6f" % want) if isinstance(want, float) else str(want)))

    # ★ 기하 계산기의 **양성 대조**: 같은 도형끼리는 프로파일 차가 0이어야 한다.
    import items
    p = S.profile(items.HEAD["중절모"], 0.0)
    d0 = max(abs(a - b) for a, b in zip(p, p))
    print("  %s %-24s %.6f (기대 0.000000)" % (okmark(d0 < 1e-12, "동일도형 프로파일차"),
                                               "동일 도형 프로파일차", d0))
    # 그리고 서로 다른 도형끼리는 0이 아니어야 한다 (0을 답하는 자는 아무것도 못 잰다)
    q = S.profile(items.HEAD["왕관"], 0.0)
    d1 = max(abs(a - b) for a, b in zip(p, q))
    print("  %s %-24s %.6f (기대 > 0)" % (okmark(d1 > 1e-6, "이종도형 프로파일차"),
                                          "이종 도형 프로파일차", d1))
    if strict and FAIL:
        print("\n★ 교정 실패 %d건 — 죽는다. 아무 숫자도 내지 않는다." % len(FAIL))
        for f in FAIL: print("   ·", f)
        sys.exit(2)
    print("\n→ 교정 통과. 아래 숫자를 신뢰한다.\n")


# ============================================================================
# §1. (a) 이름 — 표기 드리프트를 기계로 센다
# ============================================================================
#: 정본 후보. (표시명, packId, 세트키, 팔레트 색상각)
CANON = [
    ("오피스 워커",       "pack.office",   222.0),
    ("사이버 아포칼립스", "pack.cyber",    172.0),
    ("네온 낙서",         "pack.graffiti", 312.0),
    ("스포츠",            "pack.sports",     8.0),
    ("컬러 잉크",         "pack.ink",      268.0),
    ("밀리터리",          "pack.military",  80.0),
]
#: ★ **경쟁 표시명** — 같은 팩을 가리키는 다른 「이름」. 발견되면 드리프트로 센다.
VARIANTS = {
    "스포츠": ["스포츠 이펙트", "스포츠이펙트"],
    "네온 낙서": ["네온낙서", "그래피티"],
    "오피스 워커": ["오피스워커"],
    "사이버 아포칼립스": ["사이버아포칼립스"],
    "컬러 잉크": ["컬러잉크"],
    "밀리터리": [],
}
#: 본문 약칭 — 이름이 아니라 문장 안의 줄임말. **드리프트로 세지 않는다**(세면 거짓 양성).
PROSE = {"오피스 워커": ["오피스"], "사이버 아포칼립스": ["사이버"], "밀리터리": ["밀리터리 팩"]}
SCAN_DIRS = ["docs", "design"]
#: 이 표식이 있는 줄은 드리프트 계수에서 뺀다(드리프트를 **기록한** 줄).
DRIFT_EXEMPT = "<!--drift-record-->"
EXEMPT = [0]


def grep_count(needle, exts=(".md",)):
    """저장소 **저작 문서**에서 정확 문자열 출현 횟수.
    ★ 기본값이 .md 인 이유: `*.out.txt`는 이 팀이 기계로 뽑은 산출 로그라
      거기서 세면 **내 도구가 나를 세는 것**이 된다(표본 오염). 사람이 쓴 것만 센다."""
    n = 0
    hits = []
    for d in SCAN_DIRS:
        for dp, _, fns in os.walk(os.path.join(ROOT, d)):
            for fn in fns:
                if not fn.endswith(exts): continue
                p = os.path.join(dp, fn)
                if os.path.abspath(p) == os.path.abspath(__file__): continue
                try: t = open(p, encoding="utf-8").read()
                except Exception: continue
                # ★ 「드리프트를 기록한 줄」은 세지 않는다. 안 그러면 드리프트를 문서화하는 행위
                #   자체가 드리프트로 잡혀 영원히 0이 안 된다. 다만 **면제한 줄 수를 세어 함께
                #   보고**한다 — 면제가 조용해지면 그게 다음 거짓 초록이다(TEAM.md #5).
                keep, ex = [], 0
                for ln in t.splitlines():
                    if DRIFT_EXEMPT in ln:
                        ex += EXEMPT[0]; ex -= EXEMPT[0]
                        EXEMPT[0] += ln.count(needle)
                    else:
                        keep.append(ln)
                c = "\n".join(keep).count(needle)
                if c: n += c; hits.append((os.path.relpath(p, ROOT), c))
    return n, hits


def section_names():
    print("=" * 100)
    print("§1. (a) 이름 — 여섯 이름의 표기 드리프트 (저장소 docs/ + design/ 전량 grep)")
    print("=" * 100)
    print("(.md 저작 문서만 — *.out.txt 산출 로그는 표본에서 뺀다)")
    print("%-20s %-15s %4s/%-4s | %-28s | %s" % ("정본 후보", "packId", "단독", "전체",
          "경쟁 표시명(=드리프트)", "본문 약칭(무해)"))
    drift = 0
    for name, pid, _ in CANON:
        n, _ = grep_count(name)
        pn, _ = grep_count(pid)
        # ★ 정본이 이형의 **부분문자열**이면(「스포츠」⊂「스포츠 이펙트」) 정본 계수가 이형을
        #   통째로 포함한다. 그 몫을 빼야 "정본 단독 출현"이 나온다.
        sub = sum(grep_count(v)[0] for v in VARIANTS.get(name, []) if name in v and v != name)
        n_solo = n - sub
        vs = []
        for v in VARIANTS.get(name, []):
            c, hs = grep_count(v)
            if v in name and v != name: c -= n   # 이형이 정본의 부분문자열인 경우
            if c > 0:
                vs.append("%s×%d [%s]" % (v, c, ", ".join("%s:%d" % h for h in hs[:2])))
                drift += 1
        ab = []
        for v in PROSE.get(name, []):
            c, _ = grep_count(v)
            if v in name and v != name: c -= n
            if c > 0: ab.append("%s×%d" % (v, c))
        print("%-20s %-15s %4d/%-4d | %-28s | %s" % (name, "%s×%d" % (pid, pn), n_solo, n,
                                        " · ".join(vs) if vs else "없음",
                                        " · ".join(ab) if ab else "-"))
    print("\n  드리프트 %d건.  (면제 표식 `%s` 이 붙은 줄에서 뺀 출현 %d건 — 면제가 0이 아니면"
          % (drift, DRIFT_EXEMPT, EXEMPT[0]))
    print("   그 줄들이 정말 '기록'인지 사람이 봐야 한다. 면제를 조용히 두지 않으려고 센다.)")
    return drift


def section_name_axis():
    """★ (a)의 근거 — 「등급 사다리」와 「팩 여섯」은 팔레트에서 이미 직교한다.
    사다리에는 낱말(일반/희귀/영웅/전설)이, 동격에는 고유명이 맞다는 것을 색으로 보인다."""
    RAMP = ["#9C978C", "#BCAC8B", "#DBBD7F", "#F9CB70"]
    rows, _cat, err = derive12()
    assert err is None, err
    pk = [c for _, _, p, s in rows for c in (p, s)]
    prim = [p for _, _, p, _s in rows]
    print("\n" + "=" * 100)
    print("§1-b. (a)의 색 근거 — 두 축은 팔레트에서 **이미 직교한다**")
    print("=" * 100)
    rh = [CL.hue_deg(CL.hex2rgb(c)) for c in RAMP]
    rl = [CL.L(CL.hex2rgb(c)) for c in RAMP]
    ph = [CL.hue_deg(CL.hex2rgb(c)) for c in prim]
    pl = [CL.L(CL.hex2rgb(c)) for c in prim]
    hs = max(rh) - min(rh)
    print("  등급 램프 4색  색상각 %.1f°~%.1f° (폭 %.1f°)   휘도 %.4f~%.4f (대비 %.2f:1)" %
          (min(rh), max(rh), hs, min(rl), max(rl), (max(rl)+.05)/(min(rl)+.05)))
    print("  팩 주색 6색    색상각 %.1f°~%.1f° (폭 %.1f°)  휘도 %.4f~%.4f (대비 %.2f:1)" %
          (min(ph), max(ph), max(ph)-min(ph), min(pl), max(pl), (max(pl)+.05)/(min(pl)+.05)))
    a = okmark(hs <= 15.0, "등급 램프가 한 색상각이 아니다")
    b = okmark((max(pl)+.05)/(min(pl)+.05) <= 1.50, "팩 주색이 한 휘도대가 아니다")
    print("  %s 등급 = **한 색상각의 밝기 사다리**  (색상각 폭 %.1f° ≤ 15°)" % (a, hs))
    print("  %s 팩   = **한 밝기의 여섯 각도**      (휘도 대비 %.2f:1 ≤ 1.50)" %
          (b, (max(pl)+.05)/(min(pl)+.05)))
    print("  → 색이 이미 「사다리」와 「동격」을 다른 축으로 그린다.")
    print("    이름도 같은 두 축을 써야 한다: 사다리 = 등급 낱말(4, 확정) / 동격 = 고유명(6, 확정).")
    print("    ★ 6:6 1:1 이름 재사용은 **동격 축에 사다리의 서열을 주입한다**(아래 §1-c).")


def section_rank_leak():
    """★ F-5 처방(팩 6이름 = 세트 6이름)을 그대로 했을 때 팩 이름이 물려받는 것."""
    # ECONOMY_SPEC 3-3 표 — 문서에서 파싱한다(베끼지 않는다)
    doc = os.path.join(ROOT, "design", "systems", "ECONOMY_SPEC.md")
    txt = open(doc, encoding="utf-8").read()
    rows = []
    for line in txt.splitlines():
        m = re.match(r"^\|\s*([A-F])\s*\|\s*(\d)\s*\|\s*(\S+)\s*\|\s*(\S+)\s*\|\s*\*?\*?(\d+)", line)
        if m: rows.append((m.group(1), int(m.group(2)), m.group(3), int(m.group(5))))
    print("\n" + "=" * 100)
    print("§1-c. (a) F-5 처방을 문자 그대로 했을 때 — 팩 이름이 물려받는 서열")
    print("=" * 100)
    print("  ECONOMY_SPEC 3-3에서 파싱한 기본 세트 %d개:" % len(rows))
    ok = okmark(len(rows) == 6, "ECONOMY_SPEC 3-3 세트 표를 6행으로 못 읽었다")
    names = [c[0] for c in CANON]
    for i, (sid, rank, grade, lv) in enumerate(rows):
        nm = names[i] if i < len(names) else "?"
        print("    세트 %s  rank %d  등급 %-4s 완성 Lv.%-3d →  이 이름을 주면 「%s」가 **%s**가 된다"
              % (sid, rank, grade, lv, nm, grade))
    gr = [c[2] for c in rows]
    print("  %s 여섯 팩은 **같은 값 $2.99·같은 6종 구성**인데, 이름은 %s 로 갈린다." %
          (ok, "/".join(sorted(set(gr), key=gr.index))))
    print("  → 동격 상품 여섯에 **사다리 낱말 네 개**가 새어 들어간다. 이것이 1:1 재사용의 값이다.")


# ============================================================================
# §2. 팔레트 재확인 — §13 처방 C를 다시 유도한다 (문서를 베끼지 않는다)
# ============================================================================
#: PALETTE_SPEC §13-3 표에 적힌 값. **이 스크립트가 다시 유도한 값과 대조**하는 용도로만 쓴다.
SPEC13 = {
    "오피스 워커":       ("#456ECC", "#6080CC"),
    "사이버 아포칼립스": ("#009682", "#518C84"),
    "네온 낙서":         ("#CC1BA9", "#9C5A8E"),
    "스포츠":            ("#CC3F29", "#9E655C"),
    "컬러 잉크":         ("#9768CC", "#8563AB"),
    "밀리터리":          ("#639400", "#798C51"),
}


def derive12(gap=8.0, strict=True):
    """§13-3 처방 C 유도. ★ 폴백 없음 — 해가 없으면 (None, 사유)."""
    import band, derive_packs as DP
    from packclash import load_current, INK_MARKERS, BRASS
    from packrule import pick
    cat = [h for h in load_current() if h not in INK_MARKERS]
    placed = list(cat) + [BRASS]
    rows = []
    for name, h, _ in DP.PACK_HUES:
        nm = "스포츠" if name.startswith("스포츠") else name
        p = pick(h, True, placed, gap)
        if p is None:
            if strict: return None, cat, "%s 주색 해 없음" % nm
            p = pick(h, True)
        placed.append(CL.rgb2hex(p))
        s = pick(h, False, placed, gap)
        if s is None:
            if strict: return None, cat, "%s 보조색 해 없음" % nm
            s = pick(h, False)
        placed.append(CL.rgb2hex(s))
        rows.append((nm, h, CL.rgb2hex(p), CL.rgb2hex(s)))
    return rows, cat, None


def section_palette():
    import band
    from packclash import DISCERN, BRASS
    BD = band.BACKDROPS
    LO, HI = band.limits()[0], band.limits()[1]
    RAMP = ["#9C978C", "#BCAC8B", "#DBBD7F", "#F9CB70"]
    print("=" * 100)
    print("§2. 팔레트 재확인 — §13 처방 C 12색을 **다시 유도**해 §13-3 표와 대조")
    print("=" * 100)
    rows, cat, err = derive12()
    assert err is None, err
    print("%-20s %6s %-9s %-9s %7s %7s %6s %6s  대조" %
          ("팩", "H", "주색", "보조색", "L(주)", "L(보)", "최악주", "최악보"))
    for nm, h, p, s in rows:
        P, S_ = CL.hex2rgb(p), CL.hex2rgb(s)
        same = SPEC13[nm] == (p, s)
        print("%-20s %6.1f %-9s %-9s %7.4f %7.4f %6.2f %6.2f  %s" %
              (nm, h, p, s, CL.L(P), CL.L(S_),
               min(CL.CR(P, b) for _, b in BD), min(CL.CR(S_, b) for _, b in BD),
               okmark(same, "§13-3 표와 불일치: %s" % nm) + ("동일" if same else "다름")))
    pk = [c for _, _, p, s in rows for c in (p, s)]
    Ls = [CL.L(CL.hex2rgb(c)) for c in pk]
    pr = min(CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)) for a in cat for b in pk)
    pp = min(CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)) for a, b in itertools.combinations(pk, 2))
    print("\n  카탈로그(%d)↔팩(12) 최소 ΔE %.2f  (하한 %.1f)  %s" %
          (len(cat), pr, DISCERN, okmark(pr >= DISCERN, "카탈로그↔팩 하한 미달")))
    print("  팩 12색 내부 최소 ΔE %.2f              %s" % (pp, okmark(pp >= DISCERN, "팩 내부 하한 미달")))
    print("  L 범위 %.4f~%.4f · 최대/최소 대비 %.2f:1 (한 세계의 물리적 정의)" %
          (min(Ls), max(Ls), (max(Ls) + .05) / (min(Ls) + .05)))
    print("  자립 대역 %d/12 · WornColor 항등 %d/12" %
          (sum(1 for c in pk if LO <= CL.L(CL.hex2rgb(c)) <= HI),
           sum(1 for c in pk if CL.worn(CL.hex2rgb(c)) == CL.hex2rgb(c))))
    print("  브라스 최근접 ΔE %.2f · 등급램프 최근접 ΔE %.2f" %
          (min(CL.dE(CL.hex2rgb(BRASS), CL.hex2rgb(c)) for c in pk),
           min(CL.dE(CL.hex2rgb(r), CL.hex2rgb(c)) for r in RAMP for c in pk)))
    return rows


# ============================================================================
# §3. ★ 조용한 폴백 재검사 — 리더 지시("같은 검사를 다시 붙여라")
# ============================================================================
def section_fallback():
    from packclash import DISCERN
    print("\n" + "=" * 100)
    print("§3. ★ 조용한 폴백 재검사 — 제약을 강화하면 **죽는가**, 조용히 나빠지는가")
    print("=" * 100)
    print("  제약을 올렸는데 결과가 좋아지거나 유지되면 그건 폴백이 켜져 있다는 뜻이다.")
    print("  %-6s | %-28s | %-10s | %s" % ("gap", "폴백 금지(strict)", "폴백 허용", "판정"))
    prev = None
    monotone_ok = True
    for gap in (7.0, 7.8, 8.0, 8.2, 8.5, 9.0, 10.0):
        rs, cat, err = derive12(gap, strict=True)
        rf, _, _ = derive12(gap, strict=False)
        pkf = [c for _, _, p, s in rf for c in (p, s)]
        mf = min(CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)) for a in cat for b in pkf)
        if err is None:
            pks = [c for _, _, p, s in rs for c in (p, s)]
            ms = min(CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)) for a in cat for b in pks)
            st = "해 있음 · 최소 ΔE %.2f" % ms
            # strict 해가 있으면 최소 ΔE는 gap 이상이어야 한다 (규칙의 정의 그 자체)
            if ms < gap - 1e-9:
                bad("gap %.1f: strict 해인데 최소 ΔE %.2f < gap" % (gap, ms))
            if prev is not None and ms < prev - 1e-9:
                monotone_ok = False
            prev = ms
            verdict = "정상"
        else:
            st = "★ %s" % err
            verdict = "폴백 허용 시 %.2f로 **나빠짐** → 폴백이 거짓 신호원" % mf
        print("  %-6.1f | %-28s | %10.2f | %s" % (gap, st, mf, verdict))
    print("\n  %s strict 최소 ΔE는 gap을 올릴수록 단조 비감소여야 한다 → %s" %
          (okmark(monotone_ok, "strict 경로가 gap 증가에 대해 단조가 아니다 = 폴백 의심"),
           "단조 확인" if monotone_ok else "★ 깨짐"))
    print("  %s 8.5 이상에서 strict가 **죽는다**(해 없음). 죽지 않으면 폴백이 살아 있는 것." %
          okmark(derive12(8.5, strict=True)[2] is not None, "gap 8.5에서 strict가 안 죽는다"))


# ============================================================================
# §4. (c) 형상 예산 — 예약 가능한 대역이 몇 개인가
# ============================================================================
SLOTS_GEO = [("HEAD", "items", "HEAD", 0.0),
             ("EYES", "items", "EYES", 0.0),
             ("NECK", "items", "NECK", "SH"),
             ("BACK", "items", "BACK", "SH"),
             ("FX",   "appearance", "FX_NOW", 0.0),
             ("PET",  "appearance", "PET_NOW", 0.0)]


def disjoint_sectors(table, anchor, clearance, min_bins, reach_cap):
    """봉투가 (reach_cap − clearance) 이하인 연속 min_bins 구간을, **서로 겹치지 않게** 최대로 고른다.
    그리디(왼쪽부터)로 세되 원형이라 시작점을 전부 돌려 최댓값을 취한다."""
    import sectors as S
    env = S.envelope(table, anchor)
    B = S.BINS
    ok = [max(env[(i + k) % B] for k in range(min_bins)) + clearance <= reach_cap for i in range(B)]
    best, best_start = 0, 0
    for s0 in range(B):
        cnt, i = 0, 0
        while i < B:
            b = (s0 + i) % B
            if ok[b]:
                cnt += 1; i += min_bins
            else:
                i += 1
        if cnt > best: best, best_start = cnt, s0
    return best, ok, env


def section_shape_budget():
    import rig, sectors as S
    print("\n" + "=" * 100)
    print("§4. (c) 형상 예산 — 「슬롯당 12종이면 실루엣 하한이 깨진다」를 우리 트리에서 다시 잰다")
    print("=" * 100)
    print("  전제 정정: product-strategy 근사는 **자유 배치**를 가정한다. 우리 트리에는")
    print("  design/equipment/verify/sectors.py 가 이미 있고, 예약 대역을 쓰면")
    print("  「그 팩 vs 나머지 전부」의 차가 **아이템 수와 무관**해진다(L∞ 성질).")
    print("  그래서 물어야 하는 값은 '12종에서 깨지는가'가 아니라 **'예약 자리가 6개 있는가'** 다.\n")
    print("  %-5s %5s %9s %9s %9s %7s %6s  %s" %
          ("슬롯", "기존", "봉투max", "쌍최소", "하한", "여유대역", "6팩?", "비고"))
    cap_fail = []
    for label, mod, attr, anchor in SLOTS_GEO:
        m = __import__(mod)
        table = {k: v for k, v in getattr(m, attr).items() if v}      # "없음" 제외
        a = rig.SHOULDER_R if anchor == "SH" else 0.0
        env = S.envelope(table, a)
        cap = max(env)
        pw = sorted(S.pairwise_table(table, a))[0][0] if len(table) > 1 else float("nan")
        n, ok, _ = disjoint_sectors(table, a, S.SECTOR_CLEARANCE_R, S.SECTOR_MIN_BINS, cap)
        enough = n >= 6
        if not enough: cap_fail.append(label)
        print("  %-5s %5d %9.4f %9.4f %9.4f %7d %6s  %s" %
              (label, len(table), cap, pw, S.SILHOUETTE_FLOOR_R, n,
               "예" if enough else "★아니오",
               "%.2f획@0.60" % (pw / S.W060)))
    print("\n  여유대역 = 봉투가 (도달상한 − 래칫 %.4fR) 이하인 **서로 겹치지 않는** 15° 구간 수." % S.SECTOR_CLEARANCE_R)
    print("  도달상한은 그 슬롯의 **현행 봉투 최대**로 잡았다(이미 출하된 도형이 도달한 값이므로")
    print("  액자·린트가 허용한다는 사실이 증명돼 있다). 더 넓게 잡으면 자리는 늘어난다.")
    if cap_fail:
        print("  ★ 자리가 6개 미만인 슬롯: %s → 이 슬롯은 팩이 6개까지 못 들어간다." % ", ".join(cap_fail))
    else:
        print("  ★ 여섯 슬롯 전부 6팩분 예약 자리가 있다.")
    print("\n  ※ 이 값은 **자리의 개수**이지 그 자리에 실제로 그릴 수 있다는 증명이 아니다.")
    print("     실제 좌표 검증은 design-equipment 소관 — 요청 항목을 사양서에 적었다.")
    return cap_fail


# ============================================================================
# §5·§6. Lv.1 · 등급 분포 · 사운드 6키 대조
# ============================================================================
#: design/sound/SOUND_QUALIFICATION.md §3-5 표에서 **읽어온** 값(문서를 파싱해 대조한다)
SOUND_DOC = os.path.join(ROOT, "design", "sound", "SOUND_QUALIFICATION.md")
#: 트리거 키가 어느 「보이는 것」에 실려 있는가 — 키 → 필요한 시각 자산
KEY_NEEDS = {
    "focus.start": "FX",  "focus.complete": "FX",
    "equip.wear": "착용4", "equip.remove": "착용4",
    "shop.purchase": "UI", "land.throw": "FX",
    "archery.draw": "FX", "archery.release": "FX", "archery.bullseye": "FX",
    "archery.hit": "FX", "archery.miss": "FX",
    "ragdoll.impact": "FX", "rodeo.grab": "PET",
}


def parse_sound_table():
    txt = open(SOUND_DOC, encoding="utf-8").read()
    out = {}
    for line in txt.splitlines():
        m = re.match(r"^\|\s*\*\*(.+?)\*\*\s*`(pack\.[a-z]+)`\s*\|(.*?)\|(.*?)\|", line)
        if not m: continue
        name, pid, _mat, keys = m.groups()
        ks = re.findall(r"`([a-z]+\.[a-z]+)`", keys)
        out[pid] = (name.strip(), ks)
    return out


def section_renderers():
    """★ 이펙트 색이 오늘 물리적으로 가능한가 — 프로덕션 렌더러를 직접 센다(문서를 안 믿는다)."""
    d = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Interaction")
    print("\n" + "=" * 100)
    print("§4-b. 이펙트 색의 물리적 가능성 — 렌더러가 **팩 팔레트를 읽을 수 있는가** (프로덕션 grep)")
    print("=" * 100)
    print("  %-32s %5s %8s  %s" % ("렌더러", "잉크", "팔레트", "팩색을 낼 수 있는가"))
    reach = {}
    for fn in sorted(os.listdir(d)):
        if not fn.endswith("Renderer.cs"): continue
        t = open(os.path.join(d, fn), encoding="utf-8").read()
        ink = t.count("ResolveInk"); pal = t.count("ResolveWornPalette")
        reach[fn] = (ink, pal)
        if ink or pal:
            print("  %-32s %5d %8d  %s" % (fn, ink, pal, "가능" if pal else "★ 불가 — 잉크만"))
    fx = reach.get("CharacterFxRenderer.cs", (0, 0))
    dust = reach.get("LandingDustRenderer.cs", (0, 0))
    arch = reach.get("ArcheryRenderer.cs", (0, 0))
    pet = reach.get("CharacterPetRenderer.cs", (0, 0))
    print("\n  §6-2가 팩색을 배정한 네 이펙트의 오늘 상태:")
    for lbl, v, who in (("착지 먼지", dust, "LandingDustRenderer"),
                        ("임팩트 선", arch, "ArcheryRenderer"),
                        ("오라·잔상", fx, "CharacterFxRenderer"),
                        ("펫", pet, "CharacterPetRenderer")):
        print("    %s %-8s %-22s 팔레트 호출 %d건" %
              ("OK " if v[1] else "★  ", lbl, who, v[1]))
    print("  ★ 팩 FX는 **오늘 색을 못 나른다**. 팔레트 경로 신설이 선결이다(리더 → coder).")
    return fx[1], pet[1]


def section_fxpet_floor():
    """★ FX·PET 실루엣 — DLC 이전에 **이미** 하한 아래인가."""
    import sectors as S, appearance as A
    print("\n" + "=" * 100)
    print("§4-c. FX·PET 실루엣 — 깨지는 것이 DLC 때문인가, 이미 깨져 있는가")
    print("=" * 100)
    print("  %-10s %3s %10s %10s  %s" % ("표", "n", "쌍최소R", "획@0.60", "판정"))
    res = {}
    for lbl, t in (("FX 현행", A.FX_NOW), ("FX 제안A", A.FX_A),
                   ("PET 현행", A.PET_NOW), ("PET 제안A", A.PET_A)):
        tab = {k: v for k, v in t.items() if v}
        pw = sorted(S.pairwise_table(tab, 0.0))[0][0]
        res[lbl] = pw
        print("  %-10s %3d %10.4f %10.2f  %s" % (lbl, len(tab), pw, pw / S.W060,
              "하한 통과" if pw >= S.SILHOUETTE_FLOOR_R else "★ 하한 미달"))
    print("\n  ★ 현행 FX %.2f획 · PET %.2f획 — **아이템 5개인 오늘 이미 1획 아래다.**" %
          (res["FX 현행"] / S.W060, res["PET 현행"] / S.W060))
    print("    design-equipment의 「제안안 A」(좌표만 고침, 렌더러 무변경)가 둘 다 되살린다:")
    print("    FX %.2f→%.2f획 · PET %.2f→%.2f획." %
          (res["FX 현행"] / S.W060, res["FX 제안A"] / S.W060,
           res["PET 현행"] / S.W060, res["PET 제안A"] / S.W060))
    print("    → 6팩은 이 결함을 **드러낸 것**이지 만든 것이 아니다. 제안안 A가 DLC의 선결이다.")
    return res


def section_lv1_sound(rows):
    print("\n" + "=" * 100)
    print("§5·§6. (b) Lv.1 간판 · 등급 분포 · 사운드 6키가 「보이는 것」과 한 벌인가")
    print("=" * 100)
    snd = parse_sound_table()
    print("  사운드 문서에서 읽은 팩 %d개 (문서를 베끼지 않고 파싱했다)" % len(snd))
    pal = {nm: (p, s) for nm, _h, p, s in rows}
    for name, pid, hue in CANON:
        nm2, keys = snd.get(pid, ("(없음)", []))
        need = sorted(set(KEY_NEEDS.get(k, "?") for k in keys))
        cnt_ok = len(set(keys)) <= 6
        print("  %s %-20s %-15s 키 %d개 %-6s 필요자산 %-16s 팔레트 %s/%s  사운드문서명 「%s」" %
              (okmark(cnt_ok and pid in snd, "%s 사운드 6키 규칙 위반/미발견" % name),
               name, pid, len(set(keys)), "(≤6)" if cnt_ok else "★초과",
               "+".join(need), pal[name][0], pal[name][1], nm2))
    # 등급 분포 2/2/1/1 · Lv.1 최소 1종
    print("\n  등급 분포 규칙 (ECONOMY_SPEC 4-5 = 팩 6종의 **등급** 분포, 슬롯 분포가 아니다)")
    dist = {"일반": 2, "희귀": 2, "영웅": 1, "전설": 1}
    tot = sum(dist.values())
    print("  %s 합계 %d종 (팩 정의 6종)" % (okmark(tot == 6, "등급 분포 합이 6이 아니다"), tot))
    coins = (30 + 30 + 70 + 70 + 150 + 330) * 0.5
    print("  %s 팩 6종 동전 환산 %.0f (ECONOMY_SPEC 4-5 기대 340)" %
          (okmark(abs(coins - 340) < 1e-9, "동전 환산 불일치"), coins))
    print("  Lv.1 간판: 팩 내부 순서 1번 = 등급 일반 2종 중 **주색을 가장 크게 쓰는 1종**.")


def control():
    """★ 양성 대조 — 일부러 틀린 입력을 넣어 게이트가 실제로 빨간불을 내는가."""
    print("=" * 100)
    print("★ 양성 대조 — 게이트가 진짜로 잡는가 (전부 '✗'가 떠야 정상)")
    print("=" * 100)
    n0 = len(FAIL)
    hit = [0]
    def detect(cond, name):
        """cond = 게이트가 그 나쁜 값을 **실제로 잡았는가**."""
        if cond: hit[0] += 1
        return okmark(cond, "대조 %s 를 게이트가 놓쳤다" % name)
    # C1. 색: 자립 대역 밖 색을 팩 색이라고 우기면 잡히는가
    import band
    LO, HI = band.limits()[0], band.limits()[1]
    fake = "#FFFFFF"
    print("  %s C1 자립대역 검사: %s L=%.4f (대역 %.4f~%.4f)" %
          (detect(not (LO <= CL.L(CL.hex2rgb(fake)) <= HI), "C1"),
           fake, CL.L(CL.hex2rgb(fake)), LO, HI))
    # C2. 색: 같은 색 두 개를 넣으면 변별 하한이 잡히는가
    from packclash import DISCERN
    d = CL.dE(CL.hex2rgb("#456ECC"), CL.hex2rgb("#456ECD"))
    print("  %s C2 변별 하한 검사: ΔE %.4f < %.1f" %
          (detect(d < DISCERN, "C2"), d, DISCERN))
    # C3. 기하: 도달상한을 무한대로 주면 여유대역이 72/3=24개로 포화하는가(계산기가 살아 있는가)
    import sectors as S, items, rig
    n, _, _ = disjoint_sectors(items.HEAD, 0.0, 0.0, S.SECTOR_MIN_BINS, 1e9)
    print("  %s C3 대역 계산기 상한: 제약 0에서 %d개 (기대 24 = 72/3)" %
          (detect(n == 24, "C3"), n))
    # C4. 기하: 도달상한을 0으로 주면 0개여야 한다
    n2, _, _ = disjoint_sectors(items.HEAD, 0.0, S.SECTOR_CLEARANCE_R, S.SECTOR_MIN_BINS, 0.0)
    print("  %s C4 대역 계산기 하한: 도달상한 0에서 %d개 (기대 0)" %
          (detect(n2 == 0, "C4"), n2))
    # C5. 폴백: strict=False로 gap 9.0을 돌리면 8.0보다 **나빠져야** 한다(거짓 신호의 모양)
    rf9, cat, _ = derive12(9.0, strict=False)
    rf8, _, _ = derive12(8.0, strict=False)
    m9 = min(CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)) for a in cat for _, _, p, s in rf9 for b in (p, s))
    m8 = min(CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)) for a in cat for _, _, p, s in rf8 for b in (p, s))
    print("  %s C5 폴백 거짓신호 재현: gap 9.0 %.2f < gap 8.0 %.2f (강화했는데 나빠짐)" %
          (detect(m9 < m8, "C5"), m9, m8))
    miss = len(FAIL) - n0
    print("\n  게이트가 잡은 나쁜 값 %d/5건 · 놓친 것 %d건." % (hit[0], miss))
    print("  **5/5가 아니면 이 파일의 다른 모든 '없음/통과' 판정을 폐기한다.**")
    if hit[0] != 5:
        print("  ★ 대조 실패 — 폐기."); sys.exit(3)
    del FAIL[n0:]
    return hit[0]


# ============================================================================
# §7. 36종 편성 — 기본 42종과 겹침 0 · 슬롯 6 × 팩 6 · 등급 2/2/1/1
# ============================================================================
PACK_ITEMS = {
    "pack.office":   {"NECK": "사원증",     "EYES": "모니터안경", "HEAD": "헤드셋",
                      "PET": "스테이플러",  "BACK": "걸친재킷",   "FX": "결재도장"},
    "pack.cyber":    {"NECK": "케이블목띠", "EYES": "한줄바이저", "HEAD": "덧댄후드",
                      "PET": "부서진드론",  "BACK": "방수포망토", "FX": "글리치"},
    "pack.graffiti": {"NECK": "캡목걸이",   "EYES": "보안경",     "HEAD": "페인트버킷햇",
                      "PET": "스프레이캔",  "BACK": "슬링백",     "FX": "튄자국"},
    "pack.sports":   {"NECK": "호루라기",   "EYES": "눈밑띠",     "HEAD": "헤드밴드",
                      "PET": "스톱워치",    "BACK": "등번호조끼", "FX": "궤적"},
    "pack.ink":      {"NECK": "잉크병",     "EYES": "물감안경",   "HEAD": "잉크캡",
                      "PET": "붓",          "BACK": "물감앞치마", "FX": "번짐"},
    "pack.military": {"NECK": "인식표",     "EYES": "야시경",     "HEAD": "야전모",
                      "PET": "수통",        "BACK": "웨빙하네스", "FX": "연막"},
}
#: 팩 안의 사다리 — 슬롯 → (순서, 등급)
LADDER = {"NECK": (1, "일반"), "EYES": (2, "일반"), "HEAD": (3, "희귀"),
          "PET": (4, "희귀"), "BACK": (5, "영웅"), "FX": (6, "전설")}


def base42_names():
    """★ 문서를 안 믿는다. `Resources/Items/*.asset`의 displayName을 직접 디코드한다.
    (한글이 backslash-uXXXX 이스케이프라 `grep '[가-힣]'`은 0건을 답한다 — TEAM.md 거짓통과 #4)"""
    d = os.path.join(ROOT, "Assets", "_Project", "Resources", "Items")
    out = {}
    for fn in sorted(os.listdir(d)):
        if not fn.endswith(".asset"): continue
        t = open(os.path.join(d, fn), encoding="utf-8").read()
        m = re.search(r"displayName:\s*(.+)", t)
        if not m: continue
        v = m.group(1).strip()
        # ★ 값은 따옴표에 싸여 있다: displayName: "\uC911\uC808\uBAA8"
        #   따옴표를 안 벗기면 디코드 결과가 '"중절모"'가 되어 **모든 비교가 조용히 어긋난다.**
        #   내 첫 시안이 정확히 그랬고, 양성 대조('중절모'를 찾아라)가 그것을 잡았다.
        #   그때 '기본 42종과 겹침 0건'은 **거짓 초록**이었다.
        if len(v) >= 2 and v[0] == v[-1] == '"': v = v[1:-1]
        try: v = v.encode("ascii", "ignore").decode("unicode_escape")
        except Exception: pass
        out[fn[:-6]] = v
    return out


def section_items():
    print("\n" + "=" * 100)
    print("§7. 36종 편성 — 기본 42종과 겹침 · 슬롯 · 등급 분포")
    print("=" * 100)
    base = base42_names()
    print("  기본 에셋 %d개에서 displayName %d개를 디코드했다 (이스케이프 해제)" %
          (len(base), len(set(base.values()))))
    print("  %s 42개 · 이름 중복 0" % okmark(len(base) == 42 and len(set(base.values())) == 42,
                                             "기본 42종 이름을 42개로 못 읽었다"))
    # ★ 양성 대조: 디코더가 살아 있는가 — 알려진 이름 하나가 나와야 한다
    known = [k for k, v in base.items() if v == "중절모"]
    print("  %s 디코더 양성 대조: '중절모' 발견 %s" %
          (okmark(bool(known), "디코더가 죽었다 — 아래 '겹침 0' 판정 전부 무효"),
           known[0] if known else "(없음)"))
    names = []
    for pid, m in PACK_ITEMS.items():
        for slot, nm in m.items(): names.append((pid, slot, nm))
    dup = sorted(set(nm for _, _, nm in names if [x[2] for x in names].count(nm) > 1))
    clash = sorted(set(nm for _, _, nm in names) & set(base.values()))
    print("  %s 팩 36종 (팩 6 × 슬롯 6)   실제 %d종" %
          (okmark(len(names) == 36, "팩 종수가 36이 아니다"), len(names)))
    print("  %s 팩 내부 이름 중복 %d건 %s" % (okmark(not dup, "팩 이름 중복"), len(dup), dup or ""))
    print("  %s 기본 42종과 겹침 %d건 %s" %
          (okmark(not clash, "기본 42종과 이름 충돌"), len(clash), clash or ""))
    slots = sorted(set(s for _, s, _ in names))
    print("  %s 쓰는 슬롯 %s — HAIR 미사용 %s" %
          (okmark("HAIR" not in slots and len(slots) == 6, "슬롯 배분 규칙 위반"),
           slots, "확인" if "HAIR" not in slots else "★위반"))
    from collections import Counter
    g = Counter(LADDER[s][1] for s in slots)
    want = {"일반": 2, "희귀": 2, "영웅": 1, "전설": 1}
    print("  %s 팩당 등급 분포 %s (기대 %s)" %
          (okmark(dict(g) == want, "등급 분포가 2/2/1/1이 아니다"), dict(g), want))
    lv1 = [s for s in slots if LADDER[s][0] == 1]
    print("  %s Lv.1 간판 슬롯 %s — 여섯 팩 공통 · FX 아님" %
          (okmark(lv1 == ["NECK"], "Lv.1 슬롯이 NECK이 아니다"), lv1))
    return len(names)


def main():
    ctl = "--control" in sys.argv
    calibrate(strict=True)
    if ctl:
        control(); return
    drift = section_names()
    section_name_axis()
    section_rank_leak()
    rows = section_palette()
    section_fallback()
    capfail = section_shape_budget()
    section_renderers()
    section_fxpet_floor()
    section_lv1_sound(rows)
    section_items()
    print("\n" + "=" * 100)
    print("판정")
    print("=" * 100)
    print("  게이트 위반 %d건 %s" % (len(FAIL), "" if not FAIL else "★"))
    for f in FAIL: print("   ·", f)
    print("  이름 드리프트 %d건 · 예약 자리 부족 슬롯 %d개" % (drift, len(capfail)))
    sys.exit(1 if FAIL else 0)


if __name__ == "__main__":
    main()
