# 밧줄 등반(RopeClimb) 구조 설계 — game-architect

작성: `game-architect` · 2026-09-07 · **순수 설계 문서(코드 미작성)**

> 사용자 신규 기획: *"높은 창이 있을때 줄같은걸 던져서 타고 올라가는것도 구현되어야함"*.
> 기존 파쿠르 등반(`States/ParkourClimbState.cs`, 손으로 짚고 기어오르기)으로는 못 오르는
> **더 높은 창/벽**을 만났을 때, 밧줄(갈고리)을 던져 타고 오르는 새 동작을 추가한다.

이 문서는 리더 지시 6개 항목에 순서대로 답한다. 각 결정에는 근거(기존 코드 실측 위치)를 붙였고,
game-architect 단독 판단 밖인 항목은 명시적으로 회부 대상을 적었다. **다음 라운드(design-motion →
coder)가 바로 착수할 수 있는 수준**을 목표로 했다.

---

## 0. 먼저 실측한 것 — 기존 파쿠르 시스템이 이미 어디까지 하고 있는가

설계에 들어가기 전에 반드시 알아야 할 사실 5가지(전부 코드 실측, 이번 라운드가 직접 읽음):

1. **높이 상한은 이미 존재하고 값도 있다.** `AutoWanderController.ResolveStepUpMaxHeight()`가
   `StickConfig.stepUpMaxHeights`(기본 1.0551H, `BaselineCharacterTotalHeight=2.2746944` 기준
   약 2.4 월드유닛)를 신장 배수로 환산하고, 실측 Dock 낙차가 그보다 크면 그 낙차+여유로 올려 쓴다
   (`DockGeometry.ResolveStepUpMaxHeight`). **이 상한을 넘는 벽은 지금도 감지는 되지만 아무 것도
   하지 않는다** — 아래 1절에서 정확한 코드 지점을 짚는다.
2. **벽 감지 자체(`GroundSensor.TryFindClimbableWall`)에는 높이 상한이 없다.** 최소 높이차
   (`parkourDetectionRadius`, 파쿠르 대상으로 인정할 최소 눈에 띄는 높이)만 검사하고, 그보다 높으면
   **얼마든지 높아도** 그 발판을 "벽"으로 반환한다(`GroundSensor.cs:439` — `if (topLeftWorld.y -
   info.GroundWorldY < detectionRadius) continue;`, 상한 비교문 없음). 즉 **"더 높은 창"을 구분하는
   검사는 이미 있고, 지금 안 쓰이고 있을 뿐이다.**
3. **`TryFindClimbableWall`은 이미 두 소비자에게 서로 다른 높이 대역으로 쓰이는 전례가 있다.**
   `TryFindDescendTarget`이 `minDropDepth`/`maxDropDepth`로 매달리기/뛰어내리기를 가르는 것과
   똑같은 패턴이 `TryFindClimbableWall`에는 아직 없다 — **이 설계가 그 빈 칸을 채운다**
   (아래 6절 "구조적 통찰").
4. **`WalkState.cs`에 이미 잠재 사고가 하나 있다**(이번 기능이 만드는 게 아니라 **먼저 닫아야 하는
   기존 결함**). `WalkState.cs:184`와 `:202`는 `TryFindClimbableWall`을 호출해 성공하면 **높이를
   전혀 재지 않고** 곧바로 `ParkourClimb`로 보낸다. 지금은 `:202`의 유일한 트리거인
   `wanderEdgeJumpAttemptChance` 기본값이 0이라(`AutoWanderController.cs:555`) 잠들어 있지만,
   누군가 그 값을 0보다 크게 올리는 순간 임의로 높은 벽도 `ParkourClimbState`의 고정 1.20초 Lerp로
   "엘리베이터처럼" 순간 상승해버린다. **이 기능 라운드가 반드시 함께 닫아야 하는 구멍이다**
   (6절 참고, `throwTumbleEnabled`류의 "지금은 잠들어 있지만 스위치 하나로 되살아나는" 패턴과 같은 가족).
5. **원거리 목표 탐색(걸어가서 던지기)은 이 앱에 전례가 없다.** 유일한 전례(`ArcheryState`의
   `Approach` 페이즈)는 "고정된 사대까지 걸어간다"이지 "발판 경계가 아닌 곳의 목표를 찾아간다"가
   아니다. 기존 등반/매달리기/뛰어내리기는 전부 "**지금 서 있는 발판의 경계에 도달했을 때만**"
   평가된다(`AutoWanderController.IsNearFootholdEdge`). 아래 1절 결정은 이 전례를 그대로 따른다.

---

## 1. 트리거 조건

### 1-A. 높이 대역 — 하한

**하한 = `AutoWanderController.ResolveStepUpMaxHeight()`의 반환값 그대로**(새 상수를 만들지 않는다).
기존 되올라가기가 담당하는 상한과 정확히 이어 붙인다 — 손으로 오를 수 있는 높이(파쿠르)와 밧줄이
필요한 높이(로프 등반) 사이에 **빈 구간도 겹침도 없어야** 한다("어느 쪽이 담당인지" 판정이 두 곳에서
따로 계산되면 반드시 다시 어긋난다 — 이 프로젝트가 이미 여러 번 겪은 실패 유형, 예:
`ResolveEffectiveEdgeBoundary` 사고).

### 1-B. 높이 대역 — 상한 (신규 결정)

**상한 = `min(ropeClimbMaxHeights × H, 화면 클램프 상단 − 여유 − 시작 Y)`.**

- `ropeClimbMaxHeights`(신규 `StickConfig` 필드, H 배수, `stepUpMaxHeights`와 같은 단위 관례)는
  **design-motion이 정할 판단값**이다(=game-architect 단독판단 밖 — `LightClimbHeights`/
  `HardClimbHeights`가 design-motion 판단값이었던 것과 같은 성격, `ParkourClimbState.cs:80-87`).
- **화면 클램프 상단은 새로 계산하지 않는다.** `StickmanBlackboard.ComputeScreenClampOsBounds()`가
  이미 계산해 두는 `MinY`(화면 상단 하드 클램프 경계, `StickmanBlackboard.cs:1637` 부근)를
  월드로 역변환하는 조회 메서드 하나만 추가한다(`TryGetWalkableScreenTopWorldY` — 기존
  `TryGetWalkableScreenBoundsWorld`가 좌우 경계에 하는 것과 똑같은 일을 세로로 한 번 더 하는 것뿐).
  ★ **이유가 중요하다**: 이 프로젝트는 멀티모니터에서 "발판 통합 경계"(`GroundInfo.Screen*WorldX`)를
  화면 경계로 오인해 러닝머신 버그를 두 번 겪었다(`AutoWanderController.ResolveEffectiveEdgeBoundary`
  문서, 2026-08-29 최초 수정 → 2026-09-02 멀티모니터 재발). **세로축에서 같은 실수를 반복하지
  않으려면 처음부터 "발판 목록 전체의 최고 Y"가 아니라 "이 오버레이 화면 자체의 하드 클램프
  경계"를 상한으로 써야 한다.** 목표 창이 아무리 높아도, 캐릭터가 실제로 서 있을 수 있는 화면
  범위 밖으로는 등반이 끝나서는 안 된다(끝나면 캐릭터가 자기 오버레이 화면 밖에서 사라진 것처럼
  보인다).
- 두 상한 중 **더 작은 쪽**이 이긴다(안전한 쪽으로 좁히는 이 프로젝트의 기존 관례 —
  `ResolveEffectiveEdgeBoundary`가 통합 경계와 클램프 한계 중 더 좁히는 쪽을 쓰는 것과 같은 어법).

### 1-C. "내려갈 곳이 없을 때만" 인가, 독립 우선순위인가

**기존 되올라가기(3순위)의 바로 다음 하위 대역으로 끼워 넣는다. 독립 우선순위로 만들지 않는다.**

근거: `AutoWanderController.TryRollEdgeAction()`의 우선순위 1·2(뛰어내리기/매달리기)가 먼저 평가되고
실패해야만 3순위(되올라가기)가 평가되는 이유는 "아래로 갈 수 있는 자리에서 위를 함께 보면 한 경계에서
방향이 왔다갔다한다"는 것이다(`AutoWanderController.cs:726` 주석). **이 이유는 벽이 손으로 오를 수
있는 높이든 밧줄이 필요한 높이든 완전히 동일하게 적용된다** — 밧줄 등반도 결국 "위로 가는" 선택지의
연장선이지 성격이 다른 행동이 아니다. 독립 우선순위로 빼면 "내려갈 수 있는데도 로프를 던지는" 그림이
가능해지고, 그건 사용자가 "재미있는 자율 배회"로 받아들일 근거가 없는 부자연스러운 판단이다.

**정확한 배치**: `TryRollEdgeAction()`의 기존 되올라가기 블록(`AutoWanderController.cs:772-786`)을
다음과 같이 확장한다(기존 로직은 **한 줄도 바꾸지 않는다** — `else if`로만 이어 붙인다):

```
// 기존 코드 그대로:
if (wallHeight <= maxHeight) { ... 되올라가기 ...; return true; }
// ▼ 신규 추가 — 되올라가기가 못 미치는 벽만 여기로 떨어진다
else if (wallHeight <= ResolveRopeClimbMaxHeight())
{
    // ropeClimbChance로 **별도** 추첨(스텝업 확률과 독립 — Archery의 hitChance/bullseyeChance가
    // 같은 난수를 재사용하지 않는 것과 같은 이유: 재사용하면 "스텝업 확률이 높아지면 로프도
    // 덩달아 자주 나온다"는 의도치 않은 결합이 생긴다).
    if (ropeClimbChance > 0f && _rng.NextDouble() < ropeClimbChance)
    {
        _ropeClimbRequestedThisTick = true;
        ...
        return true;
    }
}
// 어느 쪽도 못 맞으면 기존처럼 그대로 흘러간다(정지 후 반대 방향) — 새 "포기 연출"은 만들지 않는다.
```

이 배치의 장점: `_edgeActionRolledThisLeg`(한 걷기 구간당 추첨 1회 제한), `descendSuppressed`(되올라간
직후 유예) 같은 기존 안전장치를 **전부 그대로 물려받는다** — 새 코드가 새 버그 클래스를 열 여지가
구조적으로 좁다.

### 1-D. 자율 AI vs 유저 조작 — "첫 번째 구조적 결정"

**권고: 자율 AI 판단(기존 철학 그대로 유지). 유저 조작을 새로 여는 첫 사례로 만들지 않는다.**

근거:
1. `IMovementIntentSource.cs` 문서가 명시하는 구조적 이유가 그대로 적용된다 — 실제 오버레이 창은
   `WS_EX_NOACTIVATE`라 키보드 포커스를 받을 수 없고(Win32WindowService.cs), 이 프로젝트 전체에
   "새 유저 트리거를 여는" 선례가 **단 한 건도 없다**. 드래그&던지기(`DragThrowState`)와 로데오
   커서(`RodeoCursorState`)가 유일한 마우스 상호작용인데, 둘 다 "캐릭터를 붙잡는" 조작이지
   "새 능동 행동을 명령하는" 조작이 아니다 — 밧줄 등반처럼 "이 벽을 오르라"는 목표 지정 명령과는
   성격이 다르다.
2. **직접 전례가 있다**: 활쏘기(`ArcheryState`)도 사용자가 "과녁이 생성되고 활을 쏘는 행동"을
   요청했지만 유저 조작이 아니라 **자율 발동 스펙터클**로 구현됐다(트리거는 `ArcheryDirector`의
   유휴 판정). 밧줄 등반 요청도 문장 형태가 같다("~하는 것도 구현되어야 함" = 캐릭터의 새로운
   행동 레퍼토리 추가 요청이지, "내가 조작하고 싶다"는 요청이 아니다).
3. 파쿠르/매달리기/되올라가기/뛰어내리기 전부가 `IMovementIntentSource`의 1프레임 펄스 계약을
   따르는 자율 판단이다. 밧줄 등반만 유저 조작으로 만들면 **같은 화면에서 같은 종류의 동작(위로
   가는 것)이 절반은 자동, 절반은 수동**이 되어 사용자가 "왜 이건 내가 시켜야 하지"라는 혼란을
   겪을 근거가 생긴다.

**이 결정을 뒤집을 신호가 있다면**: 사용자가 "내가 방향을 조준해서 던지고 싶다"는 명시적 요구를
하는 경우뿐이다. 그 경우는 이 문서의 범위를 넘는 **되돌릴 수 없는 결정**(새 입력 체계 신설)이므로
리더가 사용자에게 재확인해야 한다. **지금은 그런 요구가 없으므로 자율 판단으로 확정한다.**

새 계약: `IMovementIntentSource`에 `bool RopeClimbRequested { get; }` 1프레임 펄스 멤버 추가
(`StepUpRequested`와 완전히 동일한 契約 형태 — 왜 기존 펄스를 재사용하지 않는지도 같은 이유:
소비자가 "이건 손 등반이 아니라 밧줄 등반이다"를 구분해야 다른 상태로 보낼 수 있다).

---

## 2. 상태머신 배치

**결론: `ParkourClimbState`를 확장하지 않고 새 상태 `States/RopeClimbState.cs`를 만든다.**
`StickmanStateId.RopeClimb = 28`(현재 최댓값 `Dance = 27` 다음 번호,
`Core/StickmanEventBus.cs`).

### 2-A. 왜 확장이 아니라 신규인가 — 직접 전례로 논증

이 프로젝트에는 정확히 이 질문에 대한 판례가 이미 있다: **`LedgeHangState`를 `ParkourClimbState`에
모드 플래그로 합치지 않고 별도 상태로 분리한 결정**(`StickmanEventBus.cs:118` 주석)이다. 그 결정의
이유를 그대로 대입해보면:

> *"두 상태가 공유할 코드가 사실상 '발판 핸들 재확인' 한 줄뿐이다 ... 모드 플래그를 넣으면 거의
> 모든 줄에 분기가 생겨 이미 검증된 등반 경로까지 회귀 위험에 노출된다."*

