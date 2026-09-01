using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows <c>SetWinEventHook</c> 기반 창 변화 <b>통보</b> 창구
    /// (<see cref="StickMate.Platform.IWindowChangeNotifier"/>의 Windows 구현).
    ///
    /// ============================================================================
    /// 왜 생겼는가 (2026-09-01 — 사용자 "지금 해결방안들보다 획기적인 방안이…", 리더 선택: 폴링 제거)
    /// ============================================================================
    /// 지금까지 발판 위치는 <c>StickConfig.footholdPollInterval = 0.3</c>초마다 <b>전체 창을 다시
    /// 열거</b>해 얻었다. 실기 로그에서 열거 대상 창은 최대 <b>818개</b>였고 창 하나당
    /// <c>IsWindowVisible</c> → <c>GetWindowTextLength</c> → <c>GetWindowLong</c> → <c>GetWindowRect</c> →
    /// <c>DwmGetWindowAttribute</c>(DWM 프로세스로 가는 크로스 프로세스 호출)를 밟는다.
    /// 창은 대부분 가만히 있는데 우리는 "혹시 움직였나"를 초당 3.3회 계속 물었다. Windows는 창이
    /// 실제로 움직이면 <b>알려주는 기능</b>이 있고, 이 클래스가 그것이다.
    ///
    /// ============================================================================
    /// 절대 불변 원칙 3(유저 자산 불변) — 이 파일이 지키는 방식
    /// ============================================================================
    /// 아래 <c>#region</c>에 선언된 P/Invoke는 <b>두 개뿐</b>이다: <c>SetWinEventHook</c>(구독) /
    /// <c>UnhookWinEvent</c>(해제). 둘 다 <b>순수 관찰</b>이며 다른 프로세스의 창을 한 픽셀도
    /// 건드리지 않는다. <c>SetWindowPos</c> / <c>MoveWindow</c> / <c>DestroyWindow</c> 계열은
    /// <b>선언조차 하지 않는다</b> — 선언이 없으면 실수로도 부를 수 없다.
    /// 이 계약은 <c>Tests/EditMode/UserAssetImmutabilityAuditTests</c>가 소스 정적 스캔으로 잠근다.
    ///
    /// ============================================================================
    /// 구독 이벤트를 이렇게 고른 이유 (과하게 구독하면 이벤트 폭풍이 난다)
    /// ============================================================================
    /// <list type="bullet">
    /// <item><c>EVENT_OBJECT_LOCATIONCHANGE</c> — 창 이동/크기변경. <b>발판 좌표가 바뀌는 유일한 경로</b>다.</item>
    /// <item><c>EVENT_OBJECT_DESTROY</c> / <c>HIDE</c> — 발판이 사라지는 경로.</item>
    /// <item><c>EVENT_OBJECT_SHOW</c> — 발판이 생기는 경로. <b><c>EVENT_OBJECT_CREATE</c>는 일부러 빼
    ///   두었다</b>: 창은 생성 직후에는 아직 보이지도, 최종 위치에 있지도 않아 그 시점의 열거는
    ///   버려지는 일이 많고, 어차피 곧 <c>SHOW</c>가 온다. 같은 사실을 두 번 받으면 스캔만 두 배가 된다.</item>
    /// <item><c>EVENT_SYSTEM_MINIMIZESTART</c> / <c>MINIMIZEEND</c> — 최소화는 <c>HIDE</c>를 동반하지
    ///   않는 경우가 있어 별도로 받는다.</item>
    /// <item><c>EVENT_SYSTEM_FOREGROUND</c> — z-order가 바뀌면 <b>가려짐(오클루전) 계산 결과</b>가
    ///   달라진다. 좌표는 그대로여도 "보이는 상단 테두리 조각"이 달라지므로 필요하다.</item>
    /// </list>
    /// 그 밖(<c>EVENT_OBJECT_NAMECHANGE</c>, <c>EVENT_OBJECT_FOCUS</c>, <c>EVENT_OBJECT_VALUECHANGE</c>,
    /// 메뉴/캐럿/스크롤 계열)은 <b>구독하지 않는다</b>. 발판 기하와 무관한데 빈도는 압도적으로 높다.
    ///
    /// ============================================================================
    /// 이벤트 폭풍 대비 — 3중 필터
    /// ============================================================================
    /// <list type="number">
    /// <item><b>구독 범위</b>를 위 5종으로 좁힌다(훅 4개로 쪼갠 이유가 이것이다 — 하나의 넓은 범위로
    ///   묶으면 그 사이의 수십 종 이벤트를 전부 받게 된다).</item>
    /// <item><b>콜백 안에서</b> <c>idObject == OBJID_WINDOW &amp;&amp; idChild == CHILDID_SELF</c>만
    ///   통과시킨다. 캐럿(-8)/커서(-9)/클라이언트(-4) 이벤트가 여기서 전부 죽는다 — 드래그 중
    ///   초당 수백 번 오는 <c>LOCATIONCHANGE</c>의 대부분이 자식 개체다.</item>
    /// <item><b>합치기</b>: 콜백은 <c>Interlocked</c> 플래그 하나만 세운다. 메인 루프가 프레임당 한 번
    ///   소비하므로, 초당 500번 오든 5번 오든 <b>스캔은 최대 프레임당 1회</b>다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 스레드 — 콜백에서 Unity API를 부르지 않는다
    /// ============================================================================
    /// <c>WINEVENT_OUTOFCONTEXT</c> 훅은 훅을 <b>설치한 스레드의 메시지 큐</b>를 통해 배달된다. 실제로는
    /// Unity 메인 스레드일 가능성이 높지만 <b>그것에 의존하지 않는다</b>: 이 클래스가 콜백에서 하는 일은
    /// <c>Interlocked.Increment</c>와 플래그 세우기뿐이고 Unity API는 한 줄도 부르지 않는다.
    /// 메시지 디스패치 도중에 재진입될 수 있다는 점까지 고려한 모양이다.
    ///
    /// ============================================================================
    /// 핸들 수명 — 델리게이트를 필드로 붙잡는 이유
    /// ============================================================================
    /// 네이티브가 콜백 주소를 들고 있는 동안 관리 델리게이트가 GC되면 <b>프로세스가 죽는다</b>.
    /// <c>_callback</c> 필드가 그것을 막는다(<c>Win32WindowService._enumWindowsCallback</c>과 같은 관례).
    /// 훅 핸들은 <see cref="Dispose"/>에서 <c>UnhookWinEvent</c>로 반드시 해제한다 —
    /// 이 저장소는 "네이티브 핸들 누수"를 중점 점검 항목으로 두고 있다.
    /// </summary>
    public sealed class WindowsWindowEventHook : IWindowChangeNotifier
    {
        #region Win32 선언 (이 리전 밖으로 유출 금지 — 전부 <b>관찰 전용</b>)

        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        // 훅 플래그.
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000; // 우리 DLL을 남의 프로세스에 주입하지 않는다.
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002; // 우리 오버레이 창 자신의 이벤트는 받지 않는다.

        // 구독 이벤트(위 클래스 문서의 선정 근거 참고).
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint EVENT_OBJECT_HIDE = 0x8003;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;

        // 콜백 필터 상수.
        private const int OBJID_WINDOW = 0;
        private const int CHILDID_SELF = 0;

        #endregion

        /// <summary>등록할 훅 범위. 넓은 하나가 아니라 <b>좁은 넷</b>인 이유는 클래스 문서 참고.</summary>
        private static readonly (uint Min, uint Max, string Name)[] HookRanges =
        {
            (EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, "FOREGROUND"),
            (EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND, "MINIMIZE"),
            (EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE, "DESTROY/SHOW/HIDE"),
            (EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, "LOCATIONCHANGE"),
        };

        /// <summary>감시 목록 스냅샷. 메인 스레드가 <b>쓰지 않는 쪽</b> 버퍼를 채운 뒤 참조만 바꿔 끼운다
        /// (콜백이 반쯤 갱신된 배열을 보는 일이 없다). 버퍼가 둘인 이유는 갱신이 전체 스캔마다 한 번뿐이라
        /// 두 개면 겹칠 수 없기 때문이다.</summary>
        private sealed class WatchSnapshot
        {
            public readonly long[] Handles = new long[FootholdScanPolicy.MaxWatchedWindows];
            public int Count;

            /// <summary>감시 목록이 상한을 넘쳐 <b>좁히기를 포기</b>했는가. true면 콜백은 모든 창을
            /// "감시 대상"으로 취급한다 — 놓치는 것보다 더 보는 쪽으로 넘어진다.</summary>
            public bool Overflowed;
        }

        private readonly WatchSnapshot[] _watchBuffers = { new WatchSnapshot(), new WatchSnapshot() };
        private int _watchWriteIndex;
        private volatile WatchSnapshot _activeWatch;

        // 네이티브가 주소를 들고 있는 동안 GC되면 프로세스가 죽는다 — 반드시 필드로 붙잡는다.
        private readonly WinEventProc _callback;

        private readonly IntPtr[] _hooks = new IntPtr[HookRanges.Length];

        private int _watchedDirty;
        private int _globalDirty;
        private long _rawCallbackCount;
        private long _acceptedEventCount;

        private bool _disposed;

        public bool IsActive { get; private set; }
        public string StatusDescription { get; private set; }
        public long RawCallbackCount => Interlocked.Read(ref _rawCallbackCount);
        public long AcceptedEventCount => Interlocked.Read(ref _acceptedEventCount);

        /// <summary>등록에 성공한 훅 개수(진단). 일부만 성공해도 <see cref="IsActive"/>는 false다 —
        /// 반쪽짜리 구독은 "이벤트가 오는데 일부만 온다"라서 폴백보다 위험하다.</summary>
        public int RegisteredHookCount { get; private set; }

        public WindowsWindowEventHook()
        {
            _callback = OnWinEvent;
            _activeWatch = _watchBuffers[0];
            StatusDescription = "아직 등록하지 않았습니다.";
        }

        /// <summary>
        /// 훅을 등록한다. 실패해도 예외를 던지지 않는다 — 호출자는 <see cref="IsActive"/>가 false인
        /// 것을 보고 <b>옛 주기 폴링</b>으로 폴백해야 한다(권한/세션 격리로 실패할 수 있다).
        /// </summary>
        public bool TryRegister()
        {
            if (_disposed) return false;
            if (IsActive) return true;

            int ok = 0;
            int lastError = 0;
            for (int i = 0; i < HookRanges.Length; i++)
            {
                if (_hooks[i] != IntPtr.Zero) { ok++; continue; }
                _hooks[i] = SetWinEventHook(HookRanges[i].Min, HookRanges[i].Max, IntPtr.Zero,
                    _callback, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                if (_hooks[i] != IntPtr.Zero) ok++;
                else lastError = Marshal.GetLastWin32Error();
            }

            RegisteredHookCount = ok;
            if (ok == HookRanges.Length)
            {
                IsActive = true;
                StatusDescription = $"훅 {ok}/{HookRanges.Length}개 등록됨(FOREGROUND/MINIMIZE/DESTROY·SHOW·HIDE/LOCATIONCHANGE) — 이벤트 방식 가동.";
                // 첫 스캔은 호출자가 어차피 부트스트랩으로 한 번 한다. 여기서 플래그를 세워두면
                // "훅을 켠 직후의 창 배치"를 반드시 한 번 반영하게 되어 등록 타이밍 경합이 사라진다.
                Interlocked.Exchange(ref _globalDirty, 1);
                return true;
            }

            // 반쪽 등록은 통째로 되돌린다(위 RegisteredHookCount 문서 참고).
            ReleaseHooks();
            IsActive = false;
            StatusDescription = $"훅 등록 실패({ok}/{HookRanges.Length}, GetLastError={lastError}) — 주기 폴링으로 폴백합니다.";
            return false;
        }

        public bool ConsumeChangeSignals(out bool watchedChanged, out bool globalChanged)
        {
            watchedChanged = Interlocked.Exchange(ref _watchedDirty, 0) != 0;
            globalChanged = Interlocked.Exchange(ref _globalDirty, 0) != 0;
            return watchedChanged || globalChanged;
        }

        public void SetWatchedWindows(IReadOnlyList<long> handles)
        {
            WatchSnapshot next = _watchBuffers[_watchWriteIndex];
            int count = handles != null ? handles.Count : 0;
            bool overflow = count > next.Handles.Length;
            if (overflow) count = next.Handles.Length;

            for (int i = 0; i < count; i++) next.Handles[i] = handles[i];
            next.Count = count;
            next.Overflowed = overflow;

            _activeWatch = next;                 // volatile 쓰기 — 콜백은 이 시점부터 새 목록을 본다.
            _watchWriteIndex ^= 1;               // 다음 갱신은 반대편 버퍼에 쓴다.
        }

        /// <summary>
        /// 훅 콜백. <b>여기서 Unity API를 부르면 안 된다</b>(클래스 문서의 스레드 절 참고).
        /// 하는 일은 값싼 정수 비교와 <c>Interlocked</c> 플래그 세우기뿐이다.
        /// </summary>
        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            Interlocked.Increment(ref _rawCallbackCount);

            // 필터 (2) — 창 자신에 대한 이벤트만. 캐럿/커서/클라이언트/스크롤바 자식 개체가 여기서 죽는다.
            if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF) return;
            if (hwnd == IntPtr.Zero) return;

            Interlocked.Increment(ref _acceptedEventCount);

            long handle = hwnd.ToInt64();
            WatchSnapshot watch = _activeWatch;
            bool watched = watch == null || watch.Overflowed || Contains(watch, handle);

            // 감시 대상이면 무조건 스캔으로 이어진다. 그 밖은 "지금 좁혀도 되는 상황인가"를
            // 메인 스레드가 판단하도록 별도 플래그로 남긴다(FootholdScanPolicy.Decide).
            if (watched) Interlocked.Exchange(ref _watchedDirty, 1);
            else Interlocked.Exchange(ref _globalDirty, 1);
        }

        private static bool Contains(WatchSnapshot watch, long handle)
        {
            long[] a = watch.Handles;
            int n = watch.Count;
            for (int i = 0; i < n; i++)
            {
                if (a[i] == handle) return true;
            }
            return false;
        }

        private void ReleaseHooks()
        {
            for (int i = 0; i < _hooks.Length; i++)
            {
                if (_hooks[i] == IntPtr.Zero) continue;
                UnhookWinEvent(_hooks[i]);
                _hooks[i] = IntPtr.Zero;
            }
            RegisteredHookCount = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseHooks();
            IsActive = false;
            StatusDescription = "해제됨(UnhookWinEvent 완료).";
        }
    }
}
