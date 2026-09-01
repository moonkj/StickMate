using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-02 — "이 앱의 어떤 표면도 OS가 예약한 <b>상단</b> 띠를 덮지 않는다"는 <b>판정 규칙</b>.
    /// docs/UX_FLOW.md 41-1.
    ///
    /// ============================================================================
    /// 규칙은 두 줄이고, 두 번째 줄이 더 중요하다
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>상단</b>: 어떤 표면도 <c>상단 예약 인셋 + 화면 여백</c>보다 위로 못 간다. <b>강제.</b></item>
    ///  <item><b>하단</b>: <b>강제하지 않는다.</b> Dock은 자동 숨김이 흔하고, 이 앱은 Dock 위를
    ///        <b>의도적으로</b> 캐릭터 발판으로 쓴다(<c>Core/DockGeometry</c>). 두 띠를 같은 규칙으로
    ///        묶으면 발판 설계와 정면충돌한다. 창이 Dock을 덮는 것은 macOS의 모든 앱이 하는 표준
    ///        동작이기도 하다. (Windows 작업표시줄은 사정이 달라 <b>재검토 대상</b>이다 — 41-13.)</item>
    /// </list>
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
    ///      <b>같은 정책 / 다른 사실 조회</b>다 — 사실 조회만 <see cref="IReservedTopBarService"/>가 맡는다.
    ///  (2) <b>OS 호출이 0줄인 순수 함수라 EditMode가 잡을 수 있다.</b> 원래 자리
    ///      (<c>PopoverPanel.UpdatePlacement</c>)에서는 <c>Screen.height</c>와 실제 화면에 매달려 있어
    ///      "메뉴바를 21pt 덮는다"를 이 머신에서 재현할 방법이 없었다. 실기에서만 드러나는 회귀는
    ///      다음에도 실기에서만 드러난다.
    ///
    /// <para><b>단위 규약</b>: 이 클래스는 단위를 모른다 — 인자 다섯 개가 <b>전부 같은 단위</b>이기만
    /// 하면 된다(픽셀이든 포인트든). 섞어 넣는 것이 유일한 오용이고, 그래서 호출부는 변환을 한 줄
    /// 안에서 끝낸다.</para>
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

        /// <summary>표면의 <b>위쪽 모서리</b>가 화면 위 끝에서 얼마나 떨어져 있는가 — 진단/테스트가
        /// "메뉴바를 몇 pt 덮는가"를 <b>덧셈 없이</b> 읽게 하는 창구(테스트가 산수를 다시 하면 그 산수도
        /// 틀릴 수 있다).</summary>
        public static float TopEdgeFromScreenTop(float centerY, float sizeY, float screenHeight)
            => screenHeight - (centerY + Mathf.Max(0f, sizeY) * 0.5f);

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
