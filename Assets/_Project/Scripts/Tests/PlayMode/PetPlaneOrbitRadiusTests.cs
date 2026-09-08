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
    /// ★★ 실기(Windows) 실시간 사용자 신고 회귀 잠금(2026-09-07): <i>"종이비행기 착용했는데
    /// 캐릭터머리뒤에서만 돌고있음 너무 범위가 좁음. 캐릭터 주위로 돌아야하는데 그래서 거의
    /// 종이비행기가 안보임."</i>
    ///
    /// ============================================================================
    /// 확정된 원인 (계산으로 확정 — 추측 아님)
    /// ============================================================================
    /// 종이비행기 궤도의 가로 반폭(<see cref="CharacterPetRenderer.PlaneOrbitHalfWidthWorld"/> =
    /// 머리 반경 × <c>PlaneOrbitHalfWidthInR</c>)이 캐릭터 자신의 <b>물리적 반폭</b>
    /// (<see cref="StickmanBlackboard.CharacterPhysicalHalfWidthWorld"/>, 배율 1.0에서
    /// <see cref="StickConfig.BaselineBodyPhysicsHalfWidth"/> = 0.4유닛)보다 <b>작았다</b>
    /// (신고 당시 값 0.33유닛). 즉 궤도 위상이 어디에 있든(<c>cos</c>가 ±1이어도) 비행기가 몸통
    /// 자신의 폭 밖으로 단 한 번도 못 나갔다 — "궤도"가 아니라 몸통 폭 <b>안</b>에서의 제자리
    /// 떨림이었다. 여기에 반주기마다 도는 앞/뒤 레이어 전환이 겹치면서 "머리 뒤에서만 도는 것처럼"
    /// 보인 것이 정확히 재현된다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  P1  공식으로 유도한 가로 반폭이 몸통 물리 반폭보다 20% 이상 여유를 두고 크다.
    ///  P1n (네거티브) 신고 당시의 옛 상수(1.50 R)로 <b>같은 식</b>을 계산하면 이 조건이 성립하지
    ///      않는다 — P1이 우연이 아니라 이번 수정으로 성립한다는 증거(같은 파일 안에서 대조).
    ///  P2  <b>실제로 그려지는</b> 펫 위치(<see cref="CharacterPetRenderer.PetWorldPosition"/>)를
    ///      궤도 한 바퀴(3.2초)를 넘겨 실측한 최대 진폭이 공식값과 일치하고, 역시 물리 반폭보다
    ///      크다 — "공식은 맞는데 실제로 그려지는 것은 다르다"는 별개의 실패 유형을 반증한다.
    ///
    /// ============================================================================
    /// ★★ 2026-09-07 같은 밤 2차 신고 — "그래도 좁다 + 머리쪽에서만 돈다"
    /// ============================================================================
    /// 위 P1/P2로 닫은 것은 <b>가로</b> 결함뿐이었다. 세로는 반폭만 키우고 <b>중심을 머리 위에
    /// 고정</b>한 채였다(1차 수정 상수: 중심 2.35r · 반높이 0.90r → 타원이 통째로 정수리보다
    /// 위였다). 그래서 사용자가 "머리쪽에서만 돈다"고 다시 신고했다 — 가로축에서 이미 한 번
    /// 겪은 실패 유형의 <b>세로축 버전</b>이다.
    ///  Q1  타원 바닥이 어깨선보다 아래(몸통 진입).
    ///  Q2  타원 꼭대기가 정수리보다 위(머리 커버리지 유지).
    ///  Q1n (네거티브) 1차 수정 직후 상수(2.35r/0.90r)로는 Q1이 성립하지 않는다.
    /// </summary>
    public sealed class PetPlaneOrbitRadiusTests
    {
        private const string LogPrefix = "[PET-PLANE-ORBIT]";

        /// <summary>PET 카테고리의 "종이비행기" 자리 — AppearanceShapeBuilder.PetPlane과 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다(이 프로젝트의 기존 관례,
        /// PetFollowsOwnerFootholdTests의 PetBall과 같은 이유).</summary>
        private const int PetPlaneItem = 1;

        /// <summary>신고 당시(2026-09-07 1차 수정 전) 궤도 가로 반폭 상수 — 네거티브 컨트롤 전용.
        /// CharacterPetRenderer.PlaneOrbitHalfWidthInR의 <b>맨 처음</b> 값을 그대로 복제한다.
        /// 이 상수를 <b>다시 프로덕션 값으로 되돌리지 마라</b> — 이 테스트가 그 값을 반증하는
        /// 대조군이다.</summary>
        private const float PreFixOrbitHalfWidthInR = 1.50f;

        /// <summary>2026-09-07 1차 수정 직후(같은 밤 2차 신고 전) 값 — 아래 세로 커버리지 테스트의
        /// 네거티브 컨트롤 전용. "가로/세로를 2배 키우기만 했지 중심을 몸통 쪽으로 내리지 않았던"
        /// 상태를 그대로 복제한다. CharacterPetRenderer의 실제 상수를 <b>다시 이 값으로 되돌리지
        /// 마라</b> — 이 테스트가 그 상태를 반증하는 대조군이다.</summary>
        private const float PreSecondFixCenterAboveHeadInR = 2.35f;
        private const float PreSecondFixOrbitHalfHeightInR = 0.90f;

        /// <summary>공식 진폭과 실측 진폭 사이에 허용하는 오차(월드 유닛) — 화면 클램프/부동소수
        /// 오차 정도만 흡수한다.</summary>
        private const float SampleTolerance = 0.02f;

        /// <summary>몸통 물리 반폭 대비 요구하는 최소 여유 배수. 1.0이면 "간신히 안 겹친다" 수준이라
        /// 신고를 재현하지 않을 정도의 여유(20%)를 요구한다.</summary>
        private const float RequiredMargin = 1.2f;

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
        public IEnumerator 종이비행기_궤도_반폭이_몸통_물리_반폭보다_충분히_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, PetPlaneItem, null),
                $"{LogPrefix} 종이비행기를 걸치지 못했습니다.");

            // 빌드 + 첫 물리 반폭 계측이 돌 시간을 준다(StickmanAgent.TickVisualHalfWidth가
            // 첫 프레임에 즉시 갱신하지만, 안전하게 몇 프레임 더 기다린다).
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(PetPlaneItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 종이비행기로 빌드되지 않았습니다({pet.ActivePetItemIndex}).");

            float headRadius = metrics.HeadRadius;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            float orbitHalfWidth = pet.PlaneOrbitHalfWidthWorld;
            float required = physicalHalfWidth * RequiredMargin;

            Debug.Log($"{LogPrefix} 머리 반경 {headRadius:F4}, 몸통 물리 반폭 {physicalHalfWidth:F4}, " +
                $"궤도 가로 반폭 {orbitHalfWidth:F4}(요구 여유 {required:F4} = 물리 반폭×{RequiredMargin:F1}).");

            // P1 — 절대 조건.
            Assert.Greater(orbitHalfWidth, required,
                $"{LogPrefix} 궤도 가로 반폭 {orbitHalfWidth:F4}유닛이 몸통 물리 반폭 {physicalHalfWidth:F4}" +
                $"유닛의 {RequiredMargin:F1}배({required:F4})에 못 미칩니다 — 종이비행기가 다시 몸통 폭 " +
                "안에 갇혀 \"머리 뒤에서만 도는\" 신고가 재현될 수 있습니다.");

            // P1n — 네거티브 컨트롤: 신고 당시 상수로는 같은 조건이 성립하지 않았어야 한다.
            float preFixOrbitHalfWidth = headRadius * PreFixOrbitHalfWidthInR;
            Debug.Log($"{LogPrefix} [네거티브] 신고 당시 상수(1.50 R)로 계산한 궤도 반폭 " +
                $"{preFixOrbitHalfWidth:F4}유닛 — 물리 반폭 {physicalHalfWidth:F4}유닛보다 " +
                $"{(preFixOrbitHalfWidth < physicalHalfWidth ? "작다(신고 재현)" : "크다(대조 실패)")}.");
            Assert.LessOrEqual(preFixOrbitHalfWidth, physicalHalfWidth,
                $"{LogPrefix} 신고 당시 상수로도 조건이 성립합니다({preFixOrbitHalfWidth:F4} vs " +
                $"{physicalHalfWidth:F4}) — 이 테스트가 실제 신고를 반증하지 못합니다(무의미한 여유).");
        }

        /// <summary>
        /// ★★ 2026-09-07 같은 밤 2차 신고 회귀 잠금: <i>"종이비행기 도는 범위가 아직도 좁음 이전보다
        /// 넓어지긴했지만 지금의 2배는 되어야할듯. 그리고 머리쪽에서만 도는데 몸과 머리로 범위를
        /// 넓혀야함."</i>
        ///
        /// 1차 수정은 세로 반폭만 키우고 <b>중심을 머리 위에 고정</b>한 채였다 — 그래서 타원 전체가
        /// 정수리 위 공중에만 머물렀다(세로축 버전의 "머리 뒤에서만 도는" 결함). 이 테스트는 그 결함이
        /// 다시 들어오지 않는지 <b>두 절대 조건</b>으로 잠근다:
        ///   Q1 타원의 <b>바닥</b>(중심−세로반폭)이 <b>어깨선(ShoulderWorldY) 아래</b> — 궤도가
        ///      머리를 벗어나 몸통(가슴~어깨 아래)까지 내려온다는 뜻이다.
        ///   Q2 타원의 <b>꼭대기</b>(중심+세로반폭)가 <b>정수리(HeadTopWorldY) 위</b> — 몸통까지
        ///      내려오면서 머리 위 커버리지를 잃지 않았다는 뜻이다.
        /// 그리고 <b>네거티브 컨트롤</b>(Q1n) — 1차 수정 직후의 상수(중심 2.35r·반높이 0.90r)로
        /// 같은 Q1 조건을 계산하면 성립하지 않아야 한다(그때는 바닥이 정수리보다도 위였다).
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 종이비행기_궤도가_머리_위부터_몸통까지_감싼다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, PetPlaneItem, null),
                $"{LogPrefix} 종이비행기를 걸치지 못했습니다.");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PetPlaneItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 종이비행기로 빌드되지 않았습니다.");

            float headRadius = metrics.HeadRadius;
            float centerY = pet.HeadAnchorWorldPosition.y;
            float halfHeight = pet.PlaneOrbitHalfHeightWorld;
            float top = centerY + halfHeight;
            float bottom = centerY - halfHeight;

            float headTopY = metrics.HeadTopWorldY;
            float shoulderY = metrics.ShoulderWorldY;
            float hipY = metrics.HipWorldY;

            Debug.Log($"{LogPrefix} 궤도 중심Y {centerY:F4}, 세로반폭 {halfHeight:F4} → " +
                $"타원 [{bottom:F4}, {top:F4}]. 랜드마크 — 정수리 {headTopY:F4} / 어깨 {shoulderY:F4} / " +
                $"엉덩관절 {hipY:F4}.");

            // Q1 — 절대 조건: 바닥이 어깨선보다 아래(몸통 진입).
            Assert.Less(bottom, shoulderY,
                $"{LogPrefix} 타원 바닥({bottom:F4})이 어깨선({shoulderY:F4})보다 위입니다 — 궤도가 " +
                $"여전히 머리 근방에만 머뭅니다(2차 신고 재현). 엉덩관절({hipY:F4})까지는 못 가더라도 " +
                "최소한 어깨 아래(몸통)까지는 내려와야 합니다.");

            // Q2 — 절대 조건: 꼭대기가 정수리보다 위(머리 커버리지 유지).
            Assert.Greater(top, headTopY,
                $"{LogPrefix} 타원 꼭대기({top:F4})가 정수리({headTopY:F4})보다 아래입니다 — 몸통까지 " +
                "내려오면서 머리 위 커버리지를 잃었습니다.");

            // Q1n — 네거티브 컨트롤: 1차 수정 직후 상수로는 Q1이 성립하지 않았어야 한다.
            float preSecondCenterY = metrics.HeadCenterWorldY + headRadius * PreSecondFixCenterAboveHeadInR;
            float preSecondBottom = preSecondCenterY - headRadius * PreSecondFixOrbitHalfHeightInR;
            Debug.Log($"{LogPrefix} [네거티브] 1차 수정 직후 상수(중심 2.35r·반높이 0.90r)로 계산한 " +
                $"바닥 {preSecondBottom:F4} — 어깨선 {shoulderY:F4}보다 " +
                $"{(preSecondBottom < shoulderY ? "아래(성립, 대조 실패)" : "위(몸통 미진입, 신고 재현)")}.");
            Assert.GreaterOrEqual(preSecondBottom, headTopY,
                $"{LogPrefix} 1차 수정 직후 상수로도 바닥({preSecondBottom:F4})이 이미 정수리" +
                $"({headTopY:F4})보다 아래였습니다 — 이 테스트가 2차 신고를 반증하지 못합니다" +
                "(무의미한 대조).");

            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 실제로_그려지는_궤도_진폭이_공식값과_일치하고_몸통보다_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, PetPlaneItem, null),
                $"{LogPrefix} 종이비행기를 걸치지 못했습니다.");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PetPlaneItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 종이비행기로 빌드되지 않았습니다.");

            float expectedHalfWidth = pet.PlaneOrbitHalfWidthWorld;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;

            // 궤도 주기(3.2초)보다 넉넉히 길게 실측한다 — 벽시계 기준(이 저장소 확정 규칙:
            // 프레임 수 기반 대기 금지, 배치모드 PlayMode는 2,000fps 이상으로 돌 수 있다).
            float maxDeviation = 0f;
            float deadline = Time.realtimeSinceStartup + 4.0f;
            while (Time.realtimeSinceStartup < deadline)
            {
                float centerX = pet.HeadAnchorWorldPosition.x;
                float deviation = Mathf.Abs(pet.PetWorldPosition.x - centerX);
                if (deviation > maxDeviation) maxDeviation = deviation;
                yield return null;
            }

            Debug.Log($"{LogPrefix} 4초간 실측한 최대 가로 진폭 {maxDeviation:F4}유닛 " +
                $"(공식값 {expectedHalfWidth:F4}, 물리 반폭 {physicalHalfWidth:F4}).");

            Assert.Greater(maxDeviation, expectedHalfWidth - SampleTolerance,
                $"{LogPrefix} 실측 진폭 {maxDeviation:F4}유닛이 공식값 {expectedHalfWidth:F4}유닛에 " +
                "못 미칩니다 — 화면 클램프나 다른 경로가 궤도를 줄이고 있을 수 있습니다.");
            Assert.Less(maxDeviation, expectedHalfWidth + SampleTolerance,
                $"{LogPrefix} 실측 진폭 {maxDeviation:F4}유닛이 공식값 {expectedHalfWidth:F4}유닛을 " +
                "허용오차 이상으로 넘어섭니다 — 공식과 실제 그림이 어긋났습니다.");
            Assert.Greater(maxDeviation, physicalHalfWidth,
                $"{LogPrefix} 실측 진폭 {maxDeviation:F4}유닛이 몸통 물리 반폭 {physicalHalfWidth:F4}" +
                "유닛을 넘지 못합니다 — 실제로 그려지는 궤도가 몸통 폭 안에 갇혀 있습니다.");
        }

        /// <summary>
        /// ★★★ 2026-09-08 3차 신고 회귀 잠금: <i>"지금 종이비행기같은경우 몸의 반쪽까지만 주위를
        /// 돌고있어 척추를 중심으로해서 반경이 정해져있는데 몸 바깥쪽으로 돌수있게 범위 수정해줘."</i>
        ///
        /// 원인: 같은 날 코스튬 DLC 작업(design-motion 14-5, D-6 — 종이비행기 궤도가 새 코스튬
        /// 프롭 4종과 겹친다)이 궤도를 <b>상시</b> 뒤쪽 반원으로 접었다. 프롭이 실제로 서 있을 때만
        /// 필요한 조치인데 코스튬을 아예 안 입은 평소에도 걸려 있었다 — 이 신고의 실체다.
        ///
        /// 이 테스트는 코스튬을 <b>입지 않은</b>(=<see cref="CharacterPetRenderer.PlaneOrbitFoldedToBackHalf"/>
        /// 가 <c>false</c>여야 하는) 평소 상태에서, 궤도 한 바퀴(3.2초) 동안 앞/뒤 성분
        /// (<c>(x−center)÷side</c>, <see cref="CharacterPetRenderer.PlaneOrbitSideForTesting"/>로
        /// 캐릭터가 보는 방향을 지운 값)이 <b>양쪽 다</b> 공식 반폭 근처까지 도달하는지 잠근다 —
        /// 한쪽(음수)만 도달하면 반원 접기가 여전히 상시 걸려 있다는 뜻이다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 코스튬_프롭이_없으면_궤도가_완전한_타원이다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, PetPlaneItem, null),
                $"{LogPrefix} 종이비행기를 걸치지 못했습니다.");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PetPlaneItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 종이비행기로 빌드되지 않았습니다.");

            Assert.IsFalse(pet.PlaneOrbitFoldedToBackHalf,
                $"{LogPrefix} 코스튬을 입지 않았는데도 궤도가 뒤쪽 반원으로 접혀 있습니다 — " +
                "이 테스트의 전제가 깨졌습니다(프롭이 서 있지 않은데 접혔다).");

            float expectedHalfWidth = pet.PlaneOrbitHalfWidthWorld;
            float maxForward = float.NegativeInfinity;
            float minForward = float.PositiveInfinity;
            float deadline = Time.realtimeSinceStartup + 4.0f;
            while (Time.realtimeSinceStartup < deadline)
            {
                float side = pet.PlaneOrbitSideForTesting;
                if (Mathf.Abs(side) > 0.01f)
                {
                    float centerX = pet.HeadAnchorWorldPosition.x;
                    float forward = (pet.PetWorldPosition.x - centerX) / side;
                    if (forward > maxForward) maxForward = forward;
                    if (forward < minForward) minForward = forward;
                }
                yield return null;
            }

            Debug.Log($"{LogPrefix} 4초간 실측한 앞/뒤 성분 범위 [{minForward:F4}, {maxForward:F4}] " +
                $"(공식 반폭 {expectedHalfWidth:F4}).");

            // 절대 조건 — 완전한 타원이면 앞(+)/뒤(−) 모두 반폭 근처까지 도달한다.
            Assert.Greater(maxForward, expectedHalfWidth - SampleTolerance,
                $"{LogPrefix} 앞쪽(+) 최대 도달값 {maxForward:F4}이 공식 반폭 {expectedHalfWidth:F4}에 " +
                "못 미칩니다 — 코스튬 없이도 궤도가 여전히 뒤쪽 반원으로 접혀 있습니다(신고 재현).");
            Assert.Less(minForward, -(expectedHalfWidth - SampleTolerance),
                $"{LogPrefix} 뒤쪽(−) 최대 도달값 {minForward:F4}이 −공식 반폭({-expectedHalfWidth:F4})에 " +
                "못 미칩니다 — 뒤쪽 실루엣 범위가 줄었습니다(회귀).");

            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            yield return null;
        }

        // ==================== 헬퍼 ====================

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            return agent;
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
