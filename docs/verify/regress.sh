#!/usr/bin/env bash
# =============================================================================
# StickMate 전량 회귀 러너 — qa-regression 전용 (2026-09-02 신설)
#
#   사용법:
#     docs/verify/regress.sh edit  <라벨>          # EditMode 전량
#     docs/verify/regress.sh play  <라벨>          # PlayMode 전량
#     docs/verify/regress.sh report <결과.xml>     # 이미 있는 결과 파일 판독만
#     docs/verify/regress.sh compare <옛.xml> <새.xml>  # ★ 베이스라인 대조(귀속용)
#     docs/verify/regress.sh gate <log> <xml> <edit|play>   # ★ 세이브 격리 게이트 회계만 판독
#     docs/verify/regress.sh target                # 활성 빌드 타깃만 판정
#     docs/verify/regress.sh selfcheck             # ★ 가드가 실제로 무는지(음성 대조)
#
# 이 스크립트의 유일한 목적: **실패한 측정과 성공한 측정을 다르게 생기게 만드는 것.**
# 2026-09-02 하루에 거짓 통과 9건이 났고, 그중 5건이 "러너를 사람이 잘 쓰면 된다"로는 못 막혔다.
# 그래서 전부 자동 거부로 바꿨다 — 아래 가드 중 하나라도 걸리면 종료코드가 0이 아니다.
#
#  G1  -quit 금지            : -runTests와 함께 주면 0건 실행 + 종료코드 0이 된다.
#  G2  콤마 필터 금지         : -testFilter A,B 는 조용히 0건이 된다.
#  G3  결과 파일 선삭제 + mtime: 이틀 전 xml을 새 결과로 읽은 사고 재발 방지.
#  G4  testcasecount 하한     : 필터가 먹어 1건만 돌았는데 "전량 초록"이라고 말하지 못하게.
#  G5  total == testcasecount : 부분 실행(final3-play.xml: tcc=529 / total=106)을 전량으로 오인 금지.
#  G6  Library 락 선점 확인    : 다른 라운드의 Unity가 돌면 즉시 거부(컴파일 불가 트리 측정 방지).
#  G7  활성 빌드 타깃 기록     : 리플렉션 감사는 타깃에 종속이다. 어느 타깃에서 잰 것인지 남긴다.
#  G8  컴파일 실패 감지        : "Aborting batchmode due to failure: Scripts have compiler errors"
#                              가 로그에 있으면 결과 xml이 있어도 무효다.
#  G9  직전 실행 대비 건수 감소 : 정적 하한(G4)은 테스트가 늘어도 따라오지 않는다. 실제로 뚫린 구멍이다
#                              (2026-09-02: 하한 1390 vs 실제 1609 = 219건이 조용히 사라져도 통과).
#  G10 세이브 격리 게이트 회계   : ★ 2026-09-03 신설. 게이트의 **최종** 회계를 러너 xml과 **독립적으로**
#                              대조한다. 이게 없으면 게이트가 실행 도중에 죽어도 스위트가 초록이다.
#  R1  결과 판정(초록/빨강)      : ★ 2026-09-14 신설(docs/TEAM.md 「픽스처 끝에서 난 실패는 실패 개수에 안 들어간다」).
#                              초록 = failed==0 ∧ test-run result ∈ {Passed, Skipped:Ignored}
#                                     ∧ site가 SetUp/TearDown인 test-suite 0 ∧ Failed로 시작하는 test-suite 0
#                                     ∧ failed 속성 == result="Failed" test-case 수(다섯째 절 — TEAM.md 네 절보다 엄격).
#                              label로는 거르지 않는다(R2). 판정 코드는 docs/verify/nunit_verdict.py 하나다.
#                              실측: mut-M5p.xml(SetUpFixture OneTimeTearDown 실패)이 failed=0·Unity rc 0·옛 report rc=0이었다.
#  R5  판정 불가(Inconclusive) : report는 이름을 찍고, compare는 «초록 → 판정 불가» 등 전이를 따로 보인다.
#
#  ★ rc 계약(report · 전량 실행 · compare) — 2026-09-14부터:
#      0 = 초록   1 = 측정 무효(G가드 — 초록/빨강을 말하지 않는다)   3 = 측정은 유효하지만 빨강(R1)
#    옛 report는 테스트가 실패해도 rc=0이었다. 그래도 **rc만 보고 끝내지 마라** — 출력의 `✓ R1 초록` /
#    `✗ R1 빨강` 줄과 판정 불가 이름 목록을 함께 읽는다(Unity 자신의 rc는 이 실패에서 0을 냈다).
#
# =============================================================================
# ★★ 2026-09-03 G10 — 「게이트 생존이 실행 중 한 시점에서만 단언된다」
# =============================================================================
# test-engineer 자기 반려 1번(가장 큰 항목). 무엇이 뚫려 있었나:
#
#   PlayMode의 `PlayModeSaveIsolationGate`는 **리프 테스트마다** 격리 저장 폴더를 비운다.
#   그것이 살아 있는지는 `PlayModeSaveIsolationGateTests`가 단언하는데,
#   그 테스트가 도는 시점의 리프 수는 **424/591**이었다(실측).
#   **최종 회계 591/591/0/0은 `Debug.Log`에만 있었고 어떤 테스트도 읽지 않았다.**
#   ⇒ **게이트가 500번째 리프에서 죽어도 스위트는 초록이다.**
#
# 러너가 끝난 뒤에 도는 단언은 NUnit 안에 둘 자리가 없다(그 콜백은 예외를 던지면 러너를 무너뜨린다).
# 그래서 **바깥**에서 읽는다 — 그게 G10이다.
#
# ★ 이 장치의 값어치는 **회계 항등식이 러너 xml과 독립적으로 일치하는 것**이다:
#     (가) `leaf == purge + skip`          ← 게이트 자신의 회계
#     (나) `fail == 0`                      ← 삼킨 예외 0건
#     (다) play: `skip == 0` / edit: `purge == 0`  ← 플랫폼별 설계 기대
#     (라) **`leaf == xml.total == xml.testcasecount`**  ← ★ 서로 다른 두 자로 같은 실행을 잰다
#          하나는 Unity 로그(우리 C# 코드가 센 것), 하나는 NUnit xml(러너가 센 것).
#          둘이 갈라지면 그 자체가 사건이다.
#
# ★ 정규형으로 못박는다(TEAM.md 거짓 통과 13번째: "형태만 세지 말고 값이 채워졌는가까지 봐라").
#   산문 줄이 아니라 `회계 leaf=<정수> purge=<정수> skip=<정수> removed=<정수> fail=<정수>`를 읽는다.
#   값이 빈 줄(`leaf= purge= …`)은 **매치되지 않고 실패**한다 — 형태만 맞고 값이 빈 채 통과하지 못한다.
#
# =============================================================================
# ★★ 2026-09-02 자기 감사 — 이 러너 자신에게서 거짓 통과 2건을 찾았다
# =============================================================================
# 리더 지시: "가드가 재는 것과 러너가 내는 것이 같은 코드에서 나오지는 않는가."
# 확인 결과 G7(활성 타깃 판정)이 **두 겹으로** 거짓말할 수 있는 상태였다.
#
#  (가) <b>죽은 마커</b> — OSX 판정에 `MacOverlayWindow`를 썼는데 그 타입은 이 저장소에
#       <b>한 곳에도 선언돼 있지 않다</b>(선언 파일 0개). 즉 G7의 OSX 분기는 구조적으로
#       <b>절대 참이 될 수 없었다</b>. 그런데도 양성 대조(NullPlatformWindowService)와
#       음성 대조(NoSuchTypeNameXYZ123)는 <b>둘 다 통과</b>했다 — 대조가 "바이트 검색이
#       동작하는가"만 봤고 "마커가 실재하는가"는 아무도 안 봤기 때문이다.
#       → 새 가드 <b>G7a(마커 실재)</b>: 마커 이름이 소스에 타입 선언으로 없으면 UNKNOWN.
#
#  (나) <b>문자열 오염</b> — 이 탐침은 DLL을 <b>원시 바이트</b>로 훑으므로 타입 메타데이터와
#       <b>문자열 리터럴</b>을 구분하지 못한다. 실측: 활성 타깃이 WIN이라 `MacWindowService`
#       타입은 컴파일되지 않았는데도 탐침은 <b>True</b>를 냈다 —
#       `Core/StickConfig.cs`의 `[Tooltip("... MacWindowService가 세어서 넘긴다.")]` 문자열이
#       DLL에 들어가 있기 때문이다. TEAM.md 거짓통과 4번(`strings`로 부재 판정)과 같은 형태다.
#       → 새 가드 <b>G7b(마커 오염)</b>: 마커 이름이 자기 선언 파일 <b>바깥의 코드 문자열</b>에
#         나타나면 그 마커로는 부재를 말할 자격이 없다 → UNKNOWN.
#
# ★ 규칙(TEAM.md "거짓 통과 신형")의 적용: 기준과 대상이 같은 코드에서 나오면 안 된다.
#   그래서 마커 검증은 <b>DLL이 아니라 소스 트리</b>에서 하고(다른 자), 타깃 판정은 DLL에서 한다.
#   selfcheck는 <b>일부러 죽은 마커</b>를 넣어 UNKNOWN이 나오는지 확인한다(진짜 음성 대조).
# =============================================================================
set -uo pipefail

# ★ 2026-09-14 — 저장소 루트는 스크립트 위치에서 구한다(사용자명이 든 절대 경로를 박지 않는다).
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
[ -f "$REPO/docs/verify/regress.sh" ] || { echo "✗ 저장소 루트를 찾지 못했다 — REPO=$REPO" >&2; exit 1; }
export REGRESS_REPO="$REPO"   # 아래 python 블록들이 이 값으로 docs/verify 모듈을 찾는다
UNITY=/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity
OUTDIR="$REPO/docs/verify/runs"
SRCROOT="$REPO/Assets/_Project/Scripts"

