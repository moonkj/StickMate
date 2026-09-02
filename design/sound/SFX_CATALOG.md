# StickMate 효과음 카탈로그 · 팩별 사운드 사양

작성 2026-09-02 · `design-sound` · **오디오 파일 0개.** 이 문서는 사양이고, 조달은 리더 결정(D3).
전제: `design/sound/SILENCE_POLICY.md`를 먼저 읽어야 한다. **정책이 이 표보다 항상 위에 있다.**

---

> ## ★ R2 정정 (2026-09-02)
> **§1-C(티어 C 3키)·§1-D(레벨업)는 폐기됐다. §3-2의 팩별 교체 6키 목록도 전부 다시 짰다**
> (그 목록이 방금 죽은 `land.ambient`/`parkour.climb`/`ledge.hang`에 의존하고 있었다).
> 새 정본은 `design/sound/SOUND_QUALIFICATION.md` §3-5 · §7.
> **§0 카탈로그 규칙 5개, §2 음색 방향, §4 클립 제작 사양은 그대로 유효하다.**


## 0. 카탈로그 규칙 5개

1. **소리는 새 이벤트를 만들지 않는다.** 이미 확정된 상태 전이/이벤트만 구독한다
   (절대 불변 원칙 1의 사운드판 — 소리를 먼저 정하고 이벤트를 끼워 맞추지 않는다).
2. **소리는 값을 다시 계산하지 않는다.** 세기·결과가 이벤트에 실려 오면 그걸 쓴다.
   (`ArcheryShotEvent`가 `ImpactWorld`를 실어 보내 렌더러와 상태가 어긋날 수 없게 만든 그 방식.)
3. **모든 항목에 `시각 신호` 열이 채워져야 출하된다.** 비면 그 소리는 금지(정책 §4-2).
4. **격파 놀이 사운드는 설계하지 않는다.** 그 기능은 2026-09-02 삭제됐다
   (`SpectacleEventLock.cs:7` — `BattleMinigame` 제거).
5. **팩은 키를 추가할 수 없다.** 음색만 바꾼다(§3).

---

## 1. 기본 세트 18키

`오프셋`은 마스터 대비 dB. 출력 피크 실값은 `sound_policy_calc.out.txt` §2.
`티어`: A=사용자 직접 / B=사용자 유발 / C=자율 / M=성취.

### 1-A. 티어 A — 사용자가 직접 부른 것

| 트리거 키 | 구독할 기존 신호 | 오프셋 | 길이 | 시각 신호(필수) | 상태 |
|---|---|---:|---:|---|---|
| `sfx.ui.preview` | 설정창 [미리듣기] 버튼 | 0 dB | 400 ms | 버튼 눌림 상태 | **가능** |
| `sfx.archery.draw` | `ArcheryShotPhase.Aim` | -9 dB | 400 ms | 시위 당기는 자세 + 조준선 | **가능** |
| `sfx.archery.release` | `ArcheryShotPhase.Release` | -6 dB | 300 ms | 화살이 실제로 날아간다 | **가능** |
| `sfx.archery.miss` | `ArcheryShotResult.Miss` | -9 dB | 300 ms | 과녁 앞 땅에 꽂힘 + 흙먼지 | **가능** |
| `sfx.archery.hit` | `ArcheryShotResult.Hit` | -5 dB | 300 ms | 바깥 링에 꽂힘 | **가능** |
| `sfx.archery.bullseye` | `ArcheryShotResult.Bullseye` | -3 dB | 400 ms | 정중앙에 꽂힘 | **가능** |
| `sfx.focus.start` | `StickmanStateId.FocusStart` | -4 dB | 400 ms | 안경+팔짱 포즈 + 타이머 링 | **가능** |
| `sfx.focus.complete` | `StickmanStateId.FocusComplete` | -2 dB | 800 ms | 완료 포즈 + 링 소멸 | **가능** |
| `sfx.equip.wear` | 장비 변경 이벤트(`StickmanEventBus.cs:618`) 착용 | -8 dB | 300 ms | 캐릭터에 아이템이 붙는다 | **가능** |
| `sfx.equip.remove` | 같은 이벤트, 해제 | -10 dB | 300 ms | 아이템이 사라진다 | **가능** |
| `sfx.shop.purchase` | (없음) | 0 dB | 400 ms | 동전 수치 감소 + 소유 표시 | **선행 미충족** — 상점 미구현(`ItemCatalog.cs:263`) |

