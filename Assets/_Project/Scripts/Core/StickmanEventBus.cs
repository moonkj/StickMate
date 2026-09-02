using System;
using System.Collections.Generic;
using UnityEngine;
using StickMate.Dialogue;

namespace StickMate.Core
{
    /// <summary>스틱맨의 상태 식별자. Core에 두는 이유: States/Dialogue/Plugins 등 상위 레이어가
    /// 모두 이 값을 공유해야 하는데, Core는 프로젝트의 최하위 레이어라 순환 참조가 생기지 않는다.</summary>
    public enum StickmanStateId
    {
        Idle,
        Walk,
        Jump,
        Fall,
        ParkourClimb,
        Attack,
        Ragdoll,
        Getup,

        // ==== Phase 3 (docs/UX_FLOW.md 12/13절) — 커서 상호작용 ====
        // ★ 2026-09-02 격파 미니게임(구 10절) 삭제 — 사용자 지시 "격파놀이는 아예없애줘 별로임".
        //   상태/디렉터/렌더러/전용 테스트 전부 제거했다. 세이브의 battleWins 필드만 스키마 무변경을
        //   위해 남아 있다(Core/CharacterStatsModel.BattleWins 문서 참고).

        /// <summary>드래그&던지기(12절): 유저가 캐릭터 히트박스를 마우스다운으로 붙잡아 끄는 동안의 상태.
        /// Kinematic으로 커서를 추종하다가 놓으면 계산된 속도로 Dynamic 던지기 → 임계값 초과 시 Ragdoll,
        /// 아니면 Fall로 자연 전이.</summary>
        Dragged,

        /// <summary>로데오 커서(13절): 클릭 없이 커서 정지만으로 발동, 캐릭터가 커서 위치에 올라타 따라
        /// 다니다가 거친 흔들기/10초 타임아웃/트레이 긴급정지 3중 안전망으로 종료.</summary>
        RodeoCursor,

        // ==== Phase 4 (docs/UX_FLOW.md 27절) — OS 장난 / 파괴효과. 전부 "실제로는 아무것도 건드리지
        // 않는 착시" 스펙터클(27-7 체크리스트)이며, 실제 창/아이콘 좌표는 읽기 전용으로만 참조한다. ====

        /// <summary>윈도우 창 도둑(27-1): 작은 창을 붙잡고 미는/당기는 시늉을 2회 시도한 뒤 포기하는
        /// 순수 연출. 실제 창 좌표를 변경하는 API는 절대 호출하지 않는다(성공 케이스 자체가 설계에 없음).
        /// Interaction/WindowTheftDirector.cs가 대상 선정/취소 감시를 전담한다.</summary>
        WindowTheft,

        /// <summary>화면 낙서 그라피티(27-3): 캐릭터 근처 발판과 안 겹치는 빈 영역에 스프레이 낙서를
        /// 그렸다가 페이드아웃하는 순수 오버레이. 배경화면 파일/설정 API는 전혀 호출하지 않는다.</summary>
        Graffiti,

        /// <summary>바탕화면 청소부(27-2): 복제 스프라이트로 아이콘을 정렬하는 시늉. 실제 아이콘 좌표는
        /// 읽기 전용 조회로만 쓰이고, 오버레이는 항상 100% 클릭관통이다. 27-5(블랙홀)와 상호배제.</summary>
        DesktopTidy,

        /// <summary>블랙홀 소환(27-5): 27-2와 동일한 복제 스프라이트 파이프라인을 재사용하는 코믹 물리
        /// 스펙터클. 27-2(청소부)와 상호배제.</summary>
        BlackholeSummon,

        /// <summary>윈도우 크래시(27-4): 활성 창에 해머를 내리치는 캐릭터 스윙 모션(짧게 재생 후 자동
        /// Idle 복귀). 크랙 유리 오버레이 자체의 3초 수명은 이 상태와 독립적으로
        /// Interaction/WindowCrashDirector.cs가 관리하며, 그 오버레이는 예외 없이 100% 클릭관통이다.</summary>
        WindowCrash,

        // ==== Phase 5 (docs/UX_FLOW.md 17~20절) — 생산성(투두/포모도로) + 반항/스트레스(SULKY/가출).
        // TodoReminder/FocusStart/FocusComplete/FocusCancelled/FocusNudge/Sulky는 전부 "물리/입력 변경
        // 없는 순수 타이머 + 고정 대사 1회" 형태라 States/TimedSpectacleState.cs(선택적 대사 지원으로
        // 일반화됨)를 재사용해 인스턴스화한다 — Phase 4의 4개 상태와 동일한 재사용 컨벤션. Runaway만
        // 다중 페이즈/텔레포트/렌더러 토글이 필요해 전용 States/RunawayState.cs를 쓴다. ====

        /// <summary>투두 말풍선 '들고 다니는 모드'(17절): 종이를 꺼내 확정된 할일 1개를 대사로 보여주고
        /// 다시 접어 넣는 순수 연출. Interaction/TodoReminderDirector.cs가 유휴 판정으로 트리거한다.</summary>
        TodoReminder,

        /// <summary>포모도로 감시자(18절) 타이머 시작 포즈(안경+팔짱) + "좋아, 감시 시작" 대사.</summary>
        FocusStart,

        /// <summary>포모도로 감시자 타이머 정상 만료 축하 포즈 + "수고했어!" 대사.</summary>
        FocusComplete,

        /// <summary>포모도로 감시자 유저의 중도 취소 시 패널티 없는 톤의 포즈 + "그래 쉬자" 대사.</summary>
        FocusCancelled,

        /// <summary>포모도로 감시자 2단계 "부드러운 리마인드" — "어? 딴 데 보고 있네?" 대사 1회.</summary>
        FocusNudge,

        /// <summary>스트레스 게이지(19절)가 임계값(80%) 근접 시 확정 발동하는 SULKY(부루퉁함) — 한숨/짜증
        /// 대사와 처진 자세. "곧 가출한다"는 예고가 아니라 "지금 기분이 안 좋다"는 현재형 사실 보고.</summary>
        Sulky,