# 전량 기준선(2026-09-03 실측 갱신: edit 1800 / play 591). 실제 건수가 이보다 적으면 "전량"이 아니다.
# 새 테스트가 늘면 올려도 되지만 **내리는 것은 금지** — 내리는 순간 이 가드가 죽는다.
# ★ 이 정적 하한만으로는 부족하다(G9 문서 참고). 실제 방어선은 G9(직전 실행 대비)다.
#
# ★★ 2026-09-03 — 이 두 줄이 **낡아서 아무것도 못 재는 상태**로 하루를 넘겼다.
#    하한 1390 vs 실측 1800 = **410건이 조용히 사라져도 G4는 초록**이었다.
#    같은 병이 `.claude/agents/qa-regression.md`에도 있었다(«1,394 / 558» vs 실측 «1800 / 591»).
#    ⇒ 숫자를 손으로 관리하는 한 반드시 다시 낡는다. 그래서 **G4a(하한 노후 감지)**를 넣었다:
#      직전 최대치의 95%에 못 미치는 하한은 실행할 때마다 **시끄럽게** 자기 노후를 신고한다.
#      (초록을 빨갛게 만들지는 않는다 — 그러면 테스트를 정당하게 지운 라운드를 막는다.
#       대신 **조용한 채로 남지 못하게** 한다. 이 저장소의 병은 언제나 '조용함'이었다.)
#
# ★ 2026-09-05 qa-r9 갱신 — G4a가 두 모드 모두에서 노후를 신고해 그 자리에서 올렸다.
#   실측: edit 1982건 / play 638건. 갱신 전 하한(1780 / 585)으로는 각각 **202건 / 53건이
#   조용히 사라져도 G4가 초록**이었다. 하한은 «직전 최대의 95%» 이상으로 둔다(1882 / 606).
#
# ★ 2026-09-06 test-engineer 갱신 — G4a가 play 쪽에서 다시 노후를 신고했다.
#   실측(docs/verify/runs/dbg-r29_play.xml, r28-consolidated_play.xml): **695건**.
#   갱신 전 하한 610으로는 **85건이 조용히 사라져도 G4가 초록**이었다(러너 자체 경고문).
#   하한은 «직전 최대의 95%» 이상 규칙에 따라 660(= 695 × 95 / 100, 정수 나눗셈)으로 올린다.
# ★ 2026-09-07 리더 갱신 — G4a가 edit 쪽에서 다시 노후를 신고했다(하한 1900 / 직전 최대 2717
#   = **817건이 조용히 사라져도 G4가 초록**, 러너 자체 경고문). 같은 라운드 실측: edit 2729건 /
#   play 743건. play 하한 660도 실측 743 대비 «직전 최대의 95%»(=705)에 못 미쳐 함께 올린다.
#   규칙대로 2592(= 2729 × 95 / 100) / 705(= 743 × 95 / 100)로 갱신한다.
# ★★ 2026-09-27 qa-regression 갱신 — **다섯 번째 노후다. 이번엔 두 모드가 같이 낡아 있었다.**
#   실측(`docs/verify/runs/` 최대 total, prev_best와 같은 자):
#     edit 3206(codersys-wording_edit.xml) vs 하한 2592 → **614건이 조용히 사라져도 G4가 초록**
#     play  827(codersys-wording_play.xml) vs 하한  705 → **122건이 조용히 사라져도 G4가 초록**
#   ⇒ 규칙(«직전 최대의 95%» 이상, 정수 나눗셈)대로 3045(= 3206 × 95 / 100) / 785(= 827 × 95 / 100).
#   ★ play만 지적받았지만 **edit 쪽이 더 크게 낡아 있었다**(614 > 122). 한쪽만 고치면 반대쪽이
#     조용히 남는다 — 그게 이 상수의 병이다.
#   ★ 이 숫자는 **또 낡는다.** 고정값으로는 못 막는다는 것이 다섯 번의 결론이다. 그래서 이번에
#     두 장치를 같이 넣었다(숫자만 올리면 여섯 번째가 온다):
#       ① `.meta` 사이드카에 `floor`·`floor_prev_best`·`floor_stale`을 **적는다** — G4a 경고가
#          그전까지 러너 stdout으로만 나가서 라운드가 끝나면 사라졌다(실측: 옛 .meta 11키에
#          하한 관련 0건. `runs/*.log`는 Unity의 `-logFile`이라 러너 출력이 애초에 안 들어간다).
#       ② `selfcheck`의 **양성 대조 E** — 출하된 하한을 디스크 최신 실행과 대조해 낡으면 ✗.
#          옛 selfcheck는 합성 숫자(1390/1800)로 «G4a가 무는가»만 봤고 **출하값이 낡았는지는
#          한 번도 묻지 않았다.** 그래서 705가 827 앞에서 조용할 수 있었다.
#   ★ 기각한 안 「하한을 BASELINE.md에서 읽는다」: 대장이 낡으면 하한도 같이 낡는다
#     (TEAM.md 「기준과 대상이 같이 낡은 스냅숏」). 게다가 대장은 추적되지만 그 입력인
#     `docs/verify/runs/*`는 `.gitignore` 대상이라(실측) 새 클론에서는 대장의 옛 큰 수만 남아
#     G4가 늘 빨강이 된다. 정적 하한은 **실행 이력이 없는 트리의 유일한 방어선**이라 남긴다.
# ★★★ 2026-09-27 qa-regression — **여섯 번째 노후다. 그리고 이번엔 조용히 지나가지 않았다.**
#   바로 위 항목에서 「이 숫자는 또 낡는다 · 고정값으로는 못 막는다」고 적고 장치 둘을 같이 넣었는데,
#   같은 날 새벽 `coder` 라운드가 EditMode 테스트를 신설해 **여섯 번째가 실제로 왔다.**
#   사람이 눈치챈 것이 아니라 **selfcheck 양성 대조 E가 ✗로 잡았다**(그 실행 rc=1).
#     · 상향 전 selfcheck 실측: ✓ 69 / ✗ 1 — 그 ✗ 하나가 edit 하한 노후였다.
#       같은 실행에서 **play는 ✓**였다 ⇒ 이 대조는 「항상 신고」가 아니다(양성·음성이 한 실행에 같이 섰다).
#     · 키 집합 대조(규칙 24): 그 라운드의 EditMode 증가는 **삭제 0 · 신설만**이었다. 수 델타만
#       봤다면 「신설 N − 삭제 M」을 구분할 수 없었다 — 그래서 수와 **이름 집합**을 함께 본다.
#   ★ 유도 규칙(이것이 정본이다): **하한 = 직전 최대 total × STALE_FLOOR_RATIO_PCT / 100**(정수 나눗셈).
#     이번 근거는 `coder-r10*_edit`(2026-09-27 새벽) 전량 실행이고, **그 total 값은 여기 적지 않는다.**
#     적으면 그것이 다음 라운드에 낡는 「값」이 된다 — 위 항목에서 BASELINE.md 참조안을 기각한 바로 그 이유다.
#     **다시 재는 곳은 한 곳이다: `regress.sh selfcheck`의 양성 대조 E가 매번 디스크에서 직전 최대를 구한다.**
#   ★ MIN_PLAY_CASES는 **건드리지 않는다** — 그 라운드에서 PlayMode 총계가 불변이었고 785가 여전히
#     95% 기준을 만족한다(대조 E가 play를 ✓로 찍어 확인). 「한쪽만 움직였다」는 사실도 기록이다.
# ★★★ 2026-09-27 qa-regression — **일곱 번째 노후다. 그리고 예고한 대로 왔다.**
#   값 교체이므로 원문을 여기 남긴다(글자 삭제 0):
#     원문: MIN_EDIT_CASES=3050  ·  MIN_PLAY_CASES=785
#     신문: MIN_EDIT_CASES=3054  ·  MIN_PLAY_CASES=785(그대로)
#
#   같은 날 test-engineer가 EditMode 잠금 테스트 4건을 신설해 전량이 3211 -> 3215가 됐고,
#   그 순간 하한 3050이 «직전 최대의 95%»(3054)에 미달했다. 사람이 눈치챈 것이 아니라
#   **정본 판 실행이 G4a 경고를 냈고 `.meta`에 floor_stale=1이 처음으로 찍혔다**(그 키를 넣은 값이다).
#   ⇒ 유도 규칙 그대로: 하한 = 직전 최대 total × STALE_FLOOR_RATIO_PCT / 100(정수 나눗셈).
#     ★ 검산은 **러너 자신의 stale_floor_report를 격리 실행**해서 했다 — 3050/3215를 주면 그 함수가
#       «MIN_EDIT_CASES를 3054 이상으로 올려라»를 직접 출력하고, 3054를 주면 조용하다.
#       즉 이 값은 내 산식이 아니라 **판정하는 그 함수가 낸 값**이다.
#     ★ 그 total 값은 여기 적지 않는다(위 항목과 같은 사유 — 적으면 그것이 다음에 낡는 「값」이 된다).
#   ★ MIN_PLAY_CASES는 **건드리지 않는다.** 정본 play 총계가 827로 불변이었고 785가 여전히 95%
#     기준을 만족한다. 「한쪽만 움직였다」는 사실도 기록이다. 구조적 근거: 신설 4건은
#     Tests/EditMode/ 폴더이고 그 asmdef가 includePlatforms=['Editor']라 PlayMode 어셈블리에
#     들어갈 수 없다(실측). 그래서 play 총계 827을 **미리 예측해 못박고** 실행으로 확인했다.
# ★★★ 2026-09-28 리더 — **여덟 번째 노후다. 그리고 바로 위 항목이 예고한 그대로 왔다.**
#   값 교체이므로 원문을 여기 남긴다(글자 삭제 0):
#     원문: MIN_EDIT_CASES=3054  ·  MIN_PLAY_CASES=785
#     신문: MIN_EDIT_CASES=3054(그대로)  ·  MIN_PLAY_CASES=789
#
#   위 항목이 「MIN_PLAY_CASES는 건드리지 않는다」의 구조적 근거로 **신설 4건이 Tests/EditMode/
#   폴더이고 그 asmdef가 includePlatforms=['Editor']라 PlayMode 어셈블리에 들어갈 수 없다**를 적었다.
#   이번에는 coder가 잠금 4건을 **Tests/PlayMode/에** 넣었으므로 그 예측이 정확히 반대로 실현됐다 —
#   즉 저 근거는 틀린 것이 아니라 **조건이 바뀐 것**이다. 그 문장을 지우지 마라(조건을 적어 둔 값이다).
#
#   ★ 사람이 눈치챈 것이 아니다. **러너 자신이 G4a로 직접 말했다**:
#     «play 하한=785 / 직전 최대=<그 실행의 total> … 지금 하한으로는 <그 차이>건이 조용히 사라져도
#       G4가 초록이다.
#     ★ 인용에서 total과 차이를 **일부러 뺐다** — 위 유도 규칙이 「그 total 값은 여기 적지 않는다」이고
#       적으면 다음 라운드에 낡는 「값」이 된다. 리더가 처음 쓸 때 그 숫자를 넣었고 자기 검사로 잡았다.
#       실제 숫자는 그 실행의 `.meta`와 `docs/verify/BASELINE.md`의 해당 행에 있다.
#       regress.sh의 MIN_PLAY_CASES를 789 이상으로 올려라.»
#   ⇒ 이 값은 내 산식이 아니라 **판정하는 그 함수가 낸 값**이다(리더 독립 산술도 789로 일치했다).
#   ★★ 리더 자백: 그 경고는 실행 로그 **3행**에 있었는데 리더의 선택적 판독(R1·testcasecount·G10만 골라
#     본 것)이 **걸러 버렸다.** 「초록은 조용하다」의 반대 형태다 — **시끄럽게 말했는데 안 들었다.**
#     ⇒ 앞으로 러너 판독 니들에 **「올려라」·「⚠」·「G4a」를 반드시 포함한다.**
#   ★ 유도 규칙(정본)은 위와 같다: 하한 = 직전 최대 total × STALE_FLOOR_RATIO_PCT / 100(정수 나눗셈).
#     ★★ **그 total 값은 여기 적지 않는다** — 위 두 항목이 기각한 바로 그 이유다(적으면 다음에 낡는
#     「값」이 된다). 다시 재는 곳은 한 곳이다: `regress.sh selfcheck`의 양성 대조 E.
#   ★ ~~MIN_EDIT_CASES는 **건드리지 않는다.** EditMode 총계가 이번 실행에서 불변이었고(compare 전 항목~~
#     ~~0건·rc=0으로 개별까지 확인) 3054가 여전히 95% 기준을 만족한다. 「한쪽만 움직였다」는 사실도 기록이다.~~
# ★★★ 2026-09-28 리더(같은 날 두 번째) — **아홉 번째 노후다. 그리고 바로 위 두 줄이 하루 만에 거짓이 됐다.**
#   값 교체이므로 원문을 위에 취소선으로 남겼다(글자 삭제 0).
#     원문: MIN_EDIT_CASES=3054  ·  MIN_PLAY_CASES=789
#     신문: MIN_EDIT_CASES=3062  ·  MIN_PLAY_CASES=789(그대로)
#
#   ★ 무슨 일이 있었나: 같은 묶음에서 **EditMode 잠금 9건이 신설됐다**(빌드 인자 원복 감사 3 + 락 사유 6).
#     위 두 줄은 그 신설이 **들어오기 전에** 쓰인 것이고, 그래서 「불변」이 그 실행에서는 참이었다.
#     ⇒ **as-of를 붙이지 않은 「불변」은 다음 실행에서 거짓이 된다.** 취소선으로 남긴 이유가 그것이다.
#   ★★ 이번에는 러너가 **아직 조용했다** — G4a가 본 「직전 최대」는 신설 전 값이라 노후로 판정되지 않았다.
#     즉 **러너의 침묵이 「하한이 건전하다」를 뜻하지 않는다**: G4a는 직전 최대를 보고, 이번 실행의 총계는
#     다음 실행에서야 직전 최대가 된다. ⇒ **잠금을 신설한 묶음은 러너가 말하기 전에 리더가 하한을 닫는다.**
#     (규칙 22는 「시끄럽게 말했는데 안 들었다」를 막는다. 이 항은 그 반대 — **아직 말하지 않은 것을 미리 듣는다**.)
#   ★ 유도는 정본 규칙 그대로다: 하한 = 직전 최대 total × STALE_FLOOR_RATIO_PCT / 100(정수 나눗셈).
#     ★★ **그 total 값은 여기 적지 않는다** — 위 항목들이 기각한 바로 그 이유다(적으면 다음에 낡는 「값」이
#     된다. 리더가 직전 묶음에서 그 숫자를 넣었다가 자기 검사로 잡았다). 다시 재는 곳은 `selfcheck`의 양성 대조 E다.
#   ★ MIN_PLAY_CASES는 **이번에 건드리지 않았다** — 같은 묶음이 PlayMode 잠금을 늘리지 않았고(compare에서
#     「새로 생긴 테스트 0건」으로 확인) 789가 여전히 95% 기준을 만족한다. **이 문장에도 as-of가 붙는다** —
#     다음에 PlayMode 잠금이 늘면 그 순간 거짓이 된다.
MIN_EDIT_CASES=3062
MIN_PLAY_CASES=789

# 하한 노후 판정 계수 — 직전 최대치의 이 비율보다 하한이 낮으면 노후로 본다.
STALE_FLOOR_RATIO_PCT=95

# ★ 플랫폼 마커. **반드시 실재하는 타입 이름**이라야 하고, 자기 선언 파일 바깥의
#   코드 문자열에 나오면 안 된다. 아래 두 조건은 G7a/G7b가 매 실행 자동 검증한다
#   — 손으로 지키는 규칙은 이 저장소에서 이미 아홉 번 실패했다.
MARKER_WIN=Win32WindowService
MARKER_OSX=MacSpaceBehaviorNative
MARKER_ALWAYS=NullPlatformWindowService   # 양성 대조: 플랫폼 무관하게 항상 컴파일된다
MARKER_NEVER=NoSuchTypeNameXYZ123         # 음성 대조: 존재할 리 없는 이름

die() { echo "✗ $*" >&2; exit 1; }

# ---- G7 활성 빌드 타깃 -------------------------------------------------------
# 실제로 컴파일된 어셈블리에게 묻는다 — 플랫폼 전용 타입이 그 안에 있는가.
# #if 로 잘려 나간 타입은 이름이 메타데이터에 존재하지 않는다. rsp 파일의 mtime보다 이것이 사실이다
# (2026-09-02 실측: 플레이어 빌드가 반대편 rsp를 더 새것으로 만들어 mtime 판정이 거짓말을 했다).
#
# ★ 단, 바이트 검색은 타입과 문자열을 구분하지 못한다 — 그래서 마커 자체를 먼저 검증한다.
#   G7a: 마커가 소스에 타입 선언으로 실재하는가 (죽은 마커 방지)
#   G7b: 마커가 자기 선언 파일 바깥의 코드 문자열에 오염돼 있지 않은가 (부재 판정 자격)
verify_marker() {   # $1=마커 이름 → 0=쓸 수 있다 / 1=못 쓴다(사유를 stdout에)
  local m="$1" decl gate polluted
  decl=$(grep -rlE "(class|struct|interface|enum)[[:space:]]+${m}\b" --include="*.cs" "$SRCROOT" 2>/dev/null | head -1)
  if [ -z "$decl" ]; then
    echo "G7a:마커 '${m}'가 소스에 타입 선언으로 없다(죽은 마커 — 이 분기는 절대 참이 될 수 없다)"
    return 1
  fi

  # ★ 이 마커가 걸려 있는 플랫폼 게이트. 같은 게이트 안의 파일은 **함께 잘려 나가므로**
  #   반대 타깃의 DLL에 그 문자열을 남기지 못한다 = 부재 판정을 오염시킬 수 없다.
  #   (2026-09-02 실측으로 이 구분을 추가했다: `WindowsTopmostWatchdog.cs`의
  #    `Debug.LogWarning("... Win32WindowService.CreateOverlayWindow()가 ...")`가 오염으로 잡혀
  #    양성 대조가 빨개졌는데, 그 파일은 자신도 `#if UNITY_STANDALONE_WIN` 안이라 무해했다.
  #    반대로 `Core/StickConfig.cs`는 **게이트가 없어** 모든 타깃에 컴파일된다 — 그쪽이 진짜다.)
  gate=$(grep -o "UNITY_STANDALONE_[A-Z]*" "$decl" 2>/dev/null | sort -u | head -1)

  # 자기 선언 파일이 아니고, 같은 게이트도 아닌 파일의 **코드 문자열**에 이름이 있는가.
  # (`//`·`///`·`*`로 시작하는 주석 줄은 컴파일되지 않으므로 제외한다.)
  polluted=$(grep -rn "\"[^\"]*${m}" --include="*.cs" "$SRCROOT" 2>/dev/null \
             | grep -v "/Tests/" \
             | grep -v "^${decl}:" \
             | awk -F: '{ line=$0; sub(/^[^:]*:[0-9]*:/, "", line);
                          gsub(/^[ \t]+/, "", line);
                          if (line !~ /^\/\// && line !~ /^\*/) print $1 ":" $2 }' \
             | while IFS=: read -r pf pl; do
                 if [ -n "$gate" ] && grep -q "$gate" "$pf" 2>/dev/null; then continue; fi
                 echo "${pf}:${pl}"; break
               done)
  if [ -n "$polluted" ]; then
    echo "G7b:마커 '${m}'가 게이트 밖 코드 문자열에 있다(${polluted}) — 이 이름으로는 부재를 말할 수 없다"
    return 1
  fi
  return 0
}

active_target() {
  local dll="$REPO/Library/ScriptAssemblies/StickMate.Runtime.dll"
  [ -f "$dll" ] && [ -s "$dll" ] || { echo "UNKNOWN(어셈블리없음)"; return; }

  local reason
  for m in "$MARKER_WIN" "$MARKER_OSX" "$MARKER_ALWAYS"; do
    reason=$(verify_marker "$m") || { echo "UNKNOWN(${reason})"; return; }
  done
  # 음성 대조 마커는 **없어야** 정상이다 — 실재하면 그것대로 탐침이 무의미해진다.
  if grep -rqE "(class|struct|interface|enum)[[:space:]]+${MARKER_NEVER}\b" --include="*.cs" "$SRCROOT" 2>/dev/null; then
    echo "UNKNOWN(음성대조 마커가 실재한다 — MARKER_NEVER를 바꿔라)"; return
  fi

  python3 - "$dll" "$MARKER_WIN" "$MARKER_OSX" "$MARKER_ALWAYS" "$MARKER_NEVER" <<'PYT'
import sys
dll, mwin, mosx, malways, mnever = sys.argv[1:6]
b = open(dll, 'rb').read()
def has(name):
    return b.count(name.encode('utf-8')) > 0 or b.count(name.encode('utf-16-le')) > 0
if not has(malways):
    print('UNKNOWN(양성대조실패)'); raise SystemExit
if has(mnever):
    print('UNKNOWN(음성대조실패)'); raise SystemExit
t = []
if has(mwin): t.append('UNITY_STANDALONE_WIN')
if has(mosx): t.append('UNITY_STANDALONE_OSX')
if len(t) == 2:
    print('UNKNOWN(양쪽 다 잡힘 — 마커가 오염됐다)'); raise SystemExit
print(t[0] if t else 'UNKNOWN(플랫폼타입없음)')
PYT
}

# ---- G1/G2 실행 인자 검사 ----------------------------------------------------
# ★ 2026-09-02 — 예전에는 이 두 가지가 **주석으로만** 존재했다("-quit 없음(G1). 두 줄을 지우지 마라").
#   주석은 아무것도 재지 않는다. 누가 인자를 고치면 헤더는 여전히 "G1~G8"이라고 적혀 있고
#   0건 실행 + 종료코드 0이 그대로 나온다 — 이 러너가 막으려던 바로 그 형태다. 이제 코드로 만든다.
assert_launch_args() {
  local prev="" a
  for a in "$@"; do
    [ "$a" = "-quit" ] && die "G1: 실행 인자에 -quit이 있다 — -runTests와 함께 주면 0건 실행 + 종료코드 0이 된다."
    if [ "$prev" = "-testFilter" ]; then
      case "$a" in
        *,*) die "G2: -testFilter 값에 콤마가 있다('$a') — 콤마 구분 필터는 조용히 0건이 된다." ;;
      esac
    fi
    prev="$a"
  done
  return 0
}

