# 행동 명령창 헤더 한 줄 — 파생 규칙 (「지금은 다른 일 하는 중이에요」 거짓 교정)

> `design-narrative` · 수신: 리더(Architect) → 구현 `coder-ui`(리더 배정) · 테스트 `test-engineer`(리더 경유)
> 기준: HEAD `76504c2`. 인용한 `.cs`(`Interaction/ActionCommandPopover.cs` · `Interaction/PopoverPanel.cs` · 네 감독 · 락 보유 12파일 · `Core/CommandAvailability.cs` · `Core/HiddenCharacterCommandGate.cs` · `Assets/Editor/SceneBootstrapper.cs`)는 `git --no-optional-locks status` 기준 작업 트리 변경이 0이다. 줄 번호는 HEAD 기준이고, 옮겨질 곳에는 앵커를 함께 적었다.
> 발단: persona-immersion(소은) S3 → 리더 1.0 권고 채택. **프로덕션 `.cs` 0줄 수정 · Unity/앱 실행 0회 · 이 파일 신규 1개(미커밋).**
> **왜 새 문서인가**: 사유 문서(`CRACK_COMMAND_DISABLED_REASON.md`)는 크랙 타일 한 칸의 문구를 다룬다. 이 규칙은 **헤더라는 다른 표면**이고, 대상이 네 명령 전부와 가출·숨김 상태다. 사유 문서 §7 항목 1이 헤더 처방을 한 줄로 적어 두었는데, 이 문서의 결론이 그 처방과 다르다. 그래서 그 줄은 사유 문서에서 취소선으로 남기고 여기로 옮겼다.
> ★ **2판**(verify-change 조건부 통과 C4 반영): 규칙 4를 「락 또는 상태」에서 「**상태만**」으로 좁혔다(§3-5). 미커밋 문서라 제자리에서 고쳤고 옛 문장은 §9에 남겼다.

---

## 0. 결론 — 헤더 파생 규칙

위에서부터 처음 맞는 줄 하나를 쓴다. 조건은 전부 `RefreshContent`와 **같은 프레임**에서 계산한다.

| 순서 | 조건 | 헤더 | 지금과 비교 |
|---|---|---|---|
| 1 | 가출 중 — `_runaway.IsRunawayActive` | `지금 가출 중이에요` | 그대로 |
| 2 | 캐릭터가 안 보임 — `Agent.IsSuspended` | `HiddenCharacterCommandGate.HiddenReason` | 그대로 |
| 3 | 누를 수 있는 칸이 하나라도 있음 — `readyCount > 0` | `지금 시킬 수 있어요` | 그대로 |
| 4 | `readyCount == 0` **이고 캐릭터 상태가 바쁨** — 현재 상태가 Idle·Walk가 아님 | `지금은 다른 일 하는 중이에요` | **조건 추가** |
| 5 | 그 밖 — `readyCount == 0`, 캐릭터 상태는 Idle·Walk | **`지금은 시킬 수 있는 게 없어요`** (English `Nothing available right now`) | **신설** |

- 4의 「바쁨」은 네 감독이 상태 사유를 만드는 식과 같다: `if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)`.
- **락(`SpectacleEventLock.IsActive`)은 보지 않는다.** 락이 캐릭터 상태보다 오래 남는 보유자가 있기 때문이다(크랙, §3-5). 원칙 1상 「하는 중」은 상태에서만 파생한다.
- **`Agent == null`이면**(§1-1): 숨김도 바쁨도 거짓으로 친다 → 3 또는 5로 떨어진다. 상태 기계가 배선되지 않은 경우(`Agent.Blackboard?.Machine == null`)도 바쁘지 않은 것으로 친다. 그때 네 칸은 `이 빌드에는 없는 기능이에요`이고 5의 중립 문구가 참이다.
- 5에는 A1 전용 헤더를 **두지 않는다**(§3-3).

---

## 1. 사실 (코드 판독)

### 1-1. 헤더는 지금 어떻게 정해지는가

- `RefreshContent`(`ActionCommandPopover.cs:729-748`)가 네 칸의 `GetAvailability`를 묻고 `readyCount`를 센 뒤(`:736-740`) `SetStatusCaption(runaway, readyCount)`를 부른다(`:747`).
- `SetStatusCaption`(`:755-772`)의 결정 줄은 `:767-770`이다: 가출 → 숨김 → `readyCount > 0` → 그 밖 전부 `"지금은 다른 일 하는 중이에요"`.
  - ⇒ **캐릭터 상태를 보지 않고 「다른 일 하는 중」을 쓴다.** `readyCount == 0`인 이유가 자리 사유여도 같은 문장이 나온다.
