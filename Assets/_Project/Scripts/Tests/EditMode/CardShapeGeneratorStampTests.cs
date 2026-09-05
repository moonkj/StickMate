using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ E10 — 생성기 provenance 스탬프 감사 (docs/TEST_PLAN_FAN_AND_EQUIPMENT.md 요구 E-0, 2026-09-05).
    ///
    /// <para>사고: <c>Tools/CardShapeGen/gen_card_shapes.py</c>가 <c>r19_model.py</c>를 읽어 색 역할을 굽는데 산출물 헤더는
    /// r16·r17·ItemIcon 세 sha 만 찍었다. 그러면 r19 가 바뀌어도 스탬프는 그대로라 사람이 「현행이다」를 구조적으로 못 본다
    /// (실제로 16:04 생성 뒤 16:08 에 r19 가 바뀐 채 EditMode 가 돌았다).</para>
    ///
    /// <para><b>모집단은 생성기 소스에서 유도한다</b> — <c>import X</c> 줄 중 <c>design/equipment/verify/X.py</c>가 실재하고 이름이
    /// <c>_model</c>로 끝나는 것 전부. 파일명은 <see cref="Path.GetFileName"/>으로 만들고, <b>존재 대조</b>(실제로 찾은 이름 ≥ 2)와
    /// <b>프로브 생존</b>(없는 파일명은 같은 감사가 「없음」으로 보고한다)을 같은 테스트에서 함께 본다.</para>
    /// </summary>
    public sealed class CardShapeGeneratorStampTests
    {
        private const string GeneratorPath = "Tools/CardShapeGen/gen_card_shapes.py";
        private const string ModelDir = "design/equipment/verify";
        private const string HandoffCsPath = "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs";
        private const string GoldenPath = "Assets/_Project/Scripts/Tests/EditMode/Golden/CardShapeGolden.txt";

        /// <summary>스탬프 토큰 — 생성기(<c>stamp_line</c>)가 찍는 형식 그대로: <c>이름 sha256[:16]=16진수16자리</c>.</summary>
        private static readonly Regex StampToken = new Regex(@"(\S+\.(?:py|html)) sha256\[:16\]=([0-9a-f]{16})", RegexOptions.CultureInvariant);

        private static string Root => Directory.GetParent(Application.dataPath).FullName;

        private static string Header(string relPath, int lines)
        {
            string path = Path.Combine(Root, relPath);
            Assert.IsTrue(File.Exists(path), $"파일이 없습니다: {relPath}");
            string[] all = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
            return string.Join("\n", all, 0, Mathf.Min(lines, all.Length));
        }

        /// <summary>생성기가 import 하는 모델 파일(실재하는 <c>*_model.py</c>)의 파일명.</summary>
        private static List<string> ModelFilesReferencedByGenerator()
        {
            string src = File.ReadAllText(Path.Combine(Root, GeneratorPath)).Replace("\r\n", "\n");
            var names = new List<string>();
            foreach (Match m in Regex.Matches(src, @"^import (\w+)(?:\s+as\s+\w+)?", RegexOptions.Multiline))
            {
                string module = m.Groups[1].Value;
                if (!module.EndsWith("_model")) continue;
                string path = Path.Combine(Root, ModelDir, module + ".py");
                if (!File.Exists(path)) continue;
                string name = Path.GetFileName(path);
                if (!names.Contains(name)) names.Add(name);
            }
            return names;
        }

        private static Dictionary<string, string> Stamped(string header)
        {
            var d = new Dictionary<string, string>();
            foreach (Match m in StampToken.Matches(header)) d[m.Groups[1].Value] = m.Groups[2].Value;
            return d;
        }

        /// <summary>같은 감사기 — 스탬프에 없는 이름을 돌려준다(있으면 빈 목록).</summary>
        private static List<string> MissingFromStamp(Dictionary<string, string> stamped, IEnumerable<string> names)
        {
            var missing = new List<string>();
            foreach (string n in names) if (!stamped.ContainsKey(n)) missing.Add(n);
            return missing;
        }

        [TestCase(HandoffCsPath, 12, TestName = "E10 Handoff.cs 헤더")]
        [TestCase(GoldenPath, 12, TestName = "E10 골든 헤더")]
        public void 생성기가_읽는_모델_파일이_전부_스탬프에_있다(string relPath, int headerLines)
        {
            List<string> models = ModelFilesReferencedByGenerator();
            Assert.GreaterOrEqual(models.Count, 3, $"생성기가 import 하는 모델 파일이 {models.Count}개뿐입니다 — 모집단이 무너졌습니다(r16·r17·r19 가 있어야 한다).");

            Dictionary<string, string> stamped = Stamped(Header(relPath, headerLines));
            List<string> missing = MissingFromStamp(stamped, models);

            // 존재 대조 — 실제로 찾은 이름이 둘 이상이어야 「없음」 판정이 뜻을 갖는다.
            var found = new List<string>();
            foreach (string n in models) if (stamped.ContainsKey(n)) found.Add(n);
            Assert.GreaterOrEqual(found.Count, 2, $"스탬프에서 모델 이름을 {found.Count}개밖에 못 찾았습니다 — 파서나 스탬프 형식이 죽었습니다.");

            // 프로브 생존 — 없는 파일명은 같은 감사가 반드시 「없음」으로 낸다.
            string bogus = Path.GetFileName(Path.Combine(Root, ModelDir, "r00_no_such_model.py"));
            Assert.IsFalse(File.Exists(Path.Combine(Root, ModelDir, bogus)), "프로브 파일명이 실재합니다 — 다른 이름을 쓰십시오.");
            Assert.AreEqual(1, MissingFromStamp(stamped, new[] { bogus }).Count, "없는 파일명을 감사가 「있음」으로 냈습니다 — 위 판정은 무효입니다.");

            Assert.IsEmpty(missing,
                $"[E10] 생성기 스탬프에 {string.Join(", ", missing)}이(가) 없습니다. 그 모델이 바뀌어도 스탬프는 그대로라 " +
                "「현행이다」 판정이 구조적으로 그 변경을 못 봅니다. " +
                $"(존재 대조: {found[0]}, {found[1]}는 스탬프에서 실제로 찾았습니다 — 프로브는 살아 있습니다.)");
        }

        /// <summary>스탬프의 sha 가 <b>지금</b> 정본 파일과 같다 — 다르면 생성물이 모델보다 낡았다(재생성 필요).
        /// 모델이 바뀌면 이 검사가 먼저 빨개지므로 「스탬프만 보고 현행이다」 판정이 다시는 조용히 지나가지 않는다.</summary>
        [TestCase(HandoffCsPath, 12, TestName = "E10 Handoff.cs 스탬프 현행")]
        [TestCase(GoldenPath, 12, TestName = "E10 골든 스탬프 현행")]
        public void 스탬프의_sha가_지금_정본_파일과_같다(string relPath, int headerLines)
        {
            Dictionary<string, string> stamped = Stamped(Header(relPath, headerLines));
            Assert.GreaterOrEqual(stamped.Count, 3, "스탬프 토큰이 3개 미만입니다 — 형식이 바뀌었으면 StampToken 도 함께 갱신하십시오.");

            var stale = new List<string>();
            int compared = 0;
            foreach (KeyValuePair<string, string> kv in stamped)
            {
                string path = LocateSource(kv.Key);
                if (path == null) continue;   // html 원본은 인계본 폴더 어디든 있을 수 있다 — 못 찾으면 그 토큰은 비교하지 않는다
                compared++;
                string now = Sha256Hex16(path);
                if (now != kv.Value) stale.Add($"{kv.Key}(스탬프 {kv.Value} ≠ 지금 {now})");
            }
            Assert.GreaterOrEqual(compared, 3, "정본 파일을 3개 미만 비교했습니다 — 경로 탐색이 죽었습니다.");
            Assert.IsEmpty(stale, "생성물이 정본보다 낡았습니다 — python3 Tools/CardShapeGen/gen_card_shapes.py 를 다시 돌리십시오:\n  " + string.Join("\n  ", stale));
        }

        private static string LocateSource(string fileName)
        {
            string inModels = Path.Combine(Root, ModelDir, fileName);
            if (File.Exists(inModels)) return inModels;
            string[] hits = Directory.GetFiles(Path.Combine(Root, "docs"), fileName, SearchOption.AllDirectories);
            return hits.Length == 1 ? hits[0] : null;
        }

        private static string Sha256Hex16(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
                var sb = new System.Text.StringBuilder(16);
                for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
