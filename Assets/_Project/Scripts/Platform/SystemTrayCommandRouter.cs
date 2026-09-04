using System;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 트레이 메뉴에서 고른 명령을 <b>이미 존재하는 진입점</b>으로 넘겨 주는 배선판(2026-09-03).
    ///
    /// ============================================================================
    /// 왜 이 중간 단계가 있는가 — 두 가지 이유이고 둘 다 실질적이다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>의존 방향을 뒤집지 않기 위해.</b> 명령의 실체는
    ///     <c>AppControlDirector.QuitApplication</c> / <c>StickmanAgent.ToggleUserHidden</c> /
    ///     <c>SettingsWindow.Open</c>이고 전부 <c>StickMate.Interaction</c>·<c>StickMate.Core</c>에 있다.
    ///     <c>Platform/Windows/</c>가 그것들을 직접 부르면 <b>Platform → Interaction</b>이라는 역방향
    ///     의존이 생긴다(현재 <c>Platform/</c> 전체에서 <c>StickMate.Interaction</c> 참조는 0건이다 —
    ///     그 0을 지킨다). 대신 <c>Interaction</c> 쪽이 자기를 <b>등록</b>한다.</item>
    ///   <item><b>검증 가능성.</b> <c>Platform/Windows/</c>는 이 개발 머신의 활성 타깃(macOS)에서
    ///     <b>컴파일조차 되지 않는다.</b> 배달 규칙을 그 안에 두면 이 머신에서 한 줄도 실행해 볼 수
    ///     없다. 여기 두면 EditMode가 <b>실제로 실행</b>해 확인한다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 새 로직을 만들지 않는다
    /// ============================================================================
    /// 이 파일에는 종료·숨김·설정에 대한 <b>판단이 한 줄도 없다.</b> 2단 확인, 저장, 배타 모달 정리,
    /// 에디터 분기는 전부 기존 진입점이 이미 하고 있고 트레이는 그것을 <b>한 번 더 부를</b> 뿐이다.
    /// 특히 종료는 <c>AppControlDirector.QuitApplication</c>이 <b>단 하나의 자리</b>로 정리된 직후이므로
    /// (같은 날 신설) 여기서 <c>Application.Quit()</c>을 다시 부르는 일은 없어야 한다.
    ///
    /// ============================================================================
    /// 등록이 없을 때 — <b>조용히 성공하지 않는다</b>
    /// ============================================================================
    /// 핸들러가 없으면 <see cref="Dispatch"/>는 <b>false를 돌려주고 경고를 남긴다.</b> 이 저장소가
    /// 반복해서 당한 실패 모양이 "구현은 있는데 아무도 안 부른다"이고, 트레이는 특히 위험하다 —
    /// 메뉴는 정상으로 보이는데 눌러도 아무 일이 없으면 사용자는 앱이 <b>얼었다</b>고 판단한다.
    /// </summary>
    public static class SystemTrayCommandRouter
    {
        internal const string LogPrefix = "[트레이]";

        private static Action<TrayMenuCommand, string> _handler;
        private static Func<bool> _characterHiddenProbe;
        private static bool _missingHandlerLogged;

        /// <summary>핸들러가 등록돼 있는가. 진단/감사용.</summary>
        public static bool HasHandler => _handler != null;

        /// <summary>
        /// 트레이 명령 수신자를 등록한다. <b>나중 등록이 이긴다</b>(도메인 리로드/씬 재적재 후
        /// 죽은 델리게이트가 남아 있는 편보다 낫다).
        /// </summary>
        /// <param name="handler">명령 배달 대상. null이면 등록 해제와 같다.</param>
        /// <param name="characterHiddenProbe">지금 캐릭터가 <b>사용자 직접 숨김</b>인가를 묻는 조회.
        /// 메뉴 글자를 뒤집는 데만 쓴다. null이면 항상 "숨기기"로 표시된다.</param>
        public static void Register(Action<TrayMenuCommand, string> handler, Func<bool> characterHiddenProbe)
        {
            _handler = handler;
            _characterHiddenProbe = characterHiddenProbe;
            _missingHandlerLogged = false;
        }

        /// <summary>등록 해제. 테스트가 서로를 오염시키지 않도록 공개해 둔다.</summary>
        public static void Clear()
        {
            _handler = null;
            _characterHiddenProbe = null;
            _missingHandlerLogged = false;
        }

        /// <summary>
        /// 지금 캐릭터가 사용자 직접 숨김 상태인가. <b>조회가 없거나 던지면 false</b>(= "숨기기"로 표시).
        /// 메뉴 글자 하나 때문에 트레이 전체가 죽으면 안 된다.
        /// </summary>
        public static bool IsCharacterHidden()
        {
            Func<bool> probe = _characterHiddenProbe;
            if (probe == null) return false;
            try { return probe(); }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix} 캐릭터 표시 상태 조회가 실패했습니다({e.GetType().Name}) — " +
                    "메뉴는 '캐릭터 숨기기'로 표시됩니다. 동작 자체는 토글이라 영향이 없습니다.");
                return false;
            }
        }

        /// <summary>
        /// 명령을 배달한다. <b>예외를 밖으로 내보내지 않는다</b> — 이 호출은 Win32 메시지 처리
        /// 흐름에서 이어져 오므로, 여기서 예외가 나가면 셸/플레이어 쪽에서 무슨 일이 벌어지는지
        /// 이 머신에서 확인할 방법이 없다.
        /// </summary>
        /// <returns>실제로 배달됐으면 true.</returns>
        public static bool Dispatch(TrayMenuCommand command, string source)
        {
            Action<TrayMenuCommand, string> handler = _handler;
            if (handler == null)
            {
                if (!_missingHandlerLogged)
                {
                    _missingHandlerLogged = true;
                    Debug.LogWarning($"{LogPrefix} 명령({command})을 받았지만 수신자가 등록되지 않았습니다 — " +
                        "메뉴는 보이는데 눌러도 아무 일이 없는 상태입니다. " +
                        $"{nameof(Register)}를 부르는 배선(Interaction/SystemTrayCommandBridge)이 " +
                        "살아 있는지 확인하세요. 트레이가 죽어도 전역 단축키 · 톱니 부채꼴 · 설정창 " +
                        "3중 종료 경로는 그대로 살아 있습니다.");
                }
                return false;
            }

            try
            {
                handler(command, source);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix} 명령({command}) 처리 중 예외({e.GetType().Name}: {e.Message}). " +
                    "트레이는 계속 살아 있습니다.");
                return false;
            }
        }
    }
}
