using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 캐릭터 성장(레벨 / 경험치 / 이름) — 2026-08-29 사용자 요청
    /// ("캐릭터 장비 착용 및 캐릭터 정보 볼수있는 창을 만들어야함" → 리더가 범위를 확정:
    ///  진짜 장비/스킨 + 레벨/능력치 같은 육성 요소).
    ///
    /// ============================================================================
    /// 왜 정적 클래스인가
    /// ============================================================================
    /// Core/TodoListModel.cs / Core/StressGauge.cs / Core/SpectacleEventLock.cs와 <b>같은 이유</b>다 —
    /// 이 앱은 하루 종일 켜져 있는 상주 앱이고, 레벨/XP는 씬 생명주기가 아니라 프로세스(그리고 저장
    /// 파일)의 수명을 따르는 단일 전역 상태다. 소유자 MonoBehaviour를 하나 정해 두면 그 오브젝트가
    /// 파괴되는 순간(씬 재로드/중복 배치/테스트 리그) 값이 조용히 사라지거나 두 벌이 된다.
    ///
    /// 이 클래스는 <b>언제 XP가 들어오는지 전혀 모른다</b>. 패시브 적립과 보너스 훅(활쏘기 정중앙
    /// 명중)은 전부 Interaction/CharacterProgressionDirector.cs의 책임이고, 저장/로드는
    /// Core/CharacterSaveStore.cs의 책임이다(StressGauge가 값 보관만 하고 트리거는 Director가 맡는 것과
    /// 정확히 같은 분리).
    ///
    /// ============================================================================
    /// XP 곡선 — 왜 지수형인가, 그리고 실제로 몇 시간인가
    /// ============================================================================
    /// 다음 레벨까지 필요한 XP = <c>progressionXpCurveBase * level ^ progressionXpCurveExponent</c>
    /// (기본 100 * level^1.05 — 2026-08-30 리더 결정으로 1.15→1.05 완화, 근거는 아래 문단 참고).
    /// 패시브 적립은 분당 <c>progressionPassiveXpPerMinute</c>(기본 1.5)이므로 시간당 90XP다. 실측 환산:
    ///
    ///   Lv1→2  100XP    누적    100 →  1.1시간
    ///   Lv2→3  207XP    누적    307 →  3.4시간
    ///   Lv3→4  317XP    누적    624 →  6.9시간
    ///   Lv4→5  429XP    누적  1,053 → 11.7시간
    ///   Lv5→6  542XP    누적  1,595 → 17.7시간
    ///   Lv6→7  656XP    누적  2,251 → 25.0시간
    ///   Lv7→8  772XP    누적  3,022 → 33.6시간
    ///
    /// 리더가 준 목표("초반 레벨업이 1~3시간 안에, 갈수록 서서히 느려짐")를 만족하고, 장비 해제 레벨
    /// 2/4/6/8은 하루 8시간 사용 기준으로 각각 1일차/1일차/2.2일차/4.2일차에 열린다("며칠 안에 하나씩").
    /// 보너스 XP(명중)는 이 시간을 앞당기지만 <b>주 경로가 아니다</b> — 관찰형 앱 철학상
    /// "아무것도 안 해도 자란다"가 기본이고 능동 행동은 가속일 뿐이다.
    ///
    /// ============================================================================
    /// 지수 완화 1.15→1.05 (2026-08-30, 외부 디자인 핸드오프 32종 장비 확장에 맞춘 리더 결정)
    /// ============================================================================
    /// 32종 장비 카탈로그의 최고 요구 레벨(펫 "커서 친구" req 24)이 옛 지수(1.15)로는 누적 458시간
    /// (하루 8시간 기준 57일, 24시간 상주로도 19일)이 필요했고, 그 아래 왕관(Lv20)도 307시간(38일),
    /// 배낭(Lv22)도 378시간(47일)이었다 — 보너스 XP(당시 격파/대결/명중 25~40XP)로는 하루 500회 이상 승리해야
    /// 메울 수 있는 격차라 구조적으로 못 좁힌다. 32종 카탈로그가 레벨 24를 실질적 목표로 세운 이상
    /// 현실적으로 도달 가능해야 한다는 판단으로 지수를 1.05로 낮췄다 — 위 표에서 보듯 초반(Lv1~8) 리듬은
    /// 여전히 "1~3시간 안에 레벨업"을 만족하면서(오히려 살짝 더 빨라짐), Lv24 누적 시간만 458시간→
    /// 350시간(하루 8시간 기준 43.8일, 24시간 상주 14.6일)으로 완화된다.
    ///
    /// ============================================================================
    /// 원칙 1(행동-텍스트 싱크)과의 관계 — 무관하다
    /// ============================================================================
    /// 이 모델은 대사를 만들지 않는다. 레벨업 시 말풍선을 띄우고 싶다면 그것은 <b>상태 전이가 확정된
    /// 뒤</b> 그 상태에서 파생되어야 하며, 이 클래스가 문자열을 밖으로 내보내는 경로는 존재하지 않는다
    /// (CharacterName은 UI 라벨이지 대사가 아니다).
    /// </summary>
    public static class CharacterProgressionModel
    {
        /// <summary>이름을 한 번도 바꾸지 않았을 때의 기본값.</summary>
        public const string DefaultCharacterName = "스틱메이트";

        /// <summary>이름 입력 길이 상한 — 정보창 라벨이 창 밖으로 넘치지 않는 선.</summary>
        public const int MaxNameLength = 12;

        /// <summary>config가 없는 경로(테스트 리그 등)에서 쓰는 곡선 기본값. StickConfig의 기본값과
        /// 같아야 한다 — 두 곳에 다른 숫자가 있으면 조용히 어긋난다.</summary>
        public const float FallbackXpCurveBase = 100f;
        public const float FallbackXpCurveExponent = 1.05f;

        public static int Level { get; private set; } = 1;

        /// <summary>현재 레벨 안에서 쌓인 XP(다음 레벨까지의 진행분). 레벨업하면 0부터 다시 쌓인다.</summary>
        public static float CurrentXp { get; private set; }

        /// <summary>지금까지 벌어들인 누적 XP — 정보창의 "총 경험치" 표시 전용(레벨 계산에는 쓰지 않는다).</summary>
        public static float TotalXpEarned { get; private set; }

        public static string CharacterName { get; private set; } = DefaultCharacterName;

        /// <summary>마지막 저장 이후 값이 바뀌었는가 — 주기 저장이 매번 디스크를 두드리지 않게 한다.</summary>
        public static bool IsDirty { get; private set; }

        /// <summary><paramref name="level"/>에서 다음 레벨까지 필요한 XP. config가 null이면 기본 곡선.</summary>
        public static float XpToNextLevel(int level, StickConfig config)
        {
            float curveBase = config != null ? config.progressionXpCurveBase : FallbackXpCurveBase;
            float exponent = config != null ? config.progressionXpCurveExponent : FallbackXpCurveExponent;
            if (curveBase <= 0f) curveBase = FallbackXpCurveBase;
            if (exponent <= 0f) exponent = FallbackXpCurveExponent;
            return curveBase * Mathf.Pow(Mathf.Max(1, level), exponent);
        }

        public static float XpToNextLevel(StickConfig config) => XpToNextLevel(Level, config);

        /// <summary>현재 레벨의 진행도(0~1) — XP 바가 그대로 쓴다.</summary>
        public static float LevelProgress01(StickConfig config)
        {
            float need = XpToNextLevel(config);
            return need <= 0f ? 0f : Mathf.Clamp01(CurrentXp / need);
        }

        /// <summary>
        /// XP를 더한다. 필요치를 넘으면 넘긴 만큼을 다음 레벨로 이월하며 여러 단계 연속 레벨업도 처리한다
        /// (오랫동안 꺼뒀다 켜서 큰 보너스가 한 번에 들어오는 경우 — 한 번에 한 레벨만 올리면 남은 XP가
        /// 조용히 버려진다).
        /// </summary>
        /// <returns>이번 호출로 오른 레벨 수(0이면 레벨업 없음). 호출부가 연출/로그 판단에 쓴다.</returns>
        public static int AddXp(float amount, StickConfig config)
        {
            if (amount <= 0f || float.IsNaN(amount)) return 0;

            CurrentXp += amount;
            TotalXpEarned += amount;
            IsDirty = true;

            int gained = 0;
            // 무한 루프 방지 상한 — 곡선이 잘못 설정돼 필요치가 0에 가까워져도 프레임을 잡아먹지 않는다.
            for (int guard = 0; guard < 64; guard++)
            {
                float need = XpToNextLevel(config);
                if (need <= 0f || CurrentXp < need) break;
                CurrentXp -= need;
                Level++;
                gained++;
            }

            StickmanEventBus.RaiseCharacterProgressionChanged();
            return gained;
        }

        /// <summary>이름 변경(정보창의 이름 입력칸). 공백만 넣으면 기본값으로 되돌린다 —
        /// "이름이 사라진" 상태를 만들지 않는다.</summary>
        public static void SetCharacterName(string name)
        {
            string next = string.IsNullOrWhiteSpace(name) ? DefaultCharacterName : name.Trim();
            if (next.Length > MaxNameLength) next = next.Substring(0, MaxNameLength);
            if (next == CharacterName) return;
            CharacterName = next;
            IsDirty = true;
            StickmanEventBus.RaiseCharacterProgressionChanged();
        }

        /// <summary>저장 파일에서 복원할 때만 쓰는 진입점(Core/CharacterSaveStore.cs 전용).
        /// 이벤트를 발행하지 않는다 — 복원은 "변화"가 아니라 "초기 상태 확정"이고, 복원 도중 UI가
        /// 반쯤 채워진 값을 그리는 것을 막기 위해 저장소가 마지막에 한 번만 통지한다.</summary>
        internal static void RestoreFromSave(int level, float currentXp, float totalXpEarned, string name)
        {
            Level = Mathf.Clamp(level, 1, 9999);
            CurrentXp = Mathf.Max(0f, currentXp);
            TotalXpEarned = Mathf.Max(0f, totalXpEarned);
            CharacterName = string.IsNullOrWhiteSpace(name) ? DefaultCharacterName : name;
            IsDirty = false;
        }

        /// <summary>저장이 성공한 순간 저장소가 호출한다.</summary>
        internal static void MarkSaved() => IsDirty = false;

        /// <summary>테스트/디버그 전용 완전 초기화 — 정적 클래스라 씬을 다시 로드해도 값이 살아남기
        /// 때문에, PlayMode 테스트가 서로의 레벨을 물려받아 결과가 실행 순서에 좌우되는 것을 막는다
        /// (TodoListModel.ResetForTesting / StressGauge.ResetForTesting과 같은 이유, 같은 관례).
        /// 정상 게임플레이 경로에서는 호출되지 않는다.</summary>
        public static void ResetForTesting()
        {
            Level = 1;
            CurrentXp = 0f;
            TotalXpEarned = 0f;
            CharacterName = DefaultCharacterName;
            IsDirty = false;
        }
    }
}