        /// <summary>가출(20절, 반항 2단계): 스트레스 게이지가 확정 임계값에 도달하면 확률이 아니라
        /// 확정 발동. "나 안 해!" → 은신 → 유저 탐색(클릭)/자동 타임아웃/긴급 강제소환 → 복귀까지
        /// 여러 페이즈를 States/RunawayState.cs가 전담한다.</summary>
        Runaway,

        // ==== 매달려 내려가기 (docs/UX_FLOW.md 4절 "매달리기(HANG)", 사용자 명시 요청 2026-08-28
        // "내려갈때도 매달려서 내려가는형태로") ====

        /// <summary>
        /// 발판 가장자리에서 뚝 떨어지는 대신 **모서리를 붙잡고 매달렸다가 손을 놓아 내려가는** 동작
        /// (States/LedgeHangState.cs). ParkourClimb가 "아래에서 위"라면 이쪽은 "위에서 아래"이며,
        /// 정상 종료는 항상 Fall이다(붙잡고 있던 발판이 사라져도, 타임아웃이어도 Fall).
        ///
        /// 왜 ParkourClimb에 모드 플래그를 더하지 않고 별도 상태로 두었는가: ParkourClimb는 "시작 Y ->
        /// 벽 상단 Y를 하나의 진행도로 Lerp"하는 단일 페이즈 상태이고 종료가 Idle/Walk인 반면, 이쪽은
        /// 잡기 -> 매달림 유지 -> 손 놓기의 3페이즈에 종료가 항상 Fall이라 두 상태가 공유할 코드가
        /// 사실상 "발판 핸들 재확인" 한 줄뿐이다(그건 이미 GroundSensor의 정적 유틸이라 상태를 합치지
        /// 않고도 그대로 재사용된다). 모드 플래그를 넣으면 거의 모든 줄에 분기가 생겨 이미 검증된
        /// 등반 경로까지 회귀 위험에 노출된다.
        /// </summary>
        LedgeHang,

        // ==== 무릎앉아 착지 (사용자 명시 요청 2026-08-29: "떨어질때 관절이 이상하게 꺾이면서 넘어지는데
        // 떨어질때 무릎앉아 형태로 멋지게 착지해야지") ====

        /// <summary>
        /// 높은 곳에서 떨어져 착지한 직후, **한쪽 무릎을 굽혀 낮게 앉아 충격을 흡수했다가 일어서는**
        /// 짧은 능동 상태(States/LandingCrouchState.cs). docs/UX_FLOW.md 4절의 "구르기(ROLL)" 행이
        /// 요구하던 "부드러운 착지 연출"의 실체이며, 그 판정 신호였던
        /// <see cref="StickmanEventBus.LandingRollRequested"/>와 **같은 조건**
        /// (낙하 높이 &gt;= StickConfig.landingSoftAbsorbThresholdHeights x 신장)에서 FallState가 직접 전이시킨다.
        ///
        /// 왜 이벤트 구독자가 아니라 FallState의 직접 전이인가: 착지 직후의 상태 전이는 "있으면 좋은
        /// 연출"이 아니라 **다음 상태를 결정하는 흐름 그 자체**다. 이벤트 구독자에게 맡기면 FallState가
        /// 같은 프레임에 이미 Idle/Walk로 전이한 뒤에 구독자가 다시 ChangeState를 부르는 순서 의존이
        /// 생기고, 구독자가 없으면(이 프로젝트에서 6번 반복된 실패 유형) 조용히 사라진다.
        /// LandingRollRequested는 먼지 파티클 같은 **부수 연출**용으로 그대로 남는다.
        ///
        /// 정상 종료는 Idle/Walk(착지 시점의 이동 의도로 분기)이며, 도중 외력 임계값 초과 시 다른 능동
        /// 상태와 똑같이 Ragdoll로 강제 인터럽트될 수 있다.
        /// </summary>
        LandingCrouch,

        // ==== 던져졌을 때 공중 회전 후 무릎앉아 착지 (사용자 명시 요청 2026-08-29:
        // "마우스로 던졌을때도 이상하게 관절꺽이면서 넘어지는데 던져도 공중에서 회전하면서
        // 무릎앉아 착지할수있게 해줘") ====

        /// <summary>
        /// 유저가 커서로 붙잡아 **던진 직후의 공중 회전(텀블링)** 능동 상태(States/ThrowTumbleState.cs).
        /// 던지기가 곧바로 Ragdoll로 가던 예전 경로를 대체한다 — 랙돌은 전신 물리에 위임하므로 팔다리가
        /// 제멋대로 꺾이며 뒹굴었고, 그것이 사용자가 신고한 "이상하게 관절 꺾이면서 넘어진다"였다.
        ///
        /// 아키텍처 0절의 능동 상태 규약을 그대로 지킨다: 팔다리는 Kinematic + 절차적 localRotation이고,
        /// 몸 전체의 회전은 **루트의 시각 회전을 상태가 직접 구동**한다(물리 각속도에 맡기지 않는다 —
        /// 물리에 맡기는 순간 그것이 곧 "이상하게 꺾이는" 그림이다).
        ///
        /// 정상 종료는 LandingCrouch(무릎앉아 착지)이며, 착지 전에 회전을 정수 바퀴로 마무리해 몸을
        /// 바로 세운다. 도중 **진짜 외력**(벽 충돌 등 임계값 초과 충격)이 들어오면 다른 능동
        /// 상태와 똑같이 Ragdoll로 강제 인터럽트된다 — 즉 랙돌은 사라진 것이 아니라 "깨끗하게 던져진
        /// 자유 비행"에서만 빠진 것이다(States/DragThrowState.ReleaseAndThrow의 갈림 기준 주석 참고).
        /// StickConfig.throwTumbleEnabled를 끄면 던지기가 예전처럼 곧바로 Ragdoll/Fall로 간다.
        /// </summary>
        ThrowTumble,

