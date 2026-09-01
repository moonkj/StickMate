#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// ★ 2026-09-01 (debugger) — "레이어드 + DWM 확장 프레임" <b>하이브리드 해소기</b>.
    /// 판정 규칙은 전부 플랫폼 중립 순수 함수 <see cref="StickMate.Platform.LayeredHybridPolicy"/>에 있고
    /// (그 파일에 "왜 그냥 지우지 않는가"의 근거 전문이 있다), 이 파일은 <b>OS 관측과 실행</b>만 한다.
    ///
    /// ============================================================================
    /// 이 파일이 하는 Win32 쓰기 호출은 정확히 한 종류다
    /// ============================================================================
    /// <c>SetWindowLongPtr(우리 오버레이 HWND, GWL_EXSTYLE, ...)</c> — <b>우리 자신의 창</b>의 확장
    /// 스타일에서 <c>WS_EX_LAYERED</c> 비트 하나를 끄거나(되돌릴 때) 켠다. 타 프로세스 창은 읽지도 않고,
    /// 위치/크기/Z-order/수명에는 손대지 않는다(원칙 3 준수). 핸들은 <b>단일 출처</b>
    /// (<see cref="OverlayHandle"/> = 네이티브 <c>LibUniWinC.GetWindowHandle()</c>이 돌려준, 라이브러리가
    /// 실제로 조작 중인 바로 그 창)에서만 온다.
    ///
    /// ============================================================================
    /// 24시간 상주 비용
    /// ============================================================================
    /// 평상시 틱 하나는 <c>GetWindowLongPtrW</c> <b>1회</b>다(0.25초 주기 = 초당 4회). 이미 존재하는
    /// <see cref="WindowsTopmostWatchdog"/>이 같은 API를 초당 10회 부르고 있으므로 증분은 그 40%다.
    /// 관통 재검증(<c>GetWindowRect</c> + <c>WindowFromPoint</c>)은 <b>제거 직후 1회</b>와
    /// 그 뒤 <see cref="ReverifyIntervalSeconds"/>(2초)마다 1회뿐이다. 매 프레임 할당은 0이다.
    ///
    /// ============================================================================
    /// 사용자 긴급 차단
    /// ============================================================================
    /// 환경변수 <c>STICKMATE_KEEP_LAYERED=1</c>로 이 해소기를 통째로 끌 수 있다. 실기에서 무언가
    /// 이상하면 재빌드 없이 즉시 원래 형상으로 돌아갈 수 있어야 한다는 원칙(다른 라운드의
    /// <c>STICKMATE_FORCE_MSAA</c>와 같은 관례).
    /// </summary>
    internal sealed class WindowsLayeredHybridResolver
    {
        internal const string LogPrefix = "[레이어드해소]";

        /// <summary>스타일 관측 주기. 라이브러리는 커서가 캐릭터를 벗어날 때마다 레이어드를 다시 켜므로
        /// "한 번 지우고 끝"이 아니다. 0.25초면 사람이 눈치채기 전에 다시 지워지고, 비용은
        /// <c>GetWindowLongPtrW</c> 초당 4회다.</summary>
        private const float SampleIntervalSeconds = 0.25f;

        /// <summary>제거된 상태에서 "관통이 아직도 되는가"를 다시 묻는 주기.</summary>
        private const float ReverifyIntervalSeconds = 2f;

        private const string OptOutEnvVar = "STICKMATE_KEEP_LAYERED";

        private IntPtr _hwnd = IntPtr.Zero;
        private float _timer;
        private float _reverifyTimer;
        private bool _verifiedOnce;
        private bool _disabled;
        private int _stripCount;
        private bool _attributesNeutralized;
        private bool _optOutResolved;
        private bool _optOut;
        private LayeredHybridHold _lastHold = LayeredHybridHold.None;
        private bool _lastHoldLogged;
        private LayeredHybridResolverState _state = LayeredHybridResolverState.Pending;

        /// <summary>진단 프로브가 읽는 현재 상태(같은 어셈블리/네임스페이스 안에서만 쓴다).
        /// 정적으로 두는 이유: 프로브는 창 부착 실패 상황에서도 살아 있어야 해서 해소기 인스턴스를
        /// 참조하지 않는다(<c>WindowsCompositionProbe</c> 클래스 문서 참고).</summary>
        internal static LayeredHybridResolverState SharedState = LayeredHybridResolverState.NotPresent;
        internal static int SharedStripCount;
        internal static string SharedNote = "미가동";

        /// <summary>
        /// 매 프레임 호출. 내부에서 주기를 지킨다.
        ///
        /// <para>핸들은 <b>인자로 받지 않고</b> <see cref="UniWinCNativeHandle.Resolve"/>로 스스로 얻는다 —
        /// 이 해소기가 고쳐야 하는 대상은 "라이브러리가 실제로 레이어드를 건 그 창"이고, 호출부가 들고
        /// 있는 <c>Process.MainWindowHandle</c>이 그 창이라는 보장이 없기 때문이다(그 파일 문서 참고).
        /// 창이 사라지면(IsWindow=false) 다음 틱에 다시 해석한다.</para>
        /// </summary>
        /// <param name="transparentType">UniWindowController.transparentType(1=Alpha).</param>
        internal void Tick(float unscaledDeltaTime, int transparentType)
        {
            _timer += unscaledDeltaTime;
            if (_timer < SampleIntervalSeconds) return;
            _timer = 0f;

            if (_hwnd == IntPtr.Zero || !IsWindowSafe(_hwnd)) _hwnd = UniWinCNativeHandle.Resolve();
            IntPtr overlayHwnd = _hwnd;
            if (overlayHwnd == IntPtr.Zero) return;

            if (!_optOutResolved)
            {
                _optOutResolved = true;
                _optOut = ResolveOptOut();
                if (_optOut)
                {
                    Debug.Log($"{LogPrefix} {OptOutEnvVar}가 설정되어 있어 하이브리드 해소를 하지 않습니다 " +
                        "— 창은 예전과 똑같이 WS_EX_LAYERED + DWM 확장 프레임 하이브리드로 남습니다.");
                }
            }

            if (!TryReadExStyle(overlayHwnd, out long exStyle)) { Publish(LayeredHybridResolverState.Holding,
                "창 스타일 실측 실패"); return; }

            bool hasLayered = (exStyle & WsExLayered) != 0;
            bool hasClickThrough = (exStyle & WsExTransparent) != 0;

            // ---- (A) 이미 제거된 상태: 관통이 계속 유지되는지 상시 재검증 ----
            //   "한 번 통과했으니 영원히 안전하다"고 믿지 않는다. 원칙 2는 이 앱에서 가장 비싼 회귀라
            //   감시 비용(2초에 P/Invoke 2회)을 낼 가치가 충분하다.
            if (!hasLayered && _verifiedOnce && !_disabled)
            {
                _reverifyTimer += SampleIntervalSeconds;
                if (hasClickThrough && _reverifyTimer >= ReverifyIntervalSeconds)
                {
                    _reverifyTimer = 0f;
                    if (!ProbePassThrough(overlayHwnd))
                    {
                        RestoreLayered(overlayHwnd,
                            "상시 재검증에서 클릭 관통이 관측되지 않았습니다(WS_EX_TRANSPARENT는 켜져 있는데 " +
                            "WindowFromPoint가 우리 창을 돌려줍니다)");
                        return;
                    }
                }
                Publish(LayeredHybridResolverState.Verified,
                    $"제거 {_stripCount}회 / 관통 유지 확인됨");
                return;
            }

            // ---- (A2) 레이어드 <속성>이 실제로 해를 끼치고 있으면 그것부터 무해화한다 ----
            //   스타일 제거(아래)와 <독립된> 수리다. 제거가 어떤 사유로 보류/비활성이어도 이건 듣는다.
            //   그리고 스타일에 전혀 손대지 않으므로 <원칙 2(클릭 관통)와 물리적으로 무관>하다.
            if (!_optOut && hasLayered && transparentType == LayeredHybridPolicy.TransparentTypeAlpha)
            {
                NeutralizeHarmfulLayeredAttributes(overlayHwnd);
            }

            // ---- (B) 게이트 ----
            var observation = new LayeredHybridObservation
            {
                OsStyleReadOk = true,
                HasLayeredStyle = hasLayered,
                HasClickThroughStyle = hasClickThrough,
                TransparentType = transparentType,
                Disabled = _disabled,
                OptedOut = _optOut,
                StripCount = _stripCount,
                MaxStrips = LayeredHybridPolicy.DefaultMaxStrips,
            };
            LayeredHybridHold hold = LayeredHybridPolicy.EvaluateGate(observation);
            if (hold != LayeredHybridHold.None)
            {
                NoteHold(hold);
                return;
            }

            // ---- (C) 대조군 — 지우기 전에 "관통을 관측할 수 있는가"를 먼저 확인 ----
            if (LayeredHybridPolicy.RequiresControlProbe(_verifiedOnce))
            {
                bool controlPassThrough = ProbePassThrough(overlayHwnd);
                LayeredHybridHold controlHold = LayeredHybridPolicy.EvaluateControl(controlPassThrough);
                if (controlHold != LayeredHybridHold.None)
                {
                    _disabled = true;
                    NoteHold(controlHold);
                    Debug.LogWarning($"{LogPrefix} 대조군 실패 — {LayeredHybridPolicy.Describe(controlHold)}. " +
                        "레이어드를 <건드리지 않고> 종료합니다(원칙 2 우선). " +
                        "이 줄이 보이면 '[LAYERED+DWM] 하이브리드'는 이번 빌드에서 해소되지 않습니다.");
                    return;
                }
            }

            // ---- (D) 제거 ----
            if (!SetLayered(overlayHwnd, false)) { NoteHold(LayeredHybridHold.StyleUnreadable); return; }

            // ---- (E) 실험군 — 관통이 살아남았는가 ----
            bool stillClickThrough = TryReadExStyle(overlayHwnd, out long after) && (after & WsExTransparent) != 0;
            bool passThroughAfter = stillClickThrough && ProbePassThrough(overlayHwnd);
            if (LayeredHybridPolicy.RequiresRollback(stillClickThrough, passThroughAfter))
            {
                RestoreLayered(overlayHwnd,
                    "제거 직후 검증에서 클릭 관통이 사라졌습니다 — WS_EX_TRANSPARENT 단독으로는 관통이 " +
                    "성립하지 않는 환경입니다");
                return;
            }

            _stripCount++;
            _reverifyTimer = 0f;
            if (!_verifiedOnce)
            {
                _verifiedOnce = true;
                Debug.Log($"{LogPrefix} ★ WS_EX_LAYERED 제거 성공 — <b>WS_EX_TRANSPARENT 단독으로 클릭 관통이 " +
                    "유지됨을 이 머신에서 실측 확인</b>했습니다(대조군/실험군 모두 WindowFromPoint가 우리 창이 " +
                    "아닌 창을 돌려줌). 이제 이 창의 합성 경로는 DWM 확장 프레임 <단일 경로>입니다 — " +
                    "다음 [합성진단] 줄에서 [LAYERED+DWM]과 [LAYERED-ALPHA] 경고가 사라져야 정상입니다. " +
                    $"exStyle 0x{exStyle:X} -> 0x{after:X}. " +
                    "되돌리려면 환경변수 " + OptOutEnvVar + "=1 로 실행하세요.\n" +
                    // ★ 성능 A/B 경계 표식 — 새 계측을 만들지 않는다.
                    //   [스톨귀인]은 스파이크마다 "프레임#"과 "판정: 로직밖(렌더/프레젠트/OS 합성)"을 찍는다.
                    //   합성 경로 이중화가 그 '로직밖'에 기여하는지는 <이 프레임 번호를 경계로 앞뒤를
                    //   나눠 보면> 알 수 있다. 같은 실행 안의 비교라 하드웨어/드라이버/창 구성이 동일하다.
                    $"    ★A/B 경계: 프레임 #{Time.frameCount}. 이 번호보다 <작은> [스톨귀인] 줄은 " +
                    "'레이어드+DWM 하이브리드' 상태이고, <큰> 줄은 'DWM 단일 경로' 상태입니다. " +
                    "두 구간의 '로직밖 N.Nms'를 비교하면 하이브리드가 합성 대기에 기여하는지가 갈립니다. " +
                    $"더 깨끗한 대조가 필요하면 {OptOutEnvVar}=1로 한 번 더 실행해 전 구간을 하이브리드로 " +
                    "고정한 뒤 같은 값을 비교하세요(같은 빌드 · 환경변수 하나 차이).");
            }
            Publish(LayeredHybridResolverState.Verified, $"제거 {_stripCount}회 / 관통 유지 확인됨");
        }

        /// <summary>
        /// 레이어드 <b>속성</b>(SetLayeredWindowAttributes)이 실제로 합성에 적용 중이고 창을 흐리게/
        /// 뚫리게 만들고 있으면, 그 속성만 무해한 값(완전 불투명, 색 키 없음)으로 되돌린다.
        ///
        /// <para><b>언제 부르지 않는가가 더 중요하다.</b> dwFlags에 LWA_ALPHA/LWA_COLORKEY가 하나도 없으면
        /// 그 속성은 합성에 아무 영향이 없으므로 <b>건드리지 않는다</b> — 여기서 굳이 LWA_ALPHA를 켜면
        /// 지금은 없는 "창 단위 알파" 경로를 우리 손으로 새로 만드는 셈이다. transparentType이 Alpha가
        /// 아닐 때(ColorKey)도 호출부에서 걸러진다 — 그 경로는 색 키가 <b>있어야</b> 동작한다.</para>
        /// </summary>
        private void NeutralizeHarmfulLayeredAttributes(IntPtr hwnd)
        {
            if (_attributesNeutralized) return;
            try
            {
                if (!GetLayeredWindowAttributes(hwnd, out uint crKey, out byte alpha, out uint flags)) return;
                bool alphaHarmful = (flags & LwaAlpha) != 0 && alpha < 255;
                bool keyHarmful = (flags & LwaColorKey) != 0;
                if (!alphaHarmful && !keyHarmful) return;

                if (!SetLayeredWindowAttributes(hwnd, 0u, 255, LwaAlpha)) return;
                _attributesNeutralized = true;
                Debug.LogWarning($"{LogPrefix} ★ 레이어드 <속성>이 실제로 적용 중이었습니다 — " +
                    $"알파 {alpha}/255, dwFlags=0x{flags:X}, crKey=0x{crKey:X6}. " +
                    "이건 uGUI와 무관한 <창 단위> 반투명/색키라 UI를 아무리 고쳐도 사라지지 않습니다. " +
                    "완전 불투명(LWA_ALPHA 255, 색 키 없음)으로 되돌렸습니다 — " +
                    "다음 [합성진단] 줄에서 [LAYERED-ALPHA]/[LAYERED-COLORKEY]가 사라져야 정상입니다.");
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
        }

        /// <summary>보류 사유는 <b>바뀔 때 한 번만</b> 찍는다(24시간 상주 — 같은 사유를 초당 4번 찍지 않는다).</summary>
        private void NoteHold(LayeredHybridHold hold)
        {
            var state = hold == LayeredHybridHold.NotHybrid
                ? (_verifiedOnce ? LayeredHybridResolverState.Verified : LayeredHybridResolverState.Pending)
                : hold == LayeredHybridHold.Disabled ? LayeredHybridResolverState.RolledBack
                : LayeredHybridResolverState.Holding;
            Publish(state, LayeredHybridPolicy.Describe(hold));

            if (hold == _lastHold && _lastHoldLogged) return;
            _lastHold = hold;
            _lastHoldLogged = true;
            // 정상 두 사유(이미 목표 형상 / 지금은 관통이 꺼짐)는 매우 자주 오가므로 로그를 남기지 않는다.
            if (hold == LayeredHybridHold.NotHybrid || hold == LayeredHybridHold.ClickThroughOffRightNow) return;
            Debug.Log($"{LogPrefix} 보류 — {LayeredHybridPolicy.Describe(hold)}.");
        }

        private void RestoreLayered(IntPtr hwnd, string why)
        {
            SetLayered(hwnd, true);
            _disabled = true;
            Publish(LayeredHybridResolverState.RolledBack, "검증 실패로 되돌림");
            Debug.LogWarning($"{LogPrefix} ★ 되돌림 — {why}. WS_EX_LAYERED를 즉시 복구하고 이 해소기를 " +
                "영구 비활성화했습니다. <b>클릭 관통(원칙 2)은 손상되지 않았습니다</b> — 되돌림은 같은 프레임에 " +
                "끝나며, 그 사이 사용자 입력은 처리되지 않습니다. " +
                "이 줄은 'WS_EX_TRANSPARENT 단독으로는 관통이 성립하지 않는다'는 <실측 사실>이므로 " +
                "다음 라운드는 레이어드 제거가 아니라 다른 방향을 잡아야 합니다.");
        }

        private void Publish(LayeredHybridResolverState state, string note)
        {
            _state = state;
            SharedState = state;
            SharedStripCount = _stripCount;
            SharedNote = note;
        }

        internal LayeredHybridResolverState State => _state;

        private static bool ResolveOptOut()
        {
            try
            {
                string v = Environment.GetEnvironmentVariable(OptOutEnvVar);
                return !string.IsNullOrEmpty(v) && v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// <b>OS 자신에게 묻는 클릭 관통 판정.</b> 우리 창 사각형 안의 한 점에서
        /// <c>WindowFromPoint</c>가 우리 HWND가 <b>아닌</b> 것을 돌려주면 그 점의 마우스 입력은 우리를
        /// 통과한다는 뜻이다(창 관리자의 히트테스트가 쓰는 바로 그 함수).
        ///
        /// <para>점은 커서 위치가 아니라 <b>창 중앙</b>을 쓴다 — 커서를 쓰면 "지금 커서가 캐릭터 위인가"에
        /// 판정이 오염된다. 아래에 아무 창도 없으면 바탕화면 핸들이 돌아오는데, 그것도 "우리가 아님"이라
        /// 판정에는 아무 문제가 없다.</para>
        /// </summary>
        private static bool ProbePassThrough(IntPtr hwnd)
        {
            try
            {
                if (!GetWindowRect(hwnd, out RECT r)) return false;
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                if (w <= 2 || h <= 2) return false;
                var p = new POINT { X = r.Left + w / 2, Y = r.Top + h / 2 };
                IntPtr hit = WindowFromPoint(p);
                return hit != hwnd;
            }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
        }

        private static bool IsWindowSafe(IntPtr hwnd)
        {
            try { return IsWindow(hwnd); }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
        }

        private static bool TryReadExStyle(IntPtr hwnd, out long exStyle)
        {
            exStyle = 0;
            try
            {
                if (!IsWindow(hwnd)) return false;
                exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
                // GetWindowLongPtr은 실패를 0으로 알린다(SetLastError를 따로 읽지 않는 이 코드에서는
                // 구분 불가). 우리 오버레이는 항상 최소 WS_EX_TOPMOST/WS_EX_TRANSPARENT 중 하나를 갖고
                // 있으므로 0은 사실상 실패다. 설령 진짜 0이어도 결과는 "아무것도 하지 않음"이라 안전하다.
                return exStyle != 0;
            }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
        }

        /// <summary>우리 창의 WS_EX_LAYERED 비트 하나만 켜고 끈다. 다른 비트는 읽은 그대로 되쓴다.</summary>
        private static bool SetLayered(IntPtr hwnd, bool on)
        {
            try
            {
                if (!IsWindow(hwnd)) return false;
                long ex = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
                if (ex == 0) return false;
                long next = on ? (ex | WsExLayered) : (ex & ~WsExLayered);
                if (next == ex) return true;
                SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(next));
                return true;
            }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
        }

        // ==================== Win32 ====================

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private const int GwlExStyle = -20;
        private const long WsExTransparent = 0x00000020L;
        private const long WsExLayered = 0x00080000L;
        private const uint LwaColorKey = 0x00000001;
        private const uint LwaAlpha = 0x00000002;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLayeredWindowAttributes(IntPtr hWnd, out uint crKey, out byte bAlpha, out uint dwFlags);

        /// <summary>우리 창의 레이어드 <b>속성</b>만 바꾼다(스타일 비트는 건드리지 않는다).</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>32비트 플레이어에는 *Ptr 별칭이 존재하지 않는다(별칭이 아니라 심볼 자체가 없다).
        /// 이 앱은 x86_64로만 출하하지만 폭을 보고 갈라 두는 편이 안전하다.</summary>
        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

        private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, nIndex, value);
            else SetWindowLong32(hWnd, nIndex, value.ToInt32());
        }
    }
}
#endif
