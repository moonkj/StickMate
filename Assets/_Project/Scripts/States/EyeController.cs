using UnityEngine;

namespace StickMate.States
{
    /// <summary>
    /// 머리 안의 눈동자 점 2개(프리팹 계층의 "LeftEye"/"RightEye")를 중립 위치에서 오프셋해 "어디를
    /// 보고 있는지"를 표현하는 컨트롤러.
    ///
    /// 구조(2026-08-28 사용자 요청 "나중에 마우스 위치에 따라 눈도 움직여야 해서 눈도 있어야 하고"):
    /// 흰자 배경 없이 눈동자 점만 두고, 그 점의 <c>transform.localPosition</c>을 중립에서 조금씩
    /// 옮기면 시선이 움직이는 것처럼 보인다. 두 눈은 반드시 **머리 GameObject의 자식**이라
    /// (SceneBootstrapper가 그렇게 만든다) RAGDOLL로 머리가 뒹굴 때도 머리를 따라 함께 회전·이동한다.
    ///
    /// ── 커서 추적(2026-08-28 이번 라운드에 배선 완료) ──────────────────────────────────────────
    /// <see cref="TickLookAt"/>가 매 프레임 호출되는 유일한 진입점이다(StickmanBlackboard.TickPose()의
    /// 마지막 줄 — 상태와 무관하게 항상 실행되므로 RAGDOLL/드래그 중에도 눈은 계속 커서를 본다).
    /// 커서 월드 좌표는 이미 존재하던 채널을 그대로 재사용한다(새 배관 없음):
    ///   StickmanBlackboard.TryGetCursorWorldPosition -> Core/StickmanAgent.TryGetCursorPosition
    ///   -> Platform/ICursorPositionService(macOS: CGEventGetLocation). 클릭 관통과 완전히 독립이다.
    ///
    /// 세 가지 규칙(리더 지시):
    ///   (1) **링 밖으로 나가지 않는다** — 아래 <see cref="MaxSafePupilOffset"/>의 기하학적 유도 참고.
    ///       설정값이 아무리 커도 그 상한으로 clamp되므로 프리팹 수치를 모르는 호출부도 안전하다.
    ///   (2) **부드럽게 따라간다** — 이 프로젝트가 이미 쓰는 프레임레이트 독립 지수 감쇠
    ///       (<c>1 - Mathf.Exp(-k*dt)</c>, StickmanPoseAnimator와 동일한 패턴)로 보간한다.
    ///   (3) **가까우면 중립, 멀면 최대치에서 멈춤** — 머리~커서 거리를 [NeutralRadius, FullRangeRadius]
    ///       구간에서 0~1로 정규화한다. 커서가 캐릭터와 겹치면(반경 안) 정면(중립), 아주 멀면 1로
    ///       포화되어 더 이상 커지지 않는다.
    ///
    /// ★ 방향을 **머리 로컬 공간으로 변환**하는 이유: 눈은 머리의 자식이라 localPosition 오프셋도
    /// 머리와 함께 회전한다. 월드 방향을 그대로 대입하면 RAGDOLL로 머리가 90도 뒤집혔을 때 눈이
    /// 엉뚱한 쪽을 본다. Transform.InverseTransformDirection으로 한 번 변환해두면 머리가 어떤 각도로
    /// 뒹굴어도 눈동자는 화면상 커서 쪽을 계속 향한다(리더 지시 "RAGDOLL 중에도 눈은 머리를 따라가야
    /// 한다"를 시각적으로 올바르게 만족시키는 형태).
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    ///
    /// RagdollRig/StickmanPoseAnimator와 같은 컨벤션을 따라 MonoBehaviour가 아닌 순수 C# 클래스이며,
    /// StickmanBlackboard.GetEyeController()가 최초 1회만 생성해 캐싱한다(매 프레임 재탐색 금지).
    /// </summary>
    public sealed class EyeController
    {
        /// <summary>
        /// 눈동자가 중립에서 벗어날 수 있는 **기하학적 상한**(월드 유닛). Editor/SceneBootstrapper.cs가
        /// 굽는 실제 프리팹 수치에서 유도한 값이라, 이 값을 넘기면 눈동자가 머리 링을 뚫고 나간다:
        ///   머리 링 반경           HeadVisualRadius  = 0.22
        ///   링 선 두께             HeadOutlineWidth  = 0.09 * 0.7 = 0.063  -> 링 **안쪽 가장자리** = 0.22 - 0.063/2 = 0.1885
        ///   눈 중립 위치           (±0.075, +0.02)   -> 머리 중심에서 sqrt(0.075^2 + 0.02^2) = 0.0776
        ///   눈동자 반경            EyePupilRadius    = 0.018
        /// 최악(중립 방향과 오프셋 방향이 일직선으로 겹치는 경우)에도 링 안에 남으려면
        ///   0.0776 + offset + 0.018 &lt;= 0.1885  ->  offset &lt;= 0.0929
        /// 이므로 0.09를 상한으로 둔다. StickConfig의 튜닝 값은 항상 이 상한으로 clamp된다.
        /// </summary>
        public const float MaxSafePupilOffset = 0.09f;