        // ==== 활쏘기 (사용자 명시 요청 2026-08-29: "하는 행동중 하나가 활을 들고 화살을 쏘는건데
        // 과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동을 하는거지") ====

        /// <summary>
        /// 제자리에 서서 <b>당기기 -> 조준 정지 -> 발사</b>를 3회 반복하는 능동 상태
        /// (States/ArcheryState.cs). 트리거/과녁 자리 선정은 Interaction/ArcheryDirector.cs,
        /// 과녁·활·화살 그림은 Interaction/ArcheryRenderer.cs가 맡는다.
        ///
        /// 명중/빗나감은 <b>전이가 확정된 Enter()에서 미리 전부 뽑아두고</b>, 화살 궤적은 그 확정된
        /// 도달점을 지나도록 역산한다 — 물리로 던져놓고 우연에 맡기지 않는다(리더 지시).
        /// 정상 종료는 Idle이며, 다른 스펙터클과 동일하게 SpectacleEventLock에 참여한다.
        /// </summary>
        Archery,

        // ==== 발판 상실 공중 유예 (2026-09-01, 소은 실측 + 리더 결정 "시간은 두고 연출을 붙인다") ====

        /// <summary>
        /// 딛고 있던 발판이 창 목록에서 사라졌지만 <b>아직 떨어지지는 않는</b> 유예 구간
        /// (States/GroundLossHangState.cs). 유예 자체는 2026-09-01 오전에 이미 있었지만
        /// <c>StickmanBlackboard._graceHoldFrame</c>이라는 <b>내부 플래그</b>였고, 이 프로젝트 규약은
        /// "상태 ID 하나로 포즈가 결정된다"(StickmanBlackboard.TickPose)이므로 포즈를 붙이려면 상태로
        /// 승격하는 것이 맞다(리더 결정 2026-09-01 승인사항 2).
        ///
        /// <para><b>왜 이 상태가 필요한가(실측 근거).</b> 같은 빌드·같은 물리·같은 지속시간인데
        /// IDLE 중이면 10프레임 넘게 화소차 0.00%로 <b>완전히 얼어붙고</b>(= "앱이 멈췄다"로 읽힌다),
        /// WALK 중이면 다리가 계속 돌아가 <b>코요테 개그로 읽힌다</b>. 즉 문제는 유예의 길이가 아니라
        /// 그 시간에 <b>생명 신호가 있느냐</b>다(Tasklist.md "소은의 의견 (2026-09-01, 발판 상실 공중
        /// 정지)" 1·2항). 이 상태는 IDLE/WALK 어느 쪽에서 들어와도 같은 그림(제자리 종종걸음 + 팔
        /// 허우적)을 만들어 그 신호를 확정한다.</para>
        ///
        /// <para><b>대사는 만들지 않는다</b>(리더 결정 승인사항 3). 상태로 승격되면 기술적으로는
        /// 원칙 1을 지키며 대사를 붙일 수 있게 되지만, 사용자가 요청하지 않은 연출/대사에 반복적으로
        /// 불만을 표한 이력이 있어 <b>연출만</b> 넣는다.</para>
        ///
        /// <para>진입은 Idle/Walk에서만이다 — 이유는 States/GroundLossHangState.cs 클래스 문서의
        /// "왜 Idle/Walk에서만 승격하는가" 절에 실패 시나리오와 함께 적어 뒀다. 나가는 경로는
        /// 유예 만료 -> Fall / 발판 복귀 -> Idle·Walk / 발밑이 정말 비었음 -> Fall / 화면 이탈 -> Fall /
        /// 외력 -> Ragdoll(강제 인터럽트) / 상한 초과 -> Fall(갇힘 방지 최후 안전망)이며,
        /// <b>이 상태에 갇히면 캐릭터가 영원히 공중에 뜬다</b> — 원래 버그보다 나쁘므로
        /// Tests/PlayMode/GroundLossHangStateTests.cs가 모든 경로를 잠근다.</para>
        /// </summary>
        GroundLossHang,
    }

    /// <summary>
    /// docs/UX_FLOW.md 27-2/27-5절 — 바탕화면 청소부와 블랙홀 소환이 공유하는 "복제 스프라이트" 파이프라인
    /// (28절-25 "하나의 공용 컴포넌트로 통합 구현" 권고)에서, 지금 어느 쪽이 진행 중인지 구분하는 값.
    /// </summary>
    public enum DesktopIconMirrorKind
    {
        DesktopTidy,
        BlackholeSummon,
    }

    /// <summary>
    /// 27-1/27-2/27-3/27-4/27-5(창 도둑/청소부/그라피티/크래시/블랙홀) 오버레이 스펙터클 공용 생애주기
    /// 단계. Phase2+ 렌더링 레이어가 이 값으로 "자리표시자 오버레이 스프라이트"를 스폰(Started)/즉시
    /// 제거(Cancelled)/페이드아웃 제거(Completed)한다 — 지금은 좌표/취소판정만 확정하고 실제 스프라이트
    /// 생성·이동·제거는 다른 트리거 이벤트(WanderAmbientMotionRequested 등)와 동일하게 Phase2+ 몫이다.
    /// </summary>
    public enum SpectacleOverlayPhase
    {
        Started,
        Cancelled,
        Completed,
    }

    /// <summary>윈도우 창 도둑(27-1) 오버레이 이벤트 — 대상 창의 스냅샷 사각형(OS 화면 좌표)을 함께 실어
    /// Phase2+ 렌더링이 어디로 캐릭터 팔을 뻗을지 알 수 있게 한다.</summary>
    public readonly struct WindowTheftOverlayEvent
    {
        public readonly Rect TargetRectOsScreen;
        public readonly SpectacleOverlayPhase Phase;

        public WindowTheftOverlayEvent(Rect targetRectOsScreen, SpectacleOverlayPhase phase)
        {
            TargetRectOsScreen = targetRectOsScreen;
            Phase = phase;
        }
    }

