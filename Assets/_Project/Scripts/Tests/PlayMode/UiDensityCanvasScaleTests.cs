using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 사용자 신고(2026-08-31, 실제 Windows PC 첫 실행)
    /// <b>"캐릭터창 해상도도 엄청 낮아서 글씨도 잘 안보임"</b>의 재발 방지 테스트.
    ///
    /// ============================================================================
    /// 무엇이 버그였나
    /// ============================================================================
    /// 캔버스 배율은 지금까지 <c>1 / AutoDpiScale</c> 하나에서만 나왔고, <c>AutoDpiScale</c>은
    /// "창 사각형(OS 단위) / Screen.width(Unity 픽셀)"이다. 이 비에 <b>디스플레이 배율이 실려 오는 것은
    /// macOS에서만</b> 참이다(창 사각형이 AppKit 포인트, Screen이 백킹 픽셀 -> 0.5 -> 배율 2).
    /// Windows에서는 <c>GetWindowRect</c>도 <c>Screen.width</c>도 둘 다 물리 픽셀이라 이 비가 항상
    /// 1.0이고, 디스플레이 배율(125%/150%)이 <b>어디에도 실리지 않는다</b> -> 캔버스 배율 1 ->
    /// 논리 포인트 기준으로 맞춰 둔 모든 UI가 물리 픽셀 크기로 그려져 1/1.25~1/1.5로 쪼그라든다.
    /// (같은 배율이 초상화 RenderTexture의 슈퍼샘플 배수이기도 해서 실제로 덜 선명하기까지 했다.)
    ///
    /// ============================================================================
    /// 이 테스트가 잠그는 것
    /// ============================================================================
    ///  ① 플랫폼이 UI 밀도를 보고하면 캔버스 배율이 <b>그 값</b>이 된다(좌표 배율과 무관하게).
    ///  ② <b>단위 왕복 불변식</b>: 배치 계산(UnityScreenToCanvas)과 크기 배율(캔버스 scaleFactor)이
    ///     같은 값에서 나온다. 둘이 갈라지면 UI가 커지면서 화면 밖으로 밀려난다.
    ///  ③ <b>수동 오버라이드 우선</b>: <c>StickConfig.desktopDpiScale</c>을 사람이 지정하면 그것이 이긴다.
    ///  ④ <b>macOS 무회귀(가장 중요)</b>: 아무도 보고하지 않으면 예전 정의(<c>1 / AutoDpiScale</c>)
    ///     그대로다 — Retina 2x에서 배율 2, 비Retina에서 1.
    ///  ⑤ 쓰레기 값(0/음수/NaN/무한대)은 무시하고 직전 값을 유지한다.
    ///
    /// 실제 화면 배율에 의존하지 않는다: 값을 직접 주입해 "배율 150%인 척"을 만든다
    /// (RetinaDpiCoordinateTests와 완전히 같은 관례).
    /// </summary>
    public sealed class UiDensityCanvasScaleTests
    {
        private const string LogPrefix = "[UI밀도-TEST]";

        private StickConfig _config;
        private float _savedAutoDpi;
        private float _savedDensity;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.desktopDpiScale = 0f;  // 0 = 자동
            _savedAutoDpi = ScreenCoordinateConverter.AutoDpiScale;
            _savedDensity = ScreenCoordinateConverter.AutoUiDensityScale;
            ScreenCoordinateConverter.ClearReportedUiDensity();
            ScreenCoordinateConverter.AutoDpiScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            ScreenCoordinateConverter.AutoDpiScale = _savedAutoDpi;
            ScreenCoordinateConverter.ClearReportedUiDensity();
            if (_savedDensity > 0f) ScreenCoordinateConverter.ReportUiDensityScale(_savedDensity);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // ====================================================================
        // ① 보고된 밀도가 캔버스 배율이 된다
        // ====================================================================

        [Test]
        public void 보고된_UI밀도가_캔버스_배율이_된다()
        {
            // Windows 150% 디스플레이: 좌표 배율은 1.0(창/커서 모두 물리 픽셀)이지만 UI 밀도는 1.5다.
            ScreenCoordinateConverter.AutoDpiScale = 1f;
            ScreenCoordinateConverter.ReportUiDensityScale(1.5f);

            float scale = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            Debug.Log($"{LogPrefix} 좌표배율 1.0 + UI밀도 1.5 -> 캔버스 배율 {scale:F3}");
            Assert.AreEqual(1.5f, scale, 1e-5f,
                $"{LogPrefix} 디스플레이 배율 150%에서 캔버스 배율이 1.5가 아닙니다 — " +
                "UI가 의도한 크기의 1/1.5로 쪼그라듭니다(신고 증상 그대로).");

            // 좌표 변환용 배율은 밀도 보고에 오염되지 않아야 한다(둘은 별개 개념이다).
            Assert.AreEqual(1f, ScreenCoordinateConverter.ResolveDpiScale(_config), 1e-5f,
                $"{LogPrefix} UI 밀도 보고가 좌표 배율까지 바꿨습니다 — 캐릭터 위치가 통째로 어긋납니다.");
        }

        // ====================================================================
        // ② 배치와 크기가 같은 값에서 나온다 (단위 왕복)
        // ====================================================================

        [Test]
        public void 배치계산과_크기배율이_같은_값에서_나온다()
        {
            foreach (float density in new[] { 1f, 1.25f, 1.5f, 2f })
            {
                ScreenCoordinateConverter.ReportUiDensityScale(density);
                float scale = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);

                foreach (float px in new[] { 1f, 37.5f, 880f, 1920f })
                {
                    float canvas = ScreenCoordinateConverter.UnityScreenToCanvas(px, _config);
                    Assert.AreEqual(px / scale, canvas, 1e-3f,
                        $"{LogPrefix} 밀도 {density}에서 배치 변환이 캔버스 배율의 역이 아닙니다 " +
                        "— UI가 커지면서 화면 밖으로 밀려납니다.");

                    float back = ScreenCoordinateConverter.CanvasToUnityScreen(canvas, _config);
                    Assert.AreEqual(px, back, 1e-2f, $"{LogPrefix} 밀도 {density}에서 왕복이 깨졌습니다.");
                }
            }
        }

        // ====================================================================
        // ③ 수동 오버라이드가 이긴다
        // ====================================================================

        [Test]
        public void 수동_오버라이드가_보고된_밀도보다_우선한다()
        {
            ScreenCoordinateConverter.ReportUiDensityScale(1.5f);
            _config.desktopDpiScale = 0.5f;   // 사람이 "Retina 2x"라고 못박은 경우.

            Assert.AreEqual(2f, ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config), 1e-5f,
                $"{LogPrefix} 수동 오버라이드(desktopDpiScale)가 무시됐습니다 — 현장 튜닝 수단이 죽습니다.");
        }

        // ====================================================================
        // ④ macOS 무회귀 — 아무도 보고하지 않으면 예전 정의 그대로
        // ====================================================================

        [Test]
        public void 밀도를_보고하지_않는_플랫폼은_예전_정의를_그대로_쓴다()
        {
            ScreenCoordinateConverter.ClearReportedUiDensity();

            ScreenCoordinateConverter.AutoDpiScale = 1f;
            Assert.AreEqual(1f, ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config), 1e-5f,
                $"{LogPrefix} 비Retina에서 캔버스 배율이 1이 아닙니다(macOS 회귀).");

            ScreenCoordinateConverter.AutoDpiScale = 0.5f;
            Assert.AreEqual(2f, ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config), 1e-5f,
                $"{LogPrefix} Retina 2x에서 캔버스 배율이 2가 아닙니다(macOS 회귀).");

            // 예전 식(v * ResolveDpiScale)과 정확히 같은 값이어야 한다.
            foreach (float px in new[] { 1f, 123.5f, 1512f })
            {
                Assert.AreEqual(px * ScreenCoordinateConverter.ResolveDpiScale(_config),
                    ScreenCoordinateConverter.UnityScreenToCanvas(px, _config), 1e-3f,
                    $"{LogPrefix} 밀도 미보고 환경에서 배치 변환식이 예전과 달라졌습니다.");
            }
        }

        // ====================================================================
        // ⑤ 쓰레기 값 방어
        // ====================================================================

        [Test]
        public void 잘못된_밀도값은_무시하고_직전_값을_유지한다()
        {
            ScreenCoordinateConverter.ReportUiDensityScale(1.5f);
            ScreenCoordinateConverter.ReportUiDensityScale(0f);
            ScreenCoordinateConverter.ReportUiDensityScale(-2f);
            ScreenCoordinateConverter.ReportUiDensityScale(float.NaN);
            ScreenCoordinateConverter.ReportUiDensityScale(float.PositiveInfinity);

            Assert.AreEqual(1.5f, ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config), 1e-5f,
                $"{LogPrefix} 잘못된 밀도를 받아들여 UI 배율이 무너졌습니다.");
        }
    }
}
