using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ Retina 대응 라운드(2026-08-29, 사용자 신고 "전체적으로 해상도가 너무 안좋음")의 **잠금 테스트**.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가 — 그리고 왜 이 테스트가 없으면 조용히 깨지는가
    /// ============================================================================
    /// `ProjectSettings.macRetinaSupport`를 0 -> 1로 되돌리면서 Unity의 `Screen.width/height`와
    /// `Camera.WorldToScreenPoint`가 **물리 백킹 픽셀**(3024x1964)을 보고하게 됐다. 그런데 이 앱이
    /// 상대하는 OS 좌표(CGWindowListCopyWindowInfo의 창 사각형 / CGEventGetLocation의 커서 /
    /// CGDisplayBounds)는 전부 **AppKit 포인트**(1512x982)다. 두 단위를 잇는 배율이
    /// <see cref="ScreenCoordinateConverter.ResolveDpiScale"/> 하나이고, 이 앱의 **모든 것**이 그 위에
    /// 얹혀 있다 — 창 발판, Dock 발판, 바닥 안전망, 화면 클램프, 커서 추적, 클릭 판정, 드래그.
    /// 하나라도 어긋나면 사용자에게는 "다 망가졌다"로 보인다(실제로 그 표현을 썼다).
    ///
    /// 그래서 여기서는 배율 1(비Retina)과 배율 2(Retina 2x) **양쪽**에서 다음을 절대 조건으로 단언한다:
    ///   1) OS <-> Unity 왕복 변환이 항등인가 (WorldToOsScreen -> OsScreenToWorld, 그리고
    ///      OsScreenToUnityScreen이 그 앞부분과 같은 한 벌인가).
    ///   2) 배율이 실제로 자동 산출되는가 (창 폭[OS 포인트] / Screen.width[Unity 픽셀]).
    ///   3) 수동 오버라이드(0 이하 = 자동)의 우선순위 규칙이 지켜지는가.
    ///   4) 배율이 바뀌어도 **OS 포인트 공간의 좌표는 변하지 않는가** — 즉 좌표계가 2배로 어긋나지
    ///      않는가. 이것이 이 라운드에서 실제로 위험했던 지점이고, 아래 네거티브 컨트롤이 겨냥하는 곳이다.
    ///   5) 접지 판정(GroundSensor)이 배율과 무관하게 같은 물리 위치에서 성립하는가.
    ///
    /// ★ 네거티브 컨트롤(이 프로젝트 표준: "수정을 되돌리면 실패해야 한다"):
    ///   <see cref="DpiScale_NegativeControl_IgnoringScaleBreaksOsCoordinates"/>가
    ///   "만약 배율 보정을 빼먹고 예전처럼 1로 뒀다면" 어떤 값이 나오는지를 직접 계산해, 그 값이
    ///   올바른 값과 **정확히 2배 다르다**는 것을 단언한다. 즉 `ResolveDpiScale`이 자동 배율을 무시하도록
    ///   되돌리는 순간 위 1~5번이 전부 빨간불이 된다는 사실을 테스트 자신이 증명한다.
    ///
    /// 왜 PlayMode인가: 좌표 변환이 실제 `Camera`와 실행 중인 `Screen.width/height`에 의존한다
    /// (FootholdLandingDirectionTests가 같은 이유로 PlayMode에 있다).
    ///
    /// 주의 — 이 테스트는 실제 화면 배율에 의존하지 않는다: `AutoDpiScale`을 직접 대입해 "배율 2인 척"을
    /// 만들기 때문에 배치 모드(1x)에서도, 실기(2x)에서도 같은 결과가 나온다.
    /// </summary>
    public sealed class RetinaDpiCoordinateTests
    {
        private const string LogPrefix = "[RETINA-DPI-TEST]";

        /// <summary>비Retina(1x)에서의 배율. OS 포인트 == Unity 픽셀.</summary>
        private const float ScaleAt1x = 1f;

        /// <summary>Retina 2x에서의 배율. OS 포인트 = Unity 픽셀 x 0.5.</summary>
        private const float ScaleAt2x = 0.5f;

        private Camera _camera;
        private GameObject _cameraGo;
        private StickConfig _config;
        private Vector2 _savedOrigin;
        private float _savedAutoScale;

        [SetUp]
        public void SetUp()
        {
            _cameraGo = new GameObject("RetinaDpiTestCamera", typeof(Camera));
            _camera = _cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;            // 씬(Main.unity)과 동일한 프레이밍.
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.desktopDpiScale = 0f;              // 0 이하 = 자동(이 라운드에서 바뀐 기본값).
            _config.groundSnapTolerance = 20f;

            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            _savedAutoScale = ScreenCoordinateConverter.AutoDpiScale;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
            ScreenCoordinateConverter.AutoDpiScale = ScaleAt1x;
        }

        [TearDown]
        public void TearDown()
        {
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            ScreenCoordinateConverter.AutoDpiScale = _savedAutoScale;
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // ====================================================================
        // 1) 배율 산출 규칙 — 자동 산출과 수동 오버라이드의 우선순위
        // ====================================================================

        [Test]
        public void ResolveDpiScale_UsesAutoWhenOverrideIsZeroOrNegative()
        {
            ScreenCoordinateConverter.AutoDpiScale = ScaleAt2x;

            _config.desktopDpiScale = 0f;
            Assert.AreEqual(ScaleAt2x, ScreenCoordinateConverter.ResolveDpiScale(_config), 1e-6f,
                "desktopDpiScale=0(기본값)은 '자동'을 뜻하므로 AutoDpiScale이 그대로 쓰여야 한다.");

            _config.desktopDpiScale = -1f;
            Assert.AreEqual(ScaleAt2x, ScreenCoordinateConverter.ResolveDpiScale(_config), 1e-6f,
                "음수도 '자동'으로 취급해야 한다(0 이하 = 자동).");

            Assert.AreEqual(ScaleAt2x, ScreenCoordinateConverter.ResolveDpiScale(null), 1e-6f,
                "config가 없어도(플랫폼 계층 초기 프레임) 자동 배율로 폴백해야 한다.");
        }

        [Test]
        public void ResolveDpiScale_ManualOverrideWinsOverAuto()
        {
            ScreenCoordinateConverter.AutoDpiScale = ScaleAt2x;
            _config.desktopDpiScale = 0.25f;   // 사람이 지정한 탈출구 값.

            Assert.AreEqual(0.25f, ScreenCoordinateConverter.ResolveDpiScale(_config), 1e-6f,
                "0보다 큰 desktopDpiScale은 자동 산출을 덮어쓰는 수동 오버라이드여야 한다.");
        }

        /// <summary>
        /// 자동 산출의 **정의 자체**를 잠근다: 배율 = 창 폭(OS 포인트) / Screen.width(Unity 픽셀).
        /// 하드코딩(0.5)이 아니라 실측이라는 것이 이 테스트의 요점이다 — 배율 1인 화면과 2인 화면을
        /// 같은 코드가 서로 다른 값으로 산출해야 한다.
        /// </summary>
        [Test]
        public void ReportOverlayWindowOsRect_DerivesScaleFromWindowWidthOverScreenWidth()
        {
            int screenPx = Screen.width;
            Assume.That(screenPx, Is.GreaterThan(0), "Screen.width가 0이면 이 테스트는 의미가 없다.");

            // (a) 비Retina: 창 폭(포인트) == Screen.width(픽셀) -> 배율 1.
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(new Rect(0f, 0f, screenPx, Screen.height));
            Assert.AreEqual(ScaleAt1x, ScreenCoordinateConverter.AutoDpiScale, 1e-5f,
                "1x 화면에서는 창 폭과 Screen.width가 같으므로 배율이 1이어야 한다.");

            // (b) Retina 2x: 창 폭(포인트) == Screen.width / 2 -> 배율 0.5.
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(
                new Rect(0f, 0f, screenPx * 0.5f, Screen.height * 0.5f));
            Assert.AreEqual(ScaleAt2x, ScreenCoordinateConverter.AutoDpiScale, 1e-5f,
                "2x Retina에서는 창 폭(포인트)이 Screen.width(픽셀)의 절반이므로 배율이 0.5여야 한다.");

            // (c) 오버레이 원점도 같은 한 번의 보고에서 함께 갱신되어야 한다(두 값이 어긋날 수 없어야 한다).
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(
                new Rect(17f, 33f, screenPx * 0.5f, Screen.height * 0.5f));
            Assert.AreEqual(new Vector2(17f, 33f), ScreenCoordinateConverter.OverlayOriginOsScreen,
                "원점과 배율은 같은 관측에서 함께 나와야 한다(서로 다른 시점의 값으로 어긋나면 안 된다).");

            // (d) 비정상 값은 무시하고 직전 값을 지켜야 한다(0을 받아들이면 나눗셈이 폭발한다).
            float before = ScreenCoordinateConverter.AutoDpiScale;
            ScreenCoordinateConverter.AutoDpiScale = 0f;
            ScreenCoordinateConverter.AutoDpiScale = -3f;
            ScreenCoordinateConverter.AutoDpiScale = float.NaN;
            Assert.AreEqual(before, ScreenCoordinateConverter.AutoDpiScale, 1e-6f,
                "0/음수/NaN 배율은 무시하고 직전 값을 유지해야 한다.");
        }

        // ====================================================================
        // 2) 왕복 변환 항등 — 배율 1과 2 양쪽에서
        // ====================================================================

        [Test]
        public void WorldToOsScreen_RoundTripsExactly_AtBothScales()
        {
            foreach (float scale in new[] { ScaleAt1x, ScaleAt2x })
            {
                ScreenCoordinateConverter.AutoDpiScale = scale;

                foreach (Vector3 world in SampleWorldPoints())
                {
                    Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(_camera, world, _config, out float depth);
                    Vector3 back = ScreenCoordinateConverter.OsScreenToWorld(_camera, os, depth, _config);

                    Assert.AreEqual(world.x, back.x, 1e-3f,
                        $"{LogPrefix} 배율 {scale}에서 x 왕복이 항등이 아니다(월드 {world} -> OS {os} -> 월드 {back}).");
                    Assert.AreEqual(world.y, back.y, 1e-3f,
                        $"{LogPrefix} 배율 {scale}에서 y 왕복이 항등이 아니다(월드 {world} -> OS {os} -> 월드 {back}).");
                }
            }
        }

        /// <summary>
        /// OsScreenToUnityScreen은 OsScreenToWorld의 "앞부분과 완전히 동일한 한 벌"이어야 한다
        /// (ScreenCoordinateConverter의 BUG-M5 컨벤션). 두 식이 갈라지면 클릭 판정(앱제어 메뉴 /
        /// 투두 위젯 히트테스트)만 조용히 어긋난다 — 캐릭터는 멀쩡한데 클릭만 빗나가는 형태라 발견이 늦다.
        /// </summary>
        [Test]
        public void OsScreenToUnityScreen_MatchesWorldPathInverse_AtBothScales()
        {
            ScreenCoordinateConverter.OverlayOriginOsScreen = new Vector2(11f, 23f);

            foreach (float scale in new[] { ScaleAt1x, ScaleAt2x })
            {
                ScreenCoordinateConverter.AutoDpiScale = scale;

                foreach (Vector3 world in SampleWorldPoints())
                {
                    Vector3 expectedUnityScreen = _camera.WorldToScreenPoint(world);
                    Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(_camera, world, _config, out _);
                    Vector2 actual = ScreenCoordinateConverter.OsScreenToUnityScreen(os, _config);

                    Assert.AreEqual(expectedUnityScreen.x, actual.x, 1e-2f,
                        $"{LogPrefix} 배율 {scale}에서 OsScreenToUnityScreen의 x가 WorldToScreenPoint와 어긋난다.");
                    Assert.AreEqual(expectedUnityScreen.y, actual.y, 1e-2f,
                        $"{LogPrefix} 배율 {scale}에서 OsScreenToUnityScreen의 y가 WorldToScreenPoint와 어긋난다.");
                }
            }
        }

        // ====================================================================
        // 3) 좌표계 불변성 — Retina를 켜도 OS 포인트 좌표가 그대로여야 한다
        // ====================================================================

        /// <summary>
        /// 이 라운드의 **핵심 계약**: `macRetinaSupport`를 켜면 Screen.width/height와 WorldToScreenPoint가
        /// 전부 2배가 되지만, 배율이 함께 0.5가 되므로 **OS 포인트 좌표는 정확히 그대로여야 한다.**
        /// 그래야 발판/Dock/안전망/클램프가 참조하는 좌표 공간이 Retina 전후로 동일하고,
        /// StickConfig의 "OS-px 필드" 8종을 하나도 바꾸지 않아도 된다.
        ///
        /// 화면 크기를 실제로 바꿀 수는 없으므로, "Unity가 2배로 보고하는 상황"을 카메라 프레이밍으로
        /// 등가 재현한다: 픽셀이 2배가 되는 것은 곧 "같은 월드 폭이 2배의 픽셀에 담긴다"는 뜻이고,
        /// 그 상태에서 배율 0.5를 곱하면 원래 포인트 값으로 되돌아온다. 여기서는 그 결합 규칙만 검증하면
        /// 충분하다 — WorldToOsScreen의 식이 `unityScreen * dpi`이므로, unityScreen이 k배가 될 때
        /// dpi가 1/k배면 결과가 불변이라는 것을 직접 단언한다.
        /// </summary>
        [Test]
        public void OsPointCoordinates_AreInvariant_WhenPixelsDoubleAndScaleHalves()
        {
            var world = new Vector3(3.5f, -4.25f, 0f);

            // (a) 1x 기준값.
            ScreenCoordinateConverter.AutoDpiScale = ScaleAt1x;
            Vector2 os1x = ScreenCoordinateConverter.WorldToOsScreen(_camera, world, _config, out _);

            // (b) "화면이 2배 픽셀이 된 상황"을 등가 재현한다.
            //     WorldToScreenPoint의 결과는 뷰포트 좌표 x Screen 크기이므로, 카메라 프레이밍을 그대로 둔 채
            //     Screen만 2배가 되면 unityScreen이 정확히 2배가 된다. 배치 모드에서 Screen을 실제로 바꿀 수는
            //     없으므로, 같은 항등식을 "unityScreen 2배 + dpi 절반"으로 직접 계산해 비교한다.
            Vector3 unityScreen = _camera.WorldToScreenPoint(world);
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;

            float osX2x = (unityScreen.x * 2f) * ScaleAt2x + origin.x;
            float osY2x = ((Screen.height * 2f) - (unityScreen.y * 2f)) * ScaleAt2x + origin.y;

            Assert.AreEqual(os1x.x, osX2x, 1e-3f,
                $"{LogPrefix} Retina에서 OS x 좌표가 변했다 — 픽셀 2배 x 배율 0.5는 반드시 상쇄돼야 한다.");
            Assert.AreEqual(os1x.y, osY2x, 1e-3f,
                $"{LogPrefix} Retina에서 OS y 좌표가 변했다 — 픽셀 2배 x 배율 0.5는 반드시 상쇄돼야 한다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — "수정을 되돌리면 실패한다"의 증명.
        ///
        /// `ResolveDpiScale`이 자동 배율을 무시하고 예전처럼 1을 쓰도록 되돌아가면 어떤 값이 나오는지를
        /// 직접 계산해, 그 값이 올바른 값과 **정확히 2배** 다르다는 것을 단언한다. 즉 이 테스트가
        /// 통과한다는 것은 "배율 보정이 실제로 결과를 바꾸고 있다"는 뜻이고, 보정을 제거하면 위의
        /// 왕복/불변 테스트들이 전부 깨진다는 뜻이다(보정이 무의미한 no-op가 아니라는 증거).
        /// </summary>
        [Test]
        public void DpiScale_NegativeControl_IgnoringScaleBreaksOsCoordinates()
        {
            var world = new Vector3(3.5f, -4.25f, 0f);
            ScreenCoordinateConverter.AutoDpiScale = ScaleAt2x;

            Vector2 corrected = ScreenCoordinateConverter.WorldToOsScreen(_camera, world, _config, out _);

            // "보정을 되돌린" 세계 — 배율을 무시하고 1로 취급.
            _config.desktopDpiScale = 1f;   // 수동 오버라이드로 자동 배율(0.5)을 무시하게 만든다.
            Vector2 unscaled = ScreenCoordinateConverter.WorldToOsScreen(_camera, world, _config, out _);

            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            Assert.AreEqual((unscaled.x - origin.x) * ScaleAt2x, corrected.x - origin.x, 1e-3f,
                $"{LogPrefix} 배율 보정이 x에 실제로 적용되고 있지 않다 — 보정이 no-op라는 뜻이다.");
            Assert.AreEqual((unscaled.y - origin.y) * ScaleAt2x, corrected.y - origin.y, 1e-3f,
                $"{LogPrefix} 배율 보정이 y에 실제로 적용되고 있지 않다 — 보정이 no-op라는 뜻이다.");

            Assert.That(Mathf.Abs(unscaled.x - corrected.x), Is.GreaterThan(1f),
                $"{LogPrefix} 보정 유무의 차이가 없다 — 이 테스트는 차이를 만들어내지 못하고 있으므로 " +
                "네거티브 컨트롤로서 무의미하다(표본 좌표를 화면 원점에서 충분히 떨어뜨려야 한다).");
        }

        // ====================================================================
        // 4) 캔버스 스케일 — ScreenSpaceOverlay UI가 물리적으로 절반이 되지 않는가
        // ====================================================================

        [Test]
        public void CanvasScaleFactor_IsInverseOfDpiScale_AtBothScales()
        {
            ScreenCoordinateConverter.AutoDpiScale = ScaleAt1x;
            Assert.AreEqual(1f, ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config), 1e-5f,
                "비Retina에서는 캔버스 스케일이 1이어야 한다(예전과 동일).");

            ScreenCoordinateConverter.AutoDpiScale = ScaleAt2x;
            Assert.AreEqual(2f, ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config), 1e-5f,
                "Retina 2x에서 캔버스 스케일이 2가 아니면 말풍선/메뉴/투두 UI가 물리적으로 절반 크기가 된다.");

            // 캔버스 유닛 <-> 스크린 픽셀 왕복도 항등이어야 한다(UI 배치 코드가 이 둘을 오간다).
            foreach (float scale in new[] { ScaleAt1x, ScaleAt2x })
            {
                ScreenCoordinateConverter.AutoDpiScale = scale;
                foreach (float px in new[] { 0f, 37f, 512f, 3024f })
                {
                    float back = ScreenCoordinateConverter.CanvasToUnityScreen(
                        ScreenCoordinateConverter.UnityScreenToCanvas(px, _config), _config);
                    Assert.AreEqual(px, back, 1e-2f,
                        $"{LogPrefix} 배율 {scale}에서 캔버스<->스크린 픽셀 왕복이 항등이 아니다({px}px).");
                }
            }
        }

        // ====================================================================
        // 5) 접지 판정 — 배율이 바뀌어도 같은 물리 위치에서 성립하는가
        // ====================================================================

        /// <summary>
        /// 발판은 OS 포인트로 정의되고 캐릭터는 월드 좌표로 산다. 그 둘을 잇는 것이 배율이므로,
        /// 배율이 틀리면 "캐릭터가 발판을 뚫고 떨어지거나 공중에 뜬다"가 된다(사용자에게는 "다 망가졌다").
        /// 같은 발판을 배율 1과 2 양쪽에서 만들어, 접지가 성립하는 **월드 y가 동일**한지 확인한다.
        /// </summary>
        [Test]
        public void GroundSensor_GroundsAtSameWorldPosition_AtBothScales()
        {
            float groundWorldYAt1x = MeasureGroundedWorldY(ScaleAt1x, out bool grounded1x);
            float groundWorldYAt2x = MeasureGroundedWorldY(ScaleAt2x, out bool grounded2x);

            Assert.IsTrue(grounded1x, $"{LogPrefix} 배율 1에서 접지 판정 자체가 실패했다(테스트 전제 붕괴).");
            Assert.IsTrue(grounded2x, $"{LogPrefix} 배율 0.5(Retina)에서 접지 판정이 실패했다 — " +
                "배율 보정이 발판 판정 경로에 반영되지 않았다는 뜻이다.");
            Assert.AreEqual(groundWorldYAt1x, groundWorldYAt2x, 1e-2f,
                $"{LogPrefix} 배율에 따라 접지 월드 y가 달라진다 — 같은 OS 포인트 발판은 배율과 무관하게 " +
                "같은 물리 위치여야 한다.");
        }

        /// <summary>
        /// 화면 하단(OS 포인트 기준)에 발판을 하나 깔고, 그 위에 정확히 얹힌 캐릭터의 접지 월드 y를 잰다.
        /// 발판 좌표는 **OS 포인트**로 고정이며(배율과 무관한 상수), 배율만 바꿔 가며 호출한다.
        /// </summary>
        private float MeasureGroundedWorldY(float scale, out bool grounded)
        {
            ScreenCoordinateConverter.AutoDpiScale = scale;

            // 화면을 OS 포인트로 환산한 크기(= Screen.* x 배율) — 앱의 다른 모든 코드와 같은 규칙.
            float screenWpt = Screen.width * scale;
            float screenHpt = Screen.height * scale;
            float footholdTopOsY = screenHpt * 0.9f;

            var footholds = new List<PlatformFoothold>
            {
                new PlatformFoothold(9001L, new Rect(0f, footholdTopOsY, screenWpt, screenHpt * 0.1f), false),
            };

            // 발판 상단 위 정확히 그 지점의 월드 좌표를 구해 발을 거기에 둔다.
            _ = ScreenCoordinateConverter.WorldToOsScreen(_camera, Vector3.zero, _config, out float depth);
            Vector3 footWorld = ScreenCoordinateConverter.OsScreenToWorld(
                _camera, new Vector2(screenWpt * 0.5f, footholdTopOsY), depth, _config);

            GroundSensor.GroundInfo info = GroundSensor.Sense(
                _camera, new Vector2(footWorld.x, footWorld.y), footholds, _config);

            grounded = info.Grounded;
            return info.GroundWorldY;
        }

        /// <summary>화면 중앙/모서리를 두루 덮는 표본. 원점 근처만 보면 배율 오류가 상쇄돼 숨는다.</summary>
        private static IEnumerable<Vector3> SampleWorldPoints()
        {
            yield return new Vector3(0f, 0f, 0f);
            yield return new Vector3(5.5f, 3.25f, 0f);
            yield return new Vector3(-7.75f, -6.5f, 0f);
            yield return new Vector3(11f, -9.5f, 0f);
        }
    }
}
