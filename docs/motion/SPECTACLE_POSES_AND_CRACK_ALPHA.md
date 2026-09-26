# 연출 4종 자세 설계 + 크랙 알파 튐 수정 방향

작성: design-motion · 2026-09-15 · 기준 HEAD `ad49497`
상태: **설계만 있고 실기 캡처는 0장이다.** 근거는 코드 판독과 오프라인 검산(§12)이다. 최종 판정은 실제 빌드 캡처로만 한다(TEAM.md 디자인 공통).
위치: 리더 브리프가 지정한 경로다. 기존 모션 문서는 `docs/` 루트(`MOTION_SPEC.md`, `UX_MOTION_*.md`)와 `design/motion/`에 있다(§11).
인용 형식: `파일:줄` 뒤에 **HEAD 파일에서 정확히 1회 나오는 고정 문자열 앵커**를 붙인다. 줄 번호는 전부 HEAD `ad49497` 기준이다. 작업 트리에서는 줄이 밀릴 수 있으니 앵커로 찾는다(예: `WindowCrashDirector.cs`는 A1 작업 중이라 +25줄).
배율 표기: 길이는 신장 배수 H로 적는다. pt는 **배율 1.0(H = 80.18pt)** 과 **배포 배율 0.75(H = 60.14pt)** 를 함께 적거나, 어느 배율인지 명시한다.

---

## 0. 요약

| 항목 | 판정 |
|---|---|
| 포즈 부재 | **참.** 4종 모두 `TimedSpectacleState`를 공유하고, 포즈 라우팅에 전용 분기가 없어 중립 포즈로 떨어진다. 「해머」 문자열은 프로덕션 코드 **7줄 · 4파일**에 있지만, 그 동작을 그리는 코드는 0줄이다. |
| 시각 인과 | HEAD에서는 오버레이 `Started`와 상태 전이가 **같은 프레임, 같은 동기 호출**이다. 그래서 지금은 역전이 보이지 않는다. **해머·분사 박자를 도입하면**(임팩트 0.41초, 분사 시작 0.64초) Enter 프레임에 발행한 금·페인트가 몸짓보다 먼저 나온다. 그러므로 박자를 도입할 때 **임팩트·분사 프레임에 발행해야 한다.** **원칙 1(텍스트) 위반은 아니다** — 대사는 `Started`에서 파생되지 않는다(§1-2). 해머 박자와 `Started` 이동은 **1.0 이후 연출 라운드**다(리더 판정). |
| 그라피티 지속 시간 | **매 프레임 다시 추첨된다.** 의도 평균은 4.0초인데, 실제로는 60fps 평균 3.22초, 배치모드 8000fps 평균 3.02초다. 1회 추첨으로 고치는 것은 **1.0 권고 작은 라운드**다(설정 필드·애셋 무변경, §9). |
| 청소부·블랙홀 | **배포 빌드에서 도달할 수 없다.** 디렉터가 씬·프리팹에 없고, 렌더러와 아이콘 조회도 없으며, 명령·단축키도 없다. 여기 적은 자세는 예약 사양이다. 렌더러보다 포즈를 먼저 넣으면 가리킬 대상이 없는 몸짓이 된다. |
| 크랙 알파 튐 | **참.** 60fps 부서짐 첫 프레임에서 균열선이 0.45 → 0.960(×2.13), 그림자선이 0.315 → 0.960(×3.05)이다. 유지 알파보다 진한 시간은 연속값 0.231초이고, 프레임 기준으로 60fps 13프레임(0.217초), 30fps 6프레임(0.200초)이다. |
| 의도 여부 | **의도가 아니다.** `802143f`가 알파를 0.95 → 0.45로 낮추면서 `SetAlpha(1 - t)`를 그대로 둔 회귀다(§7-3). |
| 수정 방향 | **페이드 시작 프레임에 선마다 실제 알파를 캡처해 곱한다**(`a_i(t) = a_i,start × (1 - t)`). **「페이드 시작 캡처 곱셈」 선례는 저장소에 없다**(§7-4 검색). 가까운 형태로는 「요소별로 생성 시 저장한 기준 알파 × (1 - t)」가 있다(`Interaction/RunawayRenderer.cs:659` 앵커 `c.a = t.StartAlpha * (1f - p);`). 1.0 권고 작은 라운드다(렌더러 한 파일). |

---

## 1. 코드 사실 (HEAD `ad49497`)

### 1-1. 상태와 포즈
- 등록
  - `Core/StickmanAgent.cs:749` 앵커 `UnityEngine.Random.Range(cfg.graffitiHoldDurationMin, cfg.graffitiHoldDurationMax)`
  - `Core/StickmanAgent.cs:755` 앵커 `cfg.windowCrashSwingDuration) },`
  - 네 상태 모두 대사 선택자가 없다.
- 상태 본체: `States/TimedSpectacleState.cs:80-104` 앵커 `public void Tick(float deltaTime)`. 접지 유지 → 타이머 → 만료 시 `Idle`.
  - 진행도 공개: `States/TimedSpectacleState.cs:50` 앵커 `public float Progress01`
  - 경과 초와 확정 지속시간은 비공개다.
- 포즈 라우팅: `States/StickmanBlackboard.cs:2162` 앵커 `private void TickPoseRouting(float deltaTime)`
  - 조기 return 분기의 예: 활쏘기 `States/StickmanBlackboard.cs:2250` 앵커 `if (Machine.CurrentStateId == StickmanStateId.Archery) return;`
  - 집중 모드: `States/StickmanBlackboard.cs:2309-2319` 앵커 `float focusProgress = Machine.GetState(focusId) is TimedSpectacleState timed`
  - 네 상태가 떨어지는 곳: `States/StickmanBlackboard.cs:2358-2365` 앵커 `if (TickIdleAmbientMotion(deltaTime))` (유휴 앰비언트 또는 `ApplyIdlePose`)
- 활쏘기 선례: `git --no-optional-locks log -S Archery -- Assets/_Project/Scripts/States/StickmanPoseAnimator.cs` → 5커밋, 도입 `09ab271`. 같은 파일에 `-S WindowCrash`는 0커밋이다. (브리프의 「8건」과는 셈 범위가 다른 것으로 보인다. 이 문서는 한 파일 기준이다.)
  - 전용 상태 `ArcheryState`
  - `States/StickmanPoseAnimator.cs:3064` 앵커 `public void ApplyArcheryPose(`
  - `States/StickmanPoseAnimator.cs:4413` 앵커 `public readonly struct ArcheryPoseSettings`
  - 각도 규약 `Core/StickConfig.cs:3388-3391` 앵커 `각도 0이 "곧게 아래"`
  - 소품 렌더러 `ArcheryRenderer`
  - 수평·방향 소유권 멤버십
- 집중 모드 선례: `TimedSpectacleState`는 그대로 두고, 라우팅 분기가 `Progress01`을 읽어 `States/StickmanPoseAnimator.cs:1084` 앵커 `public void ApplyFocusPose(` 를 부른다.
- 「해머」 서술 7줄 · 4파일. 계수 절차: `git --no-optional-locks ls-tree -r --name-only HEAD -- Assets/_Project/Scripts`의 .cs 715개를 `git --no-optional-locks show HEAD:경로`로 작업 공간에 사본을 뜬 뒤, 사본에서 `grep -rn -F 해머`(테스트 제외)로 셌다. 음성 대조(없는 문자열) 0.
  - `Interaction/WindowCrashDirector.cs:8` 앵커 `활성 창에 해머를 내리치는 캐릭터 스윙(States.TimedSpectacleState`
  - `Interaction/WindowCrashDirector.cs:220` 앵커 `캐릭터의 해머 스윙`
  - `Core/StickmanEventBus.cs:70` 앵커 `활성 창에 해머를 내리치는 캐릭터 스윙 모션`
  - `Core/StickmanEventBus.cs:737` 앵커 `캐릭터의 짧은 해머 스윙 상태`
  - `Core/StickConfig.cs:1387` 앵커 `캐릭터의 해머 스윙 모션 지속 시간(초)`
  - `Interaction/WindowCrashRenderer.cs:74` 앵커 `해머를 든 캐릭터가 균열 앞에`
  - `Interaction/WindowCrashRenderer.cs:213` 앵커 `캐릭터(해머를 휘두른 쪽)에 가깝게`
  - ※ 브리프의 220줄은 `Interaction/WindowCrashDirector.cs:220` 앵커 `캐릭터의 해머 스윙` 이다. StickmanBlackboard.cs 220줄은 빈 줄이고, 활쏘기 필드는 `States/StickmanBlackboard.cs:211-219` 앵커 `public Vector2 ArcheryTargetWorld;` 다.

