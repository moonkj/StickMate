namespace StickMate.Core
{
    /// <summary>
    /// 스탯 4종. 표시 순서는 이 enum 순서 그대로다(<c>STAT_ORDER = ['집중력','관찰력','매력','민첩']</c> —
    /// 인계본 <c>equipment-screen.dc.html</c> line 700, <c>docs/DESIGN_SYSTEMS_STATS.md</c> §1-1).
    /// <para>값을 <b>끼워 넣지 마라</b>. 이 순서가 곧 <c>CurrencyModel.StatTierReached(slotIndex)</c>의
    /// 칸 번호이고, 그 배열은 <b>저장 파일에 그대로 적힌다</b>(v10 <c>statTierReached</c>). 중간에 값을
    /// 하나 끼우면 모든 사용자의 영구 해금 눈금이 한 칸씩 밀리고, 파일을 열어봐도 눈에 안 띈다
    /// (<c>EquipmentSlot</c>이 겪은 그 함정 — <c>CharacterSaveStore</c>의 "인덱스 vs 아이디" 문단).</para>
    /// </summary>
    public enum CharacterStat
    {
        /// <summary>집중력 — 주스탯 슬롯은 모자(HEAD).</summary>
        Focus = 0,

        /// <summary>관찰력 — 주스탯 슬롯은 안경(EYES).</summary>
        Observation = 1,

        /// <summary>매력 — 주스탯 슬롯은 넥타이(NECK).</summary>
        Charm = 2,

        /// <summary>민첩 — 주스탯 슬롯은 망토(BACK = <see cref="EquipmentSlot.Shoulders"/>).</summary>
        Agility = 3,
    }

    /// <summary>
    /// ★ <b>애셋이 선언하는 부스탯 방향</b>. 기본 42종은 이 값을 <b>쓰지 않는다</b> —
    /// 그쪽 부스탯은 <c>ItemCatalog</c>의 코드 표(<c>SubStatTable</c>)가 정본이다.
    /// 이 열거형이 존재하는 이유는 <b>팩 아이템이 코드 표에 못 들어가기 때문</b>이다(§21-10-a).
    ///
    /// <para>★★ <b>타입이 <see cref="CharacterStat"/>가 아닌 것은 우연이 아니라 요구사항이다</b> —
    /// <see cref="DeclaredRarity"/>가 <see cref="ItemRarity"/>가 아닌 것과 <b>같은 이유이고,
    /// 여기서는 더 나쁘다.</b> Unity는 <c>.asset</c>에 키가 없으면 필드를 C# 기본값으로 두는데
    /// <see cref="CharacterStat.Focus"/>가 <b>0</b>이다. 그 타입으로 필드를 만들면 기본 42종이
    /// <b>파일을 한 바이트도 안 고쳤는데</b> 전부 「집중력을 부스탯으로 선언함」이 되고,
    /// 그 값이 코드 표를 이기는 구조였다면 <b>24종의 부스탯이 통째로 집중력 한 방향</b>이 된다
    /// (§21-2-e가 검산한 «각 스탯 정확히 6개, 편차 0»이 «집중력 24 · 나머지 0»으로 무너진다).</para>
    ///
    /// <para><b>0의 이름이 <see cref="None"/>인 것도 요구사항이다.</b> 인스펙터 드롭다운 맨 위에
    /// <c>None</c>이 보이므로 "안 적으면 부스탯 없음"이 문서가 아니라 UI가 된다.
    /// <b>절대 하지 말 것</b>: <see cref="None"/>을 0이 아닌 값으로 바꾸거나 앞에 항목을 끼워 넣는 것.</para>
    /// </summary>
    public enum DeclaredSubStat
    {
        /// <summary>★ <b>선언 없음</b>. 기본 42종이 전부 여기다(키를 안 적었으므로).
        /// 팩의 <b>외형 슬롯</b> 아이템도 여기다 — 외형은 스탯 기여가 0이라는 구조적 사실이다(§14-1).
        /// <b>0이어야 한다.</b></summary>
        None = 0,

        /// <summary>집중력을 가리킨다(= <see cref="CharacterStat.Focus"/>).</summary>
        Focus = 1,

        /// <summary>관찰력을 가리킨다(= <see cref="CharacterStat.Observation"/>).</summary>
        Observation = 2,

        /// <summary>매력을 가리킨다(= <see cref="CharacterStat.Charm"/>).</summary>
        Charm = 3,

        /// <summary>민첩을 가리킨다(= <see cref="CharacterStat.Agility"/>).</summary>
        Agility = 4,
    }

    /// <summary>
    /// 선언 ↔ 부스탯 방향 사이의 <b>유일한 다리</b>(<c>DeclaredRarityRules</c>와 같은 자리).
    /// 두 열거형의 값을 손으로 더하거나 빼는 코드가 이 파일 밖에 생기면 그 순간 두 번째 진실이 된다.
    /// </summary>
    public static class DeclaredSubStatRules
    {
        /// <summary>
        /// 선언을 부스탯 방향으로 푼다. <b>선언이 없으면 <c>false</c></b>이고 그때
        /// <paramref name="stat"/>는 쓰면 안 되는 값이다.
        /// <para>★ 뺄셈(<c>(CharacterStat)((int)declared - 1)</c>)을 쓰지 않는 이유는
        /// <c>DeclaredRarityRules.TryResolve</c> 문단과 같다 — 뺄셈은 "두 열거형이 영원히 나란히 커야
        /// 한다"는 <b>문서에 없는 계약</b>을 만들고, 그 계약은 언젠가 조용히 깨진다.</para>
        /// </summary>
        public static bool TryResolve(DeclaredSubStat declared, out CharacterStat stat)
        {
            switch (declared)
            {
                case DeclaredSubStat.Focus: stat = CharacterStat.Focus; return true;
                case DeclaredSubStat.Observation: stat = CharacterStat.Observation; return true;
                case DeclaredSubStat.Charm: stat = CharacterStat.Charm; return true;
                case DeclaredSubStat.Agility: stat = CharacterStat.Agility; return true;
                default: stat = CharacterStat.Focus; return false;   // None — 쓰면 안 되는 값
            }
        }

        /// <summary>선언이 있는가. <see cref="TryResolve"/>와 <b>같은 판정</b>이어야 하므로 그 함수를
        /// 그대로 부른다(두 벌로 적으면 둘이 갈라진다).</summary>
        public static bool IsDeclared(DeclaredSubStat declared) => TryResolve(declared, out _);

        /// <summary>선언을 <c>ItemCatalogEntry.SubStat</c>이 쓰는 정수로. 선언이 없으면
        /// <see cref="EquipmentStatRules.NoStat"/>다 — 「없음」을 스탯 하나로 빌려 표현하지 않는다.</summary>
        public static int ToSubStatValue(DeclaredSubStat declared)
            => TryResolve(declared, out CharacterStat stat) ? (int)stat : EquipmentStatRules.NoStat;
    }

    /// <summary>
    /// 스탯 4칸을 <b>할당 없이</b> 들고 다니는 그릇. 배열을 쓰면 카드가 갱신될 때마다(0.25초 주기)
    /// 쓰레기가 생기고, 필드 4개를 늘어놓으면 호출부가 순서를 손으로 맞추다 어긋난다.
    /// </summary>
    public readonly struct StatVector4
    {
        public readonly int Focus;
        public readonly int Observation;
        public readonly int Charm;
        public readonly int Agility;

        public StatVector4(int focus, int observation, int charm, int agility)
        {
            Focus = focus;
            Observation = observation;
            Charm = charm;
            Agility = agility;
        }

        /// <summary>스탯 하나. 범위 밖은 0이다 — 카드 한 칸의 결손이 창 전체를 못 열게 만들지 않는다
        /// (<c>ItemCatalog.Rarity</c>가 못 찾는 자리를 <c>Common</c>으로 돌려주는 것과 같은 방침).</summary>
        public int Of(CharacterStat stat)
        {
            switch (stat)
            {
                case CharacterStat.Focus: return Focus;
                case CharacterStat.Observation: return Observation;
                case CharacterStat.Charm: return Charm;
                case CharacterStat.Agility: return Agility;
                default: return 0;
            }
        }

        internal StatVector4 Plus(in StatVector4 other)
            => new StatVector4(Focus + other.Focus, Observation + other.Observation,
                Charm + other.Charm, Agility + other.Agility);
    }

    /// <summary>
    /// 스탯 슬롯 한 칸의 착용 상태 — <see cref="EquipmentStatRules.Evaluate"/>의 입력 한 조각.
    /// <para>카탈로그를 다시 뒤지지 않고 <b>값으로</b> 받는 이유는 검증 가능성이다: 테스트가
    /// 임의의 등급·부스탯·테마 조합(그리고 아직 존재하지 않는 DLC)을 그대로 밀어 넣을 수 있어야
    /// I-4("DLC를 무제한 추가해도 상한이 안 변한다")를 <b>실제로</b> 잴 수 있다.</para>
    /// </summary>
    public readonly struct StatSlotLoadout
    {
        /// <summary>이 칸이 어느 슬롯인가. 주스탯은 여기서 파생된다(§1-1).</summary>
        public readonly EquipmentSlot Slot;

        /// <summary>지금 뭔가 걸치고 있는가. 미착용이면 이 칸의 기여가 통째로 0이다.</summary>
        public readonly bool Worn;

        /// <summary>걸친 것의 등급. 주스탯/부스탯 상승폭이 여기서 나온다(§1-3).</summary>
        public readonly ItemRarity Rarity;

        /// <summary>부스탯 방향. <see cref="EquipmentStatRules.NoStat"/>이면 부스탯이 없다.
        /// ★ <b>슬롯이 아니라 아이템이 지정한다</b>(§1-4) — 우리 옛 「순환」 규칙과 다른 지점이다.</summary>
        public readonly int SubStat;

        /// <summary>세트 판정 키. <b>문자열 동등성으로만</b> 본다(DS-G3) — 색·재질·조형을 판정
        /// 입력으로 쓰면 <c>design-art</c>가 팔레트를 한 칸 옮기는 날 세트가 소리 없이 깨진다.</summary>
        public readonly string Theme;

        public StatSlotLoadout(EquipmentSlot slot, bool worn, ItemRarity rarity, int subStat, string theme)
        {
            Slot = slot;
            Worn = worn;
            Rarity = rarity;
            SubStat = subStat;
            Theme = theme;
        }

        /// <summary>미착용 칸.</summary>
        public static StatSlotLoadout Empty(EquipmentSlot slot)
            => new StatSlotLoadout(slot, false, ItemRarity.Common, EquipmentStatRules.NoStat, null);
    }

    /// <summary>
    /// 한 로드아웃의 계산 결과. <b>화면이 물어보는 모든 것</b>이 여기 있다 — 표시 숫자, 장비 합,
    /// 세트 여부, 단계, 다음 임계까지의 거리, 막대 비율.
    /// <para>이 값들을 화면이 <b>다시 계산하지 않게</b> 하는 것이 이 구조체의 목적이다. 같은 사실이
    /// 두 곳에서 계산되면 그게 다음 버그다(월드와 초상화가 FX 0번을 다르게 처리한 실제 사고).</para>
    /// </summary>
    public readonly struct StatBuild
    {
        /// <summary>기본값(<see cref="EquipmentStatRules.BaseValue"/>)만 뺀 <b>장비 기여 합</b>.
        /// 인계본 표시 <c>'16 (+8)'</c>의 괄호 안이 이 값이다(세트 보너스 제외).</summary>
        public readonly StatVector4 EquipmentBonus;

        /// <summary>세트 완성 보너스(완성이 아니면 4칸 전부 0).</summary>
        public readonly StatVector4 SetBonus;

        /// <summary>클램프 <b>전</b> 총합. ★ 이 값이 존재하는 이유는 I-1의 양성 대조다 —
        /// 원시 최대가 캡을 실제로 넘는지 재지 않으면 "클램프가 일한다"는 통과가 공허해진다.</summary>
        public readonly StatVector4 Raw;

        /// <summary>화면에 뜨는 값. <c>[0, CAP]</c>로 잘린 뒤다.</summary>
        public readonly StatVector4 Total;

        /// <summary>4부위가 같은 테마로 채워졌는가(§1-7 · DS-G3).</summary>
        public readonly bool SetComplete;

        /// <summary>완성된 테마 키. 미완성이면 <c>null</c>이다.</summary>
        public readonly string SetTheme;

        internal StatBuild(in StatVector4 equipmentBonus, in StatVector4 setBonus,
            in StatVector4 raw, in StatVector4 total, bool setComplete, string setTheme)
        {
            EquipmentBonus = equipmentBonus;
            SetBonus = setBonus;
            Raw = raw;
            Total = total;
            SetComplete = setComplete;
            SetTheme = setTheme;
        }

        /// <summary>이 스탯이 도달한 단계(0 = 임계 미달). ★ <b>0-기준이 아니라 1-기준</b>인 이유는
        /// <c>CurrencyModel.StatTierReached</c>·<c>CurrencyRules.ClampStatTier</c>와 <b>같은 눈금</b>을
        /// 쓰기 위해서다(그 값은 저장 파일에 그대로 적힌다). 두 벌을 만들면 영구 해금이 한 칸 밀린다.</summary>
        public int TierReached(CharacterStat stat) => EquipmentStatRules.TierOf(Total.Of(stat));

        /// <summary>막대 비율 = <c>min(1, 총합 / CAP)</c>(§1-4).</summary>
        public float Progress01(CharacterStat stat) => EquipmentStatRules.Progress01(Total.Of(stat));
    }

    /// <summary>
    /// ★ 스탯 · 임계 · 세트의 <b>순수 규칙</b> — 상태를 하나도 들고 있지 않다.
    /// <see cref="CurrencyRules"/>가 재화에 대해 하는 일을 스탯에 대해 한다.
    ///
    /// ============================================================================
    /// 정본과 출처 (숫자를 여기서 발명하지 않았다)
    /// ============================================================================
    /// <c>docs/DESIGN_SYSTEMS_STATS.md</c> — §1-1(슬롯↔주스탯) · §1-2(BASE·TIERS·CAP) ·
    /// §1-3(등급별 주/부스탯) · §1-4(계산 규칙) · §1-5(임계 효과 문구) · §1-7(세트) ·
    /// §13-6(불변식 I-1~I-4) · §14-3(부스탯 방향 24개).
    /// <c>docs/DESIGN_SYSTEMS_GRADE_SIGNAL.md</c> §3-1(DS-G3 세트는 문자열 동등성) · §3-3(세트 +2).
    ///
    /// ============================================================================
    /// ★ U-24 = <b>M3 확정</b>(리더, 2026-09-05). M4를 되살리지 마라
    /// ============================================================================
    /// R9(§15-4)가 제안한 「전설 층」 패키지 M4 — <b>전설만 부스탯 2개 · 전설 풀세트 +6 ·
    /// 임계 4단계 · CAP 44</b> — 는 <b>이번 판정에서 채택되지 않았다</b>(다음 라운드로 미룸.
    /// 여러 라운드가 이미 M3 구조 위에서 돌고 있어 되돌리는 비용이 더 크다는 판단).
    /// 이 파일은 M3(<b>3단계 · CAP 40 · 부스탯 1개 · 테마 세트 +2</b>)만 구현한다.
    ///
    /// <para>★ M4가 나중에 채택되면 바뀌는 것은 <b>임계 배열 한 줄 · CAP 한 줄 · 전설 세트 항</b>이고
    /// <b>부스탯 24칸은 한 칸도 안 바뀐다</b> — §21-2-e가 그 「결정 독립성」을 실측으로 보였다.
    /// 그리고 §21-3 F4가 <b>M4는 U-25(B)(전설의 두 번째 부스탯을 유저가 지정)를 「선택지」가 아니라
    /// 「전제 조건」으로 요구한다</b>고 못박았다 — 그것 없이 4단계를 켜면 관찰력·매력의 4번째 눈금이
    /// <b>영원히 도달 불가</b>가 되어 죽은 콘텐츠가 된다. M4를 켜는 라운드는 그 둘을 함께 해야 한다.</para>
    ///
    /// ============================================================================
    /// ★ 데이터 미완성 상태 — 숨기지 않고 적는다
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>부스탯 방향 24개</b>(<c>ItemCatalog</c>의 표)는 <b>확정됐다</b> —
    ///    §21-2-d(R15)가 정본이고 §14-3(R8)은 <b>폐기</b>다(제약 C3 위반).
    ///    R9 §15-7의 「잠정 강등」도 §21-2-e가 근거를 반증해 해제됐다(U-7 닫힘).
    ///    남은 것은 <b>이 표를 코드에서 <c>AccessoryDefSO</c>로 옮길지</b>이고,
    ///    그 판정은 <c>game-architect</c> 소관이다(§21-10-a).</item>
    ///  <item><b>테마 42개 배정은 끝났다</b> — design-equipment R21 「안 B」, 리더 채택(2026-09-05).
    ///    스탯 4슬롯 24종이 실재 테마 6개를 갖고 외형 18종은 <b>무소속</b>이다.
    ///    그래서 오늘 <see cref="IsSetComplete"/>는 <b>실제로 true가 될 수 있다</b> — 6테마 전부
    ///    4/4 완성이 가능하고, 그중 <c>mil</c>은 1일차 무료 4종만으로 완성된다(E2).
    ///    무소속(빈 문자열)을 "같다"로 세면 아무것도 안 입은 사람에게 세트 완성이 뜬다 —
    ///    그래서 무소속은 여전히 판정에서 제외한다.</item>
    ///  <item><b>임계 효과 12칸은 전부 「문구만」이다</b> — 오늘 실제로 발동하는 칸은 0개다.
    ///    <see cref="IsTierEffectActive"/>가 그 사실을 들고 있고, §6 실측(노브 실재 3 / 관측 안 됨 2 /
    ///    인프라만 2 / 신규 5)은 그 문서에 지도로 적어 뒀다. 화면이 없는 효과를 약속하면
    ///    그건 원칙 1 위반이다(유예 자동 해금이 폐기된 것과 같은 형태의 결함).</item>
    /// </list>
    /// </summary>
    public static class EquipmentStatRules
    {
        /// <summary>부스탯이 없다(외형 슬롯 · 미배정 · 팩 아이템).</summary>
        public const int NoStat = -1;

        /// <summary>
        /// 스탯 개수. ★ 숫자를 따로 적지 않고 <see cref="CurrencyRules.StatTierSlotCount"/>에서
        /// <b>유도</b>한다 — 그 상수가 곧 저장 필드 <c>statTierReached</c>의 길이이고,
        /// 둘이 갈라지는 순간 영구 해금 눈금이 스탯 하나만큼 잘린다.
        /// </summary>
        public const int StatCount = CurrencyRules.StatTierSlotCount;

        // ====================================================================
        // 기본값 · 임계 · 캡 — §1-2 (인계본 line 750~752 원문)
        // ====================================================================

        /// <summary>스탯 상한. §1-4의 막대도 이 값을 분모로 쓴다.</summary>
        public const int Cap = 40;

        /// <summary>
        /// 임계 경계 3개. ★ <b>4스탯 공통이다</b> — 리더 가설("초급 구간 폭이 스탯마다 다른가")을
        /// §0-1이 검산해 <c>{현재값 + 잔여} = {20}</c> 한 개로 닫았다. 스탯마다 남은 거리가 다른 것은
        /// <see cref="BaseValue"/>가 스탯별로 다른 상수(8/6/5/7)이기 때문이다.
        /// </summary>
        private static readonly int[] _tierThresholds = { 10, 20, 32 };

        /// <summary>임계 단계 수. ★ <b>배열이 소스</b>다 — 여기에 <c>CurrencyRules.MaxStatTier</c>를
        /// 그대로 적으면 둘이 갈라져도 아무도 모른다. 두 값이 같은지는 테스트가 매 실행 확인한다.</summary>
        public static int TierCount => _tierThresholds.Length;

        /// <summary>단계의 임계값. <paramref name="tier"/>는 <b>1-기준</b>이다(0 = 임계 미달).</summary>
        public static int TierThreshold(int tier)
            => tier >= 1 && tier <= _tierThresholds.Length ? _tierThresholds[tier - 1] : 0;

        /// <summary>단계 이름. 0은 단계가 아니라 <b>미달</b>이라 낱말이 따로 있다(§1-4).</summary>
        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 1: return "초급";
                case 2: return "중급";
                case 3: return "고급";
                default: return "임계 미달";
            }
        }

        /// <summary>
        /// 스탯 기본값 <c>BASE</c>(§1-2). 인계본이 스탯마다 다른 상수를 쓴다 —
        /// 그것이 §0-1이 밝힌 "남은 거리가 스탯마다 다른" 유일한 원인이다.
        /// <para>【미】U-4(이 값이 상수인가 레벨 파생인가)는 아직 열려 있다(§13-7). 상수로 구현한다 —
        /// 그것이 §13-5·§14-5 곡선이 실제로 쓴 가정이고, 바뀌면 이 함수 하나만 바뀐다.</para>
        /// </summary>
        public static int BaseValue(CharacterStat stat)
        {
            switch (stat)
            {
                case CharacterStat.Focus: return 8;
                case CharacterStat.Observation: return 6;
                case CharacterStat.Charm: return 5;
                case CharacterStat.Agility: return 7;
                default: return 0;
            }
        }

        // ====================================================================
        // 등급별 상승폭 — §1-3 (인계본 line 705~706)
        // ====================================================================

        /// <summary>그 슬롯의 <b>주스탯</b>에 더해지는 값(<c>MAINV</c>).</summary>
        public static int MainStatBonus(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 3;
                case ItemRarity.Rare: return 6;
                case ItemRarity.Epic: return 10;
                case ItemRarity.Legendary: return 15;
                default: return 0;
            }
        }

        /// <summary>그 아이템의 <b>부스탯</b>에 더해지는 값(<c>SUBV</c>).</summary>
        public static int SubStatBonus(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 1;
                case ItemRarity.Rare: return 2;
                case ItemRarity.Epic: return 4;
                case ItemRarity.Legendary: return 6;
                default: return 0;
            }
        }

        // ====================================================================
        // 슬롯 ↔ 주스탯 — §1-1 (인계본 line 700, 749)
        // ====================================================================

        /// <summary>
        /// 이 슬롯의 주스탯. 외형 3슬롯(머리/이펙트/펫)은 <see cref="NoStat"/>다 —
        /// ★ 이건 플레이스홀더가 아니라 <b>구조적 사실</b>이다(§14-1: "외형 18종은 스탯 기여 0").
        /// 스탯 슬롯은 정확히 4개이고 그 수가 곧 <see cref="StatCount"/>다.
        /// </summary>
        public static int MainStatOf(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return (int)CharacterStat.Focus;
                case EquipmentSlot.Eyes: return (int)CharacterStat.Observation;
                case EquipmentSlot.Neck: return (int)CharacterStat.Charm;
                case EquipmentSlot.Shoulders: return (int)CharacterStat.Agility;
                default: return NoStat;
            }
        }

        /// <summary>이 슬롯이 스탯에 기여하는가(= 주스탯이 있는가).</summary>
        public static bool IsStatSlot(EquipmentSlot slot) => MainStatOf(slot) != NoStat;

        /// <summary>스탯 슬롯 4개를 표시 순서대로. ★ <b>배열을 만들지 않는다</b> —
        /// 이 함수를 부르는 자리가 카드 갱신 루프 안이다.</summary>
        public static EquipmentSlot StatSlotAt(int index)
        {
            switch (index)
            {
                case 0: return EquipmentSlot.Head;
                case 1: return EquipmentSlot.Eyes;
                case 2: return EquipmentSlot.Neck;
                default: return EquipmentSlot.Shoulders;
            }
        }

        // ====================================================================
        // 세트 — §1-7 · DS-G3 (docs/DESIGN_SYSTEMS_GRADE_SIGNAL.md §3-1·§3-3)
        // ====================================================================

        /// <summary>세트 완성 시 <b>스탯마다</b> 더해지는 값. 총합으로는 <c>+8</c>이다
        /// (§C-5가 "4스탯 +2 vs 총합 +8"이 충돌이 아니라 같은 말임을 닫았다).</summary>
        public const int SetCompletionBonusPerStat = 2;

        /// <summary>
        /// 4부위가 같은 테마인가. ★ <b>문자열 동등성만</b> 본다(DS-G3 · DS-4′-c).
        /// <para>빈 값(<c>null</c>/<c>""</c> = <b>무소속</b>)은 <b>같다고 세지 않는다</b>.
        /// 외형 18종과 팩 아이템이 오늘 그 값이고, 그것을 같다고 세면 <b>아무 테마도 없는 4종에
        /// 세트 완성이 뜬다</b> — 화면이 존재하지 않는 것을 약속하는 바로 그 형태다.</para>
        /// </summary>
        public static bool IsSetComplete(string head, string eyes, string neck, string back, out string theme)
        {
            theme = null;
            if (string.IsNullOrEmpty(head)) return false;
            if (!string.Equals(head, eyes, System.StringComparison.Ordinal)) return false;
            if (!string.Equals(head, neck, System.StringComparison.Ordinal)) return false;
            if (!string.Equals(head, back, System.StringComparison.Ordinal)) return false;
            theme = head;
            return true;
        }

        // ====================================================================
        // 계산 — §1-4 (인계본 line 878~910 원문 그대로)
        // ====================================================================

        /// <summary>
        /// 총합(s) = BASE[s] + Σ(주스탯 기여) + Σ(부스탯 기여) + 세트 보너스, 그리고 <c>[0, CAP]</c> 클램프.
        ///
        /// <para>★ 세트 보너스를 <b>클램프 전에</b> 더한다(§15-4-(d) "합은 캡이 흡수"). 클램프 뒤에
        /// 더하면 캡을 넘겨 화면에 41이 뜬다 — I-1이 잡는 바로 그 사고다.</para>
        ///
        /// <para>인자를 카탈로그가 아니라 <b>값</b>으로 받는 이유는 <see cref="StatSlotLoadout"/> 문서에 있다.</para>
        /// </summary>
        public static StatBuild Evaluate(in StatSlotLoadout head, in StatSlotLoadout eyes,
            in StatSlotLoadout neck, in StatSlotLoadout back)
        {
            StatVector4 bonus = Contribution(head)
                .Plus(Contribution(eyes))
                .Plus(Contribution(neck))
                .Plus(Contribution(back));

            string theme = null;
            bool complete = head.Worn && eyes.Worn && neck.Worn && back.Worn
                && IsSetComplete(head.Theme, eyes.Theme, neck.Theme, back.Theme, out theme);
            if (!complete) theme = null;

            int set = complete ? SetCompletionBonusPerStat : 0;
            var setVector = new StatVector4(set, set, set, set);

            var raw = new StatVector4(
                BaseValue(CharacterStat.Focus) + bonus.Focus + set,
                BaseValue(CharacterStat.Observation) + bonus.Observation + set,
                BaseValue(CharacterStat.Charm) + bonus.Charm + set,
                BaseValue(CharacterStat.Agility) + bonus.Agility + set);

            var total = new StatVector4(
                ClampStat(raw.Focus), ClampStat(raw.Observation),
                ClampStat(raw.Charm), ClampStat(raw.Agility));

            return new StatBuild(bonus, setVector, raw, total, complete, theme);
        }

        /// <summary>슬롯 한 칸이 4스탯에 넣는 값(주스탯 + 부스탯). 미착용이면 전부 0이다.</summary>
        public static StatVector4 Contribution(in StatSlotLoadout slot)
        {
            if (!slot.Worn) return default;

            int main = MainStatOf(slot.Slot);
            int mainValue = main == NoStat ? 0 : MainStatBonus(slot.Rarity);
            int subValue = slot.SubStat == NoStat ? 0 : SubStatBonus(slot.Rarity);

            int f = 0, o = 0, c = 0, a = 0;
            Add(ref f, ref o, ref c, ref a, main, mainValue);
            Add(ref f, ref o, ref c, ref a, slot.SubStat, subValue);
            return new StatVector4(f, o, c, a);
        }

        private static void Add(ref int f, ref int o, ref int c, ref int a, int statIndex, int value)
        {
            switch (statIndex)
            {
                case (int)CharacterStat.Focus: f += value; break;
                case (int)CharacterStat.Observation: o += value; break;
                case (int)CharacterStat.Charm: c += value; break;
                case (int)CharacterStat.Agility: a += value; break;
            }
        }

        /// <summary>★ <b>I-1의 유일한 방어선</b>(§13-6). 자유 부스탯에서 원시 최대가 CAP을 넘는다 —
        /// 4슬롯 전부 전설이 한 스탯을 가리키면 <c>8 + 15 + 6×3 = 41</c>이다(§14-4 양성 대조).
        /// 하한 0은 음수 기여가 생기는 날을 위한 것이지 지금 도달 가능한 값이 아니다.</summary>
        public static int ClampStat(int raw) => raw < 0 ? 0 : raw > Cap ? Cap : raw;

        /// <summary>도달 단계(0 = 임계 미달, 1~<see cref="TierCount"/>). §1-4의 <c>단계</c>를
        /// <b>1-기준</b>으로 옮긴 것이다 — 이유는 <see cref="StatBuild.TierReached"/> 문서.</summary>
        public static int TierOf(int total)
        {
            int tier = 0;
            for (int i = 0; i < _tierThresholds.Length; i++)
            {
                if (total >= _tierThresholds[i]) tier = i + 1;
            }
            return tier;
        }

        /// <summary>다음 임계까지 남은 값. 최고 단계면 <c>false</c>다(§1-4 "최고 단계").</summary>
        public static bool TryNextThreshold(int total, out int remaining)
        {
            int tier = TierOf(total);
            if (tier >= _tierThresholds.Length)
            {
                remaining = 0;
                return false;
            }
            remaining = _tierThresholds[tier] - total;
            return true;
        }

        /// <summary>막대 비율 = <c>min(1, 총합 / CAP)</c>. 눈금 3개는
        /// <see cref="TierMark01"/>(10/40 = 25% · 20/40 = 50% · 32/40 = 80%).</summary>
        public static float Progress01(int total)
        {
            if (total <= 0) return 0f;
            if (total >= Cap) return 1f;
            return total / (float)Cap;
        }

        /// <summary>막대 위 눈금 하나의 위치(0~1). <paramref name="tier"/>는 1-기준이다.</summary>
        public static float TierMark01(int tier) => TierThreshold(tier) / (float)Cap;

        // ====================================================================
        // 임계 효과 문구 — §1-5 (인계본 line 753~758)
        // ====================================================================

        /// <summary>
        /// 이 단계에서 열리는 효과의 문구. 단계가 0이면 빈 문자열이다.
        /// <para>★ <b>문구가 있다고 효과가 있는 것이 아니다</b> — <see cref="IsTierEffectImplemented"/>를
        /// 반드시 함께 보라. §6 실측으로 12칸 중 <b>3칸만</b> 실제 노브에 닿아 있다.</para>
        /// </summary>
        public static string TierEffectText(CharacterStat stat, int tier)
        {
            switch (stat)
            {
                case CharacterStat.Focus:
                    switch (tier)
                    {
                        case 1: return "성과물 배율 +10%";
                        case 2: return "명상 유휴 연출 해금";
                        case 3: return "성과물 배율 +25% · 졸음 연출";
                        default: return string.Empty;
                    }
                case CharacterStat.Observation:
                    switch (tier)
                    {
                        case 1: return "활쏘기 쿨다운 −10%";
                        case 2: return "궤적 잔상 이펙트";
                        case 3: return "쿨다운 −25% · 잔상 강화";
                        default: return string.Empty;
                    }
                case CharacterStat.Charm:
                    switch (tier)
                    {
                        case 1: return "오라 이펙트 발현";
                        case 2: return "오라 범위 확대";
                        case 3: return "오라 강도 최대 · 팩 색 연동";
                        default: return string.Empty;
                    }
                case CharacterStat.Agility:
                    switch (tier)
                    {
                        case 1: return "이동속도 +8%";
                        case 2: return "던지기 최소 회전 보장";
                        case 3: return "이동속도 +18% · 트레일 이펙트";
                        default: return string.Empty;
                    }
                default: return string.Empty;
            }
        }

        /// <summary>
        /// ★ 그 문구가 <b>지금 사실인가</b> — 임계를 넘겼을 때 실제로 무슨 일이 일어나는가.
        ///
        /// <para><b>오늘은 12칸 전부 <c>false</c>다.</b> 스탯 값 자체가 이 파일에서 처음 생겼고,
        /// 스탯 → 효과 배선은 이 라운드의 범위가 아니다(리더 별도 배정). 정직하게 적는다:
        /// 「문구가 있다」와 「효과가 있다」는 다른 사실이고, 둘을 같은 칸에 적으면
        /// <b>화면이 존재하지 않는 경로를 약속</b>하게 된다(유예 자동 해금이 폐기됐는데 화면이
        /// 계속 약속하던 그 결함, 원칙 1).</para>
        ///
        /// <para><b>다음 라운드의 지도</b>(§6 실측, 12칸 분류): <b>노브가 이미 있는 3칸</b> =
        /// 민첩 1·3단(<c>StickConfig.walkSpeed</c> / <c>ResolveWalkSpeed()</c>) ·
        /// 민첩 2단(<c>throwTumbleMinSpinDegreesPerSecond</c>). <b>노브는 있으나 기본 설정에서 효과가
        /// 관측되지 않는 2칸</b> = 관찰력 1·3단(<c>archeryCooldownSeconds</c>, §C-12).
        /// <b>인프라만 있는 2칸</b> = 집중력 2·3단(명상/졸음 포즈, <c>ApplyIdleAmbientPose</c>).
        /// <b>기능 전체가 신규인 5칸</b> = 집중력 1·3단의 성과물 배율 · 관찰력 2단 궤적 잔상 ·
        /// <b>매력 3단 전부</b>(<c>Aura</c> 0건 — 매력을 올려도 아무 일도 안 일어난다).</para>
        ///
        /// <para>누가 효과 하나를 배선하면 이 함수와 <see cref="ActiveTierEffectCount"/>를 같이
        /// 고쳐야 한다 — <c>Tests/EditMode/EquipmentStatInvariantTests</c>가 둘이 어긋나면 빨개진다.</para>
        /// </summary>
        public static bool IsTierEffectActive(CharacterStat stat, int tier) => false;

        /// <summary>지금 실제로 발동하는 임계 효과 칸 수. <see cref="IsTierEffectActive"/>가
        /// <c>true</c>를 내는 칸 수와 <b>같아야 한다</b> — 테스트가 그 등호를 잰다(한쪽만 고치면 빨개진다).</summary>
        public const int ActiveTierEffectCount = 0;

        // ====================================================================
        // 지금 착용 상태 — 이음매를 하나로 (EquipmentModel + ItemCatalog)
        // ====================================================================

        /// <summary>
        /// 지금 이 슬롯에 걸친 것을 <see cref="StatSlotLoadout"/>으로. 착용 여부는
        /// <see cref="EquipmentModel"/>, 등급·부스탯·테마는 <see cref="ItemCatalog"/>가 유일한 출처다 —
        /// <b>화면이 이 셋을 각자 뒤지지 않게</b> 하는 것이 이 함수의 목적이다.
        ///
        /// <para>★★ <b>2026-09-06 — 「걸쳤다」와 「그려진다」는 다른 사실이다</b>(같은 창의 슬롯 줄에서
        /// 먼저 고쳐진 결함이 여기 한 칸 남아 있었다). 이 함수는 <c>WornIndex &gt;= 0</c>만 보고
        /// 등급·부스탯·테마를 그대로 실어 보냈고, 그 값이 <see cref="CurrentBuild"/>를 거쳐
        /// <b>컬럼 2의 스탯 카드 · 「장비 합」 · H-8 등급 눈금</b>에 들어갔다.</para>
        ///
        /// <para>실측 재현(프로덕션 코드 직접 실행): 세이브에 <c>고글</c>(Lv11)·<c>요정 날개</c>(Lv28)가
        /// 들어 있는 <b>Lv3</b> 캐릭터 — <see cref="EquipmentModel.IsItemOwned"/>가 둘 다 <c>false</c>이고
        /// 슬롯 줄도 액자도 「비어 있음」인데 <b>스탯 합계만</b> 집중력 8→16 · 관찰력 6→12 · 민첩 7→22로
        /// 그 둘을 계속 세고 있었다. 더 나쁜 것은 그 숫자가 <b>Lv28로 올린 뒤와 완전히 같았다</b>는 점이다
        /// — 즉 이 칸에서는 요구 레벨이 <b>아무 일도 하지 않았다</b>.</para>
        ///
        /// <para><b>새 술어를 짓지 않았다.</b> 아래 한 줄은 이미 세 표면이 쓰고 있는 것을 그대로 옮긴
        /// 것이다 — <c>CharacterPortraitStage.EquippedAndUnlocked</c>(액자) ·
        /// <c>CharacterAccessoryRenderer.EquippedAndUnlocked</c>(몸) ·
        /// <c>CharacterInfoWindow.Cards.SyncSlotRows</c>(슬롯 줄). 잠금 규칙이 바뀌는 날 네 표면이
        /// 동시에 따라온다. 앞 항(<see cref="EquipmentModel.IsEquipped(EquipmentSlot)"/>)을 빼면
        /// 미착용일 때 <see cref="EquipmentModel.IsUnlocked"/>가 "고를 것이 하나라도 있는가"로 뜻이
        /// 바뀌어 <b>빈 슬롯이 착용으로 읽힌다</b>.</para>
        ///
        /// <para>★ <b>세트 판정도 이 한 줄로 같이 닫힌다.</b> <see cref="Evaluate"/>는 세트 완성을
        /// 각 칸의 <see cref="StatSlotLoadout.Worn"/>·<see cref="StatSlotLoadout.Theme"/>로만 판정하므로
        /// (<see cref="IsSetComplete"/>), 잠긴 칸이 여기서 <see cref="StatSlotLoadout.Empty"/>로
        /// 떨어지는 순간 <c>Theme</c>이 <c>null</c>이 되어 세트 보너스 +2도 함께 사라진다.
        /// 「입을 수도 없는 4종으로 세트 완성이 뜬다」가 이 고침에 포함된다 —
        /// <b>잠금 검사를 여기 말고 세트 쪽에 또 적으면 그때부터 이음매가 둘이 된다.</b></para>
        ///
        /// <para><b>세이브 파일은 한 글자도 안 건드린다.</b> <see cref="EquipmentModel.RestoreFromSave(EquipmentSlot,string)"/>가
        /// 잠금을 검사하지 않는 것은 의도된 설계이고(검사하면 레벨이 낮게 복원되는 순간 착용물이 조용히
        /// 사라진다), 그 문서가 «대신 렌더러/UI가 그릴 때 <see cref="EquipmentModel.IsUnlocked"/>로 함께
        /// 본다»고 약속한 그 자리가 여기다. 레벨이 요구치에 닿으면 스탯도 저절로 되살아난다.</para>
        /// </summary>
        public static StatSlotLoadout SlotLoadout(EquipmentSlot slot)
        {
            // ★ 액자·몸·슬롯 줄과 <b>같은 술어</b>다. 여기서 새로 짜지 않고 EquipmentModel의 공개 사실
            //   둘을 그대로 곱한다(위 문단 참조).
            bool drawnOnStage = EquipmentModel.IsEquipped(slot) && EquipmentModel.IsUnlocked(slot);
            if (!drawnOnStage) return StatSlotLoadout.Empty(slot);

            int worn = EquipmentModel.WornIndex(slot);
            ItemCatalogEntry entry = ItemCatalog.Item(slot, worn);
            if (entry == null) return StatSlotLoadout.Empty(slot);

            return new StatSlotLoadout(slot, true, ItemCatalog.Rarity(slot, worn), entry.SubStat, entry.Theme);
        }

        /// <summary>
        /// ★ 도달 단계를 <b>영구 해금</b>(H-8 눈금)에 기록한다. 이미 있는 저장 필드
        /// <c>statTierReached</c>(v10)를 쓰는 <b>유일한 입구</b>다 — 새 필드를 만들지 않는다.
        ///
        /// <para><b>이 함수가 존재하는 이유는 눈금 기준이 하나뿐이어야 하기 때문이다.</b>
        /// <see cref="StatBuild.TierReached"/>는 1-기준이고 <see cref="CurrencyModel.RaiseStatTier"/>의
        /// <c>slotIndex</c>는 <see cref="CharacterStat"/> 값 그대로다. 이 두 매핑을 호출부가 각자
        /// 적으면 언젠가 한 곳이 0-기준으로 적히고, 그 증상은 <b>「고급을 찍었는데 눈금이 중급까지만
        /// 남는다」</b>로 나타난다 — 화면은 멀쩡하고 저장 파일을 열어봐도 안 보인다.</para>
        ///
        /// <para>★ <b>부르는 쪽은 이 라운드에서 배선하지 않았다.</b> 착용 변경 이벤트
        /// (<c>StickmanEventBus.CharacterEquipmentChanged</c>)를 구독하는 자리는 <c>Interaction/</c>이고
        /// 그쪽은 이 라운드의 소유가 아니다. 배선하는 라운드는 <b>이 함수 하나만</b> 부르면 된다.</para>
        /// </summary>
        /// <returns>한 칸이라도 올라갔으면 <c>true</c>(그때만 저장할 값이 생긴다).</returns>
        public static bool RecordTierHighWaterMarks(in StatBuild build)
        {
            bool changed = false;
            for (int i = 0; i < StatCount; i++)
            {
                // ★ 단락 평가(||)를 쓰지 않는다 — 첫 칸이 오르면 나머지 세 칸을 건너뛴다.
                if (CurrencyModel.RaiseStatTier(i, build.TierReached((CharacterStat)i))) changed = true;
            }
            return changed;
        }

        /// <summary>지금 차림의 계산 결과. 정보창 스탯 카드가 부르는 유일한 입구다.</summary>
        public static StatBuild CurrentBuild()
            => Evaluate(
                SlotLoadout(EquipmentSlot.Head),
                SlotLoadout(EquipmentSlot.Eyes),
                SlotLoadout(EquipmentSlot.Neck),
                SlotLoadout(EquipmentSlot.Shoulders));

        /// <summary>스탯의 표시 이름. <b>낱말의 단일 출처</b>다 — 카드/상세/상점이 각자 들고 있으면
        /// 로컬라이제이션이 오는 날 번역이 갈라진다(<c>ItemCatalog.RarityName</c>과 같은 관례).</summary>
        public static string StatName(CharacterStat stat)
        {
            switch (stat)
            {
                case CharacterStat.Focus: return "집중력";
                case CharacterStat.Observation: return "관찰력";
                case CharacterStat.Charm: return "매력";
                case CharacterStat.Agility: return "민첩";
                default: return "?";
            }
        }
    }
}
