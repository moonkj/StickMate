using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 트레이 메뉴 명령을 <b>이미 존재하는 진입점</b>에 잇는 배선(2026-09-03,
    /// 사용자 확정 지시 <i>"실행시 시스템 트레이에 표시되어야함"</i>).
    ///
    /// ============================================================================
    /// 이 파일에 <b>새 기능이 없다</b> — 그게 요구사항이었다
    /// ============================================================================
    /// 세 명령 전부 기존 함수를 <b>한 번 더 부르는</b> 것뿐이다:
    /// <list type="table">
    ///   <item><term><see cref="TrayMenuCommand.Quit"/></term>
    ///     <description><see cref="AppControlDirector.QuitApplication"/> — 같은 날 신설된
    ///     <b>단 하나의 종료 자리</b>. 여기서 <c>Application.Quit()</c>을 다시 부르면 그 정리가
    ///     즉시 무의미해진다(에디터 분기를 한 곳에서 빠뜨리면 그 경로만 배치모드 테스트를 얼린다).</description></item>
    ///   <item><term><see cref="TrayMenuCommand.ToggleCharacterHidden"/></term>
    ///     <description><see cref="StickmanAgent.ToggleUserHidden"/> — <c>⌃⌥⌘K</c>와
    ///     <b>완전히 같은 축</b>(축 2 <see cref="StickmanAgent.IsUserHidden"/>). 렌더러만 끄는 옛
    ///     경로를 되살리지 않는다.</description></item>
    ///   <item><term><see cref="TrayMenuCommand.OpenSettings"/></term>
    ///     <description><see cref="SettingsWindow.Open(string)"/> — 배타 모달 정리는 그 함수
    ///     <b>한 곳</b>이 책임진다. 진입점마다 정리 코드를 흩뿌리다 실제로 샌 적이 있다.
    ///     <c>Toggle</c>이 아니라 <c>Open</c>인 이유: 메뉴 글자가 "설정 열기"다(원칙 1 —
    ///     표시된 것과 실제가 갈라지면 안 된다. 열려고 눌렀는데 닫히면 그것이 갈라진 것이다).</description></item>
    /// </list>
    ///
    /// ============================================================================
    /// 왜 씬 오브젝트가 아닌가
    /// ============================================================================
    /// <see cref="RuntimeInitializeOnLoadMethod"/>로 스스로 선다
    /// (<c>Platform/ReservedBarRevealDirector</c> · <c>Platform/StallAttributionProbe</c>의 선례).
    /// 씬 배선에 기대면 <b>씬이 바뀌거나 프리팹이 갱신될 때 조용히 죽는다</b> — 그리고 이 배선이
    /// 죽으면 트레이 메뉴는 <b>보이는데 눌러도 아무 일이 없는</b> 상태가 된다. 그 상태를
    /// <see cref="SystemTrayCommandRouter.Dispatch"/>가 경고로 잡아 주긴 하지만, 애초에 그 경로를
    /// 만들지 않는 편이 낫다. 씬 부트스트래퍼(<c>Assets/Editor/</c>)를 건드리지 않아도 되는 것도
    /// 같은 이유에서 이득이다.
    ///
    /// ============================================================================
    /// 플랫폼
    /// ============================================================================
    /// 이 파일에는 <c>#if UNITY_STANDALONE_WIN</c>이 <b>없다</b>. 등록 자체는 어느 플랫폼에서나
    /// 무해하고(부르는 쪽이 없으면 아무 일도 일어나지 않는다), 여기에 플랫폼 분기를 넣으면
    /// macOS가 나중에 <see cref="SystemTrayPresencePolicy.MacOsMechanismName"/>을 얻었을 때
    /// <b>이 배선부터 다시 만들어야 한다.</b> 지금 갈라 둘 이유가 없다.
    /// </summary>
    public static class SystemTrayCommandBridge
    {
        private static StickmanAgent _agent;
        private static SettingsWindow _settingsWindow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 씬이 다시 적재되면 캐시된 참조가 죽는다 — 등록과 함께 비운다.
            _agent = null;
            _settingsWindow = null;
            SystemTrayCommandRouter.Register(Handle, ProbeCharacterHidden);
        }

        /// <summary>
        /// 메뉴 글자를 뒤집기 위한 <b>조회 전용</b> 경로. 상태를 바꾸지 않는다.
        ///
        /// ============================================================================
        /// ★ 2026-09-05 — 읽는 값이 <c>IsUserHiddenOnly</c>에서
        /// <see cref="StickmanAgent.IsUserHidden"/>로 <b>좁아졌다</b>(verify-change 신고, M-8 후속)
        /// ============================================================================
        /// <b>글자는 이 항목이 실제로 바꾸는 축을 말해야 한다.</b> 이 메뉴가 부르는 것은
        /// <see cref="StickmanAgent.ToggleUserHidden"/> = <c>SetUserHidden(!_userHidden)</c>이므로
        /// 글자의 근거도 <c>_userHidden</c> 하나여야 한다(원칙 1).
        ///
        /// <para><b>무엇이 어긋나 있었나.</b> <c>IsUserHiddenOnly</c>는 「<b>축 2만으로</b> 숨어 있는가」라서
        /// 축 1(전체화면 게임)·축 4(다른 가상 데스크톱)가 함께 켜지면 <b>false</b>다. 그 뜻은 옳고
        /// <c>HidesScreenSurfaces</c>가 그 값을 필요로 하지만, <b>이 메뉴가 물어야 하는 질문이 아니다</b>:
        /// 사용자가 이미 숨겨 둔 상태인데 글자가 「캐릭터 숨기기」로 나오고, 누르면 축 2가 <b>꺼지는데</b>
        /// 화면은 그대로라(다른 축이 여전히 숨기고 있으므로) <b>메뉴가 고장 난 것처럼 보인다</b>.</para>
        ///
        /// <para>★ 축 4에서 특히 나쁘다. 축 1(전체화면 게임) 중에는 작업표시줄이 덮여 트레이에
        /// 사실상 닿을 수 없어 이 어긋남이 드러나지 않았지만, <b>다른 가상 데스크톱에 있는 동안에는
        /// 트레이가 그대로 보인다</b> — 즉 축 4가 이 결함을 처음으로 <b>도달 가능하게</b> 만들었다.</para>
        ///
        /// <para><b>「어떤 이유로든 숨었는가」를 쓰면 안 되는 이유</b>도 같다. 그러면 전체화면 감지로
        /// 숨은 동안 글자가 「다시 보이기」가 되고, 눌러도 축 2가 <b>켜질</b> 뿐이라 상황이 나빠진다.
        /// 토글 항목의 글자는 <b>그 토글이 소유한 축</b>에서만 나와야 한다.</para>
        /// </summary>
        private static bool ProbeCharacterHidden()
        {
            StickmanAgent agent = ResolveAgent();
            return agent != null && agent.IsUserHidden;
        }

        private static void Handle(TrayMenuCommand command, string source)
        {
            switch (command)
            {
                case TrayMenuCommand.Quit:
                    AppControlDirector.QuitApplication(source);
                    break;

                case TrayMenuCommand.ToggleCharacterHidden:
                {
                    StickmanAgent agent = ResolveAgent();
                    if (agent == null)
                    {
                        Debug.LogWarning($"{SystemTrayCommandRouter.LogPrefix} 숨기기/보이기 실패({source}) — " +
                            "씬에 StickmanAgent가 없습니다.");
                        break;
                    }
                    agent.ToggleUserHidden(source);
                    break;
                }

                case TrayMenuCommand.OpenSettings:
                {
                    SettingsWindow window = ResolveSettingsWindow();
                    if (window == null)
                    {
                        Debug.LogWarning($"{SystemTrayCommandRouter.LogPrefix} 설정창 열기 실패({source}) — " +
                            "씬에 SettingsWindow가 없습니다. Assets/Editor/SceneBootstrapper.cs의 " +
                            "EnsurePrefabComponents를 실행했는지 확인하세요.");
                        break;
                    }
                    window.Open(source);
                    break;
                }

                default:
                    Debug.LogWarning($"{SystemTrayCommandRouter.LogPrefix} 알 수 없는 트레이 명령({command})을 " +
                        "무시했습니다. SystemTrayPresencePolicy.MenuOrder에 항목이 늘었다면 여기에도 " +
                        "가지를 추가해야 합니다 — 그러지 않으면 메뉴에는 보이는데 눌러도 아무 일이 " +
                        "없습니다.");
                    break;
            }
        }

        /// <summary>씬 전체 스캔은 비싸다 — 찾으면 캐시하고, 파괴됐으면 다시 찾는다
        /// (<c>WindowsOverlayStateEnforcer.EnsureAgentResolved</c>와 같은 관례).</summary>
        private static StickmanAgent ResolveAgent()
        {
            if (_agent == null) _agent = Object.FindFirstObjectByType<StickmanAgent>();
            return _agent;
        }

        private static SettingsWindow ResolveSettingsWindow()
        {
            if (_settingsWindow == null) _settingsWindow = Object.FindFirstObjectByType<SettingsWindow>();
            return _settingsWindow;
        }
    }
}
