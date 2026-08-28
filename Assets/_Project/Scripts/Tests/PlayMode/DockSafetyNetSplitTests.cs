using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 사용자 신고 "처음엔 독위에서 잘다니다가 좀 다니다 보면 다시 독과 겹쳐서 걸음"(2026-08-29)의
    /// 재발 방지 테스트.
    ///
    /// ============================================================================
    /// 무엇이 버그였나 (리더 진단, 확정)
    /// ============================================================================
    /// 발판 구성이 두 장이었다 — Dock 발판(화면 바닥-75pt, 가로 정중앙 65%)과 바닥 안전망(화면 최하단,
    /// **가로 화면 전체 폭**). 그래서:
    ///   (1) 캐릭터가 Dock 위를 정상적으로 걷는다.            ← 사용자가 "처음엔 잘 다닌다"고 한 구간
    ///   (2) Dock 가로 끝을 벗어나 정상 낙하한다.
    ///   (3) 화면 최하단 안전망에 착지한다.                    ← 여기까지 의도된 동작
    ///   (4) 안전망이 전체 폭이라 **계속 걸어서 다시 Dock 가로 구간 안쪽으로 들어간다.**
    ///   (5) 그 자리에서 캐릭터는 화면 최하단인데 그 위 75pt를 Dock이 차지 -> **겹쳐 보인다.**
    /// (4)가 이 테스트가 잠그는 지점이다. 안전망을 Dock 좌/우 바깥 **두 조각**으로 쪼개면 (4)의 X 구간
    /// 자체가 사라져, Dock 가로 범위 안의 바닥은 오직 Dock 상단 하나만 남는다.
    ///
    /// ============================================================================
    /// 왜 내부 서비스를 빈 스텁으로 두는가
    /// ============================================================================
    /// 이 테스트의 검증 대상은 FallbackPlatformWindowService가 **스스로 합성하는** 발판(Dock + 안전망
    /// 조각)의 기하다. NullPlatformWindowService를 감싸면 그쪽의 더미 발판(에디터 전용, 화면 전체 폭)이
    /// 같은 높이에 하나 더 깔려 "Dock 밑을 걷는" 경로가 그 더미 때문에 다시 열려 측정이 오염된다 —
    /// 실제 배포(macOS)에서 내부 서비스는 MacWindowService이고 화면 최하단 전체 폭 창을 만들지 않는다.
    /// 그래서 "실제 창이 하나도 없는 상태"를 정확히 재현하는 빈 스텁을 쓴다.
    ///
    /// 왜 EditMode가 아니라 PlayMode인가: 좌표 변환에 실제 Camera와 Screen.width/height가 필요하고
    /// (ScreenCoordinateConverter), RAGDOLL 물리 검증은 실제 FixedUpdate 루프가 있어야 한다.
    /// </summary>
    public sealed class DockSafetyNetSplitTests
    {
        private const string LogPrefix = "[DOCK-SPLIT-TEST]";

        /// <summary>실제 배포 환경 실측 화면(리더 보고 기준값 OS y≈907/942가 나온 그 화면).</summary>
        private const float ReferenceScreenWidthPoints = 1512f;
        private const float ReferenceScreenHeightPoints = NullPlatformWindowService.ReferenceScreenHeightPoints; // 982

        private Camera _camera;
        private GameObject _cameraGo;
        private StickConfig _config;
        private Vector2 _savedOrigin;

        /// <summary>실제 창을 하나도 못 찾는 상황(모든 창 최소화 등)을 재현하는 내부 서비스 스텁.</summary>
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
            _cameraGo = new GameObject("DockSplitTestCamera", typeof(Camera));
            _camera = _cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;             // 씬(Main.unity)과 동일한 프레이밍.
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.desktopDpiScale = 1f;
            _config.groundSnapTolerance = 20f;
            // DefaultStickConfig.asset과 동일한 기본값을 명시적으로 못박아 둔다(자산이 바뀌어도 이
            // 테스트의 기하 계약은 그대로 유지되도록).
            _config.dockFootholdWidthFraction = 0.65f;
            _config.dockFootholdThicknessPoints = 75f;

            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
        }

        [TearDown]
        public void TearDown()
        {
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private FallbackPlatformWindowService NewService() => new FallbackPlatformWindowService(new EmptyWindowService(), _config);

        private static PlatformFoothold Find(IReadOnlyList<PlatformFoothold> list, long handle)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Handle == handle) return list[i];
            }
            Assert.Fail($"{LogPrefix} 핸들 {handle} 발판을 찾지 못했습니다(열거된 발판 {list.Count}개).");
            return default;
        }

        private static bool Has(IReadOnlyList<PlatformFoothold> list, long handle)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Handle == handle) return true;
            return false;
        }

        // ====================================================================
        // (1) 기하 계약 — 안전망은 Dock 구간을 잘라낸 두 조각이고, 틈도 겹침도 없다
        // ====================================================================

        [Test]
        public void 안전망은_Dock_가로구간을_정확히_잘라낸_두_조각이다()
        {
            // 오버레이 창 원점이 (0,0)일 때와 화면 안쪽으로 밀려 있을 때 둘 다 확인한다 — 이 합성
            // 발판들은 "우리 창 기준"이라 원점만큼 통째로 평행이동해야 하고, 그 평행이동이 Dock 구간
            // 계산과 안전망 분할 계산에 **똑같이** 적용되지 않으면 그 차이만큼 틈/겹침이 생긴다.
            foreach (Vector2 origin in new[] { Vector2.zero, new Vector2(13f, 27f) })
            {
                ScreenCoordinateConverter.OverlayOriginOsScreen = origin;

                var service = NewService();
                IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();

                float screenW = (Screen.width > 0 ? Screen.width : 1920f) * _config.desktopDpiScale;
                float screenH = (Screen.height > 0 ? Screen.height : 1080f) * _config.desktopDpiScale;
                float epsilon = Mathf.Max(0.01f, screenW * 0.0005f);

                Assert.AreEqual(3, footholds.Count,
                    $"{LogPrefix} 실제 창이 없을 때 합성 발판은 Dock 1 + 안전망 2조각 = 3개여야 합니다. 실제={footholds.Count}");

                PlatformFoothold dock = Find(footholds, FallbackPlatformWindowService.DockFootholdHandle);
                PlatformFoothold left = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandle);
                PlatformFoothold right = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandleRight);

                Debug.Log($"{LogPrefix} origin={origin} screen={screenW:F0}x{screenH:F0} " +
                    $"dock=({dock.ScreenRect.x:F1}~{dock.ScreenRect.xMax:F1}, top={dock.ScreenRect.y:F1}) " +
                    $"안전망왼쪽=({left.ScreenRect.x:F1}~{left.ScreenRect.xMax:F1}, top={left.ScreenRect.y:F1}) " +
                    $"안전망오른쪽=({right.ScreenRect.x:F1}~{right.ScreenRect.xMax:F1}, top={right.ScreenRect.y:F1})");

                // 단일 소스 계약: 안전망 구멍의 좌/우 끝 == Dock 발판의 좌/우 끝(틈도 겹침도 0).
                Assert.AreEqual(dock.ScreenRect.x, left.ScreenRect.xMax, epsilon,
                    $"{LogPrefix} 왼쪽 안전망 조각의 오른쪽 끝이 Dock 왼쪽 끝과 정확히 만나야 합니다 " +
                    "(어긋나면 틈=낙하고착 또는 겹침=Dock밑보행). TryGetDockSpanOsScreen 단일 소스 확인.");
                Assert.AreEqual(dock.ScreenRect.xMax, right.ScreenRect.x, epsilon,
                    $"{LogPrefix} 오른쪽 안전망 조각의 왼쪽 끝이 Dock 오른쪽 끝과 정확히 만나야 합니다.");

                // 두 조각을 합치면 Dock 구간을 뺀 화면 전체를 덮는다(바깥쪽 끝은 화면 끝).
                Assert.AreEqual(origin.x, left.ScreenRect.x, epsilon, $"{LogPrefix} 왼쪽 조각은 화면 왼쪽 끝에서 시작해야 합니다.");
                Assert.AreEqual(origin.x + screenW, right.ScreenRect.xMax, epsilon, $"{LogPrefix} 오른쪽 조각은 화면 오른쪽 끝에서 끝나야 합니다.");

                // 두 조각의 높이(상단 Y/두께)는 동일해야 한다 — 하나의 "화면 최하단 바닥"을 쪼갠 것이므로.
                Assert.AreEqual(left.ScreenRect.y, right.ScreenRect.y, 0.01f, $"{LogPrefix} 두 안전망 조각의 상단 Y가 같아야 합니다.");
                Assert.AreEqual(left.ScreenRect.height, right.ScreenRect.height, 0.01f, $"{LogPrefix} 두 안전망 조각의 두께가 같아야 합니다.");

                // 연쇄 확인(리더 지시 3항): 안전망 높이는 NullPlatformWindowService의 단일 소스 상수에서
                // 파생되어야 한다(Editor/SceneBootstrapper.ComputeGroundTopWorldY도 같은 상수를 쓴다).
                float expectedNetTop = origin.y + screenH - screenH * NullPlatformWindowService.DummyFootholdHeightFraction;
                Assert.AreEqual(expectedNetTop, left.ScreenRect.y, 0.05f,
                    $"{LogPrefix} 안전망 상단이 DummyFootholdHeightFraction에서 파생되지 않았습니다 — 지면 콜라이더/스폰/테스트 연쇄가 끊깁니다.");

                // Dock 상단은 "화면 바닥 - 두께"(실측 근거값). 참조 화면(1512x982)에서 OS y=907.
                Assert.AreEqual(origin.y + screenH - _config.dockFootholdThicknessPoints, dock.ScreenRect.y, 0.05f,
                    $"{LogPrefix} Dock 상단은 화면 바닥에서 dockFootholdThicknessPoints만큼 위여야 합니다.");

                // ★★ 이 버그의 본질: Dock 가로 구간 안쪽 어디에도 안전망이 있으면 안 된다.
                float dockCenterX = dock.ScreenRect.center.x;
                Assert.IsFalse(left.ScreenRect.x <= dockCenterX && dockCenterX <= left.ScreenRect.xMax,
                    $"{LogPrefix} 회귀: 왼쪽 안전망 조각이 Dock 중앙까지 덮고 있습니다 — 캐릭터가 걸어서 Dock 밑으로 들어갑니다.");
                Assert.IsFalse(right.ScreenRect.x <= dockCenterX && dockCenterX <= right.ScreenRect.xMax,
                    $"{LogPrefix} 회귀: 오른쪽 안전망 조각이 Dock 중앙까지 덮고 있습니다 — 캐릭터가 걸어서 Dock 밑으로 들어갑니다.");
            }
        }

        [Test]
        public void Dock이_비활성이면_안전망은_예전처럼_전체폭_한조각이다()
        {
            // Dock 자동 숨김/좌우 배치 사용자를 위한 탈출구(StickConfig 문서). 잘라낼 Dock이 없으면
            // 겹칠 일도 없으므로 예전 거동(전체 폭 한 장)이 그대로 유지되어야 한다.
            _config.dockFootholdWidthFraction = 0f;
            var service = NewService();
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();

            float screenW = (Screen.width > 0 ? Screen.width : 1920f) * _config.desktopDpiScale;
            Assert.AreEqual(1, footholds.Count, $"{LogPrefix} Dock 비활성 시 합성 발판은 안전망 1개뿐이어야 합니다.");
            Assert.IsFalse(Has(footholds, FallbackPlatformWindowService.DockFootholdHandle), $"{LogPrefix} Dock 발판이 남아 있으면 안 됩니다.");
            PlatformFoothold net = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandle);
            Assert.AreEqual(screenW, net.ScreenRect.width, Mathf.Max(0.01f, screenW * 0.0005f),
                $"{LogPrefix} Dock이 없으면 안전망은 화면 전체 폭이어야 합니다.");
            Debug.Log($"{LogPrefix} Dock 비활성 — 안전망 1조각 폭={net.ScreenRect.width:F1}(화면 폭={screenW:F1})");
        }

        // ====================================================================
        // (2) 실측 — Dock 안/밖 각 x좌표에서 캐릭터가 서게 되는 높이
        // ====================================================================

        [Test]
        public void Dock_안팎_x좌표별로_서게되는_바닥높이가_갈린다()
        {
            var service = NewService();
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();
            PlatformFoothold dock = Find(footholds, FallbackPlatformWindowService.DockFootholdHandle);
            PlatformFoothold left = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandle);

            float screenW = (Screen.width > 0 ? Screen.width : 1920f) * _config.desktopDpiScale;
            float screenH = (Screen.height > 0 ? Screen.height : 1080f) * _config.desktopDpiScale;
            float dockTopOs = dock.ScreenRect.y;
            float netTopOs = left.ScreenRect.y;
            // 참조 화면(1512x982) 환산 — 리더 보고의 기준값(Dock 상단 907 / 화면 최하단 942)과 직접 비교.
            float toRef = ReferenceScreenHeightPoints / screenH;

            Debug.Log($"{LogPrefix} === 실측 시작 === 화면={screenW:F0}x{screenH:F0}, " +
                $"Dock상단 OS y={dockTopOs:F1}(참조환산 {dockTopOs * toRef:F1}), " +
                $"화면최하단 OS y={netTopOs:F1}(참조환산 {netTopOs * toRef:F1}), " +
                $"Dock 가로={dock.ScreenRect.x:F1}~{dock.ScreenRect.xMax:F1}");

            // 화면 폭 대비 비율로 샘플링한다(해상도 무관). Dock 구간은 0.175~0.825(정중앙 65%).
            float[] fractions = { 0.02f, 0.10f, 0.16f, 0.17f, 0.20f, 0.35f, 0.50f, 0.65f, 0.80f, 0.83f, 0.90f, 0.98f };
            int insideCount = 0, outsideCount = 0;

            foreach (float f in fractions)
            {
                float osX = f * screenW;
                // 그 x에서 딛을 수 있는 가장 높은 표면(= 실제로 캐릭터가 서게 되는 높이).
                Vector3 probeWorld = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(osX, netTopOs), 10f, _config);
                bool found = GroundSensor.TryGetSurfaceWorldY(_camera, probeWorld, footholds, _config, out float surfaceWorldY);
                Assert.IsTrue(found, $"{LogPrefix} x비율 {f:F2}에서 딛을 표면이 하나도 없습니다 — 안전망 분할에 틈이 생겼습니다(낙하 고착).");

                Vector2 surfaceOs = ScreenCoordinateConverter.WorldToOsScreen(_camera, new Vector2(probeWorld.x, surfaceWorldY), _config, out _);
                bool insideDock = osX >= dock.ScreenRect.x && osX <= dock.ScreenRect.xMax;
                float expectedOsY = insideDock ? dockTopOs : netTopOs;

                Debug.Log($"{LogPrefix} x비율={f:F2} (OS x={osX:F1}, {(insideDock ? "Dock 안" : "Dock 밖")}) " +
                    $"-> 착지 OS y={surfaceOs.y:F1} (참조환산 {surfaceOs.y * toRef:F1}, 기대 {expectedOsY * toRef:F1}) world y={surfaceWorldY:F3}");

                Assert.AreEqual(expectedOsY, surfaceOs.y, 0.5f,
                    $"{LogPrefix} x비율 {f:F2}에서 서는 높이가 기대와 다릅니다 — " +
                    (insideDock ? "Dock 안이면 Dock 상단이어야 합니다(안전망이 Dock 밑까지 뚫고 들어왔을 수 있음)."
                                : "Dock 밖이면 화면 최하단이어야 합니다(Dock 폭 추정이 잘못됐을 수 있음)."));

                if (insideDock) insideCount++; else outsideCount++;
            }

            Assert.Greater(insideCount, 0, $"{LogPrefix} Dock 안쪽 샘플이 하나도 없습니다 — 샘플 비율표를 확인하세요.");
            Assert.Greater(outsideCount, 0, $"{LogPrefix} Dock 바깥 샘플이 하나도 없습니다 — 샘플 비율표를 확인하세요.");
            Assert.Greater(netTopOs, dockTopOs,
                $"{LogPrefix} 화면 최하단 안전망은 Dock 상단보다 아래(OS y가 더 큼)여야 합니다.");
        }

        // ====================================================================
        // (3) 회귀 잠금 — 안전망 위를 걸어서 Dock 밑으로 들어갈 수 없다
        // ====================================================================

        [Test]
        public void 안전망_위를_걸어서_Dock_밑으로_들어갈_수_없다()
        {
            var service = NewService();
            IReadOnlyList<PlatformFoothold> footholds = service.EnumerateFootholds();
            PlatformFoothold dock = Find(footholds, FallbackPlatformWindowService.DockFootholdHandle);
            PlatformFoothold leftNet = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandle);
            PlatformFoothold rightNet = Find(footholds, FallbackPlatformWindowService.SyntheticFootholdHandleRight);

            float screenW = (Screen.width > 0 ? Screen.width : 1920f) * _config.desktopDpiScale;
            float netTopOs = leftNet.ScreenRect.y;
            float step = Mathf.Max(1f, screenW / 200f);

            // 왼쪽 조각 위에서 오른쪽으로 "걸어간다". Dock 왼쪽 끝을 넘는 순간 접지가 끊겨야 한다
            // (= AutoWanderController가 그 지점을 발판 경계로 인식해 되돌아선다).
            float lastGroundedOsX = float.NaN;
            for (float osX = leftNet.ScreenRect.x + step; osX < dock.ScreenRect.center.x; osX += step)
            {
                Vector3 footWorld = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(osX, netTopOs), 10f, _config);
                GroundSensor.GroundInfo info = GroundSensor.Sense(_camera, footWorld, footholds, _config,
                    preferredHandle: FallbackPlatformWindowService.SyntheticFootholdHandle);

                if (osX <= dock.ScreenRect.x - step)
                {
                    Assert.IsTrue(info.Grounded,
                        $"{LogPrefix} Dock 왼쪽 바깥(OS x={osX:F1})에서는 화면 최하단 바닥에 접지해야 합니다 — 안전망 왼쪽 조각이 너무 짧습니다.");
                    lastGroundedOsX = osX;
                }
                else if (osX >= dock.ScreenRect.x + step)
                {
                    Assert.IsFalse(info.Grounded,
                        $"{LogPrefix} ★회귀★ OS x={osX:F1}은 Dock 가로 구간 **안**인데 화면 최하단에서 접지했습니다 — " +
                        "안전망이 Dock 밑까지 이어져 있어 캐릭터가 Dock과 겹쳐 걷게 됩니다(사용자 신고 증상 그 자체).");
                }
            }
            Debug.Log($"{LogPrefix} 왼쪽 조각 보행 한계 OS x={lastGroundedOsX:F1} (Dock 왼쪽 끝={dock.ScreenRect.x:F1})");
            Assert.IsFalse(float.IsNaN(lastGroundedOsX), $"{LogPrefix} 왼쪽 조각 위에서 접지한 지점이 하나도 없습니다.");

            // 오른쪽 조각도 대칭으로 확인(왼쪽만 고치고 오른쪽을 빠뜨리는 실수 방지).
            for (float osX = rightNet.ScreenRect.xMax - step; osX > dock.ScreenRect.center.x; osX -= step)
            {
                Vector3 footWorld = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(osX, netTopOs), 10f, _config);
                GroundSensor.GroundInfo info = GroundSensor.Sense(_camera, footWorld, footholds, _config,
                    preferredHandle: FallbackPlatformWindowService.SyntheticFootholdHandleRight);

                if (osX >= dock.ScreenRect.xMax + step) Assert.IsTrue(info.Grounded,
                    $"{LogPrefix} Dock 오른쪽 바깥(OS x={osX:F1})에서는 화면 최하단 바닥에 접지해야 합니다.");
                else if (osX <= dock.ScreenRect.xMax - step) Assert.IsFalse(info.Grounded,
                    $"{LogPrefix} ★회귀★ OS x={osX:F1}은 Dock 가로 구간 안인데 화면 최하단에서 접지했습니다(오른쪽 조각).");
            }

            // 두 조각의 핸들은 반드시 달라야 한다 — 같으면 발판 고착(preferredHandle)이 두 조각을 하나로
            // 취급해 "Dock을 건너뛰어 반대편 조각으로 이어 걷는" 경로가 열린다.
            Assert.AreNotEqual(FallbackPlatformWindowService.SyntheticFootholdHandle,
                FallbackPlatformWindowService.SyntheticFootholdHandleRight,
                $"{LogPrefix} 두 안전망 조각의 핸들이 같으면 경계 판정이 반대편 조각의 것으로 잘못 잡힙니다.");

            // Dock 위에 서 있을 때의 경계는 Dock 자신의 좌우 끝이어야 한다(안전망 조각 경계가 아님).
            Vector3 dockFootWorld = ScreenCoordinateConverter.OsScreenToWorld(_camera,
                new Vector2(dock.ScreenRect.center.x, dock.ScreenRect.y), 10f, _config);
            GroundSensor.GroundInfo dockInfo = GroundSensor.Sense(_camera, dockFootWorld, footholds, _config,
                preferredHandle: FallbackPlatformWindowService.DockFootholdHandle);
            Assert.IsTrue(dockInfo.Grounded, $"{LogPrefix} Dock 중앙 상단에서는 Dock에 접지해야 합니다.");
            Vector2 leftEdgeOs = ScreenCoordinateConverter.WorldToOsScreen(_camera,
                new Vector2(dockInfo.CurrentFootholdLeftWorldX, dockFootWorld.y), _config, out _);
            Vector2 rightEdgeOs = ScreenCoordinateConverter.WorldToOsScreen(_camera,
                new Vector2(dockInfo.CurrentFootholdRightWorldX, dockFootWorld.y), _config, out _);
            Debug.Log($"{LogPrefix} Dock 위 보행 경계 OS x={leftEdgeOs.x:F1}~{rightEdgeOs.x:F1} (Dock={dock.ScreenRect.x:F1}~{dock.ScreenRect.xMax:F1})");
            Assert.AreEqual(dock.ScreenRect.x, leftEdgeOs.x, 1f, $"{LogPrefix} Dock 위 보행 왼쪽 경계가 Dock 왼쪽 끝과 달라졌습니다.");
            Assert.AreEqual(dock.ScreenRect.xMax, rightEdgeOs.x, 1f, $"{LogPrefix} Dock 위 보행 오른쪽 경계가 Dock 오른쪽 끝과 달라졌습니다.");
        }

        [Test]
        public void Dock_폭_추정은_실측보다_좁아_틀리는_방향이_안전하다()
        {
            // 리더 지시 4항. Dock 실제 폭은 화면 기록 권한 없이는 알 수 없어 정중앙 65%로 추정한다.
            // 실측(1069/1512 = 0.707)보다 **좁게** 잡아야, 틀릴 때 캐릭터가 "Dock 없는 허공에 떠 있는"
            // 쪽이 아니라 "Dock 옆 바닥에 서는" 쪽으로 틀린다.
            const float measuredFraction = 1069f / ReferenceScreenWidthPoints; // ≈ 0.707
            Assert.Less(_config.dockFootholdWidthFraction, measuredFraction,
                $"{LogPrefix} dockFootholdWidthFraction({_config.dockFootholdWidthFraction:F3})이 실측 폭 비율" +
                $"({measuredFraction:F3}) 이상이면, Dock이 없는 자리에서 캐릭터가 Dock 높이에 떠 있게 됩니다.");
            Debug.Log($"{LogPrefix} Dock 폭 추정={_config.dockFootholdWidthFraction:F3} < 실측={measuredFraction:F3} (안전한 방향)");
        }

        // ====================================================================
        // (4) 물리 바닥은 전체 폭 유지 — Dock 구간에서 RAGDOLL이 바닥을 뚫지 않는다
        // ====================================================================

        /// <summary>
        /// 리더 지시 1항의 실측 검증. 논리적 안전망은 Dock 구간에 구멍이 뚫렸지만, 씬의 PhysicsGround
        /// (Editor/SceneBootstrapper.CreateGroundCollider)는 **전체 폭 그대로**여야 한다 — 그렇지 않으면
        /// RAGDOLL(순수 물리)이 Dock 가로 구간(=화면 정중앙, 캐릭터가 대부분 머무는 곳)에서 바닥을
        /// 통과해 화면 밖으로 사라진다.
        /// </summary>
        [UnityTest]
        public IEnumerator Dock_구간에서_RAGDOLL이_물리바닥을_뚫지_않는다()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 씬에서 PhysicsGround를 찾지 못했습니다 — Main.unity 배선 확인.");
            var box = ground.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(box, $"{LogPrefix} PhysicsGround에 BoxCollider2D가 없습니다.");

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"{LogPrefix} 씬에 메인 카메라가 없습니다.");

            // 물리 바닥의 상단 월드 Y가 논리적 안전망 높이(단일 소스 상수에서 파생)와 일치하는지 —
            // 이 연쇄가 끊기면 "물리적으로는 떠받치는데 논리적으로는 접지 못 함"이 화면 전체에서 발생한다.
            float expectedGroundTop = cam.transform.position.y
                - cam.orthographicSize * (1f - 2f * NullPlatformWindowService.DummyFootholdHeightFraction);
            Assert.AreEqual(expectedGroundTop, box.bounds.max.y, 0.05f,
                $"{LogPrefix} PhysicsGround 상단 Y가 DummyFootholdHeightFraction 파생값과 어긋납니다 " +
                "(Editor/SceneBootstrapper.ComputeGroundTopWorldY 연쇄 확인 — 씬 재생성 --force 필요할 수 있음).");

            // 전체 폭 유지: 카메라 뷰포트 좌우 끝(= Dock 구간을 포함한 화면 전체)을 모두 덮어야 한다.
            float viewHalfWidth = cam.orthographicSize * cam.aspect;
            Debug.Log($"{LogPrefix} PhysicsGround bounds x={box.bounds.min.x:F1}~{box.bounds.max.x:F1}, top={box.bounds.max.y:F3}, " +
                $"뷰포트 반폭={viewHalfWidth:F2}, 기대 상단={expectedGroundTop:F3}");
            Assert.Less(box.bounds.min.x, -viewHalfWidth, $"{LogPrefix} 물리 바닥이 화면 왼쪽 끝까지 닿지 않습니다.");
            Assert.Greater(box.bounds.max.x, viewHalfWidth, $"{LogPrefix} 물리 바닥이 화면 오른쪽 끝까지 닿지 않습니다.");
            // Dock 가로 구간(정중앙 65%)에 구멍이 없어야 한다 — 논리적 안전망과 달리 여기는 통짜다.
            Assert.IsTrue(box.bounds.min.x < 0f && box.bounds.max.x > 0f,
                $"{LogPrefix} ★회귀★ 물리 바닥이 Dock 가로 구간(화면 정중앙)에서 끊겨 있습니다 — RAGDOLL이 바닥을 뚫습니다.");

            // 실제 RAGDOLL로 확인: Dock 중앙(world x=0)에서 강제 랙돌 후 바닥 아래로 가라앉지 않아야 한다.
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            yield return new WaitForSeconds(3f); // 초기 낙하/안착.

            var body = agent.Blackboard.Body;
            Assert.IsNotNull(body, $"{LogPrefix} 캐릭터 Rigidbody2D를 찾지 못했습니다.");
            body.position = new Vector2(0f, expectedGroundTop + 0.5f);   // Dock 가로 구간의 정중앙.
            body.linearVelocity = Vector2.zero;
            agent.transform.position = new Vector3(0f, expectedGroundTop + 0.5f, agent.transform.position.z);
            float threshold = agent.Blackboard.Config != null ? agent.Blackboard.Config.ragdollForceThreshold : 8f;
            agent.ReportExternalImpact(threshold * 5f);
            Debug.Log($"{LogPrefix} Dock 중앙(x=0)에서 RAGDOLL 강제 진입 — 관찰 시작.");

            float lowestY = float.MaxValue;
            for (int i = 0; i < 24; i++) // 6초 관찰.
            {
                yield return new WaitForSeconds(0.25f);
                float y = agent.transform.position.y;
                if (y < lowestY) lowestY = y;
                Assert.Greater(y, expectedGroundTop - 3f,
                    $"{LogPrefix} ★회귀★ RAGDOLL이 Dock 구간에서 물리 바닥을 뚫고 내려갔습니다 " +
                    $"(y={y:F3}, 바닥 상단={expectedGroundTop:F3}). PhysicsGround는 전체 폭을 유지해야 합니다.");
            }
            Debug.Log($"{LogPrefix} RAGDOLL 최저 y={lowestY:F3} (물리 바닥 상단={expectedGroundTop:F3}) — 관통 없음.");
        }
    }
}
