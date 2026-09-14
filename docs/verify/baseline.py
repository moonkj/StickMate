#!/usr/bin/env python3
# =============================================================================
# docs/verify/BASELINE.md 생성기 — qa-regression 전용 (2026-09-02 신설)
#
#   python3 docs/verify/baseline.py            # BASELINE.md 재생성
#   python3 docs/verify/baseline.py --check     # 자기검사(양성/음성 대조)만
#
# 왜 생겼나 — 리더가 19:07 빌드로 활성 타깃을 OSX로 바꿔 놓고 그 뒤로도 계속
# 「WIN이다」라고 알렸다. 원인은 단순하다: **실행마다 타깃을 적어 두는 칸이 없었다.**
# 기억으로 말하면 기억이 틀린 그 순간부터 아무도 못 잡는다.
#
# ★ 이 생성기의 유일한 규율: **잰 값과 추론한 값을 다르게 생기게 만든다.**
#   - 잰 값  = 실행 시각에 regress.sh가 남긴 사이드카(.meta). 표에 그냥 적는다.
#   - 추론값 = 로그의 Bee dag 해시로 되살린 값. 앞에 `~`를 붙인다.
#   - 물려받은 값 = 그 실행이 재컴파일을 안 해 직전 실행에서 상속. `↑`를 붙인다.
#   - 모르는 값 = **비우지 않고 `미상`이라고 쓴다.** 빈 칸은 "WIN"으로 읽히더라.
# =============================================================================
import os, sys, glob, re, subprocess, datetime
import xml.etree.ElementTree as ET

# ★ 개명 흡수(2026-09-03). 이름이 바뀐 테스트를 «없던 테스트»로 취급하면
#   「마지막으로 초록이던 실행」이 **한 번도 없다**로 뒤집힌다 — 귀속이 통째로 거짓말이 된다.
#   규칙은 소스 트리로 재검증된 것만 쓴다(docs/verify/renames.py 참고).
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
try:
    import renames as _rn
    _CANON_FULL, _CANON_SHORT, _RN_OK, _RN_REJ = _rn.load()
except Exception as _e:                      # 대장이 깨져도 대장 생성은 돌아야 한다
    _CANON_FULL = _CANON_SHORT = (lambda n: n)
    _RN_OK, _RN_REJ = [], [(0, f"renames.py 로드 실패: {_e}")]

# ★ 2026-09-14 — 초록/빨강 판정은 규칙 1(docs/verify/nunit_verdict.py)로 한다. 이 import가 깨지면
#   대장을 만들지 않는다 — 옛 판정(test-case Failed만)으로 조용히 물러나면 픽스처 끝 실패가 다시 안 보인다.
import nunit_verdict as NV

# ★ 2026-09-14 — 저장소 루트는 이 파일 위치(docs/verify/)에서 구한다(사용자명이 든 절대 경로를 박지 않는다).
REPO   = os.path.dirname(os.path.dirname(os.path.dirname(os.path.realpath(__file__))))
if not os.path.isfile(os.path.join(REPO, "docs", "verify", "baseline.py")):
    raise SystemExit(f"✗ 저장소 루트를 찾지 못했다 — REPO={REPO}")
OUTDIR = os.path.join(REPO, "docs/verify/runs")
OUTMD  = os.path.join(REPO, "docs/verify/BASELINE.md")
BEE    = os.path.join(REPO, "Library/Bee/artifacts")