### 1-2. 이벤트 순서 — 시각 인과 (원칙 1 텍스트 위반 아님)
- 크랙(명령): `Interaction/WindowCrashDirector.cs:130-133` 앵커 `강제 발동({reason})`. `RaiseOverlay(Started)` 다음 줄에서 `ChangeState(WindowCrash)`를 호출한다.
- 크랙(자동): `Interaction/WindowCrashDirector.cs:219-222` 앵커 `캐릭터의 해머 스윙`. 순서가 같다.
- 렌더러는 `Started`를 받는 **같은 호출 안에서** 균열을 만든다.
  - `Interaction/WindowCrashRenderer.cs:162-164` 앵커 `Begin(evt.TargetRectOsScreen);`
  - `Interaction/WindowCrashRenderer.cs:243-245` 앵커 `ApplyGrowth(0f);`
- 크랙 3초 수명 타이머는 **디렉터**가 센다: `Interaction/WindowCrashDirector.cs:189-193` 앵커 `_overlayTimer += dt;`
- 그라피티
  - 명령: `Interaction/GraffitiDirector.cs:110-114` 앵커 `_cooldownRemaining = 0f;`
  - 자동: `Interaction/GraffitiDirector.cs:168-175` 앵커 `// 빈 영역 못 찾음`
  - 렌더러가 `Started` 시점부터 1.35초 동안 그린다: `Interaction/GraffitiRenderer.cs:47` 앵커 `private const float DrawSeconds = 1.35f;`, `Interaction/GraffitiRenderer.cs:240-245` 앵커 `ApplyProgress(t * _totalLength);`
- 청소부·블랙홀: `Interaction/DesktopIconMirrorDirector.cs:151-152` 앵커 `_player.Blackboard.Machine.ChangeState(TargetStateId);`
- **판정(리더 판정 수용)**
  - `Started`와 상태 전이는 같은 프레임, 같은 동기 호출이다. 따라서 HEAD에서 사용자가 보는 순서 역전은 없다.
  - 문제는 「**Enter 프레임 발행**」 대 「**앞으로 도입할 임팩트 박자**」다.
  - 대사는 `Started`에서 파생되지 않는다. 오버레이 구독자는 렌더러뿐이다(소스 기준 `Interaction/GraffitiRenderer.cs:100` 앵커 `StickmanEventBus.GraffitiOverlayChanged += OnOverlayChanged;` 와 크랙 렌더러의 구독 1곳. 빌드 IL 전수는 verify-change 확인). 말풍선은 `States/` Enter의 `DialogueIntent`에서만 나온다(§1-4).
  - ⇒ 원칙 1(텍스트) 위반이 아니라 **시각 인과** 문제다.

### 1-3. 청소부·블랙홀 도달 불가 (양성·음성 대조)
| 계기 | 결과 | 대조 |
|---|---|---|
| `DesktopIconMirrorDirector.cs.meta` GUID를 `Assets` 전체 `.unity/.prefab/.asset`에서 검색 | **0개 파일** | 양성: `GraffitiDirector` GUID → `Stickman.prefab` 1 / 음성: 가짜 GUID → 0 |
| 부트스트래퍼의 해당 `AddComponent` | 0 | 양성: `Editor/SceneBootstrapper.cs:1051` 앵커 `var graffiti = root.AddComponent` · `Editor/SceneBootstrapper.cs:1086` 앵커 `var windowCrash = root.AddComponent` |
| `DesktopIconMirrorOverlayChanged` 구독자 | 0(발행자와 선언만) | 양성: 그라피티 렌더러 구독(§1-2) |
| 아이콘 조회 | Windows 정직한 스텁 `Platform/Windows/Win32WindowService.cs:2132` 앵커 `public bool TryGetIconRegion(out Rect osScreenRegion)` / macOS 미구현 `Platform/MacOS/MacWindowService.cs:59` 앵커 `IDesktopIconLayoutService는 이번 라운드에도 의도적으로 구현하지 않는다` | — |
| 자동 확률 | `Data/DefaultStickConfig.asset:165` 앵커 `desktopTidyChance: 0` · `Data/DefaultStickConfig.asset:169` 앵커 `blackholeChance: 0` | — |
| 명령·단축키 | 명령창은 4종(활쏘기·그라피티·창 도둑·크래시) `Interaction/ActionCommandPopover.cs:505-531` 앵커 `public CommandAvailability GetAvailability(Command command)` / 단축키 G·T·X `Interaction/AppControlDirector.cs:362-364` 앵커 `Invoke(ControlAction.WindowCrash, HotkeySource("X"));` | — |

### 1-4. 소유권 · 대사 · 가드 · 프레임
- 수평: 네 상태 모두 자기소유가 아니다.
  - `States/StickmanBlackboard.cs:943` 앵커 `public static bool IsHorizontalMotionSelfManaged(StickmanStateId id)`
  - 계약 테스트 `HorizontalMotionOwnershipContractTests`의 `순수_타이머_연출_상태는_전부_안전망의_보호를_받는다`가 잠근다.
  - 안전망 `States/StickmanBlackboard.cs:1121` 앵커 `public void TickHorizontalDriftSafetyNet(float deltaTime)` 이 `Data/DefaultStickConfig.asset:441` 앵커 `horizontalDriftBrakeSeconds: 0.14` 선형 램프로 속도를 0에 고정한다.
- 방향: 네 상태 모두 없다 — `States/StickmanBlackboard.cs:1016` 앵커 `public static bool IsFacingSelfManaged(StickmanStateId id)`
- 대사
  - 선택자 없음: `States/TimedSpectacleState.cs:69` 앵커 `if (_dialogueTextSelector == null) return;`
  - 혼잣말 호출자는 둘뿐이다: `States/IdleState.cs:53` 앵커 `AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, _chatterParams)` · `States/WalkState.cs:54` 앵커 `AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Walk, _chatterParams)`
  - `Dialogue/` 폴더의 상태 참조: Walk 6 · Dragged 2(양성), 네 상태 0.
- 전체화면 등급 2: `Core/StickmanAgent.cs:1841-1848` 앵커 `current == StickmanStateId.BlackholeSummon || current == StickmanStateId.WindowCrash)` → 강제 `Idle`.
- A1(명령 크랙 회색)
  - HEAD `Interaction/WindowCrashDirector.cs:79-104` 앵커 `public CommandAvailability GetAvailability()` 에는 등급 1·임대 가드가 없다(HEAD 기준 미착지).
  - HEAD의 표면 가드는 `Interaction/WindowCrashDirector.cs:170-171` 앵커 `_player.IsSuspended) { CancelOverlay(); return; }` 한 곳이다.
  - 작업 트리에서는 A1이 진행 중이다(+25줄, 커밋 전). 새 가드가 같은 술어 `UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces`를 읽는다 → §3-7 요구와 방향이 맞는다.
- 프레임
  - 네 상태는 `Idle`이 아니라 활성 등급이다: `Platform/FramePacing.cs:541` 앵커 `return blackboard.Machine.CurrentStateId == Core.StickmanStateId.Idle;`
  - `Data/DefaultStickConfig.asset:416` 앵커 `windowsTargetFrameRate: 60` · `Data/DefaultStickConfig.asset:418` 앵커 `macVSyncInterval: 2`
  - 60Hz 디스플레이 macOS는 30fps로 추정하나 미확인이다. **박자는 최악 30fps로 검산한다.**

---

## 2. 공통 계약

