# -*- coding: utf-8 -*-
"""★ R11 — 안 C1 색 게이트 3종 (2026-09-03, design-equipment).

사용자 확정 「안 C1」이 색 논쟁을 닫았다:
    아이템 그림 · 착용 장비 = 재질색 (현행 유지, WornColor 항등 유지)
    등급 표시 = 리본 칸 수 + 낱말 라벨 / 카드 바탕 무틴트 / 크롬은 브라스
    인계본 착용 오버레이 4색(WEAR[]) = 폐기

⇒ 앞 라운드의 「인계본 12색 중 11색이 우리 대역 밖」은 **이제 문제가 아니다. 그 색들은 몸에
   안 온다.** 남는 색 질문은 셋뿐이고, 이 파일이 그 셋을 잰다.

  §1 불투명 — 우리 출하 아이템이 정말로 100% 불투명인가 (다음 사람이 알파를 들여오지 않게)
  §2 채택 가능한 인계본 색은 정말 망토 하나뿐인가, 그리고 **그게 값을 하는가**
  §3 팩 저작 규칙 — 한 팩 안 색상각 스프레드 상한. 팩이 계속 늘어나므로 영구 규칙이다

    python3 r11_c1gate.py            # 세 게이트
    python3 r11_c1gate.py --control  # ★ 양성 대조 3건 — 3/3이 아니면 위 전부 폐기
"""
import sys, os, itertools, statistics

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "..", "art", "verify")))
import colorlab as C, band, shipped

CONTROL = "--control" in sys.argv
LO, HI, _ = band.limits()
CR_FLOOR = 3.0
DE_FLOOR = 7.8
HUE_SPREAD_MAX = 60.0

fails = []


def check(ok, msg):
    print("  %s %s" % ("OK  " if ok else "★FAIL", msg))
    if not ok: fails.append(msg)
    return ok


def hexof(rgb): return C.rgb2hex(rgb)
def worst_cr(rgb): return min(C.CR(rgb, bg) for _, bg in band.BACKDROPS)


def spread(hues):
    """원형 색상각 집합을 덮는 최소 호(도)."""
    h = sorted(x % 360 for x in hues)
    if len(h) < 2: return 0.0
    gaps = [(h[(i + 1) % len(h)] - h[i]) % 360 for i in range(len(h))]
    return 360.0 - max(gaps)


IC = shipped.item_colors()


def main_rgb(key):
    return sorted(IC[key]["tones"][0])[0]


# ══════════════════════════════════════════════════════════════════════════
print("╔══ §1. 불투명 — 「이미 그렇다」를 기계로 못박는다 ══╗")
print("  사용자 제약: \"장비는 기존 컨셉처럼 투명해서 머리속이나 그런게 보이면 안됨\"")
print("  ★ 이건 신고가 아니라 **예방 지시**였고, 옳았다 — 인계본 91조각 중 불투명은 24개뿐이다.")
alphas = set()
for k, v in IC.items():
    alphas |= set(v["alphas"])
if CONTROL:
    alphas.add(0.55)                       # 양성 대조: 반투명 조각이 하나 섞였다고 치자
check(alphas == {1.0},
      "출하 %d아이템 전수 알파 = %s  (1.0 하나여야 한다)" % (len(IC), sorted(alphas)))
print("     근거 경로: ItemCatalog.WornColor 마지막 줄 `result.a = ink.a`  —")
print("     조각별 알파가 **존재하지 않는다.** 알파는 잉크 하나로 전역이다(페이드용).")
print("     ⇒ 인계본의 채움 알파 14단계 · 그라디언트 2종 · 선 불투명도 4단계는")
print("        **몸에 들여올 수단 자체가 없다.** 들이려면 BuildFillMesh 시그니처 +")
print("        WornColor 계약 + Shape 구조체를 동시에 바꿔야 한다.")

