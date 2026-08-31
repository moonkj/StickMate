using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 사용자 신고 회귀 잠금(2026-08-31, 디버거):
    /// **"창을 최대화하면 공은 창 위에 있고 캐릭터는 독 위에 있음"**
    ///
    /// ============================================================================
    /// 확정된 원인 (코드 경로로 확정 — 추측 아님)
    /// ============================================================================
    /// 펫 "작은 공"의 x는 처음부터 <b>주인의 실시간 위치</b>(<c>Blackboard.Body.position.x</c>)를 매
    /// 프레임 따라가고 있었다. 어긋난 것은 <b>y 하나</b>다.
    ///
    ///   Interaction/CharacterPetRenderer.ResolveGroundY()  (수정 전)
    ///     bb.TryGetGroundSurfaceWorldY(new Vector2(x, probeY), out surfaceY)
    ///     = GroundSensor.TryGetSurfaceWorldY = "그 x에서 <b>가장 높은</b> 발판 상단"
    ///
    /// 창을 하나라도 최대화하면 그 창은 화면 전체 폭을 덮으므로, <b>어느 x에서든</b> 이 함수의 답이
    /// 최대화된 창의 상단(= 화면 꼭대기)이 된다. 캐릭터는 Dock 발판(핸들 −2)에 고착돼 그대로 Dock
    /// 위에 서 있고(<c>GroundSensor.Sense</c>는 <c>CurrentFootholdHandle</c>만 본다), 펫만 화면
    /// 꼭대기로 올라간다 — 실제 앱 실측 규모로 <b>약 21유닛</b>(Dock 상단 −10.17 vs 최대화 창 상단
    /// +11.19) 차이다. 사용자가 본 그림 그대로다.
    ///
    /// 같은 API를 같은 이유로 잘못 쓴 사고가 이 프로젝트에 이미 두 번 있었다 — 드래그 순간이동
    /// (2026-08-28, GroundSensor.TryGetFloorWorldY 문서)과 구조 안전망 순간이동(2026-08-29,
    /// Tests/PlayMode/GroundSnapTeleportTests). <b>"가장 높은 표면"은 표면을 고르는 용도가 아니다.</b>
    /// 펫이 물어야 했던 질문은 "주인이 <b>지금 딛고 있는</b> 발판은?"이고 그 답은 발판 핸들
    /// (<c>StickmanBlackboard.CurrentFootholdHandle</c> → <c>TryGetFootholdTopWorldY</c>)에만 있다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  P1  화면을 덮는 최대화 창이 발판 목록에 있어도, Dock 위에 선 주인의 <b>발밑</b>에 공이 있다.
    ///  P1n (네거티브) 같은 배치에서 <b>옛 공식</b>("가장 높은 표면")을 실제로 계산해 보면 화면
    ///      꼭대기가 나온다 = P1이 항상 참인 단언이 아니라 진짜 버그를 잡고 있다는 증거.
    ///  P2  신고 시나리오 그대로의 <b>전이</b> — 창 위에 있던 주인이 그 창의 최대화로 발판을 잃고
    ///      Dock으로 옮겨 가면, 공도 같은 프레임대에 Dock으로 따라온다(옛 코드는 창 위에 남았다).
    ///  P3  주인이 <b>공중</b>(Fall)일 때 공은 마지막 발판 위에서 기다린다 — 수정이 33-6-2의
    ///      "공은 날지 않는다" 규약을 깨지 않았다는 확인.
    ///
    /// 배치는 DockSinkholeRegressionTests와 같은 방식으로 만든다: 씬의 PhysicsGround 상단을 실측해
    /// 그보다 DockGeometry.ReferenceDockDropWorldUnits 위에 Dock 발판(−2)을 놓고, 화면 상단 5%
    /// 지점에 화면 전체 폭짜리 "최대화 창"(핸들 1001)을 놓는다. StickConfig는 복제본을 꽂아 원본
    /// 자산을 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3).
    /// </summary>
    public sealed class PetFollowsOwnerFootholdTests
    {
        private const string LogPrefix = "[PET-FOLLOW-TEST]";

        /// <summary>실제 앱의 합성 Dock 발판 핸들과 같은 값(FallbackPlatformWindowService.DockFootholdHandle).</summary>
        private const long DockHandle = -2L;

        /// <summary>테스트가 만드는 가짜 앱 창 핸들(실제 창 핸들 자리 = 양수).</summary>
        private const long WindowHandle = 1001L;

        /// <summary>PET 카테고리의 "작은 공" 자리(AppearanceShapeBuilder.PetBall과 같은 값).</summary>
        private const int PetBall = 0;

        /// <summary>공의 반지름(신장 배수) — AppearanceShapeBuilder.BallRadiusInHeight와 같은 값.
        /// 테스트 어셈블리에서는 internal 상수를 볼 수 없어 값을 복제한다(이 프로젝트의 기존 관례).</summary>
        private const float BallRadiusInHeight = 0.055f;

        /// <summary>공이 주인 뒤로 끌리는 거리(신장 배수) — CharacterPetRenderer.BallTrailInHeight.</summary>
        private const float BallTrailInHeight = 0.55f;

        private static readonly float DockDropUnits = DockGeometry.ReferenceDockDropWorldUnits;

        private const float SettleWaitSeconds = 2.0f;

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

        private StickmanAgent _agent;
        private CharacterPetRenderer _pet;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private float _floorTopWorldY;
        private float _dockTopWorldY;
        private float _dockTopOsY;
        private float _windowTopOsY;
        private float _windowTopWorldY;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            // 정적 모델이라 씬을 다시 로드해도 값이 살아남는다 — 다음 테스트로 차림이 새어 나가지 않게 한다.
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _pet = null;
        }

        /// <summary>
        /// 씬을 띄우고 "최대화된 창 + Dock" 발판 배치를 꽂는다. 최대화 창은 화면 전체 폭을 덮으므로
        /// <b>어느 x에서든</b> "가장 높은 표면"이 곧 화면 꼭대기가 된다 — 이 배치가 신고의 전제다.
        /// </summary>
        private IEnumerator SetUpMaximizedWindowLayout()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            _pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(_pet, $"{LogPrefix} 씬에서 CharacterPetRenderer를 찾지 못했습니다.");
            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            _floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _dockTopWorldY = _floorTopWorldY + DockDropUnits;

            Camera cam = bb.MainCamera;
            _dockTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _dockTopWorldY), _clonedConfig, out _).y;
            _windowTopOsY = Screen.height * 0.05f; // 메뉴바 바로 아래 = 최대화 창의 상단선
            _windowTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(0f, _windowTopOsY), 10f, _clonedConfig).y;

            _service = new TestFootholdService();
            ApplyLayout(maximized: true);
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();

            Debug.Log($"{LogPrefix} 준비 — Dock 상단 월드Y={_dockTopWorldY:F3}(OS {_dockTopOsY:F1}), " +
                $"최대화 창 상단 월드Y={_windowTopWorldY:F3}(OS {_windowTopOsY:F1}), " +
                $"두 높이 차이={_windowTopWorldY - _dockTopWorldY:F3}유닛, 신장={bb.CharacterHeightWorld:F3}.");

            Assert.Greater(_windowTopWorldY - _dockTopWorldY, 2f,
                $"{LogPrefix} 전제 실패 — 창 상단과 Dock 상단이 충분히 벌어져 있지 않아 이 회귀를 관측할 수 없습니다.");
        }

        /// <summary>maximized=false면 창을 화면 중간 높이의 보통 창으로 둔다(= 주인이 그 위에 서 있는 상태).</summary>
        private void ApplyLayout(bool maximized)
        {
            float w = Screen.width;
            float h = Screen.height;
            float topOs = maximized ? _windowTopOsY : h * 0.45f;
            _service.Footholds.Clear();
            _service.Footholds.Add(new PlatformFoothold(WindowHandle, new Rect(0f, topOs, w, h - topOs), true));
            _service.Footholds.Add(new PlatformFoothold(DockHandle,
                new Rect(w * 0.13f, _dockTopOsY, w * 0.755f, h - _dockTopOsY), false));
            _agent.Blackboard.FootholdPoller?.PollImmediately();
        }

        private float NormalWindowTopWorldY()
        {
            return ScreenCoordinateConverter.OsScreenToWorld(_agent.Blackboard.MainCamera,
                new Vector2(0f, Screen.height * 0.45f), 10f, _clonedConfig).y;
        }

        private float WorldXAtScreenFraction(float frac, float osY)
        {
            return ScreenCoordinateConverter.OsScreenToWorld(_agent.Blackboard.MainCamera,
                new Vector2(Screen.width * frac, osY), 10f, _clonedConfig).x;
        }

        private void Place(float worldX, float worldY, long handle, StickmanStateId state)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            bb.MoveBodyToWorld(new Vector2(worldX, worldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = handle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(state, isForcedInterrupt: true);
        }

        private IEnumerator WearBall()
        {
            EquipmentModel.TryWear(EquipmentSlot.Pet, PetBall, _agent.Config);
            Assert.AreEqual(PetBall, EquipmentModel.WornIndex(EquipmentSlot.Pet),
                $"{LogPrefix} 전제 실패 — 작은 공을 걸치지 못했습니다.");
            yield return null;
            yield return null;
            Assert.AreEqual(PetBall, _pet.ActivePetItemIndex,
                $"{LogPrefix} 전제 실패 — 작은 공이 그려지지 않았습니다.");
        }

        private float BallRadius() => _agent.Blackboard.CharacterHeightWorld * BallRadiusInHeight;

        // ============================================================================
        // P1 — 최대화된 창이 있어도 공은 주인(Dock 위)의 발밑에 있다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator P1_공펫은_최대화된_창이_있어도_주인이_선_Dock_위에_남는다()
        {
            yield return SetUpMaximizedWindowLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(DockHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 전제 실패 — 주인이 Dock 발판을 딛고 있지 않습니다(핸들={bb.CurrentFootholdHandle}).");

            yield return WearBall();
            yield return new WaitForSeconds(1.0f); // 지수 감쇠(rate 4)로 x 오차가 2% 아래로 내려가는 시간.

            Vector2 ball = _pet.PetWorldPosition;
            float expectedY = bb.Body.position.y + BallRadius();

            Debug.Log($"{LogPrefix} P1 결과 — 주인=({bb.Body.position.x:F3},{bb.Body.position.y:F3}) " +
                $"공=({ball.x:F3},{ball.y:F3}) 기대Y={expectedY:F3} " +
                $"최대화 창 상단={_windowTopWorldY:F3} 상태={bb.Machine.CurrentStateId}");

            Assert.That(ball.y, Is.EqualTo(expectedY).Within(0.35f),
                $"{LogPrefix} ★ 공이 주인의 발판 위에 있지 않습니다(공Y={ball.y:F3}, 기대={expectedY:F3}). " +
                "CharacterPetRenderer.ResolveGroundY()가 다시 TryGetGroundSurfaceWorldY(= 그 x에서 가장 높은 " +
                "발판 상단)를 쓰고 있지 않은지 확인하세요 — 그게 사용자 신고 '공은 창 위, 캐릭터는 독 위'입니다.");

            Assert.That(ball.y, Is.LessThan(_windowTopWorldY - 1f),
                $"{LogPrefix} ★ 공이 최대화된 창 상단(월드Y {_windowTopWorldY:F3} = 화면 꼭대기)으로 올라갔습니다. " +
                "이것이 신고의 재현입니다.");

            // x도 함께 잠근다 — "y만 맞고 x는 딴 데"도 같은 신고로 보인다.
            float trail = bb.CharacterHeightWorld * BallTrailInHeight;
            Assert.That(Mathf.Abs(ball.x - bb.Body.position.x), Is.LessThan(trail * 1.6f),
                $"{LogPrefix} 공이 주인에게서 가로로 너무 멀리 있습니다(간격={Mathf.Abs(ball.x - bb.Body.position.x):F3}, " +
                $"설계상 끌림={trail:F3}).");
        }

        // ============================================================================
        // P1n — 네거티브 컨트롤: 옛 공식은 같은 배치에서 실제로 화면 꼭대기를 답한다
        // ============================================================================

        /// <summary>
        /// ★ P1이 "조건이 헐거워서" 통과하는 것이 아님을 같은 파일에서 증명한다. 수정 전 코드가 쓰던
        /// 계산(<c>TryGetGroundSurfaceWorldY</c> = 그 x에서 가장 높은 발판 상단)을 <b>공이 실제로 있는
        /// 좌표에서</b> 그대로 돌려 보고, 그 답이 최대화된 창 상단(화면 꼭대기)인지 확인한다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator P1n_네거티브컨트롤_옛_공식은_같은_배치에서_화면_꼭대기를_답한다()
        {
            yield return SetUpMaximizedWindowLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);

            yield return WearBall();
            yield return new WaitForSeconds(1.0f);

            Vector2 ball = _pet.PetWorldPosition;

            // 수정 전 ResolveGroundY()가 부르던 바로 그 호출, 바로 그 인자.
            Assert.IsTrue(bb.TryGetGroundSurfaceWorldY(new Vector2(ball.x, bb.Body.position.y), out float legacyY),
                $"{LogPrefix} 표면 조회 자체가 실패했습니다 — 테스트 배선을 확인하세요.");

            float newY = ball.y - BallRadius();
            Debug.Log($"{LogPrefix} P1n 결과 — 옛 공식={legacyY:F3}(최대화 창 상단 {_windowTopWorldY:F3}), " +
                $"지금 공이 딛는 면={newY:F3}(Dock 상단 {_dockTopWorldY:F3}), 차이={legacyY - newY:F3}유닛");

            Assert.That(legacyY, Is.EqualTo(_windowTopWorldY).Within(0.1f),
                $"{LogPrefix} 네거티브 컨트롤 실패 — 옛 공식이 최대화 창 상단을 답하지 않습니다. " +
                "이 배치가 신고 조건을 재현하지 못한다는 뜻이므로 P1의 의미도 사라집니다.");
            Assert.That(legacyY - newY, Is.GreaterThan(2f),
                $"{LogPrefix} 네거티브 컨트롤 실패 — 옛 공식과 새 공식의 답이 사실상 같습니다" +
                $"({legacyY:F3} vs {newY:F3}). 두 값이 다르지 않으면 이번 수정은 아무것도 바꾸지 않은 것입니다.");
        }

        // ============================================================================
        // P2 — 신고 시나리오 전이: 창 위 → (최대화) → Dock. 공이 따라온다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator P2_주인이_창에서_Dock으로_옮겨가면_공도_따라온다()
        {
            yield return SetUpMaximizedWindowLayout();

            StickmanBlackboard bb = _agent.Blackboard;

            // (1) 최대화 전 — 보통 크기의 창 위에 주인이 서 있고 공도 그 위에 있다.
            ApplyLayout(maximized: false);
            float windowTopY = NormalWindowTopWorldY();
            Place(WorldXAtScreenFraction(0.5f, Screen.height * 0.45f), windowTopY, WindowHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);

            yield return WearBall();
            yield return new WaitForSeconds(1.0f);

            Vector2 before = _pet.PetWorldPosition;
            Debug.Log($"{LogPrefix} P2 (1) 창 위 — 주인Y={bb.Body.position.y:F3}(창 상단 {windowTopY:F3}), 공Y={before.y:F3}");
            Assert.That(before.y, Is.EqualTo(bb.Body.position.y + BallRadius()).Within(0.35f),
                $"{LogPrefix} 전제 실패 — 최대화 전부터 공이 주인 발밑에 있지 않습니다.");

            // (2) 그 창을 최대화한다 = 상단선이 화면 꼭대기로 올라간다. 주인은 그 발판을 잃고 Dock으로
            //     내려가 선다(실제 앱에서 낙하 후 Dock에 착지하는 그 결과 상태를 그대로 만든다).
            ApplyLayout(maximized: true);
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(1.5f);

            Vector2 after = _pet.PetWorldPosition;
            float expectedY = bb.Body.position.y + BallRadius();
            Debug.Log($"{LogPrefix} P2 (2) 최대화 후 — 주인=({bb.Body.position.x:F3},{bb.Body.position.y:F3}) " +
                $"공=({after.x:F3},{after.y:F3}) 기대Y={expectedY:F3} 최대화 창 상단={_windowTopWorldY:F3}");

            Assert.That(after.y, Is.EqualTo(expectedY).Within(0.35f),
                $"{LogPrefix} ★ 창을 최대화하자 공이 주인을 따라오지 않았습니다(공Y={after.y:F3}, 주인 발밑={expectedY:F3}). " +
                "사용자 신고 '공은 창 위에 있고 캐릭터는 독 위에 있음' 그대로입니다.");
            Assert.That(after.y, Is.LessThan(_windowTopWorldY - 1f),
                $"{LogPrefix} ★ 공이 최대화된 창 상단에 남았습니다(공Y={after.y:F3}).");
        }

        // ============================================================================
        // P3 — 공은 날지 않는다: 주인이 공중이면 마지막 발판 위에서 기다린다(33-6-2)
        // ============================================================================

        /// <summary>수정이 "따라오게" 하느라 33-6-2의 다른 규약을 깨지 않았는지 확인한다.</summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator P3_주인이_공중이면_공은_마지막_발판_위에서_기다린다()
        {
            yield return SetUpMaximizedWindowLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);
            yield return WearBall();
            yield return new WaitForSeconds(1.0f);

            float groundedBallY = _pet.PetWorldPosition.y;

            // 주인을 Dock 위 높이 띄우고 Fall로 강제한다(발판 핸들 0 = 공중).
            bb.MoveBodyToWorld(new Vector2(dockCenterX, _dockTopWorldY + bb.CharacterHeightWorld * 2f));
            bb.CurrentFootholdHandle = 0L;
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);
            yield return null;
            yield return null;
            yield return null;

            Vector2 airborne = _pet.PetWorldPosition;
            Debug.Log($"{LogPrefix} P3 결과 — 주인 공중Y={bb.Body.position.y:F3}, 공Y={airborne.y:F3}(접지 시 {groundedBallY:F3})");

            Assert.That(airborne.y, Is.EqualTo(groundedBallY).Within(0.2f),
                $"{LogPrefix} ★ 주인이 뜨자 공도 따라 떴습니다(공Y={airborne.y:F3}). 33-6-2 '공은 날지 않는다'가 깨졌습니다.");
        }
    }
}
