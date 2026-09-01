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
    /// ★★ 2026-09-01 (디버거) — <b>착지 프레임의 망토 순간이동</b> 회귀 잠금.
    ///
    /// ============================================================================
    /// 결함 (실측으로 확정, 추측 아님)
    /// ============================================================================
    /// 기류 펄럭임(2026-08-31 신설)의 세기는 <c>CharacterAccessoryRenderer.ResolveAirFlow01()</c>이
    /// <b>상태로 게이트</b>해서 만든다 — Fall/Jump/ThrowTumble일 때만 0이 아니다. 그래서 <b>발이 땅에
    /// 닿는 그 한 프레임</b>에 세기가 1에서 0으로 떨어지고, <c>TickHemMotion</c>이 곧바로
    /// <c>RestoreHemBase()</c>로 밑단을 구워진 원본에 <b>순간이동</b>시켰다.
    ///
    /// 그 순간이동의 크기는 도형 상수에서 바로 나온다:
    /// <c>(HemAirPushRatio 0.85 + HemAirRippleRatio 0.34) × R = 1.19 R</c>.
    /// 출하 기본 배율(R = 0.22)에서 <b>한 프레임에 0.26유닛</b>, 획 두께(0.036유닛)의 <b>7배</b>다.
    /// 사용자가 요청한 "떨어질 때 펄럭이는 망토"가 가장 눈에 띄는 순간에 딸깍 끊겼다.
    ///
    /// ============================================================================
    /// 왜 이 테스트가 필요한가 (순수 함수 테스트로는 절대 안 잡힌다)
    /// ============================================================================
    /// <c>HemAirOffset</c>은 "세기 0 -> 오프셋 0"을 정확히 지킨다(Tests/EditMode/CapeAirFlutterTests).
    /// 결함은 <b>그 세기가 한 프레임에 0이 된다</b>는 데 있었으므로, 식이 아니라 <b>시간에 따른 세기의
    /// 궤적</b>을 봐야만 보인다. 그래서 상태를 실제로 공중 -> 지상으로 넘기고 그 <b>이음매의 프레임
    /// 간 변위</b>를 잰다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    ///  · <see cref="NegativeControl_공중에서는_실제로_크게_젖혀져_있다"/> — 이음매를 재기 전에
    ///    "젖혀질 것이 실제로 있었다"를 먼저 보인다. 이게 없으면 아래 단언은 "원래 0이었다"로도
    ///    통과해 버려 아무 결함도 잡지 못한다.
    ///  · <see cref="착지_뒤_천은_유한_시간에_정확히_원본으로_돌아온다"/> — 잦아듦이 <b>지수감쇠가
    ///    아니라</b> 유한 시간에 정확히 0에 닿는다는 성질(24시간 상주 앱의 계산 스킵 + "가만히 서
    ///    있으면 정적"이 둘 다 이 성질에 걸려 있다).
    /// </summary>
    public sealed class CapeLandingSettleTests
    {
        private const string LogPrefix = "[착지여파]";
        private const int Cape = 0;         // 짧은망토(Shoulders 0번)
        private const int HemStart = 2;     // CapeOutline의 밑단 시작 인덱스
        private const int HemEnd = 6;       // 〃 끝 인덱스(포함)

        private StickmanAgent _agent;
        private CharacterAccessoryRenderer _renderer;
        private LineRenderer _capeLine;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ====================================================================
        // 네거티브 컨트롤 — 젖혀질 것이 실제로 있다
        // ====================================================================

        [UnityTest]
        public IEnumerator NegativeControl_공중에서는_실제로_크게_젖혀져_있다()
        {
            yield return LoadSceneAndPinIdle();

            Vector3[] rest = HemPoints();
            yield return HoldFallFrames(24);
            float displaced = MaxDistance(rest, HemPoints());

            Debug.Log($"{LogPrefix} [네거티브] 전속력 낙하 중 밑단 변위 = {displaced:F5}유닛 " +
                $"(획 {_renderer.StrokeWidth:F5}유닛의 {displaced / _renderer.StrokeWidth:P0}).");

            Assert.Greater(displaced, _renderer.StrokeWidth * 1.5f,
                $"{LogPrefix} 낙하 중에도 밑단이 {displaced:F5}유닛밖에 안 젖혀졌습니다 — 그렇다면 아래 " +
                "'착지 프레임에 튀지 않는다'는 단언은 원래부터 참이라 아무 결함도 잡지 못합니다.");
        }

        // ====================================================================
        // 본 단언 — 이음매에서 튀지 않는다
        // ====================================================================

        /// <summary>
        /// 공중(전속력) -> 지상(Idle) 전이의 <b>프레임 간</b> 밑단 변위를 잰다.
        /// 고치기 전에는 이 값이 곧 <c>1.19 R</c>(= 획의 약 7배)이었다.
        /// </summary>
        [UnityTest]
        public IEnumerator 착지_프레임에_망토가_순간이동하지_않는다()
        {
            yield return LoadSceneAndPinIdle();

            yield return HoldFallFrames(24);                 // 천이 충분히 젖혀진 상태를 만든다.
            Vector3[] beforeTouchdown = HemPoints();

            // 발이 닿는 프레임 — 상태만 지상으로 넘기고, 다음 LateUpdate가 그린 결과를 본다.
            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            _agent.Blackboard.Body.linearVelocity = Vector2.zero;
            yield return null;

            float jump = MaxDistance(beforeTouchdown, HemPoints());
            float stroke = _renderer.StrokeWidth;

            Debug.Log($"{LogPrefix} 착지 한 프레임의 밑단 변위 = {jump:F5}유닛 (획 {stroke:F5}유닛의 " +
                $"{jump / stroke:P0}). 고치기 전 이 값은 (0.85+0.34)·R 이었다.");

            Assert.Less(jump, stroke,
                $"{LogPrefix} 발이 닿는 한 프레임에 밑단이 {jump:F5}유닛(획의 {jump / stroke:P0}) " +
                "순간이동했습니다 — 화면에서 망토가 딸깍 끊깁니다. 기류 세기가 상태 게이트를 따라 " +
                "한 프레임에 0으로 떨어지고 있지 않은지(CharacterAccessoryRenderer.TickAirFlowInertia) " +
                "확인하세요.");
        }

        // ====================================================================
        // 잦아듦의 성질 — 유한 시간에 정확히 0
        // ====================================================================

        [UnityTest]
        public IEnumerator 착지_뒤_천은_유한_시간에_정확히_원본으로_돌아온다()
        {
            yield return LoadSceneAndPinIdle();

            Vector3[] rest = HemPoints();
            yield return HoldFallFrames(24);

            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            _agent.Blackboard.Body.linearVelocity = Vector2.zero;

            // 잦아듦은 "천이 한 번 흔들리는 시간"(0.62초)에 걸쳐 일어난다. 넉넉히 그 두 배를 본다.
            float deadline = Time.time + 1.4f;
            bool sawResidual = false;
            float settledAt = -1f;
            float start = Time.time;
            while (Time.time < deadline)
            {
                yield return null;
                if (_agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Idle)
                {
                    // 자율 배회가 걷기로 넘어가면 이 관측은 의미가 없다 — 다시 Idle로 못 박는다.
                    _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                    _agent.Blackboard.Body.linearVelocity = Vector2.zero;
                    continue;
                }
                float d = MaxDistance(rest, HemPoints());
                if (d > 1e-4f) { sawResidual = true; continue; }
                if (sawResidual && settledAt < 0f) settledAt = Time.time - start;
            }

            Debug.Log($"{LogPrefix} 착지 뒤 잔여 운동이 관측됐는가 = {sawResidual}, " +
                $"정확히 0이 된 시점 = {settledAt:F3}초.");

            Assert.IsTrue(sawResidual,
                $"{LogPrefix} 착지 직후 잔여 운동이 <b>한 프레임도</b> 없었습니다 — 관성이 걸리지 " +
                "않았다는 뜻이고, 그러면 위 '순간이동하지 않는다'도 우연히 통과한 것입니다.");
            Assert.Greater(settledAt, 0f,
                $"{LogPrefix} 잔여 운동이 1.4초 안에 <b>정확히 0</b>이 되지 않았습니다 — 지수감쇠는 " +
                "영원히 0에 닿지 않아 24시간 상주 앱이 영구히 매 프레임 메시를 다시 씁니다.");
        }

        // ==================== 유틸 ====================

        private static float MaxDistance(Vector3[] a, Vector3[] b)
        {
            float max = 0f;
            int n = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i]));
            return max;
        }

        /// <summary>액세서리 컨테이너는 서명이 바뀌면 통째로 다시 구워지므로 매번 다시 찾는다.</summary>
        private Vector3[] HemPoints()
        {
            if (_capeLine == null)
            {
                foreach (var lr in _renderer.GetComponentsInChildren<LineRenderer>(true))
                    if (lr.name == "CapeOutline") _capeLine = lr;
                Assert.IsNotNull(_capeLine, $"{LogPrefix} CapeOutline 선을 찾지 못했습니다.");
            }
            var all = new Vector3[_capeLine.positionCount];
            _capeLine.GetPositions(all);
            var hem = new Vector3[HemEnd - HemStart + 1];
            for (int i = 0; i < hem.Length; i++) hem[i] = all[HemStart + i];
            return hem;
        }

        /// <summary>매 프레임 재개 시점(Update 뒤, LateUpdate 앞)에 공중 상태와 속도를 다시 못 박는다 —
        /// 렌더러는 LateUpdate에서 읽으므로 같은 프레임에 그 값을 본다.
        /// (CapeFallFlutterTests가 쓰는 것과 같은 방식이다.)</summary>
        private IEnumerator HoldFallFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                StickmanBlackboard bb = _agent.Blackboard;
                if (bb.Machine.CurrentStateId != StickmanStateId.Fall)
                    bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);
                bb.Body.linearVelocity = new Vector2(0f, -20f);
            }
            yield return null;   // 마지막으로 못 박은 값이 반영된 프레임을 하나 더 지난다.
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            _renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Assert.IsNotNull(_renderer, $"{LogPrefix} CharacterAccessoryRenderer가 없습니다.");
            _capeLine = null;
            _agent.Blackboard.IntentSource = new StillIntentSource();

            StickConfig config = _agent.Config;
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < 4; guard++)
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
            EquipmentModel.TryWear(EquipmentSlot.Shoulders, Cape, config);
            Assert.AreEqual(Cape, EquipmentModel.WornIndex(EquipmentSlot.Shoulders),
                $"{LogPrefix} 짧은망토를 걸치지 못했습니다.");

            float deadline = Time.realtimeSinceStartup + 15f;
            float idleSince = -1f;
            StickmanStateId last = _agent.Blackboard.Machine.CurrentStateId;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                last = _agent.Blackboard.Machine.CurrentStateId;
                if (last != StickmanStateId.Idle) { idleSince = -1f; continue; }
                if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - idleSince >= 0.5f) break;
            }
            Assert.AreEqual(StickmanStateId.Idle, last, $"{LogPrefix} Idle로 안정되지 않았습니다.");

            for (int i = 0; i < 8; i++) yield return null;   // 액세서리 재구성 완료 대기.
        }

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }
    }
}
