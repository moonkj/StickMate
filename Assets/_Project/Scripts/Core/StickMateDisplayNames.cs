using System;

namespace StickMate.Core
{
    /// <summary>
    /// ★ enum → 사람이 읽는 한글 이름의 생산자 — docs/UX_FLOW.md <b>36-13 #9/#10</b>.
    ///
    /// <para>★★ 2026-09-03 정정 — 이 자리에 원래 "<b>유일한</b> 생산자"라고 적혀 있었으나 <b>거짓</b>이다.
    /// <c>Interaction/CharacterInfoWindow.StateLabel</c>이 <see cref="StickmanStateId"/> 27개를
    /// <b>한 벌 더</b> 한글로 옮기고 있고, 그것이 정보창 프레즌스 줄("지금  ·  ○○")로 화면에 뜬다.
    /// 즉 아래 "왜 창이 아니라 Core에 두는가"가 경고한 <b>바로 그 상태가 이미 성립해 있다</b>.
    /// 실측: 27개 중 <b>19개가 서로 다른 낱말</b>이다(예: <c>ThrowTumble</c> → 여기 "공중 회전" /
    /// 저기 "날아가는 중", <c>Fall</c> → "낙하" / "떨어지는 중", <c>Graffiti</c> → "그라피티" / "낙서하는 중").
    /// 두 문구는 <b>둘 다 "지금"으로 시작</b>하고 정보창과 행동 명령창이 동시에 열릴 수 있어 한 화면에서 부딪힌다.
    /// <b>주석을 코드에 맞추지 않고 이 정정만 붙인 이유</b>: 틀린 것은 이 문장이 아니라 <b>코드</b>이고,
    /// "생산자 중 하나"로 문구만 낮추면 결함이 설계처럼 보인다. 통합 판정은
    /// <c>docs/inspection/R2_거짓주석_전수조사.md</c> §2 — 어휘는 <c>StateLabel</c> 쪽,
    /// 거처는 이 클래스가 정본이다. 통합 전까지 <b>새 상태의 이름을 두 곳에 각각 적어야 한다.</b></para>
    ///
    /// ============================================================================
    /// 왜 창이 아니라 Core에 두는가
    /// ============================================================================
    /// 행동 명령창은 "지금 활쏘기 중이에요" 같은 이유 문구를 보여줘야 하는데, 그 ○○는
    /// <see cref="SpectacleEventLock.ActiveKind"/>와 <see cref="StickmanStateId"/>라는
    /// <b>실제 값</b>에서 파생돼야 한다(CLAUDE.md 원칙 1). 문자열 테이블을 창 안에 두면 그 순간
    /// "화면에 쓰는 이름"과 "코드가 아는 값"이 두 벌이 되고, enum에 값이 하나 늘 때 화면만 조용히
    /// 옛 이름으로 남는다 — 이 프로젝트가 이미 여러 번 밟은 함정(Dock 구간 이중 계산 / 캐릭터 치수
    /// 이중 정의)과 같은 형태다.
    ///
    /// ============================================================================
    /// 문구까지 <b>미리 만들어</b> 둔다 (무할당)
    /// ============================================================================
    /// 이 앱은 하루 종일 켜져 있고, 행동 명령창은 0.25초마다 6개 타일의 가용성을 다시 묻는다. 그때마다
    /// $"지금 {이름} 중이에요"를 만들면 초당 24개의 쓰레기 문자열이 생긴다. enum 값의 개수는 유한하고
    /// 실행 중에 변하지 않으므로 <b>완성된 문장</b>을 정적 배열로 한 번만 만들어 둔다.
    /// </summary>
    public static class StickMateDisplayNames
    {
        // ==================== 스펙터클 락 ====================

        private static readonly string[] SpectacleNames = BuildSpectacleNames();
        private static readonly string[] SpectacleBusyTexts = BuildBusyTexts(SpectacleNames, "지금 ", " 중이에요");

