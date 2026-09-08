# 사용자 지시 체크시트

★ **이 문서의 목적 하나뿐이다** — 사용자 지시: *"내가 지시한사항들에대해서 총괄은 자꾸 잊어버리는거
같아 내가 지시하면 채크시트 문서신규로 만들어서 거기에 입력하고 완료시 체크하는 형태로 진행해줘"*
(2026-09-08).

**규칙**:
- 사용자가 지시를 주면 **그 즉시** 아래에 `- [ ]` 항목으로 적는다(작업을 시작하기 전에 먼저 적는다).
- 끝나면 `- [x]`로 바꾸고, 그 아래에 **한 줄로** 무엇을 했는지 남긴다(커밋 해시가 있으면 함께).
- 지시가 여러 하위 작업으로 쪼개지면 하위 체크박스로 중첩한다.
- `Tasklist.md`/`process.md`를 대신하지 않는다 — 저 둘은 팀 전체 진행상황·단계 로그이고, 이 문서는
  **"사용자가 직접 말한 것"만** 추적하는 좁고 빠른 목록이다.

---

## 진행 중 / 대기

- [ ] **오늘 할일 위젯 고도화** — "팀에이전트+페르소나 5명 추가해서 오늘할일 창 고도화 진행 필요,
      토론 먼저 10라운드 진행 후 계획 및 구현 진행" + "창 사이즈 자체를 키워야함" + "오늘할일/내일할일
      두가지다 입력가능해야하고" + "체크시 완료로 이동하는건 맞는데" + "달별이나 일별로 확인
      가능해야하고" + "완료항목 및 미완료항목도 볼수있어야함"
  - [x] 10라운드 토론 — 5명(ux-widgets/coder-systems/design-motion/persona-stress/persona-immersion)
        전원 완료.
  - [x] 종합안 작성(상충 지점 리더 판정) 완료 — `docs/UX_WIDGETS.md` R6(ux-widgets 설계, 500×512
        10행 · v13 스키마 · 2페이지 일별/달력) + R6-13(리더 판정, persona-stress의 Tasklist.md
        코드감사 R-1~R-10과 교차 병합, 커밋 `2dfc4a3`). 핵심 판정: 500×512 채택 / v13 승격 /
        **PopoverPanel 화면 클램프 기구 신설을 선행 작업으로 편입**(persona-stress가 찾은 구조적
        결함 — 크기와 무관하게 작은 화면에서 넘침, `CharacterInfoWindow.Layout.ClampPanelToScreen`과
        같은 계약으로 이식).
  - [x] 구현 계획 수립 완료 — 착수 순서를 의존관계로 못박음: ⑤클램프 기구 → ③스키마v13 →
        레이아웃 → 포스트잇 필터 → 말줄임/취소선(독립, 아무때나 가능). 두 갈래로 병렬 착수:
    - [ ] **1단계(착수함, 아래 진행 로그)**: coder-systems(PopoverPanel 클램프+v13 스키마+
          FramePacing hold+저장 절제) ∥ coder-ui(말줄임+취소선 통일, 스키마 무관·즉시 가능)
    - [ ] **2단계(1단계 완료 후)**: 500×512 레이아웃 재구축(일별/달력 2페이지) + 달력 클릭 라우팅
          (R-5) + 포스트잇 오늘자 필터(R-7) + 포스트잇↔팝오버 `TryClaimAction` 교차 클릭 충돌
          버그(P0, 아래 별도 항목과 통합)

- [ ] **포스트잇↔팝오버 클릭 충돌 버그** (persona-immersion이 실기 검증 중 발견, 오늘할일 고도화와
      별개의 현재 결함) — [추가] 버튼 클릭이 동시에 자란 포스트잇 체크박스에도 먹혀 항목이 자동
      완료되고 재화 300동전이 소진됨. `TryClaimAction`이 컴포넌트마다 따로 있어 서로 다른 두 표면이
      같은 클릭을 각자 처리하는 것을 막는 장치가 없음. 원인 코드 확인 완료(4벌 존재, 교차 방어 0건).
      → 위 고도화 2단계(P0)에 묶어 같은 파일(`TodoPostItWidget.cs`/`PopoverPanel.cs`) 만지는
      라운드에서 함께 처리하기로 함(소은 본인 권고 + 병합 충돌 회피).

