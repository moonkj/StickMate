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
    ///
    /// ============================================================================
    /// ★ 2차 라운드(2026-09-01) — <c>[스톨구간]</c> 줄이 생겼다. 그쪽을 먼저 봐라.
    /// ============================================================================
    /// 1차 계측은 "기타로직"까지만 말했다. 그건 <b>잔차</b>(= 로직 − 창열거 − 로그)라 이름이 없다.
    /// 실기 로그가 <c>로직구간 최대 684.1ms</c>를 잡았는데 같은 창의 <c>창열거 최대는 14.8ms</c>였다 —
    /// 669ms가 익명이었다. 그래서 로직 구간을 두 방향으로 더 쪼갠다:
    ///   · <b>단계</b>: 물리(FixedUpdate) / Update / 사이 / LateUpdate.
    ///     ★ 물리는 1차 계측의 <b>사각지대</b>였다 — FixedUpdate는 Update보다 먼저 도는데 로직 구간이
    ///       Update에서 시작하므로, 물리 시간이 통째로 "로직밖"으로 잘못 귀속돼 있었다.
    ///   · <b>이름 있는 구간</b> 10종(<see cref="StallSection"/>). 중첩은 자기시간으로 처리하므로
    ///     구간 합이 로직 시간을 넘지 않고, 남는 차이가 <c>미계측</c>으로 정직하게 찍힌다.
    /// 그리고 <b>최악 프레임은 5초 쿨다운과 무관하게 항상 스냅샷</b>해 요약이 다시 보고한다 —
    /// 실기에서 가장 심한 프레임이 바로 그 억제된 줄이었고, 그래서 "남은 줄이 전부 판정: 로직밖"이
    /// "로직 안은 무죄"로 잘못 읽혔다.
    ///
    /// ============================================================================
    /// [스톨구간] 읽는 법 (★ 이 설명은 <b>로그 줄에 싣지 않는다</b> — 2026-09-02)
    /// ============================================================================
    /// 이 줄의 목적은 하나다: "기타로직"이라는 <b>잔차에 이름을 붙이고</b>, 그 이름이 <b>시간이 갈수록
    /// 커지는지</b> 보여 주는 것.
    /// <list type="number">
    /// <item>두 개의 <c>[스톨구간]</c> 줄을 나란히 놓고 같은 구간 이름의 <c>생애</c>와 <c>최대</c>를
    ///   비교한다. 창 번호(<c>#N</c>)와 가동 분이 그 간격을 말해 준다.</item>
    /// <item>평균이 그대로인데 <b>최대만</b> 커지면 간헐적 큰 멈춤이 악화되는 것이고, 그게 사용자가
    ///   "켜놓을수록 렉이 심해짐"이라고 말한 현상이다.</item>
    /// </list>
    /// 이 설명 네 문장이 예전에는 로그 줄마다 그대로 실려 나가 <b>줄당 1,272B</b>(Player.log 바이트
    /// 1위)를 차지했다. 설명은 매번 같고 숫자만 매번 다르다 — 설명은 여기, 숫자만 로그로.
    /// </summary>
    public static class StallAttribution
    {
        // ------------------------------------------------------------------------------------
        // 판정 상수 — FramePacing의 [프레임스파이크]와 **같은 문턱**을 쓴다. 그래야 두 로그가
        // 같은 프레임#으로 1:1 짝을 이뤄 사용자가 눈으로 대조할 수 있다.
        // ★ 2026-09-02: 상대 조건의 분모(기대 프레임 시간)도 값 복사가 아니라 아래
        //   <see cref="ExpectedFrameMs"/> **한 함수**를 두 곳이 함께 부르는 형태로 합쳤다.
        //   따로 두면 한쪽만 고쳤을 때 두 로그의 프레임# 짝이 조용히 어긋난다.
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

            // ---- 2026-09-01 2차 라운드: "기타로직"을 더 잘게 쪼개기 위해 추가된 칸 ----
            // 이전 판(1차)에는 LogicMs 하나뿐이었다. 그래서 실기 로그가 "기타로직 51.0ms"라고만
            // 말하고 그 51ms가 어디서 났는지는 아무도 몰랐다. 아래 넷이 그 질문에 이름을 붙인다.
            public double UpdateMs;    // 모든 Update가 걸린 시간
            public double LateMs;      // 모든 LateUpdate가 걸린 시간
            public double FixedMs;     // 이 프레임의 FixedUpdate(물리) 전부. ★ 이전엔 '로직밖'에 섞여 있었다.
            public int FixedSteps;     // 그 스텝 수(따라잡기 폭주 감지용)
        }

        private static readonly FrameBucket[] Buckets = new FrameBucket[2];
        private static int _current;
        private static long _logicStartTicks;
        private static long _lateStartTicks;
        private static long _fixedStepStartTicks;

        // FixedUpdate는 같은 프레임의 Update보다 **먼저** 돈다. 즉 BeginFrame()이 버킷을 바꾸기 전에
        // 이미 끝나 있다. 그래서 곧바로 버킷에 넣으면 한 프레임씩 밀린다 — 여기 모아 두었다가
        // BeginFrame()이 새 버킷을 연 직후에 그 버킷으로 옮긴다(정확히 그 프레임의 물리 시간이 된다).
        private static double _pendingFixedMs;
        private static int _pendingFixedSteps;

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

        // ====================================================================================
        // ★★ 2026-09-01 2차 라운드 — "기타로직"에 이름을 붙이는 구간 계측
        // ====================================================================================
        // 1차 계측이 답한 것: 창 열거(0.5%)·로그 IO(0.04%)는 무죄. 남은 것이 <b>기타로직</b>이고
        // 실기 60초 요약이 <c>로직구간 최대 684.1ms</c>를 잡았다. 그런데 "기타로직"은 정의상
        // <c>총시간 − 창열거 − 로그</c>라 **정체불명 잔차**다 — 이름이 없으면 다음 라운드도 추측한다.
        //
        // 그래서 로직 구간을 (a) 단계(Update / 사이 / LateUpdate / FixedUpdate)와
        // (b) <b>이름 있는 구간</b>으로 동시에 쪼갠다. 다음 실기 로그가 상위 3개를 이름으로 말한다.
        //
        // ---- 비용 ----
        // 구간 진입/이탈당 <c>Stopwatch.GetTimestamp()</c> 1회씩(Windows QPC ~25ns) + 배열 대입 몇 개.
        // <b>할당 0</b>. 프레임당 구간 진입 횟수는 40회 수준이므로 총 80회 QPC = 약 2μs
        // (16,700μs 예산의 0.012%). "계측이 증상을 만들지 않는다"는 이 클래스의 규율은 그대로다.
        //
        // ---- 중첩은 자기시간(self time)으로 처리한다 ----
        // 예: CharacterProgressionDirector.Update(=Directors)가 CharacterSaveStore.Save(=Save)를 부른다.
        // 그대로 더하면 저장 시간이 두 번 세어져 합이 로직 시간을 넘는다. 그래서 스택을 두고
        // <b>자식 구간의 총시간을 부모에서 뺀다</b>. 결과적으로 모든 구간의 합 ≤ 로직 시간이고,
        // 남는 차이가 곧 "아직 계측 안 붙은 곳"이다(요약에 <c>미계측</c>으로 찍는다).
        //
        // ---- 시간에 따라 커지는가를 볼 수 있게 ----
        // 사용자 확정 조건: "켜놓을수록 렉이 심해짐"(p50은 그대로, p99/최대만 커짐). 그래서 구간마다
        // <b>이번 60초 창</b>의 (합/횟수/최대)와 <b>앱 가동 이후 누적</b>의 (합/횟수/최대)를 함께 남긴다.
        // 두 로그의 같은 구간 이름을 비교하면 "3분째 vs 30분째"가 바로 갈린다.

        private const int SectionCount = (int)StallSection.Count;

        /// <summary>로그에 찍히는 사람이 읽는 이름. <see cref="StallSection"/>과 <b>순서가 1:1</b>이다.</summary>
        private static readonly string[] SectionNames =
        {
            "에이전트", "연출감독", "연출렌더", "UI창", "초상화",
            "액세서리", "대사", "세이브", "플랫폼유지", "독물리",
        };

        /// <summary>구간 이름을 밖(테스트/진단)에서 읽는다.</summary>
        public static string SectionName(StallSection section)
        {
            int i = (int)section;
            return i >= 0 && i < SectionNames.Length ? SectionNames[i] : "알수없음";
        }

        // 프레임 단위(버킷 2칸) / 60초 창 / 앱 가동 이후 누적. 전부 고정 크기 배열이라 할당이 없다.
        private static readonly double[,] SectionFrameMs = new double[2, SectionCount];
        private static readonly double[] SectionWindowMs = new double[SectionCount];
        private static readonly double[] SectionWindowMaxMs = new double[SectionCount];
        private static readonly int[] SectionWindowCount = new int[SectionCount];
        private static readonly double[] SectionLifeMs = new double[SectionCount];
        private static readonly double[] SectionLifeMaxMs = new double[SectionCount];
        private static readonly int[] SectionLifeCount = new int[SectionCount];

        // 중첩 스택. 8이면 충분하고도 남는다(실제 최대 중첩은 2~3단).
        private const int MaxSectionDepth = 8;
        private static readonly int[] StackSection = new int[MaxSectionDepth];
        private static readonly long[] StackStartTicks = new long[MaxSectionDepth];
        private static readonly long[] StackChildTicks = new long[MaxSectionDepth];
        private static int _sectionDepth;
        private static int _sectionOverflowCount;

        /// <summary>
        /// 구간 하나를 재는 <c>using</c> 범위. <b>구조체라 힙 할당이 없고</b>(C# 컴파일러가 박싱 없이
        /// Dispose를 직접 부른다), <c>finally</c>로 닫히므로 중간에 예외가 나도 스택이 어긋나지 않는다.
        /// </summary>
        public readonly struct SectionScope : System.IDisposable
        {
            private readonly bool _open;
            internal SectionScope(StallSection section)
            {
                BeginSection(section);
                _open = true;
            }
            public void Dispose() { if (_open) EndSection(); }
        }

        /// <summary>
        /// <c>using var _ = StallAttribution.Section(StallSection.Directors);</c> 형태로 쓴다.
        /// 메서드 어디서 return하든 반드시 닫힌다.
        /// </summary>
        public static SectionScope Section(StallSection section) => new SectionScope(section);

        internal static void BeginSection(StallSection section)
        {
            if (_sectionDepth >= MaxSectionDepth)
            {
                // 계측 때문에 앱이 이상해지면 안 된다 — 깊이만 세고 조용히 통과시킨다.
                _sectionDepth++;
                _sectionOverflowCount++;
                return;
            }
            StackSection[_sectionDepth] = (int)section;
            StackStartTicks[_sectionDepth] = Stopwatch.GetTimestamp();
            StackChildTicks[_sectionDepth] = 0;
            _sectionDepth++;
        }

        internal static void EndSection()
        {
            if (_sectionDepth <= 0) return;
            _sectionDepth--;
            if (_sectionDepth >= MaxSectionDepth) return;

            long total = Stopwatch.GetTimestamp() - StackStartTicks[_sectionDepth];
            long self = total - StackChildTicks[_sectionDepth];
            if (self < 0) self = 0;

            int id = StackSection[_sectionDepth];
            double ms = self * TicksToMs;
            SectionFrameMs[_current, id] += ms;
            SectionWindowMs[id] += ms;
            SectionWindowCount[id]++;
            if (ms > SectionWindowMaxMs[id]) SectionWindowMaxMs[id] = ms;
            SectionLifeMs[id] += ms;
            SectionLifeCount[id]++;
            if (ms > SectionLifeMaxMs[id]) SectionLifeMaxMs[id] = ms;

            // 부모가 있으면 "내 총시간"을 부모의 자식시간에 더한다 = 부모는 자기시간만 갖는다.
            if (_sectionDepth > 0) StackChildTicks[_sectionDepth - 1] += total;
        }

        /// <summary>테스트/진단용 — 이번 60초 창에서 그 구간이 쓴 총 자기시간(ms).</summary>
        public static double WindowSectionMs(StallSection section) => SectionWindowMs[(int)section];

        /// <summary>테스트/진단용 — 이번 60초 창에서 그 구간이 몇 번 실행됐는가.</summary>
        public static int WindowSectionCount(StallSection section) => SectionWindowCount[(int)section];

        // ------------------------------------------------------------------------------------
        // 성장 관측치 — "켜놓을수록 심해진다"를 로그가 스스로 증명하게 하는 값들
        // ------------------------------------------------------------------------------------
        private static int _summaryIndex;                 // 몇 번째 60초 창인가(1부터)
        private static double _fixedMaxMs, _updateMaxMs, _lateMaxMs, _gapMaxMs;
        private static double _fixedTotalMs, _updateTotalMs, _lateTotalMs;
        private static int _maxFixedSteps;
        private static int _fontAtlasRebuildsWindow, _fontAtlasRebuildsTotal;
        private static int _fontAtlasWidth, _fontAtlasHeight;
        private static int _gc0Prev, _gc1Prev, _gc2Prev;

        /// <summary>
        /// ★ 폰트 아틀라스 재구성 횟수 — <b>"시간이 갈수록 커지는 간헐적 멈춤"의 교과서적 후보</b>다.
        /// uGUI의 동적 폰트(LegacyRuntime.ttf)는 새 글리프가 필요할 때마다 아틀라스에 굽고, 자리가
        /// 모자라면 <b>아틀라스 전체를 다시 굽고 그 폰트를 쓰는 모든 Text를 다시 만든다</b>.
        /// 한글은 글리프가 수천 자라 앱을 오래 켜 둘수록 이 재구성이 커진다.
        /// 세는 비용은 이벤트 1회당 정수 증가 몇 개뿐이다(정상 프레임에는 아예 불리지 않는다).
        /// </summary>
        public static void RecordFontAtlasRebuild(int atlasWidth, int atlasHeight)
        {
            _fontAtlasRebuildsWindow++;
            _fontAtlasRebuildsTotal++;
            _fontAtlasWidth = atlasWidth;
            _fontAtlasHeight = atlasHeight;
        }

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
            _lateStartTicks = _fixedStepStartTicks = 0;
            _pendingFixedMs = 0.0;
            _pendingFixedSteps = 0;
            _sectionDepth = 0;
            _sectionOverflowCount = 0;
            _summaryIndex = 0;
            _fixedMaxMs = _updateMaxMs = _lateMaxMs = _gapMaxMs = 0.0;
            _fixedTotalMs = _updateTotalMs = _lateTotalMs = 0.0;
            _maxFixedSteps = 0;
            _fontAtlasRebuildsWindow = _fontAtlasRebuildsTotal = 0;
            _fontAtlasWidth = _fontAtlasHeight = 0;
            _gc0Prev = _gc1Prev = _gc2Prev = 0;
            _worstDtMs = _worstLogicMs = _worstEnumMs = _worstLogMs = _worstFixedMs = 0f;
            _worstUpdateMs = _worstLateMs = 0f;
            _worstFrame = _worstFixedSteps = 0;
            for (int i = 0; i < SectionCount; i++)
            {
                SectionFrameMs[0, i] = SectionFrameMs[1, i] = 0.0;
                SectionWindowMs[i] = SectionWindowMaxMs[i] = 0.0;
                SectionWindowCount[i] = 0;
                SectionLifeMs[i] = SectionLifeMaxMs[i] = 0.0;
                SectionLifeCount[i] = 0;
                WorstSections[i] = 0.0;
            }
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
        /// <item><paramref name="dwmProbeCount"/> — 값싼 필터(가시성/제목/최소화 등)를 뚫고
        ///   <b>창 하나당 비싼 처리</b>까지 간 횟수. <b>플랫폼 중립 지표</b>다(2026-09-01 정정):
        ///   Windows는 크로스 프로세스 DWM 호출(<c>DwmGetWindowAttribute</c>)이고, macOS는
        ///   WindowServer가 목록을 한 번에 주므로 창당 왕복은 없지만 대신 CFString/딕셔너리 복사가
        ///   창 수에 비례한다. 두 플랫폼이 답하는 질문은 같다 — "창당 비싼 처리를 몇 번 했는가". 실기
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
            for (int i = 0; i < SectionCount; i++) SectionFrameMs[_current, i] = 0.0;

            // FixedUpdate는 이 Update보다 먼저 끝나 있다 — 지금 이 프레임의 물리 시간으로 확정한다.
            Buckets[_current].FixedMs = _pendingFixedMs;
            Buckets[_current].FixedSteps = _pendingFixedSteps;
            if (_pendingFixedMs > _fixedMaxMs) _fixedMaxMs = _pendingFixedMs;
            if (_pendingFixedSteps > _maxFixedSteps) _maxFixedSteps = _pendingFixedSteps;
            _pendingFixedMs = 0.0;
            _pendingFixedSteps = 0;

            // 구간 스택이 어긋난 채 넘어오면(있어서는 안 되지만) 다음 프레임까지 오염되지 않게 끊는다.
            _sectionDepth = 0;

            _logicStartTicks = Stopwatch.GetTimestamp();
            _lateStartTicks = 0;

            if (_enumSummaryTimer >= EnumSummaryIntervalSeconds) EmitEnumerationBaseline();
            if (_summaryTimer >= SummaryIntervalSeconds) EmitSummary();
        }

        /// <summary>모든 Update보다 나중에(실행 순서 +30000) 호출된다 — Update 단계의 끝.</summary>
        internal static void EndUpdatePhase()
        {
            if (_logicStartTicks == 0) return;
            double ms = (Stopwatch.GetTimestamp() - _logicStartTicks) * TicksToMs;
            Buckets[_current].UpdateMs = ms;
            if (ms > _updateMaxMs) _updateMaxMs = ms;
            _updateTotalMs += ms;
        }

        /// <summary>모든 LateUpdate보다 먼저(실행 순서 -30000) 호출된다 — LateUpdate 단계의 시작.</summary>
        internal static void BeginLatePhase() => _lateStartTicks = Stopwatch.GetTimestamp();

        /// <summary>모든 LateUpdate보다 나중에(실행 순서 +30000) 호출된다.</summary>
        internal static void EndLogicPhase()
        {
            if (_logicStartTicks == 0) return;
            long now = Stopwatch.GetTimestamp();
            double ms = (now - _logicStartTicks) * TicksToMs;
            Buckets[_current].LogicMs = ms;
            if (ms > _logicMaxMs) _logicMaxMs = ms;

            if (_lateStartTicks != 0)
            {
                double lateMs = (now - _lateStartTicks) * TicksToMs;
                Buckets[_current].LateMs = lateMs;
                if (lateMs > _lateMaxMs) _lateMaxMs = lateMs;
                _lateTotalMs += lateMs;
                double gap = ms - Buckets[_current].UpdateMs - lateMs;
                if (gap > _gapMaxMs) _gapMaxMs = gap;
            }
        }

        // ------------------------------------------------------------------------------------
        // FixedUpdate(물리) — ★ 1차 계측의 사각지대였다.
        // Unity의 프레임 순서는 [FixedUpdate x K] -> Update -> LateUpdate -> 렌더다. BeginFrame()이
        // Update(-30000)에 있으므로 **물리는 로직 구간 밖**이었고, 그래서 지금까지 물리 비용은 전부
        // "로직밖(렌더/프레젠트/합성)"으로 잘못 귀속돼 왔다. 랙돌 관절을 굴리는 앱에서 이건 큰 구멍이다.
        // 게다가 긴 프레임 뒤에는 Unity가 물리를 따라잡느라 한 프레임에 여러 스텝을 돌려(최대
        // maximumDeltaTime까지) 스스로 다음 스파이크를 만든다 — FixedSteps가 그 폭주를 드러낸다.
        // ------------------------------------------------------------------------------------

        /// <summary>모든 FixedUpdate보다 먼저(실행 순서 -30000) 호출된다.</summary>
        internal static void BeginFixedStep() => _fixedStepStartTicks = Stopwatch.GetTimestamp();

        /// <summary>모든 FixedUpdate보다 나중에(실행 순서 +30000) 호출된다.</summary>
        internal static void EndFixedStep()
        {
            if (_fixedStepStartTicks == 0) return;
            double ms = (Stopwatch.GetTimestamp() - _fixedStepStartTicks) * TicksToMs;
            _pendingFixedMs += ms;
            _pendingFixedSteps++;
            _fixedTotalMs += ms;
        }

        // ------------------------------------------------------------------------------------
        // ★ 이번 창의 "최악 프레임" 스냅샷 — 5초 쿨다운이 숨긴 진짜 범인을 되찾는 장치
        // ------------------------------------------------------------------------------------
        // 앞 라운드의 함정: 개별 스파이크 줄은 5초 쿨다운으로 억제돼 9회 중 몇 줄만 남았고, 남은 줄이
        // 전부 "판정: 로직밖"이라 <b>로직 안이 무죄인 것처럼 읽혔다</b>. 그런데 같은 60초 요약에는
        // <c>로직구간 최대 684.1ms</c>가 잡혀 있었다 — 즉 <b>가장 심한 프레임의 줄이 바로 그 억제된
        // 줄이었다</b>. 그래서 이제 최악 프레임은 쿨다운과 무관하게 **항상** 스냅샷으로 남기고
        // 60초 요약이 그 한 프레임을 이름까지 붙여 다시 보고한다. 표본을 잃지 않는다.
        private static float _worstDtMs, _worstLogicMs, _worstEnumMs, _worstLogMs, _worstFixedMs;
        private static float _worstUpdateMs, _worstLateMs;
        private static int _worstFrame, _worstFixedSteps;
        private static readonly double[] WorstSections = new double[SectionCount];

        private static void EvaluateSpike(float dtMs, int previousBucket)
        {
            if (dtMs < SpikeAbsoluteMs) return;

            // 절감 등급(Away 15fps / DisplayOff 4fps)에서는 긴 프레임이 정상이다 —
            // FramePacing의 [프레임스파이크]와 **같은 함수**로 같은 상대 조건을 건다.
            float expectedMs = ExpectedFrameMs();
            if (dtMs < expectedMs * SpikeRelativeFactor) return;

            _spikeCount++;
            _spikeSinceSummary++;

            FrameBucket b = Buckets[previousBucket];
            float logicMs = (float)b.LogicMs;
            float enumMs = (float)b.EnumMs;
            float logMs = (float)b.LogMs;
            float fixedMs = (float)b.FixedMs;
            float otherLogicMs = Mathf.Max(0f, logicMs - enumMs - logMs);
            float outsideMs = Mathf.Max(0f, dtMs - logicMs - fixedMs);

            // 쿨다운보다 **먼저** 스냅샷한다 — 억제된 줄이 곧 최악인 경우가 실제로 있었다.
            if (dtMs > _worstDtMs)
            {
                _worstDtMs = dtMs;
                _worstLogicMs = logicMs;
                _worstEnumMs = enumMs;
                _worstLogMs = logMs;
                _worstFixedMs = fixedMs;
                _worstUpdateMs = (float)b.UpdateMs;
                _worstLateMs = (float)b.LateMs;
                _worstFixedSteps = b.FixedSteps;
                _worstFrame = Time.frameCount;
                for (int i = 0; i < SectionCount; i++) WorstSections[i] = SectionFrameMs[previousBucket, i];
            }

            if (!_enabled) return;
            if (_spikeCooldownLeft > 0f) return;
            _spikeCooldownLeft = SpikeLogCooldownSeconds;

            Debug.Log($"[스톨귀인] 프레임#{Time.frameCount} 직전 {dtMs:F1}ms(기대 {expectedMs:F1}ms, 누적 {_spikeCount}회) — " +
                $"로직 {logicMs:F1}ms({Share(logicMs, dtMs):F0}%) : 창열거 {enumMs:F1}ms/{b.EnumCount}회" +
                $"(열거창 {b.EnumeratedWindowCount}개 -> 정밀검사 {b.DwmProbeCount}회 -> 발판 {b.FootholdCount}조각), " +
                $"로그 {logMs:F1}ms/{b.LogCount}줄, " +
                $"기타로직 {otherLogicMs:F1}ms | 물리 {fixedMs:F1}ms/{b.FixedSteps}스텝 | " +
                $"로직밖 {outsideMs:F1}ms({Share(outsideMs, dtMs):F0}%). " +
                $"단계: Update {b.UpdateMs:F1}ms / 사이 {Mathf.Max(0f, logicMs - (float)b.UpdateMs - (float)b.LateMs):F1}ms / " +
                $"LateUpdate {b.LateMs:F1}ms. " +
                $"구간 상위: {TopSectionsOfFrame(previousBucket)}. " +
                $"판정: **{Describe(Attribute(dtMs, logicMs, enumMs, logMs))}**.");
            // ★ 이 줄은 같은 프레임#의 [프레임스파이크]와 짝이고, 다음 SpikeLogCooldownSeconds초간
            //   억제되지만 **최악 프레임은 60초 요약이 억제와 무관하게 다시 보고한다**. 예전에는 이
            //   두 문장이 로그 줄마다 실려 나갔다 — 매번 같은 문장이므로 소스에만 둔다(2026-09-02).
        }

        /// <summary>
        /// **기대 프레임 시간을 실제 기구에서 계산한다**(스파이크 상대 조건의 분모).
        ///
        /// <para>예전에는 <c>Application.targetFrameRate</c>만 봤다. 그런데 vsync가 켜져 있으면
        /// (<c>vSyncCount &gt; 0</c>) targetFrameRate는 <b>무시되고</b> 상한은 주사율/vSyncCount로
        /// 정해진다 — 즉 "기대 프레임 시간"이라는 주석이 그 경우에 거짓말이었다.</para>
        ///
        /// <para><b>★ 이 계산으로 실효 임계가 바뀌는 등급은 DisplayOff 하나뿐이다</b>(120Hz 실기
        /// 기준, 나머지 등급은 <see cref="SpikeAbsoluteMs"/> 100ms 절대 하한이 2.5배 기대치를 전부
        /// 삼킨다). 목적은 임계 조정이 아니라 <b>주석이 거짓말하는 상태를 끝내는 것</b>이다.</para>
        ///
        /// <para><c>OnDemandRendering.renderFrameInterval</c>은 <b>곱하지 않는다</b>: 그건 프레젠트만
        /// 건너뛰고 게임 루프는 늦추지 않는데, <c>dtMs</c>의 출처인 <c>Time.unscaledDeltaTime</c>은
        /// <b>루프 주기</b>다. 곱하면 임계가 167ms로 올라가 실제 관측된 100~216ms 스톨이 통째로
        /// 사라진다 — 계기를 고치려다 눈을 감는 변경이다.</para>
        ///
        /// <para>이 함수는 스파이크 후보(100ms 초과)일 때만 불린다 — <c>QualitySettings</c>/
        /// <c>Screen</c> 조회는 네이티브라 평상시 프레임에서는 쓰지 않는다.</para>
        /// </summary>
        public static float ExpectedFrameMs()
            => ExpectedFrameMs(QualitySettings.vSyncCount,
                               Screen.currentResolution.refreshRateRatio.value,
                               Application.targetFrameRate);

        /// <summary>
        /// 위 함수의 <b>순수 산술</b>. 네이티브 상태를 읽지 않으므로 EditMode에서 그대로 검증된다
        /// (이 저장소는 "고쳤다는 서사만 남고 동작은 그대로"인 변경을 여러 번 겪었다 — 계기 수정일수록
        /// 숫자가 실제로 바뀌는지 테스트로 못 박아야 한다).
        /// </summary>
        /// <param name="vSyncCount">0이면 vsync 꺼짐.</param>
        /// <param name="refreshHz">디스플레이 주사율. 0/비정상이면 60Hz로 떨어진다.</param>
        /// <param name="targetFrameRate">vsync가 꺼져 있을 때만 쓰인다(-1 = 제한 없음).</param>
        public static float ExpectedFrameMs(int vSyncCount, double refreshHz, int targetFrameRate)
        {
            if (vSyncCount > 0)
            {
                // 주사율 조회가 0/비정상일 때 Infinity를 만들면 상대 비교가 항상 통과해
                // **스파이크를 한 건도 세지 않게** 된다(계기가 눈을 감는 실패). 60Hz로 떨어뜨린다.
                if (refreshHz > 1.0) return (float)(vSyncCount * (1000.0 / refreshHz));
                return vSyncCount * (1000f / 60f);
            }

            return targetFrameRate > 0 ? 1000f / targetFrameRate : 1000f / 60f;
        }

        /// <summary>한 프레임 안에서 자기시간이 큰 상위 3개 구간을 "이름 12.3ms"로 잇는다.</summary>
        private static string TopSectionsOfFrame(int bucket)
        {
            int a = -1, b2 = -1, c = -1;
            for (int i = 0; i < SectionCount; i++)
            {
                double v = SectionFrameMs[bucket, i];
                if (v <= 0.0) continue;
                if (a < 0 || v > SectionFrameMs[bucket, a]) { c = b2; b2 = a; a = i; }
                else if (b2 < 0 || v > SectionFrameMs[bucket, b2]) { c = b2; b2 = i; }
                else if (c < 0 || v > SectionFrameMs[bucket, c]) { c = i; }
            }
            if (a < 0) return "(계측 구간 0 — 이 프레임의 로직은 전부 미계측 경로다)";

            string result = $"{SectionNames[a]} {SectionFrameMs[bucket, a]:F1}ms";
            if (b2 >= 0) result += $", {SectionNames[b2]} {SectionFrameMs[bucket, b2]:F1}ms";
            if (c >= 0) result += $", {SectionNames[c]} {SectionFrameMs[bucket, c]:F1}ms";
            return result;
        }

        /// <summary>스냅샷된 최악 프레임의 상위 3개 구간.</summary>
        private static string TopSectionsOfWorst()
        {
            int a = -1, b2 = -1, c = -1;
            for (int i = 0; i < SectionCount; i++)
            {
                double v = WorstSections[i];
                if (v <= 0.0) continue;
                if (a < 0 || v > WorstSections[a]) { c = b2; b2 = a; a = i; }
                else if (b2 < 0 || v > WorstSections[b2]) { c = b2; b2 = i; }
                else if (c < 0 || v > WorstSections[c]) { c = i; }
            }
            if (a < 0) return "(계측 구간 0)";
            string result = $"{SectionNames[a]} {WorstSections[a]:F1}ms";
            if (b2 >= 0) result += $", {SectionNames[b2]} {WorstSections[b2]:F1}ms";
            if (c >= 0) result += $", {SectionNames[c]} {WorstSections[c]:F1}ms";
            return result;
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
                    "(-1 = 그 플랫폼이 보고하지 않는 값. '정밀검사'는 값싼 필터를 통과해 창 하나당 비싼 " +
                    "처리까지 간 횟수다 — Windows는 크로스 프로세스 DWM 조회, macOS는 창별 속성 복사. " +
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
            _summaryIndex++;

            if (_enabled && (_winEnumCount > 0 || _logCount > 0))
            {
                double enumMean = _winEnumCount > 0 ? _winEnumTotalMs / _winEnumCount : 0.0;
                double logMean = _logCount > 0 ? _logTotalMs / _logCount : 0.0;
                Debug.Log($"[스톨귀인] 최근 {window:F0}초 요약 — " +
                    $"창열거 {_winEnumCount}회 평균 {enumMean:F2}ms 최대 {_winEnumMaxMs:F1}ms " +
                    $"(초당 {_winEnumTotalMs / Mathf.Max(0.001f, window):F2}ms | " +
                    $"열거창 최근 {_lastEnumeratedCount}/최대 {_maxEnumeratedCount}개, " +
                    $"정밀검사 최근 {_lastDwmProbeCount}/최대 {_maxDwmProbeCount}회, 발판 {_lastFootholdCount}조각) | " +
                    $"로그 {_logCount}줄 평균 {logMean:F3}ms 최대 {_logMaxMs:F1}ms " +
                    $"(초당 {_logCount / Mathf.Max(0.001f, window):F1}줄 / {_logTotalMs / Mathf.Max(0.001f, window):F2}ms) | " +
                    $"로직구간 최대 {_logicMaxMs:F1}ms | 스파이크 {_spikeSinceSummary}회. " +
                    $"가장 많이 찍은 로그: {TopTags()}. " +
                    $"정보로그 스택트레이스={(PlayerLogPolicy.InfoStackTracesSuppressed ? "꺼짐" : "켜짐")}. " +
                    "(창열거와 로그, 두 '초당 ms'를 비교하면 어느 쪽이 프레임 예산을 먹는지 바로 갈린다 — " +
                    "16.7ms 예산에서 초당 1ms는 0.1%다.)");

                EmitSectionSummary(window);
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

            // 구간/단계/최악프레임 창 누적치 리셋(생애 누적치는 유지 — 그게 '커지는가'의 근거다).
            for (int i = 0; i < SectionCount; i++)
            {
                SectionWindowMs[i] = 0.0;
                SectionWindowMaxMs[i] = 0.0;
                SectionWindowCount[i] = 0;
            }
            _fixedMaxMs = _updateMaxMs = _lateMaxMs = _gapMaxMs = 0.0;
            _fixedTotalMs = _updateTotalMs = _lateTotalMs = 0.0;
            _maxFixedSteps = 0;
            _fontAtlasRebuildsWindow = 0;
            _worstDtMs = _worstLogicMs = _worstEnumMs = _worstLogMs = _worstFixedMs = 0f;
            _worstUpdateMs = _worstLateMs = 0f;
            _worstFrame = _worstFixedSteps = 0;
            for (int i = 0; i < SectionCount; i++) WorstSections[i] = 0.0;
        }

        /// <summary>
        /// ★★ <b>이 라운드의 1차 산출물</b> — "기타로직"이 무엇이었는지 <b>이름으로</b> 말하는 줄.
        ///
        /// <para>읽는 법(세 가지를 한 줄에서 본다):</para>
        /// <list type="number">
        /// <item><b>단계</b>: 물리 / Update / 사이 / LateUpdate. "사이"는 Unity 내부(코루틴·애니메이션)다.
        ///   물리가 큰데 스텝 수가 여러 개면 <b>긴 프레임이 다음 긴 프레임을 만드는 따라잡기 폭주</b>다.</item>
        /// <item><b>구간 상위 3개</b>: 각각 (합/횟수/최대). 자기시간이라 서로 겹치지 않는다.
        ///   <c>미계측</c>이 크면 아직 이름표를 안 붙인 코드에 있다는 뜻이니 다음 라운드가 거기에 붙인다.</item>
        /// <item><b>커지는가</b>: 각 구간의 <c>생애</c> 값과 창 번호(<c>#N창</c>), 가동 시간, 관리 힙,
        ///   GC 세대별 횟수, 폰트 아틀라스 재구성 횟수. 두 로그의 같은 이름을 비교하면 추세가 보인다.</item>
        /// </list>
        /// </summary>
        private static void EmitSectionSummary(float window)
        {
            double sectionSum = 0.0;
            for (int i = 0; i < SectionCount; i++) sectionSum += SectionWindowMs[i];

            int a = -1, b = -1, c = -1;
            for (int i = 0; i < SectionCount; i++)
            {
                double v = SectionWindowMs[i];
                if (v <= 0.0) continue;
                if (a < 0 || v > SectionWindowMs[a]) { c = b; b = a; a = i; }
                else if (b < 0 || v > SectionWindowMs[b]) { c = b; b = i; }
                else if (c < 0 || v > SectionWindowMs[c]) { c = i; }
            }

            string top = a < 0
                ? "(구간 계측 0)"
                : DescribeSection(a) + (b >= 0 ? " | " + DescribeSection(b) : string.Empty)
                                     + (c >= 0 ? " | " + DescribeSection(c) : string.Empty);

            // GC/힙은 60초에 한 번만 읽는다 — GetTotalMemory(false)는 수집을 유발하지 않는다.
            long heapBytes = System.GC.GetTotalMemory(false);
            int gc0 = System.GC.CollectionCount(0);
            int gc1 = System.GC.CollectionCount(1);
            int gc2 = System.GC.CollectionCount(2);
            int d0 = gc0 - _gc0Prev, d1 = gc1 - _gc1Prev, d2 = gc2 - _gc2Prev;
            _gc0Prev = gc0; _gc1Prev = gc1; _gc2Prev = gc2;

            string worst = _worstDtMs > 0f
                ? $"이번 창 최악 프레임#{_worstFrame} {_worstDtMs:F1}ms — 로직 {_worstLogicMs:F1}(Update {_worstUpdateMs:F1} / " +
                  $"Late {_worstLateMs:F1}) / 물리 {_worstFixedMs:F1}ms{_worstFixedSteps}스텝 / " +
                  $"창열거 {_worstEnumMs:F1} / 로그 {_worstLogMs:F1} / 로직밖 " +
                  $"{Mathf.Max(0f, _worstDtMs - _worstLogicMs - _worstFixedMs):F1}ms. 구간: {TopSectionsOfWorst()}"
                : "이번 창 스파이크 없음";

            Debug.Log($"[스톨구간] #{_summaryIndex}창({window:F0}초, 가동 {Time.realtimeSinceStartup / 60f:F1}분) — " +
                $"단계 합계: 물리 {_fixedTotalMs:F0}ms(최대 {_fixedMaxMs:F1}ms/{_maxFixedSteps}스텝) / " +
                $"Update {_updateTotalMs:F0}ms(최대 {_updateMaxMs:F1}) / " +
                $"LateUpdate {_lateTotalMs:F0}ms(최대 {_lateMaxMs:F1}) / 사이 최대 {_gapMaxMs:F1}ms. " +
                $"구간 상위: {top}. " +
                $"미계측(로직−구간합) 창 총 {System.Math.Max(0.0, _updateTotalMs + _lateTotalMs - sectionSum):F0}ms. " +
                $"{worst}. " +
                $"성장 관측: 힙 {heapBytes / 1048576.0:F1}MB, GC 이번창 gen0 +{d0}/gen1 +{d1}/gen2 +{d2} " +
                $"(누적 {gc0}/{gc1}/{gc2}), 폰트아틀라스 재구성 이번창 {_fontAtlasRebuildsWindow}회/" +
                $"누적 {_fontAtlasRebuildsTotal}회(마지막 {_fontAtlasWidth}x{_fontAtlasHeight}), " +
                $"구간스택 넘침 {_sectionOverflowCount}회.");
            // ★ 읽는 법은 이 클래스 문서의 "[스톨구간] 읽는 법" 절에 있다. 예전에는 그 설명 4문장이
            //   **로그 줄마다** 실려 나갔다(줄당 1,272B로 Player.log 바이트 1위). 설명은 매번 같으므로
            //   소스에 한 번 두면 되고, 로그에는 매번 다른 숫자만 남긴다 — 실측 로그 증가량 153 B/s를
            //   줄이는 가장 값싼 한 수다(2026-09-02 로그 감량 라운드).
        }

        private static string DescribeSection(int i)
            => $"{SectionNames[i]} {SectionWindowMs[i]:F0}ms/{SectionWindowCount[i]}회(평균 " +
               $"{(SectionWindowCount[i] > 0 ? SectionWindowMs[i] / SectionWindowCount[i] : 0.0):F2} 최대 " +
               $"{SectionWindowMaxMs[i]:F1}, 생애 {SectionLifeMs[i]:F0}ms/{SectionLifeCount[i]}회 최대 " +
               $"{SectionLifeMaxMs[i]:F1})";

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

    /// <summary>
    /// 이름 있는 로직 구간. <b>추가는 자유롭지만 순서를 바꾸지 마라</b> — 실기 로그를 시간순으로
    /// 비교할 때 이름이 곧 열쇠이므로, 이름과 뜻이 한 번 나가면 그대로 유지되어야 한다.
    /// </summary>
    public enum StallSection
    {
        /// <summary>StickmanAgent.Update — 상태머신 Tick + 발판 폴링 + 포즈/물리 재적용.</summary>
        Agent = 0,
        /// <summary>연출 감독들(Interaction/*Director, DragThrow, 히트박스)의 Update.</summary>
        Directors,
        /// <summary>연출 렌더러들(Interaction/*Renderer)의 LateUpdate.</summary>
        Renderers,
        /// <summary>정보창/설정창/팝오버/톱니/포스트잇 등 uGUI 창들의 Update·LateUpdate.</summary>
        UiWindows,
        /// <summary>초상화 오프스크린 촬영장(도형 재빌드 + 호흡).</summary>
        Portrait,
        /// <summary>액세서리/외형 도형 재구성(CharacterAccessoryRenderer).</summary>
        Accessory,
        /// <summary>대사 표시(DialogueBubbleRenderer).</summary>
        Dialogue,
        /// <summary>★ 세이브 파일 동기 쓰기(fsync + File.Replace). Debug.Log가 아니라 "로그" 항목에 안 잡힌다.</summary>
        Save,
        /// <summary>플랫폼 오버레이 상태 유지(Mac/Win Enforcer, 합성 프로브).</summary>
        PlatformEnforcer,
        /// <summary>독(Dock) 물리 스텝.</summary>
        DockPhysics,
        /// <summary>열거값 개수. 배열 크기로만 쓴다.</summary>
        Count,
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
