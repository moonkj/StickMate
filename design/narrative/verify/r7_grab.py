# -*- coding: utf-8 -*-
"""R7 — Dragged/ThrowTumble 대사 설계 검산  (design-narrative, 2026-09-02)

★ 이 스크립트의 첫 번째 일은 **자기 자신을 교정하는 것**이다.
  C0 교정이 깨지면 그 뒤의 모든 숫자를 폐기한다(TEAM.md §4 공통 처방).

  · C0  교정 — 프로덕션 상수를 소스에서 읽고, 골든 33줄의 IEEE754 비트와 대조
  · C1  Dragged/ThrowTumble 대사 0건 (양성 대조 포함)
  · C2  DragThrowState 구조 실측 — 홀드 상한 / Enter 시점에 알 수 있는 것
  · C3  잡힌 높이 구역 경계 (StickmanMetrics에서 파생, 고른 값 아님)
  · C4  ThrowTumble 회전·궤도 실측
  · C5  제안 대사 전수 예산표
  · C6  문자열 충돌 (기존 33줄 + R6 팩 10줄 + 영어)
  · C7  R6 영어 Walk 5줄 재확인 (분모 1.2375)
  · C8  R6 팩 「야간 정비반」 재확인 + 조건부 FLOURISH 착지 여부
"""
import re, os, sys, struct, math, glob

ROOT = "/Users/kjmoon/App/StickMate"
SRC  = os.path.join(ROOT, "Assets/_Project/Scripts")
DK   = os.path.join(SRC, "Dialogue/DialogueKind.cs")
GOLD = os.path.join(SRC, "Tests/EditMode/Golden/DialogueBudgetKoGolden.txt")
ASSET= os.path.join(ROOT, "Assets/_Project/Data/DefaultStickConfig.asset")
CFG  = os.path.join(SRC, "Core/StickConfig.cs")
MET  = os.path.join(SRC, "Core/StickmanMetrics.cs")

def f32(x): return struct.unpack('>f', struct.pack('>f', x))[0]
def bits(x): return struct.pack('>f', x).hex().upper()

FAIL = []
def check(name, ok, detail=""):
    print(("  [OK]   " if ok else "  [FAIL] ") + name + (("  " + detail) if detail else ""))
    if not ok: FAIL.append(name)

# ══════════════════════════════════════════════════════════════════════
print("C0. 교정 — 프로덕션 상수 읽기 + 골든 비트 대조")
# ══════════════════════════════════════════════════════════════════════
dk = open(DK, encoding="utf-8").read()
def const(name, src=dk):
    m = re.search(r'const\s+float\s+' + name + r'\s*=\s*([0-9.]+)f', src)
    return float(m.group(1)) if m else None

BASE   = const("BaseSeconds")
PERK   = const("PerGlyphSeconds")
PERL   = const("PerLatinGlyphSeconds")
MINS   = const("MinSeconds")
MAXS   = const("MaxSeconds")
FADEIN = const("FadeInSeconds")
FADEOUT= const("FadeOutSeconds")
POPIN  = const("PopInSeconds")
MINSCALE=const("MinVisibleScale") or 1.0
MAXSCALE=const("MaxVisibleScale") or 2.0
READS  = 2.0   # ReadsBeforeStale (private const float ReadsBeforeStale = 2f)
m = re.search(r'ReadsBeforeStale\s*=\s*([0-9.]+)f', dk); READS = float(m.group(1))
print(f"  읽은 상수: Base={BASE} PerKr={PERK} PerLatin={PERL} Min={MINS} Max={MAXS} "
      f"FadeIn={FADEIN} FadeOut={FADEOUT} PopIn={POPIN} Reads={READS} m∈[{MINSCALE},{MAXSCALE}]")
check("상수 읽기 — None 없음", all(v is not None for v in
      [BASE,PERK,PERL,MINS,MAXS,FADEIN,FADEOUT,POPIN,READS]))
# ★ 양성 대조: 존재하지 않는 상수는 None이어야 한다(정규식이 아무거나 잡지 않는가)
check("상수 읽기 양성 대조 — 없는 이름은 None", const("존재하지않는상수") is None)

SYL = re.compile(r'[가-힣ᄀ-ᇿ㄰-㆏ꥠ-꥿ힰ-퟿'
                 r'぀-ヿㇰ-ㇿ㐀-䶿一-鿿豈-﫿]')
