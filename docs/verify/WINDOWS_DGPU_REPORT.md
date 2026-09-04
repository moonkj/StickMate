# Windows 외장 GPU 강제 — 권고안

- 작성: `dev-platform` / 2026-09-03
- 성격: **판정 요청서.** 프로덕션 코드 수정 없음, `Builds/` 재빌드 없음. 구현은 리더·`game-architect` 판정 후.
- 검증 한계: **이 머신에 Windows가 없다.** 아래 "확인됨"은 전부 *macOS 위에서 빌드 산출물과 Unity
  설치본을 정적으로 실측한 것*이다. 런타임 동작 중 이 보고서가 실기로 확인한 것은 **하나도 없다.**
  (사용자 실기 A/B 수치는 리더가 `Tasklist.md:118-129`에 기록한 것을 인용한다.)

---

## 0. ★ 먼저 못박는다 — 런타임 C#으로는 불가능하다

GPU 선택은 **D3D11 디바이스가 생성되는 시점**에 끝난다. 그 시점은 우리 매니지드 코드가
**한 줄도 실행되기 전**이다(`UnityPlayer.dll`이 그래픽 디바이스를 먼저 세운다).
게다가 `NvOptimusEnablement`는 **프로세스가 시작되기도 전에 그래픽 드라이버가 PE export 테이블을
읽어** 판정한다.

실측으로 뒷받침한다: **Windows `UnityPlayer.dll` 안에 `NvOptimus` / `PowerXpress` 문자열이 0건이다.**
즉 이 값을 읽는 것은 우리 프로세스도 Unity 런타임도 아니고 **드라이버**다.

> **따라서 `Awake()`에서, `RuntimeInitializeOnLoadMethod`에서, `FramePacing`에서,
> 그 무엇으로도 이걸 끌 수 없다. 지렛대는 "바이너리" 아니면 "OS 설정" 둘뿐이다.**
> 이 문장을 나중에 누가 "런타임에서 해보자"로 되돌리지 못하게 여기에 둔다.

---

## 1. 확정된 사실

### 1-1. 원인 (정적 실측 — 이 보고서가 직접 확인)

`Builds/Windows/StickMate.exe`의 PE export 디렉터리를 직접 파싱했다(`strings` 아님).
**export는 정확히 4개:**

| 이름 | ord | RVA | 섹션 | **파일 오프셋** | 값 |
|---|---|---|---|---|---|
| `AmdPowerXpressRequestHighPerformance` | 1 | `0x19004` | `.data` | **`0x17a04`** | **1** |
| `D3D12SDKPath` | 2 | `0x19008` | `.data` | — | (포인터) |
| `D3D12SDKVersion` | 3 | `0x0f320` | `.rdata` | — | 611 |
| `NvOptimusEnablement` | 4 | `0x19000` | `.data` | **`0x17a00`** | **1** |

- export DLL 이름 필드가 `WindowsPlayer.exe`로 남아 있다.
- **출하 exe의 SHA-256이 Unity 플레이어 템플릿과 완전히 동일하다**
  (`bf64a21a…c656559`, 671,744 bytes,
  `PlaybackEngines/WindowsStandaloneSupport/Variations/win64_player_nondevelopment_mono/WindowsPlayer.exe`).
  **Unity는 이 exe를 이름만 바꿔 그대로 복사한다 — 우리 빌드에 컴파일 단계가 없다.**
- 근원: `.../Source/WindowsPlayer/WindowsPlayer/Main.cpp:9-10` — **하드코딩 리터럴.**
  조건 컴파일도, 설정 참조도, 환경변수도 없다.
- **공식 스위치는 없다**(확인함): `ProjectSettings.asset` 관련 키 0건 /
  `UnityEditor.dll`+`UnityEditor.CoreModule.dll` 문자열 스캔 0건 /
  `boot.config` GPU 선택 키 없음 / 플레이어가 아는 boot.config 키 전량에도 없음.

### 1-2. NVIDIA 문서가 정의한 값의 의미 (권고안의 핵심)

- `0x00000001` = *"rendering should be performed using **High Performance Graphics**"*
- `0x00000000` = *"**this method should be ignored**"*

**값 0은 "내장 강제"가 아니라 "이 힌트를 무시하라"다.** 해킹이 아니라 문서화된 두 값 중 하나다.

### 1-3. 사용자 실기 A/B (리더 기록 인용, `Tasklist.md:118-129`)

| | NVIDIA RTX 2050 | Intel Iris Xe |
|---|---:|---:|
| GPU **최악** | **222.48 ms** | **12.94 ms** (4분 표본) |
| GPU 평균 | 1.56 ms | 3.70 ms |
| GPU 점유(추정) | 5.8% | 13.5% |