    /// <summary>화면 낙서 그라피티(27-3) 오버레이 이벤트 — 그려질 빈 영역(OS 화면 좌표)의 스냅샷.</summary>
    public readonly struct GraffitiOverlayEvent
    {
        public readonly Rect RegionOsScreen;
        public readonly SpectacleOverlayPhase Phase;

        public GraffitiOverlayEvent(Rect regionOsScreen, SpectacleOverlayPhase phase)
        {
            RegionOsScreen = regionOsScreen;
            Phase = phase;
        }
    }

    /// <summary>바탕화면 청소부/블랙홀(27-2/27-5) 오버레이 이벤트 — 이벤트 시작 시 1회 캡처한 아이콘
    /// 사각형 목록(OS 화면 좌표, 읽기 전용 조회 결과)의 스냅샷. Started에서만 의미 있는 목록이 실리고,
    /// Cancelled/Completed는 빈 목록으로 발행된다(Phase2+ 렌더링은 자신이 스폰한 스프라이트를 이미
    /// 알고 있으므로 제거 시점에 목록이 다시 필요하지 않다).</summary>
    public readonly struct DesktopIconMirrorOverlayEvent
    {
        public readonly DesktopIconMirrorKind Kind;
        public readonly IReadOnlyList<Rect> IconRectsOsScreen;
        public readonly SpectacleOverlayPhase Phase;

        public DesktopIconMirrorOverlayEvent(DesktopIconMirrorKind kind, IReadOnlyList<Rect> iconRectsOsScreen, SpectacleOverlayPhase phase)
        {
            Kind = kind;
            IconRectsOsScreen = iconRectsOsScreen;
            Phase = phase;
        }
    }

    /// <summary>윈도우 크래시(27-4) 오버레이 이벤트 — 대상(활성) 창의 스냅샷 사각형(OS 화면 좌표).</summary>
    public readonly struct WindowCrashOverlayEvent
    {
        public readonly Rect TargetRectOsScreen;
        public readonly SpectacleOverlayPhase Phase;

        public WindowCrashOverlayEvent(Rect targetRectOsScreen, SpectacleOverlayPhase phase)
        {
            TargetRectOsScreen = targetRectOsScreen;
            Phase = phase;
        }
    }

    /// <summary>docs/UX_FLOW.md 23/27-6절 PC 하드웨어 반응 4종.</summary>
    public enum HardwareReactionKind
    {
        LowBattery,
        HighCpu,
        NetworkDown,
        Charging,
    }

    /// <summary>
    /// 하드웨어 반응 표시 상태 변경 이벤트. Active=true는 "이 반응을 지금부터 표현 시작"(23절 우선순위
    /// 판정을 거쳐 동시에 최대 하나만 true), Active=false는 "표현 종료"(회복 또는 다른 신호에 의한
    /// 우선순위 교체)를 뜻한다. 실제 배터리/CPU % 숫자는 원칙상(23절 "은유만 담당") 이 이벤트에 싣지
    /// 않는다 — Phase2+ 렌더링은 Kind만으로 미리 정해진 은유 연출(힘없이 비틀거림/부채질 등)을 고른다.
    /// </summary>
    public readonly struct HardwareReactionEvent
    {
        public readonly HardwareReactionKind Kind;
        public readonly bool Active;

        public HardwareReactionEvent(HardwareReactionKind kind, bool active)
        {
            Kind = kind;
            Active = active;
        }
    }

    /// <summary>docs/UX_FLOW.md 18절 포모도로 감시자 "딴짓 감지" 에스컬레이션 단계. None=정상 범위
    /// 복귀(즉시 리셋), Glance=1단계(곁눈질, 대사 없음, 순수 앰비언트 이벤트), Nudge=2단계(대사 1회,
    /// 실제로는 이벤트가 아니라 StickmanStateId.FocusNudge 상태 전이로 표현되므로 이 이벤트에는 잘
    /// 실리지 않는다 — 그래도 렌더링 레이어가 "지금 몇 단계인지" 한 번에 알 수 있도록 함께 통지한다),
    /// WindowTap=3단계(타이머 위젯 두드림+화면 흔들림, 순수 앰비언트 이벤트).</summary>
    public enum FocusWatchTier
    {
        None,
        Glance,
        Nudge,
        WindowTap,
    }

    /// <summary>가출(20절) 상태의 세부 생애주기. Phase2+ 렌더링이 이 값으로 "뛰어가는 애니메이션 →
    /// 사라짐 → (숨은 자리 은은한 단서) → 발견되어 놀란 표정으로 드러남 → 화해/자진복귀 → 정상 복귀"를
    /// 표현한다. 지금은 트리거 조건/좌표만 확정하고 실제 연출은 아무도 구독하지 않아도 무해하다
    /// (WanderAmbientMotionRequested류 기존 패턴과 동일).</summary>
    public enum RunawayLifecyclePhase
    {
        Fleeing,
        Hidden,
        Found,
        Reconciled,
        SelfReturned,
    }

    /// <summary>가출 생애주기 변경 이벤트 — 은신처 좌표(OS 화면, Hidden/Found에서만 의미 있음)를 함께 싣는다.</summary>
    public readonly struct RunawayLifecycleEvent
    {
        public readonly RunawayLifecyclePhase Phase;
        public readonly Vector2 HideSpotOsScreen;

        public RunawayLifecycleEvent(RunawayLifecyclePhase phase, Vector2 hideSpotOsScreen)
        {
            Phase = phase;
            HideSpotOsScreen = hideSpotOsScreen;
        }
    }

