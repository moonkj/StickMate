**Major 1건 발견 — Coder로 반려 필요**

# StickMate — Phase 4 버그 리포트 (Debugger, Teammate2)
> 작성: Debugger · 작성일: 2026-08-28 · 대상: 커밋 `577a7eb`("Phase 4: OS 장난(창도둑/청소부/그라피티/크래시/블랙홀) + PC 하드웨어 반응 + 자산 불변 감사 테스트")
> 범위: Phase 4 신규 파일 전체 — `States/WindowTheftState.cs`, `States/TimedSpectacleState.cs`, `Interaction/WindowTheftDirector.cs`, `Interaction/GraffitiDirector.cs`, `Interaction/DesktopIconMirrorDirector.cs`, `Interaction/WindowCrashDirector.cs`, `Interaction/HardwareReactionDirector.cs`, `Platform/IDesktopIconLayoutService.cs`(+ `NullPlatformWindowService`/`FallbackPlatformWindowService`/`Win32WindowService`의 구현분), `Core/SpectacleEventLock.cs`(신규 5종 `SpectacleEventKind` 추가분), `Core/StickConfig.cs`/`Core/StickmanEventBus.cs`(Phase 4 추가분), `Core/StickmanAgent.cs`(상태 등록분), `Tests/EditMode/UserAssetImmutabilityAuditTests.cs`.
> 환경: Unity 배치모드 클린 재빌드(`Library/ScriptAssemblies`/`Bee`/`PlayerDataCache` 강제 삭제 후 재컴파일) — `error CS`/`warning CS` 매치 0건, `Batchmode quit successfully`/`Exiting batchmode successfully now` 정상 종료. 이어서 `-runTests -testPlatform EditMode` 실행, `testResults.xml` 직접 파싱: `testcasecount="13" result="Passed" total="13" passed="13" failed="0"`. **에러 0/경고 0 + 13/13 통과 기준선 직접 재확인 완료.**

## 결론 요약

**Blocker 0건, Major 1건(BUG-P4-M1, 신규), Minor 2건.**