사용자 확정: *"컴퓨터 렉은 확연히 줄은거 같음"*, *"어제도 강제로 내장그래픽으로 변경후에 렉 없어졌잖아"*.

---

## 2. ★ 권고 — **(A) PE 후처리를 채택한다. 단 "심볼명 무력화"가 아니라 "값 1→0"이다.**

### 2-1. 한 줄

> **`Assets/Editor/BuildStandalone.cs`의 `PerformBuildWindows`에 `IPostprocessBuildWithReport`를 달아,
> 산출된 exe의 `.data` 섹션에서 `NvOptimusEnablement`와 `AmdPowerXpressRequestHighPerformance`의
> 값 DWORD 두 개를 `1 → 0`으로 바꾼다. export 테이블도, 심볼명도 건드리지 않는다.**

### 2-2. ★ 지시받은 형태(심볼명 무력화)를 바꾼 이유 — 그쪽이 더 위험하다

지시는 *"export 테이블에서 심볼명을 무력화"*였다. **그건 하면 안 된다.**

1. **PE 로더 규약 위반.** export name 테이블은 **사전순 오름차순이어야** 한다
   (`GetProcAddress`가 이진 탐색을 한다). 실측한 현재 순서는
   `AmdPowerXpress… < D3D12SDKPath < D3D12SDKVersion < NvOptimusEnablement` 로 정확히 오름차순이다.
   `NvOptimusEnablement`를 예컨대 `XvOptimus…`로 바꾸면 우연히 순서가 유지될 수도, 깨질 수도 있고,
   깨지면 **같은 테이블의 다른 심볼(`D3D12SDKVersion`/`D3D12SDKPath`) 조회까지 망가진다.**
   그 둘은 D3D12 Agility SDK 로더가 읽는 실사용 심볼이다(`Builds/Windows/D3D12/` 폴더가 실재한다).
2. **의도가 문서화되지 않은 상태가 된다.** 이름을 망가뜨리면 드라이버 입장에서 "요청 없음"이지만,
   그건 우리가 만든 미정의 상태다. 반면 **값 0은 NVIDIA가 문서로 정의한 상태**다(§1-2).
3. **변경량이 최소가 아니다.** 값 패치는 **8바이트 중 2바이트**만 바뀌고
   export 테이블 · 섹션 헤더 · 재배치 · 크기 · 정렬 **어느 것도 바뀌지 않는다.**

### 2-3. 되돌리기 — 쉬운가? (질문에 직접 답한다)

**예. 세 층 전부 안전하다.**

| 층 | 상태 |
|---|---|
| **소스** | **전혀 안 바뀐다.** 후처리는 `Builds/Windows/StickMate.exe` 산출물 한 개만 만진다 |
| **Unity 설치본 템플릿** | **전혀 안 바뀐다.** 원본 SHA를 §1-1에 기록해 뒀다 |
| **산출물** | 훅을 지우고 다시 빌드하면 **템플릿과 바이트 단위로 같은 exe**가 나온다(§1-1이 그 증거다) |

부수 실측: **PE 체크섬 필드 = `0x0`**(원래 0 → 재계산 불필요),
**Certificate 데이터 디렉터리 크기 = 0 → 이 exe는 서명이 없다.**
따라서 **"코드 서명 무효화" 위험은 현재 존재하지 않는다.**
(★ 미래에 Authenticode를 도입하면 **패치가 반드시 서명보다 먼저**여야 한다. 순서가 뒤집히면
그때는 진짜로 깨진다 — 이 조건을 채택 시 문서에 못박아야 한다.)

### 2-4. 안티바이러스 오탐(V3) — 정직한 평가

- **위험은 실재한다.** 사용자는 V3를 쓴다(세이브 1175 기록). AV 휴리스틱은
  "널리 배포된 알려진 바이너리와 해시가 다른 변조본"에 민감하다.
- **그러나 이 위험은 우리가 곧 어차피 잃을 이점이다.** 지금 우리 exe가 템플릿과 해시가 같은
  이유는 **아직 아이콘도 버전 정보도 안 넣었기 때문**이다
  (`ProjectSettings.asset:295` `m_BuildTargetIcons: []`, `bundleVersion: 1.0` 기본값).
  출시 전에 **아이콘·제품명·버전을 반드시 넣게 되고, 그 순간 exe는 어차피 템플릿과 달라진다.**
  즉 "알려진 해시 화이트리스트 이점"은 **이번 패치가 아니어도 사라진다.**
  → **패치가 추가하는 *한계* 위험은 처음 생각보다 훨씬 작다.**
