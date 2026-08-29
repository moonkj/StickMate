using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// Active Ragdoll(아키텍처 0절) 파츠 중 루트가 아닌 자식 파츠(팔/다리/머리 등)에 부착해, 그 파츠가
    /// 받은 충돌 충격을 StickmanAgent.ReportExternalImpact()로 전달하는 중계자.
    ///
    /// 왜 필요한가: StickmanAgent는 자기 자신의 OnCollisionEnter2D로 "루트" 파츠의 충돌만 직접 받을 수
    /// 있다. 실제 Active Ragdoll은 몸통/머리/양팔/양다리 등 여러 Rigidbody2D+Collider2D 파츠로
    /// 구성되므로(각 파츠가 독립적으로 충돌을 받음), 사지에 맞는 피격도 RAGDOLL 강제 전이의 단일
    /// 진입점(StickmanAgent.ReportExternalImpact)으로 모이려면 이 중계자가 필요하다.
    ///
    /// 실제 캐릭터 프리팹(몸통+머리+양팔+양다리 최소 구성)은 Phase 2 범위 밖(씬/프리팹 작업)이므로,
    /// 이 컴포넌트는 "스크립트만 준비"된 상태다 — 프리팹 제작 시 루트가 아닌 각 파츠 GameObject에
    /// 부착하고 Reset()이 자동으로 부모 계층에서 StickmanAgent를 찾아 연결한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RagdollLimbImpactRelay : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _agent;

        private Rigidbody2D _body;

        private void Reset()
        {
            _agent = GetComponentInParent<StickmanAgent>();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (_agent == null) _agent = GetComponentInParent<StickmanAgent>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_agent == null || _body == null) return;
            // 루트(StickmanAgent.OnCollisionEnter2D)와 **같은** 진입점을 쓴다 — 착지 접촉을 외력에서
            // 걸러내는 예외가 파츠마다 어긋나면 안 된다(2026-08-29 "무릎앉아 착지" 라운드).
            _agent.ReportCollisionImpact(collision, collision.relativeVelocity.magnitude * _body.mass);
        }
    }
}
