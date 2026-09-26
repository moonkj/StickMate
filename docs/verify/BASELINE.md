# 회귀 베이스라인 대장 — 실행당 한 줄

자동 생성: `python3 docs/verify/baseline.py` · 최종 2026-09-26 22:41:40
**손으로 고치지 마라.** 다음 실행이 통째로 덮는다.

## 읽는 법 — 표시가 붙은 값은 잰 값이 아니다

| 표시 | 뜻 |
|---|---|
| (없음) | **실행 시각에 잰 값**(`regress.sh`가 남긴 `.meta`). 이것만이 사실이다 |
| `~` | 사후 추론 — 타깃은 로그의 Bee dag 해시, HEAD는 reflog 시각 대조 |
| `↑` | **직전 실행에서 물려받음** — 그 실행은 재컴파일을 안 해 자기 타깃을 남기지 않았다 |
| `?` | **미상.** 빈 칸으로 두지 않는다 — 빈 칸은 읽는 사람이 마음대로 채운다 |

**더러움** = 그 실행 시각의 미커밋 파일 수(`.meta`의 `dirty`). 0이 아니면 그 줄은 **HEAD가 아니라 «그때 움직이던 트리»의 결과다.** 병렬 라운드가 도는 밤에는 실패가 「회귀」가 아니라 「편집 중 스냅샷」일 수 있다 — 귀속하기 전에 그 파일의 mtime을 실행 시각과 대조해라.

dag→타깃 매핑 4건: `1900b0aE.dag`=WIN, `1900b0aP.dag`=WIN, `200b0aE.dag`=OSX, `200b0aP.dag`=OSX

## 개명 대장 — 회귀가 아니라 개명인 것

정본 데이터: `docs/verify/renames.tsv` · 검증기: `docs/verify/renames.py --check`
각 줄은 **소스 트리(.cs)** 로 매번 재검증된다 — 새 이름이 실재하고(R1), 옛 이름이 사라졌고(R2), 짧은 이름이 유일할 때(R3)만 적용된다.

| 옛 이름 | 새 이름 | 등록일 | 근거 |
|---|---|---|---|
| `실제_베레모_폴백_테는_이제_몸과_같은_점수다` | `실제_베레모_폴백_테는_몸_띠의_아랫변_그대로다` | 2026-09-03 | 커밋 1eb0e2b 밤샘 라운드. 같은 클래스(AccessoryFallbackBodyParityTests) 안에서 메서드 이름만 바뀜. 옛 이름은 소스에서 사라졌고 새 이름이 :559에 있다. 베이스라인 xml 21건이 옛 이름, 6건이 새 이름. |
| `T2_실제_프리팹의_11개_선이_같은_규칙을_따른다` | `T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다` | 2026-09-03 | 마디 병합(리더 판정 CH-8)으로 본체 LineRenderer가 11 -> 7이 되어 이름의 «11개»가 거짓이 됐다. 같은 클래스 안 메서드 이름만 바뀜. 검사 내용은 넓어졌다(없어진 선 4개에 대한 부재 단언 + 개수 단언 추가). ★ 2026-09-05 qa-r9 정정: 원래 이 줄의 새 이름은 «T2_실제_프리팹의_본체_선이_같은_규칙을_따른다»였는데 그 이름은 **어느 소스에도 없다** — 그 뒤 한 번 더 개명됐고(:487) 대장이 중간 이름에서 멈춰 있었다. R1이 그 줄을 거부해 왔다(죽은 규칙). 종착 이름으로 갱신했고, 중간 이름은 아래 줄이 따로 흡수한다. |
| `T2_실제_프리팹의_본체_선이_같은_규칙을_따른다` | `T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다` | 2026-09-05 | 2번째 개명(중간 -> 현재). `final_play.xml`(09-03 11:27)에는 중간 이름이, `dbg-getupfix-FULL_play.xml`(12:22) 이후에는 현재 이름이 들어 있다 — 이 줄이 없으면 그 두 xml 대조에서 «삭제 1 + 신설 1»로 뜬다. R1: 선언 `LineRendererUvBandProbeTests.cs:487`. R2: 중간 이름은 소스에 0건(grep 실측). |
| `스토어_SDK가_들어오면_이_경보가_먼저_울린다` | `스토어_SDK는_승인된_어댑터_한_파일에서_승인된_심볼만_쓴다` | 2026-09-05 | qa-r10 대조에서 «짝없는 소멸»로 떴으나 같은 클래스 1:1 개명이다(축 B). R1: 선언 EntitlementFailOpenAuditTests.cs:403. R2: 옛 이름은 소스 전체에 0건(grep 실측). 근거는 같은 파일 :35/:59/:358이 새 이름을 축 B의 이름으로 인용한다. |
| `망토_윤곽선은_밑단_5점을_흔든다` | `망토_뒤판의_흔들_구간은_밑단이다` | 2026-09-05 | 계약 v2(인계본 이식). 옛 단언은 «CapeOutline 2번부터 5점»이라는 v1 도형의 그날 형태였고, R17 무대 뒤판(49점·밑단 16..32)에서 뜻을 잃었다. 같은 클래스 안 1:1 개명 + 파라미터화(int item). 검사는 좌표 대신 «그 구간이 실제로 밑단인가»라는 성질로 넓어졌고 점 번호 잠금은 CardShapeContractTests 골든이 맡는다. R1: 선언 CapeAirFlutterTests.cs:249. R2: 옛 이름은 소스 전체 0건(grep 실측 — 이 개명은 묘비 주석조차 없어 «짝없는 소멸»로 보였다). |
| `열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다` | `아이템은_한_벌이거나_두_벌이고_두_벌은_표면과_층이_명시된다` | 2026-09-05 | card-r17(15:49)에 «열세 종» 이름으로 빨갛다가 card-r19b(16:09)에서 새 이름으로 초록. 같은 클래스 1:1 개명이고 검사는 좁아진 것이 아니라 넓어졌다 — 표면 명시(AccessorySurfaces.IsExplicit) · 층/정렬번호 대조 · bodyFixed · 유리 워시 α<1 이 새로 들어왔다. 비공허성은 ByItem()의 Assert.AreEqual(16, byItem.Count)가 잠근다. R1: 선언 CardShapeContractTests.cs:365. R2: 옛 이름 소스 전체 0건(grep 실측). |
| `NegativeControl_RailChipAliveAndDeadInksActuallyDiffer` | `NegativeControl_OldRailChipFaceCouldNotStandOnThePanel` | 2026-09-06 | 커밋 349048f. qa-r10(09-05 09:56) 대비 «짝없는 소멸»로 떴으나 같은 클래스 1:1 개명이고 네거티브 컨트롤이라는 역할도 그대로다(옛 레일 칩 배선이 이 판정에서 실제로 빨개지는가). 검사 축만 «살아있음/죽음 잉크가 다른가»에서 «옛 면이 창 바탕 위에서 대비 하한을 못 넘는가»로 좁아졌다. R1: 선언 SettingsDisabledSurfaceTests.cs:284. R2: 옛 이름은 소스 트리 전체에 0건(grep 실측). ★ 이 줄이 없으면 매 라운드 «짝없는 소멸 1건»이 떠서 진짜 삭제를 가린다. |

## 실행 대장

**R1** = 결과 판정(`docs/verify/nunit_verdict.py`): 초록 = failed==0 ∧ test-run result ∈ {Passed, Skipped:Ignored} ∧ site가 SetUp/TearDown인 스위트 0 ∧ Failed 스위트 0 ∧ failed 속성 = Failed 케이스 수. **「실패」 칸이 0이어도 R1이 빨강일 수 있다.** `★픽스처` 로 시작하는 실패 이름은 테스트가 아니라 픽스처의 [OneTimeSetUp]/[OneTimeTearDown]이다.

**범위** = 전량/부분은 **기록된 사실로만** 가른다 — Unity 로그 `COMMAND LINE ARGUMENTS:`(한 줄에 인자 하나)에 필터 인자(`-testFilter` · `-testCategory` · `-assemblyNames` 등)가 있으면 `부분(N건)`, 없으면 `전량` (로그의 `-testResults`가 **그 xml**일 때만 인정), 로그가 없으면 `regress.sh` 사이드카, 둘 다 없으면 `미확인`. testcasecount 크기로 추정하지 않는다. **「현재」는 가장 최근 `전량`만이다.**

**수집** = `docs/verify/runs/*_edit.xml`·`*_play.xml` 전부 + 그 밖(`runs/`의 다른 xml · `Logs/**`)은 **전량이 기록으로 확인된 것만**(xml 바이트 sha256 중복 제거, `runs/` 쪽 우선). 표에서 뺀 것: 미확인(로그 명령줄·사이드카 없음) 16 · 부분 549 · 중복(해시) 2

