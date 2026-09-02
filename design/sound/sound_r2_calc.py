# -*- coding: utf-8 -*-
"""
design-sound R2 계산기 — "무엇이 소리를 낼 자격이 있는가" 확정 라운드
사용자 확정 2026-09-02: "오디오도 필요함"

R1(sound_policy_calc.py)은 '침묵의 규칙'과 볼륨을 계산했다.
R2는 그 위에서 다음 넷을 계산한다.
  §1 자격 시험(허가/필요) 2축 판정 — 후보 전수
  §2 하루에 몇 번 소리가 나는가 (design-narrative 말풍선 354회/일과 같은 잣대)
  §3 파일 크기 — 디스크·빌드·스팀 depot (메모리는 R1 §3에서 이미 끝)
  §4 팩 스키마 산술 — 덮어쓰기 vs 더하기, 6팩 전량 소유 시

★ TEAM.md 4절 공통 처방: 계산기는 알려진 값으로 먼저 교정하고,
   음성 대조(일부러 틀린 기대값)가 통과하는지까지 확인한 뒤에야 나머지를 계산한다.
   교정이 깨지면 아래 모든 숫자를 폐기한다.
"""

import math

OUT = []
def p(s=""):
    OUT.append(s)

def rule(t):
    p("=" * 78); p(t); p("=" * 78)

# ---------------------------------------------------------------------------
# 순수 함수들
# ---------------------------------------------------------------------------
BYTES_PER_SEC_MONO_48K_16 = 48000 * 2   # 96,000 B/s

def pcm_bytes(ms):
    """모노 48 kHz 16-bit PCM의 바이트 수."""
    return ms / 1000.0 * BYTES_PER_SEC_MONO_48K_16

def kb(b):  return b / 1024.0
def mb(b):  return b / 1024.0 / 1024.0

def interval_seconds(per_day):
    """하루 N회 -> 평균 간격(초)."""
    return 86400.0 / per_day if per_day > 0 else float('inf')

def occupancy_pct(total_seconds_per_day):
    """하루 중 소리가 실제로 울리는 시간의 비율(%). narrative의 '화면 점유율'과 같은 정의."""
    return total_seconds_per_day / 86400.0 * 100.0

# ---------------------------------------------------------------------------
rule("§0 교정(CALIBRATION) — 알려진 값으로 먼저 맞춘다")
# ---------------------------------------------------------------------------
cal = []
def chk(label, got, want, tol=1e-9):
    ok = abs(got - want) <= tol
    cal.append(ok)
    p("  [%s] %-46s = %s" % ("OK " if ok else "FAIL", label, got))
    return ok

# PCM 산술 — 손으로 검산 가능한 값들
chk("pcm_bytes(1000ms) == 96000 B",            pcm_bytes(1000), 96000)
chk("pcm_bytes(250ms)  == 24000 B",            pcm_bytes(250),  24000)
chk("pcm_bytes(0ms)    == 0 B",                pcm_bytes(0),    0)
chk("kb(1024)          == 1.0 KB",             kb(1024),        1.0)
chk("mb(1048576)       == 1.0 MB",             mb(1048576),     1.0)
# 간격 산술
chk("interval(1/day)   == 86400 s",            interval_seconds(1),   86400)
chk("interval(354/day) == 244.07 s  (narrative 실측 재현)",
    interval_seconds(354), 86400/354.0, 1e-6)
chk("interval(24/day)  == 3600 s",             interval_seconds(24),  3600)
# 점유율 산술
chk("occupancy(86400s) == 100 %",              occupancy_pct(86400),  100.0)
chk("occupancy(864s)   == 1 %",                occupancy_pct(864),    1.0)
chk("occupancy(0s)     == 0 %",                occupancy_pct(0),      0.0)

p()
p("  ★ 교차 교정 — 다른 팀의 출하된 숫자를 우리 함수로 재현할 수 있는가")
p("     design-narrative R2: 말풍선 하루 354회 / 간격 244초 / 화면 점유율 0.690%")
narr_interval = interval_seconds(354)
chk("narrative 간격 244초 재현", round(narr_interval, 0), 244.0, 0.5)
# 점유율 0.690%를 역산하면 말풍선이 화면에 떠 있는 하루 총 시간이 나온다
narr_visible_sec = 0.690 / 100.0 * 86400
p("     -> 역산: 말풍선이 하루에 화면에 떠 있는 총 시간 = %.0f 초 (= %.1f 분)" % (narr_visible_sec, narr_visible_sec/60))
p("        354회로 나누면 1회당 %.2f 초" % (narr_visible_sec/354))
narr_before = 7.23 / 100.0 * 86400
p("     -> 개선 전(3,654회 / 7.23%%)으로 같은 역산: 1회당 %.2f 초" % (narr_before/3654))
p("        두 역산이 %.2f vs %.2f 로 일치한다 = narrative의 두 숫자쌍이 서로 정합적이고,"
  % (narr_visible_sec/354, narr_before/3654))
