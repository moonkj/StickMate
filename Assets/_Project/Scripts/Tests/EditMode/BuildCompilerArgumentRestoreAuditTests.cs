using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ security <b>L5 · L6</b>(2026-09-28) — 배포물에서 이 PC의 절대경로를 지운 조치를 잠근다.
    /// 조사 정본은 <c>docs/security/BUILD_PDB_PATH_LEAK.md</c>이고, 이 감사는 그 문서 §4-3이
    /// 요청한 「원복 감사」다.
    ///
    /// ============================================================================
    /// 이 감사가 겨누는 실패 형태 — <b>원복 누락은 조용하다</b>
    /// ============================================================================
    /// 빌드 스크립트는 <c>pathmap</c> 인자(키가 <b>이 기계의 절대경로</b>)를 빌드 동안만 걸고
    /// <c>finally</c>에서 원복한다. 그 원복이 빠지면 값이
    /// <c>ProjectSettings/ProjectSettings.asset</c>에 남는데 <b>그 파일은 추적 파일</b>이다 —
    /// 즉 누출이 배포물에서 <b>공개 저장소로 옮겨 간다</b>. 그리고 그 상태는 아무 증상도 내지 않는다.
    ///
    /// <para>★ <b>부재 단언만 두면 조용히 초록이 되는 방향</b>이다(CLAUDE.md). 「pathmap이 없다」는
    /// <b>기능이 통째로 사라져도</b> 참이 된다. 그래서 이 파일의 부재 단언은 전부
    /// <b>같은 테스트 안의 존재 대조</b>와 짝지어 둔다 — 키 줄이 실재하는가, 그리고 그 값을 거는
    /// 프로덕션 함수가 실재하는가.</para>
    ///
    /// ============================================================================
    /// ★ 니들을 베끼지 않는다 — 기준은 프로덕션에서 온다
    /// ============================================================================
    /// 인자 접두사는 <c>BuildStandalone</c>의 공개 상수에서 <b>리플렉션으로 읽어</b> 쓴다.
    /// 메서드 이름도 먼저 리플렉션으로 찾아 <see cref="MemberInfo.Name"/>을 니들로 쓴다 —
    /// 이름이 바뀌면 소스 스캔이 조용히 0을 내는 대신 <b>리플렉션 단계에서 시끄럽게 빨개진다</b>.
    ///
    /// <para><b>예외 하나</b>: <c>additionalCompilerArguments</c>는 우리 상수가 아니라
    /// <b>Unity의 직렬화 키</b>라 소스에서 유도할 곳이 없다. 그래서 그 이름은 문자열이지만
    /// <b>존재 단언</b>으로 쓰므로 Unity가 키를 바꾸면 이 테스트가 먼저 빨개진다(조용히 통과하지 않는다).</para>
    /// </summary>
    public sealed class BuildCompilerArgumentRestoreAuditTests
    {
        private const string BuildScriptTypeName = "StickMate.EditorTools.BuildStandalone";

        /// <summary>Unity의 직렬화 키 이름. 우리 상수가 아니라 외부 스키마다 — 위 문단 참고.</summary>
        private const string UnitySerializedKeyName = "additionalCompilerArguments";

        /// <summary>계측기 교정용 — <b>절대 존재하지 않아야</b> 하는 이름.
        /// 이게 찾아지면 이 파일의 모든 부재 단언이 무효다.</summary>
        private const string AbsentMemberName = "이_이름의_멤버는_BuildStandalone에_없다";

        /// <summary>빌드를 실제로 부르는 자리를 소스에서 알아보는 표지.</summary>
        private const string BuildPlayerCall = "BuildPipeline.BuildPlayer";

        private static Type BuildScript => PredefinedEditorAssemblyProbe.RequireType(BuildScriptTypeName);

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        private static string BuildScriptSourcePath =>
            Path.Combine(ProjectRoot, "Assets", "Editor", "BuildStandalone.cs");

        private static string ProjectSettingsPath =>
            Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset");

        // ==================== 도구 ====================

        private static MethodInfo RequireMethod(string name)
        {
            MethodInfo m = PredefinedEditorAssemblyProbe.FindPublicStaticMethod(BuildScript, name);
            Assert.IsNotNull(m,
                $"'{name}'을 찾지 못했습니다 — 배포물에서 절대경로를 지우는 조치가 사라졌거나 이름이 " +
                "바뀌었습니다. 이름이 바뀐 것이라면 이 감사도 함께 고치십시오(이 단언이 없으면 아래 " +
                "부재 단언들이 «기능이 없어서» 조용히 통과합니다).");
            return m;
        }

        private static string RequireStringConstant(string name)
        {
            object value = PredefinedEditorAssemblyProbe.FindPublicStaticConstant(BuildScript, name);
            var text = value as string;
            Assert.IsFalse(string.IsNullOrEmpty(text),
                $"공개 상수 '{name}'을 읽지 못했습니다 — 이 감사는 기준 문자열을 프로덕션에서 " +
                "유도하므로, 이 상수가 없으면 아무것도 잴 수 없습니다.");
            return text;
        }

        /// <summary>비공개 상수도 <b>이름으로</b> 읽는다 — 배포 폴더 경로를 테스트에 베끼지 않기 위해서다.</summary>
        private static string RequirePrivateStringConstant(string name)
        {
            FieldInfo f = BuildScript.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, $"비공개 상수 '{name}'을 찾지 못했습니다 — 배포 폴더 경로의 정본이 " +
                "사라졌거나 이름이 바뀌었습니다. 이 값을 테스트에 베끼지 않으려고 여기서 읽습니다.");
            var text = f.GetValue(null) as string;
            Assert.IsFalse(string.IsNullOrEmpty(text), $"'{name}'의 값이 비어 있습니다.");
            return text;
        }

        private static string ReadSource(string path)
        {
            Assert.IsTrue(File.Exists(path),
                $"소스를 찾지 못했습니다: {path}. 파일이 옮겨졌다면 이 경로를 갱신하십시오 — " +
                "그대로 두면 아래 단언이 전부 공허해집니다.");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary>시그니처 다음의 균형 잡힌 중괄호 블록을 뜬다(못 찾거나 너무 짧으면 즉시 실패).</summary>
        private static string MethodBody(string src, string signature)
        {
            int at = src.IndexOf(signature, StringComparison.Ordinal);
            Assert.Greater(at, 0,
                $"\"{signature}\"을(를) 찾지 못했습니다 — 시그니처가 바뀌었다면 이 감사도 함께 " +
                "고치십시오(이 단언이 없으면 빈 문자열 위에서 검사가 통과합니다).");

            int open = src.IndexOf('{', at);
            Assert.Greater(open, at, $"본문의 여는 중괄호를 찾지 못했습니다: {signature}");

            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth != 0) continue;
                    string body = src.Substring(open, i - open + 1);
                    Assert.Greater(body.Length, 40, $"본문을 {body.Length}자밖에 못 떴습니다: {signature}");
                    return body;
                }
            }

            Assert.Fail($"본문의 닫는 중괄호를 찾지 못했습니다: {signature}");
            return string.Empty;
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        // ====================================================================
        // 1. 추적 파일에 pathmap이 남아 있지 않다 (문서 §4-3이 요청한 감사)
        // ====================================================================

        /// <summary>
        /// ★ 순서가 이 테스트의 본체다. <b>① 키 줄이 실재함을 먼저 단언하고</b>,
        /// <b>② 그다음 같은 테스트에서 pathmap 부재를 단언한다.</b>
        /// ②만 두면 파일 경로가 틀려도 · Unity가 키 이름을 바꿔도 · 기능이 사라져도 초록이다.
        /// </summary>
        [Test]
        public void 추적되는_프로젝트설정에_pathmap_인자가_남아_있지_않다()
        {
            // ── 기준을 프로덕션에서 읽는다(니들을 베끼지 않는다) ──
            string prefix = RequireStringConstant("PathMapArgumentPrefix");
            RequireMethod("WithProjectPathMap");        // 기능이 실재하는가 — 부재 단언의 비공허성
            RequireMethod("RestoreAdditionalCompilerArguments");

            // 음성(계측기 교정) — 없는 이름은 정말로 못 찾아야 한다.
            Assert.IsNull(PredefinedEditorAssemblyProbe.FindPublicStaticMethod(BuildScript, AbsentMemberName),
                "존재하지 않는 이름이 찾아졌습니다 — 이 파일의 모든 '있다/없다' 판정을 폐기하십시오.");

            string settings = ReadSource(ProjectSettingsPath);

            // ── ① 존재 대조 — 그 키 줄이 실재한다 ──
            int at = settings.IndexOf(UnitySerializedKeyName, StringComparison.Ordinal);
            Assert.Greater(at, -1,
                $"'{UnitySerializedKeyName}' 키를 {ProjectSettingsPath}에서 찾지 못했습니다.\n" +
                "Unity가 키 이름을 바꿨거나 이 감사가 엉뚱한 파일을 읽고 있습니다 — 어느 쪽이든 " +
                "아래 '남아 있지 않다' 판정은 아무것도 재지 않은 결과이므로 믿지 마십시오.");

            // ── 네거티브 컨트롤 — 이 계수 방식이 니들을 실제로 잡는가 ──
            string needle = prefix.Substring(1);   // 대시형·슬래시형 어느 쪽이든 잡히는 몸통
            Assert.AreEqual(1, CountOccurrences(settings + "\n# " + needle, needle),
                "네거티브 컨트롤 실패 — 니들을 일부러 끼워 넣었는데 1건으로 세지지 않았습니다. " +
                "아래 부재 단언은 신뢰할 수 없습니다.");

            // ── ② 부재 단언 — 파일 어디에도 남아 있지 않다 ──
            //     한 줄만 보지 않는다: 값이 비어 있지 않으면 YAML이 여러 줄로 펼쳐지므로,
            //     키 줄만 검사하면 다음 줄에 남은 인자를 구조적으로 못 본다.
            Assert.AreEqual(0, CountOccurrences(settings, needle),
                $"{ProjectSettingsPath}에 '{needle}'이 남아 있습니다 — 빌드 중에만 걸어야 하는 인자가 " +
                "원복되지 않았습니다. 그 키는 이 기계의 절대경로이고 이 파일은 추적 대상이라, 이 상태로 " +
                "커밋하면 배포물에서 막은 누출이 공개 저장소로 옮겨 갑니다. Player Settings의 " +
                "Additional Compiler Arguments에서 그 항목을 지우고 다시 돌리십시오.");
        }

        // ====================================================================
        // 2. 두 진입점이 같은 쌍을 쓴다 (한쪽만 걸면 다른 쪽 배포물에 경로가 남는다)
        // ====================================================================

        [Test]
        public void macOS와_Windows_빌드_경로가_모두_pathmap을_걸고_finally에서_원복한다()
        {
            string applyName = RequireMethod("ApplyProjectPathMap").Name;
            string restoreName = RequireMethod("RestoreAdditionalCompilerArguments").Name;

            string src = ReadSource(BuildScriptSourcePath);

            // 계측기 교정 — 이 텍스트 검색이 살아 있는가(있는 것/없는 것 양방향).
            StringAssert.Contains(applyName, src, "소스에서 적용 함수 이름조차 못 찾습니다 — 파일을 잘못 읽고 있습니다.");
            StringAssert.DoesNotContain(AbsentMemberName, src, "없어야 할 이름이 소스에 있습니다 — 교정 실패.");

            // ★ 진입점 «이름 목록»을 여기 베끼지 않는다. 빌드를 부르는 자리를 소스에서 열거해
            //   그 전부를 검사한다 — 새 진입점이 생기면 이 감사가 그것까지 자동으로 본다.
            //
            //   ★★ 왜 계수가 아닌가(내 첫 판이 이 함정에 빠졌다): 「적용 호출이 정확히 3회」로 세면
            //   방향이 거꾸로다. 새 진입점이 pathmap을 «걸면» 4회가 되어 빨개지고(정당한 작업이
            //   빨간불), «안 걸면» 3회가 유지되어 초록이다(누출이 초록불). 조용히 초록이 되는
            //   방향이므로 계수를 버리고 «호출 지점 열거»로 바꿨다.
            var entryPoints = new List<string>();
            foreach (var method in PublicStaticVoidMethods(src))
            {
                if (method.Value.IndexOf(BuildPlayerCall, StringComparison.Ordinal) < 0) continue;
                entryPoints.Add(method.Key);

                string body = method.Value;
                StringAssert.Contains(applyName, body,
                    $"'{method.Key}'가 빌드를 부르는데 pathmap을 걸지 않습니다 — 그 경로로 구운 배포물에는 " +
                    "이 PC의 절대경로가 계속 남습니다(한쪽만 고치는 사고를 막는 단언입니다).");
                StringAssert.Contains("finally", body,
                    $"'{method.Key}'에 finally가 없습니다 — 빌드가 예외로 끝나면 인자가 걸린 채 남고, " +
                    "그 값이 추적 파일에 저장되면 공개 저장소로 나갑니다.");
                StringAssert.Contains(restoreName, body,
                    $"'{method.Key}'가 원복을 부르지 않습니다.");

                int applyAt = body.IndexOf(applyName, StringComparison.Ordinal);
                int restoreAt = body.IndexOf(restoreName, StringComparison.Ordinal);
                int buildAt = body.IndexOf(BuildPlayerCall, StringComparison.Ordinal);
                Assert.Less(applyAt, buildAt,
                    $"'{method.Key}'에서 pathmap 적용이 빌드 호출보다 뒤에 있습니다 — 이미 컴파일된 뒤에 " +
                    "걸어 봐야 산출물은 옛 경로를 담고 있습니다.");
                Assert.Less(buildAt, restoreAt,
                    $"'{method.Key}'에서 원복이 빌드 호출보다 먼저 나옵니다 — 그러면 빌드는 인자 없이 " +
                    "돌고, 순서가 뒤집힌 것을 아무도 모릅니다.");
            }

            // 양성 대조 — 열거가 실제로 진입점을 잡았는가. 0개면 위 foreach는 아무것도 검사하지 않는다
            // (빈 목록 위에서는 어떤 단언도 통과한다 — 이 저장소의 거짓 통과 5번째 형태).
            Assert.GreaterOrEqual(entryPoints.Count, 2,
                "빌드를 부르는 공개 진입점을 " + entryPoints.Count + "개만 찾았습니다(기대 2개 이상: macOS · Windows). " +
                "시그니처 모양이 바뀌었다면 이 열거도 함께 고치십시오 — 그대로 두면 이 테스트는 " +
                "아무것도 재지 않은 채 초록이 됩니다.");
            Debug.Log("[빌드인자원복-TEST] pathmap 쌍을 검사한 진입점: " + string.Join(", ", entryPoints));
        }

        /// <summary>
        /// <c>public static void</c> 메서드를 이름과 본문 쌍으로 훑는다.
        /// <para>클래스 머리 주석에도 빌드 호출 문구가 있으므로 <b>메서드 본문 안</b>만 보게 하는 것이
        /// 이 훑기의 목적이다 — 주석 한 줄을 진입점으로 셈하면 열거가 조용히 부풀어 오른다.</para>
        /// </summary>
        private static List<KeyValuePair<string, string>> PublicStaticVoidMethods(string src)
        {
            var found = new List<KeyValuePair<string, string>>();
            const string marker = "public static void ";
            int i = 0;
            while ((i = src.IndexOf(marker, i, StringComparison.Ordinal)) >= 0)
            {
                int nameStart = i + marker.Length;
                int paren = src.IndexOf('(', nameStart);
                if (paren < 0) break;

                string name = src.Substring(nameStart, paren - nameStart);
                int open = src.IndexOf('{', paren);
                if (open < 0) break;

                int depth = 0;
                for (int k = open; k < src.Length; k++)
                {
                    if (src[k] == '{') depth++;
                    else if (src[k] == '}')
                    {
                        depth--;
                        if (depth != 0) continue;
                        found.Add(new KeyValuePair<string, string>(name, src.Substring(open, k - open + 1)));
                        break;
                    }
                }
                i = paren;
            }
            return found;
        }

        // ====================================================================
        // 3. 빌드 영수증이 배포 폴더 안에 없다 (L6)
        // ====================================================================

        /// <summary>
        /// ★ 이 테스트는 니들이 아니라 <b>함수를 실제로 돌려</b> 판정한다 — 경로 계산이 바뀌면
        /// 문자열 검색보다 먼저 여기서 걸린다.
        ///
        /// <para>부작용 0: <see cref="BuildStandalone.BuildReceiptDirectory"/>는 순수 계산이라
        /// 폴더를 만들지 않는다(쓰기를 하는 <c>ResolveBuildReceiptPath</c>는 부르지 않는다).</para>
        /// </summary>
        [Test]
        public void 빌드_영수증_경로가_배포_폴더_밖을_가리킨다()
        {
            MethodInfo receiptDirectory = RequireMethod("BuildReceiptDirectory");
            var dir = receiptDirectory.Invoke(null, null) as string;

            // 양성 — 계기가 실제 값을 냈는가.
            Assert.IsFalse(string.IsNullOrEmpty(dir), "영수증 폴더 계산이 빈 값을 냈습니다.");
            StringAssert.StartsWith(ProjectRoot, dir,
                "영수증 폴더가 프로젝트 밖을 가리킵니다 — 어디에 쓰는지 아무도 모르게 됩니다.");

            // 부재 — 두 배포 폴더 «안»이 아니다. 경로 정본은 프로덕션 상수에서 읽는다.
            foreach (string constantName in new[] { "BuildSubFolder", "WindowsBuildSubFolder" })
            {
                string buildFolder = Path.Combine(ProjectRoot,
                    RequirePrivateStringConstant(constantName).Replace('/', Path.DirectorySeparatorChar));

                // 네거티브 컨트롤 — 이 비교가 «안에 있는 경로»를 실제로 잡는가.
                string inside = Path.Combine(buildFolder, "probe.txt");
                Assert.IsTrue(inside.StartsWith(buildFolder, StringComparison.Ordinal),
                    "네거티브 컨트롤 실패 — 배포 폴더 안의 경로를 «안»으로 판정하지 못했습니다. " +
                    "아래 부재 단언은 신뢰할 수 없습니다.");

                Assert.IsFalse(dir.StartsWith(buildFolder, StringComparison.Ordinal),
                    $"영수증이 배포 폴더({constantName}) 안에 쓰입니다. Windows 배포 단위는 폴더 전체라 " +
                    "그 파일이 공개 zip에 동봉되고, 안에 산출물 경로가 한 줄 들어갑니다(security L6). " +
                    "배포 단위 밖에 쓰십시오.");
            }

            // 두 훅이 그 자리를 실제로 쓰는가 — 경로 계산만 옳고 호출부가 옛 자리면 의미가 없다.
            string resolveName = RequireMethod("ResolveBuildReceiptPath").Name;
            string scrubName = RequireMethod("ToProjectRelativeText").Name;

            string windowsHook = ReadSource(Path.Combine(ProjectRoot, "Assets", "Editor",
                "WindowsHybridGpuExportPostprocessor.cs"));
            StringAssert.Contains(resolveName, windowsHook,
                "Windows 훅이 영수증 경로를 공용 계산에서 받지 않습니다 — 배포 폴더로 되돌아갔을 수 있습니다.");

            string macHook = ReadSource(Path.Combine(ProjectRoot, "Assets", "Editor",
                "MacHybridGpuInfoPlistPostprocessor.cs"));

            // 두 훅 모두 텍스트 검문소를 지나야 한다. macOS 영수증에는 codesign 출력이 실려
            // 절대경로가 4번 들어 있었다 — 위치가 아니라 «내용»이 새던 자리다.
            foreach (string hookSource in new[] { windowsHook, macHook })
            {
                StringAssert.Contains(scrubName, hookSource,
                    "영수증 텍스트가 검문소를 지나지 않습니다 — 실패 분기나 외부 도구 출력에 실린 " +
                    "절대경로가 그대로 파일에 남습니다.");
            }
        }
    }
}
