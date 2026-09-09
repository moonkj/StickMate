# -*- coding: utf-8 -*-
"""R4 §1 — 출하 장비의 «실제로 화면에 그려지는» 조형 밀도 실측.

★ 리더 전제의 정정 2건이 이 스크립트의 존재 이유다.
  (정정 1) `equip_head_fedora.asset` 의 `icon`(3조각·26점)은 **죽은 폴백**이다.
           `AccessoryCardIcon.TryBuild` 는 `AccessoryShapeBuilder.Append(surface: Card)` 를 부르고,
           그것이 성공하면 `icon` 을 **한 점도 그리지 않는다**. 실패했을 때만 `BuildIcon`(=icon)이 뜬다.
           AccessoryCardIcon.cs:232 주석: "2026-09-07부터 그 폴백을 정상 경로로 타는 아이템은 0종이다".
  (정정 2) 그러므로 «중절모는 카드와 몸이 같은 데이터라 100% 일치» 도 사실이 아니다.
           중절모(인계본)는 조각 11개인데 그중 **4개가 카드 전용(surfaces:2)**, **7개가 몸 전용(surfaces:1)**이다.
           카드와 몸은 좌표가 서로 다른 두 벌이고, 같아 보이는 이유는 «색 문법과 실루엣 골격이 같아서»다.

여기서 재는 것:
  - 아이템별 조각 수 / 점 수 를 **표면(카드/몸)별로** 나눠서.
  - tone 분포(주색/보조색/그늘/하이라이트/잉크).
  - 채움 조각 수 vs 선 조각 수.
사용:  python3 r4_shipping_census.py            # 표
       python3 r4_shipping_census.py --json     # 기계 판독
"""
import os, re, sys, json, collections

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
HANDOFF = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
MAIN    = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs")

src = open(HANDOFF, encoding="utf-8").read()

# ── 좌표 배열: private static readonly float[] Handoff_xxx = { ... };
arrays = {}
for m in re.finditer(r"static readonly float\[\]\s+(Handoff_\w+)\s*=\s*\{(.*?)\};", src, re.S):
    name, body = m.group(1), m.group(2)
    nums = re.findall(r"-?\d+(?:\.\d+)?f", body)
    arrays[name] = len(nums) // 2

# ── case 블록별 HandoffPiece 호출
CASE = re.compile(r"case\s+(\w+):\s*//\s*(\S+)\s+(\w+)\s+—\s*조각\s*(\d+)")
call = re.compile(
    r'HandoffPiece\(sink, rig, xf, (\w+), "(\w+)", (\w+), loop: (\w+), filled: (\w+), tone: (\d+), '
    r'surfaces: (\d+), strokeMult: ([\d.]+)f, strokeInR: ([\d.]+)f, noStroke: (\w+), alpha: ([\d.]+)f, '
    r'lineAlpha: ([\d.]+)f, underBack: (\d+), layer: (\d+), swayStart: (-?\d+), swayCount: (\d+), bodyFixed: (\w+)\)')

items = []
marks = list(CASE.finditer(src))
for i, m in enumerate(marks):
    start = m.end()
    end = marks[i + 1].start() if i + 1 < len(marks) else len(src)
    block = src[start:end]
    pieces = []
    for c in call.finditer(block):
        (sort, pname, arr, loop, filled, tone, surfaces, smult, sinr,
         nostroke, alpha, lalpha, under, layer, sw0, swn, bfix) = c.groups()
        pieces.append(dict(name=pname, arr=arr, pts=arrays.get(arr, 0),
                           loop=loop == "true", filled=filled == "true", tone=int(tone),
                           surfaces=int(surfaces), strokeMult=float(smult),
                           noStroke=nostroke == "true", alpha=float(alpha), lineAlpha=float(lalpha),
                           layer=int(layer), bodyFixed=bfix == "true"))
    items.append(dict(caseName=m.group(1), ko=m.group(2), key=m.group(3),
                      declared=int(m.group(4)), pieces=pieces))

def on(p, surf):           # surfaces 0 = Body|Card
    return p["surfaces"] == 0 or (p["surfaces"] & surf) != 0

def summarize(pieces, surf):
    sel = [p for p in pieces if on(p, surf)]
    return sel

if "--json" in sys.argv:
    print(json.dumps(items, ensure_ascii=False))
    sys.exit(0)

print("═" * 116)
print("출하 인계본 12종 — 표면별 조형 밀도 (AccessoryShapeBuilder.Handoff.cs 직접 파싱)")
print("═" * 116)
hdr = f"{'아이템':<18}{'선언':>4} │ {'카드조각':>6}{'카드점':>7}{'카드채움':>8} │ {'몸조각':>6}{'몸점':>7}{'몸채움':>7} │ {'공용':>4} │ tone분포(몸)"
print(hdr); print("─" * 116)
rows = []
for it in items:
    card = summarize(it["pieces"], 2)
    body = summarize(it["pieces"], 1)
    both = [p for p in it["pieces"] if p["surfaces"] == 0]
    tones = collections.Counter(p["tone"] for p in body)
    tstr = " ".join(f"t{k}×{v}" for k, v in sorted(tones.items()))
    rows.append((it["key"], len(it["pieces"]), len(card), sum(p["pts"] for p in card),
                 sum(1 for p in card if p["filled"]), len(body), sum(p["pts"] for p in body),
                 sum(1 for p in body if p["filled"]), len(both), tstr))
    print(f"{it['ko']+' '+it['key']:<24}{it['declared']:>4} │ {len(card):>6}{sum(p['pts'] for p in card):>7}"
          f"{sum(1 for p in card if p['filled']):>8} │ {len(body):>6}{sum(p['pts'] for p in body):>7}"
          f"{sum(1 for p in body if p['filled']):>7} │ {len(both):>4} │ {tstr}")
print("─" * 116)
n = len(rows)
print(f"{'평균':<24}{sum(r[1] for r in rows)/n:>4.1f} │ {sum(r[2] for r in rows)/n:>6.2f}{sum(r[3] for r in rows)/n:>7.1f}"
      f"{sum(r[4] for r in rows)/n:>8.2f} │ {sum(r[5] for r in rows)/n:>6.2f}{sum(r[6] for r in rows)/n:>7.1f}"
      f"{sum(r[7] for r in rows)/n:>7.2f} │ {sum(r[8] for r in rows)/n:>4.1f}")
print()
print("표면 코드: 0=Body|Card(공용) 1=Body 2=Card  (Core/AccessoryShapeContract.cs:31-44)")