# ★ 2026-09-03 교체 — 옛 구현은 `pgrep -f "Unity.app/Contents/MacOS/Unity -batchmode"`였다.
#   `-f`는 **명령줄 전체**를 본다 = 그 문자열을 담은 스크립트·에디터·grep 자신이 걸린다.
#   앞 라운드가 정확히 그렇게 **자기 자신을 잡고** 50분을 헛기다린 뒤 "포기"를 뱉었고,
#   그 출력은 「정말로 락이 안 풀렸다」와 **똑같이 생겼다**.
#   그래서 **실행파일 경로(comm)** 로 후보를 고르고, 그 PID의 args 에서 -batchmode 를 확인한다.
#   내 셸의 comm 은 /bin/bash 라 구조적으로 자기 자신을 셀 수 없다.
#
# ★ 그리고 「0건」이 「비었다」인지 「프로브가 죽었다」인지 구분되게 **양성 대조**를 붙인다.
#   (실측 2026-09-03: 임시 확인에 `grep -v Hub`를 붙였다가 도는 Unity를 지웠다 —
#    에디터 경로가 `/Applications/Unity/**Hub**/Editor/.../Unity` 라서 필터에 자기가 걸린다.)
UNITY_BIN_PATH="$UNITY"
assert_no_unity_running() {
  local pid pids="" probe
  probe=$(ps -axo pid=,comm= | wc -l | tr -d ' ')
  [ "${probe:-0}" -gt 5 ] || die "G6: 프로세스 탐침이 죽었다(ps가 ${probe}줄). 선점 여부를 판정할 수 없다 — 재면 안 된다."
  while read -r pid; do
    [ -n "$pid" ] || continue
    ps -p "$pid" -o args= 2>/dev/null | grep -q -- "-batchmode" && pids="$pids $pid"
  done < <(ps -axo pid=,comm= | awk -v b="$UNITY_BIN_PATH" 'index($0, b) > 0 {print $1}')
  [ -z "$pids" ] || die "G6: Unity 배치모드가 이미 돌고 있다(PID$pids). Library 락이 잡혀 있으므로 지금 재면 무효다. 리더에게 창(window)을 받아라."
}

# ---- G9 직전 실행 대비 건수 --------------------------------------------------
# 정적 하한(G4)은 테스트가 늘어도 따라오지 않는다. 2026-09-02 실측: 하한 1390 / 실제 1609
# → **219건이 조용히 사라져도 G4는 초록**이었다. 어셈블리 하나가 컴파일에서 빠지면 딱 그 형태로 사라진다.
prev_best() {   # $1=mode(edit|play)  $2=이번 실행이 쓸 xml(제외)  → 최대 total 과 그 파일명
  python3 - "$OUTDIR" "$1" "${2:-}" <<'PY'
import sys, os, glob
import xml.etree.ElementTree as ET
outdir, mode, skip = sys.argv[1], sys.argv[2], sys.argv[3]
best, who = 0, ''
for p in glob.glob(os.path.join(outdir, f'*_{mode}.xml')):
    if skip and os.path.abspath(p) == os.path.abspath(skip):
        continue
    try:
        r = ET.parse(p).getroot()
    except Exception:
        continue
    tot = int(r.get('total') or 0)
    if tot > best:
        best, who = tot, os.path.basename(p)
print(f"{best} {who}")
PY
}

# ---- G8 컴파일 실패 감지 -----------------------------------------------------
# ★★ 2026-09-03 — 이 가드가 **죽은 프로브**였다. 실측으로 잡았다.
#   원래 마커는 "Aborting batchmode due to failure"였는데 그 문장은 Unity의 **stdout에만** 나오고
#   `-logFile`이 받는 로그 파일에는 **한 번도 안 찍힌다**. 실제 컴파일 실패 로그
#   (qa-r7_edit.log, 12×CS0103)에서 그 마커의 등장 횟수는 **0**이었다.
#   ★★ 2026-09-27 qa-regression — **위 줄의 파일 인용은 썩었다.** 줄은 지우지 않고 남긴다.
#     지금 디스크의 그 파일은 컴파일 실패 로그가 아니라 **정상 완주 실행**이다(`error CS` 0).
#     이름이 `<라벨>_<모드>.log`라서 **러너 자신의 쓰기 경로 안**에 있었기 때문이다 —
#     **라벨 재사용은 증거를 지운다.**
#     ★ 다만 **주장 자체는 더 강해졌다**: 디스크의 로그 **1318개 전수에서 이 마커는 0건**이다
#       (양성 대조: 같은 스윕이 "Unity"를 1301개에서 잡는다 ⇒ 그 0은 죽은 프로브가 아니다).
#       ⇒ 근거를 «한 파일»에서 «전수»로 올렸다. 파일 이름에 기대는 인용은 라벨 재사용에 지워진다.
#   ⇒ G8은 "컴파일 실패를 못 봤다"와 "컴파일이 성공했다"를 구분하지 못한 채 통과시키고 있었다.
#     이번엔 G3(결과 파일 부재)가 대신 물어서 사고가 안 났지만, G3는 다른 것을 재는 가드다.
#     둘 중 하나라도 순서가 바뀌면 그대로 거짓 통과다.
#
#   지금 마커 3종은 **실측으로 교정**했다:
#     · 실패 로그 qa-r7_edit.log      → "Scripts have compiler errors"=1, "## Script Compilation Error for"=1
#       ★★ 2026-09-27 qa-regression — **이 계측은 지금 거짓이다.** 줄은 지우지 않고 남긴다(글자 삭제 0).
#          실측: 그 파일의 두 마커는 **모두 0**이고, 짝 xml은 total 1813 / failed 5인 **정상 완주 실행**
#          이며 로그에 `error CS` 0 · COMMAND LINE ARGUMENTS 1 · 세이브게이트 회계 1이다.
#          ⇒ 교정 근거가 못 된다. 원인은 이 이름이 `<라벨>_<모드>.log`라 **러너 쓰기 경로 안**이라는 것이다
#            — **라벨 재사용은 증거를 지운다.** (「덮였다」로 단정하지는 않는다: 디스크만으로는 덮임과
#             처음부터 틀린 인용을 가를 수 없고, 어느 쪽이든 인용이 못 쓰게 된 사실은 같다.)
#       ⇒ **썩지 않는 교정 근거 셋으로 갈아탄다**(전부 실측):
#          ① **이 스크립트 안의 합성 픽스처** — 아래 selfcheck 음성 대조 12가 매 실행 이것으로 G8을
#             교정한다. 파일이 아니라 스크립트 본문이라 **어떤 실행도 덮을 수 없다.**
#             앵커(줄 번호가 아니라 고정 문자열): `cat > "$tmp/fail.log"`
#          ② **계수** — 디스크 로그 1318개 중 두 마커를 **함께** 든 것이 35개이고, 그중 **33개가
#             러너 쓰기 경로 밖**(`Logs/`의 임의 이름)이라 라벨 재사용에 지워지지 않는다.
#          ③ **쓰기 경로 밖 실례 하나** — `Logs/arch_compile3.log`(두 마커 각 1).
#       ★ 아래 «정상 로그 5종»은 **이번에 다시 확인했고 전부 실재·깨끗(0/0)하다** — 음성 대조 쪽은
#         썩지 않았으므로 건드리지 않는다. 썩은 것은 양성(실패) 쪽 인용 하나였다.
#     · 정상 로그 5종(te-r2_edit/play, qa-r6_edit, coder-grabline_edit, qa-r5_play) → 3종 전부 0
#   (옛 마커는 남겨 둔다 — Unity 버전에 따라 로그로 갈 수 있다. 늘리는 것은 안전하다.)
COMPILE_FAIL_MARKERS=(
  "Scripts have compiler errors"
  "## Script Compilation Error for"
  "Aborting batchmode due to failure"
)
log_compile_failed() {   # $1=로그 경로 → 0=컴파일 실패다 / 1=아니다
  local log="$1" m
  [ -f "$log" ] || return 1
  for m in "${COMPILE_FAIL_MARKERS[@]}"; do
    grep -qF "$m" "$log" 2>/dev/null && return 0
  done
  return 1
}

# ---- G4a 정적 하한 노후 감지 -------------------------------------------------
# 하한은 사람이 손으로 올린다 = 반드시 낡는다. 낡은 하한은 **아무 소리도 내지 않는다** —
# 이 저장소 거짓 통과의 표준형("실패한 측정과 성공한 측정이 똑같이 생겼다")이다.
# 그래서 하한 자신에게 자기 노후를 신고시킨다. 판정은 하지 않고(정당한 삭제를 막지 않는다)
# **반드시 화면에 뜨게** 한다.
stale_floor_report() {   # $1=현재 하한 $2=직전 최대 $3=그 파일 $4=mode
  local minc="$1" prevn="$2" prevwho="$3" mode="$4"
  [ "${prevn:-0}" -gt 0 ] || { echo "G4a: 직전 실행이 없어 하한 노후를 판정할 수 없다(미확인)."; return 0; }
  local thresh=$(( prevn * STALE_FLOOR_RATIO_PCT / 100 ))
  if [ "$minc" -lt "$thresh" ]; then
    echo "⚠⚠ G4a: 정적 하한이 낡았다 — ${mode} 하한=${minc} / 직전 최대=${prevn}(${prevwho})."
    echo "     지금 하한으로는 **$(( prevn - minc ))건이 조용히 사라져도 G4가 초록**이다."
    echo "     regress.sh의 MIN_$(echo "$mode" | tr a-z A-Z)_CASES를 ${thresh} 이상으로 올려라."
  else
    echo "G4a: 하한 ${minc} / 직전 최대 ${prevn} — 노후 아님(기준 ${thresh})."
  fi
  return 0
}

# ---- G10 세이브 격리 게이트 회계 --------------------------------------------
# 읽는 것: Unity 로그의 정규형 한 줄
#   [세이브게이트] 회계 leaf=<n> purge=<n> skip=<n> removed=<n> fail=<n>
# 그 줄을 내는 곳: Assets/_Project/Scripts/Tests/PlayMode/PlayModeSaveIsolationGate.cs 의 RunFinished.
#
# ★ 기준과 대상이 같은 코드에서 나오지 않는다(TEAM.md "거짓 통과 신형"):
#   게이트 회계는 **우리 C# 콜백**이 세고, total/testcasecount는 **NUnit 러너**가 센다.
#   둘을 대조하는 이 함수는 어느 쪽 코드도 공유하지 않는다.
gate_audit() {   # $1=로그  $2=결과 xml  $3=mode(edit|play)  → 0=통과 / 그 외=위반
  local log="$1" xml="$2" mode="$3"
  python3 - "$log" "$xml" "$mode" <<'PY'
import os, re, sys
import xml.etree.ElementTree as ET

log, xml, mode = sys.argv[1], sys.argv[2], sys.argv[3]
bad = []

if not os.path.isfile(log):
    print(f"✗ G10: 로그 파일이 없다 — {log}. 게이트 회계를 잴 방법이 없다(측정 무효).")
    sys.exit(1)
if not os.path.isfile(xml):
    print(f"✗ G10: 결과 xml이 없다 — {xml}.")
    sys.exit(1)

text = open(log, encoding='utf-8', errors='replace').read()

# 느슨한 니들(줄이 있기는 한가) / 엄격한 정규형(값이 채워졌는가) — 둘을 나눠 센다.
loose  = re.findall(r'\[세이브게이트\] 회계.*', text)
strict = re.findall(
    r'\[세이브게이트\] 회계 leaf=(\d+) purge=(\d+) skip=(\d+) removed=(\d+) fail=(\d+)\s*$',
    text, re.MULTILINE)

if not strict:
    if loose:
        print(f"✗ G10: 정규형이 깨졌다 — '회계' 줄은 {len(loose)}개 있는데 "
              f"`leaf=<정수> purge=<정수> skip=<정수> removed=<정수> fail=<정수>` 형태가 0개다.")
        print(f"       첫 줄: {loose[0][:200]}")
        print("       값이 비어도 줄 수는 맞는다 — 형태만 세면 통과한다(TEAM.md 거짓 통과 13번째). "
              "PlayModeSaveIsolationGate.RunFinished와 이 정규식을 같은 커밋에서 맞춰라.")
    else:
        print("✗ G10: 세이브 격리 게이트의 회계 줄이 로그에 **한 건도 없다**.")
        print("       (가) 어셈블리 콜백이 더는 안 붙는다(Unity Test Framework 변경), 또는")
        print("       (나) PlayModeSaveIsolationGate.RunFinished의 로그 줄이 지워졌다.")
        print("       어느 쪽이든 **게이트가 살아 있다는 근거가 이번 실행에 하나도 없다**.")
    sys.exit(1)

if len(strict) != 1:
    print(f"✗ G10: 회계 줄이 {len(strict)}개다 — 한 로그에 여러 실행이 섞였다.")
    print("       리프 수가 어느 실행의 것인지 특정할 수 없으므로 이 측정은 무효다.")
    sys.exit(1)

leaf, purge, skip, removed, fail = (int(x) for x in strict[0])

r = ET.parse(xml).getroot()
tcc = int(r.get('testcasecount') or 0)
tot = int(r.get('total') or 0)

print(f"G10 게이트 회계: leaf={leaf} purge={purge} skip={skip} removed={removed} fail={fail}")
print(f"    러너 xml   : testcasecount={tcc} total={tot}   (mode={mode})")

# (가) 게이트 자신의 회계 항등식
if leaf != purge + skip:
    bad.append(f"회계 항등식이 깨졌다 — leaf({leaf}) != purge({purge}) + skip({skip}). "
               "게이트가 어떤 경로로 조용히 빠져나가고 있다.")

# (나) 삼킨 예외
if fail != 0:
    bad.append(f"게이트가 정리 중 예외를 {fail}건 삼켰다. 그 리프들은 앞 테스트의 저장 파일을 "
               "물려받은 채 돌았다(로그에서 '[세이브게이트] ★ 정리 실패'를 찾아라).")

# (다) 플랫폼별 설계 기대
if mode == 'play':
    if skip != 0:
        bad.append(f"PlayMode 실행인데 게이트가 {skip}회 건너뛰었다 — 그 리프는 격리되지 않았다.")
    if purge != leaf:
        bad.append(f"PlayMode 실행인데 정리 시도({purge})가 리프({leaf})와 다르다.")
elif mode == 'edit':
    # EditMode는 **일부러 전량 건너뛴다**(씬을 안 띄우므로 축적 경로가 없다).
    # 그 설계가 실제로 도는지를 여기서 못박는다 — 주석이 아니라 숫자로.
    if purge != 0:
        bad.append(f"EditMode 실행인데 게이트가 {purge}회 실제로 지웠다. 설계상 EditMode는 "
                   "전량 건너뛴다(PlayModeSaveIsolationGate 클래스 문서). 사정거리가 넓어졌다.")
    if skip != leaf:
        bad.append(f"EditMode 실행인데 건너뜀({skip})이 리프({leaf})와 다르다.")
else:
    bad.append(f"알 수 없는 mode '{mode}' — edit/play만 판정할 수 있다.")

# (라) ★ 독립 대조 — 다른 자로 잰 같은 실행
if tot != leaf:
    bad.append(f"★ 게이트가 센 리프({leaf})와 러너 xml의 total({tot})이 다르다. "
               "게이트 콜백이 일부 리프에 안 붙었거나, 실행이 도중에 갈라졌다. "
               "둘은 같은 실행을 서로 다른 자로 센 값이므로 반드시 같아야 한다.")
if tcc != leaf:
    bad.append(f"★ 게이트가 센 리프({leaf})와 러너 xml의 testcasecount({tcc})가 다르다.")

for b in bad:
    print("\n✗ G10: " + b)
if not bad:
    print("G10: 통과 — 게이트 회계가 항등식과 러너 xml 양쪽에 모두 맞는다(실행 끝까지 살아 있었다).")
sys.exit(1 if bad else 0)
PY
}

