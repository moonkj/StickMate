namespace StickMate.Core
{
    /// <summary>
    /// 아이템 등급 4단계.
    ///
    /// <para>★ <b>값이 곧 리본 칸 수 − 1</b>이다(<c>칸 수 = (int)rarity + 1</c>). 그래서 개수를 나르는
    /// 함수가 따로 없다 — 주 채널이 열거값 그 자체다(design/art/PALETTE_SPEC.md §12-4 / §14-2).
    /// 숫자를 재배열하면 화면의 칸 수가 조용히 바뀐다.</para>
    ///
    /// <para><b>저장 필드가 0개다.</b> 등급은 슬롯 안에서 <c>requiredLevel</c> 순위로부터 파생되므로
    /// (design/systems/ECONOMY_SPEC.md §3-2) 세이브 스키마를 건드리지 않는다. 유일한 출처는
    /// <see cref="ItemCatalog.Rarity"/>이고, 색의 유일한 출처는 <c>UiChrome.RarityColor</c>다.</para>
    ///
    /// <para><b>색은 주 채널이 아니다.</b> 실측상 등급 램프 4색은 인접 쌍 ΔE 15.2~17.7로 "나란히 놓으면
    /// 다르다"(변별)는 되지만 "하나만 보고 맞힌다"(식별)는 정상 시각에서도 안 되고, 완전색맹·흑백
    /// 출력에서는 변별마저 6.14로 무너진다(PALETTE_SPEC §12-0 / §12-1). 그래서 등급을
    /// <b>색만으로</b> 표시하는 화면을 만들면 안 된다 — 칸 수(주)와 낱말(확정)이 함께 간다.</para>
    /// </summary>
    public enum ItemRarity
    {
        /// <summary>일반 — 리본 1칸.</summary>
        Common = 0,

        /// <summary>희귀 — 리본 2칸.</summary>
        Rare = 1,

        /// <summary>영웅 — 리본 3칸.</summary>
        Epic = 2,

        /// <summary>전설 — 리본 4칸.</summary>
        Legendary = 3,
    }

    /// <summary>
    /// ★ <b>에셋이 등급을 「선언」할 때 쓰는 별도 열거형.</b> 0이 <see cref="Derived"/>인 것이 존재 이유다.
    ///
    /// ============================================================================
    /// 왜 <see cref="ItemRarity"/>를 그대로 쓰지 않는가 (2026-09-02 실측)
    /// ============================================================================
    /// Unity 는 <c>.asset</c> 에 키가 없으면 그 필드를 <b>C# 기본값 그대로</b> 둔다. 그래서
    /// 선언 필드의 타입을 <see cref="ItemRarity"/>로 두면 <c>default</c> 가 곧
    /// <see cref="ItemRarity.Common"/>(= 0)이고, <b>「아무 말도 안 한 것」과 「일반이라고 말한 것」이
    /// 값으로 구분되지 않는다.</b>
    ///
    /// <para><b>실측</b>(<c>Tools/ShapeDump</c> 오프라인 하니스, 프로덕션 직렬화 경로 그대로):
    /// <c>public ItemRarity declaredRarity;</c> 를 넣고 <c>Resources/Items</c> 42개를 읽으면
    /// <b>42/42 가 Common 으로 실린다</b>(파일은 한 바이트도 안 바뀌었는데). 거기에 「선언이 이긴다」를
    /// 적용하면 <b>28/42 의 등급이 내려앉는다</b> — 희귀 12·영웅 6·전설 7 이 전부 일반이 된다.
    /// 세트 완성 4스탯이 반토막 나고, <b>"기본 42종만으로 캡 20"이라는 사용자 확정 차단선이
    /// 아무도 안 건드렸는데 깨진다.</b></para>
    ///
    /// <para>★ <b>왜 별도 열거형이고 <c>declaredRarityPlusOne</c> 같은 int 오프셋이 아닌가</b>:
    ///  · <b>컴파일러가 막는다.</b> 오프셋은 그냥 <c>int</c> 라 <c>RarityOfRank(rank, count)</c> 같은
    ///    자리에 <b>말없이 들어간다</b>. 별도 타입은 그 자리에 넣으려면 명시적 캐스팅이 필요해서
    ///    사람이 한 번 멈춘다 — <c>ForEquipment</c> 의 기본 인자를 없앤 것과 같은 처방이다.
    ///  · <b>인스펙터가 읽힌다.</b> 오프셋은 드롭다운이 아니라 숫자 칸이고, 만드는 사람이
    ///    "2가 희귀"를 외워야 한다. 외우는 규칙은 언젠가 틀린다.
    ///  · <b>0의 이름이 화면에 뜬다.</b> 드롭다운 맨 위에 <c>Derived</c> 가 보이므로
    ///    "안 적으면 파생"이 문서가 아니라 UI 가 된다.</para>
    ///
    /// <para>★★ <b>절대 하지 말 것</b>: <see cref="Derived"/> 를 0이 아닌 값으로 바꾸거나 단을 앞에
    /// 끼워 넣는 것. 기본 42종의 <c>.asset</c> 에는 이 키가 <b>한 파일도 없어서</b> 전부
    /// <c>default</c> 로 실린다 — 0이 <see cref="Derived"/> 가 아니게 되는 순간, 파일을 한 바이트도
    /// 안 고쳤는데 42종이 조용히 「무언가로 선언됨」이 된다. 그리고 그 무언가는 대개 일반이다.
    /// <c>ItemRarityDerivationTests</c> 가 그 등식(<c>default(DeclaredRarity) == Derived</c>)을 잠근다.</para>
    ///
    /// <para><b>이 열거형은 <see cref="ItemRarity"/>가 아니다.</b> 화면·색·리본 칸 수는 전부
    /// <see cref="ItemRarity"/> 하나만 안다(<c>UiChrome.RarityColor</c>). 선언은
    /// <see cref="DeclaredRarityRules.TryResolve"/> 를 지나 <see cref="ItemRarity"/> 가 된 뒤에야
    /// 바깥으로 나간다 — 그래서 "더 센 전설" 같은 것이 원리적으로 못 생긴다.</para>
    /// </summary>
    public enum DeclaredRarity
    {
        /// <summary>★ <b>선언 없음</b> — 등급을 <c>requiredLevel</c> 코호트 순위에서 파생한다.
        /// 기본 42종이 전부 여기다(키를 안 적었으므로). <b>0이어야 한다.</b></summary>
        Derived = 0,

        /// <summary>일반이라고 <b>명시적으로</b> 선언. 「안 적음」과 다르다.</summary>
        Common = 1,

        /// <summary>희귀 — DLC 팩이 쓸 수 있는 <b>유일한</b> 단이다
        /// (<c>ItemCatalog.MaxDeclaredRarityForPack</c>).</summary>
        Rare = 2,

        /// <summary>영웅 — 타입은 있지만 팩에는 못 쓴다(<c>MaxDeclaredRarityForPack</c> 초과).</summary>
        Epic = 3,

        /// <summary>전설 — 타입은 있지만 팩에는 못 쓴다(<c>MaxDeclaredRarityForPack</c> 초과).</summary>
        Legendary = 4,
    }

    /// <summary>
    /// 선언 ↔ 등급 사이의 <b>유일한 다리</b>. 두 열거형의 값을 손으로 더하거나 빼는 코드가
    /// 이 파일 밖에 생기면 그 순간 두 번째 진실이 된다.
    /// </summary>
    public static class DeclaredRarityRules
    {
        /// <summary>
        /// 선언을 등급으로 푼다. <b>선언이 없으면 <c>false</c></b> 이고, 그때
        /// <paramref name="rarity"/> 는 쓰면 안 되는 값이다.
        ///
        /// <para>★ <c>ItemRarity</c> 를 <b>돌려주지 않고</b> <c>bool</c> 을 돌려주는 이유:
        /// "선언이 없다"를 등급 하나로 표현하려면 어떤 등급을 빌려 써야 하는데, 그 순간
        /// 이 파일이 고치려던 문제(0이 곧 일반)가 그대로 돌아온다. 호출부가 <b>두 갈래를
        /// 반드시 쓰게</b> 만드는 것이 요점이다.</para>
        ///
        /// <para>★ 뺄셈(<c>(ItemRarity)((int)declared - 1)</c>)을 쓰지 않는다. 그러면 두 열거형이
        /// 영원히 나란히 커야 한다는 <b>암묵적 계약</b>이 생기고, 그 계약은 문서에 없어서 언젠가
        /// 조용히 깨진다. 여기 <c>switch</c> 는 단이 하나 늘 때 <b>컴파일러가 아니라 사람이</b>
        /// 이 자리를 보게 한다.</para>
        /// </summary>
        public static bool TryResolve(DeclaredRarity declared, out ItemRarity rarity)
        {
            switch (declared)
            {
                case DeclaredRarity.Common: rarity = ItemRarity.Common; return true;
                case DeclaredRarity.Rare: rarity = ItemRarity.Rare; return true;
                case DeclaredRarity.Epic: rarity = ItemRarity.Epic; return true;
                case DeclaredRarity.Legendary: rarity = ItemRarity.Legendary; return true;
                default: rarity = ItemRarity.Common; return false;   // Derived — 쓰면 안 되는 값
            }
        }

        /// <summary>선언이 있는가. <see cref="TryResolve"/> 와 <b>같은 판정</b>이어야 하므로
        /// 그 함수를 그대로 부른다(두 벌로 적으면 둘이 갈라진다).</summary>
        public static bool IsDeclared(DeclaredRarity declared) => TryResolve(declared, out _);
    }
}
