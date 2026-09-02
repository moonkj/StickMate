# -*- coding: utf-8 -*-
"""R3 — 래스터/EDT 자(尺).  ★ headroom.py 의 **구간 대수**와 코드를 공유하지 않는다.

왜 따로 쓰는가 (TEAM.md §"생성기와 검사기가 같이 틀린다")
------------------------------------------------------
headroom.py 는 스캔라인 **구간 대수**(다각형 교차점 정렬 + 캡슐 반평면)로 면적을 낸다.
여기는 **점 소속 판정**(crossing number + 점-선분 거리)을 numpy 로 벡터화한 완전히 다른 구현이다.
둘이 같은 답을 내면 서로의 양성 대조가 된다. 한쪽만 쓰면 그 한쪽의 버그를 아무도 못 본다.

두께는 **정확 유클리드 거리변환**(Felzenszwalb 1D 분리형)으로 잰다 — 팀의 확립된 EDT 방법.
"""
import math
import numpy as np


# ---------------------------------------------------------------- 마스크
def mask_of(shapes, w, x0, x1, y0, y1, h):
    """도형들의 잉크(채움 + 획 W/2 팽창)를 [x0,x1]x[y0,y1] 격자에 찍는다.
    ★ 점 소속 판정. headroom.ink_spans 와 코드를 한 줄도 공유하지 않는다."""
    nx = int(round((x1 - x0) / h)) + 1
    ny = int(round((y1 - y0) / h)) + 1
    xs = x0 + h * np.arange(nx)
    ys = y0 + h * np.arange(ny)
    X = xs[None, :]                     # (1, nx)
    Y = ys[:, None]                     # (ny, 1)
    m = np.zeros((ny, nx), dtype=bool)
    rho = w * 0.5
    for s in shapes:
        pts = s.pts if hasattr(s, "pts") else s["pts"]
        loop = s.loop if hasattr(s, "loop") else s["loop"]
        filled = s.filled if hasattr(s, "filled") else s["filled"]
        n = len(pts)
        if filled and n >= 3:
            inside = np.zeros((ny, nx), dtype=bool)
            for i in range(n):
                ax, ay = pts[i]; bx, by = pts[(i + 1) % n]
                if ay == by:
                    continue
                cond = (ay > Y) != (by > Y)                     # (ny,1) 브로드캐스트
                xint = ax + (Y - ay) * (bx - ax) / (by - ay)    # (ny,1)
                inside ^= (cond & (X < xint))
            m |= inside
        segs = n if loop else n - 1
        for i in range(segs):
            ax, ay = pts[i]; bx, by = pts[(i + 1) % n]
            dx, dy = bx - ax, by - ay
            L2 = dx * dx + dy * dy
            if L2 < 1e-18:
                d2 = (X - ax) ** 2 + (Y - ay) ** 2
            else:
                t = ((X - ax) * dx + (Y - ay) * dy) / L2
                t = np.clip(t, 0.0, 1.0)
                d2 = (X - ax - t * dx) ** 2 + (Y - ay - t * dy) ** 2
            m |= (d2 <= rho * rho)
    return m, xs, ys


# ---------------------------------------------------------------- 정확 EDT
def _edt1d(f):
    """1차원 하한 포락선 (Felzenszwalb & Huttenlocher). f 는 (rows, n) 의 제곱거리."""
    rows, n = f.shape
    d = np.empty_like(f)
    v = np.zeros((rows, n), dtype=np.int64)
    z = np.empty((rows, n + 1))
    k = np.zeros(rows, dtype=np.int64)
    z[:, 0] = -np.inf
    z[:, 1] = np.inf
    idx = np.arange(rows)
    for q in range(1, n):
        while True:
            vk = v[idx, k]
            s = ((f[:, q] + q * q) - (f[idx, vk] + vk * vk)) / (2.0 * q - 2.0 * vk)
            bad = (s <= z[idx, k]) & (k > 0)
            if not bad.any():
                break
            k[bad] -= 1
        vk = v[idx, k]
        s = ((f[:, q] + q * q) - (f[idx, vk] + vk * vk)) / (2.0 * q - 2.0 * vk)
        k += 1
        v[idx, k] = q
        z[idx, k] = s
        z[idx, k + 1] = np.inf
    k[:] = 0
    for q in range(n):
        while np.any(z[idx, k + 1] < q):
            adv = z[idx, k + 1] < q
            k[adv] += 1
        vk = v[idx, k]
        d[:, q] = (q - vk) ** 2 + f[idx, vk]
    return d


def edt(mask, h):
    """mask=True 픽셀의 가장 가까운 False 까지의 유클리드 거리(월드 단위)."""
    BIG = 1e12
    f = np.where(mask, BIG, 0.0)
    d = _edt1d(f)                    # 행 방향
    d = _edt1d(d.T.copy()).T         # 열 방향
    return np.sqrt(d) * h


def thickness_W(mask, h, w):
    """남은 잉크의 **최대 내접 지름**(획 배수). 0 이면 아무것도 안 남았다.
    ★ 마스크의 경계상자로 잘라서 잰다 — 바깥을 1픽셀 False 로 둘러싸므로 값은 같고 훨씬 빠르다."""
    if not mask.any():
        return 0.0
    ys, xs = np.nonzero(mask)
    y0, y1 = ys.min(), ys.max() + 1
    x0, x1 = xs.min(), xs.max() + 1
    sub = np.zeros((y1 - y0 + 2, x1 - x0 + 2), dtype=bool)
    sub[1:-1, 1:-1] = mask[y0:y1, x0:x1]
    return float(edt(sub, h).max()) * 2.0 / w


# ---------------------------------------------------------------- 연결 성분
def components(mask):
    """행 런렝스 + 유니온파인드. (성분 수, 각 성분 픽셀 수 내림차순)"""
    ny, nx = mask.shape
    parent = {}
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb: parent[rb] = ra
    prev_runs = []
    sizes = {}
    nid = 0
    for y in range(ny):
        row = mask[y]
        if not row.any():
            prev_runs = []; continue
        d = np.diff(row.astype(np.int8))
        starts = list(np.flatnonzero(d == 1) + 1)
        ends = list(np.flatnonzero(d == -1) + 1)
        if row[0]: starts.insert(0, 0)
        if row[-1]: ends.append(nx)
        runs = []
        for a, b in zip(starts, ends):
            nid += 1; parent[nid] = nid; sizes[nid] = b - a
            for pa, pb, pid in prev_runs:
                if pa < b and a < pb: union(pid, nid)
            runs.append((a, b, nid))
        prev_runs = runs
    tot = {}
    for i, s in sizes.items():
        r = find(i); tot[r] = tot.get(r, 0) + s
    out = sorted(tot.values(), reverse=True)
    return len(out), out


# ---------------------------------------------------------------- 프로파일
def profile_from_mask(mask, xs, ys, bins=72):
    """머리 중심 기준 5도 구간별 **최대 반경**. AccessorySilhouetteMetrics.ProfileOf 의 의미."""
    yy, xx = np.nonzero(mask)
    if len(xx) == 0:
        return [0.0] * bins
    px = xs[xx]; py = ys[yy]
    r = np.hypot(px, py)
    a = (np.degrees(np.arctan2(py, px)) % 360.0)
    idx = (a / (360.0 / bins)).astype(int) % bins
    prof = np.zeros(bins)
    np.maximum.at(prof, idx, r)
    return list(prof)
