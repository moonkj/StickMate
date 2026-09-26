#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# =============================================================================
# §X 잠금 검사기 — qa-regression (2026-09-26 신설 · 저장소 파일)
#
#   python3 docs/verify/x56check.py            # 전량 판정(인자 없음)
#   python3 docs/verify/x56check.py --selftest  # 변이 대조만(음성 대조 포함)
#   python3 docs/verify/x56check.py --print     # 잠금표 재생성(이 파일에 박을 리터럴을 찍는다)
#
# 종료코드: 0 = 초록(§X가 잠금표와 글자 단위로 같다)
#           3 = 빨강(잠긴 줄이 바뀌었다 / 사라졌다 / 늘었다, 또는 렌더 결함이 생겼다)
#           2 = 판정 불가(문서·절 없음 · 추출기 죽음 · 잠금표 비었음 · 범주 0 · 변이 대조 실패 · 누출 가드)
#   ★ 메시지가 아니라 **rc로** 판정한다. 파이프에 물리면 rc가 먹히니 파이프 없이 재라.
#
# 왜 생겼나 (이 파일의 존재 이유):
#   2026-09-15판 검사기(`x56check.py`·`synth_check.py`)는 세션 임시 폴더와 함께 사라졌고,
#   체크표가 그 사실을 스스로 적었다(출처 표 「§X X-6 합성 대조」 행 — 「그 집계는 재현할 수 없다」).
#   ⇒ 잠긴 줄조차 다음 세션에 재현 불가였다. 그래서 검사기를 저장소로 들인다.
#   선례: `docs/verify/promised_tests_scan.py`.
#
# 무엇을 잠그나:
#   §X 절의 **모든 줄**(절차 줄 · 판독 줄 · PowerShell 블록 **내부** 줄 · 표 · 불릿 · 공통 인용문 · 빈 줄).
#   2026-09-15판이 「결함 판정을 결정하는 줄」만 잠가서 0·1·3·4·5·8번과 절차 줄에 구멍이 남았고,
#   ⓐ 극성 뒤집기·대기 횟수 변경이 rc 0으로 샜다(같은 행의 자백). 전 줄을 잠그면 그 구멍이 구조적으로 없다.
#
# 짝짓기를 쓰지 않는다 — **자리별(positional) 해시**다:
#   줄 짝짓기(difflib 등)로 판정하면 **빈 줄·짧은 줄이 짝짓기에서 사라져** 삭제가 통과한다
#   (이 저장소에서 「삭제 0」 확인이 네 번 무너진 형태다). 자리별 비교는 줄 수가 하나만 달라도
#   그 자리에서 어긋나므로 빈 줄 삭제까지 잡는다(교정 M7이 그것을 잰다). difflib은 **진단 인쇄에만** 쓴다.
#
# 공개 저장소 규칙 (2026-09-26 확장 — security 전수 실측 인계):
#   경로는 저장소 상대경로만 인쇄한다. 세션 임시 폴더 경로·사용자 이름·호스트명·IP는 이 파일에 한 글자도 없다.
#   누출 가드는 세 곳에 건다 — ① 이 파일 자신 ② **인쇄 직전 출력**(scrub) ③ **§X 문서 본문**(rc 2).
#   ②가 없던 첫 판에는 구멍이 있었다: 검사기가 rc 3을 내며 **문서 원문 60자**를 그대로 찍어서,
#   사본에 심은 가짜 Windows 경로가 가드에 걸리지 않고 출력으로 샜다(security 실증).
#   ③은 「§X에 사용자 경로·계정·IP가 섞였다」를 rc 2로 떨어뜨려 공개 저장소 유입을 구조적으로 막는다.
#   잡는 형태: 유닉스·리눅스 홈 경로 · 윈도 사용자 경로 · UNC 공유 · C: 아닌 드라이브 · IPv4 ·
#     이메일 · 윈도 SID · GUID · 임시 폴더 조각 4종. 환경변수 자리표시자(`$env:USERPROFILE\...`)는 잡지 않는다.
#   ★ **구조적 한계 자백**: 이름 대조의 원천은 이 머신의 `id -un`·호스트명인데, §X가 모으는 것은
#     **사용자 PC의 Windows 계정 이름**이라 두 값은 같을 수 없다 ⇒ 이름 대조로는 이 절차가 걱정하는
#     그 이름을 원리상 못 잡는다. 그래서 이름이 아니라 **형태**를 잡는 정규식이 주력이고 이름 대조는 보조다.
#     면제 목록·길이 하한은 두지 않는다(그 둘이 그대로 구멍이었다).
# 쓰기: 저장소 파일을 쓰지 않는다. git을 **한 번도 부르지 않는다**(=`.git/index`를 건드릴 경로가 없다).
#   Unity 무관 · Assets 무수정.
# =============================================================================
import hashlib
import os
import re
import subprocess
import sys
import unicodedata

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.realpath(__file__))))
SELF_REL = os.path.join("docs", "verify", "x56check.py")
DOC_REL = os.path.join("docs", "verify", "WINDOWS_CHECK_SESSION.md")
SECTION_HEAD = "## §X."
HASH_CHARS = 16

