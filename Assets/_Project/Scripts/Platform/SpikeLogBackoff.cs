namespace StickMate.Platform
{
    /// <summary>
    /// **같은 상황이 계속되면 진단 로그의 간격을 스스로 벌린다.** 값 타입이라 할당이 없고
    /// 시계를 읽지 않는다(호출자가 dt를 준다) — EditMode에서 그대로 검증된다.
    ///
    /// ============================================================================
    /// 왜 필요한가 (2026-09-02 검증 R2-1, 실측)
    /// ============================================================================
    /// 전체화면 게임에 가려져 있는 452초 동안 <c>[스톨귀인]</c> 137.5 B/s + <c>[프레임스파이크]</c>
    /// 100.1 B/s = <b>전체 로그 바이트의 70.7%</b>가 이 두 줄이었다. 고정 5초 쿨다운은 제대로
    /// 동작하고 있었다 — 문제는 <b>5초마다 계속 찍을 만큼 상황이 계속됐다</b>는 것이다.
    /// 줄당 ~1.15KB × 5초 = 230 B/s가 <b>가장 조용해야 할 시간</b>에 디스크로 나갔다.
    /// 게임을 몇 시간 켜 두는 시간이 곧 그 시간이므로 절대 불변 원칙 2("비침해")와 정면으로 충돌한다.
    ///
    /// ============================================================================
    /// 규칙 — "새로운 것"은 항상 빠르게, "같은 것"은 점점 느리게
    /// ============================================================================
    /// <list type="bullet">
    /// <item>기본은 간격을 <b>2배로</b> 늘린다(최대 <see cref="MaxSeconds"/>).</item>
    /// <item><b>긴급한 새 종류</b>(= 실사용 히치)만 억제를 뚫고 간격을 최소로 되돌린다.
    ///   ★ 2026-09-02 신빌드 실측 정정: 처음엔 "등급이 바뀌면 무조건 리셋"이었는데, 전체화면을
    ///   빠르게 들락거리면 등급이 쉴 새 없이 오가 <b>억제가 통째로 무력화</b>됐다(6회 왕복 188초에서
    ///   신빌드 26줄 vs 구빌드 18줄 — 목표의 정반대). 전환/절감은 이미 이름이 붙은 알려진 현상이라
    ///   늦게 알려져도 잃는 것이 없다.</item>
    /// <item>간격이 다 지나도록 아무 일도 없었으면(조용해졌으면) 역시 최소 간격으로 되돌린다.</item>
    /// </list>
    /// 즉 <b>정보량이 0인 반복만</b> 억제하고, 상태 변화는 항상 최소 간격 안에 보고된다.
    /// 실측 452초 구간 기준 89줄 → 약 11줄(5·10·20·40·60·60…초)로 <b>8배</b> 줄어든다.
    /// </summary>
    public struct SpikeLogBackoff
    {
        /// <summary>최소(그리고 최초) 간격(초).</summary>
        public const float MinSeconds = 5f;

        /// <summary>최대 간격(초). 이보다 벌리면 "24시간 상주 앱이 아직 살아 있다"는 신호까지 잃는다.</summary>
        public const float MaxSeconds = 60f;

        private float _left;        // 남은 억제 시간
        private float _interval;    // 현재 간격
        private int _lastKind;      // 직전에 찍은 종류
        private bool _hasLast;
        private bool _firedInWindow; // 이번 간격 안에 억제된 후보가 있었는가

        /// <summary>현재 억제 간격(초) — 로그에 함께 찍어 사람이 "왜 뜸한지" 알 수 있게 한다.</summary>
        public float CurrentIntervalSeconds => _interval <= 0f ? MinSeconds : _interval;

        /// <summary>매 프레임 호출(호출자가 이미 갖고 있는 dt만 쓴다).</summary>
        public void Tick(float deltaSeconds)
        {
            if (_left <= 0f) return;
            _left -= deltaSeconds;
            if (_left > 0f) return;

            // 간격이 끝났다. 그 사이에 아무 후보도 없었다면 상황이 끝난 것이므로 간격을 되돌린다.
            if (!_firedInWindow) _interval = MinSeconds;
            _firedInWindow = false;
        }

        /// <summary>
        /// 지금 찍어도 되는가. 찍어도 되면 true를 돌려주고 <b>다음 간격을 스스로 정한다</b>.
        /// </summary>
        /// <param name="kind">이번 사건의 분류(<c>SpikeTierLedger.SpikeClass</c>).</param>
        /// <param name="urgent">사용자가 렉으로 느끼는 종류인가(= 실사용). <b>이것만</b> 억제를 뚫는다.</param>
        public bool ShouldLog(int kind, bool urgent)
        {
            bool first = !_hasLast;
            bool newKind = first || kind != _lastKind;

            // ★ 억제를 뚫는 것은 **긴급한 새 종류**뿐이다. "종류가 달라짐"만으로 뚫게 하면
            //   전체화면을 빠르게 들락거릴 때 등급이 쉴 새 없이 오가며 억제가 통째로 무력화된다
            //   (실측: 6회 왕복 188초에서 신빌드 26줄 vs 구빌드 18줄 — 오히려 늘었다).
            bool breakthrough = urgent && newKind;

            if (_left > 0f && !breakthrough)
            {
                _firedInWindow = true;
                return false;
            }

            // 첫 줄은 항상 최소 간격에서 시작한다 — 그러지 않으면 관측 시작부터 10초를 건너뛴다.
            _interval = (breakthrough || first)
                ? MinSeconds
                : System.Math.Min(MaxSeconds, (_interval <= 0f ? MinSeconds : _interval) * 2f);

            _hasLast = true;
            _lastKind = kind;
            _left = _interval;
            _firedInWindow = false;
            return true;
        }

        public void Reset()
        {
            _left = 0f; _interval = 0f; _lastKind = 0; _hasLast = false; _firedInWindow = false;
        }
    }
}