밧줄 등반과 `ParkourClimbState`를 대조하면 **공유 가능한 줄이 그보다도 적다**:

| 항목 | ParkourClimbState (기존) | RopeClimb (신규 요구) | 공유 가능? |
|---|---|---|---|
| 진행 방식 | `Mathf.Lerp(startY, wallTopY, progress)` 단일 보간 | 높이가 임의로 크므로(6.6H도 가능) **고정 시간 Lerp 불가** — 속도 기반 진행(아래 2-C) | ✗ — 진행 계산식 자체가 다르다 |
| 페이즈 수 | 1개(등반만) | 2개(던지기 + 오르기) — 리더 브리핑이 이미 지목한 그 구분 | ✗ — 페이즈 머신 자체가 다르다 |
| 대사 티어 임계값 | `0.95H`/`2.2H`(손 등반 스케일) | 이미 그보다 높은 벽만 대상이므로 **같은 임계값을 재사용하면 티어가 항상 최상위로 수렴** — 무의미 | ✗ |
| 실패 시 화면 | "잡을 곳 사라짐 → 즉시 Fall"(공중에서 손을 잡고 있던 중) | Throw 단계는 **아직 땅 위**(1-E 참고) — 실패해도 Fall이 아니라 Idle/Walk | ✗ — 실패 규칙 자체가 다르다(3절) |
| 렌더링 소유 | 상태가 직접 `ApplyParkourClimbPose` 호출 | 던지는 밧줄/갈고리는 `ArcheryRenderer`처럼 **별도 렌더러**가 필요(투사체 궤적) | △ — 이벤트 발행 패턴만 공유 |

공유 가능한 것은 딱 두 가지 **아이디어**(코드 자체가 아니라 관례)뿐이다 — 매 프레임 `TryGetFootholdTopWorldY`
재확인(창이 움직이면 목표도 같이 움직인다), 그리고 맨틀 인셋 계산(`ParkourMantleInsetWorld`,
아래 6절에서 **그대로 재사용**을 권고). 코드를 상속/합성으로 공유하는 게 아니라, **같은 정적 유틸
(`GroundSensor`)과 같은 블랙보드 프로퍼티를 양쪽이 각자 호출**하는 것으로 충분하다 — 이미 파쿠르와
매달리기가 그렇게 하고 있다.

### 2-B. 페이즈 구조 — 리더가 이미 지목한 2단계를 그대로 채택