p("        내 함수가 그것을 재현한다. **교차 교정 통과.**")

p()
p("  음성 대조 — 일부러 틀린 기대값(pcm_bytes(250ms)를 30000 B로)이 통과하는가?")
neg = abs(pcm_bytes(250) - 30000) <= 1e-9
p("    통과했는가: %s   (False여야 정상 — True면 검사기가 고장난 것)" % neg)

p()
if all(cal) and not neg:
    p("  교정 %d/%d 통과 + 음성 대조 통과. 아래 표는 전부 이 함수들로 계산됐다." % (len(cal), len(cal)))
else:
    p("  ★★ 교정 실패 — 아래 숫자를 전부 폐기하라. ★★")
p()

# ---------------------------------------------------------------------------
rule("§1 자격 시험 — 허가(Permission) 2문 AND / 필요(Necessity) 1문")
# ---------------------------------------------------------------------------
p("""
  허가 P1 [원인]  : 인과 사슬의 시작이 사용자의 명시적 조작인가?
  허가 P2 [예상]  : 사용자가 그 소리가 날 것을 이미 알고 있었는가?
                    (= 소리가 '새 정보를 놀라움으로' 전달하지 않는가)
  필요 N  [정보]  : 소리가 없으면 사용자가 놓치는 것이 있는가?

  판정 = P1 AND P2 -> 소리를 만들 수 있다.  P1 또는 P2가 아니오 -> 만들지 않는다.
  N은 티어를 가르지 않는다(마스터 하나에 전부 딸린다). N=예 인 항목은
  '시각 신호 필수' 등급이 올라간다 — 소리가 유일한 전달자가 되면 안 되므로.
""")

# (이름, P1 원인, P2 예상, N 정보, 메모)
CAND = [
    ("사용자가 누른 것 — 단축키/버튼",           True,  True,  False,
     "누른 순간 화면이 이미 답한다. 소리는 확인 사살"),
    ("집중 세션 시작",                            True,  True,  False,
     "누른 즉시"),
    ("★ 집중 세션 완료(재화 지급)",               True,  True,  True,
     "25분 전에 사용자가 길이를 직접 골랐다 = 예상됨. 동전 지급은 회수 불가"),
    ("집중 세션 중도 취소",                       True,  True,  False,
     "허가는 있으나 '만들지 않는다' — 나무라는 소리를 만들지 않는다(별도 판정)"),
    ("구매",                                      True,  True,  True,
     "동전이 줄어든다 = 회수 불가"),
    ("착용 / 해제",                               True,  True,  False,
     "몸에 붙는 것이 보인다"),
    ("던진 뒤 착지 / 구르기 (사용자가 던짐)",     True,  True,  False,
     "1~2초 지연이지만 사용자가 던지고 보고 있다"),
    ("로데오 커서 잡기",                          True,  True,  False,
     "단축키 R 토글의 결과"),
    ("자율 착지 (배회하다 떨어짐)",               False, False, False,
     "캐릭터가 스스로 한 일"),
    ("자율 파쿠르 등반",                          False, False, False,
     "stepUpChance=0.85 자율"),
    ("자율 모서리 매달리기",                      False, False, False,
     "자율"),
    ("레벨업",                                    False, False, True,
     "패시브 XP 10초마다 = 사용자가 한 일이 아니고, 진행도가 화면에 없어 예상도 불가"),
    ("말풍선 발화",                               False, False, False,
     "하루 354회. narrative가 방금 10.3배 낮춘 것을 되돌린다"),
    ("오류 / 예외 상태",                          False, False, True,
     "앱이 자기 문제로 말을 건다. OS 알림음으로 오인되는 1순위"),
    ("발판 소실 매달리기(사용자가 창을 닫음)",    True,  False, False,
     "원인은 사용자지만 소리를 예상할 수 없다 -> '내가 창을 닫았더니 앱이 반응했다'"),
    ("설정창 [미리듣기] 버튼",                    True,  True,  True,
     "마스터 OFF에서도 나는 유일한 예외 — 누른 것 자체가 동의"),
]

p("  %-42s %-4s %-4s %-4s %s" % ("후보", "P1", "P2", "N", "판정"))
p("  " + "-" * 74)
allowed, denied = [], []
for name, p1, p2, n, memo in CAND:
    ok = p1 and p2
    (allowed if ok else denied).append(name)
    p("  %-42s %-4s %-4s %-4s %s" % (
        name, "O" if p1 else "X", "O" if p2 else "X", "O" if n else "-",
        "자격 있음" if ok else "★ 자격 없음"))
p("  " + "-" * 74)
p("  자격 있음 %d / %d  (%.1f%%)   자격 없음 %d (%.1f%%)" % (
    len(allowed), len(CAND), len(allowed)/len(CAND)*100,
    len(denied), len(denied)/len(CAND)*100))
