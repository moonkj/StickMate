using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 컬럼 2 「능력치 / STATUS」와 「테마 세트 / SET」의 <b>표시 문자열 계산기</b>.
    /// 순수 함수뿐이고 <see cref="UnityEngine"/> 오브젝트를 만들지 않으므로 EditMode에서 그대로 잠근다.
    ///
    /// ============================================================================
    /// 이 파일에 <b>없는</b> 것 — 그리고 없는 이유
    /// ============================================================================
    /// 임계값(10/20/32) · 캡(40) · <c>BASE</c>(8/6/5/7) · 등급별 상승폭 · 단계 낱말 · 스탯 이름 ·
    /// 세트 보너스는 <b>한 글자도 여기 없다</b>. 전부 <see cref="EquipmentStatRules"/>(Core)가 유일한
    /// 출처이고 이 파일은 그 결과를 <b>이어 붙이기만</b> 한다.
    ///
    /// <para>같은 숫자를 표시 쪽에도 적어 두면 그 값이 바뀌는 날 한쪽만 따라오고, 그때부터 화면과
    /// 규칙이 서로 다른 말을 한다 — 이 저장소가 「설계 거울이 프로덕션과 13건 갈라진 채 서로 다른
    /// 판정」으로 이미 당한 형태다.</para>
    ///
    /// ============================================================================
    /// 교정 대상 — <c>docs/DESIGN_SYSTEMS_STATS.md</c> §0의 14/14 PASS 표
    /// ============================================================================
    /// 그 표가 <b>바깥에서 온 기대값</b>이다(설계가 인계본 렌더에서 직접 뽑아 독립 재현한 값):
    /// <c>16 (+8)</c> · <c>중급까지 4</c> · <c>+24</c> · <c>오피스 워커 3/4</c> · 매력 10의 등급명 <c>초급</c>.
    /// <b>기대값을 이 파일의 함수로 만들지 않는다</b> — 그러면 함수가 틀어질 때 기대값도 같이 틀어져
    /// 아무것도 못 잰다(TEAM.md 「생성기와 검사기가 같이 틀린다」).
    ///
    /// ============================================================================
    /// ★ 폭은 여기서 세지 않는다
    /// ============================================================================
    /// 글자 폭은 <c>UiChrome.Ellipsize</c> / <c>SettingsControls.MeasuredWidth</c>가
    /// <c>preferredWidth</c>로 <b>실제로 잰다</b>. 「한글 한 글자 = N pt」류 곱셈 상수를 두지 않는다 —
    /// <c>localization</c> 라운드가 그 형태를 폐기했고(라틴 자폭이 절반 이하라 영어에서 반대로 틀린다),
    /// §7-1의 곱셈 모형은 <b>설계가 오프라인에서 쓰는 자</b>이지 런타임의 자가 아니다.
    /// </summary>
    public static class CharacterStatReadout
    {
        /// <summary>§1-4 원문 — 마지막 임계를 넘겼을 때의 「다음까지」 표기.</summary>
        public const string TopTier = "최고 단계";

        /// <summary>§1-4 원문 — 세트가 4/4가 아닐 때.
        /// <para>★ 2026-09-06 정정 — <b>이제 실제로 화면에 뜬다.</b> 앞 서술("오늘은 도달하지 않는다,
        /// 기본 42종의 테마가 전부 미배정이라 부분 일치가 안 생긴다")은 R21 테마 배정(기본 24종)으로
        /// <b>거짓이 됐다</b>. 부위 넷 중 일부만 같은 테마면 이 줄이 그대로 보인다.</para>
        /// <para>그 서술이 걸어 둔 조건("테마가 배정되는 라운드에 <c>design-narrative</c>가 이 문구를
        /// 한 번 봐야 한다")이 <b>지금 충족됐다</b> — 「기본 중립 <b>대사</b>」는 「테마 전용 대사」가
        /// 있다는 뜻인데 그 대사 풀은 <b>아직 없다</b>. 문구를 지어내지 않고 설계 원문을 그대로 둔 채
        /// <b>보고에 올렸다</b>(<c>coder-ui</c> 2026-09-06).</para></summary>
        public const string SetIncomplete = "미완성 · 기본 중립 대사";

        /// <summary>값이 없을 때 쓰는 기호. 이 창이 이미 쓰고 있는 것과 같다.</summary>
        public const string NoValue = "—";

        /// <summary>
        /// 카드 오른쪽 큰 숫자. 음수는 0으로 눌러 「−3」이 뜨지 않게 한다 — 음수 총합은 위조가 아니라
        /// <b>계산 버그의 신호</b>이고 화면에 그릴 자리가 없다(<c>CurrencyRules.ClampCoinBalance</c>와
        /// 같은 판단). <see cref="EquipmentStatRules.ClampStat"/>이 이미 잘라 주지만, 표시 계층이
        /// 그것에 <b>의존</b>하면 규칙이 바뀌는 날 화면이 먼저 깨진다.
        /// </summary>
        public static string FormatTotal(int total) => Mathf.Max(0, total).ToString();

        /// <summary>총합 옆의 장비 기여. <b>0 이하면 빈 문자열</b>이다 — <c>(+0)</c>은 아무 정보도
        /// 없으면서 자리만 먹는다.</summary>
        public static string FormatBonus(int bonus) => bonus > 0 ? $"(+{bonus})" : string.Empty;

        /// <summary>§0 교정 대상 문자열 — <c>16 (+8)</c>. 카드는 이것을 두 개의
        /// <see cref="UnityEngine.UI.Text"/>로 나눠 그리지만(색과 크기가 다르다), <b>합치면 이 문자열</b>이다.</summary>
        public static string FormatDisplay(int total, int bonus)
        {
            string bonusText = FormatBonus(bonus);
            return bonusText.Length == 0 ? FormatTotal(total) : FormatTotal(total) + " " + bonusText;
        }

        /// <summary>
        /// §0 교정 대상 문자열 — <c>중급까지 4</c>. 최고 단계면 <see cref="TopTier"/>.
        /// <para>거리도 낱말도 <b>Core가 준다</b>: 남은 값은 <see cref="EquipmentStatRules.TryNextThreshold"/>,
        /// 「중급」은 <see cref="EquipmentStatRules.TierName"/>(현재 단계 + 1)이다.</para>
        /// <para>경계는 <b>이상(≥)</b>이다 — 총합이 정확히 임계값이면 그 단계에 들어가고, 문구는
        /// <b>그다음</b> 단계를 가리킨다(§0 교정의 「매력 10 → 초급 / 중급까지 10」이 그 증거다).</para>
        /// </summary>
        public static string FormatRemaining(int total)
        {
            int clamped = Mathf.Max(0, total);
            if (!EquipmentStatRules.TryNextThreshold(clamped, out int remaining)) return TopTier;
            return $"{EquipmentStatRules.TierName(EquipmentStatRules.TierOf(clamped) + 1)}까지 " +
                   $"{Mathf.Max(0, remaining)}";
        }

        /// <summary>§0 교정 대상 문자열 — 섹션 제목 오른쪽의 <c>+24</c>(장비 기여 총합).
        /// 0이어도 <c>+0</c>으로 적는다 — 「아직 아무것도 안 걸쳤다」는 <b>참인 사실</b>이고,
        /// 이 자리는 항상 같은 모양이어야 눈이 값을 찾는다.</summary>
        public static string FormatEquipmentSum(int sum) => $"+{Mathf.Max(0, sum)}";

        /// <summary>네 스탯의 장비 기여 합. §0 교정의 <c>장비 합 '+24'</c>가 이 합이다.</summary>
        public static int EquipmentSum(in StatVector4 equipmentBonus)
            => Mathf.Max(0, equipmentBonus.Focus) + Mathf.Max(0, equipmentBonus.Observation)
               + Mathf.Max(0, equipmentBonus.Charm) + Mathf.Max(0, equipmentBonus.Agility);

        /// <summary>
        /// ★ 테마 <b>키</b>를 화면에 쓸 이름으로 바꾼다 — <c>"mil"</c> → <c>"밀리터리"</c>
        /// (2026-09-06 신설. 그전까지 세트 패널이 <c>"mil 3/4"</c>처럼 <b>내부 코드명을 그대로</b>
        /// 한글 화면에 찍고 있었다).
        ///
        /// <para><b>표는 여기 없다</b> — 값의 유일한 출처는 <see cref="ItemCatalog.ThemeDisplayName"/>다.
        /// 화면이 자기 표를 들면 팩이 테마를 실어 오는 날 <b>한쪽만</b> 늘어난다. 반대로 «키를 이름으로
        /// 보여줄 것인가»라는 <b>표시 규칙</b>은 Core의 일이 아니라 이쪽 일이고, <c>ItemCatalog</c>도
        /// 자기 문서에 그렇게 적어 두었다.</para>
        ///
        /// <para>★ <b>모르는 키는 키 그대로 내보낸다</b>(<c>?? themeKey</c>). <see cref="NoValue"/>로
        /// 지우면 팩이 새 테마를 실어 오는 날 진행도 줄이 <b>소리 없이</b> 사라진다 — 이 저장소가
        /// 「<c>default:</c>로 조용히 흘려보내기」로 반복해 당한 형태다. 못생긴 영문 키가 잠깐 보이는
        /// 편이 낫고, 그 못생김 자체가 «Core 표를 안 늘렸다»는 신호로 눈에 띈다.</para>
        ///
        /// <para><c>null</c>/빈 문자열은 <b>그대로 돌려준다</b> — 그 값은
        /// <see cref="FormatThemeProgress"/>가 이미 <see cref="NoValue"/>로 받아 낸다. 여기서 한 번 더
        /// 판단하면 «없음»을 두 곳이 정의하게 된다.</para>
        /// </summary>
        public static string ThemeLabel(string themeKey)
            => ItemCatalog.ThemeDisplayName(themeKey) ?? themeKey;

        /// <summary>§0 교정 대상 문자열 — <c>오피스 워커 3/4</c>.
        /// 이름이 비었거나 분모가 0이면 <see cref="NoValue"/>로 물러선다.
        /// <para>★ 인자는 <b>이미 사람이 읽을 이름</b>이어야 한다 — 키를 넘기면 화면에 <c>mil 3/4</c>가
        /// 뜬다. 변환은 <see cref="ThemeLabel"/>이 한다.</para></summary>
        public static string FormatThemeProgress(string themeName, int matched, int required)
        {
            if (string.IsNullOrEmpty(themeName) || required <= 0) return NoValue;
            return $"{themeName} {Mathf.Clamp(matched, 0, required)}/{required}";
        }

        /// <summary>
        /// 세트 완성 줄 — §1-7 「스탯 총합 보너스 · <b>4부위 합계 +8</b>」.
        /// <para>두 숫자를 다 <b>유도한다</b>: 「4」는 스탯 슬롯 수, 「8」은
        /// <see cref="EquipmentStatRules.SetCompletionBonusPerStat"/> × 스탯 수다. 인계본 문구의 8이
        /// 「스탯당 2 × 4스탯」이라는 것이 §C-5의 판정이고, 그래서 <b>둘 중 하나만 바뀌어도</b>
        /// 이 문장이 따라온다.</para>
        /// </summary>
        public static string FormatSetBonus()
            => $"{EquipmentStatRules.StatCount}부위 합계 " +
               $"+{EquipmentStatRules.SetCompletionBonusPerStat * EquipmentStatRules.StatCount}";

        /// <summary>
        /// 「한 번 넘긴 눈금만 황동」(H-8)의 판정 한 곳. <paramref name="reachedTier"/>는 저장된
        /// high-water(<c>CurrencyModel.StatTierReached</c>)이고 <paramref name="currentTier"/>는 지금 값이다.
        /// 둘 다 <b>1-기준</b>이다(0 = 임계 미달).
        ///
        /// <para><b>둘 중 큰 쪽을 본다.</b> 저장 필드를 올려 주는 코드가 아직 없어도 지금 실제로 넘긴
        /// 눈금은 켜져야 한다 — 안 그러면 화면이 <b>사실보다 적게</b> 말한다. 반대로 저장값이 더 크면
        /// (예전에 넘겼다가 장비를 벗음) 그것도 켠다 — 그것이 high-water의 뜻이다.</para>
        /// </summary>
        public static bool IsTickLit(int tier, int currentTier, int reachedTier)
            => tier >= 1 && tier <= Mathf.Max(currentTier, reachedTier);

        /// <summary>
        /// ★ 착용 4부위의 <b>테마 최빈값</b>(§1-4 「세트 = 착용 4부위의 theme 최빈값 개수」).
        ///
        /// <para><b>완성 판정을 여기서 하지 않는다</b> — 그것은 <see cref="StatBuild.SetComplete"/>
        /// 하나가 답한다(<c>coder-systems</c> 인계: *"두 곳에서 같은 걸 계산하면 언젠가 갈라진다"*).
        /// 이 함수가 내는 것은 Core가 세지 않는 <b>「3/4」라는 표시 전용 진행도</b>뿐이다 —
        /// 완성 판정에는 최빈값의 <b>개수</b>가 필요 없어서 Core가 그것을 세지 않는다.</para>
        ///
        /// <para>그래도 둘은 <b>어긋나면 안 된다</b>: <c>matched == required ⟺ SetComplete</c>.
        /// <c>CharacterStatReadoutTests</c>가 그 동치를 전수 조합으로 매 실행 대조하고,
        /// 런타임에서도 어긋나면 <c>CharacterInfoWindow.RefreshSetPanel</c>이 <c>LogError</c>를 낸다.</para>
        ///
        /// <para>빈 문자열은 세지 않는다(DS-G3 · Core와 같은 규칙) — 세면 아무것도 안 입은 사람에게
        /// 「4/4」가 뜬다. 미착용 칸은 <c>null</c>이고 외형·팩 아이템은 빈 문자열
        /// (<see cref="ItemCatalog.ThemeUnassigned"/>)이라 <b>둘 다 후보가 아니다</b>.</para>
        ///
        /// <para>★ 2026-09-06 정정 — 여기 있던 *"기본 42종의 테마는 아직 전부 미배정이라 오늘 이
        /// 함수는 항상 <c>false</c>를 돌려준다"*는 R21 테마 배정(기본 24종 = 스탯 4슬롯 × 6테마)으로
        /// <b>거짓이 됐다</b>. 이 함수는 실제로 <c>true</c>를 돌려주고 세트 패널이 진행도로 바뀐다.</para>
        ///
        /// <para>★ <b>돌려주는 <paramref name="themeName"/>은 「키」다</b>(<c>mil</c>·<c>office</c> …).
        /// 사람이 읽을 이름으로 바꾸는 것은 <b>호출부</b>의 일이고 그 표는
        /// <see cref="ItemCatalog.ThemeDisplayName"/> 하나다 — 여기서 바꿔 주면 세트 판정에 쓰는
        /// 문자열과 화면에 쓰는 문자열이 같은 변수에 섞인다.</para>
        /// </summary>
        /// <remarks>★ <b>할당이 0이다.</b> 이 함수는 0.25초마다 불리는 경로에 있어서, 후보 4개를
        /// 배열로 묶으면(<c>string[] worn = { ... }</c>) 하루 종일 켜 두는 앱에서 초당 4개씩 쓰레기가
        /// 쌓인다. 사전도 LINQ도 쓰지 않고 후보마다 네 번 비교한다(n=4라 그게 제일 싸다).</remarks>
        public static bool TryGetThemeProgress(string head, string eyes, string neck, string back,
            out string themeName, out int matched, out int required)
        {
            themeName = null;
            matched = 0;
            required = EquipmentStatRules.StatCount;

            Consider(head, head, eyes, neck, back, ref themeName, ref matched);
            Consider(eyes, head, eyes, neck, back, ref themeName, ref matched);
            Consider(neck, head, eyes, neck, back, ref themeName, ref matched);
            Consider(back, head, eyes, neck, back, ref themeName, ref matched);

            return matched > 0 && !string.IsNullOrEmpty(themeName);
        }

        /// <summary>후보 하나가 네 부위 중 몇 곳과 같은지 세고, 지금까지의 최빈보다 많으면 갈아 끼운다.
        /// <b>빈 값은 후보가 아니다</b> — Core의 <c>IsSetComplete</c>와 같은 규칙(DS-G3).
        /// 동점이면 <b>앞쪽(모자 → 안경 → 넥타이 → 망토) 후보가 이긴다</b> — 순서가 정해져 있어야
        /// 같은 차림에서 프레임마다 다른 이름이 뜨지 않는다.</summary>
        private static void Consider(string candidate, string head, string eyes, string neck, string back,
            ref string best, ref int bestCount)
        {
            if (string.IsNullOrEmpty(candidate)) return;

            int count = 0;
            if (string.Equals(candidate, head, System.StringComparison.Ordinal)) count++;
            if (string.Equals(candidate, eyes, System.StringComparison.Ordinal)) count++;
            if (string.Equals(candidate, neck, System.StringComparison.Ordinal)) count++;
            if (string.Equals(candidate, back, System.StringComparison.Ordinal)) count++;

            if (count <= bestCount) return;
            bestCount = count;
            best = candidate;
        }
    }
}
