using StickMate.Interaction;

namespace StickMate.Core
{
    /// <summary>
    /// ★★★ <b>음악에 반응하는 춤은 <u>가장 낮은 우선순위</u>다</b> — 2026-09-03.
    ///
    /// <para>사용자 확정: <i>"다만 문제가 집중모드일때는 춤모션이 아닌 집중모드 모션이 우선이야"</i>.</para>
    ///
    /// ============================================================================
    /// ★★ 함정 하나 — <c>CurrentStateId</c>로 「집중 모드 중」을 판정하면 <b>99.87% 실패한다</b>
    /// ============================================================================
    /// 가장 자연스러워 보이는 구현이 틀렸다. 실측(<c>game-architect</c> §11-11-1):
    /// <code>
    ///   Focus 4종(FocusStart 16 / FocusComplete 17 / FocusCancelled 18 / FocusNudge 19)은
    ///   전부 TimedSpectacleState — "부수 효과가 전혀 없는 순수 타이머"이고,
    ///   StickConfig.pomodoroStartPoseHoldSeconds = 2.0초 뒤 ChangeState(Idle)로 빠진다.
    ///
    ///   25분(1,500초) 세션에서 상태가 Focus*인 시간 = 2 / 1500 = 0.13%
    ///                             상태가 Idle인 시간 = 99.87%
    /// </code>
    /// ⇒ <c>if (agent.CurrentStateId == StickmanStateId.FocusStart)</c>로 막으면 <b>25분 중 2초만
    /// 막히고 나머지 1,498초 동안 캐릭터가 춤춘다.</b> 그리고 <b>이 버그는 테스트에서 안 잡힌다</b> —
    /// 세션 <b>시작 직후</b>를 재면 통과하기 때문이다(성공한 측정과 똑같이 생긴 실패한 측정).
    ///
    /// <para><b>정답 신호는 <see cref="FocusWatchDirector.IsSessionActive"/>다.</b> 세션 시작부터
    /// 종료·취소까지 <b>전 구간에서 true</b>이고, 이미 UI 3곳(톱니 부채꼴 · 집중 팝오버 ·
    /// 집중 렌더러)이 <c>RemainingSeconds</c>와 짝으로 읽고 있다. 다만 그 셋은 전부 <b>UI·렌더링</b>이라,
    /// <b>이 값으로 캐릭터의 행동을 억제하는 소비자는 이 게이트가 처음이다.</b></para>
    ///
    /// ============================================================================
    /// ★ <c>null</c> 기본값이 <see cref="HiddenCharacterCommandGate"/>와 <b>정반대다</b>
    /// ============================================================================
    /// 그 게이트는 <c>player == null → false</c>(=막지 않는다)를 <b>의도적으로</b> 골랐다:
    /// <i>"여기서 true를 돌려주면 «숨어 있어요»가 «배선이 없어요»를 가려 버린다"</i> —
    /// <b>명령 경로에서는 옳다.</b> 사용자가 그 사유를 화면에서 읽기 때문이다.
    ///
    /// <para><b>자동 발동 경로에서는 정반대다.</b> 배선이 없는데 «막지 않는다»로 떨어지면
    /// <b>아무도 안 보는 사이에 춤이 발동한다.</b> 보여줄 사유도, 그것을 읽을 사람도 없다.
    /// 자동 경로의 안전한 방향은 <b>「모르면 안 한다」</b>다.
    /// ⇒ 이 게이트는 <c>BlocksNow</c>를 <b>호출하되</b>, 그 앞에 <c>null → 막는다</c>를 자기 책임으로
    /// 둔다. 발명이 아니라, <b>기존 술어의 문서화된 전제를 다른 문맥에 맞게 감싸는 것</b>이다.</para>
    ///
    /// ============================================================================
    /// ★ 숨김 판정을 <b>다시 쓰지 않는다</b>
    /// ============================================================================
    /// 보이지 않는 캐릭터는 <b>춤도 추면 안 된다</b>. 이유는 <see cref="HiddenCharacterCommandGate"/>에
    /// 이미 적혀 있고 그대로 적용된다 — <i>"보이지 않는 캐릭터가 그라피티를 그리면 상태와 화면이
    /// 갈라지고, 그 상태에서 파생된 말풍선이 주인 없이 뜬다"</i>(불변 원칙 1). 춤도 정확히 같다.
    /// 그 파일이 못박은 규칙(<i>"판정을 두 벌로 만들면 반드시 갈라진다"</i>) 때문에
    /// <b><c>IsSuspended</c>를 여기서 다시 읽지 않고 그 술어를 호출한다.</b>
    ///
    /// ============================================================================
    /// ★★ <b>매 틱 평가해야 한다. 발동 시점 1회가 아니다</b>
    /// ============================================================================
    /// 세션은 <b>춤 도중에 시작될 수 있다</b>(음악 재생 → 춤 시작 → 사용자가 [집중 모드] 클릭).
    /// 발동 시점에만 보면 그 춤은 <b>세션 내내 계속된다</b> = <i>"집중 모드 켰는데 계속 춤춘다"</i>가
    /// 재현 조건이 까다로운 신고로 올라온다. 그래서 이 값은
    /// <see cref="Platform.AudioReactiveDancePolicy.Evaluate"/>의 <c>suppressed</c> 인자로
    /// <b>매 틱 새로 계산해 넣는다</b>(그 정책은 억제가 T₃ 최소 유지보다 이기도록 짜여 있다).
    ///
    /// ============================================================================
    /// 이 게이트가 <b>덮는 것 / 안 덮는 것</b> — 우선순위 표(§11-11-4) 전수 확인
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>캐릭터 숨김</b> — ✓ 덮는다(<see cref="HiddenCharacterCommandGate"/> 재사용).</item>
    ///   <item><b>집중 모드 세션 중</b> — ✓ 덮는다(사용자 확정 2026-09-03).</item>
    ///   <item><b>남의 전체화면</b> — ★ <b>확인 완료(<c>dev-platform</c>, 2026-09-03)</b>:
    ///     <b>등급 2(전체화면 «게임»)는 ①에 이미 완전히 흡수된다.</b>
    ///     <c>StickmanAgent</c>의 축 1이 <c>ForeignFullscreenTierPolicy.SuspendsCharacter(tier)</c>로
    ///     <c>_fullscreenAutoHide</c>를 세우고, <c>ApplySuspendDecision()</c>의
    ///     <c>_fullscreenAutoHide || _userHidden</c>이 그대로 <c>_isSuspended</c>가 되므로
    ///     <see cref="StickmanAgent.IsSuspended"/>가 참이 된다.
    ///     <para><b>등급 1(게임이 아닌 전체화면 앱 — 엑셀·줌·키노트)은 흡수되지 않고, 그것이 옳다.</b>
    ///     등급 1은 <c>_fullscreenPanelRetreat</c>로만 새 나가고
    ///     <see cref="StickmanAgent.ArePanelsSuppressed"/>(표면 축)로만 읽힌다 —
    ///     <c>_isSuspended</c>에 <b>일부러 넣지 않았다</b>(2026-08-31 신고
    ///     <i>"엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림"</i>의 회귀 방지).
    ///     등급 1에서 캐릭터는 <b>계속 보이고 계속 걸어다닌다.</b> 춤은 캐릭터에 붙은 연출이므로
    ///     <b>캐릭터 축을 따라야</b> 하고, 여기에 <c>ArePanelsSuppressed</c>를 얹으면 그 신고가
    ///     이 기능을 통해 부분적으로 되살아난다. <b>얹지 마라.</b></para></item>
    ///   <item><b>사용자 토글 OFF</b> — ✗ 아직 없다. <b>리더 판정(L-5) 대기</b>:
    ///     기본값이 「켬」이면 세이브 스키마가 v10으로 올라가고 하위 호환 테스트가 의무로 붙는다.
    ///     확정되면 이 술어에 항 하나를 더한다(다른 곳에 두 번째 판정을 만들지 마라).</item>
    ///   <item><b>능동 연출 진행 중</b>(활쏘기·가출 등) — ✗ 여기 없다. 그건 <b>상태 전이 소유권</b>
    ///     문제라 <c>design-motion</c>·<c>coder</c> 소관이고, 이 게이트는 <b>발동 금지</b>만 말한다.</item>
    /// </list>
    /// </summary>
    public static class AudioReactiveDanceGate
    {
        /// <summary>
        /// 지금 춤 발동을 막아야 하는가. <b>매 틱 부른다</b>(위 문단).
        ///
        /// <para><b>배선이 없으면(<c>null</c>) 막는다</b> — 자동 경로의 안전한 방향은
        /// 「모르면 안 한다」다. <see cref="HiddenCharacterCommandGate.BlocksNow"/>와 기본값이
        /// 반대인 것은 실수가 아니라 문맥 차이다(클래스 문서).</para>
        /// </summary>
        public static bool BlocksNow(StickmanAgent player, FocusWatchDirector focus)
        {
            if (player == null || focus == null) return true;
            if (HiddenCharacterCommandGate.BlocksNow(player)) return true;
            return focus.IsSessionActive;
        }
    }
}
