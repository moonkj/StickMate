using System;
using System.Collections.Generic;

namespace StickMate.Core
{
    /// <summary>
    /// 유예 자동 해금(U-2)의 기산점 한 줄. ★ <b>v10에는 자리만 있고 로직이 없다</b>(§20-4).
    ///
    /// <para>스칼라 <c>float itemReachedAtSeconds</c>로 만들면 "없음 = 0.0 = 0초에 도달"이 되어
    /// <b>42종을 즉시 전부 열어 준다</b>(「없음 ≠ 0」 = 위험). 가변 길이 목록으로 만들면
    /// "없음 = 기록 없음"이라 그 자리에서 지금을 기산점으로 잡으면 되고, 그게 안전한 방향이다.
    /// U-2가 나중에 「존치」로 나와도 v11을 강제하지 않게 하려고 <b>필드 1개 · 로직 0줄</b>로
    /// 자리만 잡아 뒀다. 지금은 읽은 그대로 다시 쓰는 것 외에 아무 일도 하지 않는다.</para>
    /// </summary>
    [Serializable]
    public sealed class ItemGraceBaseline
    {
        public string itemId;
        public float reachedAtSeconds;
    }

    /// <summary>
    /// 저장 파일 ↔ <see cref="CurrencyModel"/> 사이를 오가는 한 덩어리.
    /// 인자 14개짜리 메서드를 만들지 않으려는 것이 전부다 — 순서만 어긋나도
    /// <c>coinBalance</c>와 <c>dayIndex</c>가 뒤바뀌는데 컴파일러가 둘 다 <c>int</c>라 못 잡는다.
    /// <para><b>직렬화 타입이 아니다</b>(<c>[Serializable]</c>가 없다) — 디스크 스키마는
    /// <see cref="CharacterSaveStore"/> 안에만 있다.</para>
    /// </summary>
    public struct CurrencySaveState
    {
        public int CoinBalance;
        public bool SeedGranted;
        public string[] PurchasedItemIds;
        public int[] StatTierReached;

        public int DayIndex;
        public int TodayGrantedCoins;
        public int PotionsUsedToday;
        public float IdleWindowUsedSeconds;

        public bool DayBoundaryOffsetSaved;
        public int DayBoundaryOffsetMinutes;

        public bool TodoCoinPaidToday;
        public int ArcheryCoinsToday;
        public int FocusXpToday;

        public string[] EquippedDanceIds;
        public ItemGraceBaseline[] ItemGraceBaselines;
    }

    /// <summary>
    /// ★ 동전 지갑 · 일일 래칫 · 구매 이력 · 등급 high-water mark · 장착한 춤 · 집중 모드 XP
    /// 일일 상한(v11)을 담는 모델. 관례는 다른 모델과 동일하다 — <b>값 보관 + IsDirty만 알고, 언제
    /// 저장할지는 모른다</b>(<see cref="CharacterSaveStore"/>가 읽고 쓰며, 주기 저장은
    /// <c>Interaction/CharacterProgressionDirector</c>).
    ///
    /// ============================================================================
    /// ★★ 벽시계 — 이 파일에 <b>정확히 한 곳</b> 있다
    /// ============================================================================
    /// security T-3-a는 <i>"수급·정산 판정 경로에서 <c>DateTime.Now</c>/<c>UtcNow</c>를 읽지 않는다"</i>이고,
    /// 오프라인 정산이 폐기되면서(§18-1) <b>그 예외는 0개가 됐다</b>.
    /// 남은 단 하나는 <b>"오늘이 며칠인가"</b>다 — 하루 경계는 원리상 달력을 봐야 정해진다.
    /// 그 읽기는 <see cref="ResolveTodayIndex"/> 안에만 있고, 그 값은 곧바로
    /// <see cref="CurrencyRules.LocalDayIndex"/>(순수 함수)로 넘어간다.
    /// <b>수급량 계산에는 단조 시계 델타만 쓴다</b> — 시계를 앞으로 돌려도 동전은 한 푼도 안 나온다.
    /// <para><c>Tests/EditMode/IncomeTimeSourceAuditTests</c>가 이 문장(1곳, 그리고 그 1곳이 어디인지)을
    /// 매 실행 집합 등호로 확인한다. "0건"이 아니라 "정확히 이 한 곳"이어야 부재 단언이 썩지 않는다.</para>
    ///
    /// ============================================================================
    /// ★ 저장하지 <b>않는</b> 것 — 여기가 이 설계의 절반이다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>오늘의 상한</b> — 필드가 아니라 함수다(<see cref="CurrencyRules.DailyCapCoins"/>, T-D-9).</item>
    ///  <item><b>회복제 보유 개수</b> — 재고는 스토어가 들고 있고 우리 파일엔 없다(T-D-8).
    ///    당일 소멸이라 누적 재고라는 개념 자체가 없다.</item>
    ///  <item><b>직전 리필 시각</b> — 프로세스 메모리에만(T-14-3-a). 세이브에 없으면 편집할 것도 없다.</item>
    ///  <item><b>활쏘기 쿨다운</b> — 단조 시계, 세션 안에서만(§20-3-b). 벽시계 유닉스 초를 저장하던
    ///    옛 설계는 시계를 600초 되감으면 즉시 재지급됐다.</item>
    ///  <item><b>보유한 춤</b> — 무료 2종(파생) ∪ 엔타이틀먼트(C층)라 저장할 것이 남지 않는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 로드값을 믿지 않는다 — 그러나 거부하지도 않는다 (T-14-5-b)
    /// ============================================================================
    /// <see cref="RestoreFromSave"/>는 모든 수치를 <see cref="CurrencyRules"/>의 클램프에 통과시킨다.
    /// 경고도 실패도 없다 — 이건 방어이기 전에 <b>위생</b>이고, 손상된 파일·구버전 파일에도 같은
    /// 코드가 같은 답을 낸다. <c>coinBalance</c>만은 상한을 걸지 않는다(§4: 막을 수 없고, 막으려 들면
    /// 정직한 유저를 잠근다).
    /// </summary>
    public static class CurrencyModel
    {
        // ====================================================================
        // A군 — 지갑 · 소유
        // ====================================================================

