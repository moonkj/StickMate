using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★★ <b>L-3 — 몰입기 동안 <c>FramePacingTier.Still</c> 도달이 관측된다.</b>
    /// 사용자 PART2 성능 제약 4번(<i>"기존 FramePacing과 충돌없이 절감등급 정상동작 확인"</i>)의
    /// <b>유일한 직접 증거</b>이고, 계약서 9-3절이 P4의 통과 조건으로 지목한 항목이다.
    ///
    /// ============================================================================
    /// ★ 왜 GPU ms가 아닌가
    /// ============================================================================
    /// 이 앱의 GPU 비용은 «면적 × 제출 고정분»이라 <b>콘텐츠와 무관</b>하다(<c>game-architect</c> 반증).
    /// 합격선을 «GPU ms 감소»로 잡으면 0이 나오는데 그건 실패가 아니라 <b>측정 한계</b>다.
    /// 그래서 잴 것은 <b>등급 그 자체</b>다.
    ///
    /// ============================================================================
    /// 이 리그가 성립시키는 것 — <b>Still 말고는 내려갈 길이 없게 만든다</b>
    /// ============================================================================
    /// 관측 서비스를 «지금 사람이 보고 있다»(무입력 0초 · 화면 켜짐 · 저전력 아님)로 고정한다.
    /// 그러면 <see cref="FramePacingPolicy.DecideTier"/>의 다른 절감 경로가 <b>구조적으로 전부 막힌다</b>:
    /// <list type="bullet">
    ///   <item><c>DisplayOff</c> — 화면이 안 꺼졌다.</item>
    ///   <item><c>Away</c> — 무입력 0초 &lt; <see cref="FramePacingPolicy.AwaySeconds"/>.</item>
    ///   <item><c>Calm</c> — 무입력 0초 &lt; <see cref="FramePacingPolicy.RecentInputSeconds"/>.</item>
    /// </list>
    /// <b>남는 유일한 길이 <c>Still</c>이다</b> — 그리고 그것은 «캐릭터가 오래 <c>Idle</c>이다»에서만
    /// 나온다. 즉 여기서 <c>Still</c>이 관측되면 그 원인은 <b>코스튬 몰입기 동안 캐릭터가 계속
    /// 제자리에 서 있었다</b>는 사실 하나다(계약서 4-1절: 새 상태 ID를 만들었다면 세션 내내
    /// <c>Active</c>로 묶여 제출이 정확히 2배가 됐을 것이다).
    /// <b>Calm이 한 번이라도 보이면 이 리그가 안 먹은 것이므로 그 회차는 무효다</b> — 아래가 그것도 잰다.
    ///
    /// ============================================================================
    /// ★ 왜 리플렉션인가 (그리고 그 프로브가 살아 있음을 어떻게 증명하는가)
    /// ============================================================================
    /// <c>FramePacing</c>은 <c>internal</c>이고 <c>Scripts/AssemblyInfo.cs</c>의
    /// <c>InternalsVisibleTo</c>는 <b>EditMode 하나뿐</b>이다(이 어셈블리의 공통 사정).
    /// 그리고 등급은 <b>벽시계 지속</b>에서 나오므로 EditMode에서는 잴 수 없다.
    /// 그래서 리플렉션을 쓰되, <see cref="계기_프로브가_살아있다_이름이_바뀌면_빨간불이_된다"/>가
    /// <b>본 검사보다 먼저</b> (가) 찾아야 할 것을 전부 찾았고 (나) <b>없는 이름은 못 찾는지</b>를 증명한다.
    /// 그 음성 대조가 없으면 «이름이 바뀌어 null이 된 프로브»와 «정상 프로브»가 똑같이 생긴다.
    ///
    /// ============================================================================
    /// ★ 실기 배선과의 차이 — 정직하게 적는다
    /// ============================================================================
    /// <c>FramePacing.Tick</c>은 <b>실제 플레이어에서만</b> 불린다
    /// (<c>Mac/WindowsOverlayStateEnforcer</c>는 <c>UNITY_STANDALONE_* &amp;&amp; !UNITY_EDITOR</c>
    /// 경로에서만 생성된다 — 그쪽 클래스 문서). 그래서 이 테스트가 <b>그 한 줄을 대신 부른다</b>:
    /// <c>Tick(ResolveCharacterIdle(agent))</c>. 인자도 함수도 Enforcer와 <b>같은 것</b>이고,
    /// 「출하 코드가 정말 그렇게 부르는가」는 EditMode 소스 감사가 따로 지킨다
    /// (<c>StillTierCompositorBudgetTests</c>가 <c>DecideTier</c> 인자를 이미 잠그고 있다).
    /// <b>여기서 재는 것은 «그 입력이 주어졌을 때 등급이 실제로 내려가는가»다.</b>
    ///
    /// <para>★ <b>Windows 실기는 이 머신에서 못 잰다</b> — 계약서 12절 U-D가 «1차 출시 플랫폼인
    /// Windows에서도 재야 한다»고 못박았고, 그건 별도 배정 항목이다.</para>
    /// </summary>
    public sealed class CostumeFocusStillTierTests
    {
        private const string LogPrefix = "[코스튬절감-TEST]";

        private const string PacingTypeName = "StickMate.Platform.FramePacing";
        private const string MissingTypeName = "StickMate.Platform.FramePacingNoSuchTypeForNegativeControl";
        private const string MissingMemberName = "CurrentTierNoSuchMemberForNegativeControl";

        /// <summary>
        /// ★ <b>세션 시간을 압축하지 않는다(1배)</b>. 다른 코스튬 픽스처는 압축하지만 여기서는 안 한다 —
        /// 등급 문턱(<c>StillDwellSeconds</c>)과 관측 폴링은 <c>Time.unscaledDeltaTime</c>·
        /// <c>Time.unscaledTime</c>으로 도는데 세션 타이머는 <c>Time.deltaTime</c>으로 돈다.
        /// 압축하면 <b>둘의 비율이 달라져</b> 몰입기의 벽시계 길이가 문턱에 가까워지고,
        /// 그러면 «도달 못 함»과 «시간이 모자람»을 구별할 수 없게 된다.
        /// 대가는 벽시계 약 40초이며 <see cref="AssertBudgetCoversThreshold"/>가 그 여유를 검산한다.
        /// </summary>
        private const float TimeCompression = 1f;

        /// <summary>몰입기에 들어선 뒤 캐릭터가 <c>Idle</c>이 되기를 기다리는 <b>벽시계</b> 상한(초).
        /// <para>배회 확률 0은 <b>다음</b> 분기부터 적용되므로, 경계 프레임에 걷고 있었다면 그 에피소드가
        /// 끝날 때까지는 걷는다(설계가 «몰입기에는 안 걷는다»로 적은 것의 실제 경계). 그 구간은
        /// L-3의 대상이 아니다 — 여기서 재는 것은 «서 있게 된 뒤 절감 등급이 실제로 내려가는가»다.</para></summary>
        private const float IdleWaitBudgetSeconds = 6f;

        private const BindingFlags StaticAny =
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private float _savedTimeScale = 1f;
        private int _savedVSync;
        private int _savedTargetFrameRate;
        private int _savedRenderInterval;
        private bool _pacingTouched;

        // ====================================================================
        // 리플렉션 프로브
        // ====================================================================

        private static Type PacingType()
        {
            Type t = typeof(FramePacingTier).Assembly.GetType(PacingTypeName, throwOnError: false);
            Assert.IsNotNull(t,
                $"{LogPrefix} '{PacingTypeName}' 타입을 못 찾았습니다 — 이름이 바뀌었거나 어셈블리가 갈렸습니다. " +
                "이 상태에서 아래 판정은 «등급이 안 내려갔다»가 아니라 «아무것도 못 읽었다»입니다.");
            return t;
        }

        private static MethodInfo Method(Type t, string name)
        {
            MethodInfo m = t.GetMethod(name, StaticAny);
            Assert.IsNotNull(m, $"{LogPrefix} {PacingTypeName}.{name}(...)을 못 찾았습니다 — 이름이 바뀌었습니다.");
            return m;
        }

        private static PropertyInfo Property(Type t, string name)
        {
            PropertyInfo p = t.GetProperty(name, StaticAny);
            Assert.IsNotNull(p, $"{LogPrefix} {PacingTypeName}.{name}을 못 찾았습니다 — 이름이 바뀌었습니다.");
            return p;
        }

        private static FieldInfo Field(Type t, string name)
        {
            FieldInfo f = t.GetField(name, StaticAny);
            Assert.IsNotNull(f, $"{LogPrefix} {PacingTypeName}.{name} 필드를 못 찾았습니다 — 이름이 바뀌었습니다.");
            return f;
        }

        private static float StillDwellSeconds()
            => Convert.ToSingle(Field(PacingType(), "StillDwellSeconds").GetRawConstantValue());

        private static FramePacingTier CurrentTier()
            => (FramePacingTier)Property(PacingType(), "CurrentTier").GetValue(null);

        /// <summary>«지금 사람이 이 화면을 보고 있다»로 고정한 관측. 이 리그의 심장이다.</summary>
        private sealed class ViewerPresentStub : IViewerPresenceService
        {
            public bool TryGetPresence(out ViewerPresenceSnapshot snapshot)
            {
                snapshot = new ViewerPresenceSnapshot(
                    displayAsleep: false, secondsSinceUserInput: 0f,
                    lowPowerMode: false, onBattery: false, sessionLocked: false);
                return true;
            }
        }

        // ====================================================================
        // 정리
        // ====================================================================

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_pacingTouched)
            {
                try { Method(PacingType(), "ResetForTests").Invoke(null, null); }
                catch (Exception e) { Debug.LogWarning($"{LogPrefix} FramePacing 초기화 실패 — {e.GetType().Name}"); }

                QualitySettings.vSyncCount = _savedVSync;
                Application.targetFrameRate = _savedTargetFrameRate;
                OnDemandRendering.renderFrameInterval = _savedRenderInterval;
                _pacingTouched = false;
            }
            CostumeFocusRig.RestoreGlobals(_savedTimeScale);
            yield return null;
        }

        // ====================================================================
        // ★ 프로브 자기검증 — 본 검사보다 먼저 읽어야 한다
        // ====================================================================

        /// <summary>
        /// 리플렉션이 <b>실제로 무는지</b>를 양성·음성 양쪽으로 증명한다.
        /// 이게 초록이 아니면 아래 L-3의 결과는 «등급»이 아니라 «null»을 잰 것이다.
        /// </summary>
        [Test]
        public void 계기_프로브가_살아있다_이름이_바뀌면_빨간불이_된다()
        {
            Type t = PacingType();

            // 양성 — 필요한 것 전부가 실재한다.
            Assert.IsNotNull(Method(t, "ApplyOnce"));
            Assert.IsNotNull(Method(t, "Tick"));
            Assert.IsNotNull(Method(t, "ResetForTests"));
            Assert.IsNotNull(Method(t, "ResolveCharacterIdle"));
            Assert.IsNotNull(Property(t, "CurrentTier"));
            Assert.IsNotNull(Property(t, "IsApplied"));
            Assert.IsNotNull(Field(t, "StillDwellSeconds"));
            Assert.IsNotNull(Field(t, "_presenceService"));

            // 음성 — 없는 이름은 못 찾는다(프로브가 아무거나 돌려주지 않는다).
            Assert.IsNull(typeof(FramePacingTier).Assembly.GetType(MissingTypeName, throwOnError: false),
                $"{LogPrefix} 존재하지 않는 타입 '{MissingTypeName}'을 찾았습니다 — 프로브를 믿을 수 없습니다.");
            Assert.IsNull(t.GetProperty(MissingMemberName, StaticAny),
                $"{LogPrefix} 존재하지 않는 멤버 '{MissingMemberName}'을 찾았습니다 — 프로브를 믿을 수 없습니다.");

            // 등급 이름표 — «Still»이 실재하는 값인지 먼저 못박는다(니들 존재 단언).
            string[] names = Enum.GetNames(typeof(FramePacingTier));
            CollectionAssert.Contains(names, FramePacingTier.Still.ToString(),
                $"{LogPrefix} FramePacingTier에 Still이 없습니다 — L-3이 겨냥할 등급이 사라졌습니다.");

            float dwell = StillDwellSeconds();
            Assert.Greater(dwell, 0f, $"{LogPrefix} StillDwellSeconds가 {dwell}입니다.");

            Debug.Log($"{LogPrefix} 프로브 자기검증 통과 — 등급 {names.Length}종 " +
                $"[{string.Join(", ", names)}], Still 문턱 {dwell:F2}초, " +
                $"Away {FramePacingPolicy.AwaySeconds:F0}초 / 최근입력 {FramePacingPolicy.RecentInputSeconds:F0}초.");
        }

        // ====================================================================
        // ★★ L-3 본 검사
        // ====================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 몰입기_동안_절감등급_Still에_도달한다()
        {
            float dwell = StillDwellSeconds();

            yield return CostumeFocusRig.LoadSceneAndSettle();

            StickmanAgent agent = CostumeFocusRig.Agent();
            StickmanBlackboard bb = agent.Blackboard;
            FocusWatchDirector director = CostumeFocusRig.Director();
            CostumePropRenderer prop = CostumeFocusRig.EnsureCostumeProp(agent);
            CostumeFocusRig.WearOfficeCostume(agent.Config);

            _savedTimeScale = Time.timeScale;
            Time.timeScale = TimeCompression;

            yield return CostumeFocusRig.StartAndReachImmersion(director, prop, TimeCompression);

            float immersionWall = CostumeFocusRig.ImmersionWallSeconds(
                director.SessionDurationSeconds, TimeCompression);
            AssertBudgetCoversThreshold(immersionWall, dwell);

            // 경계 프레임에 걷고 있었으면 그 에피소드가 끝날 때까지 기다린다(위 상수 문서).
            yield return CostumeFocusRig.WaitForGroundedIdle(agent, IdleWaitBudgetSeconds);

            // ---- 몰입기 안에서 페이싱을 «갓 시작»시킨다 ----
            // 그래야 관측되는 Still이 적응기에서 물려받은 것이 아니라 <b>몰입기 안에서 벌어들인</b>
            // 것이 된다(dwell 누적이 0에서 다시 시작한다).
            Type pacing = PacingType();
            _savedVSync = QualitySettings.vSyncCount;
            _savedTargetFrameRate = Application.targetFrameRate;
            _savedRenderInterval = OnDemandRendering.renderFrameInterval;
            _pacingTouched = true;

            Method(pacing, "ResetForTests").Invoke(null, null);
            try
            {
                Method(pacing, "ApplyOnce").Invoke(null, new object[] { agent.Config });
            }
            catch (TargetInvocationException e)
            {
                Assert.Fail($"{LogPrefix} FramePacing.ApplyOnce가 던졌습니다 — {e.InnerException?.GetType().Name}: " +
                    $"{e.InnerException?.Message}\n" +
                    "활성 빌드 타깃이 이 머신과 반대편(Windows)이면 플랫폼 관측 서비스가 그쪽 네이티브를 " +
                    "부르려다 여기서 죽습니다. 그 회차에는 L-3를 잴 수 없습니다 — 타깃을 되돌린 뒤 다시 재십시오.");
            }

            Assert.IsTrue((bool)Property(pacing, "IsApplied").GetValue(null),
                $"{LogPrefix} ApplyOnce 뒤에도 IsApplied가 거짓입니다 — 설정이 null이었습니다.");

            // ★ 관측을 «사람이 보고 있다»로 고정 — Still 말고는 내려갈 길이 없어진다.
            Field(pacing, "_presenceService").SetValue(null, new ViewerPresentStub());

            MethodInfo tick = Method(pacing, "Tick");
            MethodInfo resolveIdle = Method(pacing, "ResolveCharacterIdle");
            object[] agentArg = { agent };

            // ---- 음성 기준선: 아직 오래 서 있지 않았으므로 Active여야 한다 ----
            tick.Invoke(null, new object[] { resolveIdle.Invoke(null, agentArg) });
            Assert.AreEqual(FramePacingTier.Active, CurrentTier(),
                $"{LogPrefix} 첫 Tick 직후 등급이 {CurrentTier()}입니다 — 기준선은 Active여야 합니다. " +
                "여기서 이미 절감 등급이면 아래 «Still 도달»은 이 라운드가 만든 것이 아닙니다.");

            // ---- 관측 ----
            // 관측 예산 = 문턱의 3배. 위 AssertBudgetCoversThreshold가 «Idle 대기 + 이 예산»이
            // 몰입기 안에 들어간다는 것을 이미 검산했으므로, 여기서 시간이 모자랄 수는 없다.
            float budget = 3f * dwell;
            var tiersSeen = new HashSet<FramePacingTier>();
            bool stillSeen = false, leftIdle = false, leftImmersion = false, propLost = false;
            float stillAt = -1f;
            int poseWritesAtStart = bb.CostumePoseWriteCount;
            int poseWritesAtStill = -1;
            int frames = 0;
            float lastElapsed = 0f;

            yield return TestClock.SampleForSeconds(budget, t =>
            {
                lastElapsed = t;
                frames++;

                bool idle = (bool)resolveIdle.Invoke(null, agentArg);
                tick.Invoke(null, new object[] { idle });

                if (!idle) leftIdle = true;
                if (director.CurrentPhase != FocusSessionPhase.Immersion) { leftImmersion = true; return false; }
                if (!bb.IsCostumeImmersionActive) propLost = true;

                FramePacingTier tier = CurrentTier();
                tiersSeen.Add(tier);
                if (tier == FramePacingTier.Still && !stillSeen)
                {
                    stillSeen = true;
                    stillAt = t;
                    poseWritesAtStill = bb.CostumePoseWriteCount;
                }
                return true;
            });

            var seen = new List<string>();
            foreach (FramePacingTier tier in tiersSeen) seen.Add(tier.ToString());
            seen.Sort(StringComparer.Ordinal);

            Debug.Log($"{LogPrefix} 몰입기 관측 — 벽시계 {lastElapsed:F2}초 / {frames}프레임 " +
                $"(예산 {budget:F2}초, 몰입기 길이 {immersionWall:F2}초, Still 문턱 {dwell:F2}초). " +
                $"본 등급 [{string.Join(", ", seen)}], Still 도달 {(stillSeen ? $"{stillAt:F2}초" : "없음")}, " +
                $"Idle 이탈 {leftIdle}, 몰입기 이탈 {leftImmersion}, 프롭 소실 {propLost}, " +
                $"코스튬 포즈 쓰기 {poseWritesAtStart} -> {bb.CostumePoseWriteCount}.");

            // ---- 리그가 실제로 먹었는가(먹지 않았으면 아래 판정은 다른 것을 잰 것이다) ----
            Assert.Greater(frames, 30,
                $"{LogPrefix} 표본이 {frames}프레임뿐입니다 — 관측 창이 어긋났습니다.");
            Assert.IsFalse(tiersSeen.Contains(FramePacingTier.Calm),
                $"{LogPrefix} Calm이 관측됐습니다 — 무입력 0초 고정이 안 먹었다는 뜻이고, " +
                "그러면 이 회차의 등급은 리그가 아니라 <b>이 기계의 실제 유휴 시간</b>이 만든 것입니다(측정 무효).");
            Assert.IsFalse(tiersSeen.Contains(FramePacingTier.Away),
                $"{LogPrefix} Away가 관측됐습니다 — 위와 같은 이유로 측정 무효입니다.");
            Assert.IsFalse(propLost,
                $"{LogPrefix} 관측 도중 프롭이 사라졌습니다 — 코스튬 층이 안 도는 구간을 잰 것입니다.");

            // ---- ★ 본 판정 ----
            Assert.IsTrue(stillSeen,
                $"{LogPrefix} 몰입기 {lastElapsed:F2}초(문턱 {dwell:F2}초의 " +
                $"{lastElapsed / Mathf.Max(0.001f, dwell):F1}배) 동안 Still에 <b>도달하지 못했습니다</b>. " +
                $"본 등급은 [{string.Join(", ", seen)}] · Idle 이탈 {leftIdle} · 몰입기 이탈 {leftImmersion}.\n" +
                "  · 몰입기 이탈이 참이면: 관측 창이 구간보다 길었습니다(예산 문제이지 회귀가 아닙니다).\n" +
                "  · Idle 이탈이 참이면: 몰입기 배회 확률 0(costumeImmersionWalkChance)이 깨졌거나 " +
                "코스튬 연출이 새 상태 ID를 쓰기 시작했습니다 — 계약서 4-1절이 경고한 그 형태이고, " +
                "그 순간 제출이 정확히 2배가 됩니다.\n" +
                "  · Idle 이탈이 거짓인데 Active로 남았으면: 등급 판정 입력 배선이 끊겼습니다.\n" +
                "  이것이 사용자 성능 제약 4번의 <b>유일한 직접 증거</b>이므로, 여기서 빨간불이면 " +
                "그 요구는 «미달»입니다.");

            Assert.IsFalse(leftIdle,
                $"{LogPrefix} 몰입기 도중 캐릭터가 Idle을 벗어났습니다 — Still에는 도달했지만 " +
                "«몰입기에는 걷지 않는다»(설계 6-3 ②의 전제)가 깨졌습니다. 프롭은 진입 프레임 좌표에 " +
                "고정돼 있으므로 걸으면 프롭만 덩그러니 남습니다.");

            // ---- ★ «충돌 없이»의 실물 — Still에 도달한 그 순간에도 LFVS는 돌고 있었다 ----
            Assert.Greater(poseWritesAtStill, poseWritesAtStart,
                $"{LogPrefix} Still에 도달한 시점까지 코스튬 포즈가 한 번도 쓰지 않았습니다 " +
                $"({poseWritesAtStart} -> {poseWritesAtStill}). 그렇다면 이 Still은 «코스튬이 돌면서도 " +
                "절감이 산다»의 증거가 아니라 «코스튬이 안 돌았다»의 증거입니다.");

            Debug.Log($"{LogPrefix} ★ L-3 통과 — 몰입기 {stillAt:F2}초에 Still 도달(문턱 {dwell:F2}초), " +
                $"그 시점까지 코스튬 포즈 쓰기 {poseWritesAtStill - poseWritesAtStart}회. " +
                "즉 LFVS가 도는 동안에도 절감 등급이 살아 있습니다(사용자 성능 제약 4번).");
        }

        /// <summary>몰입기의 벽시계 길이가 Still 문턱보다 <b>충분히</b> 긴가.
        /// 숫자를 베끼지 않고 <b>프로덕션 상수끼리</b> 비교한다 — 문턱이 바뀌면 이 검산이 먼저 말한다.</summary>
        private static void AssertBudgetCoversThreshold(float immersionWall, float dwell)
        {
            Assert.Greater(immersionWall, IdleWaitBudgetSeconds + 3f * dwell,
                $"{LogPrefix} 몰입기가 벽시계 {immersionWall:F2}초인데 " +
                $"«Idle 대기 {IdleWaitBudgetSeconds:F1}초 + Still 문턱 {dwell:F2}초의 3배»가 " +
                $"{IdleWaitBudgetSeconds + 3f * dwell:F2}초입니다 — 여유가 없으면 «도달 못 함»과 " +
                $"«시간이 모자람»을 구별할 수 없습니다. 시간 압축 배율({TimeCompression:F1})을 낮추거나 " +
                "세션 길이를 늘리십시오(등급 문턱은 unscaledDeltaTime이라 압축되지 않습니다).");
        }
    }
}
