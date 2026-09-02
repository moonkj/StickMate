# -*- coding: utf-8 -*-
"""영어 게이트 §3 — 분기 술어가 실제 대사 말뭉치를 어떻게 가르는가 (문자류 전수 조사).

거짓통과 #4 방어: 모든 '없음' 판정에 양성 대조를 붙인다."""
import re, os, sys, io, unicodedata, glob
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
ROOT = "/Users/kjmoon/App/StickMate"
SRC  = os.path.join(ROOT, "Assets/_Project/Scripts")

kind = open(os.path.join(SRC, "Dialogue/DialogueKind.cs"), encoding="utf-8").read()
def const(n, s=kind):
    m = re.search(r"%s\s*=\s*([\d.]+)f" % n, s)
    assert m, "상수 %s 를 소스에서 못 찾았다 — 이 스크립트의 모든 숫자를 폐기하라" % n
    return float(m.group(1))
BASE, PER, MINS, MAXS = const("BaseSeconds"), const("PerGlyphSeconds"), const("MinSeconds"), const("MaxSeconds")
FADEIN, POPIN = const("FadeInSeconds"), const("PopInSeconds")

# ---------- 교정 (알려진 값으로 먼저) ----------
def reading(n): return min(max(BASE + n*PER, MINS), MAXS)
def required(n): return FADEIN + reading(n)
CAL = [(10, 1.090), (8, 0.940), (7, 0.865)]
for n, exp in CAL:
    assert abs(required(n)-exp) < 1e-9, ("교정 실패", n, required(n), exp)
print("[교정] 필요체류 10자=1.090 / 8자=0.940 / 7자=0.865 — 재현 OK")
print("[교정] 소스에서 읽은 상수: Base=%.3f Per=%.3f Min=%.2f Max=%.2f FadeIn=%.2f PopIn=%.2f"
      % (BASE, PER, MINS, MAXS, FADEIN, POPIN))

# ---------- 대사 말뭉치 전수 수집 ----------
# 소스 스캐너 사각지대(선행 라운드 census 21%)를 피하려고 '대사로 쓰이는 문자열 리터럴'을
# 파일 단위가 아니라 **DialogueLine.Say / 대사 표 배열** 두 경로 모두에서 긁는다.
files = glob.glob(os.path.join(SRC, "**/*.cs"), recursive=True)
files = [f for f in files if "/Tests/" not in f]
lit = re.compile(r'"((?:[^"\\]|\\.)*)"')
corpus = {}          # 문자열 -> 출처 파일 집합
for f in files:
    s = open(f, encoding="utf-8").read()
    for m in re.finditer(r'DialogueLine\.(?:Say|React|Narrate|Shout)\s*\(\s*"((?:[^"\\]|\\.)*)"', s):
        corpus.setdefault(m.group(1), set()).add(os.path.basename(f))
for name in ("Dialogue/AmbientChatter.cs",):
    s = open(os.path.join(SRC, name), encoding="utf-8").read()
    for m in re.finditer(r'^\s{12}"((?:[^"\\]|\\.)*)",\s*$', s, re.M):
        corpus.setdefault(m.group(1), set()).add(os.path.basename(name))
print("\n[말뭉치] 수집한 대사 문자열 %d개" % len(corpus))

# ---------- 양성 대조 A : 수집기가 실제로 작동하는가 ----------
must = ["음...", "여기 좋네", "산책 중", "다리가 잘 나가네"]
missing = [x for x in must if x not in corpus]
assert not missing, ("양성 대조 A 실패 — 수집기가 알려진 대사를 못 봤다", missing)
print("[양성대조 A] 알려진 대사 4줄이 전부 수집됐다 — 수집기 작동 확인")

# ---------- 문자류 분류 ----------
def cls(c):
    o = ord(c)
    if 0xAC00 <= o <= 0xD7A3: return "한글음절"
    if 0x1100 <= o <= 0x11FF: return "한글자모(조합용)"
    if 0x3130 <= o <= 0x318F: return "한글자모(호환)"
    if 0xA960 <= o <= 0xA97F or 0xD7B0 <= o <= 0xD7FF: return "한글자모(확장)"
    if 0x4E00 <= o <= 0x9FFF: return "CJK한자"
    if 0x3040 <= o <= 0x30FF: return "가나"
    if c.isascii(): return "ASCII"
    return "기타(U+%04X)" % o

