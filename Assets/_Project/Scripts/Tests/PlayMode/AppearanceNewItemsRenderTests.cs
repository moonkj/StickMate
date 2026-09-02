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

        // ★ 2026-09-02 qa-regression — 아래 5줄은 <b>프로덕션 상수의 사본</b>이다.
        //
        // 옛 주석은 두 가지를 주장했고 <b>둘 다 거짓이었다</b>:
        //   ① "상수를 다시 적는 이 프로젝트의 관례를 따른다"
        //      → CLAUDE.md의 명시 규약은 정확히 <b>반대</b>다("테스트에 프로덕션 상수를 숫자로
        //        베끼지 않는다"). 그 규약은 하드코딩 잔존으로 4건이 깨진 사고 뒤에 확정된 것이다.
        //   ② "어긋나면 아래 착용 단언이 즉시 빨개진다"
        //      → 빨개지지 않는다. 착용 단언은 <c>Assert.IsTrue(Wear(slot, index))</c>이고
        //        <c>Wear</c>가 재는 것은 <b>"그 번호가 실제로 걸쳐졌는가"</b>뿐이다
        //        (TryWear 후 WornIndex == index). 즉 <b>거부</b>(범위 밖/미해금)는 잡지만,
        //        번호가 <b>다른 아이템을 가리키게 된 것</b>은 잡지 못한다 — 재배치된 4번도
        //        멀쩡히 걸쳐지므로 Wear는 그대로 true이고, 이 테스트는 <b>엉뚱한 아이템을
        //        착용한 채 초록</b>이 된다. 사본이 어긋나는 바로 그 경우가 이 단언의 사각지대다.
        //
        // 그런데 사본을 없앨 수는 없다 — 구조적 제약이다:
        //   <c>Scripts/AssemblyInfo.cs</c>의 <c>InternalsVisibleTo</c>는
        //   <b>StickMate.Tests.EditMode 하나뿐</b>이라, PlayMode 어셈블리는 internal인
        //   <c>AppearanceShapeBuilder.FxBubble</c>을 <b>물리적으로 볼 수 없다</b>.
        //
        // 그래서 사본은 남기되 <b>드리프트를 EditMode에서 잠근다</b>:
        //   Tests/EditMode/AppearanceItemIndexMirrorTests.cs 가 이 파일의 소스를 직접 읽어
        //   아래 값들을 프로덕션 상수와 대조한다. 어긋나면 <b>그쪽이</b> 빨개진다.
        //   ★ 아래 이름/값을 고치면 그 파일의 대장(MirroredIndices)도 함께 고쳐야 한다.
        private const int FxNone = 0;
        private const int FxBubble = 4;
        private const int FxLeaf = 5;
        private const int PetBalloon = 4;
        private const int PetSnail = 5;

        /// <summary>신규 4종 중 가장 높은 요구 레벨(달팽이 Lv.30).</summary>
        private const int TopRequiredLevel = 30;

        /// <summary>관측 창(실시간 초). 물방울은 0.55초 간격, 나뭇잎은 착용 직후 첫 장이 바로 진다.</summary>
        private const float ObserveSeconds = 2.5f;

        /// <summary>렌더러가 만드는 펫 컨테이너 개체의 이름.
        /// <para>★ 프로덕션 문자열의 사본이다(<c>CharacterPetRenderer.EnsureBuilt</c>가
        /// <c>new GameObject("CharacterPet")</c>로 만든다). PlayMode 어셈블리는 그 상수를 볼 수 없으므로
        /// 사본을 피할 수 없는데, 이 니들은 <b>존재 단언</b>에만 쓴다 — 이름이 바뀌면 아래
        /// <c>Assert.AreEqual(1, ...)</c>가 <b>0개</b>로 시끄럽게 빨개진다(조용히 초록이 되는
        /// 부재 단언용이 아니다, CLAUDE.md).</para></summary>
        private const string PetContainerName = "CharacterPet";

        /// <summary>펫이 <b>완전히 사라지거나 완전히 나타날</b> 때까지의 예산(벽시계 초).
        /// <c>CharacterPetRenderer.FadeSeconds</c>(0.25초)의 열 배가 넘는다 — 넉넉하되 무한대가 아니다.
        /// ★ 프레임 수가 아니라 <b>벽시계</b>인 이유: 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로
        /// 돌아서 "N프레임 대기"가 실제로는 0.01초인 경우가 있다(CLAUDE.md 협업 프로토콜).</summary>
        private const float PetSettleSeconds = 3f;

        /// <summary>
        /// "보인다"의 기준 알파.
        ///
        /// <para>★★ 2026-09-02 <b>BUG-1의 정체가 이 상수가 한 벌이 아니었던 것</b>이다(debugger 확정).
        /// 옛 코드는 <c>while (… &amp;&amp; pet.Alpha &lt; 0.9f)</c>로 <b>알파</b>를 기다린 뒤
        /// <c>Assert.AreEqual(itemIndex, pet.ActivePetItemIndex)</c>로 <b>인덱스</b>를 단언했다 —
        /// <b>기다린 것과 잰 것이 다른 값</b>이다. 세이브가 "펫 착용"으로 복원되면 알파는 착용 순간
        /// 이미 1.0이라 루프가 <b>0프레임</b> 돌고, 아직 갱신되지 않은 인덱스를 단언한다.
        /// 근본 원인은 <c>CharacterPetRenderer</c>가 아이템 교체 시 <c>_alpha</c>를 리셋하지 않는
        /// <b>의도된 설계</b>다 — 즉 알파는 "새 펫이 준비됐다"의 지표가 아니다.</para>
        ///
        /// <para>독립 측정(debugger): 실패 실행 2.162~2.187초 / 성공 실행 2.391~2.501초로 <b>겹침 0</b>,
        /// 평균차 0.242초 ≈ <c>FadeSeconds × 0.9 = 0.225초</c>. <b>초록일 때만 루프가 돌았다.</b></para>
        /// </summary>
        private const float VisibleAlpha = 0.9f;

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

            // ════════════════════════════════════════════════════════════════════════
            // ① 관측 전제를 <b>만든 뒤 단언</b>한다 (2026-09-02, debugger 제안 ①)
            // ════════════════════════════════════════════════════════════════════════
            // Ready()가 모든 슬롯을 벗겼지만 그건 <b>모델</b>이다. 렌더러는 다음 LateUpdate부터
            // 알파를 내리고, 0에 닿아야 Teardown에서 _builtItem을 -1로 되돌린다. 그 <b>완전 해제</b>를
            // 실제로 기다리지 않으면 이 테스트에는 <b>거짓 초록</b>이 숨는다:
            //   앞 테스트/세이브에서 누출된 펫이 우연히 검사 대상과 <b>같은 번호</b>면,
            //   착용→재빌드 경로를 <b>한 번도 안 거치고</b> 모든 단언이 통과한다.
            // 그래서 "지금 그리고 있는 것이 아무것도 없다"를 만들고, 그것을 단언한 뒤에 걸친다.
            int settleFrames = 0;
            yield return PumpUntil(() => pet.ActivePetItemIndex < 0 && pet.Alpha <= 0.001f,
                PetSettleSeconds, n => settleFrames = n);

            Assert.Less(pet.ActivePetItemIndex, 0,
                $"{LogPrefix} 걸치기 <b>전</b>인데 펫 렌더러가 아직 {pet.ActivePetItemIndex}번을 " +
                $"그리고 있습니다(알파 {pet.Alpha:F2}, {settleFrames}프레임 기다림). " +
                "누출된 펫이 검사 대상과 같으면 '착용→재빌드'를 건너뛴 채 초록이 됩니다 — " +
                "여기서 멈추는 편이 그 거짓 초록보다 낫습니다.");
            Assert.LessOrEqual(pet.Alpha, 0.001f,
                $"{LogPrefix} 걸치기 전 펫 알파가 {pet.Alpha:F3}입니다 — 완전히 사라지지 않았습니다.");

            Assert.IsTrue(Wear(EquipmentSlot.Pet, itemIndex),
                $"{LogPrefix} {label}을(를) 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            // ════════════════════════════════════════════════════════════════════════
            // ② <b>기다리는 조건</b>과 <b>단언하는 값</b>을 같게 맞춘다 (BUG-1 본체)
            // ════════════════════════════════════════════════════════════════════════
            // 인덱스와 알파를 <b>둘 다</b> 기다린다. 옛 코드는 알파만 기다리고 인덱스를 단언했다.
            int buildFrames = 0;
            yield return PumpUntil(() => pet.ActivePetItemIndex == itemIndex && pet.Alpha > VisibleAlpha,
                PetSettleSeconds, n => buildFrames = n);

            // ★ 0프레임은 구조적으로 불가능하다 — 바로 위에서 인덱스 -1 / 알파 0을 단언했으므로
            //   최소한 재빌드 1프레임 + 페이드가 필요하다. 0이 나오면 위 ①이 실제로는 안 돈 것이고,
            //   그건 정확히 BUG-1이 재발한 형태다. 조용히 통과시키지 않는다.
            Assert.Greater(buildFrames, 0,
                $"{LogPrefix} {label}을(를) 걸친 직후 <b>0프레임</b> 만에 조건이 참이 됐습니다 — " +
                "해제 대기(①)가 실제로는 성립하지 않았다는 뜻입니다(BUG-1 재발 형태).");

            Assert.AreEqual(itemIndex, pet.ActivePetItemIndex,
                $"{LogPrefix} {label}을(를) 걸쳤는데 렌더러가 그리고 있는 자리는 {pet.ActivePetItemIndex}번입니다 " +
                $"({PetSettleSeconds}초 · {buildFrames}프레임 기다린 결과, 알파 {pet.Alpha:F2}).");
            Assert.Greater(pet.Alpha, VisibleAlpha,
                $"{LogPrefix} {label}의 알파가 {pet.Alpha:F2}입니다 — 그려졌지만 보이지 않습니다 " +
                $"({buildFrames}프레임 기다림).");

            // ════════════════════════════════════════════════════════════════════════
            // ③ 'CharacterPet'을 <b>집기 전에</b> 프레임 경계를 하나 넘긴다 (2026-09-02, debugger 제안 ②)
            // ════════════════════════════════════════════════════════════════════════
            // CharacterPetRenderer.EnsureBuilt는 교체 시 <c>DestroyVisuals()</c>로 옛 컨테이너를
            // <b>Destroy</b>한 뒤 <b>같은 프레임에</b> 같은 이름("CharacterPet")으로 새 컨테이너를
            // 만든다. 유니티의 Destroy는 <b>프레임 끝</b>에 처리되므로 그 프레임 안에서는 같은 이름의
            // 개체가 <b>둘</b> 존재할 수 있고, GameObject.Find는 그중 <b>죽을 쪽</b>을 집을 수 있다.
            // 그러면 아래 선 검사가 <b>이미 버려진 껍데기</b>를 재게 된다.
            //
            // ★ 프레임 하나를 더 도는 것으로 끝내지 않는다 — 그건 "그럴 것이다"이지 측정이 아니다.
            //   실제로 <b>한 개뿐</b>임을 센다. 둘이면 여기서 멈추고, 하나도 없으면 그것도 멈춘다.
            //   (비활성 포함으로 세는 이유: 죽기 직전의 껍데기가 비활성일 수 있고, 그 경우
            //    GameObject.Find는 <b>못 보는데</b> 이 검사는 본다 — 못 보는 쪽이 위험하다.)
            yield return null;

            int namedPetRoots = 0;
            Transform livePetRoot = null;
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i] == null || allTransforms[i].name != PetContainerName) continue;
                namedPetRoots++;
                livePetRoot = allTransforms[i];
            }

            Assert.AreEqual(1, namedPetRoots,
                $"{LogPrefix} {label}을(를) 걸친 뒤 '{PetContainerName}' 개체가 {namedPetRoots}개입니다 — " +
                "1개여야 합니다. 0개면 컨테이너가 만들어지지 않은 것이고, 2개 이상이면 교체 프레임의 " +
                "지연 파괴가 아직 처리되지 않은 것입니다(그 상태에서 GameObject.Find는 죽을 쪽을 집을 수 " +
                "있고, 그러면 아래 선 검사가 버려진 껍데기를 잽니다).");

            GameObject root = livePetRoot.gameObject;

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
                $"주인과의 거리 {distance:F2}유닛(신장 {height:F2}). " +
                $"해제 대기 {settleFrames}프레임 → 재빌드·페이드 {buildFrames}프레임 " +
                "(둘 다 0이면 이 테스트는 아무 전이도 관측하지 않은 것이다).");

            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            yield return null;
        }

        // ============================================================================
        // 관측 도구
        // ============================================================================

        /// <summary>
        /// 벽시계 예산 안에서 <paramref name="condition"/>이 참이 될 때까지 프레임을 돌리고,
        /// <b>실제로 돈 프레임 수</b>를 <paramref name="frames"/>로 돌려준다.
        ///
        /// <para>★ 프레임 수를 돌려주는 것이 핵심이다. <b>0프레임</b>은 "조건이 이미 참이었다"는 뜻이고,
        /// 그 경우 호출부는 <b>전이를 하나도 관측하지 않은 채</b> 단언하게 된다 — 2026-09-02 BUG-1이
        /// 정확히 그 형태였다(알파를 기다리고 인덱스를 단언 → 0프레임 → 갱신 전 값). 호출부가
        /// "0프레임이면 실패"를 <b>명시적으로</b> 걸 수 있게 값을 밖으로 낸다.</para>
        ///
        /// <para>예산이 벽시계인 이유는 CLAUDE.md 협업 프로토콜(배치모드 PlayMode가 2,000fps 이상)이다.</para>
        /// </summary>
        private static IEnumerator PumpUntil(System.Func<bool> condition, float seconds,
            System.Action<int> frames)
        {
            int n = 0;
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                n++;
            }
            frames(n);
        }

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
