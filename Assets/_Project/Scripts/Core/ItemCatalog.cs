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

        /// <summary>★ <b>채운</b> 다각형. 값은 <see cref="Polyline"/>과 같고(마지막 점 = 첫 점),
        /// 그리는 쪽이 윤곽선 위에 면을 채운다.
        /// <para>2026-09-02에 생겼다. 그 전까지 폴백 형식에는 <b>채운 면이 없어서</b>, 몸 도형의
        /// 좌표를 그대로 옮겨도 폴백만 "속 빈 윤곽선"이 됐다 — v2 스펙 원칙 2("채움이 덩어리를
        /// 만든다")를 폴백이 <b>원리적으로</b> 표현할 수 없었다는 뜻이다.
        /// 그리는 코드는 <c>CharacterInfoWindow.BuildIcon</c>, 면 그래픽은
        /// 카드 본경로가 이미 쓰는 <c>AccessoryFillGraphic</c>을 그대로 쓴다(분할을 두 벌 만들지 않는다).</para></summary>
        Polygon = 4,
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

        /// <summary>에셋(<see cref="AccessoryDefSO"/>)에서 값을 되살릴 때 쓰는 완전 생성자.
        /// 어셈블리 밖으로는 열지 않는다 — 색/역할을 임의로 지어내는 경로를 만들지 않기 위해서다.</summary>
        internal ItemIconPart(ItemIconPartKind kind, float[] values, Color color, byte tone)
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
        public int PointCount => HasPoints && Values != null ? Values.Length / 2 : 0;

        /// <summary>좌표가 <b>점 목록</b>인가(원처럼 cx,cy,r가 아니라). 새 종류가 생길 때마다
        /// 호출부 여러 곳에서 <c>== Polyline</c>을 각자 고치다 빠뜨리는 것을 막는다.</summary>
        public bool HasPoints => Kind == ItemIconPartKind.Polyline || Kind == ItemIconPartKind.Polygon;
    }

    /// <summary>보관함 항목의 종류. 지금은 둘뿐이고, 훗날 소모품/테마가 생기면 여기에 더한다.</summary>
    public enum ItemCategory
    {
        /// <summary>몸에 걸치거나 몸의 일부가 되는 것 — <see cref="EquipmentSlot"/> 하나에 대응한다.
        /// 2026-08-30 32종 확장에서 <b>외형 계열</b>(머리/이펙트/펫)도 여기 들어왔다(아래
        /// <see cref="ItemCatalog"/> 문서의 "새 enum 값을 만들지 않은 이유" 참고).</summary>
        Equipment = 0,

        /// <summary>할 줄 아는 것(활쏘기/그라피티/창 도둑…). 슬롯도 잠금도 없다.</summary>
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

        /// <summary>
        /// ★ <b>등급 순위를 매기는 모집단</b>의 식별자. 기본 42종은 전부
        /// <see cref="ItemCatalog.BaseCohortId"/>이고, DLC 팩이 오면 팩마다 다른 값을 받는다.
        ///
        /// <para><b>왜 이게 필요한가</b>(2026-09-02 game-architect 지적, 리더 재확인): 등급은
        /// <c>requiredLevel</c>의 <b>슬롯 내 순위</b>에서 파생되는데, 모집단을 "슬롯에 로드된 것 전부"로
        /// 잡으면 <c>Resources.LoadAll</c>이 팩 애셋까지 같은 배열에 꽂는 순간
        /// <b>기본 42종의 등급이 통째로 미끄러진다</b>. 실측: 슬롯이 6종에서 12종이 되면
        /// rank5가 <b>전설 → 희귀</b>, rank4가 <b>영웅 → 희귀</b>, rank2·3이 <b>희귀 → 일반</b>이 된다
        /// (18종이면 여섯 칸이 전부 일반이다). 그러면 <c>ECONOMY_SPEC</c>의 가격·유예·페이투윈 검산이
        /// 전부 <c>count = 6</c> 위에서 계산됐으므로 함께 무너지고,
        /// <b>"캡 20은 기본 42종만으로 도달"이라는 사용자 확정 차단선이 아무도 안 건드렸는데 깨진다.</b></para>
        ///
        /// <para><b>지금은 팩이 0개라 코호트 == 슬롯이고 값이 한 개도 안 바뀐다.</b> 그래서 지금 고치면
        /// 회귀 위험이 0이고, 팩을 출하한 뒤에 고치면 <b>완료된 결제 아래에서 상품 정의가 바뀐다</b>.</para>
        ///
        /// <para>★ <b>2026-09-02 배선 완료.</b> 값은 <see cref="AccessoryDefSO.cohortId"/>가 실어 오고
        /// 변환은 <see cref="ItemCatalog.EntryFrom"/> 한 곳에서만 한다. 그 전까지는 이 필드가
        /// <b>자리만 열린 상태</b>였고 <c>ForEquipment</c>가 언제나 <see cref="ItemCatalog.BaseCohortId"/>로
        /// 폴백했다 — 즉 <b>팩 에셋을 넣는 순간 위 붕괴가 조용히 일어나는 상태</b>였다.
        /// 그래서 <c>ForEquipment</c>의 기본 인자도 함께 없앴다: 코호트는 부르는 쪽이
        /// <b>매번 명시적으로 정하는 사실</b>이지, 잊으면 알아서 기본값이 되는 값이 아니다.</para>
        /// </summary>
        internal readonly int CohortId;

        /// <summary>
        /// ★ 이 아이템이 등급을 <b>선언</b>했는가, 했다면 무엇으로. <see cref="Core.DeclaredRarity.Derived"/>면
        /// 선언하지 않은 것이고 등급은 코호트 순위에서 파생된다(기본 42종 전부).
        ///
        /// <para><b>선언이 있으면 <see cref="ItemCatalog.RarityOfMember"/>는 순위 계산을 아예 하지 않는다.</b>
        /// 코호트 필터만으로는 부족하기 때문이다 — 코호트 크기가 1이면
        /// <c>step = rank × 6 ÷ 1</c> 에서 <c>rank</c>가 0이라 <b>무조건 일반</b>이 나온다.
        /// 선언값을 비율 환산에 통과시키면 그 자리에서 뭉개진다.</para>
        ///
        /// <para>타입이 <see cref="ItemRarity"/>가 <b>아닌</b> 이유(직렬화 기본값 0 = 일반)는
        /// <see cref="Core.DeclaredRarity"/> 문단에 실측과 함께 있다.</para>
        /// </summary>
        internal readonly DeclaredRarity Declared;

        private readonly string _displayName;

        private ItemCatalogEntry(string id, ItemCategory category, EquipmentSlot? slot, int itemIndex,
            string displayName, string description, string actionStatus, bool directlyInvocable, int? requiredLevel,
            ItemIconPart[] icon, int cohortId, DeclaredRarity declared)
        {
            Id = id;
            Category = category;
            Slot = slot;
            ItemIndex = itemIndex;
            CohortId = cohortId;
            Declared = declared;
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

        /// <summary>장비 한 종. <paramref name="cohortId"/>는 <b>등급 순위의 모집단</b>이고
        /// 기본 42종은 전부 <see cref="ItemCatalog.BaseCohortId"/>다(<see cref="CohortId"/> 참조).
        /// <para>★ <b>기본 인자를 두지 않는다.</b> 잊고 안 넘기면 조용히 기본 코호트에 합류하고,
        /// 그 증상은 팩을 <b>안 산 사람</b>의 등급이 내려가는 형태로 나타난다 — 컴파일러가 물어보게 둔다.
        /// <paramref name="declared"/>도 같은 이유로 기본 인자가 없다. 다만 이쪽은 잊었을 때의 기본값이
        /// <see cref="Core.DeclaredRarity.Derived"/>(= 안전)라 <paramref name="cohortId"/>와 <b>방향이 반대</b>다 —
        /// 그래도 기본 인자를 안 두는 이유는, 안전한 쪽이라도 <b>말없이 정해지는 사실</b>을 남기지 않기
        /// 위해서다. 팩이 선언을 빠뜨린 것은 침묵이 아니라 결함이고, 감사가 그걸 잡는다.</para></summary>
        internal static ItemCatalogEntry ForEquipment(string id, EquipmentSlot slot, int itemIndex,
            string displayName, string description, int requiredLevel, ItemIconPart[] icon,
            int cohortId, DeclaredRarity declared)
            => new ItemCatalogEntry(id, ItemCategory.Equipment, slot, itemIndex, displayName, description,
                null, true, requiredLevel, icon, cohortId, declared);

        internal static ItemCatalogEntry ForAction(string id, string displayName, string shortcut, string description)
            => new ItemCatalogEntry(id, ItemCategory.Action, null, -1, displayName, description,
                shortcut ?? AutoOnlyStatus, shortcut != null, null, null, ItemCatalog.BaseCohortId,
                DeclaredRarity.Derived);

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

        /// <summary>지금 이 항목을 가지고 있는가. 행동은 <b>항상 보유</b>, 장비는 레벨로 열린다.
        /// <para><see cref="EquipmentDebugUnlock.UnlockAll"/>(QA 해금 스위치, 릴리스 빌드에서는 빌드
        /// 구성상 자동으로 꺼진다)이 켜져 있으면 레벨을
        /// 보지 않는다(사용자 QA 요청). 규칙을 지운 것이 아니라 <b>앞에 스위치 하나를 둔 것</b>이고,
        /// 이 자리에 둔 이유는 여기가 카드 색·상태 문구·착용 가능 여부의 공통 뿌리이기 때문이다 —
        /// 더 아래(착용 시점)에서 우회하면 "Lv.20에 열림"이라 적힌 카드가 눌리는 거짓말이 된다.</para></summary>
        public bool IsOwned(StickConfig config)
            => !RequiredLevel.HasValue
               || EquipmentDebugUnlock.UnlockAll
               || CharacterProgressionModel.Level >= RequiredLevel.Value;

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
    /// 그리고 같은 날 <b>표정(FACE) 카테고리 삭제로 7 × 4 = 28종</b>(사용자 결정, 아래 표 주석 참고),
    /// 2026-09-01 <b>카테고리당 +2종으로 7 × 6 = 42종</b>(캐러셀 도입에 맞춘 확장 — 신규 14종은 임시 플레이스홀더).
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
    /// 단일 소스 — 28종의 이름/설명/요구레벨은 이제 <b>에셋</b>이다 (2026-08-31 A단계)
    /// ============================================================================
    /// 리더 지시: "슬롯/이름/레벨을 두 곳에 따로 하드코딩하지 마라". 확장 전에는 장비 이름이
    /// <see cref="EquipmentModel"/>에 있었고, 32종 확장에서 이 파일의 표로 옮겼다. 그리고
    /// <b>2026-08-31 DLC 이행 A단계에서 그 표가 이 파일을 떠났다</b> —
    /// <c>Assets/_Project/Resources/Items/*.asset</c>(<see cref="AccessoryDefSO"/> 28개)가 주인이고
    /// 이 클래스는 그것을 읽는 파사드다. 이유는 원칙 4다: 표가 코드 안에 있으면 DLC 팩마다
    /// 기본 로직 파일을 고쳐야 한다(docs/ARCHITECTURE.md 5-3).
    /// <b>주인은 여전히 하나</b>라는 성질은 그대로다 — 옮겨간 곳이 코드에서 에셋으로 바뀌었을 뿐이다.
    /// 회귀 잠금: Tests/EditMode/ItemCatalogTests.cs + ItemCatalogAssetParityTests.cs(골든 대조).
    /// <see cref="StickConfig"/>에 28개 필드를 늘어놓지 않은 이유는 그대로다 — 요구 레벨은 콘텐츠
    /// 설계이지 튜닝 노브가 아니다.
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
        // ============================================================================
        // ★ 표는 이제 코드가 아니라 에셋이다 (DLC 이행 A단계, docs/ARCHITECTURE.md 5-3-3)
        // ============================================================================
        // 2026-08-31까지 이 자리에는 `new Row(...)` 28줄과 아이콘 좌표 리터럴 150여 줄이 있었다.
        // 그 구조에서는 DLC 팩 하나를 붙일 때마다 <b>이 파일을 고쳐야</b> 했고, 그것이 원칙 4
        // ("신규 콘텐츠는 기본 로직 무수정")를 선언만 남기고 무력화하고 있었다. 이제 28종은
        // Assets/_Project/Resources/Items 아래 AccessoryDefSO 에셋 28개이고, 이 클래스는 그것을 읽어
        // <b>예전과 똑같은 모양</b>으로 내주는 파사드다(공개 API는 한 줄도 바뀌지 않았다).
        //
        // 옮기면서 값이 하나도 안 바뀌었다는 증거:
        //   Tests/EditMode/Golden/ItemCatalogGolden.txt  = 카탈로그 전문(전환 직전 28종 + 행동 13종에서
        //     출발해, 그 뒤의 실제 카탈로그 변경을 그대로 반영한다 — 2026-09-02 격파 놀이 삭제로 행동 12종)
        //   Tests/EditMode/ItemCatalogAssetParityTests.cs = 지금 카탈로그를 같은 형식으로 찍어 완전 대조
        // 좌표 한 칸, 색 한 채널만 흔들려도 빨개진다.
        //
        // Addressables/팩 매니페스트는 <b>여기 없다</b> — C단계 전까지는 평범한 Resources다(같은 문서).
        private const string ItemResourceFolder = "Items";

        // 성공/실패를 가리지 않고 <b>한 번만</b> 읽고 캐시한다. 실패했다고 매 접근마다 다시 읽으면
        // 고장난 빌드에서 LoadAll이 프레임마다 도는 최악이 된다(하루 종일 켜 두는 앱이다).
        // 대신 무엇이 왜 비었는지는 Debug.LogError가 한 번 크게 남긴다.
        private static ItemCatalogEntry[][] _bySlot;
        private static ItemCatalogEntry[] _entries;

        /// <summary>★ 2026-09-02 B-2 파일럿 — <b>몸에 붙는 형상</b>을 자리/번호로 찾는 표.
        /// <c>ItemCatalogEntry</c>에 얹지 않은 이유는, 카드/보관함이 쓰는 <b>표시용 한 줄</b>과
        /// 렌더러가 쓰는 <b>기하</b>가 수명도 소비자도 다르기 때문이다(엔트리는 골든 덤프에도 실린다).
        /// 값은 여전히 같은 에셋 하나에서 온다 — 이음매가 늘어난 것이 아니라 창구가 둘일 뿐이다.</summary>
        private static AccessoryWornShapeData[][][] _wornBySlot;

        private static ItemCatalogEntry[][] BySlot
        {
            get { EnsureLoaded(); return _bySlot; }
        }

        private static ItemCatalogEntry[] AllEntries
        {
            get { EnsureLoaded(); return _entries; }
        }

        /// <summary>에셋 -> 런타임 표. 정적 필드 초기화자로 두지 않는 이유는 <c>Resources.LoadAll</c>이
        /// 도메인 리로드/직렬화 도중에 부르면 안 되는 API여서다 — "타입을 건드리는 순간"이 아니라
        /// "실제로 목록을 쓰는 순간"까지 미룬다.</summary>
        private static void EnsureLoaded()
        {
            if (_bySlot != null) return;

            // 칸 수는 콘텐츠가 아니라 <b>enum이 정하는 사실</b>이다(EquipmentSlot 값이 7개다).
            // 에셋이 통째로 사라져도 카테고리 개수는 흔들리지 않아야 UI가 칸을 잃지 않는다.
            const int slots = EquipmentModel.SlotCount;

            AccessoryDefSO[] defs = Resources.LoadAll<AccessoryDefSO>(ItemResourceFolder);

            // 검사는 <b>한 번만</b> 한다 — 두 패스에서 각각 부르면 같은 에러가 두 줄씩 찍힌다.
            var placeable = new bool[defs.Length];
            var counts = new int[slots];
            for (int i = 0; i < defs.Length; i++)
            {
                placeable[i] = IsPlaceable(defs[i]);
                if (!placeable[i]) continue;

                int s = (int)defs[i].slot;
                if (defs[i].itemIndex + 1 > counts[s]) counts[s] = defs[i].itemIndex + 1;
            }

            var bySlot = new ItemCatalogEntry[slots][];
            var wornBySlot = new AccessoryWornShapeData[slots][][];
            for (int s = 0; s < slots; s++)
            {
                bySlot[s] = new ItemCatalogEntry[counts[s]];
                wornBySlot[s] = new AccessoryWornShapeData[counts[s]][];
            }

            for (int i = 0; i < defs.Length; i++)
            {
                if (!placeable[i]) continue;
                AccessoryDefSO def = defs[i];

                ItemCatalogEntry[] row = bySlot[(int)def.slot];
                if (row[def.itemIndex] != null)
                {
                    Debug.LogError($"[ItemCatalog] {def.slot} 카테고리 {def.itemIndex}번 자리를 두 아이템이 " +
                        $"다툽니다: '{row[def.itemIndex].Id}' vs '{def.itemId}'. 자리 번호는 도형" +
                        "(AccessoryShapeBuilder)이 그림을 고르는 값이라 겹치면 엉뚱한 것이 그려집니다.");
                    continue;
                }

                row[def.itemIndex] = EntryFrom(def);
                wornBySlot[(int)def.slot][def.itemIndex] = AcceptWornShapes(def);
            }

            if (defs.Length == 0)
            {
                // 카테고리별로 7줄을 쏟아내 봐야 원인은 하나다 — 한 줄만 크게 남긴다.
                Debug.LogError($"[ItemCatalog] Resources/{ItemResourceFolder} 에서 아이템 에셋을 하나도 " +
                    "찾지 못했습니다. 보관함이 통째로 비고 착용 복원이 전부 실패합니다.");
                _bySlot = bySlot;
                _wornBySlot = wornBySlot;
                _entries = BuildFlat(bySlot);
                return;
            }

            // 구멍(중간 번호가 빈 것)과 빈 카테고리만 여기서 잡을 수 있다.
            // <b>못 잡는 것</b>: 카테고리의 <b>마지막</b> 번호가 통째로 사라진 경우 — 자리 수를 에셋에서
            // 세기 때문에 그냥 "원래 3종이었다"로 보인다. 카테고리마다 몇 종이어야 하는지는 데이터에
            // 없는 사실이고, 그걸 여기 적으면 방금 코드 밖으로 꺼낸 표를 다시 코드에 적는 셈이다.
            // 그 검사는 EditMode 테스트(7×6 = 42종)가 맡고, 팩 단위 선언은 C단계 매니페스트가 맡는다.
            for (int s = 0; s < slots; s++)
            {
                if (bySlot[s].Length == 0)
                {
                    Debug.LogError($"[ItemCatalog] {(EquipmentSlot)s} 카테고리에 아이템 에셋이 하나도 " +
                        $"없습니다(Resources/{ItemResourceFolder}). 보관함에 빈 카테고리가 그대로 보입니다.");
                    continue;
                }

                for (int i = 0; i < bySlot[s].Length; i++)
                {
                    if (bySlot[s][i] != null) continue;
                    Debug.LogError($"[ItemCatalog] {(EquipmentSlot)s} 카테고리 {i}번 자리의 아이템 에셋이 " +
                        $"없습니다(Resources/{ItemResourceFolder}). 뒤 번호가 앞으로 당겨지지 않으므로 " +
                        "보관함에 빈 칸이 생기고, 그 자리를 저장 파일이 가리키면 복원에 실패합니다.");
                }
            }

            // ★ 등급 선언 감사 — 잘못 만든 팩은 <b>여기서</b> 걸린다. 조용히 지나가면 증상이
            //   "팩을 안 산 사람의 등급이 내려감" 또는 "한 팩 안에서 등급이 갈림"으로 나타나는데,
            //   둘 다 화면만 봐서는 원인을 못 찾는다. 판정 규칙은 AuditDeclarations 한 곳에만 있다.
            var faults = new List<string>();
            for (int s = 0; s < slots; s++) AuditDeclarations(bySlot[s], faults);
            for (int f = 0; f < faults.Count; f++) Debug.LogError($"[ItemCatalog] {faults[f]}");

            _bySlot = bySlot;
            _wornBySlot = wornBySlot;
            _entries = BuildFlat(bySlot);
        }

        /// <summary>
        /// ★ <b>몸에 붙는 형상</b>을 자리/번호로 돌려준다. 없으면 <c>null</c>이고, 그것은
        /// "그 자리는 아직 코드가 그린다" 또는 "에셋이 잘못됐다" 둘 중 하나다 — 후자는 로드할 때
        /// 이미 <see cref="AcceptWornShapes"/>가 큰 소리로 남겼다.
        /// <para>돌려주는 배열을 <b>복사하지 않는다</b>. 렌더러가 재구성마다 부르는 경로라
        /// 복사하면 프레임마다 쓰레기가 생긴다. 대신 소비자는 <b>읽기만</b> 한다
        /// (<c>AccessoryShapeBuilder.AppendWorn</c>이 유일한 소비자다).</para>
        /// </summary>
        public static AccessoryWornShapeData[] WornShapes(EquipmentSlot slot, int itemIndex)
        {
            EnsureLoaded();
            int s = (int)slot;
            if (_wornBySlot == null || s < 0 || s >= _wornBySlot.Length) return null;

            AccessoryWornShapeData[][] row = _wornBySlot[s];
            if (row == null || itemIndex < 0 || itemIndex >= row.Length) return null;
            return row[itemIndex];
        }

        /// <summary>
        /// ★ 에셋 하나 -> 카탈로그 항목 하나. <b>변환은 여기 한 곳뿐이다.</b>
        ///
        /// <para><c>EnsureLoaded</c> 안에 인라인으로 두지 않은 이유는 <b>검증 가능성</b>이다:
        /// 그 자리는 <c>Resources.LoadAll</c>이 물고 있어 테스트가 값을 넣어 볼 수 없고, 실제로
        /// 그 사각지대에서 <see cref="AccessoryDefSO.cohortId"/>가 <b>안 넘어가는 채로</b> 지나갔다
        /// (기본 42종은 전부 기본 코호트라 <b>어떤 초록도 갈라지지 않았다</b> — 팩을 넣어야 증상이 난다).
        /// 이제 <c>ItemRarityDerivationTests</c>가 코호트를 바꾼 def를 여기 직접 먹여 대조한다.</para>
        /// </summary>
        internal static ItemCatalogEntry EntryFrom(AccessoryDefSO def)
            => ItemCatalogEntry.ForEquipment(def.itemId, def.slot, def.itemIndex,
                def.displayName, def.description, def.requiredLevel, def.BuildIcon(), def.cohortId,
                def.declaredRarity);

        /// <summary>
        /// 에셋의 형상 스트림을 <b>한 번</b> 검사한다. 통과한 것만 표에 올린다.
        /// <para>여기서 거르는 이유는 렌더 루프에서 거르면 <b>프레임마다</b> 같은 에러가 찍혀
        /// 로그가 원인을 덮기 때문이고, 조용히 버리지 않는 이유는 DLC 팩이 잘못 만들어졌을 때
        /// 증상이 "아이템이 그냥 안 보임"이면 아무도 원인을 못 찾기 때문이다(이 파일의 다른 검사와 같다).</para>
        /// </summary>
        private static AccessoryWornShapeData[] AcceptWornShapes(AccessoryDefSO def)
        {
            AccessoryWornShapeData[] shapes = def.wornShapes;
            if (shapes == null || shapes.Length == 0) return null;

            for (int i = 0; i < shapes.Length; i++)
            {
                if (AccessoryWornShapeReader.Validate(shapes[i], out string error)) continue;

                Debug.LogError($"[ItemCatalog] '{def.itemId}'의 형상 {i}번" +
                    $"('{shapes[i].name}') 스트림이 문법에 맞지 않습니다: {error} " +
                    "이 아이템은 몸에 아무것도 그리지 못하고 '빠진 도형' 표식이 대신 뜹니다.");
                return null;
            }
            return shapes;
        }

        /// <summary>표에 놓을 수 있는 에셋인가. 놓을 수 없는 것은 <b>조용히 버리지 않는다</b> —
        /// DLC 팩이 잘못 만들어졌을 때 증상이 "아이템이 그냥 안 보임"이면 아무도 원인을 못 찾는다.</summary>
        private static bool IsPlaceable(AccessoryDefSO def)
        {
            if (def == null) return false;

            if (string.IsNullOrEmpty(def.itemId))
            {
                Debug.LogError($"[ItemCatalog] 아이템 에셋 '{def.name}'에 itemId가 없습니다 " +
                    "(저장 파일이 적을 값이라 비어 있으면 착용을 복원할 수 없습니다).");
                return false;
            }
            if ((int)def.slot < 0 || (int)def.slot >= EquipmentModel.SlotCount)
            {
                Debug.LogError($"[ItemCatalog] '{def.itemId}'의 카테고리 값 {(int)def.slot}이 범위를 벗어납니다.");
                return false;
            }
            if (def.itemIndex < 0)
            {
                Debug.LogError($"[ItemCatalog] '{def.itemId}'의 자리 번호 {def.itemIndex}가 음수입니다.");
                return false;
            }
            return true;
        }

        private static ItemCatalogEntry[] BuildFlat(ItemCatalogEntry[][] bySlot)
        {
            int equipmentCount = 0;
            for (int s = 0; s < bySlot.Length; s++)
            {
                for (int i = 0; i < bySlot[s].Length; i++)
                {
                    if (bySlot[s][i] != null) equipmentCount++;
                }
            }

            var flat = new ItemCatalogEntry[equipmentCount + _actions.Length];
            int w = 0;
            for (int s = 0; s < bySlot.Length; s++)
            {
                for (int i = 0; i < bySlot[s].Length; i++)
                {
                    if (bySlot[s][i] != null) flat[w++] = bySlot[s][i];
                }
            }
            for (int i = 0; i < _actions.Length; i++) flat[w++] = _actions[i];
            return flat;
        }

        // ============================================================================
        // ★ 아이템 소재 팔레트 — 규칙은 여기, 값은 에셋 (2026-08-31 A단계)
        // ============================================================================
        // 색 상수 표(Ivory/Wool/Gold/…)는 아이콘 리터럴과 함께 에셋으로 내려갔다. 하지만 <b>규칙</b>은
        // 코드에도 문서에도 남아야 한다 — 값만 옮기고 규칙을 지우면 다음 DLC 팩이 무지개가 된다.
        //  (1) 소재가 분명한 것(금/가죽/은/천/종이)은 그 소재색을 쓴다.
        //      Ivory #96814F · Wool #BA7636 · Felt #5577AE · Gold #9B7922 / GoldLight #988540 ·
        //      Silver #587398 · DarkLens #5075B5 · Leather #BA5928 · Canvas #AB7942 · Paper #6787B9 ·
        //      Toy #C6443C · HairBrown #A16A28
        //  (2) 소재가 없는 것(이펙트/펫)은 그 카테고리의 틴트(UiChrome.CategoryTint)와 같은 색상대에
        //      머문다. 새 색상대를 발명하지 않는다.
        //      TintHead #CC5512 · TintEyes #20878C · TintNeck #5A8C3C / NeckDeep #428C24 ·
        //      TintBack #955CCC · Accent #3378CC
        //  (3) 보조색은 "이 아이템을 다른 셋과 구별해 주는 한 부분"에만 쓴다(챙/방울/줄무늬/별).
        //
        // ★ 2026-09-02 자립 대역 이행 — 색 이름은 그대로, 값은 전부 내려왔다.
        //   옛 값은 "34-1 다크 카드 위에서 읽히도록" 명도를 올려 잡은 것이었다. 그런데 이 색들이
        //   실제로 놓이는 배경은 카드만이 아니다 — <b>밝은 바탕화면 · 어두운 바탕화면 · 종이 무대
        //   #E9EAE6 · 목탄 무대 #25282E</b> 넷이고, 넷 모두에서 비텍스트 하한 3.0을 넘어야 한다.
        //   넷의 교집합이 상대휘도 <b>L ∈ [0.1632, 0.2396]</b>(design/art/PALETTE_SPEC.md §0)이고,
        //   옛 27색 중 <b>20색이 WornColor를 통과한 뒤에도 그 밖</b>이었다(최악 #FFF0B8 -> 몸
        //   #CCBA76 = 1.60:1, 종이 무대). 밝은 배경과 어두운 배경 양쪽을 동시에 만족시키는 것은
        //   중간 밝기 배경이라 <b>극단보다 어렵다</b> — 대비는 자기 휘도에 가까운 배경에서 0으로 간다.
        //   그래서 색상각(=아이템 정체성)은 고정한 채 휘도만 대역 안으로 옮겼다(최대 이동 0.59도).
        //   덤으로 새 27색은 전부 <b>WornColor 항등</b>이다 — 카드 색과 몸 색이 바이트 단위로 같다.
        //   회귀 잠금: Tests/EditMode/ItemPaletteBandGateTests.
        //
        // ★ 2026-09-02 2차 — 위 이행이 <b>휘도만</b> 옮기느라 채도를 눌러, 한 아이템 안에서 맞닿는
        //   주색↔보조색 37쌍 중 3쌍이 변별 하한 ΔE 7.8 아래로 내려갔다(목도리 줄무늬 3.61 ·
        //   반다나 매듭 4.93). 25색 300쌍 요약 통계에는 안 잡히는 회귀다 — 그 셋은 "5~7 구간의
        //   미묘한 쌍"에 묻힌다. 위 여섯 색만 채도를 규칙 목표 둘레 ±0.10에서 다시 풀어 되살렸다
        //   (목도리 14.36 · 반다나 13.75, 미달 1/37). 색상각 이동은 8bit 반올림뿐(최대 0.85도)이고
        //   대역·항등·배경 하한은 그대로다. 판정 근거: design/art/PALETTE_SPEC.md §10.
        //
        // 아래 두 잉크 표식만 코드에 남는다 — 이건 팔레트가 아니라 "잉크색을 따르라"는 <b>지시</b>라서
        // 런타임(WornColor)이 값으로 비교한다. 대역 규칙의 <b>유일한 면제 대상</b>이기도 하다:
        // 몸 위에서는 이 값이 아니라 캐릭터 잉크색으로 칠해지므로 잴 대상이 애초에 없다.
        private static Color Rgb(int hex)
            => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        /// <summary>"이 조각은 <b>캐릭터 잉크색 그대로</b> 칠하라"는 표식 색(작은 졸라맨처럼
        /// 몸의 일부로 읽혀야 하는 것). 카드 위에서는 이 값 자체로 그려지고, 몸 위에서는
        /// <see cref="WornColor"/>가 실제 잉크색으로 바꾼다 — 얼굴만 파랗게 물드는 사고를 막는다.</summary>
        public static readonly Color InkTone = Rgb(0xD6DBE3);

        /// <summary>흐린 잉크 표식. 취급은 <see cref="InkTone"/>과 같다.</summary>
        public static readonly Color InkDimTone = Rgb(0x8B939F);

        private static readonly ItemCatalogEntry[] _actions =
        {
            // 직접 부를 수 있는 것 먼저(단축키 순), 그다음 자율 발동 전용.
            ItemCatalogEntry.ForAction("action.archery", "활쏘기", ShortcutLabel.Chord("A"),
                "과녁을 세우고 세 발을 쏜다. 마지막 한 발은 언제나 한가운데다."),
            // ★ 2026-08-30 신규 등재 — 새 기능이 아니라 **이미 있던 기능의 누락 등재**다.
            //   Dialogue/AmbientChatter.cs(유휴/보행 중 확률 발화)와 그 강제 경로
            //   Interaction/AppControlDirector.ForceSayNow(단축키 B)가 Phase 3부터 살아 있었는데
            //   보관함 목록에만 빠져 있었다. 라이벌 대결 항목이 삭제되며 발견됐다.
            ItemCatalogEntry.ForAction("action.chatter", "혼잣말", ShortcutLabel.Chord("B"),
                "가만히 있거나 걷는 동안 가끔 혼자 중얼거린다. 단축키를 누르면 지금 당장 한마디 한다."),
            ItemCatalogEntry.ForAction("action.graffiti", "그라피티", ShortcutLabel.Chord("G"),
                "남의 창 위에 낙서를 한 장 남긴다. 잠시 뒤 저절로 옅어져 사라진다."),
            ItemCatalogEntry.ForAction("action.window_theft", "창 도둑", ShortcutLabel.Chord("T"),
                "창을 통째로 들고 달아나는 척한다. 진짜 창은 1픽셀도 움직이지 않는다."),
            ItemCatalogEntry.ForAction("action.rodeo_cursor", "로데오 커서", ShortcutLabel.Chord("R"),
                "가만히 멈춰 있는 커서에 올라탄다. 커서를 흔들면 곧바로 떨어진다."),
            ItemCatalogEntry.ForAction("action.window_crash", "창 부수기", ShortcutLabel.Chord("X"),
                "창에 금이 쫙 간 것처럼 보이게 한다. 금은 그림이고 클릭은 그대로 통과한다."),
            ItemCatalogEntry.ForAction("action.runaway", "가출", ShortcutLabel.Chord("N"),
                "삐지면 화면 밖으로 나가 버린다. 한 번 더 부르면 못 이기는 척 돌아온다."),
            ItemCatalogEntry.ForAction("action.focus_watch", "집중 모드", ShortcutLabel.Chord("F"),
                "타이머가 도는 동안 곁을 지킨다. 창을 자주 바꾸면 조용히 쳐다본다."),
            ItemCatalogEntry.ForAction("action.todo_reminder", "할일 알림", ShortcutLabel.Chord("J"),
                "적어둔 할일을 때가 되면 들고 온다. 재촉은 한 번뿐이다."),
            ItemCatalogEntry.ForAction("action.hardware_reaction", "하드웨어 반응", ShortcutLabel.Chord("H"),
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

        public static IReadOnlyList<ItemCatalogEntry> Entries => AllEntries;

        public static int Count => AllEntries.Length;

        /// <summary>카테고리 수(= <see cref="EquipmentSlot"/> 값의 개수). 표가 진짜 소스라서
        /// <see cref="EquipmentModel.SlotCount"/>가 이 값을 검증한다.</summary>
        public static int SlotCount => BySlot.Length;

        /// <summary>이 카테고리의 아이템 수. <b>가변값이다</b> — 코드가 4나 6이라고 적어 두면
        /// 에셋을 늘리는 순간 그 뒤가 조용히 사라진다(정보창 카드 풀이 이 값을 그대로 센다).</summary>
        public static int ItemCountIn(EquipmentSlot slot)
        {
            int s = (int)slot;
            return s >= 0 && s < BySlot.Length ? BySlot[s].Length : 0;
        }

        /// <summary>카테고리 안의 아이템 목록(정보창 카테고리 패널이 그대로 순회한다).</summary>
        public static IReadOnlyList<ItemCatalogEntry> ItemsIn(EquipmentSlot slot)
        {
            int s = (int)slot;
            return s >= 0 && s < BySlot.Length ? BySlot[s] : System.Array.Empty<ItemCatalogEntry>();
        }

        public static ItemCatalogEntry Item(EquipmentSlot slot, int itemIndex)
        {
            int s = (int)slot;
            if (s < 0 || s >= BySlot.Length) return null;
            ItemCatalogEntry[] items = BySlot[s];
            return itemIndex >= 0 && itemIndex < items.Length ? items[itemIndex] : null;
        }

        // ============================================================================
        // ★ 등급 (2026-09-02) — design/systems/ECONOMY_SPEC.md §3-2 · design/art/PALETTE_SPEC.md §14-2
        // ============================================================================
        //
        // <b>저장 필드가 0개다.</b> 등급은 슬롯 안에서 requiredLevel 순위(rank)로부터 파생된다.
        // 애셋에 새 필드를 만들지 않으므로 세이브 스키마도 .asset도 한 바이트 안 바뀐다.
        // 그래서 이 이음매는 재화·상점보다 <b>앞에</b> 붙을 수 있다(리더 착수 순서 2번).
        //
        // 왜 순위인가: 카탈로그 42종은 슬롯마다 requiredLevel이 오름차순이라(Head 1·5·9·20·23·26 등)
        // 이 규칙이 <b>"레벨이 오를수록 스탯이 절대 내려가지 않는다"를 자동으로 보장</b>한다.
        // 등급을 애셋 필드로 두면 그 단조성이 사람 손에 맡겨지고, 어긋나는 순간 유저에게는
        // 디자인이 아니라 버그로 읽힌다(ECONOMY_SPEC §3-2의 왕관/밀짚모자 사례).
        //
        // ★ 여기에 색이 없는 것은 의도다. 등급색은 창 안(카드 크롬)에서만 살고
        //   <c>UiChrome.RarityColor</c>가 유일한 출처다 — 몸에 칠하면 배경 4종 최악 1.73:1로
        //   사라지고 희귀↔영웅이 ΔE 5.77로 붙는다(PALETTE_SPEC §12-2 실측).

        /// <summary>
        /// ★ <b>기본 코호트</b> — 기본 42종이 속한 모집단. DLC 팩은 팩마다 다른 값을 받는다.
        /// <para>등급은 이 모집단 <b>안에서의</b> 순위로 정해진다. 모집단을 "슬롯에 로드된 것 전부"로
        /// 잡으면 팩 하나가 붙는 순간 기본 42종의 등급이 통째로 미끄러진다
        /// (<see cref="ItemCatalogEntry.CohortId"/>에 실측과 근거가 있다).</para>
        /// </summary>
        internal const int BaseCohortId = 0;

        /// <summary>
        /// ★ <b>DLC 팩이 선언할 수 있는 등급 상한</b>(DS-2 단일 등급 상한, 사용자 확정 페이투윈 차단선).
        ///
        /// <para><see cref="DeclaredRarity.Epic"/>·<see cref="DeclaredRarity.Legendary"/>는 <b>타입에는
        /// 있지만</b> 여기서 막힌다. 타입에서 지우지 않는 이유: 지우면 차단선이 「값이 없어서」가 되고,
        /// 나중에 누가 단을 되살리는 순간 <b>아무 경보 없이</b> 열린다. 상수로 막으면 그 자리에
        /// 이유가 남고, 정책이 바뀔 때 <b>바꿀 곳이 한 군데</b>다.</para>
        ///
        /// <para>왜 희귀인가: 팩은 <b>현금 전용</b>이고 기본 42종은 동전으로도 살 수 있다
        /// (2026-09-02 사용자 확정). 팩이 영웅·전설을 낼 수 있으면 <b>돈으로만 닿는 상단</b>이 생기고,
        /// 그것이 곧 페이투윈이다. 희귀는 기본 42종에도 14개 있으므로 <b>지불로 열리는 새 상단이 없다</b>.</para>
        ///
        /// <para>★ 테스트는 이 상수를 <b>참조</b>한다. 숫자를 베끼면 정책이 바뀔 때 프로덕션만 움직이고
        /// 검사가 옛 값을 지킨다(CLAUDE.md 확정 규칙 — 과거 4건이 이 형태로 깨졌다).</para>
        /// </summary>
        internal const DeclaredRarity MaxDeclaredRarityForPack = DeclaredRarity.Rare;

        /// <summary>
        /// ★ 팩 아이템의 <b>요구 레벨</b>은 1이어야 한다 — 산 즉시 쓸 수 있어야 한다.
        ///
        /// <para>두 가지를 동시에 막는다. (가) <b>현금으로 사고 나서 레벨을 갈아야</b> 하는 형태는
        /// 상주 동료 앱에서 최악이다. (나) 팩은 등급을 <b>선언</b>하므로 <c>requiredLevel</c>이
        /// 등급에 아무 영향이 없는데, 거기 1이 아닌 값이 남아 있으면 <b>화면이 거짓말을 한다</b>
        /// (보유 조건으로는 읽히는데 등급과는 무관하다).</para>
        /// </summary>
        internal const int PackRequiredLevel = 1;

        /// <summary>순위 -> 등급 사다리. <b>배열 길이가 곧 기준 코호트 크기(6종)</b>이고
        /// <see cref="RarityOfRank"/>가 이 사다리로 비율 환산한다.
        /// <para>ECONOMY_SPEC §3-2: <c>rank 0,1 = 일반 / 2,3 = 희귀 / 4 = 영웅 / 5 = 전설</c>.
        /// 슬롯당 2/2/1/1이고 42종 전체로는 일반 14 / 희귀 14 / 영웅 7 / 전설 7이다.</para></summary>
        private static readonly ItemRarity[] _rarityByRank =
        {
            ItemRarity.Common, ItemRarity.Common,
            ItemRarity.Rare, ItemRarity.Rare,
            ItemRarity.Epic,
            ItemRarity.Legendary,
        };

        /// <summary>
        /// 이 자리의 등급. <b>등급의 유일한 출처</b>다 — 화면도 경제도 여기만 부른다.
        ///
        /// <para>슬롯 안에서 <c>requiredLevel</c>이 낮은 순으로 매긴 순위를 사다리에 태운다.
        /// 요구 레벨이 같은 두 아이템은 <c>itemIndex</c>가 작은 쪽이 앞이다(동점에서도 순위가
        /// 하나로 정해져야 카드가 프레임마다 흔들리지 않는다).</para>
        ///
        /// <para>못 찾는 자리(슬롯 밖 · 애셋 구멍)는 <see cref="ItemRarity.Common"/>이다. 구멍 자체는
        /// <c>EnsureLoaded</c>가 이미 <c>LogError</c>로 크게 신고했고, 여기서 예외를 던지면
        /// 보관함 한 칸의 결손이 창 전체를 못 열게 만든다.</para>
        ///
        /// <para><b>할당 없음</b>: 슬롯 배열을 한 번 훑는 것이 전부다(n = 6). 다만 카드 생성 시점에
        /// 부르는 함수이지 <c>Update()</c>에서 부르는 함수가 아니다.</para>
        /// </summary>
        public static ItemRarity Rarity(EquipmentSlot slot, int itemIndex)
        {
            int s = (int)slot;
            if (s < 0 || s >= BySlot.Length) return ItemRarity.Common;
            return RarityOfMember(BySlot[s], itemIndex);
        }

        /// <summary>
        /// ★ 등급 파생의 <b>유일한 구현</b>. <see cref="Rarity"/>도 테스트도 이 함수 하나를 탄다
        /// (두 벌로 적으면 그 순간 설계 거울이 프로덕션과 갈라진다).
        ///
        /// <para><b>모집단은 슬롯이 아니라 코호트다.</b> 같은 슬롯이라도 <see cref="ItemCatalogEntry.CohortId"/>가
        /// 다른 아이템은 순위에도 분모에도 들어오지 않는다 — 그래야 DLC 팩이 붙어도 기본 42종의
        /// 등급이 <b>한 칸도 안 움직인다</b>. 지금은 팩이 0개라 코호트 == 슬롯이고, 따라서
        /// 이 변경으로 바뀌는 값이 하나도 없다.</para>
        ///
        /// <para>구멍(<c>null</c>)은 순위에서도 분모에서도 빠진다 — 같은 모집단을 두 번 다르게 세지 않는다.
        /// 구멍 자체는 <c>EnsureLoaded</c>가 이미 <c>LogError</c>로 크게 신고했다.</para>
        ///
        /// <para><b>할당 없음</b>: 이미 존재하는 슬롯 배열을 한 번 훑는 것이 전부다.</para>
        /// </summary>
        internal static ItemRarity RarityOfMember(ItemCatalogEntry[] population, int index)
        {
            if (population == null || index < 0 || index >= population.Length) return ItemRarity.Common;

            ItemCatalogEntry mine = population[index];
            if (mine == null) return ItemRarity.Common;

            // ★★ 선언이 있으면 여기서 끝난다 — 아래 순위/비율 계산에 <b>절대 통과시키지 않는다</b>.
            //   코호트 필터만으로는 부족하다: 팩 코호트의 크기가 1이면 rank가 0이라
            //   step = 0 × 6 ÷ 1 = 0 이 되어 <b>선언이 무엇이든 일반</b>이 나온다.
            //   (그리고 팩 6종이 전부 같은 requiredLevel 1 이면 rank는 itemIndex 순서라
            //    같은 팩 안에서 등급이 일반~전설로 갈린다 — 단일 등급 계약이 그 자리에서 깨진다.)
            if (DeclaredRarityRules.TryResolve(mine.Declared, out ItemRarity declaredRarity))
                return declaredRarity;

            int cohort = mine.CohortId;
            int key = UnlockRankKey(mine);

            int rank = 0, counted = 0;
            for (int i = 0; i < population.Length; i++)
            {
                ItemCatalogEntry other = population[i];
                if (other == null || other.CohortId != cohort) continue;   // 다른 팩은 남의 모집단이다
                counted++;
                if (i == index) continue;

                int otherKey = UnlockRankKey(other);
                if (otherKey < key || (otherKey == key && i < index)) rank++;
            }
            return RarityOfRank(rank, counted);
        }

        /// <summary>순위 매김에 쓰는 값. 요구 레벨이 없는 항목(= 처음부터 보유)은 가장 낮은 단이다.</summary>
        private static int UnlockRankKey(ItemCatalogEntry entry)
            => entry.RequiredLevel ?? 0;

        /// <summary>
        /// ★★ <b>등급 선언 감사</b> — 잘못 만든 팩을 <b>로드 시점에</b> 크게 신고한다.
        ///
        /// ============================================================================
        /// 다섯 가지를 본다 (design-systems C-5 ②~⑥)
        /// ============================================================================
        /// <list type="number">
        ///  <item>② <b>기본 코호트가 선언하면</b> 결함. 기본 42종의 등급은 파생이 유일한 출처다 —
        ///        하나라도 선언하는 순간 "레벨이 오를수록 스탯이 안 내려간다"가 사람 손에 맡겨진다.</item>
        ///  <item>③ <b>팩이 선언을 빠뜨리면</b> 결함. ★ <b>이게 가장 위험하다</b> — 침묵의 결과가
        ///        "파생으로 폴백"이고, 팩 코호트는 자기들끼리 순위를 매기므로 <b>한 팩 안에서
        ///        등급이 일반~전설로 갈린다</b>. 화면은 멀쩡해 보이는데 계약만 깨져 있다.</item>
        ///  <item>④ <b>한 코호트 안에서 등급이 섞이면</b> 결함(DS-2 단일 등급).</item>
        ///  <item>⑤ <b><see cref="MaxDeclaredRarityForPack"/> 초과</b>면 결함(페이투윈 차단선).</item>
        ///  <item>⑥ <b>팩의 <c>requiredLevel</c>이 <see cref="PackRequiredLevel"/>이 아니면</b> 결함.</item>
        /// </list>
        ///
        /// ============================================================================
        /// ★ 왜 <b>신고만</b> 하고 값을 고치지 않는가 (이음매를 하나로)
        /// ============================================================================
        /// ⑤에서 전설 선언을 <see cref="MaxDeclaredRarityForPack"/>으로 <b>깎아 내리고 싶은 유혹</b>이
        /// 있는데, 그러면 상한이 <b>두 곳</b>(여기와 감사)에서 계산되고 <b>증상이 사라진다</b> —
        /// 팩은 멀쩡해 보이고 아무도 고치지 않는다. 이 파일이 반복해서 배운 것이 그것이다:
        /// <b>조용히 고치면 그 결함은 영원히 남는다.</b> 판정은 한 곳(여기), 값은 선언 그대로.
        ///
        /// <para><see cref="EnsureLoaded"/>가 슬롯마다 한 번 부르고, EditMode 테스트가 <b>같은 함수에</b>
        /// 합성 모집단을 직접 먹인다 — 규칙을 테스트가 다시 적지 않는다(두 벌은 갈라진다).</para>
        /// </summary>
        /// <param name="population">한 슬롯의 자리 배열. <c>null</c> 자리(에셋 구멍)는 건너뛴다 —
        /// 그건 이미 <see cref="EnsureLoaded"/>가 따로 신고했다.</param>
        /// <param name="faults">사람이 읽을 결함 문장이 <b>추가</b>된다(비우지 않는다).</param>
        internal static void AuditDeclarations(ItemCatalogEntry[] population, List<string> faults)
        {
            if (population == null || faults == null) return;

            for (int i = 0; i < population.Length; i++)
            {
                ItemCatalogEntry e = population[i];
                if (e == null) continue;

                bool declared = DeclaredRarityRules.IsDeclared(e.Declared);

                if (e.CohortId == BaseCohortId)
                {
                    // ② 기본 코호트는 선언하지 않는다.
                    if (declared)
                    {
                        faults.Add($"'{e.Id}'(자리 {i})가 기본 코호트({BaseCohortId})에 있으면서 등급을 " +
                            $"'{e.Declared}'로 선언했습니다. 기본 42종의 등급은 requiredLevel 파생이 " +
                            "유일한 출처입니다 — 선언하려면 팩 코호트를 쓰십시오.");
                    }
                    continue;
                }

                // ③ 팩은 반드시 선언한다. 빠뜨리면 파생으로 떨어져 팩 안에서 등급이 갈린다.
                if (!declared)
                {
                    faults.Add($"'{e.Id}'(자리 {i}, 코호트 {e.CohortId})가 등급을 선언하지 않았습니다. " +
                        "팩은 자기 코호트 안에서 순위가 매겨지므로, 선언이 없으면 같은 팩 6종의 등급이 " +
                        "일반~전설로 갈립니다(팩 단일 등급 계약 위반).");
                }
                else if (e.Declared > MaxDeclaredRarityForPack)
                {
                    // ⑤ 상한 초과.
                    faults.Add($"'{e.Id}'(자리 {i}, 코호트 {e.CohortId})가 등급을 '{e.Declared}'로 선언했는데 " +
                        $"팩 상한은 '{MaxDeclaredRarityForPack}'입니다. 팩은 현금 전용이라 기본 42종보다 " +
                        "위의 단을 내면 돈으로만 닿는 상단이 생깁니다.");
                }

                // ⑥ 팩은 사자마자 쓸 수 있어야 한다.
                if (e.RequiredLevel.HasValue && e.RequiredLevel.Value != PackRequiredLevel)
                {
                    faults.Add($"'{e.Id}'(자리 {i}, 코호트 {e.CohortId})의 requiredLevel이 " +
                        $"{e.RequiredLevel.Value}입니다(팩은 {PackRequiredLevel}이어야 합니다). " +
                        "현금으로 산 뒤 레벨을 갈게 만들지 않습니다.");
                }

                // ④ 한 코호트 안에서 등급이 섞이면 안 된다. 같은 코호트의 <b>앞선</b> 선언과만 견준다
                //    (짝마다 견주면 6종 팩 하나에 결함이 15줄 찍힌다 — 원인은 하나인데).
                if (!declared) continue;
                for (int j = 0; j < i; j++)
                {
                    ItemCatalogEntry other = population[j];
                    if (other == null || other.CohortId != e.CohortId) continue;
                    if (!DeclaredRarityRules.IsDeclared(other.Declared)) continue;

                    if (other.Declared != e.Declared)
                    {
                        faults.Add($"코호트 {e.CohortId} 안에서 등급이 섞였습니다: " +
                            $"'{other.Id}'는 '{other.Declared}', '{e.Id}'는 '{e.Declared}'. " +
                            "팩은 6종이 전부 같은 단이어야 합니다(DS-2 단일 등급).");
                    }
                    break;   // 앞선 선언 하나와만 견준다
                }
            }
        }

        /// <summary>
        /// 코호트 안 순위 -> 등급. <b>사다리는 6종 기준으로 쓰였으므로 코호트 크기가 다르면 비율로 환산한다</b>
        /// (<c>rank × 6 / count</c>). <c>count</c>는 <b>슬롯 개수가 아니라 코호트 개수</b>다 —
        /// 그 구분이 이 함수의 존재 이유다(<see cref="RarityOfMember"/>).
        /// 지금 트리는 코호트가 하나이고 7슬롯 전부 6종이라 이 환산은 <b>항등</b>이며,
        /// 팩이 자기 코호트로 6종을 들고 와도 기본 42종은 그대로다.
        ///
        /// <para>★ 자르지 않고 <b>비율</b>로 늘리는 이유: 순위를 그대로 쓰고 5 이상을 전설로 잘라내면
        /// 8종 슬롯에서 전설이 3개가 된다. 등급의 뜻은 "몇 번째로 늦게 열리는가"가 아니라
        /// "이 슬롯에서 얼마나 위쪽인가"다.</para>
        ///
        /// <para><c>internal</c>인 이유는 이음매를 늘리지 않으면서(공개 표면은 <see cref="Rarity"/> 하나다)
        /// 6종이 아닌 경우의 단조성을 EditMode 테스트가 직접 잴 수 있게 하려는 것이다
        /// (<c>InternalsVisibleTo</c>: Scripts/AssemblyInfo.cs).</para>
        /// </summary>
        internal static ItemRarity RarityOfRank(int rank, int count)
        {
            if (count <= 0) return ItemRarity.Common;

            int ladder = _rarityByRank.Length;
            if (rank < 0) rank = 0;
            if (rank >= count) rank = count - 1;

            int step = rank * ladder / count;
            if (step >= ladder) step = ladder - 1;
            return _rarityByRank[step];
        }

        /// <summary>등급의 낱말. <b>로컬라이제이션이 오면 이 함수 하나만 바뀐다</b> —
        /// 카드/상세 패널/상점이 각자 문자열을 들고 있으면 그 순간 번역이 갈라진다.
        /// <para>낱말이 필요한 이유는 색이 못 하는 일을 하기 때문이다: 색은 식별 하한(ΔE 48.6)을
        /// 여섯 쌍 중 한 쌍에서만 넘는다(PALETTE_SPEC §12-0). 문자는 그 자를 우회하는 유일한 채널이다.</para></summary>
        public static string RarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return "일반";
                case ItemRarity.Rare: return "희귀";
                case ItemRarity.Epic: return "영웅";
                case ItemRarity.Legendary: return "전설";
                default: return "일반";
            }
        }

        /// <summary>아이디로 카테고리 안의 자리를 찾는다. 없으면 -1 —
        /// 저장 파일이 <b>모르는 아이디</b>를 담고 있을 때(구버전에서 지워진 아이템, 손상) 쓰는 유일한 경로.</summary>
        public static int IndexOfItemId(EquipmentSlot slot, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            int s = (int)slot;
            if (s < 0 || s >= BySlot.Length) return -1;

            ItemCatalogEntry[] items = BySlot[s];
            for (int i = 0; i < items.Length; i++)
            {
                // null 방어: 에셋이 빠져 자리에 구멍이 난 경우에도 저장 복원 경로가 예외로 죽지 않게 한다
                // (구멍 자체는 EnsureLoaded가 이미 LogError로 크게 신고했다).
                if (items[i] != null && items[i].Id == itemId) return i;
            }
            return -1;
        }

        /// <summary>장비 항목 수(보관함 헤더 "걸치는 것 (9/32)"의 분모).</summary>
        public static int EquipmentCount
        {
            get
            {
                int n = 0;
                for (int s = 0; s < BySlot.Length; s++) n += BySlot[s].Length;
                return n;
            }
        }

        /// <summary>행동 항목 수(보관함 헤더 "할 줄 아는 것 (13)").</summary>
        public static int ActionCount => _actions.Length;

        /// <summary>지금 보유한 장비 수(보관함 헤더의 분자).</summary>
        public static int UnlockedEquipmentCount(StickConfig config)
        {
            int n = 0;
            for (int i = 0; i < AllEntries.Length; i++)
            {
                if (AllEntries[i].Category == ItemCategory.Equipment && AllEntries[i].IsOwned(config)) n++;
            }
            return n;
        }

        public static ItemCatalogEntry At(int index)
            => index >= 0 && index < AllEntries.Length ? AllEntries[index] : null;

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
        // 카드 썸네일 색을 <b>그대로</b> 몸에 칠하면 ①은 풀리지만 ②는 오히려 남는다. <b>그때의</b>
        // 카드 색은 34-1 다크 카드 위에서 읽히도록 명도를 올려 잡은 값이라 흰 잉크 캐릭터 위에서는
        // 아이보리(옛 0xE8E2D4)·종이(옛 0xEEF2F8)·은(옛 0xD3DAE4)이 전부 <b>흰색과 구분되지 않았다</b>
        // (실측: 착용 스크린샷에서 머리 원·털모자·나비넥타이가 흰 덩어리 하나로 뭉쳐 보였다).
        // ※ 이 세 hex는 <b>사건 기록</b>이다. 지금 트리의 값이 아니다(2026-09-02 자립 대역 이행).
        //
        // 그래서 색은 카탈로그에서 오되, 몸 위에서는 두 가지 하한을 강제한다. 새 색표를 만들지
        // 않으므로 "카드와 몸이 다른 색"이라는 이중 정의는 생기지 않는다 — 같은 색의 <b>착용 형태</b>다.
        //   · 채도 하한 — 잉크는 언제나 무채색(흰/검)이다. 채도가 있으면 잉크와 절대 안 섞인다.
        //   · 명도 창(0.55~0.80) — 흰 잉크(V=1)와도 검은 잉크(V=0)와도 충분히 벌어지고,
        //     밝은 바탕화면/어두운 바탕화면 양쪽에서 사라지지 않는 중간 대역이다.
        //
        // ★ 2026-09-02 — 위 "이중 정의는 생기지 않는다"는 <b>측정으로 반증됐었다</b>. 옛 27색 중
        //   18색을 이 함수가 실제로 바꿨고 카드↔몸 ΔE가 최악 42.3까지 벌어졌다. 지금은 애셋 색이
        //   전부 이 상자 <b>안</b>에 있어 이 함수가 항등이다 — 주장이 아니라 성질이 됐고,
        //   Tests/EditMode/ItemPaletteBandGateTests가 그 항등을 잠근다.
        //   이 상자는 <b>바탕화면 두 극단만</b> 보고 잡은 값이라 그것만으로는 부족하다는 것도 그날
        //   드러났다 — 종이/목탄 무대까지 넣은 대역은 위 팔레트 절과 그 테스트에 있다.
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
            for (int i = 0; i < AllEntries.Length; i++)
            {
                if (AllEntries[i].Id == id) return AllEntries[i];
            }
            return null;
        }
    }
}
