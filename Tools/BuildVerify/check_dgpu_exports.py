#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
StickMate — Windows exe 의 하이브리드 GPU export 값 독립 검증기
================================================================================
  사용법:
      python3 Tools/BuildVerify/check_dgpu_exports.py Builds/Windows/StickMate.exe
      python3 Tools/BuildVerify/check_dgpu_exports.py <exe> --expect 0
      python3 Tools/BuildVerify/check_dgpu_exports.py <exe> --template <WindowsPlayer.exe>

  종료 코드:  0 = 기대와 일치 / 1 = 불일치·파싱 실패 / 2 = 사용법 오류

================================================================================
★ 이 스크립트는 왜 존재하는가 — "다른 방법으로 다시 재기"
================================================================================
빌드 후처리 훅(Assets/Editor/WindowsHybridGpuExportPostprocessor.cs)은 패치한 뒤 스스로
되읽어 확인한다. 그러나 **자기 코드로 자기를 재는 것은 검증이 아니다** — 파서가 틀렸다면
쓰기도 읽기도 똑같이 틀린 자리를 본다(같은 함정에 같이 빠진다).

그래서 이 스크립트는 훅과 **한 줄도 코드를 공유하지 않는다**. 언어가 다르고(파이썬 대 C#),
구현이 독립이며, 게다가 **서로 다른 두 가지 방법**으로 같은 사실을 잰다:

  방법 1 — PE export 디렉터리를 직접 파싱해 심볼 이름으로 RVA를 찾고 값을 읽는다.
  방법 2 — (--template 제공 시) Unity 플레이어 템플릿 원본과 **바이트 단위로 전량 대조**해
           달라진 오프셋 집합을 뽑는다. 패치가 의도대로라면 다른 곳은 정확히
           "값 DWORD 안의 바이트"뿐이어야 한다. 이 방법은 PE 파싱을 전혀 쓰지 않으므로
           방법 1의 오프셋 계산이 틀렸다면 두 방법의 답이 갈라진다.

두 방법의 답이 다르면 그 사실 자체를 크게 찍고 실패시킨다.

================================================================================
읽는 값의 뜻 (NVIDIA 문서)
================================================================================
  1 = High Performance Graphics(외장 GPU)를 써라   ← Unity 템플릿 기본값
  0 = 이 힌트를 무시하라                            ← 우리가 원하는 값
0은 "내장 강제"가 아니라 "요청 철회"다. 자세한 근거는 docs/verify/WINDOWS_DGPU_REPORT.md.
"""

import argparse
import hashlib
import os
import struct
import sys

NV = "NvOptimusEnablement"
AMD = "AmdPowerXpressRequestHighPerformance"
SYMBOLS = (NV, AMD)

DEFAULT_TEMPLATE = (
    "/Applications/Unity/Hub/Editor/6000.0.82f1/PlaybackEngines/WindowsStandaloneSupport/"
    "Variations/win64_player_nondevelopment_mono/WindowsPlayer.exe"
)


class PeError(Exception):
    pass


def u8(b, o):
    if o + 1 > len(b):
        raise PeError("범위 밖 읽기(u8) at 0x%x" % o)
    return b[o]


def u16(b, o):
    if o + 2 > len(b):
        raise PeError("범위 밖 읽기(u16) at 0x%x" % o)
    return struct.unpack_from("<H", b, o)[0]


def u32(b, o):
    if o + 4 > len(b):
        raise PeError("범위 밖 읽기(u32) at 0x%x" % o)
    return struct.unpack_from("<I", b, o)[0]


def parse_sections(b):
    """PE 헤더를 걸어 섹션 표와 export 데이터 디렉터리를 돌려준다."""
    if len(b) < 0x40 or b[0:2] != b"MZ":
        raise PeError("MZ 서명이 없다 — PE 파일이 아니다.")
    e_lfanew = u32(b, 0x3C)
    if e_lfanew <= 0 or e_lfanew + 24 > len(b):
        raise PeError("e_lfanew(0x%x)가 파일 범위를 벗어난다." % e_lfanew)
    if b[e_lfanew:e_lfanew + 4] != b"PE\0\0":
        raise PeError("PE\\0\\0 서명이 없다 (e_lfanew=0x%x)." % e_lfanew)

    coff = e_lfanew + 4
    machine = u16(b, coff + 0)
    nsec = u16(b, coff + 2)
    opt_size = u16(b, coff + 16)
    opt = coff + 20

    magic = u16(b, opt)
    if magic == 0x20B:      # PE32+
        dd_off = opt + 112
        nrva_off = opt + 108
    elif magic == 0x10B:    # PE32
        dd_off = opt + 96
        nrva_off = opt + 92
    else:
        raise PeError("알 수 없는 optional header magic 0x%x" % magic)

    nrva = u32(b, nrva_off)
    if nrva < 1:
        raise PeError("데이터 디렉터리가 0개다 — export 테이블이 있을 수 없다.")
    exp_rva = u32(b, dd_off + 0)
    exp_size = u32(b, dd_off + 4)

    sec_off = opt + opt_size
    sections = []
    for i in range(nsec):
        s = sec_off + i * 40
        name = bytes(b[s:s + 8]).rstrip(b"\0").decode("ascii", "replace")
        sections.append({
            "name": name,
            "vsize": u32(b, s + 8),
            "vaddr": u32(b, s + 12),
            "rawsize": u32(b, s + 16),
            "rawptr": u32(b, s + 20),
        })
    return {
        "machine": machine, "magic": magic, "sections": sections,
        "export_rva": exp_rva, "export_size": exp_size,
        "checksum": u32(b, opt + 64),
    }


def rva_to_offset(pe, rva):
    for s in pe["sections"]:
        span = max(s["vsize"], s["rawsize"])
        if s["vaddr"] <= rva < s["vaddr"] + span:
            delta = rva - s["vaddr"]
            if delta >= s["rawsize"]:
                raise PeError("RVA 0x%x 는 섹션 %s 의 초기화되지 않은 영역이라 파일에 없다."
                              % (rva, s["name"]))
            return s["rawptr"] + delta, s["name"]
    raise PeError("RVA 0x%x 를 담는 섹션이 없다." % rva)


def read_cstring(b, o, limit=512):
    end = o
    while end < len(b) and end - o < limit and b[end] != 0:
        end += 1
    return bytes(b[o:end]).decode("ascii", "replace")


def parse_exports(b):
    """이름 -> {rva, offset, value(4바이트 정수)} 로 export 를 전량 돌려준다."""
    pe = parse_sections(b)
    if pe["export_rva"] == 0:
        raise PeError("export 데이터 디렉터리가 비어 있다 — 이 exe 에는 export 가 없다.")
    ed, ed_sec = rva_to_offset(pe, pe["export_rva"])

    n_func = u32(b, ed + 20)
    n_name = u32(b, ed + 24)
    a_func = u32(b, ed + 28)
    a_name = u32(b, ed + 32)
    a_ord = u32(b, ed + 36)
    dll_name = read_cstring(b, rva_to_offset(pe, u32(b, ed + 12))[0])

    if n_name > 65536 or n_func > 65536:
        raise PeError("export 개수가 비상식적이다 (funcs=%d names=%d) — 파싱이 어긋났다."
                      % (n_func, n_name))

    names_off = rva_to_offset(pe, a_name)[0]
    ords_off = rva_to_offset(pe, a_ord)[0]
    funcs_off = rva_to_offset(pe, a_func)[0]

    out = {}
    order = []
    for i in range(n_name):
        name = read_cstring(b, rva_to_offset(pe, u32(b, names_off + 4 * i))[0])
        ordinal = u16(b, ords_off + 2 * i)
        if ordinal >= n_func:
            raise PeError("%s 의 ordinal(%d)이 함수 개수(%d)를 벗어난다." % (name, ordinal, n_func))
        frva = u32(b, funcs_off + 4 * ordinal)
        entry = {"name": name, "rva": frva, "ordinal": ordinal + 1}
        # export 디렉터리 안을 가리키면 forwarder(문자열)이지 데이터가 아니다.
        if pe["export_rva"] <= frva < pe["export_rva"] + pe["export_size"]:
            entry["forwarder"] = read_cstring(b, rva_to_offset(pe, frva)[0])
        else:
            off, sec = rva_to_offset(pe, frva)
            entry["offset"] = off
            entry["section"] = sec
            entry["value"] = u32(b, off) if off + 4 <= len(b) else None
        out[name] = entry
        order.append(name)
    return pe, out, order, dll_name, ed_sec


def byte_diff(a, b, limit=64):
    """방법 2 — PE 파싱을 전혀 쓰지 않는 전량 바이트 대조."""
    if len(a) != len(b):
        return None, "길이가 다르다 (%d vs %d) — 값 패치는 길이를 바꾸지 않는다." % (len(a), len(b))
    diffs = []
    for i in range(len(a)):
        if a[i] != b[i]:
            diffs.append(i)
            if len(diffs) > limit:
                return diffs, "차이가 %d개를 넘는다 — 값 패치가 아닌 다른 변경이 섞였다." % limit
    return diffs, None


def main():
    ap = argparse.ArgumentParser(description="Windows exe 의 dGPU export 값을 독립 확인한다.")
    ap.add_argument("exe", help="검사할 .exe 경로")
    ap.add_argument("--expect", type=int, default=None,
                    help="두 심볼 모두 이 값이어야 한다(보통 0). 다르면 종료 코드 1.")
    ap.add_argument("--template", nargs="?", const=DEFAULT_TEMPLATE, default=None,
                    help="Unity 플레이어 템플릿 원본과 바이트 전량 대조(방법 2). "
                         "경로 생략 시 기본 Unity 설치 경로를 쓴다.")
    args = ap.parse_args()

    if not os.path.isfile(args.exe):
        print("FAIL: 파일이 없다 — %s" % args.exe)
        return 1

    data = open(args.exe, "rb").read()
    print("파일    : %s" % os.path.abspath(args.exe))
    print("크기    : %d bytes" % len(data))
    print("SHA-256 : %s" % hashlib.sha256(data).hexdigest())

    try:
        pe, exports, order, dll_name, ed_sec = parse_exports(data)
    except PeError as e:
        print("FAIL: PE export 파싱 실패 — %s" % e)
        return 1

    print("아키텍처: machine=0x%04x magic=0x%03x  체크섬필드=0x%x" %
          (pe["machine"], pe["magic"], pe["checksum"]))
    print("export DLL 이름 필드: %s   (export 디렉터리 섹션: %s)" % (dll_name, ed_sec))
    print("")
    print("  %-40s %-5s %-10s %-8s %-10s %s" % ("이름", "ord", "RVA", "섹션", "파일오프셋", "값"))
    for name in order:
        e = exports[name]
        if "forwarder" in e:
            print("  %-40s %-5d %-10s %-8s %-10s -> %s" %
                  (name, e["ordinal"], "0x%x" % e["rva"], "-", "-", e["forwarder"]))
        else:
            v = e["value"]
            print("  %-40s %-5d %-10s %-8s %-10s %s" %
                  (name, e["ordinal"], "0x%x" % e["rva"], e["section"],
                   "0x%x" % e["offset"], ("%d (0x%08x)" % (v, v)) if v is not None else "?"))
    print("")

    rc = 0
    missing = [s for s in SYMBOLS if s not in exports]
    if missing:
        print("FAIL: 기대한 심볼이 export 에 없다 — %s" % ", ".join(missing))
        return 1

    values = {}
    for s in SYMBOLS:
        e = exports[s]
        if "value" not in e or e["value"] is None:
            print("FAIL: %s 의 값을 읽을 수 없다(forwarder 이거나 파일 밖)." % s)
            return 1
        values[s] = e["value"]
        print("[방법1] %-40s = %d   (파일 오프셋 0x%x)" % (s, e["value"], e["offset"]))

    # ---- export 이름 테이블 오름차순 (PE 로더 규약) ----
    if order != sorted(order):
        print("FAIL: export 이름 테이블이 사전순 오름차순이 아니다 — GetProcAddress 의 이진 탐색이 깨진다.")
        print("      실제 순서: %s" % ", ".join(order))
        rc = 1
    else:
        print("[규약] export 이름 테이블 사전순 오름차순 OK (%s)" % " < ".join(order))

    # ---- 방법 2: 템플릿 전량 바이트 대조 ----
    if args.template:
        if not os.path.isfile(args.template):
            print("[방법2] 건너뜀 — 템플릿이 없다: %s" % args.template)
        else:
            tpl = open(args.template, "rb").read()
            diffs, err = byte_diff(data, tpl)
            print("[방법2] 템플릿: %s" % args.template)
            print("        템플릿 SHA-256: %s" % hashlib.sha256(tpl).hexdigest())
            if err:
                print("        FAIL: %s" % err)
                rc = 1
            elif not diffs:
                print("        차이 0바이트 — 이 exe 는 템플릿 원본 그대로다(패치되지 않았다).")
            else:
                print("        달라진 바이트 %d개: %s" % (len(diffs), ", ".join("0x%x" % d for d in diffs)))
                # 두 방법의 교차 확인: 달라진 바이트가 전부 두 값 DWORD 안에 들어가는가.
                allowed = set()
                for s in SYMBOLS:
                    o = exports[s]["offset"]
                    allowed.update(range(o, o + 4))
                stray = [d for d in diffs if d not in allowed]
                if stray:
                    print("        FAIL: 값 DWORD 밖의 바이트가 바뀌었다: %s"
                          % ", ".join("0x%x" % d for d in stray))
                    print("        → 방법1(파싱)과 방법2(바이트)의 답이 갈라졌다. 오프셋 계산을 의심하라.")
                    rc = 1
                else:
                    print("        OK: 변경은 두 값 DWORD 안에만 있다 — 방법1과 방법2가 일치한다.")

    # ---- 기대값 판정 ----
    if args.expect is not None:
        bad = [s for s in SYMBOLS if values[s] != args.expect]
        if bad:
            print("")
            print("FAIL: 기대값 %d 과 다르다 — %s" %
                  (args.expect, ", ".join("%s=%d" % (s, values[s]) for s in bad)))
            rc = 1
        else:
            print("")
            print("PASS: 두 심볼 모두 %d 이다." % args.expect)
    else:
        print("")
        print("(--expect 를 주지 않았다 — 값만 보고했고 판정은 하지 않았다.)")

    return rc


if __name__ == "__main__":
    sys.exit(main())
