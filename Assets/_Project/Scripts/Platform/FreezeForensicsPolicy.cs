using System;
using System.Globalization;
using System.Text;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 — <b>하드 프리즈에서 살아남는 계측</b>의 판정·형식·상한. 플랫폼 중립, 순수 함수.
    ///
    /// ============================================================================
    /// 왜 생겼나 — 사용자 신고와 증거의 공백
    /// ============================================================================
    /// 사용자 신고(Windows): <i>"멀티모니터를 사용중 멀티모니터 분리시 하얀화면에 캐릭터만 보이고 완전 멈춤"</i>,
    /// 이어서 <i>"아예 컴퓨터가 멈춰서 리부팅해야 했음"</i>. 커서도 Ctrl+Alt+Del도 죽었고,
    /// StickMate를 끈 상태로는 같은 분리가 무사했다.
    ///
    /// <para>그런데 우리 계측(<c>[스톨귀인]</c>/<c>[프레임스파이크]</c>)은 <b>메인 스레드가 돌아온 다음
    /// 프레임</b>에만 기록한다 — 영구 정지는 원리상 한 줄도 못 남긴다. 게다가 Player.log 줄에는 시각이 없고,
    /// 전원을 끄면 파일 캐시에만 있던 꼬리가 사라질 수 있다. 그래서 두 장치를 둔다:</para>
    /// <list type="number">
    ///   <item><b>사건 원장</b> — 스왑체인/창 표면을 건드리는 호출 <b>직전</b>에 한 줄을 쓰고
    ///     <c>Flush(true)</c>로 디스크까지 밀어낸다(<see cref="ReservedBarRestoreLedger"/>의 write-ahead와
    ///     같은 사고방식). 벽시계 UTC가 붙으므로 이벤트 뷰어의 Kernel-Power 41 / 6008 시각과 맞출 수 있다.</item>
    ///   <item><b>워치독 스레드</b> — 메인 스레드 프레임 카운터를 본다. <b>정상일 때는 디스크에 아무것도
    ///     쓰지 않는다.</b> 메인이 멈추면 그때만, 그리고 토폴로지 변화 감지 후 짧은 창 동안만 하트비트를 쓴다.
    ///     하트비트가 어느 순간 끊겼는가가 곧 "시스템 전체가 언제 죽었나"다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 파일에 무엇이 있고 무엇이 없는가
    /// ============================================================================
    /// 있는 것: 언제 쓰는가 / 즉시 쓰는가 맥락으로만 두는가 / 줄 형식 / 파일 슬롯 선택 / 상한 / 정지 판정 상태기계.
    /// 없는 것: 파일 IO(<see cref="FreezeForensicsLog"/>), 스레드(<see cref="FreezeWatchdog"/>),
    /// 플랫폼 호출(각 Enforcer). Unity API도 부르지 않는다 — 워치독 스레드에서 쓰이는 코드가 섞여 있다.
    ///
    /// <para>★ <b>계측은 동작을 바꾸지 않는다</b>: 재적합 순서·디바운스·수명 상한·SetResolution 조건은
    /// 이 장치가 한 비트도 건드리지 않는다(완화 장치 <see cref="DisplayChangeRenderHold"/>는 별개 부품이다).</para>
    /// </summary>
    public static class FreezeForensicsPolicy
    {
        /// <summary>로그/파일에 쓰는 태그. 사용자가 Player.log에서 이 이름으로 폴더 위치를 찾는다.</summary>
        public const string LogTag = "[동결기록]";

        /// <summary><c>Application.persistentDataPath</c> 아래 하위 폴더 이름.</summary>
        public const string DirectoryName = "FreezeForensics";

        /// <summary>메인 스레드가 이만큼 한 프레임도 진행하지 않으면 정지로 본다(초).
        /// 리더 지시값. 트레이 메뉴(모달 루프)를 3초 넘게 열어 두면 정직하게 한 번 찍힌다 — 그 줄의
        /// "열린 구간"이 곧바로 그 사실을 말해 준다.</summary>
        public const double StallThresholdSeconds = 3.0;

        /// <summary>워치독 폴링 주기(초). 하트비트 1초 간격보다 짧아야 한다.</summary>
        public const double WatchdogPollSeconds = 0.5;

        /// <summary>워치독 <b>자신의</b> 연속 두 폴링 사이가 이보다 벌어지면 프로세스 전체가 멈췄던 것
        /// (절전/최대 절전)으로 보고 기준을 다시 잡는다. 이게 없으면 절전 복귀마다 거짓 정지 줄이 찍힌다.</summary>
        public const double WatchdogClockJumpSeconds = 2.0;

        /// <summary>토폴로지 변화 감지(t0) 이후 하트비트를 쓰는 창(초). 리더 지시값.</summary>
        public const double HeartbeatWindowSeconds = 60.0;

        /// <summary>하트비트 간격(초). 리더 지시값.</summary>
        public const double HeartbeatIntervalSeconds = 1.0;

        /// <summary>
        /// 사건 창(초). 사건을 여는 기록(<see cref="OpensIncident"/>) 뒤 이 시간 동안은 적합 기록도 <b>즉시</b> 디스크에 쓴다.
        /// 하트비트 창과 같은 값이다 — "사건 뒤 워치독이 지켜보는 동안"과 "메인 스레드 기록을 즉시 쓰는 동안"이
        /// 갈라지면 한쪽 파일에만 줄이 남는 구간이 생긴다.
        /// </summary>
        public const double IncidentWindowSeconds = HeartbeatWindowSeconds;

        /// <summary>사건 밖의 적합 기록을 메모리에만 들고 있는 줄 수(채널별). 사건이 열리면 먼저 내려 쓴다.</summary>
        public const int ContextBufferLines = 16;

        /// <summary>맥락으로 늦게 내려 쓴 줄에 붙는 표시. 그 줄의 시각은 원래 시각이지만 <b>그때 디스크에 없었다</b>.</summary>
        public const string ContextMarker = "※맥락(기록 당시 디스크에 쓰지 않음 — 사건 밖)";

        /// <summary>즉시 쓴 줄에 붙는 "앞 기록의 쓰기+flush 소요" 표시의 머리말. 계측이 현상을 건드린 크기다.</summary>
        public const string PreviousWriteCostLabel = "직전기록=";

        /// <summary>파일 하나의 줄 상한. 넘으면 다음 슬롯 파일로 넘어간다.</summary>
        public const int MaxLinesPerFile = 2000;

        /// <summary>파일 하나의 바이트 상한.</summary>
        public const long MaxBytesPerFile = 512L * 1024L;

        /// <summary>채널별 슬롯 파일 수(링). 삭제하지 않고 <b>가장 오래된 슬롯을 덮어쓴다</b> — 이 저장소의
        /// 프로덕션 코드에는 파일 삭제가 한 건도 없고 감사가 그 0건을 지킨다(원칙 3).</summary>
        public const int SlotCount = 12;

        /// <summary>한 세션이 채널별로 차지할 수 있는 슬롯 수. 사건 폭주 세션이 <b>이전 세션들의 기록을 전부
        /// 덮어쓰는 것</b>을 막는다. 다 차면 자기 슬롯 안에서만 돈다(최신 기록은 항상 남는다).</summary>
        public const int MaxSlotsPerSession = 4;

        /// <summary>상세 문자열 최대 길이(줄 하나가 파일 상한을 혼자 먹지 않게).</summary>
        public const int MaxDetailChars = 600;

        /// <summary>이 실행에서 계측을 켤 것인가. 에디터(Play/테스트)에서는 켜지 않는다 —
        /// 개발자 디스크에 매 플레이마다 파일이 쌓이고, 도메인 리로드가 스레드를 끊는다.</summary>
        public static bool ShouldActivate(bool isEditor, bool isDesktopPlayer) => !isEditor && isDesktopPlayer;

        /// <summary>채널·슬롯 → 파일 이름. 두 자리 고정이라 탐색기에서 이름순이 곧 슬롯순이다.</summary>
        public static string SlotFileName(FreezeForensicsChannel channel, int slot)
            => (channel == FreezeForensicsChannel.Watchdog ? "freeze-watchdog-" : "freeze-forensics-")
               + slot.ToString("00", CultureInfo.InvariantCulture) + ".log";

        /// <summary>
        /// 이 기록을 사건 창을 <b>여는</b> 것으로 볼 것인가. 화면 구성 변화 감지 / 메인 스레드 정지 / 화면 변경 유예 시작.
        /// </summary>
        public static bool OpensIncident(FreezeForensicsEvent kind)
            => kind == FreezeForensicsEvent.TopologyChangeDetected
               || kind == FreezeForensicsEvent.MainThreadStall
               || kind == FreezeForensicsEvent.RenderHoldStarted;

        /// <summary>
        /// ★ verify-change (4) — 즉시 디스크에 쓸 것인가, 메모리 맥락 링에만 둘 것인가.
        /// <para>사건 계열(여는 기록·안정·원복·재개·하트비트·유예 끝·적합 보류)은 <b>언제나 즉시</b>.
        /// 적합 쓰기(SetResolution·창 크기·위치·투명 재대입)는 <b>사건 창 안에서만 즉시</b>이고 밖이면 맥락이다 —
        /// 기동 첫 적합이 매 실행 파일을 만들어 12슬롯 이력을 약 12회 실행 만에 밀어내던 것을 여기서 막는다.</para>
        /// <para>대가(정직하게): 사건 창 밖의 적합(예: 사용자가 설정에서 표시 모니터를 바꾼 직후) 도중 시스템이 죽으면
        /// 그 줄은 디스크에 없다. 이번 신고의 경로(모니터 분리)는 적합보다 t0가 먼저 오므로 해당하지 않는다.</para>
        /// </summary>
        public static ForensicsRetention ClassifyRetention(FreezeForensicsEvent kind, bool incidentWindowOpen)
        {
            switch (kind)
            {
                case FreezeForensicsEvent.SetResolution:
                case FreezeForensicsEvent.WindowResize:
                case FreezeForensicsEvent.WindowMove:
                case FreezeForensicsEvent.TransparencyReassign:
                    return incidentWindowOpen ? ForensicsRetention.Immediate : ForensicsRetention.ContextOnly;
                default:
                    return ForensicsRetention.Immediate;
            }
        }

        /// <summary>
        /// 다음에 쓸 슬롯을 고른다. (1) 없는 슬롯이 있으면 번호가 가장 작은 것,
        /// (2) 전부 있으면 마지막 쓰기 시각이 가장 오래된 것(동률이면 번호가 작은 것).
        /// <paramref name="excluded"/>는 고르지 않는다(이번 세션이 이미 쓴 슬롯).
        /// 고를 것이 없으면 -1.
        /// </summary>
        /// <remarks>
        /// ★ R-7(2026-09-14) 판단 — 단일 인스턴스 잠금이 없어 두 인스턴스가 같은 폴더에 쓸 수 있다.
        /// "방금 쓰인 슬롯은 피한다" 규칙을 따로 두지 않는다: <b>가장 오래된 슬롯</b>을 고르는 이 규칙이 이미
        /// 다른 인스턴스가 쓰는 중인(= 가장 최근에 쓰인) 슬롯을 고르지 않는다(검토 중 추가했다가 판정을 한 번도
        /// 바꾸지 못하는 죽은 규칙임을 확인하고 뺐다). 남는 위험은 두 인스턴스가 <b>같은 순간</b> 같은 슬롯을
        /// 고르는 경합뿐이고, 그때도 <see cref="FormatLine(in FreezeForensicsRecord, int)"/>가 줄마다 pid를 실어
        /// 어느 줄이 누구 것인지 갈린다.
        /// </remarks>
        public static int ChooseSlot(bool[] exists, DateTime[] lastWriteUtc, bool[] excluded)
        {
            if (exists == null || lastWriteUtc == null) return -1;
            int n = Math.Min(exists.Length, lastWriteUtc.Length);
            for (int i = 0; i < n; i++)
            {
                if (IsExcluded(excluded, i)) continue;
                if (!exists[i]) return i;
            }

            int best = -1;
            for (int i = 0; i < n; i++)
            {
                if (IsExcluded(excluded, i)) continue;
                if (best < 0 || lastWriteUtc[i] < lastWriteUtc[best]) best = i;
            }
            return best;
        }

        private static bool IsExcluded(bool[] excluded, int i) => excluded != null && i < excluded.Length && excluded[i];

        /// <summary>줄 하나를 더 써도 파일 상한 안인가.</summary>
        public static bool FitsBudget(int linesSoFar, long bytesSoFar, int nextLineBytes, int maxLines, long maxBytes)
            => linesSoFar + 1 <= maxLines && bytesSoFar + nextLineBytes <= maxBytes;

        /// <summary>
        /// 토폴로지 감시기의 관측 한 번을 사건으로 분류한다(<see cref="DisplayTopologyWatcher"/>의
        /// <c>IsSettling</c> 전/후와 <c>Observe</c> 반환값만 본다 — 감시기 자체는 건드리지 않는다).
        /// </summary>
        public static TopologyForensicsTransition ClassifyTopologyTransition(bool wasSettling, bool isSettling, bool fired)
        {
            if (fired) return TopologyForensicsTransition.Settled;
            if (!wasSettling && isSettling) return TopologyForensicsTransition.ChangeDetected;
            if (wasSettling && !isSettling) return TopologyForensicsTransition.Reverted;
            return TopologyForensicsTransition.None;
        }

        /// <summary>
        /// 투명 재적용을 원장에 적을 것인가. <b>창 사각형(= 스왑체인/리디렉션 표면)을 바꾸는 경우만</b> 적는다.
        /// Windows는 네이티브 <c>SetBorderless</c>가 <c>SetWindowPos</c>로 폭을 흔드는 경로에서만 참이고,
        /// macOS의 재대입은 <c>styleMask</c> 한 줄이라 프레임을 건드리지 않으므로 거짓이다
        /// (근거는 <see cref="OverlayStateReapplyPolicy"/> 클래스 문서). UI 표면을 열 때마다 재적용이
        /// 다시 무장되므로, 무조건 적으면 24시간 상주 앱의 원장이 잡음으로 찬다.
        /// </summary>
        public static bool ShouldRecordTransparencyReassign(bool causesWindowResize) => causesWindowResize;

        /// <summary>
        /// 이번 폴링에 하트비트를 쓸 것인가. t0(<paramref name="episodeStartSeconds"/>)가 없으면(음수) 절대 쓰지
        /// 않는다 — 정상 상주 중 디스크 쓰기 0이 이 규칙의 핵심이다.
        /// </summary>
        public static bool ShouldWriteHeartbeat(double nowSeconds, double episodeStartSeconds,
            double lastHeartbeatSeconds, double windowSeconds, double intervalSeconds)
        {
            if (episodeStartSeconds < 0.0) return false;
            if (nowSeconds < episodeStartSeconds) return false;
            if (nowSeconds - episodeStartSeconds > windowSeconds) return false;
            if (lastHeartbeatSeconds >= episodeStartSeconds && nowSeconds - lastHeartbeatSeconds < intervalSeconds)
                return false;
            return true;
        }

        /// <summary>한 줄 형식(pid 모름). <b>줄바꿈을 절대 포함하지 않는다</b>(상세 문자열은 정리한다).</summary>
        public static string FormatLine(in FreezeForensicsRecord r) => FormatLine(r, 0);

        /// <summary>
        /// 한 줄 형식. ★ R-7 — <b>줄마다 pid</b>를 싣는다. 단일 인스턴스 잠금이 없어 두 인스턴스가 같은 폴더에
        /// 쓸 수 있고, 그때도 어느 줄이 어느 프로세스 것인지 갈린다(0 이하 = 모름).
        /// </summary>
        public static string FormatLine(in FreezeForensicsRecord r, int processId)
        {
            var sb = new StringBuilder(170);
            sb.Append(r.UtcTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            sb.Append(" | ").Append(r.Kind.ToString());
            sb.Append(" | pid=");
            if (processId > 0) sb.Append(processId.ToString(CultureInfo.InvariantCulture));
            else sb.Append('?');
            sb.Append(" | rt=");
            if (r.RealtimeSinceStartup >= 0.0) sb.Append(r.RealtimeSinceStartup.ToString("F3", CultureInfo.InvariantCulture));
            else sb.Append('?');
            sb.Append(" | frame=");
            if (r.Frame >= 0) sb.Append(r.Frame.ToString(CultureInfo.InvariantCulture));
            else sb.Append('?');
            sb.Append(" | monitors=");
            if (r.MonitorCount >= 0) sb.Append(r.MonitorCount.ToString(CultureInfo.InvariantCulture));
            else sb.Append('?');
            if (r.HasRect)
            {
                sb.Append(" | rect=(")
                  .Append(r.X.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.Y.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(r.Width.ToString("0.##", CultureInfo.InvariantCulture)).Append('x')
                  .Append(r.Height.ToString("0.##", CultureInfo.InvariantCulture)).Append(')');
            }
            string detail = SanitizeDetail(r.Detail);
            if (detail.Length > 0) sb.Append(" | ").Append(detail);
            return sb.ToString();
        }

        /// <summary>상세 문자열에서 줄바꿈·탭을 공백으로 바꾸고 길이를 자른다.</summary>
        public static string SanitizeDetail(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return string.Empty;
            var sb = new StringBuilder(Math.Min(detail.Length, MaxDetailChars));
            for (int i = 0; i < detail.Length && sb.Length < MaxDetailChars; i++)
            {
                char c = detail[i];
                sb.Append(c == '\r' || c == '\n' || c == '\t' ? ' ' : c);
            }
            return sb.ToString();
        }

        /// <summary>메인 스레드 단계의 사람이 읽는 이름. 정지 줄에서 "로직 안인가 밖인가"를 가른다.</summary>
        public static string DescribePhase(MainThreadPhase phase)
        {
            switch (phase)
            {
                case MainThreadPhase.FixedUpdate: return "FixedUpdate(물리)";
                case MainThreadPhase.AfterFixedUpdate: return "FixedUpdate 뒤~Update 전";
                case MainThreadPhase.Update: return "Update";
                case MainThreadPhase.AfterUpdate: return "Update 뒤~LateUpdate 전";
                case MainThreadPhase.LateUpdate: return "LateUpdate";
                case MainThreadPhase.AfterLogic: return "로직 밖(렌더·프레젠트·프레임 상한 대기)";
                default: return "미관측";
            }
        }
    }

    /// <summary>원장 채널. 채널마다 파일과 잠금이 따로다(<see cref="FreezeForensicsLog"/> 문서).</summary>
    public enum FreezeForensicsChannel
    {
        /// <summary>메인 스레드 사건(토폴로지·적합·유예).</summary>
        Main = 0,
        /// <summary>워치독 스레드 줄(정지·재개·하트비트).</summary>
        Watchdog = 1,
    }

    /// <summary>기록을 어디에 둘 것인가.</summary>
    public enum ForensicsRetention
    {
        /// <summary>즉시 디스크에 쓰고 flush.</summary>
        Immediate = 0,
        /// <summary>메모리 맥락 링에만 둔다(사건이 열리면 먼저 내려 쓴다).</summary>
        ContextOnly = 1,
    }

    /// <summary>원장 한 줄의 종류. <b>순서를 바꾸지 마라</b> — 이름이 곧 파일의 열쇠다(추가는 끝에).</summary>
    public enum FreezeForensicsEvent
    {
        SessionHeader = 0,
        TopologyChangeDetected,
        TopologySettled,
        TopologyReverted,
        SetResolution,
        WindowResize,
        WindowMove,
        TransparencyReassign,
        MainThreadStall,
        MainThreadResumed,
        Heartbeat,
        /// <summary>화면 변경 유예(렌더 제출 억제 + 재적합 보류) 시작 — <see cref="DisplayChangeRenderHold"/>.</summary>
        RenderHoldStarted,
        /// <summary>유예 끝 — 해제 사유와 억제 중 실제 렌더 제출 수(기대값과 나란히).</summary>
        RenderHoldEnded,
        /// <summary>유예 중이라 재적합을 보류했다(유예 한 번에 한 줄).</summary>
        FitDeferred,
        /// <summary>유예의 조용한 구간이 끝나 렌더 억제를 유지한 채 재적합을 허용했다.</summary>
        RenderHoldRefitAllowed,
    }

    /// <summary>토폴로지 감시기 관측 한 번의 분류.</summary>
    public enum TopologyForensicsTransition
    {
        None = 0,
        /// <summary>디바운스 대기에 처음 들어간 순간 = t0.</summary>
        ChangeDetected,
        /// <summary>안정 신호(재적합이 다시 무장된다).</summary>
        Settled,
        /// <summary>흔들렸다가 원래 구성으로 돌아와 아무 일도 없었던 것으로 처리됨.</summary>
        Reverted,
    }

    /// <summary>메인 스레드가 지금 어느 단계에 있는가(프레임 경계 탐침이 발행한다).</summary>
    public enum MainThreadPhase
    {
        Unknown = 0,
        FixedUpdate,
        AfterFixedUpdate,
        Update,
        AfterUpdate,
        LateUpdate,
        AfterLogic,
    }

    /// <summary>원장 한 줄의 값. 스레드 간에 건넬 수 있게 Unity 타입을 쓰지 않는다.</summary>
    public readonly struct FreezeForensicsRecord
    {
        public readonly FreezeForensicsEvent Kind;
        public readonly DateTime UtcTime;
        /// <summary>음수 = 모름(워치독 스레드는 Unity 시계를 부를 수 없다).</summary>
        public readonly double RealtimeSinceStartup;
        /// <summary>음수 = 모름.</summary>
        public readonly long Frame;
        /// <summary>음수 = 모름.</summary>
        public readonly int MonitorCount;
        public readonly bool HasRect;
        public readonly float X, Y, Width, Height;
        public readonly string Detail;

        public FreezeForensicsRecord(FreezeForensicsEvent kind, DateTime utcTime, double realtimeSinceStartup,
            long frame, int monitorCount, string detail)
        {
            Kind = kind;
            UtcTime = utcTime;
            RealtimeSinceStartup = realtimeSinceStartup;
            Frame = frame;
            MonitorCount = monitorCount;
            HasRect = false;
            X = Y = Width = Height = 0f;
            Detail = detail;
        }

        public FreezeForensicsRecord(FreezeForensicsEvent kind, DateTime utcTime, double realtimeSinceStartup,
            long frame, int monitorCount, float x, float y, float width, float height, string detail)
        {
            Kind = kind;
            UtcTime = utcTime;
            RealtimeSinceStartup = realtimeSinceStartup;
            Frame = frame;
            MonitorCount = monitorCount;
            HasRect = true;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Detail = detail;
        }
    }

    /// <summary>워치독 폴링 한 번의 판정.</summary>
    public enum FreezeWatchdogVerdict
    {
        None = 0,
        /// <summary>메인 스레드가 문턱 이상 진행하지 않았다(한 정지에 한 번만).</summary>
        StallStarted,
        /// <summary>정지 보고 뒤 메인 스레드가 다시 진행했다.</summary>
        StallEnded,
        /// <summary>워치독 자신의 폴링 간격이 비정상적으로 벌어져 기준을 다시 잡았다(절전 복귀 등). 쓰지 않는다.</summary>
        ClockJump,
    }

    /// <summary>
    /// 메인 스레드 정지 판정 상태기계. 워치독 스레드가 소유하고 그 스레드의 <b>지역 변수로만</b> 쓴다(잠금 없음).
    /// <para>프레임 카운터가 <b>1 이상이 되기 전에는 무장하지 않는다</b> — 프레임 경계 탐침이 아직 붙지 않은
    /// 기동 구간(무거운 Awake/Start)을 정지로 오인하지 않기 위해서다.</para>
    /// <para>★ 참조 형식이다(2026-09-14, verify-change V1과 같은 함정) — struct였다면 readonly 필드에 담는 순간
    /// 복사본에 상태가 쌓여 정지를 영원히 못 본다.</para>
    /// </summary>
    public sealed class FreezeWatchdogTracker
    {
        private bool _hasSample;
        private long _lastFrame;
        private double _lastAdvanceSeconds;
        private double _lastPollSeconds;
        private bool _stallReported;

        /// <summary>마지막으로 판정한 정지 지속(초). StallStarted/StallEnded 직후에 읽는다.</summary>
        public double StalledSeconds { get; private set; }

        public bool StallReported => _stallReported;

        public FreezeWatchdogVerdict Observe(long frame, double nowSeconds, double stallThresholdSeconds,
            double clockJumpSeconds)
        {
            if (frame < 1)
            {
                _lastPollSeconds = nowSeconds;
                return FreezeWatchdogVerdict.None;
            }

            if (!_hasSample)
            {
                _hasSample = true;
                _lastFrame = frame;
                _lastAdvanceSeconds = nowSeconds;
                _lastPollSeconds = nowSeconds;
                return FreezeWatchdogVerdict.None;
            }

            double pollGap = nowSeconds - _lastPollSeconds;
            _lastPollSeconds = nowSeconds;

            if (pollGap > clockJumpSeconds)
            {
                bool advanced = frame != _lastFrame;
                bool wasReported = _stallReported;
                StalledSeconds = nowSeconds - _lastAdvanceSeconds;
                _lastFrame = frame;
                _lastAdvanceSeconds = nowSeconds;
                if (wasReported && advanced)
                {
                    _stallReported = false;
                    return FreezeWatchdogVerdict.StallEnded;
                }
                return FreezeWatchdogVerdict.ClockJump;
            }

            if (frame != _lastFrame)
            {
                StalledSeconds = nowSeconds - _lastAdvanceSeconds;
                _lastFrame = frame;
                _lastAdvanceSeconds = nowSeconds;
                if (_stallReported)
                {
                    _stallReported = false;
                    return FreezeWatchdogVerdict.StallEnded;
                }
                return FreezeWatchdogVerdict.None;
            }

            if (!_stallReported && nowSeconds - _lastAdvanceSeconds >= stallThresholdSeconds)
            {
                _stallReported = true;
                StalledSeconds = nowSeconds - _lastAdvanceSeconds;
                return FreezeWatchdogVerdict.StallStarted;
            }

            return FreezeWatchdogVerdict.None;
        }

        /// <summary>현재 진행 없는 시간(초) — 하트비트 줄용.</summary>
        public double SecondsSinceAdvance(double nowSeconds) => _hasSample ? nowSeconds - _lastAdvanceSeconds : -1.0;
    }
}
