namespace StickMate.States
{
    /// <summary>
    /// 커서 좌표 조회가 <b>왜</b> 실패했는가 — <c>bool</c> 하나로는 못 가리는 원인 코드.
    ///
    /// ============================================================================
    /// 왜 bool이 부족한가 (2026-09-27)
    /// ============================================================================
    /// <c>StickmanBlackboard.TryGetCursorWorldPosition</c>은 원인이 <b>넷</b>인데 실패를 전부 같은
    /// <c>false</c>로 돌려줬다: 제공자 배선 없음 · 카메라 없음 · 몸통 없음 · 제공자가 거절.
    /// 처방은 넷이 서로 다르다(배선 누락은 우리 버그, 제공자 거절은 OS 상태) 그런데
    /// <b>돌아오는 값이 똑같이 생겨서</b> 어느 쪽인지 말할 수 없었다 — 이 저장소가 반복해서 당한
    /// 「실패한 측정과 성공한 측정이 똑같이 생겼다」의 커서판이다.
    ///
    /// <para>모바일에서는 <c>Platform.ICursorPositionService</c> 구현이 없어 서비스 자체가 null이고,
    /// 그 경로도 같은 <c>false</c>로 수렴했다. 즉 「플랫폼이 원래 못 하는 것」과 「배선이 빠진 것」이
    /// 구별되지 않았다.</para>
    ///
    /// ============================================================================
    /// 집 양식을 그대로 베꼈다 — 발명하지 않았다
    /// ============================================================================
    /// 같은 병을 이 저장소가 이미 두 번 풀었고 처방이 같다: <c>Dialogue/GrabReactionLines</c>의
    /// <c>GrabZone.Unknown</c>(오프셋 0 = 「발끝을 정확히 잡았다」 = 「커서를 못 읽었다」)과
    /// <c>States/LedgeHangState</c>의 <c>LedgeHangDialogueParams.HasDescendTarget</c>
    /// (낙차 0 = 「가장 얕은 낙차」 = 「발판을 못 찾았다」). 원인 코드 열거형의 본은
    /// <c>Platform/GameVerdictRetryPolicy</c>의 <c>GameListReadOutcome</c>이다.
    ///
    /// <para>★ <b><see cref="NoProvider"/>가 0(기본값)인 것은 의도다.</b> 0이 <see cref="Ok"/>이면
    /// 값을 한 번도 채우지 않은 필드·배열·기본 생성 객체가 <b>성공을 주장</b>한다. 실패 계열을 0에
    /// 두면 조용히 틀리는 방향이 없고, 게다가 「아직 아무것도 배선되지 않았다」는 실제로
    /// <see cref="NoProvider"/>다(<c>GameListReadOutcome.Retryable = 0</c>과 같은 논지).</para>
    ///
    /// <para><b>플랫폼 중립</b>: 이 파일에는 플랫폼 분기가 한 줄도 없다. 판정은 여기, 사실 조회는
    /// 플랫폼 구현이 한다(CLAUDE.md 「정책 판정은 플랫폼 중립 위치에」).</para>
    /// </summary>
    public enum CursorReadOutcome
    {
        /// <summary>커서 제공자 자체가 배선되지 않았다(모바일 · 에디터 일부 · 배선 누락).
        /// <b>0인 이유는 이 열거형 문서의 마지막 문단</b>이다.</summary>
        NoProvider = 0,

        /// <summary>제공자를 불렀는데 좌표를 주지 않았다(OS 조회 실패 · 화면 밖 등).</summary>
        ProviderDeclined,

        /// <summary>카메라가 없어 OS 좌표를 월드로 되돌릴 수 없다.</summary>
        NoCamera,

        /// <summary>몸통(기준점)이 없어 깊이를 정할 수 없다.</summary>
        NoBody,

        /// <summary>읽었다. <b>0이 아닌 것이 계약</b>이다.</summary>
        Ok,
    }

