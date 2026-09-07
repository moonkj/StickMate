using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>시각 오버레이 렌더러의 획 하한</b> — 몸이 지키는 2.00pt를 과녁·코스튬 프롭도 지킨다
    /// (2026-09-08, design-equipment 신고 + 리더 실측).
    ///
    /// ============================================================================
    /// 무엇이 출하되고 있었나
    /// ============================================================================
    /// 몸·액세서리·FX·펫은 전부 <see cref="StickmanAgent.MinStrokeWorldWidth"/>를 물고 있는데
    /// <c>ArcheryRenderer</c>와 <c>CostumePropRenderer</c>만 <b>순수 비례</b>였다. 배율 0.35에서
    /// <b>0.951pt</b> — 하한의 절반 미만이고, <b>Windows 100%(1×)에서 device pixel 1개 미만</b>이라
    /// 선이 아예 사라질 수 있다. Windows가 1차 출시 플랫폼이다.
    ///
    /// ============================================================================
    /// ★ 이 파일이 숫자를 베끼지 않는 방법
    /// ============================================================================
    /// 하한도 비율도 <b>프로덕션 상수를 참조</b>한다(<see cref="StickConfig.MinStrokeScreenPoints"/> ·
    /// <see cref="StickConfig.ReferencePointsPerWorldUnitApprox"/> ·
    /// <see cref="StickConfig.BaselineCharacterTotalHeight"/> · <see cref="StickConfig.MinCharacterScale"/>).
    /// 획 비율만은 소스에서 <b>읽어 온다</b> — 그 값이 두 렌더러의 <c>private const</c>라
    /// 참조할 수 없고, 숫자로 베끼면 비율이 바뀌는 날 이 검사가 <b>조용히 옛 값을 지킨다</b>.
    ///
    /// <para><b>양성 대조가 이 파일의 절반이다</b>: 하한을 뺀 「옛 식」을 같은 테스트 안에서 돌려
    /// <b>실제로 미달이 나오는지</b> 먼저 보인다. 안 보이면 아래 「통과」는
    /// 「검사가 아무것도 안 한다」와 구별되지 않는다.</para>
    ///
    /// <para>소스 스캔 + 순수 산술이라 씬도 플랫폼도 필요 없다.</para>
    /// </summary>
    public sealed class OverlayStrokeFloorTests
    {
        private const string LogPrefix = "[획하한]";

        /// <summary>하한(월드 유닛) — 값의 단일 소스에서 파생시킨다.
        /// <c>StickmanAgent.MinStrokeWorldWidth</c>가 에이전트 없이 쓰는 폴백과 <b>같은 식</b>이다.</summary>
        private static float FloorWorld =>
            StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        private static float Points(float world) => world * StickConfig.ReferencePointsPerWorldUnitApprox;

        private static string ReadRenderer(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction", fileName);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        /// <summary><c>private const float StrokeWidthRatio = 0.0339f;</c>에서 숫자만 뽑는다.
        /// <b>존재 단언</b>이다 — 못 찾으면 조용히 통과하는 대신 여기서 실패한다.</summary>
        private static float ReadStrokeRatio(string source, string fileName)
        {
            const string key = "private const float StrokeWidthRatio = ";
            int i = source.IndexOf(key, StringComparison.Ordinal);
            Assert.Greater(i, 0,
                $"{LogPrefix} {fileName}에서 StrokeWidthRatio 선언을 찾지 못했다 — 이름이나 형태가 " +
                "바뀌었으면 이 파서를 고쳐라. 못 찾은 채 통과시키면 이 대조가 빈 검사가 된다.");
            int start = i + key.Length;
            int end = source.IndexOf('f', start);
            Assert.Greater(end, start, $"{LogPrefix} {fileName}의 StrokeWidthRatio 값을 읽지 못했다.");
            Assert.IsTrue(float.TryParse(source.Substring(start, end - start).Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float ratio),
                $"{LogPrefix} {fileName}의 StrokeWidthRatio를 숫자로 읽지 못했다.");
            Assert.Greater(ratio, 0f, $"{LogPrefix} {fileName}의 획 비율이 0 이하다.");
            return ratio;
        }

        /// <summary>배율 전 구간(하한 ~ 상한)을 0.05 눈금으로 훑는다. 눈금은 다이얼과 같다.</summary>
        private static float[] ScaleSweep()
        {
            var scales = new System.Collections.Generic.List<float>();
            for (float s = StickConfig.MinCharacterScale; s <= StickConfig.MaxCharacterScale + 1e-4f; s += 0.05f)
                scales.Add(Mathf.Min(s, StickConfig.MaxCharacterScale));
            return scales.ToArray();
        }

        [Test]
        public void 양성대조_하한이_없으면_실제로_미달이_난다()
        {
            float ratio = ReadStrokeRatio(ReadRenderer("ArcheryRenderer.cs"), "ArcheryRenderer.cs");
            float[] scales = ScaleSweep();
            Assert.IsNotEmpty(scales, $"{LogPrefix} 배율 표본이 비었다 — 아래 검사가 아무것도 안 잰다.");

            int below = 0;
            float worst = float.PositiveInfinity;
            foreach (float s in scales)
            {
                float bare = StickConfig.BaselineCharacterTotalHeight * s * ratio;   // 옛 식(하한 없음)
                worst = Mathf.Min(worst, bare);
                if (bare < FloorWorld) below++;
            }

            Assert.Greater(below, 0,
                $"{LogPrefix} ★ 양성 대조 실패 — 하한 없는 식이 <b>어느 배율에서도</b> 미달이 안 난다. " +
                "그렇다면 아래 「하한을 지킨다」는 검사는 아무것도 증명하지 못한다(비율이나 하한이 " +
                "바뀌어 이 검사의 전제가 사라졌는지 먼저 확인하라).");
            Assert.Less(Points(worst), StickConfig.MinStrokeScreenPoints,
                $"{LogPrefix} 최악 배율의 옛 두께가 하한 미만이어야 한다.");

            Debug.Log($"{LogPrefix} ① 양성 대조 통과 — 배율 {scales.Length}개 중 {below}개가 하한 미달, " +
                      $"최악 {Points(worst):F3}pt (하한 {StickConfig.MinStrokeScreenPoints:F2}pt).");
        }

        [Test]
        public void 두_오버레이_렌더러가_하한을_실제로_문다()
        {
            string[] files = { "ArcheryRenderer.cs", "CostumePropRenderer.cs" };
            foreach (string file in files)
            {
                string src = ReadRenderer(file);

                // 하한의 단일 소스를 실제로 읽는가. **존재 단언**이라 썩으면 조용히 초록이 되지 않는다.
                StringAssert.Contains("MinStrokeWorldWidth", src,
                    $"{LogPrefix} {file}이 StickmanAgent.MinStrokeWorldWidth를 안 읽는다 — " +
                    "하한 값을 자기 파일에 다시 적으면 몸과 어긋나는 날 아무도 모른다.");
                StringAssert.Contains("MinStrokeScreenPoints", src,
                    $"{LogPrefix} {file}에 에이전트 없는 경로의 폴백이 없다 — 0을 흘리면 하한이 조용히 사라진다.");
                StringAssert.Contains("Mathf.Max(", src,
                    $"{LogPrefix} {file}에 하한을 무는 자리가 없다.");

                // 그리고 그 하한이 **실제 두께 계산**과 맞물리는지 산술로 확인한다.
                float ratio = ReadStrokeRatio(src, file);
                int lifted = 0;
                foreach (float s in ScaleSweep())
                {
                    float drawn = Mathf.Max(StickConfig.BaselineCharacterTotalHeight * s * ratio, FloorWorld);
                    Assert.GreaterOrEqual(Points(drawn), StickConfig.MinStrokeScreenPoints - 0.001f,
                        $"{LogPrefix} {file}: 배율 {s:F2}에서 그려지는 두께가 {Points(drawn):F3}pt로 " +
                        $"하한 {StickConfig.MinStrokeScreenPoints:F2}pt 아래다.");
                    if (StickConfig.BaselineCharacterTotalHeight * s * ratio < FloorWorld) lifted++;
                }
                Debug.Log($"{LogPrefix} {file} — 하한이 물린 배율 {lifted}개.");
            }
            Debug.Log($"{LogPrefix} ② 통과 — 렌더러 {files.Length}개가 전 배율에서 하한 이상.");
        }

        /// <summary>
        /// ★ <b>기본 배율에서 그림이 바뀌면 그건 회귀다.</b> 하한은 작은 배율에서만 물려야 한다.
        /// </summary>
        [Test]
        public void 출하_기본_배율에서는_하한이_물리지_않는다()
        {
            float ratio = ReadStrokeRatio(ReadRenderer("ArcheryRenderer.cs"), "ArcheryRenderer.cs");
            var config = ScriptableObject.CreateInstance<StickConfig>();
            float shipped;
            try { shipped = config.characterScale; }
            finally { UnityEngine.Object.DestroyImmediate(config); }

            float geometric = StickConfig.BaselineCharacterTotalHeight * shipped * ratio;
            Assert.GreaterOrEqual(geometric, FloorWorld,
                $"{LogPrefix} 출하 기본 배율 {shipped:F2}에서 하한이 물린다 " +
                $"({Points(geometric):F3}pt vs 하한 {StickConfig.MinStrokeScreenPoints:F2}pt) — " +
                "그러면 지금 사용자가 보는 그림이 이 수정으로 굵어진다. 그건 회귀다.");

            float crossover = FloorWorld / (StickConfig.BaselineCharacterTotalHeight * ratio);
            Assert.Less(crossover, shipped,
                $"{LogPrefix} 교차 배율 {crossover:F4}가 출하 기본 {shipped:F2} 이상이다.");
            Debug.Log($"{LogPrefix} ③ 통과 — 교차 배율 {crossover:F4} < 출하 기본 {shipped:F2}. " +
                      $"기본에서 {Points(geometric):F3}pt로 하한 위(여유 {100f * (geometric / FloorWorld - 1f):F1}%).");
        }
    }
}
