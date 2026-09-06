using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★★ <b>부트스트래퍼가 «붙이기로 한 것»과 출하 프리팹에 «실제로 붙어 있는 것»을 대조한다</b>
    /// — 2026-09-06 신설(페르소나 «재현» 실기 신고 대응).
    ///
    /// ============================================================================
    /// 무엇이 있었나 (관측 — 예측이 아니다)
    /// ============================================================================
    /// 음악 반응 춤이 <b>실제 빌드에서 통째로 작동하지 않았다</b>. 오디오를 3분 넘게 틀어도 Player
    /// 로그에 관련 줄이 <b>한 줄도</b> 없었다. 로직은 멀쩡했다. 없던 것은 <b>배선</b>이다:
    /// <code>
    ///   SceneBootstrapper.BuildStickmanPrefab  : DanceEpisodeDirector를 붙이는 코드가 있다
    ///   Stickman.prefab(출하되는 파일)          : 그 컴포넌트가 없다      ← 여기가 갈라져 있었다
    ///   BuildStandalone.PerformBuild           : 부트스트래퍼를 부르지 않는다
    /// </code>
    /// 프리팹을 굽는 함수는 <b>이미 파일이 있으면 통째로 건너뛴다</b>(BUG-SW-M3 — fileID 재할당으로
    /// 씬 오버라이드를 고아로 만들지 않기 위한 <b>의도된</b> 동작이다). 그래서 «나중에 추가된
    /// 컴포넌트»는 누군가 메뉴를 눌러 주기 전까지 영원히 출하되지 않았다.
    ///
    /// <para><b>이 사고는 처음이 아니다.</b> 같은 형태가 33-9 #10 / 34-9 #10 / 36-13 #11 / 설정창에서
    /// 이미 네 번 반복됐고, 그때마다 처방은 <i>"체크리스트에 고정하라"</i>였다. 다섯 번째에 와서야
    /// 분명해진 것: <b>체크리스트는 사람의 기억에 의존하므로 같은 사고를 다시 막지 못한다.</b>
    /// 그래서 이 파일은 체크리스트를 <b>기계가 매 러너마다 다시 읽게</b> 만든다.</para>
    ///
    /// ============================================================================
    /// 왜 리플렉션이 아니라 <b>소스 파일</b>을 읽는가
    /// ============================================================================
    /// 기대 목록의 원본은 <c>Assets/Editor/SceneBootstrapper.cs</c>이고 그 파일은
    /// <b>에디터 어셈블리</b>(<c>Assembly-CSharp-Editor</c>)에 있다. 이 테스트 어셈블리는 그것을
    /// 참조하지 않는다(<c>StickMate.Tests.EditMode.asmdef</c>). 참조를 추가해 타입으로 읽는 방법도
    /// 있지만, CLAUDE.md가 <b>플랫폼 감사는 타입이 아니라 소스를 읽으라</b>고 못박은 이유가 여기에도
    /// 그대로 적용된다 — 어셈블리 참조가 바뀌면 조용히 아무것도 안 보는 검사가 된다.
    ///
    /// ============================================================================
    /// ★ 네거티브 컨트롤이 없으면 이 파일은 아무것도 증명하지 않는다
    /// ============================================================================
    /// 이 저장소는 하룻밤에 <b>거짓 통과 9건</b>을 냈다 — 실패한 측정과 성공한 측정이 똑같이 생겼다.
    /// 그래서 <see cref="탐지기_자체가_동작한다_컴포넌트를_하나_빼면_반드시_잡는다"/>가
    /// <b>일부러 하나를 뺀 프리팹 본문</b>을 같은 비교기에 먹여 «탐지기가 살아 있다»를 먼저 증명한다.
    /// 그 검사가 빨간불을 못 내면 아래 초록불은 전부 무의미하다.
    /// </summary>
    public sealed class BootstrapPrefabParityAuditTests
    {
        private const string LogPrefix = "[부트스트랩대조-TEST]";

        private const string PrefabAssetPath = "Assets/_Project/Prefabs/Stickman.prefab";
        private const string BootstrapperRelativePath = "Editor/SceneBootstrapper.cs";
        private const string BuildScriptRelativePath = "Editor/BuildStandalone.cs";

        /// <summary>프리팹 루트에 컴포넌트를 얹는 <b>두 경로</b>. 둘 다 봐야 한다 —
        /// 한쪽만 보면 다른 쪽에 적힌 항목이 조용히 빠진다.</summary>
        private static readonly Regex AddComponentPattern =
            new Regex(@"root\.AddComponent<([A-Za-z0-9_]+)>", RegexOptions.Compiled);

        private static readonly Regex EnsureComponentPattern =
            new Regex(@"EnsureComponent<([A-Za-z0-9_]+)>\(root\)", RegexOptions.Compiled);

        private static readonly Regex ScriptGuidPattern =
            new Regex(@"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

        // ====================================================================
        // 도구
        // ====================================================================

        private static string ReadRepoFile(string relativeToAssets)
        {
            string path = Path.Combine(Application.dataPath, relativeToAssets);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary>줄 주석을 걷어낸다. 이 저장소의 주석은 <b>지운 코드</b>를 그대로 인용해 두므로
        /// (예: 삭제된 <c>CornerHoverPanel</c> 줄) 그대로 두면 «지운 것을 붙이라»는 오탐이 된다.</summary>
        private static string StripLineComments(string source)
        {
            var sb = new StringBuilder();
            foreach (string raw in source.Split('\n'))
            {
                if (raw.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                int at = raw.IndexOf("//", StringComparison.Ordinal);
                sb.Append(at >= 0 ? raw.Substring(0, at) : raw).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>이 프로젝트가 <b>직접 작성한</b> 스크립트 이름 색인. 제네릭 인자(<c>T</c>)와
        /// 유니티 내장 컴포넌트(<c>Rigidbody2D</c> 등)를 걸러내는 데 쓴다 — 그것들은
        /// <c>.cs</c> 파일이 없으므로 색인에 없다.</summary>
        private static HashSet<string> ProjectScriptNames()
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            Assert.IsTrue(Directory.Exists(scripts), $"{LogPrefix} 스크립트 폴더가 없습니다: {scripts}");

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }
            Assert.Greater(names.Count, 100,
                $"{LogPrefix} 스크립트 색인이 {names.Count}개뿐입니다 — 스캔이 깨졌고 아래 판정은 전부 무효입니다.");
            return names;
        }

        /// <summary>부트스트래퍼가 «프리팹 루트에 붙이기로 한» 컴포넌트 이름 집합.</summary>
        private static SortedSet<string> ExpectedRootComponents()
        {
            string src = StripLineComments(ReadRepoFile(BootstrapperRelativePath));
            HashSet<string> ours = ProjectScriptNames();

            var expected = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in AddComponentPattern.Matches(src))
            {
                if (ours.Contains(m.Groups[1].Value)) expected.Add(m.Groups[1].Value);
            }
            foreach (Match m in EnsureComponentPattern.Matches(src))
            {
                if (ours.Contains(m.Groups[1].Value)) expected.Add(m.Groups[1].Value);
            }

            Assert.Greater(expected.Count, 20,
                $"{LogPrefix} 부트스트래퍼에서 기대 컴포넌트를 {expected.Count}개밖에 못 찾았습니다 — " +
                "정규식이 코드 형태 변화를 따라가지 못했습니다. 이 상태의 «전부 있다»는 공허합니다.");
            return expected;
        }

        /// <summary>프리팹 <b>본문</b>에서 스크립트 이름 집합을 뽑는다.
        /// <para><paramref name="prefabText"/>를 인자로 받는 이유는 <b>네거티브 컨트롤</b> 때문이다 —
        /// 같은 추출기에 «일부러 하나를 뺀» 본문을 먹일 수 있어야 한다.</para></summary>
        private static HashSet<string> ComponentNamesIn(string prefabText)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in ScriptGuidPattern.Matches(prefabText))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (string.IsNullOrEmpty(assetPath)) continue;   // 깨진 참조는 MissingMonoScriptAudit 소관
                names.Add(Path.GetFileNameWithoutExtension(assetPath));
            }
            return names;
        }

        private static string PrefabText()
        {
            string disk = Path.Combine(Application.dataPath,
                PrefabAssetPath.Substring("Assets/".Length));
            Assert.IsTrue(File.Exists(disk), $"{LogPrefix} 출하 프리팹이 없습니다: {disk}");
            return File.ReadAllText(disk).Replace("\r\n", "\n");
        }

        /// <summary>기대 목록에서 프리팹에 없는 것을 돌려준다. <b>본 검사와 네거티브 컨트롤이
        /// 같은 함수를 쓴다</b> — 다른 함수를 쓰면 «탐지기가 살아 있다»가 본 검사를 증명하지 못한다.</summary>
        private static List<string> MissingFrom(IEnumerable<string> expected, HashSet<string> present)
            => expected.Where(name => !present.Contains(name)).ToList();

        // ====================================================================
        // ★ 네거티브 컨트롤 먼저
        // ====================================================================

        /// <summary>★ <b>본 검사보다 먼저 읽어야 하는 검사.</b> 실제 프리팹 본문에서 컴포넌트 하나의
        /// 스크립트 참조를 지운 합성 입력을 만들어, 같은 비교기가 그것을 <b>반드시</b> 잡는지 본다.</summary>
        [Test]
        public void 탐지기_자체가_동작한다_컴포넌트를_하나_빼면_반드시_잡는다()
        {
            SortedSet<string> expected = ExpectedRootComponents();
            string prefab = PrefabText();

            // 실재하는 컴포넌트 하나를 골라 그 GUID 줄만 지운다(YAML 전체를 깨뜨리지 않는다 —
            // 우리가 재려는 것은 "비교기가 부재를 잡는가"이지 "파서가 쓰레기를 견디는가"가 아니다).
            string victim = expected.First(n => ComponentNamesIn(prefab).Contains(n));
            string victimGuid = AssetDatabase.AssetPathToGUID(ScriptAssetPath(victim));
            Assert.IsNotEmpty(victimGuid, $"{LogPrefix} 전제가 깨졌습니다 — {victim}.cs의 GUID를 못 찾았습니다.");

            string damaged = string.Join("\n",
                prefab.Split('\n').Where(line => !line.Contains(victimGuid)));

            List<string> missing = MissingFrom(expected, ComponentNamesIn(damaged));
            Assert.Contains(victim, missing,
                $"{LogPrefix} 일부러 뺀 «{victim}»을 비교기가 잡지 못했습니다 — 탐지기가 고장났으므로 " +
                "이 파일의 다른 초록불은 전부 무의미합니다.");

            // 반대 방향(오탐)도 같이 잰다 — 멀쩡한 본문에서 그것이 «없다»고 나오면 안 된다.
            Assert.IsFalse(MissingFrom(expected, ComponentNamesIn(prefab)).Contains(victim),
                $"{LogPrefix} 멀쩡한 프리팹에서 «{victim}»을 없다고 신고했습니다(오탐).");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — «{victim}»을 빼자 정확히 그것만 검출됐습니다.");
        }

        private static string ScriptAssetPath(string typeName)
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string[] hits = Directory.GetFiles(scripts, typeName + ".cs", SearchOption.AllDirectories);
            Assert.AreEqual(1, hits.Length,
                $"{LogPrefix} {typeName}.cs를 {hits.Length}개 찾았습니다 — 1개여야 합니다.");
            return "Assets" + hits[0].Substring(Application.dataPath.Length).Replace('\\', '/');
        }

        // ====================================================================
        // ① 본 검사 — 목록과 실물이 같은가
        // ====================================================================

        /// <summary>
        /// ★★ 부트스트래퍼가 붙이기로 한 컴포넌트가 <b>전부</b> 출하 프리팹에 있다.
        /// <para>실패하면 고치는 법은 하나다 — 메뉴 <c>StickMate/Ensure Prefab Components</c>
        /// (배치: <c>-executeMethod StickMate.EditorTools.SceneBootstrapper.EnsurePrefabComponents</c>).
        /// 2026-09-06부터는 <b>빌드가 그것을 자동으로 부른다</b>.</para>
        /// </summary>
        [Test]
        public void 부트스트래퍼가_붙이기로_한_컴포넌트가_전부_출하_프리팹에_있다()
        {
            SortedSet<string> expected = ExpectedRootComponents();
            HashSet<string> present = ComponentNamesIn(PrefabText());

            List<string> missing = MissingFrom(expected, present);

            Assert.IsEmpty(missing,
                $"{LogPrefix} 부트스트래퍼에는 적혀 있지만 출하 프리팹에 없는 컴포넌트 {missing.Count}건:\n" +
                string.Join("\n", missing.Select(n => "    · " + n)) +
                $"\n\n  기대 {expected.Count}개 / 프리팹에 존재 {present.Count}개.\n" +
                "  이 상태로 빌드하면 그 기능은 «코드가 있는데 실행되지 않는» 형태로 출하됩니다 —\n" +
                "  로그도 남지 않아 «감지가 깨졌다»로 오진됩니다(2026-09-06 실제 사고).\n" +
                "  고치는 법: 메뉴 StickMate/Ensure Prefab Components 실행 후 프리팹을 커밋하십시오.");

            Debug.Log($"{LogPrefix} 기대 {expected.Count}개 전부 출하 프리팹에 존재합니다.");
        }

        /// <summary>배선 관례상 부트스트래퍼가 채우는 필드 이름. <c>SceneBootstrapper.WireSerializedReferences</c>가
        /// 훑는 이름과 같아야 의미가 있다.</summary>
        private static readonly string[] WiredFieldNames = { "_player", "_config" };

        /// <summary>
        /// ★★ 유니티가 이 필드를 <b>직렬화하는가</b>. 이 판정이 이 파일에서 가장 중요한 한 줄이다.
        ///
        /// <para><b>2026-09-06 첫 실전 러너에서 이 검사가 오탐 3건을 냈다</b> —
        /// <c>InfoGearIconWidget._config</c> / <c>GearRadialMenuWidget._config</c> /
        /// <c>AppControlDirector._config</c>. 셋 다 <c>[SerializeField]</c>가 <b>없는</b> 순수 private
        /// 필드였고, <c>Awake()</c>/<c>Start()</c>에서 <c>_agent.Config</c>로 채운다. 프리팹 <b>애셋</b>은
        /// 직렬화되는 필드만 담으므로 그 셋은 애셋 위에서 <b>영원히 null</b>이다.</para>
        ///
        /// <para>★ 그때 실패 메시지가 처방으로 «메뉴 StickMate/Ensure Prefab Components를 실행하라»고
        /// 적었는데 <b>그 처방으로는 절대 안 고쳐진다</b>. 그 도구의 <c>TryWireField</c>는
        /// <c>SerializedObject.FindProperty</c>가 <c>null</c>을 돌려주면 그냥 <c>0</c>을 반환한다 —
        /// 즉 <b>도구는 옳게 건너뛰고 검사기만 틀렸다</b>. 처방을 따르면 «실행했는데 여전히 빨갛다»가
        /// 반복되고, 그 다음 수순은 대개 «감사를 지운다»이다. 그래서 여기서 갈래를 나눈다.</para>
        ///
        /// <para><b>도구와 다른 방법으로 잰다</b>(CLAUDE.md: 생성기와 검사기가 코드를 공유하면 둘 다
        /// 같은 방향으로 틀린다). 도구는 <c>SerializedObject.FindProperty</c>로 판정하고, 이쪽은
        /// <b>리플렉션 애트리뷰트</b>로 판정한다 — 한쪽이 눈이 멀어도 다른 쪽이 살아 있다.</para>
        /// </summary>
        private static bool IsUnitySerializedField(System.Reflection.FieldInfo field)
        {
            // readonly는 [SerializeField]가 붙어 있어도 유니티가 직렬화하지 않는다 —
            // 이 갈래를 빼면 «애셋이 담을 수 없는 값»을 다시 «비어 있다»로 신고하게 된다.
            if (field.IsInitOnly) return false;

            return field.IsPublic
                ? !field.IsDefined(typeof(NonSerializedAttribute), inherit: false)
                : field.IsDefined(typeof(SerializeField), inherit: false);
        }

        /// <summary>
        /// ★ <b>붙어 있는 것과 배선된 것은 다르다.</b> <c>_player</c>가 <c>null</c>이면 대부분의 감독은
        /// <c>Update()</c> 첫 줄에서 <b>조용히 반환</b>한다 — «컴포넌트가 없다»보다 찾기 어려운 형태다.
        ///
        /// <para><b>측정 방법이 위 검사와 다르다</b>: 위는 파일 텍스트를 읽고, 이쪽은 유니티가 실제로
        /// 임포트한 오브젝트를 연다. 같은 방법으로 두 번 재는 것은 검증이 아니다.</para>
        ///
        /// <para>★ <b>직렬화되는 필드만 본다</b>(<see cref="IsUnitySerializedField"/>의 경위 참고).
        /// 비직렬화 필드는 «애셋에 값이 없는 것이 정상»이므로 여기서 신고하면 아무도 고칠 수 없는
        /// 빨간불이 된다. 대신 그것들은 아래
        /// <see cref="비직렬화_참조는_런타임에_실제로_채워진다"/>가 다른 자로 잡는다 —
        /// <b>면제가 곧 사각지대가 되지 않게</b> 갈래를 옮겨 놓을 뿐이다.</para>
        /// </summary>
        [Test]
        public void 프리팹의_감독들이_실제로_배선돼_있다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            Assert.IsNotNull(prefab, $"{LogPrefix} 프리팹을 열 수 없습니다: {PrefabAssetPath}");

            var unwired = new List<string>();
            int checkedFields = 0;
            int skippedRuntimeFields = 0;

            foreach (MonoBehaviour behaviour in prefab.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;   // MissingMonoScriptAudit 소관
                foreach (string fieldName in WiredFieldNames)
                {
                    System.Reflection.FieldInfo field = behaviour.GetType().GetField(fieldName,
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public);
                    if (field == null) continue;
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue;

                    if (!IsUnitySerializedField(field)) { skippedRuntimeFields++; continue; }

                    checkedFields++;
                    if (field.GetValue(behaviour) as UnityEngine.Object == null)
                    {
                        unwired.Add($"    · {behaviour.GetType().Name}.{fieldName}");
                    }
                }
            }

            Assert.Greater(checkedFields, 10,
                $"{LogPrefix} 검사한 직렬화 필드가 {checkedFields}개뿐입니다 — 스캔이 깨졌고 아래 «전부 " +
                "배선됐다»는 공허합니다. (직렬화 판정에서 제외된 런타임 필드는 " +
                $"{skippedRuntimeFields}개였습니다 — 이 수가 비정상적으로 크면 " +
                $"{nameof(IsUnitySerializedField)}이(가) 전부를 «비직렬화»로 오판하고 있는 것입니다.)");

            Assert.IsEmpty(unwired,
                $"{LogPrefix} 프리팹에 붙어 있지만 <b>비어 있는</b> 직렬화 참조 {unwired.Count}건:\n" +
                string.Join("\n", unwired) +
                "\n\n  그 컴포넌트는 씬에 존재하지만 아무 일도 하지 않습니다.\n" +
                "  고치는 법: 메뉴 StickMate/Ensure Prefab Components 실행 후 프리팹을 커밋하십시오.\n" +
                "  (여기 뜬 것은 전부 [SerializeField]가 붙은 필드이므로 그 도구가 실제로 채웁니다 —\n" +
                "   비직렬화 필드는 도구도 못 채우고 채울 필요도 없어서 이 목록에 오지 않습니다.)");

            Debug.Log($"{LogPrefix} 직렬화 참조 {checkedFields}개 전부 채워져 있습니다" +
                $"(런타임 해석 필드 {skippedRuntimeFields}개는 아래 별도 검사가 봅니다).");
        }

        /// <summary>
        /// ★ 위 검사가 <b>면제한 것</b>을 다른 자로 잰다. 면제가 사각지대가 되면 «[SerializeField]를
        /// 지워서 빨간불을 끄는» 것이 가장 쉬운 «수정»이 되고, 그 순간 그 필드는 <b>영원히 null</b>이 된다
        /// (애셋도 안 채우고 코드도 안 채운다).
        ///
        /// <para><b>측정 방법이 또 다르다</b>: 위 둘은 임포트된 오브젝트를 열고, 이쪽은 <b>소스 텍스트</b>에
        /// 그 필드로의 <b>대입</b>이 실제로 있는지 본다.</para>
        /// </summary>
        [Test]
        public void 비직렬화_참조는_런타임에_실제로_채워진다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            Assert.IsNotNull(prefab, $"{LogPrefix} 프리팹을 열 수 없습니다: {PrefabAssetPath}");

            var dead = new List<string>();
            var examined = new List<string>();

            foreach (MonoBehaviour behaviour in prefab.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                Type type = behaviour.GetType();
                foreach (string fieldName in WiredFieldNames)
                {
                    System.Reflection.FieldInfo field = type.GetField(fieldName,
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public);
                    if (field == null) continue;
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue;
                    if (IsUnitySerializedField(field)) continue;

                    string source = StripLineComments(ReadTypeSource(type.Name));
                    examined.Add($"{type.Name}.{fieldName}");
                    if (!AssignsField(source, fieldName))
                    {
                        dead.Add($"    · {type.Name}.{fieldName} — [SerializeField]도 없고 대입도 없습니다");
                    }
                }
            }

            Assert.IsEmpty(dead,
                $"{LogPrefix} 애셋도 안 채우고 코드도 안 채우는 참조 {dead.Count}건:\n" +
                string.Join("\n", dead) +
                "\n\n  이 필드는 실행 중 영원히 null이라 그 컴포넌트의 해당 경로는 죽어 있습니다.\n" +
                "  고치는 법은 둘 중 하나입니다 — [SerializeField]를 붙여 프리팹이 담게 하거나,\n" +
                "  Awake()/Start()에서 실제로 대입하거나. 빨간불을 끄려고 애트리뷰트만 지우면\n" +
                "  이 검사가 바로 그것을 잡습니다.");

            // ★ 양성 대조 — 이 검사가 실제로 무언가를 보고 있는가. 대상이 0건이면 위 IsEmpty는
            //   «언제나 통과»이고 그건 초록불이 아니라 눈이 먼 것이다.
            Assert.IsNotEmpty(examined,
                $"{LogPrefix} 검사 대상 비직렬화 필드가 0건입니다 — 프리팹 구성이 바뀌었거나 " +
                $"{nameof(IsUnitySerializedField)}이(가) 전부를 «직렬화»로 오판하고 있습니다. " +
                "둘 중 무엇이든 이 검사는 아무것도 증명하지 못하는 상태입니다.");

            Debug.Log($"{LogPrefix} 런타임 해석 필드 {examined.Count}개 전부 대입이 존재합니다: " +
                string.Join(", ", examined));
        }

        /// <summary><paramref name="source"/> 안에 <paramref name="fieldName"/>으로의 <b>대입</b>이 있는가.
        /// <para>«이름이 등장하는가»가 아니다 — 비교(<c>==</c>/<c>!=</c>)까지 세면 <c>if (_config != null)</c>
        /// 한 줄로 통과해 버려서 이 검사는 아무것도 증명하지 못한다. 앞자리에 <c>.</c>은 허용한다
        /// (<c>this._config = …</c>도 진짜 대입이다).</para></summary>
        private static bool AssignsField(string source, string fieldName)
            => Regex.IsMatch(source,
                @"(^|[^\w])" + Regex.Escape(fieldName) + @"\s*(=[^=]|\?\?=)",
                RegexOptions.Multiline);

        /// <summary>타입 이름에 대응하는 소스 전문. <b>partial 분할 파일까지</b> 모아 붙인다 —
        /// 대입이 다른 조각에 있으면 «대입이 없다»는 거짓 빨강이 된다.</summary>
        private static string ReadTypeSource(string typeName)
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(scripts, typeName + ".cs", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(scripts, typeName + ".*.cs", SearchOption.AllDirectories));

            Assert.IsNotEmpty(files,
                $"{LogPrefix} {typeName}의 소스를 찾지 못했습니다 — 빈 문자열 위에서는 어떤 " +
                "«대입이 있다» 판정도 무조건 실패하고, 어떤 «없다» 판정도 무조건 통과합니다.");

            var sb = new StringBuilder();
            foreach (string file in files) sb.Append(File.ReadAllText(file)).Append('\n');
            return sb.ToString().Replace("\r\n", "\n");
        }

        // ====================================================================
        // ★ 직렬화 판정기 자체의 네거티브/양성 대조
        // ====================================================================

