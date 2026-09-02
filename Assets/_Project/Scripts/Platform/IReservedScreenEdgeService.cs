using System;

namespace StickMate.Platform
{
    /// <summary>
    /// 화면의 <b>네 변</b> 중 하나를 가리키는 선택자 겸 마스크.
    /// <see cref="ReservedEdgeInsets.MeasuredEdges"/>가 이 비트들의 합집합으로 "어느 변을 실제로 쟀는가"를 담는다.
    /// </summary>
    [Flags]
    public enum ReservedEdge
    {
        None = 0,
        Top = 1 << 0,
        Bottom = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        All = Top | Bottom | Left | Right,
    }

    /// <summary>
    /// OS가 화면 <b>네 변</b>에 예약해 둔 띠의 두께(OS 포인트) 한 묶음.
    ///
    /// ============================================================================
    /// ★ 왜 「0」과 「모름」을 따로 담는가 — 이 저장소가 반복해서 당한 형태다
    /// ============================================================================
    /// <c>IWindowEnumerationCostSource</c>가 <b>미지원을 0이 아니라 음수로</b> 보고하는 이유와 같다:
    /// "0개였다"와 "못 쟀다"가 같은 값이 되면 원격 진단이 틀린 결론에 도달한다. 여기서는 두께가
    /// 물리적으로 0 이상이라 음수를 쓸 수 없으므로 <see cref="MeasuredEdges"/> 비트로 가른다.
    ///
    /// <para><b>다만 소비 측 동작은 둘 다 같다 — 0이면 아무것도 바꾸지 않는다.</b> 짐작으로 메우지
    /// 않는다는 규약(<see cref="ReservedTopBarProbe"/>의 "실패는 0이다")이 네 변 전부에 그대로 적용된다.
    /// 화면 폭에서 카드 폭을 빼서 <i>"아마 여기쯤 작업표시줄이 있겠지"</i>라고 추정하는 것은
    /// 이 계약을 정면으로 깨는 행위다. 추정값이 실제보다 크면 멀쩡한 화면을 낭비하고,
    /// 작으면 그대로 덮는다. 둘 다 나쁘다.</para>
    /// </summary>
    public readonly struct ReservedEdgeInsets
    {
        /// <summary>아무 변도 못 쟀다. 네 값 모두 0이고 <see cref="MeasuredEdges"/>는 <see cref="ReservedEdge.None"/>.</summary>
        public static ReservedEdgeInsets Unknown => default;

        /// <summary>화면 위쪽 예약 띠 두께(OS 포인트). macOS 메뉴바 / Windows 상단 도킹 작업표시줄·툴바.</summary>
        public readonly float TopPoints;

        /// <summary>화면 아래쪽 예약 띠 두께(OS 포인트). macOS Dock(하단) / Windows 하단 작업표시줄.</summary>
        public readonly float BottomPoints;

        /// <summary>화면 왼쪽 예약 띠 두께(OS 포인트). macOS Dock(좌) / Windows 좌측 도킹 작업표시줄.</summary>
        public readonly float LeftPoints;

        /// <summary>화면 오른쪽 예약 띠 두께(OS 포인트). macOS Dock(우) / Windows 우측 도킹 작업표시줄.</summary>
        public readonly float RightPoints;

        /// <summary>실제로 측정된 변들의 합집합. 여기 비트가 없는 변의 값은 <b>언제나 0</b>이며 "없다"가 아니라 "모른다"다.</summary>
        public readonly ReservedEdge MeasuredEdges;

        private ReservedEdgeInsets(float top, float bottom, float left, float right, ReservedEdge measured)
        {
            TopPoints = top;
            BottomPoints = bottom;
            LeftPoints = left;
            RightPoints = right;
            MeasuredEdges = measured;
        }

        /// <summary>
        /// 네 변을 <b>한 번의 조회로</b> 모두 관측했을 때 쓴다(Windows <c>GetMonitorInfo</c> 한 번 /
        /// macOS <c>CGDisplayBounds</c> ↔ <c>visibleFrame</c> 한 쌍).
        ///
        /// <para>값 하나가 <b>NaN·무한대·음수</b>면 그 변만 <b>측정되지 않은 것</b>으로 접는다(값 0 + 비트 없음).
        /// 예약 띠 두께는 정의상 0 이상이므로 음수는 관측이 아니라 조회가 어긋난 것이고, 그걸 0으로
        /// 조용히 눌러 담으면 "OS가 아무것도 예약하지 않았다"는 <b>거짓 사실</b>이 된다.</para>
        /// </summary>
        public static ReservedEdgeInsets Observed(float top, float bottom, float left, float right)
        {
            ReservedEdge mask = ReservedEdge.None;
            if (Accept(ref top)) mask |= ReservedEdge.Top;
            if (Accept(ref bottom)) mask |= ReservedEdge.Bottom;
            if (Accept(ref left)) mask |= ReservedEdge.Left;
            if (Accept(ref right)) mask |= ReservedEdge.Right;
            return new ReservedEdgeInsets(top, bottom, left, right, mask);
        }

        /// <summary>
        /// 구식 <see cref="IReservedTopBarService"/>만 구현한 플랫폼용 — <b>상단만</b> 측정된 묶음.
        /// 나머지 세 변은 0이지만 비트가 없으므로 "없다"가 아니라 "모른다"로 읽힌다.
        /// </summary>
        public static ReservedEdgeInsets TopOnly(float top)
        {
            return Accept(ref top)
                ? new ReservedEdgeInsets(top, 0f, 0f, 0f, ReservedEdge.Top)
                : Unknown;
        }

        /// <summary><paramref name="edge"/>를 실제로 쟀는가. 복합 마스크를 주면 <b>그 전부를</b> 쟀을 때만 true.</summary>
        public bool IsMeasured(ReservedEdge edge)
            => edge != ReservedEdge.None && (MeasuredEdges & edge) == edge;

        /// <summary>
        /// 한 변의 두께(OS 포인트). 측정되지 않았으면 <b>0</b>이다(짐작으로 메우지 않는다).
        /// <b>단일 변만 받는다</b> — 복합 마스크나 <see cref="ReservedEdge.None"/>은 0을 돌려준다.
        /// </summary>
        public float PointsFor(ReservedEdge edge)
        {
            switch (edge)
            {
                case ReservedEdge.Top: return TopPoints;
                case ReservedEdge.Bottom: return BottomPoints;
                case ReservedEdge.Left: return LeftPoints;
                case ReservedEdge.Right: return RightPoints;
                default: return 0f;
            }
        }

        /// <summary>진단 로그용 한 줄. 값이 바뀔 때만 찍는 쪽에서 비교 키로도 쓴다(하루 종일 켜져 있는 앱이다).</summary>
        public override string ToString()
            => $"상{TopPoints:F1} 하{BottomPoints:F1} 좌{LeftPoints:F1} 우{RightPoints:F1}pt · 측정 {MeasuredEdges}";

        private static bool Accept(ref float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                value = 0f;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// ★ 2026-09-03 — <b>OS가 화면 네 변에 예약해 둔 띠</b>의 두께를 알려 주는 <b>사실 조회</b> 창구.
    /// <see cref="IReservedTopBarService"/>(상단 전용)의 <b>네 방향판</b>이다.
    ///
    /// ============================================================================
    /// 왜 상단 계약을 그대로 두고 네 방향 계약을 새로 만들었나 (근거)
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>축이 따로 자라면 다음 사람이 또 한 축을 빠뜨린다.</b> 실제로 그렇게 됐다 —
    ///        상단 계약이 생긴 다음 날, 화면 <b>오른쪽</b>에 붙는 할일 카드가 <b>우측 도킹
    ///        작업표시줄</b>(통상 48~62pt) 앞에서 같은 형태로 띠를 통째로 덮는다는 것이 발견됐고,
    ///        상단 프로브로는 <b>원리상</b> 잡을 수 없었다(상단 차이가 0이라 false를 낸다).
    ///        <c>IReservedLeftBarService</c>·<c>IReservedRightBarService</c>를 따로 만들면
    ///        같은 일이 세 번째로 반복된다.</item>
    ///  <item><b>OS 조회가 애초에 네 변을 한 번에 준다.</b> Windows는 <c>GetMonitorInfo</c> 한 번의
    ///        <c>rcMonitor</c>/<c>rcWork</c> 차가 네 변 전부이고, macOS는 <c>CGDisplayBounds</c>와
    ///        <c>NSScreen.visibleFrame</c> 한 쌍의 뺄셈이 네 변 전부다. 축마다 계약을 나누면
    ///        <b>같은 조회를 네 번</b> 하게 된다.</item>
    ///  <item><b>그런데 상단 계약은 못 지운다</b> — 소비 호출부가 이미 다섯 곳이다
    ///        (<c>Interaction/InfoGearIconWidget.cs</c> · <c>Interaction/GearRadialMenuWidget.cs</c> ·
    ///        <c>Interaction/CharacterInfoWindow.Layout.cs</c> · <c>Interaction/TodoPostItWidget.cs</c> ·
    ///        <c>Interaction/PopoverPanel.cs</c>). 그래서 <b>계약은 나란히 두되 산술은 한 벌</b>로 만든다:
    ///        양 플랫폼의 상단 전용 구현이 이 네 방향 조회를 호출해 <c>Top</c>만 꺼내 쓴다.
    ///        <b>두 벌이 되면 반드시 한쪽만 고쳐진다</b>는 이 저장소의 규칙을 계약이 아니라
    ///        <b>구현 층</b>에서 지킨다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 계약 — "두께"만 말한다. 무엇을 할지는 <see cref="SurfaceSafeAreaPolicy"/>가 정한다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>단위는 OS 포인트</b>(Unity 픽셀이 아니다). Windows의 <c>rcMonitor</c>/<c>rcWork</c>는
    ///        물리 픽셀이므로 구현체가 <see cref="ScreenCoordinateConverter"/> <b>한 곳</b>을 통과시켜
    ///        논리 포인트로 바꿔서 돌려준다(여기서 96으로 나누는 산술을 새로 쓰지 않는다).</item>
    ///  <item><b>false = "지금 아무것도 못 쟀다"</b>. true여도 <see cref="ReservedEdgeInsets.MeasuredEdges"/>에
    ///        없는 변은 "모른다"다. <b>둘 다 "추정하라"는 뜻이 아니다.</b></item>
    ///  <item><b>읽기 전용.</b> 작업표시줄/Dock을 숨기거나 옮기거나 크기를 바꾸는 API는 이 인터페이스의
    ///        구현체가 절대 부르지 않는다(절대 불변 원칙 3). 승인된 예외 1건(자동 숨김 비트)은
    ///        전혀 다른 경로(<c>Platform/IReservedBarAutoHideControl.cs</c>)에 있고 이 계약과 무관하다.</item>
    /// </list>
    ///
    /// <para><b>이 계약이 <see cref="IReservedBottomBarService"/>를 대체하지 않는다.</b> 그쪽은 하단
    /// 막대의 <b>사각형</b>(발판으로 쓸 좌표)을 주고 이쪽은 <b>두께</b>만 준다. 캐릭터가 밟고 서는
    /// 발판은 사각형이 필요하고, UI 표면 회피는 두께면 충분하다.</para>
    ///
    /// <para>선택적 캐퍼빌리티다 — 소비 측은 <see cref="ReservedEdgeProbe"/>를 거치고, 구현체가 없으면
    /// 네 변 전부 0(= 이 계약 도입 이전과 완전히 같은 배치)으로 돌아간다.</para>
    /// </summary>
    public interface IReservedScreenEdgeService
    {
        /// <summary>지금 이 순간 화면 네 변에 예약된 띠의 두께(OS 포인트).</summary>
        /// <param name="insets">성공했을 때의 두께 묶음. 실패하면 <see cref="ReservedEdgeInsets.Unknown"/>.</param>
        /// <returns>한 변도 재지 못했으면 false.</returns>
        bool TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets insets);
    }
}
