using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 유휴 혼잣말의 <b>자격 술어</b> 회귀 — 2026-09-06.
    ///
    /// ============================================================================
    /// 무엇이 결함이었나 (design-narrative 대사 커버리지 감사)
    /// ============================================================================
    /// <c>AmbientChatter.TryRollChatter</c>가 표 전체에서 <b>균등 추첨</b>했고 자격 술어가 하나도
    /// 없었다(<c>target.LineIndex = Random.Range(0, table.Length);</c> 한 줄). Idle 8줄 중 3줄이
    /// 상태에서 파생되지 않는데도 아무 조건 없이 뽑혔다 — <b>Idle 발화의 37.5%</b>다.
    /// <list type="number">
    ///   <item><c>"심심하다"</c> — 이 저장소에 지루함 모델이 없고 유저에 대한 주장이라 <b>삭제</b>.</item>
    ///   <item><c>"하암..."</c> — 하품 모션과 무관하게 뽑혔다. <b>모션 재생 중일 때만</b>으로 조건화.</item>
    ///   <item><c>"구경 중이야"</c> — 두리번 모션과 무관하게 뽑혔다. <b>모션 재생 중 + 훑는 그림이
    ///     실제로 그려질 때만</b>으로 조건화.</item>
    /// </list>
    /// 판정 근거: design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md §3-2·§3-3.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 성질과, 각 성질의 네거티브 컨트롤
    /// ============================================================================
    /// 자격 필터는 <b>조용히 초록이 되기 가장 쉬운 종류</b>의 코드다 — "언제나 false"라는 오답은
    /// "그 줄은 안 나온다"는 단언을 전부 통과시키면서 캐릭터를 벙어리로 만든다. 그래서 모든
    /// 자격 단언에 <b>반대쪽 짝</b>을 붙였다: 조건이 갖춰지면 <b>반드시</b> 후보가 되어야 한다.
    ///
    /// <para>대사 문자열을 니들로 쓰는 자리는 <b>전부 존재 단언</b>이다(CLAUDE.md의 부재 단언 경고).
    /// 삭제된 <c>"심심하다"</c>만이 부재 단언인데, 같은 검사 안에서 <b>지금 실재하는 줄</b>로
    /// 같은 판정기를 먼저 통과시켜 "판정기가 죽어서 못 찾은 것"과 갈라 둔다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(대사 파이프라인·배회 AI에 플랫폼 분기가 없다).</para>
    /// </summary>
    public sealed class AmbientChatterEligibilityTests
    {
        private const string LogPrefix = "[자격술어-TEST]";
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>
        /// ★ 2026-09-06 요일·시간대 축이 붙으면서 필요해진 <b>시각 고정</b>.
        ///
        /// <para>고정하지 않으면 이 파일의 모든 추첨이 <b>러너를 돌린 시각</b>에 따라 후보 집합이
        /// 달라진다 — 월요일 새벽에 돌리면 초록, 화요일 낮에 돌리면 빨강 같은 실패가 나고, 그 실패는
        /// 「불안정한 테스트」로 오진되어 아무도 안 고친다(이 저장소가 시간 기반 검증에서 이미 겪은
        /// 형태다). 그래서 <b>요일 자격이 없는 날</b>을 골라 못 박는다 — 이 파일의 관심사는 모션 축이고,
        /// 요일 축이 끼어들면 재는 대상이 흐려진다.</para>
        ///
        /// <para>「화요일」이라고 <b>주석으로 주장하지 않는다</b> — <see cref="SetUp"/>이
        /// <see cref="AmbientCalendarPolicy.DayBucketOf"/>로 실제로 확인한다(날짜를 손으로 계산해
        /// 적으면 그게 다음 거짓말이다).</para>
        /// </summary>
        private static readonly System.DateTime PinnedWallClock = new System.DateTime(2026, 9, 8, 10, 0, 0);

        /// <summary>고정 시각의 달력 스냅샷. <see cref="AmbientChatter.IsLineEligible"/>에 직접 넘긴다.</summary>
        private static AmbientCalendarSnapshot PinnedCalendar => AmbientCalendarPolicy.Classify(PinnedWallClock);

        /// <summary>배회 AI와 같은 두 인터페이스를 구현하는 스텁(PlannedDwellStateScopeTests와 동일 관례).</summary>
        private sealed class StubIntent : IMovementIntentSource, IPlannedDwellSource
        {
            public float MoveInputX { get; set; }
            public float PlannedDwellRemainingSeconds { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>대사를 만들지 않는 Idle 자리표시자 — 블랙보드가 "지금 Idle이다"를 답하게 하는 것이
        /// 유일한 목적이다(<see cref="StickmanBlackboard.BeginIdleAmbientMotion"/>의 전제).</summary>
        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private StickConfig _config;
        private StickmanBlackboard _blackboard;
        private StubIntent _intent;
        private Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting();
            _randomState = Random.state;
            Random.InitState(20260906);

            // ★ 공유 달력을 고정 시각으로 못 박는다(TryRollChatter가 이것을 읽는다).
            //   ResetForTesting을 먼저 부르는 것이 순서상 필수다 — 안 부르면 앞 테스트가 뜬 스냅샷이
            //   60초 게이트에 걸려 그대로 남고, 여기서 시각을 갈아 끼워도 반영되지 않는다.
            AmbientCalendarClock.Shared.ResetForTesting();
            AmbientCalendarClock.Shared.WallClockOverrideForTesting = () => PinnedWallClock;
            Assert.AreEqual(AmbientDayBucket.None, PinnedCalendar.Day,
                $"{LogPrefix} 고정 시각이 요일 자격을 갖는 날입니다 — 이 파일은 모션 축을 재는 곳이라 " +
                "요일 후보가 섞이면 아래 기대값이 전부 흔들립니다. 요일 자격이 없는 날로 바꾸세요.");
            Assert.AreNotEqual(AmbientTimeBucket.None, PinnedCalendar.Time,
                $"{LogPrefix} 고정 시각이 어떤 시간대에도 속하지 않습니다 — 5구간이 24시간을 덮으므로 " +
                "정상 경로에서는 불가능한 값이고, 판정기가 죽었다는 뜻입니다.");

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.dialogueBubbleEnabled = true;
            // 1.0이 아니라 그 위를 쓴다 — UnityEngine.Random.value는 1.0을 **포함**하므로 확률 1.0에서도
            // 2^-24 확률로 침묵이 섞이고, 이 파일은 만 단위로 추첨을 돌린다. 확률 게이트는 여기서
            // 재는 대상이 아니므로 아예 닫아 둔다(AppSettingsModel.ScaleChance는 상한을 두지 않는다).
            _config.idleChatterChance = 2f;
            _config.walkChatterChance = 2f;
            _config.ambientChatterCooldownSeconds = 0f;

            // 계획 잔여는 배회 AI가 실제로 낼 수 있는 가장 긴 Idle 구간을 쓴다 — 숫자를 지어내면
            // 발화 자격 게이트(규칙 8)가 이 파일의 관심사와 무관하게 끼어든다.
            _intent = new StubIntent { PlannedDwellRemainingSeconds = _config.wanderIdleDurationMax };
            _blackboard = new StickmanBlackboard { Config = _config, IntentSource = _intent };

            var machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new SilentState(StickmanStateId.Idle) },
            });
            _blackboard.Machine = machine;
            machine.Start(StickmanStateId.Idle);
        }

        [TearDown]
        public void TearDown()
        {
            Random.state = _randomState;
            AmbientCalendarClock.Shared.ResetForTesting(); // 고정 시각이 다음 파일로 새면 그 실패는 무작위로 보인다.
            _blackboard = null;
            _intent = null;
            if (_config != null) Object.DestroyImmediate(_config);
            _config = null;
            AppSettingsModel.ResetForTesting();
        }

        // ==================================================================================
        // 표와 자격 축
        // ==================================================================================

        private static string[] IdleLines() => DialogueCorpus.AmbientLines("IdleLines");
        private static string[] WalkLines() => DialogueCorpus.AmbientLines("WalkLines");

        /// <summary><c>AmbientChatter.IdleLineMotionRequirement</c>를 리플렉션으로 읽는다.
        /// 문자열 니들이 아니라 <b>구조</b>로 찾는다 — 대사 문구가 바뀌어도 이 검사는 계속 유효하다.</summary>
        private static WanderAmbientMotion?[] IdleRequirements()
        {
            FieldInfo field = typeof(AmbientChatter).GetField("IdleLineMotionRequirement",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, $"{LogPrefix} AmbientChatter.IdleLineMotionRequirement를 찾지 못했습니다 — " +
                "자격 축의 이름/형태가 바뀌었다면 이 테스트도 함께 고쳐야 합니다. " +
                "고치지 않으면 자격 술어 전체가 어떤 검사에도 닿지 않습니다(조용한 초록).");
            var value = field.GetValue(null) as WanderAmbientMotion?[];
            Assert.IsNotNull(value, $"{LogPrefix} 자격 축의 원소 타입이 바뀌었습니다.");
            return value;
        }

        /// <summary>그 모션을 요구하는 줄의 인덱스(정확히 1개여야 한다).</summary>
        private static int IndexRequiring(WanderAmbientMotion motion)
        {
            WanderAmbientMotion?[] req = IdleRequirements();
            int found = -1;
            for (int i = 0; i < req.Length; i++)
            {
                if (!req[i].HasValue || req[i].Value != motion) continue;
                Assert.AreEqual(-1, found,
                    $"{LogPrefix} {motion}을 요구하는 줄이 둘 이상입니다 — 이 파일의 검사가 " +
                    "그중 하나만 보게 되어 나머지가 조용히 방치됩니다.");
                found = i;
            }
            Assert.Greater(found, -1,
                $"{LogPrefix} {motion}을 요구하는 줄이 하나도 없습니다 — 자격 술어가 통째로 " +
                "사라졌다는 뜻이고(원칙 1 위반의 재발), 아래 모든 단언이 0건을 훑고 통과합니다.");
            return found;
        }

        /// <summary>★ 2026-09-06 — 요일·시간대 축이 붙으면서 <b>「상시」의 뜻이 좁아졌다</b>.
        /// 여기서 「상시」는 <b>세 축 어디에도 안 걸린 줄</b>이다. 시간대 축은 24시간을 덮으므로
        /// 어느 시각에나 정확히 한 줄이 더 붙지만, 그건 상시가 아니라 <b>지금 자격이 있는</b> 줄이다.
        /// 두 개념을 갈라 두지 않으면 아래 균등 검사의 기대값이 조용히 틀린다.</summary>
        private static IEnumerable<int> AlwaysEligibleIndices()
        {
            WanderAmbientMotion?[] motion = IdleRequirements();
            AmbientDayBucket[] day = DayAxis("IdleLineDayRequirement");
            AmbientTimeBucket[] time = TimeAxis("IdleLineTimeRequirement");
            for (int i = 0; i < motion.Length; i++)
            {
                if (!motion[i].HasValue && day[i] == AmbientDayBucket.None && time[i] == AmbientTimeBucket.None)
                    yield return i;
            }
        }

        /// <summary>지금 달력에서 자격이 있는 Idle 줄(모션 없음 전제). 기대값을
        /// <see cref="AmbientChatter.IsLineEligible"/>로 만들지 <b>않는다</b> — 프로덕션 함수로 기대값을
        /// 만들면 그 함수가 틀어질 때 기대값도 함께 틀어져 아무것도 못 잰다(TEAM.md §생성기와 검사기).
        /// 대신 <b>자격 축 데이터</b>에서 독립적으로 재구성한다.</summary>
        private static IEnumerable<int> EligibleIndicesWithoutMotion(AmbientCalendarSnapshot calendar)
        {
            WanderAmbientMotion?[] motion = IdleRequirements();
            AmbientDayBucket[] day = DayAxis("IdleLineDayRequirement");
            AmbientTimeBucket[] time = TimeAxis("IdleLineTimeRequirement");
            for (int i = 0; i < motion.Length; i++)
            {
                if (motion[i].HasValue) continue;
                if (day[i] != AmbientDayBucket.None && day[i] != calendar.Day) continue;
                if (time[i] != AmbientTimeBucket.None && time[i] != calendar.Time) continue;
                yield return i;
            }
        }

        private static AmbientDayBucket[] DayAxis(string fieldName) => ReadAxis<AmbientDayBucket>(fieldName);
        private static AmbientTimeBucket[] TimeAxis(string fieldName) => ReadAxis<AmbientTimeBucket>(fieldName);

        private static T[] ReadAxis<T>(string fieldName)
        {
            FieldInfo field = typeof(AmbientChatter).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, $"{LogPrefix} AmbientChatter.{fieldName}을 찾지 못했습니다 — " +
                "자격 축의 이름/형태가 바뀌었다면 이 테스트도 함께 고쳐야 합니다. " +
                "고치지 않으면 그 축 전체가 어떤 검사에도 닿지 않습니다(조용한 초록).");
            var value = field.GetValue(null) as T[];
            Assert.IsNotNull(value, $"{LogPrefix} {fieldName}의 원소 타입이 바뀌었습니다.");
            return value;
        }

        [Test]
        public void 자격_축은_대사표와_길이가_같다()
        {
            string[] idle = IdleLines();
            string[] walk = WalkLines();
            WanderAmbientMotion?[] req = IdleRequirements();

            const string why = "표에 줄만 추가하고 자격 축을 잊으면 새 줄이 조용히 '상시 자격'을 얻고, " +
                "표에서 줄만 지우면 자격이 옆줄로 밀립니다(엉뚱한 대사가 엉뚱한 조건에 묶입니다).";

            Assert.AreEqual(idle.Length, req.Length,
                $"{LogPrefix} IdleLines {idle.Length}줄 / 모션 축 {req.Length}칸으로 어긋났습니다 — {why}");
            Assert.AreEqual(idle.Length, DayAxis("IdleLineDayRequirement").Length,
                $"{LogPrefix} IdleLines와 요일 축의 길이가 어긋났습니다 — {why}");
            Assert.AreEqual(idle.Length, TimeAxis("IdleLineTimeRequirement").Length,
                $"{LogPrefix} IdleLines와 시간대 축의 길이가 어긋났습니다 — {why}");
            Assert.AreEqual(walk.Length, DayAxis("WalkLineDayRequirement").Length,
                $"{LogPrefix} WalkLines와 요일 축의 길이가 어긋났습니다 — {why}");
            Assert.AreEqual(walk.Length, TimeAxis("WalkLineTimeRequirement").Length,
                $"{LogPrefix} WalkLines와 시간대 축의 길이가 어긋났습니다 — {why}");

            Debug.Log($"{LogPrefix} Idle 표 {idle.Length}줄 / Walk 표 {walk.Length}줄 — " +
                      $"세 축 어디에도 안 걸린 Idle 줄 {System.Linq.Enumerable.Count(AlwaysEligibleIndices())}개, " +
                      $"모션 조건부 {req.Length - CountAlways(req)}개.");
        }

        private static int CountAlways(WanderAmbientMotion?[] req)
        {
            int n = 0;
            foreach (WanderAmbientMotion? r in req)
            {
                if (!r.HasValue) n++;
            }
            return n;
        }

        // ==================================================================================
        // (a) "심심하다" — 삭제
        // ==================================================================================

        /// <summary>
        /// ★ 부재 단언이라 <b>대조를 같이 건다</b>. 판정기(<see cref="System.Array.IndexOf(string[], string)"/>)가
        /// 지금 실재하는 줄을 먼저 찾아내지 못하면, 그 뒤의 "없다"는 아무것도 증명하지 않는다
        /// (CLAUDE.md: 부재 단언은 썩어도 <b>조용히 초록</b>이 된다).
        /// </summary>
        [Test]
        public void 심심하다는_대사표와_말뭉치_어디에도_없다()
        {
            const string removed = "심심하다";
            string[] idle = IdleLines();

            // ── 대조(존재 단언) — 같은 판정기가 지금 살아 있는 줄은 실제로 찾아낸다.
            Assert.Greater(idle.Length, 0, $"{LogPrefix} Idle 표가 비었습니다.");
            string alive = idle[0];
            Assert.GreaterOrEqual(System.Array.IndexOf(idle, alive), 0,
                $"{LogPrefix} 판정기가 실재하는 줄 \"{alive}\"조차 못 찾습니다 — 아래 부재 단언은 무효입니다.");

            Assert.AreEqual(-1, System.Array.IndexOf(idle, removed),
                $"{LogPrefix} \"{removed}\"가 Idle 대사표에 되살아났습니다. 이 저장소에 지루함 모델이 " +
                "없으므로(Idle = 접지 + 정지라는 물리적 사실뿐) 상태가 알 수 없는 것을 주장하고, " +
                "게다가 \"네가 안 놀아준다\"는 유저에 대한 주장입니다 — 절대 불변 원칙 1 위반입니다. " +
                "판정 근거: design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md §3-2.");

            CollectionAssert.DoesNotContain(DialogueCorpus.ScanDistinct(), removed,
                $"{LogPrefix} \"{removed}\"가 앱의 다른 대사 경로에 남아 있습니다.");
        }

        /// <summary>★ 추첨을 반복해도 절대 안 나온다 — 위 검사는 <b>표</b>를 보고, 이것은 <b>추첨 결과</b>를 본다.</summary>
        [Test]
        public void 반복_추첨에서도_삭제된_줄은_한_번도_나오지_않는다()
        {
            const string removed = "심심하다";
            string[] idle = IdleLines();
            var target = new AmbientChatter.ChatterParams();

            for (int i = 0; i < 5000; i++)
            {
                _blackboard.NextChatterAllowedUnscaledTime = 0f;
                if (!AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target)) continue;
                Assert.AreNotEqual(removed, idle[target.LineIndex],
                    $"{LogPrefix} {i}번째 추첨에서 삭제된 줄이 나왔습니다.");
            }
        }

        // ==================================================================================
        // (b) "하암..." — 하품 모션이 재생 중일 때만
        // ==================================================================================

        [Test]
        public void 하품_대사는_하품_모션이_재생_중일_때만_후보다()
        {
            int yawn = IndexRequiring(WanderAmbientMotion.SitAndYawn);
            string text = IdleLines()[yawn];

            // ① 모션 없음 → 자격 없음.
            Assert.IsFalse(_blackboard.IsIdleAmbientMotionActive, $"{LogPrefix} 사전 조건이 깨졌습니다.");
            Assert.IsFalse(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, yawn, PinnedCalendar),
                $"{LogPrefix} 하품 모션이 재생 중이 아닌데 \"{text}\"가 후보입니다 — " +
                "하품하지 않으면서 하품 대사를 하는 것이 이번에 고친 그 결함입니다(원칙 1).");

            // ② 다른 모션(두리번) → 여전히 자격 없음. "모션이면 아무거나"라는 오답을 배제한다.
            Assert.IsTrue(_blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.LookAround),
                $"{LogPrefix} 두리번 모션을 시작하지 못했습니다(리그 문제).");
            Assert.IsFalse(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, yawn, PinnedCalendar),
                $"{LogPrefix} 두리번 중인데 \"{text}\"가 후보입니다 — 자격이 모션 종류를 안 보고 있습니다.");

            // ③ ★ 네거티브 컨트롤 — 하품이 실제로 재생되면 반드시 후보가 되어야 한다.
            //    이게 없으면 "언제나 false"라는 오답이 위 두 단언을 그대로 통과한다.
            Assert.IsTrue(_blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.SitAndYawn),
                $"{LogPrefix} 하품 모션을 시작하지 못했습니다(리그 문제).");
            Assert.IsTrue(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, yawn, PinnedCalendar),
                $"{LogPrefix} 하품 모션이 재생 중인데도 \"{text}\"가 후보가 아닙니다 — " +
                "그러면 이 줄은 영원히 침묵하는 죽은 데이터가 되고, 위 두 단언은 아무것도 검사하지 않습니다.");

            // ④ 모션이 끝나면 자격도 같이 끝난다(연출과 대사가 같은 시각에 붙어 있어야 한다).
            _blackboard.CancelIdleAmbientMotion();
            Assert.IsFalse(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, yawn, PinnedCalendar),
                $"{LogPrefix} 모션이 끝났는데 \"{text}\"가 아직 후보입니다.");
        }

        [Test]
        public void 하품_중에는_추첨_결과에_하품_대사가_실제로_섞인다()
        {
            int yawn = IndexRequiring(WanderAmbientMotion.SitAndYawn);
            Assert.IsTrue(_blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.SitAndYawn));

            var target = new AmbientChatter.ChatterParams();
            int hits = 0, rolls = 0;
            for (int i = 0; i < 5000; i++)
            {
                _blackboard.NextChatterAllowedUnscaledTime = 0f;
                if (!AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target)) continue;
                rolls++;
                if (target.LineIndex == yawn) hits++;
            }

            Assert.Greater(hits, 0,
                $"{LogPrefix} 하품 재생 중 {rolls}회 추첨에서 하품 대사가 한 번도 안 나왔습니다 — " +
                "자격은 통과하는데 추첨이 그 줄을 구조적으로 건너뛰고 있다는 뜻입니다.");
            Debug.Log($"{LogPrefix} 하품 재생 중 {rolls}회 추첨 — 하품 대사 {hits}회 " +
                      $"({(float)hits / rolls * 100f:F1}%).");
        }

        // ==================================================================================
        // (c) "구경 중이야" — 두리번 모션 + 훑는 그림이 실제로 그려질 때만
        // ==================================================================================

        [Test]
        public void 두리번_대사는_두리번_모션이_재생_중일_때만_후보다()
        {
            int look = IndexRequiring(WanderAmbientMotion.LookAround);
            string text = IdleLines()[look];

            Assert.IsFalse(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, look, PinnedCalendar),
                $"{LogPrefix} 아무 모션도 없는데 \"{text}\"가 후보입니다 — 두리번거리지 않으면서 " +
                "두리번 대사를 하는 것이 이번에 고친 그 결함입니다(원칙 1).");

            Assert.IsTrue(_blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.SitAndYawn));
            Assert.IsFalse(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, look, PinnedCalendar),
                $"{LogPrefix} 하품 중인데 \"{text}\"가 후보입니다 — 자격이 모션 종류를 안 보고 있습니다.");

            // ★ 네거티브 컨트롤 — 두리번이 실제로 재생되면 반드시 후보가 되어야 한다.
            Assert.IsTrue(_blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.LookAround));
            Assert.IsTrue(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, look, PinnedCalendar),
                $"{LogPrefix} 두리번 모션이 재생 중인데도 \"{text}\"가 후보가 아닙니다 — " +
                $"기본 설정(상체 좌우 왕복 {_blackboard.LookAroundBodyLeanDegrees:F2}도)에서 " +
                "이 줄이 죽었다는 뜻입니다.");
        }

        /// <summary>
        /// ★★ 리더 지시로 함께 확인한 항목 — <b>훑는 그림을 그리는 채널이 전부 0이면 침묵한다.</b>
        ///
        /// <para>채널은 둘이다: 상체 좌우 왕복(<c>bodyLeanLookAroundDegrees</c>)과 머리 좌우 이동
        /// (<c>idleAmbientLookHeadShiftRatio</c>). 배포 자산은 머리 쪽이 <b>0</b>이고
        /// (2026-08-31 "머리가 목에서 벗어난다" 신고로 꺼졌다) 상체 쪽이 살아 있어서,
        /// <b>지금 배포 설정에서 이 대사는 살아 있다</b>. 아래 검사는 그 둘을 <b>따로</b> 껐다 켜서
        /// 각 채널이 실제로 판정에 참여하는지 확인한다.</para>
        /// </summary>
        [Test]
        public void 훑는_그림이_한_채널도_없으면_두리번_대사는_후보에서_빠진다()
        {
            int look = IndexRequiring(WanderAmbientMotion.LookAround);
            Assert.IsTrue(_blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.LookAround));

            // ① 배포 기본값 — 상체 왕복이 살아 있으므로 후보다.
            Assert.Greater(Mathf.Abs(_blackboard.LookAroundBodyLeanDegrees), 0f,
                $"{LogPrefix} 사전 조건: 상체 좌우 왕복이 기본값에서 0입니다.");
            Assert.IsTrue(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, look, PinnedCalendar));

            // ② 두 채널을 모두 끄면 침묵한다.
            _config.bodyLeanEnabled = false;
            _config.idleAmbientLookHeadShiftRatio = 0f;
            Assert.AreEqual(0f, _blackboard.LookAroundBodyLeanDegrees, 1e-6f);
            Assert.IsFalse(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, look, PinnedCalendar),
                $"{LogPrefix} 좌우로 훑는 그림이 한 장도 안 그려지는데 \"구경 중이야\"가 후보입니다 — " +
                "연출은 안 보이고 대사만 뜨는 상태입니다.");

            // ③ ★ 채널별 네거티브 컨트롤 — 각각 하나씩만 되살려도 후보가 돌아온다.
            //    (하나만 확인하면 다른 채널이 판정에서 통째로 빠져 있어도 초록이 된다.)
            _config.idleAmbientLookHeadShiftRatio = StickConfig.MaxSafeHeadShiftRatio;
            Assert.IsTrue(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, look, PinnedCalendar),
                $"{LogPrefix} 머리 좌우 이동 채널이 판정에 참여하지 않습니다.");

            _config.idleAmbientLookHeadShiftRatio = 0f;
            _config.bodyLeanEnabled = true;
            Assert.IsTrue(AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Idle, look, PinnedCalendar),
                $"{LogPrefix} 상체 좌우 왕복 채널이 판정에 참여하지 않습니다.");
        }

        /// <summary>
        /// ★ 배포 자산의 실측 기록 — <c>idleAmbientLookHeadShiftRatio</c>가 정말 0인가.
        /// 자산은 <b>읽기만</b> 한다(절대 불변 원칙 3).
        /// </summary>
        [Test]
        public void 기록_배포_자산의_머리_좌우_이동은_0이고_상체_왕복이_그_자리를_대신한다()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(shipped, $"{LogPrefix} 배포 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");

            Debug.Log($"{LogPrefix} 배포 자산 — idleAmbientLookHeadShiftRatio=" +
                      $"{shipped.idleAmbientLookHeadShiftRatio:F5}(2026-08-31 목 어긋남 신고로 0) / " +
                      $"bodyLeanEnabled={shipped.bodyLeanEnabled} " +
                      $"bodyLeanLookAroundDegrees={shipped.bodyLeanLookAroundDegrees:F2}도 / " +
                      $"idleAmbientLookEyeSweep01={shipped.idleAmbientLookEyeSweep01:F2}" +
                      "(눈은 SceneBootstrapper.BakeEyes=false로 구워지지 않아 화면 신호가 아니다).");

            Assert.AreEqual(0f, shipped.idleAmbientLookHeadShiftRatio, 1e-6f,
                $"{LogPrefix} 머리 좌우 이동이 0이 아닙니다 — 이 기록이 낡았습니다. " +
                "되살아났다면 IdleAmbientLookAroundInvariantTests의 안전 상한을 먼저 확인하세요.");

            // 배포 설정에서 이 대사가 죽지 않았다는 사실을 같은 자리에서 못 박는다.
            Assert.IsTrue(shipped.bodyLeanEnabled && Mathf.Abs(shipped.bodyLeanLookAroundDegrees) > 0f,
                $"{LogPrefix} 훑는 그림을 그리는 채널이 배포 설정에서 하나도 남지 않았습니다 — " +
                "그러면 \"구경 중이야\"는 배포본에서 영원히 침묵하는 죽은 데이터가 됩니다.");
        }

        // ==================================================================================
        // 나머지 줄의 노출 비율 — 필터가 <b>침묵</b>을 만들지 않았는가
        // ==================================================================================

        /// <summary>
        /// ★★ 회귀의 핵심 — 자격 필터는 <b>후보를 고르는 방법</b>만 바꿔야 하고 <b>말하는 횟수</b>는
        /// 건드리면 안 된다. 조건부 줄이 뽑힐 자리에서 침묵해 버리면(= 필터를 추첨 <i>뒤에</i> 걸면)
        /// Idle 발화량이 통째로 줄어든다.
        ///
        /// <para>그리고 상시 줄의 상대 빈도는 <b>올라가는 것이 정상</b>이다 — 표 전체 균등에서
        /// 자격 있는 줄만의 균등으로 바뀐다. 기대값을 숫자로 적지 않고 <b>자격 축에서 세어</b>
        /// 쓴다.</para>
        ///
        /// <para>★ 2026-09-06 요일·시간대 축이 붙으면서 「자격 있는 줄」이 <b>고정 시각에 의존</b>하게
        /// 됐다. 고정 시각은 요일 자격이 없는 날이지만 시간대 5구간은 24시간을 덮으므로,
        /// 자격 집합은 «축이 하나도 안 붙은 줄» + «지금 구간의 시간대 줄 1개»다. 그래서 아래 기대값을
        /// <see cref="EligibleIndicesWithoutMotion"/>로 계산한다 — 숫자를 적으면 대사가 늘 때 조용히
        /// 틀린다.</para>
        /// </summary>
        [Test]
        public void 필터는_발화_횟수를_줄이지_않고_남은_줄이_균등하게_나온다()
        {
            const int trials = 20000;
            string[] idle = IdleLines();
            var counts = new int[idle.Length];
            var target = new AmbientChatter.ChatterParams();

            int spoken = 0;
            for (int i = 0; i < trials; i++)
            {
                _blackboard.NextChatterAllowedUnscaledTime = 0f;
                if (!AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target)) continue;
                spoken++;
                counts[target.LineIndex]++;
            }

            Assert.AreEqual(trials, spoken,
                $"{LogPrefix} {trials}회 중 {spoken}회만 발화했습니다 — 확률 1.0 / 쿨다운 0 / 계획 잔여 " +
                "충분인데도 침묵이 생겼다는 뜻이고, 자격 필터가 발화량을 깎고 있습니다(추첨 뒤에 " +
                "거르면 이렇게 됩니다).");

            var eligible = new List<int>(EligibleIndicesWithoutMotion(PinnedCalendar));
            float expected = (float)spoken / eligible.Count;
            // 이항분포 표준편차에서 유도한 허용폭(숫자를 지어내지 않는다). 4시그마면 우연한 빨강은
            // 사실상 없고, 분포가 실제로 어긋나면(예: 한 줄이 두 배로 나옴) 확실히 잡힌다.
            float p = 1f / eligible.Count;
            float tolerance = 4f * Mathf.Sqrt(spoken * p * (1f - p));

            foreach (int index in eligible)
            {
                Assert.AreEqual(expected, counts[index], tolerance,
                    $"{LogPrefix} 자격 있는 줄 \"{idle[index]}\"가 {counts[index]}회로 기대 {expected:F0}회 " +
                    $"±{tolerance:F0}을 벗어났습니다 — 자격 있는 줄 사이의 균등이 깨졌습니다.");
            }

            for (int i = 0; i < counts.Length; i++)
            {
                if (eligible.Contains(i)) continue;
                Assert.AreEqual(0, counts[i],
                    $"{LogPrefix} 지금 자격이 없는 줄 \"{idle[i]}\"가 {counts[i]}회 나왔습니다 — " +
                    "모션이 재생 중이 아니거나(모션 축), 오늘이 그 요일이 아니거나(요일 축), " +
                    "지금이 그 시간대가 아닙니다(시간대 축).");
            }

            Debug.Log($"{LogPrefix} {trials}회 Idle 진입 — 발화 {spoken}회(침묵 0). " +
                      $"달력 {PinnedCalendar} 기준 자격 있는 줄 {eligible.Count}개가 각 {expected:F0}회" +
                      $"(±{tolerance:F0}) 균등 = 줄당 {p * 100f:F1}%. 고치기 전에는 표 전체 균등이었다 — " +
                      "상시 줄의 상대 빈도가 올라간 것은 설계된 결과다(발화량이 준 것이 아니라 " +
                      "자격 없는 줄이 후보에서 빠진 것).");
        }

        /// <summary>
        /// Walk 표에는 <b>모션 축이 없다</b> — 앉기하품·두리번은 Idle 전용 연출이라 걷는 중에는
        /// 정의상 재생되지 않는다. 그 성질은 요일·시간대 축이 붙은 뒤에도 그대로다.
        ///
        /// <para>★ 2026-09-06 정정 — 이 검사의 원래 이름은 <c>Walk_표는_전부_상시_자격이라…</c>였고
        /// 「Walk 줄은 전부 언제나 후보다」를 단언했다. <b>그 성질은 이제 거짓이다</b>: 요일 3줄과
        /// 시간대 5줄이 Walk 표에도 들어왔다(R2 §3-4 #16~#18·#22~#24 + 2026-09-06 신규 2줄).
        /// 그래서 「전부 상시」가 아니라 <b>「모션 축은 여전히 Walk에 없다」</b>와
        /// <b>「지금 자격 있는 줄은 전부 실제로 뽑힌다」</b> 둘로 갈랐다.</para>
        /// </summary>
        [Test]
        public void Walk_표에는_모션_축이_없고_지금_자격_있는_줄은_전부_뽑힌다()
        {
            string[] walk = WalkLines();
            AmbientDayBucket[] day = DayAxis("WalkLineDayRequirement");
            AmbientTimeBucket[] time = TimeAxis("WalkLineTimeRequirement");

            // ① 모션 축은 Walk에 없다 — 하품 모션을 켜 두고도 Walk 자격이 한 줄도 안 달라진다.
            //    (블랙보드가 Idle이라 모션이 실제로 켜진다. 그래도 Walk 판정은 그것을 안 본다.)
            Assert.IsTrue(_blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.SitAndYawn),
                $"{LogPrefix} 모션을 시작하지 못했습니다(리그 문제) — 아래 대조가 무효입니다.");
            var expectedEligible = new List<int>();
            for (int i = 0; i < walk.Length; i++)
            {
                bool dayOk = day[i] == AmbientDayBucket.None || day[i] == PinnedCalendar.Day;
                bool timeOk = time[i] == AmbientTimeBucket.None || time[i] == PinnedCalendar.Time;
                Assert.AreEqual(dayOk && timeOk,
                    AmbientChatter.IsLineEligible(_blackboard, StickmanStateId.Walk, i, PinnedCalendar),
                    $"{LogPrefix} Walk 줄 \"{walk[i]}\"의 자격이 요일·시간대 축만으로 설명되지 않습니다 — " +
                    "Walk에 모션 축이 새어 들어왔거나 축 배열이 표와 어긋났습니다.");
                if (dayOk && timeOk) expectedEligible.Add(i);
            }
            _blackboard.CancelIdleAmbientMotion();

            // ② 지금 자격 있는 줄은 전부 실제로 뽑히고, 없는 줄은 한 번도 안 나온다.
            _intent.PlannedDwellRemainingSeconds = _config.wanderWalkDurationMax;
            var counts = new int[walk.Length];
            var target = new AmbientChatter.ChatterParams();
            int spoken = 0;
            for (int i = 0; i < 5000; i++)
            {
                _blackboard.NextChatterAllowedUnscaledTime = 0f;
                _intent.MoveInputX = _config.moveInputDeadzone + 1f; // Walk가 계획과 일치해야 게이트를 지난다.
                if (!AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Walk, target)) continue;
                spoken++;
                counts[target.LineIndex]++;
            }

            Assert.AreEqual(5000, spoken, $"{LogPrefix} Walk 발화에 침묵이 생겼습니다.");
            for (int i = 0; i < counts.Length; i++)
            {
                if (expectedEligible.Contains(i))
                {
                    Assert.Greater(counts[i], 0,
                        $"{LogPrefix} 자격 있는 Walk 줄 \"{walk[i]}\"가 5000회 중 한 번도 안 나왔습니다 — " +
                        "죽은 데이터입니다.");
                }
                else
                {
                    Assert.AreEqual(0, counts[i],
                        $"{LogPrefix} 자격 없는 Walk 줄 \"{walk[i]}\"가 {counts[i]}회 나왔습니다.");
                }
            }

            Debug.Log($"{LogPrefix} 달력 {PinnedCalendar} — Walk 표 {walk.Length}줄 중 자격 " +
                      $"{expectedEligible.Count}줄이 5000회 발화를 나눠 가졌다(자격 없는 줄 0회).");
        }

        // ==================================================================================
        // 강제 발화 펄스도 자격은 건너뛰지 않는다
        // ==================================================================================

        /// <summary>
        /// ★ "지금 말풍선을 보고 싶다"는 사용자 명령(Ctrl+Opt+Cmd+B / 행동 명령창 [말 걸기])은
        /// <b>확률과 쿨다운</b>을 건너뛴다. 그러나 <b>자격</b>은 건너뛰지 않는다 — 그쪽은
        /// "말할지 말지"가 아니라 "그 문장이 참인가"의 문제이고, 원칙 1에는 예외가 없다.
        /// </summary>
        [Test]
        public void 강제_발화도_자격_없는_줄은_뽑지_않는다()
        {
            WanderAmbientMotion?[] req = IdleRequirements();
            AmbientDayBucket[] day = DayAxis("IdleLineDayRequirement");
            AmbientTimeBucket[] time = TimeAxis("IdleLineTimeRequirement");
            string[] idle = IdleLines();
            var target = new AmbientChatter.ChatterParams();

            for (int i = 0; i < 3000; i++)
            {
                _blackboard.ForcedChatterSignaled = true;
                // 쿨다운을 일부러 미래로 밀어 둔다 — 강제 펄스가 실제로 그것을 건너뛰는지도 함께 본다.
                _blackboard.NextChatterAllowedUnscaledTime = Time.unscaledTime + 999f;
                Assert.IsTrue(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target),
                    $"{LogPrefix} 강제 발화 펄스가 쿨다운에 막혔습니다 — 사용자 명령이 무시됩니다.");
                int picked = target.LineIndex;
                Assert.IsFalse(req[picked].HasValue,
                    $"{LogPrefix} 강제 발화가 모션 조건부 줄 \"{idle[picked]}\"를 뽑았습니다 — " +
                    "그 모션은 지금 재생 중이 아닙니다(원칙 1에 사용자 명령 예외는 없습니다).");
                // ★ 2026-09-06 — 요일·시간대 축에도 사용자 명령 예외가 없다. 「지금 말풍선을 보고 싶다」는
                //   말할지 말지의 문제이지 <b>오늘이 무슨 요일인가</b>를 바꾸는 명령이 아니다.
                Assert.IsTrue(day[picked] == AmbientDayBucket.None || day[picked] == PinnedCalendar.Day,
                    $"{LogPrefix} 강제 발화가 오늘이 아닌 요일의 줄 \"{idle[picked]}\"를 뽑았습니다.");
                Assert.IsTrue(time[picked] == AmbientTimeBucket.None || time[picked] == PinnedCalendar.Time,
                    $"{LogPrefix} 강제 발화가 지금이 아닌 시간대의 줄 \"{idle[picked]}\"를 뽑았습니다.");
            }
        }
    }
}
