using System.Collections.Generic;

namespace StickMate.Core
{
    /// <summary>보관함 항목의 종류. 지금은 둘뿐이고, 훗날 소모품/테마가 생기면 여기에 더한다.</summary>
    public enum ItemCategory
    {
        /// <summary>몸에 걸치는 것 — <see cref="EquipmentSlot"/> 하나에 대응한다.</summary>
        Equipment = 0,

        /// <summary>할 줄 아는 것(활쏘기/격파/그라피티…). 슬롯도 잠금도 없다.</summary>
        Action = 1,
    }

    /// <summary>
    /// 보관함 한 줄. <b>장비 항목은 이름/슬롯/해제 레벨을 자기가 들고 있지 않는다</b> —
    /// 전부 <see cref="EquipmentModel"/>에 위임한다(아래 클래스 문서의 "단일 소스" 항목).
    /// </summary>
    public sealed class ItemCatalogEntry
    {
        /// <summary>저장/로그/훗날의 상점 SKU가 쓸 안정적인 식별자. 표시 문자열과 분리한다 —
        /// 표시 이름은 문구 수정으로 언제든 바뀌지만 이 값은 바뀌면 안 된다.</summary>
        public readonly string Id;

        public readonly ItemCategory Category;

        /// <summary>장비면 대응 슬롯, 행동이면 null.</summary>
        public readonly EquipmentSlot? Slot;

        /// <summary>
        /// 플레이버 한 줄. 두 가지를 지킨다:
        ///  · <b>가짜 수치 금지</b>(방어력 +2 같은 것) — 이 앱에는 전투 스탯이 없다.
        ///  · <b>없는 효과 주장 금지</b> — 착용은 도형을 하나 더 그릴 뿐 포즈/자세에 아무 영향이 없다
        ///    (Interaction/CharacterAccessoryRenderer.cs 확인). "매면 자세가 곧아진다" 같은 문장은
        ///    원칙 1(행동-텍스트 싱크)을 그림 쪽에서 어기는 것이라 쓰지 않는다.
        ///  · 방해가 될 수 있는 행동에는 <b>탈출구를 반드시 명시</b>한다(원칙: 비침해/탈출구).
        /// </summary>
        public readonly string Description;

        /// <summary>보관함 목록 오른쪽 "상태 슬롯"에 들어갈 행동 전용 라벨(단축키 또는 "가끔 알아서").
        /// 장비는 착용/해제 상태에서 파생되므로 null이다.</summary>
        public readonly string ActionStatus;

        /// <summary>행동을 사용자가 직접 부를 수 있는가(단축키/메뉴가 있는가). 목록 정렬에 쓴다.</summary>
        public readonly bool IsDirectlyInvocable;

        private readonly string _actionDisplayName;

        private ItemCatalogEntry(string id, ItemCategory category, EquipmentSlot? slot,
            string actionDisplayName, string description, string actionStatus, bool directlyInvocable)
        {
            Id = id;
            Category = category;
            Slot = slot;
            _actionDisplayName = actionDisplayName;
            Description = description;
            ActionStatus = actionStatus;
            IsDirectlyInvocable = directlyInvocable;
        }

        internal static ItemCatalogEntry ForEquipment(string id, EquipmentSlot slot, string description)
            => new ItemCatalogEntry(id, ItemCategory.Equipment, slot, null, description, null, true);

        internal static ItemCatalogEntry ForAction(string id, string displayName, string shortcut, string description)
            => new ItemCatalogEntry(id, ItemCategory.Action, null, displayName, description,
                shortcut ?? AutoOnlyStatus, shortcut != null);

        /// <summary>단축키가 없는 행동(자율 발동 전용)의 상태 슬롯 문구.</summary>
        public const string AutoOnlyStatus = "가끔 알아서";

        /// <summary>장비 이름은 <see cref="EquipmentModel"/>에서만 나온다(이중 정의 금지).</summary>
        public string DisplayName => Slot.HasValue ? EquipmentModel.ItemName(Slot.Value) : _actionDisplayName;

        /// <summary>목록 한 줄에 들어갈 <b>첫 문장만</b>. 설명 전문은 아래 상세 카드가 보여준다 —
        /// 한 줄짜리 칸에 두 문장을 밀어 넣으면 두 번째 줄이 반쯤 잘려 지저분해진다(첫 육안 검증).</summary>
        public string ShortDescription
        {
            get
            {
                int end = Description.IndexOf('.');
                return end >= 0 ? Description.Substring(0, end + 1) : Description;
            }
        }

        /// <summary>목록의 부제 — 장비면 슬롯 이름("머리"), 행동이면 "행동".</summary>
        public string CategoryLabel => Slot.HasValue ? EquipmentModel.SlotName(Slot.Value) : "행동";

