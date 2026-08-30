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
    /// ★ "몸 순간이동 창구 통일"(2026-08-29 후속) 회귀 잠금.
    ///
    /// 배경(Tasklist.md "PlayMode 회귀 1건 진단·수정"): 이 프로젝트는
    /// ProjectSettings/Physics2DSettings.asset의 m_AutoSyncTransforms가 **0(꺼짐)**이다. 그래서
    /// 상태가 Rigidbody2D.position에만 대입하면 **그 프레임에 화면에 그려지는 Transform은 옛 좌표에
    /// 남는다**. 프레임 순서가 FixedUpdate(물리 적분) → Update(상태 Tick = 순간이동) → 렌더이므로,
    /// 그 한 프레임은 "가야 할 곳"이 아니라 "물리가 방금 적분해 둔 곳"이 그려진다(1프레임 팝).
    /// 착지 스냅(FallState)에서 실제로 8.82pt 잉크 이탈로 터졌던 버그이고, 그 수습으로
    /// StickmanBlackboard.MoveBodyToWorld(Vector2)가 **몸 순간이동의 유일한 창구**로 신설됐다.
    ///
    /// 이 파일이 잠그는 것은 "나머지 순간이동 지점들도 그 창구를 쓰는가"다. 검증 대상 3종:
    ///   (1) RunawayState  — 은신처로 순간이동 / 원래 자리로 복귀 순간이동(화면을 가로지르는 최대 이동)
    ///   (2) LedgeHangState — 매달린 채 유지(붙잡은 **창이 움직이면** 그 이동량만큼의 순간이동이 된다)
    ///   (3) ParkourClimbState — 등반 보간 중 **창이 움직이면** 맨틀 목표가 갱신되며 크게 튄다
    ///
    /// 계측 방식(모든 테스트 공통, 새 훅 없이 기존 public 표면만 사용):
    ///   · "물리가 아는 좌표" = Rigidbody2D.position
    ///   · "화면에 그려지는 좌표" = 그 Rigidbody2D의 **Transform.position** (렌더러들이 이 루트의
    ///     자식이므로 실제 픽셀은 전적으로 이 값에서 나온다 — Core/StickmanAgent.SetRenderersEnabled가
    ///     토글하는 Renderer 배열이 곧 이 루트의 자식 렌더러들이다)
    ///   · 코루틴의 `yield return null` 재개 지점은 그 프레임의 모든 Update() **뒤**이므로, 여기서 읽는
    ///     값이 곧 **이번 프레임에 렌더될 값**이다(씬의 Rigidbody2D는 m_Interpolate: 0이라 보간 지연도 없다).
    ///
    /// 네거티브 컨트롤: 각 테스트는 성공 단언 직후, **바로 그 상태·그 물리 조건에서** 수정 전 코드
    /// (`Body.position`만 대입)를 한 줄 그대로 실행해 화면 좌표가 실제로 뒤처지는지 측정한다. 이게
    /// 없으면 "계측기가 원래 아무것도 못 잡는 것"과 "수정이 유효한 것"을 구분할 수 없다
    /// (Tests/PlayMode/GroundSnapTeleportTests.cs의 "상한 제거 시" 네거티브 컨트롤과 같은 관례).
    /// 프레임 경계를 넘기지 않고 같은 프레임 안에서 측정하는 이유는, 다음 물리 스텝이 Transform을
    /// 되쓰면서 증거를 지우기 때문이다 — 실제 버그도 정확히 그 한 프레임짜리다.
    /// </summary>
    public sealed class BodyTeleportTransformSyncTests
    {
        private const string LogPrefix = "[TELEPORT-SYNC]";

        /// <summary>"물리 좌표 == 그려지는 좌표"로 인정하는 허용 오차(월드 유닛). MoveBodyToWorld는
        /// 두 값을 같은 프레임에 같은 값으로 쓰므로 정상이면 정확히 0이다 — 여유는 부동소수 잡음용.</summary>
        private const float SyncToleranceWorld = 0.001f;

        /// <summary>네거티브 컨트롤이 "확실히 어긋났다"고 인정하는 최소 거리(월드 유닛).</summary>
        private const float DesyncEvidenceWorld = 0.05f;

        private const float SettleWaitSeconds = 2.5f;

        private const long UpperHandle = 9101L;
        private const long LowerHandle = 9102L;
        private const long DockHandle = 9201L;
        private const long RightFloorHandle = 9202L;

        /// <summary>★ Dock 상단 → 바닥 안전망 상단 낙차(월드 유닛). **하드코딩하지 않는다** —
        /// Core/DockGeometry.cs가 (tilesize + dockThicknessTilePaddingPoints − BottomSafetyNetInsetPoints)를
        /// 월드로 환산해 주는 단일 소스다(이 개발 머신 tilesize=49 → 67pt → 1.63747유닛).
        /// 2026-08-30 횡단 리뷰 M1: 이 값이 파일마다 0.855(안전망이 40pt 위였던 시절의 화석) / 1.6375로
        /// 갈라져 있었고, 그 탓에 배율 불변식 테스트가 실제 시스템이 아니라 자기 상수를 지키고 있었다.</summary>
        private static readonly float DockDropUnits = DockGeometry.ReferenceDockDropWorldUnits;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested { get; set; }
            public bool HopDownRequested { get; set; }
            public bool StepUpRequested { get; set; }
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private FootholdPoller _poller;
        private ScriptedIntentSource _intent;
        private Renderer[] _renderers;

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
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            SpectacleEventLock.Release(this);
            _agent = null;
        }

        // ============================================================================
        // 공통 준비/계측 도우미
        // ============================================================================

        /// <summary>씬을 띄우고 캐릭터가 안착할 때까지 기다린 뒤 설정 복제본을 꽂는다.</summary>
        private IEnumerator LoadSceneAndSettle()
        {
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

            _renderers = _agent.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(_renderers.Length, 0, $"{LogPrefix} 캐릭터 렌더러를 하나도 찾지 못했습니다.");
        }

        /// <summary>지금 이 프레임에 캐릭터가 실제로 그려지는가(렌더러가 하나라도 켜져 있는가).</summary>
        private bool AnyRendererEnabled()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].enabled) return true;
            }
            return false;
        }

        private static Vector2 DrawnPos(StickmanBlackboard bb) => bb.Body.transform.position;

        /// <summary>물리 좌표와 그려지는 좌표의 거리(월드 유닛).</summary>
        private static float SyncError(StickmanBlackboard bb) => Vector2.Distance(DrawnPos(bb), bb.Body.position);

        /// <summary>
        /// 네거티브 컨트롤 — **수정 전 코드 그대로**(Rigidbody2D.position에만 대입) 몸을 옮겨보고,
        /// 그 즉시(같은 프레임, 렌더 전) 화면에 그려질 좌표가 얼마나 뒤처지는지 잰다. 측정이 끝나면
        /// MoveBodyToWorld로 원상 복구해 본 시나리오를 오염시키지 않는다.
        /// </summary>
        private void RunLegacyWriteNegativeControl(StickmanBlackboard bb, Vector2 target, string label)
        {
            Vector2 before = bb.Body.position;
            Vector2 drawnBefore = DrawnPos(bb);

            bb.Body.position = target;                       // ← 수정 전 코드(창구를 안 거치는 대입)
            float legacyError = Vector2.Distance(DrawnPos(bb), target);

            bb.MoveBodyToWorld(target);                      // ← 수정 후 코드(유일한 창구)
            float fixedError = Vector2.Distance(DrawnPos(bb), target);

            Debug.Log($"{LogPrefix} 네거티브 컨트롤({label}) — 옮기기 전 물리=({before.x:F3},{before.y:F3}) " +
                $"그려지는={drawnBefore.x:F3},{drawnBefore.y:F3} / 목표=({target.x:F3},{target.y:F3}) / " +
                $"수정 전 방식 오차={legacyError:F4}유닛, 창구 사용 시 오차={fixedError:F4}유닛.");

            Assert.Greater(legacyError, DesyncEvidenceWorld,
                $"{LogPrefix} 네거티브 컨트롤 실패({label}) — 수정 전 방식(Body.position만)으로 옮겼는데도 " +
                $"그려지는 좌표가 {legacyError:F4}유닛밖에 어긋나지 않았습니다. 계측기가 desync를 감지하지 " +
                "못하고 있으므로, 이 파일의 성공 단언들도 신뢰할 수 없습니다(autoSyncTransforms가 켜졌거나 " +
                "Transform을 대신 써주는 다른 경로가 생겼을 수 있음 — 그렇다면 이 테스트 전제를 다시 세울 것).");
            Assert.Less(fixedError, SyncToleranceWorld,
                $"{LogPrefix} MoveBodyToWorld가 Transform을 함께 쓰지 않았습니다(오차 {fixedError:F4}유닛) — " +
                "창구 자체의 회귀입니다.");

            bb.MoveBodyToWorld(before); // 원상 복구(시나리오 오염 방지).
        }

        // ============================================================================
        // (1) RunawayState — 은신처 순간이동 / 복귀 순간이동
        // ============================================================================

        [UnityTest]
        public IEnumerator RunawayHideAndReturnKeepDrawnPositionInSyncWithBody()
        {
            yield return LoadSceneAndSettle();
            StickmanBlackboard bb = _agent.Blackboard;

            // 다른 스펙터클(라이벌/활쏘기 등)이 끼어들어 Runaway를 중단시키지 않도록 잠근다.
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Runaway, this),
                $"{LogPrefix} 스펙터클 락을 잡지 못했습니다.");

            _clonedConfig.runawayFleeDurationSeconds = 0.3f; // 은신까지의 대기만 줄인다(로직은 그대로).

            Camera cam = bb.MainCamera;
            Vector2 preHide = bb.Body.position;

            // 은신처 — 화면 좌상단 안쪽(12%, 18%). 화면 하드 클램프(EnforceScreenBoundsAndRescue)에
            // 걸리지 않을 만큼 안쪽이면서, 원래 자리와 화면을 가로지를 만큼 떨어져 있어야 한다.
            _ = ScreenCoordinateConverter.WorldToOsScreen(cam, preHide, _clonedConfig, out float depth);
            Vector3 hideWorld = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(Screen.width * 0.12f, Screen.height * 0.18f), depth, _clonedConfig);
            var hideSpot = new Vector2(hideWorld.x, hideWorld.y);

            Assert.Greater(Vector2.Distance(preHide, hideSpot), 1f,
                $"{LogPrefix} 전제 실패 — 은신처가 원래 자리와 너무 가까워 이 테스트가 의미를 잃습니다.");

            bb.PendingRunawayHideWorldPos = hideSpot;
            bb.Machine.ChangeState(StickmanStateId.Runaway, isForcedInterrupt: true);

            // ── ① 은신 진입: 렌더러가 꺼지는 그 프레임을 잡는다.
            bool sawVisibleDesync = false;
            float worstVisibleError = 0f;
            bool hidden = false;
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                yield return null;
                elapsed += Time.deltaTime;

                // 불변식: **보이는 동안에는** 언제나 그려지는 좌표 == 물리 좌표.
                if (AnyRendererEnabled())
                {
                    float err = SyncError(bb);
                    if (err > worstVisibleError) worstVisibleError = err;
                    if (err > SyncToleranceWorld) sawVisibleDesync = true;
                }
                else
                {
                    hidden = true;
                    break;
                }
            }

            Assert.IsTrue(hidden, $"{LogPrefix} {elapsed:F2}초 안에 은신(렌더러 꺼짐)에 도달하지 못했습니다 — " +
                $"상태={bb.Machine.CurrentStateId}. RunawayState 진입/페이즈 회귀 의심.");

            Vector2 drawnAtHide = DrawnPos(bb);
            Debug.Log($"{LogPrefix} 은신 진입 프레임 — 물리=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), " +
                $"그려지는=({drawnAtHide.x:F3},{drawnAtHide.y:F3}), 은신처=({hideSpot.x:F3},{hideSpot.y:F3}), " +
                $"렌더러 켜짐={AnyRendererEnabled()}. 은신 전 최악 desync={worstVisibleError:F4}유닛.");

            Assert.IsFalse(sawVisibleDesync,
                $"{LogPrefix} 캐릭터가 **보이는 상태로** 물리 좌표와 최대 {worstVisibleError:F4}유닛 어긋난 " +
                "프레임이 있었습니다(그 프레임은 엉뚱한 위치에 그려집니다).");
            Assert.Less(Vector2.Distance(bb.Body.position, hideSpot), SyncToleranceWorld,
                $"{LogPrefix} 은신처로 몸이 옮겨지지 않았습니다.");
            Assert.Less(Vector2.Distance(drawnAtHide, hideSpot), SyncToleranceWorld,
                $"{LogPrefix} 은신 순간이동이 Transform을 함께 쓰지 않았습니다 — " +
                $"그려지는 좌표가 은신처에서 {Vector2.Distance(drawnAtHide, hideSpot):F4}유닛 떨어져 있습니다. " +
                "이 프레임은 렌더러가 꺼져 있어 눈에 띄지 않지만, 바로 다음의 '발견됨'에서 캐릭터가 " +
                "은신처가 아닌 곳에 나타나는 원인이 됩니다(RunawayState.HideCharacterAtHideSpot).");

            // ── ② 네거티브 컨트롤: 지금(Kinematic·은신 중) 수정 전 방식으로 옮기면 화면 좌표가 뒤처지는가.
            RunLegacyWriteNegativeControl(bb, preHide, "가출-은신중");

            // 참고 계측: 수정 전 방식의 어긋남이 **얼마나 오래** 남는가(다음 물리 스텝이 지워주는가).
            // Kinematic + 속도 0이면 물리가 이 바디를 되쓸 이유가 없어 오래 남을 수 있다.
            Vector2 restorePoint = bb.Body.position;
            bb.Body.position = preHide; // 수정 전 코드
            float errImmediate = SyncError(bb);
            yield return null;
            float errNextFrame = SyncError(bb);
            // 배치모드는 프레임이 1ms 수준이라 "몇 프레임"으로는 물리 스텝(고정 0.02초)이 한 번도 안 돌 수
            // 있다 — 되쓰기 여부를 가리려면 반드시 **실시간**으로 여러 고정 스텝을 지나야 한다.
            yield return new WaitForSeconds(0.3f);
            float errAfterPhysics = SyncError(bb);
            Debug.Log($"{LogPrefix} 잔존 계측(은신 중 Kinematic, 고정 스텝 {Time.fixedDeltaTime:F3}초) — " +
                $"수정 전 방식 어긋남: 즉시 {errImmediate:F4} / 1프레임 뒤 {errNextFrame:F4} / " +
                $"0.3초(약 {(0.3f / Mathf.Max(0.0001f, Time.fixedDeltaTime)):F0}스텝) 뒤 {errAfterPhysics:F4}유닛. " +
                "0으로 수렴하면 물리 되쓰기가 나중에 지워준다는 뜻(=1프레임짜리 팝)이고, 그대로 남아 " +
                "있으면 **은신 내내 엉뚱한 좌표에 그려진다**는 뜻이다(=발견 순간 다른 곳에서 나타남).");
            bb.MoveBodyToWorld(restorePoint);

            // ── ③ 복귀 순간이동: 렌더러가 다시 켜지는 그 프레임에 이미 원래 자리여야 한다.
            bb.RunawayManualRecallSignaled = true;

            bool revealed = false;
            Vector2 drawnAtReveal = Vector2.zero;
            Vector2 bodyAtReveal = Vector2.zero;
            elapsed = 0f;
            while (elapsed < 3f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (AnyRendererEnabled())
                {
                    revealed = true;
                    drawnAtReveal = DrawnPos(bb);
                    bodyAtReveal = bb.Body.position;
                    break;
                }
            }

            Assert.IsTrue(revealed, $"{LogPrefix} 수동 소환 뒤 {elapsed:F2}초 안에 캐릭터가 다시 보이지 않았습니다 — " +
                $"상태={bb.Machine.CurrentStateId}.");

            float revealError = Vector2.Distance(drawnAtReveal, preHide);
            Debug.Log($"{LogPrefix} 복귀(다시 보이는) 첫 프레임 — 그려지는=({drawnAtReveal.x:F3},{drawnAtReveal.y:F3}), " +
                $"물리=({bodyAtReveal.x:F3},{bodyAtReveal.y:F3}), 원래 자리=({preHide.x:F3},{preHide.y:F3}), " +
                $"은신처=({hideSpot.x:F3},{hideSpot.y:F3}), 원래 자리와의 오차={revealError:F4}유닛.");

            Assert.Less(revealError, SyncToleranceWorld,
                $"{LogPrefix} 캐릭터가 다시 보이는 **첫 프레임**이 원래 자리가 아닌 곳에 그려졌습니다" +
                $"(오차 {revealError:F4}유닛, 은신처까지의 거리는 {Vector2.Distance(drawnAtReveal, hideSpot):F4}유닛). " +
                "RunawayState.RestoreCharacter가 (a) 몸을 옮기기 전에 렌더러를 켰거나 (b) MoveBodyToWorld 대신 " +
                "Body.position만 썼을 때 화면 모서리에서 한 프레임 번쩍이는 바로 그 증상입니다.");
            Assert.Less(SyncError(bb), SyncToleranceWorld,
                $"{LogPrefix} 복귀 프레임에서 물리 좌표와 그려지는 좌표가 어긋났습니다.");
        }

        // ============================================================================
        // (2) LedgeHangState — 매달린 채 **붙잡은 창이 움직였을 때**
        // ============================================================================

        [UnityTest]
        public IEnumerator LedgeHangFollowsMovedWindowWithoutDrawnPositionLag()
        {
            yield return LoadSceneAndSettle();
            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;

            float w = Screen.width;
            float h = Screen.height;
            float upperTopOs = h * 0.25f;
            float lowerTopOs = h * 0.85f;

            // 위쪽 창(부분 폭) + 아래 바닥(전체 폭) — Tests/PlayMode/LedgeHangDescentTests.cs와 같은 배치.
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(UpperHandle, new Rect(w * 0.20f, upperTopOs, w * 0.40f, h * 0.30f), true));
            _service.Footholds.Add(new PlatformFoothold(LowerHandle, new Rect(0f, lowerTopOs, w, h * 0.15f), false));
            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            // 준비 구간에서는 0 — 한 프레임이라도 걸어 모서리 밖으로 나가면 전제가 깨진다.
            // StickmanBlackboard.MoveInputX는 IntentSource를 그대로 읽는 계산 프로퍼티라, 방향은
            // ChangeState 직전에 세워도 Enter()가 곧바로 읽는다(프레임 대기 불필요).
            _intent = new ScriptedIntentSource { MoveInputX = 0f };
            bb.IntentSource = _intent;

            // 매달림 유지시간을 넉넉히 — 창을 옮길 시간이 필요하다(로직이 아니라 시간만 조정).
            _clonedConfig.ledgeHangHoldDurationMin = 2.5f;
            _clonedConfig.ledgeHangHoldDurationMax = 2.5f;
            _clonedConfig.ledgeHangMaxDuration = 6f;

            Vector3 standWorld = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(w * 0.60f - 5f, upperTopOs), 10f, _clonedConfig);
            bb.MoveBodyToWorld(standWorld);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = UpperHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null; // 발판 주입이 한 번 Tick된 상태에서 판정하기 위한 1프레임.

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 전제 실패 — 위쪽 창에 접지하지 못했습니다.");
            Assert.IsTrue(bb.TryFindDescendTarget(info, 1, out long descendTarget, out _),
                $"{LogPrefix} 전제 실패 — 오른쪽 모서리 아래에서 내려갈 발판을 찾지 못했습니다.");
            Assert.AreEqual(LowerHandle, descendTarget, $"{LogPrefix} 전제 실패 — 내려갈 발판이 아래 바닥이 아닙니다.");

            _intent.MoveInputX = 1f; // 오른쪽 모서리에 매달린다.
            bb.Machine.ChangeState(StickmanStateId.LedgeHang, isForcedInterrupt: true);

            // Grabbing(0.28초 보간)이 끝나 Hanging(매 프레임 같은 자리 유지)에 들어갈 때까지 기다린다.
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 매달리기가 유지되지 않았습니다(현재 {bb.Machine.CurrentStateId}) — 유지시간 설정 확인.");

            Vector2 bodyBeforeMove = bb.Body.position;
            Assert.Less(SyncError(bb), SyncToleranceWorld,
                $"{LogPrefix} 창을 옮기기 전부터 이미 어긋나 있었습니다(오차 {SyncError(bb):F4}유닛).");

            // ── 붙잡은 창을 한 프레임에 오른쪽으로 크게 옮긴다(사용자가 창을 드래그하는 상황).
            const float MoveOsPx = 80f;
            Rect moved = _service.Footholds[0].ScreenRect;
            moved.x += MoveOsPx;
            _service.Footholds[0] = new PlatformFoothold(UpperHandle, moved, true);
            _poller.PollImmediately();

            yield return null; // 이 프레임의 LedgeHangState.Tick이 몸을 새 모서리로 옮긴다.

            Vector2 bodyAfterMove = bb.Body.position;
            float travelled = Vector2.Distance(bodyAfterMove, bodyBeforeMove);
            float syncError = SyncError(bb);

            Debug.Log($"{LogPrefix} 매달린 창 이동 — 창을 {MoveOsPx:F0}pt 옮긴 그 프레임에 몸이 {travelled:F3}유닛 " +
                $"순간이동. 물리=({bodyAfterMove.x:F3},{bodyAfterMove.y:F3}), " +
                $"그려지는=({DrawnPos(bb).x:F3},{DrawnPos(bb).y:F3}), 오차={syncError:F4}유닛, " +
                $"상태={bb.Machine.CurrentStateId}.");

            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 창을 옮겼더니 매달리기가 풀렸습니다 — 이 테스트는 '창을 따라간다'는 전제 위에 있습니다.");
            Assert.Greater(travelled, DesyncEvidenceWorld,
                $"{LogPrefix} 전제 실패 — 창을 {MoveOsPx:F0}pt 옮겼는데 몸이 {travelled:F4}유닛밖에 움직이지 " +
                "않았습니다. 순간이동이 일어나지 않았다면 이 테스트는 아무것도 검증하지 못합니다.");
            Assert.Less(syncError, SyncToleranceWorld,
                $"{LogPrefix} 매달린 몸이 창을 따라 {travelled:F3}유닛 옮겨진 프레임에서 화면 좌표가 " +
                $"{syncError:F4}유닛 뒤처졌습니다 — LedgeHangState가 MoveBodyToWorld 창구를 쓰지 않았습니다.");

            RunLegacyWriteNegativeControl(bb, bodyAfterMove + new Vector2(0.5f, 0f), "매달리기-창이동");
        }

        // ============================================================================
        // (3) ParkourClimbState — 등반 보간 중 **오르던 창이 움직였을 때**
        // ============================================================================

        [UnityTest]
        public IEnumerator ParkourClimbFollowsMovedWindowWithoutDrawnPositionLag()
        {
            yield return LoadSceneAndSettle();
            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;

            float w = Screen.width;
            float h = Screen.height;
            float dockTopOs = h * 0.55f;

            // Dock 역할의 턱(가로 30%~70%) + 그 오른쪽 바깥의 낮은 바닥(70%~90%).
            // 캐릭터는 낮은 바닥에서 **왼쪽으로** 턱을 타고 오른다(EdgeHopDownTests의 되올라가기와 같은 배치).
            Vector3 dockTopWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, dockTopOs), 10f, _clonedConfig);
            Vector2 floorOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(dockTopWorld.x, dockTopWorld.y - DockDropUnits), _clonedConfig, out _);
            float floorTopOs = floorOs.y;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(DockHandle, new Rect(w * 0.30f, dockTopOs, w * 0.40f, h - dockTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(RightFloorHandle, new Rect(w * 0.70f, floorTopOs, w * 0.20f, h - floorTopOs), false));
            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            _intent = new ScriptedIntentSource { MoveInputX = 0f }; // 위 매달리기 테스트와 같은 이유.
            bb.IntentSource = _intent;

            _clonedConfig.parkourClimbDuration = 1.5f; // 창을 옮길 시간 확보(보간 로직 자체는 그대로).

            float dockRightWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.70f, dockTopOs), 10f, _clonedConfig).x;
            float floorTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.85f, floorTopOs), 10f, _clonedConfig).y;

            // 턱 모서리에서 0.25유닛 안쪽 — parkourDetectionRadius(0.5) 안이면서, 준비 프레임에
            // 미세하게 밀려도 발판 밖으로 나가지 않는 거리.
            bb.MoveBodyToWorld(new Vector2(dockRightWorldX + 0.25f, floorTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = RightFloorHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 전제 실패 — 낮은 바닥에 접지하지 못했습니다.");
            Assert.IsTrue(bb.TryFindClimbableWall(info, -1, out long wallHandle, out float wallTopY),
                $"{LogPrefix} 전제 실패 — 왼쪽에서 오를 벽(턱)을 찾지 못했습니다.");
            Assert.AreEqual(DockHandle, wallHandle, $"{LogPrefix} 전제 실패 — 오를 벽이 턱이 아닙니다.");

            _intent.MoveInputX = -1f; // 왼쪽 턱을 타고 오른다.
            bb.Machine.ChangeState(StickmanStateId.ParkourClimb, isForcedInterrupt: true);

            // 등반 진행도가 충분히 쌓일 때까지 기다린다. 이 상태의 x는 Lerp(시작x, 맨틀목표x, 진행도)라
            // **진행도가 곧 창 이동량의 반영 비율**이다 — 진입 직후(진행도≈0)에 창을 옮기면 몸은 거의
            // 움직이지 않아 아무것도 검증하지 못한다(첫 시도에서 실측으로 확인: 4.5유닛 목표 이동이
            // 몸에는 0.007유닛으로만 반영됐다). 1.5초 중 0.9초 = 진행도 0.6.
            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual(StickmanStateId.ParkourClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 등반 도중 {bb.Machine.CurrentStateId}로 빠졌습니다 — 벽 재확인 실패 의심 " +
                $"(벽 상단 Y={wallTopY:F3}).");

            Vector2 bodyBeforeMove = bb.Body.position;

            // ── 오르던 창을 한 프레임에 왼쪽으로 크게 옮긴다 → 맨틀 목표 x가 그만큼 갱신된다.
            const float MoveOsPx = 90f;
            Rect moved = _service.Footholds[0].ScreenRect;
            moved.x -= MoveOsPx;
            _service.Footholds[0] = new PlatformFoothold(DockHandle, moved, true);
            _poller.PollImmediately();

            yield return null;

            Vector2 bodyAfterMove = bb.Body.position;
            float travelledX = Mathf.Abs(bodyAfterMove.x - bodyBeforeMove.x);
            float syncError = SyncError(bb);

            Debug.Log($"{LogPrefix} 등반 중 창 이동 — 창을 {MoveOsPx:F0}pt 옮긴 그 프레임에 몸이 가로로 " +
                $"{travelledX:F3}유닛 이동. 물리=({bodyAfterMove.x:F3},{bodyAfterMove.y:F3}), " +
                $"그려지는=({DrawnPos(bb).x:F3},{DrawnPos(bb).y:F3}), 오차={syncError:F4}유닛, " +
                $"상태={bb.Machine.CurrentStateId}.");

            Assert.AreEqual(StickmanStateId.ParkourClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 창을 옮겼더니 등반이 중단됐습니다 — 이 테스트는 '창을 따라간다'는 전제 위에 있습니다.");
            Assert.Greater(travelledX, DesyncEvidenceWorld,
                $"{LogPrefix} 전제 실패 — 창을 {MoveOsPx:F0}pt 옮겼는데 몸이 가로로 {travelledX:F4}유닛밖에 " +
                "움직이지 않았습니다(맨틀 목표 갱신 회귀 의심).");
            Assert.Less(syncError, SyncToleranceWorld,
                $"{LogPrefix} 등반 중 몸이 {travelledX:F3}유닛 옮겨진 프레임에서 화면 좌표가 {syncError:F4}유닛 " +
                "뒤처졌습니다 — ParkourClimbState가 MoveBodyToWorld 창구를 쓰지 않았습니다.");

            RunLegacyWriteNegativeControl(bb, bodyAfterMove + new Vector2(-0.5f, 0.3f), "벽타기-창이동");
        }
    }
}
