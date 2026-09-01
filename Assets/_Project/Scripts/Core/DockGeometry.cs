using UnityEngine;
using StickMate.Platform;

namespace StickMate.Core
{
    /// <summary>
    /// ★ macOS Dock 단차(Dock 상단 → 바닥 안전망 상단 낙차)의 **단일 소스** — 2026-08-30 횡단 리뷰 M1/M3 대응.
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼나 (M1: 같은 물리 대상을 6개 테스트가 4:2로 다르게 모델링했다)
    /// ============================================================================
    /// 2026-08-30 횡단 리뷰가 발견한 사실: PlayMode 테스트 6개가 "Dock 낙차"라는 **같은 하나의 물리
    /// 대상**을 각자 하드코딩하고 있었고, 그 값이 두 갈래로 갈라져 있었다.
    ///     0.855유닛 — EdgeHopDownTests / BodyTeleportTransformSyncTests / LandingCrouchTests /
    ///                 CharacterScaleInvarianceTests   ← **화석**(안전망이 화면 최하단 40pt 위였던 시절)
    ///     1.6375유닛 — DockPhysicsStepTests / DockSinkholeRegressionTests   ← 현재 기하
    /// 값이 갈라지면 테스트는 "시스템"이 아니라 "자기가 만든 숫자"를 지키게 된다. 실제로
    /// CharacterScaleInvarianceTests.DockHopDownBandSurvivesScale의 절대조건 2·3이 **거짓 통과**하고
    /// 있었다(임계 배율을 실제의 절반보다 작게 계산). 그래서 낙차를 여기 한 곳에서만 유도한다.
    ///
    /// ============================================================================
    /// 유도식 — 추정이 아니라 실측 파생이다
    /// ============================================================================
    ///     Dock 두께(pt)   = tilesize + StickConfig.dockThicknessTilePaddingPoints(26)
    ///     Dock 상단 OS y  = 화면 바닥 − 두께
    ///     안전망 상단 OS y = 화면 바닥 − NullPlatformWindowService.BottomSafetyNetInsetPoints(8)
    ///     ⇒ **낙차(pt) = tilesize + 26 − 8 = tilesize + 18**
    /// 월드 환산은 오버레이가 화면 전체를 덮는다는 사실에서 나온다 —
    ///     화면 높이 982pt ↔ 카메라 세로 2 x orthographicSize(12) = 24유닛
    ///     ⇒ 1pt = 24/982 = 0.0244399유닛
    /// 이 개발 머신(tilesize=49)에서 낙차 = 67pt = **1.63747유닛**. 두께 75pt는 NSScreen.visibleFrame
    /// 하단 인셋과 스크린샷 실측이 모두 일치한 값이다(Platform/IDockMetricsService.cs 4절).
    ///
    /// ============================================================================
    /// ★★ 이 값은 **상수가 아니다** (M3: 개발 머신 tilesize 하나에만 맞춰져 있었다)
    /// ============================================================================
    /// macOS `com.apple.dock tilesize`의 사용자 설정 범위는 16~128이다. 즉 같은 코드가 도는 다른
    /// 사용자의 화면에서 낙차는 0.83유닛(tilesize 16) ~ 3.57유닛(tilesize 128)까지 **4.3배** 변한다.
    /// 그런데 StickConfig.stepUpMaxHeight는 2.4라는 절대값이었고, tilesize 80 이상에서 그 상한을
    /// 넘는다 — "한 번 Dock 아래로 내려가면 영영 못 올라온다"(이 세션이 세 번 신고받고 두 번 고쳤다고
    /// 믿었던 바로 그 버그)가 **큰 Dock 아이콘을 쓰는 사용자에게 그대로 남아 있었다.**
    /// 그래서 <see cref="ResolveStepUpMaxHeight"/>는 설정 절대값과 **실측 낙차 + 여유** 중 큰 쪽을
    /// 쓴다. 실측 낙차는 OS를 다시 조회하지 않고 이미 열거된 발판(Dock 발판 상단 − 안전망 상단)에서
    /// 그대로 나온다 — 새 네이티브 호출이 없으므로 권한/성능/좌표계 위험이 하나도 늘지 않는다.
    ///
    /// ============================================================================
    /// 정직한 한계
    /// ============================================================================
    /// · 두께 관계식(tilesize + 26)의 보정점은 tilesize=49 **한 점뿐**이다. 두 번째 점을 얻으려면
    ///   사용자의 Dock 설정을 바꿔야 하는데 그건 절대 불변 원칙 3 위반이라 하지 않았다. 그래도 이
    ///   파일의 런타임 경로는 관계식이 아니라 **열거된 발판 사각형 실측**을 쓰므로, 관계식이 틀려도
    ///   되올라가기는 옳게 동작한다(관계식은 테스트가 배치를 재현할 때만 쓴다).
    /// · <see cref="ReferenceWorldUnitsPerPoint"/>는 orthographicSize=12 / 화면 982pt를 전제한
    ///   **테스트 배치 재현용** 환산이다. 런타임 코드는 이 상수를 쓰지 않고
    ///   ScreenCoordinateConverter(카메라·DPI 실측)를 경유한다.
    /// </summary>
    public static class DockGeometry
    {
        // ────────────────────────────────────────────────────────────────────────────
        // 기하 상수
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>Editor/SceneBootstrapper.cs가 씬에 굽는 카메라 orthographicSize. 여기서는 OS 포인트를
        /// 월드 유닛으로 환산하는 데만 쓴다(그 선언부에 "이 값을 바꾸면 OS-px 필드 8종 재검토" 경고가 있다).</summary>
        public const float ReferenceOrthographicSize = 12f;