        /// <summary>지금 가진 동전. v9 파일에는 이 개념이 없었고, 그때의 0은
        /// <b>"한 푼도 없다"는 정확한 사실</b>이다(§20-1 #1).</summary>
        public static int CoinBalance { get; private set; }

        /// <summary>첫 실행 시드를 받은 적이 있는가. <see cref="CoinBalance"/>만으로는
        /// <b>"다 썼다"와 "받은 적 없다"가 구분되지 않는다</b>(§20-1 #2).</summary>
        public static bool SeedGranted { get; private set; }

        private static readonly List<string> s_purchasedItemIds = new List<string>();

        /// <summary>★ <b>상점에서 산 것만</b> 적힌다. 42종의 레벨 파생 보유는 여기 없고,
        /// <c>ItemCatalogEntry.IsOwned</c>가 <b>합집합</b>으로 둘을 본다(§20-2-a).
        /// <para>그 합집합 규칙이 이 필드의 하위 호환을 성립시킨다 — v9 파일에 이 키가 없어도
        /// 레벨 파생 항이 그대로 살아 있어 <b>아무것도 잃지 않는다</b>. 누가 이걸 「대체」로 바꾸면
        /// 그 순간 이 문장이 거짓이 되고, <c>Tests/EditMode/ItemOwnershipUnionTests</c>가 빨개진다.</para>
        /// <para>이름이 <c>ownedItemIds</c>가 아닌 이유: <c>own</c>은 세이브 스키마 금지 토큰이다
        /// (<c>EntitlementNotInSaveAuditTests</c>). 필드가 아직 없을 때 개명해 비용이 0이었다.</para></summary>
        public static IReadOnlyList<string> PurchasedItemIds => s_purchasedItemIds;

        private static readonly int[] s_statTierReached = new int[CurrencyRules.StatTierSlotCount];

        // ====================================================================
        // B군 — 일일 래칫
        // ====================================================================

        /// <summary>★ <b>래칫된 최대 일자</b>다(단순 "오늘"이 아니다 — T-4-b).
        /// 시계를 되감아도 이 값은 내려가지 않으므로 "오늘 연장" 공격이 성립하지 않는다.</summary>
        public static int DayIndex { get; private set; }

        /// <summary>오늘 <b>유휴로</b> 지급된 동전. 상한은 <see cref="CurrencyRules.DailyCapCoins"/>.</summary>
        public static int TodayGrantedCoins { get; private set; }

        /// <summary>오늘 쓴 회복제 개수(0~2). 무료 1개는 이 개수의 <b>첫 1회</b>라는 뜻일 뿐이라
        /// 별도 플래그가 없다(T-D-10).</summary>
        public static int PotionsUsedToday { get; private set; }

        /// <summary>오늘 갉아 먹은 8시간 창(초). ★ <b>지급이 실제로 일어난 초만</b> 쌓인다(T-D-13).</summary>
        public static double IdleWindowUsedSeconds { get; private set; }

        // ====================================================================
        // C군 — 날짜 경계 (★ 이 스키마에서 「없음 ≠ 0」인 유일한 값)
        // ====================================================================

        /// <summary>날짜 경계 오프셋을 고정한 적이 있는가.
        /// <para>★ 이 동반 불리언이 없으면 <see cref="DayBoundaryOffsetMinutes"/> 하나가
        /// <b>v10을 강제한다</b> — <c>0</c>은 "안 정했다"가 아니라 <b>UTC+0(영국)이라는 실재 값</b>이다.
        /// 이 저장소의 <c>gearPositionSaved</c>/<c>characterScaleSaved</c>/<c>inkColorSaved</c>/
        /// <c>preferredMonitorSaved</c>와 <b>똑같은 관례</b>로 해소했다(§20-1 #9).</para></summary>
        public static bool HasDayBoundaryOffset { get; private set; }

        /// <summary>고정된 날짜 경계 오프셋(분, UTC 기준). <b>첫 실행에 그 순간의 로컬 오프셋으로
        /// 고정하고 다시는 안 바꾼다</b>(T-D-4) — 시간대를 옮겨 다니며 하루 경계를 여러 번 넘기는
        /// 것을 막는다. iOS의 자동 시간대 변경에도 같은 방식으로 버틴다.</summary>
        public static int DayBoundaryOffsetMinutes { get; private set; }

        // ====================================================================
        // D군 — 채널별 일일 카운터
        // ====================================================================

        /// <summary>[오늘 할일] 하루 1회 지급을 이미 받았는가. 벽시계 유닉스 초를 저장하던
        /// <c>todoCoinPaidDateUnix</c>를 대체한다(T-3-a 위반이었다).</summary>
        public static bool TodoCoinPaidToday { get; private set; }

        /// <summary>오늘 활쏘기로 받은 동전. <c>lastArcheryCoinUnix</c>(벽시계)의 대체다(§20-3).</summary>
        public static int ArcheryCoinsToday { get; private set; }

        /// <summary>
        /// ★ v11 — 오늘 집중 모드(완주+취소)로 받은 XP. 상한은 <see cref="CurrencyRules.FocusXpDailyCap"/>
        /// (design-systems §15-4, 활쏘기 채널 상한과 동일한 1,080). <b>동전 카운터(<see cref="ArcheryCoinsToday"/>·
        /// <see cref="TodayGrantedCoins"/>)와 완전히 독립</b>이다 — 코인 상한에 도달해도 이 카운터는
        /// 별도로 계속 쌓이고, 이 카운터가 상한에 닿아도 코인은 영향받지 않는다.
        /// </summary>
        public static int FocusXpToday { get; private set; }

