using System;
using System.Collections.Generic;

namespace StickMate.Core
{
    /// <summary>
    /// 코스튬 하나의 누적 집중 시간 한 줄. <b>키와 값을 한 레코드에</b> 담는다.
    ///
    /// <para>★ <b>병렬 배열 두 개가 아닌 이유</b>(계약서 D-1, 리더 판정): 병렬 배열은 «길이 불일치»라는
    /// 실패 모드를 만들고, 그러면 «둘 다 버린다»는 위생 규칙과 그 규칙의 테스트가 <b>필요해진다</b>.
    /// 그 규칙이 필요하다는 사실 자체가 그 형태의 결함이다. 레코드 배열은 그 분기와 그 테스트를
    /// <b>통째로 없앤다</b> — 키-값 어긋남이 <b>문법적으로 불가능</b>해진다.
    /// 그리고 저장소 선례(<see cref="ItemGraceBaseline"/>)도 이미 레코드 형태다.</para>
    ///
    /// <para>★ <b>가변 길이의 이점은 그대로 남는다</b>: 코스튬이 늘어도 세이브 스키마 버전이
    /// 안 올라간다(고정 길이 배열이면 코스튬마다 <c>v++</c>다). 그 이점은 «병렬»이 아니라
    /// «가변»에서 오는 것이었다.</para>
    ///
    /// <para><b>0분인 코스튬은 배열에 넣지 않는다.</b> 없음은 «기록 없음»이고 그게 정확한 사실이다.</para>
    /// </summary>
    [Serializable]
    public sealed class CostumeFocusRecord
    {
        /// <summary><see cref="CostumeManifestSO.costumeKey"/> 그대로("costume.office").
        /// <b>절대 바꾸지 말 것</b> — 바꾸면 사용자의 누적 100시간이 다른 코스튬 것이 된다.</summary>
        public string costumeKey;

        /// <summary>★ <b>정수 「분」</b>이다. 초도 <c>float</c>도 아니다 —
        /// <see cref="CostumeEvolutionRules"/> 문단에 그 이유(100시간에서 +87.5% 오차)가 있다.</summary>
        public int focusMinutes;
    }

    /// <summary>
    /// 저장 파일 ↔ <see cref="CostumeProgressModel"/> 사이를 오가는 한 덩어리.
    /// <see cref="CurrencySaveState"/>와 같은 성질이다 — <b>직렬화 타입이 아니고</b>
    /// (<c>[Serializable]</c>가 없다) 디스크 스키마는 <see cref="CharacterSaveStore"/> 안에만 있다.
    /// </summary>
    public struct CostumeFocusSaveState
    {
        public CostumeFocusRecord[] Records;
        public int MinutesToday;
    }

