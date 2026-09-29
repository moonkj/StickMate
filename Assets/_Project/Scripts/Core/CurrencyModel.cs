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
    ///
    /// <para>★★★ <b>2026-09-29 DLC·재화 폐지 R5 — 이 필드 목록은 한 칸도 바뀌지 않았다.</b>
    /// 재화가 폐지됐지만 <b>필드를 지우지 않았다</b>: 이 그릇에는 폐기된 축과 살아 있는 축이
    /// <b>섞여</b> 있고(<c>DayIndex</c>는 「오늘」의 유일 출처, <c>StatTierReached</c>는 등급 영구 해금,
    /// <c>FocusXpToday</c>·<c>EquippedDanceIds</c>도 그대로 쓰인다), 그릇째 지우면 그것들이 함께
    /// 사라진다. 그리고 필드를 <b>빼는</b> 변경 역시 <c>CharacterSaveStore.CurrentVersion</c>을
    /// 올려야 하는 변경이다 — 이번 라운드의 계약은 <b>버전 불변</b>이다.
    /// <list type="bullet">
    ///   <item><b>살아 있는 축</b>: <c>StatTierReached</c> · <c>DayIndex</c> ·
    ///     <c>DayBoundaryOffsetSaved</c>/<c>DayBoundaryOffsetMinutes</c> · <c>ArcheryCoinsToday</c>
    ///     (단위가 「동전」→「관문 단위」로 바뀌었다) · <c>FocusXpToday</c> · <c>EquippedDanceIds</c>.</item>
    ///   <item><b>폐기(왕복만)</b>: <c>CoinBalance</c> · <c>SeedGranted</c> · <c>PurchasedItemIds</c> ·
    ///     <c>TodayGrantedCoins</c> · <c>PotionsUsedToday</c> · <c>IdleWindowUsedSeconds</c> ·
    ///     <c>TodoCoinPaidToday</c> · <c>ItemGraceBaselines</c>.</item>
    /// </list></para>
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
    /// ★ 하루 경계 래칫 · 등급 high-water mark · 장착한 춤 · 집중 모드 XP 일일 상한 ·
    /// <b>그리고 폐기된 재화 필드들의 무손실 왕복</b>을 담는 모델. 관례는 다른 모델과 동일하다 —
    /// <b>값 보관 + IsDirty만 알고, 언제 저장할지는 모른다</b>(<see cref="CharacterSaveStore"/>가
    /// 읽고 쓰며, 주기 저장은 <c>Interaction/CharacterProgressionDirector</c>).
    ///
    /// ============================================================================
    /// ★★★ 2026-09-29 DLC·재화 폐지 R5 — <b>동전이 늘어나는 프로덕션 경로가 0개가 됐다</b>
    /// ============================================================================
    /// 사용자 확정(DLC·재화 폐지, 1회 구매 전환). 지운 지급/차감 진입점:
    /// <list type="bullet">
    ///   <item><c>TickIdleIncome</c> — 유휴 수급 1틱(그리고 세션 전용 소수분 <c>s_idleCarryCoins</c>).</item>
    ///   <item><c>TryPayTodoDailyCoins</c> — [오늘 할일] 하루 1회 정액.</item>
    ///   <item><c>TryGrantSeedCoins</c> — 첫 실행 시드.</item>
    ///   <item><c>TryUsePotion</c> — 회복제(상한 확장). 폐지 시점에 호출부가 이미 0이었다.</item>
    ///   <item><c>PayFocusCancelCoins</c> — 집중 중도 취소 지급.</item>
    ///   <item><c>DailyCapCoins()</c> · <c>RemainingDailyRoomCoins()</c> ·
    ///     <c>RemainingIdleWindowSeconds()</c> — 위 지급들의 「오늘 얼마 남았나」 조회.</item>
    /// </list>
    ///
    /// <para>★ <b>바뀐 것 하나</b>: <c>TryAwardArcheryCoins</c> → <see cref="TryClaimArcheryAward"/>.
    /// 지갑에 더하는 한 줄만 뺐고 <b>쿨다운·일일 총량 판정은 한 글자도 안 바꿨다</b> —
    /// 그 판정이 <c>CharacterProgressionDirector</c>에서 <b>활쏘기 XP의 게이트</b>로 재사용되고 있고
    /// (2026-09-07 보안 결함 수정), 함께 지우면 시간당 ~6,478XP 도배 구멍이 다시 열린다.
    /// 그래서 <c>ArcheryCoinsToday</c>는 이제 <b>동전이 아니라 관문 단위</b>의 누계다
    /// (이름과 세이브 필드 <c>archeryCoinsToday</c>는 스키마 불변을 위해 그대로 둔다).</para>
    ///
    /// <para>★★ <b>세이브 스키마는 한 칸도 움직이지 않았다</b>(<c>CharacterSaveStore.CurrentVersion</c> 불변).
    /// <see cref="CurrencySaveState"/>의 필드 15개가 <b>전부 그대로</b>이고
    /// <see cref="RestoreFromSave"/>/<see cref="CaptureSaveState"/>도 손대지 않았다. 폐기한 축
    /// (<c>coinBalance</c> · <c>seedGranted</c> · <c>purchasedItemIds</c> · <c>todayGrantedCoins</c> ·
    /// <c>potionsUsedToday</c> · <c>idleWindowUsedSeconds</c> · <c>todoCoinPaidToday</c> ·
    /// <c>itemGraceBaselines</c>)은 <b>읽고 → 담고 → 다시 쓴다</b>. 「그릇째 지우면 안 된다」가
    /// game-architect 판정이고, 그 이유는 같은 그릇에 <b>다른 축</b>이 들어 있다는 것이다 —
    /// <see cref="DayIndex"/>(오늘 날짜의 유일 출처) · <see cref="StatTierReached"/>(등급 영구 해금) ·
    /// <see cref="FocusXpToday"/> · <see cref="EquippedDanceIds"/>.</para>
    ///
    /// <para>★★ <b>남은 죽은 잔재 4개 — 프로덕션 호출부 0건(2026-09-29 실측)</b>:
    /// <see cref="CoinBalance"/> · <see cref="PurchasedItemIds"/>/<see cref="IsPurchasedItem"/>/
    /// <see cref="TryPurchaseItem"/> · <see cref="PayFocusCompletionCoins"/>. <b>새로 부르지 마라.</b>
    /// <para>이번 라운드에 <b>지우지 않은 이유</b>는 판단이지 게으름이 아니다:
    /// <see cref="TryPurchaseItem"/>/<see cref="IsPurchasedItem"/>는 R1 회귀 잠금
    /// (<c>Tests/EditMode/ItemOwnershipUnionTests</c>)이 <b>「산 이력이 있는데도 보유가 아니다」를
    /// 만드는 유일한 수단</b>이라, 지우면 그 잠금을 함께 지워야 한다 — 사용자가 닫은 문을 지키는
    /// 계기를 없애는 결정이므로 리더 판정 사안이다. <see cref="CoinBalance"/>는 폐기 필드의 왕복
    /// 저장고를 겸하므로 <b>getter만</b> 지울 수 있고, 그 한 줄을 위해 테스트 여섯 파일을 건드릴
    /// 값은 없다.</para></para>
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

        /// <summary>파일에 적혀 있던 동전. v9 파일에는 이 개념이 없었고, 그때의 0은
        /// <b>"한 푼도 없다"는 정확한 사실</b>이다(§20-1 #1).
        /// <para>★★ <b>2026-09-29 — 이 값을 늘리거나 줄이는 프로덕션 경로가 0개다.</b> 남은 것은
        /// 세이브 왕복(<see cref="RestoreFromSave"/> → <see cref="CaptureSaveState"/>)뿐이라,
        /// 사용자가 어제까지 번 잔액은 <b>파일에 그대로 보존</b>되고 화면에서 조용히 얼어 있다.
        /// 그리고 죽은 잔재 <see cref="TryPurchaseItem"/>만 이 값을 읽는다(2026-09-29 실측: 이 속성을
        /// 읽는 프로덕션 호출부 0건 — 상점 표면과 동전 칩을 병렬 라운드가 걷어냈다).</para></summary>
        public static int CoinBalance { get; private set; }

        /// <summary>첫 실행 시드를 받은 적이 있는가. <see cref="CoinBalance"/>만으로는
        /// <b>"다 썼다"와 "받은 적 없다"가 구분되지 않는다</b>(§20-1 #2).
        /// <para>★★ 2026-09-29 — 시드 지급이 폐지돼 이 플래그를 <b>세우는 코드가 없다</b>. 값은
        /// 왕복만 한다(스키마 불변). ★ 그래서 기존 사용자 전원의 값이 <c>false</c>로 남아 있고,
        /// 시드를 되살리면 <b>전원에게 한 번 더</b> 나간다 — <c>CurrencyRules</c>의 「첫 실행 시드」
        /// 절이 그 함정을 적어 뒀다.</para></summary>
        public static bool SeedGranted { get; private set; }

        private static readonly List<string> s_purchasedItemIds = new List<string>();

        /// <summary>★ <b>상점에서 산 것만</b> 적힌다. 42종의 레벨 파생 보유는 여기 없다.
        /// <para>★★★ <b>2026-09-28 DLC 폐지 R1 — 이 목록은 더 이상 보유 판정에 들어가지 않는다</b>
        /// (사용자 확정: DLC·재화 폐지, 1회 구매 전환). <c>ItemCatalogEntry.IsOwned</c>와
        /// <c>EquipmentModel.IsItemOwned</c>에서 구매 항을 <b>같은 커밋에 뗐다</b> — 옛 문장
        /// 「<c>ItemCatalogEntry.IsOwned</c>가 합집합으로 둘을 본다(§20-2-a)」는 그때부터 거짓이다.
        /// 남은 해제 경로는 <b>레벨</b>과 QA 해금 스위치뿐이다.</para>
        /// <para>★ <b>필드와 이력 자체는 남아 있다</b>(읽고 쓰고 저장한다) — 세이브 호환 판정이 끝난 뒤
        /// 다른 라운드가 정리한다. 그래서 「산 적이 있다」는 <b>사실</b>은 보존되고, 그 사실이
        /// <b>무엇도 열지 않는다</b>는 것만 바뀌었다.</para>
        /// <para>옛 합집합 규칙이 이 필드의 하위 호환을 성립시켰다 — v9 파일에 이 키가 없어도
        /// 레벨 파생 항이 그대로 살아 있어 <b>아무것도 잃지 않는다</b>. 그 항이 사라진 지금 결론은
        /// 더 강하게 참이고, <c>Tests/EditMode/ItemOwnershipUnionTests</c>가 계속 잠근다.</para>
        /// <para>이름이 <c>ownedItemIds</c>가 아닌 이유: <c>own</c>은 세이브 스키마 금지 토큰이다
        /// (<c>EntitlementNotInSaveAuditTests</c>). 필드가 아직 없을 때 개명해 비용이 0이었다.</para></summary>
        public static IReadOnlyList<string> PurchasedItemIds => s_purchasedItemIds;

        private static readonly int[] s_statTierReached = new int[CurrencyRules.StatTierSlotCount];

        // ====================================================================
        // B군 — 일일 래칫
        // ====================================================================
        //
        // ★★ 2026-09-29 — 이 군에서 <b>살아 있는 것은 DayIndex 하나</b>다. 나머지 셋은 폐기된
        //   재화 축의 그릇이고 왕복만 한다(스키마 불변). 값을 늘리는 코드도, 읽어서 판정하는 코드도
        //   없지만 <b>롤오버가 0으로 되돌리는 것은 그대로 둔다</b>(<see cref="ApplyDayRollover"/>) —
        //   「하루가 지나면 오늘 것은 0」이라는 불변식 I-15′를 셋만 예외로 빼면 그 불변식이
        //   부분적으로만 참인 문장이 되고, 그건 다음 사람이 신뢰할 수 없는 계기다.

        /// <summary>★ <b>래칫된 최대 일자</b>다(단순 "오늘"이 아니다 — T-4-b).
        /// 시계를 되감아도 이 값은 내려가지 않으므로 "오늘 연장" 공격이 성립하지 않는다.
        /// <para>★ <b>이 저장소에서 「오늘」의 유일한 출처다</b> — [오늘 할일] 날짜축
        /// (<c>Interaction/TodoBoardPopover.TodayIndex</c>)이 이 값을 그대로 읽는다. 재화가 폐지돼도
        /// <b>이 축은 그대로 살아 있어야 한다.</b></para></summary>
        public static int DayIndex { get; private set; }

        /// <summary>【폐기, 왕복 전용】 오늘 유휴로 지급된 동전이었다. 유휴 수급이 폐지돼
        /// <b>늘어나지 않는다</b>. 손상 값은 여전히 <see cref="CurrencyRules.ClampGrantedCoins"/>를 지난다.</summary>
        public static int TodayGrantedCoins { get; private set; }

        /// <summary>【폐기, 왕복 전용】 오늘 쓴 회복제 개수(0~2)였다. <c>TryUsePotion</c>이 사라져
        /// <b>늘어나지 않는다</b>(폐지 시점에 프로덕션 호출부가 이미 0이었다).</summary>
        public static int PotionsUsedToday { get; private set; }

        /// <summary>【폐기, 왕복 전용】 오늘 갉아 먹은 8시간 창(초)이었다. 창을 갉는 코드가 사라져
        /// <b>늘어나지 않는다</b>. NaN을 상한으로 보내는 클램프는 남는다(방향을 되돌리지 마라 —
        /// <see cref="CurrencyRules.ClampIdleWindowSeconds"/> 문서).</summary>
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

        /// <summary>【폐기, 왕복 전용】 [오늘 할일] 하루 1회 지급을 이미 받았는가였다. 벽시계 유닉스 초를
        /// 저장하던 <c>todoCoinPaidDateUnix</c>를 대체한 필드고(T-3-a 위반이었다), 2026-09-29에 지급
        /// 자체가 폐지돼 <b>true로 바뀌지 않는다</b>.
        /// <para>★ 혼동 주의 — [오늘 할일] <b>기능</b>은 그대로 살아 있다. 사라진 것은 그 완료에 붙던
        /// 동전이고, 목록·날짜축·완료 판정은 <c>Core/TodoListModel</c>에 한 줄도 안 바뀐 채 있다.</para></summary>
        public static bool TodoCoinPaidToday { get; private set; }

        /// <summary>오늘 활쏘기 정중앙 보상 판정을 통과한 누계. <c>lastArcheryCoinUnix</c>(벽시계)의
        /// 대체다(§20-3).
        /// <para>★★ <b>2026-09-29 — 단위가 「동전」에서 「관문 단위」로 바뀌었다</b>(숫자는 그대로 20씩).
        /// 이 값은 이제 지갑이 아니라 <b>활쏘기 XP의 일일 천장</b>을 결정한다
        /// (<see cref="TryClaimArcheryAward"/> · <see cref="ArcheryDailyLimitReached"/>).
        /// 이름과 세이브 필드 <c>archeryCoinsToday</c>는 <b>스키마 버전을 올리지 않기 위해</b> 그대로 둔다 —
        /// 같은 사실에 두 이름을 만들지 않으려고 고른 쪽이다.</para></summary>
        public static int ArcheryCoinsToday { get; private set; }

        /// <summary>
        /// ★ 오늘 집중 모드(완주+취소)로 받은 XP. 상한은 <see cref="CurrencyRules.FocusXpDailyCap"/>
        /// (design-systems §15-4, 활쏘기 채널 상한과 동일한 1,080).
        /// <para>★ 2026-09-29 — 옛 문장은 「동전 카운터와 완전히 독립」이었다. 그 독립은 이제 자명하다:
        /// <b>동전 카운터가 아무것도 안 세므로</b> 이 카운터가 집중 모드 보상의 유일한 축이다.</para>
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

        /// <summary>활쏘기 보상 쿨다운이 풀리는 단조 시각.</summary>
        private static double s_archeryCooldownUntilMonotonic = double.NegativeInfinity;

        // ====================================================================
        // 유휴 수급 — ★★★ 2026-09-29 <b>폐지</b>(DLC·재화 폐지, 사용자 확정)
        // ====================================================================
        //
        // 지운 것: <c>TickIdleIncome(double, bool, out double)</c> ·
        //   세션 전용 소수분 <c>s_idleCarryCoins</c>(그리고 그 세 리셋 지점) ·
        //   순수 함수 <c>CurrencyRules.IdleTick</c>/<c>IdleTickResult</c> ·
        //   배선 <c>Interaction/CharacterProgressionDirector.AccrueIdleIncome</c>.
        //
        // ★ <b>그 배선의 본체는 지급이 아니라 「기산점을 조건 없이 전진시키는 것」이었다</b>(I-7′).
        //   두 번째 적립 축을 새로 만드는 라운드는 그 문장을 먼저 읽어라 — 집중 세션 동안 기산점을
        //   멈춰 두면 세션이 끝난 첫 틱의 델타에 세션 전체 길이가 실려 같은 시간이 두 번 보상된다.
        //
        // ★★ <b>「관측 불가」도 함께 사라졌다</b>: 상한/창에 걸린 상태는 화면에서 고장과 똑같이 생겨서
        //   전용 로그가 필요했고, 그 로그가 정상 동작을 5초에 한 번 고장으로 신고한 사고
        //   (실측 720줄/시간)를 냈다. 지금은 지급 자체가 없으므로 그 관측 부채도 0이다.

        // ====================================================================
        // ★ 집중 모드 <b>동전</b> 지급 — 2026-09-29 현재 <b>죽은 잔재 1개</b>만 남았다
        // ====================================================================
        //
        // 지운 것: <c>PayFocusCancelCoins</c>(중도 취소 지급) ·
        //   배선 <c>Interaction/FocusWatchDirector.PayCompletionCoins</c>/<c>PayCancelCoins</c>(둘 다).
        //
        // ★ <see cref="PayFocusCompletionCoins"/>는 <b>프로덕션 호출부가 0</b>이다. 선언이 남은 이유는
        //   판단이 아니라 병렬이다 — 상점 표면을 지우는 라운드의 파일이 아직 이 이름을 참조한다.
        //
        // ★★ 옛 설계 지식 보존: 집중 지급은 <b>일일 상한 밖</b>이었다(§22-13) — 그래서 이 경로는
        //   <see cref="TodayGrantedCoins"/>를 건드리지 않고 <see cref="IdleWindowUsedSeconds"/>도
        //   갉지 않는다. 「관례에 맞춰」 <c>ClampGrantedCoins</c>를 끼우면 집중 수입이 1,500에서
        //   <b>조용히</b> 막히고 그 실패는 초록 테스트와 똑같이 생긴다. 지금은 호출부가 없어 무해하지만,
        //   같은 구조를 XP 쪽에 복사할 때 이 함정이 그대로 따라온다.

        /// <summary>집중 세션 <b>완주</b> 지급. ★★ <b>2026-09-29 — 죽은 잔재다(프로덕션 호출부 0).</b>
        /// 인자는 <b>명목 세션 길이(초)</b>다(계측 누적값이 아니다 —
        /// 이유는 <see cref="CurrencyRules.FocusCompletionCoins"/>). <b>새로 부르지 마라.</b></summary>
        /// <returns>실제로 지급된 동전(0이면 지급 없음).</returns>
        public static int PayFocusCompletionCoins(double sessionDurationSeconds)
            => GrantFocusCoins(CurrencyRules.FocusCompletionCoins(sessionDurationSeconds));

        /// <summary>집중 동전 경로의 <b>유일한</b> 잔액 반영 지점.
        /// 0동전이면 <see cref="IsDirty"/>를 세우지 않는다 — 디스크를 헛되이 두드리지 않는다
        /// (하루 종일 켜져 있는 앱이다).</summary>
        private static int GrantFocusCoins(int coins)
        {
            if (coins <= 0) return 0;
            CoinBalance = CurrencyRules.ClampCoinBalance(CoinBalance + coins);
            IsDirty = true;
            return coins;
        }

        // ====================================================================
        // ★★ 집중 모드 XP 지급 — 이제 집중 모드 보상의 <b>유일한</b> 축이다
        // ====================================================================
        //
        // ★ 원래는 「동전과 나란히, 그러나 독립된 상한」이었다. 동전 쪽이 폐지됐으므로 남은 계약은
        //   하나다: XP는 design-systems §15-4가 도입한 상한
        //   (<see cref="CurrencyRules.FocusXpDailyCap"/>)을 반드시 지나고, 그 카운터는
        //   <see cref="FocusXpToday"/> <b>하나</b>다(상한도 하나, 카운터도 하나 — 그림자 상태 금지).
        //
        // ★★ <b>XP를 상한 밖으로 열지 마라.</b> 코인은 레벨링 페이싱과 무관해서 상한 밖이 안전했지만
        //   (§22-13), XP는 레벨 캡·LevelBonus·소프트캡 전체가 조율하려는 대상이다 — 상한 밖으로
        //   열면 그 페이싱이 집중 모드 사용량에 따라 조용히 무너진다.
        //
        // 산식은 여기 없다 — <see cref="CurrencyRules.FocusCompletionXp"/> · <see cref="CurrencyRules.FocusCancelXp"/>
        // 한 곳에만 있고, 이 모델은 상한 클램프와 카운터만 담당한다.

        /// <summary>집중 세션 <b>완주</b> XP 지급. 인자는 <b>명목 세션 길이(초)</b>다(계측 누적값이 아니다 —
        /// 이유는 <see cref="CurrencyRules.FocusCompletionXp"/>). 오늘 이미 상한 근처면 <b>일부만</b>
        /// 지급될 수 있다(마지막 한 조각을 완전히 버리지 않는다 — <see cref="TryClaimArcheryAward"/>의
        /// "room보다 크면 room만큼만" 관례와 동일).</summary>
        /// <returns>실제로 지급된 XP(0이면 오늘 상한에 이미 도달).</returns>
        public static int TryGrantFocusCompletionXp(double sessionDurationSeconds)
            => GrantFocusXp(CurrencyRules.FocusCompletionXp(sessionDurationSeconds));

        /// <summary>집중 세션 <b>중도 취소</b> XP 지급. 인자는 <c>명목 세션 길이 − 잔여 초</c>다.
        /// 1분 미만이면 산식 자체가 0을 내므로 이 함수도 0을 돌려주고 아무 일도 하지 않는다.</summary>
        /// <returns>실제로 지급된 XP(0이면 1분 미만이거나 오늘 상한에 도달).</returns>
        public static int TryGrantFocusCancelXp(double elapsedSeconds)
            => GrantFocusXp(CurrencyRules.FocusCancelXp(elapsedSeconds));

        /// <summary>두 집중 XP 경로가 공유하는 <b>유일한</b> 카운터 반영 지점 — 폐지된 코인 쪽
        /// <see cref="GrantFocusCoins"/>와 모양은 대칭이었지만 <b>상한을 지난다는 점이 달랐다</b>
        /// (§22-13은 코인 한정 확정 사항, design-systems §15-4).</summary>
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

        // ====================================================================
        // 「오늘 얼마 남았나」 조회 · 회복제 · [오늘 할일] · 시드 —
        //   ★★★ 2026-09-29 <b>전부 폐지</b>(DLC·재화 폐지, 사용자 확정)
        // ====================================================================
        //
        // 지운 것: <c>DailyCapCoins()</c> · <c>RemainingDailyRoomCoins()</c> ·
        //   <c>RemainingIdleWindowSeconds()</c> · <c>TryUsePotion()</c> ·
        //   <c>TryPayTodoDailyCoins()</c> · <c>TryGrantSeedCoins()</c>.
        //
        // ★ <c>TryUsePotion</c>은 폐지 시점에 <b>프로덕션 호출부가 이미 0</b>이었다(상점에 회복제
        //   항목이 붙은 적이 없다). 그래서 이 삭제로 사라진 사용자 경로는 없다.
        //
        // ★ <b>남은 조회는 XP 쪽 둘뿐이다</b>: <see cref="RemainingFocusXpRoomToday"/> ·
        //   <see cref="FocusXpDailyLimitReached"/>. 「오늘 얼마 남았나」를 화면이 다시 물으려 들면
        //   그 둘을 쓰고, 동전 쪽 함수를 되살리지 마라.

        /// <summary>
        /// 활쏘기 정중앙 보상 <b>판정</b>. 두 관문을 지난다 —
        /// ① <b>세션 쿨다운</b>(단조 시계, 저장 안 함) ② <b>일일 총량</b>(저장).
        /// <para>①만 있으면 앱 재시작으로 무한 파밍이 된다. 그 사실을 숨기지 않고 ②로 상한을 건다
        /// (§20-3-b). ②의 값은 <b>U-41 미확정</b>이라
        /// <see cref="CurrencyRules.ArcheryDailyAwardLimit"/>에 임시값으로 열려 있다.</para>
        ///
        /// <para>★★★ <b>2026-09-29 — 이 함수의 이름과 의미가 바뀌었다</b>(옛 이름
        /// <c>TryAwardArcheryCoins</c>). <b>지갑에 더하는 한 줄만 뺐고 관문 두 개는 한 글자도 안 바꿨다.</b>
        /// 이유: <c>Interaction/CharacterProgressionDirector.OnArcheryShotChanged</c>가 이 반환값을
        /// <b>활쏘기 XP의 게이트</b>로 재사용하고 있고, 그 재사용이 2026-09-07 보안 결함 수정의 본체다
        /// (옛 코드는 동전이 쿨다운·일일 상한에 막혀도 XP는 무조건 나갔고, 연속 도배 시 시간당
        /// ~6,478XP = 패시브의 72배로 Lv50 전량을 22.4시간에 채울 수 있었다).
        /// <b>이 함수를 「동전이 없어졌으니」 지우면 그 구멍이 그대로 다시 열린다.</b></para>
        ///
        /// <para>★ 그래서 반환값의 단위는 이제 동전이 아니라 <b>관문 단위</b>다(값은 그대로 20씩).
        /// 호출부가 필요한 것은 <c>&gt; 0</c> 여부뿐이라 <c>bool</c>로 바꿀 수도 있었지만, 그러면
        /// 세이브 필드 <c>archeryCoinsToday</c>에 쌓이는 값과 반환값의 단위가 갈라진다 —
        /// 같은 사실을 두 단위로 말하지 않는 쪽을 골랐다.</para>
        /// </summary>
        /// <param name="nowMonotonic"><c>Time.realtimeSinceStartupAsDouble</c>. 벽시계가 아니다.</param>
        /// <returns>이번 명중이 통과시킨 관문 단위(0이면 쿨다운 중이거나 오늘 상한에 도달 —
        /// 그때는 호출부가 XP도 지급하지 않는다).</returns>
        public static int TryClaimArcheryAward(double nowMonotonic)
        {
            if (nowMonotonic < s_archeryCooldownUntilMonotonic) return 0;

            int room = CurrencyRules.ArcheryDailyCoinLimit - ArcheryCoinsToday;
            if (room <= 0) return 0;

            int pay = CurrencyRules.ArcheryCoinsPerAward;
            if (pay > room) pay = room;

            s_archeryCooldownUntilMonotonic = nowMonotonic + CurrencyRules.ArcheryAwardCooldownSeconds;
            ArcheryCoinsToday = CurrencyRules.ClampArcheryCoinsToday(ArcheryCoinsToday + pay);
            IsDirty = true;
            return pay;
        }

        /// <summary>활쏘기 보상이 <b>오늘 상한에 걸려</b> 멈췄는가. 연출은 계속 도는데 보상만 안 나오면
        /// 사용자는 그것을 "고장"으로 읽는다 — 문구가 필요한 자리를 화면 쪽에 알려 주는 값이다(§20-8).
        /// <para>★ 2026-09-29 이후 여기서 멈추는 것은 동전이 아니라 <b>XP</b>다.</para></summary>
        public static bool ArcheryDailyLimitReached => ArcheryCoinsToday >= CurrencyRules.ArcheryDailyCoinLimit;

        /// <summary>상점 구매 — 잔액을 깎고 구매 이력에 남긴다. 부족하면 아무 일도 하지 않는다.
        /// <para>★★ <b>2026-09-29 — 죽은 잔재다(프로덕션 호출부 0건, 실측).</b> 유일한 호출부였던
        /// 상점 화면은 같은 날 병렬 라운드가 지웠다.
        /// <b>새로 부르지 마라</b> — R1에서 보유 판정이 이미 구매 항을 뗐으므로, 이 함수가 성공해도
        /// <b>아무 아이템도 열리지 않고 잔액만 줄어든다.</b></para>
        /// <para>★ <b>선언을 남긴 이유</b>: <c>Tests/EditMode/ItemOwnershipUnionTests</c>가 R1의 회귀
        /// («구매 이력이 보유를 다시 열지 않는다»)를 잠그는데, 그 시나리오를 만드는 수단이 이 함수뿐이다.
        /// 이 함수를 지우려면 그 잠금도 같이 지워야 하므로 <b>리더 판정 사안</b>으로 남겼다.</para>
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

        /// <summary>이 아이템을 <b>상점에서</b> 산 적이 있는가. 레벨 파생 보유는 여기 안 본다.
        /// <para>★★★ <b>2026-09-28 DLC 폐지 R1 — 이 술어를 보유 판정에서 뗐다.</b> 옛 문장
        /// 「합쳐서 보는 곳은 <c>ItemCatalogEntry.IsOwned</c> 하나다」는 그때부터 거짓이다 —
        /// 지금 <b>이 파일 밖에서 이 술어를 부르는 프로덕션 호출부는 0개</b>다(2026-09-28 실측).
        /// 남은 호출자는 같은 파일 안의 둘 — 중복 구매 차단(<see cref="TryPurchaseItem"/>)과
        /// 불러오기 때의 중복 이력 접기 — 그리고 테스트 4개다. 상점 화면은 이 술어를 부르지 않고
        /// <c>ItemCatalogEntry.IsOwned</c>를 본다(그래서 R1 이후 상점의 「보유 중」 칩은
        /// <b>레벨만</b> 반영한다 — 상점 자체를 제거하는 라운드 소관).</para>
        /// <para>★ <b>다시 물리지 마라</b> — 되살리면 사용자가 닫은 문(재화로 장비를 여는 경로)을
        /// 다시 여는 것이 된다. 보유 판정의 정본 두 자리는 <c>ItemCatalogEntry.IsOwned</c>와
        /// <see cref="EquipmentModel.IsItemOwned"/>이고 둘의 항 구성은 항상 같아야 한다.</para></summary>
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
        // ★ 날짜 롤오버 — 「오늘의 것」을 되돌리는 것이 <b>하나의 사건</b>이다
        //    (T-D-10 · T-D-14 · I-15′)
        // ====================================================================
        // 여러 카운터를 다른 조건으로 나누면 (카) 공격이 각 경계를 따로 넘어 보상이 배가 되고,
        // T-14-3-a 방어를 <b>여러 곳에</b> 걸어야 한다. 한 곳만 빠뜨려도 조용히 새는 문이 된다.
        // 그래서 이 저장소에서 그것들을 되돌리는 코드는 <see cref="ApplyDayRollover"/> 하나뿐이다.
        //
        // ★★ 2026-09-29 — 재화가 폐지된 뒤에도 <b>이 절은 그대로 살아 있다</b>. 지금 여기에 실린
        //   살아 있는 축은 둘이다: <see cref="DayIndex"/>(오늘 날짜의 유일 출처 — [오늘 할일]
        //   날짜축이 읽는다)와 활쏘기/집중 XP 카운터. 폐기된 동전 카운터도 <b>같은 사건에 계속
        //   실어 둔다</b> — 예외를 만들면 I-15′가 「부분적으로만 참인 문장」이 된다.

        /// <summary>
        /// ★ <b>이 클래스에서 시각을 읽는 유일한 지점.</b> 오늘의 일자 번호를 구하고,
        /// 아직 오프셋을 고정한 적이 없으면 <b>지금 이 순간의 로컬 오프셋으로 고정</b>한다(T-D-4).
        /// <para>여기서 나온 정수 하나만 판정에 쓴다.</para>
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
            DayIndex = today;                 // 래칫 — 전진만 한다. ★ 살아 있는 축
            TodayGrantedCoins = 0;            // 【폐기】 옛 ① 일일 상한 리셋
            PotionsUsedToday = 0;             // 【폐기】 옛 ② 무료 회복제 부활
            IdleWindowUsedSeconds = 0.0;      // 【폐기】 옛 ③ 8시간 창 리셋 (T-15-1-d)
            TodoCoinPaidToday = false;        // 【폐기】 옛 [오늘 할일] 정액
            ArcheryCoinsToday = 0;            // ★ 살아 있는 축 — 활쏘기 XP 일일 천장
            FocusXpToday = 0;                 // ★ 살아 있는 축 — 집중 모드 XP 일일 상한

            s_lastRefillMonotonic = nowMonotonic;
            IsDirty = true;
        }

        // ====================================================================
        // 영속화 — ★★ 2026-09-29 <b>스키마는 한 칸도 움직이지 않았다</b>
        // ====================================================================
        //
        // <c>CharacterSaveStore.CurrentVersion</c>을 올리지 않았고, 아래 두 함수와
        // <see cref="CurrencySaveState"/>의 필드 15개를 <b>한 줄도 고치지 않았다</b>.
        // 폐기한 축은 여기서 여전히 읽히고 담기고 다시 쓰인다 — 그것이 「그릇째 지우지 않는다」의
        // 실제 모습이고, 같은 그릇에 <c>dayIndex</c>·<c>statTierReached</c>·<c>focusXpToday</c>·
        // <c>equippedDanceIds</c>가 함께 들어 있기 때문이다.

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

            IsDirty = false;
        }

        /// <summary>저장 파일에 적을 한 덩어리. ★ 2026-09-29 — <b>한 줄도 바뀌지 않았다</b>
        /// (필드 15개 전부 그대로 나간다. 폐기한 축도 읽은 값을 그대로 돌려준다).</summary>
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

            IsDirty = false;
        }

        /// <summary>테스트 전용 — 롤오버 이후의 상태를 만들지 않고 <b>일자만</b> 밀어 놓는다.
        /// (실제 롤오버는 <see cref="TickDayRollover"/> 하나만 일으킨다 — 그 단일성이 I-15′다.)</summary>
        internal static void SetDayIndexForTesting(int dayIndex) => DayIndex = dayIndex;
    }
}