- [x] **종이비행기 펫 궤도 범위 수정** — "이 다음 할일은 지금 종이비행기같은경우 몸의 반쪽까지만
      주위를 돌고있어 척추를 중심으로해서 반경이 정해져있는데 몸 바깥쪽으로 돌수있게 범위 수정해줘"
      (2026-09-08). 원인: 같은 날 코스튬 DLC 작업(design-motion 14-5, D-6 — 궤도가 신규 코스튬
      프롭 4종과 겹침)이 종이비행기 궤도를 **상시** 뒤쪽 반원으로 접어 놨었는데, 그게 코스튬을 아예
      안 입은 평소에도 걸려 있던 것이 이 신고의 실체였다. `CharacterPetRenderer.cs` — 반원 접기를
      `CostumePropRenderer.PropPlaced`(프롭이 실제로 서 있을 때)로만 게이트하도록 수정 —
      코스튬 미착용/적응기·한계 구간에는 완전한 타원(몸 양쪽을 다 돈다)으로 복귀, 몰입기에 프롭이
      실제로 떠 있을 때만 겹침 회피용 반원 유지. `PetPlaneOrbitRadiusTests.cs`에 신규 PlayMode 회귀
      테스트(`코스튬_프롭이_없으면_궤도가_완전한_타원이다`) 추가. EditMode 2,829건 실패 0 / 해당
      PlayMode 4건(신규 포함) 전부 통과. 커밋 `4379004`, Windows 릴리즈 `windows-preview-20260908i`에
      포함되어 게시 완료.

