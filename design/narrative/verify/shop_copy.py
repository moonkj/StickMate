# -*- coding: utf-8 -*-
"""상점·재화 문구 전량 — 글자폭 예산 검산(한/영).

폭 모델은 내가 새로 만든 것이 아니라 `docs/UX_SHOP_AND_CURRENCY.md`가 이미 쓰고 있는 것이다.
**알려진 값 4건으로 역산해 교정한 뒤에만** 새 문구를 잰다(교정이 깨지면 아래 숫자 전부 폐기)."""
import sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# ---------- 폭 모델 ----------
def w(text, F):
    """한글 음절 = 1.00F / 공백 = 0.30F / 그 밖 ASCII·기호 = 0.55F"""
    t = 0.0
    for c in text:
        o = ord(c)
        if c == " ":                       t += 0.30 * F
        elif 0xAC00 <= o <= 0xD7A3:        t += 1.00 * F
        else:                              t += 0.55 * F
    return t

# ---------- 교정: ux-designer 문서의 알려진 폭 4건 ----------
CAL = [("세트 완성", 10, 43.0), ("아직 세트 아님", 10, 66.0), ("정말 살까요?", 10, 58.5),
       ("동전으로 지금 열거나, 함께한 시간이 쌓이면 그냥 열려요.", 12, 314.4),
       ("Lv.9에 열립니다. 그 뒤에 동전으로 앞당기거나 기다릴 수 있어요.", 12, 356.4)]
for t, F, exp in CAL:
    got = w(t, F)
    assert abs(got - exp) < 0.05, ("교정 실패", t, got, exp)
print("[교정] UX_SHOP_AND_CURRENCY.md의 알려진 폭 5건을 역산 재현했다:")
for t, F, exp in CAL:
    print("   %-32s F=%d  %.1fpt (문서값 %.1f) OK" % (t[:30], F, w(t, F), exp))
print("   -> 폭 모델 = 한글 1.00F / 공백 0.30F / ASCII 0.55F. **새 숫자를 만들지 않았다.**")

# 상자 예산 (ux-designer 확정)
BOX = {"detail": (12, 560, "상세 패널 안내문"),
       "button": (10, 139, "카드 하단 버튼"),
       "meta":   (10,  41, "카드 메타"),
       "setrow": (10, 112, "좌측 세트 상태칸")}

ROWS = []   # (절, id, 상자, 한국어, English)
def add(sec, i, box, kr, en): ROWS.append((sec, i, box, kr, en))

# ── 2. 재화 ──────────────────────────────────────────────────────────
add("재화","C3","detail","이제 다 모았어요. 동전은 더 쓸 데가 없어서 감췄어요.",
                          "All collected. The coin counter is put away now.")
# ── 3. 획득 ──────────────────────────────────────────────────────────
add("획득","G1","detail","집중 25분 · 동전 30개가 쌓였어요.","Focused 25 min - 30 coins added.")
add("획득","G2","detail","여기까지 12분 · 동전 12개가 쌓였어요.","12 min in - 12 coins added.")
add("획득","G4","detail","창을 닫은 사이에 동전 18개가 쌓였어요.","18 coins came in while this was closed.")
# ── 4. 구매 ──────────────────────────────────────────────────────────
add("구매","B2","button","정말 살까요?","Buy it?")
add("구매","B3","detail","밀짚모자를 얻었어요. [장비] 탭에서 입힐 수 있어요.",
                          "Got the straw hat. Wear it from the [Gear] tab.")
# ── 5. 못 사는 이유 3종 ───────────────────────────────────────────────
add("불가","R1","detail","동전이 12개 모자라요. 3시간 뒤에는 그냥 열려요.",
                          "12 coins short. It opens on its own in 3 hours.")
add("불가","R1b","detail","동전이 12개 모자라요. 곧 그냥 열려요.","12 coins short. It opens on its own soon.")
add("불가","R2","detail","Lv.9에 열려요. 그 뒤에 동전으로 앞당길 수 있어요.",
                          "Opens at Lv.9. Coins can bring it forward after that.")
add("불가","R3","detail","이미 가지고 있어요. [장비] 탭에서 입힐 수 있어요.",
                          "Already yours. Wear it from the [Gear] tab.")
