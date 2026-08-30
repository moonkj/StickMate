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
    /// ★ 캐릭터 크기 배율(StickConfig.characterScale) 불변성 검증 — 2026-08-29 사용자 요구
    /// "캐릭터 사이즈가 지금의 절반정도 되어야함 추후 사이즈 조정가능해야하고".
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// 크기를 바꾸는 것 자체는 상수 하나를 곱하면 끝이지만, 이 프로젝트에서 진짜 위험한 것은
    /// **크기가 바뀌었는데 따라오지 않은 값**이다. 그런 값은 예외도 경고도 내지 않고 조용히 거동만
    /// 바꾼다(캐릭터가 발판에 살짝 파묻히거나, Dock에서 내려간 뒤 못 올라오거나, 화면 끝에서 몸이
    /// 잘리거나). 그래서 아래 네 축을 **절대 조건**으로 못박는다:
    ///
    ///   (0) 프리팹 실측 치수 = StickConfig.characterScale x 배율 1.0 기준 신장.
    ///       Core/StickmanMetrics.cs(런타임 단일 조회 경로)가 그 값을 그대로 돌려준다.
    ///   (a) 발판 위에 **정확히** 선다 — 발 높이 == 발판 상단(오차 0.05유닛 이내), 몸이 발판 위로
    ///       전신 높이만큼 온전히 올라와 있다.
    ///   (b) Dock 단차(**1.6375유닛**, Core/DockGeometry.cs가 유도)에서 뛰어내렸다가 되올라오는
    ///       **왕복**이 성립한다.
    ///   (c) 화면 밖으로 나가지 않는다 — 렌더러 전체 바운즈가 화면 가로 범위 안에 남는다.
    ///
    /// ============================================================================
    /// ★ 2026-08-30 정정 — 이 파일의 핵심 계산이 **2배 틀려 있었다**(횡단 리뷰 M1)
    /// ============================================================================
    /// 이 파일은 Dock 낙차를 0.855유닛으로 하드코딩하고 있었다. 그 값은 바닥 안전망이 화면 최하단
    /// 40pt 위였던 시절의 화석이고, 2026-08-29 라운드에 (a) 안전망이 8pt로 내려가고 (b) Dock 두께가
    /// 하드코딩 75pt에서 tilesize+26 파생으로 바뀌면서 실제 낙차는 67pt = **1.6375유닛**이 됐다.
    /// 낙차가 절반으로 과소평가돼 있었으므로 아래 임계 배율 계산도 절반이었다(0.341 vs 실제 0.6531).
    /// 이제 낙차는 Core/DockGeometry.cs 한 곳에서만 유도한다.
    ///
    /// ============================================================================
    /// Dock 단차와 배율의 상호작용 — 이 임계값이 **무엇을 뜻하고 무엇을 뜻하지 않는가**
    /// ============================================================================
    /// 매달리기 최소 낙차(StickmanBlackboard.LedgeHangMinDropDepth)는 어깨 높이 + 팔 길이, 즉 프리팹
    /// 치수에서 유도되므로 배율 s에 정확히 비례한다(배율 1.0에서 약 2.5072). 뛰어내리기 밴드는
    /// [hopDownMinDropHeight, LedgeHangMinDropDepth)이므로 Dock 단차가 그 밴드에 남으려면
    ///     2.5072 x s &gt; 1.6375   ->   s &gt; **0.6531**
    ///
    /// ★★ 그런데 이 부등식을 "슬라이더 하한(MinCharacterScale)이 반드시 넘어야 하는 금지선"으로
    ///    쓰는 것은 **틀렸다**. 두 가지 반증이 있다(2026-08-30 디버거 검증):
    ///   (반증 1) 낙차 자체가 사용자 설정이다. 낙차 = tilesize + 18pt이고 macOS tilesize 범위는
    ///            16~128이라 임계 배율도 함께 움직인다:
    ///                tilesize  16 → 낙차 0.831유닛 → 임계 배율 0.331
    ///                tilesize  48 → 1.613 → 0.643      (macOS 기본 tilesize)
    ///                tilesize  49 → 1.637 → 0.653      (이 개발 머신)
    ///                tilesize  59 → 1.882 → 0.751      ← **기본 배율 0.75가 이미 여기서 깨진다**
    ///                tilesize 128 → 3.568 → 1.423
    ///            즉 어떤 하한을 넣어도 tilesize 59 이상인 사용자에게는 기본 배율에서 이미 부등식이
    ///            성립하지 않는다. 슬라이더 하한으로는 구조적으로 막을 수 없는 조건이다.
    ///   (반증 2) 부등식이 깨졌을 때 실제로 일어나는 일은 **고장이 아니다**. Dock 단차가 '매달려
    ///            내려가기'로 분류될 뿐이고, 매달리기는 "낙차 ≥ 손끝~발끝 거리"일 때만 선택되므로
    ///            그 구간에서 매달린 발끝은 착지면을 지나치지 않는다(기하학적으로 안전한 쪽이다).
    ///            예전 주석의 "발이 이미 목적지를 지나쳐 어색해진다"는 부등호 방향이 반대였다.
    ///
    /// 그래서 아래 테스트는 임계 배율을 **문서 상수와 일치하는지 재계산해 잠그되**, 금지선으로는
    /// 두 개의 진짜 조건만 단언한다:
    ///   (진짜 1) 되올라가기 상한이 낙차를 덮는다 — 못 덮으면 **영영 못 올라온다**(진짜 갇힘).
    ///            tilesize 의존성은 DockGeometry.ResolveStepUpMaxHeight가 런타임에 흡수한다(M3).
    ///   (진짜 2) 어느 배율에서든 내려갈 길이 **최소 하나**는 열려 있다(뛰어내리기 또는 매달리기).
    ///            둘 다 막히면 캐릭터가 Dock 모서리에서 영원히 되돌아서기만 한다.
    ///
    /// 상한은 hopDownMinDropHeight를 **절대값으로 남겨둔 덕분에 존재하지 않는다**(0.35 &lt;= 1.6375는
    /// 배율과 무관하게 항상 참). 이 값을 캐릭터 비례로 바꾸면 0.35 x s &lt;= 1.6375, 즉 s &lt;= 4.68이라는
    /// 상한이 새로 생긴다 — 그래도 슬라이더 상한 2.0보다는 위지만, 절대값으로 두는 편이 여전히 넓다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤(이 테스트가 정말 무언가를 보고 있는가)
    /// ============================================================================
    /// StickConfig.characterScale을 1.0으로 되돌리고 프리팹/씬을 다시 구우면 (0)의 신장 단언이
    /// 즉시 실패한다(1.137 기대 vs 2.275 실측). Editor/SceneBootstrapper.cs에서 bodyScale 곱을 하나만
    /// 빠뜨려도 (0)이 그 부위를 짚어 실패한다.
    /// </summary>
    public sealed class CharacterScaleInvarianceTests
    {
        private const string LogPrefix = "[SCALE-TEST]";

        /// <summary>★ Dock 상단 → 바닥 안전망 상단 낙차(월드 유닛). **하드코딩하지 않는다** —
        /// Core/DockGeometry.cs가 (tilesize + dockThicknessTilePaddingPoints − BottomSafetyNetInsetPoints)를
        /// 월드로 환산해 주는 단일 소스다(이 개발 머신 tilesize=49 → 67pt → 1.63747유닛).
        /// 2026-08-30 횡단 리뷰 M1: 이 값이 파일마다 0.855(안전망이 40pt 위였던 시절의 화석) / 1.6375로
        /// 갈라져 있었고, 그 탓에 배율 불변식 테스트가 실제 시스템이 아니라 자기 상수를 지키고 있었다.</summary>
        private static readonly float DockDropUnits = DockGeometry.ReferenceDockDropWorldUnits;

        private const long DockHandle = 9101L;
        private const long LeftFloorHandle = 9102L;
        private const long RightFloorHandle = 9103L;

        private const float SettleWaitSeconds = 2.5f;
        private const float MaxObserveSeconds = 12f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested { get; set; }
            public bool HopDownRequested { get; set; }
            public bool StepUpRequested { get; set; }
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private FootholdPoller _poller;
        private ScriptedIntentSource _intent;

        private float _dockTopWorldY;
        private float _floorTopWorldY;
        private float _dockLeftWorldX;
        private float _dockRightWorldX;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        // ============================================================================
        // (0) 프리팹 실측 치수가 배율을 따라온다 + 단일 조회 경로가 같은 값을 돌려준다
        // ============================================================================

        [UnityTest]
        public IEnumerator PrefabGeometryFollowsCharacterScale()
        {
            yield return LoadSceneAndFindAgent();

            StickConfig shipped = _agent.Config;
            Assert.IsNotNull(shipped, $"{LogPrefix} StickmanAgent에 StickConfig가 배선돼 있지 않습니다.");

            float scale = shipped.ResolveCharacterScale();
            float expectedHeight = StickConfig.BaselineCharacterTotalHeight * scale;

            // ── 물리 캡슐(비-트리거)이 전신 높이의 원본이다.
            CapsuleCollider2D physicsCapsule = null;
            foreach (CapsuleCollider2D c in _agent.GetComponents<CapsuleCollider2D>())
            {
                if (c != null && !c.isTrigger) physicsCapsule = c;
            }
            Assert.IsNotNull(physicsCapsule, $"{LogPrefix} 루트에 비-트리거 CapsuleCollider2D가 없습니다.");

            var metrics = _agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics,
                $"{LogPrefix} 루트에 StickmanMetrics가 없습니다 — 렌더러들이 쓰는 실측 치수 단일 조회 경로가 " +
                "프리팹에 배치되지 않았습니다(Editor/SceneBootstrapper.cs 회귀). 프리팹을 --force로 다시 구우세요.");

            Debug.Log($"{LogPrefix} 배율={scale:F3} — 기대 신장={expectedHeight:F4}, 캡슐 size.y={physicsCapsule.size.y:F4}, " +
                $"metrics.TotalHeight={metrics.TotalHeight:F4}, metrics.Scale={metrics.Scale:F4}, " +
                $"머리중심={metrics.HeadCenterLocalY:F4}, 머리반경={metrics.HeadRadius:F4}, 어깨={metrics.ShoulderLocalY:F4}, " +
                $"엉덩이={metrics.HipLocalY:F4}, 계층실측성공={metrics.MeasuredFromHierarchy}");

            // ★ 절대 조건 — 프리팹이 실제로 배율만큼 작아져 있어야 한다.
            Assert.AreEqual(expectedHeight, physicsCapsule.size.y, 0.001f,
                $"{LogPrefix} 프리팹 전신 높이({physicsCapsule.size.y:F4})가 characterScale({scale:F3})에서 기대되는 " +
                $"{expectedHeight:F4}와 다릅니다 — 프리팹이 새 배율로 다시 구워지지 않았거나 " +
                "SceneBootstrapper의 배율 적용이 빠졌습니다.");
            Assert.AreEqual(expectedHeight, metrics.TotalHeight, 0.001f,
                $"{LogPrefix} StickmanMetrics.TotalHeight가 실제 프리팹 치수와 어긋납니다 — 단일 조회 경로가 " +
                "굽힌 상수를 복사하고 있다는 뜻입니다(계층 실측이어야 합니다).");
            Assert.AreEqual(scale, metrics.Scale, 0.001f,
                $"{LogPrefix} StickmanMetrics.Scale({metrics.Scale:F4})이 characterScale({scale:F3})과 다릅니다.");
            Assert.IsTrue(metrics.MeasuredFromHierarchy,
                $"{LogPrefix} StickmanMetrics가 계층 실측에 실패해 폴백 비율을 썼습니다 — 프리팹 계층 " +
                "(Head / LeftArm / LeftLeg / 비-트리거 캡슐)이 바뀌었습니다.");

            // ── 부위별 비율도 배율 1.0의 실루엣을 그대로 유지해야 한다(각도는 안 줄이고 길이만 줄인다).
            AssertRatio(metrics.HeadCenterLocalY, 2.0546944f * scale, "머리 중심 Y");
            AssertRatio(metrics.HeadRadius, 0.22f * scale, "머리 반경");
            AssertRatio(metrics.ShoulderLocalY, 1.7646944f * scale, "어깨 Y");
            AssertRatio(metrics.HipLocalY, 0.9346944f * scale, "엉덩이 Y");

            // ── 파생값(매달리기 최소 낙차)도 자동으로 따라와야 한다 — 이게 "단일 소스"의 증거다.
            StickmanBlackboard bb = _agent.Blackboard;
            float expectedHang = 2.5072f * scale;
            Debug.Log($"{LogPrefix} 매달리기 최소 낙차 실측={bb.LedgeHangMinDropDepth:F4}(기대 {expectedHang:F4}).");
            Assert.AreEqual(expectedHang, bb.LedgeHangMinDropDepth, 0.02f,
                $"{LogPrefix} 매달리기 최소 낙차가 배율을 따라오지 않았습니다 — 팔 길이/어깨 높이 중 하나에 " +
                "배율이 적용되지 않았다는 뜻입니다(StickmanPoseAnimator가 프리팹을 실측하므로 원인은 프리팹입니다).");
        }

        // ============================================================================
        // ★ 치수 조회 경로 단일화 잠금 (2026-08-29 리더 지시 — 같은 라운드에 단일 소스가 둘로 갈렸다)
        // ============================================================================
        // Core/StickmanAgent.CharacterTotalHeightWorld(렌더러 3종이 이미 참조 중)와
        // Core/StickmanMetrics.TotalHeight가 각각 "캐릭터 전신 높이"를 계산하고 있었다. 지금은 전자가
        // 후자에 **위임**하도록 통합했고, 이 테스트가 그 통합이 풀리는 회귀를 잡는다 — 누군가 다시
        // 독립 계산을 넣으면 두 값이 어긋나는 순간 여기서 빨간불이 난다.
        [UnityTest]
        public IEnumerator BothHeightQueryPathsAgree()
        {
            yield return LoadSceneAndFindAgent();

            var metrics = _agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} 루트에 StickmanMetrics가 없습니다.");

            float viaAgent = _agent.CharacterTotalHeightWorld;
            float viaMetrics = metrics.TotalHeight;
            float viaProperty = _agent.Metrics.TotalHeight;
            float expected = StickConfig.BaselineCharacterTotalHeight * _agent.Config.ResolveCharacterScale();

            Debug.Log($"{LogPrefix} 조회 경로 일치 — StickmanAgent.CharacterTotalHeightWorld={viaAgent:F4}, " +
                $"StickmanMetrics.TotalHeight={viaMetrics:F4}, StickmanAgent.Metrics.TotalHeight={viaProperty:F4}, " +
                $"기대={expected:F4}");

            // ★ 절대 조건 — 세 경로가 완전히 같은 값(위임이므로 오차 0이어야 한다).
            Assert.AreEqual(viaMetrics, viaAgent, 0.0001f,
                $"{LogPrefix} StickmanAgent.CharacterTotalHeightWorld가 StickmanMetrics와 다른 값을 돌려줍니다 — " +
                "위임이 풀리고 독립 계산이 되살아났습니다(단일 소스 회귀).");
            Assert.AreEqual(viaMetrics, viaProperty, 0.0001f,
                $"{LogPrefix} StickmanAgent.Metrics가 프리팹의 StickmanMetrics와 다른 인스턴스를 가리킵니다.");
            // ★ 절대 조건 — 그 값이 배율을 실제로 따라간다(둘이 같은 '틀린 값'인 경우를 배제한다).
            Assert.AreEqual(expected, viaAgent, 0.001f,
                $"{LogPrefix} 두 경로가 일치하긴 하지만 characterScale이 반영되지 않은 값입니다.");
            // 그리고 렌더러 3종이 폴백으로 쓰는 상수(2.27)와는 확실히 달라야 한다 — 절반 크기에서
            // 폴백이 쓰이고 있으면 머리 위 연출이 캐릭터 두 배 높이에 뜬다.
            Assert.Less(viaAgent, StickConfig.BaselineCharacterTotalHeight * 0.95f,
                $"{LogPrefix} 전신 높이가 배율 1.0 기준값과 사실상 같습니다 — 프리팹이 절반 크기로 다시 " +
                "구워지지 않았거나 렌더러 폴백 상수(2.27)가 쓰이고 있습니다.");
        }

        private static void AssertRatio(float actual, float expected, string label)
        {
            Assert.AreEqual(expected, actual, 0.002f,
                $"{LogPrefix} {label}이(가) 배율을 따라오지 않았습니다(기대 {expected:F4}, 실측 {actual:F4}) — " +
                "SceneBootstrapper에서 그 부위의 bodyScale 곱이 빠졌습니다.");
        }

        // ============================================================================
        // Dock 단차 임계 배율 계산 잠금 — 이 파일의 핵심 계산(클래스 문서 참고)
        // ============================================================================

        [UnityTest]
        public IEnumerator DockHopDownBandSurvivesScale()
        {
            yield return LoadSceneAndFindAgent();

            StickmanBlackboard bb = _agent.Blackboard;
            StickConfig cfg = _agent.Config;
            float scale = cfg.ResolveCharacterScale();

            float hangMin = bb.LedgeHangMinDropDepth;      // = 2.5072 x scale
            float hopMin = cfg.hopDownMinDropHeight;       // 절대값(0.35) — 배율과 무관
            float hangPerScale = hangMin / scale;          // 배율 1.0에서의 매달리기 최소 낙차
            float criticalScale = DockDropUnits / hangPerScale;

            float minScaleHangMin = hangPerScale * StickConfig.MinCharacterScale;
            float resolvedStepUpMax = DockGeometry.ResolveStepUpMaxHeight(cfg.stepUpMaxHeight, DockDropUnits);

            Debug.Log($"{LogPrefix} Dock 밴드 — 배율={scale:F3}, 뛰어내리기 밴드=[{hopMin:F3}, {hangMin:F3}), " +
                $"Dock 낙차={DockDropUnits:F4}(tilesize {DockGeometry.DeveloperMachineTileSizePoints:F0} 실측 파생), " +
                $"배율 1.0 기준 매달리기 최소={hangPerScale:F4}, 임계 배율={criticalScale:F4}, " +
                $"슬라이더 하한={StickConfig.MinCharacterScale:F3}, 문서 상수={StickConfig.DockHopDownCriticalScale:F4}, " +
                $"기본 배율 여유={(hangMin - DockDropUnits):F4}유닛, " +
                $"되올라가기 상한 설정값={cfg.stepUpMaxHeight:F3} -> 유도값={resolvedStepUpMax:F3}");

            // ★ 절대 조건 1 — 뛰어내리기 하한이 Dock 낙차보다 작다(밴드의 아래쪽 끝).
            // 이 조건은 배율과 무관하다(hopDownMinDropHeight가 절대값이므로). 깨지면 캐릭터가 Dock
            // 경계에서 아무 것도 하지 않고 되돌아서기만 한다.
            Assert.LessOrEqual(hopMin, DockDropUnits,
                $"{LogPrefix} 뛰어내리기 하한({hopMin:F3})이 Dock 낙차({DockDropUnits:F3})보다 큽니다 — " +
                "Dock 경계에서 캐릭터가 아무 것도 하지 않고 되돌아서기만 합니다.");

            // ★ 절대 조건 2 (2026-08-30 교체) — "내려갈 길이 최소 하나는 열려 있다".
            // 예전 조건("슬라이더 하한 > 임계 배율")은 폐기했다. 근거는 클래스 문서의 반증 1·2 —
            // 임계 배율은 tilesize에 따라 움직이므로(tilesize 59에서 이미 기본 배율 0.75를 넘어선다)
            // 슬라이더 하한으로는 구조적으로 지킬 수 없고, 깨졌을 때의 결과도 고장이 아니라 '매달리기'
            // 분기일 뿐이다. 진짜로 막아야 하는 것은 **양쪽 다 막히는** 경우다.
            bool hopDownApplies = hopMin <= DockDropUnits && DockDropUnits < hangMin;
            bool hangApplies = DockDropUnits >= hangMin && cfg.ledgeHangChance > 0f;
            Assert.IsTrue(hopDownApplies || hangApplies,
                $"{LogPrefix} 현재 배율({scale:F3})에서 Dock 낙차({DockDropUnits:F3})에 대해 뛰어내리기도 " +
                $"매달리기도 성립하지 않습니다(밴드=[{hopMin:F3}, {hangMin:F3}), ledgeHangChance={cfg.ledgeHangChance:F2}) — " +
                "캐릭터가 Dock 모서리에서 영원히 되돌아서기만 합니다.");

            // ★ 절대 조건 2b — 슬라이더를 **하한까지 내려도** 위 성질이 유지된다(사용자가 UI로 깨뜨릴 수
            // 없다는 원래 의도는 살리되, 올바른 형태로). 하한 배율에서는 매달리기가 담당하게 되므로
            // ledgeHangChance가 0이면 여기서 빨간불이 난다 — 그것이 진짜 금지 조합이다.
            bool hopDownAtMinScale = hopMin <= DockDropUnits && DockDropUnits < minScaleHangMin;
            bool hangAtMinScale = DockDropUnits >= minScaleHangMin && cfg.ledgeHangChance > 0f;
            Assert.IsTrue(hopDownAtMinScale || hangAtMinScale,
                $"{LogPrefix} 슬라이더 하한 배율({StickConfig.MinCharacterScale:F3}, 매달리기 최소 낙차 " +
                $"{minScaleHangMin:F3})에서 Dock 낙차({DockDropUnits:F3})를 내려갈 방법이 하나도 없습니다 — " +
                $"ledgeHangChance({cfg.ledgeHangChance:F2})가 0이면 이 조합이 곧 'Dock 위에 갇힘'입니다.");

            // ★ 절대 조건 3 — 문서/Tooltip에 적어둔 임계값이 실제 계산과 일치한다.
            // (금지선이 아니라 **거동 분기점**을 기록하는 상수다 — DockGeometry.HopDownCriticalScale 참고.)
            float expectedCritical = DockGeometry.HopDownCriticalScale(DockDropUnits, hangPerScale);
            Assert.AreEqual(expectedCritical, criticalScale, 0.0005f,
                $"{LogPrefix} 임계 배율 유도식이 DockGeometry와 어긋납니다(테스트 {criticalScale:F4} / " +
                $"헬퍼 {expectedCritical:F4}).");
            Assert.AreEqual(StickConfig.DockHopDownCriticalScale, criticalScale, 0.005f,
                $"{LogPrefix} 문서 상수 DockHopDownCriticalScale({StickConfig.DockHopDownCriticalScale:F4})이 " +
                $"실제 계산({criticalScale:F4})과 다릅니다 — 프리팹 비율이나 Dock 기하가 바뀌었으니 " +
                "Tooltip의 경고 문구도 갱신해야 합니다.");

            // ★ 절대 조건 4 — 되올라가기 상한이 Dock 단차를 덮는다. **이것이 진짜 갇힘을 막는 조건이다.**
            // 2026-08-30: 설정 절대값(stepUpMaxHeight)이 아니라 DockGeometry가 실측 낙차에서 유도한 값을
            // 본다 — tilesize 80 이상에서는 설정 절대값 2.4가 낙차를 못 덮기 때문이다(M3).
            Assert.Greater(resolvedStepUpMax, DockDropUnits,
                $"{LogPrefix} 유도된 되올라가기 상한({resolvedStepUpMax:F3})이 Dock 낙차({DockDropUnits:F3}) " +
                "이하입니다 — 한 번 Dock 아래로 내려간 캐릭터가 영영 못 올라옵니다.");
        }

        // ============================================================================
        // (a) 발판 위에 정확히 선다 / (b) Dock 왕복
        // ============================================================================

        [UnityTest]
        public IEnumerator StandsExactlyOnFootholdTopAtCurrentScale()
        {
            yield return SetUpDockLayout(DockDropUnits, startNearRightEdgeUnits: 1.2f);
            StickmanBlackboard bb = _agent.Blackboard;
            var metrics = _agent.GetComponent<StickmanMetrics>();

            _intent.MoveInputX = 0f;
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return new WaitForSeconds(1.0f);

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} Dock 위에서 접지하지 못했습니다.");

            float footY = bb.Body.position.y;
            float headTopY = metrics != null ? metrics.HeadTopWorldY : footY + StickConfig.BaselineCharacterTotalHeight;

            // 실제로 그려지는 발끝(포즈 애니메이터가 계산한 아래 마디 끝)도 함께 본다 — 루트만 맞고
            // 시각적 발이 지면에 파묻히면 사용자 눈에는 "떠 있다/파묻혔다"로 보인다.
            StickmanPoseAnimator pose = bb.GetPoseAnimator();
            float visualFootY = footY;
            if (pose != null && pose.HasLimbs)
            {
                pose.GetFootWorldPositions(out Vector2 lf, out Vector2 rf);
                visualFootY = Mathf.Min(lf.y, rf.y);
            }

            Debug.Log($"{LogPrefix} 접지 실측 — 발판 상단={_dockTopWorldY:F4}, 루트 발 Y={footY:F4}(오차 {(footY - _dockTopWorldY):F4}), " +
                $"시각 발끝 Y={visualFootY:F4}(오차 {(visualFootY - _dockTopWorldY):F4}), " +
                $"정수리 Y={headTopY:F4}, 전신 높이={(headTopY - footY):F4}");

            // ★ 절대 조건 — 루트(발 원점)가 발판 상단선에 정확히 놓인다.
            Assert.AreEqual(_dockTopWorldY, footY, 0.05f,
                $"{LogPrefix} 발 높이가 발판 상단과 어긋납니다 — 크기가 바뀌면서 접지 보정(footLift)이 " +
                "따라오지 않았다는 뜻입니다(SceneBootstrapper의 LimbDrop에 배율 적용 누락).");

            // ★ 절대 조건 — 시각적 발끝이 지면 아래로 파고들지 않는다(허용: 획 두께 정도의 0.05).
            Assert.GreaterOrEqual(visualFootY, _dockTopWorldY - 0.05f,
                $"{LogPrefix} 시각 발끝({visualFootY:F4})이 발판 상단({_dockTopWorldY:F4})보다 아래로 파고들었습니다.");

            // ★ 절대 조건 — 몸 전체가 발판 위에 있다(정수리가 발판보다 전신 높이만큼 위).
            Assert.Greater(headTopY, _dockTopWorldY,
                $"{LogPrefix} 정수리가 발판 상단보다 아래입니다 — 캐릭터가 발판에 파묻혔습니다.");
        }

        [UnityTest]
        public IEnumerator DockRoundTripHopDownThenClimbBackUp()
        {
            yield return SetUpDockLayout(DockDropUnits, startNearRightEdgeUnits: 0.10f);
            StickmanBlackboard bb = _agent.Blackboard;

            // ── 1단계: 뛰어내린다.
            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 전제 실패 — Dock에 접지하지 못했습니다.");
            Assert.IsTrue(bb.TryFindHopDownTarget(info, 1, out long hopTarget, out _),
                $"{LogPrefix} 배율 {_clonedConfig.ResolveCharacterScale():F3}에서 Dock 낙차 {DockDropUnits:F3}유닛이 " +
                $"뛰어내리기 대상으로 잡히지 않았습니다(밴드=[{_clonedConfig.hopDownMinDropHeight:F3}, {bb.HopDownMaxDropHeight:F3})).");
            Assert.AreEqual(RightFloorHandle, hopTarget, $"{LogPrefix} 뛰어내릴 발판이 오른쪽 바닥 조각이 아닙니다.");

            _intent.HopDownRequested = true;
            bool landedBelow = false;
            float elapsed = 0f;
            bool sawFall = false;
            while (elapsed < MaxObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                StickmanStateId st = bb.Machine.CurrentStateId;
                if (st == StickmanStateId.Fall) { sawFall = true; _intent.HopDownRequested = false; }
                else if (sawFall && (st == StickmanStateId.Idle || st == StickmanStateId.Walk)
                         && bb.CurrentFootholdHandle == RightFloorHandle)
                {
                    landedBelow = true;
                    break;
                }
            }
            Debug.Log($"{LogPrefix} 왕복 1단계(뛰어내리기) — 낙하관측={sawFall}, 착지={landedBelow}, " +
                $"발판핸들={bb.CurrentFootholdHandle}, 위치=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), {elapsed:F2}초");

            Assert.IsTrue(landedBelow,
                $"{LogPrefix} 왕복 1단계 실패 — {MaxObserveSeconds}초 안에 아래 발판에 착지하지 못했습니다.");
            Assert.AreEqual(_floorTopWorldY, bb.Body.position.y, 0.05f,
                $"{LogPrefix} 아래 발판 착지 높이가 어긋납니다.");

            // ── 2단계: 되올라간다(Dock 쪽으로 방향을 돌려 되올라가기 펄스).
            _intent.MoveInputX = -1f;
            float walkBack = 0f;
            bool climbed = false;
            bool sawClimb = false;
            while (walkBack < MaxObserveSeconds)
            {
                yield return null;
                walkBack += Time.deltaTime;

                GroundSensor.GroundInfo i2 = bb.SenseGround();
                if (i2.Grounded && bb.CurrentFootholdHandle == RightFloorHandle
                    && bb.TryFindClimbableWall(i2, -1, out long wallHandle, out float wallTopY))
                {
                    Assert.AreEqual(DockHandle, wallHandle,
                        $"{LogPrefix} 되올라갈 벽이 Dock 발판이 아닙니다(핸들 {wallHandle}).");
                    Assert.LessOrEqual(wallTopY - i2.GroundWorldY, _clonedConfig.stepUpMaxHeight,
                        $"{LogPrefix} Dock 턱 높이가 stepUpMaxHeight를 넘어 자율 배회로는 올라갈 수 없습니다.");
                    _intent.StepUpRequested = true;
                }

                if (bb.Machine.CurrentStateId == StickmanStateId.ParkourClimb)
                {
                    sawClimb = true;
                    _intent.StepUpRequested = false;
                }
                if (sawClimb && bb.CurrentFootholdHandle == DockHandle
                    && bb.Machine.CurrentStateId != StickmanStateId.ParkourClimb)
                {
                    climbed = true;
                    break;
                }
            }

            Debug.Log($"{LogPrefix} 왕복 2단계(되올라가기) — 등반관측={sawClimb}, 복귀={climbed}, " +
                $"발판핸들={bb.CurrentFootholdHandle}, 위치=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), {walkBack:F2}초");

            // ★ 절대 조건 — 왕복이 닫힌다. 여기서 실패하면 캐릭터가 Dock 아래에 영영 갇힌다.
            Assert.IsTrue(sawClimb, $"{LogPrefix} 왕복 2단계 실패 — ParkourClimb에 진입하지 못했습니다.");
            Assert.IsTrue(climbed, $"{LogPrefix} 왕복 2단계 실패 — Dock 발판으로 복귀하지 못했습니다.");
            Assert.AreEqual(_dockTopWorldY, bb.Body.position.y, 0.06f,
                $"{LogPrefix} Dock 복귀 높이가 어긋납니다 — 되올라선 뒤 발판 상단에 정확히 서지 못했습니다.");
        }

        // ============================================================================
        // (c) 화면 밖으로 나가지 않는다 — 작아진 시각 반폭이 클램프에 실제로 반영되는가
        // ============================================================================

        [UnityTest]
        public IEnumerator StaysInsideScreenWhilePushingEdgeAtCurrentScale()
        {
            yield return LoadSceneAndFindAgent();
            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            // 오른쪽 화면 끝을 계속 밀게 한다(클램프가 유일한 방어선이 되는 상황).
            _intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = _intent;
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            Renderer[] renderers = _agent.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0, $"{LogPrefix} 캐릭터 렌더러를 찾지 못했습니다.");

            float worstRightOs = float.NegativeInfinity;
            float worstLeftOs = float.PositiveInfinity;
            float t = 0f;
            while (t < 6f)
            {
                yield return null;
                t += Time.deltaTime;

                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || !r.enabled) continue;
                    Bounds b = r.bounds;
                    minX = Mathf.Min(minX, b.min.x);
                    maxX = Mathf.Max(maxX, b.max.x);
                }
                if (float.IsInfinity(minX)) continue;

                Vector2 leftOs = ScreenCoordinateConverter.WorldToOsScreen(bb.MainCamera, new Vector2(minX, bb.Body.position.y), _clonedConfig, out _);
                Vector2 rightOs = ScreenCoordinateConverter.WorldToOsScreen(bb.MainCamera, new Vector2(maxX, bb.Body.position.y), _clonedConfig, out _);
                worstLeftOs = Mathf.Min(worstLeftOs, leftOs.x);
                worstRightOs = Mathf.Max(worstRightOs, rightOs.x);
            }

            // 배율은 ScreenCoordinateConverter가 단일 소스다(2026-08-29 Retina 대응) — 필드를 직접 읽으면
            // 기본값 0(= '자동')을 배율 0으로 오해해 화면 폭이 0이 된다.
            float screenW = Screen.width * Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(_clonedConfig));
            Debug.Log($"{LogPrefix} 화면 클램프 실측 — 화면 폭={screenW:F0}pt, 관측 최좌={worstLeftOs:F1}, 최우={worstRightOs:F1}, " +
                $"시각 반폭={bb.CharacterVisualHalfWidthWorld:F4}유닛");

            // ★ 절대 조건 — 캐릭터의 어떤 렌더러도 화면 밖으로 나가지 않는다.
            Assert.GreaterOrEqual(worstLeftOs, 0f,
                $"{LogPrefix} 캐릭터 좌측 끝이 화면 밖({worstLeftOs:F1}pt)으로 나갔습니다.");
            Assert.LessOrEqual(worstRightOs, screenW,
                $"{LogPrefix} 캐릭터 우측 끝이 화면 밖({worstRightOs:F1}pt > {screenW:F0}pt)으로 나갔습니다 — " +
                "화면 클램프의 시각 반폭이 작아진 캐릭터를 따라오지 않았습니다.");

            // ── 몸(획으로 그린 캐릭터 자체)의 시각 반폭이 배율을 따라오는지 별도로 확인한다.
            // 왜 bb.CharacterVisualHalfWidthWorld를 그대로 단언하지 않는가: 그 값은 캐릭터 GameObject
            // **아래 있는 모든 렌더러**의 합집합이라, Phase 4/5 시각 레이어가 Awake에서 만들어 두는
            // 이펙트(발밑 타이머 링 등)까지 포함한다. 그 이펙트들은 캐릭터 크기와 무관한 자기 치수를
            // 갖고 있어 배율을 따라오지 않는다(화면 클램프 입장에서는 여유가 넓어지는 **안전한 방향**
            // 이라 문제가 아니다 — 실제로 위 화면 밖 단언이 이미 통과한다). 그래서 여기서는 몸을 이루는
            // 렌더러만 이름으로 골라 그것이 배율에 비례하는지를 본다.
            // ★ 폭은 **몸 렌더러들의 합집합 AABB 자체**에서 잰다 — 루트 좌표를 기준으로 삼지 않는다.
            // 실측으로 확인한 함정(2026-08-29): 화면 클램프에 밀착한 상태에서 Rigidbody2D.position.x와
            // 실제로 그려지는 Transform.position.x가 약 0.5유닛 어긋난다(아래 로그가 그 차이를 남긴다).
            // 그 상태에서 루트 x를 중심으로 재면 **수직선인 Torso조차 반폭 0.5**로 나와, 폭을 재는 것이
            // 아니라 그 어긋남을 재게 된다. 합집합 AABB의 폭은 그 오프셋과 무관하다.
            float scale = _clonedConfig.ResolveCharacterScale();
            var metrics = _agent.GetComponent<StickmanMetrics>();
            float bodyMinX = float.PositiveInfinity, bodyMaxX = float.NegativeInfinity;
            var widest = new List<string>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled) continue;
                Bounds b = r.bounds;
                widest.Add($"{r.gameObject.name}=[{b.min.x:F3},{b.max.x:F3}]");
                if (!IsBodyRenderer(r.gameObject.name)) continue;
                bodyMinX = Mathf.Min(bodyMinX, b.min.x);
                bodyMaxX = Mathf.Max(bodyMaxX, b.max.x);
            }
            Assert.IsFalse(float.IsInfinity(bodyMinX), $"{LogPrefix} 몸 렌더러를 하나도 찾지 못했습니다.");
            float bodyHalfWidth = (bodyMaxX - bodyMinX) * 0.5f;
            Debug.Log($"{LogPrefix} 렌더러별 x범위 — {string.Join(", ", widest)}");
            Debug.Log($"{LogPrefix} 몸 시각 반폭={bodyHalfWidth:F4}유닛(범위 [{bodyMinX:F3},{bodyMaxX:F3}]), " +
                $"전신 높이={(metrics != null ? metrics.TotalHeight : 0f):F4}, " +
                $"루트 Body.x={bb.Body.position.x:F4} / Transform.x={_agent.transform.position.x:F4} " +
                $"(차이 {(_agent.transform.position.x - bb.Body.position.x):F4})");

            // ★ 절대 조건 — 몸 전체 폭이 키에 대한 고정 비율 구간 안에 있어야 한다. 배율이 바뀌어도
            // 이 비율은 변하지 않으므로, 어느 부위 하나라도 배율을 못 따라오면 구간을 벗어난다
            // (예: 팔 길이에 배율이 빠지면 반폭이 약 0.70 -> 0.91 비율로 올라가 상한을 넘는다).
            //
            // 왜 상한이 0.80이라는 느슨한 값인가(정직한 한계): Unity의 LineRenderer는 월드 bounds를
            // 매우 보수적으로 잡는다 — 실측상 **완전히 수직인 Torso조차 x로 ±0.5유닛**을 보고한다
            // (배율 0.5, 획 두께 0.057). 그래서 이 수치는 "그려진 실루엣의 실제 폭"이 아니라 그보다
            // 훨씬 넉넉한 상계이고, 여기서 정밀한 비율을 단언하면 Unity 내부 구현에 테스트를 묶게 된다.
            // **부위별 정밀 검증은 위 PrefabGeometryFollowsCharacterScale이 0.001 오차로 이미 잠갔다** —
            // 이 단언의 역할은 "렌더링까지 실제로 작아졌는가"를 거칠게 재확인하는 것뿐이다.
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");
            Assert.Greater(bodyHalfWidth, metrics.TotalHeight * 0.20f,
                $"{LogPrefix} 몸 시각 반폭({bodyHalfWidth:F4})이 키({metrics.TotalHeight:F4}) 대비 너무 좁습니다 — " +
                "팔다리가 그려지지 않았을 수 있습니다.");
            Assert.Less(bodyHalfWidth, metrics.TotalHeight * 0.80f,
                $"{LogPrefix} 몸 시각 반폭({bodyHalfWidth:F4})이 키({metrics.TotalHeight:F4}) 대비 너무 넓습니다 — " +
                $"어느 부위(팔 길이/획 두께)가 배율 {scale:F3}을 따라오지 않았습니다.");
        }

        /// <summary>캐릭터의 "몸"을 이루는 렌더러인지 — Editor/SceneBootstrapper.cs가 굽는 이름 그대로.
        /// Phase 4/5 시각 레이어가 만드는 이펙트 오브젝트를 배제하기 위한 것이다.</summary>
        private static bool IsBodyRenderer(string name)
        {
            switch (name)
            {
                case "Torso":
                case "HeadOutline":
                case "LeftEye":
                case "RightEye":
                case "LeftArm":
                case "LeftArmLower":
                case "RightArm":
                case "RightArmLower":
                case "LeftLeg":
                case "LeftLegLower":
                case "RightLeg":
                case "RightLegLower":
                    return true;
                default:
                    return false;
            }
        }

        // ============================================================================
        // 공통 준비
        // ============================================================================

        private IEnumerator LoadSceneAndFindAgent()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            yield return new WaitForSeconds(SettleWaitSeconds);
        }

        /// <summary>Dock 배치 재현 — Tests/PlayMode/EdgeHopDownTests.cs와 동일한 관례(낙차를 월드
        /// 유닛으로 먼저 정하고 OS y를 역산한다).</summary>
        private IEnumerator SetUpDockLayout(float dropUnits, float startNearRightEdgeUnits)
        {
            yield return LoadSceneAndFindAgent();

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float dockTopOs = h * 0.55f;

            Vector3 dockTopWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, dockTopOs), 10f, _clonedConfig);
            Vector2 floorOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(dockTopWorld.x, dockTopWorld.y - dropUnits), _clonedConfig, out _);
            float floorTopOs = floorOs.y;

            Assert.Less(floorTopOs, h, $"{LogPrefix} 준비 실패 — 요청 낙차가 화면 아래로 벗어납니다.");
            Assert.Greater(floorTopOs, dockTopOs, $"{LogPrefix} 준비 실패 — 바닥이 Dock보다 위에 놓였습니다.");

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(DockHandle, new Rect(w * 0.30f, dockTopOs, w * 0.40f, h - dockTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(LeftFloorHandle, new Rect(w * 0.10f, floorTopOs, w * 0.20f, h - floorTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(RightFloorHandle, new Rect(w * 0.70f, floorTopOs, w * 0.20f, h - floorTopOs), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            _intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = _intent;

            _dockTopWorldY = dockTopWorld.y;
            _dockLeftWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.30f, dockTopOs), 10f, _clonedConfig).x;
            _dockRightWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.70f, dockTopOs), 10f, _clonedConfig).x;
            _floorTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.85f, floorTopOs), 10f, _clonedConfig).y;

            float startX = _dockRightWorldX - startNearRightEdgeUnits;
            bb.Body.position = new Vector2(startX, _dockTopWorldY);
            bb.Body.transform.position = new Vector3(startX, _dockTopWorldY, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = DockHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            Debug.Log($"{LogPrefix} Dock 배치 준비 — 배율={_clonedConfig.ResolveCharacterScale():F3}, " +
                $"Dock 상단 월드Y={_dockTopWorldY:F4}(X {_dockLeftWorldX:F3}~{_dockRightWorldX:F3}), " +
                $"바닥 상단 월드Y={_floorTopWorldY:F4}, 실측 낙차={(_dockTopWorldY - _floorTopWorldY):F4}(요청 {dropUnits:F3}), " +
                $"매달리기 최소={bb.LedgeHangMinDropDepth:F4}, 뛰어내리기 밴드=[{_clonedConfig.hopDownMinDropHeight:F3}, {bb.HopDownMaxDropHeight:F4})");
        }
    }
}
