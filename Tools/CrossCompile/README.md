# Tools/CrossCompile — 크로스 컴파일 검사

    Tools/CrossCompile/xcheck.sh <win|osx> [--selftest]

Unity 에디터를 띄우지 않고 런타임 + 테스트 어셈블리를 **양 플랫폼 정의로** 컴파일한다.
CLAUDE.md의 "Windows 쪽을 건드렸으면 Roslyn 크로스 컴파일로 0에러 확인" 절차가 이 도구다.

컴파일하는 조합 (4개, 전부 통과해야 초록):

| 조합 | rsp | 무엇을 덮는가 |
|---|---|---|
| runtime(editor) | `1900b0aE.dag` | `UNITY_EDITOR` **켜짐** — 에디터/개발 빌드 경로 |
| runtime(player) | `1900b0aP.dag` | `UNITY_EDITOR` **꺼짐** — ★ 실제 출시 빌드 경로(`#else` 가지) |
| StickMate.Tests.EditMode | `1900b0aE.dag` | EditMode 테스트 |
| StickMate.Tests.PlayMode | `1900b0aE.dag` | PlayMode 테스트 |
| Assembly-CSharp-Editor | `1900b0aE.dag` | ★ asmdef 없는 `Assets/Editor/` (프리팹/씬 굽는 코드) |

## 이 도구가 낸 "거짓 초록" 5종 — 전부 자동 검사로 막았다

이전 스크립트들은 **아무것도 컴파일하지 않고 "에러 0"** 을 보고한 적이 세 번, 그리고
**출시 빌드 경로를 한 번도 안 본** 적이 한 번 있다. 네 번 다 "사람이 잘 읽으면 된다"로는 못 막혔다.

1. **깨진 csc 래퍼** — `MonoBleedingEdge/bin/csc` 는 빌드 머신 절대경로가 박혀 있어 실행이 실패하는데
   `grep -c "error CS"` 는 0을 센다.
   → 동봉 `dotnet` + `DotNetSdkRoslyn/csc.dll` 만 쓰고, **산출 DLL이 실제로 생겼는지** 확인한다.
2. **낡은 소스 목록** — rsp의 소스 목록은 마지막 에디터 컴파일 시점이라 신규 파일이 빠진다.
   → 소스는 항상 트리에서 `find` 로 재생성하고, **최소 개수**를 확인한다.
3. **rsp에 이미 박힌 플랫폼 정의** — 이 프로젝트의 빌드 타깃이 Windows라 rsp에 이미
   `UNITY_STANDALONE_WIN` / `PLATFORM_STANDALONE_WIN` 이 있다. 여기에 osx 정의를 "추가"만 하면
   둘 다 켜진 모순 조합이 되거나 요청 타깃이 실제로는 비활성이 된다.
   → 플랫폼 계열 정의를 **전부 제거 후 재주입**하고, 그 결과를 **카나리아 소스(`#error`)** 로
     컴파일러에게 직접 확인받는다.
4. **에디터 rsp만 사용** — `-define:UNITY_EDITOR` 가 늘 켜져 있어 `#if UNITY_EDITOR ... #else` 의
   **`#else` 가지가 한 줄도 컴파일되지 않는다**. 그 가지가 깨지면 사용자에게 나가는 빌드에서만 터진다
   (`Core/EquipmentDebugUnlock.cs` 의 릴리스 게이트가 정확히 이 형태다).
   → 런타임을 editor / player 두 번 컴파일한다.

5. **Editor 어셈블리 누락** — `Assets/Editor/` 는 asmdef이 없는 기본 Editor 어셈블리라 asmdef 기반
   목록에 잡히지 않는다. 여기 `SceneBootstrapper.cs`(프리팹/씬을 굽는 15만 자)가 있고 매 라운드
   편집된다. 이게 깨지면 Unity는 `Aborting batchmode due to failure: Scripts have compiler errors`
   로 **테스트를 한 건도 돌리지 못하는데**, 이 도구는 "전부 통과"를 냈다(2026-09-01 실측).
   → Runtime + Tests 2종을 컴파일한 뒤 **마지막에** 이 어셈블리까지 컴파일한다(셋을 전부 참조한다).

## 카나리아가 침묵할 가능성까지 막는다

카나리아가 소스 목록에 안 들어가면 그 자체가 다섯 번째 거짓 초록이다. 그래서:

* **항상**: 생성된 rsp에 카나리아 경로가 들어갔는지 확인한다(없으면 FATAL).
* **`--selftest`**: 일부러 **반대 타깃** 카나리아를 넣어 컴파일이 **반드시 실패**하는지 확인한다.
  통과해 버리면 "카나리아가 물지 않는다"는 뜻이므로 스크립트가 죽는다.

새 플랫폼 분기를 추가하는 라운드에서는 `--selftest` 를 붙여 돌리는 것을 권한다.

## 정의 계열 메모

원본 rsp에는 `UNITY_EDITOR_OSX`(이 개발 머신이 macOS)와 `UNITY_STANDALONE_WIN`(빌드 타깃이 Windows)이
**동시에** 있다. 둘은 원래 다른 축이라 모순이 아니다. 이 도구는 "그 플랫폼 개발자의 컴파일을 재현"하는
것이 목적이라 둘을 함께 뒤집는다 — Windows 개발자의 에디터는 `UNITY_EDITOR_WIN` 이고, 그 조합에서만
깨지는 코드가 실제로 있다.

산출물은 `Library/xcheck/<target>/` 에 떨어진다(`Library/` 는 gitignore 대상이라 트리를 더럽히지 않는다).
