namespace StickMate.Platform
{
    /// <summary>
    /// <b>"지금 이 화면을 사용자가 실제로 보고 있는가"</b> 하나만 판정하는 순수 규칙.
    /// UnityEngine 의존도 P/Invoke도 <b>한 줄 없다</b> — 그래야 Windows 실기가 없는 이 개발 머신에서
    /// 규칙 자체를 실행해 검증할 수 있고, 무엇보다 <b>양 플랫폼이 같은 규칙을 부를 수 있다.</b>
    ///
    /// <para>★ 이 파일이 <c>Platform/</c> 바로 아래 있는 것은 취향이 아니라 <b>사고 재발 방지</b>다.
    /// <c>FullscreenSuspendPolicy</c>가 한때 <c>Platform/MacOS/</c> 안에 있었고, 그 자리에 있는 동안
    /// Windows 구현은 같은 규칙을 <b>부를 수조차 없었다</b>. 이 파일을 플랫폼 폴더로 옮기지 마라 —
    /// <c>PlatformParityAuditTests.세션_가시성_정책은_플랫폼_중립_위치에_있다()</c>가 막는다.</para>
    ///
    /// ============================================================================
    /// 무엇을 고치는가 (docs/platform/GHOST_FOOTHOLDS.md 2절)
    /// ============================================================================
    /// 잠금 화면이 떠 있는 동안에도 앱은 계속 돌고 <c>footholdPollInterval</c>(0.3초)마다 창을 계속
    /// 열거한다. 그 시간에 열거되는 집합은 사용자가 잠금을 풀었을 때 볼 화면과 아무 관계가 없다.
    /// 그래서 <b>유령 발판</b>(잠금 UI를 딛는다)과 <b>발판 전멸</b>(잠금 UI가 다 가려 후보가 0이 된다)이
    /// 동시에 열려 있고, 24시간 상주 앱의 유휴 비용도 함께 낭비된다.
    ///
    /// ============================================================================
    /// ★★ 역방향 비대칭 — <b>한 줄이 양쪽에서 각각 다른 다리로 선다</b>
    /// ============================================================================
    /// <code>
    ///           | DisplayAsleep                     | SessionLocked
    ///   --------+-----------------------------------+------------------------------------------
    ///   macOS   | 채워짐 (CGDisplayIsAsleep)         | 항상 false — 문서화된 수단이 없다
    ///   Windows | 항상 false — 창 프로시저가 필요해   | 채워짐 (WTSQuerySessionInformation
    ///           |   포기했다                          |            + OpenInputDesktop)
    /// </code>
    /// <b>어느 한쪽 다리를 "이 플랫폼에서는 항상 false네, 정리하자"고 지우면 그 플랫폼에서 이 기능이
    /// 통째로 사라진다.</b> 두 열은 서로의 사각지대를 덮고 있고, 그래서 <b>둘 다 OR에 남아 있어야
    /// 한다.</b> 각 칸이 왜 그렇게 됐는지는 해당 서비스 파일의 클래스 문서에 적혀 있다:
    /// Windows <c>WindowsViewerPresenceService</c>(DisplayAsleep을 포기한 사유) /
    /// macOS <c>MacViewerPresenceService</c>(비문서 키를 배제한 사유).
    /// ★ <c>see cref</c>로 걸지 않는 것은 의도다 — 두 타입 모두 파일 전체가 <c>#if</c> 안이라
    /// 반대 타깃에서는 <b>타입이 존재하지 않는다</b>(활성 빌드 타깃 사각지대).
    ///
    /// ============================================================================
    /// 보수 규칙 — 오판의 대가가 비대칭이다
    /// ============================================================================
    /// 조회 실패·해석 불가는 <b>전부 "사용자가 보고 있다"</b>로 간다.
    /// <list type="bullet">
    /// <item>잘못 멈추면 → 사용자가 <b>얼어붙은 캐릭터</b>를 본다(신고 대상, 되돌리기 어렵다).</item>
    /// <item>잘못 안 멈추면 → 전기를 조금 더 쓴다(지금까지의 동작 그대로).</item>
    /// </list>
    /// <c>IsCloaked</c>·<c>hasVirtualScreen</c>·<c>MacViewerPresenceService</c>의 "작은 값을 믿는다"와
    /// <b>같은 원칙</b>이다.
    /// </summary>
    public static class SessionVisibilityPolicy
    {
        /// <summary>
        /// <b>보조 신호(<c>OpenInputDesktop</c> 실패 = 보안 데스크톱)를 얼마나 오래 믿을 것인가</b>(초).
        ///
        /// <para>왜 시한이 필요한가: 주 신호(WTS)가 "잠기지 않음"이라고 답하는데 보조 신호만 계속
        /// "보안 데스크톱"이라고 답하는 상태는 원래 <b>UAC 프롬프트</b>다 — 사용자가 예/아니오를
        /// 누르면 끝나는 짧은 사건이다. 그런데 어떤 환경(보안 소프트웨어, 제한된 잡/데스크톱 권한)에서
        /// <c>OpenInputDesktop</c>이 <b>영구히</b> 실패하면, 시한이 없을 경우 발판 스캔이 <b>영원히</b>
        /// 멈춘 채 낡은 캐시로 굳는다. 그것은 이 라운드가 고치려는 버그를 스스로 만드는 것이다.</para>
        ///
        /// <para>300초인 이유: UAC 프롬프트를 띄워 놓고 자리를 비운 사람도 덮을 만큼 넉넉하되,
        /// 영구 고착을 "몇 분"으로 잘라 낸다. 잠금 화면은 이 시한과 무관하다 — 그쪽은 주 신호(WTS)가
        /// 직접 잠김을 보고하므로 몇 시간이든 정상 동작한다.</para>
        /// </summary>
        public const float SecureDesktopTrustSeconds = 300f;