리더 브리핑의 가설("던지기 단계 + 매달려 오르는 단계, 두 단계로 나뉜다면 그 자체가 별도 상태를
정당화")이 실측으로 확인됐다. `ArcheryState`의 페이즈 머신(`Approach/Intro/Draw/Aim/Recover/Outro`)이
가장 가까운 전례이므로 같은 어법으로 설계한다:

```
enum Phase { Throw, Ascend }
```

- **`Throw`**: 캐릭터가 제자리에 서서 밧줄/갈고리를 던진다. `ArcheryState.Phase.Draw/Aim/Recover`와
  같은 성격(당기기→조준→발사 대신 감기→던지기→(명중 확정))이지만 **1회**뿐이라 `ShotCount` 같은
  반복 카운터가 필요 없다. 이 단계 동안 캐릭터는 이동하지 않으므로 `ArcheryState.Approach`처럼
  `IsHorizontalMotionSelfManaged`를 켤 필요가 없다 — `ParkourClimbState.Enter()`가 이미 하듯
  진입 시 `Body.linearVelocity`를 0으로 고정하는 것으로 충분하다.
  - "명중/실패는 물리에 맡기지 않는다"(리더의 기존 지시, `ArcheryState.cs:22-28` 원칙)를 그대로
    따른다 — 갈고리가 걸릴 자리는 **`Enter()`에서 이미 확정된 `wallHandle`의 가장자리**이지 던지는
    각도의 물리 시뮬레이션 결과가 아니다. 실패 조건은 "명중 여부"가 아니라 "**목표가 여전히
    존재하는가**"뿐이다(3절).
- **`Ascend`**: 밧줄을 타고 오른다. 진행 방식은 2-C 참고.

### 2-C. Ascend의 진행 시간 — 고정 Lerp를 재사용하면 안 되는 이유

`ParkourClimbState`는 `parkourClimbDuration`(고정 1.20초)로 시작 Y→벽 상단 Y를 무조건 1.2초 만에
보간한다. 이게 지금까지 문제없었던 이유는 **높이 상한이 걸려 있어서**(최대 2.2H 안팎)다 — 즉
"1.2초 만에 최대 2.2H를 오른다"는 항상 합리적인 속도 범위 안에 있었다.

밧줄 등반은 정의상 그보다 높은 벽(최대 6.6H급, 1-B의 design-motion 판단값)을 대상으로 한다. **같은
고정 시간을 쓰면 안 된다** — 6.6H를 1.2초에 오르면 손 등반보다 3배 빠르게 하늘로 솟아 "밧줄" 연출의
설득력이 오히려 손 등반보다 낮아진다. 반대로 낮은 벽(2.5H쯤, 하한 바로 위)까지 긴 고정 시간을 쓰면
질질 끄는 느낌이 된다.

**결정: 속도 기반 진행 — `climbDurationSeconds = climbHeight / (ropeClimbSpeedHeightsPerSecond × H)`.**
이건 새 패턴이 아니라 **`WalkState`가 이미 쓰는 패턴을 세로축에 그대로 옮긴 것**이다(`WalkState`는
고정 "걷기 시간"이 아니라 `ResolveWalkSpeed()`로 속도를 정하고 거리는 저절로 따라온다). `H`를 곱하는
이유는 이 프로젝트의 기존 관례(신장 배수 단위)를 그대로 따르기 위해서다 — 캐릭터 크기 다이얼을
키우면 오르는 속도도 함께 커져야 한다는 뜻(`WalkState.ResolveWalkSpeed`가 보폭을 배율에 비례시키는
것과 같은 이유).

`ropeClimbSpeedHeightsPerSecond`도 **design-motion 판단값**이다(game-architect는 "속도가 아니라
거리/시간 중 무엇으로 진행을 표현할지"라는 **구조**만 정한다).

---

## 3. 인터럽트 규칙

### 3-A. Ragdoll 강제 인터럽트 — 새 코드가 필요 없다(구조적으로 이미 커버됨)

`RagdollImpactResolver`의 클래스 문서(정본 목록의 유일한 위치, `States/RagdollImpactResolver.cs`)가
명시한다: *"이 정적 유틸 ... StickmanAgent.ReportExternalImpact()도 내부적으로 이 메서드를 호출...
RagdollImpactResolver는 상태 목록을 보지 않으므로 새 상태가 자동으로 커버된다"*
(`StickmanStateMachine.cs` 전이 규칙 주석 인용). 즉 **`RopeClimb`을 어디에도 등록하지 않아도
외력 임계값(`ragdollForceThreshold`)을 넘으면 자동으로 Ragdoll로 인터럽트된다.**

★ **다만 이 문서 자체가 "6개 파일에 같은 목록이 복사됐다가 전부 낡았다"는 사고를 명시적으로
경고한다.** 그러므로 `RopeClimbState`의 클래스 문서에는 **목록을 다시 적지 말고**
`RagdollImpactResolver` 클래스 문서를 가리키기만 하라 — 이번 라운드가 7번째 낡은 사본을 만들지
않기 위한 유일한 방법이다.

### 3-B. 목표를 잃었을 때 — 페이즈별로 다른 안전 착지가 필요하다 (신규 판단)

기존 파쿠르는 "잡을 곳이 사라지면 즉시 Fall" 한 가지 규칙뿐이다(공중에서 손을 잡고 있던 상태이므로
당연히 낙하). **밧줄 등반은 두 페이즈의 물리적 상태가 다르므로 규칙도 갈라야 한다:**

| 페이즈 | 캐릭터의 실제 위치 | 목표(벽) 소실 시 처리 | 근거 |
|---|---|---|---|
| `Throw` | **아직 땅 위**(원래 발판에 접지 중, 손만 뻗어 던지는 동작) | **Idle/Walk로 취소**(발판은 그대로 있으므로 낙하할 이유가 없다) | 창이 물리적으로 캐릭터를 지지하고 있지 않은데 Fall로 보내면, 있지도 않은 낙하가 "허공에서 갑자기 떨어짐"으로 보인다 — `UX_FLOW.md` 4절의 금지 사항("잡으려다 허공에 붕 뜬 채 멈춤")과 정확히 대칭인 반대쪽 오류다 |
| `Ascend` | **공중**(밧줄에 매달려 오르는 중, 발판에서 완전히 이탈) | **즉시 Fall**(`ParkourClimbState.Tick()`과 100% 동일 규칙, `TryGetFootholdTopWorldY` 재확인 실패 시) | 손 등반과 물리적으로 같은 처지 — 이미 검증된 규칙을 그대로 재사용하면 된다 |

이 구분은 **원칙 3(유저 자산 불변)과도 맞물린다**(4절 참고) — "아직 던지지 않은 밧줄"은 시각적으로도
아무 것도 다른 창에 붙어있지 않으므로 취소해도 자연스럽고, "이미 걸려서 오르던 밧줄"이 사라지는
것은 "그 창이 실제로 닫히거나 이동해 갈고리가 헛돎"으로 자연스럽게 읽힌다(창을 우리가 움직인 게
아니라 창이 스스로 사라진 것 — 정확히 파쿠르가 이미 그렇게 하고 있는 방식).

`Enter()`/`Tick()` 각각에서 `TryGetFootholdTopWorldY(wallHandle, ...)`를 재확인하는 지점은
`ParkourClimbState.Tick()`의 대응 지점(`ParkourClimbState.cs:336`)과 동일한 자리에 두면 된다 —
**단, 실패 시 분기만 페이즈로 나눈다**(`_phase == Throw ? Idle/Walk : Fall`).

### 3-C. 맨틀 완료 신호 — 반드시 재사용해야 하는 이유 (구조적 함정 사전 차단)

`RopeClimbState`가 등반을 완료하면 **반드시 `_blackboard.ReportClimbMantleCompleted(direction)`을
호출해야 한다**(`ParkourClimbState.cs:387`과 동일). 이걸 빠뜨리면 정확히 2026-08-29에 이미 한 번
고쳤던 버그가 **다른 옷을 입고 재발**한다:

> 캐릭터가 밧줄로 6H를 올라간 직후, 배회 AI가 "방금 올라섰다"는 사실을 모른 채 그 경계에서 다시
> 경계 판정을 돌리면 `TryFindDescendTarget`(매달리기)이나 `TryFindHopDownTarget`(뛰어내리기)이
> **방금 6H를 들여 올라온 그 낙차를 향해 곧바로 다시 뛰어내리라고 판단할 수 있다.**

`postClimbDescendCooldown`(기존 필드, `AutoWanderController.ConsumeClimbMantleSignalIfAny` 소비)이
이미 이 정확한 시나리오를 막는 장치이지만, **그 장치는 `ClimbMantleSequence` 카운터 증가에만
반응한다.** `RopeClimbState`가 자기만의 새 신호를 만들면(예: `RopeClimbMantleSequence`) 배회 AI가
그 신호를 모르는 채로 남고, 손 등반에서 이미 고친 버그가 로프 등반에서 **처음부터 다시** 난다.
**반드시 기존 신호를 재사용해라 — 새로 만들지 마라.**

### 3-D. 확인이 더 필요한 경로 (이번 라운드 범위 밖, 다음 라운드에 위임)

**드래그(`DragThrowState`) 시작이 `Throw`/`Ascend` 도중에 끼어들 수 있는가는 이번 조사로 확인하지
못했다.** `DragThrowState`가 마우스다운을 언제 어디서 가로채는지(전역 훅인지, 특정 상태에서만
유효한지)는 이 라운드가 읽은 파일 범위 밖이다. **coder/debugger가 구현 착수 전에 확인해야 할 항목으로
명시적으로 남긴다** — 확인 없이 "당연히 Ragdoll처럼 아무 때나 인터럽트될 것"이라고 가정하지 말 것
(이 저장소의 반복 교훈 — 확인 못 한 것은 미확인으로 적는다).

---

## 4. 원칙 3(유저 자산 불변) 검토

**결론: 기존 파쿠르와 완전히 같은 패턴이므로 새 위험이 없다 — 단, 3가지를 명시적으로 금지해 둔다.**

기존 파쿠르가 이미 증명한 안전한 패턴: `ParkourClimbState`는 "벽"으로 삼은 `PlatformFoothold`의
`ScreenRect`(읽기 전용 열거값)를 좌표 계산에만 쓰고, 그 창을 옮기거나 크기를 바꾸거나 포커스를
주는 API는 **한 번도 호출하지 않는다**. 캐릭터가 "창을 붙잡고 오르는 것처럼" 보이는 건 순전히
IK 손 포즈가 그 창의 가장자리 좌표를 향해 뻗기 때문이다(`DriveClimbPose`, `nearEdgeWorldX` 인자).
밧줄 등반은 **정확히 같은 원리**를 쓴다 — 갈고리가 "박히는" 자리도 같은 `ScreenRect`의 좌표일 뿐이고,
창은 우리가 계산에 쓴다는 사실조차 모른다.

**그래도 이 기능이 새로 열 수 있는 위험 3가지를 미리 막아 둔다** (밧줄은 파쿠르보다 "물리적으로
뭔가에 걸린다"는 인상을 훨씬 강하게 주므로, 구현자가 무심코 원칙을 넘을 유혹이 더 크다):

1. **금지 — 목표 창을 앞으로 가져오기(z-order/포커스 API 호출).** "밧줄이 걸리는 순간 그 창이
   화면 맨 앞으로 온다"는 연출이 그럴듯해 보일 수 있지만, 이는 창 상태를 실제로 바꾸는 것이라
   원칙 3 위반이다. `FootholdPoller`가 이미 캐시해 둔 z-order **그대로**를 받아들인다 — 그 창이
   가려져 있으면 갈고리도 가려진 채로 그려지면 된다(실제로 안 보이는 것 자체가 정직한 그림이다).
2. **금지 — 목표 창에 "흔들림"/"당겨짐" 시각 효과를 얹기.** 밧줄이 창을 당기는 것처럼 창 자체를
   흔들거나 살짝 움직이면 원칙 3의 "실제 파일/아이콘/타 윈도우는 절대 이동·수정하지 않는다"를
   정면으로 위반한다. 흔들 것이 있다면 **밧줄(우리 오버레이 위 그림)과 캐릭터**뿐이다.
3. **금지 — 목표를 "우리 것"처럼 계속 붙잡아두는 숨은 상태.** 등반이 끝나거나 취소되면 그 순간
   갈고리/밧줄 렌더러도 즉시 사라져야 한다(3-B의 두 취소 경로 모두). 창이 닫혔는데 밧줄 그림만
   허공에 남아있으면, 사용자에게 "우리 앱이 그 창에 뭔가를 걸어뒀다"는 잘못된 인상을 준다 — 이건
   시각적 버그이자 원칙 3이 지키려는 신뢰(우리는 아무것도 실제로 건드리지 않는다)를 깨는 것이다.

**렌더링 소유권**: `ArcheryRenderer`/`ArcheryDirector` 분리 관례를 그대로 따른다 — `RopeClimbState`는
"지금 어느 좌표를 향해 던지고 있다/걸려 있다"는 **사실**만 이벤트로 발행하고(`StickmanEventBus`에
`RopeClimbOverlayChanged` 류 신설 권고), 밧줄 라인/갈고리 스프라이트를 그리는 것은 별도
`Interaction/RopeClimbRenderer.cs`가 맡는다. 이 분리 자체가 원칙 3을 지키는 데도 도움이 된다 —
"이 상태가 창에 대해 아는 것은 좌표 하나뿐"이라는 사실이 코드 구조로 강제된다.

---

## 5. 원칙 1(행동-텍스트 싱크) 고려

**판단: 대사가 필요하다.** (design-narrative 영역이므로 여기서는 이 판단만 내리고 실제 대사는
쓰지 않는다.)

근거:
1. **직접 전례** — `ParkourClimbState`는 오를 높이를 신장 배수로 나눠 3티어 대사(가뿐하네/영차.../
   헉... 높다)를 이미 갖고 있다(`ParkourClimbState.cs:245-289`). 밧줄 등반은 그 티어 체계가 이미
   담당하지 못하는 **더 극단적인 높이**(정의상 `HardClimbHeights`=2.2H보다 높은 벽만 온다)를
   대상으로 하므로, "이건 손으로는 안 되겠다"는 순간(Throw 진입)과 "정말 오래/힘들게 오른다"는
   진행(Ascend) 양쪽 다 기존 티어보다 강한 감탄이 자연스러운 자리다.
2. **활쏘기의 반례도 참고할 가치가 있다** — `ArcheryState`는 "요청하지 않은 자율 연출/대사에
   반복적으로 민감했던" 사용자 이력을 근거로 **의도적으로 대사를 넣지 않았다**(`ArcheryState.cs:76-82`).
   즉 "새 스펙터클 = 무조건 대사"가 아니라는 것도 이 저장소의 정당한 선례다. **최종 판단은
   design-narrative의 몫**이되, 손 등반과의 티어 연속성(더 높은 벽일수록 더 강한 반응) 쪽이
   더 자연스럽다는 것이 이 라운드의 의견이다.
3. **반드시 지켜야 할 제약(구조적으로 강제되는 것)**: 대사를 넣는다면 `ParkourClimbState`가 이미
   겪은 **두 가지 함정을 반드시 피해야 한다**:
   - **자율 예산 게이트 재사용** — `ParkourClimbState.TryRollClimbChatter()`가 쓰는 공유 쿨다운
     (`StickmanBlackboard.NextChatterAllowedUnscaledTime`)을 **그대로** 읽고 써야 한다. 새 타이머를
     만들면 "앰비언트 직후 0.7초 만에 등반 대사"처럼 이미 한 번 고친 문제가 재발한다(같은 파일
     `TryRollClimbChatter` 문서의 실측 경고). 확률도 무조건(=1.0)이 아니라 설정 가능한 확률 +
     쿨다운 조합이어야 한다(같은 문서 "등반은 사용자가 유발한 사건이 아니다" 경고 — 밧줄 등반도
     똑같이 배회 AI의 부산물이다).
   - **문안을 배열/상수로 빼지 않는다** — `DialogueCorpus.ExtractSayReact`/`golden_gen.py`가 인라인
     리터럴만 스캔한다(`ParkourClimbState.cs:232-237` 경고). `DialogueLine.Say("...")` 호출을
     함수 안에 직접 쓰는 형태를 그대로 따라야 회귀 검사가 새 대사를 놓치지 않는다.
4. **길이 예산 주의** — Throw 페이즈는 짧지만(활쏘기의 Draw~Aim 정도), Ascend는 `ropeClimbSpeedHeightsPerSecond`에
   따라 수 초에서 길게는 십수 초까지 걸릴 수 있다. 대사 노출 시간이 그 상태 지속보다 길면 잘리는
   문제(인계 계약 표 "design-narrative → design-motion")가 여기서도 그대로 적용되므로, Ascend
   중 대사를 넣는다면 **한 번에 다 말하지 않고 페이즈 진행에 걸쳐 여러 줄로 나누는 것**(활쏘기의
   샷마다 이벤트를 발행하는 것과 같은 어법)을 design-narrative에 권고한다.

---

## 6. 기존 시스템과의 충돌 지점

### 6-A. 구조적 통찰 — `TryFindClimbableWall`을 높이로 대역화하는 것이 이 기능의 핵심

이 설계 전체에서 가장 중요한 한 문장: **`GroundSensor.TryFindDescendTarget`이 이미 `minDropDepth`/
`maxDropDepth`로 매달리기와 뛰어내리기를 한 함수로 갈라 쓰고 있는 것과 정확히 같은 방식으로,
`TryFindClimbableWall`의 결과를 손 등반/밧줄 등반으로 높이 대역화한다.** (`TryFindDescendTarget`
문서의 표현을 그대로 빌리면: *"이 한 줄이 곧 매달리기와 뛰어내리기를 가르는 유일한 기준이며,
호출부가 밴드를 정한다"* — `GroundSensor.cs:511`.) 이번 기능은 **`GroundSensor` 자체를 한 줄도
바꾸지 않는다** — `TryFindClimbableWall`은 이미 상한 없이 벽을 반환하므로, 대역을 가르는 책임은
전부 **호출부**(`AutoWanderController.TryRollEdgeAction`, 그리고 아래 6-B에서 다루는 `WalkState`)에
있다. 이는 최소 변경 면적을 위한 선택일 뿐 아니라, `GroundSensor`가 "발판 사실 조회"만 담당하고
"어떤 사실을 어떤 행동으로 연결할지"는 상위 레이어가 정한다는 이 프로젝트의 기존 계층 분리를
그대로 지키는 것이기도 하다.

### 6-B. 반드시 함께 닫아야 하는 기존 구멍 — `WalkState`의 무제한 높이 경로

0절 4번에서 이미 지목한 것을 여기서 처방으로 정리한다. `WalkState.cs`의 두 지점
(`:184` 되올라가기 펄스 경로, `:202` 점프 펄스 경로)이 `TryFindClimbableWall`을 호출해 성공하면
**높이를 재지 않고** 무조건 `ParkourClimb`로 보낸다. 이 기능이 착수되면 두 지점 모두 **같은 대역
판정을 거치도록** 고쳐야 한다:

```
높이 <= ResolveStepUpMaxHeight()         → ParkourClimb (기존 그대로)
그 초과 && <= 로프 상한(1-B) && 조건 충족 → RopeClimb (신규)
그 밖                                     → 아무 것도 안 함(현재 동작 유지 — 이미 그렇다)
```

★ **왜 "이번 기능이 반드시" 닫아야 하는가**: 지금은 `wanderEdgeJumpAttemptChance=0`이라 `:202` 경로가
잠들어 있어 증상이 없다. 하지만 밧줄 등반이 배포된 뒤 **누군가 그 확률을 0보다 크게 튜닝하면**,
`WalkState`가 로프 등반의 존재를 모른 채 여전히 무조건 `ParkourClimb`로 보내 "높은 벽을 1.2초 만에
Lerp로 순간 상승"하는 새 시각적 결함이 생긴다. 이건 이번에 새로 만드는 버그가 아니라 **이미 있던
잠재 결함이 이번 기능으로 인해 처음으로 관측 가능해지는 경우**다 — `throwTumbleEnabled`/
`longCapeTripMeanSeconds=0`류의 "지금은 안 돌지만 스위치 하나면 산다"는 이 저장소의 익숙한 패턴과
같은 가족이며, **알면서 방치하면 CLAUDE.md가 요구하는 정직성 규칙("확인 못 한 것은 미확인으로") 위반이
아니라 "확인했는데 안 고침"이 된다.**

### 6-C. 사거리 — 원거리 목표가 아니라 "지금 서 있는 경계"

밧줄 등반은 **먼 창을 향해 캐릭터가 미리 이동한 뒤 던지는 것이 아니다.** 트리거 자체가 기존
파쿠르/되올라가기와 똑같이 "지금 딛고 있는 발판의 경계 근처"(`IsNearFootholdEdge`, 판정 거리는
`EdgeStopDistanceWorld`)에서만 평가되므로, 벽은 **항상 캐릭터 바로 옆**에 있다. 따라서:

- **탐색 폭은 기존 상수를 그대로 재사용한다** — `GroundSensor.AdjacentFootholdSearchRadiusMultiplier`
  (≈82pt, `parkourDetectionRadius`의 4배)와 `edgeProbeReach`(`DockGeometry.ResolveEdgeProbeReach`).
  **새 사거리 상수를 만들지 않는다.** "던지기"라는 이름 때문에 활쏘기 같은 원거리 사거리
  (`archeryTargetDistanceRatio` 4.6H)를 연상하기 쉽지만, 이 기능의 실제 형태는 **거의 수직에
  가까운 짧은 던지기**(캐릭터 바로 앞 벽을 향해)이지 활쏘기 같은 원거리 포물선이 아니다.
- **"멀리 있는 초고층 창을 발견하고 일부러 걸어가서 로프를 쏜다"는 이번 기능의 범위 밖이다.**
  이건 활쏘기의 `Approach` 페이즈처럼 "목표를 찾아 이동하는" 완전히 다른 행동 패턴이 필요한
  **더 큰 기능**이며, 기존 등반/매달리기/뛰어내리기 전부가 이 "목표 탐색형" 행동을 갖고 있지 않다
  (전부 기회주의적·경계 도달형이다). 일관성을 위해 이번 기능도 같은 기회주의적 모델을 따르고,
  목표 탐색형 확장은 **별도 기획으로 다음에 검토**할 것을 권고한다.

### 6-D. 맨틀 인셋 — 새 상수를 만들지 말고 재사용

등반 완료 후 "턱 위 안쪽으로 얼마나 들어가 서는가"(`ParkourMantleInsetWorld`,
`StickmanBlackboard.cs`)는 물리적으로 손 등반이든 밧줄 등반이든 **같은 이유**(경계 판정 거리에서
유도된 안전 여백)로 같은 값이어야 한다. `RopeClimbMantleInsetWorld` 같은 병렬 프로퍼티를 새로
만들지 말고 **`ParkourMantleInsetWorld`를 그대로 호출**해라 — 이름에 "Parkour"가 들어있지만 실제
계산은 파쿠르 특유의 것이 전혀 없다(경계 판정 거리 하나에서 유도된 범용 값).

### 6-E. 등반 완료 후 대칭 문제 — 이번 기능 범위 밖으로 명시

밧줄로 6H를 올라간 뒤, 그 자리에서 다시 경계에 도달하면 `TryFindDescendTarget`(매달리기,
`maxDropDepth<=0`=상한 없음)이 **그 6H 낙차를 그대로 매달려 내려가기 애니메이션으로 처리하려 든다**
— 이 애니메이션은 원래 건물 한두 층 규모의 낙차를 상정해 튜닝된 것이지 밧줄로 오른 낙차를 위한
것이 아니다. 이건 실제 문제일 가능성이 있지만 **"밧줄을 던져 올라가는" 기능의 범위가 아니라
"내려가는" 대칭 기능(로프 하강)의 범위**이므로, 3-D와 마찬가지로 **이번 라운드에서 처리하지
않고 다음 기획으로 명시적으로 넘긴다.** (postClimbDescendCooldown이 "방금 오른 그 경계"에서의
즉시 재하강은 이미 막아주므로 최소한 "오르자마자 도로 내려감"은 방지된다 — 3-C 참고. 남는 위험은
"한참 뒤 다시 그 경계로 돌아왔을 때"뿐이며 이는 급하지 않다.)

### 6-F. `EnforceScreenBoundsAndRescue`는 이미 전역으로 커버한다 — 새 호출 불필요

`StickmanAgent.cs:1007`가 상태와 무관하게 매 프레임 이 메서드를 호출하므로, `RopeClimbState`가
Ascend 중 맨틀 X를 계산해 화면 클램프 근처까지 가더라도 **기존 안전망이 자동으로 적용된다.**
새로 호출을 추가할 필요는 없다(확인만 되면 충분 — `ParkourClimbState`도 이 메서드를 직접 부르지
않는다).

---

## 7. 다음 라운드 착수 명세 요약

### design-motion이 정할 숫자(이 문서가 구조만 정하고 값은 비워둔 것)
★★ **2026-09-07 — design-motion 라운드가 아래 전부를 확정했다. 실제 값·근거·경계 연속성 증명은
8절을 보라(요약만 여기 남긴다).**

- `StickConfig.ropeClimbMaxHeights`(H 배수, 상한 — `stepUpMaxHeights`와 이어 붙는 값이어야 함)
  → **6.6f**(8-2, 화면 대략비 개산 + `archeryMaxTargetDistanceRatio`와의 자릿수 교차확인).
- `StickConfig.ropeClimbSpeedHeightsPerSecond`(오르는 속도, 초당 H)
  → **0.88f**(8-2, `= stepUpMaxHeights / parkourClimbDuration` 경계 연속성에서 역산 — 경계
  높이에서 지속시간이 1.199초로 파쿠르의 1.20초와 사실상 일치).
- `StickConfig.ropeClimbChance`(추첨 확률) — **권고: 기본값 0으로 출하**. 이 저장소의 확립된 관례
  (`longCapeTripMeanSeconds=0`, `throwTumbleEnabled`의 신중한 온보딩 등)를 따라, 포즈/렌더러가
  실제로 준비되기 전까지는 구조만 배선하고 수치로는 잠재워 둔다. 시각 자산이 준비되면 이 값을
  올리는 것만으로 발동한다. (design-motion도 동의 — 8-7 표.)
- `Throw` 페이즈 세부 타이밍(감기/던지기/걸림 확인 등 몇 박자로 나눌지) — `ArcheryState`의
  Draw/Aim 페이즈 타이밍을 참고 템플릿으로 제시.
  → **3소절 확정**: WindUp 0.35s / Swing 0.20s(고정) / HookConfirm 0.18s + 거리기반 비행시간
  0.20~0.55s(8-1). 총 0.73~1.03초.
- (design-motion이 추가로 발견해 확정한 것 — 원 설계서가 요청하지 않았지만 반드시 필요했던 값)
  `ropeClimbCycleRiseHeights = 0.9f`(H) — Ascend 반복 사이클 1회당 상승폭(8-3-A), 그리고
  Ascend 반복 구간의 손/다리 자세 스펙 전체(8-3-B/C, 기존 `ApplyParkourClimbPose`의 4분율 곡선을
  위상 반 사이클 오프셋으로 재사용).

### coder가 착수 시 만들 것 (구현 아님, 목록만)
1. `Core/StickmanEventBus.cs` — `StickmanStateId.RopeClimb = 28` 추가.
   ★ `Tests/EditMode/StickmanStateIdWireFormatTests.cs`의 `Wire` 배열 길이가 자동으로 걸릴 것 —
   **test-engineer/qa-regression에게 이 테스트가 반드시 갱신 대상임을 미리 알린다.**
2. `States/IMovementIntentSource.cs` — `bool RopeClimbRequested { get; }` 추가.
3. `AutoWanderController.cs` — `TryRollEdgeAction()`의 되올라가기 블록 뒤에 로프 대역 `else if` 추가
   (1-C 코드 스케치 그대로), `_ropeClimbRequestedThisTick` 필드/프로퍼티 추가.
4. `WalkState.cs` — `:184`/`:202` 두 지점에 높이 대역 분기 추가(6-B 처방).
5. `StickmanBlackboard.cs` — `TryGetWalkableScreenTopWorldY(out float)` 신설(1-B),
   `ReportClimbMantleCompleted` 재사용(신규 메서드 불필요, 3-C).
6. `States/RopeClimbState.cs` 신설 — `Throw`/`Ascend` 2페이즈(2-B), 실패 규칙 페이즈별 분기(3-B),
   `ParkourMantleInsetWorld` 재사용(6-D), 클래스 문서는 Ragdoll 인터럽트 목록을
   `RagdollImpactResolver`로만 가리키고 복사하지 않는다(3-A). `Enter()`에서 `FacingLocked=true`
   (모든 종료 경로에서 해제, 8-4), Ascend는 8-3-A의 사이클 분할식으로 반복구간/마감구간을 나눠
   마감구간은 `ApplyParkourClimbPose`를 무변경 재호출한다(8-0/8-3-A). 대사는 `Enter()` 한 곳에서만
   `DialogueIntent.TryCreate` 호출, `plannedDwellSeconds`는 **Throw+Ascend 전체 추정 합**을 넘긴다
   (Throw만 넘기면 규칙 8에 거의 항상 막힌다 — 8-6-B 위험 경고 필독).
7. `Interaction/RopeClimbRenderer.cs`(+ 필요시 `RopeClimbDirector.cs`) 신설 — `ArcheryRenderer`/
   `ArcheryDirector` 분리 패턴 재사용, 목표 창에 어떤 쓰기 API도 호출하지 않음(4절 금지 3종 준수).
   Throw 취소 시 밧줄 오버레이 즉시 제거(8-5).
8. `Core/StickConfig.cs` — `ropeClimbMaxHeights`/`ropeClimbSpeedHeightsPerSecond`/`ropeClimbChance`/
   `ropeClimbCycleRiseHeights`/`ropeThrowWindUpSeconds`/`ropeThrowSwingSeconds`/
   `ropeThrowHookConfirmSeconds`/`ropeThrowFlightBaseSeconds`/`ropeThrowFlightMaxSeconds`/
   `ropeClimbChatterChance` 필드 추가(기본값·근거는 8-7 표, 기존 필드들의 XML 문서 관례를 따라
   유도식/근거 명시). `AppSettingsModel.ResolveRopeClimbChatterChance(config)`도 같은 라운드에
   `ResolveParkourClimbChatterChance`와 같은 형태로 추가(8-7).
9. `States/StickmanPoseAnimator.cs` — 신규 메서드(가칭 `ApplyRopeClimbCyclePose`) 추가: 8-3-B/C
   스펙대로 손은 위상 반 사이클 오프셋(`reachHighLocal`↔`reachLowLocal`, 기존 `armReachLocal×
   ClimbGripReachUsable`/`ShoulderPivotLocalY` 재사용), 다리는 `hang.LegSpreadDegrees/
   KneeBendDegrees` ↔ `climb.MantleHipDegrees/MantleKneeDegrees` 사이를 같은 위상 규칙으로 보간.
   파라미터 타입은 새 구조체를 만들지 말고 기존 `in ParkourClimbPoseSettings`를 그대로 받는다
   (4분율 곡선이 이미 "사이클 하나의 모양"과 같으므로, 8-0/8-3-B). Ascend의 마감구간은 이 신규
   메서드를 쓰지 않고 기존 `ApplyParkourClimbPose`를 그대로 호출한다.

### 확인이 필요한 미결 항목(이 라운드가 답하지 못한 것)
- 3-D: `DragThrowState` 진입이 `RopeClimb` 도중 끼어드는 경로 — 미확인, 착수 전 확인 필요.
- 5절: 대사 필요 여부는 "필요하다"까지만 이 라운드가 판단 — 실제 문안/티어/쿨다운 값은
  design-narrative 소관.
- 6-E: 로프로 오른 낙차를 다시 내려가는 대칭 기능은 범위 밖 — 별도 기획 필요 시 재검토.

---

## 8. design-motion 산출물 — 모션·타이밍 스펙

작성: `design-motion` · 2026-09-07 · **순수 설계 문서(코드/애셋 미작성)**. 7절이 비워 둔 숫자를
채우고, 리더 지시 5개 항목(Throw 타이밍/자세, Ascend 속도+반복사이클, 좌우 반전, 취소 시 과도동작,
대사 명세)에 순서대로 답한다. 전부 기존 코드 실측(`ParkourClimbState.cs`, `StickmanPoseAnimator.cs`,
`ArcheryState.cs`, `StickConfig.cs`, `Dialogue/DialogueKind.cs`)에 근거를 붙였다.

### 8-0. 핵심 설계 통찰 — "Ascend의 마지막 한 뼘은 사실 기존 ParkourClimb 그 자체다"

이 절 전체를 관통하는 결정이라 먼저 적는다. `ApplyParkourClimbPose`(StickmanPoseAnimator.cs:2268)의
4박자(Reach 0.1833 / Hang 0.3250 / Pull 0.6583 / Release 0.8917, `StickConfig.cs:242-318`의
`parkourClimb*Fraction` 리터럴)는 이미 **"턱을 향해 다가가 두 손으로 잡고 맨틀링한다"는 한 번의
완결된 등반 동작**을 정확히 표현하고 있다. 밧줄 등반의 **마지막 구간**(밧줄 끝, 창틀에 실제로
손이 닿아 올라서는 순간)은 물리적으로 이 동작과 **완전히 같은 사건**이다 — 다른 것은 오직
"거기까지 오는 동안 밧줄을 몇 번 갈아 잡았는가"뿐이다.

그래서 Ascend를 두 구간으로 나눈다:

```
Ascend = [반복 구간: 밧줄을 N번 갈아 잡으며 오른다 (신규 설계, 8-3)]
       + [마감 구간: ApplyParkourClimbPose를 그대로 재호출 (무변경 재사용)]
```

마감 구간의 상승폭을 `ResolveStepUpMaxHeightWorld(H)`(기존 필드, `StickConfig.cs:747`)로 고정하면
**아름다운 경계 성질**이 생긴다: 벽 높이가 하한(1-A, 파쿠르 상한 바로 위)에 딱 걸쳐 있는
가장 흔한 최초 RopeClimb 케이스에서는 반복 구간이 0회가 되어, **Ascend 전체가 곧 기존
ParkourClimb의 등반 애니메이션과 픽셀 단위로 동일해진다.** 리더가 요구한 "일관된 무게감"을
말로 주장하는 대신 **경계에서 수식으로 증명**하는 방법이 이것이다.

### 8-1. Throw 단계 — 타이밍 + 자세

**구조 템플릿**: `ArcheryState`의 Draw→Aim→Recover(당기기→조준정지→발사, `ArcheryState.cs:99`)를
그대로 빌리되 1회만 돈다(`ArcheryState.ShotCount`처럼 반복 카운터 불필요, game-architect 2-B).
3소절로 나눈다 — **WindUp(감기) → Release(던지기, 팔 스윙만) → HookConfirm(걸림 확인)**.

| 소절 | 무엇을 하는가 | 지속시간(신규 `StickConfig` 필드) | 근거 |
|---|---|---|---|
| WindUp | 양손이 함께 밧줄을 아래·뒤로 감아쥔다(두 손 협업 — 활쏘기가 앞손=활/뒷손=시위로 양손을 함께 쓰는 것과 같은 이유, 2D 측면도에서 외팔 동작은 허전해 보인다). 몸통은 살짝 앞으로 숙여 힘을 싣는다(기존 `RequestBodyLean` API 재사용, StickmanPoseAnimator.cs:2326에 이미 있는 훅 — 새 API 아님). | `ropeThrowWindUpSeconds = 0.35f` | Archery Draw(0.42s, `archeryDrawSeconds`)보다 살짝 짧다 — 활시위를 당기는 저항감이 없는 단순 스윙 준비 동작이라 더 빨라도 자연스럽다. |
| Release | 팔이 코일 자세에서 위·앞으로 스윙해 던지는 정점 자세까지 SmoothStep으로 이동한 뒤 **그 자세를 유지**한다(밧줄 소품 자체의 비행은 렌더러 소관 — 8-1-A). 팔 스윙 자체는 항상 같은 빠르기다(던지는 신체 동작의 지속시간은 목표 거리와 무관, 활을 쏘는 반동 `archeryRecoilSeconds`가 발사 거리와 무관하게 고정인 것과 같은 이유). | `ropeThrowSwingSeconds = 0.20f`(고정, 거리 무관) | — |
| HookConfirm | 정점 자세를 유지한 채 대기 — "명중/실패는 물리 시뮬레이션이 아니라 이미 확정된 사실"(game-architect 2-B, Archery와 같은 원칙) 이므로 이 소절은 순수하게 **읽는 시간**이다. | `ropeThrowHookConfirmSeconds = 0.18f` | Archery Aim(0.30s)의 "조준 정지 = hold" 역할과 같되, 화살처럼 다시 쏠 일이 없어 더 짧다. |

**밧줄 비행 시간(렌더러가 쓸 값, 이 상태가 계산해 이벤트로 실어 보낸다)**: 고정하지 않고
`ArcheryState.ResolveFlightSeconds`(ArcheryState.cs:507)와 **똑같은 제곱근-거리 스케일링**을
세로축에 옮긴다 — 그 함수가 이미 증명한 "선형이면 너무 느려지고 고정이면 섬광이 된다"는 트레이드오프가
여기도 그대로 적용된다.

```
distance = climbHeightWorld (= wallTopWorldY - startWorldY, Enter()에서 이미 확정됨)
reference = ResolveStepUpMaxHeightWorld(H)   // = 파쿠르/로프의 경계 높이, 새 기준점 아님(재사용)
scale = sqrt(distance / reference)
ropeFlightSeconds = clamp(ropeThrowFlightBaseSeconds * scale, ropeThrowFlightBaseSeconds*0.8, ropeThrowFlightMaxSeconds)
```

`ropeThrowFlightBaseSeconds = 0.20f`, `ropeThrowFlightMaxSeconds = 0.55f`. 경계 높이(reference,
scale=1)에서 0.20초, 상한 높이(`ropeClimbMaxHeights`=6.6H, scale=√(6.6/1.0551)=2.50)에서 0.50초 —
Archery의 화살(최장 0.62~1.25초)보다 훨씬 짧다. **의도적이다**: game-architect 6-C가 이미 못박은 대로
이 던지기는 "거의 수직에 가까운 짧은 던지기"라 활쏘기 같은 원거리 포물선의 체공감을 흉내 낼 이유가
없다 — 오히려 짧고 굵어야 "휙 던져서 훅 걸었다"는 스피디한 그림이 산다.

**Throw 총 지속시간** = WindUp + Swing + HookConfirm = 0.73초(하한 높이) ~ 0.53초는 Swing/Confirm
고정이므로 실제로는 **0.73초(하한) ~ 1.03초(상한, Flight 0.50초일 때)** — Flight는 팔 스윙과 병렬이
아니라 Swing 직후부터 HookConfirm 전까지 이어지는 "밧줄이 날아가는 동안 팔은 이미 던진 자세로 서
있는" 구간이라, `ropeFlightSeconds`가 `ropeThrowSwingSeconds`보다 길면 그 차이만큼 HookConfirm 앞에
끼워 넣는다(팔 포즈는 그동안 그대로 유지 — 새 소절 아니라 Swing의 hold 연장).

**Throw 단계 인터럽트(취소) 대응**: 8-5절.

#### 8-1-A. 렌더링 좌표계에 대한 요구(design-motion → coder, 정보 전달용)

이 상태가 매 프레임 발행해야 하는 값(렌더러 `Interaction/RopeClimbRenderer.cs`가 소비, game-architect
4절): 던지는 손의 현재 월드 좌표(팔 IK 결과 그대로), 목표 좌표(`wallHandle`의 가장자리, Enter에서
확정), 그리고 `ropeFlightSeconds` 중 경과 비율. 렌더러는 이 세 값으로 역산 궤적을 그리면 되고
(Archery와 같은 원칙 — 판정과 그림이 어긋나지 않는다), 포즈 애니메이터는 밧줄 소품 자체를 전혀
모른다(관심사 분리, 이미 `ApplyArcheryPose`가 활/화살을 모르는 것과 같은 경계).

### 8-2. Ascend 단계 — 속도·상한

**`ropeClimbSpeedHeightsPerSecond = 0.88f`** (H/초). 임의 리터럴이 아니라 **경계 연속성에서 역산**했다:

```
ropeClimbSpeedHeightsPerSecond = stepUpMaxHeights / parkourClimbDuration
                                = 1.0551 / 1.20 = 0.87925 ≈ 0.88 H/s
```

이렇게 정하는 이유(리더 지시 "일관된 무게감"의 구체적 구현): 파쿠르가 자기 상한 높이(1.0551H)를
오르는 데 실제로 걸리는 시간은 1.20초이므로, 그 지점에서의 **실효 등반 속도**는 이미
1.0551H÷1.20s=0.879H/s로 확정돼 있다. 로프 등반의 속도를 이 값과 다르게 잡으면, 파쿠르 상한
바로 위에서 시작하는 첫 로프 등반이 하한 경계에서 갑자기 빨라지거나 느려지는 것처럼 보인다
(8-0의 "경계에서 수식으로 증명" 원칙과 같은 이유 — 속도까지 이 값으로 고정해야 경계 연속성이
위치·속도 양쪽에서 성립한다).

**상한 = `ropeClimbMaxHeights = 6.6f`** (H). 정밀 유도가 아니라 **개산 대조**로 뒷받침한다(1-B가
이미 명시했듯 실질 상한은 화면 클램프가 따로 잡으므로 이 값은 "화면 클램프가 없는 가상의 초고층
다중 모니터 스택"에서만 실제로 발동하는 안전핀이다 — 정밀할 필요가 없다):
- `ArcheryState.cs:499-501` 주석 실측: "사거리가 창 폭 전체(실측 25유닛, 화면상 900pt)까지 늘어났다."
  즉 이 앱의 기준 배율에서 화면 폭 ≈ 25 월드유닛(≈900pt, `ReferencePointsPerWorldUnitApprox`=35.25,
  `StickConfig.cs:2127`와 정합).
- 16:9 화면이라면 세로 = 25 × 9/16 = 14.06 유닛 = 14.06 / `BaselineCharacterTotalHeight`(2.2746944,
  `StickConfig.cs:2017`) ≈ **6.18H**.
- `StickConfig.archeryMaxTargetDistanceRatio = 6.6f`(`StickConfig.cs:2865`, 활쏘기 사거리 상한)가
  이미 이 근사치와 거의 같은 자릿수에 있다 — 우연이 아니라 **이 앱의 세계 스케일에서 "화면 한
  변" 정도의 크기가 대략 6~7H대라는 것**을 두 개의 독립된 값이 교차 확인해 준다. 그래서 같은
  숫자를 그대로 가져다 쓴다(계산원을 공유하는 게 아니라 — 가로/세로는 다른 축이다 — 같은 자릿수의
  타당성만 빌린다).
- **경계에서의 지속시간**: 하한 1.0551H → 1.0551/0.88 = **1.199초**(파쿠르의 1.20초와 사실상
  동일 — 8-0의 증명이 시간축에서도 성립함을 재확인). 상한 6.6H → 6.6/0.88 = **7.5초**(가장 극단적인
  케이스에만 등장, `ropeClimbChance` 자체가 출하 기본값 0이라 당장은 아무도 보지 못한다).

### 8-3. Ascend 단계 — 반복 사이클 자세 스펙

8-0에서 정한 대로 Ascend는 **[반복 구간] + [마감 구간]**이다. 마감 구간은 무변경 재사용이므로
여기서는 **반복 구간만** 신규 설계한다.

#### 8-3-A. 사이클 분할

```
finalApproachRiseWorld = min(totalRiseWorld, ResolveStepUpMaxHeightWorld(H))   // 마감 구간, 재사용
repeatRiseWorld        = totalRiseWorld - finalApproachRiseWorld               // 반복 구간이 담당할 몫
cycleRiseWorld          = ropeClimbCycleRiseHeights(H) × H                     // 사이클 1회당 상승폭
cycleCount              = floor(repeatRiseWorld / cycleRiseWorld)              // 완결 사이클 수
lastCycleRiseWorld      = repeatRiseWorld - cycleCount × cycleRiseWorld        // 나머지는 마지막 사이클에 흡수(정수 개수 유지)
```

`ropeClimbCycleRiseHeights = 0.9f` (H, 신규 판단값). 근거: 손이 담당하는 아치형 이동량은 대략
팔 길이(design-character 실측 「팔이 0.3297H」, CLAUDE.md 인용)의 2배쯤이어야 "손만 까딱이는"
느낌이 아니라 몸 전체가 실제로 추진되는 느낌이 난다 — 실제 밧줄 등반 기술이 손으로 당기는 동시에
다리로 벽/밧줄을 밀어 몸 전체를 들어올리는 것과 같은 원리(`ApplyParkourClimbPose`의 기존 뒷다리
"벽면 밀기" 로직, `climb.WallFootDropLegRatio`가 이미 정확히 이 개념을 구현해 두고 있다 — 8-3-C
에서 그대로 재사용). 0.9H는 팔길이(0.33H)의 약 2.7배다.

**핵심 성질(속도기반 요구를 사이클로 쪼개도 깨지지 않는다는 증명)**:

```
전체 Ascend 시간 = cycleCount × (cycleRiseWorld / speed) + (finalApproachRiseWorld + lastCycleRise가 반영된 나머지) / speed
               = (repeatRiseWorld + finalApproachRiseWorld) / speed
               = totalRiseWorld / speed
```

즉 사이클을 몇 개로 쪼개든 **총 소요 시간은 game-architect 2-C의 단일 공식과 정확히 같다** —
사이클 분할은 순전히 애니메이션 저작 편의이지 별도의 타이밍 시스템이 아니다.

#### 8-3-B. 손 — 위상 오프셋 교대(신규 부분)

기존 `ApplyParkourClimbPose`의 손 로직(StickmanPoseAnimator.cs:2377-2404)은 두 손이 **같은 순간에
같은 턱을 나란히 잡는다**(활쏘기의 좌우 손 처럼 `stagger`만큼만 앞뒤로 어긋난다) — 창틀처럼
넓고 평평한 손잡이에는 맞지만, 가느다란 밧줄 한 줄을 오를 때 두 손이 항상 같은 높이에 나란히
있으면 "번갈아 짚는" 리듬이 나오지 않는다(리더가 명시적으로 요구한 지점). 그래서 반복 구간에서만
다음으로 바꾼다:

- 앞손(`limb.NeutralSign>=0`, 기존 식별자 그대로 — 새 식별자 불필요)의 사이클 내부 위상 `pFront = cycleLocalProgress01`.
- 뒷손의 위상 `pBack = Frac(pFront + 0.5)` — **정확히 반 사이클 어긋난 같은 곡선**을 쓴다. 새
  "놓기" 분기를 따로 만들 필요가 없다: 위상이 1을 넘어 0으로 감기는 순간 자동으로 "방금 놓은 손이
  다시 높이 뻗는" 모양이 된다(아래 목표 높이 곡선이 주기함수이기 때문).
- 각 손의 목표 높이(루트 기준 상대값, 기존 `ledgeUpLocal`과 같은 성격의 값이지만 **고정된 벽 좌표가
  아니라 사이클마다 반복되는 값**):
  ```
  reachHighLocal = armReachLocal × ClimbGripReachUsable   // 기존 상수 재사용, StickmanPoseAnimator.cs:2302
  reachLowLocal  = ShoulderPivotLocalY                     // 기존 프로퍼티 재사용, StickmanPoseAnimator.cs:2217
  handTargetLocal(phase) = Lerp(reachHighLocal, reachLowLocal, ClimbRiseProfile 곡선(phase))
  ```
  `ClimbRiseProfile`(StickmanPoseAnimator.cs:2197)을 **그대로** 호출한다 — 기존 `ParkourClimbPoseSettings`의
  Reach/Hang/Pull/Release 4분율과 RiseAt* 값을 "사이클 하나의 모양"으로 재해석해서 넘기면 된다(즉
  새 구조체를 만들지 말고 `in ParkourClimbPoseSettings`를 그대로 파라미터로 받는다 — 8-0에서 이미
  증명했듯 이 곡선은 원래 "한 번의 등반 동작"의 모양이었고, 그 모양이 사이클 하나의 모양과
  동일하다).
  가로(앞쪽) 목표는 밧줄이 걸린 고정 앵커 X(HookConfirm 시점에 확정, 창이 옆으로 움직여도
  로프 자체는 이미 걸린 지점에 고정이므로 파쿠르처럼 매 프레임 재조회할 필요가 없다 — 단, 목표
  창이 완전히 사라졌는지는 여전히 매 프레임 확인해야 한다, 3-B)만큼 두 손 공통으로 사용한다
  (밧줄은 한 가닥이므로 좌우로 어긋나지 않는다 — 기존 `stagger`는 로프 등반의 손에는 적용하지
  않는다. 넓은 턱이 아니라 얇은 줄이기 때문).

#### 8-3-C. 다리 — 손과 같은 위상 규칙 재사용(신규 코드 최소화)

새 다리 각도 상수를 하나도 만들지 않는다. 기존 `ApplyParkourClimbPose`가 이미 갖고 있는 두 다리
극단 자세를 그대로 빌려, 반복 구간 동안 **같은 위상 오프셋 규칙**(앞다리 phase=p, 뒷다리
phase=p+0.5)으로 그 둘 사이를 오간다:

```
극단 A(다리가 늘어진 상태) = hang.LegSpreadDegrees / hang.KneeBendDegrees  (LedgeHangPoseSettings, 기존)
극단 B(무릎을 접어 딛는 상태) = climb.MantleHipDegrees / climb.MantleKneeDegrees (ParkourClimbPoseSettings, 기존)
다리각도(phase) = LerpAngle(극단A, 극단B, ClimbRiseProfile(phase))
```

이 두 극단은 이미 `ParkourClimbState`에서 시각적으로 검증된 값(각각 매달리기 상태와 맨틀 스탠스의
기존 튜닝값)이라 새로 조정할 필요가 없다. 결과적으로 팔이 위로 뻗을 때 같은 쪽 다리도 함께
접히는(밀어 올리는) 그림이 되어 — 실제 로프 등반의 손-발 협응과 방향이 같다.

### 8-4. 좌우 반전/방향

**신규 로직 불필요 — 기존 관례를 그대로 상속한다.** `ParkourClimbState.DriveClimbPose`(ParkourClimbState.cs:425-457)가
이미 "포즈 공간은 항상 앞쪽이 +"라는 방향-중립 규약을 확립해 두었고(`toFacing = FacingSign ×
_direction`), 8-3의 모든 로컬 좌표(`handTargetLocal`, 앞손/뒷손 식별)는 이 규약 위에서만 정의된다.
즉 RopeClimbState도 똑같이 `Enter()`에서 `_direction = MoveInputX 부호`로 방향을 정하고
`SetFacingSign(_direction)`을 한 번 호출한 뒤, 매 프레임 `toFacing`을 곱해 좌표를 넘기면 벽이
왼쪽/오른쪽 어디에 있어도 자동으로 거울상이 된다 — 8-1(Throw 자세)의 양손 동작도 같은 좌표계
안에서 대칭이라 좌우 반전에 별도 분기가 필요 없다.

**FacingLocked 권고(파쿠르와의 유일한 차이점)**: `ParkourClimbState`는 `FacingLocked`를 걸지 않는다
(자기 문서 178행 — "이 상태는 1초 남짓이고, 배회 AI의 이동 의도도 어차피 벽 쪽을 향하고 있다").
RopeClimb은 **Ascend가 최대 7.5초까지 늘어난다** — 파쿠르의 전제("1초 남짓이라 안전")가 더는
성립하지 않는다. 그래서 `Enter()`에서 `FacingLocked = true`, `Exit()`에서 **모든 종료 경로에** 해제
(정상 완료/Idle-Walk 취소/Fall 취소 전부 — `ArcheryState.Exit()`가 "어떤 경로로 나가든" 반드시
푸는 것과 같은 이유, ArcheryState.cs:576-578)를 권고한다.

### 8-5. 취소 전환 시 과도동작 필요 여부

**결론: 둘 다 새 "되돌림" 애니메이션 코드가 필요 없다.** 근거는 이 프로젝트의 포즈 스무딩 구조 자체다
(`StickmanPoseAnimator.SmoothTo`, StickmanPoseAnimator.cs:3381-3388): 각 관절의 `CurrentAngle`은
**상태가 바뀌어도 그대로 남아 있고**, 매 프레임 그 순간의 목표각을 향해 지수 감쇠로 계속 접근한다
(`1 - exp(-rate×dt)`, 프레임레이트 독립). 즉 "이전 상태가 마지막으로 요청한 각도"에서 "다음
상태가 요청하는 각도"로의 보간은 **상태 전이와 무관하게 이미 항상 일어나고 있다** — 이 저장소
어디에도 "취소 시 전용 되돌림" 코드가 없는 이유가 이것이다.

- **Throw 취소(→ Idle/Walk)**: 코일 자세(팔이 뒤아래로 접힌 상태)에서 취소되면, 다음 프레임부터
  Idle/Walk의 중립 목표각으로 스무딩이 즉시 시작된다 — 결과적으로 "아 됐다"는 듯 팔을 빠르게
  거두는 자연스러운 반사 동작이 **추가 코드 없이 저절로** 나온다. 이 상태가 신경 써야 할 것은
  포즈가 아니라 **렌더링**뿐이다 — 아직 던지지 않은 밧줄 오버레이(있다면)는 그 프레임에 즉시
  꺼야 한다(game-architect 4절 금지 3, "잔여 그림" 방지). 이건 design-motion 소관이 아니라
  `RopeClimbRenderer`의 몫이라 여기서는 요구사항만 남긴다.
- **Ascend 취소(→ Fall)**: `ParkourClimbState`가 이미 이 정확한 경로("잡을 곳 사라짐 → 즉시
  Fall")를 배포 중이고 문제 보고가 없다 — 같은 스무딩 메커니즘이 이미 실전 검증됐다는 뜻이다.
  같은 처리를 그대로 물려받는다.

**정직하게 남겨두는 사소한 흠 하나**(이 문서의 관례상 조용히 넘기지 않는다): Ascend 취소가
사이클의 "손이 머리 위로 뻗은" 순간(위상 절반)에 발생하면, Fall 진입 직후 한두 프레임은 양팔이
아직 위로 들린 채로 낙하가 시작된다 — 실제로 떨어지는 사람의 팔이 그 자세로 남아있는 것은 다소
어색하다. **의도적으로 고치지 않는다**: (1) 위 스무딩이 몇 프레임 안에 자연히 팔을 내리므로
지속되는 결함이 아니고, (2) 이 캐릭터는 코미디 톤의 막대인형이라 낙하 시작 순간의 잔여 포즈 정도는
허용 오차 안이며, (3) 이걸 고치려면 "Ascend 전용 취소 분기"라는 새 코드가 필요한데 그 비용이
효과 대비 크다. 실기에서 거슬리면 재검토 대상으로 남긴다.

### 8-6. 원칙 1(행동-텍스트 싱크) 대사 명세 — design-narrative 인계

**트리거 시점: 단 한 곳, `RopeClimbState.Enter()`(Throw 페이즈 진입 순간)뿐이다.** 문안 자체는
쓰지 않는다(리더 지시 — design-narrative 소관). 아래는 design-narrative가 문안을 쓸 때 지켜야
할 예산과, 이 결정에 이르게 된 구조적 이유다.

#### 8-6-A. ★★ 구조적 발견 — "여러 줄로 나누기"는 이번 라운드 배관으로는 불가능하다

game-architect 5절 4번이 Ascend 도중 여러 줄로 나누는 것을 "권고"했으나, 실측 결과 **현재
대사 파이프라인은 상태의 `Tick()` 도중 새 대사를 만들 방법이 구조적으로 없다.** `DialogueIntent`는
반드시 `StateTransitionContext`를 필요로 하고(`DialogueIntent.cs:109,123,145`), 그 컨텍스트는
오직 `StickmanStateMachine.ChangeState()`가 `Enter(context)`에 넘겨줄 때만 존재한다. 이 저장소에
살아있는 **모든** `DialogueIntent`/`DialogueIntent.TryCreate` 생성 호출부를 전수 조사했다
(`AttackState.cs:64`, `DragThrowState.cs:431`, `DanceState.cs:254`, `IdleState.cs:63`,
`LedgeHangState.cs:202`, `ParkourClimbState.cs:159`, `RagdollState.cs:86`, `WindowTheftState.cs:52`,
`RunawayState.cs:90/102/128`, `WalkState.cs:64`, `TimedSpectacleState.cs:76`) — **전부 `Enter()`
안에 있다. `Tick()` 안에서 새 대사를 만드는 경로는 0건이다.** 그러므로 "등반 도중 두 번째 줄을
끼워 넣기"는 design-motion/design-narrative 단독 판단을 넘어서는 **새 배관**(예: 컨텍스트 없이도
`Reaction`을 만들 수 있는 새 생성자)이 필요한 사안이며, game-architect/coder에게 명시적으로
넘긴다. 이번 라운드는 **단일 트리거로 설계를 확정**한다 — 다행히 아래 8-6-B가 보여주듯 단일
트리거만으로도 예산이 넉넉해 이 제약이 실질적 손해가 아니다.

#### 8-6-B. 계획 잔여 체류(plannedDwellSeconds) — Throw만 넘기지 말 것(★★ 위험 경고)

`ParkourClimbState.Enter()`의 패턴(`climbDurationForGate`, ParkourClimbState.cs:146-161)을
그대로 따르되, 넘기는 값은 **Throw 페이즈 하나가 아니라 이 진입이 커밋하는 전체 시퀀스(Throw
전체 + Ascend 예상 전체)**여야 한다:

```
plannedDwellSeconds = throwTotalDurationEstimate + (climbHeightWorld / ropeClimbSpeedHeightsPerSecond)
```

`climbHeightWorld`는 `Enter()` 시점에 `TryFindClimbableWall`이 이미 내주므로 이 값은 그 자리에서
바로 계산 가능하다(Parkour가 `ClimbHeightUnits`를 똑같이 Enter에서 확정하는 것과 같다).

**왜 Throw만 넘기면 위험한가**: Throw 총 지속시간은 0.73~1.03초(8-1)인데, 규칙 8의 절대 최소
필요체류는 페이드인(`DialogueTiming.FadeInSeconds`=0.06s) + 한국어 대사 가독예산의 **하한 클램프**
(`DialogueBudget.MinSeconds`=0.62s, `DialogueKind.cs:129`) = **0.68초**다. Throw만 넘기면 여유가
겨우 0.05~0.35초뿐이라 — 정확히 `ParkourClimbState`가 2026-09-02에 실제로 겪은 사고(리터럴 코멘트
그대로 인용, ParkourClimbState.cs:134-145: "낡은 폴백 0.5초... 이 상태는 한 마디도 하지 못한다")와
**같은 함정을 이번엔 설계 단계에서 미리 심는 꼴**이다. 전체 시퀀스를 넘기면 최소 합계가
`0.73 + (1.0551/0.88) ≈ 1.93초`로 뛰어 여유가 3~4배 늘어난다.

#### 8-6-C. 예산이 넉넉하다는 뜻 — design-narrative가 실제로 쓸 수 있는 길이

`DialogueBudget.ReadingSeconds`(DialogueKind.cs:209) 공식 `clamp(0.28 + 글자수×0.075, 0.62, 2.20)`을
역산하면, 규칙 8을 절대 걱정하지 않아도 되는 한국어 글자수 상한은:

```
(MaxSeconds − FadeInSeconds − BaseSeconds) / PerGlyphSeconds = (2.20 − 0.06 − 0.28) / 0.075 ≈ 24.5자
```

RopeClimb의 최소 plannedDwell(≈1.93초)조차 이미 이 상한을 가뿐히 커버하므로(`ReadingSeconds`
상한 2.20초 자체가 더 작은 병목), **design-narrative는 사실상 `DialogueBudget.MaxSeconds` 하나만
신경 쓰면 된다 — RopeClimb 전용 규칙 8 걱정은 없다고 봐도 좋다.** (참고로 파쿠르의 실측 최장
필요체류는 0.865초였다 — ParkourClimbState.cs:141 — 로프 등반 쪽이 그보다 훨씬 여유롭다.)

#### 8-6-D. DialogueKind / 티어 구조 제안(문안 아님)

- **종류: `DialogueKind.Narrative`**(진행 서술). Parkour와 동일 계열 — 지금 벌어지고 있는 사실만
  주장하고, 아직 확정 안 된 결과(갈고리가 실제로 걸렸는지, 끝까지 오를 것인지)를 단정하지 않는다.
- **파쿠르의 `LightClimbHeights`(0.95H)/`HardClimbHeights`(2.2H) 임계를 재사용하지 말 것** —
  game-architect가 2-A 표에서 이미 지적한 이유 그대로("재사용하면 티어가 항상 최상위로 수렴,
  무의미") — RopeClimb은 정의상 그보다 높은 벽만 다루므로 파쿠르 임계 위에서는 아무 분기력이 없다.
- **대신 RopeClimb 자신의 대역([`ResolveStepUpMaxHeightWorld`, `ropeClimbMaxHeights×H`]) 안에서
  새 2단 임계를 제안한다** — 출발점(확정 아님): 산술 평균 `(1.0551 + 6.6) / 2 ≈ 3.83H`. 그
  아래는 "로프가 있으니 오를 만하다"는 톤, 그 위는 "극단적으로 아찔하다"는 톤을 design-narrative가
  실기 분포를 본 뒤 조정하는 것을 권고한다(파쿠르의 0.95/2.2도 원래 판단값으로 시작해 실측 후
  0.4109로 재조정된 전례가 있다, ParkourClimbState.cs:265 주석).
- **완료 시점 대사는 권고하지 않는다.** `ParkourClimbState` 자신도 완료 대사가 없다 — 종류가
  Narrative라 상태가 끝나는 순간 그 문장이 거짓이 되어 즉시 컷되므로, "완료 순간"을 위한 문장은
  애초에 Narrative로 표현할 수 없다(ParkourClimbState.cs:154-156). Reaction 종류로 별도 설계하면
  가능은 하지만(규칙 8 미적용, `DialogueKind.cs:360`), 그러려면 다음 상태(`WalkState.Enter()`,
  이미 자기 몫의 `AmbientChatter.Resolve`를 쓰고 있다 — WalkState.cs:64)에서 "방금 로프 등반을
  마쳤다"는 사실을 읽어 우선순위를 조정하는 **새 배선**이 필요해 8-6-A와 같은 종류의 확장이다.
  이번 라운드는 설계만 남기고 채택하지 않는다.
- **취소 시 대사도 넣지 않는다** — 파쿠르의 즉시-Fall 취소 경로에도 전용 대사가 없는 것과 같은
  정책으로 일관성을 유지한다.

### 8-7. 신규 `StickConfig` 필드 요약(coder 착수용)

| 필드명 | 기본값 | 단위 | 근거 절 |
|---|---|---|---|
| `ropeClimbMaxHeights` | 6.6f | H | 8-2 |
| `ropeClimbSpeedHeightsPerSecond` | 0.88f | H/초 | 8-2 |
| `ropeClimbChance` | 0f(출하 시 잠재워 둠, game-architect 7절 권고) | 확률 | 원 설계서 7절 |
| `ropeClimbCycleRiseHeights` | 0.9f | H | 8-3-A |
| `ropeThrowWindUpSeconds` | 0.35f | 초 | 8-1 |
| `ropeThrowSwingSeconds` | 0.20f | 초 | 8-1 |
| `ropeThrowHookConfirmSeconds` | 0.18f | 초 | 8-1 |
| `ropeThrowFlightBaseSeconds` | 0.20f | 초 | 8-1 |
| `ropeThrowFlightMaxSeconds` | 0.55f | 초 | 8-1 |
| `ropeClimbChatterChance` | 1.0f(제안 — 근거 아래) | 확률 | 8-6 |

★ `ropeClimbChatterChance`가 파쿠르의 `parkourClimbChatterChance`(0.35, `AppSettingsModel.cs:303`)보다
훨씬 높은 이유: 파쿠르는 배회 AI가 76초에 한 번꼴로 반복하는 흔한 사건이라 잡담 확률을 낮춰야
과다 발화를 막는다. 로프 등반은 `ropeClimbChance`(트리거 확률)와 좁은 높이 대역 자체가 이미
"드문 사건"임을 보장하므로, 드물게 벌어질 때만큼은 공유 쿨다운 하나로 충분히 조절되고 굳이 두
번째 확률로 더 낮출 필요가 없다 — 오히려 낮추면 "모처럼 발동한 큰 스펙터클인데 캐릭터가
침묵한다"는 손해가 더 크다. `AppSettingsModel.ResolveRopeClimbChatterChance(config)`를
`ResolveParkourClimbChatterChance`와 같은 형태로 하나 더 추가하는 것을 권고한다(같은 파일,
같은 패턴, AppSettingsModel.cs:302-303 참고).

### 8-8. 확인/후속이 필요한 항목(design-motion이 답하지 못한 것)

- 8-6-A: Ascend 도중 여러 줄로 나누는 대사 배관 — 새 `DialogueIntent` 경로 필요, 이번 라운드
  범위 밖. game-architect가 우선순위를 정해야 한다.
- 8-6-D: 완료 시점 Reaction 대사 — `WalkState.Enter()`의 기존 `AmbientChatter.Resolve` 자리와
  경합하므로 설계만 남기고 채택 보류.
- Throw 단계의 팔 각도(코일/투척 정점 자세의 정확한 도(°) 값)와 8-3의 손 목표 높이 곡선은
  **판단값**이다(파쿠르의 `LightClimbHeights`/`MantleArmDegrees` 등도 원래 이렇게 시작해 실기
  확인 후 재조정된 전례를 따른다) — coder 구현 후 실제 빌드 캡처로 1차 확인이 필요하다(디자인
  7인 공통 규칙 — "최종 판정은 실제 빌드 캡처로만").
- 8-5의 "낙하 시작 시 팔이 잠깐 위로 들려 있는" 잔여 포즈는 의도적으로 미수정 — 실기에서 거슬리면
  재검토.

Windows 영향: 없음 — 이 절은 순수 모션/타이밍/대사 예산 설계로 플랫폼 분기가 없다(포즈 애니메이터와
대사 시스템은 이미 플랫폼 중립).

---

## 9. 트리거 재설계 — 실사용 관측성 확보

작성: `game-architect` · 2026-09-07(2차) · **리더 재지시 대응, 실측 + 시뮬레이션 근거 포함**

> 사용자 신고(원문): *"창 판단을 다른식으로 해야할거 같은데 이러면 영원히 안될거 같음."* — 신규 구현
> 직후 실기(macOS)에서 몇 분간 지켜봤지만 밧줄등반이 한 번도 관측되지 않았고, 캐릭터는 독(Dock) 근처
> 1.637유닛 높이의 쉬운 루프만 5번 이상 반복했다.

리더 브리핑의 가설은 "1-C가 되올라가기 3순위 뒤에 밧줄등반을 끼워 넣으면서, **드문 조건(내려갈곳없음)
∩ 드문 조건(초고벽)**이 곱해져 체감상 영원히 안 나온다"였다. **실측 결과 이 가설은 부분적으로만
맞았다** — 더 심한 원인이 최소 2개 더 있었고, 그중 하나는 확률이 전혀 아니라 **결정론적으로 0**이었다.
아래 9-1이 그 실측이고, 9-2가 리더가 제시한 세 방향을 그 실측에 비추어 채점한 것이며, 9-3이 "지형만
있다면 트리거 체인 자체는 얼마나 빠른가"의 시뮬레이션, 9-4가 최종 결정이다.

### 9-1. 실측 — 오늘 밤 실행 중이던 실제 인스턴스에서 확인한 사실 3가지

★ **뇌피셜 금지 지시에 따라, 이 절의 모든 수치는 코드 grep 또는 `.claude/skills/run-stickmate/driver.sh`로
실행 중이던 인스턴스의 실제 Player 로그에서 직접 뽑았다** — 새로 실기를 켜지 않고, `doctor`로 먼저
확인한 기존 인스턴스(PID 21736, 드라이버가 띄운 것 — 사용자 개인 인스턴스 아님, `그 밖의 인스턴스: 없음`
확인됨)의 로그(`/tmp/stickmate-run/current.log`)를 읽기만 했다. **프로덕션 코드나 설정은 이번 조사에서
한 줄도 바꾸지 않았다**(`git status` 클린 확인).

#### 사실 1 — `ropeClimbChance`는 확률이 아니라 **문자 그대로 0**이다 (가장 치명적)

```
Assets/_Project/Scripts/Core/StickConfig.cs:359:   public float ropeClimbChance = 0f;
Assets/_Project/Data/DefaultStickConfig.asset:61:  ropeClimbChance: 0
```

C# 기본값과 **실제로 런타임이 로드하는 직렬화 에셋 값이 둘 다 0**이다. design-motion이 8-7에서
이 값을 0으로 "출하 시 잠재워 둠"으로 권고한 이유는 명시적으로 *"포즈/렌더러가 실제로 준비되기
전까지"*였다(§7). 그런데 `States/RopeClimbState.cs`와 `Interaction/RopeClimbRenderer.cs`가 이미
이번 라운드에 구현되어 존재한다 — 즉 그 유예 조건이 지금은 재검토 대상이다. **다만 이 값이 0인 한,
1-C의 "내려갈곳없음 ∩ 초고벽" 곱셈 논증은 사실 무의미하다** — 어떤 조건을 아무리 완화해도
마지막에 `× 0`이 곱해지면 결과는 항상 0이다. 사용자가 "영원히 안 될 것 같다"고 느낀 것은 **직관적으로
정확했다** — 압축된 확률이 아니라 **말 그대로 발생 불가능**이었다.

#### 사실 2 — 실기 로그: 캐릭터의 도달가능 영역이 그날 밤 내내 발판 3개뿐이었다

`[발판리포트]`(2.5~60초 주기 상시 진단 로그, `Platform/MacOS/MacOverlayStateEnforcer.cs:1011`)를
전수 확인한 결과, 세션 내내 `합성=[Dock x254~1259 상단y907, 안전망왼쪽 x0~254 상단y974, 안전망오른쪽
x1259~1512 상단y974]`로 **고정**이었다 — Dock과 좌우 안전망 셋이 화면 전체 폭(0~1512)을 **틈도 겹침도
없이** 이미 다 덮고 있다. 이 셋 사이의 유일한 높이차가 정확히 사용자가 본 그 1.637유닛이다
(`[Dock계단] 윗면 y=-10.167, 아랫면 y=-13.804` 계열 로그와 `[되올라가기] 턱 높이=1.637유닛(상한 1.94)`
반복 — grep 결과 "[밧줄등반]" 매치 **0건**, "[되올라가기]"/"[뛰어내리기]" 매치 다수, 전부 높이
1.637유닛 하나로 동일).

같은 시간대 로그에 실제 Finder 창 2개(`Finder@(976,292 536x874)`, `Finder@(0,677 410x436)`)가
`보이는 상단테두리`로 분명히 열거되어 있었다 — 즉 **감지가 안 된 게 아니라, 감지는 됐는데 "벽"으로
채택될 자리에 없었다.**

#### 사실 3 — 왜 그 Finder 창들이 "벽"으로 안 잡혔는가: `TryFindClimbableWall`의 인접 탐지 폭이 좁다

`GroundSensor.cs:434-437`:

```csharp
bool horizontallyNear = direction > 0
    ? topLeftWorld.x >= edgeX - detectionRadius && topLeftWorld.x <= edgeX + searchSlack
    : topRightWorld.x <= edgeX + detectionRadius && topRightWorld.x >= edgeX - searchSlack;
```

`detectionRadius = parkourDetectionRadius = 0.5`유닛, `searchSlack = detectionRadius ×
AdjacentFootholdSearchRadiusMultiplier(4) = 2.0`유닛(`GroundSensor.cs:23,416,423`) — 즉 후보 발판의
**왼쪽 모서리**가 지금 서 있는 발판 경계에서 왼쪽으로 0.5유닛~오른쪽으로 2.0유닛(도합 약 2.5유닛 ≈
102pt, 이 세션의 실측 환산 41pt/유닛 기준 — `[Dock계단]` 67pt=1.637유닛에서 역산) 안에 있어야만 "벽"
후보가 된다. Dock의 오른쪽 경계는 OS x=1259인데, 위 Finder 창의 왼쪽 모서리는 x=976 — **283pt나
안쪽**(Dock 발판 자기 자신의 영역 한복판)에 있다. 그러니 이 창은 Dock의 오른쪽 경계에서 벽을 찾는
탐색에 애초에 **후보로 진입조차 못 한다** — 창이 화면에 아무리 높이 떠 있어도, 그 창의 모서리가
"지금 서 있는 발판의 경계에서 도보 한 걸음 거리 이내"에 있지 않으면 이 함수 구조상 절대 안 잡힌다.

**결론**: 오늘 밤 이 데스크톱 배치에서는, `ropeClimbChance`를 아무리 올려도(1.0으로 해도) 밧줄등반이
단 한 번도 발동할 수 없었다 — 도달 가능한 영역 안에 애초에 밧줄 등반 대역(파쿠르 상한 초과 ~ 로프
상한 이하)의 "벽" 자체가 존재하지 않았기 때문이다. 이건 확률 문제가 아니라 **그 순간의 실제 창
배치 문제**다.

### 9-2. 리더가 제시한 세 방향 — 위 실측에 비추어 채점

#### 옵션 1(전제 완화/독립 우선순위 승격) — **전면 채택 기각, 좁은 범위 보강만 채택(9-4-B)**

재분석 결과 1-C의 "내려갈곳없음 ∩ 초고벽" 곱셈 논증은 **생각보다 약하다**:

- `TryFindHopDownTarget`(아래로)과 `TryFindClimbableWall`(위로)은 **같은 방향·같은 경계에서 물리적으로
  상호배타적**이다 — 같은 발판이 동시에 "지금보다 낮다"와 "지금보다 높다"일 수 없다. 그래서 대부분의
  경우 "내려갈 곳이 있어서 로프 평가가 막히는" 상황 자체가 드물다(내려갈 곳은 반대쪽 방향에 있거나
  아예 없다).
- `_edgeActionRolledThisLeg`는 매 새 Walk 페이즈(`EnterMoving`)뿐 아니라 **경계에서 튕겨 돌아설 때마다**
  (`TickEdgePause:604`, `_edgeActionRolledThisLeg = false`)와 **공중 전이마다**(`TickMoving:522`)
  리셋된다 — 즉 좁은 두 발판 사이를 왕복하는 흔한 배회 패턴에서는 **경계에 부딪힐 때마다 새로
  추첨한다**. 오늘 밤 로그가 정확히 이 패턴을 보여준다(2분도 안 되는 구간에 되올라가기/뛰어내리기가
  번갈아 십수 회).
- **다만 진짜로 막히는 경우가 하나 있다**: 같은 탐색 폭(9-1의 ~2.5유닛) 안에 **낮은 발판과 높은 벽이
  동시에** 있으면(예: 작은 틈 바로 너머에 큰 창), 뛰어내리기 분기가 `TryFindHopDownTarget` 성공
  시점에 **추첨 성공 여부와 무관하게 무조건 `return false`**(`AutoWanderController.cs:748-761`)해
  버려 같은 프레임에 로프 벽 평가 자체가 실행되지 않는다. 이건 실재하는 결함이지만, 1-C가 우려한
  "내려갈곳없음이 항상 로프를 막는다"보다는 **훨씬 좁은 경우**다.
- 전면 승격(내려갈곳 유무와 무관한 독립 우선순위)을 안 하는 추가 이유: 1-C가 이미 지적한 "내려갈 수
  있는데도 로프를 던지는" 그림은 여전히 부자연스럽고, 이걸 감수하려면 원칙 1(행동-텍스트 싱크)에
  design-narrative의 새 설명("왜 편하게 있다가 갑자기 위험한 등반을 시도하는가")이 필요해진다 —
  실측상 이득이 크지 않은데 그 비용을 낼 이유가 없다.

→ **결정: 좁은 보강만 채택**(9-4-B) — 하지만 이건 오늘 밤 관측 실패의 원인이 **아니다**(오늘 밤은
사실 1·2가 전부였다). 후속 견고화로만 반영한다.

#### 옵션 2(배회 목표를 키큰 창 쪽으로 가중치) — **채택 안 함**

사실 2·3이 이 옵션을 무의미하게 만든다: 오늘 밤 도달 가능한 발판이 **3개뿐**이라 "더 자주 그쪽으로
걷게" 가중치를 줄 대상 자체가 없었다. 그리고 설령 캐릭터가 화면 반대편의 진짜 높은 창까지 걸어간다
해도, `TryFindClimbableWall`의 인접 탐지 폭(~2.5유닛/102pt)이 좁아서 그 창의 모서리가 마침 "지금 선
발판의 경계"에서 도보 한 걸음 이내가 아니면 여전히 안 잡힌다 — **가중치 조작보다 인접 탐지 폭 자체가
더 근본적인 병목**이다. 게다가 "다른 모든 걸 제치고 화면 저편의 큰 창으로 직행"하는 그림은 6-C가
이미 확정한 "기회주의적·경계 도달형" 원칙(활쏘기 같은 "목표 탐색형" 행동은 이 기능의 범위 밖으로
명시적으로 미뤄뒀다)과 정면 충돌한다 — 새 행동 패턴을 여는 큰 결정이라 이 라운드 범위를 넘는다.

→ **결정: 채택 안 함.** 인접 탐지 폭 자체를 넓히는 문제는 파쿠르 전체(밧줄 전용이 아니다)에 영향을
주는 더 큰 변경이라 9-5에 후속 과제로만 명시한다.

#### 옵션 3(QA용 강제 트리거) — **채택, 오늘 밤 유일하게 필요충분한 해법**

사실 1(결정론적 0)과 사실 3(인접 탐지 폭)은 **둘 다 "확률을 올리는 것"만으로는 못 고친다** — 하나는
값 자체가 0이고, 다른 하나는 애초에 벽 후보가 지형에 없다. 이 둘을 실기에서 **지금** 검증하려면 확률
조정과 별개로 "지형이 있든 없든 무조건 확인되게" 만드는 창구가 필요하다 — 정확히 오늘 밤 다른
라운드가 만든 `STICKMATE_UNLOCK_ALL`(`Core/EquipmentDebugUnlock.cs`) 선례와 같은 필요다.

→ **결정: 채택.** 9-4-C에서 구체 스펙.

### 9-3. 시뮬레이션 — "지형이 있다면" 트리거 체인 자체는 얼마나 빠른가

사실 1·3을 고친다고 가정했을 때(= 유효한 로프 대역 벽이 실제로 도달 범위 안에 있다고 가정했을 때),
`ropeClimbChance` 자체는 얼마로 잡아야 "몇 분 안에" 관측되는가를 몬테카를로로 계산했다(스크립트:
`rope_trigger_sim.py`, `AutoWanderController`의 Idle/Walk 주기·경계 정지·추첨 체인을 그대로 재현 —
Idle 2~6초, Walk 1.5~4초, ±17.5% 지터, 걷기확률 0.75, `stepUpChance=0.85`, 경계정지 0.3~0.8초,
보행속도 1.5유닛/초 — 전부 `StickConfig.cs` 실측값. 벽은 한쪽 경계에 고정 배치, 반대쪽은 평범한
반전 경계로 가정 — 즉 9-1의 지형 문제는 "해결됐다"고 가정한 이상적 케이스다). 표본 6,000회:

| `ropeClimbChance` | 평균(초) | 중앙값(초) | P(3분 이내) | P(5분 이내) |
|---:|---:|---:|---:|---:|
| 0(현재 출하값) | — | — | **0%**(결정론적) | **0%** |
| 0.05 | 247.2 | 173.9 | 51.3% | 70.2% |
| **0.15** | **80.2** | **56.0** | **89.8%** | **97.8%** |
| 0.20(권고, 9-4-A) | ~63 | ~44 | ~94% | ~99% |
| 0.25 | 47.1 | 33.3 | 98.1% | 99.8% |
| 0.35(`ledgeHangChance`와 동률) | 33.5 | 23.6 | 99.5% | 100% |
| 0.85(`stepUpChance`와 동률) | ~12 | ~9 | 100% | 100% |

★ `stepUpChance`(0.85)도 매번 곱해진다는 점이 중요하다 — 로프등반 분기는 되올라가기 분기의 "else"
안에 있으므로, **되올라가기 자체의 성공 추첨(0.85)을 통과해야 로프 추첨 기회가 열린다.** 즉 실효
로프등반 확률은 `stepUpChance × ropeClimbChance`다(§1-C 코드 스케치 그대로 실측 확인).

### 9-4. 최종 결정 — 조합안

리더 지시 4항("셋 중 하나만 고르지 말고 조합해도 된다")대로, 네 갈래를 함께 반영한다.

#### 9-4-A. `ropeClimbChance` 기본값 상향 — 권고 0.20, 단 조건부

design-motion의 "출하 시 0" 근거(포즈/렌더러 미완성)는 `RopeClimbState.cs`/`RopeClimbRenderer.cs`가
이미 존재하는 지금 시점엔 재검토 대상이다. 9-3 시뮬레이션 기준 **0.20**을 권고한다(3분 이내 관측
확률 ~94%, 5분 이내 ~99% — "몇 분만 지켜봐도" 요구를 만족하면서도 `stepUpChance`(0.85)만큼 흔해지진
않아 "특별한 사건"이라는 느낌을 유지한다). **다만 이건 디자인 결정이지 최종 확정이 아니다** —
디자인 7인 공통 규칙("최종 판정은 실제 빌드 캡처로만")에 따라, 실제 포즈/렌더러가 빌드 캡처로
확인되기 전까지는 0.20이 아니라 **9-4-C의 QA 강제 훅으로만 우선 검증**하고, 프로덕션 기본값 상향은
그 캡처 확인 이후 리더 승인을 받아야 한다.

#### 9-4-B. `TryRollEdgeAction`의 좁은 보강 — hop-down이 로프 벽을 원천봉쇄하는 경우만 닫는다

9-2가 찾은 좁은 결함(같은 탐색 폭 안에 낮은 발판과 높은 벽이 동시에 있으면 hop-down이 무조건
이겨버림)만 닫는다. 전면 재작성이 아니라 **한 줄짜리 순서 교정**이다 — hop-down이 "성공 시 무조건
return false"하기 전에, 같은 방향에 로프 대역 벽이 있는지 **먼저 존재만 확인**(추첨 없이)하고, 있으면
hop-down 쪽 추첨을 건너뛰고 로프 벽 평가로 넘긴다(있어도 없어도 hop-down 자체의 확률·연출은 전혀
바뀌지 않는다 — 이 케이스가 원래도 드물기 때문에 hop-down 체감 빈도에 미치는 영향은 무시할 수
있다). 코드 스케치(coder 착수용):

```csharp
// 1) 뛰어내리기 — 단, 같은 방향에 로프 대역 벽이 이미 있으면 그 벽 평가를 원천봉쇄하지 않는다.
bool ropeWallPresent = _blackboard.TryFindClimbableWall(info, _direction, out long ropeWallHandle, out float ropeWallTopY)
    && (ropeWallTopY - info.GroundWorldY) > ResolveStepUpMaxHeight();
if (!descendSuppressed && !ropeWallPresent && hopChance > 0f && _blackboard.TryFindHopDownTarget(...)) { ... }
```

★ 대가: `TryFindClimbableWall`을 한 프레임에 최대 2번(위 존재 확인 + 기존 3번째 분기) 호출하게 된다 —
둘 다 순수 조회(부작용 없음)라 안전하지만, 성능 민감 경로라면 캐싱을 coder가 검토할 것.

#### 9-4-C. QA 강제 트리거 — `STICKMATE_QA_ROPE_CLIMB_CHANCE` (+ 지형 우회)

`EquipmentDebugUnlock.cs`의 3단 판정(에디터/개발빌드 → 열림 / 릴리스+환경변수 → 열림 / 그 외 → 닫힘)을
그대로 본뜨되, **별도 환경변수**를 쓴다(`STICKMATE_UNLOCK_ALL`과 성격이 다르다 — 저 스위치는 "보유
판정" 우회이고 이건 "배회 AI 확률" 우회다. 같은 스위치에 묶으면 장비 QA를 하려던 사람이 뜻하지 않게
로프등반 확률까지 건드리게 된다, `EquipmentDebugUnlock.cs:29-35` 문서와 같은 논지).

- **`STICKMATE_QA_ROPE_CLIMB_CHANCE=<0..1>`** — 설정되면 `StickConfig.ropeClimbChance`를 그 값으로
  **런타임 오버라이드**(에셋은 안 건드림, 메모리상 판정에만 반영 — `StickMateDevTools`가 이미 쓰는
  "환경변수 → 오버라이드 값 → 판정 함수는 오버라이드 우선" 패턴 재사용). `1`로 주면 사실상 강제
  발동에 가까워진다(위 9-3 표에서 `stepUpChance` 곱이 아직 남아 있으므로 100% 확정은 아니다 — 이건
  의도적이다, 완전한 강제는 "밧줄등반 자체"가 아니라 "밧줄등반으로 가는 경로 전체"를 시험하지 못하게
  만든다).
- **지형 문제(사실 3)는 확률 오버라이드만으로는 못 고친다** — 그래서 같은 QA 훅이 켜져 있을 때만,
  `TryFindClimbableWall`이 실패하거나 파쿠르 상한 이하의 벽만 찾았을 경우에 한해, **기존 Dock/안전망
  합성 발판과 같은 방식**(`FallbackPlatformWindowService`의 `DockFootholdHandle`류 패턴)으로 **QA
  전용 합성 시험벽 1개**(`TryGetWalkableScreenTopWorldY` 높이, 캐릭터 바로 앞)를 임시로 삽입해
  `TryFindClimbableWall`이 그것을 찾게 한다. 이 시험벽은 실제 창이 아니므로 원칙 3과 무관하고, QA
  환경변수가 꺼지면 즉시 사라진다(에셋에도 세이브에도 안 남는다 — `STICKMATE_UNLOCK_ALL`이 보유
  판정에만 우회를 두고 문구까지 같이 바꾸는 것과 같은 "거짓말하지 않기" 원칙, `EquipmentDebugUnlock.cs:37-43`).
- **릴리스 게이트**: `EquipmentDebugUnlockReleaseGateTests.cs`와 같은 형태의 테스트 1건을 동반해야
  한다 — "이 환경변수 이름이 정확히 이것이고, 릴리스 빌드에서는 환경변수 없이는 절대 안 열린다"를
  잠근다(test-engineer/qa-regression 착수 시 명시).

#### 9-4-D. 인접 탐지 폭 확장 — 이번 라운드 범위 밖, 후속 과제로 명시

`AdjacentFootholdSearchRadiusMultiplier`(현재 4, ≈2.5유닛/102pt)를 넓히면 옵션 2가 원래 노렸던
효과(화면 반대편 큰 창도 잡히게)에 더 가까워지지만, 이 상수는 **파쿠르 전체가 공유**한다
(`TryFindClimbableWall`/`TryFindDescendTarget` 공통) — 밧줄등반만을 위해 건드리면 손 등반/매달리기/
뛰어내리기의 기존 튜닝이 전부 함께 흔들린다(회귀 위험, `qa-regression` 전량 재검증 필요). 이건
game-architect 단독 판단 밖이며, 다음 라운드에 **별도 안건**으로 올린다 — 후보 방향만 남긴다: (a)
로프등반 전용으로 별도 탐색 폭 상수를 신설(파쿠르와 분리, 리스크 국소화) (b) 폭을 넓히지 않고 대신
"현재 서 있는 발판과 안 겹치는, 화면에서 가장 높은 발판"을 별도 함수로 조회해 로프등반 전용
2차 탐색으로 쓰기(6-A의 "밴드화" 철학을 유지하면서 인접성 제약만 로프에 한해 완화).

### 9-5. 요약 — 무엇이 바뀌었고 왜

리더의 원래 가설(1-C의 곱셈 압축)은 실재하지만 **오늘 밤 관측 실패의 주 원인이 아니었다.** 진짜
원인은 (1) `ropeClimbChance`가 결정론적 0이었다는 것, (2) 그마저 고쳤어도 그날 밤 실제 데스크톱에는
로프등반 대역의 벽 자체가 도달 범위 안에 없었다는 것 — 실기 로그와 코드 실측으로 둘 다 직접
확인했다. 이 두 사실은 옵션 1(전제 완화)이나 옵션 2(배회 가중치)로는 고쳐지지 않는다. 그래서:

- 확률 기본값은 0.20으로 상향 권고하되 빌드 캡처 확인 후 확정(9-4-A) — 그전까지는
- QA 강제 훅(`STICKMATE_QA_ROPE_CLIMB_CHANCE` + 임시 합성 시험벽, 9-4-C)으로 **오늘 밤 안에도** 실기
  검증이 가능하게 만들고,
- 1-C가 지적한 압축 중 실재하는 좁은 부분(hop-down의 원천봉쇄)만 한 줄 순서 교정으로 닫으며(9-4-B),
- 더 근본적인 지형 탐지 폭 문제(9-4-D)는 파쿠르 전체에 영향을 주는 더 큰 결정이라 범위를 명시하고
  다음 라운드로 넘긴다.

**개선 정도(정량 요약)**: 현재(0) → 관측 확률 0%(결정론적, 지형 유무 무관). 9-4-A+C 적용 후,
로프 대역 벽이 도달 범위 안에 있다는 전제 하에 3분 이내 관측 확률 **0% → 약 94%**(`ropeClimbChance
0.20` 기준, QA 훅으로 `=1`까지 올리면 사실상 즉시). 단, **이 개선치는 어디까지나 "지형이 있다면"이라는
조건부다** — 실제 사용자 데스크톱에 로프등반 대역 벽이 존재하는가는 그 사람의 실제 창 배치에
달려 있고(9-4-D를 고치기 전까지는), 오늘 밤 시험한 그 배치처럼 창이 하나도 없거나 Dock 경계에서
멀리 떨어져 있으면 자연 발생은 여전히 0에 가깝다 — **그래서 9-4-C(QA 강제 훅)가 "확률 조정"이 아니라
"오늘 밤 검증 가능성 확보"의 실제 핵심이다.**

Windows 영향: 없음 — 이 절의 결정(확률값·우선순위 순서 교정·환경변수 훅)은 `AutoWanderController.cs`/
`StickConfig.cs`에만 있고 플랫폼 분기가 없다. 단, 9-4-C의 QA 합성 시험벽은 `FallbackPlatformWindowService`
패턴을 재사용하므로, 실제 구현 시 Win32WindowService 경로(Windows)에서도 같은 합성 발판 삽입이 되는지
**함께 검토해야 한다**(CLAUDE.md 플랫폼 동시 검토 원칙 — "나중에 맞추자"는 금지). 9-4-D(인접 탐지 폭)도
마찬가지로 두 플랫폼 공통 로직(`GroundSensor.cs`, 플랫폼 중립)이라 이 자체는 이미 안전하지만, 후속
라운드가 값을 바꿀 때 macOS 실측만으로 판단하지 말 것.

### 확인/후속이 필요한 항목(이 절이 답하지 못한 것)
- 9-4-A: 프로덕션 기본값 0.20 확정은 실제 빌드 캡처 확인 이후(리더 승인 필요).
- 9-4-B/C: coder 구현 대상. 9-4-C는 test-engineer/qa-regression이 릴리스 게이트 테스트 1건
  (`EquipmentDebugUnlockReleaseGateTests.cs`와 같은 형태)을 반드시 동반해야 한다.
- 9-4-D: 인접 탐지 폭 확장 여부는 game-architect+coder+qa-regression 합동 검토가 필요한 별도 안건
  (파쿠르 전체 회귀 위험) — 이번 라운드는 문제 정의와 후보 방향만 남긴다.
- 이번 조사는 **오늘 밤 실행 중이던 한 인스턴스, 한 시점의 창 배치**만 실측했다 — 다른 창 배치
  (예: Dock 경계에 딱 붙은 큰 창이 있는 경우)에서는 사실 3의 결론이 달라질 수 있다. "이 데스크톱
  배치에서는 도달 불가"이지 "모든 데스크톱에서 도달 불가"가 아니라는 점을 리더 보고 시 명확히
  구분할 것.

---

## 요약 (리더 보고용 한 문단)

새 상태 `RopeClimbState`(`StickmanStateId.RopeClimb=28`)를 신설하고, 기존 되올라가기(3순위)
바로 위 높이 대역으로 끼워 넣는다 — 하한은 `ResolveStepUpMaxHeight()`(기존 파쿠르 상한과 이어
붙임), 상한은 `ropeClimbMaxHeights×H`와 "화면 클램프 상단"(신규 조회 1개, `TryGetWalkableScreenTopWorldY`)
중 작은 쪽이다. 자율 AI 판단으로 유지하며(활쏘기와 같은 전례, 유저 조작 신설 안 함), 트리거는
`AutoWanderController.TryRollEdgeAction()`에 `else if` 한 블록만 추가하면 된다(`GroundSensor`
자체는 무변경 — `TryFindDescendTarget`이 이미 쓰는 "높이 대역화" 패턴 재사용). Ragdoll 강제
인터럽트는 코드 추가 없이 이미 구조적으로 커버되고, 목표 소실 시 취소 경로만 페이즈별로 갈라야
한다(Throw 중=Idle/Walk, Ascend 중=Fall, 기존 파쿠르와 동일). 원칙 3은 기존 파쿠르와 같은 "좌표만
읽는다" 패턴으로 지켜지며 3가지 금지(포커스 강탈/창 흔들기/잔여 그림)를 명시했다. **이번 기능과
별개로 반드시 함께 고쳐야 할 기존 잠재 결함 1건**(`WalkState`의 무제한 높이 파쿠르 진입 경로,
현재는 `wanderEdgeJumpAttemptChance=0`이라 잠들어 있음)을 발견해 처방을 포함시켰다. 다음 단계는
design-motion(수치 4종 + Throw 세부 타이밍)과 coder(8개 파일 착수 목록)이며, 확인이 더 필요한
항목 2건(드래그 인터럽트, 로프 하강 대칭 기능)은 범위 밖으로 명시해 다음 라운드로 넘긴다.
