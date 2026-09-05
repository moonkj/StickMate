# 미해결 결함 2건 규명 — 팔다리 충돌 경로 / 한 프레임 이름 중복

> 작성: `debugger` · 2026-09-05 · **진단만. 프로덕션 `.cs` 한 줄도 수정하지 않았다**(리더 지시).
> 이 문서의 모든 숫자는 이번 라운드에 파일을 직접 읽어 다시 잰 것이다. 인용은 전부 `파일:줄`.
> 실기(Unity) 실행 없이 소스·프리팹 YAML·프로젝트 설정만으로 판정했다 — **실기 미확인 항목은 그렇게 표시했다.**

> ### ★ 인용 기준 (동시 라운드 주의)
> 줄번호는 **2026-09-05 작업 트리**(= `coder`의 미커밋 변경 포함) 기준이다.
> `Interaction/CharacterAccessoryRenderer.cs`는 이번 라운드에 `coder`가 잡고 있어 **줄이 움직인다** —
> 실측 확인: `Rebuild()` `:828`과 `_container = new GameObject("EquipmentAccessories")` `:838`은
> **HEAD와 작업 트리가 동일**하지만, `lr.material = _lineMaterial`은 **HEAD `:972` → 작업 트리 `:1018`**로 46줄 밀렸다.
> ⇒ **줄번호가 안 맞으면 앵커 문자열로 다시 찾아라**(이 문서는 모든 인용에 원문 조각을 함께 적었다).
> 이번 라운드에 나는 `Assets/` 아래와 `Tasklist.md`를 **한 파일도 건드리지 않았다**(mtime 확인).

---

## 0. 한 줄 요약 (리더용)

| # | 결함 | 판정 | 심각도 | 가장 중요한 사실 |
|---|---|---|---|---|
| 1 | 팔다리 충돌 경로 8개 사망 | **확증. 다만 CLAUDE.md의 원인 기술은 절반만 맞다** | Major(정확성) / **Blocker(수정 착수 순서)** | 질량 불일치는 **세 관문 중 두 번째**다. 첫 관문(Kinematic 접촉)과 세 번째 관문(월드에 Dynamic 상대가 0개)을 그대로 두고 질량만 고치면 **출하 기본값에서 아무 일도 일어나지 않는다** |
| 2 | `Rebuild()` 한 프레임 이름 중복 | **확증(코드 사실). 단, 현재 프로덕션 소비자는 0명** | Minor(프로덕션) / Major(측정 신뢰성) | 화면에는 안 보인다. 피해는 **계층을 이름으로 읽는 관측자에게 죽어가는 쪽을 준다**는 것 — 지금은 테스트 7개 파일만 그 통로를 쓴다 |

---

# 【1】 팔다리 충돌 경로 8개 — 왜 죽어 있는가

## 1-1. 현상

`Core/RagdollLimbImpactRelay`가 붙은 파츠 8개가 물리 충돌로 RAGDOLL을 유발한 기록이 없다.
`CLAUDE.md`(캐릭터 무빙 방식 절)와 `States/RagdollImpactResolver.cs:40-45`는 원인을
**「질량 단위 불일치」 하나**로 적어 두었다.

## 1-2. 코드 근거 — 실측 재확인

### (a) 8개 경로를 특정했다

`Assets/_Project/Prefabs/Stickman.prefab`을 YAML 파싱해서 릴레이 부착 GameObject를 전수 조회했다.
스크립트 GUID `2ac7cc5aa599b44fc9b8e2ce2ebc58c9`(= `RagdollLimbImpactRelay.cs.meta`) 기준.

```
릴레이 인스턴스 수 = 8          (양성 대조: StickmanAgent GUID = 1건 검출 / 음성 대조: 가짜 GUID = 0건)
Rigidbody2D 총수  = 9          (릴레이 8 + 루트 1)
```

| # | GameObject | 질량 | bodyType(프리팹) | gravity | 레이어 | 릴레이 | **임계 8.0 도달 필요 상대속도** |
|---:|---|---:|---|---:|---:|:--:|---:|
| 1 | `LeftArm` | 0.06 | Kinematic | 3 | 8 | ✔ | **133.3** |
| 2 | `LeftArmLower` | 0.06 | Kinematic | 3 | 8 | ✔ | **133.3** |
| 3 | `RightArm` | 0.06 | Kinematic | 3 | 8 | ✔ | **133.3** |
| 4 | `RightArmLower` | 0.06 | Kinematic | 3 | 8 | ✔ | **133.3** |
| 5 | `LeftLeg` | 0.09 | Kinematic | 3 | 8 | ✔ | **88.9** |
| 6 | `LeftLegLower` | 0.09 | Kinematic | 3 | 8 | ✔ | **88.9** |
| 7 | `RightLeg` | 0.09 | Kinematic | 3 | 8 | ✔ | **88.9** |
| 8 | `RightLegLower` | 0.09 | Kinematic | 3 | 8 | ✔ | **88.9** |
| — | `Stickman`(루트) | **1.00** | **Dynamic** | 3 | 8 | ✘ | **8.0** |

⇒ **CLAUDE.md의 「0.06~0.09 / 88.9~133.3 / 8개」는 전부 참이다. 반증 없음.**

생산자: `Assets/Editor/SceneBootstrapper.cs:1300·1306`(다리 0.09) / `:1312·1318`(팔 0.06) /
`:2010`(`rb.mass = mass`) / `:2009`(`rb.bodyType = Kinematic`) / `:2041`(릴레이 부착) / `:763`(루트 1.0).

### (b) 질량 단위 불일치의 **정확한 지점**

두 호출부가 **같은 식**을 각자 갖고 있고, 곱하는 질량만 다르다.

```
Core/StickmanAgent.cs:562        ReportCollisionImpact(collision, collision.relativeVelocity.magnitude * _body.mass);   // _body = 루트, mass 1.00
Core/RagdollLimbImpactRelay.cs:41 _agent.ReportCollisionImpact(collision, collision.relativeVelocity.magnitude * _body.mass);  // _body = 팔다리, mass 0.06~0.09
```

두 값이 **같은 상수 하나**와 비교된다:

```
States/RagdollImpactResolver.cs:328   if (impulseMagnitude < blackboard.Config.ragdollForceThreshold) …   // 8.0
```

**불일치의 지점은 `:41`이 아니라 「`:562`와 `:41`이 서로 다른 질량을 곱해 놓고 같은 임계와 비교한다」는
관계 자체다.** 임계 8.0은 `StickConfig.cs:2169`가 명시하듯 **루트 질량 1.0을 전제로 유도된 값**이고
(`v = sqrt(2·9.81·3·h) = 8` → 낙하 1.09유닛), 그 전제가 `:41`에서만 깨진다.

도달 불가의 크기(전부 이번 라운드 계산):

| 바디 | 필요 상대속도 | 던지기 상한 `dragThrowMaxSpeed`=12.0 대비 | 그 속도를 자유낙하로 얻으려면(gravityScale 3) |
|---|---:|---:|---:|
| 팔(0.06) | 133.3 | **11.1배 부족** | **302.0유닛** 낙하 = 화면높이(24유닛)의 **12.6배** |
| 다리(0.09) | 88.9 | **7.4배 부족** | **134.2유닛** 낙하 = 화면높이의 **5.6배** |

설정 실측: `DefaultStickConfig.asset` → `ragdollForceThreshold: 8` / `dragThrowMaxSpeed: 12` /
`gravityScale: 3` / `characterScale: 0.75`. 화면 높이 = `2 × OrthographicSize(12)` = 24유닛
(`SceneBootstrapper.cs:396`).

> ★ **정정(2026-09-05, 실측): 위 표의 「12.0 대비」는 상한을 잘못 잡았다.** 러너 로그 실측 도달
> 상대속도는 **21.59**(자유낙하 가속이 더해진다). 「11.1배/7.4배 부족」이 아니라
> **실측 기준 4.1배 부족**이 정확하다. **도달 불가라는 결론은 그대로다** — 5-bis-2 참고.

### (c) ★ **그런데 질량 관문은 첫 번째가 아니다 — 관문이 셋이고, 앞뒤 둘은 아무도 안 적었다**

> **이것이 이번 라운드의 핵심 발견이다. 질량만 고치면 출하 기본값에서 관측 가능한 변화가 0이다.**

#### 관문 A — 능동 상태의 팔다리는 **Kinematic**이라 정적/키네마틱 상대와 접촉을 만들지 않는다

프리팹 실측(9개 바디 전수):

```
LeftArm … RightLegLower :  m_BodyType: 1 (Kinematic),  m_UseFullKinematicContacts: 0
Stickman(루트)          :  m_BodyType: 0 (Dynamic),    m_UseFullKinematicContacts: 0
```

`useFullKinematicContacts`는 **코드 어디에서도 설정하지 않는다**(전 저장소 grep 0건.
양성 대조: 같은 경로에서 `.bodyType = `는 프로덕션 11건 검출 — 프로브는 살아 있다).

Unity 2D 충돌 액션 매트릭스에서 `useFullKinematicContacts=false`인 Kinematic 바디는
**Dynamic 상대하고만** 접촉을 만든다. 정적 콜라이더/다른 Kinematic과는 접촉 자체가 생성되지 않는다
⇒ `OnCollisionEnter2D`가 호출되지 않는다.

