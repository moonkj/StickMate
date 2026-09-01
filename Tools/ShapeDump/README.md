# 프로덕션 도형 덤프 (Tools/ShapeDump)

`design/equipment/verify/verify.py`는 **설계자가 적은 좌표**(`items.py` / `hair.py`)를 검산한다.
그것이 통과해도 **프로덕션 C#이 그 좌표를 실제로 만드는지는 아무도 확인하지 않는다** —
이 저장소가 반복해서 겪은 이중 정의 계열 실패가 정확히 그 틈에서 난다.

이 도구는 그 틈을 메운다. Unity 동봉 Roslyn으로
`Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs`를 **한 줄도 고치지 않고** 컴파일해
30종의 좌표를 뽑고, **설계 하니스를 그대로 실행**해서 먹인다.

```bash
Tools/ShapeDump/build.sh              # 좌표만 (탭 구분)
python3 Tools/ShapeDump/prodverify.py # 설계 하니스로 전수 검산 → "위반 0건"이어야 한다
```

## 구성

| 파일 | 역할 |
|---|---|
| `Shim.cs` | `UnityEngine`의 `Vector2/Vector3/Mathf/Color/Mesh` 최소 대역. **순수 수학뿐**이라 엔진 없이 돈다 |
| `CoreShim.cs` | `StickMate.Core`의 `EquipmentSlot` / `StickConfig` 상수 3개 |
| `Dump.cs` | `AccessoryShapeBuilder.Append`를 30종에 대해 부르고, 좌표를 **머리 중심 원점 · R 배수**로 되돌려 출력 |
| `build.sh` | Unity의 `DotNetSdkRoslyn/csc.dll`로 컴파일 + 실행 |
| `prodverify.py` | `verify.py`가 import 하는 `items`/`hair`를 **덤프한 프로덕션 좌표**로 바꿔치기하고 그대로 실행 |

## 주의

- **검사 코드를 여기에 새로 쓰지 마라.** `prodverify.py`는 `verify.py`를 `exec`할 뿐이다.
  검사를 두 벌로 적으면 두 자가 갈라지고, 그 순간 검산이 무엇을 재는지 아무도 모르게 된다.
- `Shim.cs`가 흉내내는 것은 **좌표 계산에 필요한 것뿐**이다. 빌더가 새 Unity API를 쓰기 시작하면
  컴파일이 실패한다 — 그때 shim을 늘리면 된다(그 실패가 곧 알람이다).
- `@CLIP` 줄은 모자 6 × 머리 6 = 36조합의 **실제 클리핑 결과**다(남은 조각 수 / 잘린 꼭대기).
