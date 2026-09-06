using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Platform;

namespace StickMate.States
{
    /// <summary>
    /// ★★ 음악 반응 춤 — <b>에피소드 1회분</b>의 능동 상태(2026-09-06).
    ///
    /// <para>사용자 확정 2026-09-03: <i>"소리시스템을 전체빼줘, 다만 시스템에서 노래가 나오면 상호
    /// 반응해서 춤추는 동작을 넣어줘"</i> / <i>"집중모드일때는 춤모션이 아닌 집중모드 모션이 우선이야"</i>.</para>
    ///
    /// 자세·박자 정본은 <c>docs/UX_MOTION_DANCE.md</c>(design-motion, 2026-09-06)이고, 그 문서가
    /// <c>docs/MOTION_SPEC.md</c> 26절을 계승하며 <b>산술 오류 4건을 고쳤다</b> — 두 문서가 다르면
    /// <b>UX_MOTION_DANCE.md가 이긴다</b>.
    ///
    /// ============================================================================
    /// ★★★ 이 상태는 「음악이 나오는 동안」 잡혀 있지 않다 — 그것이 이 기능의 최대 함정이다
    /// ============================================================================
    /// <code>
    ///   1층 창       Platform/AudioReactiveDanceDirector.IsDanceGateOpen   "춤춰도 된다"(몇 시간 지속)
    ///   2층 에피소드  Interaction/DanceEpisodeDirector                      ChangeState의 유일한 소유자
    ///   3층 자세      이 파일 + StickmanPoseAnimator.ApplyDancePose         각도와 박자
    /// </code>
    /// 1층 신호를 <c>ChangeState</c>에 직결하면 <b>음악이 나오는 몇 시간 내내</b> 이 상태가 단일 상태
    /// 슬롯과 <see cref="SpectacleEventKind.Dance"/> 락을 붙들어 활쏘기·그라피티·창 도둑·청소부·
    /// 블랙홀·창 크래시·투두·SULKY·가출·집중 포즈가 <b>전부 발동 불가</b>가 된다. 그래서 한 번의
    /// 진입은 <b>8.16~15.96초</b>짜리 에피소드 하나뿐이고, 그 뒤 상태와 락을 <b>놓고</b> 휴지로 간다.
    ///
    /// ============================================================================
    /// 왜 TimedSpectacleState를 재사용하지 않는가
    /// ============================================================================
    /// ArcheryState와 같은 이유다. TimedSpectacleState는 "캐릭터 쪽 부수 효과가 전혀 없는 순수
    /// 타이머"인데, 이 상태는 (1) 매 프레임 포즈를 직접 구동하고, (2) 진입 브레이크 → 진입 박자 →
    /// 루프×N → 퇴장 박자의 페이즈 머신이며, (3) 수평 속도와 방향 부호를 스스로 소유한다.
    ///
    /// ============================================================================
    /// 상태 소유권 계약 (docs/UX_MOTION_DANCE.md 5-4 — 빠뜨리면 미끄러진다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b><c>IsGroundKeepingSelfManaged</c>에는 넣지 않았다.</b> "공중 구간이 있으니 넣어야
    ///     할 것 같다"는 직관이 <b>정확히 틀린 방향</b>이다 — 스타점프의 도약은 <c>SetBodyOffset</c>
    ///     (시각 전용)이고, 넣는 순간 <c>ApplyGroundedGravitySuppression</c> 제외 목록에도 들어가
    ///     <b>Dock 위에서 자유낙하 → 랙돌</b>이 된다.</item>
    ///   <item><b><c>IsHorizontalMotionSelfManaged</c>에는 넣었다.</b> 대가로 제자리 박자에서는
    ///     <b>매 프레임</b> 스스로 수평 속도를 죽인다(<see cref="DampHorizontal"/>).</item>
    ///   <item><b><c>IsFacingSelfManaged</c>에도 넣었다.</b> 안 넣으면 피루엣의 회전이 통째로
    ///     사라지고 문워크가 그냥 뒷걸음질이 된다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 대사 — 에피소드 진입 <b>1회</b>, 그것도 창(window)의 <b>첫</b> 에피소드에서만
    /// ============================================================================
    /// 문안은 <c>design-narrative</c>가 확정했다(2026-09-06): <c>"저절로 춤이 나와"</c>.
    /// ★ 그 리터럴은 <see cref="TryRaiseEntryDialogue"/>의 <c>DialogueLine.Say(</c> <b>괄호 안에
    /// 인라인으로</b> 있어야 한다(<c>const</c>로 빼면 대사 수집기가 구조적으로 못 본다 —
    /// 그 함수의 문서에 사유가 있다). 그래서 여기서는 <c>cref</c>가 아니라 문자열을 그대로 적는다.
    /// 곡명·가수·가사·장르·박자·피로를 <b>하나도</b> 언급하지 않는다 — 앱은 무슨 곡인지 모르고
    /// (소리만 감지한다), BPM을 모르므로 "박자에 맞춰"류는 화면과 갈라지며, 피로는 휴지 길이로만
    /// 나타나므로 대사로 말하면 <b>화면이 보여 주지 않는 시점</b>에 말하게 된다.
    ///
    /// <para>★ <b>왜 «에피소드마다»가 아니라 «창의 첫 에피소드»인가</b>(리더 결정 2026-09-06):
    /// 에피소드마다 발화하면 3시간 감상 세션에서 같은 문장이 <b>102번</b> 뜬다. 피로 램프가 이미
    /// 세고 있는 에피소드 번호를 그대로 써서 <c>n == 1</c>로 좁히면 3시간에 1회가 된다.
    /// "최대 1회"였던 규칙을 더 좁히는 것이라 원칙 1 위반이 아니다.</para>
    ///
    /// <para>★ <b>대사 리터럴을 <c>Dialogue/</c> 폴더가 아니라 여기 두는 이유</b>:
    /// <c>Tests/EditMode/DialogueCorpus.ScanAll()</c>과 <c>docs/localization/verify/golden_gen.py</c>는
    /// <c>States/**/*.cs</c>를 <b>전량</b> 훑지만 <c>Dialogue/</c>는 <b>파일 이름이 하드코딩</b>돼 있다.
    /// 새 파일로 두면 프로덕션엔 대사가 있는데 골든·말뭉치 양쪽이 못 봐서 <b>조용히 통과</b>한다 —
    /// <c>GrabReactionLines</c>에서 이미 한 번 당한 형태다.</para>
    ///
    /// <para>휴지 중에는 <b>절대</b> 대사를 띄우지 않는다. 그 순간의 사실은 «춤추는 중»이 아니라
    /// «서 있는 중»이기 때문이다(원칙 1). 종류가 <see cref="DialogueKind.Narrative"/>인 것도 그래서다 —
    /// 상태가 끝나면 가독예산을 무시하고 <b>즉시 컷</b>되므로 휴지 구간으로 샐 수 없다.</para>
    /// </summary>
    public sealed class DanceState : IStickmanState
    {
        /// <summary>사용자가 Player.log에서 찾을 태그. 1층(감지)은 <c>[음악춤]</c>이라 <b>둘 다</b>
        /// grep해야 «감지가 안 된 것»과 «연출이 안 나온 것»이 갈린다.</summary>
        public const string LogTag = "[춤]";