`RagdollRig`가 이 bodyType을 상태에 따라 갈아끼운다:
- `States/RagdollRig.cs:190` — **능동 모드**: 팔다리 `bodyType = Kinematic`
- `States/RagdollRig.cs:335` — **RAGDOLL 모드**: 팔다리 `bodyType = Dynamic`

⇒ **팔다리가 물리 접촉을 받을 수 있는 유일한 구간은 「이미 RAGDOLL인 동안」이다.**
릴레이의 설계 목적(*"사지에 맞는 피격도 RAGDOLL 강제 전이의 단일 진입점으로"* —
`RagdollLimbImpactRelay.cs:9-12`)은 **능동 상태**를 겨냥하는데, 그 구간에서 콜백이 구조적으로 안 온다.

★ `SceneBootstrapper.cs:2002`의 주석이 이 사실과 정면으로 어긋나 있다:
> `segment.layer = limbLayer; // BUG-SW-M1: 자체충돌은 레이어 매트릭스가 끄고, **바닥 등과는 정상 충돌**.`

레이어 매트릭스는 실제로 허용한다(아래 확인). **막는 것은 레이어가 아니라 bodyType이다.**
이 주석 때문에 "레이어는 열려 있으니 충돌은 되겠지"라는 오독이 계속 재생산된다.

레이어 매트릭스 실측(`ProjectSettings/Physics2DSettings.asset` `m_LayerCollisionMatrix` 디코드):
```
layer 8 (StickmanLimb) 의 마스크만 0xfffffeff  →  8↔8 만 차단.  8↔0, 8↔2 는 전부 허용
```
`TagManager.asset:16` → layer 8 = `StickmanLimb`. 루트도 layer 8 (`SceneBootstrapper.cs:760`),
`PhysicsGround`/`DockPhysicsStep`은 layer 2 (`SceneBootstrapper.cs:1710·1738`).

#### 관문 B — 질량 (위 (b))

#### 관문 C — **월드에 Dynamic 상대가 하나도 없다**

관문 A를 뚫으려면 Dynamic 상대가 있어야 한다. 전수 조사 결과:

| 후보 | 실측 | 결론 |
|---|---|---|
| `PhysicsGround` / `DockPhysicsStep` | `Main.unity` 씬 문서 23개 중 `Rigidbody2D`(`!u!50`) **0건** / `BoxCollider2D`(`!u!61`) **2건**(각각 이 둘, layer 2). 양성 대조로 프로브 생존 확인 | **Static** |
| 루트 `Stickman` | Dynamic이지만 **layer 8** ⇒ 팔다리(layer 8)와 8↔8 차단 | 접촉 불가 |
| 라이벌 스틱메이트 | `RivalStickmanAgent` 소스 **존재하지 않음**(전 저장소 grep 0건, 문서에만 남음) | 없음 |
| 화살 / 농구공 / 펫 / FX | 프로덕션 `AddComponent<Rigidbody2D>`는 `SceneBootstrapper` 2곳(루트·팔다리)뿐 | 전부 렌더러 전용 |
| 씬의 나머지 프리팹 인스턴스 `UniWindowController`(패키지 `com.kirurobo.uniwinc`) | 패키지 원본 프리팹 실측 — `Rigidbody2D` 0건 / `Collider2D` 0건(문서 3개 중) | 없음 |
| 런타임 생성 콜라이더 6곳(`CharacterInfoWindow:1264`, `InfoGearIconWidget:1222`, `SettingsWindow:980`, `RunawayRenderer:467`, `TodoPostItWidget:1073`, `PopoverPanel:671`) | 전부 `Rigidbody2D` 없음 = Static | 접촉해도 관문 A에 막힘 |

*(양성 대조 27건 / 음성 대조 0건으로 프로브 생존 확인)*

⇒ **출하 빌드 전체에서 팔다리와 접촉을 만들 수 있는 Dynamic 바디는 0개다.**

#### 관문 D(부수) — Kinematic 팔다리의 `relativeVelocity`는 0이다

`States/StickmanPoseAnimator.cs:2196·2208`은 팔다리를 `Transform.localRotation` /
`localPosition` **직접 대입**으로 움직인다(`MovePosition` 아님). Transform으로 밀린 Kinematic 바디의
`Rigidbody2D.linearVelocity`는 0으로 남으므로, 설령 접촉이 생겨도 `relativeVelocity`는
**상대 쪽 속도만** 반영한다. 즉 「팔을 빠르게 휘둘러 때린다」는 경로는 물리적으로 존재하지 않는다.

### (d) 정본 목록(`RagdollImpactResolver` 클래스 문서) 대조 — 무엇이 실제로 살아나는가

`States/RagdollImpactResolver.cs:19-45`의 6항목을 이번 실측과 하나씩 맞췄다.

| 정본 항목 | 이번 실측 | 「팔다리 질량만」 고치면? |
|---|---|---|
| 커서로 거칠게 털어내기 (`RodeoCursorState`, ×1.25 강제) | **살아 있음.** 릴레이를 거치지 않는 직접 호출 | 변화 없음 |
| 「던짐」 폐지 (`throwTumbleEnabled: 1` 실측 확인) | 폐지 상태 유지 | 변화 없음. **건드리지 않는다** |
| 「추락 충격」 차단막 (`landingImpactRagdollShield: 1` 실측 확인) | 차단 상태 유지 | 변화 없음. **건드리지 않는다** |
| 루트 물리 충돌 | 살아 있음(질량 1.0 ⇒ 필요속도 8.0 ≤ 12.0) | 변화 없음 |
| 긴 망토 자락 (`longCapeTripMeanSeconds: 0` 실측 확인) | 기본 OFF, 켜면 부활 | 변화 없음 |
| **팔다리 8개** | **관문 A·C가 먼저 막는다** | ⇒ **여전히 0회. 아무것도 안 살아난다** |

> **예측(명시적):** 질량 정규화만 착지시키면 **출하 기본값에서 RAGDOLL 진입 횟수는 변하지 않는다.**
> 관측 가능한 유일한 변화는 **RAGDOLL 중** 팔다리-지면 접촉이 임계를 넘게 되는 것뿐이고,
> 그건 개선이 아니라 **아래 1-5의 회귀 위험**이다.
> ⇒ **이 예측이 「고쳤는데 아무 일도 안 일어났다」는 다음 라운드의 오진을 막는 장치다.**

## 1-3. 원인 (정리)

**하나가 아니라 셋이고, 순서가 있다.**

```
[A] 능동 모드 팔다리 = Kinematic + useFullKinematicContacts=0   →  콜백이 아예 안 온다
                     ↓ (RAGDOLL 중에만 Dynamic이 되어 통과)
[B] 임계 8.0(루트 질량 1.0 전제) vs 팔다리 질량 0.06~0.09        →  필요속도 88.9~133.3 > 상한 12.0
                     ↓
[C] 월드에 Dynamic 상대 0개                                      →  능동 모드에서 [A]를 뚫을 방법이 없다
```

**CLAUDE.md는 [B]만 적었다.** [B]는 참이지만 **첫 관문이 아니다.**
그래서 "[B]를 고쳤다 → 여전히 안 된다 → 랙돌이 또 고장났다"는 오진이 예약돼 있는 상태였다.

## 1-4. 수정안 선택지

### 옵션 0 — **이번 라운드는 진단만 착지시키고 판정식은 손대지 않는다** (debugger 권고)

- 근거: 관문 [A]·[C]가 살아 있는 한 [B] 수정의 **관측 가능한 효과가 0**이다.
  CLAUDE.md가 이미 *"가르기 전에 임계값이나 릴레이 질량을 만지지 마라"*(`RagdollImpactResolver.cs:44`)라고
  못박아 두었고, 이번 실측은 **가르기의 도구가 아직 부족하다**는 것을 보여준다(1-6 참고).
- 착지물: 1-6의 진단 로그 2건 + 1-7의 구조 감사 테스트 1건.
- 부작용: 없음(판정 경로 무변경).
- **비용**: 결함이 한 라운드 더 열린 채로 남는다.

### 옵션 A — 충격량을 **캐릭터 한 몸 기준**으로 통일 (질량 정규화)

두 호출부의 중복식(`docs/BUG_REPORT_PHASE2.md`가 2026-08-27에 이미 DRY 위반으로 지적한 그 자리)을
헬퍼 하나로 합치고, **곱하는 질량을 보고 바디가 아니라 루트 질량으로** 바꾼다.

```
충격량 = collision.relativeVelocity.magnitude × (루트 Rigidbody2D.mass)
```

- 근거: `ragdollForceThreshold`가 답하는 질문은 *"팔이 얼마나 세게 맞았나"*가 아니라
  ***"이 캐릭터가 넘어질 만큼 맞았나"***다. 팔다리는 관절로 루트에 묶여 있어 사지 피격의 운동량은
  결국 전신에 실린다. 보고 바디의 질량을 쓰는 것은 **묻지 않은 질문에 답하는 것**이다.
- 루트 경로: 루트 질량이 1.00이므로 **비트 단위로 무변경**(회귀 위험 0).
- 팔다리 경로: 필요 상대속도가 133.3/88.9 → **8.0**으로 내려간다.
- **부작용 (중대, 1-5에서 상술)**: RAGDOLL 중 팔다리-지면 접촉이 임계를 넘게 되어 **재진입 루프** 위험.
- **부작용 2**: 루트 질량이 나중에 1.00이 아니게 되면 임계 의미가 조용히 이동한다
  (`SceneBootstrapper.cs:411-412`가 *"질량을 줄이면 ragdollForceThreshold가 조용히 예민해진다"*라고
  이미 경고한 그 함정이 팔다리에서 루트로 옮겨갈 뿐이다).