        // ====================================================================
        // E군 — 댄스
        // ====================================================================

        private static string[] s_equippedDanceIds = DanceIds.CreateFreeDefaults();

        /// <summary>지금 장착한 춤. <b>절대 비어 있지 않다</b>(§26-4-1 |장착| ≥ 1) —
        /// 비면 음악이 나와도 아무 일이 안 일어나고 그건 "고장"으로 읽힌다.</summary>
        public static IReadOnlyList<string> EquippedDanceIds => s_equippedDanceIds;

        // ====================================================================
        // U-2 자리 — 로직 없음, 왕복만
        // ====================================================================

        private static ItemGraceBaseline[] s_itemGraceBaselines = Array.Empty<ItemGraceBaseline>();

        /// <summary>유예 기산점 목록. ★ v10에서는 <b>읽은 그대로 다시 쓰기만</b> 한다(§20-4).
        /// 왕복시키는 이유: 무손실이어야 U-2가 나중에 「존치」로 나와도 v11이 필요 없다.</summary>
        public static IReadOnlyList<ItemGraceBaseline> ItemGraceBaselines => s_itemGraceBaselines;

        // ====================================================================
        // 더티 — 이 모델도 같은 파일에 실린다
        // ====================================================================

        /// <summary>마지막 저장 이후 값이 바뀌었는가(다른 모델의 <c>IsDirty</c>와 같은 역할).
        /// ★ <c>CharacterProgressionDirector.IsAnythingDirty()</c>에 <b>반드시</b> 합류해야 한다 —
        /// 빠뜨리면 동전이 60초 주기 저장에 실리지 않고 종료 시에도 안 실린다.</summary>
        public static bool IsDirty { get; private set; }

        internal static void MarkSaved() => IsDirty = false;

        // ====================================================================
        // ★ 세션 전용 상태 — 세이브에 없다(위조 대상 만들지 않기)
        // ====================================================================

        /// <summary>직전 리필의 단조 시각. 초기값이 <see cref="double.NegativeInfinity"/>라
        /// <b>앱을 새로 켠 뒤 첫 리필은 항상 통과</b>한다(T-14-3-a의 오탐 0 근거).</summary>
        private static double s_lastRefillMonotonic = double.NegativeInfinity;

        /// <summary>활쏘기 상금 쿨다운이 풀리는 단조 시각.</summary>
        private static double s_archeryCooldownUntilMonotonic = double.NegativeInfinity;

        /// <summary>아직 정수 1동전이 안 된 유휴 수입의 소수분. 세이브에 넣지 않는다
        /// (최대 1동전 미만이라 잃어도 무해하고, 필드 하나를 아끼는 쪽이 낫다).</summary>
        private static double s_idleCarryCoins;

        // ====================================================================
        // 지급 · 소비
        // ====================================================================

        /// <summary>
        /// 유휴 수급 한 틱. <paramref name="deltaSeconds"/>는 반드시
        /// <b>단조 시계 델타</b>(<c>Time.realtimeSinceStartupAsDouble</c>의 차)여야 한다 —
        /// 벽시계 델타를 넣으면 T-3-a가 그 자리에서 깨진다.
        ///
        /// <para><paramref name="isIdleEarning"/>은 <b>집중 세션이 아닐 때만</b> true여야 한다.
        /// 그 불리언 하나로 분기해 <b>집중 버킷 또는 유휴 버킷 중 하나에만</b> 더하는 것이
        /// 불변식 I-7′("같은 1초가 두 번 지급되지 않는다")를 <b>검증이 아니라 구조로</b> 참으로
        /// 만든다(GAME_ARCHITECTURE_REVIEW §12-3-b). 그림자 상태를 따로 만들지 마라.</para>
        ///
        /// <param name="windowSecondsSpent">
        /// ★★ 이번 틱이 8시간 창에서 <b>실제로 갉아먹은 초</b>. <b>«수급이 멈췄는가»의 유일한
        /// 관측값</b>이라 <c>out</c>으로 내보낸다 — 반환값(동전)으로는 그것을 알 수 없기 때문이다.
        ///
        /// <para>요율이 <c>IdleCoinsPerMinute</c>(분당 12 = 초당 0.2)라 <b>정상 상태에서도 프레임의
        /// 대부분이 0동전</b>이다(소수분은 <c>CarryCoins</c>로 다음 틱에 넘어간다). 그 0을 «멈췄다»로
        /// 읽으면 <b>정상 동작을 고장으로 신고</b>하게 된다 — 2026-09-06에 실제로 그 일이 났다
        /// (<c>Interaction/CharacterProgressionDirector.LogIdleStallOnce</c>가 5초에 한 줄씩,
        /// <b>실측 720줄/시간</b>). 게다가 그 720줄이 <b>전부</b> «오늘의 지급 가능 시간(480분)을 다
        /// 썼습니다»라고 적혔다 — 실제로는 창을 60분밖에 안 쓴 시점이었다.</para>
        ///
        /// <para>반대로 이 값은 <b>지급이 실제로 일어난 초에만</b> 값이 있다(T-15-1-a·T-D-13). 그래서
        /// <paramref name="isIdleEarning"/>이 true이고 <paramref name="deltaSeconds"/>가 양수인데
        /// 이 값이 0이면, 그때가 진짜로 멈춘 것이다 — 이유는 <b>일일 상한</b>
        /// (<see cref="RemainingDailyRoomCoins"/> = 0) 또는 <b>창 소진</b>
        /// (<see cref="RemainingIdleWindowSeconds"/> = 0) 둘뿐이다.</para></param>
        /// <returns>이번 틱에 실제로 지급된 동전.</returns>
        /// </summary>
        public static int TickIdleIncome(double deltaSeconds, bool isIdleEarning, out double windowSecondsSpent)
        {
            CurrencyRules.IdleTickResult tick = CurrencyRules.IdleTick(
                deltaSeconds, isIdleEarning, TodayGrantedCoins, PotionsUsedToday,
                IdleWindowUsedSeconds, CurrencyRules.IdleWindowCapSeconds, s_idleCarryCoins);

            s_idleCarryCoins = tick.CarryCoins;
            windowSecondsSpent = tick.WindowSecondsSpent;
            if (tick.CoinsGranted <= 0 && tick.WindowSecondsSpent <= 0.0) return 0;

            IdleWindowUsedSeconds = CurrencyRules.ClampIdleWindowSeconds(
                IdleWindowUsedSeconds + tick.WindowSecondsSpent);

            if (tick.CoinsGranted > 0)
            {
                TodayGrantedCoins = CurrencyRules.ClampGrantedCoins(
                    TodayGrantedCoins + tick.CoinsGranted, PotionsUsedToday);
                CoinBalance = CurrencyRules.ClampCoinBalance(CoinBalance + tick.CoinsGranted);
            }

            IsDirty = true;
            return tick.CoinsGranted;
        }

