using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 사용자 신고 "지금도 제대로 바닥과 독을 제대로 인식 못하는거 같음"(2026-08-29, 2차)의
    /// 재발 방지 테스트 — **Dock 사각형 계산식 자체**를 실측값에 못박는다.
    ///
    /// ============================================================================
    /// 무엇이 버그였나
    /// ============================================================================
    /// Dock 폭 공식의 유일한 미지수가 "실행 중이지만 Dock에 고정돼 있지 않은 앱의 타일 수"였고,
    /// 직전 라운드는 그걸 셀 방법이 없다고 보고 StickConfig.dockExtraRunningAppTileEstimate = 6으로
    /// **일부러 넓게** 잡았다. 결과: 실측 Dock이 x 194~1318인데 계산은 x 125.5~1386.5 — 좌우 각 77pt
    /// 과대. 그 77pt 띠에서 캐릭터가 "Dock이 없는 자리인데 Dock 상단 높이에 서 있는"(= 부양) 상태가
    /// 됐고 사용자가 스크린샷과 함께 즉시 신고했다.
    ///
    /// 수정: 그 타일 수를 NSWorkspace.runningApplications(activationPolicy == Regular)로 **정확히
    /// 센다**. 이 테스트가 잠그는 것은 두 가지다.
    ///   (1) 계산식이 실측 6표본을 최대 1.5pt 안에서 재현하는가.
    ///   (2) **네거티브 컨트롤** — 타일 수를 정확히 셌을 때(IsTileCountExact) 그 보정 상수가
    ///       정말로 무시되는가. 이게 깨지면 부양이 그대로 재발한다.
    ///
    /// 실측 표본(2026-08-29, 1512x982 / tilesize=49 / 구분선 2개). 앱을 하나씩 켜서 타일을 1개씩만
    /// 늘리고 매번 screencapture PNG에서 Dock 패널 좌우 테두리를 다시 쟀다. 매 표본에서 좌우 끝의
    /// 중점이 화면 정중앙 756.0pt와 0.25pt 이내로 일치해 측정 자체를 교차 검증했다.
    /// </summary>
    public sealed class DockMetricsGeometryTests
    {
        private const string LogPrefix = "[DOCK-METRICS-TEST]";

        /// <summary>실측 표본: 타일 수 -> Dock 패널 폭(OS 포인트). tilesize=49, 구분선 2개.</summary>
        private static readonly (int Tiles, float WidthPoints)[] MeasuredSamples =
        {
            (20, 1123.50f), (21, 1175.00f), (22, 1229.00f),
            (23, 1281.00f), (24, 1335.00f), (25, 1387.00f),
        };

        private const float MeasuredTileSize = 49f;
        private const int MeasuredSeparators = 2;

        /// <summary>공식의 허용 잔차. 실측 최대 오차는 1.0pt이며 여기에 여유 0.5pt를 뒀다.</summary>
        private const float FormulaTolerancePoints = 1.5f;

        /// <summary>실측이 이뤄진 화면 폭(OS 포인트). 가상 화면을 이보다 넓게 잡는 기준으로만 쓴다.</summary>
        private const float ReferenceScreenWidthPoints = 1512f;

        private StickConfig _config;
        private Vector2 _savedOrigin;

        /// <summary>Dock 실측값을 시험자가 직접 주입할 수 있는 내부 서비스 스텁.</summary>
        private sealed class StubDockService : IPlatformWindowService, IDockMetricsService
        {
            public DockMetrics Metrics;
            public bool Available = true;

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => System.Array.Empty<PlatformFoothold>();
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;

            public bool TryGetDockMetrics(out DockMetrics metrics)
            {
                metrics = Metrics;
                return Available;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<StickConfig>();

            // ★ 배치 모드(-batchmode)의 가상 화면은 640x480이라, 실측 표본(폭 1100~1400pt)이
            // TryGetDockRectOsScreen의 "Dock은 화면보다 넓을 수 없다" 클램프에 그대로 걸려 전부 640으로
            // 뭉개진다(실제로 이 테스트를 처음 돌렸을 때 세 건이 그렇게 실패했다). 그래서 DPI 배율로
            // 가상 화면을 실기(1512pt)보다 넉넉히 넓혀 놓고 잰다 — 이 테스트가 검증하려는 것은 화면
            // 크기와 무관한 '폭 공식' 자체이기 때문이다. 클램프 자체의 정당성은 그대로다(macOS도
            // Dock이 화면을 넘으면 타일을 줄인다).
            _config.desktopDpiScale = Mathf.Max(1f, ReferenceScreenWidthPoints * 1.25f / Mathf.Max(1f, Screen.width));
            // 실측으로 보정된 계수들을 명시적으로 못박는다(자산 기본값이 바뀌어도 이 계약은 유지).
            _config.dockMetricsFromSystemEnabled = true;
            _config.dockTilePitchPaddingPoints = 4f;
            _config.dockPanelFixedPaddingPoints = 15f;
            _config.dockSeparatorWidthPoints = 24f;
            _config.dockThicknessTilePaddingPoints = 26f;
            _config.dockFootholdEdgeInsetPoints = 6f;
            _config.dockExtraRunningAppTileEstimate = 0;
            _config.dockFootholdThicknessPoints = 75f;
            _config.dockFootholdWidthFraction = 0.65f;

            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
        }

        [TearDown]
        public void TearDown()
        {
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static DockMetrics Metrics(int tiles, bool exact = true, int separators = MeasuredSeparators,
            float tileSize = MeasuredTileSize, bool bottom = true, bool autoHide = false)
            => new DockMetrics(bottom, autoHide, tileSize, tiles, separators, exact);

        private FallbackPlatformWindowService NewService(DockMetrics m, bool available = true)
        {
            var stub = new StubDockService { Metrics = m, Available = available };
            return new FallbackPlatformWindowService(stub, _config);
        }

        private static float ScreenWidthPoints(StickConfig config)
            => (Screen.width > 0 ? Screen.width : 1920f) * config.desktopDpiScale;

        /// <summary>가장자리 여유(narrow bias)를 되돌린 "계산된 Dock 패널" 폭.</summary>
        private float PanelWidthOf(Rect rect) => rect.width + 2f * _config.dockFootholdEdgeInsetPoints;

        // ====================================================================
        // (1) 공식 대 실측 — 6표본
        // ====================================================================

        [Test]
        public void 폭_공식이_실측_6표본을_재현한다()
        {
            foreach ((int tiles, float measured) in MeasuredSamples)
            {
                var service = NewService(Metrics(tiles));
                Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect),
                    $"{LogPrefix} 타일 {tiles}개에서 Dock 사각형을 얻지 못했습니다.");

                float computed = PanelWidthOf(rect);
                Debug.Log($"{LogPrefix} 타일 {tiles}개 -> 계산 폭 {computed:F2}pt / 실측 {measured:F2}pt " +
                          $"(오차 {computed - measured:+0.00;-0.00}pt)");
                Assert.AreEqual(measured, computed, FormulaTolerancePoints,
                    $"{LogPrefix} 타일 {tiles}개에서 폭이 실측과 {FormulaTolerancePoints}pt 넘게 어긋났습니다. " +
                    "StickConfig의 dockTilePitchPaddingPoints/dockPanelFixedPaddingPoints/dockSeparatorWidthPoints를 확인하세요.");
            }
        }

        [Test]
        public void 계산된_구간은_실측_패널_안쪽에_있다_넓게_틀리지_않는다()
        {
            // 리더 지시: 오차가 남으면 틀리는 방향을 '좁게'로 둔다. 넓게 틀리면 Dock이 없는 자리에서
            // 캐릭터가 떠 보여 '고장'으로 읽히고(이번 신고), 좁게 틀리면 Dock 가장자리에서 조금 일찍
            // 내려갈 뿐이다. 이 테스트가 그 방향성을 못박는다.
            float screenW = ScreenWidthPoints(_config);
            foreach ((int tiles, float measured) in MeasuredSamples)
            {
                var service = NewService(Metrics(tiles));
                Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect));

                float measuredLeft = (screenW - measured) * 0.5f;
                float measuredRight = measuredLeft + measured;

                Assert.GreaterOrEqual(rect.xMin, measuredLeft,
                    $"{LogPrefix} 타일 {tiles}개: 계산 왼쪽 끝({rect.xMin:F2})이 실측 패널 왼쪽({measuredLeft:F2})보다 " +
                    "바깥입니다 — Dock이 없는 자리에 발판이 생겨 '부양'이 재발합니다.");
                Assert.LessOrEqual(rect.xMax, measuredRight,
                    $"{LogPrefix} 타일 {tiles}개: 계산 오른쪽 끝({rect.xMax:F2})이 실측 패널 오른쪽({measuredRight:F2})보다 바깥입니다.");
            }
        }

        // ====================================================================
        // (2) ★ 네거티브 컨트롤 — 정확히 셌을 때 보정 상수는 무시되어야 한다
        // ====================================================================

        [Test]
        public void 타일수를_정확히_셌으면_보정상수는_무시된다()
        {
            var exact = NewService(Metrics(20, exact: true));
            Assert.IsTrue(exact.TryGetDockRectOsScreen(out Rect before));

            // 이번 신고를 만든 바로 그 값(6)을 넣는다. 정확히 센 경우에는 아무 영향도 없어야 한다.
            _config.dockExtraRunningAppTileEstimate = 6;
            var stillExact = NewService(Metrics(20, exact: true));
            Assert.IsTrue(stillExact.TryGetDockRectOsScreen(out Rect after));

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 보정 0일 때 {before.xMin:F1}~{before.xMax:F1} / " +
                      $"보정 6일 때 {after.xMin:F1}~{after.xMax:F1}");
            Assert.AreEqual(before.xMin, after.xMin, 0.01f,
                $"{LogPrefix} 타일 수를 정확히 셌는데도 dockExtraRunningAppTileEstimate가 폭을 바꿨습니다 — " +
                "이 경로가 살아나면 2026-08-29 2차 신고('부양')가 그대로 재발합니다.");
            Assert.AreEqual(before.xMax, after.xMax, 0.01f);
        }

        [Test]
        public void 타일수를_못_셌을_때만_보정상수가_적용된다()
        {
            // 네거티브 컨트롤의 짝 — 보정 경로가 "죽어 있는" 게 아니라 필요할 때만 작동함을 확인한다.
            _config.dockExtraRunningAppTileEstimate = 6;
            var inexact = NewService(Metrics(20, exact: false));
            Assert.IsTrue(inexact.TryGetDockRectOsScreen(out Rect withEstimate));

            _config.dockExtraRunningAppTileEstimate = 0;
            var noEstimate = NewService(Metrics(20, exact: false));
            Assert.IsTrue(noEstimate.TryGetDockRectOsScreen(out Rect without));

            float pitch = MeasuredTileSize + _config.dockTilePitchPaddingPoints;
            Debug.Log($"{LogPrefix} 부정확 경로 — 보정 6개 적용 시 폭 {withEstimate.width:F1}, 미적용 {without.width:F1} " +
                      $"(피치 {pitch:F1} x 6 = {pitch * 6f:F1} 차이여야 함)");
            Assert.AreEqual(pitch * 6f, withEstimate.width - without.width, 0.05f,
                $"{LogPrefix} 타일 수를 못 셌을 때는 보정 상수가 정확히 타일 수만큼 폭을 넓혀야 합니다.");
        }

        // ====================================================================
        // (3) Dock 발판이 존재하면 안 되는 상태 — 자동 숨김 / 좌우 세로 Dock
        // ====================================================================

        [Test]
        public void 자동숨김이면_Dock_발판이_생기지_않는다()
        {
            var service = NewService(Metrics(20, autoHide: true));
            Assert.IsFalse(service.TryGetDockRectOsScreen(out _),
                $"{LogPrefix} 자동 숨김 Dock을 발판으로 삼으면 캐릭터가 허공에 섭니다.");

            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();
            Assert.IsFalse(HasHandle(footholds, FallbackPlatformWindowService.DockFootholdHandle),
                $"{LogPrefix} 자동 숨김인데 Dock 발판이 열거됐습니다.");
            Assert.AreEqual(1, footholds.Count,
                $"{LogPrefix} Dock이 없으면 안전망은 전체 폭 한 조각이어야 합니다(실제 {footholds.Count}개).");
        }

        [Test]
        public void 좌우_세로Dock이면_Dock_발판이_생기지_않는다()
        {
            var service = NewService(Metrics(20, bottom: false));
            Assert.IsFalse(service.TryGetDockRectOsScreen(out _),
                $"{LogPrefix} 좌/우 세로 Dock에는 '화면 하단 가로 띠'라는 발판 개념이 성립하지 않습니다.");
            Assert.IsFalse(HasHandle(service.EnumerateFootholds(), FallbackPlatformWindowService.DockFootholdHandle));
        }

        // ====================================================================
        // (4) 두께 / 구분선 / 단일 소스
        // ====================================================================

        [Test]
        public void 두께는_하드코딩이_아니라_tilesize에서_파생된다()
        {
            // 실측: tilesize=49일 때 Dock 두께가 정확히 75.00pt였다(visibleFrame 하단 인셋과 일치).
            var service = NewService(Metrics(20, tileSize: 49f));
            Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect));
            Assert.AreEqual(75f, rect.height, 0.01f,
                $"{LogPrefix} tilesize=49에서 두께가 실측 75pt와 다릅니다.");

            // Dock을 크게 만든 사용자에게도 따라가야 한다 — 하드코딩 75였다면 이 검증이 실패한다.
            var bigger = NewService(Metrics(20, tileSize: 64f));
            Assert.IsTrue(bigger.TryGetDockRectOsScreen(out Rect bigRect));
            Assert.AreEqual(64f + _config.dockThicknessTilePaddingPoints, bigRect.height, 0.01f,
                $"{LogPrefix} tilesize가 커졌는데 두께가 따라오지 않았습니다(하드코딩 회귀).");

            float screenH = (Screen.height > 0 ? Screen.height : 1080f) * _config.desktopDpiScale;
            Assert.AreEqual(screenH - rect.height, rect.y, 0.01f,
                $"{LogPrefix} Dock 상단 Y는 '화면 바닥 - 두께'여야 합니다.");
        }

        [Test]
        public void 구분선_개수가_폭에_반영된다()
        {
            var two = NewService(Metrics(20, separators: 2));
            var one = NewService(Metrics(20, separators: 1));
            Assert.IsTrue(two.TryGetDockRectOsScreen(out Rect r2));
            Assert.IsTrue(one.TryGetDockRectOsScreen(out Rect r1));
            Assert.AreEqual(_config.dockSeparatorWidthPoints, r2.width - r1.width, 0.01f,
                $"{LogPrefix} 구분선 1개 차이가 폭에 dockSeparatorWidthPoints만큼 반영되어야 합니다 " +
                "(show-recents를 끈 사용자는 구분선이 1개다).");
        }

        [Test]
        public void 안전망_구멍과_Dock_발판은_같은_단일소스에서_나온다()
        {
            // 이 프로젝트가 두 번 겪은 실패(BUG-P1-R4-B1 / BUG-P1-R5-B2): 발판과 안전망 구멍을 각각
            // 계산해 어긋나면 틈(낙하 고착) 또는 겹침(Dock 밑 보행)이 생긴다. 실측 계산 경로에서도
            // 그 계약이 유지되는지 확인한다.
            var service = NewService(Metrics(20));
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();

            PlatformFoothold dock = FindHandle(footholds, FallbackPlatformWindowService.DockFootholdHandle);
            PlatformFoothold left = FindHandle(footholds, FallbackPlatformWindowService.SyntheticFootholdHandle);
            PlatformFoothold right = FindHandle(footholds, FallbackPlatformWindowService.SyntheticFootholdHandleRight);

            Assert.AreEqual(dock.ScreenRect.xMin, left.ScreenRect.xMax, 0.01f,
                $"{LogPrefix} 왼쪽 안전망 끝과 Dock 왼쪽 끝이 정확히 만나야 합니다.");
            Assert.AreEqual(dock.ScreenRect.xMax, right.ScreenRect.xMin, 0.01f,
                $"{LogPrefix} 오른쪽 안전망 시작과 Dock 오른쪽 끝이 정확히 만나야 합니다.");
            Debug.Log($"{LogPrefix} 단일 소스 확인 — Dock {dock.ScreenRect.xMin:F1}~{dock.ScreenRect.xMax:F1}, " +
                      $"안전망 {left.ScreenRect.xMin:F1}~{left.ScreenRect.xMax:F1} / {right.ScreenRect.xMin:F1}~{right.ScreenRect.xMax:F1}");
        }

        [Test]
        public void 실측조회가_실패하면_예전_고정비율_폴백으로_돌아간다()
        {
            var service = NewService(Metrics(20), available: false);
            Assert.IsTrue(service.TryGetDockRectOsScreen(out Rect rect),
                $"{LogPrefix} 조회 실패 시에도 고정 비율 폴백으로 Dock 발판은 나와야 합니다.");

            float screenW = ScreenWidthPoints(_config);
            Assert.AreEqual(screenW * _config.dockFootholdWidthFraction, PanelWidthOf(rect),
                Mathf.Max(0.02f, screenW * 0.0005f),
                $"{LogPrefix} 폴백 폭이 dockFootholdWidthFraction에서 나오지 않았습니다.");
            Assert.AreEqual(_config.dockFootholdThicknessPoints, rect.height, 0.01f,
                $"{LogPrefix} 폴백 두께는 dockFootholdThicknessPoints여야 합니다.");
        }

        private static bool HasHandle(IReadOnlyList<PlatformFoothold> list, long handle)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Handle == handle) return true;
            return false;
        }

        private static PlatformFoothold FindHandle(IReadOnlyList<PlatformFoothold> list, long handle)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Handle == handle) return list[i];
            Assert.Fail($"{LogPrefix} 핸들 {handle} 발판을 찾지 못했습니다(열거 {list.Count}개).");
            return default;
        }
    }
}