- **SmartScreen**: 미서명 exe는 어차피 평판 0에서 시작한다. 패치가 이를 *더* 나쁘게 만드는지는
  **미확인**. 다만 빌드마다 바이너리가 달라지면 평판이 안 쌓이는 문제는 패치와 무관하게 이미 있다.
- **검증 불가**: 이 머신에 Windows가 없어 V3 반응을 확인할 방법이 없다. → §5 게이트 G3.

### 2-5. ★ 가장 중요한 한계 — **A/B가 증명한 것은 A가 아니다**

**이걸 흐리면 안 된다.**

사용자가 실측한 것은 **Windows 설정의 "절전"**, 즉 `UserGpuPreferences`의 `GpuPreference=1`이다.
그건 **"내장을 써라"는 적극적 요청**이다.
반면 **권고안 A가 하는 일은 "외장을 써라는 우리 요청을 철회하는 것"**뿐이다.

> **A/B는 "목적지(내장)가 좋다"를 증명했지, "지렛대 A가 그 목적지에 도달한다"를 증명하지 않았다.**

값 0이면 NVIDIA 우선순위(높음→낮음: Forced Mode / 우클릭 메뉴 / **앱 프로필** / 정적 링크 /
**`NvOptimusEnablement`** / **전역 프로필**)에서 **전역 프로필**로 내려간다. 전역 기본은 "자동 선택"이고,
Optimus의 설계 의도상 프로필 없는 앱은 내장으로 간다 — **그러므로 내장으로 갈 가능성이 높다.**
**확신도: 중간~높음. 실기 미확인.** 드라이버 버전·제조사 프로파일에 따라
"요청 없어도 D3D 앱이면 외장"인 구성이 존재할 수 있다.

**→ 그래서 이 권고는 "고쳤다"가 아니라 "이렇게 동작할 것으로 판단한다, 실기 미확인"이다.
§5의 게이트 G1을 통과하기 전에는 해결로 표시하지 마라.**

---

## 3. 다른 두 길을 왜 안 고르는가

### (B) `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` — **거부. 제안하지 않는다.**

효과는 **가장 확실하다**(사용자가 실측한 바로 그 경로다). 그런데도 거부한다.

**원칙 3(유저 자산 불변) 예외가 성립하려면 무엇이 필요한가**를 작업표시줄 선례
(`docs/TASKBAR_REVEAL.md`)와 나란히 놓고 보면, **핵심 안전장치가 구조적으로 불가능하다.**

| 승인된 예외(작업표시줄)의 조건 | GPU 선호 항목에서 성립하는가 |
|---|---|
| **"실행 중에만 + 종료 시 원복"** | ★ **불가능.** GPU 선호는 **프로세스가 시작되기 전에** 읽힌다. 실행 중에만 켜 두면 **아무 효과가 없다.** 효과를 보려면 **앱이 꺼져 있는 동안에도 레지스트리에 남아 있어야 한다** — 승인된 예외의 가장 중요한 울타리를 이 건은 원리상 넘을 수 없다 |
| 원복 원장(`ReservedBarRestoreLedger` 형태) + 다음 실행이 먼저 복구 | 형태는 만들 수 있으나, 위 이유로 **"복구할 시점"이 존재하지 않는다** |
| 언인스톨 시 잔존 없음 | ★ **불가능.** 우리는 인스톨러가 없다(zip 배포). 사용자가 폴더를 지우면 **원복할 주체가 사라지고** 레지스트리 항목이 영구 고아로 남는다. 값 이름이 exe **전체 경로**라 경로가 바뀌면 쓰레기 항목이 계속 늘어난다 |
| 원래 그 설정을 안 쓰던 사용자에겐 아무 일도 없음 | 성립하지 않는다. **없던 항목을 새로 만든다** |
| 사용자 명시 동의 | 별도 UI 필요 — `ux-designer` 작업이 선행돼야 한다 |

**결론: B는 "리스크가 크다"가 아니라 "예외 성립 조건을 만족할 수 없다".**
채택하려면 사용자에게 **새로운 예외 승인**을 다시 받아야 하고, 그 판단은 리더와
`game-architect`의 몫이다. **`dev-platform`은 제안하지 않는다.**

### (C) 안내만 (설치 안내 / 첫 실행 시 노트북 감지 후 1회 안내 / 매뉴얼)

- **가장 안전하고 가장 약하다.** 상주 장식 앱 사용자의 대다수는 그래픽 설정을 만지지 않는다
  (`persona-newcomer`의 발견가능성 문제 그대로다).
