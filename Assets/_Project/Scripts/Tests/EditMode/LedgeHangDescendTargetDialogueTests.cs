using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 매달리기(<c>LedgeHang</c>) 대사의 <b>「애초에 내려갈 곳이 있는가」 게이트</b> 회귀 —
    /// 2026-09-08 <c>design-narrative</c> 전체 대사 검수에서 올라온 결함.
    ///
    /// ============================================================================
    /// 무엇이 틀렸었나 — <b>0유닛이 두 가지 사실을 동시에 뜻했다</b>
    /// ============================================================================
    /// <c>LedgeHangState.Enter</c>는 내려갈 발판을 못 찾으면(<c>TryFindDescendTarget</c> 실패)
    /// 낙차를 <b>0유닛</b>으로 채운다. 그런데 매핑 함수는 낙차만 보고 «여기로 내려가자» /
    /// «어우... 아찔하네»를 갈랐으므로, <b>0 = 가장 얕은 낙차</b>로 읽혀 «여기로 내려가자»가 나갔다.
    /// 정작 그 경로의 실제 행동은 <b>어디로도 안 내려가고 그냥 <c>Fall</c></b>이다 —
    /// 절대 불변 원칙 1(행동-텍스트 싱크) 위반이다.
    ///
    /// <para>이 저장소가 반복해서 당한 그 형태다: <b>실패한 측정과 성공한 측정이 똑같이 생겼다.</b>
    /// 처방도 이미 있던 것을 그대로 복제했다 —
    /// <see cref="GrabReactionLines.GrabParams.HasGrabPoint"/>(오프셋 0 = 「발끝을 정확히 잡았다」
    /// = 「커서를 못 읽었다」).</para>
    ///
    /// ============================================================================
    /// ★ 이 파일이 <b>문안을 한 글자도 베끼지 않는</b> 이유
    /// ============================================================================
    /// 기대값을 <c>"여기로 내려가자"</c>로 적어 두면, 문안이 바뀌는 날 이 테스트는
    /// <b>게이트가 아니라 문자열을 재게 된다</b>(CLAUDE.md — 테스트에 프로덕션 상수·식별자를
    /// 문자열로 베끼지 않는다). 그래서 기대값을 <b>프로덕션 함수에서 직접 얻어</b> 대조군으로 쓴다:
    /// <c>HasDescendTarget</c>만 <c>true/false</c>로 뒤집고 나머지 축은 완전히 같게 둔 두 호출의
    /// <b>차이</b>가 곧 게이트다. 임계값도 <see cref="LedgeHangState.DeepDescentHeights"/>를 참조한다
    /// (그 상수가 <c>internal</c>인 이유가 이것이다).
    ///
    /// <para><b>네거티브 컨트롤</b>: <c>LedgeHangState.ResolveDialogue</c>의 게이트 한 줄
    /// (<c>if (p == null || !p.HasDescendTarget) …</c>)을 지우면 <c>withTarget == withoutTarget</c>이
    /// 되어 <see cref="내려갈발판이_없으면_얕은낙차_대사로_위장하지_않는다"/>가 빨개진다.
    /// 실제로 지워서 확인했다(2026-09-08).</para>
    ///
    /// <para><b>플랫폼</b>: 완전 중립. <c>#if</c>도 플랫폼 API도 없다 — 순수 함수 + 소스 텍스트뿐이라
    /// macOS/Windows/iOS에서 같은 값을 잰다.</para>
    /// </summary>
    public sealed class LedgeHangDescendTargetDialogueTests
    {
        private const string LogPrefix = "[매달리기대사-TEST]";

        /// <summary>배포 기준 신장. 배율에 무관한 축을 쓰려고 프로덕션 상수를 참조한다.</summary>
        private const float H = StickConfig.BaselineCharacterTotalHeight;

        private static DialogueLine Resolve(bool hasDescendTarget, float dropUnits, float heightWorld)
            => LedgeHangState.ResolveDialogue(StickmanStateId.LedgeHang,
                new LedgeHangState.LedgeHangDialogueParams
                {
                    HasDescendTarget = hasDescendTarget,
                    DropHeightUnits = dropUnits,
                    CharacterHeightWorld = heightWorld,
                });

        private static string Text(bool hasDescendTarget, float dropUnits, float heightWorld)
            => Resolve(hasDescendTarget, dropUnits, heightWorld).Text;

        // ==================================================================================
        // 1. ★ 본체 — 발판을 못 찾은 경로가 "얕은 낙차"로 위장하지 않는다
        // ==================================================================================

        /// <summary>
        /// 낙차 축을 <b>고정</b>하고 <c>HasDescendTarget</c>만 뒤집는다. 게이트가 없으면 두 결과가
        /// <b>같아진다</b>(그게 바로 이 결함이었다) — 그래서 <c>AreNotEqual</c>이 네거티브 컨트롤이다.
        /// </summary>
        [Test]
        public void 내려갈발판이_없으면_얕은낙차_대사로_위장하지_않는다()
        {
            // 발판을 못 찾은 실제 경로의 낙차는 0이다(LedgeHangState.Enter의 `: 0f`).
            string withTarget = Text(hasDescendTarget: true, dropUnits: 0f, heightWorld: H);
            string withoutTarget = Text(hasDescendTarget: false, dropUnits: 0f, heightWorld: H);

            // 양성 대조 — 정상 경로가 실제로 말을 하는가. 이게 깨지면 아래 두 단언은
            // "모두 침묵"이라는 이유로 조용히 통과할 수 있다(0건을 훑고 내는 초록).
            Assert.IsNotEmpty(withTarget,
                $"{LogPrefix} 양성 대조 실패 — 발판을 찾은 정상 경로마저 침묵했습니다. " +
                "게이트가 정상 경로까지 삼켰거나 매핑 함수가 죽었습니다.");

            Assert.AreNotEqual(withTarget, withoutTarget,
                $"{LogPrefix} 발판을 못 찾은 경로가 「가장 얕은 낙차」와 똑같은 대사를 냈습니다(\"{withoutTarget}\"). " +
                "낙차 0은 「가장 얕다」와 「못 찾았다」를 구분하지 못합니다 — " +
                "LedgeHangState.ResolveDialogue의 HasDescendTarget 게이트가 사라졌는지 보세요. " +
                "이 경로의 실제 행동은 어디로도 안 내려가고 그냥 Fall이라, 이 대사는 원칙 1 위반입니다.");

            Assert.IsTrue(string.IsNullOrEmpty(withoutTarget),
                $"{LogPrefix} 발판이 없을 때는 침묵이 계약입니다(침묵은 거짓말이 아니다). " +
                $"실제 값=\"{withoutTarget}\" — 새 문안이 필요하다고 판단됐다면 design-narrative를 거쳐 " +
                "골든(DialogueBudgetKoGolden.txt)까지 함께 갱신해야 합니다.");
        }

        /// <summary>파라미터 자체가 없거나 다른 타입이면 <b>아무것도 모르는 것</b>이다 — 부위를
        /// 주장하지 않는 <c>GrabReactionLines</c>와 같은 판단으로, 낙차를 0으로 가정하지 않는다.</summary>
        [Test]
        public void 파라미터를_읽지_못하면_낙차를_0으로_가정하지_않고_침묵한다()
        {
            foreach (object bogus in new object[] { null, "낙차 아님", 0f })
            {
                DialogueLine line = LedgeHangState.ResolveDialogue(StickmanStateId.LedgeHang, bogus);
                Assert.IsTrue(string.IsNullOrEmpty(line.Text),
                    $"{LogPrefix} 파라미터가 {(bogus == null ? "null" : bogus.GetType().Name)}인데 " +
                    $"대사를 만들었습니다(\"{line.Text}\") — 모르는 상태에서 낙차 0을 가정하면 " +
                    "그게 바로 이 파일이 막는 결함입니다.");
            }
        }

        // ==================================================================================
        // 2. 기존 정상 경로는 한 글자도 바뀌지 않았다 (게이트는 그 앞에 붙었을 뿐이다)
        // ==================================================================================

        /// <summary>임계값 양쪽이 여전히 <b>서로 다른 대사</b>이고, 얕은 쪽 구간이 쪼개지지 않았다.
        /// (경계 위에 <b>정확히</b> 앉은 값은 일부러 재지 않는다 — 아래 실측 주석 참고.)</summary>
        [Test]
        public void 발판을_찾은_정상경로의_낙차_임계값은_그대로다()
        {
            float threshold = LedgeHangState.DeepDescentHeights * H;   // 숫자를 베끼지 않는다.

            string shallow = Text(true, threshold * 0.99f, H);
            string deep = Text(true, threshold * 1.01f, H);

            Assert.AreNotEqual(shallow, deep,
                $"{LogPrefix} 임계값 {threshold:F3}유닛 양쪽이 같은 대사를 냅니다 — " +
                "낙차 분기가 통째로 죽었습니다(게이트가 정상 경로까지 덮었을 가능성).");

            Assert.AreEqual(shallow, Text(true, 0f, H),
                $"{LogPrefix} 낙차 0(가장 얕음)과 임계값 바로 아래가 다른 대사입니다 — " +
                "얕은 쪽 구간이 쪼개졌습니다.");

            // ★ 2026-09-08 실측 — <b>「정확히 임계값」은 잠그지 않는다.</b> 처음엔
            //   Assert.AreEqual(deep, Text(true, threshold, H))를 넣었다가 빨간불을 봤다. 원인은
            //   프로덕션이 아니라 <b>부동소수 경로 차이</b>다:
            //     · 테스트의 threshold = 컴파일타임 상수 폴딩(float32 반올림) = 3.63951110839843750
            //     · 프로덕션의 DeepDescentHeights * h = Mono 런타임 곱셈(배정도 중간값 유지) = 3.63951116263138
            //   즉 같은 식인데 런타임 쪽이 1ulp 크다. 경계 위에 정확히 앉은 낙차는 <b>어느 쪽으로도
            //   떨어질 수 있고</b>, 실제 낙차가 임계값과 비트 단위로 같을 확률은 0이다.
            //   여기서 잠글 가치가 있는 계약은 "임계값 양쪽이 서로 다른 대사"이지 부동소수 반올림 방향이
            //   아니다 — 그걸 잠그면 프로덕션이 멀쩡한데 테스트만 빨개진다. 다음 라운드도 넣지 마라.
        }

        /// <summary>신장을 못 재면 기준 신장으로 되메운다(종전 거동). 임계값이 <b>H 배수</b>라
        /// 이 폴백이 없으면 임계값이 0이 되어 전부 «깊다» 쪽으로 쏠린다.</summary>
        [Test]
        public void 신장을_못재면_기준신장으로_되메워_임계값이_유지된다()
        {
            float threshold = LedgeHangState.DeepDescentHeights * StickConfig.BaselineCharacterTotalHeight;

            Assert.AreEqual(Text(true, threshold * 0.99f, H), Text(true, threshold * 0.99f, 0f),
                $"{LogPrefix} 신장 0(못 잼) 경로가 기준 신장 경로와 다른 대사를 냅니다(얕은 쪽).");
            Assert.AreEqual(Text(true, threshold * 1.01f, H), Text(true, threshold * 1.01f, 0f),
                $"{LogPrefix} 신장 0(못 잼) 경로가 기준 신장 경로와 다른 대사를 냅니다(깊은 쪽).");

            // 위 두 단언은 "둘 다 같은 한 줄"이어도 통과한다 — 그래서 서로 달랐음을 함께 못박는다.
            Assert.AreNotEqual(Text(true, threshold * 0.99f, 0f), Text(true, threshold * 1.01f, 0f),
                $"{LogPrefix} 폴백 경로에서 임계값 양쪽이 같은 대사입니다 — 되메우기가 0을 그대로 썼습니다.");
        }

        /// <summary>말하는 두 줄은 여전히 <see cref="DialogueKind.Narrative"/>다(진행 서술).
        /// 종류가 <c>Reaction</c>으로 바뀌면 상태가 끝나도 문장이 남아 원칙 1이 다시 깨진다.</summary>
        [Test]
        public void 말하는_줄은_여전히_진행서술이다()
        {
            float threshold = LedgeHangState.DeepDescentHeights * H;
            foreach (float drop in new[] { 0f, threshold * 1.01f })
            {
                DialogueLine line = Resolve(true, drop, H);
                Assert.AreEqual(DialogueKind.Narrative, line.Kind,
                    $"{LogPrefix} 낙차 {drop:F3}유닛의 대사 종류가 바뀌었습니다(\"{line.Text}\") — " +
                    "매달려 내려가는 중이라는 진행 서술이라 상태가 끝나면 즉시 컷되어야 합니다.");
            }
        }

        // ==================================================================================
        // 3. 새 문안을 지어내지 않았다 — 말하는 줄은 전부 이미 말뭉치 골든에 있다
        // ==================================================================================

        /// <summary>
        /// 침묵을 고른 이유가 "코더가 문안을 지어내지 않는다"이므로, 그 선택을 <b>골든과의 대조</b>로
        /// 못박는다. 이 상태가 실제로 낼 수 있는 모든 텍스트를 훑어 비어 있지 않은 것만 모으고,
        /// 전부 <see cref="DialogueCorpus.ReadGolden"/>에 이미 있는 줄인지 확인한다.
        /// </summary>
        [Test]
        public void 말하는_줄은_전부_기존_대사_골든에_있다()
        {
            float threshold = LedgeHangState.DeepDescentHeights * H;
            var spoken = new List<string>();
            foreach (bool hasTarget in new[] { true, false })
            foreach (float drop in new[] { 0f, threshold * 0.5f, threshold, threshold * 2f })
            foreach (float height in new[] { 0f, H, H * 2f })
            {
                string text = Text(hasTarget, drop, height);
                if (!string.IsNullOrEmpty(text)) spoken.Add(text);
            }

            List<string> distinct = spoken.Distinct().ToList();
            Assert.GreaterOrEqual(distinct.Count, 2,
                $"{LogPrefix} 이 상태가 실제로 내는 고유 대사가 {distinct.Count}줄뿐입니다 — " +
                "얕음/깊음 두 갈래가 살아 있으면 최소 2줄이어야 합니다(수집 자체가 죽었을 수 있습니다).");

            HashSet<string> golden = new HashSet<string>(DialogueCorpus.ReadGolden().Select(r => r.Text));
            foreach (string text in distinct)
            {
                Assert.IsTrue(golden.Contains(text),
                    $"{LogPrefix} \"{text}\"가 대사 골든에 없습니다 — 새 문안이 생겼다면 " +
                    "design-narrative 인계와 골든(DialogueBudgetKoGolden.txt) 갱신이 함께 와야 합니다.");
            }
        }

        // ==================================================================================
        // 4. 순수 함수가 증명할 수 없는 마지막 한 칸 — 침묵이 실제로 "발급 안 함"인가
        // ==================================================================================

        /// <summary>
        /// <see cref="LedgeHangState.ResolveDialogue"/>가 빈 문자열을 돌려주는 것만으로는 부족하다 —
        /// 호출부가 그 빈 문자열로 <see cref="DialogueIntent"/>를 만들어 버리면 화면에는 <b>빈 말풍선</b>이
        /// 뜬다. 그 경로는 씬과 상태머신이 필요해 EditMode 순수 함수로는 닿지 않으므로,
        /// <b>소스에서 게이트가 발급보다 먼저 오는지</b>를 확인한다.
        ///
        /// <para>★ 여기 쓰는 니들은 전부 <b>존재 단언</b>이다(CLAUDE.md — 부재 단언용 니들은 썩으면
        /// 조용히 초록이 되지만 존재 단언은 시끄럽게 빨개진다). 호출부를 리팩터링해서 이 테스트가
        /// 빨개졌다면 <b>고칠 것은 이 테스트가 아니라 먼저 게이트의 생사 확인</b>이다.</para>
        /// </summary>
        [Test]
        public void 호출부가_침묵이면_말풍선을_아예_발급하지_않는다()
        {
            string path = Path.Combine(DialogueCorpus.ScriptsRoot, "States", "LedgeHangState.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 원본을 찾지 못했습니다: {path}");
            string source = File.ReadAllText(path);

            const string issueNeedle = "DialogueIntent.TryCreate(";
            const string gateNeedle = "string.IsNullOrEmpty(ResolveDialogue(";

            int issue = source.IndexOf(issueNeedle, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(issue, 0,
                $"{LogPrefix} 발급 지점(\"{issueNeedle}\")을 못 찾았습니다 — 발급 형태가 바뀌었다면 " +
                "이 검사도 함께 고쳐야 합니다(고치지 않으면 게이트가 사라져도 아무도 모릅니다).");
            Assert.AreEqual(issue, source.LastIndexOf(issueNeedle, System.StringComparison.Ordinal),
                $"{LogPrefix} 발급 지점이 둘 이상입니다 — 게이트를 지나지 않는 경로가 생겼을 수 있습니다.");

            int gate = source.IndexOf(gateNeedle, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(gate, 0,
                $"{LogPrefix} 침묵 게이트(\"{gateNeedle}\")를 못 찾았습니다 — 빈 텍스트로도 말풍선이 " +
                "발급되면 화면에 빈 말풍선이 뜹니다.");
            Assert.Less(gate, issue,
                $"{LogPrefix} 침묵 게이트가 발급보다 뒤에 있습니다 — 순서가 곧 계약입니다.");
        }
    }
}