- **최우선 점검(유저 자산 불변)**: `UserAssetImmutabilityAuditTests.CollectScannedSourceFiles()`가 `Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)`로 `Assets/_Project/Scripts/` 전체를 파일명 하드코딩 없이 스캔함을 코드로 직접 확인 — Phase 4 신규 8개 파일 전부 자동 포함된다(디렉터리 전체 탐색이므로 재확인이 아니라 설계 자체가 이를 보장). 금지 API 블랙리스트(`File.Delete`/`SetWindowPos`/`MoveWindow`/`LVM_SETITEMPOSITION`/`SPI_SETDESKWALLPAPER` 등 11종)와 화이트리스트(Win32WindowService의 자기 오버레이 Z-order 조정 1건, 라인 단위 재검증)를 직접 확인했고 실제 코드베이스에 위반 0건임을 재확인. **정적 스캔이 못 잡는 종류의 우회**(문자열 결합 리플렉션 호출 등)는 Phase 4 8개 파일 전체에 `GetMethod`/`.Invoke(`/`Reflection`/`DllImport`/`Marshal.` 매치 0건(grep 직접 확인) — 발견 없음. **UX 27-7 "클릭 즉시 자진 취소" 판정 주기**: `DesktopIconMirrorDirector.MonitorActive()`가 활성 상태(`Update()`)마다(스로틀 없이 매 프레임) `ICursorPositionService.TryGetGlobalCursorPosition`(Win32 `GetCursorPos` 직접 호출, 캐싱/배치 없음)을 조회해 커서가 캡처 영역에 들어오면 즉시 취소함을 확인 — 판정 지연 위험 없음. 설령 취소가 한 프레임 늦더라도 오버레이가 애초에 100% 클릭관통(실제 아이콘 클릭 판정은 항상 실제 좌표 기준)이라 원칙 2/3 침해로 이어지는 경로 자체가 없음도 함께 확인.
- **점검 2(DesktopIconMirrorDirector/SpectacleEventLock)**: 청소부/블랙홀은 각각 별도 `DesktopIconMirrorDirector` 인스턴스(`_kind`로만 분기)지만 동일한 전역 `SpectacleEventLock`을 공유해, 한쪽이 `TryAcquire`로 점유 중이면 다른 쪽 `TickAutoTrigger()`가 `if (SpectacleEventLock.IsActive) return;`로 조용히 스킵함을 확인(추가 락 불필요, 27-2/27-5 요구사항 충족). 4개 신규 Director(`WindowTheftDirector`/`GraffitiDirector`/`DesktopIconMirrorDirector`/`WindowCrashDirector`) 전부 `OnDisable()`에서 Phase 3 BUG-P3-M1과 동일한 패턴(소유자 확인 후 강제 Idle 복귀 + `SpectacleEventLock.Release(this)`)을 갖고 있음을 코드로 확인 — Phase 3에서 확립된 관행이 정확히 재사용됨.
- **점검 3(윈도우 크래시 100% 클릭관통)**: `Platform.ILocalClickCaptureService`/`Interaction.StickmanClickHitbox` 문자열이 `WindowCrashDirector.cs`에 등장하긴 하나 **클래스 상단 문서 주석 안에서 "참조하지 않는다"고 서술하는 텍스트로만** 등장함을 직접 확인(`using` 없음, 필드/메서드 호출 없음) — 실제로 참조하는 코드는 프로젝트 전체에 0건, 100% 클릭관통 주장이 구조적으로 사실. 스윙(`TimedSpectacleState`, `windowCrashSwingDuration`)과 크랙 오버레이(`WindowCrashDirector`의 독립 `_overlayTimer`, `windowCrashOverlayDurationSeconds=3s`) 수명 분리도 코드로 확인 — 스윙 종료(Idle 복귀)와 무관하게 크랙은 자체 타이머로 `SpectacleEventLock`을 계속 쥐고, `OnStateTransitioned` 구독 자체가 없어(다른 4개 Director와 유일하게 다른 지점) 스윙의 상태 이탈 이벤트에 반응하지 않음을 확인 — Architect 승인 해석과 구현이 정확히 일치.
- **점검 4(HardwareReactionDirector)**: 우선순위(배터리>CPU>네트워크>충전)는 `ResolveAndNotify()`가 아무것도 표시 중이지 않을 때 이 순서로 후보를 고르므로 "동시에 여러 신호가 처음 충족되는" 케이스는 정확히 적용됨을 확인. 다만 **회복-쿨다운 카운트다운 로직에 실질적 결함을 발견(BUG-P4-M1, Major, 아래 상세)** — 배터리/충전/네트워크 3개 신호의 재알림 쿨다운이 사실상 거의 영원히 끝나지 않는다. CPU 신호는 동일 코드 패턴이 아니라 정확하게 구현되어 있어 영향 없음. CPU 프레임타임 근사치가 다른 무거운 스펙터클 렌더링과 동시에 돌 때 오탐할 가능성은 현재 Phase2+ 렌더링이 미구현이라 지금 당장 재현되지 않으나 잠재 위험으로 Minor 2에 기록.
- **점검 5(WindowTheftState self-transition 재발 여부)**: `WindowTheftDirector.OnStateTransitioned()`(`:170-181`)가 `if (evt.To == StickmanStateId.WindowTheft) return;` 가드를 정확히 갖고 있음을 확인 — Phase 3에서 발견된 "self-transition을 이탈로 오판해 락을 조기 해제" 함정이 여기서는 재발하지 않았다.
- **점검 6(IDesktopIconLayoutService 미구현 스텁 안전성)**: `Win32WindowService.TryGetIconRegion()`이 예외 없이 `false`/빈 목록을 반환하고, `DesktopIconMirrorDirector.TickAutoTrigger()`가 `if (svc == null) return;` / `if (!svc.TryGetIconRegion(out Rect region)) return;`로 예외 없이 조용히 스킵함을 확인 — 무한 대기·예외 전파 경로 없음.

