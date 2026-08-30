using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>아이콘 파츠의 종류(외부 핸드오프 <c>icon-paths.json</c>의 4종을 그대로 옮긴 것).</summary>
    public enum ItemIconPartKind
    {
        /// <summary>꺾은선 한 줄. 값 = x0,y0,x1,y1,… (닫힌 도형이면 마지막 점이 첫 점과 같다).</summary>
        Polyline = 0,

        /// <summary>테두리만 있는 원. 값 = cx,cy,r.</summary>
        Ring = 1,

        /// <summary>점선 원(FX "없음" 하나만 쓴다). 값 = cx,cy,r.</summary>
        DashedRing = 2,

        /// <summary>꽉 찬 점. 값 = cx,cy,r.</summary>
        Dot = 3,
    }

    /// <summary>
    /// 40×40 썸네일 아이콘의 한 조각. <b>좌표는 스펙의 SVG viewBox 그대로</b>(원점 좌상단, y가 아래로).
    /// 캐릭터 좌표계와 아무 관계가 없다 — 이건 카탈로그 썸네일이고, 몸에 붙는 도형은
    /// <c>Interaction/AccessoryShapeBuilder.cs</c>가 따로 정의한다.
    /// <para><b>2026-08-30 색 추가</b>: 사용자 지적("아이템들은 좀 어울리는 컬러로 디자인되어야함").
    /// 조각마다 <see cref="Color"/>를 들고 다닌다 — 32종 아이콘이 전부 한 가지 잉크색이면 카드 격자가
    /// 회색 벽으로 읽힌다. 다만 <b>잠긴 아이템의 색은 그리는 쪽이 무채색으로 덮어쓴다</b>(해금 전에
    /// 색을 미리 보여주면 잠금 연출의 의미가 사라진다). 즉 여기 색은 "해금됐을 때의 색"이다.</para>
    /// </summary>
    public readonly struct ItemIconPart
    {
        public readonly ItemIconPartKind Kind;

        /// <summary><see cref="ItemIconPartKind.Polyline"/>이면 점 좌표가 순서대로, 나머지는 cx,cy,r.
        /// 정적 초기화 때 한 번만 만들어지고 이후로는 읽기만 한다.</summary>
        public readonly float[] Values;

        /// <summary>해금 상태에서 이 조각을 칠할 색. <see cref="ItemCatalog"/>가 아이템마다
        /// 주색/보조색 두 개만 정하고 조각은 둘 중 하나를 고른다(색을 조각 수만큼 발명하지 않는다).</summary>
        public readonly Color Color;

        /// <summary>0 = 주색, 1 = 보조색. 색 자체가 아니라 <b>역할</b>을 적어 두는 이유는, 아이콘 표를
        /// 쓸 때 아직 색이 정해지지 않기 때문이다(<c>Tinted()</c>가 나중에 한 번에 채운다).</summary>
        public readonly byte Tone;

        public ItemIconPart(ItemIconPartKind kind, float[] values)
        {
            Kind = kind;
            Values = values;
            Color = Color.white;
            Tone = 0;
        }

        private ItemIconPart(ItemIconPartKind kind, float[] values, Color color, byte tone)
        {
            Kind = kind;
            Values = values;
            Color = color;
            Tone = tone;
        }

        /// <summary>보조색 역할로 표시한 사본.</summary>
        public ItemIconPart AsSecondary() => new ItemIconPart(Kind, Values, Color, 1);

        /// <summary>역할에 맞는 실제 색을 채운 사본.</summary>
        public ItemIconPart WithPalette(Color primary, Color secondary)
            => new ItemIconPart(Kind, Values, Tone == 0 ? primary : secondary, Tone);

        /// <summary>꺾은선의 점 개수.</summary>
        public int PointCount => Kind == ItemIconPartKind.Polyline && Values != null ? Values.Length / 2 : 0;
    }

    /// <summary>보관함 항목의 종류. 지금은 둘뿐이고, 훗날 소모품/테마가 생기면 여기에 더한다.</summary>
    public enum ItemCategory
    {
        /// <summary>몸에 걸치거나 몸의 일부가 되는 것 — <see cref="EquipmentSlot"/> 하나에 대응한다.
        /// 2026-08-30 32종 확장에서 <b>외형 계열</b>(머리/이펙트/펫)도 여기 들어왔다(아래
        /// <see cref="ItemCatalog"/> 문서의 "새 enum 값을 만들지 않은 이유" 참고).</summary>
        Equipment = 0,

        /// <summary>할 줄 아는 것(활쏘기/격파/그라피티…). 슬롯도 잠금도 없다.</summary>
        Action = 1,
    }

    /// <summary>
    /// 보관함 한 줄. 2026-08-30 32종 확장 전에는 장비 항목이 이름/해제레벨을 <see cref="EquipmentModel"/>에
    /// 위임했지만, 이제는 <b>반대 방향</b>이다 — 아이템 단위 사실(이름/설명/요구 레벨)은 전부 이 클래스가
    /// 들고 있고 <see cref="EquipmentModel"/>이 그것을 읽는다. 카테고리 단위 사실(카테고리 이름/슬롯 코드)만
    /// 여전히 <see cref="EquipmentModel"/>에서 온다. 방향이 뒤집힌 이유는 하나다: 요구 레벨이 카테고리당
    /// 1개에서 <b>아이템당 1개(32개)</b>가 되면서, 그 표를 둘 곳이 "카탈로그" 말고는 없어졌다.
    /// </summary>
    public sealed class ItemCatalogEntry
    {
        /// <summary>저장/로그/훗날의 상점 SKU가 쓸 안정적인 식별자. 표시 문자열과 분리한다 —
        /// 표시 이름은 문구 수정으로 언제든 바뀌지만 이 값은 바뀌면 안 된다.
        /// <b>저장 파일(v5)이 이 값을 그대로 적는다</b>(Core/CharacterSaveStore.cs).</summary>
        public readonly string Id;

        public readonly ItemCategory Category;

        /// <summary>장비면 대응 슬롯, 행동이면 null.</summary>
        public readonly EquipmentSlot? Slot;

        /// <summary>슬롯 안에서의 자리(0~3). 행동이면 -1. 런타임 착용 상태는 이 값으로 표현된다
        /// (문자열은 저장 파일과 상점 SKU 전용 — <see cref="EquipmentModel"/> 문서의 "인덱스 vs 아이디").</summary>
        public readonly int ItemIndex;

        /// <summary>
        /// 플레이버 한 줄. 두 가지를 지킨다:
        ///  · <b>가짜 수치 금지</b>(방어력 +2 같은 것) — 이 앱에는 전투 스탯이 없다.
        ///  · <b>없는 효과 주장 금지</b> — 착용은 도형을 하나 더 그릴 뿐 포즈/자세에 아무 영향이 없다.
        ///  · 방해가 될 수 있는 행동에는 <b>탈출구를 반드시 명시</b>한다(원칙: 비침해/탈출구).
        /// </summary>
        public readonly string Description;

        /// <summary>보관함 목록 오른쪽 "상태 슬롯"에 들어갈 행동 전용 라벨(단축키 또는 "가끔 알아서").
        /// 장비는 착용/해제 상태에서 파생되므로 null이다.</summary>
        public readonly string ActionStatus;

        /// <summary>행동을 사용자가 직접 부를 수 있는가(단축키/메뉴가 있는가). 목록 정렬에 쓴다.</summary>
        public readonly bool IsDirectlyInvocable;

        /// <summary>장비면 이 아이템을 보유하게 되는 레벨(1이면 처음부터 보유), 행동이면 null.</summary>
        public readonly int? RequiredLevel;

        /// <summary>카드 썸네일에 그릴 40×40 아이콘(장비 32종만 있고 행동은 <c>null</c>이다 — 행동은
        /// 카드가 아니라 목록 한 줄로 나온다). 그리는 방법은 Interaction/CharacterInfoWindow.cs.</summary>
        public readonly ItemIconPart[] Icon;

        /// <summary>이 아이템의 주색. <b>아이콘 조각에서 뽑아낸다</b> — 색 표를 따로 두면 카드와 몸이
        /// 다른 색을 쓰게 된다(2026-08-30 사용자 신고 "카드엔 색이 있는데 착용하면 색이 없다"의 뿌리).</summary>
        public readonly Color PrimaryColor;

        /// <summary>보조색(챙/방울/줄무늬/별 같은 "구별해 주는 한 부분"). 보조 조각이 없으면 주색과 같다.</summary>
        public readonly Color SecondaryColor;

        private readonly string _displayName;

        private ItemCatalogEntry(string id, ItemCategory category, EquipmentSlot? slot, int itemIndex,
            string displayName, string description, string actionStatus, bool directlyInvocable, int? requiredLevel,
            ItemIconPart[] icon)
        {
            Id = id;
            Category = category;
            Slot = slot;
            ItemIndex = itemIndex;
            _displayName = displayName;
            Description = description;
            ActionStatus = actionStatus;
            IsDirectlyInvocable = directlyInvocable;
            RequiredLevel = requiredLevel;
            Icon = icon;

            Color primary = ItemCatalog.InkTone, secondary = ItemCatalog.InkTone;
            bool gotPrimary = false, gotSecondary = false;
            if (icon != null)
            {
                for (int i = 0; i < icon.Length; i++)
                {
                    if (icon[i].Tone == 0)
                    {
                        if (gotPrimary) continue;
                        primary = icon[i].Color;
                        gotPrimary = true;
                    }
                    else if (!gotSecondary)
                    {
                        secondary = icon[i].Color;
                        gotSecondary = true;
                    }
                }
            }
            PrimaryColor = primary;
            SecondaryColor = gotSecondary ? secondary : primary;
        }

        internal static ItemCatalogEntry ForEquipment(string id, EquipmentSlot slot, int itemIndex,
            string displayName, string description, int requiredLevel, ItemIconPart[] icon)
            => new ItemCatalogEntry(id, ItemCategory.Equipment, slot, itemIndex, displayName, description,
                null, true, requiredLevel, icon);

        internal static ItemCatalogEntry ForAction(string id, string displayName, string shortcut, string description)
            => new ItemCatalogEntry(id, ItemCategory.Action, null, -1, displayName, description,
                shortcut ?? AutoOnlyStatus, shortcut != null, null, null);

        /// <summary>단축키가 없는 행동(자율 발동 전용)의 상태 슬롯 문구.</summary>
        public const string AutoOnlyStatus = "가끔 알아서";

        public string DisplayName => _displayName;

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

        /// <summary>목록의 부제 — 장비면 카테고리 이름("모자"), 행동이면 "행동".
        /// 카테고리 이름은 여전히 <see cref="EquipmentModel"/> 하나에서만 나온다.</summary>
        public string CategoryLabel => Slot.HasValue ? EquipmentModel.SlotName(Slot.Value) : "행동";

        /// <summary>장비면 보유 레벨, 행동이면 null(잠금 개념이 없다 — 단축키/메뉴로 항상 쓸 수 있다).
        /// <paramref name="config"/>는 더 이상 쓰이지 않는다(요구 레벨이 아이템 단위 상수 표로 옮겨갔다) —
        /// 호출부를 한 번에 갈아엎지 않으려고 시그니처만 남겨 뒀다.</summary>
        public int? ResolveUnlockLevel(StickConfig config) => RequiredLevel;

        /// <summary>지금 이 항목을 가지고 있는가. 행동은 <b>항상 보유</b>, 장비는 레벨로 열린다.</summary>
        public bool IsOwned(StickConfig config)
            => !RequiredLevel.HasValue || CharacterProgressionModel.Level >= RequiredLevel.Value;

        /// <summary>장비면서 <b>지금 이 아이템이</b> 착용 중인가(같은 카테고리의 다른 아이템이 착용
        /// 중이면 false — 카테고리당 하나만 걸칠 수 있다).</summary>
        public bool IsEquipped()
            => Slot.HasValue && EquipmentModel.WornIndex(Slot.Value) == ItemIndex;

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
    /// 보여주면좋을듯"), 같은 날 <b>외부 디자인 핸드오프에 맞춰 8카테고리 × 4아이템 = 32종으로 확장</b>,
    /// 그리고 같은 날 <b>표정(FACE) 카테고리 삭제로 7 × 4 = 28종</b>(사용자 결정, 아래 표 주석 참고).
    ///
    /// ============================================================================
    /// 지금 이 카탈로그는 아무것도 팔지 않는다 (의도적)
    /// ============================================================================
    /// 결제 백엔드가 없다(스토어/영수증 검증/복원 어느 것도 이 프로젝트에 없다). 그래서 보관함 탭에는
    /// <b>구매 버튼이 하나도 없다</b>. 이 파일의 목적은 훗날 판매를 얹을 때 <b>데이터 모양이 이미
    /// 맞아 있게</b> 하는 것 하나뿐이다: 안정적인 <see cref="ItemCatalogEntry.Id"/>, 카테고리,
    /// 표시 이름, 설명, (장비면) 슬롯/요구 레벨, 그리고 공통 <b>상태 슬롯</b>(가격표가 들어올 자리).
    ///
    /// ============================================================================
    /// 새 enum 값을 만들지 않은 이유 — 외형 계열도 <see cref="ItemCategory.Equipment"/>다
    /// ============================================================================
    /// 머리/이펙트/펫은 "몸에 걸치는 것"이라고 부르기 어색하지만, <b>데이터로서 하는 일이 모자와
    /// 완전히 같다</b>: 슬롯 하나를 차지하고, 레벨로 열리고, 카테고리당 하나만 고를 수 있고, 저장 파일에
    /// 아이디 하나로 적힌다. 새 enum 값(Appearance 등)을 만들면 IsOwned/ResolveStatusSlot/저장/마이그레이션이
    /// 전부 "둘 중 어느 쪽이냐"를 다시 물어야 하고, 그 분기마다 두 갈래가 <b>같은 코드를 두 벌</b> 갖게 된다.
    /// 실제로 갈라지는 것은 <b>그리는 방법</b>뿐인데(모자는 머리 위 도형, 펫은 따라다니는 개체), 그건
    /// 렌더러가 슬롯으로 분기할 문제이지 카탈로그 분류가 아니다. 대신 사람이 읽을 묶음은
    /// <see cref="EquipmentModel.IsAppearanceSlot"/>로 표현했다(UI 헤더 "장비 계열 / 외형 계열").
    ///
    /// ============================================================================
    /// 단일 소스 — 32종의 이름/설명/요구레벨은 오직 여기 한 곳
    /// ============================================================================
    /// 리더 지시: "슬롯/이름/레벨을 두 곳에 따로 하드코딩하지 마라". 확장 전에는 장비 이름이
    /// <see cref="EquipmentModel"/>에 있었지만, 아이템이 4개에서 32개가 되면서 그 표는 카탈로그로
    /// 옮겼다(회귀 테스트: Tests/EditMode/ItemCatalogTests.cs). <see cref="StickConfig"/>에 32개
    /// 필드를 늘어놓지 않은 이유도 같다 — 요구 레벨은 콘텐츠 설계이지 튜닝 노브가 아니고, 인스펙터에서
    /// 한 칸만 잘못 건드리면 저장 파일과 어긋난 채 조용히 굴러간다.
    ///
    /// ============================================================================
    /// 문구 원칙 (UX 디자이너가 실제 코드와 대조해 확정, 2026-08-30)
    /// ============================================================================
    ///  · <b>없는 효과를 주장하지 않는다</b>. · <b>방해성 행동에는 탈출구를 명시한다</b>.
    ///  · 톤은 Dialogue/AmbientChatter.cs와 같은 짧은 현재형 서술이다. <b>이 문자열은 대사가 아니다</b>
    ///    (DialogueIntent를 만들지 않는다) — 원칙 1의 적용 대상이 아니다.
    /// </summary>
    public static class ItemCatalog
    {
        /// <summary>표 한 줄. 배열 리터럴을 사람이 읽을 수 있게 유지하려고 만든 <b>표기용</b> 형식이고,
        /// 정적 초기화가 끝나면 <see cref="ItemCatalogEntry"/>로 바뀌어 밖으로 나가지 않는다.</summary>
        private readonly struct Row
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Description;
            public readonly int RequiredLevel;

            /// <summary>이 아이템의 썸네일 아이콘. 이름/설명/레벨과 <b>같은 줄</b>에 적는다 —
            /// 아이콘만 다른 표에 두면 한쪽에 아이템을 끼워 넣는 순간 짝이 조용히 어긋난다.</summary>
            public readonly ItemIconPart[] Icon;

            public Row(string id, string name, int requiredLevel, string description, ItemIconPart[] icon)
            {
                Id = id;
                Name = name;
                RequiredLevel = requiredLevel;
                Description = description;
                Icon = icon;
            }
        }

        // 아래 32개 리터럴이 쓰는 짧은 생성자. 이름을 한 글자로 줄이지 않은 이유는, 이 표를
        // 사람이 눈으로 스펙(icon-paths.json)과 대조할 일이 실제로 생기기 때문이다.
        private static ItemIconPart Stroke(params float[] xy) => new ItemIconPart(ItemIconPartKind.Polyline, xy);
        private static ItemIconPart Ring(float cx, float cy, float r)
            => new ItemIconPart(ItemIconPartKind.Ring, new[] { cx, cy, r });
        private static ItemIconPart DashedRing(float cx, float cy, float r)
            => new ItemIconPart(ItemIconPartKind.DashedRing, new[] { cx, cy, r });
        private static ItemIconPart Dot(float cx, float cy, float r)
            => new ItemIconPart(ItemIconPartKind.Dot, new[] { cx, cy, r });

        /// <summary>이 조각을 <b>보조색</b>으로 칠한다("A" = accent). 표에서 한 글자로 감싸면 어떤 조각이
        /// 강조인지 리터럴만 봐도 읽힌다.</summary>
        private static ItemIconPart A(ItemIconPart part) => part.AsSecondary();

        /// <summary>아이콘 한 벌에 주색/보조색을 한 번에 입힌다. 색을 조각마다 적지 않고 아이템마다
        /// 두 개만 적게 하는 장치다 — 32종 × 조각 수만큼 색을 고르기 시작하면 팔레트가 무너진다.</summary>
        private static ItemIconPart[] Tinted(Color primary, Color secondary, params ItemIconPart[] parts)
        {
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].WithPalette(primary, secondary);
            return parts;
        }

        // ============================================================================
        // ★ 아이템 소재 팔레트 (2026-08-30 사용자 지적 "어울리는 컬러로")
        // ============================================================================
        // 규칙 세 줄로 끝난다 — 색을 아이템마다 즉흥적으로 고르면 32칸 격자가 무지개가 된다.
        //  (1) 소재가 분명한 것(금/가죽/은/천/종이)은 그 소재색을 쓴다.
        //  (2) 소재가 없는 것(이펙트/펫)은 그 카테고리의 틴트(UiChrome.CategoryTint)와 같은 색상대에
        //      머문다. 새 색상대를 발명하지 않는다.
        //  (3) 보조색은 "이 아이템을 다른 셋과 구별해 주는 한 부분"에만 쓴다(챙/방울/줄무늬/별).
        // 값은 34-1 다크 팔레트 위에서 읽히도록 명도를 올려 잡았다(어두운 카드 위의 진한 색은 사라진다).
        private static Color Rgb(int hex)
            => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        /// <summary>"이 조각은 <b>캐릭터 잉크색 그대로</b> 칠하라"는 표식 색(작은 졸라맨처럼
        /// 몸의 일부로 읽혀야 하는 것). 카드 위에서는 이 값 자체로 그려지고, 몸 위에서는
        /// <see cref="WornColor"/>가 실제 잉크색으로 바꾼다 — 얼굴만 파랗게 물드는 사고를 막는다.</summary>
        public static readonly Color InkTone = Rgb(0xD6DBE3);

        /// <summary>흐린 잉크 표식. 취급은 <see cref="InkTone"/>과 같다.</summary>
        public static readonly Color InkDimTone = Rgb(0x8B939F);

        // 소재
        private static readonly Color Ivory = Rgb(0xE8E2D4);   // 천/캔버스(밝은)
        private static readonly Color Wool = Rgb(0xC08F60);    // 털실
        private static readonly Color Felt = Rgb(0x8C96A6);    // 펠트(중절모)
        private static readonly Color Gold = Rgb(0xE8C15A);    // 금
        private static readonly Color GoldLight = Rgb(0xFFF0B8);
        private static readonly Color Silver = Rgb(0xD3DAE4);  // 은/금속
        private static readonly Color DarkLens = Rgb(0x7C8AA3); // 짙은 남색 렌즈(어두운 카드 위 하한)
        private static readonly Color Leather = Rgb(0xC9744A);
        private static readonly Color Canvas = Rgb(0xC0925F);  // 배낭 천
        private static readonly Color Paper = Rgb(0xEEF2F8);   // 종이/깃털
        private static readonly Color Toy = Rgb(0xE0574F);     // 장난감 빨강

        // 카테고리 틴트(UiChrome.CategoryTint와 같은 값 — Core는 UI를 참조할 수 없어 값으로 못박는다.
        // 두 곳이 어긋나면 카드 테두리와 아이콘 색이 서로 다른 계열이 된다).
        private static readonly Color TintHead = Rgb(0xE8834A);
        private static readonly Color TintEyes = Rgb(0x4FC0C6);
        private static readonly Color TintNeck = Rgb(0x8CC06E);
        private static readonly Color TintBack = Rgb(0xB08FD0);
        private static readonly Color Accent = Rgb(0x5DA1F5);   // = UiChrome.Accent
        private static readonly Color Ink = InkTone;            // = UiChrome.IconInk
        private static readonly Color InkDim = InkDimTone;
        private static readonly Color NeckDeep = Rgb(0x6FA957);
        private static readonly Color BackDeep = Rgb(0x9A76BF);
        private static readonly Color HairBrown = Rgb(0xB8894F);

        // ============================================================================
        // ★ 아이콘 32종 (외부 핸드오프 data/icon-paths.json을 그대로 옮긴 것, 2026-08-30)
        // ============================================================================
        // 좌표계: 스펙 그대로 <b>40×40 viewBox, 원점 좌상단, y가 아래로</b> 증가한다. 우리 캐릭터
        // 좌표계(발바닥 원점, y 위)와 <b>무관</b>하다 — 이건 평면 썸네일이지 몸에 붙는 도형이 아니다.
        // 화면 좌표로의 뒤집기는 그리는 쪽(Interaction/CharacterInfoWindow.cs)이 한 곳에서 한다.
        //
        // SVG 문자열을 그대로 두지 않고 숫자로 옮긴 이유(33-7-5): d 문자열 파서를 새로 만드는 것은
        // 32개짜리 표 하나를 위해 새 버그 표면을 만드는 일이다. 곡선(q/a)은 미리 꺾은선으로
        // 샘플링했다 — 40×40에서 5점 꺾은선과 베지어는 육안으로 구분되지 않는다.
        //
        // ★ 스펙 이탈 1건: 스펙의 채움 도형(["f",d] — 선글라스 렌즈 2개)을 <b>닫힌 선</b>으로 그린다.
        //   이 프로젝트에는 채움 도형을 만드는 경로가 없고(모든 그림이 선화다), 억지로 만들면
        //   "한 자루 펜으로 그린 선화"라는 앱 전체의 문법에서 이 아이콘 하나만 벗어난다.
        //   ["fc"](채운 원)는 눈동자/방울/발자국처럼 <b>점</b>으로 읽혀야 하는 자리라 그대로 채운다.

        private static readonly ItemIconPart[] IconHeadCap = Tinted(Ivory, TintHead,
            Stroke(11f, 25f, 12.21f, 20.5f, 15.5f, 17.21f, 20f, 16f, 24.5f, 17.21f, 27.79f, 20.5f, 29f, 25f),
            A(Stroke(6f, 25f, 34f, 25f)),
            A(Stroke(29f, 25f, 36f, 25f, 35f, 27f, 29f, 27f)));

        private static readonly ItemIconPart[] IconHeadFur = Tinted(Wool, Ivory,
            Stroke(11f, 26f, 12.21f, 21.5f, 15.5f, 18.21f, 20f, 17f, 24.5f, 18.21f, 27.79f, 21.5f, 29f, 26f),
            Stroke(8f, 26f, 32f, 26f, 32f, 30f, 8f, 30f, 8f, 26f),
            A(Ring(20f, 10f, 2.6f)));

        private static readonly ItemIconPart[] IconHeadFedora = Tinted(Felt, TintHead,
            Stroke(13f, 23f, 16.5f, 18.12f, 20f, 16.5f, 23.5f, 18.12f, 27f, 23f),
            Stroke(17f, 15f, 19f, 16.33f, 21f, 16.33f, 23f, 15f),
            Stroke(5f, 24f, 12.5f, 26.25f, 20f, 27f, 27.5f, 26.25f, 35f, 24f),
            A(Stroke(13f, 22f, 16.5f, 23.12f, 20f, 23.5f, 23.5f, 23.12f, 27f, 22f)));

        private static readonly ItemIconPart[] IconHeadCrown = Tinted(Gold, GoldLight,
            A(Stroke(8f, 29f, 32f, 29f)),
            Stroke(8f, 29f, 9f, 14f, 14.5f, 21f, 20f, 11f, 25.5f, 21f, 31f, 14f, 32f, 29f));

        private static readonly ItemIconPart[] IconEyesSunglasses = Tinted(DarkLens, Silver,
            Stroke(5f, 17f, 17f, 17f, 18f, 18f, 18f, 22f, 17.56f, 24.22f, 16.22f, 25.56f, 14f, 26f, 9f, 26f, 6.78f, 25.56f, 5.44f, 24.22f, 5f, 22f, 5f, 18f, 5f, 17f),
            Stroke(23f, 17f, 35f, 17f, 35f, 22f, 34.56f, 24.22f, 33.22f, 25.56f, 31f, 26f, 26f, 26f, 23.78f, 25.56f, 22.44f, 24.22f, 22f, 22f, 22f, 18f, 23f, 17f),
            A(Stroke(18f, 19f, 22f, 19f)));

        private static readonly ItemIconPart[] IconEyesRound = Tinted(Silver, TintEyes,
            Ring(12f, 21f, 6f),
            Ring(28f, 21f, 6f),
            A(Stroke(18f, 21f, 22f, 21f)),
            A(Stroke(6f, 19f, 2f, 16f)),
            A(Stroke(34f, 19f, 38f, 16f)));

        private static readonly ItemIconPart[] IconEyesGoggles = Tinted(Silver, TintHead,
            Stroke(8f, 15f, 32f, 15f, 34.25f, 16f, 35f, 19f, 35f, 23f, 34.56f, 25.22f, 33.22f, 26.56f, 31f, 27f, 9f, 27f, 6.78f, 26.56f, 5.44f, 25.22f, 5f, 23f, 5f, 19f, 5.75f, 16f, 8f, 15f),
            Stroke(14f, 19f, 17f, 18.25f, 20f, 18f, 23f, 18.25f, 26f, 19f),
            A(Stroke(2f, 21f, 5f, 21f)),
            A(Stroke(35f, 21f, 38f, 21f)));

        private static readonly ItemIconPart[] IconEyesMonocle = Tinted(Gold, Silver,
            Ring(15f, 19f, 6.5f),
            A(Stroke(21f, 21f, 25.88f, 30.75f, 27f, 33f)),
            A(Stroke(8.5f, 19f, 4f, 17f)));

        private static readonly ItemIconPart[] IconNeckBowtie = Tinted(TintNeck, Ivory,
            Stroke(18f, 20f, 7f, 14f, 7f, 26f, 18f, 20f),
            Stroke(22f, 20f, 33f, 14f, 33f, 26f, 22f, 20f),
            A(Stroke(18f, 17f, 22f, 17f, 22f, 23f, 18f, 23f, 18f, 17f)));

        private static readonly ItemIconPart[] IconNeckStriped = Tinted(NeckDeep, Ivory,
            Stroke(16f, 8f, 24f, 8f, 22f, 13f, 18f, 13f, 16f, 8f),
            Stroke(18f, 13f, 22f, 13f, 25f, 27f, 20f, 33f, 15f, 27f, 18f, 13f),
            A(Stroke(15f, 20f, 25f, 16f)),
            A(Stroke(16f, 25f, 25f, 21f)));

        private static readonly ItemIconPart[] IconNeckScarf = Tinted(TintHead, Leather,
            Stroke(8f, 16f, 14f, 19f, 20f, 20f, 26f, 19f, 32f, 16f),
            Stroke(8f, 16f, 14f, 19.75f, 20f, 21f, 26f, 19.75f, 32f, 16f, 32f, 20f, 26f, 23f, 20f, 24f, 14f, 23f, 8f, 20f, 8f, 16f),
            A(Stroke(14f, 21f, 14f, 32f)),
            A(Stroke(19f, 23f, 19f, 32f)));

        private static readonly ItemIconPart[] IconNeckBell = Tinted(Leather, Gold,
            Stroke(8f, 13f, 14f, 17.5f, 20f, 19f, 26f, 17.5f, 32f, 13f),
            A(Stroke(20f, 22f, 17.22f, 22.56f, 15.56f, 24.22f, 15f, 27f, 25f, 27f, 24.44f, 24.22f, 22.78f, 22.56f, 20f, 22f)),
            A(Dot(20f, 29f, 1.8f)));

        private static readonly ItemIconPart[] IconBackCape = Tinted(TintBack, Ivory,
            A(Stroke(12f, 11f, 28f, 11f)),
            Stroke(13f, 12f, 9f, 26f, 14.5f, 27.5f, 20f, 28f, 25.5f, 27.5f, 31f, 26f, 27f, 12f));

        private static readonly ItemIconPart[] IconBackLongCape = Tinted(BackDeep, Ivory,
            A(Stroke(12f, 9f, 28f, 9f)),
            Stroke(13f, 10f, 7f, 32f, 13.5f, 33.88f, 20f, 34.5f, 26.5f, 33.88f, 33f, 32f, 27f, 10f),
            A(Stroke(20f, 12f, 20f, 30f)));

        private static readonly ItemIconPart[] IconBackWings = Tinted(Paper, TintBack,
            A(Stroke(20f, 12f, 20f, 28f)),
            Stroke(19f, 14f, 13.19f, 13.5f, 8.75f, 15f, 5.69f, 18.5f, 4f, 24f, 7.94f, 24.12f, 11.75f, 23.5f, 15.44f, 22.12f, 19f, 20f, 19f, 14f),
            Stroke(21f, 14f, 26.81f, 13.5f, 31.25f, 15f, 34.31f, 18.5f, 36f, 24f, 32.06f, 24.12f, 28.25f, 23.5f, 24.56f, 22.12f, 21f, 20f, 21f, 14f));

        private static readonly ItemIconPart[] IconBackBackpack = Tinted(Canvas, TintNeck,
            Stroke(11f, 14f, 29f, 14f, 30.5f, 14.75f, 31f, 17f, 31f, 30f, 29f, 32f, 11f, 32f, 9f, 30f, 9f, 17f, 9.5f, 14.75f, 11f, 14f),
            A(Stroke(15f, 14f, 17.5f, 11.38f, 20f, 10.5f, 22.5f, 11.38f, 25f, 14f)),
            A(Stroke(9f, 22f, 31f, 22f)),
            A(Stroke(17f, 26f, 23f, 26f)));

        private static readonly ItemIconPart[] IconHairCowlick = Tinted(HairBrown, TintEyes,
            Stroke(9f, 26f, 10.09f, 21.23f, 13.14f, 17.4f, 17.55f, 15.28f, 22.45f, 15.28f, 26.86f, 17.4f, 29.91f, 21.23f, 31f, 26f),
            A(Stroke(22f, 15f, 24.06f, 12.06f, 26.25f, 10.25f, 28.56f, 9.56f, 31f, 10f)));

        private static readonly ItemIconPart[] IconHairNeat = Tinted(HairBrown, TintEyes,
            Stroke(9f, 26f, 10.09f, 21.23f, 13.14f, 17.4f, 17.55f, 15.28f, 22.45f, 15.28f, 26.86f, 17.4f, 29.91f, 21.23f, 31f, 26f),
            A(Stroke(16f, 16f, 19.12f, 17.69f, 22.5f, 18.75f, 26.12f, 19.19f, 30f, 19f)));

        private static readonly ItemIconPart[] IconHairCurly = Tinted(HairBrown, TintEyes,
            Stroke(9f, 26f, 10.09f, 21.23f, 13.14f, 17.4f, 17.55f, 15.28f, 22.45f, 15.28f, 26.86f, 17.4f, 29.91f, 21.23f, 31f, 26f),
            A(Stroke(9f, 20f, 10.5f, 18.12f, 12f, 17.5f, 13.5f, 18.12f, 15f, 20f, 16.5f, 18.12f, 18f, 17.5f, 19.5f, 18.12f, 21f, 20f, 22.5f, 18.12f, 24f, 17.5f, 25.5f, 18.12f, 27f, 20f, 28.78f, 17.89f, 30.11f, 18.22f, 31f, 21f)));

        private static readonly ItemIconPart[] IconHairBald = Tinted(Ink, TintEyes,
            Stroke(9f, 27f, 10.09f, 22.23f, 13.14f, 18.4f, 17.55f, 16.28f, 22.45f, 16.28f, 26.86f, 18.4f, 29.91f, 22.23f, 31f, 27f),
            A(Stroke(15f, 17f, 17f, 15.38f, 19f, 14.5f, 21f, 14.38f, 23f, 15f)));

        private static readonly ItemIconPart[] IconFxNone = Tinted(InkDim, InkDim,
            Dot(20f, 20f, 2f),
            DashedRing(20f, 20f, 9f));

        private static readonly ItemIconPart[] IconFxFootprint = Tinted(TintNeck, TintNeck,
            Dot(10f, 27f, 2f),
            Dot(17f, 23f, 2f),
            Dot(24f, 19f, 2f),
            Dot(31f, 15f, 2f));

        private static readonly ItemIconPart[] IconFxSparkle = Tinted(Gold, TintNeck,
            Stroke(20f, 8f, 20f, 18f),
            Stroke(20f, 22f, 20f, 32f),
            Stroke(8f, 20f, 18f, 20f),
            Stroke(22f, 20f, 32f, 20f),
            A(Stroke(12f, 12f, 16f, 16f)),
            A(Stroke(28f, 12f, 24f, 16f)),
            A(Stroke(12f, 28f, 16f, 24f)),
            A(Stroke(28f, 28f, 24f, 24f)));

        private static readonly ItemIconPart[] IconFxDust = Tinted(InkDim, TintNeck,
            Stroke(10f, 26f, 7.96f, 22.78f, 8.79f, 19.06f, 12f, 17f, 14.32f, 13.58f, 18.16f, 12.06f, 22.19f, 12.98f, 25f, 16f, 29.12f, 16.05f, 32f, 19f, 31.95f, 23.12f, 29f, 26f, 10f, 26f),
            A(Stroke(8f, 30f, 17f, 30f)),
            A(Stroke(21f, 30f, 31f, 30f)));

        private static readonly ItemIconPart[] IconPetBall = Tinted(Toy, Paper,
            Ring(20f, 18f, 8f),
            A(Stroke(14f, 13f, 15.75f, 14.62f, 17f, 16.5f, 17.75f, 18.62f, 18f, 21f)),
            A(Stroke(11f, 31f, 15.5f, 32.12f, 20f, 32.5f, 24.5f, 32.12f, 29f, 31f)));

        private static readonly ItemIconPart[] IconPetPlane = Tinted(Paper, TintBack,
            Stroke(6f, 20f, 34f, 8f, 24f, 32f, 19f, 23f, 6f, 20f),
            A(Stroke(6f, 20f, 19f, 23f, 34f, 8f)));

        private static readonly ItemIconPart[] IconPetMini = Tinted(Ink, Ink,
            Ring(20f, 13f, 5f),
            Stroke(20f, 18f, 20f, 27f),
            Stroke(20f, 21f, 14f, 25f),
            Stroke(20f, 21f, 26f, 25f),
            Stroke(20f, 27f, 15f, 34f),
            Stroke(20f, 27f, 25f, 34f));

        private static readonly ItemIconPart[] IconPetCursor = Tinted(Accent, Accent,
            Stroke(13f, 7f, 13f, 30f, 19f, 24f, 23f, 33f, 27f, 31f, 23f, 22f, 31f, 22f, 13f, 7f));
        // ============================================================================
        // ★ 콘텐츠 표 (외부 디자인 핸드오프 2026-08-30 그대로) — 슬롯 순서 = EquipmentSlot 순서
        // ============================================================================
        // 요구 레벨 1 = "처음부터 보유"(핸드오프의 '기본'). 보유와 착용은 다른 사실이다 —
        // 시작 시 실제로 걸치고 있는 것은 모자/안경 둘뿐이다(EquipmentModel.CreateDefaultWorn).
        private static readonly Row[][] Table =
        {
            // ---- HEAD 모자 ----
            new[]
            {
                new Row("equip.head.cap", "천모자", 1, "장식 없는 천 모자. 챙은 항상 가는 쪽을 향한다.", IconHeadCap),
                new Row("equip.head.fur", "털모자", 5, "겨울에만 꺼내는 두꺼운 털모자.", IconHeadFur),
                new Row("equip.head.fedora", "중절모", 9, "어딘가 진지해 보이는 효과.", IconHeadFedora),
                new Row("equip.head.crown", "왕관", 20, "책상 위에서만 통용되는 권위.", IconHeadCrown),
            },
            // ---- EYES 안경 ----
            new[]
            {
                new Row("equip.eyes.sunglasses", "선글라스", 1, "실내에서도 벗지 않는다. 표정이 잘 안 보인다.", IconEyesSunglasses),
                new Row("equip.eyes.round", "동그란안경", 6, "시야가 조금 또렷해진다.", IconEyesRound),
                new Row("equip.eyes.goggles", "고글", 11, "뛸 때 눈이 시리지 않다.", IconEyesGoggles),
                new Row("equip.eyes.monocle", "외알안경", 15, "한쪽 눈만 진지하다.", IconEyesMonocle),
            },
            // ---- NECK 넥타이 ----
            new[]
            {
                new Row("equip.neck.bowtie", "나비넥타이", 1, "목에 걸친 단 하나의 격식.", IconNeckBowtie),
                new Row("equip.neck.striped", "줄무늬타이", 8, "월요일마다 조금 느슨해진다.", IconNeckStriped),
                new Row("equip.neck.scarf", "목도리", 12, "끝자락이 걸을 때마다 흔들린다.", IconNeckScarf),
                // ★ 문구 교체(2026-08-30 리더 승인): 원문은 "움직일 때 소리가 난다"였는데 이 프로젝트에는
                //   오디오 시스템이 <b>하나도 없다</b>(AudioSource/AudioClip/PlayOneShot 전수 검색 0건).
                //   없는 효과를 주장하지 않는다는 이 파일의 문구 원칙에 정면으로 걸린다. 방울 하나 때문에
                //   오디오 스택을 새로 들이는 것이 명백한 과잉이라, 실제로 보이는 사실로 바꿔 적는다.
                new Row("equip.neck.bell", "방울목걸이", 18, "걸을 때마다 방울이 흔들린다.", IconNeckBell),
            },
            // ---- BACK 망토 (슬롯 이름은 역사적 이유로 Shoulders, 핸드오프 코드는 BACK) ----
            new[]
            {
                new Row("equip.shoulders.cape", "짧은망토", 1, "늘 가는 방향의 반대쪽으로 날린다.", IconBackCape),
                new Row("equip.shoulders.long_cape", "긴망토", 13, "가끔 밟고 넘어진다.", IconBackLongCape),
                new Row("equip.shoulders.wings", "날개", 17, "뜨지는 않지만 폼은 난다.", IconBackWings),
                new Row("equip.shoulders.backpack", "배낭", 22, "뭘 넣는지는 아무도 모른다.", IconBackBackpack),
            },
            // ★ 2026-08-30 FACE(표정) 카테고리 삭제 — 사용자 결정("표정관련은 전부삭제 어차피 구별이
            //   안됨"). 표 순서 = EquipmentSlot 순서이므로 이 자리를 비우지 않고 <b>줄 자체를 지운다</b>.
            // ---- HAIR 머리 ----
            new[]
            {
                new Row("look.hair.cowlick", "삐친머리", 1, "한 가닥이 계속 서 있다.", IconHairCowlick),
                new Row("look.hair.neat", "단정한머리", 5, "아침에만 유지된다.", IconHairNeat),
                new Row("look.hair.curly", "곱슬", 9, "습도에 민감하다.", IconHairCurly),
                new Row("look.hair.bald", "민머리", 14, "바람의 저항이 적다.", IconHairBald),
            },
            // ---- FX 이펙트 ----
            new[]
            {
                new Row("look.fx.none", "없음", 1, "조용히 걸어다닌다.", IconFxNone),
                new Row("look.fx.footprint", "발자국", 6, "지나간 자리에 점이 남는다.", IconFxFootprint),
                new Row("look.fx.sparkle", "반짝임", 12, "가끔 빛난다.", IconFxSparkle),
                new Row("look.fx.dust", "먼지구름", 16, "뛸 때만 나타난다.", IconFxDust),
            },
            // ---- PET 펫 ----
            new[]
            {
                new Row("look.pet.ball", "작은공", 1, "굴러다니며 따라온다.", IconPetBall),
                new Row("look.pet.plane", "종이비행기", 13, "머리 위를 돈다.", IconPetPlane),
                new Row("look.pet.mini", "작은졸라맨", 19, "똑같이 생겼다.", IconPetMini),
                new Row("look.pet.cursor", "커서친구", 24, "마우스를 따라다닌다.", IconPetCursor),
            },
        };

        private static readonly ItemCatalogEntry[] _actions =
        {
            // 직접 부를 수 있는 것 먼저(단축키 순), 그다음 자율 발동 전용.
            ItemCatalogEntry.ForAction("action.archery", "활쏘기", "⌃⌥⌘A",
                "과녁을 세우고 세 발을 쏜다. 마지막 한 발은 언제나 한가운데다."),
            // ★ 2026-08-30 신규 등재 — 새 기능이 아니라 **이미 있던 기능의 누락 등재**다.
            //   Dialogue/AmbientChatter.cs(유휴/보행 중 확률 발화)와 그 강제 경로
            //   Interaction/AppControlDirector.ForceSayNow(단축키 B)가 Phase 3부터 살아 있었는데
            //   보관함 목록에만 빠져 있었다. 라이벌 대결 항목이 삭제되며 발견됐다.
            ItemCatalogEntry.ForAction("action.chatter", "혼잣말", "⌃⌥⌘B",
                "가만히 있거나 걷는 동안 가끔 혼자 중얼거린다. 단축키를 누르면 지금 당장 한마디 한다."),
            ItemCatalogEntry.ForAction("action.battle", "격파 놀이", "⌃⌥⌘K",
                "허공에 송판을 세우고 발차기 한 번. 부서지는 건 그려낸 송판뿐이다."),
            ItemCatalogEntry.ForAction("action.graffiti", "그라피티", "⌃⌥⌘G",
                "남의 창 위에 낙서를 한 장 남긴다. 잠시 뒤 저절로 옅어져 사라진다."),
            ItemCatalogEntry.ForAction("action.window_theft", "창 도둑", "⌃⌥⌘T",
                "창을 통째로 들고 달아나는 척한다. 진짜 창은 1픽셀도 움직이지 않는다."),
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
                // ★ 문구 교체(2026-08-30, ux-designer 지적 + 리더 승인): 원문은 "표정만 바뀌고"였는데
                //   Interaction/HardwareReactionRenderer.cs가 실제로 그리는 것은 얼굴이 아니라
                //   <b>머리 주변에 뜨는 이모트 아이콘</b>(배터리/와이파이/땀방울)이다. 이 앱에는 상태별
                //   표정 시스템 자체가 없다 — 있지도 않은 것을 설명이 주장하고 있었다.
                "이 컴퓨터가 더워지면 같이 더워한다. 머리 옆에 아이콘만 띄우고 아무것도 만지지 않는다."),
            ItemCatalogEntry.ForAction("action.desktop_tidy", "바탕화면 정리", null,
                "아이콘을 줄 맞춰 정리하는 시늉을 한다. 움직이는 건 복사본이고 진짜 아이콘은 그대로다."),
            ItemCatalogEntry.ForAction("action.blackhole", "블랙홀 소환", null,
                "화면 구석에 블랙홀을 그려 아이콘을 빨아들인다. 빨려 들어가는 건 전부 그림자다."),
        };

        /// <summary>슬롯별 아이템(표를 그대로 옮긴 것). 정적 초기화 때 <b>한 번만</b> 만든다 —
        /// 이 앱은 하루 종일 켜져 있고, 목록을 그릴 때마다 새로 만들면 매 프레임 할당이 된다.</summary>
        private static readonly ItemCatalogEntry[][] _bySlot = BuildBySlot();

        private static readonly ItemCatalogEntry[] _entries = BuildFlat();

        private static ItemCatalogEntry[][] BuildBySlot()
        {
            var bySlot = new ItemCatalogEntry[Table.Length][];
            for (int s = 0; s < Table.Length; s++)
            {
                Row[] rows = Table[s];
                var items = new ItemCatalogEntry[rows.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    items[i] = ItemCatalogEntry.ForEquipment(rows[i].Id, (EquipmentSlot)s, i,
                        rows[i].Name, rows[i].Description, rows[i].RequiredLevel, rows[i].Icon);
                }
                bySlot[s] = items;
            }
            return bySlot;
        }

        private static ItemCatalogEntry[] BuildFlat()
        {
            int equipmentCount = 0;
            for (int s = 0; s < _bySlot.Length; s++) equipmentCount += _bySlot[s].Length;

            var flat = new ItemCatalogEntry[equipmentCount + _actions.Length];
            int w = 0;
            for (int s = 0; s < _bySlot.Length; s++)
            {
                for (int i = 0; i < _bySlot[s].Length; i++) flat[w++] = _bySlot[s][i];
            }
            for (int i = 0; i < _actions.Length; i++) flat[w++] = _actions[i];
            return flat;
        }

        public static IReadOnlyList<ItemCatalogEntry> Entries => _entries;

        public static int Count => _entries.Length;

        /// <summary>카테고리 수(= <see cref="EquipmentSlot"/> 값의 개수). 표가 진짜 소스라서
        /// <see cref="EquipmentModel.SlotCount"/>가 이 값을 검증한다.</summary>
        public static int SlotCount => _bySlot.Length;

        /// <summary>이 카테고리의 아이템 수(지금은 전부 4).</summary>
        public static int ItemCountIn(EquipmentSlot slot)
        {
            int s = (int)slot;
            return s >= 0 && s < _bySlot.Length ? _bySlot[s].Length : 0;
        }

        /// <summary>카테고리 안의 아이템 목록(정보창 카테고리 패널이 그대로 순회한다).</summary>
        public static IReadOnlyList<ItemCatalogEntry> ItemsIn(EquipmentSlot slot)
        {
            int s = (int)slot;
            return s >= 0 && s < _bySlot.Length ? _bySlot[s] : System.Array.Empty<ItemCatalogEntry>();
        }

        public static ItemCatalogEntry Item(EquipmentSlot slot, int itemIndex)
        {
            int s = (int)slot;
            if (s < 0 || s >= _bySlot.Length) return null;
            ItemCatalogEntry[] items = _bySlot[s];
            return itemIndex >= 0 && itemIndex < items.Length ? items[itemIndex] : null;
        }

        /// <summary>아이디로 카테고리 안의 자리를 찾는다. 없으면 -1 —
        /// 저장 파일이 <b>모르는 아이디</b>를 담고 있을 때(구버전에서 지워진 아이템, 손상) 쓰는 유일한 경로.</summary>
        public static int IndexOfItemId(EquipmentSlot slot, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            int s = (int)slot;
            if (s < 0 || s >= _bySlot.Length) return -1;

            ItemCatalogEntry[] items = _bySlot[s];
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].Id == itemId) return i;
            }
            return -1;
        }

        /// <summary>장비 항목 수(보관함 헤더 "걸치는 것 (9/32)"의 분모).</summary>
        public static int EquipmentCount
        {
            get
            {
                int n = 0;
                for (int s = 0; s < _bySlot.Length; s++) n += _bySlot[s].Length;
                return n;
            }
        }

        /// <summary>행동 항목 수(보관함 헤더 "할 줄 아는 것 (13)").</summary>
        public static int ActionCount => _actions.Length;

        /// <summary>지금 보유한 장비 수(보관함 헤더의 분자).</summary>
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

        /// <summary>이 카테고리에서 <b>지금 화면이 대표로 보여줄</b> 아이템 — 착용 중이면 그것,
        /// 미착용이면 첫 아이템. (착용 중인 것이 없다고 설명 카드를 비우면 "뭘 고를 수 있는지"가
        /// 사라진다 — 잠긴 카드도 회색으로 계속 보여주는 것과 같은 이유.)</summary>
        public static ItemCatalogEntry FindBySlot(EquipmentSlot slot)
        {
            int worn = EquipmentModel.WornIndex(slot);
            return Item(slot, worn >= 0 ? worn : 0);
        }

        // ============================================================================
        // ★ 착용 색 (2026-08-30 사용자 신고 2건 동시 대응)
        //   ① "모자랑 이런건 색이 들어가있는데 실제 적용시 왜 색상적용이 안됨?"
        //   ② "아이템 적용시 그림 자체가 구분이 잘안감"
        // ============================================================================
        // 카드 썸네일 색을 <b>그대로</b> 몸에 칠하면 ①은 풀리지만 ②는 오히려 남는다. 카드 색은
        // 34-1 다크 카드 위에서 읽히도록 명도를 올려 잡은 값이라 흰 잉크 캐릭터 위에서는
        // 아이보리(0xE8E2D4)·종이(0xEEF2F8)·은(0xD3DAE4)이 전부 <b>흰색과 구분되지 않는다</b>
        // (실측: 착용 스크린샷에서 머리 원·털모자·나비넥타이가 흰 덩어리 하나로 뭉쳐 보였다).
        //
        // 그래서 색은 카탈로그에서 오되, 몸 위에서는 두 가지 하한을 강제한다. 새 색표를 만들지
        // 않으므로 "카드와 몸이 다른 색"이라는 이중 정의는 생기지 않는다 — 같은 색의 <b>착용 형태</b>다.
        //   · 채도 하한 — 잉크는 언제나 무채색(흰/검)이다. 채도가 있으면 잉크와 절대 안 섞인다.
        //   · 명도 창(0.55~0.80) — 흰 잉크(V=1)와도 검은 잉크(V=0)와도 충분히 벌어지고,
        //     밝은 바탕화면/어두운 바탕화면 양쪽에서 사라지지 않는 중간 대역이다.
        private const float WornSaturationFloor = 0.42f;
        private const float WornValueFloor = 0.55f;
        private const float WornValueCeiling = 0.80f;

        /// <summary>카탈로그 색 -> 몸에 칠할 색. <see cref="InkTone"/>/<see cref="InkDimTone"/>는
        /// "잉크 그대로"라는 표식이므로 변환하지 않고 <paramref name="ink"/>를 돌려준다.</summary>
        public static Color WornColor(Color catalogColor, Color ink)
        {
            if (IsInkTone(catalogColor)) return ink;

            Color.RGBToHSV(catalogColor, out float h, out float s, out float v);
            s = Mathf.Max(s, WornSaturationFloor);
            v = Mathf.Clamp(v, WornValueFloor, WornValueCeiling);
            Color result = Color.HSVToRGB(h, s, v);
            result.a = ink.a;
            return result;
        }

        private static bool IsInkTone(Color c)
            => Approximately(c, InkTone) || Approximately(c, InkDimTone);

        private static bool Approximately(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        /// <summary>지금 이 자리의 아이템을 <b>몸에 그릴 때</b> 쓸 두 색. 아이템을 못 찾으면 둘 다 잉크색이다
        /// (표가 늘어났는데 도형이 아직 없는 경우에도 예전과 같은 그림이 나온다).</summary>
        public static void ResolveWornPalette(EquipmentSlot slot, int itemIndex, Color ink,
            out Color primary, out Color secondary)
        {
            ItemCatalogEntry entry = Item(slot, itemIndex);
            if (entry == null)
            {
                primary = ink;
                secondary = ink;
                return;
            }
            primary = WornColor(entry.PrimaryColor, ink);
            secondary = WornColor(entry.SecondaryColor, ink);
        }

        /// <summary>아이디로 항목 하나(장비/행동 모두). 없으면 null.</summary>
        public static ItemCatalogEntry FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Id == id) return _entries[i];
            }
            return null;
        }
    }
}