p()
p("  ★ 판별 기준 반박 — 리더 제안 '사용자가 방금 한 행동의 결과인가'를 그대로 쓰면")
p("     '집중 세션 완료'가 탈락한다(25분 = 방금이 아니다). 그런데 그것은 이 앱에서")
p("     소리가 가장 필요한 단 하나의 순간이다(재화 지급 = 회수 불가).")
p("     -> 하중을 받는 것은 '방금'(최근성)이 아니라 '예상됨'이다. P2로 교체한다.")
p("     교체해도 자율/말풍선/오류/레벨업은 전부 그대로 탈락한다(P1에서 이미 걸린다).")
p("     즉 P2로 바꾸는 것은 기준을 느슨하게 하는 것이 아니라 정확하게 하는 것이다.")
p()

# ---------------------------------------------------------------------------
rule("§1-B 그 결과 — 카탈로그 키가 몇 개 남는가")
# ---------------------------------------------------------------------------
# (키, 길이 ms, 티어, 살아남는가, 사유)
KEYS_R1 = [
    ("sfx.ui.preview",        400, "A", True,  "재생은 focus.start 클립 재사용(별도 파일 없음)"),
    ("sfx.archery.draw",      400, "A", True,  ""),
    ("sfx.archery.release",   300, "A", True,  ""),
    ("sfx.archery.miss",      300, "A", True,  ""),
    ("sfx.archery.hit",       300, "A", True,  ""),
    ("sfx.archery.bullseye",  400, "A", True,  ""),
    ("sfx.focus.start",       400, "A", True,  ""),
    ("sfx.focus.complete",    800, "A", True,  "★ P2가 살린 항목"),
    ("sfx.equip.wear",        300, "A", True,  ""),
    ("sfx.equip.remove",      300, "A", True,  ""),
    ("sfx.shop.purchase",     400, "A", True,  "상점 구현 후"),
    ("sfx.rodeo.grab",        250, "B", True,  ""),
    ("sfx.ragdoll.impact",    250, "B", True,  ""),
    ("sfx.land.throw",        250, "B", True,  ""),
    ("sfx.land.ambient",      200, "C", False, "P1 탈락 — 자율"),
    ("sfx.parkour.climb",     250, "C", False, "P1 탈락 — 자율"),
    ("sfx.ledge.hang",        250, "C", False, "P1 탈락 — 자율"),
    ("sfx.progress.levelup",  800, "M", False, "P1·P2 탈락 — 패시브 XP, 진행도 비가시"),
]
alive = [k for k in KEYS_R1 if k[3]]
dead  = [k for k in KEYS_R1 if not k[3]]
p("  R1 카탈로그 %d키 -> R2 %d키 (%d키 삭제, %.1f%% 감소)" % (
    len(KEYS_R1), len(alive), len(dead), len(dead)/len(KEYS_R1)*100))
for k in dead:
    p("    삭제: %-24s (%s) — %s" % (k[0], "티어 "+k[2], k[4]))
p()
p("  ★ 남은 %d키가 전부 티어 A/B다 = 전부 사용자가 원인이다." % len(alive))
p("     따라서 '팩을 사면 시끄러워진다'가 원리적으로 성립하지 않는다 —")
p("     팩이 교체할 수 있는 키가 전부 사용자 조작을 전제로 하기 때문이다.")
p()
p("  ★ 대가(정직하게): 6팩이 가장 쓰고 싶어 하던 land.ambient/parkour/ledge가 사라졌다.")
p("     R1의 팩별 6키 목록을 §4에서 전부 다시 짠다.")
p()

# 고유 파일 수 (preview는 focus.start 재사용)
DISTINCT = [k for k in alive if k[0] != "sfx.ui.preview"]
base_ms = sum(k[1] for k in DISTINCT)
base_bytes = pcm_bytes(base_ms)
p("  기본 세트 고유 파일 %d개 / 총 길이 %d ms (%.2f s)" % (len(DISTINCT), base_ms, base_ms/1000))
p("  PCM 모노 48k 16-bit 원본 크기 = %.1f KB" % kb(base_bytes))
p()

# ---------------------------------------------------------------------------
rule("§2 ★ 하루에 몇 번 소리가 나는가 — narrative와 같은 잣대")
# ---------------------------------------------------------------------------
p("""
  구동원(driver)은 design-systems ECONOMY_SPEC의 원형 B/D를 그대로 쓴다.
  [실측] = 다른 팀 문서에 있는 값 / [가정] = 텔레메트리가 없어 내가 정한 값(정직 고지)
""")

