using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ Dock 기하 단일 소스(Core/DockGeometry.cs)가 **실제 배포 자산과 어긋나지 않는지** 잠근다
    /// (2026-08-30 횡단 리뷰 M1/m3 대응).
    ///
    /// 이 파일이 잡으려는 실패는 딱 두 종류다:
    ///   (1) **상수 표류** — DockGeometry가 "코드 기본값"이라고 적어 둔 숫자가 StickConfig의 실제
    ///       기본값이나 배포 에셋(DefaultStickConfig.asset)의 값과 갈라지는 것. 갈라지면 테스트가
    ///       재현하는 Dock 배치가 실제 앱의 Dock 배치와 다른 것이 되고, 그때부터 PlayMode 테스트는
    ///       시스템이 아니라 자기 숫자를 지킨다(M1이 정확히 그렇게 6개 파일에서 벌어졌다).
    ///   (2) **코드 기본값 ↔ 에셋 불일치** — CreateInstance&lt;StickConfig&gt;()로 설정을 만드는 테스트가
    ///       배포판과 다른 값을 쓰게 되는 지뢰(m3의 groundSnapTolerance 6 vs 20).
    ///
    /// 이 테스트는 **원본 자산을 읽기만 한다**(절대 불변 원칙 3).
    ///
    /// 네거티브 컨트롤: DockGeometry.DefaultDockThicknessTilePaddingPoints를 26 → 25로 바꾸거나
    /// StickConfig.groundSnapTolerance 기본값을 20 → 6으로 되돌리면 아래 단언이 즉시 실패한다
    /// (2026-08-30 디버거가 실제로 되돌려 확인).
    /// </summary>
    public class DockGeometryInvariantTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        private static StickConfig CreateCodeDefaultConfig() => ScriptableObject.CreateInstance<StickConfig>();

        // ============================================================================
        // (1) 상수 표류
        // ============================================================================

        [Test]
        public void Dock두께_여백_상수가_코드기본값_에셋값과_모두_같아야_한다()
        {
            StickConfig deployed = LoadDeployedConfig();
            StickConfig codeDefault = CreateCodeDefaultConfig();
            try
            {
                Assert.AreEqual(DockGeometry.DefaultDockThicknessTilePaddingPoints,
                    codeDefault.dockThicknessTilePaddingPoints, 0.0001f,
                    "DockGeometry.DefaultDockThicknessTilePaddingPoints가 StickConfig의 코드 기본값과 " +
                    "다릅니다 — 테스트가 재현하는 Dock 두께가 실제와 어긋납니다.");
                Assert.AreEqual(DockGeometry.DefaultDockThicknessTilePaddingPoints,
                    deployed.dockThicknessTilePaddingPoints, 0.0001f,
                    "DockGeometry.DefaultDockThicknessTilePaddingPoints가 배포 에셋값과 다릅니다.");
            }
            finally { Object.DestroyImmediate(codeDefault); }
        }

        [Test]
        public void 기준_Dock_낙차가_실측_유도식과_일치해야_한다()
        {
            // 실측(2026-08-29, 이 개발 머신): tilesize 49, Dock 두께 75.00pt, Dock 상단 OS y=907,
            // 안전망 상단 OS y=974 → 낙차 67pt. 월드 환산 24유닛 / 982pt.
            const float measuredDropPoints = 67f;
            float derivedPoints = DockGeometry.DockDropPoints(
                DockGeometry.DeveloperMachineTileSizePoints,
                DockGeometry.DefaultDockThicknessTilePaddingPoints);

            Assert.AreEqual(measuredDropPoints, derivedPoints, 0.0001f,
                $"유도한 Dock 낙차({derivedPoints:F2}pt)가 스크린샷/visibleFrame 실측값(67pt)과 다릅니다.");

            float expectedUnits = measuredDropPoints * (2f * DockGeometry.ReferenceOrthographicSize)
                                  / NullPlatformWindowService.ReferenceScreenHeightPoints;
            Assert.AreEqual(expectedUnits, DockGeometry.ReferenceDockDropWorldUnits, 0.0001f,
                $"ReferenceDockDropWorldUnits({DockGeometry.ReferenceDockDropWorldUnits:F5})가 " +
                $"실측 환산({expectedUnits:F5})과 다릅니다.");

            // ★ 화석 감지 — 예전에 6개 테스트 파일에 흩어져 있던 0.855는 안전망이 40pt 위였던 시절의
            // 값이다. 누군가 되돌리면 여기서 즉시 빨간불이 난다.
            Assert.Greater(DockGeometry.ReferenceDockDropWorldUnits, 1.5f,
                "Dock 낙차가 1.5유닛 미만입니다 — 폐기된 0.855(안전망 40pt 시절) 기하로 되돌아갔는지 확인하세요.");
        }

        [Test]
        public void 안전망_인셋과_화면높이_기준이_단일소스여야_한다()
        {
            Assert.AreEqual(8f, NullPlatformWindowService.BottomSafetyNetInsetPoints, 0.0001f,
                "BottomSafetyNetInsetPoints가 8pt가 아닙니다 — 바뀌었다면 Dock 낙차/stepUpMaxHeights/" +
                "CharacterScaleInvarianceTests의 임계 배율을 전부 다시 계산해야 합니다(그 값들은 " +
                "DockGeometry에서 자동으로 따라오지만, 문서 상수 DockHopDownCriticalScale은 손으로 갱신해야 합니다).");
            Assert.AreEqual(982f, NullPlatformWindowService.ReferenceScreenHeightPoints, 0.0001f,
                "ReferenceScreenHeightPoints가 982pt가 아닙니다 — OS-pt → 월드 환산 비율이 바뀝니다.");
        }

        // ============================================================================
        // (2) 코드 기본값 ↔ 배포 에셋 (m3)
        // ============================================================================

        [Test]
        public void groundSnapTolerance_코드기본값이_배포에셋과_같아야_한다()
        {
            StickConfig deployed = LoadDeployedConfig();
            StickConfig codeDefault = CreateCodeDefaultConfig();
            try
            {
                Assert.AreEqual(deployed.groundSnapTolerance, codeDefault.groundSnapTolerance, 0.0001f,
                    $"StickConfig.groundSnapTolerance 코드 기본값({codeDefault.groundSnapTolerance:F1})이 " +
                    $"배포 에셋값({deployed.groundSnapTolerance:F1})과 다릅니다 — CreateInstance<StickConfig>()를 " +
                    "쓰는 테스트가 배포판과 다른 접지 밴드에서 돌게 됩니다(6이면 0.489 → 0.147유닛으로 " +
                    "3.3배 좁아집니다. 2026-08-30 횡단 리뷰 m3).");
            }
            finally { Object.DestroyImmediate(codeDefault); }
        }

        // ============================================================================
        // (3) 되올라가기 상한 유도 (M3) — 어떤 tilesize에서도 낙차를 덮는가
        // ============================================================================

        [Test]
        public void 되올라가기_상한이_모든_tilesize의_낙차를_덮어야_한다()
        {
            StickConfig deployed = LoadDeployedConfig();

            // macOS 시스템 설정 "Dock 크기" 슬라이더 전 구간을 훑는다(끝점 + 중간 표본).
            float[] tileSizes =
            {
                DockGeometry.MinTileSizePoints, 32f, 48f,
                DockGeometry.DeveloperMachineTileSizePoints, 64f, 80f, 96f,
                DockGeometry.MaxTileSizePoints
            };

            // ★ 2026-09-02 — 설정 필드가 절대 유닛에서 **신장 배수**(stepUpMaxHeights)가 됐다.
            //   숫자를 베끼지 않고 프로덕션 리졸버를 그대로 호출해 월드 유닛으로 환산한다.
            float deployedHeight = StickConfig.BaselineCharacterTotalHeight * deployed.ResolveCharacterScale();
            float configuredWorld = deployed.ResolveStepUpMaxHeightWorld(deployedHeight);

            foreach (float tile in tileSizes)
            {
                float drop = DockGeometry.DockDropWorldUnits(deployed, tile);
                float resolved = DockGeometry.ResolveStepUpMaxHeight(configuredWorld, drop);

                Debug.Log($"[DOCK-GEOM] tilesize={tile:F0}pt → 낙차 {drop:F4}유닛, " +
                    $"설정 상한 {deployed.stepUpMaxHeights:F4} H x 신장 {deployedHeight:F4} = {configuredWorld:F4}유닛 " +
                    $"→ 유도 상한 {resolved:F4}유닛 (여유 {(resolved - drop):F4})");

                Assert.Greater(resolved, drop,
                    $"tilesize {tile:F0}pt(낙차 {drop:F3}유닛)에서 유도된 되올라가기 상한({resolved:F3})이 " +
                    "낙차를 덮지 못합니다 — 이 설정을 쓰는 사용자는 한 번 Dock 아래로 내려가면 영영 못 올라옵니다.");
            }
        }

        [Test]
        public void 설정_절대값만으로는_큰_Dock을_못_덮는다는_사실을_기록한다()
        {
            StickConfig deployed = LoadDeployedConfig();

            // ★ 네거티브 컨트롤 그 자체 — M3의 근거를 테스트로 박제한다. 이 단언이 실패한다면
            // 되올라가기 상한이 3.57유닛 이상으로 올라갔다는 뜻이고, 그때는 DockGeometry의 유도가
            // 불필요해진 것이 아니라 **일반 창까지 자동 등반 대상이 됐다**는 뜻이므로 재검토해야 한다.
            //
            // ★ 2026-09-02 — 설정값이 신장 배수가 됐으므로 **가장 큰 캐릭터**(MaxCharacterScale)에서
            //   재는 것이 가장 엄격하다. 그보다 작은 배율은 자동으로 더 못 덮는다.
            float maxDrop = DockGeometry.DockDropWorldUnits(deployed, DockGeometry.MaxTileSizePoints);
            float biggestCharacterWorld = deployed.ResolveStepUpMaxHeightWorld(
                StickConfig.BaselineCharacterTotalHeight * StickConfig.MaxCharacterScale);
            Debug.Log($"[DOCK-GEOM] 최대 tilesize 낙차 {maxDrop:F4}유닛 vs 되올라가기 설정 상한 " +
                $"{deployed.stepUpMaxHeights:F4} H (가장 큰 캐릭터에서 {biggestCharacterWorld:F4}유닛).");
            Assert.Less(biggestCharacterWorld, maxDrop,
                $"되올라가기 설정 상한({deployed.stepUpMaxHeights:F4} H = 가장 큰 캐릭터에서 " +
                $"{biggestCharacterWorld:F3}유닛)이 최대 tilesize의 낙차({maxDrop:F3})를 " +
                "설정값만으로 덮고 있습니다 — 값이 이렇게 커지면 Dock이 아닌 일반 창 발판까지 " +
                "1.2초 만에 순간이동하듯 기어오르게 됩니다. 유도 방식(DockGeometry)으로 되돌리세요.");
        }

        // ============================================================================
        // (3-b) ★ 교차점 정밀 — 설정 절대값이 낙차를 못 덮기 시작하는 정확한 tilesize
        //       (2026-08-30 R3 M2: PlayMode 테스트와 R2 보고서 표가 이 경계를 **한 칸 이르게** 적었다)
        // ============================================================================

        /// <summary>
        /// R3 리뷰가 잡은 산술 오기의 박제. `DockTileSizeStepUpTests`의 네거티브 컨트롤 게이트가
        /// `tileSizePoints >= 80f`였는데, 그 시절의 실제 교차점은 80이 아니라 **80.2**였다
        /// (설정 절대값 2.400유닛 ÷ ReferenceWorldUnitsPerPoint(24/982 = 0.0244399) = 98.2pt,
        /// 낙차(pt) = tilesize + 26 − 8 = tilesize + 18 ⇒ tilesize = 80.2).
        ///
        /// <para>★ 2026-09-02 — <b>교차점 자체를 상수로 적는 것을 그만둔다.</b> 되올라가기 상한이
        /// 신장 배수(<c>StickConfig.stepUpMaxHeights</c>)가 되면서 교차점이 <b>배포 배율에 따라
        /// 움직이는 값</b>이 됐다(배율 0.75에서 약 55.6pt). 80/81을 그대로 두면 이 테스트는
        /// "산술을 잠그는 것"이 아니라 "옛 배율을 잠그는 것"이 된다 — 그건 협업 프로토콜이 금지한
        /// '테스트에 프로덕션 상수를 숫자로 베끼기'의 다른 얼굴이다. 그래서 교차점을 유도한 뒤
        /// <b>그 바로 아래 정수(floor) / 바로 위 정수(ceil)</b>에서 부등호를 양방향으로 단언한다.
        /// 잠그는 대상은 숫자가 아니라 <b>관계</b>다.</para>
        ///
        /// 이 검증을 PlayMode가 아니라 여기 두는 이유: PlayMode는 OS↔월드 좌표를 왕복하며 재기 때문에
        /// 허용오차(0.02유닛)가 붙는데 교차점 근방의 여유는 그보다 작다 — **측정 노이즈가 부등호를
        /// 뒤집을 수 있다**. 여기(순수 산술)에는 그 오차가 존재하지 않는다.
        ///
        /// 네거티브 컨트롤: 아래 두 tilesize를 맞바꾸면 두 단언이 즉시 실패한다.
        /// </summary>
        [Test]
        public void 설정값_커버리지_교차점이_유도식과_정확히_일치한다()
        {
            StickConfig deployed = LoadDeployedConfig();

            // 프로덕션 리졸버를 그대로 부른다(숫자를 베끼지 않는다).
            float deployedHeight = StickConfig.BaselineCharacterTotalHeight * deployed.ResolveCharacterScale();
            float configured = deployed.ResolveStepUpMaxHeightWorld(deployedHeight);

            float crossoverTileSize = configured / DockGeometry.ReferenceWorldUnitsPerPoint
                - deployed.dockThicknessTilePaddingPoints
                + NullPlatformWindowService.BottomSafetyNetInsetPoints;

            // 전제 — 교차점이 macOS가 실제로 허용하는 tilesize 구간 안에 있어야 이 테스트가 의미를 갖는다.
            // (구간 밖이면 "모든 Dock을 덮는다" 또는 "어떤 Dock도 못 덮는다"가 되어 양방향 단언이 성립하지 않는다.)
            Assert.That(crossoverTileSize,
                Is.GreaterThan(DockGeometry.MinTileSizePoints + 1f).And.LessThan(DockGeometry.MaxTileSizePoints - 1f),
                $"유도한 교차 tilesize({crossoverTileSize:F2}pt)가 macOS tilesize 구간" +
                $"({DockGeometry.MinTileSizePoints:F0}~{DockGeometry.MaxTileSizePoints:F0}) 밖입니다 — " +
                "그러면 '설정값 단독으로 덮는다/못 덮는다'가 한쪽으로만 존재해 이 검사가 아무것도 잠그지 못합니다.");

            float coveredTileSize = Mathf.Floor(crossoverTileSize);
            float notCoveredTileSize = Mathf.Ceil(crossoverTileSize);
            if (Mathf.Approximately(coveredTileSize, notCoveredTileSize)) notCoveredTileSize += 1f;

            float coveredDrop = DockGeometry.DockDropWorldUnits(deployed, coveredTileSize);
            float notCoveredDrop = DockGeometry.DockDropWorldUnits(deployed, notCoveredTileSize);

            Debug.Log($"[DOCK-GEOM] 설정값 커버리지 교차 tilesize = {crossoverTileSize:F2}pt " +
                $"(되올라가기 상한 {deployed.stepUpMaxHeights:F4} H x 신장 {deployedHeight:F4} = {configured:F4}유닛). " +
                $"tilesize {coveredTileSize:F0} → 낙차 {coveredDrop:F5}유닛 (여유 {(configured - coveredDrop):F5}) / " +
                $"tilesize {notCoveredTileSize:F0} → 낙차 {notCoveredDrop:F5}유닛 (부족 {(notCoveredDrop - configured):F5})");

            Assert.Greater(configured, coveredDrop,
                $"tilesize {coveredTileSize:F0}pt의 낙차({coveredDrop:F5})를 설정값({configured:F3}유닛)이 " +
                "못 덮습니다 — 교차점 유도식이 실제 커버리지와 어긋났습니다(환산 상수/인셋을 재산출하세요).");
            Assert.Less(configured, notCoveredDrop,
                $"tilesize {notCoveredTileSize:F0}pt의 낙차({notCoveredDrop:F5})를 설정값({configured:F3}유닛)이 " +
                "아직 덮고 있습니다 — 마찬가지로 유도식이 어긋났습니다.");

            // 유도 상한은 교차점 위에서도 두 tilesize 모두를 덮어야 한다(M3의 본체).
            Assert.Greater(DockGeometry.ResolveStepUpMaxHeight(configured, notCoveredDrop), notCoveredDrop,
                "유도 상한이 교차점 바로 위 tilesize의 낙차조차 덮지 못합니다.");
        }

        // ============================================================================
        // (5) ★ Dock 계단 **옆면**이 강제하는 이격 vs 배회 경계 판정 밴드 (2026-08-30 R3-M1)
        // ============================================================================
        //
        // (1)~(3)이 계단의 **높이**를 다뤘다면 여기는 같은 계단의 **옆면**이다.
        // DockPhysicsStep은 Dock 발판 사각형을 그대로 물리 계단으로 옮기므로, 계단의 옆면은 바닥
        // 안전망 조각의 논리 경계와 **정확히 같은 X**에 선다. 그 벽에 막혀 선 캐릭터의 루트는 몸의
        // 물리 반폭 아래로 절대 다가가지 못하는데, 배회 AI의 경계 판정 거리가 그보다 작으면
        // **밴드가 물리적으로 도달 불가능**해져 되올라가기를 평가할 기회조차 없다.

        /// <summary>몸의 물리 반폭(배율 1.0에서 0.4 = 머리 원 반경).</summary>
        private static float BodyHalfWidth(StickConfig c)
            => StickConfig.BaselineBodyPhysicsHalfWidth * c.ResolveCharacterScale();

        /// <summary>Box2D 접촉 이격 — ProjectSettings/Physics2DSettings의 defaultContactOffset(0.01)의
        /// 절반 정도가 정착 시 실제로 남는 틈이다(실측: 벽 6.400 / 정지 6.705 / 반폭 0.300).</summary>
        private const float Box2DContactSeparationUnits = 0.005f;

        /// <summary>"명확한 여유"의 하한. R3-M1의 0.005는 이 값의 1/10이었다.</summary>
        private const float RequiredClearanceUnits = 0.05f;

        [Test]
        public void 경계_판정_밴드가_벽_이격을_모든_배율에서_덮어야_한다()
        {
            StickConfig deployed = LoadDeployedConfig();

            // 캐릭터 크기 다이얼의 전 구간(StickConfig.Min/MaxCharacterScale) + 배포값.
            float[] scales =
            {
                StickConfig.MinCharacterScale, 0.5f, 0.6531f, deployed.ResolveCharacterScale(),
                1f, 1.5f, StickConfig.MaxCharacterScale
            };

            foreach (float scale in scales)
            {
                float halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * scale;
                float standoff = halfWidth + Box2DContactSeparationUnits;
                float resolved = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance, halfWidth);
                float clearance = resolved - standoff;

                Debug.Log($"[DOCK-GEOM] 배율 {scale:F3} → 물리 반폭 {halfWidth:F3}, 벽 이격 {standoff:F3}, " +
                    $"경계 판정 거리 설정 {deployed.wanderEdgeStopDistance:F3} → 유도 {resolved:F3} " +
                    $"(여유 {clearance:F4})");

                Assert.GreaterOrEqual(clearance, RequiredClearanceUnits,
                    $"배율 {scale:F3}에서 경계 판정 거리({resolved:F3})가 벽 이격({standoff:F3})보다 " +
                    $"{RequiredClearanceUnits:F2} 넘게 크지 않습니다(여유 {clearance:F4}) — 이 배율의 사용자는 " +
                    "Dock 계단 옆면에 붙어 서면 되올라가기를 평가조차 못 합니다(2026-08-30 R3-M1).");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 그 자체 — "설정 절대값 단독으로는 못 덮는다"를 박제한다.
        /// 이 단언이 실패한다면 wanderEdgeStopDistance를 손으로 올려 덮은 것이고, 그 방식은 배율을
        /// 키우는 순간 다시 깨진다(이격 = 0.4 x 배율이므로). 유도 쪽을 유지해야 한다.
        ///
        /// 함께 기록: 이 충돌이 **켜지는 tilesize 구간**. 벽이 머리 원까지 덮을 만큼 높아야 이격이
        /// 0.4 x 배율로 포화하고, 그보다 낮으면 루트 캡슐 반폭(0.2 x 배율)만 남아 문제가 없다.
        /// </summary>
        [Test]
        public void 설정_절대값_단독으로는_벽_이격을_못_덮는다는_사실을_기록한다()
        {
            StickConfig deployed = LoadDeployedConfig();
            float scale = deployed.ResolveCharacterScale();
            float standoff = BodyHalfWidth(deployed) + Box2DContactSeparationUnits;

            // 머리 원의 아래 끝(발바닥 기준) = (전신 높이 − 머리 시각 반경) − 머리 물리 반경.
            // 벽이 이 높이를 넘겨 덮어야 머리가 벽면에 닿는다.
            float totalHeight = StickConfig.BaselineCharacterTotalHeight * scale;
            const float BaselineHeadVisualRadius = 0.22f;
            float headBottomLocalY = totalHeight - BaselineHeadVisualRadius * scale
                                     - StickConfig.BaselineBodyPhysicsHalfWidth * scale;
            float onsetTileSize = headBottomLocalY / DockGeometry.ReferenceWorldUnitsPerPoint
                                  - deployed.dockThicknessTilePaddingPoints
                                  + NullPlatformWindowService.BottomSafetyNetInsetPoints;

            Debug.Log($"[DOCK-GEOM] 배율 {scale:F3} — 벽 이격 {standoff:F3} vs 설정 절대값 " +
                $"{deployed.wanderEdgeStopDistance:F3}(차이 {(standoff - deployed.wanderEdgeStopDistance):F4}). " +
                $"머리 원 아래 끝 {headBottomLocalY:F3}유닛 → 이 충돌이 켜지기 시작하는 tilesize ≈ " +
                $"{onsetTileSize:F1}pt (macOS 기본 48 / 이 개발 머신 " +
                $"{DockGeometry.DeveloperMachineTileSizePoints:F0} 둘 다 그 위).");

            Assert.Less(deployed.wanderEdgeStopDistance, standoff,
                $"설정 절대값({deployed.wanderEdgeStopDistance:F3})이 벽 이격({standoff:F3})을 이미 덮고 " +
                "있습니다 — R3-M1의 전제가 바뀌었습니다. 상수를 올려 덮은 것이라면 배율을 키우는 순간 " +
                "다시 깨지므로 유도(DockGeometry.ResolveEdgeStopDistance)를 유지하세요.");

            Assert.Less(onsetTileSize, DockGeometry.DeveloperMachineTileSizePoints,
                $"이 충돌이 켜지는 tilesize({onsetTileSize:F1}pt)가 이 개발 머신의 tilesize" +
                $"({DockGeometry.DeveloperMachineTileSizePoints:F0})보다 큽니다 — 그렇다면 R3-M1은 " +
                "사용자 환경에서 재현되지 않는다는 뜻이라 근거를 다시 세워야 합니다.");
        }

        /// <summary>
        /// ★ 2026-08-31 — 맨틀 인셋도 유도값이 됐다(캐릭터 크기 다이얼 선행조건 5항).
        /// 예전 이 자리의 테스트는 "고정값 0.60이 버티는 배율 천장 = 1.125"를 계산해 <b>기록</b>만
        /// 하고 배포 배율이 그 아래임을 확인했다. 이제는 다이얼이 배율을 런타임에 바꾸므로
        /// <b>전 구간(0.35~2.00)에서 불변식이 성립하는지</b>를 직접 단언한다.
        ///
        /// 불변식: 맨틀 인셋 유도값 − 경계 판정 거리 유도값 ≥ <see cref="RequiredClearanceUnits"/>.
        /// 이게 깨지면 등반이 끝나 올라선 그 자리가 이미 발판 경계라, 방향이 한 번 바깥으로 뒤집히면
        /// 곧바로 다시 뛰어내린다(2026-08-29 사용자 신고의 필요조건).
        /// </summary>
        [Test]
        public void 맨틀_인셋이_모든_배율에서_경계_밴드를_넘어서야_한다()
        {
            StickConfig deployed = LoadDeployedConfig();

            float[] scales =
            {
                StickConfig.MinCharacterScale, 0.5f, 0.6531f, deployed.ResolveCharacterScale(),
                1f, 1.125f, 1.5f, StickConfig.MaxCharacterScale
            };

            foreach (float scale in scales)
            {
                float halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * scale;
                float stop = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance, halfWidth);
                float inset = DockGeometry.ResolveParkourMantleInset(deployed.parkourMantleInset, stop, halfWidth);
                float clearance = inset - stop;

                // 이 배율에서 유도가 실제로 이겼는가(= 설정 절대값이 더 이상 지배하지 않는가).
                bool derivationWins = inset > deployed.parkourMantleInset + 1e-4f;

                Debug.Log($"[DOCK-GEOM] 배율 {scale:F3} → 반폭 {halfWidth:F3}, 경계 판정 {stop:F3}, " +
                    $"맨틀 인셋 설정 {deployed.parkourMantleInset:F3} → 유도 {inset:F3} " +
                    $"({(derivationWins ? "유도 승" : "설정 승")}), 여유 {clearance:F4}");

                Assert.GreaterOrEqual(clearance, RequiredClearanceUnits,
                    $"배율 {scale:F3}에서 맨틀 인셋({inset:F3})이 경계 판정 거리({stop:F3})보다 " +
                    $"{RequiredClearanceUnits:F2} 넘게 크지 않습니다(여유 {clearance:F4}) — 이 배율의 " +
                    "사용자는 턱 위에 올라서자마자 같은 모서리로 다시 뛰어내립니다.");
            }
        }

        /// <summary>
        /// ★ <b>2026-09-01 범위 재조정</b> — 사용자 지시로 다이얼 상한이 1.5 → <b>1.0</b>이 되면서
        /// (<see cref="StickConfig.MaxCharacterScale"/>) 이 자리가 지키는 것이 <b>뒤집혔다</b>.
        ///
        /// <para><b>옛 의도</b>(상한 1.5 시절): "고정 상수가 버티는 천장(1.125)이 상한 <b>아래</b>다"
        /// = 사용자가 도달할 수 있는 <c>(1.125, 1.5]</c> 구간에 결함이 있다는 사실을 박제했다.
        /// 사용자 신고 <i>"캐릭터도 독 올라갈때 이상하게 올라감"</i>(당시 저장 배율 1.5)이 정확히
        /// 그 구간이었다.</para>
        ///
        /// <para><b>지금</b>: 상한이 1.0이라 <b>도달 가능한 모든 배율이 천장 아래</b>다. 그러므로 이제
        /// 잠글 값어치가 있는 성질은 정반대다 — <b>"결함 구간이 사거리 밖에 있다"</b>.
        /// 그래서 부등호를 뒤집는다. <b>상한을 다시 천장 위로 올리는 순간 이 테스트가 실패한다</b> —
        /// 그것이 정확히 원하는 동작이다.</para>
        ///
        /// <para>★★ <b>근본 결함은 고쳐지지 않았다.</b> "고정 상수가 배율에 비례하지 않는다"는 결함은
        /// 그대로 남아 있고, 이번 변경은 <b>수정이 아니라 사거리 축소</b>다
        /// (<see cref="StickConfig.MaxCharacterScale"/> 문서의 같은 취지 문단 참고). 그래서 유도
        /// (<see cref="DockGeometry.ResolveParkourMantleInset"/>)를 지우면 안 되고, 아래 (4)가 그것이
        /// 여전히 옳은지 계속 확인한다.</para>
        ///
        /// <para>천장 값(1.125)을 숫자로 적지 않는다 — 그 값을 만드는 상수들에서 매번 다시 계산한다
        /// (CLAUDE.md: 프로덕션 상수를 테스트에 베끼지 않는다). 설정 인셋이나 여유 요구치가 바뀌면
        /// 천장도 따라 움직여야 하고, 숫자를 적어 두면 그 순간 이 테스트가 거짓말을 시작한다.</para>
        /// </summary>
        [Test]
        public void 맨틀_인셋_결함_구간이_다이얼_상한_밖에_있다()
        {
            StickConfig deployed = LoadDeployedConfig();

            // 유도 함수를 우회한 옛 계산: 인셋 = 설정 절대값 고정.
            float ceiling = (deployed.parkourMantleInset
                             - DockGeometry.EdgeStopWallStandoffMarginUnits
                             - RequiredClearanceUnits)
                            / StickConfig.BaselineBodyPhysicsHalfWidth;

            float bigScale = StickConfig.MaxCharacterScale;
            float halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * bigScale;
            float stop = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance, halfWidth);
            float legacyClearance = deployed.parkourMantleInset - stop;   // 유도 없음 = 설정값 그대로.
            float derivedClearance =
                DockGeometry.ResolveParkourMantleInset(deployed.parkourMantleInset, stop, halfWidth) - stop;

            Debug.Log($"[DOCK-GEOM] (사거리) 고정 상수 {deployed.parkourMantleInset:F3}이 버티는 배율 천장 = " +
                $"{ceiling:F3} vs 다이얼 상한 {StickConfig.MaxCharacterScale:F2} " +
                $"→ {(ceiling > StickConfig.MaxCharacterScale ? "결함 구간이 사거리 밖" : "★ 사거리 안 — 노출됨")}. " +
                $"상한 배율에서 여유 — 유도 끔 {legacyClearance:F4} / 유도 켬 {derivedClearance:F4} " +
                $"(요구 {RequiredClearanceUnits:F2}). 배포 배율 {deployed.ResolveCharacterScale():F3}.");

            // ── (1) 결함 구간이 사거리 밖이다 ────────────────────────────────────────────
            // 상한을 천장 위로 다시 올리면 여기서 실패한다. 그때는 "사거리 축소"가 더 이상 성립하지
            // 않으므로, 유도를 손보든 상한을 되돌리든 **판단을 강제**하는 것이 이 단언의 목적이다.
            Assert.Greater(ceiling, StickConfig.MaxCharacterScale,
                $"고정 상수가 버티는 천장({ceiling:F3})이 다이얼 상한({StickConfig.MaxCharacterScale:F2}) " +
                "이하입니다 — 사용자가 도달할 수 있는 배율에 Dock 등반 결함 구간이 다시 열렸습니다. " +
                "근본 결함(고정 상수가 배율에 비례하지 않음)은 2026-09-01 시점에도 고쳐지지 않았고, " +
                "그때는 상한을 1.0으로 내려 사거리 밖으로 밀어낸 것뿐입니다(StickConfig.MaxCharacterScale " +
                "문서 참고). 상한을 올리려면 그 결함을 먼저 고치십시오.");

            // ── (2) 공허하지 않다 ───────────────────────────────────────────────────────
            // (1)만 있으면 "천장이 아주 커서 언제나 참"인 경우와 구분되지 않는다. 천장 **바로 위**
            // 배율에서는 고정 상수가 실제로 무너진다는 것을 같은 계산으로 보여, 천장이 진짜 경계임을
            // 박제한다. 이 한 줄이 없으면 (1)은 "항상 참인 단언"이 될 수 있다.
            float justAboveCeiling = ceiling * 1.05f;
            float aboveStop = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance,
                StickConfig.BaselineBodyPhysicsHalfWidth * justAboveCeiling);
            Assert.Less(deployed.parkourMantleInset - aboveStop, RequiredClearanceUnits,
                $"천장({ceiling:F3}) 바로 위 배율 {justAboveCeiling:F3}에서도 고정 상수만으로 여유가 " +
                $"{deployed.parkourMantleInset - aboveStop:F4}로 충분합니다 — 그렇다면 이 '천장'은 경계가 " +
                "아니고, 위 (1)은 아무것도 지키지 않습니다(천장 계산식을 확인하십시오).");

            // ── (3) 도달 가능한 양 끝에서 실제로 안전하다 ────────────────────────────────
            // 천장 비교는 유도된 부등식이다. 실제 값으로도 한 번 확인해 둔다 — 하한/배포/상한 셋.
            foreach (float scale in new[]
                     { StickConfig.MinCharacterScale, deployed.ResolveCharacterScale(), StickConfig.MaxCharacterScale })
            {
                float s = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance,
                    StickConfig.BaselineBodyPhysicsHalfWidth * scale);
                Assert.GreaterOrEqual(deployed.parkourMantleInset - s, RequiredClearanceUnits,
                    $"도달 가능한 배율 {scale:F3}에서 고정 상수만으로는 여유가 " +
                    $"{deployed.parkourMantleInset - s:F4}뿐입니다(요구 {RequiredClearanceUnits:F2}) — " +
                    "천장 계산과 실제 값이 어긋납니다.");
            }

            // ── (4) 유도는 여전히 옳아야 한다 ───────────────────────────────────────────
            // 지금은 유도가 없어도 도달 가능 구간이 안전하다. 그렇다고 유도를 지우면, 누가 상한을
            // 올리는 날 방어가 통째로 사라진다. 근본 결함이 남아 있는 한 이 검사는 계속 돈다.
            Assert.GreaterOrEqual(derivedClearance, RequiredClearanceUnits,
                $"유도를 켰는데도 배율 {bigScale:F2}의 여유가 {derivedClearance:F4}입니다 — " +
                "DockGeometry.ResolveParkourMantleInset의 여유 계산을 확인하세요.");
        }

        // ============================================================================
        // ★ (3-2) 탐지 도달거리 ≥ 평가 거리 (2026-08-31, "키우면 Dock 위로 안 올라옴")
        // ============================================================================
        //
        // 배회 AI는 경계 행동을 <b>걷기 구간당 딱 한 번</b>, "경계까지 남은 거리 ≤ 평가 거리"가 되는
        // 그 프레임에 추첨하고 실패하면 그 자리에서 돌아선다 — 그보다 가까이 가는 일이 없다.
        // 그러므로 탐지(GroundSensor의 경계 근접 게이트)는 <b>정확히 그 거리에서</b> 성립해야 한다.
        // 평가 거리는 배율에서 유도되는데(0.4×배율+0.10) 탐지 게이트는 0.5 절대값이었고,
        // 배율 1.0을 넘는 순간 평가 거리가 게이트를 추월해 되올라가기/내려가기가 <b>구조적으로</b>
        // 불가능해졌다. 아래 두 테스트가 그 짝을 잠근다(PlayMode 쪽은 DockStepUpCharacterScaleTests).

        [Test]
        public void 탐지_도달거리가_모든_배율에서_평가_거리를_덮어야_한다()
        {
            StickConfig deployed = LoadDeployedConfig();

            float[] scales =
            {
                StickConfig.MinCharacterScale, 0.5f, deployed.ResolveCharacterScale(),
                0.9f, 1f, 1.25f, 1.5f, StickConfig.MaxCharacterScale
            };

            foreach (float scale in scales)
            {
                float halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * scale;
                float stop = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance, halfWidth);
                float reach = DockGeometry.ResolveEdgeProbeReach(deployed.parkourDetectionRadius, stop);
                float clearance = reach - stop;
                bool derivationWins = reach > deployed.parkourDetectionRadius + 1e-4f;

                Debug.Log($"[DOCK-GEOM] 배율 {scale:F3} → 평가 거리 {stop:F3}, 탐지 도달거리 설정 " +
                    $"{deployed.parkourDetectionRadius:F3} → 유도 {reach:F3} " +
                    $"({(derivationWins ? "유도 승" : "설정 승")}), 여유 {clearance:F4}");

                Assert.GreaterOrEqual(clearance, RequiredClearanceUnits,
                    $"배율 {scale:F3}에서 탐지 도달거리({reach:F3})가 평가 거리({stop:F3})를 " +
                    $"{RequiredClearanceUnits:F2} 넘는 여유로 덮지 못합니다(여유 {clearance:F4}) — " +
                    "이 배율의 사용자는 경계에서 추첨은 도는데 대상이 잡히지 않아 Dock을 오르내릴 수 없습니다.");
            }
        }

        /// <summary>
        /// ★ 위 테스트의 <b>네거티브 컨트롤</b> — "유도를 끄면 실제로 깨진다"를 박제한다.
        /// 유도 함수를 우회하고 옛 절대값(parkourDetectionRadius)을 그대로 게이트로 쓰는 계산을
        /// 재현해, 그 방식이 무너지는 배율 천장을 계산하고 그것이
        /// <see cref="StickConfig.MaxCharacterScale"/> <b>안쪽</b>임을 확인한다(숫자를 베끼지 않는다 —
        /// 상한이 바뀌면 이 관계가 자동으로 다시 평가되어야 한다).
        /// 이 단언이 실패한다면 유도 없이도 안전하다는 뜻이므로 위 테스트는 아무것도 지키지 않는다.
        ///
        /// <para>★ 2026-09-01 다이얼 상한이 1.5 → 1.0으로 내려온 뒤에도 <b>이 네거티브 컨트롤은
        /// 유효하다</b>. 천장이 0.875라 도달 가능한 구간 <c>[0.875, 1.00]</c>이 여전히 결함에 노출돼
        /// 있기 때문이다 — 맨틀 인셋 쪽(위 (3-1))과 달리 여기는 사거리 축소로 해결되지 않았다.</para>
        /// </summary>
        [Test]
        public void 네거티브컨트롤_유도를_끄면_큰_배율에서_탐지가_평가거리를_못_따라간다()
        {
            StickConfig deployed = LoadDeployedConfig();

            // 옛 방식이 버티는 천장 = 고정 게이트가 **요구 여유까지** 대주는 마지막 배율.
            //   parkourDetectionRadius ≥ (0.4×배율 + EdgeStopWallStandoffMargin) + RequiredClearance
            //
            // ★ 2026-09-01 수정 — 여기서 RequiredClearanceUnits가 빠져 있었다. 그래서 이 식은 "여유가
            //   0이 되는 배율"(1.000)을 천장이라 불렀는데, 아래 단언들이 재는 것은 "여유가 요구치에
            //   못 미치는가"다. 둘이 어긋난 채로 다이얼 상한이 1.5 → 1.0으로 내려오자 천장(1.000)과
            //   상한(1.00)이 정확히 같아져 `Assert.Less`가 경계에서 깨졌다. 실패의 원인은 상한 변경이
            //   아니라 **이 식의 정의가 위 테스트와 달랐던 것**이다(위 (3-1) 맨틀 쪽은 처음부터
            //   RequiredClearanceUnits를 빼고 있었다 — 두 식이 같은 뜻이어야 한다).
            //
            //   고친 뒤 천장은 (0.5 - 0.10 - 0.05) / 0.4 = 0.875로, 다이얼 상한 1.00보다 **아래**다.
            //   즉 배율 0.875~1.00 구간의 사용자는 유도가 없으면 실제로 Dock을 오르내릴 수 없다 —
            //   이 네거티브 컨트롤은 여전히 <b>살아 있는 결함</b>을 박제하고 있다.
            float ceiling = (deployed.parkourDetectionRadius
                             - DockGeometry.EdgeStopWallStandoffMarginUnits
                             - RequiredClearanceUnits)
                            / StickConfig.BaselineBodyPhysicsHalfWidth;

            float bigScale = StickConfig.MaxCharacterScale;
            float halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * bigScale;
            float stop = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance, halfWidth);
            float legacyClearance = deployed.parkourDetectionRadius - stop;                       // 유도 없음.
            float derivedClearance = DockGeometry.ResolveEdgeProbeReach(deployed.parkourDetectionRadius, stop) - stop;

            // 배포 배율에서는 유도값이 설정값과 정확히 같아야 한다(= 지금 화면 거동 무변경).
            float deployedScale = deployed.ResolveCharacterScale();
            float deployedStop = DockGeometry.ResolveEdgeStopDistance(deployed.wanderEdgeStopDistance,
                StickConfig.BaselineBodyPhysicsHalfWidth * deployedScale);
            float deployedReach = DockGeometry.ResolveEdgeProbeReach(deployed.parkourDetectionRadius, deployedStop);

            Debug.Log($"[DOCK-GEOM] (네거티브 컨트롤) 옛 절대 게이트 {deployed.parkourDetectionRadius:F3}이 " +
                $"버티는 배율 천장 = {ceiling:F3}. 배율 {bigScale:F2}에서 여유 — 유도 끔 {legacyClearance:F4} / " +
                $"유도 켬 {derivedClearance:F4}. 배포 배율 {deployedScale:F3}에서는 유도값 {deployedReach:F4} " +
                $"= 설정값 {deployed.parkourDetectionRadius:F4}(거동 무변경).");

            Assert.Less(ceiling, StickConfig.MaxCharacterScale,
                $"옛 절대 게이트가 버티는 천장({ceiling:F3})이 다이얼 상한({StickConfig.MaxCharacterScale:F2}) " +
                "이상입니다 — 그렇다면 유도가 없어도 안전하다는 뜻이라 위 테스트가 아무것도 지키지 않습니다.");

            Assert.Less(legacyClearance, RequiredClearanceUnits,
                $"유도를 끈 계산에서도 배율 {bigScale:F2}의 여유가 {legacyClearance:F4}로 충분합니다 — " +
                "네거티브 컨트롤이 성립하지 않습니다(재현 조건이 바뀌었는지 확인하세요).");

            Assert.GreaterOrEqual(derivedClearance, RequiredClearanceUnits,
                $"유도를 켰는데도 배율 {bigScale:F2}의 여유가 {derivedClearance:F4}입니다 — " +
                "DockGeometry.ResolveEdgeProbeReach의 여유 계산을 확인하세요.");

            Assert.AreEqual(deployed.parkourDetectionRadius, deployedReach, 1e-4f,
                $"배포 배율({deployedScale:F3})에서 유도값({deployedReach:F4})이 설정값" +
                $"({deployed.parkourDetectionRadius:F4})과 다릅니다 — 이 수정은 지금 화면의 거동을 " +
                "바꾸지 않는다는 전제가 깨졌습니다(배포 배율이나 여유 상수가 바뀐 것인지 확인하세요).");
        }

        // ============================================================================
        // (4) 진짜 금지 조합 — 내려갈 길이 하나도 없는 설정 (M1 재정의)
        // ============================================================================

        [Test]
        public void 매달리기_확률이_0이면_안_된다()
        {
            StickConfig deployed = LoadDeployedConfig();

            // 배율이 분기 배율(약 0.653) 아래로 내려가면 Dock 단차는 '뛰어내리기' 밴드를 벗어나
            // '매달려 내려가기'가 유일한 하강 경로가 된다. 그 확률이 0이면 캐릭터는 Dock 모서리에서
            // 영원히 되돌아서기만 한다 — 이것이 배율 하한이 아니라 **진짜 금지 조합**이다.
            Assert.Greater(deployed.ledgeHangChance, 0f,
                $"ledgeHangChance({deployed.ledgeHangChance:F2})가 0입니다 — 캐릭터 배율이 " +
                $"{StickConfig.DockHopDownCriticalScale:F3} 아래이거나 사용자의 Dock tilesize가 커서 " +
                "낙차가 매달리기 최소치를 넘으면, 뛰어내리기도 매달리기도 성립하지 않아 Dock 위에 갇힙니다.");
            Assert.Greater(deployed.hopDownChance, 0f,
                $"hopDownChance({deployed.hopDownChance:F2})가 0입니다 — 작은 tilesize(낙차가 매달리기 " +
                "최소치보다 작은 경우)에서 내려갈 방법이 사라집니다.");
            Assert.Greater(deployed.stepUpChance, 0f,
                $"stepUpChance({deployed.stepUpChance:F2})가 0입니다 — 내려갈 수는 있어도 되올라올 수 " +
                "없습니다(왕복의 절반만 성립).");
        }
    }
}
