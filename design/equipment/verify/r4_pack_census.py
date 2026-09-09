# -*- coding: utf-8 -*-
"""R4 §1 — R2/R3 팩 방출본의 조형 밀도 실측. wornShapes YAML 을 직접 읽는다(사본을 만들지 않는다).

terms 스트림 문법(AccessoryDefSO.cs 참고):
  stream := pointCount , point*pointCount
  point  := sum(x), sum(y)          # swingDegrees == 0 이면 기울인 벡터가 없다
  sum    := termCount , term*termCount
  term   := basis, gate, trig, coefCount, coef*coefCount
여기서는 **맨 앞 pointCount 하나만** 읽으면 점 수를 알 수 있다.
"""
import os, sys, glob, re, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))

def parse(path):
    shapes = []
    cur = None
    terms = None
    for raw in open(path, encoding="utf-8"):
        line = raw.rstrip("\n")
        m = re.match(r"\s*- name:\s*(\S+)", line)
        if m:
            if cur: shapes.append(cur)
            cur = dict(name=m.group(1)); terms = None
            continue
        if cur is None: continue
        m = re.match(r"\s*(\w+):\s*(-?[\d.]+)\s*$", line)
        if m:
            k, v = m.group(1), m.group(2)
            if k == "terms": continue
            cur[k] = float(v) if "." in v else int(v)
            continue
        if re.match(r"\s*terms:\s*$", line):
            terms = []
            cur["_terms"] = terms
            continue
        m = re.match(r"\s*-\s*(-?[\d.eE+]+)\s*$", line)
        if m and terms is not None:
            terms.append(float(m.group(1)))
    if cur: shapes.append(cur)
    for s in shapes:
        t = s.get("_terms") or []
        s["pts"] = int(round(t[0])) if t else 0
        s.pop("_terms", None)
    return shapes

def census(dirname, label):
    rows = []
    for p in sorted(glob.glob(os.path.join(ROOT, "design/equipment", dirname, "*.wornShapes.yaml"))):
        key = os.path.basename(p).replace(".wornShapes.yaml", "")
        sh = parse(p)
        tones = collections.Counter(s.get("tone", 0) for s in sh)
        rows.append((key, len(sh), sum(s["pts"] for s in sh),
                     sum(1 for s in sh if s.get("filled")), tones,
                     [s["pts"] for s in sh]))
    print("═" * 112)
    print(f"{label}  ({dirname})")
    print("═" * 112)
    print(f"{'아이템':<34}{'조각':>4}{'점':>6}{'점/조각':>8}{'채움':>5}  {'조각별 점수':<24} tone분포")
    print("─" * 112)
    for key, n, pts, fill, tones, each in rows:
        tstr = " ".join(f"t{k}×{v}" for k, v in sorted(tones.items()))
        print(f"{key:<34}{n:>4}{pts:>6}{(pts/n if n else 0):>8.1f}{fill:>5}  {str(each):<24} {tstr}")
    print("─" * 112)
    N = len(rows)
    tot_n = sum(r[1] for r in rows); tot_p = sum(r[2] for r in rows)
    print(f"{'평균':<34}{tot_n/N:>4.1f}{tot_p/N:>6.1f}{tot_p/tot_n:>8.1f}{sum(r[3] for r in rows)/N:>5.1f}")
    return rows

if __name__ == "__main__":
    for d, l in (("pack_detail_r2", "팩 12종 R2 방출본"), ("pack_detail_r3", "팩 12종 R3 방출본")):
        if os.path.isdir(os.path.join(ROOT, "design/equipment", d)):
            census(d, l); print()
