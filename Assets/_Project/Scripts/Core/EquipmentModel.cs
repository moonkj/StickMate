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
        /// 쓴다. 데이터로서의 취급은 두 계열이 완전히 같다(Core/ItemCatalog.cs 문서 참고).</summary>
        public static bool IsAppearanceSlot(EquipmentSlot slot) => (int)slot >= FirstAppearanceSlot;

        // ==================== 아이템 단위 사실 — 전부 ItemCatalog에 위임 ====================

        public static int ItemCount(EquipmentSlot slot) => ItemCatalog.ItemCountIn(slot);

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

        /// <summary>보유한 아이템 중 첫 자리(없으면 -1). <see cref="TryToggle"/>가 "일단 하나 걸쳐라"에 쓴다.</summary>
        public static int FirstOwnedItemIndex(EquipmentSlot slot)
        {
            int count = ItemCount(slot);
            for (int i = 0; i < count; i++)
            {
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

            if (itemIndex == NotWorn)
            {
                if (_worn[(int)slot] == NotWorn) return false;
                _worn[(int)slot] = NotWorn;
                StickmanEventBus.RaiseCharacterEquipmentChanged();
                return true;
            }

            if (itemIndex < 0 || itemIndex >= ItemCount(slot)) return false;
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
            _worn[(int)slot] = ItemCatalog.IndexOfItemId(slot, itemId);
        }

        /// <summary>v1~v4 저장 파일 복원 전용. 그 시절에는 카테고리당 아이템이 하나뿐이었으므로
        /// "착용 중이었다" = <b>그 카테고리의 기본 아이템(0번)</b>이다. 신규 4카테고리는 파일에 아예
        /// 없으므로 저장소가 미착용으로 넣는다(Core/CharacterSaveStore.cs).</summary>
        internal static void RestoreFromSave(EquipmentSlot slot, bool equipped)
        {
            if (!InRange(slot)) return;
            _worn[(int)slot] = equipped ? 0 : NotWorn;
        }

        /// <summary>테스트/디버그 전용. <b>기본 차림</b>(모자/안경만 착용)으로 되돌린다 —
        /// "새 캐릭터를 방금 만든 상태"와 같아야 저장 없는 경로의 검증이 의미를 갖는다.</summary>
        public static void ResetForTesting()
        {
            int[] fresh = CreateDefaultWorn();
            for (int i = 0; i < SlotCount; i++) _worn[i] = fresh[i];
        }
    }
}
