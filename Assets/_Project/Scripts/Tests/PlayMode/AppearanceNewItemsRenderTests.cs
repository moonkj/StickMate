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
    /// ★ FX/PET 신규 4종(물방울·나뭇잎·풍선·달팽이)이 <b>실제로 화면에 그려진다</b> — 2026-09-01.
    ///
    /// ============================================================================
    /// 이 파일이 잡는 실패
    /// ============================================================================
    /// 카테고리당 +2종 라운드는 카드(에셋)만 만들고 연출을 비워 둔 채 설명문에 "준비 중인 자리"라고
    /// 적어 두었다. 즉 <b>착용은 되는데 화면에는 한 픽셀도 나오지 않는</b> 상태였고, 이 저장소가
    /// 이미 다섯 번 겪은 실패 유형("로직 완성, 화면엔 0픽셀")과 같은 자리다. 이 라운드가 그것을 채웠고,
    /// 이 파일은 <b>다시 비어 버리는 것</b>을 막는다.
    ///
    /// ============================================================================
    /// 무엇을 재는가 — 플래그가 아니라 <b>실제로 그려진 선</b>
    /// ============================================================================
    /// "착용됐다"는 모델 상태나 "만들었다"는 카운터를 믿지 않는다. 씬 루트에 생긴 컨테이너
    /// (<c>CharacterFx</c> / <c>CharacterPet</c>)를 찾아 그 안의 <see cref="LineRenderer"/>를 훑고
    ///   (1) 이름이 이 아이템의 도형인지, (2) 점이 2개 이상인지,
    ///   (3) 점들이 만드는 사각형이 <b>0이 아닌지</b>(한 점에 뭉친 껍데기가 아닌지),
    ///   (4) 알파가 실제로 0을 넘는지(투명한 채로 존재만 하는 것이 아닌지)
    /// 를 전부 확인한다. 넷 중 하나라도 빠지면 "존재하지만 안 보이는" 상태를 초록으로 통과시킬 수 있다.
    ///
    /// <b>네거티브 컨트롤</b>: 같은 절차를 FX "없음"(0번)으로 돌리면 조각이 <b>0개</b>여야 한다 —
    /// 위 검사가 아이템과 무관하게 아무거나 세고 있는 것이 아님을 같은 파일에서 증명한다.
    /// </summary>
    public sealed class AppearanceNewItemsRenderTests
    {
        private const string LogPrefix = "[신규외형-TEST]";

        // 카테고리 안의 자리(Interaction/AppearanceShapeBuilder.cs의 같은 이름 상수와 같은 값).
        // 상수를 다시 적는 이 프로젝트의 관례를 따른다 — 어긋나면 아래 착용 단언이 즉시 빨개진다.
        private const int FxNone = 0;
        private const int FxBubble = 4;
        private const int FxLeaf = 5;
        private const int PetBalloon = 4;
        private const int PetSnail = 5;

        /// <summary>신규 4종 중 가장 높은 요구 레벨(달팽이 Lv.30).</summary>
        private const int TopRequiredLevel = 30;

        /// <summary>관측 창(실시간 초). 물방울은 0.55초 간격, 나뭇잎은 착용 직후 첫 장이 바로 진다.</summary>
        private const float ObserveSeconds = 2.5f;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // FX — 물방울 / 나뭇잎
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 물방울을_걸치면_걷는_동안_실제로_방울이_그려진다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Ready();

            Assert.IsTrue(Wear(EquipmentSlot.Fx, FxBubble),
                $"{LogPrefix} 물방울을 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            // 물방울의 발동 조건은 Walk다(반짝임=Idle, 먼지=달리기와 창을 갈라 놓았다).
            Sample sample = default;
            yield return Observe("Bubble", ObserveSeconds, agent, forceWalk: true, result: r => sample = r);

            AssertDrawn(sample, "물방울");
            Assert.AreEqual(0, sample.Colliders,
                $"{LogPrefix} 물방울이 콜라이더를 만들었습니다 — 그 자리의 다른 앱이 클릭되지 않게 됩니다(원칙 2·3).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 나뭇잎을_걸치면_가만히_있어도_잎이_떨어진다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Ready();

            Assert.IsTrue(Wear(EquipmentSlot.Fx, FxLeaf),
                $"{LogPrefix} 나뭇잎을 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            // 나뭇잎은 상태와 무관한 앰비언트다 — Idle로 고정한 채 그냥 기다리면 떨어져야 한다.
            Sample sample = default;
            yield return Observe("Leaf", ObserveSeconds, agent, forceWalk: false, result: r => sample = r);

            AssertDrawn(sample, "나뭇잎");
            Assert.AreEqual(2, sample.LinesPerPiece,
                $"{LogPrefix} 나뭇잎 한 장이 선 {sample.LinesPerPiece}개로 그려졌습니다 — 잎몸 + 잎자루 2개여야 합니다.");
        }

        /// <summary>
        /// 네거티브 컨트롤 — FX "없음"(0번)에서는 조각이 <b>하나도</b> 생기지 않는다.
        /// 위 두 테스트가 "아이템과 무관하게 아무거나 세고 있는 것"이 아님을 증명한다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FX_없음을_고르면_같은_관측에서_조각이_0개다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Ready();

            Assert.IsTrue(Wear(EquipmentSlot.Fx, FxNone),
                $"{LogPrefix} FX '없음'을 고르지 못했습니다.");

            var fx = Object.FindFirstObjectByType<CharacterFxRenderer>();
            Assert.IsNotNull(fx, $"{LogPrefix} CharacterFxRenderer가 씬에 없습니다.");

            Sample bubble = default, leaf = default;
            yield return Observe("Bubble", 1.2f, agent, forceWalk: true, result: r => bubble = r);
            yield return Observe("Leaf", 1.2f, agent, forceWalk: false, result: r => leaf = r);

            Assert.AreEqual(0, bubble.Pieces, $"{LogPrefix} FX '없음'인데 방울이 {bubble.Pieces}개 생겼습니다.");
            Assert.AreEqual(0, leaf.Pieces, $"{LogPrefix} FX '없음'인데 잎이 {leaf.Pieces}장 생겼습니다.");
            Assert.AreEqual(0, fx.LiveEffectCount, $"{LogPrefix} FX '없음'인데 살아 있는 조각이 있습니다.");
        }

        // ============================================================================
        // PET — 풍선 / 달팽이
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 풍선을_걸치면_끈과_주머니가_실제로_그려진다()
        {
            yield return LoadSceneAndPinIdle();
            yield return AssertPetDraws(PetBalloon, "풍선",
                new[] { "BalloonString", "BalloonBody" });
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 달팽이를_걸치면_발과_껍데기가_실제로_그려진다()
        {
            yield return LoadSceneAndPinIdle();
            yield return AssertPetDraws(PetSnail, "달팽이",
                new[] { "SnailFoot", "SnailShell", "SnailShellCore" });
        }

        private IEnumerator AssertPetDraws(int itemIndex, string label, string[] expectedLineNames)
        {
            StickmanAgent agent = Ready();
            var pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            Assert.IsTrue(Wear(EquipmentSlot.Pet, itemIndex),
                $"{LogPrefix} {label}을(를) 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            // 알파 페이드(0.25초)가 끝날 때까지 기다린다 — "존재하지만 완전히 투명"을 통과시키지 않는다.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && pet.Alpha < 0.9f) yield return null;

            Assert.AreEqual(itemIndex, pet.ActivePetItemIndex,
                $"{LogPrefix} {label}을(를) 걸쳤는데 렌더러가 그리고 있는 자리는 {pet.ActivePetItemIndex}번입니다.");
            Assert.Greater(pet.Alpha, 0.9f,
                $"{LogPrefix} {label}의 알파가 {pet.Alpha:F2}입니다 — 그려졌지만 보이지 않습니다.");

            GameObject root = GameObject.Find("CharacterPet");
            Assert.IsNotNull(root, $"{LogPrefix} {label}을(를) 걸쳤는데 'CharacterPet' 개체가 씬에 없습니다.");

            LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
            Assert.AreEqual(expectedLineNames.Length, lines.Length,
                $"{LogPrefix} {label}이(가) 선 {lines.Length}개로 그려졌습니다 — {expectedLineNames.Length}개여야 합니다.");

            for (int i = 0; i < expectedLineNames.Length; i++)
            {
                LineRenderer lr = FindLine(lines, expectedLineNames[i]);
                Assert.IsNotNull(lr, $"{LogPrefix} {label}의 '{expectedLineNames[i]}' 선이 없습니다.");
                Assert.GreaterOrEqual(lr.positionCount, 2,
                    $"{LogPrefix} {label}의 '{expectedLineNames[i]}'가 점 {lr.positionCount}개입니다 — 선이 아닙니다.");
                Assert.Greater(Extent(lr), 0f,
                    $"{LogPrefix} {label}의 '{expectedLineNames[i]}'가 한 점에 뭉쳐 있습니다(크기 0).");
                Assert.Greater(lr.startColor.a, 0.5f,
                    $"{LogPrefix} {label}의 '{expectedLineNames[i]}'가 거의 투명합니다(알파 {lr.startColor.a:F2}).");
                Assert.IsTrue(lr.enabled, $"{LogPrefix} {label}의 '{expectedLineNames[i]}'가 꺼져 있습니다.");
            }

            Assert.AreEqual(0, root.GetComponentsInChildren<Collider2D>(true).Length,
                $"{LogPrefix} {label}이(가) 콜라이더를 만들었습니다 — 그 자리의 다른 앱이 클릭되지 않게 됩니다(원칙 2·3).");
            Assert.AreEqual(0, root.GetComponentsInChildren<Rigidbody2D>(true).Length,
                $"{LogPrefix} {label}이(가) Rigidbody2D를 만들었습니다 — 펫은 물리 없이 보간만 합니다(33-6-1).");

            // 주인 곁에 있는가(화면 반대편에 그려 놓고 "그렸다"고 우기지 않는다).
            float height = StickmanMetrics.Find(pet).TotalHeight;
            float distance = Vector2.Distance(pet.PetWorldPosition, agent.Blackboard.Body.position);
            Assert.Less(distance, height * 4f,
                $"{LogPrefix} {label}이(가) 주인에게서 {distance:F2}유닛(신장 {height:F2}) 떨어져 있습니다.");

            Debug.Log($"{LogPrefix} {label} — 선 {lines.Length}개, 알파 {pet.Alpha:F2}, " +
                $"주인과의 거리 {distance:F2}유닛(신장 {height:F2}).");

            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            yield return null;
        }

        // ============================================================================
        // 관측 도구
        // ============================================================================

        private struct Sample
        {
            public int Pieces;          // 이름이 맞는 조각(holder) 수
            public int LinesPerPiece;   // 조각 하나가 가진 선 수
            public float MaxAlpha;      // 관측 창 안에서 본 최대 알파
            public float MaxExtent;     // 관측 창 안에서 본 최대 도형 크기(월드 유닛)
            public int Colliders;
        }

        /// <summary>
        /// 컨테이너 <c>CharacterFx</c> 안에서 이름이 <paramref name="pieceName"/>으로 시작하는 조각을
        /// 관측 창 내내 훑는다. <b>최대값</b>을 들고 오는 이유: 이펙트는 수명 곡선을 타므로 어떤
        /// 프레임에는 알파가 0에 가깝다 — "한 번이라도 실제로 보였는가"가 이 테스트의 질문이다.
        /// </summary>
        private IEnumerator Observe(string pieceName, float seconds, StickmanAgent agent, bool forceWalk,
            System.Action<Sample> result)
        {
            var sample = new Sample();
            float deadline = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (forceWalk)
                {
                    // 자율 상태 머신이 Idle로 되돌리므로 매 프레임 다시 고정한다
                    // (CharacterAppearanceLayerTests.StampFootprints와 같은 관례).
                    agent.Blackboard.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
                }
                yield return null;

                GameObject container = GameObject.Find("CharacterFx");
                if (container == null) continue;

                int pieces = 0, linesPerPiece = 0;
                foreach (Transform t in container.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || !t.name.StartsWith(pieceName)) continue;
                    pieces++;
                    LineRenderer[] lines = t.GetComponentsInChildren<LineRenderer>(true);
                    linesPerPiece = Mathf.Max(linesPerPiece, lines.Length);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i] == null || lines[i].positionCount < 2) continue;
                        sample.MaxAlpha = Mathf.Max(sample.MaxAlpha, lines[i].startColor.a);
                        sample.MaxExtent = Mathf.Max(sample.MaxExtent, Extent(lines[i]));
                    }
                }

                sample.Pieces = Mathf.Max(sample.Pieces, pieces);
                sample.LinesPerPiece = Mathf.Max(sample.LinesPerPiece, linesPerPiece);
                sample.Colliders = Mathf.Max(sample.Colliders,
                    container.GetComponentsInChildren<Collider2D>(true).Length);
            }

            if (forceWalk) agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            result(sample);
        }

        private static void AssertDrawn(Sample sample, string label)
        {
            Assert.Greater(sample.Pieces, 0,
                $"{LogPrefix} {label}을(를) 걸쳤는데 조각이 하나도 만들어지지 않았습니다 — " +
                "카드만 있고 화면에는 아무 일도 일어나지 않습니다(\"착용했는데 화면이 그대로면 그건 착용이 아니다\").");
            Assert.Greater(sample.MaxExtent, 0f,
                $"{LogPrefix} {label} 조각이 한 점에 뭉쳐 있습니다(크기 0) — 도형이 비어 있습니다.");
            Assert.Greater(sample.MaxAlpha, 0.2f,
                $"{LogPrefix} {label} 조각의 최대 알파가 {sample.MaxAlpha:F2}입니다 — " +
                "오브젝트는 생겼지만 사실상 투명해서 화면에는 보이지 않습니다.");

            Debug.Log($"{LogPrefix} {label} — 조각 {sample.Pieces}개, 조각당 선 {sample.LinesPerPiece}개, " +
                $"최대 알파 {sample.MaxAlpha:F2}, 최대 크기 {sample.MaxExtent:F4}유닛.");
        }

        /// <summary>선이 만드는 사각형의 큰 변(월드 유닛). 0이면 점 하나에 뭉친 껍데기다.</summary>
        private static float Extent(LineRenderer lr)
        {
            if (lr == null || lr.positionCount < 2) return 0f;
            Vector3 min = lr.GetPosition(0), max = min;
            for (int i = 1; i < lr.positionCount; i++)
            {
                Vector3 p = lr.GetPosition(i);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 size = max - min;
            return Mathf.Max(size.x, size.y);
        }

        private static LineRenderer FindLine(LineRenderer[] lines, string name)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null && lines[i].name == name) return lines[i];
            }
            return null;
        }

        // ============================================================================
        // 씬 준비 (CharacterAppearanceLayerTests와 같은 관례)
        // ============================================================================

        private StickmanAgent Ready()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            RaiseLevelTo(TopRequiredLevel, agent.Config);
            ClearAll();
            return agent;
        }

        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
        }

        private static void ClearAll()
        {
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, null);
            }
        }

        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level,
                $"{LogPrefix} 레벨 {level}까지 올리지 못했습니다 — 관측 전제가 성립하지 않습니다.");
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
