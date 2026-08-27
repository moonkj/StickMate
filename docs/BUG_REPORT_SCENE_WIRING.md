**Major 3건 발견 — Coder로 반려 필요**

# StickMate — 씬/프리팹 배선 버그 리포트 (Debugger, Teammate2)
> 작성: Debugger · 작성일: 2026-08-27 · 대상: 커밋 `46beba4`("씬/프리팹 배선: 최소 동작 가능한 캐릭터+씬 구성, 실측 플레이테스트")
> 범위: `Assets/Editor/SceneBootstrapper.cs`(신규), `Assets/_Project/Data/DefaultStickConfig.asset`(신규), `Assets/_Project/Prefabs/Stickman.prefab`(신규, YAML 직접 검증), `Assets/_Project/Scenes/Main.unity`(신규, YAML 직접 검증), `Assets/_Project/Scripts/Tests/PlayMode/StickmanPlaytestSmokeTests.cs`(신규), 대조 참고: `States/RagdollRig.cs`/`RagdollState.cs`/`GetupState.cs`/`GroundSensor.cs`/`StickmanBlackboard.cs`, `Core/StickmanAgent.cs`/`RagdollLimbImpactRelay.cs`, `Core/StickConfig.cs`.
> 환경: Unity 6000.0.82f1 배치모드. 컴파일 독립 재검증(`Logs/debugger_compile.log`) — `error CS`/`warning CS` 매치 0건, exit code 0. EditMode 재실행(`Logs/debugger_editmode.xml`) — `total="13" passed="13" failed="0"`, 기존 기준선 유지. PlayMode 재실행(`Logs/debugger_playmode.xml`) — `total="1" passed="1" failed="0"`, 실제 로그 상 `Debug.LogError`/`LogException` 0건(매치된 "Error/Exception" 문자열은 전부 라이선싱 핸들셰이크·usbmuxd·NUnit 내부 메서드명(`ExecuteEnumerableAndRecordExceptions`) 등 테스트와 무관한 벤치 노이즈임을 직접 확인). 다만 이번 재실행은 RNG 시드가 달라(`System.Guid.NewGuid()` 기반) Coder의 원 로그와 다른 배회 경로를 보였다 — Idle 정지 후 Walk로 왼쪽 이동, `x=-6.338`에서 반전해 복귀하는 패턴(정착 Y범위 0.0200, X범위 6.3390, 둘 다 기준 통과). **재현성/견고성 자체는 확인됨.**
> 추가 실측: `SceneBootstrapper.BuildAll()`을 `-executeMethod`로 재실행한 뒤 `git diff`로 재생성 결과를 직접 비교(테스트 후 `git checkout`으로 원상복구, 저장소에 잔여 변경 없음) — 아래 Major 3 근거.

## 결론 요약

**Blocker 0건, Major 3건, Minor 3건 — Coder로 반려 필요.**

- 중점 점검 4(`_config` 참조)는 완전히 정상 — `Stickman.prefab:480`의 `_config: {fileID: 11400000, guid: a80c0efcbfabb4baaad9177df5ad9015, type: 2}`가 `DefaultStickConfig.asset.meta`의 guid와 정확히 일치. 조용한 폴백 실패 없음.
- 중점 점검 1의 핵심(HingeJoint2D의 `connectedBody`/anchor)도 정상 — 4개 팔다리 전부 `m_ConnectedRigidBody`가 루트 Rigidbody2D(fileID 5634439439499387665) 하나를 정확히 가리키고, anchor/connectedAnchor 좌표를 직접 계산해보면 초기 구속 오차가 수학적으로 0임을 확인(예: LeftLeg `anchor={0,0.3}` + limb world y(0.3) = 0.6 = `connectedAnchor={-0.12,0.6}`의 y와 정확히 일치).
- 그러나 같은 중점 점검 1이 요구한 "부작용" 확인에서 **신규 Major(BUG-SW-M1)**를 발견 — 팔다리에 Collider2D를 넣지 않은 결정이 자체충돌 떨림은 막지만, RAGDOLL 상태가 물리적으로 영원히 안착할 수 없는 구조적 문제와 맞물려 있다.
- 중점 점검 2(`groundSnapTolerance`/`orthographicSize` 튜닝 부작용)에서 **신규 Major(BUG-SW-M2)** — 두 튜닝이 독립적으로 정당화되었지만 실제로는 곱연산으로 상호작용해, 문서가 계산한 값과 실제 결과값이 13배 이상 차이난다.
- 중점 점검 5(재실행 안전성)에서 **신규 Major(BUG-SW-M3)** — 실측(`BuildAll` 재실행 후 `git diff`)으로 재생성이 전혀 멱등적이지 않음을 확인. 부분 재실행 시 씬이 조용히 깨질 수 있다.
- 중점 점검 3(PlayMode 스모크 테스트 신뢰성)은 "가짜 테스트 아님"은 확인되었으나, 실제 커버리지가 Tasklist.md 표현보다 좁다는 것을 Minor로 기록(BUG-SW-M1과 연결됨).

---

## 권고 순서

1. **BUG-SW-M1 먼저 판단** — 지금 당장 코드를 고치라는 뜻은 아니다(Ragdoll 실제 트리거는 Phase 3 스코프). 다만 "RagdollRig 계약 충족"이라는 이번 라운드의 핵심 주장에 대해, Architect/Coder가 "바닥 Collider2D를 어떻게든 추가할지, 아니면 RAGDOLL 이탈 조건 자체를 재설계할지"를 Phase 3 착수 전에 반드시 결정해야 한다 — 이미 구현되어 있는 `DragThrowState`/`RivalStickmanAgent` 전투 로직이 전부 `ReportExternalImpact()`를 통해 RAGDOLL을 트리거하도록 짜여 있어, 이 구멍을 모른 채 Phase 3을 진행하면 처음 실제로 맞는 순간 캐릭터가 화면 밖으로 영원히 낙하한다.
2. **BUG-SW-M2** — `groundSnapTolerance`를 다시 튜닝할 필요는 없다(현재 값으로 실측 통과). 다만 Tasklist.md에 "13배 차이" 사실을 명시하고, `orthographicSize`를 나중에 실제 아트/렌더링 작업에서 되돌리거나 바꿀 때 `groundSnapTolerance`(및 7개 OS-px 필드)를 반드시 함께 재검토하라는 경고를 `SceneBootstrapper.cs` 클래스 문서에 추가할 것을 권고.
3. **BUG-SW-M3** — `SceneBootstrapper.cs`에 최소한 클래스 문서 수준의 경고("부분 재실행 금지, 항상 BuildAll 전체 실행" + "재실행 시 Main.unity의 수동 편집 내용은 전부 소실됨")를 추가. 근본 해결(예: 씬을 완전히 새로 만들지 않고 기존 GameObject를 찾아 갱신하는 방식으로 전환)은 이번 반려 사이클에서 강제하지 않되, 최소 경고 문구는 반려 조건으로 요구.
4. Minor 3건은 급하지 않음.

---

## 이번 라운드 중점 점검 항목 결론

