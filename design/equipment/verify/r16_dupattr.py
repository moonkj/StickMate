# -*- coding: utf-8 -*-
"""R16 — ★ 재현기(render/icons.js)가 원문과 다르게 그리는 이유의 실험 (design-equipment, 2026-09-05).

icons.js 는 `<path fill="url(#G)" stroke-width="${w}" ... ${at(o)}/>` 처럼 **같은 이름의 SVG 속성을 두 번** 낸다
(기본값 먼저, 옵션 나중). 원문 ItemIcon.dc.html 은 React 라 `{...o}` 가 나중이면 **옵션이 이긴다.**
HTML 파서(innerHTML)는 중복 속성에서 **첫 것만** 남긴다(duplicate-attribute parse error → 나중 것 제거).
⇒ Chrome 이 찍은 A/C 열(R15) · icons_16.png 는 원문의 `fill: A` 23건 · 획 배수 11건을 버린 그림이다(r16_model §6-1).

이 스크립트는 그 사실을 **두 픽셀로** 증명한다:
    rect  fill="#FF0000" fill="#00FF00"        → 빨강이면 첫 속성 승
    line  stroke-width="2" stroke-width="20"   → 세로 두께 2px 면 첫 속성 승
양성 대조: 속성을 하나만 낸 대조 rect/line 이 기대색·기대두께를 내는지 같이 찍는다(찍는 경로가 살아 있는가).

    python3 r16_dupattr.py
"""
import os, subprocess, sys, tempfile
from PIL import Image

CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
HTML = """<!DOCTYPE html><html><body style="margin:0;background:#000"><div id="g"></div><script>
document.getElementById('g').innerHTML = `<svg width="400" height="60" viewBox="0 0 400 60">
<rect x="0" y="0" width="60" height="60" fill="#FF0000" fill="#00FF00"/>
<line x1="80" y1="30" x2="180" y2="30" stroke="#FFFFFF" stroke-width="2" stroke-width="20"/>
<rect x="200" y="0" width="60" height="60" fill="#00FF00"/>
<line x1="280" y1="30" x2="380" y2="30" stroke="#FFFFFF" stroke-width="20"/>
</svg>`;</script></body></html>"""

def main():
    d = tempfile.mkdtemp(prefix="r16dup_")
    hp = os.path.join(d, "dup.html"); open(hp, "w").write(HTML)
    png = os.path.join(d, "dup.png")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--window-size=400,60",
                    "--screenshot=" + png, "file://" + hp], check=True, capture_output=True)
    im = Image.open(png).convert("RGB")
    def thick(x): return sum(1 for y in range(60) if sum(im.getpixel((x, y))) > 300)
    dup_rect, dup_line = im.getpixel((30, 30)), thick(130)
    ctl_rect, ctl_line = im.getpixel((230, 30)), thick(330)
    print("중복 rect (30,30) = %s  → %s" % (dup_rect, "첫 fill(#FF0000) 승" if dup_rect[0] > 200 and dup_rect[1] < 50 else "나중 fill 승"))
    print("중복 line 세로 두께 = %d px → %s" % (dup_line, "첫 stroke-width(2) 승" if dup_line <= 3 else "나중 stroke-width(20) 승"))
    print("양성 대조 rect (230,30) = %s (기대 초록) · line 두께 = %d px (기대 20)" % (ctl_rect, ctl_line))
    ok = dup_rect[0] > 200 and dup_rect[1] < 50 and dup_line <= 3 and ctl_rect[1] > 200 and 18 <= ctl_line <= 22
    print("판정: %s" % ("HTML 파서는 중복 속성에서 첫 것을 남긴다 — icons.js 는 원문 옵션을 버린다" if ok else "★ 기대와 다르다 — r16_model ATTR_SEMANTICS 근거를 다시 봐야 한다"))
    return 0 if ok else 1

if __name__ == "__main__":
    sys.exit(main())
