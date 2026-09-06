using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ============================================================================
    /// 인용 감사 #3 — <b>「회색 0~255 전 구간을 훑어 ≥3:1을 확인한다」던 그 기계</b>
    /// (test-engineer, 2026-09-06)
    /// ============================================================================
    /// <c>Interaction/InfoGearIconWidget.cs</c>가 <b>두 곳</b>에서 이 이름을 부른다:
    ///
    /// <list type="bullet">
    ///  <item>:196 — <i>"보장 대비는 <c>ResolveHaloColor</c>가 계산하고
    ///    <c>Tests/PlayMode/InfoGearContrastTests</c>가 회색 0~255 전 구간을 훑어 ≥3:1을 확인한다."</i></item>
    ///  <item>:493 — <i>"회색 전 구간 최악값이 <b>4.37 : 1</b>(WCAG 비텍스트 최소 3:1을 46% 상회).
    ///    그 단언은 <c>Tests/PlayMode/InfoGearContrastTests</c>가 전 구간을 훑어서 한다."</i></item>
    /// </list>
    ///
    /// <b>그 테스트는 2026-09-06까지 존재하지 않았다.</b> 두 문장 모두 <b>현재형 단언</b>이라
    /// 읽는 사람은 «이미 훑고 있다»고 믿게 된다. 이 파일이 그 문장들을 참으로 만든다.
    ///
    /// ============================================================================
    /// 왜 이 아이콘만 특별한가
    /// ============================================================================
    /// 이 아이콘은 <b>유저의 임의의 데스크톱</b> 위에 맨몸으로 놓인다(패널 배경이 없다).
    /// 잉크는 캐릭터 잉크 고정이고 배경 보정이 없으므로 <b>단색으로는 원리상 해결 불가</b>다 —
    /// 검정 잉크는 검은 배경에서, 흰 잉크는 흰 배경에서 각각 <b>1.00 : 1</b>이 된다(= 사라진다).
    /// 그래서 만화 레터링의 Outline 관용구를 쓴다: 같은 경로를 <b>잉크의 역상</b>으로 먼저 굵게
    /// 긋고(헤일로) 그 위에 잉크를 긋는다. 그러면 <b>어떤 배경이 와도 둘 중 하나</b>는 보인다.
    ///
    /// ============================================================================
    /// ★ 「보장 대비」의 정의 — 이 파일이 재는 수의 뜻
    /// ============================================================================
    /// 배경 <c>bg</c>에 대한 보장 대비 = <c>max(CR(잉크, bg), CR(헤일로, bg))</c>.
    /// 「둘 중 하나는 보인다」가 곧 <b>최대</b>이고, 전 구간 최악값은 그 최대들의 <b>최소</b>다.
    /// <c>min over bg of max(...)</c> — 순서를 바꾸면(<c>max of min</c>) 전혀 다른 수가 된다.
    ///
    /// ============================================================================
    /// ★ 「0건」이 아니라 「존재와 부재를 같은 테스트에서」 (CLAUDE.md)
    /// ============================================================================
    /// <see cref="헤일로가_없으면_최악_대비가_1_00까지_떨어진다"/>가 <b>같은 훑기 함수</b>로
    /// «헤일로 없이는 1.00»을 보인다. 그게 없으면 위의 «4.37 통과»는 «스위프가 실제로 나쁜
    /// 배경을 만들 수 있다»는 것을 증명하지 못한다 — 훑기가 고장 나 늘 밝은 배경만 만들어도
    /// 4.37은 나온다.
    ///
    /// <para><b>왜 PlayMode인가</b>: 인용이 <c>Tests/PlayMode/</c>로 위치를 <b>단언</b>하기 때문이다
    /// (인용 감사는 파일명뿐 아니라 <b>디렉터리</b>까지 본다 — 실제로 그 규칙이
    /// <c>PortraitBodyStrokeParityTests</c>의 위치 오기를 잡았다). 내용은 순수 계산이라
    /// 씬도 프레임 대기도 필요 없다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 색 계산만 한다.</para>
    /// </summary>
    public sealed class InfoGearContrastTests
    {
        private const string LogPrefix = "[톱니대비]";

        /// <summary>
        /// WCAG 2.2 «비텍스트 대비»(1.4.11) 최소값. <b>외부 표준이지 우리가 정한 수가 아니다</b> —
        /// 그래서 여기 적는 것이 프로덕션 상수 복사에 해당하지 않는다.
        /// 출처: https://www.w3.org/TR/WCAG22/#non-text-contrast
        /// </summary>
        private const float WcagNonTextMinimum = 3.0f;

        /// <summary>
        /// 회색 램프 표본 수. sRGB 8비트 전 구간 — 인용문의 «0~255»가 이 수다.
        /// 256이 아니면 «전 구간»이 아니다.
        /// </summary>
        private const int GrayLevels = 256;

        /// <summary>:493이 <b>숫자로</b> 적어 둔 실측값. 이건 <b>핀</b>이다 —
        /// 팔레트가 움직여 이 수가 달라지면 <b>그 주석도 함께 낡은 것</b>이므로 여기서 알려야 한다.</summary>
        private const float DocumentedWorstForBlackInk = 4.37f;

        /// <summary>핀의 허용 오차. 소수 둘째 자리까지 적힌 값이라 반올림 폭만 허용한다.</summary>
        private const float PinTolerance = 0.01f;

        // ====================================================================
        // 훑기 — 순수 함수. 아래 대조들이 <b>같은 함수</b>에 다른 색을 흘린다.
        // ====================================================================

        private static Color Gray(int level) => new Color(level / 255f, level / 255f, level / 255f, 1f);

        /// <summary>
        /// 회색 전 구간에 대한 <b>보장 대비의 최악값</b>과 그때의 회색 레벨.
        /// <para><paramref name="visited"/>로 <b>실제로 몇 개를 봤는지</b> 돌려준다 —
        /// 0건이 «통과»로 둔갑하는 것을 막는 유일한 수다.</para>
        /// </summary>
        internal static float WorstGuaranteedContrast(Color ink, Color halo,
            out int worstLevel, out int visited)
        {
            float worst = float.MaxValue;
            worstLevel = -1;
            visited = 0;

            for (int level = 0; level < GrayLevels; level++)
            {
                Color bg = Gray(level);
                float guaranteed = Mathf.Max(
                    UiChrome.ContrastRatio(ink, bg),
                    UiChrome.ContrastRatio(halo, bg));
                visited++;
                if (guaranteed >= worst) continue;
                worst = guaranteed;
                worstLevel = level;
            }
            return worst;
        }

        /// <summary>이 앱이 실제로 고를 수 있는 잉크들. 잉크는 유저 설정이라 <b>한 색만</b> 재면 안 된다.</summary>
        private static IEnumerable<(string Name, Color Ink)> InkCandidates()
        {
            yield return ("검정(출하 기본)", Color.black);
            yield return ("흰색", Color.white);
            yield return ("팔레트 밝은 끝(TextPrimary)", UiChrome.TextPrimary);
            yield return ("팔레트 어두운 끝(OnAccentSolid)", UiChrome.OnAccentSolid);
        }

        // ====================================================================
        // 1. ★ 본론 — :196이 약속한 「전 구간 ≥ 3:1」
        // ====================================================================

        [Test]
        public void 회색_전_구간에서_보장_대비가_WCAG_최소를_넘는다()
        {
            var failures = new List<string>();

            foreach ((string name, Color ink) in InkCandidates())
            {
                Color halo = InfoGearIconWidget.ResolveHaloColor(ink);
                float worst = WorstGuaranteedContrast(ink, halo, out int level, out int visited);

                Assert.AreEqual(GrayLevels, visited,
                    $"{LogPrefix} 회색을 {visited}개만 훑었습니다(기대 {GrayLevels}). " +
                    "«전 구간»이 아니면 최악값은 최악값이 아닙니다.");

                Debug.Log($"{LogPrefix} 잉크 {name} → 최악 {worst:F4} : 1 (회색 {level})");

                if (worst < WcagNonTextMinimum)
                    failures.Add($"{name}: 최악 {worst:F4} : 1 (회색 {level}) < {WcagNonTextMinimum}");
            }

            Assert.IsEmpty(failures,
                $"{LogPrefix} 회색 배경 어딘가에서 톱니 아이콘이 WCAG 비텍스트 최소({WcagNonTextMinimum}:1) " +
                "아래로 떨어집니다:\n  " + string.Join("\n  ", failures) + "\n" +
                "이 아이콘은 <b>유저의 임의의 데스크톱</b> 위에 맨몸으로 놓입니다 — 패널 배경이 " +
                "없으므로 여기서 대비를 잃으면 아이콘이 <b>그냥 사라집니다</b>(2026-09-01 P0-3의 " +
                "원래 결함이 정확히 그것이었습니다). 헤일로 색 선택 규칙(ResolveHaloColor)이나 " +
                "팔레트의 양 끝을 건드렸다면 그 변경이 원인입니다.");
        }

        // ====================================================================
        // 2. ★ :493이 「숫자로」 적어 둔 실측값의 핀
        // ====================================================================

        /// <summary>
        /// 주석은 <b>4.37 : 1</b>이라고 못박았다. 주석의 숫자는 아무도 실행하지 않으므로
        /// <b>버그보다 오래 산다</b> — 그래서 여기서 핀으로 잡는다.
        /// </summary>
        [Test]
        public void 검정_잉크의_전_구간_최악값이_문서에_적힌_수와_같다()
        {
            Color ink = Color.black;
            Color halo = InfoGearIconWidget.ResolveHaloColor(ink);
            float worst = WorstGuaranteedContrast(ink, halo, out int level, out int visited);

            Assert.AreEqual(GrayLevels, visited, $"{LogPrefix} 전 구간을 훑지 않았습니다.");
            Debug.Log($"{LogPrefix} 검정 잉크 최악 = {worst:F4} : 1 (회색 {level})");

            Assert.AreEqual(DocumentedWorstForBlackInk, worst, PinTolerance,
                $"{LogPrefix} 실측 최악값 {worst:F4}가 주석에 적힌 {DocumentedWorstForBlackInk}와 " +
                "다릅니다(InfoGearIconWidget.cs의 «회색 전 구간 최악값이 4.37 : 1» 문장).\n" +
                "숫자가 좋아졌더라도 <b>주석이 낡은 것</b>이므로 함께 고쳐야 합니다 — 이 저장소가 " +
                "반복해 당한 형태가 «주석이 버그보다 오래 사는 것»입니다. 팔레트 양 끝" +
                "(UiChrome.TextPrimary / OnAccentSolid)을 건드렸다면 그 변경이 원인입니다.");

            // 문서가 함께 주장한 «46% 상회»도 같은 자리에서 확인한다.
            float headroom = worst / WcagNonTextMinimum - 1f;
            Assert.Greater(headroom, 0.40f,
                $"{LogPrefix} WCAG 최소 대비 여유가 {headroom:P0}뿐입니다(주석은 46%라고 적었습니다).");
        }

        // ====================================================================
        // 3. ★ 존재/부재 대조 — 훑기가 실제로 «나쁜 배경»을 만들 수 있는가
        // ====================================================================

        /// <summary>
        /// ★★ <b>이 파일에서 가장 중요한 테스트다.</b> 위 두 테스트의 «통과»는 훑기가
        /// <b>실제로 최악의 배경을 찾아낼 수 있을 때만</b> 의미가 있다. 그래서 <b>같은 함수</b>에
        /// «헤일로 = 잉크»(= 헤일로가 없는 것과 같다)를 흘려, 프로덕션 주석이 적어 둔
        /// <i>"검정 잉크의 회색 전 구간 최악 대비 = 1.00 : 1"</i>이 그대로 재현되는지 본다.
        /// </summary>
        [Test]
        public void 헤일로가_없으면_최악_대비가_1_00까지_떨어진다()
        {
            foreach ((string name, Color ink) in InkCandidates())
            {
                // 헤일로 자리에 잉크를 그대로 넣는다 = 2겹 이전의 상태.
                float worst = WorstGuaranteedContrast(ink, ink, out int level, out int visited);

                Assert.AreEqual(GrayLevels, visited, $"{LogPrefix} 전 구간을 훑지 않았습니다.");
                Assert.Less(worst, WcagNonTextMinimum,
                    $"{LogPrefix} 헤일로 없이도 {name} 잉크가 WCAG 최소를 넘었습니다" +
                    $"(최악 {worst:F4} : 1, 회색 {level}).\n" +
                    "그러면 이 훑기는 <b>나쁜 배경을 만들지 못하고 있습니다</b> — 위의 «4.37 통과»는 " +
                    "헤일로 덕분이 아니라 훑기가 눈이 먼 결과일 수 있습니다. " +
                    "(프로덕션 주석은 «검정 잉크의 회색 전 구간 최악 대비 = 1.00 : 1»이라고 적었습니다.)");

                Debug.Log($"{LogPrefix} 헤일로 없음 · 잉크 {name} → 최악 {worst:F4} : 1 (회색 {level})");
            }
        }

        /// <summary>
        /// :493의 <i>"잉크가 검정이면 밝은 쪽(#f2f4f7), 흰색이면 어두운 쪽(#0b1016)이 뽑힌다"</i>.
        /// 헤일로가 잉크와 <b>같은 쪽</b>으로 뽑히면 2겹은 아무 일도 하지 않는다.
        /// </summary>
        [Test]
        public void 헤일로는_잉크의_반대쪽_끝으로_뽑힌다()
        {
            Assert.AreEqual(UiChrome.TextPrimary, InfoGearIconWidget.ResolveHaloColor(Color.black),
                $"{LogPrefix} 검정 잉크에 밝은 쪽 헤일로가 안 뽑혔습니다.");
            Assert.AreEqual(UiChrome.OnAccentSolid, InfoGearIconWidget.ResolveHaloColor(Color.white),
                $"{LogPrefix} 흰 잉크에 어두운 쪽 헤일로가 안 뽑혔습니다.");

            foreach ((string name, Color ink) in InkCandidates())
            {
                Color halo = InfoGearIconWidget.ResolveHaloColor(ink);
                Assert.Greater(UiChrome.ContrastRatio(ink, halo), WcagNonTextMinimum,
                    $"{LogPrefix} {name} 잉크와 그 헤일로가 서로 비슷합니다" +
                    $"(대비 {UiChrome.ContrastRatio(ink, halo):F2} : 1). 두 겹이 같은 색이면 " +
                    "헤일로는 «굵게 그린 잉크»일 뿐이고, 배경 대비를 하나도 벌지 못합니다.");
            }
        }

        // ====================================================================
        // 4. 훑기 함수 자체의 대조
        // ====================================================================

        [Test]
        public void 대조_훑기가_최댓값이_아니라_최솟값을_고른다()
        {
            // 검정 잉크 + 밝은 헤일로. 회색 0(검정 배경)에서는 헤일로가, 255(흰 배경)에서는 잉크가
            // 각각 크게 이긴다 — 그래서 <b>최악은 양 끝이 아니라 가운데</b>에 있어야 한다.
            Color ink = Color.black;
            Color halo = InfoGearIconWidget.ResolveHaloColor(ink);
            WorstGuaranteedContrast(ink, halo, out int level, out _);

            Assert.Greater(level, 0,
                $"{LogPrefix} 최악 회색이 0(검정)입니다 — 훑기가 <b>잉크만</b> 보고 있습니다" +
                "(헤일로가 max에 안 들어갔습니다).");
            Assert.Less(level, GrayLevels - 1,
                $"{LogPrefix} 최악 회색이 255(흰색)입니다 — 훑기가 <b>헤일로만</b> 보고 있습니다.");
        }

        [Test]
        public void 대조_회색_램프가_실제로_검정에서_흰색까지_간다()
        {
            Assert.AreEqual(0f, Gray(0).r, 1e-6f, $"{LogPrefix} 램프 시작이 검정이 아닙니다.");
            Assert.AreEqual(1f, Gray(GrayLevels - 1).r, 1e-6f, $"{LogPrefix} 램프 끝이 흰색이 아닙니다.");
            Assert.AreEqual(Gray(128).r, Gray(128).b, 1e-6f,
                $"{LogPrefix} 램프가 무채색이 아닙니다 — 채널이 어긋나면 «회색 전 구간»이 아닙니다.");

            // 대비 계산기 자체의 교정(TEAM.md 공통 처방: 알려진 값으로 먼저 맞춘다).
            Assert.AreEqual(21f, UiChrome.ContrastRatio(Color.black, Color.white), 0.01f,
                $"{LogPrefix} 흑백 대비가 21.0이 아닙니다 — 대비 계산기 교정 실패. " +
                "이 파일의 모든 숫자를 폐기하세요.");
            Assert.AreEqual(1f, UiChrome.ContrastRatio(Color.gray, Color.gray), 0.001f,
                $"{LogPrefix} 같은 색 대비가 1.0이 아닙니다 — 같은 이유로 교정 실패입니다.");
        }
    }
}
