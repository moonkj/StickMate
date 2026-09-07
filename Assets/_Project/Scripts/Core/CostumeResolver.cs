namespace StickMate.Core
{
    /// <summary>
    /// ============================================================================
    /// ★★ 규칙 C-1 — <b>코스튬은 「착용 4부위가 합의하는 소속」이 「열려 있을 때」만 산다</b>
    /// ============================================================================
    /// 정본: <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 1-2절.
    ///
    /// <list type="bullet">
    ///  <item><b>소속의 단위는 테마가 아니라 <u>코호트</u>다.</b> 기본 코호트(0)에서만 테마가 소속을 대신한다.</item>
    ///  <item><b>개방은 엔타이틀먼트다.</b> 단 무료는 「물어보지 않는 것」이지 「<c>Owned</c>를 받는 것」이 아니다.</item>
    ///  <item><b>저장 파일은 개방 판정에 절대 입력되지 않는다</b>(§E-4-a · 규칙 C-3).</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 왜 「테마 세트 완성」이 아닌가 — 실측 두 줄이 그 안을 죽였다
    /// ============================================================================
    /// <code>
    /// ItemCatalog.ThemeOfItem:  if (cohortId != BaseCohortId ...) return ThemeUnassigned;
    /// </code>
    /// ⇒ <b>팩 아이템은 테마 문자열을 구조적으로 못 받는다.</b> 그러므로
    /// <c>EquipmentStatRules.IsSetComplete</c>가 <c>true</c>가 되는 유일한 길은 <b>기본 코호트 24종</b>이고,
    /// 그 24종은 전부 무료·동전이다. <c>cyber</c> 세트 완성 = 왕관 + 외알안경 + 펜던트 + 긴망토 =
    /// <b>전부 기본 42종</b>이다 ⇒ 「세트 완성이 곧 코스튬」이면 <b><c>pack.cyber</c>의 간판 연출이 100% 무료로 샌다.</b>
    ///
    /// <para>★ 반대로 「엔타이틀먼트 단독」도 죽는다. 오늘 실린 팩이 0개이고 출처가
    /// <c>NullPackEntitlementSource</c>라 <c>StateOf</c>가 언제나 <c>Unknown</c>이며, <c>Unknown</c>은
    /// «새로 시작 거부»다 ⇒ <b>무료 오피스까지 포함해 전원이 아무것도 못 본다.</b>
    /// 그리고 그 증상은 «기능이 아직 안 붙었다»와 <b>화면상 완전히 같다</b>.</para>
    ///
    /// ============================================================================
    /// ★ 잠금 검사를 여기서 새로 짜지 않는다
    /// ============================================================================
    /// 착용 판정은 <see cref="EquipmentStatRules.SlotLoadout"/>가 유일한 창구다. 그 함수가
    /// «걸쳤다 <b>그리고</b> 해금됐다»를 액자·몸·슬롯 줄과 <b>같은 술어</b>로 이미 계산하고 있고,
    /// 그 파일이 <i>"잠금 검사를 여기 말고 또 적으면 그때부터 이음매가 둘이 된다"</i>고 못박았다.
    /// <b>여기서 <c>EquipmentModel.IsUnlocked</c>를 다시 곱하지 마라</b> — 잠금 규칙이 바뀌는 날
    /// 코스튬만 옛 규칙을 지키게 되고, 그 어긋남은 화면만 봐서는 못 찾는다.
    ///
    /// <para>★ 코호트만은 <see cref="ItemCatalog"/>에서 한 번 더 집는다 —
    /// <see cref="StatSlotLoadout"/>이 <c>CohortId</c>를 <b>안 나르기 때문</b>이고,
    /// 그건 <b>새 술어가 아니라 없는 값 하나를 읽는 것</b>이다. 위 <c>Worn</c>이 참이면
    /// 그 자리의 항목은 <c>null</c>일 수 없다(<see cref="EquipmentStatRules.SlotLoadout"/>가
    /// 항목을 못 찾으면 <c>Empty</c>를 돌려준다).</para>
    ///
    /// ============================================================================
    /// ★★ 규칙 C-3 — 이 파일은 <b>세이브를 절대 읽지 않는다</b>
    /// ============================================================================
    /// <b>세이브 저장소도, 코스튬 누적 모델도 여기서 참조하지 마라.</b>
    /// ★ 그 두 타입의 <b>이름조차 이 파일에 적지 않는다</b>: 이 규칙을 재는 감사는 소스 텍스트를
    /// 훑는데, 경고 문구 안의 이름과 실제 <c>using</c>이 <b>똑같이 생겼기</b> 때문이다.
    /// 이름을 안 적으면 그 감사가 주석 제거를 빠뜨려도 <b>여전히 옳은 답</b>을 낸다
    /// (죽은 프로브가 산 프로브와 똑같이 생기는 것 — 이 저장소가 반복해서 당한 형태다).
    /// «몇 분 쌓았는가»가 «지금 무엇을 입고 있는가»의 입력이 되면, 세이브 파일 한 줄이
    /// 유료 연출을 여는 표적이 된다.
    /// </summary>
    public static class CostumeResolver
    {
        /// <summary>코스튬을 정하는 네 자리. <b>스탯 4슬롯과 같은 집합</b>이다 —
        /// 외형 3슬롯(머리/이펙트/펫)은 테마도 코호트 합의도 갖지 않는다.</summary>
        private static readonly EquipmentSlot[] StatSlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders,
        };

        /// <summary>
        /// 지금 차림이 만드는 코스튬. <b><c>null</c>이면 코스튬 없음</b>이고 그건 정상 상태다
        /// (오늘 실린 코스튬 매니페스트가 0개라 <b>언제나</b> <c>null</c>이다 — 그것도 정상이다).
        ///
        /// <para>★ 이 함수는 <b>매 프레임 부르는 용도가 아니다.</b> 계약서 C-2가 정한 조회 시점은
        /// «세션 시작 1회 · 착용 변경마다 1회 · 세션 중 60초마다 1회»이고, 그 캐싱은
        /// 세션을 아는 층(<c>FocusWatchDirector</c>/<c>StickmanBlackboard</c>)의 책임이다.</para>
        /// </summary>
        public static CostumeDescriptor Resolve()
        {
            // ---- 1. 스탯 4슬롯이 전부 «걸쳤고 해금됐는가» + 소속이 합의되는가 ----
            StatSlotLoadout first = EquipmentStatRules.SlotLoadout(StatSlots[0]);
            if (!first.Worn) return null;

            int cohort = CohortOf(StatSlots[0]);
            string theme = first.Theme;

            for (int i = 1; i < StatSlots.Length; i++)
            {
                StatSlotLoadout slot = EquipmentStatRules.SlotLoadout(StatSlots[i]);
                if (!slot.Worn) return null;

                // ---- 2. 네 자리의 코호트가 전부 같은가 ----
                if (CohortOf(StatSlots[i]) != cohort) return null;

                // 테마는 기본 코호트 갈래에서만 뜻이 있지만, 여기서 함께 접어 두면
                // 아래 3a가 다시 순회하지 않는다(같은 사실을 두 번 걷지 않는다).
                if (!string.Equals(slot.Theme, theme, System.StringComparison.Ordinal)) theme = null;
            }

            // ---- 3. 기본 코호트 — 테마가 소속을 대신한다. 엔타이틀먼트를 «안 묻는다» ----
            if (cohort == ItemCatalog.BaseCohortId)
            {
                // 3a. 네 자리 테마가 전부 같은가. ★ 무소속(빈 문자열)은 «같다»로 세지 않는다 —
                //     세면 아무 테마도 없는 4종에 코스튬이 뜬다(IsSetComplete가 막는 그 형태).
                if (string.IsNullOrEmpty(theme)) return null;

                // 3b. 그 테마의 코스튬이 실려 있는가. 없으면 null — 「세트는 완성인데 코스튬은 없다」는
                //     정상 상태다(오늘 기본 코호트 코스튬은 costume.office 하나를 예정하고 있고,
                //     그마저 매니페스트 에셋이 아직 0개다).
                // 3c. 기본 코호트 코스튬에는 엔타이틀먼트가 없다 → 그대로 통과.
                return CostumeCatalog.FindByBaseTheme(theme);
            }

            // ---- 4. 팩 코호트 — 소속은 코호트가 정하고, 개방은 엔타이틀먼트가 정한다 ----
            // 4a. 그 코호트의 주인이 있는가. 없으면 「고아 코호트」이고 <b>살 방법이 없는 상태</b>다.
            PackDescriptor pack = PackRegistry.FindByCohort(cohort);
            if (pack == null) return null;

            // 4b. 그 팩에 코스튬이 있는가(코스튬 없는 팩도 정상이다).
            CostumeDescriptor costume = CostumeCatalog.FindByPack(pack.PackId);
            if (costume == null) return null;

            // 4c. ★ 개방 판정은 CostumeEntitlement 한 곳뿐이다(계약서 I-5).
            //     여기서 스토어 조회 창구를 직접 부르면 그 순간 판정이 두 곳이 된다.
            //     ★ 그 창구의 이름도 여기 적지 않는다 — 감사가 소스 텍스트를 훑기 때문이고,
            //       주석 속 이름과 실제 호출은 텍스트로 똑같이 생겼다.
            return CostumeEntitlement.IsOpen(costume, pack) ? costume : null;
        }

        /// <summary>
        /// 지금 차림의 코스튬 키(없으면 <c>null</c>). 「입은 채」 판정이 <b>시작 ∧ 종료</b>라
        /// (계약서 7-4절) 세션 층이 비교하는 값은 서술자가 아니라 <b>이 문자열</b>이다.
        ///
        /// <para>★ <b>런타임 누적기를 만들지 마라.</b> 세션 시작 시점의 키는 세션 층의
        /// <b>인스턴스 필드 하나</b>면 되고, 세이브에 넣지 않는다 —
        /// 세션은 프로세스를 넘어 살지 않는다.</para>
        /// </summary>
        public static string ResolveKey()
        {
            CostumeDescriptor costume = Resolve();
            return costume?.CostumeKey;
        }

        /// <summary>이 자리에 걸친 것의 코호트. 위 <c>Worn</c>이 참인 자리에서만 부른다.</summary>
        private static int CohortOf(EquipmentSlot slot)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, EquipmentModel.WornIndex(slot));
            return entry != null ? entry.CohortId : ItemCatalog.BaseCohortId;
        }
    }
}
