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

- [ ] **DLC 12종 렌더링 아이콘 제작(참고 이미지 스타일)** (2026-09-08) — 사용자가 그라데이션·
      금속광택·보석반사가 있는 참고 이미지를 주고 "방금 준 이미지랑 최대한 비슷하게 너가
      만들어줘" → 이미지 생성 도구 없음을 확인 후 대안(Python/PIL로 직접 그라데이션·글로우
      코딩) 샘플 1개(광부 헬멧) 승인받음(품질 한계 사전 고지) → "전부진행" 확정.
  - [x] 12종 PIL 버전 1차 제작 완료했으나 **사용자 판정: "차이가 너무 큼"** — 품질 격차를
        솔직히 인정(이미지 생성 도구 부재가 근본 원인). 사용자가 ChatGPT로 직접 생성한
        이미지 제공 쪽으로 전환.
  - [x] 사용자가 ChatGPT로 만든 몽타주(광부 4종+대마법사 8종... 정정: 광부 4종+대마법사
        4종 = 8종, 한 장에 2×4 배치)를 다운로드 폴더에서 확보 → 배경색 기준 자동 경계
        검출로 8종 개별 크롭·중앙정렬·512×512 정규화 완료 → **기존 PIL 파일과 같은
        경로에 덮어써서 교체**(`Assets/_Project/Resources/Items/Icons/pack_mine_*.png`
        4종, `pack_arcane_*.png` 4종). 품질 확연히 개선 확인(사용자 참고 이미지와
        동급). **사이버펑크 4종(Patched Hood/Slit Visor/Cable Collar/Tarp Cape)은
        아직 PIL 버전 — 사용자에게 같은 스타일로 추가 요청함.**
  - [ ] 게임 통합 착수함(아래 진행 로그) — 카드/상점 아이콘 전용(리더 판단: 캐릭터
        착용 시 모습은 기존 벡터 유지, 스타일 불일치 위험 최소화 목적).

- [x] **장비 디자인 고도화 — 완료** (2026-09-08) — "각 장비들의 디자인 고도화가 필요함
      게임 디자이너 채용해서 고도화진행해줘". `design-equipment`(Fable 모델) 배정,
      사양 R2 완료 + coder-systems 착지 완료 + macOS 실기 캡처로 사용자에게 전/후
      비교 전송 완료(아래 진행 로그).
  - [x] design-equipment 사양 R2 완료 — **사용자 지적이 실측으로 확인됨**: 팩 12종이
        기본 42종 평균 대비 조각 수 0.60배·점 개수 0.19배·곡선 조각 0.13배로 확연히
        단순했음(인계본 계열 대비는 1/5~1/20). 원인: 광부·대마법사는 사양서 자체가
        이미 폐지된 v1 정원 제약으로 저작됐고, 사이버 4종은 **애초에 design-equipment
        사양서 자체가 없었음**(coder-systems가 직접 최소 문법으로 착지시킴). 새 R2
        사양(조각 3.17→6.5·점 22.4→53.5, 인계본 조각 문법 적용)이 12축 게이트 전수
        통과 검증됨. 부수 성과: 오늘 발견된 "Patched Hood↔베레모 카드 판별 미달"
        (0.100, 하한 0.15)도 실루엣 재설계로 해결(0.424로 재계산). 문서
        `docs/EQUIPMENT_SHAPE_SPEC_PACK_DETAIL_R2.md` + YAML 12장
        (`design/equipment/pack_detail_r2/`) 산출, 프로덕션 파일 0줄.
  - [x] coder-systems 착지 완료 — R2 사양(조각 3.17→6.5개·점 22.4→53.5개) 12종 에셋
        반영, 왕복 검증(1,326개 필드 대조 불일치 0, 양성 대조로 계기 생존 확인).
        RED(Patched Hood↔베레모 판별 42.4%로 독립 재확인)→GREEN(대장 정리 후 실패 0).
        EditMode 2,920건 실패 0. **macOS 재빌드+실기 캡처로 최종 확인**(스크린샷
        사용자에게 전송 — 수정 전/후 비교, DLC 탭 12종 카드 전체). design-equipment가
        미확인으로 남긴 3건 전부 실기로 판정 완료(망토 자락 흔들림/후드-바이저 겹침/
        하이라이트 가시성 — 전부 조치 불요로 판정). 커밋 `dca4f08`.
      - [ ] 참고(조치 불요, 인지만) — 검증 중 실수로 앱이 한 번 종료됐으나 정상 저장
            경로였음(데이터 손실 없음). `characterScaleSaved` 플래그가 false→true로
            바뀌었으나 실제 값(0.75)은 배포 기본값과 동일해 무해 — 되돌리려면 앱 종료
            상태에서 세이브 파일 수동 편집이 필요해 지시 없이는 안 건드림.

