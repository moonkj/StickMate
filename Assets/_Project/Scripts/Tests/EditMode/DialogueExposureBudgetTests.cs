using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 말풍선 <b>노출 시간 역전</b> 회귀 테스트(docs/UX_FLOW.md 5절 규칙 4-b) — 2026-09-01 실측.
    ///
    /// ============================================================================
    /// 무엇이 잡혔나
    /// ============================================================================
    /// 하한만 글자수 비례로 바꾸고 상한은 글자수 무관 고정 4초로 남겨 두자, 화면 결과가 개편의
    /// 취지를 <b>정확히 뒤집었다</b>:
    /// <list type="table">
    ///   <item><term>하암...(5자)</term><description>가독예산 0.66초 → 실제 노출 <b>4.14초</b></description></item>
    ///   <item><term>심심하다(4자)</term><description>0.62초 → <b>4.10초</b></description></item>
    ///   <item><term>오늘 뭐 하지(7자)</term><description>0.81초 → <b>4.12초</b></description></item>
    ///   <item><term>창 위는 미끄러워(9자)</term><description>0.96초 → <b>1.45초</b></description></item>
    /// </list>
    /// <b>가장 짧은 대사가 가장 오래, 가장 긴 대사가 가장 짧게</b> 떠 있었다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 성질
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>단조성</b> — 상한이 글자수에 대해 비감소. 이것이 참이면 "짧은 대사가 더 오래
    ///     떠 있는" 역전은 <b>정의상 불가능</b>하다(개별 숫자를 베끼지 않아도 된다).</item>
    ///   <item><b>하한과 싸우지 않음</b> — 상한 ≥ 가독예산. 아니면 규칙 4-b가 규칙 4-b에게 진다.</item>
    ///   <item><b>고정 상한이 아님</b>(네거티브 컨트롤) — 실측 4행의 상한이 서로 달라야 한다.
    ///     구판은 넷 다 4.00초로 같았고, 그래서 어떤 단조성 단언도 "항상 참"이었다.</item>
    /// </list>
    ///
    /// <para>★ 상수를 숫자로 베끼지 않는다. 기대값은 전부
    /// <see cref="DialogueBudget"/>/<see cref="DialogueTiming"/>에서 <b>계산해</b> 만든다
    /// (CLAUDE.md 협업 프로토콜 — 프로덕션 상수 하드코딩 금지).</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 상수가 전부 초 단위라 배율/DPI/OS와 무관하다.</para>
    /// </summary>
    public sealed class DialogueExposureBudgetTests
    {
        /// <summary>실측 표의 네 대사 — 글자수 오름차순.</summary>
        private static readonly string[] MeasuredLines = { "심심하다", "하암...", "오늘 뭐 하지", "창 위는 미끄러워" };

        private static float Cap(string text)
            => DialogueBudget.MaxVisibleSecondsFor(text, DialogueTiming.PopInSeconds, DialogueTiming.FadeOutSeconds);

        [Test]
        public void 노출_상한은_글자수에_대해_단조_비감소다()
        {
            for (int i = 1; i < MeasuredLines.Length; i++)
            {
                string shorter = MeasuredLines[i - 1];
                string longer = MeasuredLines[i];
                Assert.LessOrEqual(shorter.Length, longer.Length, "표본이 글자수 오름차순이어야 한다(사전 조건).");
                Assert.LessOrEqual(Cap(shorter), Cap(longer) + 1e-5f,
                    $"\"{shorter}\"({shorter.Length}자, 상한 {Cap(shorter):F2}초)가 " +
                    $"\"{longer}\"({longer.Length}자, 상한 {Cap(longer):F2}초)보다 오래 떠 있을 수 있다 — " +
                    "실측에서 화면 결과가 개편 취지를 그대로 뒤집은 그 역전이다.");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 상한이 <b>실제로 글자수를 본다</b>는 것. 구판(고정 4초)에서는 네 대사의
        /// 상한이 전부 같아서 위 단조성 단언이 "항상 참"이었다.
        /// </summary>
        [Test]
        public void 네거티브_노출_상한은_대사마다_달라야_한다()
        {
            float shortest = Cap(MeasuredLines[0]);
            float longest = Cap(MeasuredLines[MeasuredLines.Length - 1]);

            Assert.Greater(longest, shortest + 0.2f,
                $"가장 짧은 대사({shortest:F2}초)와 가장 긴 대사({longest:F2}초)의 상한이 사실상 같다 — " +
                "상한이 글자수를 안 보고 있다는 뜻이고, 그러면 단조성 검사는 아무것도 검사하지 않는다.");
        }

        [Test]
        public void 노출_상한은_가독예산보다_항상_길다()
        {
            foreach (string line in MeasuredLines)
            {
                Assert.Greater(Cap(line), DialogueBudget.ReadingSeconds(line),
                    $"\"{line}\"의 상한({Cap(line):F2}초)이 가독예산({DialogueBudget.ReadingSeconds(line):F2}초) " +
                    "이하다 — 상한이 하한을 이겨 버리면 규칙 4-b가 스스로와 싸운다.");
            }

            // 가독예산이 상한(MaxSeconds)에 걸리는 아주 긴 문장에서도 성립해야 한다.
            string veryLong = new string('가', 60);
            Assert.Greater(Cap(veryLong), DialogueBudget.ReadingSeconds(veryLong));
        }

        [Test]
        public void 노출_상한은_등장과_소멸_연출을_읽기_시간_밖으로_뺀다()
        {
            // 식을 그대로 재구성한다 — 숫자를 베끼는 것이 아니라 같은 상수에서 계산한다.
            foreach (string line in MeasuredLines)
            {
                float readable = Cap(line) - DialogueTiming.PopInSeconds - DialogueTiming.FadeOutSeconds;
                Assert.AreEqual(2f * DialogueBudget.ReadingSeconds(line), readable, 1e-4f,
                    $"\"{line}\": 상한에서 등장/소멸 연출을 빼면 가독예산 두 번분이 남아야 한다 " +
                    "(읽고, 캐릭터를 보고, 한 번 더 읽는다 — MaxVisibleSecondsFor의 근거).");
            }
        }

        [Test]
        public void 빈_문자열과_null도_안전하게_처리된다()
        {
            Assert.Greater(Cap(null), 0f);
            Assert.Greater(Cap(string.Empty), 0f);
            Assert.AreEqual(Cap(null), Cap(string.Empty), 1e-6f);
        }

        // ====================================================================
        // ★ 교체 경로의 발화 자격 (2026-09-02) — 이 파일에 없던 칸
        // ====================================================================

        /// <summary>
        /// ★★ 이 파일의 <b>공백</b>이었다. 위 12건은 전부 <b>상한과 게이트</b>만 보고, <b>교체 경로의
        /// 노출 하한</b>은 한 건도 보지 않았다. 그래서 실기에서 <b>0.02초 번쩍임이 두 번 연속</b>
        /// 났는데도 전부 초록이었다(<see cref="DialogueBudget.CanReplaceVisible"/> 문서의 로그).
        ///
        /// <para>기준은 <b>팝인</b>이다 — 그 안에 지워지면 사용자가 본 것은 문장이 아니라 깜빡임이다.
        /// 숫자를 적지 않고 <see cref="DialogueTiming.PopInSeconds"/>에서 계산한다.</para>
        /// </summary>
        [Test]
        public void 팝인도_못_끝낸_대사는_교체되지_않는다()
        {
            float popIn = DialogueTiming.PopInSeconds;

            Assert.IsFalse(DialogueBudget.CanReplaceVisible(0f, popIn, replacesItself: false),
                "방금 뜬 글자(0초)가 교체 가능하다 — 실측 0.02초 번쩍임의 경로 그대로다.");
            Assert.IsFalse(DialogueBudget.CanReplaceVisible(popIn * 0.5f, popIn, replacesItself: false),
                "팝인의 절반밖에 안 지났는데 교체 가능하다 — 글자가 아직 다 커지지도 않았다.");

            Assert.IsTrue(DialogueBudget.CanReplaceVisible(popIn, popIn, replacesItself: false),
                "경계(정확히 팝인)에서 막히면 규칙 5(즉시 교체)가 필요 이상으로 좁아진다.");
            Assert.IsTrue(DialogueBudget.CanReplaceVisible(popIn * 10f, popIn, replacesItself: false));

            // 알 수 없으면 막지 않는다 — 규칙 8과 같은 방향(침묵보다 안전한 쪽).
            Assert.IsTrue(DialogueBudget.CanReplaceVisible(float.NaN, popIn, replacesItself: false));
        }

        /// <summary>
        /// ★ 같은 글자가 자기 자신을 교체하는 것(실기 로그 frame=11110)은 <b>얼마나 오래 떠 있었든</b>
        /// 막는다. 교체하면 노출 시계와 팝인이 리셋돼 화면상 같은 글자가 다시 튀어오르기 때문이다 —
        /// 사용자에게는 렌더 글리치로 읽힌다.
        /// </summary>
        [Test]
        public void 같은_글자가_자기_자신을_교체하지_못한다()
        {
            float popIn = DialogueTiming.PopInSeconds;
            Assert.IsFalse(DialogueBudget.CanReplaceVisible(popIn * 100f, popIn, replacesItself: true),
                "충분히 오래 떠 있었다는 이유로 같은 글자의 자기 교체가 허용됐다 — 화면에서는 같은 " +
                "글자가 다시 튀어오른다(3.38초 → 0.02초로 리셋된 실측 그대로).");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 보호를 빼면(= 옛 거동: 언제나 교체) 실제로 빨개지는가.
        /// 옛 구현을 그대로 적어 두고 <b>둘의 판정이 갈리는 점이 실재</b>함을 보인다. 이 짝이 없으면
        /// 위 두 초록은 "구조적으로 항상 참"일 수도 있다.
        /// </summary>
        [Test]
        public void 네거티브_보호를_빼면_판정이_실제로_갈린다()
        {
            float popIn = DialogueTiming.PopInSeconds;
            // 옛 구현: OnDialogueRequested가 무조건 교체했다.
            const bool oldBehaviour = true;

            var divergent = new List<string>();
            float[] elapsedProbes = { 0f, popIn * 0.1f, popIn * 0.5f, popIn, popIn * 2f };
            foreach (float elapsed in elapsedProbes)
            {
                foreach (bool self in new[] { false, true })
                {
                    if (DialogueBudget.CanReplaceVisible(elapsed, popIn, self) != oldBehaviour)
                        divergent.Add($"노출 {elapsed:F3}초/자기교체={self}");
                }
            }

            Assert.IsNotEmpty(divergent,
                "새 판정이 옛 거동(언제나 교체)과 한 점도 다르지 않다 — 보호가 아무것도 막지 않는다.");
            Debug.Log($"[교체자격-TEST] 옛 거동과 갈리는 점 {divergent.Count}건: {string.Join(", ", divergent)}");
        }

        // ====================================================================
        // 앰비언트 대사표 — "상태가 정의상 참으로 만드는 사실만 말한다"
        // ====================================================================

        /// <summary>
        /// ★ 2026-09-01 — <b>"창 위는 미끄러워"</b>가 폐기됐다. 페르소나 실측이 실제 창이 하나도 없고
        /// Dock 위를 걷는 중에 이 문장을 띄우는 것을 잡았고, 그 위에 두 번째 거짓이 겹쳐 있었다:
        /// 이 저장소에서 <b>미끄러짐은 결함 지표로만</b> 존재한다(<c>WalkFootSlipTests</c>의 문워크
        /// 상한). 즉 어느 발판에 서 있든 문장의 절반은 항상 거짓이었다.
        ///
        /// <para>이 테스트가 잠그는 것은 문자열 자체가 아니라 <b>재발</b>이다 — 자리/물리를 주장하는
        /// 낱말이 대사표에 다시 들어오면 빨간불이 된다. 대체 문구가 글자 수를 유지했다는 것도 함께
        /// 고정한다(글자 수가 바뀌면 가독예산과 발화 자격 게이트 거동이 함께 움직인다).</para>
        /// </summary>
        [Test]
        public void 앰비언트_대사표에_자리나_물리를_주장하는_낱말이_없다()
        {
            // "창"(실제 창일 때만 참) / "발판"(팀 내부 용어) / "미끄러"(정상 동작에서는 정의상 거짓) /
            // "좁"(안전망은 화면 전체 폭이라 거짓).
            string[] banned = { "창", "발판", "미끄러", "좁" };

            foreach (string line in AllAmbientLines())
            {
                foreach (string word in banned)
                {
                    Assert.IsFalse(line.Contains(word),
                        $"앰비언트 대사 \"{line}\"에 \"{word}\"가 들어 있다 — 이 낱말들은 상태가 아니라 " +
                        "그때그때의 자리/물리를 주장하므로 서 있는 곳에 따라 그냥 거짓이 된다 " +
                        "(절대 불변 원칙 1). 자리를 아는 대사를 하려면 ChatterParams에 발판 종류를 " +
                        "Enter() 시점 스냅샷으로 실어야 한다.");
                }
            }
        }

        [Test]
        public void 앰비언트_대사는_전부_발화_자격_게이트를_통과할_수_있는_길이다()
        {
            // 배회 페이즈 최소 길이(26-1 Walk 1.5초)에서 지터 하한을 뺀 값보다 필요체류가 길면,
            // 그 대사는 구조적으로 발화 불가가 된다(도달 불가능한 대사 = 죽은 데이터).
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float jitterFloor = 1f - config.wanderDurationJitterRatio;
                AssertAllReachable(AmbientLines("WalkLines"), "Walk", config.wanderWalkDurationMin * jitterFloor);
                AssertAllReachable(AmbientLines("IdleLines"), "Idle", config.wanderIdleDurationMin * jitterFloor);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static void AssertAllReachable(string[] lines, string label, float shortestPlan)
        {
            foreach (string line in lines)
            {
                float required = DialogueBudget.RequiredDwellSeconds(line, DialogueTiming.FadeInSeconds);
                Assert.Less(required, shortestPlan,
                    $"{label} 대사 \"{line}\"({line.Length}자)의 필요체류 {required:F3}초가 가장 짧은 " +
                    $"{label} 계획 {shortestPlan:F3}초 이상이다 — 규칙 8이 이 대사를 구조적으로 영원히 " +
                    "침묵시킨다(도달 불가능한 대사 = 죽은 데이터).");
            }
        }

        private static IEnumerable<string> AllAmbientLines()
        {
            foreach (string s in AmbientLines("IdleLines")) yield return s;
            foreach (string s in AmbientLines("WalkLines")) yield return s;
        }

        // ============================================================================
        // ★★ 2026-09-02 — 고정 절대 상한(dialogueMaxVisibleSeconds) 폐지 회귀
        // ============================================================================
        //
        // 리더 지시: "유도식 위에 4초 고정 상한이 또 걸려 있다. 0(상한 없음)으로 내려라."
        // 아래 네 검사는 그 변경이 (가) 실제로 적용됐고 (나) 폭주하지 않으며 (다) 노브가 살아 있고
        // (라) **오늘자 거동 변화가 0이라는 사실**까지 함께 박제한다.
        //
        // ★ (라)를 굳이 테스트로 남기는 이유: 지시에 적힌 증상("가장 긴 두 대사가 5.02/5.62초 →
        //   4.00초로 잘린다")이 이 저장소의 코드로는 **재현되지 않는다**. 가독예산이
        //   DialogueBudget.MaxSeconds(2.20초)로 clamp되므로 14자짜리 최장 대사도 유도 상한이
        //   2.96초이고, 4초 상한에 걸리려면 21자 이상이어야 한다. 즉 이번 변경은 증상 제거가 아니라
        //   **함정 제거**다. 그 사실을 값으로 고정해 두지 않으면 다음 사람이 "고쳤는데 화면이 안
        //   바뀐다"를 회귀로 오인한다.

        /// <summary>DialogueBubbleRenderer.ResolveMaxVisibleSeconds()의 결합 규칙을 그대로 옮긴 것.
        /// (private 메서드라 직접 부를 수 없다. 규칙이 한 줄이고 그 한 줄이 이 검사의 대상이다.)</summary>
        private static float CombineCaps(float settingMax, float budgetMax)
            => settingMax > 0f ? Mathf.Min(settingMax, budgetMax) : budgetMax;

        /// <summary>이 저장소의 정적 대사 중 <b>가장 긴</b> 두 건(각 14자). 2026-09-02 전수 조사 결과이며,
        /// 리더 지시가 "잘린다"고 지목한 바로 그 대사들이다.</summary>
        private static readonly string[] LongestShippedLines = { "헥헥... 안 되겠다...", "흥... 그럼 한 입만이다" };

        [Test]
        public void 배포_설정에_고정_절대_상한이_남아_있지_않다()
        {
            var probe = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                AppSettingsModel.ResetForTesting();
                float resolved = AppSettingsModel.ResolveDialogueMaxVisibleSeconds(probe);
                Debug.Log($"[노출상한] 코드 기본값 dialogueMaxVisibleSeconds={probe.dialogueMaxVisibleSeconds:F2} " +
                          $"-> 조회값 {resolved:F2} (0 이하 = 상한 없음).");
                Assert.LessOrEqual(resolved, 0f,
                    "dialogueMaxVisibleSeconds가 0보다 큽니다 — 글자수 유도 상한 위에 고정 상한이 다시 " +
                    "얹혔다는 뜻이고, 그러면 '가장 긴 대사만 조용히 잘리는' 노출 역전 경로가 되살아납니다. " +
                    "(배포 에셋과의 일치는 ConfigAssetDriftLedgerTests가 따로 잠급니다.)");
            }
            finally
            {
                AppSettingsModel.ResetForTesting();
                Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void 상한을_없애도_유도식_자체가_하드_천장을_갖는다()
        {
            // 폭주 방지의 근거 — 가독예산이 MaxSeconds로 clamp되므로 유도 상한에도 천장이 생긴다.
            float ceiling = DialogueTiming.PopInSeconds
                            + 2f * DialogueBudget.MaxSeconds
                            + DialogueTiming.FadeOutSeconds;

            string absurd = new string('가', 500);
            float capOfAbsurd = Cap(absurd);

            Debug.Log($"[노출상한] 500자 합성 텍스트의 유도 상한 {capOfAbsurd:F3}초 (하드 천장 {ceiling:F3}초 " +
                      $"= 팝인 {DialogueTiming.PopInSeconds:F2} + 가독예산 상한 {DialogueBudget.MaxSeconds:F2} x 2 " +
                      $"+ 페이드아웃 {DialogueTiming.FadeOutSeconds:F2}).");

            Assert.AreEqual(ceiling, capOfAbsurd, 1e-4f,
                "아무리 긴 텍스트여도 유도 상한은 하드 천장에서 포화해야 합니다 — 포화하지 않으면 " +
                "고정 상한을 없앤 근거(폭주 불가)가 무너집니다.");
            // ★ '2f'는 프로덕션 상수를 베낀 것이 아니라 위 식의 정의를 그대로 재현한 것이며,
            //   그 정의가 바뀌면 이 단언이 먼저 빨개진다(= 이 검사의 목적).
        }

        [Test]
        public void 네거티브_컨트롤_고정_상한을_되돌리면_긴_텍스트가_실제로_잘린다()
        {
            const float OldFixedCap = 4f; // 2026-09-02에 폐지된 옛 배포 기본값(역사적 값이라 핀으로 적는다).

            // 옛 상한에 실제로 걸리는 길이를 **유도식으로 찾는다**(숫자를 베끼지 않는다).
            string cutText = null;
            for (int n = 1; n <= 200; n++)
            {
                string candidate = new string('가', n);
                if (Cap(candidate) > OldFixedCap) { cutText = candidate; break; }
            }
            Assert.IsNotNull(cutText,
                $"옛 고정 상한 {OldFixedCap:F2}초를 넘는 텍스트를 200자 안에서 찾지 못했습니다 — " +
                "유도식이 바뀌어 이 네거티브 컨트롤이 아무것도 증명하지 못합니다.");

            float budget = Cap(cutText);
            float withOldCap = CombineCaps(OldFixedCap, budget);
            float withNoCap = CombineCaps(0f, budget);

            Debug.Log($"[노출상한] 네거티브 컨트롤 — {cutText.Length}자 텍스트의 유도 상한 {budget:F3}초. " +
                      $"옛 고정 상한 {OldFixedCap:F2} 적용 시 {withOldCap:F3}초(잘림) / 상한 없음 시 {withNoCap:F3}초.");

            Assert.Less(withOldCap, budget,
                "옛 고정 상한을 되돌렸는데도 잘리지 않습니다 — 결합 규칙(짧은 쪽 선택)이 죽었다는 뜻이며, " +
                "그렇다면 이 파일의 다른 단언들도 아무것도 지키지 못합니다.");
            Assert.AreEqual(budget, withNoCap, 1e-4f,
                "상한 없음(0)인데도 값이 깎였습니다 — 0을 '상한 없음'으로 해석하지 않고 있습니다.");
        }

        [Test]
        public void 기록_지금_배포된_가장_긴_대사도_옛_4초_상한에_걸리지_않았다()
        {
            const float OldFixedCap = 4f;

            float worst = 0f;
            string worstLine = null;
            foreach (string line in AllAmbientLines())
            {
                float c = Cap(line);
                if (c > worst) { worst = c; worstLine = line; }
            }
            foreach (string line in LongestShippedLines)
            {
                float c = Cap(line);
                if (c > worst) { worst = c; worstLine = line; }
            }

            Debug.Log($"[노출상한] 배포 대사 중 유도 상한이 가장 긴 것 = \"{worstLine}\"({worstLine.Length}자) " +
                      $"{worst:F3}초. 옛 고정 상한 {OldFixedCap:F2}초와의 여유 {(OldFixedCap - worst):F3}초.\n" +
                      "  → 즉 2026-09-02의 상한 폐지는 **오늘자 화면 거동을 바꾸지 않는다**(함정 제거).");

            Assert.Less(worst, OldFixedCap,
                $"배포 대사 \"{worstLine}\"의 유도 상한({worst:F3}초)이 옛 고정 상한({OldFixedCap:F2}초)을 " +
                "넘었습니다 — 대사가 길어졌다는 뜻입니다. 이제부터는 상한 폐지가 **실제 거동 변화**를 " +
                "만들므로, 그 대사가 그만큼 오래 떠 있어도 되는지 육안으로 다시 판정하세요.");
        }

        private static string[] AmbientLines(string fieldName)
        {
            FieldInfo field = typeof(AmbientChatter).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, $"AmbientChatter.{fieldName}을 찾지 못했다 — 대사표 이름이 바뀌었다면 " +
                "이 테스트도 함께 고쳐야 한다(조용히 0건을 훑고 통과하는 것이 최악이다).");
            var lines = (string[])field.GetValue(null);
            Assert.IsNotNull(lines);
            Assert.Greater(lines.Length, 0, $"{fieldName}이 비어 있다 — 검사가 0건을 훑고 통과했다.");
            return lines;
        }
    }
}
