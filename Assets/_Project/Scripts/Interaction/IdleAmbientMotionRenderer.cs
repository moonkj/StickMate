using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 유휴 앰비언트 동작 — <see cref="StickmanEventBus.WanderAmbientMotionRequested"/>의 소비자
    /// (2026-08-30 배선 감사 잔여 3건 중 1건).
    ///
    /// States/AutoWanderController.cs는 docs/UX_FLOW.md 26-3절의 트리거 조건을 이미 정확히 갖고 있었지만
    /// <b>구독자가 프로젝트 전체에 0명</b>이라 신호가 매번 허공으로 사라졌다(이 프로젝트에서 반복된
    /// "로직은 있는데 아무도 안 듣는" 패턴). 실측한 발행 조건은 다음 두 가지가 전부다:
    ///   · LookAround  — Idle 진입 후 wanderLookAroundDelayMin~Max(기본 1.0~2.5초) 뒤,
    ///                   그 Idle 구간이 아직 끝나지 않았으면 <b>그 구간에 정확히 1회</b>.
    ///                   ★ 2026-08-31 — 여기에 발행자 쪽 최소 간격이 하나 더 붙었다
    ///                   (StickConfig.wanderLookAroundCooldownSeconds, 기본 30초). 사용자 신고
    ///                   "너무 자주함" 대응이며 실측 분당 9.6회 -> 1.8회. <b>여전히 이 클래스에는
    ///                   확률도 타이머도 없다</b> — 조건은 전부 발행자에 있다.
    ///   · SitAndYawn  — "Idle 연장"이 연속 3회 이상 선택된 경우에만 wanderRestExtendSitChance
    ///                   (기본 0.15) 확률로.
    /// 이 클래스는 <b>그 조건에 아무것도 더하지 않는다</b> — 새 확률도, 새 타이머도, 새 상태도 없다.
    /// 하는 일은 "신호를 포즈 레이어로 넘기는 것" 한 줄뿐이다.
    ///
    /// 왜 렌더러가 포즈를 직접 만들지 않고 블랙보드에 넘기는가: 포즈의 단일 진실 공급원은
    /// StickmanBlackboard.TickPose()이고(그 메서드 문서: "상태 ID 하나로 포즈가 결정된다"), 여기서
    /// Transform을 직접 건드리면 다음 프레임에 TickPose가 그 위에 중립 포즈를 덧씌워 연출이 통째로
    /// 사라진다 — 무릎앉아/활쏘기가 각각 자기 분기를 TickPose에 두는 것과 정확히 같은 이유다.
    ///
    /// 왜 StickmanAgent에 직접 구독을 넣지 않았는가: 같은 라운드에 다른 에이전트들이 병행 작업 중이라
    /// 공유 파일 편집을 최소화했고, 기능 하나 = 컴포넌트 하나라는 이 프로젝트의 배치 규약
    /// (SceneBootstrapper가 렌더러/디렉터를 캐릭터 루트에 붙인다)에도 그대로 맞는다.
    /// </summary>
    public sealed class IdleAmbientMotionRenderer : MonoBehaviour
    {
        /// <summary>같은 GameObject의 StickmanAgent만 쓴다 — 사본이 플레이어의 신호를 받아
        /// 자기 포즈를 바꾸는 사고를 원천 차단한다(GraffitiRenderer와 같은 규약).</summary>
        private StickmanAgent _agent;

        /// <summary>테스트/진단용 — 마지막으로 수신한 신호.</summary>
        public WanderAmbientMotion LastRequestedMotion { get; private set; }

        /// <summary>테스트/진단용 — 마지막 신호가 실제로 재생으로 이어졌는지(Idle이 아니었거나 스위치가
        /// 꺼져 있으면 false).</summary>
        public bool LastRequestAccepted { get; private set; }

        private void Awake() => _agent = GetComponent<StickmanAgent>();

        private void OnEnable() => StickmanEventBus.WanderAmbientMotionRequested += OnAmbientMotionRequested;

        private void OnDisable()
        {
            StickmanEventBus.WanderAmbientMotionRequested -= OnAmbientMotionRequested;
            // 컴포넌트가 꺼질 때 진행 중이던 동작이 멈춘 자세로 굳지 않게 한다(다음 프레임의
            // ApplyIdlePose가 중립으로 되돌린다).
            // Unity Object에 ?. 를 쓰면 파괴된 오브젝트의 수명 검사를 건너뛰므로 명시적으로 비교한다.
            if (_agent != null && _agent.Blackboard != null) _agent.Blackboard.CancelIdleAmbientMotion();
        }

        private void OnAmbientMotionRequested(WanderAmbientMotion motion)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            var blackboard = _agent.Blackboard;
            if (blackboard == null) return;

            LastRequestedMotion = motion;
            LastRequestAccepted = blackboard.BeginIdleAmbientMotion(motion);
            if (!LastRequestAccepted) return;

            // 로그 문구는 확정된 상태에서만 파생한다(불변 원칙 1). "무수정"이라고 쓰던 예전 문구는
            // 2026-08-31에 발행자 쪽 최소 간격이 생기면서 사실이 아니게 됐으므로 함께 고쳤다 —
            // 진단 로그가 코드보다 낡으면 다음 사람이 그 문구를 믿고 엉뚱한 데를 판다.
            //
            // ★ 2026-09-01 스파이크 라운드 — 상시 로그 감량. 실측 Player.log 71.5분 세션에서 이 한 줄이
            // **2,564줄 중 661줄(26%)**을 차지했고, 661줄이 전부 같은 문장이었다("주위 살피기 재생 —
            // 상태=Idle"). 스위치 판정은 조립 **전에** 한다 — 뒤에 두면 꺼져 있어도 보간 문자열이
            // 만들어져 24시간 상주 앱의 GC 압박 금지 컨벤션과 충돌한다.
            // (Platform/PlayerLogPolicy.RoutineNarrationEnabled = StickConfig.verboseDiagnosticsLogging)
            // ★★ 2026-09-02 R2-4 — 접기 판정은 **로그 스위치와 무관하게 항상** 돈다.
            //   직전 라운드에는 스위치 조기 반환이 접기보다 앞에 있어서 접기 코드가 **한 번도
            //   실행되지 않았다**(실측: 진단 로그를 켜고 3분 대기 -> [유휴동작] 0줄 / 접음 0줄).
            //   조립은 여전히 스위치 뒤다 — 접기가 **키**로 판정하므로 할당이 필요 없기 때문이다.
            var machine = blackboard.Machine;
            IdleLogDecision decision = DecideIdleLog(
                ref _logFolder, ref _narrationWasEnabled,
                Platform.PlayerLogPolicy.RoutineNarrationEnabled,
                Describe(motion),
                machine != null ? (int)machine.CurrentStateId : NoStateKey,
                blackboard.IdleAmbientDurationSeconds,
                Time.realtimeSinceStartupAsDouble, IdleLogFoldHoldSeconds);

            if (decision.FoldedRepeats > 0)
            {
                Debug.Log(Platform.RepeatedLogFolder.Describe(LogTag, decision.FoldedRepeats));
            }
            if (!decision.EmitLine) return;

            Debug.Log($"[유휴동작] {Describe(motion)} 재생 — " +
                $"진행 중 상태={machine?.CurrentStateId}, " +
                $"{blackboard.IdleAmbientDurationSeconds:F2}초. " +
                "(트리거는 26-3 그대로 + 발행자 최소 간격, 이 구독자에는 새 확률 0개)");
        }

        /// <summary>이번 이벤트에서 무엇을 찍을지에 대한 결정. 값 타입이라 할당이 없다.</summary>
        internal struct IdleLogDecision
        {
            /// <summary>완성된 <c>[유휴동작]</c> 줄을 찍어야 하는가.</summary>
            public bool EmitLine;

            /// <summary>0보다 크면 그 횟수로 접힘 요약 한 줄을 <b>먼저</b> 찍는다.</summary>
            public int FoldedRepeats;
        }

        /// <summary>
        /// **접기 + 로그 스위치 판정을 한곳에 모은 결정 함수.** Unity 객체를 만지지 않으므로
        /// EditMode가 이 함수를 직접 몰아 <b>"접기가 실제로 도는가"</b>를 확인할 수 있다.
        ///
        /// <para>★ 이 함수가 따로 있는 이유는 2026-09-02 검증 R2-4다. 그때는 배선이
        /// <c>if (!스위치) return;</c> 뒤에 접기를 두는 형태였고, 스위치가 부팅 스냅샷이라
        /// <b>접기가 도달 불가</b>였다. 그런데 당시의 검사는 소스 문자열 순서만 봤기 때문에
        /// <b>도달 불가 경로를 초록으로 통과시켰다</b>. 구조 검사만으로는 같은 실수를 또 놓친다 —
        /// 그래서 실제로 돌려 보는 검사가 붙을 수 있게 순수 함수로 떼어냈다.</para>
        ///
        /// <para><b>스위치가 꺼져 있어도 접기는 돈다</b>(반복 횟수는 로그 설정과 무관한 사실이다).
        /// 다만 스위치가 넘나든 순간에는 접기 상태를 비운다 — 그러지 않으면 로그를 켜자마자
        /// "직전과 동일 5,000회 반복"처럼 <b>아무도 본 적 없는 줄</b>의 횟수가 튀어나온다.</para>
        /// </summary>
        internal static IdleLogDecision DecideIdleLog(
            ref Platform.RepeatedLogFolder folder, ref bool narrationWasEnabled, bool narrationEnabled,
            string motionName, int stateKey, float seconds, double now, double holdSeconds)
        {
            if (narrationEnabled != narrationWasEnabled)
            {
                narrationWasEnabled = narrationEnabled;
                folder.Reset();
            }

            bool emit = folder.ShouldEmit(motionName, stateKey, seconds, now, holdSeconds, out int folded);

            if (!narrationEnabled) return default;
            return new IdleLogDecision { EmitLine = emit, FoldedRepeats = folded };
        }

        /// <summary>접힌 채로 침묵하는 최대 시간(초). 이보다 길어지면 "몇 번 반복 중"을 한 줄로 낸다 —
        /// 로그만 보고 있는 사람이 "멈췄나?"라고 오해하지 않게 하는 최소한의 생존 신호다.</summary>
        private const double IdleLogFoldHoldSeconds = 60.0;

        private const string LogTag = "[유휴동작]";

        /// <summary>상태 기계가 아직 없을 때의 상태 키. 실제 열거값과 겹치지 않는 음수를 쓴다.</summary>
        private const int NoStateKey = -1;

        private Platform.RepeatedLogFolder _logFolder;
        private bool _narrationWasEnabled = true;

        /// <summary>로그 문구는 <b>확정된 신호 값에서만</b> 파생한다(불변 원칙 1의 텍스트-액션 싱크 규약을
        /// 진단 로그에도 그대로 적용 — 문구를 먼저 정하고 동작을 끼워 맞추지 않는다).</summary>
        private static string Describe(WanderAmbientMotion motion)
            => motion == WanderAmbientMotion.SitAndYawn ? "기지개" : "주위 살피기";
    }
}