### 옵션 A′ — 질량을 아예 곱하지 않는다(= 상대속도 임계)

- 지금 루트 질량이 1.00이라 **오늘은 옵션 A와 수치가 같다.**
- 장점: 「질량」이라는 숨은 결합이 사라진다. 임계값 8.0의 물리적 의미가 *"상대속도 8유닛/초"*로 명시된다.
- 단점: 필드명 `ragdollForceThreshold`와 `StickmanBlackboard.LastImpactMagnitude`의 이름·단위가
  전부 「힘/충격량」을 주장하는데 실체는 속도가 된다. `RagdollImpactResolver.ResolveEntryImpulse`의
  N·s 환산 문서(`:349-388`)가 통째로 거짓말이 된다 ⇒ **문서 부채가 코드 부채보다 크다.**
- 부작용은 옵션 A와 동일(재진입 루프).

### 옵션 B — 바디별 임계 테이블

- 형태: `임계(바디) = ragdollForceThreshold × (바디질량 / 루트질량)` 또는 명시 표.
- **수학적으로 옵션 A와 동치**다(양변에 같은 수를 곱한 것). 그런데 유지비만 더 든다 —
  마디가 8개에서 12개로 늘면 표를 고쳐야 하고, 고치는 것을 잊으면 **그 마디만 조용히 죽는다**
  (지금과 정확히 같은 실패 유형의 재생산).
- CLAUDE.md 원칙 4(플러그인 구조: 기본 로직 무수정 + 매니페스트 추가)와도 어긋난다.
- ⇒ **debugger 판단: 기각 권고.** 옵션 A가 같은 결과를 표 없이 얻는다.

### 옵션 C — 관문 [A]를 먼저 연다 (`useFullKinematicContacts = true`)

- 능동 모드 팔다리가 정적 지면과도 접촉을 만들게 된다.
- **강하게 반대한다.** 이유:
  1. 능동 모드의 팔다리는 매 프레임 Transform 직접 대입으로 걷기 포즈를 만든다. 접촉을 켜면
     **매 걸음마다 발 마디가 바닥과 접촉 이벤트를 쏟는다** — 24시간 상주 앱에서 상시 비용이고,
     `[착지충격]` 로그 예산도 즉시 고갈된다.
  2. 그 접촉들은 전부 「내 착지」라서 차단막이 걸러야 하는데, 차단막의 기준점 `footY`는
     **루트 원점**(`RagdollImpactResolver.cs:302`)이라 팔 접촉에 대해서는 의미가 흐려진다.
  3. **사용자가 닫은 문 근처다.** 「추락 충격 → 랙돌」을 우회로 되살릴 위험이 실재한다.
- ⇒ **기각 권고.**

### 옵션 D — 릴레이를 「진짜 외력」에만 반응하게 좁힌다 (옵션 A와 **함께** 쓰는 안전장치)

`RagdollLimbImpactRelay.OnCollisionEnter2D` 진입부에 한 줄:

```
// 상대가 Dynamic이 아니면 그것은 '내가 세상에 부딪힌 것'이지 '세상이 나를 때린 것'이 아니다.
if (collision.rigidbody == null || collision.rigidbody.bodyType != RigidbodyType2D.Dynamic) return;
```

- 이러면 옵션 A를 넣어도 **RAGDOLL 중 지면 접촉이 재진입을 못 만든다**(1-5 회귀가 구조적으로 봉쇄된다).
- 루트 경로는 무변경(이 파일만 바뀐다).
- **정직한 한계**: 관문 [C]가 그대로라 **오늘은 이 릴레이가 여전히 한 번도 안 발화한다.**
  다만 그것이 **의도된 무발화**가 되고, 첫 Dynamic 상대(DLC 소품/공/두 번째 캐릭터)가 들어오는
  순간 **자동으로 옳게 동작한다.**
- 차단막(`IsOwnLandingContact`)과 중복 아닌가? — 아니다. 차단막은 **(비Dynamic) AND (접촉이 발 근처)**
  둘 다여야 막는다. 위 한 줄은 첫 조건만으로 막는다. 팔다리에는 「발 근처」라는 개념이 안 맞으므로
  이 축소가 옳다.

### ★ debugger 권고 조합

```
지금:   옵션 0  (진단 착지)  ─────────────────────────────┐
다음:   옵션 A + 옵션 D 를 한 커밋으로              ← 둘을 나누면 A만 들어간 순간 1-5가 터진다
금지:   옵션 B(표) · 옵션 C(풀 키네마틱 접촉)
불변:   throwTumbleEnabled=1 / landingImpactRagdollShield=1 은 손대지 않는다
```

## 1-5. ★ 부작용 — 옵션 A 단독 착지 시의 **구체적 회귀 시나리오** (계산으로 유도, 실기 미확인)

차단막의 허용 천장(`RagdollImpactResolver.cs:303`):
```
ceiling = footY + CharacterHeightWorld × 0.2
        = footY + (2.2746944 × 0.75) × 0.2
        = footY + 0.3412 유닛
```
(`StickConfig.BaselineCharacterTotalHeight = 2.2746944`(`:1877`), `characterScale = 0.75`(에셋 `:251`))

Dock 낙차는 코드 주석 실측으로 **약 1.29유닛**(`SceneBootstrapper.cs:1437-1438`).

> **재현 시나리오**: 캐릭터가 Dock 가장자리에서 RAGDOLL로 쓰러진다 → 몸통(루트 원점 = 발바닥)은
> 아래쪽 바닥에 놓이고, 팔 한 짝이 **1.29유닛 높은 `DockPhysicsStep` 윗면**에 걸친다.
> 그 접촉점 y − footY = 1.29 > 0.3412 ⇒ **차단막이 안 막는다.**
> 옵션 A가 들어간 상태라면 그 접촉의 충격량 = 상대속도(자유낙하로 쉽게 8 초과) ≥ 임계
> ⇒ `TryApplyImpact` → `ChangeState(Ragdoll, isForcedInterrupt: true)`
> ⇒ `RagdollState.Enter()`가 `_settleTimer`를 **0으로 리셋**
> ⇒ 팔다리가 튀는 동안 접촉이 계속 새로 생기므로 **정착 판정에 도달하지 못한다 = GETUP 불가**.

이것은 `docs/BUG_REPORT_SCENE_WIRING.md`의 **BUG-SW-M1(랙돌 무한낙하)과 같은 가족**이다.
그때는 감쇠가 없어서였고, 이번엔 타이머 리셋이 원인이다. **옵션 D가 이 경로를 원천 봉쇄한다.**

부수 부작용(작음): 임계 **미만** 경로도 함께 활성화된다 — `TryApplyImpact:334`의 `ApplyHitLean()`이
RAGDOLL 중 팔다리 접촉마다 호출된다. 지금은 세기 0.075 수준이라 눈에 안 띄지만, 옵션 A 후에는
`Clamp01(impulse/threshold)`가 1.0으로 포화해 **GETUP 직후 상체가 기운 채 시작**할 수 있다.
옵션 D가 이것도 함께 막는다.

## 1-6. 진단용 로그 추가안 (코드 조각 — **coder 배정용. 나는 넣지 않았다**)

### 왜 지금 로그가 부족한가

`RagdollImpactResolver.LogCollisionImpact`(`:213`)는 2026-09-02에 **보고 바디 + 역산질량 자기검증**까지
갖췄다(`:269-275`). 설계는 옳다. 그런데 **관문 [A]가 참이면 이 줄은 영원히 안 찍힌다.**

```
_limbCollisionLogSamplesLeft = 6  (RagdollImpactResolver.cs:198)  ← 6인 채로 세션이 끝난다
```

**「팔다리 로그 0줄」이 「접촉이 없었다」인지 「약해서 게이트에 걸렸다」인지 구분할 수 없다.**
이것이 정확히 이 저장소가 반복해서 당한 형태다 — *"죽은 프로브의 출력이 성공한 프로브와 똑같이 생겼다."*
그리고 **`[착지충격]`의 `보고바디` 항목을 검증하는 테스트는 0건이다**(전 저장소 grep: 프로덕션 3줄 + 문서만).
즉 **가르기 도구 자체가 아직 한 번도 검증되지 않았다.**

### 추가안 ① — 구조적 가용성 원장 (침묵의 의미를 **접촉 없이** 확정한다)

`Core/RagdollLimbImpactRelay.cs`에 **판정 경로를 한 줄도 건드리지 않고** 추가.

