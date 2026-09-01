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
            // ★ 2026-09-01 — 여기는 원래 "발판 참 좁네"였다. 두 가지가 동시에 잘못돼 있었다.
            //
            //   (1) <b>"발판"은 팀 내부 용어다.</b> 코드에서 발판 = "캐릭터가 설 수 있는 상단선"이고
            //       그 정체는 <b>실제 창 / Dock / 화면 최하단 안전망</b> 셋이다(사용자 기준 분류는
            //       ArcheryDirector.IsRealWindowFoothold — 안전망만 "바탕화면", Dock과 창은 "창").
            //       화면에는 그런 이름의 물건이 하나도 없으므로 처음 보는 사람은 무엇을 가리키는지
            //       알 수 없다. 사용자 말로 옮기면 "창 위" 또는 "바탕화면"이다.
            //   (2) <b>"좁다"가 상태에서 파생되지 않는다</b>(불변 원칙 1). Idle은 저 셋 중 어디서나
            //       일어나는데 화면 최하단 안전망은 <b>화면 전체 폭</b>이다. 즉 이 문장은 서 있는
            //       자리에 따라 <b>그냥 거짓</b>이 되고, 폭을 실제로 재서 말하려면 대사가 아니라
            //       상태 쪽 계약을 늘려야 한다(이번 라운드 범위 밖 — 리더 보고).
            //
            //   대체 문구는 Idle이 <b>정의상 참으로 만드는 사실</b>만 말한다: Idle은 접지 상태에서만
            //   유지되므로(IdleState.Tick의 GroundedTick/CheckScreenBoundsOrFall) "발밑이 단단하다"는
            //   어디에 서 있든 참이다. 글자 수도 7자로 같아 발화 자격 게이트(규칙 8) 거동이 안 바뀐다.
            "발밑이 단단해",
            "구경 중이야",
        };

        /// <summary>WALK 혼잣말 — 걷는 중이라는 현재 사실에 대한 서술만 담는다.</summary>
        private static readonly string[] WalkLines =
        {
            "산책 중",
            "저쪽으로 가볼까",
            "하나 둘 하나 둘",
            "다리 좀 풀자",
            // ★ 2026-09-01 — 여기는 원래 "창 위는 미끄러워"였다. 위 IdleLines의 "발판 참 좁네"와
            //   **완전히 같은 결함**이고, 페르소나 실측이 그것을 그대로 잡았다:
            //     [말풍선] 표시 (Walk) "창 위는 미끄러워"
            //     [발판리포트] 보이는 상단테두리 0개 … 합성=[Dock, 안전망…] | 딛고있음=Dock
            //   즉 **실제 창이 하나도 없는데 "창 위는"**이라고 말했다.
            //
            //   두 주장이 각각 따로 거짓이다:
            //   (1) <b>"창 위"가 자리에서 파생되지 않는다.</b> Walk가 성립하는 발판은 실제 창 /
            //       Dock / 화면 최하단 안전망 셋인데(사용자 기준 분류는
            //       ArcheryDirector.IsRealWindowFoothold), 뒤의 둘은 창이 아니다.
            //   (2) <b>"미끄러워"가 거동에서 파생되지 않는다.</b> 이 저장소에서 미끄러짐은
            //       <b>결함 지표로만</b> 존재한다 — Tests/PlayMode/WalkFootSlipTests가 발 미끄러짐
            //       상한(0.30)을 넘으면 빨간불을 내는 "문워크 검사"다. 정상 동작에서 캐릭터는
            //       **정의상 미끄러지지 않는다.** 어느 발판에 서 있든 이 절반은 거짓이다.
            //
            //   ★ "실제 창일 때만 이 대사가 나오게 한다"는 갈래는 택하지 않았다. (2)가 남아 문장이
            //     여전히 절반 거짓이고, 자리를 아는 대사를 하려면 줄 번호를 거르는 임시 필터가 아니라
            //     ChatterParams에 발판 종류를 Enter() 시점 스냅샷으로 싣는 정식 확장이 필요하다
            //     (원칙 1의 파라미터 경로를 두 갈래로 쪼개지 않기 위해서다) — 리더 보고 사항.
            //
            //   대체 문구는 Walk가 <b>정의상 참으로 만드는 사실</b>만 말한다: Walk는 이동 의도가
            //   데드존을 넘는 동안만 유지되고(WalkState.Tick) 그동안 보행 위상이 계속 돌아 다리가
            //   번갈아 나간다(StickmanPoseAnimator의 걷기 키포즈). 평가어("잘")라 반증 대상도 아니다.
            //   글자 수도 9자로 같아 가독예산(0.955초)과 발화 자격 게이트 거동이 한 톨도 안 바뀐다.
            "다리가 잘 나가네",
        };

        /// <summary>
        /// 텍스트 매핑 함수(순수). 상태 ID로 대사표를 고르고, 파라미터의 줄 번호로 한 줄을 꺼낸다.
        /// 같은 입력이면 항상 같은 출력이며 난수/시간/전역 상태를 읽지 않는다 — 그래야 "이 텍스트가
        /// 어느 Enter() 호출의 어느 파라미터 스냅샷에서 나왔는지"를 역추적할 수 있다(31-3 체크리스트).
        /// </summary>
        public static DialogueLine Resolve(StickmanStateId stateId, object dialogueParams)
        {
            string[] table = stateId == StickmanStateId.Walk ? WalkLines : IdleLines;
            var p = dialogueParams as ChatterParams;
            int index = p != null ? p.LineIndex : 0;
            if (table.Length == 0) return DialogueLine.Say(string.Empty);
            // ★ 종류 = Narrative(진행 서술, UX_FLOW.md 5절 규칙 4-a). 이 표의 문장은 전부
            //   "나는 지금 X하고 있다"이므로 그 상태가 끝나는 순간 **문장 자체가 거짓이 된다** —
            //   실측에서 가장 선명한 거짓말이 "걸으면서 '잠깐 쉬는 중'"이었다. 그래서 상태 종료 시
            //   가독예산을 무시하고 즉시 컷되고(규칙 4-c ③), 대신 애초에 말할 시간이 없으면
            //   발화 자격 게이트가 침묵시킨다(규칙 8, IdleState/WalkState의 TryCreate 호출부).
            return DialogueLine.Say(table[((index % table.Length) + table.Length) % table.Length]);
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
                // ★ 2026-09-01 설정창 — "말풍선 표시"/"잡담 빈도"는 사용자 설정이 있으면 그것을 따른다.
                //   확률값 자체를 덮어쓰지 않고 배율로 곱하는 이유는 35-1-3 ③과 같다: 원래 값을 지우면
                //   되돌릴 수 없다(고른 적이 없으면 에셋 값 그대로라 거동 무변화).
                if (!StickMate.Core.AppSettingsModel.ResolveDialogueBubbleEnabled(config)) return false;
                if (Time.unscaledTime < blackboard.NextChatterAllowedUnscaledTime) return false;

                float chance = stateId == StickmanStateId.Walk
                    ? StickMate.Core.AppSettingsModel.ResolveWalkChatterChance(config)
                    : StickMate.Core.AppSettingsModel.ResolveIdleChatterChance(config);
                if (chance <= 0f) return false;
                if (Random.value >= chance) return false;
            }

            string[] table = stateId == StickmanStateId.Walk ? WalkLines : IdleLines;
            target.LineIndex = Random.Range(0, table.Length);

            // ★ 발화 자격 게이트(UX_FLOW.md 5절 규칙 8, 2026-09-01) — 텍스트가 확정된 **직후**,
            //   쿨다운을 소비하기 **전에** 판정한다. 순서가 중요하다:
            //   · 게이트가 여기 있어야 위 요약("한 번 true를 반환하면 그 전이의 대사는 반드시
            //     만들어진다")이 계속 참이다. 호출부에서 뒤늦게 막으면 그 계약이 깨진다.
            //   · 쿨다운을 먼저 태우면 "말할 시간이 없어서 침묵한" 대가로 다음 발화까지 11초를
            //     기다리게 된다 — 짧은 Walk가 연달아 나오는 구간에서 캐릭터가 통째로 벙어리가 된다.
            //     막힌 발화는 추첨 자체가 없었던 것으로 되돌린다.
            //   · 강제 발화(forced)는 위에서 이미 확률/쿨다운을 건너뛴 것과 같은 이유로 게이트도
            //     건너뛴다 — "지금 말풍선을 보고 싶다"는 사용자 명령 자체가 확정 사실이다.
            if (!forced)
            {
                DialogueLine line = Resolve(stateId, target);
                // ★ 2026-09-01 — 배회 페이즈 잔여가 아니라 **이 상태의** 잔여를 묻는다. 둘이 같은
                //   값인 것은 상태가 배회 페이즈 전환으로 들어왔을 때뿐이고, 격파/기상/착지/등반
                //   복귀는 전부 "배회는 걷는 중인데 Idle로 들어오는" 경로라 예전 질문으로는 게이트가
                //   2.8초를 보고 실제 체류는 1프레임이었다(StickmanBlackboard의 그 프로퍼티 문서 참고).
                float plannedDwell = blackboard.PlannedDwellRemainingSecondsFor(stateId);
                if (!DialogueBudget.IsEligible(line, plannedDwell, DialogueTiming.FadeInSeconds))
                {
                    Debug.Log($"[말풍선] 발화 보류 ({stateId}) \"{line.Text}\" — 서술 대사인데 계획 잔여 " +
                        $"체류 {plannedDwell:F2}초 < 필요체류 " +
                        $"{DialogueBudget.RequiredDwellSeconds(line.Text, DialogueTiming.FadeInSeconds):F2}초" +
                        $"(배회 페이즈 잔여 {blackboard.PlannedWanderDwellRemainingSeconds:F2}초, " +
                        $"이동의도 {blackboard.MoveInputX:F2}). " +
                        "규칙 8 — 말할 시간이 없으면 말하지 않는다(쿨다운은 소비하지 않는다).");
                    return false;
                }
            }

            float cooldown = config != null ? Mathf.Max(0f, config.ambientChatterCooldownSeconds) : 11f;
            blackboard.NextChatterAllowedUnscaledTime = Time.unscaledTime + cooldown;
            return true;
        }
    }
}
