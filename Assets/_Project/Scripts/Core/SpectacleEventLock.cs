using StickMate.Platform;
using StickMate.States;

namespace StickMate.Core
{
    /// <summary>드래그&던지기/로데오 커서/창 도둑… — 어느 것이 이 락을 걸고 있는지.
    /// <para>★ 2026-09-02 <c>BattleMinigame</c>을 뺐다(격파 놀이 기능 삭제, 사용자 지시). 이 enum은
    /// 어디에도 직렬화되지 않으므로 값이 밀려도 저장 파일에 영향이 없다.</para></summary>
    public enum SpectacleEventKind
    {
        DragAndThrow,
        RodeoCursor,

        // ==== Phase 4 (docs/UX_FLOW.md 27절/28절-29) — 기존 4종과 동일한 "한 번에 하나만" 상호배제
        // 세트에 신규 편입. DesktopTidy/BlackholeSummon은 같은 전역 락을 공유하는 것만으로 27-2/27-5가
        // 요구하는 "둘 사이의 더 강한 상호배제"도 자동으로 충족된다(별도 락 불필요). ====
        WindowTheft,
        Graffiti,
        DesktopTidy,
        BlackholeSummon,
        WindowCrash,

        // ==== Phase 5 (docs/UX_FLOW.md 17~20절) — Coder 판단 기록(Tasklist.md 교차 레이어 로그에도
        // 동일 근거 기록): 이 락에 참여시킬지 여부는 "StickmanStateMachine.ChangeState()를 직접
        // 호출해 단일 상태 슬롯을 두고 경쟁하는가"로 판단했다. Interaction/HardwareReactionDirector.cs는
        // ChangeState를 전혀 호출하지 않고(현재 상태 위에 얹는 순수 오버레이 신호) 이 락에 참여하지
        // 않는 것이 승인된 선례인데, 아래 5개는 전부 ChangeState를 호출해 다른 스펙터클과 같은 단일
        // 상태 슬롯을 다툴 수 있으므로 하드웨어 반응과 달리 이 락에 참여시킨다. ====

        /// <summary>투두 '들고 다니는 모드' 리마인더(17절) — ChangeState(TodoReminder) 호출.</summary>
        TodoReminder,

        /// <summary>포모도로 감시자 시작/종료/2단계 리마인드 포즈(18절) — FocusStart/FocusComplete/
        /// FocusCancelled/FocusNudge 4개 상태가 모두 이 하나의 kind를 공유한다(서로 겹칠 일이 없는
        /// 순차적 생애주기이므로 세분화할 실익이 없다).</summary>
        FocusPose,

        /// <summary>SULKY(19절) — ChangeState(Sulky) 호출. 하드웨어 반응과 달리 실제 상태 슬롯을 쓰므로
        /// 참여시킨다(Tasklist.md 교차 레이어 로그 판단 근거 참고).</summary>
        Sulky,

        /// <summary>가출(20절) — UX_FLOW.md 25절-20이 명시적으로 요구: "가출 상태는 16절-15의 상호배제
        /// 세트(10/11/13/14절)에 포함되어야 한다." 다른 5개와 달리 수 시간 지속될 수 있어 락을 가장
        /// 오래 붙들 수 있는 항목이다.</summary>
        Runaway,

        // ==== 활쏘기(2026-08-29 사용자 요청 "과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동") ====

        /// <summary>활쏘기(States/ArcheryState.cs) — 이 락에 참여시키는 기준은 다른 항목과 같다:
        /// "StickmanStateMachine.ChangeState()를 직접 호출해 단일 상태 슬롯을 다투는가". 활쏘기는
        /// Idle/Walk에서 StickmanStateId.Archery로 상태를 전이시키므로(ChangeState를 전혀 호출하지 않아
        /// 비참여가 승인된 HardwareReaction/StressGauge와 다르다) 참여가 맞다. 한 사이클이 4초 안팎으로
        /// 짧아 락을 오래 붙들지 않는다.</summary>
        Archery,