# ---- 결과 판독 -------------------------------------------------------------
report() {   # $1=xml  $2=기대 최소 건수(선택)  $3=실행 시작 epoch(선택)  $4=직전 최대(선택) $5=그 파일(선택)
  local xml="$1" minc="${2:-0}" started="${3:-0}" prevn="${4:-0}" prevwho="${5:-}"
  [ -f "$xml" ] || die "G3: 결과 파일이 없다 — $xml. 테스트가 한 건도 돌지 않았다."
  local mt; mt=$(stat -f %m "$xml")
  if [ "$started" -gt 0 ] && [ "$mt" -lt "$started" ]; then
    die "G3: 결과 파일이 실행 시작($(date -r "$started" '+%H:%M:%S'))보다 오래됐다($(date -r "$mt" '+%H:%M:%S')) — 낡은 파일을 읽고 있다."
  fi
  # ★ 2026-09-14 — rc 계약을 바꿨다: 0 = 초록 / 1 = 측정 무효(G가드) / 3 = 측정은 유효하지만 빨강(R1).
  #   옛 report는 테스트가 실패해도 rc=0이었고, 픽스처 끝 실패(mut-M5p.xml)는 실패 목록에조차 안 떴다.
  python3 - "$xml" "$minc" "$prevn" "$prevwho" <<'PY'
import sys, os, datetime
import xml.etree.ElementTree as ET
import os
sys.path.insert(0, os.path.join(os.environ['REGRESS_REPO'], 'docs/verify'))
import nunit_verdict as NV
xml, minc = sys.argv[1], int(sys.argv[2])
prevn, prevwho = int(sys.argv[3]), sys.argv[4]
try:
    r = ET.parse(xml).getroot()
except Exception as e:
    print(f"✗ 결과 xml을 읽지 못했다 — {e}. 측정 무효.")
    sys.exit(1)
tcc = int(r.get('testcasecount') or 0)
tot = int(r.get('total') or 0)
fa  = int(r.get('failed') or 0)
sk  = int(r.get('skipped') or 0)
inc = int(r.get('inconclusive') or 0)
pa  = int(r.get('passed') or 0)
mt  = datetime.datetime.fromtimestamp(os.stat(xml).st_mtime).strftime('%m-%d %H:%M:%S')
print(f"결과파일 {xml}  (mtime {mt})")
print(f"  testcasecount={tcc} total={tot} passed={pa} failed={fa} skipped={sk} inconclusive={inc}")

# ★ 2026-09-14 — 「부분 실행」 배너 (test-engineer 명세 docs/verify/PLAYMODE_RED7_FIX_SPEC.md §8 1안, 리더 채택).
#   G5(tcc != total)는 **Unity 필터 실행을 못 본다** — mut-M5p.xml은 필터로 3건만 돌렸는데 tcc=3 total=3이다.
#   서로 다른 자 둘로 잰다:
#     (가) 기록된 사실 — nunit_verdict.run_scope(로그 명령줄 필터 인자 / regress.sh 사이드카). baseline.py 「현재」와 같은 판정.
#     (나) 소스 리프 하한 — nunit_verdict.source_leaf_count(지금 트리의 [Test]/[UnityTest]/[TestCase] 계수).
#   ★ 배너는 **판정 rc를 바꾸지 않는다**(부분 실행이 곧 실패는 아니다). 대신 «전량»이라고 말하지 못하게 한다.
_scope, _scope_why = NV.run_scope(xml, r, NV.read_meta_file(os.path.splitext(xml)[0] + '.meta'))
_plat = NV.xml_platform(r)
_src = NV.source_leaf_count(os.path.join(os.environ['REGRESS_REPO'], 'Assets/_Project/Scripts/Tests'), _plat) if _plat else None
if _src:
    _m = f"≥{_src['lower_bound']}" if _src['unknown'] else f"{_src['lower_bound']}"
    _srcdesc = (f"소스 리프 {_m}(정적 {_src['known']} + 전개 미확인 메서드 {len(_src['unknown'])}개, "
                f"{_plat} 폴더 파일 {_src['files']}개)")
else:
    _m, _srcdesc = "?", f"소스 리프 미확인(xml에 platform 속성이 없다: {_plat})"
if _scope == '부분':
    print(f"\n★★ 부분 실행 {tot}/{_m} — 전량 아님 · 기록: {_scope_why}")
elif _src and tot < _src['lower_bound']:
    if _scope == '전량':
        print(f"\n⚠⚠ 로그는 전량인데 실행 {tot} < {_srcdesc} — 실행 뒤 테스트가 늘었거나 계수 규칙이 틀렸다. "
              "이 xml을 **지금 트리의 전량**이라고 말하지 마라.")
    else:
        print(f"\n★★ 부분 실행 {tot}/{_m} — 전량 아님 · 실행 수가 소스 리프 하한보다 적다(기록: {_scope} — {_scope_why})")
print(f"  범위: {_scope} — {_scope_why} · {_srcdesc} · 실행 {tot}")
bad = []
if tcc != tot:
    bad.append(f"G5: testcasecount({tcc}) != total({tot}) — 부분 실행이다. '전량'이라고 말할 수 없다.")
if minc and tot < minc:
    bad.append(f"G4: {tot}건만 돌았다(하한 {minc}) — 필터/컴파일 실패로 대부분이 실행되지 않았다.")
if prevn and tot < prevn:
    bad.append(f"G9: 직전 최대 {prevn}건({prevwho})보다 {prevn - tot}건 줄었다. "
               "어셈블리 하나가 컴파일에서 빠지면 정확히 이 형태로 사라진다 — "
               "테스트를 실제로 지운 라운드가 있으면 그 사실을 보고에 적고 넘어가라.")
fails = [tc for tc in r.iter('test-case') if tc.get('result') == 'Failed']
skips = [tc for tc in r.iter('test-case') if tc.get('result') == 'Skipped']
if fails:
    print(f"\n  ── 실패 {len(fails)}건 ──")
    for tc in fails:
        m = tc.find('./failure/message')
        msg = ' '.join((m.text or '').split())[:220] if m is not None else ''
        print(f"   ✗ {tc.get('fullname')}\n       {msg}")
if skips:
    print(f"\n  ── 건너뜀 {len(skips)}건 ──")
    for tc in skips:
        print(f"   · {tc.get('fullname')}")

# ★ 2026-09-14 R1/R2/R5 — docs/TEAM.md 「픽스처 끝에서 난 실패는 실패 개수에 안 들어간다」.
#   test-case만 세면 [OneTimeTearDown] 실패(site=TearDown)와 Inconclusive가 안 보인다.
v = NV.judge_root(r)
print(f"\n  test-run result={v['run_result']}")
print()
for ln in NV.format_lines(v):
    print("  " + ln)

for b in bad:
    print("\n✗ " + b)
if bad:
    sys.exit(1)                       # 측정 무효가 먼저다 — 무효한 측정의 초록/빨강은 말하지 않는다
sys.exit(0 if v['green'] else 3)
PY
}

# ---- 베이스라인 대조(귀속용) -------------------------------------------------
# ★ 이 역할의 존재 이유는 "누구 라운드가 무엇을 깼는지"를 그 라운드에 알리는 것이다.
#   전량 결과 두 개를 놓고 **새로 빨개진 것 / 초록으로 돌아온 것 / 사라진 것 / 새로 생긴 것**을 가른다.
compare() {   # $1=옛 xml  $2=새 xml
  [ -f "$1" ] || die "compare: 옛 결과 파일이 없다 — $1"
  [ -f "$2" ] || die "compare: 새 결과 파일이 없다 — $2"
  python3 - "$1" "$2" <<'PY'
import sys, os, datetime
import xml.etree.ElementTree as ET

# ★ 개명 흡수(2026-09-03 신설). 등록·검증된 개명만 «같은 테스트»로 본다.
#   등록되지 않은 이름 변경은 여전히 «삭제 1 + 신설 1»로 뜬다 — 그게 맞다.
sys.path.insert(0, os.path.join(os.environ['REGRESS_REPO'], 'docs/verify'))
try:
    import renames as _rn
    canon_full, _canon_short, _ok, _rej = _rn.load()
    _rn.banner(_ok, _rej)
except Exception as e:                      # 대장이 깨져도 대조 자체는 돌아야 한다
    print(f"\n⚠ 개명 대장을 읽지 못했다({e}) — 개명이 삭제+신설로 보일 것이다.")
    canon_full = lambda n: n
    _ok, _rej = [], []

import os
sys.path.insert(0, os.path.join(os.environ['REGRESS_REPO'], 'docs/verify'))
import nunit_verdict as NV

def load(p):
    r = ET.parse(p).getroot()
    d = {}
    for tc in r.iter('test-case'):
        fn = tc.get('fullname')
        if fn: d[canon_full(fn)] = tc.get('result')
    mt = datetime.datetime.fromtimestamp(os.stat(p).st_mtime).strftime('%m-%d %H:%M')
    return d, mt, int(r.get('total') or 0), NV.judge_root(r)
a, mta, ta, va = load(sys.argv[1])
b, mtb, tb, vb = load(sys.argv[2])
def verdict_word(v):
    return "초록" if v['green'] else "★빨강"
print(f"옛: {os.path.basename(sys.argv[1])} ({mta})  {ta}건  R1={verdict_word(va)} (run result={va['run_result']})")
print(f"새: {os.path.basename(sys.argv[2])} ({mtb})  {tb}건   Δ{tb - ta:+d}  R1={verdict_word(vb)} (run result={vb['run_result']})")
newred  = sorted(n for n in b if b[n] == 'Failed' and a.get(n) not in (None, 'Failed'))
fixed   = sorted(n for n in a if a[n] == 'Failed' and b.get(n) == 'Passed')
stayred = sorted(n for n in b if b[n] == 'Failed' and a.get(n) == 'Failed')
gone    = sorted(n for n in a if n not in b)
added   = sorted(n for n in b if n not in a)
addred  = [n for n in added if b[n] == 'Failed']
def show(title, items, limit=40):
    print(f"\n── {title} {len(items)}건 ──")
    for n in items[:limit]: print(f"   {n}")
    if len(items) > limit: print(f"   … 외 {len(items)-limit}건")
# ★ 2026-09-03 신설 — «초록 → 건너뜀»은 지금까지 이 대조에서 **보이지 않았다.**
#   CLAUDE.md가 경고한 조건부 Ignore(`ItemRarityDerivationTests.감사_코호트_안_등급_혼재를_잡는다`)가
#   조건이 참이 되는 날 조용히 건너뜀으로 바뀌는데, 실패 목록에도 신설 목록에도 안 뜬다.
#   **커버리지가 사라진 것인데 러너 요약은 「실패 0」이라 아무도 모른다.**
green2skip = sorted(n for n in b if b[n] == 'Skipped' and a.get(n) == 'Passed')
skip2green = sorted(n for n in b if b[n] == 'Passed' and a.get(n) == 'Skipped')

show("★ 새로 빨개짐(이번 라운드가 깼다)", newred)
show("★★ 초록 → 건너뜀 (조용한 커버리지 상실 — 실패 0에 가려진다)", green2skip)
show("건너뜀 → 초록 (되살아난 검사)", skip2green)
show("초록으로 돌아옴", fixed)
show("계속 빨감(이전부터)", stayred)
show("새로 생긴 테스트 중 빨감(신규 결함)", addred)

# ★ 2026-09-14 R5 — Inconclusive(Assume 실패)는 failed=에 안 들어가서 위 어느 목록에도 안 떴다.
#   실측: SettingsWindowReturnPathTests·TodoBoardDateNavigationTests 판정 불가 2건이 전량 요약에서 사라져 있었다.
pass2inc = sorted(n for n in b if b[n] == 'Inconclusive' and a.get(n) == 'Passed')
red2inc  = sorted(n for n in b if b[n] == 'Inconclusive' and a.get(n) == 'Failed')
inc2pass = sorted(n for n in b if b[n] == 'Passed' and a.get(n) == 'Inconclusive')
stayinc  = sorted(n for n in b if b[n] == 'Inconclusive' and a.get(n) == 'Inconclusive')
addinc   = sorted(n for n in added if b[n] == 'Inconclusive')
show("★★ 초록 → 판정 불가 (Assume 전제가 무너졌다 — 실패 0에 가려진다)", pass2inc)
show("★ 빨강 → 판정 불가 (고쳐진 게 아니라 전제에서 멈췄다)", red2inc)
show("판정 불가 → 초록", inc2pass)
show("계속 판정 불가", stayinc)
show("새로 생긴 테스트 중 판정 불가", addinc)

# ★ 2026-09-14 R1/R2 — 픽스처 수준 실패([OneTimeSetUp]/[OneTimeTearDown], site=SetUp/TearDown).
#   test-case 대조로는 구조적으로 안 보인다(mut-M5p.xml: failed=0인데 SetUpFixture가 TearDown에서 실패).
def fixkeys(v):
    return {f"{f['type']} {f['fullname']} @{f['site']}": f for f in v['fixture_failures']}
fa_, fb_ = fixkeys(va), fixkeys(vb)
show("★★ 새로 생긴 픽스처 수준 실패 (failed=에 안 잡힌다)", sorted(k for k in fb_ if k not in fa_))
show("계속되는 픽스처 수준 실패", sorted(k for k in fb_ if k in fa_))
show("사라진 픽스처 수준 실패", sorted(k for k in fa_ if k not in fb_))
if not vb['green']:
    print("\n✗ R1 — 새 결과는 초록이 아니다:")
    for x in vb['reasons']:
        print(f"   · {x}")

# ★ 2026-09-03 신설 — 「사라졌다」의 대부분은 사라진 게 아니다.
#   실측: PackPaletteGateTests의 [TestCase] **설명 문자열**이 한 글자 바뀌자
#   («요정날개·날개·종이비행기» → «요정날개·날개») fullname이 달라져 삭제 1 + 신설 1로 떴다.
#   이걸 조용히 흡수하면 진짜 삭제를 놓친다. 그래서 **흡수하지 않고 짝을 지어 이름을 붙인다.**
#     · 같은 메서드 + 같은 선두 인자  → 인자 설명만 바뀜(회귀 아님)
#     · 같은 클래스에서 1:1          → 개명 후보(renames.tsv에 등록하면 다음부터 흡수된다)
def head_of(n):
    return n.split('(', 1)[0]
def firstarg(n):
    if '(' not in n: return None
    inner = n[n.index('(') + 1:]
    return inner.split(',', 1)[0].strip()
pairs_arg, pairs_rename, gone_left, added_left = [], [], list(gone), list(added)
for g in list(gone_left):
    for a in list(added_left):
        if '(' in g and '(' in a and head_of(g) == head_of(a) and firstarg(g) == firstarg(a):
            pairs_arg.append((g, a)); gone_left.remove(g); added_left.remove(a); break
for g in list(gone_left):
    gcls = head_of(g).rsplit('.', 1)[0]
    cands = [a for a in added_left if head_of(a).rsplit('.', 1)[0] == gcls]
    if len(cands) == 1:
        pairs_rename.append((g, cands[0])); gone_left.remove(g); added_left.remove(cands[0])

show("사라진 테스트(지워졌거나 컴파일 안 됨)", gone)
if pairs_arg:
    print(f"\n── 그중 ⟨인자 설명만 바뀜⟩ {len(pairs_arg)}건 — 회귀 아님 ──")
    for g, a in pairs_arg[:20]:
        print(f"   {g}\n → {a}")
if pairs_rename:
    print(f"\n── 그중 ★⟨개명 후보⟩ {len(pairs_rename)}건 — 같은 클래스 1:1. "
          f"진짜 개명이면 docs/verify/renames.tsv에 등록해라 ──")
    for g, a in pairs_rename[:20]:
        print(f"   {g}\n → {a}")
if gone_left:
    print(f"\n── ★★ 짝이 없는 진짜 소멸 {len(gone_left)}건 — 여기만 보면 된다 ──")
    for n in gone_left[:40]:
        print(f"   {n}")

print(f"\n새로 생긴 테스트 총 {len(added)}건 / 사라진 테스트 총 {len(gone)}건")
print(f"  사라진 {len(gone)}건 내역: 인자설명변경 {len(pairs_arg)} / 개명후보 {len(pairs_rename)} "
      f"/ **짝없는 소멸 {len(gone_left)}**   (등록된 개명 {len(_ok)}건은 애초에 여기 세지 않는다)")
print(f"판정 불가: 옛 {len(va['inconclusive_cases'])}건 → 새 {len(vb['inconclusive_cases'])}건 · "
      f"픽스처 수준 실패: 옛 {len(fa_)}건 → 새 {len(fb_)}건")
# ★ 2026-09-14 — 새 결과가 R1 빨강이면 rc=3(옛 compare는 무엇이 빨개도 rc=0이었다).
sys.exit(0 if vb['green'] else 3)
PY
}

