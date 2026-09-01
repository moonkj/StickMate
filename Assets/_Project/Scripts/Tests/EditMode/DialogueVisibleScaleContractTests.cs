using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>대사 표시 시간</b> 3단 세그먼트의 계약(docs/UX_FLOW.md 42절) — 2026-09-02.
    ///
    /// ============================================================================
    /// 이 라운드가 고친 병
    /// ============================================================================
    /// 노출 상한이 글자수 함수가 된 뒤(2026-09-01), 초 슬라이더 <c>대사 표시 시간</c>(1.5~6.0초)은
    /// <b>손잡이 10칸 중 7칸이 화면을 한 톨도 바꾸지 못하는</b> 상태가 됐다. 그리고 배포 기본값
    /// 4.0초가 <b>이미 그 죽은 구간 안</b>이었다 — 사용자가 설정창을 처음 열었을 때 보는 상태가
    /// "손잡이를 오른쪽으로 미는 모든 행위가 무효"인 상태였다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 성질 (전부 네거티브 컨트롤을 동반한다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>★ 핵심 계약 — 배율을 바꿔도 발화되는 대사 집합이 동일하다.</b> 배율은 화면 노출
    ///     (하한·상한)에만 곱하고 <b>발화 자격 게이트에는 곱하지 않는다</b>(42-5 확정안 B).
    ///     곱하면 "더 오래 보고 싶다"는 입력이 <b>"덜 본다"</b>는 출력을 낳는다.</item>
    ///   <item><b>안전 성질</b> — 100%에서 화면 결과가 배율 도입 이전과 한 톨도 다르지 않다.</item>
    ///   <item><b>하한이 100%인 이유</b> — 규칙 6("완전 불투명 ≥ 77%")이 강제한다. 취향이 아니다.</item>
    ///   <item><b>상한이 200%인 이유</b> — 포화가 막 시작되는 문턱. 더 올리면 "칸을 옮겼는데 화면이
    ///     안 바뀐다"가 <b>위쪽에서 재발</b>한다.</item>
    ///   <item><b>칸이 서로 구분된다</b> — 세 칸의 화면 결과 차이가 페이드아웃보다 크다.</item>
    /// </list>
    ///
    /// <para>★ 숫자를 베끼지 않는다. 기대값은 전부 <see cref="DialogueBudget"/>/
    /// <see cref="DialogueTiming"/>/<see cref="AppSettingsModel"/>/<see cref="StickConfig"/>에서
    /// <b>계산해</b> 만든다(CLAUDE.md 협업 프로토콜).</para>
    ///
    /// <para><b>플랫폼</b>: 완전히 플랫폼 중립. 전부 초 단위 무차원 값이고 플랫폼 분기가 하나도 없다.</para>
    /// </summary>
    public sealed class DialogueVisibleScaleContractTests
    {
        private const string LogPrefix = "[표시시간-TEST]";

        private static readonly DialogueVisibleLength[] Ladder =
        {
            DialogueVisibleLength.Default,
            DialogueVisibleLength.Long,
            DialogueVisibleLength.VeryLong,
        };

        [SetUp]
        public void SetUp() => AppSettingsModel.ResetForTesting();

        [TearDown]
        public void TearDown() => AppSettingsModel.ResetForTesting();

        // ==================================================================================
        // 0. 표본 — 앱에 실재하는 대사 전수
        // ==================================================================================

        /// <summary>
        /// 앰비언트 대사표(리플렉션) + <c>States/*.cs</c>의 <c>DialogueLine.Say/React</c> 리터럴(소스 스캔).
        /// <para><b>왜 소스를 읽는가</b>: 상태 대사는 인라인 리터럴이라 리플렉션이 닿지 않는다.
        /// 여기에 문장을 손으로 베껴 적으면 <b>대사가 추가되는 날 이 검사가 조용히 옛 표를 훑는다</b>
        /// (<see cref="SourceConstantReader"/>가 정리한 그 판단과 같다).</para>
        /// </summary>
        private static List<string> AllLines()
        {
            var lines = new List<string>(40);
            lines.AddRange(AmbientLines("IdleLines"));
            lines.AddRange(AmbientLines("WalkLines"));

            string statesDir = Path.Combine(Application.dataPath, "_Project", "Scripts", "States");
            Assert.IsTrue(Directory.Exists(statesDir), $"{LogPrefix} States 폴더를 찾지 못했습니다: {statesDir}");

            var literal = new Regex(@"DialogueLine\.(?:Say|React)\(\s*""([^""]*)""");
            foreach (string file in Directory.GetFiles(statesDir, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in literal.Matches(File.ReadAllText(file)))
                {
                    string text = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(text)) lines.Add(text);
                }
            }

            // 표본이 비거나 쪼그라들면 아래 모든 단언이 "0건을 훑고 통과"한다 — 최악의 거짓 초록이다.
            Assert.Greater(lines.Count, 20,
                $"{LogPrefix} 대사 표본이 {lines.Count}건뿐입니다 — 대사표 이름이나 리터럴 형태가 " +
                "바뀌었다면 이 스캐너도 함께 고쳐야 합니다(조용히 통과하는 것이 최악입니다).");
            return lines;
        }

        private static string[] AmbientLines(string fieldName)
        {
            FieldInfo field = typeof(AmbientChatter).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, $"{LogPrefix} AmbientChatter.{fieldName}을 찾지 못했습니다.");
            var lines = (string[])field.GetValue(null);
            Assert.IsNotNull(lines);
            Assert.Greater(lines.Length, 0, $"{LogPrefix} {fieldName}이 비어 있습니다.");
            return lines;
        }

        private static float Cap(string text, float scale)
            => DialogueBudget.MaxVisibleSecondsFor(text, DialogueTiming.PopInSeconds,
                DialogueTiming.FadeOutSeconds, scale);

        // ==================================================================================
        // 1. ★★ 핵심 계약 — 배율을 바꿔도 발화 집합이 동일하다
        // ==================================================================================

        /// <summary>
        /// 배율은 <b>화면 노출</b>에만 곱한다. 발화 자격 게이트(규칙 8)에 새어 들어가면
        /// <b>"길게를 골랐더니 대사가 사라진다"</b>가 된다 — 이번 라운드가 고치는 병("컨트롤이
        /// 움직이는데 화면이 약속과 다르다")의 사촌이다.
        ///
        /// <para>계획 잔여 체류를 <b>가능한 모든 경계</b>에서 훑는다: 각 대사의 필요체류를 기준으로
        /// 바로 아래/정확히/바로 위 세 점. 여기서 집합이 갈리지 않으면 어떤 상태 길이에서도 갈리지
        /// 않는다.</para>
        /// </summary>
        [Test]
        public void 배율을_바꿔도_발화되는_대사_집합이_동일하다()
        {
            List<string> lines = AllLines();
            int compared = 0;

            foreach (string text in lines)
            {
                float required = DialogueBudget.RequiredDwellSeconds(text, DialogueTiming.FadeInSeconds);
                float[] probes = { required - 0.01f, required, required + 0.01f, 0f, float.NaN };

                foreach (float dwell in probes)
                {
                    bool baseline = Eligible(text, dwell, DialogueVisibleLength.Default);
                    foreach (DialogueVisibleLength length in Ladder)
                    {
                        Assert.AreEqual(baseline, Eligible(text, dwell, length),
                            $"{LogPrefix} \"{text}\"(계획 잔여 {dwell:F2}초)의 발화 여부가 " +
                            $"`{length}`에서 달라졌습니다 — 노출 배율이 발화 자격 게이트에 새어 " +
                            "들어갔습니다. 그 순간 접근성 손잡이를 끝까지 밀면 대사가 사라집니다" +
                            "(UX_FLOW.md 42-5 확정안 B).");
                        compared++;
                    }
                }
            }

            Debug.Log($"{LogPrefix} 대사 {lines.Count}줄 × 계획 잔여 5점 × 배율 3칸 = {compared}건 " +
                      "비교 — 발화 집합이 전부 동일합니다(컨트롤 하나 = 효과 하나).");
        }

        private static bool Eligible(string text, float dwell, DialogueVisibleLength length)
        {
            AppSettingsModel.SetDialogueVisibleLength(length);
            var line = DialogueLine.Say(text);   // Reaction은 언제나 통과하므로 서술로 잰다(더 빡빡한 쪽).
            return DialogueBudget.IsEligible(line, dwell, DialogueTiming.FadeInSeconds);
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 위 단언이 "아무 일도 안 일어나서" 통과한 것이 아님을 보인다.
        /// <b>만약</b> 게이트에 배율을 태웠다면(= 규칙 8의 필요체류를 <c>페이드인 + m·R</c>로 바꿨다면)
        /// 발화 집합이 <b>실제로 갈라진다</b>. 그 갈라짐을 여기서 직접 계산해 보여 준다.
        /// </summary>
        [Test]
        public void 네거티브_게이트에_배율을_태우면_발화_집합이_실제로_갈라진다()
        {
            List<string> lines = AllLines();
            float top = AppSettingsModel.ScaleOf(DialogueVisibleLength.VeryLong);

            var silenced = new List<string>();
            foreach (string text in lines)
            {
                // 계획 잔여를 "지금은 딱 말할 수 있는" 값으로 잡는다 — 실제 상태들이 서 있는 자리다.
                float dwell = DialogueBudget.RequiredDwellSeconds(text, DialogueTiming.FadeInSeconds);
                float scaledRequired = DialogueTiming.FadeInSeconds
                    + top * DialogueBudget.ReadingSeconds(text);
                if (dwell < scaledRequired) silenced.Add(text);
            }

            Assert.IsNotEmpty(silenced,
                $"{LogPrefix} 게이트에 배율을 태워도 침묵하는 대사가 0건입니다 — 그러면 위의 " +
                "'집합이 동일하다'는 단언은 아무것도 검사하지 않습니다(구조적으로 항상 참).");

            Debug.Log($"{LogPrefix} 네거티브 확인 — 게이트에 배율({top:F2})을 태웠다면 " +
                      $"{silenced.Count}/{lines.Count}줄이 자기 상태에서 침묵했을 것입니다. " +
                      "그래서 배율은 게이트에 곱하지 않습니다.");
        }

        /// <summary>
        /// ★ 구조 잠금 — 게이트 함수가 <b>배율을 받을 자리 자체를 갖지 않는다</b>. 위 두 검사는
        /// 거동을 보지만, 자리가 생기는 순간 언젠가 누가 채운다. 그래서 자리도 함께 잠근다.
        /// </summary>
        [Test]
        public void 게이트_함수는_배율_인자를_받지_않는다()
        {
            MethodInfo required = typeof(DialogueBudget).GetMethod(nameof(DialogueBudget.RequiredDwellSeconds));
            Assert.IsNotNull(required, $"{LogPrefix} RequiredDwellSeconds를 찾지 못했습니다 — 이름이 " +
                "바뀌었다면 이 검사도 함께 고쳐야 합니다.");
            Assert.AreEqual(2, required.GetParameters().Length,
                $"{LogPrefix} RequiredDwellSeconds에 인자가 늘었습니다 — 배율이 게이트로 들어가는 " +
                "문이 열렸습니다(UX_FLOW.md 42-5 확정안 B).");

            MethodInfo eligible = typeof(DialogueBudget).GetMethod(nameof(DialogueBudget.IsEligible));
            Assert.IsNotNull(eligible, $"{LogPrefix} IsEligible을 찾지 못했습니다.");
            Assert.AreEqual(3, eligible.GetParameters().Length,
                $"{LogPrefix} IsEligible에 인자가 늘었습니다 — 같은 문입니다.");
        }

        /// <summary>
        /// ★ 구조 잠금 2 — <b>게이트 호출부</b>가 배율을 한 글자도 모른다. 여기가 실제로 침묵을
        /// 결정하는 두 자리다(<c>DialogueIntent.TryCreate</c> / <c>AmbientChatter.TryRollChatter</c>).
        /// </summary>
        [Test]
        public void 게이트_호출부가_배율을_모른다()
        {
            string[] gateFiles =
            {
                Path.Combine(Application.dataPath, "_Project", "Scripts", "Dialogue", "DialogueIntent.cs"),
                Path.Combine(Application.dataPath, "_Project", "Scripts", "Dialogue", "AmbientChatter.cs"),
            };
            string[] banned = { "VisibleScale", "MinVisibleSecondsFor", "DialogueVisibleLength" };

            foreach (string file in gateFiles)
            {
                Assert.IsTrue(File.Exists(file), $"{LogPrefix} 게이트 호출부를 찾지 못했습니다: {file}");
                string source = File.ReadAllText(file);
                foreach (string token in banned)
                {
                    Assert.IsFalse(source.Contains(token),
                        $"{LogPrefix} {Path.GetFileName(file)}가 \"{token}\"를 참조합니다 — 발화 자격을 " +
                        "정하는 자리가 사용자 취향을 보기 시작했습니다. 규칙 8의 목적은 '번쩍임 노이즈 " +
                        "제거'이지 '이 사용자가 완독 가능한가'가 아닙니다.");
                }
            }
        }

        // ==================================================================================
        // 2. 안전 성질 — 100%는 배율 도입 이전과 한 톨도 다르지 않다
        // ==================================================================================

        [Test]
        public void 기본_칸에서는_배율_도입_이전과_결과가_같다()
        {
            Assert.AreEqual(DialogueBudget.MinVisibleScale,
                AppSettingsModel.ScaleOf(DialogueVisibleLength.Default), 1e-5f);

            foreach (string text in AllLines())
            {
                float before = DialogueBudget.MaxVisibleSecondsFor(text,
                    DialogueTiming.PopInSeconds, DialogueTiming.FadeOutSeconds);
                Assert.AreEqual(before, Cap(text, DialogueBudget.MinVisibleScale), 1e-5f,
                    $"{LogPrefix} \"{text}\"의 상한이 기본 칸에서 달라졌습니다 — 이 라운드가 " +
                    "2026-09-01에 착륙한 거동을 되돌렸다는 뜻입니다.");

                Assert.AreEqual(DialogueBudget.ReadingSeconds(text),
                    DialogueBudget.MinVisibleSecondsFor(text, DialogueBudget.MinVisibleScale), 1e-5f,
                    $"{LogPrefix} \"{text}\"의 화면 최소 노출이 기본 칸에서 가독예산과 달라졌습니다.");
            }
        }

        // ==================================================================================
        // 3. 하한 100% — 규칙 6이 강제한다(취향이 아니다)
        // ==================================================================================

        /// <summary>
        /// 규칙 6 개정 기준: <b>완전 불투명 구간 / 총 노출 ≥ 기준</b>. 배율을 태우면 이 비율은
        /// <c>m·R / (m·R + 팝인)</c>이고 <b>m에 대해 단조 증가</b>한다. 최단 대사의 기본값이 정확히
        /// 그 경계선 위에 서 있어서, 100% 미만은 <b>이미 비준된 규칙을 위반</b>한다.
        ///
        /// <para>기준값을 숫자로 적지 않는다 — <b>기준 자체가 "100%에서의 최단 대사 비율"</b>이므로
        /// 그 자리에서 계산해 쓴다.</para>
        /// </summary>
        [Test]
        public void 사다리의_모든_칸이_규칙6_불투명_비율을_만족한다()
        {
            string shortest = "짧";   // R이 MinSeconds에 걸리는 가장 불리한 경우.
            Assert.AreEqual(DialogueBudget.MinSeconds, DialogueBudget.ReadingSeconds(shortest), 1e-5f,
                $"{LogPrefix} 표본이 가독예산 하한에 걸리지 않습니다(사전 조건).");

            float threshold = OpaqueRatio(shortest, DialogueBudget.MinVisibleScale);

            foreach (DialogueVisibleLength length in Ladder)
            {
                float ratio = OpaqueRatio(shortest, AppSettingsModel.ScaleOf(length));
                Assert.GreaterOrEqual(ratio, threshold - 1e-5f,
                    $"{LogPrefix} `{length}`에서 최단 대사의 완전 불투명 비율이 " +
                    $"{ratio * 100f:F1}%로 기준({threshold * 100f:F1}%) 아래입니다 — 규칙 6 위반입니다.");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>"짧게"라는 칸을 넣을 수 없는 이유</b>가 취향이 아니라 규칙임을 보인다.
        /// 하한 미만(예: 90%)은 실제로 기준을 깬다. 그리고 <see cref="DialogueBudget.ClampVisibleScale"/>이
        /// 그 값을 하한으로 되돌린다.
        /// </summary>
        [Test]
        public void 네거티브_하한_미만은_규칙6을_실제로_위반하고_클램프가_되돌린다()
        {
            string shortest = "짧";
            float threshold = OpaqueRatio(shortest, DialogueBudget.MinVisibleScale);
            float below = DialogueBudget.MinVisibleScale * 0.9f;

            Assert.Less(OpaqueRatio(shortest, below), threshold,
                $"{LogPrefix} 하한 미만(×0.9)인데도 규칙 6 기준을 지킵니다 — 그러면 하한 100%에 " +
                "근거가 없다는 뜻이고, 위 단언은 아무것도 검사하지 않습니다.");

            Assert.AreEqual(DialogueBudget.MinVisibleScale, DialogueBudget.ClampVisibleScale(below), 1e-5f);
            Assert.AreEqual(DialogueBudget.MaxVisibleScale,
                DialogueBudget.ClampVisibleScale(DialogueBudget.MaxVisibleScale * 2f), 1e-5f);
            Assert.AreEqual(DialogueBudget.MinVisibleScale, DialogueBudget.ClampVisibleScale(float.NaN), 1e-5f);
        }

        private static float OpaqueRatio(string text, float scale)
        {
            float opaque = scale * DialogueBudget.ReadingSeconds(text);
            return opaque / (opaque + DialogueTiming.PopInSeconds);
        }

        // ==================================================================================
        // 4. 상한 200% — 포화가 막 시작되는 문턱
        // ==================================================================================

        /// <summary>
        /// 배율을 올리면 언젠가 상한이 <b>상태 지속시간</b>을 넘고, 그 지점부터 서술 대사는
        /// 규칙 4-c ③(상태 종료 시 즉시 컷)에 잡혀 더 이상 길어지지 않는다(= 포화).
        /// <b>포화한 칸은 죽은 칸</b>이다 — 42-1이 잰 결함과 같은 병이 위쪽에서 재발한다.
        ///
        /// <para>그래서 계약은 둘이다: <b>아래 두 칸에는 포화가 없다</b>(칸이 살아 있다) /
        /// <b>상한이 문턱이다</b>(여기가 끝인 이유).</para>
        /// </summary>
        [Test]
        public void 아래_두_칸에는_포화가_없고_상한이_포화_문턱이다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                int atDefault = SaturatedCount(config, AppSettingsModel.ScaleOf(DialogueVisibleLength.Default));
                int atLong = SaturatedCount(config, AppSettingsModel.ScaleOf(DialogueVisibleLength.Long));
                int atVeryLong = SaturatedCount(config, AppSettingsModel.ScaleOf(DialogueVisibleLength.VeryLong));

                Assert.AreEqual(0, atDefault,
                    $"{LogPrefix} `기본`에서 이미 포화한 대사가 {atDefault}줄 있습니다 — 첫 칸부터 죽어 있습니다.");
                Assert.AreEqual(0, atLong,
                    $"{LogPrefix} `길게`에서 포화한 대사가 {atLong}줄 있습니다 — 가운데 칸이 부분적으로 " +
                    "죽어 있다는 뜻입니다.");
                Assert.Greater(atVeryLong, 0,
                    $"{LogPrefix} `아주 길게`에서 포화가 하나도 시작되지 않았습니다 — 그러면 상한을 " +
                    "200%로 끊은 근거(포화 문턱)가 사라지고, 더 올릴 수 있다는 뜻이 됩니다.");

                Debug.Log($"{LogPrefix} 포화 실측 — 기본 {atDefault}줄 / 길게 {atLong}줄 / " +
                          $"아주 길게 {atVeryLong}줄. 상한은 포화가 막 시작되는 자리입니다.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>더 올리면 병이 위쪽에서 재발한다</b>. 클램프를 우회해 상한의 두 배를
        /// 넣어 보면 포화가 실제로 늘어난다(= 칸을 옮겼는데 화면이 안 바뀌는 대사가 늘어난다).
        /// </summary>
        [Test]
        public void 네거티브_상한을_더_올리면_포화가_늘어난다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                int atCeiling = SaturatedCount(config, DialogueBudget.MaxVisibleScale);
                int beyond = SaturatedCount(config, DialogueBudget.MaxVisibleScale * 1.5f, bypassClamp: true);

                Assert.Greater(beyond, atCeiling,
                    $"{LogPrefix} 상한을 1.5배로 올려도 포화가 {atCeiling} -> {beyond}로 늘지 않습니다 — " +
                    "그러면 '여기가 문턱'이라는 판정에 근거가 없고, 위 단언은 아무것도 검사하지 않습니다.");

                Debug.Log($"{LogPrefix} 네거티브 확인 — 상한 ×1.5에서 포화 {atCeiling} -> {beyond}줄. " +
                          "그래서 범위의 끝은 200%입니다.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        /// <summary>상태 지속시간(배회 최장)을 넘어 <b>더 이상 길어질 수 없는</b> 대사의 수.</summary>
        private static int SaturatedCount(StickConfig config, float scale, bool bypassClamp = false)
        {
            int count = 0;
            count += SaturatedIn(AmbientLines("WalkLines"), config.wanderWalkDurationMax, scale, bypassClamp);
            count += SaturatedIn(AmbientLines("IdleLines"), config.wanderIdleDurationMax, scale, bypassClamp);
            return count;
        }

        private static int SaturatedIn(string[] lines, float stateMaxSeconds, float scale, bool bypassClamp)
        {
            int count = 0;
            foreach (string text in lines)
            {
                float cap = bypassClamp
                    ? DialogueTiming.PopInSeconds + 2f * scale * DialogueBudget.ReadingSeconds(text)
                      + DialogueTiming.FadeOutSeconds
                    : Cap(text, scale);
                if (cap > stateMaxSeconds) count++;
            }
            return count;
        }

        // ==================================================================================
        // 5. 세 칸이 서로 구분된다
        // ==================================================================================

        /// <summary>
        /// 손잡이의 칸이 <b>육안으로 구분</b>되지 않으면 이 라운드는 죽은 칸을 다른 모양으로 다시 만든
        /// 것뿐이다. 기준은 <b>페이드아웃</b>이다 — 그보다 작은 차이는 사라지는 연출 안에 묻힌다.
        /// </summary>
        [Test]
        public void 세_칸의_화면_결과가_서로_뚜렷이_다르다()
        {
            foreach (string text in AllLines())
            {
                for (int i = 1; i < Ladder.Length; i++)
                {
                    float prev = Cap(text, AppSettingsModel.ScaleOf(Ladder[i - 1]));
                    float next = Cap(text, AppSettingsModel.ScaleOf(Ladder[i]));
                    Assert.Greater(next - prev, DialogueTiming.FadeOutSeconds,
                        $"{LogPrefix} \"{text}\"에서 `{Ladder[i - 1]}`({prev:F2}초)와 " +
                        $"`{Ladder[i]}`({next:F2}초)의 차이가 페이드아웃" +
                        $"({DialogueTiming.FadeOutSeconds:F2}초) 이하입니다 — 칸을 옮겨도 사라지는 " +
                        "연출 안에 묻혀 사용자가 구분할 수 없습니다.");
                }
            }
        }

        /// <summary>사다리 값 자체가 서로 다르고 오름차순인가(위 검사들의 사전 조건).</summary>
        [Test]
        public void 사다리는_오름차순이고_양_끝이_유도된_값이다()
        {
            Assert.AreEqual(DialogueBudget.MinVisibleScale,
                AppSettingsModel.ScaleOf(DialogueVisibleLength.Default), 1e-5f,
                "첫 칸은 규칙 6이 강제한 하한이어야 한다.");
            Assert.AreEqual(DialogueBudget.MaxVisibleScale,
                AppSettingsModel.ScaleOf(DialogueVisibleLength.VeryLong), 1e-5f,
                "끝 칸은 포화 문턱이어야 한다.");

            // 가운데는 고른 값이 아니라 산술이다 — 양 끝이 확정되면 유도된다.
            Assert.AreEqual((DialogueBudget.MinVisibleScale + DialogueBudget.MaxVisibleScale) * 0.5f,
                AppSettingsModel.ScaleOf(DialogueVisibleLength.Long), 1e-5f);

            for (int i = 1; i < Ladder.Length; i++)
            {
                Assert.Greater(AppSettingsModel.ScaleOf(Ladder[i]), AppSettingsModel.ScaleOf(Ladder[i - 1]),
                    "사다리가 오름차순이 아니면 세그먼트 칸 순서와 효과 순서가 갈린다.");
            }
        }
    }
}