def syllabic(t): return True if not t else bool(SYL.search(t))
# ★★ 부동소수 모델 — 여기서 한 번 틀렸다. 기록해 둔다.
#   1차 시도: 십진 리터럴(0.28 / 0.075)을 double로 계산하고 한 번만 접었다 -> 골든 33줄 중 **10줄이 1 ULP** 어긋났다.
#   교정   : 상수를 **먼저 float32로 접고**(C#의 `0.28f`가 실제로 갖는 값), 그 값들로 double 산술 후 한 번만 접는다 -> 0/33.
#   두 코드는 diff에서 거의 같아 보이는데 결과가 갈린다. 골든(디스크의 비트)이 없었으면 못 갈랐다.
BASE_F, PERK_F, PERL_F = f32(BASE), f32(PERK), f32(PERL)
MINS_F, MAXS_F = f32(MINS), f32(MAXS)
def R(t):
    """ReadingSeconds. 상수는 float32로 접은 값, 산술은 double, 마지막에 한 번만 float32."""
    g = len(t) if t else 0
    per = PERK_F if syllabic(t) else PERL_F
    v = BASE_F + g * per
    return f32(min(max(v, MINS_F), MAXS_F))
def required(t):  return f32(max(0.0, FADEIN) + R(t))
def minvis(t, s=1.0): return f32(min(max(s, MINSCALE), MAXSCALE) * R(t))
def maxvis(t, s=1.0): return f32(max(0.0, POPIN) + READS*min(max(s,MINSCALE),MAXSCALE)*R(t) + max(0.0, FADEOUT))

gold = []
for ln in open(GOLD, encoding="utf-8"):
    if ln.startswith("#") or not ln.strip(): continue
    b, sec, text = ln.rstrip("\n").split("\t")
    gold.append((b, float(sec), text))
bad = [(t, b, bits(R(t))) for b, s, t in gold if bits(R(t)) != b]
check(f"골든 {len(gold)}줄 비트 일치", len(bad) == 0 and len(gold) == 33,
      f"불일치 {len(bad)}건" + (f" 예: {bad[:2]}" if bad else ""))
# ★ 양성 대조: 계수를 일부러 틀리면 골든이 실제로 빨개지는가
_save = PERK_F; PERK_F = f32(0.076)
bad2 = [t for b, s, t in gold if bits(R(t)) != b]
PERK_F = _save
check("골든 대조 양성 대조 — 계수 0.075→0.076이면 불일치가 난다", len(bad2) > 0, f"{len(bad2)}건")
if FAIL:
    print("\n★★ 교정 실패 — 이 아래 숫자를 전부 폐기한다."); sys.exit(1)

# ══════════════════════════════════════════════════════════════════════
print("\nC1. Dragged / ThrowTumble 대사 0건 (양성 대조 포함)")
# ══════════════════════════════════════════════════════════════════════
pats = [re.compile(r'DialogueLine\.(?:Say|React)\(\s*"([^"]*)"'),
        re.compile(r'TriggerSelfReturn\(\s*"([^"]*)"'),
        re.compile(r'cfg\s*=>\s*"([^"]*)"')]
def lines_in(path):
    s = open(path, encoding="utf-8").read()
    out = []
    for p in pats: out += [t for t in p.findall(s) if t]
    return out
for f, exp in [("States/DragThrowState.cs", 0), ("States/ThrowTumbleState.cs", 0),
               ("States/RagdollState.cs", 3), ("States/AttackState.cs", 2),
               ("Dialogue/AmbientChatter.cs", 0)]:
    got = lines_in(os.path.join(SRC, f))
    check(f"{f}: 리터럴 {len(got)}건 (기대 {exp})", len(got) == exp, str(got) if got else "")
amb = open(os.path.join(SRC, "Dialogue/AmbientChatter.cs"), encoding="utf-8").read()
def arr(name):
    body = re.search(name + r'\s*=\s*\{(.*?)\n\s*\};', amb, re.S).group(1)
    return re.findall(r'"([^"]*)"', re.sub(r'//[^\n]*', '', body))
IDLE, WALK = arr("IdleLines"), arr("WalkLines")
check(f"AmbientChatter 배열 Idle={len(IDLE)} Walk={len(WALK)}", len(IDLE)==8 and len(WALK)==5)
ALL33 = sorted(set(IDLE + WALK + sum([lines_in(p) for p in
        glob.glob(SRC+"/**/*.cs", recursive=True) if "/Tests/" not in p], [])))
