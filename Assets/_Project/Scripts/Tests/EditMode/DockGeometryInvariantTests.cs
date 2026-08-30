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
                "BottomSafetyNetInsetPoints가 8pt가 아닙니다 — 바뀌었다면 Dock 낙차/stepUpMaxHeight/" +
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

            foreach (float tile in tileSizes)
            {
                float drop = DockGeometry.DockDropWorldUnits(deployed, tile);
                float resolved = DockGeometry.ResolveStepUpMaxHeight(deployed.stepUpMaxHeight, drop);

                Debug.Log($"[DOCK-GEOM] tilesize={tile:F0}pt → 낙차 {drop:F4}유닛, " +
                    $"설정 상한 {deployed.stepUpMaxHeight:F3} → 유도 상한 {resolved:F4}유닛 " +
                    $"(여유 {(resolved - drop):F4})");

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
            // stepUpMaxHeight가 3.57유닛 이상으로 올라갔다는 뜻이고, 그때는 DockGeometry의 유도가
            // 불필요해진 것이 아니라 **일반 창까지 자동 등반 대상이 됐다**는 뜻이므로 재검토해야 한다.
            float maxDrop = DockGeometry.DockDropWorldUnits(deployed, DockGeometry.MaxTileSizePoints);
            Assert.Less(deployed.stepUpMaxHeight, maxDrop,
                $"stepUpMaxHeight({deployed.stepUpMaxHeight:F3})가 최대 tilesize의 낙차({maxDrop:F3})를 " +
                "절대값만으로 덮고 있습니다 — 값이 이렇게 커지면 Dock이 아닌 일반 창 발판까지 " +
                "0.5초 만에 순간이동하듯 기어오르게 됩니다. 유도 방식(DockGeometry)으로 되돌리세요.");
        }

        // ============================================================================
        // (3-b) ★ 교차점 정밀 — 설정 절대값이 낙차를 못 덮기 시작하는 정확한 tilesize
        //       (2026-08-30 R3 M2: PlayMode 테스트와 R2 보고서 표가 이 경계를 **한 칸 이르게** 적었다)
        // ============================================================================

        /// <summary>
        /// R3 리뷰가 잡은 산술 오기의 박제. `DockTileSizeStepUpTests`의 네거티브 컨트롤 게이트가
        /// `tileSizePoints >= 80f`였는데, 실제 교차점은 80이 아니라 **80.2**다.
        ///     stepUpMaxHeight 2.400유닛 ÷ ReferenceWorldUnitsPerPoint(24/982 = 0.0244399) = 98.2pt
        ///     낙차(pt) = tilesize + 26 − 8 = tilesize + 18   ⇒  tilesize = 80.2
        /// 그래서 **80 → 2.3951은 아직 덮고, 81 → 2.4196부터 못 덮는다.**
        ///
        /// 이 검증을 PlayMode가 아니라 여기 두는 이유: PlayMode는 OS↔월드 좌표를 왕복하며 재기 때문에
        /// 허용오차(0.02유닛)가 붙는데, 교차점과 tilesize 80의 거리는 0.005유닛뿐이라 **측정 노이즈가
        /// 부등호를 뒤집을 수 있다**. 여기(순수 산술)에는 그 오차가 존재하지 않는다.
        ///
        /// 네거티브 컨트롤: 아래 CoveredTileSize를 81로, NotCoveredTileSize를 80으로 맞바꾸면
        /// 두 단언이 즉시 실패한다(2026-08-30 디버거가 실제로 뒤집어 확인).
        /// </summary>
        [Test]
        public void 설정_절대값_커버리지_교차점은_tilesize_80과_81_사이다()
        {
            const float CoveredTileSize = 80f;      // 여기까지는 절대값 2.4가 아직 덮는다
            const float NotCoveredTileSize = 81f;   // 여기부터 못 덮는다(= 유도가 없으면 갇힌다)

            StickConfig deployed = LoadDeployedConfig();
            float configured = deployed.stepUpMaxHeight;

            float crossoverTileSize = configured / DockGeometry.ReferenceWorldUnitsPerPoint
                - deployed.dockThicknessTilePaddingPoints
                + NullPlatformWindowService.BottomSafetyNetInsetPoints;

            float coveredDrop = DockGeometry.DockDropWorldUnits(deployed, CoveredTileSize);
            float notCoveredDrop = DockGeometry.DockDropWorldUnits(deployed, NotCoveredTileSize);

            Debug.Log($"[DOCK-GEOM] 절대값 커버리지 교차 tilesize = {crossoverTileSize:F2}pt " +
                $"(stepUpMaxHeight {configured:F3}유닛). " +
                $"tilesize {CoveredTileSize:F0} → 낙차 {coveredDrop:F5}유닛 (여유 {(configured - coveredDrop):F5}) / " +
                $"tilesize {NotCoveredTileSize:F0} → 낙차 {notCoveredDrop:F5}유닛 (부족 {(notCoveredDrop - configured):F5})");

            Assert.Greater(configured, coveredDrop,
                $"tilesize {CoveredTileSize:F0}pt의 낙차({coveredDrop:F5})를 설정 절대값({configured:F3})이 " +
                "못 덮습니다 — 교차점이 80 아래로 내려왔다는 뜻이니 이 경계를 문서/테스트 전부에서 재산출하세요.");
            Assert.Less(configured, notCoveredDrop,
                $"tilesize {NotCoveredTileSize:F0}pt의 낙차({notCoveredDrop:F5})를 설정 절대값({configured:F3})이 " +
                "아직 덮고 있습니다 — 교차점이 81 위로 올라갔다는 뜻이니 마찬가지로 재산출하세요.");

            Assert.That(crossoverTileSize, Is.GreaterThan(CoveredTileSize).And.LessThan(NotCoveredTileSize),
                $"유도한 교차 tilesize({crossoverTileSize:F2}pt)가 {CoveredTileSize:F0}~{NotCoveredTileSize:F0} " +
                "구간 밖입니다 — 위 두 단언과 모순되므로 환산 상수(ReferenceWorldUnitsPerPoint)나 " +
                "안전망 인셋이 바뀐 것입니다.");

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
        /// ★ 정직한 한계의 기록 — 맨틀 인셋만은 아직 **배율 전 구간에서 안전하지 않다.**
        /// 경계 판정 거리는 유도라 배율을 따라가지만 parkourMantleInset은 아직 고정 설정값이라,
        /// 배율이 커지면 "올라선 자리가 이미 경계"가 다시 성립한다. 그 천장을 여기서 계산해 남긴다 —
        /// 캐릭터 크기 다이얼을 실제로 켜는 라운드는 이 값을 반드시 보고 맨틀 인셋도 유도로 바꿔야 한다.
        /// </summary>
        [Test]
        public void 맨틀_인셋이_버티는_배율_천장을_기록한다()
        {
            StickConfig deployed = LoadDeployedConfig();
            float scale = deployed.ResolveCharacterScale();

            // parkourMantleInset > 0.4 x s + EdgeStopWallStandoffMarginUnits + RequiredClearanceUnits
            float ceiling = (deployed.parkourMantleInset
                             - DockGeometry.EdgeStopWallStandoffMarginUnits
                             - RequiredClearanceUnits)
                            / StickConfig.BaselineBodyPhysicsHalfWidth;

            Debug.Log($"[DOCK-GEOM] 맨틀 인셋 {deployed.parkourMantleInset:F3}이 버티는 배율 천장 = " +
                $"{ceiling:F3} (현재 배포 배율 {scale:F3}, 다이얼 상한 {StickConfig.MaxCharacterScale:F2}). " +
                "★ 다이얼을 켜는 라운드는 parkourMantleInset도 유도값으로 바꿔야 한다.");

            Assert.Less(scale, ceiling,
                $"배포 배율({scale:F3})이 맨틀 인셋이 버티는 천장({ceiling:F3})을 넘었습니다 — " +
                "parkourMantleInset을 올리거나 유도값으로 바꾸세요(Core/DockGeometry.ResolveEdgeStopDistance " +
                "와 같은 형태).");
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
