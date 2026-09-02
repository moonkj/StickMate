#!/usrr/bin/env python3
# -*- coding: utf-8 -*-
"""
레이아웃 넘침 계산기 (localization / 2026-09-02 R2)

★ 이 계산기의 성질 — 왜 "추정"이 아니라 "산수"인가
   이 저장소의 문자 폭 모형 6곳은 상자 폭을 **문자열 .Length**로 정한다(폰트에 묻지 않는다).
   그래서 **상자 폭과 누적 배치는 폰트와 무관하게 정확히 계산된다.**
   폰트가 필요한 것은 "글리프가 그 상자 안에 들어가는가"뿐이고, 라틴에서는 모형이
   과대 배정하므로 글리프는 항상 들어간다 → **넘침은 상자끼리의 문제**다.

★ 교정(CALIBRATION) — 깨지면 아래 숫자를 전부 폐기한다.
   프로덕션에서 읽어 온 상수로 한국어 현행 배치를 재현하고, 알려진 사실과 대조한다.
"""
import sys

# ---- 프로덕션에서 그대로 옮겨 온 상수 (출처를 각 줄에 적는다) ----
# Interaction/SettingsWindow.cs
SW_PanelWidth      = 720.0   # :50
SW_ContentPadX     = 20.0    # :73
SW_TabPadX         = 10.0    # :191
SW_TabLabelCharW   = 11.0    # :188
SW_TabBadgeGap     = 8.0     # :213 (= UiChrome.Space2)
SW_FontCaption     = 10      # UiChrome.cs:455
SW_Space1          = 4.0     # UiChrome.cs:44
SW_TabNames_ko     = ["일반", "캐릭터", "이벤트", "접근성 · 성능", "데이터"]   # :182
SW_Ready           = [True, True, False, False, False]   # IsTabReady: General/Character 만
SW_BadgeWord_ko    = "준비 중"   # SettingsControls.cs:158
SW_QuitButtonWidth = 132.0   # :1518

# Interaction/CharacterInfoWindow*.cs
CI_RightPadX       = 22.0    # cs:139
CI_RightContentW   = 754.0   # cs:140
CI_TabGap          = 22.0    # cs:143
CI_FontTitle       = 14      # UiChrome.cs:452
CI_TabNames_ko     = ["장비", "외형", "보관함", "상점"]   # Tabs.cs:95-98
CI_CaptionKoAdv    = 11.0    # cs:327
CI_InvDescX        = 184.0   # cs:332
CI_InvListW        = 754.0 - 24.0 - 8.0        # cs:323  RightContentWidth - InventoryRailWidth - Space2
CI_StatusSlotW     = 96.0    # cs:322
CI_Space2          = 8.0

# Core/ShortcutLabel.cs
MAC_MODS = "⌃⌥⌘"            # :36
WIN_MODS = "Ctrl+Alt+Win+"   # :40


def sw_tabbar(names, ready, badge_word):
    """설정창 탭바 — 마지막 탭의 오른쪽 끝(pt). SettingsWindow.cs:957-1014 그대로."""
    badge_w = len(badge_word) * SW_FontCaption            # :217
    x = SW_ContentPadX
    per = []
    for i, n in enumerate(names):
        label_w = len(n) * SW_TabLabelCharW               # :964
        w = SW_TabPadX * 2 + label_w + (0.0 if ready[i] else SW_TabBadgeGap + badge_w)
        per.append((n, w))
        x += w + SW_Space1
    return x - SW_Space1, per, badge_w


def ci_tabbar(names):
    """정보창 탭바 — 마지막 탭의 오른쪽 끝(pt). Tabs.cs:233-262 + :282."""
    x = CI_RightPadX
    per = []
    for n in names:
        w = len(n) * CI_FontTitle + 4.0                   # :282
        per.append((n, w))
        x += w + CI_TabGap
    return x - CI_TabGap, per


def ci_desc_chars():
    """보관함 설명 칸 글자수 상한. CharacterInfoWindow.cs:333-339."""
    width = CI_InvListW - CI_InvDescX - CI_StatusSlotW - CI_Space2
    import math
    return width, max(8, math.floor(width / CI_CaptionKoAdv))


