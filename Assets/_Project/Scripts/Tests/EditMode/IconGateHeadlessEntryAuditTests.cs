using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ code-inspection 발견 <b>N6</b>(2026-09-05) — 같은 날 신설된 1,800줄짜리 앱 아이콘 게이트
    /// (<c>Assets/Editor/StickmanIconGates.cs</c>)를 <b>EditMode 러너가 구조적으로 부를 수 없었다</b>.
    ///
    /// ============================================================================
    /// 왜 못 부르는가 — 잊으면 또 같은 자리에 코드를 놓게 된다
    /// ============================================================================
    /// <c>Assets/Editor/</c>에는 <c>.asmdef</c>가 없다 ⇒ 그 폴더는 <b>사전정의 어셈블리</b>
    /// <c>Assembly-CSharp-Editor</c>로 컴파일된다. 사전정의 어셈블리는 asmdef들을 <b>참조하는 쪽</b>이고,
    /// asmdef는 그 반대로 참조할 수 없다. 그래서 <c>StickMate.Tests.EditMode</c>는
    /// <c>StickmanIconGates</c>를 <b>컴파일 시점에</b> 알 방법이 없다.
    /// <b>게이트가 1,800줄 있는데 아무도 안 부르는 상태</b>였고, 그 사실은 초록에서 보이지 않는다.
    ///
    /// <para><b>처방</b>: 파일을 옮기는 대신(그러면 런타임 어셈블리가 에디터 전용 코드를 물게 된다)
    /// <c>-executeMethod</c>로 부를 수 있는 <b>헤드리스 진입점</b>을 낸다. 이 감사는 그 문이
    /// 열려 있는지, 그리고 <b>왜 그 문이 필요한지</b>(구조적 사실)를 함께 못 박는다.</para>
    ///
    /// <para>★ 이 파일 자신도 리플렉션으로만 대상을 볼 수 있다 — 그래서 모든 이름 단언에
    /// 양방향 교정을 붙인다(<see cref="PredefinedEditorAssemblyProbe"/> 문단).</para>
    /// </summary>
    public sealed class IconGateHeadlessEntryAuditTests
    {
        private const string GatesTypeName = "StickMate.EditorTools.StickmanIconGates";
        private const string HeadlessMethodName = "RunGatesHeadless";
        private const string BatchWrapperName = "RunGatesHeadlessBatch";
        private const string PassCodeName = "HeadlessPassExitCode";
        private const string FailCodeName = "HeadlessFailExitCode";
        private const string ErrorCodeName = "HeadlessErrorExitCode";

        /// <summary>계측기 교정용 — 절대 존재하지 않아야 하는 이름.</summary>
        private const string AbsentMemberName = "이_이름의_멤버는_StickmanIconGates에_없다";

        private static string RepoRoot => Path.GetDirectoryName(Application.dataPath);
        private static string EditorFolder => Path.Combine(RepoRoot, "Assets", "Editor");
        private static string GatesSourcePath => Path.Combine(EditorFolder, "StickmanIconGates.cs");

        // ==================== N6의 원인 (구조적 사실) ====================

        /// <summary>
        /// 아이콘 게이트가 <b>사전정의 어셈블리</b>에 있다는 사실 자체를 잠근다.
        /// 문자열 니들이 아니라 <b>파일 시스템 구조</b>로 재므로 이름이 바뀌어도 썩지 않는다.
        /// <para>양성 대조로 <b>asmdef가 있는 폴더</b>(이 테스트 폴더)를 같은 프로브로 함께 잰다 —
        /// "0개"가 "폴더를 잘못 봤다"와 구분되게.</para>
        /// </summary>
        [Test]
        public void 아이콘_게이트는_asmdef가_없는_사전정의_에디터_어셈블리에_있다()
        {
            Assert.IsTrue(File.Exists(GatesSourcePath),
                $"아이콘 게이트 소스가 없습니다: {GatesSourcePath}");

            // 음성 — Assets/Editor 아래에는 asmdef가 0개다(= 사전정의 어셈블리).
            string[] editorAsmdefs = Directory.GetFiles(EditorFolder, "*.asmdef", SearchOption.AllDirectories);
            Assert.AreEqual(0, editorAsmdefs.Length,
                "Assets/Editor/ 에 asmdef가 생겼습니다 — 그렇다면 이 감사의 전제(사전정의 어셈블리라 " +
                "테스트가 참조할 수 없다)가 바뀐 것입니다. 참조가 가능해졌다면 헤드리스 우회로 대신 " +
                "게이트를 직접 부르는 테스트로 갈아타십시오.\n  발견: " + string.Join(", ", editorAsmdefs));

            // 양성 대조 — 같은 프로브가 asmdef가 있는 폴더에서는 실제로 찾아낸다.
            string testSourceFolder = Path.Combine(RepoRoot, "Assets", "_Project", "Scripts", "Tests", "EditMode");
            Assert.Greater(
                Directory.GetFiles(testSourceFolder, "*.asmdef", SearchOption.TopDirectoryOnly).Length, 0,
                "asmdef를 찾는 프로브가 죽었습니다 — asmdef가 확실히 있는 폴더에서도 0개를 셉니다. " +
                "그렇다면 위의 '0개'는 아무것도 증명하지 않습니다.");
        }

        // ==================== 처방이 실재하는가 ====================

        [Test]
        public void 헤드리스_진입점이_실재하고_정수_코드를_돌려준다()
        {
            Type t = PredefinedEditorAssemblyProbe.RequireType(GatesTypeName);

            MethodInfo headless = PredefinedEditorAssemblyProbe.FindPublicStaticMethod(t, HeadlessMethodName);
            Assert.IsNotNull(headless,
                $"'{HeadlessMethodName}'이 없습니다 — 게이트를 배치모드에서 부를 문이 다시 닫혔습니다(N6).");
            Assert.AreEqual(typeof(int), headless.ReturnType,
                "헤드리스 진입점이 정수 코드를 돌려주지 않습니다 — 통과/실패/측정불가를 구분할 수 없습니다.");
            Assert.AreEqual(0, headless.GetParameters().Length,
                "-executeMethod는 인자를 넘길 수 없습니다 — 인자 없는 진입점이어야 합니다.");

            MethodInfo wrapper = PredefinedEditorAssemblyProbe.FindPublicStaticMethod(t, BatchWrapperName);
            Assert.IsNotNull(wrapper,
                $"'{BatchWrapperName}'이 없습니다 — Unity는 진입 메서드의 반환값을 버리므로 " +
                "종료코드로 옮겨 주는 래퍼가 없으면 배치 실행 결과를 밖에서 알 수 없습니다.");
            Assert.AreEqual(0, wrapper.GetParameters().Length, "-executeMethod 대상은 인자가 없어야 합니다.");

            // 계측기 교정 — 없는 이름은 정말 못 찾아야 한다.
            Assert.IsNull(PredefinedEditorAssemblyProbe.FindPublicStaticMethod(t, AbsentMemberName),
                "존재하지 않는 이름이 찾아졌습니다 — 이 파일의 '있다/없다' 판정을 전부 폐기하십시오.");
        }

        /// <summary>종료코드가 <b>서로 구분되는가</b>. 셋이 같은 값이면 배치 호출자는
        /// "통과"와 "재지도 못했다"를 구분할 수 없다 — 이 저장소가 반복해서 당한 형태 그대로다.</summary>
        [Test]
        public void 통과_실패_측정불가가_서로_다른_종료코드다()
        {
            Type t = PredefinedEditorAssemblyProbe.RequireType(GatesTypeName);

            object pass = PredefinedEditorAssemblyProbe.FindPublicStaticConstant(t, PassCodeName);
            object fail = PredefinedEditorAssemblyProbe.FindPublicStaticConstant(t, FailCodeName);
            object error = PredefinedEditorAssemblyProbe.FindPublicStaticConstant(t, ErrorCodeName);

            Assert.IsNotNull(pass, $"'{PassCodeName}' 상수가 없습니다.");
            Assert.IsNotNull(fail, $"'{FailCodeName}' 상수가 없습니다.");
            Assert.IsNotNull(error, $"'{ErrorCodeName}' 상수가 없습니다.");
            Assert.IsNull(PredefinedEditorAssemblyProbe.FindPublicStaticConstant(t, AbsentMemberName),
                "없는 상수가 찾아졌습니다 — 계측기 교정 실패.");

            Assert.AreEqual(0, (int)pass, "통과 코드는 0이어야 합니다(셸이 그렇게 읽습니다).");
            Assert.AreNotEqual((int)pass, (int)fail, "통과와 실패가 같은 코드입니다.");
            Assert.AreNotEqual((int)fail, (int)error, "실패와 측정불가가 같은 코드입니다 — " +
                "'게이트가 떨어졌다'와 '게이트를 재지도 못했다'는 다른 사건입니다.");
        }

        /// <summary>래퍼가 <b>실제로</b> 종료코드를 옮기는가. 리플렉션은 "메서드가 있다"까지만 말한다.
        /// (진입점을 여기서 <b>실행</b>할 수는 없다 — 실제 GPU 렌더와 파일 쓰기가 일어난다.)</summary>
        [Test]
        public void 배치_래퍼가_진입점의_반환값을_종료코드로_옮긴다()
        {
            string src = File.ReadAllText(GatesSourcePath).Replace("\r\n", "\n");

            // 계측기 교정(양방향).
            StringAssert.Contains(HeadlessMethodName, src, "소스에서 진입점 이름조차 못 찾습니다 — 파일을 잘못 읽고 있습니다.");
            StringAssert.DoesNotContain(AbsentMemberName, src, "없어야 할 이름이 소스에 있습니다 — 교정 실패.");

            int at = src.IndexOf(BatchWrapperName + "()", StringComparison.Ordinal);
            Assert.Greater(at, -1, $"'{BatchWrapperName}' 정의를 소스에서 못 찾았습니다.");

            string tail = src.Substring(at);
            int exitAt = tail.IndexOf("EditorApplication.Exit", StringComparison.Ordinal);
            int callAt = tail.IndexOf(HeadlessMethodName + "()", StringComparison.Ordinal);
            Assert.Greater(exitAt, -1,
                $"'{BatchWrapperName}'가 종료코드를 설정하지 않습니다 — 배치 실행이 무조건 0으로 끝나 " +
                "게이트 실패가 성공과 똑같이 생깁니다(이 저장소 거짓 통과 13번째 형태).");
            Assert.Greater(callAt, exitAt,
                $"'{BatchWrapperName}'가 진입점을 부르지 않고 종료코드만 냅니다.");
        }
    }
}
