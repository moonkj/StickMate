using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// OS가 돌려주는 <b>창 전체(frame) 사각형</b>에서 <b>Unity가 실제로 그리는 콘텐츠 사각형</b>을
    /// 뽑아내는 <b>순수 규칙</b>. UnityEngine의 Rect/Vector2 외에는 어떤 의존도 없고 P/Invoke도 없다 —
    /// 그래야 실기 없이 EditMode가 실행으로 검증한다(<see cref="OverlayBoundsFitPolicy"/>와 같은 설계).
    ///
    /// ============================================================================
    /// 왜 생겼는가 (2026-09-01) — 확정된 것과 확정되지 않은 것을 나눠 적는다
    /// ============================================================================
    /// <b>확정(코드+로그 대조)</b>: <c>kCGWindowBounds</c>는 <b>frame</b> 사각형이고, 창이 아직
    /// 보더리스가 아닌 동안에는 그 안에 <b>타이틀바 28pt</b>가 들어 있다. 우리 오버레이 창은
    /// <b>기동 후 부착 전까지(실측 2.3초)</b> 정확히 그 상태다. 같은 실행의 로그 두 줄이 이것을
    /// 산술로 못박는다:
    /// <code>
    ///   [MacWindowService] DetectDesktopDpiScale(): 자기 창 실측 — 창=(0, 33, 1512, 1010)
    ///   [MacOverlayStateEnforcer] 창 부착 감지 — windowSize=clientSize=(1512, 982), windowPosition=(0, -61)
    /// </code>
    /// <list type="bullet">
    ///   <item><c>1010 - 982 = 28</c> = macOS 타이틀바 높이.</item>
    ///   <item><c>982 - (-61) - 1010 = 33</c> = 메뉴바 높이. AppKit의 <c>constrainFrameRect:toScreen:</c>가
    ///         <b>타이틀바만</b> 메뉴바 아래로 밀어 넣은 결과다.</item>
    ///   <item>그러므로 이 상태의 <b>콘텐츠</b> 사각형은 <c>(0, 61, 1512, 982)</c>다 — 이전 라운드가
    ///         별도로 실측해 <c>MacOverlayStateEnforcer</c> 주석에 남긴 "Quartz 원점=(0,61)"과 일치한다.</item>
    /// </list>
    /// 즉 <c>33 + 1010 = 1043 &gt; 982</c>는 <b>모순이 아니다</b>. 타이틀바가 있는 창은 아래쪽이 화면
    /// 밖으로 나가도 되고(AppKit이 제약하는 것은 타이틀바뿐), 두 값은 서로 다른 좌표계가 아니라
    /// <b>frame과 content</b>다. 보정 없이 frame을 그대로 좌표계에 넣으면 원점이 28pt 위로, 높이가
    /// 28pt 크게 들어가 커서↔월드 변환과 발판 판정이 그만큼 어긋난다.
    ///
    /// <para><b>확정되지 않은 것(정직하게)</b>: 2026-09-01 실기 로그(PID 11451, 18:05~18:06)에서
    /// 오버레이 사각형이 <c>(0,0,1512,982)</c> ↔ <c>(0,33,1512,1010)</c>로 교대한 것은 <b>이 파일이
    /// 고치는 문제와 같은 뿌리라고 단정할 수 없다</b>. 그 시각에 조사용 <b>두 번째 인스턴스</b>가
    /// 떠 있었고, 그때 <c>MacWindowService.IsSelfWindow</c>의 <b>이름 기반 폴백</b>이 남의 인스턴스
    /// 창을 "내 창"으로 통과시키고 있었다(같은 라운드에서 함께 수정 —
    /// <c>IsSelfProcessWindow</c>/<c>IsOwnAppWindow</c> 분리). 인스턴스 1개일 때도 그 교대가
    /// 재현되는지는 <b>아직 확인되지 않았다</b>.</para>
    ///
    /// <para>그래도 이 규칙이 필요한 이유는 교대와 무관하다: <b>우리 자신의 기동 2.3초 구간</b>이
    /// 실측으로 확인된 오보고 구간이기 때문이다.</para>
    ///
    /// <para><b>Windows는 이미 이 부류를 고쳐 두었다</b>: <c>Win32WindowService.CaptureOverlayOrigin</c>은
    /// 원시 <c>GetWindowRect</c>가 아니라 <c>TryGetVisualWindowRect</c>(DWM 확장 프레임)를 쓰며,
    /// 그 주석이 "보더리스가 아직 적용되지 않은 기동 직후 몇 프레임에는 GetWindowRect가 보이지 않는
    /// 테두리를 포함해 원점을 좌상단으로 밀고 AutoDpiScale까지 부풀린다"고 <b>정확히 같은 인과</b>를
    /// 적고 있다. macOS만 그 대응물이 없었다 — 판정은 플랫폼 중립 위치에 둔다
    /// (CLAUDE.md: 정책은 <c>Platform/</c>, 플랫폼 코드는 "사실 조회"만).</para>
    ///
    /// ============================================================================
    /// 왜 불감대로 덮지 않는가
    /// ============================================================================
    /// 어긋남이 1px 상수 오차가 아니라 <b>28pt(타이틀바) / 33pt(메뉴바)</b>다.
    /// <see cref="OverlayBoundsFitPolicy.DefaultEpsilonPixels"/>를 그만큼 넓히면 사람이 인지하는
    /// 어긋남까지 전부 삼킨다(그 파일 자신이 "늘리지 말 것"이라고 경고한다). 여기서는 <b>덮는 대신
    /// 참값을 계산</b>한다 — 콘텐츠 크기는 창 라이브러리가 이미 정확히 알려주고 있었다.
    /// </summary>
    public static class OverlayContentRectPolicy
    {
        /// <summary>
        /// 창 장식(타이틀바)으로 인정하는 최대 두께(OS 포인트). macOS 표준 타이틀바는 28pt이고
        /// 툴바가 붙어도 60pt 안쪽이다. 이보다 크면 "장식"이 아니라 <b>측정이 깨진 것</b>이므로
        /// 보정하지 않고 원본을 그대로 쓴다 — 깨진 값으로 좌표계를 옮기는 것이 가장 나쁘다.
        /// </summary>
        public const float MaxTopDecorationPoints = 64f;

        /// <summary>
        /// 폭/높이 비교 불감대(OS 포인트). 값과 근거는 <see cref="OverlayBoundsFitPolicy"/> 한 곳에 있다 —
        /// 이 파일이 자기 숫자를 따로 들면 두 벌로 갈라진다.
        /// </summary>
        public const float DefaultEpsilonPoints = OverlayBoundsFitPolicy.DefaultEpsilonPixels;

        /// <summary>
        /// frame 사각형 + "우리가 아는 콘텐츠 크기"로부터 콘텐츠 사각형을 유도한다.
        ///
        /// <para>좌표계 전제: <paramref name="frameRect"/>는 <b>좌상단 원점, y 아래로 증가</b>
        /// (macOS Quartz 전역 좌표 / Windows 데스크톱 좌표 — 둘 다 같은 규약이다). 그래서 위쪽 장식은
        /// <c>y</c>를 <b>키우는</b> 방향으로 걷어낸다.</para>
        ///
        /// <para>보정하지 <b>않는</b> 경우(전부 "모르면 건드리지 않는다"): 콘텐츠 크기를 모를 때
        /// (부착 전에는 (0,0)), 폭이 다를 때(좌우 장식이 있는 형상은 이 규칙의 전제 밖),
        /// 높이 차이가 음수이거나 <see cref="MaxTopDecorationPoints"/>를 넘을 때,
        /// 차이가 불감대 안일 때(= 이미 보더리스, 흔한 정상 경로).</para>
        /// </summary>
        /// <param name="frameRect">OS가 보고한 창 전체 사각형(포인트, 좌상단 원점).</param>
        /// <param name="knownContentSize">창 라이브러리가 보고한 콘텐츠(클라이언트) 크기(포인트).
        /// 모르면 <c>Vector2.zero</c>.</param>
        /// <param name="epsilonPoints">같다고 볼 오차(포인트).</param>
        /// <param name="contentRect">유도된 콘텐츠 사각형. 보정하지 않으면 <paramref name="frameRect"/> 그대로.</param>
        /// <param name="strippedTopPoints">걷어낸 위쪽 장식 두께(포인트). 보정하지 않았으면 0.</param>
        /// <returns>실제로 보정했으면 true.</returns>
        public static bool TryStripTopDecoration(Rect frameRect, Vector2 knownContentSize,
            float epsilonPoints, out Rect contentRect, out float strippedTopPoints)
        {
            contentRect = frameRect;
            strippedTopPoints = 0f;

            if (knownContentSize.x <= 0f || knownContentSize.y <= 0f) return false;   // 부착 전 — 모른다.
            if (frameRect.width <= 0f || frameRect.height <= 0f) return false;
            if (float.IsNaN(knownContentSize.x) || float.IsNaN(knownContentSize.y)) return false;
            if (float.IsInfinity(knownContentSize.x) || float.IsInfinity(knownContentSize.y)) return false;

            // 좌우 장식이 있는 형상은 "위쪽만 걷어낸다"는 이 규칙의 전제 밖이다 — 조용히 포기한다.
            if (Mathf.Abs(frameRect.width - knownContentSize.x) > epsilonPoints) return false;

            float delta = frameRect.height - knownContentSize.y;
            if (delta <= epsilonPoints) return false;                 // 이미 보더리스(정상 경로).
            if (delta > MaxTopDecorationPoints) return false;         // 장식이라기엔 너무 두껍다 = 측정 파손.

            contentRect = new Rect(frameRect.x, frameRect.y + delta, knownContentSize.x, knownContentSize.y);
            strippedTopPoints = delta;
            return true;
        }

        /// <summary>
        /// 창 라이브러리가 아직 콘텐츠 크기를 모를 때(부착 전 몇 초) 쓰는 <b>백버퍼 기반 유도</b>.
        ///
        /// <para>항등식: <c>Screen.width/height</c>는 정의상 <b>콘텐츠 뷰의 픽셀 크기</b>다. 그리고
        /// 위쪽 장식만 있는 창에서는 <c>frame.width == content.width</c>이므로
        /// <c>포인트/픽셀 = frame.width / Screen.width</c>가 성립한다. 두 식을 곱하면 콘텐츠의
        /// <b>포인트</b> 크기가 나온다. 실측 대입: <c>1964 x (1512 / 3024) = 982</c> — 실기 로그의
        /// clientSize와 정확히 같다.</para>
        ///
        /// <para>이 유도가 없으면 기동 직후 몇 초(부착 전)에는 frame을 그대로 쓸 수밖에 없어
        /// 원점이 28pt 틀린 채로 첫 발판 판정이 돌아간다.</para>
        /// </summary>
        /// <returns>유도에 성공하면 true. 입력이 이상하면 false이고 <paramref name="contentSize"/>는 zero.</returns>
        public static bool TryDeriveContentSizeFromBackbuffer(Rect frameRect,
            int backbufferPixelWidth, int backbufferPixelHeight, out Vector2 contentSize)
        {
            contentSize = Vector2.zero;
            if (backbufferPixelWidth <= 0 || backbufferPixelHeight <= 0) return false;
            if (frameRect.width <= 0f || frameRect.height <= 0f) return false;

            float pointsPerPixel = frameRect.width / backbufferPixelWidth;
            if (pointsPerPixel <= 0f || float.IsNaN(pointsPerPixel) || float.IsInfinity(pointsPerPixel))
            {
                return false;
            }

            contentSize = new Vector2(frameRect.width, backbufferPixelHeight * pointsPerPixel);
            return true;
        }
    }
}