        // ====================================================================
        // ★★ 집중 모드 지급 — 유휴와 <b>완전히 분리된</b> 진입점 (I-7′)
        // ====================================================================
        //
        // ★ <b>왜 <see cref="TickIdleIncome"/>에 얹지 않는가.</b> 그 함수의 <c>isIdleEarning</c> 분기가
        //   "같은 1초가 두 번 지급되지 않는다"(I-7′)를 <b>검증이 아니라 구조로</b> 참으로 만들고 있다.
        //   집중을 그 분기 안으로 밀어 넣으면 그 구조가 무너진다. 그래서 별도 진입점이고,
        //   <c>isIdleEarning</c>은 <b>지금까지처럼</b> 집중 세션 중 false여야 한다(호출부 계약, 불변).
        //
        // ★★ <b>집중 지급은 「일일 상한 밖」이다</b> — DESIGN_SYSTEMS_STATS §22-13(§18-5 인용):
        //   <i>"집중 지급은 일일 상한 밖이다(캡 도달 후에도 집중 25분은 전액 600)."</i>
        //   그래서 아래 두 함수는 <see cref="TodayGrantedCoins"/>를 <b>건드리지 않고</b>,
        //   <c>ClampGrantedCoins</c>를 <b>통과시키지 않으며</b>, <see cref="IdleWindowUsedSeconds"/>도
        //   <b>갉지 않는다</b>. 잔액에 더할 때 <c>ClampCoinBalance</c>(하한 0)만 지난다.
        //   ⚠ 이 파일의 다른 지급들과 모양이 달라 보인다고 「관례에 맞춰」 <c>ClampGrantedCoins</c>를
        //   끼우지 마라 — 그 순간 집중 수입이 1,500에서 <b>조용히</b> 막히고, 그 실패는 초록 테스트와
        //   똑같이 생겼다(<c>TodayGrantedCoins</c>는 정의상 "오늘 <b>유휴로</b> 지급된 동전"이다).
        //
        // 산식은 여기 없다 — <see cref="CurrencyRules.FocusCompletionCoins"/> ·
        // <see cref="CurrencyRules.FocusCancelCoins"/> 한 곳에만 있고, 이 모델은 결과를 담기만 한다.

        /// <summary>집중 세션 <b>완주</b> 지급. 인자는 <b>명목 세션 길이(초)</b>다(계측 누적값이 아니다 —
        /// 이유는 <see cref="CurrencyRules.FocusCompletionCoins"/>).</summary>
        /// <returns>실제로 지급된 동전(0이면 지급 없음).</returns>
        public static int PayFocusCompletionCoins(double sessionDurationSeconds)
            => GrantFocusCoins(CurrencyRules.FocusCompletionCoins(sessionDurationSeconds));

        /// <summary>집중 세션 <b>중도 취소</b> 지급. 인자는 <c>명목 세션 길이 − 잔여 초</c>다.
        /// 1분 미만이면 0을 돌려주고 아무 일도 하지 않는다.</summary>
        /// <returns>실제로 지급된 동전(0이면 지급 없음).</returns>
        public static int PayFocusCancelCoins(double elapsedSeconds)
            => GrantFocusCoins(CurrencyRules.FocusCancelCoins(elapsedSeconds));

        /// <summary>두 집중 경로가 공유하는 <b>유일한</b> 잔액 반영 지점.
        /// 0동전이면 <see cref="IsDirty"/>를 세우지 않는다 — 1분 미만 취소를 반복해도 디스크를
        /// 두드리지 않는다(하루 종일 켜져 있는 앱이다).</summary>
        private static int GrantFocusCoins(int coins)
        {
            if (coins <= 0) return 0;
            CoinBalance = CurrencyRules.ClampCoinBalance(CoinBalance + coins);
            IsDirty = true;
            return coins;
        }

