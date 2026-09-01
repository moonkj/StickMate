namespace StickMate.Platform
{
    /// <summary>
    /// **연속 동일 로그 줄 접기** — Player.log가 무한히 자라는 문제의 가장 값싼 처방
    /// (2026-09-02 로그 감량 라운드).
    ///
    /// ============================================================================
    /// 왜 필요한가 (실측)
    /// ============================================================================
    /// 실기 Player.log는 <b>153 B/s ≈ 13.2MB/일 ≈ 396MB/월</b>로 자란다. 그런데 그 안을 세어 보면
    /// <see cref="PlayerLogPolicy.RoutineNarrationEnabled"/> 문서가 적어 둔 그대로다 —
    /// <c>662 [유휴동작] 중 661줄이 글자 하나 다르지 않다</c>. 정보량이 0인 줄에 파일 쓰기를 붙이는
    /// 것은 순수한 낭비다.
    ///
    /// <para><b>★ 파일 회전(rename 후 새 파일)은 인프로세스로 할 수 없다.</b> Player.log는 Unity
    /// 네이티브 플레이어가 연 핸들이 쓴다. rename하면 그 핸들은 지워진 inode를 계속 가리키고,
    /// 결과는 "로그가 통째로 사라짐"이다. 시도하지 말 것.</para>
    ///
    /// ============================================================================
    /// 무엇을 잃지 않는가
    /// ============================================================================
    /// 접힌 줄은 <b>사라지지 않고 횟수로 보존된다</b>(<c>... (직전과 동일 K회 반복 — 접음)</c>).
    /// 태그도 그대로 남으므로 태그 기준 grep 집계가 깨지지 않는다. 즉 CLAUDE.md 불변 원칙 1
    /// (행동-텍스트 싱크)을 원격에서 검증하는 데 쓰는 로그를 <b>하나도 잃지 않는다</b> —
    /// "로그를 줄인다"가 "눈을 감는다"가 되면 안 된다는 이 저장소의 기존 판단과 같은 선이다.
    ///
    /// ============================================================================
    /// 왜 전역이 아니라 **호출처마다** 하나씩 두는가
    /// ============================================================================
    /// 로그 파일 전체를 하나의 스트림으로 보고 "직전 줄"을 비교하면 거의 아무것도 접히지 않는다 —
    /// 실측에서 <c>[유휴동작]</c>은 <c>[말풍선]</c>/<c>[MacWindowService]</c>와 <b>번갈아</b> 찍히기
    /// 때문이다(661줄이 같다는 것은 <i>내용</i>이 같다는 뜻이지 <i>인접</i>하다는 뜻이 아니다).
    /// 그래서 이 구조체는 값 타입 필드로 <b>emitter마다 하나씩</b> 소유되며, 비교 대상은 "그
    /// emitter가 직전에 낸 줄"이다. 그게 사람이 실제로 읽을 때 기대하는 의미이기도 하다.
    ///
    /// ============================================================================
    /// 시간 처리 — 폴링을 요구하지 않는다
    /// ============================================================================
    /// 접힌 상태로 영원히 침묵하지 않도록 <paramref name="holdSeconds"/>가 지나면 요약을 낸다.
    /// 그 판정은 <b>다음 반복이 들어올 때</b> 한다. 반복 스트림이 계속 들어온다는 것이 애초의
    /// 전제이므로 이것으로 충분하고, <c>Update()</c>에 폴링을 새로 배선하지 않아도 된다
    /// (24시간 상주 앱에 프레임당 일거리를 늘리지 않는다). 폴링할 수 있는 호출처는
    /// <see cref="TryFlush"/>를 직접 불러도 된다.
    ///
    /// <b>할당 0</b>: 문자열 비교(Ordinal)와 int/double 대입뿐이다.
    /// </summary>
    public struct RepeatedLogFolder
    {
        private string _last;
        private int _repeats;
        private double _lastEmitAt;
        private bool _hasLast;

        /// <summary>아직 방출되지 않고 접혀 있는 반복 횟수(진단/테스트용).</summary>
        public int PendingRepeats => _repeats;

        /// <summary>
        /// 이번 줄을 <b>파일에 쓸지</b> 판정한다.
        /// </summary>
        /// <param name="message">이번에 찍으려는 완성된 로그 문장.</param>
        /// <param name="now">단조 증가 시각(초). 호출자가 <c>Time.realtimeSinceStartupAsDouble</c> 등을 넘긴다.</param>
        /// <param name="holdSeconds">같은 줄이 이만큼 이어지면 중간 요약을 한 번 낸다. 0 이하면 요약 없음.</param>
        /// <param name="foldedRepeats">
        /// 0보다 크면 호출자는 <b>먼저</b> <see cref="Describe"/> 한 줄을 찍어야 한다
        /// (반환값이 true면 그 뒤에 <paramref name="message"/>도 찍는다).
        /// </param>
        /// <returns><paramref name="message"/>를 그대로 찍어야 하면 true.</returns>
        public bool ShouldEmit(string message, double now, double holdSeconds, out int foldedRepeats)
        {
            foldedRepeats = 0;
            if (message == null) return false;

            // 문자열 비교는 Ordinal — 사람이 읽는 텍스트 정렬이 아니라 "같은 줄인가"의 동일성 판정이다.
            bool same = _hasLast && string.Equals(_last, message, System.StringComparison.Ordinal);
            if (!same)
            {
                // 다른 줄이 왔다 = 접혀 있던 반복 구간이 여기서 끝난다.
                foldedRepeats = _repeats;
                _repeats = 0;
                _last = message;
                _hasLast = true;
                _lastEmitAt = now;
                return true;
            }

            _repeats++;
            if (holdSeconds > 0.0 && now - _lastEmitAt >= holdSeconds)
            {
                foldedRepeats = _repeats;
                _repeats = 0;
                _lastEmitAt = now;
            }
            return false;
        }

        /// <summary>
        /// 새 줄이 들어오지 않아도 접힌 반복을 털어내고 싶은 호출처용(폴링 경로가 이미 있는 경우).
        /// 낼 것이 있으면 true.
        /// </summary>
        public bool TryFlush(double now, double holdSeconds, out int foldedRepeats)
        {
            foldedRepeats = 0;
            if (_repeats <= 0) return false;
            if (holdSeconds > 0.0 && now - _lastEmitAt < holdSeconds) return false;

            foldedRepeats = _repeats;
            _repeats = 0;
            _lastEmitAt = now;
            return true;
        }

        /// <summary>테스트/재시작용.</summary>
        public void Reset()
        {
            _last = null;
            _hasLast = false;
            _repeats = 0;
            _lastEmitAt = 0.0;
        }

        /// <summary>
        /// 접힘 요약 한 줄. <paramref name="tag"/>를 앞에 그대로 남기는 이유는 태그 기준 grep 집계가
        /// 깨지지 않게 하기 위해서다(로그를 줄이되 세는 능력은 잃지 않는다).
        /// </summary>
        public static string Describe(string tag, int repeats)
            => $"{tag} (직전과 동일 {repeats}회 반복 — 접음)";
    }
}
