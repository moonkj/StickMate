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
    /// ★★★ 2026-09-28 사용자 결정(DLC 폐지) — <b>게이트의 뜻이 반대가 됐고 이 감사도 함께 뒤집혔다</b>
    /// ============================================================================
    /// <para>옛 판정: 「스팀 배선이 <b>불완전</b>하면 중단」(3중단 조건, 8조합 중 4건 중단).
    /// 새 판정: <b>「스팀 배선이 조금이라도 감지되면 중단」</b>(1중단 조건, 8조합 중 <b>7건</b> 중단).</para>
    ///
    /// <para><b>무엇이 삭제됐나</b>: 어댑터 <c>Assets/_Project/Scripts/Store/SteamPackEntitlementSource.cs</c>와
    /// 그 라인 단위 재검증 감사 <c>SteamEntitlementAdapterAuditTests</c>가 같은 커밋에서 사라졌다.
    /// 승인 근거였던 「6팩을 팔 수 없다 = 출시 자체가 없다」가 DLC 폐지로 소멸했다.</para>
    ///
    /// <para>★ <b>리더 인계안(4조합)에서 벗어난 지점을 먼저 적는다</b>: 인계안은 진리표를
    /// 「심볼 × 파일」 <b>4조합</b>으로 줄이라고 했다. 그런데 새 게이트는 <b>세 사실 모두가 독립된 중단 축</b>이다 —
    /// 4조합으로 줄이면 <c>packagePresent</c>(Steamworks 패키지가 로드됨)라는 <b>살아 있는 중단 축이
    /// 한 번도 안 먹여진다</b>. 그래서 <b>8조합 전량을 유지</b>했다(4조합의 상위집합이고, 줄이면
    /// 「조용히 안 재는 축」이 하나 생긴다). 이름의 숫자는 8로 유지하고 방향만 뒤집었다.</para>
    ///
    /// ============================================================================
    /// 이 감사가 겨누는 실패 형태
    /// ============================================================================
    /// ~~심볼이 없으면 스팀 어댑터의 <c>Query</c>는 언제나 <c>Unknown</c>이다. 즉 <b>산 사람과 안 산
    /// 사람이 산출물에서 구분되지 않는다</b> — 실패한 빌드와 성공한 빌드가 똑같이 생겼다.
    /// 종료코드도, 로그도 그 차이를 말해 주지 않는다. 그래서 <b>빌드를 만들기 전에</b> 사실을 대조한다.~~
    /// <para>★ 2026-09-28 이후 겨누는 형태: <b>스팀 배선이 조용히 되살아나 출하되는 것.</b>
    /// 폐지는 「지금 없다」가 아니라 「다시 생기지 않는다」여야 하고, 그것을 재는 자리가 여기다.
    /// 파일 하나가 되살아난 빌드와 되살아나지 않은 빌드는 <b>산출물만 봐서는 구분되지 않는다</b> —
    /// S-14와 똑같은 형태이고 방향만 반대다.</para>
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
        /// <para>~~★ 「어댑터가 있는데 심볼이 없으면 무조건 실패」로 만들지 <b>않은</b> 이유가 여기서
        /// 눈에 보인다 — 오늘 이 저장소의 상태가 정확히 (어댑터 O · 패키지 X · 심볼 X)이고,
        /// 그 규칙이었다면 <b>오늘 모든 빌드가 실패</b>한다. 패키지 없이 심볼만 켜면 어댑터의
        /// <c>using Steamworks;</c>가 풀리지 않아 컴파일이 깨지므로 <b>안전한 출구도 없다</b>.~~</para>
        ///
        /// <para>★★ <b>2026-09-28 — 기대 방향을 전부 뒤집었다.</b> 이제 통과하는 조합은 <b>하나뿐</b>이고
        /// 그것이 오늘 이 저장소의 상태다(어댑터 X · 패키지 X · 심볼 X). 위 취소선의 전제
        /// (「오늘 상태 = 어댑터 O」)는 어댑터 삭제로 사라졌으므로 「오늘 모든 빌드가 실패」 걱정도 함께 사라졌다 —
        /// 그 사실을 <see cref="오늘_이_트리에서는_게이트가_빌드를_막지_않는다"/>가 실제 디스크로 다시 잰다.</para>
        ///
        /// <para>★ <b>8조합을 유지한 이유</b>(리더 인계안의 4조합에서 벗어난 지점): 1중단 조건의 세 사실은
        /// <b>각각 독립된 중단 축</b>이다. 「심볼 × 파일」 4조합으로 줄이면 <c>packagePresent</c> 축이
        /// 한 번도 먹여지지 않아, 패키지만 들어온 경우(UPM 의존이 먼저 들어오는 실제 순서)를
        /// <b>아무도 재지 않는다</b>. 8은 4의 상위집합이라 잃는 것이 없다.</para>
        /// </summary>
        [Test]
        public void 게이트가_여덟_조합에서_스팀_배선을_전부_감지한다()
        {
            //        어댑터  패키지  심볼   중단?
            AssertVerdict(false, false, false, false, "★ 유일한 통과 조합 — 2026-09-28 폐지 후의 정상 상태");
            AssertVerdict(true, false, false, true, "어댑터 파일이 되살아났다");
            AssertVerdict(false, true, false, true, "Steamworks 패키지가 들어왔다(UPM 의존이 먼저 오는 실제 순서)");
            AssertVerdict(true, true, false, true, "어댑터 + 패키지");
            AssertVerdict(false, false, true, true, "정의 심볼만 켜졌다");
            AssertVerdict(true, false, true, true, "어댑터 + 심볼");
            AssertVerdict(false, true, true, true, "패키지 + 심볼");
            AssertVerdict(true, true, true, true, "완전 배선 — 폐지 전이라면 유일한 정상이었던 조합");
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

        /// <summary>
        /// ~~어댑터 경로·심볼 이름이 <b>실물과 같은가</b>. 상수만 바뀌고 실물이 안 따라오면
        /// 게이트는 언제나 "어댑터 없음"을 보고 조용히 통과한다.~~
        ///
        /// <para>★★ <b>2026-09-28 — 방향이 반대가 됐다.</b> 어댑터는 삭제됐으므로 이제 단언은
        /// <b>「그 경로에 파일이 없다」</b>다. 옛 형태를 그대로 두면 어댑터를 지우는 순간 이 단언이
        /// 빨개진다(인계안이 「먼저 확인하라」고 지목한 자리가 여기다).</para>
        ///
        /// <para>★ <b>부재 단언은 조용히 초록이 되는 방향</b>이라(경로 상수에 오타가 나도 「없음」이 된다)
        /// 같은 테스트 안에 <b>경로 해석기 양성 대조</b>를 둔다 — 반드시 존재하는 파일을 같은 방식으로
        /// 합성해 <c>File.Exists</c>가 참을 내는지 확인한다. 그게 없으면 이 「없다」는
        /// 「없다」가 아니라 「경로를 잘못 만들고 있다」일 수 있다.</para>
        /// </summary>
        [Test]
        public void 게이트가_가리키는_어댑터_경로에_실물이_없다()
        {
            string adapterPath = PredefinedEditorAssemblyProbe
                .FindPublicStaticConstant(BuildScript, "SteamEntitlementAdapterAssetPath") as string;
            Assert.IsFalse(string.IsNullOrEmpty(adapterPath), "어댑터 경로 상수가 없습니다.");

            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            // ---- 양성 대조: 같은 방식으로 만든 경로가 실재하는 파일을 실제로 찾는가 ----
            //   (경로 조립은 이 파일이 이미 쓰는 BuildScriptSourcePath 한 곳을 재사용한다 — 두 벌이면 갈라진다)
            string knownPresent = BuildScriptSourcePath;
            Assert.IsTrue(File.Exists(knownPresent),
                "경로 해석기 양성 대조 실패 — 반드시 존재하는 파일(Assets/Editor/BuildStandalone.cs)을 " +
                "찾지 못했습니다. 이 상태에서는 아래 '없다'가 '없다'가 아니라 '경로 계산이 틀렸다'입니다.");

            // ---- 음성 대조: 절대 없는 이름은 없다고 나와야 한다 ----
            string knownAbsent = Path.Combine(projectRoot, "Assets", "Editor", "이_파일은_존재하지_않는다.cs");
            Assert.IsFalse(File.Exists(knownAbsent),
                "경로 해석기 음성 대조 실패 — 없는 파일을 있다고 합니다.");

            // ---- 본 단언 ----
            string full = Path.Combine(projectRoot, adapterPath);
            Assert.IsFalse(File.Exists(full),
                $"스팀 엔타이틀먼트 어댑터가 되살아났습니다: {adapterPath}\n" +
                "2026-09-28 사용자 결정(DLC 폐지)으로 이 파일은 삭제됐고, 같은 커밋에서 " +
                "라인 단위 재검증 감사(SteamEntitlementAdapterAuditTests)도 함께 삭제됐습니다 — " +
                "즉 이 파일이 다시 생기면 그 안의 심볼을 아무도 잠그지 않습니다.\n" +
                "다시 열어야 한다면 순서는 ① 사용자 결정 → ② 리더 재결재 → ③ 예외 문서 재개 → " +
                "④ 승인 표·전송 명부·재검증 감사 복원입니다.");

            // 심볼 이름 상수는 계기로 남아 있어야 한다(게이트가 그 이름을 재므로).
            string symbol = PredefinedEditorAssemblyProbe
                .FindPublicStaticConstant(BuildScript, "SteamworksInstalledDefineSymbol") as string;
            Assert.IsFalse(string.IsNullOrEmpty(symbol), "심볼 이름 상수가 없습니다.");
            StringAssert.Contains(symbol, File.ReadAllText(knownPresent),
                $"빌드 스크립트 소스에 '{symbol}' 문자열이 없습니다 — 게이트가 재는 심볼 이름과 " +
                "소스가 갈라졌다는 뜻이고, 그러면 심볼이 켜져도 게이트가 못 봅니다.");
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
        /// 감사를 넣으면서 빌드를 세우면 그건 고친 게 아니라 부순 것이다.
        /// <para>★★ 2026-09-28 — <b>이 테스트가 이번 반전의 안전핀이다.</b> 게이트의 뜻을 뒤집었으니
        /// 「오늘 통과한다」가 여전히 참인지가 가장 먼저 의심할 곳이다. 어댑터·패키지·심볼 셋 다 없어야
        /// 통과하고, 그 셋이 실제로 없다는 사실을 <b>디스크와 도메인에서</b> 다시 잰다(진리표와 다른 경로다).</para></summary>
        [Test]
        public void 오늘_이_트리에서는_게이트가_빌드를_막지_않는다()
        {
            MethodInfo m = PredefinedEditorAssemblyProbe.FindPublicStaticMethod(BuildScript, VerifyMethodName);
            Assert.IsNotNull(m, $"'{VerifyMethodName}'을 못 찾았습니다.");

            Assert.IsTrue((bool)m.Invoke(null, null),
                "지금 트리에서 스팀 배선 게이트가 빌드를 막습니다. 콘솔의 [BuildStandalone] 줄에 " +
                "어느 사실이 걸렸는지 적혀 있습니다 — 그것부터 읽으십시오. " +
                "2026-09-28 폐지 후에는 어댑터·패키지·심볼 셋 다 '없음'이어야 통과합니다.");
        }
    }
}
