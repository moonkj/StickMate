using System;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-28 — <b>기동 시 창 모드 분기</b>(<see cref="StartupWindowModePolicy"/>)를 잠근다.
    /// 사용자 실기 신고 「stickmate 흰화면이야 상단에 써있음」의 조치가 <b>조용히 무효가 되는 경로</b>를 막는 것이 목적이다.
    ///
    /// ============================================================================
    /// ★ 부재 단언을 쓰지 않는다 — 전부 <b>관계 단언</b>이다 (리더 지시 2026-09-28)
    /// ============================================================================
    /// 「Windows에서 <c>forceWindowed</c>를 쓰지 않는다」 같은 부재 단언은 <b>기능이 통째로 사라져도 참</b>이라
    /// 썩으면 조용히 초록이 된다(CLAUDE.md). 그래서 이 파일의 단언은 전부 <b>두 값이 서로를 가리키는 관계</b>다:
    /// <list type="number">
    ///   <item><b>두 판정의 배타성</b> — 조기 해제와 지연 해제는 모든 플랫폼에서 정확히 하나만 참이다.
    ///     한쪽이 뒤집히면 다른 쪽도 같이 뒤집혀야 하고, 그러지 않으면 <b>탈출구 0</b>이 된다.</item>
    ///   <item><b>씬 에셋 ↔ 정책</b> — 씬에 구워진 값은 <b>macOS의 답과 같아야</b> 하고
    ///     <b>Windows의 답과 달라야</b> 한다. 같아지는 순간 Windows 런타임 덮어쓰기는 죽은 코드가 되고,
    ///     달라지는 순간 macOS 동작이 조용히 바뀐다. 두 방향 다 여기서 빨개진다.</item>
    ///   <item><b>호출 순서</b> — 판정 호출이 <c>SetActive</c>보다 <b>앞</b>이어야 한다.
    ///     활성화가 <c>Awake()</c>를 동기로 실행하므로, 대입이 뒤로 내려가면 <b>컴파일도 테스트도 멀쩡한데
    ///     효과만 0</b>이 된다 — 이 저장소가 반복해 당한 "죽은 조치" 형태다.</item>
    /// </list>
    ///
    /// <para><b>니들은 베끼지 않는다</b>: 우리 식별자는 전부 <c>nameof</c>로 유도한다. 이름이 바뀌면 소스 스캔이
    /// 조용히 0을 내는 대신 <b>컴파일 단계에서</b> 빨개진다. 예외 둘은 <b>외부 스키마</b>라 유도할 곳이 없다 —
    /// 패키지 공개 필드 이름(<c>forceWindowed</c>)과 Unity 직렬화 키(<c>propertyPath:</c>)이고,
    /// 둘 다 <b>존재 단언</b>으로만 쓰므로 스키마가 바뀌면 조용히 통과하지 않고 실패한다
    /// (테스트 어셈블리는 패키지 어셈블리를 참조하지 않아 리플렉션으로 뽑을 수 없다 — asmdef 참조 목록).</para>
    ///
    /// <para><b>소스를 읽는 이유</b>: 두 플랫폼 파일은 각각 <c>#if UNITY_STANDALONE_WIN</c> /
    /// <c>_OSX</c> 안이라 <b>반대 타깃에서는 타입이 존재하지 않는다</b>. 리플렉션으로는 절반을 구조적으로 볼 수
    /// 없으므로 <b>소스 파일</b>을 읽는다(CLAUDE.md 「활성 빌드 타깃 규칙」).</para>
    /// </summary>
    public sealed class StartupWindowModeSplitTests
    {
        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");
        private static string PlatformRoot => Path.Combine(ScriptsRoot, "Platform");
        private static string WinWindowServicePath => Path.Combine(PlatformRoot, "Windows", "Win32WindowService.cs");
        private static string MacWindowServicePath => Path.Combine(PlatformRoot, "MacOS", "MacWindowService.cs");
        private static string WinEnforcerPath => Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs");
        private static string MacEnforcerPath => Path.Combine(PlatformRoot, "MacOS", "MacOverlayStateEnforcer.cs");
        private static string ScenePath => Path.Combine(Application.dataPath, "_Project", "Scenes", "Main.unity");

        /// <summary>우리 판정 이름 — <c>nameof</c>로 유도한다(베끼지 않는다).</summary>
        private static string EarlyDecisionName => nameof(StartupWindowModePolicy.ShouldForceWindowedAtStartup);

        /// <summary>우리 탈출구 판정 이름 — 같은 이유로 <c>nameof</c>.</summary>
        private static string LateDecisionName => nameof(StartupWindowModePolicy.ShouldReleaseFullscreenAfterAttachFailure);

        /// <summary>패키지 공개 필드 이름. <b>외부 스키마</b>라 유도할 곳이 없다 — 존재 단언으로만 쓴다.</summary>
        private const string PackageForceWindowedField = "forceWindowed";

        /// <summary>Unity 직렬화 키. 외부 스키마 — 존재 단언으로만 쓴다.</summary>
        private const string ScenePropertyPathKey = "propertyPath: ";

        /// <summary>실제 활성화 호출. 같은 파일의 로그 <b>문자열 안</b>에도 <c>SetActive(true)</c>가 있으므로
        /// <b>수신자까지 붙여</b> 겨눈다(그 문자열을 겨누면 순서 판정이 엉뚱한 자리를 본다).</summary>
        private const string ActivationCall = "controller.gameObject.SetActive(true)";

        /// <summary>계측기 교정용 — <b>어느 소스에도 없어야</b> 한다. 걸리면 이 파일의 판정 전부가 무효다.</summary>
        private const string AbsentNeedle = "이문자열은어느소스에도없다_계기교정용_StartupWindowModeSplitTests";

        // ====================================================================
        // (1) 배타성 — 조기 해제와 지연 해제는 정확히 하나만 참이다
        // ====================================================================

        [Test]
        public void 조기해제와_지연해제는_모든_플랫폼에서_정확히_하나만_참이다()
        {
            Array values = Enum.GetValues(typeof(OverlayHostPlatform));

            // 계기 교정 — 열거가 비었거나 우리가 아는 두 플랫폼이 사라졌으면 아래 foreach는 공허하게 통과한다.
            Assert.GreaterOrEqual(values.Length, 3,
                $"{nameof(OverlayHostPlatform)} 값이 {values.Length}개뿐입니다 — 이 검사가 공허해집니다. " +
                "열거가 줄었다면 이 테스트의 기대값도 함께 갱신하세요.");
            Assert.IsTrue(Enum.IsDefined(typeof(OverlayHostPlatform), OverlayHostPlatform.Windows));
            Assert.IsTrue(Enum.IsDefined(typeof(OverlayHostPlatform), OverlayHostPlatform.MacOS));

            foreach (OverlayHostPlatform platform in values)
            {
                bool early = StartupWindowModePolicy.ShouldForceWindowedAtStartup(platform);
                bool late = StartupWindowModePolicy.ShouldReleaseFullscreenAfterAttachFailure(platform);

                Assert.AreNotEqual(early, late,
                    $"[{platform}] 조기 해제={early}, 지연 해제={late} — 둘은 배타적이어야 합니다. " +
                    "둘 다 참이면 창 모드를 두 번 내리고(깜박임 1회 추가), 둘 다 거짓이면 부착이 실패한 세션에서 " +
                    "제목표시줄도 닫기 버튼도 없는 전체화면 불투명 창이 남습니다(= 탈출구 0). " +
                    $"{nameof(StartupWindowModePolicy)} 클래스 문서의 불변식을 먼저 읽으세요.");
            }

            // 지금의 답(방향까지 못박는다 — 배타성만 보면 두 값이 함께 뒤집혀도 통과한다).
            Assert.IsFalse(StartupWindowModePolicy.ShouldForceWindowedAtStartup(OverlayHostPlatform.Windows),
                "Windows는 기동 시 전체화면 조기 해제를 끈 상태여야 합니다 — 그 조기 전환이 " +
                "「흰 화면 상단에 StickMate」 신고의 제목표시줄 구간을 만듭니다.");
            Assert.IsTrue(StartupWindowModePolicy.ShouldForceWindowedAtStartup(OverlayHostPlatform.MacOS),
                "macOS는 조기 해제를 켠 채로 둬야 합니다 — 전체화면 창이 메뉴바를 덮고 이 플랫폼에는 " +
                "트레이 탈출구가 없습니다.");
            Assert.IsTrue(StartupWindowModePolicy.ShouldForceWindowedAtStartup(OverlayHostPlatform.Unknown),
                "모르는 플랫폼(에디터/헤드리스/모바일)은 지금까지의 동작을 그대로 받아야 합니다.");
        }

        // ====================================================================
        // (2) 씬 에셋 ↔ 정책 — 씬 값이 두 답 사이에서 어느 쪽인지가 이 조치의 전제다
        // ====================================================================

        [Test]
        public void 씬에_구운_forceWindowed는_macOS의_답과_같고_Windows의_답과_다르다()
        {
            Assert.IsTrue(File.Exists(ScenePath), $"씬 에셋을 찾지 못했습니다: {ScenePath}");
            string scene = File.ReadAllText(ScenePath);

            // 양성 대조 — 엉뚱한 파일을 읽어도 아래 판정이 조용히 통과하지 않게.
            StringAssert.Contains(ScenePropertyPathKey + "_isTransparent", scene,
                "양성 대조 실패 — 읽은 파일에 UniWindowController의 직렬화 항목이 없습니다. " +
                "이 파일은 오버레이 컨트롤러가 들어 있는 씬이 아니므로 아래 판정 전부가 무효입니다.");
            StringAssert.Contains(ScenePropertyPathKey + PackageForceWindowedField, scene,
                "씬에 forceWindowed 직렬화 항목이 없습니다 — 패키지가 필드 이름을 바꿨거나 " +
                "SceneBootstrapper가 더 이상 그 값을 굽지 않습니다. 어느 쪽이든 이 조치의 전제가 무너졌습니다.");

            // 음성 대조 — 계측기가 아무 문자열이나 찾아 주는 상태가 아닌지.
            Assert.AreEqual(-1, scene.IndexOf(AbsentNeedle, StringComparison.Ordinal),
                "음성 대조 실패 — 존재하지 않아야 할 문자열이 걸렸습니다. 이 파일의 판정 전부가 무효입니다.");

            bool bakedForceWindowed = ReadSceneBoolProperty(scene, PackageForceWindowedField);

            Assert.AreEqual(StartupWindowModePolicy.ShouldForceWindowedAtStartup(OverlayHostPlatform.MacOS),
                bakedForceWindowed,
                "씬에 구워진 forceWindowed가 macOS 정책의 답과 다릅니다. 씬 값은 두 플랫폼이 공유하므로, " +
                "이 둘이 갈리면 macOS 기동 동작이 코드가 아니라 에셋에 의해 조용히 바뀝니다. " +
                "씬을 다시 구웠다면 정책을, 정책을 바꿨다면 씬을 함께 보세요(씬은 추적 파일입니다).");

            Assert.AreNotEqual(StartupWindowModePolicy.ShouldForceWindowedAtStartup(OverlayHostPlatform.Windows),
                bakedForceWindowed,
                "씬 값과 Windows 정책의 답이 같아졌습니다. 그러면 Win32WindowService의 런타임 덮어쓰기는 " +
                "아무것도 바꾸지 않는 죽은 코드가 됩니다 — 조치가 사라진 것인지 씬이 바뀐 것인지 확인하세요.");
        }

        // ====================================================================
        // (3)(4) 호출 순서 — 판정이 활성화보다 앞이어야 뜻이 있다
        // ====================================================================

        [Test]
        public void Windows는_활성화_전에_정책으로_forceWindowed를_덮는다()
        {
            AssertDecisionPrecedesActivation(WinWindowServicePath, "Win32WindowService");
        }

        [Test]
        public void macOS도_같은_판정을_같은_자리에서_부른다()
        {
            // macOS의 답은 true(= 지금까지의 동작)라 동작은 바뀌지 않는다. 그래도 호출을 요구하는 이유는
            // 값의 정본을 씬 에셋에서 코드로 옮기기 위해서다 — 한쪽만 옮기면 두 플랫폼이 서로 다른 정본을 갖는다.
            AssertDecisionPrecedesActivation(MacWindowServicePath, "MacWindowService");
        }

        private static void AssertDecisionPrecedesActivation(string sourcePath, string label)
        {
            string source = StripLineComments(ReadSource(sourcePath));

            // 양성 대조 — 주석 제거가 파일을 통째로 비우지 않았는가.
            StringAssert.Contains(ActivationCall, source,
                $"양성 대조 실패 — {label} 소스(주석 제거 후)에 활성화 호출이 없습니다. " +
                "주석 제거가 너무 많이 먹었거나 활성화 경로가 사라졌습니다.");
            Assert.AreEqual(-1, source.IndexOf(AbsentNeedle, StringComparison.Ordinal),
                $"음성 대조 실패 — {label}에서 존재하지 않아야 할 문자열이 걸렸습니다.");

            int activationCount = CountOccurrences(source, ActivationCall);
            Assert.AreEqual(1, activationCount,
                $"{label}의 활성화 호출이 {activationCount}곳입니다(기대 1곳). 여러 곳이면 아래 순서 판정이 " +
                "어느 자리를 본 것인지 알 수 없습니다 — 호출을 하나로 모으거나 이 테스트를 함께 고치세요.");

            int decisionIndex = source.IndexOf(EarlyDecisionName, StringComparison.Ordinal);
            Assert.AreNotEqual(-1, decisionIndex,
                $"{label}이 {nameof(StartupWindowModePolicy)}.{EarlyDecisionName}을 부르지 않습니다. " +
                "기동 창 모드 판정이 플랫폼 파일 안으로 되돌아갔다면, 그 순간 두 플랫폼이 다른 값을 갖게 됩니다 " +
                "(FullscreenSuspendPolicy 사고와 같은 형태).");

            int assignmentIndex = source.IndexOf("." + PackageForceWindowedField + " =", StringComparison.Ordinal);
            Assert.AreNotEqual(-1, assignmentIndex,
                $"{label}이 판정 결과를 {PackageForceWindowedField}에 대입하지 않습니다 — 판정만 하고 " +
                "적용하지 않으면 아무 일도 일어나지 않습니다.");

            int activationIndex = source.IndexOf(ActivationCall, StringComparison.Ordinal);
            Assert.Less(decisionIndex, activationIndex,
                $"{label}에서 판정 호출이 활성화 호출보다 <b>뒤</b>에 있습니다. " +
                "GameObject.SetActive(true)는 Awake()를 동기로 실행하고 그 Awake가 forceWindowed를 읽으므로, " +
                "대입이 뒤로 내려가면 컴파일도 테스트도 멀쩡한 채 효과만 0이 됩니다.");
            Assert.Less(assignmentIndex, activationIndex,
                $"{label}에서 {PackageForceWindowedField} 대입이 활성화보다 뒤입니다 — 위와 같은 이유로 무효입니다.");
        }

        // ====================================================================
        // (5) 탈출구 — 두 Enforcer가 같은 판정을 부른다
        // ====================================================================

        [Test]
        public void 부착_실패_탈출구가_양쪽_Enforcer에_배선돼_있다()
        {
            AssertEscapeHatchWired(WinEnforcerPath, "WindowsOverlayStateEnforcer");
            AssertEscapeHatchWired(MacEnforcerPath, "MacOverlayStateEnforcer");
        }

        private static void AssertEscapeHatchWired(string sourcePath, string label)
        {
            string source = StripLineComments(ReadSource(sourcePath));

            // 양성 대조 — 부착 제한 시간 상수는 두 파일에 원래부터 있다. 이게 안 걸리면 읽기/제거가 깨진 것이다.
            StringAssert.Contains("AttachTimeoutSeconds", source,
                $"양성 대조 실패 — {label} 소스(주석 제거 후)에 부착 제한 시간 상수가 없습니다. " +
                "이 파일을 제대로 읽지 못했으므로 아래 판정이 무효입니다.");
            Assert.AreEqual(-1, source.IndexOf(AbsentNeedle, StringComparison.Ordinal),
                $"음성 대조 실패 — {label}에서 존재하지 않아야 할 문자열이 걸렸습니다.");

            StringAssert.Contains(LateDecisionName, source,
                $"{label}이 {nameof(StartupWindowModePolicy)}.{LateDecisionName}을 부르지 않습니다. " +
                "조기 해제를 뺀 플랫폼에서 이 자리가 비면 부착이 실패한 세션에 탈출구가 남지 않고, " +
                "지금 답이 false인 플랫폼에서도 이 자리가 없으면 정책을 뒤집는 날 그대로 출하됩니다.");
        }

        // ====================================================================
        // 도우미
        // ====================================================================

        private static string ReadSource(string path)
        {
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// <c>//</c>부터 줄 끝까지를 지운다. <b>"실제로 부르는가"를 물을 때만</b> 쓴다 —
        /// 이 저장소는 결함을 설명하는 주석을 구현으로 오인해 통과한 사고가 있다(PlatformParityAuditTests 문서).
        ///
        /// <para><b>한계(정직하게)</b>: 문자열 리터럴 안의 <c>//</c>도 함께 지운다. 그래서 이 도우미를 쓴 뒤에는
        /// 반드시 <b>양성 대조</b>로 "필요한 니들이 아직 살아 있는가"를 확인한다 — 위 두 도우미가 그렇게 한다.</para>
        /// </summary>
        private static string StripLineComments(string source)
        {
            string[] lines = source.Split('\n');
            var sb = new System.Text.StringBuilder(source.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(comment >= 0 ? line.Substring(0, comment) : line).Append('\n');
            }
            return sb.ToString();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int at = 0;
            while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }

        /// <summary>
        /// 씬 YAML의 <c>propertyPath: &lt;name&gt;</c> 바로 다음 <c>value:</c> 줄을 bool로 읽는다
        /// (Unity는 bool을 0/1로 적는다). 찾지 못하면 <b>기본값을 만들지 않고 실패</b>시킨다 —
        /// 조용한 기본값은 이 검사를 공허하게 만든다.
        /// </summary>
        private static bool ReadSceneBoolProperty(string scene, string propertyName)
        {
            string[] lines = scene.Split('\n');
            string key = ScenePropertyPathKey + propertyName;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != key.Trim()) continue;
                for (int j = i + 1; j < Math.Min(lines.Length, i + 3); j++)
                {
                    string candidate = lines[j].Trim();
                    if (!candidate.StartsWith("value:", StringComparison.Ordinal)) continue;
                    string raw = candidate.Substring("value:".Length).Trim();
                    if (raw == "1") return true;
                    if (raw == "0") return false;
                    Assert.Fail($"씬의 {propertyName} 값을 bool로 읽지 못했습니다(원문 \"{raw}\") — " +
                        "Unity 직렬화 형식이 바뀌었다면 이 파서를 함께 고치세요.");
                }
            }

            Assert.Fail($"씬에서 {key} 항목을 찾지 못했습니다 — 위 존재 단언과 모순입니다(파서 결함).");
            return false;
        }
    }
}