- 헤더 자리: 폭 `ContentWidth − RecallChipWidth − Space2` = 448 − 88 − 8 = **352pt**, 글꼴 `FontCaption` 10(`:400-403`). 줄바꿈 없음.
- ★ 2판 — **헤더의 `Agent`와 감독의 `_player`는 같은 인스턴스인가**: 코드상 같다(씬 · 프리팹 실측은 미확인).
  - 헤더: `protected StickmanAgent Agent;`(`PopoverPanel.cs:213`)에 `Agent = GetComponent<StickmanAgent>();`(`:350`) — 명령창과 **같은 GameObject**의 에이전트다.
  - 프리팹 갱신 경로: `SceneBootstrapper.cs`가 프리팹 루트에 `EnsureComponent<ActionCommandPopover>(root)`(`:571`)를 붙이고, 같은 루트의 `root.GetComponent<StickmanAgent>()`(`:640`)를 감독의 `_player`로 배선한다(`TryWireField(behaviour, "_player", agent)` `:657`).
  - 새로 만드는 경로: 루트 `new GameObject("Stickman")`(`:887`)에 `root.AddComponent<StickmanAgent>()`(`:911`)를 붙이고, 감독도 같은 루트에 붙여 `_player = agent`로 배선한다(그라피티 `:1053` · 창 도둑 `:1074` · 크랙 `:1086-1088`).

### 1-2. 네 명령의 불가 사유 전수 — 반환 지점 23곳

`GetAvailability` 본문을 파일마다 읽었다(HEAD `76504c2`).

| 분류 | 반환 | 화면 문구 | 크랙 | 그라피티 | 창 도둑 | 활쏘기 |
|---|---|---|---|---|---|---|
| M 배선 누락 | `CommandAvailability.Missing` | `이 빌드에는 없는 기능이에요` | `:96-97` | `:66-67` | `:88-89` | `:156-157` |
| H 숨음 | `HiddenCharacterCommandGate.WhileHidden` | `지금은 숨어 있어요` | `:103` | `:73` | `:95` | `:163` |
| S 표면 억제 (A1) | `WhileUnsummonedSurfacesSuppressed` | 사유 문서 §0 상수 | `:112-113` | 7-b 예정 | 7-b 예정(E-3 뒤) | 대상 아님 |
| C 연출 잔여 | `Blocked(OverlayBusyReason)` | `아직 금이 안 사라졌어요` | `:115-116` | — | — | — |
| C 락 | `Blocked(StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind))` | `지금 ○○ 중이에요` | `:118-119` | `:75-76` | `:97-98` | `:165-166` |
| C 상태 | `Blocked(StickMateDisplayNames.BusyText(current))` | `지금 △△ 중이라 못 해요` | `:121-123` | `:78-80` | `:100-102` | `:168-170` |
| E 대상 · 자리 | 크랙 `NoForegroundWindowReason` · 그라피티 `NoEmptyRegionReason` · 창 도둑 `NoWindowListReason`과 `NoTargetReason` · 활쏘기 `NoPlacementReason` | `지금 앞에 있는 창이 없어요` · `낙서할 빈 자리가 없어요` · `다른 창 목록을 읽을 수 없어요`와 `지금 붙잡을 만한 작은 창이 없어요` · `과녁 놓을 자리가 없어요` | `:125-126` | `:82-83` | `:106-107` · `:109-110` | `:175-176` |

- 계수(HEAD `76504c2`, 네 파일의 `GetAvailability` 본문): 크랙 7 · 그라피티 5 · 창 도둑 6 · 활쏘기 5 = **23**.
- **검사 순서가 네 파일 모두 같다**: M → H → (S) → (C 연출 잔여) → C 락 → C 상태 → E.
  - ⇒ 캐릭터 **상태**가 바쁘면, 앞선 M·H·S에 걸리지 않은 칸은 전부 C 사유(락 또는 상태)를 보인다. 상태가 바쁠 때 락도 함께 잡혀 있는 경우가 대부분이라 C 락 문구가 먼저 보일 수 있다(§3-5 표).
- **C 연출 잔여는 늘 락과 같이 온다**: 크랙은 발동할 때 락을 잡고(`SpectacleEventLock.TryAcquire(SpectacleEventKind.WindowCrash, this)`), 금이 끝나거나 취소될 때 `FinishAndCooldown`의 `SpectacleEventLock.Release(this);`에서 놓는다. 금이 떠 있는 동안 다른 세 칸은 `지금 창 부수기 중이에요`다.
- `GetAvailability`가 아닌 곳의 문구(헤더 계산에 쓰이지 않는다):
  - 클릭 실행이 거부됐을 때의 타일 안내 `지금은 시작할 수 없어요`(`ActionCommandPopover.cs:608`).
  - 빈 사유로 `Blocked`를 부를 때의 대체값 `지금은 못 해요`(`CommandAvailability.cs`). 네 감독에 빈 사유 호출은 없다.