        /// <summary>장비면 해제 레벨, 행동이면 null(잠금 개념이 없다 — 단축키/메뉴로 항상 쓸 수 있다).</summary>
        public int? ResolveUnlockLevel(StickConfig config)
            => Slot.HasValue ? EquipmentModel.UnlockLevel(Slot.Value, config) : (int?)null;

        /// <summary>지금 이 항목을 가지고 있는가. 행동은 <b>항상 보유</b>, 장비는 레벨로 열린다.</summary>
        public bool IsOwned(StickConfig config)
            => !Slot.HasValue || EquipmentModel.IsUnlocked(Slot.Value, config);

        /// <summary>장비면서 지금 착용 중인가(보관함 목록의 상태 라벨용).</summary>
        public bool IsEquipped() => Slot.HasValue && EquipmentModel.IsEquipped(Slot.Value);

        /// <summary>목록 오른쪽 상태 슬롯 문구. 장비/행동이 <b>같은 자리</b>를 쓴다 —
        /// 훗날 여기에 가격표가 들어와도 레이아웃을 두 번 고치지 않게 하려는 의도(리더/디자이너 확정).</summary>
        public string ResolveStatusSlot(StickConfig config)
        {
            if (!Slot.HasValue) return ActionStatus;
            if (!IsOwned(config)) return $"Lv.{ResolveUnlockLevel(config)}에 열림";
            return IsEquipped() ? "착용 중" : "보유";
        }
    }

    /// <summary>
    /// ★ 보관함 카탈로그 — 2026-08-30 사용자 요청("탭을 하나 더 만들어서 가지고있는 아이템 장비들을
    /// 보여주면좋을듯. 장비나, 행동들.. 나중에 아이템으로 팔거니깐").
    ///
    /// ============================================================================
    /// 지금 이 카탈로그는 아무것도 팔지 않는다 (의도적)
    /// ============================================================================
    /// 결제 백엔드가 없다(스토어/영수증 검증/복원 어느 것도 이 프로젝트에 없다 —
    /// Core/EquipmentModel.cs의 "구매 → 레벨업 해제" 판단과 같은 근거). 그래서 보관함 탭에는
    /// <b>구매 버튼이 하나도 없다</b>. 이 파일의 목적은 훗날 판매를 얹을 때 <b>데이터 모양이 이미
    /// 맞아 있게</b> 하는 것 하나뿐이다: 안정적인 <see cref="ItemCatalogEntry.Id"/>, 카테고리,
    /// 표시 이름, 설명, (장비면) 슬롯/해제 레벨, 그리고 공통 <b>상태 슬롯</b>(가격표가 들어올 자리).
    ///
    /// ============================================================================
    /// 단일 소스 — 장비 4종을 여기에 다시 적지 않는다
    /// ============================================================================
    /// 리더 지시: "슬롯/이름/레벨을 두 곳에 따로 하드코딩하지 마라". 그래서 장비 엔트리가 들고 있는
    /// 것은 <b>슬롯 값과 플레이버 문장뿐</b>이고, 이름/슬롯이름/해제레벨은 호출 시점에
    /// <see cref="EquipmentModel"/>로 위임한다(회귀 테스트: Tests/EditMode/ItemCatalogTests.cs).
    ///
    /// ============================================================================
    /// 문구 원칙 (UX 디자이너가 실제 코드와 대조해 확정, 2026-08-30)
    /// ============================================================================
    ///  · <b>없는 효과를 주장하지 않는다</b>: 나비넥타이/망토 설명은 "자세가 곧아진다" 같은 문장을 쓰지
    ///    않는다 — 착용은 도형 하나를 더 그릴 뿐 포즈에 아무 영향이 없기 때문이다.
    ///  · <b>방해성 행동에는 탈출구를 명시한다</b>: 로데오 커서는 "커서를 흔들면 곧바로 떨어진다"처럼
    ///    빠져나오는 방법을 문장 안에 넣는다(Interaction/RodeoCursorWatcher.cs가 실제로 그렇게 동작).
    ///  · 톤은 Dialogue/AmbientChatter.cs와 같은 짧은 현재형 서술이다. <b>이 문자열은 대사가 아니다</b>
    ///    (DialogueIntent를 만들지 않는다) — 원칙 1의 적용 대상이 아니다.
    /// </summary>
    public static class ItemCatalog
    {
        private static readonly ItemCatalogEntry[] _entries =
        {
            // ---- 장비 4종(해제 레벨 순): 이름/슬롯/해제레벨은 EquipmentModel에서 온다. ----
            ItemCatalogEntry.ForEquipment("equip.head.cap", EquipmentSlot.Head,
                "장식 없는 천 모자. 챙은 항상 가는 쪽을 향한다."),
            ItemCatalogEntry.ForEquipment("equip.eyes.sunglasses", EquipmentSlot.Eyes,
                "실내에서도 벗지 않는다. 눈이 어디를 보는지 아무도 모른다."),
            ItemCatalogEntry.ForEquipment("equip.neck.bowtie", EquipmentSlot.Neck,
                "목에 걸친 단 하나의 격식. 좌우가 정확히 같다."),
            ItemCatalogEntry.ForEquipment("equip.shoulders.cape", EquipmentSlot.Shoulders,
                "늘 가는 방향의 반대쪽으로 늘어진다. 날지는 못한다."),

            // ---- 행동: 직접 부를 수 있는 것 먼저(단축키 순), 그다음 자율 발동 전용. ----
            ItemCatalogEntry.ForAction("action.archery", "활쏘기", "⌃⌥⌘A",
                "과녁을 세우고 세 발을 쏜다. 마지막 한 발은 언제나 한가운데다."),
            ItemCatalogEntry.ForAction("action.battle", "격파 놀이", "⌃⌥⌘K",
                "허공에 송판을 세우고 발차기 한 번. 부서지는 건 그려낸 송판뿐이다."),
            ItemCatalogEntry.ForAction("action.graffiti", "그라피티", "⌃⌥⌘G",
                "남의 창 위에 낙서를 한 장 남긴다. 잠시 뒤 저절로 옅어져 사라진다."),
            ItemCatalogEntry.ForAction("action.window_theft", "창 도둑", "⌃⌥⌘T",
                "창을 통째로 들고 달아나는 척한다. 진짜 창은 1픽셀도 움직이지 않는다."),
            ItemCatalogEntry.ForAction("action.rival_duel", "라이벌 대결", "⌃⌥⌘V",
                "붉은 녀석을 불러 한 판 붙는다. 이기면 경험치가 조금 붙는다."),
            ItemCatalogEntry.ForAction("action.rodeo_cursor", "로데오 커서", "⌃⌥⌘R",
                "가만히 멈춰 있는 커서에 올라탄다. 커서를 흔들면 곧바로 떨어진다."),
            ItemCatalogEntry.ForAction("action.window_crash", "창 부수기", "⌃⌥⌘X",
                "창에 금이 쫙 간 것처럼 보이게 한다. 금은 그림이고 클릭은 그대로 통과한다."),
            ItemCatalogEntry.ForAction("action.runaway", "가출", "⌃⌥⌘N",
                "삐지면 화면 밖으로 나가 버린다. 한 번 더 부르면 못 이기는 척 돌아온다."),
            ItemCatalogEntry.ForAction("action.focus_watch", "집중 모드", "⌃⌥⌘F",
                "타이머가 도는 동안 곁을 지킨다. 창을 자주 바꾸면 조용히 쳐다본다."),
            ItemCatalogEntry.ForAction("action.todo_reminder", "할일 알림", "⌃⌥⌘J",
                "적어둔 할일을 때가 되면 들고 온다. 재촉은 한 번뿐이다."),
            ItemCatalogEntry.ForAction("action.hardware_reaction", "하드웨어 반응", "⌃⌥⌘H",
                "이 컴퓨터가 더워지면 같이 더워한다. 표정만 바뀌고 아무것도 만지지 않는다."),
            ItemCatalogEntry.ForAction("action.desktop_tidy", "바탕화면 정리", null,
                "아이콘을 줄 맞춰 정리하는 시늉을 한다. 움직이는 건 복사본이고 진짜 아이콘은 그대로다."),
            ItemCatalogEntry.ForAction("action.blackhole", "블랙홀 소환", null,
                "화면 구석에 블랙홀을 그려 아이콘을 빨아들인다. 빨려 들어가는 건 전부 그림자다."),
        };