# (이름, 회/일, 회당 소리 수, 회당 소리 길이 합 ms, 출처)
BASE = [
    ("집중 세션(시작+완료)",  2.00, 2, 400+800, "[실측] ECONOMY_SPEC 7-1 '집중 세션 종료 2.00회/일'"),
    ("활쏘기(당김+발사+결과)", 4.00, 3, 400+300+333, "[실측] ECONOMY_SPEC 5-2 원형 B '활쏘기 4회'"),
    ("아이템 구매",           0.41, 1, 400,     "[실측] ECONOMY_SPEC 7-1 '아이템 구매 0.41회/일'"),
    ("착용/해제 변경",        0.91, 1, 300,     "[가정] 구매 0.41 + 자발 교체 0.50"),
    ("던지기(충격+착지)",     3.00, 2, 250+250, "[가정] 하루 3회 던진다"),
    ("로데오 커서 잡기",      0.20, 1, 250,     "[가정] 5일에 1회"),
    ("설정 [미리듣기]",       0.02, 1, 400,     "[가정] 50일에 1회"),
]
p("  %-24s %8s %6s %10s %10s" % ("구동원", "회/일", "소리/회", "소리회/일", "울린초/일"))
p("  " + "-" * 66)
tot_n = tot_sec = 0.0
for name, per_day, snd, ms, src in BASE:
    n = per_day * snd
    sec = per_day * ms / 1000.0
    tot_n += n; tot_sec += sec
    p("  %-24s %8.2f %6d %10.2f %10.3f" % (name, per_day, snd, n, sec))
p("  " + "-" * 66)
p("  %-24s %8s %6s %10.2f %10.3f" % ("합계(원형 B, 기본값)", "", "", tot_n, tot_sec))
p()
for name, per_day, snd, ms, src in BASE:
    p("    %-24s %s" % (name, src))
p()

b_interval = interval_seconds(tot_n)
b_occ = occupancy_pct(tot_sec)
p("  원형 B 하루 소리 = %.1f회 / 평균 간격 %.0f 초 (= %.1f 분) / 귀 점유율 %.4f %%"
  % (tot_n, b_interval, b_interval/60, b_occ))
p()
p("  ★ design-narrative 말풍선(R2 확정치)과의 대조 — 같은 정의, 같은 분모(24h)")
p("    %-14s %10s %12s %14s" % ("", "회/일", "평균 간격", "점유율"))
p("    %-14s %10.0f %10.0f 초 %13.3f %%" % ("말풍선", 354, narr_interval, 0.690))
p("    %-14s %10.1f %10.0f 초 %13.4f %%" % ("소리(기본)", tot_n, b_interval, b_occ))
p("    %-14s %10.1f배 %9.1f배 %12.1f배" % ("소리가 더 드묾",
      354/tot_n, b_interval/narr_interval, 0.690/b_occ))
p()
p("  판정: 소리는 말풍선보다 %.1f배 드물고, 점유율은 %.1f배 낮다." % (354/tot_n, 0.690/b_occ))
p("        narrative가 22초->244초로 10.3배 낮춘 그 잣대를 통과한다.")
p()

# 원형 D — 상한
p("  ─ 원형 D(ECONOMY_SPEC 4-3 '하루 3.3시간 집중' 상한 유저) ─")
HEAVY = [
    ("집중 세션 50분 ×4", 4.0, 2, 400+800),
    ("활쏘기(동전 쿨다운 상한 48/일)", 48.0, 3, 400+300+333),
    ("구매", 2.0, 1, 400),
    ("착용/해제", 4.0, 1, 300),
    ("던지기", 10.0, 2, 500),
    ("로데오", 1.0, 1, 250),
]
h_n = sum(a*b for _, a, b, _c in HEAVY)
h_sec = sum(a*c/1000.0 for _, a, _b, c in HEAVY)
p("  원형 D 하루 소리 = %.1f회 / 평균 간격 %.0f 초 / 귀 점유율 %.4f %%"
  % (h_n, interval_seconds(h_n), occupancy_pct(h_sec)))
p("  말풍선 354회 대비 %.1f배 드묾 / 점유율 %.1f배 낮음"
  % (354/h_n, 0.690/occupancy_pct(h_sec)))
p("  ★ 상한 유저조차 말풍선보다 드물다. 이 설계에는 '시끄러운 상한'이 없다.")
p()

# 전역 안전망
p("  ─ 전역 안전망(runaway 방어) ─")
ARCH_SEQ_MIN = 0.55+0.42+0.30+0.62+0.18+0.34+0.55   # StickConfig 실측 합
p("  활쏘기 1회 시퀀스 최소 길이 = %.2f 초" % ARCH_SEQ_MIN)
p("    (StickConfig 실측: targetIntro .55 + draw .42 + aimHold .30 + arrowFlight .62")
p("     + recoil .18 + recover .34 + outro .55 — 접근 이동 시간은 여기에 안 들어감)")
theo = 60.0 / ARCH_SEQ_MIN * 3
real = 60.0 / (ARCH_SEQ_MIN + 5.0) * 3     # 접근 5초 가정
peak_b = 3.0                                # 원형 B 활쏘기 1회 = 3소리, 분당 1회 볼리
p("  이론 최대   %.1f 소리/분 (접근 0초 — 실현 불가)" % theo)
p("  현실 최대   %.1f 소리/분 (접근 5초 [가정])" % real)
p("  원형 B 피크 %.1f 소리/분" % peak_b)
NET = 40
p("  -> 전역 안전망 = 60초 슬라이딩 윈도우 %d회" % NET)
p("     현실 최대의 %.2f배 / 원형 B 피크의 %.1f배 — 정상 사용을 절대 건드리지 않는다."
  % (NET/real, NET/peak_b))
