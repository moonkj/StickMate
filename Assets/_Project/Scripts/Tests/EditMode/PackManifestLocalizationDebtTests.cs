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

            Assert.LessOrEqual(offenders.Count, KoreanItemAssetBaseline,
                $"{LogPrefix} 한글 원문을 담은 아이템 에셋이 {offenders.Count}개입니다" +
                $"(2026-09-03 실측 대장 {KoreanItemAssetBaseline}개).\n" +
                "★ 팩 아이템도 AccessoryDefSO 입니다 — 팩 하나가 6종이면 " +
                "(이름+설명) 12건이 <b>매 팩마다</b> 늘어납니다.\n" +
                "새로 들어온 파일:\n  · " + string.Join("\n  · ", offenders));

            Debug.Log($"{LogPrefix} 원문 담은 아이템 에셋 {offenders.Count}/{KoreanItemAssetBaseline} " +
                      "(줄어드는 방향은 막지 않는다 — 갚으면 대장을 내린다).");
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