    /// <summary>
    /// 커서 조회 실패를 <b>자리마다 세션당 한 줄만</b> 남기는 엣지 로그.
    ///
    /// ============================================================================
    /// 왜 엣지이고 왜 상한인가
    /// ============================================================================
    /// 이 앱은 하루 종일 켜져 있다. 실패는 한 번 시작되면 <b>그 상태가 지속</b>되는 성격이라
    /// (제공자가 없으면 계속 없다) 추첨마다 찍으면 하룻밤에 수천 줄이고 그때마다 문자열 보간이
    /// 할당된다. 그래서 <c>States/AutoWanderController</c>의 자리 비움 엣지 로그와
    /// <c>Platform/FootholdPoller</c>의 전이 로그 상한을 같은 형태로 따른다.
    ///
    /// <para>엣지만으로는 부족하다 — 실패↔성공을 왕복하면 엣지가 반복된다. 그래서
    /// <see cref="MaxLinesPerSite"/>로 <b>자리마다</b> 상한을 건다. 접힌 실패 횟수는
    /// <see cref="SuppressedFailures"/>에 남아 진단에서 셀 수 있다(정보를 버리지 않는다).</para>
    ///
    /// <para>★ <b>왜 struct가 아니라 class인가.</b> <c>Platform/WallClockIntervalGate</c>와
    /// <c>Platform/GameVerdictRetryState</c>가 같은 함정에서 값 타입을 버렸다 — 값 타입이면
    /// <c>readonly</c> 필드에 담는 순간 C#이 <b>호출마다 복사본</b>에 메서드를 불러 상태가 저장되지
    /// 않고, 상한이 <b>매 호출 리셋</b>된다(= 로그가 무한히 나간다). 컴파일 경고도 테스트 실패도
    /// 없다. 잠금으로 막는 대신 <b>함정 자체를 없앤다</b>.</para>
    ///
    /// <para><b>화면에는 아무것도 뜨지 않는다</b> — 이 장치는 로그 전용이고 사용자 표면을 만들지
    /// 않는다. 말풍선·문구를 여기서 파생하면 원칙 1(행동-텍스트 싱크)이 아니라 <b>진단</b>이
    /// 화면으로 새는 것이다.</para>
    /// </summary>
    public sealed class CursorReadFailureEdgeLog
    {
        /// <summary>한 자리(호출처)가 세션 동안 낼 수 있는 줄 수. 두 자리를 쓰면 합이 그 두 배다.
        /// <b>테스트는 이 상수를 참조한다</b> — 숫자를 베끼면 상한을 올리는 날 단언이 조용히 남는다.</summary>
        public const int MaxLinesPerSite = 1;

        /// <summary>마지막으로 본 결과. <b>초기값이 <see cref="CursorReadOutcome.Ok"/>인 것은 의도다</b> —
        /// 「아직 실패를 본 적 없다」라야 첫 실패가 엣지가 된다. 열거형 기본값(0)이 실패 계열이므로
        /// 이 필드는 <b>반드시 명시 초기화</b>해야 한다(그러지 않으면 첫 실패가 엣지로 안 잡힌다).</summary>
        private CursorReadOutcome _last = CursorReadOutcome.Ok;

        private int _emitted;
        private int _suppressed;

        /// <summary>이 자리가 실제로 낸 줄 수.</summary>
        public int EmittedLines => _emitted;

        /// <summary>엣지가 아니거나 상한에 막혀 <b>접힌</b> 실패 관측 수(진단/테스트용).</summary>
        public int SuppressedFailures => _suppressed;

        /// <summary>상한에 도달했는가 — 로그 문장이 「이 줄이 마지막」이라고 말할 근거.</summary>
        public bool ReachedCap => _emitted >= MaxLinesPerSite;

        /// <summary>
        /// 이번 관측을 <b>찍어야 하는가</b>. 성공을 넘기면 다음 실패가 다시 엣지가 되도록 되돌린다
        /// (호출자는 성공/실패를 가리지 말고 <b>매번</b> 이 메서드에 결과를 넘기면 된다 —
        /// 그래야 「읽기 한 번, 판정 한 번」이 유지된다).
        /// </summary>
        /// <returns><c>true</c>면 실패이고 이번이 그 자리의 엣지이며 상한 안이다.</returns>
        public bool ShouldEmit(CursorReadOutcome outcome)
        {
            if (outcome == CursorReadOutcome.Ok)
            {
                _last = CursorReadOutcome.Ok;
                return false;
            }

            bool isEdge = _last != outcome;   // 성공 뒤 첫 실패, 또는 원인이 바뀐 실패.
            _last = outcome;

            if (!isEdge || ReachedCap)
            {
                _suppressed++;
                return false;
            }

            _emitted++;
            return true;
        }
    }
}
