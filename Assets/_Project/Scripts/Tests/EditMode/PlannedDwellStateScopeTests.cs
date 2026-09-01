using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 발화 자격 게이트(docs/UX_FLOW.md 5절 규칙 8)의 <b>사각지대</b> 회귀 테스트 —
    /// 2026-09-01 실측.
    ///
    /// ============================================================================
    /// 무엇이 잡혔나
    /// ============================================================================
    /// <code>
    /// frame=11110 표시 (BattleMinigame) "어... 힘이 다 샜다"  가독예산 1.18초
    /// frame=11111 교체 — 이전 노출 0.02초, 새 "여기 좋네"(Idle)
    /// frame=11112 즉시 컷 (Idle) "여기 좋네" 노출 0.02초
    /// frame=11114 제거, 노출 0.05초
    /// </code>
    /// <b>4프레임 안에 글자 블록 두 개가 각각 20ms씩 번쩍였다</b> — 규칙 8이 없애려던 바로 그
    /// 현상이, 규칙 8이 들어간 그 빌드에서.
    ///
    /// ============================================================================
    /// 근본 원인 한 줄
    /// ============================================================================
    /// <c>PlannedWanderDwellRemainingSeconds</c>는 <b>배회 AI 페이즈의 잔여</b>이지 <b>이 상태의
    /// 잔여</b>가 아니었다. 둘이 같은 값인 것은 상태가 배회 페이즈 전환으로 들어왔을 때뿐이고,
    /// 격파 종료 → Idle / Getup → Idle / LandingCrouch → Idle·Walk / ParkourClimb 완료 → Idle /
    /// GroundLossHang 복귀 → Idle·Walk 는 <b>전부 배회가 Moving 한복판인데 Idle로 들어오는</b>
    /// 경로다. 그때 게이트에 물으면 "2.8초 남았다"고 답하고 실제 체류는 1프레임이다.
    ///
    /// 예전에는 그중 <b>한 경로만</b>(<c>context.From != GroundLossHang</c>) 손으로 막혀 있었다.
    /// 이 테스트가 잠그는 것은 그 손패치가 아니라, 경로를 열거하지 않는 규칙 하나다:
    /// <b>이동 의도가 이 상태를 이미 부정하고 있으면 계획 잔여는 0이다.</b>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// 모든 검사가 짝으로 되어 있다 — 의도가 <b>일치</b>할 때는 반드시 원래 잔여를 그대로 답하고
    /// 대사도 나와야 한다. 그렇지 않으면 "항상 0을 답한다 / 항상 침묵한다"는 오답이 통과한다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(배회 AI·대사 파이프라인에 플랫폼 분기 없음).</para>
    /// </summary>
    public sealed class PlannedDwellStateScopeTests
    {
        /// <summary>
        /// 이동 의도와 계획 잔여를 테스트가 직접 정하는 스텁. 실제 <see cref="AutoWanderController"/>와
        /// 같은 두 인터페이스를 구현하므로 블랙보드에서 보이는 모양이 프로덕션과 동일하다.
        /// </summary>
        private sealed class StubIntent : IMovementIntentSource, IPlannedDwellSource
        {
            public float MoveInputX { get; set; }
            public float PlannedDwellRemainingSeconds { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>계획을 모르는 소스(<see cref="IPlannedDwellSource"/> 미구현) — NaN 경로 확인용.</summary>
        private sealed class PlanlessIntent : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private const float PhaseRemaining = 2.8f; // 실측 로그의 "게이트에게 물으면 2.8초" 그 값.

        private StickConfig _config;
        private StickmanBlackboard _blackboard;
        private StubIntent _intent;
        private UnityEngine.Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting();

            // 잡담 추첨의 난수를 결정론으로 고정한다(전역 상태이므로 TearDown에서 원복).
            _randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(20260901);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.dialogueBubbleEnabled = true;
            _config.idleChatterChance = 1f;
            _config.walkChatterChance = 1f;
            _config.ambientChatterCooldownSeconds = 0f;

            _intent = new StubIntent { PlannedDwellRemainingSeconds = PhaseRemaining };
            _blackboard = new StickmanBlackboard { Config = _config, IntentSource = _intent };
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Random.state = _randomState;
            _blackboard = null;
            _intent = null;
            if (_config != null) Object.DestroyImmediate(_config);
            _config = null;
            AppSettingsModel.ResetForTesting();
        }

        /// <summary>데드존을 확실히 넘는 이동 의도(프로덕션 배회 AI는 ±1을 낸다).</summary>
        private float MovingInput => _config.moveInputDeadzone + 1f;

        // ==================== 0. ★ 되올라가기 경합 (2026-09-02 실측 → 같은 날 수정 완료) ====================

        /// <summary>
        /// ★★ <b>1프레임짜리 Idle에서 대사가 파생된다 — 불변 원칙 1 위반(2026-09-02 수정 완료).</b>
        /// 리더 실측(독립 2회, 프레임 배열까지 동일한 서명, 벽타기 완료 11회 중 2회 = 18%):
        /// <code>
        ///   frame=68488 [벽타기] 완료 → [말풍선] "심심하다"(Idle) 표시
        ///   frame=68488 [되올라가기] 안착 — 턱 안쪽으로 걸어 들어갑니다
        ///   frame=68489 [말풍선] 즉시 컷 (Idle) — 컷사유=상태종료, 노출 0.02초
        /// </code>
        ///
        /// <para><b>기제(소스에서 확정, 추측 아님)</b> — <c>StickmanAgent.Update</c>의 순서는
        /// <c>_autoWander.Tick</c> → <c>_machine.Tick</c>이다.
        /// <list type="number">
        ///   <item><c>ParkourClimbState</c>가 맨틀 완료 프레임에
        ///         <c>_blackboard.ReportClimbMantleCompleted(_direction)</c>로 <b>"곧 턱 안쪽으로
        ///         걸어 들어간다"를 확정 사실로 기록</b>한다.</item>
        ///   <item><b>바로 다음 줄에서</b> <c>MoveInputX</c>를 읽어 다음 상태를 고른다. 그 값은
        ///         아직 <b>0</b>이다(배회 AI는 다음 프레임에야 그 신호를 소비해 <c>EnterMoving</c>을
        ///         부른다) → <b>Idle</b>을 고른다.</item>
        ///   <item>Idle의 <c>Enter()</c>가 발화 자격 게이트에 묻는다.
        ///         <c>PlannedDwellRemainingSecondsFor(Idle)</c>는 "의도와 상태가 어긋나면 0"이라는
        ///         옳은 규칙을 쓰지만, 그 <c>MoveInputX</c>가 <b>블랙보드에 이미 기록된 맨틀 신호에
        ///         대해 한 프레임 낡았다</b> → 배회 페이즈 잔여(2.8초)를 그대로 답한다 → <b>통과</b>.</item>
        ///   <item>다음 프레임에 배회 AI가 신호를 소비해 <c>EnterMoving</c> → Idle → Walk → 즉시 컷.</item>
        /// </list></para>
        ///
        /// <para><b>리더 질문의 답</b>: <i>"되올라가기 직후에도 그 판정이 서는가?"</i> → <b>서지 않는다.</b>
        /// 게이트가 보는 두 사실(페이즈 잔여 · 이동 의도)이 <b>둘 다</b> 맨틀 신호에 대해 낡았다.</para>
        ///
        /// <para><b>어떻게 고쳤나(둘 다 넣었다, 그리고 그 이유)</b>:
        /// <list type="number">
        ///   <item><b>근본 — <c>ParkourClimbState</c></b>가 맨틀 직후 다음 상태를 <c>MoveInputX</c>가
        ///         아니라 <b>자기가 방금 확정한 사실</b>에서 고른다(= Walk). <b>1프레임 Idle 자체가
        ///         사라지므로</b> 대사뿐 아니라 포즈/발소리 등 모든 파생물이 함께 고쳐진다.
        ///         아래 <c>맨틀_직후_다음_상태는_이동의도가_아니라_확정된_맨틀에서_고른다</c>가 잠근다.</item>
        ///   <item><b>같은 사실의 두 번째 소비자 — <c>PlannedDwellRemainingSecondsFor</c></b>도
        ///         맨틀이 확정된 프레임에는 "이동 의도 = 걸어 들어감"으로 읽는다. 이 테스트가 잠근다.</item>
        /// </list></para>
        ///
        /// <para>★★ <b>왜 둘 다인가 — 이 테스트만으로는 근본 수정을 판별할 수 없기 때문이다.</b>
        /// 이 테스트는 게이트를 <b>직접</b> 호출해 합성한 경합을 보므로, (2)만 고쳐도 초록이 되고
        /// (1)이 없어도 초록이 된다. 즉 <b>"감사를 통과하는 유일한 배선이 곧 신고된 결함"</b>이 될 수
        /// 있는 형태였다(2026-09-02 같은 밤에 다른 라운드에서 실제로 난 사고). 그래서 근본 수정 쪽은
        /// 아래에 <b>별도 검사</b>를 세워 각각 독립적으로 잠갔다.</para>
        /// </summary>
        [Test]
        public void 맨틀_직후_1프레임_Idle에서는_대사가_나오지_않는다()
        {
            // ★ 네거티브 컨트롤(먼저) — 맨틀이 없으면 이 조합은 <b>정상적으로 말해야</b> 한다.
            //   이게 없으면 아래 단언이 "언제나 침묵"이라는 오답과 구별되지 않는다.
            _intent.MoveInputX = 0f;
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f,
                "맨틀이 없는 평범한 Idle인데 계획 잔여가 0이다 — 그러면 캐릭터가 통째로 벙어리가 된다.");
            Assert.IsTrue(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle,
                    new AmbientChatter.ChatterParams()),
                "맨틀이 없는 평범한 Idle에서 발화가 막혔다 — 게이트가 과잉 차단하고 있다.");

            // ★ 경합 합성 — ParkourClimbState가 맨틀을 보고한 <b>그 프레임</b>의 상태 그대로다:
            //   배회는 Moving 페이즈(2.8초 남음)이고, 경계 정지 중이라 이동 의도는 아직 0이다.
            //   배회 AI는 <b>다음 프레임</b>에야 이 신호를 소비해 EnterMoving을 부른다.
            _blackboard.ReportClimbMantleCompleted(1);
            _intent.MoveInputX = 0f;

            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f,
                "맨틀이 확정된 프레임인데 Idle의 계획 잔여가 남아 있다 — 게이트가 보는 이동 의도가 " +
                "<이미 블랙보드에 기록된 맨틀 신호>에 대해 한 프레임 낡았다는 뜻이다.");
            Assert.IsFalse(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle,
                    new AmbientChatter.ChatterParams()),
                "맨틀 완료 프레임의 Idle에서 대사가 파생됐다 — 실제 체류는 1프레임(0.02초)이고, " +
                "다음 프레임에 Walk로 밀려나면서 즉시 컷된다(절대 불변 원칙 1 위반).");

            // ★ 반대쪽도 확인한다 — 맨틀은 "멈춤"이 아니라 <b>"걸어 들어감"</b>이 확정된 사실이다.
            //   Idle만 0으로 만들고 Walk까지 0이면, 근본 수정으로 실제로 진입하는 Walk가 통째로
            //   벙어리가 된다(증상을 반대쪽으로 옮기는 것일 뿐이다).
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f,
                "맨틀 확정 프레임의 Walk에까지 0을 답한다 — 되올라간 뒤 걸어 들어가는 2.8초가 통째로 침묵한다.");
        }

        /// <summary>
        /// ★★ <b>근본 수정 쪽</b>을 따로 잠근다(2026-09-02).
        ///
        /// <para>위 테스트는 <b>게이트</b>의 성질만 본다. 그런데 리더가 채택한 근본 수정은 게이트가
        /// 아니라 <c>ParkourClimbState</c>가 <b>다음 상태를 무엇으로 고르는가</b>다 — 1프레임 Idle
        /// 자체를 없애 대사뿐 아니라 포즈·발소리 등 모든 파생물을 함께 고치는 쪽이다.
        /// <b>즉 위 테스트만으로는 근본 수정의 유무를 판별할 수 없다</b>(게이트만 고쳐도 초록이 된다).
        /// 그 사각지대를 이 검사가 메운다.</para>
        ///
        /// <para>소스 텍스트 스캔인 이유: 이 전이는 실제 씬/물리/등반 진행이 있어야 도달하는 경로라
        /// EditMode에서 상태 인스턴스만으로 재현할 수 없고, PlayMode로 옮기면 "무엇을 고르는가"라는
        /// 이 한 가지 결정이 등반 성공 여부에 가려진다. 이 파일의 다른 검사들과 달리 <b>구조</b>를
        /// 잠그는 검사임을 밝혀 둔다.</para>
        /// </summary>
        [Test]
        public void 맨틀_직후_다음_상태는_이동의도가_아니라_확정된_맨틀에서_고른다()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Project", "Scripts", "States", "ParkourClimbState.cs"));

            // 주석은 벗긴다 — 결함을 설명하는 주석 자체가 구현으로 오인되던 함정(다른 감사와 같은 관례).
            var sb = new StringBuilder(src.Length);
            foreach (string line in src.Replace("\r\n", "\n").Split('\n'))
            {
                int c = line.IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(c >= 0 ? line.Substring(0, c) : line).Append('\n');
            }
            string exec = sb.ToString();

            int mantle = exec.IndexOf("ReportClimbMantleCompleted(", System.StringComparison.Ordinal);
            Assert.Greater(mantle, 0, "맨틀 보고가 사라졌다 — 이 검사의 대상이 없다.");

            string afterMantle = exec.Substring(mantle);
            StringAssert.Contains("ChangeState(StickmanStateId.Walk)", afterMantle,
                "맨틀 직후 전이가 Walk 확정이 아니다 — 그러면 1프레임 Idle이 되살아나고, " +
                "그 프레임에서 파생된 대사가 0.02초 만에 잘린다(원칙 1).");
            StringAssert.DoesNotContain("MoveInputX", afterMantle,
                "맨틀 직후 다음 상태를 다시 MoveInputX로 고르고 있다 — 그 값은 이 시점에 " +
                "<이미 기록된 맨틀 신호>보다 한 프레임 낡았다(StickmanAgent.Update: " +
                "_autoWander.Tick -> _machine.Tick).");
        }

        // ==================== 1. 상태 범위 질의 자체 ====================

        [Test]
        public void 이동_의도가_데드존_밖이면_Idle의_계획_잔여는_0이다()
        {
            _intent.MoveInputX = MovingInput;

            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f,
                "배회가 '걷는 중'인데 Idle로 들어온 경우다 — Idle은 다음 Tick에 곧바로 Walk로 나간다. " +
                "그런데 게이트에는 배회 페이즈 잔여를 그대로 답하고 있었다(실측 2.8초 vs 실제 1프레임).");

            // ★ 네거티브 컨트롤 — "항상 0"이 아니다. 의도와 일치하는 Walk에는 잔여를 그대로 답한다.
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f,
                "이동 의도와 일치하는 Walk에까지 0을 답하면 배회 중 대사가 통째로 사라진다.");
        }

        [Test]
        public void 이동_의도가_데드존_안이면_Walk의_계획_잔여가_0이다()
        {
            _intent.MoveInputX = 0f;

            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f,
                "정지 의도인데 Walk에 잔여를 답하면 Walk가 다음 Tick에 Idle로 나가면서 서술 대사가 컷된다.");

            // ★ 네거티브 컨트롤 — 반대쪽(Idle)은 그대로여야 한다.
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f);
        }

        [Test]
        public void 데드존_경계는_상태의_탈출_조건과_같은_판정을_쓴다()
        {
            // IdleState.Tick / WalkState.Tick 은 둘 다 Abs(move) > deadzone / <= deadzone 으로 나눈다.
            // 게이트가 다른 경계를 쓰면 그 차이만큼 "게이트는 남았다는데 상태는 이미 나간" 틈이 생긴다.
            _intent.MoveInputX = _config.moveInputDeadzone; // 경계값 = 아직 "정지"다.
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f,
                "데드존 경계값은 상태 쪽에서 '정지'로 취급된다 — 게이트도 같아야 한다.");
            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f);

            _intent.MoveInputX = _config.moveInputDeadzone + 1e-3f; // 경계 바로 바깥 = "이동"이다.
            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f);
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f);
        }

        [Test]
        public void 배회_계획이_서술하지_않는_상태에는_모른다고_답한다()
        {
            _intent.MoveInputX = MovingInput;

            Assert.IsTrue(float.IsNaN(_blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.BattleMinigame)),
                "격파는 자기 게이지 길이를 스스로 알고 게이트에 직접 넘긴다 — 배회 잔여는 그 상태에 " +
                "대해 아무 말도 하지 않으므로 0(=침묵 강제)이 아니라 NaN(=모름)이어야 한다.");
            Assert.IsTrue(float.IsNaN(_blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.ParkourClimb)));
        }

        [Test]
        public void 계획을_모르는_소스면_상태가_일치해도_NaN이다()
        {
            _blackboard.IntentSource = new PlanlessIntent { MoveInputX = 0f };

            Assert.IsTrue(float.IsNaN(_blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle)),
                "IPlannedDwellSource를 구현하지 않은 소스(테스트 스텁 다수)는 계획을 모른다 — " +
                "게이트는 그때 막지 않는다(규칙 8은 최적화이지 검열이 아니다).");
        }

        // ==================== 2. 게이트가 실제로 그 값을 본다 ====================

        [Test]
        public void 게이트는_상태_범위_잔여를_보고_어긋난_Idle_잡담을_보류한다()
        {
            var target = new AmbientChatter.ChatterParams();
            _intent.MoveInputX = MovingInput; // "배회는 걷는 중인데 Idle로 들어왔다"

            Assert.IsFalse(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target),
                "다음 프레임에 Walk로 나갈 Idle인데 서술 잡담을 허용했다 — 화면에서 20ms 번쩍인다.");

            // ★ 네거티브 컨트롤 — 의도가 일치하면 같은 조건에서 반드시 발화한다.
            _intent.MoveInputX = 0f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.IsTrue(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target),
                "의도가 일치하는 평범한 Idle에서까지 침묵하면 캐릭터가 통째로 벙어리가 된다 — " +
                "그러면 위 단언은 아무것도 검사하지 않는다.");
        }

        [Test]
        public void 보류된_발화는_쿨다운을_소비하지_않는다()
        {
            var target = new AmbientChatter.ChatterParams();
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            _intent.MoveInputX = MovingInput;

            Assert.IsFalse(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target));
            Assert.AreEqual(0f, _blackboard.NextChatterAllowedUnscaledTime,
                "게이트에 막힌 발화가 쿨다운을 태우면, 어긋난 진입이 잦은 구간에서 캐릭터가 " +
                "말할 수 있는 순간까지 통째로 잃는다(막힌 발화는 추첨이 없었던 것으로 되돌린다).");
        }

        // ==================== 3. 실제 IdleState 진입 경로 (구현을 검사한다) ====================

        /// <summary>대사를 만들지 않는 출발 상태.</summary>
        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private int EnterIdleAndCountDialogue(float moveInputX, StickmanStateId from)
        {
            var idle = new IdleState(_blackboard);
            var machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { from, new SilentState(from) },
                { StickmanStateId.Idle, idle },
            });
            _blackboard.Machine = machine;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            _intent.MoveInputX = moveInputX;

            int count = 0;
            void OnRequested(DialogueIntent _) => count++;
            StickmanEventBus.DialogueRequested += OnRequested;
            try
            {
                machine.Start(from);
                machine.ChangeState(StickmanStateId.Idle);
            }
            finally
            {
                StickmanEventBus.DialogueRequested -= OnRequested;
                machine.ChangeState(from); // 살아 있는 DialogueIntent의 정적 구독을 정리한다.
            }
            return count;
        }

        [Test]
        public void 격파_종료에서_들어온_Idle은_배회가_걷는_중이면_침묵한다()
        {
            Assert.AreEqual(0, EnterIdleAndCountDialogue(MovingInput, StickmanStateId.BattleMinigame),
                "실측 frame=11111의 그 대사다 — 격파가 끝나 Idle로 들어왔지만 배회는 Moving 한복판이라 " +
                "Idle은 다음 Tick에 곧바로 Walk로 나간다. 손으로 막아 둔 GroundLossHang 한 경로 말고 " +
                "나머지 네 경로가 전부 이 모양이었다.");
        }

        [Test]
        public void 네거티브_배회가_쉬는_중이면_같은_진입에서도_말한다()
        {
            Assert.AreEqual(1, EnterIdleAndCountDialogue(0f, StickmanStateId.BattleMinigame),
                "배회 계획과 상태가 일치하는데도 침묵하면 위 검사는 '어떤 Idle이든 침묵'으로 " +
                "통과한다 — 그러면 아무것도 검사하지 않는 단언이 된다.");
        }

        [Test]
        public void 착지에서_들어온_Idle도_같은_규칙을_받는다()
        {
            // 손패치가 아니라 규칙이라는 것의 의미: 진입 상태를 열거하지 않았으므로 다른 경로도
            // 자동으로 같은 판정을 받는다.
            Assert.AreEqual(0, EnterIdleAndCountDialogue(MovingInput, StickmanStateId.LandingCrouch));
            Assert.AreEqual(0, EnterIdleAndCountDialogue(MovingInput, StickmanStateId.Getup));
        }
    }
}
