using System.Globalization;
using System.Text;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-01 — 오버레이 창의 <b>알파/합성 상태 한 장</b>(플랫폼 중립 자료구조).
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼는가 — "맥에서만 고치고 맥에서만 검증"을 끝내기 위해
    /// ============================================================================
    /// 이 저장소에는 Windows 런타임이 없다. 그래서 "창이 겹쳐 보인다 / 텍스트가 번진다" 같은 신고가
    /// 오면 지금까지는 <b>추측으로 고치고 사용자에게 다시 물어보는</b> 왕복만 반복했다(같은 신고 3회).
    /// 이 클래스는 그 왕복을 <b>로그 한 줄</b>로 줄이기 위한 것이다: 실기에서 관측 가능한 값만 모아
    /// <see cref="OverlayCompositionSnapshot"/>에 담고, <b>판정 자체는 순수 함수</b>
    /// (<see cref="OverlayCompositionVerdict.Diagnose"/>)에 둔다.
    ///
    /// <b>판정을 순수 함수로 뺀 이유가 핵심이다.</b> 그래야 Windows 전용 판정 로직을 macOS 개발
    /// 머신의 EditMode 테스트가 <b>합성 입력으로 전수 검증</b>할 수 있다
    /// (Tests/EditMode/OverlayCompositionDiagnosticsTests.cs). 관측(P/Invoke)만 Windows 전용
    /// 파일에 남는다 — 이 저장소가 이미 쓰는 "크로스 컴파일 + 순수 함수 검증" 기법과 같은 형태다.
    ///
    /// ============================================================================
    /// 이 값들이 왜 <b>알파/합성</b>을 가르는가 (Windows 경로 근거)
    /// ============================================================================
    /// Windows 투명화는 네이티브 LibUniWinC의 <c>enableTransparentByDWM()</c> —
    /// <c>DwmExtendFrameIntoClientArea(hWnd, MARGINS{-1})</c> 한 줄이다(패키지 소스 실측).
    /// 즉 <b>프레임버퍼 알파가 곧 OS 합성 마스크</b>이고, DWM은 그 표면을 <b>프리멀티플라이드</b>로
    /// 읽는다. 그래서 다음 네 가지가 각각 독립적으로 "겹쳐 보임/번져 보임"을 만든다:
    ///   (1) 카메라 clear가 (0,0,0,0)이 아님        -> 알파 마스크 자체가 무효/전체 화면 발색
    ///   (2) 백버퍼 크기 != 창 클라이언트 크기        -> 표시 단계 리샘플 = <b>모든 획이 두 겹으로 번짐</b>
    ///   (3) 캔버스 배율이 정수가 아님                -> 레거시 uGUI 글리프가 비정수 배율로 확대 = 흐린 글자
    ///   (4) WS_EX_LAYERED 가 DWM 유리와 함께 걸림    -> 합성 경로가 둘로 갈림(레이어드 + 확장 프레임)
    /// (4)는 우리가 <b>의도한 적 없는</b> 부작용이다: 원칙 2의 클릭 관통(ON 기본)이 네이티브
    /// <c>SetClickThrough(TRUE)</c>에서 <c>WS_EX_TRANSPARENT | WS_EX_LAYERED</c>를 함께 켠다.
    /// macOS의 클릭 관통(<c>ignoresMouseEvents</c>)은 합성 경로를 <b>건드리지 않는다</b> —
    /// 두 플랫폼이 갈리는 지점이 바로 여기다.
    ///
    /// ============================================================================
    /// ★ 2026-09-01 (debugger) — 이 판정기가 <b>스스로 틀렸던</b> 두 곳
    /// ============================================================================
    /// 이 파일의 판정이 팀을 잘못된 원인으로 한 라운드 끌고 갔다. 두 결함 모두 "관측을 덜 하고
    /// 결론을 더 냈다"는 같은 모양이다:
    ///   · <b>[LAYERED-ALPHA]</b> — <c>GetLayeredWindowAttributes</c>의 <c>dwFlags</c>를 버리고
    ///     <c>bAlpha</c>만 읽어 "창 전체가 100% 비칩니다"를 단정했다. bAlpha는 <c>LWA_ALPHA</c>가
    ///     dwFlags에 있을 때만 합성에 쓰인다. 그리고 그 결론이 참이었다면 <b>화면에 앱이 아예 보이지
    ///     않아야 하는데</b> 사용자는 캐릭터를 보고 있었다 — 판정이 관측과 모순이었다.
    ///     → dwFlags/crKey를 함께 읽고, 적용되지 않는 속성은 <c>[LAYERED-INERT]</c>로 <b>무해</b>하다고
    ///       명시한다.
    ///   · <b>[GLYPH-SCALE]</b> — "배율이 정수가 아니면 번짐"으로 판정했다. 배율 1.5에서도 짝수 pt는
    ///     잔차가 0이다. → 잔차 자체를 판정하고, 처방도 "배율을 정수로"가 아니라 "pt를 배율에 맞춰"로
    ///     바꿨다(전자는 UI 물리 크기를 33% 바꿔 이미 해결된 신고를 되살린다).
    /// 그리고 관측 대상이 <b>정말 그 창인지</b>를 <see cref="OverlayHandleSource"/>로 처음 기록한다 —
    /// 네이티브와 .NET이 서로 다른 규칙으로 창을 고르므로 같은 창이라는 보장이 없었다.
    /// </summary>
    public struct OverlayCompositionSnapshot
    {
        // ---- 창/백버퍼 기하 (증상 "번짐"의 1순위 갈림길) ----
        /// <summary>Unity가 실제로 그리는 백버퍼 크기(= Screen.width/height).</summary>
        public int BackBufferWidth;
        public int BackBufferHeight;
        /// <summary>OS 창의 클라이언트 크기(UniWindowController.clientSize). 0 이하면 미관측.</summary>
        public float ClientWidth;
        public float ClientHeight;
        /// <summary>OS 창의 전체 사각형 크기(UniWindowController.windowSize).</summary>
        public float WindowWidth;
        public float WindowHeight;
        /// <summary>Screen.fullScreenMode를 int로. 3 = FullScreenMode.Windowed.</summary>
        public int FullScreenMode;

        // ---- 배율 (증상 "번짐"의 2순위 갈림길) ----
        /// <summary>ScreenCoordinateConverter.ResolveCanvasScaleFactor 결과(= CanvasScaler.scaleFactor).</summary>
        public float CanvasScaleFactor;
        /// <summary>플랫폼이 OS에서 직접 읽어 보고한 UI 밀도(Windows: GetDpiForWindow/96). 0이면 미보고.</summary>
        public float UiDensityScale;
        /// <summary>좌표 변환용 배율(창 폭 / Screen.width). Windows에서는 1.0이 정상이다.</summary>
        public float AutoDpiScale;
        /// <summary>실측 대표 폰트 크기(pt). 글리프 리샘플 비를 계산하는 데만 쓴다.</summary>
        public int SampleFontSizePoints;

        // ---- 합성 경로 (증상 "겹침"의 갈림길) ----
        /// <summary>UniWindowController.TransparentType. 1 = Alpha(DWM 확장 프레임), 2 = ColorKey.</summary>
        public int TransparentType;
        /// <summary>라이브러리 캐시가 아니라 <b>OS 실측</b>: 창에 WS_EX_LAYERED가 걸려 있는가.</summary>
        public bool HasLayeredStyle;
        /// <summary>OS 실측: WS_EX_TRANSPARENT(클릭 관통)가 걸려 있는가.</summary>
        public bool HasClickThroughStyle;
        /// <summary>GetLayeredWindowAttributes가 성공했는가(= 레이어드 속성이 실제로 설정돼 있는가).</summary>
        public bool LayeredAttributesInEffect;
        /// <summary>레이어드 속성이 있을 때의 알파 바이트(0~255). 없으면 -1.</summary>
        public int LayeredAlphaByte;
        /// <summary>
        /// <b>GetLayeredWindowAttributes의 dwFlags</b>(1=LWA_COLORKEY, 2=LWA_ALPHA, 조합 가능). 미관측이면 -1.
        ///
        /// <para>★ 2026-09-01 (debugger) — 이 필드가 없어서 판정이 틀렸다. 이전 버전은 <c>bAlpha</c>만 읽고
        /// "레이어드 알파=0/255 — 창 전체가 100% 비칩니다"를 단정했는데, <b>bAlpha는 dwFlags에
        /// LWA_ALPHA가 없으면 합성에 아무 영향을 주지 않는다</b>(설정되지 않은 값이 0으로 돌아오는 것이
        /// 정상이다). 실기 로그가 정확히 그 모양이었고, 그 판정이 사실이라면 <b>화면에 앱이 아예 보이지
        /// 않아야 하는데</b> 사용자는 캐릭터를 보고 있었다 — 판정이 자기 자신과 모순이었다.
        /// 지표를 고치지 않고 결론만 반복하는 것이 이 저장소가 오늘 네 번 겪은 실패 패턴이다.</para>
        /// </summary>
        public int LayeredFlags;
        /// <summary>레이어드 색 키(COLORREF). 미관측이면 -1.</summary>
        public int LayeredColorKey;
        /// <summary>
        /// 이 관측이 <b>어느 창</b>의 것인가. 0=핸들 미확보, 1=네이티브(LibUniWinC)와 .NET이 같은 창,
        /// 2=<b>두 값이 다름</b>(네이티브 창을 쟀다), 3=네이티브를 못 얻어 .NET 창을 쟀다.
        ///
        /// <para>2번이면 <b>지금까지의 모든 스타일/알파 판정이 라이브러리가 투명화한 창이 아닌 다른 창의
        /// 것</b>이었다는 뜻이다. LibUniWinC는 우리 PID의 첫 최상위 창의 <c>GW_OWNER</c>를 붙잡고,
        /// .NET <c>MainWindowHandle</c>은 "보이고 오너 없는" 첫 창을 고른다 — 같은 창이라는 보장이 없다.</para>
        /// </summary>
        public int OverlayHandleSource;
        /// <summary>레이어드/DWM 하이브리드 해소기의 상태(<see cref="LayeredHybridResolverState"/>).</summary>
        public int HybridResolverState;
        /// <summary>해소기가 지금까지 WS_EX_LAYERED를 떼어낸 횟수. <b>지문에는 넣지 않는다</b> —
        /// 커서가 캐릭터를 벗어날 때마다 늘어나므로 지문에 넣으면 로그가 폭주한다.</summary>
        public int HybridStripCount;
        /// <summary>DwmIsCompositionEnabled. false면 DWM 투명화 자체가 성립하지 않는다.</summary>
        public bool DwmCompositionEnabled;
        /// <summary>OS 실측 조회에 성공했는가. false면 위 4개 필드는 의미가 없다.</summary>
        public bool OsStyleReadOk;

        // ---- 카메라 clear (알파 마스크의 출발점) ----
        public Color CameraBackground;
        /// <summary>CameraClearFlags를 int로. 2 = SolidColor.</summary>
        public int CameraClearFlags;
        public bool CameraAllowHdr;
        public bool CameraAllowMsaa;

        // ---- 샘플링/필터링 (증상 "번짐"의 3순위) ----
        public int RequestedMsaa;
        public int ActualMsaa;
        /// <summary>UiChrome이 굽는 둥근 사각형 스프라이트의 필터 모드(0=Point, 1=Bilinear, 2=Trilinear).</summary>
        public int UiSpriteFilterMode;

        /// <summary>
        /// <b>전이 감지용 지문</b>. 이 문자열이 바뀔 때만 로그를 남긴다(24시간 상주 앱 — 매 프레임 금지).
        /// 판정 문장이 아니라 <b>관측값</b>으로 만든다: 판정 규칙을 고쳐도 "상태가 바뀌었다"는 신호는
        /// 흔들리지 않아야 하기 때문이다.
        /// </summary>
        public string Signature()
        {
            var sb = new StringBuilder(160);
            sb.Append(BackBufferWidth).Append('x').Append(BackBufferHeight).Append('|');
            sb.Append(Mathf.RoundToInt(ClientWidth)).Append('x').Append(Mathf.RoundToInt(ClientHeight)).Append('|');
            sb.Append(Mathf.RoundToInt(WindowWidth)).Append('x').Append(Mathf.RoundToInt(WindowHeight)).Append('|');
            sb.Append(FullScreenMode).Append('|');
            sb.Append(CanvasScaleFactor.ToString("F3", CultureInfo.InvariantCulture)).Append('|');
            sb.Append(UiDensityScale.ToString("F3", CultureInfo.InvariantCulture)).Append('|');
            sb.Append(AutoDpiScale.ToString("F3", CultureInfo.InvariantCulture)).Append('|');
            sb.Append(TransparentType).Append('|');
            sb.Append(OsStyleReadOk ? '1' : '0');
            sb.Append(HasLayeredStyle ? '1' : '0');
            sb.Append(HasClickThroughStyle ? '1' : '0');
            sb.Append(LayeredAttributesInEffect ? '1' : '0').Append('|');
            sb.Append(LayeredAlphaByte).Append('/').Append(LayeredFlags).Append('|');
            sb.Append(OverlayHandleSource).Append('/').Append(HybridResolverState).Append('|');
            sb.Append(DwmCompositionEnabled ? '1' : '0').Append('|');
            sb.Append(CameraClearFlags).Append('|');
            sb.Append(((Color32)CameraBackground).r).Append(',')
              .Append(((Color32)CameraBackground).g).Append(',')
              .Append(((Color32)CameraBackground).b).Append(',')
              .Append(((Color32)CameraBackground).a).Append('|');
            sb.Append(RequestedMsaa).Append('/').Append(ActualMsaa).Append('|');
            sb.Append(UiSpriteFilterMode);
            return sb.ToString();
        }
    }

    /// <summary>이 관측이 어떤 증상을 만드는가 — 판정 한 건.</summary>
    public enum CompositionFault
    {
        /// <summary>이상 없음(참고용 관측 줄).</summary>
        None = 0,
        /// <summary>"창이 겹쳐 보인다"(뒤 창/바탕화면이 비침) 쪽 원인.</summary>
        SeeThrough = 1,
        /// <summary>"텍스트/획이 번져 보인다"(리샘플·흐림) 쪽 원인.</summary>
        Blur = 2,
        /// <summary>둘 다 만들 수 있다.</summary>
        Both = 3,
    }

    /// <summary>
    /// ★ 순수 판정기 — <b>Windows 실기 로그 한 줄로 원인을 가르기 위한 규칙 전부</b>가 여기 있다.
    /// UnityEngine 타입은 <see cref="Color"/>/<see cref="Mathf"/>만 쓰고 OS 호출은 하나도 없으므로
    /// macOS EditMode 테스트가 합성 입력으로 모든 분기를 돌릴 수 있다.
    /// </summary>
    public static class OverlayCompositionVerdict
    {
        /// <summary>Windows에서 정상인 값들(상수화해 테스트와 로그가 같은 기준을 쓴다).</summary>
        public const int TransparentTypeAlpha = 1;
        public const int TransparentTypeColorKey = 2;
        public const int ClearFlagsSolidColor = 2;
        public const int FullScreenModeWindowed = 3;

        /// <summary>GetLayeredWindowAttributes의 dwFlags 비트. <b>이 비트가 없으면 그 값은 합성에
        /// 아무 영향이 없다</b> — 2026-09-01에 이 사실을 무시한 판정이 팀 전체를 잘못된 원인으로
        /// 끌고 갔다(위 <see cref="OverlayCompositionSnapshot.LayeredFlags"/> 문서 참고).</summary>
        public const int LwaColorKey = 0x00000001;
        public const int LwaAlpha = 0x00000002;
        /// <summary>dwFlags를 읽지 못한(구 빌드/조회 실패) 상태.</summary>
        public const int LayeredFlagsUnknown = -1;

        /// <summary>오버레이 핸들 출처(<see cref="OverlayCompositionSnapshot.OverlayHandleSource"/>).</summary>
        public const int HandleSourceNone = 0;
        public const int HandleSourceNativeAgrees = 1;
        public const int HandleSourceNativeDiffers = 2;
        public const int HandleSourceManagedFallback = 3;

        /// <summary>배율/크기 비교 허용 오차. 1픽셀 어긋나도 표시 단계 리샘플이 일어나므로 좁게 잡는다.</summary>
        private const float SizeEpsilon = 0.5f;
        private const float RatioEpsilon = 0.001f;

        /// <summary>판정 한 줄.</summary>
        public struct Line
        {
            public string Code;
            public CompositionFault Fault;
            public string Text;
            public override string ToString()
                => Fault == CompositionFault.None ? $"[{Code}] {Text}" : $"[{Code}!{FaultTag(Fault)}] {Text}";
        }

        private static string FaultTag(CompositionFault f) => f switch
        {
            CompositionFault.SeeThrough => "겹침",
            CompositionFault.Blur => "번짐",
            CompositionFault.Both => "겹침+번짐",
            _ => "",
        };

        /// <summary>
        /// 관측 한 장을 판정 줄들로 바꾼다. <b>순서가 곧 우선순위</b>다 — 앞줄이 참이면 뒷줄의 미세한
        /// 원인은 볼 필요가 없다(예: 전체 화면이 리샘플되고 있으면 글리프 배율은 부차적이다).
        /// </summary>
        public static System.Collections.Generic.List<Line> Diagnose(OverlayCompositionSnapshot s)
        {
            var lines = new System.Collections.Generic.List<Line>(12);

            // ---------- (A) 합성 경로 자체가 성립하는가 ----------
            if (!s.DwmCompositionEnabled)
            {
                Add(lines, "DWM-OFF", CompositionFault.Both,
                    "DwmIsCompositionEnabled=false — DWM 합성이 꺼져 있어 DwmExtendFrameIntoClientArea " +
                    "투명화가 성립하지 않습니다. 이 상태에서는 알파를 아무리 1로 맞춰도 의미가 없습니다.");
            }

            if (s.TransparentType == TransparentTypeColorKey)
            {
                Add(lines, "COLORKEY", CompositionFault.SeeThrough,
                    "transparentType=ColorKey — 이 경로에서는 프레임버퍼 알파가 마스크가 아니라 " +
                    "'키 색과 같은 화소만 뚫린다'입니다. 우리 UI 색이 우연히 키 색과 같으면 그 부분만 " +
                    "구멍이 나고, 알파 기반 회귀 테스트는 전부 무의미해집니다. Alpha(=1)여야 합니다.");
            }
            else if (s.TransparentType != TransparentTypeAlpha)
            {
                Add(lines, "TRANSPARENT-TYPE", CompositionFault.SeeThrough,
                    $"transparentType={s.TransparentType} — 알려진 값(1=Alpha, 2=ColorKey)이 아닙니다.");
            }

            // ---------- (B) 알파 마스크의 출발점: 카메라 clear ----------
            if (s.CameraClearFlags != ClearFlagsSolidColor)
            {
                Add(lines, "CLEAR-FLAGS", CompositionFault.SeeThrough,
                    $"카메라 clearFlags={s.CameraClearFlags}(SolidColor=2가 아님) — 프레임버퍼 알파가 " +
                    "매 프레임 0으로 초기화되지 않으면 OS 합성 마스크가 이전 프레임과 섞입니다.");
            }
            if (Mathf.Abs(s.CameraBackground.a) > 0.004f)
            {
                Add(lines, "CLEAR-ALPHA", CompositionFault.SeeThrough,
                    $"카메라 배경 알파={s.CameraBackground.a:F3} — 0이어야 합니다. 0이 아니면 " +
                    "아무것도 그리지 않은 화소까지 창이 덮어 바탕화면 전체가 우리 색으로 덮입니다.");
            }
            float clearRgbMax = Mathf.Max(s.CameraBackground.r, Mathf.Max(s.CameraBackground.g, s.CameraBackground.b));
            if (clearRgbMax > 0.02f)
            {
                Add(lines, "CLEAR-RGB", CompositionFault.Both,
                    $"카메라 배경 RGB=({s.CameraBackground.r:F2},{s.CameraBackground.g:F2},{s.CameraBackground.b:F2}) — " +
                    "검정이어야 합니다. DWM은 창 표면을 <프리멀티플라이드>로 읽으므로 알파 0인 화소도 " +
                    "RGB가 밝으면 그대로 <더해집니다>(out = rgb + 뒤배경). 화면 전체가 뿌옇게 밝아지고 " +
                    "캐릭터 가장자리에 밝은 프린지가 남습니다. " +
                    "WindowsOverlayStateEnforcer.ApplyTransparentSafeCameraBackground()가 아직 안 돌았거나 " +
                    "투명 확인에 실패했다는 뜻입니다(씬 에셋의 초기값은 0.94 회색입니다).");
            }

            // ---------- (C) 표시 단계 리샘플 — "번짐"의 1순위 ----------
            bool haveClient = s.ClientWidth > 0.5f && s.ClientHeight > 0.5f;
            if (haveClient && s.BackBufferWidth > 0 && s.BackBufferHeight > 0)
            {
                float rx = s.ClientWidth / s.BackBufferWidth;
                float ry = s.ClientHeight / s.BackBufferHeight;
                bool mismatch = Mathf.Abs(s.ClientWidth - s.BackBufferWidth) > SizeEpsilon
                             || Mathf.Abs(s.ClientHeight - s.BackBufferHeight) > SizeEpsilon;
                if (mismatch)
                {
                    Add(lines, "RESAMPLE", CompositionFault.Blur,
                        $"백버퍼({s.BackBufferWidth}x{s.BackBufferHeight}) != 창 클라이언트" +
                        $"({s.ClientWidth:F0}x{s.ClientHeight:F0}) — 배율 ({rx:F4}, {ry:F4}). " +
                        "표시 단계에서 화면 전체가 한 번 리샘플됩니다. 1px 획과 글리프가 " +
                        "<두 겹으로 살짝 어긋나 겹쳐> 보이는 증상('유령 획')의 직접 원인이며, " +
                        "알파 채널도 함께 보간되어 가장자리에 비침 구멍이 생깁니다.");
                }
                else
                {
                    Add(lines, "RESAMPLE", CompositionFault.None,
                        $"백버퍼 == 창 클라이언트({s.BackBufferWidth}x{s.BackBufferHeight}) — 표시 단계 리샘플 없음.");
                }
            }
            else
            {
                Add(lines, "RESAMPLE", CompositionFault.None,
                    "클라이언트 크기를 아직 못 읽었습니다(창 부착 전). 리샘플 판정 보류.");
            }

            if (s.FullScreenMode != FullScreenModeWindowed)
            {
                Add(lines, "FULLSCREEN-MODE", CompositionFault.Blur,
                    $"Screen.fullScreenMode={s.FullScreenMode}(Windowed=3가 아님) — 전체화면 계열 모드에서는 " +
                    "Unity가 렌더 결과를 디스플레이 해상도로 <스케일>할 수 있고, 창 모드 전용인 " +
                    "레이어드/확장 프레임 합성과도 전제가 어긋납니다. 오버레이는 항상 Windowed여야 합니다.");
            }

            // ---------- (D) 캔버스 배율 — "번짐"의 2순위(폰트 래스터화) ----------
            //
            // ★ 2026-09-01 (debugger) — 판정 기준을 "배율이 정수인가"에서 <b>"이 폰트 크기의 잔차가
            //   0인가"</b>로 바꿨다. 둘은 같은 질문이 아니다: 배율 1.5에서도 <b>짝수 pt</b>는
            //   pt×1.5가 정수라 리샘플이 <b>전혀 없다</b>(14pt -> 21.0px). 예전 판정은 배율만 보고
            //   그런 경우까지 "번짐"으로 찍어, 실제로는 멀쩡한 텍스트를 원인 후보로 올렸다.
            if (s.CanvasScaleFactor > 0f && s.SampleFontSizePoints > 0)
            {
                float requestedPixels = s.SampleFontSizePoints * s.CanvasScaleFactor;
                int atlasPixels = Mathf.Max(1, Mathf.RoundToInt(requestedPixels));
                float glyphRatio = requestedPixels / atlasPixels;
                bool integerScale = Mathf.Abs(s.CanvasScaleFactor - Mathf.Round(s.CanvasScaleFactor)) <= RatioEpsilon;
                bool exactAtThisSize = Mathf.Abs(glyphRatio - 1f) <= RatioEpsilon;

                if (!exactAtThisSize)
                {
                    Add(lines, "GLYPH-SCALE", CompositionFault.Blur,
                        $"캔버스 배율={s.CanvasScaleFactor:F3}(정수: {integerScale}). 대표 폰트 " +
                        $"{s.SampleFontSizePoints}pt는 아틀라스에 {atlasPixels}px로 구워진 뒤 " +
                        $"{glyphRatio:F4}배로 <비정수 확대>되어 화면에 올라갑니다 — 레거시 uGUI Text의 " +
                        "글자가 흐려지는 구조적 원인입니다(알파 문제가 아닙니다). " +
                        "★ 처방은 <캔버스 배율을 정수로 바꾸는 것이 아닙니다> — 그러면 UI의 물리적 크기가 " +
                        $"{(Mathf.Round(s.CanvasScaleFactor) / s.CanvasScaleFactor - 1f) * 100f:F0}% 바뀌어 " +
                        "2026-08-31에 이미 해결된 신고(글씨가 너무 작다/크다)가 되살아납니다. " +
                        $"옳은 처방은 <폰트 pt를 배율에 맞추는 것>입니다: 배율 {s.CanvasScaleFactor:F3}에서는 " +
                        $"pt×{s.CanvasScaleFactor:F3}가 정수인 크기(예: {NearestExactPoints(s.SampleFontSizePoints, s.CanvasScaleFactor)}pt)만 " +
                        "잔차 0으로 구워집니다.");
                }
                else
                {
                    Add(lines, "GLYPH-SCALE", CompositionFault.None,
                        $"캔버스 배율={s.CanvasScaleFactor:F3}에서 대표 폰트 {s.SampleFontSizePoints}pt는 " +
                        $"정확히 {atlasPixels}px로 구워집니다 — 글리프 리샘플 없음" +
                        (integerScale ? "(배율도 정수)." : "(배율은 정수가 아니지만 이 크기에서는 잔차가 0이다)."));
                }
            }

            if (s.UiDensityScale > 0f && s.AutoDpiScale > 0f
                && Mathf.Abs(s.AutoDpiScale - 1f) > 0.01f)
            {
                Add(lines, "COORD-SCALE", CompositionFault.None,
                    $"AutoDpiScale={s.AutoDpiScale:F3} — Windows에서는 창 좌표도 Screen도 물리 픽셀이라 " +
                    "1.000이 정상입니다. 1이 아니면 전체화면 재적합이 잘못된 목표 해상도를 계산해 " +
                    "백버퍼와 창 크기를 어긋나게 만들 수 있습니다(위 RESAMPLE 줄과 함께 보세요).");
            }

            // ---------- (E) 관측 대상이 <그 창>이 맞는가 — 다른 모든 스타일 판정의 전제 ----------
            if (s.OverlayHandleSource == HandleSourceNativeDiffers)
            {
                Add(lines, "HWND-MISMATCH", CompositionFault.Both,
                    "네이티브(LibUniWinC.GetWindowHandle)와 .NET(Process.MainWindowHandle)이 <서로 다른 창>을 " +
                    "가리킵니다. 아래 스타일/알파 판정은 이제 네이티브가 지목한 창(= 라이브러리가 실제로 " +
                    "투명화·클릭관통을 건 창)의 것이지만, 좌표 원점 보고와 전체화면 판정 등 <다른 코드가 " +
                    "여전히 .NET 핸들을 쓰고 있습니다>. 두 창이 갈린 채로는 어떤 창 진단도 신뢰할 수 없으니 " +
                    "이 줄이 보이면 핸들 단일화가 최우선입니다.");
            }
            else if (s.OverlayHandleSource == HandleSourceManagedFallback)
            {
                Add(lines, "HWND", CompositionFault.None,
                    "LibUniWinC.GetWindowHandle을 쓸 수 없어 .NET MainWindowHandle을 쟀습니다 — " +
                    "이 관측이 라이브러리가 조작한 창과 같다는 보장은 없습니다(예전과 동일한 상태).");
            }

            // ---------- (F) 합성 경로 이중화 — "겹침"의 후보 ----------
            if (s.OsStyleReadOk)
            {
                bool resolverActive = s.HybridResolverState == (int)LayeredHybridResolverState.Verified;

                if (s.HasLayeredStyle && s.TransparentType == TransparentTypeAlpha)
                {
                    if (resolverActive)
                    {
                        // 해소기가 정상 가동 중이면 레이어드는 <다음 틱(0.25초)에 사라질 일시 상태>다.
                        // 라이브러리는 커서가 캐릭터를 벗어날 때마다 다시 켜므로 이 순간 포착은 정상이다.
                        Add(lines, "LAYERED+DWM", CompositionFault.None,
                            $"WS_EX_LAYERED가 관측됐지만 하이브리드 해소기가 가동 중입니다(제거 {s.HybridStripCount}회, " +
                            "관통 유지 실측 확인됨) — 라이브러리가 커서 이동마다 다시 켜는 것을 0.25초 안에 " +
                            "떼어내므로 <일시 상태>입니다.");
                    }
                    else
                    {
                        Add(lines, "LAYERED+DWM", CompositionFault.Both,
                            "창에 WS_EX_LAYERED가 걸려 있는데 투명화는 DWM 확장 프레임(Alpha) 경로입니다 — " +
                            "합성 경로가 둘로 갈린 하이브리드 상태입니다. 이 스타일은 우리가 직접 걸지 않았고 " +
                            "네이티브 SetClickThrough(TRUE)가 WS_EX_TRANSPARENT와 함께 켭니다(원칙 2: 클릭 관통 " +
                            "기본 ON). 그리고 <한 번 켜지면 다시 꺼지지 않습니다>(disable 분기가 " +
                            "WS_EX_TRANSPARENT만 지웁니다). macOS의 클릭 관통(ignoresMouseEvents)은 합성 경로를 " +
                            "전혀 건드리지 않으므로 <이 상태는 Windows에만 존재합니다>. " +
                            $"해소기 상태={(LayeredHybridResolverState)s.HybridResolverState} — " +
                            "왜 해소되지 않았는지는 [레이어드해소] 줄에 사유가 있습니다.");
                    }

                    AppendLayeredAttributeVerdict(lines, s);
                }
                else if (!s.HasLayeredStyle)
                {
                    Add(lines, "LAYERED+DWM", CompositionFault.None,
                        "WS_EX_LAYERED 없음 — DWM 확장 프레임 단일 경로(정상)." +
                        (s.HybridStripCount > 0
                            ? $" 하이브리드 해소기가 {s.HybridStripCount}회 떼어낸 결과입니다."
                            : string.Empty));
                }

                if (!s.HasClickThroughStyle)
                {
                    Add(lines, "CLICKTHROUGH", CompositionFault.None,
                        "WS_EX_TRANSPARENT 없음 — 클릭 관통이 OS 수준에서 꺼져 있습니다(원칙 2 확인 필요). " +
                        "커서가 캐릭터 실루엣 위에 있으면 라이브러리가 의도적으로 잠시 끄므로 이 줄 하나로 " +
                        "회귀를 단정하지 마세요.");
                }
            }
            else
            {
                Add(lines, "OS-STYLE", CompositionFault.None,
                    "창 스타일 실측에 실패했습니다(핸들 미확보). 위 LAYERED 판정은 보류입니다.");
            }

            // ---------- (G) 샘플링 ----------
            if (s.RequestedMsaa > 1 && s.ActualMsaa != s.RequestedMsaa)
            {
                Add(lines, "MSAA", CompositionFault.Blur,
                    $"MSAA 요청 {s.RequestedMsaa}x != 실측 {s.ActualMsaa}x — 요청이 조용히 버려졌습니다. " +
                    "가장자리 계단이 남고, 반대로 과도한 샘플 수는 투명 창에서 알파 리졸브 비용만 늘립니다.");
            }

            return lines;
        }

        /// <summary>
        /// 레이어드 <b>속성</b>(SetLayeredWindowAttributes) 판정.
        ///
        /// <para>★ 2026-09-01 (debugger) — <b>이 함수가 이번 라운드에서 고쳐진 판정 그 자체다.</b>
        /// 예전 코드는 <c>bAlpha &lt; 255</c>만 보고 "창 전체가 (1-α)만큼 비칩니다"를 단정했다. 그러나
        /// <c>bAlpha</c>는 <c>dwFlags</c>에 <c>LWA_ALPHA</c>가 있을 때만 합성에 쓰인다 — 없으면 그냥
        /// 저장돼 있지 않은 값이고 0으로 돌아오는 것이 정상이다. 실기 로그의 <c>알파=0/255</c>가 정말로
        /// 적용 중이었다면 <b>창이 완전히 투명해 아무것도 보이지 않아야 하는데</b> 사용자는 캐릭터를
        /// 보고 있었다. 즉 그 판정은 관측과 모순이었고, 팀은 그 줄을 근거로 한 라운드를 썼다.</para>
        /// </summary>
        private static void AppendLayeredAttributeVerdict(System.Collections.Generic.List<Line> lines,
            OverlayCompositionSnapshot s)
        {
            if (!s.LayeredAttributesInEffect)
            {
                Add(lines, "LAYERED-NOATTR", CompositionFault.None,
                    "GetLayeredWindowAttributes 실패 — 레이어드 스타일은 걸려 있지만 레이어드 <속성>은 " +
                    "한 번도 설정된 적이 없습니다. 네이티브 applyWindowAlphaValue()가 알파 255로 호출될 때는 " +
                    "스타일을 걸지 않고 호출만 하는데, 그 시점의 창은 아직 레이어드가 아니라 그 호출이 " +
                    "실패하기 때문입니다(그 뒤 클릭 관통이 스타일만 켭니다). " +
                    "<b>속성이 없으므로 창 단위 알파/색키는 적용되지 않습니다</b> — 즉 이 항목은 " +
                    "비침의 원인이 아닙니다. (겹침의 실제 원인 후보는 uGUI 패널 알파 쪽입니다.)");
                return;
            }

            bool flagsKnown = s.LayeredFlags >= 0;
            bool alphaApplied = flagsKnown ? (s.LayeredFlags & LwaAlpha) != 0 : s.LayeredAlphaByte >= 0;
            bool keyApplied = flagsKnown && (s.LayeredFlags & LwaColorKey) != 0;

            if (keyApplied)
            {
                Add(lines, "LAYERED-COLORKEY", CompositionFault.SeeThrough,
                    $"레이어드 색 키가 적용 중입니다(crKey=0x{s.LayeredColorKey:X6}, dwFlags=0x{s.LayeredFlags:X}). " +
                    "DWM 확장 프레임(Alpha) 경로에서는 색 키를 쓰지 않으므로 이건 남아 있으면 안 되는 값이며, " +
                    "우리 UI 색이 우연히 키 색과 같은 화소마다 <구멍>이 납니다.");
            }

            if (alphaApplied && s.LayeredAlphaByte >= 0 && s.LayeredAlphaByte < 255)
            {
                Add(lines, "LAYERED-ALPHA", CompositionFault.SeeThrough,
                    $"레이어드 알파={s.LayeredAlphaByte}/255이고 dwFlags에 LWA_ALPHA가 <실제로> 들어 있습니다" +
                    (flagsKnown ? $"(0x{s.LayeredFlags:X})" : "(dwFlags 미관측 — 구 빌드)") + " — 창 전체가 " +
                    $"{(1f - s.LayeredAlphaByte / 255f) * 100f:F0}% 균일하게 비칩니다. " +
                    "이건 uGUI 알파와 무관한 <창 단위> 반투명이라 UiChrome을 아무리 고쳐도 안 사라집니다. " +
                    "★ 자기검증: 이 판정이 맞다면 알파 0에서는 <화면에 앱이 전혀 보이지 않아야> 합니다. " +
                    "캐릭터가 보이는데 이 줄이 떴다면 판정이 아니라 <관측 대상 창>을 의심하세요(위 HWND 줄).");
            }
            else if (!keyApplied)
            {
                Add(lines, "LAYERED-INERT", CompositionFault.None,
                    $"레이어드 속성은 있으나 합성에 적용되지 않습니다(알파 바이트={s.LayeredAlphaByte}, " +
                    (flagsKnown ? $"dwFlags=0x{s.LayeredFlags:X}" : "dwFlags 미관측") +
                    ") — LWA_ALPHA도 LWA_COLORKEY도 켜져 있지 않으므로 창 단위 반투명이 <없습니다>. " +
                    "겹침의 원인이 아닙니다.");
            }
        }

        /// <summary>주어진 배율에서 <c>pt × 배율</c>이 정수가 되는 가장 가까운 pt(같은 값이면 그대로).
        /// "폰트를 몇 pt로 바꾸면 되는가"를 로그가 직접 답하게 하려고 둔다.
        ///
        /// <para>★ 2026-09-01 — 구현을 <see cref="UiGlyphScalePolicy.SnapPoints"/>에 <b>위임</b>한다.
        /// 이 로그가 권하는 pt와 실제 코드가 폰트를 스냅하는 pt는 <b>같은 규칙이어야</b> 하는데,
        /// 여기에 사본을 두면 둘이 조용히 갈라진다(로그는 14를 권하는데 코드는 12로 스냅하는 식).
        /// 판정 문구는 그대로다 — 같은 입력에 같은 답을 낸다(동률이면 위쪽, 탐색 폭 8pt).</para></summary>
        private static int NearestExactPoints(int points, float scale)
            => UiGlyphScalePolicy.SnapPoints(points, scale);

        private static void Add(System.Collections.Generic.List<Line> lines, string code,
            CompositionFault fault, string text)
            => lines.Add(new Line { Code = code, Fault = fault, Text = text });

        /// <summary>판정 줄들 중 결함으로 표시된 것만 있는지(로그 심각도 결정용).</summary>
        public static bool HasFault(System.Collections.Generic.List<Line> lines)
        {
            for (int i = 0; i < lines.Count; i++) if (lines[i].Fault != CompositionFault.None) return true;
            return false;
        }
    }
}
