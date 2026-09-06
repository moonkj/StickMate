using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 유휴 혼잣말의 <b>요일·시간대 자격 축</b> 회귀 — 2026-09-06 배선 라운드.
    ///
    /// ============================================================================
    /// 무엇을 배선했나
    /// ============================================================================
    /// design-narrative가 확정해 두고 <b>프로덕션에 한 줄도 안 들어가 있던</b> 16줄이 들어왔다:
    /// 요일 6줄(R2 §3-4 #13~#18) + 시간대 10줄(#19~#24의 6줄 + 2026-09-06 신규 오후·저녁 4줄).
    /// 경계는 design-systems R23이 확정한 5구간(아침 05–11 / 점심 11–14 / 오후 14–18 /
    /// 저녁 18–22 / 밤 22–05, 정시·반열린)이다.
    ///
    /// ============================================================================
    /// ★ 이 파일이 특히 조심하는 실패 형태 — <b>「시계가 있어서 무작위로 빨개진다」</b>
    /// ============================================================================
    /// 시간 의존 검사는 <b>러너를 돌린 시각</b>에 따라 결과가 갈리기 쉽고, 그 실패는
    /// 「불안정한 테스트」로 오진되어 아무도 안 고친다. 그래서 이 파일은
    /// <b>벽시계를 한 번도 읽지 않는다</b> — 판정 함수(<see cref="AmbientCalendarPolicy"/>)는 순수이고,
    /// 추첨 경로는 <see cref="AmbientCalendarClock.WallClockOverrideForTesting"/>로 시각을 못 박는다.
    /// 「지금이 몇 시인가」가 이 파일의 어떤 단언에도 들어오지 않는다.
    ///
    /// <para>그리고 <b>경계는 기다려서 잴 수 없다</b>(자정까지 기다리는 테스트는 존재할 수 없다) —
    /// 주입 통로가 하나 필요한 이유가 그것이고, 그 통로가 <b>프로덕션에서 쓰이지 않는다</b>는 것을
    /// <see cref="프로덕션은_테스트용_시계_주입_통로를_쓰지_않는다"/>가 소스 스캔으로 잠근다.</para>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 — 「언제나 false」라는 오답을 배제한다
    /// ============================================================================
    /// 자격 필터의 가장 흔한 오답은 <b>「언제나 false」</b>다. 그것은 "그 줄은 안 나온다"는 단언을
    /// 전부 통과시키면서 16줄을 <b>영원히 침묵하는 죽은 데이터</b>로 만든다. 그래서 모든
    /// 「안 나온다」 옆에 <b>「그 조건에서는 반드시 나온다」</b>를 붙였다.
    ///
    /// <para><b>플랫폼</b>: 완전 중립. <see cref="AmbientCalendarPolicy"/>·
    /// <see cref="AmbientCalendarClock"/> 둘 다 <c>#if</c>가 0건이고
    /// <see cref="DateTime"/>/<c>double</c>만 다룬다 — macOS/Windows/iPad/iPhone 판정이 동일하다.</para>
    /// </summary>
    public sealed class AmbientChatterCalendarTests
    {
        private const string LogPrefix = "[달력자격-TEST]";

        private StickConfig _config;
        private StickmanBlackboard _blackboard;
        private StubIntent _intent;
        private UnityEngine.Random.State _randomState;

        /// <summary>배회 AI와 같은 두 인터페이스를 구현하는 스텁(AmbientChatterEligibilityTests와 동일 관례).</summary>
        private sealed class StubIntent : IMovementIntentSource, IPlannedDwellSource
        {
            public float MoveInputX { get; set; }
            public float PlannedDwellRemainingSeconds { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting();
            AmbientCalendarClock.Shared.ResetForTesting();
            _randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(20260906);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.dialogueBubbleEnabled = true;
            // 1.0이 아니라 그 위 — Random.value는 1.0을 포함하므로 확률 1.0에서도 2^-24로 침묵이 섞인다.
            _config.idleChatterChance = 2f;
            _config.walkChatterChance = 2f;
            _config.ambientChatterCooldownSeconds = 0f;

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
            UnityEngine.Random.state = _randomState;
            // 주입한 시각이 다음 파일로 새면 그 실패는 무작위로 보인다 — 여기서 반드시 되돌린다.
            AmbientCalendarClock.Shared.ResetForTesting();
            _blackboard = null;
            _intent = null;
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
            AppSettingsModel.ResetForTesting();
        }

        // ==================================================================================
        // 0. 자격 축을 구조로 읽는다 (문자열 니들이 아니라)
        // ==================================================================================

        private static string[] IdleLines() => DialogueCorpus.AmbientLines("IdleLines");
        private static string[] WalkLines() => DialogueCorpus.AmbientLines("WalkLines");

        private static T[] Axis<T>(string fieldName)
        {
            FieldInfo field = typeof(AmbientChatter).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, $"{LogPrefix} AmbientChatter.{fieldName}을 찾지 못했습니다 — " +
                "자격 축의 이름/형태가 바뀌었다면 이 파일도 함께 고쳐야 합니다. 고치지 않으면 " +
                "그 축 전체가 어떤 검사에도 닿지 않습니다(조용한 초록).");
            var value = field.GetValue(null) as T[];
            Assert.IsNotNull(value, $"{LogPrefix} {fieldName}의 원소 타입이 바뀌었습니다.");
            Assert.Greater(value.Length, 0, $"{LogPrefix} {fieldName}이 비었습니다 — " +
                "이 상태로는 아래 모든 순회가 0건을 훑고 통과합니다(거짓 초록).");
            return value;
        }

        private static AmbientDayBucket[] DayAxis(StickmanStateId id)
            => Axis<AmbientDayBucket>(id == StickmanStateId.Walk
                ? "WalkLineDayRequirement" : "IdleLineDayRequirement");

        private static AmbientTimeBucket[] TimeAxis(StickmanStateId id)
            => Axis<AmbientTimeBucket>(id == StickmanStateId.Walk
                ? "WalkLineTimeRequirement" : "IdleLineTimeRequirement");

        private static string[] LinesOf(StickmanStateId id)
            => id == StickmanStateId.Walk ? WalkLines() : IdleLines();

        private static readonly StickmanStateId[] BothTables =
            { StickmanStateId.Idle, StickmanStateId.Walk };

        // ==================================================================================
        // 1. 구간 판정 — 경계와 커버리지 (순수 함수, 시계 없음)
        // ==================================================================================

        /// <summary>
        /// ★ 5구간이 24시간을 <b>빈틈 0 · 겹침 0</b>으로 덮는다. 24개 시각을 전수로 훑고, 기대값은
        /// <see cref="AmbientCalendarPolicy"/>의 경계 <b>상수를 참조</b>해서 만든다 — 숫자를 여기 베끼면
        /// 경계가 바뀌는 날 기준과 대상이 함께 움직여 아무것도 못 잰다(CLAUDE.md 협업 프로토콜).
        /// </summary>
        [Test]
        public void 시간대_5구간이_24시간을_빈틈없이_덮는다()
        {
            var seen = new Dictionary<AmbientTimeBucket, int>();
            for (int hour = 0; hour < AmbientCalendarPolicy.HoursPerDay; hour++)
            {
                AmbientTimeBucket bucket = AmbientCalendarPolicy.TimeBucketOf(hour);
                Assert.AreNotEqual(AmbientTimeBucket.None, bucket,
                    $"{LogPrefix} {hour}시가 어떤 구간에도 속하지 않습니다 — 그 시각에는 시간대 대사가 " +
                    "통째로 침묵하고, 최악 조합의 후보 수가 12에서 10으로 떨어져 계약 여유가 " +
                    "+7.3pp에서 +0.4pp가 됩니다(design/systems/timeofday_r23_bounds.out.txt §1).");

                AmbientTimeBucket expected =
                    hour >= AmbientCalendarPolicy.NightStartHour
                        || hour < AmbientCalendarPolicy.MorningStartHour ? AmbientTimeBucket.Night
                    : hour < AmbientCalendarPolicy.LunchStartHour ? AmbientTimeBucket.Morning
                    : hour < AmbientCalendarPolicy.AfternoonStartHour ? AmbientTimeBucket.Lunch
                    : hour < AmbientCalendarPolicy.EveningStartHour ? AmbientTimeBucket.Afternoon
                    : AmbientTimeBucket.Evening;
                Assert.AreEqual(expected, bucket, $"{LogPrefix} {hour}시의 구간 판정이 경계 상수와 어긋납니다.");

                seen.TryGetValue(bucket, out int n);
                seen[bucket] = n + 1;
            }

            // 다섯 구간이 <b>전부</b> 등장했는가 — 하나라도 0시간이면 그 구간의 대사 2줄이 죽는다.
            foreach (AmbientTimeBucket bucket in Enum.GetValues(typeof(AmbientTimeBucket)))
            {
                if (bucket == AmbientTimeBucket.None) continue;
                Assert.IsTrue(seen.ContainsKey(bucket),
                    $"{LogPrefix} 구간 {bucket}에 속하는 시각이 하루에 하나도 없습니다 — " +
                    "그 구간의 대사는 영원히 침묵하는 죽은 데이터가 됩니다.");
            }

            var parts = new List<string>();
            foreach (KeyValuePair<AmbientTimeBucket, int> pair in seen) parts.Add($"{pair.Key} {pair.Value}h");
            Debug.Log($"{LogPrefix} 24시간 분해 — {string.Join(" / ", parts)} " +
                      $"(합 {AmbientCalendarPolicy.HoursPerDay}h, 겹침 0).");
        }

        /// <summary>
        /// ★ <b>자정 경계</b>. 밤 구간만 날짜를 넘어 감기므로, 여기가 틀리면 «자정을 넘는 순간 밤 대사가
        /// 사라졌다가 05시에 돌아오는» 형태로만 드러난다(하루에 한 번, 밤에만 — 사실상 아무도 못 본다).
        /// </summary>
        [Test]
        public void 자정과_정시_경계에서_구간이_정확히_한_칸씩_넘어간다()
        {
            // 반열린 [시작, 끝) — 시작 시각은 <b>새 구간</b>에 속하고, 그 직전 시각은 이전 구간이다.
            (int hour, AmbientTimeBucket expected)[] probes =
            {
                (23, AmbientTimeBucket.Night),                                   // 자정 직전
                (0, AmbientTimeBucket.Night),                                    // 자정 직후 — 넘어가지 않는다
                (AmbientCalendarPolicy.MorningStartHour - 1, AmbientTimeBucket.Night),
                (AmbientCalendarPolicy.MorningStartHour, AmbientTimeBucket.Morning),
                (AmbientCalendarPolicy.LunchStartHour - 1, AmbientTimeBucket.Morning),
                (AmbientCalendarPolicy.LunchStartHour, AmbientTimeBucket.Lunch),
                (AmbientCalendarPolicy.AfternoonStartHour - 1, AmbientTimeBucket.Lunch),
                (AmbientCalendarPolicy.AfternoonStartHour, AmbientTimeBucket.Afternoon),
                (AmbientCalendarPolicy.EveningStartHour - 1, AmbientTimeBucket.Afternoon),
                (AmbientCalendarPolicy.EveningStartHour, AmbientTimeBucket.Evening),
                (AmbientCalendarPolicy.NightStartHour - 1, AmbientTimeBucket.Evening),
                (AmbientCalendarPolicy.NightStartHour, AmbientTimeBucket.Night),
            };

            foreach ((int hour, AmbientTimeBucket expected) in probes)
            {
                Assert.AreEqual(expected, AmbientCalendarPolicy.TimeBucketOf(hour),
                    $"{LogPrefix} {hour}시의 구간이 어긋납니다 — 경계는 <b>반열린</b>이라 시작 시각은 " +
                    "새 구간에 속해야 합니다.");
            }

            // 분·초는 구간을 바꾸지 않는다(정시 경계만 쓴다 — R23 §4).
            var justBefore = new DateTime(2026, 1, 1, AmbientCalendarPolicy.EveningStartHour - 1, 59, 59);
            var justAfter = new DateTime(2026, 1, 1, AmbientCalendarPolicy.EveningStartHour, 0, 0);
            Assert.AreEqual(AmbientTimeBucket.Afternoon, AmbientCalendarPolicy.Classify(justBefore).Time);
            Assert.AreEqual(AmbientTimeBucket.Evening, AmbientCalendarPolicy.Classify(justAfter).Time);

            // ★★ 자정을 <b>초 단위로</b> 걸친다 — 두 축이 여기서만 <b>서로 다르게</b> 움직인다:
            //    요일은 넘어가고(일→월) 시간대는 <b>밤에 머문다</b>. 한 함수가 둘을 함께 내므로 같이
            //    도는 것처럼 보이지만 축이 다르다. 「2026-09-07 00시가 «일요일 밤의 연장»인가
            //    «월요일 밤의 시작»인가」의 답은 <b>후자</b>이고, 그것이 대사 문안과도 맞는다 —
            //    요일 줄은 전부 «날짜만» 말하기 때문이다(AmbientChatter의 요일 축 주석).
            DateTime midnight = FindMonday();                       // 월요일 00:00:00
            AmbientCalendarSnapshot before = AmbientCalendarPolicy.Classify(midnight.AddSeconds(-1));
            AmbientCalendarSnapshot after = AmbientCalendarPolicy.Classify(midnight.AddSeconds(1));
            Assert.AreEqual(AmbientTimeBucket.Night, before.Time,
                $"{LogPrefix} 자정 1초 전이 밤이 아닙니다 — 밤 구간이 자정에서 끊겼습니다.");
            Assert.AreEqual(AmbientTimeBucket.Night, after.Time,
                $"{LogPrefix} 자정 1초 후가 밤이 아닙니다 — 밤 대사가 자정마다 사라졌다가 " +
                $"{AmbientCalendarPolicy.MorningStartHour}시에 돌아옵니다(하루 한 번, 밤에만 — 아무도 못 봅니다).");
            Assert.AreEqual(AmbientDayBucket.Weekend, before.Day,
                $"{LogPrefix} 월요일 자정 1초 전은 일요일(주말)이어야 합니다.");
            Assert.AreEqual(AmbientDayBucket.Monday, after.Day,
                $"{LogPrefix} 자정을 넘겼는데 요일이 안 넘어갔습니다 — 시간대가 자정을 걸친다고 해서 " +
                "요일까지 어제에 붙잡히면 월요일 대사가 하루 종일 5시간 늦게 시작합니다.");
        }

        /// <summary>손상된 시각은 <b>밤</b>이 아니라 <b>침묵</b>으로 떨어진다 — 잘못된 대사보다 침묵이 낫다.</summary>
        [Test]
        public void 범위_밖_시각은_밤이_아니라_None으로_떨어진다()
        {
            Assert.AreEqual(AmbientTimeBucket.None, AmbientCalendarPolicy.TimeBucketOf(-1));
            Assert.AreEqual(AmbientTimeBucket.None,
                AmbientCalendarPolicy.TimeBucketOf(AmbientCalendarPolicy.HoursPerDay));
            // 대조 — 같은 함수가 정상 입력에서는 실제로 구간을 낸다(판정기가 죽어서 None인 것이 아니다).
            Assert.AreEqual(AmbientTimeBucket.Night,
                AmbientCalendarPolicy.TimeBucketOf(AmbientCalendarPolicy.HoursPerDay - 1));
        }

        [Test]
        public void 요일_구간은_월_금_주말_셋이고_나머지_사흘은_자격이_없다()
        {
            var expected = new Dictionary<DayOfWeek, AmbientDayBucket>
            {
                { DayOfWeek.Monday, AmbientDayBucket.Monday },
                { DayOfWeek.Tuesday, AmbientDayBucket.None },
                { DayOfWeek.Wednesday, AmbientDayBucket.None },
                { DayOfWeek.Thursday, AmbientDayBucket.None },
                { DayOfWeek.Friday, AmbientDayBucket.Friday },
                { DayOfWeek.Saturday, AmbientDayBucket.Weekend },
                { DayOfWeek.Sunday, AmbientDayBucket.Weekend },
            };

            foreach (KeyValuePair<DayOfWeek, AmbientDayBucket> pair in expected)
            {
                Assert.AreEqual(pair.Value, AmbientCalendarPolicy.DayBucketOf(pair.Key),
                    $"{LogPrefix} {pair.Key}의 요일 구간이 어긋납니다.");
            }

            // ★ 「화·수·목이 비어 있는 것」은 누락이 아니라 설계다(R23의 최악 조합이 정확히 그 사흘).
            //   그 사흘에도 시간대 축이 Idle 1 + Walk 1을 보장하므로 후보가 0이 되지 않는다.
            Debug.Log($"{LogPrefix} 요일 자격 3구간(월/금/주말) · 자격 없는 날 3일(화/수/목) — " +
                      "빈 사흘이 R23이 계약을 계산한 «최악 조합»이다.");
        }

        // ==================================================================================
        // 2. 자격 축의 구조 — 표와 길이가 같고, 한 줄에 축은 최대 하나
        // ==================================================================================

        [Test]
        public void 요일_시간대_축은_대사표와_길이가_같다()
        {
            foreach (StickmanStateId id in BothTables)
            {
                int lines = LinesOf(id).Length;
                Assert.AreEqual(lines, DayAxis(id).Length,
                    $"{LogPrefix} {id} 표 {lines}줄과 요일 축의 길이가 어긋났습니다 — 표에 줄만 추가하고 " +
                    "축을 잊으면 새 줄이 조용히 '상시 자격'을 얻고, 줄만 지우면 자격이 옆줄로 밀립니다.");
                Assert.AreEqual(lines, TimeAxis(id).Length,
                    $"{LogPrefix} {id} 표 {lines}줄과 시간대 축의 길이가 어긋났습니다 — 같은 이유입니다.");
            }
        }

        /// <summary>
        /// ★ <b>한 줄에 축은 최대 하나</b>. 두 축이 한 문장에 섞이면 «무엇이 참이라 이 말을 했는가»가
        /// 흐려지고(예: "월요일 아침이네"는 요일 축인가 시간대 축인가), 원칙 1의 역추적이 의미를 잃는다.
        /// design-narrative도 같은 판정을 했다(2026-09-06_시간대5구간 §3 «요일» 금칙 그룹).
        /// </summary>
        [Test]
        public void 한_줄에_자격_축이_둘_이상_붙지_않는다()
        {
            var motion = Axis<WanderAmbientMotion?>("IdleLineMotionRequirement");
            int conditional = 0;

            foreach (StickmanStateId id in BothTables)
            {
                string[] lines = LinesOf(id);
                AmbientDayBucket[] day = DayAxis(id);
                AmbientTimeBucket[] time = TimeAxis(id);
                for (int i = 0; i < lines.Length; i++)
                {
                    int axes = 0;
                    if (id == StickmanStateId.Idle && motion[i].HasValue) axes++;
                    if (day[i] != AmbientDayBucket.None) axes++;
                    if (time[i] != AmbientTimeBucket.None) axes++;
                    Assert.LessOrEqual(axes, 1,
                        $"{LogPrefix} {id} 줄 \"{lines[i]}\"에 자격 축이 {axes}개 붙었습니다 — " +
                        "두 축이 한 문장에 섞이면 그 문장이 «무엇 때문에 참인지»를 역추적할 수 없습니다.");
                    if (axes == 1) conditional++;
                }
            }

            Assert.Greater(conditional, 0,
                $"{LogPrefix} 조건부 줄이 하나도 없습니다 — 위 순회가 0건을 훑고 통과했다는 뜻입니다.");
            Debug.Log($"{LogPrefix} 조건부 줄 {conditional}개 전부 «축 정확히 1개».");
        }

        /// <summary>
        /// ★★ R23의 핵심 요구 — <b>「모든 시간대가 자격 2(Idle 1 + Walk 1)를 갖는다」</b>.
        /// 한 구간이라도 한쪽 표에서 비면 그 시각·그 상태에서 후보 수 N이 한 칸 떨어지고,
        /// 벼랑(N=9에서 허용 k가 4→3으로 후퇴)에 그만큼 가까워진다.
        /// 요일 3구간도 같은 형태로 확인한다(R2 §3-4 #13~#18).
        /// </summary>
        [Test]
        public void 모든_시간대와_요일_구간이_Idle_1줄과_Walk_1줄을_갖는다()
        {
            foreach (AmbientTimeBucket bucket in Enum.GetValues(typeof(AmbientTimeBucket)))
            {
                if (bucket == AmbientTimeBucket.None) continue;
                foreach (StickmanStateId id in BothTables)
                {
                    Assert.AreEqual(1, CountWhere(TimeAxis(id), bucket),
                        $"{LogPrefix} 시간대 {bucket}의 {id} 대사가 정확히 1줄이 아닙니다 — " +
                        "0줄이면 그 시각 그 상태의 후보가 한 칸 줄고(R23 계약 여유 -7pp), " +
                        "2줄 이상이면 그 구간만 상대 빈도가 두 배가 됩니다.");
                }
            }

            foreach (AmbientDayBucket bucket in Enum.GetValues(typeof(AmbientDayBucket)))
            {
                if (bucket == AmbientDayBucket.None) continue;
                foreach (StickmanStateId id in BothTables)
                {
                    Assert.AreEqual(1, CountWhere(DayAxis(id), bucket),
                        $"{LogPrefix} 요일 {bucket}의 {id} 대사가 정확히 1줄이 아닙니다.");
                }
            }
        }

        private static int CountWhere<T>(T[] axis, T value)
        {
            int n = 0;
            foreach (T item in axis)
            {
                if (EqualityComparer<T>.Default.Equals(item, value)) n++;
            }
            return n;
        }

        // ==================================================================================
        // 3. 자격 판정 — 전수(7요일 × 24시)로 훑는다
        // ==================================================================================

        /// <summary>이 주의 월요일. <b>「2026-09-07은 월요일이다」라고 주석으로 주장하지 않는다</b> —
        /// 손으로 계산한 요일이 그대로 다음 거짓말이 된다. 실제로 찾아서 쓴다.</summary>
        private static DateTime FindMonday()
        {
            var probe = new DateTime(2026, 9, 1);
            for (int i = 0; i < 7; i++)
            {
                if (probe.DayOfWeek == DayOfWeek.Monday) return probe;
                probe = probe.AddDays(1);
            }
            Assert.Fail($"{LogPrefix} 7일 안에 월요일을 못 찾았습니다 — 달력이 깨졌습니다.");
            return default;
        }

        /// <summary>
        /// ★★ 전수 검사 — <b>168개 (요일 × 시각) 조합</b>에서 각 조건부 줄이 «자기 구간에서만» 후보다.
        ///
        /// <para>「안 나온다」와 「나온다」를 <b>같은 순회 안에서</b> 센다. 그래서 «언제나 false»라는
        /// 오답이 통과할 수 없다 — 그 오답은 아래 <c>hit</c> 카운트를 0으로 만든다.</para>
        /// </summary>
        [Test]
        public void 조건부_줄은_자기_구간에서만_후보이고_그_구간에서는_반드시_후보다()
        {
            DateTime monday = FindMonday();

            // ★★ 2026-09-06 러너 실측으로 고친 자리 — <b>자격 축은 셋이다</b>(모션 / 요일 / 시간대, AND).
            //   이 파일이 재는 것은 뒤의 둘이지만, 기대값에서 <b>모션을 빼면</b> 모션 조건이 붙은 Idle
            //   2줄("하암..."·"구경 중이야")이 168시간 <b>전부</b>에서 어긋나고, 그 실패는 화면상
            //   «달력 판정이 깨졌다»(첫 실패가 하필 자정인 2026-09-07 00시)로 읽힌다 — 실제로는 달력이
            //   아니라 이 기대식이 틀린 것이다. 판정 대상(IsLineEligible)의 계약은
            //   <c>AmbientChatter.IsLineEligible</c> 문서에 «세 축을 AND로 묶는다»로 적혀 있다.
            //
            //   이 리그는 배회 모션을 <b>한 번도 시작하지 않으므로</b> 「모션을 요구하는 줄은 항상
            //   부적격」이 기대값이다. 그 전제를 여기서 단언으로 못 박는다 — 전제가 조용히 바뀌면
            //   아래 기대식이 통째로 무의미해지는데, 그 실패는 초록과 똑같이 생긴다.
            Assert.IsFalse(_blackboard.IsIdleAmbientMotionActive,
                $"{LogPrefix} 리그가 배회 모션을 재생 중입니다 — 아래 기대식은 «모션 없음»을 전제로 " +
                "만들어져 있어 이 상태에서는 아무것도 검사하지 않습니다.");
            WanderAmbientMotion?[] idleMotion = Axis<WanderAmbientMotion?>("IdleLineMotionRequirement");
            Assert.AreEqual(IdleLines().Length, idleMotion.Length,
                $"{LogPrefix} 모션 축 길이가 Idle 표와 어긋났습니다 — 아래 순회가 범위 밖을 짚어 " +
                "«자격이 어긋났다»가 아니라 예외로 죽습니다(원인이 가려집니다).");

            foreach (StickmanStateId id in BothTables)
            {
                string[] lines = LinesOf(id);
                AmbientDayBucket[] day = DayAxis(id);
                AmbientTimeBucket[] time = TimeAxis(id);
                // Walk 표에는 모션 축이 없다(AmbientChatter.MotionRequirementFor가 Walk에서 즉시 null).
                WanderAmbientMotion?[] motion = id == StickmanStateId.Walk ? null : idleMotion;
                var hits = new int[lines.Length];
                var misses = new int[lines.Length];
                int probes = 0;

                for (int d = 0; d < 7; d++)
                {
                    for (int h = 0; h < AmbientCalendarPolicy.HoursPerDay; h++)
                    {
                        DateTime when = monday.AddDays(d).AddHours(h);
                        AmbientCalendarSnapshot calendar = AmbientCalendarPolicy.Classify(when);
                        probes++;

                        for (int i = 0; i < lines.Length; i++)
                        {
                            bool dayOk = day[i] == AmbientDayBucket.None || day[i] == calendar.Day;
                            bool timeOk = time[i] == AmbientTimeBucket.None || time[i] == calendar.Time;
                            // 모션 축은 리그 전제(모션 없음)에서 파생한다 — IsLineEligible을 다시 부르면
                            // 기대값과 대상이 같은 함수가 되어 아무것도 못 잰다(항등식).
                            bool motionOk = motion == null || !motion[i].HasValue;
                            bool eligible = AmbientChatter.IsLineEligible(_blackboard, id, i, calendar);

                            Assert.AreEqual(dayOk && timeOk && motionOk, eligible,
                                $"{LogPrefix} {when:yyyy-MM-dd(ddd) HH시} — {id} 줄 \"{lines[i]}\"의 자격이 " +
                                $"달력({calendar})·모션(요구 {(motion == null ? "축없음" : motion[i]?.ToString() ?? "없음")}, " +
                                "리그 재생 없음)과 어긋납니다.");

                            if (day[i] == AmbientDayBucket.None && time[i] == AmbientTimeBucket.None) continue;
                            if (eligible) hits[i]++; else misses[i]++;
                        }
                    }
                }

                Assert.AreEqual(7 * AmbientCalendarPolicy.HoursPerDay, probes,
                    $"{LogPrefix} 전수 순회가 예상보다 적게 돌았습니다 — 0건을 훑고 통과할 수 있습니다.");

                for (int i = 0; i < lines.Length; i++)
                {
                    if (day[i] == AmbientDayBucket.None && time[i] == AmbientTimeBucket.None) continue;

                    // ★ 네거티브 컨트롤 ① — 자기 구간이 <b>실제로 존재</b>한다(죽은 데이터가 아니다).
                    Assert.Greater(hits[i], 0,
                        $"{LogPrefix} 조건부 줄 \"{lines[i]}\"({id})가 일주일 168시간 중 한 번도 자격을 " +
                        "얻지 못했습니다 — 영원히 침묵하는 죽은 데이터입니다(«언제나 false» 오답).");
                    // ★ 네거티브 컨트롤 ② — 그런데 <b>상시는 아니다</b>(조건이 실제로 무언가를 거른다).
                    Assert.Greater(misses[i], 0,
                        $"{LogPrefix} 조건부 줄 \"{lines[i]}\"({id})가 168시간 내내 자격을 가졌습니다 — " +
                        "자격 조건이 아무것도 안 거르고 있습니다(원칙 1 위반의 재발).");
                }
            }
        }

        /// <summary>
        /// ★ 어느 (요일 × 시각)에서도 <b>후보가 0이 되지 않는다</b>. 0이 되면 그 시간대에 캐릭터가
        /// 통째로 벙어리가 되는데, 그 실패는 로그도 예외도 안 남긴다(<c>TryRollChatter</c>가 조용히
        /// false를 돌려준다).
        /// </summary>
        [Test]
        public void 어떤_시각에도_후보가_0이_되지_않는다()
        {
            DateTime monday = FindMonday();
            int worst = int.MaxValue;
            DateTime worstWhen = default;

            for (int d = 0; d < 7; d++)
            {
                for (int h = 0; h < AmbientCalendarPolicy.HoursPerDay; h++)
                {
                    DateTime when = monday.AddDays(d).AddHours(h);
                    AmbientCalendarSnapshot calendar = AmbientCalendarPolicy.Classify(when);

                    int total = 0;
                    foreach (StickmanStateId id in BothTables)
                    {
                        int n = 0;
                        string[] lines = LinesOf(id);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (AmbientChatter.IsLineEligible(_blackboard, id, i, calendar)) n++;
                        }
                        Assert.Greater(n, 0,
                            $"{LogPrefix} {when:yyyy-MM-dd(ddd) HH시}의 {id} 후보가 0개입니다 — " +
                            "그 시각 그 상태에서 캐릭터가 통째로 벙어리가 됩니다.");
                        total += n;
                    }

                    if (total < worst) { worst = total; worstWhen = when; }
                }
            }

            Debug.Log($"{LogPrefix} 168시간 전수 — 최악 조합은 {worstWhen:yyyy-MM-dd(ddd) HH시}의 " +
                      $"후보 {worst}개(Idle+Walk 합, 모션 없음). R23이 계약을 계산한 최악값과 같은 자리다.");
        }

        /// <summary>달력을 <b>아직 한 번도 안 본</b> 상태에서는 조건부 줄이 전부 침묵한다 —
        /// 잘못된 요일 대사보다 침묵이 낫다. 상시 줄은 그때도 살아 있다(대조).</summary>
        [Test]
        public void 달력을_모르는_상태에서는_조건부_줄이_전부_침묵하고_상시_줄은_산다()
        {
            AmbientCalendarSnapshot unknown = AmbientCalendarSnapshot.Unknown;
            Assert.AreEqual(AmbientDayBucket.None, unknown.Day);
            Assert.AreEqual(AmbientTimeBucket.None, unknown.Time);

            int alive = 0;
            foreach (StickmanStateId id in BothTables)
            {
                string[] lines = LinesOf(id);
                AmbientDayBucket[] day = DayAxis(id);
                AmbientTimeBucket[] time = TimeAxis(id);
                for (int i = 0; i < lines.Length; i++)
                {
                    bool conditional = day[i] != AmbientDayBucket.None || time[i] != AmbientTimeBucket.None;
                    bool eligible = AmbientChatter.IsLineEligible(_blackboard, id, i, unknown);
                    if (conditional)
                    {
                        Assert.IsFalse(eligible,
                            $"{LogPrefix} 달력을 모르는데 \"{lines[i]}\"가 후보입니다 — " +
                            "«아직 안 봤다»가 «모든 구간에 해당한다»로 읽히고 있습니다.");
                    }
                    else if (eligible) alive++;
                }
            }

            Assert.Greater(alive, 0,
                $"{LogPrefix} 달력을 모를 때 상시 줄까지 전부 침묵했습니다 — 위 단언은 " +
                "«언제나 false»라는 오답과 구별되지 않습니다.");
        }

        // ==================================================================================
        // 4. 추첨 경로 — 60초 캐시가 실제로 배선돼 있는가
        // ==================================================================================

        private static void PinClock(DateTime when)
        {
            AmbientCalendarClock.Shared.WallClockOverrideForTesting = () => when;
            AmbientCalendarClock.Shared.PollNow(Time.realtimeSinceStartupAsDouble);
        }

        private int RollUntilPicked(StickmanStateId id, int lineIndex, int trials)
        {
            var target = new AmbientChatter.ChatterParams();
            int hits = 0;
            for (int i = 0; i < trials; i++)
            {
                _blackboard.NextChatterAllowedUnscaledTime = 0f;
                if (id == StickmanStateId.Walk) _intent.MoveInputX = _config.moveInputDeadzone + 1f;
                if (!AmbientChatter.TryRollChatter(_blackboard, id, target)) continue;
                if (target.LineIndex == lineIndex) hits++;
            }
            return hits;
        }

        /// <summary>
        /// ★★ <b>배선의 본체</b> — 시계가 월요일을 가리키면 추첨 결과에 월요일 줄이 실제로 섞이고,
        /// 화요일을 가리키면 <b>한 번도</b> 안 나온다.
        ///
        /// <para>이 검사가 곧 «<see cref="AmbientCalendarClock"/>가 <see cref="AmbientChatter"/>에
        /// 배선돼 있는가»의 증명이다. 소스에 이름이 있는지 grep하는 것으로는 «배선했지만 결과가
        /// 안 쓰인다»를 못 본다.</para>
        /// </summary>
        [Test]
        public void 시계가_월요일이면_월요일_줄이_뽑히고_화요일이면_한_번도_안_뽑힌다()
        {
            DateTime monday = FindMonday().AddHours(AmbientCalendarPolicy.MorningStartHour);
            AmbientDayBucket[] day = DayAxis(StickmanStateId.Idle);
            int mondayLine = System.Array.IndexOf(day, AmbientDayBucket.Monday);
            Assert.Greater(mondayLine, -1, $"{LogPrefix} 월요일 Idle 줄이 자격 축에 없습니다.");
            string text = IdleLines()[mondayLine];

            PinClock(monday);
            Assert.AreEqual(AmbientDayBucket.Monday, AmbientCalendarClock.Shared.Current.Day,
                $"{LogPrefix} 시계 주입이 반영되지 않았습니다 — 아래 결과는 무효입니다.");
            int onMonday = RollUntilPicked(StickmanStateId.Idle, mondayLine, 3000);
            Assert.Greater(onMonday, 0,
                $"{LogPrefix} 월요일인데 \"{text}\"가 3000회 추첨에서 한 번도 안 나왔습니다 — " +
                "달력이 TryRollChatter에 배선되지 않았거나 스냅샷이 자격 판정에 안 닿습니다.");

            PinClock(monday.AddDays(1)); // 화요일 — 요일 자격 없음
            Assert.AreEqual(AmbientDayBucket.None, AmbientCalendarClock.Shared.Current.Day,
                $"{LogPrefix} 화요일인데 요일 자격이 붙었습니다.");
            int onTuesday = RollUntilPicked(StickmanStateId.Idle, mondayLine, 3000);
            Assert.AreEqual(0, onTuesday,
                $"{LogPrefix} 화요일에 \"{text}\"가 {onTuesday}회 나왔습니다 — 원칙 1 위반입니다.");

            Debug.Log($"{LogPrefix} \"{text}\" — 월요일 3000회 중 {onMonday}회 / 화요일 3000회 중 0회.");
        }

        /// <summary>시간대 쪽도 같은 형태로 확인한다 — 신규 4줄 중 <b>저녁 Walk</b>를 대표로 잡는다
        /// (가장 긴 줄이라 발화 자격 게이트에도 가장 가깝다: 9자).</summary>
        [Test]
        public void 시계가_저녁이면_저녁_Walk_줄이_뽑히고_아침이면_한_번도_안_뽑힌다()
        {
            AmbientTimeBucket[] time = TimeAxis(StickmanStateId.Walk);
            int eveningLine = System.Array.IndexOf(time, AmbientTimeBucket.Evening);
            Assert.Greater(eveningLine, -1, $"{LogPrefix} 저녁 Walk 줄이 자격 축에 없습니다.");
            string text = WalkLines()[eveningLine];

            _intent.PlannedDwellRemainingSeconds = _config.wanderWalkDurationMax;
            DateTime day = FindMonday().AddDays(1); // 화요일 — 요일 축이 안 끼어들게

            PinClock(day.AddHours(AmbientCalendarPolicy.EveningStartHour));
            Assert.AreEqual(AmbientTimeBucket.Evening, AmbientCalendarClock.Shared.Current.Time);
            int inEvening = RollUntilPicked(StickmanStateId.Walk, eveningLine, 3000);
            Assert.Greater(inEvening, 0,
                $"{LogPrefix} 저녁인데 \"{text}\"가 3000회 추첨에서 한 번도 안 나왔습니다.");

            PinClock(day.AddHours(AmbientCalendarPolicy.MorningStartHour));
            Assert.AreEqual(AmbientTimeBucket.Morning, AmbientCalendarClock.Shared.Current.Time);
            Assert.AreEqual(0, RollUntilPicked(StickmanStateId.Walk, eveningLine, 3000),
                $"{LogPrefix} 아침에 \"{text}\"가 나왔습니다 — 원칙 1 위반입니다.");

            Debug.Log($"{LogPrefix} \"{text}\"({text.Length}자) — 저녁 3000회 중 {inEvening}회 / 아침 0회.");
        }

        /// <summary>
        /// ★ 60초 캐시가 <b>실제로 막는가</b>. 추첨을 수천 번 돌려도 벽시계는 한 번만 읽힌다 —
        /// 이 앱은 하루 종일 켜져 있고 <c>DateTime.Now</c>는 공짜가 아니다.
        /// <para>그리고 «첫 호출은 기다리지 않는다»도 함께 본다(그 성질이 없으면 앱을 켠 뒤 60초 동안
        /// 요일·시간대 대사가 통째로 침묵한다).</para>
        /// </summary>
        [Test]
        public void 달력은_60초에_한_번만_읽히고_첫_호출은_기다리지_않는다()
        {
            int reads = 0;
            DateTime when = FindMonday().AddHours(AmbientCalendarPolicy.LunchStartHour);
            AmbientCalendarClock.Shared.WallClockOverrideForTesting = () => { reads++; return when; };

            Assert.AreEqual(0, AmbientCalendarClock.Shared.PollCount,
                $"{LogPrefix} 리셋 직후인데 폴링 횟수가 0이 아닙니다.");

            double t0 = Time.realtimeSinceStartupAsDouble;
            Assert.AreEqual(AmbientDayBucket.Monday, AmbientCalendarClock.Shared.PollIfDue(t0).Day,
                $"{LogPrefix} 첫 호출이 간격을 기다렸습니다 — 앱을 켠 뒤 " +
                $"{AmbientCalendarClock.PollIntervalSeconds:F0}초 동안 요일·시간대 대사가 침묵합니다.");
            Assert.AreEqual(1, reads);

            var target = new AmbientChatter.ChatterParams();
            for (int i = 0; i < 2000; i++)
            {
                _blackboard.NextChatterAllowedUnscaledTime = 0f;
                AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target);
            }
            Assert.AreEqual(1, reads,
                $"{LogPrefix} 2000회 추첨에서 벽시계를 {reads}회 읽었습니다 — 60초 캐시가 안 걸렸거나 " +
                "자격 판정 안에서 시계를 읽고 있습니다(순수성 위반).");

            // 간격을 넘긴 시각을 주면 다시 읽는다 — «언제나 막는다»는 오답을 배제한다.
            AmbientCalendarClock.Shared.PollIfDue(t0 + AmbientCalendarClock.PollIntervalSeconds);
            Assert.AreEqual(2, reads,
                $"{LogPrefix} 간격을 넘겼는데도 다시 읽지 않았습니다 — 캐시가 영구 동결이라는 뜻이고, " +
                "자정을 넘겨도 어제 요일로 말하게 됩니다.");
        }

        /// <summary>손상된 단조 시각(NaN)이 게이트를 <b>거꾸로 통과</b>하지 않는다 —
        /// 통과하면 그 뒤 모든 비교가 NaN이 되어 매 추첨마다 시스템 시계를 읽게 된다.</summary>
        [Test]
        public void NaN_단조시각은_게이트를_거꾸로_통과하지_않는다()
        {
            int reads = 0;
            DateTime when = FindMonday();
            AmbientCalendarClock.Shared.WallClockOverrideForTesting = () => { reads++; return when; };

            AmbientCalendarClock.Shared.PollIfDue(double.NaN);
            Assert.AreEqual(0, reads, $"{LogPrefix} NaN 시각으로 달력을 읽었습니다.");
            Assert.AreEqual(AmbientCalendarSnapshot.Unknown.Day, AmbientCalendarClock.Shared.Current.Day);

            // 대조 — 정상 시각이 들어오면 즉시 낫는다(«언제나 막는다»가 아니다).
            AmbientCalendarClock.Shared.PollIfDue(Time.realtimeSinceStartupAsDouble);
            Assert.AreEqual(1, reads, $"{LogPrefix} 정상 시각에도 읽지 않았습니다 — 게이트가 영구히 닫혔습니다.");
        }

        // ==================================================================================
        // 5. 문안 — 정본과 어긋나지 않았는가
        // ==================================================================================

        /// <summary>
        /// ★★ <b>«발이 빨라지네»가 배선되지 않았다.</b>
        ///
        /// <para>선행 라운드(<c>design/narrative/2026-09-02_대사체계_실측과_계약.md</c> §5-2)가 제안한
        /// 금요일 Walk 문구인데, <b>R2 §3-5가 기각했다</b>: 보행 속도는 요일에 따라 실제로 안 변하고
        /// (<c>WalkState</c>의 어떤 값도 요일을 안 본다), 같은 금요일에 상시 줄 «다리가 잘 나가네»와
        /// 나란히 나오면 정면 모순이다. 정본은 개정안 #17 «주말이 코앞이네»다.</para>
        ///
        /// <para>★ 이것은 <b>부재 단언</b>이라 대조를 반드시 건다(CLAUDE.md: 부재 단언은 썩어도 조용히
        /// 초록이 된다). 같은 판정기가 <b>정본 대체 문구</b>를 먼저 찾아내지 못하면 아래 «없다»는
        /// 아무것도 증명하지 않는다.</para>
        /// </summary>
        [Test]
        public void 기각된_금요일_Walk_문구는_어떤_대사_경로에도_없다()
        {
            const string rejected = "발이 빨라지네";
            const string canonical = "주말이 코앞이네";

            List<string> corpus = DialogueCorpus.ScanDistinct();

            // ── 대조(존재 단언) — 같은 판정기가 정본 대체 문구는 실제로 찾아낸다.
            CollectionAssert.Contains(corpus, canonical,
                $"{LogPrefix} 정본 금요일 Walk 문구 \"{canonical}\"(R2 §3-4 #17)가 말뭉치에 없습니다 — " +
                "아래 부재 단언은 무효이고, 금요일 Walk 자리가 통째로 비었다는 뜻입니다.");

            CollectionAssert.DoesNotContain(corpus, rejected,
                $"{LogPrefix} 기각된 문구 \"{rejected}\"가 배선됐습니다 — 보행 속도는 요일에 따라 " +
                "변하지 않으므로 화면이 즉시 반증합니다(절대 불변 원칙 1). " +
                "판정 근거: design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md §3-5.");
        }

        /// <summary>
        /// ★ 배선된 16줄이 <b>말뭉치와 골든에 실제로 도달</b>했는가. 대사표에만 있고 수집기에 안 걸리면
        /// 어떤 회귀 검사에도 닿지 않는다(<c>DialogueCorpus</c> 클래스 문서의 «조용한 초록»).
        /// <para>여기서는 문구를 니들로 적지 않는다 — <b>자격 축이 조건부라고 표시한 줄 전부</b>를
        /// 말뭉치에서 찾는다. 문구가 바뀌어도 이 검사는 계속 유효하다.</para>
        /// </summary>
        [Test]
        public void 조건부_줄은_전부_말뭉치_수집기에_잡힌다()
        {
            List<string> corpus = DialogueCorpus.ScanDistinct();
            int checkedCount = 0;

            foreach (StickmanStateId id in BothTables)
            {
                string[] lines = LinesOf(id);
                AmbientDayBucket[] day = DayAxis(id);
                AmbientTimeBucket[] time = TimeAxis(id);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (day[i] == AmbientDayBucket.None && time[i] == AmbientTimeBucket.None) continue;
                    CollectionAssert.Contains(corpus, lines[i],
                        $"{LogPrefix} 조건부 줄 \"{lines[i]}\"({id})가 말뭉치 수집기에 안 잡힙니다 — " +
                        "가독예산 골든에도 안 실리므로 어떤 회귀 검사에도 닿지 않습니다.");
                    checkedCount++;
                }
            }

            Assert.Greater(checkedCount, 0,
                $"{LogPrefix} 조건부 줄이 하나도 없습니다 — 위 순회가 0건을 훑고 통과했습니다.");
            Debug.Log($"{LogPrefix} 조건부 {checkedCount}줄 전부 말뭉치 도달 확인(고유 {corpus.Count}줄 중).");
        }

        // ==================================================================================
        // 6. 테스트 통로가 프로덕션으로 새지 않았는가
        // ==================================================================================

        /// <summary>
        /// ★ <see cref="AmbientCalendarClock.WallClockOverrideForTesting"/>은 <b>테스트 전용</b>이다.
        /// 프로덕션 코드가 이것을 세우면 출하본이 가짜 시계로 도는데, 그 실패는 화면에서
        /// «요일 대사가 이상하다»로만 보이고 로그가 없다.
        ///
        /// <para>★ 부재 단언이므로 <b>양성 대조를 먼저</b> 한다 — 같은 스캐너가 테스트 폴더에서는
        /// 실제로 찾아내야 한다. 못 찾으면 니들이 썩은 것이고, 그러면 «프로덕션에 0건»은
        /// 아무것도 증명하지 않는다(이 저장소가 반복해서 당한 형태).</para>
        ///
        /// <para>★★ <b>2026-09-06 러너 실측으로 고친 자리 — 「설치」와 「철거」를 가른다.</b>
        /// 구판은 <c>"WallClockOverrideForTesting ="</c> 한 줄짜리 니들이라
        /// <c>ResetForTesting()</c>의 <c>= null;</c>(=<b>가짜 시계를 치우는</b> 줄)을
        /// <b>가짜 시계를 세우는 줄과 똑같이</b> 셌고, 그래서 통로를 선언한 파일 자신이 스스로에게
        /// 걸려 빨간불이 났다. 이 감사가 실제로 막아야 하는 것은 «출하본이 가짜 시계로 돈다»이므로,
        /// 판정 단위를 <b>파일</b>에서 <b>줄</b>로 내리고 <b>대입값이 null인가</b>로 가른다.
        /// 그 결과 감사는 느슨해지지 않고 <b>좁아졌다</b> — 양성 대조도 «아무 대입이나»가 아니라
        /// «실제 설치»를 찾아내야 통과한다.</para>
        /// </summary>
        [Test]
        public void 프로덕션은_테스트용_시계_주입_통로를_쓰지_않는다()
        {
            const string needle = "WallClockOverrideForTesting =";
            const string revoke = "WallClockOverrideForTesting = null;";   // 철거 — 이것만 무해하다
            const string declaration = "Func<DateTime> WallClockOverrideForTesting";
            string scripts = DialogueCorpus.ScriptsRoot;
            Assert.IsTrue(Directory.Exists(scripts), $"{LogPrefix} 스크립트 루트를 찾지 못했습니다: {scripts}");

            string testsRoot = Path.Combine(scripts, "Tests");
            var productionInstalls = new List<string>();   // ★ 이것이 0이어야 한다
            var productionRevokes = new List<string>();
            var testInstalls = new List<string>();
            var declaringFiles = new List<string>();

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string[] fileLines = File.ReadAllLines(file);
                bool isTest = file.StartsWith(testsRoot, StringComparison.Ordinal);
                for (int i = 0; i < fileLines.Length; i++)
                {
                    string line = fileLines[i];
                    if (!isTest && line.Contains(declaration) && !declaringFiles.Contains(file))
                    {
                        declaringFiles.Add(file);
                    }
                    if (!line.Contains(needle)) continue;

                    // ★ 이 감사 자신의 <b>니들 선언·주석</b>(따옴표 바로 뒤에 오는 형태)은 세지 않는다.
                    //   세면 실제 설치가 전부 사라져도 양성 대조가 <b>자기 상수만 보고</b> 초록이 된다 —
                    //   「니들이 자기 자신만 찾는」 형태이고, 이 저장소가 반복해서 당한 거짓 초록이다.
                    if (line.IndexOf("\"" + needle, StringComparison.Ordinal) >= 0) continue;

                    // 「= null;」은 <b>연속된 한 덩어리</b>로만 인정한다 — «= 조건 ? 가짜 : null;»은
                    // 이 부분문자열을 포함하지 않으므로 설치로 분류된다.
                    string where = $"{Path.GetFileName(file)}:{i + 1}";
                    bool isRevoke = line.TrimEnd().EndsWith(revoke, StringComparison.Ordinal);
                    if (isTest) { if (!isRevoke) testInstalls.Add(where); }
                    else if (isRevoke) productionRevokes.Add(where);
                    else productionInstalls.Add(where);
                }
            }

            // ── 양성 대조 ① — 스캐너가 <b>설치</b>를 실제로 찾아내는가(«아무 대입이나»가 아니다).
            Assert.Greater(testInstalls.Count, 0,
                $"{LogPrefix} \"{needle}\" 형태의 <b>설치</b>를 테스트 폴더에서도 한 건도 못 찾았습니다 — " +
                "니들이 썩었거나 스캐너가 죽었습니다. 아래 «프로덕션 0건»은 이 상태에서 아무것도 " +
                "증명하지 않습니다.");

            // ── 양성 대조 ② — 통로 선언을 실제로 짚었는가. 못 짚으면 아래 «철거 자리» 판정이 무효다.
            Assert.AreEqual(1, declaringFiles.Count,
                $"{LogPrefix} 시계 주입 통로를 선언한 프로덕션 파일이 {declaringFiles.Count}개입니다" +
                $"(기대 1) — 선언 니들 \"{declaration}\"이 썩었거나 통로가 둘로 갈라졌습니다.");

            // ── 본 단언 — 출하 경로에 <b>가짜 시계를 세우는</b> 줄이 하나도 없다.
            Assert.AreEqual(0, productionInstalls.Count,
                $"{LogPrefix} 프로덕션 코드가 테스트용 시계를 세웁니다: {string.Join(", ", productionInstalls)} — " +
                "출하본이 가짜 시계로 돌게 되고, 그 실패는 로그도 예외도 남기지 않습니다.");

            // ── 철거는 무해하지만 <b>통로를 선언한 파일 안</b>에서만 허용한다. 다른 프로덕션 파일이
            //    테스트 통로의 수명을 관리하고 있다면 그 자체로 통로가 새어 나간 것이다.
            string declaringName = Path.GetFileName(declaringFiles[0]);
            foreach (string where in productionRevokes)
            {
                Assert.IsTrue(where.StartsWith(declaringName, StringComparison.Ordinal),
                    $"{LogPrefix} 통로를 선언하지 않은 프로덕션 파일이 테스트 시계를 치웁니다: {where} — " +
                    $"철거가 허용되는 자리는 {declaringName}의 ResetForTesting 하나뿐입니다.");
            }

            Debug.Log($"{LogPrefix} 시계 주입 통로 — 설치: 테스트 {testInstalls.Count}곳 / 프로덕션 0곳, " +
                      $"철거: 프로덕션 {productionRevokes.Count}곳(전부 {declaringName}).");
        }
    }
}