```csharp
        // ── 진단 전용(2026-09-05, debugger 제안). 판정 경로는 한 줄도 바뀌지 않는다. ────────────
        // 왜: 이 릴레이가 침묵할 때 그것이 "접촉이 약했다"인지 "콜백이 아예 안 온다"인지를
        //     [착지충격] 로그만으로는 구분할 수 없다(그 줄은 콜백이 와야만 찍힌다).
        //     그래서 접촉을 기다리지 않고 **구조로** 도달 가능성을 한 번 찍는다.
        // 상주 비용: 세션당 최대 8줄(릴레이 수), 그 뒤 영구 침묵. 문자열은 이 8회에만 만든다.
        private static int _availabilityLinesLeft = 8;

        private void Start()
        {
            if (_availabilityLinesLeft <= 0) return;
            _availabilityLinesLeft--;

            StickConfig cfg = _agent != null ? _agent.Config : null;
            float threshold = cfg != null ? cfg.ragdollForceThreshold : 8f;
            float ceilingSpeed = cfg != null ? cfg.dragThrowMaxSpeed : float.NaN;
            float mass = _body != null ? _body.mass : float.NaN;
            float needSpeed = mass > 0.0001f ? threshold / mass : float.PositiveInfinity;
            bool kinematic = _body != null && _body.bodyType == RigidbodyType2D.Kinematic;
            bool fullContacts = _body != null && _body.useFullKinematicContacts;
            bool hasCollider = GetComponent<Collider2D>() != null;

            Debug.Log($"[릴레이가용성] {name}: 질량={mass:F3}, bodyType={(_body != null ? _body.bodyType.ToString() : "?")}, " +
                $"useFullKinematicContacts={fullContacts}, 레이어={LayerMask.LayerToName(gameObject.layer)}({gameObject.layer}), " +
                $"콜라이더={hasCollider}, 임계={threshold:F1} -> 필요상대속도={needSpeed:F1}, 던지기상한={ceilingSpeed:F1}. " +
                $"속도관문={(needSpeed <= ceilingSpeed ? "통과가능" : "도달불가")}, " +
                $"접촉관문={((kinematic && !fullContacts) ? "능동모드에서 정적/키네마틱 상대와 접촉 없음" : "접촉가능")}.");
        }
```

**이 한 줄이 답하는 것**: 릴레이 8개가 붙어 있는가 / 콜라이더가 있는가 / 지금 어떤 bodyType인가 /
질량이 얼마인가 / **어느 관문에서 죽는가**. 접촉이 한 번도 안 와도 전부 확정된다.

### 추가안 ② — 표본 예산 잔량 1회 보고 (침묵을 **말하게** 한다)

`States/RagdollImpactResolver.cs`. 호출부는 `StickmanBlackboard.TickPose()` 끝 한 줄이면 된다
(이미 `RefreshNow()`를 부르는 그 자리 — 조기 return을 안 타는 유일한 지점).

```csharp
        // 진단 전용 — "팔다리 버킷이 만량인 채 30초가 지났다"는 사실 자체가 결론이다.
        // 세션당 정확히 1줄.
        private static float _silenceReportAt = -1f;

        internal static void TickReachabilityWatch()
        {
            if (_silenceReportAt < 0f) { _silenceReportAt = Time.realtimeSinceStartup + 30f; return; }
            if (_silenceReportAt == float.MaxValue) return;
            if (Time.realtimeSinceStartup < _silenceReportAt) return;
            _silenceReportAt = float.MaxValue;

            Debug.Log($"[착지충격] 30초 관측 종료 — 표본 예산 잔량: 루트 {_rootCollisionLogSamplesLeft}/{CollisionLogSampleCount}, " +
                $"팔다리 {_limbCollisionLogSamplesLeft}/{CollisionLogSampleCount}. " +
                "★ 팔다리 잔량이 만량이면 '약해서 안 찍힌 것'이 아니라 **콜백이 한 번도 오지 않았다**는 뜻이다 " +
                "(질량 임계를 고치기 전에 이 줄부터 보라).");
        }
```

### 추가안 ③ — `보고바디` 항목의 **양성 대조**(테스트, `test-engineer` 소관)

지금 이 진단 줄을 검증하는 테스트가 0건이다. **부재 단언이 아니라 존재 단언**으로 못박아야 한다:

- 루트에 강제 충돌을 만들고 `[착지충격] … (루트, 질량=1.000 …`이 **찍히는지**(양성 대조)
- 같은 러너에서 팔다리 버킷이 **만량으로 남는지**(현 상태의 사실)
- 그리고 **역산질량 == 질량**이 성립하는지(자기검증이 실제로 도는지)

⇒ 이 셋이 통과하기 전에는 `[착지충격]`으로 무엇도 판정하지 않는다.

## 1-7. 검증법

| 무엇을 | 어떻게 | 통과 기준 |
|---|---|---|
| 8개 특정 | 프리팹 YAML을 GUID로 파싱(이번 라운드 방식). **문자열 `RagdollLimbImpactRelay`로 grep하면 0건**이다 — 클래스명은 YAML에 없다(`coder`가 2026-09-03에 이 함정에 빠질 뻔한 기록 있음) | 릴레이 8 / 루트 1 / 총 바디 9. 양성·음성 대조 동반 |
| 질량 관문 | `임계/질량`을 **상수를 참조해** 계산(숫자 하드코딩 금지 — CLAUDE.md 협업 프로토콜) | 133.3 / 88.9, `dragThrowMaxSpeed` 초과 |
| 접촉 관문 | 위 추가안 ①의 `[릴레이가용성]` 줄 | 8줄 전부 `접촉관문=능동모드에서 … 접촉 없음` |
| Dynamic 상대 0개 | `AddComponent<*Collider2D>` 전수 + 각 대상에 `Rigidbody2D`가 있는지 | 프로덕션 생성 콜라이더 6곳 전부 Static |
| 옵션 A 착지 후 회귀 | `StickmanRagdollRecoveryTests`(정착 실패 = GETUP 미도달을 직접 잡는다) + Dock 가장자리 랙돌을 **의도적으로** 만드는 새 케이스 | GETUP 도달, `_settleTimer` 리셋 0회 |
| 실기 | `[착지충격]` / `[릴레이가용성]` 두 줄을 Player.log에서 확인 | **리더 승인 후 1인스턴스만**(전원 공통 규칙) |

---

# 【2】 `CharacterAccessoryRenderer.Rebuild()` — 한 프레임 이름 중복

## 2-1. 현상 (perf-doc 신고 1번, `Tasklist.md:23337·23543` 배정 대기)

`Destroy(_container)` 직후 같은 호출에서 같은 이름의 새 컨테이너를 만든다. Unity의 `Destroy`는
지연 실행이므로 **그 사이 계층에 같은 이름의 자식이 둘 존재한다.**

## 2-2. 코드 근거

```
Interaction/CharacterAccessoryRenderer.cs:828   private void Rebuild()
Interaction/CharacterAccessoryRenderer.cs:830       if (_container != null) Destroy(_container);      ← 지연 파괴 예약
Interaction/CharacterAccessoryRenderer.cs:831       DestroyFillMeshes();
Interaction/CharacterAccessoryRenderer.cs:838       _container = new GameObject("EquipmentAccessories");  ← 같은 이름, 즉시 생성
Interaction/CharacterAccessoryRenderer.cs:839       _container.transform.SetParent(transform, false);     ← 자식 목록 **끝**에 추가
Interaction/CharacterAccessoryRenderer.cs:845       var headGroup = new GameObject("HeadAttached");       ← 이 이름도 함께 둘이 된다
```

호출 경로: `LateUpdate()`(`:402`) → `EnsureBuilt()`(`:431` → `:649`) → `Rebuild()`(`:654`).

## 2-3. **무엇이** 중복되는가 — 실측

| 중복되는 것 | 개수(추정) |
|---|---|
| `EquipmentAccessories` GameObject | 2 |
| `HeadAttached` GameObject | 2 |
| 조각 GameObject — `AddLine`(`:1013`) / `AddFill`(`:983`, 이름 `<Shape>Fill`) | 직전 차림 + 새 차림 (전 슬롯 최악 기준 채움만 11개 × 2 — `Tasklist.md:12254`) |
| `Mesh`(`_fillMeshes`) | 옛것은 `DestroyFillMeshes()`로 같은 프레임 파괴 예약 |
| `LineRenderer` / `MeshRenderer` | 위와 동수 |

★ `Transform.Find` / `FindChild`류는 **자식 목록의 앞쪽을 먼저 만나므로 「죽어가는 쪽」을 돌려준다.**
새로 만든 쪽은 목록 끝에 붙기 때문이다. 즉 **틀린 답이 조용히 나온다** — 예외도 null도 아니다.

## 2-4. 재현 조건

`Rebuild()`가 도는 조건 = `EnsureBuilt`의 서명이 바뀌는 것(`ComputeSignature`, `:661~`):

1. **착용 변경** (`OnEquipmentChanged` → `_builtSignature = -1`, `:393`)
2. **좌우 반전** (`SyncFacing` → `_builtSignature = -1`, `:603`) ← **걷는 동안 수시로 일어난다**
3. **잠금 해제**(레벨업)

⇒ **가장 흔한 재현 경로는 착용 변경이 아니라 「걷다가 방향을 바꾸는 것」이다.**

관측 창(= 중복이 보이는 구간):
```
CharacterAccessoryRenderer.LateUpdate 안의 Rebuild() 실행 시점
        ↓
Unity가 지연 파괴를 커밋하는 시점 (문서: "현재 Update 루프 이후, 렌더링 이전")
```

## 2-5. 실제 피해 — **오동작이 아니라 관측 오염이다**

### (a) 화면: 피해 없음

Unity는 **렌더링 전에** 파괴를 커밋하므로 죽어가는 컨테이너는 그 프레임에 그려지지 않는다.
"모자가 두 개 겹쳐 보인다"는 증상은 **일어나지 않는다.**
(※ 이 문장은 Unity 문서 근거이고 **실기 미확인**이다. 2-7의 H2 참고.)

