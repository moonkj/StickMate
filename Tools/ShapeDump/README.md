# 프로덕션 도형·등급 덤프 (Tools/ShapeDump)

`design/equipment/verify/verify.py`는 **설계자가 적은 좌표**(`items.py` / `hair.py`)를 검산한다.
그것이 통과해도 **프로덕션 C#이 그 좌표를 실제로 만드는지는 아무도 확인하지 않는다** —
이 저장소가 반복해서 겪은 이중 정의 계열 실패가 정확히 그 틈에서 난다.

이 도구는 그 틈을 메운다. Unity 동봉 Roslyn으로 프로덕션 파일을 **한 줄도 고치지 않고** 컴파일해
30종의 좌표와 42종의 등급을 뽑고, **설계 하니스를 그대로 실행**해서 먹인다.

```bash
Tools/ShapeDump/build.sh              # 좌표 + 등급 (탭 구분)
python3 Tools/ShapeDump/prodverify.py # 설계 하니스로 전수 검산 → 종료코드 0 이어야 한다
python3 Tools/ShapeDump/shimdrift.py  # shim 이 프로덕션과 어긋났는지 → 종료코드 0 이어야 한다
```

## ★ 종료코드 (2026-09-02 변경)

이전에는 **위반이 있어도 `prodverify.py` 가 rc=0** 이었다(`verify.py` 에 `sys.exit` 가 없다).
게이트로 감싸는 사람이 그것을 모르면 "돌렸고 통과했다"가 그냥 "돌렸다"가 된다. 지금은:

| rc | 뜻 |
|---:|---|
| 0 | 위반 0건 |
| 1 | 위반 N건 · 컴파일 실패 · shim 어긋남 · 덤프가 `Debug.LogError` 를 냈다 · **위반 수를 못 읽었다** |

마지막 항목이 핵심이다. `verify.py` 의 집계 변수를 못 읽으면 그건 통과가 아니라 **판정 불능**이다.
`$?` 로 판정해도 되지만, 마지막 줄(`[prodverify] 위반 N건 -> 종료코드 …`)도 함께 읽어라.

## 구성

| 파일 | 역할 |
|---|---|
| `Shim.cs` | `UnityEngine` 최소 대역 — `Vector2/3` · `Mathf` · `Color`(HSV 포함) · `Mesh` · `Debug` · `ScriptableObject` · 직렬화 어트리뷰트 |
| `CoreShim.cs` | `StickMate.Core` **흉내 5종**. 여기 있는 것이 적을수록 하니스가 프로덕션에 가깝다 |
| `AssetShim.cs` | ★ Unity 직렬화기 대역 — `.asset`(YAML) → `ScriptableObject` **리플렉션 바인딩** |
| `Dump.cs` | `AccessoryShapeBuilder.Append` 30종 + `ItemCatalog.Rarity` 42종을 부르고 찍는다 |
| `build.sh` | Roslyn 컴파일 + 실행. **프로덕션 파일 6개를 그대로** 넣는다 |
| `prodverify.py` | `verify.py` 가 import 하는 `items`/`hair` 를 **덤프한 프로덕션 좌표**로 바꿔치기하고 그대로 실행 |
| `shimdrift.py` | shim ↔ 프로덕션 대조 — **상수 · enum 값 · 베낀 표 · 미등록 흉내 · 복제본** |

## ★ 왜 프로덕션 파일을 6개나 컴파일하는가 (2026-09-02)

NECK 6종의 **몸에 붙는 좌표가 코드에서 에셋으로 내려갔다**(B-2 파일럿). 그래서
`ItemCatalog.WornShapes` 를 스텁으로 흉내내면 덤프의 NECK 6종이 통째로 사라지고,
`mirrordrift`/`prodverify` 는 "설계 4도형 ≠ 프로덕션 0도형"만 뱉는 **거짓 빨간불**이 된다.
반대로 좌표를 shim 에 베껴 두면 그것이 곧 **세 번째 진실**이다.

그래서 흉내를 **로직이 아니라 로더 한 겹**으로 좁혔다:

- 스트림 문법을 읽는 것 → 프로덕션 `AccessoryWornShapeReader` 그대로
- 표를 만들고 등급을 파생하는 것 → 프로덕션 `ItemCatalog` 그대로
- 흉내내는 것 → `UnityEngine` · 카테고리 사실 5종 · **디스크 YAML 바인딩**뿐

`build.sh` 는 `STICKMATE_RESOURCES` 로 에셋 뿌리를 바꿀 수 있다. 합성 팩 에셋을 넣은 트리를
가리키면 **"팩이 붙었을 때 기본 42종의 등급이 움직이는가"를 프로덕션 코드로 직접 잴 수 있다.**

## 주의

- **검사 코드를 여기에 새로 쓰지 마라.** `prodverify.py`는 `verify.py`를 `exec`할 뿐이다.
- `Shim.cs`가 흉내내는 것은 **필요한 것뿐**이다. 프로덕션이 새 Unity API를 쓰면 컴파일이 실패한다 —
  그 실패가 곧 알람이다. **가능하면 shim 을 늘리지 말고 프로덕션 파일을 `build.sh` 목록에 넣어라.**
- `StickMate.*` 타입을 새로 흉내내려면 `shimdrift.py` 의 `SHIMMED` 표에 **이유와 함께** 등록해야 한다.
  등록하지 않으면 빨간불이다 — 그 한 줄이 "왜 실물을 안 쓰는가"를 사람에게 묻는 자리다.
- `@CLIP` 줄은 모자 6 × 머리 6 = 36조합의 **실제 클리핑 결과**다.
- `@RARITY` 줄은 42종 전수 등급이다. 등급에 영향을 줄 변경의 **전/후 diff 가 0줄**인지 보는 데 쓴다.
- `@LOG` 줄은 덤프가 낸 `Debug.LogError`/`LogWarning` 수다. **0이 아니면 그 실행의 모든 숫자는 무효다.**
