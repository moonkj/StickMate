# -*- coding: utf-8 -*-
"""★ 풀 크기 재계산 — '표에 적힌 줄 수'가 아니라 '그 순간 자격을 가진 줄 수'로 센다.

선행 라운드(R2 §2-1)는 N=24로 k를 풀었다. 이 스크립트는 그 N이 **어느 순간에도 존재하지 않는
숫자**임을 보이고, 실제 N(동시 자격 집합의 크기)으로 계약을 다시 푼다."""
import sys, io, math, itertools
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# ---------- 생일 문제 ----------
def no_repeat(N, k):
    if k > N: return 0.0
    p = 1.0
    for i in range(k): p *= (N - i) / N
    return p
def max_k(N, floor=0.5):
    k = 1
    while no_repeat(N, k + 1) >= floor: k += 1
    return k

# 교정: 알려진 값으로 먼저 (R2 §2-1 표)
CAL = [(13, 4, 0.601), (24, 6, 0.507), (34, 7, 0.516), (55, 9, 0.501)]
for N, k, pnr in CAL:
    got = no_repeat(N, k)
    assert abs(got - pnr) < 0.002, ("교정 실패", N, k, got, pnr)
    assert max_k(N) == k, ("교정 실패(k)", N, max_k(N), k)
print("[교정] R2 §2-1 표 4행(N=13/24/34/55 -> k=4/6/7/9)을 재현했다. 계산기 신뢰 OK")

# ---------- R2 24줄 표를 자격 축으로 분해 ----------
# (상태, 자격축, 자격값)  자격축: ALWAYS / MOTION / DOW(요일) / TOD(시간대)
NEUTRAL = [
 ("Idle","ALWAYS",None,"음..."),            ("Idle","ALWAYS",None,"여기 좋네"),
 ("Idle","ALWAYS",None,"잠깐 쉬는 중"),      ("Idle","ALWAYS",None,"오늘 뭐 하지"),
 ("Idle","ALWAYS",None,"발밑이 단단해"),
 ("Walk","ALWAYS",None,"산책 중"),          ("Walk","ALWAYS",None,"저쪽으로 가볼까"),
 ("Walk","ALWAYS",None,"하나 둘 하나 둘"),   ("Walk","ALWAYS",None,"다리 좀 풀자"),
 ("Walk","ALWAYS",None,"다리가 잘 나가네"),
 ("Idle","MOTION","yawn","하암..."),        ("Idle","MOTION","look","구경 중이야"),
 ("Idle","DOW","mon","월요일이네..."),       ("Idle","DOW","fri","금요일이다!"),
 ("Idle","DOW","week","쉬는 날이네"),
 ("Walk","DOW","mon","월요일이 왔네"),       ("Walk","DOW","fri","주말이 코앞이네"),
 ("Walk","DOW","week","주말 산책이네"),
 ("Idle","TOD","morn","아침이네"),          ("Idle","TOD","noon","점심시간이네"),
 ("Idle","TOD","night","밤이 깊었네"),
 ("Walk","TOD","morn","아침 산책이네"),      ("Walk","TOD","noon","점심때 걷네"),
 ("Walk","TOD","night","밤에도 걷네"),
]
assert len(NEUTRAL) == 24, len(NEUTRAL)
print("[입력] R2 §3-4 중립 풀 24줄 — 자격축으로 분해 완료")

def eligible(pool, dow, tod, motions):
    """지금 이 순간 뽑힐 자격이 있는 줄만 남긴다."""
    out = []
    for st, ax, val, txt in pool:
        if ax == "ALWAYS": out.append((st, txt))
        elif ax == "DOW"    and val == dow: out.append((st, txt))
        elif ax == "TOD"    and val == tod: out.append((st, txt))
        elif ax == "MOTION" and val in motions: out.append((st, txt))
    return out

DOWS = [None, "mon", "fri", "week"]     # None = 화/수/목
TODS = [None, "morn", "noon", "night"]  # None = 그 밖의 시각
MOTS = [set(), {"yawn"}, {"look"}, {"yawn","look"}]