### (b) 프로덕션 로직: **현재 소비자 0명** — 전수 조사했다

| 통로 | 실측 | 영향 |
|---|---|---|
| `transform.Find("EquipmentAccessories")` | 프로덕션 히트 **1건**(= `:838`의 생성 그 자체뿐, 조회 0건). 테스트 **7개 파일** | 프로덕션 무영향 |
| `CharacterVisualRegistry` (숨김·획두께하한·시각반폭의 단일 창구) | **pull 방식.** `CollectVisuals`가 `_lines`/`_fills` **필드**를 읽고, 두 리스트는 `Rebuild` 첫머리(`:832-836`)에서 비워진다 | **안전** — 죽어가는 쪽은 신고되지 않는다 |
| `StickmanBlackboard.cs:1327` `GetComponentsInChildren<ICharacterInkExtentProvider>` | 구현체는 **컨테이너가 아니라 루트의 컴포넌트**라 컨테이너 파괴와 무관 | 안전 |
| `CharacterAccessoryRenderer.FindDirectChild`(`:1418`) | 호출은 `Awake`의 `"Torso"`/`"Head"` 2곳뿐(`:354-355`) | 안전 |
| `StickmanAgent` `_renderers`/`_lineRenderers`(`:591·598`) | `Awake` 1회 스냅샷 — 애초에 액세서리를 못 본다(그래서 Registry가 생겼다) | 무관 |
| `StickmanMetrics.cs:238`, `StickmanPoseAnimator.cs:436` 등 `childCount` 순회 | 특정 이름(`Head`/`Torso`/`*Arm`)만 찾는다. 자식이 하나 늘 뿐 | 무해(순회 1회 증가) |

⇒ **오늘 사용자에게 보이는 오동작은 없다. 「잠재 함정」이다.**

### (c) 측정 신뢰성: **여기가 진짜 피해다**

`transform.Find("EquipmentAccessories")`를 쓰는 테스트 **7개 파일**이 **창 안에서 읽으면 죽어가는 쪽을 잰다.**
지금 안 터지는 이유는 우연에 가깝다 — 그 테스트들이 `yield return null` **뒤**(다음 프레임의 Update 단계,
즉 그 프레임의 `LateUpdate` **이전**)에 읽기 때문이다(예: `AccessoryFacingFlipFillTests.cs:59`의
`for (int i = 0; i < 3; i++) yield return null;`).
**`WaitForEndOfFrame`으로 바꾸거나, 실행 순서가 뒤인 `LateUpdate` 프로브를 새로 만드는 순간 뒤집힌다.**
그리고 그 실패는 **"컨테이너는 있는데 내용이 직전 차림"**으로 나타나 원인 추적이 매우 어렵다.

### (d) 성능/자원: 한 프레임 2배 점유 (누수 아님)

- 최악 시 GameObject 약 2×(11 채움 + 선 6~) + Mesh 2벌을 **한 프레임 동안 동시 보유**.
- ★ **프레임 페이싱이 이 「한 프레임」을 늘린다**: `Platform/ViewerPresence.cs:354`
  `DisplayOffTargetFps = 4` ⇒ 화면 슬립 등급에서 **한 프레임 = 250 ms**.
- 다만 파괴는 커밋되므로 **누적 누수는 아니다.**

### (e) ★ 인접 발견 (별건, 가설) — `AddLine`만 `.material`을 쓴다

```
:1018   lr.material = _lineMaterial;          ← AddLine
:992    mr.sharedMaterial = _lineMaterial;    ← AddFill
```
`Renderer.material`은 이 저장소 전체가 지키는 `sharedMaterial` 관례에서 유일하게 벗어난 자리다.
**가설**: Unity 버전/경로에 따라 이 대입이 머티리얼 인스턴스를 만들면, **방향을 바꿀 때마다**
선 개수만큼 머티리얼이 생겨 24시간 상주에서 누적된다.
**검증**: 걷기 5분 실기 + Profiler `Memory > Materials` 카운트 추이, 또는
`lr.sharedMaterial == _lineMaterial` 참조 동일성 단언 1건. **미확인 — 단정하지 않는다.**

## 2-6. 수정안 선택지

### 옵션 1 — **이름을 즉시 비운다** (최소 변경, debugger 권고)

```csharp
if (_container != null)
{
    _container.name = "EquipmentAccessories(파기중)";   // 이름으로 찾는 관측자에게 즉시 사실을 말한다
    _container.SetActive(false);                        // 그 프레임에 계층/렌더 양쪽에서 빠진다
    Destroy(_container);
}
```
- 장점: 3줄. 생성 순서·구조 무변경. `Find`가 **새 컨테이너를 반환**하게 된다.
- 부작용: `SetActive(false)`는 죽어가는 쪽의 `MeshRenderer.enabled`엔 손대지 않지만
  `activeInHierarchy`가 false가 되므로, **「활성 채움 개수」를 세는 테스트의 값이 바뀔 수 있다.**
  → `AccessoryFacingFlipFillTests`가 정확히 그 숫자를 센다. **회귀 확인 필수.**
- ★ 이름 문자열을 **테스트가 하드코딩하고 있다**(7개 파일). 이름을 바꾸는 순간
  CLAUDE.md가 경고한 **「니들 부패」**가 발생한다 — 그래서 **원래 이름은 새 컨테이너가 유지**해야 한다.
  위 안은 그 조건을 만족한다(죽는 쪽만 개명).

### 옵션 2 — 컨테이너를 재사용하고 **자식만** 갈아끼운다

```
컨테이너/HeadGroup은 한 번 만들고 유지 → Rebuild는 자식만 Destroy 후 재생성
```
- 장점: 이름 중복이 **정의상 사라진다.** 컨테이너 Transform(위치/회전/스케일) 재계산도 없어진다.
- 부작용: 자식 이름(`<Shape>Fill` 등)의 중복은 **그대로 남는다** — 같은 병이 한 층 아래로 내려갈 뿐.
  그리고 `SyncContainerScale`/`SetLocalPositionAndRotation` 초기화 순서 가정이 흔들린다.
- 비용 대비 이득이 옵션 1보다 낮다.

### 옵션 3 — `DestroyImmediate`
- **기각.** 런타임 사용은 Unity가 권장하지 않고, `LateUpdate` 중 계층을 즉시 부수면
  같은 프레임의 다른 소비자에게 **null 참조**를 준다(지금의 "낡은 값"보다 나쁘다).

### 옵션 4 — 아무것도 안 하고 **주석으로 못박는다**
- 프로덕션 소비자가 0명이라는 이번 실측이 이 선택지를 정당화한다.
- 단, **다음에 이름으로 찾는 사람이 생기면 조용히 틀린 답을 받는다.** 옵션 1이 3줄이므로 권고하지 않는다.

### ★ debugger 권고: **옵션 1**, 그리고 같은 커밋에 회귀 확인 1건.

## 2-7. 과학적 토론 로그 (CLAUDE.md 협업 프로토콜 형식)

| # | 가설 | 검증 방법 | 결과 |
|---|---|---|---|
| **H1** | 중복 자체가 존재하지 않는다(`Destroy`가 동기적이다) | `:830`↔`:838` 사이에 프레임 경계가 없음을 소스로 확인. Unity `Object.Destroy`는 지연 파괴 | **반증. 중복은 코드 사실이다** |
| **H2** | 죽어가는 컨테이너가 **그려져서** "장비가 겹쳐 보인다" | Unity 문서: 파괴는 *현재 Update 루프 이후 · 렌더링 이전*. 그리고 사용자 신고에 겹침 보고 없음 | **미반증이나 근거 약함 — 「화면 피해 없음」 쪽이 유력.** ★ **실기 미확인.** 확정 방법: `WaitForEndOfFrame` 코루틴에서 루트의 `EquipmentAccessories` 이름 자식 수를 세고, 같은 프레임 `LateUpdate` 말미(실행순서 +30000 프로브)에서도 세어 **두 값을 대조** |
| **H3** | 프로덕션에 이름으로 찾는 소비자가 있다 | 전 저장소 `transform.Find(`/`GameObject.Find`/`childCount` 전수(위 2-5 (b) 표) | **반증. 프로덕션 조회 0건(니들 히트 1건은 `:838` 생성뿐), 테스트 7개 파일** |
| **H4** | `Tasklist`에 열린 빨강 `AccessoryFacingFlipFillTests …채움 MeshRenderer가 3개(기대 2)`가 이 중복 때문이다 | 해당 테스트 실물 확인 — `:68`은 **현재 `Assert.AreEqual(3, …)`**이고 메시지도 *"기대 3 — 관/챙/띠"*로 갱신돼 있다 | **반증. 그 빨강은 이 결함이 아니라 「기대값이 조형 변경(HatBand 추가)보다 뒤처졌던 것」이고 이미 반영됐다.** 다음 러너에서 재확인 필요 |
| **H5** | 실제 피해는 GC/누수다 | `DestroyFillMeshes`(`:384`)가 Mesh를 손으로 지우고 컨테이너와 함께 같은 프레임에 커밋됨 | **반증(누수 아님). 다만 「한 프레임 2배 점유」는 참이고, `DisplayOff`(4fps)에서 그 한 프레임이 250 ms다** |

## 2-8. 검증법 (옵션 1 착지 후)