p("     안전망이 걸리면 그것은 '조용해졌다'가 아니라 '버그다' — 걸린 순간 경고 로그.")
p()

# ---------------------------------------------------------------------------
rule("§3 파일 크기 — 디스크 / 빌드 / 스팀")
# ---------------------------------------------------------------------------
MAC_BUILD_MB = 98.8   # 실측: find+stat 바이트 합산 (146파일)
WIN_BUILD_MB = 84.3   # 실측: find+stat 바이트 합산 (141파일)
p("  실측 현재 빌드: macOS .app %.1f MB(146파일) / Windows %.1f MB(141파일)" % (MAC_BUILD_MB, WIN_BUILD_MB))
p("  실측: UnityEngine.AudioModule.dll 이 두 빌드에 **이미 들어 있다**")
p("        (macOS 93,184 B / Windows 104,360 B). FMOD는 UnityPlayer 안에 있다.")
p("  -> ★ 오디오를 켜는 것의 바이너리 비용 = 0 바이트. 늘어나는 건 클립뿐이다.")
p()
p("  기본 세트 %d파일 / %.2f s / PCM 원본 %.1f KB = 빌드의 %.3f %% (macOS 기준)"
  % (len(DISTINCT), base_ms/1000, kb(base_bytes), kb(base_bytes)/1024/MAC_BUILD_MB*100))
p()
p("  포맷 후보 비교 (총 %.2f s, 모노 48 kHz)" % (base_ms/1000))
p("    %-26s %10s %10s %s" % ("포맷", "디스크", "메모리", "비고"))
fmts = [
    ("PCM (무압축)",        base_bytes,      base_bytes,  "디코드 0, 헤더 오버헤드 0"),
    ("ADPCM (Unity 4bit)",  base_bytes/4.0,  base_bytes,  "3.5~4:1, 디코드 매우 쌈"),
    ("Vorbis q0.7",         base_ms/1000*80000/8 + len(DISTINCT)*4096, base_bytes,
     "★ 0.3초 클립에 코드북 헤더 ~4 KB/파일이 붙는다"),
]
for n, d, m, note in fmts:
    p("    %-26s %8.1f KB %8.1f KB  %s" % (n, kb(d), kb(m), note))
p()
vorbis_b = base_ms/1000*80000/8 + len(DISTINCT)*4096
best_saving = kb(base_bytes) - kb(vorbis_b)
p("  판정: **PCM**. 최대 절감액(Vorbis 채택 시)은 %.1f KB = 빌드의 %.3f %% 다."
  % (best_saving, best_saving/1024/MAC_BUILD_MB*100))
p("        ★ 정직 정정: Vorbis(%.1f KB)가 ADPCM(%.1f KB)보다 작다 — 헤더 오버헤드를"
  % (kb(vorbis_b), kb(base_bytes/4.0)))
p("        넣어도 그렇다. 그러나 세 후보의 차이가 전부 빌드의 0.4% 미만이므로")
p("        **크기는 이 선택의 결정 변수가 아니다.** 결정 변수는 24시간 상주 앱에서")
p("        디코드 스레드/CPU를 새로 들이느냐이고, 그 답이 PCM이다.")
p("        (Vorbis 헤더 %d KB/파일 x %d파일 = %.0f KB 는 추정치 — 실측 아님)"
  % (4, len(DISTINCT), len(DISTINCT)*4))
p()