- [ ] **코스튬 DLC × 집중 모드 연동** (PART 2 스펙 전문 수령, 2026-09-08) — "저번에 요청한 이내용들은
      어떻게 됐어 아직 안되어있으면 체크시트에 추가하고 바로 병렬로진행". ★ 조사 완료(코드 직접 확인,
      2026-09-08) — **엔진/시스템 계층은 이미 대부분 완성돼 있고, 남은 것은 대부분 콘텐츠(세트 3종
      미제작)다.** 아래는 실측 결과.
  - [x] 세이브 스키마 — `CharacterSaveStore.cs:512,517` `costumeFocus`(레코드 배열) +
        `costumeFocusMinutesToday`, `FirstVersionWithCostumeFocus = 12`로 이미 v12에 포함됨.
  - [x] 2-3 필수: 코스튬별 독립 누적 카운터 — `CostumeProgressModel.AddFocusMinutes`가 유일한 입구,
        `MinutesOf(costumeKey)`로 세트별 조회.
  - [x] 2-3 필수: 정보창 UI 노출 — `CharacterInfoWindow.Stats.cs:963,1053`에서
        `CostumeProgressModel.MinutesOf` + `CostumeEvolutionRules.PercentTenthsToNextBoundary`로
        누적 시간과 다음 진화까지 남은 비율을 이미 표시 중.
  - [x] 2-4 세션 내 실시간 미세 변화(적응기/몰입기/한계 3단계) — `Core/FocusSessionPhase.cs`의
        `FocusSessionPhase`(Adapt/Immersion/Limit) + `FocusSessionPhases.Of`로 구현 완료.
    - [x] ★ 사용자 수정 지시(2026-09-08) "시간 설정에 비율로 해야함" — **이미 반영되어 있음.**
          고정 25분이 아니라 `EdgeSeconds(D) = min(clamp(0.20·D, 60, 300), 0.40·D)` 하이브리드 공식
          (순수 비율만 쓰면 60분 세션 적응기가 12분이 되고, 순수 절대값만 쓰면 1분 세션에서 적응기가
          세션보다 길어지는 두 파손을 모두 막음, 코드 주석에 근거 명시). 1~60분 임의 세션 길이 지원
          확인(`FocusSessionPopover.MinimumSessionMinutes=1`/`MaximumSessionMinutes=60`). **추가 구현
          불필요 — 사용자에게 "이미 되어 있다"고 보고할 것.**
  - [x] 2-5 성능 제약 — `CostumePropRenderer.cs`가 "한 번 만든 뒤 아무도 안 만진다"(재드로우 0) 패턴으로
        빌드, FramePacing/Still등급 충돌 검증용 `CostumeFocusStillTierTests.cs` 존재 확인.
  - [ ] **2-2 세트별 집중 모드 행동 4종 — 광부/노가다 · 사이버펑크 연구원 · 판타지 대마법사 · 현대
        직장인/독서실.** 실측: **독서실(office) 세트 1개만 데이터 존재**
        (`CostumeManifest_office.asset` + `CostumeKeyposeTable_office.asset`). **광부·사이버펑크·
        대마법사 3종은 매니페스트/키포즈 에셋 자체가 없음.**
    - [x] design-motion 완료(`docs/UX_MOTION_COSTUME_FOCUS.md` §16) — 3세트 전부 4프레임 키포즈
          사이클 표 + 몰입기 3소구간(진입/절정/이완) 감정선 + 프롭 접촉좌표 확정. 정정: "완전
          미착수"는 데이터(.asset) 기준으로만 참 — 각도표·프롭좌표 자체는 09-07 라운드에 이미
          있었고 이번 라운드는 "전사+재검산"이었음. **재검산이 결함 4건을 새로 잡음**:
          (가) 09-07 각도표가 몸 기울임 오프셋을 안 넣어 광부 K2 손끝~섬광 원점이 실제 2.76pt
          벌어져 있었음 — 이번에 수정 (나) 대마법사 조형이 바뀐 뒤 낡은 판정 잣대 사용 중이던 것
          교정 (다) `CostumeKeypose.leanDegrees` 툴팁이 실제 클램프값(30°)과 다른 7.60°를 적어
          거짓 주석이었음 — **coder가 즉시 정정함**(아래) (라) `RelaxLeanBiasDegrees` 1.5°가
          설계 자신의 가시 하한(1.0pt)의 57%뿐이라 안 보이는 수준 — **coder가 2.8°로 조정함**
          (아래, EditMode 2,829건 실패 0 확인).
      - [x] **coder 즉시 반영 2건**: `CostumeKeyposeTableSO.cs`의 `leanDegrees` 거짓 툴팁 정정
            (7.60→실제 30) / `RelaxLeanBiasDegrees` 1.5°→2.8°(가시 하한 미달 수정). 둘 다 문서
            결함·수치 보정이라 즉시 적용, 검증 통과.
      - [ ] **리더 판정 남은 3건**: J-3(§5 접촉오차 숫자 문서 정정, 문서만) / J-4(각도 3건 채택
            여부 재확인) / J-5(착수 순서 = 코호트(ItemCatalog) → 매니페스트 → 키포즈표 — 순서
            뒤바뀌면 `CostumeResolver`가 못 찾음, coder-systems 착수 시 지킬 것).
      - [ ] design-equipment(조형, 진행 중) + coder-systems(에셋 배선+ItemCatalog DLC 엔타이틀먼트
            등록, 착수 전 — 위 J-5 순서 지킬 것) 병렬 착수(2026-09-08 지시).
  - [ ] **2-3 시각적 성장 콘텐츠 — 단계별 실제 비주얼.** 메커니즘(`CostumeStageOverride`,
        `ResolveStageShapes`)은 있으나, office 세트조차 `stage: 0`(기본형) 하나만 있고 10h/50h/100h
        진화 오버라이드가 **비어 있음**. 4세트 전부에 대해 단계별 조형 데이터 신규 저작 필요.
  - [x] product-strategy 완료(10회차, `docs/strategy/CHANNEL_PRICING_DECISIONS.md`) — **가격은
        한 칸도 안 바뀜**: 독서실 $0(BaseTheme 4종, 이미 무료로 존재) / 사이버펑크·대마법사 각
        $4.99. ★ **광부는 스펙 문구("기본 or 인게임 골드")와 달리 사용자가 이미 "유료 DLC로,
        골드구매 SKU 신설 없음"으로 확정해 둔 과거 결정이 있었음**(Tasklist.md:25632) — 골드
        경로 제안은 하지 않음(맞는 판단, 구조적으로도 42종 등급 붕괴를 막음). 신규: 코스튬 라인
        3팩 번들 $12.13 제안. **진화 가속권(시간 돈으로 사기)은 기각** — 근거 5개 중 2개가
        이미 프로덕션 코드(`CostumeEvolutionRules` C-7 / `CostumeEntitlement` C-3)에 못박혀 있어
        판매 자체가 구조적으로 막혀 있음. 출시 순서: 4종 동시 출시 기각, 무료 독서실(1.0) →
        사이버펑크 → 광부/대마법사 순.
    - [ ] ★★ **신규 발견(P0급, 별도 배정 필요)** — `CostumeCatalog.AuditSource`가 BaseTheme
          코스튬 감사에서 `mil` 하나만 막고 `cyber`/`neon`/`sport`/`ink`는 통과시키며, 회귀
          테스트(`CostumeManifestCorridorTests.cs:458`)가 그걸 **양성 대조로 명시 단언**하고
          있음. 즉 나중에 `costume.cyber`를 (Pack이 아니라) BaseTheme으로 저작하면 $4.99짜리
          팩 간판이 동전 6,600(1.7일)에 새어나가는데 감사도 테스트도 초록으로 통과함. **지금
          design-equipment/coder-systems가 만들 신규 코스튬 매니페스트(사이버펑크/대마법사)는
          반드시 `sourceKind: Pack`으로 저작해야 한다** — coder-systems 착수 시 이 제약을 전달할
          것. 감사 로직 자체 강화는 test-engineer/security 별도 배정 필요.

