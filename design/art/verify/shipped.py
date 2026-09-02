# -*- coding: utf-8 -*-
"""출하된 색을 **산출물에서 직접 읽는다** — 문서를 베끼지 않는다.

  · Resources/Items/*.asset  의 아이템 색(주색 tone 0 / 보조색 tone 1)
  · UiChrome.cs 의 팔레트 토큰 (float 3튜플 -> 8bit)

★ 손으로 베낀 사본은 원본을 고치는 순간 거짓 초록을 낸다(design-equipment가 cards12.py에서
  같은 이유로 사본을 버렸다). 그래서 파싱한다.
"""
import glob
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ITEMS = os.path.join(ROOT, "Assets/_Project/Resources/Items")
UICHROME = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/UiChrome.cs")

_COLOR_RE = re.compile(
    r"color:\s*\{r:\s*([0-9.eE+-]+),\s*g:\s*([0-9.eE+-]+),\s*b:\s*([0-9.eE+-]+),\s*a:\s*([0-9.eE+-]+)\}")
_TONE_RE = re.compile(r"^\s*tone:\s*(\d+)\s*$")


def _f2b(x):
    return int(round(float(x) * 255))


def item_colors():
    """{아이템id: {'tones': {tone: set(rgb)}, 'alphas': set()}}"""
    out = {}
    for path in sorted(glob.glob(os.path.join(ITEMS, "*.asset"))):
        name = os.path.splitext(os.path.basename(path))[0]
        txt = open(path, encoding="utf-8").read()
        lines = txt.splitlines()
        rec = {"tones": {}, "alphas": set()}
        pending = None
        for ln in lines:
            m = _COLOR_RE.search(ln)
            if m:
                pending = (tuple(_f2b(m.group(i)) for i in (1, 2, 3)), float(m.group(4)))
                continue
            t = _TONE_RE.match(ln)
            if t and pending is not None:
                tone = int(t.group(1))
                rec["tones"].setdefault(tone, set()).add(pending[0])
                rec["alphas"].add(pending[1])
                pending = None
        if rec["tones"]:
            out[name] = rec
    return out


def uichrome_tokens():
    """{토큰명: (r,g,b,a)} — alpha 1인 것만 색으로 취급."""
    txt = open(UICHROME, encoding="utf-8").read()
    pat = re.compile(
        r"public static readonly Color (\w+)\s*=\s*new Color\(([0-9.f]+),\s*([0-9.f]+),\s*([0-9.f]+),\s*([0-9.f]+)\)")
    out = {}
    for m in pat.finditer(txt):
        vals = [float(m.group(i).rstrip("f")) for i in (2, 3, 4, 5)]
        out[m.group(1)] = (tuple(int(round(v * 255)) for v in vals[:3]), vals[3])
    return out


if __name__ == "__main__":
    import colorlab as C
    items = item_colors()
    print(f"아이템 에셋 {len(items)}개")
    prim, sec = set(), set()
    for k, v in items.items():
        for c in v["tones"].get(0, ()):
            prim.add(c)
        for c in v["tones"].get(1, ()):
            sec.add(c)
    print(f"  주색(tone0) 고유 {len(prim)}종, 보조색(tone1) 고유 {len(sec)}종")
    for c in sorted(prim, key=C.hue_deg):
        print(f"    주 {C.rgb2hex(c)}  H={C.hue_deg(c):6.1f}  -> worn {C.rgb2hex(C.worn(c))}")
    for c in sorted(sec, key=C.hue_deg):
        print(f"    보 {C.rgb2hex(c)}  H={C.hue_deg(c):6.1f}  -> worn {C.rgb2hex(C.worn(c))}")
    tk = uichrome_tokens()
    print(f"\nUiChrome 색 토큰 {len(tk)}종 (불투명만 표시)")
    for k, (c, a) in tk.items():
        if a == 1.0:
            print(f"    {k:26s} {C.rgb2hex(c)}")
