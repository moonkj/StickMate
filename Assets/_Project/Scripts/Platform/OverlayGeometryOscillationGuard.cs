using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 창 기하 값이 <b>A → B → A → B</b>로 진동하는 것을 감지해 "더는 재적용하지 말라"고 알리는
    /// <b>플랫폼 중립 안전장치</b>. UnityEngine의 Rect 외 의존이 없고 P/Invoke도 없다 —
    /// EditMode가 실행으로 검증할 수 있어야 하기 때문이다(<see cref="OverlayBoundsFitPolicy"/> 설계).
    ///
    /// ============================================================================
    /// 왜 불감대만으로는 부족한가 (2026-09-01)
    /// ============================================================================
    /// <see cref="OverlayBoundsFitPolicy"/>의 불감대는 <b>1px 래칫</b>을 막는다 — "되읽기가 대입값보다
    /// 늘 1px 작다"처럼 <b>한 방향으로 조금씩 밀리는</b> 상수 오차가 영원히 "불일치"로 읽히는 것을
    /// 막는 장치다.
    ///
    /// <para>그런데 <b>A/B 진동은 그 가드를 그냥 통과한다</b>. 두 값이 <b>둘 다 불감대 밖</b>이면
    /// "지금 값 != 목표" 판정은 매번 참이고, 재적용 → 값이 반대편으로 → 다시 재적용이 <b>영원히</b>
    /// 계속된다. 실제 맥 실기 로그(PID 11451)에서 오버레이 창 사각형이
    /// <c>(0,0,1512,982)</c> ↔ <c>(0,33,1512,1010)</c>로 정확히 교대했다 — 차이가 28~33pt라
    /// 불감대(2pt)로는 절대 흡수되지 않는다.</para>
    ///
    /// <para>창 기하 재적용 한 번은 곧 OS 표면(백버퍼/스왑체인/리디렉션 표면) 재생성 한 번이고,
    /// 그것이 수백 ms짜리 정지다. 그래서 <b>수렴하지 않는다는 사실 자체를 감지해 멈추는</b> 장치가
    /// 따로 필요하다. 그것이 이 클래스다.</para>
    ///
    /// ============================================================================
    /// 판정 규칙
    /// ============================================================================
    /// 최근에 본 <b>서로 다른</b> 두 값을 기억한다. 새 표본이
    /// <list type="bullet">
    ///   <item>가장 최근 값과 같으면(불감대 안) — 아무 일도 없다(정상 정착 상태).</item>
    ///   <item>그 <b>직전</b> 값과 같으면 — 값이 되돌아온 것이므로 <b>교대 1회</b>로 센다.</item>
    ///   <item>둘 다 아닌 제3의 값이면 — 진동이 아니라 이동 중이므로 카운터를 0으로 되돌린다.</item>
    /// </list>
    /// 교대가 <see cref="DefaultAlternationsToLatch"/>회 쌓이면 <see cref="IsOscillating"/>이 <b>래치</b>된다.
    ///
    /// <para><b>왜 래치는 풀지 않는가</b>: <c>_setResolutionCalls</c>가 "프로세스 수명 상한"인 것과 같은
    /// 이유다. 여기서 자동으로 풀면 진동이 다시 시작될 때 상한이 사실상 사라진다. 재적용을 멈춘 결과는
    /// "창이 지금 자리에 그대로 있는다"이고, 그것은 "몇 초마다 수백 ms씩 얼어붙는다"보다 언제나 낫다.
    /// <see cref="Reset"/>은 테스트 전용 통로다.</para>
    ///
    /// <para><b>할당 0</b>: <see cref="Observe"/>는 문자열을 만들지 않는다. 진단 문구는 래치되는
    /// <b>그 순간 한 번만</b> 조립한다 — 24시간 상주 앱의 폴링 경로에 들어가는 코드다.</para>
    /// </summary>
    public sealed class OverlayGeometryOscillationGuard
    {
        /// <summary>
        /// 래치까지 필요한 교대 횟수. 4회 = "A B A B A"를 실제로 본 것이므로 우연한 한두 번의
        /// 왕복(창 애니메이션, 모니터 전환 중간 상태)과 구분된다. 정상 세션에서는 값이 정착하므로
        /// 이 카운터가 1을 넘지 않는다 — 그래서 이 장치는 건강한 경로의 동작을 바꾸지 않는다.
        /// </summary>
        public const int DefaultAlternationsToLatch = 4;

        /// <summary>비교 불감대의 단일 출처(값과 근거는 <see cref="OverlayBoundsFitPolicy"/>).</summary>
        public const float DefaultEpsilonPoints = OverlayBoundsFitPolicy.DefaultEpsilonPixels;

        private readonly int _alternationsToLatch;

        private Rect _recent;
        private Rect _previous;
        private bool _hasRecent;
        private bool _hasPrevious;

        public OverlayGeometryOscillationGuard(int alternationsToLatch = DefaultAlternationsToLatch)
        {
            _alternationsToLatch = Mathf.Max(2, alternationsToLatch);
        }

        /// <summary>지금까지 센 교대 횟수(진단/테스트용).</summary>
        public int AlternationCount { get; private set; }

        /// <summary>진동으로 확정됐는가. 한 번 true가 되면 이 인스턴스에서는 다시 false가 되지 않는다.</summary>
        public bool IsOscillating { get; private set; }

        /// <summary>래치된 순간에 조립한 사람이 읽을 진단 문구(래치 전에는 빈 문자열).</summary>
        public string Diagnosis { get; private set; } = string.Empty;

        /// <summary>진동으로 확정된 두 값(로그/테스트용). 래치 전 값은 의미 없다.</summary>
        public Rect LatchedValueA { get; private set; }

        /// <summary>진동으로 확정된 두 값(로그/테스트용). 래치 전 값은 의미 없다.</summary>
        public Rect LatchedValueB { get; private set; }

        /// <summary>
        /// 표본 하나를 관측한다.
        /// </summary>
        /// <returns><b>이번 호출에서</b> 진동이 처음 확정됐으면 true(= 경고를 딱 한 번 남기는 지점).</returns>
        public bool Observe(Rect sample, float epsilonPoints = DefaultEpsilonPoints)
        {
            if (IsOscillating) return false;      // 이미 확정 — 더 셀 이유가 없다.

            if (!_hasRecent)
            {
                _recent = sample;
                _hasRecent = true;
                return false;
            }

            if (Approximately(sample, _recent, epsilonPoints)) return false;   // 정착 상태.

            if (_hasPrevious && Approximately(sample, _previous, epsilonPoints))
            {
                AlternationCount++;
                _previous = _recent;
                _recent = sample;

                if (AlternationCount >= _alternationsToLatch)
                {
                    IsOscillating = true;
                    LatchedValueA = _previous;
                    LatchedValueB = _recent;
                    Diagnosis =
                        $"창 기하가 두 값 사이를 {AlternationCount}회 교대했습니다 — " +
                        $"A={_previous}, B={_recent}. 두 값의 차이가 불감대" +
                        $"({epsilonPoints:F1}pt)보다 크므로 재적용은 <b>원리적으로 수렴할 수 없습니다</b>. " +
                        "재적용 한 번은 곧 OS 표면 재생성 한 번(수백 ms 정지)이므로 여기서 멈춥니다. " +
                        "창은 지금 자리에 그대로 둡니다(그편이 항상 낫습니다).";
                    return true;
                }
                return false;
            }

            // 제3의 값 — 진동이 아니라 이동 중이다.
            AlternationCount = 0;
            _previous = _recent;
            _hasPrevious = true;
            _recent = sample;
            return false;
        }

        /// <summary>
        /// ★ <b>목표가 바뀌었으니 관측 이력만 버린다</b>(2026-09-02). <see cref="IsOscillating"/> 래치는
        /// <b>절대 건드리지 않는다</b> — "래치는 풀지 않는다"는 위 문단이 그대로 유효하다.
        ///
        /// <para><b>왜 필요한가</b>: 이 가드는 "재적용이 <b>원리적으로</b> 수렴하지 않는다"를 잡는다.
        /// 그런데 사용자가 표시 모니터를 <b>왼쪽↔오른쪽으로 바꾸면</b> 창 사각형이 두 값 사이를
        /// 오가고, 그것은 가드가 보기에 A↔B 진동과 <b>구별되지 않는다</b>. 네 번 왕복하면 래치가
        /// 걸려 <b>그 프로세스에서 다시는 모니터를 옮길 수 없다</b>(재무장 자체가 막힌다).
        /// 사용자가 의도적으로 만든 변화를 "수렴 실패"로 읽는 것은 이 가드의 계약 밖이다.</para>
        ///
        /// <para>그래서 <b>목표가 달라진 순간</b>에만 과거 표본을 버린다. 이전 표본들은 <b>다른 목표</b>에
        /// 대한 관측이라 새 목표의 수렴 여부와 아무 관계가 없다 — 지우는 것이 옳다.
        /// 목표가 그대로인 진짜 진동은 이력이 유지되므로 <b>여전히 잡힌다</b>.</para>
        /// </summary>
        public void ForgetSamplesForNewTarget()
        {
            AlternationCount = 0;
            _hasRecent = false;
            _hasPrevious = false;
            _recent = default;
            _previous = default;
        }

        /// <summary>테스트 전용 초기화(프로덕션 경로는 부르지 않는다 — 위 "왜 래치는 풀지 않는가" 참고).</summary>
        public void Reset()
        {
            AlternationCount = 0;
            IsOscillating = false;
            Diagnosis = string.Empty;
            _hasRecent = false;
            _hasPrevious = false;
            _recent = default;
            _previous = default;
            LatchedValueA = default;
            LatchedValueB = default;
        }

        private static bool Approximately(Rect a, Rect b, float epsilon)
        {
            return Mathf.Abs(a.x - b.x) <= epsilon
                && Mathf.Abs(a.y - b.y) <= epsilon
                && Mathf.Abs(a.width - b.width) <= epsilon
                && Mathf.Abs(a.height - b.height) <= epsilon;
        }
    }
}
