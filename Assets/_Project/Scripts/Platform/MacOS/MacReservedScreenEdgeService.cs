#if UNITY_STANDALONE_OSX
using Kirurobo;
using UnityEngine;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// ★ 2026-09-03 — macOS <b>네 변 예약 띠</b>(메뉴바 + Dock)의 사실 조회.
    /// <see cref="MacReservedTopBarService"/>의 상단 한 줄을 네 줄로 넓힌 것이고,
    /// <b>그 클래스가 이제 이 클래스를 호출한다</b>(산술을 두 벌로 만들지 않는다).
    ///
    /// <para>이 클래스는 <b>판정을 하지 않는다</b> — "그러니까 표면을 어디에 놓아라"는
    /// <see cref="SurfaceSafeAreaPolicy"/>(플랫폼 중립)의 몫이다. 여기 있는 것은 뺄셈 네 줄뿐이다
    /// (CLAUDE.md: "플랫폼 전용 코드는 사실 조회만").</para>
    ///
    /// ============================================================================
    /// 새 네이티브 코드가 0줄인 이유 — 두 조회가 이미 있다
    /// ============================================================================
    /// <code>
    ///   화면 전체 = CGDisplayBounds       -> MacWindowService.TryGetMainDisplayBounds()  (0,0,1512,982)
    ///   작업 영역 = NSScreen.visibleFrame  -> UniWindowController.GetMonitorRect(0)       (0,75,1512,874)
    ///
    ///   상 = display.height − (visible.y + visible.height) = 982 − (75+874) = 33 pt  (메뉴바)
    ///   하 = visible.y                                     = 75 pt                   (하단 Dock)
    ///   좌 = visible.x                                     = 0 pt
    ///   우 = display.width − (visible.x + visible.width)   = 1512 − 1512 = 0 pt
    /// </code>
    /// 이 개발 머신(Dock 하단)의 실측과 일치한다. Dock을 <b>왼쪽</b>으로 옮기면 <c>visible.x</c>가
    /// Dock 두께가 되어 좌 값에 그대로 나타나고, <b>오른쪽</b>으로 옮기면 우 값에 나타난다.
    /// <c>visibleFrame</c>은 <b>OS가 직접 뺀 값</b>이라 우리가 두께를 짐작할 필요가 없다.
    ///
    /// <para><b>좌표계 주의</b>: <c>CGDisplayBounds</c>는 좌상단 원점 전역 좌표, <c>visibleFrame</c>은
    /// Cocoa(좌하단 원점) 좌표다. 이 뺄셈이 성립하는 것은 둘 다 <b>주 디스플레이</b>를 보고 있고
    /// 주 디스플레이의 원점이 두 계에서 모두 x=0이기 때문이다. 보조 모니터로 확장하려면
    /// <c>GetMonitorRect(n)</c>과 그 모니터의 <c>CGDisplayBounds</c>를 <b>짝지어</b> 넣어야 한다 —
    /// 지금은 하지 않는다(짝을 못 맞추면 값이 조용히 어긋난다).</para>
    ///
    /// ============================================================================
    /// 예외를 "모름"으로 접는 자리 — 0으로 위장하지 않는다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>메뉴바/Dock 자동 숨김</b>: <c>visibleFrame</c>이 화면 전체와 같아져 뺄셈이 0을 낸다.
    ///        이건 <b>진짜 0</b>이다(OS가 그 띠를 예약하지 않았다) — 측정됨 + 0으로 보고한다.</item>
    ///  <item><b>상식 범위를 벗어난 값</b>: 화면 변 길이의 25%를 넘는 두께는 예약 띠가 아니라 조회가
    ///        어긋난 것이다(멀티모니터 원점 혼선 등). 그 변만 <b>미측정</b>으로 접는다 — 0으로
    ///        보고하면 "여기는 비어 있다고 확인됐다"는 거짓 사실이 다음 라운드에 전달된다.</item>
    ///  <item><b>음수</b>: <c>visibleFrame</c>은 정의상 화면 안에 있으므로 음수는 관측이 아니다.
    ///        <see cref="ReservedEdgeInsets.Observed"/>가 그 변을 미측정으로 접는다.</item>
    /// </list>
    ///
    /// <para><b>안 쓰는 것</b>(2026-09-01 실측으로 전부 반증됨): <c>Screen.safeArea</c>의 top(32) ·
    /// macOS <c>statusThick</c>(22) · <c>auxiliary</c>(32). <b>셋 다 실제 33이 아니다.</b></para>
    ///
    /// <para><b>하단 값은 아직 아무도 소비하지 않는다.</b> 캐릭터가 밟는 Dock 발판은 두께가 아니라
    /// <b>사각형</b>이 필요해서 <c>Platform/IDockMetricsService.cs</c>의 타일 실측 경로를 그대로 쓴다.
    /// 여기의 하단 값은 진단용으로만 채운다 — 발판 경로를 이 값으로 바꾸는 것은 별건이다.</para>
    /// </summary>
    public sealed class MacReservedScreenEdgeService : IReservedScreenEdgeService
    {
        /// <summary>이보다 두꺼우면 예약 띠가 아니라 조회가 어긋난 것으로 본다(그 변이 놓인 축 길이 대비 비율).</summary>
        private const float SanityMaxInsetFraction = 0.25f;

        private readonly MacWindowService _display;

        public MacReservedScreenEdgeService(MacWindowService display)
        {
            _display = display;
        }

        /// <summary>플랫폼 서비스에서 <see cref="MacWindowService"/>를 찾아 조립한다. 못 찾으면 false —
        /// 에디터/폴백 구현에서는 조용히 "네 변 모두 모름"으로 남는다.</summary>
        public static bool TryCreate(IPlatformWindowService service, out MacReservedScreenEdgeService created)
        {
            var mac = service as MacWindowService;
            created = mac != null ? new MacReservedScreenEdgeService(mac) : null;
            return created != null;
        }

        public bool TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets insets)
        {
            insets = ReservedEdgeInsets.Unknown;
            if (_display == null) return false;
            if (!_display.TryGetMainDisplayBounds(out Rect display)) return false;
            if (display.width <= 0f || display.height <= 0f) return false;

            // GetMonitorRect(0)은 macOS에서 visibleFrame(Cocoa 좌하단 원점)이다 — 실측 (0,75,1512,874).
            Rect visible = UniWindowController.GetMonitorRect(0);
            if (visible.width <= 0f || visible.height <= 0f) return false;

            insets = ReservedEdgeInsets.Observed(
                Plausible(display.height - (visible.y + visible.height), display.height),
                Plausible(visible.y, display.height),
                Plausible(visible.x, display.width),
                Plausible(display.width - (visible.x + visible.width), display.width));

            return insets.MeasuredEdges != ReservedEdge.None;
        }

        /// <summary>상식 범위를 벗어나면 <see cref="float.NaN"/>을 돌려준다 —
        /// <see cref="ReservedEdgeInsets.Observed"/>가 그것을 <b>미측정</b>으로 접는다(0으로 위장하지 않는다).</summary>
        private static float Plausible(float raw, float axisExtent)
            => raw > axisExtent * SanityMaxInsetFraction ? float.NaN : raw;
    }
}
#endif
