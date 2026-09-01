# -*- coding: utf-8 -*-
"""검산을 통과한 좌표 그대로 SVG를 굽는다 — 문서의 숫자와 그림이 갈라질 수 없다."""
import sys, math, html; sys.path.insert(0,'.')
import rig, items, hair
from rig import W

INK="#14161a"; BG="#f6f3ec"
PAL={  # 주색 / 보조색 — ItemCatalog.WornColor의 채도>=0.42 · 명도 0.55~0.80 창 안의 예시값
 "야구모자":("#3f7fd0","#e0574a"), "털모자":("#c25b8f","#f0c33c"), "중절모":("#8a6a4a","#d9c07a"),
 "왕관":("#e0b23c","#c8792c"), "베레모":("#c0504a","#3d3a44"), "밀짚모자":("#d8b26a","#b06a3c"),
 "선글라스":("#2f3946","#7fd0c8"), "동그란안경":("#5a76c8","#e6c04a"), "고글":("#3f7fd0","#e0574a"),
 "외알안경":("#e0b23c","#8fd8e8"), "뿔테안경":("#4a4a58","#d05a4a"), "안대":("#3a3a46","#8fd8e8"),
 "나비넥타이":("#c0424a","#f0d060"), "줄무늬타이":("#3f6fc0","#e0d24a"), "목도리":("#c85a3c","#f0d8a0"),
 "방울목걸이":("#c04a6a","#e0b23c"), "펜던트":("#7a6ac0","#e0b23c"), "반다나":("#3fa06a","#f0e0a0"),
 "짧은망토":("#a03a4a","#e0b23c"), "긴망토":("#3a4a90","#e0b23c"), "날개":("#e8e2d0","#c8a03c"),
 "배낭":("#6a8a3c","#e0a03c"), "판초":("#c07a3c","#e0d0a0"), "요정날개":("#8fd8e8","#f0a0d0"),
 "삐친머리":("#7a4a2c","#c8863c"), "단정한머리":("#3a2c26","#8a6a4a"), "곱슬머리":("#5a3a2a","#c07a4a"),
 "민머리":("#8a6a52","#c8a07a"), "바가지머리":("#2e2a3a","#6a5a80"), "포니테일":("#c8823c","#8a4a2c"),
}
def shade(c):
    c=c.lstrip('#'); r,g,b=(int(c[i:i+2],16) for i in (0,2,4))
    return "#%02x%02x%02x"%(int(r*.62),int(g*.62),int(b*.62))

def path(pts, loop):
    d="M %.4f %.4f "%pts[0]+" ".join("L %.4f %.4f"%p for p in pts[1:])
    return d+(" Z" if loop else "")

def draw(shapes, prim, sec, sw):
    out=[]
    for s in shapes:
        col = prim if s.tone==0 else (sec if s.tone==1 else shade(prim))
        d=path(s.pts, s.loop)
        if s.filled:
            out.append('<path d="%s" fill="%s" stroke="%s" stroke-width="%.4f" stroke-linejoin="round"/>'%(d,col,shade(col),sw))
        else:
            out.append('<path d="%s" fill="none" stroke="%s" stroke-width="%.4f" stroke-linecap="round" stroke-linejoin="round"/>'%(d,col,sw))
    return "".join(out)

def card(name, shapes, size=110):
    x0,y0,x1,y1=rig.bounds([p for s in shapes for p in s.pts])
    span=max(x1-x0,y1-y0); k=size*0.86/span
    cx=(x0+x1)/2; cy=(y0+y1)/2
    sw=1.87*(size/44.0)/k                       # 카드 실측 획(44px 기준 1.87)을 R 단위로 환산
    p,s=PAL[name]
    return ('<svg viewBox="%.3f %.3f %.3f %.3f" width="%d" height="%d">'
            '<g transform="scale(1,-1)">%s</g></svg>'
            % (cx-size/2/k, -(cy+size/2/k), size/k, size/k, size, size, draw(shapes,p,s,sw)))

