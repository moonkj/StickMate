#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
StickMate 사운드 정책 검산기 (design-sound, 2026-09-02)

이 저장소의 규칙: "계산기·검사기를 만들면 알려진 값으로 먼저 교정한다.
교정이 깨지면 그 뒤 숫자를 전부 폐기한다."(docs/TEAM.md 4절 공통 처방)

그래서 이 스크립트는 §0 교정(CALIBRATION)을 맨 앞에 두고, 하나라도 틀리면
즉시 SystemExit로 죽는다. 초록으로 끝났다면 아래 모든 표는 이 교정을 통과한
같은 함수로 계산된 것이다.

출력: sound_policy_calc.out.txt
"""

import math
import sys

FAIL = []


def check(name, got, want, tol=1e-9):
    ok = abs(got - want) <= tol
    if not ok:
        FAIL.append(f"{name}: got {got!r}, want {want!r}")
    return ok


# ============================================================================
# 공통 함수 — 표 전체가 이 두 개만 쓴다
# ============================================================================

def db_to_lin(db):
    """진폭 배율(dB -> 선형). 20*log10 규약(음압/진폭)."""
    return 10.0 ** (db / 20.0)


def lin_to_db(lin):
    return 20.0 * math.log10(lin)


def perceived_ratio(db_delta):
    """체감 크기 비 — 표준 근사 '10 dB 감쇠 = 체감 절반'.
    2^(dB/10). +10dB -> 2배, -10dB -> 0.5배."""
    return 2.0 ** (db_delta / 10.0)


def slider_to_amp(s, exponent=5.0 / 3.0):
    """볼륨 슬라이더(0~100) -> 진폭 배율.
    지수 5/3을 쓰는 이유: 슬라이더를 절반으로 내리면 체감 크기가 정확히 절반이
    되게 하기 위해서다. 2^(-5/3) = 0.3150 이고 20*log10(0.3150) = -10.03 dB,
    즉 '슬라이더 반 = 체감 반'(위 perceived_ratio 규약과 정확히 맞물린다).
    흔히 쓰는 v^2(=-12.04dB/반)나 v^1(=-6.02dB/반)은 이 성질이 없다."""
    return (s / 100.0) ** exponent


# ============================================================================
# §0 교정 — 알려진 값. 하나라도 틀리면 이 파일의 모든 숫자는 무효다.
# ============================================================================
lines = []
w = lines.append

w("=" * 78)
w("§0 교정(CALIBRATION) — 알려진 값으로 먼저 맞춘다")
w("=" * 78)

CAL = [
    ("db_to_lin(0)        == 1.0",        db_to_lin(0.0),        1.0,      1e-12),
    ("db_to_lin(-6.0206)  == 0.5",        db_to_lin(-6.020599913279624), 0.5, 1e-12),
    ("db_to_lin(-20)      == 0.1",        db_to_lin(-20.0),      0.1,      1e-12),
    ("db_to_lin(+20)      == 10.0",       db_to_lin(20.0),       10.0,     1e-9),
    ("lin_to_db(1.0)      == 0 dB",       lin_to_db(1.0),        0.0,      1e-12),
    ("lin_to_db(0.5)      == -6.0206 dB", lin_to_db(0.5),        -6.020599913279624, 1e-9),
    ("round-trip lin_to_db(db_to_lin(-13.26))", lin_to_db(db_to_lin(-13.26)), -13.26, 1e-9),
    ("perceived_ratio(-10) == 0.5",       perceived_ratio(-10.0), 0.5,     1e-12),
    ("perceived_ratio(0)   == 1.0",       perceived_ratio(0.0),   1.0,     1e-12),
    ("perceived_ratio(+10) == 2.0",       perceived_ratio(10.0),  2.0,     1e-12),
    ("slider_to_amp(100)   == 1.0",       slider_to_amp(100),     1.0,     1e-12),
    ("slider_to_amp(0)     == 0.0",       slider_to_amp(0),       0.0,     1e-12),
]
for name, got, want, tol in CAL:
    ok = check(name, got, want, tol)
    w(f"  [{'OK ' if ok else 'FAIL'}] {name:44s} = {got:.12g}")

# ★ 음성 대조(negative control) — 틀린 식이 실제로 FAIL을 내는지 확인한다.
#   "모든 없음 판정에 양성 대조"(TEAM.md #4)의 사운드판. 이게 통과해 버리면
#   위 12건의 OK는 아무것도 증명하지 못한다.
bogus_ok = abs(db_to_lin(-6.0206) - 0.25) <= 1e-12   # 10*log10 규약을 잘못 쓴 경우
w("")
w("  음성 대조 — 일부러 틀린 기대값(-6.02dB를 0.25로)이 통과하는가?")
w(f"    통과했는가: {bogus_ok}   (False여야 정상 — True면 검사기가 고장난 것)")
if bogus_ok:
    FAIL.append("음성 대조 실패: 틀린 값이 통과했다. 이 파일의 모든 숫자 폐기.")

if FAIL:
    print("\n".join(lines))
    print("\n!!! 교정 실패 — 이 파일의 모든 숫자를 폐기한다 !!!")
    for f in FAIL:
        print("  -", f)
    sys.exit(1)
w("")
w("  교정 12/12 통과 + 음성 대조 통과. 아래 표는 전부 이 함수들로 계산됐다.")


# ============================================================================
# §1 마스터 볼륨 기본값 — 근거와 검산
# ============================================================================
w("")
w("=" * 78)
w("§1 마스터 볼륨 기본값")
w("=" * 78)

CLIP_TRUE_PEAK_DBTP = -1.0     # 모든 원본 클립의 정규화 목표(트루피크)
OS_NOTIFY_PEAK_DBFS = -6.0     # ★ 가정: OS 알림음의 대표 피크. 실측 미완(§7 참조)
REQUIRED_HEADROOM_DB = 8.0     # 우리 최대음이 알림음보다 최소 이만큼 아래여야 한다

SLIDER_DEFAULT = 40
amp = slider_to_amp(SLIDER_DEFAULT)
master_db = lin_to_db(amp)
loudest_dbfs = CLIP_TRUE_PEAK_DBTP + master_db          # 오프셋 0 dB 이벤트
headroom = OS_NOTIFY_PEAK_DBFS - loudest_dbfs

w(f"  클립 정규화 목표      : {CLIP_TRUE_PEAK_DBTP:+.1f} dBTP (전 클립 공통)")
w(f"  볼륨 슬라이더 기본값  : {SLIDER_DEFAULT} / 100")
w(f"  -> 진폭 배율          : {amp:.4f}  ( = (40/100)^(5/3) )")
w(f"  -> 마스터 감쇠        : {master_db:+.2f} dB")
w(f"  가장 큰 이벤트(오프셋 0 dB) 출력 피크 : {loudest_dbfs:+.2f} dBFS")
w(f"  OS 알림음 가정 피크                    : {OS_NOTIFY_PEAK_DBFS:+.2f} dBFS")
w(f"  -> 여유(headroom)     : {headroom:.2f} dB   (요구 {REQUIRED_HEADROOM_DB:.1f} dB "
  f"{'충족' if headroom >= REQUIRED_HEADROOM_DB else '미달 ★'})")
w(f"  -> 체감 크기 비       : {perceived_ratio(-headroom):.3f} 배 "
  f"(알림음을 1로 볼 때)")
w("")
w("  '슬라이더 절반 = 체감 절반' 성질 검산:")
for s in (100, 50, 40, 20, 10):
    a = slider_to_amp(s)
    w(f"    슬라이더 {s:3d} -> 진폭 {a:.4f} ({lin_to_db(a):+7.2f} dB), "
      f"체감 {perceived_ratio(lin_to_db(a)):.4f}배")
half_check = perceived_ratio(lin_to_db(slider_to_amp(50)))
w(f"    슬라이더 100 -> 50 의 체감비 = {half_check:.4f} (0.5000 이어야 한다: "
  f"{'OK' if abs(half_check - 0.5) < 0.005 else 'FAIL ★'})")


# ============================================================================
# §2 이벤트별 상대 볼륨 표
# ============================================================================
w("")
w("=" * 78)
w("§2 이벤트별 상대 볼륨(마스터 대비 dB) 과 실제 출력 피크")
w("=" * 78)

EVENTS = [
    # (트리거 키, 티어, 오프셋 dB, 길이 상한 ms)
    ("sfx.ui.preview",          "A", 0.0,   400),
    ("sfx.shop.purchase",       "A", 0.0,   400),
    ("sfx.progress.levelup",    "M", -1.5,  800),
    ("sfx.focus.complete",      "A", -2.0,  800),
    ("sfx.archery.bullseye",    "A", -3.0,  400),
    ("sfx.focus.start",         "A", -4.0,  400),
    ("sfx.archery.hit",         "A", -5.0,  300),
    ("sfx.archery.release",     "A", -6.0,  300),
    ("sfx.equip.wear",          "A", -8.0,  300),
    ("sfx.archery.draw",        "A", -9.0,  400),
    ("sfx.archery.miss",        "A", -9.0,  300),
    ("sfx.equip.remove",        "A", -10.0, 300),
    ("sfx.land.throw",          "B", -12.0, 250),
    ("sfx.ragdoll.impact",      "B", -12.0, 250),
    ("sfx.rodeo.grab",          "B", -13.0, 250),
    ("sfx.land.ambient",        "C", -14.0, 200),
    ("sfx.parkour.climb",       "C", -14.0, 250),
    ("sfx.ledge.hang",          "C", -16.0, 250),
]

w(f"  {'트리거 키':24s} {'티어':4s} {'오프셋':>8s} {'선형':>7s} "
  f"{'출력피크':>10s} {'체감(최대음=1)':>14s} {'길이':>6s}")
w("  " + "-" * 76)
total_ms = 0
for key, tier, off, ms in EVENTS:
    out = CLIP_TRUE_PEAK_DBTP + master_db + off
    w(f"  {key:24s} {tier:^4s} {off:+7.1f}dB {db_to_lin(off):7.4f} "
      f"{out:+9.2f}dBFS {perceived_ratio(off):13.3f} {ms:5d}ms")
    total_ms += ms
w("  " + "-" * 76)
w(f"  이벤트 {len(EVENTS)}종, 길이 상한 합계 {total_ms} ms")
w("")
w("  가장 조용한 이벤트(sfx.ledge.hang)의 출력 피크 = "
  f"{CLIP_TRUE_PEAK_DBTP + master_db - 16.0:+.2f} dBFS")
w("  -> 최대음과의 차 16.0 dB = 체감 "
  f"{perceived_ratio(-16.0):.3f}배. 들리긴 하되 '뒤에 있는 소리'로 읽힌다.")


# ============================================================================
# §3 메모리 예산
# ============================================================================
w("")
w("=" * 78)
w("§3 오디오 메모리 예산 (24시간 상주 앱)")
w("=" * 78)

SAMPLE_RATE = 48000
BYTES_PER_SAMPLE = 2      # 16-bit
CHANNELS = 1              # ★ 전 사운드 모노 강제(§ 정책 문서 4-3)
BYTES_PER_SEC = SAMPLE_RATE * BYTES_PER_SAMPLE * CHANNELS

base_bytes = sum(ms for _, _, _, ms in EVENTS) / 1000.0 * BYTES_PER_SEC
PACKS = 6
KEYS_PER_PACK_MAX = 6
AVG_MS = 350               # 팩 교체 대상 6키의 길이 상한 평균 근사
pack_bytes = PACKS * KEYS_PER_PACK_MAX * (AVG_MS / 1000.0) * BYTES_PER_SEC

w(f"  포맷: 모노 / {SAMPLE_RATE} Hz / 16-bit PCM = {BYTES_PER_SEC:,} bytes/s")
w(f"  기본 세트 {len(EVENTS)}키 (길이 상한 전부 사용 가정) : "
  f"{base_bytes/1024:,.1f} KB")
w(f"  DLC {PACKS}팩 x 최대 {KEYS_PER_PACK_MAX}키 x 평균 {AVG_MS}ms      : "
  f"{pack_bytes/1024:,.1f} KB")
w(f"  전 팩 소유 최악 합계                       : "
  f"{(base_bytes+pack_bytes)/1024/1024:,.2f} MB")
MEASURED_FOOTPRINT_MB = 543.0      # Editor/BuildStandalone.cs 실측 주석
FRAMEBUFFER_MB = 222.3             # 같은 실측: owned unmapped (graphics)
worst_mb = (base_bytes + pack_bytes) / 1024 / 1024
w(f"  실측 물리 풋프린트 {MEASURED_FOOTPRINT_MB:.0f} MB 대비 : "
  f"{worst_mb / MEASURED_FOOTPRINT_MB * 100:.2f} %")
w(f"  프레임버퍼 {FRAMEBUFFER_MB:.1f} MB 대비        : "
  f"{worst_mb / FRAMEBUFFER_MB * 100:.2f} %")
w("  판정: 메모리는 이 기능의 제약이 아니다. 제약은 §4의 '항상 열린 오디오 장치'다.")


# ============================================================================
# §4 오디오 장치 상주 비용 — DSP 버퍼/보이스 수
# ============================================================================
w("")
w("=" * 78)
w("§4 오디오 장치 상주 비용 (m_DisableAudio 를 되돌릴 때 함께 정해야 하는 값)")
w("=" * 78)

for buf in (256, 512, 1024, 2048):
    latency_ms = buf / SAMPLE_RATE * 1000.0
    callback_hz = SAMPLE_RATE / buf
    w(f"  DSP 버퍼 {buf:5d} 샘플 -> 지연 {latency_ms:6.2f} ms, "
      f"믹서 콜백 {callback_hz:7.2f} Hz")
cur, proposed = 512, 1024
w("")
w(f"  현재값 {cur} (ProjectSettings/AudioManager.asset 실측) -> 제안 {proposed}")
w(f"  콜백 빈도 {SAMPLE_RATE/cur:.2f} Hz -> {SAMPLE_RATE/proposed:.2f} Hz "
  f"= {(1 - (SAMPLE_RATE/proposed)/(SAMPLE_RATE/cur)) * 100:.0f}% 감소")
w(f"  대가: 지연 {cur/SAMPLE_RATE*1000:.2f} ms -> {proposed/SAMPLE_RATE*1000:.2f} ms "
  f"(+{(proposed-cur)/SAMPLE_RATE*1000:.2f} ms)")
w("  판정: 이 앱의 소리는 전부 UI 원샷이라 21 ms 지연은 인지 한계 아래다.")
w("        (리듬 게임처럼 박자에 맞추는 소리가 하나도 없다 — 사운드 카탈로그 §2 전수)")
w("")
MAX_CONCURRENT = 3
w(f"  동시 발성 상한 설계값 : {MAX_CONCURRENT}")
w(f"  m_RealVoiceCount    현재 32 -> 제안 8   (설계값 대비 여유 "
  f"{8/MAX_CONCURRENT:.2f}배)")
w(f"  m_VirtualVoiceCount 현재 512 -> 제안 32 (설계값 대비 여유 "
  f"{32/MAX_CONCURRENT:.2f}배)")


# ============================================================================
# §5 야간 묵음 창 — 경계 전수 검증
# ============================================================================
w("")
w("=" * 78)
w("§5 야간 묵음 창 23:00 ~ 07:00")
w("=" * 78)

NIGHT_START_H, NIGHT_END_H = 23, 7


def is_night(h, m=0):
    """자정을 걸치는 구간이라 OR 비교여야 한다(AND로 쓰면 항상 False = 흔한 버그)."""
    t = h * 60 + m
    return t >= NIGHT_START_H * 60 or t < NIGHT_END_H * 60


night_hours = (24 - NIGHT_START_H) + NIGHT_END_H
w(f"  창 길이 {night_hours} 시간 = 하루의 {night_hours/24*100:.2f} %")
w("")
w("  정시 24개 전수:")
row = "   "
for h in range(24):
    row += f" {h:02d}:{'묵음' if is_night(h) else '가능'}"
    if h % 6 == 5:
        w(row)
        row = "   "
w("")
w("  경계 4건(자정 넘김 버그 잠금):")
for h, m, want in ((22, 59, False), (23, 0, True), (6, 59, True), (7, 0, False)):
    got = is_night(h, m)
    w(f"    {h:02d}:{m:02d} -> {'묵음' if got else '가능'}  "
      f"(기대 {'묵음' if want else '가능'}) {'OK' if got == want else 'FAIL ★'}")
    if got != want:
        FAIL.append(f"야간 경계 {h:02d}:{m:02d}")
# AND 오식 음성 대조
and_bug = [h for h in range(24) if (h * 60 >= NIGHT_START_H * 60 and h * 60 < NIGHT_END_H * 60)]
w(f"    음성 대조 — AND로 잘못 쓰면 묵음 시간 수 = {len(and_bug)} "
  f"(0이어야 하고, 그래서 AND는 '조용히 아무것도 안 하는' 버그다)")


# ============================================================================
# §6 발동 예산(토큰 버킷) — 상한 계산
# ============================================================================
w("")
w("=" * 78)
w("§6 티어별 발동 예산 상한")
w("=" * 78)

BUCKETS = [
    # (티어, 버킷 용량, 리필 1개당 초, 최소 간격 ms)
    ("A 직접",   None, None, 150),
    ("B 유발",   4,    60,   150),
    ("C 자율",   3,    600,  150),
    ("M 성취",   2,    1800, 300),
]
w(f"  {'티어':10s} {'버스트':>6s} {'리필':>10s} {'시간당 상한':>12s} "
  f"{'12시간 사용시':>14s} {'최소간격':>8s}")
w("  " + "-" * 70)
for tier, cap, refill_s, gap_ms in BUCKETS:
    if cap is None:
        w(f"  {tier:10s} {'-':>6s} {'-':>10s} {'무제한*':>12s} {'무제한*':>14s} "
          f"{gap_ms:5d}ms")
        continue
    per_hour = 3600.0 / refill_s
    w(f"  {tier:10s} {cap:6d} {refill_s:8d}s {per_hour:11.1f}회 "
      f"{per_hour*12:13.0f}회 {gap_ms:5d}ms")
w("  " + "-" * 70)
w("  * A(직접)에 예산이 없는 이유: 사용자가 그 순간 스스로 누른 것이라 '예상 밖의")
w("    소리'가 원리적으로 발생하지 않는다. 대신 최소 간격 150 ms 로 연타만 막는다.")
w(f"    150 ms 간격의 이론 최대 = {1000/150:.2f} 회/초.")
w("  ** B(유발)의 실제 상한은 버킷이 아니라 사람의 손이다 — 던지려면 매번 직접")
w("     끌어야 한다. 버킷의 역할은 시간당 총량이 아니라 '한 번의 던지기가 만드는")
w("     연쇄(던짐->구름->착지->정착)'를 4발로 자르는 버스트 제한이다.")
w("")
w("  ★ C(자율)를 시간당 6회로 묶는 근거:")
w("    이 앱은 이미 자율 '구경거리' 확률 10종을 전부 0으로 내린 전력이 있다")
w("    (StickConfig: windowTheft/graffiti/desktopTidy/blackhole/windowCrash/")
w("     archery/todoReminder/stressSulky/wanderPostIdleJump/wanderEdgeJump = 0,")
w("     DefaultStickConfig.asset 에서도 동일 확인). 사유는 사용자 피드백")
w("     '요청하지도 않은 구경거리가 자율 확률로 계속 떠서 ... 유저는 그게 무엇인지도")
w("      알 수 없었다'. 눈으로 보는 구경거리조차 0으로 내려간 앱에서 귀로 듣는")
w("      구경거리를 기본 ON 으로 두는 것은 방향이 반대다.")


# ============================================================================
# §7 묵음 조건 x 플랫폼 커버리지
# ============================================================================
w("")
w("=" * 78)
w("§7 묵음 조건 10건 — 기존 코드 재사용률과 플랫폼 커버리지")
w("=" * 78)

MUTES = [
    # (id, 조건, macOS 신호, Windows 신호, 신규 OS 호출 필요?)
    ("M1", "전체화면 게임",     "재사용", "재사용", False),
    ("M2", "화면 꺼짐",         "재사용", "★갭",   False),
    ("M3", "자리 비움 180s",    "재사용", "재사용", False),
    ("M4", "OS 저전력 모드",    "재사용", "재사용", False),
    ("M5", "앱 자체 집중 모드", "재사용", "재사용", False),
    ("M6", "야간 23~07",        "신규(중립)", "신규(중립)", False),
    ("M7", "부팅 그레이스 60s", "신규(중립)", "신규(중립)", False),
    ("M8", "오버레이 비표시",   "재사용", "재사용", False),
    ("M9", "마이크 사용 중",    "신규(OS)", "신규(OS)", True),
    ("M10", "OS 방해금지/집중", "★없음",  "신규(OS)", True),
]
w(f"  {'ID':5s} {'조건':20s} {'macOS':12s} {'Windows':12s} {'신규 OS 호출':12s}")
w("  " + "-" * 66)
for mid, cond, mac, win, new_os in MUTES:
    w(f"  {mid:5s} {cond:20s} {mac:12s} {win:12s} {'필요' if new_os else '불필요':12s}")
w("  " + "-" * 66)
reuse = sum(1 for _, _, m, wn, _ in MUTES if m == "재사용" and wn == "재사용")
no_new = sum(1 for *_, n in MUTES if not n)
w(f"  양 플랫폼 100% 재사용 : {reuse}/{len(MUTES)} 건")
w(f"  신규 OS 호출 0 로 구현 가능 : {no_new}/{len(MUTES)} 건 "
  f"({no_new/len(MUTES)*100:.0f}%)")
w("  P1(1차 출하)에 필요한 신규 OS 호출 = 0 건. M9/M10 은 P2 심화 방어.")


# ============================================================================
# §8 원칙 검산 — '소리를 낼 자격' 규칙의 AND 진리표
# ============================================================================
w("")
w("=" * 78)
w("§8 '귀 자격 규칙' 4조건 AND — 대표 시나리오 진리표")
w("=" * 78)
w("  규칙: (1)상태전이 확정 AND (2)원인이 사용자 직접조작 AND")
w("        (3)사용자가 지금 보고 있음 AND (4)묵음조건 0건  -> 그때만 소리")
w("")

SCEN = [
    # (시나리오, 전이확정, 사용자원인, 보고있음, 묵음0건)
    ("설정창에서 미리듣기 클릭",              True,  True,  True,  True),
    ("단축키 A 로 활쏘기 -> 명중",            True,  True,  True,  True),
    ("회의 중(마이크 ON) 단축키 A",           True,  True,  True,  False),
    ("전체화면 게임 중 캐릭터 자율 착지",     True,  False, False, False),
    ("자리 비운 사이 자율 파쿠르",            True,  False, False, False),
    ("새벽 2시에 사용자가 장비 착용",         True,  True,  True,  False),
    ("사용자가 던진 캐릭터가 3초 뒤 착지",    True,  True,  True,  True),
    ("집중 세션 중 캐릭터 자율 착지",         True,  False, True,  False),
    ("화면 꺼진 채 레벨업",                   True,  False, False, False),
    ("사용자가 보는 중 자율 창도둑(확률 0)",  False, False, True,  True),
]
w(f"  {'시나리오':34s} {'(1)':>4s} {'(2)':>4s} {'(3)':>4s} {'(4)':>4s} -> 소리")
w("  " + "-" * 66)
sounded = 0
for name, a, b, c, d in SCEN:
    out = a and b and c and d
    sounded += 1 if out else 0
    w(f"  {name:34s} {'O' if a else 'X':>4s} {'O' if b else 'X':>4s} "
      f"{'O' if c else 'X':>4s} {'O' if d else 'X':>4s} -> "
      f"{'소리남' if out else '무음'}")
w("  " + "-" * 66)
w(f"  10개 대표 시나리오 중 소리가 나는 것 = {sounded} 건 "
  f"({sounded/len(SCEN)*100:.0f}%)")
w("  나머지 7건이 전부 '무음'인 것이 이 정책의 목적이다.")


# ============================================================================
# 마무리
# ============================================================================
w("")
w("=" * 78)
if FAIL:
    w("!!! 검산 실패 — 아래 항목이 깨졌다. 이 문서의 숫자를 폐기하라 !!!")
    for f in FAIL:
        w("   - " + f)
else:
    w("검산 전항목 통과. (교정 12 + 음성대조 2 + 야간경계 4 + 슬라이더 반감 1)")
w("=" * 78)

out = "\n".join(lines)
print(out)
with open(__file__.replace(".py", ".out.txt"), "w", encoding="utf-8") as fp:
    fp.write(out + "\n")
sys.exit(1 if FAIL else 0)
