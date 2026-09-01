using System.Collections;
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
    /// ★ 상체 기울임 ↔ 액세서리 교차 레이어 잠금 — 2026-09-01.
    ///
    /// ============================================================================
    /// 이 파일이 잡는 결함
    /// ============================================================================
    /// <see cref="CharacterAccessoryRenderer"/>는 <b>몸의 위치만</b> 따라가고 <b>회전은 따라가지
    /// 않았다</b>(클래스 문서 (3)/(3-1)이 위치 두 축만 다뤘다). 그 상태에서
    /// <see cref="StickmanPoseAnimator.SetBodyLean"/>이 들어오면 <b>모자만 완벽히 수평으로 뜬 채
    /// 그 아래 머리와 몸이 기울어진다</b> — 리더가 착수 전에 지목한 그대로다.
    ///
    /// ============================================================================
    /// 무엇을 재는가 — "머리 로컬 좌표계에서 본 모자 위치"
    /// ============================================================================
    /// Tests/PlayMode/AccessoryFacingFlipFillTests가 던지기 회전에서 쓴 것과 <b>같은 지표</b>다:
    /// 모자 메시의 실제 정점 하나를 월드로 올린 뒤 머리 로컬로 되돌린다. 이 값은 몸이 어떻게 돌든
    /// 모자가 머리에 붙어 있기만 하면 <b>변하지 않는다</b>(축정렬 bounds.center는 회전에 흔들려
    /// 지표가 될 수 없다는 것도 그 파일이 실측으로 밝혔다).
    ///
    /// 네거티브 컨트롤(<see cref="컨테이너_회전을_지우면_같은_지표가_실제로_깨진다"/>)이 "회전 추종을
    /// 빼면 이 검사가 실제로 실패한다"를 같은 프레임에서 실험으로 보여준다 — 통과가 "허용오차가
    /// 헐거워서"가 아님을 같은 파일에서 증명한다.
    /// </summary>
    public sealed class BodyLeanAccessoryFollowTests
    {
        private const string LogPrefix = "[LEAN-ACC]";
        private const int Cap = 0;            // 천 모자(비대칭 챙 — 회전이 그림에 확실히 드러난다).
        private const float LeanDegrees = 20f;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 상체가_기울어도_모자는_머리에_그대로_붙어_있다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Cap, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Assert.IsNotNull(renderer, $"{LogPrefix} CharacterAccessoryRenderer가 없습니다.");

            Transform head = FindDirectChild(agent.transform, "Head");
            Transform torso = FindDirectChild(agent.transform, "Torso");
            Assert.IsNotNull(head, $"{LogPrefix} Head를 못 찾았습니다.");
            Assert.IsNotNull(torso, $"{LogPrefix} Torso를 못 찾았습니다.");

            MeshRenderer crown = FindCrown(renderer);
            Assert.IsNotNull(crown, $"{LogPrefix} HatCrown 채움 메시를 못 찾았습니다(모자를 안 썼습니까?).");
            Vector3 probeLocal = crown.GetComponent<MeshFilter>().sharedMesh.vertices[0];

            Vector3 restInHead = head.InverseTransformPoint(crown.transform.TransformPoint(probeLocal));
            Vector3 restWorld = crown.transform.TransformPoint(probeLocal);
            float headRadius = agent.GetComponent<StickmanMetrics>().HeadRadius;

            // 기울임을 매 프레임 다시 요청한다 — TickBodyLean이 요청이 없으면 직립으로 되돌리기 때문
            // (그 자동 복귀 자체는 EditMode BodyLeanHipPivotTests가 따로 잠근다).
            StickmanPoseAnimator pose = agent.Blackboard.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터가 없습니다.");

            float maxDrift = 0f, maxTilt = 0f, maxWorldMove = 0f;
            for (int i = 0; i < 20; i++)
            {
                pose.SetBodyLean(LeanDegrees);
                yield return null;

                float tilt = Mathf.Abs(Mathf.DeltaAngle(0f, torso.localEulerAngles.z));
                maxTilt = Mathf.Max(maxTilt, tilt);

                Vector3 nowWorld = crown.transform.TransformPoint(probeLocal);
                maxWorldMove = Mathf.Max(maxWorldMove, Vector3.Distance(nowWorld, restWorld));

                Vector3 nowInHead = head.InverseTransformPoint(nowWorld);
                maxDrift = Mathf.Max(maxDrift, Vector3.Distance(nowInHead, restInHead));
            }

            Debug.Log($"{LogPrefix} 기울임 최대 {maxTilt:F1}도 — 모자의 머리 로컬 이탈 {maxDrift:F5}유닛 " +
                $"(머리 반경 {headRadius:F3}), 모자 월드 이동 {maxWorldMove:F4}유닛");

            // (1) 실제로 기울었는가 — 아무 일도 안 일어나서 통과하는 것을 막는다.
            Assert.Greater(maxTilt, 5f,
                $"{LogPrefix} 상체가 기울지 않았습니다({maxTilt:F1}도) — 이 검사가 아무것도 증명하지 못합니다.");
            Assert.Greater(maxWorldMove, headRadius * 0.3f,
                $"{LogPrefix} 모자가 월드에서 거의 안 움직였습니다({maxWorldMove:F4}유닛) — 기울임이 그림에 반영되지 않았습니다.");

            // (2) 그런데도 모자는 머리에 붙어 있는가.
            Assert.Less(maxDrift, headRadius * 0.05f,
                $"{LogPrefix} 상체가 기우는 동안 모자가 머리에서 {maxDrift:F5}유닛 미끄러졌습니다 " +
                $"(허용 {headRadius * 0.05f:F5}) — 액세서리가 몸통 회전을 따라가지 않습니다.");
        }

        /// <summary>네거티브 컨트롤 — 컨테이너의 회전만 지우면(= 이번 라운드 이전의 거동) 위와 똑같은
        /// 지표가 허용오차를 크게 넘어선다.</summary>
        [UnityTest]
        public IEnumerator 컨테이너_회전을_지우면_같은_지표가_실제로_깨진다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Cap, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Transform head = FindDirectChild(agent.transform, "Head");
            MeshRenderer crown = FindCrown(renderer);
            Assert.IsNotNull(crown, $"{LogPrefix} HatCrown 채움 메시를 못 찾았습니다.");
            Vector3 probeLocal = crown.GetComponent<MeshFilter>().sharedMesh.vertices[0];
            Vector3 restInHead = head.InverseTransformPoint(crown.transform.TransformPoint(probeLocal));
            float headRadius = agent.GetComponent<StickmanMetrics>().HeadRadius;

            StickmanPoseAnimator pose = agent.Blackboard.GetPoseAnimator();
            Transform container = FindByName(renderer.transform, "EquipmentAccessories");
            Assert.IsNotNull(container, $"{LogPrefix} 액세서리 컨테이너를 못 찾았습니다.");

            float driftWithoutRotation = 0f;
            for (int i = 0; i < 12; i++)
            {
                pose.SetBodyLean(LeanDegrees);
                yield return null;

                // 렌더러가 이번 프레임에 세팅한 회전을 **지운다**(위치는 그대로) = 회전 추종이 없던 코드.
                container.localRotation = Quaternion.identity;
                Vector3 nowInHead = head.InverseTransformPoint(crown.transform.TransformPoint(probeLocal));
                driftWithoutRotation = Mathf.Max(driftWithoutRotation, Vector3.Distance(nowInHead, restInHead));
            }

            Debug.Log($"{LogPrefix} [네거티브] 컨테이너 회전을 지우면 모자의 머리 로컬 이탈 " +
                $"{driftWithoutRotation:F4}유닛 (허용 {headRadius * 0.05f:F5})");

            Assert.Greater(driftWithoutRotation, headRadius * 0.05f,
                $"{LogPrefix} 회전을 지웠는데도 지표가 안 깨집니다 — 위 테스트가 아무것도 증명하지 못한다는 뜻입니다.");
        }

        // ============================================================================
        // 헬퍼 (AccessoryFacingFlipFillTests와 같은 관례)
        // ============================================================================

        private static MeshRenderer FindCrown(CharacterAccessoryRenderer renderer)
        {
            MeshRenderer crown = null;
            foreach (MeshRenderer mr in renderer.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr != null && mr.name.StartsWith("HatCrown") && mr.GetComponent<MeshFilter>() != null) crown = mr;
            }
            return crown;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c != null && c.name == name) return c;
            }
            return null;
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
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

        private static void Wear(EquipmentSlot slot, int index, StickConfig config)
        {
            EquipmentModel.TryWear(slot, index, config);
            Assert.AreEqual(index, EquipmentModel.WornIndex(slot), $"{LogPrefix} {slot} {index}번을 걸치지 못했습니다.");
        }

        private static void ClearAll(StickConfig config)
        {
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
            }
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            agent.Blackboard.IntentSource = new StillIntent();

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

        private sealed class StillIntent : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }
    }
}