| # | 점검 항목 | 결론 |
|---|---|---|
| 1 | HingeJoint2D 배선 타당성(connectedBody/anchor) + Collider2D 미부착의 부작용 | **연결 자체는 정확, 그러나 부작용 신규 발견(BUG-SW-M1).** `connectedBody`/anchor 수치는 정상(위 결론요약 참고). 하지만 팔다리 Collider2D 부재는 두 가지 결과를 낳는다: (a) `Core/RagdollLimbImpactRelay.cs`(Phase 2에서 미리 작성된, 사지 피격 중계용 컴포넌트)는 grep 결과 `Stickman.prefab` 어디에도 부착되어 있지 않고, 설령 나중에 부착해도 `OnCollisionEnter2D`가 발동하려면 그 GameObject 계층에 최소 하나의 Collider2D가 있어야 하는데 팔다리엔 전혀 없어 **영구적으로 발동 불가능한 죽은 코드**가 된다. (b) 더 심각하게, 씬 전체에 "바닥"에 해당하는 물리 Collider2D가 단 하나도 없다(발판은 전부 `GroundSensor`의 좌표 비교로만 판정되는 가상 개념, `SnapToGround`가 위치를 직접 대입할 뿐 물리 충돌이 아님 — 이는 Phase 2 로그에 이미 기록된 기존 한계). `RagdollState.Tick()`(`States/RagdollState.cs:74-96`)의 유일한 Getup 전이 조건은 `RagdollRig.GetMaxSpeed() <= ragdollSettleSpeedThreshold`인데, 모든 Rigidbody2D의 `m_LinearDamping: 0`이고 바닥 콜라이더가 없으므로 중력(`gravityScale=3`)이 감쇠 없이 계속 가속시켜 이 조건은 **수학적으로 영원히 만족되지 않는다** — RAGDOLL에 한 번 진입하면 캐릭터는 화면 밖으로 무한정 낙하하며 절대 GETUP으로 복귀하지 못한다. 이번 라운드의 15초 플레이테스트는 충격을 전혀 유발하지 않아(장애물 없음, 충돌 소스 없음) 이 경로를 전혀 건드리지 않았으므로 겉으로는 드러나지 않았다. Tasklist.md "남은 것" 항목이 "아직 검증 안 함"이라고 정직하게 적어두었지만, 실제로는 "검증하면 반드시 깨짐"에 가까워 표현보다 심각하다고 판단해 Major로 격상. |
| 2 | `groundSnapTolerance`(6→20px)/`orthographicSize`(5→20) 튜닝의 교차 영향 | **신규 Major(BUG-SW-M2) — 두 튜닝이 곱연산으로 상호작용, 문서 계산과 13배 이상 괴리.** `groundSnapTolerance`는 프로젝트 전체에서 `GroundSensor.cs:77` 단 한 곳에서만 소비되며, 이 값은 OS-픽셀 단위로 `footOs.y`(=`ScreenCoordinateConverter.WorldToOsScreen` 결과)와 비교된다. 이 변환의 px/world-unit 비율은 `Screen.height / (2 * orthographicSize)`로 직교 카메라 크기에 반비례한다. `SceneBootstrapper.CreateOrLoadConfig()`의 주석은 "20px로 넉넉히 키워 약 0.3~0.4유닛 밴드 확보"라고 계산했는데, 이는 `orthographicSize=5`(px/unit=48, `Screen=640x480` 기준) 시점의 계산이다. 그런데 바로 다음 단계(`BuildMainScene()`)에서 화면 이탈 문제를 별도로 해결하려고 `orthographicSize`를 5→20으로 **독립적으로** 올렸고, 이로 인해 px/unit이 48→12로 떨어져 최종 결합 결과는 `20px / 12(px/unit) ≈ 1.667 world-unit` 밴드다 — 문서가 주장한 0.3~0.4유닛의 **약 4~5배**, 원래 버그를 유발했던 최초 조합(6px, orthoSize 5, 0.125유닛 밴드) 대비로는 **약 13.3배**다. 실측 플레이테스트에서는 이 폭이 넓어도 스냅이 한 번에 정확한 Y로 강제 대입되므로 떨림 등 가시적 문제가 없었지만(우연히 무해), 이 계산이 다른 곳에서 재사용되지 않는다는 보장이 없고 — 실제로 `StickConfig`에는 `wanderCursorReactionRadiusPx`/`rodeoStillRadiusPx`/`rodeoReachDistancePx`/`graffitiMinRadiusPx`/`graffitiMaxRadiusPx`/`graffitiRegionSizePx`/`runawayHideSpotMarginPx` 등 OS-px 단위 필드 7개가 더 있고, 이들 전부 이번 `orthographicSize` 4배 확대로 실제 월드 공간상 유효 반경이 조용히 4배 넓어졌다(예: `rodeoReachDistancePx=400`이 이제 이전 대비 4배 넓은 월드 영역에 대응). 이 필드들은 전부 Phase 3~5 기능이라 이번 스모크 테스트로는 전혀 검증되지 않지만, Phase 3 착수 시 "왜 로데오 커서 도달 반경이 설계 의도보다 훨씬 넓게 느껴지지"라는 형태로 재발할 수 있는 잠복 지뢰다. 부가로: 향후 실제 렌더링 작업에서 `orthographicSize`가 게임플레이용 값(예: 5~8 근방)으로 되돌려지면, `groundSnapTolerance=20`만 남아있는 채로 px/unit이 다시 커져 밴드가 다시 좁아지고 **원래의 접지 터널링 버그가 아무 코드 변경 없이 조용히 재발**할 수 있다(이번 수정의 실효는 대부분 `orthographicSize` 쪽에서 나왔지, `groundSnapTolerance` 자체의 6→20 상승분 단독으로는 원래 시나리오의 터널링을 확실히 막기에 여유가 크지 않다: orthoSize=5 유지 시 20px/48=0.417유닛뿐). |
| 3 | PlayMode 스모크 테스트의 신뢰성 | **가짜 테스트 아님 — 실제 `Assert.Less`/`Assert.Greater`로 측정값을 검증하고 `Debug.LogError`/`LogException` 발생 시 자동 실패(Unity Test Framework 기본 동작)함을 코드와 재실행 양쪽으로 확인. 다만 커버리지는 Tasklist.md의 표현("자율배회AI가 실제로 동작함을 실증")보다 좁다(Minor로 기록, 아래 참고).** Coder의 원본 로그와 이번 독립 재실행 로그를 비교하면 RNG 시드가 달라 걸음 패턴 자체는 달라졌으나(정지→한 방향 단조 이동 vs 정지→이동→반전→복귀), **두 실행 모두 Idle/Walk 두 상태만 관측되었고 Jump/ParkourClimb/Ragdoll/Getup/Attack은 단 한 번도 발생하지 않았다.** 구조적으로 이는 우연이 아니다 — (a) 씬에 발판이 오직 화면 전체 폭의 단일 평면 더미 발판 하나뿐이라 높이차가 없어 `TryFindClimbableWall`이 "벽"을 찾을 조건 자체가 성립 불가능(ParkourClimb 진입 불가능), (b) 씬에 캐릭터와 충돌할 다른 오브젝트가 전혀 없어 `ReportExternalImpact`가 호출될 경로가 없음(Ragdoll/Getup 진입 불가능), (c) Jump는 `wanderPostIdleJumpChance=0.05`/`wanderEdgeJumpAttemptChance=0.10`의 저확률 분기라 15초 1회 실행으로는 통계적으로 보장되지 않음(실제로 두 번의 실행 모두 미관측). 즉 이 테스트는 "Idle/Walk 이동 + 접지 스냅"이라는 좁은 회귀만 안정적으로 검증하며, 이번 라운드의 프리팹이 실제로 새로 추가한 헤드라인 구조물(4개 HingeJoint2D 리그)의 물리적 타당성은 이 테스트로 전혀 검증되지 않는다(BUG-SW-M1과 직결). |
| 4 | `StickmanAgent._config`가 `DefaultStickConfig.asset`을 실제로 참조하는가 | **정상 — 조용한 실패 없음.** `Stickman.prefab:480`의 `_config: {fileID: 11400000, guid: a80c0efcbfabb4baaad9177df5ad9015, type: 2}`와 `DefaultStickConfig.asset.meta:2`의 `guid: a80c0efcbfabb4baaad9177df5ad9015`가 정확히 일치. `fileID: 11400000`은 ScriptableObject 메인 에셋의 표준 fileID로 정상 범주. 튜닝된 `groundSnapTolerance=20`이 실제로 런타임에 반영됨을 확인. |
| 5 | `SceneBootstrapper.cs` 재실행 안전성 | **신규 Major(BUG-SW-M3) — 실측 결과 전혀 멱등적이지 않음, 부분 재실행 시 씬이 조용히 깨질 위험.** `-executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll`을 실제로 재실행한 뒤 `git diff`로 직접 비교했다(재현 절차: `git status` 클린 확인 → 재실행 → `git diff --stat` 확인 → `git checkout --`로 원상복구, 저장소에 잔여 변경 없음). 결과: `DefaultStickConfig.asset`은 바이트 단위로 완전히 동일(md5 일치, 진짜 멱등적). 그러나 **`Stickman.prefab`과 `Main.unity`는 매번 완전히 다른 내용으로 재생성된다** — `git diff` 확인 결과 GameObject/컴포넌트의 논리적 내용(스케일/스프라이트/anchor 등)은 동일하지만, Unity가 부여하는 로컬 fileID(예: `--- !u!1 &3172068132853706066`)가 **재실행마다 완전히 무작위로 재할당**된다(`Stickman.prefab` 222줄, `Main.unity` 26줄 변경, 두 파일 모두 100% 재작성 수준). 이것이 실제로 위험한 이유: (a) `BuildStickmanPrefabMenuItem()`("Build Stickman Prefab" 메뉴)만 단독 재실행하면 프리팹은 새 fileID로 갱신되지만 `Main.unity`는 그대로 남는데, `Main.unity`의 `PrefabInstance.m_Modifications`(예: 캐릭터 낙하 시작 y좌표 `20.3` 오버라이드)는 **옛 프리팹의 fileID를 타겟팅**하고 있으므로, 재생성된 프리팹에서는 그 fileID가 더 이상 같은 대상을 가리키지 않아 오버라이드가 조용히 고아(orphan)가 되고 캐릭터가 프리팹의 기본 위치(사실상 원점 부근)로 스폰되는 등 예측 불가능한 결과를 낳을 수 있다. (b) `BuildMainScene()`은 항상 `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`로 **완전히 빈 씬에서부터 다시 생성**하므로(diff/patch가 아님), "BuildAll" 전체를 다시 실행하면 (a)의 고아 참조 문제는 피하지만 대신 그 사이 씬에 수동으로 추가된 모든 내용(배경 아트, 테스트용 보조 오브젝트, 카메라 설정 조정 등)이 **경고 없이 통째로 사라진다**. 세 빌드 함수의 재실행 정책이 서로 다르다는 점도 문제다 — 스프라이트(`GetOrCreateSprite`)는 존재하면 건드리지 않고, config는 존재해도 `groundSnapTolerance` 한 필드만 항상 강제 덮어쓰며, 프리팹/씬은 항상 전체를 파괴 후 재생성한다. 클래스 문서 상단이 "나중에 프리팹 리그를 조정하거나 씬을 재생성할 때 코드로 일관되게 재현 가능"이라고 재사용을 권장하고 있어 이 위험이 더 크다 — 정작 그렇게 재사용하면 위 두 문제 중 하나에 부딪힐 가능성이 높다. |