- [x] **DLC 팩 3종 상점 노출 + 맥으로 실행 확인 — 완료** (2026-09-08) — "상점 노출까지
      진행하고 맥으로 실행해줘". `CharacterInfoWindow.Shop.cs`가 이미 "DLC 팩은 동전
      상점에서 의도적으로 제외한다(결제 백엔드 없음)"고 문서화해 둔 것을 그대로 지키며
      **5번째 탭 [DLC] 신설**(기존 동전 상점 필터는 한 글자도 안 건드림). 3팩×4종=12장을
      카드로 노출, 가격 `$4.99`(문서값 그대로, 하드코딩 없이 배선). 결제 채널이 없으므로
      구매 버튼·확인창·재화 차감 없이 "지금은 결제 창구가 없어 전부 열려 있다"고 정직하게
      표시, [착용] 버튼은 실제 동작. 뮤테이션으로 "동전 상점에 팩 0건" 네거티브 컨트롤
      검증. **macOS 실기 캡처로 확인 완료**(사용자에게 스크린샷 2장 전송 — DLC 탭 12장
      전체 뷰 + Miner Helmet 실제 장착 확인). EditMode 2,920건 실패 0. 커밋 `ce3b102`.
  - [ ] 리더 판단 대기 2건(이번 범위 밖 발견) — (a) 팩 아이템이 이미 [장비] 탭에
        컨텍스트·가격 없이 기본 아이템과 섞여 있었음(레벨1 기본 요구치라 자동으로
        보유 처리됨) — 카드 탭 규칙 자체는 안 건드렸으므로 그대로 남음, ux-designer/
        game-architect 판단 필요. (b) 팩 표시명이 로컬라이즈 키(`pack.cyber` 등) 그대로
        화면에 노출됨 — design-narrative/marketing 배정 필요(한글 상품명 확정).

- [x] **광부·대마법사 팩도 사이버펑크처럼 실구매 배선 — 완료** (2026-09-08) — "Dlc팩
      다른것들도 있잖아". `pack.mine`(코호트 7 · `packmine` · 33°) · `pack.arcane`
      (코호트 8 · `packarcane` · 138°)를 사이버 파일럿과 같은 패턴으로 착지 — 팩 아이템
      8종(스탯 4슬롯) + 매니페스트 2개, `entitlements: []`, `ItemCatalog.cs` 0줄.
      ★ **예측 적중** — 사이버 때 30건을 낸 13개 테스트 파일이 이번엔 **0건**(RED
      재현에서 딱 1건, 그것도 단순 색상 대장 등재 누락)이었다. `BaseCohortScope`의
      코호트 일반화가 사이버 전용이 아니었음이 실증됨 — 3번째 팩부터는 정말로 "에셋만
      추가"가 됐다. 동결 보조색 2건이 사이버 `#518C84`와 똑같은 `WornColor` 명도 하한
      함정에 걸려 있던 것도 발견해 최소 이동으로 수정(문서·테스트·에셋 3곳 동기화).
      `CostumeResolver`가 두 팩을 실제로 돌려주는 것을 임시 프로브로 확인 후 착지 안
      시키고 삭제. 최종 EditMode 2,909건 실패 0(리더가 재검증 완료). 커밋 `85c4db0`.
      **이제 3종 DLC 팩(사이버/광부/대마법사) 전부 엔진상 구매 가능** — 상점 UI 노출/
      실제 결제 연동은 아직 별도(product-strategy 출시순서: 독서실 무료 → 사이버 →
      광부·대마법사, 상점 배선 자체는 이번 범위 밖).
      - [ ] 리더 인지 필요(급하지 않음) — 밀리터리·스포츠 팩(아직 미저작) 동결 보조색도
            같은 명도 함정에 걸려 있음(`#798C51`/`#9E655C`) — 저작하는 날 재발 예정,
            `docs/EQUIPMENT_PALETTE.md` 각주에 등재만 해둠. 한글 표시명(현재 3팩 전부
            영문, 번역 부채 래칫 여유 소진 — 실제 출시 전 로컬라이즈 백필 필요) /
            `HemSway` 미적용(사양서 권고 있으나 검사 게이트 자체가 없음) — 둘 다
            design-motion/localization 판단 대상.

