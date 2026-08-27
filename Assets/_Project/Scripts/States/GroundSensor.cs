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
    }
}
