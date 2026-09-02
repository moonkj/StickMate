namespace StickMate.Platform
{
    /// <summary>이번 판정이 무엇이었는지 사람이 읽을 수 있게 만드는 사유 코드.
    /// 로그 문장은 <see cref="ReservedBarRevealPolicy.Describe"/> 하나에서만 만들어진다 —
    /// 판정과 문장이 두 곳에서 따로 자라면 로그가 거짓말을 시작한다.</summary>
    public enum ReservedBarReason
    {
        /// <summary>자동 숨김이 원래 꺼져 있다. 아무 것도 하지 않고 <b>흔적도 남기지 않는다</b>
        /// (사용자 요구 4: "우리가 안 바꿨으면 안 건드린다").</summary>
        AlreadyVisible = 0,

        /// <summary>자동 숨김이 켜져 있어 이번 실행 동안만 해제한다. 흔적을 <b>먼저</b> 쓴다.</summary>
        RevealForSession,

        /// <summary>지난 실행이 원복하지 못하고 죽은 흔적을 발견했고, OS 상태도 실제로 어긋나 있다.
        /// 이번 실행이 시작하기 <b>전에</b> 사용자의 원래 값으로 되돌린다.</summary>
        RecoverLeftover,

        /// <summary>흔적은 남아 있지만 OS는 이미 원래 값이다(사용자가 직접 되돌렸거나 셸이 재시작됐다).
        /// 시스템에 쓰지 않고 흔적만 닫는다 — 필요 없는 전역 쓰기를 한 번도 하지 않기 위해서다.</summary>
        LeftoverAlreadyMatched,

        /// <summary>종료 시 원복. 우리가 바꿨고 OS도 아직 우리가 바꾼 값이다.</summary>
        RestoreOnQuit,

        /// <summary>종료 시점에 이미 원래 값이다. 흔적만 닫는다.</summary>
        QuitAlreadyMatched,

        /// <summary>이번 실행에서 우리가 아무 것도 바꾸지 않았다. 종료 시 할 일이 없다.</summary>
        NothingToRestore,

        /// <summary>이 플랫폼에 자동 숨김 제어 능력이 없거나 OS 조회 자체가 실패했다.
        /// <b>모르면 건드리지 않는다</b> — 추정으로 남의 설정을 쓰지 않는다.</summary>
        Unavailable,
    }

    /// <summary>
    /// 정책이 만들어 낸 <b>실행 계획</b>. 순수 데이터라 테스트가 통째로 단언할 수 있고,
    /// 실행부(<see cref="ReservedBarRevealDirector"/>)는 이 셋을 그대로 수행하기만 한다.
    ///
    /// <para><b>쓰기 순서는 계획이 아니라 실행부의 계약</b>이다: 흔적 쓰기가 항상 시스템 쓰기보다
    /// <b>먼저</b>다(아래 클래스 문서 "왜 write-ahead인가").</para>
    /// </summary>
    public readonly struct ReservedBarPlan
    {
        /// <summary>OS의 자동 숨김 비트에 쓸 것인가.</summary>
        public readonly bool WriteSystem;

        /// <summary>쓴다면 어떤 값으로. <see cref="WriteSystem"/>이 false면 의미 없음.</summary>
        public readonly bool SystemAutoHideValue;

        /// <summary>디스크에 "우리가 바꿨다" 흔적을 남길 것인가(원래 값과 함께).</summary>
        public readonly bool WriteTrace;

        /// <summary>디스크 흔적을 닫을 것인가(더 이상 복구할 것이 없다는 표시).</summary>
        public readonly bool CloseTrace;

        /// <summary>사람이 읽을 사유.</summary>
        public readonly ReservedBarReason Reason;

        public ReservedBarPlan(bool writeSystem, bool systemAutoHideValue,
            bool writeTrace, bool closeTrace, ReservedBarReason reason)
        {
            WriteSystem = writeSystem;
            SystemAutoHideValue = systemAutoHideValue;
            WriteTrace = writeTrace;
            CloseTrace = closeTrace;
            Reason = reason;
        }

        /// <summary>아무 것도 하지 않는 계획. 디스크도 시스템도 건드리지 않는다.</summary>
        public static ReservedBarPlan Idle(ReservedBarReason reason)
            => new ReservedBarPlan(false, false, false, false, reason);
    }

    /// <summary>
    /// ★★ 2026-09-02 — "작업표시줄/Dock 자동 숨김을 실행 중에만 해제하고 종료 시 원복한다"의
    /// <b>판정 규칙 전부</b>. P/Invoke도, <c>UNITY_STANDALONE_*</c> 분기도, 파일 I/O도 한 줄 없다.
    ///
    /// ============================================================================
    /// 이 파일이 존재하는 이유 (CLAUDE.md 구조 규약)
    /// ============================================================================
    /// 정책이 <c>Platform/Windows/</c> 안에 있으면 macOS가 <b>물리적으로 호출할 수 없다</b>
    /// (실제 사고: <c>FullscreenSuspendPolicy.cs</c>). macOS Dock을 언젠가 같은 방식으로 다루기로
    /// 결정하면, 그 라운드가 새로 써야 하는 것은 <b>사실 조회 한 벌</b>뿐이어야 한다 —
    /// "무엇을 해야 하는가"는 이 파일이 이미 답하고 있어야 한다.
    ///
    /// 그리고 순수 함수라서 <b>Windows가 없는 이 개발 머신의 EditMode가 규칙 자체를 검증할 수
    /// 있다</b>. 크래시 복구 경로는 실기에서 재현하기가 특히 어렵다(일부러 앱을 죽여야 한다).
    /// 규칙이 여기 있으면 "원복 못 하고 죽은 흔적"을 테스트가 손으로 만들어 넣을 수 있다.
    ///
    /// ============================================================================
    /// ★★★ 왜 write-ahead(흔적 먼저, 시스템 나중)인가 — 이 기능의 안전장치 전부
    /// ============================================================================
    /// <c>OnApplicationQuit</c>/<c>Application.quitting</c>은 <b>강제 종료·크래시에서 돌지
    /// 않는다</b>. 이 저장소가 이미 겪었다 — <c>driver.sh stop</c>의 SIGTERM에서 저장이 한 줄도
    /// 돌지 않았다. 그러면 사용자의 작업표시줄은 <b>영구히 바뀐 채</b> 남는다. 우리가 만든 설정 변경을
    /// 우리가 못 되돌리는 것은 원칙 3의 예외를 승인받은 근거("실행 중에만")를 통째로 무효화한다.
    ///
    /// 그래서 두 가지를 강제한다:
    /// <list type="number">
    ///  <item><b>바꾸기 전에 원래 값을 디스크에 적는다.</b> 흔적 쓰기와 시스템 쓰기 사이에서 죽으면
    ///        다음 실행은 "원래 자동 숨김이었다"를 복원한다 — 실제로 바꾸지 못했더라도 결과는
    ///        같은 값이라 무해하다. 순서를 뒤집으면(시스템 먼저) 그 사이의 크래시는 사용자를
    ///        <b>영구히</b> 바뀐 상태에 남긴다. 대칭이 아니다 — 한쪽만 안전하다.</item>
    ///  <item><b>다음 실행은 자기 일을 하기 전에 남의 흔적부터 갚는다.</b> 아래
    ///        <see cref="ResolveStartup"/>가 복구를 먼저 판정하고, 복구가 끝난 <b>뒤에</b>
    ///        이번 실행의 해제를 판정한다.</item>
    /// </list>
    ///
    /// <para><b>왜 "흔적이 기억하는 원래 값"을 그냥 이어받지 않는가</b>(= 물리적 복원을 건너뛰고
    /// 흔적만 계승하면 깜빡임이 없다): 그러면 <b>흔적이 틀렸을 때 영원히 틀린다</b>. 흔적은 우리가
    /// 쓴 것이고, 그 사이에 사용자가 직접 설정을 바꿨을 수 있다. 실제로 되돌려 놓고 <b>다시 읽으면</b>
    /// 그 시점의 진실에서 새로 출발한다 — 이 앱은 "캐시가 참일 것"이라고 믿었다가 이미 한 번 크게
    /// 당했다(<see cref="TopmostRestorePolicy"/> 문서의 <c>IsTopmost</c> 캐시). 대가는 크래시 직후
    /// 1회의 짧은 깜빡임뿐이다.</para>
    /// </summary>
    public static class ReservedBarRevealPolicy
    {
        /// <summary>
        /// 1단계 — <b>지난 실행이 갚지 못한 빚</b>을 먼저 처리한다. 이번 실행이 무엇을 원하는지는
        /// 아직 보지 않는다(순서를 섞으면 "복구했다"는 로그와 실제 동작이 어긋난다).
        /// </summary>
        /// <param name="hasLeftover">디스크에 <b>열린</b> 흔적이 있는가.</param>
        /// <param name="leftoverOriginalAutoHide">그 흔적이 기록한 사용자의 원래 값.</param>
        /// <param name="controlAvailable">이 플랫폼에 제어 능력이 있는가.</param>
        /// <param name="observedAutoHide">OS가 <b>지금</b> 보고하는 값(조회 성공 시).</param>
        /// <param name="observationSucceeded">그 조회가 성공했는가.</param>
        public static ReservedBarPlan ResolveRecovery(
            bool hasLeftover, bool leftoverOriginalAutoHide,
            bool controlAvailable, bool observedAutoHide, bool observationSucceeded)
        {
            if (!hasLeftover) return ReservedBarPlan.Idle(ReservedBarReason.NothingToRestore);

            // 능력이 없거나 지금 상태를 못 읽으면 흔적을 **그대로 둔다**. 여기서 흔적을 닫으면
            // 다음 실행이 복구 기회를 영영 잃는다(예: 일시적 조회 실패, 셸 재시작 중).
            if (!controlAvailable || !observationSucceeded)
            {
                return ReservedBarPlan.Idle(ReservedBarReason.Unavailable);
            }

            // 이미 원래 값이면 시스템에 쓰지 않는다 — 필요 없는 전역 쓰기를 0회로 유지한다.
            if (observedAutoHide == leftoverOriginalAutoHide)
            {
                return new ReservedBarPlan(false, false, false, true,
                    ReservedBarReason.LeftoverAlreadyMatched);
            }

            return new ReservedBarPlan(true, leftoverOriginalAutoHide, false, true,
                ReservedBarReason.RecoverLeftover);
        }

        /// <summary>
        /// 2단계 — 이번 실행의 판정. <b>복구가 끝난 뒤 다시 읽은</b> 값을 넣어야 한다.
        ///
        /// <para>자동 숨김이 꺼져 있으면 <b>시스템도 디스크도 건드리지 않는다</b>. 사용자 요구 4가
        /// 정확히 이것이다 — 원래 자동 숨김을 안 쓰는 사람에게는 이 기능이 존재하지 않는 것과
        /// 같아야 하고, 그 사람의 디스크에 우리 흔적 파일이 "열린 채" 생겨서도 안 된다.</para>
        /// </summary>
        public static ReservedBarPlan ResolveStartup(
            bool controlAvailable, bool observedAutoHide, bool observationSucceeded)
        {
            if (!controlAvailable || !observationSucceeded)
            {
                return ReservedBarPlan.Idle(ReservedBarReason.Unavailable);
            }

            if (!observedAutoHide) return ReservedBarPlan.Idle(ReservedBarReason.AlreadyVisible);

            // 흔적 먼저(WriteTrace), 시스템 나중(WriteSystem) — 실행 순서는 실행부의 계약이다.
            return new ReservedBarPlan(true, false, true, false, ReservedBarReason.RevealForSession);
        }

        /// <summary>
        /// 3단계 — 종료 시 원복.
        /// </summary>
        /// <param name="weChangedIt">이번 실행에서 실제로 시스템을 바꿨는가.</param>
        /// <param name="originalAutoHide">바꾸기 전 사용자의 값.</param>
        /// <param name="observedAutoHide">지금 OS 값(조회 성공 시).</param>
        /// <param name="observationSucceeded">그 조회가 성공했는가. <b>실패해도 원복은 시도한다</b> —
        /// 종료는 다시 오지 않는 기회이고, 같은 값을 한 번 더 쓰는 비용은 0이다.</param>
        public static ReservedBarPlan ResolveQuit(
            bool weChangedIt, bool originalAutoHide,
            bool observedAutoHide, bool observationSucceeded)
        {
            if (!weChangedIt) return ReservedBarPlan.Idle(ReservedBarReason.NothingToRestore);

            if (observationSucceeded && observedAutoHide == originalAutoHide)
            {
                return new ReservedBarPlan(false, false, false, true,
                    ReservedBarReason.QuitAlreadyMatched);
            }

            return new ReservedBarPlan(true, originalAutoHide, false, true,
                ReservedBarReason.RestoreOnQuit);
        }

        /// <summary>
        /// 로그 문장의 <b>단일 정의처</b>. 사용자가 Player.log만 보고 "무엇을 읽었고 무엇을 바꿨고
        /// 언제 되돌렸는지"를 알 수 있어야 한다(사용자 요구 5).
        /// </summary>
        public static string Describe(ReservedBarReason reason)
        {
            switch (reason)
            {
                case ReservedBarReason.AlreadyVisible:
                    return "자동 숨김이 원래 꺼져 있습니다 — 아무 것도 바꾸지 않았고 흔적 파일도 남기지 않았습니다.";
                case ReservedBarReason.RevealForSession:
                    return "자동 숨김이 켜져 있어 이번 실행 동안만 해제했습니다. 종료할 때 원래대로 되돌립니다.";
                case ReservedBarReason.RecoverLeftover:
                    return "지난 실행이 원복하지 못하고 종료된 흔적을 발견했습니다 — 사용자의 원래 설정으로 먼저 되돌렸습니다.";
                case ReservedBarReason.LeftoverAlreadyMatched:
                    return "지난 실행의 흔적이 남아 있었지만 시스템은 이미 원래 설정이었습니다 — 아무 것도 쓰지 않고 흔적만 닫았습니다.";
                case ReservedBarReason.RestoreOnQuit:
                    return "종료합니다 — 이번 실행에서 해제했던 자동 숨김을 원래대로 되돌렸습니다.";
                case ReservedBarReason.QuitAlreadyMatched:
                    return "종료합니다 — 시스템이 이미 원래 설정이라 되돌릴 것이 없어 흔적만 닫았습니다.";
                case ReservedBarReason.NothingToRestore:
                    return "이번 실행에서 시스템 설정을 바꾸지 않았습니다 — 되돌릴 것이 없습니다.";
                case ReservedBarReason.Unavailable:
                    return "이 플랫폼에는 자동 숨김 제어가 없거나 상태 조회에 실패했습니다 — 남의 설정을 추측으로 건드리지 않습니다.";
                default:
                    return "알 수 없는 사유입니다.";
            }
        }

        /// <summary>자동 숨김 값을 사람이 읽는 말로. 로그에서 true/false를 세지 않게 한다.</summary>
        public static string DescribeAutoHide(bool autoHideEnabled)
            => autoHideEnabled ? "자동 숨김 켜짐(막대가 숨어 있음)" : "자동 숨김 꺼짐(막대가 항상 보임)";
    }
}