- **그래도 버리지 않는다 — A와 병행한다.** 이유 두 개:
  1. **이미 배포된 zip 사용자에게는 A가 도달하지 않는다.** 새 빌드에만 적용되기 때문이다.
  2. **A가 게이트 G1에서 실패하면 C가 유일한 방어선**이 된다.
- 형태 권고: **레지스트리를 쓰지 않고**, `docs/`와 스토어 페이지·README에
  "노트북에서 배터리/발열이 신경 쓰이면 Windows 설정 → 시스템 → 디스플레이 → 그래픽에서
  StickMate.exe를 **절전**으로" 를 명시. `marketing`/`perf-doc` 인계 항목.

---

## 4. 채택 판정에 필요한 해석

### 4-1. ★ "GPU 평균이 1.56 → 3.70ms로 **올랐다**" — 무슨 뜻인가

**이건 회귀가 아니다. 지연 분포의 꼬리가 사라진 것이다.**

- 60fps 예산은 **16.67ms**다. 내장의 평균 **3.70ms는 예산의 22%** — 프레임을 놓치는 수준이 전혀 아니다.
- 외장의 최악 **222.48ms는 프레임 13장 분량의 스톨**이다. **이것이 사용자가 "렉"이라 부른 것**이고,
  평균 1.56ms라는 좋은 숫자 뒤에 숨어 있었다.
- 내장의 최악 **12.94ms는 예산 안**이다 → **프레임 드롭 0.**
- **상주 장식 앱에서 사용자 체감을 지배하는 것은 평균이 아니라 꼬리다.**
  "평균 2.4배 악화"와 "최악 17배 개선"은 같은 저울에 올릴 값이 아니다.

**GPU 점유 5.8% → 13.5%도 회귀로 읽으면 안 된다.** 이 값은
`gpuMeanMs × 실효제출장수 ÷ 10`인 **산술 추정**이고(`FramePacing.cs` 주기 요약),
**RTX 2050의 5.8%와 Iris Xe의 13.5%는 분모가 다른 하드웨어**다. 서로 비교할 수 없다.
절대 전력은 dGPU 쪽이 압도적으로 크다(엔트리 dGPU는 **아이들만으로도** 수 W를 먹는다).

### 4-2. ★ `Tasklist.md:108`의 채택 불가 조건 — **아직 측정되지 않았다**

> *"체감이 좋아져도 잔상/찢어짐 · 입력 지연이 나빠지면 채택 불가."*

**화질 (잔상/찢어짐)**
- 렌더 파이프라인 · 백버퍼 크기 · MSAA 요청은 GPU를 바꿔도 **우리 설정값 그대로**이고,
  그 셋은 매 5분 요약 줄에 그대로 찍힌다(`MSAA 요청 Nx`, `백버퍼 WxH`).
  → **원리상 화질 회귀는 예상되지 않는다.**
- **단, 미확인 축이 둘 있다**: (1) 벤더별 드라이버 기본 텍스처 필터링/AA 오버라이드가 다르다.
  (2) ★ **투명 합성 경로가 바뀐다.** 이 앱의 클릭 관통 · 픽셀 알파 투명 · 항상 위는
  D3D11 + **비-flip 모델 스왑체인**에 의존한다
  (`BuildStandalone.cs:492-501`, `useFlipModelSwapchain=false`). GPU를 갈아끼우면 DWM 합성
  경로가 달라진다. 그리고 이 저장소에는 **`WS_EX_LAYERED` + DWM 하이브리드 합성이
  "한 번 켜지면 안 꺼지는" 알려진 미해결 항목**이 있다 — 여기서 어떻게 반응할지는 **미지수다.**

**입력 지연**
- ★ **우리 계기로는 못 잰다.** `Tasklist.md:95-99`가 이미 Microsoft 1차 출처로 못박았다 —
  *"You cannot use GetFrameStatistics for swap chains that both use the bitblt presentation model
  and draw in windowed mode."* 우리 앱이 정확히 그 금지 조합이다.
  **Windows에서 이 앱의 프레임 타이밍 기반은 문서상 처음부터 유효하지 않다.**
- → **입력 지연은 사람 체감으로만 판정 가능하다.** 게이트 G2에 절차를 뒀다.

### 4-3. ★ 4분 표본의 한계 — 단순 미확인이 아니라 **경고 신호다**

`Tasklist.md`에 내장 최악 수치가 **두 개** 있다:

| 표본 길이 | 내장(Iris Xe) GPU 최악 | 출처 |
|---|---:|---|
| 4분 | **12.94 ms** | `Tasklist.md:124` |
| 10분 | **17.46 ms** | `Tasklist.md:115` |