| # | 계약 | 근거 |
|---|---|---|
| C-1 | **시각 인과**: 효과의 첫 픽셀은 그 효과를 만든 몸짓의 접촉·분사 프레임 이후에 나온다. 박자를 도입하는 라운드에서 `Started`를 그 프레임으로 옮긴다. | §1-2(원칙 1 텍스트 위반 아님) |
| C-2 | 박자 시계는 하나 — 상태 자신의 경과 시간이다. 포즈·디렉터·렌더러가 **같은 순수 박자표 함수**를 읽는다. 경과 초 읽기 창구가 필요하다. | `States/TimedSpectacleState.cs:40-44` 앵커 `선택자는 순수 함수가 아닐 수 있다` |
| C-3 | 지속 시간은 **Enter에서 1회** 확정한다. | §4-1 |
| C-4 | 접촉 박자(내리치기·차기·주먹 쥐기)는 스무딩 없이 직접 기록하고, 준비·회복만 스무딩한다. | 스무딩 램프 지연 base 35: 상완 51.9ms · 전완 69.3ms(검산 [1]). 즉시 대입 경로 `States/StickmanPoseAnimator.cs:3643-3650` 앵커 `segment.CurrentAngle = rate` · 선례 `States/StickmanPoseAnimator.cs:3253` 앵커 `private void ApplyCostumeSegment(` |
| C-5 | 박자 경계의 관절별 각도차는 절댓값 170° 이하(최단 경로 여유 10° 이상). **소품 자루 절대각은 `LerpAngle` 금지** — 전완 실제각 + 손목 곡선으로 합성한다. | 자루 절대각 실제 −235° / 최단 +125°(검산 [2-2]) |
| C-6 | **수평 소유권**: 네 상태 모두 「수평 자기소유 아님」을 유지한다(청소부 접근 보행만 예외, §5). 상태·포즈는 `Body` 속도·루트 위치를 쓰지 않고, 가라앉음은 시각 오프셋으로만 한다. 방향을 바꾸는 연출은 `IsFacingSelfManaged` 멤버십을 추가하고, 돌아서기는 정지 램프(0.14초)가 끝난 뒤에 한다. | 계약 테스트 두 목록 갱신 |
| C-7 | **발 고정**: 스탠스 전환은 정지 박자 안에서 끝내고, 이후 무릎 굽힘은 엉덩이각을 역산해 발끝 x를 고정한다. **두 발 높이차 허용치는 배율 1.0 기준 0.5pt(= 0.0062H) 이하**다 — 최대 배율(`Core/StickConfig.cs:2311` 앵커 `public const float MaxCharacterScale = 1.0f;`)에서 가장 크게 보이므로 거기서 잰다. 배포 0.75에서는 0.38pt에 해당한다. | 활쏘기 스탠스(앞 16 / 뒤 −18 / 무릎 12)를 그대로 쓰면 높이차 **0.0283H = 배율 1.0에서 2.27pt / 배포 0.75에서 1.70pt**(검산 [2-3]) → 허용 초과. 뒷다리 −4.63°면 0 |
| C-8 | 소품(해머·캔)은 그 상태 동안만 보인다. 등장·퇴장 페이드는 30fps 2프레임 이상(0.07초). | `ArcheryReadyRatio` 관례 |
| C-9 | 대사를 붙인다면 접촉·분사가 확정된 뒤에 파생한다(원칙 1). Enter에서는 금이 날지 취소될지 모른다. | design-narrative 인계 |
| C-10 | **효과를 낼 수 없으면 몸짓도 없다.** 시작 전 가능 판정에 더해, 접촉 직전에 **같은 술어 함수로 재판정**한다. 거짓이면 접촉 박자에 들어가지 않고 거두기로 가며, 효과 이벤트는 0이다. | §3-7 |

---

## 3. 크랙 — 두 손 오버헤드 해머 내리치기 (1.0 이후 연출 라운드)

### 3-1. 박자 (합 0.85초, 돌아서기 필요 시 +0.14초)
| 구간 | 초 | 곡선 | 30fps 프레임 | 유도 |
|---|---:|---|---:|---|
| (돌아서기) | 0.14 | 램프 끝에 반전 | 4.2 | = `horizontalDriftBrakeSeconds`. 대상이 등 뒤일 때만 |
| windup 들어올리기 | 0.24 | ease-in-out, 스무딩 | 7.2 | 정지 박자 0.14 이상. 평균 667°/s로, 내리치기 1300°/s의 약 1/1.95 |
| apex 정점 멈춤 | 0.07 | 정지 | 2.1 | 30fps 2프레임(0.0667)을 올림 |
| strike 내리치기 | 0.10 | ease-in u², 직접 기록 | 3.0 | 30fps 3프레임. 더 짧으면 순간이동으로 보인다 |
| **임팩트** | 0.41 시점 | 이 프레임에 `Started` | — | §3-4 |
| hold 임팩트 유지 | 0.22 | 정지(웅크림) | 6.6 | = 렌더러 `GrowSeconds`(`Interaction/WindowCrashRenderer.cs:60` 앵커 `private const float GrowSeconds = 0.22f;`, HEAD에서 private const → 공유화 선행) |
| recover 거두기 | 0.22 | ease-out, 스무딩 | 6.6 | 유지와 대칭 |

- `Data/DefaultStickConfig.asset:184` 앵커 `windowCrashSwingDuration: 0.4` 를 박자 합으로 대체하면 **D 등급 파일**(설정·애셋)과, 선택자를 바꿀 경우 **B 등급** `StickmanAgent.cs`에 닿는다 → §9 N-8 표.
- 크랙 3초 수명은 임팩트부터 센다.

### 3-2. 자세표 (방향 중립 절대각: 0 = 곧게 아래, +90 = 정면 수평)
리드 손(앞팔) = 자루 끝. 뒷손은 리드 손에서 자루 끝 쪽으로 0.08H 떨어진 곳을 매 프레임 2마디 IK로 잡는다(팔꿈치 굽힘 양수 가지).

| 키 | 앞팔 상완 | 앞팔 상대굽힘 | 손목 w(자루 = 전완 + w) | 상체 기울임 | 다리 |
|---|---:|---:|---:|---:|---|
| idle | 40 | 10 | −40 (자루 10) | 0 | Idle |
| windup(=apex) | 200 | 50 | +40 (자루 290) | −6 | 앞 16 / 뒤 −4.63 / 무릎 12 |
| impact_mid(창유리 치기) | 70 | 6 | −21 (자루 55) | +8 | 무릎 26, 발끝 x 고정 역산 |
| windup_low(바닥 치기 전용) | 190 | 50 | +40 | −6 | windup과 같음 |
| impact_low(바닥 치기) | 22 | 4 | −18 (자루 8) | +12 | 무릎 100 스쿼트, 발끝 x 고정 역산 |

기하 결과(자루 0.30H, 발바닥 y = 0):
| 항목 | H | 배율 1.0 pt | 배포 0.75 pt |
|---|---|---|---|
| windup 해머 머리 | (−0.530, +0.884) | (−42.5, +70.9) | (−31.9, +53.1) |
| impact_mid 해머 머리 | (+0.611, +0.495) | (+49.0, +39.7) | (+36.8, +29.8) |
| impact_low 해머 머리 | (+0.252, +0.0195) | (+20.2, +1.56) | (+15.2, +1.17) |
| impact_low 엉덩이 강하 | 0.1502 | 12.04 | 9.03 |
| impact_mid 웅크림 강하(참고: 16/−18 출발) | 0.0085 | 0.68 | 0.51 |

- 뒷손 IK: windup 상완 −183.6 / 굽힘 66.8, impact_mid 39.6 / 79.0, impact_low −10.1 / 79.5.
- 관절차: idle→windup 상완 +160, windup→impact_mid −130, windup_low→impact_low −168. 모두 170 이하. impact_low에 windup 200을 쓰면 −188로 반대로 돈다 → 전용 190을 둔 이유다.
- 뒷다리 −4.63에서 출발한 웅크림 수치는 미산출이다(coder IK 산출, C-7 허용치 적용).

