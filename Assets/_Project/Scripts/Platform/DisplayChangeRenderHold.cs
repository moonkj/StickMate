using System;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 (리더 채택 완화 1안) — <b>화면 변경 유예</b>의 상수·판정. 플랫폼 중립, 순수 함수.
    ///
    /// ============================================================================
    /// 무엇을 하나 — 모니터가 빠지는 순간 우리가 GPU에 가하는 일을 줄인다
    /// ============================================================================
    /// 신고: Windows에서 모니터를 떼자 PC 전체가 멈췄다(커서·Ctrl+Alt+Del 불가, StickMate를 끄면 무사,
    /// 떼어낸 모니터·StickMate·DWM 모두 같은 Intel 어댑터). 원인은 미확정이고, 우리 기여 후보는 둘이다 —
    /// (a) 모니터 크기 BitBlt 스왑체인의 Present마다 DWM 리디렉션 복사, (b) 제거 약 1초 뒤 재적합의
    /// SetResolution·리사이즈. 그래서 화면 구성 변화의 가장 이른 신호부터 <b>렌더 제출을 크게 줄이고</b>,
    /// 재적합은 조용한 구간이 끝난 뒤로 미룬다.
    ///
    /// ============================================================================
    /// 이 장치가 <b>절대 하지 않는 것</b>
    /// ============================================================================
    /// 클릭 관통·투명·항상위 상태를 건드리지 않는다(원칙 2). 렌더러를 끄지 않고, 연출을 취소하지 않는다.
    /// 렌더 간격을 직접 쓰지 않는다 — 쓰는 곳은 <c>FramePacing</c> 한 곳뿐이고 이 유예는 그 계산의 입력이다
    /// (<see cref="ResolveRenderFrameInterval"/>). 직접 쓰면 해제 뒤 다음 등급 전환까지 억제값이 남는다(R-2b).
    ///
    /// <para><b>정직한 한계</b>: 감지는 OS가 모드 변경을 시작한 <b>뒤</b>다. 드라이버가 수백 ms 안에 멈추면
    /// 못 막는다. 그리고 <c>renderFrameInterval</c>이 Windows에서 실제로 제출을 줄이는지는 미확인이다
    /// (macOS 실측: 간격 4에서 기대 15장/초 대비 실측 38.9장/초) — 그래서 해제 줄에 실측 제출 수를 기대값과
    /// 나란히 적는다. 그 숫자가 이 장치가 효과를 냈는지의 판정 근거다.</para>
    /// </summary>
    public static class DisplayChangeHoldPolicy
    {
        /// <summary>
        /// 끄기 스위치. <c>1</c>(또는 <c>0</c>이 아닌 아무 값)이면 유예가 한 번도 시작되지 않는다 — 구독도, 렌더 간격
        /// 입력도, 재적합 보류도, 원장 줄도 없다(이전 동작과 같음). 기존 <c>STICKMATE_KEEP_LAYERED</c> ·
        /// <c>STICKMATE_KEEP_TASKBAR_BUTTON</c>과 같은 관례("비었거나 0이면 기본 동작").
        /// </summary>
        public const string DisableEnvironmentVariable = "STICKMATE_DISABLE_DISPLAY_CHANGE_HOLD";

        /// <summary>
        /// 유예 중 렌더 간격. 루프 60Hz에서 초당 약 2장. 0(완전 정지)이 아닌 이유: <c>renderFrameInterval</c>은
        /// 무한대가 없고, 재적합 단계(<see cref="DisplayChangeHoldPhase.Refit"/>)에서 스왑체인 재생성 뒤 몇 장은
        /// 나가야 창 기하 되읽기가 수렴한다(실기 미확인 — 벽시계 상한이 최후 방어다).
        /// </summary>
        public const int SuppressedRenderFrameInterval = 30;

        /// <summary>
        /// 안정(또는 원복) 신호 뒤 조용히 기다리는 시간(초). <b>추정값, 측정 아님</b> — Windows가 사라진 모니터의 창을
        /// 남은 모니터로 옮기고 DWM·드라이버가 합성 경로를 다시 세우는 구간을 덮으려는 여유다. 사용자가 "몇 초 멈춤"을
        /// 받아들일 수 있는 범위(리더 판정)의 아래쪽으로 잡았다.
        /// </summary>
        public const double QuietAfterSettleSeconds = 3.0;

        /// <summary>
        /// 라이브러리 모니터 변경 신호만 오고 우리 토폴로지 감시기가 아직 변화를 못 본 경우의 유예(초).
        /// 감지 지연(표본 Windows 0.25초 / macOS 0.1초)보다 충분히 길어서, 진짜 변화라면 그 안에 t0가 와서
        /// "안정 대기"로 넘어간다. 오지 않으면(해상도 신호뿐이었다 등) 이 시간 뒤 해제한다.
        /// </summary>
        public const double EarlySignalHoldSeconds = 2.0;

        /// <summary>
        /// ★ R-2a — <b>감시기와 무관한 벽시계 상한</b>(초). 어떤 입력이 오든(흔들림 연속, 관측 실패 연속, 재적합 중
        /// 관측 스킵, 재적합이 끝내 수렴하지 않음) 시작 후 이 시간이면 반드시 해제한다. 근거: 감시기는 관측 실패를
        /// 무시하고 흔들릴 때마다 안정 타이머를 0으로 되돌린다 — 덮개 닫힘·RDP·헐거운 케이블·KVM에서 안정 신호가
        /// 영원히 안 올 수 있다. 정상 최악(감지 0.25 + 디바운스 0.75 + 조용 3 + 재적합 최대 약 3초)의 두 배 남짓이다.
        /// </summary>
        public const double MaxHoldSeconds = 15.0;

        /// <summary>환경 변수 값 → 꺼짐 여부(비었거나 <c>0</c>이면 켜짐 = 기본 동작).</summary>
        public static bool IsDisabledByEnvironmentValue(string raw) => !string.IsNullOrEmpty(raw) && raw != "0";

        /// <summary>실제 환경에서 끄기 스위치를 읽는다. 읽기 실패는 "켜짐"(기본)으로 본다.</summary>
        public static bool ReadDisabledFromEnvironment()
        {
            try { return IsDisabledByEnvironmentValue(Environment.GetEnvironmentVariable(DisableEnvironmentVariable)); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// ★ R-2b — <c>FramePacing</c>가 실제로 쓸 렌더 간격. 유예 중이면 등급 값과 억제 값 중 <b>큰 쪽</b>
        /// (등급이 이미 더 깊으면 그대로), 아니면 <b>등급 값 그대로</b>. 유예가 끝나는 호출에서 등급 값으로 정확히 돌아간다.
        /// </summary>
        public static int ResolveRenderFrameInterval(int tierInterval, bool holding)
        {
            int tier = Math.Max(1, tierInterval);
            return holding ? Math.Max(tier, SuppressedRenderFrameInterval) : tier;
        }

        /// <summary>유예 중 기대 제출 수(루프 프레임 / 간격). 해제 줄에 실측과 나란히 적는다.</summary>
        public static double ExpectedSubmissions(long loopFrames, int renderFrameInterval)
            => loopFrames <= 0 ? 0.0 : (double)loopFrames / Math.Max(1, renderFrameInterval);
    }

    /// <summary>유예의 단계.</summary>
    public enum DisplayChangeHoldPhase
    {
        /// <summary>유예 없음.</summary>
        Idle = 0,
        /// <summary>조용한 구간 — 렌더 억제 + 재적합 보류.</summary>
        Quiet,
        /// <summary>재적합 허용 구간 — 렌더 억제는 유지한 채 SetResolution·리사이즈를 끝낸다(소은 #3: 정지 해제 튐과
        /// 재적합 크기 튐을 한 번으로 합친다).</summary>
        Refit,
    }

    /// <summary>유예가 왜 시작됐나.</summary>
    public enum DisplayChangeHoldStartReason
    {
        None = 0,
        /// <summary>UniWindowController의 모니터 변경 통지(가장 이른 신호).</summary>
        LibraryMonitorChanged,
        /// <summary>우리 토폴로지 감시기의 변화 감지(t0).</summary>
        TopologyChangeDetected,
    }

    /// <summary>유예가 왜 끝났나(원장에 그대로 적는다).</summary>
    public enum DisplayChangeHoldReleaseReason
    {
        None = 0,
        /// <summary>안정 신호 → 조용한 구간 → (필요했다면) 재적합 완료.</summary>
        Settled,
        /// <summary>흔들렸다가 원래 구성으로 돌아왔다.</summary>
        Reverted,
        /// <summary>라이브러리 신호만 오고 토폴로지 변화는 끝내 없었다.</summary>
        EarlySignalExpired,
        /// <summary>★ R-2a 벽시계 상한 도달 — 감시기 입력과 무관하게 해제했다.</summary>
        SafetyCap,
    }

    /// <summary>상태기계 한 번의 결과.</summary>
    public enum DisplayChangeHoldEvent
    {
        None = 0,
        Started,
        RefitAllowed,
        Released,
    }

    /// <summary>
    /// 화면 변경 유예 상태기계. <b>메인 스레드 전용</b>, 시계는 호출자가 넘긴다(테스트가 시간을 손으로 흘린다).
    /// 참조 형식이다(readonly 필드 복사 함정 — <see cref="WallClockIntervalGate"/> 문서).
    ///
    /// <para><b>무장</b>: 첫 전체화면 적합이 확정되기 전에는 어떤 신호도 무시한다 — 라이브러리는
    /// 창을 붙잡는 순간에도 모니터 변경을 통지하고, 기동 적합을 늦추면 기동 흰 배경 구간이 길어진다(09-07 버그 계열).</para>
    ///
    /// <para>★ 2026-09-14 (verify-change 2차 X2c) — <b>공개 무장 메서드가 없다.</b> 무장의 유일한 원천은 생성자에 넘긴
    /// <see cref="FullScreenFitLatchSignal"/>의 확정 통지다. 옛 <c>Arm()</c>은 어디서나 부를 수 있어서, 호출 자리를 매 프레임
    /// 틱으로 옮겨도 아무 테스트도 빨개지지 않았다.</para>
    /// </summary>
    public sealed class DisplayChangeRenderHold
    {
        private readonly bool _disabled;
        private bool _armed;
        private FullScreenFitLatchSignal _armingLatch;
        private DisplayChangeHoldPhase _phase;
        private double _startedAt;
        private double _quietUntil = -1.0;
        private DisplayChangeHoldReleaseReason _quietCause;

        /// <param name="disabled">끄기 스위치 — 참이면 영원히 무장하지 않는다.</param>
        /// <param name="armingLatch">무장의 <b>유일한</b> 원천. null이면 영원히 무장하지 않는다(= 유예 없음, 이전 동작).</param>
        public DisplayChangeRenderHold(bool disabled, FullScreenFitLatchSignal armingLatch)
        {
            _disabled = disabled;
            if (_disabled || armingLatch == null) return;
            if (armingLatch.HasLatchedOnce)
            {
                _armed = true;   // 늦게 만들어진 경우(Enforcer의 지연 생성) — 확정은 이미 섰다.
                return;
            }
            _armingLatch = armingLatch;
            armingLatch.Latched += OnFitLatched;
        }

        private void OnFitLatched()
        {
            _armed = true;
            // 무장은 되돌리지 않는다 — 구독을 더 들고 있을 이유가 없다(호출 목록은 올릴 때 복사되므로 여기서 떼도 안전하다).
            if (_armingLatch == null) return;
            _armingLatch.Latched -= OnFitLatched;
            _armingLatch = null;
        }

        public bool IsDisabled => _disabled;
        public bool IsArmed => _armed;
        public DisplayChangeHoldPhase Phase => _phase;
        public bool IsHolding => _phase != DisplayChangeHoldPhase.Idle;
        /// <summary>지금 재적합(SetResolution·리사이즈·이동)을 보류해야 하는가 — 조용한 구간에서만 참.</summary>
        public bool ShouldDeferFit => _phase == DisplayChangeHoldPhase.Quiet;
        public double StartedAt => _startedAt;
        public int EpisodeNumber { get; private set; }
        public DisplayChangeHoldStartReason StartReason { get; private set; }
        public DisplayChangeHoldReleaseReason LastReleaseReason { get; private set; }

        /// <summary>라이브러리 모니터 변경 통지(가장 이른 신호).</summary>
        public DisplayChangeHoldEvent OnLibraryMonitorChanged(double now)
        {
            if (_disabled || !_armed) return DisplayChangeHoldEvent.None;
            if (_phase == DisplayChangeHoldPhase.Idle)
            {
                Start(DisplayChangeHoldStartReason.LibraryMonitorChanged, now);
                _quietUntil = now + DisplayChangeHoldPolicy.EarlySignalHoldSeconds;
                _quietCause = DisplayChangeHoldReleaseReason.EarlySignalExpired;
                return DisplayChangeHoldEvent.Started;
            }

            // 유예 중 새 신호 — 재적합 중이었다면 다시 조용한 구간으로. 안정 대기(-1) 중이면 그대로 기다린다.
            _phase = DisplayChangeHoldPhase.Quiet;
            if (_quietUntil >= 0.0)
                _quietUntil = Math.Max(_quietUntil, now + DisplayChangeHoldPolicy.EarlySignalHoldSeconds);
            return DisplayChangeHoldEvent.None;
        }

        /// <summary>토폴로지 감시기 관측 한 번의 분류(<see cref="FreezeForensicsPolicy.ClassifyTopologyTransition"/>).</summary>
        public DisplayChangeHoldEvent OnTopologyTransition(TopologyForensicsTransition transition, double now)
        {
            if (_disabled || !_armed || transition == TopologyForensicsTransition.None) return DisplayChangeHoldEvent.None;

            switch (transition)
            {
                case TopologyForensicsTransition.ChangeDetected:
                    if (_phase == DisplayChangeHoldPhase.Idle)
                    {
                        Start(DisplayChangeHoldStartReason.TopologyChangeDetected, now);
                        _quietUntil = -1.0;
                        _quietCause = DisplayChangeHoldReleaseReason.Settled;
                        return DisplayChangeHoldEvent.Started;
                    }
                    _phase = DisplayChangeHoldPhase.Quiet;
                    _quietUntil = -1.0;   // 새 변화 — 안정 신호를 다시 기다린다(상한은 시작 시각 기준 그대로).
                    return DisplayChangeHoldEvent.None;

                case TopologyForensicsTransition.Settled:
                case TopologyForensicsTransition.Reverted:
                    if (_phase == DisplayChangeHoldPhase.Idle) return DisplayChangeHoldEvent.None;
                    _phase = DisplayChangeHoldPhase.Quiet;
                    _quietUntil = now + DisplayChangeHoldPolicy.QuietAfterSettleSeconds;
                    _quietCause = transition == TopologyForensicsTransition.Settled
                        ? DisplayChangeHoldReleaseReason.Settled
                        : DisplayChangeHoldReleaseReason.Reverted;
                    return DisplayChangeHoldEvent.None;

                default:
                    return DisplayChangeHoldEvent.None;
            }
        }

        /// <summary>
        /// 매 프레임. <paramref name="fitPending"/> = 전체화면 재적합이 아직 끝나지 않았는가.
        /// ★ 상한 검사가 <b>맨 앞</b>이다 — 어떤 단계·입력에서도 상한이 이긴다(R-2a).
        /// </summary>
        public DisplayChangeHoldEvent Tick(double now, bool fitPending)
        {
            if (_phase == DisplayChangeHoldPhase.Idle) return DisplayChangeHoldEvent.None;
            if (now - _startedAt >= DisplayChangeHoldPolicy.MaxHoldSeconds)
                return Release(DisplayChangeHoldReleaseReason.SafetyCap);

            if (_phase == DisplayChangeHoldPhase.Quiet)
            {
                if (_quietUntil < 0.0 || now < _quietUntil) return DisplayChangeHoldEvent.None;
                if (fitPending)
                {
                    _phase = DisplayChangeHoldPhase.Refit;
                    return DisplayChangeHoldEvent.RefitAllowed;
                }
                return Release(_quietCause);
            }

            // Refit — 재적합이 끝나면(또는 필요 없어지면) 해제.
            return fitPending ? DisplayChangeHoldEvent.None : Release(_quietCause);
        }

        private void Start(DisplayChangeHoldStartReason reason, double now)
        {
            _phase = DisplayChangeHoldPhase.Quiet;
            _startedAt = now;
            EpisodeNumber++;
            StartReason = reason;
        }

        private DisplayChangeHoldEvent Release(DisplayChangeHoldReleaseReason reason)
        {
            _phase = DisplayChangeHoldPhase.Idle;
            _quietUntil = -1.0;
            LastReleaseReason = reason;
            return DisplayChangeHoldEvent.Released;
        }
    }

    /// <summary>
    /// ★★ 2026-09-14 — <b>coder용 읽기 전용 계약</b>(캐릭터 "보존 동결"이 이것을 읽는다). 메인 스레드 전용.
    ///
    /// <para><b>뜻</b>: <see cref="IsActive"/>가 참인 동안 화면 구성이 바뀌는 중이고 렌더 제출이 억제돼 있다
    /// (제출되는 장면이 드물다). 캐릭터 쪽은 이 구간을 "보존 동결"로 다룬다(상태 Tick·물리·기한·말풍선 시계 정지 —
    /// 구현은 coder 소관). 이 장치는 렌더러를 끄지 않고, 연출을 취소하지 않고, 말풍선을 숨기지 않는다.</para>
    ///
    /// <para><b>보증</b> (쓰는 쪽 <see cref="DisplayChangeHoldDriver"/>가 지키고 EditMode 테스트가 잠근다):</para>
    /// <list type="number">
    ///   <item><b>재개 순서</b>: <see cref="IsActive"/>가 false로 바뀌어 보이는 첫 읽기 시점에는 OS 모니터 목록 강제 갱신
    ///     (<c>OverlayMonitorDirectory</c> 게시 포함)이 <b>이미 끝났다</b>. 같은 호출 안에서 갱신 → 렌더 간격 복귀 →
    ///     이 상태 게시 순서다. 해제 신호를 받고 발판을 다시 읽으면 목록은 최신이다.</item>
    ///   <item><b>재적합 뒤 해제</b>: 해제는 전체화면 재적합(SetResolution·리사이즈)이 끝난 뒤다(필요 없었으면 조용한 구간 끝).
    ///     단 벽시계 상한(<see cref="DisplayChangeHoldPolicy.MaxHoldSeconds"/>)이 언제나 우선한다.</item>
    ///   <item><b>상한</b>: 시작 후 <see cref="DisplayChangeHoldPolicy.MaxHoldSeconds"/>초 안에 반드시 해제된다.</item>
    ///   <item><b>에피소드</b>: 시작마다 <see cref="EpisodeNumber"/>가 1 늘어난다(0 = 이번 실행에서 없음). 시작·해제 가장자리는
    ///     <c>IsActive</c> 변화 또는 번호 변화로 잡는다.</item>
    ///   <item>클릭 관통·투명·항상위 상태는 이 장치가 절대 바꾸지 않는다(원칙 2).</item>
    /// </list>
    /// <para>쓰기(<c>Publish*</c>)는 <c>Platform/</c>의 구동기 전용이다.</para>
    /// </summary>
    public static class DisplayChangeHoldStatus
    {
        /// <summary>지금 화면 변경 유예(보존 동결 구간) 중인가.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>이번 실행에서 몇 번째 유예인가(시작마다 +1, 0 = 아직 없음).</summary>
        public static int EpisodeNumber { get; private set; }

        /// <summary>현재(또는 마지막) 유예의 시작 사유.</summary>
        public static DisplayChangeHoldStartReason StartReason { get; private set; }

        /// <summary>마지막으로 끝난 유예의 해제 사유(끝난 적 없으면 None).</summary>
        public static DisplayChangeHoldReleaseReason LastReleaseReason { get; private set; }

        /// <summary>현재(또는 마지막) 유예가 시작된 <c>Time.frameCount</c>(-1 = 없음).</summary>
        public static int StartedAtFrame { get; private set; } = -1;

        /// <summary>마지막 유예가 해제된 <c>Time.frameCount</c>(-1 = 없음). 재개 직후 첫 발판 로그까지의 경과 판정용.</summary>
        public static int ReleasedAtFrame { get; private set; } = -1;

        internal static void PublishStarted(int episode, DisplayChangeHoldStartReason reason, int frame)
        {
            EpisodeNumber = episode;
            StartReason = reason;
            StartedAtFrame = frame;
            IsActive = true;
        }

        internal static void PublishReleased(int episode, DisplayChangeHoldReleaseReason reason, int frame)
        {
            EpisodeNumber = episode;
            LastReleaseReason = reason;
            ReleasedAtFrame = frame;
            IsActive = false;
        }

        internal static void ResetForTesting()
        {
            IsActive = false;
            EpisodeNumber = 0;
            StartReason = DisplayChangeHoldStartReason.None;
            LastReleaseReason = DisplayChangeHoldReleaseReason.None;
            StartedAtFrame = -1;
            ReleasedAtFrame = -1;
        }
    }
}