        // ====================================================================
        // ★★ 집중 모드 XP 지급 — 위 동전 지급과 나란히, 그러나 <b>독립된</b> 상한 (v11)
        // ====================================================================
        //
        // ★ 동전과 <b>여기서 갈라진다</b>: 동전 두 함수(위)는 일일 상한 밖(§22-13)이라 클램프를
        //   지나지 않지만, XP는 design-systems §15-4가 새로 도입한 상한
        //   (<see cref="CurrencyRules.FocusXpDailyCap"/>)을 반드시 지난다. 그래서 아래 두 함수는
        //   <see cref="FocusXpToday"/>를 갉고, 코인 버킷(<see cref="TodayGrantedCoins"/>·
        //   <see cref="ArcheryCoinsToday"/>·<see cref="IdleWindowUsedSeconds"/>)은 <b>전혀 건드리지
        //   않는다</b> — 두 경제가 서로 다른 지갑을 쓴다(I-7′과 같은 "그림자 상태 금지" 원칙의
        //   XP 버전: 상한도 하나만, 카운터도 하나만).
        //
        // 산식은 여기 없다 — <see cref="CurrencyRules.FocusCompletionXp"/> · <see cref="CurrencyRules.FocusCancelXp"/>
        // 한 곳에만 있고, 이 모델은 상한 클램프와 카운터만 담당한다.

        /// <summary>집중 세션 <b>완주</b> XP 지급. 인자는 <b>명목 세션 길이(초)</b>다(계측 누적값이 아니다 —
        /// 이유는 <see cref="CurrencyRules.FocusCompletionXp"/>). 오늘 이미 상한 근처면 <b>일부만</b>
        /// 지급될 수 있다(마지막 한 조각을 완전히 버리지 않는다 — <see cref="TryAwardArcheryCoins"/>의
        /// "room보다 크면 room만큼만" 관례와 동일).</summary>
        /// <returns>실제로 지급된 XP(0이면 오늘 상한에 이미 도달).</returns>
        public static int TryGrantFocusCompletionXp(double sessionDurationSeconds)
            => GrantFocusXp(CurrencyRules.FocusCompletionXp(sessionDurationSeconds));

        /// <summary>집중 세션 <b>중도 취소</b> XP 지급. 인자는 <c>명목 세션 길이 − 잔여 초</c>다.
        /// 1분 미만이면 산식 자체가 0을 내므로 이 함수도 0을 돌려주고 아무 일도 하지 않는다.</summary>
        /// <returns>실제로 지급된 XP(0이면 1분 미만이거나 오늘 상한에 도달).</returns>
        public static int TryGrantFocusCancelXp(double elapsedSeconds)
            => GrantFocusXp(CurrencyRules.FocusCancelXp(elapsedSeconds));

        /// <summary>두 집중 XP 경로가 공유하는 <b>유일한</b> 카운터 반영 지점 — 코인의
        /// <see cref="GrantFocusCoins"/>와 대칭이지만 <b>상한을 지운다는 점이 다르다</b>(§22-13은
        /// 코인 한정 확정 사항, design-systems §15-4).</summary>
        private static int GrantFocusXp(int rawXp)
        {
            if (rawXp <= 0) return 0;

            int room = CurrencyRules.FocusXpDailyCap - FocusXpToday;
            if (room <= 0) return 0;

            int pay = rawXp > room ? room : rawXp;
            FocusXpToday = CurrencyRules.ClampFocusXpToday(FocusXpToday + pay);
            IsDirty = true;
            return pay;
        }

        /// <summary>오늘 집중 모드로 더 받을 수 있는 XP. 화면/로그가 "오늘 상한 도달"을 판정할 때 쓴다.</summary>
        public static int RemainingFocusXpRoomToday()
        {
            int room = CurrencyRules.FocusXpDailyCap - FocusXpToday;
            return room < 0 ? 0 : room;
        }

        /// <summary>집중 모드 XP가 <b>오늘 상한에 걸려</b> 멈췄는가. 활쏘기의
        /// <see cref="ArcheryDailyLimitReached"/>와 같은 이유로 존재한다 — 연출(세션 완주/취소)은
        /// 그대로 도는데 XP만 안 늘면 "고장"으로 읽힌다.</summary>
        public static bool FocusXpDailyLimitReached => FocusXpToday >= CurrencyRules.FocusXpDailyCap;

        /// <summary>오늘의 상한(= <c>1500 + 500 × clamp(회복제, 0, 2)</c>). ★ 필드가 아니라 함수다.</summary>
        public static int DailyCapCoins() => CurrencyRules.DailyCapCoins(PotionsUsedToday);

        /// <summary>오늘 유휴로 더 받을 수 있는 동전. 화면의 「1,240 / 2,000」의 왼쪽이자 분모다.</summary>
        public static int RemainingDailyRoomCoins()
        {
            int room = DailyCapCoins() - TodayGrantedCoins;
            return room < 0 ? 0 : room;
        }

        /// <summary>오늘 유휴 수급 창(8시간)에 <b>남은 초</b>. <see cref="RemainingDailyRoomCoins"/>의
        /// 창 버전이고, 「남은 시간」을 말하는 화면·로그의 <b>유일한 출처</b>다.
        /// <para>★ 뺄셈을 호출부에 흩지 않기 위해 여기 둔다 — 클램프
        /// (<c>CurrencyRules.ClampIdleWindowSeconds</c>: NaN은 0이 아니라 <b>상한</b>으로 간다)를
        /// 빠뜨린 사본이 하나라도 생기면 손상된 파일에서 «남은 시간»만 다른 값을 말하게 된다.</para></summary>
        public static double RemainingIdleWindowSeconds()
        {
            double room = CurrencyRules.IdleWindowCapSeconds
                          - CurrencyRules.ClampIdleWindowSeconds(IdleWindowUsedSeconds);
            return room < 0.0 ? 0.0 : room;
        }

        /// <summary>
        /// 회복제 1개를 쓴다. ★ 성공하면 오늘의 상한이 +500 된다.
        /// <para><b>순서 규범(T-14-5-e)</b>: 유료분이라면 <b>① 여기서 상한을 올리고 → ② 스토어에
        /// 소비를 통지</b>한다. 반대로 하면 중간에 죽었을 때 <b>유저가 돈 낸 회복제를 잃는다</b> —
        /// 이 앱이 낼 수 있는 최악의 오답이다. ①②면 최악이 개발자의 500동전 손해이고,
        /// 그마저 하루 2개 clamp 때문에 유한하다.</para>
        /// </summary>
        /// <returns>실제로 썼으면 true. 이미 오늘 2개를 썼으면 false.</returns>
        public static bool TryUsePotion()
        {
            if (PotionsUsedToday >= CurrencyRules.MaxPotionsPerDay) return false;
            PotionsUsedToday = CurrencyRules.ClampPotionsUsed(PotionsUsedToday + 1);
            IsDirty = true;
            return true;
        }