### 3-3. 무게감
- 임팩트 머리 속도 41.1 H/s. 자루 0.26–0.36H 범위에서는 37.9–46.0 H/s다.
- 30fps 마지막 한 프레임에 머리가 1.01H 이동한다 → 잔상이 필요하다(design-art).
- 유지 박자에는 멈춘 채 웅크리고 앞으로 +8°. 튕기는 반동은 두지 않는다.

### 3-4. 임팩트 동기 (시각 인과)
- **임팩트 프레임** = strike 곡선 파라미터가 1에 도달한 프레임(경과 ≥ 돌아서기 + 0.41초).
- 디렉터는 전이 시점에 락과 대상 스냅숏만 잡고, 같은 박자표 함수로 임팩트 도달을 보고 **한 번만** `Started`를 발행한다(래치). 스크립트 실행 순서상 최대 1프레임 늦을 수는 있지만, **이르면 안 된다.**
- **균열 원점** = 임팩트 순간 해머 머리 월드 좌표. 블랙보드 스냅숏으로 넘긴다(`ArcheryTargetWorld` 관례).
  - 현행 렌더러는 캐릭터 몸 위치를 창 안쪽 30%로 클램프한다: `Interaction/WindowCrashRenderer.cs:213-217` 앵커 `float impactX = Mathf.Clamp(characterWorld.x - center.x`
  - 캐릭터가 창 윗변에 선 (나) 경우, 창 높이 300 / 600 / 900pt에서 해머 접점(윗변 위 0.0195H)과 렌더러 타격점(윗변 아래 0.2 × 창 높이)은 배포 배율에서 **1.02 / 2.02 / 3.01H** 떨어진다(검산 [7]). 원점을 가장자리까지 허용할지는 design-art 판단이다.

### 3-5. 접점 규칙
1. (가) impact_mid 머리 접점이 대상 창 사각형 안에 있으면 창유리 치기.
2. (나) `States/StickmanBlackboard.cs:414` 앵커 `public long CurrentFootholdHandle;` 이 대상 핸들이면 바닥 치기. 금 원점은 윗변 위다.
3. (다) 둘 다 아니면 스윙하지 않는다. 가능 판정이 Blocked를 반환하고, 사유 문구는 design-narrative가 맡는다.
   - 현행 대상 선정 `Interaction/WindowCrashDirector.cs:227` 앵커 `private bool TryFindForegroundWindow(out PlatformFoothold target)` 은 거리를 보지 않는다.
- 캐릭터가 수평을 소유하지 않아 제자리에 있으므로, Enter에서 판정하고 임팩트 직전에 한 번 더 확인한다.

### 3-6. 수평·방향 소유권 (coder 인계 계약)
- 수평: 자기소유 아님을 유지한다. 보행 속도는 안전망이 0.14초에 0으로 만들고, 이 램프는 windup 안에서 끝난다. 임팩트는 반드시 정지 후다.
- 방향: 대상이 등 뒤일 때만 돌아서기 박자를 넣고, `WindowCrash`를 `IsFacingSelfManaged`와 계약 테스트 `ExpectedFacingSelfManaged`에 추가한다.
- 발: C-7.

### 3-7. 가드 정합 — 막힌 경우 스윙이 없는가
| 경우 | 현행 HEAD | 이 설계 |
|---|---|---|
| A1 명령 크랙 등급 1 회색 | 가드가 없어 스윙이 나오고, 다음 프레임 표면 가드가 금을 취소한다 | A1이 착지하면 Blocked → `ForceTriggerNow` false → 전이 없음 → **자세 0**. (다) 규칙과 A1 가드를 같은 판정 함수에 둔다 |
| A2 자동 발동 등급 1(백로그, 출하 확률 0) | 확률을 올리면 「결과 없는 휘두르기」 | 접촉 직전 재판정이 표면 가드와 같은 술어를 읽어 내리치지 않는다. 들어올리기 0.24초는 남으므로 **A2 가드는 여전히 필요** |
| 임대가 windup 중에 켜짐 | 금만 취소된다 | 같은 술어 → 거두기, 금 0 |
| 등급 2 / 사용자 숨김 | 강제 `Idle`(§1-4) | 접촉 전이면 `Started` 0(래치가 상태 ID와 사이클 확인), 후면 표면 가드 취소 |
| 대상 창이 windup 중에 닫힘 | 금이 떴다가 취소 | 전이 시점부터 감시 → 거두기, 금 0 |
| 긴급정지 | `Interaction/WindowCrashDirector.cs:264` 앵커 `private void OnEmergencyStop()` | 같음 |

---

## 4. 그라피티 — 스프레이 캔

### 4-1. 선행 결함: 지속 시간 매 프레임 재추첨 (1.0 권고 작은 라운드)
- 선택자는 `Random.Range(cfg.graffitiHoldDurationMin, cfg.graffitiHoldDurationMax)`(float 3f / 5f, §1-1)다.
- 상태는 매 Tick 선택자를 다시 부르고 비교한다: `States/TimedSpectacleState.cs:94-99` 앵커 `_lastDurationSeconds = duration;`
- 몬테카를로 20,000회(검산 [4]):

| fps | 평균 | p50 | p95 | 최대 |
|---:|---:|---:|---:|---:|
| 30 | 3.313 | 3.300 | 3.600 | 4.167 |
| 60 | 3.223 | 3.217 | 3.433 | 3.767 |
| 8000 | 3.020 | 3.018 | 3.039 | 3.077 |
| 의도 | 4.000 | — | — | 5.000 |

- 수정: Enter에서 1회 추첨해 저장한다. **파일은 `States/TimedSpectacleState.cs` 하나이고, 설정 필드·애셋·`StickmanAgent.cs`는 0줄**이다(§9).
- 부작용 점검(qa 인계): 같은 클래스를 쓰는 다른 9개 등록(청소부·블랙홀·크랙 3 · 투두 1 · 집중 4 · SULKY 1, 그라피티 포함 전체 10)은 지속시간 선택자가 순수 필드 읽기라 결과가 같다. 다만 **상태 도중 설정값을 바꾸면 이제 반영되지 않는다.**

### 4-2. 박자 (1.0 이후 연출 라운드)
| 구간 | 시작–끝(초) | 곡선 | 유도 |
|---|---|---|---|
| stop 정지 | 0.00–0.14 | 중립 | = `horizontalDriftBrakeSeconds` |
| turn 돌아서기 | 0.14 | 순간(필요 시) | C-6 |
| shake 캔 흔들기 2회 | 0.14–0.50 | 사인 2주기 | 극점 간격 90ms = 30fps 2.7프레임 |
| aim 겨누기 | 0.50–0.64 | ease-out | 첫 획 끝점 방향 |
| **spray 분사** | 0.64–1.99 | 획 끝점 추종 | = 렌더러 `DrawSeconds` 1.35(공유화 필요) |
| lower 캔 내리기 | 1.99–2.19 | ease-out | — |
| admire 감상 | 2.19–종료 | 정지 | 1회 추첨 후 0.81–2.81초 |

- `Started`는 분사 시작 프레임에 발행한다(C-1).
  - 영역 겹침 감시 `Interaction/GraffitiDirector.cs:137` 앵커 `private void MonitorRegion()` 는 상태가 `Graffiti`인 동안 돌므로 전이 시점부터 유지된다.
  - 분사 전에 취소되면 `Started`는 0이다. 렌더러 페이드는 `None`이면 return한다: `Interaction/GraffitiRenderer.cs:300-303` 앵커 `private void BeginFade(float seconds)`
- 그리기 진행도는 상태 경과 − 0.64로 계산한다(C-2).

### 4-3. 자세
- 캔을 든 팔 = 앞팔.
  - shake: 상완 60, 전완 절대 150(가슴 앞), 상대굽힘 ±25°.
  - spray: 상완 θ = `atan2(dx_facing, -dy)`(어깨 → 현재 획 끝점), 15–165°로 클램프, 상대굽힘 +6.