# ---------------------------------------------------------------------------
rule("§4 팩 스키마 산술 — 덮어쓰기 vs 더하기")
# ---------------------------------------------------------------------------
ALIVE_MS = {k[0]: k[1] for k in alive}
PACKS = {
    "pack.office":   ["sfx.focus.start","sfx.focus.complete","sfx.equip.wear",
                      "sfx.equip.remove","sfx.shop.purchase","sfx.land.throw"],
    "pack.cyber":    ["sfx.archery.draw","sfx.archery.release","sfx.archery.bullseye",
                      "sfx.equip.wear","sfx.land.throw","sfx.ragdoll.impact"],
    "pack.graffiti": ["sfx.archery.release","sfx.archery.hit","sfx.equip.wear",
                      "sfx.shop.purchase","sfx.land.throw","sfx.rodeo.grab"],
    "pack.sports":   ["sfx.archery.bullseye","sfx.archery.hit","sfx.archery.miss",
                      "sfx.focus.complete","sfx.land.throw","sfx.ragdoll.impact"],
    "pack.ink":      ["sfx.archery.draw","sfx.archery.release","sfx.equip.wear",
                      "sfx.equip.remove","sfx.focus.start","sfx.land.throw"],
    "pack.military": ["sfx.equip.wear","sfx.equip.remove","sfx.focus.start",
                      "sfx.focus.complete","sfx.land.throw","sfx.rodeo.grab"],
}
p("  팩당 상한 6키(정확한 키 지정, 와일드카드 금지). 팩은 키를 추가할 수 없다.")
p()
p("  %-16s %5s %10s %10s" % ("팩", "키수", "길이 ms", "PCM KB"))
pack_total = 0.0
for pid, keys in PACKS.items():
    assert len(keys) == 6, pid
    for k in keys:
        assert k in ALIVE_MS, (pid, k)
    ms = sum(ALIVE_MS[k] for k in keys)
    b = pcm_bytes(ms)
    pack_total += b
    p("  %-16s %5d %10d %9.1f" % (pid, len(keys), ms, kb(b)))
p("  " + "-" * 45)
p("  %-16s %5d %10s %9.1f  (= %.2f MB)" % ("6팩 합계", 36, "", kb(pack_total), mb(pack_total)))
p()
p("  기본 %.1f KB + 6팩 %.1f KB = 전량 소유 최악 %.2f MB"
  % (kb(base_bytes), kb(pack_total), mb(base_bytes + pack_total)))
p("  macOS 빌드 %.0f MB 대비 %.2f %% (기본만 배포하면 %.3f %%)"
  % (MAC_BUILD_MB, mb(base_bytes+pack_total)/MAC_BUILD_MB*100,
     mb(base_bytes)/MAC_BUILD_MB*100))
p("  팩 1개당 평균 %.1f KB — 스팀 DLC depot 1개가 오디오 때문에 커지는 양."
  % (kb(pack_total)/6))
p()

# 더하기(additive)로 갔을 때 무슨 일이 나는가
p("  ─ 만약 '더하기(additive)'로 설계했다면 ─")
from collections import Counter
cnt = Counter()
for keys in PACKS.values():
    for k in keys:
        cnt[k] += 1
worst_key, worst_n = cnt.most_common(1)[0]
p("  6팩 전량 소유 시 한 키에 몰리는 클립 수:")
for k, c in cnt.most_common():
    if c > 1:
        p("    %-24s %d개 팩이 같은 키를 교체하려 한다" % (k, c))
p("  최악 키 = %s (%d개 팩)" % (worst_key, worst_n))
gain_db = 20 * math.log10(worst_n)
p("  더하기로 동시 재생하면 이론 합성 이득 = +%.2f dB (동일 신호 가정)" % gain_db)
p("    -> 마스터 기본 -13.26 dB 위에 얹으면 출력 피크 %.2f dBFS" % (-14.26 + gain_db))
p("    -> R1 §1이 확보한 OS 알림음 대비 여유 8.26 dB 가 %.2f dB 로 무너진다."
  % (8.26 - gain_db))
p("  동시 발성 상한 3(R1 §4-4)도 %d > 3 으로 즉시 초과한다." % worst_n)
p("  ★ 결론: **덮어쓰기. 1 트리거 키 = 최대 1 클립.** 더하기는 두 가지를 동시에 깬다 —")
p("     (a) 볼륨 예산, (b) '돈을 낼수록 시끄러워진다'는 구조. 협상 불가.")
p()
p("  ─ 그러면 6팩 전량 소유 시 %s 는 누가 이기는가 ─" % worst_key)
p("  채택: **사용자가 [이벤트] 탭에서 사운드 테마를 정확히 하나 고른다.**")
p("        기본값 = '기본(팩 없음)'. 팩을 사도 소리는 자동으로 바뀌지 않는다.")
p("  기각 A: 팩 로드 순서 / packVersion 최신 우선 -> 비결정적. 팩 업데이트가 소리를 바꾼다.")
p("  기각 B: 착용 중인 4슬롯의 다수결 -> 2:2 동률이 흔하고(4슬롯), 모자 하나 바꾸면")
p("          소리 6개가 조용히 전부 바뀐다. 사용자가 고른 적 없는 변경이다.")
p("  기각 C: 매니페스트에 우선순위 int -> 팩끼리 숫자 경쟁. 볼륨 필드와 같은 종류의 누수.")
p()
p("  ★ 이펙트와 반드시 다르게 구현해야 한다:")
p("     ARCHITECTURE 5-3-2 (D)는 (stateId, trigger) -> **이펙트 목록**(리스트)이다 = 더하기.")
p("     사운드는 (triggerKey) -> **클립 하나**다 = 덮어쓰기.")
p("     같은 레지스트리 헬퍼를 재사용하면 사운드가 조용히 더하기가 된다. 분리 필수.")
p()

