# ShapeDumpPC — `prodverify.py` / `mirrordrift.py` 의 **양성 대조 장치**

두 하니스가 "위반 0건 / 어긋남 0건"이라고 말할 때, 그 0이 **진짜 0인지** 아니면
**하니스가 죽어 있는지**를 가른다.

검사 코드도 shim 도 **한 줄도 복제하지 않는다**. `Tools/ShapeDump` 를 그대로 부르고
**먹이는 빌더 파일만** 모자 처방 이전(`7ab0468^`)의 `AccessoryShapeBuilder.cs` 로 바꿔치기한다.

## 사용법

```bash
cd /Users/kjmoon/App/StickMate
git show 7ab0468^:Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs \
  > Tools/ShapeDumpPC/AccessoryShapeBuilder.PARENT.cs

python3 Tools/ShapeDumpPC/prodverify.py                     # rc 0 이어야 한다
python3 Tools/ShapeDumpPC/mirrordrift_positive_control.py   # rc 0 이어야 한다
```

## ★ 종료코드가 뒤집혀 있다

이 폴더에서는 **빨간불이 정상**이다. 그래서 두 스크립트 모두 결과를 뒤집어 내보낸다 —
게이트에서 `A && B && C` 로 이어 붙일 때 사람이 예외를 기억하지 않아도 되게.

| rc | 뜻 |
|---:|---|
| 0 | 옛 빌더에서 하니스가 **빨간불을 냈다** = 하니스가 살아 있다 |
| 1 | 옛 빌더인데도 **0건** → 본 하니스의 모든 "0건"을 무효로 선언한다 (docs/TEAM.md 4절 사고 #4) |

## ★ 왜 복제본을 없앴는가 (2026-09-02)

이 폴더는 `CoreShim.cs` / `Shim.cs` / `Dump.cs` / `shimdrift.py` / `prodverify.py` 를
**바이트 단위로 복제**해 갖고 있었고 **동기화 검사가 없었다.** 한쪽만 고치는 순간
양성 대조가 본 하니스와 **다른 물건**을 재게 된다 — 그 상태의 양성 대조는 대조가 아니다.

지금은 `build.sh` 와 `prodverify.py` 가 **얇은 래퍼**(환경변수 두 개)이고,
`Tools/ShapeDump/shimdrift.py` 가 매 실행마다 **복제본이 되살아났는지** 확인한다
(이름이 아니라 겹치는 알맹이 줄 수로 판정한다 — 래퍼는 겹치지 않는다).
