using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 사용자 신고 "마우스로 끌었는데 갑자기 다른 창 위로 올라감"(2026-08-28)의 재발 방지 테스트.
    ///
    /// 이 라운드에 발견된 원인은 두 가지였고, 둘 다 "캐릭터가 **위쪽에 있는 창으로 끌어올려진다**"는
    /// 같은 증상을 낸다. 각각에 대응하는 검증을 여기에 둔다:
    ///
    /// (원인 1, 주범) DragThrowState.FollowCursor()의 지면 소프트 클램프가 "지면"을
    ///   GroundSensor.TryGetSurfaceWorldY(= 그 x에서 **가장 높은** 창 상단)로 물었다. 클램프 식은
    ///   `if (desired.y &lt; ground) desired.y = ground;` 라 **한 방향(위로)으로만** 작동하므로, 커서
    ///   x가 화면 위쪽 창의 가로 범위에 걸치기만 하면 화면 아래에서 끌던 캐릭터가 매 프레임 그 창
    ///   상단으로 끌어올려졌다. -> GroundSensor.TryGetFloorWorldY(가장 낮은 표면)로 교체.
    ///
    /// (원인 2, 던진 직후) FallState의 2순위 착지 판정(허용오차 밴드 + 유예)에 방향 개념이 없어서,
    ///   캐릭터를 위로 던지면 상승 중에 창 상단선 밴드에 들어가 그대로 "착지"할 수 있었다.
    ///   1순위인 스윕 교차 판정(GroundSensor.TryFindLandingCrossing)에는 원래부터 방향 조건이 있었고,
    ///   그 사실을 이 테스트가 잠그며(회귀 시 즉시 빨간불), FallState 쪽 새 가드는 런타임 실측으로 확인했다.
    ///
    /// 왜 EditMode가 아니라 PlayMode인가: GroundSensor는 순수 static이지만 좌표 변환에 실제 Camera가
    /// 필요하고(ScreenCoordinateConverter), 카메라의 픽셀/유닛 환산은 Screen.width/height에 의존한다.
    /// 실행 중인 플레이어의 실제 화면 크기 위에서 검증하는 편이 씬의 실제 조건과 일치한다.
    /// </summary>
    public sealed class FootholdLandingDirectionTests
    {
        private const string LogPrefix = "[LANDING-DIR-TEST]";

        private Camera _camera;
        private GameObject _cameraGo;
        private StickConfig _config;
        private Vector2 _savedOrigin;

        [SetUp]
        public void SetUp()
        {
            _cameraGo = new GameObject("LandingDirTestCamera", typeof(Camera));
            _camera = _cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;             // 씬(Main.unity)과 동일한 프레이밍.
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.desktopDpiScale = 1f;
            _config.groundSnapTolerance = 20f;

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

        /// <summary>
        /// 화면 위쪽의 "높은 창"(OS y = 화면 높이의 16%)과 화면 아래쪽의 "안전망"(OS y = 92%)을
        /// 가로 전체에 깔아 실제 데스크톱 상황을 흉내 낸다. 이 배치가 곧 사용자가 겪은 상황이다 —
        /// 화면 위에 Finder 창이 하나 떠 있고, 캐릭터는 화면 아래쪽에서 끌려다니는 상황.
        /// </summary>
        private List<PlatformFoothold> BuildTwoLayerFootholds(out float highTopOs, out float lowTopOs)
        {
            float screenW = Screen.width;
            float screenH = Screen.height;
            highTopOs = screenH * 0.16f;
            lowTopOs = screenH * 0.92f;
            return new List<PlatformFoothold>
            {
                new PlatformFoothold(4242L, new Rect(0f, highTopOs, screenW, screenH * 0.4f), true),
                new PlatformFoothold(-1L, new Rect(0f, lowTopOs, screenW, screenH * 0.08f), false),
            };
        }

        // ============================================================================
        // 원인 1 — 드래그 지면 클램프가 캐릭터를 위쪽 창으로 끌어올리지 않는다
        // ============================================================================

        [Test]
        public void FloorProbeReturnsLowestSurfaceSoDragNeverLiftsCharacterUp()
        {
            List<PlatformFoothold> footholds = BuildTwoLayerFootholds(out float highTopOs, out float lowTopOs);

            // 캐릭터를 화면 아래쪽(안전망 바로 위)에서 끌고 있다고 가정한 좌표.
            Vector3 draggedWorld = ScreenCoordinateConverter.OsScreenToWorld(
                _camera, new Vector2(Screen.width * 0.5f, lowTopOs - 4f), 10f, _config);
            var dragged = new Vector2(draggedWorld.x, draggedWorld.y);

            Assert.IsTrue(GroundSensor.TryGetSurfaceWorldY(_camera, dragged, footholds, _config, out float highestY),
                $"{LogPrefix} 테스트 전제 실패 — 가장 높은 표면을 찾지 못했습니다.");
            Assert.IsTrue(GroundSensor.TryGetFloorWorldY(_camera, dragged, footholds, _config, out float floorY),
                $"{LogPrefix} TryGetFloorWorldY가 바닥을 찾지 못했습니다.");

            Debug.Log($"{LogPrefix} 드래그 위치 월드Y={dragged.y:F3} / 가장 높은 표면(예전 클램프 기준)={highestY:F3} / " +
                $"바닥(새 클램프 기준)={floorY:F3} — 창 상단 OS y: 높은창={highTopOs:F1}, 안전망={lowTopOs:F1}");

            // 회귀의 본질: 예전 기준(가장 높은 표면)은 드래그 위치보다 **위**에 있어서 클램프가 캐릭터를
            // 그리로 끌어올렸다. 이 관계 자체를 명시적으로 잠가둔다 — 이게 성립하지 않으면 아래 검증이
            // 애초에 이 버그를 재현하지 못하는 무의미한 테스트가 된다.
            Assert.Greater(highestY, dragged.y,
                $"{LogPrefix} 테스트 전제 실패 — 위쪽 창이 드래그 위치보다 위에 있어야 회귀를 재현합니다.");

            // 실제 검증: 새 기준(바닥)은 드래그 위치보다 아래여야 한다 = 클램프가 발동하지 않는다
            // = 캐릭터가 끌어올려지지 않는다.
            Assert.LessOrEqual(floorY, dragged.y,
                $"{LogPrefix} 회귀! 바닥 기준({floorY:F3})이 드래그 위치({dragged.y:F3})보다 위에 있어 " +
                "소프트 클램프가 캐릭터를 위로 끌어올립니다 — 사용자 신고 '갑자기 다른 창 위로 올라감'의 재발입니다.");

            // 그리고 진짜 바닥 밑으로 내려갔을 때는 여전히 되돌려야 한다(클램프의 원래 목적 보존).
            var belowFloor = new Vector2(dragged.x, floorY - 3f);
            Assert.IsTrue(GroundSensor.TryGetFloorWorldY(_camera, belowFloor, footholds, _config, out float floorY2));
            Assert.Greater(floorY2, belowFloor.y,
                $"{LogPrefix} 바닥 밑으로 끌어내렸을 때는 클램프가 여전히 작동해야 합니다(Fall 영구 고착 방지).");
        }

        // ============================================================================
        // 원인 2 — 발판 상단선을 "아래에서 위로" 통과할 때는 착지하지 않는다
        // ============================================================================

        [Test]
        public void UpwardPassThroughFootholdTopDoesNotLand()
        {
            List<PlatformFoothold> footholds = BuildTwoLayerFootholds(out float highTopOs, out _);
            float x = Screen.width * 0.5f;

            // 위쪽 창 상단선을 **아래에서 위로** 통과하는 한 프레임(위로 던져 올린 상황).
            Vector3 prev = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(x, highTopOs + 30f), 10f, _config);
            Vector3 curr = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(x, highTopOs - 30f), 10f, _config);

            bool landed = GroundSensor.TryFindLandingCrossing(_camera, prev, curr, footholds, _config,
                out long handle, out float landingY);

            Debug.Log($"{LogPrefix} 상승 통과 — 이전 월드Y={prev.y:F3} -> 현재 월드Y={curr.y:F3} " +
                $"(OS {highTopOs + 30f:F1} -> {highTopOs - 30f:F1}, 창 상단 OS {highTopOs:F1}) => 착지={landed}");

            Assert.IsFalse(landed,
                $"{LogPrefix} 회귀! 캐릭터가 발판 상단선을 아래에서 위로 뚫고 올라가면서 착지했습니다 " +
                $"(핸들={handle}, 착지Y={landingY:F3}) — 사용자 신고 '갑자기 다른 창 위로 올라감'의 재발입니다.");
        }

        [Test]
        public void DownwardPassThroughFootholdTopStillLandsOnThatWindow()
        {
            List<PlatformFoothold> footholds = BuildTwoLayerFootholds(out float highTopOs, out _);
            float x = Screen.width * 0.5f;

            // 정상 낙하(위 -> 아래)는 예전과 똑같이 그 창 상단에 착지해야 한다(회귀 방지의 반대편).
            Vector3 prev = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(x, highTopOs - 30f), 10f, _config);
            Vector3 curr = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(x, highTopOs + 30f), 10f, _config);
            Vector3 expectedTop = ScreenCoordinateConverter.OsScreenToWorld(_camera, new Vector2(x, highTopOs), 10f, _config);

            bool landed = GroundSensor.TryFindLandingCrossing(_camera, prev, curr, footholds, _config,
                out long handle, out float landingY);

            Debug.Log($"{LogPrefix} 하강 통과 — 이전 월드Y={prev.y:F3} -> 현재 월드Y={curr.y:F3} => " +
                $"착지={landed}, 핸들={handle}, 착지Y={landingY:F3} (기대 {expectedTop.y:F3})");

            Assert.IsTrue(landed, $"{LogPrefix} 정상 낙하인데 착지하지 못했습니다 — 헤드라인 기능(창 위 착지) 회귀입니다.");
            Assert.AreEqual(4242L, handle, $"{LogPrefix} 착지한 발판이 위쪽 창이 아닙니다.");
            Assert.AreEqual(expectedTop.y, landingY, 0.01f,
                $"{LogPrefix} 착지 Y가 창 상단선과 어긋납니다(좌표 변환 회귀).");
        }
    }
}