---

## Major

### BUG-SW-M1 — 팔다리 Collider2D 부재 + 바닥 Collider2D 전무로 RAGDOLL이 물리적으로 절대 안착(Getup)할 수 없음

- **파일**: `Assets/Editor/SceneBootstrapper.cs`(`CreateLimb`, 155-274행 — Collider2D 미부착 결정), `Assets/_Project/Scripts/States/RagdollState.cs:74-96`(Getup 전이 조건), `Assets/_Project/Scripts/States/RagdollRig.cs:52-62`(`GetMaxSpeed`), `Assets/_Project/Scripts/Core/RagdollLimbImpactRelay.cs`(부착 안 됨).
- **근거**:
  1. `Stickman.prefab`의 LeftLeg/RightLeg/LeftArm/RightArm GameObject는 전부 `Rigidbody2D`+`HingeJoint2D`만 가지고 Collider2D가 전혀 없음(YAML 컴포넌트 목록 직접 확인 — 각 GameObject당 컴포넌트 2개뿐).
  2. `grep -rn "RagdollLimbImpactRelay" Assets`로 확인한 결과 이 컴포넌트는 어떤 프리팹에도 부착되어 있지 않다(문서/주석에서만 언급됨). 설령 나중에 부착해도, `OnCollisionEnter2D`가 발동하려면 그 GameObject(또는 그 계층의 compound collider)에 Collider2D가 있어야 하는데 팔다리엔 없으므로 영구적으로 무동작이다.
  3. 씬 전체에 물리적 "바닥" 역할을 하는 Collider2D가 없다(발판은 순수 좌표 비교 + `Body.position` 강제 대입으로만 구현됨, `StickmanBlackboard.SnapToGround` 참고). 이는 RAGDOLL/GETUP이 처음 작성됐을 때부터의 기존 한계지만, 실제 프리팹으로 물리 시뮬레이션이 처음 돌아가는 이번 라운드에서야 "실제로 무엇과도 충돌할 수 없다"는 사실이 구체화됐다.
  4. `RagdollState.Tick()`의 유일한 탈출 조건은 전신 속도(`RagdollRig.GetMaxSpeed()`, 모든 Rigidbody2D 중 최댓값)가 `ragdollSettleSpeedThreshold`(0.3) 이하로 `ragdollSettleHoldDuration`(0.5초) 이상 유지되는 것이다. 모든 Rigidbody2D의 `m_LinearDamping: 0`(YAML 확인)이고 저항할 바닥이 없으므로, `gravityScale=3`이 매 프레임 속도를 계속 증가시킨다 — 이 조건은 수학적으로 충족 불가능하다.