        /// <summary>월드 1유닛이 몇 OS 포인트인가 = 982 / (2 x 12) = 40.9167.</summary>
        public const float ReferencePointsPerWorldUnit =
            NullPlatformWindowService.ReferenceScreenHeightPoints / (2f * ReferenceOrthographicSize);

        /// <summary>OS 1포인트가 몇 월드 유닛인가 = 24 / 982 = 0.0244399.</summary>
        public const float ReferenceWorldUnitsPerPoint = 1f / ReferencePointsPerWorldUnit;

        /// <summary>StickConfig.dockThicknessTilePaddingPoints의 코드 기본값과 배포 에셋값(둘 다 26).
        /// 셋이 갈라지면 Tests/EditMode/DockGeometryInvariantTests가 즉시 빨간불을 낸다.</summary>
        public const float DefaultDockThicknessTilePaddingPoints = 26f;

        /// <summary>이 프로젝트가 Dock 두께를 실측한 유일한 보정점(이 개발 머신의 com.apple.dock tilesize).
        /// **기본값이 아니라 실측점**이다 — macOS 자체의 기본 tilesize는 48이다.</summary>
        public const float DeveloperMachineTileSizePoints = 49f;

        /// <summary>macOS 시스템 설정 "Dock 크기" 슬라이더의 하한(com.apple.dock tilesize).</summary>
        public const float MinTileSizePoints = 16f;

        /// <summary>같은 슬라이더의 상한. 이 값에서 낙차가 3.57유닛 = 배율 0.75 캐릭터 키의 2.1배가 된다.</summary>
        public const float MaxTileSizePoints = 128f;

        /// <summary>
        /// 되올라가기 상한을 실측 낙차 위로 얼마나 더 띄울지(월드 유닛).
        ///
        /// 0이면 안 되는 이유: 실측 낙차와 상한이 정확히 같으면 부동소수 오차/물리 정착(접지 스냅
        /// 허용오차 20 OS-pt ≈ 0.489유닛의 일부)만으로도 `wallHeight &lt;= maxHeight` 비교가 뒤집혀
        /// **가끔만** 못 올라오는, 가장 잡기 어려운 형태의 버그가 된다.
        /// 0.30을 고른 근거 = groundSnapTolerance 20pt의 월드 환산(0.489)의 약 60%이면서,
        /// wanderEdgeStopDistance(0.30)와 같은 계열의 "한 걸음 남짓" 크기다. 이 여유가 일반 창까지
        /// 자동 등반 대상으로 만들지는 않는다 — 실측 낙차는 **Dock 발판 하나에서만** 나오기 때문이다.
        /// </summary>
        public const float StepUpDockDropMarginUnits = 0.30f;

        // ────────────────────────────────────────────────────────────────────────────
        // 낙차 유도 (테스트가 Dock 배치를 재현할 때 쓰는 단일 창구)
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>Dock 띠 두께(OS 포인트) = tilesize + 여백.</summary>
        public static float DockThicknessPoints(float tileSizePoints, float thicknessTilePaddingPoints)
            => tileSizePoints + thicknessTilePaddingPoints;

