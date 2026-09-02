using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>"Missing (Mono Script)"가 빌드에 실려 나가는 것</b>을 막는 감사 — 2026-09-02.
    ///
    /// ============================================================================
    /// 무엇이 있었나 (관측 — 예측이 아니다)
    /// ============================================================================
    /// PlayMode 로그에 <c>The referenced script (Unknown) ... is missing!</c>가 <b>실제로 찍혔다</b>.
    /// 격파 놀이(BattleMinigame*) 제거 커밋(7ab0468)이 스크립트 2개와 그 <c>.meta</c>를 지웠는데,
    /// 이미 <b>구워져 있던</b> 프리팹이 두 GUID를 그대로 들고 있었다:
    /// <code>
    ///   b3c9d46f4d7e7445cb95ad00dbd9964f -> BattleMinigameDirector.cs  (삭제됨)
    ///   b45ed3881a68a4cbd8c658b8942ea6f1 -> BattleMinigameRenderer.cs  (삭제됨)
    /// </code>
    ///
    /// <para>★ <b>이 감사가 씬만 봤다면 이번 사고를 놓쳤다.</b> 부트스트래퍼 주석은 "기존 <b>씬</b>
    /// 파일에 두 컴포넌트의 참조가 남아 있다"고 적고 있었지만, <c>Main.unity</c>의 스크립트 참조
    /// 4개는 <b>전부 멀쩡했다</b>. 깨진 것은 씬이 인스턴스화하는 <c>Stickman.prefab</c>이었다.
    /// 그래서 이 파일은 <b>씬과 프리팹을 모두</b> 훑는다 — 사고가 난 곳만 좁게 잠그면 다음 사고를
    /// 또 놓친다.</para>
    ///
    /// ============================================================================
    /// 왜 텍스트로 읽고 <see cref="AssetDatabase.GUIDToAssetPath"/>로 판정하는가
    /// ============================================================================
    /// <see cref="AssetDatabase"/>로 프리팹을 <b>열어서</b> 확인하는 방법도 있지만, 그쪽은 깨진
    /// 컴포넌트가 <c>null</c>로 조용히 사라져 <b>몇 개가 왜 깨졌는지</b>를 못 말한다. 파일을 직접
    /// 읽으면 GUID를 그대로 집어 <b>어떤 스크립트였는지</b>까지 보고할 수 있다. 반대로 GUID가
    /// 실재하는지는 <b>손으로 폴더를 뒤지지 않고</b> <see cref="AssetDatabase.GUIDToAssetPath"/>에
    /// 묻는다 — 그래야 <c>Packages/</c>와 <c>Library/PackageCache</c>에 사는 UGUI·uniwinc 스크립트를
    /// "없다"고 잘못 신고하지 않는다(실제로 손으로 grep했을 때 그 셋이 전부 오탐이었다).
    ///
    /// ============================================================================
    /// ★ 네거티브 컨트롤이 없으면 이 파일은 아무것도 증명하지 않는다
    /// ============================================================================
    /// 이 저장소는 하룻밤에 <b>거짓 통과 9건</b>을 냈다 — 실패한 측정과 성공한 측정이 똑같이 생겼다.
    /// 그래서 <see cref="탐지기_자체가_동작한다_고장난_입력을_넣으면_반드시_잡는다"/>가 <b>일부러
    /// 깨진 YAML</b>을 같은 파서에 먹여 "탐지기가 살아 있다"를 먼저 증명한다. 그 검사가 빨간불을
    /// 못 내면 본 검사의 초록불은 의미가 없다.
    /// </summary>
    public sealed class MissingMonoScriptAuditTests
    {
        private const string LogPrefix = "[MissingScript-TEST]";

        /// <summary>YAML의 스크립트 참조 한 줄. <c>type: 3</c>이 MonoScript다.</summary>
        private static readonly Regex ScriptRefPattern = new Regex(
            @"m_Script:\s*\{fileID:\s*(?<fileId>-?\d+)(?:,\s*guid:\s*(?<guid>[0-9a-fA-F]{32}))?",
            RegexOptions.Compiled);

        private readonly struct BrokenRef
        {
            public readonly string AssetPath;
            public readonly int Line;
            public readonly string Guid;
            public readonly string Reason;

            public BrokenRef(string assetPath, int line, string guid, string reason)
            {
                AssetPath = assetPath; Line = line; Guid = guid; Reason = reason;
            }

            public override string ToString()
                => $"    {AssetPath}:{Line}  guid={(string.IsNullOrEmpty(Guid) ? "(없음)" : Guid)}  — {Reason}";
        }

        /// <summary>한 파일의 본문에서 깨진 스크립트 참조를 전부 찾는다.
        /// <para><paramref name="resolve"/>를 밖에서 주입받는 이유는 네거티브 컨트롤 때문이다 —
        /// 테스트가 <b>같은 파서</b>를 쓰면서 GUID 해석만 바꿔 끼울 수 있어야 한다.</para></summary>
        private static List<BrokenRef> ScanText(string assetPath, string text, System.Func<string, string> resolve)
        {
            var broken = new List<BrokenRef>();
            int line = 0;
            foreach (string raw in text.Split('\n'))
            {
                line++;
                Match m = ScriptRefPattern.Match(raw);
                if (!m.Success) continue;

                string guid = m.Groups["guid"].Success ? m.Groups["guid"].Value : null;

                // (1) 스크립트를 잃은 컴포넌트는 Unity가 fileID 0으로 적어 둔다.
                if (m.Groups["fileId"].Value == "0")
                {
                    broken.Add(new BrokenRef(assetPath, line, guid, "m_Script가 fileID 0입니다(스크립트 참조가 비었습니다)"));
                    continue;
                }

                if (string.IsNullOrEmpty(guid)) continue;

                // (2) GUID는 적혀 있는데 그 GUID의 에셋이 프로젝트에 없다 — 이번 사고가 정확히 이것이다.
                if (string.IsNullOrEmpty(resolve(guid)))
                {
                    broken.Add(new BrokenRef(assetPath, line, guid,
                        "이 GUID에 해당하는 스크립트가 프로젝트에 없습니다(삭제된 스크립트를 아직 참조합니다)"));
                }
            }
            return broken;
        }

        private static string ResolveWithAssetDatabase(string guid) => AssetDatabase.GUIDToAssetPath(guid);

        private static IEnumerable<string> ProjectAssetFiles()
        {
            string root = Application.dataPath;
            foreach (string pattern in new[] { "*.unity", "*.prefab" })
            {
                foreach (string full in Directory.GetFiles(root, pattern, SearchOption.AllDirectories))
                {
                    yield return "Assets" + full.Substring(root.Length).Replace('\\', '/');
                }
            }
        }

        private static string ToDiskPath(string assetPath)
            => Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

        // ==================== ★ 네거티브 컨트롤 먼저 ====================

        /// <summary>★ <b>본 검사보다 먼저 읽어야 하는 검사.</b> 일부러 깨뜨린 입력 셋을 같은 파서에
        /// 먹여 전부 잡히는지 본다. 여기가 초록이어야 아래 초록이 의미를 갖는다.</summary>
        [Test]
        public void 탐지기_자체가_동작한다_고장난_입력을_넣으면_반드시_잡는다()
        {
            // 이 프로젝트에 실재하지 않는 GUID(= 삭제된 BattleMinigameDirector의 실제 GUID).
            const string DeadGuid = "b3c9d46f4d7e7445cb95ad00dbd9964f";

            string brokenYaml = string.Join("\n", new[]
            {
                "--- !u!114 &111",
                "MonoBehaviour:",
                "  m_Script: {fileID: 11500000, guid: " + DeadGuid + ", type: 3}",
                "--- !u!114 &222",
                "MonoBehaviour:",
                "  m_Script: {fileID: 0}",
            });

            List<BrokenRef> hits = ScanText("(합성)", brokenYaml, ResolveWithAssetDatabase);

            Assert.AreEqual(2, hits.Count,
                $"{LogPrefix} 일부러 깨뜨린 입력 2건을 탐지기가 {hits.Count}건만 잡았습니다 — " +
                "탐지기가 고장났으므로 이 파일의 다른 초록불은 전부 무의미합니다.\n" +
                string.Join("\n", hits));

            // 살아 있는 GUID는 <b>잡으면 안 된다</b>(오탐이면 팀이 검사를 꺼 버린다).
            string aliveGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scripts/Core/StickmanAgent.cs");
            Assert.IsNotEmpty(aliveGuid, $"{LogPrefix} 전제가 깨졌습니다 — StickmanAgent.cs를 찾을 수 없습니다.");
            string goodYaml = "  m_Script: {fileID: 11500000, guid: " + aliveGuid + ", type: 3}";
            Assert.IsEmpty(ScanText("(합성)", goodYaml, ResolveWithAssetDatabase),
                $"{LogPrefix} 멀쩡한 스크립트 참조를 깨진 것으로 신고했습니다(오탐).");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 깨진 2건 검출, 정상 1건 무시.");
        }

        // ==================== 본 검사 ====================

        /// <summary>★ 씬과 프리팹 <b>전부</b>에 Missing 스크립트가 없다.
        /// 이번 사고는 씬이 아니라 <b>프리팹</b>에 있었다 — 범위를 좁히지 않는 것이 이 검사의 핵심이다.</summary>
        [Test]
        public void 씬과_프리팹_어디에도_Missing_스크립트가_없다()
        {
            var broken = new List<BrokenRef>();
            int scanned = 0;

            foreach (string assetPath in ProjectAssetFiles())
            {
                string disk = ToDiskPath(assetPath);
                if (!File.Exists(disk)) continue;
                scanned++;
                broken.AddRange(ScanText(assetPath, File.ReadAllText(disk), ResolveWithAssetDatabase));
            }

            Assert.Greater(scanned, 0,
                $"{LogPrefix} 훑은 파일이 0개입니다 — 관측 전제가 성립하지 않습니다(검사가 조용히 아무것도 안 했습니다).");

            if (broken.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{LogPrefix} Missing 스크립트 참조 {broken.Count}건 (훑은 파일 {scanned}개):");
                foreach (BrokenRef b in broken) sb.AppendLine(b.ToString());
                sb.AppendLine();
                sb.AppendLine("  이 상태로 빌드하면 그대로 릴리즈에 실립니다. 고치는 법:");
                sb.AppendLine("   · 그 컴포넌트가 <b>더 이상 필요 없다</b>면 → 에셋에서 해당 MonoBehaviour 블록과");
                sb.AppendLine("     GameObject의 m_Component 목록 항목을 <b>함께</b> 지운다(둘 중 하나만 지우면 YAML이 깨진다).");
                sb.AppendLine("   · 아직 <b>필요하다</b>면 → 스크립트를 되살리거나 .meta의 guid를 새 스크립트에 맞춘다.");
                Assert.Fail(sb.ToString());
            }

            Debug.Log($"{LogPrefix} 파일 {scanned}개 검사 — Missing 스크립트 0건.");
        }

        /// <summary>스크립트를 잃은 컴포넌트는 <see cref="AssetDatabase"/>로 열었을 때도 보이지 않아야 한다.
        /// <para>위 검사가 <b>파일</b>을 본다면 이쪽은 <b>Unity가 실제로 읽어 들인 결과</b>를 본다.
        /// 둘 다 필요하다 — 파일이 멀쩡해 보여도 임포터가 다르게 해석하면 런타임에서 터진다.</para></summary>
        [Test]
        public void 프리팹을_실제로_열었을_때도_깨진_컴포넌트가_없다()
        {
            var failures = new List<string>();
            int scanned = 0;

            foreach (string assetPath in ProjectAssetFiles())
            {
                if (!assetPath.EndsWith(".prefab")) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Assert.IsNotNull(go, $"{LogPrefix} {assetPath}를 열 수 없습니다.");
                scanned++;

                foreach (Transform t in go.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    Component[] components = t.GetComponents<Component>();
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] == null)
                        {
                            failures.Add($"    {assetPath} — '{t.name}'의 {i}번째 컴포넌트가 null입니다(스크립트 없음).");
                        }
                    }
                }
            }

            Assert.Greater(scanned, 0, $"{LogPrefix} 연 프리팹이 0개입니다 — 관측 전제가 성립하지 않습니다.");
            Assert.IsEmpty(failures,
                $"{LogPrefix} 프리팹 {scanned}개 중 깨진 컴포넌트 {failures.Count}건:\n" + string.Join("\n", failures));

            Debug.Log($"{LogPrefix} 프리팹 {scanned}개를 실제로 열어 확인 — 깨진 컴포넌트 0건.");
        }
    }
}