- S의 확장 7-b: 창 도둑 · 그라피티 명령 가드이고, 사유 문구는 크랙 A1과 공유하는 설계다(`docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md`, 앵커 `ROADMAP N-20 해제 조건 7(A1)의 확장 7-b` · `사유 문구(크랙 A1과 공유)`). **활쏘기는 7-b 대상이 아니다.**

---

## 2. 무엇이 거짓인가 — 장면별

| 장면 | 네 칸 | 지금 헤더 | 규칙 뒤 헤더 |
|---|---|---|---|
| **FTT**(전체화면 앱 위에서 명령창을 엶), 캐릭터는 걷는 중 | 크랙 S · 그라피티 E(빈 자리 없음) · 창 도둑 E(작은 창 없음) · 활쏘기 E(과녁 자리 없음) | `지금은 다른 일 하는 중이에요` — **거짓**. 캐릭터는 아무 일도 안 한다 | 5 `지금은 시킬 수 있는 게 없어요` |
| 7-b 착지 뒤 같은 장면 | 크랙 S · 그라피티 S · 창 도둑 S · 활쏘기 E | 같은 거짓 — 노출이 늘어난다 | 5 |
| 평소 바탕화면, 앞 창·빈 자리·작은 창·과녁 자리가 모두 없음(드묾) | 네 칸 E | 거짓 — A1 이전부터 있던 결함 | 5 |
| 캐릭터가 떨어지는 중 | 네 칸 C 상태 | `지금은 다른 일 하는 중이에요` — 참 | 4, 같은 문구 |
| ★ 2판: 크랙 스윙(0.4초) 동안 | 크랙 C 연출 잔여 · 나머지 C 락 | 참 | 4 |
| ★ 2판: 스윙이 끝난 뒤 금이 남은 약 2.6초 | 크랙 C 연출 잔여 · 나머지 C 락 `지금 창 부수기 중이에요` · 캐릭터는 Idle·Walk | `지금은 다른 일 하는 중이에요` — **느슨함**(캐릭터는 한가) | **5** `지금은 시킬 수 있는 게 없어요` |
| 씬에 감독 배선이 모두 없음 | 네 칸 M | 거짓(캐릭터가 바쁘지 않을 수 있다) | 캐릭터 상태가 한가하면 5, 바쁘면 4 |

- 그라피티가 전체화면 앱 위에서 빈 자리 판정에 막힌다는 것은 `UNSUMMONED_EFFECTS_INVARIANT.md`의 그라피티 행 판독 인용이다. Windows는 코드상 대부분 막히고, macOS는 미확인이다. 창 도둑과 활쏘기가 FTT에서 E로 막히는지는 창 배치에 달렸다. 세 칸이 동시에 E가 되는 빈도는 측정하지 않았다(§7).

---

## 3. 규칙의 근거

### 3-1. 원칙 1 — 캐릭터에 대한 문장은 캐릭터 상태에서만 파생한다

- 「다른 일 하는 중」은 **캐릭터 상태를 주장하는 문장**이다. 그래서 캐릭터의 상태 기계에서 나온 참 값이 있을 때만 쓴다(규칙 4).
- 중립 문구 `지금은 시킬 수 있는 게 없어요`는 캐릭터에 대해 **아무것도 주장하지 않는다**. 방금 센 `readyCount == 0` 한 가지만 말한다. 이 값은 네 칸의 판정에서 파생된 확정값이다.
- 이유는 칸마다 이미 적혀 있다(설명 자리를 이유로 교체, `ActionCommandPopover.cs:793`). 헤더가 이유를 대신 지어낼 까닭이 없다.

### 3-2. 왜 사유 문자열이 아니라 상태 식으로 판정하는가

- 헤더가 사유 **문자열**을 보고 「바쁨」을 가리려면 `StickMateDisplayNames`의 바쁨 문장 표를 헤더 쪽에 한 벌 더 들고 있어야 한다. 이는 CLAUDE.md가 경계하는 **기준과 대상이 갈라지는** 모양이다.
- 헤더가 주장하는 사실(캐릭터가 바쁘다)을 감독과 **같은 상태 식에서 직접** 읽으면 문장과 근거가 한 곳에서 나온다.
- 대가: 네 감독의 상태 식이 바뀌는 날(예: Jump 중에도 명령 허용) 헤더와 칸이 어긋날 수 있다. 그래도 헤더 문장은 여전히 상태 사실이라 **거짓말은 하지 않는다**. 어긋남은 감사 테스트로 잡는다(§4 테스트 iv).
- **대안 — 사유 종류 태그**(`CommandAvailability`에 M/H/S/C/E를 싣기): 판정이 한 벌이 되는 장점이 있다. 그러나 네 감독의 `Blocked(` 호출을 모두 고쳐야 하고, `WindowTheftDirector.cs`가 N-8 B등급이라 **E-3 결속 해제 뒤**로 묶인다. 지금은 권하지 않는다.

