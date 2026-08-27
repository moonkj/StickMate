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

        // ==== Phase 3 (docs/UX_FLOW.md 10/12/13절) — 커서 상호작용/전투 미니게임 ====

        /// <summary>격파 미니게임(10절): 기 모으기 게이지 → 스위트스팟 클릭 판정 → 성공/실패(최대
        /// battleMaxRetries회 재도전)/5초 무입력 타임아웃. Idle/Walk에서 진입, 정상 종료 시 Idle 복귀.</summary>
        BattleMinigame,

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

    /// <summary>UX_FLOW.md 10절 격파 미니게임 한 차례 시도의 결과. StickmanEventBus가 트리거 조건만
    /// 발행하고(실제 파티클/파괴 연출은 Phase 2+ 렌더링 담당, WanderAmbientMotionRequested와 동일 패턴),
    /// Success/Exhausted는 상태 종료(Idle 복귀)로 이어지고 Fail은 같은 상태 안에서 재시도로 이어진다.</summary>
    public enum BattleMinigamePhase
    {
        Success,
        Fail,
        Exhausted,
    }

    /// <summary>UX_FLOW.md 11절 라이벌 스틱맨 대결의 종료 결과.</summary>
    public enum RivalDuelResult
    {
        PlayerWon,
        RivalWon,
        Draw,
    }

    /// <summary>
    /// docs/UX_FLOW.md 26-3절 "살아있는 느낌" 디테일 — AutoWanderController가 타이밍/확률 조건만 판정해
    /// 발행하는 유휴 연출 신호. 실제 애니메이션 재생은 Phase 2+ 렌더링 레이어가 이 이벤트를 구독해 담당한다
    /// (지금은 아무도 구독하지 않아도 무해 — 트리거 조건 계산 자체는 지금 확정해두는 것이 목적).
    /// </summary>
    public enum WanderAmbientMotion
    {
        /// <summary>Idle 진입 후 1.0~2.5초 랜덤 지연 뒤 발동, 0.6~1.0초 지속(26-3).</summary>
        LookAround,

        /// <summary>"Idle 연장"이 연속 3회 이상 선택된 경우에만 15% 확률로 발동, 1.5~2.5초 지속(26-3).</summary>
        SitAndYawn,
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
        /// FallState가 착지를 확정한 순간, 낙하 높이가 StickConfig.rollLandingHeightThreshold 이상이었을
        /// 때 발생(UX_FLOW.md 4절 "구르기(ROLL)"). 페이로드는 실제 낙하 높이(월드 유닛). 실제 구르기
        /// 파티클/애니메이션 재생은 Phase 2+ 렌더링 레이어가 이 이벤트를 구독해 담당한다 — 지금은 아무도
        /// 구독하지 않아도 무해(트리거 조건 계산 자체가 지금 확정해두는 목적).
        /// </summary>
        public static event Action<float> LandingRollRequested;

        /// <summary>격파 미니게임(10절) 한 차례 시도의 결과가 확정되었을 때 발생 — 실제 파괴/코믹리액션
        /// 연출은 Phase 2+ 렌더링 레이어가 이 이벤트를 구독해 담당한다(지금은 트리거 조건만 계산).</summary>
        public static event Action<BattleMinigamePhase> BattleMinigamePhaseChanged;

        /// <summary>라이벌 스틱맨 대결(11절)이 시작되었을 때 발생 — 등장 연출/조우 대사 트리거용
        /// (지금은 구독자 없음, Phase 2+ 렌더링 레이어 몫).</summary>
        public static event Action RivalDuelStarted;

        /// <summary>라이벌 스틱맨 대결이 종료되었을 때 발생(승/패/무승부). 트레이 UI의 "대결 중" 배지
        /// 해제 등에 사용 예정(지금은 구독자 없음).</summary>
        public static event Action<RivalDuelResult> RivalDuelEnded;

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

        public static void RaiseLandingRollRequested(float fallHeight)
            => LandingRollRequested?.Invoke(fallHeight);

        public static void RaiseBattleMinigamePhaseChanged(BattleMinigamePhase phase)
            => BattleMinigamePhaseChanged?.Invoke(phase);

        public static void RaiseRivalDuelStarted()
            => RivalDuelStarted?.Invoke();

        public static void RaiseRivalDuelEnded(RivalDuelResult result)
            => RivalDuelEnded?.Invoke(result);

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
    }
}