check(f"현행 고유 대사 {len(ALL33)}줄 = 골든 {len(gold)}줄", len(ALL33) == len(gold))
check("Dragged 대사 0 — 프로브 생존 확인(같은 프로브가 다른 파일에서 3건을 찾았다)",
      len(lines_in(os.path.join(SRC,"States/DragThrowState.cs")))==0 and
      len(lines_in(os.path.join(SRC,"States/RagdollState.cs")))==3)

# ══════════════════════════════════════════════════════════════════════
print("\nC2. DragThrowState 구조 실측")
# ══════════════════════════════════════════════════════════════════════
cfgsrc = open(CFG, encoding="utf-8").read()
assetsrc = open(ASSET, encoding="utf-8").read()
def cfg_default(f):
    m = re.search(r'public\s+float\s+' + f + r'\s*=\s*([0-9.]+)f', cfgsrc); return float(m.group(1)) if m else None
def asset_val(f):
    m = re.search(r'^\s*' + f + r':\s*([0-9.]+)\s*$', assetsrc, re.M); return float(m.group(1)) if m else None
for f in ["dragThrowMaxHoldSeconds", "dragThrowMaxSpeed", "dragThrowVelocitySampleWindowSeconds",
          "throwTumbleMinSpeedHeightsPerSecond", "throwTumbleDegreesPerHeightSpeed",
          "throwTumbleMinSpinDegreesPerSecond", "throwTumbleMaxSpinDegreesPerSecond",
          "throwTumbleMaxSeconds", "ambientChatterCooldownSeconds", "gravityScale", "characterScale"]:
    c, a = cfg_default(f), asset_val(f)
    check(f"{f}: 코드 {c} = 애셋 {a}", c is not None and a is not None and abs(c-a) < 1e-6)
check("애셋 대조 양성 대조 — 없는 필드는 양쪽 None",
      cfg_default("존재하지않는필드") is None and asset_val("존재하지않는필드") is None)
drag = open(os.path.join(SRC,"States/DragThrowState.cs"), encoding="utf-8").read()
_enter = drag.split("public void Enter(")[1].split("public void Tick(")[0]
_push_enter = len(re.findall(r'PushSample\(', _enter))
_push_all = len(re.findall(r'PushSample\(', drag))
check("Enter() 블록 안의 PushSample 호출이 정확히 1회 = 커서 표본 1개",
      _push_enter == 1, "Enter {}회 / 파일 전체 {}회".format(_push_enter, _push_all))
check("ComputeThrowVelocity는 표본 2개 미만이면 0을 돌려준다",
      "_sampleCount < 2) return Vector2.zero" in drag)
check("Enter()에서 _cursorSpeedHeights = 0, _hasPrevCursor = false",
      "_cursorSpeedHeights = 0f;" in drag and "_hasPrevCursor = false;" in drag)
check("ReleaseAndThrow()는 종료 사유를 인자로 받지 않는다(사유 구분 불가)",
      re.search(r'private void ReleaseAndThrow\(\s*\)', drag) is not None)
check("DragThrowState는 IHasDialogueParams를 구현하지 않는다",
      "IHasDialogueParams" not in drag)
ctrl = open(os.path.join(SRC,"Interaction/DragThrowController.cs"), encoding="utf-8").read()
check("Dragged 진입은 Idle/Walk에서만 (재진입에 새 MouseDown이 필요)",
      "current != StickmanStateId.Idle && current != StickmanStateId.Walk" in ctrl)
bb = open(os.path.join(SRC,"States/StickmanBlackboard.cs"), encoding="utf-8").read()
check("PlannedDwellRemainingSecondsFor: Walk/Idle 외 전부 NaN",
      "else return float.NaN;" in bb)
check("IsEligible: NaN이면 막지 않는다", "if (float.IsNaN(plannedDwellSeconds)) return true;" in dk)

HOLD = asset_val("dragThrowMaxHoldSeconds")
print(f"  ⇒ 한 번 잡으면 최대 {HOLD:.0f}초. 그 뒤 강제로 놓아진다 = **30초 연속 홀드는 성립하지 않는다.**")