### 3-3. 왜 A1 전용 헤더를 두지 않는가

- 헤더는 **네 칸의 요약**이다. A1 사유가 말하는 경로는 **S 칸만** 푼다. E 칸(자리 없음)은 그대로 남는다. 헤더에 경로를 쓰면 경로가 풀지 못하는 칸까지 풀릴 것처럼 약속하게 된다.
- 「네 칸이 모두 S일 때만 A1 헤더」라는 변형도 검토했다. 그러나 활쏘기가 7-b 대상이 아니라서 H·M이 아닌 한 **네 칸이 모두 S가 되는 장면이 없다**(§1-2). 쓰이지 않을 분기라 두지 않는다.
- S 칸은 자기 칸에 이미 경로를 적는다. 헤더가 되풀이할 필요가 없다.

### 3-4. 규칙 5 문구 판정

기준은 사유 문서 §2의 1–7을 헤더에 옮겨 적용한다.
- 1: 규칙 5가 쓰이는 모든 장면에서 참이어야 한다.
- 2: 약속하지 않는다.
- 3: 루프가 없어야 한다.
- 4: 내부 용어를 쓰지 않는다.
- 5: 해요체 한 줄, 352pt 안이어야 한다.
- 6: 수식 관계가 한 읽기여야 한다.
- 7: 같은 화면의 다른 문자열과 지시 대상이 겹치지 않아야 한다.

| 후보 | 1 참 | 2 약속 없음 | 6 수식 | 7 지시 대상 | 폭 N / C (pt, 352 한도, 추정) | 판정 |
|---|---|---|---|---|---|---|
| **H1 `지금은 시킬 수 있는 게 없어요`** | 참 — 캐릭터를 말하지 않는다 | 예 | 「시킬 수 있는」 → 「게」 하나 | 「게」는 명령 칸 전체 — 겹침 없음 | 135.0 / 178.5 | **채택** |
| H2 `지금은 시킬 수 있는 게 없어요 — 칸마다 이유가 있어요` | 참 — M도 이유다 | 예 | 한 읽기 | 「칸」은 이 화면의 타일뿐 | 242.5 / 325.5 | 예비. 칸이 네 개뿐이라 뒤 절이 군더더기다 |
| (기각) `지금은 할 수 있는 게 없어요` | 주어가 캐릭터로 읽혀 「캐릭터가 못 한다」는 상태 주장이 된다 | — | — | — | — | 탈락(기준 1) |
| (기각) `잠시 뒤에 다시 시켜 주세요` | E 칸은 기다려도 안 풀린다 | **약속** | — | — | — | 탈락(기준 2) |

- H1은 규칙 3 `지금 시킬 수 있어요`와 **짝을 이룬다**. 같은 동사를 긍정과 부정으로 써서, 헤더가 요약만 한다는 것이 문장 모양으로 드러난다.
- 「지금은」의 대조 뉘앙스(「나중에는 될지도」)는 기존 규칙 4 문구와 같은 수준이다. 조건을 약속하지는 않는다.

### 3-5. 알고 두는 느슨함과 ★ 2판 — 락 보유자 조사로 규칙 4를 좁힌다

**락 보유자 전수**(프로덕션 `SpectacleEventLock.TryAcquire(` 호출 파일 12개 · 락 종류 13개, Tests 제외, HEAD `76504c2`). 「해제」 칸은 각 파일의 `OnStateTransitioned` 가드 줄과 해제 줄을 읽은 것이다(코드 판독, 러너 미확인).