**표본이 길어지자 최악이 커졌고, 17.46ms는 60fps 예산 16.67ms를 이미 넘었다(1프레임 드롭).**
표본이 다르니 단정은 못 한다. 그러나 **"장시간 축 미확인"을 낙관적으로 읽으면 안 된다는 증거**다.
사용자가 3시간 재측정 중이라 했으니, **그 결과가 게이트 G4다.**

측정 규칙(틀리면 결론 전체가 무효):
- ★ **`[렌더진단] ★A/B 요약` 줄로 장시간 비교를 하지 마라.** 그 줄은 **시작 후 60초에 한 번만**
  찍히고 이후 측정이 멈춘다(`RenderDiagnostics.Tick`의 `if (_summaryLogged) return;`).
  몇 시간에 걸쳐 자라는 현상을 그 줄로 재면 **반드시 "차이 없음"이 나온다**(`FramePacing.cs:759-766`).
- **장시간 A/B의 유일한 올바른 단위는 5분 주기 요약 줄이다**(구간마다 GPU 평균/최악을 비우고 다시 잰다).
- `GPU: 드라이버가 타이머 질의를 돌려주지 않음(표본 0)`은 **"부하 0"이 아니라 "측정 불가"다.**
  그 실행분의 GPU 수치는 버려라.

### 4-4. ★ 심각도 — **Windows 출시 차단(Release Blocker) 후보로 평가한다**

이건 사용자 1명의 환경 문제가 아니다.

- **RTX 2050은 엔트리급 노트북 dGPU다.** 가장 흔한 하이브리드 구성이며,
  Optimus/Enduro 노트북 사용자 **전원**이 같은 코드 경로를 탄다.
- **우리 앱은 24시간 상주한다.** 게임은 몇 시간 켜고 끄지만, 이 앱은 **켜 두는 것이 제품 컨셉**이다.
  dGPU를 24시간 깨워 두면 **배터리 주행시간과 팬 소음**에 그대로 꽂힌다.
  → 스토어 리뷰의 *"배터리 잡아먹는 앱"* 은 **회복이 불가능한 종류의 평판 손상**이다.
- **원칙 2(비침해)와도 정면으로 만난다.** 이 저장소는 이미 같은 계열의 사고를 한 번 잡았다 —
  `Screen.sleepTimeout` 기본값이 사용자의 디스플레이를 24시간 잠들지 못하게 막고 있었고,
  그 판정문이 `FramePacing.cs:806-828`에 남아 있다. **이번 건은 그것과 같은 성격이며 자릿수가 더 크다.**
- **판정**: Windows 빌드에 대해 **출시 차단 후보(Release Blocker candidate)**.
  최종 등급은 리더가 정하되, "성능 개선"이 아니라 **"상주 앱의 자원 예절"** 축으로 다뤄야 한다.

---

## 5. 채택 게이트 — 이 넷을 통과하기 전에는 "해결"로 표시하지 마라

| # | 게이트 | 무엇을 확인 | 실패 시 |
|---|---|---|---|
| **G1** | **A가 실제로 목적지에 도달하는가** (§2-5) | 패치된 빌드를 사용자 실기에서 **모든 GPU 설정을 "Windows 결정"으로 되돌린 상태**에서 실행 → 로그의 GPU 이름이 **Intel**로 뜨는가. **동시에 작업 관리자에서 dGPU 사용률/전용 메모리가 0인가**(이름만 바뀌고 dGPU가 켜져 있는 부분 성공을 걸러낸다) | A 폐기 → C로 후퇴, B 예외 승인 논의 |
| **G2** | **투명 · 클릭 관통 · 항상 위 무회귀** | 같은 세션에서 눈으로: (1) 캐릭터 뒤로 다른 창이 비치는가 (2) 빈자리 클릭이 아래 창에 가는가 (3) 다른 창 위에 남는가. **그리고 커서로 캐릭터를 빠르게 끌어 커서-캐릭터 간격이 커졌는지**(입력 지연 대용 측정, §4-2) | **즉시 채택 불가** — `Tasklist.md:108` |
| **G3** | **V3 오탐 없음** | 패치된 exe를 사용자 실기에 놓고 V3 실시간 감시 + 수동 검사. 격리/경고 0건 | A 폐기 → §2-2의 대안으로 이동(아래) |
| **G4** | **장시간(3시간) GPU 최악이 예산 안** | 5분 주기 요약 줄만으로 A/B(§4-3). **가동 시간을 x축으로 최악 ms를 늘어놓는다** | 원인이 dGPU가 아닐 수 있다 → 재조사 |