# ---- 자기검사: 가드가 실제로 무는가 -----------------------------------------
selfcheck() {
  # ★ 2026-09-27 — 규칙 29(출력에 **판 sha16**을 함께 찍어라). 한 실행이 편집을 가로지르면
  #   「무엇을 검사한 결과인지」가 출력에서 사라진다. 이 줄이 그 판을 고정한다 —
  #   상향 전 ✗와 상향 후 ✓를 나란히 놓을 때 **서로 다른 판이었다**는 반론을 이 줄이 막는다.
  echo "── 판 sha16: $(shasum -a 256 "${BASH_SOURCE[0]}" | cut -c1-16)  ($(basename "${BASH_SOURCE[0]}"))"
  local tmp; tmp=$(mktemp -d)
  local rc=0
  # ★ 2026-09-14 — 아래 합성 xml에 result="Passed"를 넣고, 거부 판정을 «rc≠0»에서 «rc=1(G가드)»로 좁혔다.
  #   R1이 생긴 뒤로 result 속성이 없는 xml은 **R1 때문에** rc=3이 난다 — 그러면 G5/G4/G3/G9가 죽어도
  #   이 대조들은 여전히 «거부했다»로 찍힌다(다른 절 덕에 빨개진 대조 = 죽은 대조).
  g_expect_invalid() {   # $1=설명 $2=가드 이름 $3...=report 인자 → rc=1(측정 무효)이어야 ✓
    local what="$1" g="$2" got; shift 2
    ( report "$@" ) >/dev/null 2>&1; got=$?
    if [ "$got" = 1 ]; then echo "  ✓ 거부했다($g, rc=1 측정 무효)"
    elif [ "$got" = 0 ]; then echo "  ✗ 통과해 버렸다 — $g가 물지 않는다."; rc=1
    else echo "  ✗ rc=$got — $g가 아니라 다른 이유(R1 등)로 빨개졌다. $g 생존을 증명하지 못한다."; rc=1; fi
  }
  echo "── 음성 대조 1: 부분 실행 xml(tcc != total)을 report가 거부하는가"
  cat > "$tmp/partial.xml" <<'X'
<test-run id="2" testcasecount="529" result="Passed" total="106" passed="103" failed="0" skipped="3" inconclusive="0"></test-run>
X
  g_expect_invalid "부분 실행" G5 "$tmp/partial.xml" 0 0

  # ★ 하한 숫자를 **여기에 다시 베끼지 않는다**(CLAUDE.md: 테스트에 프로덕션 상수를 베끼지 마라).
  #   실제 상수를 참조한다 — 안 그러면 하한을 올릴 때 이 대조만 낡는다.
  echo "── 음성 대조 2: 건수 미달 xml을 report가 거부하는가(하한 $MIN_EDIT_CASES 참조)"
  cat > "$tmp/tiny.xml" <<'X'
<test-run id="2" testcasecount="3" result="Passed" total="3" passed="3" failed="0" skipped="0" inconclusive="0"></test-run>
X
  g_expect_invalid "건수 미달" G4 "$tmp/tiny.xml" "$MIN_EDIT_CASES" 0

  echo "── 음성 대조 3: 낡은 파일(실행 시작보다 오래됨)을 거부하는가"
  cat > "$tmp/old.xml" <<'X'
<test-run id="2" testcasecount="1400" result="Passed" total="1400" passed="1400" failed="0" skipped="0" inconclusive="0"></test-run>
X
  local future=$(( $(date +%s) + 3600 ))
  g_expect_invalid "낡은 파일" G3 "$tmp/old.xml" 0 "$future"

  echo "── 음성 대조 4: 결과 파일이 아예 없으면 거부하는가"
  if ( report "$tmp/does-not-exist.xml" 0 0 ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 없는 파일을 초록으로 읽었다."; rc=1
  else echo "  ✓ 거부했다(G3/부재)"; fi

  echo "── 음성 대조 5: 다른 Unity가 돌고 있으면 실행을 거부하는가(G6)"
  if ( assert_no_unity_running ) >/dev/null 2>&1; then
    echo "  · 지금은 도는 Unity가 없어 이 대조는 판정 불가(미확인). 배치모드가 돌 때 다시 확인할 것."
  else
    echo "  ✓ 거부했다(G6) — 실제로 도는 Unity를 봤다."
  fi
  echo "── 음성 대조 5b: ★ G6 탐침이 «자기 자신»을 잡지 않는가(옛 pgrep -f 가 이걸로 죽었다)"
  # 자기 명령줄에 그 문자열을 넣고도 잡히면 안 된다.
  if ( UNITY_BIN_PATH="/bin/echo" assert_no_unity_running ) >/dev/null 2>&1; then
    echo "  · /bin/echo 로 바꿔도 -batchmode 인자를 가진 것이 없다 — 자기참조 없음(정상)"
  else
    echo "  ✗ 실행파일을 /bin/echo 로 바꿨는데 선점으로 판정했다 — 탐침이 엉뚱한 것을 센다."; rc=1
  fi
  echo "── 양성 대조 5c: ★ G6 탐침이 실제로 프로세스를 «셀 수» 있는가(0건이 죽은 프로브가 아님)"
  # 지금 확실히 도는 실행파일(로그인 셸)로 바꿔 세어 본다. args 조건을 -batchmode 대신 흔한 문자열로 둘 수 없으므로
  # 여기서는 후보 수집 단계만 검증한다.
  local shellhits
  shellhits=$(ps -axo pid=,comm= | awk '$0 ~ /\/bin\/(ba|z)sh/ {print $1}' | wc -l | tr -d ' ')
  if [ "$shellhits" -gt 0 ]; then
    echo "  ✓ comm 기반 수집이 셸 ${shellhits}개를 센다 — 이 탐침의 0건은 '비었다'는 뜻이다"
  else
    echo "  ✗ comm 기반 수집이 아무것도 못 센다 — G6의 0건은 '프로브가 죽었다'와 구분되지 않는다."; rc=1
  fi

  # ★ 2026-09-02 신설 — 여기부터가 이번에 뚫려 있던 자리다.
  echo "── 음성 대조 6: -quit이 인자에 있으면 거부하는가(G1이 주석이 아니라 코드인가)"
  if ( assert_launch_args -batchmode -runTests -quit ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — G1은 여전히 주석일 뿐이다."; rc=1
  else echo "  ✓ 거부했다(G1)"; fi

  echo "── 음성 대조 7: -testFilter 값에 콤마가 있으면 거부하는가(G2)"
  if ( assert_launch_args -batchmode -runTests -testFilter "A,B" ) >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — G2는 여전히 주석일 뿐이다."; rc=1
  else echo "  ✓ 거부했다(G2)"; fi

  echo "── 양성 대조 A: 정상 인자는 통과하는가"
  if ( assert_launch_args -batchmode -nographics -runTests -testPlatform EditMode ) >/dev/null 2>&1; then
    echo "  ✓ 통과했다(G1/G2 양성 대조)"
  else echo "  ✗ 정상 인자를 거부했다 — G1/G2가 과잉이다."; rc=1; fi

  # ★★ 이 두 대조는 "UNKNOWN이면 통과"로 짜면 **거짓 통과한다**. 실제로 그렇게 짰다가 잡혔다:
  #    시험 대상(OSX 마커)과 무관하게 WIN 마커가 먼저 걸려 UNKNOWN이 나왔는데도 ✓가 찍혔다.
  #    그래서 **사유 문자열이 시험 중인 마커 이름과 기대 가드 번호를 담고 있는지**까지 본다.
  echo "── 음성 대조 8: ★ 죽은 마커를 쓰면 UNKNOWN이 나오는가(G7a — 이번에 실제로 뚫려 있던 구멍)"
  local saved="$MARKER_OSX"
  MARKER_OSX=MacOverlayWindow   # 실재하지 않는 옛 마커. 예전 구현은 이걸로 조용히 초록이었다.
  local t; t=$(active_target)
  MARKER_OSX="$saved"
  case "$t" in
    *"G7a"*"MacOverlayWindow"*) echo "  ✓ 거부했다(G7a, 사유가 그 마커를 지목한다) — [$t]" ;;
    UNKNOWN*) echo "  ✗ UNKNOWN이지만 사유가 MacOverlayWindow가 아니다 — 다른 이유로 빨개진 것이다: [$t]"; rc=1 ;;
    *) echo "  ✗ 죽은 마커인데 [$t]라고 단정했다 — G7a가 물지 않는다."; rc=1 ;;
  esac

  echo "── 음성 대조 9: 게이트 밖 문자열에 오염된 마커를 쓰면 UNKNOWN이 나오는가(G7b)"
  saved="$MARKER_OSX"
  MARKER_OSX=MacWindowService   # Core/StickConfig.cs(게이트 없음)의 [Tooltip] 문자열에 이름이 있다.
  t=$(active_target)
  MARKER_OSX="$saved"
  case "$t" in
    *"G7b"*"MacWindowService"*) echo "  ✓ 거부했다(G7b, 사유가 그 마커를 지목한다) — [$t]" ;;
    UNKNOWN*) echo "  ✗ UNKNOWN이지만 사유가 MacWindowService가 아니다 — 다른 이유로 빨개진 것이다: [$t]"; rc=1 ;;
    *) echo "  ✗ 오염된 마커인데 [$t]라고 단정했다 — G7b가 물지 않는다."; rc=1 ;;
  esac

  echo "── 양성 대조 B: 지금 마커로는 실제 타깃이 나오는가(위 두 대조가 '항상 UNKNOWN'이 아님을 증명)"
  t=$(active_target)
  case "$t" in
    UNITY_STANDALONE_*) echo "  ✓ 판정됐다 — [$t]" ;;
    *) echo "  ✗ [$t] — 지금 마커로도 판정이 안 된다. G7 전체가 무의미하다."; rc=1 ;;
  esac

  echo "── 음성 대조 10: G9(직전 실행 대비 감소)가 무는가"
  cat > "$tmp/shrunk.xml" <<'X'
<test-run id="2" testcasecount="1400" result="Passed" total="1400" passed="1400" failed="0" skipped="0" inconclusive="0"></test-run>
X
  g_expect_invalid "직전 대비 209건 감소" G9 "$tmp/shrunk.xml" 0 0 1609 "b2-bake_edit.xml"

  echo "── 음성 대조 11: ★ G4a가 낡은 하한을 신고하는가(2026-09-03 신설 — 실제로 하루 낡아 있었다)"
  local g4a
  g4a=$(stale_floor_report 1390 1800 "가상_직전.xml" edit)
  case "$g4a" in
    *"G4a: 정적 하한이 낡았다"*) echo "  ✓ 신고했다 — $(echo "$g4a" | head -1)" ;;
    *) echo "  ✗ 하한 1390 / 직전 1800인데 신고하지 않았다: [$g4a]"; rc=1 ;;
  esac
  echo "── 양성 대조 D: G4a가 «항상 신고»가 아닌가(지금 하한으로는 조용해야 한다)"
  g4a=$(stale_floor_report "$MIN_EDIT_CASES" 1800 "가상_직전.xml" edit)
  case "$g4a" in
    *"노후 아님"*) echo "  ✓ 조용하다 — $g4a" ;;
    *) echo "  ✗ 지금 하한($MIN_EDIT_CASES)도 노후로 신고한다 — 하한을 올려라: [$g4a]"; rc=1 ;;
  esac

  # ★★ 2026-09-27 qa-regression 신설 — 위 두 대조는 **합성 숫자**(1390/1800)로 «G4a가 무는가»만
  #   본다. 출하된 하한이 **실제로** 낡았는지는 한 번도 묻지 않았고, 그래서 play 하한 705가
  #   실측 827 앞에서 122건을, edit 하한 2592가 실측 3206 앞에서 614건을 조용히 통과시키는
  #   상태로 넘어왔다. 이 대조는 **실행이 아니라 selfcheck에서만** 문다 — 테스트를 정당하게
  #   지운 라운드를 막지 않고, 대신 조용한 채로 남지 못하게 한다.
  echo "── 양성 대조 E: ★ 출하된 하한이 디스크 최신 실행 대비 낡지 않았는가(양 모드)"
  local sm spb spbn spbwho sfloor
  for sm in edit play; do
    spb=$(prev_best "$sm" ""); spbn=${spb%% *}; spbwho=${spb#* }
    case "$sm" in edit) sfloor=$MIN_EDIT_CASES ;; play) sfloor=$MIN_PLAY_CASES ;; esac
    if [ "${spbn:-0}" -le 0 ]; then
      echo "  · $sm: docs/verify/runs/ 에 실행이 없다 — 판정 불가(미확인). 실행 이력이 없는 트리에서는 정적 하한이 유일한 방어선이다."
    else
      g4a=$(stale_floor_report "$sfloor" "$spbn" "$spbwho" "$sm")
      case "$g4a" in
        *"G4a: 정적 하한이 낡았다"*)
          echo "  ✗ $sm 하한 $sfloor / 실측 최대 $spbn($spbwho) — $(( spbn - sfloor ))건이 조용히 사라져도 G4가 초록이다. MIN_$(echo "$sm" | tr 'a-z' 'A-Z')_CASES를 올려라."
          rc=1 ;;
        *) echo "  ✓ $sm 하한 $sfloor / 실측 최대 $spbn($spbwho) — 노후 아님" ;;
      esac
    fi
  done

  echo "── 음성 대조 12: ★ G8이 실제 컴파일 실패 로그를 잡는가(2026-09-03: 옛 마커는 죽어 있었다)"
  cat > "$tmp/fail.log" <<'X'