        /// <summary>Dock 상단 → 바닥 안전망 상단 낙차(OS 포인트) = 두께 − 안전망 인셋.</summary>
        public static float DockDropPoints(float tileSizePoints, float thicknessTilePaddingPoints)
            => DockThicknessPoints(tileSizePoints, thicknessTilePaddingPoints)
               - NullPlatformWindowService.BottomSafetyNetInsetPoints;

        /// <summary>같은 낙차를 월드 유닛으로. (tilesize 49 / 여백 26 → 67pt → 1.63747유닛)</summary>
        public static float DockDropWorldUnits(float tileSizePoints, float thicknessTilePaddingPoints)
            => DockDropPoints(tileSizePoints, thicknessTilePaddingPoints) * ReferenceWorldUnitsPerPoint;

        /// <summary>배포 설정 기준 낙차. config가 null이면 코드 기본 여백(26)을 쓴다.</summary>
        public static float DockDropWorldUnits(StickConfig config, float tileSizePoints)
            => DockDropWorldUnits(tileSizePoints,
                config != null ? config.dockThicknessTilePaddingPoints : DefaultDockThicknessTilePaddingPoints);

        /// <summary>
        /// ★ 이 개발 머신에서 **실측된** Dock 낙차(1.63747유닛). 테스트가 "지금 이 환경의 Dock 배치"를
        /// 재현할 때 쓰는 값이며, 예전에 6개 파일에 흩어져 있던 <c>DockDropUnits</c> 상수를 전부 대체한다.
        /// ★ 다른 tilesize를 재현하려면 <see cref="DockDropWorldUnits(float,float)"/>를 직접 부를 것.
        /// </summary>
        public static readonly float ReferenceDockDropWorldUnits =
            DockDropWorldUnits(DeveloperMachineTileSizePoints, DefaultDockThicknessTilePaddingPoints);

        // ────────────────────────────────────────────────────────────────────────────
        // 배율 임계값 (CharacterScaleInvarianceTests가 재확인한다)
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Dock 단차가 '뛰어내리기' 밴드에 남아 있으려면 필요한 최소 캐릭터 배율
        /// = 낙차 / (배율 1.0에서의 손끝~발끝 거리 ≈ 2.5072).
        ///
        /// ★ 이 배율 **아래에서 벌어지는 일**을 정확히 적어 둔다(2026-08-30 재검증):
        /// Dock 단차가 뛰어내리기 밴드를 벗어나 **매달려 내려가기**로 분류된다. 이는 고장이 아니다 —
        /// 매달리기는 "낙차 ≥ 손끝~발끝 거리"일 때만 선택되므로 그 구간에서는 매달린 발끝이 착지면을
        /// 지나치지 않는다(기하학적으로 안전한 쪽이다). 따라서 이 임계값은 **금지선이 아니라 거동
        /// 분기점**이다. 진짜 금지선은 두 개뿐이고 둘 다 따로 잠겨 있다:
        ///   (1) stepUpMaxHeight가 낙차를 덮을 것 — <see cref="ResolveStepUpMaxHeight"/>
        ///   (2) ledgeHangChance > 0 일 것 — 0이면 이 배율 아래에서 뛰어내리기도 매달리기도 성립하지
        ///       않아 캐릭터가 Dock 위에서 영원히 되돌아서기만 한다(Tests/EditMode 불변식으로 잠금).
        /// </summary>
        public static float HopDownCriticalScale(float dockDropWorldUnits, float hangReachAtScaleOne)
            => hangReachAtScaleOne > 0f ? dockDropWorldUnits / hangReachAtScaleOne : 0f;

