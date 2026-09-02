using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using StickMate.Dialogue;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 발화 게이트의 <b>문자체계 인식</b> 계약 — 2026-09-02(사용자 확정 "영어 출시 1.0").
    ///
    /// ============================================================================
    /// 이 라운드가 고친 병 — 단위 오류
    /// ============================================================================
    /// <c>DialogueBudget.PerGlyphSeconds = 0.075f</c>는 실제로 <b>초/음절</b>인데
    /// <c>ReadingSeconds</c>가 <c>text.Length</c>(초/글자)로 청구했다.
    /// <b>한글에서만 글자 = 음절이라 한 번도 안 들켰다.</b>
    /// <code>
    ///   헉... 높다          음절 3 / 글자  7 -> 0.865초  (ParkourClimb 분모 1.20)  발화
    ///   Whoa, that's high  음절 3 / 글자 17 -> 1.615초                            침묵
    /// </code>
    /// 증상이 로그 한 줄(<c>발화 보류</c>)뿐이라 그대로 출하되면 영어권 사용자에게는
    /// <b>"이 캐릭터는 말을 안 한다"</b>로 보인다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — <b>절대 조건은 하나다</b>
    /// ============================================================================
    /// <b>한국어 결과가 비트 단위로 한 톨도 바뀌지 않는다.</b> 하나라도 달라지면 이 변경은 무효다.
    /// 잠금은 넷이고 서로 다른 방향에서 온다:
    /// <list type="number">
    ///   <item><b>골든 동결</b>(<c>Golden/DialogueBudgetKoGolden.txt</c>) — 개정 <b>전</b> 식으로 구운
    ///     결과 숫자 자체. 식으로 다시 확인하면 <b>식이 함께 틀어질 때 같이 틀어진다</b>.</item>
    ///   <item><b>구조 동치</b> — 상수를 <b>참조</b>해 구식 식을 재구성하고 대조(숫자 베끼기 금지).</item>
    ///   <item><b>말뭉치 양방향 대조</b> — 대사가 늘거나 줄면 실패한다.</item>
    ///   <item><b>★ 양성 대조</b> — 계수를 틀리게 넣으면 <b>실제로 빨개지는가</b>.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★★★ 양성 대조 #1은 처음에 <b>거짓 통과</b>였다 — 그 함정을 여기 그대로 옮긴다
    /// ============================================================================
    /// 설계 단계(<c>docs/localization/verify/gate.py</c>)에서 "계수를 1틱 틀리면 불변 검사가 깨지는가"를
    /// 처음 짤 때 <b>전역 계수 하나를 바꿨다</b>. 그랬더니 <b>현행 식과 개정 식이 같이 움직여</b>
    /// 차이가 <c>0</c>으로 나왔고, 대조는 <b>FAIL</b>했다 — 즉 그대로 뒀으면
    /// <b>"불변이 지켜졌다"는 초록과 "아무것도 재지 않았다"는 초록이 똑같이 생길</b> 뻔했다.
    /// 이 저장소가 하루에 아홉 번 당한 바로 그 형태다.
    ///
    /// <para><b>그래서 규칙은 하나다: 변이는 반드시 <u>개정 쪽 한쪽에만</u> 가한다.</b>
    /// 기준(골든)은 절대 함께 움직이지 않는다. 아래 <see cref="양성대조_개정쪽_음절계수만_틀리면_골든과_어긋난다"/>가
    /// 그 형태이고, 이 주석은 다음 사람이 같은 함정에 빠지지 않도록 남긴다.</para>
    ///
    /// <para><b>플랫폼</b>: 완전 중립. <c>DialogueKind.cs</c>에는 <c>#if</c>가 하나도 없고 전부
    /// 초 단위 무차원 값이다. Windows/macOS/iOS 결과가 동일하다.</para>
    /// </summary>
    public sealed class DialogueLanguageBudgetTests
    {
        private const string LogPrefix = "[언어예산-TEST]";

        /// <summary>float 32비트를 <b>있는 그대로</b> 16진으로. 십진 표기는 포매터가 바뀌면 함께 흔들려
        /// "비트 단위"라는 요구를 표현하지 못한다.</summary>
        private static string Bits(float v) =>
            BitConverter.ToInt32(BitConverter.GetBytes(v), 0).ToString("X8", CultureInfo.InvariantCulture);

        private static string Sec(float v) => v.ToString("F6", CultureInfo.InvariantCulture);

        /// <summary>골든의 16진 비트를 다시 float으로. <b>기대값을 골든에 직접 묶기 위한</b> 것이다 —
        /// 프로덕션 함수로 기대값을 만들면 그 함수가 틀어질 때 기대값도 같이 틀어진다.</summary>
        private static float FromBits(string hex) =>
            BitConverter.ToSingle(BitConverter.GetBytes(
                int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)), 0);

        /// <summary>
        /// 프로덕션 식의 <b>모사판</b> — 계수 둘을 인자로 받는다. 상수는 <b>참조</b>한다(숫자 베끼기 금지).
        /// <para>양성 대조는 이 함수의 인자만 흔든다. 골든은 절대 함께 흔들리지 않는다(위 주석).</para>
        /// </summary>
        private static float ReadingWith(string text, float perSyllabic, float perLatin)
        {
            int glyphs = string.IsNullOrEmpty(text) ? 0 : text.Length;
            float per = DialogueBudget.IsSyllabicScript(text) ? perSyllabic : perLatin;
            return Mathf.Clamp(DialogueBudget.BaseSeconds + glyphs * per,
                DialogueBudget.MinSeconds, DialogueBudget.MaxSeconds);
        }

        /// <summary>
        /// 영어 표본. <b>기대값이 아니라 "분기가 실제로 걸리는가"를 재기 위한 자극</b>이다
        /// (design-narrative 2026-09-02 R2 §3-4 표의 English 열에서 발췌).
        /// 문안 확정은 <c>design-narrative</c> 소관이고 이 파일은 그 문안을 판정하지 않는다.
        /// </summary>
        private static readonly string[] LatinProbe =
        {
            "Hmm...", "Nice spot.", "Taking a break.", "What now?", "Solid footing.",
            "Out walking.", "Left, right, left.", "Good stride today.",
            "Whoa, that's high", "Whoa... that's deep",
        };

        // ==================================================================================
        // 0. 말뭉치 — 표본이 쪼그라들면 아래 전부가 0건을 훑고 통과한다
        // ==================================================================================

        /// <summary>
        /// ★ 골든과 소스 말뭉치가 <b>양방향으로</b> 일치한다.
        /// <para>한 방향만 보면 대사를 <b>추가</b>했을 때(골든에 없는 줄) 또는 <b>삭제</b>했을 때
        /// (골든에만 있는 줄) 중 한쪽을 놓친다. 둘 다 리뷰어가 봐야 하는 사건이다.</para>
        /// </summary>
        [Test]
        public void 골든과_소스_말뭉치가_양방향으로_일치한다()
        {
            List<string> scanned = DialogueCorpus.ScanDistinct();
            List<DialogueCorpus.GoldenRow> golden = DialogueCorpus.ReadGolden();

            Assert.Greater(scanned.Count, 0, $"{LogPrefix} 소스에서 대사를 하나도 찾지 못했습니다 — " +
                "수집기가 망가졌습니다(이 상태의 모든 '이상 없음'은 무효입니다).");

            var goldenTexts = new HashSet<string>(golden.Select(g => g.Text), StringComparer.Ordinal);
            var scannedTexts = new HashSet<string>(scanned, StringComparer.Ordinal);

            string[] onlyInSource = scannedTexts.Except(goldenTexts, StringComparer.Ordinal).ToArray();
            string[] onlyInGolden = goldenTexts.Except(scannedTexts, StringComparer.Ordinal).ToArray();

            Assert.IsEmpty(onlyInSource,
                $"{LogPrefix} 골든에 없는 대사가 소스에 있습니다: [{string.Join(" / ", onlyInSource)}] — " +
                "대사를 추가했다면 docs/localization/verify/golden_gen.py 로 골든을 다시 구우세요. " +
                "그 diff에서 새 대사의 가독예산을 리뷰어가 직접 봐야 합니다.");
            Assert.IsEmpty(onlyInGolden,
                $"{LogPrefix} 소스에서 사라진 대사가 골든에 남아 있습니다: [{string.Join(" / ", onlyInGolden)}] — " +
                "정말 지운 것이라면 골든도 다시 구우세요. 수집기가 못 본 것이라면 그쪽이 버그입니다.");

            Assert.AreEqual(golden.Count, scanned.Count, $"{LogPrefix} 고유 대사 수가 다릅니다.");
        }

        /// <summary>
        /// ★ 종전 스캐너가 못 보던 <b>사각지대 7줄</b>이 말뭉치에 실제로 들어와 있다.
        /// <para>이 단언이 없으면 누군가 수집기를 옛 형태(<c>States/</c> + <c>DialogueLine.Say|React</c>)로
        /// 되돌려도 <b>골든만 다시 구우면 초록</b>이 된다. 실명으로 못 박는다.</para>
        /// </summary>
        [TestCase("좋아, 감시 시작", "Core/StickmanAgent.cs 집중모드 람다")]
        [TestCase("수고했어!", "Core/StickmanAgent.cs 집중모드 람다")]
        [TestCase("그래 쉬자", "Core/StickmanAgent.cs 집중모드 람다")]
        [TestCase("어? 딴 데 보고 있네?", "Core/StickmanAgent.cs 집중모드 람다")]
        [TestCase("아 몰라...", "Core/StickmanAgent.cs 집중모드 람다")]
        [TestCase("어... 알았어, 갈게", "States/RunawayState.cs TriggerSelfReturn")]
        [TestCase("심심해서 왔어...", "States/RunawayState.cs TriggerSelfReturn")]
        public void 사각지대_대사가_말뭉치에_들어와_있다(string text, string origin)
        {
            CollectionAssert.Contains(DialogueCorpus.ScanDistinct(), text,
                $"{LogPrefix} {origin} 의 \"{text}\" 를 수집기가 못 봤습니다 — " +
                "2026-09-02 이전 스캐너는 이 형태를 못 봐서 대사 33줄 중 7줄(21%)이 " +
                "어떤 회귀 검사에도 닿지 않았습니다. 그 상태로 되돌리지 마세요.");
        }

        /// <summary>
        /// ★★ 수집기 <b>양성 대조</b> — 합성 소스에서 세 형태를 실제로 찾는가, 그리고
        /// <b>형태가 없으면 정말 0인가</b>.
        /// <para>"0건"과 "패턴이 안 맞아서 0건"은 똑같이 생겼다. 실제 트리를 건드리지 않고
        /// 그 둘을 가르는 유일한 방법이 이 합성 입력이다.</para>
        /// </summary>
        [Test]
        public void 양성대조_수집기가_세_형태를_모두_찾고_없으면_0이다()
        {
            const string withAll =
                "void A(){ var i = new DialogueIntent(ctx, _ => DialogueLine.Say(\"세이형태\")); }\n" +
                "void B(){ TriggerSelfReturn(\"자진복귀형태\"); }\n" +
                "var s = new TimedSpectacleState(bb, id, cfg => cfg.hold, cfg => \"람다형태\");\n";

            CollectionAssert.AreEqual(new[] { "세이형태" }, DialogueCorpus.ExtractSayReact(withAll),
                $"{LogPrefix} DialogueLine.Say/React 추출이 동작하지 않습니다.");
            CollectionAssert.AreEqual(new[] { "자진복귀형태" }, DialogueCorpus.ExtractSelfReturn(withAll),
                $"{LogPrefix} TriggerSelfReturn 추출이 동작하지 않습니다(사각지대 1).");
            CollectionAssert.AreEqual(new[] { "람다형태" }, DialogueCorpus.ExtractConfigLambda(withAll),
                $"{LogPrefix} cfg => \"…\" 추출이 동작하지 않습니다(사각지대 2).");

            // ★ 음성 대조 — 형태가 없으면 정말 0이어야 한다(아무거나 긁어오면 위 PASS는 무의미하다).
            const string withNone = "void C(){ var t = \"평범한 리터럴\"; Debug.Log(t); }\n";
            CollectionAssert.IsEmpty(DialogueCorpus.ExtractSayReact(withNone));
            CollectionAssert.IsEmpty(DialogueCorpus.ExtractSelfReturn(withNone));
            CollectionAssert.IsEmpty(DialogueCorpus.ExtractConfigLambda(withNone));
        }

        // ==================================================================================
        // 1. ★★ 절대 조건 — 한국어 결과 비트 단위 불변
        // ==================================================================================

        /// <summary>
        /// ★★ 개정 <b>후</b> <see cref="DialogueBudget.ReadingSeconds"/>가 개정 <b>전</b> 식으로 구운
        /// 골든과 <b>비트 단위로</b> 같다. 실측: 33줄 전수 최대 차이 <c>0.0</c>.
        /// </summary>
        [Test]
        public void 한국어_가독예산이_골든과_비트_단위로_같다()
        {
            var mismatch = new List<string>();
            foreach (DialogueCorpus.GoldenRow row in DialogueCorpus.ReadGolden())
            {
                float actual = DialogueBudget.ReadingSeconds(row.Text);
                if (Bits(actual) != row.Bits || Sec(actual) != row.Seconds)
                {
                    mismatch.Add($"\"{row.Text}\" 골든 {row.Bits}({row.Seconds}) vs 현재 {Bits(actual)}({Sec(actual)})");
                }
            }
            Assert.IsEmpty(mismatch, $"{LogPrefix} 한국어 가독예산이 바뀌었습니다 — " +
                "이 변경은 무효입니다. 문자체계 분기의 절대 조건은 '한국어가 한 톨도 안 바뀐다'입니다.\n  " +
                string.Join("\n  ", mismatch));
        }

        /// <summary>
        /// 구조 동치 — 상수를 <b>참조</b>해 재구성한 구식 식(<c>clamp(Base + N·PerGlyph, Min, Max)</c>)과
        /// 개정 후 결과가 한국어 말뭉치 전수에서 같다.
        /// <para>골든(절대 동결)과 <b>다른 방향</b>의 잠금이다. 골든만 있으면 "상수를 바꾸고 골든도 다시
        /// 구웠다"가 통과하고, 이 검사만 있으면 "식과 검사가 같이 틀어졌다"가 통과한다. 둘 다 있어야 한다.</para>
        /// </summary>
        [Test]
        public void 한국어는_구식_단일계수_식과_구조적으로_동치다()
        {
            foreach (string text in DialogueCorpus.ScanDistinct())
            {
                Assert.IsTrue(DialogueBudget.IsSyllabicScript(text),
                    $"{LogPrefix} \"{text}\" 가 음절 문자체계로 판정되지 않았습니다.");

                float legacy = Mathf.Clamp(
                    DialogueBudget.BaseSeconds + text.Length * DialogueBudget.PerGlyphSeconds,
                    DialogueBudget.MinSeconds, DialogueBudget.MaxSeconds);

                Assert.AreEqual(Bits(legacy), Bits(DialogueBudget.ReadingSeconds(text)),
                    $"{LogPrefix} \"{text}\" 가 구식 식과 다릅니다.");
            }
        }

        /// <summary>
        /// 소비자 경로도 함께 얼린다 — <see cref="DialogueBudget.RequiredDwellSeconds"/>(발화 자격)와
        /// <see cref="DialogueBudget.MinVisibleSecondsFor"/>(최소 노출).
        /// <para><c>MaxVisibleSecondsFor</c>는 <c>ReadingSeconds</c>와 <b>이번 라운드가 건드리지 않은
        /// 상수들</b>만의 함수이므로 <c>ReadingSeconds</c>가 얼면 함께 언다.</para>
        /// </summary>
        [Test]
        public void 한국어_소비자_경로가_골든에서_파생된_값과_같다()
        {
            foreach (DialogueCorpus.GoldenRow row in DialogueCorpus.ReadGolden())
            {
                // ★ 기대값을 <b>골든 비트에서 되살려</b> 만든다. 프로덕션 함수로 만들면 그 함수가
                //   틀어질 때 기대값도 함께 틀어져 아무것도 재지 못한다(이 파일 머리말의 그 함정).
                float frozen = FromBits(row.Bits);

                Assert.AreEqual(Bits(DialogueTiming.FadeInSeconds + frozen),
                    Bits(DialogueBudget.RequiredDwellSeconds(row.Text, DialogueTiming.FadeInSeconds)),
                    $"{LogPrefix} \"{row.Text}\" 의 필요체류가 바뀌었습니다(발화 자격이 달라집니다).");

                Assert.AreEqual(Bits(frozen),
                    Bits(DialogueBudget.MinVisibleSecondsFor(row.Text, DialogueBudget.MinVisibleScale)),
                    $"{LogPrefix} \"{row.Text}\" 의 최소 노출이 가독예산과 어긋납니다(배율 100%).");
            }
        }

        // ==================================================================================
        // 2. ★★★ 양성 대조 — 계수를 틀리게 넣으면 실제로 빨개지는가
        // ==================================================================================

        /// <summary>
        /// 모사판이 진짜와 같음을 먼저 보인다. <b>이게 깨지면 아래 양성 대조 전부가 무의미하다</b>
        /// (교정이 깨지면 그 뒤 숫자를 전부 폐기한다 — TEAM.md 공통 처방).
        /// </summary>
        [Test]
        public void 교정_모사판이_프로덕션과_같은_값을_낸다()
        {
            IEnumerable<string> all = DialogueCorpus.ScanDistinct().Concat(LatinProbe);
            foreach (string text in all)
            {
                Assert.AreEqual(Bits(DialogueBudget.ReadingSeconds(text)),
                    Bits(ReadingWith(text, DialogueBudget.PerGlyphSeconds, DialogueBudget.PerLatinGlyphSeconds)),
                    $"{LogPrefix} 모사판이 프로덕션과 다릅니다: \"{text}\"");
            }
        }

        /// <summary>
        /// ★★★ 양성 대조 #1 — <b>개정 쪽 음절 계수만</b> 1틱 틀리게 하면 골든과 어긋나는가.
        ///
        /// <para><b>변이를 한쪽에만 가하는 것이 이 검사의 전부다.</b> 설계 단계에서 전역 계수 하나를
        /// 바꿨더니 기준과 대상이 <b>같이 움직여</b> 차이가 0으로 나왔고 대조는 실패했다 —
        /// 그대로 뒀으면 "불변이 지켜졌다"와 "아무것도 재지 않았다"가 <b>똑같이 초록</b>이었다.
        /// 여기서는 기준이 <b>디스크의 골든</b>이라 구조적으로 함께 움직일 수 없다.</para>
        ///
        /// <para>실측(<c>docs/localization/verify/gate.out.txt</c>): 33줄 중 <b>28줄</b>이 달라진다.
        /// 나머지 5줄은 <see cref="DialogueBudget.MinSeconds"/> 하한에 걸려 있어 1틱으로는 안 움직인다 —
        /// 그래서 기대값은 "전부"가 아니라 <b>과반</b>이다.</para>
        /// </summary>
        [Test]
        public void 양성대조_개정쪽_음절계수만_틀리면_골든과_어긋난다()
        {
            float oneTickOff = DialogueBudget.PerGlyphSeconds + 0.0001f;   // 0.075 -> 0.0751
            Assert.AreNotEqual(Bits(DialogueBudget.PerGlyphSeconds), Bits(oneTickOff),
                $"{LogPrefix} 변이 자체가 값을 바꾸지 못했습니다(대조가 성립하지 않습니다).");

            List<DialogueCorpus.GoldenRow> golden = DialogueCorpus.ReadGolden();
            int differ = golden.Count(row =>
                Bits(ReadingWith(row.Text, oneTickOff, DialogueBudget.PerLatinGlyphSeconds)) != row.Bits);

            Assert.Greater(differ, golden.Count / 2,
                $"{LogPrefix} 음절 계수를 1틱 틀렸는데 골든과 어긋난 줄이 {differ}/{golden.Count}뿐입니다 — " +
                "불변 검사가 사실상 아무것도 재고 있지 않다는 뜻입니다(거짓 초록).");
        }

        /// <summary>
        /// ★★ 양성 대조 #2 — <b>분기를 지우면</b>(라틴에도 음절 계수를 쓰면) 한국어는 그대로인데
        /// 영어만 전부 달라지는가. <b>분기가 영어에만 걸린다</b>는 것을 양방향으로 증명한다.
        /// </summary>
        [Test]
        public void 양성대조_분기를_지우면_한국어는_그대로이고_영어만_달라진다()
        {
            float single = DialogueBudget.PerGlyphSeconds;

            foreach (DialogueCorpus.GoldenRow row in DialogueCorpus.ReadGolden())
            {
                Assert.AreEqual(row.Bits, Bits(ReadingWith(row.Text, single, single)),
                    $"{LogPrefix} 분기를 지웠는데 한국어 \"{row.Text}\" 가 달라졌습니다 — " +
                    "분기가 한국어 경로에도 걸려 있다는 뜻입니다.");
            }

            foreach (string en in LatinProbe)
            {
                Assert.AreNotEqual(Bits(ReadingWith(en, single, single)),
                    Bits(DialogueBudget.ReadingSeconds(en)),
                    $"{LogPrefix} 분기를 지웠는데 영어 \"{en}\" 가 그대로입니다 — 분기가 안 걸립니다.");
            }
        }

        /// <summary>
        /// ★ 양성 대조 #3 — 계수를 <b>고치지 않았다면</b> 영어가 실제로 침묵했는가.
        /// 이 라운드의 존재 이유 자체를 회귀로 남긴다.
        /// <para>분모는 <c>design-motion</c> 2026-09-02 R4 §3-3이 보증한 <c>ParkourClimb = 1.20초</c>
        /// (<c>parkourClimbDuration</c> 상수, 지터 없음). 여기서는 <b>기대값이 아니라 자극</b>으로 쓴다 —
        /// 판정은 "개정 전에는 못 하고 개정 후에는 한다"는 <b>부호</b>뿐이다.</para>
        /// </summary>
        [Test]
        public void 양성대조_계수를_고치지_않으면_영어가_침묵한다()
        {
            const string en = "Whoa, that's high";
            const float climbDwell = 1.20f;   // 자극용. 기대값이 아니다(위 주석).
            float single = DialogueBudget.PerGlyphSeconds;

            float before = DialogueTiming.FadeInSeconds + ReadingWith(en, single, single);
            float after = DialogueBudget.RequiredDwellSeconds(en, DialogueTiming.FadeInSeconds);

            Assert.Greater(before, climbDwell,
                $"{LogPrefix} 개정 전 식에서 \"{en}\" 가 침묵하지 않습니다 — 이 라운드의 전제가 무너집니다.");
            Assert.Less(after, climbDwell,
                $"{LogPrefix} 개정 후에도 \"{en}\" 가 침묵합니다 — 계수 분기가 동작하지 않습니다.");
        }

        // ==================================================================================
        // 3. 문자체계 판정 자체
        // ==================================================================================

        [TestCase("헉... 높다", true, "한글 음절")]
        [TestCase("음...", true, "한글 음절 + 마침표")]
        [TestCase("ㅋㅋㅋ", true, "한글 호환 자모")]
        [TestCase("こんにちは", true, "히라가나")]
        [TestCase("カタカナ", true, "가타카나")]
        [TestCase("漢字", true, "CJK 통합한자")]
        [TestCase("Wi-Fi 끊겼네", true, "★ 혼합 — 비싼 쪽으로 간다")]
        [TestCase("Whoa, that's high", false, "라틴 전용")]
        [TestCase("Review PR", false, "사용자가 입력한 영문")]
        [TestCase("...", false, "기호 전용 — 하한이 흡수한다")]
        [TestCase("9+", false, "숫자")]
        [TestCase("", true, "빈 문자열 — 기존 경로 유지")]
        public void 문자체계_판정(string text, bool expectSyllabic, string why)
        {
            Assert.AreEqual(expectSyllabic, DialogueBudget.IsSyllabicScript(text),
                $"{LogPrefix} \"{text}\" ({why})");
        }

        /// <summary>
        /// ★ 혼합 문자열은 <b>비싼 쪽</b>(음절 계수)으로 청구된다.
        /// <para>과다 청구의 결과는 <b>침묵</b>이고, 이 저장소에서 안전한 실패는 침묵이지
        /// 조기 소멸이 아니다. 반대로 라틴 계수로 갔다면 한국어가 <b>읽히기 전에 사라진다</b>.</para>
        /// </summary>
        [Test]
        public void 혼합_문자열은_음절_계수로_청구된다()
        {
            const string mixed = "Wi-Fi 끊겼네";
            float legacy = Mathf.Clamp(
                DialogueBudget.BaseSeconds + mixed.Length * DialogueBudget.PerGlyphSeconds,
                DialogueBudget.MinSeconds, DialogueBudget.MaxSeconds);
            Assert.AreEqual(Bits(legacy), Bits(DialogueBudget.ReadingSeconds(mixed)),
                $"{LogPrefix} 혼합 문자열이 라틴 계수로 갔습니다 — 한국어가 조기 소멸합니다.");

            // 라틴 계수로 갔다면 실제로 값이 달라진다는 것까지 보인다(단언이 공허하지 않음을 증명).
            Assert.AreNotEqual(Bits(legacy),
                Bits(ReadingWith(mixed, DialogueBudget.PerLatinGlyphSeconds, DialogueBudget.PerLatinGlyphSeconds)),
                $"{LogPrefix} 두 계수가 같은 값을 낸다면 위 단언은 아무것도 재지 않습니다.");
        }

        /// <summary>라틴 전용은 라틴 계수 식과 정확히 같다.</summary>
        [Test]
        public void 라틴_전용은_라틴_계수_식과_같다()
        {
            foreach (string en in LatinProbe)
            {
                float expected = Mathf.Clamp(
                    DialogueBudget.BaseSeconds + en.Length * DialogueBudget.PerLatinGlyphSeconds,
                    DialogueBudget.MinSeconds, DialogueBudget.MaxSeconds);
                Assert.AreEqual(Bits(expected), Bits(DialogueBudget.ReadingSeconds(en)),
                    $"{LogPrefix} \"{en}\"");
            }
        }

        /// <summary>
        /// ★ 계수 비율이 유도값과 같다 — <c>0.0472 / 0.075 = 0.629</c>
        /// (실재 대사 17줄 말뭉치의 한글 112자 / 영어 178자).
        /// <para>두 상수를 <b>참조</b>해서 검산한다. 누가 한쪽만 조용히 옮기면 여기서 걸린다.</para>
        /// </summary>
        [Test]
        public void 계수_비율이_말뭉치_유도값과_같다()
        {
            const float derived = 112f / 178f;   // design-narrative 2026-09-02 R2 §5-3
            float ratio = DialogueBudget.PerLatinGlyphSeconds / DialogueBudget.PerGlyphSeconds;
            Assert.AreEqual(derived, ratio, 0.001f,
                $"{LogPrefix} 계수 비율 {ratio:F4} 가 유도값 {derived:F4} 와 어긋납니다 — " +
                "한쪽 상수만 바뀌었을 가능성이 큽니다. design/narrative/verify/en_budget.out.txt 참고.");
        }

        // ==================================================================================
        // 4. ★ 사용자가 입력한 문자열 — 한국어 사용자도 값이 바뀌는 유일한 지점
        // ==================================================================================

        /// <summary>
        /// ★★ 할일 리마인더 대사는 <b>사용자가 타이핑한 원문</b>이다
        /// (<c>Interaction/TodoReminderDirector.cs</c> → <c>TodoListModel.SetPendingReminderText</c> →
        /// <c>ConsumePendingReminderText</c> → 말풍선).
        ///
        /// <para><b>그래서 게이트가 로케일을 보면 안 된다.</b> 로케일 기준이었다면 한국어 UI를 쓰는
        /// 사용자가 <b>영어로 적은 할일</b>이 음절 계수로 과다 청구되어 침묵한다 — 그건 우리가 만든
        /// 버그가 아니라 <b>사용자가 쓴 글자를 우리가 검열하는 것</b>이다.</para>
        ///
        /// <para>이 줄은 골든에 넣지 않는다(사용자 데이터라 동결할 수 없다). 대신 <b>거동 자체</b>를
        /// 여기서 못 박는다: 같은 사용자, 같은 설정, <b>글자만 영어면 라틴 계수를 쓴다</b>.</para>
        /// </summary>
        [Test]
        public void 사용자가_입력한_영문_할일도_라틴_계수를_쓴다()
        {
            const string englishTodo = "Review PR";
            const string koreanTodo = "리뷰 확인하기";

            Assert.IsFalse(DialogueBudget.IsSyllabicScript(englishTodo));
            Assert.IsTrue(DialogueBudget.IsSyllabicScript(koreanTodo));

            float latin = Mathf.Clamp(
                DialogueBudget.BaseSeconds + englishTodo.Length * DialogueBudget.PerLatinGlyphSeconds,
                DialogueBudget.MinSeconds, DialogueBudget.MaxSeconds);
            Assert.AreEqual(Bits(latin), Bits(DialogueBudget.ReadingSeconds(englishTodo)),
                $"{LogPrefix} 영문 할일이 라틴 계수를 쓰지 않습니다.");

            float syllabic = Mathf.Clamp(
                DialogueBudget.BaseSeconds + koreanTodo.Length * DialogueBudget.PerGlyphSeconds,
                DialogueBudget.MinSeconds, DialogueBudget.MaxSeconds);
            Assert.AreEqual(Bits(syllabic), Bits(DialogueBudget.ReadingSeconds(koreanTodo)),
                $"{LogPrefix} 한글 할일이 음절 계수를 쓰지 않습니다.");
        }

        /// <summary>
        /// 빈 문자열/null은 하한으로 떨어지고 <b>두 경로가 같은 값</b>이다(분기가 여기서 갈리지 않는다).
        /// </summary>
        [Test]
        public void 빈_문자열은_하한이고_분기와_무관하다()
        {
            Assert.AreEqual(Bits(DialogueBudget.MinSeconds), Bits(DialogueBudget.ReadingSeconds(null)));
            Assert.AreEqual(Bits(DialogueBudget.MinSeconds), Bits(DialogueBudget.ReadingSeconds(string.Empty)));
        }
    }
}