# ----------------------------------------------------------------------------
# 잠금표 — `--print`로 생성한다. 항목 형식: "<sha256 앞 16자>:<줄 길이(문자 수)>"
#   길이를 함께 박는 이유: 1글자 변이(「예」→「아니오」 같은 극성 뒤집기)가 길이로도 드러나
#   해시가 같은 우연(천문학적으로 낮지만)에 의존하지 않게 하고, 진단에 쓸 수 있기 때문이다.
# ----------------------------------------------------------------------------
LOCK_BLOB = """\
c5bebb03d7fbad7b:243
e3b0c44298fc1c14:0
0a81cbcc3234a06d:169
396d6045dcc6c08b:319
638aaecd3802fd9c:209
b851a3b39321eb7f:701
550d53fab28d1c43:291
67ffb26eddd918f7:100
de75ed813a0f67dd:1767
1dd87318ee7fe689:306
b694efb0d2fef4b6:672
19d193bc0315cce4:580
e3b0c44298fc1c14:0
ad9899694c9e86ed:68
7040f6868d48b449:233
16ad6e6d93a4915a:42
e3e9ab424430c000:15
0fb240b661991c98:5
e8551d1bd7d2e017:15
3e55415503de7068:134
a9b503f920acffb3:59
faa5e199597457f4:135
42b295caca69df6a:76
3dd75320763b8d89:67
157e294358871e69:46
aa114fd3a5fc63c8:180
475e7b81acdc741f:62
7d456aab7d79be2a:136
2c7df558179ea71e:218
753e7c35160df02f:11
5c62825e4a08b0ab:65
693ecbacd2808d48:56
07191aceffed83be:54
59db628ebd142f3a:95
b9a9a048a1df8c81:67
654c6a694c2883fd:74
3601ef1d2cdb3863:57
56769e5bc1271b06:3
666003d52605b00c:55
737db166c79ae98e:3
46686ee1332c3253:5
47fd1906e1a32728:30
f8a8f4b2794c0b0a:134
163ef17c9872ef2f:132
424dec9bcb7069fd:46
5496b4abbf9830ff:108
c1c1c621b0704251:190
7487d0534ad2e674:41
6ea2da4972b7b25f:138
9a5c2f8f7ea4ce0e:230
3a1e0931bb5b2c93:747
fca30281e76d9a9d:279
869b8dd4f289ff06:68
e3b0c44298fc1c14:0
6728a534d85e8430:64
6f075b2430a6e2dc:212
c4cf5997edac00a6:42
e3e9ab424430c000:15
0fb240b661991c98:5
f7240bfcea0280fb:15
3e55415503de7068:134
a9b503f920acffb3:59
faa5e199597457f4:135
42b295caca69df6a:76
3dd75320763b8d89:67
157e294358871e69:46
aa114fd3a5fc63c8:180
475e7b81acdc741f:62
1fe56e583e9bb10e:133
203e75e265dadd36:89
753e7c35160df02f:11
5c62825e4a08b0ab:65
0954f07ea906c92c:94
17ac0d7979f0f1e1:89
bb18a961e4c082b4:75
804dcdc214bf5555:25
c99fc99ffec343a4:22
56769e5bc1271b06:3
666003d52605b00c:55
737db166c79ae98e:3
46686ee1332c3253:5
68b782f858b3890b:13
82ca3cdcafee84be:11
0e4470f1bc361256:140
f6f73b46734906b0:79
89738fe39e3150f8:80
a70caa0641b3d4ad:110
cb03be352a6542ac:86
5714de33783d8661:65
e3b0c44298fc1c14:0
6815726a16de096f:83
413edce51373d1f6:338
3f986269f8fa4023:196
9e315807208a1b16:93
f88129e184423e27:234
c6af08dc4a26d32e:42
e3e9ab424430c000:15
0fb240b661991c98:5
4e2a22c3888f9981:34
3e55415503de7068:134
a9b503f920acffb3:59
faa5e199597457f4:135
42b295caca69df6a:76
3dd75320763b8d89:67
157e294358871e69:46
aa114fd3a5fc63c8:180
475e7b81acdc741f:62
7d456aab7d79be2a:136
753e7c35160df02f:11
5c62825e4a08b0ab:65
0e75ad5914af8953:65
a0f6b3e92aa37eda:66
4b9b4ea824dfe755:61
1c376b69a215cc9d:85
f761ca310840a855:56
1bcf0ef290f2f32f:37
86ddb573f6658a39:48
d4d57a21439d4bae:73
7c5b8d4c687d7f84:33
0617bfb2cbddb269:39
56769e5bc1271b06:3
666003d52605b00c:55
737db166c79ae98e:3
46686ee1332c3253:5
68b782f858b3890b:13
82ca3cdcafee84be:11
c4cadcd95a933a75:75
9e170c9d9bff13fa:249
b5ef10016a5a1785:70
69828155b8748d47:70
8cc0d848050c892b:70
beccfc6f5d8befed:35
46844aa0e1de3f8e:97
6cce5abf681de6b4:152
975681572834d6a4:79
e3b0c44298fc1c14:0
18213a9029688f0a:88
fd0b996590871cd0:81
9299528b5ce01833:263
889e9e55e2211ba1:42
e3e9ab424430c000:15
0fb240b661991c98:5
e29ee8a9302d287e:15
3e55415503de7068:134
a9b503f920acffb3:59
faa5e199597457f4:135
42b295caca69df6a:76
3dd75320763b8d89:67
157e294358871e69:46
aa114fd3a5fc63c8:180
475e7b81acdc741f:62
fd87944d54b95762:80
d0a4144ce8350b0f:39
d99d1fe88438b865:94
753e7c35160df02f:11
5c62825e4a08b0ab:65
d75f6193b169cb0e:115
6ce3b5f59e76b366:41
36852f63e23ddca2:68
aa33e1d1c40210f5:22
56769e5bc1271b06:3
666003d52605b00c:55
737db166c79ae98e:3
46686ee1332c3253:5
68b782f858b3890b:13
82ca3cdcafee84be:11
ec51537469c275c4:102
2799bbd8bbb6429f:115
1d548d972e215972:66
def5a45dea3b11b2:77
a63e19c082fdf44a:153
44858463e3f288a2:98
f847ad84da6616b4:68
e3b0c44298fc1c14:0
a58a3fd698eba09b:138
c1d140dfe2d5fe30:1083
dda5363d1a32c17e:427
a3d4aaa2b184140d:608
bf43295cf15f0ace:477
27722de15c2b20fa:42
e3e9ab424430c000:15
0fb240b661991c98:5
1538af69284d4d90:34
3e55415503de7068:134
a9b503f920acffb3:59
faa5e199597457f4:135
42b295caca69df6a:76
3dd75320763b8d89:67
157e294358871e69:46
aa114fd3a5fc63c8:180
475e7b81acdc741f:62
7d456aab7d79be2a:136
a5e2e607daa2c6cc:81
c385ec64d9058bf6:95
d601d87c8f428327:83
42a06910643f14d8:411
c92709007c537ae1:144
41c37252f7940861:217
753e7c35160df02f:11
5c62825e4a08b0ab:65
693ecbacd2808d48:56
2cbfb1066c831e77:71
a60c554cba0b39de:86
c4d4751d0975a8ba:91
302fd239ec8020b1:72
35a7f0d4a864497c:85
56769e5bc1271b06:3
666003d52605b00c:55
737db166c79ae98e:3
46686ee1332c3253:5
7612e13c3ea47350:1425
f313cc66db55988b:78
21d639f4c1453d49:169
b678e4c2f95f02de:670
8f7417d732125852:121
b476854b288d8b65:398
944dec7effe2cce9:40
12d95005280e130c:138
e29805315b88a403:293
a75723cd8590fc25:226
55679af5d6f73a10:122
fabc4738f4fa301d:167
71f4e6a124943144:209
2c386e9f4d3d63f4:140
86baaa10868c9224:200
fe4ca4d2f86127e0:110
e3b0c44298fc1c14:0
db0cc51dc951ad27:157
9e55b17d180d1b88:1739
80cc7f2ed4637be8:857
3cbb120d756b2820:844
c5f7f5d6097d9925:1452
987050f53733ae97:307
4cfac803731fc963:367
cb139aa61e6f3004:326
fec2779423254929:160
158179471b31bbef:582
b9f8d820089a142f:205
2143fa2c8195ef4c:42
e3e9ab424430c000:15
0fb240b661991c98:5
3d6e1df581546ebb:34
3e55415503de7068:134
a9b503f920acffb3:59
faa5e199597457f4:135
42b295caca69df6a:76
3dd75320763b8d89:67
157e294358871e69:46
aa114fd3a5fc63c8:180
475e7b81acdc741f:62
7d456aab7d79be2a:136
22e3a5e40a99d9de:37
c385ec64d9058bf6:95
c0f27228050a69f2:471
d601d87c8f428327:83
dd7d98dfa1563087:515
ecd30f14285829c0:89
7209b8b55a62c360:88
332a95e2d4f08cce:203
3db178633c537693:72
753e7c35160df02f:11
5c62825e4a08b0ab:65
693ecbacd2808d48:56
2cbfb1066c831e77:71
57c2cf89a4abdabd:84
ffbc6aaa34a799eb:112
125180b9aa457e81:33
43604d6d018bc781:67
bdbd8ac54173072d:35
8495f9d111ccfbad:134
35a7f0d4a864497c:85
56769e5bc1271b06:3
666003d52605b00c:55
737db166c79ae98e:3
46686ee1332c3253:5
f356ad4aaf5690d5:244
b771a1d1f68dc956:49
22a3b839ba8b3ea6:251
9f068945d663e51a:340
bf2c7ec6f7eed9cb:142
7d9caffcffa2454f:88
e2f7371e7147aaea:268
8b2d98109c273573:151
cc10de6d08fb513a:43
4810b1e132f65cdf:255
ea658c569f3527df:117
289708c0983c1667:117
e0f64600e167136c:229
6aa6078ab4a845fd:937
25c183dde0308c7d:212
74852ccaacaf1c56:146
e1dbd8fe4cae34a7:203
f31b0036d8db25d8:226
c5ef221b2d4fb04b:95
47f8483dca414038:220
92e7b2e0bb27da8b:643
6bba7bfa48eaeb02:366
0a7d7c1a00169e25:353
e3b0c44298fc1c14:0
cb3f91d54eee30e5:3
e3b0c44298fc1c14:0
"""
LOCK = tuple(LOCK_BLOB.split())

