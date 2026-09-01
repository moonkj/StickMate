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
    /// ★ 상체 기울임 ↔ <b>머리 기준 앵커 렌더러 4종</b> 교차 레이어 잠금 — 2026-09-01.
    /// (Tasklist "P9-b 교차 레이어 영향 로그 #22"의 미해결 항목)
    ///
    /// ============================================================================
    /// 이 파일이 잡는 결함
    /// ============================================================================
    /// <see cref="StressGaugeRenderer"/> / <see cref="FocusWatchRenderer"/> /
    /// <see cref="CharacterFxRenderer"/> / <see cref="CharacterPetRenderer"/>는 전부
    /// <see cref="StickmanMetrics"/>의 <b>중립(기울지 않은)</b> 머리·어깨 좌표로 위치를 잡았다.
    /// 그런데 States/StickmanPoseAnimator.SetBodyLean이 들어오면서 걷는 동안 상체는 <b>엉덩이를 축으로</b>
    /// 돈다 — 머리는 앞으로 나가는데 한숨 퍼프/곁눈질/반짝임/펫만 제자리에 남았다.
    /// (배율 0.75, 걷기 기울임 10도 기준 약 0.15유닛 = 화면 약 5pt.)
    ///
    /// ============================================================================
    /// 무엇을 재는가 — 렌더러마다 <b>가장 정직한 지표</b>를 고른다
    /// ============================================================================
    /// 네 경우 모두 지표는 하나다 — <b>"엉덩이를 축으로 상체와 같은 각도만큼 돌았는가"</b>.
    /// · 스트레스/집중모드는 도형이 실존하므로 <see cref="LineRenderer"/>의 <b>점</b>을 월드로 올려
    ///   기울임 전 위치를 엉덩이 피벗으로 돌린 예측과 비교한다.
    ///   <b>선 오브젝트의 <c>transform.position</c>을 쓰면 안 된다</b> — 이 두 렌더러는 선 오브젝트를
    ///   컨테이너 원점에 그대로 두고 점 좌표만 로컬에 담으므로, 그 위치는 컨테이너 회전에
    ///   <b>구조적으로 반응하지 않는다</b>(초안이 실제로 그 함정에 빠져 양성/네거티브가 같은 값을 냈다).
    /// · FX/펫은 조각이 <b>월드 고정</b>이라 "지금 어디를 기준으로 삼는가"가 앵커 값 그 자체다.
    ///   그래서 렌더러가 공개한 앵커를 읽고, <b>엉덩이 피벗 예측</b>과 일치하는지 본다.
    ///   그리고 실제로 스폰된 조각(나뭇잎 / 풍선)이 그 앵커를 따라갔는지 한 번 더 확인한다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 — 전부 같은 파일 안에 있다
    /// ============================================================================
    /// · 스트레스/집중모드: 렌더러가 이번 프레임에 세팅한 <b>회전만 지우면</b>(= 이번 라운드 이전의
    ///   거동) 같은 지표가 허용오차를 크게 넘어선다.
    /// · FX/펫: <b>중립 앵커</b>(= 옛 식)와 <b>발밑 피벗 예측</b>(= 흔한 오답) 둘 다 실측과
    ///   확실히 벌어진다는 것을 수치로 남긴다.
    /// </summary>
    public sealed class BodyLeanHeadAnchorFollowTests
    {
        private const string LogPrefix = "[LEAN-ANCHOR]";
        private const float LeanDegrees = 20f;

        /// <summary>나뭇잎 스폰의 좌우 무작위 폭(머리 반경 배수) —
        /// <c>CharacterFxRenderer.LeafSpawnSpreadInR</c>과 같은 값이다.</summary>
        private const float LeafSpawnSpreadInR = 1.1f;

        /// <summary>FX 나뭇잎 / PET 풍선의 자리와 요구 레벨.</summary>
        private const int FxLeaf = 5, PetBalloon = 4, TopRequiredLevel = 30;

        /// <summary>스트레스 상시 표시가 기본 OFF가 되기 전의 원래 주의 경계값
        /// (Phase5VisualLayerTests와 같은 이유의 같은 값 — 렌더러 <b>능력</b>을 보려면 잠깐 되돌려야 한다).</summary>
        private const float OriginalCautionLevel = 0.4f;

        private StickConfig _configToRestore;
        private float _savedCautionLevel;

        [TearDown]
        public void RestoreStressThreshold()
        {
            if (_configToRestore == null) return;
            _configToRestore.stressTierCautionLevel = _savedCautionLevel;
            _configToRestore = null;
            StressGauge.SetLevel(0f);
        }

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            StressGauge.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // (1) 스트레스 게이지 — 어깨 처짐 / 한숨 퍼프
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 스트레스_어깨표시가_상체_기울임을_따라간다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            yield return ShowStressAlarm(agent);

            Transform probe = FindLineByName("StressMoodOverlay", "ShoulderDroopR");
            Assert.IsNotNull(probe, $"{LogPrefix} 어깨 처짐 선을 찾지 못했습니다(표시가 안 떴습니까?).");

            // 컨테이너 원점은 <b>발바닥</b>이므로(AnchorWorldPosition = Body.position) 로컬 피벗은 고관절 높이 그 자체다.
            var hip = new Vector2(0f, agent.GetComponent<StickmanMetrics>().HipLocalY);
            yield return AssertFollowsTorso(agent, probe, "어깨 처짐", probe.parent, hip);
        }

        /// <summary>네거티브 컨트롤 — 컨테이너 회전만 지우면 같은 지표가 실제로 깨진다.</summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 스트레스_컨테이너_회전을_지우면_같은_지표가_깨진다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            yield return ShowStressAlarm(agent);

            Transform probe = FindLineByName("StressMoodOverlay", "ShoulderDroopR");
            Assert.IsNotNull(probe, $"{LogPrefix} 어깨 처짐 선을 찾지 못했습니다.");

            yield return AssertBreaksWithoutRotation(agent, probe, "어깨 처짐", container: probe.parent);
        }

        // ============================================================================
        // (2) 집중 모드 — 곁눈질 호는 따라가고, 발밑 링은 <b>따라가면 안 된다</b>
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 집중모드_곁눈질이_상체_기울임을_따라간다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            yield return ShowFocusGlance();

            Transform probe = FindLineByName("FocusWatchRing", "GlanceR");
            Assert.IsNotNull(probe, $"{LogPrefix} 곁눈질 호를 찾지 못했습니다(1단계가 안 떴습니까?).");
            Assert.AreEqual("GlanceGroup", probe.parent.name,
                $"{LogPrefix} 곁눈질 호가 회전 그룹 밖에 있습니다 — 그러면 기울임을 절대 따라갈 수 없습니다.");

            // 곁눈질 그룹의 부모(링 컨테이너) 원점은 <b>링 중심</b>이므로 그만큼 빼서 로컬 피벗을 만든다.
            var focus = Object.FindFirstObjectByType<FocusWatchRenderer>();
            var hip = new Vector2(0f,
                agent.GetComponent<StickmanMetrics>().HipLocalY - focus.RingCenterLocalY);
            yield return AssertFollowsTorso(agent, probe, "곁눈질 호", probe.parent, hip);
        }

        /// <summary>네거티브 컨트롤 — 곁눈질 그룹의 회전만 지우면 같은 지표가 실제로 깨진다.</summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 집중모드_곁눈질_그룹_회전을_지우면_같은_지표가_깨진다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            yield return ShowFocusGlance();

            Transform probe = FindLineByName("FocusWatchRing", "GlanceR");
            Assert.IsNotNull(probe, $"{LogPrefix} 곁눈질 호를 찾지 못했습니다.");

            yield return AssertBreaksWithoutRotation(agent, probe, "곁눈질 호", container: probe.parent);
        }

        /// <summary>
        /// ★ 반대 방향의 잠금 — <b>발밑 타이머 링은 기울어지면 안 된다</b>(18절 "캐릭터 발밑, 앱 소유 UI").
        /// 링까지 함께 돌리면 회전 중심(엉덩이)보다 아래에 있는 링이 비스듬히 눕는다. 이 테스트가 없으면
        /// "따라가게 만들었다"는 수정이 조용히 <b>과하게</b> 적용돼도 아무도 모른다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 집중모드_발밑_링은_기울임을_따라가지_않는다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            yield return ShowFocusGlance();

            Transform ring = FindLineByName("FocusWatchRing", "RingTrack");
            Assert.IsNotNull(ring, $"{LogPrefix} 타이머 링을 찾지 못했습니다.");
            StickmanPoseAnimator pose = Pose(agent);

            float maxRingTilt = 0f, maxTilt = 0f;
            for (int i = 0; i < 16; i++)
            {
                pose.SetBodyLean(LeanDegrees);
                yield return null;
                pose.SetBodyLean(LeanDegrees);
                maxTilt = Mathf.Max(maxTilt, TorsoTilt(agent));
                maxRingTilt = Mathf.Max(maxRingTilt,
                    Mathf.Abs(Mathf.DeltaAngle(0f, ring.eulerAngles.z)));
            }

            Debug.Log($"{LogPrefix} 상체 {maxTilt:F1}도 기울어도 발밑 링의 기울기는 {maxRingTilt:F3}도.");
            Assert.Greater(maxTilt, 5f, $"{LogPrefix} 상체가 기울지 않아 이 검사가 아무것도 증명하지 못합니다.");
            Assert.Less(maxRingTilt, 0.5f,
                $"{LogPrefix} 발밑 타이머 링이 {maxRingTilt:F2}도 기울었습니다 — 18절이 지정한 " +
                "'캐릭터 발밑 위젯'이 몸을 따라 눕습니다.");
        }

        // ============================================================================
        // (3) FX — 반짝임/나뭇잎의 머리 앵커
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FX_머리_앵커가_엉덩이_피벗으로_회전한다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            var fx = Object.FindFirstObjectByType<CharacterFxRenderer>();
            Assert.IsNotNull(fx, $"{LogPrefix} CharacterFxRenderer가 씬에 없습니다.");

            yield return AssertHeadAnchorRotatesAboutHip(agent, "FX",
                fx.HeadAnchorAboveHeadCenter, () => fx.HeadAnchorWorldPosition);
        }

        /// <summary>
        /// 실제로 그려진 조각이 그 앵커를 따라갔는가 — 나뭇잎의 스폰 위치로 확인한다.
        ///
        /// <para>잎의 x는 앵커에서 ±<see cref="LeafSpawnSpreadInR"/>R 안에서 무작위다. 그런데 20도
        /// 기울임이 앵커를 옮기는 거리는 그 폭보다 <b>확실히 크므로</b>, "잎이 몸의 중심선에서 폭보다
        /// 멀리 떨어져 있다"는 조건은 <b>기울임을 따라갔을 때만</b> 성립한다 — 무작위성이 있어도
        /// 결론이 흔들리지 않는 지표다(네거티브 컨트롤이 부등식 안에 들어 있다).</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FX_나뭇잎이_기울어진_머리_위에서_떨어진다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            RaiseLevelTo(TopRequiredLevel, agent.Config);
            ClearAll();

            StickmanPoseAnimator pose = Pose(agent);
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            float r = metrics.HeadRadius;

            // 기울인 채로 착용한다 — 나뭇잎은 착용 직후 첫 장이 바로 떨어진다.
            for (int i = 0; i < 4; i++) { pose.SetBodyLean(LeanDegrees); yield return null; }
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Fx, FxLeaf, null),
                $"{LogPrefix} 나뭇잎을 걸치지 못했습니다.");

            Transform leaf = null;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && leaf == null)
            {
                pose.SetBodyLean(LeanDegrees);
                yield return null;
                leaf = FindChildStartingWith("CharacterFx", "Leaf");
            }
            Assert.IsNotNull(leaf, $"{LogPrefix} 나뭇잎이 한 장도 떨어지지 않았습니다.");

            float bodyX = agent.Blackboard.Body.position.x;
            float offset = Mathf.Abs(leaf.position.x - bodyX);
            float spread = r * LeafSpawnSpreadInR;

            Debug.Log($"{LogPrefix} 나뭇잎 스폰 x가 몸 중심선에서 {offset:F4}유닛 " +
                $"(무작위 폭 {spread:F4}유닛, 머리 반경 {r:F4}) — 기울임 {TorsoTilt(agent):F1}도.");

            Assert.Greater(offset, spread,
                $"{LogPrefix} 나뭇잎이 몸 중심선에서 {offset:F4}유닛 떨어졌습니다 — 무작위 폭 " +
                $"{spread:F4}유닛 안이라 <b>기울지 않은</b> 머리 위에서 떨어진 것과 구분되지 않습니다. " +
                "즉 스폰 기준이 여전히 중립 머리입니다.");

            EquipmentModel.TryWear(EquipmentSlot.Fx, EquipmentModel.NotWorn, null);
            yield return null;
        }

        // ============================================================================
        // (4) PET — 종이비행기 궤도 중심 / 풍선 매듭
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PET_머리_앵커가_엉덩이_피벗으로_회전한다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            var pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            yield return AssertHeadAnchorRotatesAboutHip(agent, "PET",
                pet.HeadAnchorAboveHeadCenter, () => pet.HeadAnchorWorldPosition);
        }

        /// <summary>
        /// 실제로 그려진 펫이 그 앵커를 따라갔는가 — 풍선의 <b>매달린 쪽이 뒤집힌다</b>.
        ///
        /// <para>풍선 매듭은 머리 중심에서 <b>진행 반대쪽</b>으로 0.75R 떨어져 있다. 그런데 20도
        /// 기울임이 머리를 앞으로 보내는 거리는 그보다 커서, 따라가면 매듭이 <b>진행 방향 앞쪽</b>으로
        /// 넘어온다. 즉 <b>부호가 뒤집힌다</b> — 허용오차 조정으로는 통과시킬 수 없는 지표다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PET_풍선이_기울어진_머리_쪽으로_넘어온다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            RaiseLevelTo(TopRequiredLevel, agent.Config);
            ClearAll();

            var pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");
            StickmanPoseAnimator pose = Pose(agent);
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, PetBalloon, null),
                $"{LogPrefix} 풍선을 걸치지 못했습니다.");

            // 추종 계수 3.4/초 — 1.5초면 0.6% 안으로 수렴한다.
            float deadline = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                pose.SetBodyLean(LeanDegrees);
                yield return null;
            }

            float facing = agent.Blackboard.FacingSign >= 0f ? 1f : -1f;
            float bodyX = agent.Blackboard.Body.position.x;
            float ahead = (pet.PetWorldPosition.x - bodyX) * facing;
            float r = metrics.HeadRadius;

            Debug.Log($"{LogPrefix} 풍선 매듭이 몸 중심선보다 진행 방향으로 {ahead:F4}유닛 " +
                $"(머리 반경 {r:F4}) — 기울임 {TorsoTilt(agent):F1}도. " +
                "기울임을 안 따라갔다면 이 값은 -0.75R이어야 한다.");

            Assert.Greater(ahead, r * 0.4f,
                $"{LogPrefix} 풍선이 몸 중심선보다 진행 방향으로 {ahead:F4}유닛에 있습니다 — " +
                "기울임을 따라가지 않으면 매듭은 <b>진행 반대쪽</b>(음수)에 머뭅니다.");

            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            yield return null;
        }

        // ============================================================================
        // 공통 지표
        // ============================================================================

        /// <summary>
        /// 실제로 그려진 점을 <b>몸통 로컬</b>로 되돌린 값이 기울임 내내 변하지 않는가.
        /// 컨테이너가 몸통과 같은 피벗·같은 각도로 돌면 이 값은 정의상 상수다.
        /// </summary>
        /// <summary>
        /// 실제로 그려진 <b>선의 점</b>과 <b>컨테이너의 위치</b>가 "엉덩이 피벗 회전"과 정확히 일치하는가.
        /// 두 가지를 나눠서 본다 — 둘 다 있어야 "같은 피벗으로 같은 각도"가 증명된다:
        ///   (1) <b>회전</b>: 컨테이너 원점에서 본 점의 상대 위치가 기울임 각도만큼 정확히 돌았는가.
        ///   (2) <b>피벗</b>: 컨테이너 자신이 <c>엉덩이 − R·엉덩이</c>만큼 옮겨졌는가
        ///       (이게 없으면 도형이 <b>컨테이너 원점</b>, 즉 발밑을 축으로 도는 그림이 된다).
        ///
        /// <para>★ 두 지표 모두 <b>기울임 전후의 차이</b>로만 잰다. 두 렌더러는 오버레이가 화면 밖으로
        /// 잘리지 않도록 앵커를 뷰포트 안으로 <b>클램프</b>하는데(캐릭터는 Dock 위, 즉 화면 최하단에
        /// 서 있는 시간이 길다), 그 클램프 offset은 기울임과 무관한 상수라 차이를 취하면 정확히
        /// 상쇄된다. 초안은 절대 좌표로 쟀다가 그 상수를 이탈로 오인했다 — 실측 잔차
        /// (어깨 0.12745 / 곁눈질 0.08708)의 <b>차이 0.1162</b>가 두 렌더러의 클램프 여유 차이
        /// 0.1162와 소수점 넷째 자리까지 같다는 것으로 원인을 확정했다.</para>
        ///
        /// <para>★ 선의 <c>transform.position</c>이 아니라 <see cref="LineRenderer"/>의 <b>점</b>을 쓰는
        /// 이유: 이 두 렌더러는 선 오브젝트를 컨테이너 원점에 그대로 두고 점 좌표만 로컬에 담으므로,
        /// 오브젝트 위치는 컨테이너 회전에 <b>구조적으로 반응하지 않는다</b>.</para>
        /// </summary>
        /// <param name="pivotInContainerLocal">컨테이너 로컬 좌표계에서 본 고관절(회전 중심).</param>
        private IEnumerator AssertFollowsTorso(StickmanAgent agent, Transform probe, string what,
            Transform container, Vector2 pivotInContainerLocal)
        {
            Assert.IsNotNull(container, $"{LogPrefix} {what}의 회전 컨테이너를 못 찾았습니다.");

            float headRadius = agent.GetComponent<StickmanMetrics>().HeadRadius;
            StickmanPoseAnimator pose = Pose(agent);

            // 기준선은 <b>기울임이 정확히 0인 프레임</b>에서 잡는다. 유휴 앰비언트 "주위 살피기"가
            // 최대 7도까지 기울임을 요청하므로, 그냥 재면 기준선 자체가 기운 상태로 굳는다.
            pose.SetBodyLean(0f);
            yield return null;
            pose.SetBodyLean(0f);

            Vector2 restRel = ProbePoint(probe) - (Vector2)container.position;
            Vector3 restLocal = container.localPosition;
            Vector2 restWorld = ProbePoint(probe);

            Assert.Greater((restRel - pivotInContainerLocal).magnitude, headRadius,
                $"{LogPrefix} {what}의 관측점이 회전 중심에서 너무 가깝습니다 — 회전이 위치를 거의 " +
                "바꾸지 않으므로 이 검사가 무의미해집니다.");

            float maxSpin = 0f, maxPivot = 0f, maxTilt = 0f, maxWorldMove = 0f;
            for (int i = 0; i < 20; i++)
            {
                pose.SetBodyLean(LeanDegrees);
                yield return null;   // 이 프레임의 LateUpdate가 컨테이너를 이 각도로 놓는다.

                // ★ 코루틴은 다음 프레임의 Update <b>뒤</b>에 깨어나므로 그 사이 TickBodyLean이
                //   기울임을 0 쪽으로 한 스텝 감쇠시켜 두었다. 다시 세워 두지 않으면 "컨테이너가
                //   그려진 각도"와 "지금 몸통 각도"가 한 스텝 어긋나 그 차이가 그대로 이탈로 잡힌다.
                pose.SetBodyLean(LeanDegrees);

                Quaternion rot = TorsoRotation(agent);
                Vector2 nowRel = ProbePoint(probe) - (Vector2)container.position;

                maxTilt = Mathf.Max(maxTilt, TorsoTilt(agent));
                maxWorldMove = Mathf.Max(maxWorldMove, Vector2.Distance(ProbePoint(probe), restWorld));
                maxSpin = Mathf.Max(maxSpin, Vector2.Distance(nowRel, (Vector2)(rot * restRel)));

                Vector3 movedBy = container.localPosition - restLocal;
                Vector3 expectedMove = (Vector3)pivotInContainerLocal - rot * (Vector3)pivotInContainerLocal;
                maxPivot = Mathf.Max(maxPivot, Vector3.Distance(movedBy, expectedMove));
            }

            float tolerance = headRadius * 0.05f;
            Debug.Log($"{LogPrefix} {what} — 기울임 최대 {maxTilt:F1}도, 회전 이탈 {maxSpin:F5}유닛 / " +
                $"피벗 이탈 {maxPivot:F5}유닛 (허용 {tolerance:F5} = 머리 반경 5%), " +
                $"월드 이동 {maxWorldMove:F4}유닛.");

            Assert.Greater(maxTilt, 5f,
                $"{LogPrefix} 상체가 기울지 않았습니다({maxTilt:F1}도) — 이 검사가 아무것도 증명하지 못합니다.");
            Assert.Greater(maxWorldMove, headRadius * 0.3f,
                $"{LogPrefix} {what}이(가) 월드에서 거의 안 움직였습니다({maxWorldMove:F4}유닛) — " +
                "기울임이 그림에 반영되지 않았습니다.");
            Assert.Less(maxSpin, tolerance,
                $"{LogPrefix} {what}이(가) 상체와 <b>같은 각도로 돌지 않았습니다</b>({maxSpin:F5}유닛, " +
                $"허용 {tolerance:F5}).");
            Assert.Less(maxPivot, tolerance,
                $"{LogPrefix} {what}의 회전 중심이 고관절이 아닙니다(피벗 이탈 {maxPivot:F5}유닛, " +
                $"허용 {tolerance:F5}) — 컨테이너 원점(발밑)을 축으로 돌고 있습니다.");
        }

        /// <summary>네거티브 컨트롤 — 회전만 지우면(= 이번 라운드 이전의 거동) 같은 지표가 깨진다.</summary>
        private IEnumerator AssertBreaksWithoutRotation(StickmanAgent agent, Transform probe, string what,
            Transform container)
        {
            float headRadius = agent.GetComponent<StickmanMetrics>().HeadRadius;
            StickmanPoseAnimator pose = Pose(agent);

            pose.SetBodyLean(0f);
            yield return null;
            pose.SetBodyLean(0f);   // 짝 테스트와 같은 이유의 같은 기준선 정리.

            Vector2 restRel = ProbePoint(probe) - (Vector2)container.position;

            float drift = 0f;
            for (int i = 0; i < 12; i++)
            {
                pose.SetBodyLean(LeanDegrees);
                yield return null;
                pose.SetBodyLean(LeanDegrees);

                // 렌더러가 이번 프레임에 세팅한 회전을 지운다(위치는 그대로) = 회전 추종이 없던 코드.
                container.localRotation = Quaternion.identity;

                Quaternion rot = TorsoRotation(agent);
                Vector2 nowRel = ProbePoint(probe) - (Vector2)container.position;
                drift = Mathf.Max(drift, Vector2.Distance(nowRel, (Vector2)(rot * restRel)));
            }

            float tolerance = headRadius * 0.05f;
            Debug.Log($"{LogPrefix} [네거티브] {what}의 회전을 지우면 회전 이탈 {drift:F4}유닛 " +
                $"(허용 {tolerance:F5} — {drift / Mathf.Max(1e-6f, tolerance):F0}배).");

            Assert.Greater(drift, tolerance,
                $"{LogPrefix} 회전을 지웠는데도 지표가 안 깨집니다 — 짝이 되는 테스트가 아무것도 " +
                "증명하지 못한다는 뜻입니다.");
        }

        /// <summary>
        /// 이 선이 실제로 그리는 <b>첫 점</b>의 월드 좌표. GameObject의 위치가 아니라 점을 쓰는 이유는
        /// 위 <see cref="AssertFollowsTorso"/> 문서 참고.
        /// </summary>
        private static Vector2 ProbePoint(Transform probe)
        {
            var lr = probe.GetComponent<LineRenderer>();
            Assert.IsNotNull(lr, $"{LogPrefix} 관측 대상 '{probe.name}'에 LineRenderer가 없습니다.");
            Assert.Greater(lr.positionCount, 0, $"{LogPrefix} '{probe.name}'에 점이 없습니다.");
            return probe.TransformPoint(lr.GetPosition(0));
        }

        private static Quaternion TorsoRotation(StickmanAgent agent)
        {
            Transform torso = FindDirectChild(agent.transform, "Torso");
            return torso != null ? torso.localRotation : Quaternion.identity;
        }

        /// <summary>
        /// 머리 앵커가 <b>엉덩이</b>를 축으로 돌았는가. 두 가지 오답을 같은 프레임에서 함께 반증한다:
        /// ① 중립 앵커(= 옛 식, 아예 안 돈다) ② 발밑 피벗(= 흔한 오답, 너무 많이 돈다).
        /// </summary>
        private IEnumerator AssertHeadAnchorRotatesAboutHip(StickmanAgent agent, string what,
            float aboveHeadCenter, System.Func<Vector2> readAnchor)
        {
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Transform torso = FindDirectChild(agent.transform, "Torso");
            StickmanPoseAnimator pose = Pose(agent);
            Assert.IsNotNull(torso, $"{LogPrefix} Torso를 못 찾았습니다.");

            // (a) 기울이지 않았을 때는 옛 식과 <b>정확히</b> 같아야 한다(무회귀의 증거).
            //     유휴 앰비언트 "주위 살피기"가 최대 7도까지 기울임을 요청하므로 먼저 확실히 세운다.
            pose.ClearBodyLean();
            Assert.Less(TorsoTilt(agent), 0.5f,
                $"{LogPrefix} {what}: 기울임을 지웠는데 몸통이 여전히 기울어 있습니다 — (a)의 전제가 성립하지 않습니다.");

            Vector2 flat = readAnchor();
            Vector2 flatNeutral = Neutral(agent, metrics, aboveHeadCenter);
            Assert.AreEqual(flatNeutral.x, flat.x, 1e-4f,
                $"{LogPrefix} {what}: 기울임이 0인데 앵커가 옛 식과 다릅니다 — 거동이 조용히 바뀌었습니다.");
            Assert.AreEqual(flatNeutral.y, flat.y, 1e-4f,
                $"{LogPrefix} {what}: 기울임이 0인데 앵커가 옛 식과 다릅니다 — 거동이 조용히 바뀌었습니다.");

            // (b) 기울이면 엉덩이 피벗 예측과 일치해야 한다.
            for (int i = 0; i < 6; i++) { pose.SetBodyLean(LeanDegrees); yield return null; }
            pose.SetBodyLean(LeanDegrees);

            Vector2 actual = readAnchor();
            Vector2 neutral = Neutral(agent, metrics, aboveHeadCenter);
            Quaternion rot = torso.localRotation;

            Vector2 foot = agent.Blackboard.Body.position;
            var hip = new Vector2(0f, metrics.HipLocalY);
            var local = new Vector2(0f, metrics.HeadCenterLocalY + aboveHeadCenter);
            Vector2 hipPrediction = foot + hip + (Vector2)(rot * (local - hip));
            Vector2 footPrediction = foot + (Vector2)(rot * local);   // 흔한 오답: 발바닥을 축으로

            float headRadius = metrics.HeadRadius;
            float toHip = Vector2.Distance(actual, hipPrediction);
            float toNeutral = Vector2.Distance(actual, neutral);
            float toFoot = Vector2.Distance(actual, footPrediction);

            Debug.Log($"{LogPrefix} {what} 앵커 — 엉덩이 피벗 예측과 {toHip:F6}유닛, " +
                $"중립(옛 식)과 {toNeutral:F4}유닛, 발밑 피벗 예측과 {toFoot:F4}유닛 " +
                $"(머리 반경 {headRadius:F4}, 기울임 {TorsoTilt(agent):F1}도).");

            Assert.Less(toHip, headRadius * 0.02f,
                $"{LogPrefix} {what} 앵커가 엉덩이 피벗 예측에서 {toHip:F5}유닛 벗어났습니다 — " +
                "회전 중심이 엉덩이가 아닙니다.");
            Assert.Greater(toNeutral, headRadius * 0.5f,
                $"{LogPrefix} {what} 앵커가 중립(기울지 않은) 머리에서 {toNeutral:F4}유닛밖에 " +
                "안 벗어났습니다 — 기울임을 사실상 안 따라가고 있습니다(옛 거동).");
            Assert.Greater(toFoot, headRadius * 0.2f,
                $"{LogPrefix} {what} 앵커가 발밑 피벗 예측과 {toFoot:F4}유닛으로 구분되지 않습니다 — " +
                "이 검사가 피벗 위치를 잠그지 못합니다.");
        }

        /// <summary>옛 식(= 중립 머리) 그대로의 앵커. 프로덕션 코드에 화석으로 남기지 않고
        /// 테스트가 <see cref="StickmanMetrics"/>만으로 다시 만든다.</summary>
        private static Vector2 Neutral(StickmanAgent agent, StickmanMetrics metrics, float aboveHeadCenter)
        {
            Vector2 foot = agent.Blackboard.Body.position;
            return new Vector2(foot.x, foot.y + metrics.HeadCenterLocalY + aboveHeadCenter);
        }

        // ============================================================================
        // 연출 켜기
        // ============================================================================

        /// <summary>스트레스 상시 표시는 출하 설정에서 <b>기본 OFF</b>다(도달 불가능한 임계값).
        /// 렌더러 능력을 보려면 잠깐 원래 기본값으로 되돌린다 — TearDown이 반드시 복원한다.</summary>
        private IEnumerator ShowStressAlarm(StickmanAgent agent)
        {
            StressGauge.ResetForTesting();
            _configToRestore = agent.Config;
            _savedCautionLevel = agent.Config.stressTierCautionLevel;
            agent.Config.stressTierCautionLevel = OriginalCautionLevel;

            StressGauge.SetLevel(Mathf.Clamp01(agent.Config.stressSulkyThreshold) + 0.01f);
            yield return null;
            yield return null;

            var renderer = Object.FindFirstObjectByType<StressGaugeRenderer>();
            Assert.IsNotNull(renderer, $"{LogPrefix} StressGaugeRenderer가 씬에 없습니다.");
            Assert.IsTrue(renderer.IsVisible, $"{LogPrefix} 경고 단계인데 기분 표시가 뜨지 않았습니다.");
        }

        private IEnumerator ShowFocusGlance()
        {
            var director = Object.FindFirstObjectByType<FocusWatchDirector>();
            var renderer = Object.FindFirstObjectByType<FocusWatchRenderer>();
            Assert.IsNotNull(director, $"{LogPrefix} FocusWatchDirector가 씬에 없습니다.");
            Assert.IsNotNull(renderer, $"{LogPrefix} FocusWatchRenderer가 씬에 없습니다.");

            director.ForceTriggerNow("PlayMode 테스트(기울임 추종)");
            yield return null;
            yield return null;
            Assert.IsTrue(renderer.IsRingVisible, $"{LogPrefix} 타이머 링이 뜨지 않았습니다.");

            StickmanEventBus.RaiseFocusWatchTierChanged(FocusWatchTier.Glance);
            yield return null;
            Assert.AreEqual(FocusWatchTier.Glance, renderer.CurrentTier,
                $"{LogPrefix} 1단계(곁눈질)로 올라가지 않았습니다.");
        }

        // ============================================================================
        // 헬퍼
        // ============================================================================

        private static float TorsoTilt(StickmanAgent agent)
        {
            Transform torso = FindDirectChild(agent.transform, "Torso");
            return torso == null ? 0f : Mathf.Abs(Mathf.DeltaAngle(0f, torso.localEulerAngles.z));
        }

        private static StickmanPoseAnimator Pose(StickmanAgent agent)
        {
            StickmanPoseAnimator pose = agent.Blackboard.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터가 없습니다.");
            return pose;
        }

        private static Transform FindLineByName(string rootName, string lineName)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == lineName) return t;
            }
            return null;
        }

        private static Transform FindChildStartingWith(string rootName, string prefix)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t != root.transform && t.name.StartsWith(prefix)) return t;
            }
            return null;
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
                $"{LogPrefix} 레벨 {level}까지 올리지 못했습니다.");
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