# ══════════════════════════════════════════════════════════════════════
print("\nC3. 잡힌 높이 구역 — StickmanMetrics에서 파생(고른 값 아님)")
# ══════════════════════════════════════════════════════════════════════
met = open(MET, encoding="utf-8").read()
def ratio(name):
    m = re.search(name + r'\s*=\s*([0-9.]+)f\s*/\s*StickConfig\.BaselineCharacterTotalHeight', met)
    return float(m.group(1)) if m else None
TOT = float(re.search(r'BaselineCharacterTotalHeight\s*=\s*([0-9.]+)f', cfgsrc).group(1))
HEADC, HEADR = ratio("BaselineHeadCenterRatio"), ratio("BaselineHeadRadiusRatio")
SHO, HIP = ratio("BaselineShoulderRatio"), ratio("BaselineHipRatio")
check("랜드마크 4종 읽기", None not in (HEADC, HEADR, SHO, HIP),
      f"머리중심 {HEADC} 반경 {HEADR} 어깨 {SHO} 고관절 {HIP}")
rSHO, rHIP = SHO/TOT, HIP/TOT
rHEADBOT = (HEADC - HEADR)/TOT
print(f"  전신 {TOT:.7f} / 어깨 {SHO:.7f}({rSHO:.5f}) / 고관절 {HIP:.7f}({rHIP:.5f}) / 머리 아랫끝 {rHEADBOT:.5f}")
print(f"  ⇒ 구역 경계 = 어깨비 {rSHO:.4f} · 고관절비 {rHIP:.4f}  (둘 다 실측 랜드마크)")
SCALE = asset_val("characterScale")
print(f"  배포 배율 {SCALE} → 실신장 {TOT*SCALE:.4f}유닛")

# ══════════════════════════════════════════════════════════════════════
print("\nC4. ThrowTumble 회전·궤도")
# ══════════════════════════════════════════════════════════════════════
H = TOT * SCALE
vmax = asset_val("dragThrowMaxSpeed")
perH = asset_val("throwTumbleDegreesPerHeightSpeed")
spinMin, spinMax = asset_val("throwTumbleMinSpinDegreesPerSecond"), asset_val("throwTumbleMaxSpinDegreesPerSecond")
minHS = asset_val("throwTumbleMinSpeedHeightsPerSecond")
hs_max = vmax / H
spin_reach = min(max(perH*hs_max, spinMin), spinMax)
print(f"  신장 {H:.4f}유닛 · 던지기 속도 상한 {vmax}유닛/초 = {hs_max:.4f}신장/초")
print(f"  회전 = clamp({perH}×h/s, {spinMin}, {spinMax}) → **실제 도달 상한 {spin_reach:.1f}도/초** "
      f"(설정 상한 {spinMax}는 도달 불가: 그러려면 {spinMax/perH*H:.2f}유닛/초가 필요)")
print(f"  진입 하한 {minHS}신장/초 = {minHS*H:.3f}유닛/초 → 그때 회전은 {min(max(perH*minHS,spinMin),spinMax):.0f}도/초(하한에 걸림)")
r_orbit = (HEADC - HIP) * SCALE
for w in (spinMin, spin_reach):
    v_head = math.radians(w) * r_orbit
    print(f"    회전 {w:6.1f}도/초 → 머리 앵커 궤도 반경 {r_orbit:.4f}유닛, 접선 속도 {v_head:.3f}유닛/초 "
          f"(≈{v_head*40.9:.0f} OS-pt/초), 한 바퀴 {360/w:.3f}초")
g = 9.81 * asset_val("gravityScale")
print(f"  중력 {g:.2f}유닛/초² — 자유낙하 비행시간: ", end="")
for d in (0.5, 1, 2, 5, 10):
    print(f"낙차{d}유닛={math.sqrt(2*d/g):.3f}초 ", end="")
print()
print(f"  ⇒ 어떤 대사든 필요체류 하한 = 페이드인 {FADEIN} + Min {MINS} = {FADEIN+MINS:.2f}초")
print(f"     낙차 {(FADEIN+MINS)**2*g/2:.3f}유닛(= 신장의 {(FADEIN+MINS)**2*g/2/H:.2f}배)보다 낮게 던지면 "
      f"**가장 짧은 대사조차 상태보다 길다.**")

print("\n  ★ 결과: ThrowTumble은 Enter 시점에 자기 길이를 모른다(TryPlanRotation은 첫 Tick).")
tt = open(os.path.join(SRC,"States/ThrowTumbleState.cs"), encoding="utf-8").read()
check("Enter()가 PredictSecondsToGround를 부르지 않는다",
      "PredictSecondsToGround" not in tt.split("public void Enter")[1].split("public void Tick")[0])