print("\n=== 1. ★ '풀 24줄'은 어느 순간에도 존재하지 않는다 ===")
print("   요일   시간대  모션      | Idle | Walk |  N  | 허용 k | 그때 중복확률")
rows = []
for d, t, m in itertools.product(DOWS, TODS, MOTS):
    e = eligible(NEUTRAL, d, t, m)
    ni = sum(1 for s, _ in e if s == "Idle"); nw = len(e) - ni
    N = len(e); k = max_k(N)
    rows.append((d, t, m, ni, nw, N, k))
worst = min(rows, key=lambda r: r[5]); best = max(rows, key=lambda r: r[5])
for r in (worst, best):
    d, t, m, ni, nw, N, k = r
    print("   %-6s %-6s %-9s | %4d | %4d | %3d | %5d  | %.1f%%"
          % (d or "화수목", t or "그밖", ",".join(sorted(m)) or "없음", ni, nw, N, k, (1-no_repeat(N,k))*100))
Ns = sorted({r[5] for r in rows})
print("   ...조합 %d가지 전체에서 N의 범위 = %d ~ %d,  허용 k의 범위 = %d ~ %d"
      % (len(rows), min(Ns), max(Ns), min(max_k(n) for n in Ns), max(max_k(n) for n in Ns)))
print("   ★ N=24는 조합 %d가지 중 **%d가지**에서만 나온다 -> R2가 푼 k=6은 과대평가다."
      % (len(rows), sum(1 for r in rows if r[5] == 24)))

print("\n=== 2. 계약을 최악 조합으로 다시 푼다 ===")
SESSION = 23 * 60.0     # 민지 관측 세션(초)
for label, N in (("최악(화수목·시간대밖·모션없음)", worst[5]), ("최선(월/금/주말·시간대·모션2)", best[5]),
                 ("R2가 가정한 표 전체", 24)):
    k = max_k(N)
    print("   %-28s N=%2d  k=%d  ->  필요 평균간격 %6.1f초 (5분당 %.2f회, 하루 %4.0f회)"
          % (label, N, k, SESSION/k, 300/(SESSION/k), 86400/(SESSION/k)))
print("   ★ 최악 조합이 계약을 지배한다 — 계약은 '평균적으로'가 아니라 '언제나' 지켜야 한다.")

print("\n=== 3. 세트 전환 — 상시 10줄을 세트 줄로 바꾼다 ===")
print("   R1 §3-5는 세트당 Idle 4 + Walk 3 = **7줄**을 냈다.")
print("   R2 §3-6은 '상시 10줄이 세트 10줄로 바뀐다'고 적었다.  -> ★ 내 두 라운드가 어긋나 있다.")
for setsize_i, setsize_w, label in ((4,3,"R1 실제(7줄)"), (5,5,"이번 라운드 권고(10줄)")):
    SET = ([("Idle","ALWAYS",None,"set-i%d"%i) for i in range(setsize_i)]
         + [("Walk","ALWAYS",None,"set-w%d"%i) for i in range(setsize_w)]
         + [x for x in NEUTRAL if x[1] != "ALWAYS"])
    rs = []
    for d, t, m in itertools.product(DOWS, TODS, MOTS):
        N = len(eligible(SET, d, t, m)); rs.append(N)
    Nw = min(rs); k = max_k(Nw)
    print("   %-18s 표 %2d줄 | 최악 N=%2d | k=%d | 필요간격 %6.1f초 | 중립 최악(N=%d,k=%d) 대비 %s"
          % (label, len(SET), Nw, k, SESSION/k, worst[5], max_k(worst[5]),
             "계약 보존 ✔" if k == max_k(worst[5]) else "★ 계약 후퇴 (%d -> %d)" % (max_k(worst[5]), k)))

print("\n=== 4. 세트 풀 총량 ===")
for per in (7, 10):
    print("   세트당 %2d줄 x 6세트 = %2d줄 | 중립 24 + 세트 %2d = SpeechKey %3d개 (톤 2열 -> 문장 %3d개)"
          % (per, per*6, per*6, 24 + per*6, (24 + per*6)*2))
