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

        /// <summary>프로덕션이 이 상태의 진단을 찍는 태그. ★ 니들이므로 <b>썩으면 조용해지지 않게</b>
        /// 모든 arm이 "이 태그로 시작하는 줄이 하나라도 있었는가"를 먼저 못박는다(CLAUDE.md 협업 프로토콜
        /// — 부재 단언용 니들이 썩으면 조용히 초록이 된다).</summary>
        private const string TumbleLogTag = "[던지기회전]";

        /// <summary>수정이 만든 줄의 니들(존재 단언 전용). ON arm이 이것의 <b>존재</b>를 요구하므로,
        /// 프로덕션 문구가 바뀌면 조용한 초록이 아니라 빨강이 난다.</summary>
        private const string RestingFallbackNeedle = "정지 착지 폴백";

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

        /// <summary>
        /// ★★ 결정적 재현용 — <b>논리 발판 상단선을 물리 바닥보다 이만큼 「아래」에 둔다</b>(OS-pt).
        ///
        /// <para>왜 필요한가: 실기에서 두 선은 <b>같은 자리</b>다(발판 Rect를 PhysicsGround 상단에서
        /// 만든다). 그래서 던진 몸이 물리 바닥에 멈추는 순간 발이 발판 상단선 "위"에 있는지 "아래"에
        /// 있는지가 <b>부동소수 반올림의 동전 던지기</b>가 되고, 위쪽으로 떨어진 경우에만 스윕 교차
        /// (<c>currOs.y &gt;= r.y</c>)가 성립하지 않아 고착이 재현된다 — 디버거 실측 재현율 1/4.
        /// 1/4로 재현되는 시나리오를 네거티브 컨트롤로 쓰면 <b>3/4은 "안 나왔다"로 초록</b>이 되어
        /// 아무 것도 증명하지 못한다.</para>
        ///
        /// <para>그래서 이 오프셋이 그 동전을 <b>4/4로 못박는다</b>. 발판 상단선이 몸이 멈추는 자리보다
        /// 아래에 있으면 발은 그 선을 <b>원리적으로</b> 통과할 수 없다(정적 콜라이더가 먼저 막는다).</para>
        ///
        /// <para>크기의 근거(양쪽에서 조인다 — 이 값은 <b>둘 사이</b>에 있어야만 뜻이 있다):
        /// <list type="bullet">
        ///   <item><b>아래 한계 ≈ 6.5 pt</b> — 2D 물리는 이 프로젝트에서 <b>이산(Discrete)</b> 검출이고
        ///         (Stickman.prefab <c>m_CollisionDetection: 0</c>) 위치 보정은 Baumgarte 0.2로
        ///         <b>여러 스텝에 걸쳐</b> 이뤄진다. 그래서 충돌 순간 한 스텝 동안 발이 바닥면 아래로
        ///         <c>충돌속도 x fixedDeltaTime</c>만큼 <b>일시적으로 파고들 수 있다</b>:
        ///         이 던지기의 착지 속도는 14.5유닛/초(놓는 지점 2.95유닛 위 + 초기 vy 6.03,
        ///         g = 29.43)이고 물리 스텝은 0.02초라 <b>0.29유닛 ≈ 6.5 OS-pt</b>다.
        ///         오프셋이 이보다 작으면 그 일시적 파고듦만으로 스윕 교차가 성립해 고착이 재현되지 않는다
        ///         — 실기에서 재현율이 1/4에 그친 이유가 바로 이 경합이다.</item>
        ///   <item><b>위 한계 = 20 pt</b> — <see cref="StickConfig.groundSnapTolerance"/>. 이보다 크면
        ///         접지(<c>Grounded</c>)가 거짓이 되어 이 시나리오는 "고착"이 아니라 "발판이 없음"을
        ///         재게 된다. 이 부등식은 <see cref="SetUpFlatGround"/>가 <b>설정값을 읽어 실행 중에</b>
        ///         확인한다(숫자를 베끼지 않는다).</item>
        /// </list>
        /// 12 pt는 아래 한계의 1.8배이고 위 한계의 0.6배다 — 양쪽에 여유가 있다.</para>
        ///
        /// <para>★ 정직한 한계: 이 배치는 실기와 <b>완전히 같지는 않다</b>. 착지 스냅 목표(발판 상단선)가
        /// 몸이 멈추는 자리보다 0.53유닛 아래라, 확정 순간 몸이 그만큼 바닥을 파고들었다가 몇 프레임에
        /// 걸쳐 밀려 올라온다(실기에서는 두 선이 같은 자리라 그 양이 0이다). 이 파일이 이 arm에서 재는
        /// 것은 <b>"스윕 교차가 불가능할 때 2순위가 받는가"</b> 하나이고, 착지 이후의 안정성은 위
        /// (2)번 케이스 두 벌이 <b>오프셋 0</b>(= 실기와 같은 배치)에서 잰다.</para>
        /// </summary>
        private const float FootholdBelowFloorOsPt = 12f;

        /// <summary>고착 재현 arm에서 관찰하는 상한(초). 프로덕션 안전 상한(6초)에 여유를 더한 값이다 —
        /// <b>설정값 자체는 하드코딩하지 않는다</b>(<c>throwTumbleMaxSeconds</c>를 실행 중에 읽어 쓴다).</summary>
        private const float StallObserveSlackSeconds = 3.0f;

        private readonly List<string> _tumbleLogs = new List<string>();
        private bool _capturingLogs;

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
        private bool _originSaved;

        private TestFootholdService _service;
        private ScriptedIntentSource _intent;
        private float _groundWorldY;
        private float _characterHeight;
        private Vector2 _cursorWorld;

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_capturingLogs || string.IsNullOrEmpty(condition)) return;
            if (condition.StartsWith(TumbleLogTag)) _tumbleLogs.Add(condition);
        }

        /// <summary>수집한 <see cref="TumbleLogTag"/> 줄 중 니들을 포함한 것이 있는가.</summary>
        private bool SawTumbleLog(string needle)
        {
            for (int i = 0; i < _tumbleLogs.Count; i++)
            {
                if (_tumbleLogs[i].Contains(needle)) return true;
            }
            return false;
        }

        private string TumbleLogDump()
        {
            return _tumbleLogs.Count == 0 ? "(수집된 줄 없음)" : string.Join("\n    ", _tumbleLogs);
        }

        [TearDown]
        public void TearDown()
        {
            _capturingLogs = false;
            Application.logMessageReceived -= OnLogMessage;
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
            if (_originSaved) ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            _originSaved = false;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _hitbox = null;
        }

        // ============================================================================
        // 공통 준비 — ThrowTumbleTests.SetUpFlatGround와 같은 배치
        // ============================================================================

        /// <param name="footholdTopOsOffset">논리 발판 상단선을 물리 바닥보다 얼마나 아래에 둘지(OS-pt).
        /// 0이면 실기와 같은 배치(두 선이 같은 자리). 0보다 크면 스윕 교차가 <b>원리적으로</b> 성립할 수
        /// 없는 배치가 된다 — <see cref="FootholdBelowFloorOsPt"/> 문서 참고.</param>
        private IEnumerator SetUpFlatGround(float footholdTopOsOffset = 0f)
        {
            _tumbleLogs.Clear();
            _capturingLogs = false;
            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;

            // ★ 한 테스트가 arm을 두 번 돌 수 있다(ON/OFF 대조). 그때 앞 arm의 사본을 남겨 두면
            //   ScriptableObject가 새 나가고, 다음 arm이 "이미 0으로 덮인" 원본 좌표를 원본으로 저장한다.
            if (_clonedConfig != null)
            {
                Object.DestroyImmediate(_clonedConfig);
                _clonedConfig = null;
            }

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
            // ★ 원본 오버레이 원점은 **처음 한 번만** 저장한다. 두 번째 arm에서 다시 저장하면
            //   이미 우리가 0으로 덮어 둔 값을 "원본"으로 기억해 TearDown이 복원을 못 한다.
            if (!_originSaved)
            {
                _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
                _originSaved = true;
            }
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

            // ★ 오프셋이 접지 허용오차 안에 들어 있어야 "고착"을 재는 시나리오가 성립한다 —
            //   허용오차를 넘으면 Grounded가 거짓이 되어 «발판이 아예 없다»를 재게 된다.
            //   숫자를 베끼지 않고 **설정값을 읽어** 확인한다(CLAUDE.md 협업 프로토콜).
            Assert.Less(footholdTopOsOffset, _clonedConfig.groundSnapTolerance * 0.8f,
                $"{LogPrefix} 발판 상단 오프셋({footholdTopOsOffset:F2}pt)이 접지 허용오차" +
                $"({_clonedConfig.groundSnapTolerance:F1}pt)에 비해 너무 큽니다 — 이 픽스처는 '고착'이 아니라 " +
                "'접지 상실'을 재게 됩니다.");

            float footholdTopOs = groundOs.y + footholdTopOsOffset;
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, footholdTopOs, w, Mathf.Max(1f, h - footholdTopOs)), true));
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
                $"항상앉기={_clonedConfig.throwTumbleAlwaysCrouchOnLanding}, " +
                $"정지착지 스위치={_clonedConfig.throwTumbleRestingLandingEnabled}, " +
                $"발판상단 OS오프셋={footholdTopOsOffset:F2}pt(접지허용 {_clonedConfig.groundSnapTolerance:F1}pt, " +
                $"유예 {_clonedConfig.fallGraceDuration:F2}초, 회전 상한 {_clonedConfig.throwTumbleMaxSeconds:F1}초).");

            // 준비 로그 이후부터 수집한다 — 픽스처가 찍는 줄과 시나리오가 찍는 줄을 섞지 않는다.
            _tumbleLogs.Clear();
            _capturingLogs = true;
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
        //
        // ★★ 2026-09-06 — 원래 이 자리에는 커서 속도 <b>vx = 9.0 하나</b>만 스크립트된 테스트가
        //    있었다. 그 값은 디버거가 고착을 실측한 <b>바로 그 지점</b>이라 «경계에 딱 붙은 표본»이고,
        //    한 점만 재는 테스트는 두 가지를 못 가른다:
        //      (a) 수정이 그 한 점에서만 성립하는 경우(그 점을 지나면 다시 고장)
        //      (b) 그 점이 우연히 좋은 쪽 동전이 나온 경우(디버거 실측 재현율 1/4 — 즉 이 테스트는
        //          <b>3/4 확률로 결함을 못 보고 초록</b>이었다)
        //    그래서 <b>경계에서 떼어 양쪽을 잠근다</b>. 아래 두 케이스가 그 두 표본이다.
        //
        //    숫자를 그렇게 고른 근거(검산):
        //      · 놓는 지점은 지면에서 2.95유닛 위, 세로 초속도 +6.0 → 비행 시간 t ≈ 0.697초
        //        (14.715t² − 6.03t − 2.95 = 0의 양의 근. g = |−9.81| × gravityScale 3 = 29.43).
        //      · 착지 x는 vx에 비례하므로 Δx = 0.697 × Δvx.
        //        vx 8.9 → 9.0 대비 −0.07유닛, vx 9.2 → +0.14유닛. 두 표본의 간격은 <b>0.21유닛</b>
        //        (신장 1.71의 12%)이라 "같은 자리에 두 번 떨어뜨린 것"이 아니다.
        //      · 그러면서도 둘 다 회전이 성립하는 대역 안이다(속력 10.75 / 11.00 &lt; 던지기 상한 12.0,
        //        회전 하한 1.2신장/초를 크게 넘는다) — 경계를 넘겨 시나리오 자체가 바뀌지 않는다.
        // ============================================================================

        [UnityTest]
        public IEnumerator 착지후_Idle이_2초간_흔들리지_않는다_수평8_9()
        {
            // 경계값 9.0의 **아래쪽**. 착지 x가 9.0 대비 0.07유닛 앞이다.
            yield return RunLandingRegrabScenario(new Vector2(8.9f, 6.0f), "vx 8.9(경계 아래)");
        }

        [UnityTest]
        public IEnumerator 착지후_Idle이_2초간_흔들리지_않는다_수평9_2()
        {
            // 경계값 9.0의 **위쪽**. 착지 x가 9.0 대비 0.14유닛 뒤다.
            yield return RunLandingRegrabScenario(new Vector2(9.2f, 6.0f), "vx 9.2(경계 위)");
        }

        /// <summary>던지기 → 회전 → 무릎앉기 → Idle 복귀 → 재잡기 왕복 한 벌. 커서 속도만 바꿔 여러
        /// 표본을 같은 자로 잰다(두 케이스가 서로 다른 판정을 쓰면 비교가 성립하지 않는다).</summary>
        private IEnumerator RunLandingRegrabScenario(Vector2 cursorVelocity, string label)
        {
            yield return SetUpFlatGround();
            StickmanBlackboard bb = _agent.Blackboard;

            StickmanStateId control = RequestGrabViaRealPath($"양성 대조(평범한 Idle) — {label}");
            if (control != StickmanStateId.Dragged)
            {
                Assert.Inconclusive($"{LogPrefix} [{label}] 양성 대조 실패(상태 {control}) — 픽스처 배선 문제라 판정 불가.");
            }

            // 세게 던져 가장 긴 무릎앉기(T3 버팀)를 유도한다.
            yield return DragThenRelease(cursorVelocity, 0.35f);

            // 무릎앉기 도중에 잡기를 시도한다 — 설계된 거부(Idle/Walk에서만 잡을 수 있음)를 그대로 확인한다.
            float elapsed = 0f;
            float tumbleSeconds = 0f;
            bool probedDuringCrouch = false;
            StickmanStateId duringCrouchResult = default;
            while (elapsed < MaxFlightObserveSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                if (bb.Machine.CurrentStateId == StickmanStateId.ThrowTumble) tumbleSeconds += dt;
                if (!probedDuringCrouch && bb.Machine.CurrentStateId == StickmanStateId.LandingCrouch)
                {
                    probedDuringCrouch = true;
                    duringCrouchResult = RequestGrabViaRealPath($"무릎앉기 도중 — {label}");
                    // 잡기 시도 뒤 커서를 다시 치운다(로데오/커서 반응 배제).
                    _cursorWorld += new Vector2(0f, 6f * _characterHeight);
                }
                if (probedDuringCrouch && bb.Machine.CurrentStateId != StickmanStateId.LandingCrouch) break;
            }

            // ★★ 2026-09-06 회귀 잠금 — 이 줄이 이번 결함의 <b>직접</b> 판정이다.
            //    ThrowTumble이 안전 상한(throwTumbleMaxSeconds)까지 머물렀다면 그것이 곧 고착이고,
            //    그 구간 내내 잡기가 거부되며 빠져나온 뒤엔 낙하높이 0이라 무릎앉기가 통째로 사라진다.
            //    아래의 "무릎앉기를 못 봤다"만으로도 결국 빨강이 나지만, 그 메시지는 <b>왜</b>인지를
            //    말해 주지 않는다 — 그 침묵이 이 결함이 오래 남은 이유 중 하나다.
            float tumbleCap = _clonedConfig.throwTumbleMaxSeconds;
            Debug.Log($"{LogPrefix} [{label}] 회전 체류 {tumbleSeconds:F2}초(안전 상한 {tumbleCap:F1}초), " +
                $"무릎앉기 관측={probedDuringCrouch}, 관찰 {elapsed:F2}초. " +
                $"수집된 {TumbleLogTag} 줄 {_tumbleLogs.Count}개.");
            Assert.Less(tumbleSeconds, tumbleCap * 0.5f,
                $"{LogPrefix} ★ [{label}] 던진 캐릭터가 공중 회전 상태에 {tumbleSeconds:F2}초 머물렀습니다" +
                $"(안전 상한 {tumbleCap:F1}초의 절반을 넘음). 이것이 2026-09-06 결함의 서명입니다 — " +
                "스윕 교차는 몸이 '내려가는 중'일 때만 성립하는데 던진 몸은 물리 바닥에 닿아 멈추므로 " +
                "그 조건이 영구히 거짓이 되고, 2순위(밴드+유예) 폴백이 없으면 상한까지 제자리에서 돕니다. " +
                $"그 동안 잡기는 전부 거부되고, 빠져나온 뒤엔 낙하높이가 0이라 무릎앉기/먼지/대사가 사라집니다.\n" +
                $"    {TumbleLogTag} 줄:\n    {TumbleLogDump()}");

            Assert.IsTrue(probedDuringCrouch,
                $"{LogPrefix} [{label}] 무릎앉기 상태를 한 프레임도 잡지 못해 거부 창을 잴 수 없습니다" +
                $"(회전 체류 {tumbleSeconds:F2}초).\n    {TumbleLogTag} 줄:\n    {TumbleLogDump()}");
            Assert.AreEqual(StickmanStateId.LandingCrouch, duringCrouchResult,
                $"{LogPrefix} [{label}] 무릎앉기 도중 잡기가 {duringCrouchResult}로 갔습니다 — 설계(Idle/Walk에서만 잡을 수 있음)와 다릅니다.");

            var crouchState = bb.Machine.GetState(StickmanStateId.LandingCrouch) as LandingCrouchState;
            Assert.IsNotNull(crouchState, $"{LogPrefix} [{label}] LandingCrouchState 인스턴스를 얻지 못했습니다.");
            float crouchPlanned = crouchState.DurationSeconds;
            Debug.Log($"{LogPrefix} [{label}] 설계된 거부 창 — 무릎앉기 계획 지속 {crouchPlanned:F3}초(티어 {crouchState.Tier}, " +
                $"낙차 {crouchState.HeightsFallen:F2} H). 이 시간 동안은 설계상 잡을 수 없다(로그 '[2/6] 드래그 진입 무시').");
            Assert.LessOrEqual(crouchPlanned, Mathf.Max(_clonedConfig.landingCrouchDurationBrace, _clonedConfig.landingCrouchDurationDeep) + 0.001f,
                $"{LogPrefix} [{label}] 무릎앉기 지속이 설정 상한을 넘었습니다({crouchPlanned:F3}초).");

            // Idle 복귀 뒤 2초 동안 상태가 흔들리지 않아야 한다(정적 발판 위에서 GroundLossHang/Fall 재진입은 결함).
            StickmanStateId now = bb.Machine.CurrentStateId;
            Assert.IsTrue(now == StickmanStateId.Idle || now == StickmanStateId.Walk,
                $"{LogPrefix} [{label}] 무릎앉기가 끝난 직후 상태가 {now}입니다(Idle/Walk 기대).");

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
                $"{LogPrefix} [{label}] 착지 뒤 {PostLandingWatchSeconds:F1}초 사이에 Idle/Walk 밖의 상태를 봤습니다: " +
                string.Join(", ", seen) + " — 정적 발판 위에서 이런 전이가 나면 그것이 '일어선 뒤 멈춤'의 후보입니다.");

            // 그리고 2초 뒤에도 실제 경로 잡기는 즉시 받아들여진다.
            StickmanStateId later = RequestGrabViaRealPath($"착지 2초 뒤 — {label}");
            Assert.AreEqual(StickmanStateId.Dragged, later,
                $"{LogPrefix} [{label}] 착지 {PostLandingWatchSeconds:F1}초 뒤 잡기가 {later}로 갔습니다(Dragged 기대).");
        }

        // ============================================================================
        // (3) ★★ 정지 착지 폴백 — 켠 arm / 끈 arm을 같은 테스트 안에서 대조한다
        // ============================================================================

        /// <summary>
        /// ★★ 2026-09-06 결함의 <b>네거티브 컨트롤 포함</b> 잠금.
        ///
        /// <para><b>왜 한 테스트에 두 arm인가</b>: 이 결함의 증상("6초간 제자리 회전 + 잡기 무반응")은
        /// 스위치를 끄면 <b>반드시</b> 나와야 한다. 두 arm을 다른 테스트로 갈라 두면, 켠 arm이 초록인
        /// 이유가 "수정이 동작해서"인지 "이 시나리오가 애초에 고착하지 않아서"인지 <b>구분할 수 없다</b> —
        /// 이 저장소가 반복해서 당한 거짓 통과의 형태다. 여기서는 끈 arm이 고착을 <b>실제로 재현</b>하지
        /// 못하면 그 자리에서 실패한다.</para>
        ///
        /// <para><b>왜 발판을 1pt 내리는가</b>: 실기에서 이 고착의 재현율은 부동소수 반올림에 좌우되어
        /// 1/4이다(디버거 실측). 1/4짜리 시나리오로는 네거티브 컨트롤이 성립하지 않는다 —
        /// <see cref="FootholdBelowFloorOsPt"/> 문서에 그 동전을 4/4로 못박는 근거와 크기의 상·하한이
        /// 있다. <b>던지기 자체는 두 arm이 완전히 같다.</b></para>
        ///
        /// <para><b>니들 관리</b>: 두 arm 모두 <see cref="TumbleLogTag"/>로 시작하는 줄이 하나라도
        /// 수집됐는지를 먼저 못박는다(태그가 바뀌면 부재 단언이 조용히 초록이 되는 것을 막는다). 상한
        /// 초과 니들은 <b>설정값에서 조립</b>하고(숫자를 베끼지 않는다), 수정이 만든 줄
        /// (<see cref="RestingFallbackNeedle"/>)은 켠 arm이 <b>존재</b>를 요구한다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 정지착지_폴백을_끄면_같은_던지기가_상한까지_고착하고_켜면_유예만에_착지한다()
        {
            var throwVelocity = new Vector2(9.0f, 6.0f);   // 디버거가 고착을 실측한 바로 그 던지기

            // ---------------- arm A(네거티브 컨트롤): 수정을 되돌린다 ----------------
            yield return SetUpFlatGround(FootholdBelowFloorOsPt);
            _clonedConfig.throwTumbleRestingLandingEnabled = false;
            float capSeconds = _clonedConfig.throwTumbleMaxSeconds;
            // ★ 니들을 **설정값에서 조립**한다 — 프로덕션 로그의 형식 문자열과 같은 자리를 겨눈다.
            string capNeedle = $"상한 {capSeconds:F1}초 초과";

            var offArm = new StallProbe();
            yield return RunStallProbe(offArm, throwVelocity, capSeconds + StallObserveSlackSeconds, "OFF(수정 되돌림)");

            Assert.Greater(_tumbleLogs.Count, 0,
                $"{LogPrefix} [OFF] {TumbleLogTag} 로 시작하는 줄을 한 개도 수집하지 못했습니다 — " +
                "태그가 바뀌었거나 상태에 아예 진입하지 못했습니다. 이 상태로는 아래 부재/존재 단언이 " +
                "전부 무의미합니다(니들이 썩으면 조용히 초록이 된다).");
            Assert.IsTrue(offArm.SawTumble,
                $"{LogPrefix} [OFF] 던졌는데 ThrowTumble에 들어가지 못했습니다 — 시나리오가 성립하지 않습니다.\n" +
                $"    {TumbleLogTag} 줄:\n    {TumbleLogDump()}");
            Assert.IsTrue(SawTumbleLog(capNeedle),
                $"{LogPrefix} ★ 네거티브 컨트롤 실패 — 수정을 껐는데도 '{capNeedle}'가 찍히지 않았습니다. " +
                "그러면 아래 ON arm의 초록은 '수정이 동작한다'가 아니라 '이 시나리오가 애초에 고착하지 " +
                "않는다'일 수 있습니다 — 픽스처를 다시 설계해야 합니다.\n" +
                $"    {TumbleLogTag} 줄:\n    {TumbleLogDump()}");
            Assert.GreaterOrEqual(offArm.TumbleSeconds, capSeconds * 0.9f,
                $"{LogPrefix} [OFF] 회전 체류가 {offArm.TumbleSeconds:F2}초로 안전 상한 {capSeconds:F1}초에 " +
                "못 미칩니다 — 상한 로그는 찍혔는데 체류가 짧다면 두 측정이 서로 다른 것을 보고 있습니다.");
            Assert.IsTrue(offArm.ProbedMidStall,
                $"{LogPrefix} [OFF] 고착 도중 잡기를 한 번도 시도하지 못했습니다(회전 체류 " +
                $"{offArm.TumbleSeconds:F2}초 < 시도 시점 1.0초) — '잡아도 반응이 없다'를 잴 수 없습니다.");
            Assert.AreEqual(StickmanStateId.ThrowTumble, offArm.MidStallGrabResult,
                $"{LogPrefix} [OFF] 고착 도중 잡기가 {offArm.MidStallGrabResult}로 갔습니다 — " +
                "사용자가 신고한 '잡아도 반응이 없다'는 이 거부가 실체입니다(여기서 Dragged가 나오면 " +
                "고착 중에도 잡을 수 있다는 뜻이라 증상 모델이 틀린 것).");
            // ★ 여기서 «무릎앉기를 못 봤다»로 대조하지 않는다 — 실측으로 그것이 <b>픽스처 인공물</b>임이
            //   드러났다. 이 arm은 발판 상단선을 12pt(=0.60월드유닛) 내려 뒀으므로, 상한을 빠져나온 뒤
            //   FallState가 "0.00유닛"이 아니라 <b>정확히 그 오프셋만큼</b>(0.60유닛)을 낙차로 보고,
            //   그 값이 마침 T0.5 문턱(0.35 H = 0.60유닛)에 걸려 얕은 흡수가 한 번 나온다.
            //   실기에서는 두 선이 같은 자리라 그 값이 0.00이고 무릎앉기가 통째로 생략된다.
            //   그래서 대조는 <b>오프셋과 무관한 값</b>으로 한다: ThrowTumble <b>자신이</b> 확정한 낙차.
            //   고착한 arm은 착지를 확정하지 못하므로 이 값이 정확히 0이다.
            Assert.AreEqual(0f, offArm.TumbleLandingHeight, 0.0001f,
                $"{LogPrefix} [OFF] 고착했다면서 ThrowTumble이 착지를 확정했습니다" +
                $"(환산 낙차 {offArm.TumbleLandingHeight:F3}유닛) — 상한 로그와 이 값이 서로 다른 것을 " +
                "말하고 있습니다. 두 측정 중 하나가 틀렸습니다.");

            // ---------------- arm B: 수정을 켠 채 같은 던지기 ----------------
            yield return SetUpFlatGround(FootholdBelowFloorOsPt);
            Assert.IsTrue(_clonedConfig.throwTumbleRestingLandingEnabled,
                $"{LogPrefix} [ON] 배포 기본값이 꺼져 있습니다 — 이 수정은 기본 ON이어야 합니다.");

            var onArm = new StallProbe();
            yield return RunStallProbe(onArm, throwVelocity, capSeconds + StallObserveSlackSeconds, "ON(수정 적용)");

            Assert.Greater(_tumbleLogs.Count, 0,
                $"{LogPrefix} [ON] {TumbleLogTag} 로 시작하는 줄을 한 개도 수집하지 못했습니다 — 니들이 썩었습니다.");
            Assert.IsTrue(onArm.SawTumble,
                $"{LogPrefix} [ON] 던졌는데 ThrowTumble에 들어가지 못했습니다 — 두 arm이 다른 시나리오를 탔습니다.");

            Assert.IsTrue(SawTumbleLog(RestingFallbackNeedle),
                $"{LogPrefix} [ON] '{RestingFallbackNeedle}' 줄이 없습니다 — 스윕 교차가 성립할 수 없는 배치인데도 " +
                "2순위 경로를 지나가지 않았다는 뜻입니다(또는 그 문구가 바뀌었습니다).\n" +
                $"    {TumbleLogTag} 줄:\n    {TumbleLogDump()}");
            Assert.IsFalse(SawTumbleLog(capNeedle),
                $"{LogPrefix} ★ [ON] 수정을 켰는데도 '{capNeedle}'가 찍혔습니다 — 고착이 그대로입니다.\n" +
                $"    {TumbleLogTag} 줄:\n    {TumbleLogDump()}");

            // 유예(fallGraceDuration) 한 번이면 끝날 일이었다. 여유를 넉넉히 줘도 상한의 1/4을 넘지 않는다.
            Assert.Less(onArm.TumbleSeconds, capSeconds * 0.25f,
                $"{LogPrefix} [ON] 회전 체류가 {onArm.TumbleSeconds:F2}초입니다(상한 {capSeconds:F1}초의 25% 미만 기대, " +
                $"유예는 {_clonedConfig.fallGraceDuration:F2}초).");
            Assert.IsTrue(onArm.SawLandingCrouch,
                $"{LogPrefix} ★ [ON] 무릎앉기가 나오지 않았습니다 — 사용자가 잃었던 것이 정확히 이것입니다" +
                "(고착을 빠져나온 뒤엔 낙하높이가 0이라 무릎앉기/먼지/대사가 전부 생략됐다).");

            // 낙하 높이가 0이 아니어야 착지 연출의 깊이가 살아난다 — 값으로 못박는다(로그가 아니라).
            Debug.Log($"{LogPrefix} [ON/OFF] 대조 — 회전 체류 OFF {offArm.TumbleSeconds:F2}초 / ON {onArm.TumbleSeconds:F2}초" +
                $"(상한 {capSeconds:F1}초), 회전이 확정한 낙차 OFF {offArm.TumbleLandingHeight:F2}유닛 / " +
                $"ON {onArm.TumbleLandingHeight:F2}유닛, 무릎앉기 OFF {offArm.SawLandingCrouch} / ON {onArm.SawLandingCrouch}.");
            Assert.Greater(onArm.TumbleLandingHeight, 0f,
                $"{LogPrefix} [ON] 회전이 확정한 환산 낙차가 {onArm.TumbleLandingHeight:F2}유닛입니다 — " +
                "0이면 ThrowTumble이 착지를 확정하지 못했다는 뜻이고, 그 뒤를 Fall이 받으면 " +
                "'이미 바닥에 있는 몸을 뒤늦게 착지시킨' 그림이라 연출 깊이가 살아나지 않습니다.");
            Assert.Less(onArm.TumbleSeconds, offArm.TumbleSeconds,
                $"{LogPrefix} 두 arm의 회전 체류가 갈리지 않았습니다(ON {onArm.TumbleSeconds:F2}초 >= " +
                $"OFF {offArm.TumbleSeconds:F2}초) — 스위치가 실제로 무언가를 바꾸지 않았다는 뜻입니다.");
        }

        private sealed class StallProbe
        {
            public bool SawTumble, SawLandingCrouch;
            public float TumbleSeconds;
            public StickmanStateId MidStallGrabResult = StickmanStateId.Idle;
            public bool ProbedMidStall;
            public StickmanStateId FinalState;

            /// <summary>이 arm에서 <b>ThrowTumble 자신이</b> 확정한 착지의 환산 낙차(월드 유닛).
            /// 0이면 이 상태는 착지를 확정하지 못하고 상한으로 빠져나갔다는 뜻이다 —
            /// 두 arm을 <b>픽스처 인공물 없이</b> 가르는 값이다(아래 대조 참고).</summary>
            public float TumbleLandingHeight;
        }

        /// <summary>실제 드래그&amp;던지기 경로로 던진 뒤, 회전 체류 시간과 "그 도중 잡기가 거부되는가"를
        /// 함께 관찰한다. 두 arm이 <b>같은 관찰 코드</b>를 쓰도록 한 곳에 모은다.</summary>
        private IEnumerator RunStallProbe(StallProbe probe, Vector2 cursorVelocity, float observeSeconds, string label)
        {
            StickmanBlackboard bb = _agent.Blackboard;

            StickmanStateId control = RequestGrabViaRealPath($"양성 대조(평범한 Idle) — {label}");
            if (control != StickmanStateId.Dragged)
            {
                Assert.Inconclusive($"{LogPrefix} [{label}] 양성 대조 실패(상태 {control}) — 픽스처 배선 문제라 판정 불가.");
            }

            yield return DragThenRelease(cursorVelocity, 0.35f);

            float elapsed = 0f;
            float tumbleSeen = 0f;
            while (elapsed < observeSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.ThrowTumble)
                {
                    probe.SawTumble = true;
                    probe.TumbleSeconds += dt;
                    tumbleSeen += dt;
                    // 회전에 1초 이상 머물렀다면 그것은 이미 정상 비행이 아니다(실측 비행 중앙값 1.0초,
                    // 이 던지기의 계획은 0.63초였다). 그 시점에 잡기를 시도해 "잡아도 반응 없음"을 잰다.
                    if (!probe.ProbedMidStall && tumbleSeen >= 1.0f)
                    {
                        probe.ProbedMidStall = true;
                        probe.MidStallGrabResult = RequestGrabViaRealPath($"고착 도중 — {label}");
                        _cursorWorld += new Vector2(0f, 6f * _characterHeight);
                    }
                }
                if (state == StickmanStateId.LandingCrouch) probe.SawLandingCrouch = true;
                // 무릎앉기까지 봤고 다시 서 있으면 더 볼 것이 없다(ON arm의 조기 탈출).
                if (probe.SawLandingCrouch && (state == StickmanStateId.Idle || state == StickmanStateId.Walk)) break;
            }
            probe.FinalState = bb.Machine.CurrentStateId;
            probe.TumbleLandingHeight = bb.Machine.GetState(StickmanStateId.ThrowTumble) is ThrowTumbleState ts
                ? ts.LastLandingEffectiveHeight
                : float.NaN;

            Debug.Log($"{LogPrefix} [{label}] 관찰 종료 — 정지착지 스위치={_clonedConfig.throwTumbleRestingLandingEnabled}, " +
                $"회전 체류 {probe.TumbleSeconds:F2}초, 무릎앉기={probe.SawLandingCrouch}, " +
                $"회전이 확정한 낙차={probe.TumbleLandingHeight:F2}유닛, " +
                $"고착중 잡기 시도={probe.ProbedMidStall}({probe.MidStallGrabResult}), " +
                $"최종상태={probe.FinalState}, 총 {elapsed:F2}초, 몸 y={bb.Body.position.y:F3}(지면 {_groundWorldY:F3}).");

            // 잡혔으면 반드시 놓아 준다 — 정적 락(SpectacleEventLock)이 다음 arm으로 샌다.
            if (bb.Machine.CurrentStateId == StickmanStateId.Dragged)
            {
                bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                yield return null;
            }
        }
    }
}
