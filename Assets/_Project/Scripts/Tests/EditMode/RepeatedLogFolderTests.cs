using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// **연속 동일 로그 접기**(Player.log 무한 증가 대응, 2026-09-02)의 순수 정책 잠금.
    ///
    /// <para>실측 근거: Player.log가 <b>153 B/s ≈ 13.2MB/일 ≈ 396MB/월</b>로 자라고, 그 안에서
    /// <c>662 [유휴동작] 중 661줄이 글자 하나 다르지 않다</c>(PlayerLogPolicy 문서).</para>
    ///
    /// <para><b>이 테스트가 지키는 계약은 두 개다</b>:
    /// (1) 반복은 접힌다, (2) <b>접혀도 아무 정보도 잃지 않는다</b> — 접힌 횟수가 반드시 방출되고
    /// 태그가 남는다. "로그를 줄인다"가 "눈을 감는다"가 되면 원칙 1(행동-텍스트 싱크)을 원격에서
    /// 검증할 수단이 사라진다.</para>
    /// </summary>
    public class RepeatedLogFolderTests
    {
        private const string A = "[유휴동작] 주위 살피기 재생 — 상태=Idle, 2.40초.";
        private const string B = "[유휴동작] 기지개 재생 — 상태=Idle, 3.10초.";

        [Test]
        public void 첫줄은_항상_찍힌다()
        {
            var folder = new RepeatedLogFolder();
            Assert.IsTrue(folder.ShouldEmit(A, 0.0, 60.0, out int folded));
            Assert.AreEqual(0, folded);
        }

        [Test]
        public void 연속_동일줄은_접힌다()
        {
            var folder = new RepeatedLogFolder();
            folder.ShouldEmit(A, 0.0, 60.0, out _);

            for (int i = 1; i <= 10; i++)
            {
                Assert.IsFalse(folder.ShouldEmit(A, i, 60.0, out int folded),
                    $"{i}번째 동일 줄이 그대로 파일에 쓰였다.");
                Assert.AreEqual(0, folded, "홀드 시간 전에는 요약도 내지 않는다.");
            }
            Assert.AreEqual(10, folder.PendingRepeats);
        }

        [Test]
        public void 다른_줄이_오면_접힌_횟수를_먼저_방출한다()
        {
            var folder = new RepeatedLogFolder();
            folder.ShouldEmit(A, 0.0, 60.0, out _);
            for (int i = 1; i <= 7; i++) folder.ShouldEmit(A, i, 60.0, out _);

            Assert.IsTrue(folder.ShouldEmit(B, 8.0, 60.0, out int folded));
            Assert.AreEqual(7, folded, "접힌 7줄이 통째로 사라지면 그게 '눈을 감는' 감량이다.");
            Assert.AreEqual(0, folder.PendingRepeats);
        }

        [Test]
        public void 홀드시간이_지나면_침묵하지_않고_중간요약을_낸다()
        {
            var folder = new RepeatedLogFolder();
            folder.ShouldEmit(A, 0.0, 10.0, out _);

            folder.ShouldEmit(A, 5.0, 10.0, out int early);
            Assert.AreEqual(0, early, "홀드 전에는 조용해야 한다.");

            folder.ShouldEmit(A, 10.0, 10.0, out int onTime);
            Assert.AreEqual(2, onTime, "홀드가 지났으면 '몇 번 반복 중'을 반드시 한 줄 낸다.");

            // 요약을 낸 뒤에는 카운터가 리셋되어 다음 홀드 창이 새로 시작한다.
            folder.ShouldEmit(A, 12.0, 10.0, out int afterFlush);
            Assert.AreEqual(0, afterFlush);
        }

        [Test]
        public void 홀드가_0이하면_시간요약을_내지_않는다()
        {
            var folder = new RepeatedLogFolder();
            folder.ShouldEmit(A, 0.0, 0.0, out _);
            for (int i = 1; i <= 100; i++)
            {
                Assert.IsFalse(folder.ShouldEmit(A, i * 1000.0, 0.0, out int folded));
                Assert.AreEqual(0, folded);
            }
            Assert.AreEqual(100, folder.PendingRepeats, "횟수는 여전히 보존되어야 한다.");
        }

        [Test]
        public void TryFlush는_접힌게_없으면_아무것도_안_낸다()
        {
            var folder = new RepeatedLogFolder();
            Assert.IsFalse(folder.TryFlush(100.0, 1.0, out int folded));
            Assert.AreEqual(0, folded);

            folder.ShouldEmit(A, 0.0, 60.0, out _);
            Assert.IsFalse(folder.TryFlush(0.1, 60.0, out _), "홀드 전 강제 방출은 없다.");

            folder.ShouldEmit(A, 1.0, 60.0, out _);
            Assert.IsTrue(folder.TryFlush(100.0, 60.0, out int late));
            Assert.AreEqual(1, late);
        }

        [Test]
        public void 접힘_요약줄에_태그와_횟수가_모두_남는다()
        {
            string line = RepeatedLogFolder.Describe("[유휴동작]", 41);
            StringAssert.Contains("[유휴동작]", line,
                "태그가 사라지면 태그 기준 grep 집계가 깨진다 — 세는 능력까지 잃는다.");
            StringAssert.Contains("41", line, "접힌 횟수가 사라지면 정보 손실이다.");
        }

        [Test]
        public void null은_아무것도_찍지_않고_상태도_망가뜨리지_않는다()
        {
            var folder = new RepeatedLogFolder();
            folder.ShouldEmit(A, 0.0, 60.0, out _);
            folder.ShouldEmit(A, 1.0, 60.0, out _);

            Assert.IsFalse(folder.ShouldEmit(null, 2.0, 60.0, out int folded));
            Assert.AreEqual(0, folded);
            Assert.AreEqual(1, folder.PendingRepeats, "null 한 번에 접힌 카운트가 날아가면 안 된다.");
        }

        [Test]
        public void Reset은_직전줄_기억까지_지운다()
        {
            var folder = new RepeatedLogFolder();
            folder.ShouldEmit(A, 0.0, 60.0, out _);
            folder.ShouldEmit(A, 1.0, 60.0, out _);
            folder.Reset();

            Assert.AreEqual(0, folder.PendingRepeats);
            Assert.IsTrue(folder.ShouldEmit(A, 2.0, 60.0, out int folded),
                "Reset 뒤 같은 줄은 '첫 줄'이므로 다시 찍혀야 한다.");
            Assert.AreEqual(0, folded);
        }

        // ====================================================================================
        // 배선 잠금 — 정책만 있고 아무도 안 쓰면 로그는 그대로 자란다
        // ====================================================================================

        [Test]
        public void 가장_시끄러운_로그가_접기를_실제로_쓴다()
        {
            string src = File.ReadAllText(Path.Combine(
                Application.dataPath, "_Project", "Scripts", "Interaction", "IdleAmbientMotionRenderer.cs"));

            StringAssert.Contains("RepeatedLogFolder", src,
                "실측 71.5분 세션의 26%를 차지하던 줄이 접기를 쓰지 않으면 이 라운드의 감량은 0이다.");
            StringAssert.Contains("ShouldEmit(", src);

            int gate = src.IndexOf("PlayerLogPolicy.RoutineNarrationEnabled", System.StringComparison.Ordinal);
            int fold = src.IndexOf("ShouldEmit(", System.StringComparison.Ordinal);
            Assert.Greater(gate, 0);
            Assert.Greater(fold, gate,
                "verbose 스위치보다 접기가 앞에 오면, 로그를 꺼 둔 세션에서도 접기 상태가 굴러 " +
                "다시 켰을 때 엉뚱한 반복 횟수가 나온다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 감량한다고 <b>줄 자체를 없애 버리지는 않았는지</b> 확인한다.
        /// 이 저장소는 "고쳤다는 서사만 남는" 변경을 이번 라운드에만 여러 건 기각했다.
        /// </summary>
        [Test]
        public void 감량은_줄을_지우는_방식이_아니다()
        {
            string src = File.ReadAllText(Path.Combine(
                Application.dataPath, "_Project", "Scripts", "Interaction", "IdleAmbientMotionRenderer.cs"));
            StringAssert.Contains("$\"[유휴동작]", src, "태그 줄 자체가 사라지면 감량이 아니라 실명이다.");
            StringAssert.Contains("Debug.Log(line)", src, "첫 줄은 여전히 그대로 찍혀야 한다.");
        }
    }
}
