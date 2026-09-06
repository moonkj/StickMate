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
    /// ★★ 2026-09-06 — <b>초상화(정보창 미리보기)의 채움 조각에도 z 단차가 있다</b>
    /// (debugger 규명 → coder 수정). 사용자 신고: <b>«정보창 미리보기에서 망토를 착탈하면
    /// 모자 무늬가 나타났다 사라졌다»</b>.
    ///
    /// ============================================================================
    /// 무엇이 고장나 있었나
    /// ============================================================================
    /// 몸 렌더러(<c>CharacterAccessoryRenderer.AddFill</c>)는 같은 아이템의 채움끼리 <b>로컬 z</b>로
    /// 그리기 순서를 갈랐다. 초상화(<c>CharacterPortraitStage.AddFill</c>)에는 <b>그 줄이 없었다</b> —
    /// 채움이 전부 z = 0이고 아이템 안에서 <c>sortingOrder</c>도 동률이라, 순서를 Unity가
    /// <b>렌더러 생성/파괴 순서</b>로 임의 해소했다. 그래서 <b>재구성이 순서를 뒤집었고</b>,
    /// 망토 착탈(= 재구성 트리거)마다 천 모자의 그늘띠(<c>Piece_F1</c>)와 관(<c>Piece_B0</c>)의
    /// 위아래가 바뀌었다. 그것이 신고된 «나타났다 사라졌다»다.
    ///
    /// 근본 원인은 <b>두 렌더러가 같은 규칙을 각자 구현했다</b>는 것이다. 그래서 고침은 계산을
    /// <c>AccessoryShapeBuilder.FillDepthOffset</c> 한 함수로 모으고 양쪽이 그것을 부르게 했다.
    ///
    /// ============================================================================
    /// 그래서 이 파일은 «값»이 아니라 «두 표면이 같은가»를 잰다
    /// ============================================================================
    /// 단차의 크기(−0.0001)를 베끼면 이 검사는 상수의 사본이 되고, 사본은 언젠가 갈라진다.
    /// 대신 세 가지를 본다:
    /// <list type="number">
    ///   <item><b>동률 금지</b> — 같은 <c>sortingOrder</c>를 가진 초상화 채움끼리 z가 겹치지 않는다.
    ///     겹침이 곧 «그리기 순서 미정»이고, 그것이 신고된 상태 누수의 정의다.</item>
    ///   <item><b>표면 간 일치</b> — 같은 이름의 채움이 몸과 초상화에서 <b>같은 z</b>를 갖는다.
    ///     두 렌더러가 다시 갈라지면 여기가 빨개진다.</item>
    ///   <item><b>재구성을 견딘다</b> — 망토를 여러 번 착탈해도(벽시계 예산) 위 둘이 매번 성립한다.
    ///     신고의 재현 절차 그 자체다.</item>
    /// </list>
    ///
    /// <para><b>공허한 초록 방지</b>(전부 마지막에 함께 단언):
    /// 관측 중 «같은 sortingOrder에 채움이 2개 이상» 있었는가(없으면 (1)은 공허),
    /// z가 <b>실제로</b> 서로 달랐는가(전부 0이면 (2)는 0 == 0),
    /// 착탈이 <b>실제로</b> 여러 번 일어났는가(안 그러면 (3)은 정지 화면 한 장).</para>
    /// </summary>
    public sealed class PortraitFillDepthParityTests
    {
        private const string LogPrefix = "[초상화z]";

        /// <summary>천 모자 / 짧은 망토. PlayMode 어셈블리에는 <c>InternalsVisibleTo</c>가 없어
        /// <c>AccessoryShapeBuilder</c>의 번호 상수를 참조할 수 없다(이 어셈블리의 공통 사정).
        /// 번호가 재배치돼도 아래 «채움이 여러 개 그려졌는가» 대조군이 먼저 빨개진다.</summary>
        private const int ClothHat = 0;
        private const int ShortCape = 0;

        /// <summary>착탈 폭풍의 길이와 간격(벽시계). 프레임 수로 세지 않는다 — 배치 모드는
        /// 2,000fps를 넘겨서 «20프레임»이 0.01초가 된다(CLAUDE.md 확정 규약).</summary>
        private const float StormSeconds = 2.4f;
        private const float ToggleIntervalSeconds = 0.12f;

        private CharacterInfoWindow _window;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator 망토를_착탈해도_초상화_채움의_z_단차가_몸과_같게_유지된다()
        {
            yield return LoadSceneAndPinIdle();

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            StickConfig config = agent.Config;
            RaiseLevelTo(24, config);

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
            }
            EquipmentModel.TryWear(EquipmentSlot.Head, ClothHat, config);
            Assert.AreEqual(ClothHat, EquipmentModel.WornIndex(EquipmentSlot.Head),
                $"{LogPrefix} 천 모자를 걸치지 못했습니다 — 관측 전제가 깨졌습니다.");

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 정보창이 열리지 않았습니다.");
            yield return TestClock.SampleForSeconds(0.4f, _ => { });

            CharacterPortraitStage stage = _window.PortraitStageForTests;
            Assert.IsNotNull(stage, $"{LogPrefix} 정보창이 초상화 촬영장을 갖고 있지 않습니다.");
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");

            // 대조군 축적기 — 마지막에 «이 관측이 무의미하지 않았다»를 함께 단언한다.
            int observations = 0, toggles = 0, skippedDirtyFrames = 0;
            int maxTieGroupSize = 0;      // 같은 sortingOrder에 채움이 몇 개까지 모였는가
            int maxDistinctZ = 0;         // z가 실제로 몇 종류까지 나왔는가
            int parityComparisons = 0;    // 몸과 이름으로 맞춰 본 짝의 수
            int nonZeroZ = 0;             // 0이 아닌 z를 실제로 본 횟수

            bool capeOn = false;
            float start = Time.realtimeSinceStartup;
            float nextToggle = start;

            while (Time.realtimeSinceStartup - start < StormSeconds)
            {
                if (Time.realtimeSinceStartup >= nextToggle)
                {
                    capeOn = !capeOn;
                    EquipmentModel.TryWear(EquipmentSlot.Shoulders,
                        capeOn ? ShortCape : EquipmentModel.NotWorn, config);
                    Assert.AreEqual(capeOn ? ShortCape : EquipmentModel.NotWorn,
                        EquipmentModel.WornIndex(EquipmentSlot.Shoulders),
                        $"{LogPrefix} 망토 착탈이 모델에 반영되지 않았습니다 — 재구성을 못 일으켰습니다.");
                    toggles++;
                    nextToggle += ToggleIntervalSeconds;
                }

                yield return null;

                List<MeshRenderer> portrait = FillsUnder(figure);
                if (portrait.Count == 0) continue;   // 재구성 사이의 빈 프레임.

                // ★★ Unity의 <c>Destroy</c>는 <b>프레임 끝</b>에 처리된다. 재구성이 이 프레임의
                //   <b>내 표본보다 먼저</b> 돌았다면 «지워질 옛 채움»과 «새 채움»이 잠시 <b>공존</b>한다.
                //   그 스냅샷에서는 같은 이름이 두 번 나오고, (1)의 동률 검사가 <b>거짓 빨강</b>이 된다
                //   (옛 것과 새 것이 당연히 같은 z를 갖는다). 이름 중복은 그 상태의 정확한 서명이므로
                //   그 프레임만 건너뛰고 <b>몇 번 건너뛰었는지 센다</b> — 조용히 넘기지 않는다.
                if (HasDuplicateName(portrait)) { skippedDirtyFrames++; continue; }

                // ---- (1) 같은 sortingOrder 안에서 z가 겹치지 않는다 ----
                var byOrder = new Dictionary<int, List<MeshRenderer>>();
                var distinctZ = new HashSet<float>();
                for (int i = 0; i < portrait.Count; i++)
                {
                    int order = portrait[i].sortingOrder;
                    if (!byOrder.TryGetValue(order, out List<MeshRenderer> bucket))
                    {
                        bucket = new List<MeshRenderer>();
                        byOrder[order] = bucket;
                    }
                    bucket.Add(portrait[i]);
                    float z = portrait[i].transform.localPosition.z;
                    distinctZ.Add(z);
                    if (Mathf.Abs(z) > 1e-7f) nonZeroZ++;
                }
                maxDistinctZ = Mathf.Max(maxDistinctZ, distinctZ.Count);

                foreach (KeyValuePair<int, List<MeshRenderer>> pair in byOrder)
                {
                    List<MeshRenderer> bucket = pair.Value;
                    maxTieGroupSize = Mathf.Max(maxTieGroupSize, bucket.Count);
                    for (int a = 0; a < bucket.Count; a++)
                    {
                        for (int b = a + 1; b < bucket.Count; b++)
                        {
                            float za = bucket[a].transform.localPosition.z;
                            float zb = bucket[b].transform.localPosition.z;
                            Assert.AreNotEqual(za, zb,
                                $"{LogPrefix} 초상화의 채움 '{bucket[a].name}'과 '{bucket[b].name}'이 " +
                                $"같은 sortingOrder({pair.Key})에 <b>같은 z({za})</b>로 놓였습니다 — " +
                                "그리기 순서가 미정이라 재구성마다 위아래가 뒤집힙니다(사용자 신고 " +
                                "«망토를 착탈하면 모자 무늬가 나타났다 사라졌다»의 정체). " +
                                $"착탈 {toggles}회 / 관측 {observations}회째.");
                        }
                    }
                }

                // ---- (2) 같은 이름의 채움은 몸과 초상화에서 같은 z ----
                Transform container = AccessoryContainer();
                if (container != null)
                {
                    // 몸 쪽도 같은 이유로 «이름이 한 번만 나오는» 채움만 짝짓는다(UniqueByName 문서).
                    Dictionary<string, float> body = UniqueByName(FillsUnder(container));
                    Dictionary<string, float> mini = UniqueByName(portrait);
                    foreach (KeyValuePair<string, float> entry in mini)
                    {
                        if (!body.TryGetValue(entry.Key, out float bodyZ)) continue;
                        parityComparisons++;
                        Assert.AreEqual(bodyZ, entry.Value, 1e-9f,
                            $"{LogPrefix} 채움 '{entry.Key}'의 z가 몸({bodyZ})과 초상화({entry.Value})에서 " +
                            "다릅니다 — 두 렌더러가 다시 각자 계산하고 있습니다. 단차 계산은 " +
                            "AccessoryShapeBuilder.FillDepthOffset 한 곳이어야 합니다.");
                    }
                }

                observations++;
            }

            Debug.Log($"{LogPrefix} 착탈 {toggles}회 / 관측 {observations}회 " +
                $"(재구성 겹침으로 건너뛴 프레임 {skippedDirtyFrames}장) — " +
                $"동일 sortingOrder 최대 묶음 {maxTieGroupSize}개, z 종류 최대 {maxDistinctZ}종, " +
                $"0이 아닌 z 관측 {nonZeroZ}회, 몸↔초상화 이름 대조 {parityComparisons}쌍.");

            // ---- 대조군 — 위 세 검사가 공허하지 않았음을 증명한다 ----
            Assert.Greater(toggles, 4,
                $"{LogPrefix} 망토를 {toggles}번밖에 착탈하지 못했습니다 — 재구성 폭풍을 만들지 " +
                "못했으므로 위 단언은 사실상 정지 화면 한 장을 본 것입니다.");
            Assert.Greater(observations, 20,
                $"{LogPrefix} 관측이 {observations}회뿐입니다 — 표본이 너무 적습니다.");
            Assert.GreaterOrEqual(maxTieGroupSize, 2,
                $"{LogPrefix} 같은 sortingOrder에 채움이 2개 이상 모인 적이 <b>한 번도</b> 없습니다 — " +
                "그렇다면 «동률 금지» 검사는 아무것도 재지 않은 채 통과한 것입니다. " +
                "천 모자가 실제로 여러 개의 채움 조각을 그리는지 확인하십시오.");
            Assert.GreaterOrEqual(maxDistinctZ, 2,
                $"{LogPrefix} 초상화 채움의 z가 끝까지 {maxDistinctZ}종뿐이었습니다 — 전부 같은 " +
                "평면에 놓였다는 뜻이고, 그러면 몸과의 «같은 z» 대조도 0 == 0으로 통과합니다.");
            Assert.Greater(nonZeroZ, 0,
                $"{LogPrefix} 0이 아닌 z를 한 번도 보지 못했습니다 — 단차가 실제로 안 걸렸습니다.");
            Assert.Greater(parityComparisons, 0,
                $"{LogPrefix} 몸과 이름으로 맞춰 본 채움이 0쌍입니다 — 두 표면이 같은 조각을 " +
                "그리고 있지 않거나 컨테이너를 못 찾았습니다. 표면 간 일치 검사가 공허했습니다.");
        }

        // ==================== 유틸 ====================

        private static List<MeshRenderer> FillsUnder(Transform root)
        {
            var fills = new List<MeshRenderer>();
            if (root == null) return fills;
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                fills.Add(mr);
            }
            return fills;
        }

        /// <summary>같은 이름의 채움이 둘 이상인가 — Unity의 지연 <c>Destroy</c> 때문에 «지워질 옛
        /// 채움»과 «새 채움»이 한 프레임 공존하는 상태의 서명이다(호출부 문단 참고).</summary>
        private static bool HasDuplicateName(List<MeshRenderer> fills)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < fills.Count; i++)
            {
                if (!seen.Add(fills[i].name)) return true;
            }
            return false;
        }

        /// <summary>이름이 <b>한 번만</b> 나오는 채움만 «이름 → z»로 접는다. 중복 이름은 대조에서
        /// 뺀다 — 어느 쪽과 짝지어야 하는지 알 수 없는 짝을 억지로 비교하면 거짓 빨강이 된다.</summary>
        private static Dictionary<string, float> UniqueByName(List<MeshRenderer> fills)
        {
            var seen = new Dictionary<string, int>();
            var z = new Dictionary<string, float>();
            for (int i = 0; i < fills.Count; i++)
            {
                string name = fills[i].name;
                seen[name] = seen.TryGetValue(name, out int n) ? n + 1 : 1;
                z[name] = fills[i].transform.localPosition.z;
            }
            var unique = new Dictionary<string, float>();
            foreach (KeyValuePair<string, int> entry in seen)
            {
                if (entry.Value == 1) unique[entry.Key] = z[entry.Key];
            }
            return unique;
        }

        private static Transform AccessoryContainer()
        {
            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            if (renderer == null) return null;
            foreach (Transform t in renderer.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "EquipmentAccessories") return t;
            }
            return null;
        }

        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level, $"{LogPrefix} 레벨을 못 올렸습니다.");
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            agent.Blackboard.IntentSource = new StillSource();

            float deadline = Time.realtimeSinceStartup + 15f;
            float idleSince = -1f;
            StickmanStateId last = agent.Blackboard.Machine.CurrentStateId;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                last = agent.Blackboard.Machine.CurrentStateId;
                if (last != StickmanStateId.Idle) { idleSince = -1f; continue; }
                if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - idleSince >= 0.5f) break;
            }
            Assert.AreEqual(StickmanStateId.Idle, last, $"{LogPrefix} Idle로 안정되지 않았습니다.");
        }

        private sealed class StillSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }
    }
}
