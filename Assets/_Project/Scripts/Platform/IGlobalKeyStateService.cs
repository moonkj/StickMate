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

        /// <summary>★ <b>비어 있다(예약).</b> 2026-09-02까지 격파 미니게임 강제 발동 데모였고, 격파 놀이
        /// 기능이 삭제되면서 <b>바인딩만</b> 사라졌다 — 지금 이 키를 눌러도 아무 일도 일어나지 않고,
        /// 부팅 배너의 단축키 목록에도 나오지 않는다.
        ///
        /// <para>왜 열거값을 지우지 않았나: 이 값을 지우면 <c>Platform/Windows/Win32WindowService.cs</c>의
        /// <c>case GlobalKey.K</c>가 컴파일되지 않아 그 파일을 함께 고쳐야 하는데, 삭제 라운드와
        /// 동시에 다른 라운드가 <c>Platform/Windows/</c>를 편집 중이었다(리더 지시로 접근 금지).
        /// 양쪽 플랫폼의 키코드 매핑은 그대로 살아 있으므로 다시 배선하기만 하면 즉시 쓸 수 있다.
        /// <b>다음에 새 기능이 단축키를 필요로 하면 이 자리를 먼저 써라.</b></para></summary>
        K,

        /// <summary>그라피티 강제 발동 데모(Ctrl+Opt+Cmd+G, Graffiti) — 자동 발동이 60초마다 4% 추첨 +
        /// 10분 쿨다운이라 확률만으로는 검증이 사실상 불가능해 강제 경로가 필요하다.
        /// Interaction/AppControlDirector.cs.</summary>
        G,

        /// <summary>창 도둑 강제 발동 데모(Ctrl+Opt+Cmd+T, Theft) — 자동 발동이 60초마다 3% 추첨 +
        /// 15분 쿨다운이라 G와 같은 이유로 강제 경로가 필요하다. Interaction/AppControlDirector.cs.</summary>
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

        /// <summary>스트레스 게이지 단계 순환 미리보기(Ctrl+Opt+Cmd+S, Stress) — 하드웨어 반응(H)과 같은
        /// 성격이다. 실사용에서 게이지가 실제로 차오르는 데는 수 시간~반나절이 걸려(19절: 반나절 방치 /
        /// 5분 내 8회 과다 상호작용) 확률을 건너뛰는 것만으로는 검증이 불가능하다.
        /// Interaction/StressGaugeDirector.ForceTriggerNow 문서 참고.</summary>
        S,

        /// <summary>가출 강제 발동 / 가출 중이면 [돌아오라고 부르기](Ctrl+Opt+Cmd+N, "Nope 나 안 해") —
        /// 가출은 확률이 아니라 스트레스 임계값 도달 시 확정 발동(24절)이라, 이 경로는 확률이 아니라
        /// <b>임계값</b>을 건너뛴다. 20절이 상시 제공을 요구한 수동 소환 탈출구를 겸한다
        /// (캐릭터가 화면에서 사라진 상태라 캐릭터 우클릭에만 의존하면 탈출구에 도달하지 못할 수 있다).
        /// Interaction/RunawayDirector.ForceTriggerNow 문서 참고.</summary>
        N,

        /// <summary>할일 추가(데모) + 들고 다니는 모드 알림 강제 발동(Ctrl+Opt+Cmd+J, Job) — 17절의 정식
        /// 진입점("[+ 할일 추가]")이 설정창/트레이와 함께 아직 없어, 지금은 이 경로가 목록에 항목을 넣는
        /// 유일한 통로다. Interaction/TodoReminderDirector.ForceTriggerNow 문서 참고.</summary>
        J,

        /// <summary>집중 모드 켜기/끄기(Ctrl+Opt+Cmd+F, Focus) — 다른 항목과 달리 데모가 아니라 <b>정식
        /// 진입점</b>이다. 18절의 "[시작] 트레이 메뉴 '집중 모드'"/"[종료-중도취소] 트레이에서 끄기"를
        /// 트레이가 없는 지금 아키텍처에서 이 단축키가 대신한다.
        /// Interaction/FocusWatchDirector.ForceTriggerNow 문서 참고.</summary>
        F,

        /// <summary>활쏘기 강제 발동(Ctrl+Opt+Cmd+A, Archery) — 자율 발동 확률이 기본 0이라
        /// (StickConfig.archeryChance, 사용자가 요청하지 않은 연출이 뜨는 것에 반복적으로 불만을
        /// 표했다) 이 단축키와 캐릭터 우클릭 메뉴가 <b>유일한 발동 경로</b>다. 즉 다른 데모 키들처럼
        /// "확률을 건너뛰는 지름길"이 아니라 정식 진입점이다(F/집중 모드와 같은 성격).
        /// Interaction/ArcheryDirector.ForceTriggerNow 문서 참고.</summary>
        A,

        /// <summary>캐릭터 정보/장비 창 열기·닫기(Ctrl+Opt+Cmd+I, Info) — 활쏘기(A)/집중 모드(F)와 같은
        /// 성격의 <b>정식 진입점</b>이다(확률을 건너뛰는 데모 지름길이 아니다). 주 진입점은 화면 우상단
        /// 톱니 아이콘(Interaction/InfoGearIconWidget.cs)이고, 이 단축키와 캐릭터 우클릭 메뉴
        /// [캐릭터 정보]가 보조 경로다 — 이 프로젝트의 모든 기능이 갖는 "단축키 + 메뉴" 이중 경로 관례.
        /// Interaction/CharacterInfoWindow.cs 참고.</summary>
        I,

        /// <summary>★ 설정창 열기·닫기(Ctrl+Opt+Cmd+<b>P</b>, Preferences) — docs/UX_FLOW.md 36-11.
        /// I(정보창)/A(활쏘기)/F(집중 모드)와 같은 <b>정식 진입점</b>이지 확률을 건너뛰는 데모
        /// 지름길이 아니다.
        ///
        /// <para><b>★ 2026-09-01 쉼표에서 P로 옮겼다 — 원칙 2·3 위반이었다.</b> 이 자리는 원래
        /// <c>Comma</c>였고 <c>⌘,</c>(환경설정) 관례를 따른 것이었다. 그런데 macOS는
        /// <c>⌃⌥⌘,</c>를 접근성 시스템 단축키 <b>"대비 줄이기"</b>로 <b>이미 예약</b>해 두었다
        /// (symbolic hotkey 26). 즉 키 한 번에 두 가지 일이 일어났고, 우리가 설정창을 열고 닫을
        /// 때마다(2회 누름) <c>com.apple.universalaccess</c>의 <c>contrast</c> 값이 0.10씩
        /// <b>실제로 내려갔다</b> — 하필 대비 조절을 쓰는 접근성 사용자만 골라서 맞는 결함이다.
        /// 금지 목록과 재현 절차는 <c>Core/ShortcutLabel.MacReservedActionKeys</c>에 있고,
        /// <c>Tests/EditMode/ShortcutLabelParityTests</c>가 재발을 막는다.</para></summary>
        P,
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