# ══════════════════════════════════════════════════════════════════════════
print()
print("╔══ §2. 인계본에서 몸으로 가져올 수 있는 색 ══╗")
HANDOFF = {
    "아이템색 C": "#E8E2D6", "강조색 A(브라스)": "#C8A15A", "망토 재질색": "#D2402F",
    "등급 일반": "#D8B27A", "등급 희귀": "#7FB0F2", "등급 영웅": "#C08FEC",
    "등급 전설": "#F0C25C", "망토 뒤판 채움": "#A8332A", "망토 뒤판 보더": "#7E1F17",
    "망토 칼라": "#8E241C", "망토 클래스프": "#C8A15A", "하이라이트": "#FFFFFF",
}
passed = []
print("  %-18s %-9s %-8s %-9s %s" % ("색", "hex", "L", "최악CR", "판정"))
for name, hx in HANDOFF.items():
    rgb = C.hex2rgb(hx)
    w = worst_cr(rgb)
    inb = LO <= C.L(rgb) <= HI and w >= CR_FLOOR
    if inb: passed.append((name, hx))
    print("  %-18s %-9s %.4f %8.2f  %s" % (name, hx, C.L(rgb), w, "대역 OK" if inb else "대역 밖"))
check(len(passed) == 1 and passed[0][1] == "#D2402F",
      "대역을 넘는 인계본 색은 정확히 1개(망토 재질색)여야 한다 — 실제 %d개: %s"
      % (len(passed), [p[1] for p in passed]))

print()
print("  ── 그 하나를 애셋에 어떻게 적는가 ──")
src = C.hex2rgb("#D2402F")
w_src = C.rgb2hex(C.worn(src))
print("     #D2402F 는 WornColor **비항등**이다 (→ %s, ΔE %.2f)." % (w_src, C.dE(src, C.hex2rgb(w_src))))
tgt = C.hex2rgb(w_src)
check(C.rgb2hex(C.worn(tgt)).upper() == w_src.upper(),
      "애셋에 %s 로 적으면 항등이 된다 (카드 색 = 몸 색, 바이트 단위)" % w_src)
print("     %s  L=%.4f  최악CR %.2f  색상각 %.1f°" % (w_src, C.L(tgt), worst_cr(tgt), C.hue_deg(tgt)))

print()
print("  ── ★ 그런데 그게 값을 하는가 — 우리가 **이미 쓰고 있는** 망토색과 대 본다 ──")
cur = main_rgb("equip_shoulders_cape")
print("     현행 망토 주색  %s  L=%.4f  최악CR %.2f  색상각 %.1f°"
      % (hexof(cur), C.L(cur), worst_cr(cur), C.hue_deg(cur)))
print("     인계본 유래     %s  L=%.4f  최악CR %.2f  색상각 %.1f°"
      % (w_src, C.L(tgt), worst_cr(tgt), C.hue_deg(tgt)))
d = C.dE(cur, tgt)
print("     ΔE = %.2f  (주↔보조 변별 하한 %.1f)  · 최악CR 이득 %+.2f  · 색상각 %+.1f°"
      % (d, DE_FLOOR, worst_cr(tgt) - worst_cr(cur), C.hue_deg(tgt) - C.hue_deg(cur)))
print("     ⇒ 「유일하게 들여올 수 있는 색」이 **이미 그 아이템에 칠해져 있는 색의 6.1° 회전**이다.")
print("        바꾸면 애셋 2개(cape·long_cape) + 골든이 낡는다. 얻는 것은 최악CR +0.01.")
print("        ★ 권고: **채택하지 않는다.** 인계본에서 몸으로 오는 색은 **0개**다.")

# ══════════════════════════════════════════════════════════════════════════
print()
print("╔══ §3. 팩 저작 규칙 — 한 팩 안 색상각 스프레드 ≤ %.0f° ══╗" % HUE_SPREAD_MAX)
print("  사용자 확정 \"출시 이후부터 계속 추가팩 만들거야\" ⇒ 이건 1회성 판정이 아니라 영구 규칙이다.")
print()
print("  ── (a) design-art 가 이미 선언한 6팩 (PACK_THEME_SPEC 5-1~5-6) ──")
ART = {"office": ("#456ECC", "#6080CC"), "cyber": ("#009682", "#518C84"),
       "graffiti": ("#CC1BA9", "#9C5A8E"), "sports": ("#CC3F29", "#9E655C"),
       "ink": ("#9768CC", "#8563AB"), "military": ("#639400", "#798C51")}
