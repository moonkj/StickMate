using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 사용자 신고 "창이 최대이면 갑자기 제일위로 순간이동해서 떨어짐"(2026-08-29)의 재발 방지 테스트.
    ///
    /// ============================================================================
    /// 무엇이 진짜 원인이었나 (실측 로그로 확정)
    /// ============================================================================
    /// 리더의 최초 가설은 "StickmanBlackboard.SnapToGround()의 이동 거리에 상한이 없다"였다. 그 지적
    /// 자체는 옳았지만(그래서 아래 SnapToGround 상한 테스트도 함께 잠근다), **이번 신고의 원인은
    /// 그쪽이 아니었다.** GroundSensor.Sense()는 발이 발판 상단의 ±groundSnapTolerance(에셋 20 OS-pt
    /// ≈ 0.49 월드유닛) 안에 있을 때만 Grounded=true를 주고 GroundWorldY도 그때의 그 발판 상단이므로,
    /// SnapToGround가 옮길 수 있는 거리는 이미 그 허용오차로 묶여 있다 — 화면 높이만큼 끌어올리는 일은
    /// 그 경로로는 원리적으로 일어날 수 없다.
    ///
    /// 실제 범인은 **StickmanBlackboard.RescueToSafeGround()**였다. "6초 넘게 착지하지 못하면 회수한다"는
    /// 최종 안전망이 복귀 지점을 TryGetGroundSurfaceWorldY(= 그 x에서 **가장 높은** 발판 상단)로 골랐고,
    /// 창을 최대화하면 그 값이 곧 **화면 꼭대기**가 된다.
    ///
    /// 실측 증거(~/Library/Logs/DefaultCompany/StickMateSkeleton/):
    ///   · Player-prev.log — [캐릭터구조] 15회 중 **15회 전부** 복귀 지점 월드 (0.000, 11.193).
    ///     11.193 = 최대화된 Cursor 창 상단(OS y=33) = 화면 꼭대기.
    ///   · Player.log      — 24회 중 6회가 11.193(그 창이 목록에 있던 구간), 18회는 -10.167(Dock 상단).
    ///   즉 "최대화된 창이 목록에 있을 때만" 최상단으로 튄다 — 신고 문구와 정확히 일치한다.
    ///
    /// 그래서 이 파일은 **두 가지**를 잠근다.
    ///   (A) RescueToSafeGround가 최대화된 창이 있어도 화면 꼭대기로 올리지 않는다  ← 진짜 원인
    ///   (B) SnapToGround가 상한을 넘는 이동을 요구받으면 끌고 가지 않고 Fall로 보낸다 ← 방어적 불변식
    /// 두 테스트 모두 **네거티브 컨트롤**(수정을 되돌린 것과 동등한 조건에서 실제로 증상이 재현되는지)을
    /// 같은 파일 안에서 함께 단언한다 — 이 프로젝트의 표준이다.
    ///
    /// 왜 PlayMode인가: 좌표 변환에 실제 Camera와 Screen.width/height가 필요하다(ScreenCoordinateConverter).
    /// </summary>
    public sealed class GroundSnapTeleportTests
    {
        private const string LogPrefix = "[SNAP-TELEPORT-TEST]";

        /// <summary>실제 배포 환경 실측 화면(신고가 재현된 그 화면).</summary>
        private const float ReferenceScreenWidthPoints = 1512f;
        private const float ReferenceScreenHeightPoints = 982f;

        /// <summary>최대화된 창의 상단 OS y — 메뉴바 아래. 실측 로그의 Cursor@(0,33 1512x874) 그대로.</summary>
        private const float MaximizedWindowTopOsY = 33f;

        private Camera _camera;
        private GameObject _cameraGo;
        private StickConfig _config;
        private Vector2 _savedOrigin;
        private GameObject _bodyGo;
        private Rigidbody2D _body;

        /// <summary>
        /// 창 목록을 테스트가 직접 지정하는 스텁. FallbackPlatformWindowService로 감싸면 그 위에
        /// Dock/바닥 안전망이 자동으로 합성되므로, "최대화된 창 + 화면 하단 발판들"이라는 신고 당시의
        /// 발판 구성을 그대로 재현할 수 있다.
        /// </summary>
        private sealed class StubWindowService : IPlatformWindowService
        {
            private readonly List<PlatformFoothold> _footholds = new List<PlatformFoothold>();
            public void Set(params PlatformFoothold[] footholds)
            {
                _footholds.Clear();
                _footholds.AddRange(footholds);
            }
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => _footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>전이만 기록하는 최소 상태 — 실제 상태 그래프를 끌어오지 않고 Fall 전이 여부만 본다.</summary>
        private sealed class RecordingState : IStickmanState
        {
            public RecordingState(StickmanStateId id) { StateId = id; }
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        [SetUp]
        public void SetUp()
        {
            _cameraGo = new GameObject("SnapTeleportTestCamera", typeof(Camera));
            _camera = _cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;             // 씬(Main.unity)과 동일한 프레이밍.
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.desktopDpiScale = 1f;
            _config.groundSnapTolerance = 20f;
            _config.groundSnapMaxDistanceWorld = 0.6f;  // 수정본 기본값.
            _config.dockFootholdWidthFraction = 0.65f;
            _config.dockFootholdThicknessPoints = 75f;

            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _bodyGo = new GameObject("SnapTeleportTestBody", typeof(Rigidbody2D));
            _body = _bodyGo.GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic; // 테스트가 위치를 통제한다(중력 개입 배제).
        }

        [TearDown]
        public void TearDown()
        {
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_bodyGo != null) Object.DestroyImmediate(_bodyGo);
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private StickmanBlackboard NewBlackboard(StubWindowService stub)
        {
            var states = new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new RecordingState(StickmanStateId.Idle) },
                { StickmanStateId.Fall, new RecordingState(StickmanStateId.Fall) },
            };
            var machine = new StickmanStateMachine(states);
            var blackboard = new StickmanBlackboard
            {
                Body = _body,
                MainCamera = _camera,
                Config = _config,
                Machine = machine,
                FootholdPoller = new FootholdPoller(new FallbackPlatformWindowService(stub, _config), _config),
            };
            machine.Start(StickmanStateId.Idle);
            return blackboard;
        }

        /// <summary>OS 화면 y를 월드 y로 환산(카메라/화면 기준). 기대값을 사람이 읽을 수 있는 OS 좌표로 쓰기 위한 도우미.</summary>
        private float OsYToWorldY(float osY)
        {
            Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(_camera, Vector2.zero, _config, out float depth);
            _ = os;
            return ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(0f, osY), depth, _config).y;
        }

        // ============================================================================
        // (A) 진짜 원인 — RescueToSafeGround
        // ============================================================================

        /// <summary>
        /// ★ 절대 조건: 화면 전체를 덮는 **최대화된 창**이 발판 목록에 있어도, 구조 안전망은 캐릭터를
        /// 화면 꼭대기로 올려놓지 않는다. 신고 문구 "창이 최대이면 갑자기 제일위로 순간이동해서 떨어짐"
        /// 그 자체를 코드로 못박는 단언이다.
        /// </summary>
        [Test]
        public void 구조안전망은_최대화된_창이_있어도_캐릭터를_화면_꼭대기로_올리지_않는다()
        {
            var stub = new StubWindowService();
            // 실측 로그와 동일한 최대화 창: 메뉴바 아래부터 Dock 위까지 화면 전체 폭.
            stub.Set(new PlatformFoothold(865L,
                new Rect(0f, MaximizedWindowTopOsY, ReferenceScreenWidthPoints, 874f), true));

            StickmanBlackboard blackboard = NewBlackboard(stub);

            // 캐릭터가 화면 하단 사각지대에 빠져 6초간 착지하지 못한 그 상황을 재현한다.
            float bottomWorldY = OsYToWorldY(ReferenceScreenHeightPoints - 40f);
            _body.position = new Vector2(-3.34f, bottomWorldY);

            blackboard.RescueToSafeGround();

            float maximizedTopWorldY = OsYToWorldY(MaximizedWindowTopOsY);
            float rescuedY = _body.position.y;

            Debug.Log($"{LogPrefix} 구조 후 월드Y={rescuedY:F3} / 최대화 창 상단(=화면 꼭대기) 월드Y={maximizedTopWorldY:F3}");

            // 절대 조건 1 — 최대화된 창의 상단(화면 꼭대기) 근처로 가면 안 된다.
            Assert.That(rescuedY, Is.LessThan(maximizedTopWorldY - 1f),
                $"{LogPrefix} 구조 안전망이 캐릭터를 최대화된 창 상단(월드Y {maximizedTopWorldY:F3} = 화면 꼭대기)으로 " +
                $"끌어올렸습니다. 실제 복귀 Y={rescuedY:F3}. 이것이 사용자 신고 '창이 최대이면 갑자기 제일위로 " +
                "순간이동해서 떨어짐'의 재현입니다 — RescueToSafeGround()가 TryGetFloorWorldY(가장 낮은 표면)가 " +
                "아니라 TryGetGroundSurfaceWorldY(가장 높은 표면)를 쓰고 있지 않은지 확인하세요.");

            // 절대 조건 2 — 화면 아래쪽 절반(= 실제 바닥 대역) 안이어야 한다.
            Assert.That(rescuedY, Is.LessThan(0f),
                $"{LogPrefix} 복귀 지점이 화면 위쪽 절반입니다(월드Y={rescuedY:F3}). 구조 안전망의 복귀 지점은 " +
                "언제나 그 x의 '바닥'(Dock 또는 화면 최하단 합성 안전망)이어야 합니다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 수정을 되돌린 것과 동등한 계산(가장 **높은** 표면을 복귀 지점으로 채택)이
        /// 실제로 화면 꼭대기를 답하는지 확인한다. 이게 통과해야 위 테스트가 "무언가를 실제로 잡고 있다"는
        /// 것이 증명된다(항상 참인 단언이 아니라는 증거).
        /// </summary>
        [Test]
        public void 네거티브컨트롤_가장_높은_표면을_쓰면_실제로_화면_꼭대기가_나온다()
        {
            var stub = new StubWindowService();
            stub.Set(new PlatformFoothold(865L,
                new Rect(0f, MaximizedWindowTopOsY, ReferenceScreenWidthPoints, 874f), true));

            StickmanBlackboard blackboard = NewBlackboard(stub);
            var probe = new Vector2(0f, OsYToWorldY(ReferenceScreenHeightPoints - 40f));

            Assert.IsTrue(blackboard.TryGetGroundSurfaceWorldY(probe, out float highest),
                $"{LogPrefix} 표면 조회 자체가 실패했습니다 — 테스트 배선을 확인하세요.");
            Assert.IsTrue(blackboard.TryGetFloorWorldY(probe, out float floor),
                $"{LogPrefix} 바닥 조회 자체가 실패했습니다 — 테스트 배선을 확인하세요.");

            float maximizedTopWorldY = OsYToWorldY(MaximizedWindowTopOsY);
            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 가장 높은 표면={highest:F3}(창 상단 {maximizedTopWorldY:F3}), 바닥={floor:F3}");

            // 예전 구현이 쓰던 값은 확실히 화면 꼭대기다 = 버그가 실재했다.
            Assert.That(highest, Is.EqualTo(maximizedTopWorldY).Within(0.05f),
                $"{LogPrefix} 네거티브 컨트롤 실패 — '가장 높은 표면'이 최대화된 창 상단이 아닙니다. " +
                "이 테스트가 재현하려는 조건 자체가 성립하지 않으므로 위 테스트의 의미도 사라집니다.");
            // 그리고 새 구현이 쓰는 값은 그보다 확실히 아래다 = 수정이 실제로 다른 답을 만든다.
            Assert.That(floor, Is.LessThan(highest - 1f),
                $"{LogPrefix} 네거티브 컨트롤 실패 — 바닥과 가장 높은 표면이 사실상 같은 값입니다({floor:F3} vs {highest:F3}). " +
                "두 값이 다르지 않으면 이번 수정은 아무것도 바꾸지 않은 것입니다.");
        }

        // ============================================================================
        // (B) 방어적 불변식 — SnapToGround 이동 거리 상한 (리더 지시 항목)
        // ============================================================================

        /// <summary>
        /// ★ 절대 조건: 딛고 있는 발판의 상단 Y가 갑자기 크게 올라가도, 캐릭터는 그 높이로 따라 올라가지
        /// 않는다. 대신 발판을 놓고 Fall로 간다(창이 캐릭터를 지나쳐 자라났다면 캐릭터는 공중에 남는 것이
        /// 물리적으로 맞다 — 리더 지시).
        /// </summary>
        [Test]
        public void 딛고있는_발판_상단이_갑자기_크게_오르면_캐릭터가_따라_올라가지_않는다()
        {
            var stub = new StubWindowService();
            StickmanBlackboard blackboard = NewBlackboard(stub);

            const float startY = 0f;
            const float raisedGroundY = 8f; // 상한 0.6의 13배 — "창이 최대화돼 상단이 화면 꼭대기로 뛴" 규모.
            _body.position = new Vector2(0f, startY);
            blackboard.CurrentFootholdHandle = 42L;

            var info = new GroundSensor.GroundInfo(
                grounded: true, groundWorldY: raisedGroundY, hasAnyFoothold: true,
                screenLeftWorldX: -20f, screenRightWorldX: 20f,
                currentFootholdLeftWorldX: -20f, currentFootholdRightWorldX: 20f,
                groundedFootholdHandle: 42L);

            bool transitioned = blackboard.GroundedTick(0.02f, info);

            Debug.Log($"{LogPrefix} 스냅 상한 시험 — 요구 이동 {raisedGroundY - startY:F2}유닛, " +
                $"상한 {_config.groundSnapMaxDistanceWorld:F2}, 실제 Y={_body.position.y:F3}, " +
                $"상태={blackboard.Machine.CurrentStateId}");

            Assert.That(_body.position.y, Is.EqualTo(startY).Within(0.001f),
                $"{LogPrefix} 캐릭터가 갑자기 올라간 발판 상단(Y={raisedGroundY})을 따라 순간이동했습니다 " +
                $"(실제 Y={_body.position.y:F3}). StickmanBlackboard.SnapToGround()의 이동 거리 상한" +
                "(StickConfig.groundSnapMaxDistanceWorld)이 살아 있는지 확인하세요.");
            Assert.IsTrue(transitioned,
                $"{LogPrefix} 상한 초과 시 GroundedTick()은 'Fall로 전이했다'는 뜻의 true를 반환해야 합니다.");
            Assert.That(blackboard.Machine.CurrentStateId, Is.EqualTo(StickmanStateId.Fall),
                $"{LogPrefix} 상한을 넘었으면 발판을 놓고 Fall로 가야 합니다(현재 {blackboard.Machine.CurrentStateId}).");
            Assert.That(blackboard.CurrentFootholdHandle, Is.EqualTo(0L),
                $"{LogPrefix} 발판을 놓았다면 고착 핸들도 0으로 지워져야 합니다.");
        }

        /// <summary>
        /// ★ 반대편 절대 조건(과잉 수정 방지) — 정상 보행 중의 미세 정착과 걷다 만나는 작은 단차는
        /// **계속 스냅돼야 한다.** 이게 깨지면 캐릭터가 걷다 말고 덜덜거리며 낙하한다(리더가 명시적으로
        /// 경계한 실패 모드).
        /// </summary>
        [Test]
        public void 정상적인_미세정착과_작은_단차는_그대로_스냅된다()
        {
            var stub = new StubWindowService();
            StickmanBlackboard blackboard = NewBlackboard(stub);

            // groundSnapTolerance(20 OS-pt)가 실제로 만들어낼 수 있는 최대 크기(≈0.49유닛)에 가까운 값 —
            // 즉 "정상 접지가 만들어낼 수 있는 가장 큰 스냅"도 상한 아래여야 한다.
            foreach (float delta in new[] { 0.0005f, 0.05f, 0.25f, 0.48f })
            {
                _body.position = Vector2.zero;
                blackboard.CurrentFootholdHandle = 7L;
                blackboard.Machine.ChangeState(StickmanStateId.Idle);

                var info = new GroundSensor.GroundInfo(
                    grounded: true, groundWorldY: delta, hasAnyFoothold: true,
                    screenLeftWorldX: -20f, screenRightWorldX: 20f,
                    currentFootholdLeftWorldX: -20f, currentFootholdRightWorldX: 20f,
                    groundedFootholdHandle: 7L);

                bool transitioned = blackboard.GroundedTick(0.02f, info);

                Assert.IsFalse(transitioned,
                    $"{LogPrefix} 정상 범위의 스냅({delta:F4}유닛)에서 Fall로 전이했습니다 — 상한이 너무 빡빡합니다. " +
                    $"상한은 groundSnapTolerance(20 OS-pt ≈ 0.49유닛)보다 반드시 커야 합니다.");
                Assert.That(_body.position.y, Is.EqualTo(delta).Within(0.001f),
                    $"{LogPrefix} 정상 범위의 스냅({delta:F4}유닛)이 적용되지 않았습니다(실제 Y={_body.position.y:F4}). " +
                    "미세 정착이 사라지면 걸음이 덜덜거립니다.");
                Assert.That(blackboard.Machine.CurrentStateId, Is.EqualTo(StickmanStateId.Idle),
                    $"{LogPrefix} 정상 범위의 스냅({delta:F4}유닛)에서 상태가 바뀌면 안 됩니다.");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 상한을 사실상 무한대로 되돌리면(= 수정 이전 동작) 캐릭터가 실제로
        /// 그 높이까지 순간이동하는지 확인한다. 이게 통과해야 위 상한 테스트가 진짜로 상한을 잡고 있다는
        /// 것이 증명된다.
        /// </summary>
        [Test]
        public void 네거티브컨트롤_상한을_없애면_실제로_순간이동이_재현된다()
        {
            var stub = new StubWindowService();
            StickmanBlackboard blackboard = NewBlackboard(stub);

            _config.groundSnapMaxDistanceWorld = float.MaxValue; // 수정 이전과 동등한 조건.

            const float raisedGroundY = 8f;
            _body.position = Vector2.zero;
            blackboard.CurrentFootholdHandle = 42L;

            var info = new GroundSensor.GroundInfo(
                grounded: true, groundWorldY: raisedGroundY, hasAnyFoothold: true,
                screenLeftWorldX: -20f, screenRightWorldX: 20f,
                currentFootholdLeftWorldX: -20f, currentFootholdRightWorldX: 20f,
                groundedFootholdHandle: 42L);

            blackboard.GroundedTick(0.02f, info);

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 상한 제거 시 Y={_body.position.y:F3}(기대: {raisedGroundY})");

            Assert.That(_body.position.y, Is.EqualTo(raisedGroundY).Within(0.001f),
                $"{LogPrefix} 네거티브 컨트롤 실패 — 상한을 없앴는데도 순간이동이 재현되지 않았습니다. " +
                "그렇다면 위 상한 테스트는 상한이 아니라 다른 무언가를 측정하고 있는 것이므로 " +
                "테스트 자체를 다시 설계해야 합니다.");
        }
    }
}
