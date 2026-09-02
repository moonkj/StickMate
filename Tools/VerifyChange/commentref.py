#!/usr/bin/env python3
# verify-change 독립 포팅 — CommentReferenceAuditTests 의 .cs 참조 스캐너를 파이썬으로 다시 쓴다.
# 목적 두 가지:
#   (1) "534건 / 위반 3건"을 다른 구현으로 재현되는지 본다
#   (2) ★ 탐지력 실측 — 일부러 심은 거짓 참조를 잡는가
import os, re, sys

CS_NOW  = re.compile(r"[A-Za-z0-9_./*-]*[A-Za-z0-9_]\.cs(?![A-Za-z0-9_])")
CS_OLD  = re.compile(r"[A-Za-z0-9_./*-]*[A-Za-z0-9_]\.cs\b")   # 첫 판(\b) 재현
TESTS_FRAG = "/Scripts/Tests/"

EXTERNAL = {"UniWinCore.cs","UniWindowController.cs","UniWindowControllerEditor.cs",
            "OnDemandRendering.bindings.cs"}
HISTORICAL = {"WindowsFramePacing.cs","MacFramePacing.cs"}
KNOWN_BROKEN = {"Tests/EditMode/VisibleTopEdgeSolverTests.cs",
                "States/IPlannedDwellSource.cs",
                "Tests/*/GlobalTestIsolation.cs"}

def comment_part(line):
    t = line.lstrip()
    if t.startswith("///") or t.startswith("//") or t.startswith("*") or t.startswith("/*"):
        return t
    at = 0
    while True:
        at = line.find("//", at)
        if at < 0: return None
        if at > 0 and line[at-1] == ":":
            at += 2; continue
        return line[at:]

def index_sources(assets):
    idx = {}
    for dp,_,fs in os.walk(assets):
        for f in fs:
            if f.endswith(".cs"):
                idx.setdefault(f, []).append(os.path.join(dp,f).replace("\\","/"))
    return idx

def resolves(ref, idx):
    ref = ref.lstrip("/")
    name = ref.rsplit("/",1)[-1]
    if name not in idx: return False
    if "/" not in ref: return True
    return any(c.endswith("/"+ref) for c in idx[name])

def run(assets, rx):
    idx = index_sources(assets)
    total = 0; viol = []
    for dp,_,fs in os.walk(assets):
        for f in sorted(fs):
            if not f.endswith(".cs"): continue
            p = os.path.join(dp,f).replace("\\","/")
            if TESTS_FRAG in p: continue
            for i, line in enumerate(open(p, encoding="utf-8", errors="replace").read().splitlines(), 1):
                c = comment_part(line)
                if c is None: continue
                for m in rx.finditer(c):
                    ref = m.group(0).lstrip("/")
                    total += 1
                    name = ref.rsplit("/",1)[-1]
                    if name in EXTERNAL or name in HISTORICAL: continue
                    if resolves(ref, idx): continue
                    viol.append((p.split("/Assets/")[-1], i, ref, line.strip()[:110]))
    return total, viol

if __name__ == "__main__":
    assets = sys.argv[1]
    for label, rx in (("지금 판 (?![A-Za-z0-9_])", CS_NOW), ("첫 판 \\b", CS_OLD)):
        total, viol = run(assets, rx)
        unexpected = [v for v in viol if v[2] not in KNOWN_BROKEN]
        print(f"--- {label}: 총 참조 {total}건 / 미해결 {len(viol)}건 / 명부 밖 {len(unexpected)}건")
        for v in viol:
            mark = "명부" if v[2] in KNOWN_BROKEN else "★새것"
            print(f"    [{mark}] {v[0]}:{v[1]}  -> {v[2]}")