# 범주 수 — 추출기가 죽으면(면제 목록이 비면 foreach가 아무것도 안 재고 초록이 되는 그 병)
# 0이 되어 rc=2로 떨어진다. 값이 바뀌면 rc=3(§X가 실제로 바뀐 것이니 의도적으로 재생성해야 한다).
COUNTS = {
    "blank": 9,
    "block_inner": 155,
    "bullets": 36,
    "fence_close": 6,
    "fence_open": 6,
    "fence_unclosed": 0,
    "items": 6,
    "judgement": 25,
    "lines": 301,
    "quote": 10,
    "table": 19,
}

# 렌더 결함 수 — 0이어야 하는 것 둘과, 지금 값을 그대로 박는 것 하나.
RENDER = {
    "bold_flank": 1,
}

MIN_NONZERO = (
    "items", "fence_open", "fence_close", "block_inner", "judgement", "bullets", "quote",
)


def repo_path(rel):
    return os.path.join(REPO, rel)


def fail(rc, msg):
    # ★ 인쇄 직전에 가린다 — 가드가 자기 소스만 보던 구멍(출력으로 새는 길)을 여기서 닫는다.
    print(scrub(msg))
    sys.exit(rc)


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:HASH_CHARS]


def mask_code_spans(line):
    """백틱 코드 스팬 안을 **같은 길이의 'x'**로 바꾼다.

    ★ 공백으로 비우면 자기 취소선·굵게 판정을 스스로 깨뜨린다(이 저장소의 실제 사고).
    같은 길이 'x'는 `~`·`*`만 없애고 길이·자리를 보존하므로 그 사고를 되풀이하지 않는다.
    """
    out = []
    i = 0
    n = len(line)
    while i < n:
        if line[i] == "`":
            j = line.find("`", i + 1)
            if j < 0:
                out.append(line[i])
                i += 1
                continue
            out.append("`" + "x" * (j - i - 1) + "`")
            i = j + 1
        else:
            out.append(line[i])
            i += 1
    return "".join(out)


