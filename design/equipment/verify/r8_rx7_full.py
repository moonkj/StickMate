# -*- coding: utf-8 -*-
"""R8 — A군 처방(띠에 두께 h)을 **거울에 실제로 적용하고 verify.py 전량을 다시 돌린다**.

★ 이 스크립트는 프로덕션 .cs 를 고치지 않는다. 설계 거울(items.py / hair.py)의 반환값을
  런타임에 감싸서 30종 전수 검산을 다시 태운다 — 「처방이 다른 규칙을 깨지 않는가」를
  주장이 아니라 러너로 답하기 위해서다.

  h = 0.46 R  (배율 0.60 의 1획 = 0.4298 R 보다 0.0302 R 여유. 0.75 에서는 1.34획)
"""
import os, subprocess, sys, textwrap

HERE = os.path.dirname(os.path.abspath(__file__))
H_BAND = 0.46

PATCH = textwrap.dedent('''
    import rig, items, hair
    from rig import Shape
    H = %f
    A = {"중절모": ("FedoraBand", items.HEAD), "밀짚모자": ("StrawBand", items.HEAD),
         "왕관": ("CrownRim", items.HEAD), "베레모": ("BeretRim", items.HEAD),
         "바가지머리": ("HairFringe", hair.SET)}

    def quad(base, h):
        return list(base) + [(x, y + h) for x, y in reversed(list(base))]

    def wrap(tbl, key, aname):
        sh = tbl[key]
        sh = sh() if callable(sh) else list(sh)
        tbl[key] = [Shape(s.name, quad(s.pts, H), True, filled=True, tone=s.tone)
                    if s.name == aname else s for s in sh]

    for k, (n, t) in A.items():
        wrap(t, k, n)
''' % H_BAND)

if __name__ == "__main__":
    src = open(os.path.join(HERE, "verify.py"), encoding="utf-8").read()
    marker = "import rig, items, hair\n"
    assert marker in src, "verify.py 머리말이 바뀌었다 — 패치 지점을 다시 잡아라"
    patched = src.replace(marker, marker + PATCH, 1)
    out = os.path.join(HERE, "_r8_verify_patched.py")
    open(out, "w", encoding="utf-8").write(patched)
    r = subprocess.run([sys.executable, out], cwd=HERE, capture_output=True, text=True)
    print(r.stdout[-4000:]); print(r.stderr[-2000:])
    os.remove(out)
