using System;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 팩을 파는 <b>창구</b>. 채널이 늘어나는 것은 SDK가 늘어나는 것이라 <b>코드 변경을 동반한다</b> —
    /// 그래서 여기는 문자열이 아니라 열거형이다(팩이 늘어나는 것과 성질이 다르다).
    ///
    /// <para>★ <b>이 열거형이 「N팩 통로」의 예외가 아니라는 점을 분명히 해 둔다</b>:
    /// 팩을 하나 더 파는 데는 이 파일이 필요 없다. 새 <b>스토어</b>를 여는 데만 필요하고,
    /// 그때는 어차피 그 스토어의 SDK 배선이 프로덕션 코드다.</para>
    /// </summary>
    public enum PackStoreChannel
    {
        /// <summary>스팀 DLC(appid). 1.0의 유일한 채널이다(사용자 확정: 스팀 단독).</summary>
        Steam = 0,

        /// <summary>Microsoft Store 애드온.</summary>
        MicrosoftStore = 1,

        /// <summary>Mac App Store 인앱 구매.</summary>
        MacAppStore = 2,

        /// <summary>iOS/iPadOS StoreKit.</summary>
        AppleStoreKit = 3,
    }

    /// <summary>
    /// 채널 하나에서 이 팩을 가리키는 식별자. <b>무결성 검증은 채널 SDK 책임</b>이고
    /// (docs/security/ENTITLEMENT_CONTRACT.md §E-5) 우리는 문자열을 나르기만 한다.
    /// <para>여러 채널을 한 매니페스트에 적을 수 있는 이유: 팩은 상품이고 상품은 여러 스토어에
    /// 동시에 오른다. 채널마다 매니페스트를 복제하면 팩 하나의 사실이 채널 수만큼 갈라진다.</para>
    /// </summary>
    [Serializable]
    public struct PackEntitlementRef
    {
        public PackStoreChannel channel;

        /// <summary>스팀이면 DLC appid 문자열, 스토어마다 형식이 다르다.
        /// <b>비면 그 채널에서는 팔지 않는다</b>는 뜻이다(빈 문자열을 「무료」로 읽지 않는다 —
        /// 무료는 채널 항목 자체가 없는 것이다).</summary>
        [Tooltip("절대 바꾸지 말 것 — 출시 후 재변경하면 이미 산 사람이 이 팩을 못 쓰게 됩니다. " +
            "companyName/번들ID(I-1)와 같은 등급의 동결 대상입니다(결재-3, 2026-09-05, " +
            "docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md §10-1).")]
        public string entitlementId;
    }

    /// <summary>
    /// ★ 팩이 <b>색을 어디서 얻는가</b>. 리더 질의(2026-09-03): <i>"팩이 계속 늘면 새 팩이 색을
    /// 어디서 얻는가 — 기존 정원 재사용인가, 팩당 배정인가."</i>
    ///
    /// <para><b>이 파일은 그 정책을 정하지 않는다</b>(수치·색 판정은 <c>design-art</c> 소관).
    /// 정하는 것은 <b>매니페스트가 그 답을 적을 수 있는 자리</b> 하나다 — 자리가 없으면
    /// 일곱 번째 팩의 색이 어디서 왔는지 아무 데도 안 남고, 그때 팔레트 감사가
    /// "이 색이 규칙을 지켰는가"를 물을 대상이 사라진다.</para>
    /// </summary>
    public enum PackPaletteOrigin
    {
        /// <summary>이 팩만의 색상각을 <b>배정받았다</b>. 같은 각을 두 팩이 쓰면 결함이다
        /// (<see cref="PackRegistry"/>가 잡는다).</summary>
        DedicatedHue = 0,

        /// <summary>기존 카탈로그 색을 <b>재사용</b>한다. 각 중복 검사를 하지 않는다 —
        /// 재사용이 곧 의도이기 때문이다.</summary>
        ReusedCatalogColor = 1,
    }

    /// <summary>
    /// ============================================================================
    /// ★ DLC 팩 하나 = 매니페스트 에셋 하나 (원칙 4의 실제 구현, C단계)
    /// ============================================================================
    /// 사용자 확정 2026-09-03: <b>"6팩모두"</b> · <b>"출시 이후부터 계속 추가팩 만들거야"</b>.
    /// 두 번째가 설계를 바꾼다 — 만들어야 하는 것은 「6팩짜리 통로」가 아니라 <b>「N팩짜리 통로」</b>다.
    /// 합격 기준은 하나다: <b>일곱 번째 팩을 프로덕션 <c>.cs</c> 0줄로 추가할 수 있는가.</b>
    /// 그것을 <c>Tests/EditMode/PackManifestCorridorTests.cs</c> 가 합성 7번째 팩으로 직접 잰다.
    ///
    /// ============================================================================
    /// 이 에셋이 <b>선언하지 않는</b> 것 — 아이템 목록
    /// ============================================================================
    /// 팩이 <c>AccessoryDefSO[]</c> 를 직접 들고 있으면 "이 아이템이 어느 팩 것인가"라는 <b>같은
    /// 사실이 두 곳</b>에 앉는다(매니페스트의 배열과 아이템의 <see cref="AccessoryDefSO.cohortId"/>).
    /// 둘이 갈라지는 날 증상은 <b>등급이 조용히 미끄러지는 것</b>이고, 그건 화면만 봐서는 못 찾는다
    /// (<see cref="ItemCatalogEntry.CohortId"/> 문단의 실측: 슬롯 6→12에서 전설이 희귀가 된다).
    ///
    /// <para>그래서 <b>소속은 아이템이 선언하고</b>(<c>cohortId</c>), 매니페스트는 <b>그 번호를 가진
    /// 팩이 무엇인지</b>를 선언한다. 조인은 <see cref="PackRegistry"/> 한 곳에서만 일어난다.
    /// 매니페스트가 적는 것은 개수(<see cref="declaredItemCount"/>)뿐이고, 그건 목록이 아니라
    /// <b>검사값</b>이다 — <c>ItemCatalog.EnsureLoaded</c> 가 못 잡는 유일한 결손
    /// ("카테고리의 <b>마지막</b> 번호가 통째로 사라진 경우")을 이 값이 잡는다.</para>
    ///
    /// ============================================================================
    /// ★ 문자열은 <b>키</b>다. 원문을 적으면 로드가 거부한다
    /// ============================================================================
    /// <c>docs/ARCHITECTURE.md</c> §528 "로컬라이즈 키 · 원문 문자열 금지". 그 규칙은 기본 42종에서
    /// <b>이미 한 번 어겨졌고</b>(§557 자기 정정 — <c>Resources/Items/*.asset</c> 에 한글 84건),
    /// 팩이 계속 늘면 팩당 (이름+설명) 2건씩 부채가 자란다.
    /// <para>그래서 이 자리는 <b>산문 규칙이 아니라 구조</b>로 막는다:
    /// 필드 이름이 <c>...Key</c> 이고, <see cref="PackRegistry"/> 가 로드할 때
    /// <see cref="PackManifestKeys.IsWellFormed"/> 로 <b>ASCII 키 모양</b>인지 검사해
    /// 아니면 그 팩을 <b>싣지 않는다</b>. 조용히 넘기지 않는 이유는 하나다 — 넘기면
    /// 한글이 들어간 팩이 그대로 출하되고, 그때는 이미 유저 화면에 있다.</para>
    /// <para>★ <c>grep '[가-힣]' *.asset</c> 은 이 위반을 <b>영원히 0건</b>으로 답한다
    /// (YAML이 <c>\uXXXX</c> 로 이스케이프한다). 그래서 감사는 <b>디코드한 뒤에</b> 센다 —
    /// <c>Tests/EditMode/PackManifestLocalizationDebtTests.cs</c>.</para>
    ///
    /// ============================================================================
    /// 팩이 늘어날 때 <b>무엇을 하지 않아도 되는가</b>
    /// ============================================================================
    ///  · 이 파일을 고치지 않는다. · <see cref="PackRegistry"/> 를 고치지 않는다.
    ///  · 어떤 <c>enum</c> 에도 값을 더하지 않는다. · 어떤 배열/목록에도 항목을 더하지 않는다.
    /// <b>새 팩 = 새 에셋 파일들</b>(매니페스트 1 + 아이템 n). 그게 전부다.
    /// </summary>
    [CreateAssetMenu(fileName = "PackManifest", menuName = "StickMate/Pack Manifest", order = 2)]
    public sealed class StickPackManifestSO : ScriptableObject
    {
        /// <summary>
        /// ★ 이 <b>스키마</b>의 현재 판. 팩이 늘어나는 것과 무관하고, <b>매니페스트에 필드가
        /// 늘어날 때만</b> 오른다.
        /// <para>왜 필요한가: 출시 뒤에도 팩을 계속 낸다면 <b>새 팩이 옛 앱에 놓이는 조합</b>이
        /// 반드시 생긴다(스팀은 DLC와 본편을 따로 갱신한다). 그때 옛 앱이 모르는 필드를 무시하고
        /// <b>반쯤 읽는</b> 것이 최악이다 — 화면은 뜨는데 뜻이 다르다. 세이브의 다운그레이드 방어와
        /// 같은 사고방식이다(<c>CharacterSaveStore</c>의 <c>HandleNewerVersionFile</c>).</para>
        /// <para><b>세이브 스키마와 아무 관계가 없다.</b> 팩 보유는 저장하지 않는다
        /// (ENTITLEMENT_CONTRACT §E-4-a · §E-8-b) — 그래서 이 값이 올라도
        /// <c>CharacterSaveStore.CurrentVersion</c> 은 움직이지 않는다.</para>
        /// <para>★ <b>v2 (2026-09-05)</b> — 팩이 싣는 아이템 에셋의 형상에 계약 v2 필드가 생겼다. 조각(<see cref="AccessoryWornShapeData"/>) 9개:
        /// <c>surfaces / strokeMult / strokeInR / noStroke / alpha / lineAlpha / underBack / layer / bodyFixed</c>,
        /// 아이템(<see cref="AccessoryDefSO"/>) 5개: <c>wornGroupAlpha / wornScale / wornScaleY / wornOffsetYInR / wornMirrorX</c>.
        /// (초안의 <c>bodyAlpha / fixedFill / fixedLine</c> 은 실사용 0 이라 같은 날 제거됐다 — 이름 <c>strokeGrade</c> 는 존재한 적이 없다.)
        /// 매니페스트 자체의 필드는 안 늘었지만 이 판이 지키는 것은 「옛 앱이 새 팩을 반쯤 읽는 것」이고, v1 앱은 <c>surfaces</c>를 몰라
        /// <b>카드 전용 조각을 몸에 그린다</b>. 그래서 그 필드를 쓰는 팩은 2를 요구해야 한다.
        /// v1 팩(키 없음)은 열네 필드가 전부 0/false = v1과 같은 뜻이라 v2 앱에서 그대로 옳다
        /// (<c>WornShapeContractCompatTests</c>가 잠근다).</para>
        ///
        /// <para>★★ <b>2026-09-08 — 열려 있는 판단 하나(리더 결재 대기).</b> 같은 날 아이템 에셋에
        /// 필드 둘이 생겼다: <see cref="AccessoryDefSO.themeKey"/> · <see cref="AccessoryDefSO.declaredSubStat"/>.
        /// <b>이 라운드는 판을 올리지 않았다</b>(<c>SchemaVersion</c>은 2 그대로).
        /// 근거와 남은 위험을 여기 적어 둔다 — 결정이 문서 밖에만 있으면 다음 사람이 못 찾는다.
        /// <list type="bullet">
        ///  <item><b>왜 안 올렸나</b>: 판을 올리고 팩이 3을 요구하면 <b>옛 앱이 그 팩을 통째로 거부</b>한다.
        ///        되돌릴 수 없는 방향이고, 오늘 트리에 <b>매니페스트 에셋이 0개</b>라 서두를 이유가 없다.</item>
        ///  <item><b>남은 위험</b>: v2 앱이 <b>팩 없이</b> 먼저 출하되고 나중에 그 앱에 새 팩이 팔리면,
        ///        옛 앱은 <c>themeKey</c>를 몰라 그 팩 아이템을 전부 <b>무소속</b>으로 싣는다 —
        ///        화면은 멀쩡한데 <b>세트 보너스만 영원히 안 붙는다</b>. 이 문단 맨 위가 말하는
        ///        「반쯤 읽는다」의 교과서적 사례다.</item>
        ///  <item><b>판단 기준은 하나다</b>: <b>팩보다 먼저 출하되는 앱 빌드가 존재하는가.</b>
        ///        없으면 판을 안 올려도 되고(첫 팩과 첫 팩-지원 앱이 같은 빌드다),
        ///        있으면 <b>올려야 한다</b> — 그때는 「거부」가 「조용히 약한 상품」보다 낫다.
        ///        그 사실은 <c>product-strategy</c>/리더만 안다.</item>
        /// </list></para>
        /// </summary>
        public const int SchemaVersion = 2;

        [Header("정체")]
        [Tooltip("역DNS 형식(pack.office). 엔타이틀먼트 키이자 세트 아이디다. 절대 바꾸지 말 것 — " +
                 "바꾸면 산 사람의 소유가 사라진다.")]
        public string packId;

        [Tooltip("이 팩 아이템들의 cohortId. 기본 42종은 0(ItemCatalog.BaseCohortId)이므로 팩은 " +
                 "0을 쓸 수 없다. 두 팩이 같은 값을 쓰면 등급이 서로 섞인다.")]
        public int cohortId;

        [Tooltip("이 매니페스트가 요구하는 스키마 판. 앱의 StickPackManifestSO.SchemaVersion 보다 " +
                 "크면 앱이 이 팩을 싣지 않고 크게 신고한다(반쯤 읽는 것보다 낫다).")]
        public int requiresSchemaVersion = 1;

        [Tooltip("팩 자체의 갱신 번호. 콘텐츠가 바뀌면 올린다. 판정에는 쓰이지 않고 로그·지원용이다.")]
        public int packVersion = 1;

        [Header("표시 — ★ 키만 적는다. 원문을 적으면 로드가 거부한다")]
        [Tooltip("표시 이름의 로컬라이즈 키(예: pack.office.name). ASCII 소문자/숫자/점만.")]
        public string displayNameKey;

        [Tooltip("설명의 로컬라이즈 키(예: pack.office.desc). ASCII 소문자/숫자/점만.")]
        public string descriptionKey;

        [Header("팔레트 — 동결값(PALETTE_SPEC §14-3)")]
        [Tooltip("이 팩이 색을 어디서 얻었는가. 배정이면 색상각 중복을 감사가 잡는다.")]
        public PackPaletteOrigin paletteOrigin = PackPaletteOrigin.DedicatedHue;

        [Tooltip("배정받은 색상각(도). ReusedCatalogColor 면 뜻이 없다.")]
        public float paletteHueDegrees;

        [Tooltip("주색 — 동결값. 규칙으로 매번 다시 유도하면 이미 산 팩의 색이 조용히 바뀐다.")]
        public Color primaryColor = Color.white;

        [Tooltip("보조색 — 동결값.")]
        public Color secondaryColor = Color.white;

        [Header("아이템 — 목록이 아니라 검사값이다")]
        [Tooltip("이 팩이 싣는 아이템 수. 실제로 로드된 같은 코호트 아이템 수와 다르면 결함이다 " +
                 "(마지막 번호가 통째로 빠진 결손은 이 값으로만 잡힌다).")]
        public int declaredItemCount;

        [Tooltip("이 팩 아이템들이 슬롯 안에서 차지하는 첫 자리 번호. 기본 42종이 0..5를 쓰므로 " +
                 "팩은 6 이상이고, 두 팩이 같은 값을 쓰면 자리가 겹쳐 엉뚱한 그림이 그려진다.")]
        public int itemIndexBase;

        [Header("소리 — 키만. AudioClip 참조는 여기 두지 않는다")]
        [Tooltip("이 팩이 교체하는 효과음 키(design/sound/SFX_CATALOG.md §3-2의 교체 6키). " +
                 "실제 클립 배선은 사운드 라운드 소관이고, 여기는 '무엇을 교체하는가'만 적는다.")]
        public string[] soundKeys;

        [Header("대사")]
        [Tooltip("이 팩의 대사 세트 키. 비면 중립 풀을 그대로 쓴다(세트를 만들지 않는 팩도 있다).")]
        public string dialogueSetKey;

        [Header("판매 창구")]
        [Tooltip("채널별 식별자. 비어 있으면 어디서도 팔지 않는다(= 아직 상품이 아니다).")]
        public PackEntitlementRef[] entitlements;
    }

    /// <summary>
    /// 로컬라이즈 <b>키 모양</b>의 유일한 판정자. 매니페스트도 테스트도 감사도 이 함수 하나를 부른다 —
    /// 두 벌로 적으면 그 순간 "검사는 통과했는데 화면에 한글이 박힌" 상태가 만들어진다.
    /// </summary>
    public static class PackManifestKeys
    {
        /// <summary>키에 허용되는 최대 길이. 값 자체에 뜻이 있는 것이 아니라,
        /// <b>원문 문장이 키 자리에 들어오는 것</b>을 길이로도 한 번 더 막는 자리다.</summary>
        public const int MaxKeyLength = 96;

        /// <summary>
        /// <paramref name="key"/> 가 <b>ASCII 키</b>인가. 소문자·숫자·점·밑줄만 허용하고
        /// 점으로 시작하거나 끝날 수 없으며 빈 마디(<c>..</c>)를 허용하지 않는다.
        ///
        /// <para>★ 한글 한 글자라도 들어오면 <c>false</c> 다 — 이 함수가 지키려는 것이 정확히 그것이다.
        /// 그리고 대문자를 막는 이유는 <b>같은 키가 두 표기로 갈리는 것</b>을 막기 위해서다
        /// (<c>Pack.Office.Name</c> 과 <c>pack.office.name</c> 은 사람에게는 같고 표에는 다르다).</para>
        /// </summary>
        public static bool IsWellFormed(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key.Length > MaxKeyLength) return false;
            if (key[0] == '.' || key[key.Length - 1] == '.') return false;

            bool previousWasDot = false;
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c == '.')
                {
                    if (previousWasDot) return false;
                    previousWasDot = true;
                    continue;
                }
                previousWasDot = false;

                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>
        /// ASCII 밖의 글자가 하나라도 있는가. <see cref="IsWellFormed"/> 가 이미 막지만,
        /// <b>감사가 "왜 거부됐는가"를 사람에게 구분해 말하려면</b> 이 사실이 따로 필요하다
        /// ("키 모양이 아니다"와 "원문을 적었다"는 고치는 방법이 다르다).
        /// </summary>
        public static bool ContainsNonAscii(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++) if (value[i] > 0x7F) return true;
            return false;
        }
    }
}
