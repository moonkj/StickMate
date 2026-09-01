using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 긴 망토를 밟고 넘어진다 — 2026-08-30, docs/UX_FLOW.md 33-2-5 (B), <b>리더 승인 완료</b>.
    ///
    /// ============================================================================
    /// 왜 만들었는가 — 원칙 1(행동-텍스트 싱크) / 그리고 왜 지금은 꺼져 있는가
    /// ============================================================================
    /// 만들 당시 카탈로그의 긴 망토 설명문은 <b>"가끔 밟고 넘어진다"</b>였다. 그 문장이 확정
    /// 콘텐츠인데 넘어지는 로직이 없으면, 그건 대사를 먼저 정하고 행동을 안 만든 것 = 원칙 1
    /// 위반이다. 그래서 리더가 <b>실제로 넘어지게 하는 쪽</b>을 승인했다.
    ///
    /// ★ 2026-08-31 — 사용자가 그 행동 자체를 없애 달라고 명시적으로 요청했다. 원칙 1은 양방향
    /// 이므로(행동이 없으면 그 행동을 약속하는 문구도 없어야 한다) 행동만 끄고 문구를 두면 같은
    /// 위반이 방향만 바꿔 남는다. 그래서 <b>둘을 함께</b> 처리했다 —
    /// 자율 발동을 0으로 내리고(<see cref="StickConfig.longCapeTripMeanSeconds"/>),
    /// 설명문을 행동을 약속하지 않는 순수 외형 서술
    /// <b>"발목까지 내려오는 긴 자락."</b>로 바꿨다
    /// (Resources/Items/equip_shoulders_long_cape.asset + 골든 스냅샷 동시 갱신).
    /// 그 문구는 실측으로 참이다 — Tests/PlayMode/CharacterAppearanceLayerTests가 긴 망토
    /// (TorsoLength × 2.10)가 발목 언저리까지 내려오는 것을 이미 잠그고 있다.
    ///
    /// ============================================================================
    /// ★★ 2026-08-31 — 기본 OFF다. 사용자 명시 요청으로 자율 발동이 꺼져 있다.
    /// ============================================================================
    /// 사용자 원문: <b>"그리고 걷다가 갑자기 아픈것처럼 쓰러지는데 이런건 없애줘"</b>.
    /// 이 디렉터가 바로 그 연출이었고, 추정이 아니라 사용자 실측 로그로 확인됐다:
    /// <code>
    /// [긴망토] 자락을 밟고 넘어졌습니다 — 충격량 8.16 (임계값 8.00의 최소 초과분)
    /// [말풍선] 표시 (Ragdoll) "윽...!"
    /// </code>
    /// 신고의 "아픈것처럼"은 저 <c>"윽...!"</c> 대사다(RagdollState가 충격 강도에서 파생시킨다).
    /// 사용자 저장 파일의 <c>ragdollFalls</c>는 4시간 18분 만에 48이었다.
    ///
    /// 그래서 <b>발동 주기를 <see cref="StickConfig.longCapeTripMeanSeconds"/>로 옮기고 기본값을
    /// 0(=발동 안 함)으로 내렸다</b>. 코드는 한 줄도 지우지 않았다 — 그 값을 양수로 올리면
    /// 아래 로직이 예전 그대로 되살아난다(원래 값 90초). 이 라운드가 요구한 것은 "기능 삭제"가
    /// 아니라 "요청하지 않은 연출이 스스로 뜨지 않는 것"이기 때문이다(2026-08-29 라운드가
    /// 다른 구경거리 연출 전부를 *Chance = 0으로 내린 것과 정확히 같은 처리).
    ///
    /// ★ 왜 2026-08-29의 "자율 발동 전부 OFF" 정리에서 이것만 살아남았는가(재발 방지 기록):
    /// 이 기능은 그 정리 <b>다음 날</b> 만들어졌고, 발동 주기를 StickConfig가 아니라 이 파일의
    /// private const에 숨겨 뒀다. "StickConfig의 *Chance 필드를 0으로"라는 그 라운드의 정리
    /// 방식이 구조적으로 닿을 수 없는 위치였다. 이번 이동의 진짜 목적이 그것이다 —
    /// <b>자율 발동 파라미터는 반드시 StickConfig에 노출한다.</b>
    ///
    /// ============================================================================
    /// 발동 조건 — 새 상태도, 새 이벤트도 만들지 않는다
    /// ============================================================================
    /// 긴 망토 착용 + Walk + 접지 중일 때 매 프레임
    /// <c>p = dt / longCapeTripMeanSeconds</c>(포아송 과정, 평균 N초에 1회).
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
    /// · <b>탈출구는 "긴 망토를 벗으면 즉시 멈춘다"</b>. 정보창 [장비] 탭에서 1클릭이다.
    ///   (이 탈출구는 기본 OFF가 된 지금 무의미하지만, 값을 되살렸을 때를 위해 그대로 둔다.
    ///   단 예전에 여기 적혀 있던 "설명문이 인과를 예고한다"는 근거는 더 이상 성립하지 않는다 —
    ///   그 설명문을 위에 적은 이유로 바꿨기 때문이다.)
    /// · 복제 방어: 같은 GameObject의 StickmanAgent가 없으면 아무것도 하지 않는다.
    /// </summary>
    public sealed class LongCapeTripDirector : MonoBehaviour
    {
        /// <summary>긴 망토의 아이템 자리(Core/ItemCatalog.cs BACK 표의 순서).</summary>
        private const int BackLongCape = AccessoryShapeBuilder.BackLongCape;

        /// <summary>평균 발동 간격(초)의 <b>폴백</b> — <see cref="StickmanAgent.Config"/>가 없을 때만 쓴다.
        /// ★ 0 = 발동 안 함. 예전에는 여기 90f가 박혀 있었고 그것이 StickConfig의 자율 발동 정리에서
        /// 이 기능만 빠진 원인이었다(클래스 문서 참고). 폴백까지 0으로 두는 이유는 "설정을 못 읽으면
        /// 조용히 있는다"가 이 프로젝트의 안전한 기본값이기 때문이다.</summary>
        private const float FallbackMeanIntervalSeconds = 0f;

        /// <summary>지금 쓰이는 평균 발동 간격(초). 0 이하면 발동하지 않는다.</summary>
        private float MeanIntervalSeconds
        {
            get
            {
                StickConfig config = _agent != null ? _agent.Config : null;
                return config != null ? config.longCapeTripMeanSeconds : FallbackMeanIntervalSeconds;
            }
        }

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
            // (평균간격 <= 0인 경우는 ResolveArmed()가 이미 걸렀으므로 여기서 0으로 나눌 일은 없다.)
            float p = Time.deltaTime / MeanIntervalSeconds;
            if (Random.value >= p) return;

            StickConfig config = _agent.Config;
            float threshold = config != null ? config.ragdollForceThreshold : 8f;
            // ★ 2026-09-01 (P9-b) 방향 = 걷던 쪽(정면). 자락을 밟으면 발이 멈추고 상체만 계속 나아가므로
            // 물리적으로도 앞으로 고꾸라지는 것이 옳다. 이 디렉터는 Walk일 때만 발동하므로(ResolveArmed)
            // FacingSign이 곧 진행 방향이다.
            StickmanBlackboard bb = _agent.Blackboard;
            float facing = bb != null ? bb.FacingSign : 1f;
            _agent.ReportExternalImpact(threshold * TripImpulseMultiplier, new Vector2(facing, 0f));
            TripCount++;

            Debug.Log($"[긴망토] 자락을 밟고 넘어졌습니다 — 충격량 {(threshold * TripImpulseMultiplier):F2} " +
                      $"(임계값 {threshold:F2}의 최소 초과분). 평균 {MeanIntervalSeconds:F0}초에 1회. " +
                      "멈추려면 정보창 [장비] 탭에서 긴 망토를 벗으면 됩니다.");
        }

        private bool ResolveArmed()
        {
            if (_agent == null) return false;

            // ★★ 2026-08-31 — 자율 발동 마스터 게이트. 기본값 0이므로 **여기서 끝난다**.
            // 맨 앞에 두는 이유는 두 가지다:
            //  (1) 기본 OFF에서 이 컴포넌트의 Update가 비교 한 번으로 끝나야 한다(24시간 상주 앱).
            //  (2) 0 이하일 때 아래 확률 계산이 0으로 나누는 것을 원천 차단한다
            //      (dt/0 = +Infinity라 Random.value >= p가 **항상 거짓** = 매 프레임 발동이 된다.
            //       즉 이 가드가 없으면 "0으로 껐다"가 정반대로 "무한히 자주"가 됐을 것이다).
            if (!(MeanIntervalSeconds > 0f)) return false;

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