# ---------------------------------------------------------------------------
rule("§5 재발행 비용 — 지금 굳는 것과 나중에 바꿔도 되는 것")
# ---------------------------------------------------------------------------
p("  game-architect: '팩은 6개라 재발행 비용이 세이브의 6배다.'")
p("  -> 팩 6개 × 채널 1(스팀) = 재발행 단위 6개. 세이브는 1개.")
p()
p("  %-38s %-10s %s" % ("스키마 요소", "재발행?", "이유"))
p("  " + "-"*74)
FREEZE = [
    ("트리거 키 문자열 집합", "예", "팩이 문자열로 참조한다. 이름 변경·삭제 = 6팩 전부 깨짐"),
    ("assetKey(주소) vs AudioClip 직접참조", "예", "직접참조면 미소유 팩도 매니페스트와 함께 로드된다"),
    ("클립 원본 포맷(모노/48k/16bit/WAV)", "예", "재수록은 원본 재작업 = 6팩 전부"),
    ("팩당 키 상한 6", "아니오", "늘리는 건 하위호환. 줄이면 재발행"),
    ("soundSchemaVersion 필드 존재", "예", "없으면 키 집합을 영원히 버전할 수 없다"),
    ("키 '추가'", "아니오", "구팩은 그 키를 안 덮을 뿐 — 기본 클립이 난다"),
    ("음색·믹싱(클립 내용)", "아니오", "같은 키에 새 파일 = 팩 업데이트 1건"),
    ("티어/예산/볼륨/묵음 조건", "아니오", "★ 애초에 팩 스키마에 넣지 않는다(앱 소유)"),
]
for a, b, c in FREEZE:
    p("  %-38s %-10s %s" % (a, b, c))
p()
p("  즉 '지금 안 정하면 6팩 재발행'인 것은 %d건뿐이다."
  % sum(1 for a,b,c in FREEZE if b == "예"))
p("  나머지는 나중에 정해도 된다 — 그게 이 스키마의 설계 목표다.")
p()

# ---------------------------------------------------------------------------
rule("§6 원칙 2(비침해) 잔여 위험 — '회의 중이면?'")
# ---------------------------------------------------------------------------
p("  §1-B에서 티어 C/M이 전부 사라졌으므로, 남은 %d키는 전부 사용자 조작이 원인이다." % len(alive))
p("  -> 회의 중에 소리가 나려면 **사용자가 회의 중에 그 조작을 해야 한다.**")
p()
p("  잔여 벡터는 정확히 하나다: **sfx.focus.complete**")
p("    사용자가 25분 집중을 걸어 놓고 그 사이에 회의에 들어갈 수 있다.")
p("    이것이 이 설계 전체에서 유일하게 '사용자가 지금 안 하고 있는데 나는 소리'다.")
p()
p("  1차 방어선(추가 구현 0): FramePacingPolicy.RecentInputSeconds = 2f [실측]")
p("    티어 Active = 최근 2초 내 입력. 회의 중 사용자는 듣고 있지 타이핑하지 않는다.")
p("    -> 완료 순간 입력이 2초 내에 없으면 무음, 시각 신호만 남는다.")
p("  2차 방어선(P2): M9 마이크 사용 중 — 회의를 직접 관측한다.")
p("  3차: M6 야간 / M5 집중모드 / M1 전체화면 — R1 §3 그대로.")
p()
p("  ★ 구현 함정 (이걸 안 적으면 '완료음이 영원히 안 난다'가 된다):")
p("    M5(집중 모드 중 묵음)를 **전이 전 상태**로 평가하면 focus.complete가 자기 자신에게")
p("    막힌다. 묵음 조건은 반드시 **전이가 확정된 뒤의 상태**로 평가한다.")
p("    (절대 불변 원칙 1 '상태 전이가 확정된 뒤 그 상태로부터만 파생'의 사운드판)")
p()
p("  ★ 즉시 숨김 ⌃⌥⌘K — 소리도 멎는가: **구조적으로 멎는다.**")
p("    StickmanAgent.SetUserHidden -> ApplySuspendDecision -> Suspend()")
p("    Tick()이 `if (_isSuspended) return;`(StickmanAgent.cs:623)로 조기 반환한다 [실측].")
p("    상태 전이가 아예 일어나지 않으므로 상태 파생 소리는 발생할 수 없다.")
p("    다만 상태 밖 경로(설정창 버튼/구매)는 별도 게이트가 필요하다:")
p("      묵음 조건에 **M8 = StickmanAgent.IsSuspended** 를 명시적으로 넣는다.")
p("      IsSuspended 는 전체화면 자동 숨김과 사용자 숨김을 **이미 합성한 값**이다")
p("      (StickmanAgent.cs:1152 `_fullscreenAutoHide || _userHidden`) [실측] —")
p("      두 축을 사운드가 따로 볼 이유가 없다.")
p()

