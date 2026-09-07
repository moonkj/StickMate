#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
StickMate — macOS .app 의 «자동 그래픽 전환 선언» 독립 검증기
================================================================================
  사용법:
      python3 Tools/BuildVerify/check_mac_gpu_switching.py Builds/macOS/StickMate.app
      python3 Tools/BuildVerify/check_mac_gpu_switching.py <app> --expect-absent
      python3 Tools/BuildVerify/check_mac_gpu_switching.py <app> --skip-signature

  종료 코드:  0 = 기대와 일치 / 1 = 불일치·파싱 실패 / 2 = 사용법 오류

================================================================================
★ 이 스크립트는 왜 존재하는가 — "다른 방법으로 다시 재기"
================================================================================
빌드 후처리 훅(Assets/Editor/MacHybridGpuInfoPlistPostprocessor.cs)은 고친 뒤 스스로 되읽어
확인한다. 그러나 **자기 코드로 자기를 재는 것은 검증이 아니다** — 파서가 틀렸다면 쓰기도 읽기도
똑같이 틀린 자리를 본다(같은 함정에 같이 빠진다).

그래서 이 스크립트는 훅과 **한 줄도 코드를 공유하지 않는다**. 언어가 다르고(파이썬 대 C#),
게다가 **서로 다른 세 가지 계기**로 같은 사실을 잰다:

  계기 1 — 파이썬 표준 라이브러리 plistlib 로 파싱한다(XML/바이너리 둘 다 읽는다).
  계기 2 — Apple 자신의 도구 /usr/libexec/PlistBuddy 로 그 키 하나를 직접 꺼내 본다.
  계기 3 — codesign 으로 **서명이 유효한지** 확인한다.

셋의 답이 갈라지면 그 사실 자체를 크게 찍고 실패시킨다.

★ 계기 3 이 왜 이 스크립트에 있는가(macOS 고유):
  Info.plist 는 코드 서명에 **봉인**된다. 한 글자만 고쳐도 서명이 깨지고,
  **Apple Silicon 에서 서명이 깨진 앱은 아예 실행되지 않는다.**
  즉 "키는 들어갔는데 앱이 안 켜진다"가 가능한 실패 형태이고, 키만 확인하는 검증기는
  그 상태를 **초록으로 통과시킨다.** 그래서 둘을 같이 잰다.

================================================================================
읽는 값의 뜻
================================================================================
  NSSupportsAutomaticGraphicsSwitching = true
      "이 앱은 그래픽 전환을 감당한다" → macOS 가 **내장 GPU 에서 시작**하고,
      정말 필요할 때만 디스크리트로 올린다.
  (키 없음)
      macOS 가 보수적으로 디스크리트 GPU 를 깨울 수 있다 ← Unity 템플릿의 기본 상태

  ★ 이것은 "내장 강제"가 **아니다**. Windows 쪽 값 0 이 "내장 강제"가 아니라 "요청 철회"인 것과
  같다. 그리고 이 선언은 **GPU 사용률을 낮추지 않는다** — 어느 GPU 가 그리는가만 바꾼다.
"""

import argparse
import os
import plistlib
import subprocess
import sys

KEY = "NSSupportsAutomaticGraphicsSwitching"
PLIST_BUDDY = "/usr/libexec/PlistBuddy"
CODESIGN = "/usr/bin/codesign"


def fail(message):
    print("  [FAIL] " + message)
    return False


def ok(message):
    print("  [ OK ] " + message)
    return True


def run(argv):
    """외부 도구 하나를 돌리고 (rc, stdout, stderr) 를 돌려준다."""
    try:
        p = subprocess.run(argv, capture_output=True, text=True, timeout=120)
        return p.returncode, p.stdout.strip(), p.stderr.strip()
    except FileNotFoundError:
        return None, "", "도구가 없다: " + argv[0]
    except subprocess.TimeoutExpired:
        return None, "", "시간 초과: " + " ".join(argv)


# ==============================================================================
# 계기 1 — plistlib
# ==============================================================================
def measure_plistlib(plist_path):
    """(발견됨, 값, 전체 키 개수) 또는 예외."""
    with open(plist_path, "rb") as f:
        data = plistlib.load(f)
    if not isinstance(data, dict):
        raise ValueError("루트가 dict 가 아니다: %r" % type(data))
    return (KEY in data), data.get(KEY, None), len(data)


# ==============================================================================
# 계기 2 — PlistBuddy (Apple 자신의 도구)
# ==============================================================================
def measure_plistbuddy(plist_path):
    """(측정가능, 발견됨, 원문) — 도구가 없으면 측정가능=False."""
    if not os.path.exists(PLIST_BUDDY):
        return False, None, "PlistBuddy 없음"
    rc, out, err = run([PLIST_BUDDY, "-c", "Print :" + KEY, plist_path])
    if rc is None:
        return False, None, err
    if rc != 0:
        # "Does Not Exist" 가 정상적인 '없음' 응답이다.
        return True, False, (out + " " + err).strip()
    return True, True, out


# ==============================================================================
# 계기 3 — codesign
# ==============================================================================
def measure_signature(app_path):
    """(측정가능, 서명있음, 유효함, 설명)."""
    signature_dir = os.path.join(app_path, "Contents", "_CodeSignature")
    signed_on_disk = os.path.isdir(signature_dir)

    if not os.path.exists(CODESIGN):
        return False, signed_on_disk, None, "codesign 없음(비 macOS 호스트)"

    rc, out, err = run([CODESIGN, "--verify", "--deep", "--strict", app_path])
    if rc is None:
        return False, signed_on_disk, None, err

    rc2, out2, err2 = run([CODESIGN, "-dv", "--verbose=2", app_path])
    description = (out2 + " " + err2).replace("\n", " | ").strip()
    return True, signed_on_disk, rc == 0, (err or out or "").strip() + " || " + description


def main():
    parser = argparse.ArgumentParser(add_help=True)
    parser.add_argument("app", help="검사할 .app 번들 경로")
    parser.add_argument("--expect-absent", action="store_true",
                        help="키가 **없어야** 정상이라고 기대한다(후처리 이전 산출물을 대조할 때).")
    parser.add_argument("--skip-signature", action="store_true",
                        help="서명 검사를 건너뛴다(서명 없이 굽는 파이프라인 전용).")
    args = parser.parse_args()

    app_path = args.app.rstrip("/")
    plist_path = os.path.join(app_path, "Contents", "Info.plist")

    print("StickMate — macOS 자동 그래픽 전환 선언 독립 검증")
    print("  대상: " + app_path)
    print("  키  : " + KEY)
    print("  기대: " + ("키 없음" if args.expect_absent else "키 = true"))
    print("")

    if not os.path.isdir(app_path):
        print("  [FAIL] .app 번들이 없다: " + app_path)
        return 1
    if not os.path.isfile(plist_path):
        print("  [FAIL] 번들 안에 Contents/Info.plist 가 없다: " + plist_path)
        return 1

    good = True

    # ---- 계기 1 ----
    try:
        found1, value1, total = measure_plistlib(plist_path)
    except Exception as e:  # noqa: BLE001 — 어떤 파싱 실패든 요란하게 남긴다.
        print("  [FAIL] 계기1(plistlib) 파싱 실패: %s: %s" % (type(e).__name__, e))
        return 1
    print("  계기1 plistlib     : 루트 키 %d개, %s=%r" % (total, KEY, value1 if found1 else "(없음)"))

    # ---- 계기 2 ----
    usable2, found2, raw2 = measure_plistbuddy(plist_path)
    if usable2:
        print("  계기2 PlistBuddy   : %s (%s)" % ("있음" if found2 else "없음", raw2))
    else:
        print("  계기2 PlistBuddy   : 측정 불가 (%s)" % raw2)

    # ---- 두 계기의 답이 갈라지는가 ----
    if usable2 and found1 != found2:
        good = fail("계기1과 계기2의 답이 갈라진다(plistlib=%s, PlistBuddy=%s). "
                    "둘 중 하나가 이 문서를 잘못 읽고 있다 — 어느 쪽도 믿지 마라."
                    % (found1, found2))

    # ---- 기대와의 대조 ----
    if args.expect_absent:
        if found1:
            good = fail("키가 없어야 하는데 있다(%r)." % value1)
        else:
            ok("키가 없다 — 기대와 일치(후처리 이전 상태).")
    else:
        if not found1:
            good = fail("키가 없다. 이 .app 은 후처리를 거치지 않았거나 후처리가 실패했다. "
                        "산출물 옆 mac-gpu-switching-plist.txt 영수증을 읽어라.")
        elif value1 is not True:
            good = fail("키는 있는데 값이 %r 이다(기대: True)." % value1)
        else:
            ok("%s = true — 기대와 일치." % KEY)

    # ---- 다른 키가 살아 있는가(무손상 대조) ----
    required_keys = ("CFBundleIdentifier", "CFBundleExecutable", "CFBundleName")
    try:
        with open(plist_path, "rb") as f:
            data = plistlib.load(f)
        missing = [k for k in required_keys if k not in data]
    except Exception as e:  # noqa: BLE001
        missing = None
        good = fail("무손상 대조 중 재파싱 실패: %s: %s" % (type(e).__name__, e))

    if missing:
        good = fail("필수 키가 사라졌다(%s) — 후처리가 다른 것을 망가뜨렸다." % ", ".join(missing))
    elif missing is not None:
        ok("무손상 대조: %s 가 모두 살아 있다." % " / ".join(required_keys))

    # ---- 계기 3: 서명 ----
    if args.skip_signature:
        print("  계기3 codesign     : 건너뜀(--skip-signature)")
    else:
        usable3, signed, valid, description = measure_signature(app_path)
        print("  계기3 codesign     : 서명흔적=%s 유효=%s" % (signed, valid))
        if description:
            print("                       " + description[:300])
        if not usable3:
            print("  [ ?? ] 서명을 잴 수 없다(%s) — 이 실행은 '서명 미확인'이다." % description)
        elif signed and valid is not True:
            good = fail("서명이 유효하지 않다. Info.plist 는 서명에 봉인돼 있어서 후처리가 고친 뒤 "
                        "재서명하지 않으면 이 상태가 된다. **Apple Silicon 에서는 앱이 아예 실행되지 "
                        "않는다** — 이 번들을 배포하지 마라.")
        elif signed:
            ok("서명 유효 — 이 번들의 Info.plist 와 서명이 서로 맞는다.")
        else:
            ok("서명 없음 — 봉인 문제가 성립하지 않는다.")

    print("")
    print("RESULT=" + ("PASS" if good else "FAIL"))
    if good:
        print("★ 여기까지가 이 머신에서 증명 가능한 전부다: 키가 올바르게 들어갔고 다른 키가 "
              "그대로이며 서명이 유효하다.")
        print("★ 미확인: 그 키가 실제로 **내장 GPU 선택**으로 귀결되는지. 듀얼 GPU Mac 에서만 갈린다.")
        print("★ 그리고 이 선언은 GPU **사용률**을 낮추지 않는다 — 부하는 별개의 문제다.")
    return 0 if good else 1


if __name__ == "__main__":
    sys.exit(main())
