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
    /// 대사를 만드는 상태는 Attack/Ragdoll/ParkourClimb/LedgeHang/WindowTheft/Runaway 등
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
    ///   4) ★ 2026-09-06 — 어떤 줄은 **그 장면에서만 참**이다. 그런 줄은 <see cref="IsLineEligible"/>가
    ///      후보에서 빼고, 난수는 <b>자격 있는 줄 사이에서만</b> 돈다. 즉 "말할지 말지"(확률·쿨다운)
    ///      뒤에 "무엇이 참인가"(자격)가 오고, 그 뒤에야 텍스트가 만들어진다.
    ///   5) ★ 2026-09-06 — 자격 축이 <b>셋</b>이다: 모션 / 요일 / 시간대. 뒤의 둘은 벽시계에서 오는데,
    ///      <see cref="IsLineEligible"/>는 <b>시계를 읽지 않는다</b> —
    ///      <see cref="AmbientCalendarClock"/>가 60초 캐시로 뜬 스냅샷을
    ///      <see cref="TryRollChatter"/>가 <b>추첨 시작 시점에 한 번</b> 받아 그대로 넘긴다.
    ///      그래야 (가) 순수성이 유지되고 (나) 한 번의 추첨이 <b>하나의 시각</b>만 본다.
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

        /// <summary>IDLE 혼잣말 — "지금 멈춰 서 있다"는 현재 상황에 대한 서술만 담는다.
        ///
        /// ★ 2026-09-06 — 여기 세 번째 줄에 <b>"심심하다"</b>가 있었다. <b>삭제했다</b>
        /// (design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md §3-2 판정).
        /// 두 가지가 동시에 틀렸다: (1) 이 저장소에 <b>지루함 모델이 없다</b> — Idle은 접지 + 정지라는
        /// 물리적 사실뿐이고 <c>AutoWanderController._consecutiveIdleExtensions</c>조차 블랙보드에
        /// 노출돼 있지 않으므로, 상태가 알 수 없는 것을 주장한다. (2) <b>유저에 대한 주장</b>이다 —
        /// "네가 안 놀아준다"로 읽히고, 비침해·관찰형 앱이 확인할 수 없는 관계를 지어낸다
        /// (민지 페르소나 실측: "심심한 건 앱이 아니라 접니다").
        /// 경계선: <c>"금요일이다!"</c> 같은 감정은 살린다. 금지된 것은 감정이 아니라
        /// <b>유저와의 관계에 대한 주장</b>이다.</summary>
        private static readonly string[] IdleLines =
        {
            "음...",
            "여기 좋네",
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

            // ────────────────────────────────────────────────────────────────────
            // ★ 2026-09-06 요일 축 — R2 §3-4 #13·#14·#15
            // ────────────────────────────────────────────────────────────────────
            // 세 줄 다 <b>날짜만 말한다</b>. 요일이 바꾸는 것은 달력뿐이고 이 저장소의 어떤 값도
            // 요일을 보지 않으므로(보행 속도·자세·물리 전부), 날짜 밖의 주장을 하면 그 순간 거짓이 된다.
            "월요일이네...",
            "금요일이다!",
            "쉬는 날이네",

            // ────────────────────────────────────────────────────────────────────
            // ★ 2026-09-06 시간대 축 5구간 — R2 §3-4 #19·#20·#21 + 2026-09-06_시간대5구간 §1
            // ────────────────────────────────────────────────────────────────────
            // 순서는 AmbientTimeBucket 선언 순서(아침→점심→오후→저녁→밤)와 같게 둔다. 자격 축이
            // 병렬 배열이라 눈으로 대조하는 것이 유일한 1차 방어이고, 순서가 어긋나면 그 대조가 죽는다.
            //
            // ★ 이 다섯 줄이 <b>전부</b> 있어야 계약이 성립한다(R23). 최악 조합(화·수·목 × 모션 없음)의
            //   후보 수 N은 상시 10 + 시간대 2뿐이고, 한 구간이라도 비면 그 시간대에 N=10으로 떨어져
            //   중복 확률이 42.7% → 49.6%로 올라가 계약 여유가 +7.3pp에서 +0.4pp가 된다.
            //   즉 「신규 오후·저녁만 넣고 기존 아침·점심·밤을 미룬다」는 갈래는 존재하지 않는다.
            "아침이네",
            "점심시간이네",
            "아직 오후네",
            "저녁은 느긋하네",
            "밤이 깊었네",
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

            // ────────────────────────────────────────────────────────────────────
            // ★ 2026-09-06 요일 축 — R2 §3-4 #16·#17·#18
            // ────────────────────────────────────────────────────────────────────
            // ★★ 금요일 Walk 자리에 대해: 선행 라운드(2026-09-02_대사체계_실측과_계약.md §5-2)가
            //    제안한 «발이 빨라지네»는 <b>R2 §3-5가 이미 기각했다</b>. 이유가 둘이다 —
            //    (가) 보행 속도는 요일에 따라 실제로 안 변한다(WalkState의 어떤 값도 요일을 안 본다),
            //    (나) 같은 금요일에 상시 줄 «다리가 잘 나가네»와 나란히 나오면 정면 모순이다.
            //    그래서 이 자리의 정본은 개정안 #17 «주말이 코앞이네»이고, 이 줄은 <b>날짜만</b> 말한다.
            //    → 배선 대상에서 빠진 것은 «발이 빨라지네»이고, 금요일 Walk 자리는 비어 있지 않다.
            "월요일이 왔네",
            "주말이 코앞이네",
            "주말 산책이네",

            // ────────────────────────────────────────────────────────────────────
            // ★ 2026-09-06 시간대 축 5구간 — R2 §3-4 #22·#23·#24 + 2026-09-06_시간대5구간 §1
            // ────────────────────────────────────────────────────────────────────
            // Idle 표와 <b>같은 순서</b>(아침→점심→오후→저녁→밤). 모든 구간이 Idle 1 + Walk 1을 갖는
            // 것이 R23의 「전구간 커버 = 항상 자격 2」 요구다.
            //
            // ★ 「저녁엔 천천히 걷네」류를 쓰지 않은 이유는 위 금요일 건과 같다 — 보행 속도는 시간대에
            //   따라 바뀌지 않으므로 화면이 즉시 반증한다. 여기 네 줄은 시각·하루의 위치만 말한다.
            "아침 산책이네",
            "점심때 걷네",
            "오후가 지나가네",
            "하루를 마무리하네",
            "밤에도 걷네",
        };

        // ============================================================================
        // ★ 자격 술어 축 (2026-09-06) — "그 장면이 아니면 그 줄은 후보에 없다"
        // ============================================================================
        // design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md §3-3의 판정을 착지시킨 것이다.
        // 그 전까지 TryRollChatter는 표 전체에서 균등 추첨했고 자격 술어가 하나도 없어서,
        // **하품하지 않으면서 "하암...", 두리번거리지 않으면서 "구경 중이야"**가 나왔다(원칙 1 위반).
        //
        // 구조는 선행 라운드(2026-09-02_대사체계_실측과_계약.md §5-1)가 정한 그대로다:
        //   TryRollChatter — ① 지금 자격 있는 줄만 센다 ② 그 안에서 난수 ③ **절대 인덱스**를 스냅샷
        //   Resolve        — 스냅샷된 절대 인덱스로 전체 표를 조회(순수 함수 유지, 시계를 다시 안 읽는다)
        // 상대 인덱스를 저장하면 Resolve가 필터를 재현하려고 모션 상태를 다시 읽어야 하고, 그 순간
        // "이 텍스트가 어느 Enter()의 어느 스냅샷에서 나왔는가"라는 역추적 계약이 깨진다.

        // ★ 2026-09-06 두 번째 축 확장 — 축이 <b>셋</b>이 됐다(모션 / 요일 / 시간대).
        //   셋은 서로 독립이고 AND로 묶인다. 한 줄에 두 축이 동시에 걸린 경우는 지금 없고,
        //   앞으로도 만들지 마라 — 두 축이 한 문장에 섞이면 「무엇이 참이라 이 말을 했는가」가
        //   흐려지고, 그 순간 원칙 1의 역추적(스냅샷 → 텍스트)이 의미를 잃는다.
        //   그래서 아래 세 요구 축은 <b>표당 3개의 병렬 배열</b>로 나란히 두고, 길이 일치와
        //   「한 줄에 축은 최대 하나」를 Tests/EditMode/AmbientChatterCalendarTests가 함께 잠근다.

        /// <summary><see cref="IdleLines"/>와 <b>같은 길이·같은 순서</b>의 모션 자격 축.
        /// null이면 이 축을 안 보고, 값이 있으면 <b>그 모션이 지금 재생 중일 때만</b> 후보가 된다.
        /// (길이 일치는 <c>Tests/EditMode/AmbientChatterEligibilityTests</c>가 잠근다 —
        /// 표만 늘리고 여기를 잊으면 새 줄이 조용히 상시 자격을 얻는다.)</summary>
        private static readonly WanderAmbientMotion?[] IdleLineMotionRequirement =
        {
            null,                            // 음...
            null,                            // 여기 좋네
            null,                            // 잠깐 쉬는 중
            null,                            // 오늘 뭐 하지
            WanderAmbientMotion.SitAndYawn,  // 하암...
            null,                            // 발밑이 단단해
            WanderAmbientMotion.LookAround,  // 구경 중이야
            null,                            // 월요일이네...
            null,                            // 금요일이다!
            null,                            // 쉬는 날이네
            null,                            // 아침이네
            null,                            // 점심시간이네
            null,                            // 아직 오후네
            null,                            // 저녁은 느긋하네
            null,                            // 밤이 깊었네
        };

        /// <summary><see cref="IdleLines"/>와 같은 길이·순서의 <b>요일</b> 자격 축.
        /// <see cref="AmbientDayBucket.None"/>이면 이 축을 안 본다.</summary>
        private static readonly AmbientDayBucket[] IdleLineDayRequirement =
        {
            AmbientDayBucket.None,      // 음...
            AmbientDayBucket.None,      // 여기 좋네
            AmbientDayBucket.None,      // 잠깐 쉬는 중
            AmbientDayBucket.None,      // 오늘 뭐 하지
            AmbientDayBucket.None,      // 하암...
            AmbientDayBucket.None,      // 발밑이 단단해
            AmbientDayBucket.None,      // 구경 중이야
            AmbientDayBucket.Monday,    // 월요일이네...
            AmbientDayBucket.Friday,    // 금요일이다!
            AmbientDayBucket.Weekend,   // 쉬는 날이네
            AmbientDayBucket.None,      // 아침이네
            AmbientDayBucket.None,      // 점심시간이네
            AmbientDayBucket.None,      // 아직 오후네
            AmbientDayBucket.None,      // 저녁은 느긋하네
            AmbientDayBucket.None,      // 밤이 깊었네
        };

        /// <summary><see cref="IdleLines"/>와 같은 길이·순서의 <b>시간대</b> 자격 축.
        /// <see cref="AmbientTimeBucket.None"/>이면 이 축을 안 본다.</summary>
        private static readonly AmbientTimeBucket[] IdleLineTimeRequirement =
        {
            AmbientTimeBucket.None,       // 음...
            AmbientTimeBucket.None,       // 여기 좋네
            AmbientTimeBucket.None,       // 잠깐 쉬는 중
            AmbientTimeBucket.None,       // 오늘 뭐 하지
            AmbientTimeBucket.None,       // 하암...
            AmbientTimeBucket.None,       // 발밑이 단단해
            AmbientTimeBucket.None,       // 구경 중이야
            AmbientTimeBucket.None,       // 월요일이네...
            AmbientTimeBucket.None,       // 금요일이다!
            AmbientTimeBucket.None,       // 쉬는 날이네
            AmbientTimeBucket.Morning,    // 아침이네
            AmbientTimeBucket.Lunch,      // 점심시간이네
            AmbientTimeBucket.Afternoon,  // 아직 오후네
            AmbientTimeBucket.Evening,    // 저녁은 느긋하네
            AmbientTimeBucket.Night,      // 밤이 깊었네
        };

        /// <summary><see cref="WalkLines"/>와 같은 길이·순서의 <b>요일</b> 자격 축.
        /// <para>★ Walk 표에는 여전히 <b>모션 축이 없다</b> — 모션(앉기하품/두리번)은 Idle 전용 연출이라
        /// 걷는 중에는 정의상 재생되지 않는다. 그래서 Walk 쪽 모션 배열을 만들지 않았고,
        /// <see cref="MotionRequirementFor"/>가 Walk에서 즉시 null을 돌려주는 것이 그 사실의 표현이다.</para></summary>
        private static readonly AmbientDayBucket[] WalkLineDayRequirement =
        {
            AmbientDayBucket.None,      // 산책 중
            AmbientDayBucket.None,      // 저쪽으로 가볼까
            AmbientDayBucket.None,      // 하나 둘 하나 둘
            AmbientDayBucket.None,      // 다리 좀 풀자
            AmbientDayBucket.None,      // 다리가 잘 나가네
            AmbientDayBucket.Monday,    // 월요일이 왔네
            AmbientDayBucket.Friday,    // 주말이 코앞이네
            AmbientDayBucket.Weekend,   // 주말 산책이네
            AmbientDayBucket.None,      // 아침 산책이네
            AmbientDayBucket.None,      // 점심때 걷네
            AmbientDayBucket.None,      // 오후가 지나가네
            AmbientDayBucket.None,      // 하루를 마무리하네
            AmbientDayBucket.None,      // 밤에도 걷네
        };

        /// <summary><see cref="WalkLines"/>와 같은 길이·순서의 <b>시간대</b> 자격 축.</summary>
        private static readonly AmbientTimeBucket[] WalkLineTimeRequirement =
        {
            AmbientTimeBucket.None,       // 산책 중
            AmbientTimeBucket.None,       // 저쪽으로 가볼까
            AmbientTimeBucket.None,       // 하나 둘 하나 둘
            AmbientTimeBucket.None,       // 다리 좀 풀자
            AmbientTimeBucket.None,       // 다리가 잘 나가네
            AmbientTimeBucket.None,       // 월요일이 왔네
            AmbientTimeBucket.None,       // 주말이 코앞이네
            AmbientTimeBucket.None,       // 주말 산책이네
            AmbientTimeBucket.Morning,    // 아침 산책이네
            AmbientTimeBucket.Lunch,      // 점심때 걷네
            AmbientTimeBucket.Afternoon,  // 오후가 지나가네
            AmbientTimeBucket.Evening,    // 하루를 마무리하네
            AmbientTimeBucket.Night,      // 밤에도 걷네
        };

        /// <summary>이 줄이 요구하는 모션(없으면 null). Walk 표에는 모션 축이 없다(위 문서 참고).</summary>
        private static WanderAmbientMotion? MotionRequirementFor(StickmanStateId stateId, int lineIndex)
        {
            if (stateId == StickmanStateId.Walk) return null;
            if (lineIndex < 0 || lineIndex >= IdleLineMotionRequirement.Length) return null;
            return IdleLineMotionRequirement[lineIndex];
        }

        /// <summary>이 줄이 요구하는 요일 구간(없으면 <see cref="AmbientDayBucket.None"/>).</summary>
        private static AmbientDayBucket DayRequirementFor(StickmanStateId stateId, int lineIndex)
        {
            AmbientDayBucket[] axis = stateId == StickmanStateId.Walk
                ? WalkLineDayRequirement
                : IdleLineDayRequirement;
            if (lineIndex < 0 || lineIndex >= axis.Length) return AmbientDayBucket.None;
            return axis[lineIndex];
        }

        /// <summary>이 줄이 요구하는 시간대 구간(없으면 <see cref="AmbientTimeBucket.None"/>).</summary>
        private static AmbientTimeBucket TimeRequirementFor(StickmanStateId stateId, int lineIndex)
        {
            AmbientTimeBucket[] axis = stateId == StickmanStateId.Walk
                ? WalkLineTimeRequirement
                : IdleLineTimeRequirement;
            if (lineIndex < 0 || lineIndex >= axis.Length) return AmbientTimeBucket.None;
            return axis[lineIndex];
        }

        /// <summary>
        /// 이 줄이 <b>지금</b> 후보가 될 수 있는가. 세 축(모션 / 요일 / 시간대)을 AND로 묶는다.
        /// 축이 안 붙은 줄은 언제나 true이고, 모션 조건이 붙은 줄은 그 모션이 재생 중일 때만 true다
        /// (<see cref="StickmanBlackboard.IsIdleAmbientMotionActive"/>가 곧 "화면에서 지금 그 동작이
        /// 그려지는 중"이다 — Idle을 벗어나거나 스위치가 꺼지면 같은 프레임에 false가 된다).
        ///
        /// <para><b>순수 함수다.</b> 벽시계를 여기서 읽지 않는다 — 이 함수는 추첨 한 번에 표 길이만큼
        /// 불리고, 그 사이에 정시 경계를 밟으면 <b>같은 추첨 안에서 후보 집합이 달라진다</b>
        /// (2패스 추첨이 어긋난 인덱스를 고른다). 그래서 <paramref name="calendar"/>를
        /// <b>값으로 받는다</b>. 스냅샷을 뜨는 자리는 <see cref="AmbientCalendarClock"/> 하나다.</para>
        ///
        /// <para>강제 발화 펄스(Ctrl+Opt+Cmd+B)도 이 필터를 건너뛰지 <b>않는다</b>. 확률과 쿨다운은
        /// "말할지 말지"의 문제라 사용자 명령이 덮을 수 있지만, 자격은 "그 문장이 참인가"의 문제다.</para>
        ///
        /// <para>★ <paramref name="calendar"/>의 기본값을 두지 <b>않았다</b>. 두면 «달력을 안 넘긴
        /// 호출부»가 조용히 컴파일되고, 그 순간 요일·시간대 16줄이 <b>영원히 침묵하는 죽은 데이터</b>가
        /// 된다 — 그 실패는 초록 테스트와 똑같이 생긴다.</para>
        /// </summary>
        public static bool IsLineEligible(StickmanBlackboard blackboard, StickmanStateId stateId, int lineIndex,
            AmbientCalendarSnapshot calendar)
        {
            AmbientDayBucket day = DayRequirementFor(stateId, lineIndex);
            if (day != AmbientDayBucket.None && day != calendar.Day) return false;

            AmbientTimeBucket time = TimeRequirementFor(stateId, lineIndex);
            if (time != AmbientTimeBucket.None && time != calendar.Time) return false;

            WanderAmbientMotion? required = MotionRequirementFor(stateId, lineIndex);
            if (!required.HasValue) return true;
            if (blackboard == null) return false;
            if (!blackboard.IsIdleAmbientMotionActive) return false;
            if (blackboard.CurrentIdleAmbientMotion != required.Value) return false;
            return required.Value != WanderAmbientMotion.LookAround || HasVisibleLookAroundScan(blackboard);
        }

        /// <summary>
        /// "구경 중이야"의 두 번째 조건 — <b>좌우로 훑는 그림이 실제로 그려지는가</b>.
        ///
        /// <para>이 검사가 따로 필요한 이유: 이 문장이 주장하는 것은 손차양 <i>자세</i>가 아니라
        /// <b>훑는 움직임</b>이고, 그 움직임을 그리는 채널이 설정으로 꺼질 수 있다. 채널은 둘이다 —
        /// 상체 좌우 왕복(<c>bodyLeanLookAroundDegrees</c>, 마스터 스위치까지 접은 값이
        /// <see cref="StickmanBlackboard.LookAroundBodyLeanDegrees"/>)과 머리 좌우 이동
        /// (<c>idleAmbientLookHeadShiftRatio</c>). 둘 다 0이면 훑는 그림이 한 장도 없으므로 침묵한다.</para>
        ///
        /// <para>★ 세 번째 채널인 <b>눈동자 좌우 훑기</b>(<c>idleAmbientLookEyeSweep01</c>)는 일부러
        /// 빼 두었다 — 2026-09-01 그림체 전환으로 캐릭터에서 눈이 삭제됐고
        /// (<c>Editor/SceneBootstrapper.BakeEyes = false</c>) 그동안은 값이 0.85여도 화면에 아무것도
        /// 안 나온다. 눈을 되살리는 날 여기에 한 줄을 더한다.</para>
        /// </summary>
        private static bool HasVisibleLookAroundScan(StickmanBlackboard blackboard)
        {
            if (Mathf.Abs(blackboard.LookAroundBodyLeanDegrees) > 0f) return true;
            StickConfig config = blackboard.Config;
            return config != null && Mathf.Abs(config.idleAmbientLookHeadShiftRatio) > 0f;
        }

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

            // ★★ 2026-09-06 — 달력을 <b>여기서 딱 한 번</b> 뜬다. 아래 두 번의 훑기가 <b>같은 스냅샷</b>을
            //   봐야 한다. 자격 판정 안에서 시계를 읽으면 정시·자정 경계에서 1패스와 2패스의 후보
            //   집합이 달라지고, 그러면 "eligibleCount번째 후보"가 존재하지 않아 chosen이 -1로 남는다
            //   (그 뒤 Resolve가 음수 인덱스를 받는다 — 지금은 나머지 연산이 감싸므로 크래시는 아니지만
            //   화면에는 자격 없는 문장이 뜬다). 폴링 게이트가 대부분의 호출에서 double 뺄셈 하나로
            //   끝나므로 이 한 줄의 비용은 사실상 0이다(24시간 상주 앱: 할당 0).
            AmbientCalendarSnapshot calendar =
                AmbientCalendarClock.Shared.PollIfDue(Time.realtimeSinceStartupAsDouble);

            // ★ 2026-09-06 — 자격 있는 줄만 후보에 넣는다(위 "자격 술어 축" 참고). 두 번 훑는 이유는
            //   후보 목록을 만들지 않기 위해서다 — 이 앱은 하루 종일 켜져 있고 이 함수는 Idle/Walk
            //   전이마다 돈다. 뽑는 결과는 후보 목록에서 균등 추첨한 것과 같고 난수 소비도 1회로 같다.
            int eligibleCount = 0;
            for (int i = 0; i < table.Length; i++)
            {
                if (IsLineEligible(blackboard, stateId, i, calendar)) eligibleCount++;
            }
            if (eligibleCount == 0) return false; // 지금 참인 문장이 하나도 없다 — 침묵(쿨다운도 안 태운다).

            int pick = Random.Range(0, eligibleCount);
            int chosen = -1;
            for (int i = 0; i < table.Length; i++)
            {
                if (!IsLineEligible(blackboard, stateId, i, calendar)) continue;
                if (pick == 0) { chosen = i; break; }
                pick--;
            }
            target.LineIndex = chosen; // 절대 인덱스 — Resolve가 필터를 재현하지 않아도 되게.

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
                //   값인 것은 상태가 배회 페이즈 전환으로 들어왔을 때뿐이고, 기상/착지/등반
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
