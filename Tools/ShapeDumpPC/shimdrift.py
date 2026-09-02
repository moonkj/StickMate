# -*- coding: utf-8 -*-
"""CoreShim.cs 가 베껴 둔 StickConfig 상수가 프로덕션과 같은 값인지 검사한다.

왜 있는가 (2026-09-02)
----------------------
prodverify.py 는 설계 검산 하니스에 **프로덕션이 실제로 만드는 좌표**를 먹이는 장치다.
그 프로덕션 코드를 컴파일하려고 CoreShim.cs 가 StickConfig 상수 몇 개를 베껴 두는데,
오늘 밤 StickConfig 에 MinFillOutlineScreenPoints 가 생기자 shim 에는 안 생겨서
prodverify 가 컴파일 단계에서 죽었다. 즉 **설계 좌표와 프로덕션이 일치하는지 보는
유일한 장치가 조용히 꺼져 있었다.** 값이 달라졌을 때는 더 나쁘다 — 컴파일은 되고
검산만 거짓말을 한다.

규칙: shim 이 선언한 상수는 전부 프로덕션에 같은 이름·같은 값으로 존재해야 한다.
      (shim 에만 있는 것 = 유령, 값이 다른 것 = 검산 오염)
"""
import pathlib, re, sys

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
SHIM = HERE / "CoreShim.cs"
PROD = REPO / "Assets/_Project/Scripts/Core/StickConfig.cs"

CONST = re.compile(r"public\s+const\s+float\s+(\w+)\s*=\s*([^;]+);")


def consts(path: pathlib.Path) -> dict[str, float]:
    out = {}
    for name, expr in CONST.findall(path.read_text(encoding="utf-8")):
        e = re.sub(r"(?<=[\d.])[fF]\b", "", expr).strip()
        if not re.fullmatch(r"[-+*/(). \d]+", e):
            continue                      # 다른 상수를 참조하는 식은 건너뛴다
        try:
            out[name] = float(eval(e, {"__builtins__": {}}))
        except Exception:
            pass
    return out


def main() -> int:
    if not SHIM.exists() or not PROD.exists():
        print(f"!! 파일 없음: {SHIM if not SHIM.exists() else PROD}")
        return 1

    shim, prod = consts(SHIM), consts(PROD)
    if not shim:                          # 양성 대조 — 파서가 죽으면 '통과' 로 둔갑한다
        print("!! shim 에서 상수를 하나도 못 읽었다 — 파서 고장. 판정 무효.")
        return 1

    bad = []
    for name, want in sorted(shim.items()):
        got = prod.get(name)
        if got is None:
            print(f"  !! {name:<38} shim={want!r}  프로덕션에 없음(유령 상수)")
            bad.append(name)
        elif abs(got - want) > 1e-9:
            print(f"  !! {name:<38} shim={want!r}  프로덕션={got!r}  ← 값이 다르다")
            bad.append(name)
        else:
            print(f"  OK {name:<38} {want!r}")

    print(f"\nshim 상수 {len(shim)}개 대조 — {'어긋남 ' + str(len(bad)) + '건' if bad else '전부 일치'}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
