using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>[오늘 할일] 행이 창 밖으로 새지 않는다 / 취소선이 폰트 폴백에 기대지 않는다.</b>
    /// (docs/UX_WIDGETS.md <b>R6-0-2</b> · <b>UW-6-3</b>, ux-widgets 2026-09-08 감사)
    ///
    /// ============================================================================
    /// 무엇이 출하돼 있었나
    /// ============================================================================
    /// (가) <c>UiChrome.AddText(..., wrap:false)</c>는 <c>horizontalOverflow = Overflow</c>로 그리고
    ///      이 패널에는 마스크가 없다. 라벨 상자를 162pt로 잡아 둔 것은 <b>그림에만</b> 있는 약속이었다.
    ///      실측: 17자 → [✕] 위를 덮음, 60자(입력 상한) → 패널 밖 474pt(차단막 <b>바깥</b>의 바탕화면).
    /// (나) 취소선이 글자마다 U+0336을 끼우는 <b>결합문자 조립</b>이었다. macOS 실기로는 그려지지만
    ///      Windows 폰트 폴백은 <b>미확인</b>이다(U+2715 두부 논쟁과 같은 종류의 위험, R6-11 #3).
    ///
    /// ============================================================================
    /// 이 파일이 재는 것 / 재지 않는 것
    /// ============================================================================
    /// 여기서는 <b>처방이 그 자리에 걸려 있는가</b>만 소스로 잰다(씬·플랫폼·실행 불필요, 양쪽 타깃 동일).
    /// 실제로 잉크가 상자 안에서 멈추는가, 선이 라벨과 같은 줄에 같은 두께로 놓이는가는
    /// PlayMode의 <c>TodoBoardRowOverflowTests</c>가 <b>실측</b>으로 잰다. 두 파일은 서로를 대체하지 않는다.
    ///
    /// <para>★ 니들을 쓰는 곳마다 <b>양성 대조</b>를 붙였다. 이 저장소는 부재 단언이 썩어서
    /// 조용히 초록이 되는 사고를 반복해서 겪었다(CLAUDE.md 협업 프로토콜).</para>
    /// </summary>
    public sealed class TodoBoardRowOverflowAuditTests
    {
        private const string LogPrefix = "[할일행넘침-감사]";

        private const string PopoverFile = "TodoBoardPopover.cs";
        private const string PostItFile = "TodoPostItWidget.cs";

        /// <summary>유니코드 결합 취소선(U+0336). <b>이 결함의 본체 그 자체</b>라 문자로 적는다 —
        /// 식별자가 아니므로 «이름이 바뀌어 니들이 썩는» 경로가 없다.</summary>
        private const char CombiningLongStrokeOverlay = '̶';

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string part in relative) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");
            string source = File.ReadAllText(path);
            Assert.Greater(source.Length, 2000,
                $"{LogPrefix} {path}를 읽었는데 {source.Length}자뿐입니다 — 빈 문자열을 훑고 " +
                "\"없다\"고 말하는 거짓 초록을 막습니다.");
            return source;
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        // ====================================================================
        // (가) 말줄임 — 상자와 «자르는 기준»이 같은 상수에서 나온다
        // ====================================================================

        [Test]
        public void 행_라벨은_UiChrome_Ellipsize로_상자_폭에서_잘린다()
        {
            string source = ReadScript("Interaction", PopoverFile);

            // 니들을 nameof로 조립한다 — 함수 이름이 바뀌면 이 테스트가 <b>컴파일</b>에서 죽는다.
            // (문자열로 베껴 두면 이름이 바뀐 날 조용히 IndexOf = -1이 된다.)
            string call = nameof(UiChrome) + "." + nameof(UiChrome.Ellipsize) + "(";
            int at = source.IndexOf(call, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0,
                $"{LogPrefix} [오늘 할일]이 「{call}」를 부르지 않습니다. Overflow로 그리는 라벨에 " +
                "마스크도 말줄임도 없으면 60자 항목이 패널 밖 바탕화면까지 흘러나갑니다(R6-0-2 가).");

            // 스캐너가 살아 있는지 — 같은 스캐너를 «없는 것이 확실한» 표본에 돌려 본다.
            Assert.Less("이 문자열에는 그 호출이 없다".IndexOf(call, StringComparison.Ordinal), 0,
                $"{LogPrefix} 스캐너가 아무 문자열에나 물고 있습니다 — 위 단언은 무의미합니다.");

            // 예산으로 <b>라벨 상자 폭 상수</b>를 넘기는가. 숫자를 따로 적어 넣으면 상자가 넓어지는 날
            // 자르는 기준만 옛 값에 남는다(이 저장소는 폭 1042 때 정확히 그렇게 깨졌다).
            string region = source.Substring(at, Math.Min(200, source.Length - at));
            Assert.GreaterOrEqual(region.IndexOf(nameof(TodoBoardPopover.RowLabelWidth), StringComparison.Ordinal), 0,
                $"{LogPrefix} 말줄임 예산이 「{nameof(TodoBoardPopover.RowLabelWidth)}」가 아닙니다:\n{region}\n" +
                "상자와 자르는 기준이 갈라지면 둘 중 하나만 고쳐지는 날이 옵니다.");
        }

        [Test]
        public void 라벨_폭은_행_폭에서_파생되고_숫자로_굳어_있지_않다()
        {
            string source = ReadScript("Interaction", PopoverFile);

            const string decl = "public const float " + nameof(TodoBoardPopover.RowLabelWidth);
            int at = source.IndexOf(decl, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0,
                $"{LogPrefix} 「{decl}」 선언을 찾지 못했습니다 — 선언 형태가 바뀌었으면 이 감사를 함께 고치세요.");

            int end = source.IndexOf(';', at);
            Assert.Greater(end, at, $"{LogPrefix} {nameof(TodoBoardPopover.RowLabelWidth)} 선언이 끝나지 않습니다.");
            string body = source.Substring(at, end - at);

            Assert.GreaterOrEqual(body.IndexOf("RowWidth", StringComparison.Ordinal), 0,
                $"{LogPrefix} 라벨 폭이 행 폭에서 파생되지 않습니다:\n{body}\n" +
                "R6에서 패널이 300 → 500으로 넓어지면 행만 넓어지고 라벨은 옛 값에 남습니다.");

            // 옛 하드코딩 잔재. 162(라벨 폭) / 32(RowWidth - 32f = [✕] 왼끝)가 다시 생기면
            // 파생이 무너진 것이다.
            Assert.AreEqual(0, CountOf(source, "162f"),
                $"{LogPrefix} 162f 리터럴이 되살아났습니다 — 라벨 폭의 정본은 " +
                $"{nameof(TodoBoardPopover.RowLabelWidth)} 하나여야 합니다.");
            Assert.AreEqual(0, CountOf(source, "RowWidth - 32f"),
                $"{LogPrefix} [✕] 왼끝이 다시 손으로 계산돼 있습니다 — 라벨 폭과 서로를 모르는 상태로 돌아갔습니다.");

            // 실제 값도 한 번 본다(다른 방법으로 다시 재기). 숫자를 베끼지 않고 «말이 되는 범위»만.
            Assert.Greater(TodoBoardPopover.RowLabelWidth, 0f,
                $"{LogPrefix} 라벨 폭이 {TodoBoardPopover.RowLabelWidth}pt입니다 — 파생식이 음수로 뒤집혔습니다.");
        }

        // ====================================================================
        // (나) 취소선 — 결합문자가 아니라 1pt 선
        // ====================================================================

        [Test]
        public void 취소선은_U0336_조립이_아니라_포스트잇과_같은_1pt_선이다()
        {
            string popover = ReadScript("Interaction", PopoverFile);
            string postIt = ReadScript("Interaction", PostItFile);

            // ---- 부재 단언(썩으면 조용히 초록이 되는 쪽) ----
            Assert.Less(popover.IndexOf(CombiningLongStrokeOverlay), 0,
                $"{LogPrefix} {PopoverFile}에 U+0336 결합문자가 있습니다 — 글리프 가용성에 기대는 방식은 " +
                "이 저장소가 U+2715로 이미 한 번 데인 형태이고, Windows 폰트 폴백은 지금도 미확인입니다(R6-11 #3).");

            // ---- 그 부재 단언이 «살아 있는 스캐너»의 결과라는 증명 ----
            Assert.GreaterOrEqual(("a" + CombiningLongStrokeOverlay + "b").IndexOf(CombiningLongStrokeOverlay), 0,
                $"{LogPrefix} 스캐너가 일부러 심은 U+0336조차 못 찾습니다 — 위 \"없다\"는 어떤 파일에서도 " +
                "통과하는 빈 조건입니다(거짓 초록).");

            // ---- 그리고 «없앴다»와 «옮겨 심었다»를 구분하는 양성 대조 ----
            //      포스트잇(정본)과 팝오버(이식본)가 <b>같은 이름의 처방</b>을 갖고 있는가.
            const string prescription = "ApplyStrikethrough";
            Assert.GreaterOrEqual(postIt.IndexOf(prescription, StringComparison.Ordinal), 0,
                $"{LogPrefix} 정본인 {PostItFile}에서 「{prescription}」가 사라졌습니다 — " +
                "이 대조가 무의미해졌으니 두 곳을 함께 고치세요.");
            Assert.GreaterOrEqual(popover.IndexOf(prescription, StringComparison.Ordinal), 0,
                $"{LogPrefix} {PopoverFile}에 「{prescription}」가 없습니다 — 취소선이 사라진 것인지 " +
                "방식만 바뀐 것인지 구분되지 않습니다.");

            // ---- 두 곳 모두 «글자 폭을 실측해서» 선을 긋는가(길이 추정이 아니라) ----
            foreach (var pair in new[]
                     {
                         new { File = PopoverFile, Source = popover },
                         new { File = PostItFile, Source = postIt },
                     })
            {
                int at = pair.Source.IndexOf(prescription + "(RowWidgets", StringComparison.Ordinal);
                if (at < 0) at = pair.Source.IndexOf(prescription + "(RowView", StringComparison.Ordinal);
                Assert.GreaterOrEqual(at, 0,
                    $"{LogPrefix} {pair.File}에서 「{prescription}」 <b>정의</b>를 찾지 못했습니다(호출만 있습니다).");

                string body = pair.Source.Substring(at, Math.Min(700, pair.Source.Length - at));
                Assert.GreaterOrEqual(body.IndexOf(nameof(Text.preferredWidth), StringComparison.Ordinal), 0,
                    $"{LogPrefix} {pair.File}의 취소선이 실측 폭({nameof(Text.preferredWidth)})을 " +
                    "쓰지 않습니다 — 글자 수 모형은 한글에서만 맞고 라틴에서는 선이 글자 밖으로 남습니다.");
            }
        }
    }
}