        // ────────────────────────────────────────────────────────────────────────────
        // 되올라가기 상한 (M3 근본 수정)
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ★ 자율 배회의 되올라가기 상한을 정한다 = max(설정 절대값, 실측 Dock 낙차 + 여유).
        ///
        /// 설정 절대값(StickConfig.stepUpMaxHeight)을 그대로 쓰면 tilesize 80 이상에서 낙차가 그 값을
        /// 넘어 되올라가기가 영구히 실패한다. 반대로 실측값만 쓰면 Dock을 못 찾은 프레임(전체화면 게임
        /// 감지 중 / Dock 자동 숨김 / 비-macOS)에 상한이 0이 되어 되올라가기가 통째로 죽는다.
        /// 그래서 **둘 중 큰 쪽**이다 — 어느 한쪽이 실패해도 다른 쪽이 받친다.
        /// </summary>
        /// <param name="configuredMaxHeight">StickConfig.stepUpMaxHeight.</param>
        /// <param name="measuredDockDropWorldUnits">실측 낙차. 측정 실패 시 0 이하 또는 NaN을 넘길 것.</param>
        public static float ResolveStepUpMaxHeight(float configuredMaxHeight, float measuredDockDropWorldUnits)
        {
            float floor = Mathf.Max(0f, configuredMaxHeight);
            if (float.IsNaN(measuredDockDropWorldUnits) || measuredDockDropWorldUnits <= 0f) return floor;
            return Mathf.Max(floor, measuredDockDropWorldUnits + StepUpDockDropMarginUnits);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // ★ 경계 판정 거리 (2026-08-30 R3-M1 근본 수정) — Dock 계단 **옆면**이 강제하는 이격
        // ────────────────────────────────────────────────────────────────────────────
        //
        // 위 절들이 Dock 계단의 **높이**(낙차)를 다뤘다면, 여기는 그 계단의 **옆면(벽)**이 만드는
        // 수평 제약이다. 둘 다 같은 하나의 물리 계단(Platform/DockPhysicsStep.cs)에서 나오므로
        // 이 파일에 함께 둔다.
        //
        // ============================================================================
        // 무슨 일이 있었나 (실측 — 추측 아님)
        // ============================================================================
        // DockPhysicsStep은 Dock 발판 사각형을 **그대로** 물리 계단으로 옮긴다. 그래서 계단의 옆면은
        // 바닥 안전망 조각의 논리 경계(= 안전망 구멍의 가장자리)와 **정확히 같은 X**에 선다.
        // 그 아래에서 안전망을 따라 Dock 쪽으로 걸어오는 캐릭터는 벽에 막혀 서는데, 루트 원점에서
        // 벽까지의 거리는 몸의 물리 반폭
        // (<see cref="StickConfig.BaselineBodyPhysicsHalfWidth"/> x 배율 = 머리 원 반경) 아래로는
        // 절대 내려가지 않는다. 배율 0.75에서 그 값은 0.300 + Box2D 접촉 이격(약 0.005) = **0.305**.
        //
        // 그런데 배회 AI의 경계 판정 거리 StickConfig.wanderEdgeStopDistance는 **0.300**이었다.
        // 두 숫자는 완전히 다른 계보에서 나왔는데(하나는 프리팹 물리 형상, 하나는 UX 26-2의 배회
        // 튜닝값) 우연히 0.005유닛 차이로 붙어 있었고, 그 결과 **경계 밴드가 물리적으로 도달 불가능**
        // 했다 — 캐릭터는 x=6.705에 서 있는데 밴드는 6.700부터라, 되올라가기 판정을 평가할 기회조차
        // 없이 그 걷기 구간이 끝날 때까지 벽에 붙어 있었다(사용자가 세 번 신고한 "Dock 근처에서
        // 멈춰 있음"과 같은 증상 계열).
        //
        // ★ 이 충돌은 tilesize에 따라 켜졌다 꺼진다. 벽이 **머리 원까지 덮을 만큼 높을 때만** 이격이
        //   0.305가 되고, 그보다 낮으면 캡슐 반폭(0.15)만 남아 문제가 없다. 머리 원 아래 끝은
        //   배율 0.75에서 발바닥 위 1.241유닛이므로,
        //       낙차(유닛) = (tilesize + 18) x 0.0244399  >=  1.241  ⇒  tilesize >= 33
        //   부터 증상이 시작되고 tilesize >= 46부터는 이격이 정확히 0.305로 포화한다.
        //   **macOS 기본 tilesize 48도, 이 개발 머신의 49도 전부 그 안이다.**
        //
        // ★★ 그리고 배율을 올리면 반드시 재발한다: 이격 = 0.4 x 배율이므로 배율 0.7375 이상에서는
        //   0.300 상수가 항상 진다. 배포 배율 0.75는 그 절벽에서 겨우 0.0125 위였다. 그래서 상수
        //   비교가 아니라 **실측 반폭에서 유도**하는 방식으로 바꾼다.
        //
        // ============================================================================
        // 왜 "벽이 있을 때만" 좁히지 않고 판정 거리 자체를 올리는가
        // ============================================================================
        // (a) 벽의 유무를 매 프레임 물리 질의로 알아내면 새 비용/새 실패 모드가 생긴다.
        // (b) 이 프로젝트는 이미 같은 계열의 버그를 화면 끝에서 한 번 겪었고
        //     (러닝머신 — 클램프가 58pt 안쪽에 세우는데 판정은 24pt였다) 그때의 해법도 "판정 기준을
        //     캐릭터가 실제로 갈 수 있는 한계로 옮긴다"였다.
        // (c) 대가는 **모든** 발판 경계에서 0.105유닛(약 4pt) 일찍 서는 것뿐이다 — 육안으로 구분되지
        //     않는 크기이며, 뛰어내리기 확약 거리(hopDownEdgeCommitDistance)는 루트 기준 그대로라
        //     "모서리 코앞까지 걸어간 뒤 발을 뗀다"는 연출은 전혀 바뀌지 않는다.

        /// <summary>
        /// 경계 판정 거리를 몸의 물리 반폭 위로 얼마나 더 띄울지(월드 유닛).
        ///
        /// 0.10을 고른 근거(전부 이 값보다 작은 것들을 덮어야 한다):
        ///   · Box2D 접촉 이격 — ProjectSettings의 defaultContactOffset 0.01 → 정착 시 약 0.005.
        ///   · 이 프로젝트가 OS↔월드 좌표 왕복에 허용하는 오차 **0.02**(DockTileSizeStepUpTests).
        ///   · 30fps 한 프레임의 보행 이동 = walkSpeed(2.5) x 배율(0.75) / 30 = **0.0625**.
        ///     접근 도중에도 밴드를 건너뛰지 않으려면 이보다 커야 한다
        ///     (StickConfig.hopDownEdgeCommitDistance가 0.12인 것과 같은 계열의 근거).
        /// 셋 중 가장 큰 0.0625보다 확실히 크고, 동시에 맨틀 인셋(0.60)과의 여유도 넉넉히 남긴다.
        /// ★ 0.005 같은 소수점 셋째 자리 땜질을 다시 만들지 않기 위해, 이 여유는 **위 세 값 전부보다
        ///   크다**는 사실 자체를 Tests/EditMode/DockGeometryInvariantTests가 잠근다.
        /// </summary>
        public const float EdgeStopWallStandoffMarginUnits = 0.10f;

        /// <summary>
        /// ★ 자율 배회의 발판 경계 판정 거리를 정한다 = max(설정값, 몸의 물리 반폭 + 여유).
        ///
        /// <see cref="ResolveStepUpMaxHeight"/>와 정확히 같은 형태다 — 설정값은 **하한**이고, 물리
        /// 실측이 그보다 큰 값을 요구하면 실측이 이긴다. 어느 한쪽이 실패해도 다른 쪽이 받친다:
        ///   · 실측이 0/NaN(프리팹 없는 테스트 리그) → 설정값 그대로(예전 거동과 100% 동일).
        ///   · 설정값이 0 → 물리 반폭 + 여유가 받친다(경계 판정이 통째로 죽지 않는다).
        /// </summary>
        /// <param name="configuredStopDistance">StickConfig.wanderEdgeStopDistance.</param>
        /// <param name="bodyPhysicalHalfWidthWorld">루트 비-트리거 콜라이더의 실측 반폭. 실패 시 0 이하 또는 NaN.</param>
        public static float ResolveEdgeStopDistance(float configuredStopDistance, float bodyPhysicalHalfWidthWorld)
        {
            float floor = Mathf.Max(0f, configuredStopDistance);
            if (float.IsNaN(bodyPhysicalHalfWidthWorld) || bodyPhysicalHalfWidthWorld <= 0f) return floor;
            return Mathf.Max(floor, bodyPhysicalHalfWidthWorld + EdgeStopWallStandoffMarginUnits);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // ★ 맨틀 인셋 (2026-08-31) — 등반이 끝나 올라선 자리가 "이미 경계"가 되지 않게
        // ────────────────────────────────────────────────────────────────────────────
        //
        // 위 <see cref="ResolveEdgeStopDistance"/>가 경계 판정 거리를 배율에서 유도하게 되면서,
        // 그 값과 짝을 이루는 StickConfig.parkourMantleInset만 <b>고정 설정값으로 남아 있었다</b>.
        // 불변식은 하나다 — <b>맨틀 인셋 &gt; 경계 판정 거리</b>. 이게 깨지면 턱 위에 올라선 그 자리가
        // 이미 발판 경계로 판정되어, 진행 방향이 한 번 바깥으로 뒤집히기만 하면 곧바로 다시
        // 뛰어내린다(2026-08-29 사용자 신고 "독위로 올라오긴 하지만 바로 다시 내려감"의 필요조건).
        //
        // 고정값 0.60이 버티는 천장은 <b>배율 1.125</b>였다(Tests/EditMode/DockGeometryInvariantTests가
        // 그 값을 계산해 로그로 남겨 두었다). 캐릭터 크기 다이얼(docs/UX_FLOW.md 34-3)이 배율을
        // 0.35~2.00 전 구간에서 <b>런타임에</b> 바꾸므로, 에셋 검사만으로는 그 천장을 지킬 수 없다.
        // 그래서 이 값도 경계 판정 거리와 <b>정확히 같은 형태</b>(설정값은 하한, 물리 유도가 이기면
        // 유도가 이긴다)로 바꾼다.
        //
        // 여유(margin)를 절대값 하나로 두지 않고 <b>몸의 물리 반폭에 비례하는 항과 절대 하한 중 큰 쪽</b>
        // 으로 잡는 이유 — 덮어야 하는 것 세 가지 중 둘이 배율에 비례하기 때문이다:
        //   (1) 유휴 "주위 살피기"가 머리를 미는 폭 = 신장 x idleAmbientLookHeadShiftRatio(0.035)
        //       = 2.2747 x 배율 x 0.035 = <b>0.0796 x 배율</b> = 0.199 x 물리 반폭(0.4 x 배율).
        //   (2) 30fps 한 프레임의 보행 이동 = ResolveWalkSpeed(2.5 x 배율) / 30 = <b>0.083 x 배율</b>
        //       = 0.208 x 물리 반폭. 접근 도중에 밴드를 건너뛰지 않으려면 이보다 커야 한다.
        //   (3) 좌표 왕복 오차 0.02 + "명확한 여유" 하한 0.05 — 이 둘만 <b>절대값</b>이다.
        // (1)(2) 중 큰 쪽이 0.208 x 반폭이므로 비율 항을 <see cref="MantleInsetMarginHalfWidthRatio"/>
        // = 0.25(약 20% 여유)로 잡고, (3)은 <see cref="MantleInsetMinMarginUnits"/> = 0.10이 덮는다.
        //
        // ★ 배포 배율(0.75)에서는 유도값이 0.505라 설정값 0.60이 이긴다 — 즉 <b>지금 화면의 거동은
        //   한 픽셀도 바뀌지 않는다</b>. 유도가 실제로 이기기 시작하는 배율은 0.875부터다.

        /// <summary>맨틀 인셋 여유의 절대 하한(월드 유닛). 위 (3)만 덮는다.</summary>
        public const float MantleInsetMinMarginUnits = 0.10f;

        /// <summary>맨틀 인셋 여유의 배율 비례 항 — 몸의 물리 반폭에 대한 비율. 위 (1)(2)를 덮는다.</summary>
        public const float MantleInsetMarginHalfWidthRatio = 0.25f;

        /// <summary>
        /// ★ 등반 후 발판 안쪽으로 들어가 설 거리를 정한다 = max(설정값, 경계 판정 거리 + 여유).
        ///
        /// <see cref="ResolveEdgeStopDistance"/> / <see cref="ResolveStepUpMaxHeight"/>와 같은 형태다 —
        /// 설정값은 <b>하한</b>이고, 물리 실측이 그보다 큰 값을 요구하면 실측이 이긴다.
        /// 어느 한쪽이 실패해도 다른 쪽이 받친다:
        ///   · 경계 판정 거리가 0/NaN(프리팹 없는 리그) → 설정값 그대로(예전 거동과 100% 동일).
        ///   · 설정값이 0 → 유도가 받친다(맨틀이 모서리 선 위에 서지 않는다).
        /// </summary>
        /// <param name="configuredInset">StickConfig.parkourMantleInset.</param>
        /// <param name="resolvedEdgeStopDistance"><see cref="ResolveEdgeStopDistance"/>의 결과. 실패 시 0 이하/NaN.</param>
        /// <param name="bodyPhysicalHalfWidthWorld">몸의 실측 물리 반폭. 실패 시 0 이하/NaN이면 절대 하한만 쓴다.</param>
        public static float ResolveParkourMantleInset(float configuredInset, float resolvedEdgeStopDistance,
            float bodyPhysicalHalfWidthWorld)
        {
            float floor = Mathf.Max(0f, configuredInset);
            if (float.IsNaN(resolvedEdgeStopDistance) || resolvedEdgeStopDistance <= 0f) return floor;

            float scaled = float.IsNaN(bodyPhysicalHalfWidthWorld) || bodyPhysicalHalfWidthWorld <= 0f
                ? 0f
                : bodyPhysicalHalfWidthWorld * MantleInsetMarginHalfWidthRatio;
            float margin = Mathf.Max(MantleInsetMinMarginUnits, scaled);
            return Mathf.Max(floor, resolvedEdgeStopDistance + margin);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // ★ 경계 행동 탐지 도달거리 (2026-08-31) — "평가하는 거리"와 "탐지하는 거리"를 맞춘다
        // ────────────────────────────────────────────────────────────────────────────
        //
        // 2026-08-31 사용자 신고: "맥에서 캐릭터 크기를 키우면 독 아래에서 독 위로 안 올라옴".
        //
        // ============================================================================
        // 원인 — 한 쌍이어야 할 두 거리가 서로 다른 계보로 갈라져 있었다
        // ============================================================================
        // 배회 AI는 경계 행동(뛰어내리기/매달리기/되올라가기)을 <b>한 걷기 구간에 딱 한 번</b>,
        // "발판 경계까지 남은 거리 &lt;= 경계 판정 거리"가 되는 그 프레임에 추첨한다
        // (States/AutoWanderController.TickMoving의 _edgeActionRolledThisLeg). 추첨이 불발하면 그
        // 자리에서 멈춰 돌아서므로, <b>그보다 더 가까이 다가가는 일은 영영 없다.</b>
        // 즉 경계 행동의 대상 탐지(GroundSensor.TryFindClimbableWall / TryFindDescendTarget)는
        // <b>정확히 그 거리에서</b> 성립해야 한다.
        //
        // 그런데 두 값의 계보가 달랐다:
        //   · 평가 거리 = <see cref="ResolveEdgeStopDistance"/> → 2026-08-30부터 <b>배율에서 유도</b>
        //     (0.4 x 배율 + 0.10). 배율 0.75에서 0.400, 1.50에서 0.700, 2.00에서 <b>0.900</b>.
        //   · 탐지 거리 = StickConfig.parkourDetectionRadius → <b>0.5 절대값</b>(배율 무관).
        // 배율이 1.0을 넘는 순간 평가 거리가 탐지 거리를 추월한다. 그러면 캐릭터는 Dock에서
        // 0.7~0.9유닛 떨어진 자리에서 "경계다"라고 판정해 추첨을 돌리는데, 그 거리에서는 벽 탐지가
        // <b>게이트에서 곧바로 기각</b>되어(`distanceToEdge &gt; detectionRadius`) 되올라가기가
        // 성립할 수 없다. 그리고 그 걷기 구간은 돌아서기로 끝난다 — <b>구조적 영구 실패</b>다.
        // ★ 같은 게이트를 하강 탐지(TryFindDescendTarget)도 쓰므로, 배율을 키우면 Dock <b>위</b>에서
        //   내려오지도 못한다(사용자는 "못 올라온다"만 신고했지만 반대쪽도 같은 원인으로 죽어 있었다).
        //
        // ============================================================================
        // 왜 parkourDetectionRadius를 그냥 키우지 않는가
        // ============================================================================
        // 그 필드는 이 파일 계보에서 <b>세 가지 역할을 겸한다</b>:
        //   (a) 경계 근접 게이트, (b) "벽으로 인정할 최소 높이차", (c) 인접 발판 탐색 폭(x4).
        // 상수를 키우면 (b)가 함께 커져 낮은 턱이 등반 대상에서 빠지고, (c)가 커져 멀리 떨어진 창까지
        // 벽 후보가 된다 — 신고와 무관한 거동 두 개가 조용히 바뀐다. 그래서 (a)만 떼어 <b>평가 거리와
        // 같은 입력</b>에서 유도한다. (b)(c)는 설정 절대값 그대로다.
        //
        // ★ 배포 배율(0.75)에서 유도값 = max(0.500, 0.400 + 0.10) = <b>0.500</b> = 지금 상수와 완전히
        //   같다 — 즉 <b>지금 화면의 거동은 한 픽셀도 바뀌지 않는다</b>. 유도가 실제로 이기기 시작하는
        //   배율은 0.75 초과이며, 그 구간이 바로 지금 고장나 있던 구간이다.

        /// <summary>
        /// 탐지 도달거리를 평가 거리 위로 얼마나 더 띄울지(월드 유닛).
        ///
        /// 0이면 안 되는 이유(둘 다 실측에서 나온 값이다):
        ///   · 이 프로젝트가 OS↔월드 좌표 왕복에 허용하는 오차 <b>0.02</b>(DockTileSizeStepUpTests).
        ///   · 추첨 프레임과 ParkourClimbState.Enter()의 <b>재확인 프레임</b> 사이에 몸이 벽에서
        ///     밀려나는 접촉 복원 드리프트 <b>0.03~0.04</b>(2026-08-30 DockTileSizeStepUpTests (C)가
        ///     실측해 주석으로 남긴 값). 재확인이 기각되면 등반은 진입 직후 Fall로 무효화된다.
        /// 둘 중 큰 0.04의 2배 이상이면서, 맨틀 인셋 여유(<see cref="MantleInsetMinMarginUnits"/>)와
        /// 같은 계열의 크기로 0.10을 쓴다.
        /// </summary>
        public const float EdgeProbeReachMarginUnits = 0.10f;

        /// <summary>
        /// ★ 경계 행동 대상 탐지의 도달거리를 정한다 = max(설정 절대값, 평가 거리 + 여유).
        ///
        /// <see cref="ResolveEdgeStopDistance"/> / <see cref="ResolveParkourMantleInset"/>와 같은
        /// 형태다 — 설정값은 <b>하한</b>이고, 유도가 더 큰 값을 요구하면 유도가 이긴다.
        /// 어느 한쪽이 실패해도 다른 쪽이 받친다:
        ///   · 평가 거리가 0/NaN(유도가 죽은 리그) → 설정값 그대로(예전 거동과 100% 동일).
        ///   · 설정값이 0 → 평가 거리 + 여유가 받친다(탐지가 통째로 죽지 않는다).
        /// </summary>
        /// <param name="configuredDetectionRadius">StickConfig.parkourDetectionRadius.</param>
        /// <param name="resolvedEdgeStopDistance"><see cref="ResolveEdgeStopDistance"/>의 결과. 실패 시 0 이하/NaN.</param>
        public static float ResolveEdgeProbeReach(float configuredDetectionRadius, float resolvedEdgeStopDistance)
        {
            float floor = Mathf.Max(0f, configuredDetectionRadius);
            if (float.IsNaN(resolvedEdgeStopDistance) || resolvedEdgeStopDistance <= 0f) return floor;
            return Mathf.Max(floor, resolvedEdgeStopDistance + EdgeProbeReachMarginUnits);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 실측 낙차 — 어디서 재는가 (런타임. 새 OS 호출 0건)
        // ────────────────────────────────────────────────────────────────────────────
        //
        // 실제 측정은 States/AutoWanderController.TryMeasureDockDropWorldUnits()가 한다. 이 클래스에
        // 두지 않은 이유는 **할당** 때문이다 — 발판 핸들 조회를 델리게이트로 받으면 호출마다 람다가
        // 새로 할당되고, 이 앱은 24시간 상주라 그런 종류의 쓰레기를 만들지 않는다는 컨벤션이 있다.
        // 측정 자체는 두 줄이다(Dock 발판 상단 월드Y − 바닥 안전망 상단 월드Y). 쓰는 핸들:
        //     FallbackPlatformWindowService.DockFootholdHandle          (-2)  Dock 띠
        //     FallbackPlatformWindowService.SyntheticFootholdHandle     (-1)  안전망 왼쪽 조각
        //     FallbackPlatformWindowService.SyntheticFootholdHandleRight(-3)  안전망 오른쪽 조각
        // 안전망 두 조각의 상단 Y는 같은 단일 소스에서 나오므로 어느 쪽을 재도 같다.
        // ★ tilesize를 몰라도, 두께 관계식(tilesize+26)이 틀려도 이 측정은 옳다 — 관계식을 건너뛰고
        //   OS가 준 사각형 자체를 재기 때문이다. 관계식은 테스트가 배치를 재현할 때만 쓴다.
    }
}
