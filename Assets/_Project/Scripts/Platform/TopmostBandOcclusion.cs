namespace StickMate.Platform
{
    /// <summary>
    /// ============================================================================
    /// "topmost 밴드 <b>안</b>에서 우리가 아래로 내려간다" — 2026-09-01 3차 (debugger)
    /// ============================================================================
    /// 앞 라운드가 만든 <see cref="TopmostRestorePolicy"/>는 <b>WS_EX_TOPMOST 비트가 살아 있는가</b>만
    /// 본다. 실기 로그(20260901d)는 그 비트가 시종 <c>True</c>인데도 이렇게 찍혔다:
    /// <code>
    /// [Z-ORDER] 전경 창 전환(항상위는 정상 유지 중) — ... WS_EX_TOPMOST(전=True, 후=True),
    ///   재적용=안 함, ★ 우리가 아래(우리 #19 &gt; 전경 #18) — 캐릭터가 가려집니다 (열거 20개)
    ///   / 전경 창 0x7E1D02 pid 16632 "Explorer.EXE" (3840,2088 3840x72) exStyle=0x00000088(TOPMOST)
    /// </code>
    /// 즉 <b>비트는 정상인데 밴드 안 순위가 뒤집혔다</b>. 앞 라운드의 수정으로는 원리적으로 못 잡는다.
    ///
    /// ============================================================================
    /// 기전(추측 아님 — Win32 z-order 규칙에서 곧바로 따라나온다)
    /// ============================================================================
    ///   (1) <c>WS_EX_TOPMOST</c> 창들은 하나의 <b>밴드</b>를 이루고 그 안에서 다시 순서가 있다.
    ///   (2) 창이 <b>활성화</b>되면(사용자 클릭 / <c>SetForegroundWindow</c>) 자기 밴드의 <b>맨 위</b>로 간다.
    ///   (3) 작업표시줄(<c>Shell_TrayWnd</c>)도 topmost다(위 로그의 <c>exStyle=0x88</c> = TOPMOST|TOOLWINDOW).
    ///       사용자가 작업표시줄/시작 버튼/트레이를 누르는 순간 (2)에 의해 <b>우리 위로 올라간다.</b>
    ///   (4) 우리 창은 <c>WS_EX_TRANSPARENT</c> 클릭 관통이라 <b>영원히 활성화되지 않는다</b>.
    ///       그래서 (2)로 다시 올라갈 경로가 <b>없다</b> — 한 번 밀리면 그 세션 내내 밀린 채로 남는다.
    /// 결론: 이 상태는 <b>드문 경합이 아니라 결정론적 최종 상태</b>다. 작업표시줄을 한 번이라도 클릭하면
    /// 반드시 그렇게 되고, 되돌아오지 않는다.
    ///
    /// ============================================================================
    /// ★ 그런데 그것이 곧 "캐릭터가 사라진다"는 아니다 — macOS 실측이 반증한다
    /// ============================================================================
    /// 이 개발 머신(macOS)에서 <c>CGWindowListCopyWindowInfo</c>로 직접 재 보았다:
    /// <code>
    /// owner=Dock          layer= 20  bounds=(0,0 1512x982)   ← 화면 전체를 덮는 창
    /// owner=Window Server layer= 24  name=Menubar
    /// (우리 오버레이)      layer=  3                          ← process.md:161 / Tasklist:502에서 이미 확정
    /// </code>
    /// 즉 <b>macOS에서는 화면 전체 크기의 Dock 창이 우리보다 17단계 위에 상시 존재</b>하는데도
    /// 캐릭터는 잘 보인다. 이유는 하나뿐이다: <b>z-order상 위에 있는 것과 그 픽셀이 불투명한 것은 다르다.</b>
    /// Dock 창은 Dock 띠 밖에서는 투명하고, 캐릭터는 그 띠 <b>위</b>에 선다.
    ///
    /// Windows 작업표시줄도 정확히 같다: 사각형이 <c>(3840,2088 3840x72)</c>이고 캐릭터는 그 띠의
    /// <b>윗면</b>(<c>y=2088</c>)에 선다. 그러므로 "작업표시줄이 우리 위"라는 사실만으로는
    /// 캐릭터가 가려진다고 결론지을 수 없다 — <b>가려지려면 캐릭터 픽셀이 그 띠 안에 들어가야 한다.</b>
    ///
    /// 그래서 이 파일의 규칙은 두 질문을 <b>분리</b>한다:
    ///   Q1. 우리 위에 있는 topmost 창이 <b>하단 예약 막대(작업표시줄/Dock)</b>인가?
    ///       → 그렇다면 macOS와 동일한 정상 상태다. 경보가 아니라 정보다.
    ///   Q2. 그 <b>밖</b>의 창이 우리를 덮고 있는가?
    ///       → 그것이 진짜 버그다. 그때만 ★를 찍는다.
    /// 이 구분이 없으면 다음 실기 로그도 "작업표시줄이 위에 있다"만 반복하고 원인은 또 안 갈린다.
    ///
    /// <para><b>P/Invoke가 한 줄도 없어야 한다</b> — Windows 실기가 없는 개발 머신의 EditMode에서
    /// 규칙 자체를 검증하기 위해서다(<see cref="TopmostRestorePolicy"/>, FullscreenSuspendPolicy와 같은 설계).</para>
    /// </summary>
    public struct BandRect
    {
        public int Left, Top, Right, Bottom;

        public BandRect(int left, int top, int right, int bottom)
        {
            Left = left; Top = top; Right = right; Bottom = bottom;
        }

        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool IsEmpty => Right <= Left || Bottom <= Top;

        public override string ToString() => $"({Left},{Top} {Width}x{Height})";
    }

    /// <summary>우리 위에 있는 topmost 창의 정체. 이 분류가 "정상"과 "버그"를 가른다.</summary>
    public enum BandOccluderKind
    {
        /// <summary>우리를 덮는 창이 없다.</summary>
        None = 0,

        /// <summary>OS가 예약한 <b>하단 막대</b>(Windows 작업표시줄 / macOS Dock 띠) 안에 완전히 들어간다.
        /// 캐릭터는 그 띠의 <b>윗면</b>에 서므로 이것만으로는 가려지지 않는다 — macOS에서 Dock 창(layer 20)이
        /// 우리(layer 3) 위에 상시 있는 것과 완전히 같은 상태다. <b>경보가 아니다.</b></summary>
        ReservedBottomBar,

        /// <summary>하단 막대가 아닌 창이 우리를 덮고 있다. <b>이것이 진짜 가림이다.</b></summary>
        Other,
    }

    /// <summary>
    /// 밴드 내 가림 판정 — 전부 순수 계산. 입력은 전부 OS 실측 사각형(픽셀)이다.
    /// </summary>
    public static class TopmostBandOcclusionPolicy
    {
        /// <summary>사각형 비교 허용 오차(px). 작업표시줄은 배율/테두리 때문에 rcWork 경계와 1~2px
        /// 어긋나는 것이 정상이다. 너무 크게 잡으면 화면 하단에 걸친 <b>진짜</b> 창까지 막대로 오분류되므로
        /// 픽셀 몇 개 수준으로만 둔다.</summary>
        public const int ToleranceDefaultPx = 2;

        /// <summary>두 사각형이 실제로 겹치는가(맞닿기만 하는 것은 겹침이 아니다).</summary>
        public static bool Overlaps(in BandRect a, in BandRect b)
        {
            if (a.IsEmpty || b.IsEmpty) return false;
            return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
        }

        /// <summary>
        /// 우리를 덮는 창 하나를 분류한다.
        /// </summary>
        /// <param name="occluder">덮는 창의 OS 사각형.</param>
        /// <param name="monitor">우리 창이 놓인 모니터 전체 사각형(<c>rcMonitor</c>).</param>
        /// <param name="workAreaBottom">그 모니터의 작업영역 하단(<c>rcWork.Bottom</c>).
        /// <c>monitor.Bottom</c>과 같으면 하단 예약 막대가 <b>없다</b>(자동 숨김이거나 좌/우/상단 배치).</param>
        public static BandOccluderKind Classify(in BandRect occluder, in BandRect monitor,
            int workAreaBottom, int tolerancePx = ToleranceDefaultPx)
        {
            if (occluder.IsEmpty) return BandOccluderKind.None;

            int barHeight = monitor.Bottom - workAreaBottom;
            if (barHeight <= 0) return BandOccluderKind.Other;   // 예약 막대 자체가 없다.

            // 막대 띠 안에 **완전히** 들어가야 막대로 본다. 화면 하단에 걸쳐 있을 뿐인 창
            // (예: 하단에 도킹한 일반 창, 전체화면 창)은 여기서 걸러져 Other가 된다.
            bool insideBand = occluder.Top >= workAreaBottom - tolerancePx
                              && occluder.Bottom <= monitor.Bottom + tolerancePx;
            if (!insideBand) return BandOccluderKind.Other;

            // 가로로도 모니터 안이어야 한다(다른 모니터의 막대가 아니라 우리 모니터의 막대인지).
            bool insideMonitorX = occluder.Left >= monitor.Left - tolerancePx
                                  && occluder.Right <= monitor.Right + tolerancePx;
            return insideMonitorX ? BandOccluderKind.ReservedBottomBar : BandOccluderKind.Other;
        }

        /// <summary>
        /// ★ <b>미배선(dry-run) 규칙</b> — 지금은 어떤 Win32 쓰기에도 연결되어 있지 않다.
        /// "topmost 밴드 안에서 우리를 다시 위로 올릴 것인가"를 <b>가장 좁고 안전한 조건</b>으로만 허용한다.
        ///
        /// <para>왜 배선하지 않았는가(리더 판단 대기): 밴드 안에서 위로 올라가는 유일한 수단은
        /// <c>SetWindowPos(HWND_TOPMOST)</c>이고, 그러면 <b>작업표시줄 위에 영구히 올라앉는다</b>.
        /// 0.1초마다 다시 걸면 시작 메뉴/알림 센터/트레이 플라이아웃 위로도 100ms 안에 올라간다 —
        /// 절대 불변 원칙 2(비침해) 위반이다. 그래서 아래 네 조건이 <b>전부</b> 참일 때만 허용한다.</para>
        /// </summary>
        /// <param name="desiredTopmost">목표가 항상위인가.</param>
        /// <param name="suspended">전체화면 게임 감지로 숨는 중인가(그때는 절대 올리지 않는다).</param>
        /// <param name="barOccludesUs">하단 예약 막대가 우리 위에 있는가.</param>
        /// <param name="characterInsideBar"><b>캐릭터 픽셀이 실제로 그 띠 안에 그려지고 있는가.</b>
        /// 이 값이 false면 가려지는 것이 없으므로 올릴 이유도 없다(= macOS의 정상 상태와 동일).
        /// <b>이 입력을 줄 수 있는 훅이 아직 없다</b>(캐릭터 화면 사각형 제공자 필요) — 배선 보류의
        /// 또 다른 이유이며, 리더가 승인하면 coder 단계에서 붙일 항목이다.</param>
        /// <param name="otherOccluderCount">막대가 <b>아닌</b> topmost 창이 우리를 덮는 개수.
        /// 시작 메뉴 / 알림 센터 / 트레이 플라이아웃 / 작업 전환기는 전부 여기에 잡힌다.
        /// 하나라도 있으면 <b>올리지 않는다</b> — "시작 메뉴가 열려 있지 않을 때만"을 별도 감지 없이
        /// 이 한 숫자로 보수적으로 만족시킨다.</param>
        public static bool ShouldRaiseWithinBand(bool desiredTopmost, bool suspended,
            bool barOccludesUs, bool characterInsideBar, int otherOccluderCount)
        {
            if (!desiredTopmost) return false;
            if (suspended) return false;             // 원칙 2 — 전체화면 게임 위로 기어 올라가지 않는다.
            if (otherOccluderCount > 0) return false; // 원칙 2 — 셸 팝업/다른 topmost 창을 절대 덮지 않는다.
            if (!barOccludesUs) return false;
            return characterInsideBar;                // 실제로 가려질 때만.
        }
    }

    /// <summary>밴드 내 가림의 <b>전이</b>만 골라낸다. None이면 절대 로그하지 않는다(24시간 상주 앱).</summary>
    public enum BandOcclusionEvent
    {
        /// <summary>변화 없음 — 로그 금지.</summary>
        None = 0,

        /// <summary>가림이 <b>시작</b>됐다. 이 순간의 직전 이벤트가 곧 범인이다
        /// (<see cref="WatchTraceRing"/>이 그 목록을 갖고 있다).</summary>
        Started,

        /// <summary>덮는 창의 <b>정체가 바뀌었다</b>(작업표시줄 → 다른 창, 혹은 그 반대).
        /// 핸들만 바뀐 것은 사건이 아니다 — 그러면 창이 뜨고 지는 동안 로그가 폭주한다.</summary>
        KindChanged,

        /// <summary>가림이 <b>해소</b>됐다. 지속 시간이 함께 보고된다.
        /// <para>★ 그 지속 시간은 <b>에피소드 전체</b>(가림이 처음 시작된 순간부터)이지
        /// 마지막 <see cref="KindChanged"/>부터가 아니다. 알고 싶은 것은 "얼마나 오래 덮여 있었나"이지
        /// "마지막으로 덮개가 바뀐 뒤 얼마나 지났나"가 아니다 — 실행 검증에서 이 두 해석이 갈렸으므로
        /// 명시해 둔다(<c>KindChanged가_지속시간_시계를_되감지_않는다</c> 테스트가 잠근다).</para></summary>
        Cleared,
    }

    /// <summary>
    /// 밴드 내 가림 상태 추적기(순수 계산). <see cref="TopmostWatchdogTracker"/>와 같은 계약이다:
    /// 폴링은 자주 돌지만 이 추적기를 통과하는 사건은 <b>상태가 실제로 바뀔 때</b>뿐이다.
    /// </summary>
    public struct BandOcclusionTracker
    {
        private bool _initialized;
        private BandOccluderKind _kind;
        private double _startedAtSeconds;
        private int _episodeCount;

        /// <summary>가림 에피소드 누적 횟수.</summary>
        public int EpisodeCount => _episodeCount;

        /// <summary>지금 가려지고 있는가(마지막 관측 기준).</summary>
        public bool IsOccluded => _kind != BandOccluderKind.None;

        /// <summary>지금 우리를 덮고 있는 창의 종류.</summary>
        public BandOccluderKind Kind => _kind;

        /// <param name="kind">이번 관측에서 우리를 덮는 <b>가장 위</b> 창의 종류
        /// (막대와 일반 창이 동시에 덮으면 <see cref="BandOccluderKind.Other"/>가 우선한다 —
        /// 그쪽이 진짜 버그이므로 절대 가려지면 안 된다).</param>
        /// <param name="occludedForSeconds">Cleared에서만 의미가 있다.</param>
        public BandOcclusionEvent Observe(BandOccluderKind kind, double nowSeconds,
            out double occludedForSeconds)
        {
            occludedForSeconds = 0.0;

            if (!_initialized)
            {
                _initialized = true;
                _kind = kind;
                _startedAtSeconds = nowSeconds;
                // 기동 첫 관측은 기준선만 잡는다. 이미 가려진 채로 시작했다면 그것도 사건이므로
                // 에피소드로는 세되 로그는 다음 전이에 맡긴다(기동 로그 폭주 방지).
                if (kind != BandOccluderKind.None) _episodeCount++;
                return BandOcclusionEvent.None;
            }

            if (kind == _kind) return BandOcclusionEvent.None;

            BandOccluderKind previous = _kind;
            _kind = kind;

            if (previous == BandOccluderKind.None)
            {
                _episodeCount++;
                _startedAtSeconds = nowSeconds;
                return BandOcclusionEvent.Started;
            }

            if (kind == BandOccluderKind.None)
            {
                occludedForSeconds = nowSeconds - _startedAtSeconds;
                return BandOcclusionEvent.Cleared;
            }

            return BandOcclusionEvent.KindChanged;
        }
    }

    /// <summary>어떤 종류의 관측을 흔적으로 남기는가. <b>"우리가 아래로 내려간 순간의 직전 이벤트"</b>를
    /// 로그에 남기라는 리더 지시(2026-09-01 3차)의 구현 수단이다.</summary>
    public enum WatchTraceKind
    {
        None = 0,
        /// <summary>전경 창이 바뀌었다. 핸들이 함께 기록된다 — 밴드 순위를 뒤집는 <b>1순위 용의자</b>다
        /// (활성화된 창은 자기 밴드의 맨 위로 간다).</summary>
        ForegroundChanged,
        /// <summary>WS_EX_TOPMOST 비트를 잃었다.</summary>
        TopmostLost,
        /// <summary>WS_EX_TOPMOST 비트가 돌아왔다.</summary>
        TopmostRestored,
        /// <summary>우리가 topmost를 재적용했다(= 우리 스스로 밴드 맨 위로 올라간 순간).</summary>
        Reasserted,
        /// <summary>전체화면 게임 숨김이 켜졌다.</summary>
        SuspendedOn,
        /// <summary>전체화면 게임 숨김이 풀렸다.</summary>
        SuspendedOff,
        /// <summary>밴드 스캔이 실행됐다(가림 없음 관측 포함) — "언제까지는 멀쩡했는가"의 기준점.</summary>
        BandScanClear,
    }

    /// <summary>
    /// 마지막 N개의 관측을 담는 고정 크기 링. <b>정상 구간에서 할당이 0</b>이어야 한다(24시간 상주 앱):
    /// 항목은 struct 배열에 덮어쓰고, 문자열은 <see cref="Describe"/>를 부를 때 = <b>로그를 남길 때만</b>
    /// 만든다.
    ///
    /// <para>왜 필요한가: 실기 로그에서 "우리가 아래(#19 &gt; #18)"는 <b>이미 벌어진 뒤</b>의 사진일 뿐,
    /// 무엇이 그렇게 만들었는지는 없었다. 순위가 뒤집힌 <b>순간</b>에 직전 몇 초의 관측을 함께 찍으면
    /// (예: "0.12초 전 전경창이 0x7E1D02로 바뀜") 다음 로그 한 줄로 인과가 확정된다.</para>
    /// </summary>
    public sealed class WatchTraceRing
    {
        /// <summary>보관 개수. 0.1초 폴링 기준 사건 8개면 사람이 창을 한 번 전환하는 동안의 전후 맥락을
        /// 충분히 담는다. 늘리면 로그 한 줄이 길어질 뿐 판정은 나아지지 않는다.</summary>
        public const int Capacity = 8;

        private struct Entry
        {
            public WatchTraceKind Kind;
            public long Handle;
            public double AtSeconds;
        }

        private readonly Entry[] _entries = new Entry[Capacity];
        private int _cursor;
        private int _count;

        /// <summary>지금까지 기록된 항목 수(최대 <see cref="Capacity"/>).</summary>
        public int Count => _count;

        /// <summary>같은 종류·같은 핸들이 연속으로 들어오면 <b>기록하지 않는다</b>. 폴링이 초당 10회라
        /// 그대로 두면 링이 같은 항목으로 가득 차 맥락이 사라진다.</summary>
        public void Record(WatchTraceKind kind, long handle, double nowSeconds)
        {
            if (kind == WatchTraceKind.None) return;

            if (_count > 0)
            {
                int last = (_cursor - 1 + Capacity) % Capacity;
                if (_entries[last].Kind == kind && _entries[last].Handle == handle)
                {
                    _entries[last].AtSeconds = nowSeconds;   // 시각만 갱신 — 중복 항목을 만들지 않는다.
                    return;
                }
            }

            _entries[_cursor] = new Entry { Kind = kind, Handle = handle, AtSeconds = nowSeconds };
            _cursor = (_cursor + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        /// <summary>최신 → 과거 순으로 "N초 전 무엇" 목록을 만든다. <b>로그를 남길 때만</b> 호출할 것.</summary>
        public string Describe(double nowSeconds)
        {
            if (_count == 0) return "(직전 이벤트 없음)";

            var sb = new System.Text.StringBuilder(160);
            for (int i = 0; i < _count; i++)
            {
                int idx = (_cursor - 1 - i + Capacity * 2) % Capacity;
                Entry e = _entries[idx];
                if (i > 0) sb.Append(" <- ");
                sb.Append($"{(nowSeconds - e.AtSeconds) * 1000.0:F0}ms전 {Describe(e.Kind)}");
                if (e.Handle != 0) sb.Append($"(0x{e.Handle:X})");
            }
            return sb.ToString();
        }

        private static string Describe(WatchTraceKind kind)
        {
            switch (kind)
            {
                case WatchTraceKind.ForegroundChanged: return "전경창바뀜";
                case WatchTraceKind.TopmostLost: return "TOPMOST비트잃음";
                case WatchTraceKind.TopmostRestored: return "TOPMOST비트복구";
                case WatchTraceKind.Reasserted: return "우리가재적용";
                case WatchTraceKind.SuspendedOn: return "전체화면숨김ON";
                case WatchTraceKind.SuspendedOff: return "전체화면숨김OFF";
                case WatchTraceKind.BandScanClear: return "밴드스캔:가림없음";
                default: return "?";
            }
        }
    }
}