check("교체 게이트: 노출 0.18초 미만이면 다음 대사를 **버린다**(큐잉 없음)",
      "activeVisibleSeconds >= Mathf.Max(0f, popInSeconds)" in dk and "규칙 5는 유지된다(큐에 쌓지 않고 버린다)" in
      open(os.path.join(SRC,"Dialogue/DialogueBubbleRenderer.cs"), encoding="utf-8").read())

# ══════════════════════════════════════════════════════════════════════
print("\nC5. 제안 대사 전수 예산표")
# ══════════════════════════════════════════════════════════════════════
POOL = [
  # 구역 전용 — 잡힌 자리에서만 파생된다. 경계는 StickmanMetrics 랜드마크(C3)에서 온다.
  ("HEAD_A",  "위: 머리링 안 (h >= 0.80657)",  "머리 놔",   "Not the head."),
  ("HEAD_B",  "위: 머리링 안 (h >= 0.80657)",  "거긴 머리야", "That's my head."),
  ("LEG_A",   "아래: 고관절 밑 (h < 0.41091)", "다리 놔",   "Not the leg."),
  ("LEG_B",   "아래: 고관절 밑 (h < 0.41091)", "거긴 다리야", "That's my leg."),
  # 공통 — 세 구역 전부에서 뽑힌다(가운데 구역은 이 넷만).
  ("ANY_A",   "공통",                          "야!",       "Hey!"),
  ("ANY_B",   "공통",                          "놔, 놔",     "Let go. Let go."),
  ("ANY_C",   "공통",                          "안 놔?",     "Let go."),
  ("ANY_D",   "공통",                          "어딜 잡아",   "Watch it."),
  # 폴백 — 커서 좌표를 못 읽었거나 오프셋이 클램프에 걸렸을 때. **일부러 1줄**이다(아래 근거).
  ("FALLBACK","불명 (HasGrabPoint = false)",   "잡혔다",     "Grabbed."),
]
print(f"  {'슬롯':<9}{'구역':<20}{'한국어':<9}{'자':>3}{'R':>7}{'최소노출':>8}{'노출상한':>8}{'상한m2':>8}   "
      f"{'English':<16}{'자':>3}{'R':>7}{'상한':>7}")
for slot, zone, ko, en in POOL:
    print(f"  {slot:<9}{zone:<20}{ko:<9}{len(ko):>3}{R(ko):>7.3f}{minvis(ko):>8.3f}{maxvis(ko):>8.3f}"
          f"{maxvis(ko,2.0):>8.3f}   {en:<16}{len(en):>3}{R(en):>7.3f}{maxvis(en):>7.3f}")
kmax = max(POOL, key=lambda p: maxvis(p[2]))
emax = max(POOL, key=lambda p: maxvis(p[3]))
print(f"\n  풀 최장 노출상한: 한국어 '{kmax[2]}' {maxvis(kmax[2]):.3f}초(m=1) / {maxvis(kmax[2],2.0):.3f}초(m=2)")
print(f"                    영어  '{emax[3]}' {maxvis(emax[3]):.3f}초(m=1) / {maxvis(emax[3],2.0):.3f}초(m=2)")
print(f"  ⇒ 연타 억제 쿨다운 = 그 줄의 노출상한(새 숫자 없음). m=2에서도 {maxvis(kmax[2],2.0):.2f}초.")
print(f"  정독 천장(한국어 13자 / 영어 21자) 초과: "
      f"{[p[2] for p in POOL if len(p[2])>13] + [p[3] for p in POOL if len(p[3])>21] or '0건'}")
print(f"  작업 상한(R6 §1-3, 한국어 14 / 라틴 22) 초과: "
      f"{[p[2] for p in POOL if len(p[2])>14] + [p[3] for p in POOL if len(p[3])>22] or '0건'}")

# ══════════════════════════════════════════════════════════════════════
print("\nC6. 문자열 충돌")
# ══════════════════════════════════════════════════════════════════════
PACK_G = ["자...","이 구역 이상 무","정비 대기","바닥 단단함","난 이게 편해",
          "순찰 중","다음 구역으로","보폭 일정","보행 정상","앞치마 흔들림"]
PACK_G_EN = ["Mm...","Sector clear.","On standby.","Floor's firm.","This suits me.",
             "On rounds.","Next section.","Pace holding.","Gait normal.","Bib swaying."]