**소리를 일부러 만들지 않는 것 — `StickmanStateId.FocusCancelled`**
집중 세션 중도 취소는 사용자의 정당한 선택이다. **사용자를 나무라는 소리를 만들지 않는다.**
`StickConfig.cs`가 취소 포즈를 *"패널티 없는 톤"*으로 규정한 것과 같은 판단이다.

### 1-B. 티어 B — 사용자가 원인이지만 결과가 몇 초 뒤

| 트리거 키 | 구독할 기존 신호 | 오프셋 | 길이 | 시각 신호(필수) | 비고 |
|---|---|---:|---:|---|---|
| `sfx.rodeo.grab` | `StickmanStateId.RodeoCursor` 진입 | -13 dB | 250 ms | 커서에 매달린 캐릭터 | |
| `sfx.ragdoll.impact` | `StickmanStateId.ThrowTumble` 최초 충격 | -12 dB | 250 ms | 래그돌 구르기 | 구르는 동안 **반복 재생 금지**. 진입 1회 |
| `sfx.land.throw` | `StickmanStateId.LandingCrouch` (직전이 `Dragged`/`ThrowTumble`) | -12 dB | 250 ms | 무릎앉기 + 흙먼지 | ★ 아래 |

★ **`sfx.land.throw`의 세기는 새로 계산하지 않는다.**
`LandingDustRenderer.LastIntensity`(0~1)를 **그대로 읽는다**. 그 값은
`ComputeIntensity(fallHeight, cfg, height)`가 무릎앉기 깊이 램프와 **같은 식**으로 계산해 둔 것이다
(`LandingDustRenderer.cs:104-138`). 볼륨 = 오프셋 + `Lerp(-8 dB, 0 dB, intensity)`.

> 이 저장소는 *"같은 값을 두 곳에서 따로 계산해 어긋난 전례가 2회 있다"*고 스스로 적어 뒀다.
> 소리가 세 번째가 되지 않게 한다. 먼지가 옅으면 소리도 작다 — 자동으로.

### 1-C. 티어 C — 자율 (별도 스위치, 기본 OFF, 시간당 6회 상한)

| 트리거 키 | 구독할 기존 신호 | 오프셋 | 길이 | 시각 신호(필수) |
|---|---|---:|---:|---|
| `sfx.land.ambient` | `LandingCrouch`(직전이 `Fall`/`Jump`) | -14 dB | 200 ms | 무릎앉기 + 흙먼지 |
| `sfx.parkour.climb` | `StickmanStateId.ParkourClimb` 진입 | -14 dB | 250 ms | 기어오르는 동작 |
| `sfx.ledge.hang` | `StickmanStateId.LedgeHang` 진입 | -16 dB | 250 ms | 모서리에 매달린 자세 |

**이 셋뿐인 이유**: 배포 기본 설정에서 자율 확률이 살아 있는 이동은 이것들뿐이다
(`stepUpChance = 0.85` 등). 창도둑·그라피티·청소부·블랙홀·크래시·활쏘기·투두는
확률이 전부 **0**이라(정책 F6) 자율로는 발생하지 않고, 단축키로 부르면 **티어 A**로 들어온다.

**`GroundLossHang`에는 소리를 붙이지 않는다.** 발판이 사라져 매달리는 것은
사용자가 창을 닫았기 때문에 일어나는데, 그 순간 소리가 나면
**"내가 창을 닫았더니 앱이 반응했다"**로 읽혀 원칙 3(유저 자산 불변)에 대한 오해를 만든다.

### 1-D. 티어 M — 성취 (별도 스위치, 기본 OFF, 시간당 2회)

| 트리거 키 | 구독할 기존 신호 | 오프셋 | 길이 | 시각 신호(필수) | 상태 |
|---|---|---:|---:|---|---|
| `sfx.progress.levelup` | `CharacterProgressionDirector` 레벨업 | -1.5 dB | 800 ms | **없음 — `Debug.Log` 한 줄뿐** | **★ 보류(Blocked)** |

`CharacterProgressionDirector.cs:170`이 유일한 신호이고 그건 사용자에게 보이지 않는다.
소리를 붙이면 **레벨업을 소리만이 알려주게 되어** 정책 §4-1을 정면 위반한다.
**시각 신호가 먼저 생겨야 한다**(리더 → `ux-designer` 배정 사안, 정책 D4).

---

## 2. 음색 방향 — 기본 세트 (팩 미소유 사용자가 듣는 소리)

**컨셉: "종이 위의 연필"** — 이 캐릭터는 선으로 그려진 졸라맨이고, 기본 팔레트는 잉크다.
소리도 **재료가 있어야 한다**: 종이, 흑연, 나무. 전자음이 아니다.

