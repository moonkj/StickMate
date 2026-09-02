#!/usr/bin/env python3
"""빌드 산출물 마커 검증 (macOS/Windows 동시).

    python3 Tools/BuildVerify/verify-build.py

왜 이게 있는가 (2026-09-02):
  오염된 디버그 코드(NEGCTRL)가 빌드에 안 들어갔는지 `strings | grep` 으로 확인하고
  "0건 = 깨끗" 이라고 보고했다. 그런데 .NET 어셈블리는 문자열을 #US 힙에 UTF-16LE 로
  저장한다 — 글자마다 널바이트가 끼어서 ASCII 스캔에는 애초에 안 걸린다.
  같은 문자열을 일부러 심어둔 파일로 실측: strings 0건, grep -a 0건, UTF-16 카운트 1건.
  즉 그 검사는 탐지력이 0이었고, "깨끗함"과 "못 찾음"이 똑같이 0으로 보였다.

그래서 이 스크립트의 규칙 두 개:
  1. 인코딩을 가정하지 않는다 — UTF-8 과 UTF-16LE 를 둘 다 센다.
  3. ★ 산출물이 **마지막 커밋보다 새로운가**를 실패 조건으로 둔다.
     (2026-09-02 검증팀 발견) 이 검사는 원래 빌드 시각을 **출력만 하고 게이트하지 않았다.**
     그래서 오늘 고친 것이 하나도 안 들어간 07:54 빌드에 "전부 통과"를 줬다 —
     커밋은 10:39/11:21 이었다. 마커는 전부 맞았다. 낡았을 뿐이다.
     사용자가 그 zip 을 받으면 격파 놀이가 그대로 있고 모자도 그대로 머리를 덮는다.
     "마커가 맞다"와 "지금 코드다"는 다른 축이고, 후자가 없으면 전자는 무의미하다.

  2. ★ 모든 '없어야 함' 판정에 양성 대조를 강제한다. 양성 대조가 **하나라도**
     안 잡히면 그 파일의 '0건' 들은 근거 없음으로 선언하고 실패시킨다.
     하나라도, 인 이유: ASCII 전용으로 되돌려 실측했더니 '무릎앉아'는 17건이
     잡히고 '벽타기'만 0건이 됐다. 즉 검사는 반쯤 작동할 수 있고, 그때 남은
     양성 대조가 '정상'이라는 착시를 준다. 부분 고장이 완전 고장보다 위험하다.
     (경로 오타·인코딩 불일치·빈 파일이 전부 '통과'로 둔갑하는 것도 함께 막는다)
"""
import datetime, pathlib, subprocess, sys

REPO = pathlib.Path(__file__).resolve().parents[2]

TARGETS = {
    "macOS":   REPO / "Builds/macOS/StickMate.app/Contents/Resources/Data/Managed/StickMate.Runtime.dll",
    "Windows": REPO / "Builds/Windows/StickMate_Data/Managed/StickMate.Runtime.dll",
}

# (마커, 있어야 하는가)
#   True  = 양성 대조. 검사 방법이 이 파일에서 작동한다는 증거.
#   False = 본 검사. 임시 디버그 코드나 삭제한 문구가 남아 있지 않은가.
MARKERS = [
    ("무릎앉아",      True),
    ("되올라가기",    True),
    ("벽타기",        True),
    ("NEGCTRL",       False),   # 네거티브 컨트롤용 임시 코드
    ("창 밖을 클릭",  False),   # 삭제된 닫기 안내 문구
    ("닫으려면",      False),
]


def count(blob: bytes, needle: str) -> int:
    """UTF-8 + UTF-16LE 양쪽에서 센다. 인코딩을 가정하지 않는다."""
    return blob.count(needle.encode("utf-8")) + blob.count(needle.encode("utf-16-le"))


def last_commit_time() -> float | None:
    """마지막 커밋 시각. git 이 없거나 실패하면 None(그때는 신선도 검사를 건너뛰되 알린다)."""
    try:
        out = subprocess.run(["git", "-C", str(REPO), "log", "-1", "--format=%ct"],
                             capture_output=True, text=True, timeout=10)
        return float(out.stdout.strip()) if out.returncode == 0 and out.stdout.strip() else None
    except Exception:
        return None


def main() -> int:
    failures = []
    commit_ts = last_commit_time()
    if commit_ts is None:
        print("!! 마지막 커밋 시각을 못 읽었다 — 신선도 검사를 건너뛴다(판정 신뢰도 낮음).")
    else:
        print(f"마지막 커밋: {datetime.datetime.fromtimestamp(commit_ts):%Y-%m-%d %H:%M}")
    for name, path in TARGETS.items():
        print(f"\n=== {name} ===")
        if not path.exists():
            # 경로가 틀렸는데 0건이 나와 '깨끗'으로 읽히는 사고를 막는다.
            print(f"  !! 산출물 없음: {path}")
            failures.append(f"{name}: 산출물 없음")
            continue

        blob = path.read_bytes()
        mtime_ts = path.stat().st_mtime
        mtime = datetime.datetime.fromtimestamp(mtime_ts)
        print(f"  {path.name}  {len(blob):,}B  빌드 {mtime:%Y-%m-%d %H:%M}")

        # ★ 신선도 — 마커가 다 맞아도 낡은 산출물이면 그 통과는 무의미하다.
        if commit_ts is not None and mtime_ts < commit_ts:
            behind = (commit_ts - mtime_ts) / 60.0
            print(f"  !! 산출물이 마지막 커밋보다 {behind:.0f}분 낡았다 — 오늘 고친 것이 안 들어 있다.")
            failures.append(f"{name}: 낡은 산출물({behind:.0f}분 뒤처짐)")

        positive_missing = []
        for needle, expected in MARKERS:
            n = count(blob, needle)
            ok = (n > 0) == expected
            if expected and n == 0:
                positive_missing.append(needle)
            print(f"  {'OK' if ok else '!!'}  {needle:<14} {n:>4}건  "
                  f"(기대: {'있음' if expected else '없음'})")
            if not ok:
                failures.append(f"{name}/{needle}: {n}건 (기대 {'있음' if expected else '없음'})")

        if positive_missing:
            # 양성 대조가 깨진 파일에서는 위의 '0건' 이 '깨끗함' 을 뜻하지 않는다.
            print(f"  !! 양성 대조 실패({', '.join(positive_missing)}) — "
                  f"이 파일의 '0건' 은 근거 없음. 오염 여부 판정 불가.")
            failures.append(f"{name}: 양성 대조 실패 — '없음' 판정 전부 무효")

    print("\n" + "=" * 52)
    if failures:
        print("실패:\n  " + "\n  ".join(failures))
        return 1
    print("전부 통과")
    return 0


if __name__ == "__main__":
    sys.exit(main())
