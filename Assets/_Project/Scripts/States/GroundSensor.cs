using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.States
{
    /// <summary>
    /// 캐릭터 발 위치와 캐시된 발판 목록을 비교해 "접지 여부"와 "모든 발판의 좌우 경계"를 계산하는
    /// 순수 함수형 유틸리티(Debugger BUG-M5 대응: 좌표계 변환은 오직 ScreenCoordinateConverter만
    /// 거치고, 이 클래스는 그 위에서 접지/경계 판정 로직만 담당한다 — 상태별 개별 구현 금지).
    /// IdleState/WalkState/JumpState/FallState가 공유해서 쓰며, 매 프레임 호출되어도 되도록 할당
    /// 없이(구조체 반환, for 루프만 사용) 작성한다. 이 클래스는 IPlatformWindowService를 직접 호출하지
    /// 않고 오직 FootholdPoller가 이미 캐시해둔 목록만 읽는다(BUG-M3 컨벤션 — 폴링 규율은 FootholdPoller가
    /// 전담하고 여기서는 재확인하지 않는다).
    /// </summary>
    public static class GroundSensor
    {
        // ParkourClimb 벽 탐지(TryFindClimbableWall)에서, 경계 건너편 발판을 "충분히 가깝다"고 인정하는
        // 탐색 폭을 parkourDetectionRadius의 배수로 정의하는 구현 세부 상수. AutoWanderController의
        // ScreenEdgeEpsilon과 같은 성격(디자이너 튜닝 값이 아니라 휴리스틱 여유값)이라 StickConfig가
        // 아니라 여기 상수로 둔다 — 실제 프리팹/씬으로 검증되면 조정될 수 있다.
        private const float AdjacentFootholdSearchRadiusMultiplier = 4f;
        public readonly struct GroundInfo
        {
            /// <summary>캐릭터 발이 어떤 발판 상단의 접지 허용 오차(StickConfig.groundSnapTolerance) 안에 있는지.</summary>
            public readonly bool Grounded;

            /// <summary>Grounded일 때 스냅해야 할 Unity 월드 Y좌표(그 발판 상단을 역변환한 값).</summary>
            public readonly float GroundWorldY;

            /// <summary>현재 발판이 하나라도 존재하는지(0개면 좌우 경계 판정 자체가 무의미).</summary>
            public readonly bool HasAnyFoothold;

            /// <summary>모든 발판을 통틀어 가장 왼쪽 경계(Unity 월드 X). 화면(발판 좌우 범위) 이탈 판정용.</summary>
            public readonly float ScreenLeftWorldX;

            /// <summary>모든 발판을 통틀어 가장 오른쪽 경계(Unity 월드 X).</summary>
            public readonly float ScreenRightWorldX;

            /// <summary>
            /// Grounded일 때, 지금 실제로 딛고 서 있는 그 발판 "하나"만의 왼쪽 경계(Unity 월드 X).
            /// docs/UX_FLOW.md 26-2/26-7 요구사항(AutoWanderController 배회 AI의 "발판 경계 도달" 판정) —
            /// ScreenLeftWorldX/RightWorldX(전체 발판 통합 경계)와 달리, 넓은 발판 하나를 걷다가 그 발판의
            /// 끝에 도달했지만 마침 그 옆에 다른 발판이 더 있는 경우를 올바르게 구분하기 위해 필요하다.
            /// Grounded==false면 무의미(footWorldPos.x로 채워짐).
            /// </summary>
            public readonly float CurrentFootholdLeftWorldX;

            /// <summary>위와 동일하되 지금 딛고 있는 발판의 오른쪽 경계.</summary>
            public readonly float CurrentFootholdRightWorldX;

            public GroundInfo(bool grounded, float groundWorldY, bool hasAnyFoothold, float screenLeftWorldX, float screenRightWorldX,
                float currentFootholdLeftWorldX, float currentFootholdRightWorldX)
            {
                Grounded = grounded;
                GroundWorldY = groundWorldY;
                HasAnyFoothold = hasAnyFoothold;
                ScreenLeftWorldX = screenLeftWorldX;
                ScreenRightWorldX = screenRightWorldX;
                CurrentFootholdLeftWorldX = currentFootholdLeftWorldX;
                CurrentFootholdRightWorldX = currentFootholdRightWorldX;
            }
        }

        /// <param name="cam">좌표 변환 기준 카메라. null이면 접지 판정 불가로 취급(안전한 기본값 반환).</param>
        /// <param name="footWorldPos">캐릭터 발바닥 기준 월드 좌표(Rigidbody2D.position 등 피벗이 발이라고 가정).</param>
        /// <param name="footholds">FootholdPoller.CachedFootholds — 이 함수는 OS를 직접 호출하지 않는다.</param>
        public static GroundInfo Sense(Camera cam, Vector2 footWorldPos, IReadOnlyList<PlatformFoothold> footholds, StickConfig config)
        {
            if (cam == null || footholds == null || footholds.Count == 0)
            {
                return new GroundInfo(false, footWorldPos.y, false, footWorldPos.x, footWorldPos.x, footWorldPos.x, footWorldPos.x);
            }

            Vector2 footOs = ScreenCoordinateConverter.WorldToOsScreen(cam, footWorldPos, config, out float depth);
            float tolerance = config != null ? config.groundSnapTolerance : 6f;

            bool grounded = false;
            float groundWorldY = footWorldPos.y;
            float minLeftOs = float.MaxValue;
            float maxRightOs = float.MinValue;
            float currentLeftOs = footOs.x;
            float currentRightOs = footOs.x;

            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold fh = footholds[i];
                Rect r = fh.ScreenRect; // 좌상단 원점: r.y = 발판(창) 상단, r.y + r.height = 하단

                if (r.x < minLeftOs) minLeftOs = r.x;
                float rightEdge = r.x + r.width;
                if (rightEdge > maxRightOs) maxRightOs = rightEdge;

                if (grounded) continue; // 이미 접지 확정 — 좌우 경계 누적은 계속하되 재판정은 생략

                bool withinX = footOs.x >= r.x && footOs.x <= rightEdge;
                bool withinYBand = Mathf.Abs(footOs.y - r.y) <= tolerance;
                if (withinX && withinYBand)
                {
                    grounded = true;
                    Vector3 topWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(footOs.x, r.y), depth, config);
                    groundWorldY = topWorld.y;
                    currentLeftOs = r.x;
                    currentRightOs = rightEdge;
                }
            }

            float screenLeftWorldX = footWorldPos.x;
            float screenRightWorldX = footWorldPos.x;
            if (minLeftOs <= maxRightOs)
            {
                Vector3 leftWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(minLeftOs, footOs.y), depth, config);
                Vector3 rightWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(maxRightOs, footOs.y), depth, config);
                screenLeftWorldX = leftWorld.x;
                screenRightWorldX = rightWorld.x;
            }

            float currentFootholdLeftWorldX = footWorldPos.x;
            float currentFootholdRightWorldX = footWorldPos.x;
            if (grounded)
            {
                Vector3 curLeftWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(currentLeftOs, footOs.y), depth, config);
                Vector3 curRightWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(currentRightOs, footOs.y), depth, config);
                currentFootholdLeftWorldX = curLeftWorld.x;
                currentFootholdRightWorldX = curRightWorld.x;
            }

            return new GroundInfo(grounded, groundWorldY, true, screenLeftWorldX, screenRightWorldX,
                currentFootholdLeftWorldX, currentFootholdRightWorldX);
        }

        /// <summary>
        /// 주어진 월드 X 위치에서 "가장 높은 발판 상단"의 월드 Y를 구한다(접지 허용 오차와 무관 —
        /// 캐릭터가 지금 그 높이에 있든 말든 그 x에서 딛을 수 있는 표면이 어디인지만 답한다).
        ///
        /// 왜 Sense()로는 안 되는가: Sense()의 Grounded는 "발이 발판 상단의 groundSnapTolerance 안에
        /// 있는가"라서, 캐릭터가 발판보다 한참 아래에 있으면 언제나 false다. 그런데 드래그&던지기/
        /// 로데오 커서는 **커서가 지면보다 아래에 있을 수 있고**(macOS Dock 영역 등), 그 좌표로 캐릭터를
        /// Kinematic MovePosition 하면 정적 바닥 콜라이더를 그대로 통과해 지면 밑에 놓이게 된다. 그
        /// 상태에서 Dynamic으로 돌아가면 접지 판정은 영원히 false이고(허용 오차 밖) 물리 바닥이 위로
        /// 올려주지도 못해 **Fall 상태에 영구 고착**된다 — 드래그&던지기 배선 라운드(2026-08-28)에
        /// 실측으로 확인한 현상이다. 그 상황을 애초에 만들지 않기 위해 "이 x에서 지면은 어디인가"를
        /// 접지 여부와 분리해 물어볼 수 있어야 한다.
        /// </summary>
        public static bool TryGetSurfaceWorldY(Camera cam, Vector2 probeWorldPos,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, out float surfaceWorldY)
        {
            surfaceWorldY = probeWorldPos.y;
            if (cam == null || footholds == null || footholds.Count == 0) return false;

            Vector2 probeOs = ScreenCoordinateConverter.WorldToOsScreen(cam, probeWorldPos, config, out float depth);

            // 좌상단 원점(y 아래로 증가)이므로 "가장 높은 상단" = r.y가 가장 작은 것.
            bool found = false;
            float bestTopOs = float.MaxValue;
            for (int i = 0; i < footholds.Count; i++)
            {
                Rect r = footholds[i].ScreenRect;
                if (probeOs.x < r.x || probeOs.x > r.x + r.width) continue;
                if (r.y >= bestTopOs) continue;
                bestTopOs = r.y;
                found = true;
            }
            if (!found) return false;

            Vector3 topWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(probeOs.x, bestTopOs), depth, config);
            surfaceWorldY = topWorld.y;
            return true;
        }

        /// <summary>
        /// ParkourClimb 진입 판정(아키텍처 0절, UX_FLOW.md 4절): 지금 딛고 있는 발판(info)의 진행방향
        /// 경계 근처(parkourDetectionRadius 이내)에, 상단이 지금 발판보다 눈에 띄게(parkourDetectionRadius
        /// 이상) 높은 다른 발판이 있는지 찾는다. 있으면 그 발판(핸들 포함, 이후 등반 중 "잡을 곳이
        /// 사라졌는지" 재확인용)과 상단 월드 Y를 반환한다("벽"으로 간주). 비슷하거나 더 낮은 발판은
        /// 파쿠르 대상이 아니라 평범한 점프/낙하 대상이므로 제외한다.
        /// </summary>
        public static bool TryFindClimbableWall(Camera cam, Vector2 footWorldPos, GroundInfo info, int direction,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, out PlatformFoothold wallFoothold, out float wallTopWorldY)
        {
            wallFoothold = default;
            wallTopWorldY = 0f;
            if (cam == null || !info.Grounded || footholds == null || footholds.Count == 0) return false;

            float detectionRadius = config != null ? config.parkourDetectionRadius : 0.5f;
            float edgeX = direction > 0 ? info.CurrentFootholdRightWorldX : info.CurrentFootholdLeftWorldX;
            float distanceToEdge = direction > 0 ? edgeX - footWorldPos.x : footWorldPos.x - edgeX;
            if (distanceToEdge > detectionRadius) return false; // 아직 경계 근처가 아님

            _ = ScreenCoordinateConverter.WorldToOsScreen(cam, footWorldPos, config, out float depth);
            float searchSlack = detectionRadius * AdjacentFootholdSearchRadiusMultiplier;
            float bestTopY = float.NegativeInfinity;
            bool found = false;

            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold fh = footholds[i];
                Rect r = fh.ScreenRect;
                Vector3 topLeftWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x, r.y), depth, config);
                Vector3 topRightWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x + r.width, r.y), depth, config);

                bool horizontallyNear = direction > 0
                    ? topLeftWorld.x >= edgeX - detectionRadius && topLeftWorld.x <= edgeX + searchSlack
                    : topRightWorld.x <= edgeX + detectionRadius && topRightWorld.x >= edgeX - searchSlack;
                if (!horizontallyNear) continue;

                if (topLeftWorld.y - info.GroundWorldY < detectionRadius) continue; // 충분히 높지 않음(파쿠르 대상 아님)

                if (topLeftWorld.y > bestTopY)
                {
                    bestTopY = topLeftWorld.y;
                    wallFoothold = fh;
                    found = true;
                }
            }

            if (found) wallTopWorldY = bestTopY;
            return found;
        }

        /// <summary>
        /// ParkourClimb 등반 중, handle로 식별된 발판이 캐시에 여전히 존재하는지 재확인하고 존재하면
        /// 그 발판의 최신 상단 월드 Y를 반환한다(창이 이동했을 수 있으므로 매 프레임 재계산). 존재하지
        /// 않으면(창이 닫히거나 이동해 사라짐) false — "잡을 곳이 사라짐" 실패 처리(UX_FLOW.md 4절)에 사용.
        /// </summary>
        public static bool TryGetFootholdTopWorldY(Camera cam, Vector2 refWorldPos, long handle,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, out float topWorldY)
        {
            topWorldY = 0f;
            if (cam == null || footholds == null) return false;

            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle != handle) continue;
                Rect r = footholds[i].ScreenRect;
                _ = ScreenCoordinateConverter.WorldToOsScreen(cam, refWorldPos, config, out float depth);
                Vector3 topWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x, r.y), depth, config);
                topWorldY = topWorld.y;
                return true;
            }
            return false;
        }
    }
}