        private readonly StickmanBlackboard _blackboard;

        /// <summary>진입 브레이크 → 진입 박자 → 루프×N → 퇴장 박자. D1/D2는 세트/사이클 안에 이미
        /// 준비·마무리 박자가 있어 <b>Intro/Outro 길이가 0</b>이다(밖에 또 더하면 준비 자세를 두 번 한다).</summary>
        private enum Phase { EntryBrake, Intro, Loop, Outro, ForcedRelease }

        private StickConfig _cfg;
        private StickmanPoseAnimator.DancePoseSettings _dance;

        private Phase _phase;
        private float _phaseTimer;
        private float _elapsed;

        private int _moveIndex;
        private float _introSeconds;
        private float _outroSeconds;

        private float _baseLoopSeconds;
        private float _loopSeconds;      // 이번 루프의 실제 길이(박자 흔들림이 곱해진 값).
        private float _loopElapsed;
        private int _loopIndex;
        private int _loopTarget;
        private float _amplitudeScale = 1f;

        private float _turnRemaining;    // > 0이면 문워크의 방향 전환 중(그동안 활강하지 않는다).
        private bool _turnFlipped;

        // D1/D2 전용 — 박자 표의 «실제» 길이(초). 캐논 표는 StickmanPoseAnimator가 갖고 있고
        // 여기서는 배율/설정에 맞춰 실제 길이만 다시 잡는다(표를 두 벌로 만들지 않는다).
        private readonly float[] _beatSeconds = new float[8];
        private int _beatCount;
        private int _beatIndex;
        private float _beatElapsed;

        private int _pirouetteFlips;     // 회전 중 지금까지 뒤집은 횟수(2바퀴 = 4회).

        private bool _exitRequested;     // 정상 퇴장 요청(음악 종료 / 에피소드 만료 / T₄ 고착 상한).
        private float _exitRequestedElapsed;
        private float _forcedReleaseSeconds;

        // ==================== 진단/테스트용 관찰 창구 ====================

        /// <summary>이번 에피소드에서 출고 있는 동작의 <c>DanceIds.CreateAll()</c> 인덱스.
        /// ★ <b>직렬화하지 마라</b> — 신원의 정본은 문자열 아이디다.</summary>
        public int MoveIndex => _moveIndex;

        /// <summary>이번 에피소드에 <b>계획된</b> 총 길이(초, 진입 브레이크 포함). 대사 발화 자격
        /// 게이트(<c>DialogueIntent.TryCreate</c>)에 넘기는 «계획 잔여 체류»가 이 값이다.</summary>
        public float PlannedEpisodeSeconds { get; private set; }

        /// <summary>지금 이 상태에 머문 시간(초).</summary>
        public float ElapsedSeconds => _elapsed;

        /// <summary>이번 에피소드의 <b>흔들림 없는</b> 루프 길이(초). D1은 세트 2.86, D2는 사이클
        /// (배율에 따라 2.26~2.62), 나머지는 그 동작의 루프다.</summary>
        public float LoopSeconds => _baseLoopSeconds;

        /// <summary>퇴장 박자 길이(초). D1/D2는 세트/사이클 안에 마무리 박자가 있어 <b>0</b>이다.</summary>
        public float OutroSeconds => _outroSeconds;

        /// <summary>이번 에피소드에 돌기로 한 루프 수.</summary>
        public int LoopTarget => _loopTarget;

        /// <summary>정상 퇴장이 요청된 뒤 흐른 시간(초). <c>danceGracefulExitBudgetSeconds</c> 감시견의 입력.</summary>
        public float GracefulExitElapsedSeconds => _exitRequested ? _exitRequestedElapsed : 0f;

        /// <summary>정상 퇴장이 요청됐는가(요청만으로는 나가지 않는다 — 박자 경계를 기다린다).</summary>
        public bool IsGracefulExitRequested => _exitRequested;

        /// <summary>이번 프레임에 포즈로 넘어간 포락선(진입/퇴장 램프 × 진폭 흔들림). 실측 검증용.</summary>
        public float CurrentEnvelope01 { get; private set; }

        public DanceState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Dance;

        // ============================================================================
        // 진입
        // ============================================================================

        public void Enter(StateTransitionContext context)
        {
            _cfg = _blackboard.Config;
            _dance = _blackboard.BuildDancePoseSettings();

            // ★ 문자열 → 인덱스 해석은 **여기 한 번뿐**이다(docs/UX_MOTION_DANCE.md 9-2 함정 #1).
            _moveIndex = ResolveMoveIndex(_blackboard.DanceId);

            _phase = Phase.EntryBrake;
            _phaseTimer = 0f;
            _elapsed = 0f;
            _loopIndex = 0;
            _loopElapsed = 0f;
            _beatIndex = 0;
            _beatElapsed = 0f;
            _pirouetteFlips = 0;
            _turnRemaining = 0f;
            _turnFlipped = false;
            _exitRequested = false;
            _exitRequestedElapsed = 0f;
            _forcedReleaseSeconds = 0f;
            CurrentEnvelope01 = 0f;

            _introSeconds = ResolveIntroSeconds();
            _outroSeconds = _introSeconds;              // 퇴장은 진입과 대칭(목표만 반대).
            _baseLoopSeconds = ResolveBaseLoopSeconds();   // 안에서 RebuildBeatSeconds()가 _beatCount까지 잡는다.
            _loopTarget = ResolveLoopTarget();
            BeginLoopTiming();

            PlannedEpisodeSeconds = EntryBrakeSeconds + _introSeconds + _outroSeconds
                + _loopTarget * _baseLoopSeconds + TurnCount(_loopTarget) * TurnSeconds();

            // 방향은 이 상태가 소유한다(IsFacingSelfManaged). 들고 온 부호를 그대로 못박아 두면
            // 피루엣의 반전과 문워크의 전환이 «지금 보고 있는 쪽»에서 시작한다.
            _blackboard.SetFacingSign(_blackboard.FacingSign);

            // 보행 위상은 초기화하지 않는다 — Walk에서 들어오면 러닝맨의 다리가 이음매 없이 이어진다
            // (GroundLossHangState가 같은 이유로 ResetWalkPhase를 부르지 않는다).

            TryRaiseEntryDialogue(context);

            Debug.Log($"{LogTag} 에피소드 시작 — {_blackboard.DanceId}(인덱스 {_moveIndex}), " +
                $"창 안 {_blackboard.DanceEpisodeIndex}번째. 계획 {PlannedEpisodeSeconds:F2}초 " +
                $"(진입 브레이크 {EntryBrakeSeconds:F2} + 진입 박자 {_introSeconds:F2} + 루프 " +
                $"{_baseLoopSeconds:F2}초 x {_loopTarget}회 + 퇴장 박자 {_outroSeconds:F2}). " +
                $"하드캡 {HardCapSeconds:F1}초. 음악이 멈추면 박자를 끝내고 나갑니다" +
                $"(예산 {GracefulBudgetSeconds:F2}초).");
        }