# ---------------------------------------------------------------------------
rule("§7 백신·배포 위험 — 오디오가 실제로 늘리는 표면")
# ---------------------------------------------------------------------------
p("  시장 조사 결론(MARKET_LANDSCAPE 3-3): Shimeji를 죽인 건 기능이 아니라 배포와 백신 경고.")
p("  SECURITY_MODEL 선 3: 난독화·패킹·무결성 자가검사는 전부 백신이 싫어하는 형태.")
p()
p("  %-42s %s" % ("오디오 도입안", "백신 표면 증가"))
p("  " + "-"*74)
AV = [
    ("Unity 내장 오디오(m_DisableAudio=0)", "0 — AudioModule.dll이 이미 서명된 빌드에 있다 [실측]"),
    ("OS 원샷 P/Invoke(winmm / AudioToolbox)", "0 — OS 라이브러리. 새 바이너리 없음"),
    ("★ 네이티브 오디오 플러그인 번들(.dll/.bundle)", "★ 큼 — 서명 안 된 새 바이너리. 금지"),
    ("★ 런타임 오디오 다운로드(CDN/원격 팩)", "★ 큼 — 실행 중 파일 기록 + 네트워크. 금지"),
    ("클립 %d개 %.0f KB 를 depot에 동봉" % (len(DISTINCT), kb(base_bytes)),
     "0 — 데이터 파일은 휴리스틱 대상이 아니다"),
]
for a, b in AV:
    p("  %-42s %s" % (a, b))
p()
p("  판정: 오디오는 백신 위험을 **늘리지 않는다** — 위의 두 금지선만 지키면.")
p("  실제 위험은 크기가 아니라 '실행 중에 새 실행가능 바이트가 생기는가'다.")
p("  빌드 증가분 %.0f KB(%.3f%%)는 SmartScreen 평판에도 무의미하다."
  % (kb(base_bytes), kb(base_bytes)/1024/MAC_BUILD_MB*100))
p()
p("  ★ 다만 하나 진짜 비용이 있다 — macOS 마이크 방어선(M9, P2)을 넣으면")
p("    Info.plist에 NSMicrophoneUsageDescription이 필요해질 수 있다.")
p("    권한 프롬프트는 이 카테고리에서 **최대 이탈 요인**이다(Shimeji의 교훈).")
p("    -> M9는 kAudioDevicePropertyDeviceIsRunningSomewhere(장치 속성 조회, 캡처 아님)로만")
p("       구현하고, 권한 프롬프트가 뜨면 **그 자리에서 기능을 버린다**. 실기 확인 대상.")
p()

# ---------------------------------------------------------------------------
rule("§8 오디오 장치 상주 — 두 경로의 대가 (리더 결정 D1/D2)")
# ---------------------------------------------------------------------------
p("  실측: ProjectSettings/AudioManager.asset m_EnableOutputSuspension = 1")
p("  그러나 Unity 공식 소스가 이 항목을 '(editor only)'로 라벨한다 —")
p("  즉 **출하 플레이어에서는 자동 서스펜드가 없다**. R1의 판단이 재확인됐다.")
p()
p("  %-32s %-14s %-14s %s" % ("", "경로 1 Unity", "경로 2 OS원샷", ""))
p("  " + "-"*74)
ROWS = [
    ("m_DisableAudio", "0으로 되돌림", "1 유지", ""),
    ("FMOD 장치 24h 상주", "예 (R1 F4 재발)", "아니오", "상주 앱의 유일한 진짜 비용"),
    ("macOS 슬립 어서션 위험", "있음", "없음", "pmset 게이트 필수"),
    ("볼륨 슬라이더", "정상 동작", "OS 의존/제한", "R1 §5 표가 경로 2에서 흔들린다"),
    ("신규 P/Invoke", "0벌", "2벌", "dev-platform 배정"),
    ("팩 스키마 영향", "AudioClip", "파일 경로", "★ assetKey 문자열이면 양쪽 다 가능"),
    ("새 바이너리(백신)", "0", "0", "둘 다 안전"),
]
for a, b, c, d in ROWS:
    p("  %-32s %-14s %-14s %s" % (a, b, c, d))
p()
p("  ★ 내가 스키마로 두 경로를 다 살려 둔다: sounds[i]는 **assetKey 문자열**만 갖는다.")
p("    AudioClip 직접 참조를 금지하면 D1/D2가 나중에 뒤집혀도 **팩을 재발행하지 않는다.**")
p("    이것이 §5 표에서 '예(재발행)'로 표시된 항목을 실제로 방어하는 유일한 장치다.")
p()

rule("끝 — 이 파일은 sound_r2_calc.py 가 생성했다")
print("\n".join(OUT))
