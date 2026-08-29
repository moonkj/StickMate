using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// RAGDOLL 강제 전이 판정("충격량 &gt;= ragdollForceThreshold면 Ragdoll로, 아니면 그대로")을 한
    /// 곳에 모은 순수 함수 헬퍼(부작용은 blackboard.Machine.ChangeState 호출 하나뿐).
    ///
    /// 왜 필요한가(Phase 3): Core/StickmanAgent.ReportExternalImpact()가 원래 이 판정의 유일한
    /// 진입점이었지만(Phase 2, "단일 진입점" 설계), 그 메서드는 MonoBehaviour 인스턴스 메서드라 블랙보드만
    /// 가진 순수 상태/컨트롤러 클래스(States/DragThrowState.cs — 던진 속도로부터 계산한 충격량,
    /// States/RodeoCursorState.cs — 거친 흔들기로 튕겨 떨어질 때, Interaction/RivalStickmanAgent.cs —
    /// 라이벌 자신이 맞았을 때)에서 직접 호출할 수 없다(참조 대상이 다름). 이 정적 유틸로 로직을 분리해
    /// 세 곳 이상에서 같은 판정식이 어긋나지 않게 한다 — StickmanAgent.ReportExternalImpact()도 내부적으로
    /// 이 메서드를 호출하도록 리팩터했다(공개 시그니처는 그대로, 내부 구현만 위임).
    /// </summary>
    public static class RagdollImpactResolver
    {
        /// <summary>
        /// "발밑에서 올라온 충돌"로 볼 접촉점의 높이 상한 — 발 높이 + 신장의 이 비율까지. 루트 원점이
        /// 곧 발바닥이라는 이 프로젝트의 규약(StickmanBlackboard.SenseGround 문서)을 그대로 쓴다.
        /// 0이 아니라 여유를 두는 이유: 캡슐 콜라이더의 접촉점은 정확히 발바닥 선에 찍히지 않고,
        /// 빠른 낙하에서는 한 물리 스텝 안에 몇 센티 파고든 뒤 접촉이 생성되기 때문이다. 반대로 이
        /// 값이 크면 옆에서 들어온 타격까지 착지로 오인하므로 신장의 20%(무릎 높이 언저리)로 묶는다.
        /// </summary>
        private const float LandingContactHeightRatio = 0.2f;

        /// <summary>
        /// ★ 충돌 콜백 전용 진입점(2026-08-29, "무릎앉아 착지" 라운드). 충격량 판정 자체는 아래
        /// <see cref="TryApplyImpact"/>와 완전히 같고, 그 앞에 **"이건 외력이 아니라 내 착지다"** 라는
        /// 한 가지 예외만 둔다.
        ///
        /// 왜 필요한가: 씬에는 눈에 보이지 않는 물리 바닥이 있고(Editor/SceneBootstrapper의 PhysicsGround),
        /// 캐릭터 루트는 능동 상태에서도 Dynamic이라 낙하 중 그 바닥에 부딪히면 OnCollisionEnter2D가
        /// 발생한다. 충격량은 relativeVelocity * mass(=1)이고 ragdollForceThreshold가 8, gravityScale이
        /// 3이므로 계산상 v = sqrt(2*9.81*3*h) = 8, 즉 **1.09유닛만 떨어져도** 임계값을 넘는다.
        ///
        /// ★ 다만 실측 결과는 계산보다 좁았다(정직하게 기록 — 2026-08-29 PlayMode 실측 로그 [착지충격]).
        /// **논리 발판이 있는 정상 착지에서는 이 충격량이 0.00이다.** FallState의 스윕 교차 판정이
        /// Update에서 먼저 착지를 확정하면서 몸을 발판 상단으로 스냅하고 하강 속도를 지우기 때문에,
        /// 그 다음 물리 스텝에서 생기는 접촉은 이미 정지 상태의 안착 접촉이다. 그래서 이 예외가 실제로
        /// 겨냥하는 것은 **물리 바닥은 있는데 논리 발판은 없는 구간**이다 —
        /// Editor/SceneBootstrapper.CreateGroundCollider가 명시한 그 상황(화면 최하단 안전망은 Dock 가로
        /// 구간에 구멍이 있는 반면 PhysicsGround는 전체 폭이라, 그 구간에서 캐릭터는 "물리적으로는
        /// 떠받쳐지지만 논리적으로는 접지하지 않는다"). 그리로 떨어지면 착지가 확정되지 않아 스냅도
        /// 없고, 몸이 전속력 그대로 바닥에 부딪혀 즉시 랙돌이 된다. 사용자가 실제 데스크톱에서 캐릭터를
        /// Dock 구간으로 던지거나 떨어뜨렸을 때 밟게 되는 경로가 그것이다.
        /// Tests/PlayMode/LandingCrouchTests가 그 시나리오로 이 스위치의 on/off 대조를 실측한다.
        ///
        /// 판정 근거는 아키텍처 0절이다 — RAGDOLL이 배정된 대상은 **피격/던져짐 같은 외력**이고, 자기가
        /// 떨어져서 땅에 닿는 것은 외력이 아니라 착지다. 그래서 다음 두 조건을 **동시에** 만족할 때만
        /// 무시한다:
        ///   (1) 지금 상태가 공중/착지 계열(Jump / Fall / LandingCrouch)이고,
        ///   (2) 접촉점이 발 높이 근처 이하다(= 발밑에서 올라온 면).
        /// 그래서 옆에서 날아온 라이벌의 주먹이나 던져져 벽에 부딪히는 충돌은 그대로 랙돌이 되고,
        /// 직접 호출 경로(DragThrowState의 던진 속도 / RivalStickmanAgent의 타격 / RodeoCursorState의
        /// 거친 흔들기)는 애초에 이 메서드를 거치지 않으므로 전혀 영향을 받지 않는다.
        /// StickConfig.landingImpactRagdollShield를 끄면 이 예외 전체가 사라져 예전 거동으로 되돌아간다
        /// (Tests/PlayMode/LandingCrouchTests.cs의 네거티브 컨트롤이 그 스위치를 쓴다).
        /// </summary>
        /// <returns>Ragdoll로 전이시켰으면 true.</returns>
        public static bool TryApplyCollisionImpact(StickmanBlackboard blackboard, Collision2D collision, float impulseMagnitude)
        {
            bool shielded = IsOwnLandingContact(blackboard, collision);
            LogCollisionImpact(blackboard, collision, impulseMagnitude, shielded);
            if (shielded) return false;
            return TryApplyImpact(blackboard, impulseMagnitude);
        }

        /// <summary>
        /// 충돌 충격 통지의 진단 로그. 리더가 화면을 볼 수 없으므로 "착지 충격이 어디로 갔는가"는 로그가
        /// 유일한 판별 수단이다 — 그런데 24시간 상주 앱이라 매 충돌마다 남기면 로그가 무너진다. 그래서
        /// [눈추적]/[발판리포트]와 동일한 컨벤션을 쓴다: **임계값을 넘는 충격만**, 그리고 기본적으로
        /// 시작 직후 CollisionLogSampleCount회까지만. StickConfig.verboseDiagnosticsLogging을 켜면 계속 남는다.
        /// </summary>
        private static int _collisionLogSamplesLeft = CollisionLogSampleCount;
        private const int CollisionLogSampleCount = 6;

        private static void LogCollisionImpact(StickmanBlackboard blackboard, Collision2D collision,
            float impulseMagnitude, bool shielded)
        {
            if (blackboard == null || blackboard.Config == null) return;
            // 충돌 진입은 이산 이벤트(매 프레임이 아니다)라 시작 직후 몇 건은 세기와 무관하게 전부 남긴다 —
            // "충돌이 아예 발생하지 않는 것"과 "발생했는데 약한 것"을 구분하지 못하면 진단이 불가능하기 때문이다.
            // 그 표본을 다 쓰면 임계값 이상만, 그것도 verboseDiagnosticsLogging이 켜져 있을 때만 남긴다.
            bool verbose = blackboard.Config.verboseDiagnosticsLogging;
            if (_collisionLogSamplesLeft > 0) _collisionLogSamplesLeft--;
            else if (!verbose || impulseMagnitude < blackboard.Config.ragdollForceThreshold) return;

            float footY = blackboard.Body != null ? blackboard.Body.position.y : float.NaN;
            float lowestContactY = float.NaN;
            int count = collision != null ? collision.contactCount : 0;
            for (int i = 0; i < count; i++)
            {
                float y = collision.GetContact(i).point.y;
                if (float.IsNaN(lowestContactY) || y < lowestContactY) lowestContactY = y;
            }

            Debug.Log($"[착지충격] 충돌 충격량={impulseMagnitude:F2}(랙돌 임계 " +
                $"{blackboard.Config.ragdollForceThreshold:F1}), 상태=" +
                $"{(blackboard.Machine != null ? blackboard.Machine.CurrentStateId.ToString() : "?")}, " +
                $"접촉 {count}개(최저 y={lowestContactY:F3}), 발 y={footY:F3}, " +
                $"차단스위치={blackboard.Config.landingImpactRagdollShield} -> " +
                $"{(shielded ? "착지로 판정해 무시" : "외력으로 판정 -> RAGDOLL 전이")}.");
        }

        /// <summary>위 문서의 (1)+(2) 판정. 판단에 필요한 것이 하나라도 없으면 안전한 쪽(= 예외 아님,
        /// 즉 기존 거동)으로 false를 돌려준다.</summary>
        public static bool IsOwnLandingContact(StickmanBlackboard blackboard, Collision2D collision)
        {
            if (blackboard == null || blackboard.Machine == null || blackboard.Body == null) return false;
            if (blackboard.Config != null && !blackboard.Config.landingImpactRagdollShield) return false;

            StickmanStateId state = blackboard.Machine.CurrentStateId;
            if (state != StickmanStateId.Fall && state != StickmanStateId.Jump &&
                state != StickmanStateId.LandingCrouch) return false;

            if (collision == null) return false;
            float footY = blackboard.Body.position.y;
            float ceiling = footY + blackboard.CharacterHeightWorld * LandingContactHeightRatio;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                if (collision.GetContact(i).point.y <= ceiling) return true;
            }
            return false;
        }

        /// <returns>임계값 이상이라 Ragdoll로 전이시켰으면 true, 미만이라 아무 것도 하지 않았으면 false.</returns>
        public static bool TryApplyImpact(StickmanBlackboard blackboard, float impulseMagnitude)
        {
            if (blackboard == null || blackboard.Machine == null || blackboard.Config == null) return false;
            if (impulseMagnitude < blackboard.Config.ragdollForceThreshold) return false;

            // UX_FLOW.md 31-2 #2 대비 스냅샷 — RagdollState.Enter()가 이 값을 IHasDialogueParams로
            // 노출해 "윽.../으악!/으아아아악?!" 같은 충격 강도별 대사를 파생시킨다(31-1 원칙).
            blackboard.LastImpactMagnitude = impulseMagnitude;
            blackboard.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            return true;
        }
    }
}