| 시각 | 라벨 | 모드 | 범위 | HEAD | 더러움 | 활성 타깃 | total | 통과 | 실패 | 건너뜀 | 판정 불가 | R1 | 실패 목록 |
|---|---|---|---|---|---:|---|---:|---:|---:|---:|---:|---|---|
| 08-28 02:25 | `Logs/playmode_diag` | play | 전량 | ~ec03cf0 | ? | **~OSX** | 1 | 0 | 1 | 0 | 0 | **빨강** | StickmanFallsSettlesAndWanders |
| 08-28 02:29 | `Logs/editmode_final` | edit | 전량 | ~ec03cf0 | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 02:40 | `Logs/debugger_editmode` | edit | 전량 | ~46beba4 | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 02:40 | `Logs/debugger_playmode` | play | 전량 | ~46beba4 | ? | **↑OSX** | 1 | 1 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:48 | `Logs/mac_final_editmode` | edit | 전량 | ~2862ad6 | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:54 | `Logs/coder_final_editmode` | edit | 전량 | ~2862ad6 | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:55 | `Logs/coder_pm_run1` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:55 | `Logs/coder_pm_run2` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:55 | `Logs/coder_pm_run3` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:56 | `Logs/coder_pm_run4` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:56 | `Logs/coder_pm_run5` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:57 | `Logs/coder_pm_run6` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:57 | `Logs/coder_pm_run7` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:58 | `Logs/coder_pm_run8` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:58 | `Logs/coder_pm_run9` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:58 | `Logs/coder_pm_run10` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:59 | `Logs/coder_pm_run11` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 03:59 | `Logs/coder_pm_run12` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 04:00 | `Logs/coder_pm_run13` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 04:00 | `Logs/coder_pm_run14` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 04:01 | `Logs/coder_pm_run15` | play | 전량 | ~2862ad6 | ? | **↑OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:17 | `Logs/coder_editmode` | edit | 전량 | ~cd395fd | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:17 | `Logs/coder_pm_probe1` | play | 전량 | ~cd395fd | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:18 | `Logs/coder_pm_probe2` | play | 전량 | ~cd395fd | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:19 | `Logs/coder_pm_probe3` | play | 전량 | ~cd395fd | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:19 | `Logs/coder_pm_probe4` | play | 전량 | ~cd395fd | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:20 | `Logs/coder_pm_probe5` | play | 전량 | ~cd395fd | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:26 | `Logs/dbg2_editmode` | edit | 전량 | ~10e55ea | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:26 | `Logs/dbg2_pm_run1` | play | 전량 | ~10e55ea | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:27 | `Logs/dbg2_pm_run2` | play | 전량 | ~10e55ea | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:27 | `Logs/dbg2_pm_run3` | play | 전량 | ~10e55ea | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:28 | `Logs/dbg2_pm_run4` | play | 전량 | ~10e55ea | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:29 | `Logs/dbg2_pm_run5` | play | 전량 | ~10e55ea | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:38 | `Logs/coder_native_editmode` | edit | 전량 | ~10e55ea | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 05:41 | `Logs/coder_native_playmode` | play | 전량 | ~10e55ea | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 12:44 | `Logs/coder_visual_editmode` | edit | 전량 | ~fd4de0e | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 12:45 | `Logs/coder_visual_playmode` | play | 전량 | ~fd4de0e | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 12:46 | `Logs/coder_visual_playmode_run2` | play | 전량 | ~fd4de0e | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 12:47 | `Logs/coder_visual_playmode_run3` | play | 전량 | ~fd4de0e | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 12:47 | `Logs/coder_visual_playmode_run4` | play | 전량 | ~fd4de0e | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 20:56 | `Logs/coder_dock_editmode` | edit | 전량 | ~c721251 | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-28 20:57 | `Logs/coder_dock_playmode` | play | 전량 | ~c721251 | ? | **↑OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-28 21:00 | `Logs/coder_dock_playmode2` | play | 전량 | ~c721251 | ? | **~OSX** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 08-29 07:49 | `Logs/coder_edge_editmode` | edit | 전량 | ~f68e515 | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-29 07:49 | `Logs/coder_edge_playmode` | play | 전량 | ~f68e515 | ? | **↑OSX** | 26 | 26 | 0 | 0 | 0 | 초록 | — |
| 08-29 07:52 | `Logs/coder_edge_playmode2` | play | 전량 | ~9c7c786 | ? | **~OSX** | 26 | 26 | 0 | 0 | 0 | 초록 | — |
| 08-29 08:10 | `Logs/p4vis_edit` | edit | 전량 | ~3b5094c | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-29 08:10 | `Logs/p4vis_play` | play | 전량 | ~3b5094c | ? | **↑OSX** | 37 | 37 | 0 | 0 | 0 | 초록 | — |
| 08-29 08:30 | `Logs/p4vis_play2` | play | 전량 | ~3b5094c | ? | **~OSX** | 37 | 37 | 0 | 0 | 0 | 초록 | — |
| 08-29 08:39 | `Logs/p4vis_edit3` | edit | 전량 | ~3b5094c | ? | **↑OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-29 08:39 | `Logs/p4vis_play3` | play | 전량 | ~3b5094c | ? | **↑OSX** | 37 | 37 | 0 | 0 | 0 | 초록 | — |
| 08-29 09:14 | `Logs/p5_edit` | edit | 전량 | ~350ade2 | ? | **~OSX** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 08-29 09:14 | `Logs/p5_play` | play | 전량 | ~350ade2 | ? | **↑OSX** | 44 | 44 | 0 | 0 | 0 | 초록 | — |
| 08-29 09:23 | `Logs/final_play` | play | 전량 | ~350ade2 | ? | **↑OSX** | 44 | 44 | 0 | 0 | 0 | 초록 | — |
| 08-29 10:24 | `Logs/verify_edit` | edit | 전량 | ~9122aa3 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 10:47 | `Logs/verify_play` | play | 전량 | ~9122aa3 | ? | **~OSX** | 49 | 49 | 0 | 0 | 0 | 초록 | — |
| 08-29 10:57 | `Logs/final_edit` | edit | 전량 | ~9122aa3 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:15 | `Logs/scale_edit` | edit | 전량 | ~802143f | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:15 | `Logs/scale_play` | play | 전량 | ~802143f | ? | **~OSX** | 55 | 53 | 2 | 0 | 0 | **빨강** | StanceFootStaysPlantedWhileBodyMovesForward<br>StaysInsideScreenWhilePushingEdgeAtCurrentScale |
| 08-29 11:21 | `Logs/hw_play` | play | 전량 | ~802143f | ? | **~OSX** | 57 | 55 | 2 | 0 | 0 | **빨강** | ManualPathStillWorksWhileDisabledAndFlagRestoresAutonomous<br>StaysInsideScreenWhilePushingEdgeAtCurrentScale |
| 08-29 11:24 | `Logs/s2_edit` | edit | 전량 | ~802143f | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:24 | `Logs/s2_play` | play | 전량 | ~802143f | ? | **↑OSX** | 58 | 56 | 2 | 0 | 0 | **빨강** | ManualPathStillWorksWhileDisabledAndFlagRestoresAutonomous<br>StaysInsideScreenWhilePushingEdgeAtCurrentScale |
| 08-29 11:26 | `Logs/hw2_edit` | edit | 전량 | ~802143f | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:27 | `Logs/hw2_play` | play | 전량 | ~802143f | ? | **↑OSX** | 67 | 66 | 1 | 0 | 0 | **빨강** | StaysInsideScreenWhilePushingEdgeAtCurrentScale |
| 08-29 11:29 | `Logs/s3_play` | play | 전량 | ~802143f | ? | **~OSX** | 67 | 66 | 1 | 0 | 0 | **빨강** | StaysInsideScreenWhilePushingEdgeAtCurrentScale |
| 08-29 11:36 | `Logs/f_edit` | edit | 전량 | ~e96140c | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:36 | `Logs/f_play` | play | 전량 | ~e96140c | ? | **↑OSX** | 67 | 67 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:39 | `Logs/retina_edit` | edit | 전량 | ~e96140c | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:39 | `Logs/retina_play` | play | 전량 | ~e96140c | ? | **↑OSX** | 67 | 67 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:56 | `Logs/r2_edit` | edit | 전량 | ~e96140c | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:56 | `Logs/r2_play` | play | 전량 | ~e96140c | ? | **↑OSX** | 73 | 73 | 0 | 0 | 0 | 초록 | — |
| 08-29 11:59 | `Logs/kj_edit` | edit | 전량 | ~e96140c | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:07 | `Logs/r3_edit` | edit | 전량 | ~e96140c | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:08 | `Logs/kj_edit2` | edit | 전량 | ~e96140c | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:08 | `Logs/kj_play2` | play | 전량 | ~e96140c | ? | **↑OSX** | 85 | 85 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:21 | `Logs/fin_edit` | edit | 전량 | ~e96140c | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:21 | `Logs/fin_play` | play | 전량 | ~e96140c | ? | **↑OSX** | 85 | 84 | 1 | 0 | 0 | **빨강** | StressTierChangeDrawsMoodAndFullyCleansUp |
| 08-29 12:25 | `Logs/kj_edit3` | edit | 전량 | ~e96140c | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:25 | `Logs/kj_play3` | play | 전량 | ~e96140c | ? | **↑OSX** | 85 | 85 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:54 | `Logs/r_edit` | edit | 전량 | ~9353757 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 12:55 | `Logs/r_play` | play | 전량 | ~9353757 | ? | **↑OSX** | 85 | 85 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:10 | `Logs/dockfix_editmode` | edit | 전량 | ~39ab690 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:10 | `Logs/dockfix_playmode` | play | 전량 | ~39ab690 | ? | **~OSX** | 95 | 47 | 48 | 0 | 0 | **빨강** | AutoWanderHopsDownAndClimbsBackWithoutScriptedPulses<br>AutonomousReactionNeverFiresWhileDisabled<br>BothHeightQueryPathsAgree<br>CancelledEventRemovesEveryObject<br>CompletedEventRemovesEveryObject<br>CompletedEventShattersAndRemovesEveryObject<br>DisablingRendererRemovesEveryObject<br>DockHopDownBandSurvivesScale<br>DockRoundTripHopDownThenClimbBackUp<br>Dock_구간에서_RAGDOLL이_물리바닥을_뚫지_않는다<br>EmoteNeverOverlapsCharacterHead<br>EveryPhase5ComponentIsPlacedExactlyOnce<br>EveryReactionKindDrawsAndFullyCleansUp<br>FocusSessionDrawsTimerRingAndTierVisualsThenCleansUp<br>ForcedInterrupt_RemovesBubbleInSameFrame<br>HiddenRunawayCharacterStaysClickableAndRevealsOnHitboxDown<br>HopsDownThenClimbsBackOntoDock<br>LargeDropStillHangsAndRejectsHopDownPulse<br>LedgeHangAlwaysReleasesAtMaxDurationTimeout<br>LedgeHangDescendsThenFallsOntoLowerFoothold<br>LedgeHangFallsImmediatelyWhenGrabbedFootholdDisappears<br>ManualPathStillWorksWhileDisabledAndFlagRestoresAutonomous<br>NewDialogue_ReplacesPreviousBubbleImmediately<br>NewReactionReplacesPreviousInsteadOfStacking<br>NormalTransition_KeepsBubbleForMinimumExposureThenFadesOut<br>OtherSpeakerDialogue_IsNotRendered<br>PrefabGeometryFollowsCharacterScale<br>RagdollEntersAndRecoversToActiveState<br>RunawayLifecycleDrawsPhasesAndSnackIsTheOnlyClickTarget<br>SmallDropStepsOffAndLandsOnLowerFoothold<br>StanceFootStaysPlantedWhileBodyMovesForward<br>StandsExactlyOnFootholdTopAtCurrentScale<br>StartedEventActuallyCreatesCrackObjectsAndStaysClickThrough<br>StartedEventActuallyCreatesGhostWindowObjects<br>StaysInsideScreenWhilePushingEdgeAtCurrentScale<br>StickmanFallsSettlesAndWanders<br>StickmanStaysWithinVerticalViewportMargin<br>StressTierChangeDrawsMoodAndFullyCleansUp<br>TodoPostItCheckboxIsActuallyClickableThroughUguiRaycast<br>TodoReminderStateDrawsHeldPaperAndCleansUp<br>WalkableBoundsAreExactlyWhereTheHardClampStopsTheCharacter<br>WalksIntoScreenEdgeAndTurnsAroundWithinAbsoluteDeadline<br>구분선_개수가_폭에_반영된다<br>타일수를_못_셌을_때만_보정상수가_적용된다<br>폭_공식이_실측_6표본을_재현한다 |
| 08-29 13:12 | `Logs/dockfix_playmode3` | play | 전량 | ~39ab690 | ? | **↑OSX** | 95 | 95 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:16 | `Logs/comic_editmode` | edit | 전량 | ~39ab690 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:16 | `Logs/comic_playmode` | play | 전량 | ~39ab690 | ? | **↑OSX** | 107 | 104 | 3 | 0 | 0 | **빨강** | HighFallLandsInCrouchWithoutRagdoll<br>NegativeControl_ShieldOffReproducesRagdollOnLanding<br>TextGapStaysProportionalToCharacterHeightAtEveryScale |
| 08-29 13:23 | `Logs/dockfix_final_em` | edit | 전량 | ~39ab690 | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:23 | `Logs/dockfix_final_pm` | play | 전량 | ~39ab690 | ? | **↑OSX** | 107 | 105 | 2 | 0 | 0 | **빨강** | HighFallLandsInCrouchWithoutRagdoll<br>NegativeControl_ShieldOffReproducesRagdollOnLanding |
| 08-29 13:49 | `Logs/comic_final_EditMode` | edit | 전량 | ~12d7672 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:49 | `Logs/comic_final_PlayMode` | play | 전량 | ~12d7672 | ? | **↑OSX** | 108 | 108 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:56 | `Logs/landing_editmode` | edit | 전량 | ~12d7672 | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 13:56 | `Logs/landing_playmode` | play | 전량 | ~12d7672 | ? | **↑OSX** | 108 | 108 | 0 | 0 | 0 | 초록 | — |
| 08-29 16:00 | `Logs/coder_floor_edit` | edit | 전량 | ~9ad6279 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 16:00 | `Logs/coder_floor_play` | play | 전량 | ~9ad6279 | ? | **~OSX** | 109 | 107 | 2 | 0 | 0 | **빨강** | FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>ShieldDecidesWhetherFullSpeedGroundImpactBecomesRagdoll |
| 08-29 16:19 | `Logs/tt_edit` | edit | 전량 | ~9ad6279 | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 16:20 | `Logs/tt_play` | play | 전량 | ~9ad6279 | ? | **↑OSX** | 122 | 120 | 2 | 0 | 0 | **빨강** | ShieldDecidesWhetherFullSpeedGroundImpactBecomesRagdoll<br>ThrownCharacterTumblesThenLandsInCrouchWithoutRagdoll |
| 08-29 16:24 | `Logs/coder_floor_edit2` | edit | 전량 | ~9ad6279 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 16:25 | `Logs/coder_floor_play4` | play | 전량 | ~9ad6279 | ? | **↑OSX** | 135 | 132 | 3 | 0 | 0 | **빨강** | FullCycleFiresExactlyThreeArrowsAndFullyCleansUp<br>ShieldDecidesWhetherFullSpeedGroundImpactBecomesRagdoll<br>ThrownCharacterTumblesThenLandsInCrouchWithoutRagdoll |
| 08-29 16:29 | `Logs/tt_play2` | play | 전량 | ~9ad6279 | ? | **↑OSX** | 135 | 133 | 2 | 0 | 0 | **빨강** | FullCycleFiresExactlyThreeArrowsAndFullyCleansUp<br>ShieldDecidesWhetherFullSpeedGroundImpactBecomesRagdoll |
| 08-29 17:03 | `Logs/arch_em_final` | edit | 전량 | ~9ad6279 | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 17:03 | `Logs/arch_pm_final` | play | 전량 | ~9ad6279 | ? | **↑OSX** | 136 | 136 | 0 | 0 | 0 | 초록 | — |
| 08-29 17:34 | `Logs/arch_em_f2` | edit | 전량 | ~9ad6279 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 17:35 | `Logs/arch_pm_f2` | play | 전량 | ~9ad6279 | ? | **↑OSX** | 136 | 136 | 0 | 0 | 0 | 초록 | — |
| 08-29 18:04 | `Logs/dbg_pm_full` | play | 전량 | ~9ad6279 | ? | **↑OSX** | 136 | 136 | 0 | 0 | 0 | 초록 | — |
| 08-29 18:08 | `Logs/dbg_em_full` | edit | 전량 | ~9ad6279 | ? | **↑OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 18:33 | `Logs/sync_edit` | edit | 전량 | ~09ab271 | ? | **~OSX** | 28 | 28 | 0 | 0 | 0 | 초록 | — |
| 08-29 18:33 | `Logs/sync_play` | play | 전량 | ~09ab271 | ? | **↑OSX** | 139 | 138 | 1 | 0 | 0 | **빨강** | ParkourClimbFollowsMovedWindowWithoutDrawnPositionLag |
| 08-29 18:40 | `Logs/sync_play_full` | play | 전량 | ~09ab271 | ? | **↑OSX** | 139 | 138 | 1 | 0 | 0 | **빨강** | FeetVisuallyTouchScreenBottomAndAreNeverClipped |
| 08-29 18:46 | `Logs/sync_play_full2` | play | 전량 | ~09ab271 | ? | **↑OSX** | 139 | 139 | 0 | 0 | 0 | 초록 | — |
| 08-29 19:23 | `Logs/dbg_dock_play_full` | play | 전량 | ~5acafd1 | ? | **~OSX** | 141 | 141 | 0 | 0 | 0 | 초록 | — |
| 08-29 19:29 | `Logs/dbg_dock_edit` | edit | 전량 | ~5acafd1 | ? | **~OSX** | 31 | 31 | 0 | 0 | 0 | 초록 | — |
| 08-29 19:39 | `Logs/dbg_dock_play_final` | play | 전량 | ~5acafd1 | ? | **↑OSX** | 141 | 141 | 0 | 0 | 0 | 초록 | — |
| 08-29 19:44 | `Logs/dbg_dock_edit_final` | edit | 전량 | ~5acafd1 | ? | **↑OSX** | 31 | 31 | 0 | 0 | 0 | 초록 | — |
| 08-29 20:15 | `Logs/dbg_edit` | edit | 전량 | ~7128f87 | ? | **~OSX** | 31 | 31 | 0 | 0 | 0 | 초록 | — |
| 08-29 21:10 | `Logs/coder_theft_edit1` | edit | 전량 | ~8418e77 | ? | **~OSX** | 42 | 42 | 0 | 0 | 0 | 초록 | — |
| 08-29 21:11 | `Logs/coder_theft_play1` | play | 전량 | ~8418e77 | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-30 05:29 | `Logs/coder_ui_em1` | edit | 전량 | ~1154629 | ? | **↑OSX** | 63 | 63 | 0 | 0 | 0 | 초록 | — |
| 08-30 05:31 | `Logs/coder_ui_pm2` | play | 전량 | ~1154629 | ? | **~OSX** | 169 | 168 | 1 | 0 | 0 | **빨강** | WindowAndStatsDirectorArePlacedExactlyOnce |
| 08-30 05:40 | `Logs/coder_ui_em2` | edit | 전량 | ~1154629 | ? | **↑OSX** | 63 | 63 | 0 | 0 | 0 | 초록 | — |
| 08-30 05:40 | `Logs/coder_ui_pm3` | play | 전량 | ~1154629 | ? | **↑OSX** | 172 | 172 | 0 | 0 | 0 | 초록 | — |
| 08-30 05:59 | `Logs/coder_ui_em3` | edit | 전량 | ~1154629 | ? | **~OSX** | 63 | 63 | 0 | 0 | 0 | 초록 | — |
| 08-30 05:59 | `Logs/coder_ui_pm4` | play | 전량 | ~1154629 | ? | **↑OSX** | 172 | 172 | 0 | 0 | 0 | 초록 | — |
| 08-30 06:09 | `Logs/coder_ui_em4` | edit | 전량 | ~1154629 | ? | **~OSX** | 63 | 63 | 0 | 0 | 0 | 초록 | — |
| 08-30 06:09 | `Logs/coder_ui_pm5` | play | 전량 | ~1154629 | ? | **↑OSX** | 172 | 172 | 0 | 0 | 0 | 초록 | — |
| 08-30 07:59 | `Logs/step_edit` | edit | 전량 | ~e1dd86d | ? | **↑OSX** | 63 | 63 | 0 | 0 | 0 | 초록 | — |
| 08-30 07:59 | `Logs/step_play1` | play | 전량 | ~e1dd86d | ? | **↑OSX** | 188 | 187 | 1 | 0 | 0 | **빨강** | HiddenPoseDrawsNothingAndStandingPoseDrawsLines |
| 08-30 08:07 | `Logs/step_rep1` | play | 전량 | ~e1dd86d | ? | **~OSX** | 188 | 187 | 1 | 0 | 0 | **빨강** | SpinDirectionFollowsThrowDirection |
| 08-30 08:13 | `Logs/step_rep2` | play | 전량 | ~e1dd86d | ? | **↑OSX** | 188 | 188 | 0 | 0 | 0 | 초록 | — |
| 08-30 08:19 | `Logs/step_rep3` | play | 전량 | ~e1dd86d | ? | **↑OSX** | 188 | 188 | 0 | 0 | 0 | 초록 | — |
| 08-30 08:56 | `Logs/dbg3_em_final` | edit | 전량 | ~e1dd86d | ? | **~OSX** | 63 | 63 | 0 | 0 | 0 | 초록 | — |
| 08-30 08:56 | `Logs/dbg3_pm_final` | play | 전량 | ~e1dd86d | ? | **↑OSX** | 190 | 190 | 0 | 0 | 0 | 초록 | — |
| 08-30 09:43 | `Logs/coder_em_final2` | edit | 전량 | ~c9b39d6 | ? | **↑OSX** | 63 | 63 | 0 | 0 | 0 | 초록 | — |
| 08-30 09:56 | `Logs/coder_gear_edit` | edit | 전량 | ~be751b7 | ? | **~OSX** | 67 | 67 | 0 | 0 | 0 | 초록 | — |
| 08-30 09:56 | `Logs/coder_gear_play` | play | 전량 | ~be751b7 | ? | **↑OSX** | 198 | 198 | 0 | 0 | 0 | 초록 | — |
| 08-30 10:31 | `Logs/coder_fan_edit` | edit | 전량 | ~b2bd722 | ? | **~OSX** | 67 | 67 | 0 | 0 | 0 | 초록 | — |
| 08-30 10:31 | `Logs/coder_fan_play` | play | 전량 | ~b2bd722 | ? | **↑OSX** | 205 | 205 | 0 | 0 | 0 | 초록 | — |
| 08-30 10:58 | `Logs/coder_fan_edit2` | edit | 전량 | ~b2bd722 | ? | **↑OSX** | 74 | 74 | 0 | 0 | 0 | 초록 | — |
| 08-30 10:58 | `Logs/coder_wire3_edit` | edit | 전량 | ~b2bd722 | ? | **↑OSX** | 74 | 74 | 0 | 0 | 0 | 초록 | — |
| 08-30 10:58 | `Logs/coder_wire3_play` | play | 전량 | ~b2bd722 | ? | **↑OSX** | 213 | 210 | 3 | 0 | 0 | **빨강** | DeeperFallCrouchesDeeperAndLonger<br>HighFallLandsInCrouchWithoutRagdoll<br>LookAroundSignalRaisesOneArmAndShiftsHead |
| 08-30 11:05 | `Logs/coder_fan_edit3` | edit | 전량 | ~b2bd722 | ? | **~OSX** | 87 | 87 | 0 | 0 | 0 | 초록 | — |
| 08-30 11:06 | `Logs/coder_fan_play2` | play | 전량 | ~b2bd722 | ? | **~OSX** | 223 | 215 | 8 | 0 | 0 | **빨강** | AdjacentButtonsNeverOverlap<br>AllClickBlockersAreDisabledWhenNothingIsOpen<br>DeeperFallCrouchesDeeperAndLonger<br>HighFallLandsInCrouchWithoutRagdoll<br>LargestTileSizeStillClimbsBackOntoDock<br>LookAroundSignalRaisesOneArmAndShiftsHead<br>StepUpCoversDrop_TileSize80<br>ThreeEventConsumersArePlacedExactlyOnce |
| 08-30 11:13 | `Logs/coder_wire3_play2` | play | 전량 | ~b2bd722 | ? | **~OSX** | 223 | 219 | 4 | 0 | 0 | **빨강** | AdjacentButtonsNeverOverlap<br>AllClickBlockersAreDisabledWhenNothingIsOpen<br>StepUpCoversDrop_TileSize80<br>StretchSignalRaisesBothArmsOverhead |
| 08-30 11:21 | `Logs/coder_fan_play3` | play | 전량 | ~b2bd722 | ? | **~OSX** | 223 | 219 | 4 | 0 | 0 | **빨강** | IdleAmbientMotionDisabledKeepsNeutralPose<br>LookAroundSignalRaisesOneArmAndShiftsHead<br>StepUpCoversDrop_TileSize80<br>StretchSignalRaisesBothArmsOverhead |
| 08-30 11:29 | `Logs/coder_wire3_play3` | play | 전량 | ~b2bd722 | ? | **~OSX** | 223 | 221 | 2 | 0 | 0 | **빨강** | StepUpCoversDrop_TileSize80<br>StretchSignalRaisesBothArmsOverhead |
| 08-30 11:38 | `Logs/coder_wire3_play4` | play | 전량 | ~b2bd722 | ? | **~OSX** | 223 | 221 | 2 | 0 | 0 | **빨강** | StepUpCoversDrop_TileSize80<br>StretchSignalRaisesBothArmsOverhead |
| 08-30 11:48 | `Logs/coder_wire3_play5` | play | 전량 | ~b2bd722 | ? | **~OSX** | 223 | 221 | 2 | 0 | 0 | **빨강** | LargestTileSizeStillClimbsBackOntoDock<br>StepUpCoversDrop_TileSize80 |
| 08-30 11:56 | `Logs/coder_fan_play4` | play | 전량 | ~b2bd722 | ? | **~OSX** | 223 | 222 | 1 | 0 | 0 | **빨강** | StepUpCoversDrop_TileSize80 |
| 08-30 12:04 | `Logs/coder_fan_edit4` | edit | 전량 | ~b2bd722 | ? | **↑OSX** | 87 | 87 | 0 | 0 | 0 | 초록 | — |
| 08-30 15:33 | `Logs/te_em1` | edit | 전량 | ~4f21fca | ? | **~OSX** | 142 | 141 | 1 | 0 | 0 | **빨강** | 설명에_이_앱에_없는_소리를_주장하지_않는다 |
| 08-30 15:34 | `Logs/te_pm1` | play | 전량 | ~4f21fca | ? | **↑OSX** | 238 | 236 | 2 | 0 | 0 | **빨강** | LongCapeTripStopsImmediatelyWhenTheCapeIsRemoved<br>NewAppearanceComponentsExistOnPlayerOnlyAndAddNoColliders |
| 08-30 15:46 | `Logs/te_em2` | edit | 전량 | ~4f21fca | ? | **~OSX** | 142 | 141 | 1 | 0 | 0 | **빨강** | 설명에_이_앱에_없는_소리를_주장하지_않는다 |
| 08-30 15:46 | `Logs/te_pm2` | play | 전량 | ~4f21fca | ? | **↑OSX** | 238 | 238 | 0 | 0 | 0 | 초록 | — |
| 08-30 17:33 | `Logs/te_final_em` | edit | 전량 | ~4f21fca | ? | **↑OSX** | 143 | 143 | 0 | 0 | 0 | 초록 | — |
| 08-30 17:33 | `Logs/te_final_pm` | play | 전량 | ~4f21fca | ? | **↑OSX** | 238 | 237 | 1 | 0 | 0 | **빨강** | LargestTileSizeStillClimbsBackOntoDock |
| 08-30 17:44 | `Logs/te_final_pm2` | play | 전량 | ~4f21fca | ? | **↑OSX** | 238 | 238 | 0 | 0 | 0 | 초록 | — |
| 08-30 17:51 | `Logs/te_final_pm3` | play | 전량 | ~4f21fca | ? | **↑OSX** | 238 | 238 | 0 | 0 | 0 | 초록 | — |
| 08-30 18:08 | `Logs/dbg_m1_base_play1` | play | 전량 | ~4f21fca | ? | **↑OSX** | 238 | 238 | 0 | 0 | 0 | 초록 | — |
| 08-30 18:26 | `Logs/dbg_m1_edit1` | edit | 전량 | ~4f21fca | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-30 18:26 | `Logs/dbg_m1_play1` | play | 전량 | ~4f21fca | ? | **↑OSX** | 245 | 241 | 4 | 0 | 0 | **빨강** | WallStandoffFitsInsideEdgeBand_TileSize128<br>WallStandoffFitsInsideEdgeBand_TileSize16<br>WallStandoffFitsInsideEdgeBand_TileSize48<br>WallStandoffFitsInsideEdgeBand_TileSize80 |
| 08-30 18:35 | `Logs/dbg_m1_play2` | play | 전량 | ~4f21fca | ? | **↑OSX** | 245 | 245 | 0 | 0 | 0 | 초록 | — |
| 08-30 18:44 | `Logs/dbg_m1_edit2` | edit | 전량 | ~4f21fca | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-30 18:54 | `Logs/dbg_m1_final_edit` | edit | 전량 | ~4f21fca | ? | **~OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-30 19:17 | `Logs/dbg_m1_final_play1` | play | 전량 | ~4f21fca | ? | **↑OSX** | 245 | 245 | 0 | 0 | 0 | 초록 | — |
| 08-30 21:07 | `Logs/coder_modal_play` | play | 전량 | ~4f21fca | ? | **↑OSX** | 255 | 253 | 2 | 0 | 0 | **빨강** | ClippedActionButtonIsNotClickableAndComesBackWhenVisibleAgain<br>FullscreenDetectionHidesEveryUiSurfaceAndItsClickBlocker |
| 08-30 21:19 | `Logs/coder_modal_edit` | edit | 전량 | ~4f21fca | ? | **~OSX** | 143 | 143 | 0 | 0 | 0 | 초록 | — |
| 08-30 21:19 | `Logs/coder_modal_play3` | play | 전량 | ~4f21fca | ? | **↑OSX** | 253 | 253 | 0 | 0 | 0 | 초록 | — |
| 08-30 21:49 | `Logs/te2_em` | edit | 전량 | ~4f21fca | ? | **↑OSX** | 143 | 143 | 0 | 0 | 0 | 초록 | — |
| 08-30 21:49 | `Logs/te2_pm1` | play | 전량 | ~4f21fca | ? | **↑OSX** | 253 | 253 | 0 | 0 | 0 | 초록 | — |
| 08-30 22:00 | `Logs/te2_pm2` | play | 전량 | ~4f21fca | ? | **~OSX** | 253 | 253 | 0 | 0 | 0 | 초록 | — |
| 08-30 22:09 | `Logs/te2_pm3` | play | 전량 | ~4f21fca | ? | **↑OSX** | 253 | 253 | 0 | 0 | 0 | 초록 | — |
| 08-31 05:35 | `Logs/coder_em1` | edit | 전량 | ~7335745 | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 05:40 | `Logs/coder_pm1` | play | 전량 | ~7335745 | ? | **↑OSX** | 268 | 265 | 2 | 1 | 0 | **빨강** | NegativeControl_액세서리_상쇄를_끄면_이중_스케일이_실제로_생긴다<br>액세서리_컨테이너의_월드_스케일은_모든_배율에서_1이다 |
| 08-31 05:53 | `Logs/coder_em2` | edit | 전량 | ~7335745 | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 05:53 | `Logs/coder_pm2` | play | 전량 | ~7335745 | ? | **↑OSX** | 268 | 267 | 0 | 1 | 0 | 초록 | — |
| 08-31 06:11 | `Logs/coder_em_final` | edit | 전량 | ~7335745 | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 06:12 | `Logs/coder_pm_final` | play | 전량 | ~7335745 | ? | **↑OSX** | 270 | 264 | 6 | 0 | 0 | **빨강** | FallenPoseKeepsTheWholeHeadAndEveryStrokeInsideTheFrame<br>HiddenPoseDrawsNothingAndStandingPoseDrawsLines<br>PortraitCameraRunsOnlyWhileWindowIsOpen<br>PortraitDoesNotFollowTheRealCharacterWhileDragged<br>RagdollThrowTumbleAndGetupAllGetAFramedPortrait<br>StageSitsFarOutsideMainCameraViewAndHasNoColliders |
| 08-31 06:22 | `Logs/coder_pm_final2` | play | 전량 | ~7335745 | ? | **↑OSX** | 270 | 269 | 1 | 0 | 0 | **빨강** | FeetVisuallyTouchScreenBottomAndAreNeverClipped |
| 08-31 06:33 | `Logs/coder_pm_final3` | play | 전량 | ~7335745 | ? | **↑OSX** | 270 | 270 | 0 | 0 | 0 | 초록 | — |
| 08-31 08:14 | `Logs/coder_full_play2` | play | 전량 | ~7335745 | ? | **↑OSX** | 276 | 276 | 0 | 0 | 0 | 초록 | — |
| 08-31 08:27 | `Logs/coder_edit_final` | edit | 전량 | ~7335745 | ? | **~OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 08:31 | `Logs/te_r5_edit` | edit | 전량 | ~7335745 | ? | **~OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 08:31 | `Logs/te_r5_play` | play | 전량 | ~7335745 | ? | **↑OSX** | 275 | 274 | 0 | 1 | 0 | 초록 | — |
| 08-31 09:06 | `Logs/te_r5_edit_final` | edit | 전량 | ~7335745 | ? | **~OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 09:25 | `Logs/dbg_fix_edit` | edit | 전량 | ~e482101 | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 09:35 | `Logs/dbg_fix_play` | play | 전량 | ~e482101 | ? | **~OSX** | 293 | 284 | 8 | 1 | 0 | **빨강** | DockHopDownBandSurvivesScale<br>FallenPoseKeepsTheWholeHeadAndEveryStrokeInsideTheFrame<br>PrefabGeometryFollowsCharacterScale<br>StanceFootStaysPlantedWhileBodyMovesForward<br>T3_SinkholeRecoversFastAndWithoutHorizontalTeleport<br>T3n_네거티브_계단을_끄면_사각지대회수가_실제로_발동한다<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율을_바꿔도_획이_화면상_최소_두께_아래로_내려가지_않는다 |
| 08-31 09:52 | `Logs/coder_win4_edit` | edit | 전량 | ~e482101 | ? | **~OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 10:03 | `Logs/coder_win4_play` | play | 전량 | ~e482101 | ? | **↑OSX** | 292 | 283 | 8 | 1 | 0 | **빨강** | DockHopDownBandSurvivesScale<br>FallenPoseKeepsTheWholeHeadAndEveryStrokeInsideTheFrame<br>PrefabGeometryFollowsCharacterScale<br>StanceFootStaysPlantedWhileBodyMovesForward<br>T3_SinkholeRecoversFastAndWithoutHorizontalTeleport<br>T3n_네거티브_계단을_끄면_사각지대회수가_실제로_발동한다<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율을_바꿔도_획이_화면상_최소_두께_아래로_내려가지_않는다 |
| 08-31 10:27 | `Logs/te_r6_edit` | edit | 전량 | ~e482101 | ? | **↑OSX** | 147 | 147 | 0 | 0 | 0 | 초록 | — |
| 08-31 10:28 | `Logs/te_r6_play` | play | 전량 | ~e482101 | ? | **↑OSX** | 292 | 282 | 9 | 1 | 0 | **빨강** | DockHopDownBandSurvivesScale<br>FallenPoseKeepsTheWholeHeadAndEveryStrokeInsideTheFrame<br>PrefabGeometryFollowsCharacterScale<br>StanceFootStaysPlantedWhileBodyMovesForward<br>T3_SinkholeRecoversFastAndWithoutHorizontalTeleport<br>T3n_네거티브_계단을_끄면_사각지대회수가_실제로_발동한다<br>던져서_공중회전하는_동안_모자_가시성을_기록한다<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율을_바꿔도_획이_화면상_최소_두께_아래로_내려가지_않는다 |
| 08-31 11:06 | `Logs/coder_b2_edit` | edit | 전량 | ~e482101 | ? | **~WIN** | 150 | 150 | 0 | 0 | 0 | 초록 | — |
| 08-31 11:07 | `Logs/coder_b2_play` | play | 전량 | ~e482101 | ? | **↑WIN** | 296 | 295 | 0 | 1 | 0 | 초록 | — |
| 08-31 11:27 | `Logs/leader_final_edit` | edit | 전량 | ~e482101 | ? | **~WIN** | 150 | 150 | 0 | 0 | 0 | 초록 | — |
| 08-31 11:28 | `Logs/leader_final_play` | play | 전량 | ~e482101 | ? | **↑WIN** | 296 | 295 | 0 | 1 | 0 | 초록 | — |
| 08-31 13:30 | `Logs/coder_walk_em` | edit | 전량 | ~2ae5726 | ? | **↑WIN** | 159 | 159 | 0 | 0 | 0 | 초록 | — |
| 08-31 13:30 | `Logs/coder_walk_pm_full` | play | 전량 | ~2ae5726 | ? | **↑WIN** | 306 | 305 | 0 | 1 | 0 | 초록 | — |
| 08-31 14:02 | `Logs/coder_ink_em` | edit | 전량 | ~2ae5726 | ? | **↑WIN** | 174 | 174 | 0 | 0 | 0 | 초록 | — |
| 08-31 14:02 | `Logs/coder_ink_pm_full` | play | 전량 | ~2ae5726 | ? | **↑WIN** | 314 | 313 | 0 | 1 | 0 | 초록 | — |
| 08-31 14:17 | `Logs/perf_r2_editmode` | edit | 전량 | ~2ae5726 | ? | **↑WIN** | 174 | 174 | 0 | 0 | 0 | 초록 | — |
| 08-31 14:17 | `Logs/perf_r2_playmode` | play | 전량 | ~2ae5726 | ? | **↑WIN** | 314 | 313 | 0 | 1 | 0 | 초록 | — |
| 08-31 14:42 | `Logs/perf_r2_editmode2` | edit | 전량 | ~2ae5726 | ? | **↑WIN** | 174 | 174 | 0 | 0 | 0 | 초록 | — |
| 08-31 14:43 | `Logs/perf_r2_playmode2` | play | 전량 | ~2ae5726 | ? | **↑WIN** | 314 | 313 | 0 | 1 | 0 | 초록 | — |
| 08-31 15:18 | `Logs/te_final_editmode` | edit | 전량 | ~2ae5726 | ? | **↑WIN** | 174 | 174 | 0 | 0 | 0 | 초록 | — |
| 08-31 15:18 | `Logs/te_final_playmode` | play | 전량 | ~2ae5726 | ? | **↑WIN** | 314 | 313 | 0 | 1 | 0 | 초록 | — |
| 08-31 15:42 | `Logs/coder_r5_editmode` | edit | 전량 | ~2ae5726 | ? | **↑WIN** | 178 | 178 | 0 | 0 | 0 | 초록 | — |
| 08-31 15:43 | `Logs/coder_r5_playmode` | play | 전량 | ~2ae5726 | ? | **~WIN** | 314 | 313 | 0 | 1 | 0 | 초록 | — |
| 08-31 15:57 | `Logs/coder_r5_editmode_final` | edit | 전량 | ~2ae5726 | ? | **↑WIN** | 178 | 178 | 0 | 0 | 0 | 초록 | — |
| 08-31 16:39 | `Logs/archery_edit_all` | edit | 전량 | ~f85081b | ? | **↑WIN** | 206 | 206 | 0 | 0 | 0 | 초록 | — |
| 08-31 16:48 | `Logs/dbg_r5_edit` | edit | 전량 | ~f85081b | ? | **~WIN** | 221 | 221 | 0 | 0 | 0 | 초록 | — |
| 08-31 16:50 | `Logs/dbg_r5_play_full` | play | 전량 | ~f85081b | ? | **↑WIN** | 317 | 316 | 0 | 1 | 0 | 초록 | — |
| 08-31 17:06 | `Logs/dbg_r5_edit2` | edit | 전량 | ~f85081b | ? | **~OSX** | 231 | 231 | 0 | 0 | 0 | 초록 | — |
| 08-31 17:35 | `Logs/coder_topology_editmode` | edit | 전량 | ~f85081b | ? | **~OSX** | 245 | 245 | 0 | 0 | 0 | 초록 | — |
| 08-31 17:36 | `Logs/coder_topology_win64` | edit | 전량 | ~f85081b | ? | **~WIN** | 245 | 245 | 0 | 0 | 0 | 초록 | — |
| 08-31 17:46 | `Logs/coderA_baseline` | edit | 전량 | ~f85081b | ? | **↑WIN** | 245 | 245 | 0 | 0 | 0 | 초록 | — |
| 08-31 17:51 | `Logs/te_offline_edit1` | edit | 전량 | ~f85081b | ? | **~OSX** | 257 | 257 | 0 | 0 | 0 | 초록 | — |
| 08-31 17:52 | `Logs/te_offline_edit2` | edit | 전량 | ~f85081b | ? | **~OSX** | 257 | 257 | 0 | 0 | 0 | 초록 | — |
| 08-31 17:57 | `Logs/coderA_after` | edit | 전량 | ~f85081b | ? | **~WIN** | 280 | 280 | 0 | 0 | 0 | 초록 | — |
| 08-31 17:57 | `Logs/coder_edit_osx` | edit | 전량 | ~f85081b | ? | **~OSX** | 280 | 280 | 0 | 0 | 0 | 초록 | — |
| 08-31 20:19 | `Logs/coderA_final_edit` | edit | 전량 | ~f85081b | ? | **~OSX** | 280 | 280 | 0 | 0 | 0 | 초록 | — |
| 08-31 20:23 | `Logs/coderA_unlock` | edit | 전량 | ~f85081b | ? | **~OSX** | 298 | 298 | 0 | 0 | 0 | 초록 | — |
| 08-31 20:24 | `Logs/coderA_unlock_play` | play | 전량 | ~f85081b | ? | **↑OSX** | 328 | 323 | 4 | 1 | 0 | **빨강** | ClimbProbeReachesDecisionDistance_Scale150<br>ClimbProbeReachesDecisionDistance_Scale200<br>ClimbsBackOntoDock_Scale150<br>ClimbsBackOntoDock_Scale200 |
| 08-31 20:39 | `Logs/coder_36_edit` | edit | 전량 | ~f85081b | ? | **~OSX** | 312 | 312 | 0 | 0 | 0 | 초록 | — |
| 08-31 20:39 | `Logs/tilt_edit` | edit | 전량 | ~f85081b | ? | **~OSX** | 312 | 312 | 0 | 0 | 0 | 초록 | — |
| 08-31 20:40 | `Logs/coder_36_play` | play | 전량 | ~f85081b | ? | **↑OSX** | 345 | 133 | 211 | 1 | 0 | **빨강** | 0f)<br>35f)<br>75f)<br>AdjacentButtonsNeverOverlap<br>AllClickBlockersAreDisabledWhenNothingIsOpen<br>AppearanceTabShowsExactlyTheRemainingCategoriesAndNoEmptySection<br>AutoWanderHopsDownAndClimbsBackWithoutScriptedPulses<br>AutoWanderStaysOnDockAfterClimbingBackInsteadOfImmediatelyHoppingDown<br>AutonomousReactionNeverFiresWhileDisabled<br>BothEventConsumersArePlacedExactlyOnce<br>BothHeightQueryPathsAgree<br>CancelledEventRemovesEveryObject<br>CharacterButtonOpensCharacterInfoWindow<br>ClickingBesideAButtonDoesNothingAndInvisibleButtonsIgnoreClicks<br>ClickingCharacterDuringArcheryCancelsEverything<br>ClickingGearAgainCollapsesAndOutsideClickCollapses<br>ClimbsBackOntoDock_TileSize16<br>ClimbsBackOntoDock_TileSize48<br>ClimbsBackOntoDock_TileSize80<br>ClippedActionButtonIsNotClickableAndComesBackWhenVisibleAgain<br>CompletedEventRemovesEveryObject<br>CompletedEventShattersAndRemovesEveryObject<br>DecoyHeadInCharacterHierarchyDoesNotStealTheEyes<br>DeeperFallCrouchesDeeperAndLonger<br>DisablingRendererRemovesEveryObject<br>DockHopDownBandSurvivesScale<br>DockRoundTripHopDownThenClimbBackUp<br>DockStepDropDoesNotTriggerCrouch<br>Dock_구간에서_RAGDOLL이_물리바닥을_뚫지_않는다<br>DragCannotPushIconOffScreen<br>DraggingFarEnoughStartsDragBeforeTheTimeThreshold<br>DroppedPositionSurvivesSceneReload<br>EmoteNeverOverlapsCharacterHead<br>EmptyListIsReportedAsAReasonNotASilentNoOp<br>EveryCommandTileHasAClickableRect<br>EveryPhase5ComponentIsPlacedExactlyOnce<br>EveryReactionKindDrawsAndFullyCleansUp<br>FallenPoseKeepsTheWholeHeadAndEveryStrokeInsideTheFrame<br>FanStaysOnScreenFromEveryCorner<br>FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>FocusPopoverStartsTheChosenDurationNotTheNinetySecondDemo<br>FocusSessionDrawsTimerRingAndTierVisualsThenCleansUp<br>ForcedReminderNeverWritesFakeTodosIntoTheRealList<br>FullCycleFiresExactlyThreeArrowsAndFullyCleansUp<br>FullscreenDetectionHidesEveryUiSurfaceAndItsClickBlocker<br>FxPiecesRepaintThemselvesWhenTheInkColorChanges<br>GaugeAtAnyLevelDrawsNothingWithShippedConfig<br>GearClickWhileWindowIsOpenClosesItWithoutExpandingTheFan<br>GentleReleaseDoesNotTumble<br>GreyOutAlwaysMatchesTheRealAvailabilityJudgement<br>HandTipTouchesLedgeTopAtDefaultScale<br>HandTipTouchesLedgeTopAtMaxScale<br>HandTipTouchesLedgeTopAtUserSavedMinScale<br>HeldCharacterActuallyStruggles<br>HiddenPoseDrawsNothingAndStandingPoseDrawsLines<br>HiddenRunawayCharacterStaysClickableAndRevealsOnHitboxDown<br>HighFallLandsInCrouchWithoutRagdoll<br>HighLandingRaisesDustAtFeetAndCleansUp<br>HitRectCoversBothGearsAndNothingElse<br>HopsDownThenClimbsBackOntoDock<br>IdleAmbientMotionDisabledKeepsNeutralPose<br>InteractiveRectCoversButtonsOnlyWhileExpanded<br>LandingDustDisabledDrawsNothing<br>LargeDropStillHangsAndRejectsHopDownPulse<br>LargestTileSizeStillClimbsBackOntoDock<br>LedgeHangAlwaysReleasesAtMaxDurationTimeout<br>LedgeHangDescendsThenFallsOntoLowerFoothold<br>LedgeHangFallsImmediatelyWhenGrabbedFootholdDisappears<br>LedgeHangFollowsMovedWindowWithoutDrawnPositionLag<br>LongCapeTripImpulseMustActuallyExceedTheRagdollThreshold<br>LongCapeTripIsArmedWhileWalkingAndYieldsToLockSuspendAndIdle<br>LongCapeTripStopsImmediatelyWhenTheCapeIsRemoved<br>LongPressTurnsIntoDragAndNeverOpensWindow<br>LookAroundSignalRaisesOneArmAndSweepsEyesWithoutMovingHead<br>ManualPathStillWorksWhileDisabledAndFlagRestoresAutonomous<br>ModalSortsAboveFanAndPopoversAndDiffersFromBubble<br>NegativeControl_CrouchDisabledSkipsTheWholePerformance<br>NegativeControl_DisablingStruggleFreezesJoints<br>NegativeControl_DisablingTumbleFallsBackToRagdoll<br>NegativeControl_LegacyMantleInsetIsActuallyRestoredOnlyWhenDerivationIsOff<br>NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately<br>NegativeControl_수정_전_규칙은_실제_잉크보다_크게_부풀어_있다<br>NegativeControl_액세서리_상쇄를_끄면_이중_스케일이_실제로_생긴다<br>NegativeControl_직렬화_필드에_직접_쓰면_이_검사가_실제로_잡아낸다<br>NegativeControl_하한을_빼면_액세서리_획이_실제로_하한_아래로_내려간다<br>NewAppearanceComponentsArePlacedOnceAndAddNoColliders<br>NewReactionReplacesPreviousInsteadOfStacking<br>NoNameIsShownWhileTheCursorIsOffEveryButton<br>NoStartedEventMeansNothingIsDrawn<br>O1_가려진_경계에서는_접지되지_않는다<br>O1n_네거티브_수정_전_열거_규칙이면_같은_자리에서_접지된다<br>O2_실제로_보이는_구간에서는_여전히_접지된다<br>O3_맨_앞_창은_아무_영향도_받지_않는다<br>O4_가려진_경계_위에_세워두면_그_자리에_머물지_못한다<br>OnlyTheHoveredButtonShowsItsName<br>OpeningWhilePopoverIsUpLeavesOnlyTheWindow<br>OutsideClickClosesWindowButInsideAndGearDoNot<br>P1_공펫은_최대화된_창이_있어도_주인이_선_Dock_위에_남는다<br>P1n_네거티브컨트롤_옛_공식은_같은_배치에서_화면_꼭대기를_답한다<br>P2_주인이_창에서_Dock으로_옮겨가면_공도_따라온다<br>P3_주인이_공중이면_공은_마지막_발판_위에서_기다린다<br>ParkourClimbFollowsMovedWindowWithoutDrawnPositionLag<br>PetBallDoesNotSpinThousandsOfDegreesOnItsFirstFrame<br>PopoverIsWiredOnTheSameObjectAsTheFan<br>PortraitCameraRunsOnlyWhileWindowIsOpen<br>PortraitDoesNotFollowTheRealCharacterWhileDragged<br>PrefabGeometryFollowsCharacterScale<br>PressingAButtonAndReleasingOutsideCancels<br>QuitButtonExistsAndRequiresTwoSteps<br>RagdollEntersAndRecoversToActiveState<br>RagdollThrowTumbleAndGetupAllGetAFramedPortrait<br>RecallChipIsHiddenWhileTheCharacterIsHome<br>RepeatedForcedRemindersStillLeaveTheListEmpty<br>RestoringOriginalThresholdMakesItVisibleAgain<br>RunawayHideAndReturnKeepDrawnPositionInSyncWithBody<br>RunawayLifecycleDrawsPhasesAndSnackIsTheOnlyClickTarget<br>SavedPositionOutsideTheScreenIsPulledBackOnStartup<br>SceneHasExactlyOneDirectorAndOneRenderer<br>ShieldDecidesWhetherFullSpeedGroundImpactBecomesRagdoll<br>ShippedConfigAssetHasUnreachableCautionThreshold<br>ShortClickExpandsThreeButtonsInSequence<br>ShortClickStillSpinsAndDoesNotMoveIcon<br>SmallDropStepsOffAndLandsOnLowerFoothold<br>SpinDirectionFollowsThrowDirection<br>StageSitsFarOutsideMainCameraViewAndHasNoColliders<br>StanceFootStaysPlantedAtMaxScale<br>StanceFootStaysPlantedAtUserSavedMinScale<br>StanceFootStaysPlantedWhileBodyMovesForward<br>StandsExactlyOnFootholdTopAtCurrentScale<br>StartedEventActuallyCreatesCrackObjectsAndStaysClickThrough<br>StartedEventActuallyCreatesGhostWindowObjects<br>StartedEventActuallyCreatesTargetAndStaysClickThrough<br>StatusCaptionIsAlwaysDerivedFromRealState<br>StaysInsideScreenWhilePushingEdgeAtCurrentScale<br>StepUpCoversDrop_TileSize128<br>StepUpCoversDrop_TileSize16<br>StepUpCoversDrop_TileSize48<br>StepUpCoversDrop_TileSize80<br>StickmanFallsSettlesAndWanders<br>StickmanStaysWithinVerticalViewportMargin<br>StressTierChangeDrawsMoodAndFullyCleansUp<br>StretchSignalRaisesBothArmsOverhead<br>StruggleDoesNotBreakCursorStickiness<br>StrugglePoseCarriesIntoTumbleWithoutSnap<br>StruggleRhythmHasBurstAndRestPhases<br>SwappingItemWithinTheSameCategoryActuallyRedrawsTheShape<br>T1_AttackOnDockDoesNotRagdollOrSinkBelowDock<br>T1_계단이_있으면_Dock에서_접지가_끊겨도_거의_안_떨어진다<br>T1b_BattleMinigameOnDockDoesNotRagdoll<br>T1c_ShieldAloneStopsRagdollOnRealFloorImpact<br>T1n_NegativeControl_WithoutSafetyNetAttackFallsBelowDock<br>T1n_네거티브_계단을_끄면_깊은_낙하가_재현된다<br>T2_GetupOnDockDoesNotRagdoll<br>T2_계단_기하가_Dock_발판과_정확히_일치한다<br>T3_SinkholeRecoversFastAndWithoutHorizontalTeleport<br>T3_계단이_있으면_사각지대회수가_한_번도_발동하지_않는다<br>T3n_NegativeControl_WithoutLiftRecoveryStaysStuckAndTeleports<br>T3n_네거티브_계단을_끄면_사각지대회수가_실제로_발동한다<br>T4_Dock_발판이_사라지면_계단도_즉시_사라진다<br>T4_RealExternalImpactStillRagdolls<br>TextItselfIsTilted_AndTheTiltMirrorsTheSideItSitsOn<br>TheHoverLabelChangesNeitherLayoutNorClickBlocking<br>TheHoverLabelStaysOnScreenForEveryButton<br>TheNameDisappearsWhenTheCursorLeaves<br>TheNameIsGoneAfterTheFanCollapses<br>TheRealUserEntryPathStillWorks<br>ThrowTumbleDerivationIsScaleInvariant<br>ThrownCharacterTumblesThenLandsInCrouchWithoutRagdoll<br>TitleBarDragMovesWindowStaysOnScreenAndReopenRecenters<br>TodoPopoverAddsAndTogglesThroughTheRealPath<br>TodoPostItCheckboxIsActuallyClickableThroughUguiRaycast<br>TodoReminderStateDrawsHeldPaperAndCleansUp<br>TwoGearsSpinInOppositeDirectionsAtTheToothRatio<br>WalkableBoundsAreExactlyWhereTheHardClampStopsTheCharacter<br>WalksIntoScreenEdgeAndTurnsAroundWithinAbsoluteDeadline<br>WallStandoffFitsInsideEdgeBand_TileSize128<br>WallStandoffFitsInsideEdgeBand_TileSize16<br>WallStandoffFitsInsideEdgeBand_TileSize48<br>WallStandoffFitsInsideEdgeBand_TileSize80<br>WindowAndStatsDirectorArePlacedExactlyOnce<br>WithoutFullscreenDetectionNothingIsHidden<br>가출_은신_프레임에도_액세서리_펫_FX가_함께_사라진다<br>기상_중_어떤_정착각에서도_잉크가_화면_아래로_잘리지_않는다<br>네거티브컨트롤_바닥_클리어런스를_끄면_같은_스윕이_바닥을_뚫는다<br>다이얼을_돌리면_캐릭터_크기가_실제로_바뀐다<br>던져서_공중회전하는_동안_모자_가시성을_기록한다<br>랙돌_회전_중_액세서리_가시성을_기록한다<br>망토_채움이_흔들리는_윤곽선을_따라가는가<br>모자_채움_면이_실제로_생기고_머리_링_윗호를_덮는다<br>몸보다_튀어나온_액세서리가_보고_반폭에_포함된다<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_바꿔도_랙돌로_무너지지_않는다<br>배율을_바꿔도_획이_화면상_최소_두께_아래로_내려가지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다<br>상태별_모자_가시성_전수_스윕<br>숨어_있는_동안_클릭_차단막이_꺼져_있다<br>씬을_다시_로드하면_배포_기본_배율로_돌아온다<br>씬을_다시_로드하면_배포_기본_잉크색으로_돌아온다<br>액세서리_컨테이너의_월드_스케일은_모든_배율에서_1이다<br>왕관은_채움이_없다는_사실을_기록한다<br>잉크색_전환이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>전체화면_감지_프레임에_액세서리_펫_FX가_한_개도_남지_않는다<br>전체화면_감지에서_즉시_거둔다<br>좌우반전을_20회_반복해도_모자_채움이_항상_유효하다<br>채움_재질이_양면인지_와인딩_반전_렌더로_실측한다<br>패널_컴포넌트가_캐릭터_프리팹에_실제로_붙어_있다 |
| 08-31 21:08 | `Logs/coder_36_play2` | play | 전량 | ~f85081b | ? | **↑OSX** | 361 | 345 | 12 | 1 | **3** | **빨강** | EveryCommandTileHasAClickableRect<br>GreyOutAlwaysMatchesTheRealAvailabilityJudgement<br>OnlyTheHoveredButtonShowsItsName<br>PopoverIsWiredOnTheSameObjectAsTheFan<br>QuitButtonExistsAndRequiresTwoSteps<br>RecallChipIsHiddenWhileTheCharacterIsHome<br>StatusCaptionIsAlwaysDerivedFromRealState<br>TheHoverLabelStaysOnScreenForEveryButton<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 02:17 | `Logs/tilt_edit_final` | edit | 전량 | ~f85081b | ? | **~OSX** | 333 | 333 | 0 | 0 | 0 | 초록 | — |
| 09-01 02:19 | `Logs/coder_36_play3` | play | 전량 | ~f85081b | ? | **↑OSX** | 379 | 372 | 6 | 1 | 0 | **빨강** | F2_던지기_공중회전을_미니가_그대로_따라간다<br>F3_무릎앉아_착지에서_미니도_함께_웅크린다<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 02:36 | `Logs/coder_36_edit2` | edit | 전량 | ~f85081b | ? | **~OSX** | 364 | 364 | 0 | 0 | 0 | 초록 | — |
| 09-01 02:36 | `Logs/alpha_edit_all` | edit | 전량 | ~f85081b | ? | **↑OSX** | 364 | 364 | 0 | 0 | 0 | 초록 | — |
| 09-01 02:36 | `Logs/alpha_play_all` | play | 전량 | ~f85081b | ? | **↑OSX** | 379 | 374 | 4 | 1 | 0 | **빨강** | 배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 02:52 | `Logs/coder_hair_edit` | edit | 전량 | ~f85081b | ? | **↑OSX** | 364 | 364 | 0 | 0 | 0 | 초록 | — |
| 09-01 02:52 | `Logs/coder_hair_play` | play | 전량 | ~f85081b | ? | **↑OSX** | 379 | 374 | 4 | 1 | 0 | **빨강** | 배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 06:49 | `Logs/p1_edit` | edit | 전량 | ~f85081b | ? | **↑OSX** | 393 | 393 | 0 | 0 | 0 | 초록 | — |
| 09-01 06:54 | `Logs/p1_play_all` | play | 전량 | ~f85081b | ? | **↑OSX** | 383 | 376 | 5 | 2 | 0 | **빨강** | TwoGearsSpinInOppositeDirectionsAtTheToothRatio<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 07:12 | `Logs/p1_edit2` | edit | 전량 | ~f85081b | ? | **↑OSX** | 394 | 394 | 0 | 0 | 0 | 초록 | — |
| 09-01 07:32 | `Logs/p9_edit1` | edit | 전량 | ~f85081b | ? | **↑OSX** | 407 | 407 | 0 | 0 | 0 | 초록 | — |
| 09-01 07:38 | `Logs/p9_edit2` | edit | 전량 | ~f85081b | ? | **↑OSX** | 407 | 407 | 0 | 0 | 0 | 초록 | — |
| 09-01 07:38 | `Logs/p9_play_all` | play | 전량 | ~f85081b | ? | **↑OSX** | 388 | 382 | 4 | 2 | 0 | **빨강** | 배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 07:54 | `Logs/p9_edit3` | edit | 전량 | ~f85081b | ? | **~OSX** | 407 | 407 | 0 | 0 | 0 | 초록 | — |
| 09-01 08:15 | `Logs/p9b_edit_all` | edit | 전량 | ~f85081b | ? | **↑OSX** | 415 | 415 | 0 | 0 | 0 | 초록 | — |
| 09-01 08:22 | `Logs/p9b_play_all` | play | 전량 | ~f85081b | ? | **↑OSX** | 394 | 388 | 4 | 2 | 0 | **빨강** | 배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 08:52 | `Logs/p9b_fix_play_all` | play | 전량 | ~f85081b | ? | **~OSX** | 394 | 388 | 4 | 2 | 0 | **빨강** | 배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 09:31 | `Logs/p9b_final_edit` | edit | 전량 | ~f85081b | ? | **↑OSX** | 441 | 438 | 3 | 0 | 0 | **빨강** | v5_왕복은_카테고리마다_고른_아이템을_아이디로_보존한다<br>구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_색은_여전히_고른_적_없음이다<br>요구_레벨은_카테고리_안에서_점점_높아지고_전체적으로_퍼져_있다 |
| 09-01 09:32 | `Logs/p9b_final_play` | play | 전량 | ~f85081b | ? | **↑OSX** | 410 | 402 | 6 | 2 | 0 | **빨강** | CardEquipButtonWearsAndCategoryStaysMutuallyExclusive<br>EyesAreAbsentUnderEveryGlassesItem<br>몸보다_튀어나온_액세서리가_보고_반폭에_포함된다<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다<br>상자가_다_자란_뒤에_다이얼이_나타난다 |
| 09-01 09:48 | `Logs/coder_edit_full` | edit | 전량 | ~f85081b | ? | **~OSX** | 441 | 438 | 3 | 0 | 0 | **빨강** | v5_왕복은_카테고리마다_고른_아이템을_아이디로_보존한다<br>구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_색은_여전히_고른_적_없음이다<br>요구_레벨은_카테고리_안에서_점점_높아지고_전체적으로_퍼져_있다 |
| 09-01 09:50 | `Logs/coder_edit_full2` | edit | 전량 | ~f85081b | ? | **↑OSX** | 441 | 439 | 2 | 0 | 0 | **빨강** | v5_왕복은_카테고리마다_고른_아이템을_아이디로_보존한다<br>구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_색은_여전히_고른_적_없음이다 |
| 09-01 09:50 | `Logs/coder_play_full2` | play | 전량 | ~f85081b | ? | **↑OSX** | 410 | 406 | 2 | 2 | 0 | **빨강** | EyesAreAbsentUnderEveryGlassesItem<br>몸보다_튀어나온_액세서리가_보고_반폭에_포함된다 |
| 09-01 10:05 | `Logs/dbg_full_edit_r1` | edit | 전량 | ~f85081b | ? | **↑OSX** | 441 | 439 | 2 | 0 | 0 | **빨강** | v5_왕복은_카테고리마다_고른_아이템을_아이디로_보존한다<br>구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_색은_여전히_고른_적_없음이다 |
| 09-01 10:05 | `Logs/dbg_full_play_r1` | play | 전량 | ~f85081b | ? | **↑OSX** | 410 | 406 | 2 | 2 | 0 | **빨강** | EyesAreAbsentUnderEveryGlassesItem<br>몸보다_튀어나온_액세서리가_보고_반폭에_포함된다 |
| 09-01 10:21 | `Logs/dbg_full_edit_r2` | edit | 전량 | ~f85081b | ? | **~OSX** | 441 | 439 | 2 | 0 | 0 | **빨강** | v5_왕복은_카테고리마다_고른_아이템을_아이디로_보존한다<br>구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_색은_여전히_고른_적_없음이다 |
| 09-01 10:21 | `Logs/dbg_full_play_r2` | play | 전량 | ~f85081b | ? | **↑OSX** | 410 | 407 | 1 | 2 | 0 | **빨강** | 몸보다_튀어나온_액세서리가_보고_반폭에_포함된다 |
| 09-01 10:37 | `Logs/dbg_full_edit_r3` | edit | 전량 | ~f85081b | ? | **↑OSX** | 441 | 439 | 2 | 0 | 0 | **빨강** | v5_왕복은_카테고리마다_고른_아이템을_아이디로_보존한다<br>구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_색은_여전히_고른_적_없음이다 |
| 09-01 10:51 | `Logs/coder_edit_fix3` | edit | 전량 | ~f85081b | ? | **~OSX** | 441 | 440 | 1 | 0 | 0 | **빨강** | 구버전_파일을_읽은_뒤_저장하면_v7로_올라가고_색은_여전히_고른_적_없음이다 |
| 09-01 10:56 | `Logs/dbg_full_play_r3` | play | 전량 | ~f85081b | ? | **↑OSX** | 411 | 408 | 1 | 2 | 0 | **빨강** | 몸보다_튀어나온_액세서리가_보고_반폭에_포함된다 |
| 09-01 11:16 | `Logs/coder_lean_edit3` | edit | 전량 | ~f85081b | ? | **↑OSX** | 507 | 507 | 0 | 0 | 0 | 초록 | — |
| 09-01 11:19 | `Logs/coder_visor_edit` | edit | 전량 | ~f85081b | ? | **~OSX** | 507 | 507 | 0 | 0 | 0 | 초록 | — |
| 09-01 11:21 | `Logs/coder_lean_playfull` | play | 전량 | ~f85081b | ? | **~OSX** | 413 | 406 | 5 | 2 | 0 | **빨강** | AllClickBlockersAreDisabledWhenNothingIsOpen<br>ClickingGearAgainCollapsesAndOutsideClickCollapses<br>GearClickWhileWindowIsOpenClosesItWithoutExpandingTheFan<br>InteractiveRectCoversButtonsOnlyWhileExpanded<br>몸보다_튀어나온_액세서리가_보고_반폭에_포함된다 |
| 09-01 11:41 | `Logs/coder_lean_edit4` | edit | 전량 | ~f85081b | ? | **~OSX** | 512 | 512 | 0 | 0 | 0 | 초록 | — |
| 09-01 11:41 | `Logs/dbg2_edit_all` | edit | 전량 | ~f85081b | ? | **~OSX** | 512 | 512 | 0 | 0 | 0 | 초록 | — |
| 09-01 11:41 | `Logs/dbg2_play_all` | play | 전량 | ~f85081b | ? | **↑OSX** | 427 | 418 | 7 | 2 | 0 | **빨강** | AllClickBlockersAreDisabledWhenNothingIsOpen<br>ClickingGearAgainCollapsesAndOutsideClickCollapses<br>ClosingSettingsByClickingOutsideAlsoReturns<br>EveryCardInACategoryIsReachableByScrollingToTheEnd<br>GearClickWhileWindowIsOpenClosesItWithoutExpandingTheFan<br>InteractiveRectCoversButtonsOnlyWhileExpanded<br>몸보다_튀어나온_액세서리가_보고_반폭에_포함된다 |
| 09-01 12:06 | `Logs/coder_silhouette_edit` | edit | 전량 | ~f85081b | ? | **~OSX** | 523 | 523 | 0 | 0 | 0 | 초록 | — |
| 09-01 12:08 | `Logs/coder_silhouette_play` | play | 전량 | ~f85081b | ? | **~OSX** | 429 | 423 | 3 | 3 | 0 | **빨강** | StruggleDoesNotBreakCursorStickiness<br>몸보다_튀어나온_액세서리가_보고_반폭에_포함된다<br>상체가_기울어도_모자는_머리에_그대로_붙어_있다 |
| 09-01 12:39 | `Logs/coder_silhouette_edit2` | edit | 전량 | ~f85081b | ? | **↑OSX** | 563 | 560 | 0 | 3 | 0 | 초록 | — |
| 09-01 12:46 | `Logs/t3_parity_edit_all` | edit | 전량 | ~f85081b | ? | **↑OSX** | 563 | 560 | 0 | 3 | 0 | 초록 | — |
| 09-01 12:58 | `Logs/coder_fall_edit_all` | edit | 전량 | ~f85081b | ? | **~OSX** | 578 | 574 | 1 | 3 | 0 | **빨강** | 지표가_옛_펜던트를_실제로_잡는다 |
| 09-01 13:01 | `Logs/coder_band_bell_edit_all` | edit | 전량 | ~f85081b | ? | **↑OSX** | 578 | 575 | 0 | 3 | 0 | 초록 | — |
| 09-01 13:35 | `Logs/coder_pom_edit_all` | edit | 전량 | ~f85081b | ? | **↑OSX** | 618 | 616 | 0 | 2 | 0 | 초록 | — |
| 09-01 13:59 | `Logs/coder_hang_edit` | edit | 전량 | ~f85081b | ? | **~OSX** | 622 | 620 | 0 | 2 | 0 | 초록 | — |
| 09-01 14:00 | `Logs/coder_hang_full_play` | play | 전량 | ~f85081b | ? | **~OSX** | 474 | 470 | 1 | 3 | 0 | **빨강** | FeetVisuallyTouchScreenBottomAndAreNeverClipped |
| 09-02 00:17 | `Logs/dbg_base_edit` | edit | 전량 | ~d84732b | ? | **↑OSX** | 1167 | 1160 | 0 | 7 | 0 | 초록 | — |
| 09-02 00:21 | `Logs/dbg_after_edit` | edit | 전량 | ~d84732b | ? | **~OSX** | 1167 | 1157 | 3 | 7 | 0 | **빨강** | 모든_직렬화_필드가_에셋에_구워져_있다<br>에셋_키와_직렬화_필드가_정확히_일대일이다<br>에셋이_코드_기본값과_다른_필드는_전부_대장에_등재돼_있다 |
| 09-02 00:35 | `Logs/dbg_final_edit` | edit | 전량 | ~d84732b | ? | **~OSX** | 1167 | 1160 | 0 | 7 | 0 | 초록 | — |
| 09-02 00:37 | `Logs/coder_r4_edit` | edit | 전량 | ~d84732b | ? | **~OSX** | 1171 | 1164 | 0 | 7 | 0 | 초록 | — |
| 09-02 00:38 | `Logs/dbg_final2_edit` | edit | 전량 | ~d84732b | ? | **↑OSX** | 1171 | 1164 | 0 | 7 | 0 | 초록 | — |
| 09-02 00:53 | `Logs/coder_r4_edit_final` | edit | 전량 | ~d84732b | ? | **↑OSX** | 1194 | 1187 | 0 | 7 | 0 | 초록 | — |
| 09-02 00:54 | `Logs/dbg_r2_full_edit` | edit | 전량 | ~d84732b | ? | **~OSX** | 1194 | 1187 | 0 | 7 | 0 | 초록 | — |
| 09-02 05:15 | `Logs/coder_shadowfade_edit` | edit | 전량 | ~728a33f | ? | **~OSX** | 1307 | 1297 | 0 | 10 | 0 | 초록 | — |
| 09-02 05:26 | `Logs/coder_noshadow_edit` | edit | 전량 | ~728a33f | ? | **~OSX** | 1312 | 1299 | 3 | 10 | 0 | **빨강** | 갭추적_만화텍스트_외곽선_하한은_배율1에서_아직_양립하지_않는다<br>네거티브컨트롤_외곽선_비율을_아주_조금_내리면_macOS가_그림자로_넘어간다<br>말풍선_폰트는_스냅_정책을_거쳐서_반환된다 |
| 09-02 05:30 | `Logs/coder_noshadow_edit2` | edit | 전량 | ~728a33f | ? | **~OSX** | 1314 | 1302 | 2 | 10 | 0 | **빨강** | 갭추적_만화텍스트_외곽선_하한은_배율1에서_아직_양립하지_않는다<br>말풍선_폰트는_스냅_정책을_거쳐서_반환된다 |
| 09-02 05:31 | `Logs/coder_edit` | edit | 전량 | ~728a33f | ? | **~OSX** | 1314 | 1302 | 2 | 10 | 0 | **빨강** | 갭추적_만화텍스트_외곽선_하한은_배율1에서_아직_양립하지_않는다<br>말풍선_폰트는_스냅_정책을_거쳐서_반환된다 |
| 09-02 05:31 | `Logs/coder_noshadow_play_all` | play | 전량 | ~728a33f | ? | **~OSX** | 529 | 514 | 10 | 5 | 0 | **빨강** | 0f)<br>5f)<br>75f)<br>ClimbProbeReachesDecisionDistance_Scale125<br>ClimbsBackOntoDock_Scale125<br>FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>다이얼_전_구간에서_채움이_머리_실루엣을_넘지_않는다<br>배율_전_구간에서_치수와_접지와_보행속도가_따라온다<br>배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다 |
| 09-02 05:52 | `Logs/coder_edit2` | edit | 전량 | ~728a33f | ? | **~OSX** | 1314 | 1304 | 0 | 10 | 0 | 초록 | — |
| 09-02 06:00 | `Logs/coder_edit3` | edit | 전량 | ~89de9de | ? | **~OSX** | 1317 | 1304 | 2 | 11 | 0 | **빨강** | macOS_창목록_왕복은_전부_중첩_타이머를_거친다<br>계측코드_자체에는_OS_창조회가_없다 |
| 09-02 06:05 | `Logs/coder_arch_edit` | edit | 전량 | ~89de9de | ? | **~OSX** | 1329 | 1316 | 2 | 11 | 0 | **빨강** | macOS_창목록_왕복은_전부_중첩_타이머를_거친다<br>계측코드_자체에는_OS_창조회가_없다 |
| 09-02 06:13 | `Logs/coder_carry_edit1` | edit | 전량 | ~63b42ee | ? | **~OSX** | 1335 | 1325 | 0 | 10 | 0 | 초록 | — |
| 09-02 06:14 | `Logs/coder_frame_edit` | edit | 전량 | ~63b42ee | ? | **~OSX** | 1335 | 1322 | 3 | 10 | 0 | **빨강** | Windows_창열거도_같은_계측기를_거친다<br>맨틀_직후_1프레임_Idle에서는_대사가_나오지_않는다<br>맨틀_직후_다음_상태는_이동의도가_아니라_확정된_맨틀에서_고른다 |
| 09-02 06:15 | `Logs/coder_carry_mutant` | edit | 전량 | ~63b42ee | ? | **↑OSX** | 1335 | 1322 | 3 | 10 | 0 | **빨강** | Windows_창열거도_같은_계측기를_거친다<br>맨틀_직후_1프레임_Idle에서는_대사가_나오지_않는다<br>맨틀_직후_다음_상태는_이동의도가_아니라_확정된_맨틀에서_고른다 |
| 09-02 06:21 | `Logs/coder_carry_edit_final` | edit | 전량 | ~63b42ee | ? | **~OSX** | 1335 | 1325 | 0 | 10 | 0 | 초록 | — |
| 09-02 07:22 | `Logs/coder_final_edit` | edit | 전량 | ~3c0efb6 | ? | **~OSX** | 1373 | 1363 | 0 | 10 | 0 | 초록 | — |
| 09-02 09:27 | `Logs/coder_hat_editmode` | edit | 전량 | ~079327d | ? | **~WIN** | 1381 | 1368 | 3 | 10 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다<br>모자_6종_실루엣_차이가_2_95획_아래로_내려가지_않는다<br>안쪽_서비스의_선택적_인터페이스는_전부_데코레이터를_통과한다 |
| 09-02 09:29 | `Logs/coder_hat_editmode_base` | edit | 전량 | ~079327d | ? | **~WIN** | 1381 | 1369 | 2 | 10 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다<br>안쪽_서비스의_선택적_인터페이스는_전부_데코레이터를_통과한다 |
| 09-02 09:30 | `Logs/coder_hat_editmode2` | edit | 전량 | ~079327d | ? | **~WIN** | 1381 | 1369 | 2 | 10 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다<br>안쪽_서비스의_선택적_인터페이스는_전부_데코레이터를_통과한다 |
| 09-02 09:31 | `Logs/coder_hat_playmode` | play | 전량 | ~079327d | ? | **↑WIN** | 558 | 549 | 5 | 4 | 0 | **빨강** | FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>G1_앱이_도는_동안_살아있는_오브젝트_바닥선이_올라가지_않는다<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 11:36 | `Logs/dbg2_full_edit` | edit | 전량 | ~890fb1f | ? | **~WIN** | 1394 | 1382 | 1 | 11 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다 |
| 09-02 11:39 | `BASELINE-20260902-1140` | edit | **미확인** | ~890fb1f | ? | **↑WIN** | 1405 | 1393 | 1 | 11 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다 |
| 09-02 11:40 | `BASELINE-20260902-1201` | play | **미확인** | ~890fb1f | ? | **↑WIN** | 563 | 556 | 4 | 3 | 0 | **빨강** | FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 12:20 | `Logs/coder_full_edit` | edit | 전량 | ~23fbb11 | ? | **~WIN** | 1434 | 1419 | 3 | 12 | 0 | **빨강** | NegativeControl_옛_방식으로_반쯤_쓰면_실제로_전손된다<br>네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다<br>손상된_JSON에서도_기록이_기본값으로_떨어지고_크래시하지_않는다 |
| 09-02 12:24 | `Logs/coder_savegen` | edit | 전량 | ~23fbb11 | ? | **↑WIN** | 1442 | 1429 | 1 | 12 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다 |
| 09-02 12:27 | `Logs/coder_final_green` | edit | 전량 | ~23fbb11 | ? | **~WIN** | 1442 | 1429 | 1 | 12 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다 |
| 09-02 13:21 | `qa-baseline` | edit | 전량 | ~aaac7b2 | ? | **↑WIN** | 1442 | 1429 | 1 | 12 | 0 | **빨강** | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다 |
| 09-02 13:22 | `qa-baseline` | play | 전량 | ~aaac7b2 | ? | **↑WIN** | 563 | 553 | 6 | 4 | 0 | **빨강** | FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>StanceFootStaysPlantedWhileBodyMovesForward<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>안_걸치면_신규_4종_미리보기가_하나도_없다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 13:53 | `qa-after-fix` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1460 | 1446 | 3 | 11 | 0 | **빨강** | 상호작용_표면_명부가_빠짐없이_배선돼_있다<br>정보창_홀드는_열려있음이_아니라_조작중일때만_걸린다<br>최단_실제_변_검사를_액세서리_30종으로_확장한다 |
| 09-02 14:24 | `Logs/dbg_edit_all` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1490 | 1475 | 2 | 13 | 0 | **빨강** | 상호작용_표면_명부가_빠짐없이_배선돼_있다<br>정보창_홀드는_열려있음이_아니라_조작중일때만_걸린다 |
| 09-02 14:25 | `Logs/dbg_play_all` | play | 전량 | ~eca8c58 | ? | **↑WIN** | 570 | 558 | 9 | 3 | 0 | **빨강** | CardRowEndsOnTheSameRightEdgeAsItsHeader<br>DraggingRowScrollsContentAndClampsAtBothEnds<br>EveryCardInACategoryIsReachableByScrollingToTheEnd<br>SwitchingTabResetsCarouselToStart<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>에이전트의_Suspend와_Resume이_절대기한을_얼리고_되살린다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 15:14 | `Logs/devplat_edit` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1517 | 1501 | 4 | 12 | 0 | **빨강** | 결정_전체화면_기하_관용은_macOS만_켠다<br>발판_클리핑이_양_플랫폼_모두_오버레이_화면을_따른다<br>역방향_보조모니터_전체화면_감지는_Windows가_낫다<br>해당없음_전체화면_보조창_알파거부권은_Windows_경로에_없다 |
| 09-02 15:15 | `Logs/devplat_edit2` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1517 | 1505 | 0 | 12 | 0 | 초록 | — |
| 09-02 15:22 | `dbg-fix` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1517 | 1505 | 0 | 12 | 0 | 초록 | — |
| 09-02 15:23 | `dbg-fix` | play | 전량 | ~eca8c58 | ? | **↑WIN** | 570 | 561 | 5 | 4 | 0 | **빨강** | G1_앱이_도는_동안_살아있는_오브젝트_바닥선이_올라가지_않는다<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 16:04 | `loc-gate` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1602 | 1586 | 4 | 12 | 0 | **빨강** | 목_형상은_데이터화_전후로_비트까지_같다<br>양성대조_분기를_지우면_한국어는_그대로이고_영어만_달라진다<br>한국어_가독예산이_골든과_비트_단위로_같다<br>한국어_소비자_경로가_골든에서_파생된_값과_같다 |
| 09-02 16:07 | `loc-gate-2` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1602 | 1589 | 1 | 12 | 0 | **빨강** | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:08 | `b2-neck` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1602 | 1589 | 1 | 12 | 0 | **빨강** | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:11 | `b2-probe` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1603 | 1590 | 1 | 12 | 0 | **빨강** | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:13 | `ui-postit` | edit | 전량 | ~eca8c58 | ? | **↑WIN** | 1603 | 1590 | 1 | 12 | 0 | **빨강** | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:24 | `b2-bake` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1609 | 1595 | 2 | 12 | 0 | **빨강** | 목_형상은_데이터화_전후로_비트까지_같다<br>전체화면_판정_한_줄에_사용자숨김을_얹지_않는다 |
| 09-02 16:25 | `ui-postit` | play | 전량 | ~eca8c58 | ? | **~WIN** | 574 | 563 | 7 | 4 | 0 | **빨강** | CardEquipButtonWearsAndCategoryStaysMutuallyExclusive<br>OutsideClickDoesNotCloseWindowButTheCloseButtonStillDoes<br>SavedPositionInsideTheReservedTopBarIsPulledOutOnStartup<br>SavedPositionOutsideTheScreenIsPulledBackOnStartup<br>ShortClickStillSpinsAndDoesNotMoveIcon<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 |
| 09-02 16:46 | `b2-final` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1626 | 1611 | 1 | 14 | 0 | **빨강** | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 16:47 | `qa-round2` | edit | 전량 | ~eca8c58 | ? | **↑WIN** | 1626 | 1611 | 1 | 14 | 0 | **빨강** | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 18:14 | `qa-r3` | edit | 전량 | ~eca8c58 | ? | **↑WIN** | 1626 | 1611 | 1 | 14 | 0 | **빨강** | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 18:16 | `qa-r3` | play | 전량 | ~eca8c58 | ? | **↑WIN** | 578 | 566 | 7 | 5 | 0 | **빨강** | CardEquipButtonWearsAndCategoryStaysMutuallyExclusive<br>OutsideClickDoesNotCloseWindowButTheCloseButtonStillDoes<br>SavedPositionInsideTheReservedTopBarIsPulledOutOnStartup<br>SavedPositionOutsideTheScreenIsPulledBackOnStartup<br>ShortClickStillSpinsAndDoesNotMoveIcon<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 |
| 09-02 18:44 | `qa-r4b` | edit | 전량 | ~eca8c58 | ? | **~WIN** | 1637 | 1623 | 1 | 13 | 0 | **빨강** | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 18:44 | `qa-r4b` | play | 전량 | ~eca8c58 | ? | **↑WIN** | 582 | 576 | 2 | 4 | 0 | **빨강** | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 |
| 09-02 19:27 | `c1-edit` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1674 | 1657 | 2 | 15 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 19:29 | `c1-play` | play | 전량 | ~7ed996d | ? | **↑OSX** | 589 | 579 | 6 | 4 | 0 | **빨강** | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>설정창_톱니_위치_행은_옮긴_뒤에만_눌리고_누르면_되돌아간다<br>온보딩이_지나가도_사용자가_옮겨_둔_자리는_그대로다<br>온보딩이_톱니를_옮겨도_사용자가_옮긴_것으로_저장되지_않는다<br>처음_자리로가_저장까지_되돌리고_다음_프레임에_되살아나지_않는다 |
| 09-02 19:52 | `c2-edit` | edit | 전량 | ~7ed996d | ? | **↑OSX** | 1674 | 1657 | 2 | 15 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 19:53 | `c2-play` | play | 전량 | ~7ed996d | ? | **↑OSX** | 589 | 580 | 5 | 4 | 0 | **빨강** | G1_앱이_도는_동안_살아있는_오브젝트_바닥선이_올라가지_않는다<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 20:24 | `te-purge` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1679 | 1660 | 4 | 15 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다<br>양성대조_심어_놓은_오염_파일을_정리기가_실제로_지운다<br>재발방지_다섯_픽스처는_저장파일을_다시_쓰지_않는다 |
| 09-02 20:28 | `te-purge2` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1680 | 1664 | 1 | 15 | 0 | **빨강** | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 20:29 | `te-play` | play | 전량 | ~7ed996d | ? | **↑OSX** | 589 | 584 | 1 | 4 | 0 | **빨강** | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate |
| 09-02 21:37 | `qa-r5` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1706 | 1687 | 3 | 16 | 0 | **빨강** | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>줄번호_참조를_새로_만들지_않는다 |
| 09-02 22:13 | `qa-r5` | play | 전량 | ~7ed996d | ? | **~OSX** | 589 | 584 | 1 | 4 | 0 | **빨강** | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate |
| 09-02 23:39 | `te-r2` | edit | 전량 | 7ed996d | **140** | **OSX** | 1707 | 1688 | 3 | 16 | 0 | **빨강** | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>줄번호_참조를_새로_만들지_않는다 |
| 09-02 23:40 | `te-r2` | play | 전량 | 7ed996d | **143** | **OSX** | 591 | 587 | 0 | 4 | 0 | 초록 | — |
| 09-03 00:02 | `qa-r6` | edit | 전량 | 7ed996d | **167** | **OSX** | 1754 | 1735 | 1 | 18 | 0 | **빨강** | 줄번호_참조를_새로_만들지_않는다 |
| 09-03 01:59 | `lead-final` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1798 | 1775 | 4 | 19 | 0 | **빨강** | 빌드타깃과_무관하게_소스에서도_통과_누락을_감사한다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>줄번호_참조를_새로_만들지_않는다<br>톱니가_우측_도킹_작업표시줄_뒤로_들어가지_않는다 |
| 09-03 02:06 | `coder-golden` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1798 | 1777 | 2 | 19 | 0 | **빨강** | 빌드타깃과_무관하게_소스에서도_통과_누락을_감사한다<br>톱니가_우측_도킹_작업표시줄_뒤로_들어가지_않는다 |
| 09-03 02:14 | `devplat-edge` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1800 | 1781 | 0 | 19 | 0 | 초록 | — |
| 09-03 02:18 | `devplat-edge2` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1801 | 1780 | 1 | 20 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다 |
| 09-03 02:20 | `devplat-edge3` | edit | 전량 | ~7ed996d | ? | **~OSX** | 1800 | 1781 | 0 | 19 | 0 | 초록 | — |
| 09-03 05:00 | `coder-grabline` | edit | 전량 | ~1eb0e2b | ? | **~OSX** | 1800 | 1781 | 0 | 19 | 0 | 초록 | — |
| 09-03 05:01 | `coder-grabline-poscontrol-EXPECTED-RED` | edit | **미확인** | ~1eb0e2b | ? | **~OSX** | 33 | 32 | 1 | 0 | 0 | **빨강** | 골든과_소스_말뭉치가_양방향으로_일치한다 |
| 09-03 05:10 | `qa-r7` | edit | 전량 | 1eb0e2b | **40** | **OSX** | 1813 | 1789 | 5 | 19 | 0 | **빨강** | B1_Windows_실행부는_자기창_확장스타일_한_종류만_쓴다<br>Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>PlayMode가_베낀_식별자_문자열이_프로덕션과_같다<br>양성_대조_문자열_파서가_값을_읽고_틀린_값을_잡아낸다<br>자기창_스타일_쓰기_API는_해소기_한_파일에만_있다 |
| 09-03 05:13 | `qa-r7` | play | 전량 | 1eb0e2b | **45** | **OSX** | 610 | 603 | 0 | 7 | 0 | 초록 | — |
| 09-03 05:36 | `coder-r8` | edit | 전량 | 1eb0e2b | **71** | **OSX** | 1825 | 1805 | 2 | 18 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>자기창_스타일_쓰기_API는_해소기_한_파일에만_있다 |
| 09-03 05:36 | `coder-r8` | play | 전량 | 1eb0e2b | **72** | **OSX** | 629 | 622 | 0 | 7 | 0 | 초록 | — |
| 09-03 05:59 | `te-r2b` | edit | 전량 | 1eb0e2b | **81** | **OSX** | 1828 | 1810 | 0 | 18 | 0 | 초록 | — |
| 09-03 06:02 | `te-r2c` | edit | 전량 | 1eb0e2b | **79** | **OSX** | 1828 | 1810 | 0 | 18 | 0 | 초록 | — |
| 09-03 06:02 | `devplat-toolwindow` | edit | 전량 | ~1eb0e2b | ? | **↑OSX** | 1828 | 1810 | 0 | 18 | 0 | 초록 | — |
| 09-03 06:10 | `ui-textwidth` | edit | 전량 | 1eb0e2b | **88** | **OSX** | 1828 | 1810 | 0 | 18 | 0 | 초록 | — |
| 09-03 06:11 | `ui-textwidth` | play | 전량 | 1eb0e2b | **89** | **OSX** | 630 | 622 | 0 | 8 | 0 | 초록 | — |
| 09-03 06:35 | `docs/verify/runs/ledgehang-GREEN` | play | 전량 | ~1eb0e2b | ? | **~OSX** | 630 | 622 | 0 | 8 | 0 | 초록 | — |
| 09-03 06:58 | `debugger-r9` | edit | **미확인** | ~1eb0e2b | ? | **↑OSX** | 1853 | 1831 | 5 | 17 | 0 | **빨강** | C층_소유판정은_Unknown을_NotOwned로_붕괴시키지_않는다<br>같은_아이디를_쓰는_두_팩은_뒤쪽이_거부된다<br>세이브_스키마의_필드_이름에_유료권한_토큰이_하나도_없다<br>주석이_지목한_소스_파일이_새로_사라지지_않는다<br>주인_없는_코호트를_가진_아이템은_신고된다 |
| 09-03 07:09 | `ledgehang-GREEN` | edit | 전량 | ~1eb0e2b | ? | **~OSX** | 1869 | 1835 | 17 | 17 | 0 | **빨강** | NegativeControl_같은_버전_파일이면_평소대로_덮어쓴다<br>NegativeControl_직전_세대가_없으면_같은_사고가_전손이_된다<br>★픽스처 SetUp StickMate.Tests.EditMode.SaveConcurrentInstanceTests<br>★픽스처 SetUp StickMate.Tests.EditMode.SavePreviousGenerationTests<br>같은_아이디를_쓰는_두_팩은_뒤쪽이_거부된다<br>교체가_몇_번_거절돼도_재시도가_원자성을_지킨다<br>교체가_아예_안_되는_환경에서는_대피시킨_뒤_덮어쓴다<br>다른_인스턴스의_임시파일을_밟지_않는다<br>덮어쓰기_도중_프로세스가_사라져도_직전_저장으로_되돌아간다<br>두_번째_저장부터_직전_세대가_남고_그것은_바로_앞_내용이다<br>본체가_아예_없으면_세대를_뒤지지_않고_새_캐릭터로_시작한다<br>상주_인스턴스는_발밑에서_신버전으로_바뀐_파일을_덮어쓰지_않는다<br>세이브_스키마의_필드_이름에_유료권한_토큰이_하나도_없다<br>손상되거나_빈_파일은_저장을_막지_않는다<br>양성대조_심어_놓은_오염_파일을_정리기가_실제로_지운다<br>이미_깨진_본체로_온전한_직전_세대를_덮지_않는다<br>임시_파일_경로는_인스턴스마다_다르다<br>첫_저장은_남길_직전_세대가_없다<br>한번_확인된_보유는_조회가_실패해도_회수되지_않는다 |
| 09-03 07:19 | `packcorridor` | play | 전량 | ~1eb0e2b | ? | **↑OSX** | 630 | 622 | 0 | 8 | 0 | 초록 | — |
| 09-03 07:43 | `packcorridor` | edit | 전량 | ~1eb0e2b | ? | **~OSX** | 1872 | 1855 | 0 | 17 | 0 | 초록 | — |
| 09-03 08:09 | `rarity-ribbon` | play | 전량 | 1eb0e2b | **172** | **OSX** | 635 | 625 | 2 | 8 | 0 | **빨강** | DetailWordAndRibbonCellsAgreeOnTheSameRarity<br>T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다 |
| 09-03 08:42 | `ribbonfix` | play | **부분(5건)** | ~1eb0e2b | ? | **~WIN** | 5 | 5 | 0 | 0 | 0 | 초록 | — |
| 09-03 08:52 | `ribbonfix` | edit | **부분(13건)** | ~1eb0e2b | ? | **↑WIN** | 13 | 13 | 0 | 0 | 0 | 초록 | — |
| 09-03 09:03 | `brasscheck` | edit | **부분(31건)** | ~1eb0e2b | ? | **↑WIN** | 31 | 30 | 0 | 1 | 0 | 초록 | — |
| 09-03 09:22 | `full_win` | edit | 전량 | ~1eb0e2b | ? | **↑WIN** | 1881 | 1862 | 2 | 17 | 0 | **빨강** | 관절_끝점이_획_두께_이상으로_안쪽으로_물러나지_않는다<br>위마디와_아래마디의_곡선이_관절에서_정확히_이어진다 |
| 09-03 10:28 | `debugger-limbcurve-fix` | edit | 전량 | 1eb0e2b | **229** | **WIN** | 1908 | 1889 | 1 | 18 | 0 | **빨강** | 상태_테두리가_등급_최대보다_밝다 |
| 09-03 10:54 | `gate` | edit | 전량 | ~1eb0e2b | ? | **~WIN** | 1914 | 1894 | 2 | 18 | 0 | **빨강** | 대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다<br>상태_테두리가_등급_최대보다_밝다 |
| 09-03 11:01 | `borderfix` | edit | **부분(6건)** | ~1eb0e2b | ? | **~WIN** | 6 | 6 | 0 | 0 | 0 | 초록 | — |
| 09-03 11:02 | `final` | edit | 전량 | ~1eb0e2b | ? | **↑WIN** | 1914 | 1896 | 0 | 18 | 0 | 초록 | — |
| 09-03 11:04 | `final` | play | 전량 | ~1eb0e2b | ? | **~WIN** | 638 | 627 | 3 | 8 | 0 | **빨강** | T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다<br>TheHoverLabelNeverCoversAnotherButton<br>기상_중_어떤_정착각에서도_잉크가_화면_아래로_잘리지_않는다 |
| 09-03 11:36 | `archery_prob` | edit | **부분(14건)** | ~1eb0e2b | ? | **~WIN** | 14 | 14 | 0 | 0 | 0 | 초록 | — |
| 09-03 11:37 | `archery_visual` | play | **부분(24건)** | ~1eb0e2b | ? | **~WIN** | 24 | 24 | 0 | 0 | 0 | 초록 | — |
| 09-03 11:37 | `dbg-lagprobe` | play | **부분(1건)** | ~1eb0e2b | ? | **~WIN** | 1 | 1 | 0 | 0 | 0 | 초록 | — |
| 09-03 11:41 | `dbg-getupfix-GREEN` | play | **부분(2건)** | ~1eb0e2b | ? | **~WIN** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 09-03 11:45 | `dbg-getupfix-MUT` | play | **부분(2건)** | ~1eb0e2b | ? | **~WIN** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 09-03 11:59 | `dbg-getupfix-FULL` | play | 전량 | ~1eb0e2b | ? | **↑WIN** | 638 | 628 | 2 | 8 | 0 | **빨강** | T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다<br>좌우반전을_20회_반복해도_모자_채움이_항상_유효하다 |
| 09-03 12:24 | `dbg-flipfill-CONTROL` | play | **부분(5건)** | ~1eb0e2b | ? | **~WIN** | 5 | 4 | 1 | 0 | 0 | **빨강** | 좌우반전을_20회_반복해도_모자_채움이_항상_유효하다 |
| 09-03 12:35 | `dbg-accstale-RED` | play | **부분(1건)** | ~1eb0e2b | ? | **~WIN** | 1 | 0 | 1 | 0 | 0 | **빨강** | T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다 |
| 09-03 12:36 | `dbg-accstale-RED2` | play | **부분(1건)** | ~1eb0e2b | ? | **~WIN** | 1 | 0 | 1 | 0 | 0 | **빨강** | T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다 |
| 09-03 12:37 | `dbg-accstale-GREEN` | play | **부분(1건)** | ~1eb0e2b | ? | **~WIN** | 1 | 1 | 0 | 0 | 0 | 초록 | — |
| 09-03 12:38 | `dbg-accstale-BLAST` | play | **부분(202건)** | ~1eb0e2b | ? | **~WIN** | 202 | 197 | 1 | 4 | 0 | **빨강** | 좌우반전을_20회_반복해도_모자_채움이_항상_유효하다 |
| 09-03 12:44 | `tray` | edit | **부분(16건)** | ~1eb0e2b | ? | **~WIN** | 16 | 16 | 0 | 0 | 0 | 초록 | — |
| 09-03 12:44 | `dbg-accstale-AUDIT` | edit | **부분(485건)** | ~1eb0e2b | ? | **↑WIN** | 485 | 471 | 0 | 14 | 0 | 초록 | — |
| 09-03 13:36 | `Logs/devplat_audio_editmode` | edit | 전량 | ~1eb0e2b | ? | **~WIN** | 1955 | 1934 | 2 | 19 | 0 | **빨강** | T7_플랫폼_계층_어디에도_크로스프로세스_메시지_전송이_없다<br>부채꼴에서_이름표가_어떤_형제_버튼도_덮지_않는다 |
| 09-03 13:38 | `Logs/devplat_audio_editmode2` | edit | 전량 | ~1eb0e2b | ? | **~WIN** | 1955 | 1934 | 2 | 19 | 0 | **빨강** | T7_플랫폼_계층_어디에도_크로스프로세스_메시지_전송이_없다<br>부채꼴에서_이름표가_어떤_형제_버튼도_덮지_않는다 |
| 09-05 05:37 | `qa-r9` | edit | 전량 | 1eb0e2b | **318** | **WIN** | 1982 | 1962 | 1 | 19 | 0 | **빨강** | 부채꼴에서_이름표가_어떤_형제_버튼도_덮지_않는다 |
| 09-05 05:38 | `qa-r9` | play | 전량 | 1eb0e2b | **319** | **WIN** | 638 | 629 | 1 | 8 | 0 | **빨강** | 좌우반전을_20회_반복해도_모자_채움이_항상_유효하다 |
| 09-05 06:20 | `quitchip-latin` | play | **부분(3건)** | ~1eb0e2b | ? | **~WIN** | 3 | 3 | 0 | 0 | 0 | 초록 | — |
| 09-05 09:32 | `qa-r10` | edit | 전량 | c234f1e | **132** | **OSX** | 2016 | 1997 | 3 | 16 | 0 | **빨강** | 여섯_종이_화면에서_서로_구분된다<br>줄번호_참조를_새로_만들지_않는다<br>최단_실제_변_검사를_액세서리_30종으로_확장한다 |
| 09-05 09:33 | `qa-r10` | play | 전량 | c234f1e | **132** | **OSX** | 641 | 633 | 0 | 8 | 0 | 초록 | — |
| 09-05 10:08 | `qa-r10-equipmut-EXPECTED-RED` | edit | **부분(4건)** | ~c234f1e | ? | **~OSX** | 4 | 1 | 3 | 0 | 0 | **빨강** | 되돌리기는_통지를_정확히_한_번_흘린다<br>바뀐_것이_없어도_되돌리기는_통지를_흘린다<br>통지는_모델이_이미_기본_차림이_된_뒤에_도착한다 |
| 09-05 10:08 | `qa-r10-equipmut-RESTORED-GREEN` | edit | **부분(4건)** | ~c234f1e | ? | **~OSX** | 4 | 4 | 0 | 0 | 0 | 초록 | — |
| 09-05 12:35 | `card-r16` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2031 | 1935 | 80 | 16 | 0 | **빨강** | BACK 긴망토<br>BACK 날개<br>BACK 배낭<br>BACK 짧은망토<br>EYES 0번<br>EYES 1번<br>EYES 2번<br>EYES 3번<br>EYES 고글<br>EYES 동그란안경<br>EYES 선글라스<br>EYES 외알안경<br>HEAD 야구모자<br>HEAD 왕관<br>HEAD 중절모<br>HEAD 털모자<br>NECK 나비넥타이<br>NECK 목도리<br>NECK 방울목걸이<br>NECK 줄무늬타이<br>가리개_채움이_눈_자리를_덮는다(3)<br>각_아이템의_선이_선언한_레이어로_나온다<br>경고구간_다이얼_최소_배율에서도_색면이_0인_도형은_없다<br>고글_스트랩이_좌우로_똑같이_뻗는다<br>규칙1C Eyes 3번(외알안경)<br>규칙1C Head 0번(천모자)<br>규칙1C Head 2번(중절모)<br>규칙1C Head 3번(왕관)<br>규칙1C Neck 0번(나비넥타이)<br>규칙1C Neck 1번(줄무늬타이)<br>규칙1C Neck 2번(목도리)<br>규칙1C Neck 3번(방울목걸이)<br>규칙1C Shoulders 0번(짧은망토)<br>규칙1C Shoulders 1번(긴망토)<br>규칙1C Shoulders 2번(날개)<br>규칙1C Shoulders 3번(배낭)<br>날개_두_깃과_등뼈가_한_점에서_만난다<br>날개_두_깃이_규칙_1을_지킨다<br>날개_두_깃이_좌우_한_쌍이다<br>망토_뒤판의_흔들_구간은_밑단이다<br>망토_주름의_끝점도_흔들_구간에_들어_있다(0)<br>망토_주름의_끝점도_흔들_구간에_들어_있다(1)<br>면제_대장은_실제_위반_집합과_정확히_일치한다<br>면제되지_않은_모든_도형이_획_예산을_지킨다<br>모자_띠는_보조색으로_남아있다(2,"FedoraBand")<br>모자_띠는_자기_관_밑변과_정확히_겹친다(2,"FedoraBand","FedoraCrown")<br>몸의_중절모_띠는_관_밑변의_두_끝점_그_자체다<br>방울_폴백에는_추가_없다<br>방울_폴백은_줄_최저점에_매달린_채운_원이다<br>방울은_매끄러운_원으로_인정되는_각도를_유지한다<br>방울은_목줄_최저점에_매달린다<br>방울은_통째로_흔들린다<br>방울은_획_예산을_지킨다<br>방울을_키워도_펜던트와_갈린다<br>배낭_어깨끈의_끝점은_배낭_몸의_꼭짓점_그_자체다<br>배낭_어깨끈이_획_예산을_지킨다<br>아이템_단위_몸_파라미터는_골든과_같고_날개_배낭만_그룹_알파를_갖는다<br>액자_기준_최고점이_실제_최고_아이템과_한_획_이내다<br>왕관은_좌우_대칭이라_반전해도_같은_그림이다<br>인계본_조각이_두_표면에서_골든과_같다<br>자_선택은_오직_Filled_하나로_갈린다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>줄무늬_타이는_월요일에만_느슨해진다<br>지표가_옛_방울을_실제로_잡는다<br>지표가_옛_중절모_띠를_실제로_잡는다<br>천모자_챙의_닫힘변이_획_하나보다_길다<br>최단_실제_변_검사를_액세서리_30종으로_확장한다<br>카드_변형이_있는_아이템은_잉크_윤곽과_워시_채움으로_그려진다<br>펜던트는_원이_아니다<br>폴백_아이콘의_띠도_관_밑변_직선이다<br>폼폼_꺾임이_검사_문턱에서_확실히_떨어져_있다<br>폼폼_꼭대기가_액자_상한에_그대로_머문다<br>폼폼은_유일한_보조색_채움으로_남는다<br>폼폼이_관에_얹혀_있다<br>폼폼이_획_예산을_지킨다<br>한쪽만_가리는_물건만_반대쪽_눈을_보여준다(3)<br>흔들린다고_적힌_아이템은_흔들_점_구간을_선언한다(Neck,1)<br>흔들린다고_적힌_아이템은_흔들_점_구간을_선언한다(Neck,2)<br>흔들린다고_적힌_아이템은_흔들_점_구간을_선언한다(Neck,3) |
| 09-05 15:49 | `card-r17` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2042 | 1950 | 6 | 86 | 0 | **빨강** | 가리개_채움이_눈_자리를_덮는다(1)<br>가리개_채움이_눈_자리를_덮는다(3)<br>면제되지_않은_모든_도형이_획_예산을_지킨다<br>모든_가리개가_채움_실루엣을_갖는다(1)<br>아이템은_한_벌이거나_두_벌이고_두_벌은_표면과_층이_명시된다<br>여섯_종이_화면에서_서로_구분된다 |
| 09-05 16:06 | `card-r19` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2042 | 1949 | 5 | 88 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>가리개_채움이_눈_자리를_덮는다(3)<br>아이템_단위_몸_파라미터는_골든과_같고_날개_배낭만_그룹_알파를_갖는다<br>아이템은_한_벌이거나_두_벌이고_두_벌은_표면과_층이_명시된다<br>여섯_종이_화면에서_서로_구분된다 |
| 09-05 16:09 | `card-r19b` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2042 | 1951 | 1 | 90 | 0 | **빨강** | 여섯_종이_화면에서_서로_구분된다 |
| 09-05 16:14 | `card-r19c` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2042 | 1951 | 1 | 90 | 0 | **빨강** | 여섯_종이_화면에서_서로_구분된다 |
| 09-05 20:32 | `card-r20` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2053 | 1967 | 0 | 86 | 0 | 초록 | — |
| 09-05 20:38 | `esc-gate` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2056 | 1970 | 0 | 86 | 0 | 초록 | — |
| 09-05 20:56 | `n1-dy` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2057 | 1968 | 3 | 86 | 0 | **빨강** | E10 골든 헤더<br>cs 헤더<br>v1_에셋_펜던트와_반다나는_몸과_카드에_같은_조각으로_나온다 |
| 09-05 20:59 | `n1-dy2` | edit | 전량 | ~c234f1e | ? | **~OSX** | 2057 | 1971 | 0 | 86 | 0 | 초록 | — |
| 09-05 21:00 | `qa-r12` | edit | 전량 | c234f1e | **316** | **OSX** | 2059 | 1973 | 0 | 86 | 0 | 초록 | — |
| 09-05 21:02 | `qa-r12-M1-EXPECTED-RED` | edit | 전량 | c234f1e | **316** | **OSX** | 2059 | 1972 | 1 | 86 | 0 | **빨강** | 간접_Ignore_게이트도_명부에_있고_호출부마다_사유를_남긴다 |
| 09-05 21:02 | `qa-r12-M2-EXPECTED-RED` | edit | 전량 | c234f1e | **316** | **OSX** | 2059 | 1972 | 1 | 86 | 0 | **빨강** | 간접_Ignore_게이트도_명부에_있고_호출부마다_사유를_남긴다 |
| 09-05 21:03 | `qa-r12-RESTORED-GREEN` | edit | 전량 | c234f1e | **316** | **OSX** | 2059 | 1973 | 0 | 86 | 0 | 초록 | — |
| 09-05 21:05 | `qa-r12b` | edit | 전량 | c234f1e | **316** | **OSX** | 2059 | 1973 | 0 | 86 | 0 | 초록 | — |
| 09-05 21:32 | `dbg-throwregrab` | play | **부분(2건)** | ~c234f1e | ? | **~OSX** | 2 | 2 | 0 | 0 | 0 | 초록 | — |
| 09-05 22:04 | `dbg-diag` | edit | **부분(17건)** | ~c234f1e | ? | **~OSX** | 17 | 17 | 0 | 0 | 0 | 초록 | — |
| 09-05 22:05 | `dbg-diag` | play | **부분(1건)** | ~c234f1e | ? | **↑OSX** | 1 | 1 | 0 | 0 | 0 | 초록 | — |
| 09-05 22:46 | `te-standbygear` | play | **부분(8건)** | ~c234f1e | ? | **~OSX** | 8 | 7 | 1 | 0 | 0 | **빨강** | 차단막은_펼쳐진_부채꼴_밖으로_번지지_않는다 |
| 09-05 22:48 | `te-neighbors` | play | **부분(14건)** | ~c234f1e | ? | **~OSX** | 14 | 13 | 1 | 0 | 0 | **빨강** | 차단막은_펼쳐진_부채꼴_밖으로_번지지_않는다 |
| 09-05 22:49 | `te-afterorder` | play | **부분(6건)** | ~c234f1e | ? | **~OSX** | 6 | 6 | 0 | 0 | 0 | 초록 | — |
| 09-06 15:35 | `coder-fan-r1` | edit | 전량 | 349048f | **13** | **OSX** | 2499 | 2402 | 4 | 93 | 0 | **빨강** | 대조_고치기_전_규칙은_같은_스윕에서_형제를_덮는다<br>모든_직렬화_필드가_에셋에_구워져_있다<br>에셋_키와_직렬화_필드가_정확히_일대일이다<br>이름표_최근접_보장은_현실_화면과_예약띠에서도_선다 |
| 09-06 15:38 | `coder-fan-r2` | edit | 전량 | 349048f | **14** | **OSX** | 2499 | 2406 | 0 | 93 | 0 | 초록 | — |
| 09-06 15:39 | `coder-fan-play` | play | 전량 | 349048f | **14** | **OSX** | 685 | 646 | 31 | 8 | 0 | **빨강** | EyesAreAbsentUnderEveryGlassesItem<br>FallenPoseKeepsTheWholeHeadAndEveryStrokeInsideTheFrame<br>NegativeControl_M6_되올리기_두_경로가_각각_채움_경계선을_2pt로_되돌리지_않는다<br>NegativeControl_공중에서는_실제로_크게_젖혀져_있다<br>NegativeControl_액세서리_상쇄를_끄면_이중_스케일이_실제로_생긴다<br>SwappingItemWithinTheSameCategoryActuallyRedrawsTheShape<br>T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다<br>TwoClicksBuyAndTheBalanceDropsByExactlyThePrice<br>가만히_서_있으면_망토는_정적이다<br>가출_은신_프레임에도_액세서리_펫_FX가_함께_사라진다<br>기상_중_어떤_정착각에서도_잉크가_화면_아래로_잘리지_않는다<br>낙하_속도가_0에_가까우면_펄럭임도_거의_없다<br>낙하_중_채움_면이_윤곽선을_따라온다<br>낙하하면_망토_밑단이_정적_상태와_다른_자리로_간다<br>던져서_공중회전하는_동안_모자_가시성을_기록한다<br>던져져_회전하는_동안에도_펄럭이고_기류가_몸을_따라_돈다<br>말풍선_슬라이더가_배포_에셋의_직렬화_필드를_건드리지_않는다<br>말풍선을_끄면_아래_세_행이_함께_비활성이_된다<br>망토_채움이_흔들리는_윤곽선을_따라가는가<br>모자_채움_면이_실제로_생기고_머리_링_윗호를_덮는다<br>배율을_바꿔도_획이_화면상_최소_두께_아래로_내려가지_않는다<br>상체가_기울어도_모자는_머리에_그대로_붙어_있다<br>양성대조_낱선은_표식도_채움도_없다<br>왕관은_채워지되_얹는_물건으로_남는다<br>전체화면_감지_프레임에_독립루트_기분표시도_함께_사라졌다_돌아온다<br>전체화면_감지_프레임에_액세서리_펫_FX가_한_개도_남지_않는다<br>좌우반전을_20회_반복해도_모자_채움이_항상_유효하다<br>착지_뒤_천은_유한_시간에_정확히_원본으로_돌아온다<br>착지_프레임에_망토가_순간이동하지_않는다<br>채움_재질이_양면인지_와인딩_반전_렌더로_실측한다<br>컨테이너_회전을_지우면_같은_지표가_실제로_깨진다 |
| 09-06 16:16 | `coder-fan-final` | edit | 전량 | 349048f | **19** | **OSX** | 2499 | 2406 | 0 | 93 | 0 | 초록 | — |
| 09-06 18:01 | `r28-consolidated` | edit | 전량 | 349048f | **119** | **OSX** | 2522 | 2436 | 0 | 86 | 0 | 초록 | — |
| 09-06 18:02 | `r28-consolidated` | play | 전량 | 349048f | **119** | **OSX** | 695 | 665 | 3 | 27 | 0 | **빨강** | StickmanFallsSettlesAndWanders<br>세션을_끄면_관망_자세가_풀리고_평소_중립으로_돌아온다<br>착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다 |
| 09-06 18:37 | `dbg-r29` | play | 전량 | 349048f | **119** | **OSX** | 695 | 666 | 2 | 27 | 0 | **빨강** | 몸보다_튀어나온_액세서리가_보고_반폭에_포함된다<br>착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다 |
| 09-06 19:50 | `te-flaky` | play | 전량 | 349048f | **125** | **OSX** | 697 | 670 | 0 | 27 | 0 | 초록 | — |
| 09-06 20:22 | `r30-final` | edit | 전량 | 349048f | **125** | **OSX** | 2522 | 2436 | 0 | 86 | 0 | 초록 | — |
| 09-06 20:23 | `r30-final` | play | 전량 | 349048f | **125** | **OSX** | 697 | 670 | 0 | 27 | 0 | 초록 | — |
| 09-06 22:44 | `r31-final` | edit | 전량 | a1294a9 | **96** | **OSX** | 2539 | 2454 | 0 | 85 | 0 | 초록 | — |
| 09-06 22:45 | `r31-final` | play | 전량 | a1294a9 | **96** | **OSX** | 694 | 667 | 0 | 27 | 0 | 초록 | — |
| 09-07 09:50 | `testeng-awaywander` | edit | 전량 | ff1a506 | **100** | **OSX** | 2643 | 2554 | 3 | 86 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>주석이_지목한_소스_파일이_새로_사라지지_않는다<br>줄번호_참조를_새로_만들지_않는다 |
| 09-07 09:53 | `testeng-awaywander-r2` | edit | 전량 | ff1a506 | **101** | **OSX** | 2645 | 2556 | 3 | 86 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>주석이_지목한_소스_파일이_새로_사라지지_않는다<br>줄번호_참조를_새로_만들지_않는다 |
| 09-07 10:10 | `testeng-final` | edit | 전량 | ff1a506 | **104** | **OSX** | 2648 | 2561 | 0 | 87 | 0 | 초록 | — |
| 09-07 12:19 | `devplat-84e9b23` | edit | **부분(118건)** | ~84e9b23 | ? | **↑OSX** | 118 | 118 | 0 | 0 | 0 | 초록 | — |
| 09-07 12:42 | `petplaneorbit` | play | **부분(0건)** | ~84e9b23 | ? | **~OSX** | 0 | 0 | 0 | 0 | 0 | 초록 | — |
| 09-07 12:43 | `bodylean` | play | **부분(6건)** | ~84e9b23 | ? | **↑OSX** | 6 | 6 | 0 | 0 | 0 | 초록 | — |
| 09-07 12:44 | `shapebudget` | edit | **부분(39건)** | ~7b26078 | ? | **↑OSX** | 39 | 38 | 0 | 1 | 0 | 초록 | — |
| 09-07 12:45 | `petfallsync` | play | **부분(6건)** | ~7b26078 | ? | **↑OSX** | 6 | 6 | 0 | 0 | 0 | 초록 | — |
| 09-07 12:52 | `devplat-1f8a3a9` | play | **부분(10건)** | ~1f8a3a9 | ? | **↑OSX** | 10 | 10 | 0 | 0 | 0 | 초록 | — |
| 09-07 13:58 | `devplat-91f14c2` | edit | **부분(41건)** | ~91f14c2 | ? | **↑OSX** | 41 | 40 | 0 | 1 | 0 | 초록 | — |
| 09-07 13:58 | `devplat-91f14c2` | play | **부분(25건)** | ~91f14c2 | ? | **↑OSX** | 25 | 24 | 0 | 1 | 0 | 초록 | — |
| 09-07 14:45 | `devplat-c1b6890` | play | **부분(6건)** | ~c1b6890 | ? | **↑OSX** | 6 | 6 | 0 | 0 | 0 | 초록 | — |
| 09-07 15:02 | `devplat-ea91d17` | play | **부분(8건)** | ~ea91d17 | ? | **↑OSX** | 8 | 8 | 0 | 0 | 0 | 초록 | — |
| 09-07 15:51 | `devplat-a85914a` | edit | **부분(17건)** | ~a85914a | ? | **↑OSX** | 17 | 17 | 0 | 0 | 0 | 초록 | — |
| 09-07 15:51 | `devplat-a85914a` | play | **부분(19건)** | ~a85914a | ? | **↑OSX** | 19 | 18 | 1 | 0 | 0 | **빨강** | 실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다 |
| 09-07 16:05 | `devplat-c5166eb` | edit | **부분(17건)** | ~c5166eb | ? | **↑OSX** | 17 | 17 | 0 | 0 | 0 | 초록 | — |
| 09-07 16:05 | `devplat-c5166eb` | play | **부분(19건)** | ~c5166eb | ? | **↑OSX** | 19 | 18 | 1 | 0 | 0 | **빨강** | 실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다 |
| 09-07 18:15 | `Logs/coder_focusxp_editmode_20260907_181531` | edit | 전량 | ~a79a318 | ? | **~OSX** | 2684 | 2595 | 2 | 87 | 0 | **빨강** | 취소는_분을_먼저_내린_뒤_요율을_곱한다_149_9초<br>취소는_분을_먼저_내린_뒤_요율을_곱한다_90_5초 |
| 09-07 18:17 | `Logs/coder_focusxp_editmode_20260907_181754` | edit | 전량 | ~a79a318 | ? | **~OSX** | 2684 | 2597 | 0 | 87 | 0 | 초록 | — |
| 09-07 18:30 | `Logs/full_editmode_regression` | edit | 전량 | ~b0df7b4 | ? | **↑OSX** | 2688 | 2601 | 0 | 87 | 0 | 초록 | — |
| 09-07 22:20 | `ropeclimb-r3` | edit | 전량 | 3738ad7 | **8** | **OSX** | 2717 | 2627 | 3 | 87 | 0 | **빨강** | 등재되지_않은_폴백_리터럴_드리프트가_없다<br>줄번호_참조를_새로_만들지_않는다<br>테스트_강제값을_공개로_여는_스위치는_늘어나지_않는다 |
| 09-07 22:21 | `ropeclimb-r3` | play | 전량 | 3738ad7 | **8** | **OSX** | 743 | 713 | 3 | 27 | 0 | **빨강** | stepUpChance가_0이어도_로프등반은_독립적으로_평가된다<br>넓은_탐색_전용_로프벽도_안전망이_상시존재해도_발동한다<br>정지한_표면에서는_재스냅이_돌지_않는다 |
| 09-07 22:53 | `ropeclimb-r4` | edit | 전량 | 7443920 | **8** | **OSX** | 2717 | 2629 | 1 | 87 | 0 | **빨강** | 테스트_강제값을_공개로_여는_스위치는_늘어나지_않는다 |
| 09-07 23:10 | `sec-audit-r5` | edit | 전량 | 7443920 | **29** | **OSX** | 2729 | 2642 | 0 | 87 | 0 | 초록 | — |
| 09-08 01:01 | `part2-final` | edit | 전량 | 7443920 | **139** | **OSX** | 2828 | 2734 | 7 | 87 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>감사_상한_초과를_잡는다<br>감사_코호트_안_등급_혼재를_잡는다<br>감사_팩_요구레벨이_1이_아니면_잡는다<br>감사가_올바른_팩에_침묵하고_망가진_팩에는_문다<br>인계본_열여섯종은_에셋_스트림으로_적어도_비트까지_같다<br>출하_아이템은_아직_아무도_에셋_조형으로_넘어가지_않았다 |
| 09-08 01:02 | `part2-final` | play | 전량 | 7443920 | **139** | **OSX** | 759 | 729 | 3 | 27 | 0 | **빨강** | StruggleDoesNotBreakCursorStickiness<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다 |
| 09-08 16:49 | `packcyber-base` | edit | 전량 | 50f1c6e | **5** | **WIN** | 2885 | 2796 | 1 | 88 | 0 | **빨강** | 주석이_확장자_없이_지목한_테스트가_새로_사라지지_않는다 |
| 09-08 16:56 | `packcyber-red` | edit | 전량 | 50f1c6e | **21** | **WIN** | 2900 | 2779 | 33 | 88 | 0 | **빨강** | HEAD 6종<br>I15_각_슬롯의_여섯_종이_나머지_세_스탯을_정확히_두_번씩_가리킨다<br>게이트_표가_HEAD_카탈로그_전부를_덮는다<br>골든이_적은_코호트와_등급이_에셋이_말하는_값과_같다<br>교정_부스탯_방향이_네_스탯에_고르게_배분됐다<br>그릴_수_있는_카테고리는_아이템_전부가_도형을_갖는다<br>기본_42종은_선언_키를_아예_적지_않는다<br>기본_카탈로그만으로_세트가_실제로_완성되고_대부분의_조합은_완성되지_않는다<br>넓힌_자리는_아직_옛_경로로_흐르고_목만_에셋으로_흐른다<br>대조가_공허해지는_팩_모양이_실재한다_사이사이는_대조로_쓸_수_없다<br>등급이_슬롯_안_요구레벨_순위에서_나온다<br>면제가_없는_아이템은_전부_린트_목록에_들어와_있다<br>목록이_카탈로그와_같은_수다<br>보조색_색면_게이트가_HEAD_전_종을_덮는다<br>상품은_장비_기본코호트만이고_행동과_DLC팩은_실리지_않는다<br>아이템_에셋은_카테고리당_같은_개수이고_자리가_겹치지도_비지도_않는다<br>요구_레벨은_카테고리_안에서_점점_높아지고_전체적으로_퍼져_있다<br>요구레벨이_높을수록_등급이_내려가지_않는다<br>인계본_열여섯_종은_넘침_보정이_항등이다<br>인계본에_없는_열네_종은_v1_규칙_그대로다<br>장비_항목은_7카테고리_고정개수이고_EquipmentModel과_같은_사실을_말한다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>지금은_코호트가_하나다_그래서_코호트가_곧_슬롯이다<br>출하_42종은_애셋에_테마도_부스탯도_적지_않았다<br>출하_모자_여섯의_hidesHair는_렌더러가_실제로_하는_말과_같다<br>출하_아이템은_아직_아무도_에셋_조형으로_넘어가지_않았다<br>카테고리마다_첫_아이템은_처음부터_보유이고_요구_레벨은_오름차순이다<br>커버리지가_2026_09_01_수준_아래로_내려가지_않는다<br>커버선_면제는_왕관_하나뿐이고_그_개수가_고정돼_있다<br>코호트를_안_적은_에셋의_기본값이_기본_코호트와_같다_42종의_유일한_근거<br>테마는_등급이나_자리번호에서_파생되지_않는다<br>테마는_스탯_24종에_전부_배정됐고_외형_18종은_무소속이다<br>팩_12색이_카탈로그_전_색과_충분히_떨어져_있다 |
| 09-08 17:02 | `packcyber-probe` | edit | 전량 | 50f1c6e | **25** | **⇄OSX** | 2903 | 2783 | 32 | 88 | 0 | **빨강** | HEAD 6종<br>I15_각_슬롯의_여섯_종이_나머지_세_스탯을_정확히_두_번씩_가리킨다<br>게이트_표가_HEAD_카탈로그_전부를_덮는다<br>골든이_적은_코호트와_등급이_에셋이_말하는_값과_같다<br>교정_부스탯_방향이_네_스탯에_고르게_배분됐다<br>그릴_수_있는_카테고리는_아이템_전부가_도형을_갖는다<br>기본_42종은_선언_키를_아예_적지_않는다<br>기본_카탈로그만으로_세트가_실제로_완성되고_대부분의_조합은_완성되지_않는다<br>넓힌_자리는_아직_옛_경로로_흐르고_목만_에셋으로_흐른다<br>대조가_공허해지는_팩_모양이_실재한다_사이사이는_대조로_쓸_수_없다<br>등급이_슬롯_안_요구레벨_순위에서_나온다<br>면제가_없는_아이템은_전부_린트_목록에_들어와_있다<br>목록이_카탈로그와_같은_수다<br>보조색_색면_게이트가_HEAD_전_종을_덮는다<br>상품은_장비_기본코호트만이고_행동과_DLC팩은_실리지_않는다<br>요구_레벨은_카테고리_안에서_점점_높아지고_전체적으로_퍼져_있다<br>요구레벨이_높을수록_등급이_내려가지_않는다<br>인계본_열여섯_종은_넘침_보정이_항등이다<br>인계본에_없는_열네_종은_v1_규칙_그대로다<br>장비_항목은_7카테고리_고정개수이고_EquipmentModel과_같은_사실을_말한다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>지금은_코호트가_하나다_그래서_코호트가_곧_슬롯이다<br>출하_42종은_애셋에_테마도_부스탯도_적지_않았다<br>출하_모자_여섯의_hidesHair는_렌더러가_실제로_하는_말과_같다<br>출하_아이템은_아직_아무도_에셋_조형으로_넘어가지_않았다<br>카테고리마다_첫_아이템은_처음부터_보유이고_요구_레벨은_오름차순이다<br>커버리지가_2026_09_01_수준_아래로_내려가지_않는다<br>커버선_면제는_왕관_하나뿐이고_그_개수가_고정돼_있다<br>코호트를_안_적은_에셋의_기본값이_기본_코호트와_같다_42종의_유일한_근거<br>테마는_등급이나_자리번호에서_파생되지_않는다<br>테마는_스탯_24종에_전부_배정됐고_외형_18종은_무소속이다<br>팩_12색이_카탈로그_전_색과_충분히_떨어져_있다 |
| 09-08 17:05 | `packcyber-mutation` | edit | 전량 | 50f1c6e | **16** | **OSX** | 2900 | 2780 | 32 | 88 | 0 | **빨강** | DS_G7은_6테마_중_정확히_한_개만_만족하고_그것이_전설_세트다<br>E1_각_테마는_스탯_4슬롯에_정확히_1종씩이다<br>E2_1일차_무료_4종과_E3_전설_4종이_각각_같은_테마다<br>E3_전설_세트가_혼합_최고를_실제로_이긴다<br>I14_각_스탯이_받는_부스탯_여섯_개의_등급_구성이_균등하다<br>I15_각_슬롯의_여섯_종이_나머지_세_스탯을_정확히_두_번씩_가리킨다<br>I2_기본_카탈로그만으로_네_스탯_각각이_고급에_도달할_수_있다<br>I3_동시_고급은_최대_둘이고_둘은_실제로_달성_가능하다<br>감사_출하_42종에_결함이_0건이다<br>골든이_적은_코호트와_등급이_에셋이_말하는_값과_같다<br>교정_1일차_네_종의_스탯이_설계_검산표와_같다<br>교정_부스탯_방향이_네_스탯에_고르게_배분됐다<br>교정_스탯별_최대치가_설계_검산표와_같다<br>기본_카탈로그만으로_세트가_실제로_완성되고_대부분의_조합은_완성되지_않는다<br>기본_코호트_아이템은_카테고리당_같은_개수이고_자리가_겹치지도_비지도_않는다<br>기본코호트_코스튬은_허용목록_밖_테마를_전부_거부한다<br>등급이_슬롯_안_요구레벨_순위에서_나온다<br>상품은_장비_기본코호트만이고_행동과_DLC팩은_실리지_않는다<br>슬롯마다_등급_분포가_2_2_1_1이다<br>양성_대조_사다리가_어긋나면_본안_비교가_잡는다<br>에셋_코호트가_다르면_등급이_갈리고_같으면_안_갈린다<br>왕관만_머리카락을_남긴다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>지금은_코호트가_하나다_그래서_코호트가_곧_슬롯이다<br>첫날_무료_4종만으로_세트가_완성되고_보너스가_붙는다<br>코호트를_안_적은_에셋의_기본값이_기본_코호트와_같다_42종의_유일한_근거<br>테마_배정이_인계_표_안_B와_한_칸도_다르지_않다<br>테마는_등급이나_자리번호에서_파생되지_않는다<br>테마는_스탯_24종에_전부_배정됐고_외형_18종은_무소속이다<br>팩_코스튬_프롭이_설계_단계별_선수와_같다<br>팩이_붙어도_기본_아이템_등급이_안_움직인다_아래_깔기<br>팩이_붙어도_기본_아이템_등급이_안_움직인다_위로_쌓기 |
| 09-08 17:06 | `packcyber-green` | edit | 전량 | 50f1c6e | **15** | **OSX** | 2900 | 2812 | 0 | 88 | 0 | 초록 | — |
| 09-08 17:47 | `packcyber-land-green` | edit | **미확인** | ~35424db | ? | **↑OSX** | 2909 | 2819 | 0 | 90 | 0 | 초록 | — |
| 09-08 18:24 | `packmine-arcane-green` | edit | **미확인** | ~23ea97b | ? | **~WIN** | 2909 | 2819 | 0 | 90 | 0 | 초록 | — |
| 09-09 11:07 | `Logs/coder_wornbitmap_edit_all` | edit | 전량 | ~a7e8760 | ? | **~WIN** | 2954 | 2863 | 1 | 90 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다 |
| 09-09 11:10 | `Logs/coder_wornbitmap_edit_all2` | edit | 전량 | ~a7e8760 | ? | **~WIN** | 2954 | 2864 | 0 | 90 | 0 | 초록 | — |
| 09-14 09:54 | `coder-freeze` | edit | 전량 | ccfaef9 | **45** | **OSX** | 3055 | 2965 | 0 | 90 | 0 | 초록 | — |
| 09-14 09:56 | `coder-freeze-related` | play | **부분(62건)** | ~ccfaef9 | ? | **↑OSX** | 62 | 62 | 0 | 0 | 0 | 초록 | — |
| 09-14 10:09 | `docs/verify/runs/vc2-edit` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2965 | 0 | 90 | 0 | 초록 | — |
| 09-14 10:20 | `docs/verify/runs/vc2-mut-X1` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2964 | 1 | 90 | 0 | **빨강** | 금지된_API_호출_패턴이_소스코드_어디에도_없다 |
| 09-14 10:21 | `docs/verify/runs/vc2-mut-X2` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2964 | 1 | 90 | 0 | **빨강** | 무장_전에는_어떤_신호도_무시한다 |
| 09-14 10:22 | `docs/verify/runs/vc2-mut-X2c` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2965 | 0 | 90 | 0 | 초록 | — |
| 09-14 10:23 | `docs/verify/runs/vc2-mut-X4` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2964 | 1 | 90 | 0 | **빨강** | 입구_네_곳은_타이머와_전이보다_먼저_동결을_본다 |
| 09-14 10:24 | `docs/verify/runs/vc2-mut-X5` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2964 | 1 | 90 | 0 | **빨강** | 유예_중에는_새_연출을_잡지_못하고_이미_쥔_주인의_재진입은_된다 |
| 09-14 10:24 | `docs/verify/runs/vc2-mut-X6` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2962 | 3 | 90 | 0 | **빨강** | 사건_밖의_적합_기록은_디스크에_쓰지_않는다<br>사건을_여는_기록은_변화감지_정지_유예시작_셋이다<br>사건이_열리면_맥락이_먼저_내려가고_창_안의_적합은_즉시_쓴다 |
| 09-14 10:25 | `docs/verify/runs/vc2-mut-X7` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2964 | 1 | 90 | 0 | **빨강** | 트레이_프로시저가_세션_종료를_동기로_처리하고_옵트아웃에도_수신자가_있다 |
| 09-14 10:27 | `docs/verify/runs/vc2-edit-post` | edit | 전량 | ~ccfaef9 | ? | **~OSX** | 3055 | 2965 | 0 | 90 | 0 | 초록 | — |
| 09-14 18:04 | `Logs/coder-onbstore/edit-full` | edit | 전량 | ~7900ad0 | ? | **↑OSX** | 3141 | 3050 | 0 | 91 | 0 | 초록 | — |
| 09-14 18:10 | `Logs/coder-onbstore/play-full` | play | 전량 | ~7900ad0 | ? | **~OSX** | 783 | 750 | 5 | 26 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-14 20:00 | `Logs/coder-onbstore/s2-editfull` | edit | 전량 | ~7900ad0 | ? | **~OSX** | 3146 | 3055 | 0 | 91 | 0 | 초록 | — |
| 09-14 20:09 | `Logs/coder-onbstore/s4-playfull` | play | 전량 | ~7900ad0 | ? | **~OSX** | 783 | 751 | 4 | 26 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-14 21:19 | `Logs/coder-onbstore/s5-editfull` | edit | 전량 | ~34bc4f0 | ? | **~OSX** | 3146 | 3055 | 0 | 91 | 0 | 초록 | — |
| 09-14 21:30 | `Logs/coderui-e12/E-full` | edit | 전량 | ~6173b6e | ? | **~OSX** | 3152 | 3061 | 0 | 91 | 0 | 초록 | — |
| 09-14 21:55 | `vc-e12` | edit | 전량 | 6173b6e | **46** | **OSX** | 3152 | 3061 | 0 | 91 | 0 | 초록 | — |
| 09-14 21:59 | `vc-e12` | play | 전량 | 6173b6e | **46** | **OSX** | 787 | 756 | 4 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-14 23:56 | `Logs/coderui-e4/E-full` | edit | 전량 | ~eb4670d | ? | **~OSX** | 3158 | 3067 | 0 | 91 | 0 | 초록 | — |
| 09-15 00:23 | `Logs/vc-e4/E-full` | edit | 전량 | ~eb4670d | ? | **~OSX** | 3158 | 3067 | 0 | 91 | 0 | 초록 | — |
| 09-15 00:24 | `Logs/vc-e4/P-full` | play | 전량 | ~eb4670d | ? | **↑OSX** | 802 | 769 | 6 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>몰입기_동안_절감등급_Still에_도달한다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-15 02:38 | `Logs/vc-5d/E` | edit | 전량 | ~e4b9931 | ? | **↑OSX** | 3165 | 3073 | 0 | 92 | 0 | 초록 | — |
| 09-15 02:39 | `Logs/vc-5d/P` | play | 전량 | ~e4b9931 | ? | **↑OSX** | 802 | 770 | 5 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>StruggleDoesNotBreakCursorStickiness<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-15 04:00 | `Logs/coderui-n20/fix_edit` | edit | 전량 | ~3751a10 | ? | **~OSX** | 3168 | 3076 | 0 | 92 | 0 | 초록 | — |
| 09-15 04:03 | `Logs/coderui-n20/final_edit` | edit | 전량 | ~3751a10 | ? | **~OSX** | 3168 | 3076 | 0 | 92 | 0 | 초록 | — |
| 09-15 04:04 | `Logs/coderui-n20/final_play` | play | 전량 | ~3751a10 | ? | **↑OSX** | 808 | 776 | 5 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-15 05:06 | `Logs/vc-n20/full_edit` | edit | 전량 | ~761f5cb | ? | **~OSX** | 3168 | 3076 | 0 | 92 | 0 | 초록 | — |
| 09-15 05:07 | `Logs/vc-n20/full_play` | play | 전량 | ~761f5cb | ? | **↑OSX** | 808 | 775 | 6 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>몰입기_동안_절감등급_Still에_도달한다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-15 08:13 | `Logs/coderui-a1/full_edit` | edit | 전량 | ~ad49497 | ? | **~OSX** | 3168 | 3076 | 0 | 92 | 0 | 초록 | — |
| 09-15 08:14 | `Logs/coderui-a1/full_play` | play | 전량 | ~ad49497 | ? | **↑OSX** | 812 | 777 | 8 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>FullCycleFiresExactlyThreeArrowsAndFullyCleansUp<br>NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately<br>StruggleDoesNotBreakCursorStickiness<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-15 08:56 | `Logs/coderui-a1/full2_edit` | edit | 전량 | ~ad49497 | ? | **↑OSX** | 3168 | 3076 | 0 | 92 | 0 | 초록 | — |
| 09-15 08:57 | `Logs/coderui-a1/full2_play` | play | 전량 | ~ad49497 | ? | **↑OSX** | 812 | 778 | 7 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>FullCycleFiresExactlyThreeArrowsAndFullyCleansUp<br>NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately<br>ThrownCharacterTumblesThenLandsInCrouchWithoutRagdoll<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-15 13:15 | `Logs/te-flaky/full_edit` | edit | 전량 | ~76504c2 | ? | **↑OSX** | 3168 | 3075 | 1 | 92 | 0 | **빨강** | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다 |
| 09-15 13:20 | `Logs/te-flaky/full_edit2` | edit | 전량 | ~76504c2 | ? | **~OSX** | 3168 | 3076 | 0 | 92 | 0 | 초록 | — |
| 09-15 13:22 | `Logs/te-flaky/full_play` | play | 전량 | ~76504c2 | ? | **↑OSX** | 813 | 782 | 4 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-26 15:49 | `Logs/coderui-7b/full-play` | play | 전량 | ~fa25570 | ? | **↑OSX** | 825 | 791 | 7 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>규칙3_누를_수_있는_칸이_있으면_시킬_수_있다고_말한다<br>규칙5_전부_회색인데_캐릭터가_한가하면_시킬_게_없다고_말한다<br>규칙5_크랙_금이_남은_구간은_락만_남아도_시킬_게_없다고_말한다<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-26 16:32 | `Logs/coderui-7b/full-edit` | edit | 전량 | ~fa25570 | ? | **~OSX** | 3197 | 3104 | 0 | 93 | 0 | 초록 | — |
| 09-26 16:59 | `Logs/devplat-fc1/full_edit` | edit | 전량 | ~fa25570 | ? | **↑OSX** | 3203 | 3110 | 0 | 93 | 0 | 초록 | — |
| 09-26 19:39 | `Logs/devplat-fc1/full_play` | play | 전량 | ~fa25570 | ? | **↑OSX** | 825 | 789 | 9 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately<br>규칙3_누를_수_있는_칸이_있으면_시킬_수_있다고_말한다<br>규칙5_전부_회색인데_캐릭터가_한가하면_시킬_게_없다고_말한다<br>규칙5_크랙_금이_남은_구간은_락만_남아도_시킬_게_없다고_말한다<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-26 20:41 | `Logs/coder-hdr3/edit1` | edit | 전량 | ~fa25570 | ? | **↑OSX** | 3206 | 3113 | 0 | 93 | 0 | 초록 | — |
| 09-26 20:42 | `Logs/coder-hdr3/play1` | play | 전량 | ~fa25570 | ? | **↑OSX** | 826 | 794 | 5 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-26 21:20 | `Logs/coder-hdr3/play2` | play | 전량 | ~fa25570 | ? | **↑OSX** | 826 | 793 | 6 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>FX_나뭇잎이_기울어진_머리_위에서_떨어진다<br>R7a_층2_등급없음에서_시작한_낙서는_등급1이_주입되면_벽시계_예산_안에_취소된다<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |
| 09-26 21:58 | `Logs/coder-hdr3/play3` | play | 전량 | ~fa25570 | ? | **↑OSX** | 826 | 794 | 5 | 25 | **2** | **빨강** | ClosingSettingsReopensTheInfoWindowItReplaced<br>EyesAreAbsentUnderEveryGlassesItem<br>NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately<br>몰입기_도중_취소해도_프롭이_화면에_남지_않는다<br>왕관은_채워지되_얹는_물건으로_남는다 |

