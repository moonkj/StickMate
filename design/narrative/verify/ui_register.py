# -*- coding: utf-8 -*-
"""출하된 **화면 문구**의 말투(종결형) 전수 조사 — 상점 문구가 어느 어법을 이어야 하는가.
문서가 아니라 코드에서 센다. 로그/주석은 화면 문구가 아니므로 수집 대상이 아니다."""
import re, os, sys, io, glob
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
SRC = "/Users/kjmoon/App/StickMate/Assets/_Project/Scripts"
files = [f for f in glob.glob(os.path.join(SRC, "**/*.cs"), recursive=True) if "/Tests/" not in f]

# ★ 화면에 닿는 경로만: (a) `.text = "..."`  (b) 불가 사유 상수  (c) 탭 안내문 상수
# ★ 보간 문자열($"...")도 포함한다 — 첫 판에서 이걸 빼먹어 상세 패널 문구를 통째로 놓쳤다.
PATS = [re.compile(r'\.text\s*=\s*\$?"([^"\\\n]{2,90})"'),
        re.compile(r'const string \w*(?:Reason|Notice|Text|Title|Body|Caption)\w*\s*=\s*"([^"\\\n]{2,90})"'),
        re.compile(r'(?:notice|disabledNote|reason)\s*:\s*\$?"([^"\\\n]{2,90})"'),
        re.compile(r'[?:]\s*\$?"([^"\\\n]{2,90})"'),
        re.compile(r'return\s+\$?"([^"\\\n]{2,90})"')]
found = {}
for f in files:
    for line in open(f, encoding="utf-8"):
        s = line.strip()
        if s.startswith("//") or s.startswith("*"): continue
        for p in PATS:
            for m in p.finditer(line):
                for g in m.groups():
                    if g and re.search(r"[가-힣]", g): found[g] = os.path.basename(f)

END_HAEYO  = re.compile(r"요[.!?]?$")
END_HAPSYO = re.compile(r"니다[.!?]?$")
hae = {t: f for t, f in found.items() if END_HAEYO.search(t)}
hap = {t: f for t, f in found.items() if END_HAPSYO.search(t)}
neut = {t: f for t, f in found.items() if t not in hae and t not in hap}

MUST_HAE = ["낙서할 빈 자리가 없어요", "아직 비어 있어요", "정한 시간 동안 옆에서 지켜볼게요."]
MUST_HAP = ["상점은 다음 업데이트에 들어옵니다.",
            "레벨 {entry.RequiredLevel}이 되면 열립니다. 지금은 실루엣만 보입니다."]
miss = [t for t in MUST_HAE if t not in hae] + [t for t in MUST_HAP if t not in hap]
assert not miss, ("양성 대조 실패", miss)
print("[양성대조] 알려진 해요체 3건 · 합쇼체 1건을 각각 옳게 분류했다 — 아래 숫자를 믿어도 된다")
NOT_UI = ["말할 시간이 없으면", "가설"]
assert not any(any(x in t for x in NOT_UI) for t in found), "음성 대조 실패 — 로그 문장이 섞였다"
print("[음성대조] 로그 문장이 수집물에 섞이지 않았다")

print("\n=== 화면 문구 종결형 전수 (수집 %d건) ===" % len(found))
print("   해요체 %2d건 / 합쇼체 %2d건 / 종결형 없음(라벨·명사구) %2d건" % (len(hae), len(hap), len(neut)))
print("   문장으로 끝나는 것만 세면: 해요체 %d / 합쇼체 %d  ->  해요체 %.0f%%"
      % (len(hae), len(hap), 100*len(hae)/max(1, len(hae)+len(hap))))

print("\n   [합쇼체 전량 — 소수파라 전부 나열한다]")
for t in sorted(hap): print("     %-56s (%s)" % (t, hap[t]))
print("\n   [해요체 전량]")
for t in sorted(hae): print("     %-56s (%s)" % (t, hae[t]))

print("\n=== 가설 검정 ===")
road = [t for t in hap if "업데이트" in t]
print("   H1 '합쇼체는 제품 로드맵 전용이다' : 합쇼체 %d건 중 '다음 업데이트' 계열 %d건 (%.0f%%)"
      % (len(hap), len(road), 100*len(road)/max(1,len(hap))))
for t in sorted(t for t in hap if t not in road):
    print("      반례: %s  (%s)" % (t, hap[t]))

print("\n=== ★ 표면을 갈라서 다시 센다 — '어디에 쓰인 말투인가' ===")
FACE = {"TodoBoardPopover.cs","FocusSessionPopover.cs","ActionCommandPopover.cs","CharacterInfoWindow.cs",
        "CharacterInfoWindow.Cards.cs","CharacterInfoWindow.Tabs.cs","CharacterInfoWindow.Inventory.cs",
        "GraffitiDirector.cs","ArcheryDirector.cs","WindowTheftDirector.cs","WindowCrashDirector.cs",
        "TodoReminderDirector.cs","CommandAvailability.cs","StickMateDisplayNames.cs",
        "GearRadialMenuWidget.cs","RunawayDirector.cs","ItemCatalog.cs"}
SETTINGS = {"SettingsWindow.cs","SettingsControls.cs","InfoGearIconWidget.cs","AppControlDirector.cs"}
def bucket(f):
    if f in FACE: return "① 캐릭터 표면"
    if f in SETTINGS: return "② 설정창"
    return "③ 진단·시스템 보고"
tab = {}
for t, f in found.items():
    if t not in hae and t not in hap: continue
    b = bucket(f); r = "해요체" if t in hae else "합쇼체"
    tab.setdefault(b, {"해요체": 0, "합쇼체": 0})[r] += 1
print("   표면                 | 해요체 | 합쇼체 | 해요체 비율")
for b in sorted(tab):
    h, p_ = tab[b]["해요체"], tab[b]["합쇼체"]
    print("   %-20s | %5d | %5d | %.0f%%" % (b, h, p_, 100*h/max(1, h+p_)))
print("\n   ★ ① 캐릭터 표면의 합쇼체 반례 전량 — 상점 문구가 바로 이 표면에 들어간다:")
for t, f in sorted(found.items()):
    if t in hap and bucket(f) == "① 캐릭터 표면":
        print("     %-56s (%s)" % (t, f))
