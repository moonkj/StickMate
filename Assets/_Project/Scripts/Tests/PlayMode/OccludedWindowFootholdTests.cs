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
    /// ★★ 사용자 신고 회귀 잠금(2026-08-31, 디버거):
    /// **"창이 겹쳐있을때 창이 뒤에 있음에도 그 경계면을 따라 걸음"**
    ///
    /// EditMode의 <c>VisibleTopEdgeOcclusionTests</c>가 <b>계산</b>(솔버 단독)을 잠근다면, 이 파일은
    /// <b>실제 파이프라인 전체</b>를 잠근다: 창 두 개를 겹쳐 배치한 가짜 플랫폼 서비스 →
    /// <c>FootholdPoller</c> → <c>StickmanBlackboard.SenseGround()</c> → <c>GroundSensor</c> →
    /// 실제 씬의 캐릭터. "가려진 창의 경계에 실제로 접지가 되는가"를 추측이 아니라 실측한다.
    ///
    /// ============================================================================
    /// 확정된 원인 (코드 경로로 확정 — 추측 아님)
    /// ============================================================================
    /// <c>Win32WindowService.EnumerateFootholds()</c>가 EnumWindows 결과(창 전체 사각형)를 <b>가려짐
    /// 계산 없이 그대로</b> 발판으로 내보냈다. macOS는 2026-08-28에 이미 고쳐졌지만 그 수정이 macOS
    /// 전용 파일의 private 메서드에 갇혀 있어 Windows가 재사용하지 못했다(중복이 아니라 <b>누락</b>).
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  O1   가려진 x에서 뒤 창을 딛으려 하면 <b>접지되지 않는다</b>(수정 후 파이프라인).
    ///  O1n  (네거티브) <b>같은 배치·같은 파이프라인</b>에 수정 전 열거 규칙만 꽂으면 접지된다
    ///       = O1이 항상 참인 단언이 아니라 진짜 버그를 잡고 있다는 증거.
    ///  O2   과잉 제거 방지 — 앞 창 밖으로 삐져나와 <b>실제로 보이는</b> 구간에서는 여전히 접지된다.
    ///  O3   맨 앞 창 자신은 아무 영향도 받지 않는다.
    ///  O4   실제 캐릭터 실측 — 가려진 경계 위에 세워두면 그 자리에 <b>머물지 못하고 떨어진다</b>
    ///       (사용자가 본 "그 경계를 따라 걷는" 그림이 재현되지 않는다).
    ///
    /// 배치(OS 스크린 좌표, 좌상단 원점 — r.y가 창 상단선):
    ///   앞 창(z0): x 0.30w~0.85w, y 0.25h~0.75h
    ///   뒤 창(z1): x 0.10w~0.70w, 상단선 y=0.45h  ← 앞 창의 y 구간 안이라 앞 창이 덮는다
    ///   가려진 x = 0.50w (앞 창 안), 보이는 x = 0.20w (앞 창 왼쪽 밖)
    /// StickConfig는 복제본을 꽂아 배포용 원본 자산을 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3).
    /// </summary>
    public sealed class OccludedWindowFootholdTests
    {
        private const string LogPrefix = "[창겹침발판-PLAY]";

        private const long FrontHandle = 2001L;
        private const long BackHandle = 2002L;

        /// <summary>Win32WindowService/MacWindowService가 쓰는 것과 같은 값.</summary>
        private const float MinVisibleWidth = 24f;

        private const float SettleWaitSeconds = 2.0f;

        /// <summary>
        /// z-order를 가진 가짜 창 목록을 내보내는 서비스. <c>ApplyOcclusionFilter</c>가
        /// <b>수정 전(false) / 수정 후(true)</b>를 그대로 가르는 스위치다 —
        /// true일 때의 코드가 곧 Win32WindowService.BuildVisibleTopEdgeFootholds()와 같은 계산이고,
        /// false일 때가 수정 전 OnEnumWindow()가 하던 일(창 전체 사각형 = 발판) 그대로다.
        /// </summary>
        private sealed class ZOrderedWindowService : IPlatformWindowService
        {
            /// <summary>z-order 앞->뒤 순서의 원본 창 목록.</summary>
            public readonly List<PlatformFoothold> RawFrontToBack = new List<PlatformFoothold>();

            public bool ApplyOcclusionFilter = true;

            private readonly List<PlatformFoothold> _out = new List<PlatformFoothold>();
            private readonly VisibleTopEdgeSolver _solver = new VisibleTopEdgeSolver();

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
            {
                _out.Clear();
                if (!ApplyOcclusionFilter)
                {
                    for (int i = 0; i < RawFrontToBack.Count; i++) _out.Add(RawFrontToBack[i]);
                    return _out;
                }

                _solver.Begin();
                for (int i = 0; i < RawFrontToBack.Count; i++) _solver.AddWindow(RawFrontToBack[i].ScreenRect);
                _solver.Solve(MinVisibleWidth, false, default);

                for (int s = 0; s < _solver.SegmentCount; s++)
                {
                    int i = _solver.GetSegmentWindowIndex(s);
                    PlatformFoothold src = RawFrontToBack[i];
                    Rect r = src.ScreenRect;
                    _out.Add(new PlatformFoothold(src.Handle,
                        new Rect(_solver.GetSegmentStartX(s), r.y, _solver.GetSegmentWidth(s), r.height),
                        src.IsTopmost));
                }
                return _out;
            }

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
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private ZOrderedWindowService _service;
        private float _backTopOsY;
        private float _frontTopOsY;
        private float _hiddenOsX;
        private float _visibleOsX;

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
            _agent = null;
        }

        private IEnumerator SetUpOverlappingWindows()
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

            float w = Screen.width;
            float h = Screen.height;
            _frontTopOsY = h * 0.25f;
            _backTopOsY = h * 0.45f;
            _hiddenOsX = w * 0.50f;
            _visibleOsX = w * 0.20f;

            _service = new ZOrderedWindowService();
            // z0 = 맨 앞 창.
            _service.RawFrontToBack.Add(new PlatformFoothold(FrontHandle,
                new Rect(w * 0.30f, _frontTopOsY, w * 0.55f, h * 0.50f), true));
            // z1 = 뒤 창. 상단선이 앞 창의 세로 구간 안에 들어가므로 앞 창이 그 경계를 덮는다.
            _service.RawFrontToBack.Add(new PlatformFoothold(BackHandle,
                new Rect(w * 0.10f, _backTopOsY, w * 0.60f, h * 0.30f), false));

            bb.IntentSource = new StillIntentSource();

            // 전제 확인 — 배치가 실제로 "가려짐"을 만들어야 이 테스트가 의미를 갖는다.
            Assert.Greater(_hiddenOsX, w * 0.30f, $"{LogPrefix} 전제 실패 — 가려진 x가 앞 창 왼쪽 밖입니다.");
            Assert.Less(_hiddenOsX, w * 0.85f, $"{LogPrefix} 전제 실패 — 가려진 x가 앞 창 오른쪽 밖입니다.");
            Assert.Less(_visibleOsX, w * 0.30f, $"{LogPrefix} 전제 실패 — '보이는' x가 앞 창에 덮여 있습니다.");
            Assert.Greater(_visibleOsX, w * 0.10f, $"{LogPrefix} 전제 실패 — '보이는' x가 뒤 창 밖입니다.");
            Assert.Greater((w * 0.30f) - (w * 0.10f), MinVisibleWidth,
                $"{LogPrefix} 전제 실패 — 뒤 창의 보이는 조각이 최소 폭보다 좁아 이 해상도에서는 관측할 수 없습니다.");

            Debug.Log($"{LogPrefix} 준비 — 화면 {w}x{h}, 앞 창 상단 OS y={_frontTopOsY:F0}(x {w * 0.30f:F0}~{w * 0.85f:F0}), " +
                $"뒤 창 상단 OS y={_backTopOsY:F0}(x {w * 0.10f:F0}~{w * 0.70f:F0}), " +
                $"가려진 x={_hiddenOsX:F0}, 보이는 x={_visibleOsX:F0}.");
        }

        /// <summary>주어진 OS 좌표를 발 위치로 삼아 "그 발판을 딛고 있다"고 주장했을 때 실제로 접지되는가.</summary>
        private bool IsGroundedAt(float osX, float osY, long claimedHandle, bool occlusionFilter)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            _service.ApplyOcclusionFilter = occlusionFilter;
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller.PollImmediately();

            Vector3 world = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera, new Vector2(osX, osY), 10f, _clonedConfig);
            bb.MoveBodyToWorld(new Vector2(world.x, world.y));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = claimedHandle;
            bb.ResetGroundLossTimer();

            GroundSensor.GroundInfo info = bb.SenseGround();
            Debug.Log($"{LogPrefix} 측정 — OS({osX:F0},{osY:F0}) 핸들={claimedHandle} 가려짐필터={(occlusionFilter ? "ON" : "OFF")} " +
                $"-> 발판 {bb.FootholdPoller.CachedFootholds.Count}개, 접지={info.Grounded}");
            return info.Grounded;
        }

        // ============================================================================
        // O1 / O1n — 신고 재현과 네거티브 컨트롤
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator O1_가려진_경계에서는_접지되지_않는다()
        {
            yield return SetUpOverlappingWindows();

            bool grounded = IsGroundedAt(_hiddenOsX, _backTopOsY, BackHandle, occlusionFilter: true);
            Assert.IsFalse(grounded,
                $"{LogPrefix} 신고 재현 — 앞 창에 완전히 덮인 x에서 뒤 창의 경계에 접지됐습니다. " +
                $"사용자 눈에는 캐릭터가 허공(또는 다른 창 위)을 걷는 것으로 보입니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator O1n_네거티브_수정_전_열거_규칙이면_같은_자리에서_접지된다()
        {
            yield return SetUpOverlappingWindows();

            bool groundedBefore = IsGroundedAt(_hiddenOsX, _backTopOsY, BackHandle, occlusionFilter: false);
            Assert.IsTrue(groundedBefore,
                $"{LogPrefix} 네거티브 컨트롤이 성립하지 않습니다 — 수정 전 규칙에서도 접지가 안 되면 " +
                $"O1은 버그를 잡는 단언이 아니라 항상 참인 단언입니다. 배치를 다시 설계해야 합니다.");

            bool groundedAfter = IsGroundedAt(_hiddenOsX, _backTopOsY, BackHandle, occlusionFilter: true);
            Assert.IsFalse(groundedAfter, $"{LogPrefix} 수정 전/후가 같은 답을 냅니다 — 수정이 실효가 없습니다.");

            Debug.Log($"{LogPrefix} O1n 네거티브 컨트롤 성립 — 수정 전: 접지됨(버그) / 수정 후: 접지 안 됨.");
        }

        // ============================================================================
        // O2 / O3 — 과잉 제거 방지
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator O2_실제로_보이는_구간에서는_여전히_접지된다()
        {
            yield return SetUpOverlappingWindows();

            bool grounded = IsGroundedAt(_visibleOsX, _backTopOsY, BackHandle, occlusionFilter: true);
            Assert.IsTrue(grounded,
                $"{LogPrefix} 앞 창 왼쪽으로 삐져나와 **실제로 보이는** 구간까지 발판이 사라졌습니다 — " +
                $"이건 과잉 제거입니다(가려진 동안만 막아야 하며 기능을 죽이면 안 됩니다).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator O3_맨_앞_창은_아무_영향도_받지_않는다()
        {
            yield return SetUpOverlappingWindows();

            bool grounded = IsGroundedAt(_hiddenOsX, _frontTopOsY, FrontHandle, occlusionFilter: true);
            Assert.IsTrue(grounded,
                $"{LogPrefix} 맨 앞 창이 발판을 잃었습니다 — z-order 방향이 뒤집혔거나 가려짐 계산이 " +
                $"자기 자신까지 지우고 있습니다.");
        }

        // ============================================================================
        // O4 — 실제 캐릭터: 가려진 경계 위에 세워두면 머물지 못한다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator O4_가려진_경계_위에_세워두면_그_자리에_머물지_못한다()
        {
            yield return SetUpOverlappingWindows();

            StickmanBlackboard bb = _agent.Blackboard;
            _service.ApplyOcclusionFilter = true;
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller.PollImmediately();

            Vector3 standWorld = ScreenCoordinateConverter.OsScreenToWorld(
                bb.MainCamera, new Vector2(_hiddenOsX, _backTopOsY), 10f, _clonedConfig);
            bb.MoveBodyToWorld(new Vector2(standWorld.x, standWorld.y));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = BackHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            float startY = bb.Body.position.y;

            // ★ 매 프레임 표본을 뜬다(끝에서 한 번만 재지 않는다). 이유: 낙하가 화면 하단 클램프에
            // 걸려 속도가 0이 되면 "Dock 사각지대 즉시 회수"(TryLiftOutOfSinkhole)가 캐릭터를 **위쪽
            // 발판으로 다시 올려세울 수 있다.** 마지막 프레임만 보면 그 회수 때문에 y가 되돌아와 이
            // 테스트가 타이밍에 따라 흔들린다. 최저점(누적 최솟값)과 "한 번이라도 접지했는가"는
            // 나중에 무슨 일이 일어나도 뒤집히지 않는 관측이라 플레이키하지 않다.
            bool everGroundedOnHiddenEdge = false;
            float lowestY = startY;
            float deadline = Time.time + 1.5f;
            while (Time.time < deadline)
            {
                if (bb.CurrentFootholdHandle == BackHandle && bb.SenseGround().Grounded)
                {
                    everGroundedOnHiddenEdge = true;
                }
                lowestY = Mathf.Min(lowestY, bb.Body.position.y);
                yield return null;
            }

            Debug.Log($"{LogPrefix} O4 — 가려진 경계 월드Y={startY:F3}, 1.5초간 최저 Y={lowestY:F3} " +
                $"(낙하 {startY - lowestY:F3}유닛), 가려진 경계 접지 관측={everGroundedOnHiddenEdge}, " +
                $"현재 발판 핸들={bb.CurrentFootholdHandle}, 상태={bb.Machine.CurrentStateId}.");

            Assert.IsFalse(everGroundedOnHiddenEdge,
                $"{LogPrefix} 캐릭터가 가려진 뒤 창의 경계에 접지했습니다 — 사용자가 신고한 " +
                $"'보이지 않는 창의 경계를 따라 걷는' 그림 그대로입니다.");
            Assert.Less(lowestY, startY - 0.5f,
                $"{LogPrefix} 캐릭터가 가려진 경계 높이를 한 번도 벗어나지 않았습니다" +
                $"(최대 낙하 {startY - lowestY:F3}유닛) — 발판이 아직 그 자리에 남아 있다는 뜻입니다.");
        }
    }
}
