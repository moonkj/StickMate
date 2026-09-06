#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R27 — 부채꼴 심볼 5종의 «프로덕션 거울». (design-art, 2026-09-06)

★ 이 파일은 좌표를 <베끼지 않는다>. `GearRadialMenuWidget.cs` / `UiChrome.cs`를 **읽어서 판다**.
   R26 라운드의 `design/fanmenu/verify/fanglyph.py`는 사람이 손으로 옮겨 적은 거울이었고,
   그래서 «거울이 낡았는가»를 매번 사람이 대조해야 했다. 여기서는 그 대조가 원리상 불필요하다 —
   상수 하나가 프로덕션에서 바뀌면 이 파서의 결과가 같이 바뀐다.

   파싱 대상은 세 가지뿐이다:
     · `private const float|int NAME = 값;`
     · `private static readonly Vector2 NAME = 식;`
     · `private static readonly Vector2[] NAME = { 식, 식, … };`  또는 `= BuildArcPath(…);`
   식은 C# 부분집합(숫자 리터럴 / 식별자 / + − * / 괄호 / `new Vector2(a,b)` / `Vector2.zero` /
   `Polar(도, r)` / `BuildArcPath(중심, r, 시작, 끝, 분할)`)만 지원한다. 그 밖의 문법을 만나면
   **조용히 넘어가지 않고 예외로 죽는다** — 조용한 실패가 곧 낡은 거울이다.

프로덕션 `.cs`는 **읽기만 한다**(design-art 규약: 프로덕션 .cs 수정 금지).
"""
import ast
import math
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FAN_CS = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/GearRadialMenuWidget.cs")
CHROME_CS = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/UiChrome.cs")


class Vec(tuple):
    """C# Vector2의 최소 대역. + − 스칼라곱만 쓴다."""

    def __new__(cls, x, y):
        return super().__new__(cls, (float(x), float(y)))

    @property
    def x(self):
        return self[0]

    @property
    def y(self):
        return self[1]

    def __add__(self, o):
        return Vec(self[0] + o[0], self[1] + o[1])

    def __sub__(self, o):
        return Vec(self[0] - o[0], self[1] - o[1])

    def __mul__(self, s):
        return Vec(self[0] * s, self[1] * s)

    __rmul__ = __mul__

    def __repr__(self):
        return f"({self[0]:+.3f},{self[1]:+.3f})"


def _polar(deg, r):
    a = math.radians(deg)
    return Vec(math.cos(a) * r, math.sin(a) * r)


def _build_arc_path(center, radius, a0, a1, segments):
    segments = max(1, int(segments))
    return [center + _polar(a0 + (a1 - a0) * (i / segments), radius) for i in range(segments + 1)]


class CsMirror:
    """한 .cs 파일에서 상수와 Vector2[] 를 판다."""

    _NUM = re.compile(r"(?<![\w.])(\d+(?:\.\d+)?)[fF](?![\w])")
    _SCALAR = re.compile(
        r"(?:private|public|internal|protected)\s+(?:static\s+readonly|const)\s+(float|int)\s+(\w+)\s*=\s*([^;]+);")
    _VEC = re.compile(
        r"private\s+static\s+readonly\s+Vector2\s+(\w+)\s*=\s*([^;]+);")
    _ARR = re.compile(
        r"private\s+static\s+readonly\s+Vector2\[\]\s+(\w+)\s*=\s*(\{.*?\}|[^;]+?);", re.S)

    def __init__(self, path):
        self.path = path
        with open(path, encoding="utf-8") as f:
            self.src = f.read()
        self.env = {"Polar": _polar, "BuildArcPath": _build_arc_path, "Vector2": None}
        self.consts, self.vectors, self.arrays = {}, {}, {}
        self._parse()

    # ---- C# 부분집합 → 파이썬 ----
    def _py(self, expr):
        e = expr.strip()
        e = re.sub(r"//.*", "", e)
        e = re.sub(r"/\*.*?\*/", "", e, flags=re.S)
        e = self._NUM.sub(r"\1", e)                     # 12.5f → 12.5
        e = e.replace("Vector2.zero", "Vec(0,0)")
        e = re.sub(r"\bnew\s+Vector2\s*\(", "Vec(", e)
        e = re.sub(r"\bMathf\.", "math.", e)
        return e

    def _eval(self, expr):
        py = self._py(expr)
        # 지원하지 않는 문법은 여기서 죽는다(조용한 실패 금지).
        node = ast.parse(py, mode="eval")
        for n in ast.walk(node):
            if isinstance(n, ast.Call) and not (
                    isinstance(n.func, ast.Name) and n.func.id in ("Vec", "Polar", "BuildArcPath")):
                raise ValueError(f"{self.path}: 지원하지 않는 호출 «{ast.dump(n.func)}» in {expr!r}")
        return eval(compile(node, "<mirror>", "eval"),
                    {"Vec": Vec, "math": math, "__builtins__": {}}, dict(self.env))

    def _parse(self):
        for kind, name, expr in self._SCALAR.findall(self.src):
            try:
                v = self._eval(expr)
            except Exception:
                continue                                 # 문자열/열거형 등 — 우리 관심 밖
            if isinstance(v, (int, float)):
                self.consts[name] = float(v)
                self.env[name] = float(v)
        for name, expr in self._VEC.findall(self.src):
            self.vectors[name] = self._eval(expr)
            self.env[name] = self.vectors[name]
        for name, body in self._ARR.findall(self.src):
            b = body.strip()
            if b.startswith("{"):
                inner = b[1:-1]
                pts = [self._eval(t) for t in _split_top(inner) if t.strip()]
            else:
                pts = self._eval(b)
            self.arrays[name] = [Vec(p[0], p[1]) for p in pts]

    def need(self, name):
        if name not in self.consts:
            raise KeyError(f"{os.path.basename(self.path)} 에서 상수 «{name}» 를 못 찾았다 — 거울 실패")
        return self.consts[name]

    def path_of(self, name):
        if name not in self.arrays:
            raise KeyError(f"{os.path.basename(self.path)} 에서 경로 «{name}» 를 못 찾았다 — 거울 실패")
        return self.arrays[name]


