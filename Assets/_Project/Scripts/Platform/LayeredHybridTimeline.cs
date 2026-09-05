namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-05 진단 전용 — Windows 오버레이 창의 <c>WS_EX_LAYERED</c>가 <b>언제 붙고 언제 벗겨지는지</b>를
    /// 프레임 번호와 벽시계 초로 남기는 타임라인(사용자 신고 "던진 뒤 일어선 캐릭터가 멈추고 잡아도 반응 없음"의
    /// 1순위 후보 A4 — <c>docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md</c> §3-A4 — 를 실기 로그로 가르기 위한 계측).
    ///
    /// <para><b>왜 플랫폼 중립 파일인가</b>: 이 프로젝트의 규칙("정책/판정은 <c>Platform/</c>에, 플랫폼 코드는 사실 조회·실행만").
    /// 사건을 엣지로 걸러내는 규칙·상한·문자열은 여기 있고 OS 호출도 UnityEngine 시간 조회도 없다 — 그래서 Windows 실기가 없는
    /// 이 개발 머신의 EditMode 테스트가 전 분기를 실행한다. 관측값(라이브러리 캐시, OS 스타일 비트, 제거/복구)은
    /// <c>Platform/Windows/WindowsLayeredHybridResolver.cs</c>가 넣어 준다.</para>
    ///
    /// <para><b>무엇을 남기는가</b> — 네 종류의 사건, 전부 <b>엣지에서만</b>(같은 값이 이어지면 아무것도 안 남긴다):
    /// <list type="bullet">
    ///   <item>라이브러리 관통 ON/OFF — <c>UniWindowController.isClickThrough</c> <b>캐시</b>가 바뀐 프레임. ON은 네이티브가
    ///     <c>WS_EX_TRANSPARENT|WS_EX_LAYERED</c>를 켜는 순간이다(<see cref="LayeredHybridPolicy"/> 문서의 C++ 인용). 매 프레임 본다 —
    ///     0.25초 표본으로는 "언제 붙었는가"를 못 본다.</item>
    ///   <item>OS 실측 LAYERED 켜짐/꺼짐 — 해소기의 0.25초 표본에서 비트가 바뀐 시점.</item>
    ///   <item>제거 — 해소기가 실제로 비트를 뗀 시점(누적 횟수 포함).</item>
    ///   <item>복구 — 검증 실패로 되돌린 시점(사유 포함).</item>
    /// </list></para>
    ///
    /// <para><b>상한</b>: 제거는 프로세스당 <see cref="LayeredHybridPolicy.DefaultMaxStrips"/>회가 상한이므로 사건 수도 그
    /// 자릿수다. 그래도 24시간 상주 앱이라 <see cref="MaxLogLines"/>줄에서 멈추고 한 줄로 알린 뒤 <b>세기만</b> 한다.</para>
    /// </summary>
    public sealed class LayeredHybridTimeline
    {
        /// <summary>해소기와 같은 태그를 쓴다 — 사용자가 <c>[레이어드해소]</c> 한 단어로 전부 찾을 수 있어야 한다.</summary>
        public const string LogPrefix = "[레이어드해소]";

        /// <summary>남길 최대 줄 수. 제거 상한의 2배(제거마다 재부착 관측이 한 번씩 선행한다).</summary>
        public const int MaxLogLines = LayeredHybridPolicy.DefaultMaxStrips * 2;

        private bool _hasLibrarySample;
        private bool _lastLibraryClickThrough;
        private bool _hasOsSample;
        private bool _lastOsLayered;
        private int _eventCount;
        private int _lineCount;
        private int _suppressedCount;
        private float _lastEventSeconds = float.NaN;
        private bool _capNoticeEmitted;

        /// <summary>엣지로 확정된 사건의 누적 수(상한 뒤에도 계속 센다).</summary>
        public int EventCount => _eventCount;

        /// <summary>실제로 돌려준 로그 줄 수(상한 알림 줄은 세지 않는다).</summary>
        public int LineCount => _lineCount;

        /// <summary>상한 때문에 남기지 못한 사건 수.</summary>
        public int SuppressedCount => _suppressedCount;

        /// <summary>라이브러리 <c>isClickThrough</c> 캐시 관측. 값이 바뀐 프레임에만 줄을 돌려준다(첫 관측은 "초기 관측"으로 1줄).</summary>
        /// <returns>남길 로그 한 줄. 남길 것이 없으면 null.</returns>
        public string ObserveLibraryClickThrough(bool isClickThrough, int frame, float nowSeconds)
        {
            if (_hasLibrarySample && isClickThrough == _lastLibraryClickThrough) return null;
            bool first = !_hasLibrarySample;
            _hasLibrarySample = true;
            _lastLibraryClickThrough = isClickThrough;
            return Emit(isClickThrough
                    ? LayeredHybridTimelineEvent.LibraryClickThroughOn
                    : LayeredHybridTimelineEvent.LibraryClickThroughOff,
                first ? "초기 관측" : null, frame, nowSeconds);
        }

        /// <summary>OS 실측 <c>WS_EX_LAYERED</c> 비트 관측(0.25초 표본). 값이 바뀐 표본에만 줄을 돌려준다.</summary>
        public string ObserveOsLayered(bool hasLayered, int frame, float nowSeconds)
        {
            if (_hasOsSample && hasLayered == _lastOsLayered) return null;
            bool first = !_hasOsSample;
            _hasOsSample = true;
            _lastOsLayered = hasLayered;
            return Emit(hasLayered ? LayeredHybridTimelineEvent.OsLayeredOn : LayeredHybridTimelineEvent.OsLayeredOff,
                first ? "초기 관측" : null, frame, nowSeconds);
        }

        /// <summary>해소기가 비트를 실제로 뗀 직후.</summary>
        public string NoteStripped(int stripCount, int frame, float nowSeconds)
            => Emit(LayeredHybridTimelineEvent.Stripped, $"누적 제거 {stripCount}회", frame, nowSeconds);

        /// <summary>검증 실패로 되돌린 직후.</summary>
        public string NoteRestored(string why, int frame, float nowSeconds)
            => Emit(LayeredHybridTimelineEvent.Restored, why, frame, nowSeconds);

        private string Emit(LayeredHybridTimelineEvent ev, string note, int frame, float nowSeconds)
        {
            _eventCount++;
            float delta = float.IsNaN(_lastEventSeconds) ? float.NaN : nowSeconds - _lastEventSeconds;
            _lastEventSeconds = nowSeconds;

            if (_lineCount >= MaxLogLines)
            {
                _suppressedCount++;
                if (_capNoticeEmitted) return null;
                _capNoticeEmitted = true;
                return $"{LogPrefix} 타임라인 상한 {MaxLogLines}줄 도달 — 이후 사건은 세기만 하고 남기지 않습니다" +
                       $"(사건 #{_eventCount}, 프레임#{frame}, t={nowSeconds:F3}초).";
            }

            _lineCount++;
            string deltaText = float.IsNaN(delta) ? "첫 사건" : $"직전 사건 +{delta:F3}초";
            string noteText = string.IsNullOrEmpty(note) ? string.Empty : ", " + note;
            return $"{LogPrefix} 타임라인 #{_eventCount} {Describe(ev)} — 프레임#{frame} t={nowSeconds:F3}초 ({deltaText}{noteText}).";
        }

        /// <summary>Player.log에 그대로 찍히는 한국어 사건 이름.</summary>
        public static string Describe(LayeredHybridTimelineEvent ev) => ev switch
        {
            LayeredHybridTimelineEvent.LibraryClickThroughOn =>
                "라이브러리 관통 ON(isClickThrough 캐시 false→true = 네이티브가 WS_EX_TRANSPARENT|WS_EX_LAYERED를 켬)",
            LayeredHybridTimelineEvent.LibraryClickThroughOff =>
                "라이브러리 관통 OFF(true→false = WS_EX_TRANSPARENT 끔, LAYERED는 남김)",
            LayeredHybridTimelineEvent.OsLayeredOn => "OS 실측 WS_EX_LAYERED 켜짐(0.25초 표본)",
            LayeredHybridTimelineEvent.OsLayeredOff => "OS 실측 WS_EX_LAYERED 꺼짐(0.25초 표본)",
            LayeredHybridTimelineEvent.Stripped => "WS_EX_LAYERED 제거",
            LayeredHybridTimelineEvent.Restored => "WS_EX_LAYERED 복구(되돌림)",
            _ => ev.ToString(),
        };
    }

    /// <summary><see cref="LayeredHybridTimeline"/>이 남기는 사건 종류.</summary>
    public enum LayeredHybridTimelineEvent
    {
        LibraryClickThroughOn = 0,
        LibraryClickThroughOff = 1,
        OsLayeredOn = 2,
        OsLayeredOff = 3,
        Stripped = 4,
        Restored = 5,
    }
}