### G3 실패 시의 대안 (미리 적어 둔다)

**Unity 공식 커스텀 플레이어 빌드.** Unity가 `WindowsPlayer.sln` / `.vcxproj` / `UnityPlayerStub`을
통째로 동봉한다(§1-1 경로). `Main.cpp:9-10`의 리터럴을 `0`으로 고쳐 다시 컴파일하면
**바이너리 패치가 아니라 정상 컴파일 산출물**이 나온다 — AV 관점에서 성격이 다르다.
- 필요: **Windows + Visual Studio(C++) + Windows 10 SDK**
  (`PlatformToolset = $(DefaultPlatformToolset)`, `WindowsTargetPlatformVersion = 10.0` → 최신 VS면 된다).
- **이 개발 머신에서는 불가능하다.** 사용자 실기에 VS를 설치해야 해서 비용이 크다 →
  **G3가 실패할 때만 꺼낸다.**

---

## 6. 구현 형태 (설계만 — 코드는 판정 후)

`Assets/Editor/BuildStandalone.cs`(625줄, `namespace StickMate.EditorTools`)에
`IPostprocessBuildWithReport` 구현을 추가. **`report.summary.platform == StandaloneWindows64`일 때만** 동작.

**반드시 지켜야 할 것 넷** — 이것 없이는 붙이지 마라:

1. **오프셋을 믿지 말고 값을 검증한다.**
   `0x17a00` / `0x17a04`는 *이 Unity 버전의 이 템플릿* 기준이다. 마이너 버전이 올라 오프셋이 밀리면
   **엉뚱한 바이트를 덮어쓴다 — 이 접근의 진짜 사고 지점이다.**
   → **export 테이블을 파싱해서 심볼 이름으로 RVA를 찾고**, RVA→파일 오프셋을 변환하고,
   **현재 값이 `01 00 00 00`인지 확인한 뒤에만** 쓴다. 하나라도 어긋나면 **빌드를 실패시킨다**
   (조용히 넘어가면 이 저장소가 아홉 번 당한 거짓 통과가 된다).
2. **쓰고 나서 되읽는다.** 패치 후 export 테이블을 **다시 파싱**해 두 값이 0인지 확인하고,
   그 결과를 빌드 로그에 남긴다. 쓰기만 하고 안 읽으면 조용한 실패다.
3. **`Tests/EditMode/PlatformParityAuditTests.cs`에 항목 추가.**
   G1~G4를 통과하기 전까지는 `Assert.Fail`이 아니라
   **`Assert.Ignore("Windows 실기 미확인 — dGPU export 값 패치, 게이트 G1/G3 대기")`**
   로 남겨 러너에 계속 보이게 한다.
4. **정책은 플랫폼 중립 위치에.** *"하이브리드 GPU에서 어느 쪽을 쓸 것인가"*라는 **판정**은
   `Platform/`(중립)에 두고, PE 쓰기는 에디터 후처리의 **"적용"**만 담당한다.
   `FullscreenSuspendPolicy`가 `Platform/MacOS/` 안에 있어 Windows가 못 불렀던 사고를 반복하지 마라.
   (이번 건은 판정 자체가 빌드 타임 상수라 중립 위치에 **의도 문서 + 상수**만 두면 된다.
   macOS가 §7-2를 구현할 때 **같은 판정을 재사용**하게 하는 것이 목적이다.)

---

## 7. macOS 영향

### **판정: 현재 사용자 환경 기준 "영향 없음" / 잠재 갭은 "별도 배정 필요".**

둘로 나눠 쓴다 — 하나로 뭉치면 거짓말이 된다.

### 7-1. 지금의 영향: 없음 (확인됨)

- `NvOptimusEnablement` / `AmdPowerXpressRequestHighPerformance`는 **Windows PE export 전용**이다.
  Mach-O에는 대응 개념 자체가 없다. **권고안 A는 `StandaloneWindows64` 빌드 경로에서만 동작하므로
  macOS 산출물에 어떤 바이트도 닿지 않는다.**
- 개발/사용 머신은 **Apple M2 Pro**(`uname -m`=arm64, `system_profiler` 확인). **GPU가 하나뿐이라
  "외장으로 올라간다"가 물리적으로 성립하지 않는다.**

### 7-2. 잠재 갭 (별도 배정 필요) — **정확히 대응하는 현상이 macOS에도 있다**

출하 중인 `Builds/macOS/StickMate.app/Contents/Info.plist`를 전량 덤프해 확인했다:
**`NSSupportsAutomaticGraphicsSwitching` 키가 없다.**

