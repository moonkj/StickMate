# -*- coding: utf-8 -*-
"""R9 — 축 B(부모 채움 축)의 **모집단을 다시 세운다**.

★ 앞 라운드(그리고 리더 배정서)의 「9/42 미달, 최악 2.756」은 **42 아이템 전부**를
  (주색, 보조색) 쌍으로 세었다. 그런데 `FillOutlineColor`는 아무 데서나 안 불린다:

    AccessoryCardIcon.TryBuild / CharacterAccessoryRenderer.AddShape / CharacterPortraitStage
        → if (shape.Filled) outline = FillOutlineColor(color);
    CharacterInfoWindow.BuildIcon (AccessoryCardIcon 이 실패한 슬롯의 폴백)
        → case ItemIconPartKind.Polygon 만 FillOutlineColor 를 부른다

  애셋 census: **FX 6 · PET 6 의 아이콘 조각에 Polygon(kind 4)이 0개다**(kind 0/1/2/3 뿐).
  그리고 FX/PET 은 도형 빌더에 case 가 없어 AccessoryCardIcon 이 실패 → 폴백을 탄다.
  ⇒ **FX·PET 12종은 축 B에 아예 없다.** 그런데 옛 최악값(look_pet_ball 2.75)이 바로 그것이었다.

  남는 30종도 「보조색 조각이 Filled 이고, 그 윤곽이 실제로 주색 채움 위를 지나는가」를 봐야 한다.
  기하 판정은 설계 거울(items.py / hair.py, mirrordrift 0건)에서 한다.
"""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "design", "art", "verify"))
sys.path.insert(0, HERE)
import colorlab as C, shipped
import items, hair

KS = [0.62, 0.45, 0.40, 0.36, 0.35, 0.32, 0.30, 0.28, 0.25]
FLOOR = 3.0
SLOTS = [("HEAD", items.HEAD, ["equip_head_cap", "equip_head_fur", "equip_head_fedora",
                               "equip_head_crown", "equip_head_beret", "equip_head_straw"]),
         ("EYES", items.EYES, ["equip_eyes_sunglasses", "equip_eyes_round", "equip_eyes_goggles",
                               "equip_eyes_monocle", "equip_eyes_browline", "equip_eyes_patch"]),
         ("NECK", items.NECK, ["equip_neck_bowtie", "equip_neck_striped", "equip_neck_scarf",
                               "equip_neck_bell", "equip_neck_pendant", "equip_neck_bandana"]),
         ("BACK", items.BACK, ["equip_shoulders_cape", "equip_shoulders_long_cape",
                               "equip_shoulders_wings", "equip_shoulders_backpack",
                               "equip_shoulders_poncho", "equip_shoulders_fairy_wings"]),
         ("HAIR", hair.SET, ["look_hair_cowlick", "look_hair_neat", "look_hair_curly",
                             "look_hair_bald", "look_hair_bowl", "look_hair_ponytail"])]


def lin1(c): return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
def Lf(v):
    r, g, b = (lin1(max(0.0, min(1.0, x))) for x in v); return .2126 * r + .7152 * g + .0722 * b
def CRf(a, b):
    la, lb = Lf(a), Lf(b); hi, lo = max(la, lb), min(la, lb); return (hi + .05) / (lo + .05)
def f01(c): return tuple(v / 255.0 for v in c)
def mul(c, k): return tuple(v / 255.0 * k for v in c)


