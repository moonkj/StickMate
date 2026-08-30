using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 긴 망토를 밟고 넘어진다 — 2026-08-30, docs/UX_FLOW.md 33-2-5 (B), <b>리더 승인 완료</b>.
    ///
    /// ============================================================================
    /// 왜 만들었는가 — 원칙 1(행동-텍스트 싱크)
    /// ============================================================================
    /// 카탈로그의 긴 망토 설명문은 <b>"가끔 밟고 넘어진다"</b>다. 그 문장이 확정 콘텐츠인데 넘어지는
    /// 로직이 없으면, 그건 대사를 먼저 정하고 행동을 안 만든 것 = 원칙 1 위반이다. 문구를 고치는 쪽은
    /// "확정 콘텐츠를 바꾸지 않는다"는 전제를 깨므로 리더가 <b>실제로 넘어지게 하는 쪽</b>을 승인했다.
    ///
    /// ============================================================================
    /// 발동 조건 — 새 상태도, 새 이벤트도 만들지 않는다
    /// ============================================================================
    /// 긴 망토 착용 + Walk + 접지 중일 때 매 프레임 <c>p = dt / 90초</c>(포아송 과정, 평균 90초에 1회).
    /// (접지 검사는 2026-08-30 R3-m3에서 실제로 추가됐다 — 그 전까지 이 문장은 구현보다 앞서 있었다.)
    /// 성공하면 기존 경로 <see cref="StickmanAgent.ReportExternalImpact"/> 하나만 부른다 —
    /// RAGDOLL -> GETUP 복귀는 이미 있는 그대로다.
    ///
    /// ============================================================================
    /// ★ 세기: 33절의 "최약 피격의 0.6배"를 그대로 쓸 수 없다 (실측 근거 — 리더 보고 대상)
    /// ============================================================================
    /// <see cref="RagdollImpactResolver.TryApplyImpact"/>는 <c>impulse &lt; ragdollForceThreshold</c>면
    /// <b>아무 일도 하지 않는다</b>. 지금 가장 약한 기존 피격은 로데오 흔들기이고 그것도
    /// <c>threshold × 1.25</c>다. 그 0.6배 = <c>threshold × 0.75</c> < threshold이므로
    /// <b>넘어지는 일이 영원히 일어나지 않는다</b>(예외도 로그도 없이 조용히). 그래서 "가능한 가장 약한
    /// 값"인 threshold 바로 위(<see cref="TripImpulseMultiplier"/> = 1.02)를 쓴다 —
    /// 33절이 의도한 "아프지 않게 보이도록"의 실제 하한이다.
    ///
    /// ============================================================================
    /// 상호배제와 탈출구
    /// ============================================================================
    /// · <see cref="SpectacleEventLock"/>이 잠겨 있으면 발동하지 않는다. 활쏘기/격파/대결 중에 망토를
    ///   밟고 자빠지면 그 연출이 통째로 깨진다(33절 명시 요구).
    /// · 이 컴포넌트는 락을 <b>걸지 않는다</b>. RAGDOLL은 순간 전이이고 그 뒤는 기존 복귀 경로가
    ///   전부 처리한다 — 락을 걸면 해제 주체를 또 만들어야 하고, 그건 이 프로젝트가 이미 12곳에서
    ///   반복해 온 해제 보일러플레이트를 13번째로 늘리는 일이다.
    /// · <b>탈출구는 "긴 망토를 벗으면 즉시 멈춘다"</b>. 정보창 [장비] 탭에서 1클릭이고,
    ///   설명문이 인과("가끔 밟고 넘어진다")를 이미 예고하고 있어 원인 추적도 가능하다.
    /// · 복제 방어: 같은 GameObject의 StickmanAgent가 없으면 아무것도 하지 않는다.
    /// </summary>
    public sealed class LongCapeTripDirector : MonoBehaviour
    {
        /// <summary>긴 망토의 아이템 자리(Core/ItemCatalog.cs BACK 표의 순서).</summary>
        private const int BackLongCape = AccessoryShapeBuilder.BackLongCape;

        /// <summary>평균 발동 간격(초). 포아송 과정이라 "정확히 90초마다"가 아니라 "평균 90초에 한 번"이다.</summary>
        private const float MeanIntervalSeconds = 90f;

        /// <summary>ragdollForceThreshold 배수. 클래스 문서의 "0.6배를 쓸 수 없는 이유" 참고.</summary>
        private const float TripImpulseMultiplier = 1.02f;

        private StickmanAgent _agent;

        /// <summary>테스트/진단용 — 지금까지 몇 번 걸려 넘어졌는가.</summary>
        public int TripCount { get; private set; }

        /// <summary>테스트/진단용 — 지금 발동 조건이 전부 충족돼 있는가(확률만 남은 상태인가).</summary>
        public bool IsArmed => ResolveArmed();

        private void Awake() => _agent = GetComponent<StickmanAgent>();

        private void Update()
        {
            if (!ResolveArmed()) return;

            // 포아송: 매 프레임 p = dt / 평균간격. 프레임레이트가 달라져도 기대 빈도가 같다.
            float p = Time.deltaTime / MeanIntervalSeconds;
            if (Random.value >= p) return;

            StickConfig config = _agent.Config;
            float threshold = config != null ? config.ragdollForceThreshold : 8f;
            _agent.ReportExternalImpact(threshold * TripImpulseMultiplier);
            TripCount++;

            Debug.Log($"[긴망토] 자락을 밟고 넘어졌습니다 — 충격량 {(threshold * TripImpulseMultiplier):F2} " +
                      $"(임계값 {threshold:F2}의 최소 초과분). 평균 {MeanIntervalSeconds:F0}초에 1회. " +
                      "멈추려면 정보창 [장비] 탭에서 긴 망토를 벗으면 됩니다.");
        }

        private bool ResolveArmed()
        {
            if (_agent == null) return false;

            // 전체화면 감지 중에는 ReportExternalImpact가 조용히 무시된다 — 여기서 먼저 끊지 않으면
            // 임펄스는 안 걸리는데 "넘어졌습니다" 로그와 TripCount만 늘어난다(원칙 1의 로그 버전).
            if (_agent.IsSuspended) return false;

            // 긴 망토를 걸치고 있고 지금 레벨에서 보유 중일 때만.
            if (EquipmentModel.WornIndex(EquipmentSlot.Shoulders) != BackLongCape) return false;
            if (!EquipmentModel.IsUnlocked(EquipmentSlot.Shoulders)) return false;

            // 다른 순수 연출/스펙터클과 겹치지 않는다.
            if (SpectacleEventLock.IsActive) return false;

            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Machine == null) return false;
            // 걷는 중. 서 있을 때 자빠지면 "밟고 넘어진다"로 읽히지 않는다.
            if (bb.Machine.CurrentStateId != StickmanStateId.Walk) return false;

            // ★ 2026-08-30 R3-m3 — 접지 검사. 클래스 문서가 처음부터 "Walk + **접지 중**"이라고
            // 적고 있었는데 실제 코드에는 없었다(문서-구현 불일치). 문서 쪽을 지우지 않고 검사를
            // 넣은 이유: Walk는 접지를 보장하지 않는다 — 발이 땅에서 떨어져도 fallGraceDuration
            // (0.1초) 동안은 Walk가 유지된다(States/StickmanBlackboard.GroundedTick). 그 공중 구간에
            // 걸리면 "자락을 밟고" 넘어졌다는 로그가 사실이 아니게 된다(원칙 1의 로그 버전이며,
            // 바로 위 IsSuspended 가드를 넣은 것과 같은 이유다).
            // 이 조회를 **맨 마지막**에 두는 이유: 발판 목록을 훑는 유일한 비-상수 시간 검사라,
            // 긴 망토를 걸치고 걷는 동안에만 돌게 한다(24시간 상주 앱의 매 프레임 경로).
            return bb.SenseGround().Grounded;
        }
    }
}
