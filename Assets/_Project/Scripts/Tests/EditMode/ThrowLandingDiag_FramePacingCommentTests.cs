using System.IO;
using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-05 진단 (c) — <c>FramePacing.cs</c>의 UI 홀드 주석이 "Windows Calm은 targetFrameRate를 30으로 나눈다"고
    /// 적혀 있던 것(perf-doc 신고, 2026-09-01 이전 서술)을 현행 동작으로 정정했다. 이 테스트는 두 가지를 한 자리에서 잠근다:
    /// <list type="number">
    ///   <item><b>행동의 진실</b> — <see cref="FramePacingPolicy.BuildPlan"/>이 Calm/Still에서 Windows 기준값(vSync 0, target 60)의
    ///     targetFrameRate를 건드리지 않고 renderFrameInterval만 나눈다.</item>
    ///   <item><b>주석의 진실</b> — 소스가 그 사실을 말하고, 낡은 문장은 없다.</item>
    /// </list>
    /// 2번의 부재 단언은 1번이 <b>양성 대조</b>다: 동작이 예전으로 되돌아가면 1번이 시끄럽게 빨개지고, 그때 주석도 함께
    /// 다시 봐야 한다(CLAUDE.md "부재 단언은 썩으면 조용히 초록" — 그래서 같은 테스트 안에 둔다).
    /// </summary>
    public sealed class ThrowLandingDiag_FramePacingCommentTests
    {
        private const int WindowsBaseVSync = 0;
        private const int WindowsBaseTargetFps = 60;

        [Test]
        public void 현행_Calm과_Still은_Windows에서도_targetFrameRate를_건드리지_않고_renderFrameInterval만_나눈다()
        {
            FramePacingPlan calm = FramePacingPolicy.BuildPlan(FramePacingTier.Calm, WindowsBaseVSync, WindowsBaseTargetFps, lowPowerMode: false);
            Assert.AreEqual(WindowsBaseTargetFps, calm.TargetFrameRate, "Calm이 게임 루프를 늦추면 안 된다(2026-09-01 결정).");
            Assert.AreEqual(WindowsBaseVSync, calm.VSyncCount);
            Assert.AreEqual(2, calm.RenderFrameInterval);
            Assert.AreEqual(30, calm.EffectiveTargetFps, "표시는 절반(30fps)이 맞다 — 루프가 아니라 제출만 준다.");

            FramePacingPlan still = FramePacingPolicy.BuildPlan(FramePacingTier.Still, WindowsBaseVSync, WindowsBaseTargetFps, lowPowerMode: false);
            Assert.AreEqual(WindowsBaseTargetFps, still.TargetFrameRate);
            Assert.AreEqual(FramePacingPolicy.DefaultStillDivisor, still.RenderFrameInterval);
        }

        [Test]
        public void 주석은_현행_동작을_말하고_낡은_문장은_없다()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "_Project", "Scripts", "Platform", "FramePacing.cs");
            Assert.IsTrue(File.Exists(path), path);
            string src = File.ReadAllText(path);

            // 존재 단언 — 정정 문장.
            StringAssert.Contains("2026-09-05 정정(perf-doc 신고)", src);
            StringAssert.Contains("Windows에서도 게임 루프·커서 폴링은 60Hz다", src);

            // 부재 단언 — 낡은 문장(현재형). 역사 서술은 "(당시)"를 붙여 남기므로 현재형 원문만 겨눈다.
            //   양성 대조는 위 테스트(행동의 진실)다.
            Assert.IsFalse(src.Contains("3. Windows에서 Calm은 `Application.targetFrameRate`를 60 -> 30으로 나눈다"),
                "정정 전 문장이 현재형으로 남아 있다 — 코드가 다시 루프를 늦추게 바뀐 게 아니라면 주석만 낡은 것이다.");
            Assert.IsFalse(src.Contains("즉 **게임 루프 자체가 30Hz**가 된다."),
                "정정 전 결론 문장이 남아 있다.");
        }
    }
}
