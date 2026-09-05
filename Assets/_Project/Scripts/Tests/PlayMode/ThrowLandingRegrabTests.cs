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
    /// ★ 2026-09-05 사용자 신고(Windows) — "던지기 → 착지 → 무릎앉기 → 일어선 뒤 캐릭터가 멈추고
    /// 마우스로 잡아끌어도 반응이 없다가 1~2초 뒤 저절로 풀린다(간헐)". 조사 문서:
    /// docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md.
    ///
    /// ============================================================================
    /// 이 테스트가 가르는 것 — <b>플랫폼 중립 로직에 "일어선 뒤 잡기 거부" 창이 있는가</b>
    /// ============================================================================
    /// 신고를 설명하는 가설은 두 갈래다. (A) Windows 쪽 동기 스톨/입력 경로, (B) 우리 상태기계·락·
    /// 배회 타이머가 만드는 <b>설계된 거부 창</b>. 이 개발 머신에는 Windows 실기가 없으므로 (A)는
    /// 사용자 로그로만 갈릴 수 있지만, (B)는 <b>여기서 실행으로 갈린다</b>: 실제 드래그&던지기 경로
    /// (StickmanClickHitbox → DragThrowController → SpectacleEventLock → LocalClickCapture →
    /// Dragged)로 던지고, 무릎앉기가 끝나 Idle로 돌아온 <b>바로 그 프레임</b>에 같은 실제 경로로
    /// 다시 잡는다. 여기서 Dragged로 즉시 들어가면 (B)의 "일어선 뒤 잠금"은 존재하지 않는다 —
    /// 남는 거부 창은 <b>ThrowTumble/LandingCrouch 체류 시간 자체</b>뿐이고, 그 길이는 두 번째
    /// 테스트가 숫자로 남긴다.
    ///
    /// <para>이 테스트가 <b>초록이어도 Windows 신고가 해결된 것은 아니다</b>. 초록의 뜻은
    /// "우리 상태기계에는 잠금이 없다 = 원인은 Windows 쪽(입력 도달/좌표 변환/메인 스레드 스톨)에
    /// 있다"이고, 그 판정표는 위 문서에 있다. 빨강이면 (B)가 실재하며 문서의 결론이 바뀐다.</para>
    ///
    /// <para>시간 예산은 전부 벽시계 초다(CLAUDE.md — 배치모드 PlayMode는 2,000fps 이상이라
    /// 프레임 수 대기는 무의미하다). 픽스처는 ThrowTumbleTests와 같은 배치(화면 전폭 발판 1장)를
    /// 그대로 쓴다 — 같은 판 위에서 재야 "던지기 자체는 정상"이라는 그쪽 초록과 비교가 된다.</para>
    /// </summary>
    public sealed class ThrowLandingRegrabTests
    {
        private const string LogPrefix = "[착지후재잡기-TEST]";
        private const long FlatGroundHandle = 9401L;

        private const float SettleWaitSeconds = 2.0f;
        private const float MaxFlightObserveSeconds = 8f;

        /// <summary>일어선 뒤 잡기 요청이 Dragged로 확정되기까지 허용하는 벽시계 상한(초).
        /// 실제 경로는 OnMouseDown 안에서 <b>동기</b>로 ChangeState하므로 정상이면 0프레임이다.
        /// 여기 값은 "사용자가 1~2초를 봤다"는 신고와 <b>한 자릿수 이상</b> 떨어진 자다 — 이보다
        /// 늦으면 우리 로직에 거부 창이 있다는 뜻이고, 그 이하면 신고의 원인은 우리 로직 밖이다.</summary>
        private const float RegrabAcceptBudgetSeconds = 0.25f;

        /// <summary>Idle 복귀 뒤 상태가 흔들리지 않는지(GroundLossHang/Fall 재진입 등) 지켜보는 창(초).
        /// 신고된 "1~2초"를 덮는 길이다.</summary>
        private const float PostLandingWatchSeconds = 2.0f;

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
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private StickmanClickHitbox _hitbox;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private CursorPositionQuery _originalCursor;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private ScriptedIntentSource _intent;
        private float _groundWorldY;
        private float _characterHeight;
        private Vector2 _cursorWorld;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                StickmanBlackboard bb = _agent.Blackboard;
                // 잡은 채로 끝났으면 놓아 준다 — Dragged를 벗어나는 전이가 DragThrowController의
                // SpectacleEventLock/클릭캡처 해제를 유발한다(정적 락이라 다음 테스트로 샌다).
                if (bb.Machine != null && bb.Machine.CurrentStateId == StickmanStateId.Dragged)
                {
                    bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                }
                if (_originalConfig != null) bb.Config = _originalConfig;
                if (_originalIntent != null) bb.IntentSource = _originalIntent;
                if (_originalPoller != null) bb.FootholdPoller = _originalPoller;
                bb.CursorProvider = _originalCursor;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _hitbox = null;
        }

        // ============================================================================
        // 공통 준비 — ThrowTumbleTests.SetUpFlatGround와 같은 배치
        // ============================================================================

        private IEnumerator SetUpFlatGround()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            _hitbox = _agent.GetComponent<StickmanClickHitbox>();
            Assert.IsNotNull(_hitbox, $"{LogPrefix} StickmanAgent에 StickmanClickHitbox가 없습니다 — 실제 클릭 경로를 태울 수 없습니다.");

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _originalCursor = bb.CursorProvider;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            // 로데오 자동 발동은 이 테스트의 관심 밖이고, 커서가 캐릭터 옆에 오래 머무는 순간 상태를
            // 가로챌 수 있어 명시적으로 끈다(기본값도 OFF지만 애셋이 바뀌어도 이 테스트는 그대로다).
            _clonedConfig.rodeoCursorEnabled = false;
            bb.Config = _clonedConfig;

            GameObject physicsGround = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(physicsGround, $"{LogPrefix} 씬에서 PhysicsGround를 찾지 못했습니다.");
            var groundBox = physicsGround.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(groundBox, $"{LogPrefix} PhysicsGround에 BoxCollider2D가 없습니다.");
            _groundWorldY = groundBox.bounds.max.y;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _groundWorldY), _clonedConfig, out _);

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, w, Mathf.Max(1f, h - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _intent = new ScriptedIntentSource { MoveInputX = 0f };
            bb.IntentSource = _intent;
            bb.CursorProvider = TryGetScriptedCursor;

            Vector2 start = new Vector2(0f, _groundWorldY);
            bb.Body.bodyType = RigidbodyType2D.Dynamic;
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FlatGroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            yield return new WaitForSeconds(0.5f);
            _characterHeight = bb.CharacterHeightWorld;
            // 커서는 평소 캐릭터에서 멀리 둔다 — 잡을 때만 몸 위로 옮긴다.
            _cursorWorld = new Vector2(start.x + 6f * _characterHeight, start.y + 4f * _characterHeight);

            Debug.Log($"{LogPrefix} 준비 완료 — 지면 월드Y={_groundWorldY:F3}, 신장={_characterHeight:F3}유닛, " +
                $"회전 스위치={_clonedConfig.throwTumbleEnabled}, 무릎앉아 스위치={_clonedConfig.landingCrouchEnabled}, " +
                $"항상앉기={_clonedConfig.throwTumbleAlwaysCrouchOnLanding}.");
        }

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _agent != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, _cursorWorld, _clonedConfig, out _);
            return true;
        }

        /// <summary>커서를 몸통 가운데로 옮기고 <b>실제 클릭 경로</b>로 잡기를 요청한다.
        /// 반환값은 요청 직후(같은 프레임)의 상태 — 정상이면 Dragged다.</summary>
        private StickmanStateId RequestGrabViaRealPath(string why)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            _cursorWorld = bb.Body.position + new Vector2(0f, _characterHeight * 0.5f);
            StickmanStateId before = bb.Machine.CurrentStateId;
            _hitbox.SimulateMouseDownForTests();
            StickmanStateId after = bb.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} 잡기 요청({why}) — 요청 직전 상태={before}, 직후 상태={after}, " +
                $"락 활성={SpectacleEventLock.IsActive}({SpectacleEventLock.ActiveKind}).");
            return after;
        }

        /// <summary>잡은 채로 커서를 지정 속도로 끌다가 놓는다(ThrowTumbleTests.DragAndRelease의 놓기 절반).</summary>
        private IEnumerator DragThenRelease(Vector2 cursorVelocity, float dragSeconds)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float t = 0f;
            while (t < dragSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                t += dt;
                _cursorWorld += cursorVelocity * dt;
            }
            bb.DragReleaseSignaled = true;
            Debug.Log($"{LogPrefix} 놓기 신호 — 커서 속도={cursorVelocity.ToString("F2")}(속력 {cursorVelocity.magnitude:F2} = " +
                $"{cursorVelocity.magnitude / _characterHeight:F2}신장/초), 놓은 위치={_cursorWorld.ToString("F2")}, " +
                $"지면까지 {(_cursorWorld.y - _groundWorldY):F2}유닛.");
            // 놓은 뒤에는 커서를 멀리 치운다 — 착지 지점 근처에 커서가 남아 있으면 안 된다.
            _cursorWorld = new Vector2(_cursorWorld.x - 8f * _characterHeight, _cursorWorld.y + 6f * _characterHeight);
        }

        private sealed class LandingTimeline
        {
            public bool SawTumble, SawLandingCrouch, SawRagdoll;
            public float TumbleSeconds, CrouchSeconds;
            public float CrouchDurationPlanned = float.NaN;
            public LandingCrouchState.LandingTier Tier;
            public bool ReturnedToIdle;
            public StickmanStateId FinalState;
            public float TotalSeconds;
        }

        /// <summary>던진 뒤 회전 → 무릎앉기 → Idle 복귀까지를 벽시계로 관찰한다. Idle로 <b>처음</b>
        /// 돌아온 프레임에서 즉시 반환한다(그 프레임이 곧 "일어선 직후"다).</summary>
        private IEnumerator ObserveUntilStoodUp(LandingTimeline tl)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float elapsed = 0f;
            while (elapsed < MaxFlightObserveSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.Ragdoll || state == StickmanStateId.Getup) tl.SawRagdoll = true;
                if (state == StickmanStateId.ThrowTumble) { tl.SawTumble = true; tl.TumbleSeconds += dt; }
                if (state == StickmanStateId.LandingCrouch)
                {
                    tl.SawLandingCrouch = true;
                    tl.CrouchSeconds += dt;
                    if (bb.Machine.GetState(StickmanStateId.LandingCrouch) is LandingCrouchState crouch)
                    {
                        tl.CrouchDurationPlanned = crouch.DurationSeconds;
                        tl.Tier = crouch.Tier;
                    }
                }
                if (tl.SawLandingCrouch && (state == StickmanStateId.Idle || state == StickmanStateId.Walk))
                {
                    tl.ReturnedToIdle = true;
                    break;
                }
            }
            tl.TotalSeconds = elapsed;
            tl.FinalState = bb.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} 착지 타임라인 — 회전 {tl.TumbleSeconds:F2}초, 무릎앉기 {tl.CrouchSeconds:F2}초" +
                $"(계획 {tl.CrouchDurationPlanned:F2}초, 티어 {tl.Tier}), 랙돌={tl.SawRagdoll}, " +
                $"Idle 복귀={tl.ReturnedToIdle}({tl.FinalState}), 놓은 뒤 총 {tl.TotalSeconds:F2}초.");
        }

        // ============================================================================
        // (1) 일어선 직후 실제 경로 재잡기는 같은 프레임에 받아들여진다
        // ============================================================================

        [UnityTest]
        public IEnumerator 일어선_직후_실제경로_재잡기가_즉시_받아들여진다()
        {
            yield return SetUpFlatGround();
            StickmanBlackboard bb = _agent.Blackboard;

            // ---- 양성 대조: 평범한 Idle에서 실제 경로 잡기가 성립하는가. 안 되면 이 테스트는 아무것도 못 잰다.
            StickmanStateId control = RequestGrabViaRealPath("양성 대조(평범한 Idle)");
            if (control != StickmanStateId.Dragged)
            {
                Assert.Inconclusive($"{LogPrefix} 양성 대조 실패 — 평범한 Idle에서도 실제 클릭 경로가 Dragged로 " +
                    $"가지 않았습니다(상태 {control}). 픽스처 배선(DragThrowController/클릭캡처 서비스) 문제라 " +
                    "이 테스트는 착지 후 재잡기를 판정할 수 없습니다.");
            }

            // ---- 던진다(회전 하한 1.2신장/초를 넉넉히 넘는 세기, 위로 비스듬히).
            yield return DragThenRelease(new Vector2(4.5f, 3.0f), 0.35f);

            var tl = new LandingTimeline();
            yield return ObserveUntilStoodUp(tl);

            Assert.IsTrue(tl.SawTumble, $"{LogPrefix} 던졌는데 ThrowTumble을 보지 못했습니다 — 던지기 자체가 성립하지 않아 재잡기를 잴 수 없습니다.");
            Assert.IsFalse(tl.SawRagdoll, $"{LogPrefix} 랙돌/기상이 관측됐습니다 — 이 테스트의 전제(회전 → 무릎앉기)가 아닙니다.");
            Assert.IsTrue(tl.SawLandingCrouch, $"{LogPrefix} 무릎앉기를 보지 못했습니다(항상앉기={_clonedConfig.throwTumbleAlwaysCrouchOnLanding}).");
            Assert.IsTrue(tl.ReturnedToIdle, $"{LogPrefix} {MaxFlightObserveSeconds}초 안에 Idle/Walk로 돌아오지 못했습니다(최종 {tl.FinalState}).");

            // ---- 일어선 "바로 그 프레임"에 실제 경로로 다시 잡는다.
            float grabRequestedAt = Time.realtimeSinceStartup;
            StickmanStateId immediate = RequestGrabViaRealPath("일어선 직후");
            float waited = 0f;
            while (bb.Machine.CurrentStateId != StickmanStateId.Dragged && waited < RegrabAcceptBudgetSeconds)
            {
                yield return null;
                waited = Time.realtimeSinceStartup - grabRequestedAt;
            }
            StickmanStateId eventual = bb.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} 일어선 직후 재잡기 — 요청 직후 {immediate}, {waited:F3}초 뒤 {eventual}.");

            Assert.AreEqual(StickmanStateId.Dragged, eventual,
                $"{LogPrefix} ★ 일어선 직후 잡기가 {RegrabAcceptBudgetSeconds:F2}초 안에 Dragged로 가지 않았습니다" +
                $"(직후 {immediate}, 최종 {eventual}, 락={SpectacleEventLock.ActiveKind}/{SpectacleEventLock.IsActive}). " +
                "우리 상태기계/락에 '일어선 뒤 잡기 거부 창'이 실재합니다 — 신고의 원인이 플랫폼 밖이 아니라 여기입니다.");
            Assert.AreEqual(StickmanStateId.Dragged, immediate,
                $"{LogPrefix} 재잡기가 결국 받아들여졌지만 요청한 그 프레임에는 {immediate}였습니다 — " +
                "OnMouseDown은 동기 전이라 정상이면 같은 프레임에 Dragged여야 합니다(한 프레임이라도 늦으면 어떤 게이트가 끼어든 것).");
        }

        // ============================================================================
        // (2) 착지 뒤 Idle이 흔들리지 않는다 + 설계된 거부 창의 길이를 숫자로 남긴다
        // ============================================================================

        [UnityTest]
        public IEnumerator 착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다()
        {
            yield return SetUpFlatGround();
            StickmanBlackboard bb = _agent.Blackboard;

            StickmanStateId control = RequestGrabViaRealPath("양성 대조(평범한 Idle)");
            if (control != StickmanStateId.Dragged)
            {
                Assert.Inconclusive($"{LogPrefix} 양성 대조 실패(상태 {control}) — 픽스처 배선 문제라 판정 불가.");
            }

            // 최대 세기(상한 12유닛/초 근처)로 던져 가장 긴 무릎앉기(T3 버팀)를 유도한다.
            yield return DragThenRelease(new Vector2(9.0f, 6.0f), 0.35f);

            // 무릎앉기 도중에 잡기를 시도한다 — 설계된 거부(Idle/Walk에서만 잡을 수 있음)를 그대로 확인한다.
            float elapsed = 0f;
            bool probedDuringCrouch = false;
            StickmanStateId duringCrouchResult = default;
            while (elapsed < MaxFlightObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (!probedDuringCrouch && bb.Machine.CurrentStateId == StickmanStateId.LandingCrouch)
                {
                    probedDuringCrouch = true;
                    duringCrouchResult = RequestGrabViaRealPath("무릎앉기 도중");
                    // 잡기 시도 뒤 커서를 다시 치운다(로데오/커서 반응 배제).
                    _cursorWorld += new Vector2(0f, 6f * _characterHeight);
                }
                if (probedDuringCrouch && bb.Machine.CurrentStateId != StickmanStateId.LandingCrouch) break;
            }
            Assert.IsTrue(probedDuringCrouch, $"{LogPrefix} 무릎앉기 상태를 한 프레임도 잡지 못해 거부 창을 잴 수 없습니다.");
            Assert.AreEqual(StickmanStateId.LandingCrouch, duringCrouchResult,
                $"{LogPrefix} 무릎앉기 도중 잡기가 {duringCrouchResult}로 갔습니다 — 설계(Idle/Walk에서만 잡을 수 있음)와 다릅니다.");

            var crouchState = bb.Machine.GetState(StickmanStateId.LandingCrouch) as LandingCrouchState;
            Assert.IsNotNull(crouchState, $"{LogPrefix} LandingCrouchState 인스턴스를 얻지 못했습니다.");
            float crouchPlanned = crouchState.DurationSeconds;
            Debug.Log($"{LogPrefix} 설계된 거부 창 — 무릎앉기 계획 지속 {crouchPlanned:F3}초(티어 {crouchState.Tier}, " +
                $"낙차 {crouchState.HeightsFallen:F2} H). 이 시간 동안은 설계상 잡을 수 없다(로그 '[2/6] 드래그 진입 무시').");
            Assert.LessOrEqual(crouchPlanned, Mathf.Max(_clonedConfig.landingCrouchDurationBrace, _clonedConfig.landingCrouchDurationDeep) + 0.001f,
                $"{LogPrefix} 무릎앉기 지속이 설정 상한을 넘었습니다({crouchPlanned:F3}초).");

            // Idle 복귀 뒤 2초 동안 상태가 흔들리지 않아야 한다(정적 발판 위에서 GroundLossHang/Fall 재진입은 결함).
            StickmanStateId now = bb.Machine.CurrentStateId;
            Assert.IsTrue(now == StickmanStateId.Idle || now == StickmanStateId.Walk,
                $"{LogPrefix} 무릎앉기가 끝난 직후 상태가 {now}입니다(Idle/Walk 기대).");

            float watch = 0f;
            var seen = new HashSet<StickmanStateId>();
            while (watch < PostLandingWatchSeconds)
            {
                yield return null;
                watch += Time.deltaTime;
                seen.Add(bb.Machine.CurrentStateId);
            }
            seen.Remove(StickmanStateId.Idle);
            seen.Remove(StickmanStateId.Walk);
            Assert.AreEqual(0, seen.Count,
                $"{LogPrefix} 착지 뒤 {PostLandingWatchSeconds:F1}초 사이에 Idle/Walk 밖의 상태를 봤습니다: " +
                string.Join(", ", seen) + " — 정적 발판 위에서 이런 전이가 나면 그것이 '일어선 뒤 멈춤'의 후보입니다.");

            // 그리고 2초 뒤에도 실제 경로 잡기는 즉시 받아들여진다.
            StickmanStateId later = RequestGrabViaRealPath("착지 2초 뒤");
            Assert.AreEqual(StickmanStateId.Dragged, later,
                $"{LogPrefix} 착지 {PostLandingWatchSeconds:F1}초 뒤 잡기가 {later}로 갔습니다(Dragged 기대).");
        }
    }
}