1. **양성 대조 먼저**: 수정 *전* 트리에서, `LateUpdate` 실행순서 뒤(`[DefaultExecutionOrder(30000)]`)
   임시 프로브가 **`EquipmentAccessories` 이름 자식 2개**를 실제로 보는지 찍는다.
   → **여기서 2가 안 나오면 그 뒤 초록은 아무것도 증명하지 않는다.**
2. 수정 후 같은 프로브가 **항상 1개**를 보는지.
3. `Transform.Find`가 **새** 컨테이너(= `_lines.Count > 0`인 쪽)를 돌려주는지.
4. 회귀: `AccessoryFacingFlipFillTests` · `AccessoryFillRenderingTests` · `CharacterVisualHalfWidthTests` ·
   `CharacterScaleRuntimeTests` · `BodyLeanAccessoryFollowTests` · `FullscreenSuspendCharacterInkTests` ·
   `CharacterAppearanceLayerTests` (= `"EquipmentAccessories"` 니들을 쓰는 7개 스위트 전량)
5. **이름 문자열을 새로 하드코딩하지 말 것** — 이미 7개 파일에 박혀 있다. 늘리면 부패 표면이 커진다.

---

# 3. 플랫폼 영향

- **Windows 영향: 없음(동일 경로, 별도 배정 불요).**
  이번에 다룬 3개 파일 `Core/RagdollLimbImpactRelay.cs` · `States/RagdollImpactResolver.cs` ·
  `Interaction/CharacterAccessoryRenderer.cs`에 **`#if`가 0건**이다(직접 grep. 양성 대조:
  `Core/ShortcutLabel.cs`에서 같은 프로브가 3건을 검출 — 프로브 생존 확인).
  물리·프리팹·레이어 매트릭스·`Destroy` 지연은 전부 플랫폼 중립 엔진 동작이다.
- **macOS 영향: 없음** (이번 라운드 프로덕션 `.cs` 수정 0줄).
- 다만 【2】(d)의 프레임 페이싱 등급은 플랫폼별 기본값이 갈린 이력이 있다
  (`Tasklist.md` 2026-09-01 MSAA/플랫폼별 기본값 분리) ⇒ **「한 프레임의 길이」는 Windows에서 다시 재야 한다.**
  옵션 1이 착지하면 이 축은 무의미해지므로 별도 배정은 불요.

---

# 4. ★ 리더 판단 필요 항목

| # | 판단 사안 | 선택지 | debugger 의견 |
|---:|---|---|---|
| **L-1** | 【1】을 **이번 라운드에 고칠 것인가** | (가) 옵션 0(진단만) → 다음 라운드에 A+D / (나) 지금 A+D 한 커밋 / (다) 보류 | **(가)**. [A]·[C]가 살아 있어 [B] 수정의 관측 효과가 0이다. 진단 없이 고치면 「고쳤는데 아무 일도 안 일어남」을 또 오진한다 |
| **L-2** | 옵션 A와 옵션 D를 **나눌 것인가** | 나눔 / 한 커밋 | **절대 나누지 말 것.** A 단독 착지는 1-5의 GETUP 불가 회귀를 연다 |
| **L-3** | 옵션 A′(질량 제거)를 채택할 것인가 | A / A′ | **A.** A′는 `ragdollForceThreshold`·`LastImpactMagnitude`·`ResolveEntryImpulse`(N·s 환산 문서 40줄)를 전부 거짓말로 만든다 |
| **L-4** | 옵션 C(`useFullKinematicContacts=true`) | 채택 / 기각 | **기각.** 상시 접촉 비용 + 「추락 충격」 우회 부활 위험 |
| **L-5** | `SceneBootstrapper.cs:2002`의 **틀린 주석**(*"바닥 등과는 정상 충돌"*) | 지금 고침 / 【1】 라운드에 묶음 | 【1】 라운드에 묶는다. 지금 고치면 `coder`와 파일이 겹친다 |
| **L-6** | `RagdollLimbImpactRelay.cs:7`의 **낡은 문서**(`ReportExternalImpact` → 실제는 `ReportCollisionImpact`) — `Tasklist.md:22154`에 이미 신고돼 있음 | 지금 / 묶음 | 【1】 라운드에 묶는다 |
| **L-7** | 【2】 옵션 1을 **누구에게** 배정하는가 | `coder` / `coder-ui` | 파일이 `Interaction/`이고 `coder`가 같은 파일을 최근에 만졌다(2026-09-03 `ReleaseWhenNothingWorn`). **소유권 충돌 확인 후 배정** |
| **L-8** | `[착지충격]` 진단 줄을 검증하는 **테스트**가 0건이다 | `test-engineer` 배정 / 보류 | ★ **2026-09-05 실측으로 우선순위 하향.** 테스트는 여전히 0건이지만 러너 로그 **218/218 역산질량 일치**로 도구가 실측 검증됐다(5-bis-5). **P0 아님.** 대신 1-6 추가안 ①(`[릴레이가용성]`)은 우선순위 유지 |
| **L-10** | ★ **정본 목록 문장 정정** — `RagdollImpactResolver` 클래스 문서의 *"루트의 물리 충돌 — 원리상 살아 있다"* | 그대로 / 실측 문구로 교체 | **교체 권고.** 러너 194건(출하 기본값)에서 충돌 기반 RAGDOLL **0건**, 임계 초과 3건은 **전부 차단막이 막았다**. 지금 문장은 이 경로가 평상시 도는 인상을 준다(5-bis-3) |
| **L-11** | 최근 러너 `qa-r11`이 **로그만 있고 결과 xml이 없다**(09-05 19:56, 로그가 `[FOOTSLIP-TEST] 준비`에서 끊김) | 진행 중으로 보고 대기 / 확인 | **진행 중일 가능성 높음.** 다만 TEAM.md 정본대로 **판정은 xml + `testcasecount == total`로만** — 나는 이 런의 결과를 통계에 쓰지 않았다. ★ 락 충돌 프로브는 **양성 대조 표본이 없어** 「없었다」와 「내가 못 본다」를 구분 못 했다(5-bis-7) |
| **L-9** | 인접 발견 (2-5 (e)) `AddLine`의 `.material` 대입 | `perf-doc`에 별건 배정 / 무시 | **가설 단계.** 5분 걷기 + Profiler 1회로 갈린다. 저비용이므로 배정 권고 |

---

# 5-bis. ★★ 실측 교차검증 — 러너 로그 218건 (2026-09-05 추가)

> 앞의 1~4절은 **정적 분석**이다. `docs/verify/runs/`의 PlayMode 로그 26개에서 `[착지충격]` 줄
> **218건**을 파싱해 정적 판정을 실제 실행 기록과 대조했다. **정규식 파싱 실패 0건**,
> 차단스위치 필드가 True/False 두 값을 모두 내는 것으로 프로브 생존 확인.
> (★ 첫 시도에서 캡처 그룹 인덱스를 잘못 잡아 "0건/0건"이 나왔다 — **조용한 초록이 아니라 예외로
> 죽었다.** 그 시끄러운 실패가 없었다면 "차단스위치 통계 0건"을 결론으로 적을 뻔했다. 기록해 둔다.)

## 5-bis-1. 관문 [A] — **실측으로 확증됐다 (순도 100%)**

| 보고 바디 역할 | 건수 | 그때의 상태 분포 |
|---|---:|---|
| 루트(`Stickman`) | 122 | `Ragdoll` 58 · `LandingCrouch` 41 · `Attack` 11 · `Fall` 11 · `Getup` 1 |
| **비루트(팔다리 8개)** | **96** | **`Ragdoll` 96 — 그 외 상태 0건** |

> **능동 상태(Idle/Walk/Jump/Fall/Attack/LandingCrouch/Getup)에서 팔다리가 보고한 건은 96건 중 0건이다.**
> 정적 분석이 예측한 그대로다 — 능동 모드 팔다리는 Kinematic이라 콜백이 오지 않고,
> `RagdollRig.cs:335`가 Dynamic으로 바꾼 **RAGDOLL 구간에서만** 보고가 발생한다.
> ⇒ **관문 [A]는 가설이 아니라 측정된 사실이다.**

## 5-bis-2. ★ 정정 ① — 「던지기 상한 12.0」은 틀린 상한이다 (결론은 불변)

내가 1-2 (b)에서 도달 가능 상대속도의 상한을 `dragThrowMaxSpeed = 12.0`으로 잡았다.
**실측은 그보다 크다** — 자유낙하 가속이 더해지기 때문이다.

| | 실측 최댓값(전 러너 누적) |
|---|---:|
| 팔다리 상대속도 | **21.59** (`LeftLegLower`) |
| 팔다리 충격량 | **1.94** = 21.59 × 0.090 |
| 루트 상대속도 / 충격량 | **18.84 / 18.84** |

팔다리 상위 6건: 21.59 / 17.46 / 16.56 / 16.47 … → 충격량 1.94 / 1.57 / 1.49 / 1.48
= **임계 8.0의 24.2% / 19.6% / 18.6% / 18.5%**.

> **결론은 그대로다** — 필요 88.9(다리)·133.3(팔)에 **최대 21.59로도 24%밖에 못 간다**.
> 하지만 **근거 숫자를 12.0으로 적은 것은 틀렸다.** 「11.1배/7.4배 부족」이 아니라
> **실측 기준 4.1배 부족**(다리 최상위 건)이 정확한 표현이다. 정정한다.

