using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 자리 비움 배회 감축(2026-09-07, design-motion 처방)의 회귀 못.
    ///
    /// <para><b>기능 한 줄</b>: 사용자가 <see cref="FramePacingPolicy.AwaySeconds"/> 이상 입력을 하지
    /// 않으면 배회 AI가 <b>걷기 확률만</b> 낮춘다(평소 <c>wanderPostIdleWalkChance</c> →
    /// <c>awayWanderWalkChance</c>). Idle 길이·두리번 주기·경계 행동은 <b>한 줄도 바뀌지 않는다.</b></para>
    ///
    /// <para><b>이 파일이 재는 것 여섯 가지</b>
    /// <list type="number">
    ///   <item><b>순수 판정 경계</b> — <see cref="FramePacingPolicy.IsViewerLikelyAway"/>가
    ///     <c>AwaySeconds</c> 앞뒤에서 갈리고, 관측 실패(<c>Valid=false</c>)는 <b>무입력이 아무리 길어도</b>
    ///     항상 거짓인가. 경계값은 전부 <c>AwaySeconds</c>에서 만든다 — <b>180을 리터럴로 베끼지 않는다</b>
    ///     (상수가 바뀌는 날 테스트가 조용히 낡는 것을 막는다).</item>
    ///   <item><b>양성 대조 + 네거티브 컨트롤</b> — 자리 비움에서 <c>awayWanderWalkChance</c>가 <b>실제로
    ///     쓰였음</b>을 먼저 보이고, 그 다음 음수 센티널로 끄면 평소 확률로 <b>정확히</b> 돌아오는가.
    ///     부재 단언만 두면 기능이 애초에 안 붙어 있어도 초록이 된다(이 저장소 반복 원칙).</item>
    ///   <item><b>사다리 우선순위</b> — 부채꼴 0 &gt; 자리비움 &gt; 집중 &gt; 평소. 두 조건이 <b>동시에
    ///     성립</b>할 때 어느 쪽이 이기는가를 «둘 다 켜져 있음»을 확인한 뒤에 잰다.</item>
    ///   <item>★★★ <b>Idle 길이 불변</b> — 이 기능의 핵심 설계 제약이자 <b>가장 깨지기 쉬운 지점</b>이다.
    ///     집중 세션 선례가 <c>wanderIdleDurationMin/Max</c>(2~6초)를 4~11초로 늘렸다가 복귀 지연 문제를
    ///     냈고, 다음 사람이 <b>반사적으로 같은 패턴을 자리 비움에도 적용할</b> 위험이 가장 크다
    ///     (design-motion이 명시적으로 경고). 그래서 여기서는 <b>같은 난수로 뽑은 Idle 길이가 비트 단위로
    ///     같은지</b>를 잠그고, <b>같은 저울에 집중 세션을 올려 실제로 달라짐을 보이는</b> 양성 대조를 붙인다
    ///     — 대조가 없으면 «안 바뀌었다»와 «저울이 고장났다»가 똑같이 생긴다.</item>
    ///   <item><b>구조 잠금</b> — <c>StickConfig</c>에 <c>away*</c> 손잡이가 걷기 확률 하나뿐인가,
    ///     그리고 <c>EnterResting()</c>이 자리 비움 판정을 <b>읽지 않는가</b>(같은 추출기로 뽑은
    ///     <c>ResolvePostIdleBranch()</c>는 반대로 <b>읽는다</b>는 양성 대조 포함).</item>
    ///   <item><b>안전한 실패</b> — OS 관측이 없으면(에디터·테스트·<c>STICKMATE_ADAPTIVE_PACING=0</c>)
    ///     조용히 꺼지고 평소대로 걷는가. 그리고 판정이 <b>플랫폼 중립 파일</b>에만 있는가.</item>
    /// </list></para>
    ///
    /// <para><b>어떻게 재는가 — 시뮬레이션이 아니라 프로덕션 컨트롤러를 직접 몬다.</b>
    /// <see cref="AutoWanderController"/>는 순수 C# 클래스이고 난수를 <b>주입</b>받는다. 그래서
    /// <see cref="ConstantRandom"/>(모든 뽑기가 같은 값 r을 돌려준다)을 꽂고 <b>r을 이분 탐색</b>하면,
    /// «걷는다 → 안 걷는다»가 뒤집히는 지점이 곧 그 순간 <see cref="AutoWanderController"/>가
    /// <b>실제로 읽은 걷기 확률</b>이다(<c>ResolvePostIdleBranch</c>의 <c>roll &lt; walkChance</c>).
    /// 확률 값을 테스트가 다시 계산하지 않으므로 «생성기와 검사기가 같이 틀리는» 형태를 구조적으로 피한다
    /// (docs/TEAM.md 「거짓 통과 신형」).</para>
    ///
    /// <para><b>플랫폼</b>: 중립. 판정은 <c>Platform/ViewerPresence.cs</c>(플랫폼 중립)에 있고 이 파일은
    /// 자산을 <b>읽기만</b> 한다(절대 불변 원칙 3). macOS/Windows 영향은 동일하다 — 플랫폼별로 다른 것은
    /// <c>SecondsSinceUserInput</c>을 <b>채우는 쪽</b>뿐이며 그 패리티는
    /// <c>PlatformParityAuditTests</c>가 따로 본다.</para>
    /// </summary>
    public sealed class AwayWanderReductionTests
    {
        private const string LogPrefix = "[자리비움배회]";
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";
        private const string WanderSourceRelativePath = "_Project/Scripts/States/AutoWanderController.cs";

        // ====================================================================
        // 주입 창구 — FramePacing.LastPresence 를 세운다
        // ====================================================================
        // ★ 지금 프로덕션에는 SetPresenceForTests(...) 같은 전용 창구가 없다. 그래서 private static
        //   필드를 리플렉션으로 세우되, **공개 창구(FramePacing.LastPresence)로 되읽어 확인**한다.
        //   필드 이름이 바뀌면 GetField가 null을 돌려주고 아래 Assert가 빨개진다 — 니들이 죽으면
        //   조용히 초록이 되는 형태(CLAUDE.md 「부재 단언」)를 여기서 구조적으로 막는다.
        //   (FramePacing 은 internal 이지만 이 어셈블리에는 Scripts/AssemblyInfo.cs 의
        //    InternalsVisibleTo("StickMate.Tests.EditMode") 가 있어 직접 볼 수 있다. PlayMode 에는
        //    그 선언이 없으므로 이 검증은 반드시 EditMode 여야 한다.)

        private const string PresenceFieldName = "_presence";

        [SetUp]
        public void SetUp() => FramePacing.ResetForTests();

        [TearDown]
        public void TearDown() => FramePacing.ResetForTests();

        private static ViewerPresenceSnapshot Presence(float idleSeconds, bool asleep = false,
            bool lowPower = false, bool onBattery = false, bool sessionLocked = false)
            => new ViewerPresenceSnapshot(asleep, idleSeconds, lowPower, onBattery, sessionLocked);

        /// <summary>OS 관측을 세우고 <b>공개 창구로 되읽어</b> 실제로 들어갔는지 확인한다.</summary>
        private static void InjectPresence(in ViewerPresenceSnapshot snapshot)
        {
            FieldInfo field = typeof(FramePacing).GetField(PresenceFieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field,
                $"{LogPrefix} FramePacing.{PresenceFieldName} 필드를 찾지 못했습니다 — 이름/형태가 바뀌었다면 " +
                "이 파일의 모든 «자리 비움» 시나리오는 관측이 비어 있는 채로 돌아 조용히 «평소 거동»만 " +
                "확인하게 됩니다(거짓 통과). 프로덕션에 SetPresenceForTests(in ViewerPresenceSnapshot) " +
                "창구를 만들고 이 헬퍼를 그쪽으로 바꾸십시오.");

            field.SetValue(null, snapshot);

            ViewerPresenceSnapshot readBack = FramePacing.LastPresence;
            Assert.AreEqual(snapshot.Valid, readBack.Valid,
                $"{LogPrefix} 주입한 관측이 FramePacing.LastPresence 로 되읽히지 않았습니다(Valid).");
            Assert.AreEqual(snapshot.SecondsSinceUserInput, readBack.SecondsSinceUserInput, 1e-4f,
                $"{LogPrefix} 주입한 무입력 시간이 되읽히지 않았습니다 — 창구가 죽었습니다.");
        }

        /// <summary>무입력 1시간. design-motion 이 요구한 «밤» 시나리오다.</summary>
        private static void InjectAwayOneHour() => InjectPresence(Presence(idleSeconds: 3600f));

        /// <summary>사용자가 방금 마우스를 움직였다 — 자리 비움이 아니다.</summary>
        private static void InjectPresent() => InjectPresence(Presence(idleSeconds: 0f));

        // ====================================================================
        // 상수 난수 — 모든 뽑기가 같은 값을 돌려준다
        // ====================================================================

        /// <summary>
        /// <see cref="AutoWanderController"/>는 난수를 <c>NextDouble()</c>로만 쓴다(실측: 그 파일의
        /// <c>_rng.</c> 호출 11곳 전부). 그래서 <b>모든 뽑기가 같은 값</b>을 돌려주게 만들면
        /// «몇 번째 뽑기인가»라는 취약한 전제 없이 결정론이 성립한다 — 내부 소비 순서가 바뀌어도
        /// 이 픽스처는 깨지지 않는다.
        /// </summary>
        private sealed class ConstantRandom : System.Random
        {
            private readonly double _value;
            public ConstantRandom(double value) => _value = value;
            public override double NextDouble() => _value;
            protected override double Sample() => _value;
        }

        // ====================================================================
        // 리그 — 블랙보드 + 부채꼴 위젯 + 집중 감시자
        // ====================================================================

        private sealed class Rig : IDisposable
        {
            public GameObject Host;
            public StickConfig Config;
            public StickmanBlackboard Blackboard;
            public GearRadialMenuWidget RadialMenu;
            public FocusWatchDirector FocusDirector;

            public void Dispose()
            {
                if (Host != null) UnityEngine.Object.DestroyImmediate(Host);
                if (Config != null) UnityEngine.Object.DestroyImmediate(Config);
            }
        }

        /// <summary>배포 설정 자산(읽기 전용)을 찾는다.</summary>
        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 배포 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        /// <summary>
        /// 배포 자산의 <b>복제본</b>으로 리그를 세운다 — 원본 자산은 절대 고치지 않는다(원칙 3).
        /// 복제본을 쓰는 이유는 «코드 기본값이 아니라 실제로 출하되는 값»을 재기 위해서다
        /// (docs/TEAM.md 거짓 통과 9번: 애셋이 코드 기본값을 덮는데 애셋을 안 봐서 스위치가 꺼진 채
        /// 나갈 뻔했다).
        /// </summary>
        private static Rig BuildRig(string name)
        {
            var rig = new Rig
            {
                Host = new GameObject(name),
                Config = UnityEngine.Object.Instantiate(LoadDeployedConfig()),
            };
            var body = rig.Host.AddComponent<Rigidbody2D>();
            // 부채꼴/집중 감시자는 «미리» 붙여 둔다 — 블랙보드가 같은 GameObject 를 1회만 탐색하고
            // 결과를 캐싱하기 때문이다(StickmanBlackboard.IsRadialMenuOpen / IsFocusSessionActive).
            // 나중에 붙이면 캐시가 null 로 굳어 시나리오가 조용히 «메뉴 없음»으로 돈다.
            rig.RadialMenu = rig.Host.AddComponent<GearRadialMenuWidget>();
            rig.FocusDirector = rig.Host.AddComponent<FocusWatchDirector>();
            rig.Blackboard = new StickmanBlackboard { Config = rig.Config, Body = body };

            Assert.IsFalse(rig.Blackboard.IsRadialMenuOpen,
                $"{LogPrefix} 리그 초기 상태에서 부채꼴이 이미 «떠 있다»고 나옵니다 — 전제가 깨졌습니다.");
            Assert.IsFalse(rig.Blackboard.IsFocusSessionActive,
                $"{LogPrefix} 리그 초기 상태에서 집중 세션이 이미 켜져 있습니다 — 전제가 깨졌습니다.");
            return rig;
        }

        /// <summary>
        /// 부채꼴을 «떠 있는» 상태로 만든다. <b>내부 상태 이름을 문자열로 베끼지 않는다</b> —
        /// 비공개 단계 열거의 값을 하나씩 넣어 보고 <b>프로덕션 프로퍼티 <c>IsVisible</c> 자신</b>이
        /// 참이 되는 값을 고른다. 즉 판정의 오라클이 프로덕션이다.
        /// </summary>
        private static void OpenRadialMenu(Rig rig)
        {
            FieldInfo phaseField = null;
            foreach (FieldInfo f in typeof(GearRadialMenuWidget)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType.IsEnum) { phaseField = f; if (TrySetVisible(rig, f)) return; }
            }
            Assert.Fail($"{LogPrefix} 부채꼴을 «떠 있는» 상태로 만들지 못했습니다" +
                (phaseField == null
                    ? " — GearRadialMenuWidget 에 열거형 비공개 필드가 하나도 없습니다."
                    : $" — 마지막으로 시도한 필드는 {phaseField.Name}({phaseField.FieldType.Name})입니다.") +
                " 위젯의 가시성 표현이 바뀌었다면 사다리 우선순위 테스트는 «메뉴 없음»으로 조용히 " +
                "돌아 아무것도 증명하지 못합니다.");
        }

        private static bool TrySetVisible(Rig rig, FieldInfo enumField)
        {
            object original = enumField.GetValue(rig.RadialMenu);
            foreach (object candidate in Enum.GetValues(enumField.FieldType))
            {
                enumField.SetValue(rig.RadialMenu, candidate);
                if (rig.RadialMenu.IsVisible) return true;
            }
            enumField.SetValue(rig.RadialMenu, original);
            return false;
        }

        /// <summary>집중 세션을 켠다 — <c>FocusSessionAmbientTests</c>가 세운 어법 그대로다.</summary>
        private static void StartFocusSession(Rig rig)
        {
            PropertyInfo prop = typeof(FocusWatchDirector)
                .GetProperty(nameof(FocusWatchDirector.IsSessionActive));
            Assert.IsNotNull(prop, $"{LogPrefix} FocusWatchDirector.IsSessionActive 프로퍼티가 없습니다.");
            prop.SetValue(rig.FocusDirector, true);
            Assert.IsTrue(rig.FocusDirector.IsSessionActive,
                $"{LogPrefix} 집중 세션을 켜지 못했습니다 — 이 상태로는 아래 단언이 아무것도 증명하지 못합니다.");
        }

        // ====================================================================
        // 저울 — «컨트롤러가 실제로 읽은 걷기 확률»을 이분 탐색으로 되찾는다
        // ====================================================================

        /// <summary>난수가 항상 <paramref name="roll"/>일 때, Idle 한 구간이 끝난 뒤 실제로 걷기 시작하는가.</summary>
        private static bool WalksAt(Rig rig, double roll)
        {
            var wander = new AutoWanderController(rig.Blackboard, rig.Config, new ConstantRandom(roll));
            // 갓 만든 컨트롤러는 Resting 이고 _restTimer 가 0이므로, 이 값이 곧 «이번에 뽑힌 Idle 길이»다.
            float rest = wander.PlannedDwellRemainingSeconds;
            Assert.Greater(rest, 0f,
                $"{LogPrefix} 뽑힌 Idle 길이가 {rest:F4}초입니다 — 0 이하면 아래 한 번의 Tick 이 " +
                "«Idle 을 지나갔다»를 의미하지 않게 되어 저울이 무효입니다.");
            wander.Tick(rest + 0.001f);
            return Mathf.Abs(wander.MoveInputX) > 0.5f;
        }

        /// <summary>
        /// <b>컨트롤러가 이 상황에서 읽은 걷기 확률</b>. <c>ResolvePostIdleBranch</c>는
        /// <c>roll &lt; walkChance</c>로 갈라지므로, r을 이분 탐색해 뒤집히는 지점을 찾으면 그것이 곧
        /// 그 확률이다. <b>테스트가 확률을 다시 계산하지 않는다</b> — 값은 프로덕션에서만 온다.
        /// </summary>
        private static float MeasureWalkChance(Rig rig)
        {
            // r = 0 에서 안 걸으면 확률이 0 이하라는 뜻이다(부채꼴 사다리 칸).
            if (!WalksAt(rig, 0.0)) return 0f;
            Assert.IsFalse(WalksAt(rig, 1.0),
                $"{LogPrefix} r=1.0 에서도 걷습니다 — 걷기 확률이 1을 넘었다는 뜻이고, 이분 탐색의 " +
                "상한 전제가 깨집니다.");

            double lo = 0.0, hi = 1.0;
            for (int i = 0; i < 44; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (WalksAt(rig, mid)) lo = mid; else hi = mid;
            }
            return (float)(0.5 * (lo + hi));
        }

        /// <summary>«같은 난수로 뽑은 Idle 길이» — ★★★ 회귀 못이 재는 값.</summary>
        private static float DrawIdleDuration(Rig rig, double roll)
            => new AutoWanderController(rig.Blackboard, rig.Config, new ConstantRandom(roll))
                .PlannedDwellRemainingSeconds;

        /// <summary>«같은 난수로 정해진 두리번 유예» — 프로덕션 로그가 "그대로"라고 주장하는 두 번째 값.</summary>
        private static float DrawLookAroundCooldown(Rig rig, double roll)
        {
            var wander = new AutoWanderController(rig.Blackboard, rig.Config, new ConstantRandom(roll));
            float rest = wander.PlannedDwellRemainingSeconds;
            // Idle 구간 «안»에서 한 틱 — 두리번 지연은 지났고 Idle 은 아직 안 끝난 지점이라야 발동한다.
            wander.Tick(rest * 0.75f);
            Assert.AreEqual(1, wander.LookAroundRaisedCount,
                $"{LogPrefix} 두리번이 발동하지 않았습니다(발동 {wander.LookAroundRaisedCount}회, " +
                $"Idle {rest:F2}초의 75% 지점) — 유예 값을 잴 수 없으므로 이 대조는 무효입니다.");
            return wander.LookAroundCooldownRemaining;
        }

        // ====================================================================
        // (1) 순수 판정 경계 — AwaySeconds 를 참조해서 만든다(180을 베끼지 않는다)
        // ====================================================================

        [Test]
        public void 자리비움_판정은_AwaySeconds_경계에서_갈린다()
        {
            const float epsilon = 0.1f;
            float threshold = FramePacingPolicy.AwaySeconds;

            Assert.Greater(threshold, epsilon * 10f,
                $"{LogPrefix} AwaySeconds 가 {threshold}로 너무 작아 ±{epsilon}초 경계 표본이 의미를 " +
                "잃었습니다 — 이 테스트의 해상도를 다시 잡아야 합니다.");

            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(Presence(threshold - epsilon)),
                $"{LogPrefix} 무입력 {threshold - epsilon:F1}초(문턱 {threshold:F0} 미만)인데 자리 비움으로 " +
                "판정했습니다 — 사용자가 아직 보고 있는 시간에 캐릭터가 조용해집니다.");
            Assert.IsTrue(FramePacingPolicy.IsViewerLikelyAway(Presence(threshold)),
                $"{LogPrefix} 무입력이 정확히 문턱({threshold:F0}초)인데 자리 비움이 아니라고 합니다 — " +
                "판정이 >= 가 아니라 > 로 바뀌었는지 확인하십시오.");
            Assert.IsTrue(FramePacingPolicy.IsViewerLikelyAway(Presence(threshold + epsilon)),
                $"{LogPrefix} 무입력 {threshold + epsilon:F1}초인데 자리 비움이 아닙니다.");

            // «알 수 없음»(음수)은 절대 자리 비움이 아니다 — 관측이 없으면 평소대로 걷는 쪽이 안전하다.
            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(Presence(-1f)),
                $"{LogPrefix} 무입력 시간을 «알 수 없음»(-1)으로 보고했는데 자리 비움으로 판정했습니다.");

            // 관측 실패(기본값 구조체) — 다만 이 표본은 무입력이 0이라 «Valid 가 막았는가»를
            // 「0 < 문턱」과 구분하지 못한다. 그 구분은 아래 별도 테스트가 맡는다.
            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(default),
                $"{LogPrefix} 관측 실패(Valid=false) 스냅샷을 자리 비움으로 판정했습니다.");

            Debug.Log($"{LogPrefix} 경계 확인 — 문턱 {threshold:F0}초 기준 " +
                $"{threshold - epsilon:F1}=거짓 / {threshold:F1}=참 / {threshold + epsilon:F1}=참, " +
                "«알 수 없음»(-1)과 관측 실패는 둘 다 거짓.");
        }

        /// <summary>
        /// ★ 위 테스트의 <c>default</c> 표본이 못 가르는 축 하나를 따로 못박는다 —
        /// <b><c>Valid=false</c>는 무입력이 아무리 길어도 거짓인가.</b>
        ///
        /// <para>공개 생성자는 <c>Valid=true</c>만 만들 수 있으므로, 생성자로 만든 스냅샷의 <c>Valid</c>를
        /// 리플렉션으로 내린다. <b>내려갔는지 먼저 확인하고</b> 본 단언으로 간다 — 그래야 위조가 실패했을 때
        /// 조용히 초록이 되지 않는다.</para>
        ///
        /// <para>★ <b>런타임이 <c>readonly</c> 필드 쓰기를 거부하면 «건너뜀»이 아니라 «실패»로 낸다.</b>
        /// 처음에는 <c>Assert.Ignore</c>로 썼는데 <c>TestClaimExpiryAuditTests</c>가 그것을 잡았다 —
        /// 이 저장소에서 Ignore 는 <b>명부 등록 + 역방향 장치</b>가 있어야 하는 «미해결 갭»의 표시이고,
        /// 이 축은 갭이 아니다(현재 Mono 에서 위조가 <b>실제로 성공</b>한다 — 실행 결과 skipped=0).
        /// 즉 그 Ignore 는 <b>한 번도 안 도는 방어 코드</b>였고, 이 저장소가 반복해서 당한 «조용히 초록이
        /// 되는 경로»를 하나 더 만드는 일이었다. 그래서 지웠다.</para>
        /// </summary>
        [Test]
        public void 관측_실패는_무입력이_아무리_길어도_자리비움이_아니다()
        {
            float longIdle = FramePacingPolicy.AwaySeconds * 20f; // 문턱의 스무 배 = 하룻밤
            var valid = Presence(longIdle);

            // 양성 대조 — 같은 무입력 시간이 Valid=true 에서는 «참»이다. 이게 없으면 아래 거짓이
            // Valid 때문인지 무입력 시간 때문인지 구분할 수 없다.
            Assert.IsTrue(FramePacingPolicy.IsViewerLikelyAway(valid),
                $"{LogPrefix} 무입력 {longIdle:F0}초 · Valid=true 인데 자리 비움이 아니라고 합니다 — " +
                "이 대조가 깨지면 아래 «거짓»은 아무것도 증명하지 못합니다.");

            FieldInfo validField = typeof(ViewerPresenceSnapshot)
                .GetField(nameof(ViewerPresenceSnapshot.Valid));
            Assert.IsNotNull(validField,
                $"{LogPrefix} ViewerPresenceSnapshot.Valid 필드를 찾지 못했습니다.");

            object boxed = valid;
            try
            {
                validField.SetValue(boxed, false);
            }
            catch (Exception e)
            {
                Assert.Fail($"{LogPrefix} 이 런타임이 readonly 구조체 필드의 리플렉션 쓰기를 " +
                    $"거부했습니다({e.GetType().Name}: {e.Message}) — «Valid=false + 긴 무입력» 표본을 만들 수 " +
                    "없어 이 축을 **측정하지 못했습니다**(«통과»가 아닙니다).\n" +
                    "처방: ViewerPresenceSnapshot 에 테스트 전용 무효 팩토리(또는 " +
                    "internal 생성자)를 두고 이 테스트를 그쪽으로 옮기십시오. " +
                    "Ignore 로 덮지 마십시오 — 이 축은 «미해결 갭»이 아니라 «측정 수단 상실»이고, " +
                    "그 둘은 러너에서 다르게 생겨야 합니다.");
            }

            var forged = (ViewerPresenceSnapshot)boxed;
            Assert.IsFalse(forged.Valid,
                $"{LogPrefix} 위조가 실패했습니다 — Valid 가 여전히 참이라 아래 단언은 " +
                "«Valid 게이트»가 아니라 그냥 문턱 비교를 재게 됩니다(거짓 통과).");
            Assert.GreaterOrEqual(forged.SecondsSinceUserInput, FramePacingPolicy.AwaySeconds,
                $"{LogPrefix} 위조본의 무입력이 {forged.SecondsSinceUserInput:F0}초로 문턱 미만입니다 — " +
                "그러면 «거짓»의 이유가 Valid 인지 시간인지 갈리지 않습니다.");

            Assert.IsFalse(FramePacingPolicy.IsViewerLikelyAway(forged),
                $"{LogPrefix} 관측이 실패(Valid=false)했는데 무입력 {forged.SecondsSinceUserInput:F0}초만 보고 " +
                "자리 비움으로 판정했습니다 — 조회에 실패했을 때 남아 있던 쓰레기 값으로 캐릭터가 " +
                "조용해질 수 있습니다.");

            Debug.Log($"{LogPrefix} Valid 게이트 확인 — 무입력 {longIdle:F0}초에서 " +
                "Valid=true → 참 / Valid=false → 거짓(같은 시간, 같은 함수).");
        }

        // ====================================================================
        // (2) 배포 자산의 사다리가 단조 내림차순인가
        // ====================================================================

        [Test]
        public void 배포_자산의_걷기확률_사다리가_단조_내림차순이다()
        {
            StickConfig c = LoadDeployedConfig();

            Assert.Greater(c.awayWanderWalkChance, 0f,
                $"{LogPrefix} 배포 자산의 awayWanderWalkChance 가 {c.awayWanderWalkChance:0.###}입니다 — " +
                "0이면 밤새 «절대 안 걷는다»가 되어 파쿠르·뛰어내리기·매달리기가 구조적으로 도달 불가가 " +
                "됩니다(집중 세션 절이 0.40을 고른 것과 같은 이유). 기능을 끄려면 0이 아니라 음수 " +
                "센티널을 쓰십시오.");
            Assert.Less(c.awayWanderWalkChance, c.focusSessionWalkChance,
                $"{LogPrefix} 자리 비움({c.awayWanderWalkChance:0.###})이 집중 세션" +
                $"({c.focusSessionWalkChance:0.###})보다 조용하지 않습니다 — 사다리가 단조 내림차순이 " +
                "아니면 «두 조건이 겹칠 때 더 조용한 쪽이 자동으로 이긴다»는 설계 전제가 깨지고, " +
                "우선순위를 따로 판정하는 코드가 필요해집니다.");
            Assert.Less(c.focusSessionWalkChance, c.wanderPostIdleWalkChance,
                $"{LogPrefix} 집중 세션({c.focusSessionWalkChance:0.###})이 평소" +
                $"({c.wanderPostIdleWalkChance:0.###})보다 조용하지 않습니다.");
            Assert.LessOrEqual(c.wanderPostIdleWalkChance, 1f,
                $"{LogPrefix} 평소 걷기 확률이 1을 넘습니다({c.wanderPostIdleWalkChance:0.###}).");

            Debug.Log($"{LogPrefix} 사다리(배포 자산) — 부채꼴 0.00 > 자리비움 " +
                $"{c.awayWanderWalkChance:0.##} > 집중 {c.focusSessionWalkChance:0.##} > 평소 " +
                $"{c.wanderPostIdleWalkChance:0.##}. 단조 내림차순 확인.");
        }

        // ====================================================================
        // (3) ★ 양성 대조 + 네거티브 컨트롤 — 한 테스트 안에서
        // ====================================================================

        /// <summary>
        /// design-motion 지정 항목 2. <b>끄기 전에 먼저 켜져 있음을 보인다.</b>
        /// 부재 단언만 두면 «기능이 애초에 안 붙어 있었다»와 «껐더니 꺼졌다»가 똑같이 생긴다.
        /// </summary>
        [Test]
        public void 자리비움에서_전용_확률이_쓰이고_음수로_끄면_평소_확률로_돌아온다()
        {
            using (Rig rig = BuildRig(nameof(자리비움에서_전용_확률이_쓰이고_음수로_끄면_평소_확률로_돌아온다)))
            {
                float away = rig.Config.awayWanderWalkChance;
                float normal = rig.Config.wanderPostIdleWalkChance;
                Assert.AreNotEqual(away, normal,
                    $"{LogPrefix} 배포 자산에서 자리비움 확률과 평소 확률이 같습니다({away:0.###}) — " +
                    "그러면 이 테스트는 스위치가 죽어도 초록입니다.");

                // ── 전제: 관측을 넣기 전에는 자리 비움이 아니다(대조의 기준선).
                Assert.IsFalse(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 관측을 넣기도 전에 자리 비움이라고 합니다 — 전제가 깨졌습니다.");
                float before = MeasureWalkChance(rig);
                Assert.AreEqual(normal, before, 1e-4f,
                    $"{LogPrefix} 자리 비움이 아닌데 컨트롤러가 읽은 확률이 {before:0.####}입니다" +
                    $"(평소 {normal:0.###}) — 기준선부터 어긋났습니다.");

                // ── ★ 양성: 무입력 1시간이면 전용 확률이 «실제로» 쓰인다.
                InjectAwayOneHour();
                Assert.IsTrue(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 무입력 1시간인데 자리 비움 배회가 활성이 아닙니다 — " +
                    "블랙보드 ← FramePacing.LastPresence 배선이 끊겼습니다.");
                float measuredAway = MeasureWalkChance(rig);
                Assert.AreEqual(away, measuredAway, 1e-4f,
                    $"{LogPrefix} 자리 비움인데 컨트롤러가 읽은 걷기 확률이 {measuredAway:0.####}입니다" +
                    $"(기대 awayWanderWalkChance={away:0.###}). 사다리가 이 칸을 안 읽고 있습니다.");
                // NUnit 의 AreNotEqual 에는 허용오차 오버로드가 없다 — 차이를 직접 잰다.
                Assert.Greater(Mathf.Abs(normal - measuredAway), 1e-4f,
                    $"{LogPrefix} 자리 비움인데 여전히 평소 확률({normal:0.###})을 읽었습니다.");

                // ── ★ 네거티브 컨트롤: 음수 센티널로 끄면 «관측은 그대로인데» 평소 확률로 돌아온다.
                //    관측을 그대로 둔 채 스위치만 내리는 것이 핵심이다 — 관측까지 걷으면 무엇이
                //    되돌렸는지 갈리지 않는다.
                rig.Config.awayWanderWalkChance = -1f;
                Assert.IsTrue(FramePacingPolicy.IsViewerLikelyAway(FramePacing.LastPresence),
                    $"{LogPrefix} 스위치를 내렸더니 OS 관측까지 사라졌습니다 — 이 대조의 전제가 깨졌습니다.");
                Assert.IsFalse(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} awayWanderWalkChance 를 음수로 내렸는데 자리 비움 배회가 여전히 " +
                    "활성입니다 — «음수 = OFF 센티널» 계약이 깨졌습니다.");
                float measuredOff = MeasureWalkChance(rig);
                Assert.AreEqual(normal, measuredOff, 1e-4f,
                    $"{LogPrefix} 기능을 껐는데 컨트롤러가 읽은 확률이 {measuredOff:0.####}입니다" +
                    $"(기대 wanderPostIdleWalkChance={normal:0.###}) — «끄면 예전 그대로»가 거짓입니다. " +
                    "특히 음수가 그대로 확률로 쓰이면 캐릭터는 밤새 한 걸음도 안 걷습니다.");

                Debug.Log($"{LogPrefix} 스위치 확인 — 평소 {before:0.####} / 자리비움 {measuredAway:0.####} / " +
                    $"음수로 끈 뒤 {measuredOff:0.####}(관측은 무입력 " +
                    $"{FramePacing.LastPresence.SecondsSinceUserInput:F0}초 그대로).");
            }
        }

        // ====================================================================
        // (4) ★ 사다리 우선순위 — 두 조건이 «동시에» 성립할 때
        // ====================================================================

        [Test]
        public void 사다리_부채꼴이_자리비움을_이긴다()
        {
            using (Rig rig = BuildRig(nameof(사다리_부채꼴이_자리비움을_이긴다)))
            {
                InjectAwayOneHour();

                // 양성 대조 — 부채꼴을 열기 «전»에 자리 비움 칸이 실제로 잡혀 있다. 이게 없으면
                // 아래 0은 «부채꼴이 이겼다»가 아니라 «원래 안 걷고 있었다»와 구분되지 않는다.
                float awayOnly = MeasureWalkChance(rig);
                Assert.AreEqual(rig.Config.awayWanderWalkChance, awayOnly, 1e-4f,
                    $"{LogPrefix} 부채꼴을 열기 전 확률이 {awayOnly:0.####}입니다 — 자리 비움 칸이 " +
                    "안 잡혀 있어 아래 비교가 무효입니다.");

                OpenRadialMenu(rig);
                Assert.IsTrue(rig.Blackboard.IsRadialMenuHoldActive,
                    $"{LogPrefix} 부채꼴을 폈는데 제자리 대기가 활성이 아닙니다.");
                Assert.IsTrue(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 부채꼴을 편 뒤 자리 비움 판정이 사라졌습니다 — 두 조건이 «동시에» " +
                    "성립하는 상황을 재려는 이 테스트의 전제가 깨졌습니다.");

                float both = MeasureWalkChance(rig);
                Assert.AreEqual(0f, both, 1e-4f,
                    $"{LogPrefix} 부채꼴 + 자리 비움 동시 성립에서 걷기 확률이 {both:0.####}입니다(기대 0) — " +
                    "메뉴를 열어 둔 채 캐릭터가 걸어 나갑니다(2026-09-06 사용자 지시 위반).\n" +
                    "★ 이 축에는 잠금이 두 겹이다(둘 다 확인하십시오): ① ResolvePostIdleBranch 의 " +
                    "사다리 첫 칸(hold → 0), ② EnterMoving 진입부의 «걷기로 들어오는 모든 문» 가드. " +
                    "여기서 재는 것은 «화면에서 안 걷는다»라는 관찰 가능한 계약이므로 둘 중 하나만 " +
                    "남아도 초록이지만, 둘 다 빠지면 반드시 빨개집니다.");

                Debug.Log($"{LogPrefix} 사다리 (a) — 자리비움만 {awayOnly:0.##} → " +
                    $"부채꼴 동시 성립 {both:0.##}. 부채꼴이 이겼습니다.");
            }
        }

        [Test]
        public void 사다리_자리비움이_집중세션을_이긴다()
        {
            using (Rig rig = BuildRig(nameof(사다리_자리비움이_집중세션을_이긴다)))
            {
                StartFocusSession(rig);
                Assert.IsTrue(rig.Blackboard.IsFocusSessionAmbientActive,
                    $"{LogPrefix} 집중 세션을 켰는데 앰비언트가 활성이 아닙니다(마스터 스위치 확인).");

                // 양성 대조 — 자리 비움을 넣기 «전»에 집중 칸이 실제로 잡혀 있다. 이게 없으면 아래
                // 0.15는 «자리비움이 이겼다»가 아니라 «집중이 애초에 안 켜져 있었다»와 구분되지 않는다.
                Assert.IsFalse(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 관측을 넣기 전인데 자리 비움이라고 합니다 — 전제가 깨졌습니다.");
                float focusOnly = MeasureWalkChance(rig);
                Assert.AreEqual(rig.Config.focusSessionWalkChance, focusOnly, 1e-4f,
                    $"{LogPrefix} 집중 세션만 켠 상태의 확률이 {focusOnly:0.####}입니다" +
                    $"(기대 {rig.Config.focusSessionWalkChance:0.###}) — 집중 칸이 안 잡혀 있어 아래 " +
                    "비교가 무효입니다.");

                InjectAwayOneHour();
                Assert.IsTrue(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 무입력 1시간인데 자리 비움 배회가 활성이 아닙니다.");
                Assert.IsTrue(rig.Blackboard.IsFocusSessionAmbientActive,
                    $"{LogPrefix} 자리 비움을 넣었더니 집중 세션이 꺼졌습니다 — 두 조건이 «동시에» " +
                    "성립하는 상황을 재려는 이 테스트의 전제가 깨졌습니다.");

                float both = MeasureWalkChance(rig);
                Assert.AreEqual(rig.Config.awayWanderWalkChance, both, 1e-4f,
                    $"{LogPrefix} 집중 세션 + 자리 비움 동시 성립에서 걷기 확률이 {both:0.####}입니다" +
                    $"(기대 awayWanderWalkChance={rig.Config.awayWanderWalkChance:0.###}, " +
                    $"집중 세션 값은 {rig.Config.focusSessionWalkChance:0.###}). 사다리 순서가 뒤집혔습니다 — " +
                    "더 조용한 칸이 이겨야 «두 조건이 겹쳐도 우선순위 판정 코드가 필요 없다»는 설계가 성립합니다.");

                Debug.Log($"{LogPrefix} 사다리 (b) — 집중만 {focusOnly:0.##} → " +
                    $"자리비움 동시 성립 {both:0.##}. 자리비움이 이겼습니다.");
            }
        }

        // ====================================================================
        // (5) ★★★ 가장 중요한 회귀 못 — Idle 길이는 한 틱도 바뀌지 않는다
        // ====================================================================

        /// <summary>
        /// ★★★ <b>이 기능의 핵심 설계 제약.</b> 자리 비움에서 <c>wanderIdleDurationMin/Max</c>는
        /// <b>전혀</b> 바뀌지 않는다.
        ///
        /// <para><b>왜 이게 가장 위험한 지점인가</b>: 바로 앞 선례인 집중 세션이 «한 번 서면 더 오래
        /// 선다»를 위해 그 두 값을 2~6초 → 4~11초로 늘렸고, 그만큼 <b>복귀 지연</b>이 커졌다.
        /// 자리 비움에 같은 패턴을 반사적으로 복사하면 «돌아왔는데 캐릭터가 11초간 안 움직인다»가 된다.
        /// design-motion 은 확률만 낮추는 쪽을 택했고(복귀 지연 p99 13.37 → 13.79초, 사실상 무변화),
        /// 그 선택을 코드가 아니라 <b>여기서</b> 잠근다.</para>
        ///
        /// <para><b>대조 설계</b>: 같은 난수 r에서 «자리 비움 ON» 과 «OFF» 의 Idle 길이를 <b>정확히</b>
        /// 같은 값으로 요구하고, <b>같은 저울에 집중 세션을 올려 실제로 달라짐을 보인다</b>.
        /// 양성 대조가 없으면 «안 바뀌었다»와 «저울이 아무것도 못 잰다»가 똑같이 생긴다 —
        /// 이 저장소가 반복해서 당한 형태다.</para>
        /// </summary>
        [Test]
        public void 자리비움은_Idle_길이와_두리번_주기를_전혀_바꾸지_않는다()
        {
            using (Rig rig = BuildRig(nameof(자리비움은_Idle_길이와_두리번_주기를_전혀_바꾸지_않는다)))
            {
                var rolls = new List<double>();
                for (int i = 0; i <= 20; i++) rolls.Add(i / 20.0);

                // ── 기준선(자리 비움 아님)
                var baseline = new List<float>();
                var baselineCooldown = new List<float>();
                foreach (double r in rolls)
                {
                    baseline.Add(DrawIdleDuration(rig, r));
                    baselineCooldown.Add(DrawLookAroundCooldown(rig, r));
                }

                float lo = Mathf.Min(baseline.ToArray());
                float hi = Mathf.Max(baseline.ToArray());
                Assert.Greater(hi, lo,
                    $"{LogPrefix} 난수 {rolls.Count}표본이 전부 같은 Idle 길이({lo:F4}초)를 뽑았습니다 — " +
                    "이 저울이 난수에 반응하지 않는다는 뜻이고, 아래 «같다»는 아무것도 증명하지 못합니다.");
                Assert.That(lo, Is.GreaterThanOrEqualTo(
                        rig.Config.wanderIdleDurationMin * (1f - rig.Config.wanderDurationJitterRatio) - 1e-3f)
                    .And.LessThanOrEqualTo(rig.Config.wanderIdleDurationMax
                        * (1f + rig.Config.wanderDurationJitterRatio) + 1e-3f),
                    $"{LogPrefix} 뽑힌 Idle 길이 하한 {lo:F4}초가 설정 구간" +
                    $"({rig.Config.wanderIdleDurationMin:F2}~{rig.Config.wanderIdleDurationMax:F2}초 ± 지터 " +
                    $"{rig.Config.wanderDurationJitterRatio:P0}) 밖입니다 — 저울이 엉뚱한 값을 읽고 있습니다.");

                // ── ★ 본 단언: 자리 비움을 켜도 같은 난수는 같은 Idle 길이를 뽑는다(비트 단위).
                InjectAwayOneHour();
                Assert.IsTrue(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 자리 비움을 못 켰습니다 — 이 상태로는 아래 «같다»가 그냥 " +
                    "«같은 조건을 두 번 잰 것»입니다(거짓 통과).");

                for (int i = 0; i < rolls.Count; i++)
                {
                    float awayDuration = DrawIdleDuration(rig, rolls[i]);
                    Assert.AreEqual(baseline[i], awayDuration, 0f,
                        $"{LogPrefix} ★★★ 자리 비움에서 Idle 길이가 {baseline[i]:F4}초 → {awayDuration:F4}초로 " +
                        $"바뀌었습니다(난수 r={rolls[i]:F2}).\n" +
                        "이 기능은 «걷기 확률만» 낮춥니다. Idle 길이를 늘리면 사용자가 돌아온 뒤 캐릭터가 " +
                        "그만큼 더 오래 굳어 있고, 그게 집중 세션(2~6 → 4~11초)에서 이미 나온 복귀 지연 " +
                        "문제입니다. 자리 비움은 «사용자가 없는 시간»이라 그 대가가 더 나쁩니다 — " +
                        "지연이 끝나는 시점이 곧 사용자가 돌아온 시점이기 때문입니다.\n" +
                        "AutoWanderController.EnterResting() 에 away 분기를 넣었다면 되돌리고, 정말 " +
                        "필요하면 design-motion + 리더 판정을 받으십시오(이 테스트를 지우는 것이 아니라).");

                    float awayCooldown = DrawLookAroundCooldown(rig, rolls[i]);
                    Assert.AreEqual(baselineCooldown[i], awayCooldown, 0f,
                        $"{LogPrefix} 자리 비움에서 두리번 유예가 {baselineCooldown[i]:F2}초 → " +
                        $"{awayCooldown:F2}초로 바뀌었습니다(난수 r={rolls[i]:F2}) — 프로덕션 로그가 " +
                        "«Idle 길이·두리번 주기·경계 행동은 그대로»라고 사용자에게 말하고 있는데 그게 " +
                        "거짓이 됩니다.");
                }

                // ── ★ 양성 대조: 같은 저울에 집중 세션을 올리면 «실제로» 달라진다.
                //    이게 없으면 위 «전부 같다»는 저울이 죽어 있어도 초록이다.
                FramePacing.ResetForTests();
                Assert.IsFalse(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 관측을 걷었는데 자리 비움이 남아 있습니다 — 대조가 오염됩니다.");
                StartFocusSession(rig);
                Assert.IsTrue(rig.Blackboard.IsFocusSessionAmbientActive,
                    $"{LogPrefix} 집중 세션을 못 켰습니다 — 양성 대조가 성립하지 않습니다.");

                int changedDurations = 0, changedCooldowns = 0;
                float focusLo = float.MaxValue, focusHi = float.MinValue;
                for (int i = 0; i < rolls.Count; i++)
                {
                    float d = DrawIdleDuration(rig, rolls[i]);
                    focusLo = Mathf.Min(focusLo, d);
                    focusHi = Mathf.Max(focusHi, d);
                    if (!Mathf.Approximately(d, baseline[i])) changedDurations++;
                    if (!Mathf.Approximately(DrawLookAroundCooldown(rig, rolls[i]), baselineCooldown[i]))
                        changedCooldowns++;
                }

                Assert.AreEqual(rolls.Count, changedDurations,
                    $"{LogPrefix} 양성 대조 실패 — 집중 세션을 켰는데 Idle 길이가 바뀐 표본이 " +
                    $"{changedDurations}/{rolls.Count}뿐입니다. 이 저울은 «Idle 길이 변경»을 감지하지 " +
                    "못하므로, 위의 «자리 비움에서 안 바뀌었다» 전부가 무효입니다.");
                Assert.Greater(changedCooldowns, 0,
                    $"{LogPrefix} 양성 대조 실패 — 집중 세션에서 두리번 유예가 한 표본도 안 바뀌었습니다" +
                    "(집중 28초 / 평소 30초). 유예 저울이 죽어 있어 위 «안 바뀌었다»가 무효입니다.");

                Debug.Log($"{LogPrefix} ★★★ Idle 길이 불변 확인 — 난수 {rolls.Count}표본에서 " +
                    $"평소 {lo:F2}~{hi:F2}초 · 자리비움 {lo:F2}~{hi:F2}초(비트 단위 동일). " +
                    $"양성 대조: 집중 세션은 {focusLo:F2}~{focusHi:F2}초로 {changedDurations}/{rolls.Count} " +
                    $"표본이 달라졌고 두리번 유예도 {changedCooldowns}표본이 달라졌습니다 — 저울은 살아 있습니다.");
            }
        }

        /// <summary>
        /// ★★★ 위 회귀 못의 <b>구조</b> 쪽 절반 — 애초에 «자리 비움용 Idle 길이 손잡이»가
        /// <c>StickConfig</c>에 생기지 못하게 한다. 다음 사람이 집중 세션 패턴을 복사한다면
        /// <c>awayWanderIdleDurationMin/Max</c>를 추가하는 것이 첫 걸음이다.
        ///
        /// <para><b>부재 단언이 아니라 «정확한 집합» 단언이다</b> — «away 로 시작하는 필드가 없다»로
        /// 쓰면 명명 규칙이 바뀐 날 조용히 초록이 된다. 그래서 «정확히 이 하나»를 요구한다:
        /// 그 하나가 사라져도 빨개지고, 하나가 늘어도 빨개진다.</para>
        /// </summary>
        [Test]
        public void StickConfig에_자리비움_손잡이는_걷기확률_하나뿐이다()
        {
            var found = new List<string>();
            foreach (FieldInfo f in typeof(StickConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name.StartsWith("away", StringComparison.OrdinalIgnoreCase)) found.Add(f.Name);
            }
            found.Sort(StringComparer.Ordinal);

            CollectionAssert.AreEqual(new[] { nameof(StickConfig.awayWanderWalkChance) }, found,
                $"{LogPrefix} StickConfig 의 «away*» 손잡이가 [{string.Join(", ", found)}]입니다.\n" +
                $"기대는 {nameof(StickConfig.awayWanderWalkChance)} 하나뿐입니다.\n" +
                "· 하나가 늘었다면: 자리 비움에 Idle 길이/두리번 주기 같은 두 번째 축을 붙이려는 " +
                "것입니다. 그건 집중 세션이 이미 복귀 지연으로 대가를 치른 길이고, design-motion 이 " +
                "명시적으로 배제한 설계입니다 — 리더 판정을 받으십시오.\n" +
                "· 하나가 사라졌다면: 명명이 바뀌었다는 뜻이고, 이 검사와 위 Idle 길이 회귀 못이 " +
                "함께 눈이 멉니다.");

            Debug.Log($"{LogPrefix} 구조 확인 — StickConfig 의 자리비움 손잡이는 " +
                $"{string.Join(", ", found)} 하나뿐입니다(Idle 길이 축 없음).");
        }

        /// <summary>
        /// ★ <c>EnterResting()</c>(= Idle 길이를 뽑는 유일한 자리)이 자리 비움 판정을 <b>읽지 않는가</b>.
        ///
        /// <para><b>니들 두 개를 쓰되 둘 다 «살아 있음»을 같은 테스트에서 증명한다</b>(CLAUDE.md 규약):
        /// 같은 추출기로 뽑은 <c>ResolvePostIdleBranch()</c>는 반대로 그 이름을 <b>반드시 포함</b>해야 한다.
        /// 추출기가 고장 나거나 이름이 바뀌면 그 양성 쪽이 먼저 빨개진다 — «본문을 못 찾아서 0건»이
        /// «안 읽어서 0건»으로 둔갑하는 형태를 구조적으로 막는다.</para>
        /// </summary>
        [Test]
        public void EnterResting은_자리비움_판정을_읽지_않는다()
        {
            string path = Path.Combine(Application.dataPath, WanderSourceRelativePath);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 원본을 찾지 못했습니다: {path}");
            string source = File.ReadAllText(path);

            const string needle = "IsViewerAwayForWander";
            Assert.IsTrue(source.Contains(needle, StringComparison.Ordinal),
                $"{LogPrefix} {WanderSourceRelativePath} 에 «{needle}» 이 한 번도 안 나옵니다 — " +
                "이름이 바뀌었거나 기능이 지워졌습니다. 어느 쪽이든 아래 «안 읽는다»는 아무것도 " +
                "증명하지 못합니다(죽은 니들).");

            string resting = ExtractMethodBody(source, "private void EnterResting()");
            string branch = ExtractMethodBody(source, "private void ResolvePostIdleBranch()");

            // ★ 양성 — 추출기가 «진짜 본문»을 잘라 왔다는 증거. 이게 먼저 통과해야 아래 0건이 값을 한다.
            StringAssert.Contains(needle, branch,
                $"{LogPrefix} ResolvePostIdleBranch() 본문에 «{needle}» 이 없습니다 — 사다리에서 자리 비움 " +
                "칸이 사라졌거나, 본문 추출기가 엉뚱한 구간을 잘랐습니다. 어느 쪽이든 아래 검사는 무효입니다.");

            Assert.IsFalse(resting.Contains(needle, StringComparison.Ordinal),
                $"{LogPrefix} ★★★ EnterResting() 이 «{needle}» 을 읽습니다 — Idle 길이가 자리 비움에 " +
                "따라 갈라진다는 뜻입니다. 이 기능은 걷기 확률만 낮춥니다(복귀 지연 근거는 " +
                "IsViewerAwayForWander 클래스 문서).\n본문:\n" + resting);
            Assert.IsFalse(resting.Contains("awayWander", StringComparison.OrdinalIgnoreCase),
                $"{LogPrefix} ★★★ EnterResting() 이 자리 비움 설정값을 직접 읽습니다.\n본문:\n" + resting);

            Debug.Log($"{LogPrefix} 소유권 확인 — «{needle}» 은 ResolvePostIdleBranch() 안에만 있고 " +
                $"EnterResting()({resting.Length}자) 안에는 0건입니다.");
        }

        /// <summary>여는 중괄호부터 짝이 맞는 닫는 중괄호까지 잘라 낸다(문자열/주석은 다루지 않는다 —
        /// 이 두 메서드에는 중괄호를 담은 리터럴이 없고, 그 전제가 깨지면 위 양성 단언이 먼저 빨개진다).</summary>
        private static string ExtractMethodBody(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0,
                $"{LogPrefix} 원본에서 «{signature}» 을 찾지 못했습니다 — 시그니처가 바뀌었다면 이 검사는 " +
                "아무것도 훑지 못하고 조용히 초록이 됩니다.");
            int open = source.IndexOf('{', at);
            Assert.GreaterOrEqual(open, 0, $"{LogPrefix} «{signature}» 뒤에 여는 중괄호가 없습니다.");

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            Assert.Fail($"{LogPrefix} «{signature}» 의 본문 끝을 찾지 못했습니다(중괄호 짝이 안 맞습니다).");
            return string.Empty;
        }

        // ====================================================================
        // (6) 안전한 실패 + 플랫폼 중립
        // ====================================================================

        /// <summary>
        /// OS 관측이 없으면(에디터·EditMode 테스트·<c>STICKMATE_ADAPTIVE_PACING=0</c>) 이 기능은
        /// <b>조용히 꺼지고 평소대로 걷는다</b>. «모르면 평소대로»가 안전한 쪽이라는 설계를 잠근다 —
        /// 반대로 떨어지면 증상이 «캐릭터가 안 움직인다»가 되어 버그로 보인다.
        /// </summary>
        [Test]
        public void 관측이_없으면_기능이_조용히_꺼지고_평소대로_걷는다()
        {
            using (Rig rig = BuildRig(nameof(관측이_없으면_기능이_조용히_꺼지고_평소대로_걷는다)))
            {
                // 양성 대조 먼저 — 관측이 있으면 실제로 켜진다.
                InjectAwayOneHour();
                Assert.IsTrue(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 관측을 넣었는데 기능이 안 켜집니다 — 아래 «꺼짐»이 무엇 때문인지 " +
                    "갈리지 않습니다.");
                Assert.AreEqual(rig.Config.awayWanderWalkChance, MeasureWalkChance(rig), 1e-4f,
                    $"{LogPrefix} 관측이 있는데 전용 확률이 안 쓰였습니다 — 대조 전제가 깨졌습니다.");

                // 관측을 걷는다 = 에디터/테스트/적응형 꺼짐과 같은 상태.
                FramePacing.ResetForTests();
                Assert.IsFalse(FramePacing.LastPresence.Valid,
                    $"{LogPrefix} ResetForTests 후에도 관측이 유효하다고 나옵니다.");
                Assert.IsFalse(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 관측이 없는데 자리 비움이라고 합니다 — 관측 실패가 «자리 비움»으로 " +
                    "떨어지면 에디터·테스트·적응형 페이싱 OFF 실행에서 캐릭터가 이유 없이 조용해집니다.");

                float measured = MeasureWalkChance(rig);
                Assert.AreEqual(rig.Config.wanderPostIdleWalkChance, measured, 1e-4f,
                    $"{LogPrefix} 관측이 없는데 걷기 확률이 {measured:0.####}입니다" +
                    $"(기대 {rig.Config.wanderPostIdleWalkChance:0.###}).");

                // ★ 무입력 «미만»도 같은 결론이어야 한다(문턱 바로 아래).
                InjectPresence(Presence(FramePacingPolicy.AwaySeconds - 0.1f));
                Assert.IsFalse(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 무입력이 문턱 미만인데 자리 비움이라고 합니다.");
                InjectPresent();
                Assert.IsFalse(rig.Blackboard.IsViewerAwayWanderActive,
                    $"{LogPrefix} 방금 입력이 있었는데 자리 비움이라고 합니다.");

                Debug.Log($"{LogPrefix} 안전한 실패 확인 — 관측 없음 / 문턱 미만 / 방금 입력, 셋 다 " +
                    $"평소 확률 {rig.Config.wanderPostIdleWalkChance:0.##} 로 떨어집니다.");
            }
        }

        /// <summary>
        /// 판정이 <b>플랫폼 중립 위치</b>에만 있는가(CLAUDE.md: 정책은 <c>Platform/</c>, 플랫폼 전용
        /// 코드는 «사실 조회»만). 정책이 <c>Platform/MacOS/</c> 안으로 들어가면 Windows 가 물리적으로
        /// 호출할 수 없다 — <c>FullscreenSuspendPolicy.cs</c>가 실제로 그 사고를 냈다.
        ///
        /// <para><b>«0건»에 양성 대조를 붙인다</b>: 두 플랫폼 폴더를 실제로 훑었는지(파일이 0개가 아닌지)와,
        /// 같은 스캐너가 중립 파일에서는 실제로 히트를 낸다는 것을 먼저 보인다.</para>
        /// </summary>
        [Test]
        public void 자리비움_판정은_플랫폼_중립_파일에만_있다()
        {
            const string needle = nameof(FramePacingPolicy.IsViewerLikelyAway);
            string platformRoot = Path.Combine(Application.dataPath, "_Project/Scripts/Platform");

            string neutral = Path.Combine(platformRoot, "ViewerPresence.cs");
            Assert.IsTrue(File.Exists(neutral), $"{LogPrefix} 중립 파일을 찾지 못했습니다: {neutral}");
            StringAssert.Contains(needle, File.ReadAllText(neutral),
                $"{LogPrefix} 중립 파일 ViewerPresence.cs 에 «{needle}» 이 없습니다 — 판정이 옮겨졌거나 " +
                "이름이 바뀌었습니다. 어느 쪽이든 아래 «플랫폼 폴더에 0건»은 무의미해집니다.");

            foreach (string platformDir in new[] { "MacOS", "Windows" })
            {
                string dir = Path.Combine(platformRoot, platformDir);
                Assert.IsTrue(Directory.Exists(dir), $"{LogPrefix} 플랫폼 폴더가 없습니다: {dir}");
                string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
                Assert.Greater(files.Length, 0,
                    $"{LogPrefix} {platformDir} 폴더에서 .cs 를 한 개도 못 찾았습니다 — 스캐너가 아무것도 " +
                    "훑지 않았으므로 «0건»은 «깨끗함»을 뜻하지 않습니다(양성 대조 실패).");

                foreach (string file in files)
                {
                    Assert.IsFalse(File.ReadAllText(file).Contains(needle, StringComparison.Ordinal),
                        $"{LogPrefix} 플랫폼 전용 파일이 판정을 직접 부릅니다: {Path.GetFileName(file)}.\n" +
                        "정책은 플랫폼 중립 위치에 한 곳만 두고, 플랫폼 코드는 «사실 조회»(무입력 시간을 " +
                        "채우는 것)만 합니다 — 정책이 한쪽 폴더로 들어가면 반대쪽 플랫폼이 물리적으로 " +
                        "호출할 수 없습니다(FullscreenSuspendPolicy.cs 사고 사례).");
                }
            }

            Debug.Log($"{LogPrefix} 플랫폼 중립 확인 — «{needle}» 은 Platform/ViewerPresence.cs 에만 있고 " +
                "MacOS/·Windows/ 폴더에는 0건입니다(두 폴더 모두 실제로 훑었습니다).");
        }
    }
}