이 키가 없는 앱이 Metal 컨텍스트를 만들면, **듀얼 GPU Intel Mac**(예: 2019 MacBook Pro 16" —
Intel UHD + Radeon Pro)에서 macOS가 **자동으로 디스크리트 GPU로 전환한다.**
**Windows의 `NvOptimusEnablement`와 성격·증상(발열·팬·배터리)이 같은 계열의 문제**다.
관련 지렛대로 Unity의 `-force-low-power-device`(macOS 전용 커맨드라인 인자)도 존재한다.

**왜 이번 라운드에서 안 고치는가:**
1. 사용자 머신이 Apple Silicon이라 **재현·검증이 불가능하다.** 고쳐도 초록을 만들 수 없다.
2. 이 키를 켜는 것은 **"GPU 전환을 앱이 감당할 수 있다"는 선언**이다. 전환 시점에 드로어블/디바이스가
   갈아끼워져도 **이 앱의 투명 오버레이 합성이 버티는지 검증 없이 켜면 위험하다**(§4-2와 같은 축).
3. `PlayerSettings`에 이 키를 넣는 API가 확인되지 않았다(§1-1 스캔에서 `lowPowerDevice` 등 **0건**).
   → plist 후처리 형태가 되는데, 그건 Windows PE 패치와 **같은 등급의 결정**이라 별도 판정이 필요하다.

**후속 라운드 요청**: `Tests/EditMode/PlatformParityAuditTests.cs`에
**"하이브리드 GPU 선택 정책 — Win: PE export(값 1, 미제어) / mac: `NSSupportsAutomaticGraphicsSwitching`(부재)"**
항목을 추가하고 **`Assert.Ignore`(사유 포함)** 로 남길 것.
★ 이번 라운드는 산출물이 보고서 하나라 테스트 파일을 건드리지 않았다 —
**이 줄이 그 갭을 잊지 않게 하는 유일한 장치다.**

---

## 8. 사용자가 오늘 낮에 할 것 (게이트 G1·G2용 절차)

빌드가 아직 안 나왔으므로, **오늘 확보할 것은 "현재 수동 설정이 유지되는지"와 "G2 체감 축"이다.**

### 8-1. 로그 경로

```
%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate\Player.log        ← 현재/직전 실행
%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate\Player-prev.log   ← 그 이전 실행 (A/B용)
```
(`Vibelab`/`StickMate`는 `ProjectSettings.asset:15-16`의 `companyName`/`productName`과 일치 확인.)
**Unity는 실행 때마다 `Player.log`를 `Player-prev.log`로 밀어낸다** — 전/후 실행을 한 번에 대조할 수 있다.

### 8-2. ★ PowerShell 한 줄

```powershell
Select-String -Encoding utf8 -Path "$env:USERPROFILE\AppData\LocalLow\Vibelab\StickMate\Player*.log" -Pattern 'Renderer:|VRAM=|FramePacing' | ForEach-Object { "$($_.Filename):$($_.LineNumber): $($_.Line)" }
```

| 패턴 | 무엇 | 언제 |
|---|---|---|
| `Renderer:` | **Unity 플레이어 자신이 찍는 GPU 이름**(`Direct3D:` 블록의 `Renderer:`/`Vendor:`) | **시작 직후 몇 초** — 가장 빠른 답 |
| `VRAM=` | 우리 코드의 `[렌더진단] 콜드스타트` 줄. `그래픽API=Direct3D11 (<GPU 이름>, …), VRAM=…MB` | 창 부착 직후 **1회** |
| `FramePacing` | 5분 주기 요약. `DescribeRunIdentity()`가 `GPU <이름>`과 `가동 N시간M분`을 같은 줄에 넣는다(`FramePacing.cs:795`) | 첫 줄 **60초**, 이후 **300초마다** |

> **GPU 이름만 볼 거면 `Renderer:`/`VRAM=`로 몇 초 안에 끝난다.** 5분 요약은 §4-3의 장시간 축(G4)용이다.
> (상수: `FramePacing.cs:651` `FirstTierSummarySeconds=60f`, `:645` `TierSummaryIntervalSeconds=300f`.)

### 8-3. 판정 기준

- **성공**: `Intel(R) Iris Xe Graphics` 등 **내장** 이름.
- **실패**: `NVIDIA GeForce RTX 2050` 등 **외장** 이름.
- ★ **부분 성공에 속지 마라**: 이름은 내장인데 **작업 관리자 → 성능 → NVIDIA 항목의 사용률/전용 메모리가
  0이 아니면**, D3D 디바이스만 옮겨졌고 dGPU는 계속 켜져 있는 것이다.
  **로그와 작업 관리자를 반드시 둘 다 봐라.**

