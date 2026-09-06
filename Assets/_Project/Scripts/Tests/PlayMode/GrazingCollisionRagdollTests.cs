using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 사용자 신고 회귀 잠금(2026-09-06, 디버거 규명): <b>"가끔 캐릭터가 넘어짐"</b>
    ///
    /// ============================================================================
    /// 무엇이 고장나 있었나 (실측 — 추측 아님)
    /// ============================================================================
    /// <c>Core/StickmanAgent.OnCollisionEnter2D</c>가 넘기는 충격량은
    /// <c>collision.relativeVelocity.magnitude * mass</c>, 즉 <b>방향이 없는 속력</b>이었다. 그래서
    /// 접촉 법선과 거의 <b>수직</b>으로 스치는 접촉(Dock 물리 계단 옆면을 스치며 떨어지는 그림)이
    /// 정면충돌과 <b>똑같이 채점</b>됐다.
    ///
    /// <para>실제 앱 Player.log 실측: 법선 성분 <b>0.81 N·s</b>인데 판정에 쓰인 값은 <b>27.08</b>
    /// (임계 8.0). <b>33배 과대평가</b>로 불필요한 RAGDOLL이 강제됐다.</para>
    ///
    /// <para>수정은 <see cref="RagdollImpactResolver"/>의 법선 성분 투영이고, 스위치는
    /// <see cref="StickConfig.collisionImpactUsesNormalComponent"/>다.</para>
    ///
    /// ============================================================================
    /// ★ 이 파일이 왜 필요했나 — <b>이 경로에는 테스트가 0건이었다</b>
    /// ============================================================================
    /// 기존 랙돌 테스트는 전부 <c>StickmanAgent.ReportExternalImpact</c>(방향을 아는 직접 통지)만
    /// 쓴다. 충돌 콜백 경로(<c>ReportCollisionImpact</c> → <c>TryApplyCollisionImpact</c>)를 태우는
    /// 테스트는 저장소를 통틀어 <b>한 건도 없었고</b>, 그래서 이 결함이 6일 동안 살아 있었다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 저장소 표준)
    /// ============================================================================
    ///  T1  정적 벽 옆면을 스치며 수직 낙하 → <b>RAGDOLL이 되지 않는다</b>.
    ///  T1n (네거티브) 법선 성분 스위치를 끄면 T1이 <b>실제로 깨진다</b>
    ///      (= 관측창이 실패를 볼 수 있다는 증거. 이게 없으면 "항상 참인 단언"이다).
    ///  T2  같은 벽에 <b>수평 정면</b>으로 부딪히면 여전히 RAGDOLL이다 — 과보호가 아니다.
    ///
    /// ============================================================================
    /// ★ 왜 "랙돌 여부" 한 비트만 보면 안 되는가 (이 저장소의 거짓 통과 형태)
    /// ============================================================================
    /// 랙돌을 막는 장치는 <b>둘</b>이다 — 착지 차단막(<see cref="StickConfig.landingImpactRagdollShield"/>,
    /// 발밑에서 올라온 접촉을 걸러낸다)과 이번 라운드의 법선 성분. 결과 비트 하나만 보면
    /// <b>어느 쪽이 막았는지 구조적으로 가릴 수 없다</b>. 그래서 이 파일은 매 판정마다
    /// <c>RagdollImpactResolver.PeakCollision*</c> 진단 창구를 함께 단언한다:
    /// <list type="bullet">
    ///   <item><b>충돌이 실제로 일어났는가</b>(<c>CollisionImpactCount &gt; 0</c>) — 안 일어났으면
    ///     "랙돌 0회"는 아무것도 증명하지 않는다.</item>
    ///   <item><b>원본 충격량이 임계값을 넘었는가</b> — 안 넘었으면 시나리오가 애초에 결함을
    ///     재현하지 않은 것이다.</item>
    ///   <item><b>차단막에 걸리지 <u>않았는가</u></b>(<c>PeakCollisionShielded == false</c>) —
    ///     이것이 거짓이면 막은 것은 차단막이지 이번 수정이 아니다.</item>
    /// </list>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 판정 코드(States/RagdollImpactResolver)와 설정
    /// (Core/StickConfig)은 둘 다 플랫폼 분기가 없고, 이 테스트는 씬의 물리만 쓴다.
    /// Windows 영향: 없음(같은 코드가 그대로 돈다).</para>
    /// </summary>
    public sealed class GrazingCollisionRagdollTests
    {
        private const string LogPrefix = "[스침충돌]";

        /// <summary>낙하 속도(월드 유닛/초). 실제 앱 로그의 27.08 N·s(= 루트 질량 1.00 x 27.08)를 그대로 쓴다.</summary>
        private const float GrazeFallSpeed = 27f;

        /// <summary>벽 쪽으로 다가가는 속도. 접촉을 <b>실제로 접근해서</b> 만들기 위한 최소한의 값이며,
        /// 이것이 곧 법선 성분이 된다(2.0 / 27.07 = 정렬도 0.074 → 법선 충격량 2.0 &lt; 임계 8.0).</summary>
        private const float GrazeApproachSpeed = 2f;

        /// <summary>정면충돌 속도. dragThrowMaxSpeed 상한(12)과 같은 값이라 "실제로 도달 가능한 가장 센 정면"이다.</summary>
        private const float HeadOnSpeed = 12f;

        /// <summary>벽과의 초기 간격(월드 유닛). 한 물리 스텝(0.02초 x 접근속도 2.0 = 0.04유닛) 안에
        /// 반드시 닫히도록 그 절반으로 둔다 — 겹쳐 놓고 시작하면 <c>relativeVelocity</c>가 접근 속도가
        /// 아니라 충돌 해소 이후 값일 위험이 있어, <b>실제 앱과 같은 "다가가서 부딪힘"</b>으로 만든다.</summary>
        private const float WallGap = 0.02f;

        private const float SettleWaitSeconds = 1.0f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>충돌 판정이 실제로 무엇을 보고 무엇을 결정했는지의 스냅샷(첫 충돌 시점에 찍는다).</summary>
        private struct ImpactSnapshot
        {
            public int Count;
            public float Raw;
            public float Normal;
            public float Alignment01;
            public Vector2 PushDirection;
            public bool Shielded;
            public bool ReporterIsRoot;

            public static ImpactSnapshot Capture()
            {
                return new ImpactSnapshot
                {
                    Count = RagdollImpactResolver.CollisionImpactCount,
                    Raw = RagdollImpactResolver.PeakCollisionRawImpulse,
                    Normal = RagdollImpactResolver.PeakCollisionNormalImpulse,
                    Alignment01 = RagdollImpactResolver.PeakCollisionAlignment01,
                    PushDirection = RagdollImpactResolver.PeakCollisionPushDirection,
                    Shielded = RagdollImpactResolver.PeakCollisionShielded,
                    ReporterIsRoot = RagdollImpactResolver.PeakCollisionReporterIsRoot,
                };
            }

            public override string ToString()
            {
                return $"통지 {Count}건, 원본={Raw:F2} -> 법선={Normal:F2}, 정렬도={Alignment01:F3}, " +
                       $"법선방향=({PushDirection.x:F2},{PushDirection.y:F2}), 차단막={Shielded}, 루트보고={ReporterIsRoot}";
            }
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;
        private GameObject _wall;

        private readonly List<string> _trace = new List<string>();
        private int _ragdollEntries;

        [TearDown]
        public void TearDown()
        {
            StickmanEventBus.StateTransitioned -= OnTransition;
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_wall != null) Object.DestroyImmediate(_wall);
            _wall = null;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        private void OnTransition(StateTransitionEvent e)
        {
            StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
            Vector2 p = bb != null && bb.Body != null ? bb.Body.position : Vector2.zero;
            _trace.Add($"f={Time.frameCount} {e.From}->{e.To}{(e.IsForcedInterrupt ? "(강제)" : "")} 몸={p.ToString("F3")}");
            if (e.To == StickmanStateId.Ragdoll) _ragdollEntries++;
        }

        private string Trace() => _trace.Count == 0 ? "(전이 없음)" : string.Join("\n    ", _trace);

        // ============================================================================
        // 준비 — 공중에 떠 있는 정적 벽 하나. 발판은 0개(착지/스냅이 시나리오를 흔들지 못하게).
        // ============================================================================

        private IEnumerator SetUpFloatingWall()
        {
            _trace.Clear();
            _ragdollEntries = 0;

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            // Dock 물리 계단은 끈다 — 이 시나리오가 재현하려는 것은 "옆면 스침"이고, 그 옆면을
            // 여기서는 **내가 놓은 벽**으로 통제한다. 씬의 계단이 함께 있으면 어느 콜라이더가
            // 통지를 만들었는지 가릴 수 없다.
            _clonedConfig.dockPhysicsStepEnabled = false;
            // 접지 안전망/사각지대 회수는 낙하 자체를 붙잡아 시나리오를 지운다.
            _clonedConfig.groundKeepingSafetyNetEnabled = false;
            _clonedConfig.sinkholeLiftRecoveryEnabled = false;
            // ★ 착지 차단막은 **배포 기본값 그대로 켜 둔다.** 끄면 "이번 수정이 막았다"는 주장을
            //   대조 없이 얻게 된다 — 아래 단언이 PeakCollisionShielded == false 로 그 주장을 검증한다.
            _clonedConfig.landingImpactRagdollShield = true;

            // 발판 0개: 착지 스냅/화면 이탈 판정이 전부 no-op이 되어 순수 자유낙하가 된다
            // (StickmanBlackboard.CheckScreenBoundsOrFall은 HasAnyFoothold=false면 아무것도 하지 않는다).
            var service = new TestFootholdService();
            bb.FootholdPoller = new FootholdPoller(service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();

            StickmanEventBus.StateTransitioned += OnTransition;

            Debug.Log($"{LogPrefix} 준비 — 신장={bb.CharacterHeightWorld:F3}유닛, 루트질량={bb.Body.mass:F2}, " +
                $"랙돌임계={_clonedConfig.ragdollForceThreshold:F1}, " +
                $"법선성분스위치={_clonedConfig.collisionImpactUsesNormalComponent}, " +
                $"차단막={_clonedConfig.landingImpactRagdollShield}(일부러 켜 둠).");
        }

        /// <summary>루트 캡슐의 실제 반폭(월드). 프리팹 상수를 베끼지 않고 콜라이더에서 직접 잰다.</summary>
        private float MeasureRootHalfWidth()
        {
            var capsule = _agent.Blackboard.Body.GetComponent<CapsuleCollider2D>();
            Assert.IsNotNull(capsule, $"{LogPrefix} 루트에 CapsuleCollider2D가 없습니다 — 프리팹 배선 확인.");
            return capsule.bounds.extents.x;
        }

        /// <summary>
        /// 캐릭터의 <b>오른쪽</b>에 정적 벽을 세운다. 벽 아랫면은 발 높이 + <paramref name="bottomHeightRatio"/> x 신장
        /// 이상으로 두어, 접촉이 <b>절대 "발밑에서 올라온 것"으로 오인될 수 없게</b> 만든다
        /// (그래야 차단막이 아니라 법선 성분이 판정을 좌우한다).
        /// </summary>
        private void PlaceWallRightOf(Vector2 footWorldPos, float bottomHeightRatio)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float h = bb.CharacterHeightWorld;
            float halfWidth = MeasureRootHalfWidth();

            float wallBottomY = footWorldPos.y + bottomHeightRatio * h;
            float wallHeight = h * 6f;                       // 낙하 중에도 옆면이 계속 남아 있도록 넉넉히
            float wallThickness = h;                         // 얇으면 빠른 접근에서 관통할 수 있다
            float wallLeftFaceX = footWorldPos.x + halfWidth + WallGap;

            _wall = new GameObject("TestGrazeWall");
            // 레이어 2 = PhysicsGround와 같은 "Ignore Raycast". 캐릭터(StickmanLimb)와의 충돌은
            // 그대로 살아 있고(씬의 물리 바닥이 이미 그 조합으로 동작한다) 히트테스트만 비켜간다.
            _wall.layer = 2;
            _wall.transform.position = new Vector3(
                wallLeftFaceX + wallThickness * 0.5f,
                wallBottomY + wallHeight * 0.5f, 0f);
            var box = _wall.AddComponent<BoxCollider2D>();
            box.size = new Vector2(wallThickness, wallHeight);

            Debug.Log($"{LogPrefix} 벽 배치 — 왼쪽면 x={wallLeftFaceX:F3}(발 x={footWorldPos.x:F3} + 반폭 {halfWidth:F3} " +
                $"+ 간격 {WallGap:F3}), 아랫면 y={wallBottomY:F3}(발 y={footWorldPos.y:F3} + {bottomHeightRatio:F2}x신장 " +
                $"{h:F3}), 크기={box.size.ToString("F2")}. 차단막 상한(발+0.2x신장)={footWorldPos.y + 0.2f * h:F3} " +
                "— 벽 아랫면이 그보다 위라 이 벽과의 접촉은 '내 착지'로 오인될 수 없습니다.");
        }

        /// <summary>캐릭터를 그 자리에 두고 지정 속도를 실은 뒤 Fall 상태로 자유낙하시킨다.</summary>
        private void LaunchFrom(Vector2 footWorldPos, Vector2 velocity)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            bb.Body.bodyType = RigidbodyType2D.Dynamic;
            bb.MoveBodyToWorld(footWorldPos);
            bb.Body.linearVelocity = velocity;
            bb.CurrentFootholdHandle = 0L;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);

            _trace.Clear();
            _ragdollEntries = 0;
            RagdollImpactResolver.ResetCollisionDiagnostics();
        }

        /// <summary>
        /// 벽시계 예산으로 관찰하며 <b>첫 충돌 통지 시점의 진단 창구를 스냅샷</b>한다.
        /// (프레임 수 기반 대기 금지 — 이 저장소의 배치 PlayMode는 2,000fps 이상으로 돈다.)
        /// 스냅샷을 뒤로 미루면 안 되는 이유: 그 뒤에 이어지는 낙하가 화면 최하단 물리 바닥에
        /// 부딪히면서 <b>더 큰 원본 충격량</b>으로 창구를 덮어쓴다(그 접촉은 차단막에 걸린다).
        /// </summary>
        private IEnumerator ObserveUntil(float seconds, System.Action<ImpactSnapshot> onFirstImpact)
        {
            bool captured = false;
            float t = 0f;
            while (t < seconds)
            {
                if (!captured && RagdollImpactResolver.CollisionImpactCount > 0)
                {
                    captured = true;
                    onFirstImpact(ImpactSnapshot.Capture());
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (!captured) onFirstImpact(ImpactSnapshot.Capture());
        }

        /// <summary>세 케이스가 공유하는 배치/발사/관찰. 시나리오가 갈리는 곳은 속도와 스위치뿐이다.</summary>
        private IEnumerator RunWallScenario(Vector2 velocity, float wallBottomHeightRatio,
            System.Action<ImpactSnapshot> onFirstImpact)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;
            Assert.IsNotNull(cam, $"{LogPrefix} MainCamera가 없습니다.");

            // 화면 위쪽 절반에서 출발한다 — 관찰 창(0.35초) 안에 화면 최하단 물리 바닥에 닿지
            // 않아야 스냅샷이 그 충돌로 오염되지 않는다.
            Vector2 start = new Vector2(0f, cam.orthographicSize * 0.5f);
            PlaceWallRightOf(start, wallBottomHeightRatio);
            LaunchFrom(start, velocity);

            yield return ObserveUntil(0.35f, onFirstImpact);
        }

        private void AssertScenarioActuallyReproduced(ImpactSnapshot s, string label)
        {
            float threshold = _clonedConfig.ragdollForceThreshold;

            Assert.Greater(s.Count, 0,
                $"{LogPrefix} [{label}] 충돌 통지가 **한 건도** 발생하지 않았습니다 — 벽에 부딪히지 않았다는 뜻이라 " +
                "이 케이스는 아무 것도 재지 못했습니다(랙돌 0회는 이 상황에서 증거가 아닙니다). " +
                "레이어 충돌 매트릭스(StickmanLimb x Layer2)나 벽 좌표를 확인하십시오.\n" +
                $"    스냅샷: {s}\n    전이추적:\n    {Trace()}");

            Assert.IsTrue(s.ReporterIsRoot,
                $"{LogPrefix} [{label}] 가장 큰 통지를 보고한 것이 루트가 아닙니다 — 팔다리(질량 0.06~0.09) 통지를 " +
                $"보고 판정을 논하면 안 됩니다.\n    스냅샷: {s}");

            Assert.GreaterOrEqual(s.Raw, threshold,
                $"{LogPrefix} [{label}] 원본 충격량({s.Raw:F2})이 랙돌 임계값({threshold:F1})에 못 미쳤습니다 — " +
                "수정 전 코드였어도 랙돌이 되지 않는 시나리오라, 이 케이스는 결함을 재현하지 못했습니다. " +
                "낙하 속도를 올리거나 임계값 설정을 확인하십시오.\n" +
                $"    스냅샷: {s}");
        }

        // ============================================================================
        // T1 — 옆면을 스치며 수직 낙하: 랙돌이 되지 않는다
        // ============================================================================

        [UnityTest]
        public IEnumerator T1_GrazingWallDuringVerticalFallDoesNotRagdoll()
        {
            yield return SetUpFloatingWall();

            ImpactSnapshot snap = default;
            yield return RunWallScenario(
                velocity: new Vector2(GrazeApproachSpeed, -GrazeFallSpeed),
                wallBottomHeightRatio: 0.30f,
                onFirstImpact: s => snap = s);

            float threshold = _clonedConfig.ragdollForceThreshold;
            Debug.Log($"{LogPrefix} [T1 스침] {snap}, 랙돌진입={_ragdollEntries}회, " +
                $"최종상태={_agent.Blackboard.Machine.CurrentStateId}\n    전이추적:\n    {Trace()}");

            AssertScenarioActuallyReproduced(snap, "T1 스침");

            // (a) 막은 것이 **차단막이 아니라는** 증명. 이 줄이 없으면 이 테스트는
            //     "차단막이 잘 돈다"를 확인하고 초록이 될 수 있다.
            Assert.IsFalse(snap.Shielded,
                $"{LogPrefix} [T1] 이 접촉이 착지 차단막(landingImpactRagdollShield)에 걸렸습니다. " +
                "그러면 '법선 성분이 막았다'는 주장이 성립하지 않고, 이 테스트는 다른 장치를 확인한 것입니다. " +
                "벽 아랫면을 더 높이 올려 접촉이 발 높이 근처에서 생기지 않게 하십시오.\n" +
                $"    스냅샷: {snap}");

            // (b) 정말 "스침"이었는가 — 정렬도가 낮아야 이 시나리오의 이름이 참이다.
            Assert.Less(snap.Alignment01, 0.3f,
                $"{LogPrefix} [T1] 정렬도가 {snap.Alignment01:F3}로 높습니다 — 스친 것이 아니라 정면에 가깝게 " +
                "부딪혔다는 뜻이라, 이 케이스는 '스침'을 재지 못했습니다.\n    스냅샷: {snap}");

            // (c) 수정의 본체 — 판정에 실제로 들어간 값이 임계값 아래로 내려왔다.
            Assert.Less(snap.Normal, threshold,
                $"{LogPrefix} [T1] 법선 성분({snap.Normal:F2})이 여전히 임계값({threshold:F1}) 이상입니다 — " +
                "States/RagdollImpactResolver.ResolveNormalImpulse의 투영이 동작하지 않았습니다.\n" +
                $"    스냅샷: {snap}");

            // (d) 사용자가 실제로 보는 결과.
            Assert.AreEqual(0, _ragdollEntries,
                $"{LogPrefix} 회귀 — 벽 옆면을 스치며 떨어졌을 뿐인데 RAGDOLL이 됐습니다({_ragdollEntries}회). " +
                "사용자 신고 '가끔 캐릭터가 넘어짐'의 직접 재현입니다.\n" +
                $"    스냅샷: {snap}\n    전이추적:\n    {Trace()}");
        }

        // ============================================================================
        // T1n — 네거티브 컨트롤: 법선 성분 스위치를 끄면 T1이 실제로 깨진다
        //       (관측창이 실패를 볼 수 있다는 증거. 이게 초록이면 T1은 아무 것도 안 잰 것이다.)
        // ============================================================================

        [UnityTest]
        public IEnumerator T1n_NegativeControl_WithoutNormalComponentTheSameGrazeRagdolls()
        {
            yield return SetUpFloatingWall();
            _clonedConfig.collisionImpactUsesNormalComponent = false;   // 수정을 되돌린다

            ImpactSnapshot snap = default;
            yield return RunWallScenario(
                velocity: new Vector2(GrazeApproachSpeed, -GrazeFallSpeed),
                wallBottomHeightRatio: 0.30f,
                onFirstImpact: s => snap = s);

            Debug.Log($"{LogPrefix} [T1n 네거티브] {snap}, 랙돌진입={_ragdollEntries}회, " +
                $"최종상태={_agent.Blackboard.Machine.CurrentStateId}\n    전이추적:\n    {Trace()}");

            AssertScenarioActuallyReproduced(snap, "T1n 네거티브");

            Assert.IsFalse(snap.Shielded,
                $"{LogPrefix} [T1n] 접촉이 차단막에 걸렸습니다 — T1과 같은 접촉을 재고 있지 않다는 뜻입니다.\n" +
                $"    스냅샷: {snap}");

            // 스위치를 끈 경로에서는 **원본이 그대로** 판정에 들어가야 한다(비트 단위 동일 계약).
            Assert.AreEqual(snap.Raw, snap.Normal, 0.0001f,
                $"{LogPrefix} [T1n] 스위치를 껐는데도 판정값이 원본과 다릅니다({snap.Raw:F4} vs {snap.Normal:F4}) — " +
                "'끄면 2026-09-06 이전과 비트 단위로 같다'는 계약이 깨졌습니다.\n    스냅샷: {snap}");

            Assert.Greater(_ragdollEntries, 0,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 법선 성분 스위치를 껐는데도 RAGDOLL이 발생하지 않았습니다. " +
                "그러면 T1의 초록은 '수정이 동작한다'가 아니라 '애초에 랙돌이 안 나는 시나리오였다'일 수 있습니다 — " +
                "관측창을 다시 설계해야 합니다.\n" +
                $"    스냅샷: {snap}\n    전이추적:\n    {Trace()}");
        }

        // ============================================================================
        // T2 — 과보호 아님: 같은 벽에 수평 정면으로 부딪히면 여전히 랙돌이다
        // ============================================================================

        [UnityTest]
        public IEnumerator T2_HeadOnCollisionStillRagdolls()
        {
            yield return SetUpFloatingWall();

            ImpactSnapshot snap = default;
            yield return RunWallScenario(
                velocity: new Vector2(HeadOnSpeed, 0f),
                wallBottomHeightRatio: 0.30f,
                onFirstImpact: s => snap = s);

            float threshold = _clonedConfig.ragdollForceThreshold;
            Debug.Log($"{LogPrefix} [T2 정면] {snap}, 랙돌진입={_ragdollEntries}회, " +
                $"최종상태={_agent.Blackboard.Machine.CurrentStateId}\n    전이추적:\n    {Trace()}");

            AssertScenarioActuallyReproduced(snap, "T2 정면");

            Assert.IsFalse(snap.Shielded,
                $"{LogPrefix} [T2] 정면 충돌이 차단막에 걸렸습니다 — 벽 아랫면 배치를 확인하십시오.\n" +
                $"    스냅샷: {snap}");

            // 정면이면 정렬도가 1에 가깝고, 그래서 법선 성분이 원본과 사실상 같아야 한다.
            Assert.Greater(snap.Alignment01, 0.9f,
                $"{LogPrefix} [T2] 정렬도가 {snap.Alignment01:F3}에 그쳤습니다 — 정면으로 부딪히지 않았다는 뜻이라 " +
                "이 케이스는 '과보호 아님'을 재지 못했습니다.\n    스냅샷: {snap}");

            Assert.GreaterOrEqual(snap.Normal, threshold,
                $"{LogPrefix} [T2] 과보호 — 정면 충돌인데 법선 성분({snap.Normal:F2})이 임계값({threshold:F1}) " +
                "아래로 깎였습니다. 투영이 정면 타격까지 줄이면 그것은 결함 제거가 아니라 새 결함입니다.\n" +
                $"    스냅샷: {snap}");

            Assert.Greater(_ragdollEntries, 0,
                $"{LogPrefix} 과보호 — 정면으로 부딪혔는데 RAGDOLL이 되지 않았습니다. " +
                "이번 수정은 '스치는 접촉의 과대평가 제거'이지 '충돌 랙돌 폐지'가 아닙니다.\n" +
                $"    스냅샷: {snap}\n    전이추적:\n    {Trace()}");
        }
    }
}