- [x] **말풍선 대사(멘트) 전체 검수** (2026-09-08) — "체크시트에 멘트 정리도 추가해줘... 멘트들
      전체 검수가 필요함". design-narrative가 대사 리터럴 63줄(13개 소스 파일) 전수 스캔 — 골든
      파일(`DialogueBudgetKoGolden.txt`) 63줄과 정확히 일치해 전수 소진을 교차 확인함.
      **5건 확정, 그중 4건(문안 교체) 즉시 적용 완료:**
  - [x] `LedgeHangState.cs:211` "어우... 꽤 깊네" → **"어우... 아찔하네"** — 사용자 지적("높은창인데
        깊은~~") 그대로. 높은 곳에 매달려 아래를 볼 때의 감각(아찔함)으로 방향 교정, 10자 유지로
        발화 예산(1.030초 ≤ 최악 체류 1.12초) 불변 확인.
  - [x] `AmbientChatter.cs:149` "다리가 잘 나가네" → **"발걸음이 가볍네"** —
        "다리가 나가다"는 탈진/골절 관용이 먼저 와 의도(잘 걷는다)와 반대로 읽힐 수 있음. 9→8자로
        오히려 예산에 여유가 생겨 회귀 위험 없음.
  - [x] `RopeClimbState.cs:677` "밧줄이다!" → **"여긴 밧줄이 낫겠다"** — 밧줄을 던지는 주체가
        캐릭터 자신인데 발견형 감탄이었던 화자·주체 불일치 수정(원래 "design-narrative 몫" 플레이스홀더로
        명시돼 있던 자리).
  - [x] `ParkourClimbState.cs:281` "가뿐하네" → **"올라가 보자"** — 등반 141회 중 137회(97.2%)가
        이 한 줄인데 "자기 키의 94.5%를 기어오르며 가볍다"는 모션과 어긋남. 노력 정도를 주장하지
        않는 문장으로 교체, design-motion의 향후 임계 재조정(0.95→0.4109H) 양쪽에서 안전 검증됨.
  - [x] `docs/localization/verify/golden_gen.py`로 골든 파일 재굽기 완료(손으로 안 고침).
  - [x] 검증 — EditMode 2,829건 실패 0(교체 4건 전부 반영), exit 0.
  - [ ] **5번째 건 — 구조 수정(coder 배정 필요)**: `LedgeHangState.cs:185-187` — 내려갈 발판이
        없을 때(`_hasDescendTarget == false`) `DropHeightUnits`가 0이 되어 "가장 얕은 낙차"와 같은
        값으로 위장, "여기로 내려가자"가 잘못 나갈 수 있음(엣지 케이스). 문안이 아니라
        `LedgeHangDialogueParams`에 `HasDescendTarget` 플래그 추가가 필요 — 아직 미착수.
  - [ ] 경계선 관찰 3건(교체 보류, design-narrative 의견) — "밤이 깊었네"(22시 정각 근처 과장 소지),
        "저쪽으로 가볼까"(걷는 중인데 숙고형 어미), "심심해서 왔어..."(RunawayState, IdleLines에서
        이미 삭제한 "심심하다" 계열과 선례 불일치) — 리더 판단 대기, 급하지 않음.
  - [ ] 죽은 대사 3줄 발견(문안 문제 아님, 배선 문제) — "어? 딴 데 보고 있네?"(FocusNudge 호출부
        0건), "한 발 더!"/"오늘은 여기까지"(Attack 상태 프로덕션 진입 0건) — 되살릴지 여부는 별도
        판단 필요, coder 배정 대상.

- [ ] **이 체크시트 자체를 계속 쓸 것** — "내가 지시하면 채크시트 문서신규로 만들어서 거기에
      입력하고 완료시 체크하는 형태로 진행해줘" (2026-09-08). 이 문서 신설로 최초 지시는 이행.
      **앞으로 매 지시마다 이 파일을 갱신하는 습관 자체가 계속 진행 중인 항목.**

---

## 완료

- [x] **밧줄등반 목표 선정 랜덤화 + 배회 거리 화면비례 수정**
  - "낮은창과 높은창이 있으면 무조건 낮은창에 밧줄던짐" + "높은창에서 낮은창으로 밧줄만 안던지게
    하고 낮은창에서는 높은창으로 밧줄던져서 이동가능... 올라갈때는 낮은창이든 높은창이든 랜덤" +
    "일단 한번 추첨해서 걸으면 좀 많이 이동해야하는데 너무 짧게 이동함"
  - `TryPickClimbTarget`을 최단거리 결정론 → 균등 무작위 선택(높이 필터로 「높은→낮은 금지」는
    이미 구조적 보장 확인)으로 교체. `wanderWalkTargetScreenFraction`/`…CapSeconds` 신설해 걷기
    지속시간에 화면 비례 바닥값 추가. EditMode 2,829건 실패 0, PlayMode 51/51 통과.
  - 커밋 `de3fb32` 완료, macOS 빌드 갱신 완료(driver.sh test/build 경로로 확인), Windows 크로스
    빌드도 이전 라운드에서 exit 0 확인됨(89,001,835 bytes).
  - [x] zip 압축 + 태그 + 원격 push 완료 — `windows-preview-20260908h` 태그(코드기준 `de3fb32`,
        기존 빌드산출물 `Builds/Windows/`와 DLL 타임스탬프 일치 확인)를 push했고, zip
        (32.4MB)은 스크래치패드에 있음.
  - [x] ★ **GitHub Releases 게시 완료** — 원인 규명: 이 환경(컨테이너/샌드박스)에 `gh` 바이너리
        자체가 없었을 뿐, 인증정보(macOS 키체인)는 이미 살아 있었다(사용자 확인: "계속 여기서
        했는데" — 즉 예전 라운드들은 실제로 여기서 됐던 것이 맞고, 이번에 환경이 초기화되며
        `gh` 바이너리만 사라진 것). GitHub 공식 배포처(`github.com/cli/cli/releases`)에서
        `gh` 바이너리를 직접 받아 `~/bin/gh`에 설치 → `gh auth status`가 기존 키체인 토큰을
        그대로 인식(재로그인 불필요, 자격증명을 직접 열람하지 않고 `gh` 자신이 정상 경로로 읽음).
        `windows-preview-20260908h` 릴리즈를 zip 자산(32,360,962 bytes, 코드기준 `de3fb32`)과
        함께 게시함: https://github.com/moonkj/StickMate/releases/tag/windows-preview-20260908h
  - [x] 최신본 재게시 완료 — `StickMate.EditorTools.BuildStandalone.PerformBuildWindows` 재실행
        (exit 0, DLL 갱신 확인) → `windows-preview-20260908i` 태그(코드기준 `f35cf04` = 종이비행기
        궤도+대사 4건+코스튬 모션 상수 조정 전부 포함) → zip(32,361,187 bytes) 첨부해 게시:
        https://github.com/moonkj/StickMate/releases/tag/windows-preview-20260908i
