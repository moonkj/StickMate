using UnityEngine;

namespace StickMate.States
{
    /// <summary>
    /// 머리 안의 눈동자 점 2개(프리팹 계층의 "LeftEye"/"RightEye")를 중립 위치에서 오프셋해 "어디를
    /// 보고 있는지"를 표현하는 최소 컨트롤러.
    ///
    /// 구조(2026-08-28 사용자 요청 "나중에 마우스 위치에 따라 눈도 움직여야 해서 눈도 있어야 하고"):
    /// 흰자 배경 없이 눈동자 점만 두고, 그 점의 <c>transform.localPosition</c>을 중립에서 조금씩
    /// 옮기면 시선이 움직이는 것처럼 보인다. 두 눈은 반드시 **머리 GameObject의 자식**이라
    /// (SceneBootstrapper가 그렇게 만든다) RAGDOLL로 머리가 뒹굴 때도 머리를 따라 함께 회전·이동한다.
    ///
    /// ── 다음 라운드 배선 지점(커서 추적) ──────────────────────────────────────────────────────
    /// 이번 라운드 범위는 "구조와 진입점만 만들고 항상 정면(중립)을 보게 한다"이다(Architect 지시).
    /// 실제 마우스 추적은 이미 존재하는 전역 커서 좌표 경로를 이 클래스의 <see cref="SetLookDirection"/>에
    /// 연결하기만 하면 된다 — 새로 만들 배관은 없다:
    ///   1. <c>StickmanBlackboard.TryGetCursorWorldPosition(out Vector2 world)</c>
    ///      (내부적으로 Core/StickmanAgent.TryGetCursorPosition -> Platform/ICursorPositionService,
    ///       Phase 1/3에서 이미 구현·검증된 경로. 클릭 관통과 완전히 독립이다 — UX_FLOW.md 9절-3.)
    ///   2. 머리 월드 좌표(Head transform)에서 커서 월드 좌표로 향하는 벡터를 구해 정규화한다.
    ///   3. 그 벡터를 SetLookDirection에 넘긴다(길이 1을 넘으면 여기서 자동으로 clamp된다).
    /// 호출 위치는 StickmanBlackboard.TickPose()의 마지막 줄(지금 LookForward()를 부르는 자리)이
    /// 자연스럽다 — 매 프레임 1회, 상태와 무관하게 항상 실행되는 지점이기 때문이다.
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    ///
    /// RagdollRig/StickmanPoseAnimator와 같은 컨벤션을 따라 MonoBehaviour가 아닌 순수 C# 클래스이며,
    /// StickmanBlackboard.GetEyeController()가 최초 1회만 생성해 캐싱한다(매 프레임 재탐색 금지).
    /// </summary>
    public sealed class EyeController
    {
        /// <summary>
        /// 눈동자가 중립에서 벗어날 수 있는 최대 거리(월드 유닛). 머리 링 반경 0.22, 눈 중립 X ±0.075,
        /// 눈동자 반경 0.018 기준으로 0.05까지 밀어도 바깥 끝이 0.075+0.05+0.018 = 0.143 &lt; 0.22라
        /// 어떤 방향으로도 링 밖으로 튀어나가지 않는다(대각선 방향도 원 안이므로 동일하게 안전).
        /// </summary>
        private const float MaxPupilOffset = 0.05f;

        private readonly Transform _leftEye;
        private readonly Transform _rightEye;
        private readonly Vector3 _leftNeutral;
        private readonly Vector3 _rightNeutral;

        // 바라보는 방향(+1 오른쪽 / -1 왼쪽) — StickmanPoseAnimator와 같은 부호 규약. 눈 중립 X를 함께
        // 뒤집어 "보고 있는 방향"이 몸의 방향과 어긋나지 않게 한다(2026-08-28 "이상하게 뒤로 걸어" 대응).
        private float _facingSign = 1f;
        private Vector2 _lookDirection;

        /// <summary>두 눈을 모두 찾았는지. 프리팹이 구버전이어도 조용히 무시되도록 호출부에서 쓰지 않아도
        /// 안전하지만(모든 메서드가 null 가드), 진단 목적으로 노출한다.</summary>
        public bool HasEyes => _leftEye != null && _rightEye != null;

        public EyeController(Transform root)
        {
            if (root == null) return;

            // 이름으로 찾는다 — StickmanPoseAnimator가 팔다리를 찾는 방식과 동일한 컨벤션(계층 순회
            // 순서에는 좌우 의미가 없으므로 이름이 유일하게 신뢰할 수 있는 식별자다).
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].name == "LeftEye") _leftEye = all[i];
                else if (all[i].name == "RightEye") _rightEye = all[i];
            }

            if (_leftEye != null) _leftNeutral = _leftEye.localPosition;
            if (_rightEye != null) _rightNeutral = _rightEye.localPosition;
        }

        /// <summary>
        /// 시선 방향 설정. normalizedDir은 머리 기준 방향 벡터(길이 0~1)이며, 길이가 1을 넘으면
        /// clamp된다 — 호출부가 정규화를 잊어도 눈동자가 머리 밖으로 튀어나가지 않는다.
        /// 두 눈에 같은 오프셋을 준다(각 눈이 서로 다른 각도로 사시처럼 보이지 않게).
        /// </summary>
        public void SetLookDirection(Vector2 normalizedDir)
        {
            Vector2 dir = normalizedDir;
            float magnitude = dir.magnitude;
            if (magnitude > 1f) dir /= magnitude;
            _lookDirection = dir;
            Apply();
        }

        /// <summary>바라보는 방향 설정(+1 오른쪽 / -1 왼쪽). 눈 중립 위치의 X가 함께 미러링된다.</summary>
        public void SetFacing(float sign)
        {
            float next = sign >= 0f ? 1f : -1f;
            if (Mathf.Approximately(next, _facingSign)) return;
            _facingSign = next;
            Apply();
        }

        private void Apply()
        {
            var offset = new Vector3(_lookDirection.x * MaxPupilOffset, _lookDirection.y * MaxPupilOffset, 0f);
            if (_leftEye != null) _leftEye.localPosition = Mirror(_leftNeutral) + offset;
            if (_rightEye != null) _rightEye.localPosition = Mirror(_rightNeutral) + offset;
        }

        private Vector3 Mirror(Vector3 neutral)
        {
            return new Vector3(neutral.x * _facingSign, neutral.y, neutral.z);
        }

        /// <summary>정면(중립)을 보게 한다 — 이번 라운드의 유일한 호출 형태.</summary>
        public void LookForward() => SetLookDirection(Vector2.zero);
    }
}