        /// <summary>[오늘 할일] 하루 1회 지급. 이미 받았으면 0을 돌려주고 아무 일도 하지 않는다.</summary>
        public static int TryPayTodoDailyCoins()
        {
            if (TodoCoinPaidToday) return 0;
            TodoCoinPaidToday = true;
            CoinBalance = CurrencyRules.ClampCoinBalance(CoinBalance + CurrencyRules.TodoDailyCoins);
            IsDirty = true;
            return CurrencyRules.TodoDailyCoins;
        }

        /// <summary>
        /// 활쏘기 상금. 두 관문을 지난다 —
        /// ① <b>세션 쿨다운</b>(단조 시계, 저장 안 함) ② <b>일일 총량</b>(저장).
        /// <para>①만 있으면 앱 재시작으로 무한 파밍이 된다. 그 사실을 숨기지 않고 ②로 상한을 건다
        /// (§20-3-b). ②의 값은 <b>U-41 미확정</b>이라
        /// <see cref="CurrencyRules.ArcheryDailyAwardLimit"/>에 임시값으로 열려 있다.</para>
        /// </summary>
        /// <param name="nowMonotonic"><c>Time.realtimeSinceStartupAsDouble</c>. 벽시계가 아니다.</param>
        /// <returns>지급된 동전(0이면 쿨다운 중이거나 오늘 상한에 도달).</returns>
        public static int TryAwardArcheryCoins(double nowMonotonic)
        {
            if (nowMonotonic < s_archeryCooldownUntilMonotonic) return 0;

            int room = CurrencyRules.ArcheryDailyCoinLimit - ArcheryCoinsToday;
            if (room <= 0) return 0;

            int pay = CurrencyRules.ArcheryCoinsPerAward;
            if (pay > room) pay = room;

            s_archeryCooldownUntilMonotonic = nowMonotonic + CurrencyRules.ArcheryAwardCooldownSeconds;
            ArcheryCoinsToday = CurrencyRules.ClampArcheryCoinsToday(ArcheryCoinsToday + pay);
            CoinBalance = CurrencyRules.ClampCoinBalance(CoinBalance + pay);
            IsDirty = true;
            return pay;
        }

        /// <summary>활쏘기 상금이 <b>오늘 상한에 걸려</b> 멈췄는가. 연출은 계속 도는데 동전만 안 나오면
        /// 사용자는 그것을 "고장"으로 읽는다 — 문구가 필요한 자리를 화면 쪽에 알려 주는 값이다(§20-8).</summary>
        public static bool ArcheryDailyLimitReached => ArcheryCoinsToday >= CurrencyRules.ArcheryDailyCoinLimit;

        /// <summary>첫 실행 시드 — <b>평생 1회</b>. 지급액은 <see cref="CurrencyRules.SeedCoins"/>다.
        /// <para>★ <b>U-42가 닫혔다</b>(리더 승인 2026-09-05, 1,200). 그 전까지 금액이 0이라
        /// 이 함수는 <b>아무 일도 하지 않고 플래그도 안 세웠고</b>, 그 덕분에 <b>기존 사용자 전원의
        /// <c>seedGranted</c>가 <c>false</c>로 남아 지금 첫 지급 대상이 된다</b>. 순서를 반대로
        /// (플래그 먼저) 했으면 되돌릴 수 없는 손실이었다 — <c>CurrencyRules.SeedCoins</c> 문서 참고.</para>
        /// <para>★ <b>부르는 쪽은 아직 없다</b>(첫 실행 지급 배선은 다음 라운드). 이 함수가 도는 순간
        /// 잔액이 늘고 <see cref="IsDirty"/>가 서므로, 배선하는 라운드는 <b>저장 시점</b>까지 같이 봐야 한다.</para></summary>
        public static int TryGrantSeedCoins()
        {
            if (!CurrencyRules.CanGrantSeed(SeedGranted)) return 0;
            SeedGranted = true;
            CoinBalance = CurrencyRules.ClampCoinBalance(CoinBalance + CurrencyRules.SeedCoins);
            IsDirty = true;
            return CurrencyRules.SeedCoins;
        }

        /// <summary>상점 구매 — 잔액을 깎고 구매 이력에 남긴다. 부족하면 아무 일도 하지 않는다.
        /// <para>★ <b>경제 원칙 E-1(I-15)</b>: 같은 사용자가 <b>같은 것을 두 번 살 수 있으면</b> 위반이다.
        /// 여기가 그 선이 실제로 그어지는 자리라, 이미 산 것은 다시 팔지 않는다.</para></summary>
        public static bool TryPurchaseItem(string itemId, int priceCoins)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (priceCoins < 0) return false;
            if (IsPurchasedItem(itemId)) return false;      // E-1 — 반복 구매 금지
            if (CoinBalance < priceCoins) return false;

            CoinBalance = CurrencyRules.ClampCoinBalance(CoinBalance - priceCoins);
            s_purchasedItemIds.Add(itemId);
            IsDirty = true;
            return true;
        }

