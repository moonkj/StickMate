using System;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 재화(동전) 경제의 <b>순수 규칙</b> — 상태를 하나도 들고 있지 않다.
    ///
    /// ============================================================================
    /// 이 파일이 존재하는 이유 — 「같은 사실이 두 곳에서 계산되지 않게」
    /// ============================================================================
    /// 상한·요율·클램프·창 소비는 <b>세 곳</b>에서 필요해진다: 세이브 로드(정규화), 매 틱 수급,
    /// 그리고 화면 표시(오늘 잔여). 그 셋이 각자 계산하면 그 순간 어긋나고, 어긋난 뒤에는
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
        // 요율 — §18-2 확정 상수표
        // ====================================================================

        /// <summary>집중 모드 완주 시의 분당 동전. 사용자 최초 지시값.</summary>
        public const int FocusCoinsPerMinute = 24;

        /// <summary>
        /// 집중 모드 <b>중도 취소</b>의 분당 동전. 정본 §13-3 표(「불변」) — 완주의 <b>83.3%</b>.
        ///
        /// <para>★★ <b>선언 「형태」는 design-systems 확인 대기 중이다(값 20은 확정, 2026-09-06).</b>
        /// 리터럴로 둘 것인지 <c>FocusCoinsPerMinute * 5 / 6</c>(= 정확히 20)으로 유도할 것인지만
        /// 열려 있다. <b>어느 쪽이든 이 줄 하나만 바꾸면 되고 값도 동작도 바뀌지 않는다</b> —
        /// <c>Tests/EditMode/FocusSessionPayoutTests</c>가 <c>FocusCancelCoinsPerMinute × 6 ==
        /// FocusCoinsPerMinute × 5</c>(83.3%)를 매 실행 확인하므로, 두 형태 모두 같은 관문을 지난다.
        /// <b>지금 리터럴을 고른 이유는 판단이 아니라 컴파일이다</b> — 선언이 없으면 트리 전체가
        /// 빌드되지 않아 병렬 라운드가 전부 막힌다.</para>
        ///
        /// <para>참고로 <see cref="IdleCoinsPerMinute"/>가 유도식인 것은 §18-2가 <b>"집중의 정확히 1/2"</b>이라는
        /// <b>관계</b>를 정본으로 정했기 때문이고, 여기는 §13-3이 <b>20이라는 숫자</b>를 정본으로 적었다 —
        /// 그 차이가 형태를 가르는 축이다.</para>
        ///
        /// <para>스팸 이득이 없다는 것은 §22-12가 전수로 확인했다 — 어떤 취소 주기(0.5·0.99·1.0·1.5·5·60분)로도
        /// 완주 시급(1,440)을 넘지 못하고, 1분 미만 취소는 <b>0동전</b>이라 바닥이 자동으로 막힌다.
        /// 그래서 최소 보상 하한·세션 쿨다운·세션당 고정비는 <b>전부 기각됐다</b>(DS-8).
        /// <b>되살리지 마라</b> — 격자별 최대 수입이 동일해서 방어할 이득이 없다.</para>
        /// </summary>
        public const int FocusCancelCoinsPerMinute = 20;

        /// <summary>온라인 유휴의 분당 동전. ★ 숫자를 따로 적지 않고 <b>집중의 정확히 1/2</b>로
        /// 유도한다 — §18-2가 정한 관계가 그것이고, 둘을 각각 적으면 한쪽만 바뀌는 날 관계가
        /// 조용히 깨진다. 오프라인 요율은 <b>0</b>이고, 그건 상수가 아니라
        /// "오프라인 수급 코드가 존재하지 않는다"로 표현된다(I-13′).</summary>
        public const int IdleCoinsPerMinute = FocusCoinsPerMinute / 2;

        /// <summary>유휴 초당 동전. 정수 나눗셈을 피하려고 double로 유도한다.</summary>
        public const double IdleCoinsPerSecond = IdleCoinsPerMinute / 60.0;

        // ====================================================================
        // 일일 상한 — §18-2 · T-14-5-b · T-D-9
        // ====================================================================

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
        // 8시간 창 — T-15
        // ====================================================================

        /// <summary>하루에 <b>동전이 실제로 지급될 수 있는</b> 분. 사용자 최초 지시(480분).
        /// ★ "앱이 켜져 있던 분"이 아니다 — T-15-1-a.</summary>
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

        /// <summary>[오늘 할일] 하루 1회 지급액(§0-2-6).</summary>
        public const int TodoDailyCoins = 300;

        /// <summary>활쏘기 1회 상금.</summary>
        public const int ArcheryCoinsPerAward = 20;

        /// <summary>활쏘기 상금의 쿨다운(초). ★ <b>세이브에 남기지 않는다</b> — 벽시계 유닉스 초를
        /// 저장하던 <c>lastArcheryCoinUnix</c>는 T-3-a 위반이라 폐기됐다(§20-3). 쿨다운은
        /// 단조 시계로 세션 안에서만 산다.</summary>
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
        // 상점 가격 — 등급에서 파생한다 (§21-10-a (4) · U-17 확정 §21-5)
        // ====================================================================
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
        // ★ 2026-09-06 — <b>배선이 붙었다</b>(이 자리에 «배선은 아직 없다»가 남아 있었다).
        //   구매 플로우는 <c>CharacterInfoWindow.Shop</c>이고, 그것이 <c>TryPurchaseItem</c>을 부른다.
        //   값을 미리 여기 한 곳에 못박아 둔 목적은 그대로 달성됐다 — 화면이 9,600을 손으로 적지
        //   않고 <c>ShopPriceCoins</c>가 등급에서 파생시킨다(<c>ShopPurchaseFlowTests</c>가 그 항등을 잠근다).

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
        // 첫 실행 시드 — U-42 확정 (리더 승인 2026-09-05)
        // ====================================================================

        /// <summary>
        /// ★ 첫 실행 시드 동전 — <b>U-42 확정값</b>(리더 승인 2026-09-05).
        /// <b>기존 사용자를 포함한 전원에게 평생 1회</b> 지급한다.
        ///
        /// <para><b>왜 1,200인가</b>(<c>ECONOMY_SPEC</c> §0-6-4(a)): 0이면 <b>첫 50분간 상점 버튼이
        /// 전부 회색</b>이라 1일차에 누를 것이 없다. 1,200은 §0-2-3이 구단위 60으로 만들려던 구조를
        /// 그대로 옮긴 값이고(×20), 그 구조는 <i>"시드가 2개를 사고, 그날 수입이 3번째를 사고,
        /// 잔액이 남아 「다음은 모아야 한다」가 즉시 성립한다"</i>이다.
        /// ★ <b>1,800을 안 고른 이유</b>: 셋 다 살 수 있으면 첫 화면이 "고르는 화면"이 아니라
        /// "전부 누르는 화면"이 된다.</para>
        ///
        /// <para>★★ <b>기존 사용자가 받을 수 있는 이유 — 이 값이 0이었던 덕분이다.</b>
        /// <see cref="CanGrantSeed"/>가 <c>SeedCoins &gt; 0</c>을 요구했으므로 미확정 기간에
        /// <b>지급도 안 했고 <c>seedGranted</c> 플래그도 안 세웠다</b>. 그래서 지금까지의 모든 세이브에서
        /// 그 필드는 <c>false</c>이고, 이 상수가 켜지는 순간 전원이 첫 지급 대상이 된다.
        /// <b>순서를 반대로 했으면(플래그 먼저) 되돌릴 수 없는 손실이었다</b> — 그 방어가
        /// 실제로 값을 한 셈이므로 기록으로 남긴다.</para>
        ///
        /// <para>★ <b>design-systems 확인 요청 1건(값을 막지는 않는다)</b>: §0-6-4(a)의 유도표는
        /// <i>"Lv.1에 살 수 있는 것은 외형 3슬롯 rank0 3종(각 600)뿐"</i>을 전제로 「2/3을 산다」를
        /// 셌는데, <b>출하 카탈로그에서 그 3종은 <c>requiredLevel = 1</c>이라 이미 무상 보유</b>다
        /// (<c>look.hair.cowlick</c>·<c>look.fx.none</c>·<c>look.pet.ball</c>, 골든 실측).
        /// <see cref="ItemCatalogEntry.IsOwned"/>가 레벨 파생 ∪ 구매이므로 <b>Lv.1 7종 전부가 공짜</b>이고,
        /// 시드로 처음 살 수 있는 것은 레벨 위쪽 아이템이다. <b>금액 자체는 리더가 별도 근거
        /// (「최저가 600도 이틀 걸리는 첫 30분」)로 승인했으므로 그대로 간다</b> — 다만 유도표의
        /// 분모가 출하 데이터와 다르다.</para>
        /// </summary>
        public const int SeedCoins = 1200;

        /// <summary>시드를 지급할 수 있는 상태인가. <b>평생 1회</b>이고,
        /// 금액이 0이면(과거 U-42 미확정 상태) 지급도 플래그 설정도 하지 않는다.</summary>
        public static bool CanGrantSeed(bool seedAlreadyGranted)
            => !seedAlreadyGranted && SeedCoins > 0;

        // ====================================================================
        // ★ 클램프 — "복구"가 아니라 "정규화"다 (T-14-5-b)
        // ====================================================================
        // 저장된 값을 그대로 믿지 않는다. 읽는 즉시 다듬고, 실패도 경고도 남기지 않는다.
        // 이건 방어이기 전에 <b>위생</b>이다 — 손상된 세이브·구버전 파일·`.writing` 잔해에도
        // 같은 코드가 같은 답을 낸다. 값을 거부하지 않으므로 "우리 버그를 치터보다 먼저 만나는"
        // 종류의 검사가 아니다(§4-1-4).

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
        /// <para>0으로 보내면 <b>손상된 파일이 창을 리셋하는 무료 우회</b>가 된다. 상한으로 보내면
        /// 손상 파일을 만난 정상 사용자가 그날 유휴 수급만 못 하고, 일일 상한 2,500은 그대로
        /// 살아 있어 활쏘기·집중 모드·[오늘 할일]로 계속 벌 수 있다 — <b>잠기지 않는다</b>.</para>
        /// </summary>
        public static double ClampIdleWindowSeconds(double usedSeconds)
        {
            if (double.IsNaN(usedSeconds)) return IdleWindowCapSeconds;
            if (usedSeconds < 0.0) return 0.0;
            return usedSeconds > IdleWindowCapSeconds ? IdleWindowCapSeconds : usedSeconds;
        }

        /// <summary>오늘 활쏘기로 받은 동전(§20-3-b — 위조 이득 상한이 곧 이 값이다).</summary>
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
        // 유휴 수급 1틱 — 순수 함수 (T-15-1-b)
        // ====================================================================

        /// <summary>한 틱의 결과. 세 값을 한꺼번에 돌려주는 이유는 <b>세 값이 한 사건</b>이기
        /// 때문이다 — 지급액만 받고 창 소비를 따로 계산하면 그 둘이 어긋난다.</summary>
        public readonly struct IdleTickResult
        {
            /// <summary>이번 틱에 지급할 동전(정수).</summary>
            public readonly int CoinsGranted;

            /// <summary>이번 틱이 창에서 갉아먹은 초. ★ <b>지급이 일어난 초만</b> 값이 있다
            /// (T-15-1-a·T-D-13) — 상한에 걸려 한 푼도 못 버는 동안 창이 닳으면
            /// 그건 방어가 아니라 버그다.</summary>
            public readonly double WindowSecondsSpent;

            /// <summary>다음 틱으로 넘기는 <b>동전 소수분</b>.
            /// <para>★ 이게 없으면 60fps에서 한 틱의 수입이 0.0033동전이라 <c>(int)</c> 절단으로
            /// <b>전부 사라진다</b>(하루 종일 켜 둬도 0원). 같은 계열의 실제 사고를 security T-3-c가
            /// "적게 쌓이는 방향의 공정성 문제"로 적어 뒀다. 소수분은 세이브에 넣지 않는다 —
            /// 최대 1동전 미만이라 잃어도 무해하고, 필드 하나를 아끼는 쪽이 낫다.</para></summary>
            public readonly double CarryCoins;

            public IdleTickResult(int coinsGranted, double windowSecondsSpent, double carryCoins)
            {
                CoinsGranted = coinsGranted;
                WindowSecondsSpent = windowSecondsSpent;
                CarryCoins = carryCoins;
            }
        }

        /// <summary>
        /// 유휴 수급 한 틱. <b>세 축이 전부 여기서 만난다</b>(T-15-3): ① 일일 상한 ② (호출부의)
        /// 리필 간격 ③ 8시간 창.
        ///
        /// <para><paramref name="deltaSeconds"/>는 <b>단조 시계 델타</b>여야 한다
        /// (<c>Time.realtimeSinceStartupAsDouble</c>의 차). 벽시계 델타를 넣으면 T-3-a가 깨진다 —
        /// 이 함수는 그것을 알 수 없으므로 호출부의 책임이고,
        /// <c>Tests/EditMode/DailyLimitClampAuditTests</c>가 소스 스캔으로 그 책임을 잠근다.</para>
        ///
        /// <para><paramref name="idleWindowCapSeconds"/>를 인자로 받는 이유는 <b>네거티브 컨트롤</b>
        /// 때문이다 — <see cref="double.PositiveInfinity"/>를 넣어 "창이 없으면 실제로 얼마까지
        /// 새는가"를 재야 5,760이 창의 성과임이 증명된다(T-15-7 B).</para>
        /// </summary>
        public static IdleTickResult IdleTick(
            double deltaSeconds,
            bool isIdleEarning,
            int todayGrantedCoins,
            int potionsUsedToday,
            double idleWindowUsedSeconds,
            double idleWindowCapSeconds,
            double carryCoins)
        {
            if (double.IsNaN(carryCoins) || carryCoins < 0.0) carryCoins = 0.0;

            // 비유휴/역행/NaN 델타 — 아무 일도 없다. 창도 안 갉는다(T-15-1-a).
            if (!isIdleEarning || !(deltaSeconds > 0.0))
                return new IdleTickResult(0, 0.0, carryCoins);

            int room = DailyCapCoins(potionsUsedToday) - todayGrantedCoins;
            if (room <= 0) return new IdleTickResult(0, 0.0, carryCoins);

            double windowRoom = idleWindowCapSeconds - ClampIdleWindowSeconds(idleWindowUsedSeconds);
            if (!(windowRoom > 0.0)) return new IdleTickResult(0, 0.0, carryCoins);

            double paidSeconds = deltaSeconds < windowRoom ? deltaSeconds : windowRoom;
            double earned = carryCoins + paidSeconds * IdleCoinsPerSecond;

            int pay = (int)Math.Floor(earned);
            double carry = earned - pay;

            // 상한에 걸리면 남은 소수분은 버린다 — 오늘은 어차피 못 받는다.
            if (pay >= room) { pay = room; carry = 0.0; }

            return new IdleTickResult(pay, paidSeconds, carry);
        }

        // ====================================================================
        // ★★ 집중 모드 지급 — floor의 「위치」가 완주와 취소에서 다르다 (DS-5′ · §22-12)
        // ====================================================================
        //
        //   완주 = floor( FocusCoinsPerMinute × 경과초 / 60 )        ← 초를 그대로 읽고 <b>마지막에</b> floor
        //   취소 = floor( 경과초 / 60 ) × FocusCancelCoinsPerMinute  ← <b>분을 먼저</b> floor한 뒤 요율
        //
        // ★ <b>이 비대칭은 의도다. 「통일」하지 마라.</b> 90.5초에서 취소가 20이냐 30이냐로 갈리고
        //   (1.5배), 149.9초에서 40이냐 49냐로 갈린다(§22-12 표). 취소를 「동전에 floor」로 읽으면
        //   지급이 <b>초 단위로 연속</b>이 되어 사용자가 취소 타이밍을 초 단위로 재는 동기가 생긴다.
        //   분 격자에 계단으로 묶으면 그 동기가 0이다. 완주 쪽이 반대로 초를 그대로 읽는 이유는
        //   데모 90초·최소 60초 같은 <b>격자 밖 진입로를 같은 식 하나로</b> 처리하기 위해서다.
        //   <c>Tests/EditMode/FocusSessionPayoutTests</c>가 기각된 해석 (나)를 대조로 함께 못박는다.
        //
        // ★ <b>여기가 집중 지급 산식의 유일한 출처다.</b> 화면(「[그만두기]가 지금 얼마인지 말한다」,
        //   UX_WIDGETS R2-5)도 이 함수를 부른다 — 미리보기가 자기 식을 따로 쓰면 그 순간
        //   "표시된 금액과 실제 지급액이 다르다"가 되고, 그건 우리가 반복해 당한 형태다
        //   (같은 사실이 두 곳에서 계산되면 그게 다음 버그다 — CLAUDE.md).

        /// <summary>
        /// 집중 세션 <b>완주</b> 지급액. <paramref name="sessionDurationSeconds"/>는
        /// <b>명목 세션 길이</b>(<c>분 × 60</c>)를 넣는다 — 누적 계측한 경과 시간이 아니다.
        /// <para>그래야 25분 완주가 <b>정확히 600</b>이 된다. 계측값을 넣으면 부동소수 누적 오차로
        /// 1499.9997초가 들어와 <c>floor</c>가 599를 내고, 사용자는 그것을 「1동전 떼먹혔다」로 읽는다.
        /// 명목값은 <c>minutes × 60f</c>를 <c>/60</c>이 <b>정확히</b> 복원한다(float 가수 24비트,
        /// m ≤ 60이라 오차 0 — §22-11).</para>
        /// </summary>
        public static int FocusCompletionCoins(double sessionDurationSeconds)
        {
            if (double.IsNaN(sessionDurationSeconds) || !(sessionDurationSeconds > 0.0)) return 0;
            return ToCoinInt(Math.Floor(FocusCoinsPerMinute * sessionDurationSeconds / 60.0));
        }

        /// <summary>
        /// 집중 세션 <b>중도 취소</b> 지급액. <paramref name="elapsedSeconds"/>는
        /// <c>명목 세션 길이 − 잔여 초</c>다.
        /// <para><b>1분 미만은 0동전</b>이고, 그것은 별도 규칙이 아니라 <c>floor(경과/60) = 0</c>에서
        /// <b>저절로</b> 나온다. 하한 규칙을 따로 넣지 마라 — 넣는 순간 두 곳이 같은 사실을 말하게 된다.</para>
        /// </summary>
        public static int FocusCancelCoins(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || !(elapsedSeconds > 0.0)) return 0;
            return ToCoinInt(Math.Floor(elapsedSeconds / 60.0) * FocusCancelCoinsPerMinute);
        }

        /// <summary>이미 <c>floor</c>된 동전 실수값을 <c>int</c>로 안전하게 내린다.
        /// 상한 클램프는 <b>정책이 아니라 위생</b>이다 — 호출부가 말도 안 되는 경과 시간을 넘겨도
        /// <c>int</c> 캐스트가 <b>음수로 감기는</b> 일이 없어야 한다(캐스트 오버플로는 정의되지 않은
        /// 값을 내고, 그 값이 잔액에 더해지면 우리 버그가 사용자 잔액을 망친다).</summary>
        private static int ToCoinInt(double flooredCoins)
        {
            if (!(flooredCoins > 0.0)) return 0;
            return flooredCoins >= int.MaxValue ? int.MaxValue : (int)flooredCoins;
        }

        // ====================================================================
        // 불변식 검산 — 이 파일 안에서 닫힌다 (T-D-15)
        // ====================================================================

        /// <summary>
        /// T-D-15: 창(480분 × 12동전)이 하루 절대 천장(2,500)의 <b>1.5배 이상</b>이어야 한다.
        /// 이 비율이 1.0 아래로 내려가면 <b>창이 정상 사용자에게 먼저 걸려</b> 오탐이 기본값이 된다.
        /// 요율·상한·창 중 하나만 바꿔도 이 비율이 움직이므로, 테스트가 매 실행 확인한다.
        /// </summary>
        public const double MinWindowToCeilingRatio = 1.5;

        /// <summary>현재 상수 조합의 창/천장 비율. 지금은 5,760 / 2,500 = 2.304배다.</summary>
        public static double WindowToCeilingRatio =>
            (IdleWindowCapMinutes * (double)IdleCoinsPerMinute) / HardCeilingCoins;
    }
}