#pragma warning disable 0169, 0649   // 대조용 표본 — 읽지도 쓰지도 않는다.
        private sealed class SerializationProbe
        {
            [SerializeField] private GameObject _serializedPrivate;
            private GameObject _plainPrivate;
            public GameObject PlainPublic;
            [NonSerialized] public GameObject OptedOutPublic;
            [SerializeField] private readonly GameObject _serializedButReadonly = null;
        }
#pragma warning restore 0169, 0649

        /// <summary>★ <see cref="IsUnitySerializedField"/>가 <b>실제로 갈래를 가르는가</b>.
        /// 무조건 <c>true</c>면 2026-09-06의 오탐 3건이 그대로 돌아오고, 무조건 <c>false</c>면
        /// 배선 감사 전체가 조용히 «검사 0건 초록»이 된다 — 둘 다 성공한 측정과 똑같이 생겼다.</summary>
        [Test]
        public void 컨트롤_직렬화_판정기가_다섯_가지_형태를_정확히_가른다()
        {
            System.Reflection.FieldInfo Field(string name) =>
                typeof(SerializationProbe).GetField(name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);

            Assert.IsTrue(IsUnitySerializedField(Field("_serializedPrivate")),
                $"{LogPrefix} [SerializeField] private을 «비직렬화»로 봅니다 — 진짜 배선 누락이 " +
                "면제되어 조용히 초록이 됩니다(부재 단언이 썩는 형태).");
            Assert.IsFalse(IsUnitySerializedField(Field("_plainPrivate")),
                $"{LogPrefix} 순수 private을 «직렬화»로 봅니다 — 2026-09-06 오탐 3건이 그대로 " +
                "돌아옵니다(애셋이 담을 수 없는 값을 «비어 있다»고 신고).");
            Assert.IsTrue(IsUnitySerializedField(Field("PlainPublic")),
                $"{LogPrefix} public 필드를 «비직렬화»로 봅니다 — 유니티는 public 필드를 기본 직렬화합니다.");
            Assert.IsFalse(IsUnitySerializedField(Field("OptedOutPublic")),
                $"{LogPrefix} [NonSerialized] public을 «직렬화»로 봅니다.");
            Assert.IsFalse(IsUnitySerializedField(Field("_serializedButReadonly")),
                $"{LogPrefix} [SerializeField] readonly를 «직렬화»로 봅니다 — 유니티는 readonly를 " +
                "직렬화하지 않으므로 애셋은 그 값을 담을 수 없고, 담을 수 없는 것을 «비어 있다»고 " +
                "신고하면 아무도 못 고치는 빨간불이 됩니다(2026-09-06 오탐 3건과 같은 형태).");
        }

        /// <summary>★ <see cref="AssignsField"/>가 <b>대입과 비교를 가르는가</b>. 가르지 못하면
        /// <see cref="비직렬화_참조는_런타임에_실제로_채워진다"/>는 «이름이 어딘가 나오면 통과»가 되고,
        /// 그건 초록불이 아니라 눈이 먼 것이다.</summary>
        [Test]
        public void 컨트롤_대입_탐지기가_비교문에_속지_않는다()
        {
            // (a) 대입으로 세야 하는 형태 — 이 저장소에 실재하는 관용구들.
            foreach (string yes in new[]
            {
                "            _config = _agent != null ? _agent.Config : null;",
                "        this._config = value;",
                "            _config ??= FindConfig();",
                "_config=x;",
            })
            {
                Assert.IsTrue(AssignsField(yes, "_config"),
                    $"{LogPrefix} 실제 대입을 못 봅니다 — 거짓 빨강이 납니다: {yes.Trim()}");
            }

            // (b) 대입이 아닌 형태 — 여기에 속으면 «죽은 필드»를 영원히 못 잡는다.
            foreach (string no in new[]
            {
                "            if (_config != null) return;",
                "            return _config == null ? 0f : _config.scale;",
                "            float v = _configCache = 1f;",     // 다른 필드다
                "            Use(_config);",
                "            private StickConfig _config;",
            })
            {
                Assert.IsFalse(AssignsField(no, "_config"),
                    $"{LogPrefix} 대입이 아닌 줄을 대입으로 셉니다 — 죽은 필드가 조용히 통과합니다: {no.Trim()}");
            }
        }

        // ====================================================================
        // ② 파이프라인 — 빌드가 그 대조를 <b>스스로</b> 거치는가
        // ====================================================================

        /// <summary>
        /// ★★★ <b>이번 사고의 근본 원인에 대한 자물쇠.</b> 위 두 검사는 «지금 어긋났는가»를 재고,
        /// 이 검사는 «다시 어긋나도 빌드가 스스로 고치는가»를 잰다.
        ///
        /// <para><b>양 플랫폼을 함께 잰다</b>(CLAUDE.md 플랫폼 동시 검토). 한쪽에만 넣으면
        /// «Windows 빌드에만 컴포넌트가 빠지는» 형태가 되고, 그건 이 저장소가 반복해서 겪은
        /// «한 플랫폼에서만 조용히 어긋나는» 실패다. Windows 빌드는 이 머신에서 실행 검증이
        /// 불가능하므로 <b>소스로 잠그는 것이 유일한 수단</b>이다.</para>
        /// </summary>
        [Test]
        public void 두_플랫폼_빌드_모두_출하_전에_프리팹_배선을_거친다()
        {
            string src = StripLineComments(ReadRepoFile(BuildScriptRelativePath));

            const string hook = "EnsureShippedPrefabWiring()";
            foreach (string entryPoint in new[] { "PerformBuild", "PerformBuildWindows" })
            {
                string body = MethodBody(src, "public static void " + entryPoint + "()");
                StringAssert.Contains(hook, body,
                    $"{LogPrefix} {entryPoint}()가 출하 프리팹 배선 점검({hook})을 거치지 않습니다:\n" +
                    body.Trim() +
                    "\n이 한 줄이 없으면 신규 컴포넌트는 «누군가 메뉴를 눌러 준 빌드»에만 실립니다 — " +
                    "2026-09-06 사고가 정확히 그 형태였습니다.");
            }

            // 그 훅이 실제로 부트스트래퍼를 부르는가 — 위 단언은 «이름만 같은 빈 함수»로도 통과한다.
            string hookBody = MethodBody(src, "public static void EnsureShippedPrefabWiring()");
            StringAssert.Contains("SceneBootstrapper.EnsurePrefabComponents", hookBody,
                $"{LogPrefix} EnsureShippedPrefabWiring()가 부트스트래퍼를 부르지 않습니다:\n" + hookBody.Trim());

            Debug.Log($"{LogPrefix} macOS/Windows 빌드 진입점 둘 다 출하 프리팹 배선 점검을 거칩니다.");
        }

        /// <summary>시그니처 다음의 균형 잡힌 중괄호 블록을 뜬다.
        /// <para><b>양성 대조 포함</b>: 시그니처를 못 찾거나 본문이 지나치게 짧으면 즉시 실패한다 —
        /// 빈 문자열 위에서는 <c>Contains</c>가 언제나 실패하고 <c>IndexOf==-1</c>은 언제나 통과한다.</para></summary>
        private static string MethodBody(string src, string signature)
        {
            int at = src.IndexOf(signature, StringComparison.Ordinal);
            Assert.Greater(at, 0,
                $"{LogPrefix} \"{signature}\"을(를) 찾지 못했습니다 — 시그니처가 바뀌었다면 이 감사도 " +
                "함께 고치십시오(이 단언이 없으면 빈 문자열 위에서 검사가 통과합니다).");

            int open = src.IndexOf('{', at);
            Assert.Greater(open, at, $"{LogPrefix} 본문의 여는 중괄호를 찾지 못했습니다: {signature}");

            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth != 0) continue;
                    string body = src.Substring(open, i - open + 1);
                    Assert.Greater(body.Length, 40,
                        $"{LogPrefix} 본문을 {body.Length}자밖에 못 떴습니다 — 스캔이 깨졌습니다: {signature}");
                    return body;
                }
            }

            Assert.Fail($"{LogPrefix} 본문의 닫는 중괄호를 찾지 못했습니다: {signature}");
            return string.Empty;
        }
    }
}
