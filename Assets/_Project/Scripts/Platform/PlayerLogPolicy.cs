using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// **Player.log 쓰기 비용 정책** — 정보성 로그의 스택트레이스를 릴리즈에서 끈다
    /// (2026-09-01 스파이크 라운드, 후보 B "파일 IO").
    ///
    /// ============================================================================
    /// 코드로 확인한 사실(추측 아님)
    /// ============================================================================
    /// <c>ProjectSettings/ProjectSettings.asset</c>:
    /// <code>m_StackTraceTypes: 010000000100000001000000010000000100000001000000</code>
    /// = int32 6칸이 전부 <c>1</c> = <see cref="StackTraceLogType.ScriptOnly"/>.
    /// 즉 <b>Log/Warning을 포함한 모든 로그 종류가 스택트레이스를 캡처</b>하고 있었다.
    /// 직전 z-order 라운드도 같은 사실을 지적했다("로그 한 줄이 ... 스택트레이스가 켜진 Debug.Log +
    /// Player.log 동기 쓰기를 한다").
    ///
    /// 이 앱은 던지기 한 번에 <c>[DragThrowState]/[던지기회전]/[착지먼지]/[발판변경]/[무릎앉아]/
    /// [착지충격]</c> 등 8줄 이상을 쏟는다. 정보성 로그의 스택트레이스는 그 자체로 낭비다 —
    /// 어느 코드가 찍었는지는 <b>태그가 이미 말해주기 때문이다</b>(이 프로젝트는 모든 로그에
    /// <c>[태그]</c>를 붙이는 컨벤션을 지킨다).
    ///
    /// ============================================================================
    /// 무엇을 끄고 무엇을 남기는가 — 절충
    /// ============================================================================
    /// <list type="bullet">
    /// <item><b>끈다</b>: <see cref="LogType.Log"/>, <see cref="LogType.Warning"/></item>
    /// <item><b>남긴다</b>: <see cref="LogType.Error"/>, <see cref="LogType.Exception"/>,
    ///       <see cref="LogType.Assert"/> — 스택 없이는 원격 예외 추적이 불가능하다.</item>
    /// </list>
    /// 이 절충이 없으면 "로그가 싸졌지만 사고가 나도 못 고치는" 상태가 된다.
    ///
    /// ============================================================================
    /// 왜 ProjectSettings가 아니라(도) 런타임인가
    /// ============================================================================
    /// 빌드 스크립트(<c>Assets/Editor/BuildStandalone.cs</c>의 <c>ConfigureLogStackTraces</c>)가
    /// <c>PlayerSettings.SetStackTraceLogType</c>으로 프로젝트 기본값도 함께 내리지만, 그 API는
    /// <see cref="LogType"/> 열거로 지정하므로 <b>YAML 바이트 순서를 추측하지 않아도 된다</b>.
    /// 그래도 런타임에서 한 번 더 강제하는 이유는 두 가지다:
    ///   (1) 다른 경로(에디터 UI/다른 빌드 스크립트)로 만든 빌드에서도 정책이 지켜진다.
    ///   (2) <see cref="Configure"/>로 <b>되돌릴 수 있는 스위치</b>가 된다 — 회귀 조사 때
    ///       <c>StickConfig.suppressInfoLogStackTraces=false</c>면 예전 거동으로 즉시 복귀한다.
    ///
    /// 에디터에서는 아무것도 하지 않는다: 콘솔 더블클릭으로 소스로 점프하는 기능이 스택트레이스에
    /// 의존하므로, 개발 편의를 릴리즈 최적화 때문에 깎을 이유가 없다.
    /// </summary>
    public static class PlayerLogPolicy
    {
        private static bool _applied;
        private static bool _suppressing;

        /// <summary>
        /// ★ 2026-09-02 — <b>스냅샷이 아니라 설정 자산 참조를 들고 있는다.</b>
        ///
        /// <para>예전에는 <see cref="Configure"/>가 <c>bool</c> 하나를 복사해 뒀는데, 그 함수의
        /// 호출처가 <c>Platform/FootholdPoller.cs</c> 생성자 <b>단 한 곳</b>이라 값이 부팅 시점에
        /// 얼어붙었다. 그래서 개발자 도구의 진단 로그 토글(<c>AppControlDirector</c>가
        /// <c>StickConfig.verboseDiagnosticsLogging</c>을 런타임에 뒤집는다)이
        /// <b>이 스위치에만 도달하지 못했다</b> — 실측: 토글을 켜고 3분을 기다려도
        /// <c>[유휴동작]</c> 0줄인데 같은 3분에 <c>[발판리포트] 84줄 / [눈추적] 106줄</c>이 쏟아졌다.</para>
        ///
        /// <para>그 두 태그가 멀쩡했던 이유는 <c>MacOverlayStateEnforcer.VerboseDiagnostics</c>가
        /// <b>설정을 매번 읽기</b> 때문이다. 여기도 같은 방식으로 맞춘다 — 스위치의 단일 진실 원천은
        /// <c>StickConfig</c> 자산 하나이고, 복사본을 만들지 않는다.</para>
        /// </summary>
        private static Core.StickConfig _config;

        /// <summary>
        /// **상시 반복되는 동작 서술 로그**를 낼지 여부(= <c>StickConfig.verboseDiagnosticsLogging</c>).
        ///
        /// <para><b>실측 근거</b>(2026-09-01, 실제 Player.log 71.5분 세션 2,564줄 전수 집계):</para>
        /// <code>
        ///   662 [유휴동작]   <- 그 중 661줄이 **글자 하나 다르지 않은 같은 문장**이다
        ///   492 [MacWindowService](오버레이 원점 갱신)
        ///   415 [말풍선]
        ///   143 [프레임시간] / 143 [발판변경] / 127 [Dock계단] ...
        /// </code>
        /// <c>[유휴동작]</c> 한 태그가 전체의 26%이고, 내용은 "주위 살피기 재생 — 상태=Idle"의 반복이다.
        /// 정보량이 0인 줄에 스택 캡처 + 동기 파일 쓰기를 붙이는 것은 명백한 낭비다.
        ///
        /// <para><b>왜 이 태그만 끄는가</b>: <c>[말풍선]</c>과 상태 전이 로그(<c>[벽타기]/[뛰어내리기]</c>
        /// 등)는 CLAUDE.md 불변 원칙 1(행동-텍스트 싱크)을 원격에서 검증하는 유일한 수단이라 남긴다.
        /// 반복률이 압도적으로 높으면서 정보량이 없는 것만 끈다 — "로그를 줄인다"가 "눈을 감는다"가
        /// 되면 안 된다. 이 프로젝트는 2026-08-28에 같은 판단을 이미 한 적이 있고
        /// (<c>verboseDiagnosticsLogging</c> 도입), 이건 그 스위치의 재사용이다.</para>
        /// </summary>
        public static bool RoutineNarrationEnabled
            // Unity Object의 == null은 파괴된 오브젝트까지 잡아 주므로 의도적으로 그대로 쓴다.
            // 설정이 아직 배선되지 않은 기동 직후에는 true = "로그를 잃지 않는다"가 기본값이다.
            => _config == null || _config.verboseDiagnosticsLogging;

        /// <summary>현재 정보성 로그(Log/Warning)의 스택트레이스가 꺼져 있는가(진단 로그가 함께 찍는다).</summary>
        public static bool InfoStackTracesSuppressed => _suppressing;

        /// <summary>
        /// 설정이 로드되기 <b>전</b>(= 기동 직후 첫 로그들)에도 정책이 걸리도록 기본값으로 한 번 건다.
        /// 씬 로드 전에 실행되므로 <see cref="Core.StickConfig"/>는 아직 없다 — 기본값(끄기)을 쓰고,
        /// 설정이 로드되면 <see cref="Configure"/>가 덮어쓴다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyDefault()
        {
            Apply(true);
        }

        /// <summary>StickConfig가 배선된 뒤 실제 설정값으로 다시 건다(Platform/FootholdPoller.cs가 부른다).</summary>
        public static void Configure(Core.StickConfig config)
        {
            _config = config;
            Apply(config == null || config.suppressInfoLogStackTraces);
        }

        /// <summary>테스트가 상태를 되돌릴 때 쓴다.</summary>
        public static void ResetForTests()
        {
            _applied = false;
            _suppressing = false;
            _config = null;
        }

        private static void Apply(bool suppress)
        {
            if (_applied && _suppressing == suppress) return;
            _applied = true;
            _suppressing = suppress;

#if !UNITY_EDITOR
            StackTraceLogType info = suppress ? StackTraceLogType.None : StackTraceLogType.ScriptOnly;
            Application.SetStackTraceLogType(LogType.Log, info);
            Application.SetStackTraceLogType(LogType.Warning, info);

            // 아래 셋은 **절대 끄지 않는다** — 원격 예외 추적의 유일한 수단이다.
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
#endif
        }
    }
}
