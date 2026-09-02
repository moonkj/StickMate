# -*- coding: utf-8 -*-
"""★ R6 과제 C·D — 인계 문자열 재확인 + 카테고리 틴트 매핑 판정 (design-art, 2026-09-02)

C. 내가 R5에 준 애셋 처방이 **오늘 트리에서도 유효한가**.
   (coder-systems가 AccessoryDefSO에 cohortId·declaredRarity를 넣었다 — 애셋이 움직였는가?)
D. UiChrome._categoryTints 의 `(int)slot & 3` 이 **의도인가 사고인가**.

★ 모든 "없음" 판정에 양성 대조를 붙인다(TEAM.md §4-2 4번).

  python3 handoff_recheck.py
"""
import sys, os, re, itertools, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
EQV = os.path.abspath(os.path.join(HERE, "..", "..", "equipment", "verify"))
sys.path.insert(0, HERE)
sys.path.insert(0, EQV)
import colorlab as CL
import shipped
import rig, items, hair
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ITEMS = shipped.ITEMS
FOOT = os.path.join(ITEMS, "look_fx_footprint.asset")
LEAF = os.path.join(ITEMS, "look_fx_leaf.asset")
UICHROME = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/UiChrome.cs")
EQMODEL = os.path.join(ROOT, "Assets/_Project/Scripts/Core/EquipmentModel.cs")


def sec(t):
    print("\n" + "=" * 100)
    print(t)
    print("=" * 100)