## 지금 빨간 것 — 그리고 **언제부터**인가

「현재」 = 그 모드의 **가장 최근 `전량` 실행**. 그보다 새 `부분`·`미확인` 실행은 「현재」로 치지 않고 개수만 적는다.

### edit — 현재 `Logs/coder-hdr3/edit1` (09-26 20:41, 전량 3206건, 타깃 ↑OSX)

범위 근거: 로그 명령줄에 필터 인자 없음

빨강 없음 (R1 초록).

### play — 현재 `Logs/coder-hdr3/play3` (09-26 21:58, 전량 826건, 타깃 ↑OSX)

범위 근거: 로그 명령줄에 필터 인자 없음

**판정 불가(Inconclusive) 2건** — `failed=`에 안 들어가고 옛 대장·옛 `compare`에는 안 보였다: `ClickingACalendarCellPicksTheDayInsteadOfDraggingTheWindow` · `ClickingOutsideSettingsNeitherClosesItNorReturnsTheInfoWindow`

| 실패 | 마지막으로 **실제로 초록**(Passed)이던 실행 | 그 뒤 건너뜀·판정 불가(초록 아님) | 처음 빨개진 실행 | 연속 빨강 |
|---|---|---|---|---:|
| ClosingSettingsReopensTheInfoWindowItReplaced | `part2-final` 09-08 01:02 | — | `Logs/coder-onbstore/play-full` 09-14 18:10 | 15 |
| EyesAreAbsentUnderEveryGlassesItem | `part2-final` 09-08 01:02 | — | `Logs/coder-onbstore/play-full` 09-14 18:10 | 15 |
| NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately | `Logs/coder-hdr3/play2` 09-26 21:20 | — | `Logs/coder-hdr3/play3` 09-26 21:58 | 1 |
| 몰입기_도중_취소해도_프롭이_화면에_남지_않는다 | **한 번도 없다** | — | `part2-final` 09-08 01:02 | 16 |
| 왕관은_채워지되_얹는_물건으로_남는다 | `qa-r10` 09-05 09:33 | 7건 (최근 `part2-final` 09-08 01:02) | `coder-fan-play` 09-06 15:39 | 16 |


<!-- rows=496 -->