    /// <summary>
    /// ============================================================================
    /// ★ 코스튬별 누적 집중 시간(세이브 v12) — <b>값 보관 + IsDirty만 안다</b>
    /// ============================================================================
    /// 관례는 <see cref="CurrencyModel"/>과 동일하다: <b>언제 저장할지는 모른다</b>
    /// (<see cref="CharacterSaveStore"/>가 읽고 쓰며, 주기 저장은 <c>CharacterProgressionDirector</c>).
    ///
    /// ============================================================================
    /// ★ 이 모델이 <b>하지 않는</b> 것
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>단계를 저장하지 않는다.</b> 단계는 누적 분에서 <b>파생</b>한다
    ///    (<see cref="CostumeEvolutionRules.StageOf"/>). 누적은 줄어들지 않으므로
    ///    <b>강등이 문법적으로 존재하지 않고</b>, high-water 필드가 필요했던 이유
    ///    (스탯은 로드아웃에 따라 내려간다)가 여기엔 없다.</item>
    ///  <item><b>「입은 채였는가」를 판정하지 않는다.</b> 그건 <see cref="CostumeResolver"/>의 일이고,
    ///    <b>시작 ∧ 종료</b> 두 시점만 본다(세션 중 샘플링 없음 — 런타임 누적기를 만들지 마라).</item>
    ///  <item><b>세션 시간을 세지 않는다.</b> 세션 시간의 유일한 생산자는 <c>FocusWatchDirector</c>다.
    ///    두 곳에서 같은 시간을 세면 반드시 어긋난다.</item>
    ///  <item><b>프레임마다 아무 일도 하지 않는다.</b> 누적은 <b>세션 종료 1회</b>뿐이라
    ///    프레임 누적 축이 통째로 없다 — 부동소수 오차가 이 축에서 사라지는 이유가 그것이다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 「오늘 몇 분」의 날짜는 <b>여기서 다시 세지 않는다</b>
    /// ============================================================================
    /// 일일 소프트캡용 <see cref="MinutesToday"/>는 «오늘»이라는 사실을 필요로 하는데,
    /// 이 저장소에서 <b>오늘이 며칠인가를 아는 곳은 <see cref="CurrencyModel.DayIndex"/> 하나뿐이다</b>
    /// (그 값은 래칫이라 시계를 되감아도 안 내려간다). 그래서 여기서 <c>DateTime</c>을 읽지 않고
    /// <b>그 정수가 바뀌었는지만</b> 본다 — 새 벽시계 읽기가 0개이고,
    /// <c>archeryCoinsToday</c>·<c>focusXpToday</c>와 <b>같은 사건에 같이 0이 된다</b>.
    ///
    /// <para>★ 이것이 규칙 C-7(«진화는 재화 축과 연결되지 않는다»)을 어기지 <b>않는</b> 이유:
    /// 읽는 것은 <b>달력 사실 하나</b>이지 잔액이 아니다. 동전으로 단계를 앞당길 수 있는 경로가
    /// 생기지 않는다. 그리고 임계·캡 자체는 <see cref="CostumeEvolutionRules"/>에만 있고
    /// 그 파일은 <c>CurrencyModel</c>을 <b>한 글자도</b> 참조하지 않는다.</para>
    ///
    /// <para>★ <b>복원 순서 의존이 하나 있다</b>: <see cref="RestoreFromSave"/>는
    /// <see cref="CurrencyModel.RestoreFromSave"/> <b>뒤에</b> 불러야 한다. 앞에 부르면
    /// 「오늘」이 직전 세션의 값이라 방금 읽은 오늘분이 즉시 0이 된다.
    /// <see cref="CharacterSaveStore.Load"/>가 그 순서를 지킨다.</para>
    /// </summary>
    public static class CostumeProgressModel
    {
        private static readonly List<CostumeFocusRecord> s_records = new List<CostumeFocusRecord>();

        private static int s_minutesToday;

        /// <summary><see cref="s_minutesToday"/>가 «어느 날의 값인가». 세이브에 나가지 않는다 —
        /// 같은 파일의 <c>dayIndex</c>가 이미 그 사실을 들고 있고, 두 벌로 적으면 갈라진다.</summary>
        private static int s_minutesTodayDayIndex;

        /// <summary>마지막 저장 이후 값이 바뀌었는가(다른 모델의 <c>IsDirty</c>와 같은 역할).
        /// ★ <c>CharacterProgressionDirector.IsAnythingDirty()</c>에 <b>합류해야 한다</b> —
        /// 빠뜨리면 누적이 60초 주기 저장에도 종료 시 저장에도 실리지 않는다.</summary>
        public static bool IsDirty { get; private set; }

        internal static void MarkSaved() => IsDirty = false;

        /// <summary>지금 기록이 있는 코스튬들. <b>0분인 코스튬은 여기 없다</b>(기록 없음이 정확한 사실).</summary>
        public static IReadOnlyList<CostumeFocusRecord> Records
        {
            get { return s_records; }
        }

        /// <summary>이 코스튬의 누적 분. 기록이 없으면 <c>0</c>이고, 그 0은
        /// «아직 이 코스튬으로 집중한 적이 없다»는 정확한 사실이다.</summary>
        public static int MinutesOf(string costumeKey)
        {
            if (string.IsNullOrEmpty(costumeKey)) return 0;
            for (int i = 0; i < s_records.Count; i++)
            {
                if (s_records[i].costumeKey == costumeKey) return s_records[i].focusMinutes;
            }
            return 0;
        }

        /// <summary>
        /// 오늘 코스튬 누적에 실린 분(<b>전 코스튬 공유 1개</b>). 일자가 넘어갔으면 그 자리에서 0이 된다.
        /// <para>★ 화면에 <b>표시하지 않는다</b> — 정상 사용자가 평생 못 보는 값을 HUD에 두면
        /// 결제 압박 HUD와 같은 형태가 된다(R26 §5-3).</para>
        /// </summary>
        public static int MinutesToday
        {
            get { SyncDay(); return s_minutesToday; }
        }

