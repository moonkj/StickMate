namespace StickMate.Core
{
    /// <summary>
    /// ★★ 연출(스펙터클)이 <b>어떻게 끝났는가</b>를 도착 상태 하나로 판정하는 단 하나의 지점
    /// (2026-09-02 — 절대 불변 원칙 1/3 위반 수정).
    ///
    /// ============================================================================
    /// 무엇이 버그였나
    /// ============================================================================
    /// 그림을 그리다 <b>미끄러져 발판 밖으로 떨어져도</b> 시스템은 "정상 완료"로 기록했다.
    /// <c>GraffitiDirector.OnStateTransitioned</c>가 <c>From == Graffiti &amp;&amp; _hasRegion</c>만 보고
    /// <c>Completed</c>를 발행하고 <b>10분 쿨다운까지 걸었기</b> 때문이다. 사용자가 실제로 본 것은
    /// "낙서하다 굴러떨어짐"인데 기록은 "낙서 완료"였다 — 행동과 기록이 어긋난다.
    ///
    /// <para><b>전수 조사 결과 같은 형태가 4개 디렉터에 있었다</b>(2026-09-02, coder):</para>
    /// <list type="bullet">
    ///   <item><c>GraffitiDirector</c> — Fall/Ragdoll 둘 다 Completed + 쿨다운.</item>
    ///   <item><c>DesktopIconMirrorDirector</c>(DesktopTidy/BlackholeSummon) — 같음.</item>
    ///   <item><c>WindowTheftDirector</c> — Ragdoll은 <c>IsForcedInterrupt</c>로 걸렀지만
    ///         <b>Fall은 Completed</b>였다(발판 상실 전이는 강제 인터럽트가 아니다).</item>
    ///   <item><c>ArcheryDirector</c> — 위와 같은 형태.</item>
    /// </list>
    /// 즉 <c>IsForcedInterrupt</c>만으로는 부족하다. 그 플래그는 "<b>누가</b> 끊었는가"(외부 개입)를
    /// 말하고, 이 판정은 "<b>어디로</b> 나갔는가"(물리적 이탈)를 말한다 — 두 축은 독립이다.
    ///
    /// ============================================================================
    /// 왜 도착 상태 목록인가 (그리고 왜 이 목록이 안전한가)
    /// ============================================================================
    /// 연출 상태의 <b>정상</b> 종료는 전부 <c>Idle</c>이다(TimedSpectacleState / ArcheryState 모두
    /// 타이머 만료 시 <c>ChangeState(Idle)</c>). 그래서 아래 넷은 "연출이 스스로 끝난 것이 아니라
    /// 몸이 그 자리에서 밀려났다"는 뜻이며, 이 목록에 <b>거짓 양성이 들어올 경로가 없다</b>:
    /// <c>GroundLossHang</c> 승격은 <c>Idle/Walk</c>에서만 일어나고
    /// (<c>StickmanBlackboard.TryEnterGroundLossHang</c>), <c>Ragdoll</c>은 항상 외력이며,
    /// <c>ThrowTumble</c>은 유저가 집어던진 것이고, <c>Fall</c>은 발판 상실/화면 이탈/스냅 상한 초과뿐이다.
    ///
    /// <para>이 판정을 <c>Interaction/</c>이 아니라 <c>Core/</c>에 두는 이유는 이 저장소의 정책 배치
    /// 규칙 그대로다 — <b>정책은 중립 위치에, 호출부는 사실 조회만</b>. 디렉터마다 같은 목록을
    /// 복사하면 다음에 상태가 하나 늘 때 네 곳 중 세 곳만 고쳐진다(이 프로젝트에서 반복된 실패 유형).</para>
    /// </summary>
    public static class SpectacleExitClassification
    {
        /// <summary>
        /// 연출 상태에서 <paramref name="to"/>로 나간 것이 <b>비정상 이탈</b>(= 연출이 완수되지 못하고
        /// 몸이 그 자리에서 밀려난 것)인가.
        ///
        /// <para>true이면 호출부는 <c>Completed</c>가 아니라 <c>Cancelled</c>를 발행하고
        /// <b>쿨다운을 걸지 않는다</b>. 쿨다운은 "방금 충분히 보여줬으니 한동안 쉬자"는 뜻인데
        /// 보여주다 만 연출에 그걸 걸면 사용자는 실패한 연출 하나 때문에 10~15분을 기다리게 된다.</para>
        /// </summary>
        public static bool IsAbnormalExit(StickmanStateId to)
        {
            switch (to)
            {
                case StickmanStateId.Fall:           // 발판 상실 / 화면 좌우 이탈 / 접지 스냅 상한 초과.
                case StickmanStateId.Ragdoll:        // 외력으로 넘어짐(아키텍처 0절).
                case StickmanStateId.ThrowTumble:    // 유저가 집어던졌다.
                case StickmanStateId.GroundLossHang: // 발판 상실 유예 — 이 프레임부터 이미 공중이다.
                    return true;
                default:
                    return false;
            }
        }
    }
}