- **왜 Major인가**: 이번 라운드가 명시적으로 내세운 "RagdollRig 계약 충족" 프리팹의 헤드라인 구조(4개 HingeJoint2D)가 구조적으로는 맞지만 기능적으로는 진입하면 절대 복귀 못 하는 상태다. 다만 (a) 이번 스모프 테스트/현재 스코프의 자율배회 루프는 RAGDOLL을 전혀 트리거하지 않으므로 지금 당장 눈에 보이는 회귀는 없고, (b) Coder가 Tasklist.md에 "미검증"으로 이미 부분적으로 disclosure했으므로 Blocker까지는 아니라고 판단했다. 그러나 Phase 3에 이미 구현되어 있는 `DragThrowState`/`RivalStickmanAgent`(둘 다 `ReportExternalImpact()`로 RAGDOLL을 강제 트리거하도록 설계됨)가 이 씬에 연결되는 순간 100% 재현되는 심각한 결함이라 지금 알려야 한다.
- **수정 제안(Architect/Coder 판단 필요, 코드 작업 지시 아님)**: (1) 화면 하단에 얇고 보이지 않는 물리 Collider2D "가상 바닥"을 추가해 RAGDOLL 낙하가 최소한 어딘가에서는 멈추게 하거나, (2) Getup 전이 조건에 "일정 시간(예: 2~3초) 경과 시 속도 무관 강제 Getup" 안전망을 추가하거나, (3) RAGDOLL 진입 시 일시적으로 `gravityScale`을 낮추는 등 물리적 우회. 어느 방향이든 Phase 3 착수 전 결정 필요.

### BUG-SW-M2 — `groundSnapTolerance`(6→20px)와 `orthographicSize`(5→20) 튜닝이 곱연산으로 상호작용, 실제 유효 밴드가 문서 계산(0.3~0.4유닛) 대비 약 4~13배 벗어남

- **파일**: `Assets/Editor/SceneBootstrapper.cs:63-71`(groundSnapTolerance 튜닝 근거 주석), `:181-187`(orthographicSize 튜닝 근거 주석), `Assets/_Project/Scripts/States/GroundSensor.cs:76-99`(실제 비교 로직), `Assets/_Project/Scripts/Platform/ScreenCoordinateConverter.cs`(px/world 변환 근거).
- **근거**: 위 중점 점검 2 참고. px/world-unit = `Screen.height / (2*orthographicSize)`. 실측 로그(`Logs/debugger_playmode.log`, `[PLAYTEST] DIAG Screen=640x480, cam.orthoSize=20`) 기준 px/unit=12, `groundSnapTolerance=20px` → 유효 밴드 ≈1.667 world-unit. `SceneBootstrapper.cs:69` 주석이 계산한 "0.3~0.4유닛"은 `orthographicSize=5`(당시 아직 미변경) 가정 하의 값으로, 이후 `orthographicSize`가 독립적으로 4배 커지면서 계산이 무효화됐다.
- **왜 Major인가**: 지금 당장 가시적 버그는 아니다(스냅은 순간 강제 대입이라 넓은 밴드가 떨림을 유발하지 않음). 그러나 (1) 문서에 남은 근거 계산이 실제 결과와 4~5배 어긋나 있어 향후 튜닝 시 잘못된 기준점이 되고, (2) 7개의 다른 OS-px 단위 `StickConfig` 필드(`wanderCursorReactionRadiusPx` 등, 이번 스코프에서 미검증)가 같은 카메라 크기에 종속되어 조용히 4배 재조정됐으며, (3) `orthographicSize`가 나중에 실제 게임플레이용 값으로 되돌아가면 `groundSnapTolerance` 단독으로는 원래의 접지 터널링 버그를 안전하게 막기에 여유가 부족하다(orthoSize=5 유지 시 0.417유닛뿐, 최초 반려 유발 시나리오인 1유닛 낙하 한 프레임 통과 케이스를 항상 막는다는 보장이 약함).
- **수정 제안**: 코드 변경 불필요. `SceneBootstrapper.cs` 클래스 문서 및 Tasklist.md에 "orthographicSize 변경 시 groundSnapTolerance 및 7개 OS-px 필드 재검토 필수"라는 경고를 명시할 것을 권고. 근본적으로는 `groundSnapTolerance`를 OS-px 대신 world-unit(또는 캐릭터 신장 비례) 단위로 바꿔 카메라 크기와 독립시키는 것이 장기적으로 더 견고하나, 이는 `GroundSensor`/`ScreenCoordinateConverter` 설계를 건드리는 더 큰 변경이라 이번 반려 사이클 범위로 강제하지 않는다.

### BUG-SW-M3 — `SceneBootstrapper` 재생성이 멱등적이지 않음(fileID 무작위 재할당) — 부분 재실행 시 씬 오버라이드 고아화, 전체 재실행 시 씬 수동 편집 소실

- **파일**: `Assets/Editor/SceneBootstrapper.cs:102-168`(`BuildStickmanPrefab`), `:175-214`(`BuildMainScene`).
- **근거**: 위 중점 점검 5 참고 — `-executeMethod BuildAll` 재실행 후 `git diff`로 실측(`Stickman.prefab` 222줄/`Main.unity` 26줄 변경, `DefaultStickConfig.asset`은 완전 동일). 각 GameObject/컴포넌트의 `--- !u!N &<fileID>` 헤더가 재실행마다 다른 무작위 값으로 바뀜을 diff로 직접 확인(예: `Visual` 자식의 fileID가 `3172068132853706066` → `943890134920210563`로 변경, 내용은 동일).
- **왜 Major인가**: 클래스 문서 상단이 "코드로 일관되게 재현 가능"이라며 재사용을 권장하고 메뉴 아이템(`StickMate/Build Stickman Prefab` 등)으로 부분 실행을 허용하고 있는데, 실제로는 (1) 프리팹만 재생성하면 `Main.unity`의 `PrefabInstance.m_Modifications`(낙하 시작 y=20.3 등 스폰 위치 오버라이드 포함)가 옛 fileID를 가리킨 채 남아 조용히 무효화될 수 있고, (2) 전체(`BuildAll`)를 재실행하면 `EditorSceneManager.NewScene(EmptyScene, Single)`로 씬을 완전히 새로 만들기 때문에 그 사이 `Main.unity`에 수동으로 추가된 어떤 내용도(배경 아트, 보조 테스트 오브젝트 등) 경고 없이 전부 사라진다. 어느 경로로 재실행해도 팀원이 예상하지 못한 방식으로 데이터가 유실될 수 있다.
- **수정 제안**: (1) 최소 조치 — 클래스 문서 상단에 "부분 재실행(`Build Stickman Prefab`만) 금지, 항상 `BuildAll` 전체 실행" + "재실행 시 `Main.unity`의 수동 편집 내용은 전부 소실됨(백업 필요)" 경고 추가. (2) 근본 조치(선택) — `BuildMainScene()`이 씬을 통째로 새로 만드는 대신 기존 씬을 열어 기존 Stickman 인스턴스만 찾아 교체하는 방식으로 전환하면 수동 편집 소실 문제를 줄일 수 있으나, 이는 설계 변경이라 이번 사이클에서 강제하지 않음. 최소한 (1)은 이번 반려 조건으로 요구.

---

## Minor

