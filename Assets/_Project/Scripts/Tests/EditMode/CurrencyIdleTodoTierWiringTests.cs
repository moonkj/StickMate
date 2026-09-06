using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>유휴 수급 · [오늘 할일] 보상 · H-8 등급 눈금이 실제로 배선됐는가</b>
    /// — 2026-09-06 배선 라운드 3차(재화 획득 경로의 마지막 세 개).
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

        private static string IdleModelCall =>
            nameof(CurrencyModel) + "." + nameof(CurrencyModel.TickIdleIncome) + "(";

        private static string TodoModelCall =>
            nameof(CurrencyModel) + "." + nameof(CurrencyModel.TryPayTodoDailyCoins) + "(";

        private static string TierRecorderCall =>
            nameof(EquipmentStatRules) + "." + nameof(EquipmentStatRules.RecordTierHighWaterMarks) + "(";

        /// <summary>«집중 세션 중인가»의 단일 출처. <b>존재 단언</b>으로 쓴다 —
        /// 이름이 바뀌면 <see cref="집중세션_판정의_단일출처가_실재한다"/>가 먼저 빨개진다.</summary>
        private const string FocusSessionFlag = "IsSessionActive";

        private const string DirectorFileSuffix = "/CharacterProgressionDirector.cs";
        private const string StatsDirectorFileSuffix = "/CharacterStatsDirector.cs";
        private const string TodoModelFileSuffix = "/TodoListModel.cs";
        private const string TierRulesFileSuffix = "/EquipmentStatRules.cs";

        private const string IdleMemberHeader = "void AccrueIdleIncome()";
        private const string UpdateHeader = "void Update()";
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

        [Test]
        public void 프로덕션에_유휴_수급_호출부가_실재한다()
        {
            List<string> callers = FilesCalling(All(), IdleModelCall);
            Assert.IsNotEmpty(callers,
                $"{LogPrefix} 「{IdleModelCall}」를 부르는 프로덕션 파일이 0건입니다. " +
                "하루 종일 켜 두는 앱의 <b>주 수급원</b>이라, 이게 없으면 사용자는 집중 모드를 " +
                "돌리지 않는 한 동전을 한 푼도 못 법니다.");

            Assert.AreEqual(1, callers.Count,
                $"{LogPrefix} 유휴 수급 호출부가 {callers.Count}곳입니다(1이어야 합니다): " +
                string.Join(", ", callers) + ". 둘이 되면 <b>같은 1초가 두 번</b> 지급됩니다 — " +
                "각자 자기 기산점을 들고 있기 때문입니다.");

            TestContext.WriteLine($"{LogPrefix} 유휴 호출부: {string.Join(", ", callers)}");
        }

        [Test]
        public void 프로덕션에_할일_보상_호출부가_실재한다()
        {
            List<string> callers = FilesCalling(All(), TodoModelCall);
            Assert.IsNotEmpty(callers,
                $"{LogPrefix} 「{TodoModelCall}」를 부르는 프로덕션 파일이 0건입니다. " +
                "할일을 체크해도 동전이 나오지 않습니다.");

            Assert.AreEqual(1, callers.Count,
                $"{LogPrefix} 할일 보상 호출부가 {callers.Count}곳입니다(1이어야 합니다): " +
                string.Join(", ", callers) + ". «완료했다»의 정의가 여러 벌이 되면 " +
                "어떤 진입점으로 체크했는지에 따라 보상이 갈립니다.");

            TestContext.WriteLine($"{LogPrefix} 할일 호출부: {string.Join(", ", callers)}");
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
        // §2. ★★ 유휴 — 기산점 전진이 조건 없는가 (I-7′의 소스 쪽 자)
        // ====================================================================

        [Test]
        public void 유휴_기산점은_집중세션_판정보다_먼저_조건없이_전진한다()
        {
            string code = CodeOfOrFail(All(), DirectorFileSuffix);
            string region = RegionOrFail(code, IdleMemberHeader);

            // ── 구역 추출기 음성 대조: 다른 멤버를 삼키지 않았는가.
            Assert.Less(region.IndexOf("CharacterSaveStore.Load(", StringComparison.Ordinal), 0,
                $"{LogPrefix} 유휴 구역에 Start의 내용이 섞였습니다 — 구역이 너무 넓어 순서 판정이 무효입니다.");

            int assignAt = region.IndexOf("_idleTickMonotonic =", StringComparison.Ordinal);
            Assert.GreaterOrEqual(assignAt, 0,
                $"{LogPrefix} 유휴 구역에서 기산점 대입(_idleTickMonotonic =)을 찾지 못했습니다. " +
                "필드 이름이 바뀌었다면 이 니들을 갱신하고, 대입 자체가 사라졌다면 " +
                "<b>델타가 영원히 커지는</b> 상태입니다.");

            int flagAt = region.IndexOf(FocusSessionFlag, StringComparison.Ordinal);
            Assert.GreaterOrEqual(flagAt, 0,
                $"{LogPrefix} 유휴 구역에서 「{FocusSessionFlag}」를 찾지 못했습니다 — " +
                "집중 세션 판정을 다른 방법으로(그림자 상태로) 하고 있다는 뜻입니다. " +
                "CurrencyModel.TickIdleIncome 문서가 명시적으로 금지한 형태입니다.");

            int callAt = region.IndexOf(IdleModelCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(callAt, 0, $"{LogPrefix} 유휴 구역에 모델 호출이 없습니다.");

            Assert.Less(assignAt, flagAt,
                $"{LogPrefix} ★ 기산점 전진이 집중 세션 판정 <b>뒤</b>에 있습니다. " +
                "그 순서면 «집중 중에는 전진하지 않는» 코드로 쉽게 미끄러지고, 그러면 세션이 끝난 " +
                "첫 틱이 세션 길이 전체를 유휴로 <b>한 번 더</b> 지급합니다(I-7′ 파손). " +
                "전진은 어떤 분기보다도 앞에서, 조건 없이 일어나야 합니다.");
            Assert.Less(assignAt, callAt,
                $"{LogPrefix} 기산점 전진이 모델 호출보다 뒤에 있습니다 — 같은 파손입니다.");

            // ── 프레임 델타 금지(이 배선의 시간 입력은 단조 시계 두 시점의 차다).
            foreach (string forbidden in new[] { "deltaTime", "Time.time", "DateTime.Now", "DateTime.UtcNow" })
            {
                Assert.Less(region.IndexOf(forbidden, StringComparison.Ordinal), 0,
                    $"{LogPrefix} 유휴 구역에 「{forbidden}」이 들어왔습니다. " +
                    "프레임 델타는 기계가 잠든 시간을 잃고(T-3-c), 벽시계는 시계를 앞으로 돌리는 것만으로 " +
                    "동전을 만듭니다(T-3-a).");
            }
        }

        /// <summary>★ 위 테스트의 니들 <see cref="FocusSessionFlag"/>가 <b>실재를 가리키는지</b>
        /// 같은 실행에서 대조한다. 이 니들이 썩으면 위 단언은 «찾지 못했습니다»로 빨개지므로 조용히
        /// 초록이 되지는 않지만, <b>어느 쪽이 썩었는지</b>는 이 테스트가 가른다.</summary>
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
                "유휴 배선이 읽는 값이 실재하지 않거나 이름이 바뀌었습니다.");
        }

        [Test]
        public void 유휴_수급은_Update에서_롤오버_뒤에_돈다()
        {
            string code = CodeOfOrFail(All(), DirectorFileSuffix);
            string region = RegionOrFail(code, UpdateHeader);

            int rolloverAt = region.IndexOf("TickIfDue(", StringComparison.Ordinal);
            int idleAt = region.IndexOf("AccrueIdleIncome(", StringComparison.Ordinal);

            Assert.GreaterOrEqual(rolloverAt, 0, $"{LogPrefix} Update에서 롤오버 틱을 찾지 못했습니다.");
            Assert.GreaterOrEqual(idleAt, 0,
                $"{LogPrefix} Update에서 유휴 틱 호출을 찾지 못했습니다 — 유휴 수급은 매 틱 돌아야 합니다.");
            Assert.Less(rolloverAt, idleAt,
                $"{LogPrefix} 유휴 수급이 날짜 롤오버 <b>앞</b>에서 돕니다. 자정 직후 한 틱이 어제 " +
                "카운터를 보고, 상한에 걸려 조용히 0동전을 줍니다 — 화면에도 로그에도 흔적이 없습니다.");
        }

        // ====================================================================
        // §3. 할일 — 지급이 «완료 전이» 한 자리에만 있는가
        // ====================================================================

        [Test]
        public void 할일_보상은_UI가_아니라_완료_전이_한_곳에서_나간다()
        {
            List<CurrencySeedAndArcheryWiringTests.Source> all = All();

            // (1) 지급은 모델의 완료 전이 안에 있다.
            string todoCode = CodeOfOrFail(all, TodoModelFileSuffix);
            string region = RegionOrFail(todoCode, ToggleHeader);

            Assert.GreaterOrEqual(region.IndexOf("Completed", StringComparison.Ordinal), 0,
                $"{LogPrefix} 완료 전이 구역에서 Completed 갱신을 찾지 못했습니다 — 구역 앵커가 낡았습니다.");
            Assert.GreaterOrEqual(
                region.IndexOf("PayTodoDailyCoins(", StringComparison.Ordinal),
                0,
                $"{LogPrefix} 완료 전이(ToggleComplete) 안에서 보상 지급을 찾지 못했습니다. " +
                "지급이 다른 자리로 가면 «완료했다»의 정의가 두 벌이 됩니다.");

            // (2) ★ 부재 단언 — UI 두 곳은 지급을 <b>직접</b> 하지 않는다.
            //     부재 단언은 썩으면 조용히 초록이 되므로, 바로 아래에 <b>같은 파일 같은 스캐너</b>로
            //     잡히는 양성 대조를 붙인다(그 파일들이 완료 전이를 실제로 부르는가).
            string toggleCall = nameof(TodoListModel) + "." + nameof(TodoListModel.ToggleComplete) + "(";
            foreach (string uiSuffix in new[] { "/TodoPostItWidget.cs", "/TodoBoardPopover.cs" })
            {
                string uiCode = CodeOfOrFail(all, uiSuffix);

                Assert.GreaterOrEqual(uiCode.IndexOf(toggleCall, StringComparison.Ordinal), 0,
                    $"{LogPrefix} {uiSuffix}가 「{toggleCall}」를 부르지 않습니다 — " +
                    "아래 «지급을 직접 하지 않는다»가 «이 파일은 할일과 무관하다»와 구분되지 않습니다. " +
                    "양성 대조가 죽었으므로 이 실행의 부재 판정은 무효입니다.");

                Assert.Less(uiCode.IndexOf(TodoModelCall, StringComparison.Ordinal), 0,
                    $"{LogPrefix} {uiSuffix}가 「{TodoModelCall}」를 직접 부릅니다. " +
                    "지급처가 UI로 번지면 진입점마다 규칙이 갈리고, 새 진입점이 생기는 날 " +
                    "그 길로 체크한 사용자만 조용히 보상을 못 받습니다.");
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
        /// ★ <b>완료 전이 → 지급</b>이 실제로 이어지는가, 그리고 하루 1회인가.
        /// <para>여기서는 UI를 거치지 않고 <see cref="TodoListModel.ToggleComplete"/>를 직접 부른다 —
        /// 그것이 <b>두 UI가 공유하는 유일한 진입점</b>이고, 이 라운드가 지급을 얹은 자리다.</para>
        /// </summary>
        [Test]
        public void 할일을_체크하면_동전이_들어오고_같은_날_두_번째부터는_0이다()
        {
            const int SoftCap = 99;
            TodoListModel.Add("첫 번째 할일", SoftCap);
            TodoListModel.Add("두 번째 할일", SoftCap);
            Assert.AreEqual(2, TodoListModel.ActiveItems.Count, "전제 — 할일 두 개가 있어야 합니다.");

            int before = CurrencyModel.CoinBalance;
            Assert.IsFalse(CurrencyModel.TodoCoinPaidToday, "전제 — 오늘 아직 안 받았어야 합니다.");

            // ── 첫 완료 → 지급
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[0].Id);
            Assert.AreEqual(before + CurrencyRules.TodoDailyCoins, CurrencyModel.CoinBalance,
                "할일을 체크했는데 보상이 들어오지 않았습니다(또는 금액이 상수와 다릅니다).");
            Assert.IsTrue(CurrencyModel.TodoCoinPaidToday);
            Assert.IsTrue(CurrencyModel.IsDirty, "지급이 저장 대상으로 표시되지 않았습니다.");

            int afterFirst = CurrencyModel.CoinBalance;

            // ── 체크 해제 → 아무 일도 없다(되돌리기로 환불하지 않는다).
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[0].Id);
            Assert.AreEqual(afterFirst, CurrencyModel.CoinBalance,
                "체크를 해제했더니 잔액이 움직였습니다.");

            // ── 다시 체크 → 하루 1회이므로 0
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[0].Id);
            Assert.AreEqual(afterFirst, CurrencyModel.CoinBalance,
                "★ 체크를 껐다 켜는 것만으로 보상이 또 나왔습니다 — 무한 파밍입니다.");

            // ── 다른 할일을 완료해도 오늘은 0
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[1].Id);
            Assert.AreEqual(afterFirst, CurrencyModel.CoinBalance,
                "같은 날 두 번째 할일에도 보상이 나왔습니다 — 하루 1회 계약이 깨졌습니다.");
        }

        /// <summary>
        /// ★ 그 «하루 1회»가 <b>내일 다시 열리는가</b>. 이 라운드가 배선한 롤오버가 실제로
        /// <c>todoCoinPaidToday</c>를 되돌리는지까지 이어서 잰다(리더 요청).
        /// <para>일자는 <see cref="CurrencyModel.SetDayIndexForTesting"/>으로 «어제»로 만든다 —
        /// 벽시계를 밀어 넣을 구멍은 이 모델에 <b>일부러</b> 없다(T-3-a).</para>
        /// </summary>
        [Test]
        public void 할일_보상은_날짜가_바뀌면_다시_열린다()
        {
            TodoListModel.Add("오늘 할일", 99);
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[0].Id);
            Assert.IsTrue(CurrencyModel.TodoCoinPaidToday, "전제 — 오늘 보상을 받았어야 합니다.");

            int balanceBeforeRollover = CurrencyModel.CoinBalance;

            // 어제까지 본 것으로 만들고, 프로세스가 방금 켜진 상태(직전 리필 −∞)에서 한 번 굴린다.
            CurrencyModel.SetDayIndexForTesting(0);
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "롤오버가 일어나지 않았습니다.");

            Assert.IsFalse(CurrencyModel.TodoCoinPaidToday,
                "날짜가 바뀌었는데 [오늘 할일] 카운터가 그대로입니다 — 보상이 <b>영영</b> 다시 열리지 않습니다.");
            Assert.AreEqual(balanceBeforeRollover, CurrencyModel.CoinBalance,
                "롤오버가 지갑을 건드렸습니다 — 리셋되는 것은 «오늘의 예산»이지 «지갑»이 아닙니다.");

            // 새 날의 첫 완료는 다시 지급된다.
            TodoListModel.Add("내일 할일", 99);
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[TodoListModel.ActiveItems.Count - 1].Id);
            Assert.AreEqual(balanceBeforeRollover + CurrencyRules.TodoDailyCoins, CurrencyModel.CoinBalance,
                "날짜가 바뀐 뒤 첫 완료에 보상이 안 나왔습니다.");
        }

        /// <summary>
        /// ★ 유휴 수급이 <b>집중 세션 중에는 한 푼도 만들지 않는다</b>(모델 쪽 자).
        /// <para>배선 쪽 자는 §2(소스 순서)와 PlayMode(실제 세션)에 있다. 셋이 서로 다른 방법으로
        /// 같은 불변식을 재는 것이 의도다 — 하나가 썩어도 나머지가 남는다.</para>
        /// </summary>
        [Test]
        public void 집중_세션_중_유휴_틱은_창도_안_갉고_한_푼도_안_준다()
        {
            double windowBefore = CurrencyModel.IdleWindowUsedSeconds;

            Assert.AreEqual(0, CurrencyModel.TickIdleIncome(600.0, false, out _),
                "집중 세션 중(isIdleEarning=false)인데 유휴 수급이 지급됐습니다 — 같은 1초가 두 번 지급됩니다(I-7′).");
            Assert.AreEqual(windowBefore, CurrencyModel.IdleWindowUsedSeconds, 1e-9,
                "지급도 안 했으면서 8시간 창을 갉았습니다 — 그건 방어가 아니라 버그입니다(T-15-1-a).");
            Assert.IsFalse(CurrencyModel.IsDirty, "아무 일도 없었는데 저장 대상이 됐습니다.");

            // 양성 대조 — 같은 델타를 «유휴»로 넣으면 실제로 지급된다(위 0이 «아무것도 안 도는 것»이 아니다).
            Assert.Greater(CurrencyModel.TickIdleIncome(600.0, true, out _), 0,
                "유휴로 넣어도 0입니다 — 위의 «0동전»은 계약이 아니라 기능 부재입니다.");
        }

        /// <summary>
        /// ★★ <b>«0동전»과 «멈췄다»는 다른 사실이다</b> — 그 둘을 가르는 값이
        /// <c>windowSecondsSpent</c>라는 계약(2026-09-06).
        ///
        /// <para><b>왜 필요한가.</b> 요율이 <c>IdleCoinsPerMinute</c>(분당 12 = 초당 0.2)라
        /// <b>정상 상태에서도 프레임의 대부분이 0동전</b>이다(소수분은 다음 틱으로 넘어간다).
        /// 그 0을 정지로 읽던 <c>CharacterProgressionDirector.LogIdleStallOnce</c>가
        /// <b>5초에 한 줄(실측 720줄/시간)</b>을 찍었다. 그 오판을 구조적으로 불가능하게 만드는 것이
        /// 이 계약이고, 정지 판정이 이 값을 보는 한 «0동전 프레임»은 절대 정지로 읽히지 않는다.</para>
        ///
        /// <para>★ 이 테스트는 <b>모델의 계약</b>만 잰다. «디렉터가 실제로 조용한가»는 로그 줄 수를
        /// 세는 <c>Tests/PlayMode/CurrencyWiringRuntimeTests</c> §6이 잰다 — 서로 다른 자 두 개다.</para>
        /// </summary>
        [Test]
        public void 정상_프레임의_0동전과_정지의_0동전은_창_소비로_구별된다()
        {
            // 숫자를 베끼지 않는다 — «1동전이 안 되는 시간»을 요율에서 유도한다.
            double underOneCoinSeconds = 0.5 / CurrencyRules.IdleCoinsPerSecond;

            // ── (가) 정상. 0동전이지만 <b>창은 갉힌다</b>.
            int coins = CurrencyModel.TickIdleIncome(underOneCoinSeconds, true, out double windowSpent);
            Assert.AreEqual(0, coins,
                $"{LogPrefix} 전제 — 초당 {CurrencyRules.IdleCoinsPerSecond}동전이라 " +
                $"{underOneCoinSeconds:F1}초는 0동전이어야 합니다. 여기서 지급이 나오면 아래 대조가 공허합니다.");
            Assert.Greater(windowSpent, 0.0,
                $"{LogPrefix} ★ 0동전 프레임인데 창도 안 갉혔습니다 — 그러면 «정상»과 «정지»가 " +
                "관측상 완전히 같아지고, 정지 로그를 <b>매 프레임</b> 찍는 것 말고는 방법이 없어집니다.");

            // ── (나) 진짜 정지(오늘 상한 도달). 이번에는 창도 안 갉힌다.
            CurrencyModel.TickIdleIncome(
                CurrencyModel.DailyCapCoins() / CurrencyRules.IdleCoinsPerSecond, true, out _);
            Assert.AreEqual(0, CurrencyModel.RemainingDailyRoomCoins(),
                $"{LogPrefix} 전제 — 상한을 채우지 못하면 «정지»를 만들 수 없습니다.");

            Assert.AreEqual(0,
                CurrencyModel.TickIdleIncome(underOneCoinSeconds, true, out double stalledWindow),
                $"{LogPrefix} 상한에 걸렸는데 지급이 나왔습니다.");
            Assert.AreEqual(0.0, stalledWindow, 1e-12,
                $"{LogPrefix} 지급도 없이 8시간 창만 닳았습니다 — 방어가 아니라 버그입니다(T-15-1-a). " +
                "그리고 이 값이 0이 아니면 정지 판정이 «정지»를 영원히 못 봅니다.");
        }
    }
}
