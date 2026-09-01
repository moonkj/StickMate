using System;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// <see cref="ItemIconPart"/>의 <b>직렬화 가능한 쌍둥이</b>. 런타임 구조체는 readonly 필드라
    /// Unity 직렬화기가 손대지 못한다 — 값을 에셋에 눕히려면 쓰기 가능한 형태가 한 벌 필요하다.
    /// 두 형태의 필드는 <b>1:1</b>이고 변환은 <see cref="AccessoryDefSO.BuildIcon"/> 한 곳에서만 한다.
    /// </summary>
    [Serializable]
    public struct AccessoryIconPartData
    {
        public ItemIconPartKind kind;

        /// <summary>꺾은선이면 x0,y0,x1,y1,… / 원이면 cx,cy,r. 좌표계는 40×40 viewBox(원점 좌상단).</summary>
        public float[] values;

        /// <summary>해금 상태에서 이 조각을 칠할 색(이미 <c>Tinted()</c>가 역할에 맞는 색을 채운 결과값).</summary>
        public Color color;

        /// <summary>0 = 주색, 1 = 보조색. 런타임 구조체는 byte지만 에셋에는 int로 눕힌다 —
        /// YAML에서 byte/int는 같은 정수로 적히고, int 쪽이 인스펙터/JSON 도구와 마찰이 없다.</summary>
        public int tone;
    }

    /// <summary>
    /// ★ 장비 아이템 1종 = 에셋 1개 (DLC 이행 A단계, docs/ARCHITECTURE.md 5-3-3).
    ///
    /// ============================================================================
    /// 왜 만들었나
    /// ============================================================================
    /// 원칙 4("신규 모션/이펙트는 기본 로직 무수정으로 매니페스트를 통해 추가")가 선언만 되어 있고
    /// 실제로는 28종이 <c>ItemCatalog.cs</c>의 <c>new Row(...)</c> 나열이었다. 그 상태에서는 DLC 팩
    /// 하나를 붙일 때마다 <b>기본 로직 파일을 고쳐야</b> 한다. 이 에셋은 그 표를 코드 밖으로 꺼낸 것이다.
    ///
    /// ============================================================================
    /// 이 에셋이 <b>아직</b> 하지 않는 일 (A단계 경계)
    /// ============================================================================
    ///  · 도형(몸에 붙는 벡터)은 여전히 <c>Interaction/AccessoryShapeBuilder.cs</c>의 switch가 갖고 있다.
    ///    여기 있는 <see cref="icon"/>은 <b>카드 썸네일 40×40</b>이지 몸에 붙는 도형이 아니다.
    ///  · Addressables/팩 매니페스트는 C단계다. 지금은 평범한 <c>Resources</c> 로드다.
    ///
    /// ============================================================================
    /// 아이디가 곧 세이브 키다
    /// ============================================================================
    /// <see cref="itemId"/>는 세이브 v5가 그대로 적는 값이다(<c>Core/CharacterSaveStore.cs</c>).
    /// <b>이름을 바꾸면 사용자의 차림이 사라진다</b> — 표시 이름(<see cref="displayName"/>)만 바꿔라.
    /// </summary>
    [CreateAssetMenu(fileName = "AccessoryDef", menuName = "StickMate/Accessory Def", order = 1)]
    public sealed class AccessoryDefSO : ScriptableObject
    {
        [Header("정체")]
        [Tooltip("세이브 파일과 훗날의 상점 SKU가 쓰는 안정적인 아이디. 절대 바꾸지 말 것.")]
        public string itemId;

        [Tooltip("이 아이템이 차지하는 카테고리.")]
        public EquipmentSlot slot;

        [Tooltip("카테고리 안에서의 자리(0~3). AccessoryShapeBuilder의 switch가 이 번호로 도형을 고른다 " +
                 "— 순서를 바꾸면 그림이 통째로 어긋난다.")]
        public int itemIndex;

        [Header("표시")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("없는 효과를 주장하지 말 것(전투 수치/소리). 방해성 행동에는 탈출구를 명시할 것.")]
        public string description;

        [Header("규칙")]
        [Tooltip("이 아이템을 보유하게 되는 레벨. 1이면 처음부터 보유.")]
        public int requiredLevel = 1;

        /// <summary>
        /// ★ 이 아이템이 <b>머리카락을 가리는가</b>. 지금 <c>AccessoryShapeBuilder.HatCoverLocalY</c>는
        /// "모자면 가린다"를 전역 규칙으로 갖고 있는데, 그건 규칙이 아니라 <b>아이템별 성질</b>이다
        /// (왕관은 얹는 것이라 밑이 뚫려 있어 머리가 보이는 게 옳다).
        /// <para><b>A단계에서는 아직 아무도 읽지 않는다</b> — 값만 실제 렌더러 동작과 일치하게 채워 둔다.
        /// 렌더러를 이 필드로 갈아타게 하는 것은 별도 라운드다(Major 4).</para>
        /// </summary>
        [Tooltip("모자 계열이 머리카락을 덮는가. A단계에서는 기록만 하고 렌더러는 아직 읽지 않는다.")]
        public bool hidesHair;

        [Header("카드 썸네일 (40×40 viewBox, 원점 좌상단, y 아래로)")]
        public AccessoryIconPartData[] icon;

        /// <summary>에셋에 누운 값 -> 런타임 구조체 배열. <b>배열을 복사</b>하는 이유는, 복사하지 않으면
        /// 런타임이 들고 있는 <c>float[]</c>가 곧 임포트된 에셋의 배열이라 누가 한 칸이라도 쓰면
        /// 에디터에서 에셋이 조용히 더러워지기 때문이다. 정적 초기화 때 한 번만 도는 경로다.</summary>
        public ItemIconPart[] BuildIcon()
        {
            if (icon == null || icon.Length == 0) return null;

            var parts = new ItemIconPart[icon.Length];
            for (int i = 0; i < icon.Length; i++)
            {
                float[] src = icon[i].values;
                float[] values = src != null ? new float[src.Length] : null;
                if (src != null) Array.Copy(src, values, src.Length);

                parts[i] = new ItemIconPart(icon[i].kind, values, icon[i].color, (byte)icon[i].tone);
            }
            return parts;
        }
    }
}
