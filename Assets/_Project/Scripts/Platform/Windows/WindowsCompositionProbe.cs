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

        /// <summary>
        /// 글리프 리샘플 비를 계산할 대표 폰트 크기(pt).
        ///
        /// <para>★ 2026-09-01 — <b>사본을 지우고 진짜 상수를 가리킨다.</b> 여기에 <c>13</c>이 손으로
        /// 적혀 있었고, 그 값이 <see cref="Interaction.UiChrome.FontTitle"/>의 <b>사본</b>이었다.
        /// 같은 라운드에 FontTitle이 14로 옮겨지면(홀수 pt 번짐 수정) 이 줄만 13으로 남아
        /// <c>[합성진단]</c>이 <b>이미 고쳐진 화면을 계속 "번짐"이라고 신고</b>한다 — 진단 도구가
        /// 거짓 경보를 내는 순간 아무도 안 믿게 되므로, 사본을 남기지 않는 편이 낫다.</para>
        ///
        /// <para><b>Platform → Interaction 참조가 괜찮은가</b>: 이 파일은 <b>이미</b> 그 방향으로
        /// 참조한다(<see cref="CaptureUiSpriteFilter"/>가 <c>Interaction.UiChrome.RoundedFill</c>을
        /// 읽는다). 즉 새 결합이 아니라 기존 결합의 재사용이고, 둘 다 <b>읽기만</b> 하는 진단 경로다.
        /// 정책이 아니라 관측값이라 CLAUDE.md의 "정책은 플랫폼 중립 위치에" 규약과도 어긋나지 않는다
        /// (판정 규칙 자체는 <see cref="UiGlyphScalePolicy"/>, 즉 <c>Platform/</c>에 있다).
        /// 같은 어셈블리의 <c>const</c>라 컴파일 시점에 값이 박히므로 상주 비용도 0이다.</para>
        /// </summary>
        private const int SampleFontSizePoints = Interaction.UiChrome.FontTitle;

        /// <summary>지문이 바뀌어도 이 간격 안에서는 두 줄을 찍지 않는다(위 Update의 상한 문서 참고).
        /// 15초면 사용자가 창을 다른 모니터로 옮기는 실험을 해도 각 전이가 한 줄씩 남고,
        /// 드리프트로 인한 폭주는 분당 4줄로 묶인다.</summary>
        private const float LogMinIntervalSeconds = 15f;

        /// <summary>이 프로브가 프로세스 수명 동안 남길 상세 줄의 총량. 진단 가치는 초반 몇 줄에
        /// 거의 전부 있고, 그 뒤로는 같은 사실의 반복이다.</summary>
        private const int MaxLogs = 12;

        private UniWindowController _controller;
        private Core.StickConfig _config;
        private Core.StickmanAgent _agent;
        private float _timer;
        private float _cooldown;
        private int _logCount;
        private int _suppressed;
        private string _lastSignature;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _hwndLookupFailedLogged;
        private int _handleSource = OverlayCompositionVerdict.HandleSourceNone;

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
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.PlatformEnforcer);   // [스톨구간] 계측
            _timer += Time.unscaledDeltaTime;
            if (_cooldown > 0f) _cooldown -= Time.unscaledDeltaTime;
            if (_timer < SampleIntervalSeconds) return;
            _timer = 0f;

            OverlayCompositionSnapshot snapshot = Capture();
            string signature = snapshot.Signature();
            if (signature == _lastSignature) return;   // 전이 없음 — 아무것도 찍지 않는다.
            _lastSignature = signature;

            // ★ 2026-09-01 2차 — "지문이 바뀔 때만"은 상한이 아니다.
            //
            // 이 프로브는 "정상 상태로 안정되면 로그가 완전히 멈춘다"를 전제로 설계됐다. 그런데 관측
            // 항목 중 하나라도 **드리프트하거나 진동하면** 그 전제가 통째로 무너진다. 실제로 같은 날
            // 창 크기가 1px씩 줄어드는 결함이 발견됐고(WindowsOverlayStateEnforcer.TickFullScreenBounds),
            // Signature()는 창 크기를 정수로 포함하므로 그 드리프트마다 **1KB짜리 여러 줄 경고**가
            // 2초에 한 번씩 영원히 찍혔을 것이다. 24시간 상주 앱에서 그것은 파일 IO와 GC 압력이다.
            //
            // 그래서 지문 판정은 그대로 두되(감도를 낮추면 진짜 결함을 놓친다) **찍는 빈도에 상한**을
            // 건다. 억제된 전이는 개수를 세어 다음 줄에 함께 보고하므로 사실을 숨기지 않는다.
            if (_cooldown > 0f || _logCount >= MaxLogs)
            {
                _suppressed++;
                return;
            }
            _cooldown = LogMinIntervalSeconds;
            _logCount++;

            var verdict = OverlayCompositionVerdict.Diagnose(snapshot);
            var sb = new StringBuilder(1024);
            sb.Append(LogPrefix).Append(" 오버레이 알파/합성 상태가 바뀌었습니다");
            if (_suppressed > 0)
            {
                sb.Append($"(직전 줄 이후 {_suppressed}건은 상한으로 억제됨 — 무언가 계속 흔들리고 있다는 신호다)");
                _suppressed = 0;
            }
            sb.Append($" [{_logCount}/{MaxLogs}] — 관측:\n");
            AppendObservations(sb, snapshot);
            sb.Append("  판정:\n");
            for (int i = 0; i < verdict.Count; i++) sb.Append("    ").Append(verdict[i]).Append('\n');
            if (_logCount >= MaxLogs)
            {
                sb.Append("    ※ 이 줄이 이 프로브의 마지막 상세 보고입니다(수명 상한). " +
                    "이후 합성 상태가 또 바뀌어도 로그를 남기지 않습니다 — " +
                    "상주 앱에서 진단이 자원을 무제한으로 먹지 않게 하기 위한 의도된 상한입니다.\n");
            }

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
              .Append($"레이어드속성={(s.LayeredAttributesInEffect ? $"있음(알파 {s.LayeredAlphaByte}/255, dwFlags=0x{s.LayeredFlags:X}, crKey=0x{s.LayeredColorKey:X6})" : "없음")}\n");
            sb.Append($"    핸들/해소기: 관측대상={HandleSourceText(s.OverlayHandleSource)}, ")
              .Append($"하이브리드해소기={(LayeredHybridResolverState)s.HybridResolverState}")
              .Append($"(제거 {s.HybridStripCount}회, {WindowsLayeredHybridResolver.SharedNote})\n");
            sb.Append($"    카메라: clearFlags={(CameraClearFlags)s.CameraClearFlags}, ")
              .Append($"배경=({s.CameraBackground.r:F3},{s.CameraBackground.g:F3},{s.CameraBackground.b:F3},{s.CameraBackground.a:F3}), ")
              .Append($"HDR={s.CameraAllowHdr}, MSAA허용={s.CameraAllowMsaa}\n");
            sb.Append($"    샘플링: MSAA 요청={s.RequestedMsaa}x 실측={s.ActualMsaa}x, ")
              .Append($"UI스프라이트 필터={(FilterMode)s.UiSpriteFilterMode}, ")
              .Append($"GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})\n");
        }

        private static string HandleSourceText(int code) => code switch
        {
            OverlayCompositionVerdict.HandleSourceNativeAgrees => "네이티브==.NET(같은 창)",
            OverlayCompositionVerdict.HandleSourceNativeDiffers => "★네이티브(.NET과 다름)",
            OverlayCompositionVerdict.HandleSourceManagedFallback => ".NET 폴백(네이티브 조회 불가)",
            _ => "미확보",
        };

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
                LayeredFlags = OverlayCompositionVerdict.LayeredFlagsUnknown,
                LayeredColorKey = -1,
                HybridResolverState = (int)WindowsLayeredHybridResolver.SharedState,
                HybridStripCount = WindowsLayeredHybridResolver.SharedStripCount,
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
            s.OverlayHandleSource = _handleSource;
            if (_hwnd == IntPtr.Zero) return;

            try
            {
                long exStyle = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
                s.OsStyleReadOk = true;
                s.HasLayeredStyle = (exStyle & WsExLayered) != 0;
                s.HasClickThroughStyle = (exStyle & WsExTransparent) != 0;

                // ★ 2026-09-01 (debugger) — dwFlags를 <반드시> 함께 읽는다.
                //   예전에는 `out uint _`로 버렸고, 그래서 판정이 "bAlpha < 255 = 창이 비친다"를 단정했다.
                //   bAlpha는 dwFlags에 LWA_ALPHA가 있을 때만 합성에 쓰이는 값이다 — 이 한 줄이 없어서
                //   팀 전체가 잘못된 원인을 한 라운드 동안 쫓았다(OverlayCompositionSnapshot.LayeredFlags 참고).
                if (GetLayeredWindowAttributes(_hwnd, out uint crKey, out byte alpha, out uint flags))
                {
                    s.LayeredAttributesInEffect = true;
                    s.LayeredAlphaByte = alpha;
                    s.LayeredFlags = unchecked((int)flags);
                    s.LayeredColorKey = unchecked((int)crKey);
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
                IntPtr native = UniWinCNativeHandle.TryGetNative();
                IntPtr managed = UniWinCNativeHandle.Fallback();

                if (native != IntPtr.Zero)
                {
                    _handleSource = (managed != IntPtr.Zero && native != managed)
                        ? OverlayCompositionVerdict.HandleSourceNativeDiffers
                        : OverlayCompositionVerdict.HandleSourceNativeAgrees;
                    // 불일치 경고는 UniWinCNativeHandle.Resolve()가 한 번만 남긴다(중복 방지).
                    UniWinCNativeHandle.Resolve();
                    return native;
                }

                if (managed != IntPtr.Zero)
                {
                    _handleSource = OverlayCompositionVerdict.HandleSourceManagedFallback;
                    return managed;
                }

                _handleSource = OverlayCompositionVerdict.HandleSourceNone;
                if (!_hwndLookupFailedLogged)
                {
                    _hwndLookupFailedLogged = true;
                    Debug.LogWarning($"{LogPrefix} 오버레이 HWND를 아직 확보하지 못했습니다 " +
                        "(네이티브/.NET 둘 다 0) — 창 스타일 실측 항목은 보류로 남습니다.");
                }
                return IntPtr.Zero;
            }
            catch (Exception e)
            {
                _handleSource = OverlayCompositionVerdict.HandleSourceNone;
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
