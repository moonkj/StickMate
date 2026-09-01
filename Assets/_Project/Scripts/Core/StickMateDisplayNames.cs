using System;

namespace StickMate.Core
{
    /// <summary>
    /// ★ enum → 사람이 읽는 한글 이름의 <b>유일한 생산자</b> — docs/UX_FLOW.md <b>36-13 #9/#10</b>.
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

            Set(names, SpectacleEventKind.BattleMinigame, "격파 놀이");
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
            Set(names, StickmanStateId.BattleMinigame, "격파 놀이");
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