def inside(poly, q):
    c = False; n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        if (a[1] > q[1]) != (b[1] > q[1]):
            x = a[0] + (q[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
            if q[0] < x: c = not c
    return c


def boundary_pts(sh, n=600):
    p = list(sh.pts) + ([sh.pts[0]] if sh.loop else [])
    seg = [(p[i], p[i + 1]) for i in range(len(p) - 1)]
    tot = sum(math.dist(a, b) for a, b in seg) or 1.0
    out = []
    for a, b in seg:
        m = max(2, int(n * math.dist(a, b) / tot))
        for i in range(m):
            t = i / m
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out


def calib():
    print("╔══ 교정 ══╗")
    sq = [(0, 0), (2, 0), (2, 2), (0, 2)]
    c1 = inside(sq, (1, 1)) and not inside(sq, (3, 1))
    print("  [%s] 점-다각형 안/밖 판정" % ("OK" if c1 else "★ "))
    c2 = abs(CRf((1, 1, 1), (0, 0, 0)) - 21.0) < 5e-4
    print("  [%s] CR 흰/검 21.0" % ("OK" if c2 else "★ "))
    b = boundary_pts(items.Shape("t", sq, True))
    c3 = abs(len(b) - 600) < 20 and all(abs(x[0]) < 2.01 for x in b)
    print("  [%s] 변 표본 %d점" % ("OK" if c3 else "★ ", len(b)))
    if not (c1 and c2 and c3): sys.exit("★ 교정 실패")
    print()


def main():
    calib()
    ic = shipped.item_colors()
    pop, skipped = [], []
    print("╔══ 모집단 재구성 — 보조색 조각이 Filled 이고 그 윤곽이 주색 채움 위를 지나는가 ══╗")
    print("  %-4s %-2s %-11s %-16s %-7s %9s" % ("슬롯", "#", "이름", "보조색 조각", "Filled", "주채움위"))
    for slot, tbl, stems in SLOTS:
        for idx, (key, stem) in enumerate(zip(tbl.keys(), stems)):
            sh = tbl[key]
            sh = sh() if callable(sh) else sh
            pri = sorted(ic[stem]["tones"][0])[0]
            sec = sorted(ic[stem]["tones"].get(1, ic[stem]["tones"][0]))[0]
            parents = [s for s in sh if s.tone == 0 and s.filled]
            accs = [s for s in sh if s.tone == 1]
            best = 0.0; nm = "-"; fl = False
            for a in accs:
                nm = a.name; fl = a.filled
                if not a.filled: continue
                b = boundary_pts(a)
                f = sum(1 for q in b if any(inside(p.pts, q) for p in parents)) / len(b)
                best = max(best, f)
            print("  %-4s %-2d %-11s %-16s %-7s %8.1f%%"
                  % (slot, idx, key, nm, "true" if fl else "false", 100 * best))
            (pop if (fl and best > 0) else skipped).append((slot, idx, key, stem, pri, sec, best))
    print("\n  ⇒ 축 B 모집단 **%d종** (제외 %d종: 보조색이 안 채워졌거나 주색 채움 위를 안 지난다)"
          % (len(pop), len(skipped)))
    print("     제외: " + ", ".join("%s %s" % (s, k) for s, i, k, *_ in skipped))
    print("     ★ FX 6 · PET 6 은 애초에 모집단이 아니다(아이콘에 Polygon 0개 = FillOutlineColor 미호출)")

    print("\n╔══ 축 B — CR(보조색×k, 주색), 모집단 %d종 ══╗" % len(pop))
    print("  %-6s %8s %8s %8s   %s" % ("배수", "최악", "최고", "3.0미달", "최악 아이템"))
    for k in KS:
        v = sorted((CRf(mul(s, k), f01(p)), "%s %s" % (sl, key)) for sl, i, key, st, p, s, f in pop)
        print("  ×%-5.2f %8.3f %8.3f %8d   %s" % (k, v[0][0], v[-1][0],
                                                  sum(1 for x, _ in v if x < FLOOR), v[0][1]))
    lo, hi = 0.01, 1.0
    for _ in range(60):
        m = (lo + hi) / 2
        if min(CRf(mul(s, m), f01(p)) for *_, p, s, f in pop) >= FLOOR: lo = m
        else: hi = m
    print("  ★ 모집단 전원이 3.0을 넘는 **최대 배수 = %.4f**  (×0.35 여유 %+.4f · ×0.30 여유 %+.4f)"
          % (lo, lo - 0.35, lo - 0.30))
    for k in (0.35, 0.30):
        bad = sorted((CRf(mul(s, k), f01(p)), "%s %s" % (sl, key))
                     for sl, i, key, st, p, s, f in pop if CRf(mul(s, k), f01(p)) < FLOOR)
        print("  ×%.2f 미달 %d건: %s" % (k, len(bad), ", ".join("%s %.3f" % (n, c) for c, n in bad) or "(없음)"))

    print("\n╔══ 참고 — 제외된 종의 값(모집단이었다면 어땠는가) ══╗")
    for sl, i, key, st, p, s, f in skipped:
        print("  %-4s %-11s ×0.35 %.3f · ×0.30 %.3f"
              % (sl, key, CRf(mul(s, 0.35), f01(p)), CRf(mul(s, 0.30), f01(p))))


if __name__ == "__main__":
    main()