- [x] **밧줄등반 중 창 소실 시 낙하 안 함 — 수정 완료** (2026-09-08) — "줄타고 올라가다가
      창을 치워도 계속 줄타고 올라감 -> 떨어져야함". debugger가 가설 5건 중 5번(판정
      기준이 느슨함)으로 원인 확정: 취소 게이트가 "핸들이 발판 목록에 있는가"만 물었는데,
      창 핸들(macOS `kCGWindowNumber`/Windows `HWND`)은 창을 **옮겨도 안 바뀐다** —
      그래서 창을 **닫으면** 감지됐지만(핸들 소멸) **옆으로 치우면** 핸들이 그대로 남아
      영원히 감지되지 않았다. 실기로 재현 확인(수정 전 빌드: 창을 치우면 17유닛 순간이동
      하며 "완주" 처리됨). `GroundSensor.TryGetFootholdTopWorldYCoveringX()` 신설(핸들
      일치 + 밧줄 앵커 x좌표를 실제로 덮는가)로 수정, 클래스 문서의 "ParkourClimbState와
      100% 동일 규칙"이라는 틀린 서술도 정정(손 등반은 움직이는 창을 의도적으로 따라가는
      설계라 규칙이 다름 — 원래 설계 문서는 맞게 적혀 있었음). 실기 왕복 검증(닫기/치우기/
      무변경 3갈래) + 신규 회귀 테스트 2건 + 등반 관련 PlayMode 51/51·EditMode 199건
      전부 통과. Windows 영향: 함께 수정함(핸들 불변 원인이 HWND에도 동일 적용, 크로스
      컴파일 0에러). 커밋 `99df3f3`.
  - [ ] ★ debugger 신규 발견(사용자 미신고, 리더 판단 대기) — `ParkourClimbState`도 같은
        핸들-only 판정이라 창을 멀리 치우면 손 등반 캐릭터가 순간이동한다(따라가기 자체는
        의도된 설계라 이번 수정을 그대로 복제할 수 없음 — 별도 설계 필요, 급하지 않음
        [사용자가 아직 신고 안 함]).

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
    - [x] **1단계 완료** (커밋 `512de5f`) — coder-systems: `PopoverPanel`에 화면 클램프 기구
          신설(`ResolvePanelSizePoints`/`ResolvePanelCenterPoints`, `CharacterInfoWindow.Layout`과
          같은 계약, `UiWindowDrag.ClampCenterPoints` 재사용). 크기를 한 번 풀어
          `sizeDelta`·`PanelScreenRect`·`SyncClickBlocker`에 전부 쓰는 구조라 persona-stress
          R-4(차단막≠표시)가 구조적으로 사라짐. `[✕]`가 항상 남는 최소 크기를 파생값으로 강제.
          `TodoItem.PlannedDayIndex`(0=미상) 1필드 + 세이브 v13, 하위호환 단언 2줄 동반.
          `FramePacing.HoldActiveForInteraction` 배선(R-9). 탐색은 저장 안 함(R-10) — 모델이
          "선택된 날짜"를 아예 안 들고 있어 그 불변식을 리플렉션 테스트가 잠금.
          coder-ui: 말줄임(`UiChrome.Ellipsize` 재사용)·취소선 통일(`TodoPostItWidget`과 같은
          1pt Image 선) 완료, 실기 캡처로 검증. 뮤테이션 주입 RED→GREEN 절차 양쪽 다 준수.
          **EditMode 전량 재검증(2866건, 신규 41건 포함) 실패 0, exit 0.**
      - [ ] **리더 판단 대기 3건**: (a) 완료 항목 라벨 알파 0.5 흐림이 `TodoPostItWidget`의
            "알파로 흐리지 않는다" 원칙과 불일치(R6에서 완료 행이 영구 표시되면 구멍도 영구화됨)
            — ux-designer/design-art 판단 필요. (b) `TodoBoardPopover`의 탭 칩이 아직
            `CloseChipLeft` 역산 앵커라, 500×512가 클램프에 걸리는 화면에서 칩이 창 밖에 남을 수
            있음 — coder-systems가 만든 `PlaceTopRight` 앵커 헬퍼로 옮겨야 함(2단계에서 처리).
            (c) `run-stickmate` 스킬 문서의 `[J]` 설명 오류는 이미 정정함(커밋 `af8b2b0`).
    - [ ] **2단계 착수 대기**: 500×512 레이아웃 재구축(일별/달력 2페이지, coder-systems가 이미
          만든 조회 API 5종 — `TallyForDay`/`AppendItemsForDay`/`AppendUnknownPlannedDayItems`/
          `AppendOverdueItems`/`TryGetEarliestPlannedDay` — 그대로 소비) + 달력 클릭 라우팅(R-5)
          + 포스트잇 오늘자 필터(R-7) + 위 (b) 탭 칩 앵커 이전 + 포스트잇↔팝오버
          `TryClaimAction` 교차 클릭 충돌 버그(P0, 아래 별도 항목과 통합) + UW-6-5 소프트캡
          정의 이전(`design-systems` 값, 이쪽 API는 이미 준비됨).

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
      - [x] design-equipment 완료(위 §2-3 참조). coder-systems(에셋 저작) **완료** — 아래.
  - [ ] **2-3 시각적 성장 콘텐츠 — 단계별 실제 비주얼.** 메커니즘(`CostumeStageOverride`,
        `ResolveStageShapes`)은 있으나, office 세트조차 `stage: 0`(기본형) 하나만 있고 10h/50h/100h
        진화 오버라이드가 **비어 있음**.
    - [x] design-equipment 완료(`docs/EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS_R29.md`) — 광부·사이버·
          대마법사 S0 좌표 + 4세트 진화 좌표는 이미 R28에 있었음(재검증, 위반 0건). ★ 신규 발견:
          **출하된 `CostumeManifest_office.asset`이 승인된 설계본과 다른 물건**(§5-2 초안을
          손으로 옮긴 것) — 위반 5건(보조색·채움 0개, s_min 0.645≠설계 0.550) + 진화 3단계가
          전부 stage 0과 동일(빈 오버라이드). `filled` 비트가 렌더러 2곳에서 뜻이 다름(장비=실제
          메시, 코스튬프롭=굵은 테두리)도 발견 — 채움 낀 좌표쌍 2건(광부 Vein1×Crack1, 대마법사
          StaffMoon×Ring1)이 새로 위반되는 것도 찾아 수정 좌표까지 냄.
    - [x] 리더 판정 4건(R29-1~4):
      - R29-1(보조색 추가) — **채택**. R29-3에 포함.
      - R29-2(이번 라운드에 진화 3단계 채움) — **채택**.
      - R29-3(경로 A: 설계본 전체 교체·골든 5개 변경 vs 경로 B: 골든 안 건드리고 접붙이기) —
        **경로 B 채택**(위반 0건으로 이미 검증됨, 기존 골든·출하 콘텐츠 안 건드려 리스크 최소).
      - R29-4(`filled`=메시로 통일하는 구조 수정 vs 좌표로 우회) — **당장은 좌표 우회**(design-
        equipment가 이미 수정 좌표를 냄, 채택). 렌더러 2곳의 `filled` 의미 불일치 자체는
        `game-architect` 별도 배정 대상으로 체크시트에만 남겨둠(이번 라운드 범위 밖).
    - [x] coder-systems 완료(R30, 2026-09-08) — office 경로 B 접붙이기(DeskTop/DeskLeg/DeskLip
          3건 좌표 수정 + stage1/2/3 신설, 4/10/14/18선) 골든 5개 무손상 확인. 광부/사이버/
          대마법사 3종 신규 매니페스트+키포즈 저작, **전부 `sourceKind: Pack`으로 저작 확인**
          (office만 BaseTheme). `CostumeManifestCorridorTests` 6건 전부 통과,
          `CostumePropForbiddenZoneTests` 감사 대상 2집합→20집합(944쌍, 위반 0)으로 확대.
          RED(에셋 없이 테스트만) → GREEN(에셋 저작 후) 절차 지킴 — 저작 전 실제로
          "진화 4단계가 전부 같은 그림"이 빨갛게 재현됨을 먼저 확인.
      - [x] ★ **ItemCatalog는 의도적으로 안 건드림(맞는 판단)** — `pack.mine` 등 팩 ID를
            프로덕션 문자열로 넣으면 기존 회귀 테스트(`PackManifestCorridorTests`, "팩 ID
            리터럴 0건" 감사)가 깨짐. `CostumeCatalog.AuditSource`는 매니페스트 감사에
            ItemCatalog가 필요 없어(sourceKind==Pack이면 그 자리에서 통과) 이번 목표는
            달성했음. 다만 **`CostumeResolver.Resolve()`가 실제로 이 3종을 돌려주진
            못한다**(코호트 아이템 6종×2팩 + `StickPackManifestSO` 팩 매니페스트 자체가
            없음 — 이건 "구매 가능하게 만들기"이고 이번 라운드는 "만들어서 감사 통과시키기"
            였음). **다음 라운드 필요**: 실제 팩 구매/엔타이틀먼트 배선(product-strategy
            출시순서 — 사이버펑크 먼저 → 광부/대마법사 순으로 우선순위 삼을 것).
      - [ ] ★ 리더 판정 대기 2건(coder-systems 신규 발견) — (가) R29 §6-4 광부 채움-간격
            수정이 불완전: `RockFace×Vein1`(2.16 W_P) · `Vein1×Vein2`(2.44 W_P)가 아직
            채움 하한(2.60) 밑 — 프로덕션 느슨한 규칙(2.00)은 통과해 지금 안 깨짐, 급하지
            않음, design-equipment 재배정. (나) 광부 K2의 `propFrame: 1`(타격 섬광 3획)을
            `CostumePropRenderer`가 **아무도 안 읽어서 실제로 안 뜬다** — 프레임별 조각을
            담을 스키마 자리가 없음(고치려면 코스튬 스키마 v2→v3, 되돌릴 수 없는 결정) →
            일단 `Assert.Ignore`로 회귀 명부에 등재만 해둠(승격 시 자동 감지되게 배선함),
            **game-architect 판단 필요**(v3 승격 여부).
  - [x] product-strategy 완료(10회차, `docs/strategy/CHANNEL_PRICING_DECISIONS.md`) — **가격은
        한 칸도 안 바뀜**: 독서실 $0(BaseTheme 4종, 이미 무료로 존재) / 사이버펑크·대마법사 각
        $4.99. ★ **광부는 스펙 문구("기본 or 인게임 골드")와 달리 사용자가 이미 "유료 DLC로,
        골드구매 SKU 신설 없음"으로 확정해 둔 과거 결정이 있었음**(Tasklist.md:25632) — 골드
        경로 제안은 하지 않음(맞는 판단, 구조적으로도 42종 등급 붕괴를 막음). 신규: 코스튬 라인
        3팩 번들 $12.13 제안. **진화 가속권(시간 돈으로 사기)은 기각** — 근거 5개 중 2개가
        이미 프로덕션 코드(`CostumeEvolutionRules` C-7 / `CostumeEntitlement` C-3)에 못박혀 있어
        판매 자체가 구조적으로 막혀 있음. 출시 순서: 4종 동시 출시 기각, 무료 독서실(1.0) →
        사이버펑크 → 광부/대마법사 순.
    - [x] ★★ **P0급 발견 → security가 막음(Track 2, 2026-09-08)** — `CostumeCatalog`에
          `AllowedBaseThemes()`/`IsAllowedBaseTheme()` 화이트리스트 신설(블랙리스트 `mil`
          단독 방식 폐기). 지금 허용은 `office` **하나뿐**(유일한 실제 BaseTheme 코스튬) —
          `cyber`/`mine`/`arcane`/`ink`/`sport`/`neon`은 전부 거부(신규 팩 3종은 이미
          `sourceKind: Pack`으로 저작돼 있어 이 감사와 무관, Pack 소스는 이 화이트리스트를
          안 거침). 뮤테이션 대조로 구버전 로직이 실제로 `ink` 코스튬을 새게 놔뒀음을 확인한
          뒤 고침(RED→GREEN). `CostumeManifestCorridorTests.cs`에 신규 회귀 1건(대조 5개
          내장) 추가. 브리핑 오류 정정: 구멍을 양성 대조로 단언하던 테스트는
          `CostumeManifestCorridorTests.cs`가 아니라 `CostumePackCostumeAssetTests.cs`(오늘
          신설된 파일)였음 — 그 파일 소유자(coder-systems R30)가 아니라 나(리더)가 직접
          안내받은 패치를 적용해 방향을 뒤집음(`구멍_실증_...` → `구멍_닫힘_...`, 클래스 문서
          갱신). `CostumeCatalog.cs:109`의 낡은 "코스튬 0개" 주석도 함께 정정. Track 4(대사
          5번째 건)와 파일이 겹쳐 EditMode 재검증은 두 트랙 모두 끝난 뒤 한 번에 진행.

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

- [ ] **"병렬로 전부 진행"** (2026-09-08) — 남아있던 모든 대기 항목을 한 번에 병렬 착수하라는
      지시. 리더가 즉시 판단 가능한 저위험 항목은 그 자리에서 결정하고, 나머지는 파일 겹침
      없이 5갈래로 쪼개 병렬 착수함:
  - [x] 즉결 판정(에이전트 불필요) — 경계선 대사 3건(밤이 깊었네/저쪽으로 가볼까/심심해서 왔어)은
        **현행 유지**(급하지 않음, 각각 근거가 약한 관찰일 뿐 확정 결함 아님). 죽은 대사 3줄
        (딴 데 보고 있네?/한 발 더!/오늘은 여기까지)도 **당장 되살리지 않음**(FocusNudge
        에스컬레이션은 의도적 삭제, Attack 대사는 프로덕션 진입 경로 자체가 없어 부활은 별도
        기능 복원 논의가 먼저 필요) — code-inspection 대상으로만 남겨둠, 급하지 않음.
        J-3(문서 정정)·J-4(각도 채택)·J-5(저작 순서)는 R30 구현이 이미 그대로 따랐으므로
        **완료로 판정**.
  - [x] **Track 1(coder-ui) 완료** — 6건 전부 반영. 500×512 레이아웃(일별/달력 2페이지),
        달력 셀은 지연 생성 없이 전부 생성해 `PopoverPanel`의 Awake 1회 스크레이프 함정을
        구조적으로 피함, 포스트잇 오늘자 필터, 완료 항목 알파→색 토큰, 탭칩 `PlaceTopRight`
        이전, 신규 `UiClickArbiter`(z-순서+프레임 이중 게이트)로 클릭충돌 P0 해결 —
        단순 프레임 게이트만으로는 승자가 Update 순서에 좌우돼 증상만 바뀜을 실측으로
        확인 후 이중 게이트로 설계. `TodoBoardPopover.CountTodaySurfaceUncompleted`를
        design-systems 인계용으로 준비(UW-6-5). 실기 캡처는 안전상 중단(부채꼴 진입점이
        우클릭뿐이라 반복 우클릭이 바탕화면 오조작 위험 — 데스크톱 변경 0 확인 후 판단
        보류, dev 게이트 전역 단축키 신설 제안). EditMode 2,901건 실패 0, 뮤테이션 7건
        전부 RED→GREEN. 커밋 `450482c`.
      - [ ] 후속 배정 — design-systems(소프트캡 정의, API 준비됨) / design-art(달력 색
            토큰 임시 배선, §3-3 채도 3단 미구현) / design-narrative+design-art(밀린 것·
            미상 그룹 시각 구분 없음) / ux-widgets+리더(완료함 탭 날짜 라벨 40pt 좌표
            미정) / 리더(재화 문구 배선, UW-2 별건) / dev-platform(폭 500 iPhone 미대응,
            기존 갭).
  - [x] **Track 2(security) 완료** — `CostumeCatalog.AllowedBaseThemes()` 화이트리스트 신설,
        허용은 `office` 하나뿐. 뮤테이션 대조로 구버전이 `ink`를 새게 놔뒀음을 실증 후 수정
        (RED→GREEN). 신규 회귀 1건(대조 5개 내장). 브리핑 오류 정정: 구멍을 양성 단언하던
        테스트는 `CostumePackCostumeAssetTests.cs`였음(내가 직접 패치 적용, 방향 반전).
        커밋 `060e6a5`.
  - [x] ★ Track 3 재판정 — 착수 직전 조사에서 **"기존 DLC 팩(ink/mil)과 같은 패턴"이라는
        전제가 틀렸음을 확인**: `ink`/`mil`/`cyber`/`neon`/`sport`/`office`는 전부 **기본
        아이템 42종의 테마 색상 분류**일 뿐 유료 DLC가 아니고, `StickPackManifestSO`(v2
        스키마, 채널·엔타이틀먼트·팔레트 필드까지 이미 설계됨)는 **실제 에셋 인스턴스가
        프로젝트에 0개** — 이 저장소에 "실제로 팔린 팩"의 선례 자체가 없다. 즉 이건 "기존
        패턴을 따라 배선"이 아니라 **이 게임 최초의 실제 팩을 만드는 되돌릴 수 없는 결정**이라
        `game-architect` 판단이 먼저 필요함 → **Track 5로 흡수**.
  - [x] **Track 4(coder) 완료** — `LedgeHangDialogueParams.HasDescendTarget` 플래그 추가.
        발판 없으면 침묵(새 문안 안 지어냄, `GrabReactionLines.HasGrabPoint`와 같은 패턴).
        낙차 비교 로직 자체는 무변경. 네거티브 컨트롤로 게이트 제거 시 결함 원문이 그대로
        재현됨을 실측. 커밋 `060e6a5`.
  - [x] **Track 5(game-architect) 완료** — `docs/GAME_ARCHITECTURE_REVIEW.md` §18, 커밋
        `aa9c0d7`. ★ 먼저 — mtime 대조로 **R29-1/R29-2는 이미 해소됨을 확인**(coder-systems
        R30 저작이 R29 문서보다 41분 늦어 그 지적을 이미 반영했음) — 리더가 미결로 안 들고
        가도 됨.
    - **판정 1(propFrame v2→v3)**: **미룬다**(출시 이후 폴리싱 라운드). 타격감은 이미
      포즈 낙차+기울임 반전으로 전달되고, 섬광 3획은 이 배율에서 잉크 한 덩어리로 뭉쳐
      안 읽힘(계산 근거 포함). 되살릴 조건: 프레임 축 쓰는 코스튬 2종 이상 + 그중 하나
      프레임 수 2 초과. → 문서 정정 완료(커밋 `e00e60b`).
    - **판정 2(filled 렌더러 불일치)**: **통합 안 함**(되돌릴 수 없는 결정이 아니고, 원칙
      2가 반대로 밈 — 프롭은 바탕화면 바로 위 층이라 메시 채움이 불투명 면적을 키움).
      ★★ **다만 실제 출하 데이터에 시각 결함을 새로 발견** — `costume.cyber`의
      `BasePad`·`costume.mine`의 `Vein1`이 채움 안쪽 여백 미달로 "가운데 뚫린 테두리"로
      보임(ρ_in 0.0600H/0.0494H vs 하한 0.0373H). → **Track 6(design-equipment)로
      좌표 수정 착수**(아래). 공유 문서(`AccessoryDefSO.cs`)에 "소비자마다 뜻이 다르다"
      경고 추가 완료(커밋 `e00e60b`).
    - **판정 3(DLC 팩 아키텍처, 최초 판정)**: 스토어 채널 **지금 확정 안 함** —
      `entitlements: []`(빈 배열)가 이미 테스트로 잠긴 "무료는 안 묻는다" 경로를 그대로
      타서 안전한 첫걸음. 사이버펑크 팩부터, **4개 스탯슬롯 아이템만**(FX/PET 2종은
      렌더러가 하드코딩 스위치라 데이터만으론 안 열림 — 별도 코드 작업, 이번엔 보류).
      다음 라운드가 바로 실행 가능한 구체 절차(아이템 코호트/인덱스, 회귀 게이트 3개
      좁히기)까지 명시. → **Track 7(coder-systems)로 즉시 착수**(아래).
      부수 발견 3건(리더 인지 필요, 지금 안 고쳐도 됨): ① `IsOwned`가 엔타이틀먼트를
      아직 안 봐서 팩 아이템이 폴더에 놓이는 순간 Lv.1 전원에게 열림(출시 전 무해, 다음
      라운드가 `Assert.Ignore`로 등재) ② FX/PET 슬롯 하드코딩 확인됨(위 언급) ③
      `sourceKind: Pack`은 옳은 선택이었음(되돌리지 말 것, 이미 확정).
  - [x] **Track 6(design-equipment) 완료** — ★ 리더 지시서의 부등호가 뒤집혀 있던 것을
        agent가 스스로 교정(넓히는 게 아니라 **좁혀야** 채움 안쪽이 잉크로 덮인다).
        `cyber/BasePad` 8각(0.12H 두께)→끝을 깎은 6각(0.068H), `mine/Vein1` 쐐기
        뿌리폭 0.12→0.078H — 둘 다 ρ_in이 상한(0.037290H) 안으로 들어옴. 부수 발견:
        `mine Vein1×Vein2`도 금지대 미달이었음(이전 라운드들이 프롭당 최악 1쌍만 재서
        놓침) — 함께 해소. `cyber Mast×BasePad`는 실제로 접합(d=0, 의도된 관통)임을
        정정(이전 측정 도구의 오판). 생성기(`r30_costume_assets.py`)에 오버라이드를
        넣어 재생성해도 안 되돌아가게 함. EditMode 2,900건 실패 0. 커밋 `f157bb8`.
      - [ ] 후속 배정(미확인 항목, 급하지 않음) — `r29_shipped_office.py` 검증 스크립트
            자체가 깨져 있음(이번 수정과 무관한 선행 결함, 별도 수리 필요) /
            `CostumePackCostumeAssetTests`에 ρ_in 상한 회귀 테스트 추가(같은 함정 세
            번째 재발 방지, `StrokeWidthRatio` 상수 참조 방식으로).
  - [x] **Track 7(coder-systems) 완료 — 단 착지는 안 함(리더 결재 대기 후 착지 라운드
        필요)**. ★ 핵심 성공 실측: `CostumeResolver.Resolve("costume.cyber")`가 **실제로
        작동함을 프로브로 증명**(`PackRegistry` declared 4/resolved 4, `entitlements: []`
        → `IsOpen=True`). 다만 game-architect의 "회귀 관문 3개" 예측이 빗나가 **실측
        33건/18개 클래스**가 빨개짐을 발견 — 에셋을 트리에 남기지 않고
        `docs/handoff/packcyber-firstpack/`에 파킹(EditMode 최종 2900건 실패 0 유지),
        `ItemCatalogAssetParityTests`만 코호트 한정으로 좁혀 착지(뮤테이션 검증).
      - [x] 리더 판정 3건(즉결) — (1) `PackPaletteGateTests` 비교 모집단을 **기본 코호트
            색으로 한정**(채택 — 구조적 자기모순이라 이 방법밖에 없음: 첫 팩이 카탈로그에
            실리는 순간 자기 색과 ΔE 0이 되어 전 색 비교로는 어떤 팩도 통과 불가) (2)
            `PACK_THEME_SPEC` 동결 보조색 `#518C84`→**`#518D85`**(채택 — 이미 러너 실측
            통과 확인됨, design-art가 스펙 문서에 역반영할 것) (3) 번역 부채 래칫
            42→**46 상향**(채택, 임시 — 팩 아이템 영문 이름으로 우선 착지, **한글 이름
            백필은 빚으로 남음**, `localization` 배정 대상).
      - [x] **착지 라운드 완료** — RED 독립 재현(예측 33건 vs 실측 30건, 차이 3건은 이미
            선반영됐음을 규명) → 판정 3건 반영(팔레트 게이트 모집단 좁히되 "팩 색은 동결
            대장 안" 존재 단언 신설로 검사 무력화 방지 / 보조색 3곳 동기화 / 번역 래칫을
            기본42+팩4 두 항으로 갈라 상향, 이 래칫이 RED에서 실제로는 안 빨갰다는 사실
            정정도 문서화). 30건의 뿌리 원인 하나("카탈로그=42종"이 13개 파일에서 각자
            계산됨)를 신규 `BaseCohortScope.cs`로 중앙화. 26건 기계적 수정 + 나머지는
            "팩은 반대 방향으로 잼". 진짜 조형 결함 1건(Patched Hood↔베레모 10%만 다름,
            하한 15%)은 design-equipment 배정 대상으로 자동 만료 Ignore만 등재, 좌표는
            안 건드림 — Eyes 아이콘 넘침은 조사 결과 규칙 위반 아님으로 판명(수정 불필요).
            최종 EditMode 2,909건 실패 0. 커밋 `edde04e`(파킹 디렉터리
            `docs/handoff/packcyber-firstpack/`는 착지 완료로 정리함).
      - [ ] 남은 배정(급하지 않음) — design-equipment: Patched Hood 실루엣 재설계(하한
            15% 대비 10%).
      - [x] 회귀로만 등재(고치지 않음) — `IsOwned`가 엔타이틀먼트를 아직 안 봐서 팩
            아이템이 폴더에 놓이는 순간 Lv.1 전원에게 열림을 **실측으로 확인**
            (`owned=True`) — 등재 대상 파일은 `ItemOwnershipUnionTests`(다음 라운드).
      - [x] 자진 신고 — 실행 도중 다른 라운드가 활성 빌드 타깃을 Win→OSX로 전환한 것을
            포착(전역 상태 변경, CLAUDE.md 규칙상 리더 승인 필요한 축인데 이번엔 그냥
            일어남 — 문제는 없었으나 인지 필요). `regress.sh`의 `MIN_EDIT_CASES=2592`가
            낡음(현재 최대 2900) — 별도로 올릴 것.

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
  - [x] 재게시(2차) 완료 — 오늘 할일 위젯 1단계(팝오버 클램프+v13 스키마+말줄임/취소선 통일) +
        코스튬 팩 3종 콘텐츠 저작까지 반영. `windows-preview-20260908j` 태그(코드기준 `cac40b3`)
        → zip(32,365,763 bytes) 첨부해 게시:
        https://github.com/moonkj/StickMate/releases/tag/windows-preview-20260908j
  - [x] 재게시(3차) 완료 — "병렬로 전부 진행" 웨이브 전체 반영: 밧줄등반 창소실 수정,
        오늘 할일 위젯 2단계(500×512+달력+클릭충돌 P0), 코스튬 채움 결함 수정, 사이버펑크
        팩 최초 착지. `windows-preview-20260908k` 태그(코드기준 `42f3b64`) → zip
        (32,376,698 bytes) 첨부해 게시:
        https://github.com/moonkj/StickMate/releases/tag/windows-preview-20260908k
  - [x] 재게시(4차) 완료 — 광부·대마법사 팩 배선까지 반영(3종 DLC 팩 전부 엔진상 구매
        가능). `windows-preview-20260908l` 태그(코드기준 `621a177`) → zip
        (32,381,605 bytes) 첨부해 게시:
        https://github.com/moonkj/StickMate/releases/tag/windows-preview-20260908l
  - [x] 재게시(6차) 완료 — DLC 상점 노출 + 장비 12종 디자인 고도화까지 반영.
        `windows-preview-20260908m` 태그(코드기준 `204d4a8`) → zip(32,388,728 bytes)
        첨부해 게시: https://github.com/moonkj/StickMate/releases/tag/windows-preview-20260908m