### 8-4. 보존

```powershell
Copy-Item "$env:USERPROFILE\AppData\LocalLow\Vibelab\StickMate\Player.log" "$HOME\Desktop\stickmate-dgpu-after.log"
```

---

## 9. 미확인 목록 (이 보고서가 확인하지 *못한* 것)

**아래는 전부 "판단"이지 "확인"이 아니다.**

1. **`NvOptimusEnablement=0`이 실제로 내장으로 귀결되는가** — §2-5. **가장 중요한 미확인.**
2. GPU를 내장으로 바꿨을 때 **투명 / 클릭 관통 / 항상 위가 유지되는가** — §4-2. **위험 항목.**
3. **입력 지연 변화** — 우리 계기로는 원리상 측정 불가(§4-2). 사람 체감뿐.
4. **V3가 패치된 exe를 오탐하는가** — 이 머신에서 확인할 방법이 없다.
5. **장시간(3시간) 내장 GPU 최악 ms** — 4분 12.94 → 10분 17.46의 상승 추세가 이어지는가(§4-3).
6. 드라이버 버전/제조사 프로파일에 따라 "요청 없어도 D3D 앱이면 외장"인 구성이 존재하는가.
7. `Assets/Editor/BuildStandalone.cs`에 후처리를 붙였을 때 **Unity가 산출물을 다시 만지는 단계가
   뒤에 있는지** — `IPostprocessBuildWithReport`의 실행 순서가 압축/서명 단계보다 앞인지 뒤인지
   **실기에서 확인해야 한다.**

---

## 부록 — 이 보고서가 실행한 검증 (재현용)

| 확인한 것 | 방법 |
|---|---|
| export 4개와 값 | PE optional header → data directory[0] → `IMAGE_EXPORT_DIRECTORY` 직접 파싱, RVA→파일오프셋 변환 후 DWORD 읽기 |
| 템플릿 동일성 | `shasum -a 256` 로 출하 exe ↔ Unity `Variations/win64_player_nondevelopment_mono/WindowsPlayer.exe` 대조 |
| export name 오름차순 | 파싱한 이름 테이블 순서 확인 |
| 서명 부재 | data directory[4](Certificate) 크기 = 0 |
| 체크섬 | optional header + 64 = `0x0` |
| PlayerSettings API 부재 | `UnityEditor.dll` / `UnityEditor.CoreModule.dll` 문자열 스캔 |
| 드라이버가 읽는다는 근거 | Windows `UnityPlayer.dll`에 `NvOptimus`/`PowerXpress` 문자열 **0건** |
| boot.config 키 전량 | Windows `UnityPlayer.dll` 문자열 추출 후 키 패턴 필터 |
| 근원 코드 | `WindowsStandaloneSupport/Source/WindowsPlayer/WindowsPlayer/Main.cpp:1-13` 직접 열람 |
| 아이콘 미설정 | `ProjectSettings.asset:295` `m_BuildTargetIcons: []` |
| macOS plist | `plutil -p Builds/macOS/StickMate.app/Contents/Info.plist` 전량 덤프 |
| 로그 상수/문자열 | `FramePacing.cs:645,651,735,759-766,787-803`, `LogSnapshot`, `ProjectSettings.asset:15-16` |

### 참고 문헌
- [NVIDIA Optimus — Rendering Policies (우선순위 표, `NvOptimusEnablement` 값 정의)](https://archive.docs.nvidia.com/gameworks/content/technologies/desktop/optimus.htm)
- [NVIDIA — Enabling High Performance Graphics Rendering on Optimus Systems (PDF)](https://developer.download.nvidia.com/devzone/devcenter/gamegraphics/files/OptimusRenderingPolicies.pdf)
- [AMD GPUOpen — Selecting the Best Graphics Device to Run a 3D Intensive Application](https://gpuopen.com/learn/amdpowerxpressrequesthighperformance/)
- [Unity 6000.0 Manual — Player command line arguments (`-force-device-index`, `-force-low-power-device`)](https://docs.unity3d.com/6000.0/Documentation/Manual/PlayerCommandLineArguments.html)
- [Microsoft Learn — DXGI_GPU_PREFERENCE](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/ne-dxgi1_6-dxgi_gpu_preference)
- [Microsoft Q&A — GPU Preferences not taking effect in Win11](https://learn.microsoft.com/en-us/answers/questions/3998315/gpu-preferences-not-taking-effect-in-win11)