def dag_target_map():
    """Bee dag 해시 -> UNITY_STANDALONE_*  (파일 mtime이 아니라 **내용**에서 읽는다)"""
    m = {}
    for rsp in glob.glob(os.path.join(BEE, "*.dag", "StickMate.Runtime.rsp")):
        h = os.path.basename(os.path.dirname(rsp))          # 예: 200b0aE.dag
        try:
            txt = open(rsp, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        t = sorted(set(re.findall(r"UNITY_STANDALONE_[A-Z]+", txt)))
        if len(t) == 1:
            m[h] = t[0]
    return m


def reflog_commits():
    """[(epoch, shorthash)] 최신순. HEAD 되살리기용."""
    try:
        out = subprocess.run(
            ["git", "-C", REPO, "reflog", "--date=unix",
             "--format=%h %gd"], capture_output=True, text=True, timeout=20).stdout
    except Exception:
        return []
    res = []
    for line in out.splitlines():
        mm = re.match(r"^(\S+)\s+HEAD@\{(\d+)\}", line)
        if mm:
            res.append((int(mm.group(2)), mm.group(1)))
    return res


def head_at(epoch, commits):
    for ts, h in commits:          # reflog는 최신순
        if ts <= epoch:
            return h
    return None


def read_meta(base, outdir=None):
    """regress.sh가 실행 시각에 남긴 사이드카. 있으면 이것이 사실이다."""
    p = os.path.join(outdir or OUTDIR, base + ".meta")
    if not os.path.isfile(p):
        return {}
    d = {}
    for line in open(p, encoding="utf-8", errors="replace"):
        if "=" in line:
            k, v = line.rstrip("\n").split("=", 1)
            d[k.strip()] = v.strip()
    return d


def log_dag(base, outdir=None):
    p = os.path.join(outdir or OUTDIR, base + ".log")
    if not os.path.isfile(p):
        return None
    try:
        with open(p, encoding="utf-8", errors="replace") as f:
            hits = set(re.findall(r"artifacts/([0-9a-zA-Z]+\.dag)", f.read()))
    except OSError:
        return None
    hits = {h for h in hits if h.endswith("E.dag")}     # 에디터 어셈블리
    return sorted(hits)[0] if len(hits) == 1 else None


FIXTURE_KEY = "★픽스처 "          # 픽스처 수준 실패를 «실패 이름»과 같은 칸에서 추적하기 위한 접두어
RUNRESULT_KEY = "★test-run result="

# =============================================================================
# ★ 2026-09-14 — 「현재」 = 가장 최근 **전량** 실행 (리더 지시, qa-regression 구현)
# =============================================================================
# 사고: 대장의 「지금 빨간 것」이 mtime 최신 실행을 «현재»로 삼았다. 09-14 최신 play는
#   `coder-freeze-related`(62건 부분 실행)였고 「빨강 없음」이 찍혔다 — 같은 날 전량 783건(실패 5 ·
#   판정 불가 2)은 `Logs/coder-onbstore/`에 떨어져 **대장에 아예 안 들어왔다.**
#
# 전량/부분은 **기록된 사실로만** 가른다(testcasecount 크기로 추정하지 않는다 — 테스트 수는 계속 는다):
#   (1) Unity 로그의 `COMMAND LINE ARGUMENTS:` 블록 — ★ **한 줄에 인자 하나**다(한 줄로 읽으면 전부 죽는다,
#       2026-09-14 실측: 한 줄로 파싱한 프로브가 848건 전부 «테스트 실행 아님»을 냈다).
#       그 블록의 `-testResults`가 **이 xml 경로**여야 그 로그를 이 실행의 기록으로 인정한다(짝 확인).
#       필터 인자(SCOPE_FILTER_ARGS)가 하나라도 있으면 부분, 없으면 전량.
#   (2) 로그가 없을 때만 — `regress.sh`의 `.meta` 사이드카(키 집합이 regress.sh의 것이고 라벨·모드가 맞을 때).
#       regress.sh는 필터 인자를 넘기지 않는다(git 이력 6커밋 전부 확인, 2026-09-14).
#   (3) 둘 다 없으면 「미확인」 — 「현재」 후보가 아니다.
#   ★ 모순 검사(추정이 아니라 **같은 xml 안의 기록끼리** 대조): 필터가 없다는데 xml 뿌리 스위트의
#     발견 수(testcasecount)가 실행 수(total)보다 크면 「미확인(모순)」. 2026-09-14 실측 모순 0건.
#
# 수집 범위(리더 질의 3 — 추천안 「수집 확장」):
#   - `docs/verify/runs/*_edit.xml`·`*_play.xml` — 옛 대장 그대로 **전부**(부분·미확인도 칸으로 보인다)
#   - 그 밖(`runs/`의 다른 xml, `Logs/**`) — **전량이 기록으로 확인된 것만.** 부분·미확인은 표에 넣지 않고 개수만 적는다
#   - 같은 결과의 사본은 **xml 바이트 sha256**으로 한 줄만 남긴다(`runs/` 쪽이 이긴다 — 사이드카가 거기 있다)
#   기각한 안 「전량 결과는 runs/로 복사·라벨」: 사람이 기억해야 도는 규칙이다. 6일 17시간 공백이 정확히
#   «각 라운드가 자기 자리에 결과를 두고 잊은 것»이었다.
import hashlib
import collections

LOGS_ROOT = os.path.join(REPO, "Logs")
# ★ 범위 판정의 사실(로그 명령줄 · regress.sh 사이드카)은 **docs/verify/nunit_verdict.py 한 곳**에 있다.
#   regress.sh report의 「부분 실행」 배너와 이 대장의 「현재」가 **같은 판정**을 쓴다(리더 지시 2026-09-14).
SCOPE_FILTER_ARGS = NV.SCOPE_FILTER_ARGS
REGRESS_META_KEYS = NV.REGRESS_META_KEYS
unity_cmdline = NV.unity_cmdline
xml_platform = NV.xml_platform
run_scope = NV.run_scope


def run_start_epoch(root):
    s = (root.get("start-time") or "").strip()
    try:
        return datetime.datetime.strptime(s, "%Y-%m-%d %H:%M:%SZ") \
            .replace(tzinfo=datetime.timezone.utc).timestamp()
    except ValueError:
        return None


def collect(outdir=None, extra_roots=None):
    outdir = os.path.abspath(outdir or OUTDIR)
    if extra_roots is None:                      # 실제 대장일 때만 Logs/ 를 본다(--check 임시 폴더는 안 본다)
        extra_roots = [LOGS_ROOT] if outdir == os.path.abspath(OUTDIR) else []
    stats = collections.Counter()
    seen = {}

    primaries = sorted(glob.glob(os.path.join(outdir, "*_edit.xml")) +
                       glob.glob(os.path.join(outdir, "*_play.xml")))
    cands = [(x, True) for x in primaries]
    others = [x for x in glob.glob(os.path.join(outdir, "*.xml")) if x not in set(primaries)]
    for root_dir in extra_roots:
        others += glob.glob(os.path.join(root_dir, "**", "*.xml"), recursive=True)
    cands += [(x, False) for x in sorted(set(others))]

    dmap = dag_target_map()
    commits = reflog_commits()
    rows = []
    for xml, primary in cands:
        stem = os.path.splitext(xml)[0]
        logp = stem + ".log"
        if not primary:
            # 싸게 먼저 거른다 — 부분·미확인은 xml을 열지도 않는다.
            cmd = unity_cmdline(logp)
            if cmd is None:
                if not os.path.isfile(stem + ".meta"):
                    stats["미확인(로그 명령줄·사이드카 없음)"] += 1
                    continue
            elif any(a in SCOPE_FILTER_ARGS for a in cmd):
                stats["부분"] += 1
                continue
        try:
            with open(xml, "rb") as f:
                raw = f.read()
            r = ET.fromstring(raw)
        except Exception:
            if not primary:
                stats["읽기 실패"] += 1
            continue
        if r.tag != "test-run":
            continue
        h = hashlib.sha256(raw).hexdigest()
        base = os.path.basename(stem)
        if primary:
            label, mode = base.rsplit("_", 1)[0], base.rsplit("_", 1)[-1]
            meta = read_meta(base, outdir)
        else:
            mode = {"editmode": "edit", "playmode": "play"}.get((xml_platform(r) or "").lower())
            if mode is None:
                stats["모드 미상"] += 1
                continue
            label = os.path.relpath(stem, REPO) if os.path.abspath(xml).startswith(REPO + os.sep) else base
            meta = read_meta(base, os.path.dirname(xml))
        scope, scope_why = run_scope(xml, r, meta, logp, label if primary else None, mode if primary else None)
        if not primary:
            if scope != "전량":
                stats["미확인"] += 1
                continue
            if h in seen:
                stats["중복(해시)"] += 1
                continue
        seen.setdefault(h, xml)
        mt = os.stat(xml).st_mtime
        start = run_start_epoch(r)
        fails = sorted(_CANON_SHORT(tc.get("fullname").split(".")[-1])
                       for tc in r.iter("test-case") if tc.get("result") == "Failed")
        # ★ "그때는 초록이었다"와 "그때는 존재하지 않았다"는 완전히 다른 말이다.
        #   이름 집합을 함께 들고 다니지 않으면 대장이 없던 테스트를 '초록이었다'로 둔갑시킨다.
        present = {_CANON_SHORT(tc.get("fullname").split(".")[-1]) for tc in r.iter("test-case")}

        # ★ 2026-09-14 R1/R2/R5 (docs/TEAM.md 「픽스처 끝에서 난 실패는 실패 개수에 안 들어간다」).
        #   옛 대장은 test-case Failed만 셌다 → mut-M5p.xml(failed=0, SetUpFixture TearDown 실패)을
        #   「빨강 없음」으로 적었을 것이다. 판정은 nunit_verdict(규칙 1)로 하고,
        #   픽스처 실패도 «실패 이름» 칸에 넣어 「언제부터」 추적에 태운다.
        v = NV.judge_root(r)
        for f in v["fixture_failures"]:
            fails.append(f"{FIXTURE_KEY}{f['site']} {f['fullname']}")
        if (v["run_result"] or "") not in NV.GREEN_RUN_RESULTS and not fails:
            fails.append(f"{RUNRESULT_KEY}{v['run_result']}")   # 원인 미분류 빨강도 조용히 두지 않는다
        fails = sorted(set(fails))
        for s in r.iter("test-suite"):
            fn = s.get("fullname") or s.get("name")
            if fn:
                present.add(f"{FIXTURE_KEY}SetUp {fn}")
                present.add(f"{FIXTURE_KEY}TearDown {fn}")
        present.add(f"{RUNRESULT_KEY}{v['run_result']}")
        inconclusive = sorted(_CANON_SHORT(n.split(".")[-1]) for n, _ in v["inconclusive_cases"])
        # ★ 2026-09-14 — 「실제로 초록」은 **Passed인 실행만**이다. 옛 대장은 «빨강이 아니면 초록»이라
        #   Skipped/Ignored/Inconclusive를 초록으로 셌다(실측: 왕관 테스트 — 대장 «마지막 초록 part2-final»,
        #   그 실행에서는 건너뜀이었고 실제 마지막 Passed는 qa-r10 09-05).
        passed_names = {_CANON_SHORT(tc.get("fullname").split(".")[-1])
                        for tc in r.iter("test-case") if tc.get("result") == "Passed"}
        for s in r.iter("test-suite"):                      # 픽스처 이름: 그 스위트가 실패하지 않았으면 «통과»
            fn = s.get("fullname") or s.get("name")
            if fn and not (s.get("result") or "").startswith("Failed"):
                passed_names.add(f"{FIXTURE_KEY}SetUp {fn}")
                passed_names.add(f"{FIXTURE_KEY}TearDown {fn}")

        rows.append(dict(
            passed_names=passed_names,
            base=base, label=label, mode=mode, mt=mt,
            # ★ 순서·시각은 **xml에 기록된 실행 시작 시각**이 먼저다. mtime은 복사·재저장으로 바뀐다.
            t=start if start is not None else mt, tsrc_time="" if start is not None else "~",
            origin="runs" if primary else os.path.relpath(os.path.dirname(xml), REPO),
            scope=scope, scope_why=scope_why,
            tcc=int(r.get("testcasecount") or 0), total=int(r.get("total") or 0),
            passed=int(r.get("passed") or 0), failed=int(r.get("failed") or 0),
            skipped=int(r.get("skipped") or 0),
            fails=fails, present=present,
            green=v["green"], reasons=v["reasons"], inconclusive=inconclusive,
            meta=meta, dag=log_dag(base, os.path.dirname(xml)), dmap=dmap, commits=commits))
    rows.sort(key=lambda x: x["t"])
    collect.last_stats = stats

    # 타깃 확정 — 잰 값 > dag 추론 > 상속 > 미상
    last_known = None
    for row in rows:
        mt_ = row["meta"].get("target", "")
        if mt_.startswith("UNITY_STANDALONE_"):
            row["target"], row["tsrc"] = mt_, ""                        # 잰 값
        elif mt_.startswith("UNKNOWN"):
            # ★ regress.sh가 판정에 실패한 것을 '추론 없음'으로 덮지 않는다. 실패했다는 사실이 값이다.
            row["target"], row["tsrc"] = mt_, "?" 
        elif row["dag"] and row["dag"] in row["dmap"]:
            row["target"], row["tsrc"] = row["dmap"][row["dag"]], "~"   # 추론
        elif last_known:
            row["target"], row["tsrc"] = last_known, "↑"                # 상속
        else:
            row["target"], row["tsrc"] = "미상", "?"
        if row["target"] != "미상":
            last_known = row["target"]
        # HEAD
        if row["meta"].get("head"):
            row["head"], row["hsrc"] = row["meta"]["head"], ""
        else:
            h = head_at(int(row["t"]), row["commits"])
            row["head"], row["hsrc"] = (h, "~") if h else ("미상", "?")
    return rows, dmap


def short(t):
    return {"UNITY_STANDALONE_OSX": "OSX", "UNITY_STANDALONE_WIN": "WIN"}.get(t, t)


def render(rows, dmap):
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    L = []
    L.append("# 회귀 베이스라인 대장 — 실행당 한 줄")
    L.append("")
    L.append(f"자동 생성: `python3 docs/verify/baseline.py` · 최종 {now}")
    L.append("**손으로 고치지 마라.** 다음 실행이 통째로 덮는다.")
    L.append("")
    L.append("## 읽는 법 — 표시가 붙은 값은 잰 값이 아니다")
    L.append("")
    L.append("| 표시 | 뜻 |")
    L.append("|---|---|")
    L.append("| (없음) | **실행 시각에 잰 값**(`regress.sh`가 남긴 `.meta`). 이것만이 사실이다 |")
    L.append("| `~` | 사후 추론 — 타깃은 로그의 Bee dag 해시, HEAD는 reflog 시각 대조 |")
    L.append("| `↑` | **직전 실행에서 물려받음** — 그 실행은 재컴파일을 안 해 자기 타깃을 남기지 않았다 |")
    L.append("| `?` | **미상.** 빈 칸으로 두지 않는다 — 빈 칸은 읽는 사람이 마음대로 채운다 |")
    L.append("")
    L.append("**더러움** = 그 실행 시각의 미커밋 파일 수(`.meta`의 `dirty`). "
             "0이 아니면 그 줄은 **HEAD가 아니라 «그때 움직이던 트리»의 결과다.** "
             "병렬 라운드가 도는 밤에는 실패가 「회귀」가 아니라 「편집 중 스냅샷」일 수 있다 — "
             "귀속하기 전에 그 파일의 mtime을 실행 시각과 대조해라.")
    L.append("")
    L.append(f"dag→타깃 매핑 {len(dmap)}건: " +
             (", ".join(f"`{k}`={short(v)}" for k, v in sorted(dmap.items())) or "**0건 — 타깃 추론이 전부 죽었다**"))
    L.append("")

    # ★ 개명 대장 — 「삭제 1 + 신설 1」로 보이는 것 중 무엇이 개명인지 여기 적힌다.
    L.append("## 개명 대장 — 회귀가 아니라 개명인 것")
    L.append("")
    L.append("정본 데이터: `docs/verify/renames.tsv` · 검증기: `docs/verify/renames.py --check`")
    L.append("각 줄은 **소스 트리(.cs)** 로 매번 재검증된다 — 새 이름이 실재하고(R1), "
             "옛 이름이 사라졌고(R2), 짧은 이름이 유일할 때(R3)만 적용된다.")
    L.append("")
    if _RN_OK:
        L.append("| 옛 이름 | 새 이름 | 등록일 | 근거 |")
        L.append("|---|---|---|---|")
        for o, n, d, w, ln in _RN_OK:
            L.append(f"| `{o.rsplit('.', 1)[-1]}` | `{n.rsplit('.', 1)[-1]}` | {d} | {w} |")
    else:
        L.append("**0건.** 지금 흡수 중인 개명이 없다 — 이 표가 비어 있는 것 자체가 정상 상태다.")
    if _RN_REJ:
        L.append("")
        L.append("> ⚠ **거부된 규칙 %d건** — 적용되지 않았다(그 이름들은 대조에서 삭제/신설로 보인다):"
                 % len(_RN_REJ))
        for ln, why in _RN_REJ:
            L.append(f"> - {ln}행: {why}")
    L.append("")
    L.append("## 실행 대장")
    L.append("")
    # ★ 2026-09-14 — 「R1」 칸과 「판정 불가」 칸을 추가했다. 「실패」 칸(test-case failed=)이 0이어도
    #   R1이 빨강일 수 있다 — 픽스처 끝 실패는 failed=에 안 들어간다(docs/TEAM.md 같은 이름의 절).
    L.append("**R1** = 결과 판정(`docs/verify/nunit_verdict.py`): 초록 = failed==0 ∧ test-run result ∈ {Passed, Skipped:Ignored} "
             "∧ site가 SetUp/TearDown인 스위트 0 ∧ Failed 스위트 0 ∧ failed 속성 = Failed 케이스 수. **「실패」 칸이 0이어도 R1이 빨강일 수 있다.** "
             "`★픽스처` 로 시작하는 실패 이름은 테스트가 아니라 픽스처의 [OneTimeSetUp]/[OneTimeTearDown]이다.")
    L.append("")
    # ★ 2026-09-14 — 「범위」 칸. 「현재」는 가장 최근 **전량** 실행만이다(collect 위 문단).
    st = getattr(collect, "last_stats", None) or {}
    L.append("**범위** = 전량/부분은 **기록된 사실로만** 가른다 — Unity 로그 `COMMAND LINE ARGUMENTS:`(한 줄에 인자 하나)에 "
             "필터 인자(`-testFilter` · `-testCategory` · `-assemblyNames` 등)가 있으면 `부분(N건)`, 없으면 `전량` "
             "(로그의 `-testResults`가 **그 xml**일 때만 인정), 로그가 없으면 `regress.sh` 사이드카, 둘 다 없으면 `미확인`. "
             "testcasecount 크기로 추정하지 않는다. **「현재」는 가장 최근 `전량`만이다.**")
    L.append("")
    L.append("**수집** = `docs/verify/runs/*_edit.xml`·`*_play.xml` 전부 + 그 밖(`runs/`의 다른 xml · `Logs/**`)은 "
             "**전량이 기록으로 확인된 것만**(xml 바이트 sha256 중복 제거, `runs/` 쪽 우선). 표에서 뺀 것: "
             + (" · ".join(f"{k} {v}" for k, v in sorted(st.items())) if st else "0"))
    L.append("")
    L.append("| 시각 | 라벨 | 모드 | 범위 | HEAD | 더러움 | 활성 타깃 | total | 통과 | 실패 | 건너뜀 | 판정 불가 | R1 | 실패 목록 |")
    L.append("|---|---|---|---|---|---:|---|---:|---:|---:|---:|---:|---|---|")
    for r in rows:
        # 시각 = xml에 기록된 실행 시작(로컬). `~` = 기록이 없어 파일 mtime을 썼다.
        ts = r["tsrc_time"] + datetime.datetime.fromtimestamp(r["t"]).strftime("%m-%d %H:%M")
        fl = "—" if not r["fails"] else "<br>".join(r["fails"])
        r1 = "초록" if r["green"] else "**빨강**"
        inc = f"**{len(r['inconclusive'])}**" if r["inconclusive"] else "0"
        scope_cell = ("전량" if r["scope"] == "전량"
                      else f"**부분({r['total']}건)**" if r["scope"] == "부분" else "**미확인**")
        tcc = "" if r["tcc"] == r["total"] else f" ⚠tcc={r['tcc']}"
        # ★ 실행 도중 재컴파일로 타깃이 바뀌면 '실행 전' 값으로 재해석하면 안 된다.
        if r["meta"].get("target_shifted") == "1":
            r["tsrc"] += "⇄"
        # ★ 「작업 트리가 몇 개 더러웠는가」 — 병렬 라운드가 도는 밤에는 이 값이 곧
        #   «이 측정이 HEAD가 아니라 움직이는 트리의 스냅샷이다»라는 뜻이다.
        #   실측 2026-09-03: dirty=40 상태에서 잰 EditMode의 실패 5건 중 3건이
        #   **측정 중에 편집되고 있던 파일**이었다. 이 칸이 없으면 그 사실이 표에서 사라진다.
        d = r["meta"].get("dirty", "")
        dcell = f"{d}" if d else "?"
        if d and d.isdigit() and int(d) > 0:
            dcell = f"**{d}**"
        L.append(f"| {ts} | `{r['label']}` | {r['mode']} | {scope_cell} | {r['hsrc']}{r['head']} "
                 f"| {dcell} "
                 f"| **{r['tsrc']}{short(r['target'])}** | {r['total']}{tcc} | {r['passed']} "
                 f"| {r['failed']} | {r['skipped']} | {inc} | {r1} | {fl} |")
    L.append("")

    # 지금 무엇이 빨간가 + 언제부터인가
    L.append("## 지금 빨간 것 — 그리고 **언제부터**인가")
    L.append("")
    L.append("「현재」 = 그 모드의 **가장 최근 `전량` 실행**. 그보다 새 `부분`·`미확인` 실행은 「현재」로 치지 않고 개수만 적는다.")
    L.append("")
    for mode in ("edit", "play"):
        mr = [r for r in rows if r["mode"] == mode]
        if not mr:
            continue
        # ★ 2026-09-14 — 옛 판은 `cur = mr[-1]`(mtime 최신)이었다. 09-14 play 최신이 62건 부분 실행이라
        #   「빨강 없음」이 찍혔고, 같은 날 전량 783건의 실패 5·판정 불가 2가 보이지 않았다.
        mf = [r for r in mr if r["scope"] == "전량"]
        if not mf:
            npart = sum(1 for r in mr if r["scope"] == "부분")
            nunk = sum(1 for r in mr if r["scope"] == "미확인")
            L.append(f"### {mode} — **현재 없음**")
            L.append("")
            L.append(f"전량 실행 기록이 한 건도 없다(부분 {npart} · 미확인 {nunk}). "
                     "부분 실행의 「빨강 없음」을 이 모드의 상태로 읽지 마라.")
            L.append("")
            continue
        cur = mf[-1]
        after = [r for r in mr if r["t"] > cur["t"]]
        mr = [r for r in mr if r["t"] <= cur["t"]]          # 「언제부터」도 현재 시점까지만 센다
        L.append(f"### {mode} — 현재 `{cur['label']}` "
                 f"({cur['tsrc_time']}{datetime.datetime.fromtimestamp(cur['t']).strftime('%m-%d %H:%M')}, "
                 f"전량 {cur['total']}건, 타깃 {cur['tsrc']}{short(cur['target'])})")
        L.append("")
        L.append(f"범위 근거: {cur['scope_why']}"
                 + (f" · 그 뒤 부분/미확인 실행 **{len(after)}건**(최근 `{after[-1]['label']}`)은 「현재」로 치지 않았다"
                    if after else ""))
        L.append("")
        # ★ 2026-09-14 R5 — 판정 불가는 failed=에 안 들어간다. 빨강/초록과 무관하게 이름을 적는다.
        if cur["inconclusive"]:
            L.append(f"**판정 불가(Inconclusive) {len(cur['inconclusive'])}건** — `failed=`에 안 들어가고 "
                     "옛 대장·옛 `compare`에는 안 보였다: " + " · ".join(f"`{n}`" for n in cur["inconclusive"]))
            L.append("")
        # ★ 2026-09-14 R1 — 「빨강 없음」은 **R1이 초록일 때만** 쓴다. 옛 대장은 test-case 실패 목록이
        #   비면 무조건 「빨강 없음」이었다 = mut-M5p.xml(픽스처 끝 실패)을 초록으로 적었을 것이다.
        if cur["green"]:
            L.append("빨강 없음 (R1 초록).")
            L.append("")
            continue
        if not cur["fails"]:
            L.append("**R1 빨강인데 실패 이름을 특정하지 못했다** — 사유: " + " / ".join(cur["reasons"]))
            L.append("")
            continue
        # ★ 2026-09-14 — 「실제로 초록」 = 그 테스트가 **Passed**였던 실행만. 옛 판은 «빨강이 아니면 초록»이라
        #   건너뜀·판정 불가를 초록으로 셌다 — 표 제목의 「실제로」가 거짓이 되는, 죽은 측정이 산 측정처럼 생긴 형태.
        #   건너뜀·판정 불가는 초록도 빨강도 아니다: 마지막 초록을 갱신하지 않고, 연속 빨강을 끊지도 늘리지도 않는다.
        L.append("| 실패 | 마지막으로 **실제로 초록**(Passed)이던 실행 | 그 뒤 건너뜀·판정 불가(초록 아님) | 처음 빨개진 실행 | 연속 빨강 |")
        L.append("|---|---|---|---|---:|")
        for name in cur["fails"]:
            lastgreen = firstred = lastskip = None
            redstreak = skips = 0
            for r in mr:
                if name not in r["present"]:
                    continue                     # ★ 없던 실행은 초록도 빨강도 아니다
                if name in r["fails"]:
                    if firstred is None:
                        firstred = r
                    redstreak += 1
                elif name in r["passed_names"]:
                    lastgreen, firstred, redstreak, skips, lastskip = r, None, 0, 0, None   # 진짜 초록 — 다시 센다
                else:
                    skips, lastskip = skips + 1, r   # Skipped/Ignored/Inconclusive — 재지 않았다
            def tag(r):
                if r is None:
                    return "**한 번도 없다**"
                scope = "" if r["scope"] == "전량" else f" ({r['scope']})"
                return (f"`{r['label']}`{scope} "
                        f"{r['tsrc_time']}{datetime.datetime.fromtimestamp(r['t']).strftime('%m-%d %H:%M')}")
            skipcell = "—" if not skips else f"{skips}건 (최근 {tag(lastskip)})"
            L.append(f"| {name} | {tag(lastgreen)} | {skipcell} | {tag(firstred)} | {redstreak} |")
        L.append("")
    return "\n".join(L) + "\n"


# ---- 자기검사: 이 생성기가 조용히 거짓말하지 않는가 -------------------------
def check():
    rc = 0
    dmap = dag_target_map()
    print("── 양성 대조 1: dag→타깃 매핑이 실제로 서 있는가")
    if dmap:
        print(f"  ✓ {len(dmap)}건 — " + ", ".join(f"{k}={short(v)}" for k, v in sorted(dmap.items())))
    else:
        print("  ✗ 0건 — 타깃 추론이 전부 죽는다. 표는 전부 `?미상`이 되어야 한다."); rc = 1

    print("── 양성 대조 2: 매핑에 WIN과 OSX가 **둘 다** 나오는가(한쪽만이면 구분 능력이 없다)")
    vals = set(dmap.values())
    if {"UNITY_STANDALONE_WIN", "UNITY_STANDALONE_OSX"} <= vals:
        print("  ✓ 둘 다 나온다 — 이 탐침은 실제로 타깃을 **가른다**")
    else:
        print(f"  · 한쪽만 있다({sorted(short(v) for v in vals)}). "
              "지금은 가를 수 없다 — 표의 값을 '구분됐다'로 읽지 마라.")

    print("── 음성 대조 3: 없는 dag 해시를 물으면 미상이 되는가")
    if "ZZZnoSuchDag.dag" in dmap:
        print("  ✗ 존재할 리 없는 해시가 매핑에 있다."); rc = 1
    else:
        print("  ✓ 없다")

    print("── 양성 대조 4: reflog로 HEAD를 되살릴 수 있는가")
    c = reflog_commits()
    if c:
        h = head_at(int(datetime.datetime.now().timestamp()), c)
        real = subprocess.run(["git", "-C", REPO, "rev-parse", "--short", "HEAD"],
                              capture_output=True, text=True).stdout.strip()
        if h == real:
            print(f"  ✓ 지금 시각으로 물으면 실제 HEAD({real})가 나온다")
        else:
            print(f"  ✗ 되살린 값({h}) != 실제 HEAD({real}) — HEAD 칸을 믿지 마라."); rc = 1
    else:
        print("  ✗ reflog를 못 읽었다 — HEAD 칸이 전부 `?미상`이 되어야 한다."); rc = 1

    print("── 음성 대조 5: 아주 오래된 시각을 물으면 None이 나오는가")
    print("  ✓ 없다" if head_at(0, c) is None else "  ✗ 0 epoch에 커밋이 잡혔다"); rc |= 0 if head_at(0, c) is None else 1

    print("── 양성 대조 6: 표에 실제로 두 종류의 타깃이 찍히는가")
    rows, _ = collect()
    seen = {}
    for r in rows:
        seen.setdefault(short(r["target"]), 0)
        seen[short(r["target"])] += 1
    print(f"  · 실행 {len(rows)}건의 타깃 분포: {seen}")
    if len(rows) and set(seen) == {"미상"}:
        print("  ✗ 전부 미상이다 — 이 대장은 아무것도 알려주지 않는다."); rc = 1
    print("── 양성 대조 7: ★ '잰 값'(.meta 사이드카)이 실제로 읽히는가")
    measured = [r for r in rows if r["tsrc"] == ""]
    if measured:
        print(f"  ✓ {len(measured)}/{len(rows)}건이 실행 시각에 잰 값이다 — "
              + ", ".join(r["base"] for r in measured[-3:]))
    else:
        print(f"  · 0/{len(rows)}건. 지금 표의 타깃은 **전부 사후 추론**이다(`~`/`↑`). "
              "regress.sh가 .meta를 쓰기 시작한 뒤의 실행부터 잰 값이 된다 — "
              "그전 줄을 '기록됐다'로 읽지 마라.")

    print("── 음성 대조 8: 조작된 .meta를 넣으면 그 값이 '잰 값'으로 표에 뜨는가(사이드카 경로가 살아 있는가)")
    # ★ 2026-09-14 — 탐침을 실제 docs/verify/runs/ 에 쓰지 않고 임시 폴더에 쓴다. 옛 판은 실행 중인
    #   다른 라운드의 prev_best(G9)가 잠깐 존재하는 ZZZ 탐침을 볼 수 있었다. 읽는 코드(collect)는 같다.
    import tempfile, shutil
    tdir = tempfile.mkdtemp(prefix="baseline-check-")
    try:
        probe = os.path.join(tdir, "ZZZbaselineprobe_edit.meta")
        probexml = os.path.join(tdir, "ZZZbaselineprobe_edit.xml")
        open(probexml, "w").write(
            '<test-run testcasecount="1" result="Passed" total="1" passed="1" failed="0" skipped="0"></test-run>')
        open(probe, "w").write("target=UNITY_STANDALONE_WIN\nhead=deadbee\ntarget_shifted=1\n")
        rows2, _ = collect(tdir)
        hit = [r for r in rows2 if r["base"] == "ZZZbaselineprobe_edit"]
        if hit and hit[0]["tsrc"] == "" and hit[0]["target"] == "UNITY_STANDALONE_WIN" \
                and hit[0]["head"] == "deadbee":
            print("  ✓ 사이드카를 읽어 '잰 값'으로 표시했다 — 이 경로는 죽어 있지 않다")
        else:
            got = (hit[0]["tsrc"], hit[0]["target"], hit[0]["head"]) if hit else None
            print(f"  ✗ 사이드카가 무시됐다({got}) — .meta 를 써도 표에 반영되지 않는다."); rc = 1

        # ★★ 2026-09-14 R1/R2/R5 대조 — docs/TEAM.md 「픽스처 끝에서 난 실패는 실패 개수에 안 들어간다」.
        #   옛 대장은 test-case Failed만 셌다. 합성본은 이 파일 안에 문자열로 박는다(저장소에서 읽지 않는다).
        def wlog(xml_path, platform, filt=None):
            """Unity 로그 머리를 **실제 모양 그대로**(한 줄에 인자 하나) 흉내 낸다 — play-full.log 16~27행 대조."""
            args = ["/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity",
                    "-batchmode", "-nographics", "-projectPath", "/probe", "-runTests", "-testPlatform", platform]
            if filt:
                args += ["-testFilter", filt]
            args += ["-testResults", xml_path, "-logFile", os.path.splitext(xml_path)[0] + ".log"]
            open(os.path.splitext(xml_path)[0] + ".log", "w", encoding="utf-8").write(
                "[Licensing::Module] 합성 머리말\n\nCOMMAND LINE ARGUMENTS:\n" + "\n".join(args) +
                "\nSuccessfully changed project path to: /probe\n")

        def w(name, run_attrs, sfx_attrs, cases, d=None, start=None, filt=None, log=True,
              platform=None, discovered=None):
            """합성 결과 xml. ★ 2026-09-14 — 기본으로 짝 로그(필터 없음)를 함께 쓴다: 로그가 없으면
            범위가 「미확인」이 되어 「현재」 후보에서 빠진다(그것 자체가 이 파일의 규칙이다)."""
            d = d or tdir
            plat = platform or ("PlayMode" if name.endswith("_play") else "EditMode")
            body = "".join(f'<test-case name="{n}" fullname="StickMate.Tests.Probe.{n}" result="{r_}" />'
                           for n, r_ in cases)
            xp = os.path.join(d, name + ".xml")
            st_ = f' start-time="{start}"' if start else ""
            disc = discovered if discovered is not None else len(cases)
            open(xp, "w", encoding="utf-8").write(
                f'<test-run id="2" testcasecount="{len(cases)}" total="{len(cases)}"{st_} {run_attrs}>'
                f'<test-suite type="SetUpFixture" fullname="ProbeIsolationFixture" testcasecount="{disc}" {sfx_attrs}>'
                f'<properties><property name="platform" value="{plat}" /></properties>{body}</test-suite>'
                '</test-run>')
            if log:
                wlog(xp, plat, filt)
            return xp
        w("ZZZr1ok_play", 'result="Passed" passed="2" failed="0"', 'result="Passed"',
          [("판정탐침_가", "Passed"), ("판정탐침_나", "Passed")])
        w("ZZZr1teardown_play", 'result="Failed(Child)" passed="2" failed="0"',
          'result="Failed" label="Ignored" site="TearDown"', [("판정탐침_가", "Passed"), ("판정탐침_나", "Passed")])
        w("ZZZr1inc_edit", 'result="Passed" passed="1" failed="0" inconclusive="1"', 'result="Passed"',
          [("판정탐침_가", "Passed"), ("판정탐침_불가", "Inconclusive")])
        # 시각 순서를 박는다: ok(과거) → teardown(최신) — 「언제부터」 추적이 ok를 마지막 초록으로 잡아야 한다
        now = datetime.datetime.now().timestamp()
        os.utime(os.path.join(tdir, "ZZZr1ok_play.xml"), (now - 60, now - 60))
        os.utime(os.path.join(tdir, "ZZZr1teardown_play.xml"), (now, now))
        rows3, dm3 = collect(tdir)
        by = {r["base"]: r for r in rows3}
        md3 = render(rows3, dm3)

        print("── R1 정상(음성) 대조: 규칙 1을 지키는 xml은 초록인가")
        ok_ = by.get("ZZZr1ok_play")
        if ok_ and ok_["green"] and not ok_["fails"]:
            print("  ✓ 초록, 실패 이름 0")
        else:
            print(f"  ✗ 정상 xml을 초록으로 못 읽었다: {ok_ and (ok_['green'], ok_['fails'], ok_['reasons'])}"); rc = 1

        print("── R1 탐지(양성) 대조: ★ failed=0 + SetUpFixture site=TearDown(label=Ignored 변종)이 빨강이고 «실패 이름»에 오르는가")
        td = by.get("ZZZr1teardown_play")
        want = f"{FIXTURE_KEY}TearDown ProbeIsolationFixture"
        if td and not td["green"] and want in td["fails"] and td["failed"] == 0:
            print(f"  ✓ 빨강 — 실패 칸 failed=0인데 실패 이름에 `{want}`")
        else:
            print(f"  ✗ 못 잡았다: {td and (td['green'], td['fails'], td['failed'])}"); rc = 1

        print("── R1 탐지(양성) 대조: 「지금 빨간 것」이 그 픽스처를 적고, 마지막 초록을 앞선 실행으로 잡는가")
        sect = md3.split("### play", 1)[-1]
        if want in sect and "빨강 없음" not in sect and "`ZZZr1ok`" in sect:
            print("  ✓ 「지금 빨간 것」에 픽스처가 있고 마지막 초록 = `ZZZr1ok`")
        else:
            print("  ✗ 「지금 빨간 것」 절이 픽스처 실패를 못 적었다:\n" + sect[:600]); rc = 1

        print("── R5 대조: 판정 불가 **이름**이 대장에 찍히고, 규칙 1상 초록은 유지되는가")
        inc = by.get("ZZZr1inc_edit")
        sect_e = md3.split("### edit", 1)[-1].split("### play", 1)[0]
        if inc and inc["green"] and "판정탐침_불가" in inc["inconclusive"] and "판정탐침_불가" in sect_e \
                and "빨강 없음 (R1 초록)" in sect_e:
            print("  ✓ 이름 `판정탐침_불가`가 찍혔고 R1 초록")
        else:
            print(f"  ✗ 판정 불가 이름이 안 찍혔다: {inc and (inc['green'], inc['inconclusive'])}"); rc = 1

        # ★★ 2026-09-14 — 「현재」 = 가장 최근 **전량** (리더 후속 지시). 시나리오마다 폴더를 갈라 섞이지 않게 한다.
        P2 = 'result="Passed" passed="2" failed="0"'
        R2 = 'result="Failed(Child)" passed="1" failed="1"'
        C2 = [("판정탐침_가", "Passed"), ("판정탐침_나", "Passed")]
        C2F = [("판정탐침_가", "Passed"), ("판정탐침_나", "Failed")]
        A = os.path.join(tdir, "A")
        B = os.path.join(tdir, "B")
        CR = os.path.join(tdir, "C", "runs")
        CL = os.path.join(tdir, "C", "logs")
        for dd in (A, B, CR, CL):
            os.makedirs(dd)

        print("── 범위 양성 대조: ★ 최신 mtime이 부분 실행이고 그 전에 전량이 있으면 「현재」가 전량을 가리키는가")
        w("ZZZfull_play", R2, 'result="Failed" site="Child"', C2F, d=A, start="2026-09-10 00:00:00Z")
        w("ZZZpart_play", P2, 'result="Passed"', C2, d=A, start="2026-09-12 00:00:00Z", filt="^StickMate\\.Tests\\.Probe\\.")
        w("ZZZunk_play", P2, 'result="Passed"', C2, d=A, start="2026-09-13 00:00:00Z", log=False)
        w("ZZZcontra_edit", P2, 'result="Passed"', C2, d=A, start="2026-09-13 00:00:00Z", discovered=99)
        now = datetime.datetime.now().timestamp()
        for nm, dt in (("ZZZfull_play", -300), ("ZZZunk_play", -200), ("ZZZcontra_edit", -100), ("ZZZpart_play", 60)):
            os.utime(os.path.join(A, nm + ".xml"), (now + dt, now + dt))          # mtime 최신 = 부분 실행
        rowsA, dmA = collect(A, extra_roots=[])
        mdA = render(rowsA, dmA)
        byA = {r["label"]: r for r in rowsA}
        gotA = {k: byA[k]["scope"] for k in byA}
        wantA = {"ZZZfull": "전량", "ZZZpart": "부분", "ZZZunk": "미확인", "ZZZcontra": "미확인"}
        playA = mdA.split("### play", 1)[-1]
        okA = (gotA == wantA
               and max(rowsA, key=lambda x: x["mt"])["label"] == "ZZZpart"      # 전제: mtime 최신이 정말 부분이다
               and playA.startswith(" — 현재 `ZZZfull`")
               and "그 뒤 부분/미확인 실행 **2건**" in playA
               and "판정탐침_나" in playA                                      # 현재(전량)의 실패가 떴다
               and "**부분(2건)**" in mdA and "**미확인**" in mdA
               and "모순" in byA["ZZZcontra"]["scope_why"])
        if okA:
            print("  ✓ 현재 play = `ZZZfull`(전량) · 뒤의 부분·미확인 2건 제외 · 범위 칸 부분(2건)/미확인 · 모순은 미확인")
        else:
            print(f"  ✗ 범위 {gotA} (기대 {wantA})\n{playA[:500]}"); rc = 1

        print("── 범위 음성 대조: 전량 기록이 전혀 없으면 「현재 없음」인가(부분 실행을 현재로 삼지 않는가)")
        w("ZZZonlypart_play", P2, 'result="Passed"', C2, d=B, start="2026-09-12 00:00:00Z", filt="^Probe")
        w("ZZZonlyunk_play", P2, 'result="Passed"', C2, d=B, start="2026-09-13 00:00:00Z", log=False)
        rowsB, dmB = collect(B, extra_roots=[])
        playB = render(rowsB, dmB).split("### play", 1)[-1]
        # ★ 「빨강 없음」 글자 자체는 안내문(«부분 실행의 「빨강 없음」을 …읽지 마라»)에 들어 있다 — 판정 줄로 본다.
        if playB.startswith(" — **현재 없음**") and "부분 1 · 미확인 1" in playB and "빨강 없음 (R1 초록)" not in playB:
            print("  ✓ 현재 없음(부분 1 · 미확인 1)")
        else:
            print(f"  ✗ 전량이 없는데 「현재」를 골랐다:\n{playB[:400]}"); rc = 1

        print("── 수집 확장 대조: runs/ 밖(Logs 흉내)의 전량은 들어오고 · 부분·로그 없음은 빠지고 · 같은 바이트 사본은 한 줄인가")
        oldp = w("ZZZold_play", P2, 'result="Passed"', C2, d=CR, start="2026-09-10 00:00:00Z")
        dup = os.path.join(CL, "dup.xml")
        shutil.copyfile(oldp, dup)
        wlog(dup, "PlayMode")                                                  # 바이트 동일 사본 + 자기 경로 짝 로그
        w("newer", R2, 'result="Failed" site="Child"', C2F, d=CL, start="2026-09-12 00:00:00Z", platform="PlayMode")
        w("partial", P2, 'result="Passed"', C2, d=CL, start="2026-09-13 00:00:00Z", platform="PlayMode", filt="^Probe")
        w("nolog", P2, 'result="Passed"', C2, d=CL, start="2026-09-14 00:00:00Z", platform="PlayMode", log=False)
        rowsC, dmC = collect(CR, extra_roots=[CL])
        stC = dict(collect.last_stats)
        playC = render(rowsC, dmC).split("### play", 1)[-1]
        labelsC = sorted(r["label"] for r in rowsC)
        okC = (labelsC == ["ZZZold", "newer"]
               and stC.get("중복(해시)") == 1 and stC.get("부분") == 1
               and stC.get("미확인(로그 명령줄·사이드카 없음)") == 1
               and playC.startswith(" — 현재 `newer`") and "판정탐침_나" in playC)
        if okC:
            print(f"  ✓ 행 {labelsC} · 뺀 것 {stC} · 현재 play = runs/ 밖의 `newer`")
        else:
            print(f"  ✗ 행 {labelsC} · 뺀 것 {stC}\n{playC[:400]}"); rc = 1

        # ★★ 2026-09-14 — 「실제로 초록」 = Passed만 (리더 후속 지시 1)
        def hist_row(md, name):
            for ln in md.split("\n"):
                if ln.startswith(f"| {name} |"):
                    return [c.strip() for c in ln.strip().strip("|").split("|")]
            return None

        print("── 「실제로 초록」 대조: ★ 건너뜀·판정 불가가 더 최신이고 Passed가 더 옛날이면 옛 Passed를 가리키는가")
        H = os.path.join(tdir, "H")
        os.makedirs(H)
        def T3(a, b, c):
            return [("대상", a), ("늘건너뜀", b), ("곁", c)]
        SKIPSFX = 'result="Skipped" label="Ignored"'
        w("ZZZh1_play", 'result="Skipped:Ignored" passed="2" failed="0" skipped="1"', SKIPSFX,
          T3("Passed", "Skipped", "Passed"), d=H, start="2026-09-01 00:00:00Z")
        w("ZZZh2_play", 'result="Skipped:Ignored" passed="1" failed="0" skipped="2"', SKIPSFX,
          T3("Skipped", "Skipped", "Passed"), d=H, start="2026-09-05 00:00:00Z")
        w("ZZZh3_play", 'result="Skipped:Ignored" passed="1" failed="0" inconclusive="1" skipped="1"', SKIPSFX,
          T3("Inconclusive", "Skipped", "Passed"), d=H, start="2026-09-07 00:00:00Z")
        w("ZZZh4_play", 'result="Failed(Child)" passed="1" failed="2"', 'result="Failed" site="Child"',
          T3("Failed", "Failed", "Passed"), d=H, start="2026-09-10 00:00:00Z")
        rowsH, dmH = collect(H, extra_roots=[])
        mdH = render(rowsH, dmH)
        rA, rB = hist_row(mdH, "대상"), hist_row(mdH, "늘건너뜀")
        okH = (rA is not None and "`ZZZh1`" in rA[1] and "2건" in rA[2] and "`ZZZh3`" in rA[2]
               and "`ZZZh4`" in rA[3] and rA[4] == "1"
               and rB is not None and "한 번도 없다" in rB[1] and "3건" in rB[2] and "`ZZZh4`" in rB[3]
               and hist_row(mdH, "곁") is None)
        if okH:
            print(f"  ✓ 대상: 마지막 초록 {rA[1]} · 건너뜀 {rA[2]} · 처음 빨강 {rA[3]} / 늘건너뜀: 마지막 초록 {rB[1]}")
        else:
            print(f"  ✗ 대상 {rA} / 늘건너뜀 {rB}"); rc = 1

        print("── 「실제로 초록」 실측 교정: 왕관 — 마지막 Passed는 qa-r10(09-05)이고 그 뒤 건너뜀은 초록이 아니다")
        RL = os.path.join(tdir, "RL")
        os.makedirs(RL)
        links = [(os.path.join(OUTDIR, b + ext), os.path.join(RL, b + ext))
                 for b in ("qa-r10_play", "coder-fan-play_play", "r28-consolidated_play", "part2-final_play")
                 for ext in (".xml", ".log", ".meta")]
        links += [(os.path.join(REPO, "Logs/coder-onbstore/play-full" + ext), os.path.join(RL, "onbstore-play-full_play" + ext))
                  for ext in (".xml", ".log")]
        need = [s for s, _ in links if s.endswith(".xml")]
        missing = [os.path.basename(s) for s in need if not os.path.isfile(s)]
        if missing:
            print("  · 실측 xml이 디스크에 없다 — 미확인: " + ", ".join(missing))
        else:
            for s, d_ in links:
                if os.path.isfile(s):
                    os.symlink(s, d_)       # 심볼릭 링크 — 로그 짝 판정은 realpath로 원본과 맞춘다
            rowsR, dmR = collect(RL, extra_roots=[])
            playR = render(rowsR, dmR).split("### play", 1)[-1]
            crown = hist_row(playR, "왕관은_채워지되_얹는_물건으로_남는다")
            if playR.startswith(" — 현재 `onbstore-play-full`") and crown and "`qa-r10`" in crown[1] \
                    and "2건" in crown[2] and "`part2-final`" in crown[2] and "`coder-fan-play`" in crown[3]:
                print(f"  ✓ 마지막 초록 {crown[1]} · 건너뜀 {crown[2]} · 처음 빨강 {crown[3]} · 연속 {crown[4]}")
            else:
                print(f"  ✗ 왕관 행 {crown} / 현재 {playR[:80]!r}"); rc = 1

        # ★★ 2026-09-14 — 사이드카 단독(로그 없음) 전량 판정 경로 (리더 후속 지시 2 — 실데이터 0건이라 미검증이었다)
        print("── 사이드카 단독 대조: 전량 사이드카 → 전량 · 필터 흔적 사이드카 → 부분 · 형식/라벨 불일치 → 미확인")
        SC = os.path.join(tdir, "SC")
        os.makedirs(SC)
        def wmeta(base_, **over):
            kv = dict(label=base_.rsplit("_", 1)[0], mode=base_.rsplit("_", 1)[1], head="deadbee", dirty="0",
                      target="UNITY_STANDALONE_OSX", target_before="UNITY_STANDALONE_OSX", target_shifted="0",
                      started="1", finished="2", unity_rc="0")
            kv.update(over)
            open(os.path.join(SC, base_ + ".meta"), "w", encoding="utf-8").write(
                "".join(f"{k}={v}\n" for k, v in kv.items()))
        for b in ("ZZZsidefull_play", "ZZZsideargs_play", "ZZZsidefilt_play", "ZZZsidelabel_play", "ZZZsidekeys_play"):
            w(b, P2, 'result="Passed"', C2, d=SC, start="2026-09-11 00:00:00Z", log=False)
        wmeta("ZZZsidefull_play")                                                   # args 기록 전 판
        wmeta("ZZZsideargs_play", args="-batchmode -nographics -runTests -testPlatform PlayMode -testResults /x.xml -logFile /x.log")
        wmeta("ZZZsidefilt_play", args="-batchmode -runTests -testPlatform PlayMode -testFilter ^Probe -testResults /x.xml")
        wmeta("ZZZsidelabel_play", label="딴라벨")
        open(os.path.join(SC, "ZZZsidekeys_play.meta"), "w", encoding="utf-8").write("label=ZZZsidekeys\nmode=play\n")
        rowsS, _ = collect(SC, extra_roots=[])
        gotS = {r["label"]: r["scope"] for r in rowsS}
        whyS = {r["label"]: r["scope_why"] for r in rowsS}
        wantS = {"ZZZsidefull": "전량", "ZZZsideargs": "전량", "ZZZsidefilt": "부분",
                 "ZZZsidelabel": "미확인", "ZZZsidekeys": "미확인"}
        if gotS == wantS and "사이드카" in whyS["ZZZsidefull"] and "-testFilter" in whyS["ZZZsidefilt"] \
                and "라벨" in whyS["ZZZsidelabel"] and "키 부족" in whyS["ZZZsidekeys"]:
            print(f"  ✓ {gotS}")
        else:
            print(f"  ✗ 범위 {gotS} (기대 {wantS}) · 근거 {whyS}"); rc = 1
    finally:
        shutil.rmtree(tdir, ignore_errors=True)

    print("── R1 실측 교정: 실제 러너 xml(TEAM.md 규칙 3) — 없으면 미확인")
    for path, expect_green, token in (
            (os.path.join(REPO, "Logs/coder-onbstore/mut-M5p.xml"), False, "GlobalPlayModeTestIsolation"),
            (os.path.join(REPO, "Logs/coder-onbstore/edit-full.xml"), True, None)):
        if not os.path.isfile(path):
            print(f"  · {os.path.basename(path)} 없음 — 미확인"); continue
        v = NV.judge(path)
        hit = token is None or any(token in f["fullname"] for f in v["fixture_failures"])
        if v["green"] == expect_green and hit:
            print(f"  ✓ {os.path.basename(path)} → {'초록' if v['green'] else '빨강'}")
        else:
            print(f"  ✗ {os.path.basename(path)} → {'초록' if v['green'] else '빨강'}(기대 {'초록' if expect_green else '빨강'}) {v['reasons'][:2]}"); rc = 1

    print("── 양성 대조 9: ★ 개명 대장이 실제로 로드·검증되는가")
    try:
        import renames as rn2
        _cf, _cs, ok2, rej2 = rn2.load()
        print(f"  · 적용 {len(ok2)}건 / 거부 {len(rej2)}건")
        if rej2:
            for ln, why in rej2:
                print(f"    ✗ {ln}행 — {why}")
            print("  ⚠ 거부된 줄이 있다. 그 개명은 흡수되지 않는다(=삭제+신설로 보인다).")
        # ★ 음성 대조: 정규화 함수가 실제로 무엇인가를 «바꾸는가».
        #   항등 함수여도 위의 '적용 N건'은 똑같이 찍힌다 — 그게 이 저장소의 병이다.
        if ok2:
            probe_old = ok2[0][0].rsplit(".", 1)[-1]
            probe_new = ok2[0][1].rsplit(".", 1)[-1]
            if _cs(probe_old) == probe_new:
                print(f"  ✓ 정규화가 실제로 이름을 바꾼다: {probe_old} → {_cs(probe_old)}")
            else:
                print(f"  ✗ 정규화가 이름을 안 바꾼다({probe_old} → {_cs(probe_old)}) — "
                      "'적용 N건'은 표시일 뿐 실제로는 항등이다."); rc = 1
            if _cs("ZZZ_등록되지_않은_이름_XYZ") != "ZZZ_등록되지_않은_이름_XYZ":
                print("  ✗ 등록되지 않은 이름까지 바꾼다 — 흡수가 과잉이다."); rc = 1
            else:
                print("  ✓ 등록되지 않은 이름은 건드리지 않는다(음성 대조)")
        else:
            print("  · 대장이 비어 정규화 대조는 판정 불가(미확인).")
    except Exception as e:
        print(f"  ✗ 개명 대장을 못 읽었다: {e}"); rc = 1

    print("자기검사 통과" if rc == 0 else "자기검사 실패")
    return rc


def prev_row_count():
    """직전 BASELINE.md가 적어 둔 실행 건수. 없으면 None."""
    if not os.path.isfile(OUTMD):
        return None
    m = re.search(r"<!-- rows=(\d+) -->", open(OUTMD, encoding="utf-8", errors="replace").read())
    return int(m.group(1)) if m else None


if __name__ == "__main__":
    if "--check" in sys.argv:
        sys.exit(check())
    # ★ 2026-09-14 — `--out <경로>`: 대장을 다른 곳에 쓴다(교정·예행용). 실제 BASELINE.md를 덮지 않고
    #   새 판정 규칙의 출력을 볼 수 있어야 한다. 경로 없이 `--out`만 주면 조용히 기본값으로 가지 않고 멈춘다.
    if "--out" in sys.argv:
        i = sys.argv.index("--out")
        if i + 1 >= len(sys.argv) or not sys.argv[i + 1].strip() or sys.argv[i + 1].startswith("--"):
            print("✗ --out 뒤에 출력 경로가 없다 — 실제 BASELINE.md를 덮지 않도록 멈춘다.")
            sys.exit(2)
        OUTMD = os.path.abspath(sys.argv[i + 1])
    rows, dmap = collect()
    prev = prev_row_count()
    md = render(rows, dmap)

    # ★ 이 대장은 **디스크에 남은 결과 파일에서만** 만들어진다. 그런데 docs/verify/runs/ 는
    #   .gitignore 대상이라 누가 지우면 그대로 사라지고, 그때 줄어든 표는 "실행이 적었다"와
    #   **똑같이 생겼다**. 그래서 직전 건수를 파일 안에 심어 두고 줄면 표 안에 경고를 박는다.
    warn = ""
    if prev is not None and len(rows) < prev:
        warn = (f"\n> ⚠ **직전 생성 때는 {prev}건이었는데 지금 {len(rows)}건이다 "
                f"({prev - len(rows)}건 사라졌다).** `docs/verify/runs/`는 `.gitignore` 대상이라 "
                "지워지면 이력이 함께 사라진다 — 줄어든 표를 '실행이 적었다'로 읽지 마라.\n")
    md = md.replace("**손으로 고치지 마라.** 다음 실행이 통째로 덮는다.",
                    "**손으로 고치지 마라.** 다음 실행이 통째로 덮는다." + warn)
    md += f"\n<!-- rows={len(rows)} -->\n"
    open(OUTMD, "w", encoding="utf-8").write(md)
    print(f"{OUTMD} 갱신 — 실행 {len(rows)}건 / dag매핑 {len(dmap)}건"
          + (f"  ⚠ 직전 {prev}건에서 줄었다" if warn else ""))