## 5-bis-3. ★ 새 발견 — **출하 기본값에서 충돌 기반 RAGDOLL은 0건이다**

`차단스위치`(= `landingImpactRagdollShield`) 값으로 갈랐다.

| 차단스위치 | 건수 | 임계 8.0 이상 | **RAGDOLL 전이** | 충격량 max |
|---|---:|---:|---:|---:|
| **True(출하 기본값)** | **194** | 3 | **0건** | 17.46 |
| False(네거티브 컨트롤이 일부러 끔) | 24 | 24 | **24건** | 18.84 |

- **24건의 RAGDOLL 전이는 전부 `차단스위치=False`, 전부 루트다.** 즉 **테스트가 일부러 방어를 끈 상태**에서만 났다.
- 출하 기본값에서 임계를 넘은 3건은 **전부 차단막이 막았다**:
```
보고바디=Stickman(루트, 질량=1.000, 역산질량=1.000, 상대속도=17.21), 충격량=17.21(임계 8.0),
  상태=Ragdoll, 최저 y=-11.812, 발 y=-11.832, 차단스위치=True -> 착지로 판정해 무시.
보고바디=Stickman(루트, 질량=1.000, 역산질량=1.000, 상대속도=17.46), 충격량=17.46(임계 8.0),
  상태=Ragdoll, 최저 y=-11.895, 발 y=-11.924, 차단스위치=True -> 착지로 판정해 무시.
```

> ### 정본 목록에 대한 정정 제안 (리더 판정 필요 — **L-10**)
> `RagdollImpactResolver` 클래스 문서는 *"루트의 물리 충돌 — **원리상 살아 있다**"*라고 적었다.
> **원리는 맞지만, 24개 러너 로그 어디에서도 출하 기본값으로 실현된 적이 없다.**
> 실현된 24건은 전부 차단막을 끈 네거티브 컨트롤이다.
> ⇒ *"원리상 살아 있으나 **차단막이 실제로 전부 막고 있다**(러너 194건 중 전이 0건, 임계 초과 3건 전부 차단)"*로
> 고칠 것을 제안한다. 지금 문장은 **읽는 사람에게 이 경로가 평상시 돌고 있다는 인상**을 준다.

## 5-bis-4. ★ 옵션 A의 위험이 **정량화됐다** (내 1-5 시나리오의 근거 강화)

실측 팔다리 96건에 **옵션 A(루트 질량 기준 = 상대속도 그대로)**를 대입하면:

```
상대속도 >= 8.0 인 팔다리 건 = 96건 중 8건 (8.3%)
  → 옵션 A 적용 시 이 8건이 전부 "임계 초과"가 된다.
  → 그러면 유일한 방어선은 차단막 하나뿐이다.
```

그 차단막의 실측 여유(천장 = 발y + 0.3412):

| 근접 상위 | 여유 | 바디 | 최저 접촉 y | 발 y |
|---|---:|---|---:|---:|
| 1위 | **−0.2272** | `RightLegLower` | −11.810 | −11.924 |
| 2위 | −0.3282 | `LeftLegLower` | −11.861 | −11.874 |
| 3위 | −0.3372 | `LeftLegLower` | −11.814 | −11.818 |

- **96건 전부 천장 아래 — 한 번도 넘지 않았다.** 여기까지는 옵션 A에 유리한 증거다.
- **그러나 최악 여유가 0.2272유닛으로, 천장 폭 0.3412의 33.4%밖에 안 남는다.**
  그리고 이 로그들은 **평평한 바닥 시나리오**다 — 내가 1-5에서 제시한 **Dock 단차 1.29유닛**
  시나리오는 이 러너들이 한 번도 만들지 않았다(팔다리 96건 전부 `발y ≈ 최저y ± 0.34` 범위).
  **1.29 > 0.3412이므로 그 시나리오에서는 차단막이 구조적으로 진다.**
- ⇒ **1-5 회귀 시나리오는 「미관측」이지 「반증」이 아니다.** 방어선이 하나뿐이고 여유가 1/3인 이상
  **옵션 D를 함께 넣으라는 권고는 유지한다.** 오히려 강화됐다.

## 5-bis-5. ★ 정정 ② — 진단 줄은 **테스트는 없지만 실측 검증은 있다**

내가 1-6에서 *"`[착지충격]`의 `보고바디`·`역산질량`을 검증하는 테스트가 0건"*이라고 적었다.
테스트가 0건인 것은 **여전히 사실**이다. 그러나 **검증 자체가 없다는 인상은 틀렸다**:

```
역산질량 자기검증:  검증 가능 218건 / 불일치 0건  → 218/218 통과
   (루트 역산질량 1.000 == 질량 1.000,  팔다리 0.060/0.090 == 0.060/0.090)
```

> **이 저장소가 만든 자기반증 장치가 실제로 작동하고 있다.**
> `RagdollImpactResolver.cs:185-189`가 *"진단용 줄일수록 자기 자신을 반증할 수 있어야 한다"*고 적고
> 넣은 `역산질량` 항목이, 218건에서 한 번도 어긋나지 않았다 ⇒ **이 로그의 보고 바디를 믿어도 된다.**
> ⇒ **L-8의 우선순위를 낮춘다**: 테스트 추가는 여전히 옳지만 P0가 아니다.
> 대신 **1-6 추가안 ①(`[릴레이가용성]`)의 우선순위는 그대로 높다** — 그것만이 답할 수 있는 질문
> (*"능동 모드에서 왜 한 건도 안 오는가"*)에 대해 지금 로그는 **침묵으로만** 답하고 있다.

## 5-bis-6. 【2】 H4 종결 — **실측으로 확정 반증**

내가 2-7 H4에서 *"`AccessoryFacingFlipFillTests` 빨강은 이 결함이 아니다"*라고 반증했고,
러너 결과가 그것을 확정했다:

| 러너 | 시각 | 결과 |
|---|---|---|
| `qa-r9_play.xml` | 09-05 06:00 | 638건 / **failed=1** — `AccessoryFacingFlipFillTests` |
| **`qa-r10_play.xml`** | **09-05 09:56** | **641건 / failed=0** — `AccessoryFacingFlipFillTests` **5/5 Passed** |

⇒ **그 빨강은 닫혔다.** 이름 중복과 무관했고, 기대값(2→3, HatBand 추가) 반영으로 해소됐다.
`AccessoryFillRenderingTests` 5/5 · `BodyLeanAccessoryFollowTests` 2/2 · `CharacterAccessoryScaleTests` 15/15도 전부 Passed.
⇒ **【2】의 이름 중복이 현재 어떤 테스트도 빨갛게 만들고 있지 않다** — 「잠재 함정」이라는 내 판정이 실측으로 확인됐다.

## 5-bis-7. 라운드 운영 관측 (리더에게 — **L-11**)

- 가장 최근 러너 **`qa-r11_play.log`(09-05 19:56)는 로그만 있고 결과 `xml`이 없다.**
  로그 말미가 `[FOOTSLIP-TEST] 준비`에서 끊겨 있다 ⇒ **진행 중일 가능성이 높다.**
- TEAM.md 정본대로 **판정은 프로브가 아니라 산출물(xml + `testcasecount == total`)로 한다** —
  따라서 **qa-r11은 아직 판정 불가**이고, 내 위 통계에서도 로그 줄만 쓰고 결과는 쓰지 않았다.
- ★ **정직한 한계**: 락 충돌 흔적(`Fatal Error` / `another Unity instance`)을 찾는 프로브가
  qa-r11에서 0건을 냈지만, **양성 대조 표본이 없다**(qa-r10에서도 0건). 즉 나는
  *"락 충돌이 없었다"*와 *"내 프로브가 락 충돌을 못 본다"*를 **구분하지 못했다.**
  이 저장소가 반복해서 당한 형태 그대로이므로 결론으로 쓰지 않는다.

---

# 5. Tasklist.md 등재할 문장 (리더가 그대로 옮길 것 — 나는 수정하지 않았다)

