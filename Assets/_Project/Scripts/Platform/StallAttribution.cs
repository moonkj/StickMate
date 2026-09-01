using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StickMate.Platform
{
    /// <summary>
    /// **스톨 귀인(歸因) 계측** — "긴 프레임이 났다"까지만 말하는 <c>[프레임스파이크]</c> 로그를
    /// "그 시간이 <b>어디서</b> 갔는가"까지 말하게 만드는 장치(2026-09-01 스파이크 라운드).
    ///
    /// ============================================================================
    /// 왜 필요한가 — 진단이 "둘 중 하나"에서 멈춰 있었다
    /// ============================================================================
    /// 사용자 실기 로그(릴리즈 20260901c)는 이렇게 끝난다:
    /// <code>
    /// [프레임스파이크] 누적 55회 — 109/116/140/171/276/315/332ms
    ///   백버퍼: 3831x2160(변화 없음). GC: gen0 +0 / gen1 +0.
    /// </code>
    /// 백버퍼 불변 + GC 증가 0이 반복 확인됐으므로 <c>Screen.SetResolution</c> 재생성과 관리 힙 GC는
    /// 배제됐다. 그런데 <c>FramePacing</c>의 스파이크 로그가 남기는 다음 문장은 <b>추론이지 계측이
    /// 아니다</b>:
    /// <code>"GC 증가분이 0이고 백버퍼도 그대로면 네이티브 창 열거/파일 IO 쪽이다."</code>
    /// 이건 **거짓 이분법**이다. 관리 힙 GC를 일으키지 않고 백버퍼도 바꾸지 않으면서 프레임을 수백 ms
    /// 멈추게 하는 원인은 최소 네 부류다:
    ///   (A) 네이티브 창 열거(EnumWindows + 창마다 DWM 조회)
    ///   (B) 파일 IO(스택트레이스가 켜진 Debug.Log -> Player.log 동기 쓰기)
    ///   (C) 우리 Update 안의 다른 관리 코드(가려짐 솔버 O(n^2), 렌더러 갱신 등)
    ///   (D) **우리 Update 밖** — 렌더/프레젠트/OS 합성기(DWM) 대기. 전체화면 투명 오버레이라
    ///       프레젠트가 컴포지터에 직렬화되면 우리 프로세스 CPU에는 아무것도 잡히지 않는다.
    ///
    /// 이 클래스는 그 넷을 **한 줄로 가른다.** 프레임을 두 구간으로 쪼개고(로직 구간 = 모든 Update +
    /// LateUpdate / 나머지 = 렌더+프레젠트+vsync 대기), 로직 구간 안에서 다시 (A)와 (B)를 실측한다.
    ///
    /// ============================================================================
    /// 계측이 증상을 만들지 않게 하는 설계 — 이 저장소는 오늘 그 사고를 한 번 겪었다
    /// ============================================================================
    /// 직전 z-order 라운드에서 "진단 장치가 초당 10회 전체 창을 열거해 증상을 키운" 사고가 있었다.
    /// 그래서 이 클래스의 상시 비용은 다음이 전부다:
    ///   · 프레임당 <c>Stopwatch.GetTimestamp()</c> 2회(= Windows QPC 2회, 각 ~25ns)
    ///   · 폴링당(0.3초) 2회, 로그 한 줄당 2회
    ///   · float/int 대입 몇 개. <b>할당 0</b>, OS 창 조회 0, <c>Screen</c>/<c>Application</c> 조회는
    ///     스파이크 후보(100ms 초과)일 때만.
    /// 로그는 (1) 스파이크가 났을 때 쿨다운 5초, (2) 60초 요약 한 줄뿐이다.
    ///
    /// ============================================================================
    /// 로그 읽는 법
    /// ============================================================================
    /// <code>
    /// [스톨귀인] 프레임#7160 직전 128.4ms(기대 16.7ms) — 로직 3.1ms(24%) : 창열거 0.4ms/0회,
    ///   로그 0.2ms/2줄, 기타로직 2.5ms | 로직밖 125.3ms(76%). 판정: **로직밖(렌더/프레젠트/합성)**
    /// </code>
    /// · "판정: 창열거"  -> 후보 A 확정. <c>footholdPollInterval</c>과 Win32 열거 경로를 본다.
    /// · "판정: 로그쓰기" -> 후보 B 확정. 스택트레이스/로그 양을 줄인다.
    /// · "판정: 기타로직" -> 우리 Update 안의 다른 코드. 60초 요약의 후보창 수와 함께 본다.
    /// · "판정: 로직밖"  -> **A도 B도 아니다.** 렌더/프레젠트/DWM 합성 대기다. 이 앱의 CPU%에는
    ///                     잡히지 않는 종류의 비용이므로, 다음 라운드는 렌더 쪽을 봐야 한다.
    /// </summary>
    public static class StallAttribution
    {
        // ------------------------------------------------------------------------------------
        // 판정 상수 — FramePacing의 [프레임스파이크]와 **같은 문턱**을 쓴다. 그래야 두 로그가
        // 같은 프레임#으로 1:1 짝을 이뤄 사용자가 눈으로 대조할 수 있다(FramePacing.cs는 이번 라운드
        // 소유가 아니라 그쪽에 필드를 넣을 수 없다 — 값 동기화로 대신한다).
        // ------------------------------------------------------------------------------------
        public const float SpikeAbsoluteMs = 100f;
        public const float SpikeRelativeFactor = 2.5f;
        private const float SpikeLogCooldownSeconds = 5f;
        private const float SummaryIntervalSeconds = 60f;

        /// <summary>
        /// <c>[발판열거]</c> 기준선 줄의 주기(초). 요약보다 촘촘한 이유: 이 줄은 <b>폴링 -> 이벤트
        /// 구조 변경 라운드의 성공 판정 기준</b>이라(리더 지시) 짧은 실행에서도 표본이 여러 개
        /// 나와야 before/after를 비교할 수 있다. 30초에 한 줄은 로그 부담이 사실상 0이다
        /// (실측 기준 이 앱의 총 로그량은 초당 0.6줄이다).
        /// </summary>
        private const float EnumSummaryIntervalSeconds = 30f;

        /// <summary>기여자 하나가 이 비율 이상을 설명해야 그 이름으로 판정한다. 아니면 "혼합".</summary>
        public const float DominantShare = 0.5f;

        /// <summary>로직 구간이 프레임의 이 비율 미만이면 원인은 우리 Update 밖이다.</summary>
        public const float LogicShareThreshold = 0.5f;

        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        // ------------------------------------------------------------------------------------
        // 프레임 버킷 — 스파이크의 dt는 **직전 프레임**의 길이이므로 두 칸이 필요하다.
        // (프레임 N의 Update 시작 시점에 읽는 unscaledDeltaTime = 프레임 N-1이 실제로 걸린 시간)
        // 구조체 배열이라 할당이 없다.
        // ------------------------------------------------------------------------------------
        private struct FrameBucket
        {
            public double EnumMs;
            public int EnumCount;
            public int EnumeratedWindowCount;
            public int DwmProbeCount;
            public int FootholdCount;
            public double LogMs;
            public int LogCount;
            public double LogicMs;
        }

        private static readonly FrameBucket[] Buckets = new FrameBucket[2];
        private static int _current;
        private static long _logicStartTicks;

        // [발판열거] 30초 기준선용 누적치(요약과 별도 창)
        private static double _baseTotalMs, _baseMaxMs;
        private static int _baseCount, _baseMaxEnumerated, _baseMaxDwm;

        // 60초 요약용 누적치(할당 0)
        private static double _winEnumTotalMs, _winEnumMaxMs;
        private static int _winEnumCount;
        private static double _logTotalMs, _logMaxMs;
        private static int _logCount;
        private static double _logicMaxMs;
        private static int _spikeCount, _spikeSinceSummary;
        private static int _lastEnumeratedCount, _maxEnumeratedCount;
        private static int _lastDwmProbeCount, _maxDwmProbeCount, _lastFootholdCount;

        private static float _spikeCooldownLeft;
        private static float _summaryTimer;
        private static float _enumSummaryTimer;
        private static bool _enabled = true;

        // ------------------------------------------------------------------------------------
        // 태그 히스토그램 — "어떤 로그가 제일 많이 찍히는가"를 앱이 스스로 센다.
        // 최초 1회만 Substring 한 번(태그 종류 수만큼, 프로세스 전체에서 최대 MaxTags개) 할당하고
        // 그 뒤로는 int 증가뿐이다. 카운트만 60초마다 리셋하고 이름 배열은 유지한다.
        // ------------------------------------------------------------------------------------
        private const int MaxTags = 24;
        private const int MaxTagLength = 40;
        private static readonly string[] TagNames = new string[MaxTags];
        private static readonly int[] TagHashes = new int[MaxTags];
        private static readonly int[] TagCounts = new int[MaxTags];
        private static int _tagKinds;
        private static int _untaggedCount;

        /// <summary>스톨 귀인 로그를 낼지 여부. StickConfig가 배선되기 전에도 계측은 돌아간다.</summary>
        public static bool Enabled => _enabled;

        /// <summary>지금까지 관측한 스파이크 총 횟수(테스트/요약용).</summary>
        public static int SpikeCount => _spikeCount;

        public static void Configure(Core.StickConfig config)
        {
            if (config == null) return;
            _enabled = config.logStallAttribution;
        }

        /// <summary>EditMode 테스트가 상태를 초기화할 때 쓴다(정적 상태가 테스트 간에 새지 않게).</summary>
        public static void ResetForTests()
        {
            Buckets[0] = default;
            Buckets[1] = default;
            _current = 0;
            _logicStartTicks = 0;
            _winEnumTotalMs = _winEnumMaxMs = 0;
            _winEnumCount = 0;
            _baseTotalMs = _baseMaxMs = 0;
            _baseCount = _baseMaxEnumerated = _baseMaxDwm = 0;
            _logTotalMs = _logMaxMs = 0;
            _logCount = 0;
            _logicMaxMs = 0;
            _spikeCount = _spikeSinceSummary = 0;
            _lastEnumeratedCount = _maxEnumeratedCount = 0;
            _lastDwmProbeCount = _maxDwmProbeCount = _lastFootholdCount = 0;
            _spikeCooldownLeft = 0f;
            _summaryTimer = 0f;
            _enumSummaryTimer = 0f;
            _enabled = true;
            _tagKinds = 0;
            _untaggedCount = 0;
            for (int i = 0; i < MaxTags; i++)
            {
                TagNames[i] = null;
                TagHashes[i] = 0;
                TagCounts[i] = 0;
            }
        }

        // ====================================================================================
        // 계측 입력 (1) — 네이티브 창 열거. Platform/FootholdPoller.cs가 부른다.
        // ====================================================================================

        /// <summary>
        /// 한 번의 <c>IPlatformWindowService.EnumerateFootholds()</c>가 걸린 시간을 기록한다.
        ///
        /// <para>세 개수를 함께 받는 이유는 리더가 지정한 세 질문에 각각 답하기 위해서다:</para>
        /// <list type="number">
        /// <item><paramref name="enumeratedWindowCount"/> — <c>EnumWindows</c> 콜백이 <b>실제로 몇 번</b>
        ///   불렸는가. z-order 라운드 실측에서 이 사용자 환경은 16 -> 54 -> 57 -> 60 -> <b>818</b>까지
        ///   갔다. 818 x 3.3회/초면 초당 2,700회의 관리↔네이티브 전환이다. 지원하지 않는 플랫폼은 -1.</item>
        /// <item><paramref name="dwmProbeCount"/> — 값싼 필터(<c>IsWindowVisible</c>/제목/최소화 등)를
        ///   뚫고 <b>크로스 프로세스 DWM 호출</b>(<c>DwmGetWindowAttribute</c>)까지 간 횟수. 실기
        ///   로그상 발판은 6~10개뿐이었으므로 이 값이 작으면 <b>DWM 호출은 범인이 아니다</b>가
        ///   실측으로 확정된다(리더가 "확정하지 못했다"고 한 바로 그 지점이다). 미지원 -1.</item>
        /// <item><paramref name="footholdCount"/> — 가려짐 계산 뒤 남은 발판 조각 수. 위 둘과 함께
        ///   보면 O(n^2) 가려짐 솔버가 커지고 있는지도 보인다.</item>
        /// </list>
        /// </summary>
        public static void RecordWindowEnumeration(long elapsedTicks, int enumeratedWindowCount,
            int dwmProbeCount, int footholdCount)
        {
            double ms = elapsedTicks * TicksToMs;
            Buckets[_current].EnumMs += ms;
            Buckets[_current].EnumCount++;
            Buckets[_current].EnumeratedWindowCount = enumeratedWindowCount;
            Buckets[_current].DwmProbeCount = dwmProbeCount;
            Buckets[_current].FootholdCount = footholdCount;

            _winEnumTotalMs += ms;
            _winEnumCount++;
            _baseTotalMs += ms;
            _baseCount++;
            if (ms > _baseMaxMs) _baseMaxMs = ms;
            if (enumeratedWindowCount > _baseMaxEnumerated) _baseMaxEnumerated = enumeratedWindowCount;
            if (dwmProbeCount > _baseMaxDwm) _baseMaxDwm = dwmProbeCount;
            if (ms > _winEnumMaxMs) _winEnumMaxMs = ms;
            _lastEnumeratedCount = enumeratedWindowCount;
            _lastDwmProbeCount = dwmProbeCount;
            _lastFootholdCount = footholdCount;
            if (enumeratedWindowCount > _maxEnumeratedCount) _maxEnumeratedCount = enumeratedWindowCount;
            if (dwmProbeCount > _maxDwmProbeCount) _maxDwmProbeCount = dwmProbeCount;
        }

        // ====================================================================================
        // 계측 입력 (2) — 로그 쓰기. StallProfilingLogHandler가 부른다.
        // ====================================================================================

        /// <summary>
        /// <c>Debug.Log</c> 한 줄이 실제로 걸린 시간(스택트레이스 캡처 + Player.log 동기 쓰기 포함).
        /// <paramref name="message"/>는 태그 집계에만 쓰며 <b>보관하지 않는다</b>(최초 1회 Substring 제외).
        /// </summary>
        public static void RecordLogWrite(long elapsedTicks, string message)
        {
            double ms = elapsedTicks * TicksToMs;
            Buckets[_current].LogMs += ms;
            Buckets[_current].LogCount++;

            _logTotalMs += ms;
            _logCount++;
            if (ms > _logMaxMs) _logMaxMs = ms;
            CountTag(message);
        }

        internal static void CountTag(string message)
        {
            if (string.IsNullOrEmpty(message) || message[0] != '[')
            {
                _untaggedCount++;
                return;
            }

            int close = message.IndexOf(']', 1);
            if (close <= 1 || close > MaxTagLength)
            {
                _untaggedCount++;
                return;
            }

            int hash = 17;
            for (int i = 1; i < close; i++) hash = hash * 31 + message[i];

            for (int i = 0; i < _tagKinds; i++)
            {
                if (TagHashes[i] != hash || TagNames[i].Length != close + 1) continue;
                TagCounts[i]++;
                return;
            }

            if (_tagKinds >= MaxTags)
            {
                _untaggedCount++;
                return;
            }

            TagHashes[_tagKinds] = hash;
            TagNames[_tagKinds] = message.Substring(0, close + 1); // 태그 종류당 딱 한 번만 할당된다.
            TagCounts[_tagKinds] = 1;
            _tagKinds++;
        }

        // ====================================================================================
        // 프레임 경계
        // ====================================================================================

        /// <summary>모든 Update보다 먼저(실행 순서 -30000) 호출된다.</summary>
        internal static void BeginFrame()
        {
            float dtMs = Time.unscaledDeltaTime * 1000f;
            if (_spikeCooldownLeft > 0f) _spikeCooldownLeft -= Time.unscaledDeltaTime;
            _summaryTimer += Time.unscaledDeltaTime;
            _enumSummaryTimer += Time.unscaledDeltaTime;

            int previous = _current;
            EvaluateSpike(dtMs, previous);

            // 다음 버킷으로 넘어가며 비운다(2칸 순환).
            _current = 1 - previous;
            Buckets[_current] = default;
            _logicStartTicks = Stopwatch.GetTimestamp();

            if (_enumSummaryTimer >= EnumSummaryIntervalSeconds) EmitEnumerationBaseline();
            if (_summaryTimer >= SummaryIntervalSeconds) EmitSummary();
        }

        /// <summary>모든 LateUpdate보다 나중에(실행 순서 +30000) 호출된다.</summary>
        internal static void EndLogicPhase()
        {
            if (_logicStartTicks == 0) return;
            double ms = (Stopwatch.GetTimestamp() - _logicStartTicks) * TicksToMs;
            Buckets[_current].LogicMs = ms;
            if (ms > _logicMaxMs) _logicMaxMs = ms;
        }

        private static void EvaluateSpike(float dtMs, int previousBucket)
        {
            if (dtMs < SpikeAbsoluteMs) return;

            // 절감 등급(Away 15fps / DisplayOff 4fps)에서는 긴 프레임이 정상이다 —
            // FramePacing의 [프레임스파이크]와 완전히 같은 상대 조건을 건다.
            int cap = Application.targetFrameRate;
            float expectedMs = cap > 0 ? 1000f / cap : 1000f / 60f;
            if (dtMs < expectedMs * SpikeRelativeFactor) return;

            _spikeCount++;
            _spikeSinceSummary++;
            if (!_enabled) return;
            if (_spikeCooldownLeft > 0f) return;
            _spikeCooldownLeft = SpikeLogCooldownSeconds;

            FrameBucket b = Buckets[previousBucket];
            float logicMs = (float)b.LogicMs;
            float enumMs = (float)b.EnumMs;
            float logMs = (float)b.LogMs;
            float otherLogicMs = Mathf.Max(0f, logicMs - enumMs - logMs);
            float outsideMs = Mathf.Max(0f, dtMs - logicMs);

            Debug.Log($"[스톨귀인] 프레임#{Time.frameCount} 직전 {dtMs:F1}ms(기대 {expectedMs:F1}ms, 누적 {_spikeCount}회) — " +
                $"로직 {logicMs:F1}ms({Share(logicMs, dtMs):F0}%) : 창열거 {enumMs:F1}ms/{b.EnumCount}회" +
                $"(열거창 {b.EnumeratedWindowCount}개 -> DWM조회 {b.DwmProbeCount}회 -> 발판 {b.FootholdCount}조각), " +
                $"로그 {logMs:F1}ms/{b.LogCount}줄, " +
                $"기타로직 {otherLogicMs:F1}ms | 로직밖 {outsideMs:F1}ms({Share(outsideMs, dtMs):F0}%). " +
                $"판정: **{Describe(Attribute(dtMs, logicMs, enumMs, logMs))}**. " +
                $"(같은 프레임#의 [프레임스파이크] 줄과 짝이다. '로직밖'이면 원인은 창열거도 로그도 아니라 " +
                $"렌더/프레젠트/OS 합성 대기다. 다음 {SpikeLogCooldownSeconds:F0}초간 이 줄은 억제한다.)");
        }

        private static float Share(float part, float whole) => whole > 0f ? part / whole * 100f : 0f;

        /// <summary>
        /// **폴링 -> 이벤트 구조 변경 라운드의 기준선(baseline) 줄.** 리더 지시로 형식을 고정한다 —
        /// 그 라운드는 이 한 줄만 before/after로 비교하면 효과를 증명할 수 있다.
        ///
        /// <para>읽는 법: <c>초당 X.XXms</c>가 이 기능이 프레임 예산에서 실제로 가져가는 몫이다.
        /// 60fps에서 1초는 1000ms이므로 <b>초당 1ms = 0.1%</b>다. 이 값이 1ms 근처면 창 열거는
        /// 렉의 원인이 아니고, 수십 ms면 원인이다. 추측이 아니라 이 숫자가 답한다.</para>
        /// </summary>
        private static void EmitEnumerationBaseline()
        {
            float window = _enumSummaryTimer;
            _enumSummaryTimer = 0f;

            if (_enabled && _baseCount > 0)
            {
                double mean = _baseTotalMs / _baseCount;
                double perSecond = _baseTotalMs / Mathf.Max(0.001f, window);
                Debug.Log($"[발판열거] 1회 평균 {mean:F2}ms / 최대 {_baseMaxMs:F2}ms, {_baseCount}회/{window:F0}초 " +
                    $"(초당 {perSecond:F2}ms = 실행 시간의 {perSecond / 10.0:F2}%), " +
                    $"전체 창 {_lastEnumeratedCount}개(최대 {_baseMaxEnumerated}), " +
                    $"정밀검사 {_lastDwmProbeCount}회(최대 {_baseMaxDwm}), 발판 {_lastFootholdCount}조각. " +
                    "(-1 = 그 플랫폼이 보고하지 않는 값. '정밀검사'는 크로스 프로세스 DWM 조회 횟수다. " +
                    "'초당 ms'가 이 기능이 실제로 가져가는 몫이다 — 60fps 예산 16.7ms/프레임 x 60 = " +
                    "초당 1000ms이므로 초당 10ms면 1%다.)");
            }

            // 로그를 껐을 때도 누적치는 반드시 비운다 — 24시간 상주 앱에서 '최대값'이 실행 전체의
            // 최대로 굳으면 이 줄의 창(window) 의미가 무너진다.
            _baseTotalMs = _baseMaxMs = 0;
            _baseCount = 0;
            _baseMaxEnumerated = _lastEnumeratedCount;
            _baseMaxDwm = _lastDwmProbeCount;
        }

        private static void EmitSummary()
        {
            float window = _summaryTimer;
            _summaryTimer = 0f;

            if (_enabled && (_winEnumCount > 0 || _logCount > 0))
            {
                double enumMean = _winEnumCount > 0 ? _winEnumTotalMs / _winEnumCount : 0.0;
                double logMean = _logCount > 0 ? _logTotalMs / _logCount : 0.0;
                Debug.Log($"[스톨귀인] 최근 {window:F0}초 요약 — " +
                    $"창열거 {_winEnumCount}회 평균 {enumMean:F2}ms 최대 {_winEnumMaxMs:F1}ms " +
                    $"(초당 {_winEnumTotalMs / Mathf.Max(0.001f, window):F2}ms | " +
                    $"열거창 최근 {_lastEnumeratedCount}/최대 {_maxEnumeratedCount}개, " +
                    $"DWM조회 최근 {_lastDwmProbeCount}/최대 {_maxDwmProbeCount}회, 발판 {_lastFootholdCount}조각) | " +
                    $"로그 {_logCount}줄 평균 {logMean:F3}ms 최대 {_logMaxMs:F1}ms " +
                    $"(초당 {_logCount / Mathf.Max(0.001f, window):F1}줄 / {_logTotalMs / Mathf.Max(0.001f, window):F2}ms) | " +
                    $"로직구간 최대 {_logicMaxMs:F1}ms | 스파이크 {_spikeSinceSummary}회. " +
                    $"가장 많이 찍은 로그: {TopTags()}. " +
                    $"정보로그 스택트레이스={(PlayerLogPolicy.InfoStackTracesSuppressed ? "꺼짐" : "켜짐")}. " +
                    "(창열거와 로그, 두 '초당 ms'를 비교하면 어느 쪽이 프레임 예산을 먹는지 바로 갈린다 — " +
                    "16.7ms 예산에서 초당 1ms는 0.1%다. 둘 다 작은데 스파이크가 계속 나면 원인은 " +
                    "우리 Update 밖(렌더/프레젠트/OS 합성)이며, 위 [스톨귀인] 스파이크 줄의 판정이 그렇게 말한다.)");
            }

            _winEnumTotalMs = _winEnumMaxMs = 0;
            _winEnumCount = 0;
            _logTotalMs = _logMaxMs = 0;
            _logCount = 0;
            _logicMaxMs = 0;
            _spikeSinceSummary = 0;
            _maxEnumeratedCount = _lastEnumeratedCount;
            _maxDwmProbeCount = _lastDwmProbeCount;
            _untaggedCount = 0;
            for (int i = 0; i < _tagKinds; i++) TagCounts[i] = 0;
        }

        /// <summary>상위 3개 태그를 "이름 xN" 형태로 잇는다. 요약 로그(60초에 한 번)에서만 호출된다.</summary>
        internal static string TopTags()
        {
            if (_tagKinds == 0) return $"(태그 없는 줄 {_untaggedCount}개)";

            int a = -1, bIdx = -1, c = -1;
            for (int i = 0; i < _tagKinds; i++)
            {
                if (a < 0 || TagCounts[i] > TagCounts[a]) { c = bIdx; bIdx = a; a = i; }
                else if (bIdx < 0 || TagCounts[i] > TagCounts[bIdx]) { c = bIdx; bIdx = i; }
                else if (c < 0 || TagCounts[i] > TagCounts[c]) { c = i; }
            }

            string result = $"{TagNames[a]} x{TagCounts[a]}";
            if (bIdx >= 0 && TagCounts[bIdx] > 0) result += $", {TagNames[bIdx]} x{TagCounts[bIdx]}";
            if (c >= 0 && TagCounts[c] > 0) result += $", {TagNames[c]} x{TagCounts[c]}";
            if (_untaggedCount > 0) result += $", (태그 없음) x{_untaggedCount}";
            return result;
        }

        // ====================================================================================
        // 판정 — 순수 함수라 EditMode 테스트가 실기 없이 실측한다.
        // ====================================================================================

        /// <summary>
        /// 프레임 시간을 네 구간으로 나눈 실측치에서 <b>지배적 기여자</b>를 고른다.
        /// 어느 쪽도 <see cref="DominantShare"/>를 넘지 못하면 <see cref="StallCulprit.Mixed"/>다 —
        /// 확정하지 못했다는 사실을 숨기지 않는 것이 이 프로젝트의 규칙이다.
        /// </summary>
        public static StallCulprit Attribute(float frameMs, float logicMs, float enumMs, float logMs)
        {
            if (frameMs <= 0f) return StallCulprit.Unknown;

            float logic = Mathf.Clamp(logicMs, 0f, frameMs);
            float outside = frameMs - logic;
            if (logic < frameMs * LogicShareThreshold)
            {
                return outside >= frameMs * DominantShare ? StallCulprit.OutsideLogic : StallCulprit.Mixed;
            }

            float e = Mathf.Max(0f, enumMs);
            float l = Mathf.Max(0f, logMs);
            float other = Mathf.Max(0f, logic - e - l);

            if (e >= frameMs * DominantShare) return StallCulprit.WindowEnumeration;
            if (l >= frameMs * DominantShare) return StallCulprit.LogWrite;
            if (other >= frameMs * DominantShare) return StallCulprit.OtherLogic;
            return StallCulprit.Mixed;
        }

        public static string Describe(StallCulprit culprit)
        {
            switch (culprit)
            {
                case StallCulprit.WindowEnumeration: return "창열거(네이티브)";
                case StallCulprit.LogWrite: return "로그쓰기(파일 IO)";
                case StallCulprit.OtherLogic: return "기타로직(우리 Update 안)";
                case StallCulprit.OutsideLogic: return "로직밖(렌더/프레젠트/OS 합성)";
                case StallCulprit.Mixed: return "혼합(지배적 원인 없음)";
                default: return "판정불가";
            }
        }
    }

    /// <summary>긴 프레임의 지배적 원인. <see cref="StallAttribution.Attribute"/>가 실측치로만 고른다.</summary>
    public enum StallCulprit
    {
        Unknown = 0,
        WindowEnumeration,
        LogWrite,
        OtherLogic,
        OutsideLogic,
        Mixed,
    }
}
