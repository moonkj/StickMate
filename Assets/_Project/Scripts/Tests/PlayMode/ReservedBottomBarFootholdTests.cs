using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 사용자 신고(2026-08-31, 실제 Windows PC 첫 실행) <b>"작업표시줄에 걸쳐서 돌아다닌다"</b>의
    /// 재발 방지 테스트.
    ///
    /// ============================================================================
    /// 무엇이 버그였나
    /// ============================================================================
    /// Windows에는 "하단 예약 막대"를 알려주는 경로가 아예 배선돼 있지 않았다. 그래서
    /// <see cref="FallbackPlatformWindowService.TryGetDockRectOsScreen"/>이 <b>2순위 폴백</b>인
    /// <c>dockFootholdWidthFraction</c>(화면 폭 65%를 <b>가운데</b> 정렬) + <c>dockFootholdThicknessPoints</c>
    /// (75pt)로 떨어졌다 — 이건 macOS Dock의 모양이다. 실제 작업표시줄은 <b>화면 가로 전체</b>다.
    /// 결과: 화면 좌우 각 17.5% 구간에서는 발판이 "화면 최하단 안전망"이 되어 캐릭터가 작업표시줄
    /// <b>안</b>에 섰다(= 걸쳐서 돌아다님).
    ///
    /// ============================================================================
    /// 이 테스트가 잠그는 것 (전부 플랫폼 중립 — 데코레이터 로직만 시험한다)
    /// ============================================================================
    ///  ① 정확한 사각형을 아는 플랫폼에서는 그 값이 <b>그대로</b> 발판이 된다(추정식이 개입하지 않는다).
    ///  ② <b>네거티브 컨트롤(핵심)</b>: 그 사각형이 화면 전체 폭일 때 65% 추정으로 좁아지지 않는다.
    ///  ③ <b>안전망 구멍 = 막대 구간</b>이므로 막대 가로 구간 안에서 "화면 최하단"에 설 수 있는 합성
    ///     발판이 <b>하나도 남지 않는다</b>. 이게 남으면 신고 증상이 그대로 재발한다.
    ///  ④ OS가 "하단 막대 없음"이라고 확정하면(자동 숨김/좌우 세로 배치) 추정으로 <b>흘러가지 않는다</b>.
    ///     흘러가면 존재하지 않는 막대 위에 캐릭터가 부양한다.
    ///  ⑤ <b>macOS 무회귀</b>: 이 캐퍼빌리티를 구현하지 않는 내부 서비스에서는 예전 경로(고정 비율
    ///     추정)가 <b>한 글자도 바뀌지 않고</b> 그대로 동작한다.
    ///
    /// 배치 실행의 가상 화면(640x480)에 종속되지 않도록, 막대 사각형은 실제 화면 크기에서 유도한다.
    /// </summary>
    public sealed class ReservedBottomBarFootholdTests
    {
        private const string LogPrefix = "[작업표시줄-TEST]";

        /// <summary>실제 Windows 작업표시줄의 전형적인 두께(100% 배율 Win11 = 48px). 값 자체가 계약은
        /// 아니고 "추정 75pt와 확실히 다른 값"이어야 ①/②가 의미를 갖는다.</summary>
        private const float BarThickness = 48f;

        private StickConfig _config;
        private Vector2 _savedOrigin;

        /// <summary>하단 예약 막대를 시험자가 직접 주입하는 내부 서비스 스텁(Windows 역할).</summary>
        private sealed class StubBottomBarService : IPlatformWindowService, IReservedBottomBarService
        {
            public Rect Bar;
            public bool Available = true;

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => System.Array.Empty<PlatformFoothold>();
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;

            public bool TryGetReservedBottomBarOsScreen(out Rect osScreenRect)
            {
                osScreenRect = Bar;
                return Available;
            }
        }

        /// <summary>이 캐퍼빌리티를 <b>구현하지 않는</b> 플랫폼(macOS 역할) — ⑤ 무회귀 대조군.</summary>
        private sealed class StubPlainService : IPlatformWindowService
        {
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => System.Array.Empty<PlatformFoothold>();
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.desktopDpiScale = 1f;              // 좌표 배율 고정 — 화면 픽셀 = OS 좌표.
            _config.dockMetricsFromSystemEnabled = true;
            _config.dockFootholdWidthFraction = 0.65f; // 신고를 만든 그 추정값(네거티브 컨트롤의 기준).
            _config.dockFootholdThicknessPoints = 75f;
            // 추정 경로의 "안쪽으로 깎기"는 이 테스트의 관심사가 아니다 — 0으로 두어 대조군(⑤)의
            // 기대 폭이 자산 기본값 변화에 흔들리지 않게 한다.
            _config.dockFootholdEdgeInsetPoints = 0f;

            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
        }

        [TearDown]
        public void TearDown()
        {
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static float ScreenW => Screen.width > 0 ? Screen.width : 1920f;
        private static float ScreenH => Screen.height > 0 ? Screen.height : 1080f;

        /// <summary>화면 가로 전체를 덮는 작업표시줄(가장 흔한 구성).</summary>
        private static Rect FullWidthBar() => new Rect(0f, ScreenH - BarThickness, ScreenW, BarThickness);

        private FallbackPlatformWindowService NewService(Rect bar, bool available = true)
            => new FallbackPlatformWindowService(new StubBottomBarService { Bar = bar, Available = available }, _config);

        // ====================================================================
        // ① 정확한 사각형이 그대로 발판이 된다
        // ====================================================================

        [Test]
        public void OS가_알려준_막대_사각형이_그대로_발판이_된다()
        {
            Rect bar = FullWidthBar();
            var service = NewService(bar);

            Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect),
                $"{LogPrefix} 하단 막대가 있는데 발판 사각형을 얻지 못했습니다.");

            Debug.Log($"{LogPrefix} 주입 {bar} -> 발판 {rect}");
            Assert.AreEqual(bar.xMin, rect.xMin, 0.001f, $"{LogPrefix} 좌측 끝이 주입값과 다릅니다.");
            Assert.AreEqual(bar.xMax, rect.xMax, 0.001f, $"{LogPrefix} 우측 끝이 주입값과 다릅니다.");
            Assert.AreEqual(bar.yMin, rect.yMin, 0.001f,
                $"{LogPrefix} 발판 상단 Y가 작업표시줄 상단과 달라졌습니다 — 추정 두께(75pt)가 섞였을 수 있습니다.");
            Assert.AreEqual(bar.height, rect.height, 0.001f, $"{LogPrefix} 두께가 주입값과 다릅니다.");
        }

        // ====================================================================
        // ② 네거티브 컨트롤 — 65% 추정으로 좁아지지 않는다 (이 신고의 본체)
        // ====================================================================

        [Test]
        public void 전체폭_막대가_65퍼센트_추정으로_좁아지지_않는다()
        {
            Rect bar = FullWidthBar();
            var service = NewService(bar);
            Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect));

            float estimateWidth = ScreenW * _config.dockFootholdWidthFraction;
            Debug.Log($"{LogPrefix} 발판 폭 {rect.width:F1} / 화면 폭 {ScreenW:F1} " +
                      $"(옛 추정이었다면 {estimateWidth:F1}이었을 것)");

            Assert.AreEqual(ScreenW, rect.width, 0.001f,
                $"{LogPrefix} 작업표시줄이 화면 전체 폭인데 발판이 좁아졌습니다 — 추정 경로가 되살아났습니다.");
            Assert.Less(estimateWidth, rect.width,
                $"{LogPrefix} 이 단언이 무의미해지지 않도록: 옛 추정 폭({estimateWidth:F1})은 반드시 " +
                $"실제 폭({rect.width:F1})보다 좁아야 두 경로가 구분된다.");
        }

        [Test]
        public void 두께가_추정상수_75가_아니라_실측값을_따른다()
        {
            var service = NewService(FullWidthBar());
            Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect));
            Assert.AreEqual(BarThickness, rect.height, 0.001f,
                $"{LogPrefix} 두께가 실측 {BarThickness}가 아니라 {rect.height}입니다 — " +
                $"dockFootholdThicknessPoints(75) 추정이 섞였습니다.");
        }

        // ====================================================================
        // ③ 막대 구간 안에 "화면 최하단" 합성 발판이 남지 않는다
        // ====================================================================

        [Test]
        public void 막대_가로구간에_화면최하단_안전망이_남지_않는다()
        {
            Rect bar = FullWidthBar();
            var service = NewService(bar);
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();

            int dockCount = 0;
            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold f = footholds[i];
                Debug.Log($"{LogPrefix} 발판[{i}] handle={f.Handle} rect={f.ScreenRect}");
                if (f.Handle == FallbackPlatformWindowService.DockFootholdHandle) { dockCount++; continue; }

                bool overlapsBarSpan = f.ScreenRect.xMax > bar.xMin + 0.5f && f.ScreenRect.xMin < bar.xMax - 0.5f;
                bool belowBarTop = f.ScreenRect.yMin > bar.yMin + 0.5f;
                Assert.IsFalse(overlapsBarSpan && belowBarTop,
                    $"{LogPrefix} 작업표시줄 가로 구간 안에 그보다 낮은 합성 발판(handle={f.Handle}, " +
                    $"{f.ScreenRect})이 남아 있습니다 — 캐릭터가 작업표시줄 안에서 걷게 됩니다(신고 증상 그대로).");
            }

            Assert.AreEqual(1, dockCount,
                $"{LogPrefix} 하단 막대 발판은 정확히 1개여야 합니다(실제 {dockCount}개).");
        }

        // ====================================================================
        // ④ "막대 없음" 확정 시 추정으로 흘러가지 않는다
        // ====================================================================

        [Test]
        public void 자동숨김_등으로_막대가_없으면_추정으로_흘러가지_않는다()
        {
            var service = NewService(FullWidthBar(), available: false);

            Assert.IsFalse(service.TryGetDockRectOsScreen(out _),
                $"{LogPrefix} OS가 '하단 막대 없음'을 확정했는데도 발판이 만들어졌습니다 — " +
                "고정 비율 추정(65%)으로 흘러갔습니다. 그러면 캐릭터가 허공에 부양합니다.");

            // 그 대신 화면 최하단 안전망은 정상적으로 살아 있어야 한다(무한 낙하 방지).
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();
            bool hasNet = false;
            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle == FallbackPlatformWindowService.SyntheticFootholdHandle
                    && footholds[i].ScreenRect.width > 1f) hasNet = true;
            }
            Assert.IsTrue(hasNet,
                $"{LogPrefix} 막대가 없을 때 화면 최하단 안전망까지 사라졌습니다 — 무한 낙하 회귀입니다.");
        }

        // ====================================================================
        // ⑤ macOS 무회귀 — 이 캐퍼빌리티가 없으면 예전 경로 그대로
        // ====================================================================

        [Test]
        public void 캐퍼빌리티가_없는_플랫폼은_예전_추정경로를_그대로_쓴다()
        {
            var service = new FallbackPlatformWindowService(new StubPlainService(), _config);
            Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect),
                $"{LogPrefix} 예전 추정 경로가 사라졌습니다(macOS 회귀).");

            float expectedWidth = ScreenW * _config.dockFootholdWidthFraction;
            Debug.Log($"{LogPrefix} (대조군) 추정 발판 {rect} / 기대 폭 {expectedWidth:F1}, 기대 두께 75");
            Assert.AreEqual(expectedWidth, rect.width, 1f,
                $"{LogPrefix} 고정 비율 추정 폭이 달라졌습니다.");
            Assert.AreEqual(_config.dockFootholdThicknessPoints, rect.height, 0.001f,
                $"{LogPrefix} 고정 추정 두께가 달라졌습니다.");
        }
    }
}
