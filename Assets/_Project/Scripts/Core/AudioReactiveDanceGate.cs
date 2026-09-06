using StickMate.Interaction;
using UnityEngine;

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
    ///   <item><b>남의 전체화면</b> — ★★★ <b>2026-09-06 리더 판정으로 결론이 바뀌었다. 아래 2026-09-03
    ///     문단은 «왜 그때 그렇게 판단했는가»의 기록으로 남긴다.</b>
    ///     <para><b>지금 규칙</b>: 등급 1(게임이 아닌 전체화면 앱 — 줌·팀즈·키노트)에서
    ///     <b>자동으로 발동하는 춤만</b> 막는다. <b>캐릭터는 한 비트도 건드리지 않는다</b>
    ///     (<see cref="StickmanAgent.IsSuspended"/>는 여전히 거짓이고 캐릭터는 계속 걸어다닌다).
    ///     읽는 값은 <see cref="StickmanAgent.IsForeignFullscreenAppPresent"/>이며,
    ///     <b><see cref="StickmanAgent.ArePanelsSuppressed"/>가 아니다</b> — 그쪽에는
    ///     «사용자 임대» 항이 붙어 있어 <i>설정창을 여는 동안만 춤이 되살아나는</i> 결함이 된다.</para>
    ///     <para><b>왜 뒤집었나</b>: macOS 프로브는 «소리»가 아니라 <b>«출력 스트림이 열렸는가»</b>를
    ///     본다. 회의 앱은 발표를 시작하는 순간 스트림을 열어 두므로 <b>아무 소리가 없는 회의에서도</b>
    ///     게이트가 열린다. 프로브의 근본 한계는 이번 라운드에 못 고치지만, 그 앱들이 거의 항상
    ///     전체화면이라는 사실이 값싼 상관 신호를 준다.</para>
    ///     <para><b>2026-08-31 신고의 회귀가 아닌 이유</b>: 그 신고는 <i>"캐릭터가 없어져버림"</i>이고,
    ///     그 회귀를 막는 함수는 <c>ForeignFullscreenTierPolicy.SuspendsCharacter</c> 하나인데
    ///     <b>그 함수는 한 글자도 안 바뀌었다</b>. 여기서 바뀌는 것은 «스스로 시작하는 춤» 하나뿐이고,
    ///     그것을 원치 않는 사용자는 아래 «이번 세션만 끄기»로 즉시 되돌릴 수 있다.</para>
    ///     ---- 이하 2026-09-03 기록 ----
    ///     <b>확인 완료(<c>dev-platform</c>, 2026-09-03)</b>:
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
    ///   <item><b>사용자 토글 OFF</b> — ★★★ <b>2026-09-06 리더 판정 완료. 「이번 세션만 끄기」로 간다</b>
    ///     (<see cref="MutedForThisSession"/>). 세이브에 <b>내려가지 않는다</b>.
    ///     <para><b>왜 비영속인가</b>: 이 토글이 존재하는 이유는 «발표·회의 중에 자동 연출을 끈다»이고,
    ///     발표와 회의는 <b>세션 안의 사건</b>이다. 영속시키면 (가) 세이브 스키마가 v11로 올라가
    ///     하위 호환 테스트가 의무로 붙고, (나) 무엇보다 <b>사용자가 반년 전에 끈 것을 잊고
    ///     «춤 기능이 고장났다»고 신고하는 경로</b>가 생긴다 — 이 저장소가
    ///     <c>gearIconVisible</c>에서 이미 겪은 형태다(되돌리는 문이 없는 저장 항목 금지, 41-8).
    ///     앱을 껐다 켜면 원래대로 돌아온다는 것이 <b>그 자체로 탈출구</b>다.</para>
    ///     <para>★ 판정은 <b>여기 한 곳</b>에만 있다. 이 값을 다른 곳에서 다시 읽어 분기하지 마라.</para></item>
    ///   <item><b>능동 연출 진행 중</b>(활쏘기·가출 등) — ✗ 여기 없다. 그건 <b>상태 전이 소유권</b>
    ///     문제라 <c>design-motion</c>·<c>coder</c> 소관이고, 이 게이트는 <b>발동 금지</b>만 말한다.</item>
    /// </list>
    /// </summary>
    public static class AudioReactiveDanceGate
    {
        /// <summary>
        /// ★ <b>「이번 세션만 끄기」가 켜져 있는가</b>(리더 판정 2026-09-06).
        /// <c>true</c>면 음악에 반응한 <b>자동</b> 춤이 전부 막힌다.
        ///
        /// <para><b>세이브에 내려가지 않는다.</b> 앱을 다시 켜면 <c>false</c>로 돌아온다 —
        /// 그것이 «되돌리는 문»이다(클래스 문서 ④). 세이브 스키마는 한 비트도 안 바뀌므로
        /// 기존 세이브가 그대로 읽히고 하위 호환 테스트가 새로 붙지 않는다.</para>
        ///
        /// <para>★ 이 값은 <b>자동 발동만</b> 막는다. 사용자가 직접 시킨 동작은 이 게이트를
        /// 지나지 않는다(이 게이트를 읽는 곳은 1층 감독과 2층 에피소드 감독뿐이다).</para>
        /// </summary>
        public static bool MutedForThisSession { get; private set; }

        /// <summary>
        /// 「이번 세션만 끄기」를 세운다. 설정창이 부르는 유일한 진입점이다.
        ///
        /// <para><paramref name="source"/>는 <b>로그에만</b> 쓴다 — 사유 문자열로 분기하지 않는다
        /// (문구가 바뀌는 날 조용히 갈래가 사라진다).</para>
        /// </summary>
        public static void SetMutedForThisSession(bool muted, string source)
        {
            if (MutedForThisSession == muted) return;
            MutedForThisSession = muted;
            Debug.Log($"[음악춤] 음악 반응 춤을 «이번 세션만» {(muted ? "껐습니다" : "다시 켰습니다")}" +
                $"(요청: {source}). 이 설정은 저장되지 않습니다 — 앱을 다시 켜면 켜진 상태로 돌아옵니다." +
                (muted ? " 진행 중이던 춤은 다음 프레임에 정상 퇴장합니다." : string.Empty));
        }

        /// <summary>테스트 격리용. 정적 값은 도메인 리로드를 끈 에디터에서 세션을 넘어 남는다.</summary>
        public static void ResetForTesting() => MutedForThisSession = false;

        /// <summary>
        /// 지금 춤 발동을 막아야 하는가. <b>매 틱 부른다</b>(위 문단).
        ///
        /// <para><b>배선이 없으면(<c>null</c>) 막는다</b> — 자동 경로의 안전한 방향은
        /// 「모르면 안 한다」다. <see cref="HiddenCharacterCommandGate.BlocksNow"/>와 기본값이
        /// 반대인 것은 실수가 아니라 문맥 차이다(클래스 문서).</para>
        ///
        /// <para>★ 항의 순서는 <b>싼 것부터</b>다. 세션 토글은 정적 bool 하나이고, 전체화면 축은
        /// 이미 계산돼 있는 필드 읽기이며, 숨김 술어만 함수 호출이다.</para>
        /// </summary>
        public static bool BlocksNow(StickmanAgent player, FocusWatchDirector focus)
        {
            if (player == null || focus == null) return true;
            if (MutedForThisSession) return true;
            if (HiddenCharacterCommandGate.BlocksNow(player)) return true;
            // 등급 1 이상(남의 전체화면 앱)에서는 «스스로 시작하는 춤»만 막는다 — 캐릭터는 그대로다.
            // 규칙 본문은 Platform/FullscreenSuspendPolicy의 SuppressesAutoDance에 있다.
            if (player.IsForeignFullscreenAppPresent) return true;
            return focus.IsSessionActive;
        }
    }
}