- 추종 폭: 영역 96pt, 거리 200–300pt에서 **18.2–27.0°**(pt 고정 필드라 배율 무관). 거리는 배율 1.0에서 2.49–3.74H, 배포 0.75에서 3.33–4.99H다. UX_FLOW 27-3의 H 비례 개정은 코드에 없다.
- 뒷팔: 허리에 손(IK 목표점 수치 미산출). 다리: 크랙 windup 스탠스.
- 몸과 3H 넘게 떨어진 그림을 잇는 **분무선**(노즐 → 획 끝점)이 필수다(design-art · coder).
- 획 끝점을 포즈가 알아야 한다. 경로 스냅숏을 전이 전에 블랙보드에 쓰고(활쏘기 관례), 포즈와 렌더러가 스냅숏 + 상태 경과를 함께 읽는다(game-architect 판단).

### 4-4. 소유권
수평 자기소유 아님을 유지한다. `Graffiti`를 `IsFacingSelfManaged`에 추가하고, 돌아서기는 0.14초 뒤에 한다.

---

## 5. 청소부 — 발로 차 정렬 (예약 사양 · 도달 불가 · 각도 기하 미검산)
- 착수 조건: 복제 스프라이트 렌더러 + 양 플랫폼 아이콘 조회 + 디렉터 배치.
- 아이콘 격자에 발이 닿으려면 **접근 보행이 필수**다. 그러면 수평 자기소유로 옮기고 제자리 구간 감쇠 의무를 진다.
  - 선례 `States/ArcheryState.cs:250-269` 앵커 `float damping = (_cfg != null ? _cfg.archeryHorizontalDamping : 14f);`
  - 접근을 채택하지 않으면 차기 몸짓은 폐기한다.
- 박자(접근 제외 2.50초 = 설정 `desktopTidyDurationSeconds`): 차기 1회 0.58초(chamber 0.16 + kick 0.10 직접 기록 + hold 0.14 + recover 0.18) × 3 + 「짠」 0.40 + 정리 0.36.
- 동기: kick 끝 프레임 = 아이콘 한 줄 슬라이드 시작.
- 형태 제안(미검산): 차는 다리 chamber 엉덩이 +60 / 무릎 90 → kick +75 / 8, 지지 다리 −6 / 10, 팔 반대 스윙, 기울임 −6. 착수 전에 접지·크리즈를 검산해야 한다.

## 6. 블랙홀 — 손바닥 소환 (예약 사양 · 도달 불가 · 각도 기하 미검산)
- 착수 조건은 §5와 같다. 원격 연출이라 접근이 없고, 수평 자기소유 아님을 유지한다. 방향은 아이콘 영역 쪽이다(멤버십 추가).

| 구간 | 초 | 동기점 |
|---|---|---|
| raise 손바닥 들기 | 0.00–0.30 | 끝 프레임 = 블랙홀 등장 |
| spawn-hold 버팀 | 0.30–0.50 | — |
| suction 빨아들임(기울기 0 → −8) | 0.50–1.70 | 흡입 궤적 |
| collapse 주먹 쥠(직접 기록) | 1.70–1.80 | 끝 프레임 = 소멸 + 튕겨남 시작 |
| recoil 튕김 | 1.80–2.05 | — |
| flinch 움찔 | 2.05–2.35 | 아이콘 복귀 |
| settle | 2.35–2.50 | — |

- 형태 제안(미검산): 앞팔 상완 95 / 전완 절대 150, 뒷팔은 앞 손목 받침 IK. 블랙홀 앵커 = 앞손 + (0, +0.06H).

---

## 7. 크랙 알파 튐

### 7-1. 사실
- 선 알파는 생성 시 한 번만 정해진다.
  - `Interaction/WindowCrashRenderer.cs:89` 앵커 `private const float CrackMaxAlpha = 0.45f;`
  - `Interaction/WindowCrashRenderer.cs:192` 앵커 `_crackShadowColor = new Color(crackInk.r, crackInk.g, crackInk.b, CrackMaxAlpha * 0.7f);`
- 성장 함수는 점 개수만 바꾼다: `Interaction/WindowCrashRenderer.cs:362` 앵커 `private void ApplyGrowth(float t)`. → **표시 알파는 「0.45 × 진행도」가 아니라 상수 0.45 / 0.315다.**
- 페이드 두 경로가 절대값을 대입한다.
  - 부서짐: `Interaction/WindowCrashRenderer.cs:342-349` 앵커 `TickShards(Time.deltaTime);`
  - 취소: `Interaction/WindowCrashRenderer.cs:351-357` 앵커 `float t = Mathf.Clamp01(_modeTimer / _fadeSeconds);`
  - 공통 대입: `Interaction/WindowCrashRenderer.cs:443` 앵커 `private void SetAlpha(float alpha)`
  - 섬광은 캡만 씌운다: `Interaction/WindowCrashRenderer.cs:459` 앵커 `f.a = Mathf.Min(f.a, alpha);`
- 취소 페이드 진입은 부서짐 중을 막지 않는다: `Interaction/WindowCrashRenderer.cs:434-437` 앵커 `private void BeginFade(float seconds, string reason)`. → 부서짐 → 취소 순서면 알파가 1에서 다시 시작한다. 현 디렉터는 `Completed` 뒤에 `Cancelled`를 보내지 않으므로 **지금은 도달 불가**이고, 렌더러 계약상으로만 열려 있다.

### 7-2. 크기 (검산 [3], 독립 재계산 일치)
| 경로 | 선 | 첫 프레임 30 / 60 / 120fps | 유지보다 진한 시간: 연속값 | 30fps | 60fps |
|---|---|---|---|---|---|
| 부서짐 0.42초 | 균열 0.45 | 0.921 / **0.960(×2.13)** / 0.980 | 0.231초(55%) | 6프레임 0.200초 | 13프레임 0.217초 |
| 부서짐 | 그림자 0.315 | 0.921 / **0.960(×3.05)** / 0.980 | 0.288초(68%) | 8프레임 0.267초 | 17프레임 0.283초 |
| 취소 0.12초 | 균열 | 0.722 / 0.861(×1.91) / 0.931 | 0.066초 | 1프레임 0.033초 | 3프레임 0.050초 |
| 취소 | 그림자 | 0.722 / 0.861(×2.73) / 0.931 | 0.082초 | 2프레임 0.067초 | 4프레임 0.067초 |

### 7-3. 의도 여부 — 의도가 아니다
1. `git --no-optional-locks log -L` 로 `SetAlpha` 본문 이력을 보면 `953d92a` 한 커밋뿐이다.
2. 원판 `953d92a`의 알파는 0.95 / 0.85였고, 첫 프레임 증가는 +0.010 / +0.110으로 거의 연속이었다.
3. `802143f`가 0.45로 낮추며 의도를 명시했다(`Interaction/WindowCrashRenderer.cs:85-89` 앵커 `알파도 낮춰(CrackMaxAlpha)`): 캐릭터보다 뒤로 물러나 보이게. 이 커밋은 `SetAlpha`를 건드리지 않았다.
4. 클래스 문서의 Completed 서술: `Interaction/WindowCrashRenderer.cs:44-46` 앵커 `동시에 옅어진다`.
5. 번쩍임에 해당하는 요소는 별도의 섬광이다: `Interaction/WindowCrashRenderer.cs:239-241` 앵커 `_flash = CreateLine("ImpactFlash"`, 감쇠 `Interaction/WindowCrashRenderer.cs:399` 앵커 `private void TickFlash()`. 임팩트 시점 전용이고 유지 중에는 알파 0이다.

→ `802143f`의 회귀다.

### 7-4. 수정 방향 (1.0 권고 작은 라운드, 렌더러 한 파일)
- **채택: 페이드 시작 프레임에 선마다 실제 알파를 캡처하고 곱한다.**
  - 60fps 부서짐 첫 프레임: 균열 0.4321 · 그림자 0.3025 — 둘 다 유지값 이하, 비율 0.7 유지.
  - 섬광도 같은 방식으로 한다(현행 `Min`은 성장 중 취소 시 섬광 감쇠를 멈춘다).
  - 캡처 방식이라 부서짐 → 취소 재진입도 연속이다.
