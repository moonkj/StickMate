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
    /// ★★★ 실기 신고 회귀 잠금(2026-09-07, 같은 밤 3차) — <i>"풍선도 캐릭터가 멈춰있을때
    /// 캐릭터와 겹치지 않고 바깥쪽에 있어야하는데 여전히 겹쳐져있음"</i>.
    ///
    /// ============================================================================
    /// 배경 — 왜 앞선 수정(SortHead+1)으로 안 끝났는가
    /// ============================================================================
    /// 같은 밤 먼저 <see cref="PetBalloonHatSortOrderTests"/>가 "풍선이 모자 뒤로 간다"는 신고를
    /// <b>정렬 순서 동률</b>(SortBalloon == SortHead == 10)로 확정하고 SortHead+1로 고쳤다. 그런데
    /// 그 수정은 <b>그리기 순서</b>만 바꿀 뿐 <b>좌표</b>는 건드리지 않는다 — 풍선의 실제 (x,y)가
    /// 애초에 몸 실루엣 안쪽이면 순서를 아무리 앞으로 당겨도 여전히 겹쳐 보인다. 이 파일은 좌표
    /// 자체를 검산한다.
    ///
    /// ============================================================================
    /// 확정된 원인 (계산으로 확정 — 추측 아님, 오늘 밤 이미 네 번째 같은 패턴)
    /// ============================================================================
    /// 옛 <c>CharacterPetRenderer.BalloonTetherBehindInR</c> = 0.75R이었다. 그런데 풍선 주머니
    /// (<see cref="AppearanceShapeBuilder.BalloonBody"/>)는 매듭과 <b>같은 로컬 x</b>를 중심으로 한
    /// 대칭 원이라 반경(<see cref="AppearanceShapeBuilder.BalloonRadiusInR"/>=0.80R)만큼 몸 쪽으로
    /// 파고든다. 즉 Idle(기울임 0·기울기 0)에서 주머니의 몸쪽 안쪽 가장자리는 머리 중심에서
    /// <c>(0.75 − 0.80)R = −0.05R</c> — <b>머리 중심을 이미 지나쳐 반대쪽까지 파고들어 있었다.</b>
    /// 매듭 자체만 봐도 머리 중심에서의 거리가 <c>sqrt(0.75² + 0.30²)R ≈ 0.807R</c>로 머리 반경(1R)
    /// 보다 작아 <b>매듭이 머리 원 안쪽</b>이었다 — 몸통 물리 반폭(1.8182R)과 비교할 것도 없이 머리
    /// 자체와도 겹쳐 있었다.
    ///
    /// 종이비행기 궤도(2회) · 나뭇잎 스폰폭 · 물방울 스폰편차와 <b>똑같은 결함 유형</b>이다 —
    /// 위치 상수가 캐릭터 몸 실측 치수보다 작아서 몸 밖으로 못 나간다.
    ///
    /// 고침은 나뭇잎 수정과 <b>같은 검산 방식</b>이다: "최악 편차(오프셋 − 안쪽으로 파고드는 최대량)가
    /// 몸통 물리 반폭×1.2를 넘는가". 오프셋을 0.75R → 3.5R로 올렸다(나뭇잎이 이미 3.5R로 검증됐으므로
    /// 같은 값을 재사용 — 매직넘버를 새로 고르지 않는다).
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  P1  공식으로 유도한 "최악 편차"(오프셋 − 주머니 반경)가 몸통 물리 반폭보다 20% 이상
    ///      여유를 두고 크다.
    ///  P1n (네거티브) 신고 당시의 옛 상수(0.75R)로 같은 식을 계산하면 값이 <b>음수</b>다 — 겹침이
    ///      아니라 관통이었다는 것을 같은 파일에서 증명한다.
    ///  P2  <b>실제로 그려지는</b> 풍선 매듭 위치(<see cref="CharacterPetRenderer.PetWorldPosition"/>)를
    ///      Idle에서 실측한 몸통 중심 대비 편차가 공식값과 일치하고, 몸통 물리 반폭보다 크다 —
    ///      "공식은 맞는데 실제로 그려지는 것은 다르다"는 별개의 실패 유형을 반증한다.
    /// </summary>
    public sealed class PetBalloonClearsBodyTests
    {
        private const string LogPrefix = "[PET-풍선-몸통이탈]";

        /// <summary>PET 카테고리의 "풍선" 자리 — AppearanceShapeBuilder.PetBalloon과 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다(PetBalloonHatSortOrderTests와
        /// 같은 관례).</summary>
        private const int PetBalloonItem = 4;

        /// <summary>Resources/Items/look_pet_balloon.asset의 requiredLevel 사본
        /// (PetBalloonHatSortOrderTests.BalloonRequiredLevel과 같은 값).</summary>
        private const int BalloonRequiredLevel = 27;

        /// <summary>CharacterPetRenderer.BalloonTetherBehindInR의 사본(2026-09-07 3차 수정 값) —
        /// private라 테스트 어셈블리가 직접 참조할 수 없어 복제한다(LeafFallClearsBodyTests와 같은
        /// 관례, CLAUDE.md 협업 프로토콜의 "internal/private 상수 복제" 예외).</summary>
        private const float BalloonTetherBehindInR = 3.5f;

        /// <summary>AppearanceShapeBuilder.BalloonRadiusInR의 사본(internal이라 복제) — 풍선 주머니가
        /// 매듭에서 몸 쪽으로 파고드는 최대량(머리 반경 배수).</summary>
        private const float BalloonRadiusInR = 0.80f;

        /// <summary>신고 당시(3차 수정 전) 묶인 자리 오프셋 — 네거티브 컨트롤 전용. <b>다시 프로덕션
        /// 값으로 되돌리지 마라</b> — 이 테스트가 그 값을 반증하는 대조군이다.</summary>
        private const float PreFixTetherBehindInR = 0.75f;

        /// <summary>몸통 물리 반폭 대비 요구하는 최소 여유 배수(종이비행기·나뭇잎 테스트와 같은 관례).</summary>
        private const float RequiredMargin = 1.2f;

        /// <summary>실측값과 공식값 사이에 허용하는 오차(월드 유닛).</summary>
        private const float SampleTolerance = 0.02f;

        /// <summary>CharacterPetRenderer.BalloonTetherAboveInR의 사본(수정 전후 값이 같다) — private라
        /// 테스트 어셈블리가 직접 참조할 수 없어 복제한다(위 <see cref="BalloonTetherBehindInR"/>과 같은
        /// 관례·같은 사유). <b>묶인 자리는 머리 중심 위 0.30R</b>인데 렌더러가 공개한
        /// <see cref="CharacterPetRenderer.HeadAnchorWorldPosition"/>은 머리 중심 <b>아래</b> 1.75R
        /// (종이비행기 궤도 중심)이라, 상체가 기울면 두 점의 x가 갈라진다 — 아래 대조군이 재는 것이 그 사실이다.</summary>
        private const float BalloonTetherAboveInR = 0.30f;

        /// <summary>대조군이 강제하는 상체 기울임(도). 유휴 앰비언트 "주위 살피기"가 실제로 내는 정점과
        /// 같은 크기를 쓴다 — 이 테스트가 잡아야 하는 것이 바로 그 상태이기 때문이다.</summary>
        private const float ControlLeanDegrees = 7f;

        /// <summary>풍선 매듭의 지수 추종(CharacterPetRenderer.BalloonFollowRate=3.4/초)이 <b>정지한</b>
        /// 목표에 수렴하기를 기다리는 벽시계 예산(초). 3.4/초면 1.5초에 초기 오차의 0.6%만 남는다.
        /// ★ 프레임 수로 기다리지 않는다(배치모드는 2,000fps 이상으로 돈다 — CLAUDE.md 협업 프로토콜).</summary>
        private const float BalloonSettleSeconds = 1.5f;

        /// <summary>표본 창(초) — 한 프레임만 우연히 맞은 경우를 배제하는 벽시계 예산.</summary>
        private const float SampleWindowSeconds = 1.0f;

        /// <summary>"지금 곧게 서 있다"를 x축으로 확인할 때 쓰는 허용오차. 공식 허용오차보다 4배 엄격하게
        /// 잡아, 기준축 어긋남이 공식 검산을 오염시키기 전에 먼저 빨간불이 켜지게 한다.</summary>
        private const float UprightAxisTolerance = SampleTolerance * 0.25f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 풍선_묶인자리의_최악편차가_몸통_물리_반폭보다_충분히_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            float headRadius = metrics.HeadRadius;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            // 최악 표본 — 오프셋에서 풍선 주머니 자신의 반경(몸 쪽으로 파고드는 최대량)을 뺀다
            // (나뭇잎 테스트의 "오프셋 − 팔랑임 진폭"과 같은 식).
            float worstCaseDeviation = headRadius * (BalloonTetherBehindInR - BalloonRadiusInR);
            float required = physicalHalfWidth * RequiredMargin;

            Debug.Log($"{LogPrefix} 머리 반경 {headRadius:F4}, 몸통 물리 반폭 {physicalHalfWidth:F4}, " +
                $"최악 편차 {worstCaseDeviation:F4}(요구 여유 {required:F4} = 물리 반폭×{RequiredMargin:F1}).");

            // P1 — 절대 조건.
            Assert.Greater(worstCaseDeviation, required,
                $"{LogPrefix} 최악 편차 {worstCaseDeviation:F4}유닛이 몸통 물리 반폭 {physicalHalfWidth:F4}" +
                $"유닛의 {RequiredMargin:F1}배({required:F4})에 못 미칩니다 — 풍선이 다시 몸통 폭 안으로 " +
                "들어와 겹칠 수 있습니다.");

            // P1n — 네거티브 컨트롤: 신고 당시 상수(0.75R)로 같은 식을 계산하면 음수다(겹침이 아니라
            // 관통 — 주머니가 이미 머리 중심을 넘어 반대쪽까지 파고든다).
            float preFixWorstCase = headRadius * (PreFixTetherBehindInR - BalloonRadiusInR);
            Debug.Log($"{LogPrefix} [네거티브] 신고 당시 상수(0.75R)로 계산한 최악 편차 " +
                $"{preFixWorstCase:F4}유닛 — {(preFixWorstCase < 0f ? "음수(관통, 신고 재현)" : "양수(대조 실패)")}.");
            Assert.Less(preFixWorstCase, physicalHalfWidth,
                $"{LogPrefix} 신고 당시 상수로도 조건이 성립합니다({preFixWorstCase:F4} vs " +
                $"{physicalHalfWidth:F4}) — 이 테스트가 실제 신고를 반증하지 못합니다(무의미한 대조).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            RaiseLevelTo(BalloonRequiredLevel, agent.Config);
            Assert.IsTrue(Wear(EquipmentSlot.Pet, PetBalloonItem),
                $"{LogPrefix} 풍선을 걸치지 못했습니다({BalloonRequiredLevel}레벨을 만들었는데도 실패) — " +
                "관측 전제가 성립하지 않습니다.");

            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PetBalloonItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 풍선으로 빌드되지 않았습니다({pet.ActivePetItemIndex}).");

            float expectedOffset = pet.BalloonTetherOffsetWorld;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            StickmanPoseAnimator pose = agent.Blackboard.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터가 없습니다.");

            // ================================================================
            // 단계 A — 공식 충실도. <b>기준축을 프로덕션과 같게</b> 맞춘 상태에서만 잰다.
            // ================================================================
            // ★ 2026-09-26 하네스 수정. 옛 판은 기준을 <b>몸 루트</b>(Body.position.x)로 잡고 그
            //   값을 루프 <b>밖에서 한 번</b> 떴다. 그런데 프로덕션(CharacterPetRenderer.TickBalloon)은
            //   <b>기울어진 머리</b>(LeanedHeadWorld)에서 오프셋을 잰다. Idle에도 유휴 앰비언트
            //   "주위 살피기"가 상체를 최대 7도까지 기울이므로 두 기준은 매 프레임 갈라지고, 그
            //   차이(실측 정점 0.0861유닛)가 허용오차 0.02의 4.3배라 통과/실패가 <b>표본을 뜬 순간의
            //   기울임</b>으로 갈렸다 — 프로덕션은 멀쩡한데 테스트만 간헐로 빨개진 원인이다.
            //
            // 그래서 이 단계는 상체를 <b>곧게 고정</b>하고 잰다. 곧게 선 프레임에서는 기울임 회전이
            // 항등이라(LeanedHeadWorld의 조기 반환) 머리 앵커의 x가 몸 루트의 x와 <b>정확히</b>
            // 같아지고, 그래서 아래 "같은 프레임 앵커 읽기"가 공식과 한 치도 어긋나지 않는다.
            // 매듭은 목표를 지수 추종(3.4/초)하므로 목표가 움직이는 동안에는 구조적으로 뒤처진다 —
            // 목표를 세워 두고 수렴을 기다리는 이유다.
            float settleUntil = Time.realtimeSinceStartup + BalloonSettleSeconds;
            while (Time.realtimeSinceStartup < settleUntil)
            {
                pose.ClearBodyLean();
                yield return null;
            }

            float maxFormulaError = 0f;
            float maxAxisGap = 0f;
            int samples = 0;
            float deadline = Time.realtimeSinceStartup + SampleWindowSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                pose.ClearBodyLean();

                // ★ 같은 프레임에 읽는다 — 기준(앵커)과 대상(매듭)이 서로 다른 시각의 값이면
                //   그 차이가 그대로 이탈로 잡힌다(이 파일이 실제로 겪은 형태).
                float anchorX = pet.HeadAnchorWorldPosition.x;
                float bodyX = agent.Blackboard.Body.position.x;
                maxAxisGap = Mathf.Max(maxAxisGap, Mathf.Abs(anchorX - bodyX));

                float deviation = Mathf.Abs(pet.PetWorldPosition.x - anchorX);
                maxFormulaError = Mathf.Max(maxFormulaError, Mathf.Abs(deviation - expectedOffset));
                samples++;
                yield return null;
            }

            Debug.Log($"{LogPrefix} [단계 A] 곧게 선 {samples}표본 — 공식값 {expectedOffset:F4}유닛 대비 " +
                $"최대 오차 {maxFormulaError:F4}(허용 {SampleTolerance:F4}), 기준축 간극(앵커 x − 몸 루트 x) " +
                $"최대 {maxAxisGap:F5}(허용 {UprightAxisTolerance:F5}), 몸통 물리 반폭 {physicalHalfWidth:F4}.");

            Assert.Greater(samples, 0,
                $"{LogPrefix} 표본을 한 개도 뜨지 못했습니다 — 관측 창이 0프레임입니다(측정 무효).");

            // A-0 — "지금 곧게 서 있다"의 자기검증. 이게 깨지면 아래 공식 검산의 기준축이 이미
            //       오염된 것이므로, 공식 오차보다 <b>먼저</b> 이 줄이 빨간불을 켜야 한다.
            Assert.Less(maxAxisGap, UprightAxisTolerance,
                $"{LogPrefix} 곧게 세웠는데도 머리 앵커 x가 몸 루트 x에서 최대 {maxAxisGap:F5}유닛 " +
                $"벌어졌습니다(허용 {UprightAxisTolerance:F5}) — 상체가 여전히 기울어 있어 이 단계의 " +
                "기준축 전제가 성립하지 않습니다(측정 무효).");

            // A-1 — 실제로 그려지는 매듭이 공식과 일치한다(양방향 허용오차).
            Assert.Less(maxFormulaError, SampleTolerance,
                $"{LogPrefix} 실측 편차가 공식값 {expectedOffset:F4}유닛에서 최대 {maxFormulaError:F4}유닛 " +
                $"벗어났습니다(허용 {SampleTolerance:F4}) — 화면 클램프나 다른 경로가 오프셋을 줄이고 " +
                "있거나, 공식과 실제 그림이 어긋났습니다.");

            // ================================================================
            // 단계 B — 신고 계약. "캐릭터가 멈춰있을때 겹치지 않고 바깥쪽에 있어야한다".
            // ================================================================
            // 이쪽은 <b>기울임을 손대지 않는다</b> — 유휴 앰비언트가 상체를 기울이는 순간이야말로
            // 풍선이 몸 쪽으로 가장 많이 다가오는 최악 표본이고, 신고가 말하는 "겹침"은 기준축이
            // 무엇이든 <b>몸 중심선 대비</b>로 판정되기 때문이다. 그래서 여기서는 몸 루트가 옳은 기준이다.
            float minAbsDeviation = float.MaxValue;
            float maxAmbientTilt = 0f;
            int contractSamples = 0;
            float contractDeadline = Time.realtimeSinceStartup + SampleWindowSeconds;
            while (Time.realtimeSinceStartup < contractDeadline)
            {
                float bodyX = agent.Blackboard.Body.position.x;
                float deviation = Mathf.Abs(pet.PetWorldPosition.x - bodyX);
                if (deviation < minAbsDeviation) minAbsDeviation = deviation;
                maxAmbientTilt = Mathf.Max(maxAmbientTilt, TorsoTilt(agent));
                contractSamples++;
                yield return null;
            }

            float worstCaseDeviation = minAbsDeviation - metrics.HeadRadius * BalloonRadiusInR;
            Debug.Log($"{LogPrefix} [단계 B] 앰비언트를 그대로 둔 {contractSamples}표본 — 몸 중심선 대비 " +
                $"최소 편차 {minAbsDeviation:F4}유닛, 주머니 반경을 뺀 최악 {worstCaseDeviation:F4}유닛 " +
                $"(몸통 물리 반폭 {physicalHalfWidth:F4}), 관측된 최대 기울임 {maxAmbientTilt:F1}도.");

            Assert.Greater(contractSamples, 0,
                $"{LogPrefix} 신고 계약 표본을 한 개도 뜨지 못했습니다(측정 무효).");
            Assert.Greater(worstCaseDeviation, physicalHalfWidth,
                $"{LogPrefix} 실측 편차에서 풍선 주머니 반경을 뺀 값({worstCaseDeviation:F4})이 몸통 " +
                $"물리 반폭({physicalHalfWidth:F4})보다 크지 않습니다 — 실제로 그려지는 풍선이 몸통 폭 " +
                "안에 걸쳐 있을 수 있습니다.");
        }

        /// <summary>
        /// ★ 기준축 대조군(2026-09-26 신설) — <b>무엇이 간헐 빨강을 만들었는지를 같은 파일에서 증명한다.</b>
        ///
        /// <para>위 단계 A는 상체를 곧게 세워 "기준축이 갈라지지 않는 세계"에서 공식을 검산한다.
        /// 그것만으로는 <b>왜 옛 판이 깨졌는가</b>를 아무도 말하지 않는다 — 곧게 세우면 옛 판(몸 루트
        /// 기준)도 같이 통과하기 때문이다. 그래서 이 테스트는 반대로 <b>기울인 채로</b> 두 기준을
        /// 나란히 재서, 같은 프레임에:</para>
        ///   (가) <b>기울어진 묶인 자리</b> 기준 편차는 공식값과 허용오차 안에서 일치하고,
        ///   (나) <b>몸 루트</b> 기준 편차는 공식값에서 허용오차보다 <b>크게</b> 벗어난다
        /// <para>는 것을 단언한다. (나)가 곧 "옛 판이었다면 여기서 빨개졌다"의 실측이고, (가)가
        /// "고친 기준축은 기울여도 맞다"의 실측이다. 둘 중 하나라도 무너지면 이 대조는 무의미해지므로
        /// 기울임 자체가 실제로 들어갔는지도 함께 못박는다(대조가 조용히 죽는 것을 막는다).</para>
        ///
        /// <para>★ 왜 <see cref="CharacterPetRenderer.HeadAnchorWorldPosition"/>을 그대로 쓰지 않는가:
        /// 그 앵커는 <b>종이비행기 궤도 중심</b>(머리 중심 아래 1.75R)이고 풍선이 묶이는 자리는 머리
        /// 중심 <b>위</b> 0.30R이다. 회전 팔이 2.05R만큼 다르므로 기울임이 들어가면 두 점의 x가
        /// 갈라진다(7도에서 약 0.04유닛 = 허용오차의 2배). 그래서 기울인 세계에서는 묶인 자리를
        /// <b>같은 프레임의 몸통 회전</b>으로 직접 재구성한다 —
        /// BodyLeanHeadAnchorFollowTests가 이미 같은 식으로 검증해 둔 그 공식이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 상체가_기울면_풍선의_기준축이_몸_루트가_아니라_기울어진_묶인자리다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            RaiseLevelTo(BalloonRequiredLevel, agent.Config);
            Assert.IsTrue(Wear(EquipmentSlot.Pet, PetBalloonItem),
                $"{LogPrefix} 풍선을 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            StickmanPoseAnimator pose = agent.Blackboard.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터가 없습니다.");

            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PetBalloonItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 풍선으로 빌드되지 않았습니다({pet.ActivePetItemIndex}).");

            // 기울임을 <b>고정</b>한다 — 목표가 움직이면 지수 추종(3.4/초)이 구조적으로 뒤처져
            // "기준축이 틀렸다"와 "아직 수렴하지 않았다"가 섞인다.
            float settleUntil = Time.realtimeSinceStartup + BalloonSettleSeconds;
            while (Time.realtimeSinceStartup < settleUntil)
            {
                pose.SetBodyLean(ControlLeanDegrees);
                yield return null;
            }
            pose.SetBodyLean(ControlLeanDegrees);

            float expectedOffset = pet.BalloonTetherOffsetWorld;
            float r = metrics.HeadRadius;
            Transform torso = FindDirectChild(agent.transform, "Torso");
            Assert.IsNotNull(torso, $"{LogPrefix} Torso를 못 찾았습니다.");

            Vector2 foot = agent.Blackboard.Body.position;
            var hip = new Vector2(0f, metrics.HipLocalY);
            var local = new Vector2(0f, metrics.HeadCenterLocalY + r * BalloonTetherAboveInR);
            float knotAnchorX = (foot + hip + (Vector2)(torso.localRotation * (local - hip))).x;

            float knotX = pet.PetWorldPosition.x;
            float leanedAxisError = Mathf.Abs(Mathf.Abs(knotX - knotAnchorX) - expectedOffset);
            float bodyAxisError = Mathf.Abs(Mathf.Abs(knotX - foot.x) - expectedOffset);
            float tilt = TorsoTilt(agent);

            Debug.Log($"{LogPrefix} [기준축 대조] 기울임 {tilt:F1}도 — 매듭 x {knotX:F4} / 묶인자리 앵커 x " +
                $"{knotAnchorX:F4} / 몸 루트 x {foot.x:F4}. 공식값 {expectedOffset:F4} 대비 오차: " +
                $"기울어진 앵커 기준 {leanedAxisError:F4}(허용 {SampleTolerance:F4}), " +
                $"몸 루트 기준 {bodyAxisError:F4}(옛 판이 쓰던 기준축). " +
                $"참고 — 렌더러가 공개한 머리 앵커 x {pet.HeadAnchorWorldPosition.x:F4}(궤도 중심, 회전 팔이 다르다).");

            // 대조 유효성 — 기울임이 실제로 들어갔고, 그 결과 두 기준축이 충분히 갈라졌는가.
            Assert.Greater(tilt, 3f,
                $"{LogPrefix} 상체가 기울지 않았습니다({tilt:F1}도) — 이 대조가 아무것도 증명하지 못합니다(대조 무효).");
            Assert.Greater(Mathf.Abs(knotAnchorX - foot.x), SampleTolerance,
                $"{LogPrefix} 기울였는데도 묶인 자리 x와 몸 루트 x의 차이가 허용오차" +
                $"({SampleTolerance:F4})보다 크지 않습니다 — 두 기준축을 구분할 수 없어 대조가 성립하지 않습니다(대조 무효).");

            // (가) 고친 기준축은 기울여도 맞다.
            Assert.Less(leanedAxisError, SampleTolerance,
                $"{LogPrefix} 기울어진 묶인 자리 기준 편차가 공식값에서 {leanedAxisError:F4}유닛 " +
                $"벗어났습니다(허용 {SampleTolerance:F4}) — 프로덕션이 더 이상 기울어진 머리에서 " +
                "오프셋을 재지 않거나, 추종이 수렴하지 못했습니다.");

            // (나) 옛 기준축(몸 루트)이었다면 같은 프레임에서 빨개진다 — 간헐 빨강의 원인 그 자체.
            Assert.Greater(bodyAxisError, SampleTolerance,
                $"{LogPrefix} 몸 루트 기준으로 재도 공식값과 허용오차 안({bodyAxisError:F4})입니다 — " +
                "두 기준축이 이 프레임에서 구분되지 않아, 이 테스트가 「기준축 불일치가 원인이었다」를 " +
                "증명하지 못합니다(대조 무효).");
        }

        // ==================== 헬퍼 ====================

        /// <summary>지금 적용돼 있는 상체 기울임(도, 절대값). 포즈 애니메이터의 내부 상태가 아니라
        /// <b>실제로 그려지는 몸통 회전</b>을 읽는다 — 렌더러가 보는 것과 같은 값이어야 한다.</summary>
        private static float TorsoTilt(StickmanAgent agent)
        {
            Transform torso = FindDirectChild(agent.transform, "Torso");
            return torso == null ? 0f : Mathf.Abs(Mathf.DeltaAngle(0f, torso.localEulerAngles.z));
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

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            return agent;
        }

        /// <summary>"이 아이템을 걸친 상태로 만든다". <see cref="EquipmentModel.TryWear"/>를 그냥 쓰면
        /// 안 된다 — <b>이미 그것을 걸치고 있으면 false</b>(변화 없음)를 돌려주기 때문이다
        /// (PetBalloonHatSortOrderTests.Wear와 같은 이유·같은 관례).</summary>
        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
        }

        /// <summary>목표 레벨까지 정상 경로(AddXp)로 올린다(기존 펫 테스트와 같은 관례).</summary>
        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level,
                $"{LogPrefix} 레벨 {level}까지 올리지 못했습니다 — 관측 전제가 성립하지 않습니다.");
        }

        private sealed class StillIntent : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            agent.Blackboard.IntentSource = new StillIntent();

            float deadline = Time.realtimeSinceStartup + 15f;
            StickmanStateId last = agent.Blackboard.Machine.CurrentStateId;
            float idleSince = -1f;
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
    }
}
