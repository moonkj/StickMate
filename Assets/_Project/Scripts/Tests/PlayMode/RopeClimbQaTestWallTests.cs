using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ QA 전용 밧줄등반 합성 시험벽(<see cref="FallbackPlatformWindowService"/> + 4번째 합성 핸들
    /// <see cref="FallbackPlatformWindowService.SyntheticRopeClimbTestWallHandle"/>) 회귀 테스트 —
    /// docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md §9-4-C의 "기존 Dock/안전망 합성 발판과 같은 방식" 처방.
    ///
    /// 검증 하네스는 Tests/PlayMode/DockSafetyNetSplitTests.cs와 완전히 동일한 관례를 따른다 — 실제
    /// 창이 하나도 없는 상태를 재현하는 빈 스텁을 감싸, FallbackPlatformWindowService가 <b>스스로
    /// 합성하는</b> 발판만 측정한다(좌표 변환에 실제 Camera/Screen.width·height가 필요해 PlayMode).
    ///
    /// 무엇을 잡으려는가:
    ///  (1) 스위치가 꺼져 있으면 시험벽은 <b>절대</b> 목록에 나타나지 않는다(원칙 3 관련 안전장치 —
    ///      릴리스 사용자에게는 이 발판 자체가 존재해서는 안 된다).
    ///  (2) Dock이 있으면 시험벽은 Dock과 <b>정확히 같은 가로 구간</b>에, Dock보다 <b>훨씬 높게</b>
    ///      생긴다(GroundSensor.TryFindClimbableWall이 "같은 탐색폭 안의 최고 높이"를 채택하므로,
    ///      이 배치만으로 Dock 대신 채택된다 — 새 경계를 만들 필요가 없다는 설계 근거를 값으로 못박는다).
    ///  (3) Dock이 없으면(자동 숨김 등) 스스로 화면 중앙에 가짜 경계를 만들어 안전망도 그 자리에서
    ///      갈라지고, 시험벽도 그 경계에 선다("실제 데스크톱 창 배치와 무관하게" 요구의 핵심).
    ///  (4) 스위치를 끄면 다음 폴링부터 즉시 사라진다(캐시에 남지 않는다).
    /// </summary>
    public sealed class RopeClimbQaTestWallTests
    {
        private const string LogPrefix = "[밧줄등반QA-테스트]";

        private Camera _camera;
        private GameObject _cameraGo;
        private StickConfig _config;
        private Vector2 _savedOrigin;

        private sealed class EmptyWindowService : IPlatformWindowService
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
            _cameraGo = new GameObject("RopeClimbQaTestWallCamera", typeof(Camera));
            _camera = _cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.desktopDpiScale = 1f;
            _config.groundSnapTolerance = 20f;
            _config.dockFootholdWidthFraction = 0.65f;
            _config.dockFootholdThicknessPoints = 75f;

            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
        }

        [TearDown]
        public void TearDown()
        {
            // ★ 정적 QA 스위치는 프로세스 전역이다 — 여기서 되돌리지 않으면 이 파일 뒤에 도는 다른
            // 스위트(RopeClimbTests 등)가 시험벽이 켜진 채로 조용히 오염된다.
            RopeClimbQaOverride.SetWallTestOverride(null);
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private FallbackPlatformWindowService NewService() => new FallbackPlatformWindowService(new EmptyWindowService(), _config);

        private static bool Has(IReadOnlyList<PlatformFoothold> list, long handle)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Handle == handle) return true;
            return false;
        }

        private static PlatformFoothold Find(IReadOnlyList<PlatformFoothold> list, long handle)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Handle == handle) return list[i];
            Assert.Fail($"{LogPrefix} 핸들 {handle} 발판을 찾지 못했습니다(열거된 발판 {list.Count}개).");
            return default;
        }

        // ============================================================================
        // (1) 스위치가 꺼져 있으면 시험벽은 절대 나타나지 않는다
        // ============================================================================

        [Test]
        public void 스위치가_꺼져있으면_시험벽이_전혀_없다()
        {
            RopeClimbQaOverride.SetWallTestOverride(false);

            var service = NewService();
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();

            Assert.AreEqual(3, footholds.Count,
                $"{LogPrefix} 스위치가 꺼진 기본 상태(Dock 있음)는 Dock 1 + 안전망 2조각 = 3개여야 합니다. 실제={footholds.Count}");
            Assert.IsFalse(Has(footholds, FallbackPlatformWindowService.SyntheticRopeClimbTestWallHandle),
                $"{LogPrefix} STICKMATE_QA_ROPE_CLIMB_TEST_WALL이 꺼져 있는데 시험벽이 발판 목록에 나타났습니다 — " +
                "릴리스 사용자에게 나가면 안 되는 발판입니다.");
        }

        // ============================================================================
        // (2) Dock이 있으면 시험벽은 Dock과 같은 가로구간에, 훨씬 높게 생긴다
        // ============================================================================

        [Test]
        public void Dock이_있으면_시험벽은_Dock과_같은_가로구간에_훨씬_높게_생긴다()
        {
            RopeClimbQaOverride.SetWallTestOverride(true);

            var service = NewService();
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();

            Assert.AreEqual(4, footholds.Count,
                $"{LogPrefix} Dock 1 + 안전망 2조각 + 시험벽 1 = 4개여야 합니다. 실제={footholds.Count}");

            PlatformFoothold dock = Find(footholds, FallbackPlatformWindowService.DockFootholdHandle);
            PlatformFoothold wall = Find(footholds, FallbackPlatformWindowService.SyntheticRopeClimbTestWallHandle);

            Debug.Log($"{LogPrefix} Dock=({dock.ScreenRect.x:F1}~{dock.ScreenRect.xMax:F1}, top={dock.ScreenRect.y:F1}) " +
                $"시험벽=({wall.ScreenRect.x:F1}~{wall.ScreenRect.xMax:F1}, top={wall.ScreenRect.y:F1})");

            float epsilon = 0.5f;
            Assert.AreEqual(dock.ScreenRect.x, wall.ScreenRect.x, epsilon,
                $"{LogPrefix} 시험벽의 왼쪽 끝이 Dock의 왼쪽 끝과 같아야 합니다 — GroundSensor.TryFindClimbableWall이 " +
                "같은 경계에서 이 시험벽을 후보로 인정하려면 X 구간이 Dock과 일치해야 합니다.");
            Assert.AreEqual(dock.ScreenRect.xMax, wall.ScreenRect.xMax, epsilon,
                $"{LogPrefix} 시험벽의 오른쪽 끝이 Dock의 오른쪽 끝과 같아야 합니다.");

            // OS 좌표는 y가 아래로 갈수록 증가한다 — "훨씬 높다"는 OS y가 훨씬 작다는 뜻이다.
            Assert.Less(wall.ScreenRect.y, dock.ScreenRect.y,
                $"{LogPrefix} 시험벽 상단(OS y={wall.ScreenRect.y:F1})이 Dock 상단(OS y={dock.ScreenRect.y:F1})보다 " +
                "높지(작지) 않습니다 — GroundSensor의 '같은 탐색폭 안 최고높이' 채택 규칙에서 이 시험벽이 지지 않으려면 " +
                "Dock보다 확실히 높아야 합니다.");
        }

        // ============================================================================
        // (3) Dock이 없으면 스스로 화면 중앙에 가짜 경계를 만들고, 안전망도 그 자리에서 갈라진다
        // ============================================================================

        [Test]
        public void Dock이_없으면_스스로_화면중앙에_가짜경계를_만들고_안전망도_갈라진다()
        {
            _config.dockFootholdWidthFraction = 0f; // Dock 비활성(자동 숨김 등 재현).
            RopeClimbQaOverride.SetWallTestOverride(true);

            var service = NewService();
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();

            // Dock 자체는 없다(DockFootholdHandle 부재) — 하지만 안전망은 "전체 폭 한 조각"이 아니라
            // 시험벽이 만든 가짜 경계에서 갈라진 두 조각이어야 한다(그래야 배회 AI가 실제로 경계에 도달한다).
            Assert.IsFalse(Has(footholds, FallbackPlatformWindowService.DockFootholdHandle),
                $"{LogPrefix} 전제 실패 — Dock이 비활성인데도 Dock 발판이 열거됐습니다.");
            Assert.IsTrue(Has(footholds, FallbackPlatformWindowService.SyntheticFootholdHandle),
                $"{LogPrefix} 안전망 왼쪽 조각이 없습니다 — Dock 없이도 갈라져야 합니다.");
            Assert.IsTrue(Has(footholds, FallbackPlatformWindowService.SyntheticFootholdHandleRight),
                $"{LogPrefix} 안전망 오른쪽 조각이 없습니다 — 가짜 경계로 갈라지지 않고 예전처럼 전체 폭 " +
                "한 조각으로 남았다면, 배회 AI가 그 경계에 아예 도달하지 못해 이 QA 스위치가 무의미해집니다.");
            Assert.IsTrue(Has(footholds, FallbackPlatformWindowService.SyntheticRopeClimbTestWallHandle),
                $"{LogPrefix} Dock이 없는데 시험벽 자체가 생기지 않았습니다.");

            PlatformFoothold left = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandle);
            PlatformFoothold right = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandleRight);
            PlatformFoothold wall = Find(footholds, FallbackPlatformWindowService.SyntheticRopeClimbTestWallHandle);

            Debug.Log($"{LogPrefix} 안전망왼쪽=({left.ScreenRect.x:F1}~{left.ScreenRect.xMax:F1}) " +
                $"안전망오른쪽=({right.ScreenRect.x:F1}~{right.ScreenRect.xMax:F1}) " +
                $"시험벽=({wall.ScreenRect.x:F1}~{wall.ScreenRect.xMax:F1}, top={wall.ScreenRect.y:F1})");

            // 단일 소스 계약(Dock/안전망과 같은 어법) — 가짜 틈의 좌/우 끝이 시험벽의 좌/우 끝과 정확히 같다.
            float epsilon = 0.5f;
            Assert.AreEqual(left.ScreenRect.xMax, wall.ScreenRect.x, epsilon,
                $"{LogPrefix} 왼쪽 안전망 조각의 오른쪽 끝이 시험벽의 왼쪽 끝과 만나지 않습니다(틈/겹침).");
            Assert.AreEqual(wall.ScreenRect.xMax, right.ScreenRect.x, epsilon,
                $"{LogPrefix} 시험벽의 오른쪽 끝이 오른쪽 안전망 조각의 왼쪽 끝과 만나지 않습니다(틈/겹침).");

            // ★ 2026-09-07 3차 — 시험벽 상단은 "화면 절대 좌표"가 아니라 "지금 서 있는 바닥(안전망
            // 상단)에서 화면 높이의 일정 비율만큼(단, 작은 화면에서는 절대 하한만큼) 위"로 정의된다
            // (실기 검증 중 발견한 결함 수정, FallbackPlatformWindowService.AppendBottomSafetyNet 문서
            // 참고 — 절대좌표 기준이었을 때는 실측 기본 배율(0.75)에서 ropeClimbMaxHeights 상한을
            // 넘겨 밧줄등반이 한 번도 발동하지 못했다). 프로덕션 상수를 숫자로 다시 베끼지 않고
            // 그대로 참조해 기대값을 계산한다(CLAUDE.md 관례).
            float screenH = (Screen.height > 0 ? Screen.height : 1080f) * _config.desktopDpiScale;
            float expectedAboveGround = Mathf.Max(
                screenH * FallbackPlatformWindowService.RopeClimbTestWallAboveGroundHeightFraction,
                FallbackPlatformWindowService.RopeClimbTestWallMinimumAboveGroundOsPoints);
            float expectedWallTop = left.ScreenRect.y - expectedAboveGround;
            Assert.AreEqual(expectedWallTop, wall.ScreenRect.y, 0.5f,
                $"{LogPrefix} 시험벽 상단이 '안전망 상단 - max(화면높이×비율, 절대하한)' 공식과 " +
                "어긋납니다 — 지면 기준 상대 높이 계산이 깨졌을 수 있습니다.");
            Assert.Less(wall.ScreenRect.y, left.ScreenRect.y,
                $"{LogPrefix} 시험벽이 안전망 바닥보다 높지(작은 OS y) 않습니다.");
        }

        // ============================================================================
        // (4) 끄면 다음 폴링부터 즉시 사라진다
        // ============================================================================

        [Test]
        public void 끄면_다음_호출부터_즉시_사라진다()
        {
            RopeClimbQaOverride.SetWallTestOverride(true);
            var service = NewService();
            Assert.IsTrue(Has(service.EnumerateFootholds(), FallbackPlatformWindowService.SyntheticRopeClimbTestWallHandle),
                $"{LogPrefix} 전제 실패 — 켠 직후에도 시험벽이 없습니다.");

            RopeClimbQaOverride.SetWallTestOverride(false);
            Assert.IsFalse(Has(service.EnumerateFootholds(), FallbackPlatformWindowService.SyntheticRopeClimbTestWallHandle),
                $"{LogPrefix} 스위치를 껐는데도 다음 호출에서 시험벽이 남아 있습니다 — 캐시에 태워졌다는 뜻입니다.");
        }
    }
}
