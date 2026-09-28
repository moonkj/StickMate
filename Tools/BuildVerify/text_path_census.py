#!/usr/bin/env python3
"""배포물 안 **모든 파일**에서 이 PC의 계정명과 홈 경로 문자열을 전수로 센다. 읽기 전용.

    python3 Tools/BuildVerify/text_path_census.py <빌드폴더|zip> [...]
    python3 Tools/BuildVerify/text_path_census.py --selftest [<빌드폴더|zip> ...]

왜 이게 있는가 (2026-09-28, 릴리즈 직전에 실재하는 구멍을 잡았다):
  `pdb_path_census.py` 는 **관리 DLL 의 PE 디버그 디렉터리(CodeView RSDS)** 만 읽는다. 그래서
  «DLL 이 아닌 파일» 의 유출을 구조적으로 못 본다. 실제로 그 틈으로 하나가 새 나갔다 —
  `Builds/Windows/dgpu-export-patch.txt` (2026-09-14 빌드가 남긴 영수증)가 산출물 폴더에 그대로
  남아 있었고, 그 안에 홈 경로 1회와 계정명 1회가 있었다. 2026-09-28 묶음이 영수증을
  `Logs/BuildReceipts/` (gitignore)로 옮겼지만, **옮긴 것은 새로 쓰는 위치일 뿐이고 폴더에 이미
  있던 옛 파일은 그대로 남는다.** 그래서 zip 을 만들면 그 옛 영수증이 같이 들어간다.

  ★ 이 계기가 없었으면 그 파일은 공개 릴리스에 올라갔다. 계수기가 DLL 만 재고 있었기 때문에
    「우리_절대경로=0」 이라는 초록이 나오고 있었고, 그 초록은 «DLL 에 관해서만» 참이었다.

★★ 산출물 폴더는 증분이다 — 「이번 빌드가 만든 것」과 「폴더에 있는 것」은 다른 집합이다.
  2026-09-28 실측: 143 파일 중 그날 갱신된 것은 11개뿐이었다(나머지는 09-03/09-07/09-14).
  그러므로 «빌드 로그가 초록» 이라는 사실은 «폴더가 깨끗하다» 를 뜻하지 않는다. 폴더를 재라.

이 스크립트의 규칙 세 개 — `pdb_path_census.py` 와 같은 계열이다:
  1. ★ 종료코드로 판정하지 않는다. 판정은 마지막 「판정:」 줄들이다.
     rc 는 «계기가 살아 있었는가» 만 말한다(0 = 살아 있음, 3 = 무효, 2 = 사용법).
     적중이 있어도 rc 는 0이다 — 적용 «전» 산출물에도 돌려 양성 대조로 쓰기 때문이다.
  2. ★ 「0건」을 그냥 내지 않는다. 파일을 하나도 못 읽었으면 그 입력의 모든 0을 폐기하고
     「계기 무효」를 찍는다(경로를 잘못 준 것과 깨끗한 것이 똑같이 생긴다).
     그리고 --selftest 는 합성 표본으로 «찾아내는 능력» 자체를 먼저 교정한다.
  3. ★ 계정명을 출력에 남기지 않는다. 계정명은 실행 시점에 `id -un` 으로 얻고 절대 찍지 않으며,
     경로의 사용자 부분은 <이름>으로 가린다. 적중은 «파일 이름과 건수» 로만 보고한다.
"""

import os
import re
import subprocess
import sys
import zipfile

USER_PLACEHOLDER = "<이름>"
HOME_PREFIXES = (b"/Users/", b"/home/", b"C:\\Users\\")


def account_name():
    """실행 시점에 얻는다. 소스에 박지 않는다(박으면 그 자체가 유출이다)."""
    try:
        out = subprocess.run(["id", "-un"], capture_output=True, timeout=10)
        return out.stdout.decode("utf-8", "replace").strip()
    except Exception:
        return ""


def mask(text):
    text = re.sub(r"/Users/[^/\s\"']+", "/Users/" + USER_PLACEHOLDER, text)
    text = re.sub(r"/home/[^/\s\"']+", "/home/" + USER_PLACEHOLDER, text)
    return re.sub(r"C:\\\\Users\\\\[^\\\\\s\"']+", "C:\\\\Users\\\\" + USER_PLACEHOLDER, text)


def members(path):
    """(표시이름, 바이트) 를 흘린다. 폴더와 zip 을 같은 모양으로 읽는다."""
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as zf:
            for name in zf.namelist():
                if name.endswith("/"):
                    continue
                try:
                    yield name, zf.read(name)
                except Exception:
                    continue
        return
    if os.path.isdir(path):
        for root, _dirs, files in os.walk(path):
            for f in files:
                full = os.path.join(root, f)
                try:
                    with open(full, "rb") as fh:
                        yield os.path.relpath(full, path), fh.read()
                except Exception:
                    continue
        return
    if os.path.isfile(path):
        try:
            with open(path, "rb") as fh:
                yield os.path.basename(path), fh.read()
        except Exception:
            pass


