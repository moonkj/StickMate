using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 팩 하나의 <b>런타임 사실</b>. 매니페스트 에셋에서 온 값 + 아이템 조인 결과 하나
    /// (<see cref="ResolvedItemCount"/>)를 함께 들고 다닌다.
    /// <para>에셋을 그대로 들고 다니지 <b>않는</b> 이유는 <c>ItemCatalogEntry</c>가
    /// <c>AccessoryDefSO</c>를 안 들고 다니는 이유와 같다 — 소비자가 에셋 필드를 직접 쓰기 시작하면
    /// 검증을 통과하지 <b>않은</b> 값이 화면에 닿는 경로가 생긴다. 여기 앉은 값은
    /// <see cref="PackRegistry.Build"/>를 통과한 것뿐이다.</para>
    /// </summary>
    public sealed class PackDescriptor
    {
        public readonly string PackId;
        public readonly int CohortId;
        public readonly string DisplayNameKey;
        public readonly string DescriptionKey;
        public readonly PackPaletteOrigin PaletteOrigin;
        public readonly float PaletteHueDegrees;
        public readonly Color PrimaryColor;
        public readonly Color SecondaryColor;
        public readonly int DeclaredItemCount;
        public readonly int ItemIndexBase;
        public readonly string DialogueSetKey;
        public readonly int PackVersion;

        /// <summary>실제로 로드된 같은 코호트 아이템 수. <see cref="DeclaredItemCount"/>와 다르면
        /// <see cref="PackRegistry.Build"/>가 결함으로 신고했고 이 팩은 색인에 들어오지 않는다.</summary>
        public readonly int ResolvedItemCount;

        private readonly string[] _soundKeys;
        private readonly PackEntitlementRef[] _entitlements;

        internal PackDescriptor(StickPackManifestSO manifest, int resolvedItemCount)
        {
            PackId = manifest.packId;
            CohortId = manifest.cohortId;
            DisplayNameKey = manifest.displayNameKey;
            DescriptionKey = manifest.descriptionKey;
            PaletteOrigin = manifest.paletteOrigin;
            PaletteHueDegrees = manifest.paletteHueDegrees;
            PrimaryColor = manifest.primaryColor;
            SecondaryColor = manifest.secondaryColor;
            DeclaredItemCount = manifest.declaredItemCount;
            ItemIndexBase = manifest.itemIndexBase;
            DialogueSetKey = manifest.dialogueSetKey;
            PackVersion = manifest.packVersion;
            ResolvedItemCount = resolvedItemCount;

            _soundKeys = manifest.soundKeys != null
                ? (string[])manifest.soundKeys.Clone()
                : new string[0];
            _entitlements = manifest.entitlements != null
                ? (PackEntitlementRef[])manifest.entitlements.Clone()
                : new PackEntitlementRef[0];
        }

        /// <summary>이 팩이 교체하는 효과음 키. <b>복사본을 들고 있다</b> —
        /// 안 하면 에셋의 배열이 곧 런타임 배열이라 누가 한 칸만 써도 에디터에서 에셋이 더러워진다
        /// (<c>AccessoryDefSO.BuildIcon</c>이 같은 이유로 복사한다).</summary>
        public IReadOnlyList<string> SoundKeys => _soundKeys;

        /// <summary>채널별 식별자.</summary>
        public IReadOnlyList<PackEntitlementRef> Entitlements => _entitlements;
    }

    /// <summary>
    /// ============================================================================
    /// ★ 팩 발견의 <b>유일한 창구</b> — 여기에 팩 목록이 하드코딩되면 통로가 아니라 벽이다
    /// ============================================================================
    /// 사용자 확정 2026-09-03: <b>"출시 이후부터 계속 추가팩 만들거야"</b>.
    /// 그래서 이 파일이 답해야 하는 질문은 <b>"여섯 팩을 어떻게 싣는가"</b>가 아니라
    /// <b>"N번째 팩을 코드 수정 없이 어떻게 싣는가"</b>다.
    ///
    /// <para><b>답: 폴더를 훑는다.</b> <c>Resources/Items</c>에 매니페스트 에셋을 하나 떨어뜨리면
    /// 그게 곧 새 팩이다. 이 파일에도, <see cref="StickPackManifestSO"/>에도, 어떤 <c>enum</c>에도,
    /// 어떤 배열에도 항목을 더하지 않는다. 그 성질을
    /// <c>Tests/EditMode/PackManifestCorridorTests.cs</c>가 <b>합성 7번째 팩</b>으로 직접 잰다
    /// (그리고 같은 파일이 <b>하드코딩 판</b>을 나란히 세워 그 판은 7번째에서 넘어지는 것을 보인다 —
    /// 양성 대조 없는 "통과"는 아무것도 증명하지 않는다).</para>
    ///
    /// ============================================================================
    /// ★ <c>Resources.LoadAll</c>은 이미 지목된 위험 지점이다 — 그래서 이렇게 다룬다
    /// ============================================================================
    /// <c>docs/localization/PLAN_1.0.md</c> §3-2의 3번: <i>"<c>Resources.LoadAll</c>은 이미 위험 지점이다 —
    /// <c>ItemCatalog.EnsureLoaded</c>가 도메인 리로드 문제 때문에 지연 로드로 우회하고 있다."</i>
    /// 그 지적을 그대로 받아 네 가지를 지킨다:
    /// <list type="number">
    ///  <item><b>정적 초기화자에 두지 않는다.</b> <c>Resources.LoadAll</c>은 도메인 리로드/직렬화
    ///    도중에 부르면 안 되는 API다. "타입을 건드리는 순간"이 아니라 <b>"목록을 실제로 쓰는 순간"</b>까지 미룬다.</item>
    ///  <item><b>성공/실패를 가리지 않고 한 번만</b> 읽는다. 실패했다고 매 접근마다 다시 읽으면
    ///    고장난 빌드에서 <c>LoadAll</c>이 프레임마다 도는 최악이 된다(종일 켜 두는 앱이다).</item>
    ///  <item><b>새 폴더를 만들지 않는다.</b> 아이템과 같은 <c>Resources/Items</c>를 훑는다 —
    ///    <c>LoadAll&lt;T&gt;</c>는 타입으로 거르므로 서로 간섭하지 않고,
    ///    <b>빈 폴더는 "팩 없음"과 "폴더 이름 오타"를 구분할 수 없다</b>는 함정을 원천에서 없앤다
    ///    (그 폴더에는 아이템 42개가 실재하므로 경로가 죽으면 <b>보관함이 통째로 비어</b> 즉시 드러난다).</item>
    ///  <item><b>실패는 조용하지 않다.</b> 결함은 <c>Debug.LogError</c>로 한 번 크게 남기고,
    ///    결함 있는 팩은 <b>싣지 않는다</b>. 반쯤 실린 팩은 화면만 멀쩡하고 뜻이 다르다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 소속은 <b>아이템</b>이 선언한다 — 매니페스트는 그 번호의 주인일 뿐이다
    /// ============================================================================
    /// 매니페스트가 <c>AccessoryDefSO[]</c>를 들고 있으면 "이 아이템이 어느 팩 것인가"가
    /// <b>두 곳</b>에 앉는다. 그래서 조인 키는 <see cref="AccessoryDefSO.cohortId"/> 하나이고,
    /// 조인은 <see cref="Build"/> 한 곳에서만 일어난다.
    ///
    /// <para>★ 이 구조가 새로 잡아 주는 결함이 하나 있다 — <b>고아 코호트</b>.
    /// 아이템이 <c>cohortId = 3</c>이라고 적었는데 3번 팩의 매니페스트가 없으면, 지금까지는
    /// 아무도 못 봤다(<c>ItemCatalog.AuditDeclarations</c>는 코호트 <b>안</b>의 일관성만 본다).
    /// 그 아이템은 <b>영원히 팔 수 없는 상태로 카탈로그에 앉아 있게</b> 된다.</para>
    /// </summary>
    public static class PackRegistry
    {
        private const string LogPrefix = "[팩]";

        /// <summary>매니페스트를 찾는 폴더. <b>아이템과 같은 폴더다</b> — 위 문단 4번 참고.
        /// <para><c>ItemCatalog</c>가 같은 문자열을 들고 있으면 그 순간 폴더가 두 곳에서 정해지므로
        /// <see cref="ItemCatalog.ItemResourceFolder"/>를 그대로 참조한다.</para></summary>
        internal static string ResourceFolder => ItemCatalog.ItemResourceFolder;

        private static PackDescriptor[] _packs;

        /// <summary>지금 실린 팩. 오늘은 <b>0개</b>이고 그건 정상이다 — 매니페스트 에셋이 아직 없다.</summary>
        public static IReadOnlyList<PackDescriptor> Packs
        {
            get { EnsureLoaded(); return _packs; }
        }

        public static int Count
        {
            get { EnsureLoaded(); return _packs.Length; }
        }

        /// <summary>아이디로 찾는다. 없으면 <c>null</c>.</summary>
        public static PackDescriptor Find(string packId)
        {
            if (string.IsNullOrEmpty(packId)) return null;
            EnsureLoaded();
            for (int i = 0; i < _packs.Length; i++)
            {
                if (_packs[i].PackId == packId) return _packs[i];
            }
            return null;
        }

        /// <summary>코호트 번호로 찾는다. 기본 코호트(<see cref="ItemCatalog.BaseCohortId"/>)는
        /// 팩이 아니므로 언제나 <c>null</c>이다.</summary>
        public static PackDescriptor FindByCohort(int cohortId)
        {
            if (cohortId == ItemCatalog.BaseCohortId) return null;
            EnsureLoaded();
            for (int i = 0; i < _packs.Length; i++)
            {
                if (_packs[i].CohortId == cohortId) return _packs[i];
            }
            return null;
        }

        /// <summary>
        /// 이 아이템이 팩 소속인가. <b>기본 42종은 <c>false</c></b>이고 그건 결함이 아니다 —
        /// 기본 아이템은 레벨로 열리는 A·B층이라 C층 판정 대상이 아니다.
        /// <para>소속이면 <see cref="PackEntitlements.StateOf"/>에 <c>descriptor.PackId</c>를
        /// 넘기는 것이 다음 단계다. 이 함수가 상태까지 돌려주지 <b>않는</b> 이유:
        /// 그러면 "팩 소속인가"와 "가졌는가"라는 <b>성질이 다른 두 사실</b>이 한 반환값에 섞이고,
        /// 기본 아이템에 대해 무엇을 돌려줘야 할지가 그 자리에서 애매해진다.</para>
        /// </summary>
        public static bool TryFindPackOfItem(ItemCatalogEntry entry, out PackDescriptor descriptor)
        {
            descriptor = entry == null ? null : FindByCohort(entry.CohortId);
            return descriptor != null;
        }

        private static void EnsureLoaded()
        {
            if (_packs != null) return;

            StickPackManifestSO[] manifests = Resources.LoadAll<StickPackManifestSO>(ResourceFolder);
            var faults = new List<string>();
            _packs = Build(manifests, ItemCatalog.Entries, faults);

            for (int i = 0; i < faults.Count; i++) Debug.LogError($"{LogPrefix} {faults[i]}");
        }

        /// <summary>테스트 전용 — 다음 접근에서 폴더를 다시 훑게 한다.</summary>
        internal static void ResetForTesting() => _packs = null;

        /// <summary>
        /// ★ <b>매니페스트 배열 -> 실린 팩 배열</b>. 순수 함수이고 <b>판정은 여기 한 곳뿐이다.</b>
        ///
        /// <para><c>EnsureLoaded</c> 안에 인라인으로 두지 않은 이유는 <b>검증 가능성</b>이다.
        /// 그 자리는 <c>Resources.LoadAll</c>이 물고 있어 테스트가 값을 넣어 볼 수 없고,
        /// <c>ItemCatalog</c>가 정확히 그 사각지대에서 <c>cohortId</c>를 안 넘긴 채 지나간 전례가 있다
        /// (<see cref="ItemCatalog.EntryFrom"/> 문단). 여기로 꺼내면 <b>합성 7번째 팩</b>을 직접 먹일 수 있다.</para>
        ///
        /// <para><b>결함은 고치지 않고 신고한다.</b> 예컨대 코호트가 겹쳤을 때 한쪽 번호를 조용히
        /// 바꿔 주고 싶은 유혹이 있는데, 그러면 증상이 사라지고 아무도 안 고친다 —
        /// <c>ItemCatalog.AuditDeclarations</c>가 같은 이유로 값을 안 고친다.</para>
        /// </summary>
        /// <param name="manifests">폴더에서 나온 그대로. <c>null</c> 항목이 있어도 죽지 않는다.</param>
        /// <param name="items">아이템 전량(코호트 조인용). <c>null</c>이면 조인 검사만 건너뛴다.</param>
        /// <param name="faults">사람이 읽을 결함 문장이 <b>추가</b>된다(비우지 않는다).</param>
        internal static PackDescriptor[] Build(IReadOnlyList<StickPackManifestSO> manifests,
            IReadOnlyList<ItemCatalogEntry> items, List<string> faults)
        {
            if (faults == null) faults = new List<string>();

            // ★ 매니페스트가 0개여도 <b>여기서 끝내지 않는다.</b> 그 경우가 바로 고아 코호트가
            //   가장 잘 생기는 자리다(아이템은 팩 소속이라 적었는데 매니페스트 파일이 통째로 빠진 것).
            //   조기 반환을 두면 그 결함이 <b>정확히 가장 심할 때만</b> 안 보인다.
            var accepted = new List<StickPackManifestSO>();
            int manifestCount = manifests == null ? 0 : manifests.Count;
            for (int i = 0; i < manifestCount; i++)
            {
                StickPackManifestSO m = manifests[i];
                if (m == null)
                {
                    faults.Add($"매니페스트 목록 {i}번이 비어 있습니다(에셋이 깨졌거나 스크립트 참조가 끊겼습니다).");
                    continue;
                }
                if (Accepts(m, accepted, faults)) accepted.Add(m);
            }

            var result = new List<PackDescriptor>(accepted.Count);
            for (int i = 0; i < accepted.Count; i++)
            {
                StickPackManifestSO m = accepted[i];
                int resolved = CountItemsInCohort(items, m.cohortId);

                // ★ items가 null이면 조인 자체가 없는 것이므로 개수 검사를 하지 않는다.
                //   여기서 "0개"라고 신고하면 조인을 안 준 호출자에게 없는 결함을 보고하게 된다.
                if (items != null && resolved != m.declaredItemCount)
                {
                    faults.Add($"'{m.packId}'가 아이템 {m.declaredItemCount}개를 선언했는데 코호트 " +
                        $"{m.cohortId}로 실제로 실린 것은 {resolved}개입니다. " +
                        "카테고리의 마지막 번호가 통째로 빠지면 ItemCatalog는 그것을 '원래 그만큼이었다'로 " +
                        "읽습니다 — 그 결손을 잡는 것이 이 선언의 유일한 목적입니다.");
                    continue;
                }

                // ★ 자리대 선언이 <b>실물과 맞는가</b>. 이 검사가 없으면 itemIndexBase 는 장식이 된다 —
                //   그리고 장식은 보증처럼 생겼다. 다음 팩을 만드는 사람이 "6~11은 이미 찼구나"를
                //   이 표에서 읽는데, 표가 실물과 갈라져 있으면 그 판단이 통째로 틀린다.
                //   (겹침 자체는 ItemCatalog.EnsureLoaded 가 "자리를 두 아이템이 다툽니다"로 크게 신고하지만,
                //    <b>어느 팩의 선언이 거짓이었는지</b>는 거기서 알 수 없다.)
                if (items != null && TryFindMisplaced(items, m, out string misplaced, out int misplacedIndex))
                {
                    faults.Add($"'{m.packId}'가 자리 {m.itemIndexBase}번부터 쓴다고 선언했는데 " +
                        $"'{misplaced}'가 {misplacedIndex}번에 앉아 있습니다. " +
                        "선언이 실물과 갈라지면 다음 팩을 만드는 사람이 빈 자리를 잘못 고르고, " +
                        "그때 두 팩의 아이템이 같은 자리를 다퉈 한쪽이 통째로 사라집니다.");
                    continue;
                }

                result.Add(new PackDescriptor(m, resolved));
            }

            AuditOrphanCohorts(items, accepted, faults);
            return result.ToArray();
        }

        /// <summary>
        /// 이 매니페스트를 실을 것인가. <b>거부 사유를 전부 모아서</b> 신고한다(첫 번째에서 끊지 않는다) —
        /// 하나 고칠 때마다 다시 돌려 다음 결함을 만나는 것은 팩을 만드는 사람에게 낭비다.
        /// </summary>
        private static bool Accepts(StickPackManifestSO m, List<StickPackManifestSO> earlier, List<string> faults)
        {
            int before = faults.Count;
            string who = string.IsNullOrEmpty(m.packId) ? $"이름 없는 매니페스트('{m.name}')" : $"'{m.packId}'";

            // ---- 정체 ----
            if (!PackManifestKeys.IsWellFormed(m.packId))
            {
                faults.Add($"{who}의 packId가 키 모양이 아닙니다(ASCII 소문자·숫자·점·밑줄, " +
                    $"최대 {PackManifestKeys.MaxKeyLength}자). packId는 엔타이틀먼트 키이자 세트 아이디입니다.");
            }
            if (m.cohortId == ItemCatalog.BaseCohortId)
            {
                faults.Add($"{who}가 기본 코호트({ItemCatalog.BaseCohortId})를 씁니다. " +
                    "팩이 기본 코호트에 합류하면 기본 42종의 등급이 통째로 미끄러집니다 — " +
                    "증상은 팩을 산 사람이 아니라 <b>안 산 사람</b>에게 나타납니다.");
            }
            if (m.requiresSchemaVersion > StickPackManifestSO.SchemaVersion)
            {
                faults.Add($"{who}는 매니페스트 스키마 v{m.requiresSchemaVersion}를 요구하는데 " +
                    $"이 앱은 v{StickPackManifestSO.SchemaVersion}입니다. 앱이 모르는 필드를 무시하고 " +
                    "반쯤 읽으면 화면은 뜨는데 뜻이 다릅니다 — 앱을 갱신해야 이 팩이 실립니다.");
            }

            // ---- 문자열은 키다(원문 금지) ----
            RequireKey(m.displayNameKey, $"{who}의 displayNameKey", faults);
            RequireKey(m.descriptionKey, $"{who}의 descriptionKey", faults);
            if (!string.IsNullOrEmpty(m.dialogueSetKey))
            {
                RequireKey(m.dialogueSetKey, $"{who}의 dialogueSetKey", faults);
            }
            if (m.soundKeys != null)
            {
                for (int i = 0; i < m.soundKeys.Length; i++)
                {
                    RequireKey(m.soundKeys[i], $"{who}의 soundKeys[{i}]", faults);
                }
            }

            // ---- 자리 ----
            if (m.declaredItemCount <= 0)
            {
                faults.Add($"{who}의 declaredItemCount가 {m.declaredItemCount}입니다. " +
                    "아이템이 없는 팩은 상품이 아닙니다.");
            }
            if (m.itemIndexBase <= 0)
            {
                faults.Add($"{who}의 itemIndexBase가 {m.itemIndexBase}입니다. " +
                    "0번대는 기본 42종의 자리이고, 겹치면 그 자리를 두 아이템이 다퉈 엉뚱한 그림이 그려집니다.");
            }

            // ---- 판매 창구 ----
            if (m.entitlements != null)
            {
                for (int i = 0; i < m.entitlements.Length; i++)
                {
                    if (string.IsNullOrEmpty(m.entitlements[i].entitlementId))
                    {
                        faults.Add($"{who}의 entitlements[{i}]({m.entitlements[i].channel})에 식별자가 없습니다. " +
                            "빈 식별자는 '무료'가 아니라 '조회 불가'가 됩니다 — 안 팔 채널은 항목 자체를 지우십시오.");
                    }
                    for (int j = 0; j < i; j++)
                    {
                        if (m.entitlements[j].channel != m.entitlements[i].channel) continue;
                        faults.Add($"{who}가 {m.entitlements[i].channel} 채널을 두 번 적었습니다. " +
                            "한 팩은 한 채널에서 하나입니다.");
                        break;
                    }
                }
            }

            // ---- 앞서 실린 팩과의 충돌 ----
            for (int i = 0; i < earlier.Count; i++)
            {
                StickPackManifestSO other = earlier[i];

                if (other.packId == m.packId)
                {
                    faults.Add($"packId '{m.packId}'를 매니페스트 둘이 씁니다('{other.name}' vs '{m.name}'). " +
                        "packId는 엔타이틀먼트 키라 겹치면 한쪽을 산 사람이 다른 쪽도 가진 것이 됩니다.");
                }
                if (other.cohortId == m.cohortId)
                {
                    faults.Add($"코호트 {m.cohortId}를 팩 둘이 씁니다('{other.packId}' vs '{m.packId}'). " +
                        "코호트는 등급 모집단이라 겹치면 두 팩의 등급이 서로 섞입니다.");
                }
                if (other.itemIndexBase == m.itemIndexBase)
                {
                    faults.Add($"itemIndexBase {m.itemIndexBase}를 팩 둘이 씁니다('{other.packId}' vs '{m.packId}'). " +
                        "자리 번호가 겹치면 ItemCatalog가 '자리를 두 아이템이 다툽니다'로 신고하고 " +
                        "한쪽이 통째로 사라집니다.");
                }
                if (other.paletteOrigin == PackPaletteOrigin.DedicatedHue
                    && m.paletteOrigin == PackPaletteOrigin.DedicatedHue
                    && other.paletteHueDegrees == m.paletteHueDegrees)
                {
                    faults.Add($"색상각 {m.paletteHueDegrees}도를 팩 둘이 <b>배정</b>받았습니다" +
                        $"('{other.packId}' vs '{m.packId}'). 재사용이 의도라면 " +
                        $"paletteOrigin을 {PackPaletteOrigin.ReusedCatalogColor}로 적으십시오 — " +
                        "그러면 이 검사를 지나갑니다.");
                }
                if (!string.IsNullOrEmpty(m.dialogueSetKey) && other.dialogueSetKey == m.dialogueSetKey)
                {
                    faults.Add($"대사 세트 키 '{m.dialogueSetKey}'를 팩 둘이 씁니다" +
                        $"('{other.packId}' vs '{m.packId}'). 세트가 겹치면 어조가 팩을 구분하지 못합니다.");
                }
                AuditSharedEntitlement(other, m, faults);
            }

            return faults.Count == before;
        }

        /// <summary>두 팩이 <b>같은 채널의 같은 식별자</b>를 가리키면 하나를 산 사람이 둘 다 갖게 된다.</summary>
        private static void AuditSharedEntitlement(StickPackManifestSO a, StickPackManifestSO b, List<string> faults)
        {
            if (a.entitlements == null || b.entitlements == null) return;
            for (int i = 0; i < a.entitlements.Length; i++)
            {
                for (int j = 0; j < b.entitlements.Length; j++)
                {
                    if (a.entitlements[i].channel != b.entitlements[j].channel) continue;
                    if (string.IsNullOrEmpty(a.entitlements[i].entitlementId)) continue;
                    if (a.entitlements[i].entitlementId != b.entitlements[j].entitlementId) continue;

                    faults.Add($"'{a.packId}'와 '{b.packId}'가 {a.entitlements[i].channel} 채널에서 " +
                        $"같은 식별자 '{a.entitlements[i].entitlementId}'를 가리킵니다. " +
                        "한쪽을 산 사람이 다른 쪽도 가진 것이 됩니다.");
                }
            }
        }

        private static void RequireKey(string value, string what, List<string> faults)
        {
            if (PackManifestKeys.IsWellFormed(value)) return;

            if (PackManifestKeys.ContainsNonAscii(value))
            {
                faults.Add($"{what}에 원문 문자열이 들어 있습니다(\"{value}\"). " +
                    "매니페스트는 <b>키</b>만 담습니다(docs/ARCHITECTURE.md 로컬라이즈 규칙) — " +
                    "팩이 계속 늘어나면 팩마다 (이름+설명) 2건씩 번역 부채가 자랍니다. " +
                    "★ 이 위반은 grep '[가-힣]'로는 영원히 0건입니다(YAML이 \\uXXXX로 이스케이프합니다).");
                return;
            }
            faults.Add($"{what}가 키 모양이 아닙니다(\"{value}\"). " +
                $"ASCII 소문자·숫자·점·밑줄만, 최대 {PackManifestKeys.MaxKeyLength}자, " +
                "점으로 시작·끝나거나 빈 마디를 둘 수 없습니다.");
        }

        /// <summary>이 팩의 아이템 중 <b>선언한 자리대 아래</b>에 앉은 것이 있으면 첫 번째를 돌려준다.
        /// <para>상한은 보지 <b>않는다</b>: 팩이 슬롯마다 몇 종을 넣는지는 데이터이고, 그 수를
        /// 여기서 다시 정하면 매니페스트가 말한 것을 코드가 한 번 더 말하게 된다. 아래쪽만 막아도
        /// 「기본 42종의 자리를 침범했다」는 실제 사고 형태는 전부 걸린다.</para></summary>
        private static bool TryFindMisplaced(IReadOnlyList<ItemCatalogEntry> items, StickPackManifestSO m,
            out string itemId, out int itemIndex)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemCatalogEntry e = items[i];
                if (e == null || e.CohortId != m.cohortId) continue;
                if (e.ItemIndex >= m.itemIndexBase) continue;

                itemId = e.Id;
                itemIndex = e.ItemIndex;
                return true;
            }
            itemId = null;
            itemIndex = -1;
            return false;
        }

        private static int CountItemsInCohort(IReadOnlyList<ItemCatalogEntry> items, int cohortId)
        {
            if (items == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemCatalogEntry e = items[i];
                if (e != null && e.CohortId == cohortId) n++;
            }
            return n;
        }

        /// <summary>
        /// ★ <b>고아 코호트</b> — 아이템이 팩 소속이라고 적었는데 그 팩의 매니페스트가 없다.
        /// <para>지금까지 이 결함을 보는 눈이 없었다. <c>ItemCatalog.AuditDeclarations</c>는 코호트
        /// <b>안</b>의 일관성만 보고, 그 코호트에 <b>주인이 있는지</b>는 묻지 않는다.
        /// 증상은 조용하다 — 그 아이템은 카탈로그에 앉아 있고 등급도 나오는데 <b>살 방법이 없다.</b></para>
        /// </summary>
        private static void AuditOrphanCohorts(IReadOnlyList<ItemCatalogEntry> items,
            List<StickPackManifestSO> accepted, List<string> faults)
        {
            if (items == null) return;

            var reported = new List<int>();
            for (int i = 0; i < items.Count; i++)
            {
                ItemCatalogEntry e = items[i];
                if (e == null || e.CohortId == ItemCatalog.BaseCohortId) continue;
                if (reported.Contains(e.CohortId)) continue;

                bool owned = false;
                for (int j = 0; j < accepted.Count; j++)
                {
                    if (accepted[j].cohortId != e.CohortId) continue;
                    owned = true;
                    break;
                }
                if (owned) continue;

                reported.Add(e.CohortId);
                faults.Add($"'{e.Id}'가 코호트 {e.CohortId}에 속한다고 적었는데 그 번호를 가진 팩 " +
                    "매니페스트가 없습니다(없거나 결함으로 거부됐습니다). " +
                    "이 아이템은 카탈로그에 보이고 등급도 나오지만 <b>살 방법이 없습니다</b> — " +
                    "조용한 결함이라 화면만 봐서는 못 찾습니다.");
            }
        }
    }
}