def is_korean(ch):
    return "HANGUL" in unicodedata.name(ch, "")


FLANK_PUNCT = ")]}」』”’.·,:;!?%…"


def render_stats(lines):
    """렌더 결함 셋을 센다. odd_bold·stray_tilde는 0이어야 하고, bold_flank는 지금 값을 잠근다."""
    odd_bold, stray_tilde, bold_flank = [], [], []
    for idx, line in enumerate(lines):
        m = mask_code_spans(line)
        if m.count("**") % 2 == 1:
            odd_bold.append(idx)
        if m.replace("~~", "").count("~") % 2 == 1:
            stray_tilde.append(idx)
        # ★ 2026-09-26 규칙 정정(리더): 바른 규칙은 **「닫는 `**` 바로 앞이 구두점이고 바로 뒤가 한글일 때」**다.
        #   첫 판은 여는 `**`까지 같이 셌다 — `(**굵게**…` 같은 **정상 여는 자리**가 거짓 빨강이 된다.
        #   그래서 `**` 런을 순서대로 세어 **홀수 번째(여는 자리)는 건너뛰고 짝수 번째(닫는 자리)만** 본다.
        for order, hit in enumerate(re.finditer(r"\*\*", m)):
            if order % 2 == 0:
                continue  # 여는 자리
            end = hit.end()
            before = m[hit.start() - 1] if hit.start() > 0 else ""
            after = m[end] if end < len(m) else ""
            # ★★ 뿌리 함정(2026-09-26 · security 적발) — Python에서 `"" in <문자열>`은 **언제나 참**이다.
            #    `before`를 빈 값으로 두고 `before in FLANK_PUNCT`만 보면 **줄머리 `**`**(앞 글자가 없다)가
            #    「앞이 구두점」으로 집계된다. 위 런 패리티가 run0을 건너뛰어 **지금은 도달 불가**였지만
            #    (줄머리 두 사례로 실측 확인), 패리티 가정에 기대지 않게 뿌리에서 막는다. 교정 C11이 못박는다.
            if not before:
                continue
            if before in FLANK_PUNCT and after and is_korean(after):
                bold_flank.append(idx)
                break
    return {"odd_bold": odd_bold, "stray_tilde": stray_tilde, "bold_flank": bold_flank}


