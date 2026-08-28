namespace StickMate.Platform
{
    /// <summary>
    /// 전역 단축키 조회에 필요한 최소한의 키 목록. 실제 OS 키코드는 각 플랫폼 구현체가 내부에서
    /// 매핑한다(이 열거형은 플랫폼 중립 — Core/Interaction 레이어가 macOS 가상 키코드 숫자를
    /// 직접 알 필요가 없다).
    ///
    /// 왜 "필요한 것만" 있는가: 이 채널의 목적은 범용 키보드 입력이 아니라 **앱 제어용 단축키 하나**다.
    /// 전체 키맵을 노출하면 조회 전용이라 해도 사실상 키로거의 형태가 되어, CLAUDE.md 비침해 원칙과
    /// 어긋난다. 조합키 3개 + 동작키 4개로 범위를 못박아 그 위험 자체를 구조적으로 없앤다.
    /// </summary>
    public enum GlobalKey
    {
        Command,
        Option,
        Control,
        Q,
        C,
        D,
        R,

        /// <summary>말풍선 즉시 발화 데모(Ctrl+Opt+Cmd+B) — Interaction/AppControlDirector.cs.</summary>
        B,

        /// <summary>라이벌 스틱맨 강제 스폰 데모(Ctrl+Opt+Cmd+V) — Interaction/AppControlDirector.cs.</summary>
        V,

        /// <summary>격파 미니게임 강제 발동 데모(Ctrl+Opt+Cmd+K, "격파"/breaK) — 자동 발동이 60초마다
        /// 5% 추첨이라 확률만으로는 검증이 사실상 불가능하다. Interaction/AppControlDirector.cs.</summary>
        K,

        /// <summary>그라피티 강제 발동 데모(Ctrl+Opt+Cmd+G, Graffiti) — 자동 발동이 60초마다 4% 추첨 +
        /// 10분 쿨다운이라 K와 같은 이유로 강제 경로가 필요하다. Interaction/AppControlDirector.cs.</summary>
        G,

        /// <summary>창 도둑 강제 발동 데모(Ctrl+Opt+Cmd+T, Theft) — 자동 발동이 60초마다 3% 추첨 +
        /// 15분 쿨다운이라 K/G와 같은 이유로 강제 경로가 필요하다. Interaction/AppControlDirector.cs.</summary>
        T,

        /// <summary>윈도우 크래시 강제 발동 데모(Ctrl+Opt+Cmd+X, 부서짐) — 자동 발동이 60초마다 2% 추첨 +
        /// 25분 쿨다운으로 이 프로젝트의 모든 스펙터클 중 가장 희소해(27-4: 파괴 연출은 더 드물어야 한다)
        /// 확률만으로는 실물 검증이 사실상 불가능하다. Interaction/AppControlDirector.cs.</summary>
        X,

        /// <summary>PC 하드웨어 반응 데모 미리보기(Ctrl+Opt+Cmd+H, Hardware) — 다른 데모와 성격이 다르다.
        /// 확률을 건너뛰는 게 아니라 "실제로는 일어나지 않은 신호의 연출만" 4종 순환 미리보기하는
        /// 경로이며(배터리를 20%로 만드는 것은 원칙 3/27-7이 금지하는 OS 제어다), 짧게 표시되고 스스로
        /// 걷힌다. Interaction/HardwareReactionDirector.ForceTriggerNow 문서 참고.</summary>
        H,
    }

    /// <summary>
    /// "지금 이 키가 눌려 있는가"를 **창 포커스와 무관하게** 조회하는 채널
    /// (IGlobalPointerButtonService의 키보드 판 — 같은 이유로 IPlatformWindowService에서 분리되어 있고,
    /// 지원하지 않는 플랫폼은 아예 구현하지 않는다. 소비 측은 `as IGlobalKeyStateService`로 판정).
    ///
    /// ============================================================================
    /// 왜 필요한가 — "터미널 없이는 앱을 끌 수조차 없다"(2026-08-28 리더 지시)
    /// ============================================================================
    /// 이 앱의 창은 클릭 관통 상태라 클릭으로 포커스를 줄 수 없다. Unity의 <c>Input.GetKeyDown</c>은
    /// 우리 창이 키보드 포커스를 가진 동안만 동작하므로(Core/StickmanAgent의 Escape 긴급 해제가 가진
    /// 바로 그 한계), 사용자가 다른 앱을 한 번 클릭한 뒤에는 앱 안에서 무엇도 되돌릴 수 없었다.
    /// 유일한 종료 수단이 터미널 <c>kill</c>이라는 것은 실사용 앱으로서 치명적이다.
    ///
    /// **권한에 대하여(중요, 실측 확인함)**: macOS 구현은 <c>CGEventSourceKeyState</c>를 쓴다. 이는
    /// 이미 이 프로젝트가 마우스 버튼 조회에 쓰고 있는 <c>CGEventSourceButtonState</c>와 정확히 같은
    /// 계열의 **조회 전용 공개 C ABI**이며, 이벤트를 가로채는 <c>CGEventTap</c>과 달리 접근성
    /// (Accessibility) 권한을 요구하지 않는다. 2026-08-28에 이 프로젝트 환경에서 직접 실측했다:
    /// 권한 부여 없이 호출했을 때 크래시 없이 false를 돌려주었고, 세션에 실제로 키 이벤트가 들어오자
    /// 같은 호출이 true로 바뀌었다(그리고 떼는 즉시 false로 돌아왔다).
    ///
    /// **비침해 원칙 유지**: 조회만 하고 어떤 입력도 주입하지 않으며, 위 GlobalKey에 열거된 11개 키
    /// 외에는 애초에 물어볼 수단이 없다. 조합키 3개를 모두 누른 상태에서만 동작키를 확인하므로
    /// 사용자가 다른 앱에서 타이핑하는 내용은 이 채널로 관측될 수 없다.
    /// </summary>
    public interface IGlobalKeyStateService
    {
        /// <summary>
        /// 해당 키가 지금 눌려 있으면 pressed=true. 조회 자체가 불가능한 환경이면 false를 반환하고
        /// 소비자는 전역 단축키 기능을 조용히 비활성화한다("지원 안 함"을 명시적으로 알린다).
        /// </summary>
        bool TryGetKeyPressed(GlobalKey key, out bool pressed);
    }
}