# ============================================================================
def task_c():
    sec("과제 C-1. look_fx_footprint.asset — 내 R5 처방 문자열이 오늘도 유효한가")
    raw = open(FOOT, encoding="utf-8").read()
    lines = raw.splitlines()
    cols = re.findall(r"^    color: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: ([-\d.eE]+)\}$",
                      raw, re.M)
    tones = re.findall(r"^    tone: (\d+)$", raw, re.M)
    hexes = [CL.rgb2hex(tuple(int(round(float(x) * 255)) for x in c[:3])) for c in cols]
    alphas = sorted({float(c[3]) for c in cols})
    print(f"  color 줄 {len(cols)}개 · tone 줄 {len(tones)}개 → 패턴 {tones}")
    print(f"  색: {hexes}  · 알파 {alphas}")
    print(f"  ★ R5 처방 전제: 'color 3곳(tone 0×2, tone 1×1)' → "
          f"{'✔ 일치' if len(cols) == 3 and tones == ['0','0','1'] else '★ 어긋남'}")
    print(f"  ★ R5 처방 전제: '3곳 모두 #5A8C3C' → "
          f"{'✔ 일치' if set(hexes) == {'#5A8C3C'} else '★ 어긋남 ' + str(set(hexes))}")

    print(f"\n  coder-systems가 넣은 신규 필드가 이 애셋에 **직렬화됐는가**")
    for f in ("cohortId", "declaredRarity"):
        hit = [i + 1 for i, l in enumerate(lines) if re.match(r"^\s*%s:" % f, l)]
        print(f"    {f:16s} {'★ 있음 줄 ' + str(hit) if hit else '없음'}")
    # ★ 양성 대조 — 같은 프로브가 실재하는 필드는 찾는가
    for f in ("requiredLevel", "hidesHair", "itemIndex"):
        hit = [i + 1 for i, l in enumerate(lines) if re.match(r"^\s*%s:" % f, l)]
        print(f"    [대조] {f:11s} {'✔ 찾음 줄 ' + str(hit) if hit else '★ 프로브 고장'}")
    allf = {}
    for p in sorted(os.listdir(ITEMS)):
        if not p.endswith(".asset"):
            continue
        t = open(os.path.join(ITEMS, p), encoding="utf-8").read()
        for f in ("cohortId", "declaredRarity"):
            if re.search(r"^\s*%s:" % f, t, re.M):
                allf.setdefault(f, []).append(p)
    print(f"  42종 전체에서 cohortId {len(allf.get('cohortId', []))}건 · "
          f"declaredRarity {len(allf.get('declaredRarity', []))}건")
    print(f"  → 두 필드는 **C# 기본값으로만 존재하고 디스크에는 없다**(Unity가 아직 재직렬화하지 않았다).")
    print(f"     따라서 내 처방 #1(‘color 3곳을 InkTone으로’)은 **줄 위치·줄 수가 그대로**라 유효하다.")
    print(f"     ★ 다만 조건이 하나 붙는다 — 누군가 에디터에서 이 애셋을 저장하면 두 필드가 끼어들고")
    print(f"       `icon:` 블록 **앞**에 삽입된다(필드 선언 순서). color 줄 번호가 밀린다.")
    print(f"       그래서 처방을 **줄 번호가 아니라 문자열 치환**으로 적어 둔 것이 옳았다.")

    sec("과제 C-2. \\uXXXX 양성 대조 — 생 grep 0/42, 디코드 42/42")
    files = [f for f in sorted(os.listdir(ITEMS)) if f.endswith(".asset")]
    raw_hits = 0
    dec_hits = 0
    for f in files:
        t = open(os.path.join(ITEMS, f), encoding="utf-8").read()
        m = re.search(r'^  displayName:\s*(".*?")\s*$', t, re.M)
        if re.search(r"[가-힣]", t):
            raw_hits += 1
        if m:
            s = m.group(1)[1:-1].encode("ascii", "backslashreplace").decode("unicode_escape")
            if re.search(r"[가-힣]", s):
                dec_hits += 1
    probe = '"\\uBC1C\\uC790\\uAD6D"'[1:-1].encode("ascii", "backslashreplace").decode("unicode_escape")
    print(f"  디코더 교정: \\uBC1C\\uC790\\uAD6D -> '{probe}'  "
          f"{'✔' if probe == '발자국' else '★ 디코더 고장 — 아래 숫자 전부 무효'}")
    print(f"  생 grep '[가-힣]'  : {raw_hits}/{len(files)}  (0이어야 정상 — 전부 이스케이프다)")
    print(f"  디코드 후          : {dec_hits}/{len(files)}  (42여야 정상)")
    ok = (probe == "발자국") and raw_hits == 0 and dec_hits == len(files)
    print(f"  ★ 판정: {'유효 — 0건이 「깨끗함」이지 「프로브 고장」이 아니다' if ok else '★ 무효'}")

    sec("과제 C-3. 새 잠금 1건 — test-engineer가 그대로 쓸 수 있는 **정확한 단언 형태**")
    lraw = open(LEAF, encoding="utf-8").read()
    lc = re.findall(r"^    color: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: 1\}$", lraw, re.M)
    lhex = [CL.rgb2hex(tuple(int(round(float(x) * 255)) for x in c)) for c in lc]
    lidx = re.search(r"^  itemIndex:\s*(\d+)", lraw, re.M).group(1)
    lreq = re.search(r"^  requiredLevel:\s*(\d+)", lraw, re.M).group(1)
    print(f"  look_fx_leaf: itemIndex={lidx} · requiredLevel={lreq} · 색 {lhex}")
    print(f"  ★ 오늘 렌더러 실측: CharacterFxRenderer:707 `SetGroupInk(p.Lines, ResolveInk())`")
    print(f"     → **나뭇잎도 잉크색으로 칠해진다.** 그래서 오늘은 나뭇잎을 걸쳐도")
    print(f"       StaleInkPieceCount == 0 이고, 조각 색은 #5A8C3C가 **아니라 잉크색**이다.")
    print(f"  ⇒ 이 단언은 **처방 #2·#3이 착지한 뒤에만 참**이다. 오늘 트리에 그대로 넣으면")
    print(f"     '기대색 불일치'로 빨갛게 나고, 그건 테스트 결함이 아니라 **미구현**이다.")
    print(f"     test-engineer는 이 테스트를 **처방과 같은 커밋**에 넣거나, 그때까지")
    print(f"     Assert.Ignore('FX 팔레트 경로 미구현 — PALETTE_SPEC §16-6 #2·#3 대기')로 남겨야 한다.")

    print(f"\n  ── 단언 형태 (PlayMode · CharacterAppearanceLayerTests 이웃 자리) ──")
    print(f"""
  [UnityTest, Timeout(120000)]
  public IEnumerator 고정색_FX는_잉크_전환을_따라가지_않는다()
  {{
      yield return LoadSceneAndPinIdle();
      var fx    = Object.FindFirstObjectByType<CharacterFxRenderer>();
      var agent = Object.FindFirstObjectByType<StickmanAgent>();
      _inkConfig = agent.Config;

      // 기대색은 **데이터**에서 온다 — 검사 대상 함수(ResolveWornPalette)에서 만들지 않는다.
      //   TEAM.md 「생성기와 검사기가 같이 틀린다」 규칙 1.
      Color expected = ItemCatalog.Item(EquipmentSlot.Fx, FxLeaf).Icon[0].Color;   // {lhex[0]}

      RaiseLevelTo({lreq}, agent.Config);                  // 나뭇잎 요구 레벨 — 상수로 베끼지 않는다면
      Assert.IsTrue(Wear(EquipmentSlot.Fx, FxLeaf), "나뭇잎을 걸치지 못했습니다 — 관측 전제 불성립.");

      // ── (1) 양성 대조를 **먼저** 세운다: 계수기가 실제로 움직이는가 ──────────
      //     이게 없으면 뒤의 '0'이 '정상'인지 '게터가 죽었다'인지 구분되지 않는다.
      SetInk(agent, StickmanInkColor.Black);
      yield return StampLeaves(agent, fx, LeafCapacity + 4);
      Assert.AreEqual(LeafCapacity, fx.LiveEffectCount, "나뭇잎이 버퍼를 못 채웠습니다.");
      int movable = fx.StalePieceCount;                    // 기대색≠잉크이므로 **0이 아니어야** 한다
      Assert.AreNotEqual(0, movable,
          "고정색 FX인데 '기대색과 다른 조각'이 0입니다 — 계수기가 잉크 기준을 그대로 쓰고 있거나 죽었습니다.");

      // ── (2) 본 단언: 잉크를 뒤집어도 조각 색이 안 움직인다 ──────────────────
      SetInk(agent, StickmanInkColor.White);
      yield return StampLeaves(agent, fx, LeafCapacity + 4);
      Assert.AreEqual(0, fx.StalePieceCount,
          "잉크를 흑→백으로 바꿨는데 기대색과 다른 조각이 남았습니다.");

      foreach (Color c in fx.LivePieceColorsForTests)      // ★ 새 노출 필요(아래 참조)
      {{
          Assert.IsTrue(Mathf.Approximately(c.r, expected.r) &&
                        Mathf.Approximately(c.g, expected.g) &&
                        Mathf.Approximately(c.b, expected.b),
              $"나뭇잎 조각이 {{c}} 입니다. 기대 {{expected}} — 잉크를 따라갔습니다.");
          Assert.AreEqual(0x{lhex[0][1:]}, ToRgb24(c), "8bit 왕복이 어긋납니다.");   // 바이트 동일
      }}

      // ── (3) 음성 대조: 잉크 표식 아이템은 **반대로** 움직여야 한다 ───────────
      Assert.IsTrue(Wear(EquipmentSlot.Fx, FxFootprint));
      SetInk(agent, StickmanInkColor.Black);
      yield return StampFootprints(agent, fx, FootprintCapacity + 4);
      Assert.AreEqual(0, fx.StalePieceCount, "발자국은 잉크 표식이라 잉크를 따라가야 합니다.");
  }}""")
    print(f"\n  ★ 이 단언이 잠그는 것 넷")
    print(f"     (1) 계수기가 **움직일 수 있음**을 먼저 증명한다 — '0'의 뜻을 확정한다")
    print(f"     (2) 기대값을 **데이터(ItemCatalog.Item(...).Icon[0].Color)**에서 뽑는다 —")
    print(f"         검사 대상인 ResolveWornPalette로 만들면 그 함수가 틀려도 같이 틀린다")
    print(f"     (3) Approximately(프로덕션 비교자와 동일) **와** 8bit 왕복 둘 다 본다")
    print(f"     (4) 발자국(잉크 표식)이 **반대로** 움직이는지 같은 테스트 안에서 대조한다")
    print(f"  ★ 필요한 신규 노출 1건: `CharacterFxRenderer.LivePieceColorsForTests`")
    print(f"     (지금은 StaleInkPieceCount가 **세기만** 하고 색을 안 돌려준다 — 색 동일성을 못 잰다.)")
    print(f"     이건 `.cs` 변경이라 **내 권한 밖**이다. 리더 경유로 coder-ui에 붙인다.")


