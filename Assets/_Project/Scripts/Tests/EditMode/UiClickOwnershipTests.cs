using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 회귀 잠금 — <b>한 프레임에 들어온 클릭 하나는 표면 하나만 먹는다.</b>
    ///
    /// ============================================================================
    /// 무엇이 신고됐나 (2026-09-08, P0)
    /// ============================================================================
    /// 사용자 원문 요지: <c>[오늘 할일]</c> 팝오버의 <c>[추가]</c>를 눌렀는데 그 아래 겹쳐 있던
    /// 포스트잇 카드의 <b>체크박스까지 함께 반응</b>해 항목이 자동 완료되고 하루 1회 300동전이
    /// 소진됐다. 되돌릴 방법이 없다(모델에 복구 API가 없다).
    ///
    /// 원인은 «중복 제거가 없어서»가 아니다 — <c>TryClaimAction</c>은 <b>4벌</b>이나 있었다.
    /// 넷 다 <b>자기 표면 안의</b> 두 입력 경로(uGUI · 전역 폴링)만 막았고,
    /// <b>서로 다른 두 표면</b>이 같은 물리적 클릭을 각자 정상 처리하는 것은 아무도 안 봤다.
    ///
    /// ============================================================================
    /// 이 파일이 특별히 조심하는 것 — <b>순서 의존</b>
    /// ============================================================================
    /// 단순한 «먼저 잡는 쪽이 이긴다» 게이트를 넣으면 승자가 <c>Update</c> 실행 순서에 좌우된다.
    /// 포스트잇이 먼저 도는 프레임에는 <c>[추가]</c>가 <b>삼켜져</b> 「추가가 안 된다」는 다른
    /// 버그가 된다 — 증상만 바뀌고 이 저장소가 제일 싫어하는 «가끔만 동작하는» 형태가 된다.
    /// 그래서 <see cref="판정은_호출_순서에_의존하지_않는다"/>가 <b>양쪽 순서를 다 돌려 본다</b>.
    /// </summary>
    public sealed class UiClickOwnershipTests
    {
        private const string LogPrefix = "[클릭소유권]";

        private const string Popover = nameof(TodoBoardPopover);
        private const string PostIt = nameof(TodoPostItWidget);

        private static readonly Rect PopoverRect = new Rect(200f, 200f, 500f, 512f);

        /// <summary>팝오버 안쪽이면서 포스트잇 행과도 겹치는 지점(= 신고된 <c>[추가]</c> 자리).</summary>
        private static readonly Vector2 OverlapPoint = new Vector2(600f, 650f);

        /// <summary>팝오버 밖 — 포스트잇만 있는 자리.</summary>
        private static readonly Vector2 OutsidePoint = new Vector2(1200f, 900f);

        [SetUp]
        public void Reset() => UiClickArbiter.ResetForTests();

        [TearDown]
        public void ResetAfter() => UiClickArbiter.ResetForTests();

        // ====================================================================
        // ① 시간 축 — 한 프레임, 한 표면
        // ====================================================================

        [Test]
        public void 한_프레임의_클릭은_한_표면만_먹는다()
        {
            const int frame = 10;

            Assert.IsTrue(UiClickArbiter.TryClaimFrame(Popover, frame),
                $"{LogPrefix} 아무도 안 잡은 프레임을 첫 표면이 못 잡았습니다.");
            Assert.IsFalse(UiClickArbiter.TryClaimFrame(PostIt, frame),
                $"{LogPrefix} 같은 프레임의 같은 클릭을 <b>두 표면이</b> 처리했습니다 — " +
                "이것이 «[추가]를 눌렀는데 포스트잇 체크박스도 눌렸다»의 본체입니다.");

            // 같은 표면의 재요청은 통과한다 — 그건 «uGUI와 전역 폴링이 겹친» 경우이고,
            // 그 중복은 각 창의 TryClaimAction(0.35초)이 이미 막는다. 여기서 또 막으면
            // 두 판정이 서로를 모르는 채 겹치고, 정상 클릭이 조용히 씹힌다.
            Assert.IsTrue(UiClickArbiter.TryClaimFrame(Popover, frame),
                $"{LogPrefix} 같은 표면이 같은 프레임에 두 번 물었더니 거절됐습니다 — " +
                "uGUI 경로와 전역 폴링 경로가 겹치는 정상 상황이 막힙니다.");

            Assert.IsTrue(UiClickArbiter.TryClaimFrame(PostIt, frame + 1),
                $"{LogPrefix} 다음 프레임의 새 클릭까지 막혔습니다 — 게이트가 영구화됐습니다.");
        }

        // ====================================================================
        // ② 공간 축 — 위에 열린 창이 덮은 자리
        // ====================================================================

        [Test]
        public void 위에_열린_창이_덮은_자리는_아래_카드가_먹지_않는다()
        {
            const int frame = 20;
            UiClickArbiter.PublishSurface(Popover, UiClickArbiter.LayerOpenedPanel, PopoverRect, frame);

            Assert.IsTrue(PopoverRect.Contains(OverlapPoint),
                $"{LogPrefix} 표본이 잘못됐습니다 — 겹침 지점이 팝오버 사각형 밖입니다.");

            Assert.IsFalse(UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                    OverlapPoint, frame),
                $"{LogPrefix} 위에 열린 창이 덮은 자리의 클릭을 상시 카드가 먹었습니다.");

            // ---- 양성 대조 ① : 팝오버 밖이면 카드가 <b>정상적으로</b> 먹는다 ----
            Assert.IsFalse(PopoverRect.Contains(OutsidePoint), $"{LogPrefix} 대조 표본이 팝오버 안입니다.");
            Assert.IsTrue(UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                    OutsidePoint, frame + 1),
                $"{LogPrefix} 팝오버 밖의 클릭까지 막혔습니다 — 이 기구가 상시 카드를 통째로 죽였습니다.");
        }

        [Test]
        public void 등록이_없으면_아무것도_막지_않는다()
        {
            // ★ 위 단언이 «사각형 때문에» 거절된 것이 맞는지 확인하는 음성 대조.
            //   등록을 안 한 상태에서도 거절된다면 그 테스트는 아무것도 증명하지 못한다.
            Assert.AreEqual(0, UiClickArbiter.PublishedSurfaceCountForTests,
                $"{LogPrefix} 앞 테스트의 등록이 샜습니다 — 정적 상태가 테스트 사이에 남아 있습니다.");
            Assert.IsTrue(UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                    OverlapPoint, 30),
                $"{LogPrefix} 아무도 등록하지 않았는데 거절됐습니다.");
        }

        [Test]
        public void 아래_카드가_먼저_잡아도_위_창의_자리는_뺏기지_않는다()
        {
            const int frame = 40;
            UiClickArbiter.PublishSurface(Popover, UiClickArbiter.LayerOpenedPanel, PopoverRect, frame);

            // 포스트잇이 <b>먼저</b> 돌았다(Update 순서는 정해져 있지 않다).
            bool postItGot = UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                OverlapPoint, frame);
            bool popoverGot = UiClickArbiter.TryClaimClick(Popover, UiClickArbiter.LayerOpenedPanel,
                OverlapPoint, frame);

            Assert.IsFalse(postItGot, $"{LogPrefix} 아래 카드가 위 창의 자리를 가져갔습니다.");
            Assert.IsTrue(popoverGot,
                $"{LogPrefix} 아래 카드가 프레임을 먼저 잡아 <b>[추가]가 삼켜졌습니다</b> — " +
                "증상만 «자동 완료»에서 «추가가 안 됨»으로 바뀐 것입니다.");
        }

        [Test]
        public void 판정은_호출_순서에_의존하지_않는다()
        {
            // 같은 상황을 두 순서로 돌려 <b>같은 승자</b>가 나오는지 본다.
            string winnerA = ResolveWinner(popoverFirst: true, frame: 50);
            string winnerB = ResolveWinner(popoverFirst: false, frame: 60);

            Assert.AreEqual(Popover, winnerA, $"{LogPrefix} 팝오버가 먼저 돈 순서에서 승자가 팝오버가 아닙니다.");
            Assert.AreEqual(winnerA, winnerB,
                $"{LogPrefix} 호출 순서에 따라 승자가 바뀝니다({winnerA} vs {winnerB}) — " +
                "«가끔만 동작하는» 수정입니다.");
        }

        private static string ResolveWinner(bool popoverFirst, int frame)
        {
            UiClickArbiter.ResetForTests();
            UiClickArbiter.PublishSurface(Popover, UiClickArbiter.LayerOpenedPanel, PopoverRect, frame);

            string winner = null;
            if (popoverFirst)
            {
                if (UiClickArbiter.TryClaimClick(Popover, UiClickArbiter.LayerOpenedPanel, OverlapPoint, frame))
                    winner = Popover;
                if (UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard, OverlapPoint, frame))
                    winner ??= PostIt;
                return winner;
            }

            if (UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard, OverlapPoint, frame))
                winner = PostIt;
            if (UiClickArbiter.TryClaimClick(Popover, UiClickArbiter.LayerOpenedPanel, OverlapPoint, frame))
                winner ??= Popover;
            return winner;
        }

        // ====================================================================
        // ③ 등록이 사라지는 두 경로 — 거두기 / 낡음
        // ====================================================================

        [Test]
        public void 창이_닫히면_그_자리는_다시_아래_카드의_것이다()
        {
            const int frame = 70;
            UiClickArbiter.PublishSurface(Popover, UiClickArbiter.LayerOpenedPanel, PopoverRect, frame);
            Assert.IsFalse(UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                OverlapPoint, frame), $"{LogPrefix} 전제 — 열려 있는 동안에는 막혀야 합니다.");

            UiClickArbiter.WithdrawSurface(Popover);
            Assert.AreEqual(0, UiClickArbiter.PublishedSurfaceCountForTests,
                $"{LogPrefix} 거뒀는데 등록이 남아 있습니다.");
            Assert.IsTrue(UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                    OverlapPoint, frame + 1),
                $"{LogPrefix} 창이 닫힌 뒤에도 그 자리가 클릭을 삼킵니다 — " +
                "원래 고치려던 것보다 나쁜 상태입니다(보이지도 않는 자리가 영원히 먹는다).");
        }

        [Test]
        public void 거두는_것을_잊은_등록은_스스로_무효가_된다()
        {
            const int published = 80;
            UiClickArbiter.PublishSurface(Popover, UiClickArbiter.LayerOpenedPanel, PopoverRect, published);

            Assert.IsFalse(UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                    OverlapPoint, published + UiClickArbiter.StaleFrames),
                $"{LogPrefix} 유효 기간 안인데 벌써 무효가 됐습니다 — Update 순서 때문에 «질문이 " +
                "등록보다 먼저» 오는 프레임이 정상적으로 존재하고, 그때 답을 잃으면 이 기구가 " +
                "가끔만 동작합니다.");

            Assert.IsTrue(UiClickArbiter.TryClaimClick(PostIt, UiClickArbiter.LayerAmbientCard,
                    OverlapPoint, published + UiClickArbiter.StaleFrames + 1),
                $"{LogPrefix} {UiClickArbiter.StaleFrames}프레임이 지난 등록이 아직도 클릭을 삼킵니다.");
        }

        // ====================================================================
        // ④ 두 표면이 실제로 같은 기구를 쓰는가 (소스 감사)
        // ====================================================================

        [Test]
        public void 두_표면의_이름과_층이_서로_다르다()
        {
            Assert.AreNotEqual(TodoBoardPopover.ClickArbiterSurfaceId, TodoPostItWidget.ClickArbiterSurfaceId,
                $"{LogPrefix} 두 표면이 같은 이름을 씁니다 — 중재가 «같은 표면»으로 보고 둘 다 통과시킵니다.");
            Assert.Greater(TodoBoardPopover.ClickArbiterLayer, TodoPostItWidget.ClickArbiterLayer,
                $"{LogPrefix} 사용자가 직접 연 팝오버가 상시 카드보다 아래층입니다 — " +
                "z-순서 모형이 화면과 반대입니다.");
        }

        [Test]
        public void 포스트잇_전역폴링은_히트한_뒤에_소유권을_묻는다()
        {
            string source = ReadScript("Interaction", "TodoPostItWidget.cs");

            int poll = source.IndexOf("private void TickGlobalClickPolling()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(poll, 0,
                $"{LogPrefix} TodoPostItWidget.TickGlobalClickPolling()이 사라졌습니다 — 이 감사를 함께 고치세요.");
            int end = source.IndexOf("\n        /// <summary>", poll, StringComparison.Ordinal);
            Assert.Greater(end, poll, $"{LogPrefix} 전역 폴링 블록의 끝을 찾지 못했습니다.");
            string body = source.Substring(poll, end - poll);

            // 히트한 분기마다 중재를 거친다 — 개수를 세어 «한 군데만 고치고 나머지는 그대로»를 막는다.
            int claims = CountOf(body, "ClaimGlobalClick(cursor)");
            int actions = CountOf(body, "TryClaimAction(");
            Assert.AreEqual(actions, claims,
                $"{LogPrefix} 전역 폴링 분기 {actions}개 중 {claims}개만 중재를 거칩니다 — " +
                "안 거치는 분기가 남으면 그 버튼에서 신고된 사고가 그대로 재현됩니다.");
            Assert.GreaterOrEqual(claims, 3,
                $"{LogPrefix} 중재 호출이 {claims}건뿐입니다([숨기기]/[더보기]/행 = 3건이어야 합니다) — " +
                "블록 잘라내기가 어긋났으면 이 감사가 아무것도 안 보고 초록이 됩니다.");

            // ★ 양성 대조 — 스캐너가 실제로 세고 있는가(지운 사본에서 0이 나오는가).
            Assert.AreEqual(0, CountOf(body.Replace("ClaimGlobalClick(cursor)", "NoClaim()"),
                    "ClaimGlobalClick(cursor)"),
                $"{LogPrefix} 양성 대조 실패 — 호출을 지운 사본에서도 세어집니다(거짓 통과).");
        }

        [Test]
        public void 팝오버_전역폴링은_들어오자마자_소유권을_잡는다()
        {
            string source = ReadScript("Interaction", "TodoBoardPopover.cs");

            int at = source.IndexOf("protected override void OnGlobalClick(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"{LogPrefix} TodoBoardPopover.OnGlobalClick이 사라졌습니다.");
            string head = source.Substring(at, Math.Min(900, source.Length - at));

            Assert.GreaterOrEqual(head.IndexOf(nameof(UiClickArbiter) + "." + nameof(UiClickArbiter.TryClaimClick),
                    StringComparison.Ordinal), 0,
                $"{LogPrefix} 팝오버가 전역 클릭을 처리하면서 소유권을 잡지 않습니다 — " +
                "빈자리에 떨어진 클릭까지 잡아야 아래 카드가 그것을 함께 먹지 않습니다.");

            // 스캐너 생존 확인.
            Assert.Less("이 문장에는 그 호출이 없다".IndexOf(nameof(UiClickArbiter), StringComparison.Ordinal), 0,
                $"{LogPrefix} 스캐너가 아무 문자열에나 물고 있습니다.");
        }

        [Test]
        public void 팝오버의_모든_버튼_배선이_중재를_거친다()
        {
            string source = ReadScript("Interaction", "TodoBoardPopover.cs");

            // 이 파일에서 기반 클래스의 <c>Wire(</c>를 <b>직접</b> 부르는 자리는 WireGuarded 정의
            // 한 곳뿐이어야 한다. 하나라도 새로 직접 부르면 그 버튼만 중재 밖에 남는다(경고 0건).
            // ※ "Wire("는 "WireGuarded("의 부분문자열이 아니다(W-i-r-e 다음이 '('가 아니라 'G').
            int direct = CountOf(source, "Wire(");
            Assert.AreEqual(1, direct,
                $"{LogPrefix} 기반 클래스 Wire(를 직접 부르는 자리가 {direct}곳입니다 — " +
                "WireGuarded 정의 한 곳이어야 합니다. 직접 배선된 버튼은 중재 밖에 남습니다.");

            // ★ 위 셈이 «두 이름을 실제로 가르고 있는가»를 못박는다. 가르지 못하면 위 단언은
            //   WireGuarded 호출을 전부 세어 버려 어떤 값이 나오든 의미가 없다.
            Assert.AreEqual(0, CountOf("WireGuarded(x, y, z);", "Wire("),
                $"{LogPrefix} 스캐너가 WireGuarded(를 Wire(로 세고 있습니다 — 위 단언이 무의미합니다.");

            Assert.Greater(CountOf(source, "WireGuarded("), 10,
                $"{LogPrefix} WireGuarded 호출이 너무 적습니다 — 이 창의 버튼은 열 개가 넘습니다. " +
                "세는 방식이 깨졌다면 이 감사가 무의미합니다.");
        }

        // ====================================================================
        // 도구
        // ====================================================================

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
    }
}