        // ==== 음악 반응 춤(2026-09-03 사용자 요청 "노래가 나오면 상호 반응해서 춤추는 동작") ====

        /// <summary>음악 반응 춤 <b>에피소드 1회분</b>(States/DanceState.cs) — 참여 기준은 다른
        /// 항목과 같다("ChangeState()로 단일 상태 슬롯을 다투는가"). Idle/Walk에서
        /// StickmanStateId.Dance로 전이하므로 참여한다.
        /// <para>★ <b>락을 잡는 단위가 「음악이 나오는 동안」이 아니라 「에피소드」인 것이 핵심</b>이다.
        /// 3시간짜리 플레이리스트 내내 이 락을 잡으면 활쏘기·그라피티·창 도둑·청소부·블랙홀·
        /// 창 크래시·투두·SULKY·가출·포모도로 포즈가 <b>전부 발동 불가</b>가 된다. 그래서 휴지
        /// 구간에는 락도 상태도 잡지 않는다(docs/UX_MOTION_DANCE.md 4절).</para></summary>
        Dance,
    }

    /// <summary>
    /// ★★★ 2026-09-28 — <b><see cref="SpectacleEventLock.TryAcquire(SpectacleEventKind, object)"/>가 거짓을 내는 사유</b>.
    ///
    /// <para><b>왜 생겼나</b>: 그 <c>bool</c> 하나가 서로 전혀 다른 두 사실에 <b>똑같이 <c>false</c></b>를 냈다 —
    /// 「남이 쥐고 있다」와 「보존 동결이 <b>빈 락의</b> 신규 획득을 막는다」. 이 저장소가 반복해서 당한
    /// <b>「실패한 측정과 성공한 측정이 똑같이 생겼다」의 프로덕션판</b>이고, 실제 피해는 호출부가 로그에
    /// <b>거짓을 적는</b> 것이었다(<c>FocusWatchDirector.DescribePoseGate</c>가 동결에 막힌 순간에도
    /// <i>「관문은 지금 열려 있습니다」</i>라고 말했다).</para>
    ///
    /// <para>★ <b>이 enum은 어디에도 직렬화되지 않는다</b> — <see cref="SpectacleEventKind"/>와 같은 사정이라
    /// 값이 밀려도 저장 파일에 영향이 없다. 다만 <see cref="None"/>은 <b>0으로 고정</b>한다: 「사유 없음 = 성공」이
    /// <c>default</c>와 같아야 <c>out</c> 인자를 받는 호출부가 초기화를 빠뜨려도 거짓 사유를 보고하지 않는다.</para>
    /// </summary>
    public enum SpectacleLockDenialReason
    {
        /// <summary>거절되지 않았다(획득 성공, 또는 지금 획득할 수 있다).</summary>
        None = 0,

        /// <summary>소유자 토큰이 <c>null</c>이다 — 배선 사고다. 락 상태와 무관하다.</summary>
        NullOwner,

        /// <summary>다른 소유자가 이미 쥐고 있다. <b>같은</b> 소유자의 재진입은 거절이 아니다.</summary>
        HeldByOther,

        /// <summary>락은 <b>비어 있는데</b> 화면 변경 유예(<see cref="CharacterPreservationFreeze.BlocksNewSpectacle"/>)가
        /// 신규 획득을 막는다. 이미 쥔 주인의 재진입에서는 <b>절대 나오지 않는다</b> — 진행 중 연출을 끊지 않는
        /// 것이 그 동결의 계약이기 때문이다.</summary>
        PreservationFreeze,
    }

