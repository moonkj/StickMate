using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <see cref="UiChrome.EdgeOnSurface(Color)"/> — <b>테두리의 세 번째 문</b> 회귀 잠금.
    /// 근거 문서: <c>docs/UI_ALPHA_BLEED_POLICY.md</c> §4-3 / §4-3-a / §4-3-b / §4-3-c
    /// (design-art, 2026-09-06 R14-b 개정판).
    ///
    /// ============================================================================
    /// 무엇이 있었나 — 결함 세 겹
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>방식</b>: 테두리는 「흰색에 α를 얹는다」로 그려졌고, 그 방식은 밝은 바탕에서
    ///     <b>원리상</b> 실패한다(종이 무대 1.02 / 흰 견본 1.00). 흰색 위에 흰색을 더 얹을 수 없다.</item>
    ///   <item><b>목표</b>(§4-3-a): 초판이 하한 3.0을 그대로 목표로 썼다. 부동소수로는 통과하지만
    ///     <b>8비트로 반올림하면 회색 램프의 40.6 % · 유채색의 37.7 %가 미달</b>이었다.
    ///     선언 바탕 17종만 보면 7종이라 「예외」로 보이는데, 전 색공간에서는
    ///     <b>가능한 바탕의 약 40 %에서 정책이 성립하지 않는다</b>는 뜻이다.</item>
    ///   <item><b>방향</b>(§4-3-b): 초판이 「흰쪽 우선」이라 <b>절벽이 target에 딸려 움직였다</b>.
    ///     마진 상수를 1.20으로 고르는 것만으로 ΔL* 2 이내인 두 크롬 면이 정반대 색 테두리를 받는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 파일이 지키는 규율
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>★ 8비트 반올림 후에 잰다.</b> 부동소수로만 재면 (2)를 못 본다 — 그게 이번 회차가
    ///     잡은 것 그 자체다(§4-3-c 말미, design-art 필수 조항).</item>
    ///   <item><b>★ 표본이 아니라 모집단을 잰다.</b> 회색 램프 256단 <b>전수</b> + 유채색 4096격자.
    ///     선언 바탕 17종만 보면 40 %짜리 구멍이 7종으로 보인다.</item>
    ///   <item><b>계약</b>(목표 달성 · 최소성 · α=1 · 방향)은 <b>프로덕션 상수에서 파생</b>해서 잰다.</item>
    ///   <item><b>인계표 검산</b>(§4-3-c의 17행)은 <b>따로</b> 잰다. 이건 값을 베끼는 게 맞다 —
    ///     design-art의 표와 이 코드가 <b>서로 다른 두 계산</b>임을 증명하는 것이 목적이기 때문이다.
    ///     이 테스트가 빨개지면 코드가 아니라 <b>팔레트가 움직인</b> 것일 수 있다.</item>
    ///   <item><b>네거티브 컨트롤이 절반이다.</b> 개정 ①·② 각각에 대해 <b>옛 규칙이 실제로 깨지는지</b>를
    ///     같은 파일에서 단언한다. 그게 없으면 이 초록은 아무것도 증명하지 않는다.</item>
    /// </list>
    ///
    /// <para><b>화면 픽셀은 여기서 못 잰다.</b> 이 파일은 값(토큰) 층만 본다.</para>
    /// </summary>
    public sealed class EdgeOnSurfaceTests
    {
        private const string LogPrefix = "[테두리-TEST]";

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>8비트로 <b>양자화된</b> 색. ★ 화면에 실제로 나가는 값은 이것이고,
        /// <b>하한은 여기서 서야 한다</b>(§4-3-c 필수 조항).</summary>
        private static Color Q8(Color c)
        {
            ColorUtility.TryParseHtmlString(Hex(c), out Color q);
            return q;
        }

        /// <summary>8비트 반올림 후 대비 — 이 파일의 기본 자다.</summary>
        private static float Q8Contrast(Color edge, Color backdrop)
            => UiChrome.ContrastRatio(Q8(edge), Q8(backdrop));

        private static Color Gray(int v) => new Color(v / 255f, v / 255f, v / 255f, 1f);

        /// <summary><c>CharacterPortraitStage.ResolveBackdropColor</c>가 돌려주는 무대색.
        /// 값을 베끼지 않고 그 함수에서 직접 받는다 — 무대색 결정은 거기 한 곳에만 있다.</summary>
        private static Color Stage(StickMate.Core.StickmanInkColor ink)
        {
            var config = ScriptableObject.CreateInstance<StickMate.Core.StickConfig>();
            try
            {
                config.SetRuntimeInkColor(ink);
                return CharacterPortraitStage.ResolveBackdropColor(config);
            }
            finally { Object.DestroyImmediate(config); }
        }

        private static Color CharcoalStage() => Stage(StickMate.Core.StickmanInkColor.White);
        private static Color PaperStage() => Stage(StickMate.Core.StickmanInkColor.Black);

        /// <summary>이 앱에 실재하거나 실재할 수 있는 바탕 전부 + 극단값.</summary>
        private static IEnumerable<(string name, Color color)> AllBackdrops()
        {
            foreach (Color c in UiChrome.TextBackdrops) yield return ("TextBackdrops " + Hex(c), c);
            foreach (Color c in UiChrome.RaisedTextBackdrops) yield return ("RaisedTextBackdrops " + Hex(c), c);
            foreach (Color c in UiChrome.BrightTextBackdrops) yield return ("BrightTextBackdrops " + Hex(c), c);

            yield return ("PortraitSurface", UiChrome.PortraitSurface);
            yield return ("ThumbSurfaceLocked", UiChrome.ThumbSurfaceLocked);
            yield return ("InkContrastCharcoal", UiChrome.InkContrastCharcoal);
            yield return ("CardActionSurface", UiChrome.CardActionSurface);
            yield return ("ControlFace on Panel", UiChrome.ControlFaceOnSurface(UiChrome.PanelSurface));
            yield return ("목탄 무대", CharcoalStage());
            yield return ("종이 무대", PaperStage());
            yield return ("순백", Color.white);
            yield return ("순흑", Color.black);
            yield return ("중간회색 #808080", Gray(128));
        }

        // ================================================================================
        // 1. ★ 하한은 8비트에서 선다 — 표본이 아니라 모집단으로
        // ================================================================================

        /// <summary>선언된 바탕 전부에서 <b>8비트 반올림 후</b> WCAG 하한을 넘는가.</summary>
        [Test]
        public void 선언된_바탕에서_8비트_반올림_후에도_하한을_넘는다()
        {
            var failures = new List<string>();
            float worst = float.MaxValue;

            foreach ((string name, Color bd) in AllBackdrops())
            {
                Color edge = UiChrome.EdgeOnSurface(bd);
                float q = Q8Contrast(edge, bd);
                worst = Mathf.Min(worst, q);
                if (q < UiChrome.MinNonTextContrast) failures.Add($"{name} {Hex(bd)} -> {Hex(edge)} = {q:F4}");
                Debug.Log($"{LogPrefix} {name} {Hex(bd)} -> {Hex(edge)} q8={q:F4}");
            }

            Debug.Log($"{LogPrefix} 선언 바탕 최소(q8) = {worst:F4}");
            Assert.IsEmpty(failures, $"8비트 반올림 후 하한 미달: {string.Join(" / ", failures)}");
        }

        /// <summary>
        /// ★ <b>회색 램프 256단 전수.</b> 17종 표본으로는 부족하다는 것이 이번 회차의 결론이다 —
        /// 목표 3.0에서는 여기서 <b>104단(40.6 %)</b>이 무너졌고 17종만 보면 7종으로 보였다.
        /// </summary>
        [Test]
        public void 회색_램프_256단_전수에서_8비트_후에도_하한을_넘는다()
        {
            var failures = new List<string>();
            float worst = float.MaxValue; int worstAt = -1;

            for (int v = 0; v <= 255; v++)
            {
                Color bd = Gray(v);
                float q = Q8Contrast(UiChrome.EdgeOnSurface(bd), bd);
                if (q < worst) { worst = q; worstAt = v; }
                if (q < UiChrome.MinNonTextContrast) failures.Add($"gray{v}={q:F4}");
            }

            Debug.Log($"{LogPrefix} 회색 256단 최소(q8) = {worst:F4} @ gray{worstAt}");
            Assert.IsEmpty(failures,
                $"회색 램프에서 {failures.Count}단 미달: {string.Join(" ", failures)}");
            Assert.GreaterOrEqual(worst, UiChrome.MinNonTextContrast);
        }

        /// <summary>유채색 4096격자(각 채널 16단) — 휘도가 채널마다 다르게 실리므로 회색만으로는 부족하다.</summary>
        [Test]
        public void 유채색_4096격자에서_8비트_후에도_하한을_넘는다()
        {
            int failed = 0; float worst = float.MaxValue; Color worstBd = Color.black;

            for (int r = 0; r < 16; r++)
                for (int g = 0; g < 16; g++)
                    for (int b = 0; b < 16; b++)
                    {
                        Color bd = new Color(r * 17 / 255f, g * 17 / 255f, b * 17 / 255f, 1f);
                        float q = Q8Contrast(UiChrome.EdgeOnSurface(bd), bd);
                        if (q < worst) { worst = q; worstBd = bd; }
                        if (q < UiChrome.MinNonTextContrast) failed++;
                    }

            Debug.Log($"{LogPrefix} 유채 4096격자 최소(q8) = {worst:F4} @ {Hex(worstBd)}");
            Assert.Zero(failed, $"유채색 격자에서 {failed}색 미달(최악 {Hex(worstBd)} = {worst:F4}).");
        }

        /// <summary>
        /// ★★ <b>개정 ①의 네거티브 컨트롤</b> — 옛 기본값 3.0이 <b>실제로</b> 무너지는가.
        /// 이게 없으면 위 세 초록은 "원래부터 통과했을지도" 모른다.
        /// <para>design-art 실측(§4-3-a): 회색 256단 중 <b>104단</b>, 최악 <c>#121212</c> <b>2.9790</b>.</para>
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_옛_목표_3점0은_회색_램프의_40퍼센트에서_무너진다()
        {
            int failed = 0; float worst = float.MaxValue; int worstAt = -1;

            for (int v = 0; v <= 255; v++)
            {
                Color bd = Gray(v);
                float q = Q8Contrast(UiChrome.EdgeOnSurface(bd, UiChrome.MinNonTextContrast), bd);
                if (q < worst) { worst = q; worstAt = v; }
                if (q < UiChrome.MinNonTextContrast) failed++;
            }

            Debug.Log($"{LogPrefix} [네거티브] target=3.0 회색 미달 {failed}/256, 최악 {worst:F4} @ gray{worstAt}");

            Assert.Greater(failed, 100,
                "옛 목표 3.0이 8비트에서 안 깨진다 — 개정 ①의 전제가 틀렸거나 자가 틀렸다.");
            Assert.Less(worst, UiChrome.MinNonTextContrast);

            // 그리고 채택된 목표는 같은 자에서 0건이다(대조).
            Assert.GreaterOrEqual(Q8Contrast(UiChrome.EdgeOnSurface(Gray(worstAt)), Gray(worstAt)),
                UiChrome.MinNonTextContrast,
                "채택 목표에서도 같은 자리가 무너진다면 ×1.05는 아무것도 고치지 못한 것이다.");
        }

        /// <summary>채택 배수가 <b>실측 최악 손실보다 넉넉한가</b> — 상수를 베끼지 않고 관계로 잠근다.
        /// (§4-3-a: 실측 최악 손실 0.699 %, ×1.05의 여유 +4.37 % = 6.2배)</summary>
        [Test]
        public void 채택_목표는_하한보다_위이고_면_목표보다_아래다()
        {
            Assert.Greater(UiChrome.EdgeContrastTarget, UiChrome.MinNonTextContrast,
                "테두리 목표가 하한과 같으면 8비트에서 하한이 안 선다(개정 ①의 전부다).");
            Assert.Less(UiChrome.EdgeContrastTarget, UiChrome.ControlFaceContrastTarget,
                "테두리 목표가 면 목표(×1.20) 이상이면 「면이 있는 것」과 「테두리만 있는 것」이 " +
                "같은 단으로 읽힌다(§4-3-a 근거 2 — 위계가 뭉개진다).");
        }

        // ================================================================================
        // 2. ★ 방향 — 절벽은 한 점에 고정되고 target과 무관하다 (개정 ②)
        // ================================================================================

        /// <summary>회색 램프에서 <b>처음으로 어두운 테두리가 나오는 칸</b>. 방향이 뒤집히는 절벽이다.</summary>
        private static int FlipIndex(float target)
        {
            for (int v = 0; v <= 255; v++)
            {
                Color bd = Gray(v);
                if (UiChrome.RelativeLuminance(UiChrome.EdgeOnSurface(bd, target))
                    < UiChrome.RelativeLuminance(bd)) return v;
            }
            return -1;
        }

        /// <summary>
        /// ★★ <b>개정 ②의 본체</b>. 절벽 위치가 <paramref name="target"/>과 <b>무관</b>한가.
        /// <para>초판(「흰쪽 우선」)에서는 절벽 조건이 <c>target ≤ 1.05/(L+0.05)</c>라 목표를 조정하는
        /// 것만으로 테두리 색이 흰↔검으로 뒤집혔다. 「여유 큰 쪽」에서는 두 최대가 같아지는
        /// <b>한 점</b>에서만 갈리고 그 점에 target이 안 들어간다.</para>
        /// <para>★ 이 테스트는 프로덕션 판정식을 <b>베끼지 않는다</b> — 서로 다른 네 목표에서
        /// 관측된 절벽이 같은 칸인지만 본다. 판정식을 베끼면 같은 함정에 같이 빠진다.</para>
        /// </summary>
        [Test]
        public void 방향_절벽은_목표값과_무관하게_한_칸에_고정된다()
        {
            float[] targets =
            {
                UiChrome.MinNonTextContrast,          // 3.00 — 초판 기본값
                UiChrome.EdgeContrastTarget,          // 3.15 — 채택값
                UiChrome.ControlFaceContrastTarget,   // 3.60 — 자매 함수의 마진
                UiChrome.MinTextContrast,             // 4.50 — 글자 하한(√21 아래라 여전히 풀린다)
            };

            int expected = FlipIndex(targets[0]);
            Assert.Greater(expected, 0, "절벽을 못 찾았다 — 회색 램프 전체가 한 방향이라는 뜻이다.");

            foreach (float t in targets)
            {
                Assert.AreEqual(expected, FlipIndex(t),
                    $"target {t:F2}에서 절벽이 옮겨졌다 — 방향이 마진 상수에 딸려 움직인다(개정 ② 회귀).");
            }
            Debug.Log($"{LogPrefix} 절벽 = gray{expected} (target 4종에서 동일)");
        }

        /// <summary>
        /// 절벽을 <b>양쪽에서 1/255씩</b> 찌른다 — 바로 앞 칸은 밝은 쪽, 절벽 칸은 어두운 쪽이어야 하고
        /// 그 경계가 네 목표에서 모두 같아야 한다(§9-2 필수 조항).
        /// <para>그리고 그 경계가 해석해 <c>L* = √(1.05 × 0.05) − 0.05</c>를 감싸는지 확인한다.
        /// 1.05와 0.05는 WCAG 대비식 <c>(L1+0.05)/(L2+0.05)</c>에서 그대로 온 값이고, 흰쪽 최대
        /// <c>1.05/(L+0.05)</c>와 검은쪽 최대 <c>(L+0.05)/0.05</c>가 같아지는 점이 L*다.</para>
        /// </summary>
        [Test]
        public void 절벽_양쪽_한_칸씩_찔러도_방향이_바뀌지_않는다()
        {
            int flip = FlipIndex(UiChrome.EdgeContrastTarget);
            Assert.Greater(flip, 0);

            Color below = Gray(flip - 1);
            Color at = Gray(flip);

            foreach (float t in new[]
            {
                UiChrome.MinNonTextContrast, UiChrome.EdgeContrastTarget,
                UiChrome.ControlFaceContrastTarget, UiChrome.MinTextContrast,
            })
            {
                Assert.Greater(UiChrome.RelativeLuminance(UiChrome.EdgeOnSurface(below, t)),
                    UiChrome.RelativeLuminance(below),
                    $"target {t:F2}: 절벽 바로 아래 칸(gray{flip - 1})이 어두운 쪽으로 갔다.");
                Assert.Less(UiChrome.RelativeLuminance(UiChrome.EdgeOnSurface(at, t)),
                    UiChrome.RelativeLuminance(at),
                    $"target {t:F2}: 절벽 칸(gray{flip})이 밝은 쪽으로 갔다.");
            }

            float lStar = Mathf.Sqrt(1.05f * 0.05f) - 0.05f;
            float lBelow = UiChrome.RelativeLuminance(below);
            float lAt = UiChrome.RelativeLuminance(at);
            Debug.Log($"{LogPrefix} L* = {lStar:F6} / L(gray{flip - 1}) = {lBelow:F6} / L(gray{flip}) = {lAt:F6}");

            Assert.Less(lBelow, lStar, "관측된 절벽이 해석해 L*보다 위에 있다.");
            Assert.GreaterOrEqual(lAt, lStar, "관측된 절벽이 해석해 L*보다 아래에 있다.");
        }

        /// <summary>
        /// ★★ <b>개정 ②의 네거티브 컨트롤</b> — 옛 「흰쪽 우선」 규칙이 <b>실제로</b> 두 이웃 면을
        /// 정반대로 가르는가. §4-3-b의 K-4가 잰 그 상황을 여기서 재현한다.
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_흰쪽_우선_규칙은_이웃한_두_크롬_면을_정반대로_가른다()
        {
            Color chrome = UiChrome.ChromeButtonSurface;                       // #898B8E
            Color onCard = UiChrome.CardActionSurface;                         // #838589
            float margin = UiChrome.ControlFaceContrastTarget;                 // 1.20 배수

            // 옛 규칙: 흰쪽 해가 있으면 무조건 흰쪽.
            bool legacyChromeUp = LegacyWhiteFirstGoesUp(chrome, margin);
            bool legacyCardUp = LegacyWhiteFirstGoesUp(onCard, margin);

            Debug.Log($"{LogPrefix} [네거티브] 흰쪽 우선 @×1.20 — " +
                $"{Hex(chrome)} {(legacyChromeUp ? "흰" : "검")} / {Hex(onCard)} {(legacyCardUp ? "흰" : "검")}");

            Assert.AreNotEqual(legacyChromeUp, legacyCardUp,
                "옛 규칙이 두 면을 같은 방향으로 보낸다 — 개정 ②의 전제(절벽이 target에 딸려 움직인다)가 " +
                "재현되지 않았다. 전제가 틀렸거나 이 자가 틀렸다.");

            // 두 면은 사실상 같은 밝기다 — 그래서 위 갈림이 결함이다.
            float dL = Mathf.Abs(UiChrome.RelativeLuminance(chrome) - UiChrome.RelativeLuminance(onCard));
            Assert.Less(dL, 0.03f, $"두 면의 휘도차가 {dL:F4}로 크다 — 「이웃한 두 면」이라는 전제가 깨졌다.");

            // 지금 규칙은 둘을 같은 쪽(어두운 쪽)으로 보낸다.
            Assert.Less(UiChrome.RelativeLuminance(UiChrome.EdgeOnSurface(chrome)),
                UiChrome.RelativeLuminance(chrome));
            Assert.Less(UiChrome.RelativeLuminance(UiChrome.EdgeOnSurface(onCard)),
                UiChrome.RelativeLuminance(onCard));
        }

        /// <summary>옛 「흰쪽 우선」 판정식. <b>프로덕션에는 없다</b> — 네거티브 컨트롤 전용이다.</summary>
        private static bool LegacyWhiteFirstGoesUp(Color backdrop, float target)
            => target * (UiChrome.RelativeLuminance(backdrop) + 0.05f) - 0.05f <= 1f;

        /// <summary>밝은 크롬 면에서 잉크와 테두리가 <b>같은 방향</b>인가 — 개정 ②가 노린 결과.
        /// 그 면들은 <see cref="UiChrome.BrightTextBackdrops"/>라 잉크가 어두운 쪽으로 뒤집힌다.</summary>
        [Test]
        public void 밝은_면에서_잉크와_테두리가_같은_방향을_본다()
        {
            foreach (Color face in UiChrome.BrightTextBackdrops)
            {
                float faceL = UiChrome.RelativeLuminance(face);
                float inkL = UiChrome.RelativeLuminance(
                    UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled: true));
                float edgeL = UiChrome.RelativeLuminance(UiChrome.EdgeOnSurface(face));

                Assert.AreEqual(inkL < faceL, edgeL < faceL,
                    $"{Hex(face)}: 잉크는 {(inkL < faceL ? "어두운" : "밝은")} 쪽인데 테두리는 " +
                    $"{(edgeL < faceL ? "어두운" : "밝은")} 쪽이다 — 같은 칩에서 두 요소가 반대 방향이다.");
            }
        }

        // ================================================================================
        // 3. 계약 — α=1 · 최소성
        // ================================================================================

        [Test]
        public void 반환값은_언제나_불투명하다()
        {
            foreach ((string name, Color bd) in AllBackdrops())
            {
                Assert.AreEqual(1f, UiChrome.EdgeOnSurface(bd).a, 1e-6f,
                    $"{name}에서 α<1이 나왔다 — 그 화소만큼 유저의 바탕화면이 비친다(파일 머리 「알파 채널의 법칙」).");
            }
            for (int v = 0; v <= 255; v++)
                Assert.AreEqual(1f, UiChrome.EdgeOnSurface(Gray(v)).a, 1e-6f, $"gray{v}");
        }

        /// <summary>「<b>최소</b> 혼합」이 실제로 최소인가 — 한 계단 덜 섞으면 목표를 못 넘어야 한다.
        /// 이게 없으면 "그냥 순백/순검을 돌려주는" 구현도 다른 테스트를 전부 통과한다.</summary>
        [Test]
        public void 목표를_넘기는_최소_혼합이다()
        {
            const int Steps = 1024;   // 프로덕션 격자와 같은 폭. 다르면 아래 역산이 성립하지 않는다.

            foreach ((string name, Color bd) in AllBackdrops())
            {
                Color edge = UiChrome.EdgeOnSurface(bd);
                bool up = UiChrome.RelativeLuminance(edge) >= UiChrome.RelativeLuminance(bd);
                Color mix = up ? Color.white : Color.black;

                float alpha = RecoverAlpha(edge, bd, mix);
                if (alpha <= 1f / Steps) continue;   // 이미 첫 계단이면 「덜 섞을」 여지가 없다.

                Color oneLess = UiChrome.Flatten(new Color(mix.r, mix.g, mix.b, alpha - 1f / Steps), bd);
                float ratio = UiChrome.ContrastRatio(oneLess, bd);

                Assert.Less(ratio, UiChrome.EdgeContrastTarget,
                    $"{name}: 한 계단 덜 섞은 {Hex(oneLess)}도 {ratio:F4}:1로 목표를 넘는다 — " +
                    "최소 해가 아니다(테두리가 필요 이상으로 튄다).");
            }
        }

        private static float RecoverAlpha(Color mixed, Color backdrop, Color over)
        {
            float best = 0f, span = 0f;
            for (int ch = 0; ch < 3; ch++)
            {
                float o = over[ch], b = backdrop[ch], m = mixed[ch];
                float d = Mathf.Abs(o - b);
                if (d <= span) continue;
                span = d;
                best = (m - b) / (o - b);
            }
            Assert.Greater(span, 0f, "바탕과 혼합색이 완전히 같으면 α를 역산할 수 없다.");
            return Mathf.Clamp01(best);
        }

        // ================================================================================
        // 4. 옛 「흰 α」 방식이 실제로 깨지는가 (§4-3 전제)
        // ================================================================================

        [Test]
        public void 네거티브_컨트롤_옛_흰알파_방식은_두_무대에서_전부_무너진다()
        {
            Color charcoal = CharcoalStage();
            Color paper = PaperStage();

            Color oldOnCharcoal = UiChrome.Flatten(new Color(1f, 1f, 1f, 0.18f), charcoal);
            Color oldOnPaper = UiChrome.Flatten(UiChrome.CardBorder, paper);

            float oldCharcoalRatio = UiChrome.ContrastRatio(oldOnCharcoal, charcoal);
            float oldPaperRatio = UiChrome.ContrastRatio(oldOnPaper, paper);

            Debug.Log($"{LogPrefix} 옛 값 — 목탄 {Hex(oldOnCharcoal)} {oldCharcoalRatio:F2}:1 / " +
                $"종이 {Hex(oldOnPaper)} {oldPaperRatio:F2}:1");

            Assert.Less(oldCharcoalRatio, UiChrome.MinNonTextContrast, "옛 목탄 값이 깨지지 않는다 — 전제가 틀렸다.");
            Assert.Less(oldPaperRatio, UiChrome.MinNonTextContrast, "옛 종이 값이 깨지지 않는다 — 전제가 틀렸다.");

            Assert.GreaterOrEqual(Q8Contrast(UiChrome.EdgeOnSurface(charcoal), charcoal),
                UiChrome.MinNonTextContrast);
            Assert.GreaterOrEqual(Q8Contrast(UiChrome.EdgeOnSurface(paper), paper),
                UiChrome.MinNonTextContrast);
        }

        /// <summary>밝은 바탕에서 <b>어떤 α로도</b> 흰 테두리는 하한에 못 간다 — "α만 올리면 되는 것
        /// 아닌가"에 대한 답이자 이 함수의 존재 이유 그 자체.</summary>
        [Test]
        public void 네거티브_컨트롤_밝은_무대에서는_흰_테두리가_어떤_알파로도_하한에_못_간다()
        {
            Color paper = PaperStage();
            float best = 0f;
            for (int i = 0; i <= 1024; i++)
                best = Mathf.Max(best, UiChrome.ContrastRatio(
                    UiChrome.Flatten(new Color(1f, 1f, 1f, i / 1024f), paper), paper));

            Debug.Log($"{LogPrefix} 종이 무대에서 흰 테두리 최댓값 = {best:F3}:1");
            Assert.Less(best, UiChrome.MinNonTextContrast,
                "흰쪽으로 하한에 도달했다 — 그렇다면 방향 판정이 필요 없다는 뜻이고 이 함수의 전제가 틀렸다.");
        }

        // ================================================================================
        // 5. 해가 없을 때 — 조용히 넘기지 않는다
        // ================================================================================

        /// <summary>「여유 큰 쪽」의 최대는 언제나 <c>√21 ≈ 4.583</c> 이상이므로, 그보다 높은 목표에서만
        /// 해가 사라진다. 그때 짖는지 확인한다 — 안 짖으면 "3.15인 줄 알았는데 아니었던" 테두리가
        /// 조용히 출하된다.</summary>
        [Test]
        public void 해가_없는_목표에서는_로그로_짖는다()
        {
            Color mid = Gray(128);
            const float Impossible = 7f;   // 7 > √21. 중간 회색이 그 띠 한복판이다.

            LogAssert.Expect(LogType.Error, new Regex(@"\[테두리\].*없습니다"));
            Color edge = UiChrome.EdgeOnSurface(mid, Impossible);

            Assert.AreEqual(1f, edge.a, 1e-6f, "실패해도 α는 1이어야 한다 — 창 알파를 건드리면 안 된다.");
            Assert.Less(UiChrome.ContrastRatio(edge, mid), Impossible,
                "해가 없다고 판정해 놓고 목표를 넘겼다 — 판정식과 탐색이 갈라져 있다.");
        }

        /// <summary>반대 방향 대조: <c>√21</c> 아래 목표에서는 <b>어떤 회색에서도</b> 짖지 않는다.
        /// (짖으면 <c>LogAssert</c>가 처리되지 않은 에러로 이 테스트를 실패시킨다.)</summary>
        [Test]
        public void 루트21_아래_목표에서는_어느_바탕에서도_짖지_않는다()
        {
            foreach (float t in new[]
            {
                UiChrome.MinNonTextContrast, UiChrome.EdgeContrastTarget,
                UiChrome.ControlFaceContrastTarget, UiChrome.MinTextContrast,
            })
            {
                for (int v = 0; v <= 255; v++) UiChrome.EdgeOnSurface(Gray(v), t);
            }
        }

        // ================================================================================
        // 6. design-art §4-3-c 확정표 검산 — 서로 다른 두 계산이 같은 결론에 오는가
        // ================================================================================

        /// <summary>
        /// §4-3-c 확정표 <b>17행</b>(개정 반영, target 3.15 · 방향 「여유 큰 쪽」).
        /// <b>이 hex는 「베끼면 안 되는 프로덕션 상수」가 아니라 「다른 자가 잰 값」</b>이다 —
        /// 두 계산이 독립적으로 같은 결론에 오는지가 이 테스트의 전부다.
        /// <para><b>빨개졌다면 먼저 팔레트를 의심해라.</b> 바탕 토큰이 한 계단이라도 움직이면 이 표는
        /// 통째로 다시 굴려야 하고, <c>coder-ui</c> 인계표의 검산값도 함께 갱신해야 한다.</para>
        /// <para>바탕 중 넷(<c>ControlFace on Card/Panel</c>, <c>ButtonSurfaceOnCard</c>, 크롬 3단)은
        /// hex를 적지 않고 <b>프로덕션 함수/토큰에서 파생</b>시킨다 — 그래야 그 파생 규칙이 바뀌면
        /// 여기서도 같이 잡힌다.</para>
        /// </summary>
        [Test]
        public void design_art_확정표_17행을_재현한다()
        {
            (string label, Color backdrop, string expected, float expectedQ8)[] rows =
            {
                ("PanelSurface",         UiChrome.PanelSurface,       "#65676A", 3.1662f),
                ("CardSurface",          UiChrome.CardSurface,        "#6A6D71", 3.1786f),
                ("CardSurfaceMuted",     UiChrome.CardSurfaceMuted,   "#65676B", 3.1370f),
                ("SubtleSurface",        UiChrome.SubtleSurface,      "#686B6F", 3.1558f),
                ("ThumbSurfaceLocked",   UiChrome.ThumbSurfaceLocked, "#626468", 3.1391f),
                ("목탄 무대(흰 잉크)",    CharcoalStage(),             "#727478", 3.1547f),
                ("종이 무대(검 잉크)",    PaperStage(),                "#828381", 3.1521f),
                ("검정 잉크 견본",        Color.black,                 "#5C5C5C", 3.1405f),
                ("흰 잉크 견본",          Color.white,                 "#919191", 3.1517f),
                ("Accent",               UiChrome.Accent,             "#64512D", 3.1564f),
                ("ChromeButtonSurface",  UiChrome.ChromeButtonSurface,        "#3D3E3F", 3.1376f),
                ("ChromeButtonHover",    UiChrome.ChromeButtonSurfaceHover,   "#4C4D4E", 3.1514f),
                ("ChromeButtonPressed",  UiChrome.ChromeButtonSurfacePressed, "#5A5B5C", 3.1357f),
                ("ControlFace on Card",  UiChrome.CardActionSurface,  "#38393A", 3.1311f),
                ("ControlFace on Panel", UiChrome.ControlFaceOnSurface(UiChrome.PanelSurface),
                                                                      "#38393A", 3.1368f),
                ("ButtonSurfaceOnCard",  UiChrome.RaisedTextBackdrops[0], "#7F8286", 3.1809f),
                ("TextPrimary 흰 채움",   UiChrome.TextPrimary,        "#888A8B", 3.1476f),
            };

            var failures = new List<string>();
            float worst = float.MaxValue;

            foreach ((string label, Color bd, string expected, float expectedQ8) in rows)
            {
                Color edge = UiChrome.EdgeOnSurface(bd);
                string got = Hex(edge);
                float q = Q8Contrast(edge, bd);
                worst = Mathf.Min(worst, q);
                Debug.Log($"{LogPrefix} §4-3-c {label} {Hex(bd)} -> {got} (q8 {q:F4}) 기대 {expected} / {expectedQ8:F4}");

                if (got != expected) failures.Add($"{label} {Hex(bd)}: {got} ≠ {expected}");
                else if (Mathf.Abs(q - expectedQ8) > 0.0006f)
                    failures.Add($"{label}: q8 {q:F4} ≠ {expectedQ8:F4}");
            }

            Debug.Log($"{LogPrefix} §4-3-c 전 17종 양자화 후 최소 = {worst:F4}");
            Assert.IsEmpty(failures,
                "design-art §4-3-c 확정표와 갈라졌다. 코드가 아니라 팔레트가 움직인 것일 수 있다 — " +
                $"표를 다시 굴리고 인계표 검산값을 갱신해라. 불일치: {string.Join(" / ", failures)}");

            // 문서가 적어 둔 「전 17종 최소 3.1311」과 같은 자리에 서는지.
            Assert.AreEqual(3.1311f, worst, 0.0006f);
        }

        /// <summary>§4-3의 액자 개선치 두 줄(1.79 → 3.15 / 1.02 → 3.15)을 재현한다.</summary>
        [Test]
        public void design_art_액자_개선치를_재현한다()
        {
            Color charcoal = CharcoalStage();
            Color paper = PaperStage();

            Assert.AreEqual(1.79f,
                UiChrome.ContrastRatio(UiChrome.Flatten(new Color(1f, 1f, 1f, 0.18f), charcoal), charcoal), 0.01f);
            Assert.AreEqual(1.02f,
                UiChrome.ContrastRatio(UiChrome.Flatten(UiChrome.CardBorder, paper), paper), 0.01f);

            Assert.AreEqual(3.15f, Q8Contrast(UiChrome.EdgeOnSurface(charcoal), charcoal), 0.01f);
            Assert.AreEqual(3.15f, Q8Contrast(UiChrome.EdgeOnSurface(paper), paper), 0.01f);
        }

        // ================================================================================
        // 7. 리터럴이 실제로 사라졌는가 (§5-1 인계표 1행)
        // ================================================================================

        /// <summary>
        /// <c>CharacterInfoWindow.ApplyPortraitTheme</c>의 흰 α0.18 리터럴이 <b>사라졌는가</b>.
        /// <para>★ <b>부재 단언은 썩으면 조용히 초록이 된다</b>(CLAUDE.md). 그래서 같은 테스트 안에
        /// <b>존재 단언</b>을 짝지어 둔다 — 파일을 못 읽거나 호출이 사라지면 <b>먼저 빨개진다</b>.</para>
        /// </summary>
        [Test]
        public void 정보창_액자_테두리_리터럴이_규칙_호출로_바뀌었다()
        {
            string path = Path.Combine(Application.dataPath,
                "_Project", "Scripts", "Interaction", "CharacterInfoWindow.cs");
            Assert.IsTrue(File.Exists(path), $"소스를 못 찾았다: {path}");

            string src = File.ReadAllText(path);

            // (존재 단언) 이게 없으면 아래 부재 단언은 아무것도 증명하지 않는다.
            StringAssert.Contains($"UiChrome.{nameof(UiChrome.EdgeOnSurface)}(", src,
                "액자 테두리가 규칙을 통해 색을 받지 않는다 — 부재 단언이 썩었다는 뜻이다.");

            // (부재 단언) 손으로 굴린 옛 방식.
            Assert.IsFalse(Regex.IsMatch(src, @"new\s+Color\(\s*1f\s*,\s*1f\s*,\s*1f\s*,\s*0\.18f\s*\)"),
                "흰 α0.18 리터럴이 아직 남아 있다(UI_ALPHA_BLEED_POLICY §4-3).");
        }
    }
}
