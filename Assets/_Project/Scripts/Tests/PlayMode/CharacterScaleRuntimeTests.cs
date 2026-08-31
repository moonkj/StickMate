using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>런타임 캐릭터 크기 변경</b>(docs/UX_FLOW.md 34-3 크기 다이얼)의 회귀 잠금 —
    /// <see cref="StickmanAgent.ApplyCharacterScale"/>이 실제 씬의 실제 캐릭터에서 무엇을 보장하는가.
    ///
    /// ============================================================================
    /// 왜 이 파일이 필요한가 — 물리가 아니라 <b>파생 레이어</b>가 문제였다
    /// ============================================================================
    /// 2026-08-30 디버거 실측이 확인한 것: 물리는 배율 0.35~2.00 전 구간에서 안전하다
    /// (질량이 스케일을 안 따라가 랙돌 임계가 배율 불변 / breakForce가 Infinity라 관절 파단 불가 /
    /// 루트 원점이 발바닥이라 접지 오차 0). 그런데 <b>물리가 아닌 네 가지</b>가 조용히 어긋났다:
    ///   (1) <see cref="StickmanMetrics"/>가 1회 캐싱이라 Remeasure 없이는 옛 값을 계속 돌려준다.
    ///   (2) LineRenderer의 <b>두께</b>는 Transform 스케일을 따라가지 않는다(실측: 배율 3종에서 고정).
    ///   (3) 액세서리 컨테이너가 루트의 자식이라 metrics(월드 값)가 <b>한 번 더</b> 곱해졌다(s²).
    ///   (4) <c>ResolveWalkSpeed()</c>는 <c>config.characterScale</c>만 보므로 루트만 키우면
    ///       보폭은 커지는데 속도가 그대로라 발이 미끄러진다.
    /// 전부 "예외도 경고도 없이 그림만 조용히 깨지는" 유형이라, 실행으로 잡지 않으면 잡히지 않는다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <see cref="NegativeControl_액세서리_상쇄를_끄면_이중_스케일이_실제로_생긴다"/>가
    /// "상쇄를 되돌리면 실제로 s²가 된다"를 <b>같은 씬에서 실측</b>해, 위 (3)의 단언이 통과하는 이유가
    /// "조건이 헐거워서"가 아님을 증명한다.
    /// </summary>
    public sealed class CharacterScaleRuntimeTests
    {
        private const string LogPrefix = "[SCALE-RUNTIME]";

        /// <summary>다이얼 전 구간의 대표 배율. 34-3-5의 표에 나오는 지점을 그대로 쓴다.</summary>
        private static readonly float[] Scales = { 0.35f, 0.75f, 1.0f, 1.5f, 2.0f };

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private float _restoreScale;

        private IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            yield return new WaitForSeconds(1.5f);   // 낙하 정착.

            // ★ 원본 자산(DefaultStickConfig.asset)을 절대 건드리지 않는다(불변 원칙 3).
            //   ApplyCharacterScale은 <b>에이전트의 _config</b>에 characterScale을 쓰므로, 블랙보드만
            //   갈아끼우면 부족하다 — 그 private 필드까지 복제본으로 바꿔야 배포 에셋이 안전하다
            //   (리플렉션 주입은 FullscreenSuspendUiHidingTests가 이미 쓰는 이 프로젝트의 관례다).
            _originalConfig = _agent.Config;
            _clonedConfig = Object.Instantiate(_originalConfig);
            _agent.Blackboard.Config = _clonedConfig;
            AgentConfigField.SetValue(_agent, _clonedConfig);
            _restoreScale = _agent.CurrentCharacterScale;
        }

        private static readonly System.Reflection.FieldInfo AgentConfigField =
            typeof(StickmanAgent).GetField("_config",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _restoreScale > 0f) _agent.ApplyCharacterScale(_restoreScale, "테스트 정리");
            if (_agent != null && _originalConfig != null)
            {
                AgentConfigField.SetValue(_agent, _originalConfig);
                if (_agent.Blackboard != null) _agent.Blackboard.Config = _originalConfig;
            }
            if (_clonedConfig != null) Object.Destroy(_clonedConfig);
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
        }

        // ============================================================================
        // (1) 전 구간 — 치수/접지/보행속도/획두께/맨틀인셋이 전부 따라오는가
        // ============================================================================

        [UnityTest]
        public IEnumerator 배율_전_구간에서_치수와_접지와_보행속도가_따라온다()
        {
            yield return SetUp();

            StickmanMetrics metrics = _agent.Metrics;
            StickmanBlackboard bb = _agent.Blackboard;
            float baked = _agent.BakedCharacterScale;
            Assert.Greater(baked, 0f, $"{LogPrefix} 구워진 배율이 0입니다 — Awake 캐싱 경로 확인.");

            float footBefore = metrics.FootWorldY;

            foreach (float v in Scales)
            {
                Assert.IsTrue(_agent.ApplyCharacterScale(v, "테스트"), $"{LogPrefix} 배율 {v:F2} 적용이 무시됐습니다.");
                yield return null;   // 같은 프레임 원자 연산이므로 사실 기다릴 필요는 없다(그래도 한 프레임 돌려본다).

                float expectedHeight = StickConfig.BaselineCharacterTotalHeight * v;
                float foot = metrics.FootWorldY;
                float walk = _clonedConfig.ResolveWalkSpeed();
                float stop = bb.EdgeStopDistanceWorld;
                float inset = bb.ParkourMantleInsetWorld;

                Debug.Log($"{LogPrefix} 배율 {v:F2} — 전신 {metrics.TotalHeight:F4}(기대 {expectedHeight:F4}), " +
                    $"발Y {foot:F4}(기준 {footBefore:F4}), 보행 {walk:F4}, " +
                    $"경계판정 {stop:F4} / 맨틀인셋 {inset:F4}(여유 {(inset - stop):F4}), " +
                    $"루트 스케일 {_agent.transform.localScale.y:F4}.");

                // (1-a) Remeasure가 같은 프레임에 붙어 있는가 — 이게 빠지면 0.8초 내내 옛 값이 나온다.
                Assert.AreEqual(expectedHeight, metrics.TotalHeight, expectedHeight * 0.01f,
                    $"{LogPrefix} 배율 {v:F2}에서 전신 높이가 따라오지 않았습니다 — StickmanMetrics.Remeasure()가 " +
                    "스케일 대입과 같은 프레임에 불리는지 확인하세요(_measured 1회 캐싱).");

                // (1-b) 루트 원점이 발바닥이라 균일 스케일해도 발이 뜨거나 박히지 않는다.
                Assert.AreEqual(footBefore, foot, 0.05f,
                    $"{LogPrefix} 배율 {v:F2}에서 발바닥 Y가 {Mathf.Abs(foot - footBefore):F4}유닛 움직였습니다 — " +
                    "스케일 중심이 발바닥이 아니면 접지 보정이 따로 필요해집니다.");

                // (1-c) 보폭이 커지는 만큼 속도도 커져야 발이 미끄러지지 않는다(ResolveWalkSpeed의 유일한 소스).
                Assert.AreEqual(_clonedConfig.walkSpeed * v, walk, 1e-3f,
                    $"{LogPrefix} 배율 {v:F2}에서 보행 속도가 따라오지 않았습니다 — config.characterScale을 " +
                    "같은 프레임에 함께 대입했는지 확인하세요(안 그러면 보행 사이클 주파수가 어긋나 문워크가 됩니다).");

                // (1-d) ★ 1단계의 목적 — 맨틀 인셋이 전 구간에서 경계 밴드를 넘는가.
                Assert.Greater(inset - stop, 0.05f,
                    $"{LogPrefix} 배율 {v:F2}에서 맨틀 인셋({inset:F4})이 경계 판정 거리({stop:F4})보다 " +
                    "0.05 넘게 크지 않습니다 — 턱 위에 올라선 자리가 이미 경계라 곧바로 다시 뛰어내립니다.");
            }
        }

        /// <summary>
        /// ★ 2026-08-31 <b>이 테스트는 오늘까지 몸만 검사하고 있었다</b>(거짓 안심).
        ///
        /// <para>예전 코드는 <c>GetComponentsInChildren&lt;LineRenderer&gt;(true)</c>를 <b>배율 변경 전에
        /// 1회만</b> 캐시했다. 기본 차림이 천모자+선글라스라 캐시 시점에는 액세서리 선이 목록에 들어
        /// 있었지만, <see cref="CharacterAccessoryRenderer"/>의 재구성 서명에
        /// <c>metrics.TotalHeight</c>가 들어 있어서 <b>배율이 바뀌는 순간 컨테이너가 Destroy되고 다시
        /// 구워진다</b> → 캐시된 항목이 전부 파괴돼 <c>if (lr == null) continue;</c>로 조용히 스킵됐다.
        /// 그래서 액세서리 획이 출하 배율에서도 하한 미달(1.47pt / 하한 2pt)인 채로 초록불이었다.</para>
        ///
        /// <para>고치는 방법은 두 가지다: (a) 배율마다 다시 조회한다, (b) "지금 그리고 있는 것"을 물어보는
        /// 단일 창구에서 받는다. <b>(b)를 쓴다</b> — (a)는 <c>GetComponentsInChildren</c>이라 캐릭터의
        /// <b>자식이 아닌</b> 펫/FX를 여전히 못 보기 때문이다(그 둘도 같은 하한을 받아야 한다).
        /// 창구는 <see cref="StickmanAgent.DynamicVisuals"/>이고, 그것을 여기서 쓰는 것 자체가
        /// "창구가 실제로 채워진다"는 회귀 잠금이기도 하다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 배율을_바꿔도_획이_화면상_최소_두께_아래로_내려가지_않는다()
        {
            yield return SetUp();
            yield return EquipEverySlot();

            // 화면상 하한을 월드로 환산 — 프리팹을 구울 때와 같은 상수/같은 규칙(StickConfig에 단일 소스).
            Camera cam = _agent.Blackboard.MainCamera;
            float pointsPerWorldUnit = cam != null && cam.orthographic
                ? (Screen.height * Platform.ScreenCoordinateConverter.ResolveDpiScale(_clonedConfig))
                  / (2f * cam.orthographicSize)
                : StickConfig.ReferencePointsPerWorldUnitApprox;
            float floorWorld = StickConfig.MinStrokeScreenPoints / pointsPerWorldUnit;

            Assert.AreEqual(floorWorld, _agent.MinStrokeWorldWidth, floorWorld * 0.02f,
                $"{LogPrefix} 에이전트가 쓰는 하한({_agent.MinStrokeWorldWidth:F5})이 이 테스트가 손계산한 " +
                $"하한({floorWorld:F5})과 다릅니다 — 하한의 단일 소스가 갈라졌습니다.");

            // ★ 단조 증가를 <b>엄격</b>하게 요구하면 안 된다 — 화면이 작은 환경(배치 모드 창)에서는
            //   작은 배율들이 전부 하한에 걸려 같은 값이 나오는 것이 <b>정상</b>이다. 그래서
            //   (a) 모든 배율에서 하한 이상, (b) 비감소, (c) 최대 배율은 최소 배율보다 <b>실제로 굵다</b>
            //   (= 재대입이 실제로 일어난다)를 나눠서 단언한다.
            float previousMax = -1f;
            float firstMax = -1f, lastMax = -1f;
            int minAccessoryLinesSeen = int.MaxValue;
            foreach (float v in Scales)
            {
                _agent.ApplyCharacterScale(v, "테스트");
                // 재구성(파괴 + 재생성)이 소유자들의 LateUpdate에서 일어난다 — 몇 프레임 준다.
                for (int f = 0; f < 5; f++) yield return null;

                // ★ 매번 다시 조회한다(캐시 금지 — 위 문서). 몸은 계층에서, 몸 바깥의 잉크는 창구에서.
                float min = float.MaxValue, max = 0f;
                int bodyCount = 0, dynamicCount = 0;
                LineRenderer[] bodyLines = _agent.GetComponentsInChildren<LineRenderer>(true);
                for (int i = 0; i < bodyLines.Length; i++)
                {
                    LineRenderer lr = bodyLines[i];
                    if (lr == null) continue;
                    float w = lr.startWidth;   // 실제 월드 두께(widthMultiplier는 프리팹에서 1.0 그대로다).
                    if (w <= 0f) continue;
                    bodyCount++;
                    min = Mathf.Min(min, w);
                    max = Mathf.Max(max, w);
                }

                CharacterVisualRegistry registry = _agent.DynamicVisuals;
                registry.Refresh();
                for (int i = 0; i < registry.Count; i++)
                {
                    LineRenderer lr = registry[i].Line;
                    if (lr == null) continue;
                    float w = lr.startWidth;
                    if (w <= 0f) continue;
                    dynamicCount++;
                    min = Mathf.Min(min, w);
                    max = Mathf.Max(max, w);
                }

                minAccessoryLinesSeen = Mathf.Min(minAccessoryLinesSeen, dynamicCount);

                Debug.Log($"{LogPrefix} 배율 {v:F2} — 획 두께 {min:F5}~{max:F5}(화면상 하한 {floorWorld:F5}유닛 " +
                    $"= {StickConfig.MinStrokeScreenPoints:F1}pt), 검사한 선 = 몸 {bodyCount}개 + " +
                    $"액세서리/펫/FX {dynamicCount}개.");

                // ★★ 이 단언이 없으면 예전과 똑같이 "액세서리를 하나도 못 보고 통과"가 다시 가능해진다.
                Assert.Greater(dynamicCount, 0,
                    $"{LogPrefix} 배율 {v:F2}에서 몸 바깥의 선을 <b>하나도</b> 검사하지 못했습니다 — " +
                    "액세서리/펫/FX가 단일 창구(StickmanAgent.DynamicVisuals)에 신고하지 않고 있습니다. " +
                    "이 상태로는 아래 하한 단언이 몸만 검사하는 거짓 안심이 됩니다.");

                Assert.GreaterOrEqual(min, floorWorld - 1e-4f,
                    $"{LogPrefix} 배율 {v:F2}에서 가장 얇은 획이 {min:F5}유닛으로 화면상 하한" +
                    $"({floorWorld:F5}유닛 = {StickConfig.MinStrokeScreenPoints:F1}pt) 아래입니다 — " +
                    "작은 배율에서 선이 안티에일리어싱에 묻힙니다.");

                if (previousMax > 0f)
                {
                    Assert.GreaterOrEqual(max, previousMax - 1e-5f,
                        $"{LogPrefix} 배율이 올랐는데 가장 굵은 획이 {previousMax:F5} → {max:F5}로 <b>줄었습니다</b>.");
                }
                previousMax = max;
                if (firstMax < 0f) firstMax = max;
                lastMax = max;
            }

            // (c) LineRenderer의 width는 Transform 스케일을 따라가지 않는다 — 재대입을 빼먹으면
            //     여기서 값이 <b>전 배율 동일</b>로 나온다(2026-08-30 실측한 실패 그대로).
            Assert.Greater(lastMax, firstMax + 1e-4f,
                $"{LogPrefix} 배율 {Scales[0]:F2} → {Scales[Scales.Length - 1]:F2}인데 가장 굵은 획이 " +
                $"{firstMax:F5} → {lastMax:F5}로 전혀 굵어지지 않았습니다 — 획 두께 재대입이 빠졌습니다" +
                "(Transform 스케일은 LineRenderer의 두께를 따라가게 하지 않습니다).");

            Debug.Log($"{LogPrefix} 전 배율에서 검사한 몸 바깥 선의 최소 개수 = {minAccessoryLinesSeen}개.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>하한이 실제로 일을 하고 있는가</b>.
        ///
        /// <para>위 테스트는 "모든 선이 하한 이상"만 본다. 그런데 만약 비례값이 애초에 전부 하한보다
        /// 굵었다면 그 단언은 <b>항상 참</b>이라 아무것도 잡지 못한다. 여기서는 같은 실행에서
        /// <b>수정 전 규칙(순수 비례)</b>을 그대로 계산해, 낮은 배율에서 그 값이 하한 <b>미만</b>임을
        /// 보인다 — 즉 하한을 빼면 실제로 결함이 되돌아온다.</para>
        ///
        /// <para>실측 근거(2026-08-31, 실사용 화면 982pt / ortho 12 = 40.9pt/유닛):
        /// 액세서리는 배율 0.35에서 0.69pt, <b>출하 기본 배율 0.75에서도 1.47pt</b>로 하한(2pt) 미달이었다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NegativeControl_하한을_빼면_액세서리_획이_실제로_하한_아래로_내려간다()
        {
            yield return SetUp();
            yield return EquipEverySlot();

            var accessory = _agent.GetComponent<CharacterAccessoryRenderer>();
            Assert.IsNotNull(accessory, $"{LogPrefix} CharacterAccessoryRenderer가 없습니다.");

            float floorWorld = _agent.MinStrokeWorldWidth;
            float pointsPerWorldUnit = StickConfig.MinStrokeScreenPoints / Mathf.Max(1e-6f, floorWorld);

            bool sawBelowFloor = false;
            foreach (float v in Scales)
            {
                _agent.ApplyCharacterScale(v, "테스트");
                for (int f = 0; f < 5; f++) yield return null;

                // 수정 전 규칙 = 순수 비례(StrokeWidth). 지금 실제로 그려지는 두께와 나란히 찍는다.
                float proportional = accessory.StrokeWidth;
                float drawnMin = float.MaxValue;
                CharacterVisualRegistry registry = _agent.DynamicVisuals;
                registry.Refresh();
                for (int i = 0; i < registry.Count; i++)
                {
                    LineRenderer lr = registry[i].Line;
                    if (lr == null || lr.startWidth <= 0f) continue;
                    drawnMin = Mathf.Min(drawnMin, lr.startWidth);
                }

                Debug.Log($"{LogPrefix} [네거티브] 배율 {v:F2} — 수정 전 규칙(순수 비례) " +
                    $"{proportional:F5}유닛 = {proportional * pointsPerWorldUnit:F2}pt / " +
                    $"지금 그려지는 최소 두께 {drawnMin:F5}유닛 = {drawnMin * pointsPerWorldUnit:F2}pt / " +
                    $"하한 {floorWorld:F5}유닛 = {StickConfig.MinStrokeScreenPoints:F1}pt.");

                if (proportional < floorWorld - 1e-5f) sawBelowFloor = true;
            }

            Assert.IsTrue(sawBelowFloor,
                $"{LogPrefix} 다이얼 전 구간({Scales[0]:F2}~{Scales[Scales.Length - 1]:F2})에서 액세서리의 " +
                "순수 비례 두께가 <b>한 번도</b> 하한 아래로 내려가지 않았습니다 — 그렇다면 위 하한 단언은 " +
                "항상 참이라 아무 결함도 잡지 못합니다(이 환경에서는 이 회귀 잠금이 무의미하므로 " +
                "화면 크기/카메라 설정을 확인하세요).");
        }

        /// <summary>7슬롯을 전부 착용시킨다 — 액세서리/펫/FX가 <b>실제로 존재하는</b> 상태를 만든다.
        /// 기본 차림(모자+안경)만으로는 펫/FX가 없어 창구가 절반만 검증된다.</summary>
        private IEnumerator EquipEverySlot()
        {
            CharacterProgressionModel.AddXp(1000000f, _clonedConfig);
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (!EquipmentModel.IsUnlocked(slot)) continue;
                if (!EquipmentModel.IsEquipped(slot)) EquipmentModel.TryWear(slot, 0, _clonedConfig);
            }
            for (int f = 0; f < 30; f++) yield return null;

            Assert.IsNotNull(_agent.transform.Find("EquipmentAccessories"),
                $"{LogPrefix} 액세서리 컨테이너가 만들어지지 않았습니다 — 이 테스트의 전제가 성립하지 않습니다.");
        }

        /// <summary>
        /// ★ 2회차 조작의 함정 — <c>config.characterScale</c>이 "구워진 배율"과 "원하는 배율" 두 의미를
        /// 겸하고 있었다. 다이얼이 그 값을 덮어쓰는 순간 둘이 갈라지므로, 구워진 배율을 따로 캐싱하지
        /// 않으면 <b>두 번째 조작부터</b> 크기가 조용히 어긋난다.
        /// </summary>
        [UnityTest]
        public IEnumerator 배율을_연달아_바꿔도_두_번째부터_어긋나지_않는다()
        {
            yield return SetUp();

            StickmanMetrics metrics = _agent.Metrics;
            float[] sequence = { 2.0f, 0.5f, 1.25f, 0.75f };

            foreach (float v in sequence)
            {
                _agent.ApplyCharacterScale(v, "테스트 연속");
                yield return null;

                float expected = StickConfig.BaselineCharacterTotalHeight * v;
                Debug.Log($"{LogPrefix} 연속 조작 {v:F2} — 전신 {metrics.TotalHeight:F4}(기대 {expected:F4}), " +
                    $"구워진 배율 {_agent.BakedCharacterScale:F4}, 현재 배율 {_agent.CurrentCharacterScale:F4}.");

                Assert.AreEqual(expected, metrics.TotalHeight, expected * 0.01f,
                    $"{LogPrefix} 연속 조작에서 배율 {v:F2}의 전신 높이가 어긋났습니다 — " +
                    "구워진 배율(StickmanAgent.BakedCharacterScale)을 config.characterScale로 다시 읽고 " +
                    "있지 않은지 확인하세요.");
                Assert.AreEqual(v, _agent.CurrentCharacterScale, 0.01f,
                    $"{LogPrefix} CurrentCharacterScale이 실제 적용 값과 다릅니다.");
            }
        }

        // ============================================================================
        // (2) 액세서리 이중 스케일(s²) 제거 + 그 네거티브 컨트롤
        // ============================================================================

        /// <summary>
        /// 액세서리 컨테이너가 실제로 만들어질 때까지 기다린다.
        /// <para>★ <see cref="EquipmentModel.TryWear"/>는 <b>이미 그 아이템을 걸치고 있으면 false</b>를
        /// 돌려준다(값이 안 바뀌었으므로 이벤트를 쏘지 않는다는 계약). 그런데 이 프로젝트의 기본 차림이
        /// 마침 "모자 + 안경"이라, 그 false를 실패로 단언하면 <b>정상 상태에서 테스트가 깨진다</b> —
        /// 실제로 여기서 한 번 깨졌다. 그래서 "걸치게 만든다"가 아니라 <b>"걸치고 있다"</b>를 확인한다.</para>
        /// </summary>
        private IEnumerator EnsureAccessoryContainer()
        {
            if (!EquipmentModel.IsEquipped(EquipmentSlot.Head))
            {
                CharacterProgressionModel.AddXp(1000000f, _clonedConfig);
                EquipmentModel.TryWear(EquipmentSlot.Head, 0, _clonedConfig);
            }
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Head),
                $"{LogPrefix} 모자를 걸친 상태를 만들지 못했습니다 — 이 테스트의 전제가 성립하지 않습니다.");

            // 페이드가 올라오고 컨테이너가 만들어질 때까지(FadeSeconds + 여유).
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (_agent.transform.Find("EquipmentAccessories") != null) break;
            }
        }

        /// <summary>
        /// 지금 살아 있는 컨테이너를 찾는다. <b>매번 다시 찾아야 한다</b> — 재구성 서명에
        /// <c>metrics.TotalHeight</c>가 들어 있어서 배율을 바꾸면 컨테이너가 <b>파괴되고 새로 만들어진다</b>
        /// (그래서 배율 루프 안에서 참조를 들고 있으면 MissingReferenceException이 난다. 실제로 났다).
        /// </summary>
        private Transform FindAccessoryContainer()
        {
            Transform t = _agent.transform.Find("EquipmentAccessories");
            Assert.IsNotNull(t, $"{LogPrefix} 액세서리 컨테이너를 찾지 못했습니다(착용/페이드/재구성 확인).");
            return t;
        }

        [UnityTest]
        public IEnumerator 액세서리_컨테이너의_월드_스케일은_모든_배율에서_1이다()
        {
            yield return SetUp();
            yield return EnsureAccessoryContainer();

            foreach (float v in Scales)
            {
                _agent.ApplyCharacterScale(v, "테스트");
                // 재구성(파괴 + 재생성) + 상쇄 적용까지 도는 데 몇 프레임 필요하다.
                for (int i = 0; i < 5; i++) yield return null;

                Transform container = FindAccessoryContainer();
                float world = Mathf.Abs(container.lossyScale.y);
                Debug.Log($"{LogPrefix} 배율 {v:F2} — 액세서리 컨테이너 월드 스케일 {world:F5} " +
                    $"(루트 {_agent.transform.localScale.y:F4} × 컨테이너 로컬 {container.localScale.y:F5}).");

                Assert.AreEqual(1f, world, 0.01f,
                    $"{LogPrefix} 배율 {v:F2}에서 액세서리 컨테이너의 월드 스케일이 {world:F4}입니다 — " +
                    "1이 아니면 StickmanMetrics(이미 월드 값)에서 나온 좌표에 배율이 한 번 더 곱해져 " +
                    "모자가 정수리에서 떠오릅니다(s² 이중 스케일).");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 상쇄를 되돌리면(컨테이너 localScale = 1) 정확히 s²가 된다.
        /// 모자 꼭대기의 <b>월드</b> 높이를 재서, 상쇄가 있을 때는 배율에 비례하고 없을 때는
        /// 배율의 제곱에 비례한다는 것을 같은 씬에서 실측한다.
        /// </summary>
        [UnityTest]
        public IEnumerator NegativeControl_액세서리_상쇄를_끄면_이중_스케일이_실제로_생긴다()
        {
            yield return SetUp();
            yield return EnsureAccessoryContainer();

            const float BigScale = 2.0f;
            _agent.ApplyCharacterScale(BigScale, "네거티브 컨트롤");
            for (int i = 0; i < 5; i++) yield return null;   // 재구성 + 상쇄 적용(위 FindAccessoryContainer 문서).
            Transform container = FindAccessoryContainer();

            float footY = _agent.Metrics.FootWorldY;
            float fixedTop = HighestAccessoryWorldY(container) - footY;

            // 상쇄를 껐을 때(= 옛 코드)의 월드 높이. 컨테이너 안의 로컬 좌표는 그대로이므로,
            // 상쇄만 지우면 그 좌표에 루트 배율이 그대로 한 번 더 곱해진다.
            float rootScale = Mathf.Abs(_agent.transform.lossyScale.y);
            // ★ 여기서 <b>프레임을 넘기면 안 된다</b> — LateUpdate의 SyncContainerScale이 상쇄를 즉시
            //   되돌려 놓아 대조가 성립하지 않는다(실제로 그렇게 해서 비 1.000이 나왔다).
            //   Transform 변경은 즉시 반영되므로 같은 프레임에 그대로 잰다.
            container.localScale = Vector3.one;
            float brokenTop = HighestAccessoryWorldY(container) - footY;
            container.localScale = new Vector3(1f / rootScale, 1f / rootScale, 1f);   // 즉시 원복.

            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 배율 {BigScale:F2}, 루트 스케일 {rootScale:F4} — " +
                $"상쇄 켬 모자 꼭대기 {fixedTop:F4}유닛 / 상쇄 끔 {brokenTop:F4}유닛 " +
                $"(비 {(brokenTop / Mathf.Max(0.0001f, fixedTop)):F3}, 기대 ≈ 루트 스케일).");

            Assert.AreEqual(rootScale, brokenTop / Mathf.Max(0.0001f, fixedTop), 0.15f,
                $"{LogPrefix} 상쇄를 껐는데도 모자 높이가 루트 배율만큼 부풀지 않았습니다 — " +
                "네거티브 컨트롤이 성립하지 않습니다(이중 스케일 재현 조건이 바뀌었는지 확인하세요).");
        }

        private static float HighestAccessoryWorldY(Transform container)
        {
            float top = float.NegativeInfinity;
            var lines = container.GetComponentsInChildren<LineRenderer>(true);
            var buffer = new Vector3[64];
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                if (lr == null || lr.positionCount <= 0) continue;
                int count = Mathf.Min(lr.positionCount, buffer.Length);
                lr.GetPositions(buffer);
                for (int p = 0; p < count; p++)
                {
                    Vector3 world = lr.useWorldSpace ? buffer[p] : lr.transform.TransformPoint(buffer[p]);
                    if (world.y > top) top = world.y;
                }
            }
            Assert.Greater(top, float.NegativeInfinity, "액세서리 선을 하나도 찾지 못했습니다.");
            return top;
        }

        // ============================================================================
        // (3) 물리 — 배율을 바꾼 직후 랙돌로 무너지지 않는가
        // ============================================================================

        [UnityTest]
        public IEnumerator 배율을_바꿔도_랙돌로_무너지지_않는다()
        {
            yield return SetUp();
            StickmanBlackboard bb = _agent.Blackboard;

            foreach (float v in Scales)
            {
                _agent.ApplyCharacterScale(v, "테스트");
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    Assert.AreNotEqual(StickmanStateId.Ragdoll, bb.Machine.CurrentStateId,
                        $"{LogPrefix} 배율 {v:F2}로 바꾼 뒤 {i}프레임 만에 RAGDOLL로 무너졌습니다 — " +
                        "스케일 변경이 물리 튐을 만든다는 뜻입니다(2026-08-30 실측 결론과 배치됩니다).");
                }
                Debug.Log($"{LogPrefix} 배율 {v:F2} — 60프레임 동안 상태 {bb.Machine.CurrentStateId}, " +
                    $"발Y {_agent.Metrics.FootWorldY:F4}, 접지 {bb.SenseGround().Grounded}.");
            }
        }
    }
}
