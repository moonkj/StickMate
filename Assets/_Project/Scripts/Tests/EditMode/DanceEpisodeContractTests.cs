using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 음악 반응 춤 — <b>에피소드 계약</b>(2026-09-06 신설).
    ///
    /// <para>사용자 요청 2026-09-03 <i>"시스템에서 노래가 나오면 상호 반응해서 춤추는 동작을 넣어줘"</i>의
    /// 2·3층(에피소드/자세)을 잠근다. 1층(감지→게이트)은
    /// <c>AudioReactiveDancePolicyTests</c>/<c>AudioReactiveDanceRunnerTests</c>가 이미 본다.</para>
    ///
    /// ============================================================================
    /// 무엇을 잠그는가 — 전부 <b>씬 없이</b> 재는 것들이다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>에피소드 길이</b>가 정책의 창 안에 들어오는가. ★ 8/16초를 <b>숫자로 베끼지 않고</b>
    ///     <see cref="AudioReactiveDancePolicy.MotionEpisodeMinSeconds"/>를 참조한다 —
    ///     하드코딩하면 정책의 T₃와 갈라져도 조용히 초록이 된다.</item>
    ///   <item><b>T₃(최소 유지)가 최단 에피소드를 자르지 않는가.</b> 그리고 그것이 우연이 아니라
    ///     <c>T₃ = MotionEpisodeMinSeconds</c>라는 <b>구조적 짝</b>임을 함께 못박는다.</item>
    ///   <item><b>감시견 두 개(하드캡·정상퇴장 예산)가 정상 동작에서 걸리지 않는가.</b>
    ///     걸린다는 것은 버그가 있다는 뜻이므로 «걸리지 않는다»를 재는 것이 맞다.</item>
    ///   <item><b>발판 게이트</b>가 동작별 요구를 지키는가(양성/음성 대조 짝으로).</item>
    ///   <item><b>피로 램프</b>가 창 안에서 자라고 상한에서 멈추고 창이 닫히면 되돌아오는가.</item>
    ///   <item><b>폴백 리터럴</b>이 설정 기본값과 같은가 — 정규식 스캐너
    ///     (<see cref="ConfigFallbackLiteralDriftTests"/>)와 <b>다른 방법</b>으로 다시 잰다.</item>
    /// </list>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 이 경로에는 <c>UNITY_STANDALONE</c> 분기가 한 곳도 없다
    /// (자세·박자·소유권 계약은 양 플랫폼이 같은 코드를 탄다 — 갈라지는 것은 1층 프로브뿐이다).</para>
    /// </summary>
    public sealed class DanceEpisodeContractTests
    {
        private const string LogPrefix = "[춤계약-TEST]";
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>추첨이 걸린 값을 <b>표본</b>으로 훑는 횟수. 한 번만 뽑으면 «우연히 좋은 값»을 재게 된다.</summary>
        private const int DrawSamples = 400;

        private static StickConfig LoadDeployedConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(cfg, $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {DeployedConfigPath}");
            return cfg;
        }

        private static StickmanBlackboard MakeBlackboard(StickConfig cfg, string danceId)
        {
            // Body가 없으므로 CharacterHeightWorld는 배포 기준 신장으로 떨어지고, 포즈 애니메이터/눈은
            // 만들어지지 않는다(둘 다 null 안전). 이 테스트가 재는 것은 **박자와 길이**라 그거면 충분하다.
            return new StickmanBlackboard { Config = cfg, DanceId = danceId, DanceEpisodeIndex = 1 };
        }

        private static DanceState EnterEpisode(StickConfig cfg, string danceId)
        {
            var state = new DanceState(MakeBlackboard(cfg, danceId));
            state.Enter(null);   // context가 null이면 대사 경로를 타지 않는다(그건 PlayMode에서 잰다).
            return state;
        }

        // ================================================================================
        // ① 에피소드 길이 — 정책의 창 안에 들어오는가
        // ================================================================================

        [Test]
        public void 모든_동작의_에피소드가_정책이_정한_창_안에_들어온다()
        {
            StickConfig cfg = LoadDeployedConfig();
            float min = AudioReactiveDancePolicy.MotionEpisodeMinSeconds;
            float max = AudioReactiveDancePolicy.MotionEpisodeMaxSeconds;
            float brake = cfg.danceEntryBrakeSeconds;

            foreach (string id in DanceIds.CreateAll())
            {
                float lowest = float.MaxValue;
                float highest = float.MinValue;
                var seenLoopCounts = new HashSet<int>();

                for (int i = 0; i < DrawSamples; i++)
                {
                    DanceState state = EnterEpisode(cfg, id);
                    float loopsPart = state.PlannedEpisodeSeconds - brake;
                    lowest = Mathf.Min(lowest, loopsPart);
                    highest = Mathf.Max(highest, loopsPart);
                    seenLoopCounts.Add(state.LoopTarget);
                }

                Assert.GreaterOrEqual(lowest, min - 0.001f,
                    $"{LogPrefix} {id}의 최단 에피소드가 {lowest:F2}초로 정책 하한 {min:F2}초보다 짧습니다. " +
                    "정책의 Stop이 에피소드 한가운데를 자릅니다(T₃와의 구조적 짝이 깨집니다).");
                Assert.LessOrEqual(highest, max + 0.001f,
                    $"{LogPrefix} {id}의 최장 에피소드가 {highest:F2}초로 정책 상한 {max:F2}초를 넘습니다.");

                // ★ 네거티브 컨트롤 — 추첨이 실제로 돌고 있는가. 한 값에 고정돼 있으면 위 두 단언은
                //   "그 한 값이 우연히 창 안에 있다"만 확인한 셈이 된다.
                Assert.Greater(seenLoopCounts.Count, 1,
                    $"{LogPrefix} {id}의 루프 수가 {DrawSamples}회 추첨에서 한 값으로 고정됐습니다 " +
                    $"({string.Join(",", seenLoopCounts)}) — 사용자가 요구한 «매번 같지 않다»가 깨집니다.");

                Debug.Log($"{LogPrefix} {id} — 에피소드 {lowest:F2}~{highest:F2}초, " +
                    $"루프 수 {seenLoopCounts.Count}종.");
            }
        }

        [Test]
        public void T3_최소유지가_최단_에피소드를_자르지_않는다()
        {
            // ★ 이것이 «우연이 아니라 구조적 짝»이라는 주장의 코드 표현이다. 두 상수가 갈라지는 순간
            //    아래 여유 계산이 통째로 무의미해지므로 **먼저** 못박는다.
            Assert.AreEqual(AudioReactiveDancePolicy.MotionEpisodeMinSeconds,
                AudioReactiveDancePolicy.MinimumHoldSeconds, 0.0001f,
                $"{LogPrefix} 정책의 T₃(최소 유지)와 모션의 에피소드 하한이 갈라졌습니다. " +
                "이 둘은 같은 값을 가리켜야 하고, 에피소드 하한을 내리면 T₃도 함께 내려야 합니다 — " +
                "안 그러면 정책의 Stop이 에피소드 한가운데를 자릅니다.");

            StickConfig cfg = LoadDeployedConfig();
            float hold = AudioReactiveDancePolicy.MinimumHoldSeconds;
            float worst = float.MaxValue;
            string worstId = null;

            foreach (string id in DanceIds.CreateAll())
            {
                for (int i = 0; i < DrawSamples; i++)
                {
                    DanceState state = EnterEpisode(cfg, id);
                    if (state.PlannedEpisodeSeconds >= worst) continue;
                    worst = state.PlannedEpisodeSeconds;
                    worstId = id;
                }
            }

            Assert.GreaterOrEqual(worst, hold,
                $"{LogPrefix} 최단 에피소드가 {worst:F2}초({worstId})로 T₃ {hold:F2}초보다 짧습니다 — " +
                "정책이 Stop을 낼 수 있게 되는 시점이 에피소드가 끝나기 전입니다.");
            Debug.Log($"{LogPrefix} 최단 에피소드 {worst:F3}초({worstId}) vs T₃ {hold:F2}초 — " +
                $"여유 {worst - hold:F3}초. 진입 브레이크 {cfg.danceEntryBrakeSeconds:F2}초가 그 여유의 몫이다.");
        }

        [Test]
        public void 하드캡_감시견은_정상_에피소드에서는_걸리지_않는다()
        {
            StickConfig cfg = LoadDeployedConfig();
            float cap = cfg.danceEpisodeHardCapSeconds;
            float worst = 0f;
            string worstId = null;

            foreach (string id in DanceIds.CreateAll())
            {
                for (int i = 0; i < DrawSamples; i++)
                {
                    DanceState state = EnterEpisode(cfg, id);
                    if (state.PlannedEpisodeSeconds <= worst) continue;
                    worst = state.PlannedEpisodeSeconds;
                    worstId = id;
                }
            }

            Assert.Less(worst, cap,
                $"{LogPrefix} 정상 최악 에피소드가 {worst:F2}초({worstId})로 하드캡 {cap:F1}초에 닿습니다. " +
                "하드캡은 연출값이 아니라 감시견이라, 정상 동작에서 걸리면 경고 로그가 상시로 쌓입니다.");
            Debug.Log($"{LogPrefix} 정상 최악 {worst:F2}초({worstId}) vs 하드캡 {cap:F1}초 — 여유 {cap - worst:F2}초.");
        }

        // ================================================================================
        // ② 정상 퇴장 — 「음악 끊겼는데 왜 계속 추지」의 상한
        // ================================================================================

        [Test]
        public void 정상퇴장_최악_대기가_예산_안에_있다()
        {
            StickConfig cfg = LoadDeployedConfig();
            float budget = cfg.danceGracefulExitBudgetSeconds;
            string[] ids = DanceIds.CreateAll();

            float globalWorst = 0f;
            string globalWorstId = null;

            for (int move = 0; move < ids.Length; move++)
            {
                DanceState state = EnterEpisode(cfg, ids[move]);
                float loop = state.LoopSeconds;
                float outro = state.OutroSeconds;
                // 문워크만 퇴장 직전에 방향 전환이 끼어 있을 수 있다(4루프마다). 최악을 재는 자리라 더한다.
                float pending = ids[move] == DanceIds.Moonwalk ? cfg.danceMoonwalkTurnSeconds : 0f;

                float worst = 0f;
                const int Steps = 4000;
                for (int i = 0; i < Steps; i++)
                {
                    float phase = i / (float)Steps;
                    float wait = DistanceToNextBoundary(move, phase) * loop + outro + pending;
                    if (wait > worst) worst = wait;
                }

                Assert.LessOrEqual(worst, budget + 0.0001f,
                    $"{LogPrefix} {ids[move]}의 정상 퇴장 최악 대기가 {worst:F3}초로 예산 {budget:F2}초를 넘습니다 " +
                    $"(루프 {loop:F2} + 퇴장 {outro:F2} + 전환 {pending:F2}). " +
                    "예산을 넘으면 상태가 감시견에 강제로 끊겨 자세가 임의 각도에서 멎습니다.");

                if (worst > globalWorst) { globalWorst = worst; globalWorstId = ids[move]; }
                Debug.Log($"{LogPrefix} {ids[move]} 정상 퇴장 최악 {worst:F3}초(루프 {loop:F3} / 퇴장 {outro:F2}).");
            }

            Debug.Log($"{LogPrefix} 전체 최악 {globalWorst:F3}초({globalWorstId}) vs 예산 {budget:F2}초.");
            // ★ 네거티브 컨트롤 — 예산이 «아무 값이나 통과하는 헐거운 상한»이면 위 단언이 무의미하다.
            //   설계상 최악은 D1 피루엣이고 예산은 그 바로 위에 잡혀 있어야 한다.
            Assert.Greater(globalWorst, budget * 0.5f,
                $"{LogPrefix} 최악 대기 {globalWorst:F3}초가 예산 {budget:F2}초의 절반도 안 됩니다 — " +
                "경계 판정이 «항상 즉시 나갈 수 있다»로 무너졌을 때와 구분되지 않습니다(거짓 초록).");
            Assert.AreEqual(DanceIds.Pirouette, globalWorstId,
                $"{LogPrefix} 정상 퇴장의 구속 동작이 피루엣이 아니라 {globalWorstId}입니다. " +
                "구속이 바뀌었다면 예산(1.90초)의 유도도 함께 바뀌어야 합니다.");
        }

        /// <summary>
        /// 위상 <paramref name="phase01"/>에서 <b>다음 퇴장 허용 경계까지의 거리</b>(루프 비율).
        /// 판정 자체는 프로덕션의 <see cref="DanceState.BoundaryOrdinal"/>을 그대로 소비한다 —
        /// 규칙을 여기 다시 적으면 두 벌이 되어 함께 틀어진다.
        /// </summary>
        private static float DistanceToNextBoundary(int moveIndex, float phase01)
        {
            const int Resolution = 20000;
            int start = DanceState.BoundaryOrdinal(moveIndex, phase01);
            for (int i = 1; i <= Resolution; i++)
            {
                float p = phase01 + i / (float)Resolution;
                // ★ 위상 1.0 «까지» 본다. 여기서 1.0을 빼먹으면 마지막 구간의 대기가 다음 루프까지
                //   부풀어 실제보다 나쁘게 나온다(루프 끝은 대부분의 동작에서 그 자체가 허용 경계다).
                if (p > 1f) break;
                if (DanceState.BoundaryOrdinal(moveIndex, p) > start) return i / (float)Resolution;
            }
            // 루프를 넘어가면 위상이 0부터 다시 시작한다.
            return 1f - phase01 + NextBoundaryFromZero(moveIndex);
        }

        private static float NextBoundaryFromZero(int moveIndex)
        {
            const int Resolution = 20000;
            int start = DanceState.BoundaryOrdinal(moveIndex, 0f);
            for (int i = 1; i <= Resolution; i++)
            {
                float p = i / (float)Resolution;
                if (DanceState.BoundaryOrdinal(moveIndex, p) > start) return p;
            }
            return 1f;
        }

        [Test]
        public void 퇴장_허용_경계의_개수가_동작마다_사양대로다()
        {
            // 로봇만 8개(8키가 각각 완결 포즈), 나머지는 2개다. 개수가 바뀌면 위 최악 대기 계산의
            // 전제가 통째로 바뀐다.
            //
            // ★ 피루엣과 스타점프의 «2개»는 반루프가 아니다 — 구속 구간의 <b>양 끝</b>이다:
            //   피루엣은 ③회전+④닫기가 구속이므로 경계가 «②의 끝»과 «④의 끝»이고,
            //   스타점프는 ②~⑦(도약~복귀)이 구속이므로 «①의 끝»과 «사이클 끝»이다.
            //   ★ 피루엣에서 «④의 끝» 하나만 두면 ⑤ 호흡 도중에 온 요청이 다음 세트를 통째로
            //     기다려 2.86초가 되고 예산 1.90초를 넘긴다(이 검사가 실제로 잡은 결함이다).
            AssertBoundaryCount(StickmanPoseAnimator.DanceMovePirouette, 2, "피루엣");
            AssertBoundaryCount(StickmanPoseAnimator.DanceMoveStarJump, 2, "스타점프");
            AssertBoundaryCount(StickmanPoseAnimator.DanceMoveMoonwalk, 2, "문워크");
            AssertBoundaryCount(StickmanPoseAnimator.DanceMoveRunningMan, 2, "러닝맨");
            AssertBoundaryCount(StickmanPoseAnimator.DanceMoveRobot,
                StickmanPoseAnimator.RobotKeyCount, "로봇");
            AssertBoundaryCount(StickmanPoseAnimator.DanceMovePrisyadka, 2, "프리샤트카");
            AssertBoundaryCount(StickmanPoseAnimator.DanceMoveHorseDance, 2, "말춤");
        }

        private static void AssertBoundaryCount(int moveIndex, int expected, string label)
        {
            const int Steps = 20000;
            int crossings = 0;
            int previous = DanceState.BoundaryOrdinal(moveIndex, 0f);
            for (int i = 1; i <= Steps; i++)
            {
                int current = DanceState.BoundaryOrdinal(moveIndex, i / (float)Steps);
                if (current > previous) crossings++;
                previous = current;
            }
            Assert.AreEqual(expected, crossings,
                $"{LogPrefix} {label}의 루프당 퇴장 허용 경계가 {crossings}개입니다(사양 {expected}개).");
        }

        // ================================================================================
        // ③ 발판 게이트 — 못 하는 것을 하는 척하지 않는다
        // ================================================================================

        [Test]
        public void 발판_게이트가_동작별_요구를_지킨다()
        {
            float inPlace = DanceEpisodeDirector.InPlaceClearanceHeights;
            float moon = DanceEpisodeDirector.MoonwalkClearanceHeights;
            float star = DanceEpisodeDirector.StarJumpClearanceHeights;

            Assert.Less(inPlace, moon, $"{LogPrefix} 제자리 요구가 문워크 요구보다 큽니다 — 유도가 뒤집혔습니다.");
            Assert.Less(moon, star, $"{LogPrefix} 문워크 요구가 스타점프 요구보다 큽니다 — 유도가 뒤집혔습니다.");

            // 좁은 발판: 제자리조차 안 된다(양쪽 다 하한 미만).
            Assert.IsFalse(DanceEpisodeDirector.IsPossibleHere(DanceIds.Pirouette, inPlace * 0.5f, inPlace * 0.5f),
                $"{LogPrefix} 좌우 여유가 하한의 절반인데 제자리 동작이 가능하다고 판정됐습니다.");

            // 딱 하한: 제자리 5종은 전부 가능하고, 이동 2종은 불가능하다.
            foreach (string id in DanceIds.CreateAll())
            {
                bool ok = DanceEpisodeDirector.IsPossibleHere(id, inPlace, inPlace);
                bool isTravelling = id == DanceIds.Moonwalk || id == DanceIds.StarJump;
                Assert.AreEqual(!isTravelling, ok,
                    $"{LogPrefix} 좌우 여유가 정확히 {inPlace:F2} H일 때 {id}의 판정이 사양과 다릅니다.");
            }

            // 문워크 요구는 «한쪽»이면 된다(왕복이라 넓은 쪽으로 돈다).
            Assert.IsTrue(DanceEpisodeDirector.IsPossibleHere(DanceIds.Moonwalk, inPlace, moon),
                $"{LogPrefix} 한쪽이 문워크 요구를 채웠는데 불가능으로 판정됐습니다.");
            Assert.IsFalse(DanceEpisodeDirector.IsPossibleHere(DanceIds.StarJump, inPlace, moon),
                $"{LogPrefix} 문워크 요구만 채운 발판에서 스타점프가 가능하다고 판정됐습니다.");
            Assert.IsTrue(DanceEpisodeDirector.IsPossibleHere(DanceIds.StarJump, inPlace, star),
                $"{LogPrefix} 스타점프 요구를 채운 발판에서 스타점프가 불가능으로 판정됐습니다.");

            // ★ 음성 대조 — 좌우 하한은 이동 동작에도 **여전히** 걸린다(팔다리 뻗침은 방향과 무관하다).
            Assert.IsFalse(DanceEpisodeDirector.IsPossibleHere(DanceIds.StarJump, inPlace * 0.5f, star * 2f),
                $"{LogPrefix} 한쪽이 아무리 넓어도 반대쪽 여유가 하한 미만이면 안 됩니다 — 팔다리가 뻗칩니다.");
        }

        // ================================================================================
        // ④ 피로 램프 — 원칙 2(비침해)의 수치
        // ================================================================================

        [Test]
        public void 피로_램프가_창_안에서_자라고_상한에서_멈춘다()
        {
            StickConfig cfg = LoadDeployedConfig();
            float step = cfg.danceRestFatigueStep;
            float cap = cfg.danceRestFatigueCap;

            Assert.AreEqual(1f, DanceEpisodeDirector.ResolveFatigue(1, step, cap), 0.0001f,
                $"{LogPrefix} 창의 첫 에피소드에서 피로 계수가 1이 아닙니다 — 새 감상 세션이 " +
                "처음부터 늘어진 채 시작합니다.");

            float previous = 0f;
            bool reachedCap = false;
            for (int n = 1; n <= 200; n++)
            {
                float f = DanceEpisodeDirector.ResolveFatigue(n, step, cap);
                Assert.GreaterOrEqual(f, previous - 0.0001f,
                    $"{LogPrefix} 피로 계수가 n={n}에서 줄었습니다 — 창 안에서는 단조 증가여야 합니다.");
                Assert.LessOrEqual(f, cap + 0.0001f,
                    $"{LogPrefix} 피로 계수 {f:F2}가 상한 {cap:F2}를 넘었습니다 — 휴지가 무한히 길어집니다.");
                if (Mathf.Approximately(f, cap)) reachedCap = true;
                previous = f;
            }
            Assert.IsTrue(reachedCap,
                $"{LogPrefix} 200번째 에피소드까지도 상한 {cap:F2}에 도달하지 않았습니다 — " +
                "step이 0에 가까워 램프가 사실상 죽었다는 뜻입니다.");

            // ★ 네거티브 컨트롤 — step = 0이면 «피로 램프가 없는 예전 설계»로 정확히 되돌아간다.
            for (int n = 1; n <= 50; n++)
            {
                Assert.AreEqual(1f, DanceEpisodeDirector.ResolveFatigue(n, 0f, cap), 0.0001f,
                    $"{LogPrefix} step=0인데 n={n}에서 피로 계수가 1이 아닙니다 — 킬 스위치가 동작하지 않습니다.");
            }

            // 사양이 약속한 정상상태 듀티(약 11%). 12초는 평균 에피소드, (min+max)/2는 평균 휴지 기저값이다.
            float meanEpisode = (AudioReactiveDancePolicy.MotionEpisodeMinSeconds
                + AudioReactiveDancePolicy.MotionEpisodeMaxSeconds) * 0.5f;
            float meanRest = (cfg.danceRestMinSeconds + cfg.danceRestMaxSeconds) * 0.5f;
            float steady = meanEpisode / (meanEpisode + meanRest * cap);
            float initial = meanEpisode / (meanEpisode + meanRest);
            Assert.Less(steady, initial,
                $"{LogPrefix} 정상상태 듀티({steady:P1})가 첫 에피소드 듀티({initial:P1})보다 작지 않습니다 — " +
                "긴 감상에서 듀티가 내려간다는 설계가 성립하지 않습니다.");
            Debug.Log($"{LogPrefix} 듀티 — 첫 에피소드 {initial:P1} → 정상상태 {steady:P1}(상한 F={cap:F1}).");
        }

        [Test]
        public void 창이_닫히면_피로_카운터가_1로_되돌아간다()
        {
            var go = new GameObject("~DanceEpisodeDirectorTest");
            try
            {
                var director = go.AddComponent<DanceEpisodeDirector>();
                director.OpenDanceWindow();
                Assert.AreEqual(1, director.EpisodeIndex,
                    $"{LogPrefix} 창이 열린 직후 에피소드 번호가 1이 아닙니다.");
                Assert.IsTrue(director.IsDanceWindowOpen);

                director.OnEpisodeFinished();
                director.OnEpisodeFinished();
                Assert.AreEqual(3, director.EpisodeIndex,
                    $"{LogPrefix} 에피소드가 두 번 끝났는데 번호가 3이 아닙니다 — 피로 램프의 입력이 멎습니다.");
                Assert.Greater(director.RestRemainingSeconds, 0f,
                    $"{LogPrefix} 에피소드가 끝났는데 휴지가 0입니다 — 락을 쉬지 않고 다시 잡습니다.");

                director.CloseDanceWindow();
                Assert.AreEqual(1, director.EpisodeIndex,
                    $"{LogPrefix} 창이 닫혔는데 피로 카운터가 1로 되돌아가지 않았습니다 — " +
                    "새 감상 세션이 늘어진 채 시작합니다(그리고 진입 대사도 다시 뜨지 않습니다).");
                Assert.IsFalse(director.IsDanceWindowOpen);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================================
        // ⑤ 신원 — 문자열 아이디가 정본이다
        // ================================================================================

        [Test]
        public void 춤_아이디_해석은_문자열_표의_순서를_그대로_따른다()
        {
            string[] all = DanceIds.CreateAll();
            for (int i = 0; i < all.Length; i++)
            {
                Assert.AreEqual(i, DanceState.ResolveMoveIndex(all[i]),
                    $"{LogPrefix} {all[i]}의 인덱스가 표 순서와 다릅니다 — 장착 집합과 재생이 갈라집니다.");
            }

            // 모르는 아이디(지워진 팩의 잔재, 손상된 파일)는 제자리 무료 동작으로 떨어진다.
            Assert.AreEqual(StickmanPoseAnimator.DanceMovePirouette, DanceState.ResolveMoveIndex("dance.does_not_exist"),
                $"{LogPrefix} 모르는 아이디가 제자리 무료 동작으로 떨어지지 않습니다.");
            Assert.AreEqual(StickmanPoseAnimator.DanceMovePirouette, DanceState.ResolveMoveIndex(null),
                $"{LogPrefix} null 아이디가 제자리 무료 동작으로 떨어지지 않습니다.");

            // ★ 음성 대조 — 「전부 0으로 떨어뜨리면 통과」하는 오답을 막는다.
            Assert.AreNotEqual(0, DanceState.ResolveMoveIndex(DanceIds.HorseDance),
                $"{LogPrefix} 해석기가 모든 입력을 0으로 떨어뜨리고 있습니다(위 단언들이 무의미해집니다).");
        }

        // ================================================================================
        // ⑥ 폴백 리터럴 — 정규식이 아니라 「두 설정을 나란히 세워」 다시 잰다
        // ================================================================================

        [Test]
        public void 춤_포즈_폴백_리터럴이_설정_기본값과_한_톨도_다르지_않다()
        {
            // 왼쪽: Config가 없는 경로(= 소스에 적힌 폴백 리터럴).
            // 오른쪽: 코드 기본값만 든 새 설정 인스턴스(= StickConfig의 필드 초기화식).
            // 두 값이 같아야 «테스트가 프로덕션과 다른 자세를 검증하는» 상태가 원리적으로 없다.
            var withoutConfig = new StickmanBlackboard();
            var defaults = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                var withDefaults = new StickmanBlackboard { Config = defaults };
                StickmanPoseAnimator.DancePoseSettings a = withoutConfig.BuildDancePoseSettings();
                StickmanPoseAnimator.DancePoseSettings b = withDefaults.BuildDancePoseSettings();

                var mismatches = new List<string>();
                int compared = CompareRecursive("dance", a, b, mismatches);

                Assert.IsEmpty(mismatches,
                    $"{LogPrefix} 폴백 리터럴이 설정 기본값과 어긋났습니다:\n  " + string.Join("\n  ", mismatches) +
                    "\n이 사본은 정상 실행에서 절대 쓰이지 않으므로 어긋나도 화면이 안 바뀌고, 그래서 " +
                    "조용히 낡습니다. 다만 **설정 없이 도는 테스트 경로에서는 실제로 쓰입니다**.");

                // ★ 네거티브 컨트롤 — 비교기가 필드를 하나도 안 읽고 통과했으면 위 단언은 껍데기다.
                Assert.Greater(compared, 50,
                    $"{LogPrefix} 비교한 필드가 {compared}개뿐입니다 — 재귀가 중첩 구조체를 못 들어갔다는 뜻이고, " +
                    "그러면 이 검사는 언제나 초록인 껍데기입니다(거짓 통과).");
                Debug.Log($"{LogPrefix} 춤 포즈 폴백 {compared}개 필드를 대조했고 전부 일치합니다.");

                Assert.AreNotEqual(0f, defaults.dancePoseSmoothingRate,
                    $"{LogPrefix} 설정 기본 계수가 0입니다 — 로봇 전용 «즉시 대입»이 전 동작에 퍼집니다.");
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }

        /// <summary>중첩 readonly struct를 끝까지 내려가며 float 필드를 대조한다. 반환값은 비교한 필드 수.</summary>
        private static int CompareRecursive(string path, object left, object right, List<string> mismatches)
        {
            int count = 0;
            foreach (FieldInfo f in left.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object lv = f.GetValue(left);
                object rv = f.GetValue(right);
                if (f.FieldType == typeof(float))
                {
                    count++;
                    if (!Mathf.Approximately((float)lv, (float)rv))
                        mismatches.Add($"{path}.{f.Name}: 폴백 {(float)lv} vs 기본값 {(float)rv}");
                }
                else if (f.FieldType.IsValueType && !f.FieldType.IsPrimitive)
                {
                    count += CompareRecursive($"{path}.{f.Name}", lv, rv, mismatches);
                }
            }
            return count;
        }

        // ================================================================================
        // ⑦ 상태 소유권 — 세 목록의 멤버십(HorizontalMotionOwnershipContractTests와 짝)
        // ================================================================================

        [Test]
        public void 춤은_접지를_스스로_관리하지_않는다()
        {
            // ★ "공중 구간이 있으니 넣어야 할 것 같다"는 직관이 **정확히 틀린 방향**이다.
            //   넣으면 ApplyGroundedGravitySuppression 제외 목록에도 들어가 Dock 위에서 자유낙하하고,
            //   낙차 1.64유닛만으로 v = 9.8 > ragdollForceThreshold(8)이라 랙돌로 강제 전이된다.
            Assert.IsFalse(StickmanBlackboard.IsGroundKeepingSelfManaged(StickmanStateId.Dance),
                $"{LogPrefix} Dance가 접지 자기관리 목록에 들어갔습니다 — 스타점프의 도약은 물리가 아니라 " +
                "SetBodyOffset(시각 전용)입니다. 넣는 순간 Dock 위에서 랙돌이 됩니다.");

            // 짝이 되는 두 목록은 반대로 반드시 들어 있어야 한다(빠지면 동작이 소멸한다).
            Assert.IsTrue(StickmanBlackboard.IsHorizontalMotionSelfManaged(StickmanStateId.Dance),
                $"{LogPrefix} Dance가 수평 자기소유 목록에서 빠졌습니다 — 안전망이 도움닫기 속도를 " +
                "매 프레임 지워 스타점프가 영원히 도약하지 못합니다.");
            Assert.IsTrue(StickmanBlackboard.IsFacingSelfManaged(StickmanStateId.Dance),
                $"{LogPrefix} Dance가 방향 자기소유 목록에서 빠졌습니다 — 피루엣의 회전이 통째로 사라지고 " +
                "문워크가 그냥 뒷걸음질이 됩니다.");
        }

        [Test]
        public void 춤_상태와_스펙터클_종류가_사람이_읽는_이름을_갖는다()
        {
            // 이름을 잊으면 화면에 "지금 딴 일 중이라 못 해요"라는 정보 없는 문장이 뜬다.
            Assert.AreNotEqual("딴 일", StickMateDisplayNames.Of(StickmanStateId.Dance),
                $"{LogPrefix} Dance 상태의 표시 이름이 없습니다.");
            Assert.AreNotEqual("다른 일", StickMateDisplayNames.Of(SpectacleEventKind.Dance),
                $"{LogPrefix} Dance 스펙터클의 표시 이름이 없습니다.");
        }
    }
}
