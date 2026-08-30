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
