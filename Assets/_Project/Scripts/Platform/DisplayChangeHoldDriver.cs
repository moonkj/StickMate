using System;
using System.Globalization;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 — 화면 변경 유예의 <b>구동기</b>(플랫폼 중립). 두 플랫폼 Enforcer가 <b>같은 코드</b>를 부르고,
    /// 플랫폼 파일은 신호를 넘기고 사실 조회 훅을 주입할 뿐이다(CLAUDE.md "정책은 중립, 플랫폼은 사실 조회").
    ///
    /// <para>부작용(렌더 간격 입력, OS 모니터 목록 강제 갱신, 상태 게시, 원장·로그)은 전부 <see cref="Hooks"/>와
    /// 중립 타입을 거친다 — 그래서 <b>재개 순서</b>("목록 갱신이 해제 게시보다 먼저")를 EditMode가 가짜 훅으로
    /// 실행해 잠근다. 시계와 프레임 번호도 호출자가 넘긴다.</para>
    ///
    /// <para><b>끄기 스위치가 켜져 있으면</b>(<see cref="DisplayChangeHoldPolicy.DisableEnvironmentVariable"/>) 상태기계가
    /// 아무 신호도 받지 않으므로 이 구동기는 어떤 훅도 부르지 않는다.</para>
    /// </summary>
    public sealed class DisplayChangeHoldDriver
    {
        public const string LogTag = "[화면변경유예]";

        /// <summary>플랫폼이 주입하는 사실 조회·실행 훅. 전부 메인 스레드에서 불린다.</summary>
        public sealed class Hooks
        {
            public string PlatformTag = "?";
            /// <summary>전체화면 재적합이 아직 끝나지 않았는가.</summary>
            public Func<bool> IsFitPending;
            /// <summary>OS 모니터 목록을 게이트를 우회해 지금 한 번 갱신·게시한다.</summary>
            public Action ForceRefreshOsMonitors;
            /// <summary>FramePacing에 유예 입력을 넘긴다(렌더 간격을 쓰는 곳은 FramePacing 한 곳뿐).</summary>
            public Action<bool> SetRenderHold;
            /// <summary>실측 렌더 콜백이 온 프레임 누적 수.</summary>
            public Func<int> ActualRenderedFrames;
            /// <summary><c>Time.renderedFrameCount</c>(보조 계기 — 거짓말할 수 있다).</summary>
            public Func<int> RenderedFrameCount;
            /// <summary>지금 실제로 걸린 렌더 간격.</summary>
            public Func<int> EffectiveRenderFrameInterval;
            /// <summary>모니터 수(원장용).</summary>
            public Func<int> MonitorCount;
        }

        private readonly Hooks _hooks;
        private readonly DisplayChangeRenderHold _hold;
        private int _startFrame;
        private double _startTime;
        private int _startActualRenders;
        private int _startRenderedFrames;
        private int _holdInterval;
        private bool _fitDeferredNoted;

        /// <param name="hooks">플랫폼 사실 조회·실행 훅.</param>
        /// <param name="armingLatch">★ 무장의 유일한 원천 — Enforcer의 적합 확정 판정 객체(<see cref="FullScreenFitLatchSignal"/>).
        /// 이 구동기에는 무장 메서드가 없다(verify-change 2차 X2c: 호출 자리를 틱으로 옮겨도 초록이던 구멍).</param>
        /// <param name="disabled">끄기 스위치.</param>
        public DisplayChangeHoldDriver(Hooks hooks, FullScreenFitLatchSignal armingLatch, bool disabled)
        {
            _hooks = hooks ?? new Hooks();
            _hold = new DisplayChangeRenderHold(disabled, armingLatch);
        }

        public bool IsDisabled => _hold.IsDisabled;
        public bool IsHolding => _hold.IsHolding;
        /// <summary>지금 재적합을 보류해야 하는가(조용한 구간).</summary>
        public bool ShouldDeferFit => _hold.ShouldDeferFit;
        /// <summary>진단/테스트용 상태기계.</summary>
        public DisplayChangeRenderHold State => _hold;

        public void OnLibraryMonitorChanged(double now, int frame) => Handle(_hold.OnLibraryMonitorChanged(now), now, frame);

        public void OnTopologyTransition(TopologyForensicsTransition transition, double now, int frame)
            => Handle(_hold.OnTopologyTransition(transition, now), now, frame);

        /// <summary>매 프레임(Enforcer Update의 이른 자리 — 부착·컨트롤러 조기 반환보다 앞).</summary>
        public void Tick(double now, int frame)
        {
            if (!_hold.IsHolding) return;
            bool fitPending = _hooks.IsFitPending != null && _hooks.IsFitPending();
            Handle(_hold.Tick(now, fitPending), now, frame);
        }

        /// <summary>재적합 틱이 보류됐다. 유예 한 번에 한 줄만 남긴다.</summary>
        public void NotifyFitDeferred(int frame)
        {
            if (_fitDeferredNoted || !_hold.IsHolding) return;
            _fitDeferredNoted = true;
            if (FreezeForensics.IsActive)
            {
                FreezeForensics.Record(FreezeForensicsEvent.FitDeferred, MonitorCount(),
                    $"{_hooks.PlatformTag} 유예 #{_hold.EpisodeNumber} 조용한 구간 — SetResolution·리사이즈·이동 보류(시도 횟수 미소모), frame={frame}");
            }
        }

        private void Handle(DisplayChangeHoldEvent evt, double now, int frame)
        {
            switch (evt)
            {
                case DisplayChangeHoldEvent.Started: OnStarted(now, frame); break;
                case DisplayChangeHoldEvent.RefitAllowed: OnRefitAllowed(now, frame); break;
                case DisplayChangeHoldEvent.Released: OnReleased(now, frame); break;
            }
        }

        private void OnStarted(double now, int frame)
        {
            _startFrame = frame;
            _startTime = now;
            _startActualRenders = _hooks.ActualRenderedFrames != null ? _hooks.ActualRenderedFrames() : 0;
            _startRenderedFrames = _hooks.RenderedFrameCount != null ? _hooks.RenderedFrameCount() : 0;
            _fitDeferredNoted = false;

            _hooks.SetRenderHold?.Invoke(true);
            _holdInterval = _hooks.EffectiveRenderFrameInterval != null ? _hooks.EffectiveRenderFrameInterval() : 0;
            DisplayChangeHoldStatus.PublishStarted(_hold.EpisodeNumber, _hold.StartReason, frame);

            string detail = $"{_hooks.PlatformTag} 유예 #{_hold.EpisodeNumber} 시작 — 사유={_hold.StartReason}, " +
                $"렌더간격={_holdInterval}(억제값 {DisplayChangeHoldPolicy.SuppressedRenderFrameInterval}), " +
                $"조용한구간={DisplayChangeHoldPolicy.QuietAfterSettleSeconds:F1}초(안정 후), 상한={DisplayChangeHoldPolicy.MaxHoldSeconds:F0}초, " +
                "클릭관통·투명·항상위 무변경";
            if (FreezeForensics.IsActive) FreezeForensics.Record(FreezeForensicsEvent.RenderHoldStarted, MonitorCount(), detail);
            Debug.Log(LogTag + " " + detail + ". 끄려면 " + DisplayChangeHoldPolicy.DisableEnvironmentVariable + "=1.");
        }

        private void OnRefitAllowed(double now, int frame)
        {
            string detail = $"{_hooks.PlatformTag} 유예 #{_hold.EpisodeNumber} 조용한 구간 끝 — 렌더 억제는 유지한 채 재적합 허용 " +
                $"(경과 {(now - _startTime).ToString("F1", CultureInfo.InvariantCulture)}초, frame={frame})";
            if (FreezeForensics.IsActive) FreezeForensics.Record(FreezeForensicsEvent.RenderHoldRefitAllowed, MonitorCount(), detail);
            Debug.Log(LogTag + " " + detail + ".");
        }

        private void OnReleased(double now, int frame)
        {
            // ★ 순서가 계약이다(DisplayChangeHoldStatus 보증 1): 목록 강제 갱신 → 렌더 간격 복귀 → 해제 게시.
            //   해제를 먼저 게시하면 그 신호를 받은 쪽이 낡은 목록(최악 약 1.25초)으로 발판을 다시 읽는다.
            bool refreshed = true;
            try { _hooks.ForceRefreshOsMonitors?.Invoke(); }
            catch (Exception) { refreshed = false; }   // 갱신 실패로 해제를 막지 않는다 — 상한 보증이 더 크다.

            _hooks.SetRenderHold?.Invoke(false);
            DisplayChangeHoldStatus.PublishReleased(_hold.EpisodeNumber, _hold.LastReleaseReason, frame);

            long loopFrames = Math.Max(0, frame - _startFrame);
            int actual = _hooks.ActualRenderedFrames != null ? _hooks.ActualRenderedFrames() - _startActualRenders : -1;
            int rendered = _hooks.RenderedFrameCount != null ? _hooks.RenderedFrameCount() - _startRenderedFrames : -1;
            double expected = DisplayChangeHoldPolicy.ExpectedSubmissions(loopFrames, _holdInterval);
            string detail = $"{_hooks.PlatformTag} 유예 #{_hold.EpisodeNumber} 해제 — 사유={_hold.LastReleaseReason}, " +
                $"지속 {(now - _startTime).ToString("F2", CultureInfo.InvariantCulture)}초, 시작frame={_startFrame}, 해제frame={frame}, " +
                $"루프 {loopFrames}프레임, ★실측 렌더 제출 {actual}장(기대 약 {expected.ToString("F1", CultureInfo.InvariantCulture)}장, 간격 {_holdInterval}), " +
                $"renderedFrameCount 증가 {rendered}, OS 모니터 목록 강제 갱신={(refreshed ? "완료" : "실패")}";
            if (FreezeForensics.IsActive) FreezeForensics.Record(FreezeForensicsEvent.RenderHoldEnded, MonitorCount(), detail);
            Debug.Log(LogTag + " " + detail + ".");
        }

        private int MonitorCount()
        {
            try { return _hooks.MonitorCount != null ? _hooks.MonitorCount() : -1; }
            catch (Exception) { return -1; }
        }
    }
}