---

## 권고 순서

1. **BUG-P4-M1 수정** — `HardwareReactionDirector.TickBattery`/`TickCharging`/`TickNetwork` 3곳이 `UpdateSignalLifecycle(...)` 호출 시 회복-쿨다운 감소량으로 그 프레임 하나의 `Time.deltaTime`(`dt`)을 넘기고 있어, 실제 경과한 폴링 간격(수십 초~수십 분)이 아니라 매번 한 프레임분(~0.016초)만 차감된다. `TickCpu`가 이미 올바르게 하고 있는 패턴(`elapsedThisSample`, 그 폴링 사이클에 실제로 경과한 시간을 넘김)을 나머지 3곳에도 동일하게 적용하면 된다 — 각 함수가 이미 `interval`(그 신호의 폴링 주기)을 로컬 변수로 갖고 있으므로 그 값을 넘기는 한 줄 수정 3곳이면 충분.
2. Minor 2건은 급하지 않음 — Minor 1(우선순위 프리엠션 해석)은 Architect 확인만 받으면 되는 저비용 사안, Minor 2(CPU 오탐 잠재 위험)는 Phase2+ 렌더링 착수 전 참고 사항으로 이월.

---

## 이번 라운드 중점 점검 항목 결론

| # | 점검 항목 | 결론 |
|---|---|---|
| 1 | 유저 자산 불변 재확인 — 정적 스캔의 디렉터리 커버리지 + 정적 스캔이 못 잡는 실질적 침해 + 27-7 "즉시 자진 취소" 판정 주기 | **커버리지 확인, 실질적 우회 미발견, 판정 지연 위험 없음.** `UserAssetImmutabilityAuditTests.CollectScannedSourceFiles()`(`Tests/EditMode/UserAssetImmutabilityAuditTests.cs:37-46`)는 `Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)`로 파일명 하드코딩 없이 `Assets/_Project/Scripts/` 전체를 스캔 — Phase 4의 8개 신규 파일이 자동으로 포함됨을 디렉터리 구조로 직접 확인(모두 `Scripts/States`, `Scripts/Interaction`, `Scripts/Platform` 하위). Phase 4 8개 파일에 리플렉션 기반 API명 조립 호출(`GetMethod`/`.Invoke(`/`DllImport`/`Marshal.`) 매치 0건(grep). `DesktopIconMirrorDirector.MonitorActive()`가 청소부/블랙홀 진행 중 매 `Update()` 프레임마다(스로틀 없음) `ICursorPositionService.TryGetGlobalCursorPosition`을 직접 호출(캐싱 레이어 없음, Win32는 `GetCursorPos` 즉시 호출)해 커서가 캡처 영역에 들어오는 순간 즉시 취소함을 확인 — "판정 주기가 길어 그 사이 유저가 클릭"할 여지가 사실상 없다. 설계상으로도 이 자진 취소가 설령 한 프레임 늦더라도 실제 클릭 판정은 항상 오버레이가 아닌 **실제 아이콘 좌표**를 기준으로 하므로(오버레이는 100% 클릭관통) 원칙 2/3 침해로 이어지는 경로 자체가 없다. |
| 2 | DesktopIconMirrorDirector와 SpectacleEventLock의 상호배제, 하나 진행 중일 때 다른 하나 요청 시 조용히 스킵 여부, `OnDisable()` 락 해제 관행 적용 여부 | **상호배제/조용한 스킵/OnDisable 해제 전부 확인.** 청소부/블랙홀은 별도 인스턴스이지만 같은 전역 `SpectacleEventLock`을 공유해, `TickAutoTrigger()`(`DesktopIconMirrorDirector.cs:120-157`)의 `if (SpectacleEventLock.IsActive) return;`(:133) 한 줄만으로 서로도 자동 상호배제됨을 확인. `WindowTheftDirector`/`GraffitiDirector`/`DesktopIconMirrorDirector`/`WindowCrashDirector` 4개 전부 `OnDisable()`에서 "소유자 확인 → 소유 중이면 강제 Idle 복귀 → `SpectacleEventLock.Release(this)`" 패턴(Phase 3 BUG-P3-M1 수정 패턴과 동일)을 갖추고 있음을 코드로 확인(각 파일 `ReleaseOwnedLock()`/`ReleaseOwned()` 메서드). |
| 3 | 윈도우 크래시 100% 클릭관통 — `ILocalClickCaptureService` 실참조 여부(grep), 스윙/크랙 수명 분리 구현 여부 | **클릭관통 구조적으로 보장 확인, 수명 분리 구현 확인.** `grep -n "ILocalClickCaptureService\|StickmanClickHitbox" Interaction/WindowCrashDirector.cs` 결과 매치 1건뿐이며 그 라인은 클래스 문서 주석("...어디서도 참조하지 않는다")이지 실제 참조가 아님을 직접 확인 — `using`/필드/메서드 호출 전부 0건. `TimedSpectacleState`(스윙, `windowCrashSwingDuration`)와 `WindowCrashDirector`의 독립 `_overlayTimer`(크랙, `windowCrashOverlayDurationSeconds`)가 서로 다른 두 타이머로 분리되어 있고, `WindowCrashDirector`는 (다른 3개 Director와 달리) `StickmanEventBus.StateTransitioned`를 아예 구독하지 않아 캐릭터의 Idle 복귀 이벤트에 반응하지 않음을 확인 — Architect가 승인한 "스윙 짧게, 크랙은 3초 독립 유지" 해석과 구현이 정확히 일치한다. |
| 4 | HardwareReactionDirector — 4개 신호 독립 쿨다운/회복게이트 여부, 우선순위가 동시 충족 시 실제 적용되는지, CPU 근사치의 다른 스펙터클과의 상호작용으로 인한 오탐 가능성 | **Major 발견(BUG-P4-M1) — 4개 중 3개(배터리/충전/네트워크)의 회복 쿨다운이 사실상 작동하지 않는다.** `TickBattery`/`TickCharging`/`TickNetwork`(`:73-139`)가 `UpdateSignalLifecycle(state, sustainedNow, dt, ...)`를 호출할 때 그 프레임의 `Time.deltaTime`(`dt`, ~0.016초)을 그대로 넘기는데, 이 호출은 매 프레임이 아니라 각 신호의 폴링 주기(배터리 90초/충전 30초/네트워크 20초 기본값)가 찰 때만 실행된다 — 즉 "경과 시간"으로 넘겨야 할 값이 실제로는 폴링 주기 전체가 아니라 그 순간의 한 프레임분뿐이다. `UpdateSignalLifecycle`의 `RecoveryCooldownRemaining -= dt`(:160)가 이 값으로 감소하므로, 기본 쿨다운 420초(7분)가 실제로 0에 도달하려면 (420 / 0.0167) × 90초 ≈ 26일(배터리), × 30초 ≈ 8.7일(충전), × 20초 ≈ 5.8일(네트워크)이 걸린다 — 사실상 그 세션 동안 다시는 재알림되지 않는다. `TickCpu`(:103-124)만은 `elapsedThisSample`(그 샘플 구간에 실제 경과한 시간, ~7초)을 올바르게 넘겨 정상 작동한다. 결과적으로 "정상 범위로 회복 후 쿨다운 경과 시 재알림"(27-6 보강 규칙)이라는 반복 사용성 요구사항이 배터리/충전/네트워크 3종에서 최초 1회만 동작하고 이후로는 사실상 영구 침묵하는 방향으로 깨진다(반대로 과도한 재알림/스팸 방향은 아니므로 원칙 위반은 아니나, 명시된 UX 스펙과 실제 동작이 크게 어긋난다). 자동 테스트로는 잡히지 않는다(HardwareReactionDirector 전용 EditMode 테스트 없음, 정적 스캔 대상도 아님) — 수 시간~수일 단위 드라이프 세션에서만 드러나는 전형적인 은닉 버그. **우선순위**는 `ResolveAndNotify()`(:174-189)가 아무것도 표시 중이지 않을 때만 배터리→CPU→네트워크→충전 순서로 후보를 선택하므로 "여러 신호가 동시에 새로 충족되는" 케이스는 정확히 적용됨을 확인했다. 다만 이미 낮은 우선순위가 표시 중일 때 더 높은 우선순위가 나중에 충족되어도 선점(preempt)하지 않는 설계(코드 주석에 판단 근거 명시, Architect 확인 요청 상태)라 "우선순위"의 의미를 "동시 후보 중 택1"로 좁게 해석하고 있음 — 버그로 잡지 않고 Minor 1로 기록(이미 Coder가 스스로 확인을 요청해둔 사안). CPU 근사치가 다른 무거운 스펙터클(그라피티/청소부/블랙홀의 Phase2+ 렌더링)과 동시에 돌 때 프레임타임 상승을 "시스템 과부하"로 오판할 가능성은 구조적으로 존재하나(HardwareReactionDirector는 `SpectacleEventLock` 상태를 전혀 참조하지 않음), 그 렌더링 자체가 아직 구현되지 않아 지금 재현 불가능 — Minor 2로 기록. |
| 5 | WindowTheftState self-transition 패턴 재사용 시 Phase 3 함정(Director가 self-transition을 이탈로 오판) 재발 여부 | **재발하지 않음, 가드 정확히 존재.** `WindowTheftState.Tick()`(`States/WindowTheftState.cs:74-82`)이 2회차 시도 종료 시 `_pendingGiveUp = true`로 표시한 뒤 `Machine.ChangeState(StickmanStateId.WindowTheft, isForcedInterrupt: false)`로 자기 자신에게 재전이하고, 재실행된 `Enter()`가 이 플래그를 소비해 GiveUp 페이즈로 진입하며 대사(`DialogueIntent`)를 만든다(원칙 1 — `Tick()` 도중에는 대사를 만들지 않음). `WindowTheftDirector.OnStateTransitioned()`(`:170-181`)에 `if (evt.To == StickmanStateId.WindowTheft) return;` 가드가 정확히 존재해, `From==To==WindowTheft`인 self-transition 이벤트에서 락을 조기 해제하지 않음을 확인 — `BattleMinigameDirector`가 Phase 3에서 겪은 것과 동일한 함정이 이번엔 처음부터 예방적으로 구현되어 있다. |
| 6 | IDesktopIconLayoutService Win32 미구현 스텁이 상위 DesktopIconMirrorDirector를 무한 대기/예외 없이 안전하게 스킵시키는지 | **안전 확인, 예외/무한대기 경로 없음.** `Win32WindowService.TryGetIconRegion()`(`Platform/Windows/Win32WindowService.cs:258-262`)은 항상 `false`+`default`를 즉시 반환하고 `EnumerateIconRects()`는 빈 리스트를 반환한다(예외 던지는 코드 없음, 블로킹 호출 없음). `DesktopIconMirrorDirector.TickAutoTrigger()`(`:120-157`)는 `IconService`가 `null`이거나(`:136`) `TryGetIconRegion`이 `false`를 반환하면(`:137`) 그 즉시 `return`하여 이번 유휴 판정 주기를 조용히 스킵한다 — Windows 실빌드에서 청소부/블랙홀은 "트리거만 영구 억제"되는 안전한 no-op으로 확인된다. |

