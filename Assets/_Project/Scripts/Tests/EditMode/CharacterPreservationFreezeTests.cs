using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — 화면 변경 유예 동안의 캐릭터 「보존 동결」(<see cref="CharacterPreservationFreeze"/>) 잠금.
    /// 여기서는 순수 판정(가장자리·재기점), 계약 값 추종, 스펙터클 락 신규 획득 차단, 입구 네 곳의 배선 순서를 본다.
    /// 에이전트·말풍선의 실제 거동(벽시계)은 PlayMode <c>CharacterPreservationFreezeAgentTests</c>가 잰다.
    /// </summary>
    public sealed class CharacterPreservationFreezeTests
    {
        [SetUp]
        public void SetUp()
        {
            DisplayChangeHoldStatus.ResetForTesting();
            ReleaseAnyLock();
        }

        [TearDown]
        public void TearDown()
        {
            // 새면 뒤 테스트의 스펙터클 락 획득이 전부 거절된다.
            DisplayChangeHoldStatus.ResetForTesting();
            ReleaseAnyLock();
        }

        private static void ReleaseAnyLock()
        {
            object owner = SpectacleEventLock.CurrentOwner;
            if (owner != null) SpectacleEventLock.Release(owner);
        }

        private static void StartHold(int episode) =>
            DisplayChangeHoldStatus.PublishStarted(episode, DisplayChangeHoldStartReason.TopologyChangeDetected, 100);

        private static void ReleaseHold(int episode) =>
            DisplayChangeHoldStatus.PublishReleased(episode, DisplayChangeHoldReleaseReason.Settled, 200);

        // ==================== 1. 가장자리 ====================

        [Test]
        public void 래치는_시작_유지_에피소드교체_해제를_한_번씩만_낸다()
        {
            var latch = new PreservationFreezeLatch();

            Assert.AreEqual(PreservationFreezeEdge.None, latch.Step(false, 0, 10f, 100));
            Assert.AreEqual(PreservationFreezeEdge.Started, latch.Step(true, 1, 10.5f, 130));
            Assert.IsTrue(latch.IsFrozen);
            Assert.AreEqual(10.5f, latch.FrozenAtUnscaledTime, 1e-5f);

            Assert.AreEqual(PreservationFreezeEdge.None, latch.Step(true, 1, 11f, 160),
                "같은 에피소드가 이어지는 동안에는 가장자리가 없다 — 매 프레임 진입 처리가 돌면 안 된다.");

            Assert.AreEqual(PreservationFreezeEdge.Continued, latch.Step(true, 2, 11.5f, 190));
            Assert.IsTrue(latch.IsFrozen, "해제와 새 시작이 두 읽기 사이에 끝났으면 동결을 풀지 않는다.");
            Assert.AreEqual(10.5f, latch.FrozenAtUnscaledTime, 1e-5f,
                "동결 기점은 첫 시작 그대로여야 한다 — 잡담 쿨다운이 동결 전체 길이만큼 밀려야 한다.");

            Assert.AreEqual(PreservationFreezeEdge.Released, latch.Step(false, 2, 13f, 220));
            Assert.IsFalse(latch.IsFrozen);
            Assert.AreEqual(2.5f, latch.LastFrozenSeconds, 1e-5f);
            Assert.AreEqual(90, latch.LastFrozenFrames);

            Assert.AreEqual(PreservationFreezeEdge.None, latch.Step(false, 2, 14f, 250));
        }

        // ==================== 2. 재기점 ====================

        [Test]
        public void 남아_있던_기한만_동결_길이만큼_밀리고_지난_기한은_되살아나지_않는다()
        {
            // 얼린 시각 10초, 해제 15초 = 동결 5초. 기대값은 손 계산이다(프로덕션 함수로 만들지 않는다).
            Assert.AreEqual(17f, CharacterPreservationFreeze.RebaseDeadline(12f, 10f, 15f), 1e-5f,
                "얼릴 때 남아 있던 2초는 해제 뒤에도 2초여야 한다.");
            Assert.AreEqual(9f, CharacterPreservationFreeze.RebaseDeadline(9f, 10f, 15f), 1e-5f,
                "얼리기 전에 지난 기한은 그대로다 — 되살리면 해제 뒤 몰아서 추첨한다(조건 7).");
            Assert.AreEqual(10f, CharacterPreservationFreeze.RebaseDeadline(10f, 10f, 15f), 1e-5f,
                "얼린 순간 딱 만료된 기한도 되살리지 않는다.");
            Assert.AreEqual(0f, CharacterPreservationFreeze.RebaseDeadline(0f, 10f, 15f), 1e-5f,
                "쿨다운 없음(0)은 그대로다.");
        }

        // ==================== 3. 사실의 원천 ====================

        [Test]
        public void 판정은_유예_계약_값을_그대로_따른다()
        {
            Assert.IsFalse(CharacterPreservationFreeze.IsDisplayChangeHoldActive, "전제: 초기화 직후 유예 없음");

            StartHold(1);
            Assert.IsTrue(CharacterPreservationFreeze.IsDisplayChangeHoldActive);
            Assert.IsTrue(CharacterPreservationFreeze.BlocksNewDialogue);
            Assert.IsTrue(CharacterPreservationFreeze.BlocksNewSpectacle);

            ReleaseHold(1);
            Assert.IsFalse(CharacterPreservationFreeze.IsDisplayChangeHoldActive);
            Assert.IsFalse(CharacterPreservationFreeze.BlocksNewDialogue);
            Assert.IsFalse(CharacterPreservationFreeze.BlocksNewSpectacle);
        }

        // ==================== 4. 스펙터클 락 ====================

        [Test]
        public void 유예_중에는_새_연출을_잡지_못하고_이미_쥔_주인의_재진입은_된다()
        {
            object holder = new object();
            object newcomer = new object();

            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, holder), "전제: 유예 전에는 잡힌다");

            StartHold(1);
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, holder),
                "진행 중 연출을 끊지 않는다 — 이미 쥔 주인의 재진입은 유예 중에도 된다.");
            SpectacleEventLock.Release(holder);
            Assert.IsFalse(SpectacleEventLock.IsActive, "전제: 락이 비었다");

            Assert.IsFalse(SpectacleEventLock.TryAcquire(SpectacleEventKind.Dance, newcomer),
                "락이 비었는데도 거절돼야 한다 — 남은 거절 사유는 동결뿐이다.");
            Assert.IsFalse(SpectacleEventLock.IsActive, "거절이 락을 반쯤 잡은 채 남기면 안 된다.");

            ReleaseHold(1);
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Dance, newcomer),
                "양성 대조: 유예가 끝나면 같은 요청이 잡힌다 — 위 거절이 동결 때문이었음을 가른다.");
        }

        // ==================== 5. 입구 네 곳의 배선 순서 ====================

        /// <summary>
        /// design-narrative 조건 6 — 전이를 <b>입구에서</b> 미룬다. 네 입구 모두 동결 가드가 타이머 누적·락 획득·전이보다
        /// 앞에 있어야 한다(뒤에 있으면 타이머가 유예 중 쌓여 해제 직후 몰아서 발동한다).
        /// 거동 검사는 PlayMode(가출·드래그)가 하고, 여기서는 네 곳 전부의 순서를 소스로 본다.
        /// </summary>
        [Test]
        public void 입구_네_곳은_타이머와_전이보다_먼저_동결을_본다()
        {
            string guard = "." + nameof(StickmanAgent.IsPreservationFrozen);
            var entrances = new (string file, string signature, string[] mustFollowGuard)[]
            {
                ("WindowTheftDirector.cs", "private void TickAutoTrigger(",
                    new[] { "_checkTimer +=", Transition(StickmanStateId.WindowTheft) }),
                ("RunawayDirector.cs", "private void Update(",
                    new[] { "SpectacleEventLock." + nameof(SpectacleEventLock.TryAcquire), Transition(StickmanStateId.Runaway) }),
                ("DanceEpisodeDirector.cs", "private void Update(",
                    new[] { "_restRemaining -=", "TickStartAttempt(" }),
                ("DragThrowController.cs", "private void OnMouseDown(",
                    new[] { "SpectacleEventLock." + nameof(SpectacleEventLock.TryAcquire), Transition(StickmanStateId.Dragged) }),
            };
            Assert.AreEqual(4, entrances.Length, "빈 목록으로 아무것도 안 재고 초록이 되는 형태를 막는다.");

            foreach (var e in entrances)
            {
                string src = StripLineComments(File.ReadAllText(
                    Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction", e.file)));
                string body = TryMethodBody(src, e.signature);
                Assert.IsNotNull(body, $"{e.file}에서 {e.signature}를 찾지 못했다 — 감사 대상이 없다.");

                string broken = Diagnose(body, guard, e.mustFollowGuard);
                Assert.IsNull(broken, $"{e.file} {e.signature} — {broken}");

                // 양성 대조: 가드를 지운 사본에서 같은 판정이 실제로 빨개지는가.
                Assert.IsNotNull(Diagnose(body.Replace(guard, ".NotAFreezeGuard"), guard, e.mustFollowGuard),
                    $"{e.file}: 가드를 지운 사본에서도 초록이다 — 매처가 눈이 멀었다.");
            }
        }

        private static string Transition(StickmanStateId id) => "ChangeState(StickmanStateId." + id + ")";

        /// <summary>성립하면 null. 판정과 양성 대조가 같은 코드를 쓴다.</summary>
        private static string Diagnose(string body, string guard, string[] mustFollowGuard)
        {
            int g = body.IndexOf(guard, System.StringComparison.Ordinal);
            if (g < 0) return "동결 가드가 없다";
            foreach (string needle in mustFollowGuard)
            {
                int at = body.IndexOf(needle, System.StringComparison.Ordinal);
                if (at < 0) return $"'{needle}'이(가) 본문에 없다 — 감사가 낡았다(대상이 바뀌었으면 니들을 고쳐라)";
                if (at < g) return $"'{needle}'이(가) 동결 가드보다 먼저 온다";
            }
            return null;
        }

        private static string StripLineComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            foreach (string line in src.Replace("\r\n", "\n").Split('\n'))
            {
                int c = line.IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(c >= 0 ? line.Substring(0, c) : line).Append('\n');
            }
            return sb.ToString();
        }

        private static string TryMethodBody(string source, string signature)
        {
            int at = source.IndexOf(signature, System.StringComparison.Ordinal);
            if (at < 0) return null;
            int open = source.IndexOf('{', at);
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            return null;
        }
    }
}