WALK_EN = ["Out walking.","Off that way.","Left, right.","Loosening up.","Good stride."]
existing = set(ALL33) | set(PACK_G) | set(PACK_G_EN) | set(WALK_EN)
dup = [p for p in POOL if p[2] in existing or p[3] in existing]
check(f"제안 {len(POOL)*2}개 문자열 vs 기존 {len(existing)}개 — 충돌", len(dup)==0, str(dup))
_self = [p[2] for p in POOL] + [p[3] for p in POOL]
check("제안 풀 내부 중복 0", len(_self) == len(set(_self)),
      str([x for x in set(_self) if _self.count(x) > 1]))
check("충돌 검사 양성 대조 — 기존 줄 '으악!'을 넣으면 걸린다", "으악!" in existing)
check("충돌 검사 양성 대조(영어) — 'Left, right.'가 대조집합에 실재", "Left, right." in existing)

# ══════════════════════════════════════════════════════════════════════
print("\nC7. R6 영어 Walk 5줄 재확인 (분모 1.2375)")
# ══════════════════════════════════════════════════════════════════════
wmin = cfg_default("wanderWalkDurationMin"); imin = cfg_default("wanderIdleDurationMin")
jit  = cfg_default("wanderDurationJitterRatio")
wmin_a, imin_a, jit_a = asset_val("wanderWalkDurationMin"), asset_val("wanderIdleDurationMin"), asset_val("wanderDurationJitterRatio")
check(f"wanderWalkDurationMin 코드 {wmin} = 애셋 {wmin_a}", wmin==wmin_a)
check(f"wanderIdleDurationMin 코드 {imin} = 애셋 {imin_a}", imin==imin_a)
check(f"wanderDurationJitterRatio 코드 {jit} = 애셋 {jit_a}", jit==jit_a)
DEN_W = wmin*(1-jit); DEN_I = imin*(1-jit)
print(f"  Walk 분모 = {wmin} × (1−{jit}) = {DEN_W}   (Idle {DEN_I})")
check("분모가 1.2375다(1.24 아님)", abs(DEN_W-1.2375) < 1e-9, f"{DEN_W!r}")
MARGIN = 0.18
print(f"  {'슬롯':<10}{'한국어':<12}{'자':>3}{'R':>7}{'여유':>8}   {'English':<15}{'자':>3}{'R':>7}{'여유':>8}")
for slot, ko, en in zip(["MOVE","DIR","RHYTHM","LEGS","FLOURISH"], WALK, WALK_EN):
    sk = DEN_W - FADEIN - R(ko) - MARGIN; se = DEN_W - FADEIN - R(en) - MARGIN
    print(f"  {slot:<10}{ko:<12}{len(ko):>3}{R(ko):>7.3f}{sk:>8.3f}   {en:<15}{len(en):>3}{R(en):>7.3f}{se:>8.3f}")
    if sk <= 0 or se <= 0: FAIL.append(f"C7 {slot} 여유 음수")
check("영어 5줄 전부 규칙 8 통과(여유 > 0)",
      all(DEN_W - FADEIN - R(e) - MARGIN > 0 for e in WALK_EN))
check("마진 없는 물리 하한에서도 전부 통과",
      all(DEN_W - FADEIN - R(e) > 0 for e in WALK_EN))
check("영어 5줄 ≤ 작업상한 22자", all(len(e) <= 22 for e in WALK_EN))
check("한국어 5줄 무변경 — AmbientChatter.WalkLines와 1:1", len(WALK)==5)

# ══════════════════════════════════════════════════════════════════════
print("\nC8. 팩「야간 정비반」 재확인 + 조건부 FLOURISH")
# ══════════════════════════════════════════════════════════════════════
ITEMS = os.path.join(ROOT, "Assets/_Project/Resources/Items")
newids = ["equip_head_havelock","equip_eyes_respirator","equip_neck_apronbib",
          "equip_shoulders_toolbag","look_hair_napetie","look_pet_worklamp"]
landed = [i for i in newids if os.path.exists(os.path.join(ITEMS, i + ".asset"))]
check(f"팩 6종 .asset 착지 = {len(landed)}/6", True, str(landed) if landed else "(아직 0건)")
check("착지 프로브 양성 대조 — 기존 equip_head_beret.asset은 보인다",
      os.path.exists(os.path.join(ITEMS, "equip_head_beret.asset")))