---

## Major

### BUG-P4-M1 — HardwareReactionDirector의 배터리/충전/네트워크 3개 신호가 회복-쿨다운 감소량으로 폴링 간격이 아닌 한 프레임분(`Time.deltaTime`)만 사용해, 재알림 쿨다운이 사실상 영구히 끝나지 않는다

- **파일**: `Assets/_Project/Scripts/Interaction/HardwareReactionDirector.cs:73-139` (`TickBattery`/`TickCharging`/`TickNetwork`), 비교 대상 정상 구현: `:103-124`(`TickCpu`)
- **근거 코드**:
  ```csharp
  private void TickBattery(float dt)          // dt = 그 프레임의 Time.deltaTime
  {
      _batteryPollTimer += dt;
      float interval = Mathf.Max(1f, _config.hardwareBatteryPollInterval); // 예: 90초
      if (_batteryPollTimer < interval) return;   // 폴링 주기(90초)마다 한 번만 아래로 진행
      _batteryPollTimer = 0f;
      ...
      UpdateSignalLifecycle(_battery, sustainedNow, dt, _config.hardwareReactionCooldownSeconds);
      //                                        ^^ 여기 — 90초가 아니라 이 한 프레임의 dt(~0.016초)를 넘김
  }

  private void TickCpu(float dt)
  {
      ...
      float elapsedThisSample = _cpuSampleTimer;  // 실제로 경과한 샘플 구간(~7초)을 별도로 기억해뒀다가
      _cpuSampleTimer = 0f;
      ...
      UpdateSignalLifecycle(_cpu, sustainedNow, elapsedThisSample, ...); // 올바르게 그 값을 넘김
  }

  private static void UpdateSignalLifecycle(SignalState state, bool sustainedNow, float dt, float cooldownSeconds)
  {
      ...
      if (!sustainedNow)
      {
          if (state.RecoveryCooldownRemaining > 0f) state.RecoveryCooldownRemaining -= dt; // dt가 작을수록 거의 안 줄어듦
          else state.Notified = false;
      }
  }
  ```
  `TickCharging`(`:100`)과 `TickNetwork`(`:138`)도 동일하게 `dt`를 그대로 넘긴다. `TickCpu`만 실제 경과 시간(`elapsedThisSample`)을 별도로 계산해 넘기고 있어 이 3곳과 코드 형태가 다르다.
