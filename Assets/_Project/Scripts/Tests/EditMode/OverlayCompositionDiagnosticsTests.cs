using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 — <b>Windows 알파/합성 판정 로직</b>의 회귀 잠금.
    ///
    /// ============================================================================
    /// 왜 이 테스트가 존재하는가 (이 라운드의 핵심 교훈)
    /// ============================================================================
    /// 기존 알파 회귀 테스트 3종(<c>InfoWindowPanelOpacityTests</c> /
    /// <c>PopoverAndHoverPanelOpacityTests</c> / <c>SettingsWindowChromeTests</c>)은 uGUI 계층의
    /// <b>알파 산술</b>을 CPU에서 시뮬레이션한다. 그 자체는 플랫폼 중립이라 Windows에서도 같은 값이
    /// 나오지만, <b>OS 합성 단계는 한 글자도 검증하지 않는다</b>. 그래서 그 셋이 전부 초록이어도
    /// Windows에서 "창이 겹쳐 보이고 텍스트가 번진다"가 그대로 남을 수 있었고, 실제로 그랬다.
    ///
    /// 이 파일이 검증하는 것은 <b>OS 관측값 -> 원인 판정</b>이라는 다른 층이다. 실기 실행이 불가능한
    /// 개발 머신에서 Windows 전용 판정 규칙을 지킬 수 있는 유일한 방법이며,
    /// <see cref="OverlayCompositionVerdict.Diagnose"/>가 순수 함수인 이유가 정확히 이것이다.
    ///
    /// <b>각 케이스는 실기에서 실제로 일어날 수 있는 상태 하나</b>에 대응하고, 그 상태가
    /// 두 신고 증상 중 어느 쪽(겹침/번짐)을 만드는지까지 잠근다.
    /// </summary>
    public sealed class OverlayCompositionDiagnosticsTests
    {
        /// <summary>"정상 Windows 출하 형상" 기준 관측 — 각 테스트는 여기서 <b>한 가지만</b> 어긋뜨린다.
        /// 그래야 판정 줄이 뜬 이유가 그 한 가지임이 보장된다.</summary>
        private static OverlayCompositionSnapshot Healthy() => new OverlayCompositionSnapshot
        {
            BackBufferWidth = 2560,
            BackBufferHeight = 1440,
            ClientWidth = 2560f,
            ClientHeight = 1440f,
            WindowWidth = 2560f,
            WindowHeight = 1440f,
            FullScreenMode = OverlayCompositionVerdict.FullScreenModeWindowed,

            CanvasScaleFactor = 1f,
            UiDensityScale = 1f,
            AutoDpiScale = 1f,
            SampleFontSizePoints = 13,

            TransparentType = OverlayCompositionVerdict.TransparentTypeAlpha,
            HasLayeredStyle = false,
            HasClickThroughStyle = true,
            LayeredAttributesInEffect = false,
            LayeredAlphaByte = -1,
            DwmCompositionEnabled = true,
            OsStyleReadOk = true,

            CameraBackground = new Color(0f, 0f, 0f, 0f),
            CameraClearFlags = OverlayCompositionVerdict.ClearFlagsSolidColor,
            CameraAllowHdr = false,
            CameraAllowMsaa = true,

            RequestedMsaa = 4,
            ActualMsaa = 4,
            UiSpriteFilterMode = (int)FilterMode.Bilinear,
        };

        private static List<OverlayCompositionVerdict.Line> Diagnose(OverlayCompositionSnapshot s)
            => OverlayCompositionVerdict.Diagnose(s);

        private static bool Has(List<OverlayCompositionVerdict.Line> lines, string code, out OverlayCompositionVerdict.Line hit)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Code == code) { hit = lines[i]; return true; }
            }
            hit = default;
            return false;
        }

        private static void AssertFault(List<OverlayCompositionVerdict.Line> lines, string code, CompositionFault expected)
        {
            Assert.IsTrue(Has(lines, code, out var line),
                $"판정 줄 [{code}]가 없습니다 — 실기 로그에서 이 원인을 가를 수 없다는 뜻입니다. " +
                $"실제 줄: {string.Join(" / ", lines.ConvertAll(l => l.Code))}");
            Assert.AreEqual(expected, line.Fault,
                $"[{code}]의 증상 분류가 {line.Fault}입니다 — {expected}여야 합니다. 본문: {line.Text}");
        }

        // ============================================================================
        // (0) 기준선 — 정상 형상에서는 아무 결함도 보고하지 않는다(오탐 0)
        // ============================================================================
        [Test]
        public void HealthyWindowsShapeReportsNoFault()
        {
            var lines = Diagnose(Healthy());
            Assert.IsFalse(OverlayCompositionVerdict.HasFault(lines),
                "정상 출하 형상인데 결함이 보고됐습니다 — 오탐은 진단 로그를 통째로 신뢰 불가로 만듭니다. " +
                $"보고된 줄: {string.Join(" | ", lines.ConvertAll(l => l.ToString()))}");
        }

        // ============================================================================
        // (1) 증상 "번짐" — 백버퍼 != 창 클라이언트(표시 단계 리샘플)
        // ============================================================================
        [Test]
        public void BackBufferSmallerThanClientIsReportedAsBlur()
        {
            var s = Healthy();
            s.BackBufferWidth = 1920;    // 창은 2560인데 그림은 1920 -> 1.333배 확대
            s.BackBufferHeight = 1080;
            var lines = Diagnose(s);
            AssertFault(lines, "RESAMPLE", CompositionFault.Blur);
            Assert.IsTrue(Has(lines, "RESAMPLE", out var line) && line.Text.Contains("1.3333"),
                $"실제 배율이 로그에 남지 않으면 사람이 원인을 못 가릅니다. 본문: {line.Text}");
        }

        /// <summary>1픽셀 차이도 리샘플이다 — "거의 같으니 괜찮다"는 판정을 금지한다.</summary>
        [Test]
        public void OnePixelMismatchIsAlreadyResample()
        {
            var s = Healthy();
            s.ClientWidth = 2561f;
            AssertFault(Diagnose(s), "RESAMPLE", CompositionFault.Blur);
        }

        [Test]
        public void MatchingSizesAreReportedAsClean()
        {
            var lines = Diagnose(Healthy());
            Assert.IsTrue(Has(lines, "RESAMPLE", out var line));
            Assert.AreEqual(CompositionFault.None, line.Fault);
        }

        // ============================================================================
        // (2) 증상 "번짐" — 전체화면 모드(오버레이는 항상 Windowed여야 한다)
        // ============================================================================
        [Test]
        public void NonWindowedFullScreenModeIsReportedAsBlur()
        {
            var s = Healthy();
            s.FullScreenMode = (int)FullScreenMode.FullScreenWindow;
            AssertFault(Diagnose(s), "FULLSCREEN-MODE", CompositionFault.Blur);
        }

        // ============================================================================
        // (3) 증상 "번짐" — 비정수 캔버스 배율(디스플레이 배율 125%/150%)
        // ============================================================================
        [Test]
        public void FractionalCanvasScaleIsReportedAsGlyphBlur()
        {
            var s = Healthy();
            s.CanvasScaleFactor = 1.5f;      // Windows 150%
            s.UiDensityScale = 1.5f;
            AssertFault(Diagnose(s), "GLYPH-SCALE", CompositionFault.Blur);
        }

        [Test]
        public void RetinaLikeIntegerScaleIsNotReported()
        {
            var s = Healthy();
            s.CanvasScaleFactor = 2f;
            s.UiDensityScale = 2f;
            Assert.IsTrue(Has(Diagnose(s), "GLYPH-SCALE", out var line));
            Assert.AreEqual(CompositionFault.None, line.Fault,
                "정수 배율(Retina 2x / Windows 200%)은 글리프 리샘플이 없습니다 — 여기서 경고가 뜨면 오탐입니다.");
        }

        // ============================================================================
        // (4) 증상 "겹침" — 카메라 clear가 알파 마스크로 성립하지 않는 경우 3종
        // ============================================================================
        [Test]
        public void NonZeroClearAlphaIsReportedAsSeeThrough()
        {
            var s = Healthy();
            s.CameraBackground = new Color(0f, 0f, 0f, 1f);
            AssertFault(Diagnose(s), "CLEAR-ALPHA", CompositionFault.SeeThrough);
        }

        /// <summary>씬 에셋의 초기값(0.94 회색)이 런타임 교정 없이 남은 상태 — 프리멀티플라이드
        /// 합성에서는 화면 전체가 밝게 <b>더해진다</b>. 두 증상을 동시에 만든다.</summary>
        [Test]
        public void UncorrectedBrightClearRgbIsReportedAsBoth()
        {
            var s = Healthy();
            s.CameraBackground = new Color(0.94f, 0.94f, 0.94f, 0f);
            AssertFault(Diagnose(s), "CLEAR-RGB", CompositionFault.Both);
        }

        [Test]
        public void NonSolidColorClearIsReportedAsSeeThrough()
        {
            var s = Healthy();
            s.CameraClearFlags = (int)CameraClearFlags.Skybox;
            AssertFault(Diagnose(s), "CLEAR-FLAGS", CompositionFault.SeeThrough);
        }

        // ============================================================================
        // (5) 증상 "겹침" — 합성 경로가 둘로 갈린 상태(레이어드 + DWM 확장 프레임)
        //     이 라운드에서 코드로 특정한 macOS/Windows 갈림길이다.
        // ============================================================================
        [Test]
        public void LayeredStyleOnTopOfDwmGlassIsReported()
        {
            var s = Healthy();
            s.HasLayeredStyle = true;        // SetClickThrough(TRUE)의 부작용
            var lines = Diagnose(s);
            AssertFault(lines, "LAYERED+DWM", CompositionFault.Both);
            // 레이어드인데 속성이 없다는 두 번째 줄까지 함께 떠야 한다(원인 사슬이 로그에 다 남는다).
            AssertFault(lines, "LAYERED-NOATTR", CompositionFault.SeeThrough);
        }

        [Test]
        public void LayeredWindowWithPartialAlphaIsReportedSeparately()
        {
            var s = Healthy();
            s.HasLayeredStyle = true;
            s.LayeredAttributesInEffect = true;
            s.LayeredAlphaByte = 200;
            var lines = Diagnose(s);
            AssertFault(lines, "LAYERED-ALPHA", CompositionFault.SeeThrough);
            Assert.IsFalse(Has(lines, "LAYERED-NOATTR", out _),
                "레이어드 속성이 실제로 걸려 있으면 '속성 없음' 줄은 뜨면 안 됩니다(상호 배타적 원인).");
        }

        [Test]
        public void NoLayeredStyleIsReportedAsCleanSinglePath()
        {
            Assert.IsTrue(Has(Diagnose(Healthy()), "LAYERED+DWM", out var line));
            Assert.AreEqual(CompositionFault.None, line.Fault);
        }

        // ============================================================================
        // (6) 합성 경로 전제 — ColorKey / DWM 꺼짐
        // ============================================================================
        [Test]
        public void ColorKeyTransparencyInvalidatesTheWholeAlphaPremise()
        {
            var s = Healthy();
            s.TransparentType = OverlayCompositionVerdict.TransparentTypeColorKey;
            AssertFault(Diagnose(s), "COLORKEY", CompositionFault.SeeThrough);
        }

        [Test]
        public void DwmCompositionOffIsReportedAsBoth()
        {
            var s = Healthy();
            s.DwmCompositionEnabled = false;
            AssertFault(Diagnose(s), "DWM-OFF", CompositionFault.Both);
        }

        // ============================================================================
        // (7) 관측 실패는 <b>결함으로 위장하지 않는다</b> — 정직한 보류
        // ============================================================================
        [Test]
        public void UnreadableOsStyleIsHeldNotGuessed()
        {
            var s = Healthy();
            s.OsStyleReadOk = false;
            s.HasLayeredStyle = false;
            var lines = Diagnose(s);
            Assert.IsTrue(Has(lines, "OS-STYLE", out var line));
            Assert.AreEqual(CompositionFault.None, line.Fault);
            Assert.IsFalse(Has(lines, "LAYERED+DWM", out _),
                "스타일을 못 읽었는데 레이어드 판정을 내리면 안 됩니다(추측 금지).");
        }

        [Test]
        public void MissingClientSizeHoldsResampleVerdict()
        {
            var s = Healthy();
            s.ClientWidth = 0f;
            s.ClientHeight = 0f;
            Assert.IsTrue(Has(Diagnose(s), "RESAMPLE", out var line));
            Assert.AreEqual(CompositionFault.None, line.Fault,
                "창 부착 전에는 리샘플을 판정할 근거가 없습니다 — 보류여야 합니다.");
        }

        // ============================================================================
        // (8) 지문(Signature) — 전이에서만 로그가 찍히는 계약
        // ============================================================================
        [Test]
        public void SignatureIsStableForTheSameObservation()
        {
            Assert.AreEqual(Healthy().Signature(), Healthy().Signature(),
                "같은 관측인데 지문이 달라지면 24시간 상주 앱이 매 2초마다 로그를 뱉습니다.");
        }

        [Test]
        public void SignatureChangesWhenAnyDiagnosticFieldChanges()
        {
            string baseline = Healthy().Signature();

            var mutations = new List<(string name, OverlayCompositionSnapshot s)>();
            var a = Healthy(); a.BackBufferWidth = 1920; mutations.Add(("백버퍼", a));
            var b = Healthy(); b.ClientWidth = 1920f; mutations.Add(("클라이언트", b));
            var c = Healthy(); c.WindowWidth = 1920f; mutations.Add(("창", c));
            var d = Healthy(); d.FullScreenMode = 1; mutations.Add(("전체화면모드", d));
            var e = Healthy(); e.CanvasScaleFactor = 1.5f; mutations.Add(("캔버스배율", e));
            var f = Healthy(); f.UiDensityScale = 1.25f; mutations.Add(("UI밀도", f));
            var g = Healthy(); g.AutoDpiScale = 1.33f; mutations.Add(("좌표배율", g));
            var h = Healthy(); h.TransparentType = 2; mutations.Add(("투명방식", h));
            var i = Healthy(); i.HasLayeredStyle = true; mutations.Add(("레이어드", i));
            var j = Healthy(); j.HasClickThroughStyle = false; mutations.Add(("클릭관통", j));
            var k = Healthy(); k.LayeredAttributesInEffect = true; k.LayeredAlphaByte = 255; mutations.Add(("레이어드속성", k));
            var l = Healthy(); l.DwmCompositionEnabled = false; mutations.Add(("DWM합성", l));
            var m = Healthy(); m.CameraClearFlags = 1; mutations.Add(("clearFlags", m));
            var n = Healthy(); n.CameraBackground = new Color(0.94f, 0.94f, 0.94f, 0f); mutations.Add(("배경RGB", n));
            var o = Healthy(); o.CameraBackground = new Color(0f, 0f, 0f, 1f); mutations.Add(("배경알파", o));
            var p = Healthy(); p.ActualMsaa = 1; mutations.Add(("실측MSAA", p));
            var q = Healthy(); q.RequestedMsaa = 8; mutations.Add(("요청MSAA", q));
            var r = Healthy(); r.UiSpriteFilterMode = (int)FilterMode.Point; mutations.Add(("스프라이트필터", r));
            var t = Healthy(); t.OsStyleReadOk = false; mutations.Add(("스타일실측", t));

            for (int idx = 0; idx < mutations.Count; idx++)
            {
                Assert.AreNotEqual(baseline, mutations[idx].s.Signature(),
                    $"'{mutations[idx].name}'이(가) 바뀌었는데 지문이 그대로입니다 — 그 전이는 실기 로그에 " +
                    "영원히 남지 않습니다(진단이 그 원인을 놓친다는 뜻).");
            }
        }

        // ============================================================================
        // (9) 실기 시나리오 복합 — 사용자 신고 형태 그대로
        // ============================================================================
        /// <summary>"겹쳐 보이고 텍스트도 다 번져 보임"을 만들 수 있는 최악 조합이 <b>두 증상 모두</b>로
        /// 분류되는지. 실기 로그를 받았을 때 사람이 어디부터 볼지 정하는 근거다.</summary>
        [Test]
        public void UserReportedCombinationClassifiesBothSymptoms()
        {
            var s = Healthy();
            s.BackBufferWidth = 1920; s.BackBufferHeight = 1080;   // 리샘플
            s.CanvasScaleFactor = 1.5f; s.UiDensityScale = 1.5f;   // 글리프 비정수 확대
            s.HasLayeredStyle = true;                              // 합성 경로 이중화
            var lines = Diagnose(s);

            bool blur = false, seeThrough = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Fault == CompositionFault.Blur || lines[i].Fault == CompositionFault.Both) blur = true;
                if (lines[i].Fault == CompositionFault.SeeThrough || lines[i].Fault == CompositionFault.Both) seeThrough = true;
            }
            Assert.IsTrue(blur, "번짐 원인이 하나도 분류되지 않았습니다.");
            Assert.IsTrue(seeThrough, "겹침 원인이 하나도 분류되지 않았습니다.");
        }
    }
}