def extract_section(text):
    """§X 절을 뽑는다. 못 뽑으면 (None, 사유)."""
    lines = text.split("\n")
    start = None
    for i, line in enumerate(lines):
        if line.startswith(SECTION_HEAD):
            if start is not None:
                return None, "§X 제목이 두 번 나온다 — 추출기가 어느 쪽인지 모른다"
            start = i
    if start is None:
        return None, "§X 제목을 찾지 못했다(`%s`로 시작하는 줄 0)" % SECTION_HEAD
    end = None
    for i in range(start + 1, len(lines)):
        if lines[i].startswith("## "):
            end = i
            break
    if end is None:
        return None, "§X 다음 절 제목(`## `)을 찾지 못했다"
    return lines[start:end], None


def categorize(lines):
    counts = {
        "lines": len(lines),
        "blank": sum(1 for l in lines if l.strip() == ""),
        "items": sum(1 for l in lines if re.match(r"^- \[ \] \*\*X-\d", l)),
        "fence_open": sum(1 for l in lines if l.strip() == "```powershell"),
        "fence_close": 0,
        "block_inner": 0,
        "judgement": sum(1 for l in lines if re.match(r"^\s*\d+\. ", l)),
        "bullets": sum(1 for l in lines if re.match(r"^\s*- ", l)),
        "quote": sum(1 for l in lines if l.startswith("> ")),
        "table": sum(1 for l in lines if l.strip().startswith("|")),
    }
    inside = False
    for l in lines:
        s = l.strip()
        if s.startswith("```"):
            if inside:
                counts["fence_close"] += 1
            inside = not inside
            continue
        if inside:
            counts["block_inner"] += 1
    counts["fence_unclosed"] = 1 if inside else 0
    return counts


def build_lock(lines):
    return tuple("%s:%d" % (sha(l), len(l)) for l in lines)


def verdict(lines, lock, counts_locked, render_locked):
    """(rc, 메시지 목록). 순수 함수라 교정(selftest)이 변이 입력으로 그대로 부른다."""
    msgs = []
    counts = categorize(lines)

    # --- 판정 불가: 추출기가 죽었다 / 잠금표가 비었다 ---
    if not lock:
        return 2, ["✗ 잠금표(LOCK)가 비어 있다 — `--print`로 생성해 이 파일에 박아라"
                   "(빈 목록으로 도는 검사기는 아무것도 재지 않고 초록이 된다)"]
    if not counts_locked:
        return 2, ["✗ 범주 수(COUNTS)가 비어 있다 — `--print`로 생성해 박아라"]
    if counts["fence_unclosed"]:
        return 2, ["✗ PowerShell 블록 울타리가 닫히지 않았다 — 추출기가 블록 내부를 잘못 센다"]
    for key in MIN_NONZERO:
        if counts.get(key, 0) == 0:
            return 2, ["✗ 범주 `%s`가 0이다 — 추출기가 죽었다(0인 범주로는 아무것도 못 잰다)" % key]

    rc = 0

    # --- 빨강: 자리별 해시 ---
    if len(lines) != len(lock):
        rc = 3
        msgs.append("✗ 줄 수가 다르다: 지금 %d · 잠금 %d (줄이 %s)"
                    % (len(lines), len(lock), "늘었다" if len(lines) > len(lock) else "사라졌다"))
    now = build_lock(lines)
    bad = [i for i in range(min(len(now), len(lock))) if now[i] != lock[i]]
    if bad:
        rc = 3
        msgs.append("✗ 잠긴 줄이 바뀌었다: %d줄 (§X 안 상대 줄번호 %s%s)"
                    % (len(bad), ", ".join(str(b + 1) for b in bad[:8]),
                       " …" if len(bad) > 8 else ""))
        for b in bad[:3]:
            was_len = int(lock[b].split(":")[1])
            # ★ 원문을 그대로 찍지 않는다 — 길이·해시가 진단의 본체이고, 발췌는 짧게·가려서 낸다.
            msgs.append("    - 상대 %d: 길이 %d → %d · 해시 %s → %s · 발췌(가림): %s"
                        % (b + 1, was_len, len(lines[b]), lock[b].split(":")[0],
                           sha(lines[b]), scrub(lines[b][:40])))

    # --- 빨강: 범주 수 변화(전 줄 잠금과 이중으로 문다) ---
    for key, want in sorted(counts_locked.items()):
        got = counts.get(key)
        if got != want:
            rc = 3
            msgs.append("✗ 범주 `%s`: 지금 %s · 잠금 %s" % (key, got, want))

    # --- 빨강: 렌더 결함 ---
    stats = render_stats(lines)
    for key in ("odd_bold", "stray_tilde"):
        if stats[key]:
            rc = 3
            msgs.append("✗ 렌더 결함 `%s` %d줄 (상대 줄번호 %s) — 0이어야 한다"
                        % (key, len(stats[key]), ", ".join(str(i + 1) for i in stats[key][:8])))
    want_flank = render_locked.get("bold_flank")
    if want_flank is not None and len(stats["bold_flank"]) != want_flank:
        rc = 3
        msgs.append("✗ 굵게 플랭킹 의심 줄: 지금 %d · 잠금 %d (구두점 뒤 `**` + 바로 한글)"
                    % (len(stats["bold_flank"]), want_flank))

    if rc == 0:
        msgs.append("✓ §X %d줄 전부 잠금과 같다(블록 내부 %d · 판독 %d · 불릿 %d · 표 %d · 공통 %d)"
                    % (counts["lines"], counts["block_inner"], counts["judgement"],
                       counts["bullets"], counts["table"], counts["quote"]))
    return rc, msgs