        /// <summary>
        /// ★★ 2026-09-26 — <b>크랙 칸만 「지금 ○○ 중이에요」 문형에서 뺀다.</b>
        ///
        /// <para><b>무엇이 거짓이었나</b>: 형제 세 칸(활쏘기 · 낙서 · 창 도둑)이 크랙 락 동안 「지금 <b>창 부수기</b>
        /// 중이에요」를 띄웠는데, 그것은 <b>캐릭터 상태 주장</b>이다. 캐릭터 스윙은
        /// <c>windowCrashSwingDuration</c>(출하 0.4초)이면 끝나고 그 뒤 캐릭터는 <b>Idle로 한가히 걷는다</b>.
        /// 그런데 락은 금 오버레이 수명(<c>windowCrashOverlayDurationSeconds</c>, 출하 3초)까지 남는다 ⇒
        /// <b>약 2.6초 동안 그 문장이 거짓</b>이다(페르소나 둘이 독립으로 짚었다).</para>
        ///
        /// <para>★ <b>불변식(ux-widgets)</b>: <b>문장은 그것이 뜰 수 있는 구간 전체에서 참이어야 한다.</b>
        /// 락 13종 가운데 <b>락이 캐릭터 상태보다 오래 남는 것은 크랙 하나뿐</b>이므로 <b>정확히 한 칸만</b> 바꾼다.
        /// 나머지 12종은 지금 참이다 — <b>건드리지 마라</b>(전면 교체는 기각됐다).</para>
        ///
        /// <para>★ <b>왜 여기서 배열 칸을 덮어쓰는가</b>(<c>BusyText</c>에 분기를 넣지 않는 이유): 이 클래스의 계약은
        /// <b>완성된 문장을 미리 만들어 두는 것</b>(위 「무할당」 절)이고, <c>CommandAvailabilityContractTests</c>가
        /// 같은 값을 두 번 물으면 <b>같은 인스턴스</b>가 오는지로 그것을 잰다. 분기를 넣으면 그 계약이 깨진다.
        /// 칸을 덮어쓰면 호출부는 한 글자도 바뀌지 않고, <c>BusyText(ActiveKind)</c>를 부르는 <b>프로덕션 8개 파일이
        /// 전부 이 한 곳</b>을 본다(문구 복제 0).</para>
        ///
        /// <para>★ <c>Of(kind)</c> <b>이름 표는 건드리지 않는다</b> — 거기서 크랙은 여전히 「창 부수기」다.
        /// 바꾸는 것은 <b>락 점유 사유 문장</b> 하나뿐이다. 기각된 대안 둘(되살리지 마라):
        /// ⑴ 형제 칸이 크랙 감독에게 직접 묻기 — 진실이 두 벌이 되고 새 결합이 생긴다.
        /// ⑵ 13종 전면 교체 — 12종은 지금 참이라 고칠 것이 없다.</para>
        ///
        /// <para>★ <b>문구는 design-narrative 확정 대기</b>다. 지금 값은 추천안 <b>T1</b>이고, 자리와 배선을 먼저 만들어
        /// 확정이 오면 <b>이 상수의 글자만</b> 바꾸면 되게 했다. 폭 재계산(내가 다시 쟀다 · 글꼴
        /// <c>UiChrome.FontCaption</c> 10 · 타일 설명 상자 폭 <b>372</b> = <c>RowWidth</c> − 56):
        /// 15자(한글 11 · 공백 4) ⇒ 모형 N <b>122.0pt</b> · 모형 C(글자수 × 10.5) <b>157.5pt</b> — 한도까지 214.5pt 남는다.</para>
        /// </summary>
        public const string WindowCrashBusyText = "금이 사라지면 할 수 있어요";

        /// <summary>위 한 칸만 덮어쓴다. <see cref="SpectacleBusyTexts"/>는 <c>readonly</c>지만 그것은 <b>참조</b>가
        /// 고정된다는 뜻이고 원소 대입은 막지 않는다 — 생성 줄은 한 글자도 바꾸지 않았다.</summary>
        static StickMateDisplayNames()
        {
            SpectacleBusyTexts[(int)SpectacleEventKind.WindowCrash] = WindowCrashBusyText;
        }

        /// <summary>지금 락을 쥔 스펙터클의 한글 이름.</summary>
        public static string Of(SpectacleEventKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < SpectacleNames.Length ? SpectacleNames[i] : "다른 일";
        }

        /// <summary>"지금 ○○ 중이에요" — 36-7의 불가 이유 문구(락 점유).</summary>
        public static string BusyText(SpectacleEventKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < SpectacleBusyTexts.Length ? SpectacleBusyTexts[i] : "지금 다른 일 중이에요";
        }