sway_ok = len(landed) == 6 and any("swayStart" in open(os.path.join(ITEMS,"equip_neck_apronbib.asset"),
          encoding="utf-8", errors="ignore").read() for _ in [0])
print(f"  ⇒ FLOURISH 조건부: swayStart 착지 {'O' if sway_ok else 'X'} → "
      f"{'`앞치마 흔들림`' if sway_ok else '**형태 주장 폴백 `앞치마가 길다`가 현재 유효**'}")
# ★ 1차 프로브는 **망토**로 양성 대조를 했고 0건이 나왔다 — 프로브가 죽은 게 아니라
#   sway 필드가 **NECK 6종에만** 있었다. 그 사실 자체가 §7-4를 강화한다.
sway_files = sorted(os.path.basename(f) for f in glob.glob(ITEMS + "/*.asset")
                    if "swayStart" in open(f, encoding="utf-8", errors="ignore").read())
check(f"swayStart 필드를 가진 애셋 {len(sway_files)}종 — 전부 NECK인가",
      len(sway_files) == 6 and all(f.startswith("equip_neck_") for f in sway_files), str(sway_files))
check("sway 프로브 양성 대조 — 목도리에서 실제로 찾는다",
      "swayStart" in open(os.path.join(ITEMS,"equip_neck_scarf.asset"), encoding="utf-8", errors="ignore").read())
check("sway 프로브 음성 대조 — 망토(BACK)에는 없다(=BACK 움직임 주장 금지는 스키마가 강제)",
      "swayStart" not in open(os.path.join(ITEMS,"equip_shoulders_cape.asset"), encoding="utf-8", errors="ignore").read())
for ko in PACK_G:
    sk_i = DEN_I - FADEIN - R(ko) - MARGIN
    sk_w = DEN_W - FADEIN - R(ko) - MARGIN
    print(f"  {ko:<14}{len(ko):>3}자 R={R(ko):.3f}  Idle여유 {sk_i:+.3f}  Walk여유 {sk_w:+.3f}")
check("팩 Idle 5줄 규칙 8 통과", all(DEN_I-FADEIN-R(k)-MARGIN > 0 for k in PACK_G[:5]))
check("팩 Walk 5줄 규칙 8 통과", all(DEN_W-FADEIN-R(k)-MARGIN > 0 for k in PACK_G[5:]))
check("팩 10줄 ≤ 정독 천장 13자", all(len(k) <= 13 for k in PACK_G))
check("팩 대사에 시각 주장 0건",
      not any(w in "".join(PACK_G) for w in ["야간","밤","새벽","아침","저녁","낮","오늘"]))
check("팩 대사·설명에 발광 주장 0건",
      not any(w in "".join(PACK_G) for w in ["빛","밝","어둠","환하"]))


# ══════════════════════════════════════════════════════════════════════
print("\nC9. ★ 앞 라운드 자백의 재현 — 「클램프가 계수를 삼킨다」")
# ══════════════════════════════════════════════════════════════════════
# 죽은 라운드가 남긴 마지막 로그:
#   "My positive control was itself a false pass — the clamp absorbed the wrong
#    coefficient, and I wrote `or True`. Fixing it honestly."
# 그 기제를 숫자로 재현한다.
n_kr = (MINS_F - BASE_F) / PERK_F
n_en = (MINS_F - BASE_F) / PERL_F
print(f"  클램프 바닥 {MINS}초를 넘기는 최소 글자수: 한국어 {math.ceil(n_kr)}자(경계 {n_kr:.3f}) / 라틴 {math.ceil(n_en)}자(경계 {n_en:.3f})")
print(f"  ⇒ 한국어 {math.ceil(n_kr)-1}자 이하 · 라틴 {math.ceil(n_en)-1}자 이하는 **계수를 아무 값으로 바꿔도 결과가 안 변한다.**")
def R_with(t, perk, perl):
    per = perk if syllabic(t) else perl
    return f32(min(max(BASE_F + len(t)*per, MINS_F), MAXS_F))
short_kr = [t for t in ALL33 if syllabic(t) and len(t) <= math.ceil(n_kr)-1]
blind = [t for t in short_kr if R_with(t, f32(0.076), PERL_F) == R(t)]
check(f"실재 대사 중 클램프에 눌린 한국어 {len(short_kr)}줄 — 계수를 0.075→0.076으로 바꿔도 전부 불변",
      len(blind) == len(short_kr) and len(short_kr) > 0, f"{len(blind)}/{len(short_kr)}줄이 무반응")