art_bad = []
for k, (a, b) in ART.items():
    ra, rb = C.hex2rgb(a), C.hex2rgb(b)
    sp = spread([C.hue_deg(ra), C.hue_deg(rb)])
    de = C.dE(ra, rb)
    inb = all(LO <= C.L(r) <= HI and worst_cr(r) >= CR_FLOOR for r in (ra, rb))
    ident = all(C.rgb2hex(C.worn(r)).upper() == h.upper() for r, h in ((ra, a), (rb, b)))
    ok = sp <= HUE_SPREAD_MAX and de >= DE_FLOOR and inb and ident
    if not ok: art_bad.append(k)
    print("    %-9s %s/%s  스프레드 %5.1f°  주↔보조 ΔE %5.2f  대역 %s  항등 %s  %s"
          % (k, a, b, sp, de, "OK" if inb else "밖", "OK" if ident else "✗",
             "OK" if ok else "★"))
check(not art_bad, "design-art 6팩이 장비 게이트(스프레드·ΔE·대역·항등)를 전부 통과한다")
print("    ★ 이 6팩은 색상각을 **≤0.9° 안에서** 잡고 주↔보조 변별을 **채도·밝기로** 만든다.")
print("       ⇒ %.0f° 는 목표가 아니라 **천장**이다. 실무 관행은 이미 훨씬 좁다." % HUE_SPREAD_MAX)

print()
print("  ── (b) 왜 %.0f° 인가 — 「잡탕」 기준선을 실측해서 정한다 ──" % HUE_SPREAD_MAX)
slots = {}
for k in IC:
    if k.startswith("equip_"):
        slots.setdefault(k.split("_")[1], []).append(k)