        /// <summary>StickConfig가 배선되지 않은 경로(테스트/폴백)에서 쓰는 기본 최대 오프셋.</summary>
        public const float DefaultMaxPupilOffset = 0.05f;

        /// <summary>커서 추적 파라미터 묶음(readonly struct — 매 프레임 경로라 힙 할당이 없다).
        /// StickmanBlackboard.BuildEyeTrackingSettings()가 StickConfig에서 만들어 넘긴다.</summary>
        public readonly struct EyeTrackingSettings
        {
            public readonly bool Enabled;
            public readonly float MaxPupilOffset;
            public readonly float FollowRate;
            public readonly float NeutralRadius;
            public readonly float FullRangeRadius;

            public EyeTrackingSettings(bool enabled, float maxPupilOffset, float followRate,
                float neutralRadius, float fullRangeRadius)
            {
                Enabled = enabled;
                // 설정값이 무엇이든 기하학적 상한을 넘지 못한다(클래스 문서 (1)).
                MaxPupilOffset = Mathf.Clamp(maxPupilOffset, 0f, MaxSafePupilOffset);
                FollowRate = Mathf.Max(0f, followRate);
                NeutralRadius = Mathf.Max(0f, neutralRadius);
                // 반드시 NeutralRadius보다 커야 정규화 구간이 성립한다(0으로 나누기 방지).
                FullRangeRadius = Mathf.Max(NeutralRadius + 0.01f, fullRangeRadius);
            }

            public static EyeTrackingSettings Default =>
                new EyeTrackingSettings(true, DefaultMaxPupilOffset, 12f, 0.6f, 4f);
        }

        private readonly Transform _leftEye;
        private readonly Transform _rightEye;
        private readonly Transform _head; // 두 눈의 부모(= 프리팹의 "Head" 앵커). 월드->로컬 방향 변환 기준.
        private readonly Vector3 _leftNeutral;
        private readonly Vector3 _rightNeutral;

        // 바라보는 방향(+1 오른쪽 / -1 왼쪽) — StickmanPoseAnimator와 같은 부호 규약. 눈 중립 X를 함께
        // 뒤집어 "보고 있는 방향"이 몸의 방향과 어긋나지 않게 한다(2026-08-28 "이상하게 뒤로 걸어" 대응).
        private float _facingSign = 1f;

        // 현재 적용 중인(스무딩된) 시선 벡터 — **머리 로컬 공간**, 길이 0~1. 여기에 MaxPupilOffset을
        // 곱한 값이 실제 localPosition 오프셋이 된다.
        private Vector2 _lookDirection;
        private float _appliedMaxOffset = DefaultMaxPupilOffset;

        /// <summary>두 눈을 모두 찾았는지. 프리팹이 구버전이어도 조용히 무시되도록 호출부에서 쓰지 않아도
        /// 안전하지만(모든 메서드가 null 가드), 진단 목적으로 노출한다.</summary>
        public bool HasEyes => _leftEye != null && _rightEye != null;

        /// <summary>지금 적용된 눈동자 오프셋(월드 유닛, 머리 로컬 방향 기준). 진단 로그 전용 —
        /// 실제 눈 움직임은 사용자만 볼 수 있으므로 리더/에이전트가 로그로 검증할 수 있게 노출한다.</summary>
        public Vector2 CurrentPupilOffset => _lookDirection * _appliedMaxOffset;

        /// <summary>지금 적용된 시선 벡터(정규화, 머리 로컬 공간). 진단 로그 전용.</summary>
        public Vector2 CurrentLookDirection => _lookDirection;

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