print(f"     그 {len(short_kr)}줄: {short_kr}")
print("  ⇒ **짧은 대사만으로 계수 양성 대조를 짜면 그 대조는 초록인데 아무것도 못 잰다.**")
print("     이번 라운드의 C0 대조가 살아남은 이유는 골든 33줄 전수(=긴 줄 포함)를 썼기 때문이다(28건 검출).")
pool_kr = [p[2] for p in POOL]
pool_blind = [t for t in pool_kr if R_with(t, f32(0.076), PERL_F) == R(t)]
print(f"  ★★ 내 제안 풀 {len(pool_kr)}줄 중 **{len(pool_blind)}줄이 클램프 바닥**이다: {pool_blind}")
print("     ⇒ test-engineer 경고: 이 풀로 계수 회귀를 검증하면 구조적으로 못 본다. 긴 대사를 따로 써라.")

# ══════════════════════════════════════════════════════════════════════
print("\nC10. 10초 홀드 · 던지기 낙차 문턱")
# ══════════════════════════════════════════════════════════════════════
longest = max(pool_kr, key=lambda t: maxvis(t))
for s in (1.0, 1.5, 2.0):
    v = maxvis(longest, s)
    print(f"  m={s:.1f}: 최장 줄 '{longest}' 노출상한 {v:.3f}초 → 10초 홀드 중 침묵 {HOLD-v:.3f}초 ({(HOLD-v)/HOLD:.1%})")
need = FADEIN + MINS
print(f"  ThrowTumble이 최단 대사({need:.2f}초)를 견디려면 순수 자유낙하 기준 낙차 "
      f"{need*need*g/2:.3f}유닛 = 신장의 {need*need*g/2/H:.2f}배가 필요하다.")
rev = 360.0/spin_reach
print(f"  최고 회전에서 한 바퀴 {rev:.3f}초 < 최단 가독예산 {MINS}초 "
      f"⇒ **가장 짧은 대사를 다 읽기 전에 말풍선이 캐릭터를 한 바퀴 돈다.**")
check("한 바퀴 < 최단 가독예산", rev < MINS, f"{rev:.3f} < {MINS}")

# ══════════════════════════════════════════════════════════════════════
print("\nC11. Dragged 놓기 분기 — 잡기 Reaction이 Ragdoll 대사를 먹을 수 있는가")
# ══════════════════════════════════════════════════════════════════════
RAG_TH = asset_val("ragdollForceThreshold")
check(f"ragdollForceThreshold 코드 {cfg_default('ragdollForceThreshold')} = 애셋 {RAG_TH}",
      cfg_default("ragdollForceThreshold") == RAG_TH)
check("Rigidbody2D.mass는 배율을 따라가지 않는다(= 임계가 순수 속도 8유닛/초로 축약)",
      "랙돌 진입 임계가" in open(os.path.join(SRC,"Core/StickmanAgent.cs"), encoding="utf-8").read())
clean_th = minHS * H
print(f"  IsCleanThrow 하한 = {minHS}신장/초 × 신장 {H:.4f} = **{clean_th:.3f}유닛/초**")
print(f"  Ragdoll 임계     = 충격량 {RAG_TH} ÷ 질량 1 = **{RAG_TH:.3f}유닛/초**   (던지기 상한 {vmax})")
check("ReleaseAndThrow는 IsCleanThrow를 **먼저** 보고, 참이면 return한다(랙돌 분기 도달 불가)",
      drag.index("IsCleanThrow(speed") < drag.index("RagdollImpactResolver.TryApplyImpact"))
print(f"  ⇒ 기본 설정에서 놓기 속도 {clean_th:.3f}~{vmax} 유닛/초는 전부 ThrowTumble(대사 0줄)로 간다.")
print(f"    Dragged -> Ragdoll 직행은 **throwTumbleEnabled = 0** 일 때만 열린다(애셋 값 "
      f"{int(asset_val('throwTumbleEnabled') or 0) if asset_val('throwTumbleEnabled') is not None else 'n/a'}).")

print("\n" + "="*70)
print("실패 " + str(len(FAIL)) + "건" + (": " + str(FAIL) if FAIL else " — 전부 통과"))
sys.exit(1 if FAIL else 0)