def main():
    print("=" * 78)
    print("CALIBRATION — 한국어 현행 배치를 프로덕션 상수로 재현한다")
    print("=" * 78)
    ok = True

    end, per, bw = sw_tabbar(SW_TabNames_ko, SW_Ready, SW_BadgeWord_ko)
    print("설정창 탭바(한국어): 끝 %.0fpt / 한계(PanelWidth) %.0fpt / 여유 %.0fpt"
          % (end, SW_PanelWidth, SW_PanelWidth - end))
    for n, w in per:
        print("    %-16s %6.0fpt" % (n, w))
    print("    배지 '%s' 폭 = %d자 x %dpt = %.0fpt" % (SW_BadgeWord_ko, len(SW_BadgeWord_ko), SW_FontCaption, bw))
    if end >= SW_PanelWidth:
        print("  !! 교정 실패 — 현행 한국어가 이미 넘친다는 결과. 상수 전사 오류."); ok = False

    cend, cper = ci_tabbar(CI_TabNames_ko)
    limit = CI_RightPadX + CI_RightContentW
    print("정보창 탭바(한국어): 끝 %.0fpt / 밑줄 한계 %.0fpt / 여유 %.0fpt" % (cend, limit, limit - cend))
    for n, w in cper: print("    %-8s %6.0fpt" % (n, w))
    # ★ 알려진 값 대조: Tabs.cs:282 주석 "탭 4개 = 22..230pt, 폭 1042에서는 776pt"
    if abs(cper[0][1] - 32.0) > 0.01:
        print("  !! 교정 실패 — [장비] 상자가 32pt(=2자x14+4)가 아니다."); ok = False
    if abs(limit - 776.0) > 0.01:
        print("  !! 교정 실패 — 밑줄 한계가 주석의 776pt와 다르다: %.0f" % limit); ok = False
    else:
        print("    OK  밑줄 한계 776pt = Tabs.cs:282 주석의 실측값과 일치")
    if abs(cend - 230.0) > 0.01:
        print("  !! 교정 실패 — 마지막 탭 끝이 주석의 230pt와 다르다: %.0f" % cend); ok = False
    else:
        print("    OK  마지막 탭 끝 230pt = Tabs.cs:282 주석의 실측값과 일치")

    w, ch = ci_desc_chars()
    print("보관함 설명 칸: 폭 %.0fpt / 글자수 상한 %d자" % (w, ch))
    if ch != 39:
        print("  !! 교정 실패 — CharacterInfoWindow.cs:337 주석이 '폭 1042에서 39자'라고 적었다: %d" % ch); ok = False
    else:
        print("    OK  39자 = CharacterInfoWindow.cs:337 주석과 일치")

    print()
    if not ok:
        print("★★ 교정이 깨졌다. 아래 영어 계산을 전부 폐기한다."); sys.exit(1)
    print("★ 교정 3/3 통과 — 아래 숫자를 쓴다.")
    print()

    print("=" * 78)
    print("영어 — 예시 문안으로 같은 식을 돌린다 (문안은 ux-designer 소관, 여기서는 길이만 쓴다)")
    print("=" * 78)
    cases = [
        ("풀네임",   ["General", "Character", "Events", "Accessibility & Performance", "Data"], "Coming soon"),
        ("중간",     ["General", "Character", "Events", "Accessibility", "Data"], "Coming soon"),
        ("최단",     ["General", "Character", "Events", "Access.", "Data"], "Soon"),
    ]
    for tag, names, badge in cases:
        end, per, bw = sw_tabbar(names, SW_Ready, badge)
        over = end - SW_PanelWidth
        print("[%s] 배지='%s'(%.0fpt)  끝 %.0fpt  %s %.0fpt"
              % (tag, badge, bw, end, "초과" if over > 0 else "여유", abs(over)))
        for n, ww in per: print("      %-30s %6.0fpt" % (n, ww))

    # 한계 문자수 역산
    for badge in ("Coming soon", "Soon"):
        bw = len(badge) * SW_FontCaption
        fixed = SW_ContentPadX + 5 * (SW_TabPadX * 2) + 3 * (SW_TabBadgeGap + bw) + 4 * SW_Space1
        maxL = (SW_PanelWidth - fixed) / SW_TabLabelCharW
        print("  → 배지 '%s'일 때 탭 5개 이름의 **총 글자수 상한 = %.1f자** (한국어 현행 %d자)"
              % (badge, maxL, sum(len(n) for n in SW_TabNames_ko)))

    print()
    for tag, names in [("정보창 풀네임", ["Equipment", "Appearance", "Inventory", "Shop"]),
                       ("정보창 최단",   ["Gear", "Looks", "Items", "Shop"])]:
        cend, cper = ci_tabbar(names)
        over = cend - limit
        print("[%s] 끝 %.0fpt / 한계 %.0fpt / %s %.0fpt"
              % (tag, cend, limit, "초과" if over > 0 else "여유", abs(over)))
        for n, ww in cper: print("      %-12s %6.0fpt" % (n, ww))
    fixed = CI_RightPadX + 4 * 4.0 + 3 * CI_TabGap
    print("  → 탭 4개 이름의 **총 글자수 상한 = %.1f자** (한국어 현행 %d자)"
          % ((limit - fixed) / CI_FontTitle, sum(len(n) for n in CI_TabNames_ko)))

    print()
    print("=" * 78)
    print("ShortcutLabel — Windows 표기가 macOS보다 %d자 길다" % (len(WIN_MODS) - len(MAC_MODS)))
    print("=" * 78)
    for plat, mods in (("macOS", MAC_MODS), ("Windows", WIN_MODS)):
        for lang, tmpl in (("한국어", "지금 종료 (%sQ)"), ("영어", "Quit now (%sQ)")):
            s = tmpl % mods
            han = sum(1 for c in s if '가' <= c <= '힣')
            other = len(s) - han
            print("  %-8s %-6s '%s'  한글 %d자 + 나머지 %d자" % (plat, lang, s, han, other))
    print("  버튼 폭 %dpt. 한글 1자를 12pt, 라틴 1자를 a pt로 보면 폭 = 12*han + a*other." % SW_QuitButtonWidth)
    for a in (5.5, 6.0, 6.5, 7.0):
        s = "Quit now (%sQ)" % WIN_MODS
        print("     영어/Windows, a=%.1f -> %.0fpt  (%s)"
              % (a, a * len(s), "초과" if a * len(s) > SW_QuitButtonWidth else "여유"))
    print("  ※ a(라틴 평균 자폭)는 이 머신에서 측정 불가 — 러너의 Text.preferredWidth로만 확정된다.")


if __name__ == "__main__":
    main()