    /// <summary>활쏘기(2026-08-29) 한 발의 <b>사전 확정된</b> 결과. 물리 판정 결과가 아니라
    /// States/ArcheryState.Enter()가 시나리오로 미리 뽑아둔 값이며(마지막 발은 항상 Bullseye,
    /// 앞 두 발 중 정확히 하나가 Miss), 렌더러는 이 값에 맞는 도달점을 지나도록 궤적을 역산할 뿐
    /// 스스로 명중 여부를 판단하지 않는다 — 판정과 그림이 어긋날 경우의 수를 없앤다.</summary>
    public enum ArcheryShotResult
    {
        /// <summary>과녁에 못 미치고 그 앞 땅에 꽂힌다(흙먼지가 함께 난다).</summary>
        Miss,

        /// <summary>과녁 바깥 링에 꽂힌다.</summary>
        Hit,

        /// <summary>정중앙에 꽂힌다. 연출의 클라이맥스라 항상 마지막 발이다.</summary>
        Bullseye,
    }

    /// <summary>활쏘기 한 발의 두 시점. Aim=시위를 당기기 시작(렌더러가 이 발의 계획을 받아 활을
    /// 조준선에 맞춘다), Release=놓는 순간(렌더러가 화살을 실제로 띄운다).</summary>
    public enum ArcheryShotPhase
    {
        Aim,
        Release,
    }

    /// <summary>활쏘기 한 발의 계획/발사 통지. 도달점이 <b>이벤트에 실려 오므로</b> 렌더러와 상태가
    /// 같은 좌표를 보게 되고, 둘이 각자 계산해 어긋날 여지가 없다.</summary>
    public readonly struct ArcheryShotEvent
    {
        public readonly int ShotIndex;
        public readonly ArcheryShotPhase Phase;
        public readonly ArcheryShotResult Result;

        /// <summary>미리 확정된 도달점(월드 좌표).</summary>
        public readonly Vector2 ImpactWorld;

        /// <summary>이 화살이 날아가는 데 걸리는 시간(초). 도달점과 이 값이 정해지면 포물선의 초기
        /// 속도는 유일하게 결정된다(ArcheryRenderer.SolveLaunchVelocity).</summary>
        public readonly float FlightSeconds;

        public ArcheryShotEvent(int shotIndex, ArcheryShotPhase phase, ArcheryShotResult result,
            Vector2 impactWorld, float flightSeconds)
        {
            ShotIndex = shotIndex;
            Phase = phase;
            Result = result;
            ImpactWorld = impactWorld;
            FlightSeconds = flightSeconds;
        }
    }

    /// <summary>활쏘기 과녁 오버레이의 생애주기. 다른 스펙터클과 같은 SpectacleOverlayPhase 3단계를
    /// 쓰고, 과녁 중심/지면 높이/바라보는 방향 스냅샷을 함께 싣는다(전부 월드 좌표 — 이 연출은 창
    /// 좌표계와 무관하게 캐릭터 주변에서만 일어나므로 OS 좌표로 변환할 이유가 없다).</summary>
    public readonly struct ArcheryOverlayEvent
    {
        public readonly Vector2 TargetWorld;
        public readonly float GroundWorldY;
        public readonly float Facing;
        public readonly SpectacleOverlayPhase Phase;

        public ArcheryOverlayEvent(Vector2 targetWorld, float groundWorldY, float facing, SpectacleOverlayPhase phase)
        {
            TargetWorld = targetWorld;
            GroundWorldY = groundWorldY;
            Facing = facing;
            Phase = phase;
        }
    }

    /// <summary>
    /// docs/UX_FLOW.md 26-3절 "살아있는 느낌" 디테일 — AutoWanderController가 타이밍/확률 조건만 판정해
    /// 발행하는 유휴 연출 신호. 실제 동작 재생은 Interaction/IdleAmbientMotionRenderer.cs가 구독해
    /// StickmanBlackboard.BeginIdleAmbientMotion()으로 넘긴다(2026-08-30 배선 완료).
    /// </summary>
    public enum WanderAmbientMotion
    {
        /// <summary>Idle 진입 후 1.0~2.5초 랜덤 지연 뒤 발동, 0.6~1.0초 지속(26-3).</summary>
        LookAround,

        /// <summary>"Idle 연장"이 연속 3회 이상 선택된 경우에만 15% 확률로 발동, 1.5~2.5초 지속(26-3).</summary>
        SitAndYawn,
    }

    /// <summary>
    /// 착지 부수 연출(먼지) 신호의 페이로드.
    ///
    /// ★ 2026-08-30 — 예전에는 <c>Action&lt;float&gt;</c>(낙하 높이 하나)였다. 구독자를 붙이는 순간
    /// **누구의 착지인지 알 수 없다**는 문제가 드러나 좌표를 함께 싣도록 바꿨다. 지금은 씬에
    /// 캐릭터가 하나뿐이지만 페이로드는 그대로 유지한다 — 먼지를 "낙하 높이"만으로 그리면 발행자가
    /// 아니라 구독자가 위치를 추측해야 하고, 그 추측은 상태 전이 타이밍에 따라 어긋난다.
    /// </summary>
    public readonly struct LandingImpactEvent
    {
        /// <summary>실제 낙하 높이(월드 유닛). StickConfig.landingSoftAbsorbThresholdHeights x 신장 이상일 때만 발행된다.</summary>
        public readonly float FallHeight;

        /// <summary>착지 확정 시점의 발밑 월드 좌표(캐릭터 루트 원점 = 발바닥).</summary>
        public readonly Vector2 FootWorldPosition;

        public LandingImpactEvent(float fallHeight, Vector2 footWorldPosition)
        {
            FallHeight = fallHeight;
            FootWorldPosition = footWorldPosition;
        }
    }

