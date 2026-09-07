namespace StickMate.Core
{
    /// <summary>
    /// ============================================================================
    /// ★ 코스튬 진화 단계의 <b>유일한 출처</b> — 임계 숫자는 여기 말고 어디에도 없다
    /// ============================================================================
    /// 정본: <c>docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md</c> §3-3(경계값) · §5-3(일일 소프트캡) ·
    /// §7-2·§7-3(표시 형식과 내림 방향), 그리고 <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 7절.
    ///
    /// <para><b>왜 매니페스트에 임계를 두지 않는가</b>: 팩마다 임계가 갈리면
    /// <i>"이 팩만 빨리 큰다"</i>가 되고 그건 곧 페이투윈 축이다. 그래서
    /// <see cref="CostumeManifestSO"/>는 단계별 <b>조형</b>만 들고, <b>언제 그 단계가 되는가</b>는
    /// 전부 이 파일이 정한다.</para>
    ///
    /// ============================================================================
    /// ★★ 규칙 C-7 — <b>진화는 재화 축과 연결되지 않는다</b>
    /// ============================================================================
    /// 이 파일은 <b>재화 모델(동전 잔액을 들고 있는 쪽)을 참조하지 않는다</b>.
    /// 코스튬 단계는 동전으로 앞당길 수 없다.
    /// ★ 그 타입의 <b>이름조차 여기 적지 않는다</b>: 이 규칙을 재는 감사는 소스 텍스트를 훑는데,
    /// 경고 문구 안의 이름과 실제 호출이 <b>똑같이 생겼기</b> 때문이다. 이름을 안 적으면 그 감사가
    /// 주석 제거를 빠뜨려도 <b>여전히 옳은 답</b>을 낸다.
    /// 그러지 않으면 <i>"돈으로 살 수 있는 것 중에 그냥 플레이해서 얻는 것보다 센 것은 없다"</i>가
    /// 연출 축에서 뚫린다. <b>일일 소프트캡조차 인자로 받는다</b>(<see cref="RemainingDailyMinutes"/>) —
    /// 오늘 얼마나 쌓았는지를 이 파일이 <b>조회</b>하기 시작하면 그 순간 참조가 생긴다.
    ///
    /// ============================================================================
    /// ★ 단위가 정수 「분」인 이유 (부동소수가 이 축에서 통째로 사라진다)
    /// ============================================================================
    /// <c>float</c> 초에 <c>dt = 1/60</c>을 계속 더하면 100시간(360,000초) 근처에서 ULP가 0.03125초라
    /// 매 가산이 1 ULP를 통째로 먹어 <b>시계가 +87.5% 빨라진다</b>(두 라운드가 조율 없이 같은 값에
    /// 독립 수렴했다 — 계약서 3-2절 · R26 §4-3). 증상은 <i>"어떤 사람은 54시간 만에 마스터가 된다"</i>이고
    /// <b>로그에도 화면에도 아무 흔적이 안 남는다</b>. 6,000은 <c>int</c>에서 정확하다.
    ///
    /// <para>그래서 누적은 <b>세션 종료에 1회</b>, <c>floor(경과초 / 60)</c>분씩만 더한다 —
    /// 동전·XP가 쓰는 격자와 <b>같은 격자</b>다(25분 완주 → +25분/600동전/150XP,
    /// 3분40초 취소 → +3분/60동전/15XP, 40초 취소 → +0분/0동전/0XP).</para>
    ///
    /// ============================================================================
    /// ★ 「3단계」의 뜻 = <b>상태 4개 · 진화 사건 3회</b>(S4)
    /// ============================================================================
    /// 사용자가 쓴 네 숫자(0 · 10h · 50h · 100h)를 하나도 안 버리는 읽기다.
    /// <b>강등은 문법적으로 존재하지 않는다</b> — 누적은 줄어들지 않으므로 high-water 필드가 필요 없다
    /// (스탯의 <c>statTierReached</c>가 필요했던 이유는 스탯이 로드아웃에 따라 <b>내려가기</b> 때문이다).
    /// <para>★ 되살릴 조건 1개: <b>임계를 패치로 「올리면」</b> 이미 3단계였던 사용자가 2단계로
    /// 내려앉는다. 그런 라운드가 생기면 그 라운드가 <c>stageReached</c> high-water를 함께 도입해야
    /// 하고, 그때는 세이브 v13 + 하위 호환 1벌이 따라온다.</para>
    /// </summary>
    public static class CostumeEvolutionRules
    {
        /// <summary>★ 상태의 <b>개수</b>다(진화 사건 수가 아니다 — 사건은 3회).
        /// <c>CostumeManifestSO.stageShapes</c>의 유효 단계 번호가 이 값에서 나온다.</summary>
        public const int StageCount = 4;

        /// <summary>마스터(마지막) 단계 번호. <b>숫자를 베끼지 말고 이 상수를 참조하라</b> —
        /// 단이 하나 늘면 여기 하나만 따라온다.</summary>
        public const int MasterStage = StageCount - 1;

        /// <summary>1단계 진입 — 10시간. 사용자가 직접 쓴 숫자다(R26 §0-2: 하나도 옮기지 않았다).</summary>
        public const int Stage1BoundaryMinutes = 10 * 60;

        /// <summary>2단계 진입 — 50시간.</summary>
        public const int Stage2BoundaryMinutes = 50 * 60;

        /// <summary>3단계(마스터) 진입 — 100시간.</summary>
        public const int Stage3BoundaryMinutes = 100 * 60;

        /// <summary>
        /// 하루에 코스튬 누적에 실을 수 있는 최대 분(R26 §5-3). <b>전 코스튬 공유 1개</b>다 —
        /// 한 번에 한 벌만 입을 수 있으므로 나눌 이유가 없다.
        ///
        /// <para><b>240은 취향이 아니라 승계다</b>: 원형 D(가장 적극적인 정상 사용자) 집중 200분/일 ×
        /// 1.20(<c>CurrencyRules</c>의 활쏘기 일일 한도가 이미 쓴 여유율) = 240. 원형 D조차 40분 여유가
        /// 남으므로 <b>정상 사용자에게 닿지 않는다</b>.</para>
        ///
        /// <para><b>왜 동전에는 없는 캡이 여기엔 있는가</b>: 동전은 소진되는 자원이라 많이 벌어도 살 것이
        /// 없어지면 끝이다. 진화는 <b>되돌릴 수 없는 시각적 보상</b>이라, 4일 만에 마스터하면
        /// «마지막 목표» 그 자체가 사라진다.</para>
        ///
        /// <para>★ 넘으면 그날 <b>누적만</b> 멈춘다 — 동전·XP는 건드리지 않는다(다른 축이다).
        /// 그리고 그날 남은 분은 <b>화면에 표시하지 않는다</b>(정상 사용자가 평생 못 보는 값을 HUD에
        /// 두면 결제 압박 HUD와 같은 형태가 된다).</para>
        /// </summary>
        public const int DailySoftCapMinutes = 240;

        /// <summary>
        /// 이 누적 분이 몇 단계인가. <b>반열린 구간 <c>[시작, 끝)</c></b>이고 경계는 「이상」이다 —
        /// 정확히 <see cref="Stage3BoundaryMinutes"/>면 마스터다(§7-3의 표시 동치성이 여기 걸린다).
        /// <para>음수는 0으로 본다(손상된 파일의 위생은 <see cref="CostumeProgressModel"/>이 이미
        /// 하지만, 이 함수만 따로 불리는 경로에서도 같은 답이 나와야 한다).</para>
        /// </summary>
        public static int StageOf(int focusMinutes)
        {
            if (focusMinutes >= Stage3BoundaryMinutes) return 3;
            if (focusMinutes >= Stage2BoundaryMinutes) return 2;
            if (focusMinutes >= Stage1BoundaryMinutes) return 1;
            return 0;
        }

        /// <summary>이 단계가 시작되는 누적 분. 범위 밖 단계는 0단계로 취급한다.</summary>
        public static int StageStartMinutes(int stage)
        {
            switch (stage)
            {
                case 3: return Stage3BoundaryMinutes;
                case 2: return Stage2BoundaryMinutes;
                case 1: return Stage1BoundaryMinutes;
                default: return 0;
            }
        }

        /// <summary>
        /// 이 단계에서 <b>다음 경계</b>까지의 누적 분. 마스터면 <see cref="NoNextBoundary"/>(-1)다 —
        /// 다음이 없다는 사실을 0이나 <c>int.MaxValue</c>로 적으면 화면이 «0분 남았다» 또는
        /// «영원히 안 끝난다»를 그리게 된다.
        /// </summary>
        public static int NextBoundaryMinutes(int stage)
        {
            switch (stage)
            {
                case 0: return Stage1BoundaryMinutes;
                case 1: return Stage2BoundaryMinutes;
                case 2: return Stage3BoundaryMinutes;
                default: return NoNextBoundary;
            }
        }

        /// <summary>«다음 경계가 없다»(마스터). <see cref="NextBoundaryMinutes"/>와
        /// <see cref="PercentTenthsToNextBoundary"/>가 같은 값을 쓴다.</summary>
        public const int NoNextBoundary = -1;

        /// <summary>
        /// 오늘 이 코스튬 축에 <b>더 실을 수 있는</b> 분. 캡을 넘겨 저장된 파일에서도 음수를 내지 않는다.
        /// <para>★ 인자로 받는다 — 이 파일이 <c>CostumeProgressModel</c>을 조회하면 규칙 C-7의
        /// «재화 축과 연결되지 않는다»가 «어떤 모델도 조회하지 않는다»로 지켜지지 않게 된다.</para>
        /// </summary>
        public static int RemainingDailyMinutes(int minutesToday)
        {
            if (minutesToday >= DailySoftCapMinutes) return 0;
            if (minutesToday < 0) return DailySoftCapMinutes;
            return DailySoftCapMinutes - minutesToday;
        }

        /// <summary>
        /// ★ <b>다음 경계까지의 진행률을 1/1000 단위 정수로</b>. 마스터면 <see cref="NoNextBoundary"/>.
        ///
        /// <para><b>왜 정수 천분율인가</b>: 표시 규약이 <b>소수 1자리 내림</b>이고(R26 §7-2),
        /// <c>float</c>로 계산한 뒤 <c>Mathf.Floor</c>를 걸면 나눗셈 오차가 경계에서 한 칸을
        /// 넘길 수 있다. 정수 나눗셈은 음이 아닌 값에서 <b>정확히 내림</b>이다.</para>
        ///
        /// <para><b>왜 내림인가</b>(§7-3, 결정적 근거): <c>round</c>는 <b>「100.0%인데 아직 그 경계가
        /// 아닌」 화면</b>을 만든다. 내림은 «100.0% 표시»와 «경계 도달»이 <b>정확히 동치</b>가 되어
        /// 어긋나는 창이 0분이다. 그리고 코인·XP가 전부 내림이다 — 같은 앱에서 반올림 방향이 갈리지 않는다.</para>
        ///
        /// <para>★★ <b>2026-09-07 주석 정정 — 폭이 틀려 있었다(코드는 처음부터 옳았다).</b>
        /// 원래 여기 <i>"<c>round</c>는 5,997~5,999분 구간에서 3분 동안"</i>이라고 적혀 있었다.
        /// 그 숫자는 <b>통짜 0→100h 기준</b>(5997/6000)에서 나온 것인데 이 함수는 <b>구간 기준</b>이라
        /// 폭이 다르다. <b>전수 확인(0~5,999분 6,000칸)</b>한 실제 값:
        /// <list type="bullet">
        ///  <item><b>마스터 경계에서 <c>round</c>가 100.0%를 내는 것은 5,999분 <u>한 칸</u>뿐이다</b>
        ///    (2,999/3,000 = 99.9667% → 반올림 100.0%). <b>3분이 아니라 1분이다.</b></item>
        ///  <item>★ 그리고 마스터 경계만의 일이 아니다 — <b>2,999분</b>(1→2단계 경계)에서도 같은 일이 난다
        ///    (2,399/2,400 = 99.9583% → 반올림 100.0%). 즉 <c>round</c>의 거짓 100.0%는 <b>총 2칸</b>이고,
        ///    §7-3이 마스터만 본 것은 그 절이 마스터 표시를 논하던 자리였기 때문이다.
        ///    0단계에는 없다(599/600 = 99.833% → 99.8%).</item>
        ///  <item><b>내림은 그 6,000칸 전부에서 최댓값이 999다 — 1000 도달 0건.</b></item>
        /// </list>
        /// 정정의 계기는 <c>design-systems</c>가 자기 R26 문서를 고치다가 이 주석까지 전파된 것을
        /// <b>스스로 찾아 보고</b>한 것이다. 낡은 채 두면 다음 사람이 <b>틀린 근거 위에서</b>
        /// 반올림 방향을 재검토하게 된다.</para>
        ///
        /// <para>★ 그래서 이 함수는 <b>마스터가 아닌 동안 1000(=100.0%)을 절대 돌려주지 않는다</b>.
        /// 반환 범위는 <c>[0, 999]</c>이고, 마스터에 닿는 순간 <see cref="NoNextBoundary"/>로 바뀐다
        /// (마스터는 퍼센트를 그리지 않고 누적 시간만 남긴다 — §7-2).</para>
        /// </summary>
        public static int PercentTenthsToNextBoundary(int focusMinutes)
        {
            int stage = StageOf(focusMinutes);
            int end = NextBoundaryMinutes(stage);
            if (end == NoNextBoundary) return NoNextBoundary;

            int start = StageStartMinutes(stage);
            int span = end - start;
            if (span <= 0) return 0;      // 임계표가 단조가 아니면 여기 온다 — 아래 감사가 그것을 막는다

            int done = focusMinutes - start;
            if (done <= 0) return 0;

            long tenths = (long)done * 1000L / span;    // 음이 아닌 값이라 정수 나눗셈 = 내림

            // ★★ 이 클램프는 <b>오늘 경로에서 도달하지 않는다</b> — 그리고 그것이 이 줄의 값이다.
            //
            //    전수 확인(0~5,999분 6,000칸): 내림 천분율의 최댓값은 <b>999</b>이고 1000 도달은
            //    <b>0건</b>이다. 즉 지금 「100.0%인데 경계가 아닌」 화면이 안 나오는 이유는
            //    <b>이 줄이 아니라 위의 내림</b>이다.
            //
            //    ★ 그런데 그건 <b>오늘의 우연</b>이다. 같은 자리에 반올림을 넣으면 그 즉시
            //    2,999분과 5,999분 <b>두 칸</b>이 1000을 낸다(위 문단의 전수값). 이 줄이 지키는 것은
            //    «「100.0% ≡ 경계 도달」이 <b>반올림 방향 선택에 의존하지 않는다</b>»이고,
            //    그건 내림이 우연히 성립시키는 성질이 아니라 <b>이 함수가 약속한 계약</b>이다.
            //
            //    ⇒ <b>커버리지 도구가 이 줄을 「미실행」으로 지목해도 결함이 아니다. 지우지 마라.</b>
            //       지우는 순간, 훗날 누가 표시 규약을 반올림으로 바꾸면 «마스터가 아닌데 100.0%»가
            //       <b>아무 경보 없이</b> 돌아온다(이 저장소가 반복해서 당한 «조용한 회귀»의 형태다).
            if (tenths > 999L) tenths = 999L;
            return (int)tenths;
        }

        /// <summary>
        /// 같은 값의 표시용 실수판(예: <c>37.4f</c>). 마스터면 <see cref="NoNextBoundaryPercent"/>(-1f).
        /// <para>계산은 <see cref="PercentTenthsToNextBoundary"/>가 하고 여기서는 10으로 나누기만 한다 —
        /// 두 함수가 각자 계산하면 그게 곧 두 번째 이음매다.</para>
        /// </summary>
        public static float PercentToNextBoundary(int focusMinutes)
        {
            int tenths = PercentTenthsToNextBoundary(focusMinutes);
            return tenths == NoNextBoundary ? NoNextBoundaryPercent : tenths / 10f;
        }

        /// <summary>마스터라 퍼센트를 그리지 않는다는 표시. <c>0.0f</c>가 아닌 이유는
        /// <see cref="NoNextBoundary"/>와 같다 — «0%»는 실재하는 값이다.</summary>
        public const float NoNextBoundaryPercent = -1f;

        /// <summary>
        /// 임계표가 <b>단조 증가</b>인가. 테스트가 숫자를 베끼지 않고 이 성질을 잴 수 있게 둔다
        /// (임계를 손보는 라운드가 순서를 뒤집으면 <see cref="StageOf"/>가 조용히 틀린 답을 낸다 —
        /// 컴파일도 되고 화면도 뜬다).
        /// </summary>
        public static bool BoundariesAreStrictlyIncreasing()
        {
            int previous = 0;
            for (int stage = 1; stage < StageCount; stage++)
            {
                int boundary = StageStartMinutes(stage);
                if (boundary <= previous) return false;
                previous = boundary;
            }
            return true;
        }
    }
}
