# 회귀 베이스라인 대장 — 실행당 한 줄

자동 생성: `python3 docs/verify/baseline.py` · 최종 2026-09-03 00:04:49
**손으로 고치지 마라.** 다음 실행이 통째로 덮는다.

## 읽는 법 — 표시가 붙은 값은 잰 값이 아니다

| 표시 | 뜻 |
|---|---|
| (없음) | **실행 시각에 잰 값**(`regress.sh`가 남긴 `.meta`). 이것만이 사실이다 |
| `~` | 사후 추론 — 타깃은 로그의 Bee dag 해시, HEAD는 reflog 시각 대조 |
| `↑` | **직전 실행에서 물려받음** — 그 실행은 재컴파일을 안 해 자기 타깃을 남기지 않았다 |
| `?` | **미상.** 빈 칸으로 두지 않는다 — 빈 칸은 읽는 사람이 마음대로 채운다 |

dag→타깃 매핑 4건: `1900b0aE.dag`=WIN, `1900b0aP.dag`=WIN, `200b0aE.dag`=OSX, `200b0aP.dag`=OSX

## 실행 대장

| 시각 | 라벨 | 모드 | HEAD | 활성 타깃 | total | 통과 | 실패 | 건너뜀 | 실패 목록 |
|---|---|---|---|---|---:|---:|---:|---:|---|
| 09-02 11:40 | `BASELINE-20260902-1140` | edit | ~890fb1f | **?미상** | 1405 | 1393 | 1 | 11 | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다 |
| 09-02 12:01 | `BASELINE-20260902-1201` | play | ~43c69c9 | **?미상** | 563 | 556 | 4 | 3 | FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 13:21 | `qa-baseline` | edit | ~aaac7b2 | **?미상** | 1442 | 1429 | 1 | 12 | 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다 |
| 09-02 13:43 | `qa-baseline` | play | ~eca8c58 | **?미상** | 563 | 553 | 6 | 4 | FeetVisuallyTouchScreenBottomAndAreNeverClipped<br>StanceFootStaysPlantedWhileBodyMovesForward<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>안_걸치면_신규_4종_미리보기가_하나도_없다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 13:53 | `qa-after-fix` | edit | ~eca8c58 | **~WIN** | 1460 | 1446 | 3 | 11 | 상호작용_표면_명부가_빠짐없이_배선돼_있다<br>정보창_홀드는_열려있음이_아니라_조작중일때만_걸린다<br>최단_실제_변_검사를_액세서리_30종으로_확장한다 |
| 09-02 15:22 | `dbg-fix` | edit | ~eca8c58 | **~WIN** | 1517 | 1505 | 0 | 12 | — |
| 09-02 15:44 | `dbg-fix` | play | ~eca8c58 | **↑WIN** | 570 | 561 | 5 | 4 | G1_앱이_도는_동안_살아있는_오브젝트_바닥선이_올라가지_않는다<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 16:04 | `loc-gate` | edit | ~eca8c58 | **~WIN** | 1602 | 1586 | 4 | 12 | 목_형상은_데이터화_전후로_비트까지_같다<br>양성대조_분기를_지우면_한국어는_그대로이고_영어만_달라진다<br>한국어_가독예산이_골든과_비트_단위로_같다<br>한국어_소비자_경로가_골든에서_파생된_값과_같다 |
| 09-02 16:08 | `loc-gate-2` | edit | ~eca8c58 | **~WIN** | 1602 | 1589 | 1 | 12 | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:08 | `b2-neck` | edit | ~eca8c58 | **~WIN** | 1602 | 1589 | 1 | 12 | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:11 | `b2-probe` | edit | ~eca8c58 | **~WIN** | 1603 | 1590 | 1 | 12 | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:13 | `ui-postit` | edit | ~eca8c58 | **↑WIN** | 1603 | 1590 | 1 | 12 | 목_형상은_데이터화_전후로_비트까지_같다 |
| 09-02 16:24 | `b2-bake` | edit | ~eca8c58 | **~WIN** | 1609 | 1595 | 2 | 12 | 목_형상은_데이터화_전후로_비트까지_같다<br>전체화면_판정_한_줄에_사용자숨김을_얹지_않는다 |
| 09-02 16:46 | `ui-postit` | play | ~eca8c58 | **~WIN** | 574 | 563 | 7 | 4 | CardEquipButtonWearsAndCategoryStaysMutuallyExclusive<br>OutsideClickDoesNotCloseWindowButTheCloseButtonStillDoes<br>SavedPositionInsideTheReservedTopBarIsPulledOutOnStartup<br>SavedPositionOutsideTheScreenIsPulledBackOnStartup<br>ShortClickStillSpinsAndDoesNotMoveIcon<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 |
| 09-02 16:46 | `b2-final` | edit | ~eca8c58 | **~WIN** | 1626 | 1611 | 1 | 14 | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 16:47 | `qa-round2` | edit | ~eca8c58 | **↑WIN** | 1626 | 1611 | 1 | 14 | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 18:15 | `qa-r3` | edit | ~eca8c58 | **↑WIN** | 1626 | 1611 | 1 | 14 | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 18:37 | `qa-r3` | play | ~eca8c58 | **↑WIN** | 578 | 566 | 7 | 5 | CardEquipButtonWearsAndCategoryStaysMutuallyExclusive<br>OutsideClickDoesNotCloseWindowButTheCloseButtonStillDoes<br>SavedPositionInsideTheReservedTopBarIsPulledOutOnStartup<br>SavedPositionOutsideTheScreenIsPulledBackOnStartup<br>ShortClickStillSpinsAndDoesNotMoveIcon<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 |
| 09-02 18:44 | `qa-r4b` | edit | ~eca8c58 | **~WIN** | 1637 | 1623 | 1 | 13 | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 19:06 | `qa-r4b` | play | ~eca8c58 | **↑WIN** | 582 | 576 | 2 | 4 | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다 |
| 09-02 19:28 | `c1-edit` | edit | ~7ed996d | **~OSX** | 1674 | 1657 | 2 | 15 | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 19:51 | `c1-play` | play | ~7ed996d | **↑OSX** | 589 | 579 | 6 | 4 | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>설정창_톱니_위치_행은_옮긴_뒤에만_눌리고_누르면_되돌아간다<br>온보딩이_지나가도_사용자가_옮겨_둔_자리는_그대로다<br>온보딩이_톱니를_옮겨도_사용자가_옮긴_것으로_저장되지_않는다<br>처음_자리로가_저장까지_되돌리고_다음_프레임에_되살아나지_않는다 |
| 09-02 19:52 | `c2-edit` | edit | ~7ed996d | **↑OSX** | 1674 | 1657 | 2 | 15 | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 20:16 | `c2-play` | play | ~7ed996d | **↑OSX** | 589 | 580 | 5 | 4 | G1_앱이_도는_동안_살아있는_오브젝트_바닥선이_올라가지_않는다<br>TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate<br>달팽이를_걸치면_발과_껍데기가_실제로_그려진다<br>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다<br>풍선을_걸치면_끈과_주머니가_실제로_그려진다 |
| 09-02 20:25 | `te-purge` | edit | ~7ed996d | **~OSX** | 1679 | 1660 | 4 | 15 | Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다<br>부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다<br>양성대조_심어_놓은_오염_파일을_정리기가_실제로_지운다<br>재발방지_다섯_픽스처는_저장파일을_다시_쓰지_않는다 |
| 09-02 20:28 | `te-purge2` | edit | ~7ed996d | **~OSX** | 1680 | 1664 | 1 | 15 | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다 |
| 09-02 21:00 | `te-play` | play | ~7ed996d | **↑OSX** | 589 | 584 | 1 | 4 | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate |
| 09-02 21:38 | `qa-r5` | edit | ~7ed996d | **~OSX** | 1706 | 1687 | 3 | 16 | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>줄번호_참조를_새로_만들지_않는다 |
| 09-02 22:35 | `qa-r5` | play | ~7ed996d | **~OSX** | 589 | 584 | 1 | 4 | TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate |
| 09-02 23:39 | `te-r2` | edit | 7ed996d | **OSX** | 1707 | 1688 | 3 | 16 | 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다<br>전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다<br>줄번호_참조를_새로_만들지_않는다 |
| 09-03 00:02 | `te-r2` | play | 7ed996d | **OSX** | 591 | 587 | 0 | 4 | — |
| 09-03 00:03 | `qa-r6` | edit | 7ed996d | **OSX** | 1754 | 1735 | 1 | 18 | 줄번호_참조를_새로_만들지_않는다 |

## 지금 빨간 것 — 그리고 **언제부터**인가

### edit — 최신 `qa-r6` (09-03 00:03, 타깃 OSX)

| 실패 | 마지막으로 **실제로 초록**이던 실행 | 처음 빨개진 실행 | 연속 빨강 |
|---|---|---|---:|
| 줄번호_참조를_새로_만들지_않는다 | `te-purge2` 09-02 20:28 | `qa-r5` 09-02 21:38 | 3 |

### play — 최신 `te-r2` (09-03 00:02, 타깃 OSX)

빨강 없음.


<!-- rows=32 -->