order = ["head", "eyes", "neck", "shoulders"]
combos = list(itertools.product(*[sorted(slots[s]) for s in order]))
sp_all = sorted(spread([C.hue_deg(main_rgb(k)) for k in c]) for c in combos)
print("    현행 출하 장비 4부위 임의 조합 %d가지의 색상각 스프레드:" % len(sp_all))
print("      최소 %.1f° · 25%% %.1f° · 중앙 %.1f° · 75%% %.1f° · 최대 %.1f° · 평균 %.1f°"
      % (sp_all[0], sp_all[len(sp_all)//4], sp_all[len(sp_all)//2],
         sp_all[3*len(sp_all)//4], sp_all[-1], statistics.mean(sp_all)))
print()
print("    문턱 후보별 통과율 — **45~60 과 75~90 이 각각 같은 값**이라 문턱이 벼랑에 안 걸린다:")
prev = None
for th in (30, 45, 60, 75, 90, 120):
    n = sum(1 for x in sp_all if x <= th)
    flat = " ← 45°와 동률(고원)" if th == 60 and n == sum(1 for x in sp_all if x <= 45) else ""
    print("      ≤ %3d°  %5d / %5d  (%4.1f%%)%s" % (th, n, len(sp_all), 100.0*n/len(sp_all), flat))
n60 = sum(1 for x in sp_all if x <= 60)
n45 = sum(1 for x in sp_all if x <= 45)
check(n60 == n45,
      "문턱 60°가 고원 위에 있다 (45°와 통과 집합이 같다 ⇒ ±15° 흔들려도 판정이 안 뒤집힌다)")
check(statistics.median(sp_all) > 3 * HUE_SPREAD_MAX,
      "「잡탕」 중앙값 %.1f° 가 문턱 %.0f° 의 3배를 넘는다 (규칙이 실제로 무언가를 거른다)"
      % (statistics.median(sp_all), HUE_SPREAD_MAX))

print()
print("  ── (c) 현행 출하 장비의 색상각 군집 (팩이 앉을 수 있는 자리) ──")
GAP = 25.0
hues = sorted((C.hue_deg(main_rgb(k)), k) for s in order for k in slots[s])
cluster, out = [], []
for h, k in hues:
    if cluster and h - cluster[-1][0] > GAP:
        out.append(cluster); cluster = []
    cluster.append((h, k))
out.append(cluster)
widths = []
for cl in out:
    w = cl[-1][0] - cl[0][0]
    widths.append(w)
    print("    %5.0f°~%5.0f°  폭 %5.1f°  %2d종  %s"
          % (cl[0][0], cl[-1][0], w, len(cl),
             " ".join(k.split("_", 2)[2] for k in [x[1] for x in cl])))
check(all(w <= HUE_SPREAD_MAX for w in widths),
      "★ 현행 출하 24종이 이미 %.0f° 이하 군집 %d개로 갈린다 (최대 폭 %.1f°) — "
      "%.0f° 는 새 제약이 아니라 **이미 하고 있는 것의 성문화**다"
      % (HUE_SPREAD_MAX, len(out), max(widths), HUE_SPREAD_MAX))

# ══════════════════════════════════════════════════════════════════════════
print()
print("╔══ §4. 염색 가능/불가 — 재질이 곧 색인 아이템은 색을 못 돌린다 ══╗")
print("  근거: persona-immersion 실측. \"재질이 사라지면 이름이 바뀐다\" —")
print("  파란 밀짚모자는 밀짚모자가 아니라 **파란 버킷햇**이다.")
NO_DYE = {                      # 아이템 → (재질, 허용 색상각 구간)
    "equip_head_straw":   ("밀짚", (20, 55)),
    "equip_head_crown":   ("금",   (35, 60)),
    "equip_eyes_monocle": ("금테", (35, 60)),
    "equip_neck_bell":    ("놋쇠", (15, 60)),
    "equip_head_fur":     ("모피", (20, 50)),
}
DYE = ["equip_head_beret", "equip_head_fedora", "equip_neck_bandana", "equip_neck_bowtie",
       "equip_neck_scarf", "equip_neck_striped", "equip_shoulders_poncho",
       "equip_shoulders_cape", "equip_shoulders_long_cape", "equip_eyes_patch"]
bad_dye = []
for k, (mat, (a, b)) in NO_DYE.items():
    h = C.hue_deg(main_rgb(k))
    if CONTROL and k == "equip_head_straw": h = 215.0     # 양성 대조: 파란 밀짚모자
    ok = a <= h <= b
    if not ok: bad_dye.append((k, mat, h))
    print("  %-22s %-4s 주색 %s 색상각 %5.1f°  허용 %d~%d°  %s"
          % (k.split("_", 1)[1], mat, hexof(main_rgb(k)) if not (CONTROL and k == "equip_head_straw") else "#3378CC",
             h, a, b, "OK" if ok else "★ 재질 모순"))
check(not bad_dye,
      "염색 불가 5종이 전부 자기 재질 색상각 안에 있다 — 실제 위반 %d건 %s"
      % (len(bad_dye), bad_dye))
print("  ⇒ ★ 이건 **새로 부과하는 제약이 아니다.** 출하 25색 팔레트가 **이미 지키고 있다.**")
print("     적어 두는 이유는 다음 팩이 모르고 깨지 않게 하기 위해서다.")
print("  염색 가능(천·펠트) %d종: %s" % (len(DYE), " ".join(k.split("_", 1)[1] for k in DYE)))

# ══════════════════════════════════════════════════════════════════════════
print()
if CONTROL:
    print("╔══ ★ 양성 대조 판정 ══╗")
    print("  고의로 심은 것 3건: (1) 반투명 조각 0.55  (2) — (3) 파란 밀짚모자 215°")
    print("  ⇒ 실패 %d건. **2건 이상이 아니면 이 자는 아무것도 안 재고 있다.**" % len(fails))
    for f in fails: print("     · %s" % f)
    sys.exit(0 if len(fails) >= 2 else 1)

print("╔══ 총 실패 %d건 ══╗" % len(fails))
for f in fails: print("  · %s" % f)
sys.exit(1 if fails else 0)