| 키 | 음색 한 줄 | 피해야 할 것 |
|---|---|---|
| `land.*` | 종이에 연필심이 툭 닿는 소리 + 아주 짧은 종이 스침 | 쿵/붐 — 저역은 80 Hz 하이패스에서 어차피 잘린다 |
| `parkour.climb` | 흑연이 종이를 짧게 긁는 소리(위로 미끄러지는 피치) | 스크래치가 길면 불쾌하다. 250 ms 상한 |
| `ledge.hang` | 종이 모서리가 살짝 눌리는 소리 | |
| `archery.draw` | 나무·실이 팽팽해지는 소리, **피치 상승** | 실제 활 소리의 저역 |
| `archery.release` | 짧은 튕김 + 공기 스침 | 슬로모션 같은 잔향 |
| `archery.hit/bullseye` | 목표에 꽂히는 짧은 "톡" — bullseye는 **한 음 높고 배음 하나 추가** | 팡파레. 400 ms 안에 끝나야 한다 |
| `focus.start` | 연필을 책상에 놓는 소리 | 알람·비프(금지 목록) |
| `focus.complete` | 짧은 상승 3음(온음계, 종결감) | 승리 팡파레. 800 ms 상한 |
| `equip.wear/remove` | 옷감이 스치는 소리, remove는 그것을 **반전** | 금속 찰칵 |
| `shop.purchase` | 종이 영수증 + 낮은 동전 하나 | 동전 무더기(카지노처럼 읽힌다) |
| `progress.levelup` | 상승 4음 + 종이 팔랑 | 게임 레벨업 팡파레 |
| `ui.preview` | `focus.start`와 동일 클립 재사용 | 전용 클립을 새로 만들 이유가 없다 |

---

## 3. 팩별 전용 사운드 (DLC 6팩)

`docs/ARCHITECTURE.md` 5-3-2 (A)의 `sounds : (트리거키, AudioClip)[]` 필드를 쓴다.

### 3-1. ★ 팩 사운드 3원칙

1. **팩은 트리거 키를 추가할 수 없다.** §1의 18키 중 일부의 **음색만** 교체한다.
   → 원칙 4(기본 로직 무수정)를 지키면서, **DLC가 새 소음을 밀반입하는 경로를 구조적으로 차단**한다.
2. **팩당 최대 6키.** 팩 정체성은 6개면 충분하고, 상한이 메모리 예산(1.75 MB)을 고정한다.
3. **팩은 볼륨·티어·예산을 지정할 수 없다.**
   → **`ARCHITECTURE.md`의 스키마는 이미 옳다** — `sounds` 필드에 볼륨 항목이 없다.
   **볼륨 필드를 추가하지 마라.** 추가하는 순간 "돈 낸 팩이 더 시끄러운" 구조가 되고,
   그건 되돌릴 수 없는 스키마 결정이다(`game-architect` 인계 사항).

### 3-2. 팩별 음색 (교체 6키 제안)

| 팩 | 재료 | 교체 6키 | 금지 |
|---|---|---|---|
| **오피스 워커** `pack.office` | 종이·스테이플러·키캡·도장 | land / parkour / focus.start / focus.complete / equip.wear / purchase | 실제 사무실 전화벨(금지 목록) |
| **사이버 아포칼립스** `pack.cyber` | 짧은 디지털 글리치, 필터 스윕 | land / parkour / archery.release / archery.bullseye / equip.wear / levelup | 경보음·에러 비프(알림으로 오인) |
| **네온 낙서** `pack.graffiti` | 스프레이 캔 쉿, 마커 스퀵, 캡 딸깍 | land / parkour / ledge.hang / archery.release / equip.wear / purchase | 캔 흔드는 소리 반복(길다) |
| **스포츠** `pack.sports` | 공 튀김, 스니커 스퀵, 네트 스침 | land / parkour / ledge.hang / archery.bullseye / focus.complete / levelup | **호루라기**(경기장 반사, 사람이 돌아본다) |
| **컬러 잉크** `pack.ink` | 물감 방울, 붓, 잉크 번짐 | land / parkour / archery.release / equip.wear / focus.start / levelup | — (6팩 중 **가장 조용한 팩**. 전 키 추가 -3 dB) |
| **밀리터리** `pack.military` | 금속 버클, 무전 클릭, 캔버스 천 | land / parkour / equip.wear / equip.remove / focus.start / focus.complete | ★ **총성·폭발·무전 음성 절대 금지** |

