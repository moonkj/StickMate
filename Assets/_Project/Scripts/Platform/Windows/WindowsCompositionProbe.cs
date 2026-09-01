#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Kirurobo;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// ★ 2026-09-01 — Windows 실기에서 <b>알파/합성 상태를 실측해 로그 한 줄로 남기는</b> 진단 프로브.
    ///
    /// ============================================================================
    /// 왜 필요한가 (리더 지시: "진단 로그 강화 + 사용자 테스트 루프 단축")
    /// ============================================================================
    /// 이 개발 머신에서 Windows는 <b>한 번도 실행된 적이 없다</b>. 그런데 "창이 겹쳐 보인다 /
    /// 텍스트가 번져 보인다"는 신고가 세 라운드째 반복되고 있고, 그때마다 <b>추측으로 고친 뒤</b>
    /// 사용자에게 다시 빌드를 보내 물어보는 왕복이 발생했다. 이 프로브의 목적은 단 하나다 —
    /// <b>다음 신고에서 Player.log 한 줄만 보고 원인을 확정</b>하는 것.
    ///
    /// 판정 규칙은 여기 없다. 전부 플랫폼 중립 순수 함수
    /// <see cref="StickMate.Platform.OverlayCompositionVerdict.Diagnose"/>에 있고, 그래서 macOS
    /// EditMode 테스트가 Windows 판정 로직을 <b>전수 검증</b>한다. 이 파일은 <b>관측만</b> 한다.
    ///
    /// ============================================================================
    /// 24시간 상주 규약 — 매 프레임 찍지 않는다
    /// ============================================================================
    /// <see cref="SampleIntervalSeconds"/>마다 한 번 관측하고, 관측 <b>지문이 바뀔 때만</b> 로그를
    /// 남긴다(<see cref="OverlayCompositionSnapshot.Signature"/>). 정상 상태로 안정되면 로그는
    /// 완전히 멈추고, 유저가 창을 다른 모니터로 옮기거나 배율을 바꾸면 그 순간 한 줄이 더 찍힌다.
    /// 관측 비용은 user32/dwmapi 호출 4개(≈수 마이크로초)이며 2초에 한 번이다.
    ///
    /// <b>로그 위치(사용자 안내용)</b>:
    /// <c>%USERPROFILE%\AppData\LocalLow\DefaultCompany\StickMate\Player.log</c>
    /// (탐색기 주소창에 <c>%USERPROFILE%\AppData\LocalLow\DefaultCompany\StickMate</c> 붙여넣기).
    /// 검색어는 <c>[합성진단]</c> 한 단어면 된다.
    /// </summary>
    internal sealed class WindowsCompositionProbe : MonoBehaviour
    {
        internal const string LogPrefix = "[합성진단]";
        private const string HostObjectName = "StickMate_WindowsCompositionProbe";

        /// <summary>관측 주기. 사람이 창을 옮기는 속도보다 충분히 빠르고, 상주 비용은 무시할 수 있다.</summary>
        private const float SampleIntervalSeconds = 2f;

        /// <summary>글리프 리샘플 비를 계산할 대표 폰트 크기(pt). 정보창 본문과 같은 값이면 된다 —
        /// 어떤 값을 넣어도 "캔버스 배율이 정수인가"라는 결론은 같고, 숫자만 사람이 읽기 쉬워진다.</summary>
        private const int SampleFontSizePoints = 13;

        private UniWindowController _controller;
        private Core.StickConfig _config;
        private Core.StickmanAgent _agent;
        private float _timer;
        private string _lastSignature;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _hwndLookupFailedLogged;

        internal static WindowsCompositionProbe EnsureExists(UniWindowController controller, Core.StickConfig config)
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<WindowsCompositionProbe>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing._controller = controller;
                if (config != null) existing._config = config;
                return existing;
            }

            var go = new GameObject(HostObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var probe = go.AddComponent<WindowsCompositionProbe>();
            probe._controller = controller;
            probe._config = config;
            // 첫 관측은 다음 Update에서 곧바로 한 번(부착 직후 상태를 놓치지 않는다).
            probe._timer = SampleIntervalSeconds;
            return probe;
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < SampleIntervalSeconds) return;
            _timer = 0f;

            OverlayCompositionSnapshot snapshot = Capture();
            string signature = snapshot.Signature();
            if (signature == _lastSignature) return;   // 전이 없음 — 아무것도 찍지 않는다.
            _lastSignature = signature;

            var verdict = OverlayCompositionVerdict.Diagnose(snapshot);
            var sb = new StringBuilder(1024);
            sb.Append(LogPrefix).Append(" 오버레이 알파/합성 상태가 바뀌었습니다 — 관측:\n");
            AppendObservations(sb, snapshot);
            sb.Append("  판정:\n");
            for (int i = 0; i < verdict.Count; i++) sb.Append("    ").Append(verdict[i]).Append('\n');

            if (OverlayCompositionVerdict.HasFault(verdict)) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        /// <summary>관측값 원문 — 판정 규칙이 틀렸을 때도 <b>사람이 다시 판단할 수 있게</b> 날것을 남긴다.</summary>
        private static void AppendObservations(StringBuilder sb, OverlayCompositionSnapshot s)
        {
            sb.Append($"    창/백버퍼: 백버퍼={s.BackBufferWidth}x{s.BackBufferHeight}, ")
              .Append($"클라이언트={s.ClientWidth:F0}x{s.ClientHeight:F0}, 창={s.WindowWidth:F0}x{s.WindowHeight:F0}, ")
              .Append($"fullScreenMode={(FullScreenMode)s.FullScreenMode}\n");
            sb.Append($"    배율: 캔버스={s.CanvasScaleFactor:F3}, UI밀도(GetDpiForWindow/96)={s.UiDensityScale:F3}, ")
              .Append($"AutoDpiScale={s.AutoDpiScale:F3}\n");
            sb.Append($"    합성: transparentType={(s.TransparentType == 1 ? "Alpha(DWM확장프레임)" : s.TransparentType == 2 ? "ColorKey" : s.TransparentType.ToString())}, ")
              .Append($"DWM합성={s.DwmCompositionEnabled}, 스타일실측={s.OsStyleReadOk}, ")
              .Append($"WS_EX_LAYERED={s.HasLayeredStyle}, WS_EX_TRANSPARENT={s.HasClickThroughStyle}, ")
              .Append($"레이어드속성={(s.LayeredAttributesInEffect ? $"있음(알파 {s.LayeredAlphaByte}/255)" : "없음")}\n");
            sb.Append($"    카메라: clearFlags={(CameraClearFlags)s.CameraClearFlags}, ")
              .Append($"배경=({s.CameraBackground.r:F3},{s.CameraBackground.g:F3},{s.CameraBackground.b:F3},{s.CameraBackground.a:F3}), ")
              .Append($"HDR={s.CameraAllowHdr}, MSAA허용={s.CameraAllowMsaa}\n");
            sb.Append($"    샘플링: MSAA 요청={s.RequestedMsaa}x 실측={s.ActualMsaa}x, ")
              .Append($"UI스프라이트 필터={(FilterMode)s.UiSpriteFilterMode}, ")
              .Append($"GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})\n");
        }

        private OverlayCompositionSnapshot Capture()
        {
            var s = new OverlayCompositionSnapshot
            {
                BackBufferWidth = Screen.width,
                BackBufferHeight = Screen.height,
                FullScreenMode = (int)Screen.fullScreenMode,
                CanvasScaleFactor = ScreenCoordinateConverter.ResolveCanvasScaleFactor(ResolveConfig()),
                UiDensityScale = ScreenCoordinateConverter.AutoUiDensityScale,
                AutoDpiScale = ScreenCoordinateConverter.AutoDpiScale,
                SampleFontSizePoints = SampleFontSizePoints,
                RequestedMsaa = QualitySettings.antiAliasing,
                ActualMsaa = Screen.msaaSamples,
                LayeredAlphaByte = -1,
                UiSpriteFilterMode = (int)FilterMode.Bilinear,
            };

            if (_controller != null)
            {
                Vector2 client = _controller.clientSize;
                Vector2 window = _controller.windowSize;
                s.ClientWidth = client.x;
                s.ClientHeight = client.y;
                s.WindowWidth = window.x;
                s.WindowHeight = window.y;
                s.TransparentType = (int)_controller.transparentType;

                Camera cam = _controller.currentCamera != null ? _controller.currentCamera : Camera.main;
                if (cam != null)
                {
                    s.CameraBackground = cam.backgroundColor;
                    s.CameraClearFlags = (int)cam.clearFlags;
                    s.CameraAllowHdr = cam.allowHDR;
                    s.CameraAllowMsaa = cam.allowMSAA;
                }
            }

            CaptureOsStyles(ref s);
            CaptureUiSpriteFilter(ref s);
            return s;
        }

        /// <summary>
        /// OS 실측 — 라이브러리 캐시가 아니라 <b>창의 진짜 확장 스타일</b>을 읽는다. 같은 날 다른
        /// 라운드가 topmost에서 "캐시를 진실로 착각했다"는 반증을 겪었으므로(Tasklist 참고),
        /// 알파 쪽도 처음부터 실측으로 간다.
        /// </summary>
        private void CaptureOsStyles(ref OverlayCompositionSnapshot s)
        {
            if (_hwnd == IntPtr.Zero) _hwnd = ResolveOverlayHandle();
            if (_hwnd == IntPtr.Zero) return;

            try
            {
                long exStyle = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
                s.OsStyleReadOk = true;
                s.HasLayeredStyle = (exStyle & WsExLayered) != 0;
                s.HasClickThroughStyle = (exStyle & WsExTransparent) != 0;

                if (GetLayeredWindowAttributes(_hwnd, out uint _, out byte alpha, out uint _))
                {
                    s.LayeredAttributesInEffect = true;
                    s.LayeredAlphaByte = alpha;
                }

                s.DwmCompositionEnabled = DwmIsCompositionEnabled(out bool enabled) == 0 && enabled;
            }
            catch (EntryPointNotFoundException) { s.OsStyleReadOk = false; }
            catch (DllNotFoundException) { s.OsStyleReadOk = false; }
        }

        /// <summary>UiChrome이 굽는 둥근 사각형 스프라이트의 필터 모드(캐시된 것을 읽기만 한다).
        /// "번짐"이 폰트가 아니라 UI 스프라이트 확대 때문일 가능성을 가르는 값이다.</summary>
        private static void CaptureUiSpriteFilter(ref OverlayCompositionSnapshot s)
        {
            try
            {
                Sprite sprite = Interaction.UiChrome.RoundedFill(Interaction.UiChrome.RadiusPanel);
                if (sprite != null && sprite.texture != null) s.UiSpriteFilterMode = (int)sprite.texture.filterMode;
            }
            catch (Exception) { /* 진단이 앱을 죽이지 않는다 — 기본값(Bilinear)을 그대로 둔다. */ }
        }

        /// <summary>설정을 늦게 확보한다 — 이 프로브는 창 부착 <b>실패</b> 상황에서도 살아 있어야 해서
        /// 에이전트보다 먼저 만들어질 수 있다. 한 번 찾으면 캐시한다(상주 비용 0).</summary>
        private Core.StickConfig ResolveConfig()
        {
            if (_config != null) return _config;
            if (_agent == null) _agent = UnityEngine.Object.FindAnyObjectByType<Core.StickmanAgent>();
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard != null) _config = blackboard.Config;
            return _config;
        }

        /// <summary>
        /// 우리 오버레이 창의 HWND. <c>Win32WindowService</c>가 쓰는 것과 <b>같은 방법</b>
        /// (<c>Process.MainWindowHandle</c>)을 쓴다 — 그 파일을 건드리지 않고 같은 값을 얻기 위함이다.
        /// </summary>
        private IntPtr ResolveOverlayHandle()
        {
            try
            {
                IntPtr h = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (h == IntPtr.Zero && !_hwndLookupFailedLogged)
                {
                    _hwndLookupFailedLogged = true;
                    Debug.LogWarning($"{LogPrefix} 오버레이 HWND를 아직 확보하지 못했습니다 " +
                        "(MainWindowHandle=0) — 창 스타일 실측 항목은 보류로 남습니다.");
                }
                return h;
            }
            catch (Exception e)
            {
                if (!_hwndLookupFailedLogged)
                {
                    _hwndLookupFailedLogged = true;
                    Debug.LogWarning($"{LogPrefix} 오버레이 HWND 조회 실패: {e.GetType().Name} — 스타일 실측 보류.");
                }
                return IntPtr.Zero;
            }
        }

        // ==================== Win32 ====================

        private const int GwlExStyle = -20;
        private const long WsExTransparent = 0x00000020L;
        private const long WsExLayered = 0x00080000L;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        /// <summary>32비트 플레이어에는 GetWindowLongPtrW가 없다(별칭이 아니라 아예 없음).
        /// 이 앱은 x86_64로만 출하하지만, 진단 코드가 EntryPointNotFoundException으로
        /// 앱을 시끄럽게 만드는 것보다 폭을 보고 갈라 두는 편이 안전하다.</summary>
        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLayeredWindowAttributes(IntPtr hWnd, out uint crKey, out byte bAlpha, out uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    }
}
#endif