    /// <summary>
    /// ★ 캐릭터 <b>배율 변경</b> 1건 — 2026-09-01 설정창 신설과 함께 추가(docs/UX_FLOW.md 35-1-3 ①).
    ///
    /// <para><b>왜 이 이벤트가 필요한가</b>: 배율을 바꾸는 UI가 <b>둘</b>이 됐다(구석 호버 다이얼 /
    /// 설정창 슬라이더). 알림 채널이 없으면 설정창에서 1.20×로 바꾼 뒤 구석 패널을 열었을 때
    /// 다이얼이 <b>옛 값</b>을 가리킨다 — "켜진 눈금 = 표시 숫자 = 실제 값"이라는 34-3-4의 보증이
    /// 깨지고, 그것이 곧 절대 불변 원칙 1(표시와 실제의 일치) 위반이다.</para>
    ///
    /// <para><b>Value는 "사용자가 고른 값"이다</b>(캐릭터에 이미 들어간 값이 아니다). 랙돌/스펙터클
    /// 중에는 적용이 최대 3초 유예되는데(34-3-6), 그 동안에도 두 UI는 <b>사용자가 고른 값</b>을
    /// 똑같이 보여줘야 한다 — 유예는 몸이 늦는 것이지 선택이 취소된 것이 아니다. 실제 적용 여부는
    /// <see cref="AppliedToCharacter"/>로 구분한다(대기 중이면 false로 한 번, 적용될 때 true로 한 번
    /// 더 발행된다).</para>
    /// </summary>
    public readonly struct CharacterScaleChangeEvent
    {
        /// <summary>사용자가 고른 배율(0.05 눈금에 스냅됨, StickConfig.Min/MaxCharacterScale 구간).</summary>
        public readonly float Value;

        /// <summary>출처(로그/진단용) — "구석 다이얼" / "설정창 슬라이더" / "저장된 크기 복원" 등.</summary>
        public readonly string Reason;

        /// <summary>이 발행 시점에 실제 캐릭터까지 반영됐는가. false면 적용 대기 중(최대 3초).</summary>
        public readonly bool AppliedToCharacter;

        public CharacterScaleChangeEvent(float value, string reason, bool appliedToCharacter)
        {
            Value = value;
            Reason = reason;
            AppliedToCharacter = appliedToCharacter;
        }
    }

    /// <summary>상태 전이 1건을 나타내는 불변 이벤트 페이로드 (From -> To).</summary>
    public readonly struct StateTransitionEvent
    {
        public readonly StickmanStateId From;
        public readonly StickmanStateId To;

        /// <summary>
        /// 이 전이가 "일반 전이"(자연스러운 완료/입력에 의한 전이)가 아니라 진행 중이던 상태를
        /// 중도에 인터럽트한 "강제 전이"(예: RAGDOLL 피격)인지 여부.
        /// UX_FLOW.md 5절/9절-2 요구사항: 말풍선 UI가 "정상 종료 시 최소 노출시간 보장 후 페이드아웃"과
        /// "강제 취소 시 최소 노출시간 무시하고 즉시 제거"를 구분할 수 있어야 하므로, 그 판단 근거를
        /// DialogueIntent의 IsValid(같은 프레임 만료) 메커니즘과는 별개로 이벤트에 함께 실어 보낸다.
        /// </summary>
        public readonly bool IsForcedInterrupt;

        public StateTransitionEvent(StickmanStateId from, StickmanStateId to, bool isForcedInterrupt)
        {
            From = from;
            To = to;
            IsForcedInterrupt = isForcedInterrupt;
        }
    }

    /// <summary>
    /// 상태 전이 / 발판 변경 / 대사 요청을 알리는 경량 이벤트 버스.
    ///
    /// 왜 C# event 기반 "정적" 클래스인가:
    /// 1) 레이어 간 결합도 최소화 — 입력/렌더/네이티브(P/Invoke)/AI/UI 레이어가 서로의 구체 타입을
    ///    몰라도 통신할 수 있다. 상태머신은 "누가 듣는지" 모른 채 RaiseStateTransitioned만 호출하면 된다.
    /// 2) MonoBehaviour 싱글톤 대신 순수 C# 정적 클래스를 쓰는 이유 — 이 앱은 24시간 상주하는
    ///    백그라운드형 앱이라 씬 전환/오브젝트 파괴 생명주기와 이벤트 구독을 분리해두는 편이
    ///    Destroy 타이밍에 따른 구독 유실 문제를 피하기 쉽다.
    /// 3) 정적 event 자체는 GC 압박이 없다 — 구독/해제(+=/-=)만 규율 있게 지키면 된다.
    ///    특히 MonoBehaviour 구독자는 반드시 OnDisable/OnDestroy에서 -= 로 해제해야 메모리 누수가 없다
    ///    (정적 이벤트가 파괴된 인스턴스를 계속 붙들고 있는 전형적인 Unity 버그 패턴 방지).
    /// 4) "매 프레임 폴링" 대신 "확정된 사건"만 통지 → 구독자들이 Update()에서 매 프레임 상태 비교를
    ///    반복할 필요가 없어져 할당/CPU를 아낄 수 있다 (24시간 상주 앱 성능 컨벤션과 직결).
    /// </summary>
    public static class StickmanEventBus
    {
        /// <summary>상태 전이가 "확정"된 프레임에 발생. DialogueIntent의 자동 만료 감지도 이 이벤트를 구독한다.</summary>
        public static event Action<StateTransitionEvent> StateTransitioned;

        /// <summary>발판 목록이 바뀌었을 때(창 열림/닫힘, 모바일 유저 지정 추가/삭제 등) 발생.</summary>
        public static event Action FootholdsChanged;

        /// <summary>새 DialogueIntent가 생성되었을 때 발생 — UI 레이어는 이 이벤트만 구독해 말풍선을 띄운다.</summary>
        public static event Action<DialogueIntent> DialogueRequested;

        /// <summary>DialogueIntent가 만료(상태 재전이로 무효화)되었을 때 발생 — UI 레이어는 말풍선을 즉시 숨긴다.</summary>
        public static event Action<DialogueIntent> DialogueExpired;