- 기각 1: 절대 1에서 시작(현행).
- 기각 2: 「고정 상수 × (1 - t)」 — 선별 비율이 사라지거나, 기준값이 바뀌면 조용히 갈라진다.
- **선례 조사**(고정 문자열·정규식 검색, 양성 대조 포함)
  - `Interaction/WindowTheftRenderer.cs:474` 앵커 `c.a = DustColor.a * (1f - t) * CurrentGlobalAlpha();` 는 **정적 상수 곱 = 기각 2의 형태**다(선례 아님).
  - `Interaction/RunawayRenderer.cs:659` 앵커 `c.a = t.StartAlpha * (1f - p);` 는 **요소별로 생성 시 저장한 기준 알파**의 곱이다(`Interaction/RunawayRenderer.cs:591` 앵커 `StartAlpha = startAlpha,`). 페이드 시작 캡처는 아니지만 「선마다 자기 기준 알파를 들고 곱한다」는 부분은 이 설계와 같다.
  - **페이드 시작 시점 캡처 곱셈 선례는 찾지 못했다**: `Scripts` 전체(테스트 제외)에서 알파 필드 캡처 대입 0건, 캡처형 곱 후보는 위 `RunawayRenderer` 1건뿐. 양성 대조 `DustColor.a * (1f - t)` 1건 적중.
- 부서짐 순간 번쩍임을 원한다면(design-art): 선 알파와 분리한 별도 요소로, 자기 포락선을 갖게 한다.

### 7-5. 같은 경로 전수 (범위 밖 후보)
| 파일 | 형태 | 기준 알파 | 튐 |
|---|---|---|---|
| `Interaction/WindowTheftRenderer.cs:380` 앵커 `SetAlpha(1f - t);` | 절대 대입 | `Interaction/WindowTheftRenderer.cs:96` 앵커 `GhostTitleColor = new Color(0.24f, 0.52f, 0.92f, 0.60f);` · 테두리 0.95 | 있음(크기는 페이드 길이 인자에 따라 다름, 미측정) |
| `Interaction/GraffitiRenderer.cs:256` 앵커 `SetAlpha(1f - t);` | 절대 대입 | `Interaction/GraffitiRenderer.cs:55` 앵커 `private static readonly Color[] SprayColors` (1.0) | 지금은 없음 |
| `Interaction/HardwareReactionRenderer.cs:341` 앵커 `SetAlpha(1f - t);` | 절대 대입 | 아이콘 1.0, 깜빡이 선의 페이드 시작값 미확인 | 미확인 |
| `Interaction/ArcheryRenderer.cs:608` 앵커 `alpha = 1f - t;` | 절대 | 미확인 | 미확인 |

---

## 8. 테스트로 잴 수 있는 수치 (벽시계 · 상수를 베끼지 않음)

**순서 규칙**
- HEAD에 없는 창구를 읽는 테스트는 **컴파일이 안 된다.** 따라서 ① 창구만 추가하는 선행 커밋(행동 불변) → ② 테스트 추가, 빨강 박제 → ③ 수정 커밋, 초록 순서로 한다.
- 「HEAD 컴파일」 열이 O인 테스트만 HEAD 그대로 빨강 박제가 가능하다.

| # | 측정 | 합격 | HEAD 컴파일 | HEAD 기대 |
|---|---|---|---|---|
| T-A1 | `WindowCrashOverlay` 아래 모든 LineRenderer `startColor.a`: 마지막 유지 프레임 vs 페이드 첫 프레임(부서짐·취소 각각) | 첫 ≤ 직전 + 1e-6 | O | **빨강** |
| T-A2 | 페이드 시작부터 소멸까지 매 프레임(단독 부서짐, 단독 취소) | a(n+1) ≤ a(n) + 1e-6 | O | **초록** — 튐은 T-A1 경계에만 있고 페이드 안에서는 단조 감소 |
| T-A2b | **주입 경로**: `Completed` 발행 후 부서짐 도중 `Cancelled` 발행(렌더러 계약 수준, 현 디렉터는 안 보냄) → 매 프레임. **주입 조건을 테스트 안에서 단언한다**: HEAD 컴파일 형태는 「주입 직전 부서짐 경과 프레임 수 ≥ 4」(테스트가 `Completed` 뒤 프레임을 센다. 4는 HEAD private 상수에서 유도한 값이라 상수 공개 뒤에는 그 상수로 대체), 상수 공개 뒤 형태는 「주입 직전 균열선 알파 < 1 − dt / CancelFadeSeconds」. 유도: 취소 첫 프레임 알파 1 − dt/0.12가 부서짐 n프레임 알파 1 − n·dt/0.42보다 크려면 n > 3.5(= 0.42 / 0.12). 0–3프레임에 주입하거나 벽시계 대기만 하면(30fps 0.100초 = 3프레임) HEAD에서도 초록 | 위와 같음 | O(프레임 수 형태) | **빨강**(주입 조건 충족 시, 알파 1에서 재시작) |
| T-A3 | 그림자선 ÷ 균열선 알파를 유지 프레임에서 잰 비율과 비교(0.7을 적지 않음) | 균열 a > 0.01 동안 차이 1e-3 이하 | O | **빨강**(페이드 중 1.0) |
| T-A4 | `Completed` → 컨테이너 소멸 벽시계 | HEAD 형태: 기존 테스트 `CompletedEventShattersAndRemovesEveryObject`의 여유 상한 관례만 가능. 정밀 상한(`ShatterSeconds` + 그 프레임 dt)은 **상수 공개 선행 커밋 뒤** — `Interaction/WindowCrashRenderer.cs:61` 앵커 `private const float ShatterSeconds = 0.42f;` 는 HEAD에서 private | 여유 상한 O / 정밀 X | 초록 |
| T-M1 | 임팩트 프레임(박자 진단 창구 = Impact) 벽시계 vs `Started` 수신 벽시계 | 0 ≤ Δ ≤ 그 프레임 dt, 프레임차 0 또는 1 | **X — 창구 선행** | 선행 커밋에서 빨강 확인 |
| T-M2 | A1 가드 참 + 명령 발동 → 1.0초 관측 | `WindowCrash` 프레임 0, `Started` 0 | O(A1 착지 후) | A1 라운드 소관 |
| T-M3 | windup 중(경과 0.15초) 가드 참 → 1.5초 관측 | `Started` 0, 진단 단계 Strike 도달 0회 | **X — 창구 선행** | 선행 커밋에서 빨강 확인 |
| T-M4 | 정지 램프 종료 후부터 상태 종료까지 두 발끝 월드 x·높이 | x 변화 0.02H 이하, 높이차 C-7 허용치 이하 | O(API 있음) | HEAD 초록은 설계 미착지라 무의미 |
| T-M5 | 균열 원점 vs 해머 머리 스냅숏 | 0.10H 이하 또는 사각형 경계 클램프 점과 일치 | X — 스냅숏 창구 선행 | — |
| T-M6 | (EditMode) 박자표 순수 함수의 모든 키 쌍 관절차 | 170° 이하 | X — 박자표 선행 | — |
| T-M7 | 크랙 상태 체류 벽시계 | 박자 합(공개 함수) ± 종료 프레임 dt | **X — 창구 선행** | 선행 커밋에서 빨강(0.4) 확인 |
| T-G1 | 첫 획 `positionCount > 0` 벽시계 | Enter + 분사 시작(박자 함수) 이상 | **X — 창구 선행** | 선행 커밋에서 빨강 확인 |
| T-G2 | 분사 시작 +0.10초 이후, 어깨→손 방향 vs 어깨→획 끝점 방향 각 | 12° 이하 | X — 끝점 창구 선행 | — |
| T-G3 | 그라피티 상태 동안 매 프레임 확정 지속시간 | 최대 − 최소 = 0 | **X — 읽기 창구 선행**(`TimedSpectacleState.cs` 한 파일) | 선행 커밋에서 빨강 확인 |
| T-F1 | 방향 반전 프레임 벽시계 | Enter + `horizontalDriftBrakeSeconds`(애셋 참조) 이상 | O | HEAD는 반전 자체가 없어 판정 불가 |