# ----------------------------------------------------------------------------
# 교정(변이 대조) — 하나라도 「변이가 살아남았다」면 rc=2. 검사기가 무는지를 검사기 자신이 잰다.
# ----------------------------------------------------------------------------
def longest_unique_line(lines):
    """가장 길고 **문서 안에서 유일한** 줄의 인덱스. 짧은 줄·빈 줄로 시험하면 대조가 죽는다."""
    order = sorted(range(len(lines)), key=lambda i: -len(lines[i]))
    for i in order:
        if lines[i].strip() and lines.count(lines[i]) == 1:
            return i
    return None


def selftest(lines, lock, counts_locked, render_locked):
    results = []

    def run(name, mutated, want_rc, must_differ=True):
        if must_differ and mutated == lines:
            results.append((name, "죽은 변이 — 입력이 바뀌지 않았다", False))
            return
        rc, _ = verdict(mutated, lock, counts_locked, render_locked)
        results.append((name, "rc=%d(기대 %d)" % (rc, want_rc), rc == want_rc))

    base_rc, _ = verdict(lines, lock, counts_locked, render_locked)
    results.append(("C0 양성 대조(무변이)", "rc=%d(기대 0)" % base_rc, base_rc == 0))

    idx = longest_unique_line(lines)
    if idx is None:
        return [("C-추출", "길고 유일한 줄을 못 찾았다", False)]

    # C1 길고 유일한 줄의 1글자 변이
    m = list(lines)
    pos = len(m[idx]) // 2
    m[idx] = m[idx][:pos] + ("X" if m[idx][pos] != "X" else "Y") + m[idx][pos + 1:]
    run("C1 긴 줄 1글자 변이", m, 3)

    # C2 길고 유일한 줄 **삭제**
    m = list(lines)
    del m[idx]
    run("C2 긴 줄 삭제", m, 3)

    # C3 같은 줄 삽입(중복)
    m = list(lines)
    m.insert(idx, lines[idx])
    run("C3 줄 삽입", m, 3)

    # C4 블록 **내부** 줄 변이
    inside, bidx = False, None
    for i, l in enumerate(lines):
        if l.strip().startswith("```"):
            inside = not inside
            continue
        if inside and l.strip():
            bidx = i
            break
    if bidx is None:
        results.append(("C4 블록 내부 변이", "블록 내부 줄을 못 찾았다", False))
    else:
        m = list(lines)
        m[bidx] = m[bidx] + " "
        run("C4 블록 내부 줄 변이(공백 1개)", m, 3)

    # C5 판독 줄 **극성 뒤집기** — 2026-09-15판이 rc 0으로 샌 바로 그 형태
    jidx = None
    for i, l in enumerate(lines):
        if re.match(r"^\s*\d+\. ", l) and "「아니오」" in l:
            jidx = i
            break
    if jidx is None:
        results.append(("C5 판독 극성 뒤집기", "「아니오」가 있는 판독 줄을 못 찾았다", False))
    else:
        m = list(lines)
        m[jidx] = m[jidx].replace("「아니오」", "「예」", 1)
        run("C5 판독 극성 뒤집기", m, 3)

    # C6 **빈 줄** 삭제 — 짝짓기 검사기가 구조적으로 못 보는 형태
    eidx = next((i for i, l in enumerate(lines) if l.strip() == ""), None)
    if eidx is None:
        results.append(("C6 빈 줄 삭제", "빈 줄이 없다", False))
    else:
        m = list(lines)
        del m[eidx]
        run("C6 빈 줄 삭제", m, 3)

    # C7 렌더 결함 주입(홑물결표 1개) — 렌더 가드가 무는지
    m = list(lines)
    m[idx] = m[idx] + " ~"
    run("C7 홑물결표 주입", m, 3)

    # C8 추출기 죽음 모사 — 블록 울타리를 없애면 범주 0 → rc 2
    m = [l for l in lines if not l.strip().startswith("```")]
    run("C8 블록 울타리 제거(추출기 죽음)", m, 2)

    # C9 잠금표 비움 — 빈 목록으로 초록이 되지 않는지
    rc, _ = verdict(lines, (), counts_locked, render_locked)
    results.append(("C9 잠금표 비움", "rc=%d(기대 2)" % rc, rc == 2))

    # C10 범주 수만 어긋남 — 전 줄 잠금과 별개로 무는지
    bogus = dict(counts_locked)
    bogus["judgement"] = bogus.get("judgement", 0) + 1
    rc, _ = verdict(lines, lock, bogus, render_locked)
    results.append(("C10 범주 수 불일치", "rc=%d(기대 3)" % rc, rc == 3))

    # C11 굵게 플랭킹 판정기 고정 대조 — 줄머리 굵게는 **안 잡히고** 진짜 결함 세 형태는 **잡혀야** 한다.
    #   이 대조가 없으면 「빈 문자열 함정」이 조용히 돌아와도 §X에 줄머리 굵게가 없는 동안은 아무도 모른다.
    flank_cases = (
        ("줄머리 `**요지(핵심)**:`", "**요지(핵심)**: 정상이다.", False),
        ("줄머리 `**중요**한`", "**중요**한 것은 이것이다.", False),
        ("정상 닫기(뒤 공백)", "**굵게** 뒤에 공백이 온다.", False),
        ("코드 스팬 안", "`**가짜(x)**이` 는 코드다.", False),
        ("진짜 결함 `%`", "**점유율 80%**이고 나머지는", True),
        ("진짜 결함 `)`", "**소속 줄 0 → 1번(판정 불가)**이 되고", True),
        ("진짜 결함 `」`", "**「홈」**으로 돌아간다", True),
    )
    flank_bad = [name for name, line, want in flank_cases
                 if (0 in render_stats([line])["bold_flank"]) != want]
    results.append(("C11 굵게 플랭킹 대조 7종",
                    "어긋남 %d%s" % (len(flank_bad), (" — " + ", ".join(flank_bad)) if flank_bad else ""),
                    not flank_bad))
    # C11-b 함정이 **실재**한다는 것 자체를 남긴다(그래서 위 가드가 필요하다).
    results.append(("C11-b 빈 문자열 함정 실재", "'' in FLANK_PUNCT = %s" % ("" in FLANK_PUNCT),
                    ("" in FLANK_PUNCT) is True))

    return results


