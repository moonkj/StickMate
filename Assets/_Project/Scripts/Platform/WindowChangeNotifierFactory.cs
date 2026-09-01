namespace StickMate.Platform
{
    /// <summary>
    /// 플랫폼에 맞는 <see cref="IWindowChangeNotifier"/>를 고르는 <b>유일한</b> 지점.
    ///
    /// <para>여기 분기 조건은 <c>StickmanAgent.CreatePlatformService()</c>와 <b>일부러 똑같이</b>
    /// 맞춰 두었다(<c>UNITY_STANDALONE_WIN &amp;&amp; !UNITY_EDITOR</c>). 이유도 같다: Unity 에디터는
    /// 활성 빌드 타깃이 Windows면 에디터 컴파일 컨텍스트에도 <c>UNITY_STANDALONE_WIN</c>을 함께
    /// 정의하므로, <c>!UNITY_EDITOR</c>가 없으면 macOS 에디터에서 user32.dll을 부르다 죽는다.</para>
    ///
    /// <para><b>macOS는 이번 라운드에서 바꾸지 않는다</b>(판단 근거는 Tasklist.md의 이 라운드 항목).
    /// 요약: macOS에는 <c>SetWinEventHook</c>의 1:1 대응물이 없다 — 타 앱 창의 이동 통보를 받으려면
    /// 접근성 권한을 요구하는 <c>AXObserver</c>를 앱마다 붙여야 하고, <c>NSWorkspace</c> 알림은
    /// 앱 단위(실행/활성화)라 창 기하 변화를 주지 않는다. 즉 "권한 요구 + 새 네이티브 코드"라는
    /// 전혀 다른 크기의 작업이며, 신고는 Windows 전용이고 macOS 실측은 멀쩡하다. 양쪽을 같은
    /// 라운드에 바꾸면 Windows 검증 결과의 원인이 둘로 갈린다.</para>
    ///
    /// <para>대신 <b>좁히기 규칙(<see cref="FootholdScanPolicy"/>) 자체는 플랫폼 중립</b>이고
    /// <c>FootholdPoller</c>에 있으므로, macOS가 나중에 통보 창구를 얻으면 이 팩토리 한 줄만 바꾸면
    /// 같은 절감이 그대로 적용된다.</para>
    /// </summary>
    public static class WindowChangeNotifierFactory
    {
        /// <summary>
        /// 통보 창구를 만든다. <b>절대 null을 돌려주지 않는다</b> — 지원하지 않는 플랫폼에서는
        /// <see cref="NullWindowChangeNotifier"/>가 나오고, 그러면 호출자는 자동으로 옛 주기 폴링을
        /// 유지한다(에디터에서 크래시 없이 동작해야 한다는 컨벤션과 같은 취지).
        /// </summary>
        public static IWindowChangeNotifier Create()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hook = new Windows.WindowsWindowEventHook();
            hook.TryRegister(); // 실패해도 예외를 던지지 않는다 — IsActive=false로 폴백된다.
            return hook;
#else
            return new NullWindowChangeNotifier(
                "이 플랫폼에는 창 변화 통보 창구가 없습니다(Windows 전용) — 주기 폴링을 유지합니다.");
#endif
        }
    }
}
