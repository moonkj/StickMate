# Tools/CrossCompile — 크로스 컴파일 검사

    Tools/CrossCompile/xcheck.sh <win|osx> [--selftest]

Unity 에디터를 띄우지 않고 런타임 + 테스트 어셈블리를 **양 플랫폼 정의로** 컴파일한다.
CLAUDE.md의 "Windows 쪽을 건드렸으면 Roslyn 크로스 컴파일로 0에러 확인" 절차가 이 도구다.

★ **정본은 `CLAUDE.md` 「플랫폼 동시 검토」 절이다**(리더 판정 2026-09-26). 이 문서가 그 절과 어긋나면 그 절이 맞다.
실측(2026-09-26): 이 저장소의 크로스 컴파일 러너 3개는 **전부** Unity 동봉 `dotnet` + `DotNetSdkRoslyn/csc.dll` 을 부른다 — Mono 시절의 `csc` 실행 파일을 부르는 러너는 **0개**다.
(그 낡은 파일 이름을 이 문서에 글자 그대로 적지 않는다. 「낡은 표기가 남아 있다」를 찾는 검색이 이 설명 줄에 걸려 오진한 적이 있다 — 2026-09-26 이 문서가 실제로 그렇게 오진됐다.)
`MonoBleedingEdge/bin/csc` 래퍼는 존재하지 않는 빌드 머신 절대 경로를 `exec` 해 실행 자체가 실패한다(아래 1번을 실측으로 재확인했다. 파이프 없이 재면 rc 126, 파이프로 재면 마지막 명령의 rc 0이 나와 「정상」처럼 보인다).

컴파일하는 조합 (4개, 전부 통과해야 초록):

★ 2026-09-26 실측 정정: 아래 표는 **5행**이고 실행도 **5유닛**이다(`osx`·`win` 각 5/5 errors=0). 이 「4개」는 `Assembly-CSharp-Editor` 행이 늘 때 갱신되지 않은 수다.

| 조합 | rsp | 무엇을 덮는가 |
|---|---|---|
| runtime(editor) | `1900b0aE.dag` | `UNITY_EDITOR` **켜짐** — 에디터/개발 빌드 경로 |
| runtime(player) | `1900b0aP.dag` | `UNITY_EDITOR` **꺼짐** — ★ 실제 출시 빌드 경로(`#else` 가지) |
| StickMate.Tests.EditMode | `1900b0aE.dag` | EditMode 테스트 |
| StickMate.Tests.PlayMode | `1900b0aE.dag` | PlayMode 테스트 |
| Assembly-CSharp-Editor | `1900b0aE.dag` | ★ asmdef 없는 `Assets/Editor/` (프리팹/씬 굽는 코드) |

## 누가 무엇을 쓰는가 (2026-09-26 perf-doc 실측)

| 러너 | 호출 컴파일러 | 대상 어셈블리 | 산출물 위치 | 저장소에 쓰는가 |
|---|---|---|---|---|
| `Tools/CrossCompile/xcheck.sh win` / `osx` | 동봉 `dotnet` + `DotNetSdkRoslyn/csc.dll` | 5개 — 런타임(editor·player) · EditMode · PlayMode · `Assembly-CSharp-Editor` | `Library/xcheck/<타깃>/` | 쓴다 — 단 `Library/` 는 gitignore 대상(`.gitignore:2`) |
| `Tools/CrossCompile/xcheck-editor.sh win` / `osx` | 같음 | 2개 — 런타임(editor) · `Assembly-CSharp-Editor` | `mktemp -d`(종료 시 삭제) | 쓰지 않는다 |
| `Tools/VerifyChange/xcheck_isolated.sh win` / `osx` + 출력루트 | 같음 | 5개 — `xcheck.sh` 와 같음 | 출력루트 밑 `<타깃>.<pid>/` | 쓰지 않는다 — 출력루트만 쓴다 |

`xcheck.sh` 가 2026-09-01부터 `Assembly-CSharp-Editor` 까지 컴파일하므로 `xcheck-editor.sh` 는 그 축소판이다(런타임이 먼저 깨졌는지만 빨리 보고 싶을 때 쓴다).
병렬 라운드에서는 격리판을 쓴다 — `Library/xcheck/<타깃>` 을 공유하지 않아 경합이 없다.
소스 수가 러너마다 1씩 다르게 보이는 것은 `xcheck.sh` 계열이 카나리아 1파일을 소스 목록에 더하기 때문이다(2026-09-26 실측: 런타임 285 대 284, 에디터 14 대 13).

## 이 도구가 낸 "거짓 초록" 5종 — 전부 자동 검사로 막았다

이전 스크립트들은 **아무것도 컴파일하지 않고 "에러 0"** 을 보고한 적이 세 번, 그리고
**출시 빌드 경로를 한 번도 안 본** 적이 한 번 있다. 네 번 다 "사람이 잘 읽으면 된다"로는 못 막혔다.
(★ 2026-09-26: 다섯 번째(Editor 어셈블리 누락)가 2026-09-01에 나와 **제목만** 「5종」으로 고쳐졌다. 이 문단의 「세 번 / 한 번 / 네 번」은 그때 갱신되지 않은 수이고, 정확한 수는 아래 목록의 **다섯 개**다.)

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
격리판 `Tools/VerifyChange/xcheck_isolated.sh` 는 인수로 받은 출력루트 밑 `<타깃>.<pid>/` 에만 쓴다 — 2026-09-26 marker 대조에서 실행 뒤 저장소 변경 0건이었다.