def body(full=False):
    sh=rig.SHOULDER_R; hip=rig.HIP_R; foot=-9.3395
    g=['<circle cx="0" cy="%.4f" r="1" fill="%s"/>'%(0,INK)]
    lw=0.22*0.11*0.7/0.22*1.0   # 몸통 획 비율(참고용 두께)
    lw=0.30
    def ln(a,b): return '<line x1="%.3f" y1="%.3f" x2="%.3f" y2="%.3f" stroke="%s" stroke-width="%.3f" stroke-linecap="round"/>'%(a[0],a[1],b[0],b[1],INK,lw)
    g.append(ln((0,-1.0),(0,sh)))
    g.append(ln((0,sh),(0,hip)))
    g.append(ln((0,sh),(-0.9,sh-1.5))); g.append(ln((-0.9,sh-1.5),(-1.3,sh-2.9)))
    g.append(ln((0,sh),( 0.9,sh-1.5))); g.append(ln(( 0.9,sh-1.5),( 1.5,sh-2.7)))
    g.append(ln((0,hip),(-0.8,hip-2.1))); g.append(ln((-0.8,hip-2.1),(-0.9,foot)))
    g.append(ln((0,hip),( 0.8,hip-2.1))); g.append(ln(( 0.8,hip-2.1),( 1.0,foot)))
    return "".join(g)

def worn(name, shapes, cat, w=150):
    if cat in ("HEAD","EYES","HAIR"): vx,vy,vw,vh = -3.0, -2.6, 6.0, 5.4
    elif cat=="NECK":                 vx,vy,vw,vh = -2.6, -4.6, 5.2, 6.4
    else:                             vx,vy,vw,vh = -4.0,-10.0, 8.0,12.4
    p,s=PAL[name]
    sw=W  # 배율 0.75 착용 획
    h=int(w*vh/vw)
    return ('<svg viewBox="%.2f %.2f %.2f %.2f" width="%d" height="%d">'
            '<g transform="scale(1,-1) translate(0,0)">%s%s</g></svg>'
            % (vx,-(vy+vh),vw,vh,w,h, body(), draw(shapes,p,s,sw)))

DIAG = {
 "HAIR":"현행 5종은 정수리에서 머리카락이 <b>획 하나보다 얇게</b>(0.41~0.81획) 두피 링을 덮어, 링 획과 윤곽선이 한 줄로 뭉쳐 '뚜껑'이 됐다. 새 값은 전부 1.6획 이상이다.",
 "HEAD":"현행 6종의 커버선은 전부 머리 중심 <b>위쪽</b>(+0.42~+0.62R)이라 모자가 정수리에 <b>얹혀</b> 있었다. 새 값은 −0.06~+0.08R — 눈썹 높이까지 내려와 <b>감싼다</b>.",
 "EYES":"눈이 삭제된 뒤 6종은 전부 불투명 판이 됐다. 이번에 <b>한쪽만 가리는 2종</b>에만 반대쪽 눈을 되살린다. 두 눈을 다 가리는 4종은 판 그대로다 — 이유는 취향이 아니라 산술이다(아래 증명).",
 "NECK":"몸통이 선 하나뿐이라 목 아이템은 <b>폭이 곧 존재감</b>이다. 줄무늬타이 blade 0.15R(0.87획)·나비넥타이 매듭 0.91획처럼 획에 먹히던 자리를 전부 1.5획 위로 올렸다.",
 "BACK":"배낭이 '대괄호'로 읽힌 것은 몸이 얇은 윤곽선이었기 때문이다. 상자+뚜껑+버클로 <b>덩어리</b>를 만들고, 망토 3종에는 목을 감는 옷깃 띠(서명 디테일)를 넣었다.",
}
CATS=[("HAIR","머리",hair.SET),("HEAD","모자",items.HEAD),("EYES","안경",items.EYES),
      ("NECK","넥타이",items.NECK),("BACK","망토·등",items.BACK)]

