using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 원래 제목: <b>유휴 수급 · [오늘 할일] 보상 · H-8 등급 눈금이 실제로 배선됐는가</b>
    /// — 2026-09-06 배선 라운드 3차(재화 획득 경로의 마지막 세 개).
    ///
    /// ============================================================================
    /// ★★★ 2026-09-29 DLC·재화 폐지 R5 — <b>앞 둘의 방향이 뒤집혔다</b>
    /// ============================================================================
    /// 유휴 수급과 [오늘 할일] 동전이 폐지됐다. 이 파일은 그 둘에 대해 원래
    /// <b>「배선이 실재하는가」(존재 단언)</b>를 재고 있었는데, 지금은
    /// <b>「배선이 0건인가」(부재 단언)</b>를 잰다 — <b>되살아나는 것을 막는 잠금</b>이다.
    ///
    /// <para>★★ 부재 단언은 썩으면 <b>조용히 초록</b>이 된다(CLAUDE.md). 그래서 두 부재 단언 모두
    /// <b>같은 실행·같은 스캐너</b>로 잡히는 양성 대조를 옆에 붙였다: 살아 있는 세 번째 축
    /// (등급 눈금 기록)이 실제로 <b>잡히는지</b>를 함께 확인한다. 그 양성 대조가 0건이면 이 실행의
    /// 부재 판정은 전부 무효다.</para>
    ///
    /// <para>★ 등급 눈금(§1-③·§4)은 <b>재화와 무관한 축</b>이라 한 줄도 바뀌지 않았다.</para>
    ///
    /// ============================================================================
    /// 착수 직전 실측 — 셋 다 프로덕션 호출부 <b>0건</b>이었다
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><c>CurrencyModel.TickIdleIncome</c> — 하루 종일 켜 두는 앱의 <b>주 수급원</b>인데 0건.</item>
    ///   <item><c>CurrencyModel.TryPayTodoDailyCoins</c> — 0건.</item>
    ///   <item><c>EquipmentStatRules.RecordTierHighWaterMarks</c> — 0건. 그런데
    ///     <c>CharacterInfoWindow.Stats.cs</c>는 그 값을 <b>이미 그리고 있었다</b> ⇒ 눈금이 영구히 0단계.
    ///     «아직 못 찍었다»와 «기록이 안 된다»가 화면에서 똑같이 생긴 상태였다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 이 파일이 잰다고 주장하지 <b>않는</b> 것
    /// ============================================================================
    /// 수급 <b>계산</b>(요율·상한·창·래칫)은 <c>CurrencyRulesTests</c>가, 등급 눈금의 <b>매핑</b>은
    /// <c>EquipmentStatInvariantTests</c>가 이미 검증한다. 여기서 그것을 다시 재면 같은 사실을
    /// 두 곳에서 재게 되고, 둘이 갈라지는 날 어느 쪽이 옳은지 가릴 수 없다.
    /// <b>이 파일은 「부르는 코드가 있는가 / 옳은 자리에서 부르는가」만 잰다.</b>
    ///
    /// ============================================================================
    /// ★★ 가장 중요한 단언 — <b>유휴 기산점은 조건 없이 전진해야 한다</b>(I-7′)
    /// ============================================================================
    /// 집중 세션 동안 기산점을 멈춰 두면 세션이 끝난 <b>첫 틱의 델타에 세션 전체 길이가 실린다</b> —
    /// 25분을 완주한 사용자가 완주 보상을 받고 그 25분을 <b>유휴로 한 번 더</b> 받는다.
    /// 그것이 «같은 1초가 두 번 지급되지 않는다»의 파손이고, 이 배선에서 유일하게
    /// <b>조용히 틀릴 수 있는</b> 자리다(화면에도 로그에도 «두 번 받았다»는 흔적이 없다).
    /// <para>여기서는 <b>소스 순서</b>로, <c>Tests/PlayMode/CurrencyWiringRuntimeTests</c>에서는
    /// <b>실제 세션을 돌려</b> 잰다 — 서로 다른 자 두 개다.</para>
    ///
    /// ============================================================================
    /// ★ 스캐너는 <see cref="CurrencySeedAndArcheryWiringTests"/>의 것을 <b>그대로</b> 쓴다
    /// ============================================================================
    /// 복제하면 검사기가 둘이 되고 둘이 같은 방향으로 썩는다. 그리고 스캐너의
    /// <b>양성·음성·주석배제 대조는 저 파일에만 있다</b> — 공유해야 그 대조가 이 파일의 초록까지 보증한다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(<c>Core/</c>·<c>Interaction/</c> 소스만 읽는다).</para>
    /// </summary>
    public sealed class CurrencyIdleTodoTierWiringTests
    {
        private const string LogPrefix = "[재화배선3]";

        // ====================================================================
        // 니들 — nameof로 조립한다(오타가 컴파일 에러가 되도록)
        // ====================================================================

        // ★★ 2026-09-29 — 폐지된 두 진입점의 니들은 <c>nameof</c>로 조립할 수 없다(멤버가 없다).
        //   그래서 <b>문자열 리터럴</b>로 둔다. 니들이 죽는 것을 막는 장치는 두 가지다:
        //   ① 이 두 이름이 <b>되살아나면</b> 아래 부재 단언이 빨개진다(그것이 이 니들의 목적이다).
        //   ② 스캐너 자체가 죽는 경우는 <see cref="TierRecorderCall"/> 양성 대조가 잡는다
        //      (같은 실행·같은 함수로 «잡히는 이름»이 하나 이상 있음을 증명한다).
        //   ⚠ 「소스에 문자열이 없다 = 죽은 니들」은 틀린 판정법이다(CLAUDE.md) — 여기서는
        //      부재가 <b>기대값</b>이고, 계기의 생존은 ②로 따로 잰다.

        /// <summary>폐지된 유휴 수급 진입점. <b>다시 나타나면 빨개진다.</b></summary>
        private const string RetiredIdleModelCall = "CurrencyModel.TickIdleIncome(";

        /// <summary>폐지된 [오늘 할일] 정액 진입점. <b>다시 나타나면 빨개진다.</b></summary>
        private const string RetiredTodoModelCall = "CurrencyModel.TryPayTodoDailyCoins(";

        private static string TierRecorderCall =>
            nameof(EquipmentStatRules) + "." + nameof(EquipmentStatRules.RecordTierHighWaterMarks) + "(";

        /// <summary>«집중 세션 중인가»의 단일 출처. <b>존재 단언</b>으로 쓴다 —
        /// 이름이 바뀌면 <see cref="집중세션_판정의_단일출처가_실재한다"/>가 먼저 빨개진다.</summary>
        private const string FocusSessionFlag = "IsSessionActive";

        private const string StatsDirectorFileSuffix = "/CharacterStatsDirector.cs";
        private const string TodoModelFileSuffix = "/TodoListModel.cs";
        private const string TierRulesFileSuffix = "/EquipmentStatRules.cs";

        private const string ToggleHeader = "void ToggleComplete(";

        // ====================================================================
        // 소스 도구 — 전부 CurrencySeedAndArcheryWiringTests의 것
        // ====================================================================

        private static List<CurrencySeedAndArcheryWiringTests.Source> All()
            => CurrencySeedAndArcheryWiringTests.ProductionOrFail();

        private static List<string> FilesCalling(List<CurrencySeedAndArcheryWiringTests.Source> all, string needle)
            => CurrencySeedAndArcheryWiringTests.FilesCalling(all, needle);

        private static string CodeOfOrFail(List<CurrencySeedAndArcheryWiringTests.Source> all, string suffix)
        {
            var found = new List<string>();
            string code = null;
            foreach (CurrencySeedAndArcheryWiringTests.Source s in all)
            {
                if (!s.Path.EndsWith(suffix, StringComparison.Ordinal)) continue;
                found.Add(s.Path);
                code = s.Code;
            }
            Assert.AreEqual(1, found.Count,
                $"{LogPrefix} 「{suffix}」에 해당하는 프로덕션 파일이 {found.Count}개입니다(1이어야 합니다): " +
                string.Join(", ", found) + ". 파일을 옮겼거나 같은 이름이 여럿입니다 — " +
                "아래 구역 단언은 지금 아무것도 재지 못합니다.");
            return code;
        }

        private static string RegionOrFail(string code, string header)
        {
            string region = CurrencySeedAndArcheryWiringTests.MemberRegionOrNull(code, header);
            Assert.IsNotNull(region,
                $"{LogPrefix} 「{header}」 구역을 잘라 내지 못했습니다 — 그 멤버가 없거나 앵커가 낡았습니다.");
            return region;
        }

        // ====================================================================
        // §1. 배선 실재 — <b>이 라운드 전에는 셋 다 빨갛다</b>
        // ====================================================================

        /// <summary>
        /// ★★★ 2026-09-29 — <b>방향이 뒤집힌 테스트</b>. 옛 이름은
        /// <c>프로덕션에_유휴_수급_호출부가_실재한다</c>였고 「0건이면 실패」였다.
        /// 지금은 <b>0건이 기대값</b>이다(DLC·재화 폐지, 사용자 확정).
        /// <para>★ 부재 단언이 조용히 썩지 않도록 <b>같은 실행에서 같은 스캐너</b>로 양성 대조를 잡는다:
        /// 살아 있는 축(등급 눈금 기록)이 실제로 걸리는지 확인한다. 그쪽이 0이면 스캐너가 죽은 것이고,
        /// 이 실행의 «0건» 판정은 <b>측정이 아니라 침묵</b>이다.</para>
        /// </summary>
        [Test]
        public void 폐지된_유휴_수급과_할일_정액은_프로덕션에_되살아나지_않았다()
        {
            List<CurrencySeedAndArcheryWiringTests.Source> all = All();

            // ── 양성 대조 먼저. 이게 0이면 아래 두 «0건»은 아무 뜻이 없다.
            var outside = new List<CurrencySeedAndArcheryWiringTests.Source>();
            foreach (CurrencySeedAndArcheryWiringTests.Source s in all)
            {
                if (s.Path.EndsWith(TierRulesFileSuffix, StringComparison.Ordinal)) continue;
                outside.Add(s);
            }
            List<string> liveCallers = FilesCalling(outside, TierRecorderCall);
            Assert.IsNotEmpty(liveCallers,
                $"{LogPrefix} 양성 대조 실패 — 같은 스캐너가 살아 있는 호출부(「{TierRecorderCall}」)도 " +
                "찾지 못했습니다. 스캐너가 눈이 멀었으므로 아래 «0건»은 측정이 아닙니다.");

            // ── 부재 단언 둘.
            List<string> idleCallers = FilesCalling(all, RetiredIdleModelCall);
            Assert.IsEmpty(idleCallers,
                $"{LogPrefix} 폐지된 유휴 수급(「{RetiredIdleModelCall}」)이 되살아났습니다: " +
                string.Join(", ", idleCallers) + ". 사용자가 닫은 문(재화)을 다시 여는 변경입니다 — " +
                "되살리려면 요율·일일 상한·8시간 창·소수분 캐리가 한꺼번에 돌아와야 하고, " +
                "그 넷 중 하나만 빠져도 「하루 종일 켜 뒀는데 0원」이 됩니다.");

            List<string> todoCallers = FilesCalling(all, RetiredTodoModelCall);
            Assert.IsEmpty(todoCallers,
                $"{LogPrefix} 폐지된 [오늘 할일] 정액(「{RetiredTodoModelCall}」)이 되살아났습니다: " +
                string.Join(", ", todoCallers) + ".");

            TestContext.WriteLine($"{LogPrefix} 폐지 확인 — 유휴 0건 / 할일 0건. " +
                $"양성 대조(등급 눈금) {liveCallers.Count}건: {string.Join(", ", liveCallers)}");
        }

        [Test]
        public void 프로덕션에_등급_눈금_기록_호출부가_실재한다()
        {
            List<CurrencySeedAndArcheryWiringTests.Source> all = All();

            // ★ 선언 파일을 빼지 않으면 이 단언은 <b>항상</b> 통과한다 —
            //   EquipmentStatRules.cs 안에 그 이름의 선언이 있기 때문이다(롤오버 감사가 티커를 뺀 것과 같은 이유).
            var outside = new List<CurrencySeedAndArcheryWiringTests.Source>();
            int declaring = 0;
            foreach (CurrencySeedAndArcheryWiringTests.Source s in all)
            {
                if (s.Path.EndsWith(TierRulesFileSuffix, StringComparison.Ordinal)) { declaring++; continue; }
                outside.Add(s);
            }
            Assert.AreEqual(1, declaring,
                $"{LogPrefix} 선언 파일({TierRulesFileSuffix})이 정확히 1개가 아닙니다 — " +
                "제외가 아무것도 안 걸렀다면 아래 단언은 뜻을 잃습니다.");

            List<string> callers = FilesCalling(outside, TierRecorderCall);
            Assert.IsNotEmpty(callers,
                $"{LogPrefix} 「{TierRecorderCall}」를 부르는 프로덕션 파일이 (선언 파일을 빼고) 0건입니다. " +
                "정보창은 그 값을 <b>이미 그리고 있으므로</b>, 배선이 없으면 눈금이 영구히 0단계로 보입니다 — " +
                "«아직 못 찍었다»와 구분되지 않습니다.");

            TestContext.WriteLine($"{LogPrefix} 등급 눈금 호출부: {string.Join(", ", callers)}");
        }

        // ====================================================================
        // §2. 유휴 기산점 순서 — ★★★ 2026-09-29 <b>절 전체 폐지</b>
        // ====================================================================
        //
        // 뗀 것: <c>유휴_기산점은_집중세션_판정보다_먼저_조건없이_전진한다</c> ·
        //   <c>유휴_수급은_Update에서_롤오버_뒤에_돈다</c>. 배선(<c>AccrueIdleIncome</c>)이 삭제됐다.
        //
        // ★★ <b>그 두 테스트가 지키던 사실은 다음 적립 축에서 그대로 필요하다</b>:
        //   ① 기산점 전진은 <b>어떤 분기보다도 앞</b>에서, 조건 없이 일어나야 한다 — 「집중 중에는
        //      전진하지 않는」 코드로 미끄러지면 세션이 끝난 첫 틱이 세션 길이 전체를 한 번 더 지급한다.
        //   ② 지급 배선은 <b>날짜 롤오버 뒤</b>에 놓아야 한다 — 앞에 놓으면 자정 직후 한 틱이 어제
        //      카운터를 보고 상한에 걸려 조용히 0을 준다(화면에도 로그에도 흔적이 없다).
        //   ③ 그 배선의 시간 입력은 <b>단조 시계 두 시점의 차</b>다(프레임 델타·벽시계 금지).
        //   ③은 지금도 <c>DailyLimitClampAuditTests</c>가 살아 있는 진입점들에 대해 계속 잰다.

        /// <summary>★ <see cref="FocusSessionFlag"/>가 <b>실재를 가리키는지</b> 확인한다.
        /// <para>★ 2026-09-29 — 이 니들의 원래 독자(유휴 배선)는 사라졌지만 니들 자체는
        /// <b>살아 있는 계약</b>을 가리킨다: 세션 보상 셋(완주·중도 취소·긴급정지)이 전부 이 플래그를
        /// 재진입 관문으로 쓰고, <c>IsSessionActive = false</c> <b>앞</b>에서 지급해야 한다는 규약
        /// (DS-5′)이 그것에 걸려 있다. 이름이 바뀌면 그 규약을 재는 테스트들이 조용히 무력화된다.</para></summary>
        [Test]
        public void 집중세션_판정의_단일출처가_실재한다()
        {
            List<CurrencySeedAndArcheryWiringTests.Source> all = All();
            var owners = new List<string>();
            foreach (CurrencySeedAndArcheryWiringTests.Source s in all)
            {
                if (s.Path.EndsWith("/FocusWatchDirector.cs", StringComparison.Ordinal)
                    && s.Code.IndexOf(FocusSessionFlag, StringComparison.Ordinal) >= 0)
                {
                    owners.Add(s.Path);
                }
            }
            Assert.IsNotEmpty(owners,
                $"{LogPrefix} 「{FocusSessionFlag}」를 선언한 FocusWatchDirector를 찾지 못했습니다 — " +
                "세션 보상 세 경로의 재진입 관문이 실재하지 않거나 이름이 바뀌었습니다.");
        }

        // ====================================================================
        // §3. 할일 — ★ 방향이 뒤집혔다: 지급이 <b>어디에도</b> 없는가
        // ====================================================================

        /// <summary>
        /// ★★★ 2026-09-29 — 옛 이름은 <c>할일_보상은_UI가_아니라_완료_전이_한_곳에서_나간다</c>였고
        /// 「완료 전이 안에 지급이 있어야 한다」를 재고 있었다. 지금은 <b>지급이 없어야 한다</b>.
        /// <para>★ 양성 대조를 그대로 살렸다(그쪽이 원래 이 테스트의 강점이었다): UI 두 곳이 실제로
        /// <c>ToggleComplete</c>를 부르는지 먼저 확인한다 — 안 부르면 아래 부재 판정이
        /// 「이 파일은 할일과 무관하다」와 구분되지 않는다.</para>
        /// </summary>
        [Test]
        public void 할일_완료_전이와_UI_어디에도_동전_지급이_없다()
        {
            List<CurrencySeedAndArcheryWiringTests.Source> all = All();

            // (1) 완료 전이 구역 자체는 살아 있다(앵커 생존 확인 = 양성 대조).
            string todoCode = CodeOfOrFail(all, TodoModelFileSuffix);
            string region = RegionOrFail(todoCode, ToggleHeader);
            Assert.GreaterOrEqual(region.IndexOf("Completed", StringComparison.Ordinal), 0,
                $"{LogPrefix} 완료 전이 구역에서 Completed 갱신을 찾지 못했습니다 — 구역 앵커가 낡았습니다. " +
                "이 상태의 «지급 없음»은 측정이 아닙니다.");

            Assert.Less(region.IndexOf("PayTodoDailyCoins(", StringComparison.Ordinal), 0,
                $"{LogPrefix} 완료 전이(ToggleComplete)에 [오늘 할일] 동전 지급이 되살아났습니다 — " +
                "사용자가 닫은 문(재화)을 다시 여는 변경입니다.");

            // (2) UI 두 곳도 마찬가지. 각 파일마다 양성 대조(ToggleComplete 호출)를 먼저 확인한다.
            string toggleCall = nameof(TodoListModel) + "." + nameof(TodoListModel.ToggleComplete) + "(";
            foreach (string uiSuffix in new[] { "/TodoPostItWidget.cs", "/TodoBoardPopover.cs" })
            {
                string uiCode = CodeOfOrFail(all, uiSuffix);

                Assert.GreaterOrEqual(uiCode.IndexOf(toggleCall, StringComparison.Ordinal), 0,
                    $"{LogPrefix} {uiSuffix}가 「{toggleCall}」를 부르지 않습니다 — " +
                    "아래 부재 판정이 «이 파일은 할일과 무관하다»와 구분되지 않습니다. " +
                    "양성 대조가 죽었으므로 이 실행의 부재 판정은 무효입니다.");

                Assert.Less(uiCode.IndexOf(RetiredTodoModelCall, StringComparison.Ordinal), 0,
                    $"{LogPrefix} {uiSuffix}가 폐지된 「{RetiredTodoModelCall}」를 부릅니다.");
            }
        }

        // ====================================================================
        // §4. 등급 눈금 — 착용 변경 + 실행 직후 둘 다 잡는가
        // ====================================================================

        [Test]
        public void 등급_눈금은_착용_변경과_실행_직후_두_경로에서_기록된다()
        {
            string code = CodeOfOrFail(All(), StatsDirectorFileSuffix);

            string equipmentEvent = nameof(StickmanEventBus) + "." + nameof(StickmanEventBus.CharacterEquipmentChanged);
            Assert.GreaterOrEqual(code.IndexOf(equipmentEvent, StringComparison.Ordinal), 0,
                $"{LogPrefix} 「{equipmentEvent}」 구독을 찾지 못했습니다 — 장비를 갈아입어도 눈금이 안 찹니다. " +
                "착용·해제·세트 완성이 전부 이 이벤트 하나로 도착합니다.");

            // ★ 구독과 해지가 짝이어야 한다(구독만 있으면 씬 재로드마다 핸들러가 누적된다).
            Assert.GreaterOrEqual(code.IndexOf(equipmentEvent + " +=", StringComparison.Ordinal), 0,
                $"{LogPrefix} 구독(+=)이 없습니다.");
            Assert.GreaterOrEqual(code.IndexOf(equipmentEvent + " -=", StringComparison.Ordinal), 0,
                $"{LogPrefix} 해지(-=)가 없습니다 — 씬을 다시 로드할 때마다 핸들러가 쌓입니다.");

            // ★★ 실행 직후 경로. 이게 없으면 <b>이미 그 장비를 입고 있던 기존 사용자</b>는
            //    아무것도 갈아입지 않는 한 이벤트를 한 번도 못 받아 영원히 0단계로 남는다.
            string firstRunRegion = RegionOrFail(code, "void EnsureFirstRunStamped()");
            Assert.GreaterOrEqual(firstRunRegion.IndexOf("RecordStatTierMarks(", StringComparison.Ordinal), 0,
                $"{LogPrefix} 실행 직후 경로(EnsureFirstRunStamped)에서 눈금 기록을 찾지 못했습니다. " +
                "그러면 이미 장비를 입고 있던 사용자는 <b>갈아입기 전까지</b> 눈금이 0단계입니다.");

            // ★ 그 경로가 Start가 아니라 첫 Update인 것이 계약이다(저장 로드 순서 비보장).
            string startRegion = CurrencySeedAndArcheryWiringTests.MemberRegionOrNull(code, "void Start()");
            if (startRegion != null)
            {
                Assert.Less(startRegion.IndexOf("RecordStatTierMarks(", StringComparison.Ordinal), 0,
                    $"{LogPrefix} 눈금 기록이 Start()에 있습니다. 저장 파일 로드는 다른 컴포넌트의 Start()가 " +
                    "하는데 두 Start의 순서는 보장되지 않습니다 — 로드 전에 기록하면 그 값이 곧이어 " +
                    "덮입니다(이 파일의 근속 기산점이 같은 함정으로 한 번 깨졌습니다).");
            }
        }

        // ====================================================================
        // §5. 계약 — [오늘 할일] 보상이 실제로 하루 1회인가 (모델 + 롤오버 왕복)
        // ====================================================================

        [SetUp]
        public void Reset()
        {
            CurrencyModel.ResetForTesting();
            TodoListModel.ResetForTesting();
        }

        [TearDown]
        public void Clean()
        {
            CurrencyModel.ResetForTesting();
            TodoListModel.ResetForTesting();
        }

        /// <summary>
        /// ★★★ 2026-09-29 — 옛 이름은 <c>할일을_체크하면_동전이_들어오고_같은_날_두_번째부터는_0이다</c>였다.
        /// 지금 잠그는 것은 <b>반대</b>다: 할일을 체크해도 <b>지갑이 한 푼도 움직이지 않는다</b>.
        /// <para>★ 양성 대조를 반드시 함께 둔다 — 토글이 실제로 <b>완료 상태를 바꾸는지</b>. 그것 없이
        /// 「잔액 불변」만 재면 <b>토글 자체가 고장난 상태</b>와 구분되지 않는다(실패한 측정과 성공한
        /// 측정이 똑같이 생긴다 — 이 저장소의 거짓 통과 형태 #4).</para>
        /// <para>★ [오늘 할일] <b>기능</b>은 폐지 대상이 아니다. 사라진 것은 완료에 붙던 동전뿐이다.</para>
        /// </summary>
        [Test]
        public void 할일을_체크해도_지갑이_움직이지_않는다()
        {
            const int SoftCap = 99;
            TodoListModel.Add("첫 번째 할일", SoftCap);
            TodoListModel.Add("두 번째 할일", SoftCap);
            Assert.AreEqual(2, TodoListModel.ActiveItems.Count, "전제 — 할일 두 개가 있어야 합니다.");

            // 파일에서 읽은 잔액을 깔아 둔다 — 0에서 재면 「줄어들었는가」를 볼 수 없다.
            CurrencyModel.RestoreFromSave(new CurrencySaveState { CoinBalance = 1234 });
            int before = CurrencyModel.CoinBalance;
            Assert.Greater(before, 0, "전제 — 잔액이 0이면 이 단언이 약해집니다.");
            Assert.IsFalse(CurrencyModel.TodoCoinPaidToday, "전제 — 오늘 아직 안 받았어야 합니다.");

            int id = TodoListModel.ActiveItems[0].Id;
            TodoListModel.ToggleComplete(id);

            // ── 양성 대조: 토글이 실제로 일했다.
            Assert.IsTrue(TodoListModel.ActiveItems[0].Completed,
                "토글이 완료 상태를 바꾸지 않았습니다 — 아래 «잔액 불변»은 «아무것도 안 일어났다»와 " +
                "구분되지 않으므로 이 실행의 판정은 무효입니다.");

            // ── 부재 단언: 동전이 없다.
            Assert.AreEqual(before, CurrencyModel.CoinBalance,
                "★ 할일을 체크했더니 동전이 들어왔습니다 — 사용자가 닫은 문(재화)을 되열었습니다.");
            Assert.IsFalse(CurrencyModel.TodoCoinPaidToday,
                "★ [오늘 할일] 수령 플래그가 세워졌습니다 — 그 필드는 왕복만 해야 합니다(스키마 불변).");

            // 두 번째 할일도 같다.
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[1].Id);
            Assert.AreEqual(before, CurrencyModel.CoinBalance, "두 번째 완료에서 동전이 나왔습니다.");
        }

        // ★★ 2026-09-29 — 여기 있던 세 테스트를 뗐다(전부 <b>대상 소멸</b>):
        //   <c>할일_보상은_날짜가_바뀌면_다시_열린다</c> ·
        //   <c>집중_세션_중_유휴_틱은_창도_안_갉고_한_푼도_안_준다</c> ·
        //   <c>정상_프레임의_0동전과_정지의_0동전은_창_소비로_구별된다</c>.
        //
        // ★ 첫 번째가 재던 «롤오버가 todoCoinPaidToday를 되돌린다»는 계약은 <b>사라지지 않았다</b> —
        //   <c>CurrencyDayRolloverTests</c>와 <c>CurrencyRulesTests</c>가 폐기 카운터 넷을 한 묶음으로
        //   계속 잰다(값은 파일에서 세우고 결과만 본다).
        //
        // ★★ 세 번째가 남긴 <b>관측 설계</b>는 기록으로 남긴다: 「0지급」과 「멈췄다」가 관측상 같아지면
        //   정지 로그를 매 프레임 찍는 것 말고는 방법이 없어진다. 그래서 두 사실을 가르는 <b>별도의
        //   관측값</b>(그때는 창 소비 초)이 필요했고, 그것이 없던 옛 코드는 정상 동작을 5초에 한 번
        //   고장으로 신고했다(실측 720줄/시간). <b>다음에 「조용히 멈추는 기능」을 만들 때 읽어라.</b>
    }
}
