using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
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

        // ====================================================================================
        // ★★ R2-4 회귀 잠금 — "접기가 한 번도 실행되지 않는다"
        // ====================================================================================
        // 2026-09-02 검증: 직전 라운드의 배선은
        //     if (!PlayerLogPolicy.RoutineNarrationEnabled) return;   // 여기서 끝
        //     ... _logFolder.ShouldEmit(...)                          // 도달 불가
        // 였고, PlayerLogPolicy가 부팅 스냅샷이라 진단 로그를 켜도 이 조기 반환을 넘지 못했다.
        // 실측: 토글 ON 후 3분 -> [유휴동작] 0줄 / 접음 0줄 (같은 3분에 [발판리포트] 84, [눈추적] 106).
        //
        // ★ 그리고 그때의 검사는 **소스 문자열 순서만** 보고 "gate < fold"를 오히려 요구했다 —
        //   즉 검사 자신이 도달 불가 배선을 초록으로 통과시켰다. 이 저장소가 반복해 당한 유형이라,
        //   여기서는 순서 검사를 **뒤집는 것으로 끝내지 않고 실제로 돌려 본다.**

        /// <summary>
        /// ★ 실행 검사 — 로그 스위치가 <b>꺼져 있어도</b> 접기 상태가 실제로 전진한다.
        /// 소스 순서가 아니라 <b>동작</b>으로 도달 가능성을 증명한다.
        /// </summary>
        [Test]
        public void 접기는_로그스위치가_꺼져있어도_실제로_돈다()
        {
            var folder = new RepeatedLogFolder();
            bool was = false;

            for (int i = 0; i < 5; i++)
            {
                var d = IdleAmbientMotionRenderer.DecideIdleLog(
                    ref folder, ref was, narrationEnabled: false,
                    motionName: "주위 살피기", stateKey: 3, seconds: 2.40f,
                    now: i, holdSeconds: 60.0);

                Assert.IsFalse(d.EmitLine, "스위치가 꺼져 있으면 아무것도 찍지 않는다.");
                Assert.AreEqual(0, d.FoldedRepeats);
            }

            Assert.AreEqual(4, folder.PendingRepeats,
                "접기 상태가 전진하지 않았다 = 접기 코드가 도달 불가다(R2-4 재발).");
        }

        /// <summary>
        /// ★ 실행 검사 — 스위치가 켜져 있으면 반복이 진짜로 접힌다(줄이 실제로 줄어든다).
        /// </summary>
        [Test]
        public void 스위치가_켜지면_반복이_실제로_접힌다()
        {
            var folder = new RepeatedLogFolder();
            bool was = true;
            int emitted = 0, foldSummaries = 0;

            for (int i = 0; i < 30; i++)
            {
                var d = IdleAmbientMotionRenderer.DecideIdleLog(
                    ref folder, ref was, narrationEnabled: true,
                    "주위 살피기", 3, 2.40f, i, 60.0);
                if (d.EmitLine) emitted++;
                if (d.FoldedRepeats > 0) foldSummaries++;
            }

            Assert.AreEqual(1, emitted, "30번의 동일 이벤트에서 줄은 딱 1개만 나와야 한다.");
            Assert.AreEqual(0, foldSummaries, "홀드(60초) 전에는 요약도 없다.");
            Assert.AreEqual(29, folder.PendingRepeats, "접힌 29회가 보존되어야 한다.");
        }

        /// <summary>
        /// 스위치를 넘나들면 접기 상태를 비운다 — 그러지 않으면 로그를 켜자마자
        /// "직전과 동일 N회 반복"으로 <b>아무도 본 적 없는 줄</b>의 횟수가 튀어나온다.
        /// </summary>
        [Test]
        public void 스위치_전환에서_묵은_카운트가_새_구간으로_넘어오지_않는다()
        {
            var folder = new RepeatedLogFolder();
            bool was = true;

            for (int i = 0; i < 50; i++)
            {
                IdleAmbientMotionRenderer.DecideIdleLog(ref folder, ref was, false,
                    "주위 살피기", 3, 2.40f, i, 60.0);
            }
            Assert.AreEqual(49, folder.PendingRepeats);

            var first = IdleAmbientMotionRenderer.DecideIdleLog(ref folder, ref was, true,
                "주위 살피기", 3, 2.40f, 50, 60.0);

            Assert.IsTrue(first.EmitLine, "켠 직후 첫 줄은 반드시 보여야 한다.");
            Assert.AreEqual(0, first.FoldedRepeats,
                "꺼져 있던 구간의 49회가 새 구간의 첫 줄로 새어 나왔다.");
        }

        /// <summary>
        /// 배선 검사(동작 검사의 보조) — 조기 반환으로 접기를 건너뛰는 형태가 되살아나지 않게 한다.
        /// ★ 이것만으로는 부족하다는 것이 R2-4의 교훈이라, 위 실행 검사들과 <b>함께</b>만 의미가 있다.
        /// </summary>
        [Test]
        public void 로그스위치_조기반환이_접기_앞을_막지_않는다()
        {
            string src = File.ReadAllText(Path.Combine(
                Application.dataPath, "_Project", "Scripts", "Interaction", "IdleAmbientMotionRenderer.cs"));

            StringAssert.DoesNotContain("if (!Platform.PlayerLogPolicy.RoutineNarrationEnabled) return;", src,
                "스위치 조기 반환이 돌아왔다 — 그 뒤의 접기는 다시 도달 불가가 된다(R2-4).");
            StringAssert.Contains("DecideIdleLog(", src,
                "접기/스위치 판정이 실행 검사가 닿는 결정 함수를 거치지 않는다.");

            // 조립은 여전히 스위치 뒤여야 한다 — 접기가 '키'로 판정하므로 둘 다 만족할 수 있다.
            int gate = src.IndexOf("PlayerLogPolicy.RoutineNarrationEnabled", System.StringComparison.Ordinal);
            int build = src.IndexOf("$\"[유휴동작]", System.StringComparison.Ordinal);
            Assert.Greater(gate, 0);
            Assert.Greater(build, gate, "꺼져 있어도 보간 문자열이 만들어지면 24시간 상주 컨벤션 위반이다.");
        }

        // ====================================================================================
        // ★★ R2-4 근본 원인 — 로그 스위치가 **부팅 스냅샷**이라 런타임 토글이 도달하지 못했다
        // ====================================================================================

        /// <summary>
        /// <c>PlayerLogPolicy.Configure</c>의 호출처는 <c>Platform/FootholdPoller.cs</c> 생성자
        /// <b>한 곳뿐</b>이다. 그런데 개발자 도구의 진단 로그 토글
        /// (<c>Interaction/AppControlDirector.cs</c>)은 <c>StickConfig.verboseDiagnosticsLogging</c>을
        /// <b>런타임에 직접 뒤집는다</b>. 스위치를 복사해 두면 그 토글이 영원히 도달하지 못한다 —
        /// 실측으로 <c>[유휴동작]</c>만 0줄이었고 <c>[발판리포트]</c>/<c>[눈추적]</c>은 멀쩡했는데,
        /// 그 둘은 설정을 <b>매번 읽기</b> 때문이었다.
        ///
        /// <para>그래서 이 테스트는 <b>Configure를 다시 부르지 않는다</b>. 그게 실기 조건이다.</para>
        /// </summary>
        [Test]
        public void 진단로그_런타임_토글이_Configure_재호출_없이_반영된다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                config.verboseDiagnosticsLogging = false;
                PlayerLogPolicy.Configure(config);          // 부팅 시 단 한 번(실기와 동일)
                Assert.IsFalse(PlayerLogPolicy.RoutineNarrationEnabled);

                // ★ 개발자 도구가 하는 일은 이것뿐이다 — Configure를 다시 부르지 않는다.
                config.verboseDiagnosticsLogging = true;

                Assert.IsTrue(PlayerLogPolicy.RoutineNarrationEnabled,
                    "런타임 토글이 반영되지 않는다 = 진단 수단이 조용히 죽어 있다(R2-4의 근본 원인).");

                config.verboseDiagnosticsLogging = false;
                Assert.IsFalse(PlayerLogPolicy.RoutineNarrationEnabled, "끄는 방향도 즉시 반영되어야 한다.");
            }
            finally
            {
                PlayerLogPolicy.ResetForTests();
                Object.DestroyImmediate(config);
            }
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
            StringAssert.Contains("Debug.Log($\"[유휴동작]", src,
                "첫 줄은 여전히 **원문 그대로** 찍혀야 한다 — 접기는 반복만 접는 것이지 요약으로 " +
                "바꿔치기하는 것이 아니다.");
            StringAssert.Contains("RepeatedLogFolder.Describe(", src,
                "접힌 횟수를 방출하는 줄이 없으면 접힌 정보가 통째로 사라진다.");
        }
    }
}
