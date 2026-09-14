namespace StickMate.Platform
{
    /// <summary>
    /// ★★★ 2026-09-03 — <b>등급 1(패널 회수) 중에 사용자가 <i>직접 부른</i> 표면을 허용할 것인가</b>만
    /// 판정하는 순수 규칙. UnityEngine 의존도 P/Invoke도 <b>한 줄 없다</b>.
    ///
    /// ============================================================================
    /// 무엇을 고치는가 — <b>스위치가 자기 자신 뒤에 숨어 있었다</b>
    /// ============================================================================
    /// <code>
    ///   등급 1을 끄는 유일한 스위치 = AppSettingsModel.AutoHideOnFullscreen
    ///     └ 유일한 조작 지점 = 설정창 [일반]
    ///          └ 그 설정창은 ArePanelsSuppressed이면 그 프레임에 스스로 닫힌다
    /// </code>
    /// 즉 <b>등급 1이 켜져 있는 동안 등급 1을 끄는 방법이 앱 안에 하나도 없었다.</b>
    /// 경로 4개가 전부 막혀 있었다: 톱니→부채꼴→정보창→설정(≤1프레임 만에 닫힘) /
    /// 전역 단축키(열자마자 다음 <c>Update()</c>가 닫음) / 복귀 예약(등급 1에서 반환) /
    /// 사용자 숨김 단축키(더 숨긴다).
    ///
    /// <para>★ 이것은 등급 1이 만든 결함이 <b>아니다</b>. 같은 루프가 등급 2에 이미 있었고, 등급 1은
    /// 조건을 「전체화면 게임」에서 「디스플레이를 덮는 모든 창」으로 넓혀 <b>드물던 것을 일상으로</b>
    /// 만들었을 뿐이다. 그러므로 등급 1을 되돌리는 것은 처방이 아니다.</para>
    ///
    /// ============================================================================
    /// 처방 — 회수 기준을 <b>「표면의 종류」가 아니라 「누가 열었는가」</b>로
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>등급 1 진입 시</b>: 전부 회수한다(비침해 유지). 이미 떠 있던 창은 예외 없이 걷힌다.</item>
    ///   <item><b>등급 1 체류 중 사용자가 부른 것</b>: 허용한다. 우리가 빼앗은 것을 사용자가 다시
    ///     달라고 <b>명시적으로</b> 요청한 경우다.</item>
    /// </list>
    /// 저장소가 이미 같은 판단을 적어 두었다 — <c>SettingsWindow</c>의 복귀 예약 문서:
    /// <i>"빼앗은 것을 돌려주는 것은 「부르지 않은 창을 띄우는 것」이 아니라 되돌리기다."</i>
    ///
    /// ============================================================================
    /// ★ 왜 <b>임대(lease)</b>인가 — 한 번 켜고 마는 플래그로는 비침해가 샌다
    /// ============================================================================
    /// 허가를 "등급 1이 끝날 때까지" 붙잡아 두면, 사용자가 톱니를 한 번 누른 뒤부터는 그 전체화면
    /// 세션이 끝날 때까지 <b>사용자가 부르지도 않은</b> 표면(할 일 리마인더 / 창 크래시 오버레이 /
    /// 포스트잇)까지 발표 화면 위로 돌아온다. 두 시간짜리 발표에서 그것은 원칙 2의 정면 위반이다.
    ///
    /// <para>그래서 허가는 <b>매 프레임 갱신되어야 유지되는 임대</b>다. 사용자가 부른 표면이 살아 있는
    /// 동안 그 표면이 스스로 갱신하고, 마지막 표면이 닫히면 <see cref="LeaseSeconds"/> 안에 만료되어
    /// 등급 1의 회수가 그대로 돌아온다. <b>부활은 없다</b>(<see cref="RenewedLeaseUntil"/> —
    /// 만료된 임대는 갱신으로 살아나지 않는다). 새 허가는 <see cref="CanGrant"/>를 통과한
    /// <b>명시적 사용자 행위</b>로만 난다.</para>
    ///
    /// <para>★★ 2026-09-15 (N-20) — <b>임대만으로는 위 문단의 약속이 절반만 지켜졌다.</b> 임대는 사용자 창이 <b>닫힌 뒤</b>의
    /// 누수를 막았지만, 사용자 창이 <b>떠 있는 동안</b>에는 자동 표면(리마인더 · 크랙 · 포스트잇 · 설정창 자동 재오픈)이
    /// <see cref="SuppressesPanels"/>를 읽어 그 임대에 편승했다. 그래서 자동 표면은 허가에 대해 억제만 넓히는
    /// <see cref="SuppressesUnsummonedSurfaces"/>를 읽는다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 <c>Platform/</c> <b>바로 아래</b> 있는 이유
    /// ============================================================================
    /// <c>FullscreenSuspendPolicy</c>가 한때 <c>Platform/MacOS/</c> 안에 있었고, 그 자리에 있는 동안
    /// Windows 구현은 같은 규칙을 <b>부를 수조차 없었다</b>(CLAUDE.md에 실제 사고로 기록됨).
    /// 이 판정은 <b>플랫폼 분기가 하나도 없다</b> — 양 플랫폼이 같은 규칙을 부른다.
    /// 플랫폼 폴더로 옮기지 마라.
    /// </summary>
    public static class UserSurfaceSummonPolicy
    {
        /// <summary>
        /// 허가 임대의 수명(초). 사용자가 부른 표면이 <b>매 프레임</b> 갱신하므로, 이 값은
        /// "표면이 닫힌 뒤 회수가 돌아오기까지의 지연"이다.
        ///
        /// <para><b>0.5초인 이유 — 이 앱의 <i>가장 느린</i> 프레임 예산에서 역산했다</b>:
        /// 적응형 페이싱의 최저 등급은 <c>ViewerPresence</c>의 <c>Still</c>(최대 분주 8 = 60→7.5fps,
        /// 프레임당 <b>133ms</b>)이고, 화면이 꺼진 등급은 <c>DisplayOffTargetFps</c>(4fps = 250ms)다.
        /// 후자는 사용자가 톱니를 누를 수 있는 상황이 아니므로 실질 최악은 133ms이고,
        /// 0.5초는 그 <b>3.7배</b>다. 즉 <c>Update</c>/<c>LateUpdate</c> 실행 순서나 표면 사이 인계
        /// (부채꼴 → 정보창 → 설정창)에서 <b>한두 프레임의 공백</b>이 생겨도 임대가 끊기지 않는다.</para>
        ///
        /// <para>반대쪽 상한: (a) 사람이 "닫았는데 아직 안 걷혔다"를 인지하는 하한(약 1초)보다 짧고,
        /// (b) 사용자가 부르지 않은 연출이 이 틈에 끼어들 확률은 0.5초 ÷ 전체화면 세션 길이라
        /// 무시할 수준이다.</para>
        ///
        /// <para>★ <b>선례가 이 저장소 안에 이미 있다</b>: <c>FramePacing.InteractionHoldSeconds</c>도
        /// <b>같은 0.5초</b>이고 <b>같은 형태</b>다 — "열려 있는 동안 매 프레임 다시 부르고, 호출이
        /// 끊기면 저절로 만료된다". 그 주석이 적어 둔 근거가 여기에도 그대로 적용된다:
        /// <i>"만료 시각 방식은 호출부가 죽어도 저절로 풀린다"</i>. 해제 책임을 가진 코드가 없으므로
        /// <b>해제를 빠뜨려 영구히 켜지는 누수</b>가 구조적으로 불가능하다 —
        /// 24시간 상주 앱에서 그것이 가장 나쁜 실패다.</para></summary>
        public const float LeaseSeconds = 0.5f;

        /// <summary>
        /// <b>지금 새 허가를 낼 수 있는가.</b> 이 함수가 이 파일의 안전판이다.
        ///
        /// <list type="number">
        ///   <item><b>등급 2에서는 절대 나지 않는다</b>(<paramref name="characterSuspended"/>).
        ///     전체화면 <b>게임</b>이거나 사용자가 직접 숨긴 상태다 — 전자는 원칙 2 그 자체이고,
        ///     후자에서 표면을 되살리면 화면공유 중에 "지금은 나오지 마"를 우리가 뒤집는 것이 된다.</item>
        ///   <item><b>등급 1이 <i>이미</i> 켜져 있을 때만 난다</b>(<paramref name="panelRetreatActive"/>).
        ///     이 조건이 <b>"등급 1 진입 시 전부 회수"</b>를 구조적으로 보장한다 — 등급 1이 켜지기
        ///     <b>전에</b> 열려 있던 창은 이 함수를 통과한 적이 없으므로 임대가 없고, 그래서 진입하는
        ///     순간 예외 없이 걷힌다. 스스로에게 허가를 발급하는 경로가 열리지 않는다.</item>
        /// </list>
        /// </summary>
        public static bool CanGrant(bool characterSuspended, bool panelRetreatActive)
            => !characterSuspended && panelRetreatActive;

        /// <summary>
        /// 갱신 후의 임대 만료 시각. <b>이미 만료된 임대는 되살리지 않는다</b> —
        /// 갱신자(사용자가 부른 표면)는 자기가 살아 있다는 사실만 말할 뿐, 허가를 <b>낼</b> 권한은 없다.
        /// 그 권한은 <see cref="CanGrant"/>를 통과한 명시적 사용자 행위에만 있다.
        /// </summary>
        /// <param name="currentLeaseUntil">현재 임대 만료 시각(무허가면 0 이하).</param>
        /// <param name="now">지금(벽시계, 초).</param>
        public static float RenewedLeaseUntil(float currentLeaseUntil, float now)
            => now < currentLeaseUntil ? now + LeaseSeconds : currentLeaseUntil;

        /// <summary>
        /// <b>화면 고정 표면과 그 클릭 차단막을 지금 걷어야 하는가</b>의 최종 판정.
        ///
        /// <para>★ <b>불변식 — 등급 2를 항상 포함한다</b>: 첫 항이 <c>||</c>의 왼쪽에 있으므로
        /// <paramref name="characterSuspended"/>가 참이면 나머지 인자가 무엇이든 참이다.
        /// 이 포함관계가 깨지면 "캐릭터는 숨었는데 차단막은 남은" 상태 —
        /// <b>안 보이는데 클릭만 먹는</b>, 이 앱에서 가장 나쁜 형태 — 가 구조적으로 가능해진다.
        /// 허가는 <b>등급 1에만</b> 작용한다.</para>
        /// </summary>
        public static bool SuppressesPanels(bool characterSuspended, bool panelRetreatActive,
            bool userSummonGranted)
            => characterSuspended || (panelRetreatActive && !userSummonGranted);

        /// <summary>
        /// ★★ 2026-09-15 (N-20) — <b>사용자가 부르지 않은 자동 표면을 지금 억제해야 하는가.</b>
        /// 대상: 할 일 메모 카드와 그 클릭 차단막 · 할 일 리마인더 · 창 크랙 오버레이 · 설정창 자동 재오픈.
        /// 설계 정본: <c>docs/systems/AUTO_SURFACE_LEASE_AXIS.md</c>.
        ///
        /// <para>★ <b>불변식 — 사용자 허가(임대)는 사용자가 부르지 않은 표면을 절대 드러내지 않는다.</b>
        /// 즉 이 값은 모든 입력에서 <see cref="SuppressesPanels"/>를 포함하고(<paramref name="panelsSuppressed"/>가 참이면 참),
        /// <paramref name="userSummonGranted"/>에 대해 단조 증가다(허가가 거짓에서 참으로 가도 억제가 풀리는 칸이 없다).</para>
        ///
        /// <para><b>왜 필요한가</b>: <see cref="SuppressesPanels"/>는 s ∨ (r ∧ ¬g)라서, 등급 1(r)에서 사용자가 연 창이 임대(g)를
        /// 갱신하는 동안 거짓이다. 그 값을 읽던 자동 표면 4곳이 <b>남의 임대에 편승해</b> 발표 화면 위로 되살아났다
        /// (메모 카드 차단막은 그 사각형의 클릭까지 먹었다 — 원칙 2). 갱신자는 누가 창을 열었는지 묻지 않으므로,
        /// 표면 쪽이 «나는 사용자가 부른 적이 없다»를 스스로 판정해야 한다.</para>
        ///
        /// <para><b>진리표</b> — 인덱스 = s·4 + r·2 + g. 인자는 <b>같은 프레임의</b> <c>StickmanAgent.ArePanelsSuppressed</c>와
        /// <c>StickmanAgent.IsUserSummonGrantActive</c>다(둘 다 <c>Time.unscaledTime</c>을 읽어 한 프레임 안에서 값이 같다).
        /// <code>
        ///   (s,r,g)                        FFF FFT FTF FTT TFF TFT TTF TTT
        ///   SuppressesPanels (P)            F   F   T   F   T   T   T   T
        ///   SuppressesUnsummonedSurfaces    F   T   T   T   T   T   T   T
        /// </code>
        /// P와 갈리는 칸은 정확히 둘이다 — <b>FTT</b>(결함 칸: 등급 1에서 사용자 창이 열린 동안)와 <b>FFT</b>(이력 칸).</para>
        ///
        /// <para>★ <b>FFT 칸은 의도다</b>: 등급 1에서 연 사용자 창이 아직 떠 있는데 전체화면 앱은 사라진 상태다. 자동 표면은
        /// 사용자 창이 닫힐 때까지(+ <see cref="LeaseSeconds"/>) 기다린다. 그래서 설정창 자동 재오픈이 사용자의 정보창을
        /// 빼앗지 않고, Windows에서 우리 창을 만지는 동안 등급이 None으로 떨어지는 경우(가설 H1)에도 자동 표면이 새지 않는다.
        /// 대가는 그 사이 메모 카드 복귀 · 리마인더 · 크랙이 늦어지는 것뿐이다(억제를 <b>더하는</b> 방향).
        /// 평상시 데스크톱(g 거짓)에서는 <see cref="SuppressesPanels"/>와 같다 — 허가는 <see cref="CanGrant"/>상 등급 1이 이미
        /// 켜져 있을 때만 나고, 갱신(<see cref="RenewedLeaseUntil"/>)은 만료된 임대를 되살리지 않는다.</para>
        ///
        /// <para>★ <b>순수 s ∨ r이 아닌 이유</b>: r(<c>StickmanAgent</c>의 등급 1 필드)은 private이고, 공개 값 {s, g, P}로는 복원할 수
        /// 없다(g가 참이면 P = s). 에이전트 파일을 열지 않고 소비자 호출부에서 조립할 수 있는 인자는 이 둘뿐이다.</para>
        ///
        /// <para>★ <b>소비자 호출형은 하나다</b>: <c>UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(agent.ArePanelsSuppressed,
        /// agent.IsUserSummonGrantActive)</c>. 첫 인자를 <c>HidesScreenSurfaces</c> · <c>IsUserSummonBlocked</c>로 바꾸면 등급 1 억제가
        /// 통째로 사라진다(그 둘은 등급 2에서만 참). 캐릭터 축이 필요한 소비자(리마인더 · 크랙)는 이 호출 <b>밖</b>에
        /// <c>|| IsSuspended</c>를 붙인다. ★ <b>사용자가 여는 표면</b>(정보창 · 부채꼴 · 팝오버 · 설정창 닫기 · 시트 복귀)은 이 값을
        /// <b>읽지 않는다</b> — 허가가 참이면 이 값도 참이라, 읽는 순간 사용자 창이 스스로 닫혀 등급 1 입구가 다시 사라진다.
        /// 분류는 <c>Tests/EditMode/UnsummonedSurfaceAxisTests</c>가 소스 전수 스캔으로 잠근다.</para>
        /// </summary>
        public static bool SuppressesUnsummonedSurfaces(bool panelsSuppressed, bool userSummonGranted)
            => panelsSuppressed || userSummonGranted;

        /// <summary>
        /// ★★ 2026-09-14 — <b>허가를 받아도 표면이 억제되는가</b>(= 사용자가 지금 불러도 소용없는가).
        /// <b>「열기 판정」 전용</b>이다 — 캐릭터 우클릭 게이트의 넷째 항(<c>AppControlDirector.RightClickFanGatePolicy</c>).
        ///
        /// <para><b>왜 필요한가</b>: 옛 게이트는 <see cref="SuppressesPanels"/>(= 「지금 억제 중인가」)를 넷째 항으로 썼다.
        /// 허가가 없는 등급 1에서 그 값은 참이고, 허가는 게이트를 <b>통과한 뒤에야</b> 난다 — 「허가를 받으려면 이미 허가가
        /// 있어야 한다」는 순환이라, 게임이 아닌 전체화면 앱 위에서 캐릭터 우클릭이 조용히 아무 일도 하지 않았다(P1,
        /// <c>docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md</c> §15-16, <c>docs/UX_RIGHTCLICK_FAN_MENU.md</c> 「§10-1 #3 정정」).</para>
        ///
        /// <para><b>식</b>: 지금 허가를 낼 수 있다면 난 것으로 치고 <see cref="SuppressesPanels"/>를 다시 묻는다.
        /// 진리표로는 <paramref name="characterSuspended"/>와 같다 — 등급 2(전체화면 게임)·다른 가상 데스크톱에서만 참.
        /// 그래도 그 인자를 직접 쓰지 않는 이유: 두 정책(<see cref="CanGrant"/> · <see cref="SuppressesPanels"/>) 중 하나가
        /// 바뀌는 날 게이트가 <b>자동으로 따라 움직여야</b> 한다. 직접 쓰면 기준과 대상이 갈라진다.</para>
        ///
        /// <para>★ <b>닫기 소비자에게 쓰지 마라.</b> 표면을 걷을지는 여전히 <see cref="SuppressesPanels"/>
        /// (<c>StickmanAgent.ArePanelsSuppressed</c>)가 정한다. 이 값으로 바꾸면 등급 1 <b>진입 순간의 회수</b>가 사라진다
        /// (원칙 2 회귀, 같은 문서 §16-2b B2). ★ 2026-09-15 (N-20): 사용자가 부르지 않은 자동 표면은 그것을 포함하는
        /// <see cref="SuppressesUnsummonedSurfaces"/>가 정한다 — 역시 이 값으로 바꾸면 안 된다.</para>
        /// </summary>
        public static bool BlocksUserSummon(bool characterSuspended, bool panelRetreatActive,
            bool userSummonGranted)
            => SuppressesPanels(characterSuspended, panelRetreatActive,
                userSummonGranted || CanGrant(characterSuspended, panelRetreatActive));
    }
}