1. **PlayMode 스모크 테스트의 실제 커버리지가 이름/문서 표현보다 좁음** — 위 중점 점검 3 참고. 테스트 이름(`StickmanFallsSettlesAndWanders`)과 Tasklist.md 표현은 "자율배회 AI 실증"을 폭넓게 주장하지만, 실측 재현 결과 Idle/Walk 외 다른 상태는 씬 구조상(단일 평면 발판, 충돌 대상 전무) 근본적으로 도달 불가능하다. 테스트 자체를 지금 확장할 필요는 없으나(그러려면 벽/장애물 있는 발판 구성이 먼저 필요), 주석이나 Tasklist에 "이 테스트는 Idle/Walk/접지 스냅만 검증하며 ParkourClimb/Ragdoll/Getup/Attack은 커버하지 않는다"는 범위 한정 문구를 추가하면 다음 라운드에서 혼동을 줄일 수 있다.
2. **`SceneBootstrapper.CreateOrLoadConfig()`가 기존 config 로드 시에도 `groundSnapTolerance` 한 필드만 항상 강제 덮어씀** — `SceneBootstrapper.cs:71`. 다른 필드는 "기존 값 보존"이 기본 정책인데 이 필드만 예외적으로 항상 20으로 재설정된다. 누군가 나중에 이 값을 다른 이유로 에디터에서 수동 조정해두면, `BuildAll`을 다시 실행하는 순간(예: 프리팹만 고치려고) 조용히 20으로 되돌아간다. 급하지 않으나 주석으로 "이 필드는 재실행 시 항상 재적용됨"을 명시하면 좋겠다.
3. **`HingeJoint2D.m_UseLimits: 0`(각도 제한 없음)** — 4개 관절 전부 각도 제한이 없어(YAML 확인) GETUP의 P-제어 모터가 이론상 무릎/팔꿈치를 반대 방향으로 굽히는 등 부자연스러운 경로로 목표 각도에 도달할 수 있다. 이는 Phase 2 리포트에서 이미 "이번 배선에서 구조만 만족, 실측 후 결정" 대상으로 disclosure된 항목이라 신규 버그로 잡지 않으나, 실제 프리팹이 생긴 지금이 `useLimits`를 켤 적기라는 점을 재확인해둔다.

---

## 반려 수정 + 디플레이킹 최종 확인 (Debugger, 2026-08-28, 대상 커밋 `2862ad6`)

**결론: 씬/프리팹 배선 최종 승인 보류 — 검증 과정에서 신규 Major 1건(BUG-SW-M4) 발견, Coder 재작업 필요.**

BUG-SW-M1/M2/M3 원 지적사항 자체와 스모크 테스트 디플레이킹 수정은 전부 실측으로 건전함을 확인했다. 그러나 지시받은 대로 PlayMode를 여러 차례 독립 재실행해 신뢰성을 검증하는 과정에서, 이번 라운드에 Coder가 신설한 `StickmanRagdollRecoveryTests.cs` 자체가 약 25% 확률로 실패함을 발견했고, 원인을 추적한 결과 BUG-SW-M1이 완전히 해결되지 않았다는 결론에 도달했다. 아래 상세.

### BUG-SW-M1 재검증 — 배선은 정확하나, 신규 Major(BUG-SW-M4) 발견

**배선 자체는 전부 정확히 구현됨(실측 확인)**:
- `ProjectSettings/TagManager.asset`에 `StickmanLimb` 레이어가 인덱스 8에 등록됨을 확인.
- `ProjectSettings/Physics2DSettings.asset`의 `m_LayerCollisionMatrix`(256 hex = 128byte = 32×32bit 매트릭스)를 직접 바이트 단위로 디코딩 — 레이어8 행의 8번 비트가 정확히 0(자체충돌 비활성)이고 나머지 31비트는 전부 1(다른 레이어와는 정상 충돌)임을 확인. `Physics2D.IgnoreLayerCollision(8,8,true)`가 의도대로 정확히 반영됨.
- `Main.unity`에 `PhysicsGround`(BoxCollider2D, size={200,2}, position.y=4, layer=0 Default) 신규 확인 — 콜라이더 상단 Y=5가 `cam.transform.position.y(0) + cam.orthographicSize(5)`와 정확히 일치, 클래스 문서 주석과 부합.
- `Stickman.prefab` YAML 직접 확인: 4개 팔다리 전부 `m_Layer: 8`, 각각 `BoxCollider2D` 부착(시각 크기와 동일), `RagdollLimbImpactRelay` 4개 부착(스크립트 GUID `2ac7cc5aa599b44fc9b8e2ce2ebc58c9`로 대조 확인 — 클래스명은 YAML에 없으므로 문자열 grep만으로는 놓칠 뻔했다), 4개 전부 `_agent` 필드가 루트 `StickmanAgent`(fileID를 script guid까지 교차 검증)로 정확히 배선됨. `HingeJoint2D` 4개 전부 `m_EnableCollision: 0`(연결된 바디끼리 애초에 충돌 안 함, 표준 설정) 확인.
- `StickmanRagdollRecoveryTests.cs` 코드 리뷰 — `ReportExternalImpact()` 강제 호출 → `Assert.AreEqual(Ragdoll, ...)` → Getup/Idle·Walk 폴링 → `Assert.IsTrue(sawGetup)`/`Assert.IsTrue(recoveredToActive)` 이중 검증. 실제 물리 상태를 측정하는 진짜 assert이며 트리비얼하지 않음을 확인.

**그러나 반복 재실행에서 실제 정착 실패를 발견**:
- `-runTests -testPlatform PlayMode -quit 없이` 8회 독립 재실행(매회 새 프로세스, `System.Guid` 기반 RNG로 경로 상이) 결과 **2회(런3, 런7) `RagdollEntersAndRecoversToActiveState` 실패**, 6회 통과.
- 실패 2건 모두 로그상 `충격 전 상태=Walk`(캐릭터가 이동 중일 때 강제 충격을 받음), 통과 6건 모두 `충격 전 상태=Idle`(정지 중 피격) — 뚜렷한 상관관계.
- 실패 시 15초 관찰 동안 `state=Ragdoll`을 벗어나지 못하고, `maxLimbSpeed`가 정착 임계값(0.3) 위아래를 감쇠 없이 계속 넘나듦(예: 런3 로그 `0.018→0.889→1.092→0.541→...`) — settle 조건(0.3 이하 0.5초 연속 유지)이 한 번도 성립하지 않음.
- 근본 원인 규명을 위해 임시 진단 PlayMode 테스트(`DebuggerDiagRagdollWalkImpactTest.cs`, 검증 직후 삭제 — 삭제 후 `git status`/`git diff`로 저장소에 잔여 변경 없음 확인)를 작성해 "Walk 상태를 확정으로 잡은 뒤 강제 충격 + 45초 장기 관찰"을 실행: `maxLimbSpeed`가 **45초 내내 감쇠 없이 약 2초 주기로 0.02~0.65 사이를 안정적으로 오갔다**(진폭이 시간에 따라 줄어드는 추세가 관측되지 않음) — 15초든 45초든 질적으로 동일한 패턴이라 "느리게 정착"이 아니라 **"사실상 정착하지 않는 비감쇠 진동"**에 가깝다고 판단.
- 코드상 원인 추정(수정은 Architect/Coder 판단, 여기서는 진단만): (1) 모든 사지 `Rigidbody2D`가 `m_LinearDamping: 0`(각속도 감쇠도 확인 필요)로 감쇠가 전혀 없음. (2) `RagdollRig.EnterRagdoll()`(`Assets/_Project/Scripts/States/RagdollRig.cs`)은 조인트 모터만 끌 뿐 각 파츠의 `linearVelocity`/`angularVelocity`를 전혀 초기화하지 않음. (3) `WalkState.Tick()`이 매 프레임 루트 `Rigidbody2D.linearVelocity.x`를 직접 대입하는데 `HingeJoint2D` 구속 때문에 팔다리도 결국 비슷한 속도로 끌려가고, 이 운동량을 가진 채 그대로 RAGDOLL로 전이하면 바닥 마찰만으로는 다 흡수되지 못하고 진자처럼 계속 되튀는 것으로 보인다.
- **신규 Major — BUG-SW-M4(제안): "이동 중 피격 시 RAGDOLL이 사실상 정착하지 못하고 GETUP에 영원히 도달하지 못할 수 있음"**. 원래 BUG-SW-M1이 우려했던 "화면 밖 무한낙하"는 확실히 해결됐다(바닥에 붙은 채로 진동하므로 카메라 밖으로 사라지지 않음). 그러나 "RAGDOLL→GETUP 복귀"라는 이번 라운드의 핵심 계약은 **정지 중 피격(8/8 전부 0.25~1.25초 내 정상 복귀)**에서만 검증됐을 뿐, **이동 중 피격(2/2 전부 15초+/45초 확장관찰에서도 미복귀)**에서는 깨져 있다. `DragThrowState`/`RivalStickmanAgent`가 트리거하는 실전 RAGDOLL은 캐릭터가 가만히 서 있을 때보다 전투/이동 중 발생할 확률이 낮지 않으므로, 재현 빈도가 무시할 수준이 아니라고 판단해 Major로 분류한다.
- 수정 제안(강제 아님): (a) 사지 `Rigidbody2D`에 적당한 linear/angular damping 부여, (b) `EnterRagdoll()` 시점에 파츠 속도를 일부만 감쇠(완전 제로화는 "충격에 날아가는" 손맛을 죽이므로 비추천), (c) 원 리포트가 이미 제안했던 "일정 시간(예: 5~7초) 경과 시 속도 무관 강제 Getup" 안전망 — 이번 실측 결과를 보면 이 안전망이 사실상 필수로 보인다.