| 락 종류 | 보유 파일 | 락을 놓는 때 | 캐릭터 상태 | 상태가 락보다 먼저 끝나는가 |
|---|---|---|---|---|
| DragAndThrow | `DragThrowController.cs` | `if (evt.From != StickmanStateId.Dragged) return;` 뒤 `SpectacleEventLock.Release(this);` | Dragged | 아니오 — 같은 전이에서 놓는다. 반대로 놓은 뒤에도 던짐·착지 상태가 이어져 **상태가 더 길다** |
| RodeoCursor | `RodeoCursorWatcher.cs` | `if (evt.From != StickmanStateId.RodeoCursor) return;` 뒤 해제 | RodeoCursor | 아니오 — 같이 끝남 |
| Runaway | `RunawayDirector.cs` | `if (evt.From != StickmanStateId.Runaway) return;` 뒤 해제 | Runaway(`RunawayState`) | 아니오 — 같이 끝남 |
| FocusPose | `FocusWatchDirector.cs` | `if (!IsFocusPoseState(evt.From)) return;` 뒤 해제 | FocusStart · FocusComplete · FocusCancelled(시간 상태) | 아니오 — 같이 끝남 |
| TodoReminder | `TodoReminderDirector.cs` | `if (evt.From != StickmanStateId.TodoReminder) return;` 뒤 해제 | TodoReminder(시간 상태) | 아니오 — 같이 끝남 |
| Sulky | `StressGaugeDirector.cs` | `if (evt.From == StickmanStateId.Sulky && evt.To != StickmanStateId.Sulky)` 안에서 해제 | Sulky(시간 상태) | 아니오 — 같이 끝남 |
| Dance | `DanceEpisodeDirector.cs` | `if (evt.From != StickmanStateId.Dance) return;` 뒤 해제 | Dance | 아니오 — 같이 끝남 |
| Archery | `ArcheryDirector.cs` | `if (evt.From != StickmanStateId.Archery) return;` 뒤 해제 | Archery | 아니오 — 같이 끝남 |
| Graffiti | `GraffitiDirector.cs` | `if (evt.From != StickmanStateId.Graffiti) return;` 뒤 해제 | Graffiti(시간 상태) | 아니오 — 같이 끝남 |
| WindowTheft | `WindowTheftDirector.cs` | `if (evt.From != StickmanStateId.WindowTheft) return;` 뒤 해제 | WindowTheft(`WindowTheftState`) | 아니오 — 같이 끝남 |
| DesktopTidy · BlackholeSummon | `DesktopIconMirrorDirector.cs` | `if (evt.From != TargetStateId) return;` 뒤 해제 | 각각의 시간 상태 | 아니오 — 같이 끝남 |
| **WindowCrash** | `WindowCrashDirector.cs` | 상태 전이가 아니라 **금의 수명 끝** — `FinishAndCooldown`의 `SpectacleEventLock.Release(this);`(완료 · 취소) | WindowCrash(시간 상태 `windowCrashSwingDuration: 0.4`, `DefaultStickConfig.asset:184`) | **예 — 약 2.6초.** 금 수명 `windowCrashOverlayDurationSeconds: 3`(`:185`)에서 스윙 0.4초를 뺀 동안, 캐릭터는 Idle로 돌아와 걷는데 락은 남는다 |

- 계수 기준: 파일은 `TryAcquire(` 호출이 있는 프로덕션 파일, 종류는 그 파일들이 넘기는 `SpectacleEventKind` 값이다. 리더 전달의 「11종」과 수가 다르다. 셈의 경계(예: 드래그·로데오를 연출로 볼지)가 다른 것으로 보이며, 어느 쪽이 맞는지는 판정하지 않았다.
- **보유자 12파일 중 상태보다 락이 오래 남는 것은 크랙 하나다.** 나머지는 상태를 벗어나는 전이에서 락을 놓는다.

**느슨함 (2판 기록)**
1. **크랙 금이 남은 약 2.6초**: 규칙 4를 「락 또는 상태」로 두면 헤더가 걷는 캐릭터에게 `지금은 다른 일 하는 중이에요`를 띄운다. 다른 세 칸의 `지금 창 부수기 중이에요`(`BusyText(ActiveKind)`)도 같은 느슨함이다. 칸 문구 쪽은 이 문서 범위 밖이라 **고치지 않고 목록으로만 둔다**(§4).
2. **DragAndThrow**: 락 종류가 `DragAndThrow`면 칸은 `지금 붙잡혀 있는 중이에요`인데 헤더는 규칙 4다. 붙잡힘은 「하는 일」이라기보다 당하는 일이라 조금 느슨하다. 거짓은 아니라서 바꾸지 않는다.

