using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Dialogue
{
    /// <summary>
    /// IDLE/WALK 유휴 혼잣말 — docs/UX_FLOW.md 26-3절 "살아있는 느낌" 디테일의 대사 판.
    ///
    /// ============================================================================
    /// 왜 필요했나
    /// ============================================================================
    /// 대사를 만드는 상태는 Attack/Ragdoll/ParkourClimb/LedgeHang/BattleMinigame/Runaway 등
    /// "사건이 일어났을 때"뿐이고, 캐릭터가 실제로 대부분의 시간을 보내는 Idle/Walk에는 대사가 전혀
    /// 없었다(States/IdleState.cs의 `TODO(Phase 2)` 주석이 그 자리를 비워두고 있었다). 그래서 말풍선
    /// 렌더링을 붙여도 사용자가 몇 분씩 아무것도 못 보는 상태가 될 수 있었다. 이 클래스가 그 빈자리를
    /// 채운다.
    ///
    /// ============================================================================
    /// 원칙 1(행동-텍스트 싱크)을 어기지 않는 방식 — 중요
    /// ============================================================================
    /// "그냥 랜덤 문자열을 띄우는" 구조가 아니다. UX_FLOW.md 31-1절이 요구하는 형태를 그대로 따른다:
    ///   1) **말할지 말지**는 상태 전이가 확정된 뒤 <c>Enter(context)</c> 안에서 정해지고, 말하기로
    ///      했다면 그 자리에서 곧바로 <see cref="DialogueIntent"/>가 만들어진다 — 즉 "혼잣말을 한다"는
    ///      행동 자체가 그 전이로부터 파생된 확정 사실이다.
    ///   2) **무엇을 말할지**는 상태가 <see cref="IHasDialogueParams"/>로 구조적으로 노출하는
    ///      <see cref="ChatterParams"/>(고른 줄 번호 스냅샷) 하나에서만 나온다. 텍스트 매핑
    ///      함수(<see cref="Resolve"/>)는 (상태 ID, 파라미터) -> 문자열의 **순수 함수**이며 그 안에서
    ///      난수를 뽑지 않는다 — 난수는 파라미터를 확정하는 Enter() 시점에 이미 소진되고, 그 결과가
    ///      상태 인스턴스에 스냅샷으로 남는다. 31-2 표의 다른 행들(Attack.shotsRemaining 등)과 정확히
    ///      같은 모양이다.
    ///   3) Idle과 Walk는 **각자의 매핑 함수**를 갖지 않는다 — 하나의 <see cref="Resolve"/> 안에서
    ///      상태 ID로 분기한다(31-1의 "같은 매핑 함수 안의 분기만 허용" 정신).
    /// 대사 내용도 계약을 따른다: 전부 **현재형 서술**이고 미래형 약속("이제 뛴다!" 같은 예고)이
    /// 하나도 없다 — 5절이 금지하는 "말만 하고 안 함"이 성립할 여지 자체를 없앤다.
    /// </summary>
    public static class AmbientChatter
    {
        /// <summary>
        /// 상태가 대사 매핑 함수에 노출하는 파라미터 — 이번 전이에서 고른 대사 줄 번호 스냅샷.
        /// 상태 인스턴스가 하나씩 들고 재사용한다(States/AttackState.AttackDialogueParams와 동일 관례).
        /// </summary>
        public sealed class ChatterParams
        {
            public int LineIndex;
        }

        /// <summary>IDLE 혼잣말 — "지금 멈춰 서 있다"는 현재 상황에 대한 서술만 담는다.</summary>
        private static readonly string[] IdleLines =
        {
            "음...",
            "여기 좋네",
            "심심하다",
            "잠깐 쉬는 중",
            "오늘 뭐 하지",
            "하암...",
            "발판 참 좁네",
            "구경 중이야",
        };

        /// <summary>WALK 혼잣말 — 걷는 중이라는 현재 사실에 대한 서술만 담는다.</summary>
        private static readonly string[] WalkLines =
        {
            "산책 중",
            "저쪽으로 가볼까",
            "하나 둘 하나 둘",
            "다리 좀 풀자",
            "창 위는 미끄러워",
        };

        /// <summary>
        /// 텍스트 매핑 함수(순수). 상태 ID로 대사표를 고르고, 파라미터의 줄 번호로 한 줄을 꺼낸다.
        /// 같은 입력이면 항상 같은 출력이며 난수/시간/전역 상태를 읽지 않는다 — 그래야 "이 텍스트가
        /// 어느 Enter() 호출의 어느 파라미터 스냅샷에서 나왔는지"를 역추적할 수 있다(31-3 체크리스트).
        /// </summary>
        public static string Resolve(StickmanStateId stateId, object dialogueParams)
        {
            string[] table = stateId == StickmanStateId.Walk ? WalkLines : IdleLines;
            var p = dialogueParams as ChatterParams;
            int index = p != null ? p.LineIndex : 0;
            if (table.Length == 0) return string.Empty;
            return table[((index % table.Length) + table.Length) % table.Length];
        }

        /// <summary>
        /// "이번 전이에서 혼잣말을 할 것인가"를 판정하고, 하기로 했다면 <paramref name="target"/>에
        /// 줄 번호 스냅샷을 채운 뒤 true를 반환한다(호출자는 그때만 DialogueIntent를 만든다).
        ///
        /// 판정 순서가 곧 계약이다 — 확률/쿨다운 추첨은 <b>텍스트를 만들기 전에</b> 전부 끝나며,
        /// 한 번 true를 반환하면 그 전이의 대사는 반드시 만들어진다("말할지 말지"를 나중에 번복하는
        /// 경로가 없다). 쿨다운 타이머는 Idle과 Walk가 공유한다(둘은 2~6초마다 번갈아 일어나므로
        /// 따로 두면 체감상 수다스러워진다).
        /// </summary>
        public static bool TryRollChatter(StickmanBlackboard blackboard, StickmanStateId stateId, ChatterParams target)
        {
            if (blackboard == null || target == null) return false;
            StickConfig config = blackboard.Config;

            // 강제 발화 펄스(Interaction/AppControlDirector.cs의 Ctrl+Opt+Cmd+B 데모 단축키)는 확률과
            // 쿨다운을 모두 건너뛴다 — "지금 말풍선을 보고 싶다"는 사용자 명령 자체가 확정 사실이다.
            bool forced = blackboard.ForcedChatterSignaled;
            blackboard.ForcedChatterSignaled = false; // 소비 즉시 리셋(이 프로젝트의 1프레임 펄스 관례).

            if (!forced)
            {
                if (config == null) return false;
                if (!config.dialogueBubbleEnabled) return false;
                if (Time.unscaledTime < blackboard.NextChatterAllowedUnscaledTime) return false;

                float chance = stateId == StickmanStateId.Walk ? config.walkChatterChance : config.idleChatterChance;
                if (chance <= 0f) return false;
                if (Random.value >= chance) return false;
            }

            string[] table = stateId == StickmanStateId.Walk ? WalkLines : IdleLines;
            target.LineIndex = Random.Range(0, table.Length);

            float cooldown = config != null ? Mathf.Max(0f, config.ambientChatterCooldownSeconds) : 11f;
            blackboard.NextChatterAllowedUnscaledTime = Time.unscaledTime + cooldown;
            return true;
        }
    }
}
