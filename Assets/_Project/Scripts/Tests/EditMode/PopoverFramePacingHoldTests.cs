using System;
using System.IO;
using NUnit.Framework;
using StickMate.Interaction;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 팝오버 계열의 <b>프레임 페이싱 홀드 배선</b> — 2026-09-08, persona-stress R-9.
    ///
    /// <para>신고 원문: <i>"드래그는 켰는데 <c>FramePacing.HoldActiveForInteraction()</c> 호출이
    /// <c>PopoverPanel</c>에 <b>0건</b>이다(정보창 <c>CharacterInfoWindow.Input.cs</c>,
    /// 설정창 <c>SettingsWindow.cs</c>에는 있다). 폴링 간격만 없앴다. 저티어에서 큰 창을 끌 때
    /// 뚝뚝 끊기는지 — 가설, 실기 확인 필요."</i></para>
    ///
    /// <para><b>이 파일은 <c>UiInteractionFramePacingHoldTests</c>의 「불변식 4-b」 절과 같은 계보다</b>
    /// (표면이 늘어날 때마다 한 줄씩 늘어나는 것이 정상인 그 목록). 별도 파일로 둔 이유는 이번
    /// 라운드의 파일 배정이 갈려 있어서다 — 리더가 정리할 때 그쪽으로 합쳐도 된다.</para>
    ///
    /// ============================================================================
    /// 왜 소스 검사인가 (그리고 니들이 썩지 않게 하는 장치)
    /// ============================================================================
    /// 「끄는 동안 등급을 붙잡는가」는 <b>배선의 존재 여부</b> 문제라 순수 함수로 잴 수 없고,
    /// 그 판정을 실제로 돌리려면 PlayMode 프레임이 필요하다. 그래서 이 저장소의 기존 관례
    /// (<c>UiInteractionFramePacingHoldTests</c>)를 그대로 따른다. 다만 CLAUDE.md가 요구하는 대로
    /// <b>존재 단언</b>만 쓴다 — 니들이 썩으면 <b>시끄럽게 빨개진다</b>(조용히 초록이 되는
    /// 부재 단언이 아니다). 그리고 같은 니들이 <b>이미 배선된 창</b>에서도 실제로 잡히는지를
    /// 같은 테스트 안에서 대조해, 니들 자체가 죽었는지를 구별한다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    ///  · <c>PopoverPanel.Update()</c>에서 <c>TickFramePacingHold()</c> 한 줄을 지우면
    ///    <c>팝오버는_창을_끄는_동안_홀드를_갱신한다</c>가 실패한다(이 라운드 이전의 상태 = 호출 0건).
    ///  · 홀드 조건을 «열려 있으면»으로 넓히면 <c>팝오버는_열려있다는_이유로는_홀드하지_않는다</c>가
    ///    실패한다.
    /// </summary>
    public sealed class PopoverFramePacingHoldTests
    {
        private const string HoldCall = "FramePacing.HoldActiveForInteraction(";

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string part in relative) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        private static int CountOf(string source, string needle)
        {
            int n = 0, i = 0;
            while ((i = source.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static int IndexOfOrFail(string source, string needle, int from, string what)
        {
            int i = source.IndexOf(needle, from, StringComparison.Ordinal);
            Assert.Greater(i, 0, what);
            return i;
        }

        [Test]
        public void 팝오버는_창을_끄는_동안_홀드를_갱신한다()
        {
            string popover = ReadScript("Interaction", "PopoverPanel.cs");

            // ---- 니들 자격 검증 — 이미 배선된 창에서 같은 니들이 실제로 잡히는가 ----
            //      여기가 0이면 아래 단언들은 «API 이름이 바뀌어서» 실패한 것이므로
            //      팝오버를 고치는 것이 답이 아니다(측정기를 먼저 고쳐야 한다).
            Assert.AreEqual(1, CountOf(ReadScript("Interaction", "SettingsWindow.cs"), HoldCall),
                $"대조군(설정창)에서 '{HoldCall}'을 찾지 못했습니다 — 이 니들이 죽었습니다. " +
                "아래 단언들은 팝오버가 아니라 이 테스트의 고장을 가리킵니다.");

            Assert.AreEqual(1, CountOf(popover, HoldCall),
                "팝오버의 홀드가 없거나 둘 이상입니다(2026-09-07에 드래그만 켜지고 홀드는 0건이었다 — " +
                "persona-stress R-9).");

            int tick = IndexOfOrFail(popover, "private void TickFramePacingHold(", 0,
                "PopoverPanel.TickFramePacingHold()가 사라졌습니다 — 이 테스트를 갱신하세요.");
            int policy = IndexOfOrFail(popover, "FramePacingPolicy.ShouldHoldForSurface(", tick,
                "팝오버가 플랫폼 중립 판정 함수를 쓰지 않습니다(정보창·설정창과 같은 한 벌이어야 한다).");
            int call = IndexOfOrFail(popover, HoldCall, tick,
                "팝오버의 홀드가 TickFramePacingHold() 안에 없습니다.");
            Assert.Less(policy, call, "홀드가 판정보다 앞입니다 — 조건 없이 붙잡게 됩니다.");

            // ---- 루프에 실제로 걸려 있는가 ----
            int update = IndexOfOrFail(popover, "protected virtual void Update()", 0,
                "PopoverPanel.Update()가 사라졌습니다 — 이 테스트를 갱신하세요.");
            Assert.Greater(popover.IndexOf("TickFramePacingHold();", update, StringComparison.Ordinal), update,
                "TickFramePacingHold()가 Update()에서 불리지 않습니다 — 함수만 있고 아무도 안 부르면 " +
                "호출 0건과 결과가 같습니다.");

            // ---- 「조작 중」의 정의가 창을 잡고 있는 것인가 ----
            string grabbed = "." + nameof(UiWindowDrag.IsGrabbed);
            Assert.Greater(popover.IndexOf(grabbed, tick, StringComparison.Ordinal), tick,
                $"팝오버가 «창을 잡고 있다»({grabbed})를 조작으로 세지 않습니다 — 커서가 창 밖으로 " +
                "나가도 드래그는 이어지므로 사각형 판정만으로는 못 잡습니다(설정창과 같은 사정).");
        }

        /// <summary>
        /// ★ 팝오버는 <b>사용자가 [✕]를 누를 때까지</b> 열려 있을 수 있다(2026-09-02 지시).
        /// 그래서 «열려 있으면 홀드»로 넓히면 열린 창 하나가 적응형 절전을 통째로 무력화한다 —
        /// <see cref="FramePacingPolicy.ShouldHoldForSurface"/> 문서가 실측 125분으로 기록한 그 사고이고,
        /// <c>TodoPostItWidget</c>이 상시 표면이라 «클릭 중에만» 거는 것과 같은 판단이다.
        /// </summary>
        [Test]
        public void 팝오버는_열려있다는_이유로는_홀드하지_않는다()
        {
            string popover = ReadScript("Interaction", "PopoverPanel.cs");
            int tick = IndexOfOrFail(popover, "private void TickFramePacingHold(", 0,
                "PopoverPanel.TickFramePacingHold()가 사라졌습니다 — 이 테스트를 갱신하세요.");
            int end = popover.IndexOf("\n        }", tick, StringComparison.Ordinal);
            Assert.Greater(end, tick, "TickFramePacingHold()의 본문 끝을 찾지 못했습니다.");
            string body = popover.Substring(tick, end - tick);

            Assert.IsFalse(body.Contains("_open"),
                "홀드 판정이 «열려 있는가»(_open)를 봅니다 — 이 창은 [✕]를 누를 때까지 남으므로 " +
                "그 배선은 곧 24시간 60fps입니다(적응형 절감 무력화).");
            Assert.IsFalse(body.Contains("IsOpen"),
                "홀드 판정이 «열려 있는가»(IsOpen)를 봅니다 — 위와 같은 이유로 금지입니다.");
        }

        /// <summary>판정 함수 자체의 계약을 <b>값으로</b>도 한 번 잰다 — 소스 검사만 있으면
        /// "함수를 부르긴 하는데 뜻이 반대"인 배선을 못 잡는다.</summary>
        [Test]
        public void 잡고_있으면_홀드하고_놓고_한참_지나면_안_한다()
        {
            Assert.IsTrue(FramePacingPolicy.ShouldHoldForSurface(
                    cursorOverSurface: false, manipulating: true, secondsSinceLastTouch: 0f),
                "창을 잡고 있는데 홀드하지 않습니다.");
            Assert.IsFalse(FramePacingPolicy.ShouldHoldForSurface(
                    cursorOverSurface: false, manipulating: false,
                    secondsSinceLastTouch: float.PositiveInfinity),
                "아무도 안 만지는 창이 영원히 홀드합니다 — 초깃값이 «방금 만졌다»로 읽히면 이렇게 됩니다.");
        }
    }
}
