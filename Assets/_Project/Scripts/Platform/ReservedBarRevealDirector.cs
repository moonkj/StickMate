using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★★ 2026-09-02 — 작업표시줄/Dock <b>자동 숨김 강제 해제</b>의 실행부.
    /// 사용자 지시: <i>"일단 우리 프로그램을 실행하면 작업표시줄 숨김처리가 되어 있어도 강제로
    /// 보이게 해야함"</i> + 방식 선택 <b>"실행 중에만 + 종료 시 원복"</b>.
    /// <b>원칙 3의 승인된 예외</b>다 — 경위와 범위는 <see cref="IReservedBarAutoHideControl"/> 문서와
    /// <c>docs/TASKBAR_REVEAL.md</c>, 그리고 CLAUDE.md 원칙 3의 예외 조항에 있다.
    ///
    /// ============================================================================
    /// 이 클래스는 <b>판정을 하나도 하지 않는다</b>
    /// ============================================================================
    /// "바꿔야 하는가 / 되돌려야 하는가 / 흔적이 남아 있는가"는 전부
    /// <see cref="ReservedBarRevealPolicy"/>(플랫폼 중립·순수 함수)가 정한다. 여기 있는 것은
    /// <b>순서</b>와 <b>부작용</b>뿐이다: 흔적 먼저, 시스템 나중, 그리고 로그.
    ///
    /// ============================================================================
    /// ★ 타이밍 — 왜 <c>BeforeSceneLoad</c>인가 (리더 확인 요청 사항)
    /// ============================================================================
    /// 이 앱의 작업표시줄 <b>실측</b>(<c>Win32WindowService.TryGetReservedBottomBarOsScreen</c>)은
    /// <c>_overlayHwnd</c>가 잡히기 전에는 조기 반환한다. 사용자 실기 로그가 그 순서를 보여 준다:
    /// <c>[Dock실측] 하단 예약 막대 없음(OS 확정)</c> → (한참 뒤) <c>… 실측 … rect=(0, 1552, 2560, 48)</c>.
    ///
    /// <para>여기서 쓰는 두 호출(상태 조회/상태 쓰기)은 <b>창 핸들을 요구하지 않는다</b>. 그래서
    /// 오버레이가 생기기 전에 끝낼 수 있고, <b>끝내야 한다</b>: 해제가 첫 실측보다 늦으면
    /// "막대 없음 → 막대 있음" 전이가 <b>세션 중간</b>에 일어난다. 그 전이는 이 저장소에
    /// 이미 열려 있는 가설과 맞물린다 — Windows 작업표시줄은 화면 가로 <b>전체</b>를 점유하므로
    /// <see cref="BottomSafetyNetPolicy"/>가 뚫는 "구멍"이 안전망 전체를 덮어 좌/우 두 조각이 동시에
    /// 폭 0으로 사라지고(<c>MinPieceWidthOsPoints</c> 미달), 그 순간 안전망 위에 서 있던 캐릭터는
    /// 자기보다 <b>위</b>에 생긴 작업표시줄 발판을 낙하로 잡을 수 없다. <c>BeforeSceneLoad</c>는
    /// 씬의 어떤 <c>Awake</c>보다도 앞이라 그 전이를 세션 시작 전으로 밀어낸다.</para>
    ///
    /// <para><b>실기 미확인으로 남는 것</b>: 셸이 상태 변경을 반영해 작업 영역(<c>rcWork</c>)을
    /// 다시 계산하는 데 걸리는 시간. 그 반영이 오버레이 확보보다 늦으면 위 전이가 여전히 한 번
    /// 일어난다. 이 개발 머신에는 Windows가 없어 <b>측정하지 못했다</b>. 사용자 실기 로그로
    /// 확인해야 한다(아래 로그 두 줄의 시간 간격으로 바로 읽힌다).</para>
    ///
    /// ============================================================================
    /// 에디터에서는 절대 돌지 않는다
    /// ============================================================================
    /// <c>UNITY_STANDALONE_WIN &amp;&amp; !UNITY_EDITOR</c> — 개발자가 에디터에서 Play를 누를 때마다
    /// <b>그 사람의 실제 작업표시줄</b>이 바뀌면 안 된다. 이 가드는 <c>StickmanAgent.CreatePlatformService</c>가
    /// 쓰는 것과 같은 것이며, 그 자리의 주석에 "에디터 컴파일 컨텍스트에도 STANDALONE 심볼이 함께
    /// 정의된다"는 실측 근거가 있다.
    /// </summary>
    public static class ReservedBarRevealDirector
    {
        /// <summary>사용자가 Player.log에서 찾을 태그. 한 곳에서만 정의한다.</summary>
        public const string LogTag = "[작업표시줄]";

        private static IReservedBarAutoHideControl _control;
        private static bool _quitHookInstalled;

        /// <summary>이번 실행에서 실제로 시스템 설정을 바꿨는가. 종료 판정의 입력이자 진단용.</summary>
        public static bool ChangedThisSession { get; private set; }

        /// <summary>바꾸기 전 사용자의 원래 값(<see cref="ChangedThisSession"/>이 true일 때만 의미 있음).</summary>
        public static bool OriginalAutoHide { get; private set; }

        /// <summary>마지막 기동 판정 사유. 테스트/진단이 문자열이 아니라 이 값을 단언한다.</summary>
        public static ReservedBarReason LastStartupReason { get; private set; } = ReservedBarReason.Unavailable;

        /// <summary>마지막 종료 판정 사유.</summary>
        public static ReservedBarReason LastShutdownReason { get; private set; } = ReservedBarReason.NothingToRestore;

        /// <summary>마지막 기동에서 디스크 흔적을 어떻게 읽었는가. 복구 경로 검증의 핵심 관측점.</summary>
        public static ReservedBarLedgerState LastLedgerState { get; private set; } = ReservedBarLedgerState.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            RunStartup(new StickMate.Platform.Windows.WindowsReservedBarAutoHideControl());
#else
            // macOS/모바일/에디터: 제어 능력을 배선하지 않는다. 이 경로는 디스크에도 시스템에도
            // 한 바이트도 쓰지 않는다 — macOS Dock을 같은 방식으로 다룰지는 아직 결정되지 않았고,
            // 그 판단 근거는 docs/TASKBAR_REVEAL.md 3절에 있다(별도 배정).
            RunStartup(null);
#endif
        }

        /// <summary>
        /// 기동 경로 — 테스트가 가짜 제어기를 주입해 크래시 복구까지 통째로 재현할 수 있도록 공개한다.
        /// <b>순서가 계약이다</b>: (1) 지난 실행의 빚을 갚고 → (2) 상태를 <b>다시 읽고</b> →
        /// (3) 이번 실행을 판정한다.
        /// </summary>
        public static void RunStartup(IReservedBarAutoHideControl control)
        {
            _control = control;
            ChangedThisSession = false;
            OriginalAutoHide = false;

            bool available = control != null;
            string tag = available ? control.PlatformTag : "(없음)";

            // ---------- 0. 디스크 흔적 읽기 ----------
            ReservedBarLedgerState ledger = ReservedBarRestoreLedger.Read(tag, out ReservedBarRestoreTrace trace);
            LastLedgerState = ledger;

            if (ledger == ReservedBarLedgerState.NewerSchema || ledger == ReservedBarLedgerState.ForeignPlatform)
            {
                // 우리가 해석하거나 갚을 수 있는 흔적이 아니다. 손대지 않고 이번 실행도 쉰다 —
                // 그래야 그 흔적의 주인(신버전/다른 OS)이 나중에 정상적으로 갚을 수 있다.
                LastStartupReason = ReservedBarReason.Unavailable;
                Debug.LogWarning($"{LogTag} 원복 흔적이 이 빌드의 것이 아닙니다(상태={ledger}, " +
                    $"기록 플랫폼={trace?.platform ?? "?"}, 기록 버전={trace?.version ?? 0}, " +
                    $"이 빌드 버전={ReservedBarRestoreLedger.CurrentVersion}). " +
                    "그 흔적을 그대로 보존하고 이번 실행에서는 시스템 설정을 바꾸지 않습니다.");
                InstallQuitHook();
                return;
            }

            bool hasLeftover = ledger == ReservedBarLedgerState.Open;
            bool leftoverOriginal = hasLeftover && trace != null && trace.originalAutoHide;

            // ---------- 1. 관측 ----------
            bool observed = false;
            bool observationOk = available && control.TryReadAutoHide(out observed);

            if (available)
            {
                Debug.Log($"{LogTag} 시작 — 지금 상태를 읽었습니다: " +
                    (observationOk
                        ? ReservedBarRevealPolicy.DescribeAutoHide(observed)
                        : "조회 실패") +
                    $". 디스크 흔적={DescribeLedger(ledger)}" +
                    (hasLeftover
                        ? $"(지난 실행이 기록한 원래 값: {ReservedBarRevealPolicy.DescribeAutoHide(leftoverOriginal)})"
                        : string.Empty) +
                    $". 흔적 파일={ReservedBarRestoreLedger.FilePath}");
            }

            // ---------- 2. 지난 실행의 빚 갚기 ----------
            ReservedBarPlan recovery = ReservedBarRevealPolicy.ResolveRecovery(
                hasLeftover, leftoverOriginal, available, observed, observationOk);

            if (recovery.Reason != ReservedBarReason.NothingToRestore)
            {
                bool systemOk = true;
                if (recovery.WriteSystem)
                {
                    systemOk = control.TrySetAutoHide(recovery.SystemAutoHideValue);
                }

                if (recovery.CloseTrace && systemOk)
                {
                    ReservedBarRestoreLedger.Close(leftoverOriginal, tag);
                }

                if (recovery.WriteSystem)
                {
                    if (systemOk)
                    {
                        Debug.Log($"{LogTag} ★ 복구 — {ReservedBarRevealPolicy.Describe(recovery.Reason)} " +
                            $"되돌린 값: {ReservedBarRevealPolicy.DescribeAutoHide(recovery.SystemAutoHideValue)}.");
                    }
                    else
                    {
                        Debug.LogWarning($"{LogTag} ★ 복구 실패 — 지난 실행의 흔적을 발견했지만 " +
                            $"{ReservedBarRevealPolicy.DescribeAutoHide(recovery.SystemAutoHideValue)}(으)로 " +
                            "되돌리지 못했습니다. 흔적을 닫지 않고 남겨 다음 실행이 다시 시도합니다.");
                    }
                }
                else if (recovery.CloseTrace)
                {
                    Debug.Log($"{LogTag} {ReservedBarRevealPolicy.Describe(recovery.Reason)}");
                }
                else if (hasLeftover)
                {
                    // 갚아야 할 빚이 있는데 지금은 갚을 수 없다. **반드시 소리를 내야 한다** —
                    // 조용히 지나가면 사용자의 설정이 바뀐 채로 남았다는 사실을 아무도 모른다.
                    Debug.LogWarning($"{LogTag} ★ 지난 실행이 원복하지 못한 흔적이 있는데 지금 갚을 수 " +
                        $"없습니다({ReservedBarRevealPolicy.Describe(recovery.Reason)}) " +
                        $"기록된 원래 값={ReservedBarRevealPolicy.DescribeAutoHide(leftoverOriginal)}. " +
                        "흔적을 그대로 남겨 다음 실행이 다시 시도합니다.");
                }

                // 복구가 시스템을 실제로 바꿨다면 이번 실행의 판정은 **새로 읽은 값**으로 해야 한다.
                // 복구 전 값으로 판정하면 방금 되돌린 것을 못 본 채 결정하게 된다.
                if (recovery.WriteSystem && systemOk)
                {
                    observationOk = control.TryReadAutoHide(out observed);
                }
            }

            // ---------- 3. 이번 실행 판정 ----------
            ReservedBarPlan startup = ReservedBarRevealPolicy.ResolveStartup(available, observed, observationOk);
            LastStartupReason = startup.Reason;

            if (!startup.WriteSystem)
            {
                if (available)
                {
                    Debug.Log($"{LogTag} {ReservedBarRevealPolicy.Describe(startup.Reason)}");
                }
                InstallQuitHook();
                return;
            }

            // ★ write-ahead — 흔적이 먼저다. 흔적을 못 남기면 시스템을 바꾸지 않는다
            //   (원복을 보증할 수 없는 변경은 원칙 3의 예외 범위를 벗어난다).
            if (startup.WriteTrace && !ReservedBarRestoreLedger.Open(observed, tag))
            {
                LastStartupReason = ReservedBarReason.Unavailable;
                InstallQuitHook();
                return;
            }

            OriginalAutoHide = observed;
            bool applied = control.TrySetAutoHide(startup.SystemAutoHideValue);
            ChangedThisSession = applied;

            if (applied)
            {
                Debug.Log($"{LogTag} ★ {ReservedBarRevealPolicy.Describe(startup.Reason)} " +
                    $"원래 값={ReservedBarRevealPolicy.DescribeAutoHide(OriginalAutoHide)} → " +
                    $"지금={ReservedBarRevealPolicy.DescribeAutoHide(startup.SystemAutoHideValue)}. " +
                    $"원복 흔적을 먼저 남겼습니다({ReservedBarRestoreLedger.FilePath}) — " +
                    "강제 종료나 크래시로 이 프로그램이 원복하지 못하더라도 다음 실행이 되돌립니다.");
            }
            else
            {
                // 시스템이 안 바뀌었으면 흔적은 거짓말이 된다. 즉시 닫는다.
                ReservedBarRestoreLedger.Close(observed, tag);
                LastStartupReason = ReservedBarReason.Unavailable;
                Debug.LogWarning($"{LogTag} 자동 숨김을 해제하지 못했습니다(OS가 요청을 반영하지 않았습니다). " +
                    "남겼던 원복 흔적을 닫았습니다 — 되돌릴 변경이 없기 때문입니다. " +
                    "작업표시줄은 원래 설정 그대로입니다.");
            }

            InstallQuitHook();
        }

        /// <summary>
        /// 종료 경로. <c>Application.quitting</c>에서 불린다.
        ///
        /// <para><b>이 경로는 크래시/강제 종료에서 돌지 않는다.</b> 그것이 이 기능의 전제이고,
        /// 그래서 <see cref="ReservedBarRestoreLedger"/>가 있다 — 여기가 안 돌아도 다음 실행이 갚는다.
        /// 이 메서드는 "정상 종료일 때 더 빨리, 조용히 갚는" 최적 경로일 뿐 유일한 보증이 아니다.</para>
        /// </summary>
        public static void RunShutdown()
        {
            IReservedBarAutoHideControl control = _control;
            bool available = control != null;
            string tag = available ? control.PlatformTag : "(없음)";

            bool observed = false;
            bool observationOk = available && control.TryReadAutoHide(out observed);

            ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveQuit(
                ChangedThisSession && available, OriginalAutoHide, observed, observationOk);
            LastShutdownReason = plan.Reason;

            if (!plan.WriteSystem && !plan.CloseTrace)
            {
                // 우리가 아무 것도 안 바꿨다 = 로그를 남길 사건도 없다(24시간 상주 앱, 무의미한 줄 금지).
                return;
            }

            bool systemOk = true;
            if (plan.WriteSystem) systemOk = control.TrySetAutoHide(plan.SystemAutoHideValue);

            if (plan.CloseTrace && systemOk) ReservedBarRestoreLedger.Close(OriginalAutoHide, tag);

            if (systemOk)
            {
                Debug.Log($"{LogTag} ★ {ReservedBarRevealPolicy.Describe(plan.Reason)} " +
                    $"복원값={ReservedBarRevealPolicy.DescribeAutoHide(OriginalAutoHide)}.");
                ChangedThisSession = false;
            }
            else
            {
                Debug.LogWarning($"{LogTag} 종료 시 원복에 실패했습니다 — 흔적을 열어 둔 채로 남깁니다. " +
                    "다음 실행이 시작하자마자 되돌립니다.");
            }
        }

        /// <summary>테스트 전용 — 정적 상태를 기동 전 모습으로 되돌린다.</summary>
        public static void ResetForTesting()
        {
            _control = null;
            ChangedThisSession = false;
            OriginalAutoHide = false;
            LastStartupReason = ReservedBarReason.Unavailable;
            LastShutdownReason = ReservedBarReason.NothingToRestore;
            LastLedgerState = ReservedBarLedgerState.None;
        }

        private static void InstallQuitHook()
        {
            if (_quitHookInstalled) return;
            _quitHookInstalled = true;

            // MonoBehaviour의 OnApplicationQuit과 같은 시점에 불리면서 씬 오브젝트를 하나도 만들지
            // 않는다 — 이 기능은 씬 배선에 의존할 이유가 없고, 의존하면 씬이 바뀔 때 조용히 죽는다.
            Application.quitting += RunShutdown;
        }

        private static string DescribeLedger(ReservedBarLedgerState state)
        {
            switch (state)
            {
                case ReservedBarLedgerState.None: return "없음(정상)";
                case ReservedBarLedgerState.Closed: return "닫힘(갚을 것 없음)";
                case ReservedBarLedgerState.Open: return "★ 열림 — 지난 실행이 원복하지 못했습니다";
                case ReservedBarLedgerState.Unreadable: return "읽을 수 없음";
                case ReservedBarLedgerState.NewerSchema: return "이 빌드보다 새로운 스키마";
                case ReservedBarLedgerState.ForeignPlatform: return "다른 OS가 남긴 흔적";
                default: return state.ToString();
            }
        }
    }
}
