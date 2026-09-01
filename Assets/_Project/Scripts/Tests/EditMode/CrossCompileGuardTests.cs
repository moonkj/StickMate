using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 — <b>크로스 컴파일 검사 도구 자체</b>를 잠근다(<c>Tools/CrossCompile/xcheck.sh</c>).
    ///
    /// ============================================================================
    /// 왜 스크립트를 테스트가 지키는가
    /// ============================================================================
    /// CLAUDE.md는 "Windows 쪽을 건드렸으면 Roslyn 크로스 컴파일로 0에러를 확인한다"를 절차로 못박고
    /// 있다. 그런데 이 저장소는 그 검사가 <b>아무것도 컴파일하지 않고 "에러 0"을 보고한</b> 사고를
    /// <b>네 번</b> 겪었다:
    ///  <list type="number">
    ///   <item>깨진 csc 래퍼(실행 실패를 에러 0으로 오인)</item>
    ///   <item>rsp의 낡은 소스 목록(신규 파일 누락)</item>
    ///   <item>rsp에 이미 박힌 플랫폼 정의(요청 타깃이 실제로는 비활성 / 양쪽 동시 활성)</item>
    ///   <item>에디터 rsp만 사용(<c>#if UNITY_EDITOR</c>의 <b>#else 가지</b>가 한 줄도 컴파일 안 됨)</item>
    ///  </list>
    /// 네 번 다 "사람이 스크립트를 잘 읽으면 된다"로는 못 막혔다. 검사 도구가 조용히 무력해지는 것은
    /// <b>검사가 없는 것보다 나쁘다</b> — 없으면 확인하지만, 거짓 초록은 확인했다고 믿게 만든다.
    ///
    /// <para>여기서 스크립트 <b>동작</b>을 실행해 검증할 수는 없다(EditMode 테스트가 Roslyn을 몇 분씩
    /// 돌릴 수는 없다). 그래서 <b>방어 장치가 소스에 살아 있는지</b>만 본다 — 누군가 "간단히 정리"하다
    /// 가드를 들어내면 여기서 걸린다. 실제 실효성은 스크립트의 <c>--selftest</c>가 증명한다
    /// (반대 타깃 카나리아를 넣어 컴파일이 반드시 실패하는지 확인하는 모드).</para>
    /// </summary>
    public sealed class CrossCompileGuardTests
    {
        private static string RepoRoot => Path.GetDirectoryName(Application.dataPath);
        private static string ToolPath => Path.Combine(RepoRoot, "Tools", "CrossCompile", "xcheck.sh");

        /// <summary>
        /// 주석(<c>#</c>)을 걷어낸 <b>실행되는 줄</b>만. 이 문서화 습관이 강한 저장소에서는 금지 패턴을
        /// 원문 그대로 훑으면 <b>"왜 금지인지 설명하는 주석"이 스스로 걸린다</b> — 실제로 걸렸다
        /// (2026-09-01, 아래 <see cref="산출물과_소스_개수를_스스로_확인한다"/>가 자기 스크립트의
        /// 설명 문단에 반응했다). 같은 사고를 <c>EquipmentDebugUnlockReleaseGateTests</c>에서도 겪었다.
        /// 감사가 잡아야 하는 것은 <b>실제 호출</b>이지 서술이 아니다.
        /// </summary>
        private static string StripComments(string src)
            => Regex.Replace(src, @"^\s*#.*$", string.Empty, RegexOptions.Multiline);

        private static string ReadTool()
        {
            Assert.IsTrue(File.Exists(ToolPath),
                $"크로스 컴파일 검사 도구가 없습니다: {ToolPath}\n" +
                "CLAUDE.md가 요구하는 Windows 확인 절차가 통째로 사라졌다는 뜻입니다. " +
                "옮겼다면 이 테스트도 함께 옮기십시오(그냥 지우지 마십시오).");
            return File.ReadAllText(ToolPath).Replace("\r\n", "\n");
        }

        /// <summary>함정 3 — 플랫폼 계열 정의를 <b>전부</b> 제거한 뒤 재주입하는가.
        /// 하나라도 빠지면 요청한 타깃이 실제로는 비활성이거나 양쪽이 동시에 켜진다.</summary>
        [Test]
        public void 플랫폼_정의를_여섯_계열_모두_제거한다()
        {
            string src = ReadTool();
            foreach (string fam in new[] { "UNITY_STANDALONE", "PLATFORM_STANDALONE", "UNITY_EDITOR" })
            {
                StringAssert.Contains(fam, src,
                    $"{fam}_WIN/OSX 를 제거하는 처리가 없습니다 — rsp에 원래 박혀 있던 정의가 살아남아 " +
                    "요청한 플랫폼으로 컴파일되지 않습니다(거짓 초록 3형).");
            }
        }

        /// <summary>함정 3의 실제 방어 — 카나리아가 컴파일러에게 직접 묻는가.</summary>
        [Test]
        public void 카나리아가_정의_불일치를_컴파일_에러로_만든다()
        {
            string src = ReadTool();
            StringAssert.Contains("#error", src,
                "카나리아의 #error 지시문이 없습니다 — 정의가 틀려도 컴파일이 통과합니다.");
            StringAssert.Contains("XCHECK_CANARY", src,
                "카나리아 표식이 없습니다 — 자기검사가 무엇을 찾아야 할지 알 수 없습니다.");
            StringAssert.Contains("xcheck_canary.cs", src,
                "카나리아가 소스 목록에 실제로 들어갔는지 확인하는 처리가 없습니다 — " +
                "카나리아가 컴파일되지 않으면 그 자체가 새로운 거짓 초록입니다.");
        }

        /// <summary>카나리아가 <b>실제로 무는지</b> 확인하는 자기검사 모드가 살아 있는가.</summary>
        [Test]
        public void 자기검사_모드가_있다()
        {
            StringAssert.Contains("--selftest", ReadTool(),
                "자기검사 모드가 없습니다 — 카나리아가 침묵해도 아무도 알 수 없습니다.");
        }

        /// <summary>★ 함정 4 — 출시 빌드 경로(<c>UNITY_EDITOR</c> 없음)를 실제로 컴파일하는가.
        /// 이게 빠지면 <c>EquipmentDebugUnlock</c>의 릴리스 게이트 같은 <c>#else</c> 가지가
        /// <b>사용자 빌드에서만</b> 터진다.</summary>
        [Test]
        public void 출시_빌드_경로도_컴파일한다()
        {
            string src = ReadTool();
            StringAssert.Contains("1900b0aP.dag", src,
                "플레이어(출시) rsp를 쓰지 않습니다 — UNITY_EDITOR가 항상 켜진 채로만 컴파일되어 " +
                "#else(릴리스) 가지가 한 줄도 검증되지 않습니다(거짓 초록 4형).");
            StringAssert.Contains("1900b0aE.dag", src,
                "에디터 rsp를 쓰지 않습니다 — 테스트 어셈블리를 컴파일할 수 없습니다.");
        }

        /// <summary>
        /// ★ 함정 5 — <b>Editor 어셈블리</b>(<c>Assembly-CSharp-Editor</c> = asmdef 없는
        /// <c>Assets/Editor/</c>)를 컴파일하는가.
        ///
        /// <para>2026-09-01 실측 사고: 이 도구가 win/osx 모두 "전부 통과"를 냈는데 <b>같은 시각</b>
        /// Unity 배치모드는 <c>Aborting batchmode due to failure: Scripts have compiler errors</c>로
        /// 거부했다. 원인은 <c>Assets/Editor/SceneBootstrapper.cs</c>(프리팹/씬을 굽는 15만 자, 매
        /// 라운드 편집된다)가 asmdef 기반 목록에 잡히지 않아 검사에서 통째로 빠져 있던 것이다.
        /// <b>테스트를 한 줄도 못 돌리는 상태를 "초록"이라고 말한 것</b>이라, 앞의 네 함정 중 어느
        /// 것보다도 직접적인 거짓 초록이었다.</para>
        /// </summary>
        [Test]
        public void Editor_어셈블리도_컴파일한다()
        {
            string src = ReadTool();
            StringAssert.Contains("Assembly-CSharp-Editor", src,
                "Editor 어셈블리를 컴파일하지 않습니다 — Assets/Editor/ 가 깨져도 이 도구는 초록입니다. " +
                "그 상태에서 Unity 배치모드는 테스트를 한 건도 돌리지 못하고 거부합니다(거짓 초록 5형).");
            StringAssert.Contains("Assets/Editor", src,
                "Assets/Editor/ 의 소스를 모으는 처리가 없습니다.");
        }

        /// <summary>함정 1·2 — 컴파일러가 실제로 돌았는지, 트리를 제대로 봤는지 스스로 확인하는가.</summary>
        [Test]
        public void 산출물과_소스_개수를_스스로_확인한다()
        {
            string src = ReadTool();
            StringAssert.Contains("assert_artifact", src,
                "산출 DLL이 실제로 생겼는지 확인하지 않습니다 — 컴파일러가 아예 실행되지 않아도 " +
                "'에러 0'이 나옵니다(거짓 초록 1형).");
            StringAssert.Contains("MIN_RUNTIME_SOURCES", src,
                "소스 최소 개수 확인이 없습니다 — 트리를 잘못 보고도 초록이 됩니다(거짓 초록 2형).");
            StringAssert.Contains("DotNetSdkRoslyn", src,
                "Unity 동봉 Roslyn을 쓰지 않습니다 — MonoBleedingEdge/bin/csc는 깨진 래퍼입니다.");
            StringAssert.DoesNotContain("MonoBleedingEdge/bin/csc", StripComments(src),
                "깨진 csc 래퍼를 다시 쓰고 있습니다(거짓 초록 1형의 원인 그 자체입니다). " +
                "※ 주석은 걷어내고 봅니다 — 그 래퍼가 왜 금지인지 적어 둔 설명까지 걸리면 " +
                "기록을 남길수록 빨간불이 켜지는 이상한 규칙이 됩니다.");
        }
    }
}