tally = {}
for t in corpus:
    for c in t: tally[cls(c)] = tally.get(cls(c), 0) + 1
print("\n[문자류] 대사 말뭉치 전체 글자 분포")
for k in sorted(tally, key=lambda x: -tally[x]):
    print("   %-18s %4d자" % (k, tally[k]))

# ---------- 양성 대조 B : 분류기가 실제로 가르는가 ----------
probe = {"가": "한글음절", "ㅋ": "한글자모(호환)", "漢": "CJK한자", "ア": "가나", "A": "ASCII", "…": "기타(U+2026)"}
for c, exp in probe.items():
    got = cls(c)
    assert got == exp, ("양성 대조 B 실패", c, got, exp)
print("[양성대조 B] 분류기가 가/ㅋ/漢/ア/A/… 를 각각 다르게 가른다 — 0건 판정을 믿어도 된다")

# ---------- ★ 분기 술어 후보 3안 ----------
def p_narrow(t):   return any(0xAC00 <= ord(c) <= 0xD7A3 for c in t)
def p_mid(t):      return any(0xAC00 <= ord(c) <= 0xD7A3 or 0x4E00 <= ord(c) <= 0x9FFF
                              or 0x3040 <= ord(c) <= 0x30FF for c in t)
def p_wide(t):     return p_mid(t) or any(0x1100 <= ord(c) <= 0x11FF or 0x3130 <= ord(c) <= 0x318F
                              or 0xA960 <= ord(c) <= 0xA97F or 0xD7B0 <= ord(c) <= 0xD7FF for c in t)
print("\n[분기] 세 후보 술어가 지금 말뭉치를 어떻게 가르는가")
for nm, p in (("① 한글음절만", p_narrow), ("② +한자/가나", p_mid), ("③ +한글자모", p_wide)):
    kr = [t for t in corpus if p(t)]
    print("   %-14s CJK분기 %2d줄 / 라틴분기 %2d줄" % (nm, len(kr), len(corpus)-len(kr)))
diff = [t for t in corpus if p_wide(t) != p_narrow(t)]
print("   ① vs ③ 판정이 갈리는 실재 대사: %d줄  %s" % (len(diff), diff if diff else "(없음)"))

# ---------- 함정 문자열: 술어별 판정 차이가 실제로 요금을 바꾸는가 ----------
W = 0.0472
def reading_en(n): return min(max(BASE + n*W, MINS), MAXS)
print("\n[함정] 지금은 말뭉치에 없지만 언젠가 반드시 들어오는 형태")
TRAPS = ["ㅋㅋㅋ", "ㅎㅎ", "30", "Lv.9", "Wi-Fi 끊겼네", "!!!", "...", "", "   "]
print("   문자열              | 길이 | ①  | ③  | CJK요금 | 라틴요금 | 차이")
for t in TRAPS:
    a, b = p_narrow(t), p_wide(t)
    print("   %-18s | %4d | %-3s| %-3s| %7.3f | %8.3f | %s"
          % (repr(t)[1:-1] or "(빈)", len(t), "CJK" if a else "라틴", "CJK" if b else "라틴",
             reading(len(t)), reading_en(len(t)),
             "판정 갈림 ★" if a != b else "같음"))
print("\n   -> ★ 'ㅋㅋㅋ'은 ①에서 라틴 분기로 떨어져 **한국어인데 싸게 청구**된다.")
print("      요금 자체는 둘 다 하한 0.62에 걸려 같지만(짧아서), 길어지면 갈라진다:")
for t in ("ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ",):
    print("      %s (%d자): CJK %.3f초 vs 라틴 %.3f초 — %.3f초 차이"
          % (t, len(t), reading(len(t)), reading_en(len(t)), reading(len(t))-reading_en(len(t))))