        /// <summary>오늘 이 축에 더 실을 수 있는 분. 계산은 <see cref="CostumeEvolutionRules"/>가 한다.</summary>
        public static int RemainingMinutesToday => CostumeEvolutionRules.RemainingDailyMinutes(MinutesToday);

        /// <summary>
        /// ★ 누적을 더하는 <b>유일한 입구</b>. 일일 소프트캡은 <b>여기 안에서</b> 걸린다.
        ///
        /// <para><b>왜 호출부가 <c>min(...)</c>을 직접 쓰지 않는가</b>: 그러면 «오늘 얼마나 더 실을 수
        /// 있는가»가 호출부와 모델 두 곳에서 계산된다. 종료 경로가 셋이라
        /// (<c>CompleteSession</c> / <c>StopFocusSession</c> / <c>OnEmergencyStop</c>) 그 계산이
        /// <b>세 벌</b>로 퍼지고, 한 곳만 캡을 빠뜨리면 그 경로로 끝낸 사용자만 캡을 우회한다 —
        /// 화면에는 아무 흔적이 안 남는다.</para>
        ///
        /// <para><b>넘긴 분은 이미 <c>floor(경과초 / 60)</c>을 거친 값이어야 한다</b> —
        /// 동전·XP가 쓰는 것과 <b>같은 격자</b>다(지급 3분인데 누적 3.7분이면 두 사실이 갈라진다).
        /// 이 함수는 초를 모른다.</para>
        /// </summary>
        /// <returns>★ <b>실제로 더해진</b> 분. 캡에 걸렸거나 0분이면 0이다.
        /// 승급 판정은 이 값이 0보다 클 때만 뜻이 있다.</returns>
        public static int AddFocusMinutes(string costumeKey, int minutes)
        {
            if (string.IsNullOrEmpty(costumeKey)) return 0;
            if (minutes <= 0) return 0;
            if (CostumeCatalog.Find(costumeKey) == null) return 0;

            SyncDay();
            int room = CostumeEvolutionRules.RemainingDailyMinutes(s_minutesToday);
            int add = minutes < room ? minutes : room;
            if (add <= 0) return 0;

            for (int i = 0; i < s_records.Count; i++)
            {
                if (s_records[i].costumeKey != costumeKey) continue;
                s_records[i].focusMinutes += add;
                s_minutesToday += add;
                IsDirty = true;
                return add;
            }

            s_records.Add(new CostumeFocusRecord { costumeKey = costumeKey, focusMinutes = add });
            s_minutesToday += add;
            IsDirty = true;
            return add;
        }

        /// <summary>
        /// 날짜가 넘어갔으면 오늘분을 0으로 돌린다. <b>새 타이머도 새 벽시계 읽기도 없다</b> —
        /// <see cref="CurrencyModel.DayIndex"/>가 이미 래칫으로 전진해 있고, 이 함수는 그 정수가
        /// 바뀌었는지만 본다.
        /// <para>★ 값이 실제로 바뀔 때만 <see cref="IsDirty"/>를 세운다 — 조회할 때마다 세우면
        /// 아무 일도 없는 날에 60초마다 디스크를 쓴다(종일 켜 두는 앱이다).</para>
        /// </summary>
        private static void SyncDay()
        {
            int today = CurrencyModel.DayIndex;
            if (today == s_minutesTodayDayIndex) return;

            s_minutesTodayDayIndex = today;
            if (s_minutesToday == 0) return;

            s_minutesToday = 0;
            IsDirty = true;
        }

        // ====================================================================
        // 영속화 (저장 스키마 v12)
        // ====================================================================