        /// <summary>
        /// <b>이번 프레임에 발판 재열거를 통째로 건너뛸 것인가.</b>
        ///
        /// <para>이 함수가 참을 돌려줘도 <b>호출부는 캐시를 절대 비우지 않는다</b> — 비우는 순간 그것이
        /// 곧 발판 전멸이고, 고치려던 것을 스스로 만드는 셈이 된다
        /// (<c>FootholdPoller.Tick</c>의 주석 참고).</para>
        /// </summary>
        public static bool ShouldSuspendFootholdScan(in ViewerPresenceSnapshot snapshot)
        {
            // 관측 자체가 실패했으면 "모름"이고, 모름은 "보고 있다"로 간다(위 보수 규칙).
            if (!snapshot.Valid) return false;

            // ★ 이 OR의 두 항은 플랫폼마다 서로 다른 쪽이 채워진다. 하나를 지우면 그 플랫폼에서
            //   기능이 사라진다(클래스 문서의 표).
            return snapshot.DisplayAsleep || snapshot.SessionLocked;
        }

        /// <summary>
        /// 로그에 찍을 사유. <b>상수 문자열만 돌려주므로 할당이 0</b>이다(24시간 상주 앱 컨벤션).
        /// 두 다리 중 <b>어느 쪽이 섰는지</b>가 남아야 실기 로그로 플랫폼 배선을 확인할 수 있다 —
        /// 이 개발 머신에 Windows가 없어서 그 로그가 사실상 유일한 확인 수단이다.
        /// </summary>
        public static string DescribeSuspendReason(in ViewerPresenceSnapshot snapshot)
        {
            if (!snapshot.Valid) return "관측실패";
            if (snapshot.DisplayAsleep && snapshot.SessionLocked) return "화면꺼짐+세션잠금";
            if (snapshot.DisplayAsleep) return "화면꺼짐";
            if (snapshot.SessionLocked) return "세션잠금";
            return "없음";
        }

        /// <summary>
        /// 보조 신호(보안 데스크톱)를 아직 믿어도 되는가 — <see cref="SecureDesktopTrustSeconds"/> 참고.
        ///
        /// <para>규칙 자체는 Windows에서만 쓰이지만 <b>판정은 여기(플랫폼 중립)에 둔다.</b>
        /// <c>Platform/Windows/</c> 안에 두면 이 머신에서 한 번도 컴파일되지 않고 한 줄도 실행되지
        /// 않는다 — 즉 <b>검증이 불가능한 자리</b>다. 사실 조회(<c>OpenInputDesktop</c>의 성패)만
        /// 그쪽에 남긴다.</para>
        ///
        /// <para>음수/NaN(= 시각을 아직 모른다)은 <b>믿지 않는 쪽</b>으로 떨어뜨린다. "모르면 멈추지
        /// 않는다"가 이 파일 전체의 보수 방향이기 때문이다.</para>
        /// </summary>
        public static bool ShouldTrustSecureDesktopSignal(float secondsSinceSecureDesktopBegan)
        {
            // NaN은 두 비교 모두 false이므로 여기서 자동으로 "믿지 않음"이 된다.
            return secondsSinceSecureDesktopBegan >= 0f
                && secondsSinceSecureDesktopBegan <= SecureDesktopTrustSeconds;
        }
    }
}