        /// <summary>
        /// 인질극/로데오 커서 등 방해성 이벤트를 상태머신의 일반 전이 규칙과 완전히 무관하게 즉시
        /// 중단시키기 위한 전역 긴급 정지 채널 (UX_FLOW.md 6-5절/9절-6). 발행자는 Phase 3/4의
        /// 트레이/메뉴바 상시 노출 버튼이며, 구독자는 방해성 이벤트를 구현하는 상태들이다.
        /// Phase 0에서는 아직 아무도 발행/구독하지 않지만, "지금 인터페이스에 자리를 예약해둘 것"이라는
        /// 교차 레이어 요구사항에 따라 이벤트 슬롯만 미리 확보해둔다.
        /// </summary>
        public static event Action GlobalEmergencyStopRequested;

        /// <summary>AutoWanderController가 두리번거리기/앉기·하품 트리거 조건을 판정했을 때 발생(UX_FLOW.md 26-3절).</summary>
        public static event Action<WanderAmbientMotion> WanderAmbientMotionRequested;

        /// <summary>
        /// FallState/ThrowTumbleState가 착지를 확정한 순간, 낙하 높이가
        /// StickConfig.landingSoftAbsorbThresholdHeights x 신장 이상이었을 때 발생(UX_FLOW.md 4절 "구르기(ROLL)").
        ///
        /// <b>착지의 물리적 반응 자체는 이 이벤트가 아니라 StickmanStateId.LandingCrouch가 담당한다</b>
        /// (같은 조건에서 상태가 직접 전이한다 — 그 이유 전문은 LandingCrouch 열거값 문서). 이 이벤트는
        /// 그 위에 얹는 <b>부수 연출</b>(발밑 먼지) 전용이며, 구독자는
        /// Interaction/LandingDustRenderer.cs다(2026-08-30 배선 완료).
        /// </summary>
        public static event Action<LandingImpactEvent> LandingRollRequested;

        /// <summary>활쏘기(2026-08-29) 과녁 오버레이 생애주기 변경 — Interaction/ArcheryRenderer.cs가
        /// 구독해 과녁을 세우고 걷는다.</summary>
        public static event Action<ArcheryOverlayEvent> ArcheryOverlayChanged;

        /// <summary>활쏘기 한 발의 조준 시작/발사 통지 — 사전 확정된 도달점을 함께 싣는다.</summary>
        public static event Action<ArcheryShotEvent> ArcheryShotChanged;

        /// <summary>윈도우 창 도둑(27-1) 오버레이 생애주기 변경. Phase2+ 렌더링이 이 이벤트만 구독해
        /// 팔 IK/파티클 스폰-제거를 담당한다(지금은 트리거/취소 판정만 계산).</summary>
        public static event Action<WindowTheftOverlayEvent> WindowTheftOverlayChanged;

        /// <summary>화면 낙서 그라피티(27-3) 오버레이 생애주기 변경.</summary>
        public static event Action<GraffitiOverlayEvent> GraffitiOverlayChanged;

        /// <summary>바탕화면 청소부/블랙홀(27-2/27-5) 공용 복제 스프라이트 오버레이 생애주기 변경.</summary>
        public static event Action<DesktopIconMirrorOverlayEvent> DesktopIconMirrorOverlayChanged;

        /// <summary>윈도우 크래시(27-4) 크랙 오버레이 생애주기 변경 — 캐릭터의 짧은 해머 스윙 상태
        /// (StickmanStateId.WindowCrash)와 독립된 별도 수명(3초)이다.</summary>
        public static event Action<WindowCrashOverlayEvent> WindowCrashOverlayChanged;

        /// <summary>PC 하드웨어 반응(23/27-6절) 표시 상태 변경 — 동시에 최대 하나만 Active=true.</summary>
        public static event Action<HardwareReactionEvent> HardwareReactionChanged;

        /// <summary>투두 목록(17절)이 바뀌었을 때(추가/체크/취소/삭제/자동정리) 발생 — FootholdsChanged와
        /// 동일한 "변경 사실만 통지, 실제 데이터는 Core.TodoListModel에서 직접 조회" 패턴. UI(포스트잇
        /// 위젯)와 Interaction/TodoReminderDirector.cs가 함께 구독한다.</summary>
        public static event Action TodoListChanged;

        /// <summary>스트레스 게이지(19절) 값이 바뀌었을 때 발생(0~1, 클램프됨). 3단 노출(표정/트레이 점/
        /// 설정창 상세)은 지금 렌더링 레이어가 없어 아무도 구독하지 않아도 무해하다 — 트리거 조건 계산
        /// 자체를 지금 확정해두는 것이 목적(LandingRollRequested/WanderAmbientMotionRequested와 동일 패턴).</summary>
        public static event Action<float> StressLevelChanged;

        /// <summary>포모도로 감시자(18절) "딴짓 감지" 에스컬레이션 단계 변경 — Glance/WindowTap은 순수
        /// 앰비언트 신호(상태 전이 없음), Nudge는 StickmanStateId.FocusNudge 상태 전이와 별도로 "지금
        /// 몇 단계인지"를 렌더링 레이어에 알리기 위해 함께 발행된다. None은 즉시 리셋을 뜻한다.</summary>
        public static event Action<FocusWatchTier> FocusWatchTierChanged;

        /// <summary>가출(20절) 생애주기 변경 — 실제 사라짐/발견/화해 연출은 Phase2+ 렌더링 담당.</summary>
        public static event Action<RunawayLifecycleEvent> RunawayLifecycleChanged;

        /// <summary>가출 중 은신처 근처의 은은한 단서(흔들림/소리) 트리거 — Interaction/RunawayDirector.cs가
        /// runawayHintPulseIntervalSeconds 주기로 발행한다. 실제 연출은 Phase2+ 렌더링 담당.</summary>
        public static event Action<Vector2> RunawayHintPulseRequested;

