# ShapeDumpPC — `prodverify.py` / `mirrordrift.py` 의 **양성 대조 장치**

두 하니스가 "위반 0건 / 어긋남 0건"이라고 말할 때, 그 0이 **진짜 0인지** 아니면
**하니스가 죽어 있는지**를 가른다.

검사 코드는 한 줄도 고치지 않는다. **먹이는 프로덕션 좌표만** 모자 처방 이전(`7ab0468^`)의
`AccessoryShapeBuilder.cs` 로 바꿔치기한다. 하니스가 살아 있으면 반드시 빨간불이 나와야 한다.

## 사용법

```bash
cd /Users/kjmoon/App/StickMate
git show 7ab0468^:Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs \
  > Tools/ShapeDumpPC/AccessoryShapeBuilder.PARENT.cs

python3 Tools/ShapeDumpPC/prodverify.py                     # 빨간불이어야 한다
python3 Tools/ShapeDumpPC/mirrordrift_positive_control.py   # 빨간불 + 종료코드 1 이어야 한다
```

빨간불이 안 나오면 그 하니스의 **모든 "0건"을 무효로 선언한다**(docs/TEAM.md 4절 사고 #4).

## 주의

`prodverify.py` 는 위반이 있어도 **종료코드 0** 이다(`verify.py` 에 `sys.exit` 가 없다).
`$?` 로 판정하지 말고 마지막 줄(`★ 총 위반 N건`)을 읽어라.