> **[debugger, 2026-09-05] 미해결 결함 2건 규명 — `docs/DEBUG_LIMB_IMPACT_AND_FRAMENAME.md`**
>
> **(1) 팔다리 충돌 경로 8개.** 프리팹 YAML을 GUID(`2ac7cc5aa599b44fc9b8e2ce2ebc58c9`)로 파싱해
> 릴레이 **8개 / 루트 1개 / 총 바디 9개**를 특정했고(양성·음성 대조 동반), 질량 0.06×4·0.09×4,
> 임계 8.0 도달 필요 상대속도 **133.3 / 88.9**(던지기 상한 12.0)를 **재확인했다 — CLAUDE.md의 숫자는 전부 참이다.**
> **다만 원인 기술이 절반이다.** 관문이 셋이고 질량은 **두 번째**다:
> **[A]** 능동 모드 팔다리는 `bodyType=Kinematic` + `useFullKinematicContacts=0`(프리팹 실측, 코드 설정 0건)이라
> **정적 지면과 접촉 자체가 생성되지 않는다** — 콜백이 아예 안 온다.
> **[B]** 질량 불일치(`RagdollLimbImpactRelay.cs:41` vs `StickmanAgent.cs:562`가 서로 다른 질량을 곱해 같은 임계와 비교).
> **[C]** 출하 월드에 Dynamic 상대가 **0개**다(라이벌 소스 부재, 화살·공·펫은 렌더러 전용, 런타임 콜라이더 6곳 전부 Static,
> 루트는 layer 8이라 8↔8 매트릭스 차단). 부수로 **[D]** Kinematic 팔다리는 Transform 직접 대입 구동이라 `relativeVelocity`가 0이다.
> ⇒ **예측: 질량만 고치면 출하 기본값에서 RAGDOLL 진입 횟수가 변하지 않는다.**
> 권고 = **옵션 A(충격량을 루트 질량 기준으로 통일, 루트 경로 비트 무변경) + 옵션 D(릴레이가 비-Dynamic 상대를 무시)를
> 한 커밋으로**. **나누면 안 된다** — A 단독은 Dock 가장자리 랙돌에서 팔이 1.29유닛 위 `DockPhysicsStep`에 걸릴 때
> 차단막 천장(0.3412유닛 = 0.2×신장 1.706)을 넘어 **재진입 → `_settleTimer` 리셋 → GETUP 불가**를 연다(BUG-SW-M1과 같은 가족).
> 옵션 B(바디별 표)는 옵션 A와 수학적 동치인데 유지비만 크고 원칙 4에 어긋나 **기각 권고**,
> 옵션 C(`useFullKinematicContacts=true`)는 상시 접촉 비용 + 「추락 충격」 우회 부활 위험으로 **기각 권고**.
> `throwTumbleEnabled=1` / `landingImpactRagdollShield=1`은 **손대지 않는다**(사용자가 닫은 문, 에셋에서 값 재확인).
> **진단 우선**: `[착지충격]`은 콜백이 와야만 찍히므로 **「팔다리 0줄」이 「접촉 없음」인지 「약함」인지 못 가른다**
> (`_limbCollisionLogSamplesLeft`가 6인 채로 끝난다). 그래서 접촉을 기다리지 않고 구조로 찍는
> **`[릴레이가용성]` 1회 원장**(질량/bodyType/`useFullKinematicContacts`/레이어/필요속도/어느 관문에서 죽는지, 세션당 8줄)과
> **표본 잔량 30초 1회 보고**를 제안했다(코드 조각은 문서에). ★ **`[착지충격]`의 `보고바디`·`역산질량`을 검증하는 테스트가
> 현재 0건**이다 — 가르기 도구 자체가 미검증이므로 `test-engineer` 배정을 권고한다.
> 덤: `SceneBootstrapper.cs:2002` 주석 *"바닥 등과는 정상 충돌"*은 **거짓**(막는 것은 레이어가 아니라 bodyType이다),
> `RagdollLimbImpactRelay.cs:7`의 `ReportExternalImpact` 표기는 실제 `:41`의 `ReportCollisionImpact`와 불일치(기신고).
>
> **(2) `CharacterAccessoryRenderer.Rebuild()` 한 프레임 이름 중복.** 코드 사실 확증
> (`:830` 지연 파괴 예약 → `:838` 같은 이름 즉시 생성 → `:839` 자식 목록 **끝**에 추가).
> `HeadAttached`(`:845`)도 함께 둘이 된다. **`Transform.Find`는 앞쪽을 먼저 만나므로 「죽어가는 쪽」을 돌려준다 —
> 예외도 null도 아닌 조용히 틀린 답이다.**
> **재현 조건은 착용 변경이 아니라 「걷다가 방향 전환」이 주경로**(`SyncFacing`이 `_builtSignature=-1`).
> **피해 판정: 프로덕션 소비자 0명(전수 확인).** `CharacterVisualRegistry`는 pull 방식으로 `_lines`/`_fills` 필드를 읽고
> 그 둘은 `Rebuild` 첫머리에서 비워지므로 안전하고, `transform.Find("EquipmentAccessories")`는 **프로덕션 조회 0건 / 테스트 7개 파일**이다.
> 화면 겹침도 없다(파괴는 렌더링 전 커밋 — **실기 미확인**, 확정 프로브를 문서에 적었다).
> **진짜 피해는 측정 신뢰성**이다 — 테스트들이 지금 안 터지는 것은 `yield return null`이 `LateUpdate` **이전**에 재개하기 때문이고,
> `WaitForEndOfFrame`이나 실행순서 뒤 프로브로 바꾸는 순간 **「컨테이너는 있는데 내용이 직전 차림」**으로 뒤집힌다.
> 부수: 한 프레임 자원 2배 점유(누수 아님)이고, `ViewerPresence.DisplayOffTargetFps=4`에서 **그 한 프레임이 250 ms**다.
> 권고 = **옵션 1(죽는 쪽만 즉시 개명 + `SetActive(false)` 후 `Destroy`, 3줄)**. 새 컨테이너는 이름을 유지해야 한다
> (7개 파일이 이름을 하드코딩하고 있어 바꾸면 니들 부패). 착지 시 **양성 대조 선행 필수** —
> 수정 전 트리에서 실행순서 +30000 프로브가 자식 2개를 실제로 보는지 먼저 찍는다.
> ★ **가설 반증 1건**: `Tasklist`에 열려 있던 빨강 `AccessoryFacingFlipFillTests …채움 3개(기대 2)`는 **이 결함이 아니다** —
> 실물 `:68`은 이미 `Assert.AreEqual(3, …)`("기대 3 — 관/챙/띠, HatBand 추가")로 갱신돼 있다. 다음 러너에서 재확인 필요.
> ★ **인접 발견(가설, 미확인)**: `AddLine`(`:1018`)만 `lr.material =`을 쓰고 `AddFill`(`:992`)은 `sharedMaterial`이다.
> 방향 전환마다 도는 경로라 머티리얼 인스턴스가 누적되면 24시간 상주에서 문제가 된다 — Profiler 1회로 갈린다(`perf-doc` 배정 검토).
>
>
> **(3) ★ 실측 교차검증 — 러너 로그 218건**(`docs/verify/runs/*_play.log` 26개, 파싱 실패 0건, 차단스위치 True/False 양성 대조).
> **관문 [A]가 실측으로 확증됐다**: 비루트(팔다리) 보고 **96건 전부 `상태=Ragdoll`**, 능동 상태 **0건** —
> 팔다리는 `RagdollRig`가 Dynamic으로 바꾼 구간에서만 보고한다.
> **정정 ①**: 도달 상한을 `dragThrowMaxSpeed 12.0`으로 잡은 것은 **틀렸다** — 실측 팔다리 상대속도 max **21.59**
> (자유낙하 가속). 「11.1배/7.4배 부족」이 아니라 **실측 4.1배 부족**(충격량 1.94 = 임계 8.0의 24.2%)이 정확하다.
> **결론(도달 불가)은 불변.**
> **새 발견**: `차단스위치=True`(출하 기본값) **194건 중 충돌 기반 RAGDOLL 전이 0건**이고, 임계를 넘은 3건
> (상대속도 17.21·17.46)은 **전부 차단막이 막았다**. RAGDOLL 전이 24건은 **전부 `차단스위치=False`인 네거티브 컨트롤**이다.
> ⇒ **정본 목록의 *"루트의 물리 충돌 — 원리상 살아 있다"*를 실측 문구로 고칠 것을 제안한다(L-10)** — 지금 문장은
> 그 경로가 평상시 도는 인상을 준다.
> **옵션 A 위험 정량화**: 팔다리 96건 중 상대속도 ≥8.0인 건이 **8건(8.3%)** — 옵션 A를 넣으면 이 8건이 전부 임계 초과가
> 되고 **방어선은 차단막 하나뿐**이 된다. 실측 최악 여유는 **−0.2272유닛**(천장 0.3412의 33.4%만 남음)이고,
> 이 러너들은 **평평한 바닥만** 만들었다 — Dock 단차 **1.29 > 0.3412**인 1-5 시나리오는 **미관측이지 반증이 아니다.**
> ⇒ **옵션 D 동반 권고는 유지, 오히려 강화됐다.**
> **정정 ②**: *"진단 줄 검증 0건"*은 톤이 틀렸다 — 테스트는 0건이 맞지만 **역산질량 자기검증이 218/218 일치**한다.
> 저장소가 넣어 둔 자기반증 장치가 실제로 작동 중이고, **이 로그의 보고 바디는 믿어도 된다.** L-8 우선순위 하향.
> **【2】 H4 종결**: `qa-r9`(09-05 06:00) `AccessoryFacingFlipFillTests` **Failed 1건** → `qa-r10`(09-05 09:56)
> **641건 failed=0, 해당 스위트 5/5 Passed**. 그 빨강은 이름 중복과 무관했고 이미 닫혔다 ⇒ **【2】는 현재 어떤 테스트도
> 빨갛게 만들지 않는 「잠재 함정」**이라는 판정이 실측으로 확인됐다.
> **운영 관측(L-11)**: `qa-r11`(09-05 19:56)은 **로그만 있고 xml이 없다**(로그가 `[FOOTSLIP-TEST] 준비`에서 끊김) —
> 진행 중으로 보이며 **판정 불가**라 통계에서 제외했다. 락 충돌 프로브는 **양성 대조 표본이 없어**
> 「없었다」와 「내가 못 본다」를 구분하지 못했다 — 결론으로 쓰지 않았다.
>
> **Windows 영향: 없음(동일 경로, 별도 배정 불요)** — 대상 3개 파일 모두 `#if` **0건**(양성 대조 `ShortcutLabel.cs` 3건 검출).
> **이번 라운드 프로덕션 `.cs` 수정 0줄. `Tasklist.md` 직접 수정 없음.**