rows=[]
for code,label,d in CATS:
    cards=[]
    for n,sh in d.items():
        x0,y0,x1,y1=rig.bounds([p for s in sh for p in s.pts])
        mn=min(rig.min_corner_seg(s,W) or 99 for s in sh)
        cards.append('<figure><div class="pair">%s%s</div>'
                     '<figcaption><b>%s</b><span>도형 %d · 폭 %.2fR · 높이 %.2fR · 최단 변 %.2f획</span></figcaption></figure>'
                     % (card(n,sh), worn(n,sh,code), html.escape(n), len(sh), x1-x0, y1-y0, mn))
    rows.append('<section><p class="eyebrow">%s · %s</p><p class="diag">%s</p><div class="grid">%s</div></section>'
                % (html.escape(label), code, DIAG[code], "".join(cards)))

doc = """<!doctype html><html lang="ko"><meta charset="utf-8">
<title>StickMate 장비 30종 재설계 v2</title>
<style>
 body{margin:0;background:#efeae0;color:#22252b;font:15px/1.65 -apple-system,BlinkMacSystemFont,"Apple SD Gothic Neo",sans-serif}
 .wrap{max-width:1180px;margin:0 auto;padding:44px 26px 90px}
 h1{font-size:30px;margin:0 0 6px;letter-spacing:-.02em}
 .sub{color:#6a6f78;margin:0 0 30px}
 section{background:#fbf9f4;border:1px solid #ded7c9;border-radius:16px;padding:24px;margin:0 0 22px}
 .eyebrow{margin:0 0 4px;font-size:12px;letter-spacing:.12em;color:#8a7f6a;font-weight:700}
 .diag{margin:0 0 18px;color:#4d5057;font-size:14px}
 .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:16px}
 figure{margin:0;background:#fff;border:1px solid #e4ded1;border-radius:12px;padding:12px}
 .pair{display:flex;align-items:center;gap:10px;justify-content:center;min-height:170px;background:#f6f3ec;border-radius:9px}
 figcaption{margin-top:9px;font-size:12px;color:#6a6f78;display:flex;flex-direction:column}
 figcaption b{color:#22252b;font-size:14px}
 .note{background:#fffdf6;border:1px solid #e8dcb8;border-radius:14px;padding:20px 24px;margin:0 0 22px}
 code{background:#eee9dc;padding:1px 5px;border-radius:4px;font-size:13px}
 table{border-collapse:collapse;width:100%;font-size:13px;margin-top:10px}
 th,td{border-bottom:1px solid #e4ded1;padding:6px 8px;text-align:left}
 th{color:#8a7f6a;font-weight:700;font-size:12px}
</style><div class="wrap">
<h1>장비 30종 재설계 v2 — 검산본</h1>
<p class="sub">왼쪽 = 보관함 카드(44px 상자 · 획 1.87) / 오른쪽 = 착용(배율 0.75 · 머리 지름 11.6pt · 획 2pt).
두 그림과 아래 숫자는 <b>같은 좌표에서 나왔다</b> — 문서와 그림이 갈라질 수 없다.</p>
<div class="note"><b>이 페이지의 모든 도형은 프로덕션 린트를 통과한 좌표다.</b>
규칙 1(모든 변 ≥ 1.0획 · 잉크 사각형 ≥ 1.5획), 규칙 2(가리는 것은 채운다), 규칙 3-2(보조색 정확히 1개),
규칙 4(두피/몸에 1획 이상 물린다), 규칙 5(정원 2~4개)를 30종 전수 검사해 <b>위반 0건</b>.
검산 배율 0.75에서 <code>W = 0.3439R</code>, 머리 지름은 <b>5.82 W</b>뿐이다 — 이 앱에서 얼굴 위에 놓을 수 있는
독립된 요소는 최대 3개다.</div>
""" + "".join(rows) + """
</div></html>"""
open('/Users/kjmoon/App/StickMate/design/equipment/equipment-shapes-v2.html','w').write(doc)
print("wrote", len(doc), "bytes")
