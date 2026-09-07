using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 코스튬 하나의 <b>런타임 사실</b>. 매니페스트 에셋을 그대로 들고 다니지 <b>않는</b> 이유는
    /// <see cref="PackDescriptor"/>와 같다 — 소비자가 에셋 필드를 직접 쓰기 시작하면
    /// <b>검증을 통과하지 않은 값</b>이 화면에 닿는 경로가 생긴다. 여기 앉은 값은
    /// <see cref="CostumeCatalog.Build"/>를 통과한 것뿐이다.
    /// </summary>
    public sealed class CostumeDescriptor
    {
        /// <summary>세이브 파일에 앉는 안정 식별자(<c>costume.office</c>).</summary>
        public readonly string CostumeKey;

        public readonly CostumeSourceKind SourceKind;

        /// <summary><see cref="CostumeSourceKind.BaseTheme"/>면 테마 키(<c>office</c>),
        /// <see cref="CostumeSourceKind.Pack"/>이면 <c>packId</c>(<c>pack.cyber</c>).</summary>
        public readonly string SourceId;

        public readonly string DisplayNameKey;
        public readonly float PropAnchorOffsetXInH;
        public readonly int ManifestSchemaVersion;

        /// <summary>이 코스튬의 전용 모션 표(LFVS). <b><c>null</c>이면 전용 모션 없음</b>이고 그건
        /// 결함이 아니라 선언된 사실이다 — 무료 코스튬은 소환 오브젝트만 출하한다(티어 게이트 P-13).
        /// <para>★ <b>여기서 검증하지 않는다</b>: 표의 합법성(스텝레이트가 15의 약수인가 등)은
        /// <see cref="CostumeKeyposeTableSO.IsUsable"/> 한 곳이 판정하고, 소비자가 쓰기 직전에 묻는다.
        /// 판정을 두 곳에 두면 「카탈로그는 실었는데 포즈 층은 거부하는」 상태가 만들어진다.</para></summary>
        public readonly CostumeKeyposeTableSO Keyposes;

        private readonly AccessoryWornShapeData[] _propShapes;
        private readonly CostumeStageOverride[] _stageShapes;

        internal CostumeDescriptor(CostumeManifestSO manifest)
        {
            CostumeKey = manifest.costumeKey;
            SourceKind = manifest.sourceKind;
            SourceId = manifest.sourceId;
            DisplayNameKey = manifest.displayNameKey;
            PropAnchorOffsetXInH = manifest.propAnchorOffsetXInH;
            ManifestSchemaVersion = manifest.requiresSchemaVersion;
            Keyposes = manifest.keyposes;

            // ★ 복사본을 들고 있다 — 안 하면 에셋의 배열이 곧 런타임 배열이라 누가 한 칸만 써도
            //   에디터에서 에셋이 더러워진다(PackDescriptor.SoundKeys가 같은 이유로 복사한다).
            _propShapes = manifest.propShapes != null
                ? (AccessoryWornShapeData[])manifest.propShapes.Clone()
                : new AccessoryWornShapeData[0];
            _stageShapes = manifest.stageShapes != null
                ? (CostumeStageOverride[])manifest.stageShapes.Clone()
                : new CostumeStageOverride[0];
        }

        /// <summary>프롭 조각. <b>비면 프롭이 없는 것</b>이고 그건 결함이 아니라 선언된 사실이다.</summary>
        public IReadOnlyList<AccessoryWornShapeData> PropShapes => _propShapes;

        /// <summary>단계별 조형 차이. 비면 단계가 올라도 외형이 안 바뀐다.</summary>
        public IReadOnlyList<CostumeStageOverride> StageShapes => _stageShapes;

        /// <summary>이 코스튬이 <b>팩 소속</b>인가. 기본 코호트 테마 코스튬은 <c>false</c>이고,
        /// 그 갈래에는 <b>물어볼 엔타이틀먼트가 없다</b>(무료는 「안 묻는 것」이다).</summary>
        public bool IsPackSourced => SourceKind == CostumeSourceKind.Pack;
    }

    /// <summary>
    /// ============================================================================
    /// ★ 코스튬 발견의 <b>유일한 창구</b> — 여기에 코스튬 목록이 하드코딩되면 통로가 아니라 벽이다
    /// ============================================================================
    /// 답해야 하는 질문은 <i>"네 코스튬을 어떻게 싣는가"</i>가 아니라
    /// <b>"아홉 번째 코스튬을 코드 수정 없이 어떻게 싣는가"</b>다.
    /// <b>답: 폴더를 훑는다.</b> <c>Resources/Items</c>에 매니페스트 에셋을 하나 떨어뜨리면 그게 곧
    /// 새 코스튬이다. 이 파일에도, <see cref="CostumeManifestSO"/>에도, 어떤 <c>enum</c>에도,
    /// 어떤 배열에도 항목을 더하지 않는다.
    ///
    /// <para><b>어법은 <see cref="PackRegistry"/>와 한 글자도 다르지 않게 맞췄다</b> —
    /// 지연 로드(정적 초기화자 금지) · 성공/실패를 가리지 않고 한 번만 · 새 폴더를 만들지 않음 ·
    /// 실패는 조용하지 않음(<c>LogError</c> + 결함 있는 코스튬은 <b>싣지 않는다</b>).
    /// 그 네 가지의 근거는 <see cref="PackRegistry"/> 클래스 문서에 그대로 있다.</para>
    ///
    /// ============================================================================
    /// 결함은 <b>고치지 않고 신고한다</b>
    /// ============================================================================
    /// 예컨대 두 코스튬이 같은 소속을 주장하면 한쪽을 조용히 버리고 싶은 유혹이 있는데,
    /// 그러면 증상이 사라지고 아무도 안 고친다. 둘 다 안 싣고 크게 신고한다 —
    /// <see cref="PackRegistry.Build"/> · <c>ItemCatalog.AuditDeclarations</c>와 같은 방침이다.
    ///
    /// ============================================================================
    /// ★ 이 클래스는 <b>개방을 판정하지 않는다</b>
    /// ============================================================================
    /// «이 코스튬이 존재하는가»와 «지금 이 사용자에게 열려 있는가»는 성질이 다른 두 사실이다.
    /// 개방은 <see cref="CostumeEntitlement"/> 한 파일에만 있다(계약서 I-5) —
    /// 여기서 상태까지 돌려주면 그 판정이 두 곳이 되고, 「반만 열린」 상태가 만들어진다.
    /// </summary>
    public static class CostumeCatalog
    {
        private const string LogPrefix = "[코스튬]";

        /// <summary>매니페스트를 찾는 폴더. <b>아이템·팩과 같은 폴더다</b> —
        /// <c>LoadAll&lt;T&gt;</c>는 타입으로 거르므로 서로 간섭하지 않고,
        /// <b>빈 폴더는 "코스튬 없음"과 "폴더 이름 오타"를 구분할 수 없다</b>는 함정을 원천에서 없앤다
        /// (그 폴더에는 아이템 42개가 실재하므로 경로가 죽으면 보관함이 통째로 비어 즉시 드러난다).</summary>
        internal static string ResourceFolder => ItemCatalog.ItemResourceFolder;

        private static CostumeDescriptor[] _costumes;

        /// <summary>지금 실린 코스튬. <b>오늘은 0개이고 그건 정상이다</b> — 매니페스트 에셋이 아직 없다.</summary>
        public static IReadOnlyList<CostumeDescriptor> Costumes
        {
            get { EnsureLoaded(); return _costumes; }
        }

        public static int Count
        {
            get { EnsureLoaded(); return _costumes.Length; }
        }

        /// <summary>세이브에 적힌 키로 찾는다. 없으면 <c>null</c> —
        /// 그 <c>null</c>이 <b>"이 앱은 그 코스튬을 모른다"</b>는 정확한 사실이다.</summary>
        public static CostumeDescriptor Find(string costumeKey)
        {
            if (string.IsNullOrEmpty(costumeKey)) return null;
            EnsureLoaded();
            for (int i = 0; i < _costumes.Length; i++)
            {
                if (_costumes[i].CostumeKey == costumeKey) return _costumes[i];
            }
            return null;
        }

        /// <summary>기본 코호트 <b>테마</b>에서 나오는 코스튬. 없으면 <c>null</c>.
        /// <para>★ 오늘 이 갈래에 <c>mil</c>이 없는 것은 <b>의도</b>다 — 1일차 무상 4종이 그대로
        /// <c>mil</c> 4/4라, <c>costume.mil</c>을 만들면 <b>동전 0원·0일</b>에 코스튬이 열린다
        /// (아래 <see cref="Accepts"/>가 그 조합을 결함으로 신고한다).</para></summary>
        public static CostumeDescriptor FindByBaseTheme(string themeKey)
        {
            if (string.IsNullOrEmpty(themeKey)) return null;
            EnsureLoaded();
            for (int i = 0; i < _costumes.Length; i++)
            {
                CostumeDescriptor c = _costumes[i];
                if (c.SourceKind == CostumeSourceKind.BaseTheme && c.SourceId == themeKey) return c;
            }
            return null;
        }

        /// <summary>팩에서 나오는 코스튬. 없으면 <c>null</c>(그 팩은 코스튬이 없는 팩이다).</summary>
        public static CostumeDescriptor FindByPack(string packId)
        {
            if (string.IsNullOrEmpty(packId)) return null;
            EnsureLoaded();
            for (int i = 0; i < _costumes.Length; i++)
            {
                CostumeDescriptor c = _costumes[i];
                if (c.SourceKind == CostumeSourceKind.Pack && c.SourceId == packId) return c;
            }
            return null;
        }

        private static void EnsureLoaded()
        {
            if (_costumes != null) return;

            CostumeManifestSO[] manifests = Resources.LoadAll<CostumeManifestSO>(ResourceFolder);
            var faults = new List<string>();
            _costumes = Build(manifests, faults);

            for (int i = 0; i < faults.Count; i++) Debug.LogError($"{LogPrefix} {faults[i]}");
        }

        /// <summary>테스트 전용 — 다음 접근에서 폴더를 다시 훑게 한다.</summary>
        internal static void ResetForTesting() => _costumes = null;

        /// <summary>
        /// 테스트 전용 — 폴더 대신 <b>이 목록</b>을 실린 코스튬으로 삼는다.
        ///
        /// <para><see cref="ResetForTesting"/>만으로는 «코스튬을 아는 상태»를 만들 수 없다.
        /// <c>Resources.LoadAll</c>이 그 자리를 물고 있어서다 — <see cref="PackRegistry"/>가
        /// <see cref="Build"/>를 밖으로 꺼낸 것과 <b>같은 이유</b>이고, 여기서는 한 걸음 더 나가
        /// <b>전역 상태</b>까지 넣을 수 있게 한다. 세이브 정규화("코드가 모르는 키는 버린다")를
        /// 재려면 «아는 키»가 실제로 있어야 하기 때문이다.</para>
        ///
        /// <para><b><c>internal</c>이다</b> — <c>public</c>이면 프로덕션 어셈블리 밖에서 코스튬 목록을
        /// 갈아끼울 수 있고, 코스튬 목록은 <b>개방 판정의 입력</b>이다
        /// (<c>PackEntitlements.SetTestOverride</c>가 <c>internal</c>인 것과 같은 이유).</para>
        /// </summary>
        internal static void UseForTesting(IReadOnlyList<CostumeManifestSO> manifests, List<string> faults = null)
        {
            _costumes = Build(manifests, faults ?? new List<string>());
        }

        /// <summary>
        /// ★ <b>매니페스트 배열 -> 실린 코스튬 배열</b>. 순수 함수이고 <b>판정은 여기 한 곳뿐이다.</b>
        /// <c>EnsureLoaded</c> 안에 인라인으로 두지 않은 이유는 <b>검증 가능성</b>이다 —
        /// 그 자리는 <c>Resources.LoadAll</c>이 물고 있어 <b>합성 아홉 번째 코스튬</b>을 먹일 수 없다.
        /// </summary>
        /// <param name="manifests">폴더에서 나온 그대로. <c>null</c> 항목이 있어도 죽지 않는다.</param>
        /// <param name="faults">사람이 읽을 결함 문장이 <b>추가</b>된다(비우지 않는다).</param>
        internal static CostumeDescriptor[] Build(IReadOnlyList<CostumeManifestSO> manifests, List<string> faults)
        {
            if (faults == null) faults = new List<string>();

            var accepted = new List<CostumeManifestSO>();
            int manifestCount = manifests == null ? 0 : manifests.Count;
            for (int i = 0; i < manifestCount; i++)
            {
                CostumeManifestSO m = manifests[i];
                if (m == null)
                {
                    faults.Add($"코스튬 매니페스트 목록 {i}번이 비어 있습니다" +
                        "(에셋이 깨졌거나 스크립트 참조가 끊겼습니다).");
                    continue;
                }
                if (Accepts(m, accepted, faults)) accepted.Add(m);
            }

            var result = new CostumeDescriptor[accepted.Count];
            for (int i = 0; i < accepted.Count; i++) result[i] = new CostumeDescriptor(accepted[i]);
            return result;
        }

        /// <summary>
        /// 이 매니페스트를 실을 것인가. <b>거부 사유를 전부 모아서</b> 신고한다(첫 번째에서 끊지 않는다) —
        /// 하나 고칠 때마다 다시 돌려 다음 결함을 만나는 것은 코스튬을 만드는 사람에게 낭비다.
        /// </summary>
        private static bool Accepts(CostumeManifestSO m, List<CostumeManifestSO> earlier, List<string> faults)
        {
            int before = faults.Count;
            string who = string.IsNullOrEmpty(m.costumeKey)
                ? $"이름 없는 코스튬 매니페스트('{m.name}')"
                : $"'{m.costumeKey}'";

            // ---- 정체 ----
            if (!PackManifestKeys.IsWellFormed(m.costumeKey))
            {
                faults.Add($"{who}의 costumeKey가 키 모양이 아닙니다(ASCII 소문자·숫자·점·밑줄, " +
                    $"최대 {PackManifestKeys.MaxKeyLength}자). costumeKey는 <b>세이브 파일에 앉는 값</b>이라 " +
                    "모양이 흔들리면 누적 시간이 다른 코스튬 것이 됩니다.");
            }
            if (m.requiresSchemaVersion > CostumeManifestSO.SchemaVersion)
            {
                faults.Add($"{who}는 코스튬 스키마 v{m.requiresSchemaVersion}를 요구하는데 " +
                    $"이 앱은 v{CostumeManifestSO.SchemaVersion}입니다. 앱이 모르는 필드를 무시하고 " +
                    "반쯤 읽으면 화면은 뜨는데 뜻이 다릅니다 — 앱을 갱신해야 이 코스튬이 실립니다.");
            }

            // ---- 문자열은 키다(원문 금지) ----
            RequireKey(m.displayNameKey, $"{who}의 displayNameKey", faults);

            // ---- 소속 ----
            AuditSource(m, who, faults);

            // ---- 프롭 조각: 문법을 <b>실제로 돌려</b> 확인한다(검사기를 새로 적지 않는다) ----
            if (m.propShapes != null)
            {
                for (int i = 0; i < m.propShapes.Length; i++)
                {
                    if (AccessoryWornShapeReader.Validate(m.propShapes[i], out string error)) continue;
                    faults.Add($"{who}의 propShapes[{i}]" +
                        $"('{m.propShapes[i].name}')가 좌표 스트림 문법에 맞지 않습니다: {error} " +
                        "반쯤 읽힌 좌표는 «허공에 그려진 프롭»이 되고, 그건 행동-텍스트 싱크가 깨진 화면입니다.");
                }
            }

            // ---- 단계 조형 ----
            if (m.stageShapes != null)
            {
                for (int i = 0; i < m.stageShapes.Length; i++)
                {
                    int stage = m.stageShapes[i].stage;
                    if (stage < 0 || stage >= CostumeEvolutionRules.StageCount)
                    {
                        faults.Add($"{who}의 stageShapes[{i}]가 단계 {stage}를 가리킵니다 — " +
                            $"유효 범위는 0~{CostumeEvolutionRules.StageCount - 1}입니다" +
                            "(진화는 상태 4개 · 사건 3회다). 범위 밖 단계는 <b>영원히 그려지지 않습니다</b>.");
                        continue;
                    }
                    for (int j = 0; j < i; j++)
                    {
                        if (m.stageShapes[j].stage != stage) continue;
                        faults.Add($"{who}가 단계 {stage}의 조형을 두 번 적었습니다. " +
                            "어느 쪽이 그려질지는 배열 순서가 정하게 되고, 그 순서는 에셋을 편집할 때마다 흔들립니다.");
                        break;
                    }
                }
            }

            // ---- 앞서 실린 코스튬과의 충돌 ----
            for (int i = 0; i < earlier.Count; i++)
            {
                CostumeManifestSO other = earlier[i];

                if (other.costumeKey == m.costumeKey)
                {
                    faults.Add($"costumeKey '{m.costumeKey}'를 매니페스트 둘이 씁니다" +
                        $"('{other.name}' vs '{m.name}'). costumeKey는 세이브 파일의 누적 키라 " +
                        "겹치면 두 코스튬의 누적 시간이 한 칸에 섞입니다.");
                }
                if (other.sourceKind == m.sourceKind && other.sourceId == m.sourceId)
                {
                    faults.Add($"소속 {m.sourceKind}/'{m.sourceId}'를 코스튬 둘이 주장합니다" +
                        $"('{other.costumeKey}' vs '{m.costumeKey}'). 같은 차림이 두 코스튬을 가리키면 " +
                        "해석기가 어느 쪽을 고를지 <b>배열 순서</b>가 정하게 됩니다 — 화면만 봐서는 못 찾습니다.");
                }
            }

            return faults.Count == before;
        }

        /// <summary>
        /// 소속이 <b>실재하는 것</b>을 가리키는가.
        ///
        /// <para>★ <b>기본 코호트 <c>mil</c>은 거부한다</b>(설계 결정을 코드에 못박는 자리).
        /// 1일차 무상 4종(스탯 4슬롯 idx0)이 <b>전부 <c>mil</c></b>이라, <c>mil</c> 테마 코스튬을 만들면
        /// <b>동전 0원 · 0일</b>에 코스튬이 열린다 — 유료 코스튬의 간판 연출이 그 자리에서 샌다.
        /// 값이 없어서 막히는 것이 아니라 <b>여기서 사유와 함께 막힌다</b>:
        /// 정책이 바뀌면 <b>바꿀 곳이 한 군데</b>이고, 그때 이 문단이 무엇을 여는지 말해 준다
        /// (<c>ItemCatalog.MaxDeclaredRarityForPack</c>이 같은 방식으로 페이투윈 차단선을 들고 있다).</para>
        /// </summary>
        private static void AuditSource(CostumeManifestSO m, string who, List<string> faults)
        {
            if (!PackManifestKeys.IsWellFormed(m.sourceId))
            {
                faults.Add($"{who}의 sourceId가 키 모양이 아닙니다(\"{m.sourceId}\"). " +
                    "BaseTheme면 ItemCatalog의 테마 키, Pack이면 packId를 그대로 적습니다.");
                return;
            }

            if (m.sourceKind == CostumeSourceKind.Pack) return;   // 팩 실재 여부는 해석 시점의 사실이다

            string[] themes = ItemCatalog.AllThemes();
            bool known = false;
            for (int i = 0; i < themes.Length; i++)
            {
                if (themes[i] != m.sourceId) continue;
                known = true;
                break;
            }
            if (!known)
            {
                faults.Add($"{who}가 기본 코호트 테마 '{m.sourceId}'를 가리키는데 그런 테마가 없습니다. " +
                    "이 코스튬은 <b>어떤 차림으로도 성립하지 않습니다</b> — 화면상 «기능 미구현»과 " +
                    "완전히 같아 보입니다.");
                return;
            }

            if (m.sourceId == ItemCatalog.ThemeMil)
            {
                faults.Add($"{who}가 기본 코호트 테마 '{ItemCatalog.ThemeMil}'를 가리킵니다. " +
                    "1일차 무상 4종(스탯 4슬롯의 첫 아이템)이 전부 이 테마라, 이 코스튬은 " +
                    "<b>동전 0원 · 0일</b>에 전원에게 열립니다 — 유료 코스튬의 간판 연출이 그 자리에서 샙니다. " +
                    "정책을 바꾸려면 이 검사를 지우고 <b>왜 열기로 했는지</b>를 같은 자리에 적으십시오.");
            }
        }

        private static void RequireKey(string value, string what, List<string> faults)
        {
            if (PackManifestKeys.IsWellFormed(value)) return;

            if (PackManifestKeys.ContainsNonAscii(value))
            {
                faults.Add($"{what}에 원문 문자열이 들어 있습니다(\"{value}\"). " +
                    "매니페스트는 <b>키</b>만 담습니다 — 코스튬이 계속 늘어나면 코스튬마다 번역 부채가 자랍니다. " +
                    "★ 이 위반은 grep '[가-힣]'로는 영원히 0건입니다(YAML이 \\uXXXX로 이스케이프합니다).");
                return;
            }
            faults.Add($"{what}가 키 모양이 아닙니다(\"{value}\"). " +
                $"ASCII 소문자·숫자·점·밑줄만, 최대 {PackManifestKeys.MaxKeyLength}자, " +
                "점으로 시작·끝나거나 빈 마디를 둘 수 없습니다.");
        }
    }
}