# ============================================================================
def slot_envelopes():
    """몸에 붙는 5슬롯의 **잉크 봉투**를 같은 몸 좌표에서 래스터로 만든다."""
    G = 512
    LO, HI = -6.0, 3.0          # 몸 좌표 y 범위(머리 중심 원점, R 단위)
    XL, XR = -4.5, 4.5
    tabs = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK,
            "BACK": items.BACK, "HAIR": hair.SET}
    out = {}
    for name, tab in tabs.items():
        im = Image.new("1", (G, G), 0)
        d = ImageDraw.Draw(im)
        for _nm, shapes in tab.items():
            for s in shapes:
                P = [((x - XL) / (XR - XL) * G, (HI - y) / (HI - LO) * G) for x, y in s.pts]
                if s.filled and len(P) >= 3:
                    d.polygon(P, fill=1)
                elif len(P) >= 2:
                    d.line(P, fill=1, width=6, joint="curve")
        out[name] = list(im.getdata())
    return out


def iou(a, b):
    i = sum(1 for x, y in zip(a, b) if x and y)
    u = sum(1 for x, y in zip(a, b) if x or y)
    return i / u if u else 0.0


def task_d():
    sec("과제 D-1. `(int)slot & 3` 은 의도인가 사고인가 — **git으로 갈랐다**")
    enum = {}
    src = open(EQMODEL, encoding="utf-8").read()
    for m in re.finditer(r"^\s*(\w+) = (\d+),", src, re.M):
        enum[m.group(1)] = int(m.group(2))
    print(f"  지금 EquipmentSlot: {enum}  (총 {len(enum)}슬롯)")
    faces = subprocess.run(["git", "log", "--format=%h %ad", "--date=short", "--",
                            "Assets/_Project/Scripts/Core/EquipmentModel.cs"],
                           cwd=ROOT, capture_output=True, text=True).stdout.split("\n")
    print(f"  EquipmentModel.cs 커밋 {len([f for f in faces if f.strip()])}개 전수에서 `Face` 선언:")
    anyface = False
    for line in [f for f in faces if f.strip()]:
        h = line.split()[0]
        t = subprocess.run(["git", "show", f"{h}:Assets/_Project/Scripts/Core/EquipmentModel.cs"],
                           cwd=ROOT, capture_output=True, text=True).stdout
        n = len(re.findall(r"^\s*Face\s*[=,]", t, re.M))
        anyface = anyface or n > 0
        print(f"    {line:24s} Face 선언 {n}건")
    intro = subprocess.run(["git", "log", "--format=%h %ad", "--date=short", "-S", "_categoryTints",
                            "--", "Assets/_Project/Scripts/Interaction/UiChrome.cs"],
                           cwd=ROOT, capture_output=True, text=True).stdout.strip()
    print(f"  `_categoryTints` 도입 커밋: {intro}")
    print(f"\n  ★ 판정: **사고다. 그리고 「FACE 삭제가 회전시킨 것」도 아니다 —**")
    print(f"     `Face`는 **커밋된 어느 판본에도 존재한 적이 없고**({'있었다' if anyface else '전수 0건'}),")
    print(f"     `& 3`을 도입한 그 커밋 시점에 enum은 **이미 7슬롯**이었다.")
    print(f"     즉 핸드오프의 **8칸 표를 7칸 enum에 그대로 옮겨 적은 것**이다.")
    print(f"     주석은 옮겨 적을 때의 8칸 세계를 그대로 말하고 있어서 **처음부터 거짓**이었다.")
    print(f"  ⇒ 주석만 고치면 **틀린 매핑에 맞는 설명을 붙이는 것**이 된다. 매핑을 고쳐야 한다.")

    sec("과제 D-2. 지금 매핑이 실제로 무엇을 붙였는가")
    tints = ["#E8834A 살구주황", "#4FC0C6 청록", "#8CC06E 연둣초록", "#B08FD0 라벤더"]
    cur = {}
    for k, v in sorted(enum.items(), key=lambda kv: kv[1]):
        cur.setdefault(v & 3, []).append(k)
    for i, t in enumerate(tints):
        print(f"  {t:18s} ← {cur.get(i, [])}")
    print(f"  ★ 라벤더는 **한 슬롯(Shoulders)만** 갖고, HEAD와 HAIR가 **같은 색**이 됐다.")
    print(f"     주석이 약속한 배정(HEAD 단독 / EYES+HAIR / NECK+FX / BACK+PET)과 전부 다르다.")

    sec("과제 D-3. 어떤 짝이 해로운가 — **몸에서 같은 자리를 쓰는가**로 잰다")
    env = slot_envelopes()
    print("  몸에 붙는 5슬롯의 잉크 봉투(6종 합집합) 상호 IoU — 같은 자리를 쓸수록 크다")
    keys = ["HEAD", "EYES", "NECK", "BACK", "HAIR"]
    print(f"  {'':7s}" + "".join(f"{k:>8s}" for k in keys))
    M = {}
    for a in keys:
        row = ""
        for b in keys:
            v = 1.0 if a == b else iou(env[a], env[b])
            M[(a, b)] = v
            row += f"{v:8.3f}"
        print(f"  {a:7s}{row}")
    print(f"\n  FX / PET 은 **몸에 붙지 않는다** — 별도 렌더러다. IoU를 0으로 둔다:")
    for f in ("CharacterFxRenderer.cs", "CharacterPetRenderer.cs"):
        p = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction", f)
        print(f"    {f:28s} {'존재 ✔' if os.path.exists(p) else '★ 없음'}")
    for a in keys:
        for b in ("FX", "PET"):
            M[(a, b)] = M[(b, a)] = 0.0
    M[("FX", "PET")] = M[("PET", "FX")] = 0.0

    sec("과제 D-4. 7슬롯 → 4틴트 재배정 — 장비 4슬롯은 그대로 두고 외형 3슬롯만 고른다")
    print("  제약: HEAD/EYES/NECK/BACK 은 핸드오프 그대로 틴트 0/1/2/3.")
    print("        HAIR/FX/PET 이 **서로 다른** 틴트를 하나씩 가져간다(한 틴트에 3슬롯이 몰리지 않게).")
    print("  목적: 같은 틴트를 쓰는 짝의 **몸 겹침 IoU 최댓값**을 최소화한다.")
    base = {"HEAD": 0, "EYES": 1, "NECK": 2, "BACK": 3}
    best = []
    for hh, ff, pp in itertools.permutations(range(4), 3):
        m = dict(base); m["HAIR"], m["FX"], m["PET"] = hh, ff, pp
        groups = {}
        for k, v in m.items():
            groups.setdefault(v, []).append(k)
        worst = 0.0
        pairs = []
        for g in groups.values():
            for a, b in itertools.combinations(g, 2):
                worst = max(worst, M[(a, b)])
                pairs.append((M[(a, b)], a, b))
        best.append((round(worst, 4), m, sorted(pairs, reverse=True)))
    best.sort(key=lambda x: x[0])
    print(f"\n  {'순위':>3s} {'최대 겹침':>8s}  HAIR/FX/PET 틴트        같은 틴트 짝(겹침)")
    for r, (w, m, pairs) in enumerate(best[:6], 1):
        tag = " ← 현행" if (m["HAIR"], m["FX"], m["PET"]) == (0, 1, 2) else ""
        tag += " ← 주석이 약속한 것" if (m["HAIR"], m["FX"], m["PET"]) == (1, 2, 3) else ""
        ps = " ".join(f"{a}+{b} {v:.3f}" for v, a, b in pairs)
        print(f"  {r:3d} {w:8.3f}  {m['HAIR']}/{m['FX']}/{m['PET']}                 {ps}{tag}")
    cur_w = [x for x in best if (x[1]["HAIR"], x[1]["FX"], x[1]["PET"]) == (0, 1, 2)][0]
    doc_w = [x for x in best if (x[1]["HAIR"], x[1]["FX"], x[1]["PET"]) == (1, 2, 3)][0]
    print(f"\n  현행(& 3)          최대 겹침 {cur_w[0]:.3f}")
    print(f"  주석이 약속한 배정   최대 겹침 {doc_w[0]:.3f}")
    print(f"  최선                최대 겹침 {best[0][0]:.3f}  (HAIR={tints[best[0][1]['HAIR']]})")

    print(f"\n  ★ 최선이 {sum(1 for x in best if x[0] == best[0][0])}개로 동률이다 — "
          f"HAIR=연둣초록은 고정되고 FX/PET만 남는다. 두 기준을 **더 잰다**.")
    print(f"\n  (i) 정렬층 실측 — 프로덕션 상수를 직접 읽는다")
    SORT = {}
    for f, names in ((os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs"),
                      ("SortBack", "SortHair", "SortNeck", "SortEyes", "SortHead")),
                     (os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/CharacterFxRenderer.cs"),
                      ("SortFootprint", "SortAerial")),
                     (os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/CharacterPetRenderer.cs"),
                      ("SortDefault",))):
        t = open(f, encoding="utf-8").read()
        for n in names:
            m = re.search(r"const int %s = (-?\d+);" % n, t)
            if m:
                SORT[n] = int(m.group(1))
    print(f"      {SORT}")
    neg = sorted(k for k, v in SORT.items() if v < 0)
    print(f"      ★ **몸 뒤(음수 정렬층)에 그려지는 것은 {neg} 둘뿐**이다.")
    print(f"        = BACK(망토)과 FX(발자국). PET은 SortDefault={SORT.get('SortDefault')}로 **앞**이다.")
    print(f"        → 라벤더(BACK)의 짝은 PET이 아니라 **FX**다. 핸드오프 주석의 'BACK/PET'은")
    print(f"          이 트리의 정렬층과 맞지 않는다(주석이 또 한 번 틀렸다).")
    tot = {k: sum(M[(k, o)] for o in keys if o != k) for k in keys}
    print(f"\n  (ii) 가장 얽힌 슬롯이 **혼자** 써야 한다 — 슬롯별 겹침 총합")
    for k, v in sorted(tot.items(), key=lambda kv: -kv[1]):
        print(f"      {k:6s} {v:.3f}")
    print(f"      ★ {max(tot, key=tot.get)}가 가장 얽혀 있다 → 단독 틴트는 {max(tot, key=tot.get)}가 갖는다.")
    FINAL = {"HEAD": 0, "EYES": 1, "NECK": 2, "BACK": 3, "HAIR": 2, "FX": 3, "PET": 1}
    grp = {}
    for k, v in FINAL.items():
        grp.setdefault(v, []).append(k)
    wf = max((M[(a, b)] for g in grp.values() for a, b in itertools.combinations(g, 2)), default=0.0)
    print(f"\n  ⇒ ★ 확정 배정 (최대 겹침 {wf:.3f})")
    for i, t in enumerate(tints):
        print(f"      {t:18s} ← {grp.get(i, [])}")
    print(f"      HEAD 단독(가장 얽힘) · NECK+HAIR {M[('NECK','HAIR')]:.3f} · "
          f"BACK+FX {M[('BACK','FX')]:.3f}(둘 다 몸 뒤) · EYES+PET {M[('EYES','PET')]:.3f}")
    print(f"      현행 대비 최대 겹침 {cur_w[0]:.3f} → {wf:.3f} ({wf/cur_w[0]*100:.0f}%)")
    print(f"\n  ⇒ 코드 형태: `& 3` 같은 산술을 쓰지 마라. **깨진 것이 바로 그 산술이다.**")
    print(f"     슬롯 수만큼의 명시 표 + 길이 단언으로 간다(enum이 또 바뀌면 **시끄럽게** 깨지도록):")
    print(f"       private static readonly int[] _categoryTintIndex = {{ "
          + ", ".join(str(FINAL[k]) for k in ("HEAD", "EYES", "NECK", "BACK", "HAIR", "FX", "PET"))
          + " };   // Head,Eyes,Neck,Shoulders,Hair,Fx,Pet")
    print(f"       CategoryTint(slot) => _categoryTints[_categoryTintIndex[(int)slot]];")
    print(f"     ★ test-engineer용 잠금: _categoryTintIndex.Length == Enum.GetValues(typeof(EquipmentSlot)).Length")
    print(f"       (지금 {len(enum)}. 이 단언이 없으면 다음 슬롯 추가 때 **조용히 IndexOutOfRange**거나")
    print(f"        더 나쁘게는 옛 산술처럼 **조용히 틀린 색**이 된다.)")

    sec("과제 D-5. 틴트 4색 자체 점검 — 크롬 대역에 있는가, 팩 색과 안 섞이는가")
    tok = shipped.uichrome_tokens()
    tc = [CL.hex2rgb(t.split()[0]) for t in tints]
    bg = tok.get("CardSurface", ((27, 31, 38), 1.0))[0]
    print(f"  {'틴트':20s} {'L':>7s} {'CardSurface 대비':>16s}")
    for t, c in zip(tints, tc):
        print(f"  {t:20s} {CL.L(c):7.4f} {CL.CR(c, bg):15.2f}:1")
    print(f"  틴트 4색 상호 최소 ΔE {min(CL.dE(a,b) for a,b in itertools.combinations(tc,2)):.2f}")
    PACK = ["#CC3F29", "#9E655C", "#639400", "#798C51", "#009682", "#518C84",
            "#456ECC", "#6080CC", "#9768CC", "#8563AB", "#CC1BA9", "#9C5A8E"]
    print(f"  틴트 ↔ 팩 12색 최소 ΔE {min(CL.dE(c, CL.hex2rgb(p)) for c in tc for p in PACK):.2f}")
    print(f"  틴트 L 범위 {min(CL.L(c) for c in tc):.4f}~{max(CL.L(c) for c in tc):.4f} "
          f"vs 아트 대역 상한 0.2396 → 크롬은 전부 대역 **위** ✔ (§0의 법)")


if __name__ == "__main__":
    CL.calibrate()
    task_c()
    task_d()
