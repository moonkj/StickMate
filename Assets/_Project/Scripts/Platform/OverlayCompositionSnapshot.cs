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
    /// (4)는 우리가 <b>의도적으로</b> 만든 상태다: 원칙 2의 클릭 관통(ON 기본)이 네이티브
    /// <c>SetClickThrough(TRUE)</c>에서 <c>WS_EX_TRANSPARENT | WS_EX_LAYERED</c>를 함께 켠다.
    /// macOS의 클릭 관통(<c>ignoresMouseEvents</c>)은 합성 경로를 <b>건드리지 않는다</b> —
    /// 두 플랫폼이 갈리는 지점이 바로 여기다.
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
            sb.Append(LayeredAlphaByte).Append('|');
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
            if (s.CanvasScaleFactor > 0f && s.SampleFontSizePoints > 0)
            {
                float requestedPixels = s.SampleFontSizePoints * s.CanvasScaleFactor;
                int atlasPixels = Mathf.Max(1, Mathf.RoundToInt(requestedPixels));
                float glyphRatio = requestedPixels / atlasPixels;
                bool integerScale = Mathf.Abs(s.CanvasScaleFactor - Mathf.Round(s.CanvasScaleFactor)) <= RatioEpsilon;
                if (!integerScale || Mathf.Abs(glyphRatio - 1f) > RatioEpsilon)
                {
                    Add(lines, "GLYPH-SCALE", CompositionFault.Blur,
                        $"캔버스 배율={s.CanvasScaleFactor:F3}(정수 아님: {!integerScale}). 대표 폰트 " +
                        $"{s.SampleFontSizePoints}pt는 아틀라스에 {atlasPixels}px로 구워진 뒤 " +
                        $"{glyphRatio:F4}배로 <비정수 확대>되어 화면에 올라갑니다 — 레거시 uGUI Text의 " +
                        "글자가 흐려지는 구조적 원인입니다(알파 문제가 아닙니다). " +
                        "디스플레이 배율 125%/150%에서 특히 두드러집니다.");
                }
                else
                {
                    Add(lines, "GLYPH-SCALE", CompositionFault.None,
                        $"캔버스 배율={s.CanvasScaleFactor:F3}(정수) — 글리프 리샘플 없음.");
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

            // ---------- (E) 합성 경로 이중화 — "겹침"의 유력 후보 ----------
            if (s.OsStyleReadOk)
            {
                if (s.HasLayeredStyle && s.TransparentType == TransparentTypeAlpha)
                {
                    Add(lines, "LAYERED+DWM", CompositionFault.Both,
                        "창에 WS_EX_LAYERED가 걸려 있는데 투명화는 DWM 확장 프레임(Alpha) 경로입니다 — " +
                        "합성 경로가 둘로 갈린 하이브리드 상태입니다. 이 스타일은 우리가 직접 걸지 않았고 " +
                        "네이티브 SetClickThrough(TRUE)가 WS_EX_TRANSPARENT와 함께 켭니다(원칙 2: 클릭 관통 " +
                        "기본 ON). 그리고 <한 번 켜지면 다시 꺼지지 않습니다>(disable 분기가 " +
                        "WS_EX_TRANSPARENT만 지웁니다). macOS의 클릭 관통(ignoresMouseEvents)은 합성 경로를 " +
                        "전혀 건드리지 않으므로 <이 상태는 Windows에만 존재합니다>.");

                    if (!s.LayeredAttributesInEffect)
                    {
                        Add(lines, "LAYERED-NOATTR", CompositionFault.SeeThrough,
                            "게다가 GetLayeredWindowAttributes가 실패했습니다 — 레이어드 창인데 " +
                            "SetLayeredWindowAttributes/UpdateLayeredWindow가 <한 번도 성립하지 않은> 상태입니다. " +
                            "네이티브 applyWindowAlphaValue()는 알파가 255면 스타일을 걸지 않고 호출만 하는데, " +
                            "그 호출은 창이 아직 레이어드가 아니라 실패하고, 나중에 클릭 관통이 " +
                            "레이어드만 켭니다. 이 조합의 합성 결과는 OS/드라이버 정의에 맡겨져 있습니다.");
                    }
                    else if (s.LayeredAlphaByte >= 0 && s.LayeredAlphaByte < 255)
                    {
                        Add(lines, "LAYERED-ALPHA", CompositionFault.SeeThrough,
                            $"레이어드 알파={s.LayeredAlphaByte}/255 — 창 전체가 " +
                            $"{(1f - s.LayeredAlphaByte / 255f) * 100f:F0}% 균일하게 비칩니다. " +
                            "이건 uGUI 알파와 무관한 <창 단위> 반투명이라 UiChrome을 아무리 고쳐도 안 사라집니다.");
                    }
                }
                else if (!s.HasLayeredStyle)
                {
                    Add(lines, "LAYERED+DWM", CompositionFault.None,
                        "WS_EX_LAYERED 없음 — DWM 확장 프레임 단일 경로(정상).");
                }

                if (!s.HasClickThroughStyle)
                {
                    Add(lines, "CLICKTHROUGH", CompositionFault.None,
                        "WS_EX_TRANSPARENT 없음 — 클릭 관통이 OS 수준에서 꺼져 있습니다(원칙 2 확인 필요).");
                }
            }
            else
            {
                Add(lines, "OS-STYLE", CompositionFault.None,
                    "창 스타일 실측에 실패했습니다(핸들 미확보). 위 LAYERED 판정은 보류입니다.");
            }

            // ---------- (F) 샘플링 ----------
            if (s.RequestedMsaa > 1 && s.ActualMsaa != s.RequestedMsaa)
            {
                Add(lines, "MSAA", CompositionFault.Blur,
                    $"MSAA 요청 {s.RequestedMsaa}x != 실측 {s.ActualMsaa}x — 요청이 조용히 버려졌습니다. " +
                    "가장자리 계단이 남고, 반대로 과도한 샘플 수는 투명 창에서 알파 리졸브 비용만 늘립니다.");
            }

            return lines;
        }

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
