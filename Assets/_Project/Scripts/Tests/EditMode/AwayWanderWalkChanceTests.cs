using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// <b>자리 비움 배회 감쇄</b>(2026-09-07 design-motion 처방)의 계약.
    ///
    /// <para>처방 한 줄: <b>사용자가 3분 이상 입력이 없으면 배회 걷기 확률만 0.75 -> 0.15로 낮춘다.</b>
    /// 걷기가 곧 프레임 제출이고 제출이 곧 컴포지터 비용이라, 아무도 안 보는 밤에 그 duty cycle을
    /// 줄이는 것이 GPU 라운드에 남아 있던 가장 큰 지렛대였다
    /// (<c>FramePacing.StillDwellSeconds</c> 문서: *"남은 제출의 대부분은 캐릭터가 하루의 약 34%를
    /// 걸어다니기 때문이며, 그것은 페이싱이 아니라 배회 AI의 듀티 사이클 문제다"*).</para>
    ///
    /// ============================================================================
    /// ★ 이 파일이 지키는 것 중 하나는 <b>「하지 않은 것」</b>이다
    /// ============================================================================
    /// design-motion이 <b>절대 금지</b>로 못 박은 항목: <c>wanderIdleDurationMin/Max</c>는 무수정.
    /// 집중 세션은 그 값을 2~6초에서 4~11초로 늘렸고 그만큼 복귀 지연이 커졌는데, 자리 비움에 같은
    /// 수를 쓰면 <b>"돌아왔는데 캐릭터가 11초간 안 움직인다"</b>는 회귀가 된다.
    /// 확률만 낮추면 복귀 지연 p99가 13.37 -> 13.79초로 거의 변하지 않는다(시뮬레이션).
    ///
    /// <b>「안 했다」는 테스트가 가장 조용히 썩는다</b>(CLAUDE.md: 부재 단언은 썩으면 초록이 된다).
    /// 그래서 여기서는 부재를 <b>문자열이 없다</b>로 재지 않고, 다음 두 가지로 재정의했다:
    /// <list type="bullet">
    /// <item><b>실재 단언</b> — Idle 길이 상수가 <b>지금도 2.0/6.0이다</b>(값이 있어야 통과).</item>
    /// <item><b>구조 단언</b> — 자리 비움용 Idle 길이 설정이 <see cref="StickConfig"/>에
    ///   <b>애초에 존재하지 않는다</b>(리플렉션. 누가 추가하면 그 순간 빨개진다).</item>
    /// </list>
    /// </summary>
    public sealed class AwayWanderWalkChanceTests
    {
        private static StickConfig FreshConfig() => ScriptableObject.CreateInstance<StickConfig>();

        private static ViewerPresenceSnapshot Presence(float idleSeconds, bool asleep = false,
            bool locked = false)
            => new ViewerPresenceSnapshot(asleep, idleSeconds, lowPowerMode: false, onBattery: false,
                sessionLocked: locked);

        // ========================================================================
        // 불변식 1 — 판정은 순수하게 OS 관측(무입력 시간)에서만 온다
        // ========================================================================

        [Test]
        public void 자리비움_판정은_AwaySeconds_문턱을_그대로_쓴다()
        {
            float t = FramePacingPolicy.AwaySeconds;

            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(Presence(t - 0.01f)), "문턱 직전");
            Assert.IsTrue(FramePacingPolicy.IsViewerLikelyAway(Presence(t)), "문턱 정각");
            Assert.IsTrue(FramePacingPolicy.IsViewerLikelyAway(Presence(t + 0.01f)), "문턱 직후");
            Assert.IsTrue(FramePacingPolicy.IsViewerLikelyAway(Presence(28800f)), "8시간");

            // 프레임 등급의 Away와 **같은 문턱**을 본다는 것을 대조로 못 박는다.
            Assert.AreEqual(FramePacingTier.Away,
                FramePacingPolicy.DecideTier(Presence(t), suspendedForFullscreen: false, characterIdle: true),
                "대조군 전제 실패 — 같은 관측에서 프레임 등급이 Away가 아니면 두 판정의 문턱이 갈라진 것이다.");
        }

        [Test]
        public void 관측이_없으면_평소대로_걷는다()
        {
            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(default),
                "관측 실패(Valid=false)에서 자리 비움으로 판정하면, 에디터·테스트·적응형 페이싱이 꺼진 " +
                "실행에서 캐릭터가 이유 없이 조용해진다. '모르면 평소대로'가 안전한 쪽이다.");
        }

        [Test]
        public void 캐릭터_상태를_인자로_받지_않는다_되먹임_고리_금지()
        {
            // ★ design-motion이 반증 실험으로 확정한 항목. characterIdle을 받으면
            //   판정 -> 걷기 감소 -> 더 오래 정지 -> 판정 강화 의 되먹임이 생긴다.
            //   "안 받는다"를 문자열이 아니라 **시그니처**로 잰다(부재 단언을 실재 단언으로 바꾼 것).
            MethodInfo m = typeof(FramePacingPolicy).GetMethod(
                nameof(FramePacingPolicy.IsViewerLikelyAway),
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(m, "IsViewerLikelyAway가 사라졌다 — 이 테스트를 갱신하라.");

            ParameterInfo[] ps = m.GetParameters();
            Assert.AreEqual(1, ps.Length,
                "인자가 늘었다. 캐릭터 상태(characterIdle 등)를 받는 순간 되먹임 고리가 생긴다 — " +
                "design-motion이 반증 실험으로 기각한 설계다.");
            Assert.AreEqual(typeof(ViewerPresenceSnapshot), ps[0].ParameterType.GetElementType() ?? ps[0].ParameterType,
                "유일한 인자는 OS 관측이어야 한다.");
        }

        [Test]
        public void 화면꺼짐과_세션잠금은_자리비움으로_승격되지_않는다()
        {
            // 그 등급들은 제출이 이미 바닥이라 걷기 확률을 낮춰서 얻는 절감이 0이고, 대가만 남는다.
            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(Presence(0f, asleep: true)),
                "DisplayAsleep을 OR로 넣었다 — 화면이 꺼진 순간부터 캐릭터가 조용해지지만 절감은 0이다.");
            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(Presence(0f, locked: true)),
                "SessionLocked를 OR로 넣었다 — 같은 이유로 대가만 남는다.");

            // 네거티브 컨트롤 — 같은 관측에 무입력만 더하면 실제로 true가 된다(위 단언이 공허하지 않다).
            Assert.IsTrue(
                FramePacingPolicy.IsViewerLikelyAway(Presence(FramePacingPolicy.AwaySeconds, asleep: true)),
                "대조군 전제 실패 — 무입력 조건이 아예 안 걸리고 있다.");
        }

        // ========================================================================
        // 불변식 2 — 사다리는 단조 내림차순이고, 자리 비움은 «묶어두기»가 아니다
        // ========================================================================

        [Test]
        public void 걷기_확률_사다리는_단조_내림차순이다()
        {
            StickConfig c = FreshConfig();
            try
            {
                // 부채꼴(0) > 자리비움 > 집중 > 평소 순으로 확률이 커져야 한다. 이 순서가 깨지면
                // 두 조건이 겹쳤을 때 "더 시끄러운 쪽"이 이겨 버린다(사다리가 우선순위를 겸한다).
                Assert.Less(0f, c.awayWanderWalkChance,
                    "자리 비움이 0이면 «묶어두기»다 — 파쿠르·뛰어내리기·매달리기가 밤새 " +
                    "구조적으로 도달 불가가 된다(전부 걷기에서 갈라진다).");
                Assert.Less(c.awayWanderWalkChance, c.focusSessionWalkChance,
                    "자리 비움이 집중 세션보다 조용해야 한다 — 앞은 아무도 안 보고 뒤는 보고 있다.");
                Assert.Less(c.focusSessionWalkChance, c.wanderPostIdleWalkChance,
                    "집중 세션이 평소보다 조용해야 한다(기존 계약).");
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        [Test]
        public void 애셋_기본값이_코드_기본값과_같다()
        {
            // ★ 거짓 통과 #9: "애셋이 코드 기본값을 덮는데 애셋을 안 고쳐 스위치가 꺼진 채 출하될 뻔".
            StickConfig c = FreshConfig();
            try
            {
                string path = Path.Combine(Application.dataPath, "_Project", "Data", "DefaultStickConfig.asset");
                Assert.IsTrue(File.Exists(path), $"애셋을 찾지 못했다: {path}");
                string asset = File.ReadAllText(path);

                // 양성 대조 — 이미 있는 형제 키를 먼저 찾아 파서가 살아 있음을 보인다.
                Assert.IsTrue(asset.Contains("wanderPostIdleWalkChance:"),
                    "대조군 전제 실패 — 형제 키조차 못 찾는다면 이 파일 파싱이 죽은 것이다.");

                Assert.IsTrue(asset.Contains($"{nameof(StickConfig.awayWanderWalkChance)}:"),
                    "출하 애셋에 awayWanderWalkChance가 없다 — 애셋이 코드 기본값을 덮으므로 " +
                    "이 기능은 사용자 기기에서 0으로 직렬화되어 «절대 안 걷는다»가 된다.");

                float inAsset = ParseFloat(asset, nameof(StickConfig.awayWanderWalkChance));
                Assert.AreEqual(c.awayWanderWalkChance, inAsset, 1e-4f,
                    "코드 기본값과 출하 애셋 값이 갈라졌다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        private static float ParseFloat(string yaml, string key)
        {
            int i = yaml.IndexOf($"\n  {key}:", StringComparison.Ordinal);
            Assert.Greater(i, 0, $"{key}를 애셋에서 찾지 못했다.");
            int start = yaml.IndexOf(':', i) + 1;
            int end = yaml.IndexOf('\n', start);
            Assert.Greater(end, start, $"{key} 값 줄을 파싱하지 못했다.");
            string raw = yaml.Substring(start, end - start).Trim();
            Assert.IsTrue(float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v),
                $"{key} 값을 숫자로 읽지 못했다: \"{raw}\"");
            return v;
        }

        [Test]
        public void 음수는_기능을_꺼서_평소_확률로_되돌린다()
        {
            // 네거티브 컨트롤용 센티널. 0은 «절대 안 걷는다»라는 유효한 값이라 OFF로 쓰지 않는다.
            var blackboard = new StickmanBlackboard();
            StickConfig c = FreshConfig();
            try
            {
                blackboard.Config = c;

                c.awayWanderWalkChance = -1f;
                Assert.IsFalse(blackboard.IsViewerAwayWanderActive, "음수면 꺼져 있어야 한다.");

                // 0은 OFF가 아니다 — 스위치로서는 켜진 상태다(다만 걷지 않는다).
                c.awayWanderWalkChance = 0f;
                Assert.IsFalse(blackboard.IsViewerAwayWanderActive,
                    "에디터에는 OS 관측이 없으므로 여기서도 false여야 한다(아래 테스트가 그 이유를 잠근다).");
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }

            // ★ 위 두 단언은 에디터에 관측이 없어 **둘 다 false**라 서로를 구분하지 못한다.
            //   그래서 센티널 계약은 게이트 식 자체로 따로 잰다(구분하는 단언 하나를 반드시 남긴다).
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "States",
                "StickmanBlackboard.cs");
            string source = File.ReadAllText(path);

            int gate = source.IndexOf(nameof(StickmanBlackboard.IsViewerAwayWanderActive) + " =>",
                StringComparison.Ordinal);
            Assert.Greater(gate, 0, "게이트 프로퍼티가 사라졌다 — 이 테스트를 갱신하라.");
            int semi = source.IndexOf(';', gate);
            string expr = source.Substring(gate, semi - gate);

            StringAssert.Contains(nameof(StickConfig.awayWanderWalkChance) + " >= 0f", expr,
                "음수 센티널이 사라졌다 — 그러면 네거티브 컨트롤(기능 자체 끄기)이 불가능해지고, " +
                "0을 OFF로 쓰게 되어 «절대 안 걷는다»와 «기능 꺼짐»이 구분되지 않는다.");
            StringAssert.Contains(nameof(FramePacingPolicy.IsViewerLikelyAway), expr,
                "게이트가 정본 판정을 부르지 않는다 — 판정이 두 곳에서 갈라진다.");

            // 음성 대조 — 캐릭터 상태를 섞지 않았는가(되먹임 고리 금지).
            Assert.AreEqual(-1, expr.IndexOf("Idle", StringComparison.OrdinalIgnoreCase),
                "게이트가 캐릭터 상태를 읽는다 — IsViewerLikelyAway 문서의 되먹임 고리 항목 위반이다.");
        }

        [Test]
        public void 에디터_테스트에서는_관측이_없어_기능이_꺼져_있다()
        {
            // FramePacing.LastPresence는 실제 플레이어에서만 채워진다(ApplyOnce가 에디터에서 안 돈다).
            // 그래서 PlayMode/EditMode 테스트가 이 기능 때문에 타이밍이 흔들리는 일이 없다.
            FramePacing.ResetForTests();
            Assert.IsFalse(FramePacing.LastPresence.Valid,
                "에디터에서 관측이 채워져 있다 — 테스트 타이밍이 OS 상태에 의존하게 된다.");
            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(FramePacing.LastPresence));
        }

        // ========================================================================
        // 불변식 3 — ★ 절대 금지: Idle 길이는 건드리지 않았다
        // ========================================================================

        [Test]
        public void Idle_길이_기본값은_2초_6초_그대로다()
        {
            StickConfig c = FreshConfig();
            try
            {
                // 실재 단언이다(값이 있어야 통과) — 부재 단언으로 쓰면 조용히 썩는다.
                Assert.AreEqual(2.0f, c.wanderIdleDurationMin, 1e-4f,
                    "자리 비움 라운드가 Idle 최소 길이를 건드렸다. 복귀 지연이 커져 " +
                    "'돌아왔는데 캐릭터가 안 움직인다'가 된다 — design-motion 절대 금지 항목.");
                Assert.AreEqual(6.0f, c.wanderIdleDurationMax, 1e-4f,
                    "자리 비움 라운드가 Idle 최대 길이를 건드렸다 — design-motion 절대 금지 항목.");
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        [Test]
        public void 자리비움용_Idle_길이_설정은_존재하지_않는다()
        {
            // 「안 했다」를 구조로 잰다. 누가 awayWanderIdleDurationMin 같은 필드를 추가하는 순간
            // 빨개진다 — 집중 세션이 이미 그 길을 갔고 그 대가가 복귀 지연이었다.
            string[] offenders = typeof(StickConfig)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .Where(n => n.StartsWith("away", StringComparison.OrdinalIgnoreCase)
                            && n.IndexOf("IdleDuration", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "자리 비움 전용 Idle 길이 설정이 생겼다: " + string.Join(", ", offenders) +
                ". 확률만 낮추면 복귀 지연 p99가 13.37 -> 13.79초로 거의 안 변하지만, Idle 길이를 " +
                "늘리면 집중 세션과 같은 회귀(최악 11초 무반응)가 자리 비움에도 생긴다.");

            // 양성 대조 — 같은 프로브가 집중 세션 쪽에서는 실제로 항목을 찾아낸다.
            //   (0건이 "규칙 준수"인지 "프로브가 죽음"인지 가른다.)
            string[] focusIdle = typeof(StickConfig)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .Where(n => n.StartsWith("focus", StringComparison.OrdinalIgnoreCase)
                            && n.IndexOf("IdleDuration", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            CollectionAssert.IsNotEmpty(focusIdle,
                "대조군 전제 실패 — 집중 세션의 Idle 길이 설정조차 못 찾는다면 위 0건은 " +
                "'규칙을 지켰다'가 아니라 '프로브가 죽었다'는 뜻이다.");
        }

        [Test]
        public void Idle_길이를_고르는_분기는_집중세션만_본다()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "States",
                "AutoWanderController.cs");
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            string source = File.ReadAllText(path);

            // 양성 대조 먼저 — 프로브가 실제로 문자열을 찾아낼 수 있는가.
            Assert.IsTrue(source.Contains("awayWanderWalkChance"),
                "대조군 전제 실패 — 이번 라운드가 넣은 확률 배선조차 못 찾는다면 이 스캔은 무효다.");

            int enter = source.IndexOf("private void EnterResting()", StringComparison.Ordinal);
            Assert.Greater(enter, 0, "EnterResting이 사라졌다 — 이 테스트를 갱신하라.");
            int idleMax = source.IndexOf("wanderIdleDurationMax", enter, StringComparison.Ordinal);
            Assert.Greater(idleMax, enter, "EnterResting이 Idle 최대 길이를 읽지 않는다.");

            string body = source.Substring(enter, idleMax - enter);
            StringAssert.Contains("focusSessionIdleDurationMin", body,
                "집중 세션 분기가 사라졌다 — 이 테스트가 겨누는 지점이 옮겨갔다.");
            Assert.AreEqual(-1, body.IndexOf("away", StringComparison.OrdinalIgnoreCase),
                "Idle 길이를 고르는 분기가 자리 비움을 읽는다 — design-motion 절대 금지 항목이다. " +
                "자리 비움은 **확률만** 낮춘다.");
        }

        // ========================================================================
        // 불변식 4 — 배선(정적 스캔). 설정만 늘고 사다리에 안 꽂히면 전부 초록불이다
        // ========================================================================

        [Test]
        public void 사다리가_자리비움_확률을_실제로_읽는다()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "States",
                "AutoWanderController.cs");
            string source = File.ReadAllText(path);

            Assert.IsFalse(source.Contains("_thisIdentifierMustNotExist_"),
                "음성 대조 실패 — 없는 문자열이 발견됐다. 이 파일의 정적 스캔 결과 전부 무효다.");

            int branch = source.IndexOf("private void ResolvePostIdleBranch()", StringComparison.Ordinal);
            Assert.Greater(branch, 0, "ResolvePostIdleBranch가 사라졌다 — 이 테스트를 갱신하라.");
            int jump = source.IndexOf("wanderPostIdleJumpChance", branch, StringComparison.Ordinal);
            Assert.Greater(jump, branch, "추첨부를 파싱하지 못했다.");

            string ladder = source.Substring(branch, jump - branch);
            StringAssert.Contains("awayWanderWalkChance", ladder,
                "사다리가 자리 비움 확률을 읽지 않는다 — 설정만 있고 배선이 없으면 " +
                "밤새 여전히 0.75로 걷고 이 라운드의 절감이 0이다.");
            StringAssert.Contains("IsViewerAwayForWander", ladder, "자리 비움 게이트가 사다리에 없다.");
            StringAssert.Contains("focusSessionWalkChance", ladder, "집중 세션 갈래가 사라졌다(기존 계약).");
        }
    }
}
