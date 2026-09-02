using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Platform
{
    /// <summary>
    /// IPlatformWindowService.EnumerateFootholds()를 감싸는 절제된 폴러.
    /// (Debugger BUG-M3 대응: "매 프레임 호출 금지" 계약을 강제하는 코드가 전혀 없었음 — 이 클래스가
    /// 그 계약을 코드 레벨로 강제한다.)
    ///
    /// 왜 필요한가: IPlatformWindowService.EnumerateFootholds()는 데스크톱 구현체(Win32WindowService)에서
    /// EnumWindows P/Invoke + 창마다 여러 번의 조회(가시성/최소화/스타일/소유 프로세스/제목/DWM)를
    /// 수행하는 상대적으로 무거운 작업이다. 상태머신 Tick()이 이걸 매 프레임 직접 호출하면 24시간
    /// 상주 앱에서 불필요한 OS 호출 부하가 누적된다. 이 클래스는 StickConfig.footholdPollInterval
    /// 주기로만 실제 열거를 수행하고, 그 사이 프레임에는 마지막으로 열거한 결과를 캐시로 재사용한다.
    ///
    /// 컨벤션: States/*.cs는 IPlatformWindowService.EnumerateFootholds()를 절대 직접 호출하지 않고
    /// 반드시 이 클래스의 CachedFootholds(또는 이를 감싼 StickmanBlackboard.SenseGround())만 읽는다.
    ///
    /// 알림 방식: 캐시가 이전 폴링과 달라졌을 때만 StickmanEventBus.FootholdsChanged를 발생시켜
    /// 구독자(향후 렌더링/디버그 오버레이 등)가 "변경 있을 때만" 반응하게 한다. 상태머신 자체는
    /// 매 프레임 CachedFootholds를 읽어 접지 판정을 하되(메모리 읽기일 뿐 OS 호출이 아니므로 저렴하다),
    /// 실제 OS 재열거 빈도만 이 클래스가 통제한다.
    ///
    /// ★ 2026-09-01 — "폴링을 이벤트로 바꾸자"는 라운드가 실제로 열렸다가 <b>실측으로 중단</b>됐다.
    /// 사용자 실기 계측이 <c>[발판열거] 1회 평균 1.72~1.87ms, 초당 4.88~6.22ms = 실행 시간의 0.5%</c>,
    /// 스톨 귀인 <c>판정: 로직밖(렌더/프레젠트/OS 합성)</c>을 내놓아 <b>창 열거는 렉의 원인이
    /// 아님</b>이 확정됐기 때문이다. 그 라운드가 설계·검증까지 마친 규칙만
    /// <c>Platform/FootholdScanPolicy.cs</c>에 <b>배선하지 않은 채</b> 남겨 두었다 — 근거와 함께
    /// 그 파일 문서에 적혀 있으니, 나중에 열거 비용이 실제로 문제가 되면 그것부터 읽으면 된다.
    ///
    /// ★★ 2026-09-02 — <b>세션 가시성 게이트</b>가 이 클래스에 배선됐다(유령 발판 라운드 ②).
    /// 잠금 화면 / UAC 보안 데스크톱 / 화면 꺼짐 동안에는 <c>Tick()</c>이 <c>Poll()</c>을 통째로
    /// 건너뛰고, 해제되는 그 틱에 <c>PollImmediately()</c>를 1회 부른다. <b>중단 중에도 캐시는
    /// 그대로 유지한다</b> — 비우는 순간 그것이 곧 발판 전멸이고, 고치려던 버그를 스스로 만든다.
    /// 판정은 이 파일에 없다(<c>Platform/SessionVisibilityPolicy</c>). 근거는 그 파일 문서와
    /// <c>docs/platform/GHOST_FOOTHOLDS.md</c> 2절.
    ///
    /// 모바일(ScreenshotBackdropPlatformService)에서는 유저가 발판을 추가/삭제할 때 그 서비스가 스스로
    /// StickmanEventBus.RaiseFootholdsChanged()를 즉시 호출해 UX_FLOW.md 3절이 요구하는 "탭 즉시 피드백"을
    /// 보장한다. 이 폴러가 그 위에 추가로 주기 폴링을 얹는 것은 중복이지만 무해하다(캐시가 이미
    /// 최신이면 변경 감지 로직이 이벤트를 재발행하지 않음) — 데스크톱과 동일한 코드 경로를 유지하기
    /// 위한 의도적 트레이드오프(Coder→Debugger 확인 요청 항목, Tasklist.md 참고).
    /// </summary>
    public sealed class FootholdPoller
    {
        private readonly IPlatformWindowService _service;
        private readonly StickConfig _config;

        // 마지막으로 확정된 발판 스냅샷. 서비스 구현체가 내부적으로 재사용하는 List를 그대로 들고
        // 있으면(Null/ScreenshotBackdrop/Win32 구현체 모두 내부 버퍼 재사용 컨벤션을 따름) 다음
        // EnumerateFootholds() 호출 때 같은 참조가 제자리에서 바뀌어 "이전 값과 비교"가 불가능해진다.
        // 그래서 별도의 안정된 캐시 리스트에 값(구조체)을 복사해 보관한다. 폴링 주기(0.2~0.5초)마다
        // 한 번만 복사하므로 매 프레임 할당 금지 규칙과 충돌하지 않는다.
        private readonly List<PlatformFoothold> _cache = new List<PlatformFoothold>(64);

        // BUG-P1-M4 대응(Major, docs/BUG_REPORT_PHASE1.md, Phase 0 Minor m2 재발): IReadOnlyList<T>로
        // 노출해도 List<T>가 그 인터페이스를 구현하므로 호출부가 캐스팅해 Add/Clear/Sort로 변형할 수
        // 있었다. _cache.AsReadOnly()는 같은 List를 그대로 감싸는 "살아있는" 뷰이므로(내부 리스트가
        // 바뀌면 이 래퍼를 통한 조회에도 즉시 반영됨) 생성자에서 1회만 감싸두면 된다 — Poll()이 캐시
        // 내용을 갱신할 때마다 매번 새로 감쌀 필요가 없다(그러면 매 프레임 호출되는 프로퍼티에서
        // 할당이 생겨 24시간 상주 앱 GC 압박 방지 컨벤션과 충돌한다).
        private readonly ReadOnlyCollection<PlatformFoothold> _readOnlyCache;

        private float _timer;

        // ★ 2026-08-29 창 도둑 복구 라운드 — "가려짐 필터 이전 원본 창" 캐시(Platform/IRawWindowRectSource.cs).
        // 발판 캐시와 **같은 폴링 주기**로만 갱신해, 소비자(Interaction/WindowTheftDirector)가 프레임마다
        // 서비스를 직접 두드리지 않게 한다(이 클래스가 존재하는 이유 자체가 그 계약의 강제다).
        // 서비스 구현체의 내부 버퍼를 그대로 들고 있으면 다음 열거 때 제자리에서 바뀌므로 값을 복사한다
        // (_cache와 같은 이유). 지원하지 않는 플랫폼에서는 이 목록이 영원히 비어 있고, 소비자는 그것을
        // 폴백 신호로 쓴다.
        private readonly IRawWindowRectSource _rawSource;
        private readonly List<PlatformFoothold> _rawCache = new List<PlatformFoothold>(64);
        private readonly ReadOnlyCollection<PlatformFoothold> _readOnlyRawCache;

        public IReadOnlyList<PlatformFoothold> CachedFootholds => _readOnlyCache;

        /// <summary>
        /// 가려짐(오클루전) 필터를 거치기 <b>전</b>의 원본 창 목록. 창 도둑처럼 "딛는 것이 아니라 미는"
        /// 연출만 이 목록을 쓴다 — 접지/걷기 계열은 절대 이걸 쓰면 안 된다(보이지 않는 창을 딛고 허공을
        /// 걷는 2026-08-28 사용자 신고 버그가 그대로 재발한다). 플랫폼이 지원하지 않으면 빈 목록.
        /// </summary>
        public IReadOnlyList<PlatformFoothold> CachedRawWindows => _readOnlyRawCache;

        // ★ 2026-09-01 스파이크 라운드 — 열거 비용 실측용. 비용은 폴링당(0.3초) 타임스탬프 2회다.
        // ★★ 2026-09-02 정정 — 예전 주석은 "창 열거는 이 클래스가 유일한 호출자라 여기 스톱워치
        //    하나면 앱 전체가 빠짐없이 잡힌다"고 적혀 있었다. **거짓이었다.** 전체화면 판정 경로가
        //    네이티브 창 목록을 따로 조회한다(아래 Poll() 주석 참고). 그쪽은 데코레이터
        //    FallbackPlatformWindowService가 StallAttribution.RecordFullscreenProbe로 따로 잰다.
        private readonly IWindowEnumerationCostSource _costSource;

        public FootholdPoller(IPlatformWindowService service, StickConfig config)
        {
            _service = service;
            _config = config;
            _timer = 0f;
            _readOnlyCache = _cache.AsReadOnly();
            _rawSource = service as IRawWindowRectSource;
            _costSource = service as IWindowEnumerationCostSource;
            _readOnlyRawCache = _rawCache.AsReadOnly();

            // 설정이 배선되는 유일한 지점이 여기다(이 클래스가 이미 StickConfig를 들고 있고,
            // 아래 Poll() 직전에 ScreenCoordinateConverter 스위치를 미는 것과 같은 이유).
            StallAttribution.Configure(config);
            PlayerLogPolicy.Configure(config);

            Poll(); // 첫 프레임부터 발판 정보가 있어야 "빈 화면에 멈춰 보임"을 피할 수 있다 (UX_FLOW.md 6-1절).
        }

        // ============================================================================
        // ★ 세션 가시성 게이트 (2026-09-02) — 아무도 못 보는 동안에는 열거하지 않는다
        // ============================================================================
        // 판정은 이 파일에 없다. Platform/SessionVisibilityPolicy가 전부 가지고 있고 여기는 그
        // 결과에 따라 Poll()을 건너뛰거나 재개하는 배선뿐이다(정책은 플랫폼 중립 위치에, 플랫폼
        // 코드는 사실 조회만 — FullscreenSuspendPolicy 사고의 재발 방지 규칙).

        /// <summary>지금 세션 가시성 게이트로 스캔을 멈춘 상태인가(진단·테스트 창구).</summary>
        internal bool IsScanSuspended => _scanSuspended;

        /// <summary>게이트가 <b>참으로 전이한</b> 누적 횟수(진단·테스트 창구).</summary>
        internal int ScanSuspendCount => _scanSuspendCount;

        /// <summary>게이트가 <b>풀린</b> 누적 횟수 = 즉시 재열거가 일어난 횟수(진단·테스트 창구).</summary>
        internal int ScanResumeCount => _scanResumeCount;

        /// <summary>
        /// 관측값을 어디서 읽을지 갈아 끼우는 <b>테스트 전용</b> 창구. null이면 제품 경로
        /// (<see cref="FramePacing.LastPresence"/>)를 그대로 쓴다.
        ///
        /// <para>왜 필요한가: 제품 경로의 <c>_presence</c>는 <c>FramePacing._applied</c>가 true일 때만
        /// 갱신되고 그 값은 <b>에디터/테스트에서 언제나 false</b>다. 그 상태로는 이 게이트가 한 번도
        /// 참이 되지 않아 <b>"잠기면 실제로 서는가"를 아무도 못 재게 된다</b> — 이 저장소가 싫어하는
        /// "아무것도 안 재고 초록"이다. <b>쓰는 테스트는 반드시 TearDown에서 null로 되돌린다</b>
        /// (정적이라 다음 테스트로 샌다).</para>
        /// </summary>
        internal static IViewerPresenceService PresenceProbeOverride;

        /// <summary>전이 로그의 상한. 신호가 병적으로 깜빡여도 Player.log를 채우지 않게 한다
        /// (24시간 상주 앱: 로그 감량 라운드의 규칙). 넘어가면 조용히 동작만 계속한다.</summary>
        private const int MaxTransitionLogs = 20;

        private bool _scanSuspended;
        private int _scanSuspendCount;
        private int _scanResumeCount;
        private int _transitionLogs;

        /// <summary>매 프레임 호출해도 안전 — 내부적으로 StickConfig.footholdPollInterval 주기를 스스로 지킨다.</summary>
        public void Tick(float deltaTime)
        {
            // ---- (5) 세션 가시성 게이트: 참이면 Poll()을 통째로 건너뛴다 ----------------------
            ViewerPresenceSnapshot presence = ResolvePresence();
            if (SessionVisibilityPolicy.ShouldSuspendFootholdScan(presence))
            {
                if (!_scanSuspended)
                {
                    _scanSuspended = true;
                    _scanSuspendCount++;
                    LogTransition("중단", SessionVisibilityPolicy.DescribeSuspendReason(presence));
                }

                // ★★ 캐시를 절대 비우지 않는다. 비우는 순간 그것이 곧 **발판 전멸**이고, 이 게이트가
                //    고치려던 버그를 스스로 만드는 셈이 된다(잠금 해제 순간 캐릭터가 화면 밖으로
                //    떨어진다). 우리가 멈추는 것은 "다시 묻는 일"뿐이고, 마지막으로 확정된 발판
                //    목록은 그대로 유효하게 남는다.
                //    타이머도 건드리지 않는다 — 여기서 return하므로 누적되지 않고 그대로 얼어붙는다.
                return;
            }

            // ---- (6) 참 -> 거짓 전이: 다음 주기를 기다리지 않고 즉시 한 번 재열거한다 ----------
            // 이것이 없으면 잠금 해제 직후 최대 footholdPollInterval(0.3초) 동안 **잠금 이전의 낡은
            // 집합**으로 접지 판정을 한다. 사용자가 보는 첫 화면이 정확히 그 순간이라, 여기서 늦으면
            // "돌아왔더니 캐릭터가 엉뚱한 데 서 있다"로 보인다.
            if (_scanSuspended)
            {
                _scanSuspended = false;
                _scanResumeCount++;
                LogTransition("재개", "관측 해제");
                PollImmediately();
                return;
            }

            _timer += deltaTime;
            float interval = _config != null ? Mathf.Max(0.05f, _config.footholdPollInterval) : 0.5f;
            if (_timer < interval) return;
            _timer = 0f;
            Poll();
        }

        /// <summary>
        /// 관측값 한 개를 읽어 온다. 제품 경로는 <see cref="FramePacing"/>이 이미 0.2~0.5초 주기로
        /// 갱신해 둔 값을 <b>그대로 재사용</b>한다 — 여기서 OS를 다시 두드리면 유휴 비용을 줄이려는
        /// 이 게이트가 스스로 비용을 만든다.
        ///
        /// <para><b>정직하게 남기는 한계</b>: <c>FramePacing</c>의 관측은 적응형 페이싱이 꺼져 있으면
        /// (<c>STICKMATE_ADAPTIVE_PACING=0</c> 또는 에디터/테스트) 갱신되지 않고 <c>Valid=false</c>로
        /// 남는다. 그러면 이 게이트는 <b>영원히 거짓</b>이 되어 지금까지의 동작(계속 스캔)이 그대로
        /// 유지된다. 즉 <b>기능이 조용히 꺼지는 방향은 안전한 쪽</b>이다(보수 규칙과 같은 방향).
        /// 반대로 잘못 멈추는 경로는 없다.</para>
        /// </summary>
        private static ViewerPresenceSnapshot ResolvePresence()
        {
            IViewerPresenceService probe = PresenceProbeOverride;
            if (probe == null) return FramePacing.LastPresence;
            return probe.TryGetPresence(out ViewerPresenceSnapshot snapshot) ? snapshot : default;
        }

        private void LogTransition(string what, string reason)
        {
            if (_transitionLogs >= MaxTransitionLogs) return;
            _transitionLogs++;
            // ★ UnityEngine을 명시한다 — 이 파일은 System.Diagnostics(Stopwatch)도 using하고 있어
            //   맨 이름 Debug는 CS0104(모호한 참조)가 된다.
            UnityEngine.Debug.Log($"[세션가시성] 발판 스캔 {what} — 사유={reason}, 캐시 {_cache.Count}개 유지."
                + (_transitionLogs == MaxTransitionLogs
                    ? " (이 줄이 마지막입니다 — 전이 로그 상한 도달)"
                    : string.Empty));
        }

        /// <summary>다음 Tick을 기다리지 않고 즉시 재열거하고 싶을 때(예: 모바일 온보딩 완료 직후) 사용.</summary>
        public void PollImmediately()
        {
            _timer = 0f;
            Poll();
        }

        private void Poll()
        {
            // ★ 2026-09-01 — 오버레이 원점 위생 검사 스위치를 좌표 변환기에 밀어 넣는 유일한 지점.
            // 플랫폼 서비스가 EnumerateFootholds() 안에서 오버레이 사각형을 보고하므로(Mac/Win 양쪽
            // CaptureOverlayOrigin), 그 호출 **직전**인 여기가 설정을 반영할 정확한 자리다.
            // ScreenCoordinateConverter가 StickConfig를 직접 알지 않게 하려는 것이 목적이다
            // (그 클래스는 순수 static 유틸이며 설정 의존이 없다 — 그쪽 문서 참고).
            ScreenCoordinateConverter.OverlayOriginSanityCheckEnabled =
                _config == null || _config.overlayOriginSanityCheckEnabled;

            // ★★ 2026-09-02 — **여기는 창 목록 조회의 "유일한" 지점이 아니다.** 그렇게 적혀 있던
            //    예전 주석이 조사를 통째로 한 라운드 잡아먹었다. 실제 경로는 최소 셋이다:
            //      (1) 여기(발판 폴링, footholdPollInterval 0.3초 = 초당 3.33회)
            //      (2) IsFullscreenAppActive()(fullscreenPollInterval 1.5초 = 초당 0.67회)
            //          -> FallbackPlatformWindowService가 RecordFullscreenProbe로 잰다.
            //      (3) 오버레이 재적합 직후 단발(MacWindowService.ReportOverlayRectNow) — 상주 아님.
            //    (2)가 계측 밖에 있던 동안 **초당 4회 중 17%가 원장에 나타나지 않았다.**
            //
            // 그리고 아래 스톱워치가 재는 구간은 **OS 창 열거만이 아니다**: 데코레이터의
            // EnumerateFootholds()에는 Dock 실측(TryGetDockFoothold)과 바닥 안전망 합성
            // (AppendBottomSafetyNet)도 들어 있다. OS 왕복만의 비용은 플랫폼 구현체 안쪽의 중첩
            // 타이머(StallAttribution.RecordNativeWindowListQuery)가 따로 보고한다 — 두 값의 차이가
            // 곧 "우리 후처리"다.
            long enumStart = Stopwatch.GetTimestamp();
            IReadOnlyList<PlatformFoothold> latest = _service.EnumerateFootholds();
            long enumTicks = Stopwatch.GetTimestamp() - enumStart;
            StallAttribution.RecordWindowEnumeration(enumTicks,
                _costSource != null ? _costSource.LastEnumeratedWindowCount : -1,
                _costSource != null ? _costSource.LastDwmProbeCount : -1,
                latest != null ? latest.Count : 0);

            // 원본 창 캐시는 발판 변경 여부와 무관하게 매 폴링 갱신한다 — 아래 HasChanged 조기 반환보다
            // **먼저** 해야 한다. 발판 목록이 그대로여도(예: 맨 앞 큰 창 하나만 계속 보이는 상황) 그 뒤에
            // 가려진 창들은 열리고 닫히기 때문이다.
            RefreshRawCache();

            if (!HasChanged(latest)) return;

            _cache.Clear();
            for (int i = 0; i < latest.Count; i++)
            {
                _cache.Add(latest[i]);
            }
            StickmanEventBus.RaiseFootholdsChanged();
        }

        private void RefreshRawCache()
        {
            if (_rawSource == null) return;
            IReadOnlyList<PlatformFoothold> raw = _rawSource.RawWindows;
            _rawCache.Clear();
            if (raw == null) return;
            for (int i = 0; i < raw.Count; i++)
            {
                _rawCache.Add(raw[i]);
            }
        }

        private bool HasChanged(IReadOnlyList<PlatformFoothold> latest)
        {
            if (latest.Count != _cache.Count) return true;
            for (int i = 0; i < latest.Count; i++)
            {
                PlatformFoothold a = _cache[i];
                PlatformFoothold b = latest[i];
                if (a.Handle != b.Handle || a.IsTopmost != b.IsTopmost || a.ScreenRect != b.ScreenRect)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
