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
    /// ★★ 사용자 신고 회귀 잠금 2건(2026-08-31, 디버거) — 펫 "리틀스틱메이트"(PET 자리 2번)
    ///
    /// ============================================================================
    /// 신고 A — "높은 곳에서 떨어질 때 작은 졸라맨도 캐릭터와 동일한 형태로 떨어져야 하는데 안 됨"
    /// ============================================================================
    /// 확정 원인(추측 아님 — 수정 전 <c>CharacterPetRenderer.TickMini</c> 본문이 12줄이었고 그 안에
    /// 상태를 읽는 줄이 <b>하나도</b> 없었다):
    ///   · y가 언제나 <c>ResolveGroundY</c>(= 마지막 발판 상단)라 주인이 20유닛을 떨어져도 미니는
    ///     원래 높이에 붙박이로 남아 x만 미끄러졌다.
    ///   · 몸통 회전이 <c>Quaternion.identity</c> 하드코딩이라 던지기 공중 회전이 한 도도 전달되지 않았다.
    ///   · 무릎앉아(LandingCrouch)를 구독하는 경로가 아예 없었다.
    ///
    /// ============================================================================
    /// 신고 B — "작은 졸라맨도 창 위에 있을 때 창 범위 안에 있어야 하는데 공중에 떠 있음"
    /// ============================================================================
    /// 확정 원인: 펫의 <b>y</b>는 "주인이 딛고 있는 발판의 상단"인데 <b>x</b>는 <c>주인 x − 끌림거리</c>
    /// 하나로만 정해져 <b>발판의 가로 범위를 아무도 보지 않았다</b>. 주인이 창 가장자리에서 <b>돌아서면</b>
    /// facing 부호가 뒤집혀 끌림거리가 바깥쪽으로 향하고, 리틀스틱메이트의 끌림거리는 신장의 0.75배라
    /// 창 밖으로 확실히 벗어난다 = "창 높이에 떠 있는" 그림.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  F1  주인이 Fall이면 미니의 y가 <b>주인의 발바닥</b>을 따라 함께 올라간다.
    ///  F1n (네거티브) 같은 배치에서 <b>옛 규칙</b>(= 마지막 발판 상단)을 실제로 계산하면 접지 시
    ///      높이 그대로가 나온다 = F1이 항상 참인 단언이 아니라 진짜 회귀를 잡고 있다는 증거.
    ///  F2  던지기 공중 회전 중 미니의 몸통 회전각이 주인의 루트 회전각과 <b>같다</b>(0이 아니다).
    ///  F3  무릎앉아 착지 중 미니도 함께 웅크린다 — 진행 곡선 값을 따라가고 몸이 실제로 <b>내려간다</b>.
    ///  F4  주인이 창 가장자리에서 돌아서도 미니는 그 창의 가로 범위 <b>안</b>에 남는다.
    ///  F4n (네거티브) 같은 배치에서 <b>제한이 없었다면</b> 목표 x가 창 밖이라는 것을 직접 계산해 보인다.
    ///
    /// 배치는 PetFollowsOwnerFootholdTests와 같은 방식(씬 PhysicsGround 실측 -> Dock 발판 -> 가짜 창).
    /// StickConfig는 복제본을 꽂아 원본 자산을 건드리지 않는다(CLAUDE.md 불변 원칙 3).
    /// </summary>
    public sealed class PetFallSyncTests
    {
        private const string LogPrefix = "[PET-FALL-TEST]";

        /// <summary>실제 앱의 합성 Dock 발판 핸들(FallbackPlatformWindowService.DockFootholdHandle).</summary>
        private const long DockHandle = -2L;

        /// <summary>테스트가 만드는 가짜 앱 창 핸들(실제 창 핸들 자리 = 양수).</summary>
        private const long WindowHandle = 2001L;

        /// <summary>PET 카테고리의 "리틀스틱메이트" 자리(AppearanceShapeBuilder.PetMini와 같은 값).
        /// 테스트 어셈블리에서는 internal 상수를 볼 수 없어 값을 복제한다(이 프로젝트의 기존 관례).</summary>
        private const int PetMini = 2;

        /// <summary>미니가 주인 뒤로 끌리는 거리(신장 배수) — CharacterPetRenderer.MiniTrailInHeight.</summary>
        private const float MiniTrailInHeight = 0.75f;

        /// <summary>미니의 키(주인 신장 배수) — AppearanceShapeBuilder.MiniScale.</summary>
        private const float MiniScale = 0.45f;

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

        // 좁은 "창" 발판 — 신고 B의 전제(주인이 그 가장자리에서 돌아설 수 있어야 한다).
        private float _windowTopWorldY;
        private float _windowLeftWorldX;
        private float _windowRightWorldX;

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
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _pet = null;
        }

        private IEnumerator SetUpLayout()
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

            float w = Screen.width;
            float h = Screen.height;
            float windowTopOs = h * 0.45f;
            float windowLeftOs = w * 0.35f;
            float windowWidthOs = w * 0.30f;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(WindowHandle,
                new Rect(windowLeftOs, windowTopOs, windowWidthOs, h - windowTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(DockHandle,
                new Rect(w * 0.13f, _dockTopOsY, w * 0.755f, h - _dockTopOsY), false));

            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();
            bb.FootholdPoller.PollImmediately();

            _windowTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(0f, windowTopOs), 10f, _clonedConfig).y;
            _windowLeftWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(windowLeftOs, windowTopOs), 10f, _clonedConfig).x;
            _windowRightWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(windowLeftOs + windowWidthOs, windowTopOs), 10f, _clonedConfig).x;

            Debug.Log($"{LogPrefix} 준비 — Dock 상단 월드Y={_dockTopWorldY:F3}, 창 상단 월드Y={_windowTopWorldY:F3}, " +
                $"창 가로=[{_windowLeftWorldX:F3}, {_windowRightWorldX:F3}](폭 {_windowRightWorldX - _windowLeftWorldX:F3}유닛), " +
                $"신장={bb.CharacterHeightWorld:F3}, 끌림거리={bb.CharacterHeightWorld * MiniTrailInHeight:F3}유닛.");
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

        private IEnumerator WearMini()
        {
            EquipmentModel.TryWear(EquipmentSlot.Pet, PetMini, _agent.Config);
            Assert.AreEqual(PetMini, EquipmentModel.WornIndex(EquipmentSlot.Pet),
                $"{LogPrefix} 전제 실패 — 리틀스틱메이트를 걸치지 못했습니다.");
            yield return null;
            yield return null;
            Assert.AreEqual(PetMini, _pet.ActivePetItemIndex,
                $"{LogPrefix} 전제 실패 — 리틀스틱메이트가 그려지지 않았습니다.");
        }

        /// <summary>그려진 미니 그림의 실제 Transform(무릎앉아로 몸이 내려갔는지 확인용).</summary>
        private static Transform FindMiniBody()
        {
            GameObject container = GameObject.Find("CharacterPet");
            return container != null ? container.transform.Find("Body") : null;
        }

        // ============================================================================
        // F1 — 주인이 공중이면 미니도 함께 떨어진다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F1_주인이_낙하하면_미니도_같은_높이로_함께_떨어진다()
        {
            yield return SetUpLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(Screen.width * 0.5f, _dockTopOsY), 10f, _clonedConfig).x;
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);

            yield return WearMini();
            yield return new WaitForSeconds(1.0f);

            float groundedMiniY = _pet.PetWorldPosition.y;

            // 주인을 신장 3배 높이로 띄우고 Fall로 강제한다(발판 핸들 0 = 공중).
            float airborneY = _dockTopWorldY + bb.CharacterHeightWorld * 3f;
            bb.MoveBodyToWorld(new Vector2(dockCenterX, airborneY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = 0L;
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);
            yield return null;
            yield return null;
            yield return null;

            Vector2 mini = _pet.PetWorldPosition;
            float ownerY = bb.Body.position.y;

            Debug.Log($"{LogPrefix} F1 결과 — 주인 공중Y={ownerY:F3}(접지 때 {_dockTopWorldY:F3}), " +
                $"미니Y={mini.y:F3}(접지 때 {groundedMiniY:F3}), 상태={bb.Machine.CurrentStateId}, " +
                $"공중자세세기={_pet.MiniAirPostureAmount:F3}");

            Assert.That(mini.y, Is.EqualTo(ownerY).Within(bb.CharacterHeightWorld * 0.25f),
                $"{LogPrefix} ★ 미니가 주인과 같은 높이로 떨어지지 않았습니다(미니Y={mini.y:F3}, 주인Y={ownerY:F3}). " +
                "CharacterPetRenderer.TickMini가 공중에서도 ResolveGroundY(= 마지막 발판 상단)를 쓰고 있지 " +
                "않은지 확인하세요 — 그게 사용자 신고 '캐릭터와 동일한 형태로 떨어지지 않는다'입니다.");

            Assert.That(mini.y - groundedMiniY, Is.GreaterThan(bb.CharacterHeightWorld),
                $"{LogPrefix} ★ 미니가 접지 시 높이 그대로입니다(변화={mini.y - groundedMiniY:F3}유닛). 붙박이 회귀.");
        }

        /// <summary>
        /// ★ F1이 "조건이 헐거워서" 통과하는 것이 아님을 같은 파일에서 증명한다. 수정 전 규칙
        /// (= 주인이 공중이면 <b>마지막 발판 상단</b>)을 실제로 계산해 보고, 그 답이 접지 시 높이와
        /// 같은지 = 미니가 따라 떨어질 수 없었는지 확인한다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F1n_네거티브컨트롤_옛_규칙은_같은_배치에서_접지_높이를_답한다()
        {
            yield return SetUpLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(Screen.width * 0.5f, _dockTopOsY), 10f, _clonedConfig).x;
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);
            yield return WearMini();
            yield return new WaitForSeconds(1.0f);

            float groundedMiniY = _pet.PetWorldPosition.y;

            float airborneY = _dockTopWorldY + bb.CharacterHeightWorld * 3f;
            bb.MoveBodyToWorld(new Vector2(dockCenterX, airborneY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);
            yield return null;
            yield return null;

            // 수정 전 TickMini가 물어보던 바로 그 값: "주인이 마지막으로 딛고 있던 발판의 상단".
            Assert.IsTrue(bb.TryGetFootholdTopWorldY(DockHandle, out float legacyY),
                $"{LogPrefix} 발판 조회 자체가 실패했습니다 — 테스트 배선을 확인하세요.");

            Debug.Log($"{LogPrefix} F1n 결과 — 옛 규칙={legacyY:F3}(접지 시 미니Y {groundedMiniY:F3}), " +
                $"주인 공중Y={bb.Body.position.y:F3}, 차이={bb.Body.position.y - legacyY:F3}유닛");

            Assert.That(legacyY, Is.EqualTo(groundedMiniY).Within(0.2f),
                $"{LogPrefix} 네거티브 컨트롤 실패 — 옛 규칙이 접지 시 높이를 답하지 않습니다. " +
                "이 배치가 신고 조건을 재현하지 못한다는 뜻이므로 F1의 의미도 사라집니다.");
            Assert.That(bb.Body.position.y - legacyY, Is.GreaterThan(bb.CharacterHeightWorld),
                $"{LogPrefix} 네거티브 컨트롤 실패 — 옛 규칙과 새 규칙의 답이 사실상 같습니다. " +
                "두 값이 다르지 않으면 이번 수정은 아무것도 바꾸지 않은 것입니다.");
        }

        // ============================================================================
        // F2 — 던지기 공중 회전을 미니가 그대로 따라간다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F2_던지기_공중회전을_미니가_그대로_따라간다()
        {
            yield return SetUpLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(Screen.width * 0.5f, _dockTopOsY), 10f, _clonedConfig).x;
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);
            yield return WearMini();
            yield return new WaitForSeconds(1.0f);

            // 실제 던지기와 같은 입력으로 ThrowTumble에 진입시킨다(속도 스냅샷 -> 회전 방향/각속도 파생).
            float height = bb.CharacterHeightWorld;
            bb.LastThrowVelocity = new Vector2(height * 4f, height * 6f);
            bb.MoveBodyToWorld(new Vector2(dockCenterX, _dockTopWorldY + height * 4f));
            bb.Body.linearVelocity = bb.LastThrowVelocity;
            bb.CurrentFootholdHandle = 0L;
            bb.Machine.ChangeState(StickmanStateId.ThrowTumble, isForcedInterrupt: true);

            // ★ 프레임 수가 아니라 <b>흘러간 게임 시간</b>으로 표본을 모은다. batchmode의 프레임은
            //   1ms 미만이라(실측: 20프레임 = 5.5ms) 고정 프레임 반복으로는 711도/초의 회전이 4도밖에
            //   쌓이지 않아 "회전을 안 한다"는 가짜 실패가 난다.
            float maxAbsSpin = 0f;
            float worstMismatch = 0f;
            float elapsed = 0f;
            int sampled = 0;
            float prevOwner = bb.Body.rotation;
            while (elapsed < 0.35f && sampled < 20000)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (bb.Machine.CurrentStateId != StickmanStateId.ThrowTumble) break;

                float owner = bb.Body.rotation;
                float miniSpin = _pet.MiniSpinDegrees;
                maxAbsSpin = Mathf.Max(maxAbsSpin, Mathf.Abs(owner));

                // ★ 한 프레임의 <b>표집 어긋남</b>을 허용한다: 테스트 코루틴은 Update 단계에서 깨어나므로
                //   StickmanAgent.Update(회전 적분)와의 순서가 정해져 있지 않지만, 펫은 언제나 LateUpdate에서
                //   쓴다. 그래서 "주인은 이번 프레임 값 / 미니는 직전 프레임 값"인 표본이 섞일 수 있다
                //   (실측 최대 1.43도 = 정확히 한 프레임분 회전). 붙박이 회귀(미니가 0도에 멈춤)는 두 값
                //   어디와도 맞지 않으므로 이 허용으로 느슨해지지 않는다.
                float mismatch = Mathf.Min(
                    Mathf.Abs(Mathf.DeltaAngle(owner, miniSpin)),
                    Mathf.Abs(Mathf.DeltaAngle(prevOwner, miniSpin)));
                worstMismatch = Mathf.Max(worstMismatch, mismatch);
                prevOwner = owner;
                sampled++;
            }

            Debug.Log($"{LogPrefix} F2 결과 — 표본 {sampled}프레임({elapsed:F3}초), 주인 최대 회전각={maxAbsSpin:F1}도, " +
                $"미니와의 최대 불일치={worstMismatch:F3}도, 마지막 상태={bb.Machine.CurrentStateId}, " +
                $"던지기 웅크림={_pet.MiniTumblePostureAmount:F3}");

            Assert.Greater(sampled, 3,
                $"{LogPrefix} 전제 실패 — ThrowTumble이 곧바로 끝나 회전을 관측하지 못했습니다.");
            Assert.Greater(maxAbsSpin, 20f,
                $"{LogPrefix} 전제 실패 — 주인이 실제로 회전하지 않았습니다(최대 {maxAbsSpin:F1}도). " +
                "ThrowTumbleState 배선을 확인하세요.");
            Assert.Less(worstMismatch, 1f,
                $"{LogPrefix} ★ 미니의 몸통 회전이 주인의 루트 회전을 따라가지 않습니다" +
                $"(최대 불일치 {worstMismatch:F2}도). 수정 전에는 Quaternion.identity 하드코딩이었습니다.");
            Assert.Greater(Mathf.Abs(_pet.MiniSpinDegrees), 1f,
                $"{LogPrefix} ★ 마지막 프레임에 미니의 회전각이 사실상 0입니다({_pet.MiniSpinDegrees:F2}도) — " +
                "불일치 단언이 '둘 다 0'으로 통과하는 경우를 막는 양성 대조입니다.");
        }

        // ============================================================================
        // F3 — 무릎앉아 착지를 미니가 따라 웅크린다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F3_무릎앉아_착지에서_미니도_함께_웅크린다()
        {
            yield return SetUpLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(Screen.width * 0.5f, _dockTopOsY), 10f, _clonedConfig).x;
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);
            yield return WearMini();
            yield return new WaitForSeconds(1.0f);

            Transform miniBody = FindMiniBody();
            Assert.IsNotNull(miniBody, $"{LogPrefix} 전제 실패 — 그려진 미니 Transform(CharacterPet/Body)을 찾지 못했습니다.");
            float uprightGap = miniBody.position.y - _pet.PetWorldPosition.y;

            // 실제 경로와 같은 입력으로 무릎앉아에 진입시킨다(깊이/지속시간이 이 스냅샷에서 파생된다).
            bb.LastLandingFallHeight = bb.CharacterHeightWorld * 3f;
            bb.CurrentFootholdHandle = DockHandle;
            bb.Machine.ChangeState(StickmanStateId.LandingCrouch, isForcedInterrupt: true);

            // F2와 같은 이유로 <b>흘러간 게임 시간</b>으로 센다(무릎앉아 지속은 0.32~0.62초인데
            // batchmode 40프레임은 11ms라, 고정 프레임 반복은 눌림 구간에 들어가지도 못한다).
            float peakCrouch = 0f;
            float deepestDropWorld = 0f;
            float crouchSeconds = 0f;   // 주인이 실제로 무릎앉아 상태에 머문 시간(전제 확인용)
            float elapsed = 0f;
            int sampled = 0;
            while (elapsed < 0.9f && sampled < 20000)
            {
                yield return null;
                elapsed += Time.deltaTime;
                sampled++;
                if (bb.Machine.CurrentStateId != StickmanStateId.LandingCrouch) break;

                crouchSeconds += Time.deltaTime;
                peakCrouch = Mathf.Max(peakCrouch, _pet.MiniCrouchAmount);
                Transform body = FindMiniBody();
                if (body == null) continue;
                float gap = body.position.y - _pet.PetWorldPosition.y;
                deepestDropWorld = Mathf.Max(deepestDropWorld, uprightGap - gap);
            }

            float miniHeight = bb.CharacterHeightWorld * MiniScale;
            Debug.Log($"{LogPrefix} F3 결과 — 표본 {sampled}프레임({elapsed:F3}초), 무릎앉아 체류={crouchSeconds:F3}초, " +
                $"미니 최대 웅크림={peakCrouch:F3}, " +
                $"몸이 내려간 최대 거리={deepestDropWorld:F4}유닛(미니 키 {miniHeight:F3}유닛의 " +
                $"{(miniHeight > 0f ? deepestDropWorld / miniHeight * 100f : 0f):F1}%), 마지막 상태={bb.Machine.CurrentStateId}");

            // ★ 전제: 관측 구간이 눌림 구간(지속시간의 18%, 최소 0.32초 × 0.18 ≈ 58ms)을 <b>확실히</b>
            //   지났는가. batchmode 프레임이 1ms 미만이라 이 전제를 확인하지 않으면 "아직 앉기 전"을
            //   "안 앉는다"로 오독하게 된다 — 실제로 첫 실행에서 그렇게 가짜 실패가 났다.
            Assert.Greater(crouchSeconds, 0.15f,
                $"{LogPrefix} 전제 실패 — 무릎앉아 상태에 {crouchSeconds:F3}초밖에 머물지 못해 " +
                "눌림 구간을 관측하지 못했습니다(연출 지속은 0.32~0.62초).");
            Assert.Greater(peakCrouch, 0.3f,
                $"{LogPrefix} ★ 주인이 무릎앉아 중인데 미니는 직립 그대로입니다(최대 웅크림={peakCrouch:F3}). " +
                "CharacterPetRenderer가 LandingCrouchState.CurrentCrouchAmount를 읽고 있는지 확인하세요.");
            Assert.Greater(deepestDropWorld, miniHeight * 0.03f,
                $"{LogPrefix} ★ 웅크림 값만 움직이고 그림은 그대로입니다(내려간 거리={deepestDropWorld:F4}유닛). " +
                "값과 그림이 어긋나면 그것이 곧 '행동-그림 불일치'입니다.");
        }

        // ============================================================================
        // F4 — 신고 B: 창 가장자리에서 돌아서도 미니는 창 범위 안에 남는다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F4_창_가장자리에서_돌아서도_미니는_창_범위_안에_남는다()
        {
            yield return SetUpLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float height = bb.CharacterHeightWorld;
            float trail = height * MiniTrailInHeight;

            // 주인을 창의 오른쪽 끝 부근에 세우고 <b>왼쪽</b>을 보게 한다 = 미니의 목표 x가 오른쪽 바깥.
            float ownerX = _windowRightWorldX - height * 0.2f;
            Place(ownerX, _windowTopWorldY, WindowHandle, StickmanStateId.Idle);
            bb.SetFacingSign(-1f);
            yield return new WaitForSeconds(0.5f);

            yield return WearMini();
            yield return new WaitForSeconds(1.5f);   // 지수 감쇠(rate 3)가 충분히 수렴할 시간.

            Vector2 mini = _pet.PetWorldPosition;
            float inset = Mathf.Min(height * MiniScale * 0.5f, (_windowRightWorldX - _windowLeftWorldX) * 0.5f);

            Debug.Log($"{LogPrefix} F4 결과 — 주인=({bb.Body.position.x:F3},{bb.Body.position.y:F3}) facing={bb.FacingSign:F0} " +
                $"미니=({mini.x:F3},{mini.y:F3}) 창 가로=[{_windowLeftWorldX:F3}, {_windowRightWorldX:F3}] " +
                $"안쪽여백={inset:F3} 상태={bb.Machine.CurrentStateId} 발판핸들={bb.CurrentFootholdHandle}");

            Assert.AreEqual(WindowHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 전제 실패 — 주인이 창 발판을 딛고 있지 않습니다(핸들={bb.CurrentFootholdHandle}).");
            Assert.AreEqual(-1f, bb.FacingSign,
                $"{LogPrefix} 전제 실패 — 주인이 왼쪽을 보고 있지 않아 미니가 오른쪽 바깥으로 밀리는 조건이 아닙니다.");

            Assert.That(mini.x, Is.LessThanOrEqualTo(_windowRightWorldX - inset + 0.01f),
                $"{LogPrefix} ★ 미니가 창 오른쪽 바깥에 있습니다(미니x={mini.x:F3}, 창 오른쪽 끝={_windowRightWorldX:F3}). " +
                "사용자 신고 '창 위에 있을 때 창 범위 안에 있어야 하는데 공중에 떠 있음' 그대로입니다.");
            Assert.That(mini.x, Is.GreaterThanOrEqualTo(_windowLeftWorldX + inset - 0.01f),
                $"{LogPrefix} ★ 미니가 창 왼쪽 바깥에 있습니다(미니x={mini.x:F3}, 창 왼쪽 끝={_windowLeftWorldX:F3}).");
            Assert.That(mini.y, Is.EqualTo(_windowTopWorldY).Within(0.35f),
                $"{LogPrefix} 미니가 창 상단에 서 있지 않습니다(미니y={mini.y:F3}, 창 상단={_windowTopWorldY:F3}).");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — F4의 배치에서 <b>제한이 없었다면</b> 미니의 목표 x가 실제로 창 밖이라는
        /// 것을 같은 값으로 직접 계산해 보인다. 이게 없으면 F4는 "원래부터 안쪽이라 항상 참"일 수 있다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F4n_네거티브컨트롤_제한이_없었다면_목표x는_창_밖이다()
        {
            yield return SetUpLayout();

            StickmanBlackboard bb = _agent.Blackboard;
            float height = bb.CharacterHeightWorld;
            float trail = height * MiniTrailInHeight;

            float ownerX = _windowRightWorldX - height * 0.2f;
            Place(ownerX, _windowTopWorldY, WindowHandle, StickmanStateId.Idle);
            bb.SetFacingSign(-1f);
            yield return new WaitForSeconds(0.5f);
            yield return WearMini();
            yield return new WaitForSeconds(1.0f);

            // 수정 전 TickMini의 목표 x 공식 그대로: 주인 x − facing × 신장 × 0.75.
            float legacyTargetX = bb.Body.position.x - bb.FacingSign * trail;
            float overshoot = legacyTargetX - _windowRightWorldX;

            Debug.Log($"{LogPrefix} F4n 결과 — 옛 목표x={legacyTargetX:F3}, 창 오른쪽 끝={_windowRightWorldX:F3}, " +
                $"바깥으로 나간 거리={overshoot:F3}유닛(신장 {height:F3}의 {overshoot / height * 100f:F0}%), " +
                $"실제 미니x={_pet.PetWorldPosition.x:F3}");

            Assert.Greater(overshoot, 0f,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 옛 공식의 목표 x가 창 안쪽입니다({legacyTargetX:F3}). " +
                "이 배치가 신고 조건을 재현하지 못한다는 뜻이므로 F4의 의미도 사라집니다.");
            Assert.Less(_pet.PetWorldPosition.x, legacyTargetX,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 지금 미니가 옛 공식과 같은 자리에 있습니다. " +
                "제한이 실제로 작동하지 않은 것입니다.");
        }
    }
}