        /// <summary>캐릭터 성장(레벨/XP/이름)이 바뀌었을 때 발생 — TodoListChanged와 동일한 "변경 사실만
        /// 통지, 실제 값은 Core.CharacterProgressionModel에서 직접 조회" 패턴. 소비자는 정보창
        /// (Interaction/CharacterInfoWindow.cs)이다. XP는 패시브로 계속 차오르므로 <b>수 초에 한 번씩
        /// 계속 발행된다</b> — 구독자는 이 이벤트마다 UI를 통째로 재구성하지 말 것(24시간 상주 앱).</summary>
        public static event Action CharacterProgressionChanged;

        /// <summary>장비 착용 상태가 바뀌었을 때 발생(착용/해제/레벨업으로 해제됨/저장파일 로드).
        /// 소비자는 액세서리 렌더러(Interaction/CharacterAccessoryRenderer.cs)와 정보창이다.</summary>
        public static event Action CharacterEquipmentChanged;

        /// <summary>
        /// 캐릭터 배율(크기)이 바뀌었을 때 발생 — <b>발행자는 Core/CharacterScaleController 하나뿐</b>이다
        /// (2026-09-01, 35-1-3 ①). 구석 호버 다이얼과 설정창 슬라이더는 <b>둘 다 구독자이자 발행자</b>이며,
        /// 어느 쪽에서 바꾸든 다른 쪽이 같은 프레임에 따라온다.
        ///
        /// <para>구독자 주의: 자기가 방금 요청해서 돌아온 값도 그대로 받는다(에코). 값이 같으면 아무것도
        /// 하지 않는 것으로 충분하다 — 다이얼/슬라이더 모두 <c>SetValue</c>가 같은 값이면 즉시 빠져나온다.
        /// 사용자가 <b>끌고 있는 중</b>인 컨트롤은 에코를 무시해야 한다(끄는 손을 되돌리게 된다).</para>
        /// </summary>
        public static event Action<CharacterScaleChangeEvent> CharacterScaleChanged;

        public static void RaiseStateTransitioned(StickmanStateId from, StickmanStateId to, bool isForcedInterrupt = false)
            => StateTransitioned?.Invoke(new StateTransitionEvent(from, to, isForcedInterrupt));

        public static void RaiseFootholdsChanged()
            => FootholdsChanged?.Invoke();

        public static void RaiseDialogueRequested(DialogueIntent intent)
            => DialogueRequested?.Invoke(intent);

        public static void RaiseDialogueExpired(DialogueIntent intent)
            => DialogueExpired?.Invoke(intent);

        public static void RaiseGlobalEmergencyStop()
            => GlobalEmergencyStopRequested?.Invoke();

        public static void RaiseWanderAmbientMotionRequested(WanderAmbientMotion motion)
            => WanderAmbientMotionRequested?.Invoke(motion);

        public static void RaiseLandingRollRequested(float fallHeight, Vector2 footWorldPosition)
            => LandingRollRequested?.Invoke(new LandingImpactEvent(fallHeight, footWorldPosition));

        public static void RaiseArcheryOverlayChanged(Vector2 targetWorld, float groundWorldY, float facing, SpectacleOverlayPhase phase)
            => ArcheryOverlayChanged?.Invoke(new ArcheryOverlayEvent(targetWorld, groundWorldY, facing, phase));

        public static void RaiseArcheryShotChanged(int shotIndex, ArcheryShotPhase phase, ArcheryShotResult result,
            Vector2 impactWorld, float flightSeconds)
            => ArcheryShotChanged?.Invoke(new ArcheryShotEvent(shotIndex, phase, result, impactWorld, flightSeconds));

        public static void RaiseWindowTheftOverlayChanged(Rect targetRectOsScreen, SpectacleOverlayPhase phase)
            => WindowTheftOverlayChanged?.Invoke(new WindowTheftOverlayEvent(targetRectOsScreen, phase));

        public static void RaiseGraffitiOverlayChanged(Rect regionOsScreen, SpectacleOverlayPhase phase)
            => GraffitiOverlayChanged?.Invoke(new GraffitiOverlayEvent(regionOsScreen, phase));

        public static void RaiseDesktopIconMirrorOverlayChanged(DesktopIconMirrorKind kind, IReadOnlyList<Rect> iconRectsOsScreen, SpectacleOverlayPhase phase)
            => DesktopIconMirrorOverlayChanged?.Invoke(new DesktopIconMirrorOverlayEvent(kind, iconRectsOsScreen, phase));

        public static void RaiseWindowCrashOverlayChanged(Rect targetRectOsScreen, SpectacleOverlayPhase phase)
            => WindowCrashOverlayChanged?.Invoke(new WindowCrashOverlayEvent(targetRectOsScreen, phase));

        public static void RaiseHardwareReactionChanged(HardwareReactionKind kind, bool active)
            => HardwareReactionChanged?.Invoke(new HardwareReactionEvent(kind, active));

        public static void RaiseTodoListChanged()
            => TodoListChanged?.Invoke();

        public static void RaiseStressLevelChanged(float level)
            => StressLevelChanged?.Invoke(level);

        public static void RaiseFocusWatchTierChanged(FocusWatchTier tier)
            => FocusWatchTierChanged?.Invoke(tier);

        public static void RaiseRunawayLifecycleChanged(RunawayLifecyclePhase phase, Vector2 hideSpotOsScreen)
            => RunawayLifecycleChanged?.Invoke(new RunawayLifecycleEvent(phase, hideSpotOsScreen));

        public static void RaiseRunawayHintPulseRequested(Vector2 hideSpotOsScreen)
            => RunawayHintPulseRequested?.Invoke(hideSpotOsScreen);

        public static void RaiseCharacterProgressionChanged()
            => CharacterProgressionChanged?.Invoke();

        public static void RaiseCharacterEquipmentChanged()
            => CharacterEquipmentChanged?.Invoke();

        public static void RaiseCharacterScaleChanged(float value, string reason, bool appliedToCharacter)
            => CharacterScaleChanged?.Invoke(new CharacterScaleChangeEvent(value, reason, appliedToCharacter));
    }
}