Assets/_Project/Scripts/States/ThrowTumbleState.cs(247,27): error CS0103: The name 'ResolveThrowStrength01' does not exist in the current context
## Script Compilation Error for: Csc Library/Bee/artifacts/200b0aE.dag/StickMate.Runtime.dll (+2 others)
AssetDatabase: script compilation time: 2.129521s
Scripts have compiler errors.
X
  if log_compile_failed "$tmp/fail.log"; then echo "  ✓ 잡았다(G8)"
  else echo "  ✗ 실제 실패 로그의 문장을 그대로 넣었는데 못 잡았다 — G8이 죽은 프로브다."; rc=1; fi

  echo "── 양성 대조 F: G8이 «항상 실패»가 아닌가 — 정상 로그를 실패로 오인하지 않는가"
  local goodlog cleanhits=0 goodn=0
  for goodlog in "$OUTDIR"/te-r2_edit.log "$OUTDIR"/qa-r6_edit.log "$OUTDIR"/te-r2_play.log; do
    [ -f "$goodlog" ] || continue
    goodn=$((goodn+1))
    log_compile_failed "$goodlog" || cleanhits=$((cleanhits+1))
  done
  if [ "$goodn" -eq 0 ]; then
    echo "  · 대조할 과거 정상 로그가 디스크에 없다 — 판정 불가(미확인)."
  elif [ "$cleanhits" -eq "$goodn" ]; then
    echo "  ✓ 과거 정상 로그 ${goodn}건 전부 '실패 아님'으로 읽었다"
  else
    echo "  ✗ 정상 로그 $((goodn - cleanhits))건을 컴파일 실패로 오인했다 — 마커가 과잉이다."; rc=1
  fi

  echo "── 음성 대조 13: 존재하지 않는 로그는 '실패'로 단정하지 않는가"
  if log_compile_failed "$tmp/no-such.log"; then
    echo "  ✗ 없는 파일을 컴파일 실패로 읽었다."; rc=1
  else echo "  ✓ 단정하지 않는다"; fi

  echo "── 양성 대조 E: 개명 대장 검증기가 스스로 초록인가"
  if python3 "$REPO/docs/verify/renames.py" --check >/dev/null 2>&1; then
    echo "  ✓ renames.py --check 통과"
  else
    echo "  ✗ renames.py --check 실패 — 개명 흡수가 검증되지 않은 채 돌고 있다."; rc=1
  fi

  # ==========================================================================
  # ★★ G10 대조 (2026-09-03 신설) — 세이브 격리 게이트 회계
  # ==========================================================================
  # 이 가드는 **"게이트가 실행 끝까지 살아 있었는가"**를 유일하게 재는 자리다.
  # 그래서 대조를 넉넉히 붙인다 — 여기가 조용히 죽으면 아무도 모른다.
  gate_probe_xml() {   # $1=경로 $2=건수  (tcc=total=건수)
    cat > "$1" <<X
<test-run id="2" testcasecount="$2" total="$2" passed="$2" failed="0" skipped="0" inconclusive="0"></test-run>
X
  }
  gate_probe_log() {   # $1=경로 $2=leaf $3=purge $4=skip $5=removed $6=fail
    printf '%s\n' \
      "[테스트격리] 아무 줄" \
      "[세이브게이트] 실행 종료 — 리프 테스트 $2건, 정리 시도 $3회, 삭제 $5개, 건너뜀 $4회(마지막 사유: 없음), 실패 $6건." \
      "[세이브게이트] 회계 leaf=$2 purge=$3 skip=$4 removed=$5 fail=$6" > "$1"
  }

  gate_probe_xml "$tmp/g10.xml" 591

  echo "── 양성 대조 G: G10이 **정상 PlayMode 쌍**을 통과시키는가(이게 빨강이면 아래 음성 대조는 전부 무의미)"
  gate_probe_log "$tmp/g10_ok.log" 591 591 0 77 0
  if gate_audit "$tmp/g10_ok.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✓ 통과했다"
  else echo "  ✗ 정상 쌍을 거부했다 — G10이 과잉이다."; gate_audit "$tmp/g10_ok.log" "$tmp/g10.xml" play; rc=1; fi

  echo "── 양성 대조 H: G10이 **정상 EditMode 쌍**(전량 건너뜀)을 통과시키는가"
  gate_probe_xml "$tmp/g10e.xml" 1800
  gate_probe_log "$tmp/g10e_ok.log" 1800 0 1800 0 0
  if gate_audit "$tmp/g10e_ok.log" "$tmp/g10e.xml" edit >/dev/null 2>&1; then
    echo "  ✓ 통과했다"
  else echo "  ✗ 정상 EditMode 쌍을 거부했다."; gate_audit "$tmp/g10e_ok.log" "$tmp/g10e.xml" edit; rc=1; fi

  echo "── 음성 대조 14: 회계 항등식이 깨지면(leaf != purge+skip) 거부하는가"
  gate_probe_log "$tmp/g10_id.log" 591 500 0 77 0
  if gate_audit "$tmp/g10_id.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 항등식 가드가 없다."; rc=1
  else echo "  ✓ 거부했다(항등식)"; fi

  echo "── 음성 대조 15: ★ 게이트가 도중에 죽은 형태(leaf 591인데 정리 500회 + 건너뜀 91회)를 거부하는가"
  # 이것이 **자기 반려 1번이 말한 바로 그 실패**다 — 항등식은 맞지만 play에서 skip>0이면
  # 그 91개 리프는 앞 테스트의 저장 파일을 물려받은 채 돌았다.
  gate_probe_log "$tmp/g10_die.log" 591 500 91 77 0
  if gate_audit "$tmp/g10_die.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — **게이트가 500번째 리프에서 죽어도 초록**이던 그 상태 그대로다."; rc=1
  else echo "  ✓ 거부했다(play는 skip==0)"; fi

  echo "── 음성 대조 16: 삼킨 예외(fail>0)를 거부하는가"
  gate_probe_log "$tmp/g10_fail.log" 591 591 0 77 3
  if gate_audit "$tmp/g10_fail.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 예외 3건을 삼켰는데 초록이다."; rc=1
  else echo "  ✓ 거부했다(fail==0)"; fi

  echo "── 음성 대조 17: ★ 러너 xml과 어긋나면(leaf != total) 거부하는가 — **이 대조가 G10의 값어치다**"
  gate_probe_log "$tmp/g10_drift.log" 424 424 0 40 0   # 424 = 게이트 테스트가 도는 시점의 실측 중간값
  if gate_audit "$tmp/g10_drift.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 게이트 424 vs 러너 591인데 초록이다(독립 대조가 죽었다)."; rc=1
  else echo "  ✓ 거부했다(leaf != total/testcasecount)"; fi

  echo "── 음성 대조 18: 회계 줄이 아예 없으면 거부하는가(콜백이 안 붙은 형태)"
  printf '%s\n' "[테스트격리] 아무 줄" "다른 로그" > "$tmp/g10_none.log"
  if gate_audit "$tmp/g10_none.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — 줄이 없는데 초록이다(0건을 '깨끗'으로 읽었다)."; rc=1
  else echo "  ✓ 거부했다(줄 부재)"; fi

  echo "── 음성 대조 19: ★ 값이 빈 정규형(leaf= purge= …)을 거부하는가 — TEAM.md 거짓 통과 13번째"
  printf '%s\n' "[세이브게이트] 회계 leaf= purge= skip= removed= fail=" > "$tmp/g10_empty.log"
  if gate_audit "$tmp/g10_empty.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — **형태만 맞고 값이 비었는데** 초록이다."; rc=1
  else echo "  ✓ 거부했다(정규형/값 채움)"; fi

  echo "── 음성 대조 20: EditMode인데 게이트가 실제로 지웠으면(purge>0) 거부하는가"
  gate_probe_log "$tmp/g10e_bad.log" 1800 5 1795 5 0
  if gate_audit "$tmp/g10e_bad.log" "$tmp/g10e.xml" edit >/dev/null 2>&1; then
    echo "  ✗ 통과해 버렸다 — EditMode 전량 건너뜀 설계가 지켜지지 않는데 초록이다."; rc=1
  else echo "  ✓ 거부했다(edit는 purge==0)"; fi

  echo "── 음성 대조 21: 로그가 없으면 '통과'로 읽지 않는가"
  if gate_audit "$tmp/no-such-gate.log" "$tmp/g10.xml" play >/dev/null 2>&1; then
    echo "  ✗ 없는 로그를 초록으로 읽었다."; rc=1
  else echo "  ✓ 거부했다(로그 부재)"; fi

  # ★ 실측 교정 — 합성만으로는 "실제 러너가 뱉는 줄"을 읽는지 알 수 없다.
  #   정규형 줄이 들어 있는 **진짜 실행 기록**이 디스크에 있으면 그것으로 교정한다.
  #   없으면 **미확인**이라고 쓴다("통과"라고 쓰지 않는다).
  echo "── 실측 교정: 정규형 줄이 들어 있는 실제 실행 기록으로 G10을 교정한다"
  local realn=0 realok=0 rl rx rmode
  for rl in "$OUTDIR"/*_play.log "$OUTDIR"/*_edit.log; do
    [ -f "$rl" ] || continue
    grep -qE '\[세이브게이트\] 회계 leaf=[0-9]+ purge=[0-9]+ skip=[0-9]+ removed=[0-9]+ fail=[0-9]+' "$rl" || continue
    rx="${rl%.log}.xml"; [ -f "$rx" ] || continue
    case "$rl" in *_play.log) rmode=play ;; *) rmode=edit ;; esac
    realn=$((realn+1))
    if gate_audit "$rl" "$rx" "$rmode" >/dev/null 2>&1; then realok=$((realok+1))
    else echo "  ✗ 실제 기록을 거부했다: $(basename "$rl")"; gate_audit "$rl" "$rx" "$rmode"; rc=1; fi
  done
  if [ "$realn" -eq 0 ]; then
    echo "  · 정규형 줄이 든 실제 기록이 아직 없다 — **미확인**(러너를 한 번 돌린 뒤 다시 확인할 것)."
  else
    echo "  ✓ 실제 기록 ${realok}/${realn}건이 G10을 통과했다(합성이 아니라 러너가 뱉은 줄로 교정)."
  fi

  # ==========================================================================
  # ★★ R1/R2/R5 대조 (2026-09-14 신설) — 결과 판정 (docs/TEAM.md 「픽스처 끝에서 난 실패는…」)
  # ==========================================================================
  # 옛 selfcheck의 합성 xml은 test-run에 result 속성조차 없었다 = 이 형태를 구조적으로 못 쟀다.
  # ★ 합성본마다 규칙 1의 **절 하나만** 깬다 — 한 대조가 다른 절 덕분에 빨개져서 «물었다»로 보이지 않게.
  # ★ rc만 보지 않는다 — 기대 rc **그리고** 그 절을 지목하는 사유 문자열이 출력에 있어야 ✓다
  #   (TEAM.md: 「양성 대조가 '에러가 났다'만 보면 죽는다」).
  r1_make() {   # $1=경로 $2=test-run 속성 $3=SetUpFixture 속성 $4=TestFixture 속성 $5 $6 $7=케이스 A/B/C 결과
    cat > "$1" <<X
<test-run id="2" testcasecount="3" total="3" $2>
  <test-suite type="SetUpFixture" name="Probe.dll" fullname="ProbeIsolationFixture" $3>
    <failure><message><![CDATA[TearDown : NUnit.Framework.AssertionException : 합성 탐침 — 픽스처 끝 단언 실패 <test-suite site="TearDown"> 모양 글자]]></message></failure>
    <test-suite type="TestFixture" name="ProbeFixture" fullname="StickMate.Tests.Probe.ProbeFixture" $4>
      <test-case name="A" fullname="StickMate.Tests.Probe.ProbeFixture.판정탐침_A" result="$5"><reason><message><![CDATA[합성 사유 A]]></message></reason></test-case>
      <test-case name="B" fullname="StickMate.Tests.Probe.ProbeFixture.판정탐침_B" result="$6"><reason><message><![CDATA[합성 사유 B]]></message></reason></test-case>
      <test-case name="C" fullname="StickMate.Tests.Probe.ProbeFixture.판정탐침_C" result="$7"><reason><message><![CDATA[합성 사유 C]]></message></reason></test-case>
    </test-suite>
  </test-suite>
</test-run>
X
  }
  r1_expect() {   # $1=설명 $2=xml $3=기대 rc $4..=출력에 반드시 있어야 할 문자열
    local what="$1" x="$2" want="$3" o got miss=""; shift 3
    o=$( report "$x" 0 0 2>&1 ); got=$?
    for t in "$@"; do printf '%s\n' "$o" | grep -qF -- "$t" || miss="$miss [$t]"; done
    if [ "$got" = "$want" ] && [ -z "$miss" ]; then
      echo "  ✓ $what — rc=$got, 사유 문자열 확인"
    else
      echo "  ✗ $what — rc=$got(기대 $want), 출력에 없는 문자열:${miss:- 없음}"
      printf '%s\n' "$o" | tail -12 | sed 's/^/      /'; rc=1
    fi
  }
  local P3='passed="3" failed="0" inconclusive="0" skipped="0"'
  r1_make "$tmp/r1_ok.xml"        "result=\"Passed\" $P3"        'result="Passed"' 'result="Passed"' Passed Passed Passed
  r1_make "$tmp/r1_td.xml"        "result=\"Failed(Child)\" $P3" 'result="Failed" label="Error" site="TearDown"' 'result="Passed"' Passed Passed Passed
  r1_make "$tmp/r1_tdign.xml"     'result="Failed(Child)" passed="2" failed="0" inconclusive="0" skipped="1"' \
                                  'result="Failed" label="Ignored" site="TearDown"' 'result="Skipped" label="Ignored"' Passed Passed Skipped
  r1_make "$tmp/r1_siteonly.xml"  "result=\"Passed\" $P3"        'result="Passed" site="SetUp"' 'result="Passed"' Passed Passed Passed
  r1_make "$tmp/r1_runonly.xml"   "result=\"Failed(Child)\" $P3" 'result="Passed"' 'result="Passed"' Passed Passed Passed
  r1_make "$tmp/r1_suiteonly.xml" "result=\"Passed\" $P3"        'result="Passed"' 'result="Failed" site="Child"' Passed Passed Passed
  r1_make "$tmp/r1_inc.xml"       'result="Passed" passed="2" failed="0" inconclusive="1" skipped="0"' \
                                  'result="Passed"' 'result="Passed"' Passed Inconclusive Passed
  r1_make "$tmp/r1_fail.xml"      'result="Failed(Child)" passed="2" failed="1" inconclusive="0" skipped="0"' \
                                  'result="Failed" site="Child"' 'result="Failed" site="Child"' Passed Failed Passed
  r1_make "$tmp/r1_nores.xml"     "$P3"                          'result="Passed"' 'result="Passed"' Passed Passed Passed

  echo "── R1 정상(음성) 대조: 규칙 1을 전부 지키는 xml은 초록·rc=0인가(이게 빨강이면 아래 탐지는 전부 무의미)"
  r1_expect "정상 xml" "$tmp/r1_ok.xml" 0 "✓ R1 초록"
  echo "── R1 탐지(양성) 대조 1: ★ 이번에 뚫려 있던 형태 — Failed(Child) + SetUpFixture site=TearDown + failed=0"
  r1_expect "픽스처 끝 실패(label=Error)" "$tmp/r1_td.xml" 3 "✗ R1 빨강" "ProbeIsolationFixture" "site=TearDown"
  echo "── R1 탐지(양성) 대조 2: ★ R2 — 같은 형태의 label=\"Ignored\" 변종(label로 거르면 놓친다)"
  r1_expect "픽스처 끝 실패(label=Ignored)" "$tmp/r1_tdign.xml" 3 "✗ R1 빨강" "site=TearDown" "label=Ignored"
  echo "── R1 탐지(양성) 대조 3: 절 3만 단독으로 — test-run은 Passed인데 스위트 site=SetUp"
  r1_expect "site 절 단독" "$tmp/r1_siteonly.xml" 3 "site=SetUp"
  echo "── R1 탐지(양성) 대조 4: 절 2만 단독으로 — 스위트는 멀쩡한데 test-run result=Failed(Child)"
  r1_expect "run result 절 단독" "$tmp/r1_runonly.xml" 3 "test-run result=Failed(Child)"
  echo "── R1 탐지(양성) 대조 5: 절 4만 단독으로 — test-run Passed인데 Failed 스위트 1개"
  r1_expect "Failed 스위트 절 단독" "$tmp/r1_suiteonly.xml" 3 "Failed로 시작하는 test-suite 1개"
  echo "── R1 탐지(양성) 대조 6: 평범한 테스트 실패도 이제 rc≠0인가(옛 report는 rc=0이었다)"
  r1_expect "테스트 케이스 실패" "$tmp/r1_fail.xml" 3 "판정탐침_B" "failed=1"
  echo "── R1 탐지(양성) 대조 7: test-run에 result 속성이 없으면 초록이라고 하지 않는가"
  r1_expect "result 속성 부재" "$tmp/r1_nores.xml" 3 "result 속성이 없다"
  echo "── R5 대조: 판정 불가(Inconclusive)는 **이름**이 찍히는가 — 규칙 1상 초록(rc=0)은 유지"
  r1_expect "판정 불가 이름 출력" "$tmp/r1_inc.xml" 0 "판정 불가(Inconclusive) 1건" "판정탐침_B" "✓ R1 초록"

  echo "── R5 compare 대조: «초록 → 판정 불가» · «빨강 → 판정 불가» · 새 픽스처 실패가 제 칸에 뜨고 rc=3인가"
  r1_make "$tmp/cmp_old.xml" 'result="Failed(Child)" passed="2" failed="1" inconclusive="0" skipped="0"' \
                             'result="Failed" site="Child"' 'result="Failed" site="Child"' Passed Failed Passed
  r1_make "$tmp/cmp_new.xml" 'result="Failed(Child)" passed="1" failed="0" inconclusive="2" skipped="0"' \
                             'result="Failed" label="Error" site="TearDown"' 'result="Passed"' Inconclusive Inconclusive Passed
  local co cr cmiss=""
  co=$( compare "$tmp/cmp_old.xml" "$tmp/cmp_new.xml" 2>&1 ); cr=$?
  sect_has() { printf '%s\n' "$co" | grep -A1 -F -- "$1" | tail -1 | grep -qF -- "$2"; }
  sect_has "초록 → 판정 불가 (" "판정탐침_A"               || cmiss="$cmiss [초록→판정불가:A]"
  sect_has "빨강 → 판정 불가 (" "판정탐침_B"               || cmiss="$cmiss [빨강→판정불가:B]"
  sect_has "새로 생긴 픽스처 수준 실패" "ProbeIsolationFixture" || cmiss="$cmiss [새 픽스처 실패]"
  if [ "$cr" = 3 ] && [ -z "$cmiss" ]; then echo "  ✓ 세 전이가 제 칸에 떴고 rc=3"
  else echo "  ✗ rc=$cr(기대 3), 제 칸에 안 뜬 것:${cmiss:- 없음}"; printf '%s\n' "$co" | tail -30 | sed 's/^/      /'; rc=1; fi
  echo "── R5 compare 정상(음성) 대조: 같은 정상 xml끼리는 rc=0이고 전이 칸이 0건인가"
  co=$( compare "$tmp/r1_ok.xml" "$tmp/r1_ok.xml" 2>&1 ); cr=$?
  if [ "$cr" = 0 ] && printf '%s\n' "$co" | grep -qF "실패 0에 가려진다) 0건" \
     && printf '%s\n' "$co" | grep -qF "새로 생긴 픽스처 수준 실패 (failed=에 안 잡힌다) 0건"; then
    echo "  ✓ rc=0, 전이 0건"
  else echo "  ✗ rc=$cr — 정상끼리 대조가 빨갛거나 칸이 안 찍혔다"; printf '%s\n' "$co" | tail -20 | sed 's/^/      /'; rc=1; fi

  # ★ 실측 교정 — 러너가 실제로 뱉은 xml. 없으면 **미확인**이라고 쓴다(✓라고 쓰지 않는다).
  echo "── R1 실측 교정: 실제 러너 xml로 맞춘다(TEAM.md 규칙 3)"
  local rx_m5p="$REPO/Logs/coder-onbstore/mut-M5p.xml" rx_edit="$REPO/Logs/coder-onbstore/edit-full.xml"
  local rx_play="$REPO/Logs/coder-onbstore/play-full.xml" rx_setup="$OUTDIR/ledgehang-GREEN_edit.xml"
  if [ -f "$rx_m5p" ]; then r1_expect "실측 mut-M5p.xml(failed=0, Unity rc 0) → 빨강" "$rx_m5p" 3 "GlobalPlayModeTestIsolation" "site=TearDown"
  else echo "  · mut-M5p.xml 없음 — 미확인. 이 교정 없이 낸 R1 초록은 믿지 마라."; fi
  if [ -f "$rx_edit" ]; then r1_expect "실측 edit-full.xml(3141건, Skipped:Ignored) → 초록" "$rx_edit" 0 "✓ R1 초록"
  else echo "  · edit-full.xml 없음 — 미확인."; fi
  if [ -f "$rx_play" ]; then r1_expect "실측 play-full.xml → 빨강 + 판정 불가 2건 이름" "$rx_play" 3 "판정 불가(Inconclusive) 2건" \
       "ClickingOutsideSettingsNeitherClosesItNorReturnsTheInfoWindow" "ClickingACalendarCellPicksTheDayInsteadOfDraggingTheWindow"
  else echo "  · play-full.xml 없음 — 미확인."; fi
  if [ -f "$rx_setup" ]; then r1_expect "실측 ledgehang-GREEN_edit.xml([OneTimeSetUp] 실패) → 빨강" "$rx_setup" 3 "site=SetUp"
  else echo "  · ledgehang-GREEN_edit.xml 없음 — 미확인."; fi

  # ==========================================================================
  # ★★ 부분 실행 배너 대조 (2026-09-14 — docs/verify/PLAYMODE_RED7_FIX_SPEC.md §8 1안, 리더 채택)
  # ==========================================================================
  # G5는 필터 실행을 못 본다(mut-M5p.xml: tcc=3 total=3). 배너는 **rc를 바꾸지 않는다** — 그래서 rc와 함께
  # «있어야 할 문구»와 «있으면 안 될 문구»를 둘 다 본다(배너가 늘 뜨거나 늘 안 뜨는 죽은 장치를 가른다).
  b_expect() {   # $1=설명 $2=xml $3=기대 rc $4=있어야 할 문구(| 구분) $5=있으면 안 될 문구(| 구분, 빈 값 허용)
    local what="$1" x="$2" want="$3" has="$4" hasnot="$5" o got miss="" t
    o=$( report "$x" 0 0 2>&1 ); got=$?
    local -a hs hn
    IFS='|' read -r -a hs <<< "$has"
    IFS='|' read -r -a hn <<< "$hasnot"
    # ★ macOS /bin/bash 3.2 + set -u 에서 빈 배열 "${a[@]}"는 unbound 오류로 selfcheck 전체를 죽인다(2026-09-14 실측).
    for t in ${hs[@]+"${hs[@]}"}; do [ -z "$t" ] || printf '%s\n' "$o" | grep -qF -- "$t" || miss="$miss [없음:$t]"; done
    for t in ${hn[@]+"${hn[@]}"}; do [ -z "$t" ] || ! printf '%s\n' "$o" | grep -qF -- "$t" || miss="$miss [있으면 안 됨:$t]"; done
    if [ "$got" = "$want" ] && [ -z "$miss" ]; then echo "  ✓ $what — rc=$got"
    else echo "  ✗ $what — rc=$got(기대 $want)$miss"; printf '%s\n' "$o" | grep -E "범위|부분 실행|⚠⚠" | sed 's/^/      /'; rc=1; fi
  }
  b_make() {   # $1=경로 — platform=PlayMode, 3건 전부 통과(R1 초록)
    cat > "$1" <<X
<test-run id="2" testcasecount="3" total="3" result="Passed" passed="3" failed="0" inconclusive="0" skipped="0">
  <test-suite type="TestSuite" name="StickMate" fullname="StickMate" testcasecount="3" total="3" result="Passed">
    <properties><property name="platform" value="PlayMode" /></properties>
    <test-case name="A" fullname="StickMate.Tests.Probe.배너탐침_A" result="Passed" />
    <test-case name="B" fullname="StickMate.Tests.Probe.배너탐침_B" result="Passed" />
    <test-case name="C" fullname="StickMate.Tests.Probe.배너탐침_C" result="Passed" />
  </test-suite>
</test-run>
X
  }
  b_log() {   # $1=xml 경로 $2=필터 값(빈 값이면 필터 인자 없음) — Unity 로그 머리 모양 그대로(한 줄에 인자 하나)
    {
      echo "[Licensing::Module] 합성 머리말"; echo; echo "COMMAND LINE ARGUMENTS:"
      echo "/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity"
      printf '%s\n' -batchmode -nographics -runTests -testPlatform PlayMode
      if [ -n "$2" ]; then printf '%s\n' -testFilter "$2"; fi
      printf '%s\n' -testResults "$1" -logFile "${1%.xml}.log"
      echo "Successfully changed project path to: /probe"
    } > "${1%.xml}.log"
  }
  b_make "$tmp/b_part.xml";    b_log "$tmp/b_part.xml" "^StickMate\\.Tests\\.Probe\\."
  b_make "$tmp/b_short.xml"
  b_make "$tmp/b_fulllog.xml"; b_log "$tmp/b_fulllog.xml" ""

  echo "── 배너 탐지(양성) 1: 로그 명령줄에 -testFilter가 있으면 «부분 실행» — 그리고 R1 초록 rc=0은 그대로인가"
  b_expect "필터 로그 + 초록 xml" "$tmp/b_part.xml" 0 "★★ 부분 실행 3/|로그 명령줄 -testFilter|✓ R1 초록" ""
  # ★ 2026-09-14 후속 ② — 필터 인자가 명령줄 **뒤쪽(13번째)**에 있는 로그. 위 대조는 7번째, 실측 M5p는 9번째라
  #   «앞 N개 인자만 읽는» 결함(verify-change 생존 결함 mB)이 실측 M5p 한 파일에만 기대어 잡혔다.
  b_log_late() {   # $1=xml 경로 — 인자 12개를 먼저 두고 -testFilter를 13번째에 둔다
    {
      echo "[Licensing::Module] 합성 머리말"; echo; echo "COMMAND LINE ARGUMENTS:"
      echo "/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity"
      printf '%s\n' -batchmode -nographics -projectPath /probe -runTests -testPlatform PlayMode \
        -testResults "$1" -logFile "${1%.xml}.log" -testFilter "^StickMate\\.Tests\\.Probe\\.Late\\."
      echo "Successfully changed project path to: /probe"
    } > "${1%.xml}.log"
  }
  b_make "$tmp/b_late.xml"; b_log_late "$tmp/b_late.xml"
  echo "── 배너 탐지(양성) 1b: 필터 인자가 명령줄 뒤쪽(13번째)에 있어도 «부분 실행»인가(앞 N개만 읽는 결함 대조)"
  b_expect "뒤쪽 필터 로그 + 초록 xml" "$tmp/b_late.xml" 0 "★★ 부분 실행 3/|로그 명령줄 -testFilter ^StickMate" ""
  echo "── 배너 탐지(양성) 2: 로그가 없어도 실행 수가 소스 리프 하한보다 적으면 «부분 실행»인가(둘째 자 단독)"
  b_expect "로그 없음 + 3건" "$tmp/b_short.xml" 0 "★★ 부분 실행 3/|실행 수가 소스 리프 하한보다 적다" ""
  echo "── 배너 모순 대조: 로그는 필터 없음(전량)인데 실행 수가 하한보다 적으면 «부분»이 아니라 «모순»으로 말하는가"
  b_expect "전량 로그 + 3건" "$tmp/b_fulllog.xml" 0 "⚠⚠ 로그는 전량인데 실행 3" "★★ 부분 실행"
  echo "── 배너 정상(음성) 대조: platform을 모르는 xml에 소스 하한을 들이대지 않는가(0이라고 하지 않는다)"
  b_expect "platform 없음" "$tmp/r1_ok.xml" 0 "소스 리프 미확인" "★★ 부분 실행|⚠⚠"
  echo "── 배너 실측 교정: 실제 전량 xml에는 배너가 없고, 실제 필터 xml에는 있는가"
  # ★★ 2026-09-27 qa-regression 정정 — **이 줄의 기대가 달력에 썩었다(내 변경이 아니다).**
  #   원래 기대는 «실측 전량 xml에는 ⚠⚠ 배너가 없다»였다. 그런데 이 xml은 09-14에 박제된 783건이고
  #   그 뒤 PlayMode가 827건까지 자랐다 — 오늘 소스 리프는 ≥821이라 **배너가 뜨는 것이 옳다.**
  #   (하한 MIN_PLAY_CASES와는 무관하다: b_expect는 report에 minc=0을 **리터럴로** 넘긴다.)
  #   ⇒ 기대를 뒤집어 **«낡은 전량 xml이면 배너가 뜨고, 그 사유가 «테스트가 늘었다»여야 한다»**로 만든다.
  #     이 줄은 이제 «신선도 배너가 실제로 무는가»의 양성 대조다. «늘 켜짐»이 아닌지는 바로 아래
  #     새 대조(오늘 트리의 최신 전량 실행)가 본다 — 박제된 입력과 자가 갱신 입력을 갈라 둔다.
  #   ★ 만약 누가 테스트를 783건 이하로 줄이면 이 줄이 ✗가 된다. 그건 조용한 실패가 아니라
  #     시끄러운 실패이고, 그때 다시 볼 값어치가 있는 사건이다.
  if [ -f "$rx_play" ]; then b_expect "실측 play-full.xml(09-14 박제 783 — 그 뒤 트리가 자랐다)" "$rx_play" 3 "범위: 전량|PlayMode 폴더 파일|⚠⚠ 로그는 전량인데|실행 뒤 테스트가 늘었거나" "★★ 부분 실행|파일 0개"
  else echo "  · play-full.xml 없음 — 미확인."; fi
  if [ -f "$rx_edit" ]; then b_expect "실측 edit-full.xml(전량 3141)" "$rx_edit" 0 "범위: 전량|EditMode 폴더 파일" "★★ 부분 실행|⚠⚠|파일 0개"
  else echo "  · edit-full.xml 없음 — 미확인."; fi
  if [ -f "$rx_m5p" ]; then b_expect "실측 mut-M5p.xml(필터 3건)" "$rx_m5p" 3 "★★ 부분 실행 3/|-testFilter" ""
  else echo "  · mut-M5p.xml 없음 — 미확인."; fi

  # ★★ 2026-09-27 qa-regression 신설 — 위 실측 교정 셋은 **박제된 xml**을 쓴다. 트리는 계속 자라서
  #   어제의 «전량»이 오늘은 «소스 리프보다 적은 실행»이 된다(실제로 play-full.xml이 그렇게 됐다).
  #   그래서 «배너가 늘 켜져 있지는 않은가»는 **오늘 트리에서 나온 최신 전량 실행**으로 따로 잰다.
  #   입력이 매 라운드 갱신되므로 이 대조는 달력에 썩지 않는다. 판정 기준을 배너와 같은 코드에서
  #   다시 계산하지 않는다는 점도 중요하다 — 입력만 신선하게 갈고, 판정은 report 출력 그대로 읽는다.
  echo "── 배너 신선도 대조: ★ 오늘 트리의 최신 전량 play 실행에는 배너가 없는가(«늘 켜짐» 방지)"
  local nplay nout
  nplay=$(ls -t "$OUTDIR"/*_play.xml 2>/dev/null | head -1)
  if [ -n "${nplay:-}" ] && [ -f "${nplay:-}" ]; then
    nout=$( report "$nplay" 0 0 2>&1 ) || true
    if ! printf '%s\n' "$nout" | grep -qF '범위: 전량'; then
      echo "  · 최신 play 실행($(basename "$nplay"))이 «전량»으로 읽히지 않는다 — 이 대조는 판정 불가(미확인)."
    elif printf '%s\n' "$nout" | grep -qF '⚠⚠'; then
      echo "  ✗ 오늘 트리의 최신 전량 실행($(basename "$nplay"))에도 배너가 떴다 — 배너가 «늘 켜짐»이거나 소스 리프 계수가 과대하다."
      printf '%s\n' "$nout" | grep -E '범위|⚠⚠' | sed 's/^/      /'
      rc=1
    else
      echo "  ✓ $(basename "$nplay") — 배너 없음(오늘 트리의 전량으로 읽힌다)"
    fi
  else
    echo "  · docs/verify/runs/ 에 play 실행 xml이 없다 — 이 대조는 판정 불가(미확인)."
  fi

  # ★ 교차 대조 — report·compare·baseline.py가 nunit_verdict 하나를 공유하므로 **같이 틀릴 수 있다.**
  #   그 모듈을 import하지 않는 원시 정규식 판정기로 같은 파일을 다시 잰다(다른 방법으로 다시 잰다).
  echo "── ★ R1 교차 대조: nunit_verdict(ElementTree)와 원시 정규식 판정기(모듈 미사용)가 같은 판정을 내는가"
  local -a xfiles=( "$tmp"/r1_*.xml )
  for real in "$rx_m5p" "$rx_edit" "$rx_play" "$rx_setup"; do [ -f "$real" ] && xfiles+=( "$real" ); done
  if python3 - "${xfiles[@]}" <<'PY'
import re, sys
import os
sys.path.insert(0, os.path.join(os.environ['REGRESS_REPO'], 'docs/verify'))
import nunit_verdict as NV
ATTR = re.compile(r'([\w-]+)="([^"]*)"')
def raw_green(p):
    s = open(p, 'rb').read().decode('utf-8', 'replace')
    s = re.sub(r'<!\[CDATA\[.*?\]\]>', '', s, flags=re.S)   # 메시지·로그 안의 태그 모양 글자를 걷는다
    m = re.search(r'<test-run\b([^>]*)>', s)
    if not m:
        return False
    a = dict(ATTR.findall(m.group(1)))
    failed = int(a.get('failed') or 0)
    if failed != 0 or len(re.findall(r'<test-case\b[^>]*\sresult="Failed"', s)) != failed:
        return False
    if a.get('result') not in ('Passed', 'Skipped:Ignored'):
        return False
    for t in re.finditer(r'<test-suite\b([^>]*)>', s):
        sa = dict(ATTR.findall(t.group(1)))
        if sa.get('site') in ('SetUp', 'TearDown') or (sa.get('result') or '').startswith('Failed'):
            return False
    return True
files = sys.argv[1:]
bad = 0
for p in files:
    x, y = NV.judge(p)['green'], raw_green(p)
    print(f"  {'✓' if x == y else '✗'} {p.rsplit('/', 1)[-1]}: nunit_verdict={'초록' if x else '빨강'} / 원시정규식={'초록' if y else '빨강'}")
    bad += (x != y)
greens = sum(1 for p in files if NV.judge(p)['green'])
if len(files) < 9 or greens == 0 or greens == len(files):
    print(f"  ✗ 대조가 공허하다 — 파일 {len(files)}개(최소 9) · 초록 {greens}개(초록과 빨강이 둘 다 있어야 한다)")
    bad += 1
sys.exit(1 if bad else 0)
PY
  then echo "  ✓ 두 판정기가 ${#xfiles[@]}개 파일에서 전부 일치(초록·빨강이 둘 다 섞인 표본)"
  else echo "  ✗ 판정기 불일치 또는 대조 공허 — R1 판정을 믿지 마라."; rc=1; fi

  echo "── 양성 대조 C: 정상 xml은 통과하는가(이게 빨간불이면 위 대조는 전부 무의미)"
  local okn=$(( MIN_EDIT_CASES + 10 ))
  cat > "$tmp/ok.xml" <<X
<test-run id="2" testcasecount="$okn" result="Passed" total="$okn" passed="$okn" failed="0" skipped="0" inconclusive="0"></test-run>
X
  local okrc
  ( report "$tmp/ok.xml" "$MIN_EDIT_CASES" 0 "$okn" "prev.xml" ) >/dev/null 2>&1; okrc=$?
  if [ "$okrc" = 0 ]; then
    echo "  ✓ 통과했다(양성 대조, rc=0)"
  else echo "  ✗ 정상 파일을 거부했다(rc=$okrc) — 판독기가 고장났다."; rc=1; fi

  rm -rf "$tmp"
  [ "$rc" -eq 0 ] && echo "자기검사 통과 — 가드 10종(G10 포함) + R1/R2/R5 결과 판정 + 양성 대조 전부 제 일을 한다." || echo "자기검사 실패."
  return $rc
}

# ---- 본 실행 ---------------------------------------------------------------
run() {   # $1=edit|play  $2=라벨
  local mode="$1" label="$2"
  local platform minc
  case "$mode" in
    edit) platform=EditMode; minc=$MIN_EDIT_CASES ;;
    play) platform=PlayMode; minc=$MIN_PLAY_CASES ;;
    *) die "usage: regress.sh <edit|play> <label>" ;;
  esac
  mkdir -p "$OUTDIR"
  local xml="$OUTDIR/${label}_${mode}.xml"
  local log="$OUTDIR/${label}_${mode}.log"

  assert_no_unity_running

  # G9 — 이번 실행 파일을 지우기 **전에** 직전 최대치를 잡아 둔다(같은 라벨 재실행 대비 제외).
  local pb prevn prevwho
  pb=$(prev_best "$mode" "$xml"); prevn=${pb%% *}; prevwho=${pb#* }

  # G4a — 정적 하한이 낡았는가. 이 저장소가 실제로 당한 형태다(하한 1390 vs 실측 1800).
  stale_floor_report "$minc" "$prevn" "$prevwho" "$mode"

  # G3 — 먼저 지운다. 지워지지 않으면 그 자체가 실패다.
  rm -f "$xml" "$log"
  [ -f "$xml" ] && die "G3: 이전 결과 파일을 지우지 못했다 — $xml"

  local head dirty target started
  # ★★ 2026-09-27 qa-regression — 이 두 줄이 **리더 커밋을 깨뜨릴 수 있었다**(리더 채택 1).
  #   git 옵션 교체이고 **글자 삭제는 없다** — 원문을 여기 남긴다.
  #     원문: head=$(git -C "$REPO" rev-parse --short HEAD)
  #           dirty=$(git -C "$REPO" status --porcelain | wc -l | tr -d ' ')
  #     신문: 둘 다 **git 전역 옵션 자리**(하위 명령 **앞**)에 --no-optional-locks 를 넣었다.
  #
  #   왜: plain `git status`는 작업 트리 파일의 stat이 바뀌어 있으면 `.git/index`를 고쳐 쓴다
  #   (docs/TEAM.md 4절 「에이전트의 git 호출은 전부 git --no-optional-locks」). 그동안 index.lock이
  #   잡히고, 같은 순간 커밋 쪽 `git add`는 **rc 128**로 죽는다 — **피해는 읽는 쪽이 아니라
  #   커밋하는 쪽에 난다.** 이 줄은 전량 실행 **직전**에 돌고 리더는 실행이 끝나면 **곧바로**
  #   커밋하므로 겹침 구간은 가정이 아니라 실재한다.
  #
  #   실측(qa-regression, git 2.54.0 Apple Git-157 / macOS 26.6.2, **명령마다 새 임시 저장소** ·
  #   touch -t 로 stat만 바꾼 뒤 .git/index 해시 대조):
  #     · 양성 대조 plain status --porcelain               -> 인덱스 **바뀜**
  #     · 신문 --no-optional-locks ... status --porcelain  -> 인덱스 **불변**
  #     · 신문 --no-optional-locks ... rev-parse --short   -> 인덱스 **불변**
  #     · 음성 대조 「명령 없음」(touch 만)                 -> 인덱스 **불변**
  #   ★ 첫 시도는 **양성 대조가 불변으로 나와 폐기했다**: 저장소 생성과 touch가 같은 초에 들어가면
  #     git이 그 항목을 racily clean으로 보고 갱신된 stat을 **일부러 쓰지 않는다**. 교정이 깨진
  #     측정은 「고쳤다」와 「프로브가 죽었다」를 구분하지 못한다 — 그래서 touch -t 로 다시 쟀다.
  #
  #   ★★ 옵션 위치를 틀리면 **조용한 거짓**이 된다(실측): status --porcelain --no-optional-locks 는
  #     rc 129(unknown option)로 죽고 출력이 비어 wc -l 이 **0**을 낸다 ⇒ dirty=0 ⇒ 러너가
  #     「작업 트리가 깨끗하다」고 말한다. **전역 옵션 자리를 반드시 지켜라.**
  #     같은 실측의 양성 대조: 신문 형태는 수정 1 + 미추적 1을 plain과 **똑같이 2**로 센다.
  #
  #   ★ rev-parse 는 옵션 없이도 인덱스 불변으로 측정되지만(TEAM.md 허용 목록) 규칙이 「전부」라
  #     함께 붙였다. 같은 폴더 다른 도구의 git 호출 6곳(baseline.py 2 · promised_tests_scan.py 4)은
  #     이미 이 형태였다 ⇒ 이 줄 둘이 **유일한 누락**이었다(「0건 누락」이 아니라 2건 누락이고,
  #     그 6곳이 이 전수 조사가 죽은 프로브가 아니라는 양성 대조다).
  head=$(git --no-optional-locks -C "$REPO" rev-parse --short HEAD)
  dirty=$(git --no-optional-locks -C "$REPO" status --porcelain | wc -l | tr -d ' ')
  target=$(active_target)
  started=$(date +%s)

  echo "=========================================================="
  echo " 전량 회귀 — $platform / 라벨 '$label'"
  echo " HEAD=$head  작업트리 변경 파일=${dirty}개  활성 빌드 타깃=[$target]"
  echo " 직전 최대 ${prevn}건 (${prevwho:-없음}) — 이보다 줄면 G9가 문다"
  echo " 시작 $(date '+%F %H:%M:%S')"
  echo "=========================================================="
  [ "$dirty" != "0" ] && echo "⚠ 작업 트리가 더럽다(${dirty}개). 이 측정은 **HEAD가 아니라 지금 트리**의 결과다."
  case "$target" in
    UNKNOWN*) echo "⚠ 활성 타깃을 판정하지 못했다($target). 리플렉션/타입 기반 감사의 결과를 신뢰하지 마라." ;;
  esac

  # ★ G1/G2는 이제 주석이 아니라 코드다 — 인자 배열을 만들고 검사한 뒤에 넘긴다.
  local -a args
  args=( -batchmode -nographics
         -projectPath "$REPO"
         -runTests -testPlatform "$platform"
         -testResults "$xml"
         -logFile "$log" )
  assert_launch_args "${args[@]}"

  "$UNITY" "${args[@]}"
  local unity_rc=$?
  echo "unity 종료코드=$unity_rc"

  # G8 — 컴파일 실패는 결과 xml 유무와 무관하게 무효다.
  if log_compile_failed "$log"; then
    echo "── 컴파일 에러(중복 제거, 최대 10건) ──"
    grep -o "Assets/[^ ]*([0-9]*,[0-9]*): error CS[0-9]*: .*" "$log" 2>/dev/null | sort -u | head -10
    die "G8: 컴파일 실패로 배치모드가 거부됐다 — 이 트리에서 잰 어떤 숫자도 무효다."
  fi

  # ★ 타깃을 **실행 뒤에 다시** 읽는다. 배치모드는 시작할 때 재컴파일할 수 있어,
  #   실행 전에 읽은 값은 '직전 컴파일'의 타깃일 수 있다. 두 값이 다르면 그 자체가 사건이다.
  local target_after; target_after=$(active_target)
  [ "$target_after" != "$target" ] && \
    echo "⚠ 활성 타깃이 실행 전후로 달라졌다: [$target] -> [$target_after]. 이 실행 중에 재컴파일이 있었다."

  # ---- .meta 사이드카 -------------------------------------------------------
  # ★ 2026-09-02 qa-regression 신설. BASELINE.md 생성기(docs/verify/baseline.py)가 이 파일을
  #   **잰 값**으로 읽는다. 없으면 생성기는 로그의 Bee dag 해시로 **추론**하고 `~`를 붙인다.
  #
  #   왜 필요한가: 리더가 19:07 빌드로 활성 타깃을 OSX로 바꿔 놓고 그 뒤로도 「WIN이다」라고
  #   계속 알렸다. 원인은 **실행마다 타깃을 적어 두는 칸이 없었던 것**이다.
  #
  # ★ target= 에는 **실행 후(target_after)** 값을 적는다. 실행 전 값은 '직전 컴파일'의 산물이라
  #   재부팅 직후·타깃 전환 직후에 구조적으로 거짓말을 한다(실측). 실행 전 값은 별도 칸으로
  #   남겨 두 값이 갈렸다는 **사실 자체**가 대장에 보이게 한다.
  {
    echo "label=$label"
    echo "mode=$mode"
    echo "head=$head"
    echo "dirty=$dirty"
    echo "target=$target_after"
    echo "target_before=$target"
    echo "target_shifted=$([ "$target_after" = "$target" ] && echo 0 || echo 1)"
    echo "started=$started"
    echo "finished=$(date +%s)"
    echo "unity_rc=$unity_rc"
    # ★ 2026-09-27 qa-regression — G4a(하한 노후) 판정을 **보존한다.**
    #   그전까지 이 경고는 러너 stdout으로만 나갔다 = 라운드가 끝나면 사라졌고, 하한이 낡은 채
    #   넘어간 것을 사후에 확인할 방법이 없었다(실측: 옛 .meta 11키에 하한 관련 0건이고
    #   `runs/*.log`는 Unity `-logFile`이라 러너 출력이 안 들어간다 — 「경고했는데 무시됐다」와
    #   「경고가 없었다」가 디스크상 똑같이 생긴 상태였다).
    #   ★ 키 추가는 안전하다: nunit_verdict.run_scope는 REGRESS_META_KEYS를 **부분집합**으로 본다
    #     (`REGRESS_META_KEYS <= set(meta)`) — 키가 늘어도 「미확인」이 되지 않는다(호출 2곳 확인).
    # ★ 2026-09-27 — 규칙 29의 사이드카판: **어느 판의 러너가 이 실행을 판정했는가.**
    #   로그·xml에는 러너 자신의 신원이 없어서, 나중에 「그때 하한이 얼마였나 · 그 판에 이 가드가
    #   있었나」를 되짚을 방법이 없었다. floor= 와 함께 판 자체를 못박는다.
    echo "runner_sha16=$(shasum -a 256 "${BASH_SOURCE[0]}" | cut -c1-16)"
    echo "floor=$minc"
    echo "floor_prev_best=$prevn"
    if [ "${prevn:-0}" -gt 0 ]; then
      echo "floor_stale=$([ "$minc" -lt $(( prevn * STALE_FLOOR_RATIO_PCT / 100 )) ] && echo 1 || echo 0)"
    else
      echo "floor_stale=unknown"
    fi
    # ★ 2026-09-14 — 실제로 넘긴 인자를 적는다. docs/verify/nunit_verdict.run_scope가 로그가 사라진 실행의
    #   전량/부분을 이 줄로 가른다(필터 인자가 있으면 부분). 인자는 한 줄에 공백으로 잇는다(경로에 공백 없음).
    echo "args=${args[*]}"
  } > "$OUTDIR/${label}_${mode}.meta"

  report "$xml" "$minc" "$started" "$prevn" "$prevwho"
  local rrc=$?

  # ---- G10 세이브 격리 게이트 회계 -----------------------------------------
  # ★ report와 **따로** 돈다. report는 러너 xml만 보고, 이쪽은 로그와 xml을 **대조**한다.
  #   둘의 rc를 합친다 — 어느 하나만 빨개져도 이 실행은 통과가 아니다.
  echo
  echo "── G10 세이브 격리 게이트 회계 ──"
  gate_audit "$log" "$xml" "$mode" || rrc=1

  echo
  echo "측정 조건 요약: HEAD=$head / dirty=$dirty / 타깃(후)=[$target_after] / 파일=$xml"
  return $rrc
}

case "${1:-}" in
  edit|play) [ $# -ge 2 ] || die "usage: regress.sh <edit|play> <label>"; run "$1" "$2" ;;
  report)    [ $# -ge 2 ] || die "usage: regress.sh report <xml>"; report "$2" 0 0 ;;
  compare)   [ $# -ge 3 ] || die "usage: regress.sh compare <옛.xml> <새.xml>"; compare "$2" "$3" ;;
  gate)      [ $# -ge 4 ] || die "usage: regress.sh gate <로그> <결과.xml> <edit|play>"
             gate_audit "$2" "$3" "$4" ;;
  target)    echo "활성 빌드 타깃=[$(active_target)]" ;;
  selfcheck) selfcheck ;;
  *) echo "usage: regress.sh <edit|play> <label> | report <xml> | compare <a.xml> <b.xml> | gate <log> <xml> <edit|play> | target | selfcheck"; exit 2 ;;
esac
