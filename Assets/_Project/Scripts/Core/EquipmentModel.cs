using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 착용 카테고리 7종. 카테고리 하나에는 아이템이 <b>4개</b> 있고 그중 <b>최대 하나</b>만 걸칠 수 있다
    /// (2026-08-30 핸드오프 8×4=32종으로 확장 → 같은 날 표정 삭제로 7×4=28종). 아이템 목록/이름/설명/요구 레벨은 전부
    /// Core/ItemCatalog.cs의 표에서 온다 — 이 enum은 "자리"만 정의한다.
    ///
    /// ★ 값(0~7)은 저장 파일이 아니라 <b>배열 인덱스</b>로 쓰인다. 순서를 바꾸면 렌더러 서명
    /// (Interaction/CharacterAccessoryRenderer.ComputeSignature)의 비트 자리와 정보창 카드 순서가 함께
    /// 바뀐다. 저장 파일은 순서에 의존하지 않는다(카테고리별로 이름 붙은 필드 + 아이템 아이디 문자열).
    /// </summary>
    public enum EquipmentSlot
    {
        /// <summary>모자(HEAD). 챙이 진행 방향으로 뻗는 <b>비대칭</b> 아이템이라 좌우 반전 검증 대상.</summary>
        Head = 0,

        /// <summary>안경(EYES). 렌즈 2개는 대칭이지만 안경다리가 진행 반대쪽으로 뻗어 비대칭이다.</summary>
        Eyes = 1,

        /// <summary>넥타이(NECK). 좌우 대칭.</summary>
        Neck = 2,

        /// <summary>망토(BACK). 진행 반대쪽으로 흘러내리는 <b>가장 비대칭</b>인 아이템.
        /// enum 이름이 Shoulders인 것은 역사적 이유다(4슬롯 시절) — 핸드오프의 슬롯 코드는 BACK이고
        /// <see cref="EquipmentModel.SlotCode"/>가 그 값을 돌려준다. 이름을 바꾸지 않은 이유는
        /// 이 식별자가 이미 저장 파일 필드명/렌더러/테스트에 퍼져 있어서다.</summary>
        Shoulders = 3,

        // ★ 2026-08-30 표정(FACE) 카테고리 삭제 — 사용자 결정("장비중에 표정관련은 전부삭제
        //   어차피 구별이 안됨"). 40×40 카드에서도, 화면상 지름 32pt짜리 머리 위에서도 눈/입 두 부위의
        //   차이가 읽히지 않았다. 값을 남겨 두고 "쓰지 않는다"로 하지 않은 이유는 그 자리가 곧
        //   저장/렌더/UI 세 곳에서 계속 분기를 요구하기 때문이다(오늘 라이벌 삭제와 같은 방침).
        //   뒤 값들이 한 칸씩 당겨졌지만 저장 파일은 <b>아이디 문자열</b>로 적히므로 영향이 없다
        //   (Core/CharacterSaveStore.cs — "인덱스 vs 아이디" 문서).

        /// <summary>머리 모양(HAIR).</summary>
        Hair = 4,

        /// <summary>이펙트(FX). 몸에 붙는 도형이 아니라 <b>움직임에 따라 발동</b>하는 연출이다.</summary>
        Fx = 5,

        /// <summary>펫(PET). 캐릭터를 따라다니는 별도 개체.</summary>
        Pet = 6,
    }

    /// <summary>
    /// ★ 착용 상태 — 2026-08-29 사용자 요청("캐릭터 장비 착용"), 2026-08-30 32종으로 확장.
    ///
    /// ============================================================================
    /// 원안(docs/UX_FLOW.md 7절 "스킨/DLC 탭")을 왜 그대로 쓰지 않았는가 — <b>구매 → 레벨업 해제</b>
    /// ============================================================================
    /// 결제 백엔드가 없고(스토어/영수증 검증/복원 어느 것도 이 프로젝트에 없다), 외부 아트 에셋도 없다
    /// (모든 시각 요소가 LineRenderer 프로시저럴 선화다). 결제 UI를 흉내만 내는 것은 사용자에게 거짓
    /// 약속이 되므로 해제 조건을 <b>레벨</b>로 치환했다. 관찰형 앱 철학("아무것도 안 해도 자란다")과도
    /// 맞는다 — 지갑이 아니라 함께 보낸 시간이 보상을 연다.
    ///
    /// ============================================================================
    /// 보유(owned)와 착용(worn)은 다른 사실이다
    /// ============================================================================
    /// 확장 전에는 카테고리당 아이템이 하나뿐이라 "해제 = 착용 가능"이 곧 상태의 전부였고 bool 4개로
    /// 충분했다. 이제는 카테고리 안에서 <b>무엇을</b> 걸쳤는지를 골라야 하므로 상태가
    /// <c>int[8]</c>(아이템 자리, <see cref="NotWorn"/>=-1)로 바뀌었다. 보유 여부는 상태가 아니라
    /// 레벨에서 매번 파생된다(저장하지 않는다 — 저장하면 레벨과 어긋난 두 번째 진실이 생긴다).
    ///
    /// ============================================================================
    /// 인덱스 vs 문자열 아이디 — 런타임은 인덱스, 파일은 아이디
    /// ============================================================================
    /// 착용 상태를 문자열로 들고 있으면 렌더러가 <b>매 프레임</b> 문자열 비교를 하게 된다(액세서리
    /// 서명 계산은 Update 경로다). 이 앱은 하루 종일 켜져 있어서 그런 상시 비용을 만들지 않는다.
    /// 반대로 저장 파일에 인덱스를 적으면 훗날 표 중간에 아이템을 하나 끼워 넣는 순간 <b>모든 사용자의
    /// 착용 아이템이 조용히 한 칸씩 밀린다</b>. 그래서 경계에서 한 번만 변환한다:
    /// 파일 ↔ 아이디(Core/CharacterSaveStore.cs), 메모리 ↔ 인덱스(여기).
    ///
    /// TodoListModel/StressGauge와 같은 이유로 정적 클래스이며, 저장/로드는 Core/CharacterSaveStore.cs가
    /// 전담한다.
    /// </summary>
    public static class EquipmentModel
    {
        /// <summary>카테고리 수. 표(Core/ItemCatalog.cs)가 진짜 소스라 상수와 어긋나면 표를 따른다.</summary>
        public const int SlotCount = 7;

        /// <summary>"이 카테고리에 아무것도 걸치지 않았다". null 대신 -1을 쓰는 이유는 상태 배열이
        /// <c>int[]</c>여서다(<c>int?[]</c>는 박싱 없는 대신 비교마다 HasValue 분기가 붙고, 저장 경로에서
        /// null과 "미착용"을 두 번 표현하게 된다).</summary>
        public const int NotWorn = -1;

        /// <summary>외형 계열(머리/이펙트/펫)이 시작되는 자리.</summary>
        private const int FirstAppearanceSlot = (int)EquipmentSlot.Hair;

        /// <summary>지금 카테고리별로 걸치고 있는 아이템 자리. -1이면 미착용.</summary>
        private static readonly int[] _worn = CreateDefaultWorn();

        /// <summary>
        /// 새 캐릭터의 시작 차림(핸드오프 확정): 모자=천모자, 안경=선글라스만 착용하고 나머지 5개
        /// 카테고리는 <b>보유하되 미착용</b>이다. 머리도 마찬가지라 처음 얼굴은 지금까지와 똑같다 —
        /// 기본값이 캐릭터 생김새를 바꾸면 "내가 안 했는데 달라졌다"가 되기 때문.
        /// </summary>
        private static int[] CreateDefaultWorn()
        {
            var worn = new int[SlotCount];
            for (int i = 0; i < SlotCount; i++) worn[i] = NotWorn;
            worn[(int)EquipmentSlot.Head] = 0;   // 천모자
            worn[(int)EquipmentSlot.Eyes] = 0;   // 선글라스
            return worn;
        }

        private static bool InRange(EquipmentSlot slot) => (int)slot >= 0 && (int)slot < SlotCount;

        // ==================== 카테고리 단위 사실(이름/코드) — 여기가 단일 소스 ====================

        /// <summary>카테고리 표시 이름(정보창 그리드의 부제, 보관함 목록의 부제).</summary>
        public static string SlotName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "모자";
                case EquipmentSlot.Eyes: return "안경";
                case EquipmentSlot.Neck: return "넥타이";
                case EquipmentSlot.Shoulders: return "망토";
                case EquipmentSlot.Hair: return "머리";
                case EquipmentSlot.Fx: return "이펙트";
                case EquipmentSlot.Pet: return "펫";
                default: return "?";
            }
        }

        /// <summary>디자인 핸드오프의 슬롯 코드. 시각 설계 문서(docs/UX_FLOW.md)와 코드를 대조할 때
        /// 쓰는 이름이라 표시 문자열과 분리해 둔다.</summary>
        public static string SlotCode(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "HEAD";
                case EquipmentSlot.Eyes: return "EYES";
                case EquipmentSlot.Neck: return "NECK";
                case EquipmentSlot.Shoulders: return "BACK";
                case EquipmentSlot.Hair: return "HAIR";
                case EquipmentSlot.Fx: return "FX";
                case EquipmentSlot.Pet: return "PET";
                default: return "?";
            }
        }

        /// <summary>외형 계열(머리/이펙트/펫)인가 — UI가 "장비 계열 / 외형 계열"로 묶어 보여줄 때만
        /// 쓴다. 데이터로서의 취급은 두 계열이 완전히 같다(Core/ItemCatalog.cs 문서 참고).
        /// <para>★ 이 술어는 <b>계열</b>만 말한다. 그 계열 중 화면에 실제로 뜨는 자리인지는
        /// <see cref="IsRetiredSlot"/>가 따로 답한다 — 머리는 계열로는 여전히 외형이지만
        /// 고를 수 있는 자리가 아니다.</para></summary>
        public static bool IsAppearanceSlot(EquipmentSlot slot) => (int)slot >= FirstAppearanceSlot;

        /// <summary>
        /// ★ <b>은퇴한 카테고리</b> — 사용자 지시 2026-09-06 *"외형에서 머리 스타일은 전체 삭제"*.
        /// 지금 해당하는 자리는 <see cref="EquipmentSlot.Hair"/> 하나다.
        ///
        /// <para><b>왜 enum 값과 에셋을 지우지 않았는가</b> — 2026-08-30 표정(FACE) 삭제와 조건이
        /// 달라졌다. 그때 카테고리는 스탯·등급 체계가 들어오기 <b>전</b>이었다. 지금 머리 6종은
        /// 카탈로그 <b>42종(7 × 6)</b>의 일부이고, 그 42가 등급 파생(슬롯당 2/2/1/1)·골든 덤프·
        /// 팩 자리 번호가 딛고 선 분모다. 에셋을 빼면 <b>아무도 안 건드린 등급이 통째로 미끄러진다</b>
        /// (<c>ItemCatalog.CohortId</c> 문단이 경고하는 바로 그 사고). 그래서 데이터는 그대로 두고
        /// <b>고를 수 있는 자리에서만</b> 뺀다.</para>
        ///
        /// <para>★ <b>이 술어 하나로 화면과 몸이 함께 닫힌다.</b> 걸치는 유일한 경로
        /// (<see cref="TryWear"/>)와 저장 복원(<see cref="RestoreFromSave(EquipmentSlot,string)"/>)이
        /// 둘 다 여기서 막히므로 <see cref="WornIndex"/>는 이 자리에서 <b>영원히</b>
        /// <see cref="NotWorn"/>이고, 몸/초상화 렌더러의 "걸친 것만 그린다"(<c>ShouldDraw</c>)가
        /// 그대로 렌더 스킵이 된다. 렌더러에 분기를 하나도 더하지 않는 이유가 이것이다 —
        /// 더하면 "무엇이 은퇴했는가"가 두 곳에 생기고, 한쪽만 고치는 날 화면과 모델이 갈라진다.</para>
        ///
        /// <para>★ <b>이미 머리를 걸치고 있던 저장 파일</b>은 그대로 열린다. 복원이 그 칸만 미착용으로
        /// 떨어뜨리고 나머지 값은 한 줄도 건드리지 않는다 — 파일을 고쳐 쓰지도, 버리지도, 마이그레이션
        /// 하지도 않는다. 다음 저장에서 <c>wornHair</c>가 비는 것은 "지금 아무것도 안 걸쳤다"는
        /// <b>사실 그대로</b>이고, 저장 스키마 버전은 움직이지 않는다.</para>
        /// </summary>
        public static bool IsRetiredSlot(EquipmentSlot slot) => slot == EquipmentSlot.Hair;

        /// <summary>이 아이템의 안정적 아이디. <b>여기 말고 어디에도 적지 않는다</b> —
        /// 자리 번호(0)로 적으면 카탈로그가 재정렬되는 날 엉뚱한 이펙트가 사라진다.</summary>
        internal const string RetiredFxNoneItemId = "look.fx.none";

        /// <summary>
        /// ★ <b>은퇴한 아이템</b> — 사용자 지시 2026-09-06 *"이펙트 없음은 왜 있는거야 삭제해줘 장비창에서"*.
        /// 지금 해당하는 것은 이펙트의 <c>look.fx.none</c>(<see cref="RetiredFxNoneItemId"/>) 하나다.
        ///
        /// <para><b>왜 「없음」이 이상한가</b> — 42종 중 <b>기능이 겹치는 유일한 카드</b>였다.
        /// 벗기는 일은 이미 모든 카드가 자기 하단 버튼으로 한다(걸치고 있으면 그 버튼이 [해제]다).
        /// 그래서 이펙트만 「아무것도 아닌 것을 <b>걸치는</b>」 두 번째 경로를 갖고 있었고,
        /// 모자·안경·목걸이·망토·펫 다섯 카테고리에는 그런 카드가 없다. 사용자의 "왜 있는거야"는
        /// 정확히 그 <b>비대칭</b>을 가리킨다.</para>
        ///
        /// <para><b>왜 에셋을 지우지 않는가</b>(<see cref="IsRetiredSlot"/>와 같은 이유, 그리고 하나 더):
        /// ① 42종(7 × 6)이 등급 파생·코호트 순위·골든 덤프의 분모다. ② 이펙트는 <b>자리 번호 자체가
        /// 렌더러의 약속</b>이다 — <c>AppearanceShapeBuilder.FxNone = 0 / FxFootprint = 1 …</c>이고
        /// <c>CharacterFxRenderer</c>는 <c>item &lt;= FxNone</c>으로 «그릴 것 없음»을 판정한다.
        /// 0번을 빼면 발자국이 0번이 되어 <b>발자국이 통째로 안 그려진다</b>. 데이터는 그대로 두고
        /// <b>보여주는 목록에서만</b> 뺀다.</para>
        ///
        /// <para>★ 은퇴한 <b>카테고리</b>의 아이템도 여기서 참이다 — 부르는 쪽이 술어를 둘 들고
        /// 다니지 않게 한다(<see cref="ItemCatalog.IsListed"/>가 이 하나만 본다).
        /// <see cref="NotWorn"/>(−1)은 은퇴가 아니라 <b>미착용</b>이므로 거짓이다.</para>
        ///
        /// <para>★ 걸치는 경로(<see cref="TryWear"/>)와 저장 복원(<see cref="RestoreFromSave(EquipmentSlot,string)"/>
        /// · <see cref="RestoreFromSave(EquipmentSlot,bool)"/>)이 함께 막히므로, <b>「없음」을 걸친 채
        /// 저장했던 파일도 미착용으로 열린다</b>. 그 상태의 겉모습은 원래 이펙트 없음과 <b>같으므로</b>
        /// 사용자가 잃는 것은 없다. 파일은 읽기만 하고 스키마 버전도 움직이지 않는다.</para>
        /// </summary>
        public static bool IsRetiredItem(EquipmentSlot slot, int itemIndex)
        {
            if (itemIndex < 0) return false;              // 미착용은 «은퇴»가 아니다.
            if (!InRange(slot)) return false;
            if (IsRetiredSlot(slot)) return true;         // 은퇴한 카테고리는 통째로.
            return slot == EquipmentSlot.Fx
                   && itemIndex == ItemCatalog.IndexOfItemId(EquipmentSlot.Fx, RetiredFxNoneItemId);
        }

        // ==================== 아이템 단위 사실 — 전부 ItemCatalog에 위임 ====================

        public static int ItemCount(EquipmentSlot slot) => ItemCatalog.ItemCountIn(slot);

        /// <summary>이 카테고리에서 <b>화면에 보여주는</b> 아이템 수(정보창 카테고리 제목줄의 "n / 5").
        /// 위 <see cref="ItemCount"/>는 카탈로그 전량이라 감사·등급 파생이 쓰고, 이쪽은 화면이 쓴다 —
        /// 둘을 하나로 합치면 둘 중 하나가 반드시 틀린다(<see cref="ItemCatalog.EquipmentCount"/> 문단).</summary>
        public static int ListedItemCount(EquipmentSlot slot) => ItemCatalog.ListedItemCountIn(slot);

        /// <summary>보여주는 목록의 <paramref name="listedIndex"/>번째가 카탈로그의 몇 번째인가.
        /// 범위 밖이면 <see cref="NotWorn"/>(−1).</summary>
        public static int ListedItemIndex(EquipmentSlot slot, int listedIndex)
            => ItemCatalog.ListedItemIndex(slot, listedIndex);

        public static string ItemName(EquipmentSlot slot, int itemIndex)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, itemIndex);
            return entry != null ? entry.DisplayName : "?";
        }

        public static string ItemId(EquipmentSlot slot, int itemIndex)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, itemIndex);
            return entry != null ? entry.Id : null;
        }

        /// <summary>이 아이템을 보유하게 되는 레벨(1이면 처음부터). 표에 없는 자리는 도달 불가로 취급.</summary>
        public static int RequiredLevel(EquipmentSlot slot, int itemIndex)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, itemIndex);
            return entry != null && entry.RequiredLevel.HasValue ? entry.RequiredLevel.Value : int.MaxValue;
        }

        /// <summary><see cref="EquipmentDebugUnlock.UnlockAll"/>이 켜져 있으면 레벨을 보지 않는다
        /// (QA용 — 장비 전종을 눌러 보기 위해). 위 <see cref="RequiredLevel"/>은 그대로 살아 있고,
        /// 스위치가 꺼지면 원래 규칙으로 돌아온다.
        /// <para>★ 2026-09-01: 그 스위치는 더 이상 "사람이 출시 전에 되돌려야 하는 상수"가 아니다 —
        /// 빌드 구성으로 갈린다(<b>사용자에게 나가는 릴리스 빌드에서는 자동으로 꺼진다</b>).
        /// 근거와 검증은 <c>EquipmentDebugUnlock</c> 문서 / <c>EquipmentDebugUnlockReleaseGateTests</c>.</para></summary>
        public static bool IsItemOwned(EquipmentSlot slot, int itemIndex)
            => EquipmentDebugUnlock.UnlockAll
               || CharacterProgressionModel.Level >= RequiredLevel(slot, itemIndex);

        /// <summary>이 카테고리에서 지금 보유한 아이템 수(정보창 카테고리 카드의 "n/4").</summary>
        public static int OwnedItemCount(EquipmentSlot slot)
        {
            int n = 0;
            int count = ItemCount(slot);
            for (int i = 0; i < count; i++)
            {
                if (IsItemOwned(slot, i)) n++;
            }
            return n;
        }

        /// <summary>보여주는 목록 안에서 지금 보유한 아이템 수 — 카테고리 제목줄의 <b>분자</b>다.
        /// 분모(<see cref="ListedItemCount"/>)와 <b>같은 모집단</b>을 센다(한쪽만 은퇴를 반영하면
        /// 「6 / 5」가 나온다).</summary>
        public static int ListedOwnedItemCount(EquipmentSlot slot)
        {
            int n = 0;
            int listed = ListedItemCount(slot);
            for (int i = 0; i < listed; i++)
            {
                int item = ListedItemIndex(slot, i);
                if (item >= 0 && IsItemOwned(slot, item)) n++;
            }
            return n;
        }

        /// <summary>보유한 아이템 중 첫 자리(없으면 -1). <see cref="TryToggle"/>가 "일단 하나 걸쳐라"에 쓴다.
        /// <para>★ 은퇴한 아이템은 건너뛴다 — 이 경로로 걸치면 <see cref="TryWear"/>가 거절해
        /// <b>«벗기만 하고 아무것도 안 걸치는»</b> 토글이 된다(이펙트에서는 0번이 바로 그 자리다).</para></summary>
        public static int FirstOwnedItemIndex(EquipmentSlot slot)
        {
            int count = ItemCount(slot);
            for (int i = 0; i < count; i++)
            {
                if (IsRetiredItem(slot, i)) continue;
                if (IsItemOwned(slot, i)) return i;
            }
            return NotWorn;
        }

        // ==================== 착용 상태 ====================

        /// <summary>지금 걸치고 있는 아이템 자리. 미착용이면 <see cref="NotWorn"/>.</summary>
        public static int WornIndex(EquipmentSlot slot) => InRange(slot) ? _worn[(int)slot] : NotWorn;

        /// <summary>지금 걸치고 있는 아이템의 안정적 아이디(저장 전용). 미착용이면 null.</summary>
        public static string WornItemId(EquipmentSlot slot)
        {
            int worn = WornIndex(slot);
            return worn >= 0 ? ItemId(slot, worn) : null;
        }

        /// <summary>이 카테고리에 뭔가 걸치고 있는가.</summary>
        public static bool IsEquipped(EquipmentSlot slot) => WornIndex(slot) >= 0;

        /// <summary>바로 <b>이 아이템</b>이 걸쳐져 있는가.</summary>
        public static bool IsEquipped(EquipmentSlot slot, int itemIndex)
            => itemIndex >= 0 && WornIndex(slot) == itemIndex;

        /// <summary>
        /// 지금 이 카테고리가 "쓸 수 있는" 상태인가.
        ///  · 착용 중이면 <b>그 아이템을 지금 레벨에서 보유하는지</b>(저장 파일이 앞선 레벨에서 만들어졌다가
        ///    레벨이 낮게 복원된 경우를 렌더러가 걸러내는 자리 — 그래서 복원은 잠금을 검사하지 않는다).
        ///  · 미착용이면 <b>고를 수 있는 것이 하나라도 있는지</b>.
        /// 요구 레벨이 <see cref="ItemCatalog"/>의 아이템 단위 데이터로 옮겨간 뒤로 카테고리 단위
        /// <c>StickConfig</c> 조회가 필요 없어져 인자를 없앴다(2026-08-30 R2 m5).
        /// </summary>
        public static bool IsUnlocked(EquipmentSlot slot)
        {
            int worn = WornIndex(slot);
            return worn >= 0 ? IsItemOwned(slot, worn) : FirstOwnedItemIndex(slot) >= 0;
        }

        // ★ 2026-08-30 R3-m2 — 아래 세 공개 API를 삭제했다(호출부 0, 제품·테스트 전부 확인):
        //   · UnlockLevel(EquipmentSlot, StickConfig) — 마지막 호출자였던
        //     CharacterProgressionDirector.DescribeNewUnlocks가 이번 라운드에 ItemCatalog 순회로
        //     교체되면서 통째로 죽었다. 화면에 "필요 레벨"을 보여주는 자리는 지금
        //     ItemCatalogEntry.ResolveUnlockLevel(config) / LockedLabel이 아이템 단위로 처리한다.
        //   · LowestRequiredLevel(EquipmentSlot) — 위 메서드에서만 불렸다(전이적 사망).
        //   · ItemName(EquipmentSlot) 1인자 오버로드 — 2인자 오버로드만 쓰인다.
        // 되살릴 일이 생기면 카테고리 단위가 아니라 **아이템 단위**(ItemCatalog)로 다시 짜는 것이
        // 맞다 — 카테고리당 아이템이 4종이 된 뒤로 "카테고리의 대표 이름/대표 레벨"은 의미가 없다.

        /// <summary>지금 하나라도 착용 중인가 — 렌더러가 "그릴 것이 아무것도 없으면 통째로 쉰다"에 쓴다.</summary>
        public static bool AnyEquipped()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_worn[i] >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// ★ 착용 상태 전체의 서명. 렌더러가 "다시 구울지"를 판단할 때 <b>카테고리 비트마스크 대신</b>
        /// 이 값을 써야 한다 — 같은 카테고리 안에서 아이템만 바꾸면(천모자 → 왕관) 마스크는 그대로라
        /// 도형이 갱신되지 않는다. 32종 확장으로 새로 생긴 함정이고, 몸/초상화 두 렌더러가 이미 이 값을 쓴다.
        /// 할당 없이 정수 하나만 굴린다(Update 경로에서 불린다).
        /// </summary>
        public static int WornStateSignature
        {
            get
            {
                int hash = 17;
                for (int i = 0; i < SlotCount; i++) hash = hash * 31 + (_worn[i] + 1);
                return hash;
            }
        }

        /// <summary>
        /// 이 아이템을 걸친다. <paramref name="itemIndex"/>가 <see cref="NotWorn"/>이면 벗는다.
        /// 잠긴 아이템이면 아무 일도 하지 않고 false — "잠금 해제"를 여기서 대신 해주지 않는다
        /// (레벨만이 유일한 해제 경로라는 규칙을 코드로 강제).
        /// </summary>
        public static bool TryWear(EquipmentSlot slot, int itemIndex, StickConfig config)
        {
            if (!InRange(slot)) return false;

            // 은퇴한 카테고리(<see cref="IsRetiredSlot"/>)는 걸치는 것도 벗는 것도 없다 — 항상 미착용이라
            // 벗기 요청도 "이미 그렇다"이고, 이 한 줄이 "화면에 없는 것은 몸에도 없다"의 유일한 잠금이다.
            if (IsRetiredSlot(slot)) return false;

            if (itemIndex == NotWorn)
            {
                if (_worn[(int)slot] == NotWorn) return false;
                _worn[(int)slot] = NotWorn;
                StickmanEventBus.RaiseCharacterEquipmentChanged();
                return true;
            }

            if (itemIndex < 0 || itemIndex >= ItemCount(slot)) return false;
            // 은퇴한 아이템(<see cref="IsRetiredItem"/>)도 같은 잠금을 받는다 — 화면에 카드가 없는데
            // 다른 경로(토글/복원/테스트 API)로만 걸쳐지면 벗을 손잡이가 없는 차림이 된다.
            if (IsRetiredItem(slot, itemIndex)) return false;
            if (!IsItemOwned(slot, itemIndex)) return false;
            if (_worn[(int)slot] == itemIndex) return false;

            _worn[(int)slot] = itemIndex;
            StickmanEventBus.RaiseCharacterEquipmentChanged();
            return true;
        }

        /// <summary>같은 아이템을 다시 누르면 벗고, 아니면 걸친다(정보창 아이템 클릭).</summary>
        public static bool ToggleItem(EquipmentSlot slot, int itemIndex, StickConfig config)
            => IsEquipped(slot, itemIndex)
                ? TryWear(slot, NotWorn, config)
                : TryWear(slot, itemIndex, config);

        /// <summary>
        /// 카테고리 단위 토글(확장 전 API 그대로 남긴 것 — 호출부가 아직 카테고리만 아는 경로가 있다).
        /// 착용 중이면 벗고, 미착용이면 <b>보유한 첫 아이템</b>을 걸친다. 고를 수 있는 것이 없으면 false.
        /// </summary>
        public static bool TryToggle(EquipmentSlot slot, StickConfig config)
        {
            if (!InRange(slot)) return false;
            if (IsEquipped(slot)) return TryWear(slot, NotWorn, config);

            int first = FirstOwnedItemIndex(slot);
            return first >= 0 && TryWear(slot, first, config);
        }

        // ==================== 저장 복원 ====================

        /// <summary>저장 파일(v5) 복원 전용. 잠금 여부를 검사하지 <b>않는다</b> — 검사하면 저장 시점보다
        /// 레벨이 낮게 복원되는 순간(파일 손상 등) 착용물이 조용히 사라진다. 대신 렌더러/UI가 그릴 때
        /// <see cref="IsUnlocked"/>로 함께 본다.
        /// 모르는 아이디(훗날 표에서 빠진 아이템, 손상)는 <b>미착용</b>으로 떨어뜨린다 — 없는 아이템을
        /// 억지로 다른 것으로 바꿔치기하면 사용자가 고르지 않은 차림이 된다.</summary>
        internal static void RestoreFromSave(EquipmentSlot slot, string itemId)
        {
            if (!InRange(slot)) return;

            // ★ 은퇴한 카테고리는 파일에 무엇이 적혀 있든 <b>미착용</b>으로 연다(<see cref="IsRetiredSlot"/>).
            //   파일은 읽기만 한다 — 값을 지우지도, 다른 아이템으로 바꿔치기하지도 않는다.
            //   이 자리가 없으면 머리를 걸친 채 저장했던 사용자만 "화면에서는 지웠는데 몸에는 남은"
            //   상태로 앱을 켜게 되고, 그건 정보창 어디에서도 벗을 수 없는 상태다(탈출구가 없다).
            //   ★ 2026-09-06 후속 — 판정을 <b>아이템 단위</b>(<see cref="IsRetiredItem"/>)로 넓혔다.
            //   이펙트 「없음」을 걸친 채 저장했던 파일이 그대로 열리면 카드가 없는 차림이 된다.
            int restored = IsRetiredSlot(slot) ? NotWorn : ItemCatalog.IndexOfItemId(slot, itemId);
            _worn[(int)slot] = IsRetiredItem(slot, restored) ? NotWorn : restored;
        }

        /// <summary>v1~v4 저장 파일 복원 전용. 그 시절에는 카테고리당 아이템이 하나뿐이었으므로
        /// "착용 중이었다" = <b>그 카테고리의 기본 아이템(0번)</b>이다. 신규 4카테고리는 파일에 아예
        /// 없으므로 저장소가 미착용으로 넣는다(Core/CharacterSaveStore.cs).</summary>
        internal static void RestoreFromSave(EquipmentSlot slot, bool equipped)
        {
            if (!InRange(slot)) return;
            // ★ 여기서 0번은 이펙트에서는 <b>은퇴한 「없음」</b>이다 — 구버전 파일이 그 카드를
            //   되살리지 못하게 <see cref="IsRetiredItem"/>로 함께 막는다(위 v5 경로와 같은 판정).
            _worn[(int)slot] = equipped && !IsRetiredItem(slot, 0) ? 0 : NotWorn;
        }

        /// <summary>테스트/디버그 전용. <b>기본 차림</b>(모자/안경만 착용)으로 되돌린다 —
        /// "새 캐릭터를 방금 만든 상태"와 같아야 저장 없는 경로의 검증이 의미를 갖는다.
        ///
        /// <para>★ 2026-09-05 — <b>착용 변경 통지를 흘린다</b>(perf-doc 신고, qa-regression 등재).
        /// 이 메서드는 <c>_worn</c>을 통째로 갈아치우면서 <see cref="TryWear"/>가 하던 통지를 빼먹고 있었다.
        /// 그래서 <b>모델은 기본 차림인데 계층에는 직전 차림의 장비 선이 그대로 남는</b> 창이 열렸고,
        /// 씬을 다시 로드하는 테스트만 우연히 무사했다(그 우연이 유일한 방어였다).
        /// 통지를 여기서 흘리면 구독자 셋이 각자 알아서 따라온다 — 액세서리 렌더러/초상화는 서명을
        /// 무효화하고, 정보창은 열려 있을 때만 갱신한다.</para>
        ///
        /// <para>★ <b>변화가 있을 때만</b>이 아니라 <b>항상</b> 흘리는 이유:
        /// <see cref="RestoreFromSave(EquipmentSlot,string)"/>가 <b>조용히</b> <c>_worn</c>을 바꾼다
        /// (복원 중간 상태를 UI가 그리지 않도록 저장소가 끝에서 한 번만 통지하는 설계).
        /// 그 경로가 중간에 끊기면 "모델은 이미 기본값인데 계층만 낡은" 상태가 <b>실제로</b> 존재하고,
        /// 변화 감지로 막으면 정확히 그 상태를 못 고친다. 여기는 Update 경로가 아니라
        /// 여분의 통지 한 번이 비용이 아니다.</para></summary>
        public static void ResetForTesting()
        {
            int[] fresh = CreateDefaultWorn();
            for (int i = 0; i < SlotCount; i++) _worn[i] = fresh[i];
            StickmanEventBus.RaiseCharacterEquipmentChanged();
        }
    }
}