def _frag(*parts):
    """니들을 **조각으로 조립**한다 — 온전한 글자로 적으면 이 파일 자신이 걸려 가드가 **늘** 울린다.
    실제로 그렇게 짰다가 첫 실행에서 rc=2가 났고 「빈 잠금표 가드가 물었다」로 오독할 뻔했다."""
    return "".join(parts)


# ----------------------------------------------------------------------------
# 누출 탐지기 (2026-09-26 확장 — security 전수 실측 인계)
#   ★ **구조적 한계 자백**: 이름 니들의 원천은 이 머신의 `id -un`·호스트명이다. 그런데 §X가 모으는 것은
#     **사용자 PC의 Windows 계정 이름**이라 두 값은 절대 같지 않다 ⇒ 이름 대조만으로는 이 절차가
#     걱정하는 바로 그 이름을 **원리상 못 잡는다**. 그래서 이름이 아니라 **형태**를 잡는 아래 정규식이 주력이고,
#     이름 대조는 보조로만 남긴다.
#   ★ 환경변수 자리표시자(`$env:USERPROFILE\...`)는 실제 이름이 아니므로 잡지 않는다 — §X 블록이 그 형태를 쓴다.
# ----------------------------------------------------------------------------
_USERS = _frag("Us", "ers")
_IDENT_PATTERNS = (
    ("유닉스 홈 경로", re.compile(r"/" + _USERS + r"/[^/\s`\"'<>%$]+")),
    ("리눅스 홈 경로", re.compile(r"/" + _frag("ho", "me") + r"/[^/\s`\"'<>%$]+")),
    ("윈도 사용자 경로", re.compile(r"[A-Za-z]:\\" + _USERS + r"\\[^\\\s`\"'<>%$]+")),
    ("UNC 공유", re.compile(r"\\\\[A-Za-z0-9._-]{2,}\\[A-Za-z0-9._$-]+")),
    ("드라이브 절대경로", re.compile(r"(?<![A-Za-z0-9$%:])[D-Zd-z]:\\[A-Za-z0-9]")),
    ("IPv4", re.compile(r"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])")),
    ("이메일", re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")),
    ("윈도 SID", re.compile(r"S-1-5-21-\d")),
    ("GUID", re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-"
                        r"[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")),
)
_LITERAL_NEEDLES = (
    _frag("scratch", "pad"), _frag("/priv", "ate/tmp/"), _frag("/va", "r/folders/"),
    _frag("/Vol", "umes/"),
)


def find_identifiers(text):
    """(이름, 적중 수) 목록. **값은 돌려주지 않는다** — 호출부가 값을 찍을 수 없게."""
    hits = []
    for label, pat in _IDENT_PATTERNS:
        n = len(pat.findall(text))
        if n:
            hits.append((label, n))
    for needle in _LITERAL_NEEDLES:
        n = text.count(needle)
        if n:
            hits.append(("임시 폴더 조각", n))
    return hits