- **영향 계산**: 기본값 기준(`hardwareReactionCooldownSeconds = 420f`, 7분), 60fps 가정(`dt ≈ 0.0167초`):
  - 배터리(폴링 90초): 420초 쿨다운을 실제로 소진하려면 (420/0.0167) × 90초 ≈ 26.2일 소요.
  - 충전(폴링 30초): ≈ 8.7일.
  - 네트워크(폴링 20초): ≈ 5.8일.
  - 즉 이 세 신호는 한 번 표현되고 회복된 뒤, 사실상 그 앱 실행 세션 동안 다시는 재알림되지 않는다(`Notified`가 사실상 영구히 `true`로 고착).
- **왜 Major인가**: 24시간 상주를 표방하는 앱의 핵심 반복 사용성 요구사항(27-6 "회복 확인 후 쿨다운 경과 시에만 재알림")이 정확히 반대 방향(과소 알림)으로 깨지며, 자동화된 테스트로 전혀 검출되지 않고(정적 스캔 대상 아님, 전용 EditMode 테스트 없음) 수 시간~수일 단위로만 드러나는 은닉성 짙은 버그다. 다만 Blocker로 올리지 않은 이유: 크래시/예외/유저 자산 침해/코어 루프 마비를 유발하지 않고, 최초 1회 발동은 정상 동작하며, 발현까지 시간이 걸려 당장 앱을 못 쓰게 만들지는 않는다.
- **수정 제안**: `TickBattery`/`TickCharging`/`TickNetwork` 3곳의 `UpdateSignalLifecycle(...)` 호출에서 마지막에서 두 번째 인자로 `dt` 대신 그 함수가 이미 로컬로 갖고 있는 `interval`(폴링 주기)을 넘긴다 — `TickCpu`가 `elapsedThisSample`을 넘기는 것과 동일한 수정. 각 함수당 한 줄 변경.