### BUG-SW-M2 재검증 — 정상

`Main.unity`에서 `orthographic size: 5` 직접 확인(원복됨). `NullPlatformWindowService.cs`에 `DummyFootholdWidthMultiplier=4f` 기반 화면 폭 독립 확장 로직이 정확히 반영됨을 확인. 재계산: px/unit = `Screen.height(480) / (2×orthoSize(5))` = 48, `groundSnapTolerance(20px) / 48 ≈ 0.417` world-unit — 원래 설계 의도(0.3~0.4유닛)와 재부합함을 확인(실측 로그 `cam.orthoSize=5` 매치).

### BUG-SW-M3 재검증 — 정상

`-executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll`을 강제 플래그 없이 재실행 → 로그에 `DefaultStickConfig.asset`/`Stickman.prefab`/`Main.unity` 3개 전부 "이미 존재해 건너뜁니다" 메시지 확인, `git status`/`git diff`로 세 대상 파일이 바이트 단위로 무변경임을 재확인 — 멱등성 유지됨.

### 디플레이킹 수정 검증 — 건전함 (가장 중요, 상세)

- **코드 구조 분석**: `WalkState`/`IdleState` 둘 다 매 프레임 `StickmanBlackboard.GroundedTick()`을 호출하며, 이 함수는 `info.Grounded==false`가 `fallGraceDuration`(0.1초) 이상 지속되면 즉시 `Fall`로 강제 전이시킨다. 즉 상태머신이 지금 `Idle`/`Walk`로 분류돼 있다는 사실 자체가 "현재 실제로 접지 중"임을 구조적으로 보장하며, `Jump`/`Fall`/`ParkourClimb`/`Ragdoll`/`Getup`는 이 최종 판정에서 전부 배제된다 — "불안정한 상태에서 우연히 걸려 통과"하는 역방향 위양성 경로는 코드 구조상 존재하지 않음을 확인했다.
- **반복 재실행**: 기존 스모크 테스트(`StickmanFallsSettlesAndWanders`)만 놓고 8회 독립 실행 전부 100% 통과 — 회차마다 RNG 배회 경로가 달랐음(X 이동 범위 3.4~10.0유닛 등 다양)에도 흔들림 없었다.
- **버그 주입 검증 1(양성 대조)**: `GroundSensor.cs`의 `withinYBand` 판정을 강제로 `false`로 바꿔(접지가 영구히 실패하도록 재현) 재실행 → 테스트가 정확히 실패(`최종 상태=Fall`, exit code 2)함을 확인 — 이 assert에 실제 탐지력이 있음을 실증. 즉시 원복 후 `git diff` 클린 확인.
- **버그 주입 검증 2(참고)**: `StickmanBlackboard.SnapToGround()`의 위치 강제대입 라인만 비활성화하는 주입은 테스트를 통과시켰다 — 이는 위양성이 아니라, BUG-SW-M1 수정으로 캐릭터 루트에 실제 `CapsuleCollider2D`+바닥 `Collider2D`가 생기면서 물리 충돌 자체가 위치를 붙잡아주는 이중 안전장치가 됐기 때문(수정의 자연스러운 부산물). 즉시 원복 후 `git diff` 클린 확인.
- **결론: 디플레이킹 판정 로직(`finalState == Idle/Walk`) 자체는 건전하고 실제 탐지력이 있다.** 다만 이를 검증하는 과정(반복 PlayMode 실행)에서 위 BUG-SW-M4를 별도로 발견했다.

### 최종 재검증 수치

- Unity 배치모드 독립 재컴파일: `error CS`/`warning CS` 매치 0건, exit code 0.
- EditMode: `total="13" passed="13" failed="0"` — 기준선 유지.
- PlayMode(기존 2개 테스트, `-quit` 없이 `-runTests` 사용): 단발 실행 기준 `total="2" passed="2" failed="0"` 재현 확인. 다만 8회 반복 시 `StickmanRagdollRecoveryTests`만 2/8 실패(위 BUG-SW-M4), `StickmanPlaytestSmokeTests`는 8/8 전부 통과.
- 모든 임시 진단 파일(`DebuggerDiagRagdollWalkImpactTest.cs`)과 임시 버그 주입(2건)은 검증 직후 삭제/원복했으며, 최종 `git status`/`git diff` 기준 이 검토가 추적 대상 파일에 남긴 변경은 0건이다(검토 도중 무관한 별도 작업자가 macOS 네이티브 창 열거 관련 파일들을 동시에 수정 중이었음을 확인했으나, 이는 이번 검토 범위 밖이며 손대지 않았다).

---

## macOS 열거 + 랙돌감쇠 + 가드대칭화 통합 확인 (Debugger, 2026-08-28, 대상 커밋 `6344058`)

**전체 승인 — 이번 라운드(씬배선+macOS+랙돌감쇠) 최종 완료**

Coder의 15회 100% 통과 주장을 그대로 신뢰하지 않고, 세 변경분(BUG-SW-M4 수정/macOS 창 열거/가드 대칭화) 전부 독립 재검증했다. 세 가지 모두 실측으로 건전함을 확인했다.

### 1) BUG-SW-M4(이동 중 피격 GETUP 영구실패) — 재발 없음, 승인