**판정 — 규칙 4를 「상태만」으로 좁힌다**
- **원칙 1**: 「다른 일 하는 중」은 캐릭터 상태를 주장한다. 락은 **앱의 배타 토큰**이다. 크랙처럼 캐릭터 행동이 끝난 뒤에도 연출 수명 동안 남을 수 있다. 캐릭터 상태를 말하는 문장의 근거로는 상태 기계가 참에 더 가깝다.
- **바뀌는 장면은 크랙 금이 남은 약 2.6초 하나다**(위 표). 다른 11종은 락과 상태가 같은 전이에서 끝나므로 결과가 같다. DragAndThrow는 상태가 더 길어서 「상태만」으로도 바쁨이 유지된다.
- **좁혔을 때 그 2.6초의 헤더**: 규칙 5 `지금은 시킬 수 있는 게 없어요`. 캐릭터에 대해 아무 주장도 하지 않으므로 참이다. 칸들이 `지금 창 부수기 중이에요` · `아직 금이 안 사라졌어요`로 이유를 보이므로 정보도 잃지 않는다.
- **넓게 두는 쪽의 이점**(락이면 무조건 「바쁨」)은 칸 문구와 헤더가 같은 「중」을 말한다는 일관성뿐이다. 그 일관성이 참이 아닌 문장을 하나 더 만든다.
- 락을 잡은 직후 같은 호출 안에서 `ChangeState`가 이어지므로, 「락은 잡혔는데 상태는 아직 Idle」인 프레임은 헤더 갱신 사이에 생기지 않는다(프로덕션 `TryAcquire(` 호출 17곳 · 12파일 모두, 같은 메서드 안에서 2–28줄 뒤에 `ChangeState`가 오고 그 사이에 `yield`가 없다 — 판독 스크립트. 그 사이의 조기 반환은 락 획득 실패 블록 안이거나, 잡은 락을 돌려주고 나가는 경로(`DragThrowController.cs` 143–144)뿐이다. `FocusWatchDirector.cs`는 전이가 착지하지 않으면 그 자리에서 락을 돌려준다).

---

## 4. 구현 인계 (리더 경유 — 직접 지시 아님)

- **파일**: `Interaction/ActionCommandPopover.cs` 한 파일. 바쁨 계산을 `SetStatusCaption` 안에 둘지 인자로 넘길지는 구현 몫이다. `RefreshContent`와 같은 프레임에서 계산해야 한다.
- **문자열 상수화**: 헤더 문구는 지금 리터럴이다(`:767-770`). 규칙 4 · 5 문구를 `const`로 올려 테스트가 **참조**하게 한다(CLAUDE.md 니들 규칙).
- **바쁨 식**: `Agent.Blackboard.Machine.CurrentStateId`가 Idle·Walk가 아님. **읽기만** 하므로 `StickmanAgent.cs`는 바뀌지 않는다(§5). `Agent`나 상태 기계가 null이면 바쁘지 않은 것으로 친다(§0).
- **테스트 요구**(test-engineer):
  - (i) FTT + 캐릭터 Idle + 네 칸 전부 비바쁨 사유 → 헤더 = 규칙 5 상수.
  - (ii) 캐릭터 상태가 바쁨(예: 떨어지는 중, 또는 활쏘기 진행 중) → 규칙 4 상수.
  - (iii) 음성 대조: 한 칸이라도 Ready → `지금 시킬 수 있어요`.
  - (iv) 소스 감사: 네 감독 `GetAvailability` 본문에 상태 식이 **있다**는 존재 단언. 부재 단언으로 짜면 니들이 썩어도 초록이 된다.
  - (v) ★ 2판: 크랙 발동 뒤 스윙이 끝나고(`windowCrashSwingDuration` 뒤) 금이 남은 동안 → 헤더 = 규칙 5 상수. 스윙 동안에는 규칙 4다. 대기는 벽시계 기준이고, 두 경계는 상수를 참조한다.
- **목록만(고치지 않음)**: 크랙 금이 남은 동안 다른 세 칸의 `지금 창 부수기 중이에요`(락 이름에서 파생)도 같은 느슨함이다. 칸 문구 판정은 별도 배정이 필요하다.
  - ★ **2026-09-26 — 그 배정이 와서 확정됐다.** 형제 3칸의 문구는 `금이 사라지면 할 수 있어요`(122.0 N / 157.5 C, 타일 한도 372)로 바뀐다. 구조·불변식은 `docs/UX_WIDGETS.md` §4-6, 글자 확정과 기준 1–7 판정은 `docs/narrative/ACTION_POPOVER_TILE_AND_FOOTER_WORDING.md` §2다. **헤더 5단은 건드리지 않는다** — 이 문서의 판정은 그대로 유효하고, 바뀌는 것은 칸 쪽 글자 하나뿐이다.
- **7-b와의 관계**: 그라피티·창 도둑이 S 가드를 받으면 규칙 5가 쓰이는 장면이 늘어난다. 7-b 그라피티 라운드(coder-ui 러너 줄)와 **같은 라운드**에 넣기를 권한다. 창 도둑 가드는 E-3 뒤지만, 이 헤더 변경은 그 파일을 건드리지 않는다.

---

## 5. N-8 — 구현 파일이 목록 밖인가

`docs/verify/DISPLAY_CHANGE_PATH_FILES.md`의 `DCP-LIST` 블록을 경로 단위로 파싱했다(46행 = A 16 · B 8 · C 6 · D 7 · E 4 · X 5, 문서 표기와 일치).