**변이 대조**
1. 디렉터가 `Started`를 Enter에서 발행하도록 되돌리면 → T-M1 · T-G1 빨강.
2. 캡처 곱셈을 절대 대입으로 되돌리면 → **T-A1 · T-A2b · T-A3** 빨강(T-A2는 초록 유지 — 대조로 쓰지 않는다).
3. 접촉 전 재판정을 삭제하면 → T-M3 빨강.
4. 1회 추첨을 매 Tick 추첨으로 되돌리면 → T-G3 빨강.

---

## 9. 인계 — 파일과 N-8 결속

N-8 목록은 `docs/verify/DISPLAY_CHANGE_PATH_FILES.md` 목록 블록 정본을 따른다.
- **D 등급**: `Core/StickConfig.cs`, `Data/DefaultStickConfig.asset`
- **B 등급**: `Core/StickmanAgent.cs`, `Core/SpectacleEventLock.cs`, `Interaction/WindowTheftDirector.cs` 등
- 이 설계의 나머지 후보 파일은 전부 목록 밖이다.
- **구현이 D·B 파일에 닿으면 E-3 증거 빌드 결속이 깨진다.**

| # | 항목 | 건드리는 파일 | N-8 | 시기(리더 판정) |
|---|---|---|---|---|
| ① | 크랙 알파 캡처 곱셈 + 섬광 + (선택) `ShatterSeconds` 공개 | `Interaction/WindowCrashRenderer.cs` + 새 테스트 | 목록 밖 | **1.0 권고 작은 라운드** — 필드·애셋 0 |
| ② | 그라피티 1회 추첨 + 확정 지속시간 읽기 창구 | `States/TimedSpectacleState.cs` + 새 테스트 | 목록 밖(`StickmanAgent.cs` 선택자 0줄) | **1.0 권고 작은 라운드** — 필드·애셋 0 |
| ③ | 크랙 박자·자세·임팩트 래치·접점 규칙·접촉 전 재판정 | `Interaction/WindowCrashDirector.cs`(A1 라운드와 같은 파일) · `Interaction/WindowCrashRenderer.cs` · `States/StickmanPoseAnimator.cs` · `States/StickmanBlackboard.cs` · `States/TimedSpectacleState.cs` — **박자를 설정 필드로 두면 D 등급 2파일, 지속시간 선택자를 바꾸면 B 등급 파일**(`StickmanAgent.cs`) | D·B 닿을 수 있음 | 1.0 이후 연출 라운드. 필드가 필요하면 E-3 뒤 |
| ④ | 그라피티 분사 박자·조준·분사 시작 `Started` | `Interaction/GraffitiDirector.cs` · `Interaction/GraffitiRenderer.cs` · `States/StickmanBlackboard.cs` · `States/StickmanPoseAnimator.cs` | 목록 밖(필드를 두면 D) | 1.0 이후 |
| ⑤ | 방향 소유권 멤버십 | `States/StickmanBlackboard.cs` + `HorizontalMotionOwnershipContractTests.cs` | 목록 밖 | ③④와 함께 |
| ⑥ | `GrowSeconds`·`DrawSeconds` 공유화 | 두 렌더러 | 목록 밖 | ③④와 함께 |
| ⑦ | 창 도둑 제목선 알파(범위 밖 후보) | `Interaction/WindowTheftRenderer.cs`만 — 짝 `WindowTheftDirector.cs`(B)는 건드리지 않는다 | 목록 밖 | 리더 배분 |

| 받는 쪽 | 항목 |
|---|---|
| design-art | 섬광 분리 여부(§7-4), 스윙 잔상(§3-3), 균열 원점 가장자리 허용(§3-4), 분무선(§4-3), 블랙홀·복제 아이콘 |
| design-equipment | 해머 자루 0.26–0.36H(권고 0.30H, 민감도는 검산 [2-1]), 두 손 간격 0.08H, 스프레이 캔, 배포 배율 가시성 |
| design-narrative | 접점 규칙 (다) 사유 문구, 대사는 임팩트·분사 확정 후(C-9) |
| game-architect | `TimedSpectacleState` 순수 타이머 계약 대 박자표·경과 초 창구, 전용 상태냐 라우팅 분기냐, 임팩트 이벤트 소유자, 그라피티 경로 스냅숏 위치, 청소부 접근 보행 |
| test-engineer / qa-regression | §8 표 순서 규칙, ②의 상태 도중 설정 변경 비반영 점검 |

---

## 10. 미확인
- 실기 캡처 0. 곡선 팔다리에서 무릎 100 스쿼트 · 엉덩이 +62.5의 크리즈 규칙 B 미검산.
- 뒷다리 −4.63 출발 웅크림 수치, 뒷손 허리 IK 목표점.
- 청소부·블랙홀 각도의 기하·접지 검산.
- 60Hz 디스플레이 macOS 실측 fps.
- 창 도둑 튐 크기, 하드웨어 반응 깜빡이 선, 활쏘기 렌더러 기준 알파.
- 브리프 「Archery 8건」과 이 문서 5건의 셈 범위 차이.

## 11. 관련 기존 문서 (수정하지 않음)
- `docs/UX_FLOW.md` 27-2 · 27-3 · 27-4 · 27-5
- `docs/MOTION_SPEC.md`
- `docs/UX_MOTION_DANCE.md` 4절
- `design/motion/2026-09-02_신규행동_모션사양.md`
- `design/motion/2026-09-02_R4_출발무게_활쏘기접근_대사동기.md`
- `design/motion/2026-09-06_집중세션_팔짱기하_검산.py` (리그 상수 출처)
- `design/character/BODY_ENVELOPE_FOR_EQUIPMENT.md`
- `docs/GAME_ARCHITECTURE_REVIEW.md`
- `docs/verify/DISPLAY_CHANGE_PATH_FILES.md` (N-8 목록)

## 12. 검산 기록 (결과 수치 옮김)
검산 스크립트는 세션 작업 공간에만 있다(저장소 반입은 리더 판단). 최종 실행 rc = 0, 실패 0. 두 번째 스크립트(코드 공유 없음, 월드 유닛 직접 계산)로 발 높이차·프레임 알파·(나) 거리를 독립 재계산했고 일치했다.

**교정**(하나라도 깨지면 이후 숫자 폐기)
- 전신 H 2.2746944 = `Core/StickConfig.cs:2319` 앵커 `public const float BaselineCharacterTotalHeight = 2.2746944f;`
- 팔 길이 0.32971H(인용 0.3297)
- 배율 1.0의 H 80.18pt(UX 80.2)
- 스무딩 35의 95% 도달 0.0856초(툴팁 0.086)
- 발끝 x 역산기 항등(16.00000000)

**출력 발췌**(라벨은 스크립트 출력 절)
- [1] base 35: 상완 k 19.25, t90 0.120초, 램프지연 51.9ms / 전완 k 14.44, t90 0.159초, 69.3ms
- [2-1] 자루 0.30H: windup 머리 (−0.530, +0.884)H / impact_mid 머리 (+0.611, +0.495)H, 뒷손 IK 39.6 · 79.0 / 임팩트 머리 속도 41.1 H/s, 30fps 마지막 프레임 1.01H / 자루 0.26H 37.9 H/s · 0.36H 46.0 H/s
- [2-1b] impact_low: 무릎 100, 기울임 +12, 상완 22, 굽힘 4, 손목 −18 → 머리 (+0.252, +0.0195)H, 강하 0.1502H
- [2-2] windup→impact_mid 상완 −130 / windup_low→impact_low 상완 −168 / 자루 절대 실제 −235 대 최단 +125
- [2-3] 앞 16 / 뒤 −18 / 무릎 12의 높이차 0.02827H(유닛 0.06429 ÷ 2.2747). 뒤 −4.63이면 0
- [3] 부서짐 60fps 첫 프레임 0.960(균열 ×2.13, 그림자 ×3.05). 연속 0.231 / 0.288초. 프레임 기준은 §7-2
- [4] 그라피티 종료 평균 30fps 3.313 / 60fps 3.223 / 8000fps 3.020초(의도 4.000)
- [5] 추종 폭 18.2–27.0°, 분사 시작 0.64초, 그리기 끝 1.99초
- [6] 청소부 합 2.50 / 블랙홀 합 2.50
- [7] 창 높이 300 / 600 / 900pt에서 (나) 접점과 렌더러 타격점 거리 1.017 / 2.015 / 3.013H(배포 0.75)