def _split_top(s):
    """중괄호/괄호 깊이를 세면서 최상위 콤마로 자른다."""
    out, depth, cur = [], 0, []
    for ch in s:
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == "," and depth == 0:
            out.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
    out.append("".join(cur))
    return [t for t in out if t.strip()]


# ─────────────────────────── 조각(도형) 모형 ───────────────────────────
# alpha(p) = clamp01(0.5 + sdf(p)/f).  sdf = 코어 경계까지의 부호 거리(안쪽이 +), f = EdgeFeather.
# 이 식은 UiChrome.Capsule()/CircleSprite()의 텍셀 굽기에서 그대로 유도된다(r27_shrink_fg3.py §0 교정6).

class Piece:
    def __init__(self, name, thickness):
        self.name, self.t = name, float(thickness)

    def sdf(self, X, Y):
        raise NotImplementedError

    def alpha(self, X, Y, feather):
        import numpy as np
        return np.clip(0.5 + self.sdf(X, Y) / feather, 0.0, 1.0)


class Poly(Piece):
    """꺾은선 한 줄(UiChrome.AddPolyline) — 선분마다 둥근 캡슐."""

    def __init__(self, name, points, thickness):
        super().__init__(name, thickness)
        self.pts = [Vec(p[0], p[1]) for p in points]

    def sdf(self, X, Y):
        import numpy as np
        best = None
        for i in range(1, len(self.pts)):
            a, b = self.pts[i - 1], self.pts[i]
            vx, vy = b[0] - a[0], b[1] - a[1]
            wx, wy = X - a[0], Y - a[1]
            L2 = vx * vx + vy * vy
            tt = np.clip((wx * vx + wy * vy) / L2, 0.0, 1.0) if L2 > 0 else 0.0
            d = np.hypot(wx - tt * vx, wy - tt * vy)
            best = d if best is None else np.minimum(best, d)
        return self.t / 2.0 - best


class Capsule(Piece):
    """UiChrome.AddStroke 한 획(길이 L·두께 t·각 a·중심 c). 보이는 총 길이가 L이다."""

    def __init__(self, name, length, thickness, angle_deg, center):
        super().__init__(name, thickness)
        h = max(0.0, length / 2.0 - thickness / 2.0)
        a = math.radians(angle_deg)
        self.a = Vec(center[0] - math.cos(a) * h, center[1] - math.sin(a) * h)
        self.b = Vec(center[0] + math.cos(a) * h, center[1] + math.sin(a) * h)

    def sdf(self, X, Y):
        import numpy as np
        vx, vy = self.b[0] - self.a[0], self.b[1] - self.a[1]
        wx, wy = X - self.a[0], Y - self.a[1]
        L2 = vx * vx + vy * vy
        tt = np.clip((wx * vx + wy * vy) / L2, 0.0, 1.0) if L2 > 0 else 0.0
        return self.t / 2.0 - np.hypot(wx - tt * vx, wy - tt * vy)


class Disc(Piece):
    """채운 원반 — AddCircle(ringThickness 생략)."""

    def __init__(self, name, diameter, center=(0.0, 0.0)):
        super().__init__(name, 0.0)
        self.d, self.c = float(diameter), Vec(center[0], center[1])

    def sdf(self, X, Y):
        import numpy as np
        return self.d / 2.0 - np.hypot(X - self.c[0], Y - self.c[1])