            // 머리 기준 Transform은 "눈의 부모"로 정의한다 — 이름("Head")으로 다시 찾지 않는 이유는,
            // 눈이 실제로 매달린 그 Transform이야말로 localPosition 오프셋이 해석되는 좌표계 그 자체라
            // 정의상 항상 옳기 때문이다(프리팹 계층이 바뀌어도 자동으로 따라간다).
            if (_leftEye != null) _head = _leftEye.parent;
            else if (_rightEye != null) _head = _rightEye.parent;
        }

        /// <summary>
        /// ★ 매 프레임 유일한 진입점 — 커서(월드 좌표)를 향해 눈동자를 부드럽게 이동시킨다.
        /// </summary>
        /// <param name="hasTarget">커서 좌표 조회에 성공했는지. false면 중립으로 부드럽게 되돌아간다.</param>
        /// <param name="targetWorld">커서의 Unity 월드 좌표.</param>
        /// <param name="deltaTime">프레임 시간(지수 감쇠에 사용 — 프레임레이트 독립).</param>
        /// <param name="settings">StickConfig에서 만든 추적 파라미터.</param>
        public void TickLookAt(bool hasTarget, Vector2 targetWorld, float deltaTime, in EyeTrackingSettings settings)
        {
            _appliedMaxOffset = settings.MaxPupilOffset;
            Vector2 desired = Vector2.zero;

            if (settings.Enabled && hasTarget && _head != null)
            {
                Vector2 headWorld = _head.position;
                Vector2 delta = targetWorld - headWorld;
                float distance = delta.magnitude;

                // (3-a) 커서가 캐릭터와 겹치면 중립(정면). 방향 자체가 의미 없는 구간이라 흔들림 방지도 겸한다.
                if (distance > settings.NeutralRadius)
                {
                    // (3-b) 멀수록 1로 수렴하고 FullRangeRadius 이상에서는 포화(=최대 오프셋에서 멈춤).
                    float amount = Mathf.Clamp01(
                        (distance - settings.NeutralRadius) / (settings.FullRangeRadius - settings.NeutralRadius));

                    // 월드 방향 -> 머리 로컬 방향(클래스 문서 ★ 절 참고).
                    Vector3 local = _head.InverseTransformDirection(new Vector3(delta.x / distance, delta.y / distance, 0f));
                    var localDir = new Vector2(local.x, local.y);
                    float localMag = localDir.magnitude;
                    if (localMag > 0.0001f) desired = (localDir / localMag) * amount;
                }
            }

            // (2) 프레임레이트 독립 지수 감쇠 — 이 프로젝트의 기존 스무딩 패턴과 동일.
            float t = deltaTime > 0f ? 1f - Mathf.Exp(-settings.FollowRate * deltaTime) : 1f;
            _lookDirection = Vector2.Lerp(_lookDirection, desired, Mathf.Clamp01(t));
            Apply();
        }

        /// <summary>
        /// 시선 방향 즉시 설정(스무딩 없음). normalizedDir은 **머리 로컬 기준** 방향 벡터(길이 0~1)이며,
        /// 길이가 1을 넘으면 clamp된다 — 호출부가 정규화를 잊어도 눈동자가 머리 밖으로 튀어나가지 않는다.
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

        /// <summary>바라보는 방향 설정(+1 오른쪽 / -1 왼쪽). 눈 중립 위치의 X가 함께 미러링된다.
        /// 눈동자 오프셋 자체는 미러링하지 않는다 — 오프셋은 "커서가 실제로 어느 쪽에 있는가"이므로
        /// 몸이 어느 쪽을 보고 있든 화면상 같은 방향을 가리켜야 한다.</summary>
        public void SetFacing(float sign)
        {
            float next = sign >= 0f ? 1f : -1f;
            if (Mathf.Approximately(next, _facingSign)) return;
            _facingSign = next;
            Apply();
        }

        private void Apply()
        {
            var offset = new Vector3(_lookDirection.x * _appliedMaxOffset, _lookDirection.y * _appliedMaxOffset, 0f);
            if (_leftEye != null) _leftEye.localPosition = Mirror(_leftNeutral) + offset;
            if (_rightEye != null) _rightEye.localPosition = Mirror(_rightNeutral) + offset;
        }

        private Vector3 Mirror(Vector3 neutral)
        {
            return new Vector3(neutral.x * _facingSign, neutral.y, neutral.z);
        }

        /// <summary>정면(중립)을 즉시 보게 한다 — SnapToIdlePose() 같은 "보간 없는" 경로 전용.</summary>
        public void LookForward() => SetLookDirection(Vector2.zero);
    }
}