        private static string[] BuildSpectacleNames()
        {
            var values = (SpectacleEventKind[])Enum.GetValues(typeof(SpectacleEventKind));
            int max = 0;
            for (int i = 0; i < values.Length; i++) max = Math.Max(max, (int)values[i]);

            var names = new string[max + 1];
            for (int i = 0; i < names.Length; i++) names[i] = "다른 일";

            Set(names, SpectacleEventKind.DragAndThrow, "붙잡혀 있는");
            Set(names, SpectacleEventKind.RodeoCursor, "커서 타기");
            Set(names, SpectacleEventKind.WindowTheft, "창 도둑");
            Set(names, SpectacleEventKind.Graffiti, "그라피티");
            Set(names, SpectacleEventKind.DesktopTidy, "바탕화면 정리");
            Set(names, SpectacleEventKind.BlackholeSummon, "블랙홀");
            Set(names, SpectacleEventKind.WindowCrash, "창 부수기");
            Set(names, SpectacleEventKind.TodoReminder, "할일 알림");
            Set(names, SpectacleEventKind.FocusPose, "집중 모드");
            Set(names, SpectacleEventKind.Sulky, "부루퉁");
            Set(names, SpectacleEventKind.Runaway, "가출");
            Set(names, SpectacleEventKind.Archery, "활쏘기");
            Set(names, SpectacleEventKind.Dance, "춤");
            return names;
        }

        private static void Set(string[] table, SpectacleEventKind kind, string name) => table[(int)kind] = name;

        // ==================== 상태 ====================

        private static readonly string[] StateNames = BuildStateNames();
        private static readonly string[] StateBusyTexts = BuildBusyTexts(StateNames, "지금 ", " 중이라 못 해요");

        /// <summary>지금 캐릭터가 하고 있는 일의 한글 이름.</summary>
        public static string Of(StickmanStateId state)
        {
            int i = (int)state;
            return i >= 0 && i < StateNames.Length ? StateNames[i] : "딴 일";
        }

        /// <summary>"지금 △△ 중이라 못 해요" — 36-7의 불가 이유 문구(상태 불일치).</summary>
        public static string BusyText(StickmanStateId state)
        {
            int i = (int)state;
            return i >= 0 && i < StateBusyTexts.Length ? StateBusyTexts[i] : "지금 딴 일 중이라 못 해요";
        }

        private static string[] BuildStateNames()
        {
            var values = (StickmanStateId[])Enum.GetValues(typeof(StickmanStateId));
            int max = 0;
            for (int i = 0; i < values.Length; i++) max = Math.Max(max, (int)values[i]);

            var names = new string[max + 1];
            for (int i = 0; i < names.Length; i++) names[i] = "딴 일";

            Set(names, StickmanStateId.Idle, "쉬는");
            Set(names, StickmanStateId.Walk, "걷는");
            Set(names, StickmanStateId.Jump, "점프");
            Set(names, StickmanStateId.Fall, "낙하");
            Set(names, StickmanStateId.ParkourClimb, "등반");
            Set(names, StickmanStateId.Attack, "공격");
            Set(names, StickmanStateId.Ragdoll, "넘어져 있는");
            Set(names, StickmanStateId.Getup, "일어나는");
            Set(names, StickmanStateId.Dragged, "붙잡혀 있는");
            Set(names, StickmanStateId.RodeoCursor, "커서 타기");
            Set(names, StickmanStateId.WindowTheft, "창 도둑");
            Set(names, StickmanStateId.Graffiti, "그라피티");
            Set(names, StickmanStateId.DesktopTidy, "바탕화면 정리");
            Set(names, StickmanStateId.BlackholeSummon, "블랙홀");
            Set(names, StickmanStateId.WindowCrash, "창 부수기");
            Set(names, StickmanStateId.TodoReminder, "할일 알림");
            Set(names, StickmanStateId.FocusStart, "집중 모드");
            Set(names, StickmanStateId.FocusComplete, "집중 모드");
            Set(names, StickmanStateId.FocusCancelled, "집중 모드");
            Set(names, StickmanStateId.FocusNudge, "집중 모드");
            Set(names, StickmanStateId.Sulky, "부루퉁");
            Set(names, StickmanStateId.Runaway, "가출");
            Set(names, StickmanStateId.LedgeHang, "매달리는");
            Set(names, StickmanStateId.LandingCrouch, "착지");
            Set(names, StickmanStateId.ThrowTumble, "공중 회전");
            Set(names, StickmanStateId.Archery, "활쏘기");
            Set(names, StickmanStateId.GroundLossHang, "허둥대는");
            Set(names, StickmanStateId.Dance, "춤추는");
            Set(names, StickmanStateId.RopeClimb, "밧줄 등반");
            return names;
        }

        private static void Set(string[] table, StickmanStateId state, string name) => table[(int)state] = name;

        // ==================== 공통 ====================

        private static string[] BuildBusyTexts(string[] names, string prefix, string suffix)
        {
            var texts = new string[names.Length];
            for (int i = 0; i < names.Length; i++) texts[i] = prefix + names[i] + suffix;
            return texts;
        }
    }
}
