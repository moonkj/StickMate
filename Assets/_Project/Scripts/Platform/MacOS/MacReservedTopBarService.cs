#if UNITY_STANDALONE_OSX
using Kirurobo;
using UnityEngine;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// ★ 2026-09-02 — macOS <b>메뉴바 두께</b>의 사실 조회. docs/UX_FLOW.md 41-1 ③.
    ///
    /// <para>이 클래스는 <b>판정을 하지 않는다</b> — "그러니까 팝오버를 어디에 놓아라"는
    /// <see cref="SurfaceSafeAreaPolicy"/>(플랫폼 중립)의 몫이다. 여기 있는 것은 뺄셈 한 줄뿐이다
    /// (CLAUDE.md: "플랫폼 전용 코드는 사실 조회만").</para>
    ///
    /// ============================================================================
    /// 새 네이티브 코드가 0줄인 이유 — 두 조회가 이미 있다
    /// ============================================================================
    /// <code>
    ///   화면 전체 = CGDisplayBounds          → MacWindowService.TryGetMainDisplayBounds()  (0,0,1512,982)
    ///   작업 영역 = NSScreen.visibleFrame     → UniWindowController.GetMonitorRect(0)       (0,75,1512,874)
    ///
    ///   상단 인셋 = 982 − (75 + 874) = 33 pt   ✔ 이 화면의 메뉴바 실측과 일치
    ///   (참고로 하단 인셋 = visibleFrame.y = 75 = Dock 두께. Tools/와 ARCHITECTURE.md가 이미
    ///    같은 식으로 75pt를 검증해 뒀다 — 선례가 있는 유도식이다.)
    /// </code>
    /// <c>visibleFrame</c>은 <b>OS가 직접 뺀 값</b>이라 노치 맥이든 외장 모니터든 메뉴바 글꼴이 크든
    /// 우리가 33이나 38을 짐작할 필요가 없다.
    ///
    /// <para><b>안 쓰는 것</b>(2026-09-01 실측으로 전부 반증됨): <c>Screen.safeArea</c>의 top(32) ·
    /// macOS <c>statusThick</c>(22) · <c>auxiliary</c>(32). <b>셋 다 실제 33이 아니다.</b></para>
    ///
    /// ============================================================================
    /// 예외를 0으로 접는 자리
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>메뉴바 자동 숨김</b>: <c>visibleFrame</c>이 화면 전체와 같아져 뺄셈이 0을 낸다 →
    ///        false. OS가 그 띠를 예약하지 않았으므로 우리도 비우지 않는다(더 나빠지지 않는다).</item>
    ///  <item><b>보조 모니터</b>: macOS 기본값은 "메뉴바는 주 디스플레이에만"이라 인셋이 0일 수 있다.
    ///        유도식이 알아서 0을 낸다 — 특례가 필요 없다.</item>
    ///  <item><b>상식 범위를 벗어난 값</b>: 화면 높이의 25%를 넘는 인셋은 메뉴바가 아니라 조회가
    ///        어긋난 것이다(멀티모니터 원점 혼선 등). 그때는 <b>0으로 접는다</b> — 화면 위쪽을
    ///        근거 없이 잘라 먹는 쪽이 더 나쁘다.</item>
    /// </list>
    /// </summary>
    public sealed class MacReservedTopBarService : IReservedTopBarService
    {
        /// <summary>이보다 두꺼우면 메뉴바가 아니라 조회가 어긋난 것으로 본다(화면 높이 대비 비율).</summary>
        private const float SanityMaxInsetFraction = 0.25f;

        private readonly MacWindowService _display;

        public MacReservedTopBarService(MacWindowService display)
        {
            _display = display;
        }

        /// <summary>플랫폼 서비스에서 <see cref="MacWindowService"/>를 찾아 조립한다. 못 찾으면 false —
        /// 에디터/폴백 구현에서는 조용히 인셋 0으로 남는다.</summary>
        public static bool TryCreate(IPlatformWindowService service, out MacReservedTopBarService created)
        {
            var mac = service as MacWindowService;
            created = mac != null ? new MacReservedTopBarService(mac) : null;
            return created != null;
        }

        public bool TryGetReservedTopInsetPoints(out float insetPoints)
        {
            insetPoints = 0f;
            if (_display == null) return false;
            if (!_display.TryGetMainDisplayBounds(out Rect display) || display.height <= 0f) return false;

            // GetMonitorRect(0)은 macOS에서 visibleFrame(Cocoa 좌하단 원점)이다 — 실측 (0,75,1512,874).
            Rect visible = UniWindowController.GetMonitorRect(0);
            if (visible.height <= 0f) return false;

            float inset = display.height - (visible.y + visible.height);
            if (float.IsNaN(inset) || float.IsInfinity(inset)) return false;
            if (inset <= 0f) return false;                                       // 자동 숨김 / 보조 화면.
            if (inset > display.height * SanityMaxInsetFraction) return false;   // 조회가 어긋났다.

            insetPoints = inset;
            return true;
        }
    }
}
#endif