        public static IReadOnlyList<ItemCatalogEntry> Entries => _entries;

        public static int Count => _entries.Length;

        /// <summary>장비 항목 수(보관함 헤더 "걸치는 것 (2/4)"의 분모).</summary>
        public static int EquipmentCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Category == ItemCategory.Equipment) n++;
                }
                return n;
            }
        }

        /// <summary>행동 항목 수(보관함 헤더 "할 줄 아는 것 (13)").</summary>
        public static int ActionCount => _entries.Length - EquipmentCount;

        /// <summary>지금 해제된 장비 수(보관함 헤더의 분자).</summary>
        public static int UnlockedEquipmentCount(StickConfig config)
        {
            int n = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Category == ItemCategory.Equipment && _entries[i].IsOwned(config)) n++;
            }
            return n;
        }

        public static ItemCatalogEntry At(int index)
            => index >= 0 && index < _entries.Length ? _entries[index] : null;

        /// <summary>슬롯으로 장비 엔트리를 찾는다(장비 탭이 설명 카드를 채울 때 쓴다 — 설명 문장도
        /// 두 곳에 적히지 않게 이 카탈로그 하나에서만 나온다).</summary>
        public static ItemCatalogEntry FindBySlot(EquipmentSlot slot)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Slot.HasValue && _entries[i].Slot.Value == slot) return _entries[i];
            }
            return null;
        }
    }
}
