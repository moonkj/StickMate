using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 붙잡힘(<c>Dragged</c>) 반응 대사 계약 — 2026-09-02 사용자 요청으로 신설된 9줄.
    /// 설계: <c>design-narrative</c> R7 §2 / 구현: <c>Dialogue/GrabReactionLines.cs</c> ·
    /// <c>States/DragThrowState.cs</c>.
    ///
    /// ============================================================================
    /// ★ 이 파일이 여는 순서 — <b>수집기가 살아 있음을 먼저 증명하고, 그 다음에 대사를 센다</b>
    /// ============================================================================
    /// 새 대사는 <c>AmbientChatter</c>와 같은 <b>배열</b> 형태라
    /// <c>DialogueLine.Say|React("…")</c> 리터럴 스캐너가 <b>구조적으로 못 본다</b>.
    /// 그래서 <see cref="DialogueCorpus"/>에 수집 경로를 새로 넓혔는데, 그 경로가 죽어 있으면
    /// <b>"대사가 골든과 일치한다"는 초록이 아무것도 재지 않은 초록</b>이 된다 —
    /// 이 저장소가 반복해서 당한 형태다(실패한 측정과 성공한 측정이 똑같이 생겼다).
    ///
    /// <para>그래서 <see cref="양성대조_배열수집기가_있는표를_찾고_없는_표는_실패한다"/>가
    /// <b>먼저</b> 온다: 합성 타입에 심어 둔 표를 실제로 읽어 오는가, 그리고 <b>없으면 정말 false인가</b>.
    /// 이게 통과한 뒤에야 아래의 "9줄이 말뭉치에 있다"가 의미를 갖는다.</para>
    ///
    /// ============================================================================
    /// ★ 계수 회귀에는 이 풀을 쓰지 마라 (design-narrative R7 §6 경고)
    /// ============================================================================
    /// 9줄 중 <b>6줄이 가독예산 클램프 바닥</b>(<c>DialogueBudget.MinSeconds</c>)에 붙어 있다.
    /// <c>PerGlyphSeconds</c>를 아무 값으로 흔들어도 그 6줄의 예산은 <b>한 톨도 안 변한다</b> —
    /// 이 표본으로 계수 회귀를 짜면 <b>검증은 초록인데 아무것도 못 잰다</b>.
    /// 계수 회귀는 <c>DialogueLanguageBudgetTests</c>가 <b>골든 전수</b>(클램프 위 줄 포함)로 짠다.
    /// <b>여기서는 계수를 재지 않는다</b> — 못 재는 것을 재는 척하지 않는다.
    ///
    /// <para><b>플랫폼</b>: 완전 중립. <c>#if</c>도, 플랫폼 API도 쓰지 않는다(문자열 + 비율).</para>
    /// </summary>
    public sealed class GrabReactionDialogueTests
    {
        private const string LogPrefix = "[붙잡힘대사-TEST]";

        /// <summary>실제 트리의 대사표 이름들. <b>수집기와 같은 이름을 쓴다</b> —
        /// 표 이름이 바뀌면 <see cref="DialogueCorpus.GrabLines"/>가 시끄럽게 실패한다.</summary>
        private static readonly string[] GrabTableNames = { "HeadLines", "LegLines", "AnyLines", "FallbackLines" };

        private GameObject _metricsHost;

        [TearDown]
        public void CleanUp()
        {
            if (_metricsHost != null)
            {
                Object.DestroyImmediate(_metricsHost);
                _metricsHost = null;
            }
        }

        // ==================================================================================
        // 0. ★ 수집기가 살아 있는가 — 이게 먼저다
        // ==================================================================================

        /// <summary>합성 타입에 심어 둔 표를 <b>실제로</b> 읽는가, 그리고 <b>없으면 정말 false인가</b>.</summary>
        [Test]
        public void 양성대조_배열수집기가_있는표를_찾고_없는_표는_실패한다()
        {
            // 양성 — 있는 것을 찾는다.
            Assert.IsTrue(DialogueCorpus.TryReadStringArray(typeof(ProbeTables), "AliveLines", out string[] alive),
                $"{LogPrefix} 합성 표를 못 읽었습니다 — 이 수집기의 모든 '찾았다'가 무효입니다.");
            CollectionAssert.AreEqual(new[] { "합성대사1", "합성대사2" }, alive,
                $"{LogPrefix} 합성 표의 내용이 다릅니다(엉뚱한 필드를 읽고 있습니다).");

            // 음성 1 — 이름이 없으면 false. (아무거나 긁어오면 위 양성은 무의미하다.)
            Assert.IsFalse(DialogueCorpus.TryReadStringArray(typeof(ProbeTables), "존재하지않는표", out string[] missing));
            Assert.IsNull(missing);

            // 음성 2 — 이름이 맞아도 string[]이 아니면 false(형태까지 본다).
            Assert.IsFalse(DialogueCorpus.TryReadStringArray(typeof(ProbeTables), "NotStrings", out _));

            // 음성 3 — 비어 있는 표는 "읽었다"로 치지 않는다. 0건을 조용히 통과시키는 것이 최악이다.
            Assert.IsFalse(DialogueCorpus.TryReadStringArray(typeof(ProbeTables), "EmptyLines", out _));
        }

        /// <summary>그리고 <b>실제 프로덕션 표</b> 넷을 전부 읽어 낸다(합성만 되고 실물은 안 되는 경우 차단).</summary>
        [Test]
        public void 수집기가_실제_붙잡힘_표_넷을_전부_읽는다()
        {
            foreach (string table in GrabTableNames)
            {
                string[] lines = DialogueCorpus.GrabLines(table);
                Assert.Greater(lines.Length, 0, $"{LogPrefix} GrabReactionLines.{table}이 비어 있습니다.");
                CollectionAssert.AllItemsAreNotNull(lines);
                CollectionAssert.AllItemsAreUnique(lines, $"{LogPrefix} {table} 안에 같은 줄이 두 번 있습니다.");
            }
        }

        // ==================================================================================
        // 1. 말뭉치·골든 도달 — 위가 통과한 뒤에만 의미가 있다
        // ==================================================================================

        /// <summary>
        /// 9줄이 <b>말뭉치</b>와 <b>골든</b>에 전부 도달했다. <b>기대값을 숫자로 적지 않는다</b> —
        /// 프로덕션 표 자체를 기대값으로 쓴다(줄이 늘거나 줄면 이 검사가 자동으로 따라간다).
        /// </summary>
        [Test]
        public void 붙잡힘_대사가_말뭉치와_골든에_전부_들어와_있다()
        {
            List<string> corpus = DialogueCorpus.ScanDistinct();
            var goldenTexts = new HashSet<string>(DialogueCorpus.ReadGolden().Select(r => r.Text));

            foreach (string table in GrabTableNames)
            {
                foreach (string line in DialogueCorpus.GrabLines(table))
                {
                    CollectionAssert.Contains(corpus, line,
                        $"{LogPrefix} \"{line}\"({table})를 말뭉치가 못 봤습니다 — 배열 형태는 리터럴 " +
                        "스캐너에 안 걸립니다. DialogueCorpus의 수집 경로를 되돌리지 마세요.");
                    Assert.IsTrue(goldenTexts.Contains(line),
                        $"{LogPrefix} \"{line}\"이 골든에 없습니다 — " +
                        "docs/localization/verify/golden_gen.py 로 골든을 다시 구우세요.");
                }
            }
        }

        // ==================================================================================
        // 2. ★★ 종류 — 여기서 설계가 갈렸다
        // ==================================================================================

        /// <summary>
        /// ★★ 붙잡힘 대사는 <b>전부 Reaction</b>이다. 하나라도 Narrative가 되면
        /// <b>0.1초짜리 클릭이 번쩍임</b>이 된다 — <c>Dragged</c>의 계획 잔여 체류가 정의상 NaN이라
        /// 발화 자격 게이트(규칙 8)가 <b>막지 못하기 때문</b>이다.
        /// <para>전 구역 × 전 인덱스를 훑는다(한 줄만 몰래 Narrative로 바뀌는 것을 막는다).</para>
        /// </summary>
        [Test]
        public void 붙잡힘_대사는_전부_반응이다()
        {
            int checkedLines = 0;
            foreach (GrabReactionLines.GrabZone zone in System.Enum.GetValues(typeof(GrabReactionLines.GrabZone)))
            {
                foreach (bool hasGrabPoint in new[] { true, false })
                {
                    int size = GrabReactionLines.PoolSizeFor(zone, hasGrabPoint);
                    Assert.Greater(size, 0, $"{LogPrefix} 구역 {zone}(확신={hasGrabPoint})의 풀이 비었습니다 — " +
                        "Resolve가 빈 문자열을 돌려주게 됩니다.");
                    for (int i = 0; i < size; i++)
                    {
                        DialogueLine line = Resolve(zone, hasGrabPoint, i);
                        Assert.AreEqual(DialogueKind.Reaction, line.Kind,
                            $"{LogPrefix} \"{line.Text}\"(구역 {zone}, {i}번)가 Reaction이 아닙니다.");
                        Assert.IsNotEmpty(line.Text, $"{LogPrefix} 구역 {zone}의 {i}번이 빈 문자열입니다.");
                        checkedLines++;
                    }
                }
            }
            Assert.Greater(checkedLines, 0, $"{LogPrefix} 아무 줄도 검사하지 않았습니다(0건 초록).");
        }

        // ==================================================================================
        // 3. ★★ 「발끝을 잡았다」와 「커서를 못 읽었다」가 갈리는가
        // ==================================================================================

        /// <summary>
        /// ★★ 이 저장소의 대표 사고 형태를 그대로 겨눈다 — <c>_grabOffset == Vector2.zero</c>는
        /// <b>「발끝을 정확히 잡았다」와 「커서를 못 읽었다」가 똑같이 생겼다.</b>
        ///
        /// <para><b>같은 높이비(0)</b>를 주고 <c>HasGrabPoint</c>만 갈랐을 때 <b>결과가 달라져야 한다.</b>
        /// 같다면 그 플래그는 아무것도 하지 않는 장식이고, 좌표 조회 실패가 전부 「다리 놔」로 위장한다.</para>
        /// </summary>
        [Test]
        public void 부위확신_플래그가_실제로_결과를_가른다()
        {
            var leg = new GrabReactionLines.GrabParams
            {
                GrabHeightRatio = 0f,
                Zone = GrabReactionLines.GrabZone.Leg,
                HasGrabPoint = true,
                LineIndex = 0,
            };
            var unknown = new GrabReactionLines.GrabParams
            {
                GrabHeightRatio = 0f,               // ★ 완전히 같은 값이다.
                Zone = GrabReactionLines.GrabZone.Unknown,
                HasGrabPoint = false,
                LineIndex = 0,
            };

            string legLine = GrabReactionLines.Resolve(StickmanStateId.Dragged, leg).Text;
            string unknownLine = GrabReactionLines.Resolve(StickmanStateId.Dragged, unknown).Text;

            Assert.AreNotEqual(legLine, unknownLine,
                $"{LogPrefix} 높이비가 같을 때 부위확신 유무가 결과를 가르지 못했습니다 — " +
                "커서 조회 실패가 「발끝을 잡았다」로 위장합니다.");
            CollectionAssert.Contains(DialogueCorpus.GrabLines("LegLines"), legLine,
                $"{LogPrefix} 다리 구역 0번이 다리 줄이 아닙니다.");
            CollectionAssert.Contains(DialogueCorpus.GrabLines("FallbackLines"), unknownLine,
                $"{LogPrefix} 부위 불명인데 폴백이 아닙니다.");
        }

        /// <summary>부위확신이 없으면 <b>구역이 무엇이든</b> 폴백만 쓴다(잠금이 둘인지 확인).</summary>
        [Test]
        public void 부위확신이_없으면_어느_구역이든_폴백만_쓴다()
        {
            string[] fallback = DialogueCorpus.GrabLines("FallbackLines");
            foreach (GrabReactionLines.GrabZone zone in System.Enum.GetValues(typeof(GrabReactionLines.GrabZone)))
            {
                for (int i = 0; i < 8; i++)   // 인덱스가 풀 크기를 넘어도 안전해야 한다(감싸기).
                {
                    CollectionAssert.Contains(fallback, Resolve(zone, false, i).Text,
                        $"{LogPrefix} 부위확신 없음인데 구역 {zone}의 {i}번이 폴백이 아닙니다.");
                }
            }

            // 파라미터가 아예 없는 경로(대사 파이프라인이 스냅샷을 못 실은 경우)도 같은 자리로 떨어진다.
            CollectionAssert.Contains(fallback, GrabReactionLines.Resolve(StickmanStateId.Dragged, null).Text);
        }

        /// <summary>
        /// ★ 폴백은 <b>일부러 1줄</b>이다. 좌표 프로브가 조용히 죽으면 캐릭터가 항상 같은 말만 하게 되어
        /// <b>증상이 화면에 드러난다</b>. 줄을 더 넣으면 프로브 사망이 다양성 뒤에 숨는다 —
        /// 이 단언은 그 설계 결정을 못 박는 것이지 문안 취향이 아니다.
        /// </summary>
        [Test]
        public void 폴백은_일부러_한줄이다()
        {
            Assert.AreEqual(1, DialogueCorpus.GrabLines("FallbackLines").Length,
                $"{LogPrefix} 폴백이 1줄이 아닙니다 — design-narrative R7 §2-5의 카나리아 설계를 " +
                "되돌리려면 그 문서를 먼저 고치세요.");
        }

        // ==================================================================================
        // 4. 구역 — 경계는 StickmanMetrics 랜드마크에서 온다(숫자를 베끼지 않는다)
        // ==================================================================================

        /// <summary>
        /// 구역 경계가 <b>머리 링 아랫끝</b>과 <b>고관절</b>에서 오는지 확인한다.
        /// <b>0.80657 / 0.41091 같은 숫자를 여기 적지 않는다</b> — <see cref="StickmanMetrics"/>가
        /// 실측한 값을 참조한다(조형이 바뀌면 기준과 대상이 함께 움직인다).
        ///
        /// <para>★ <b>어깨는 경계가 아니다.</b> 어깨를 썼다면 어깨~머리링 사이(=목)를 "머리"라고
        /// 부르게 된다. 그래서 어깨 높이를 잡으면 <b>몸통</b>이어야 한다.</para>
        /// </summary>
        [Test]
        public void 구역_경계는_머리링_아랫끝과_고관절이다()
        {
            StickmanMetrics metrics = BuildMetrics();
            float total = metrics.TotalHeight;
            Assert.Greater(total, 0f, $"{LogPrefix} 신장이 0입니다 — 아래 비율이 전부 무의미해집니다.");

            float headBottom = (metrics.HeadCenterLocalY - metrics.HeadRadius) / total;
            float hip = metrics.HipLocalY / total;
            float shoulder = metrics.ShoulderLocalY / total;

            Assert.Greater(headBottom, shoulder,
                $"{LogPrefix} 머리 링 아랫끝이 어깨보다 낮습니다 — 이 리그의 치수가 뒤집혔습니다.");
            Assert.Greater(shoulder, hip, $"{LogPrefix} 어깨가 고관절보다 낮습니다.");

            const float eps = 0.001f;
            Assert.AreEqual(GrabReactionLines.GrabZone.Head, GrabReactionLines.Classify(1f, headBottom, hip));
            Assert.AreEqual(GrabReactionLines.GrabZone.Head,
                GrabReactionLines.Classify(headBottom, headBottom, hip), $"{LogPrefix} 경계값은 머리 쪽에 포함된다.");
            Assert.AreEqual(GrabReactionLines.GrabZone.Torso,
                GrabReactionLines.Classify(headBottom - eps, headBottom, hip));

            // ★ 목은 머리가 아니다.
            Assert.AreEqual(GrabReactionLines.GrabZone.Torso,
                GrabReactionLines.Classify(shoulder, headBottom, hip),
                $"{LogPrefix} 어깨 높이를 「머리」로 불렀습니다 — 경계가 어깨로 잘못 옮겨졌습니다.");

            Assert.AreEqual(GrabReactionLines.GrabZone.Torso,
                GrabReactionLines.Classify(hip, headBottom, hip), $"{LogPrefix} 고관절은 몸통 쪽에 포함된다.");
            Assert.AreEqual(GrabReactionLines.GrabZone.Leg,
                GrabReactionLines.Classify(hip - eps, headBottom, hip));
            Assert.AreEqual(GrabReactionLines.GrabZone.Leg,
                GrabReactionLines.Classify(0f, headBottom, hip), $"{LogPrefix} 발끝은 다리다.");
        }

        /// <summary>경계가 말이 안 되면 <b>부위를 주장하지 않는다</b>(모르는 것을 아는 척하지 않는다).</summary>
        [TestCase(float.NaN, 0.8f, 0.4f, "높이비가 NaN")]
        [TestCase(0.5f, float.NaN, 0.4f, "머리 경계가 NaN")]
        [TestCase(0.5f, 0.8f, float.NaN, "고관절 경계가 NaN")]
        [TestCase(0.5f, 0.4f, 0.8f, "경계 순서가 뒤집힘")]
        [TestCase(0.5f, 0.8f, 0f, "고관절이 0(측정 실패)")]
        [TestCase(float.PositiveInfinity, 0.8f, 0.4f, "높이비가 무한")]
        public void 경계가_말이_안되면_구역을_주장하지_않는다(float h, float headBottom, float hip, string why)
        {
            Assert.AreEqual(GrabReactionLines.GrabZone.Unknown,
                GrabReactionLines.Classify(h, headBottom, hip), $"{LogPrefix} {why}");
        }

        // ==================================================================================
        // 5. 구역별 풀 — 머리를 잡았는데 다리 대사가 나오지 않는가
        // ==================================================================================

        [Test]
        public void 구역별_풀이_다른_구역의_부위를_말하지_않는다()
        {
            string[] head = DialogueCorpus.GrabLines("HeadLines");
            string[] leg = DialogueCorpus.GrabLines("LegLines");
            string[] any = DialogueCorpus.GrabLines("AnyLines");

            AssertZonePool(GrabReactionLines.GrabZone.Head, head, leg, any);
            AssertZonePool(GrabReactionLines.GrabZone.Leg, leg, head, any);

            // 몸통은 부위를 주장하지 않는다 — 공통 줄만 나온다.
            int torsoSize = GrabReactionLines.PoolSizeFor(GrabReactionLines.GrabZone.Torso, true);
            Assert.AreEqual(any.Length, torsoSize,
                $"{LogPrefix} 몸통 풀이 공통 줄 수와 다릅니다 — 몸통 전용 줄이 생겼습니까?");
            for (int i = 0; i < torsoSize; i++)
            {
                CollectionAssert.Contains(any, Resolve(GrabReactionLines.GrabZone.Torso, true, i).Text,
                    $"{LogPrefix} 몸통 구역이 공통 줄이 아닌 것을 말했습니다.");
            }
        }

        /// <summary>한 구역의 풀 = 그 구역 전용 줄 + 공통 줄. 다른 구역의 전용 줄은 <b>절대</b> 안 나온다.</summary>
        private static void AssertZonePool(GrabReactionLines.GrabZone zone, string[] own, string[] other, string[] any)
        {
            int size = GrabReactionLines.PoolSizeFor(zone, true);
            Assert.AreEqual(own.Length + any.Length, size,
                $"{LogPrefix} {zone} 풀 크기가 (전용 {own.Length} + 공통 {any.Length})와 다릅니다.");

            var seen = new HashSet<string>();
            for (int i = 0; i < size; i++)
            {
                string text = Resolve(zone, true, i).Text;
                seen.Add(text);
                CollectionAssert.DoesNotContain(other, text,
                    $"{LogPrefix} {zone}를 잡았는데 다른 부위 대사 \"{text}\"가 나왔습니다 — " +
                    "이건 원칙 1(행동-텍스트 싱크) 위반입니다.");
            }
            Assert.AreEqual(size, seen.Count,
                $"{LogPrefix} {zone} 풀에서 서로 다른 줄이 {seen.Count}개뿐입니다 — 추첨이 일부 줄에 닿지 못합니다.");
            foreach (string line in own)
            {
                CollectionAssert.Contains(seen, line, $"{LogPrefix} {zone} 전용 줄 \"{line}\"에 추첨이 닿지 않습니다.");
            }
        }

        // ==================================================================================
        // 보조
        // ==================================================================================

        private static DialogueLine Resolve(GrabReactionLines.GrabZone zone, bool hasGrabPoint, int index)
        {
            return GrabReactionLines.Resolve(StickmanStateId.Dragged, new GrabReactionLines.GrabParams
            {
                GrabHeightRatio = float.NaN,   // ★ 일부러 쓸모없는 값 — 텍스트가 여기서 나오면 안 된다.
                Zone = zone,
                HasGrabPoint = hasGrabPoint,
                LineIndex = index,
            });
        }

        /// <summary>치수 조회 창구를 만든다. 계층이 없으면 <see cref="StickmanMetrics"/>가 배율 1.0
        /// 기준 비율로 되메우므로(그 클래스의 폴백 경로) EditMode에서도 <b>같은 비율</b>이 나온다.</summary>
        private StickmanMetrics BuildMetrics()
        {
            _metricsHost = new GameObject("GrabReactionDialogueTests_Metrics");
            return _metricsHost.AddComponent<StickmanMetrics>();
        }

        /// <summary>수집기 양성/음성 대조 전용 합성 표. <b>실제 대사가 아니다</b> —
        /// 말뭉치에 섞이지 않도록 <c>Dialogue/</c>가 아니라 이 테스트 안에만 둔다.</summary>
        private static class ProbeTables
        {
#pragma warning disable CS0414 // 리플렉션으로만 읽는다 — "쓰이지 않음" 경고가 대조의 요점이다.
            private static readonly string[] AliveLines = { "합성대사1", "합성대사2" };
            private static readonly string[] EmptyLines = new string[0];
            private static readonly int[] NotStrings = { 1, 2 };
#pragma warning restore CS0414
        }
    }
}