| 구분 | 경로 | 등급 | 파일 존재 |
|---|---|---|---|
| 대상 | `Assets/_Project/Scripts/Interaction/ActionCommandPopover.cs` | **없음** | 예 |
| 대상(참고) | `Assets/_Project/Scripts/Interaction/PopoverPanel.cs` · `Core/CommandAvailability.cs` · `Core/StickMateDisplayNames.cs` | 없음 | 예 |
| 읽기만 함 | `Assets/_Project/Scripts/Core/StickmanAgent.cs`(상태 기계) | B | 예 |
| 양성 대조 | `Interaction/WindowTheftDirector.cs` · `Core/SpectacleEventLock.cs` | B · B | 예 |
| 음성 대조 | 존재하지 않는 경로 | 없음 | 아니오 |

- ⇒ **헤더 변경은 N-8 목록 밖이다.** 「E-3 결속 해제 뒤」 표기가 붙지 않는다.
- `StickmanAgent.cs`는 B등급이지만 N-8은 **파일 diff 단위**다. 헤더는 에이전트의 상태를 읽기만 하므로 목록 파일의 diff는 0이다. 목록 문서의 제외 규칙도 `잠금·서비스를 호출만 하는` 파일을 제외로 적는다.

---

## 6. 폭 (§5 모형, 교정값이 추정이라 **전부 추정**)

모형 N은 한글 1.00F · 공백 0.30F · 라틴과 기호 0.55F, 모형 C는 글자 수 × 10.5pt, 글꼴 10이다. 교정값 63.0은 `1f7e139` 주석의 추정이라 화면 실측이 아니다. 라틴 가정은 신뢰도가 낮다.

| 문자열 | 글자 | N | C | 352pt |
|---|---:|---:|---:|---|
| (지금) `지금은 다른 일 하는 중이에요` | 16 | 132.0 | 168.0 | OK |
| (지금) `지금 시킬 수 있어요` | 11 | 89.0 | 115.5 | OK |
| **H1** `지금은 시킬 수 있는 게 없어요` | 17 | **135.0** | **178.5** | **OK** |
| H2 `지금은 시킬 수 있는 게 없어요 — 칸마다 이유가 있어요` | 31 | 242.5 | 325.5 | OK |
| English `Nothing available right now` | 27 | 141.0 | — | OK(신뢰도 낮음) |

---

## 7. 안 본 것 · 미확인

- 실제 빌드 캡처 0. 요청 두 장: FTT에서 명령창을 열고 네 칸이 모두 회색일 때의 헤더, 크랙 금이 남은 동안의 헤더.
- FTT에서 창 도둑 · 활쏘기가 E로 막히는 빈도는 측정하지 않았다. 그라피티의 macOS 동작도 미확인이다(판독 인용).
- `Agent`와 `_player`가 같은 인스턴스라는 것은 부트스트래퍼 코드 판독이다. 저장된 씬 · 프리팹의 실제 참조는 보지 않았다.
- 락 보유자 표는 해제 줄 판독이다. 각 상태가 실제로 몇 초 이어지는지(크랙 제외)는 재지 않았다.
- 구현 전이라 헤더 규칙은 코드에 없다. 이 문서는 설계다.
- 영어 원어민 검토 0. 프로덕션 영어 UI 경로는 아직 없다.

## 8. 플랫폼 영향

- **Windows 영향: 없음(문서만).** 헤더 · 네 감독 · 락 보유자 · 상태 기계는 공통 코드다(`#if` 분기 없음). 그라피티가 Windows 등급 1에서 코드상 대부분 막히므로, 규칙 5가 쓰이는 장면은 Windows에서 더 자주 닿을 것으로 본다(판독 인용, 실기 미확인). 창 도둑 가드(7-b)는 E-3 뒤지만 헤더 변경과 파일이 겹치지 않는다.
- **macOS 영향: 없음(문서만).** 같은 공통 코드다. 등급 1에서는 우클릭 → 부채꼴 → [행동]이 사실상 유일한 입구라 FTT 장면이 가장 자주 닿는다. 그라피티의 등급 1 동작은 미확인이다.

---

## 9. 초판(미커밋) 대비 정정 기록 — verify-change 조건부 통과(C4 · 권고) 반영