    /// <summary>
    /// docs/UX_FLOW.md 16절-15 "모든 방해성/스펙터클 이벤트는 서로 상호 배제 락이 필요하다"의 구현체.
    /// 한 번에 하나의 스펙터클/개입 이벤트만 활성화되도록 강제하는 전역 단일 소유자 락.
    /// ★ 2026-09-14 — 화면 변경 유예(<see cref="CharacterPreservationFreeze.BlocksNewSpectacle"/>) 중에는 락이 비어 있어도
    /// <b>새 획득</b>을 거절한다. 이미 쥔 주인의 재진입과 해제는 막지 않는다(진행 중 연출을 끊지 않는다).
    /// StickmanEventBus와 같은 이유(24시간 상주 앱, 레이어 간 결합 최소화, 씬 생명주기와 무관한 정적
    /// 상태)로 정적 클래스로 구현한다.
    ///
    /// Platform.ILocalClickCaptureService/LocalClickCaptureGate의 "부분적 클릭관통 해제 단일 소유자
    /// 락"(15절-4)과는 목적이 다른 별개의 락이다 — 이 락은 "한 번에 하나의 스펙터클 이벤트만"을
    /// 강제하고, 저 락은 "한 번에 하나만 캐릭터 클릭을 가로챌 수 있음"을 강제한다. 로데오 커서(13절)는
    /// 클릭을 전혀 쓰지 않으므로(15절 대상 아님) 이 스펙터클 락만 걸면 되고, 드래그&던지기는
    /// 이 락과 저 락을 둘 다 걸어야 한다(오너 토큰은 보통 같은 object를 재사용).
    /// </summary>
    public static class SpectacleEventLock
    {
        private static object _owner;
        private static SpectacleEventKind _activeKind;

        public static bool IsActive => _owner != null;
        public static SpectacleEventKind ActiveKind => _activeKind;
        public static object CurrentOwner => _owner;

        /// <summary>
        /// 지금 <paramref name="owner"/>가 획득을 시도하면 <b>왜</b> 거절되는가. 거절되지 않으면
        /// <see cref="SpectacleLockDenialReason.None"/>. <b>순수 조회 — 락을 건드리지 않는다.</b>
        ///
        /// <para>★ <b>왜 판정을 여기 한 곳에 모으는가</b>: <see cref="TryAcquire(SpectacleEventKind, object)"/>와
        /// 「지금 왜 막혔나」를 말하는 로그가 <b>서로 다른 자로</b> 재면 반드시 한쪽이 오탐한다. 실제로 그랬다 —
        /// <c>FocusWatchDirector</c>는 <see cref="IsActive"/>와 <see cref="CurrentOwner"/>만 봤기 때문에
        /// 보존 동결에 막힌 경우를 <i>「관문은 지금 열려 있습니다」</i>로 보고했다. 그래서 판정은 이 함수
        /// 하나이고 <see cref="TryAcquire(SpectacleEventKind, object, out SpectacleLockDenialReason)"/>도
        /// <b>이것을 부른다</b>.</para>
        ///
        /// <para><paramref name="kind"/>를 받지 않는 것은 <b>의도</b>다 — 거절 사유는 종류에 의존하지 않는다.
        /// 인자로 받으면 다음 사람이 「종류마다 다를 수 있다」고 읽는다.</para>
        ///
        /// <para>할당 0(enum 반환). 24시간 상주 앱이라 이 함수는 로그 경로에서 매 프레임 불릴 수 있다.</para>
        /// </summary>
        public static SpectacleLockDenialReason EvaluateAcquire(object owner)
        {
            if (owner == null) return SpectacleLockDenialReason.NullOwner;
            if (_owner != null && _owner != owner) return SpectacleLockDenialReason.HeldByOther;
            // ★ 2026-09-14 — 화면 변경 유예(보존 동결) 중에는 <b>새</b> 연출을 시작하지 않는다. 상태 Tick이 멈춰
            //   있어 시작해도 진행하지 못하고, 그 전이의 대사는 사용자가 못 본 행동에 붙는다(원칙 1).
            //   이미 쥔 주인의 재진입은 막지 않는다 — 진행 중 연출을 끊지 않는 것이 이 동결의 계약이다.
            if (_owner == null && CharacterPreservationFreeze.BlocksNewSpectacle)
                return SpectacleLockDenialReason.PreservationFreeze;
            return SpectacleLockDenialReason.None;
        }