        /// <summary>
        /// 문자열 아이디 → <c>DanceIds.CreateAll()</c> 인덱스. 모르는 아이디는 <b>0(피루엣)</b>으로
        /// 떨어진다 — 기본 무료 2종 중 제자리 동작이라 어떤 발판에서도 안전하고, «아무 것도 안 하고
        /// 중립으로 굳는» 실패보다 낫다(그 실패는 «음악이 안 나온다»와 겉보기가 같아 오진을 부른다).
        /// </summary>
        public static int ResolveMoveIndex(string danceId)
        {
            string[] all = DanceIds.CreateAll();
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i], danceId, System.StringComparison.Ordinal)) return i;
            }
            return StickmanPoseAnimator.DanceMovePirouette;
        }

        /// <summary>
        /// 진입 대사 — <b>창의 첫 에피소드에서만</b>. 자격 게이트에 넘기는 «계획 잔여 체류»는
        /// 지어낸 값이 아니라 바로 위에서 확정한 <see cref="PlannedEpisodeSeconds"/>다
        /// (최단 에피소드 8.44초 ≫ 필요체류 0.28 + 0.955 = 1.235초라 잘릴 위험이 0이다).
        ///
        /// <para>★★ <b>문자열을 상수로 빼지 마라.</b> 대사 골든/말뭉치 수집기
        /// (<c>DialogueCorpus.ExtractSayReact</c> · <c>docs/localization/verify/golden_gen.py</c>)의
        /// 정규식은 <c>DialogueLine.Say(</c> <b>괄호 안의 인라인 리터럴</b>만 본다. <c>const</c>를
        /// 거치면 <b>구조적으로 못 본다</b> — 그러면 골든을 정상 절차로 재생성하는 날 이 줄이 조용히
        /// 빠지고, 화면에는 뜨는데 어떤 회귀 검사에도 닿지 않는 대사가 된다(2026-09-06 실제로 한 번
        /// 이 형태로 들어갔다가 되돌렸다. 수집기를 고치는 것이 아니라 <b>호출부를 인라인으로 두는 것</b>이
        /// 이 저장소의 규약이다).</para>
        /// </summary>
        private void TryRaiseEntryDialogue(StateTransitionContext context)
        {
            if (context == null) return;
            if (_blackboard.DanceEpisodeIndex > 1) return;   // 리더 결정: 창당 1회.

            // 문안은 design-narrative 확정(2026-09-06). 9글자, 가독예산 0.955초.
            // 곡명·가수·가사·장르·박자·«피로»를 하나도 언급하지 않는다 — 앱은 무슨 곡인지 모르고,
            // BPM을 모르므로 "박자에 맞춰"류는 화면과 갈라지며, 피로는 휴지 길이로만 나타난다.
            // ★ 청각 어휘를 아예 쓰지 않은 이유: macOS의 «스트림 개방» 신호는 무음 스트림을 여는 앱
            //   하나만 상주해도 참이 되므로 **소리 없이 춤추는 화면이 실제로 가능하다**.
            DialogueIntent.TryCreate(context, (id, _) => DialogueLine.Say("저절로 춤이 나와"),
                PlannedEpisodeSeconds);
        }

        // ============================================================================
        // 매 프레임
        // ============================================================================

        public void Tick(float deltaTime)
        {
            if (_blackboard == null || _blackboard.Machine == null) return;

            // 연출 중에도 발판은 계속 확인한다 — 이 프로젝트의 발판은 실제 타 앱 창이라 몇 초 사이에
            // 닫히거나 움직일 수 있다(ArcheryState/LandingCrouchState와 같은 첫 관문).
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            _elapsed += deltaTime;
            if (_exitRequested) _exitRequestedElapsed += deltaTime;

            // ★ 감시견 두 개. 여기 걸렸다는 것은 **버그가 있다는 뜻**이므로 조용히 넘어가지 않는다.
            //   상태에서 빠질 때 락도 함께 풀려야 하는데, 그 정리는 에피소드 감독이
            //   StateTransitioned 구독으로 한다(SpectacleEventLock을 든 채 상태만 빠지는 것이
            //   이 저장소가 가장 자주 낸 사고 형태라, 두 줄을 반드시 짝으로 둔다).
            if (_elapsed >= HardCapSeconds)
            {
                Debug.LogWarning($"{LogTag} ★ 하드캡 발동 — 에피소드가 {_elapsed:F2}초(상한 " +
                    $"{HardCapSeconds:F1}초)를 넘겼습니다. 정상 최악은 16.24초이므로 이 줄이 보이면 " +
                    "박자 진행이 어딘가에서 멎었다는 뜻입니다(연출값이 아니라 감시견입니다). " +
                    "강제로 Idle로 내보내고 스펙터클 락도 함께 풉니다.");
                _blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                return;
            }
            if (_exitRequested && _exitRequestedElapsed >= GracefulBudgetSeconds)
            {
                Debug.LogWarning($"{LogTag} ★ 정상 퇴장 예산 초과 — 요청 후 {_exitRequestedElapsed:F2}초" +
                    $"(예산 {GracefulBudgetSeconds:F2}초). 최악은 D1 피루엣 1.82초이므로 이 줄이 보이면 " +
                    "퇴장 박자 경계 판정이 어긋났다는 뜻입니다. 강제로 Idle로 내보냅니다.");
                _blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                return;
            }

            switch (_phase)
            {
                case Phase.EntryBrake: TickEntryBrake(deltaTime); break;
                case Phase.Intro: TickIntro(deltaTime); break;
                case Phase.Loop: TickLoop(deltaTime); break;
                case Phase.Outro: TickOutro(deltaTime); break;
                default: TickForcedRelease(deltaTime); break;
            }
        }

        /// <summary>Walk에서 들고 온 수평 속도를 죽이는 구간. Idle에서 들어왔으면 이미 0이라
        /// 사실상 즉시 지나간다. 자세는 아직 중립이다(포락선 0).</summary>
        private void TickEntryBrake(float deltaTime)
        {
            DampHorizontal(deltaTime);
            ApplyPose(deltaTime, 0f, 0f);

            _phaseTimer += deltaTime;
            if (_phaseTimer < EntryBrakeSeconds) return;
            _phaseTimer = 0f;
            _phase = _introSeconds > 0.0001f ? Phase.Intro : Phase.Loop;
            if (_phase == Phase.Loop) BeginLoopTiming();
        }

        /// <summary>중립 → 그 춤의 첫 루프 경계 자세. 반(half) 루프 격자에 올림 붙인 길이라
        /// <b>첫 루프가 박에 맞게 시작한다</b>(그냥 루프 첫 프레임으로 갈아타면 «툭» 튄다).</summary>
        private void TickIntro(float deltaTime)
        {
            DampHorizontal(deltaTime);
            _phaseTimer += deltaTime;
            float t = _introSeconds > 0.0001f ? Mathf.Clamp01(_phaseTimer / _introSeconds) : 1f;
            ApplyPose(deltaTime, 0f, t);

            if (_phaseTimer < _introSeconds) return;
            _phaseTimer = 0f;
            _phase = Phase.Loop;
            BeginLoopTiming();
        }

        private void TickLoop(float deltaTime)
        {
            // 문워크의 방향 전환 — 4루프마다 끼는 제자리 스텝이다. 그동안은 활강하지 않는다.
            if (_turnRemaining > 0f)
            {
                _turnRemaining -= deltaTime;
                DampHorizontal(deltaTime);
                if (!_turnFlipped && _turnRemaining <= TurnSeconds() * 0.5f)
                {
                    _turnFlipped = true;
                    _blackboard.SetFacingSign(-_blackboard.FacingSign);
                }
                ApplyPose(deltaTime, 0f, 1f);
                if (_turnRemaining > 0f) return;
                BeginLoopTiming();
                return;
            }

            float previous = LoopPhase01();
            _loopElapsed += deltaTime;
            TickBeatCursor(deltaTime);
            float phase = LoopPhase01();

            DriveHorizontal(deltaTime);
            DrivePirouetteFacing();
            ApplyPose(deltaTime, phase, 1f);

            // ★ 정상 퇴장은 «지금 당장»이 아니라 **다음 박자 경계**에서 나간다. 루프 중간에 끊으면
            //   팔다리가 임의 각도에서 멎는다 — 특히 피루엣은 반쯤 돌아간 몸이 남는다.
            if (_exitRequested && CrossedGracefulBoundary(previous, phase))
            {
                BeginOutro("정상 퇴장(박자 경계)");
                return;
            }

            if (_loopElapsed < _loopSeconds) return;

            _loopElapsed -= _loopSeconds;
            _loopIndex++;
            _beatIndex = 0;
            _beatElapsed = 0f;
            _pirouetteFlips = 0;

            if (_loopIndex >= _loopTarget)
            {
                BeginOutro("에피소드 만료");
                return;
            }

            // 문워크만 4루프마다 방향을 뒤집는다(왕복 폭이 곧 발판 요구 1.60 H다).
            if (_moveIndex == StickmanPoseAnimator.DanceMoveMoonwalk && _loopIndex % 4 == 0)
            {
                _turnRemaining = TurnSeconds();
                _turnFlipped = false;
                return;
            }

            BeginLoopTiming();
        }

        private void TickOutro(float deltaTime)
        {
            DampHorizontal(deltaTime);
            _phaseTimer += deltaTime;
            float t = _outroSeconds > 0.0001f ? Mathf.Clamp01(_phaseTimer / _outroSeconds) : 1f;
            ApplyPose(deltaTime, 0f, 1f - t);

            if (_phaseTimer < _outroSeconds) return;
            Debug.Log($"{LogTag} 에피소드 종료 — {_elapsed:F2}초(계획 {PlannedEpisodeSeconds:F2}초), " +
                $"{_loopIndex}루프. 상태와 스펙터클 락을 놓고 휴지로 갑니다 — 그 사이 다른 연출이 " +
                "정상적으로 발동합니다.");
            _blackboard.Machine.ChangeState(StickmanStateId.Idle);
        }

        /// <summary>억제(집중 세션 시작 / 사용자 숨김 / 전체화면 게임 감지)로 끊는 구간.
        /// 자세만 <c>danceForcedExitSeconds</c> 동안 중립으로 녹이고 곧바로 나간다 — 박자를 기다리지
        /// 않는 이유는 사용자의 의도가 «지금 당장»이기 때문이다.</summary>
        private void TickForcedRelease(float deltaTime)
        {
            DampHorizontal(deltaTime);
            _phaseTimer += deltaTime;
            float span = Mathf.Max(0.0001f, _forcedReleaseSeconds);
            ApplyPose(deltaTime, 0f, 1f - Mathf.Clamp01(_phaseTimer / span));

            if (_phaseTimer < _forcedReleaseSeconds) return;
            _blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
        }

        public void Exit()
        {
            // 되돌릴 것이 없다 — 포즈는 다음 프레임 StickmanBlackboard.TickPose()가 현재 상태 ID를
            // 보고 다시 정하고, 상체 기울임은 아무도 요청하지 않는 순간 TickBodyLean이 자동으로
            // 직립으로 되돌린다(요청형이라 취소 배관이 필요 없다 — GroundLossHangState와 같은 관례).
            _exitRequested = false;
            _forcedReleaseSeconds = 0f;
            CurrentEnvelope01 = 0f;
        }

        // ============================================================================
        // 감독(2층)이 부르는 창구
        // ============================================================================

        /// <summary>
        /// ★ <b>정상 퇴장</b> 요청 — 음악 종료(창 닫힘) · T₄ 고착 상한. 급한 일이 아니므로
        /// <b>현재 박자를 끝내고</b> 퇴장 박자를 지나 Idle로 간다.
        /// <para>이미 요청됐거나 이미 퇴장 중이면 아무 일도 하지 않는다(멱등).</para>
        /// </summary>
        public void RequestGracefulExit(string reason)
        {
            if (_exitRequested || _phase == Phase.Outro || _phase == Phase.ForcedRelease) return;
            _exitRequested = true;
            _exitRequestedElapsed = 0f;

            // ★ 스타점프의 도움닫기 중이라면 기다릴 박자가 없다 — 아직 도약도 안 했으므로
            //   진입 브레이크와 같은 시간에 즉시 취소하는 편이 정직하다.
            if (_moveIndex == StickmanPoseAnimator.DanceMoveStarJump && _phase == Phase.Loop && _beatIndex == 0)
            {
                BeginOutro($"{reason} — 도움닫기 중 즉시 취소");
                return;
            }
            if (_phase == Phase.EntryBrake || _phase == Phase.Intro)
            {
                BeginOutro($"{reason} — 아직 루프에 들어가지 않았다");
                return;
            }
            Debug.Log($"{LogTag} 정상 퇴장 요청({reason}) — 현재 박자를 끝내고 나갑니다" +
                $"(예산 {GracefulBudgetSeconds:F2}초).");
        }

        /// <summary>
        /// ★ <b>강제 퇴장</b> — 집중 세션 시작 · 사용자 숨김 · 전체화면 게임 감지. 박자를 기다리지
        /// 않고 <c>danceForcedExitSeconds</c> 크로스페이드만 두고 나간다.
        /// </summary>
        public void RequestForcedExit(string reason)
        {
            if (_phase == Phase.ForcedRelease) return;
            _phase = Phase.ForcedRelease;
            _phaseTimer = 0f;
            _forcedReleaseSeconds = Mathf.Max(0f, _cfg != null ? _cfg.danceForcedExitSeconds : 0.18f);
            Debug.Log($"{LogTag} 강제 퇴장({reason}) — {_forcedReleaseSeconds:F2}초 크로스페이드 후 " +
                "즉시 나갑니다. 박자를 기다리지 않는 이유는 사용자의 의도가 «지금 당장»이기 때문입니다.");
        }

        private void BeginOutro(string reason)
        {
            _phase = Phase.Outro;
            _phaseTimer = 0f;
            if (_outroSeconds <= 0.0001f)
            {
                Debug.Log($"{LogTag} 퇴장({reason}) — 이 동작은 세트 안에 마무리 박자가 있어 " +
                    "밖에 퇴장 박자를 더하지 않습니다.");
            }
        }

        // ============================================================================
        // 박자
        // ============================================================================

        private float EntryBrakeSeconds => Mathf.Max(0f, _cfg != null ? _cfg.danceEntryBrakeSeconds : 0.28f);
        private float HardCapSeconds => Mathf.Max(1f, _cfg != null ? _cfg.danceEpisodeHardCapSeconds : 18f);
        private float GracefulBudgetSeconds => Mathf.Max(0.1f, _cfg != null ? _cfg.danceGracefulExitBudgetSeconds : 1.9f);

        private float ResolveIntroSeconds()
        {
            switch (_moveIndex)
            {
                // D1/D2는 세트/사이클 안에 이미 준비·마무리 박자가 있다(더하면 준비 자세를 두 번 한다).
                case StickmanPoseAnimator.DanceMovePirouette:
                case StickmanPoseAnimator.DanceMoveStarJump:
                    return 0f;
                case StickmanPoseAnimator.DanceMoveMoonwalk:
                    return _cfg != null ? _cfg.danceMoonwalkIntroSeconds : 0.36f;
                case StickmanPoseAnimator.DanceMoveRunningMan:
                    return _cfg != null ? _cfg.danceRunningManIntroSeconds : 0.56f;
                case StickmanPoseAnimator.DanceMoveRobot:
                    return _cfg != null ? _cfg.danceRobotIntroSeconds : 0.2f;
                case StickmanPoseAnimator.DanceMovePrisyadka:
                    return _cfg != null ? _cfg.danceSquatKickIntroSeconds : 0.84f;
                default:
                    return _cfg != null ? _cfg.danceHorseIntroSeconds : 0.48f;
            }
        }

        private float ResolveBaseLoopSeconds()
        {
            switch (_moveIndex)
            {
                case StickmanPoseAnimator.DanceMovePirouette:
                case StickmanPoseAnimator.DanceMoveStarJump:
                {
                    RebuildBeatSeconds();
                    float sum = 0f;
                    for (int i = 0; i < _beatCount; i++) sum += _beatSeconds[i];
                    return Mathf.Max(0.05f, sum);
                }
                case StickmanPoseAnimator.DanceMoveMoonwalk:
                    return Mathf.Max(0.05f, _cfg != null ? _cfg.danceMoonwalkLoopSeconds : 0.72f);
                case StickmanPoseAnimator.DanceMoveRunningMan:
                    return Mathf.Max(0.05f, _cfg != null ? _cfg.danceRunningManLoopSeconds : 0.56f);
                case StickmanPoseAnimator.DanceMoveRobot:
                    return Mathf.Max(0.05f, (_cfg != null ? _cfg.danceRobotStepSeconds : 0.2f)
                        * StickmanPoseAnimator.RobotKeyCount);
                case StickmanPoseAnimator.DanceMovePrisyadka:
                    return Mathf.Max(0.05f, _cfg != null ? _cfg.danceSquatKickLoopSeconds : 0.84f);
                default:
                    return Mathf.Max(0.05f, _cfg != null ? _cfg.danceHorseLoopSeconds : 0.48f);
            }
        }

        /// <summary>
        /// D1/D2의 <b>실제</b> 박자 길이(초). 캐논 표(<see cref="StickmanPoseAnimator.DanceCanonicalBeatSeconds"/>)를
        /// 그대로 쓰되 두 자리만 다시 잡는다:
        /// <list type="bullet">
        ///   <item>D1 ③ 회전 = <c>dancePirouetteRevolutionSeconds × dancePirouetteRevolutions</c>.</item>
        ///   <item>D2 ① 도움닫기 = <b>거리/속도에서 파생</b>(둘 다 배율에 비례해 배율이 약분된다),
        ///     ③④⑤ 체공 = <c>ResolveDanceStarJumpAirSeconds()</c>에 캐논 비율을 곱한 값,
        ///     ⑦ 복귀+방향 전환 = <c>danceStarJumpTurnSeconds</c>.</item>
        /// </list>
        /// 나머지 박자는 캐논 그대로다 — 표를 두 벌로 만들지 않는 것이 이 함수의 존재 이유다.
        /// </summary>
        private void RebuildBeatSeconds()
        {
            _beatCount = StickmanPoseAnimator.DanceBeatCount(_moveIndex);
            if (_beatCount <= 0) return;

            float airTotalCanonical = 0f;
            for (int i = 0; i < _beatCount; i++)
            {
                if (StickmanPoseAnimator.DanceBeatScalesWithAirTime(_moveIndex, i))
                    airTotalCanonical += StickmanPoseAnimator.DanceCanonicalBeatSeconds(_moveIndex, i);
            }
            float airActual = _cfg != null ? _cfg.ResolveDanceStarJumpAirSeconds() : 0.7644f;

            for (int i = 0; i < _beatCount && i < _beatSeconds.Length; i++)
            {
                float canonical = StickmanPoseAnimator.DanceCanonicalBeatSeconds(_moveIndex, i);
                float actual = canonical;

                if (_moveIndex == StickmanPoseAnimator.DanceMovePirouette && i == 2)
                {
                    float revolution = _cfg != null ? _cfg.dancePirouetteRevolutionSeconds : 0.7f;
                    int revolutions = Mathf.Max(1, _cfg != null ? _cfg.dancePirouetteRevolutions : 2);
                    actual = Mathf.Max(0.05f, revolution) * revolutions;
                }
                else if (_moveIndex == StickmanPoseAnimator.DanceMoveStarJump)
                {
                    if (i == 0) actual = ResolveStarJumpRunSeconds();
                    else if (StickmanPoseAnimator.DanceBeatScalesWithAirTime(_moveIndex, i))
                        actual = airTotalCanonical > 0.0001f ? airActual * (canonical / airTotalCanonical) : canonical;
                    else if (i == 6) actual = Mathf.Max(0.05f, _cfg != null ? _cfg.danceStarJumpTurnSeconds : 0.34f);
                }

                _beatSeconds[i] = Mathf.Max(0.01f, actual);
            }
        }

        /// <summary>
        /// 도움닫기 시간(초) = 거리 / 속도. ★ <b>상수로 두지 마라</b> — 두 값 모두 배율에 비례하므로
        /// 배율이 약분되어 결과가 일정하고(약 1.004초), 26절의 «0.602초»는 거리에만 배율을 곱한 값이다.
        /// </summary>
        private float ResolveStarJumpRunSeconds()
        {
            float distance = _blackboard.CharacterHeightWorld
                * (_cfg != null ? _cfg.danceStarJumpRunHeights : 1.6f);
            float speed = (_cfg != null ? _cfg.ResolveWalkSpeed() : 2.5f)
                * Mathf.Max(0.05f, _cfg != null ? _cfg.danceStarJumpRunSpeedScale : 1.45f);
            return speed > 0.0001f ? Mathf.Clamp(distance / speed, 0.05f, 6f) : 1f;
        }

        private float TurnSeconds()
            => _moveIndex == StickmanPoseAnimator.DanceMoveMoonwalk
                ? Mathf.Max(0f, _cfg != null ? _cfg.danceMoonwalkTurnSeconds : 0.34f)
                : 0f;

        private int TurnCount(int loops)
            => _moveIndex == StickmanPoseAnimator.DanceMoveMoonwalk ? loops / 4 : 0;

        /// <summary>
        /// 이번 에피소드의 루프 수. <b>에피소드 길이 8/16초를 여기 숫자로 적지 않는다</b> —
        /// <see cref="AudioReactiveDancePolicy.MotionEpisodeMinSeconds"/>/<c>MaxSeconds</c>를 참조한다.
        /// 두 벌이 되는 순간 정책의 T₃(최소 유지 8.0초)와 갈라져 <b>Stop이 에피소드 한가운데를 자른다</b>
        /// (docs/UX_MOTION_DANCE.md 3-3절의 구조적 짝).
        /// </summary>
        private int ResolveLoopTarget()
        {
            float min = AudioReactiveDancePolicy.MotionEpisodeMinSeconds;
            float max = AudioReactiveDancePolicy.MotionEpisodeMaxSeconds;
            float fixedPart = _introSeconds + _outroSeconds;

            int lo = -1;
            int hi = -1;
            for (int n = 1; n <= MaxLoopSearch; n++)
            {
                float total = fixedPart + n * _baseLoopSeconds + TurnCount(n) * TurnSeconds();
                if (lo < 0 && total >= min) lo = n;
                if (total <= max) hi = n;
                if (total > max) break;
            }

            if (lo < 0) lo = 1;                 // 루프가 지나치게 길면 1회라도 돈다.
            if (hi < lo) hi = lo;               // 하한이 상한을 넘으면 하한을 따른다(잘리는 것보다 낫다).
            return Random.Range(lo, hi + 1);
        }

        /// <summary>루프 탐색 상한. 가장 짧은 루프(D7 0.48초)로 16초를 채워도 33회면 충분하다.</summary>
        private const int MaxLoopSearch = 64;

        /// <summary>
        /// 다음 루프의 길이와 진폭을 정한다 — <b>재추첨은 루프 경계에서만</b> 한다(루프 중간에 바꾸면
        /// 위상이 튄다).
        ///
        /// <para>★ 예외 2건은 지어낸 것이 아니라 그 동작의 정체성이다:
        /// <b>D5 로봇은 흔들림 0</b>(메트로놈처럼 정확한 것이 그 농담 자체이고, 흔들면 dime stop이
        /// «타이밍이 안 맞는 로봇»이 된다), <b>D1/D2는 세트 내부 박자에 걸지 않고 끝의 호흡·복귀
        /// 박자에만</b> 건다(회전·도약이 흔들리면 회전이 아니라 떨림이 된다).</para>
        /// </summary>
        private void BeginLoopTiming()
        {
            _loopElapsed = 0f;
            _beatIndex = 0;
            _beatElapsed = 0f;
            _pirouetteFlips = 0;

            float loopJitter = _cfg != null ? _cfg.danceLoopJitterFraction : 0.06f;
            float ampJitter = _cfg != null ? _cfg.danceAmplitudeJitterFraction : 0.05f;
            if (_moveIndex == StickmanPoseAnimator.DanceMoveRobot)
            {
                loopJitter = 0f;
                ampJitter = 0f;
            }

            _amplitudeScale = 1f + Random.Range(-ampJitter, ampJitter);

            if (_beatCount > 0)
            {
                // D1/D2 — 마지막 박자(호흡 / 복귀)에만 흔들림을 실어 사이클 전체 길이를 흔든다.
                RebuildBeatSeconds();
                float sum = 0f;
                for (int i = 0; i < _beatCount; i++) sum += _beatSeconds[i];
                int last = _beatCount - 1;
                float delta = _beatSeconds[last] * Random.Range(-loopJitter, loopJitter);
                _beatSeconds[last] = Mathf.Max(0.01f, _beatSeconds[last] + delta);
                _loopSeconds = Mathf.Max(0.05f, sum + delta);
                return;
            }

            _loopSeconds = Mathf.Max(0.05f, _baseLoopSeconds * (1f + Random.Range(-loopJitter, loopJitter)));
        }

        /// <summary>박자 표가 있는 동작(D1/D2)에서 지금 몇 번째 박자인지 갱신한다.</summary>
        private void TickBeatCursor(float deltaTime)
        {
            if (_beatCount <= 0) return;
            _beatElapsed += deltaTime;
            while (_beatIndex < _beatCount - 1 && _beatElapsed >= _beatSeconds[_beatIndex])
            {
                _beatElapsed -= _beatSeconds[_beatIndex];
                _beatIndex++;
                OnBeatEntered(_beatIndex);
            }
        }

        private void OnBeatEntered(int beatIndex)
        {
            // 스타점프는 매 회 진행 방향을 뒤집는다 — 안 뒤집으면 4~6회에 8~12 H를 이동해
            // 어떤 발판에서도 못 한다(⑦ 복귀 박자가 그 자리다).
            if (_moveIndex == StickmanPoseAnimator.DanceMoveStarJump && beatIndex == 6)
            {
                _blackboard.SetFacingSign(-_blackboard.FacingSign);
            }
        }

        /// <summary>
        /// 지금 루프 안의 위상(0~1). 박자 표가 있는 동작은 <b>실제 박자 길이 → 캐논 비율</b>로
        /// 조각별 선형 사상한다 — 그래야 배율에 따라 체공이 늘어나도 포즈의 박자 경계가
        /// 실제 경계와 정확히 맞는다(표를 두 벌 만들지 않고 어긋남을 없애는 방법이다).
        /// </summary>
        private float LoopPhase01()
        {
            if (_beatCount <= 0)
                return _loopSeconds > 0.0001f ? Mathf.Clamp01(_loopElapsed / _loopSeconds) : 0f;

            float canonTotal = 0f;
            for (int i = 0; i < _beatCount; i++)
                canonTotal += StickmanPoseAnimator.DanceCanonicalBeatSeconds(_moveIndex, i);
            if (canonTotal <= 0.0001f) return 0f;

            float canonStart = 0f;
            for (int i = 0; i < _beatIndex; i++)
                canonStart += StickmanPoseAnimator.DanceCanonicalBeatSeconds(_moveIndex, i);
            float canonSpan = StickmanPoseAnimator.DanceCanonicalBeatSeconds(_moveIndex, _beatIndex);

            float within = _beatSeconds[_beatIndex] > 0.0001f
                ? Mathf.Clamp01(_beatElapsed / _beatSeconds[_beatIndex])
                : 1f;
            return Mathf.Clamp01((canonStart + canonSpan * within) / canonTotal);
        }

        // ============================================================================
        // 수평 · 방향 — 이 상태가 소유한다
        // ============================================================================

        /// <summary>
        /// 제자리 박자에서 <b>매 프레임</b> 수평 속도를 죽인다. 한 번만 대입하고 끝내면 그 뒤에 들어온
        /// 외력이 그대로 남아 미끄러진다(IdleState.Enter가 그 반례다). 지수 감쇠를 쓰는 이유는
        /// <c>archeryHorizontalDamping</c>/<c>landingCrouchHorizontalDamping</c>과 같다 —
        /// 0으로 즉시 대입하면 뚝 끊겨 오히려 부자연스럽다.
        /// </summary>
        private void DampHorizontal(float deltaTime)
        {
            if (_blackboard.Body == null) return;
            float damping = _cfg != null ? _cfg.danceHorizontalDamping : 14f;
            Vector2 v = _blackboard.Body.linearVelocity;
            v.x = damping > 0f ? v.x * Mathf.Exp(-damping * deltaTime) : 0f;
            _blackboard.Body.linearVelocity = v;
        }

        /// <summary>7종 중 <b>둘만</b> 자기 속도를 싣는다 — 스타점프의 도움닫기와 문워크의 후진 활강.
        /// 나머지는 전부 위 감쇠로 제자리를 유지한다.</summary>
        private void DriveHorizontal(float deltaTime)
        {
            if (_blackboard.Body == null) return;

            if (_moveIndex == StickmanPoseAnimator.DanceMoveMoonwalk)
            {
                // ★ 앞을 보면서 **뒤로** 간다. 보행의 37.9%(2.64배 느림)라 보행과 혼동되지 않는다.
                float glide = _blackboard.CharacterHeightWorld
                    * (_cfg != null ? _cfg.danceMoonwalkGlideHeights : 0.3f)
                    / Mathf.Max(0.05f, _baseLoopSeconds);
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = -_blackboard.FacingSign * glide;
                _blackboard.Body.linearVelocity = v;
                return;
            }

            if (_moveIndex == StickmanPoseAnimator.DanceMoveStarJump)
            {
                if (_beatIndex == 0)
                {
                    Vector2 v = _blackboard.Body.linearVelocity;
                    v.x = _blackboard.FacingSign * (_cfg != null ? _cfg.ResolveWalkSpeed() : 2.5f)
                        * Mathf.Max(0.05f, _cfg != null ? _cfg.danceStarJumpRunSpeedScale : 1.45f);
                    _blackboard.Body.linearVelocity = v;
                    return;
                }
                if (_beatIndex == 1)
                {
                    // 도약 직전 브레이크 — 이 박자 동안 정확히 danceStarJumpHorizontalBrakeScale까지
                    // 줄어들도록 감쇠율을 역산한다(고정 계수를 쓰면 남는 속도가 설정과 갈라진다).
                    float scale = Mathf.Clamp(_cfg != null ? _cfg.danceStarJumpHorizontalBrakeScale : 0.25f,
                        0.01f, 0.99f);
                    float span = Mathf.Max(0.01f, _beatSeconds[1]);
                    float rate = -Mathf.Log(scale) / span;
                    Vector2 v = _blackboard.Body.linearVelocity;
                    v.x *= Mathf.Exp(-rate * deltaTime);
                    _blackboard.Body.linearVelocity = v;
                    return;
                }
                // ★ 공중(③④⑤)에서는 죽이지 않는다 — 남은 속도가 «공중 잔여 0.3045 H»이고,
                //   그것까지 포함해 발판 요구 2.70 H가 검산됐다.
                if (_beatIndex >= 2 && _beatIndex <= 4) return;
            }

            DampHorizontal(deltaTime);
        }

        /// <summary>
        /// 피루엣의 «회전» — 이 리그에는 세로 회전축이 없어서 <b>facing 부호 반전</b>으로 만든다.
        /// 반 바퀴마다 뒤집으면 retiré의 든 무릎이 앞↔뒤로 미러링되어 «돌았다»로 읽힌다.
        /// <para>2바퀴 = 4회 반전이라 세트가 끝나면 방향이 <b>원래대로 돌아온다</b>(홀수면 매 세트마다
        /// 캐릭터가 반대쪽을 보게 된다).</para>
        /// </summary>
        private void DrivePirouetteFacing()
        {
            if (_moveIndex != StickmanPoseAnimator.DanceMovePirouette) return;
            if (_beatCount <= 0 || _beatIndex != 2) return;   // ③ 회전 박자에서만.

            float half = Mathf.Max(0.02f,
                (_cfg != null ? _cfg.dancePirouetteRevolutionSeconds : 0.7f) * 0.5f);
            int wanted = Mathf.FloorToInt(_beatElapsed / half);
            int revolutions = Mathf.Max(1, _cfg != null ? _cfg.dancePirouetteRevolutions : 2);
            wanted = Mathf.Min(wanted, revolutions * 2);
            while (_pirouetteFlips < wanted)
            {
                _pirouetteFlips++;
                _blackboard.SetFacingSign(-_blackboard.FacingSign);
            }
        }

        // ============================================================================
        // 퇴장 박자 경계
        // ============================================================================

        /// <summary>
        /// 이번 프레임에 <b>정상 퇴장이 허용되는 박자 경계</b>를 지났는가.
        ///
        /// <list type="bullet">
        ///   <item><b>D1 피루엣</b> — ④(1번으로 닫고 착지)가 끝나는 지점 하나뿐이다. ⑤ 호흡은
        ///     건너뛴다. 회전 시작 직후에 요청이 오면 <b>1.82초</b>가 최악이며 그것이 예산 1.90초의 근거다.</item>
        ///   <item><b>D2 스타점프</b> — 사이클 끝. 도움닫기 중이면 <see cref="RequestGracefulExit"/>가
        ///     이미 즉시 취소로 갈랐다.</item>
        ///   <item><b>D5 로봇</b> — 8키가 각각 완결 포즈라 <b>어느 키 경계에서든</b> 나갈 수 있다
        ///     (정상 퇴장 0.40초로 7종 중 가장 빠르다. 26-6-4의 «2.02초»는 구속 동작을 잘못 짚었다).</item>
        ///   <item>나머지 — 반루프 경계.</item>
        /// </list>
        /// </summary>
        private bool CrossedGracefulBoundary(float previous, float current)
            => BoundaryOrdinal(_moveIndex, current) > BoundaryOrdinal(_moveIndex, previous);

        /// <summary>
        /// ★ 위 판정의 <b>순수 함수 본체</b> — 위상 0~1을 «지금까지 지나온 퇴장 허용 경계의 개수»로
        /// 바꾼다. 값이 늘어난 프레임이 곧 경계를 지난 프레임이다.
        ///
        /// <para>순수 함수로 뽑아 둔 이유: 정상 퇴장 최악 대기시간이 예산
        /// (<c>danceGracefulExitBudgetSeconds</c>) 안에 들어오는지는 <b>모든 위상에 대해</b> 확인해야
        /// 하는데, 씬을 띄워 재면 «우연히 좋은 순간만 재는» 표본이 된다. 테스트가 이 함수를 그대로
        /// 소비해 위상을 촘촘히 훑으므로 판정이 두 벌이 되지 않는다.</para>
        /// </summary>
        internal static int BoundaryOrdinal(int moveIndex, float phase01)
        {
            float p = Mathf.Clamp01(phase01);

            if (moveIndex == StickmanPoseAnimator.DanceMovePirouette)
            {
                // ★ 구속 구간은 ③회전 + ④닫고 착지뿐이다. 그 앞(①준비 plié·②상승)은 아직 아무 것도
                //   시작하지 않았으므로 그대로 접어도 되고, 그 뒤(⑤ 세트 간 호흡)는 이미 중립 근처다.
                //   ⇒ 허용 경계는 «②의 끝»과 «④의 끝» 둘이고, 최악 대기는 ③ 시작 직후에 요청이 왔을 때의
                //   ③1.40 + ④0.42 = 1.82초다(= danceGracefulExitBudgetSeconds 1.90의 유도값 그 자체).
                //   ★ ④의 끝 하나만 두면 ⑤ 도중에 온 요청이 다음 세트를 통째로 기다려 2.86초가 된다.
                return (p >= StickmanPoseAnimator.BeatEdge(moveIndex, 2) ? 1 : 0)
                     + (p >= StickmanPoseAnimator.BeatEdge(moveIndex, 4) ? 1 : 0);
            }

            if (moveIndex == StickmanPoseAnimator.DanceMoveStarJump)
            {
                // ①도움닫기는 아직 도약도 안 한 구간이라 그 끝이 곧 허용 경계다(그 안에서 온 요청은
                // RequestGracefulExit가 아예 즉시 취소로 가른다). 그 뒤로는 사이클 끝까지가 한 덩어리다 —
                // 공중에서 끊으면 캐릭터가 허공에 자세로 멎는다. 최악은 ② 시작 직후의 1.50초다.
                return (p >= StickmanPoseAnimator.BeatEdge(moveIndex, 1) ? 1 : 0)
                     + (p >= 1f - 0.0001f ? 1 : 0);
            }

            int divisions = moveIndex == StickmanPoseAnimator.DanceMoveRobot
                ? StickmanPoseAnimator.RobotKeyCount   // 8키가 각각 완결 포즈 = 어느 경계에서든 나갈 수 있다.
                : 2;                                    // 반루프.
            return Mathf.FloorToInt(p * divisions);
        }

        // ============================================================================
        // 포즈
        // ============================================================================

        /// <summary>
        /// 이번 프레임의 자세. <b>포락선에 진폭 흔들림을 곱해서</b> 넘기므로 값이 1을 살짝 넘을 수
        /// 있고, 그것이 «봉우리 진폭 ×U(0.95,1.05)»의 실체다.
        ///
        /// <para>★ 스타점프의 도움닫기 박자만 <b>보행 포즈를 그대로</b> 쓴다 — WalkState와 완전히 같은
        /// 인자로 부르므로 "춤 전용 걷기"라는 두 번째 보행 구현이 생기지 않는다(ArcheryState의
        /// 접근 페이즈가 세운 선례). 진폭은 <c>walkPoseAmplitudeScale</c> 그대로 넘긴다 —
        /// 도움닫기 속도가 보행의 1.45배라 애니메이터의 속도 연동 진폭이 알아서 상한(1.35)에 붙는다.</para>
        /// </summary>
        private void ApplyPose(float deltaTime, float phase01, float envelope01)
        {
            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            if (pose == null) return;

            if (_moveIndex == StickmanPoseAnimator.DanceMoveStarJump && _phase == Phase.Loop && _beatIndex == 0)
            {
                float speed = Mathf.Abs(_blackboard.Body != null ? _blackboard.Body.linearVelocity.x : 0f);
                pose.TickWalkPose(deltaTime, speed, _blackboard.BuildPoseSettings(),
                    _blackboard.PoseSmoothingRate, _blackboard.WalkSpeedSmoothingRate,
                    _cfg != null ? _cfg.walkFootGroundingBlend : 1f,
                    _cfg != null ? _cfg.walkPoseAmplitudeScale : 1f,
                    _cfg != null ? _cfg.walkStrideScale : 0.93f,
                    _blackboard.RunBodyLeanDegrees);
                CurrentEnvelope01 = 0f;
                return;
            }

            float env = envelope01 * _amplitudeScale;
            CurrentEnvelope01 = env;
            pose.ApplyDancePose(deltaTime, _blackboard.BuildPoseSettings(), ResolveSmoothingRate(),
                _dance, _moveIndex, phase01, env);
        }

        /// <summary>
        /// 이번 프레임의 자세 보간 계수. 기본은 <c>dancePoseSmoothingRate</c>(78)이고,
        /// <b>로봇춤의 루프 구간에서만</b> <c>danceRobotPoseSmoothingRate</c>(0)로 내려간다 —
        /// <c>SmoothTo</c>의 «rate ≤ 0이면 즉시 대입» 폴백이 그대로 dime stop이다.
        ///
        /// <para>★ 진입·퇴장 박자는 <b>예외</b>다. Idle에서 곧바로 rate = 0으로 튀면 «글리치»로
        /// 읽히므로 그 한 스텝만 78로 부드럽게 키 1에 도착시킨다(퇴장도 대칭).</para>
        /// </summary>
        private float ResolveSmoothingRate()
        {
            if (_moveIndex == StickmanPoseAnimator.DanceMoveRobot && _phase == Phase.Loop)
                return _blackboard.DanceRobotPoseSmoothingRate;
            return _blackboard.DancePoseSmoothingRate;
        }
    }
}
