using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 P0-3 — <b>톱니가 어떤 데스크톱 위에서도 보이는가</b>.
    ///
    /// ============================================================================
    /// 이 아이콘만 조건이 다르다
    /// ============================================================================
    /// 이 앱의 다른 글자/도형은 전부 <b>우리가 고른 불투명 패널</b> 위에 있어서 대비를 설계 시점에
    /// 정할 수 있다. 톱니 하나만은 <b>유저의 임의의 바탕화면</b> 위에 맨몸으로 놓인다 — 배경을 고를
    /// 수 없다. 그래서 "예쁜가"가 아니라 <b>"최악의 배경에서도 보이는가"</b>를 잠근다.
    ///
    /// 실측(docs/UI_SURFACE_SPEC.md §5.1): 고치기 전 보장 대비는 <b>1.00 : 1</b>이었다. 검정 잉크는
    /// 거의 검은 배경에서 완전히 사라지고, 흰 잉크로 뒤집으면 흰 배경에서 같은 일이 난다 —
    /// <b>단색으로는 원리상 해결이 안 된다</b>는 뜻이다. 그래서 잉크 뒤에 <b>역상 헤일로</b>를 깐다.
    ///
    /// ============================================================================
    /// 왜 EditMode인가 / 왜 숫자를 안 베끼는가
    /// ============================================================================
    /// 검증 대상이 순수 함수(<see cref="InfoGearIconWidget.ResolveHaloColor"/> ·
    /// <see cref="UiChrome.ContrastRatio"/>)와 형태 상수뿐이라 씬이 필요 없다.
    /// 그리고 프로덕션이 쓰는 <b>바로 그 함수와 상수</b>를 참조해서 계산한다 — 여기에 4.37이나 13.8을
    /// 손으로 적으면 그 순간부터 이 파일은 프로덕션이 아니라 자기 자신을 검사하게 된다(CLAUDE.md).
    /// </summary>
    public sealed class InfoGearHaloContrastTests
    {
        private const string LogPrefix = "[톱니대비-TEST]";

        /// <summary>WCAG 2.x 비텍스트(아이콘/그래픽) 최소 대비. 글자가 아니므로 4.5가 아니라 3.0이다.</summary>
        private const float NonTextMinimumContrast = 3.0f;

        /// <summary>잉크 프리셋은 검정/흰색 두 가지뿐이다(<c>StickConfig.ResolveInkColor</c>).
        /// 둘 다 명도 축의 끝이라 역상 헤일로가 반대쪽 끝을 잡아 준다.</summary>
        private static readonly Color[] InkPresets = { Color.black, Color.white };

        /// <summary>
        /// 회색 0~255 전 구간에서 "잉크와 헤일로 중 더 잘 보이는 쪽"의 대비 최소값.
        ///
        /// <para><b>화면에 실제로 나오는 색</b>으로 잰다. 알파는 두 겹 <b>모두</b>에 걸리므로
        /// <c>헤일로' = mix(헤일로, 배경, α)</c>, <c>잉크' = mix(잉크, 헤일로', α)</c>다. 이 프로젝트는
        /// 감마 색공간(<c>m_ActiveColorSpace: 0</c>)이라 블렌딩이 sRGB 값에서 그대로 일어난다.</para>
        ///
        /// <para><b>이 합성을 빼먹으면 테스트가 거짓말을 한다</b> — 스펙 §5.1이 계산한 4.18:1은
        /// α=1 전제였고, 실제 그리기 알파(당시 0.70)를 넣으면 2.65:1로 <b>기준 미달</b>이었다.
        /// "색 조합만 옳으면 됐다"가 아니라 "그 알파로 그렸을 때 보이는가"가 요구사항이다.</para>
        ///
        /// <para>회색만 훑는 이유: 유채색 배경의 상대 휘도는 언제나 어떤 회색의 휘도와 같으므로,
        /// 휘도 기준 대비의 <b>최악값은 회색 축 위에 전부 나타난다</b>. 즉 이 스윕은 모든 색을 덮는다.</para>
        /// </summary>
        private static float WorstGuaranteedContrast(Color ink, Color halo, float alpha, out int worstLevel)
        {
            float worst = float.MaxValue;
            worstLevel = 0;
            for (int level = 0; level <= 255; level++)
            {
                float c = level / 255f;
                var background = new Color(c, c, c, 1f);
                Color haloOnScreen = Color.Lerp(background, halo, alpha);
                Color inkOnScreen = Color.Lerp(haloOnScreen, ink, alpha);

                float best = Mathf.Max(UiChrome.ContrastRatio(inkOnScreen, background),
                                       UiChrome.ContrastRatio(haloOnScreen, background));
                if (best >= worst) continue;
                worst = best;
                worstLevel = level;
            }
            return worst;
        }

        [Test]
        public void HaloGuaranteesNonTextContrastOnEveryPossibleDesktopBackground()
        {
            foreach (Color ink in InkPresets)
            {
                Color halo = InfoGearIconWidget.ResolveHaloColor(ink);
                float alpha = InfoGearIconWidget.IdleOpacity;
                float worst = WorstGuaranteedContrast(ink, halo, alpha, out int level);

                Assert.GreaterOrEqual(worst, NonTextMinimumContrast,
                    $"{LogPrefix} 잉크 {Hex(ink)} + 헤일로 {Hex(halo)}를 불투명도 {alpha:F2}로 그렸을 때 " +
                    $"보장 대비가 {worst:F2}:1입니다(최악 배경 회색 {level}). 비텍스트 최소 " +
                    $"{NonTextMinimumContrast:F1}:1 미만이면 그 바탕화면을 쓰는 유저에게 이 아이콘은 " +
                    "존재하지 않는 것과 같습니다. 색 조합이 옳아도 <b>알파가 그 이득을 먹으면</b> 같은 결함입니다.");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>헤일로가 없으면 이 테스트가 실제로 실패하는가</b>.
        /// 이게 없으면 위 테스트가 "무엇이든 통과하는 초록"일 가능성을 배제할 수 없다
        /// (이 저장소가 크로스 컴파일에서 다섯 번 겪은 거짓 초록과 같은 종류의 위험).
        /// </summary>
        [Test]
        public void SingleInkColourAloneCannotReachThreeToOne_NegativeControl()
        {
            foreach (Color ink in InkPresets)
            {
                // 헤일로 자리에 잉크와 같은 색을 넣는다 = 헤일로가 없는 것과 같다.
                float worst = WorstGuaranteedContrast(ink, ink, InfoGearIconWidget.IdleOpacity, out int level);
                Assert.Less(worst, NonTextMinimumContrast,
                    $"{LogPrefix} 단색 {Hex(ink)}만으로 보장 대비 {worst:F2}:1이 나왔습니다(배경 회색 {level}). " +
                    "이 테스트의 전제(단색으로는 원리상 불가능)가 깨졌다는 뜻이므로 스윕이 " +
                    "제대로 돌고 있는지 먼저 의심해야 합니다.");
            }
        }

        /// <summary>
        /// ★ 이 라운드에 실제로 발견된 함정 — <b>불투명도가 대비 보장을 먹는다</b>.
        /// 스펙 §5.1은 α=1로 4.18:1을 계산했지만 이 아이콘은 평소 α&lt;1로 그려진다.
        /// 옛 값(0.70)이면 2.65:1로 기준 미달이었다. 그 관계를 잊지 않게 잠근다.
        /// </summary>
        [Test]
        public void LoweringOpacityEatsTheGuarantee_SoIdleOpacityMustStayHighEnough()
        {
            Color ink = Color.black;
            Color halo = InfoGearIconWidget.ResolveHaloColor(ink);

            float atFull = WorstGuaranteedContrast(ink, halo, 1f, out _);
            float atIdle = WorstGuaranteedContrast(ink, halo, InfoGearIconWidget.IdleOpacity, out _);

            Assert.Less(atIdle, atFull,
                $"{LogPrefix} 불투명도를 낮췄는데 보장 대비가 줄지 않았습니다({atIdle:F2} vs {atFull:F2}) — " +
                "합성 계산이 실제로 돌고 있는지 의심해야 합니다.");
            Assert.GreaterOrEqual(atIdle, NonTextMinimumContrast,
                $"{LogPrefix} 평상 불투명도 {InfoGearIconWidget.IdleOpacity:F2}에서 보장 대비가 {atIdle:F2}:1입니다 — " +
                "'은은하게'를 이유로 알파를 더 내리려면 그만큼 헤일로/잉크 조합을 바꿔야 합니다.");
        }

        [Test]
        public void HaloIsDistinguishableFromTheInkItself()
        {
            foreach (Color ink in InkPresets)
            {
                Color halo = InfoGearIconWidget.ResolveHaloColor(ink);
                float c = UiChrome.ContrastRatio(ink, halo);
                Assert.GreaterOrEqual(c, NonTextMinimumContrast,
                    $"{LogPrefix} 잉크 {Hex(ink)}와 헤일로 {Hex(halo)}의 대비가 {c:F2}:1입니다 — " +
                    "두 겹이 서로 구분되지 않으면 헤일로는 그냥 굵은 획일 뿐입니다.");
            }
        }

        /// <summary>
        /// ★ 형태 — <b>헤일로가 들어갈 자리가 실제로 있는가</b>.
        ///
        /// <para>옛 형태가 정확히 여기서 실패했다: 작은 기어의 이 골이 1.68pt인데 헤일로는 3.74pt라
        /// 골이 통째로 메워졌다(그래서 형태를 단일 기어로 바꿨다). 두 겹을 얹는 아이콘은 이제부터
        /// <b>잉크 여유</b>(획 1.5배 규칙)와 <b>헤일로 여유</b>(0보다 커야 한다)를 동시에 지켜야 한다.</para>
        ///
        /// <para>여유 = (두 획의 중심선 간격) − (획 폭). 값은 전부
        /// <see cref="InfoGearIconWidget"/>의 공개 상수에서 <b>다시 계산</b>한다.</para>
        /// </summary>
        [Test]
        public void GearFormLeavesRoomForBothTheInkAndTheHalo()
        {
            float w = InfoGearIconWidget.StrokeWidth;
            float haloW = InfoGearIconWidget.HaloWidth;
            int teeth = InfoGearIconWidget.Teeth;
            float rTip = InfoGearIconWidget.TipRadius;
            float rRoot = InfoGearIconWidget.RootRadius;
            float rHub = InfoGearIconWidget.HubRadius;

            Assert.Greater(teeth, 2, $"{LogPrefix} 잇수가 {teeth}개입니다 — 기어로 읽히지 않습니다.");
            Assert.Greater(haloW, w, $"{LogPrefix} 헤일로가 잉크보다 굵지 않으면 잉크에 완전히 가려집니다.");

            float pitch = Mathf.PI * 2f / teeth;
            float tipHalf = pitch * InfoGearIconWidget.ToothTipHalfFraction_ForTests;
            float rootHalf = pitch * InfoGearIconWidget.ToothRootHalfFraction_ForTests;

            Assert.Less(tipHalf, rootHalf,
                $"{LogPrefix} 이 끝이 뿌리보다 넓습니다 — 사다리꼴 이가 아니라 거꾸로 된 쐐기가 됩니다.");

            // (간격 이름, 두 획의 중심선 간격)
            var gaps = new (string Name, float Centerline)[]
            {
                ("허브 -> 뿌리원", rRoot - rHub),
                ("뿌리원 -> 팁원(이 높이)", rTip - rRoot),
                ("이 폭 @뿌리원(호)", rRoot * 2f * rootHalf),
                ("이 사이 골 @뿌리원(호)", rRoot * (pitch - 2f * rootHalf)),
                ("이 폭 @팁원(호)", rTip * 2f * tipHalf),
            };

            const float MinInkClearInStrokes = 1.5f;   // 이 값 미만이면 화면에서 두 선이 한 줄로 뭉친다.
            foreach (var gap in gaps)
            {
                float inkClear = gap.Centerline - w;
                Assert.GreaterOrEqual(inkClear, MinInkClearInStrokes * w,
                    $"{LogPrefix} '{gap.Name}'의 잉크 여유가 {inkClear:F2}pt({inkClear / w:F2}획)입니다 — " +
                    $"{MinInkClearInStrokes:F1}획 미만이면 두 선이 한 줄로 뭉쳐 형태가 사라집니다.");

                float haloClear = gap.Centerline - haloW;
                Assert.Greater(haloClear, 0f,
                    $"{LogPrefix} '{gap.Name}'이 헤일로({haloW:F2}pt)에 통째로 메워집니다" +
                    $"(간격 {gap.Centerline:F2}pt). 옛 두 기어 형태가 정확히 이 이유로 폐기됐습니다 — " +
                    "헤일로를 넣으려면 형태가 그만큼 자리를 내줘야 합니다.");
            }

            // 허브 링 안쪽에도 배경이 남아야 한다(헤일로가 안쪽으로도 번지므로).
            float hubHoleRadius = rHub - haloW * 0.5f;
            Assert.Greater(hubHoleRadius, 0f,
                $"{LogPrefix} 허브 링 안쪽이 헤일로로 다 메워집니다(안쪽 반경 {hubHoleRadius:F2}pt) — " +
                "링이 아니라 점이 됩니다.");
        }

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