def scrub(text):
    """인쇄 직전에 한 번 더 건다 — 가드가 자기 소스만 보던 구멍을 닫는다.

    ★ 사고 형태: 검사기가 rc 3을 내면서 **문서 원문 60자**를 그대로 인쇄했고, 그 안에 가짜
    Windows 경로를 심었더니 가드는 울리지 않고 경로만 출력됐다(security 실증). 출력 경로에도
    같은 니들을 건다.
    """
    out = text
    for label, pat in _IDENT_PATTERNS:
        out = pat.sub("<가림:%s>" % label, out)
    for needle in _LITERAL_NEEDLES:
        out = out.replace(needle, "<가림:임시 폴더>")
    for value in _runtime_names():
        if value:
            out = out.replace(value, "<가림:실행 계정>")
    return out


def _runtime_names():
    names = []
    for cmd in (["id", "-un"], ["hostname"]):
        try:
            v = subprocess.run(cmd, capture_output=True, text=True, timeout=5).stdout.strip()
        except Exception:
            v = ""
        # 길이 하한·면제 목록을 두지 않는다 — 그 둘이 그대로 구멍이었다(security 지적).
        if v:
            names.append(v)
    return names


def leak_guard():
    """이 파일에 절대 경로·식별자가 박혀 있지 않은지. 값은 **출력하지 않는다**."""
    try:
        with open(repo_path(SELF_REL), encoding="utf-8") as fh:
            src = fh.read()
    except OSError as exc:
        return "✗ 자기 소스를 읽지 못했다(%s)" % exc.__class__.__name__
    hits = find_identifiers(src)
    if hits:
        return "✗ 누출 가드: 이 파일에 식별자 형태가 있다(%s · 값은 찍지 않는다)" % \
               ", ".join("%s %d건" % h for h in hits)
    for value in _runtime_names():
        if value and value in src:
            return "✗ 누출 가드: 이 파일에 실행 계정·호스트 이름이 들어 있다(값은 찍지 않는다)"
    return None


def main():
    args = sys.argv[1:]
    mode = args[0] if args else ""
    if mode not in ("", "--selftest", "--print"):
        fail(2, "✗ 모르는 인자: %s (없음 | --selftest | --print)" % mode)

    leak = leak_guard()
    if leak:
        fail(2, leak)

    doc = repo_path(DOC_REL)
    if not os.path.isfile(doc):
        fail(2, "✗ 문서가 없다: %s" % DOC_REL)
    with open(doc, encoding="utf-8") as fh:
        text = fh.read()
    lines, why = extract_section(text)
    if lines is None:
        fail(2, "✗ %s" % why)

    # ★ 2026-09-26 — 니들을 **문서 본문에도** 건다. 「§X에 사용자 경로·계정·IP가 섞였다」를 rc 2로 떨어뜨려,
    #   공개 저장소에 그 값이 실리는 사고를 구조적으로 막는다(환경변수 자리표시자는 잡지 않는다).
    doc_hits = find_identifiers("\n".join(lines))
    if doc_hits:
        fail(2, "✗ §X 본문에 식별자 형태가 있다(%s · 값은 찍지 않는다) — 자리표시자로 바꾸고 다시 돌려라"
             % ", ".join("%s %d건" % h for h in doc_hits))

    if mode == "--print":
        counts = categorize(lines)
        stats = render_stats(lines)
        print("# --- 아래를 이 파일의 LOCK_BLOB / COUNTS / RENDER에 그대로 박는다 ---")
        print('LOCK_BLOB = """\\')
        for item in build_lock(lines):
            print(item)
        print('"""')
        print("LOCK = tuple(LOCK_BLOB.split())")
        print("COUNTS = {")
        for k in sorted(counts):
            print('    "%s": %d,' % (k, counts[k]))
        print("}")
        print("RENDER = {")
        print('    "bold_flank": %d,' % len(stats["bold_flank"]))
        print("}")
        print("# odd_bold=%d stray_tilde=%d (둘 다 0이어야 한다)"
              % (len(stats["odd_bold"]), len(stats["stray_tilde"])))
        sys.exit(0)

    if mode == "--selftest":
        results = selftest(lines, LOCK, COUNTS, RENDER)
        for name, detail, ok in results:
            print("%s %s — %s" % ("✓" if ok else "✗", name, detail))
        bad = [r for r in results if not r[2]]
        if bad:
            fail(2, "✗ 교정 %d건 실패 — 검사기를 믿을 수 없다(판정 숫자를 인쇄하지 않는다)" % len(bad))
        print("✓ 교정 %d건 전부 통과 · 생존 변이 0" % len(results))
        sys.exit(0)

    rc, msgs = verdict(lines, LOCK, COUNTS, RENDER)
    for m in msgs:
        print(scrub(m))
    if rc == 3:
        print("→ §X를 의도적으로 고쳤다면 `--print`로 잠금표를 다시 생성해 박고, "
              "무엇이 새로 덮였는지 보고에 적는다.")
    sys.exit(rc)


if __name__ == "__main__":
    main()