        /// <summary>
        /// 저장 파일에서 되살린다. <b>이벤트를 쏘지 않는다</b> — 복원 도중의 중간 상태를 UI가
        /// 그리지 않게 하는 관례(<see cref="CharacterSaveStore.Load"/>가 전부 끝난 뒤 한 번만 통지).
        ///
        /// <para><b>로드값을 믿지 않는다 — 그러나 거부하지도 않는다.</b> 경고도 실패도 없이 정규화한다:
        /// 이건 방어이기 전에 <b>위생</b>이고, 손상된 파일·구버전 파일에도 같은 코드가 같은 답을 낸다
        /// (<see cref="CurrencyModel.RestoreFromSave"/>와 같은 방침).</para>
        ///
        /// <para>★ <b>배열 길이 상한을 숫자로 적지 않는다.</b> «코드가 아는 키만 살린다»는 규칙이
        /// 길이를 <b>실린 코스튬 수</b>로 저절로 묶는다 — 숫자를 적으면 코스튬이 아홉 개가 되는 날
        /// 그 숫자만 옛 값을 지킨다.</para>
        ///
        /// <para>★ <b>모르는 키를 버리는 것의 대가를 여기 적어 둔다</b>(정직성):
        /// 매니페스트가 <b>앱 업데이트로 은퇴</b>하면 그 코스튬의 누적이 다음 저장에서 사라진다.
        /// 오늘 그 위험이 낮은 이유는 매니페스트가 앱 번들 안에 실려 <b>전원에게 같은 목록</b>이고
        /// (팩 «보유»는 별개 축이다), 구버전 앱이 신버전 세이브를 여는 경로는
        /// <c>data.version &gt; CurrentVersion</c> 가드가 이미 막기 때문이다.
        /// 코스튬을 <b>은퇴시키는 라운드</b>는 이 문단을 먼저 읽어야 한다.</para>
        /// </summary>
        internal static void RestoreFromSave(CostumeFocusSaveState state)
        {
            s_records.Clear();

            if (state.Records != null)
            {
                for (int i = 0; i < state.Records.Length; i++)
                {
                    CostumeFocusRecord record = state.Records[i];
                    if (record == null) continue;
                    if (string.IsNullOrEmpty(record.costumeKey)) continue;

                    // 코드가 모르는 키는 버린다(위 문단).
                    if (CostumeCatalog.Find(record.costumeKey) == null) continue;

                    // 중복은 파일 손상의 흔적 — 조용히 첫 항목만 살린다.
                    if (MinutesOfExisting(record.costumeKey)) continue;

                    // 음수는 0으로 본다. 그리고 <b>0분짜리는 목록에 넣지 않는다</b> —
                    // 「없음 = 기록 없음」이라는 이 필드의 뜻을 로드 경로에서도 성립시킨다
                    // (안 그러면 다음 저장이 0분 레코드를 디스크에 다시 굳힌다).
                    int minutes = record.focusMinutes;
                    if (minutes <= 0) continue;

                    s_records.Add(new CostumeFocusRecord
                    {
                        costumeKey = record.costumeKey,
                        focusMinutes = minutes,
                    });
                }
            }

            // ★ 오늘분은 «오늘»과 짝이라 CurrencyModel 복원 뒤에 와야 한다(클래스 문서 마지막 문단).
            s_minutesTodayDayIndex = CurrencyModel.DayIndex;
            int today = state.MinutesToday;
            if (today < 0) today = 0;
            if (today > CostumeEvolutionRules.DailySoftCapMinutes) today = CostumeEvolutionRules.DailySoftCapMinutes;
            s_minutesToday = today;

            IsDirty = false;
        }

        /// <summary>이 키가 이미 목록에 있는가(중복 정규화 전용).</summary>
        private static bool MinutesOfExisting(string costumeKey)
        {
            for (int i = 0; i < s_records.Count; i++)
            {
                if (s_records[i].costumeKey == costumeKey) return true;
            }
            return false;
        }

        /// <summary>저장 파일에 적을 한 덩어리. <b>복사본을 내준다</b> —
        /// 안 하면 세이브가 들고 간 배열이 곧 모델의 배열이라 한쪽을 고치면 다른 쪽이 따라 바뀐다.</summary>
        internal static CostumeFocusSaveState CaptureSaveState()
        {
            var copy = new CostumeFocusRecord[s_records.Count];
            for (int i = 0; i < s_records.Count; i++)
            {
                copy[i] = new CostumeFocusRecord
                {
                    costumeKey = s_records[i].costumeKey,
                    focusMinutes = s_records[i].focusMinutes,
                };
            }

            return new CostumeFocusSaveState
            {
                Records = copy,
                MinutesToday = MinutesToday,
            };
        }

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).</summary>
        public static void ResetForTesting()
        {
            s_records.Clear();
            s_minutesToday = 0;
            s_minutesTodayDayIndex = 0;
            IsDirty = false;
        }
    }
}