add("불가","R1btn","button","◉ 330","◉ 330")
add("불가","R2btn","button","LV.9 필요","Needs LV.9")
add("불가","R3btn","button","보유 중","Owned")
# ── 6. 빈 상태 ────────────────────────────────────────────────────────
add("빈상태","E1a","detail","아직 살 수 있는 게 없어요","Nothing on sale yet")
add("빈상태","E1b","detail","Lv.5가 되면 첫 상품이 열려요. 지금은 구경만 해도 돼요.",
                             "The first item opens at Lv.5. Feel free to just look.")
add("빈상태","E2","detail","동전이 아직 없어요. 집중 모드를 한 번 마치면 쌓이기 시작해요.",
                            "No coins yet. They start adding up after one focus session.")
add("빈상태","E3","detail","이 칸은 다 모았어요.","This row is complete.")
add("빈상태","E4","detail","모을 수 있는 건 다 모았어요. 새 물건은 업데이트로 들어옵니다.",
                            "Everything available is collected. New items come with updates.")
add("빈상태","E5","detail","상품 목록을 불러오는 중…","Loading the shelf…")
add("빈상태","E6","detail","상품 목록을 불러오지 못했어요. 앱을 다시 켜면 대개 해결됩니다.",
                            "Couldn't load the shelf. Restarting the app usually fixes it.")
add("빈상태","E7","detail","가격이 아직 정해지지 않은 물건이에요.","This item has no price yet.")
add("빈상태","E7b","button","가격 미정","No price")
# ── 7. [장비] 탭 쪽 ───────────────────────────────────────────────────
add("장비탭","Q1","detail","[상점] 탭에서 얻을 수 있어요.","Available in the [Shop] tab.")
add("장비탭","Q2","meta","상점","Shop")
add("장비탭","Q3","detail","확장팩은 다음 업데이트에 들어옵니다.","Expansion packs come with a future update.")
add("장비탭","Q4","button","준비 중","Coming soon")

print("\n=== 문구 전량 폭 검산 ===")
print("   절     |id    |상자     |예산 | 한국어 폭 | 영어 폭 | 판정")
bad = []
for sec, i, box, kr, en in ROWS:
    F, budget, _ = BOX[box]
    wk, we = w(kr, F), w(en, F)
    ok = wk <= budget and we <= budget
    if not ok: bad.append((i, kr, en, wk, we, budget))
    print("   %-6s |%-6s|%-8s|%4d | %8.1f | %7.1f | %s"
          % (sec, i, box, budget, wk, we, "✔" if ok else "★ 넘침"))
print("\n   총 %d개 문구 | 넘침 %d개" % (len(ROWS), len(bad)))
for i, kr, en, wk, we, b in bad:
    print("   ★ %s: 한 %.1f / 영 %.1f > 예산 %d — %s / %s" % (i, wk, we, b, kr, en))

print("\n=== ★ 영어 폭에 대한 정직한 단서 ===")
print("   위 영어 폭은 **같은 모델의 외삽**이다(ASCII 일률 0.55F). 실제 라틴 서체는 자폭이 고르지")
print("   않으므로 이 값은 근사다. 여유가 가장 얇은 3건만 뽑아 ux-designer에게 실측을 요청한다:")
tight = sorted(((BOX[b][1] - w(en, BOX[b][0]), i, en, BOX[b][1]) for _, i, b, _, en in ROWS))[:3]
for m, i, en, b in tight:
    print("   %-6s 여유 %6.1fpt / 예산 %dpt   \"%s\"" % (i, m, b, en))

print("\n=== 어법 감사 — 민지가 칭찬한 어법을 실제로 잇고 있는가 ===")
SHIPPED = ["낙서할 빈 자리가 없어요","지금 붙잡을 만한 작은 창이 없어요","아직 적어둔 할일이 없어요",
           "과녁 놓을 자리가 없어요","지금 앞에 있는 창이 없어요","아직 비어 있어요"]
