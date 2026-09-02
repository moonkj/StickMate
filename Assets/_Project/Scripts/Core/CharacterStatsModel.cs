using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 정보창 하단 스탯 블록이 읽는 <b>실제 기록</b> — 2026-08-30 정보창 리디자인 라운드.
    ///
    /// ============================================================================
    /// 왜 "공격력/방어력" 같은 전투 스탯을 만들지 않았는가
    /// ============================================================================
    /// 참고한 RPG 캐릭터 창의 2열x3행 스탯 배치는 그대로 쓰되, <b>내용은 이 앱에 실제로 존재하는
    /// 사실</b>로만 채운다(리더 확정). 이 앱에는 HP도 데미지도 없다 — 없는 숫자를 그럴듯하게 적으면
    /// 그 순간부터 창 전체가 장식이 되고, 사용자는 어느 숫자가 진짜인지 구분할 수 없게 된다.
    /// 그래서 다섯 칸은 근속 / 함께한 시간 / 보유 장비 / 활쏘기 명중률 / 넘어진 횟수다.
    /// ("대결 승리"는 라이벌 기능 전체 삭제(2026-08-30)로 영구 0이 되는 죽은 칸이 되어 "보유 장비"
    /// — ItemCatalog.UnlockedEquipmentCount/EquipmentCount — 로 교체했다. 없는 숫자를 적지 않는다는
    /// 위 원칙은 그대로다: 보유 장비는 레벨에 따라 실제로 변하는 값이다.
    /// <b>"격파 성공"은 2026-09-02 격파 놀이 기능 삭제로 같은 이유(영구 0)로 빠졌다</b> — 다만
    /// 저장 필드는 지우지 않았다. 아래 <see cref="BattleWins"/> 참고.)
    ///
    /// ============================================================================
    /// 이 클래스는 "언제 세는지"를 모른다
    /// ============================================================================
    /// StressGauge ↔ StressGaugeDirector, CharacterProgressionModel ↔ CharacterProgressionDirector와
    /// <b>정확히 같은 분리</b>다. 값 보관과 영속화 대상만 여기 있고, 누가 이겼는지/얼마나 켜져 있었는지
    /// 판정하는 일은 Interaction/CharacterStatsDirector.cs가 전부 이벤트 <b>구독</b>으로만 한다
    /// (기존 판정 로직은 한 줄도 건드리지 않는다 — 리더 지시).
    ///
    /// 정적 클래스인 이유도 같다: 값의 수명이 씬이 아니라 프로세스(그리고 저장 파일)다.
    /// </summary>
    public static class CharacterStatsModel
    {
        /// <summary>
        /// ★ <b>죽은 값이지만 일부러 살려둔다</b> — 2026-09-02 격파 놀이 삭제(리더 판정).
        ///
        /// <para>이 값을 올리는 코드는 이제 하나도 없다(<c>AddBattleWin()</c>은 같은 날 삭제됐다).
        /// 그런데도 프로퍼티와 <see cref="RestoreFromSave"/>의 인자가 남아 있는 이유는 <b>단 하나</b>,
        /// 저장 파일의 <c>battleWins</c> 필드를 <b>읽은 그대로 다시 쓰기</b> 위해서다. 실제 사용자
        /// 세이브(스키마 v9)에 3회 전적이 들어 있고, 필드를 빼면 (a) <c>CurrentVersion</c>을 올려야 하고
        /// (b) CLAUDE.md가 그 라운드에 v8 하위 호환 테스트를 의무화한다. 죽은 필드 하나를 지우려고
        /// 마이그레이션 위험을 사지 않는다. 화면에서 사라지면 그만이고, 되살리고 싶어지면 전적이 남아
        /// 있다(지우면 못 돌린다).</para>
        ///
        /// <para>회귀 잠금: <c>Tests/EditMode/CharacterStatsPersistenceTests</c>의
        /// "격파_전적은_기능이_사라진_뒤에도_저장_왕복에서_살아남는다".</para>
        /// </summary>
        public static int BattleWins { get; private set; }

        /// <summary>지금까지 쏜 화살 수(Release 시점 기준). 명중률의 분모다.</summary>
        public static int ArcheryShots { get; private set; }

        /// <summary>그중 정중앙(Bullseye) 수. 명중률의 분자다.</summary>
        public static int ArcheryBullseyes { get; private set; }

        /// <summary>앱이 켜져 있던 누적 초. 실행마다 이어서 쌓인다("함께한 시간").</summary>
        public static float TotalCompanionSeconds { get; private set; }

        /// <summary>넘어진 횟수(Ragdoll 상태로 <b>진입</b>한 횟수). 던져지거나 높은 데서 떨어져 구르면 오른다.</summary>
        public static int RagdollFalls { get; private set; }

        /// <summary>이 캐릭터를 처음 만난 시각(Unix 초, UTC). 0이면 아직 기록되지 않은 것이다
        /// (구버전 저장 파일 / 새 캐릭터) — <see cref="EnsureFirstRunInitialized"/>가 그 자리에서 채운다.</summary>
        public static long FirstRunUnixSeconds { get; private set; }

        /// <summary>마지막 저장 이후 값이 바뀌었는가 — 주기 저장이 매번 디스크를 두드리지 않게 한다
        /// (CharacterProgressionModel.IsDirty와 같은 역할, 같은 관례).</summary>
        public static bool IsDirty { get; private set; }

        /// <summary>"근속" — 처음 만난 날을 1일차로 세는 사람 감각의 날짜 수. 시각이 아직 없으면 1일차.</summary>
        public static int DaysTogether
        {
            get
            {
                if (FirstRunUnixSeconds <= 0L) return 1;
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long elapsed = now - FirstRunUnixSeconds;
                if (elapsed < 0L) return 1; // 시계를 과거로 돌린 경우 — 음수 근속을 만들지 않는다.
                return 1 + (int)(elapsed / 86400L);
            }
        }

        /// <summary>앱 시작 시 1회(저장 파일 로드 직후). 기록이 없으면 <b>지금</b>을 첫 만남으로 삼는다.</summary>
        public static void EnsureFirstRunInitialized()
        {
            if (FirstRunUnixSeconds > 0L) return;
            FirstRunUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            IsDirty = true;
        }

        /// <summary>발사 0회면 <c>false</c> — UI가 "기록 없음"을 표시할 근거다(0%로 보여주면
        /// "쏴봤는데 다 빗나갔다"는 <b>다른 사실</b>이 된다).</summary>
        public static bool TryGetArcheryAccuracy01(out float accuracy01)
        {
            if (ArcheryShots <= 0)
            {
                accuracy01 = 0f;
                return false;
            }
            accuracy01 = Mathf.Clamp01((float)ArcheryBullseyes / ArcheryShots);
            return true;
        }

        /// <summary>한 발 기록. 명중 여부와 발사 수를 <b>한 번에</b> 올려 둘이 어긋날 수 없게 한다
        /// (분모만 오르고 분자가 누락되는 종류의 버그를 구조적으로 막는다).</summary>
        public static void AddArcheryShot(bool bullseye)
        {
            ArcheryShots++;
            if (bullseye) ArcheryBullseyes++;
            IsDirty = true;
        }

        public static void AddRagdollFall()
        {
            RagdollFalls++;
            IsDirty = true;
        }

        public static void AddCompanionSeconds(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds)) return;
            TotalCompanionSeconds += seconds;
            IsDirty = true;
        }

        /// <summary>"3시간 12분" 같은 사람이 읽는 표기. 1시간 미만이면 분만 보여준다.
        /// <b>호출부가 매 프레임 부르지 않는다</b>(정보창의 0.25초 주기 갱신에서만 쓴다).</summary>
        public static string FormatCompanionTime()
        {
            int totalMinutes = Mathf.FloorToInt(TotalCompanionSeconds / 60f);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            if (hours <= 0) return $"{minutes}분";
            return $"{hours}시간 {minutes}분";
        }

        /// <summary>저장 파일 복원 전용(Core/CharacterSaveStore.cs). 이벤트를 쏘지 않는 이유는
        /// CharacterProgressionModel.RestoreFromSave와 같다(복원은 변화가 아니라 초기 상태 확정).</summary>
        internal static void RestoreFromSave(int battleWins, int archeryShots,
            int archeryBullseyes, float companionSeconds, int ragdollFalls, long firstRunUnixSeconds)
        {
            RagdollFalls = Mathf.Max(0, ragdollFalls);
            FirstRunUnixSeconds = firstRunUnixSeconds > 0L ? firstRunUnixSeconds : 0L;
            BattleWins = Mathf.Max(0, battleWins);
            ArcheryShots = Mathf.Max(0, archeryShots);
            // 명중 수가 발사 수보다 큰 파일(손상/구버전)이 들어와도 명중률이 100%를 넘지 않게 한다.
            ArcheryBullseyes = Mathf.Clamp(archeryBullseyes, 0, ArcheryShots);
            TotalCompanionSeconds = Mathf.Max(0f, companionSeconds);
            IsDirty = false;
        }

        internal static void MarkSaved() => IsDirty = false;

        /// <summary>테스트/디버그 전용 완전 초기화(CharacterProgressionModel.ResetForTesting과 같은 이유 —
        /// 정적 상태가 테스트 사이에 새지 않게). 정상 게임플레이 경로에서는 호출되지 않는다.</summary>
        public static void ResetForTesting()
        {
            BattleWins = 0;
            ArcheryShots = 0;
            ArcheryBullseyes = 0;
            TotalCompanionSeconds = 0f;
            RagdollFalls = 0;
            FirstRunUnixSeconds = 0L;
            IsDirty = false;
        }
    }
}
