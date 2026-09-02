using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Dialogue;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 앱에 실재하는 <b>한국어 대사 전수</b>를 소스에서 훑는 단 하나의 수집기 — 2026-09-02 신설.
    ///
    /// ============================================================================
    /// 왜 생겼나 — 종전 스캐너는 33줄 중 <b>26줄만</b> 훑고 있었다
    /// ============================================================================
    /// <c>DialogueVisibleScaleContractTests.AllLines()</c>는 <c>AmbientChatter</c> 배열(리플렉션)과
    /// <c>States/*.cs</c>의 <c>DialogueLine.Say|React("…")</c> 리터럴만 봤다. 그런데 대사를 만드는
    /// 형태가 <b>둘 더</b> 있었고 둘 다 그 두 패턴에 걸리지 않는다:
    /// <list type="number">
    ///   <item><c>States/RunawayState.cs</c> — <c>TriggerSelfReturn("어… 알았어, 갈게")</c> 3곳(고유 2줄).
    ///     <c>DialogueLine.</c>으로 시작하지 않는다.</item>
    ///   <item><c>Core/StickmanAgent.cs</c> — <c>new TimedSpectacleState(…, cfg =&gt; "좋아, 감시 시작")</c> 5줄.
    ///     <b><c>States/</c> 폴더 밖</b>이고 형태도 람다다. 스캐너는 폴더조차 보지 않았다.</item>
    ///   <item>★ 2026-09-02 추가 — <c>Dialogue/GrabReactionLines.cs</c>의 붙잡힘 반응 <b>9줄</b>.
    ///     <c>AmbientChatter</c>와 같은 <b>배열</b> 형태라 리터럴 스캐너가 구조적으로 못 본다.
    ///     <c>design-narrative</c>가 인계 시점에 이 사각지대를 미리 경고했고, 그래서 대사가 늘어난
    ///     같은 라운드에 수집기를 함께 넓혔다(<see cref="GrabLines"/>).</item>
    /// </list>
    /// 그래서 <b>21%가 어떤 회귀 검사에도 닿지 않았다.</b> 게다가 그 파일의 표본 하한이
    /// <c>Assert.Greater(lines.Count, 20)</c>이라 <b>5줄이 더 사라져도 초록</b>이었다.
    ///
    /// <para>수집기를 파일마다 따로 두면 사본이 갈라진다(이 저장소가 이미 밟은 함정 —
    /// <see cref="SourceConstantReader"/>가 정리한 그 판단과 같다). 그래서 여기 하나로 모은다.</para>
    ///
    /// ============================================================================
    /// ★ 추출 함수를 <b>문자열 인자</b>로 받게 만든 이유 — 양성 대조
    /// ============================================================================
    /// "0건"과 "패턴이 안 맞아서 0건"은 <b>똑같이 생겼다</b>. 그래서 각 추출기는 실제 파일이 아니라
    /// <b>임의의 소스 텍스트</b>를 받는 순수 함수이고, 테스트가 <b>합성 소스</b>를 먹여
    /// "이 형태를 실제로 찾는가 / 형태가 없으면 정말 0인가"를 양방향으로 확인할 수 있다
    /// (<c>DialogueLanguageBudgetTests</c>의 스캐너 양성 대조).
    /// </summary>
    internal static class DialogueCorpus
    {
        internal const string LogPrefix = "[대사말뭉치-TEST]";

        /// <summary>이 저장소의 대사 총 고유 줄 수. <b>숫자를 여기 적지 않는다</b> —
        /// 골든 파일의 줄 수가 기대값이고(<see cref="GoldenPath"/>), 이 상수는 존재하지 않는다.</summary>
        internal static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        internal static string GoldenPath =>
            Path.Combine(ScriptsRoot, "Tests", "EditMode", "Golden", "DialogueBudgetKoGolden.txt");

        // ================================================================================
        // 추출기 — 전부 순수 함수(소스 텍스트 -> 리터럴 목록)
        // ================================================================================

        private static readonly Regex SayReact = new Regex(@"DialogueLine\.(?:Say|React)\(\s*""([^""]*)""");
        private static readonly Regex SelfReturn = new Regex(@"TriggerSelfReturn\(\s*""([^""]*)""");
        private static readonly Regex ConfigLambda = new Regex(@"cfg\s*=>\s*""([^""]*)""");

        /// <summary><c>DialogueLine.Say("…")</c> / <c>.React("…")</c>.</summary>
        internal static List<string> ExtractSayReact(string source) => Extract(SayReact, source);

        /// <summary>★ 사각지대 1 — <c>TriggerSelfReturn("…")</c>(가출 자진 복귀).</summary>
        internal static List<string> ExtractSelfReturn(string source) => Extract(SelfReturn, source);

        /// <summary>★ 사각지대 2 — <c>cfg =&gt; "…"</c>(<c>TimedSpectacleState</c>의 대사 공급 람다).</summary>
        internal static List<string> ExtractConfigLambda(string source) => Extract(ConfigLambda, source);

        private static List<string> Extract(Regex pattern, string source)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(source)) return found;
            foreach (Match m in pattern.Matches(source))
            {
                string text = m.Groups[1].Value;
                if (!string.IsNullOrEmpty(text)) found.Add(text);
            }
            return found;
        }

        // ================================================================================
        // 수집 — 실제 트리
        // ================================================================================

        /// <summary>
        /// ★ 배열형 대사표를 <b>리플렉션으로</b> 읽는다(소스 파싱이 아니라 <b>실제 배열</b>).
        ///
        /// <para><b>왜 <see cref="Assert"/>를 안 쓰고 bool을 돌려주는가</b>: 이 함수 자체의
        /// <b>양성/음성 대조</b>를 짜기 위해서다. 안에서 단언해 버리면 "없을 때 정말 false인가"를
        /// 테스트가 확인할 방법이 없고, 그러면 <b>수집기가 죽었는데 0건이 나온 것</b>과
        /// <b>정말 0건인 것</b>이 또 똑같이 생긴다. 실제 트리에서는 아래
        /// <see cref="RequireStringArray"/>가 시끄럽게 실패한다.</para>
        /// </summary>
        internal static bool TryReadStringArray(System.Type owner, string fieldName, out string[] lines)
        {
            lines = null;
            if (owner == null || string.IsNullOrEmpty(fieldName)) return false;
            FieldInfo field = owner.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || field.FieldType != typeof(string[])) return false;
            var value = field.GetValue(null) as string[];
            if (value == null || value.Length == 0) return false;
            lines = value;
            return true;
        }

        /// <summary>실제 트리용 — 못 읽으면 <b>실패</b>한다. 조용히 0건을 돌려주는 것이 최악이다.</summary>
        internal static string[] RequireStringArray(System.Type owner, string fieldName)
        {
            Assert.IsTrue(TryReadStringArray(owner, fieldName, out string[] lines),
                $"{LogPrefix} {owner?.Name}.{fieldName}(string[])을 읽지 못했습니다 — " +
                "대사표 이름/형태가 바뀌었다면 이 수집기도 함께 고쳐야 합니다. " +
                "고치지 않으면 그 표의 대사가 어떤 회귀 검사에도 닿지 않습니다(조용한 초록).");
            return lines;
        }

        /// <summary><c>AmbientChatter</c>의 대사표.</summary>
        internal static string[] AmbientLines(string fieldName)
            => RequireStringArray(typeof(AmbientChatter), fieldName);

        /// <summary>★ 사각지대 3(2026-09-02 신설) — <c>GrabReactionLines</c>의 붙잡힘 반응 대사표.
        /// <c>AmbientChatter</c>와 같은 <b>배열</b> 형태라 소스 리터럴 스캐너에는 걸리지 않는다.</summary>
        internal static string[] GrabLines(string fieldName)
            => RequireStringArray(typeof(GrabReactionLines), fieldName);

        /// <summary>앱에 실재하는 대사 <b>전수</b>(중복 포함). 다섯 갈래를 전부 훑는다 —
        /// AmbientChatter 배열 / GrabReactionLines 배열 / States의 DialogueLine.Say|React 리터럴 /
        /// RunawayState.TriggerSelfReturn / StickmanAgent 집중모드 람다.</summary>
        internal static List<string> ScanAll()
        {
            var lines = new List<string>(48);
            lines.AddRange(AmbientLines("IdleLines"));
            lines.AddRange(AmbientLines("WalkLines"));

            // ★ 사각지대 3 — 붙잡힘 반응 4표(2026-09-02). 배열이라 리터럴 스캐너에 안 걸린다.
            lines.AddRange(GrabLines("HeadLines"));
            lines.AddRange(GrabLines("LegLines"));
            lines.AddRange(GrabLines("AnyLines"));
            lines.AddRange(GrabLines("FallbackLines"));

            string statesDir = Path.Combine(ScriptsRoot, "States");
            Assert.IsTrue(Directory.Exists(statesDir), $"{LogPrefix} States 폴더를 찾지 못했습니다: {statesDir}");
            string[] stateFiles = Directory.GetFiles(statesDir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(stateFiles.Length, 0, $"{LogPrefix} States 폴더에서 .cs를 하나도 찾지 못했습니다.");
            foreach (string file in stateFiles) lines.AddRange(ExtractSayReact(File.ReadAllText(file)));

            // ★ 사각지대 1 — RunawayState.TriggerSelfReturn
            lines.AddRange(ExtractSelfReturn(ReadRequired(Path.Combine(statesDir, "RunawayState.cs"))));

            // ★ 사각지대 2 — StickmanAgent 집중 모드 람다 (States/ 밖이다)
            lines.AddRange(ExtractConfigLambda(
                ReadRequired(Path.Combine(ScriptsRoot, "Core", "StickmanAgent.cs"))));

            return lines;
        }

        /// <summary>정렬된 고유 대사. 골든과 <b>양방향</b>으로 대조하는 쪽이다.</summary>
        internal static List<string> ScanDistinct()
        {
            var distinct = ScanAll().Distinct().ToList();
            distinct.Sort(System.StringComparer.Ordinal);
            return distinct;
        }

        private static string ReadRequired(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 대사 원본을 찾지 못했습니다: {path} — " +
                "파일이 옮겨졌다면 이 수집기도 함께 고쳐야 합니다.");
            return File.ReadAllText(path);
        }

        // ================================================================================
        // 골든
        // ================================================================================

        internal readonly struct GoldenRow
        {
            internal readonly string Bits;      // IEEE754 single, 빅엔디안 16진 8자리
            internal readonly string Seconds;   // F6
            internal readonly string Text;

            internal GoldenRow(string bits, string seconds, string text)
            {
                Bits = bits;
                Seconds = seconds;
                Text = text;
            }
        }

        /// <summary>골든을 읽는다. <b>비어 있으면 실패한다</b> — 빈 목록을 순회하고 초록을 내는 것이
        /// 이 저장소가 겪은 거짓 통과 #5의 형태다.</summary>
        internal static List<GoldenRow> ReadGolden()
        {
            Assert.IsTrue(File.Exists(GoldenPath), $"{LogPrefix} 골든 파일이 없습니다: {GoldenPath}");
            var rows = new List<GoldenRow>(40);
            foreach (string raw in File.ReadAllLines(GoldenPath))
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                string[] parts = raw.Split('\t');
                Assert.AreEqual(3, parts.Length,
                    $"{LogPrefix} 골든 형식이 어긋납니다(탭 3열이어야 합니다): {raw}");
                rows.Add(new GoldenRow(parts[0], parts[1], parts[2]));
            }
            Assert.Greater(rows.Count, 0, $"{LogPrefix} 골든이 비어 있습니다 — " +
                "이 상태로는 아래 모든 단언이 0건을 훑고 통과합니다(거짓 초록).");
            return rows;
        }
    }
}