- **프리팹 실측**: `Stickman.prefab` YAML 직접 확인 — RightArm/LeftLeg/RightLeg/LeftArm(4개 팔다리) 전부 `m_LinearDamping: 0.6`, `m_AngularDamping: 1.5`로 변경됨(이전 `0`/`0.05`). 루트 `Stickman`(mass=1, 이동을 직접 구동하는 몸통)은 의도대로 `0`/`0.05` 그대로 유지 — 걷기 반응성에 영향 없이 팔다리 관성만 흡수하도록 설계된 것을 확인.
- **`RagdollRig.EnterRagdoll()` 코드 확인**: 관절 모터 OFF에 더해 전신 파츠(`_bodies`, 루트 포함)의 `angularVelocity`를 진입 시 1회 `×0.5`. `linearVelocity`는 미변경 — "충격에 날아가는" 손맛 보존 확인.
- **독립 재실행(Coder의 15회와 별개로 내가 직접 실행)**: `-runTests -testPlatform PlayMode -quit 없이` **20회** 독립 재실행(새 프로세스 2배치, 매회 `System.Guid` 기반 RNG로 경로 상이) — **20/20(100%) 전부 통과**, 매회 `total="2" passed="2" failed="0"`. 지시받은 8~10회를 넘겨 표본을 2배로 확보.
  - 충격 전 상태 분포: Idle 18회, **Walk 2회**(run8, run11). **Walk 피격 2/2 전부 성공** — run8은 t=3.25s, run11은 t=6.75s에 Idle/Walk로 복귀(15초 관찰 한도 대비 충분한 여유, 이전 반려 당시 관측됐던 "15초/45초 내내 미정착" 패턴 재현 없음). Idle 피격 18/18 전부 1.25s로 일관되게 복귀(회귀 없음).
  - 20개 로그 전체에서 `error CS`/`warning CS`/의미있는 `LogError`/`LogException` 0건.
- **결론**: Walk-피격 표본이 2건뿐이라 Coder의 4/4보다 적지만, 합산하면 Walk 피격 총 6/6(Coder 4 + Debugger 2) 전부 성공으로 반려 당시의 25%(2/8) 실패율과 뚜렷이 대비된다. damping 부여 + 진입 시 각속도 감쇠 조합이 실제로 문제를 해소했다고 판단.

### 2) MacWindowService P/Invoke 마샬링 — 코드 확인 결과 정확, 실측 재현 성공

- **Boolean 마샬링**: `CGRectMakeWithDictionaryRepresentation`/`CFStringGetCString`/`CFNumberGetValue` 3개 함수 전부 `[return: MarshalAs(UnmanagedType.I1)]` 명시 확인(1바이트 CoreFoundation Boolean과 정확히 일치). Win32Service의 `bool` 반환 함수들(4바이트 Win32 BOOL)에는 이 속성이 없는 것도 대조 확인 — 두 플랫폼 규칙 차이가 파일별로 정확히 반영됨.
- **`EnumerateFootholds()` 필터**: `kCGWindowLayer==0` 필터와, `kCGWindowOwnerPID`(1차)+`kCGWindowOwnerName`(보조) 이중 자기 자신 제외 로직 코드로 확인.
- **안전가드 대칭성**: `CreateOverlayWindow()`는 조회 전용(자기 창의 CGWindowID를 찾아 기록할 뿐, 아무것도 조작하지 않음), `SetClickThrough()`/`SetAlwaysOnTop()`은 Win32의 BUG-B1 가드와 동일하게 무조건 `NotSupportedException`. 부작용을 내는 코드는 어디에도 없음을 전체 파일 정독으로 확인 — macOS 쪽에 실수로 실제 조작 코드가 들어간 흔적 없음.
- **`MacWindowEnumerationDiagnostic.cs` 직접 재실행**(`-executeMethod ...LogEnumeration`, 이번 세션): `EnumerateFootholds() 결과 개수 = 2`(Cursor, 메모/Notes — 이번 세션 실제 창과 일치), 원시 21개 중 제어센터/메뉴바/Dock/알림센터/Finder데스크톱/Wallpaper/WindowServer배경 등 `layer≠0` 19개 전부 정확히 제외, 한글 오너 이름("제어 센터"/"메모"/"알림 센터") 깨짐 없이 디코딩, `SetClickThrough()`/`SetAlwaysOnTop()` 둘 다 `NotSupportedException` 정상 발동. Coder의 원 실측 로그와 값까지 일치(Cursor 창 rect 등). 재현 성공.

### 3) `!UNITY_EDITOR` 가드 대칭화 — 정확, 에디터는 항상 NullPlatformWindowService로 폴백

- `StickmanAgent.CreatePlatformService()` 4개 분기 전문 확인: `UNITY_STANDALONE_WIN && !UNITY_EDITOR` / `UNITY_STANDALONE_OSX && !UNITY_EDITOR` / `UNITY_IOS || UNITY_ANDROID` / `#else`(NullPlatformWindowService). Windows·macOS 두 분기 모두 정확히 `&& !UNITY_EDITOR` 대칭 적용 확인.
- `Library/EditorUserBuildSettings.asset` 직접 확인 — `m_ActiveBuildTarget`이 `OSXUniversal`(Architecture x64+ARM64)로 설정되어 있음을 재확인, Coder/Architect 주장(활성 빌드타깃=macOS)과 일치.
- 간접 증거: EditMode 13/13, PlayMode 20/20 전부 통과하며 로그 어디에도 `MacWindowService`/CoreGraphics 관련 로그가 등장하지 않고(발판은 계속 `NullPlatformWindowService`의 더미 발판 패턴), 낙하/스냅/배회 좌표 범위도 기존 기준선과 동일 — 에디터 배치모드에서 macOS 분기가 실제로 컴파일·실행되지 않고 있음을 실측으로 재확인(코드 가드가 실제로 작동 중).

### 최종 재검증 수치 (Debugger 독립)

- Unity 배치모드 컴파일: `error CS`/`warning CS` 매치 0건, exit code 0.
- EditMode: `total="13" passed="13" failed="0"` — 기준선 유지.
- PlayMode: 20회 독립 반복 `total="2" passed="2" failed="0"` **20/20(100%)** — 기준선(2/2) 대비 반복 견고성까지 확인(BUG-SW-M4 케이스 포함).
- `MacWindowEnumerationDiagnostic` 재실행 — Coder 원 실측과 값 일치, 재현 성공.

### 결론

세 변경분(BUG-SW-M4 damping 수정, MacWindowService, `!UNITY_EDITOR` 가드 대칭화) 전부 코드 검토 + 독립 실측(20회 PlayMode 반복 + macOS 진단 재실행)으로 문제 없음을 확인했다. 씬/프리팹 배선 반려 사이클에서 시작된 BUG-SW-M4까지 포함해 이번 라운드 전체 항목이 모두 해소됨.

**전체 승인 — 이번 라운드(씬배선+macOS+랙돌감쇠) 최종 완료**

---

## 카메라 프레이밍 수정 확인 (Debugger, 2026-08-28, 대상 커밋 `10e55ea`)

**승인 — BUG-P1-R4-B1(카메라 프레이밍) 해소 확인, 회귀 없음.**