| 위치 | 초판 문장 | 2판 | 근거 |
|---|---|---|---|
| 머리말 | 인용 `.cs` 목록에 `PopoverPanel.cs` · 락 보유 12파일 · `SceneBootstrapper.cs` 없음 | 추가, 「2판」 한 줄 추가 | 인용 범위가 넓어짐 |
| §0 규칙 4 | 「`readyCount == 0` 이고 캐릭터가 바쁨 — `SpectacleEventLock.IsActive` 또는 현재 상태가 Idle·Walk가 아님」 | 「캐릭터 상태가 바쁨 — 현재 상태가 Idle·Walk가 아님」 | C4 — 크랙 금 약 2.6초 동안 락만 남는다(§3-5) |
| §0 규칙 5 | 「그 밖 — `readyCount == 0`, 캐릭터는 바쁘지 않음」 | 「캐릭터 상태는 Idle·Walk」 | 규칙 4 변경에 딸림 |
| §0 불릿 | 「4의 「바쁨」은 네 감독이 바쁨 사유를 만드는 두 식과 같은 식이다」(락 식 · 상태 식) · 「상태 기계가 배선되지 않았으면 … 5의 중립 문구가 참이다」 | 상태 식 하나로 줄이고, 락을 보지 않는 까닭과 `Agent == null` 규정을 추가 | C4 · 권고(`Agent == null`) |
| §1-1 | (없음) | 「헤더의 `Agent`와 감독의 `_player`는 같은 인스턴스인가」 신설 | 권고 — `PopoverPanel.cs:213` |
| §1-2 순서 불릿 | 「캐릭터가 바쁘면(락 또는 상태), 앞선 M·H·S에 걸리지 않은 칸은 전부 C 사유를 보인다」 | 「캐릭터 상태가 바쁘면 … C 사유(락 또는 상태)」 + 락 문구가 먼저 보일 수 있다는 단서 | 규칙 4 변경에 딸림 |
| §2 표 | 「금이 아직 떠 있음 — 참 — 4, 같은 문구」 한 행 | 스윙 0.4초 행(4)과 금이 남은 약 2.6초 행(느슨함 → 5) 두 행으로 나눔 | C4 |
| §3-1 | 「캐릭터 상태, 즉 락과 상태 기계에서 나온 참 값이 있을 때만 쓴다」 | 「캐릭터의 상태 기계에서 나온 참 값」 | C4 |
| §3-2 | 「헤더가 주장하는 사실(캐릭터가 바쁘다)을 사유와 같은 두 식에서 직접 읽으면」 · 「네 감독의 바쁨 식이 바뀌는 날」 | 「같은 상태 식」 · 「상태 식」 | C4 |
| §3-4 | 기준 1–6 · 표에 기준 7 칸 없음 | 기준 7(지시 대상) 칸 추가 | 사유 문서 §2 기준 7 신설(C3)에 맞춤 |
| §3-5 | 「알고 두는 느슨함」: DragAndThrow 한 항목 | 락 보유자 전수 표 · 크랙 약 2.6초 느슨함 · 규칙 4 좁힘 판정 신설 | C4 |
| §4 | 바쁨 식 「`SpectacleEventLock.IsActive`와 … Idle·Walk가 아님」 · `StickmanAgent.cs`와 `SpectacleEventLock.cs`는 바뀌지 않는다 · 테스트 (ii) 「락 활성(예: 활쏘기 진행 중)」 · (iv) 「바쁨 두 식」 | 상태 식 · `StickmanAgent.cs`만 · (ii) 상태 바쁨 · (iv) 상태 식 · (v) 크랙 금 창 신설 · 칸 문구 느슨함 목록 | C4 |
| §5 | 「읽기만 함 — `SpectacleEventLock.cs` B」 · 양성 대조 `WindowTheftDirector.cs` · `StickmanAgent.cs` | 읽기만 함 `StickmanAgent.cs` · 양성 대조 `WindowTheftDirector.cs` · `SpectacleEventLock.cs` | 헤더가 락을 읽지 않게 됨. 등급 파싱 결과 자체는 초판과 같다 |
| §7 | 캡처 한 장 | 두 장 · 인스턴스 실측 미확인 · 락 보유자 상태 길이 미측정 추가 | C4 · 권고 |
| §4 「목록만(고치지 않음)」 | 2판까지 그 줄은 `칸 문구 판정은 별도 배정이 필요하다.`로 끝났다(그 글자는 **그대로 남아 있다** — 지운 글자 0, 아래 한 줄을 **덧붙이기만** 했다) | ★ 2026-09-26 — 그 배정이 와서 확정된 사실(형제 3칸 = `금이 사라지면 할 수 있어요`)과 새 정본 경로를 가리키는 하위 항목 한 줄을 삽입 | `design-narrative` 칸·푸터 문구 확정 라운드. 구조는 `docs/UX_WIDGETS.md` §4-6, 글자 확정은 `docs/narrative/ACTION_POPOVER_TILE_AND_FOOTER_WORDING.md` §2. **이 문서의 헤더 5단 판정은 바뀌지 않았다** |
