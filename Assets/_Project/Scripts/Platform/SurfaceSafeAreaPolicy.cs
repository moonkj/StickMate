using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-02 — "이 앱의 어떤 표면도 OS가 예약한 띠를 덮지 않는다"는 <b>판정 규칙</b>.
    /// docs/UX_FLOW.md 41-1. ★ 2026-09-03 <b>가로축 추가</b>.
    ///
    /// ============================================================================
    /// 규칙은 네 줄이고, 두 번째 줄이 가장 중요하다
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>상단</b>: 어떤 표면도 <c>상단 예약 인셋 + 화면 여백</c>보다 위로 못 간다. <b>강제.</b></item>
    ///  <item><b>하단</b>: <b>강제하지 않는다.</b> Dock은 자동 숨김이 흔하고, 이 앱은 Dock 위를
    ///        <b>의도적으로</b> 캐릭터 발판으로 쓴다(<c>Core/DockGeometry</c>). 두 띠를 같은 규칙으로
    ///        묶으면 발판 설계와 정면충돌한다. 창이 Dock을 덮는 것은 macOS의 모든 앱이 하는 표준
    ///        동작이기도 하다. (Windows 작업표시줄은 사정이 달라 <b>재검토 대상</b>이다 — 41-13.)</item>
    ///  <item><b>좌·우</b>: <b>둘 다 강제.</b> 세로축의 비대칭(하단 면제)이 가로축에 없는 이유는
    ///        <b>가로에는 발판이 없기 때문</b>이다 — 캐릭터가 좌/우 도킹 작업표시줄이나 좌/우 Dock
    ///        위를 걷는 설계가 존재하지 않는다. 그래서 좌·우 예약 띠는 순수한 침해 금지 구역이다.</item>
    /// </list>
    ///
    /// <para><b>★ 왜 가로축이 필요했나</b>: 상단 축을 고친 다음 날, 화면 <b>오른쪽</b>에 여백 16pt로
    /// 붙는 할일 메모 카드가 <b>우측 도킹 작업표시줄</b>(통상 48~62pt) 앞에서 그 띠를 <b>통째로</b>
    /// 덮는다는 것이 드러났다. 상단 프로브로는 원리상 못 잡는다(상단 차이가 0이라 "띠 없음"을 낸다).
    /// 사실 조회는 <see cref="IReservedScreenEdgeService"/>가 네 변을 한 번에 준다.</para>
    ///
    /// <para><b>검산</b>(이 개발 머신, macOS 15.6 / 1512×982pt / 배율 2):
    /// 팝오버 560pt, 화면 1964px, 인셋 66px, 여백 24px →
    /// <c>maxCenterY = 1964 − 66 − 24 − 560 = 1314px</c> → 패널 상단 OS y =
    /// <c>982 − (1314+560)/2 = 45pt</c> = 메뉴바 하단 33 + 여백 12. 겹침 21pt → <b>0pt</b>.</para>
    ///
    /// ============================================================================
    /// 왜 여기(플랫폼 중립 위치)에 있는가 — CLAUDE.md
    /// ============================================================================
    ///  (1) <b>정책은 플랫폼 중립 위치에.</b> 이 판정이 <c>Platform/MacOS/</c> 안에 있으면 Windows가
    ///      물리적으로 호출할 수 없다(실제 사고: <c>FullscreenSuspendPolicy.cs</c>). 상단 안전영역은
    ///      <b>같은 정책 / 다른 사실 조회</b>다 — 사실 조회는 <see cref="IReservedScreenEdgeService"/>
    ///      (네 변)와 <see cref="IReservedTopBarService"/>(그중 상단 한 값)가 맡는다.
    ///  (2) <b>OS 호출이 0줄인 순수 함수라 EditMode가 잡을 수 있다.</b> 원래 자리
    ///      (<c>PopoverPanel.UpdatePlacement</c>)에서는 <c>Screen.height</c>와 실제 화면에 매달려 있어
    ///      "메뉴바를 21pt 덮는다"를 이 머신에서 재현할 방법이 없었다. 실기에서만 드러나는 회귀는
    ///      다음에도 실기에서만 드러난다.
    ///
    /// <para><b>단위 규약</b>: 이 클래스는 단위를 모른다 — <b>한 호출에 들어가는 인자가 전부 같은
    /// 단위</b>이기만 하면 된다(픽셀이든 포인트든). 섞어 넣는 것이 유일한 오용이고, 그래서 호출부는
    /// 변환을 한 줄 안에서 끝낸다.</para>
    /// </summary>
    public static class SurfaceSafeAreaPolicy
    {
        /// <summary>
        /// <b>y가 위로 자라는</b> 좌표계(Unity 스크린 픽셀, 원점 좌하단)에서 표면 중심의 세로 위치를 자른다.
        ///
        /// <para>상한(위쪽)에만 <paramref name="topInset"/>이 들어간다. 하한(아래쪽)은 예전 그대로
        /// <c>여백 + 반높이</c>다 — 규칙 2.</para>
        ///
        /// <para><b>표면이 안전 영역보다 클 때</b>는 <b>상단을 고정하고 아래로 넘치게</b> 둔다.
        /// "가운데로 맞춘다"를 고르면 위아래로 반씩 잘려 <b>메뉴바를 다시 덮는다</b> — 이 규칙이
        /// 존재하는 이유 자체를 무효로 만드는 선택이다.</para>
        /// </summary>
        public static float ClampCenterY(float desiredCenterY, float sizeY, float screenHeight,
            float topInset, float margin)
        {
            if (!IsFinite(desiredCenterY) || !IsFinite(sizeY) || !IsFinite(screenHeight)
                || !IsFinite(topInset) || !IsFinite(margin)) return desiredCenterY;
            if (screenHeight <= 0f) return desiredCenterY;

            float half = Mathf.Max(0f, sizeY) * 0.5f;
            float m = Mathf.Max(0f, margin);
            float inset = Mathf.Max(0f, topInset);

            float maxCenterY = screenHeight - inset - m - half;   // 위쪽 한계(예약 띠 아래).
            float minCenterY = m + half;                          // 아래쪽 한계(예약 띠 미적용).

            if (maxCenterY <= minCenterY) return maxCenterY;      // 상단 우선.
            return Mathf.Clamp(desiredCenterY, minCenterY, maxCenterY);
        }

        /// <summary>
        /// <b>y가 아래로 자라는</b> 좌표계(창 좌상단 원점 — 톱니 아이콘이 자기 자리를 저장하는 계)용 어댑터.
        /// 같은 규칙을 <see cref="ClampCenterY"/> <b>하나</b>에서 가져온다(두 벌이 되면 반드시 한쪽만 고쳐진다).
        /// </summary>
        public static float ClampTopDownCenterY(float desiredCenterY, float sizeY, float screenHeight,
            float topInset, float margin)
        {
            if (!IsFinite(desiredCenterY) || !IsFinite(screenHeight) || screenHeight <= 0f) return desiredCenterY;
            float flipped = ClampCenterY(screenHeight - desiredCenterY, sizeY, screenHeight, topInset, margin);
            return screenHeight - flipped;
        }

        /// <summary>
        /// <b>화면 중앙이 원점</b>인 좌표계(uGUI <c>anchoredPosition</c> — 정보창이 드래그 자리를 담는 계)용
        /// 어댑터. 반환값은 "중앙에서 얼마나 떨어져도 되는가"이며, 위쪽만 예약 띠만큼 좁아진다.
        /// </summary>
        public static float ClampCenterOriginOffsetY(float desiredOffsetY, float sizeY, float screenHeight,
            float topInset, float margin)
        {
            if (!IsFinite(desiredOffsetY) || !IsFinite(screenHeight) || screenHeight <= 0f) return desiredOffsetY;
            float centerY = screenHeight * 0.5f + desiredOffsetY;
            return ClampCenterY(centerY, sizeY, screenHeight, topInset, margin) - screenHeight * 0.5f;
        }

        /// <summary>
        /// ★ 2026-09-03 — <b>가로축</b> 판정. <c>x</c>가 오른쪽으로 자라는 좌표계(Unity 스크린 픽셀)에서
        /// 표면 중심의 가로 위치를 자른다. 세로와 달리 <b>양쪽 다 강제</b>한다(클래스 문서 규칙 3).
        ///
        /// <para><b>표면이 안전 영역보다 넓을 때</b>는 <b>예약 띠가 얇은 쪽으로 넘치게</b> 둔다.
        /// 세로축이 "상단 고정 + 아래로 넘침"인 것과 형태는 같지만 근거가 다르다 — 가로에는
        /// 특권을 가진 변이 없으므로 <b>실제로 덜 침해하는 쪽</b>을 계산해서 고른다.
        /// 예: 우측 도킹 48pt / 좌측 0pt인 화면에서 넘칠 수밖에 없다면 <b>왼쪽으로</b> 넘긴다
        /// (거기엔 덮을 OS UI가 없다). 가운데 정렬을 고르면 <b>양쪽 띠를 반씩 덮어</b> 이 규칙이
        /// 존재하는 이유 자체를 무효로 만든다.</para>
        /// </summary>
        public static float ClampCenterX(float desiredCenterX, float sizeX, float screenWidth,
            float leftInset, float rightInset, float margin)
        {
            if (!IsFinite(desiredCenterX) || !IsFinite(sizeX) || !IsFinite(screenWidth)
                || !IsFinite(leftInset) || !IsFinite(rightInset) || !IsFinite(margin)) return desiredCenterX;
            if (screenWidth <= 0f) return desiredCenterX;

            float half = Mathf.Max(0f, sizeX) * 0.5f;
            float m = Mathf.Max(0f, margin);
            float left = Mathf.Max(0f, leftInset);
            float right = Mathf.Max(0f, rightInset);

            float minCenterX = left + m + half;                    // 왼쪽 한계(좌 예약 띠 바깥).
            float maxCenterX = screenWidth - right - m - half;     // 오른쪽 한계(우 예약 띠 바깥).

            if (maxCenterX <= minCenterX)
            {
                // 들어갈 자리가 없다 -> 얇은 띠 쪽으로 넘긴다.
                //   오른쪽이 더 얇다 -> 왼쪽 한계에 붙여 오른쪽으로 넘친다.
                //   그 밖(왼쪽이 더 얇거나 같다) -> 오른쪽 한계에 붙여 왼쪽으로 넘친다.
                return right < left ? minCenterX : maxCenterX;
            }
            return Mathf.Clamp(desiredCenterX, minCenterX, maxCenterX);
        }

        /// <summary>
        /// <b>화면 중앙이 원점</b>인 좌표계(uGUI <c>anchoredPosition</c>)용 가로 어댑터.
        /// 같은 규칙을 <see cref="ClampCenterX"/> <b>하나</b>에서 가져온다.
        /// </summary>
        public static float ClampCenterOriginOffsetX(float desiredOffsetX, float sizeX, float screenWidth,
            float leftInset, float rightInset, float margin)
        {
            if (!IsFinite(desiredOffsetX) || !IsFinite(screenWidth) || screenWidth <= 0f) return desiredOffsetX;
            float centerX = screenWidth * 0.5f + desiredOffsetX;
            return ClampCenterX(centerX, sizeX, screenWidth, leftInset, rightInset, margin) - screenWidth * 0.5f;
        }

        /// <summary>
        /// <b>화면 오른쪽에 붙는 표면</b>(할일 메모 카드 등)이 실제로 가져야 하는 인셋 —
        /// 화면 오른쪽 끝에서 표면의 <b>우변</b>까지의 거리. 호출부가 대수(代數)를 다시 풀지 않게 하는 창구다
        /// (다시 풀면 그 산수도 틀릴 수 있다 — <see cref="TopEdgeFromScreenTop"/>이 생긴 이유와 같다).
        ///
        /// <para><b>예약 띠가 없으면 <paramref name="desiredInset"/>이 그대로 나온다</b> — 즉 이 함수를
        /// 끼워 넣어도 띠가 0인 환경에서는 한 픽셀도 바뀌지 않는다. 그게 회귀 없음 보증이다.</para>
        /// </summary>
        public static float ClampRightAnchoredInset(float desiredInset, float sizeX, float screenWidth,
            float leftInset, float rightInset, float margin)
        {
            if (!IsFinite(desiredInset) || !IsFinite(sizeX) || !IsFinite(screenWidth)
                || screenWidth <= 0f) return desiredInset;

            float half = Mathf.Max(0f, sizeX) * 0.5f;
            float centerX = ClampCenterX(screenWidth - desiredInset - half, sizeX, screenWidth,
                leftInset, rightInset, margin);
            return RightEdgeFromScreenRight(centerX, sizeX, screenWidth);
        }

        /// <summary>표면의 <b>위쪽 모서리</b>가 화면 위 끝에서 얼마나 떨어져 있는가 — 진단/테스트가
        /// "메뉴바를 몇 pt 덮는가"를 <b>덧셈 없이</b> 읽게 하는 창구(테스트가 산수를 다시 하면 그 산수도
        /// 틀릴 수 있다).</summary>
        public static float TopEdgeFromScreenTop(float centerY, float sizeY, float screenHeight)
            => FarEdgeGap(centerY, sizeY, screenHeight);

        /// <summary>표면의 <b>오른쪽 모서리</b>가 화면 오른쪽 끝에서 얼마나 떨어져 있는가 —
        /// <see cref="TopEdgeFromScreenTop"/>의 가로판이고 <b>같은 산술 한 벌</b>을 쓴다.</summary>
        public static float RightEdgeFromScreenRight(float centerX, float sizeX, float screenWidth)
            => FarEdgeGap(centerX, sizeX, screenWidth);

        /// <summary>좌표가 커지는 쪽 끝에서 표면의 그쪽 모서리까지의 거리. 세로/가로가 공유하는 한 줄.</summary>
        private static float FarEdgeGap(float center, float size, float screenExtent)
            => screenExtent - (center + Mathf.Max(0f, size) * 0.5f);

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