        /// <summary>이미 다른 소유자가 점유 중이면 false. 같은 owner가 다시 요청하면(재진입) true.
        /// 락이 비어 있어도 화면 변경 유예 중이면 새 획득은 false(재진입은 true).
        /// <para>★ <b>사유가 필요하면</b>
        /// <see cref="TryAcquire(SpectacleEventKind, object, out SpectacleLockDenialReason)"/>를 쓴다. 이
        /// 2인자 형태를 남겨 둔 이유는 호출부 대부분이 <b>그냥 물러나면 되는</b> 자리이기 때문이고
        /// (사유를 받아 버리면 쓰지 않는 변수가 생긴다), 그 자리들을 건드리지 않는 것이 이 보강의 범위다.</para></summary>
        public static bool TryAcquire(SpectacleEventKind kind, object owner)
            => TryAcquire(kind, owner, out _);

        /// <summary>
        /// 획득을 시도하고 <b>실패하면 그 사유</b>를 돌려준다. 성공하면
        /// <paramref name="reason"/>은 <see cref="SpectacleLockDenialReason.None"/>이다.
        ///
        /// <para>★ <b>왜 「마지막 실패 사유」 정적 프로퍼티가 아닌가</b>(기각한 대안): 이 락은 정적이고
        /// 소비자가 여러 Director다. 마지막 사유를 필드에 남기면 <b>A가 읽기 전에 B의 시도가 덮어쓴다</b> —
        /// 24시간 상주 앱에서 재현 불가능한 오보고가 되고, 「사유를 구분한다」는 목적 자체가 무너진다.
        /// <c>out</c>은 호출자 스택에 남으므로 그 경합이 원리적으로 없다.</para>
        /// </summary>
        public static bool TryAcquire(SpectacleEventKind kind, object owner, out SpectacleLockDenialReason reason)
        {
            reason = EvaluateAcquire(owner);
            if (reason != SpectacleLockDenialReason.None) return false;
            _owner = owner;
            _activeKind = kind;
            return true;
        }

        /// <summary>사유 한 줄(로그·진단 전용).
        /// <para>★ 글자를 <b>여기 한 곳에</b> 두는 이유는 <see cref="StickMateDisplayNames"/>·
        /// <see cref="HiddenCharacterCommandGate.HiddenReason"/>과 같다 — 호출부마다 적으면 같은 상황에
        /// 다른 말이 뜨고, 테스트가 문자열을 <b>베끼는</b> 대신 이 함수를 참조할 수 있어야 한다.</para>
        /// <para>★ <b>사용자에게 보이는 문구가 아니다.</b> 타일이 회색인 이유처럼 화면에 뜨는 글자는
        /// <c>design-narrative</c> 소관이고 그쪽 창구는 <see cref="CommandAvailability"/>다.</para>
        /// <para>리터럴을 그대로 돌려주므로 할당이 없다.</para></summary>
        public static string Describe(SpectacleLockDenialReason reason)
        {
            switch (reason)
            {
                case SpectacleLockDenialReason.NullOwner:
                    return "소유자 토큰이 null입니다(배선 사고 — 락 상태와 무관합니다)";
                case SpectacleLockDenialReason.HeldByOther:
                    return "다른 연출이 이미 쥐고 있습니다";
                case SpectacleLockDenialReason.PreservationFreeze:
                    return "화면 변경 유예(보존 동결) 중이라 새 연출을 시작하지 않습니다";
                default:
                    return "거절 사유가 없습니다(지금 획득할 수 있습니다)";
            }
        }

        /// <summary>소유자 본인만 해제할 수 있다. 이미 해제됐거나 소유자가 아니면 no-op.</summary>
        public static void Release(object owner)
        {
            if (_owner == null || _owner != owner) return;
            _owner = null;
        }

