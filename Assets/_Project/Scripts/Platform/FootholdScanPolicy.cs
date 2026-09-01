namespace StickMate.Platform
{
    /// <summary>이번 프레임에 발판 목록을 어디까지 다시 볼 것인가.</summary>
    public enum FootholdScanScope
    {
        /// <summary>아무것도 하지 않는다. OS 창 열거 0회 = 이 라운드가 노리는 정상 상태.</summary>
        None = 0,

        /// <summary>전체 재열거(<c>EnumWindows</c> / <c>CGWindowListCopyWindowInfo</c>).</summary>
        Full = 1,
    }

    /// <summary>왜 그렇게 결정했는가. 로그와 60초 요약이 이 값을 사람이 읽는 말로 바꾼다.</summary>
    public enum FootholdScanTrigger
    {
        /// <summary>스캔하지 않았다.</summary>
        Idle = 0,

        /// <summary>첫 스캔(캐시가 아직 없다).</summary>
        Bootstrap = 1,

        /// <summary>딛고 있던 발판이 캐시에서 사라졌다 — <b>확인 사살</b> 재열거.</summary>
        GroundLossConfirm = 2,

        /// <summary>이벤트 훅이 없거나 죽었다 — 옛 주기 폴링으로 되돌아간 경로.</summary>
        FallbackPolling = 3,

        /// <summary>저빈도 안전망(훅이 이벤트를 흘렸을 때를 대비한 바닥).</summary>
        SafetyNet = 4,

        /// <summary>유저가 캐릭터를 붙잡았다 — 곧 어디로든 던져질 수 있다.</summary>
        Grabbed = 5,

        /// <summary>공중에 있다(던져짐/낙하) — 착지 후보를 알아야 한다.</summary>
        Airborne = 6,

        /// <summary>걸어서 "근처 창 집합"을 만든 반경 밖으로 나갔다.</summary>
        NeighborhoodExit = 7,

        /// <summary><b>감시 중인 창</b>이 움직이거나 사라졌다는 통보를 받았다.</summary>
        WatchedWindowEvent = 8,

        /// <summary>감시 대상 밖의 창이 바뀌었다는 통보를 받았다(좁힘이 꺼져 있을 때만 유효).</summary>
        GlobalWindowEvent = 9,

        /// <summary>스캔 사유는 성립했으나 최소 간격에 걸려 이번 프레임은 건너뛰었다.</summary>
        Throttled = 10,
    }

    /// <summary>한 프레임의 결정. 값 타입이라 할당이 없다(24시간 상주 앱 컨벤션).</summary>
    public readonly struct FootholdScanDecision
    {
        public readonly FootholdScanScope Scope;
        public readonly FootholdScanTrigger Trigger;

        public FootholdScanDecision(FootholdScanScope scope, FootholdScanTrigger trigger)
        {
            Scope = scope;
            Trigger = trigger;
        }

        public bool ShouldEnumerate => Scope == FootholdScanScope.Full;
    }

    /// <summary>
    /// <see cref="FootholdScanPolicy.Decide"/>의 입력. <b>전부 bool/float</b>다 — 상태머신 enum이나
    /// UnityEngine 타입을 넣지 않는 것이 이 파일이 순수하게 남는 조건이다(아래 클래스 문서 참고).
    /// </summary>
    public struct FootholdScanSignals
    {
        /// <summary>캐시가 한 번이라도 채워졌는가.</summary>
        public bool HasBootstrapped;

        /// <summary>이벤트 훅(<c>SetWinEventHook</c> 등)이 지금 살아 있는가.</summary>
        public bool NotifierActive;

        /// <summary>딛고 있던 발판이 캐시에서 사라졌다 — 호출자가 자기 쿨다운을 통과시킨 뒤에만 true.</summary>
        public bool GroundLossConfirmRequested;

        /// <summary>유저가 캐릭터를 붙잡고 있다(Dragged).</summary>
        public bool CharacterGrabbed;

        /// <summary>공중에 있다(Fall/ThrowTumble/Jump/Ragdoll 등).</summary>
        public bool CharacterAirborne;

        /// <summary>캐릭터가 오래 정지해 있다(<c>FramePacing</c>의 Still 판정을 그대로 재사용).</summary>
        public bool CharacterStill;

        /// <summary>접지 중이다(발판 핸들 != 0). 합성 발판(Dock/안전망)도 포함한다.</summary>
        public bool CharacterGrounded;

        /// <summary>마지막 전체 스캔이 만든 "근처" 반경을 캐릭터가 벗어났다.</summary>
        public bool NeighborhoodExited;

        /// <summary>감시 대상 창에 대한 변화 통보가 밀려 있다.</summary>
        public bool WatchedWindowEventPending;

        /// <summary>감시 대상 밖 창에 대한 변화 통보가 밀려 있다.</summary>
        public bool GlobalWindowEventPending;

        /// <summary>마지막 전체 스캔 이후 경과(초).</summary>
        public float SecondsSinceFullScan;

        /// <summary>훅이 살아 있을 때의 저빈도 안전망 주기(초).</summary>
        public float SafetyNetIntervalSeconds;

        /// <summary>훅이 죽었을 때의 폴링 주기(초) = 이번 라운드 이전의 <c>footholdPollInterval</c>.</summary>
        public float FallbackPollIntervalSeconds;

        /// <summary>이벤트가 유발하는 스캔의 최소 간격(초). 이벤트 폭풍 상한.</summary>
        public float MinEventScanIntervalSeconds;

        /// <summary>상태(붙잡힘/공중/반경이탈)가 유발하는 스캔의 최소 간격(초).</summary>
        public float MinStateScanIntervalSeconds;
    }

    /// <summary>
    /// "지금 창 목록을 다시 열거해야 하는가, 그리고 무엇을 감시 대상으로 둘 것인가"를 결정하는
    /// <b>순수 규칙</b>. UnityEngine 의존도 P/Invoke도 한 줄 없다 — 그래야 Windows 실기가 없는 개발
    /// 머신에서 규칙 자체를 실행해 검증할 수 있다
    /// (<see cref="OverlayBoundsFitPolicy"/> / <see cref="TopmostRestorePolicy"/>와 같은 설계).
    ///
    /// ============================================================================
    /// 왜 생겼는가 (2026-09-01, 사용자 "지금 해결방안들보다 획기적인 방안이 마련되어야할거같은데")
    /// ============================================================================
    /// 오늘 Windows 렉을 네 번 고쳤고 네 번 다 부분적이었다. 남은 부조리는 구조 자체에 있었다:
    /// <b>창은 대부분 가만히 있는데 우리는 "혹시 움직였나"를 초당 3.3회 계속 물었다</b>
    /// (<c>StickConfig.footholdPollInterval = 0.3</c>). 실기 로그에서 열거 대상 창은 최대 <b>818개</b>였고,
    /// 창 하나당 <c>IsWindowVisible</c> → <c>GetWindowTextLength</c> → <c>GetWindowLong</c> →
    /// <c>GetWindowRect</c> → <c>DwmGetWindowAttribute</c>(DWM 프로세스로 가는 크로스 프로세스 호출)다.
    /// 818 x 3.3회/초 = 초당 약 2,700회의 관리↔네이티브 전환이 <b>24시간 내내</b> 돈다.
    ///
    /// 처방은 두 축이다. 둘 다 있어야 비용이 0에 수렴한다:
    /// <list type="number">
    /// <item><b>"언제"</b> — OS가 창 변화를 <b>통보</b>하게 한다(Windows <c>SetWinEventHook</c>).
    ///   아무 창도 안 움직이면 스캔 0회.</item>
    /// <item><b>"무엇을"</b> — 통보를 받아도 <b>우리에게 의미 있는 창</b>의 통보만 스캔으로 이어지게
    ///   한다. 캐릭터가 작업표시줄 위에 가만히 서 있으면 감시 대상은 사실상 0개다.</item>
    /// </list>
    ///
    /// 2번은 사용자가 직접 짚은 설계다: <i>"유휴상태일때 하는 행동에 따라서 화면 스캔하면 될거같고 …
    /// 창위에 올라가있을때 사용자가 창을 없애거나 옮길수있으니까 그 해당창주위만 스캔해도 될거같긴한데."</i>
    /// 그리고 <i>"마우스로 캐릭을 잡거나하는 이벤트 발생시"</i> — 붙잡히면 어디로든 던져지므로 그때는
    /// 다시 넓게 봐야 한다(실기 로그에 <c>[DragThrowState] 놓음 … 속력 12.00</c> → 화면 반대편 창에 착지가
    /// 실제로 찍혀 있다).
    ///
    /// ============================================================================
    /// 안전 설계 — "창에서 갑자기 떨어짐"을 되살리지 않는 두 개의 못
    /// ============================================================================
    /// (1) <b>좁게 보다가 딛고 있는 창을 놓치는 것</b>이 이 라운드의 가장 큰 위험이다. 그래서
    ///     <b>딛고 있는 창은 어떤 최적화에서도 예외 없이 감시 대상</b>이고
    ///     (<see cref="ShouldNarrowToWatchedWindows"/>가 좁히는 것은 "그 밖"뿐이다),
    /// (2) 그럼에도 캐시에서 그 발판이 사라지면 <b>유예 타이머를 시작하기 전에 전체 재열거를 한 번 더
    ///     한다</b>(<see cref="FootholdScanTrigger.GroundLossConfirm"/>). 이벤트 방식에는 폴링과 달리
    ///     "다음 주기에 저절로 고쳐진다"가 없기 때문에 이 확인 사살이 반드시 있어야 한다 —
    ///     이것이 없으면 열거가 한 번 튀었을 때 안전망 주기(2~5초)까지 잘못된 목록이 유지된다.
    ///     이 확인 사살 덕분에 <b>발판 상실 경로의 최악 캐시 지연은 이벤트 방식에서도 폴링 방식보다
    ///     길어지지 않는다</b> — 그래서 <c>StickConfig.ResolveGroundLossGraceDuration()</c>의 유도식을
    ///     짧아지는 방향으로 바꾸지 않았다(<see cref="ResolveWorstCaseStalenessSeconds"/> 참고).
    /// </summary>
    public static class FootholdScanPolicy
    {
        /// <summary>
        /// 감시 목록의 최대 크기. 넘으면 <b>좁히기를 포기</b>하고 전역 통보를 받는다 —
        /// 안전한 방향으로 넘어지는 선택이다(놓치는 것보다 더 보는 것이 낫다). 콜백은 워커 스레드에서
        /// 도는 선형 검색이라 이 정도가 상한이어야 이벤트 폭풍에서도 콜백 자체가 싸게 유지된다.
        /// </summary>
        public const int MaxWatchedWindows = 32;

        /// <summary>
        /// "근처" 반경의 절대 하한(OS 포인트). 캐릭터가 완전히 정지해 보행 속도 유도값이 0에
        /// 가까워져도 감시 상자가 캐릭터 몸보다 작아지면 안 된다.
        /// 값의 근거: 이 앱 캐릭터의 배포 형상 높이가 약 63pt이므로 그 2배를 하한으로 둔다.
        /// </summary>
        public const float MinNeighborhoodRadiusOsPx = 128f;

        /// <summary>
        /// 결정 본체. 순서가 곧 우선순위이며, 위쪽일수록 안전(더 자주 본다) 쪽이다.
        /// </summary>
        public static FootholdScanDecision Decide(in FootholdScanSignals s)
        {
            // 1. 캐시가 아직 없다 — 무조건 본다.
            if (!s.HasBootstrapped)
            {
                return new FootholdScanDecision(FootholdScanScope.Full, FootholdScanTrigger.Bootstrap);
            }

            // 2. 발판 상실 확인 사살. 스로틀보다 위에 둔다(위 클래스 문서 (2)) — 이 경로가 늦으면
            //    캐릭터가 멀쩡한 창 위에서 떨어진다. 호출자가 자기 쿨다운으로 이미 한 번 걸렀다.
            if (s.GroundLossConfirmRequested)
            {
                return new FootholdScanDecision(FootholdScanScope.Full, FootholdScanTrigger.GroundLossConfirm);
            }

            // 3. 훅이 없거나 죽었다 → 이 라운드 이전 거동으로 정확히 되돌아간다.
            //    여기서 저빈도 안전망(2~5초)을 쓰면 캐릭터가 허공에 서 있게 된다 — 폴백은 옛 주기 그대로다.
            if (!s.NotifierActive)
            {
                return s.SecondsSinceFullScan >= Positive(s.FallbackPollIntervalSeconds, 0.3f)
                    ? new FootholdScanDecision(FootholdScanScope.Full, FootholdScanTrigger.FallbackPolling)
                    : new FootholdScanDecision(FootholdScanScope.None, FootholdScanTrigger.Idle);
            }

            // 4. 저빈도 안전망 — 훅이 이벤트를 흘렸거나(세션 격리/권한) 우리가 좁혀서 놓친 변화를 흡수한다.
            if (s.SecondsSinceFullScan >= Positive(s.SafetyNetIntervalSeconds, 3f))
            {
                return new FootholdScanDecision(FootholdScanScope.Full, FootholdScanTrigger.SafetyNet);
            }

            // 5~7. 상태가 유발하는 스캔. 붙잡힘/공중은 "곧 어디로 갈지 모른다"라서 넓게 본다.
            bool stateWants = s.CharacterGrabbed || s.CharacterAirborne || s.NeighborhoodExited;
            if (stateWants)
            {
                if (s.SecondsSinceFullScan < Positive(s.MinStateScanIntervalSeconds, 0.3f))
                {
                    return new FootholdScanDecision(FootholdScanScope.None, FootholdScanTrigger.Throttled);
                }

                FootholdScanTrigger trigger = s.CharacterGrabbed ? FootholdScanTrigger.Grabbed
                    : s.CharacterAirborne ? FootholdScanTrigger.Airborne
                    : FootholdScanTrigger.NeighborhoodExit;
                return new FootholdScanDecision(FootholdScanScope.Full, trigger);
            }

            // 8~9. 통보가 유발하는 스캔.
            bool narrowed = ShouldNarrowToWatchedWindows(s.NotifierActive, s.CharacterStill,
                s.CharacterGrounded, s.CharacterGrabbed, s.CharacterAirborne);
            bool eventWants = s.WatchedWindowEventPending || (s.GlobalWindowEventPending && !narrowed);
            if (!eventWants)
            {
                return new FootholdScanDecision(FootholdScanScope.None, FootholdScanTrigger.Idle);
            }

            if (s.SecondsSinceFullScan < Positive(s.MinEventScanIntervalSeconds, 0.1f))
            {
                return new FootholdScanDecision(FootholdScanScope.None, FootholdScanTrigger.Throttled);
            }

            return new FootholdScanDecision(FootholdScanScope.Full,
                s.WatchedWindowEventPending ? FootholdScanTrigger.WatchedWindowEvent
                                            : FootholdScanTrigger.GlobalWindowEvent);
        }

        /// <summary>
        /// "감시 대상 밖의 창 변화를 무시해도 되는가" — 사용자 제안의 <b>무엇을 볼지</b> 축.
        ///
        /// <para>좁힐 수 있는 조건은 <b>전부 동시에</b> 성립해야 한다:</para>
        /// <list type="bullet">
        /// <item>훅이 살아 있다(죽었으면 통보 자체가 없으므로 좁힐 대상도 없다).</item>
        /// <item>캐릭터가 오래 정지해 있다 — <c>FramePacing</c>의 Still 판정을 그대로 받는다.
        ///   <b>같은 판정을 두 벌 만들지 않는다</b>(이 저장소가 오늘 여러 번 겪은 사고).</item>
        /// <item>접지해 있다 — 발판이 확정돼 있어야 "그 창만 보면 된다"가 성립한다.</item>
        /// <item>붙잡히지도, 공중에 있지도 않다 — 그 둘은 착지 지점을 모르므로 넓게 봐야 한다.</item>
        /// </list>
        ///
        /// <para><b>좁혀서 놓치게 되는 것과 그 안전성</b>: 감시 밖 창이 우리 창 위로 드래그돼 와서
        /// 상단 테두리를 가리는 경우를 안전망 주기(2~5초)까지 모를 수 있다. 그 결과는 "이미 가려진
        /// 창의 옛 상단선 위에 잠깐 더 서 있다"이고, <b>캐릭터가 떨어지는 방향이 아니다</b>. 반대로
        /// 딛고 있는 창 자신의 이동/숨김/파괴는 언제나 감시 대상이라 즉시 통보된다.</para>
        /// </summary>
        public static bool ShouldNarrowToWatchedWindows(bool notifierActive, bool characterStill,
            bool characterGrounded, bool characterGrabbed, bool characterAirborne)
        {
            if (!notifierActive) return false;
            if (characterGrabbed || characterAirborne) return false;
            return characterStill && characterGrounded;
        }

        /// <summary>
        /// "근처"의 반경(OS 포인트)을 <b>보행 속도와 갱신 주기에서 유도</b>한다. 숫자를 적지 않는 이유는
        /// 이 저장소의 다른 유도값들과 같다 — 캐릭터 크기 다이얼이 배율을 0.35~2.00으로 바꾸고
        /// 안전망 주기도 설정으로 움직이기 때문에, 고정 반경은 그 둘 중 하나만 바뀌어도 의미를 잃는다.
        ///
        /// <para>= max(<see cref="MinNeighborhoodRadiusOsPx"/>,
        /// 보행속도(OS pt/초) x 지평선(초) x 여유배수)</para>
        ///
        /// <para><b>지평선</b>으로는 저빈도 안전망 주기를 넣는다. 그것이 "전체 스캔 없이 캐릭터가 계속
        /// 걸을 수 있는 최악의 시간"이기 때문이다(그 사이에 반경을 벗어나면 반경 이탈 트리거가 먼저
        /// 전체 스캔을 부른다 — 즉 이 값은 '얼마나 자주 다시 볼 것인가'의 손잡이다).</para>
        ///
        /// <para>던지기 속도(실측 12 유닛/초)는 여기에 넣지 않는다. 붙잡힘/공중은 아예 넓게 보는
        /// 갈래로 빠지므로 이 반경이 쓰이지 않는다 — 넣으면 반경만 5배로 부풀어 좁히기가 무의미해진다.</para>
        /// </summary>
        public static float ResolveNeighborhoodRadiusOsPx(float walkSpeedOsPxPerSecond,
            float horizonSeconds, float marginFactor)
        {
            float derived = Positive(walkSpeedOsPxPerSecond, 0f)
                * Positive(horizonSeconds, 0f)
                * Positive(marginFactor, 1f);
            return derived > MinNeighborhoodRadiusOsPx ? derived : MinNeighborhoodRadiusOsPx;
        }

        /// <summary>
        /// <b>발판 캐시가 최악의 경우 얼마나 낡을 수 있는가</b>(초) — 발판 상실 유예의 유도 근거.
        ///
        /// <para>이번 라운드 이전의 답은 단순했다: "캐시는 <c>footholdPollInterval</c> 동안 고정"이므로
        /// 유예 = max(fallGraceDuration, 폴링주기 x 1.5). 폴링을 없애면 그 전제가 바뀌므로 여기서
        /// 다시 유도한다.</para>
        ///
        /// <para><b>결론: 값은 그대로 <c>fallbackPollIntervalSeconds</c>다.</b> 근거는 두 갈래 모두
        /// 검사해서 나온다:</para>
        /// <list type="bullet">
        /// <item><b>훅이 죽은 경우</b> — 폴백이 옛 주기를 그대로 쓰므로 최악 지연도 그대로다.</item>
        /// <item><b>훅이 살아 있는 경우</b> — 발판이 캐시에서 사라지는 순간 확인 사살 재열거가 한 번
        ///   더 돈다(<see cref="FootholdScanTrigger.GroundLossConfirm"/>). 그 확인 사살의 쿨다운을
        ///   폴링 주기와 같게 두면, <b>발판 상실 경로의 최악 지연은 정확히 폴링 방식과 같아진다.</b>
        ///   저빈도 안전망(2~5초)은 이 식에 들어오지 않는다 — 안전망은 "그 밖의 창"을 늦게 아는
        ///   경로이고, 딛고 있는 발판은 확인 사살이 담당하기 때문이다.</item>
        /// </list>
        ///
        /// <para>그래서 이 라운드는 <b>유예를 한 밀리초도 줄이지 않는다.</b> 이것은 비용을 줄이는
        /// 라운드이지 유예를 줄이는 라운드가 아니다 — 유예를 함께 건드리면 오늘 고친 "창에서 갑자기
        /// 떨어짐"이 되살아났을 때 원인이 둘로 갈려 원격 진단이 불가능해진다.</para>
        ///
        /// <para><paramref name="safetyNetIntervalSeconds"/>를 받으면서 쓰지 않는 것처럼 보이는 것은
        /// 의도다: <b>안전망이 이 식에 들어오면 안 된다</b>는 사실 자체를 시그니처와 테스트로 못 박는다
        /// (안전망을 5초로 늘려도 유예가 7.5초로 부풀지 않는다는 회귀 테스트가 이 인자를 흔든다).</para>
        /// </summary>
        public static float ResolveWorstCaseStalenessSeconds(bool notifierActive,
            float fallbackPollIntervalSeconds, float safetyNetIntervalSeconds,
            float groundLossConfirmCooldownSeconds)
        {
            float poll = Positive(fallbackPollIntervalSeconds, 0.3f);
            if (!notifierActive) return poll;

            // 훅이 살아 있으면 확인 사살 쿨다운이 상한이다. 다만 유예가 <b>짧아지는 방향</b>으로는
            // 절대 가지 않게 폴링 주기를 하한으로 둔다(위 문단 참고).
            float confirm = Positive(groundLossConfirmCooldownSeconds, poll);
            return confirm > poll ? confirm : poll;
        }

        /// <summary>0 이하/NaN이면 대체값. 설정 필드가 0으로 남아도 규칙이 무너지지 않게 한다.</summary>
        private static float Positive(float value, float fallback)
        {
            return value > 0f ? value : fallback; // NaN 비교는 false이므로 NaN도 여기서 걸러진다.
        }
    }
}
