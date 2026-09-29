using System;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 하루 경계 · 등급 래칫 · 집중 모드 XP의 <b>순수 규칙</b> — 상태를 하나도 들고 있지 않다.
    ///
    /// ============================================================================
    /// ★★★ 2026-09-29 DLC·재화 폐지 R5 — <b>동전 경제는 여기서 끝났다</b>
    /// ============================================================================
    /// 사용자 확정(DLC·재화 폐지, 1회 구매 전환)으로 <b>동전을 지급하거나 차감하는 산식을 전부
    /// 걷어냈다</b>. 지운 것: 유휴 수급 1틱(<c>IdleTick</c>·<c>IdleTickResult</c>) · 유휴 요율
    /// (<c>IdleCoinsPerMinute</c>) · [오늘 할일] 정액(<c>TodoDailyCoins</c>) · 첫 실행 시드
    /// (<c>SeedCoins</c>·<c>CanGrantSeed</c>) · 집중 취소 지급(<c>FocusCancelCoins</c>·
    /// <c>FocusCancelCoinsPerMinute</c>) · 창/천장 불변식(<c>WindowToCeilingRatio</c>).
    ///
    /// <para>★ <b>남긴 것과 그 이유</b> — 세 갈래뿐이고, 어느 것도 지갑을 늘리지 않는다:
    /// <list type="number">
    ///   <item><b>저장값 위생</b>(<see cref="ClampPotionsUsed"/> · <see cref="DailyCapCoins"/> ·
    ///     <see cref="ClampGrantedCoins"/> · <see cref="ClampIdleWindowSeconds"/> ·
    ///     <see cref="ClampCoinBalance"/> · <see cref="ClampArcheryCoinsToday"/>). 폐기한 세이브
    ///     필드들은 <b>스키마에 그대로 남아 왕복만 한다</b>(스키마 버전 불변) — 그 왕복이 손상된
    ///     파일에서도 같은 답을 내야 하므로 클램프는 남는다.
    ///     <c>Tests/EditMode/DailyLimitClampAuditTests</c>가 이 셋의 실재를 매 실행 확인한다.</item>
    ///   <item><b>활쏘기 정중앙 관문</b>(<see cref="ArcheryAwardCooldownSeconds"/> ·
    ///     <see cref="ArcheryDailyCoinLimit"/>). ★ 동전이 아니라 <b>XP 보안</b>이 이 관문에 걸려 있다 —
    ///     <c>CharacterProgressionDirector</c>가 이 판정 결과를 XP 게이트로 재사용하고, 그 재사용이
    ///     2026-09-07 보안 결함(시간당 ~6,478XP 도배)의 수정 그 자체다. <b>지우면 그 구멍이 다시
    ///     열린다.</b></item>
    ///   <item><b>하루 경계 · 등급 · 집중 XP</b> — 원래부터 동전과 무관한 축이다.</item>
    /// </list></para>
    ///
    /// <para>★ <b>죽은 잔재 표시</b>: <see cref="FocusCoinsPerMinute"/> ·
    /// <see cref="FocusCompletionCoins"/> · 상점 가격 4종 + <see cref="PriceCoins"/>는
    /// <b>호출부가 사라지는 중인 상점 화면 라운드와 병렬</b>이라 이번 라운드에 남겼다. 상점 표면이
    /// 착지하면 프로덕션 호출부 0이 되고, 그때 다음 라운드가 지운다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 존재하는 이유 — 「같은 사실이 두 곳에서 계산되지 않게」
    /// ============================================================================
    /// 상한·클램프·하루 경계는 <b>두 곳</b>에서 필요해진다: 세이브 로드(정규화)와 판정.
    /// 그 둘이 각자 계산하면 그 순간 어긋나고, 어긋난 뒤에는
    /// "누가 옳은가"를 판정할 기준이 사라진다. 그래서 <b>숫자와 산식은 전부 여기 한 곳</b>에 있고
    /// <see cref="CurrencyModel"/>은 그 결과를 담기만 한다.
    ///
    /// <para>정본 문서: <c>docs/DESIGN_SYSTEMS_STATS.md</c> §18-2(확정 상수표) · §18-13(T-D-8~12) ·
    /// §20(v10 필드 확정), <c>docs/security/SECURITY_MODEL.md</c> T-14(clamp·리필 간격) ·
    /// T-15(8시간 창 래칫).</para>
    ///
    /// ============================================================================
    /// ★ 벽시계를 읽지 않는다 (security T-3-a)
    /// ============================================================================
    /// 이 클래스의 함수는 전부 <b>인자로 받은 값</b>만 본다. <c>DateTime.Now</c>/<c>UtcNow</c>도
    /// <c>Time.realtimeSinceStartupAsDouble</c>도 여기서 읽지 않는다 — 단조 시계는 호출부가 읽어
    /// 델타로 넘기고, 날짜 경계 계산(<see cref="LocalDayIndex"/>)은 <b>시각을 인자로 받는다</b>.
    /// 덕분에 이 파일 전체가 EditMode에서 실시간 대기 없이 검증된다(T-15-7-b).
    ///
    /// ============================================================================
    /// ★ 상한은 필드가 아니라 <b>함수</b>다 (T-D-9)
    /// ============================================================================
    /// <see cref="DailyCapCoins"/>는 저장되지 않는다. "오늘의 상한"을 파일에 적으면 그 숫자가
    /// 위조 대상이 되고, 재계산된 숫자는 위조 대상이 아니다. 세이브 스키마에
    /// <c>dailyCap</c>/<c>todayLimit</c>류 필드를 만들지 마라 —
    /// <c>Tests/EditMode/DailyLimitClampAuditTests</c>가 그 부재를 매 실행 확인한다.
    /// </summary>
    public static class CurrencyRules
    {
        // ====================================================================
        // ★ 죽은 잔재 — 상점 표면 제거 라운드와 병렬이라 이번에 남겼다
        // ====================================================================

        /// <summary>집중 모드 완주 시의 분당 동전. 사용자 최초 지시값.
        /// <para>★★ <b>2026-09-29 — 이 값으로 지갑이 늘어나는 프로덕션 경로는 0개다.</b>
        /// <c>Interaction/FocusWatchDirector</c>의 완주 지급 호출을 같은 라운드에 뗐다.
        /// 선언이 남아 있는 이유는 <see cref="FocusCompletionCoins"/>와 같다 — 상점 화면을 지우는
        /// <b>병렬 라운드</b>와 같은 시각에 돌아 같은 커밋에서 지울 수 없었다. 그 라운드는 같은 날
        /// 착지했고, <b>2026-09-29 실측으로 이 이름의 프로덕션 호출부는 0건</b>이다(남은 독자는
        /// <see cref="FocusCompletionCoins"/> 하나이고 그것도 호출부가 0이다).
        /// <b>새로 부르지 마라 — 다음 라운드가 둘을 함께 지운다.</b></para></summary>
        public const int FocusCoinsPerMinute = 24;

        // ====================================================================
        // 일일 상한 — ★ 지금은 <b>저장값 위생 전용</b>이다 (T-14-5-b · T-D-9)
        // ====================================================================
        //
        // ★★ 2026-09-29 — 이 네 상수는 더 이상 «오늘 얼마까지 벌 수 있는가»를 정하지 않는다.
        //   유휴 수급이 폐지됐으므로 이 값을 읽는 지급 코드가 존재하지 않는다. 남은 용도는 하나뿐이다:
        //   폐기된 세이브 필드 <c>todayGrantedCoins</c>·<c>potionsUsedToday</c>를 <b>왕복시킬 때
        //   손상 값을 다듬는 것</b>(<see cref="ClampGrantedCoins"/>). 스키마 버전을 올리지 않기
        //   위해 필드를 남겼고, 남긴 필드는 위생을 지나야 한다 — 그 하나의 이유로 여기 있다.

        /// <summary>회복제를 하나도 안 썼을 때의 하루 유휴 상한. 사용자 확정 1.</summary>
        public const int BaseDailyCapCoins = 1500;

        /// <summary>회복제 1개가 올려 주는 상한. 사용자 확정 1.</summary>
        public const int PotionBonusCoins = 500;

        /// <summary>하루에 쓸 수 있는 회복제 개수. 사용자 확정 1("하루에 회복제사용은 2개까지만").
        /// ★ 무료 1개는 이 2개에 <b>포함된다</b> — 유료 채널이 하루에 팔 수 있는 것은 최대 1개다(§18-2).</summary>
        public const int MaxPotionsPerDay = 2;

        /// <summary>어떤 경우에도 넘을 수 없는 하루 총액. ★ 숫자를 베끼지 않고 유도한다
        /// (1500 + 500×2 = 2500). 셋 중 하나가 바뀌면 이 값이 <b>저절로</b> 따라간다.</summary>
        public const int HardCeilingCoins = BaseDailyCapCoins + PotionBonusCoins * MaxPotionsPerDay;

        // ====================================================================
        // 8시간 창 — T-15. ★ 여기도 지금은 <b>저장값 위생 전용</b>이다(위 문단과 같은 이유)
        // ====================================================================

        /// <summary>하루에 <b>동전이 실제로 지급될 수 있는</b> 분. 사용자 최초 지시(480분).
        /// ★ "앱이 켜져 있던 분"이 아니다 — T-15-1-a.
        /// <para>★★ 2026-09-29 — 창을 갉는 코드가 사라졌다. 남은 용도는
        /// <see cref="ClampIdleWindowSeconds"/> 하나이고, 그건 폐기 필드
        /// <c>idleWindowUsedSeconds</c>의 왕복 위생이다.</para></summary>
        public const int IdleWindowCapMinutes = 480;

        /// <summary>창 상한(초). 저장 필드 <c>idleWindowUsedSeconds</c>의 상한이기도 하다.</summary>
        public const double IdleWindowCapSeconds = IdleWindowCapMinutes * 60.0;

        // ====================================================================
        // 리필 최소 간격 — T-14-3-a
        // ====================================================================

        /// <summary>일자 리필(상한 리셋 + 무료 회복제 부활 + 창 리셋) 사이에 요구하는
        /// <b>단조 시계</b> 최소 간격. security 권고 20시간.
        /// <para>★ 이 값과 짝이 되는 "직전 리필 시각"은 <b>세이브에 남기지 않는다</b>(T-14-3-a) —
        /// 프로세스 메모리에만 둔다. 그래서 앱을 새로 켜면 첫 리필은 <b>항상 통과</b>하고,
        /// 정상 사용자를 잠글 수 있는 경로가 문법적으로 존재하지 않는다.</para></summary>
        public const double MinRefillGapSeconds = 20.0 * 60.0 * 60.0;

        // ====================================================================
        // 채널별 카운터 — §20-1 D군
        // ====================================================================

        // ★★ 2026-09-29 — [오늘 할일] 정액(<c>TodoDailyCoins</c> = 300)은 <b>삭제됐다</b>.
        //    지급 경로(<c>Core/TodoListModel.PayTodoDailyCoins</c>)를 같은 라운드에 뗐다.
        //    세이브 필드 <c>todoCoinPaidToday</c>는 스키마에 남아 왕복만 한다(버전 불변).

        /// <summary>활쏘기 정중앙 1회의 <b>관문 단위</b>. ★ 원래 「1회 상금 20동전」이었고,
        /// 2026-09-29 이후 <b>지갑에 들어가지 않는다</b> — 아래 일일 총량과 짝이 되어
        /// «오늘 몇 번까지 보상 판정을 통과시킬 것인가»만 정한다.
        /// <para>★ <b>이름에 <c>Coins</c>가 남아 있는 것은 의도다</b>: 카운터가 실리는 세이브 필드가
        /// <c>archeryCoinsToday</c>이고 그 이름은 <b>스키마 버전을 올리지 않기 위해 바꿀 수 없다</b>.
        /// 상수 이름을 필드 이름과 갈라 놓으면 「같은 사실이 두 이름으로」 살게 된다 —
        /// 그 대가로 여기 한 줄을 읽게 하는 쪽을 골랐다.</para></summary>
        public const int ArcheryCoinsPerAward = 20;

        /// <summary>활쏘기 보상 판정의 쿨다운(초). ★ <b>세이브에 남기지 않는다</b> — 벽시계 유닉스 초를
        /// 저장하던 <c>lastArcheryCoinUnix</c>는 T-3-a 위반이라 폐기됐다(§20-3). 쿨다운은
        /// 단조 시계로 세션 안에서만 산다.
        /// <para>★★ <b>이 쿨다운은 이제 XP 방어선이다.</b> <c>CharacterProgressionDirector</c>가
        /// <c>CurrencyModel.TryClaimArcheryAward</c>의 판정 결과를 XP 게이트로 재사용한다
        /// (2026-09-07 보안 결함 수정 — 그 게이트가 없으면 연속 도배로 시간당 ~6,478XP).
        /// 동전이 사라졌다고 이 값을 지우면 <b>그 구멍이 그대로 다시 열린다.</b></para></summary>
        public const double ArcheryAwardCooldownSeconds = 600.0;

        /// <summary>★ <b>임시값, U-41 확정 대기.</b> 활쏘기 하루 상금 횟수 상한.
        /// §20-3-b가 원형 D(6,000동전/일)에서 <b>1,200동전 = 60회</b>를 유도하고 1.20배 여유를 얹어
        /// 72회를 권고했지만, <b>실사용 텔레메트리가 없다</b>. 필드(<c>archeryCoinsToday</c>)는 v10에
        /// 넣었고 이 상수만 열어 둔다 — 상수는 코드 한 줄이지만 필드는 나중에 넣으면 v11이다.
        /// <para>정상 사용자(원형 B)의 활쏘기는 하루 4회 = 이 상한의 5.6%라, 이 값이 조금 틀려도
        /// 정상 경로에는 닿지 않는다.</para></summary>
        public const int ArcheryDailyAwardLimit = 72;

        /// <summary>활쏘기 하루 상금 총액 상한(= 횟수 × 1회 상금). 숫자를 따로 적지 않는다.</summary>
        public const int ArcheryDailyCoinLimit = ArcheryDailyAwardLimit * ArcheryCoinsPerAward;

        // ====================================================================
        // 스탯 등급 high-water mark — §20-1 A군 #4
        // ====================================================================

        /// <summary>등급 high-water mark를 기록하는 슬롯 수(H-8 눈금이 걸리는 4스탯).</summary>
        public const int StatTierSlotCount = 4;

        /// <summary>등급 최대치. 눈금 3개(25/50/80%)라 등급은 0~3이다.
        /// ★ "평생 최대 12회 갱신"(§20-1)이 <c>StatTierSlotCount × MaxStatTier</c>와 같은지는
        /// 테스트가 확인한다 — 두 숫자가 어긋나면 둘 중 하나가 낡은 것이다.</summary>
        public const int MaxStatTier = 3;

        // ====================================================================
        // 상점 가격 — ★★ 2026-09-29 <b>죽은 잔재</b>다. 새로 부르지 마라.
        // ====================================================================
        //
        // ★★★ DLC·재화 폐지(사용자 확정)로 상점 자체가 없어졌다. 이 네 상수와 PriceCoins가
        //   아직 선언으로 남아 있는 이유는 판단이 아니라 <b>병렬</b>이었다: 상점 화면
        //   (<c>Interaction/CharacterInfoWindow.Shop</c>)을 지우는 라운드가 같은 시각에 돌고 있어
        //   이 라운드가 지우면 어느 한쪽이 컴파일되지 않는 순간이 생겼다.
        //   ⇒ 그 라운드는 <b>같은 날 착지했고</b>, 2026-09-29 실측으로 이 다섯의
        //   <b>프로덕션 호출부는 0건</b>이다. <b>다음 라운드가 이 절 전체를 지운다</b>
        //   (함께 지울 것: <c>Tests/EditMode/CurrencyRulesTests</c>의 §8 가격 테스트 3개).
        //
        // 아래 옛 근거 문단은 그대로 남긴다 — 「왜 여기였나」가 사라지면 다음 사람이 같은 자리를
        // ItemCatalog로 다시 옮기려 든다.
        //
        // ★ <b>왜 여기(재화 규칙)이고 ItemCatalog가 아닌가</b> — 리더 판정 2026-09-05.
        //   <c>ItemCatalog</c>의 <c>SubStat</c>·<c>Theme</c>은 아이템마다 <b>선언</b>되는 원시 데이터인데
        //   가격은 <b>등급에서 파생</b>되는 재화 도메인 값이다. 그리고 그 값을 실제로 쓰는
        //   <c>CurrencyModel.TryPurchaseItem</c>이 이미 재화 도메인에 있다 —
        //   모델(<c>CurrencyModel</c>) + 규칙(여기)으로 짝을 맞추는 이 파일의 기존 패턴 그대로다.
        //
        // ★★ <b>의존 방향은 한 쪽뿐이다</b>: 카탈로그가 가격이 필요하면 <b>여기를 참조</b>한다.
        //   <b>반대는 금지</b> — 재화 규칙은 <c>ItemCatalog</c>를 참조하지 않는다. 이 파일이 카탈로그를
        //   알게 되는 순간 "순수 규칙"이 아니게 되고(위 클래스 문서), 테스트가 <c>Resources.LoadAll</c>
        //   없이는 못 돌게 된다. 여기서 쓰는 <see cref="ItemRarity"/>는 <b>그 자체로 독립된 열거형</b>이다.
        //
        // ★ 2026-09-29 — <b>대상 소멸(DLC 폐지)</b>. 2026-09-06에 여기 적혀 있던 배선
        //   (상점 화면 → 구매 API → 이 가격 상수를 등급에서 파생시키는 테스트가 항등을 잠금)은
        //   상점 화면·구매 경로 전체가 폐지되며 사라졌다. 구매 API는 R1 회귀 잠금
        //   (<see cref="ItemOwnershipUnionTests"/>) 때문에 죽은 잔재로만 남아 있다
        //   (<c>CurrencyModel.cs</c> 클래스 문서 참조) — 이 등급별 가격 상수를 실제로 읽는
        //   프로덕션 호출부는 지금 0개다.

        /// <summary>일반 등급 아이템 가격.</summary>
        public const int CommonPriceCoins = 600;

        /// <summary>희귀 등급 아이템 가격.</summary>
        public const int RarePriceCoins = 1400;

        /// <summary>영웅 등급 아이템 가격.</summary>
        public const int EpicPriceCoins = 3200;

        /// <summary>
        /// 전설 등급 아이템 가격. ★ <b>U-17 확정값</b>(리더 승인 2026-09-05, §21-5).
        /// <para>고른 근거 셋: ① 완주 판정 기준(≤ 97.5일) 대비 <b>여유 7.5일</b> — 상한 10,971은 여유 0이라
        /// 하류 수치가 1%만 흔들려도 깨진다. ② 하류 계산 4개(가격 스윕 · 코인 완주 · 성장 곡선 ·
        /// 외형 가격)가 전부 이 값 위에 서 있다. ③ <b>집중 25분 세션 정확히 16.0회</b> —
        /// 10,971은 18.285회라 화면에서 설명할 수 없는 숫자다.</para>
        /// </summary>
        public const int LegendaryPriceCoins = 9600;

        /// <summary>
        /// 등급 → 가격. <b>가격의 유일한 출처</b>다 — 화면도 상점도 여기만 부른다
        /// (<c>ItemCatalog.RarityName</c>이 낱말에 대해 하는 일과 같은 역할).
        /// <para>모르는 값은 <see cref="CommonPriceCoins"/>다. 여기서 예외를 던지거나 0을 돌려주면
        /// <b>공짜로 사지는 아이템</b>이 생긴다 — 가장 싼 단으로 떨어지는 쪽이 안전한 방향이다.</para>
        /// </summary>
        public static int PriceCoins(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return CommonPriceCoins;
                case ItemRarity.Rare: return RarePriceCoins;
                case ItemRarity.Epic: return EpicPriceCoins;
                case ItemRarity.Legendary: return LegendaryPriceCoins;
                default: return CommonPriceCoins;
            }
        }

        // ====================================================================
        // 첫 실행 시드 — ★★★ 2026-09-29 <b>폐지</b>(DLC·재화 폐지, 사용자 확정)
        // ====================================================================
        //
        // 지운 것: <c>SeedCoins</c>(1,200) · <c>CanGrantSeed(bool)</c> ·
        //   <c>CurrencyModel.TryGrantSeedCoins()</c> ·
        //   <c>Interaction/CharacterProgressionDirector.TryGrantSeedCoinsOnce()</c>와 그 Start 배선.
        //
        // ★ <b>세이브 필드 <c>seedGranted</c>는 지우지 않았다</b> — 스키마에 그대로 남아 왕복만 한다
        //   (스키마 버전 불변). 그래서 이미 시드를 받은 사용자의 파일도, 못 받은 사용자의 파일도
        //   읽기/쓰기가 어제와 한 비트도 다르지 않다.
        //
        // ★★ <b>되살리지 마라.</b> 옛 설계의 핵심은 「금액이 0인 동안에는 플래그를 세우지 않는다」였고
        //   그 덕에 기존 사용자 전원이 아직 <c>seedGranted == false</c>다. 즉 <b>이 절을 다시 켜는
        //   순간 전원에게 동전이 한 번 더 나간다</b> — 사용자가 닫은 문(재화)을 되돌리는 것이 된다.

        // ====================================================================
        // ★ 클램프 — "복구"가 아니라 "정규화"다 (T-14-5-b)
        // ====================================================================
        // 저장된 값을 그대로 믿지 않는다. 읽는 즉시 다듬고, 실패도 경고도 남기지 않는다.
        // 이건 방어이기 전에 <b>위생</b>이다 — 손상된 세이브·구버전 파일·`.writing` 잔해에도
        // 같은 코드가 같은 답을 낸다. 값을 거부하지 않으므로 "우리 버그를 치터보다 먼저 만나는"
        // 종류의 검사가 아니다(§4-1-4).

        // ★★ 2026-09-29 — 아래 다섯 클램프(<c>ClampPotionsUsed</c>·<c>ClampGrantedCoins</c>·
        //   <c>ClampIdleWindowSeconds</c>·<c>ClampArcheryCoinsToday</c>·<c>ClampCoinBalance</c>)의
        //   <b>역할이 바뀌었다</b>. 원래는 「지급 경로가 상한을 넘지 못하게」였고, 지금은 그 지급 경로가
        //   없으므로 «폐기된 세이브 필드를 무손실로 왕복시킬 때의 위생»만 남았다.
        //   <b>지우지 마라</b>: 필드를 스키마에 남긴 채(버전 불변) 클램프를 떼면, 손상된 파일이나
        //   손으로 편집한 파일에서 읽은 값이 그대로 다시 디스크로 나가고 —
        //   <c>Tests/EditMode/DailyLimitClampAuditTests</c>가 그 순간 빨개진다(그 표가 세 값의 실재를
        //   존재 단언으로 못박고 있다).

        /// <summary>오늘 쓴 회복제 개수. ★ I-12′의 유일한 방어선 — 9999를 써 넣어도 2에서 멈춘다.</summary>
        public static int ClampPotionsUsed(int potionsUsedToday)
            => potionsUsedToday < 0 ? 0
               : potionsUsedToday > MaxPotionsPerDay ? MaxPotionsPerDay
               : potionsUsedToday;

        /// <summary>오늘의 상한. ★ <b>필드가 아니라 함수다</b>(T-D-9). 인자는 안에서 다시 클램프하므로
        /// 호출부가 클램프를 잊어도 상한이 새지 않는다.</summary>
        public static int DailyCapCoins(int potionsUsedToday)
            => BaseDailyCapCoins + PotionBonusCoins * ClampPotionsUsed(potionsUsedToday);

        /// <summary>오늘 지급된 동전. 상한은 <see cref="DailyCapCoins"/>다 —
        /// <see cref="HardCeilingCoins"/>보다 <b>같거나 더 좁고</b>, 정상 경로에서 둘의 차이가
        /// 관측되는 경우가 없다(회복제를 n개 쓴 상태에서 지급될 수 있는 최대가 곧 DailyCap이다).
        /// security T-14-5-b가 적은 <c>Clamp(0, HardCeilingCoins)</c>를 <b>더 좁게</b> 만족시킨다.</summary>
        public static int ClampGrantedCoins(int coins, int potionsUsedToday)
        {
            int cap = DailyCapCoins(potionsUsedToday);
            return coins < 0 ? 0 : coins > cap ? cap : coins;
        }

        /// <summary>
        /// 오늘 갉아 먹은 창(초). ★★ <b>NaN을 0이 아니라 상한으로 보낸다</b> — 다른 클램프와
        /// <b>방향이 반대</b>라 반드시 이유를 남긴다(T-15-1-c).
        /// <para>0으로 보내면 <b>손상된 파일이 창을 리셋하는 무료 우회</b>가 됐다 — 그것이 방향을 뒤집은
        /// 원래 이유다. ★ 2026-09-29 이후 이 값으로 벌 수 있는 것이 없어서 두 방향 모두 사용자에게
        /// 아무 결과를 내지 않지만, <b>방향을 되돌리지 마라</b>: 되돌리는 변경은 「무해하다」를 근거로
        /// 삼게 되고, 그 근거는 재화가 다시 붙는 날 조용히 거짓이 된다.</para>
        /// </summary>
        public static double ClampIdleWindowSeconds(double usedSeconds)
        {
            if (double.IsNaN(usedSeconds)) return IdleWindowCapSeconds;
            if (usedSeconds < 0.0) return 0.0;
            return usedSeconds > IdleWindowCapSeconds ? IdleWindowCapSeconds : usedSeconds;
        }

        /// <summary>오늘 활쏘기 보상 판정을 통과한 누계(§20-3-b). ★ 2026-09-29 이후 <b>동전이 아니라
        /// 관문 단위</b>다 — 단위와 필드 이름(<c>archeryCoinsToday</c>)은 스키마 불변을 위해 그대로 두고,
        /// 이 값이 상한에 닿으면 <b>XP도 함께</b> 멈춘다(<see cref="ArcheryAwardCooldownSeconds"/> 문서).</summary>
        public static int ClampArcheryCoinsToday(int coins)
            => coins < 0 ? 0 : coins > ArcheryDailyCoinLimit ? ArcheryDailyCoinLimit : coins;

        /// <summary>등급 high-water mark 한 칸.</summary>
        public static int ClampStatTier(int tier)
            => tier < 0 ? 0 : tier > MaxStatTier ? MaxStatTier : tier;

        /// <summary>
        /// 동전 잔액. ★ <b>상한을 걸지 않는다</b> — security §4 · T-14-5-c · T-15-2의 확정 판정이다
        /// (<c>coinBalance = 999999</c> 편집 1회는 우리가 로컬로 막을 수 없고, 막으려 드는 순간
        /// 정직한 유저에게 "당신은 치터입니다"를 말하는 코드가 된다).
        /// <para>하한 0만 건다. 음수 잔액은 위조가 아니라 <b>우리 차감 버그의 신호</b>이고,
        /// 화면에 "−300동전"을 그릴 자리가 없다.</para>
        /// </summary>
        public static int ClampCoinBalance(int coins) => coins < 0 ? 0 : coins;

        /// <summary>고정된 날짜 경계 오프셋(분). 실존 시간대는 UTC−12:00 ~ UTC+14:00이라
        /// 그 바깥 값은 손상으로 보고 잘라 낸다.</summary>
        public const int MaxDayBoundaryOffsetMinutes = 14 * 60;

        /// <summary>날짜 경계 오프셋 정규화.</summary>
        public static int ClampDayBoundaryOffsetMinutes(int minutes)
            => minutes < -MaxDayBoundaryOffsetMinutes ? -MaxDayBoundaryOffsetMinutes
               : minutes > MaxDayBoundaryOffsetMinutes ? MaxDayBoundaryOffsetMinutes
               : minutes;

        // ====================================================================
        // 날짜 경계 — ★ 이 저장소에서 시각을 보는 유일한 지점
        // ====================================================================

        private static readonly DateTime UnixEpochUtc =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 고정된 오프셋 기준 "오늘"의 일자 번호. <b>시각을 인자로 받는다</b> — 이 함수 자체는
        /// 벽시계를 읽지 않으므로 테스트가 임의의 날짜를 밀어 넣을 수 있다.
        /// <para>오프셋을 <b>첫 실행에 고정하고 다시 안 바꾸는</b> 이유는 T-D-4다 —
        /// 시간대를 옮겨 다니며(또는 서머타임으로) 하루 경계를 여러 번 넘기는 것을 막는다.
        /// 여행자가 비행기에서 내려도 하루 경계가 흔들리지 않는다.</para>
        /// </summary>
        public static int LocalDayIndex(DateTime utcNow, int dayBoundaryOffsetMinutes)
        {
            DateTime shifted = utcNow.AddMinutes(ClampDayBoundaryOffsetMinutes(dayBoundaryOffsetMinutes));
            return (int)Math.Floor((shifted - UnixEpochUtc).TotalDays);
        }

        /// <summary>
        /// 리필(= 일일 상한 리셋 + 무료 회복제 부활 + 창 리셋)을 허용할 것인가.
        /// <para>두 조건이 <b>동시에</b> 참이어야 한다: ① 일자가 <b>전진</b>했다(T-4-b 래칫 —
        /// 시계를 되감아도 과거로 가지 않는다) ② 직전 리필로부터 단조 시계로
        /// <see cref="MinRefillGapSeconds"/>가 지났다(T-14-3-a — 세션 안에서 날짜를 반복해서
        /// 넘기는 (카) 공격을 완전히 막는다).</para>
        /// <para><paramref name="lastRefillMonotonic"/>의 초기값은
        /// <see cref="double.NegativeInfinity"/>다 — 그래야 <b>앱을 새로 켠 뒤 첫 리필이 항상 통과</b>한다.
        /// 그것이 이 규칙의 오탐이 0인 구조적 이유다.</para>
        /// </summary>
        public static bool MayRefill(int dayIndexNow, int maxDayIndexSeen,
            double nowMonotonic, double lastRefillMonotonic)
            => dayIndexNow > maxDayIndexSeen
               && (nowMonotonic - lastRefillMonotonic) >= MinRefillGapSeconds;

        // ====================================================================
        // 유휴 수급 1틱 — ★★★ 2026-09-29 <b>폐지</b>(DLC·재화 폐지, 사용자 확정)
        // ====================================================================
        //
        // 지운 것: <c>IdleTickResult</c>(구조체) · <c>IdleTick(...)</c>(7인자 순수 함수) ·
        //   요율 <c>IdleCoinsPerMinute</c>/<c>IdleCoinsPerSecond</c> ·
        //   불변식 <c>MinWindowToCeilingRatio</c>/<c>WindowToCeilingRatio</c> ·
        //   모델 진입점 <c>CurrencyModel.TickIdleIncome</c> ·
        //   배선 <c>Interaction/CharacterProgressionDirector.AccrueIdleIncome</c>와 그 정지/요약 로그.
        //
        // ★ <b>여기 있던 설계 지식 중 하나는 폐기하지 않는다</b>(다른 축에서 계속 참이다):
        //   «같은 1초가 두 번 지급되지 않는다»(I-7′)는 <b>분기를 하나로 두어 구조로</b> 만들었다.
        //   집중 모드 XP가 지금 그 형태를 쓰고 있다 — 유휴 버킷이 없어졌으므로 그쪽은 이제
        //   자동으로 참이지만, 두 번째 적립 축을 새로 만드는 라운드는 이 문장을 먼저 읽어라.
        //
        // ★★ <b>되살리지 마라.</b> 되살리려면 요율·상한·창·소수분 carry가 한꺼번에 돌아오고,
        //   그 넷 중 하나만 빠져도 「하루 종일 켜 뒀는데 0원」 또는 「정상 동작이 5초에 한 번
        //   고장으로 신고되는」 형태가 된다(둘 다 이 저장소에서 실제로 났다).

        // ====================================================================
        // ★ 집중 모드 <b>동전</b> 지급 — 2026-09-29 현재 <b>죽은 잔재 1개</b>만 남았다
        // ====================================================================
        //
        // 지운 것: <c>FocusCancelCoins</c> · <c>FocusCancelCoinsPerMinute</c>(취소 요율 20) ·
        //   모델 진입점 <c>CurrencyModel.PayFocusCancelCoins</c> ·
        //   배선 <c>FocusWatchDirector.PayCancelCoins</c>/<c>PayCompletionCoins</c>(둘 다).
        //
        // ★ 남은 <see cref="FocusCompletionCoins"/>는 <b>프로덕션 호출부가 0건</b>이다(2026-09-29 실측).
        //   상점 표면을 지우는 병렬 라운드와 같은 시각에 돌아 같은 커밋에서 지울 수 없었다 —
        //   그 라운드는 같은 날 착지했으므로 <b>다음 라운드가 이 함수와 FocusCoinsPerMinute를 함께 지운다.</b>
        //
        // ★★ 옛 설계 지식 보존(되살릴 때가 아니라 <b>비슷한 계단을 새로 만들 때</b> 읽어라):
        //   완주는 «초를 그대로 읽고 마지막에 floor», 취소는 «분을 먼저 floor한 뒤 요율»이었고
        //   그 비대칭은 <b>의도</b>였다. 취소를 「보상에 floor」로 읽으면 지급이 초 단위로 연속이 되어
        //   사용자가 취소 타이밍을 초 단위로 재는 동기가 생긴다 — 분 격자에 계단으로 묶으면 0이다.
        //   집중 모드 <b>XP</b>(아래 절)가 지금 그 비대칭을 그대로 쓰고 있다.

        /// <summary>
        /// 집중 세션 <b>완주</b> 동전. ★★ <b>2026-09-29 — 죽은 잔재다(프로덕션 호출부 0).</b>
        /// <see cref="FocusCoinsPerMinute"/>와 같은 이유로 선언만 남겼다. <b>새로 부르지 마라.</b>
        /// <para><paramref name="sessionDurationSeconds"/>는 <b>명목 세션 길이</b>(<c>분 × 60</c>)였다 —
        /// 누적 계측값이 아니다. 계측값을 넣으면 부동소수 누적 오차로 1499.9997초가 들어와
        /// <c>floor</c>가 599를 내고, 사용자는 그것을 「1동전 떼먹혔다」로 읽는다. 같은 계약이
        /// <see cref="FocusCompletionXp"/>에 <b>그대로 살아 있으므로</b> 이 문단은 여전히 값을 한다.</para>
        /// </summary>
        public static int FocusCompletionCoins(double sessionDurationSeconds)
        {
            if (double.IsNaN(sessionDurationSeconds) || !(sessionDurationSeconds > 0.0)) return 0;
            return ToFlooredNonNegativeInt(Math.Floor(FocusCoinsPerMinute * sessionDurationSeconds / 60.0));
        }

        /// <summary>이미 <c>floor</c>된 실수값을 <c>int</c>로 안전하게 내린다. 이름이 <c>…Coin…</c>이
        /// 아닌 이유: 아래 집중 모드 <b>XP</b> 지급(<see cref="FocusCompletionXp"/>·
        /// <see cref="FocusCancelXp"/>)도 <b>같은 위생</b>이 필요해 이 함수를 그대로 재사용한다 —
        /// 단위가 동전이든 XP든 "이미 floor된 음수 아닌 실수를 안전하게 int로 내린다"는 사실은
        /// 하나이고, 그 사실을 두 벌로 만들 이유가 없다.
        /// <para>상한 클램프는 <b>정책이 아니라 위생</b>이다 — 호출부가 말도 안 되는 경과 시간을 넘겨도
        /// <c>int</c> 캐스트가 <b>음수로 감기는</b> 일이 없어야 한다(캐스트 오버플로는 정의되지 않은
        /// 값을 내고, 그 값이 잔액/누계에 더해지면 우리 버그가 사용자 값을 망친다).</para></summary>
        private static int ToFlooredNonNegativeInt(double flooredValue)
        {
            if (!(flooredValue > 0.0)) return 0;
            return flooredValue >= int.MaxValue ? int.MaxValue : (int)flooredValue;
        }

        // ====================================================================
        // ★★ 집중 모드 XP 지급 — 위 동전 지급과 「완전히 같은 구조」 (2026-09-07, design-systems
        //    §15 확정 + 부록D). 상수만 다르다 — 새로 발명하지 않는다.
        // ====================================================================
        //
        //   완주 = floor( FocusXpPerMinute × 세션초 / 60 )        ← 초를 그대로 읽고 <b>마지막에</b> floor
        //   취소 = floor( 경과초 / 60 ) × FocusCancelXpPerMinute  ← <b>분을 먼저</b> floor한 뒤 요율
        //
        // ★ 이 비대칭이 필요한 이유는 위 동전 절과 <b>글자 하나까지 동일</b>하다 — 취소를 「XP에
        //   floor」로 읽으면 지급이 초 단위로 연속이 되어 사용자가 취소 타이밍을 초 단위로 재는
        //   동기가 생긴다. 완주가 초를 그대로 읽는 이유는 데모(90초)·최소(60초) 같은 격자 밖
        //   세션 길이를 같은 식 하나로 처리하기 위해서다(design-systems §15-1).
        //
        // ★★ 왜 「일일 상한 밖」이 아니라 상한이 있는가 — 코인과 여기서 갈라진다(design-systems §15-4).
        //   코인은 "얼마나 빨리 모으는가"가 레벨링 페이싱에 영향을 주지 않는 순수 자원 풀이라 상한
        //   밖이어도 안전했다(§22-13, FocusWatchDirector.PayCompletionCoins/PayCancelCoins 문서).
        //   XP는 정반대다 — 레벨 캡·LevelBonus·소프트캡 전체가 XP 하나를 조율하려는 설계이므로,
        //   XP를 상한 밖으로 열면 그 페이싱이 집중 모드 사용량에 따라 조용히 무너진다. 그래서
        //   <see cref="FocusXpDailyCap"/>이 새로 생겼고, 코인 쪽(§22-13)과 <b>이 한 가지만</b> 다르다 —
        //   floor 위치·비대칭 구조·완주:취소 비율(5:6, 아래 참고)은 전부 그대로 복제했다.

        /// <summary>집중 모드 완주 시의 분당 XP. design-systems §15-3 확정값 — 세 조건을 동시에
        /// 만족하는 유일한 값이다: ① 취소 요율(<see cref="FocusCancelXpPerMinute"/>)이 정수로 딱
        /// 떨어짐 ② 실사용 프리셋(15/25/50분)에서 전부 깔끔한 XP(90/150/300)가 나옴 ③ 일일 상한
        /// (<see cref="FocusXpDailyCap"/>)에 도달하는 시간이 3시간으로 현실적임.</summary>
        public const int FocusXpPerMinute = 6;

        /// <summary>
        /// 집중 모드 <b>중도 취소</b>의 분당 XP. <c>FocusXpPerMinute × 5 / 6</c>(= 정확히 5)로
        /// 유도할 수도 있었지만 리터럴을 쓴다 — "선언 형태는 판단이 아니라 컴파일"이다.
        /// <para>취소:완주 = 5:6이고, 폐지된 코인 쪽 요율(20:24)도 <b>같은 비율</b>이었다
        /// (design-systems §15-2) — "완주가 항상 이득"이라는 메시지를 한 벌로 통일한 결과다.
        /// ★ 코인 요율이 사라졌으므로 지금 그 비율을 지키는 곳은 <b>여기 하나</b>다.
        /// <c>Tests</c>가 <c>FocusCancelXpPerMinute × 6 == FocusXpPerMinute × 5</c>를 항등식으로 고정한다.</para>
        /// </summary>
        public const int FocusCancelXpPerMinute = 5;

        /// <summary>
        /// 집중 모드 XP의 하루 상한. design-systems §15-4-3 확정값 — 임의가 아니라 ① 활쏘기 채널의
        /// 기존 상한(<c>72회 × 15XP = 1,080</c>, 15는 <c>StickConfig.progressionBullseyeXp</c>라
        /// 컴파일타임 상수로 유도할 수 없어 리터럴로 못박는다) ② 패시브 하루치(24h×90XP/h=2,160)의
        /// <b>정확히 절반</b>이다 — "패시브가 기준선, 활쏘기·집중모드는 각각 최대 그 절반까지의
        /// 가속 채널"이라는 대칭을 의도적으로 만든 값이다(§15-5가 "집중모드를 상한까지 밀어붙여도
        /// 활쏘기 상한과 정확히 같아진다"를 검산했다).
        /// <para>★ 필드가 아니라 상수다 — 코인의 <see cref="DailyCapCoins"/>와 달리 회복제 같은
        /// 가변 입력이 없어 함수로 뽑을 이유가 없다. 오늘 쓴 양(<c>CurrencyModel.FocusXpToday</c>)만
        /// 저장하고 상한 자체는 저장하지 않는다(T-D-9와 같은 이유 — 위조 대상을 만들지 않는다).</para>
        /// </summary>
        public const int FocusXpDailyCap = 1080;

        /// <summary>
        /// 집중 세션 <b>완주</b> XP. <paramref name="sessionDurationSeconds"/>는 <see cref="FocusCompletionCoins"/>와
        /// <b>완전히 같은 계약</b>이다 — 명목 세션 길이(분×60)를 넣는다, 계측 누적값이 아니다.
        /// 일일 상한 클램프는 여기서 하지 않는다(<see cref="ClampFocusXpToday"/>가 별도 지점).
        /// </summary>
        public static int FocusCompletionXp(double sessionDurationSeconds)
        {
            if (double.IsNaN(sessionDurationSeconds) || !(sessionDurationSeconds > 0.0)) return 0;
            return ToFlooredNonNegativeInt(Math.Floor(FocusXpPerMinute * sessionDurationSeconds / 60.0));
        }

        /// <summary>
        /// 집중 세션 <b>중도 취소</b> XP. <paramref name="elapsedSeconds"/>는 <c>명목 세션 길이 − 잔여 초</c>다
        /// (폐지된 코인 쪽 <c>FocusCancelCoins</c>와 같은 계약이었다). <b>1분 미만은 0XP</b>이고,
        /// 별도 하한 규칙을 두지 않는다 — <c>floor(경과/60) = 0</c>에서 저절로 나온다.
        /// </summary>
        public static int FocusCancelXp(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || !(elapsedSeconds > 0.0)) return 0;
            return ToFlooredNonNegativeInt(Math.Floor(elapsedSeconds / 60.0) * FocusCancelXpPerMinute);
        }

        /// <summary>오늘 집중 모드로 받은 XP. <see cref="ClampArcheryCoinsToday"/>와 같은 모양의
        /// 클램프다 — <see cref="FocusXpDailyCap"/>이 어떤 경우에도 위조 상한이다.</summary>
        public static int ClampFocusXpToday(int xp)
            => xp < 0 ? 0 : xp > FocusXpDailyCap ? FocusXpDailyCap : xp;

        // ====================================================================
        // 불변식 검산 — ★★★ 2026-09-29 <b>폐지</b>(T-D-15의 전제가 사라졌다)
        // ====================================================================
        //
        // 지운 것: <c>MinWindowToCeilingRatio</c>(1.5) · <c>WindowToCeilingRatio</c>(실측 2.304배).
        //
        // ★ <b>왜 「값이 여전히 참이니 남겨 두자」가 아닌가.</b> 이 비율의 분자는 유휴 요율
        //   (<c>IdleWindowCapMinutes × IdleCoinsPerMinute</c>)이었고 그 요율이 삭제됐다. 분자를
        //   1로 바꾸거나 상수를 남겨 두면 <b>비율이 참인 채로 아무것도 보장하지 않는</b> 계기가 된다 —
        //   이 저장소가 반복해 당한 「죽은 프로브가 산 프로브와 똑같이 생겼다」 그 형태다.
        //   T-D-15가 지키려던 것(「창이 정상 사용자에게 먼저 걸려 오탐이 기본값이 되는」 일)은
        //   창을 갉는 코드가 없어서 <b>구조적으로</b> 일어나지 않는다.
    }
}
