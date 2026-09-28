using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-28 — <b>기동 표시 보류</b>(<see cref="StartupPresentationHold"/>)를 잠근다.
    /// 사용자 실기 신고 「시작할때 전체 흰화면이 계속 켜져있다가 꺼짐」의 조치다.
    ///
    /// ============================================================================
    /// 이 파일의 단언은 전부 <b>관계</b>다 — 부재 단언 하나로 서 있는 항목이 없다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>보류색 ↔ 출하 설정</b>: 보류색 상수는 <c>ProjectSettings.asset</c>의 스플래시 배경색과
    ///     <b>같아야</b> 한다. 런타임 API가 없어 상수로 복사해 둔 값이라, 한쪽이 바뀌면 여기서 빨개진다.</item>
    ///   <item><b>상한 ↔ 복원</b>: 상한에 닿으면 <b>반드시</b> 복원하고, 복원 색은 <see cref="StartupPresentationHold.Begin"/>에
    ///     넘긴 폴백과 <b>정확히</b> 같아야 한다(성분을 일부러 서로 다르게 줘서 뒤바뀜도 잡는다).</item>
    ///   <item><b>양 플랫폼 ↔ 공용 상수</b>: 두 Enforcer가 같은 상한을 <b>참조</b>해야 한다. 「각자 15f를
    ///     들고 있지 않다」는 부재 단언은 <b>그 참조가 실재한다</b>는 존재 단언과 <b>짝으로만</b> 쓴다.</item>
    ///   <item><b>배선 ↔ 순서</b>: 보류 호출이 <b>부착 판정보다 앞</b>이어야 한다. 뒤로 내려가면
    ///     컴파일도 테스트도 멀쩡한 채 「부착 전 구간」을 한 프레임도 덮지 못한다.</item>
    /// </list>
    ///
    /// <para><b>PlayMode 예산 영향 0의 근거</b>도 여기서 잠근다(아래 마지막 검사): 두 Enforcer는 파일
    /// 전체가 <c>#if UNITY_STANDALONE_*</c> 안이라 <b>에디터·헤드리스에 타입이 존재하지 않는다</b> ⇒
    /// 배치 PlayMode에서는 보류가 무장되지 않으므로 기다림이 생기지 않는다.</para>
    /// </summary>
    public sealed class StartupPresentationHoldTests
    {
        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string WinEnforcerPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs");

        private static string MacEnforcerPath =>
            Path.Combine(PlatformRoot, "MacOS", "MacOverlayStateEnforcer.cs");

        private static string ProjectSettingsPath =>
            Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset");

        /// <summary>Unity 직렬화 키 — <b>외부 스키마</b>라 유도할 곳이 없다. 존재 단언으로만 쓴다.</summary>
        private const string SplashColorKey = "m_SplashScreenBackgroundColor:";

        /// <summary>같은 파일을 읽었는지 증명하는 양성 대조 앵커(역시 Unity 스키마).</summary>
        private const string SplashToggleKey = "m_ShowUnitySplashScreen:";

        /// <summary>계측기 교정용 — 어느 소스에도 없어야 한다.</summary>
        private const string AbsentNeedle = "이문자열은어느소스에도없다_계기교정용_StartupPresentationHoldTests";

        /// <summary>부착 판정 줄(양 Enforcer 공통). 순서 판정의 기준점이다.</summary>
        private const string AttachVerdictAnchor = "bool attached = ";

        private sealed class RecordingSink
        {
            public readonly List<(float R, float G, float B)> Applied = new List<(float, float, float)>();
            public void Apply(float r, float g, float b) => Applied.Add((r, g, b));
        }

        private static StartupPresentationHold NewHold(RecordingSink sink, bool disabled = false)
            => new StartupPresentationHold(disabled, OverlayStateReapplyPolicy.AttachTimeoutSeconds, sink.Apply);

        // ====================================================================
        // (1) 보류색 ↔ 출하 스플래시 배경색
        // ====================================================================

        [Test]
        public void 보류색은_출하_스플래시_배경색과_같다()
        {
            Assert.IsTrue(File.Exists(ProjectSettingsPath),
                $"ProjectSettings.asset을 찾지 못했습니다: {ProjectSettingsPath}");
            string settings = File.ReadAllText(ProjectSettingsPath);

            // 양성 대조 — 엉뚱한 파일을 읽어도 아래 비교가 조용히 통과하지 않게.
            StringAssert.Contains(SplashToggleKey, settings,
                "양성 대조 실패 — 읽은 파일에 스플래시 설정 자체가 없습니다. ProjectSettings.asset이 " +
                "아니므로 아래 색 비교는 무효입니다.");
            // 음성 대조 — 계측기가 아무 문자열이나 찾아 주는 상태가 아닌지.
            Assert.AreEqual(-1, settings.IndexOf(AbsentNeedle, StringComparison.Ordinal),
                "음성 대조 실패 — 존재하지 않아야 할 문자열이 걸렸습니다.");

            string line = settings.Split('\n').FirstOrDefault(l => l.Contains(SplashColorKey));
            Assert.IsNotNull(line,
                $"{SplashColorKey} 줄을 찾지 못했습니다 — Unity가 키 이름을 바꿨다면 보류색의 출처가 " +
                "사라진 것이므로 이 검사를 함께 고치세요(조용히 통과시키지 마세요).");

            int brace = line.IndexOf('{');
            Assert.Greater(brace, -1, $"색 줄의 형식이 예상과 다릅니다: {line}");
            string body = line.Substring(brace + 1);

            float r = ParseComponent(body, "r:");
            float g = ParseComponent(body, "g:");
            float b = ParseComponent(body, "b:");

            const float Tolerance = 1e-5f;
            Assert.AreEqual(r, StartupPresentationHoldPolicy.HoldRed, Tolerance,
                "보류색 R이 출하 스플래시 배경색과 다릅니다. 보류의 목적은 「스플래시가 잠시 더 이어지는 " +
                "것처럼 보이게」인데 색이 갈라지면 기동에 새 화면이 하나 생깁니다. 스플래시 색을 바꿨다면 " +
                $"{nameof(StartupPresentationHoldPolicy)}의 상수도 같은 커밋에서 바꾸세요(런타임 API가 " +
                "없어 복사해 둔 값입니다).");
            Assert.AreEqual(g, StartupPresentationHoldPolicy.HoldGreen, Tolerance, "보류색 G가 스플래시와 다릅니다.");
            Assert.AreEqual(b, StartupPresentationHoldPolicy.HoldBlue, Tolerance, "보류색 B가 스플래시와 다릅니다.");

            // 보류색이 「어둡다」는 전제 자체도 못박는다 — 밝은 값으로 바뀌면 흰 화면을 흰색으로 덮는 셈이다.
            Assert.Less(StartupPresentationHoldPolicy.HoldRed, 0.5f,
                "보류색이 더 이상 어둡지 않습니다 — 근백색 노출을 덮는다는 이 장치의 전제가 무너집니다.");
        }

        // ====================================================================
        // (2)(3)(4)(5) 상태기계 — 상한·복원·넘김·끄기 스위치
        // ====================================================================

        [Test]
        public void 상한에_닿으면_반드시_복원하고_복원색은_폴백과_정확히_같다()
        {
            var sink = new RecordingSink();
            StartupPresentationHold hold = NewHold(sink);

            const double Start = 1234.5;
            // 성분을 일부러 서로 다르게 준다 — R/G/B가 뒤바뀌면 아래 비교가 잡는다.
            const float FallbackR = 0.94f, FallbackG = 0.93f, FallbackB = 0.92f;

            Assert.IsTrue(hold.Begin(Start, FallbackR, FallbackG, FallbackB), "보류가 시작되지 않았습니다.");
            Assert.AreEqual(StartupPresentationHoldPhase.Holding, hold.Phase);
            Assert.AreEqual(1, sink.Applied.Count, "보류 시작이 색을 정확히 한 번 써야 합니다.");
            AssertRgb(sink.Applied[0], StartupPresentationHoldPolicy.HoldRed,
                StartupPresentationHoldPolicy.HoldGreen, StartupPresentationHoldPolicy.HoldBlue,
                "보류 시작이 스플래시 배경색을 쓰지 않았습니다.");

            double budget = OverlayStateReapplyPolicy.AttachTimeoutSeconds;

            Assert.IsFalse(hold.Tick(Start + budget - 0.001), "상한 <b>전</b>에 복원하면 안 됩니다.");
            Assert.AreEqual(1, sink.Applied.Count, "상한 전 틱이 색을 썼습니다.");

            Assert.IsTrue(hold.Tick(Start + budget), "상한에 닿았는데 복원하지 않았습니다 — " +
                "이 경로가 죽으면 투명이 실패한 환경에서 어두운 배경이 영원히 남습니다(검정-on-검정).");
            Assert.AreEqual(2, sink.Applied.Count);
            AssertRgb(sink.Applied[1], FallbackR, FallbackG, FallbackB,
                "복원 색이 Begin에 넘긴 폴백(StickConfig.backgroundFallbackColor)과 다릅니다.");
            Assert.AreEqual(StartupPresentationHoldPhase.Restored, hold.Phase);
            Assert.AreEqual(StartupPresentationHoldRelease.TimeoutRestored, hold.LastRelease);

            // 멱등 — 복원 뒤에는 어떤 호출도 색을 다시 쓰지 않는다.
            Assert.IsFalse(hold.Tick(Start + budget * 10.0));
            Assert.IsFalse(hold.RestoreNow(StartupPresentationHoldRelease.AttachFailureRestored));
            Assert.IsFalse(hold.NoteTransparentCorrectionApplied());
            Assert.AreEqual(2, sink.Applied.Count, "복원 뒤에 색이 또 쓰였습니다.");
        }

        [Test]
        public void 투명_교정이_걸리면_색을_쓰지_않고_넘기며_이후_상한에도_다시_칠하지_않는다()
        {
            var sink = new RecordingSink();
            StartupPresentationHold hold = NewHold(sink);

            Assert.IsTrue(hold.Begin(100.0, 0.94f, 0.93f, 0.92f));
            Assert.AreEqual(1, sink.Applied.Count);

            Assert.IsTrue(hold.NoteTransparentCorrectionApplied(), "넘김이 한 번은 먹어야 합니다.");
            Assert.AreEqual(StartupPresentationHoldPhase.HandedOver, hold.Phase);
            Assert.AreEqual(StartupPresentationHoldRelease.HandedOverToTransparency, hold.LastRelease);
            Assert.AreEqual(1, sink.Applied.Count,
                "넘김이 색을 썼습니다 — 그 시점의 배경은 투명 교정이 이미 검정(알파 보존)으로 바꿔 놓았고, " +
                "여기서 또 쓰면 그 교정을 덮습니다.");

            // 넘긴 뒤에는 상한이 지나도 근백색을 덧칠하면 안 된다(투명 창을 흰색으로 덮는 최악).
            Assert.IsFalse(hold.Tick(100.0 + OverlayStateReapplyPolicy.AttachTimeoutSeconds * 10.0));
            Assert.AreEqual(1, sink.Applied.Count,
                "넘김 뒤 상한이 근백색을 덧칠했습니다 — 투명하게 합성되던 창이 흰 사각형이 됩니다.");
        }

        [Test]
        public void 부착_실패_복원도_같은_폴백색을_쓰고_멱등이다()
        {
            var sink = new RecordingSink();
            StartupPresentationHold hold = NewHold(sink);

            const float FallbackR = 0.94f, FallbackG = 0.93f, FallbackB = 0.92f;
            Assert.IsTrue(hold.Begin(0.0, FallbackR, FallbackG, FallbackB));

            Assert.IsTrue(hold.RestoreNow(StartupPresentationHoldRelease.AttachFailureRestored));
            Assert.AreEqual(2, sink.Applied.Count);
            AssertRgb(sink.Applied[1], FallbackR, FallbackG, FallbackB,
                "부착 실패 복원이 상한 복원과 다른 색을 썼습니다 — 두 경로는 같은 폴백을 써야 합니다.");
            Assert.AreEqual(StartupPresentationHoldRelease.AttachFailureRestored, hold.LastRelease);

            Assert.IsFalse(hold.RestoreNow(StartupPresentationHoldRelease.AttachFailureRestored));
            Assert.AreEqual(2, sink.Applied.Count);
        }

        [Test]
        public void 끄기_스위치가_켜지면_한_번도_시작하지_않는다()
        {
            var sink = new RecordingSink();
            StartupPresentationHold hold = NewHold(sink, disabled: true);

            Assert.IsTrue(hold.IsDisabled);
            Assert.IsFalse(hold.Begin(0.0, 0.94f, 0.93f, 0.92f), "끄기 스위치가 켜졌는데 보류가 시작됐습니다.");
            Assert.AreEqual(StartupPresentationHoldPhase.Inactive, hold.Phase);
            Assert.IsFalse(hold.Tick(1000.0));
            Assert.IsEmpty(sink.Applied, "끄기 스위치가 켜졌는데 색이 쓰였습니다 — 이전 동작과 달라집니다.");

            // 스위치 해석 자체도 교정한다(비었거나 0이면 켜짐 = 기본 동작).
            Assert.IsFalse(StartupPresentationHoldPolicy.IsDisabledByEnvironmentValue(null));
            Assert.IsFalse(StartupPresentationHoldPolicy.IsDisabledByEnvironmentValue(string.Empty));
            Assert.IsFalse(StartupPresentationHoldPolicy.IsDisabledByEnvironmentValue("0"));
            Assert.IsTrue(StartupPresentationHoldPolicy.IsDisabledByEnvironmentValue("1"));
        }

        // ====================================================================
        // (6) 양 플랫폼 ↔ 공용 상한 상수
        // ====================================================================

        [Test]
        public void 양_플랫폼_Enforcer가_같은_부착_상한_상수를_참조한다()
        {
            Assert.Greater(OverlayStateReapplyPolicy.AttachTimeoutSeconds, 0f,
                "공용 부착 상한이 0 이하입니다 — 보류가 시작조차 하지 않습니다(Begin이 거절합니다).");

            string reference = nameof(OverlayStateReapplyPolicy) + "." +
                nameof(OverlayStateReapplyPolicy.AttachTimeoutSeconds);

            foreach ((string path, string label) in EnforcerSources())
            {
                string src = StripLineComments(File.ReadAllText(path));

                // 양성 대조 — 주석 제거가 파일을 비우지 않았는가.
                StringAssert.Contains(nameof(OverlayStateReapplyPolicy.ReapplyAttempts), src,
                    $"양성 대조 실패 — {label}에서 기존 공용 상수 참조조차 보이지 않습니다.");
                Assert.AreEqual(-1, src.IndexOf(AbsentNeedle, StringComparison.Ordinal),
                    $"음성 대조 실패 — {label}에서 존재하지 않아야 할 문자열이 걸렸습니다.");

                // ① 존재: 공용 정본을 실제로 참조한다.
                StringAssert.Contains(reference, src,
                    $"{label}이 공용 부착 상한({reference})을 참조하지 않습니다. 각자 숫자를 들면 " +
                    "두 플랫폼이 다른 시각에 포기하게 되고, 그 차이는 아무도 모릅니다(두 파일 모두 " +
                    "#if 안이라 테스트가 타입으로는 볼 수 없습니다).");

                // ② 부재: 숫자를 다시 들고 있지 않다. ★ 위 존재 단언과 <b>짝으로만</b> 성립한다 —
                //    기능이 통째로 사라져도 참이 되는 부재 단언을 홀로 두지 않는다(CLAUDE.md).
                StringAssert.DoesNotContain("AttachTimeoutSeconds = 15f", src,
                    $"{label}이 부착 상한 숫자를 다시 들고 있습니다 — 공용 정본과 갈라집니다.");
            }
        }

        // ====================================================================
        // (7) 배선과 순서 — 보류는 부착 판정보다 앞에서 돈다
        // ====================================================================

        [Test]
        public void 양_플랫폼_Enforcer가_보류를_부착_판정보다_앞에서_돌린다()
        {
            foreach ((string path, string label) in EnforcerSources())
            {
                string src = StripLineComments(File.ReadAllText(path));

                StringAssert.Contains(AttachVerdictAnchor, src,
                    $"양성 대조 실패 — {label}에서 부착 판정 줄을 찾지 못했습니다(기준점이 사라졌습니다).");

                foreach (string needle in new[]
                         {
                             nameof(StartupPresentationHold),
                             nameof(StartupPresentationHoldPolicy),
                             nameof(StartupPresentationHold.NoteTransparentCorrectionApplied),
                             nameof(StartupPresentationHold.RestoreNow),
                             nameof(StartupPresentationHoldRelease.AttachFailureRestored),
                         })
                {
                    StringAssert.Contains(needle, src,
                        $"{label}에 보류 배선({needle})이 없습니다 — 그 플랫폼만 기동 흰 화면이 남습니다.");
                }

                int beginAt = src.IndexOf("BeginStartupPresentationHoldIfNeeded()", StringComparison.Ordinal);
                int tickAt = src.IndexOf("TickStartupPresentationHold()", StringComparison.Ordinal);
                int attachAt = src.IndexOf(AttachVerdictAnchor, StringComparison.Ordinal);

                Assert.Greater(beginAt, -1, $"{label}에 보류 시작 호출이 없습니다(이름이 바뀌었다면 이 검사도 함께 고치세요).");
                Assert.Greater(tickAt, -1, $"{label}에 보류 틱 호출이 없습니다.");
                Assert.Less(beginAt, attachAt,
                    $"{label}에서 보류 시작이 부착 판정보다 뒤입니다 — 보류가 필요한 구간이 정확히 " +
                    "'부착 전'이라, 뒤로 내려가면 그 구간을 한 프레임도 덮지 못합니다(컴파일도 " +
                    "테스트도 멀쩡한 채 효과만 0이 되는 형태).");
                Assert.Less(tickAt, attachAt, $"{label}에서 보류 틱이 부착 판정보다 뒤입니다(위와 같은 이유).");
            }
        }

        // ====================================================================
        // (8) PlayMode 예산 영향 0의 근거
        // ====================================================================

        [Test]
        public void 두_Enforcer는_에디터_헤드리스에_타입조차_없다()
        {
            var expected = new (string Path, string Label, string Guard)[]
            {
                (WinEnforcerPath, "WindowsOverlayStateEnforcer", "#if UNITY_STANDALONE_WIN"),
                (MacEnforcerPath, "MacOverlayStateEnforcer", "#if UNITY_STANDALONE_OSX"),
            };

            foreach ((string path, string label, string guard) in expected)
            {
                Assert.IsTrue(File.Exists(path), $"{label} 소스를 찾지 못했습니다: {path}");
                string[] lines = File.ReadAllLines(path);
                Assert.Greater(lines.Length, 10, $"{label} 소스가 비었습니다 — 아래 판정이 공허합니다.");
                Assert.AreEqual(guard, lines[0].Trim(),
                    $"{label}의 첫 줄이 플랫폼 가드가 아닙니다. 이 가드가 곧 " +
                    "「배치 PlayMode에는 이 타입이 없다 ⇒ 기동 표시 보류가 무장되지 않는다 ⇒ " +
                    "테스트 벽시계 예산 영향 0」의 근거입니다. 가드가 풀리면 그 근거가 사라지므로 " +
                    "PlayMode 예산을 다시 재야 합니다(상한이 " +
                    $"{OverlayStateReapplyPolicy.AttachTimeoutSeconds:F0}초이므로 기동을 기다리는 " +
                    "테스트 1건당 최악 그만큼 늘어납니다).");
                StringAssert.Contains("class " + label, string.Join("\n", lines),
                    $"양성 대조 실패 — {label} 안에서 그 이름의 타입 선언을 찾지 못했습니다.");
            }
        }

        // ====================================================================
        // 도우미
        // ====================================================================

        private static IEnumerable<(string Path, string Label)> EnforcerSources()
        {
            yield return (WinEnforcerPath, "WindowsOverlayStateEnforcer");
            yield return (MacEnforcerPath, "MacOverlayStateEnforcer");
        }

        private static void AssertRgb((float R, float G, float B) actual, float r, float g, float b, string message)
        {
            const float Tolerance = 1e-6f;
            Assert.AreEqual(r, actual.R, Tolerance, message + " (R)");
            Assert.AreEqual(g, actual.G, Tolerance, message + " (G)");
            Assert.AreEqual(b, actual.B, Tolerance, message + " (B)");
        }

        /// <summary>
        /// <c>//</c>부터 줄 끝까지 지운다 — 결함을 설명하는 주석을 구현으로 오인하지 않기 위해서다
        /// (이 저장소의 거짓 초록 두 건이 그 형태였다). <b>한계</b>: 문자열 리터럴 안의 <c>//</c>도 지우므로,
        /// 쓰는 쪽은 반드시 양성 대조로 「필요한 니들이 아직 살아 있는가」를 함께 확인한다.
        /// </summary>
        private static string StripLineComments(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                int at = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(at >= 0 ? line.Substring(0, at) : line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Unity 색 직렬화(<c>{r: 0.12, g: 0.12, b: 0.13, a: 1}</c>)에서 성분 하나를 읽는다.</summary>
        private static float ParseComponent(string body, string key)
        {
            int at = body.IndexOf(key, StringComparison.Ordinal);
            Assert.Greater(at, -1, $"색 성분 '{key}'를 찾지 못했습니다: {body}");

            int start = at + key.Length;
            int end = start;
            while (end < body.Length && body[end] != ',' && body[end] != '}') end++;

            string raw = body.Substring(start, end - start).Trim();
            Assert.IsTrue(float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value),
                $"색 성분 '{key}'를 수로 읽지 못했습니다(원문 \"{raw}\") — Unity 직렬화 형식이 바뀌었다면 " +
                "이 파서를 함께 고치세요.");
            return value;
        }
    }
}
