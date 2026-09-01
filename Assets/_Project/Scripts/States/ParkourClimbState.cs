using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 벽 타기/모서리 매달리기 동작(아키텍처 0절, UX_FLOW.md 4절).
    ///
    /// 진입: WalkState의 Jump 판정이 "지금 딛고 있는 발판 경계 근처 + 진행방향에 그보다 눈에 띄게 높은
    /// 발판(벽)이 있음"을 감지했을 때(StickmanBlackboard.TryFindClimbableWall, UX_FLOW.md 26-2 "경계에서
    /// 점프 시도"의 자연스러운 확장 — AutoWanderController가 발생시키는 JumpRequested + 발판 경계 근접
    /// 조합이 실제 트리거다). IdleState의 "제자리 점프"는 방향 의도가 없어(MoveInputX==0) 이 판정을
    /// 의도적으로 건너뛴다(WalkState.cs 참고, UX_FLOW.md 4절 "애매하면 더 안전한 쪽을 선택" 원칙).
    ///
    /// 전이: 등반 완료 -> Idle/Walk 복귀(이동 입력 유무로 분기) / 잡을 곳이 사라짐(창 이동/닫힘) -> 즉시
    /// Fall(같은 프레임 대사도 자동 취소) / 외력 임계값 초과 -> Ragdoll(StickmanAgent.ReportExternalImpact가
    /// 상태와 무관하게 처리하는 단일 진입점, RagdollState.cs 참고).
    /// </summary>
    public sealed class ParkourClimbState : IStickmanState, IHasDialogueParams
    {
        private readonly StickmanBlackboard _blackboard;

        private int _direction;
        private bool _hasWall;
        private long _wallHandle;
        private float _wallTopWorldY;
        private float _startWorldY;
        private float _climbProgress;

        // ★ 맨틀(mantle) — 등반이 끝났을 때 **턱 위에 실제로 올라서 있게** 하는 수평 이동(2026-08-29).
        // 왜 필요했나: 이 상태는 원래 y만 보간하고 x는 손대지 않았다. 그런데 진입 조건(TryFindClimbableWall)은
        // "지금 딛고 있는 발판의 경계 근처"일 뿐이라, 등반이 끝난 캐릭터는 여전히 **아래 발판 쪽 x**에
        // 있다 — 즉 턱 위가 아니라 턱 옆 허공이다. 그러면 다음 프레임의 접지 판정이 실패해 곧바로
        // 다시 떨어진다(등반이 통째로 무효화됨). 실제로 이 경로는 wanderEdgeJumpAttemptChance 기본값이
        // 0이 되면서 아무도 밟지 않아 드러나지 않았을 뿐이고, 2026-08-29에 "뛰어내린 뒤 다시 올라오기"를
        // 붙이면서 처음으로 상시 경로가 되어 발견되었다.
        private float _startWorldX;
        private bool _hasMantleTarget;

        /// <summary>
        /// BUG-M7 파라미터 파이프라인 시연(UX_FLOW.md 31-2 #4) — 오를 거리(월드 유닛)와
        /// <b>그 프레임의 신장 H</b>.
        ///
        /// ★ 2026-09-01 — H가 파라미터에 함께 실린 이유(교차 레이어 로그 L6): 개정된 임계값이
        /// <c>0.95 × H</c> / <c>2.2 × H</c>라 매핑 함수가 신장을 알아야 하는데, 매핑 함수가
        /// 설정이나 에이전트를 직접 읽으면 31-1(하나의 Enter, 하나의 스냅샷)이 깨진다. 그래서
        /// 신장도 스냅샷의 일부로 넘긴다.
        /// </summary>
        public sealed class ParkourClimbDialogueParams
        {
            public float ClimbHeightUnits;
            /// <summary>이 전이 프레임의 캐릭터 실측 신장(월드 유닛).</summary>
            public float CharacterHeightWorld;
        }

        private readonly ParkourClimbDialogueParams _dialogueParams = new ParkourClimbDialogueParams();

        public object DialogueParams => _dialogueParams;

        public ParkourClimbState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.ParkourClimb;

        /// <summary>"가뿐하네"의 상한(신장 배수 H). Dock 단차(0.72H)는 이 아래라 가볍게 읽힌다.
        /// ★ 판단값이지 실측이 아니다 — UX_FLOW.md 31-2 #4 / MOTION_SPEC 2-7. 실기 분포 확인 후
        /// 조정될 수 있다. 각도가 아니라 거리이므로 반드시 H 배수다(31-4 C1 축 ①).</summary>
        private const float LightClimbHeights = 0.95f;

        /// <summary>"영차..."의 상한(신장 배수 H). 이 위는 창 상단 등반급 — "헉... 높다".
        /// ★ 위와 같은 이유로 판단값이며 실측 조정 대상이다.</summary>
        private const float HardClimbHeights = 2.2f;

        public void Enter(StateTransitionContext context)
        {
            _climbProgress = 0f;
            _direction = _blackboard.MoveInputX >= 0f ? 1 : -1;
            _startWorldY = _blackboard.Body != null ? _blackboard.Body.position.y : 0f;
            _startWorldX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            _hasWall = _blackboard.TryFindClimbableWall(info, _direction, out _wallHandle, out _wallTopWorldY);
            _hasMantleTarget = TryComputeMantleTargetX(out float mantleTargetX);

            if (_blackboard.Body != null)
            {
                // 매달리기 도입부: 잔여 속도를 죽여 벽에 붙은 듯 고정한다. 이 상태 동안은(능동 상태와
                // 동일하게) 캐릭터 스스로 위치를 제어하므로 중력에 의한 낙하는 발생하지 않는다.
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = 0f;
                v.y = 0f;
                _blackboard.Body.linearVelocity = v;
            }

            // BUG-M7 대응 시연(UX_FLOW.md 31-2 #4) — 벽이 실제로 감지된 경우에만 유의미한 값이므로,
            // 감지 실패(_hasWall==false) 시에는 0으로 두어 "가뿐하네" 쪽으로 안전하게 수렴시킨다(어차피
            // 다음 Tick에서 곧바로 Fall로 전이되어 이 대사는 즉시 만료된다).
            _dialogueParams.ClimbHeightUnits = _hasWall ? Mathf.Max(0f, _wallTopWorldY - _startWorldY) : 0f;
            _dialogueParams.CharacterHeightWorld = _blackboard.CharacterHeightWorld;

            // ★ 2026-09-01 개정(UX_FLOW.md 31-2 #4 / MOTION_SPEC 1절 표 #2) — 임계값을 **절대 월드
            //   유닛에서 신장 배수(H)로** 옮기고 분기를 3종으로 늘렸다.
            //
            //   구 임계값 2.0유닛은 배율 1.0에서 0.88H인데, 실제 데스크톱의 유일한 상시 단차인
            //   Dock이 0.72H라 **"헉... 높다"가 구조적으로 도달 불가**였다(실측 0/7, 전부 "가뿐하네").
            //   신장 배수로 적으면 배율 슬라이더에도, Windows 작업표시줄처럼 높이가 다른 환경에도
            //   그대로 성립한다(플랫폼 분기 없음 — 높이가 다르면 배수가 달라져 티어가 알아서 갈린다).
            //
            //   ★ 0.95 / 2.2라는 두 계수는 design-motion의 **판단값이지 실측이 아니다**(MOTION_SPEC
            //     2-7). 실기에서 창 상단 등반의 분포를 본 뒤 조정될 수 있다.
            //
            //   종류 = Narrative(진행 서술: "지금 오르고 있고 그게 이만큼 힘들다"). 그래서 등반이
            //   끝나면 즉시 컷되고, 계획 잔여 체류(= 등반 총 길이 x (1 - 진행도), Enter에서 진행도 0)가
            //   가독예산에 못 미치면 애초에 발화하지 않는다(규칙 8).
            // ★ 2026-09-02 (디버거) — 폴백 0.5는 Phase 2의 코드 기본값이 굳은 **낡은 사본**이다(세 번째 사본).
            //   코드 기본값과 배포 에셋은 0.5 -> 1.05 -> **1.20**으로 움직였는데 이 두 줄만 0.5에 남아 있었다.
            //   (1.05 -> 1.20은 이 라운드가 도는 **도중에** 또 일어났다 — 사본이 얼마나 빨리 낡는지의 실물 증거다.
            //    그래서 이 자리는 리터럴로 남겨 드리프트 스캐너의 감시 아래 둔다.)
            //   여기는 특히 위험한 자리다: 이 값이 곧 규칙 8 게이트에 넘기는 **계획 잔여 체류**이고
            //   (Enter의 _climbProgress가 0이라 곱셈이 항등), 0.5초는 이 상태의 대사 **세 갈래 전부**의
            //   필요체류보다 짧다 — 실측: 가뿐하네 0.680 / 영차... 0.715 / 헉... 높다 0.865초
            //   (= DialogueTiming.FadeInSeconds 0.06 + DialogueBudget.ReadingSeconds). 즉 낡은 폴백
            //   하나 때문에 이 상태는 **한 마디도 하지 못한다**. 규칙 8은 침묵을 정상 결과로 취급하므로
            //   로그 말고는 아무 증상이 없다 — 그래서 조용히 살아남았다.
            float climbDurationForGate = _blackboard.Config != null ? _blackboard.Config.parkourClimbDuration : 1.20f;
            _ = DialogueIntent.TryCreate(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as ParkourClimbDialogueParams;
                float height = p != null ? p.ClimbHeightUnits : 0f;
                float h = p != null && p.CharacterHeightWorld > 0.0001f
                    ? p.CharacterHeightWorld
                    : StickConfig.BaselineCharacterTotalHeight;
                if (height < LightClimbHeights * h) return DialogueLine.Say("가뿐하네");
                if (height < HardClimbHeights * h) return DialogueLine.Say("영차...");
                return DialogueLine.Say("헉... 높다");
            }, climbDurationForGate * (1f - _climbProgress));

            Debug.Log($"[벽타기] 진입 — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                $"벽핸들={_wallHandle}, 시작 월드=({_startWorldX:F3},{_startWorldY:F3}), " +
                $"벽 상단 Y={_wallTopWorldY:F3}(오를 높이 {(_hasWall ? _wallTopWorldY - _startWorldY : 0f):F3}유닛), " +
                $"올라설 X={(_hasMantleTarget ? mantleTargetX.ToString("F3") : "없음(수평 이동 생략)")}.");

            // ★ 오르는 벽을 바라본다(2026-09-01). 등반 자세는 "앞쪽"을 기준으로 팔다리를 뻗으므로
            // (StickmanPoseAnimator의 방향 중립 공간 규약), 등지고 서면 손이 뒤로 뻗는다. 활쏘기가
            // 같은 이유로 SetFacingSign을 쓰는 것과 동일하다(FacingLocked는 걸지 않는다 — 이 상태는
            // 1초 남짓이고, 배회 AI의 이동 의도도 어차피 벽 쪽을 향하고 있다).
            if (_hasWall) _blackboard.SetFacingSign(_direction);

            // TODO(Phase 2 렌더링): 손끝 마찰 먼지 파티클, 매달리기 Perlin 흔들림(UX_FLOW.md 4절).
            //   양손 그립 포즈는 2026-09-01에 들어왔다 -> StickmanPoseAnimator.ApplyParkourClimbPose.
        }

        /// <summary>
        /// 등반이 끝났을 때 서 있어야 할 x — 붙잡은 턱의 **가까운 쪽 모서리에서 안쪽으로
        /// StickConfig.parkourMantleInset만큼 들어간 지점**이다. 매 프레임 다시 계산한다(창이 옆으로
        /// 움직이면 올라설 자리도 함께 움직여야 하므로, 잡을 곳 재확인과 같은 계약).
        ///
        /// "가까운 쪽 모서리"는 진행 방향의 **반대편** 모서리다: 오른쪽으로 오르면 그 턱의 왼쪽 끝,
        /// 왼쪽으로 오르면 오른쪽 끝. 턱이 inset보다 좁으면 반대편 끝을 넘지 않도록 클램프한다.
        /// </summary>
        private bool TryComputeMantleTargetX(out float targetX) => TryComputeMantleTargetX(out targetX, out _);

        /// <summary>위 오버로드에 <b>붙잡는 모서리의 월드 X</b>를 함께 내보내는 버전 — 등반 자세가
        /// "손을 어디에 짚는가"를 알아야 하는데, 그 값은 이미 여기서 구하고 있다. 같은 값을 두 번째
        /// 계산원에서 다시 구하지 않기 위해 out 파라미터로 흘려보낼 뿐, <b>맨틀 계산은 한 줄도
        /// 바뀌지 않았다</b>(이 프로젝트가 두 번 겪은 "같은 값의 두 번째 계산원" 함정 회피).</summary>
        private bool TryComputeMantleTargetX(out float targetX, out float nearEdgeWorldX)
        {
            targetX = _startWorldX;
            nearEdgeWorldX = _startWorldX;
            if (!_hasWall) return false;
            if (!_blackboard.TryGetFootholdEdgeWorld(_wallHandle, -_direction, out _, out float nearEdgeX)) return false;
            nearEdgeWorldX = nearEdgeX;
            if (!_blackboard.TryGetFootholdEdgeWorld(_wallHandle, _direction, out _, out float farEdgeX)) return false;

            // ★ 2026-08-31 — 설정값을 직접 읽지 않는다. 인셋은 이제 경계 판정 거리와 같은 입력에서
            // 유도되는 값이고(StickmanBlackboard.ParkourMantleInsetWorld), 그 유도가 없으면 캐릭터
            // 크기 다이얼로 배율을 1.125 넘게 올리는 순간 "올라선 자리가 이미 경계"가 다시 성립한다.
            // 폴백(Config 없음)도 그 프로퍼티가 코드 기본값 0.6으로 받친다.
            float inset = _blackboard.ParkourMantleInsetWorld;
            float desired = nearEdgeX + _direction * Mathf.Max(0f, inset);
            targetX = Mathf.Clamp(desired, Mathf.Min(nearEdgeX, farEdgeX), Mathf.Max(nearEdgeX, farEdgeX));
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard.Body == null)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            // 매 프레임 "잡을 곳"이 여전히 존재하는지 재확인 — 창이 이동했으면 목표 높이도 함께 갱신된다.
            if (!_hasWall || !_blackboard.TryGetFootholdTopWorldY(_wallHandle, out _wallTopWorldY))
            {
                // 잡을 곳이 사라짐(창 이동/닫힘) -> 즉시 Fall (UX_FLOW.md 4절 실패 처리 — 이 상태가 만든
                // 대사가 있었다면 TransitionGeneration 불일치로 같은 프레임에 자동 취소됨, 5절 계약).
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            float climbDuration = _blackboard.Config != null ? _blackboard.Config.parkourClimbDuration : 1.20f;
            _climbProgress += climbDuration > 0f ? deltaTime / climbDuration : 1f;
            if (_climbProgress > 1f) _climbProgress = 1f;

            Vector2 pos = _blackboard.Body.position;
            pos.y = Mathf.Lerp(_startWorldY, _wallTopWorldY, _climbProgress);
            // 맨틀 수평 이동(위 _startWorldX 필드 주석 참고). 목표를 매 프레임 다시 구해 창이 움직여도
            // 따라간다. 구하지 못하면(테스트 리그 등 발판 조회 실패) 예전처럼 x를 건드리지 않는다.
            bool hasEdge = TryComputeMantleTargetX(out float mantleTargetX, out float nearEdgeWorldX);
            if (_hasMantleTarget && hasEdge)
            {
                pos.x = Mathf.Lerp(_startWorldX, mantleTargetX, _climbProgress);
            }
            // 몸 위치를 쓰는 유일한 창구(StickmanBlackboard.MoveBodyToWorld) — Rigidbody2D.position만
            // 쓰면 그 프레임에 그려지는 Transform이 낡은 좌표로 남는다(autoSyncTransforms 꺼짐).
            // 여기는 1.20초 보간이라 프레임당 이동량이 작지만, 창이 갑자기 크게 움직이면 그만큼 튄다.
            _blackboard.MoveBodyToWorld(pos);

            // BUG-P2-M1 대응(Major, docs/BUG_REPORT_PHASE2.md): Enter()의 1회성 속도 제로화만으로는
            // 부족하다 — Body는 여전히 일반 Dynamic Rigidbody2D라 매 FixedUpdate마다 중력이
            // linearVelocity.y에 조용히 계속 누적된다(등반 도중엔 위 pos.y Lerp가 매 프레임 위치를
            // 덮어써 화면상 안 보이지만, 등반 완료로 Idle/Walk에 전이된 직후 그 누적 속도가 그대로
            // 적용돼 착지 튐(pop)이 매번 재현됨). SnapToGround의 기존 관행(위치를 옮길 때마다 속도도
            // 함께 재확정)과 동일하게 여기서도 매 프레임 재확정한다.
            // x도 함께 0으로 확정한다 — 이제 이 상태가 x를 직접 구동하므로(맨틀), 진입 직전 걷던 속도가
            // 남아 있으면 매 프레임 위치 대입과 물리 적분이 서로 밀어내며 미세하게 어긋난다.
            _blackboard.Body.linearVelocity = Vector2.zero;

            DriveClimbPose(deltaTime, pos, hasEdge, nearEdgeWorldX);

            if (_climbProgress >= 1f)
            {
                // 올라선 발판을 즉시 고착한다 — 이게 없으면 다음 프레임의 접지 판정이 핸들 0(미획득)
                // 상태로 목록 첫 매치를 새로 고르게 되고, 마침 아래 발판이 먼저 걸리면 방금 오른 턱을
                // 두고 도로 내려간 것처럼 보인다. GroundedTick의 "접지 획득" 경로와 같은 취지다.
                _blackboard.CurrentFootholdHandle = _wallHandle;
                _blackboard.ReportFootholdChangeIfNeeded("벽타기 완료 — 턱 위에 올라섬");
                _blackboard.ResetGroundLossTimer();

                // ★ 배회 AI에게 "방금 턱 위로 올라섰다"를 알린다(2026-08-29). 이 신호가 없으면 등반을
                // 유발했던 경계 판정이 그대로 살아 있어, 배회 AI가 진행 방향을 방금 올라온 바깥쪽으로
                // 뒤집고 곧바로 같은 모서리로 다시 뛰어내린다(StickmanBlackboard.ClimbMantleSequence의
                // 실측 근거 주석 참고). 상태 머신을 구독시키지 않고 블랙보드 카운터로 알리는 이유도 거기 적었다.
                _blackboard.ReportClimbMantleCompleted(_direction);

                Debug.Log($"[벽타기] 완료 — 올라선 월드=({pos.x:F3},{pos.y:F3}), 발판핸들={_wallHandle}, " +
                    $"올라선 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}(맨틀 신호 #{_blackboard.ClimbMantleSequence}).");

                // ★★ 2026-09-02 (절대 불변 원칙 1 위반 수정) — 다음 상태는 **바로 윗줄에서 내가 방금
                // 확정한 사실**에서 파생한다. 예전에는 여기서 MoveInputX를 읽었는데, 그 값은 이 시점에
                // 아직 0이다: StickmanAgent.Update의 순서가 `_autoWander.Tick -> _machine.Tick`이라
                // 배회 AI는 **다음 프레임**에야 맨틀 신호를 소비해 EnterMoving(inward)을 부른다.
                // 그래서 "곧 턱 안쪽으로 걸어 들어간다"가 이미 블랙보드에 기록됐는데도 Idle을 골랐고,
                // 그 1프레임짜리 Idle에서 대사가 파생돼 0.02초 만에 잘렸다(실측 벽타기 완료 11회 중 2회).
                //
                // 맨틀 신호의 소비자(AutoWanderController)는 그 신호를 보면 **조건 없이**
                // EnterMoving(ClimbMantleDirection)을 부른다 — 즉 "걸어 들어간다"는 추정이 아니라
                // 확정 사실이고, 이 상태가 그 사실의 출처다. 그러므로 다음 상태는 Walk 하나뿐이다.
                //
                // ★ 배회 AI가 아닌 의도 소스가 물려 있어 이동 의도가 끝내 0이면 WalkState가 다음 틱에
                //   스스로 Idle로 나간다(기존 탈출 조건 그대로). 그 1프레임 Walk에서는 대사가 나오지
                //   않는다 — PlannedDwellRemainingSecondsFor(Walk)가 이미 0을 답하기 때문이다.
                //   즉 어느 쪽으로도 "1프레임짜리 상태에서 대사가 파생되는" 경로가 남지 않는다.
                _blackboard.Machine.ChangeState(StickmanStateId.Walk);
            }
        }

        /// <summary>
        /// ★ 등반 자세 구동(2026-09-01, 사용자 신고 "사람처럼 손으로 집고 다리를 올려서 올라가야지").
        /// LandingCrouch/Archery와 같은 관례로 <b>상태가 자기 진행 곡선으로 포즈를 직접 구동</b>한다
        /// (StickmanBlackboard.TickPoseRouting의 ParkourClimb 분기가 그래서 아무것도 하지 않는다).
        ///
        /// <para>이 메서드는 <b>몸 좌표를 한 번도 쓰지 않는다</b> — 위치/맨틀/전이는 전부 예전 그대로이며,
        /// 여기서는 이미 확정된 좌표를 <b>읽어서</b> "턱이 지금 몸에서 어디에 있는가"를 포즈에 알려줄
        /// 뿐이다. 매 프레임 다시 알려주는 이유는 잡을 곳 재확인과 같다: 오르던 창이 움직이면
        /// 짚는 자리도 함께 움직여야 한다.</para>
        ///
        /// <para>거리 성분은 전부 <see cref="StickmanBlackboard.CharacterHeightWorld"/>에서 유도한다
        /// (리더 지시: "거리·속도 성분은 StickmanMetrics에서 파생시켜라"). 그래서 캐릭터 크기 다이얼을
        /// 0.35로 줄이든 2.0으로 키우든 짚는 자리와 딛는 자리가 몸에 대해 같은 비율을 유지한다.</para>
        /// </summary>
        private void DriveClimbPose(float deltaTime, Vector2 pos, bool hasEdge, float nearEdgeWorldX)
        {
            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            if (pose == null || !_hasWall) return;

            float height = _blackboard.CharacterHeightWorld;
            StickConfig cfg = _blackboard.Config;

            // 포즈의 각도 공간은 **바라보는 방향이 앞**이다. 등반 방향과 바라보는 방향이 어긋나 있어도
            // (배회 AI가 프레임 중간에 의도를 뒤집는 경우) 손이 엉뚱한 쪽으로 가지 않도록 부호로 흡수한다.
            float toFacing = _blackboard.FacingSign * _direction >= 0f ? 1f : -1f;

            // 모서리를 못 구하면(테스트 리그 등 발판 조회 실패) 신장 비율로 폴백한다 — 그래도 자세는
            // 성립한다(손이 실제 모서리가 아니라 "몸 앞 어딘가"를 잡을 뿐이다).
            float edgeForward = hasEdge
                ? (nearEdgeWorldX - pos.x) * _direction
                : height * FallbackEdgeForwardRatio;

            float gripInset = height * (cfg != null ? cfg.parkourClimbGripInsetRatio : 0.10f);
            float footAhead = height * (cfg != null ? cfg.parkourClimbFootPlantAheadRatio : 0.18f);

            pose.ApplyParkourClimbPose(deltaTime,
                _blackboard.BuildPoseSettings(),
                _blackboard.BuildLedgeHangPoseSettings(),
                _blackboard.BuildParkourClimbPoseSettings(),
                _blackboard.ParkourClimbPoseSmoothingRate,
                _climbProgress,
                _wallTopWorldY - _startWorldY,
                _wallTopWorldY - pos.y,
                (edgeForward + gripInset) * toFacing,
                (edgeForward + gripInset + footAhead) * toFacing,
                edgeForward * toFacing);
        }

        /// <summary>모서리 X를 구하지 못했을 때 손을 짚을 앞쪽 거리(신장 대비). 실제 경로에서는 쓰이지
        /// 않고(발판이 있어야 이 상태에 들어온다) 발판 조회가 없는 테스트 리그를 위한 폴백이다.</summary>
        private const float FallbackEdgeForwardRatio = 0.2f;

        public void Exit() { }
    }
}