★ **밀리터리 팩의 금지가 이 표에서 가장 중요한 줄이다.**
총성은 공공장소·회의·가족이 있는 방에서 **되돌릴 수 없는 사고**를 만든다.
이 팩의 정체성은 **화약이 아니라 장비의 질감**(버클·천·금속)으로 표현한다.
`archery.*`를 이 팩에서 교체 대상에서 **뺀 것도 같은 이유**다 — 밀리터리 문맥의 발사음은
반드시 총성 쪽으로 끌려간다.

### 3-3. 팩 사운드도 정책을 통과해야 한다

팩 클립은 §1의 키에 **꽂히기만 할 뿐**, 그 키의 티어·오프셋·예산·묵음 조건을 그대로 상속한다.
즉 **팩을 사왔다고 회의 중에 소리가 나지 않는다.** 게이트는 하나뿐이다(정책 §3-8).

### 3-4. 미소유 팩

미소유 팩의 클립은 **로드하지 않는다**(Addressables 주소 기반, `assetKey` 필드 존재).
전 팩 소유 최악값 1.75 MB 중 실사용은 기본 614 KB + 소유 팩분뿐이다.

---

## 4. 클립 제작 사양 (조달·제작 공통)

| 항목 | 값 |
|---|---|
| 채널 | **모노 1채널** (`spatialBlend = 0`, `pan = 0` 강제) |
| 샘플레이트 | 48,000 Hz |
| 비트 | 16-bit PCM (원본은 24-bit로 작업 후 디더링) |
| 트루피크 | **-1.0 dBTP** 전 클립 공통 |
| 하이패스 | 80 Hz |
| 3.5~4 kHz | 광대역 대비 -6 dB 이하 |
| 길이 | 원샷 ≤400 ms / 성취 ≤800 ms |
| 페이드아웃 | 마지막 20 ms 필수(클릭 노이즈 방지) |
| **페이드인** | **첫 1.5 ms 필수, 첫 샘플 = 0** ★R4 (온디맨드 개방 시 갓 열린 스트림의 DC 스텝 방지 — `SILENCE_POLICY.md` §11-3) |
| 무음 여백 | 앞 0 ms(어택이 바로) / 뒤 0 ms — ★R4에서 근거 추가: 앞 여백이 있으면 장치 개방 클릭이 소리와 **융합되지 못하고** 홀로 들린다 |
| Unity 임포트 | Decompress On Load, Force To Mono ON, Preload Audio Data **OFF**, Load In Background ON |
| 파일명 | 트리거 키 그대로 + 팩 접미사 — `sfx.land.ambient.ink.wav` |

**Preload Audio Data를 끄는 이유**: 24시간 상주 앱에서 **한 번도 안 날 수 있는 소리**(기본 OFF!)를
부팅 시 전부 메모리에 올릴 이유가 없다. 마스터가 켜진 뒤 첫 재생 시 로드한다.

---

## 5. 인계 (리더 경유 — 나는 직접 지시하지 않는다)

| 받는 쪽 | 내용 | 왜 이 사람인가 |
|---|---|---|
| `ux-designer` | (a) 설정창 소리 행 배치 = [접근성·성능] + [이벤트] (정책 §6-2, `SettingsWindow.cs:62`의 "[일반]은 항상 넘친다" 실측 근거) · (b) **"지금 소리: 꺼짐 — 사유"** 한 줄 표시 · (c) **레벨업 시각 신호**(§1-D 보류의 해제 조건) | UI 표면 담당 |
| `game-architect` | `StickPackManifestSO.sounds`에 **볼륨 필드를 넣지 말 것**(§3-1-3). 넣으면 되돌릴 수 없는 스키마 결정이 된다 | 되돌릴 수 없는 결정 식별 |
| `dev-platform` | M9(마이크)/M10(방해금지) 조회 2건의 실현성. **판정은 `Platform/` 중립, 조회만 플랫폼 파일** | `Platform/` 전체 담당 |
| `design-art` | 6팩 음색(§3-2)이 각 팩 팔레트/테마와 같은 이야기를 하는가 | DLC 6팩 테마 통일 |
| `design-motion` | `sfx.land.*`가 `LandingDustRenderer.LastIntensity`를 읽는 것이 무릎앉기 6티어 램프와 어긋나지 않는가 | 착지 박자·무게감 소유자 |
| `test-engineer` | 정책 §8의 검증 8건. **3번(음성 대조)이 빠지면 나머지 7건의 초록은 무의미하다** | 기능 검증 |
| `perf-doc` | `pmset -g assertions` 게이트 + DSP 버퍼 512→1024 실측 | 성능 |
