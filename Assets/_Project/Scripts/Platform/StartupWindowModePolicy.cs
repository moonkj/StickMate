namespace StickMate.Platform
{
    /// <summary>
    /// 오버레이 창을 실제로 들고 있는 호스트 플랫폼. <b>사실</b>이고 판정이 아니다 —
    /// 값을 고르는 것은 각 플랫폼 파일(자기 자신이 누구인지만 말한다)이고,
    /// 그 값으로 <b>무엇을 할지</b>는 <see cref="StartupWindowModePolicy"/>가 정한다.
    ///
    /// <para><see cref="Unknown"/>은 에디터·헤드리스·앞으로 추가될 타깃(iPad/iPhone 스크린샷 백드롭 모드)이다.
    /// 모르는 플랫폼은 <b>지금까지의 동작</b>을 받는다(아래 정책의 기본값).</para>
    /// </summary>
    public enum OverlayHostPlatform
    {
        Unknown = 0,
        Windows = 1,
        MacOS = 2,
    }

    /// <summary>
    /// ★ 2026-09-28 — <b>기동 시 창 모드를 언제 창모드로 내릴 것인가</b>의 순수 판정.
    /// OS 호출도 UnityEngine 의존도 없다(플랫폼 중립 위치 = <c>Platform/</c>, CLAUDE.md
    /// "정책 판정은 플랫폼 중립 위치에, 플랫폼 전용 코드는 사실 조회만").
    ///
    /// ============================================================================
    /// 무슨 신고를 고치는가 — 「stickmate 흰화면이야 상단에 써있음」
    /// ============================================================================
    /// 사용자 실기 신고(2026-09-28, Windows, windows-preview-20260928a):
    /// 「시작할때 전체 흰화면이 계속 켜져있다가 꺼짐 잠깐깜박깜박하고」 ·
    /// 「stickmate 흰화면이야 상단에 써있음」.
    ///
    /// <para>흰 배경 자체는 <b>이미 원인이 확정된 기존 결함</b>이다(2026-09-07 실기 확정, 수정
    /// <c>d443eae</c>로 최대 0.5초만 짧아졌다). 이번 신고가 <b>새로 보탠 사실</b>은 「상단에
    /// StickMate라고 써 있다」 한 줄이고, 그 제목표시줄의 정체가 이 파일이 고치는 것이다.</para>
    ///
    /// <para><b>왜 제목표시줄이 보이는가</b>: 패키지의 <c>UniWindowController.forceWindowed</c>는
    /// <c>Awake</c>에서 <c>Screen.fullScreen = false</c>를 건다. 그런데 창을 실제로 붙잡는 것
    /// (<c>AttachMyWindow</c>)과 테두리를 없애는 것(<c>SetBorderless</c>)은 <b>첫 <c>Update</c> 이후</b>다.
    /// 그 사이 구간에서 창은 <b>이미 창모드</b>(= 제목표시줄 있음)인데 <b>아직 투명이 아니다</b> —
    /// 그래서 사용자는 근백색 불투명 창 위에 우리 제품명이 적힌 캡션을 읽는다.</para>
    ///
    /// <para><b>그리고 그 조기 전환은 Windows에서 중복이다</b>: 부착이 확인되는 프레임에
    /// <c>WindowsOverlayStateEnforcer.TickFullScreenBounds()</c>가 <c>Screen.SetResolution(..., Windowed)</c>로
    /// 같은 일을 <b>다시</b> 한다(2026-09-07 <c>d443eae</c> 이후 같은 프레임에 1회가 보장된다).
    /// 즉 조기 전환의 유일한 순효과가 <b>「캡션이 보이는 구간을 만드는 것」</b>이다.</para>
    ///
    /// ============================================================================
    /// ★ 그래서 왜 씬 값을 그냥 false로 바꾸지 않는가 (리더 판정 2026-09-28)
    /// ============================================================================
    /// 씬 에셋(<c>Main.unity</c>)의 <c>forceWindowed</c>는 <b>두 플랫폼이 공유하는 한 값</b>이다.
    /// 그것을 false로 구우면 macOS도 함께 바뀌는데, <b>부착이 영영 실패하는 환경</b>에서 두 플랫폼의
    /// 결과가 다르다:
    /// <list type="bullet">
    ///   <item><b>Windows</b> — 전체화면 창이 남아도 <b>트레이 아이콘</b>이 살아 있다.
    ///     <c>WindowsSystemTrayIcon.Tick</c>은 Enforcer의 부착 판정·컨트롤러 조기 반환보다 <b>위</b>에서
    ///     돌기 때문이다(그 자리에 둔 이유가 정확히 "부착이 영영 실패한 환경에서야말로 유일한 탈출구").
    ///     ★ 다만 트레이는 <c>SystemTrayPresencePolicy.OptOutEnvironmentVariable</c>로 <b>끌 수 있다</b> —
    ///     그래서 트레이 하나에 기대지 않고 아래 (2)를 이 변경의 부품으로 함께 넣는다.</item>
    ///   <item><b>macOS</b> — 전체화면 창이 남으면 <b>메뉴바까지 덮인다</b>. 대응 트레이(NSStatusItem)는
    ///     이 저장소에 아직 없다(<c>SystemTrayPresencePolicy.MacOsGapReason</c>). 즉 macOS에서 조기 전환을
    ///     빼는 것은 <b>탈출구를 줄이는</b> 변경이다.</item>
    /// </list>
    /// ⇒ 그래서 <b>런타임에 플랫폼으로 가른다</b>. 씬 에셋은 한 줄도 건드리지 않는다(추적 파일이고 공유다).
    ///
    /// ============================================================================
    /// 이 파일이 정하는 것 둘 — <b>둘 중 정확히 하나</b>가 일어난다
    /// ============================================================================
    /// <list type="number">
    ///   <item><see cref="ShouldForceWindowedAtStartup"/> — <b>조기</b> 전환(패키지 <c>Awake</c>).
    ///     활성화 <b>전</b>에 <c>forceWindowed</c>에 대입해야 뜻이 있다(활성화 순간 <c>Awake</c>가 동기로 돈다).</item>
    ///   <item><see cref="ShouldReleaseFullscreenAfterAttachFailure"/> — <b>지연</b> 전환.
    ///     부착 제한 시간이 지나도 창을 못 붙잡았을 때 그때 창모드로 내려 <b>제목표시줄을 되돌린다</b>
    ///     (= 사용자가 창을 옮기고 닫을 수 있는 상태). 조기 전환을 뺀 플랫폼의 <b>탈출구</b>다.</item>
    /// </list>
    ///
    /// <para><b>불변식</b>: 모든 플랫폼에서 두 값은 <b>서로 배타적</b>이다(정확히 하나만 참).
    /// 조기 전환을 하는 플랫폼은 이미 창모드이므로 지연 전환이 할 일이 없고, 조기 전환을 뺀 플랫폼은
    /// 지연 전환이 <b>반드시</b> 있어야 탈출구가 0이 되지 않는다. 그래서 두 번째 판정은 첫 번째의
    /// <b>부정으로 정의한다</b> — 값을 두 벌로 적으면 한쪽만 바뀌어 조용히 어긋날 수 있고, 그 어긋남의
    /// 나쁜 쪽이 "탈출구 0"이다. <c>Tests/EditMode/StartupWindowModeSplitTests</c>가 이 배타성과
    /// 씬 값·호출 순서까지 관계로 잠근다.</para>
    ///
    /// ============================================================================
    /// ★ 정직한 한계 — 이 변경이 <b>하지 않는 것</b>
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>흰 배경을 없애지 못한다.</b> 캡션만 사라진다. 흰 구간의 길이는 부착까지의 시간이
    ///     지배하고, 그것을 줄이는 것은 별건(리더 배정 대기: 「부착 전 표시 억제」 설계)이다.</item>
    ///   <item><b>흰 구간이 최대 한 번의 재적용 주기만큼 늘어날 수 있다.</b> 조기 전환이 없으면 창모드
    ///     전환이 부착 프레임의 <c>SetResolution</c>으로 미뤄지고, 그 적용은 <b>프레임 끝</b>이다.
    ///     투명·보더리스 재적용은 그 다음 재적용 회차(<c>OverlayStateReapplyPolicy.ReapplyIntervalSeconds</c>)에
    ///     다시 걸린다. <b>실기 미확인</b>이며, 바꾸는 쪽(캡션 제거)이 사용자가 실제로 신고한 것이다.</item>
    ///   <item><b>Windows 레지스트리 복원값을 이기지 못한다.</b> Unity 플레이어는 직전 세션의 해상도·모드를
    ///     복원한다. 직전 세션이 창모드로 끝났다면 <b>첫 프레임부터 창모드</b>이고, 그 경우 이 변경은
    ///     캡션을 없애지 못한다(전체화면으로 뜬 세션에서만 효과가 있다). 그 경계는 실기 로그의
    ///     <c>[렌더진단] 콜드스타트</c> 줄의 <c>창모드=</c> 값으로만 갈린다.</item>
    /// </list>
    /// </summary>
    public static class StartupWindowModePolicy
    {
        /// <summary>
        /// 패키지의 기동 시 전체화면 <b>조기</b> 해제(<c>UniWindowController.forceWindowed</c>)를 켤 것인가.
        ///
        /// <para>Windows만 false다 — 그 플랫폼에서만 (a) 부착 프레임에 Enforcer가 같은 전환을 이미 하고,
        /// (b) 전환이 먼저 일어나면 부착까지의 구간 내내 제목표시줄이 노출되며, (c) 부착이 영영 실패해도
        /// 아래 지연 해제와 트레이 아이콘이 탈출구로 남는다.</para>
        ///
        /// <para>Windows가 아닌 값은 전부 true — <b>지금까지의 동작</b>이다(씬 에셋에 구워진 값과 같다).
        /// 모르는 플랫폼에 새 동작을 주지 않는다.</para>
        /// </summary>
        public static bool ShouldForceWindowedAtStartup(OverlayHostPlatform platform)
            => platform != OverlayHostPlatform.Windows;

        /// <summary>
        /// 부착 제한 시간이 지나도 창을 못 붙잡았을 때 <b>그때</b> 전체화면을 풀 것인가(= 탈출구).
        ///
        /// <para><b>위 판정의 부정으로 정의한다</b> — 클래스 문서의 배타성 불변식 참고. 조기 해제를 한
        /// 플랫폼은 이미 창모드라 할 일이 없고, 조기 해제를 뺀 플랫폼은 이것이 유일한 창 되돌리기다.</para>
        /// </summary>
        public static bool ShouldReleaseFullscreenAfterAttachFailure(OverlayHostPlatform platform)
            => !ShouldForceWindowedAtStartup(platform);
    }
}
