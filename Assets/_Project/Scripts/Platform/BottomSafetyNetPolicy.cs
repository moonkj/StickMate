using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-01 — 바닥 안전망 조각의 <b>판정 규칙</b>. 사용자 신고 <b>"캐릭터가 없어진다"</b>의
    /// 후보 경로 하나를 여기서 끊는다.
    ///
    /// ============================================================================
    /// 무엇이 잘못됐었나 — 좌표 출처가 둘로 갈려 있었다
    /// ============================================================================
    /// <see cref="FallbackPlatformWindowService.AppendBottomSafetyNet"/>는 안전망을
    /// <b>오버레이 창</b>의 기하(<c>ScreenCoordinateConverter.OverlayOriginOsScreen</c> +
    /// <c>Screen.width/height</c>)에서 만들고, 하단 예약 막대(작업표시줄)는
    /// <see cref="IReservedBottomBarService"/>(Win32 <c>GetMonitorInfo</c>)에서 온다. 두 값이
    /// 일치한다는 보장이 <b>어디에도 없다</b> — 실제로 어긋났다.
    ///
    /// <para>Windows 실측(2026-09-01):
    /// 안전망 원점 <c>(3851, 45)</c> / 오버레이 3831x2160 대 모니터 <c>x[3840..7680]</c>,
    /// 하단 2160(작업표시줄 상단 2088). 결과:
    ///  <list type="bullet">
    ///   <item>오른쪽 끝이 모니터 밖으로 <c>7682 − 7680 = 2pt</c> 삐져나가고, 이 조각이
    ///         <c>MinPieceWidthOsPoints</c>(1pt)보다 넓어 <b>발판으로 살아남는다</b>.</item>
    ///   <item>그 조각의 상단 OS y ≈ <c>2199</c> — 모니터 하단 2160보다 <b>39px 아래</b>,
    ///         즉 화면 밖이면서 작업표시줄 뒤다.</item>
    ///  </list>
    /// 여기 착지하면 캐릭터가 <b>화면에서 사라진다</b>. 폭 2pt짜리라 발이 걸릴 확률은 낮지만, 낙하
    /// 경로 끝이 하필 화면 오른쪽 끝이면 그대로 빨려 들어간다.</para>
    ///
    /// ============================================================================
    /// 어떻게 고치는가 — 출처를 하나로 모은다
    /// ============================================================================
    /// OS가 하단 예약 막대의 사각형을 알려주면 그 사각형은 <b>모니터의 좌/우/하단을 그대로 담고
    /// 있다</b>. Win32 구현이 <c>new Rect(rcMonitor.Left, rcWork.Bottom, rcMonitor.Right −
    /// rcMonitor.Left, rcMonitor.Bottom − rcWork.Bottom)</c>로 만들기 때문이다. 따라서
    ///   <c>xMin = rcMonitor.Left</c>, <c>xMax = rcMonitor.Right</c>, <c>yMax = rcMonitor.Bottom</c>.
    /// 이 셋을 <b>권위 있는 화면 경계</b>로 받아 안전망을 그 안으로 접는다(가로는 잘라내고, 세로는
    /// 두께를 유지한 채 위로 밀어 올린다 — 잘라 얇게 만들면 접지 판정이 채터링한다).
    ///
    /// <para><b>모니터 상단(rcMonitor.Top)은 일부러 받지 않는다.</b> 막대 사각형만으로는 알 수 없는
    /// 값이라, 안다고 치고 인자를 만들면 호출부가 어딘가에서 지어내게 된다. 아는 것만 받는다.</para>
    ///
    /// ============================================================================
    /// 왜 별도 파일인가 (CLAUDE.md 플랫폼 중립 규정)
    /// ============================================================================
    ///  (1) <b>정책은 플랫폼 중립 위치에</b>. 이 판정이 <c>Platform/Windows/</c> 안에 있으면 macOS가
    ///      물리적으로 호출할 수 없다(실제 사고: <c>FullscreenSuspendPolicy.cs</c>).
    ///  (2) <b>순수 함수라 테스트가 잡을 수 있다.</b> <c>Screen.width</c>·창 원점·OS 조회에 걸려 있는
    ///      원래 자리에서는 "모니터 밖 2pt 조각"을 EditMode에서 재현할 방법이 없었다. 실기에서만
    ///      드러나는 회귀는 다음에도 실기에서만 드러난다.
    ///
    /// <para>화면 경계를 <b>모르면</b>(<paramref name="hasScreenBounds"/>=false) 예전 계산과
    /// <b>한 글자도 다르지 않다</b> — macOS는 <see cref="IReservedBottomBarService"/>를 구현하지
    /// 않으므로 언제나 이 경로다(회귀 없음).</para>
    /// </summary>
    public static class BottomSafetyNetPolicy
    {
        /// <summary>
        /// 안전망 조각을 발판으로 인정하는 최소 폭(OS 포인트). 이보다 얇은 조각은 캐릭터가 설 수 없는
        /// 실오라기라 오히려 접지/낙하가 매 프레임 뒤집히는 채터링만 만든다.
        /// <para>이 값을 <b>올려서</b> 위 2pt 사고를 막으려는 유혹이 있는데, 그건 증상 가리기다 —
        /// 어긋난 좌표계는 그대로 남고 다음엔 3pt로 돌아온다. 근본 수정은 화면 경계 접기 쪽이다.</para>
        /// </summary>
        public const float MinPieceWidthOsPoints = 1f;

        /// <summary>안전망 두 조각의 계산 결과. 폭이 최소치 이하이거나 화면 밖이면 <c>Has*</c>가 false다.</summary>
        public readonly struct Pieces
        {
            public readonly bool HasLeft;
            public readonly bool HasRight;
            public readonly Rect Left;
            public readonly Rect Right;

            public Pieces(bool hasLeft, Rect left, bool hasRight, Rect right)
            {
                HasLeft = hasLeft;
                Left = left;
                HasRight = hasRight;
                Right = right;
            }
        }

        /// <summary>
        /// 오버레이 창에서 뽑은 안전망 사각형을 <b>실제 화면 경계</b> 안으로 접고, Dock/작업표시줄
        /// 가로 구간을 구멍으로 잘라내 좌/우 두 조각을 만든다.
        /// </summary>
        /// <param name="overlayNetRect">오버레이 창 기준 안전망 사각형(OS 좌표, 좌상단 원점·y 하향 증가).
        /// 높이가 곧 발판 두께다.</param>
        /// <param name="hasScreenBounds">OS가 알려준 권위 있는 화면 경계가 있는가.
        /// false면 접지 않는다(= 2026-09-01 이전 동작 그대로).</param>
        /// <param name="screenLeftOsX">모니터 왼쪽 끝(rcMonitor.Left).</param>
        /// <param name="screenRightOsX">모니터 오른쪽 끝(rcMonitor.Right).</param>
        /// <param name="screenBottomOsY">모니터 아래쪽 끝(rcMonitor.Bottom). 안전망 바닥이 이보다
        /// 아래로 내려가면 캐릭터가 화면 밖에 선다.</param>
        /// <param name="hasDock">Dock/작업표시줄 발판이 있는가(있으면 그 가로 구간에 구멍을 뚫는다).</param>
        /// <param name="dockLeftOsX">구멍 왼쪽 끝.</param>
        /// <param name="dockRightOsX">구멍 오른쪽 끝.</param>
        public static Pieces Resolve(
            Rect overlayNetRect,
            bool hasScreenBounds, float screenLeftOsX, float screenRightOsX, float screenBottomOsY,
            bool hasDock, float dockLeftOsX, float dockRightOsX)
        {
            float thickness = Mathf.Max(0f, overlayNetRect.height);
            float netLeftOsX = overlayNetRect.xMin;
            float netRightOsX = overlayNetRect.xMax;
            float netTopOsY = overlayNetRect.yMin;

            if (hasScreenBounds)
            {
                // 가로: 화면 밖으로 삐져나간 부분을 잘라낸다. 화면 경계가 뒤집혀 들어와도(병리적 입력)
                // 조각 폭이 음수가 되지 않도록 min/max로 정규화해서 쓴다.
                float boundLeft = Mathf.Min(screenLeftOsX, screenRightOsX);
                float boundRight = Mathf.Max(screenLeftOsX, screenRightOsX);
                netLeftOsX = Mathf.Clamp(netLeftOsX, boundLeft, boundRight);
                netRightOsX = Mathf.Clamp(netRightOsX, boundLeft, boundRight);

                // 세로: 두께를 유지한 채 위로 민다. 잘라서 얇게 만들면 발판 두께가 화면마다 달라져
                // 접지 판정이 흔들린다(두께는 캐릭터 발끝 보정과 묶인 값이다).
                float netBottomOsY = Mathf.Min(netTopOsY + thickness, screenBottomOsY);
                netTopOsY = netBottomOsY - thickness;
            }

            // Dock이 없으면 구멍의 좌우 끝을 둘 다 안전망 오른쪽 끝에 두어, 왼쪽 조각이 전체 폭을
            // 차지하고 오른쪽 조각이 폭 0이 되게 한다(= 구멍 개념이 없던 시절과 완전히 동일).
            // Clamp는 Dock이 안전망보다 넓거나 밖으로 벗어난 병리적 설정에서도 조각 폭이 음수가
            // 되지 않게 한다.
            float holeLeftOsX = hasDock ? Mathf.Clamp(dockLeftOsX, netLeftOsX, netRightOsX) : netRightOsX;
            float holeRightOsX = hasDock ? Mathf.Clamp(dockRightOsX, netLeftOsX, netRightOsX) : netRightOsX;

            float leftPieceWidth = holeLeftOsX - netLeftOsX;
            float rightPieceWidth = netRightOsX - holeRightOsX;

            var left = new Rect(netLeftOsX, netTopOsY, Mathf.Max(0f, leftPieceWidth), thickness);
            var right = new Rect(holeRightOsX, netTopOsY, Mathf.Max(0f, rightPieceWidth), thickness);

            return new Pieces(
                leftPieceWidth > MinPieceWidthOsPoints, left,
                rightPieceWidth > MinPieceWidthOsPoints, right);
        }
    }
}