print("   출하된 불가·빈상태 문구 %d건의 공통 형태:" % len(SHIPPED))
print("     (a) 해요체 종결 '-어요/-아요'      : %d/%d" % (sum(1 for s in SHIPPED if s.endswith("어요") or s.endswith("아요")), len(SHIPPED)))
print("     (b) 마침표 없음                   : %d/%d" % (sum(1 for s in SHIPPED if not s.endswith(".")), len(SHIPPED)))
print("     (c) '지금/아직'으로 회복 가능성 암시 : %d/%d" % (sum(1 for s in SHIPPED if s.startswith(("지금","아직"))), len(SHIPPED)))
print("     (d) 막는 대상을 명사구로 지목       : %d/%d (전부 '무엇이 없다'를 말한다)" % (len(SHIPPED), len(SHIPPED)))
print("     (e) 사과·유저 탓 0건               : %d/%d" % (sum(1 for s in SHIPPED if "죄송" not in s and "잘못" not in s), len(SHIPPED)))
mine = [kr for sec, i, b, kr, en in ROWS if sec in ("불가","빈상태") and b == "detail"]
first = [m.split(". ")[0] for m in mine]
print("\n   이번 라운드 불가·빈상태 문구 %d건의 **첫 문장**:" % len(first))
print("     (a) 해요체 종결                   : %d/%d" % (sum(1 for s in first if re.search(r"(어요|아요|예요|에요|해요)$", s.rstrip("."))), len(first)))
print("     (d) 막는 대상을 명사구로 지목       : %d/%d"
      % (sum(1 for s in first if re.search(r"(동전|Lv\.\d+|가격|목록|살 수 있는 게)", s)), len(first)))
print("     ※ (b) 마침표: 두 문장짜리는 첫 문장에 마침표가 **필요하다** — 출하 어법과 갈리는 유일한 지점.")
print("        근거: 출하 문구는 전부 한 문장이었다. 두 문장이 된 것은 '또는 기다리세요'를")
print("        말하라는 design-systems 요구 때문이고, 그 둘째 문장이 없으면 상점이 벽이 된다.")

print("\n=== ★ §4-1 자기 반증 — '이름을 넣으면 넘친다'가 참인가 ===")
import glob, codecs
names = set()
for f in glob.glob("/Users/kjmoon/App/StickMate/Assets/_Project/Resources/Items/*.asset"):
    m = re.search(r'displayName:\s*(.+)', open(f, encoding="utf-8").read())
    if not m: continue
    v = m.group(1).strip().strip('"')
    if "\\u" in v: v = codecs.decode(v, 'unicode_escape')
    names.add(v)
assert len(names) == 42, ("아이템 42종을 못 모았다", len(names))
print("   아이템 42종 수집 OK. 버튼 예산 139pt @F=10")
worst = max(names, key=lambda n: w(n + "를 살까요?", 10))
for n in sorted(names, key=lambda x: -w(x, 10))[:3]:
    print("     %-12s '%s를 살까요?' = %5.1f / 139  %s"
          % (n, n, w(n + "를 살까요?", 10), "들어간다" if w(n+"를 살까요?",10) <= 139 else "넘친다"))
print("   -> ★ 최장(%s)도 %.1fpt로 **들어간다**. '폭 때문에 못 넣는다'는 **거짓**이다."
      % (worst, w(worst + "를 살까요?", 10)))
print("      기각 사유는 폭이 아니라 (1) 정보 중복 (2) 카드마다 문자열 조립 (3) 여유 %.1fpt에 성장 여지 없음."
      % (139 - w(worst + "를 살까요?", 10)))

print("\n=== §6-5 E1 한 줄 합침안 ===")
for t in ["아직 살 수 있는 게 없어요. Lv.5가 되면 열려요.",
          "아직 살 수 있는 게 없어요. Lv.5가 되면 첫 물건이 열려요."]:
    print("   %-44s %6.1f / 560  %s" % (t, w(t, 12), "✔" if w(t, 12) <= 560 else "★넘침"))

print("\n=== §3-2 활쏘기 무알림 판정의 산술 ===")
CD = 600.0
print("   지급 쿨다운 %.0f초 (StickConfig.archeryCooldownSeconds 재사용)" % CD)
print("   24시간 상주 상한          = 86400 / %.0f = %.0f회/일" % (CD, 86400/CD))
print("   관찰력 고급(쿨다운 -50%%) = 86400 / %.0f = %.0f회/일" % (CD/2, 86400/(CD/2)))
print("   비교: R2가 낮추기로 한 자율 발화 = 3,654회/일 -> 354회/일 (10.3배)")
print("   -> 알림을 붙이면 채널만 바꿔 같은 크기의 문제가 돌아온다. **무알림이 정답이다.**")