        /// <summary>
        /// 개선 R2(docs/CODE_REVIEW_FINAL.md "SpectacleEventLock 해제 보일러플레이트" 지적 대응) —
        /// Director들의 OnDisable() 등이 각자 손으로 반복해온 3단계(소유권 확인 → 필요시 강제 Idle
        /// 전이 → Release(+옵션으로 ILocalClickCaptureService 해제))를 추출한 공용 헬퍼.
        ///
        /// ★ 2026-08-30 R3-m1 — 개수 정정. **지금 이 메서드를 부르는 파일은 11개**다:
        /// ArcheryDirector / DesktopIconMirrorDirector / DragThrowController /
        /// FocusWatchDirector / GraffitiDirector / RodeoCursorWatcher / RunawayDirector /
        /// StressGaugeDirector / TodoReminderDirector / WindowCrashDirector / WindowTheftDirector.
        /// (2026-09-02 격파 놀이 삭제로 BattleMinigameDirector가 빠져 12 → 11이 됐다.)
        /// 아래 "11곳"은 <b>추출 당시(개선 R2)의 숫자</b>이고 그대로 둔다 — 그때의 분류 근거이기
        /// 때문이다. 그 11곳과 지금의 11곳은 <b>구성이 다르다</b>: ArcheryDirector는 그 뒤
        /// (2026-08-29 활쏘기 연출 라운드)에 **처음부터 이 헬퍼를 쓰며** 태어나 아래 8/3 분류에는
        /// 애초에 등장하지 않고, 그 자리를 채우고 있던 BattleMinigameDirector는 이제 없다.
        ///
        /// 값을 고정한 이유: fallback 상태는 항상 <see cref="StickmanStateId.Idle"/>, 전이는 항상
        /// isForcedInterrupt:true — 11곳 전부 예외 없이 이 두 값을 쓴다(파라미터로 열어두지 않는다,
        /// 과설계 방지). clickCapture는 옵션(기본 null) — DragThrowController 1곳만 실제로 넘긴다.
        ///
        /// 소유권 확인을 항상 먼저 하는 이유: 추출 당시 11곳 중 8곳(GraffitiDirector/TodoReminderDirector/
        /// RunawayDirector/WindowTheftDirector/DesktopIconMirrorDirector/RodeoCursorWatcher/
        /// StressGaugeDirector/FocusWatchDirector)은 원래도 이 가드가 있었다.
        /// (당시) BattleMinigameDirector/DragThrowController/WindowCrashDirector 3곳은 원래 이 가드
        /// 없이 상태 비교만 했지만, 세 곳 모두 "SpectacleEventLock.TryAcquire 성공 직후에만 guardedState로
        /// ChangeState한다"는 불변식을 코드 전체에서 예외 없이 지킨다(다른 어떤 컴포넌트도 이 세
        /// state로 전이하지 않는다) — 즉 CurrentStateId==guardedState이면 항상 CurrentOwner==owner이기도
        /// 하므로, 이 가드를 추가해도 실제로 관찰 가능한 동작은 전혀 달라지지 않는다(Tasklist.md 개선
        /// R2 절에 근거 기록).
        ///
        /// 이 헬퍼로 흡수하지 않은 1곳: FocusWatchDirector(단일 상태가 아니라 4개 상태 중
        /// 하나인지(IsFocusPoseState)를 확인하는 커스텀 가드라 단일 StickmanStateId 파라미터로
        /// 표현할 수 없음) — 억지로 끼워맞추지 않고 자기 정리 로직을 유지한다.
        /// </summary>
        public static void ReleaseIfOwned(
            object owner,
            StickmanStateMachine machine,
            StickmanStateId guardedState,
            ILocalClickCaptureService clickCapture = null)
        {
            if (_owner == null || _owner != owner) return;
            if (machine != null && machine.CurrentStateId == guardedState)
            {
                machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
            clickCapture?.ReleaseLocalClickCapture(owner);
            Release(owner);
        }
    }
}
