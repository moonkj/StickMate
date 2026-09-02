#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R3 게이트 검산 — design-sound
목적: R1 SILENCE_POLICY §3-1(M1)/§3-2/§3-8 와 R2 SOUND_QUALIFICATION §4-3/§4-4 가
      "묵음 게이트"로 고른 신호가 코드의 실제 의미와 일치하는가를 소스에서 다시 잰다.

★ 이 스크립트는 프로덕션 함수를 부르지 않는다(Unity 없이 돈다).
  판정 규칙을 소스에서 '읽어' 독립 재구현하고, 소스 문자열 존재로 그 재구현을 교정한다.
  (docs/TEAM.md: 기대값을 프로덕션 함수로 만들지 마라 / 생성기와 검사기가 같이 틀린다)
"""
import re, sys, os

ROOT = "/Users/kjmoon/App/StickMate"
SRC = {
    "tier":   f"{ROOT}/Assets/_Project/Scripts/Platform/FullscreenSuspendPolicy.cs",
    "agent":  f"{ROOT}/Assets/_Project/Scripts/Core/StickmanAgent.cs",
    "pres":   f"{ROOT}/Assets/_Project/Scripts/Platform/ViewerPresence.cs",
    "focus":  f"{ROOT}/Assets/_Project/Scripts/Interaction/FocusWatchDirector.cs",
    "audio":  f"{ROOT}/ProjectSettings/AudioManager.asset",
    "save":   f"{ROOT}/Assets/_Project/Scripts/Core/CharacterSaveStore.cs",
    "setwin": f"{ROOT}/Assets/_Project/Scripts/Interaction/SettingsWindow.cs",
}
text = {}
for k, p in SRC.items():
    if not os.path.exists(p):
        print(f"FATAL: 소스 없음 {p}"); sys.exit(2)
    text[k] = open(p, encoding="utf-8").read()

fails, checks = [], 0
def chk(name, cond, detail=""):
    global checks
    checks += 1
    print(f"  [{'OK ' if cond else 'FAIL'}] {name}" + (f"  — {detail}" if detail else ""))
    if not cond: fails.append(name)

print("=" * 78)
print("0. 교정 (calibration) — 알려진 참 6건 + 알려진 거짓 3건(음성 대조)")
print("=" * 78)
# 알려진 참: 이 6건이 깨지면 아래 숫자를 전부 폐기한다.
chk("C1 m_DisableAudio=1 (오디오는 빌드에서 꺼져 있다)", "m_DisableAudio: 1" in text["audio"])
chk("C2 AudioModule 소비 코드 0건 (AudioSource/PlayOneShot)",
    not re.search(r"\bAudioSource\b|\bPlayOneShot\b", text["agent"] + text["pres"] + text["focus"]))
chk("C3 CurrentVersion = 9", "CurrentVersion = 9" in text["save"])
chk("C4 설정창 탭 5개", "TabCount = 5" in text["setwin"])
chk("C5 RecentInputSeconds = 2f", "RecentInputSeconds = 2f" in text["pres"])
chk("C6 ForeignFullscreenTier 3값 존재",
    all(s in text["tier"] for s in ["None = 0", "PanelsOnly = 1", "Full"]))
# 음성 대조: 아래 3건이 '참'으로 나오면 검사기가 고장난 것이다.
chk("N1 (음성) 존재하지 않는 심볼 SoundMasterEnabled 가 코드에 없다",
    "SoundMasterEnabled" not in "".join(text.values()))
chk("N2 (음성) m_DisableAudio: 0 이 아니다", "m_DisableAudio: 0" not in text["audio"])
chk("N3 (음성) TabCount = 4 가 아니다", "TabCount = 4" not in text["setwin"])
if fails:
    print("\n교정 실패 — 이 스크립트의 이후 숫자를 전부 폐기한다."); sys.exit(1)

print()
print("=" * 78)
print("1. 소스에서 읽은 판정 규칙 (독립 재구현의 근거)")
print("=" * 78)
# 규칙을 '소스 문자열'로 확인하고 나서 파이썬으로 재구현한다.
r_resolve = ("if (!coversDisplay) return ForeignFullscreenTier.None;" in text["tier"]
             and "return isGame ? ForeignFullscreenTier.Full : ForeignFullscreenTier.PanelsOnly;" in text["tier"])
r_suspend = "bool shouldSuspend = _fullscreenAutoHide || _userHidden;" in text["agent"]
r_aps     = "public bool ArePanelsSuppressed => _isSuspended || _fullscreenPanelRetreat;" in text["agent"]
r_retreat = "=> tier != ForeignFullscreenTier.None;" in text["tier"]
r_suschar = "=> tier == ForeignFullscreenTier.Full;" in text["tier"]
chk("R1 Resolve(covers,isGame) 규칙 원문 일치", r_resolve)
chk("R2 _isSuspended = _fullscreenAutoHide || _userHidden", r_suspend)
chk("R3 ArePanelsSuppressed = _isSuspended || _fullscreenPanelRetreat", r_aps)
chk("R4 RetreatsPanels(tier) = tier != None", r_retreat)
chk("R5 SuspendsCharacter(tier) = tier == Full", r_suschar)
# 등급 2에서만 캐릭터 숨김 -> _fullscreenAutoHide 는 Full 에서만 참
chk("R6 FramePacing.Suspended 는 _isSuspended 로만 켜진다(Tier1 경로 없음)",
    "Platform.FramePacing.SetSuspended(true);" in text["agent"]
    and "_suspendedNow ? FramePacingTier.Suspended" in open(f"{ROOT}/Assets/_Project/Scripts/Platform/FramePacing.cs", encoding="utf-8").read())

print()
print("=" * 78)
print("2. ★ 진리표 — 현행 게이트(IsSuspended) vs 제안 게이트(ArePanelsSuppressed)")
print("=" * 78)
def resolve(covers, is_game):
    if not covers: return "None"
    return "Full" if is_game else "PanelsOnly"

rows, differ = [], 0
for covers in (False, True):
    for is_game in (False, True):
        for hidden in (False, True):
            tier = resolve(covers, is_game)
            auto_hide   = (tier == "Full")            # SuspendsCharacter
            panel_retr  = (tier != "None")            # RetreatsPanels
            is_susp     = auto_hide or hidden         # _isSuspended
            aps         = is_susp or panel_retr       # ArePanelsSuppressed
            cur_silent  = is_susp                     # R1 M1/M8, R2 §4-4 가 고른 것
            new_silent  = aps                         # 제안
            d = (cur_silent != new_silent)
            differ += d
            rows.append((covers, is_game, hidden, tier, is_susp, aps, cur_silent, new_silent, d))

print(f"{'덮음':<5}{'게임':<5}{'숨김':<5}{'등급':<12}{'IsSusp':<8}{'APS':<7}{'현행':<7}{'제안':<7}차이")
for c, g, h, t, s, a, cs, ns, d in rows:
    print(f"{str(c):<5}{str(g):<5}{str(h):<5}{t:<12}{str(s):<8}{str(a):<7}"
          f"{'무음' if cs else '소리':<7}{'무음' if ns else '소리':<7}{'★' if d else ''}")
print(f"\n  전체 {len(rows)}행 중 갈리는 행 = {differ}행 ({differ/len(rows)*100:.1f}%)")
gap = [r for r in rows if r[8]]
for r in gap:
    print(f"  ★ 갈리는 조건: 덮음={r[0]} 게임={r[1]} 사용자숨김={r[2]} → 등급 {r[3]}")
    print(f"     현행 게이트로는 '소리 남'. 이것이 발표/화상회의 전체화면이다.")
chk("T1 갈리는 행이 정확히 1행이다", differ == 1)
chk("T2 그 1행이 PanelsOnly & 미숨김이다", gap and gap[0][3] == "PanelsOnly" and gap[0][2] is False)
chk("T3 제안 게이트는 현행을 항상 포함한다(약화 없음)",
    all(not (r[6] and not r[7]) for r in rows), "현행 무음인데 제안이 소리인 행 = 0")

print()
print("=" * 78)
print("3. FramePacingTier.Active 의 실제 의미 — R1 §3-2 주장 검증")
print("=" * 78)
# R1 §3-2 주장: "Active = 최근 2초 안에 입력이 있었다"
m = re.search(r"public static FramePacingTier DecideTier\((.|\n)*?\n        \}", text["pres"])
body = m.group(0) if m else ""
chk("A1 DecideTier 본문 추출", bool(body), f"{len(body)}자")
chk("A2 마지막 문장이 무조건 return Active 다(= 폴백)",
    body.rstrip().rstrip("}").rstrip().endswith("return FramePacingTier.Active;"))
chk("A3 Calm 은 characterIdle AND 무입력>=2s 를 요구한다",
    "characterIdle && presence.Valid" in body and "SecondsSinceUserInput >= RecentInputSeconds" in body)
chk("A4 Away 는 characterIdle 을 AND 로 요구한다",
    "SecondsSinceUserInput >= AwaySeconds && characterIdle" in body)
# 재구현
def decide(display_asleep, suspended, secs_idle, char_idle, ui_hold, char_still,
           away=180.0, recent=2.0):
    if display_asleep: return "DisplayOff"
    if suspended: return "Suspended"
    if secs_idle >= away and char_idle: return "Away"
    if ui_hold: return "Active"
    if char_still: return "Still"
    if char_idle and secs_idle >= recent: return "Calm"
    return "Active"
print()
print("  반례 탐색 — '무입력인데 Active' 가 성립하는가")
cases = [
    ("배회 중(걷는다) · 무입력 600초", dict(display_asleep=False, suspended=False, secs_idle=600.0,
                                        char_idle=False, ui_hold=False, char_still=False)),
    ("가만히 서 있음 · 무입력 600초",   dict(display_asleep=False, suspended=False, secs_idle=600.0,
                                        char_idle=True,  ui_hold=False, char_still=True)),
    ("발표 중 슬라이드 클릭(1초 전)",   dict(display_asleep=False, suspended=False, secs_idle=1.0,
                                        char_idle=False, ui_hold=False, char_still=False)),
]
counter = 0
for label, kw in cases:
    t = decide(**kw)
    flag = ""
    if t == "Active" and kw["secs_idle"] >= 2.0:
        counter += 1; flag = "  ← ★ 반례: 무입력인데 Active"
    print(f"    {label:<28} → {t}{flag}")
chk("A5 '무입력이면 Active 가 아니다'는 반증된다", counter >= 1,
    f"반례 {counter}건 — 캐릭터가 움직이면 무입력 600초여도 Active")
# ★ 정직 정정: 초판은 이 칸을 decide(...,600s,...)=="Still" 로 썼고 FAIL 이 났다.
#   코드가 아니라 내 시나리오가 틀렸다 — 600초는 Away(>=180s AND idle)가 먼저 잡는다.
#   Still 과 Away 는 둘 다 R1 §3-2 의 무음 등급이므로 결론은 바뀌지 않는다.
#   점 하나가 아니라 무입력 구간 전체를 훑어 "Active 가 되는 구간이 있는가"를 묻는 것이 옳다.
print()
print("  집중 세션 중(캐릭터 정지) 무입력 시간별 등급 — 소리가 나는 구간이 있는가")
focus_tiers = {}
for secs in (0.0, 1.9, 2.0, 5.0, 30.0, 60.0, 179.9, 180.0, 600.0, 1500.0):
    t = decide(False, False, secs, True, False, True)
    focus_tiers[secs] = t
    print(f"    무입력 {secs:>7.1f}초 → {t}")
audible = [s for s, t in focus_tiers.items() if t == "Active"]
chk("A6 집중 세션 중에는 어떤 무입력 시간에도 Active 가 아니다", not audible,
    f"Active 구간 {len(audible)}개 / {len(focus_tiers)}개 → 'Active만 소리'는 focus.complete 를 구조적으로 죽인다")
chk("A7 그 등급은 전부 R1 §3-2 의 무음 등급(Still/Away/Calm)이다",
    set(focus_tiers.values()) <= {"Still", "Away", "Calm"},
    f"관측된 등급 = {sorted(set(focus_tiers.values()))}")

print()
print("=" * 78)
print("4. focus.complete 배선 — 상태 전이는 best-effort 다")
print("=" * 78)
chk("F1 CompleteSession 은 TryTriggerPoseState 로만 포즈를 건다",
    "IsSessionActive = false;\n            TryTriggerPoseState(StickmanStateId.FocusComplete);" in text["focus"])
chk("F2 TryTriggerPoseState 는 Idle/Walk 가 아니면 조용히 스킵한다",
    "if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return;" in text["focus"])
chk("F3 SpectacleEventLock 이 잡혀 있어도 조용히 스킵한다",
    "if (SpectacleEventLock.IsActive) return;" in text["focus"])
chk("F4 Update 는 IsSuspended 에서만 멈춘다(= 등급 1에서는 타이머가 계속 간다)",
    "if (_player.IsSuspended) return;" in text["focus"])

print()
print("=" * 78)
print("5. ★ 인용 줄번호 감사 — R2가 틀린 이유가 정확히 이것이었다")
print("=" * 78)
# R2 §4-4는 `StickmanAgent.cs:1152`를 [실측]으로 인용했는데 그 줄은 카메라 스케일 함수였다.
# 줄이 밀린 인용은 '조용히' 틀린다 — 사람이 다시 열어보지 않기 때문이다. 그래서 기계가 본다.
import glob
FILEMAP = {
    "StickmanAgent.cs":     SRC["agent"],
    "ViewerPresence.cs":    SRC["pres"],
    "FocusWatchDirector.cs": SRC["focus"],
    "SettingsWindow.cs":    SRC["setwin"],
    "CharacterSaveStore.cs": SRC["save"],
}
# (파일, 시작줄, 그 줄들 안에 반드시 있어야 하는 문자열)
EXPECT = [
    ("StickmanAgent.cs", 140, 143, "ArePanelsSuppressed"),
    ("StickmanAgent.cs", 153, 153, "Zoom/Teams/Keynote"),
    ("StickmanAgent.cs", 1250, 1250, "_fullscreenAutoHide || _userHidden"),
    ("ViewerPresence.cs", 385, 416, "return FramePacingTier.Active;"),
    ("SettingsWindow.cs", 176, 182, "TabCount = 5"),
]
lines = {}
for name, path in FILEMAP.items():
    lines[name] = open(path, encoding="utf-8").read().splitlines()

for name, a, b, needle in EXPECT:
    seg = "\n".join(lines[name][a - 1:b])
    chk(f"X {name}:{a}" + (f"-{b}" if b != a else "") + f" 에 «{needle[:34]}» 가 있다",
        needle in seg)

# 문서에 적힌 모든 인용을 긁어 실재하는 줄인지까지 확인
cited = 0
bad = []
for md in sorted(glob.glob(f"{ROOT}/design/sound/*.md")):
    body = open(md, encoding="utf-8").read()
    for m in re.finditer(r"(\w+\.cs):(\d+)", body):
        fn, ln = m.group(1), int(m.group(2))
        if fn not in lines:
            continue
        cited += 1
        if ln < 1 or ln > len(lines[fn]):
            bad.append(f"{os.path.basename(md)} → {fn}:{ln} (파일은 {len(lines[fn])}줄)")
chk("X* 문서의 모든 .cs 줄번호 인용이 파일 범위 안이다", not bad,
    f"검사 {cited}건 / 범위 밖 {len(bad)}건" + ("" if not bad else " → " + "; ".join(bad)))
print(f"\n  (범위 검사는 '그 줄이 존재하는가'까지다. 내용 일치는 위 X 항목이 본다.)")

print()
print("=" * 78)
print(f"최종: 검사 {checks}건 / 실패 {len(fails)}건")
print("=" * 78)
if fails:
    for f in fails: print("  FAIL:", f)
    sys.exit(1)
print("전건 통과.")