---

## Minor

### Minor 1 — HardwareReactionDirector 우선순위가 "동시 신규 충족" 시에만 적용되고, 이미 낮은 우선순위가 표시 중일 때 더 높은 우선순위가 나중에 충족돼도 선점(preempt)하지 않는다

- **파일**: `Assets/_Project/Scripts/Interaction/HardwareReactionDirector.cs:169-198`(`ResolveAndNotify`/`TryStart`)
- UX 23절 "동시 충족 시 배터리>CPU>네트워크>충전 우선순위로 하나만 표현"을 Coder가 "이미 표시 중인 반응은 강제로 끊지 않고 회복될 때까지 유지(표현 전환 시 깜빡임 방지)"로 해석해 구현했다 — 코드 주석에 판단 근거를 명시하고 Architect 확인을 요청해둔 상태(Tasklist.md 교차 레이어 로그 "설계 결정 5" 인근). 실제 영향은 제한적이다: 배터리-낮음과 충전-중이 물리적으로 동시에 오래 지속되기는 드물지만, CPU 과부하와 네트워크 끊김은 충분히 동시 발생 가능해 "먼저 충족된 낮은 우선순위가 나중에 충족된 높은 우선순위를 계속 가린다"는 시나리오가 현실적으로 존재한다.
- 버그로 집계하지 않은 이유: 코드가 이미 이 해석과 근거를 명시적으로 남겨 Architect 확인을 기다리고 있고("설계 결정 4"처럼 이미 확인된 것과 달리, Architect의 명시적 승인 목록(Tasklist.md Phase4 교차 레이어 로그 첫 항목)에는 아직 포함되지 않았다), 위험도가 낮다. **권고**: Architect가 다음 라운드 착수 전 이 해석을 확정할 것 — 선점 방식으로 바꾸려면 `ResolveAndNotify()`에 몇 줄만 추가하면 되는 저비용 변경이라고 Coder가 이미 명시해뒀다(단, 이번엔 하드웨어 반응 자체가 아니라 우선순위 프리엠션 여부에 대한 것이라 별도 확인 필요).