class Ring(Piece):
    """링 — AddCircle(ringThickness>0). 코어는 r ∈ [d/2 − t, d/2].
    <para>틈(gap)은 <b>스프라이트가 아니라 메시</b>로 잘린다(Image.Type.Filled/Radial360) —
    즉 <b>램프가 없는 하드 엣지</b>다. 그래서 틈 가장자리 쪽 골은 모형보다 오히려 넓다.</para>"""

    def __init__(self, name, diameter, thickness, gap_deg=0.0, gap_center_deg=90.0, center=(0.0, 0.0)):
        super().__init__(name, thickness)
        self.d, self.gap, self.gc = float(diameter), float(gap_deg), float(gap_center_deg)
        self.c = Vec(center[0], center[1])

    def _cut(self, X, Y):
        import numpy as np
        if self.gap <= 0:
            return np.zeros_like(X, dtype=bool)
        th = np.degrees(np.arctan2(Y - self.c[1], X - self.c[0])) % 360.0
        g0 = (self.gc - self.gap / 2) % 360.0
        g1 = (self.gc + self.gap / 2) % 360.0
        return ((th >= g0) & (th <= g1)) if g0 < g1 else ((th >= g0) | (th <= g1))

    def sdf(self, X, Y):
        import numpy as np
        r = np.hypot(X - self.c[0], Y - self.c[1])
        s = np.minimum(self.d / 2.0 - r, r - (self.d / 2.0 - self.t))
        return np.where(self._cut(X, Y), -1e3, s)

    def alpha(self, X, Y, feather):
        import numpy as np
        a = np.clip(0.5 + self.sdf(X, Y) / feather, 0.0, 1.0)
        return np.where(self._cut(X, Y), 0.0, a)


# ─────────────────────────── 5종 조립 (프로덕션 파싱) ───────────────────────────
def load_glyphs():
    fan = CsMirror(FAN_CS)
    chrome = CsMirror(CHROME_CS)

    W = fan.need("SymbolStroke")
    G1 = fan.need("SymbolStrokeDetail")
    G2 = fan.need("SymbolStrokeHeavy")
    feather = chrome.need("EdgeFeather")

    stopwatch = [
        Poly("CrownStem", fan.path_of("StopwatchCrownStemPath"), W),
        Capsule("CrownCap", 6.0, G2, 0.0, Vec(0.0, 12.4)),      # AddStroke 인자(코드 리터럴)
        Ring("Ring", fan.need("StopwatchRingDiameterPoints"), W),
        Poly("MinuteHand", fan.path_of("StopwatchMinuteHandPath"), W),
        Poly("HourHand", fan.path_of("StopwatchHourHandPath"), W),
    ]
    stickman = [
        Disc("IconHead", fan.need("StickHeadDiameterPoints"), Vec(0.0, fan.need("StickHeadCenterY"))),
        Poly("IconSpine", fan.path_of("StickSpinePath"), W),
        Poly("IconArmL", fan.path_of("StickArmLPath"), W),
        Poly("IconArmR", fan.path_of("StickArmRPath"), W),
        Poly("IconLegL", fan.path_of("StickLegLPath"), W),
        Poly("IconLegR", fan.path_of("StickLegRPath"), W),
    ]
    checklist = [
        Poly("Box", fan.path_of("ChecklistBoxPath"), G1),
        Poly("Line0", fan.path_of("ChecklistLine0Path"), W),
        Poly("Check", fan.path_of("ChecklistCheckPath"), W),
        Poly("Line1", fan.path_of("ChecklistLine1Path"), W),
    ]
    megaphone = [
        Poly("Horn", fan.path_of("MegaphoneHornPath"), W),
        Poly("Handle", fan.path_of("MegaphoneHandlePath"), W),
        Poly("Wave", fan.path_of("MegaphoneWavePath"), W),
    ]
    power = [
        Ring("PowerRing", fan.need("PowerRingDiameterPoints"), W,
             gap_deg=fan.need("PowerGapDegrees"), gap_center_deg=90.0),
        Poly("PowerStem", fan.path_of("PowerStemPath"), W),
    ]

    glyphs = {
        "① 집중(스톱워치)": stopwatch,
        "② 캐릭터(스틱맨)": stickman,
        "③ 할일(체크리스트)": checklist,
        "④ 행동명령(확성기)": megaphone,
        "⑤ 종료(전원)": power,
    }
    consts = dict(
        W=W, G1=G1, G2=G2, feather=feather,
        button_d=fan.need("ButtonDiameterPoints"),
        shrunk_d=fan.need("ShrunkDiameterPoints"),
        symbol_box=fan.need("SymbolBoxPoints"),
        field_r=fan.need("SymbolFieldRadiusPoints"),
        power_gap=fan.need("PowerGapDegrees"),
        power_d=fan.need("PowerRingDiameterPoints"),
        stopwatch_d=fan.need("StopwatchRingDiameterPoints"),
        border_t=1.2,          # BuildButton: AddCircle(Border, …, 1.2f) — 리터럴 인자
    )
    return glyphs, consts, fan, chrome