        /// <summary>이 아이템을 <b>상점에서</b> 산 적이 있는가. 레벨 파생 보유는 여기 안 본다 —
        /// 합쳐서 보는 곳은 <c>ItemCatalogEntry.IsOwned</c> 하나다.</summary>
        public static bool IsPurchasedItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            for (int i = 0; i < s_purchasedItemIds.Count; i++)
            {
                if (string.Equals(s_purchasedItemIds[i], itemId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>등급 high-water mark 읽기. 범위를 벗어난 슬롯은 0이다.</summary>
        public static int StatTierReached(int slotIndex)
            => slotIndex < 0 || slotIndex >= s_statTierReached.Length ? 0 : s_statTierReached[slotIndex];

        /// <summary>등급 high-water mark 갱신 — <b>내려가지 않는다</b>(영구 해금).
        /// 평생 최대 <c>슬롯 4 × 등급 3 = 12</c>회만 참을 돌려준다.</summary>
        /// <returns>실제로 올라갔으면 true(그때만 저장할 값이 생긴다).</returns>
        public static bool RaiseStatTier(int slotIndex, int tier)
        {
            if (slotIndex < 0 || slotIndex >= s_statTierReached.Length) return false;
            int clamped = CurrencyRules.ClampStatTier(tier);
            if (clamped <= s_statTierReached[slotIndex]) return false;
            s_statTierReached[slotIndex] = clamped;
            IsDirty = true;
            return true;
        }

        /// <summary>장착한 춤을 갈아 끼운다. 정규화(§20-2-b)를 거치므로 <b>빈 집합으로 만들 수 없다</b>.</summary>
        public static void SetEquippedDanceIds(string[] danceIds, Func<string, bool> isAvailable)
        {
            string[] normalized = DanceIds.Normalize(danceIds, isAvailable);
            if (SameSequence(s_equippedDanceIds, normalized)) return;
            s_equippedDanceIds = normalized;
            IsDirty = true;
        }

        private static bool SameSequence(string[] a, string[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        // ====================================================================
        // ★ 날짜 롤오버 — 상한 리셋 · 무료 회복제 부활 · 창 리셋이 「하나의 사건」이다
        //    (T-D-10 · T-D-14 · I-15′)
        // ====================================================================
        // 셋을 다른 조건으로 나누면 (카) 공격이 각 경계를 따로 넘어 상금이 배가 되고,
        // T-14-3-a 방어를 <b>세 곳에</b> 걸어야 한다. 한 곳만 빠뜨려도 조용히 새는 문이 된다.
        // 그래서 이 저장소에서 그 셋을 되돌리는 코드는 <see cref="ApplyDayRollover"/> 하나뿐이다.

        /// <summary>
        /// ★ <b>이 클래스에서 시각을 읽는 유일한 지점.</b> 오늘의 일자 번호를 구하고,
        /// 아직 오프셋을 고정한 적이 없으면 <b>지금 이 순간의 로컬 오프셋으로 고정</b>한다(T-D-4).
        /// <para>여기서 나온 정수 하나만 판정에 쓴다 — 수급량은 이 값을 보지 않는다.</para>
        /// </summary>
        private static int ResolveTodayIndex()
        {
            DateTime utcNow = DateTime.UtcNow;

            if (!HasDayBoundaryOffset)
            {
                DayBoundaryOffsetMinutes = CurrencyRules.ClampDayBoundaryOffsetMinutes(
                    (int)TimeZoneInfo.Local.GetUtcOffset(utcNow).TotalMinutes);
                HasDayBoundaryOffset = true;
                IsDirty = true;
            }

            return CurrencyRules.LocalDayIndex(utcNow, DayBoundaryOffsetMinutes);
        }

        /// <summary>
        /// 날짜가 넘어갔는지 보고, 넘어갔으면 <b>한 번에</b> 되돌린다.
        /// </summary>
        /// <param name="nowMonotonic"><c>Time.realtimeSinceStartupAsDouble</c>.</param>
        /// <returns>이번 호출이 리필을 일으켰으면 true.</returns>
        public static bool TickDayRollover(double nowMonotonic)
        {
            int today = ResolveTodayIndex();
            if (!CurrencyRules.MayRefill(today, DayIndex, nowMonotonic, s_lastRefillMonotonic)) return false;

            ApplyDayRollover(today, nowMonotonic);
            return true;
        }

        private static void ApplyDayRollover(int today, double nowMonotonic)
        {
            DayIndex = today;                 // 래칫 — 전진만 한다
            TodayGrantedCoins = 0;            // ① 일일 상한 리셋
            PotionsUsedToday = 0;             // ② 무료 회복제 부활(= 첫 1회가 다시 무상)
            IdleWindowUsedSeconds = 0.0;      // ③ 8시간 창 리셋 (T-15-1-d)
            TodoCoinPaidToday = false;
            ArcheryCoinsToday = 0;
            FocusXpToday = 0;          // v11 — 활쏘기와 같은 이유로 같은 사건에 실린다(I-15′ 확장).

            s_lastRefillMonotonic = nowMonotonic;
            s_idleCarryCoins = 0.0;
            IsDirty = true;
        }

        // ====================================================================
        // 영속화 (저장 스키마 v11 — v10 게임화 묶음 + v11 집중 모드 XP 일일 상한)
        // ====================================================================

        /// <summary>
        /// 저장 파일에서 되살린다. <b>이벤트를 쏘지 않는다</b> — 복원 도중의 중간 상태를 UI가
        /// 그리지 않게 하는 관례(<see cref="CharacterSaveStore.Load"/>가 전부 끝난 뒤 한 번만 통지).
        ///
        /// <para>v9 이하 파일에는 이 필드들이 <b>하나도 없다</b>. JsonUtility가 0/false/null로 채우고,
        /// 그 값들이 전부 <b>"오늘 아무것도 안 받았다 / 아직 아무것도 안 샀다"</b>는 정확한 사실이다.
        /// 유일한 예외였던 <c>dayBoundaryOffsetMinutes</c>는 동반 불리언
        /// <c>dayBoundaryOffsetSaved</c>가 해소한다.</para>
        /// </summary>
        internal static void RestoreFromSave(CurrencySaveState state)
        {
            CoinBalance = CurrencyRules.ClampCoinBalance(state.CoinBalance);
            SeedGranted = state.SeedGranted;

            s_purchasedItemIds.Clear();
            if (state.PurchasedItemIds != null)
            {
                for (int i = 0; i < state.PurchasedItemIds.Length; i++)
                {
                    string id = state.PurchasedItemIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    if (IsPurchasedItem(id)) continue;      // 중복은 파일 손상의 흔적 — 조용히 한 개로 만든다
                    s_purchasedItemIds.Add(id);
                }
            }

            for (int i = 0; i < s_statTierReached.Length; i++)
            {
                int raw = state.StatTierReached != null && i < state.StatTierReached.Length
                    ? state.StatTierReached[i] : 0;
                s_statTierReached[i] = CurrencyRules.ClampStatTier(raw);
            }

            DayIndex = state.DayIndex < 0 ? 0 : state.DayIndex;
            PotionsUsedToday = CurrencyRules.ClampPotionsUsed(state.PotionsUsedToday);
            TodayGrantedCoins = CurrencyRules.ClampGrantedCoins(state.TodayGrantedCoins, PotionsUsedToday);
            IdleWindowUsedSeconds = CurrencyRules.ClampIdleWindowSeconds(state.IdleWindowUsedSeconds);

            HasDayBoundaryOffset = state.DayBoundaryOffsetSaved;
            DayBoundaryOffsetMinutes = HasDayBoundaryOffset
                ? CurrencyRules.ClampDayBoundaryOffsetMinutes(state.DayBoundaryOffsetMinutes)
                : 0;

            TodoCoinPaidToday = state.TodoCoinPaidToday;
            ArcheryCoinsToday = CurrencyRules.ClampArcheryCoinsToday(state.ArcheryCoinsToday);
            // v10 이하 파일에는 이 키가 없다 — 0으로 채워지고, 그 0은 "오늘 집중 모드로 받은 적
            // 없다"는 정확한 사실이다(archeryCoinsToday와 같은 종류).
            FocusXpToday = CurrencyRules.ClampFocusXpToday(state.FocusXpToday);

            // ★ null과 빈 배열을 같은 분기가 받는다(§20-2-b). 보유 판정은 아직 배선 전이라 null —
            //   "아는 아이디는 전부 허용"으로 떨어지고, 보유/장착 화면 라운드가 여기에 조회를 꽂는다.
            s_equippedDanceIds = DanceIds.Normalize(state.EquippedDanceIds, null);

            s_itemGraceBaselines = state.ItemGraceBaselines ?? Array.Empty<ItemGraceBaseline>();

            // 세션 전용 상태는 파일에서 오지 않는다 — 로드가 이것들을 되살리면 그 순간
            // "앱 재시작으로 쿨다운 초기화"가 아니라 "파일 편집으로 초기화"가 되어 표적이 하나 는다.
            s_lastRefillMonotonic = double.NegativeInfinity;
            s_archeryCooldownUntilMonotonic = double.NegativeInfinity;
            s_idleCarryCoins = 0.0;

            IsDirty = false;
        }

        /// <summary>저장 파일에 적을 한 덩어리.</summary>
        internal static CurrencySaveState CaptureSaveState()
        {
            return new CurrencySaveState
            {
                CoinBalance = CoinBalance,
                SeedGranted = SeedGranted,
                PurchasedItemIds = s_purchasedItemIds.ToArray(),
                StatTierReached = (int[])s_statTierReached.Clone(),

                DayIndex = DayIndex,
                TodayGrantedCoins = TodayGrantedCoins,
                PotionsUsedToday = PotionsUsedToday,
                IdleWindowUsedSeconds = (float)IdleWindowUsedSeconds,

                DayBoundaryOffsetSaved = HasDayBoundaryOffset,
                DayBoundaryOffsetMinutes = HasDayBoundaryOffset ? DayBoundaryOffsetMinutes : 0,

                TodoCoinPaidToday = TodoCoinPaidToday,
                ArcheryCoinsToday = ArcheryCoinsToday,
                FocusXpToday = FocusXpToday,

                EquippedDanceIds = (string[])s_equippedDanceIds.Clone(),
                ItemGraceBaselines = s_itemGraceBaselines,
            };
        }

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).</summary>
        public static void ResetForTesting()
        {
            CoinBalance = 0;
            SeedGranted = false;
            s_purchasedItemIds.Clear();
            Array.Clear(s_statTierReached, 0, s_statTierReached.Length);

            DayIndex = 0;
            TodayGrantedCoins = 0;
            PotionsUsedToday = 0;
            IdleWindowUsedSeconds = 0.0;

            HasDayBoundaryOffset = false;
            DayBoundaryOffsetMinutes = 0;

            TodoCoinPaidToday = false;
            ArcheryCoinsToday = 0;
            FocusXpToday = 0;

            s_equippedDanceIds = DanceIds.CreateFreeDefaults();
            s_itemGraceBaselines = Array.Empty<ItemGraceBaseline>();

            s_lastRefillMonotonic = double.NegativeInfinity;
            s_archeryCooldownUntilMonotonic = double.NegativeInfinity;
            s_idleCarryCoins = 0.0;

            IsDirty = false;
        }

        /// <summary>테스트 전용 — 롤오버 이후의 상태를 만들지 않고 <b>일자만</b> 밀어 놓는다.
        /// (실제 롤오버는 <see cref="TickDayRollover"/> 하나만 일으킨다 — 그 단일성이 I-15′다.)</summary>
        internal static void SetDayIndexForTesting(int dayIndex) => DayIndex = dayIndex;
    }
}