### Minor 2 — CPU 프레임타임 근사치가 향후 Phase2+ 렌더링(다른 스펙터클의 무거운 시각효과)과 동시에 돌 때 오탐할 잠재 위험

- **파일**: `Assets/_Project/Scripts/Interaction/HardwareReactionDirector.cs:103-124`(`TickCpu`)
- CPU 신호는 이 앱 자신의 `Time.deltaTime`을 "시스템 부하의 매우 거친 근사치"로 쓴다는 한계가 코드 주석에 이미 정직하게 명시되어 있다. 이 근사치는 `HardwareReactionDirector`가 `SpectacleEventLock` 상태를 전혀 참조하지 않으므로(설계상 의도적 — Minor 1과 같은 맥락), 만약 향후 그라피티/청소부/블랙홀의 실제 스프라이트·파티클 렌더링(Phase2+ 담당, 아직 미구현)이 무거워지면 그 프레임타임 상승을 "PC가 과부하"라고 오판해 유저에게 잘못된 정보(가짜로 헐떡이는 연출)를 보여줄 위험이 구조적으로 존재한다.
- 지금 당장은 렌더링 레이어가 없어 재현 불가능하므로 버그로 집계하지 않는다. **권고**: Phase2+에서 실제 스펙터클 렌더링을 구현하는 담당자는 이 상호작용을 인지하고, 필요시 `TickCpu`의 샘플링 구간에 `SpectacleEventLock.IsActive` 동안의 프레임을 제외하거나 가중치를 낮추는 보정을 검토할 것.

---

## 과학적 토론 로그

이번 라운드는 원인 불명 버그가 없었다(BUG-P4-M1은 코드를 직접 추적해 원인이 100% 확정됨 — 가설 검증 절차 불필요).