def census(path, user):
    """한 입력을 전수로 센다. (읽은 파일 수, 계정명 적중, 홈경로 적중) 을 준다."""
    read = 0
    acct = []
    home = []
    needle = user.encode("utf-8") if user else None
    for name, raw in members(path):
        read += 1
        if needle and needle in raw:
            acct.append((name, raw.count(needle)))
        total = sum(raw.count(p) for p in HOME_PREFIXES)
        if total:
            home.append((name, total))
    return read, acct, home


def selftest():
    """★ 찾아내는 능력을 먼저 교정한다. 여기서 실패하면 뒤의 모든 0은 의미가 없다."""
    user = account_name()
    if not user:
        print("selftest: 계정명을 얻지 못했다 -> 계기 무효")
        return False

    import io
    import tempfile

    ok = True

    # 양성 1 — 계정명이 든 zip 멤버를 찾아내는가.
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("probe.txt", "/Users/" + user + "/App/x")
        zf.writestr("clean.txt", "no secrets here")
    buf.seek(0)
    with tempfile.NamedTemporaryFile(suffix=".zip", delete=False) as tf:
        tf.write(buf.getvalue())
        zpath = tf.name
    read, acct, home = census(zpath, user)
    os.unlink(zpath)
    pos_zip = (read == 2 and len(acct) == 1 and len(home) == 1)
    ok = ok and pos_zip

    # 양성 2 — 폴더에서도 같은가. 그리고 «깨끗한 파일» 은 세지 않는가(음성).
    with tempfile.TemporaryDirectory() as d:
        with open(os.path.join(d, "dirty.txt"), "w", encoding="utf-8") as fh:
            fh.write("C:\\Users\\" + user + "\\y")
        with open(os.path.join(d, "clean.bin"), "wb") as fh:
            fh.write(b"\x00\x01\x02 nothing")
        read2, acct2, home2 = census(d, user)
    pos_dir = (read2 == 2 and len(acct2) == 1 and len(home2) == 1)
    ok = ok and pos_dir

    # 음성 — 계정명이 없는 입력에서 0이 나오는가.
    with tempfile.TemporaryDirectory() as d:
        with open(os.path.join(d, "a.txt"), "w", encoding="utf-8") as fh:
            fh.write("relative/paths/only")
        read3, acct3, home3 = census(d, user)
    neg = (read3 == 1 and not acct3 and not home3)
    ok = ok and neg

    # 가리기 — 출력 경로에 계정명이 남지 않는가.
    masked = mask("/Users/" + user + "/App/StickMate")
    hidden = (user not in masked and USER_PLACEHOLDER in masked)
    ok = ok and hidden

    print("selftest: zip양성 %s · 폴더양성 %s · 음성 %s · 가리기 %s -> %s"
          % (pos_zip, pos_dir, neg, hidden, "통과" if ok else "실패"))
    return ok


def main(argv):
    args = [a for a in argv[1:]]
    want_selftest = "--selftest" in args
    inputs = [a for a in args if a != "--selftest"]

    if want_selftest and not selftest():
        print("판정: 계기가 무효다. 아래 숫자를 쓰지 마라.")
        return 3
    if not inputs:
        if want_selftest:
            return 0
        print(__doc__)
        return 2

    user = account_name()
    if not user:
        print("판정: 계정명을 얻지 못했다 -> 계기 무효(모든 0을 폐기한다).")
        return 3

    void = False
    for path in inputs:
        label = os.path.basename(path.rstrip("/")) or path
        if not os.path.exists(path):
            print("%s: 입력이 없다 -> 계기 무효" % label)
            void = True
            continue
        read, acct, home = census(path, user)
        for name, n in acct:
            print("  [계정명] %s (%d회)" % (name, n))
        for name, n in home:
            print("  [홈경로] %s (%d회)" % (name, n))
        print("%s: 파일=%d 계정명적중=%d 홈경로적중=%d" % (label, read, len(acct), len(home)))
        if read == 0:
            print("  판정: 파일을 하나도 읽지 못했다 -> 계기 무효(이 입력의 0은 폐기한다).")
            void = True
            continue
        if acct:
            print("  판정: 계정명이 %d개 파일에 남아 있다 — 공개 배포 금지." % len(acct))
        else:
            print("  판정: 계정명 0건 (계기 생존: 파일 %d개를 읽었다)." % read)
        if home:
            print("  참고: 홈경로 적중 %d건. Unity 동봉 DLL 은 «Unity 빌드 머신» 의 경로라 정상이다 —"
                  " 우리 파일인지 아닌지를 이름으로 가려라." % len(home))

    print("판정: %s" % ("계기가 한 번이라도 무효였다 — 그 입력의 0을 신뢰하지 마라."
                       if void else
                       "계기는 전부 살아 있었다. 위 「계정명적중」 숫자로 판정하라."))
    return 3 if void else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
