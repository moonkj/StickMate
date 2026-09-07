using System;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 코스튬이 <b>어디에 소속되는가</b>. 소속은 개방(엔타이틀먼트)과 <b>다른 축</b>이다 —
    /// 여기는 «누구의 차림인가»이고, 개방은 «지금 그 사람에게 열려 있는가»다.
    /// </summary>
    public enum CostumeSourceKind
    {
        /// <summary>기본 코호트(<see cref="ItemCatalog.BaseCohortId"/>)의 <b>테마</b>에서 나온다.
        /// 이 갈래에는 엔타이틀먼트가 없다 — <b>무료는 「안 묻는 것」</b>이다.</summary>
        BaseTheme = 0,

        /// <summary>DLC 팩의 코호트에서 나온다. 개방은 그 팩의 엔타이틀먼트가 정한다.</summary>
        Pack = 1,
    }

    /// <summary>
    /// 단계(진화)별로 <b>다르게 그리는 조각</b>. 비어 있으면 그 단계는 0단계와 같은 모습이다.
    ///
    /// <para><b>임계 「값」은 여기 없다</b> — 언제 이 단계가 되는가는 전부
    /// <see cref="CostumeEvolutionRules"/>가 정한다. 매니페스트마다 임계를 적을 수 있게 하면
    /// «이 팩만 빨리 큰다»가 되고 그건 페이투윈 축이다.</para>
    ///
    /// <para>★ <c>AccessorySurface.Stage</c>(조각 계약의 새 비트)를 <b>열지 않았다</b>(계약서 I-9 보류).
    /// 단계 조형이 이 배열로 들어오면 비트가 필요 없고, 조각 계약 v2→v3 승격은
    /// 팩 스키마와 같은 등급의 되돌릴 수 없는 결정이다.</para>
    /// </summary>
    [Serializable]
    public struct CostumeStageOverride
    {
        [Tooltip("이 조형이 적용되는 단계 번호(0~3). 0번은 기본이고, 적지 않아도 기본 조형이 그려진다.")]
        public int stage;

        [Tooltip("그 단계에서 대신 그릴 조각. 비면 이 항목은 아무 일도 하지 않는다.")]
        public AccessoryWornShapeData[] shapes;
    }

    /// <summary>
    /// ============================================================================
    /// ★ 코스튬 하나 = 매니페스트 에셋 하나 (불변 원칙 4의 코스튬 판)
    /// ============================================================================
    /// 합격 기준은 <see cref="StickPackManifestSO"/>와 <b>같은 문장</b>이다:
    /// <b>아홉 번째 코스튬을 프로덕션 <c>.cs</c> 0줄로 추가할 수 있는가.</b>
    /// 발견은 <see cref="CostumeCatalog"/>가 폴더를 훑어서 한다 — 이 파일에도, 어떤 <c>enum</c>에도,
    /// 어떤 배열에도 항목을 더하지 않는다.
    ///
    /// ============================================================================
    /// 왜 <see cref="StickPackManifestSO"/>에 필드를 더하지 않았는가 (근거 3건)
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>기본 코호트 코스튬은 팩이 없다.</b> 오늘 유일하게 4부위 에셋이 전부 실재하는
    ///    코스튬은 기본 코호트 테마 <c>office</c>에서 나오고, 그것을 팩 매니페스트에 넣으면
    ///    <b>무료 코스튬이 팩 에셋을 요구</b>하게 되어 그 자리에서 죽는다.</item>
    ///  <item><b>팩 스키마를 v2→v3로 올려야 한다.</b> 그러면 <b>v2 앱이 v3 팩을 반쯤 읽는</b> 창이
    ///    열린다 — <see cref="StickPackManifestSO.SchemaVersion"/>이 막으려는 바로 그것이다.</item>
    ///  <item><b>연출은 상품과 수명이 다르다.</b> 이미 판 팩에 코스튬을 나중에 얹는 경우가 정상이다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 문자열은 <b>키</b>다 — 검사기를 새로 짓지 않는다
    /// ============================================================================
    /// 모양 판정은 <see cref="PackManifestKeys.IsWellFormed"/> 하나를 쓴다. 두 벌로 적으면
    /// 그 순간 «검사는 통과했는데 화면에 한글이 박힌» 상태가 만들어진다(팩이 이미 겪은 길이다).
    ///
    /// <para>★ <b>키 공간은 <c>costume.*</c>이고 <c>packId</c>와 별개다.</b>
    /// <c>pack.office</c>는 상품이고 <c>costume.office</c>는 연출이며 <b>1:1이 아니다</b> —
    /// <c>costume.office</c>는 오늘 팩 없이 기본 코호트 테마에서 나온다. 그리고 이 문자열은
    /// <b>세이브 파일에 앉는다</b>(<see cref="CostumeFocusRecord.costumeKey"/>) — 바꾸면 누적 100시간이
    /// <b>다른 코스튬 것이 된다</b>.</para>
    ///
    /// ============================================================================
    /// 이 에셋이 <b>선언하지 않는</b> 것
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>진화 임계</b> — <see cref="CostumeEvolutionRules"/> 한 곳이다(위 문단).</item>
    ///  <item><b>4부위 아이템 목록</b> — 소속은 <b>아이템</b>이 <c>cohortId</c>로 선언하고
    ///    (<see cref="AccessoryDefSO.cohortId"/>), 코스튬은 <b>그 소속의 이름</b>만 적는다.
    ///    목록을 여기 적으면 «이 아이템이 어느 코스튬인가»가 두 곳에 앉고, 갈라지는 날 증상은
    ///    <b>코스튬이 조용히 안 뜨는 것</b>이다.</item>
    ///  <item><b>「상위 티어인가」 같은 불리언</b> — 있으면 플래그와 실물이 갈라진다.
    ///    <b>「있으면 그것이 곧 데이터」</b>다(<see cref="propShapes"/>가 비면 프롭이 없는 것이다).</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(fileName = "CostumeManifest", menuName = "StickMate/Costume Manifest", order = 3)]
    public sealed class CostumeManifestSO : ScriptableObject
    {
        /// <summary>
        /// ★ 이 <b>스키마</b>의 현재 판. 코스튬이 늘어나는 것과 무관하고, <b>매니페스트에 필드가
        /// 늘어날 때만</b> 오른다(<see cref="StickPackManifestSO.SchemaVersion"/>과 같은 성질).
        /// <para><b>세이브 스키마와 아무 관계가 없다</b> — 코스튬 «보유»는 저장하지 않는다.
        /// 저장하는 것은 <b>입은 채 몇 분을 보냈는가</b>뿐이고 그건
        /// <see cref="CharacterSaveStore.CurrentVersion"/>의 축이다.</para>
        /// <para>★ <b>v1 (2026-09-07)</b> — 첫 판. 계약서 2-3절이 적은 <c>keyposes</c>는 그 타입
        /// (<see cref="CostumeKeyposeTableSO"/>)이 아직 없어 빠져 있었다.</para>
        /// <para>★ <b>v2 (2026-09-07, P4)</b> — 그 타입이 생겨 <see cref="keyposes"/>를 더했다.
        /// <b>지금이 올릴 수 있는 유일하게 싼 시점이다</b>: 실린 코스튬 매니페스트가 <b>0개</b>라
        /// 승격 비용이 0이고, 팩이 하나라도 나간 뒤에는 «v1 앱이 v2 팩을 반쯤 읽는» 창이 열린다
        /// (<see cref="StickPackManifestSO.SchemaVersion"/>이 막으려는 바로 그것이다).
        /// 키포즈를 쓰는 코스튬만 <see cref="requiresSchemaVersion"/>을 2로 적으면 되고,
        /// 프롭만 있는 코스튬은 <b>v1 그대로 유효하다</b> — 없는 키는 C# 기본값(null)이고
        /// null은 「전용 모션 없음」이라는 <b>선언된 사실</b>이지 결함이 아니다.</para>
        /// </summary>
        public const int SchemaVersion = 2;

        [Header("정체 — ★ costume.* 역DNS. packId와 같은 문자열을 쓰지 말 것")]
        [Tooltip("역DNS 형식(costume.office). 세이브 파일의 누적 분이 이 문자열로 적힌다 — " +
                 "절대 바꾸지 말 것. 바꾸면 사용자의 누적 100시간이 다른 코스튬 것이 된다.")]
        public string costumeKey;

        [Tooltip("이 매니페스트가 요구하는 스키마 판. 앱의 CostumeManifestSO.SchemaVersion 보다 크면 " +
                 "앱이 이 코스튬을 싣지 않고 크게 신고한다(반쯤 읽는 것보다 낫다).")]
        public int requiresSchemaVersion = 1;

        [Header("소속 — 이 코스튬은 어느 «차림»이 합의될 때 사는가")]
        [Tooltip("BaseTheme면 기본 코호트의 테마에서, Pack이면 그 팩의 코호트에서 나온다.")]
        public CostumeSourceKind sourceKind;

        [Tooltip("BaseTheme면 ItemCatalog의 테마 키(office 등), Pack이면 packId(pack.cyber 등). " +
                 "둘 다 ASCII 소문자/숫자/점/밑줄만.")]
        public string sourceId;

        [Header("표시 — ★ 키만 적는다. 원문을 적으면 로드가 거부한다")]
        [Tooltip("표시 이름의 로컬라이즈 키(예: costume.office.name). ASCII 소문자/숫자/점만.")]
        public string displayNameKey;

        /// <summary>
        /// 프롭의 좌표 스트림.
        ///
        /// <para>★ <b>새 문법을 만들지 않았다.</b> 계약서 2-3절은 이 자리를 <c>CostumePropShapeData[]</c>로
        /// 적었지만, 이 저장소에는 <b>이미 검사기까지 갖춘</b> 좌표 스트림 문법이 있다
        /// (<see cref="AccessoryWornShapeData"/> + <see cref="AccessoryWornShapeReader.Validate"/>).
        /// 두 벌을 만들면 «검사는 통과했는데 화면은 깨진» 상태가 만들어진다 —
        /// <see cref="PackManifestKeys.IsWellFormed"/>를 한 벌로 유지한 이유와 같다.
        /// <b>이 선택은 리더에게 보고했다</b>(형태 차이일 뿐 이음매는 동일하다).</para>
        ///
        /// <para>기준 좌표계는 <b>프롭 앵커</b>다(캐릭터 몸이 아니다). 앵커는 몰입기 진입 프레임에
        /// 한 번 스냅샷되고 그 뒤로 움직이지 않는다 — 그 «한 번 만든 뒤 아무도 안 만진다»가
        /// 사용자 요구(«매 프레임 재드로우 금지»)의 실체다.</para>
        /// </summary>
        [Header("소환 오브젝트(프롭) — 전 팩 필수. 비면 프롭 없음")]
        public AccessoryWornShapeData[] propShapes;

        [Tooltip("캐릭터 발밑 기준 수평 오프셋(신장 H 배수). 캐릭터가 바라보는 방향으로 양수다.")]
        public float propAnchorOffsetXInH;

        /// <summary>
        /// 이 코스튬의 <b>전용 모션</b>(LFVS 키포즈 표). <b>비어도 정상이다</b> —
        /// 무료 코스튬은 소환 오브젝트만 출하하고 캐릭터는 기존 관망 자세 그대로다(티어 게이트 P-13).
        ///
        /// <para>★ <b>「있으면 그것이 곧 데이터」</b> — <c>propShapes</c>가 비면 프롭이 없는 것과 같은
        /// 어법이다. 「상위 티어인가」 같은 불리언을 따로 두지 않는다(플래그와 실물이 갈라진다).</para>
        ///
        /// <para>★ 이 필드를 쓰는 코스튬은 <see cref="requiresSchemaVersion"/>을 <b>2</b>로 적어야 한다.
        /// v1 앱은 이 키를 몰라 <b>조용히 무시</b>하고, 그러면 프롭은 서는데 캐릭터만 안 움직이는
        /// 「반쯤 읽힌」 화면이 된다.</para>
        /// </summary>
        [Header("전용 모션(LFVS) — 유료 티어. 비면 기존 관망 자세 그대로다")]
        public CostumeKeyposeTableSO keyposes;

        [Header("진화 — ★ 임계 «값»은 여기 없다(CostumeEvolutionRules가 단일 출처)")]
        [Tooltip("단계별 조각 차이. 비면 단계가 올라도 외형이 변하지 않는다(그 자체가 정상 상태다).")]
        public CostumeStageOverride[] stageShapes;
    }
}