## 13. 플랫폼 영향
- **Windows 영향**: 없음(문서만). 대상 코드는 플랫폼 공통이고 Windows 전용 분기가 없다. 아이콘 조회 스텁은 청소부·블랙홀 착수 조건으로만 인용했다. 박자는 30fps 최악으로 검산했다.
- **macOS 영향**: 없음(문서만). 같은 공통 코드다. 아이콘 조회 미구현이 같은 착수 조건이다.

---

## 초판(미커밋) 대비 정정 기록
TEAM.md §5에 따라 미커밋 초판의 문장은 본문에 취소선으로 남기지 않고 여기에 적는다. 검증: verify-change 1차 조건부 반려(초판 sha `b8f7e8efd4ef8015`, #1–15) · 2차 조건부 통과(sha `da2d10bdeb618d9e`, 경미 #16–20).

| # | 초판 요지 | 정정 | 근거 |
|---|---|---|---|
| 1 | 오버레이 `Started` 선발행을 「원칙 1 위반·복구」로 서술(§0 · §1-2) | 「같은 프레임·같은 동기 호출이라 HEAD에서 역전은 안 보인다. 박자를 도입할 때 임팩트 프레임 발행이 필요한 **시각 인과** 문제이며 원칙 1 텍스트 위반이 아니다. 1.0 이후 연출 라운드」 | 리더 판정(대사는 `Started`에서 파생되지 않음, verify-change 소스·IL 전수) |
| 2 | WindowTheftRenderer.cs 473–474줄을 캡처 곱셈의 「저장소 안 선례」로 제시 | 그 줄은 정적 상수 곱 = 기각 2 형태. **페이드 시작 캡처 곱셈 선례 없음**. 가까운 형태로 RunawayRenderer.cs 659줄(생성 시 저장한 요소별 기준 알파 곱)을 새로 찾아 병기 | 틀린 점 1 + 저장소 재검색(양성 대조 포함) |
| 3 | 두 발 높이차 「1.70pt」를 배율 표기 없이 적음. C-7 허용치 기준 배율 없음 | **0.0283H = 배율 1.0에서 2.27pt / 배포 0.75에서 1.70pt**. 허용치는 배율 1.0 기준 0.5pt(0.0062H). ★ **검증 지적의 수치(0.0212H = 배율 1.0에서 1.70pt, 0.75에서 1.28pt)는 독립 재계산으로 재현되지 않았다.** 유닛 직접 계산: 앞다리 0.5cos16° + 0.45cos4° = 0.92953, 뒷다리 0.5cos18° + 0.45cos30° = 0.86524, 차 0.06429유닛 ÷ H 2.2747 = 0.02827H. 0.0212는 0.02827 × 0.75와 같아 **배율이 두 번 곱해진 것으로 추정**한다. → **리더 판정(2026-09-15): 0.02827H 확정 — verify-change 1차 0.0212H는 배율 이중 적용**(프리팹 관절 길이에 이미 ×0.75가 들어 있다, SceneBootstrapper.cs 878–879줄 · 캡슐 1.7060208 = 2.2747 × 0.75로 교정) | 틀린 점 2 + 스크립트 2개 일치 |
| 4 | §8: T-A2를 HEAD 빨강으로 표기, 변이 ② 「T-A1부터 A3까지」, 창구 없는 테스트의 컴파일 여부 미표시, `ShatterSeconds`를 공개 참조로 전제 | T-A2는 HEAD 초록으로 정정. 주입 경로 T-A2b 추가. 변이 ②는 T-A1 · T-A2b · T-A3. 「HEAD 컴파일」 열과 선행 커밋 순서 규칙 추가. T-A4는 HEAD 여유 상한 / 정밀 상한은 공개 선행 커밋 뒤 | 틀린 점 3 |
| 5 | 단일 물결표 범위 표기가 GFM 취소선으로 렌더됨(자루 범위, 추종 폭 줄 — 굵게도 안 닫힘) | 문서 전체에서 범위는 EN DASH, 단일 물결표 0 | 틀린 점 4 |
| 6 | 「해머 코드 6곳」 | 7줄 · 4파일(HEAD 사본 grep 계수, #19 참조) | 경미 |
| 7 | 선택자를 `Random.Range(3, 5)`로 표기 | `Random.Range(cfg.graffitiHoldDurationMin, cfg.graffitiHoldDurationMax)`(float 3f / 5f) | 경미 |
| 8 | 3초 수명 타이머 인용 189–193줄이 렌더러 문맥에 놓임 | WindowCrashDirector.cs 189–193줄로 명시(본문 §1-2는 앵커 병기) | 경미 |
| 9 | ApplyFocusPose 1084줄의 파일 미표기 / 「StickmanBlackboard.cs 220줄은 활쏘기 필드」 | StickmanPoseAnimator.cs 1084줄 / StickmanBlackboard.cs 220줄은 빈 줄, 활쏘기 필드는 211–219줄(본문 앵커 병기) | 경미 |
| 10 | 교차 참조 「§12-3」 「§12-2 표」 | 스크립트 출력 절 라벨 [2-3] · [3] · [2-1]로 교체(가리키는 표가 없던 참조 제거) | 경미 |
| 11 | 0.231초만 적음 | 연속값 0.231초와 프레임 기준 60fps 13프레임 0.217초 · 30fps 6프레임 0.200초 병기(그림자·취소 포함, §7-2) | 경미 |
| 12 | 창 높이별 거리 1.00 / 2.00 / 2.99H | (나) 경우 머리 +0.0195H를 넣어 1.02 / 2.02 / 3.01H | 경미 |
| 13 | 줄 인용에 앵커 없음(A1 작업 트리 +25줄로 밀림 위험) | 모든 코드·애셋 줄 인용에 HEAD 1회 고정 문자열 앵커 — 검사기로 앵커 유일성과 줄 범위를 HEAD에서 대조 | 경미 |
| 14 | 작업 공간 경로 표기로 근거 제시 | 경로 표기 제거, 결과 수치를 §12로 옮김 | 경미 |
| 15 | N-8 결속 미언급 | §9에 항목별 건드리는 파일 · N-8 등급 · 시기 표 추가. ①② 1.0 권고 작은 라운드는 필드·애셋 0 형태로 한정 | 경미(★) + 리더 판정 |
| 16 | §1-2 판정 문장이 「」 괄호를 감싼 굵게라 GFM에서 닫히지 않음(닫는 기호 앞이 문장부호, 뒤가 글자) | 굵게를 괄호 안쪽으로 옮김 | 2차 경미 1 · 검사기 flanking 규칙 추가로 재확인 |
| 17 | T-A2b를 주입 시점 조건 없이 「HEAD 빨강」으로 표기 | 주입 직전 부서짐 경과 ≥ 4프레임(또는 알파 < 1 − dt / CancelFadeSeconds)을 테스트 안에서 단언. 0–3프레임·벽시계 대기만으로는 초록 | 2차 경미 2 |
| 18 | 1회 추첨 부작용 점검에서 「다른 7개 등록(투두·집중·SULKY)」 | 다른 9개(청소부·블랙홀·크랙 3 · 투두 1 · 집중 4 · SULKY 1), 전체 10 — HEAD 사본에서 등록 10건 재계수, 결론 불변 | 2차 경미 3 |
| 19 | 「해머」 계수 절차를 git grep으로 적음 | ls-tree 목록 → show HEAD 사본 → 파일 grep으로 재계수(7줄 · 4파일 동일) | 2차 경미 4 |
| 20 | #3의 끝이 「리더 판정 요청」 | 리더 판정으로 종결: 0.02827H 확정, 1차 0.0212H는 배율 이중 적용 | 2차 경미 5 |
