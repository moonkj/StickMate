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
            if (!Platform.PlayerLogPolicy.RoutineNarrationEnabled) return;

            string line = $"[유휴동작] {Describe(motion)} 재생 — " +
                $"진행 중 상태={blackboard.Machine?.CurrentStateId}, " +
                $"{blackboard.IdleAmbientDurationSeconds:F2}초. " +
                "(트리거는 26-3 그대로 + 발행자 최소 간격, 이 구독자에는 새 확률 0개)";

            // ★ 2026-09-02 로그 감량 — 연속 동일 줄 접기. 실측에서 이 태그가 71.5분 세션의 26%를
            //   차지했고 661/662줄이 **글자 하나 다르지 않았다**. 접어도 횟수는 보존되므로
            //   불변 원칙 1(행동-텍스트 싱크) 원격 검증에 필요한 정보는 하나도 잃지 않는다.
            //   판정 규칙 자체는 Platform/RepeatedLogFolder.cs(순수 정책, EditMode에서 검증)에 있다.
            double now = Time.realtimeSinceStartupAsDouble;
            bool emit = _logFolder.ShouldEmit(line, now, IdleLogFoldHoldSeconds, out int folded);
            if (folded > 0) Debug.Log(Platform.RepeatedLogFolder.Describe(LogTag, folded));
            if (emit) Debug.Log(line);
        }

        /// <summary>접힌 채로 침묵하는 최대 시간(초). 이보다 길어지면 "몇 번 반복 중"을 한 줄로 낸다 —
        /// 로그만 보고 있는 사람이 "멈췄나?"라고 오해하지 않게 하는 최소한의 생존 신호다.</summary>
        private const double IdleLogFoldHoldSeconds = 60.0;

        private const string LogTag = "[유휴동작]";

        private Platform.RepeatedLogFolder _logFolder;

        /// <summary>로그 문구는 <b>확정된 신호 값에서만</b> 파생한다(불변 원칙 1의 텍스트-액션 싱크 규약을
        /// 진단 로그에도 그대로 적용 — 문구를 먼저 정하고 동작을 끼워 맞추지 않는다).</summary>
        private static string Describe(WanderAmbientMotion motion)
            => motion == WanderAmbientMotion.SitAndYawn ? "기지개" : "주위 살피기";
    }
}
