using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★ 번역 부채가 <b>팩마다</b> 자라는 것을 막는다 — 그리고 그 감사는 <c>grep</c> 으로는 못 만든다
    /// ============================================================================
    /// <c>docs/ARCHITECTURE.md</c> 「로컬라이즈 키 · 원문 문자열 금지」. 그 규칙은 기본 42종에서
    /// <b>이미 한 번 어겨졌고</b>(같은 문서의 자기 정정), 사용자 확정
    /// <b>"출시 이후부터 계속 추가팩 만들거야"</b> 때문에 이제 <b>팩마다</b> 부채가 자란다.
    ///
    /// ============================================================================
    /// ★★ 이 파일의 존재 이유 하나 — <c>grep '[가-힣]' *.asset</c> 은 <b>영원히 0건</b>이다
    /// ============================================================================
    /// Unity YAML은 한글을 <c>천</c> 처럼 <b>이스케이프</b>해 적는다. 그래서 평범한 정규식은
    /// 위반을 <b>한 건도</b> 못 본다 — 그리고 그 0건은 <b>「깨끗하다」와 글자 하나까지 똑같이 생겼다</b>
    /// (TEAM.md §4 사고 #4와 같은 형태: <c>strings</c> 가 UTF-16을 못 봐 "0건 = 깨끗"으로 읽었다).
    ///
    /// <para>그래서 이 파일은 <b>같은 파일을 두 자로 잰다</b>:</para>
    /// <list type="number">
    ///  <item><b>날것 그대로</b> 훑는다 → 0건이 나와야 한다(그것이 오늘 우리가 속고 있는 그 0건이다).</item>
    ///  <item><b>디코드한 뒤</b> 훑는다 → 42건이 나와야 한다.</item>
    /// </list>
    /// <b>두 수가 갈라지는 것 자체가 이 감사의 값이다.</b> 둘이 같아지면(둘 다 0) 디코더가 죽은 것이고,
    /// 그때는 아래 모든 판정이 무효다.
    /// </summary>
    public sealed class PackManifestLocalizationDebtTests
    {
        private const string LogPrefix = "[번역부채]";

        /// <summary>
        /// ★ <b>부채 대장</b> — 2026-09-03 실측. 한글 원문을 담고 있는 <c>AccessoryDefSO</c> 에셋 수.
        /// <para>기본 42종이 전부 <c>displayName</c>+<c>description</c> 을 한글 원문으로 담고 있다
        /// (<c>ARCHITECTURE.md</c> 가 기록한 84건 = 42 × 2). <b>이 수는 늘 수 없다.</b>
        /// 팩 아이템도 <c>AccessoryDefSO</c> 이므로, 팩이 원문을 달고 들어오면 <b>여기서 먼저 빨개진다</b>.</para>
        /// <para>줄어드는 방향은 막지 않는다 — 그건 부채를 갚은 것이다(그때 이 숫자를 내린다).</para>
        /// </summary>
        private const int KoreanItemAssetBaseline = 42;

        /// <summary>
        /// ★★ <b>2026-09-08 리더 판정 3 — 팩 4종 몫의 백필 여유(임시 조치, 빚으로 기록).</b>
        ///
        /// <para><b>지금 무엇이 사실인가</b>: 첫 유료 팩 <c>pack.cyber</c>의 아이템 4종
        /// (<c>Patched Hood</c> · <c>Slit Visor</c> · <c>Cable Collar</c> · <c>Tarp Cape</c>)이
        /// <b>영문 이름·설명으로 저작돼 있다</b>. 그래서 위 래칫(한글 원문을 담은 에셋 수)은
        /// <b>오늘도 42다</b> — 팩은 이 대장에 한 건도 보태지 않았다.</para>
        ///
        /// <para>★ <b>그것이 좋은 소식이 아니다.</b> 이 앱의 UI는 한국어이고, 보관함·상점 카드에
        /// 「덧댄후드」가 아니라 <b>「Patched Hood」가 그대로 뜬다</b>. 즉 팩 4종은
        /// <b>번역 부채를 안 진 것이 아니라, 부채 대장에 안 잡히는 형태로 결함을 갖고 있다</b> —
        /// 이 파일이 재는 축(한글 원문 ≠ 로컬라이즈 키)과 <b>직교하는</b> 두 번째 결함이다.
        /// <c>design/art/PACK_THEME_SPEC.md</c> §5-2는 이 4종의 한국어 이름을 이미 정해 두었다
        /// (케이블목띠 · 한줄바이저 · 덧댄후드 · 방수포망토).</para>
        ///
        /// <para><b>그래서 무엇을 하는가</b>: 그 결함을 고치는 길은 둘이고 <b>둘 다 이 라운드 밖</b>이다.
        /// (가) 한국어 원문을 그대로 넣는다 → 화면은 낫고 <b>이 래칫이 42 → 46으로 오른다</b>.
        /// (나) 로컬라이즈 런타임을 먼저 만든다 → 대장은 그대로지만 그 런타임이 이 프로젝트에 <b>아직 없다</b>.
        /// 리더가 (가)를 <b>막지 않기로</b> 판정했으므로 여유 4칸을 여기 <b>이름으로</b> 둔다.
        /// 숫자 46을 위 상수에 합쳐 적지 <b>않는</b> 이유: 합치면 «출하 42종의 빚»과 «팩 4종의 임시 여유»가
        /// 한 숫자에 뭉쳐 다음 사람이 무엇이 무엇인지 못 가린다.</para>
        ///
        /// <para>★ 여유가 <b>기본 42종 쪽으로 새지 않게</b>
        /// <see cref="빚이_기본_코호트와_팩_코호트로_갈려서_보인다"/>가 축을 갈라 다시 잰다 —
        /// 그게 없으면 이 +4는 «기본 아이템 4개를 더 한글로 적어도 되는 허가»가 된다.</para>
        ///
        /// <para><b>갚는 날 할 일</b>: 팩 4종을 한국어로 백필하면 이 값은 그대로 두고
        /// <see cref="KoreanItemAssetBaseline"/>을 46으로 올린 뒤 이 상수를 0으로 내린다(또는 지운다).
        /// 로컬라이즈 런타임이 생겨 키로 바뀌면 둘 다 내려간다.</para>
        /// </summary>
        private const int PackKoreanNameBackfillAllowance = 4;

        /// <summary>래칫 상한 = 출하분 빚 + 팩 백필 여유. <b>파생</b>이라 두 항 중 하나만 움직여도 따라온다.</summary>
        private const int KoreanItemAssetCeiling = KoreanItemAssetBaseline + PackKoreanNameBackfillAllowance;

        /// <summary>스캔이 공허해지는 것을 막는 바닥값. 아이템 에셋이 이보다 적게 잡히면
        /// 그건 "부채가 줄었다"가 아니라 <b>"스캐너가 눈이 멀었다"</b>이다.</summary>
        private const int ItemAssetFloor = 40;

        // ====================================================================
        // 0. 디코더 교정 — 깨지면 아래 숫자를 전부 폐기한다
        // ====================================================================

        [Test]
        public void 디코더가_알려진_코드포인트에서_교정된다()
        {
            // ★ 기대값을 프로덕션 함수로 만들지 않는다. 오른쪽은 <b>Roslyn</b>이 같은 코드포인트를
            //   해석한 결과이고, 왼쪽은 이 파일의 디코더다 — 서로 독립인 두 구현이 만난다.
            Assert.AreEqual("천모자", DecodeEscapes("\\uCC9C\\uBAA8\\uC790"),
                $"{LogPrefix} \\uXXXX 디코더가 틀렸습니다. 이 파일의 모든 수를 폐기하십시오.");
            Assert.AreEqual("A", DecodeEscapes("\\u0041"));
            Assert.AreEqual("plain", DecodeEscapes("plain"));
            Assert.AreEqual("\\uZZZZ", DecodeEscapes("\\uZZZZ"),
                $"{LogPrefix} 16진수가 아닌 것을 디코드했습니다.");
            Assert.AreEqual("", DecodeEscapes(""));
            Assert.IsNull(DecodeEscapes(null));

            Assert.IsTrue(HasHangul("천모자"));
            Assert.IsFalse(HasHangul("pack.office.name"));
            Assert.IsFalse(HasHangul("\\uCC9C\\uBAA8\\uC790"),
                $"{LogPrefix} 이스케이프 <b>원문</b>에서 한글이 잡혔습니다 — " +
                "그러면 아래 「날것 0건 / 디코드 42건」 대조가 성립하지 않습니다.");
        }

        // ====================================================================
        // 1. 두 자가 다른 답을 낸다 — 이것이 이 감사의 본체다
        // ====================================================================

        [Test]
        public void 같은_파일을_날것과_디코드로_재면_답이_갈린다()
        {
            string[] items = AssetsReferencingScript(nameof(AccessoryDefSO));
            Assert.GreaterOrEqual(items.Length, ItemAssetFloor,
                $"{LogPrefix} 아이템 에셋을 {items.Length}개밖에 못 찾았습니다 — " +
                "스캐너가 눈이 멀었으므로 아래 수는 전부 무효입니다.");

            int rawHits = 0, decodedHits = 0;
            foreach (string path in items)
            {
                string text = File.ReadAllText(path);
                if (HasHangul(text)) rawHits++;
                if (HasHangul(DecodeEscapes(text))) decodedHits++;
            }

            Assert.AreEqual(0, rawHits,
                $"{LogPrefix} 날것 스캔이 {rawHits}건을 찾았습니다. YAML 표기가 바뀐 것이라면 " +
                "이 대조의 전제가 달라진 것이니 대장을 다시 세우십시오.");
            Assert.Greater(decodedHits, rawHits,
                $"{LogPrefix} 두 자가 같은 답({decodedHits})을 냈습니다. " +
                "디코드가 아무 일도 하지 않았다는 뜻이고, 그러면 이 감사는 " +
                "<b>평범한 grep 과 똑같이 눈이 먼 상태</b>입니다.");
            Assert.GreaterOrEqual(decodedHits, ItemAssetFloor,
                $"{LogPrefix} 디코드 스캔이 {decodedHits}/{items.Length} 건만 찾았습니다 " +
                $"(2026-09-03 실측은 {KoreanItemAssetBaseline}건 전부였습니다).");

            Debug.Log($"{LogPrefix} 같은 {items.Length}개 파일 — 날것 {rawHits}건 / 디코드 {decodedHits}건. " +
                      "이 차이가 grep 기반 감사가 영원히 0건을 답하는 이유다.");
        }

        // ====================================================================
        // 2. 래칫 — 부채는 늘 수 없다
        // ====================================================================

        [Test]
        public void 원문을_담은_아이템_에셋_수는_늘지_않는다()
        {
            var offenders = new List<string>();
            foreach (string path in AssetsReferencingScript(nameof(AccessoryDefSO)))
            {
                if (HasHangul(DecodeEscapes(File.ReadAllText(path)))) offenders.Add(Path.GetFileName(path));
            }

            Assert.LessOrEqual(offenders.Count, KoreanItemAssetCeiling,
                $"{LogPrefix} 한글 원문을 담은 아이템 에셋이 {offenders.Count}개입니다" +
                $"(상한 {KoreanItemAssetCeiling} = 출하 대장 {KoreanItemAssetBaseline}" +
                $" + 팩 백필 여유 {PackKoreanNameBackfillAllowance}).\n" +
                "★ 팩 아이템도 AccessoryDefSO 입니다 — 팩 하나가 6종이면 " +
                "(이름+설명) 12건이 <b>매 팩마다</b> 늘어납니다.\n" +
                "새로 들어온 파일:\n  · " + string.Join("\n  · ", offenders));

            Debug.Log($"{LogPrefix} 원문 담은 아이템 에셋 {offenders.Count}/{KoreanItemAssetCeiling} " +
                      $"(출하 {KoreanItemAssetBaseline} + 팩 여유 {PackKoreanNameBackfillAllowance}) " +
                      "— 줄어드는 방향은 막지 않는다(갚으면 대장을 내린다).");
        }

        /// <summary>
        /// ★★ <b>여유가 새지 않게 축을 가른다</b>(2026-09-08, 리더 판정 3의 짝).
        ///
        /// <para>위 래칫이 <see cref="KoreanItemAssetCeiling"/>(46)로 올라간 순간,
        /// 그 4칸은 <b>누구의 것인지 적혀 있지 않은 여유</b>가 된다 — 그대로 두면
        /// 「기본 42종에 한글 원문을 4개 더 넣어도 통과」가 되고, 그건 판정 3이 준 허가가 아니다.
        /// 그래서 <b>기본 코호트만</b> 따로 세어 <see cref="KoreanItemAssetBaseline"/>에 <b>여전히 못 박는다</b>.</para>
        ///
        /// <para>★ 코호트는 <b>런타임 카탈로그</b>(<see cref="BaseCohortScope"/>)가 아니라 <b>파일 텍스트</b>에서
        /// 읽는다. 이 파일의 다른 검사들이 전부 «디스크의 바이트»를 자로 쓰고 있고, 두 자를 섞으면
        /// <b>카탈로그가 안 실린 상태</b>에서 파일은 46개인데 코호트는 0개로 읽히는 어긋남이 생긴다.</para>
        ///
        /// <para><b>오늘의 실측이 이 검사의 본문</b>: 팩 4종은 한글을 <b>한 글자도</b> 담고 있지 않다 —
        /// 영문 이름(<c>Patched Hood</c> 등)이 그대로 저작돼 있기 때문이다. 그 수를 로그에 남긴다.
        /// 백필이 끝나 4가 되면 이 검사는 그대로 초록이고(여유 안), 5가 되는 순간 빨개진다.</para>
        /// </summary>
        [Test]
        public void 빚이_기본_코호트와_팩_코호트로_갈려서_보인다()
        {
            string[] items = AssetsReferencingScript(nameof(AccessoryDefSO));
            Assert.GreaterOrEqual(items.Length, ItemAssetFloor,
                $"{LogPrefix} 아이템 에셋을 {items.Length}개밖에 못 찾았습니다 — 아래 수는 전부 무효입니다.");

            var baseOffenders = new List<string>();
            var packOffenders = new List<string>();
            int packFiles = 0;

            foreach (string path in items)
            {
                string text = File.ReadAllText(path);
                bool isPack = CohortIdOf(text) != ItemCatalog.BaseCohortId;
                if (isPack) packFiles++;
                if (!HasHangul(DecodeEscapes(text))) continue;
                (isPack ? packOffenders : baseOffenders).Add(Path.GetFileName(path));
            }

            // (가) 출하분은 여유를 <b>한 칸도</b> 못 쓴다. 이 단언이 판정 3의 +4를 팩 축에 가둔다.
            Assert.LessOrEqual(baseOffenders.Count, KoreanItemAssetBaseline,
                $"{LogPrefix} <b>기본 코호트</b>에서 한글 원문을 담은 에셋이 {baseOffenders.Count}개입니다" +
                $"(대장 {KoreanItemAssetBaseline}개). 팩 백필 여유 {PackKoreanNameBackfillAllowance}칸은 " +
                "<b>팩 아이템 몫</b>이지 출하 42종이 빚을 더 질 허가가 아닙니다.\n  · " +
                string.Join("\n  · ", baseOffenders));

            // (나) 팩 쪽도 여유를 넘지 못한다 — 다음 팩이 들어오면 그때 리더가 다시 판정한다.
            Assert.LessOrEqual(packOffenders.Count, PackKoreanNameBackfillAllowance,
                $"{LogPrefix} <b>팩 코호트</b>에서 한글 원문을 담은 에셋이 {packOffenders.Count}개입니다" +
                $"(여유 {PackKoreanNameBackfillAllowance}칸). 팩이 하나 더 들어왔다면 이 여유를 늘리는 것은 " +
                "리더 판정 사항입니다 — 늘리기 전에 로컬라이즈 런타임 유무를 먼저 답하십시오.\n  · " +
                string.Join("\n  · ", packOffenders));

            // (다) ★ 분류기 자체를 <b>다른 자</b>로 다시 잰다 — 파일 텍스트에서 읽은 팩 수와
            //     런타임 카탈로그가 아는 팩 수가 같은가. 갈라지면 위 두 단언이 «엉뚱한 축»을 센 것이다
            //     (예: cohortId 필드 이름이 바뀌면 이 파서는 조용히 «전부 기본»이라고 답한다 —
            //      그 상태에서 (가)는 46건을 42로 재게 되고 빨간불이 «출하분 폭증»으로 오독된다).
            Assert.AreEqual(BaseCohortScope.PackEquipmentCount, packFiles,
                $"{LogPrefix} 파일 텍스트로 센 팩 에셋 {packFiles}개와 카탈로그가 아는 팩 아이템 " +
                $"{BaseCohortScope.PackEquipmentCount}종이 다릅니다 — 두 자 중 하나가 코호트를 못 읽고 있습니다.");
            Assert.Greater(baseOffenders.Count, 0,
                $"{LogPrefix} 기본 코호트에서 한글을 한 건도 못 찾았습니다 — 스캐너가 눈이 멀었습니다" +
                "(출하 42종은 전부 한글 원문을 담고 있습니다).");

            Debug.Log($"{LogPrefix} 파일 {items.Length}개 = 기본 {items.Length - packFiles} + 팩 {packFiles} · " +
                      $"한글 원문 기본 {baseOffenders.Count}/{KoreanItemAssetBaseline} · " +
                      $"팩 {packOffenders.Count}/{PackKoreanNameBackfillAllowance}. " +
                      (packFiles > 0 && packOffenders.Count == 0
                          ? "★ 팩 0건은 «깨끗»이 아니라 «영문 이름 그대로»다 — 한국어 UI에 영문이 뜬다(백필 대기)."
                          : "팩 파일이 없거나 이미 백필됐다."));
        }

        /// <summary>애셋 YAML 텍스트에서 <c>cohortId</c> 를 읽는다. 없으면 기본 코호트로 본다
        /// (필드가 추가되기 전 형식이거나 직렬화가 생략된 경우 — 그때는 실제로 기본값이다).</summary>
        private static int CohortIdOf(string yaml)
        {
            const string key = "\n  cohortId: ";
            int at = yaml.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return ItemCatalog.BaseCohortId;
            int from = at + key.Length, to = from;
            while (to < yaml.Length && yaml[to] != '\n' && yaml[to] != '\r') to++;
            return int.TryParse(yaml.Substring(from, to - from).Trim(), out int v) ? v : ItemCatalog.BaseCohortId;
        }

        /// <summary>
        /// 팩 매니페스트는 <b>처음부터</b> 원문을 담지 않는다. 오늘 매니페스트 에셋은 <b>0개</b>이고
        /// 그 0은 <b>기대값으로 명시</b>한다(거짓 통과 #5: 빈 목록은 아무것도 재지 않는다).
        /// <para>0이 의미를 갖는 근거는 위 <see cref="같은_파일을_날것과_디코드로_재면_답이_갈린다"/> 다 —
        /// <b>같은 스캐너</b>가 아이템 에셋 42개를 실제로 찾아낸다.</para>
        /// </summary>
        [Test]
        public void 매니페스트_에셋은_원문을_담지_않는다()
        {
            string[] manifests = AssetsReferencingScript(nameof(StickPackManifestSO));

            var offenders = new List<string>();
            foreach (string path in manifests)
            {
                if (HasHangul(DecodeEscapes(File.ReadAllText(path)))) offenders.Add(Path.GetFileName(path));
            }
            Assert.IsEmpty(offenders,
                $"{LogPrefix} 매니페스트에 한글 원문이 들어 있습니다({offenders.Count}건):\n  · " +
                string.Join("\n  · ", offenders) + "\n" +
                "매니페스트는 <b>키</b>만 담습니다 — PackRegistry 가 로드에서도 거부합니다.");

            Debug.Log($"{LogPrefix} 매니페스트 에셋 {manifests.Length}개 중 원문 {offenders.Count}건. " +
                      "오늘 0개인 것은 정상이다(조형 라운드가 채운다) — " +
                      "같은 스캐너가 아이템 에셋은 실제로 찾아낸다는 것을 위 테스트가 증명한다.");
        }

        /// <summary>
        /// 매니페스트가 <b>실제로 놓였을 때</b> 키 검사가 도는가를, 오늘 도는 형태로 확인한다.
        /// (에셋이 0개라 위 테스트가 공허해지는 구간을 이 검사가 메운다.)
        /// </summary>
        [Test]
        public void 원문이_들어간_매니페스트는_로드에서_거부된다()
        {
            var m = ScriptableObject.CreateInstance<StickPackManifestSO>();
            try
            {
                m.name = "fixture_korean";
                m.packId = "packfixture.korean";
                m.cohortId = 1;
                m.requiresSchemaVersion = StickPackManifestSO.SchemaVersion;
                m.displayNameKey = "오피스 워커";   // "오피스 워커"
                m.descriptionKey = "packfixture.korean.desc";
                m.declaredItemCount = 1;
                m.itemIndexBase = 6;

                var faults = new List<string>();
                PackDescriptor[] loaded = PackRegistry.Build(new[] { m }, new ItemCatalogEntry[0], faults);

                Assert.AreEqual(0, loaded.Length,
                    $"{LogPrefix} 원문이 들어간 매니페스트가 실렸습니다.");
                Assert.IsNotEmpty(faults);
                Assert.IsTrue(PackManifestKeys.ContainsNonAscii(m.displayNameKey),
                    $"{LogPrefix} 픽스처가 실제로 한글이 아닙니다 — 그러면 이 검사는 아무것도 안 잽니다.");

                Debug.Log($"{LogPrefix} 로드 거부 확인: {faults[0]}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(m);
            }
        }

        // ====================================================================
        // 도구
        // ====================================================================

        /// <summary><c>\uXXXX</c> 만 푼다. 정규식을 쓰지 않는다 — .NET <c>\b</c> 가 한글을 낱말로 세는
        /// 함정과 같은 계열의 사고를 이 저장소가 여러 번 겪었다.</summary>
        internal static string DecodeEscapes(string source)
        {
            if (source == null) return null;
            if (source.IndexOf("\\u", StringComparison.Ordinal) < 0) return source;

            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                if (source[i] == '\\' && i + 6 <= source.Length && source[i + 1] == 'u'
                    && TryHex(source, i + 2, out int code))
                {
                    sb.Append((char)code);
                    i += 6;
                    continue;
                }
                sb.Append(source[i]);
                i++;
            }
            return sb.ToString();
        }

        private static bool TryHex(string s, int at, out int value)
        {
            value = 0;
            for (int k = 0; k < 4; k++)
            {
                char c = s[at + k];
                int digit;
                if (c >= '0' && c <= '9') digit = c - '0';
                else if (c >= 'a' && c <= 'f') digit = 10 + (c - 'a');
                else if (c >= 'A' && c <= 'F') digit = 10 + (c - 'A');
                else return false;
                value = value * 16 + digit;
            }
            return true;
        }

        /// <summary>한글 음절 블록(U+AC00~U+D7A3) 또는 자모(U+3131~U+318E)가 하나라도 있는가.</summary>
        internal static bool HasHangul(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '가' && c <= '힣') return true;
                if (c >= 'ㄱ' && c <= 'ㆎ') return true;
            }
            return false;
        }

        /// <summary><c>&lt;타입&gt;.cs.meta</c> 의 GUID로 <c>.asset</c> 을 찾는다.
        /// <b>이름으로 찾으면 탐지력이 애초에 0이다</b> — 애셋에는 타입 이름이 없고 GUID만 있다.</summary>
        internal static string[] AssetsReferencingScript(string typeName)
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string metaPath = null;
            foreach (string p in Directory.GetFiles(scriptsRoot, typeName + ".cs.meta", SearchOption.AllDirectories))
            {
                metaPath = p;
                break;
            }
            Assert.IsNotNull(metaPath, $"{LogPrefix} {typeName}.cs.meta 를 찾지 못했습니다.");

            string guid = null;
            foreach (string line in File.ReadAllLines(metaPath))
            {
                string t = line.Trim();
                if (!t.StartsWith("guid:", StringComparison.Ordinal)) continue;
                guid = t.Substring("guid:".Length).Trim();
                break;
            }
            Assert.IsNotEmpty(guid, $"{LogPrefix} {typeName}.cs.meta 에 guid 줄이 없습니다.");

            string resources = Path.Combine(Application.dataPath, "_Project", "Resources");
            var hits = new List<string>();
            if (Directory.Exists(resources))
            {
                foreach (string asset in Directory.GetFiles(resources, "*.asset", SearchOption.AllDirectories))
                {
                    if (File.ReadAllText(asset).IndexOf(guid, StringComparison.Ordinal) >= 0) hits.Add(asset);
                }
            }
            hits.Sort(StringComparer.Ordinal);
            return hits.ToArray();
        }
    }
}
