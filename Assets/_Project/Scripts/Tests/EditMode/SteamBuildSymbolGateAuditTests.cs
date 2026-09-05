using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ security 발견 <b>S-14</b>(2026-09-05) — <c>Assets/Editor/BuildStandalone.cs</c>가
    /// 스크립팅 정의 심볼을 <b>한 번도 보지 않아</b> <c>STICKMATE_STEAMWORKS_INSTALLED</c>가 빠진
    /// 빌드가 <b>조용히 성공</b>하던 것을 잠근다. Windows·macOS 두 경로에 똑같이 걸린다.
    ///
    /// ============================================================================
    /// 이 감사가 겨누는 실패 형태
    /// ============================================================================
    /// 심볼이 없으면 스팀 어댑터의 <c>Query</c>는 언제나 <c>Unknown</c>이다. 즉 <b>산 사람과 안 산
    /// 사람이 산출물에서 구분되지 않는다</b> — 실패한 빌드와 성공한 빌드가 똑같이 생겼다.
    /// 종료코드도, 로그도 그 차이를 말해 주지 않는다. 그래서 <b>빌드를 만들기 전에</b> 사실을 대조한다.
    ///
    /// ============================================================================
    /// ★ 왜 리플렉션인가 (그리고 그 대가)
    /// ============================================================================
    /// <c>BuildStandalone</c>은 <c>Assets/Editor/</c> = 사전정의 어셈블리라 이 테스트 어셈블리가
    /// <b>참조할 수 없다</b>(<see cref="PredefinedEditorAssemblyProbe"/> 문단, 같은 라운드의 N6).
    /// 대가는 <b>이름이 문자열</b>이라는 것이다. 그래서 모든 이름 단언에 <b>양방향 교정</b>을 붙인다 —
    /// 있는 이름은 반드시 찾고, 없는 이름은 반드시 못 찾아야 한다. 한쪽만 보면
    /// "이름이 틀린 것"과 "정말 없는 것"을 가를 수 없다.
    /// </summary>
    public sealed class SteamBuildSymbolGateAuditTests
    {
        private const string BuildScriptTypeName = "StickMate.EditorTools.BuildStandalone";
        private const string GateMethodName = "ShouldStopBuild";
        private const string VerifyMethodName = "VerifySteamEntitlementWiring";
        private const string TypeProbeMethodName = "IsTypeLoaded";

        /// <summary>계측기 교정용 — <b>절대 존재하지 않아야</b> 하는 이름.
        /// 이게 찾아지면 그 순간 이 파일의 모든 부재 단언이 무효다.</summary>
        private const string AbsentMemberName = "이_이름의_멤버는_BuildStandalone에_없다";

        /// <summary>알려진 값 — 반드시 로드돼 있는 타입. 타입 프로브의 양성 대조.</summary>
        private const string AlwaysLoadedTypeName = "UnityEngine.Debug";

        /// <summary>알려진 값 — 절대 로드돼 있지 않은 타입. 타입 프로브의 음성 대조.</summary>
        private const string NeverLoadedTypeName = "StickMate.이런.타입은.없다";

        private static Type BuildScript => PredefinedEditorAssemblyProbe.RequireType(BuildScriptTypeName);

        private static string BuildScriptSourcePath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "Assets", "Editor", "BuildStandalone.cs");

        // ==================== 게이트가 실재하는가 ====================

        [Test]
        public void 빌드_스크립트에_스팀_배선_게이트가_실재한다()
        {
            Type t = BuildScript;

            // 양성 — 있어야 하는 것이 있다.
            Assert.IsNotNull(PredefinedEditorAssemblyProbe.FindPublicStaticMethod(t, GateMethodName),
                $"'{GateMethodName}'이 없습니다 — 심볼 누락 빌드를 가르는 판정 자체가 사라졌습니다(S-14).");
            Assert.IsNotNull(PredefinedEditorAssemblyProbe.FindPublicStaticMethod(t, VerifyMethodName),
                $"'{VerifyMethodName}'이 없습니다 — 판정은 있는데 빌드가 그것을 부르지 않습니다.");
            Assert.IsNotNull(PredefinedEditorAssemblyProbe.FindPublicStaticMethod(t, TypeProbeMethodName),
                $"'{TypeProbeMethodName}'이 없습니다 — 패키지 유무를 재는 계측기가 사라졌습니다.");

            // 음성(계측기 교정) — 없는 이름은 정말로 못 찾아야 한다.
            Assert.IsNull(PredefinedEditorAssemblyProbe.FindPublicStaticMethod(t, AbsentMemberName),
                "존재하지 않는 이름이 찾아졌습니다 — 이 파일의 모든 '있다/없다' 판정을 폐기하십시오.");
        }

        // ==================== 판정 진리표 ====================

        /// <summary>
        /// 사실 셋(어댑터 파일 · Steamworks 패키지 · 정의 심볼)의 <b>여덟 조합을 전부</b> 먹인다.
        /// <para>★ 「어댑터가 있는데 심볼이 없으면 무조건 실패」로 만들지 <b>않은</b> 이유가 여기서
        /// 눈에 보인다 — 오늘 이 저장소의 상태가 정확히 (어댑터 O · 패키지 X · 심볼 X)이고,
        /// 그 규칙이었다면 <b>오늘 모든 빌드가 실패</b>한다. 패키지 없이 심볼만 켜면 어댑터의
        /// <c>using Steamworks;</c>가 풀리지 않아 컴파일이 깨지므로 <b>안전한 출구도 없다</b>.</para>
        /// </summary>
        [Test]
        public void 게이트가_여덟_조합을_전부_옳게_가른다()
        {
            //        어댑터  패키지  심볼   중단?
            AssertVerdict(false, false, false, false, "아무것도 배선 안 됨");
            AssertVerdict(true, false, false, false, "휴면 — 오늘 이 저장소의 상태");
            AssertVerdict(false, true, false, false, "패키지만 있고 우리 코드가 안 씀");
            AssertVerdict(true, true, false, true, "★ S-14 본체 — 패키지를 깔고 심볼을 빠뜨렸다");
            AssertVerdict(false, false, true, true, "심볼만 켰다(패키지 없음)");
            AssertVerdict(true, false, true, true, "심볼을 켰는데 패키지가 없다 = 컴파일이 깨진다");
            AssertVerdict(false, true, true, true, "심볼·패키지는 있는데 어댑터가 없다");
            AssertVerdict(true, true, true, false, "완전 배선");
        }

        private static void AssertVerdict(bool adapter, bool package, bool symbol, bool expectStop, string what)
        {
            MethodInfo m = PredefinedEditorAssemblyProbe.FindPublicStaticMethod(BuildScript, GateMethodName);
            Assert.IsNotNull(m, $"'{GateMethodName}'을 못 찾아 진리표를 잴 수 없습니다.");

            object[] args = { adapter, package, symbol, null };
            bool stop = (bool)m.Invoke(null, args);
            string reason = args[3] as string;

            Assert.AreEqual(expectStop, stop,
                $"[{what}] 어댑터={adapter} 패키지={package} 심볼={symbol} → 중단 기대 {expectStop}, 실제 {stop}\n" +
                $"  게이트가 말한 이유: {reason}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason),
                $"[{what}] 판정은 냈는데 이유가 비었습니다 — 로그만 보고는 왜 멈췄는지(또는 왜 안 멈췄는지) 알 수 없습니다.");
        }

        // ==================== 계측기 교정 ====================

        [Test]
        public void 타입_프로브가_알려진_값으로_교정된다()
        {
            MethodInfo m = PredefinedEditorAssemblyProbe.FindPublicStaticMethod(BuildScript, TypeProbeMethodName);
            Assert.IsNotNull(m, $"'{TypeProbeMethodName}'을 못 찾았습니다.");

            Assert.IsTrue((bool)m.Invoke(null, new object[] { AlwaysLoadedTypeName }),
                $"'{AlwaysLoadedTypeName}'을 못 찾습니다 — 패키지 탐지가 죽었습니다. " +
                "이 상태에서는 '패키지 없음'이 언제나 참이 되어 게이트가 영원히 휴면으로 판정합니다.");
            Assert.IsFalse((bool)m.Invoke(null, new object[] { NeverLoadedTypeName }),
                $"'{NeverLoadedTypeName}'이 찾아졌습니다 — 프로브가 아무거나 true를 냅니다.");
            Assert.IsFalse((bool)m.Invoke(null, new object[] { null }),
                "null에 대해 true를 냅니다.");
        }

        /// <summary>어댑터 경로·심볼 이름이 <b>실물과 같은가</b>. 상수만 바뀌고 실물이 안 따라오면
        /// 게이트는 언제나 "어댑터 없음"을 보고 조용히 통과한다.</summary>
        [Test]
        public void 게이트가_가리키는_어댑터_경로와_심볼이_실물과_같다()
        {
            string adapterPath = PredefinedEditorAssemblyProbe
                .FindPublicStaticConstant(BuildScript, "SteamEntitlementAdapterAssetPath") as string;
            Assert.IsFalse(string.IsNullOrEmpty(adapterPath), "어댑터 경로 상수가 없습니다.");

            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), adapterPath);
            Assert.IsTrue(File.Exists(full),
                $"게이트가 보는 어댑터 경로에 파일이 없습니다: {adapterPath}\n" +
                "파일을 옮겼다면 이 상수도 함께 옮기십시오 — 안 그러면 게이트는 영원히 '어댑터 없음'을 " +
                "보고 아무것도 막지 않습니다(조용히 초록이 되는 방향입니다).");

            string symbol = PredefinedEditorAssemblyProbe
                .FindPublicStaticConstant(BuildScript, "SteamworksInstalledDefineSymbol") as string;
            Assert.IsFalse(string.IsNullOrEmpty(symbol), "심볼 이름 상수가 없습니다.");
            StringAssert.Contains(symbol, File.ReadAllText(full),
                $"어댑터 소스에 '{symbol}'가 없습니다 — 게이트가 지키는 심볼과 어댑터가 켜지는 심볼이 " +
                "다른 이름이라는 뜻입니다.");
        }

        // ==================== 두 빌드 경로가 실제로 부르는가 ====================

        /// <summary>
        /// 리플렉션은 "메서드가 있다"까지만 말한다. <b>빌드 경로가 그것을 부르는지</b>는
        /// 소스를 봐야 안다. <c>CrossCompileGuardTests</c>가 셸 스크립트에 쓰는 것과 같은 방식이고,
        /// 니들이 죽으면 <b>시끄럽게</b> 빨개지는 존재 단언만 쓴다.
        /// </summary>
        [Test]
        public void macOS와_Windows_빌드_경로가_모두_게이트를_먼저_부른다()
        {
            string src = File.ReadAllText(BuildScriptSourcePath).Replace("\r\n", "\n");

            // 계측기 교정 — 이 텍스트 검색이 살아 있는가(있는 것/없는 것 양방향).
            StringAssert.Contains(GateMethodName, src, "소스에서 게이트 이름조차 못 찾습니다 — 파일을 잘못 읽고 있습니다.");
            StringAssert.DoesNotContain(AbsentMemberName, src, "없어야 할 이름이 소스에 있습니다 — 교정 실패.");

            foreach (string entry in new[] { "PerformBuild", "PerformBuildWindows" })
            {
                int at = src.IndexOf("public static void " + entry + "()", StringComparison.Ordinal);
                Assert.Greater(at, -1, $"'{entry}' 진입점을 소스에서 못 찾았습니다 — 이 단언의 전제가 깨졌습니다.");

                int callAt = src.IndexOf(VerifyMethodName, at, StringComparison.Ordinal);
                int buildAt = src.IndexOf("BuildPipeline.BuildPlayer", at, StringComparison.Ordinal);
                Assert.Greater(callAt, -1, $"'{entry}'가 {VerifyMethodName}을 부르지 않습니다 — " +
                    "그 플랫폼만 심볼 누락 빌드를 조용히 성공시킵니다(S-14는 Win/macOS 양쪽 발견입니다).");
                Assert.Greater(buildAt, -1, $"'{entry}' 뒤에서 실제 빌드 호출을 못 찾았습니다 — " +
                    "이 순서 단언의 전제가 깨졌습니다.");
                Assert.Less(callAt, buildAt,
                    $"'{entry}'에서 {VerifyMethodName} 호출이 실제 빌드보다 뒤에 있습니다 — " +
                    "산출물을 만든 뒤에 막아 봐야 이미 덮어쓴 다음입니다.");
            }
        }

        // ==================== 오늘의 트리 ====================

        /// <summary>★ 이 게이트가 <b>오늘 빌드를 막지 않는다</b>는 것을 못 박는다.
        /// 감사를 넣으면서 빌드를 세우면 그건 고친 게 아니라 부순 것이다.</summary>
        [Test]
        public void 오늘_이_트리에서는_게이트가_빌드를_막지_않는다()
        {
            MethodInfo m = PredefinedEditorAssemblyProbe.FindPublicStaticMethod(BuildScript, VerifyMethodName);
            Assert.IsNotNull(m, $"'{VerifyMethodName}'을 못 찾았습니다.");

            Assert.IsTrue((bool)m.Invoke(null, null),
                "지금 트리에서 스팀 배선 게이트가 빌드를 막습니다. 콘솔의 [BuildStandalone] 줄에 " +
                "어느 사실이 어긋났는지 적혀 있습니다 — 그것부터 읽으십시오.");
        }
    }
}