지시받은 4개 항목을 전부 독립적으로(코드 검산 + grep + 실제 Unity 배치모드 실행) 재검증했다. Coder의 실측 주장을 그대로 신뢰하지 않고 직접 계산과 재실행으로 확인했다.

### 1) `NullPlatformWindowService`의 더미 발판 배치 — 계산으로 검산, 정확히 화면 하단

`NullPlatformWindowService.cs`의 생성자는 `dummyRect.y = baseHeight - baseHeight*DummyFootholdHeightFraction`(f=0.2) = `baseHeight*0.8`로 발판을 놓는다. `ScreenCoordinateConverter`의 좌표계(좌상단 원점, y 아래로 증가)에서 이 값을 Unity 스크린 좌표(좌하단 원점)로 뒤집으면 `unityY = Screen.height - osY = baseHeight*0.2`, 즉 화면 아래에서 20% 지점 — 명백히 "화면 하단" 쪽이다(예전 버그의 `y=0`은 반대로 화면 맨 위였음).

이 `unityY`를 직교카메라 월드좌표로 환산하면 `worldY = cam.y + orthographicSize*(2*unityY/Screen.height - 1) = cam.y - orthographicSize*(1-2f)` — `SceneBootstrapper.ComputeGroundTopWorldY()`가 쓰는 폐쇄형 수식과 대수적으로 정확히 일치함을 직접 유도해 확인했다(두 파일이 독립적으로 같은 값에 도달하는 게 아니라, 애초에 후자가 전자의 기하학적 결과를 그대로 재유도한 것 — 우연의 일치가 아니라 설계대로 상수(f)를 공유해서 나온 필연적 일치). f=0.2, orthoSize=5, cam.y=0 기준 `groundTopWorldY=-3`, 문서 주장과 일치.

### 2) `ComputeGroundTopWorldY` 단일 헬퍼 사용 — grep으로 중복 계산 없음 확인

`grep -rn "ComputeGroundTopWorldY\|groundTopWorldY\|orthographicSize" Assets/`로 전수조사한 결과, 이 계산식을 실제로 수행하는 코드는 `SceneBootstrapper.cs:396`(헬퍼 본체) 단 한 곳이고, 호출부는 `BuildMainScene()`의 캐릭터 초기 배치(`:350`)와 `CreateGroundCollider()`의 RAGDOLL 바닥 배치(`:412`) 정확히 두 곳뿐이다. 나머지 매치는 전부 주석/문서 설명이었다. 예전처럼 두 곳이 `cam.transform.position.y + cam.orthographicSize`를 각자 따로 계산하던 코드는 남아있지 않음을 확인 — 재발 방지 리팩터가 실제로 적용됐다.

### 3) `StickmanOnScreenFramingTests` — 진짜 assert, X축 미검증 근거도 실측으로 타당함 확인

- 가짜 통과 아님: `SceneManager.LoadScene`으로 실제 `Main.unity`를 로드하고, 실제 `SpriteRenderer.bounds`를 합산해 `Camera.WorldToScreenPoint`로 환산한 뒤 `Assert.GreaterOrEqual`/`Assert.LessOrEqual`로 화면 세로 범위를 검증한다 — 물리 시뮬레이션을 실제로 돌리는 진짜 상태 기반 assert다.
- X축 미검증 근거 검산: 주석이 드는 수치(walkSpeed=2.5, 최대 Walk 지속시간 ~4.7초)를 코드로 직접 대조했다 — `StickConfig.cs`의 `wanderWalkDurationMax=4.0`과 `AutoWanderController.Jitter()`의 `wanderDurationJitterRatio=0.175` 기본값으로 `4.0×1.175=4.7`이 정확히 나온다. `2.5×4.7=11.75`유닛 편도 이동이 가능한데, orthoSize=5·640x480(aspect 4:3) 기준 뷰포트 반폭은 `5×(640/480)=6.67`유닛뿐이라 산술적으로 자주 초과한다 — 근거가 타당하다.
- 아래 실측(5회 재실행)에서도 X가 실제로 화면 폭을 벗어나는 사례를 여러 번 직접 확인했다(예: run5 t=10s `bottomScreen.x=-44.3`, `Screen.width=640` 기준 화면 왼쪽 밖). X를 화면 폭으로 강제 검증했다면 이 버그와 무관하게 자주 실패했을 것 — 미검증 결정은 타당하다.

### 4) 이전 라운드(BUG-SW-M1~M4, macOS, 에디터가드)와의 공존 — 회귀 없음

재실행한 5회 PlayMode 로그에서 `StickmanRagdollRecoveryTests`(BUG-SW-M4 대상) 결과를 직접 대조했다: 5회 전부 `recoveredToActive=True`(런2~5는 정지 중 피격 1.25s, 런1은 이동 중 피격 3.25s로 정상 복귀 — Walk 피격 케이스도 재현됐고 정상 처리됨). 랙돌 정착 위치(`pos.y`)는 5회 전부 `-2.99~-3.00`으로, 바뀐 `groundTopWorldY=-3`과 정확히 일치 — 새 지면 Y로 바뀐 뒤에도 랙돌이 여전히 같은 논리적 바닥에 올바르게 안착함을 확인했다. macOS/에디터가드 쪽은 이번 커밋이 건드리지 않은 파일이라 별도 재검증하지 않았다(직전 라운드에서 이미 승인 완료).

### 독립 실측 수치 (Debugger, Coder의 실측과 별개로 직접 재실행)

- **컴파일**: Unity 배치모드(`Logs/dbg2_compile.log`) — `error CS`/`warning CS` 매치 0건, exit code 0.
- **EditMode**: `total="13" passed="13" failed="0"`(`Logs/dbg2_editmode.xml`) — 기준선 유지.
- **PlayMode 5회 독립 재실행**(매회 새 프로세스, `-runTests -testPlatform PlayMode`, `-quit` 미사용, `Logs/dbg2_pm_run1~5.log/xml`): **5/5 전부 통과**, 매회 `total="3" passed="3" failed="0"`(신규 `StickmanStaysWithinVerticalViewportMargin` 포함). 매회 다른 RNG seed 확인(진짜 독립 실행).
  - 신규 프레이밍 테스트 실측(5회, 15개 샘플): `bottomScreen.y` 89.4~96.4px, `topScreen.y` 117.1~183.0px — 여백 하한(24px)·상한(456px) 근처에 전혀 근접하지 않음.
  - `StickmanRagdollRecoveryTests` 5/5, `StickmanPlaytestSmokeTests` 5/5(최종상태 Idle/Walk 접지) 전부 정상 — 회귀 없음.
  - 로그 5개 전체에서 `error CS`/`warning CS` 0건.
- 테스트 실행 중 `Assets/`에 생성된 임시 `InitTestScene<guid>.unity`(Unity 테스트 러너가 배치모드에서 남기는 부산물, 이전 커밋에도 동일 패턴의 파일이 있었음)는 검증 후 삭제해 정리했다. `Assets/Plugins/macOS/`는 이번 검토와 무관한 별도 작업자의 진행 중인 작업(네이티브 오버레이 플러그인)으로 확인되어 손대지 않았다.

### 결론

계산 검산·grep 전수조사·실제 Unity 재실행 세 가지 모두 Coder의 주장과 일치했고, 회귀도 없었다. **승인.**
