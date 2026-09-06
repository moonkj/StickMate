namespace StickMate.Core
{
    /// <summary>
    /// ★ <b>「언제 달력을 볼 것인가」만 아는 물건</b> — 2026-09-06 롤오버 배선 라운드.
    ///
    /// ============================================================================
    /// 왜 <see cref="CurrencyModel.TickDayRollover"/>를 직접 <c>Update()</c>에서 부르지 않는가
    /// ============================================================================
    /// 부를 수도 있었다. 그런데 그러면 <b>「하루가 넘어갔는가」와 「얼마나 자주 물어보는가」가
    /// 같은 함수 안에 섞인다</b>. 이 저장소가 반복해서 당한 형태가 그것이다 —
    /// 같은 사실이 두 곳에서 계산되면 그게 다음 버그다(CLAUDE.md).
    ///
    /// <para>그래서 역할을 정확히 둘로 자른다:</para>
    /// <list type="bullet">
    ///   <item><b>하루가 넘어갔는가</b> — <see cref="CurrencyModel.TickDayRollover"/> 한 곳.
    ///     달력을 읽는 유일한 지점이고(T-3-a), 리필 조건(<see cref="CurrencyRules.MayRefill"/>)도 거기 있다.</item>
    ///   <item><b>얼마나 자주 물어보는가</b> — <b>이 파일</b>. 여기에는 날짜 판정이 한 줄도 없다.</item>
    /// </list>
    ///
    /// <para>덕분에 이 클래스는 <b>UnityEngine을 참조하지 않고</b>, 주기 게이트를 EditMode에서
    /// 벽시계 대기 없이 전부 잰다(<c>Tests/EditMode/CurrencyDayRolloverTests.cs</c>).</para>
    ///
    /// ============================================================================
    /// ★ 프레임 델타를 누적하지 않는다 — <b>단조 시계를 직접 뺀다</b>
    /// ============================================================================
    /// <c>Time.unscaledDeltaTime</c>을 더해 가는 흔한 형태를 <b>일부러 쓰지 않았다</b>.
    /// 이 앱은 24시간 상주하고 <b>기계가 잠들었다 깬다</b>. 델타 누적은
    /// ① 엔진이 한 프레임 델타에 상한을 걸면 그만큼 <b>잃고</b>,
    /// ② 긴 정지 뒤에 큰 값이 한 번에 들어오면 <b>잔여 이월 방식에 따라 연속 발화</b>한다.
    /// <para>단조 시계 두 시점의 차는 그 둘 어느 쪽에도 걸리지 않는다 —
    /// 정지 중에 흐른 시간이 다음 호출에 <b>그대로 남아 있고</b>, 발화는 언제나 한 번이다.
    /// 그리고 이 시계는 <see cref="CurrencyModel.TickDayRollover"/>가 리필 간격을 재는 시계와
    /// <b>같은 시계</b>라, 시계가 두 개가 되어 어긋날 자리가 없다.</para>
    ///
    /// ============================================================================
    /// ★ 정확도가 아니라 <b>「적어도 이 간격마다 한 번 묻는다」</b>가 계약이다
    /// ============================================================================
    /// 하루가 넘어갔다는 <b>사실</b>은 달력이 정하고, 이 게이트는 그 사실을 <b>언제 알아채는지</b>만
    /// 정한다. 그래서 늦게 알아채도 하루를 <b>건너뛰지 않는다</b> —
    /// <see cref="CurrencyModel.DayIndex"/>는 래칫이라 밀린 롤오버가 사라지지 않고 그대로 기다린다.
    /// <para>그 성질 때문에 잔여를 이월하지 않고 <b>발화할 때마다 기산점을 지금으로 옮긴다</b>.
    /// 이월하면 정지 직후 여러 프레임 연속으로 달력을 읽는데, 그렇게 얻는 것이 하나도 없다.</para>
    ///
    /// <para><b>인스턴스가 둘이어도 안전하다</b>: 실제 리필은
    /// <see cref="CurrencyRules.MayRefill"/>이 <b>모델 쪽에서</b> 한 번만 통과시킨다.
    /// 이 클래스가 세는 <see cref="RolloverCount"/>는 그래서 <b>지시가 아니라 관측</b>이다.</para>
    /// </summary>
    public sealed class CurrencyDayRolloverTicker
    {
        /// <summary>
        /// 달력을 다시 보기까지의 최소 간격(초).
        ///
        /// <para><b>왜 60인가</b> — 세 가지가 같은 값을 가리킨다:</para>
        /// <list type="number">
        ///   <item>이 앱의 주기 저장이 60초다(<c>StickConfig.progressionAutoSaveIntervalSeconds</c>).
        ///     롤오버는 <see cref="CurrencyModel.IsDirty"/>를 세우므로, 이 값이 저장 주기와 같으면
        ///     <b>리셋이 관측된 뒤 늦어도 한 저장 주기 안에 디스크에 닿는다</b>.</item>
        ///   <item>사용자가 체감하는 지연이 <b>자정으로부터 최대 60초</b>다. 하루 예산이 다시 열리는
        ///     사건이라 초 단위 정밀도가 의미를 갖지 않는다.</item>
        ///   <item>비용이 하루 1,440회다. 매 프레임 호출은 60fps에서 하루 <b>518만 회</b>이고,
        ///     그 대부분이 «오늘은 어제와 같은 날이다»라는 같은 답을 낸다.</item>
        /// </list>
        ///
        /// <para>★ <b>이 값은 경제 수치가 아니다</b> — 지급량·상한·요율이 아니라 <b>폴링 간격</b>이라
        /// <c>design-systems</c> 소관이 아니고, <c>StickConfig</c>에도 넣지 않는다.
        /// 애셋 필드로 만들면 <c>Data/DefaultStickConfig.asset</c>까지 함께 관리해야 하는데
        /// (애셋이 코드 기본값을 덮는다), 사용자가 조절할 이유가 하나도 없는 값이다.</para>
        /// </summary>
        public const double CheckIntervalSeconds = 60.0;

        /// <summary>마지막으로 달력을 본 단조 시각.
        /// <para>초기값이 <see cref="double.NegativeInfinity"/>인 것이 계약의 일부다 —
        /// <b>첫 <see cref="TickIfDue"/>는 간격을 기다리지 않는다</b>. 그래서 «앱을 켰는데 60초 동안
        /// 어제 상태로 산다»는 구간이 존재하지 않는다.</para></summary>
        private double _lastCheckMonotonic = double.NegativeInfinity;

        /// <summary>달력을 실제로 본 횟수(관측용). 주기 게이트가 일을 하고 있는지 재는 유일한 값이라
        /// 테스트가 이것을 본다 — 「롤오버가 0번 일어났다」만으로는
        /// <b>게이트가 막은 것</b>과 <b>날짜가 안 바뀐 것</b>을 구별할 수 없다.</summary>
        public int CheckCount { get; private set; }

        /// <summary>실제로 롤오버가 일어난 횟수(관측용).</summary>
        public int RolloverCount { get; private set; }

        /// <summary>마지막으로 달력을 본 단조 시각. 아직 한 번도 안 봤으면
        /// <see cref="double.NegativeInfinity"/>.</summary>
        public double LastCheckMonotonic => _lastCheckMonotonic;

        /// <summary>
        /// 주기 게이트를 지나면 달력을 본다. <b>매 프레임 불러도 되는 값싼 호출</b>이다 —
        /// 대부분의 프레임에서 <c>double</c> 뺄셈 하나로 끝난다.
        ///
        /// <para>★ 이름이 그냥 <c>Tick</c>이 아닌 이유: 이 저장소에는 <c>Tick(</c>이 여러 파일에 있어
        /// (<c>CharacterScaleController.Tick()</c> 등) <b>배선 감사의 니들이 무디어진다</b>.
        /// <c>TickIfDue(</c>는 트리에서 유일하므로 «이 진입점이 실제로 배선돼 있는가»를
        /// 소스 스캔으로 명확히 잴 수 있다. 이름이 계약의 일부다.</para>
        /// </summary>
        /// <param name="nowMonotonic"><c>Time.realtimeSinceStartupAsDouble</c>. <b>벽시계가 아니다.</b></param>
        /// <returns>이번 호출이 롤오버를 일으켰으면 true.</returns>
        public bool TickIfDue(double nowMonotonic)
        {
            // 시계가 뒤로 갔으면 차가 음수라 게이트가 막는다 — 여기서 발화가 폭주하지 않는다.
            // (단조 시계라 정상 경로에서는 일어나지 않지만, 막는 쪽이 공짜다.)
            //
            // ★ NaN 가드를 <b>여기 두지 않는다.</b> NaN은 모든 비교가 false라 이 게이트를
            //   <b>거꾸로 통과</b>하지만, 그 앞을 여기서 한 번 더 막으면 손상된 시각을 막는 자리가
            //   두 곳이 된다. 같은 사실을 두 곳에서 판정하면 그게 다음 버그다(CLAUDE.md) —
            //   그리고 실제로 그렇게 두면 <b>둘 중 하나를 지워도 테스트가 안 깨진다</b>
            //   (돌연변이 실측으로 확인했다). 가드는 CheckNow 한 곳뿐이다.
            if (nowMonotonic - _lastCheckMonotonic < CheckIntervalSeconds) return false;

            return CheckNow(nowMonotonic);
        }

        /// <summary>
        /// 주기를 <b>무시하고</b> 지금 즉시 달력을 본다.
        ///
        /// <para>쓰는 자리는 하나다 — <b>저장 파일을 읽은 직후</b>. 며칠 만에 다시 켠 사용자의
        /// 어제(혹은 사흘 전) 카운터가 <b>첫 프레임에</b> 풀려야, 그 뒤에 붙는 어떤 수급 배선도
        /// «오늘 이미 다 받았다»는 낡은 상태 위에서 돌지 않는다.</para>
        ///
        /// <para>★ <b>반드시 <see cref="CharacterSaveStore"/> 로드 뒤에 부른다.</b> 앞에서 부르면
        /// 기본값(<see cref="CurrencyModel.DayIndex"/> = 0) 위에서 롤오버가 한 번 일어나고,
        /// 곧이어 로드가 그것을 <b>파일 값으로 덮어써</b> 아무 일도 안 한 것이 된다.
        /// 그리고 그 실패는 로그도 예외도 남기지 않는다.</para>
        /// </summary>
        /// <param name="nowMonotonic"><c>Time.realtimeSinceStartupAsDouble</c>.</param>
        /// <returns>이번 호출이 롤오버를 일으켰으면 true.</returns>
        public bool CheckNow(double nowMonotonic)
        {
            // ★ 손상된 시각을 막는 <b>유일한</b> 자리(TickIfDue 주석 참고).
            //   NaN을 그대로 받으면 기산점이 NaN이 되고, 그 뒤 「now − NaN = NaN」이라
            //   TickIfDue의 「< 간격」이 <b>언제나 false</b>가 되어 게이트가 사실상 사라진다 —
            //   NaN이 한 번 들어온 뒤로는 매 프레임 달력을 읽는다(정상값이 한 번 들어오면 낫는다).
            if (double.IsNaN(nowMonotonic)) return false;

            _lastCheckMonotonic = nowMonotonic;
            CheckCount++;

            // ★ 이 한 줄이 이 파일의 전부다. 날짜 판정·리필 조건·무엇을 되돌리는지는
            //   전부 저쪽에 있고, 여기로 복사해 오지 않는다.
            bool rolled = CurrencyModel.TickDayRollover(nowMonotonic);
            if (rolled) RolloverCount++;
            return rolled;
        }

        // ★ 리셋 API를 두지 않는다 — 이 객체는 상태가 세 개뿐이고 <b>새로 만드는 것이 곧 리셋</b>이다.
        //   테스트가 <c>new</c>를 쓰면 «리셋을 빠뜨려 앞 테스트가 샌다»는 종류의 실패가 성립하지 않는다.
        //   (정적 상태인 CurrencyModel 쪽은 그럴 수 없어 ResetForTesting을 갖는다 — 그 비대칭이 의도다.)
    }
}
