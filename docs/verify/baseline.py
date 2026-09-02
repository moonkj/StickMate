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

REPO   = "/Users/kjmoon/App/StickMate"
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


def read_meta(base):
    """regress.sh가 실행 시각에 남긴 사이드카. 있으면 이것이 사실이다."""
    p = os.path.join(OUTDIR, base + ".meta")
    if not os.path.isfile(p):
        return {}
    d = {}
    for line in open(p, encoding="utf-8", errors="replace"):
        if "=" in line:
            k, v = line.rstrip("\n").split("=", 1)
            d[k.strip()] = v.strip()
    return d


def log_dag(base):
    p = os.path.join(OUTDIR, base + ".log")
    if not os.path.isfile(p):
        return None
    try:
        with open(p, encoding="utf-8", errors="replace") as f:
            hits = set(re.findall(r"artifacts/([0-9a-zA-Z]+\.dag)", f.read()))
    except OSError:
        return None
    hits = {h for h in hits if h.endswith("E.dag")}     # 에디터 어셈블리
    return sorted(hits)[0] if len(hits) == 1 else None


def collect():
    dmap = dag_target_map()
    commits = reflog_commits()
    rows = []
    for xml in glob.glob(os.path.join(OUTDIR, "*_edit.xml")) + \
               glob.glob(os.path.join(OUTDIR, "*_play.xml")):
        base = os.path.basename(xml)[:-4]
        try:
            r = ET.parse(xml).getroot()
        except Exception:
            continue
        mt = os.stat(xml).st_mtime
        fails = sorted(tc.get("fullname").split(".")[-1]
                       for tc in r.iter("test-case") if tc.get("result") == "Failed")
        # ★ "그때는 초록이었다"와 "그때는 존재하지 않았다"는 완전히 다른 말이다.
        #   이름 집합을 함께 들고 다니지 않으면 대장이 없던 테스트를 '초록이었다'로 둔갑시킨다.
        present = {tc.get("fullname").split(".")[-1] for tc in r.iter("test-case")}
        meta = read_meta(base)
        rows.append(dict(
            base=base, mode=base.rsplit("_", 1)[-1], mt=mt,
            tcc=int(r.get("testcasecount") or 0), total=int(r.get("total") or 0),
            passed=int(r.get("passed") or 0), failed=int(r.get("failed") or 0),
            skipped=int(r.get("skipped") or 0),
            fails=fails, present=present,
            meta=meta, dag=log_dag(base), dmap=dmap, commits=commits))
    rows.sort(key=lambda x: x["mt"])

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
            h = head_at(int(row["mt"]), row["commits"])
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
    L.append(f"dag→타깃 매핑 {len(dmap)}건: " +
             (", ".join(f"`{k}`={short(v)}" for k, v in sorted(dmap.items())) or "**0건 — 타깃 추론이 전부 죽었다**"))
    L.append("")
    L.append("## 실행 대장")
    L.append("")
    L.append("| 시각 | 라벨 | 모드 | HEAD | 활성 타깃 | total | 통과 | 실패 | 건너뜀 | 실패 목록 |")
    L.append("|---|---|---|---|---|---:|---:|---:|---:|---|")
    for r in rows:
        ts = datetime.datetime.fromtimestamp(r["mt"]).strftime("%m-%d %H:%M")
        fl = "—" if not r["fails"] else "<br>".join(r["fails"])
        tcc = "" if r["tcc"] == r["total"] else f" ⚠tcc={r['tcc']}"
        # ★ 실행 도중 재컴파일로 타깃이 바뀌면 '실행 전' 값으로 재해석하면 안 된다.
        if r["meta"].get("target_shifted") == "1":
            r["tsrc"] += "⇄"
        L.append(f"| {ts} | `{r['base'].rsplit('_',1)[0]}` | {r['mode']} | {r['hsrc']}{r['head']} "
                 f"| **{r['tsrc']}{short(r['target'])}** | {r['total']}{tcc} | {r['passed']} "
                 f"| {r['failed']} | {r['skipped']} | {fl} |")
    L.append("")

    # 지금 무엇이 빨간가 + 언제부터인가
    L.append("## 지금 빨간 것 — 그리고 **언제부터**인가")
    L.append("")
    for mode in ("edit", "play"):
        mr = [r for r in rows if r["mode"] == mode]
        if not mr:
            continue
        cur = mr[-1]
        L.append(f"### {mode} — 최신 `{cur['base'].rsplit('_',1)[0]}` "
                 f"({datetime.datetime.fromtimestamp(cur['mt']).strftime('%m-%d %H:%M')}, "
                 f"타깃 {cur['tsrc']}{short(cur['target'])})")
        L.append("")
        if not cur["fails"]:
            L.append("빨강 없음.")
            L.append("")
            continue
        L.append("| 실패 | 마지막으로 **실제로 초록**이던 실행 | 처음 빨개진 실행 | 연속 빨강 |")
        L.append("|---|---|---|---:|")
        for name in cur["fails"]:
            lastgreen = firstred = None
            redstreak = 0
            for r in mr:
                if name not in r["present"]:
                    continue                     # ★ 없던 실행은 초록도 빨강도 아니다
                if name in r["fails"]:
                    if firstred is None:
                        firstred = r
                    redstreak += 1
                else:
                    lastgreen, firstred, redstreak = r, None, 0   # 초록으로 돌아오면 다시 센다
            def tag(r):
                if r is None:
                    return "**한 번도 없다**"
                return (f"`{r['base'].rsplit('_',1)[0]}` "
                        f"{datetime.datetime.fromtimestamp(r['mt']).strftime('%m-%d %H:%M')}")
            L.append(f"| {name} | {tag(lastgreen)} | {tag(firstred)} | {redstreak} |")
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
    import tempfile
    probe = os.path.join(OUTDIR, "ZZZbaselineprobe_edit.meta")
    probexml = os.path.join(OUTDIR, "ZZZbaselineprobe_edit.xml")
    try:
        open(probexml, "w").write(
            '<test-run testcasecount="1" total="1" passed="1" failed="0" skipped="0"></test-run>')
        open(probe, "w").write("target=UNITY_STANDALONE_WIN\nhead=deadbee\ntarget_shifted=1\n")
        rows2, _ = collect()
        hit = [r for r in rows2 if r["base"] == "ZZZbaselineprobe_edit"]
        if hit and hit[0]["tsrc"] == "" and hit[0]["target"] == "UNITY_STANDALONE_WIN" \
                and hit[0]["head"] == "deadbee":
            print("  ✓ 사이드카를 읽어 '잰 값'으로 표시했다 — 이 경로는 죽어 있지 않다")
        else:
            got = (hit[0]["tsrc"], hit[0]["target"], hit[0]["head"]) if hit else None
            print(f"  ✗ 사이드카가 무시됐다({got}) — .meta 를 써도 표에 반영되지 않는다."); rc = 1
    finally:
        for f in (probe, probexml):
            if os.path.exists(f): os.remove(f)

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
