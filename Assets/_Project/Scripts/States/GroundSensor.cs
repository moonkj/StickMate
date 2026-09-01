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

            /// <summary>
            /// Grounded일 때 실제로 딛고 있는 그 발판의 PlatformFoothold.Handle.
            /// "지금 어느 창 위에 서 있는가"를 상위 레이어(진단 로그/헤드라인 기능 검증)가 사람이 읽을 수
            /// 있는 이름으로 되짚기 위한 식별자다 — 이 값으로 원본 창을 조작하는 API는 절대 호출하지
            /// 않는다(CLAUDE.md 절대 불변 원칙 3, 읽기 전용). Grounded==false면 0(무의미).
            /// FallbackPlatformWindowService의 합성 안전망은 -1을 쓰므로, -1이면 "실제 창이 아니라
            /// 안전망 위"라는 뜻이 된다.
            /// </summary>
            public readonly long GroundedFootholdHandle;

            /// <summary>
            /// ★ 2026-09-01 — <b>발판을 잃은 사유가 "걸어서 모서리를 넘어갔다"인가</b>
            /// (딛고 있던 발판이 목록에 <b>그대로 있는데</b> 발 X만 그 가로 범위 밖).
            /// Grounded==true면 언제나 false.
            ///
            /// <para>왜 필요한가: 발판 상실 유예(StickConfig.ResolveGroundLossGraceDuration) 동안
            /// <b>몸을 붙잡아 둘지</b>를 이 값 하나로 가른다. 유예의 목적은 "창 열거가 한 번 튄 것"을
            /// 흡수하는 것인데, 그러려면 그동안 몸이 <b>움직이지 않아야</b> 한다 — 튐이 지나갔을 때
            /// 이미 접지 밴드 밖으로 떨어져 있으면 유예가 아무 것도 흡수하지 못한다(유예를 늘리는 것만으로는
            /// 디버거 가설 H5가 완전히 닫히지 않는 이유가 이것이다).
            /// 반대로 <b>정말 걸어서 모서리를 넘어갔다면</b> 발밑에 아무것도 없는 것이 사실이므로 붙잡아
            /// 두면 공중부양이 된다. 그래서 그 한 경우만 예외로 가른다.</para>
            /// </summary>
            public readonly bool WalkedOffPreferredFoothold;

            public GroundInfo(bool grounded, float groundWorldY, bool hasAnyFoothold, float screenLeftWorldX, float screenRightWorldX,
                float currentFootholdLeftWorldX, float currentFootholdRightWorldX, long groundedFootholdHandle = 0L,
                bool walkedOffPreferredFoothold = false)
            {
                Grounded = grounded;
                GroundWorldY = groundWorldY;
                HasAnyFoothold = hasAnyFoothold;
                ScreenLeftWorldX = screenLeftWorldX;
                ScreenRightWorldX = screenRightWorldX;
                CurrentFootholdLeftWorldX = currentFootholdLeftWorldX;
                CurrentFootholdRightWorldX = currentFootholdRightWorldX;
                GroundedFootholdHandle = groundedFootholdHandle;
                WalkedOffPreferredFoothold = walkedOffPreferredFoothold;
            }
        }

        /// <param name="cam">좌표 변환 기준 카메라. null이면 접지 판정 불가로 취급(안전한 기본값 반환).</param>
        /// <param name="footWorldPos">캐릭터 발바닥 기준 월드 좌표(Rigidbody2D.position 등 피벗이 발이라고 가정).</param>
        /// <param name="footholds">FootholdPoller.CachedFootholds — 이 함수는 OS를 직접 호출하지 않는다.</param>
        /// <param name="preferredHandle">
        /// ★ 사용자 신고 "새 창을 켜면 캐릭터가 최상단으로 순간이동"의 수정(2026-08-28, 리더 지시 3~5항).
        /// 0이 아니면 **오직 그 핸들의 발판만** 접지 후보로 본다. 왜 필요한가: 예전에는 매 프레임
        /// 목록을 앞에서부터 훑어 "첫 매치"를 채택했는데, 새 창이 열리면 그 창이 z-order 최전면이라
        /// 목록 앞쪽에 끼어들고, 마침 캐릭터가 그 창 상단선의 허용오차 안에 있으면 채택 대상이 바뀐다.
        /// 그러면 StickmanBlackboard.SnapToGround()가 캐릭터 Y를 그 창 상단으로 **즉시 대입**해
        /// 공중 순간이동이 발생했다. 이제 딛고 있는 발판은 핸들로 고정되고, 발판 전환은 오직
        /// "낙하 -> 착지"로만 일어난다.
        ///
        /// 그 핸들의 발판이 목록에서 사라졌거나(창이 닫힘/가려짐) 캐릭터 X가 그 발판의 X 범위를
        /// 벗어났으면 Grounded=false가 되어 호출부가 즉시 Fall로 보낸다(리더 지시 4항) — "사라져도 계속
        /// 걷는" 반대편 버그가 재발하지 않도록 두 요구를 한 판정에 함께 담았다.
        /// 0이면 "아직 딛고 있는 발판이 없음"이라 예전처럼 목록 순서대로 첫 매치를 새로 획득한다.
        /// </param>
        public static GroundInfo Sense(Camera cam, Vector2 footWorldPos, IReadOnlyList<PlatformFoothold> footholds, StickConfig config,
            long preferredHandle = 0L)
        {
            if (cam == null || footholds == null || footholds.Count == 0)
            {
                return new GroundInfo(false, footWorldPos.y, false, footWorldPos.x, footWorldPos.x, footWorldPos.x, footWorldPos.x);
            }

            Vector2 footOs = ScreenCoordinateConverter.WorldToOsScreen(cam, footWorldPos, config, out float depth);
            float tolerance = config != null ? config.groundSnapTolerance : 6f;

            bool grounded = false;
            long groundedHandle = 0L;
            float groundWorldY = footWorldPos.y;
            float minLeftOs = float.MaxValue;
            float maxRightOs = float.MinValue;
            float currentLeftOs = footOs.x;
            float currentRightOs = footOs.x;
            // 고착 발판이 목록에 남아 있는가 / 발 X가 그 조각 중 하나의 가로 범위 안인가
            // (WalkedOffPreferredFoothold 문서 참고 — 유예 중 몸을 붙잡아 둘지의 유일한 판정 재료).
            bool preferredFound = false;
            bool footWithinPreferredX = false;

            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold fh = footholds[i];
                Rect r = fh.ScreenRect; // 좌상단 원점: r.y = 발판(창) 상단, r.y + r.height = 하단

                if (r.x < minLeftOs) minLeftOs = r.x;
                float rightEdge = r.x + r.width;
                if (rightEdge > maxRightOs) maxRightOs = rightEdge;

                if (grounded) continue; // 이미 접지 확정 — 좌우 경계 누적은 계속하되 재판정은 생략

                if (preferredHandle != 0L && fh.Handle == preferredHandle)
                {
                    preferredFound = true;
                    if (footOs.x >= r.x && footOs.x <= rightEdge) footWithinPreferredX = true;
                }

                // 발판 고착(sticky): 이미 딛고 있는 발판이 지정돼 있으면 그 핸들만 후보다.
                if (preferredHandle != 0L && fh.Handle != preferredHandle) continue;

                bool withinX = footOs.x >= r.x && footOs.x <= rightEdge;
                bool withinYBand = Mathf.Abs(footOs.y - r.y) <= tolerance;
                if (withinX && withinYBand)
                {
                    grounded = true;
                    groundedHandle = fh.Handle;
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

            bool walkedOff = !grounded && preferredHandle != 0L && preferredFound && !footWithinPreferredX;

            return new GroundInfo(grounded, groundWorldY, true, screenLeftWorldX, screenRightWorldX,
                currentFootholdLeftWorldX, currentFootholdRightWorldX, groundedHandle, walkedOff);
        }

        /// <summary>
        /// ★ 헤드라인 기능("윈도우 창 = 지형")의 실제 착지 판정 — 스윕(sweep) 방식 교차 검사.
        ///
        /// ============================================================================
        /// 왜 Sense()의 허용오차 밴드만으로는 실제 창 위에 절대 착지할 수 없었는가 (2026-08-28 실측/유도)
        /// ============================================================================
        /// Sense()의 Grounded는 "발이 발판 상단에서 ±groundSnapTolerance(기본 20 OS-pt) 안에 있는가"라는
        /// **한 시점(instant) 검사**다. 그리고 FallState는 그 조건이 fallGraceDuration(0.1초) **연속으로**
        /// 유지돼야 착지를 확정한다(스쳐 지나가는 한 프레임 접촉으로 인한 채터링 방지). 두 규칙을 곱하면
        /// 실제로 착지 가능한 낙하 속도의 상한이 생긴다:
        ///     밴드 두께 2 x 20pt = 40pt = 40 / (창높이[OS 포인트] / (2 x orthographicSize)) 월드유닛
        ///     (창높이=846pt, orthographicSize=12 기준 = 약 1.13유닛)
        ///     ※ 단위 주의(2026-08-29 Retina 대응): 분모는 Screen.height(Unity 픽셀)가 아니라 그것에 dpi
        ///       배율을 곱한 **OS 포인트** 창높이다. Retina에서는 Screen.height=1964, dpi=0.5로 곱이
        ///       982pt가 되어 이 유도식의 값이 변하지 않는다(tolerance 자체가 포인트 단위이므로 —
        ///       Core/StickConfig.cs의 "OS-px 필드 단위 규약" 블록 참고).
        ///     -> 착지 가능 최대 낙하속도 ~= 1.13유닛 / 0.1초 = 약 11.3유닛/초
        /// 그런데 gravityScale=3이면 가속도가 29.4유닛/초^2라 **2.2유닛(=약 78 OS-pt)만 자유낙하해도**
        /// 이 상한을 넘는다. 즉 캐릭터는 창 상단을 그냥 통과해 버리고, 유일하게 "착지에 성공하던" 곳은
        /// 화면 하단의 합성 안전망뿐이었다 — 그 위치에만 Editor/SceneBootstrapper.cs가 만든 **물리
        /// 정적 콜라이더**가 겹쳐 있어서 몸이 물리적으로 멈춰 서고, 속도가 0이 된 뒤에야 비로소 밴드
        /// 조건이 0.1초를 채울 수 있었기 때문이다. 실제 타 앱 창에는 그런 콜라이더가 없으므로
        /// "창 위를 걸어다닌다"는 헤드라인 기능이 원리적으로 한 번도 성립할 수 없었다.
        ///
        /// 해법: 낙하를 "점"이 아니라 "이번 프레임에 발이 지나간 선분"으로 보고, 그 선분이 어떤 발판의
        /// 상단선을 위->아래로 가로질렀는지 검사한다(연속 충돌 검출의 표준 기법). 이러면 낙하 속도와
        /// 프레임률에 관계없이 통과가 불가능해진다. 여러 발판을 한 프레임에 가로질렀다면 가장 **높은**
        /// (좌상단 원점이라 r.y가 가장 작은) 발판을 채택한다 — 위에서 떨어지면 제일 먼저 닿는 면이다.
        ///
        /// 좌표계/읽기전용 원칙은 Sense()와 동일하다(ScreenCoordinateConverter만 경유, OS 호출 없음).
        /// </summary>
        /// <param name="prevFootWorldPos">직전 프레임의 발 월드 좌표.</param>
        /// <param name="currFootWorldPos">이번 프레임의 발 월드 좌표.</param>
        /// <param name="handle">채택된 발판의 PlatformFoothold.Handle(안전망이면 -1).</param>
        /// <param name="landingWorldY">그 발판 상단의 월드 Y — 호출부가 여기로 스냅해야 한다.</param>
        /// <param name="ignoreHandle">
        /// ★ 2026-08-29 — 이 핸들의 발판은 착지 후보에서 제외한다(0 = 제외 없음, 기본값이라 기존 호출부는
        /// 무수정으로 예전과 완전히 동일하게 동작한다). 뛰어내리기(HopDown) 직후 **방금 떠난 그 발판**을
        /// 짧은 시간 통과시키는 플랫포머의 drop-through 관행용이다 — 서 있던 몸은 발판 상단선에 정확히
        /// 스냅돼 있어서, 모서리를 아직 넘지 않은 채 Fall로 전이하면 아래 교차 조건
        /// (prevOs.y &lt;= r.y &amp;&amp; currOs.y &gt;= r.y)이 곧바로 성립해 제자리에 도로 착지해 버린다.
        /// 유예/해제 관리는 StickmanBlackboard가 전담한다(이 함수는 넘겨받은 값만 본다).
        /// </param>
        public static bool TryFindLandingCrossing(Camera cam, Vector2 prevFootWorldPos, Vector2 currFootWorldPos,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, out long handle, out float landingWorldY, long ignoreHandle = 0L)
        {
            handle = 0L;
            landingWorldY = currFootWorldPos.y;
            if (cam == null || footholds == null || footholds.Count == 0) return false;

            Vector2 currOs = ScreenCoordinateConverter.WorldToOsScreen(cam, currFootWorldPos, config, out float depth);
            Vector2 prevOs = ScreenCoordinateConverter.WorldToOsScreen(cam, prevFootWorldPos, config, out _);

            // 좌상단 원점(y가 아래로 증가)이므로 "아래로 이동" = os y 증가. 상승 중이거나 정지 상태면
            // 착지 교차가 성립하지 않는다(점프 상승 중 천장을 뚫고 착지하는 사고 방지).
            if (currOs.y <= prevOs.y) return false;

            bool found = false;
            float bestTopOs = float.MaxValue;
            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold fh = footholds[i];

                // drop-through 유예 중인 발판은 아예 후보에서 뺀다(위 ignoreHandle 문서 참고).
                if (ignoreHandle != 0L && fh.Handle == ignoreHandle) continue;
                Rect r = fh.ScreenRect;

                // 가로 범위: 이번 프레임 끝 지점 기준으로 판정한다(수평 이동은 낙하 속도에 비해 훨씬
                // 느려서 이 근사로 충분하다 — 발판 모서리에 정확히 걸치는 경우만 한 프레임 늦어진다).
                if (currOs.x < r.x || currOs.x > r.x + r.width) continue;

                // 위->아래 교차: 직전에는 상단선 위(또는 같은 높이)에 있었고 지금은 아래로 내려갔다.
                if (prevOs.y > r.y || currOs.y < r.y) continue;

                if (r.y >= bestTopOs) continue;
                bestTopOs = r.y;
                handle = fh.Handle;
                found = true;
            }
            if (!found) return false;

            Vector3 topWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(currOs.x, bestTopOs), depth, config);
            landingWorldY = topWorld.y;
            return true;
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
        /// ★ 사용자 신고 "마우스로 끌었는데 갑자기 다른 창 위로 올라감"의 수정(2026-08-28).
        ///
        /// 주어진 월드 X에서 **가장 낮은** 발판 상단(= 그 x에서의 "바닥")의 월드 Y를 구한다.
        /// TryGetSurfaceWorldY()가 "가장 **높은** 표면"을 답하는 것과 정확히 반대다.
        ///
        /// ============================================================================
        /// 왜 이 메서드가 새로 필요했는가 — 드래그 순간이동의 진짜 원인
        /// ============================================================================
        /// DragThrowState.FollowCursor()에는 "지면 아래로는 끌고 내려가지 않는다"는 **소프트 클램프**가
        /// 있다(그 위치에 놓으면 정적 바닥 콜라이더 밑에 갇혀 Fall에 영구 고착되기 때문 — 그 함수 문서
        /// 참고). 그런데 그 클램프가 "지면"을 TryGetSurfaceWorldY(= 그 x에서 **가장 높은** 창 상단)로
        /// 물었다. 클램프 식은 `if (desired.y &lt; surfaceY) desired.y = surfaceY;` 라 **한 방향으로만
        /// 작동한다 — 캐릭터를 위로 올린다.** 따라서 커서 x가 화면 위쪽에 있는 창의 가로 범위에
        /// 걸치기만 하면, 캐릭터를 화면 아래쪽에서 끌고 있어도 매 프레임 그 창의 상단선으로 **끌어
        /// 올려졌다**. 실측 규모: 안전망(OS y=907, 월드 -10.17) 근처에서 끌던 캐릭터가 OS y=160
        /// 짜리 Finder 창 상단(월드 +8.1)으로 약 18유닛 순간이동한다 — 사용자가 본 그 증상이다.
        ///
        /// 클램프의 원래 목적("세상 바닥 밑으로 내려보내지 않는다")에 실제로 대응하는 값은 **그 x에서
        /// 가장 낮은 표면**이다(대개 화면 최하단의 합성 안전망, Platform/FallbackPlatformWindowService).
        /// 그 값으로 클램프하면 위로 끌어올리는 힘이 "정말로 바닥 밑으로 내려갔을 때"로만 제한된다.
        /// </summary>
        public static bool TryGetFloorWorldY(Camera cam, Vector2 probeWorldPos,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, out float floorWorldY)
        {
            floorWorldY = probeWorldPos.y;
            if (cam == null || footholds == null || footholds.Count == 0) return false;

            Vector2 probeOs = ScreenCoordinateConverter.WorldToOsScreen(cam, probeWorldPos, config, out float depth);

            // 좌상단 원점(y 아래로 증가)이므로 "가장 낮은 상단" = r.y가 가장 **큰** 것.
            bool found = false;
            float lowestTopOs = float.MinValue;
            for (int i = 0; i < footholds.Count; i++)
            {
                Rect r = footholds[i].ScreenRect;
                if (probeOs.x < r.x || probeOs.x > r.x + r.width) continue;
                if (r.y <= lowestTopOs) continue;
                lowestTopOs = r.y;
                found = true;
            }
            if (!found) return false;

            Vector3 topWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(probeOs.x, lowestTopOs), depth, config);
            floorWorldY = topWorld.y;
            return true;
        }

        /// <summary>
        /// ParkourClimb 진입 판정(아키텍처 0절, UX_FLOW.md 4절): 지금 딛고 있는 발판(info)의 진행방향
        /// 경계 근처(<paramref name="edgeProbeReach"/> 이내)에, 상단이 지금 발판보다 눈에 띄게
        /// (parkourDetectionRadius 이상) 높은 다른 발판이 있는지 찾는다. 있으면 그 발판(핸들 포함,
        /// 이후 등반 중 "잡을 곳이 사라졌는지" 재확인용)과 상단 월드 Y를 반환한다("벽"으로 간주).
        /// 비슷하거나 더 낮은 발판은 파쿠르 대상이 아니라 평범한 점프/낙하 대상이므로 제외한다.
        /// </summary>
        /// <param name="edgeProbeReach">
        /// ★ 2026-08-31 — "경계 근처인가" 게이트에만 쓰는 도달거리(월드 유닛). 0 이하/NaN이면 예전처럼
        /// StickConfig.parkourDetectionRadius를 쓴다(기본값이라 기존 호출부는 한 줄도 안 바뀐다).
        ///
        /// 왜 별도 인자인가: 이 게이트는 <b>배회 AI가 경계 행동을 추첨하는 거리</b>와 반드시 짝이어야
        /// 하는데(그 거리보다 가까이 다가가는 일이 영영 없다), 그 거리는 2026-08-30부터 캐릭터 배율에서
        /// 유도된다. 반면 이 함수 안의 다른 두 용도 — "벽으로 인정할 최소 높이차"와 "인접 발판 탐색 폭" —
        /// 은 판정 상대가 캐릭터가 아니라 OS 창 사각형이라 절대값이 맞다. 그래서 <b>게이트만</b> 분리한다.
        /// 유도식/근거: Core/DockGeometry.ResolveEdgeProbeReach.
        /// </param>
        public static bool TryFindClimbableWall(Camera cam, Vector2 footWorldPos, GroundInfo info, int direction,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, out PlatformFoothold wallFoothold, out float wallTopWorldY,
            float edgeProbeReach = 0f)
        {
            wallFoothold = default;
            wallTopWorldY = 0f;
            if (cam == null || !info.Grounded || footholds == null || footholds.Count == 0) return false;

            float detectionRadius = config != null ? config.parkourDetectionRadius : 0.5f;
            float probeReach = edgeProbeReach > 0f && !float.IsNaN(edgeProbeReach) ? edgeProbeReach : detectionRadius;
            float edgeX = direction > 0 ? info.CurrentFootholdRightWorldX : info.CurrentFootholdLeftWorldX;
            float distanceToEdge = direction > 0 ? edgeX - footWorldPos.x : footWorldPos.x - edgeX;
            if (distanceToEdge > probeReach) return false; // 아직 경계 근처가 아님

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
        /// ★ 매달려 내려가기(LedgeHang) 진입 판정 — <see cref="TryFindClimbableWall"/>의 **반대 방향**.
        /// 지금 딛고 있는 발판(info)의 진행방향 경계 근처에서, 그 경계 바깥으로 몸을 내렸을 때 실제로
        /// 내려앉을 **더 낮은 발판**이 있는지 찾는다. 있으면 그 발판의 핸들과 상단 월드 Y를 반환한다.
        ///
        /// 왜 "아래에 발판이 있을 때만" 매달리는가(안전 규칙): 이 판정이 없으면 화면 최하단 안전망 위
        /// (그 아래에는 아무것도 없다)에서도 매달리기를 시도하게 되고, 그건 "내려가기"가 아니라 그냥
        /// 화면 밖으로 떨어지려는 동작이 된다. 반환된 발판은 낙하 목적지의 **예상값**일 뿐이며, 실제
        /// 착지는 언제나처럼 FallState의 스윕 교차 판정이 확정한다(그 사이에 창이 움직여도 안전).
        ///
        /// 판정 위치는 "지금 서 있는 x"가 아니라 **경계에서 dropOutwardOffset만큼 바깥으로 나간 x**다 —
        /// 실제로 손을 놓았을 때(혹은 발을 내딛었을 때) 몸이 있을 자리가 거기이기 때문이다. 그 x를 가로
        /// 범위에 포함하면서 낙차가 [minDropDepth, maxDropDepth) 밴드 안인 발판 중 **가장 높은 것**
        /// (= 가장 먼저 만나는 착지면)을 고른다.
        ///
        /// ★ 2026-08-29 — 이 함수는 이제 매달리기와 **뛰어내리기** 양쪽의 목적지 판정을 겸한다. 둘의
        /// 차이는 오직 호출부가 넘기는 낙차 밴드뿐이다(아래 루프 안 주석에 유도 과정을 적어뒀다).
        /// </summary>
        /// <param name="minDropDepth">인정할 최소 낙차(월드 유닛). 이보다 얕은 발판은 목적지가 아니다.</param>
        /// <param name="maxDropDepth">인정할 낙차 상한(월드 유닛, 이 값 자신은 제외). 0 이하면 상한 없음.</param>
        /// <param name="edgeProbeReach">
        /// ★ 2026-08-31 — "경계 근처인가" 게이트에만 쓰는 도달거리. 0 이하/NaN이면 예전처럼
        /// parkourDetectionRadius. 근거는 <see cref="TryFindClimbableWall"/>의 같은 인자 문서와
        /// Core/DockGeometry.ResolveEdgeProbeReach에 있다 — 올라가는 쪽만 고치면 배율을 키운 사용자는
        /// Dock <b>위</b>에 갇힌다(같은 게이트가 하강 판정도 막고 있었다).
        /// </param>
        public static bool TryFindDescendTarget(Camera cam, Vector2 footWorldPos, GroundInfo info, int direction,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, float dropOutwardOffset,
            float minDropDepth, float maxDropDepth,
            out long targetHandle, out float targetTopWorldY, float edgeProbeReach = 0f)
        {
            targetHandle = 0L;
            targetTopWorldY = 0f;
            if (cam == null || !info.Grounded || footholds == null || footholds.Count == 0) return false;

            float detectionRadius = config != null ? config.parkourDetectionRadius : 0.5f;
            float probeReach = edgeProbeReach > 0f && !float.IsNaN(edgeProbeReach) ? edgeProbeReach : detectionRadius;
            float edgeX = direction > 0 ? info.CurrentFootholdRightWorldX : info.CurrentFootholdLeftWorldX;
            float distanceToEdge = direction > 0 ? edgeX - footWorldPos.x : footWorldPos.x - edgeX;
            if (distanceToEdge > probeReach) return false; // 아직 경계 근처가 아님

            // 손을 놓았을 때 몸이 실제로 있을 x(모서리 바깥).
            float dropX = edgeX + direction * Mathf.Max(0f, dropOutwardOffset);

            _ = ScreenCoordinateConverter.WorldToOsScreen(cam, footWorldPos, config, out float depth);
            float bestTopY = float.NegativeInfinity;
            bool found = false;

            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold fh = footholds[i];
                if (fh.Handle == info.GroundedFootholdHandle) continue; // 지금 딛고 있는 그 발판은 목적지가 아니다
                Rect r = fh.ScreenRect;
                Vector3 topLeftWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x, r.y), depth, config);
                Vector3 topRightWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x + r.width, r.y), depth, config);

                if (dropX < topLeftWorld.x || dropX > topRightWorld.x) continue; // 그 x가 발판 위가 아님

                // ★ 낙차 밴드 [minDropDepth, maxDropDepth) 판정 — 이 한 줄이 "매달리기"와 "뛰어내리기"를
                // 가르는 유일한 기준이며, **호출부가 밴드를 정한다**(2026-08-29 이전에는 여기서
                // Mathf.Max(detectionRadius, minDropDepth)로 하한을 강제해 호출부가 더 얕은 목적지를
                // 물어볼 방법 자체가 없었다 — 그래서 Dock 단차(당시 인식 0.855, 실제 1.6375유닛 — Core/DockGeometry.cs)
                // 에서 캐릭터가 아무 것도 못 했다).
                //
                //  · 매달리기(LedgeHang) 호출  : [손끝~발끝 거리, ∞)  — 목적지가 그보다 위에 있으면
                //      매달리는 순간 발이 이미 그 발판을 지나쳐, "매달려 내려간다"가 아니라 "목적지를
                //      건너뛰고 더 아래로 떨어진다"가 된다. 이 하한이 그 사고를 막는 안전 조건이다.
                //  · 뛰어내리기(HopDown) 호출  : [hopDownMinDropHeight, 손끝~발끝 거리) — 매달릴 이유가
                //      없는 한 계단 턱. 상한이 매달리기의 하한과 같아 두 밴드는 틈도 겹침도 없다.
                //
                // maxDropDepth <= 0이면 상한 없음(매달리기 호출이 쓰는 형태).
                float dropDepth = info.GroundWorldY - topLeftWorld.y;
                if (dropDepth < minDropDepth) continue;
                if (maxDropDepth > 0f && dropDepth >= maxDropDepth) continue;

                if (topLeftWorld.y > bestTopY)
                {
                    bestTopY = topLeftWorld.y;
                    targetHandle = fh.Handle;
                    found = true;
                }
            }

            if (found) targetTopWorldY = bestTopY;
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

        /// <summary>
        /// 위 <see cref="TryGetFootholdTopWorldY"/>와 같은 재확인을 하되, 매달린 **모서리의 x**까지 함께
        /// 돌려준다(LedgeHang 전용). 매달린 동안 창이 옆으로 움직이면 붙잡은 모서리도 함께 움직여야
        /// 하므로, 상단 Y만으로는 부족하고 그 방향 경계 X가 매 프레임 필요하다.
        /// direction &gt; 0이면 오른쪽 모서리, 아니면 왼쪽 모서리를 돌려준다.
        /// </summary>
        public static bool TryGetFootholdEdgeWorld(Camera cam, Vector2 refWorldPos, long handle, int direction,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, out float topWorldY, out float edgeWorldX)
        {
            topWorldY = 0f;
            edgeWorldX = 0f;
            if (cam == null || footholds == null) return false;

            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle != handle) continue;
                Rect r = footholds[i].ScreenRect;
                _ = ScreenCoordinateConverter.WorldToOsScreen(cam, refWorldPos, config, out float depth);
                Vector3 topLeft = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x, r.y), depth, config);
                Vector3 topRight = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x + r.width, r.y), depth, config);
                topWorldY = topLeft.y;
                edgeWorldX = direction > 0 ? topRight.x : topLeft.x;
                return true;
            }
            return false;
        }

        /// <summary>
        /// ★ 2026-09-01 — <b>"이 프레임 하나만으로 접지가 풀리는" 임계 프레임 시간</b>(초)을 지금의
        /// 카메라/설정에서 직접 계산한다. 상수를 베껴 적지 않기 위한 유일한 계산 지점이다.
        ///
        /// <para>유도: 논리 발판에는 물리 콜라이더가 없으므로 접지 유지는 매 프레임 위치 스냅뿐이고,
        /// 그 사이에도 중력은 적분된다. 한 프레임(dt)의 자유낙하가 접지 허용오차를 넘으면 그 프레임이
        /// 끝나는 순간 접지 판정이 실패한다:
        /// <code>
        ///   0.5 * |Physics2D.gravity.y| * gravityScale * dt^2  >  groundSnapTolerance / (OS포인트/월드유닛)
        ///   =>  dt_crit = sqrt(2 * tolerance_world / (g * gravityScale))
        /// </code>
        /// 배포 형상(gravityScale 3, 허용오차 20 OS-pt, 카메라 orthographicSize 12 → 1유닛≈40.9pt)에서
        /// <b>약 0.182초</b>가 나온다 — 디버거가 손으로 계산해 둔 그 182ms와 같은 값이며, 이제는
        /// 설정을 바꾸면 함께 움직인다.</para>
        ///
        /// <para>이 값이 무서운 이유: 절전 프레임페이싱 티어 <c>DisplayOff</c>는 4fps(=250ms/프레임)이고
        /// 엔진 최대 timestep도 333ms다. 즉 임계를 <b>상시</b> 넘는 동작 등급이 존재한다. 그래서 근본
        /// 처방은 이 임계를 키우는 것이 아니라 접지 중 중력 적분 자체를 막는 것이다
        /// (StickConfig.groundedGravitySuppressionEnabled).</para>
        ///
        /// <para>★ 이 값은 <b>낙관적 상한</b>이다(= 실제는 이보다 더 빨리 터진다). 위 식은 연속
        /// 운동학이지만 Box2D는 semi-implicit Euler라 한 스텝에서 <c>Δy = g·dt²</c>(연속의 2배)만큼
        /// 내려간다. 긴 프레임이 fixedDeltaTime 여러 번으로 쪼개지면 연속에 가까워지지만, 쪼개지지 않는
        /// 극단(한 번의 큰 적분)에서는 임계가 √2배 더 짧다. 즉 여기서 나온 값보다 안전한 쪽은 없다.</para>
        /// </summary>
        /// <returns>계산에 필요한 것(카메라)이 없거나 중력이 0이면 <see cref="float.PositiveInfinity"/>
        /// (= "어떤 프레임 길이로도 이 경로로는 떨어지지 않는다").</returns>
        public static float ComputeGroundLossFrameTimeThreshold(Camera cam, StickConfig config)
        {
            if (cam == null) return float.PositiveInfinity;
            return ComputeGroundLossFrameTimeThreshold(ComputeOsPointsPerWorldUnit(cam, config), config);
        }

        /// <summary>
        /// 위와 같은 계산을, 카메라 대신 <b>환산 비율을 직접 받아서</b> 수행한다.
        ///
        /// <para>왜 나눠 뒀는가: 이 임계값은 화면 기하(월드 1유닛이 몇 OS 포인트인가)에 정비례하는데,
        /// batchmode 테스트 환경의 화면(기본 640x480 → 20pt/유닛)은 배포 형상(982pt → 40.9pt/유닛)과
        /// 다르다. 그대로 재면 <b>테스트 환경의 임계</b>(261ms)를 배포 임계(182ms)로 착각하게 된다.
        /// 그래서 "지금 이 화면"을 볼 때는 카메라 버전을, "출하 형상"을 볼 때는 이 스칼라 버전에
        /// DockGeometry.ReferencePointsPerWorldUnit을 넘겨 쓴다.</para>
        /// </summary>
        public static float ComputeGroundLossFrameTimeThreshold(float osPointsPerWorldUnit, StickConfig config)
        {
            float gravityScale = config != null ? config.gravityScale : 1f;
            float g = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
            if (g <= 0f) return float.PositiveInfinity;
            if (osPointsPerWorldUnit <= 0f) return float.PositiveInfinity;

            float toleranceWorld = (config != null ? config.groundSnapTolerance : 6f) / osPointsPerWorldUnit;
            if (toleranceWorld <= 0f) return 0f;

            return Mathf.Sqrt(2f * toleranceWorld / g);
        }

        /// <summary>월드 1유닛이 몇 OS 포인트인가 — 카메라/DPI를 실측해서 얻는다(상수 환산 금지 규약).
        /// DockGeometry.ReferencePointsPerWorldUnit은 테스트 배치 재현용 상수이고, 런타임은 이쪽을 쓴다.</summary>
        public static float ComputeOsPointsPerWorldUnit(Camera cam, StickConfig config)
        {
            if (cam == null) return 0f;
            Vector2 a = ScreenCoordinateConverter.WorldToOsScreen(cam, Vector3.zero, config, out _);
            Vector2 b = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector3(0f, 1f, 0f), config, out _);
            return Mathf.Abs(b.y - a.y);
        }

        /// <summary>
        /// ★ 2026-09-01 (디버거) — <b>왜 접지가 풀렸는지</b>를 숫자와 함께 한 줄로 설명한다.
        /// StickmanBlackboard.GroundedTick()의 <c>[발판상실]</c> 로그 전용(그 순간에만 불리는 이산
        /// 경로라 문자열 할당이 상주 비용에 잡히지 않는다 — 매 프레임 경로에서는 절대 부르지 말 것).
        ///
        /// <para>왜 필요한가: 기존 <c>[발판상실]</c> 로그는 사유를 "(a) 사라짐 / (b) X 범위 이탈 /
        /// (c) 상단선 이탈 중 하나"라고만 적어서, 사용자 신고 "창에서 가끔 갑자기 떨어짐"을 조사할 때
        /// 실측 로그(Player.log.prevround, 핸들 5242 = 메모 창)에서 <b>세 사유를 끝내 구분할 수
        /// 없었다.</b> 로그가 유일한 판별 수단인 상주 오버레이 앱에서 "셋 중 하나"는 진단 정보가 아니다.
        /// 그래서 그 자리에서 실제 값을 재서 사유를 <b>하나로 확정</b>해 남긴다.</para>
        ///
        /// <para>사유 (d)를 새로 구분하는 것이 이 함수의 핵심이다: 논리 발판에는 물리 콜라이더가 없어
        /// "서 있기"가 <b>매 프레임 스냅</b>으로만 유지되므로(StickmanBlackboard.TickGroundKeepingSafetyNet
        /// 문서 참고), 프레임이 한 번 길어지면 <b>창이 전혀 변하지 않았는데도</b> 몸이 자유낙하로 허용
        /// 오차 밴드를 이탈한다. 실측 상수(gravityScale 3, groundSnapTolerance 20 OS-pt, 1유닛≈40.9pt)로
        /// 계산한 임계 프레임시간은 <b>182 ms</b>다. 그래서 발판이 멀쩡히 있고 X도 범위 안인데 세로만
        /// 어긋난 경우에는 그 프레임의 dt를 함께 찍어 (c)와 (d)를 구분할 수 있게 한다.</para>
        /// </summary>
        /// <param name="worstDeltaTime">유예가 쌓이는 동안 관측한 가장 긴 프레임 시간(초). 0 이하면 생략.</param>
        /// <param name="gravitySuppressedWhileGrounded">
        /// 접지 중 중력 억제(StickConfig.groundedGravitySuppressionEnabled)가 실제로 동작 중이었는지.
        /// ★ 2026-09-01 근본 수정 이후 이 값이 true라면 사유 (d)는 **구조적으로 불가능**하다 —
        /// 그런데도 (d)가 찍혔다면 억제가 어떤 경로로든 풀려 있었다는 뜻이므로, 그 사실 자체가
        /// 다음 조사의 출발점이 되도록 문구를 갈라 둔다(진단 함수의 존재 이유 그대로).
        /// </param>
        public static string DescribeGroundLoss(Camera cam, Vector2 footWorldPos,
            IReadOnlyList<PlatformFoothold> footholds, StickConfig config, long handle, float worstDeltaTime,
            bool gravitySuppressedWhileGrounded = false)
        {
            if (cam == null) return "카메라가 없어 접지 판정 자체가 불가능했습니다(사유 판정 불가).";
            int total = footholds != null ? footholds.Count : 0;
            if (handle == 0L)
            {
                return $"고착된 발판이 애초에 없었습니다(핸들 0). 발판 목록 {total}개 중 발 위치에서 " +
                    "접지 가능한 것이 하나도 없었다는 뜻입니다 — 앱 시작 직후/구조 회수 직후의 정상 경로일 수 있습니다.";
            }

            Vector2 footOs = ScreenCoordinateConverter.WorldToOsScreen(cam, footWorldPos, config, out _);
            float tolerance = config != null ? config.groundSnapTolerance : 6f;

            bool found = false;
            float bestDx = float.MaxValue;   // X 범위 밖으로 벗어난 거리(안이면 0)
            float bestDy = float.MaxValue;   // 상단선과의 세로 차(+ = 발이 상단선보다 아래)
            Rect bestRect = default;
            int segments = 0;
            for (int i = 0; i < total; i++)
            {
                if (footholds[i].Handle != handle) continue;
                segments++;
                Rect r = footholds[i].ScreenRect;
                float right = r.x + r.width;
                float dx = footOs.x < r.x ? r.x - footOs.x : (footOs.x > right ? footOs.x - right : 0f);
                float dy = footOs.y - r.y;
                // "가장 가까운 조각" 하나만 설명한다 — X가 범위 안인 조각이 있으면 그것을 우선한다.
                bool better = !found || dx < bestDx || (Mathf.Approximately(dx, bestDx) && Mathf.Abs(dy) < Mathf.Abs(bestDy));
                if (!better) continue;
                found = true; bestDx = dx; bestDy = dy; bestRect = r;
            }

            // ★ 2026-09-01 — 임계값을 숫자로 베끼지 않는다. 지금의 카메라/중력/허용오차에서 직접 유도한다
            // (ComputeGroundLossFrameTimeThreshold 문서의 유도 참고 — 배포 형상에서 약 182ms).
            float hitchThreshold = ComputeGroundLossFrameTimeThreshold(cam, config);
            string hitch;
            if (worstDeltaTime <= 0f)
            {
                hitch = string.Empty;
            }
            else if (worstDeltaTime < hitchThreshold)
            {
                hitch = $" 유예 구간의 최장 프레임={worstDeltaTime * 1000f:F0}ms(임계 {hitchThreshold * 1000f:F0}ms 미만).";
            }
            else if (gravitySuppressedWhileGrounded)
            {
                // 억제가 켜져 있으면 긴 프레임 자체는 더 이상 사유가 아니다 — 그 사실을 명시해야
                // 다음 조사자가 (d)를 다시 파는 헛수고를 하지 않는다.
                hitch = $" 유예 구간의 최장 프레임={worstDeltaTime * 1000f:F0}ms(임계 {hitchThreshold * 1000f:F0}ms 초과)이지만, " +
                    "접지 중 중력 억제가 동작 중이었으므로 **사유 (d) 프레임 끊김은 원인이 아닙니다** " +
                    "(억제 중에는 세로 적분 자체가 0이라 프레임 길이와 무관).";
            }
            else
            {
                hitch = $" 유예 구간의 최장 프레임={worstDeltaTime * 1000f:F0}ms ★임계 {hitchThreshold * 1000f:F0}ms 초과 — " +
                    "이 한 프레임의 자유낙하만으로 밴드를 벗어납니다(사유 d: 프레임 끊김). " +
                    "접지 중 중력 억제(StickConfig.groundedGravitySuppressionEnabled)가 꺼져 있거나 풀려 있었다는 뜻입니다.";
            }

            if (!found)
            {
                return $"사유 (a) 발판이 목록에서 사라졌습니다 — 핸들 {handle}의 조각이 0개입니다" +
                    $"(현재 발판 목록 {total}개). 그 창이 닫혔거나, 다른 창에 완전히 가려졌거나, " +
                    $"창 열거 자체가 한 번 실패했다는 뜻입니다. 캐릭터OS=({footOs.x:F1},{footOs.y:F1})." + hitch;
            }

            if (bestDx > 0f)
            {
                return $"사유 (b) 캐릭터 X가 발판의 가로 범위를 {bestDx:F1}pt 벗어났습니다 — " +
                    $"발판(핸들 {handle}, 조각 {segments}개) x {bestRect.x:F1}~{bestRect.x + bestRect.width:F1}, " +
                    $"캐릭터OS=({footOs.x:F1},{footOs.y:F1}). 창이 옆으로 움직였거나, 가려짐 계산으로 " +
                    "보이는 조각이 좁아졌거나, 캐릭터가 걸어서 모서리를 넘어갔습니다." + hitch;
            }

            return $"사유 (c/d) 발판은 그대로 있고 X도 범위 안인데 세로만 {bestDy:F1}pt 어긋났습니다" +
                $"(허용 ±{tolerance:F1}pt, {(bestDy > 0f ? "발이 상단선보다 아래" : "발이 상단선보다 위")}) — " +
                $"발판(핸들 {handle}) 상단OS y={bestRect.y:F1}, 캐릭터OS=({footOs.x:F1},{footOs.y:F1})." + hitch;
        }
    }
}
