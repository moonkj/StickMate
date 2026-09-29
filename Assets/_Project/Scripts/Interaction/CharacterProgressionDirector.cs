using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 캐릭터 성장의 "언제"를 담당하는 유일한 주체 — 2026-08-29 성장/장비 라운드.
    ///
    /// Core/CharacterProgressionModel.cs는 값만 보관하고, 이 컴포넌트가 XP가 들어오는 네 경로를 전부
    /// 소유한다(StressGauge ↔ StressGaugeDirector와 정확히 같은 분리).
    ///
    /// ============================================================================
    /// XP 소스 — 기존 판정 로직을 <b>한 줄도</b> 건드리지 않는다
    /// ============================================================================
    /// 리더 지시: "기존 판정 로직에 읽기 전용으로 훅만 걸어라 — 승패 판정 자체를 바꾸지 마라."
    /// 그래서 보너스는 <b>전부 StickmanEventBus 구독</b>으로만 구현했다. 이 파일은
    /// ArcheryState를 <b>참조조차 하지 않는다</b>
    /// (grep으로 검증 가능) — 그곳의 소스 코드는 이번 라운드에 수정되지 않았다.
    ///
    ///  · 패시브        : progressionPassiveTickSeconds 주기로 분당 값을 쪼개 적립.
    ///                    "아무것도 안 해도 자란다"(관찰형 앱 철학)가 주 경로다.
    ///  · 활쏘기 명중    : ArcheryShotChanged.Result == Bullseye (Release 시점 1회)
    ///                    ★★ 2026-09-07 보안 결함 수정(design-systems 발견, §3-3) — 옛 코드는
    ///                    쿨다운(600초)·일일 상한(72회)에 막혀도 XP는 <b>무조건</b> 나갔다 —
    ///                    연속 도배 시 시간당 ~6,478XP(패시브의 72배)로 Lv50 전체 요구량을
    ///                    22.4시간에 채우는 익스플로잇이었다. 지금은
    ///                    <see cref="ClaimArcheryAward"/>가 돌려주는 값이 0이면 XP도 지급하지 않는다 —
    ///                    XP 전용 쿨다운을 새로 만들지 않고 <b>이미 있는 판정 한 곳</b>을 재사용한다.
    ///                    ★★★ 2026-09-29 DLC·재화 폐지 R5 — 그 훅이 내던 <b>동전 20이 사라졌다</b>.
    ///                    <b>관문은 그대로다</b>: <c>CurrencyModel.TryClaimArcheryAward</c>가 쿨다운·
    ///                    일일 총량을 한 글자도 안 바꾼 채 판정만 하고, 이 파일은 계속 그 결과를
    ///                    XP 게이트로 쓴다. <b>관문을 「동전이 없어졌으니」 지우면 위 구멍이 다시 열린다.</b>
    ///
    /// ★ 2026-09-02 — 보너스 소스가 <b>2종에서 1종</b>이 됐다(격파 승리 +25XP 삭제, 격파 놀이 기능
    ///   제거). 패시브가 주 경로라는 설계 덕에 성장 속도에 미치는 영향은 사실상 없다 —
    ///   위 XP 곡선 표(CharacterProgressionModel)는 애초에 패시브만으로 계산된 값이다.
    ///
    ///  · 집중 모드 완주/취소(2026-09-07, design-systems §15) — <see cref="GrantFocusCompletionXp"/>·
    ///    <see cref="GrantFocusCancelXp"/>. 위 둘과 달리 <b>이벤트 구독이 아니라 직접 호출</b>이다 —
    ///    <c>FocusWatchDirector</c>가 코인도 같은 방식(직접 호출)으로 지급하고 있어 그 관례를
    ///    따랐다(그 코인 지급은 2026-09-29에 폐지됐고, 이 XP 경로만 남았다). 산식은
    ///    <c>CurrencyRules.FocusCompletionXp</c>/<c>FocusCancelXp</c> 한 곳에만 있고, <b>일일 상한
    ///    (<c>CurrencyRules.FocusXpDailyCap</c>)은 코인과 달리 존재한다</b> — 활쏘기/패시브와
    ///    달리 코인 쪽 §22-13("집중 지급은 일일 상한 밖")을 XP는 물려받지 않는다(design-systems
    ///    §15-4: 레벨 페이싱 전체가 XP 하나를 조율하므로 상한 없이 열면 그 설계가 무너진다).
    ///    상한 체크와 카운터(<c>CurrencyModel.FocusXpToday</c>)는 <c>CurrencyModel</c>이 맡고,
    ///    이 컴포넌트는 그 결과(클램프된 XP)를 받아 <b>기존 <see cref="Grant"/>를 그대로 재사용</b>한다
    ///    — 레벨업 로그·즉시 저장·장비 해금 알림이 다른 세 경로와 동일하게 딸려온다.
    ///
    /// ============================================================================
    /// ★ 2026-09-06 — 재화 <b>일일 롤오버</b>의 구동자도 여기다
    /// ============================================================================
    /// XP가 아닌 책임이 하나 더 붙었다. 이유는 «성장과 재화가 같은 종류의 것»이어서가 아니라
    /// <b>이 컴포넌트가 세이브 파일의 「언제」를 이미 전부 쥐고 있어서</b>다 —
    /// 로드 · 주기 저장 · 종료 저장 · <c>IsAnythingDirty</c>. 롤오버를 다른 컴포넌트에 두면
    /// 같은 파일을 두 컴포넌트가 서로 다른 박자로 만지게 되고, 그건 이 파일이
    /// <c>OnApplicationQuit</c> 주석에서 이미 «주기/종료 저장 경로는 이 컴포넌트 하나로 유지한다»고
    /// 못박아 둔 것과 정면으로 어긋난다.
    ///
    /// <para>배선 지점은 둘뿐이다 — <see cref="Start"/>(로드 직후 1회, 「며칠 만에 재실행」) ·
    /// <see cref="Update"/> 맨 앞(「켠 채로 자정 통과」). 주기와 게이트는
    /// <c>Core/CurrencyDayRolloverTicker.cs</c>가 알고, 날짜 판정은
    /// <c>Core/CurrencyModel.TickDayRollover</c>가 안다. 이 파일은 <b>둘을 잇기만 한다</b>.</para>
    ///
    /// ============================================================================
    /// ★★★ 2026-09-29 DLC·재화 폐지 R5 — <b>여기서 흐르던 동전 셋 중 둘이 사라졌다</b>
    /// ============================================================================
    /// 이 파일에서 뗀 것:
    /// <list type="bullet">
    ///  <item><b>첫 실행 시드</b>(<c>TryGrantSeedCoinsOnce</c>와 <see cref="Start"/>의 그 한 줄) —
    ///    삭제. 「평생 1회」를 보장하던 세이브 필드 <c>seedGranted</c>는 스키마에 그대로 남아
    ///    왕복만 한다. <b>되살리지 마라</b> — 기존 사용자 전원의 플래그가 <c>false</c>라 되살리는
    ///    순간 전원에게 한 번 더 나간다(<c>CurrencyRules</c>의 「첫 실행 시드」 절).</item>
    ///  <item><b>유휴 수급</b>(<c>AccrueIdleIncome</c> · <c>LogIdleStallOnce</c> ·
    ///    <c>LogIdleIncomeIfDue</c>와 그 네 필드) — 삭제. 그 배선의 본체는 지급이 아니라
    ///    <b>기산점을 조건 없이 전진시키는 것</b>이었다(집중 세션 동안 멈춰 두면 세션이 끝난 첫 틱이
    ///    그 25분을 유휴로 <b>한 번 더</b> 지급한다 — I-7′ 파손). <b>두 번째 적립 축을 새로 만드는
    ///    라운드는 이 문장을 먼저 읽어라.</b></item>
    ///  <item><b>활쏘기 상금</b> — <b>판정은 남기고 지급만 뗐다.</b> <see cref="ClaimArcheryAward"/>가
    ///    <c>CurrencyModel.TryClaimArcheryAward</c>를 계속 부르고, 그 반환값이 <b>XP 게이트</b>다
    ///    (2026-09-07 보안 결함 수정의 본체 — 위 XP 소스 절).</item>
    /// </list>
    ///
    /// ★ <b>남은 것 — 이 파일이 여전히 소유하는 재화 책임 하나</b>: 일일 롤오버 구동
    /// (<c>_dayRollover</c>). <c>CurrencyModel.DayIndex</c>는 [오늘 할일] 날짜축이 읽는
    /// <b>「오늘」의 유일 출처</b>라 재화와 함께 지울 수 없다.
    ///
    /// <para>★ 활쏘기는 <b>이미 있는 명중 훅</b>(<see cref="OnArcheryShotChanged"/>)에 얹는 것이
    /// «명중 1회 = 판정 1회»를 한 이음매로 유지하는 유일한 방법이다. 쿨다운(단조 시계 600초)과
    /// 일일 총량은 <c>CurrencyModel.TryClaimArcheryAward</c> 안에만 있고, 이 파일은
    /// <b>언제 물어볼지</b>만 안다.</para>
    ///
    /// <para>★ <b>쿨다운·상한 숫자는 이 파일에 한 개도 없다</b>(<c>CurrencyRules</c> 전용).
    /// 여기에 600이나 72를 적으면 그 순간 같은 사실이 두 곳에 살게 된다.</para>
    ///
    /// ============================================================================
    /// 매 프레임 할당 금지 (24시간 상주 앱)
    /// ============================================================================
    /// Update()는 타이머 두 개만 굴리고 임계값을 넘을 때만 일한다. 문자열 보간은 실제로 XP가 들어온
    /// 순간(패시브는 10초에 한 번)과 레벨업/저장 시점에만 일어난다.
    ///
    /// ============================================================================
    /// 원칙 1(행동-텍스트 싱크) — 무관하다
    /// ============================================================================
    /// 레벨업해도 대사를 만들지 않는다. 이 컴포넌트는 DialogueIntent를 생성하지도, ChangeState를
    /// 호출하지도 않으므로 SpectacleEventLock에도 참여하지 않는다(StressGauge/HardwareReaction의
    /// "순수 오버레이는 락에 참여하지 않는다"와 같은 기준).
    /// </summary>
    public sealed class CharacterProgressionDirector : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private StickmanAgent _agent;
        private float _passiveTimer;
        private float _autoSaveTimer;

        // ====================================================================
        // ★★★ 2026-09-29 DLC·재화 폐지 R5 — 유휴 수급 배선이 통째로 사라졌다
        // ====================================================================
        // 지운 필드: <c>_focusWatch</c>(집중 세션 조회) · <c>_idleTickMonotonic</c>(기산점) ·
        //   <c>_idleLogMonotonic</c> · <c>_idleCoinsSinceLog</c> · <c>_idleStallLogged</c> ·
        //   <c>IdleIncomeLogIntervalSeconds</c>, 그리고 정지 로그 표지 4개
        //   (<c>IdleStallLogMarker</c> · <c>IdleStallCapPhrase</c> · <c>IdleStallWindowPhrase</c> ·
        //   <c>IdleStallUnknownPhrase</c> — 테스트가 문장을 베끼지 않도록 상수로 두었던 것들).
        //
        // ★ <b>여기 있던 관측 설계를 기록으로 남긴다</b>(다음에 「조용히 멈추는 기능」을 만들 때 읽어라):
        //   ① 상한/창에 걸린 상태는 화면에서 <b>고장과 똑같이 생겨서</b> 전용 로그가 필요했다.
        //   ② 그런데 그 로그가 «지급액 0»을 정지로 읽어 <b>정상 동작을 5초에 한 번 고장으로 신고</b>했다
        //      (실측 720줄/시간). ③ 고친 방법은 판정 기준을 «지급액»에서 «창이 갉혔는가»로 바꾼 것 —
        //      후자는 «지급이 실제로 일어난 초»에만 값이 있어 0과 0이 다른 사실이 된다.
        //   ④ 그리고 사유를 <b>추측하지 않고</b> 둘을 각각 확인했다(둘 다 아니면 그렇다고 적었다).

        /// <summary>
        /// ★ <b>일일 롤오버의 유일한 구동자</b>(2026-09-06 배선). 「오늘의 것」을 되돌리는 코드는
        /// <c>Core/CurrencyModel.TickDayRollover</c> 하나뿐이다(불변식 I-15′).
        ///
        /// <para>★★ <b>2026-09-29 — 재화가 폐지된 뒤에도 이 배선은 그대로 남는다.</b> 이 롤오버가
        /// 전진시키는 <c>CurrencyModel.DayIndex</c>는 [오늘 할일] 날짜축
        /// (<c>Interaction/TodoBoardPopover.TodayIndex</c>)이 읽는 <b>「오늘」의 유일 출처</b>이고,
        /// 활쏘기·집중 XP의 일일 천장도 여기서 열린다. <b>「재화 정리」 명목으로 지우지 마라</b> —
        /// 지우면 [오늘 할일]의 날짜가 앱 생애 동안 고정되고, XP 천장이 하루 뒤 영구히 닫힌다.</para>
        ///
        /// <para><b>왜 이 파일인가</b>: 여기가 이미 세이브 파일의 「언제」를 전부 쥐고 있다 —
        /// 로드(<see cref="Start"/>) · 주기 저장 · 종료 저장 · <c>CurrencyModel.IsDirty</c> 합류
        /// (<see cref="IsAnythingDirty"/>). 롤오버를 다른 컴포넌트에 두면
        /// <b>같은 파일을 두 컴포넌트가 서로 다른 박자로 만지게</b> 된다.
        /// <c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §17-11의 순서 1이 지목한 자리도 여기다.</para>
        ///
        /// <para>★ 주기·게이트는 이 필드가 아니라 <c>CurrencyDayRolloverTicker</c>가 안다.
        /// 이 파일에는 <b>날짜 판정이 한 줄도 없다</b>.</para>
        /// </summary>
        private readonly CurrencyDayRolloverTicker _dayRollover = new CurrencyDayRolloverTicker();

        // 활쏘기 한 발은 Aim/Release 두 번 발행된다 — 같은 발을 두 번 세지 않도록 마지막으로 보상한
        // 발의 인덱스를 기억한다(TodoPostItWidget의 TryClaimAction과 같은 성격의 중복 방어).
        //
        // ★★ 2026-09-06 발견 — <b>미수정 결함(리더 배정 대기)</b>. 이 방어는 <b>세션 경계에서
        //    오탐한다</b>: 활쏘기 사이클마다 발 인덱스가 0부터 다시 매겨지므로, 앞 사이클의
        //    <b>마지막 보상 인덱스</b>와 다음 사이클의 <b>첫 보상 인덱스</b>가 같으면(예: 둘 다 2번 발)
        //    두 번째 것이 <b>조용히 버려진다</b>. 형제 파일 <c>CharacterStatsDirector</c>는 같은 방어를
        //    쓰면서 <c>ArcheryOverlayChanged(Started)</c>에서 <c>-1</c>로 되돌려 이 구멍을 이미 막았다
        //    (그 파일의 <c>OnArcheryOverlayChanged</c>). 여기는 그 구독이 없다.
        //    ⇒ 영향: XP 보너스가 그 경우 누락된다(2026-09-29 이전에는 동전도 함께였지만, 동전은
        //      600초 쿨다운이라 대부분 지연에 그쳤다 — 지금은 <b>XP 누락만</b> 남았고 영향이 더 크다).
        //    ⇒ <b>이번 라운드에서도 고치지 않았다</b> — XP 거동을 바꾸는 변경이고, 먼저 빨간 테스트를
        //      세워야 한다(CLAUDE.md). 고칠 때는 형제 파일의 형태를 그대로 가져오면 된다.
        private int _lastRewardedShotIndex = -1;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 복제본에 이 컴포넌트가 남아 있어도
            // XP가 두 배로 들어가지 않게 하는 2차 방어(1차 방어는 SceneBootstrapper의 제거).
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;

            // ★ 2026-09-29 — 여기 있던 `_focusWatch = GetComponent<FocusWatchDirector>();`를 뗐다.
            //   그 참조의 유일한 독자가 유휴 수급(«집중 세션 중인가»)이었고 그 배선이 폐지됐다.
            //   ⚠ 집중 모드 <b>XP</b>는 반대 방향이다 — FocusWatchDirector가 이 컴포넌트를 찾아
            //   GrantFocusCompletionXp/GrantFocusCancelXp를 부른다. 그 방향은 건드리지 않았다.
        }

        private void Start()
        {
            if (_agent == null)
            {
                enabled = false;
                return;
            }

            CharacterSaveStore.Load();

            // ★ 2026-09-01 구석 호버 패널 삭제로 이사 온 두 가지 책임(그 패널이 유일한 주인이었다).
            //   (1) 저장된 캐릭터 크기 복원. 여기서 하는 이유는 <b>순서 때문</b>이다 — 바로 위에서
            //       저장 파일을 읽은 그 호출자가 곧바로 부르므로, 옛 구현이 안고 있던 "누가 먼저 도는지
            //       모른다"(매 프레임 재시도 + 2초 유예 마감) 경주가 아예 성립하지 않는다.
            //   (2) Bind. 설정창도 자기 Start에서 부르지만 두 Start의 순서는 보장되지 않는다.
            //       멱등이라 둘 다 불러도 안전하고, 여기 한 줄이 있어야 설정창이 없는 조립에서도 산다.
            CharacterScaleController.Bind(_agent);
            CharacterScaleController.RestoreFromSaveModel();

            // ★★ 「며칠 만에 다시 켰다」가 해소되는 자리 — 반드시 위 CharacterSaveStore.Load() <b>뒤</b>다.
            //    앞에서 부르면 기본값(DayIndex = 0) 위에서 한 번 굴러가고 로드가 그것을 덮어써
            //    <b>아무 일도 안 한 것이 된다</b>(그리고 그 실패는 로그도 예외도 안 남긴다).
            //    주기 틱(Update)을 기다리지 않는 이유: 기다리면 그 사이 60초 동안 어제 상태로 산다 —
            //    그 창에서 「오늘 이미 다 받았다」를 보게 되는 채널이 실재한다: 활쏘기 보상 판정
            //    (archeryCoinsToday)이 XP의 일일 천장이다. 즉 이 줄은 가정이 아니라 <b>실효 방어</b>다.
            //    ★ 2026-09-29 — 여기 있던 「유휴·[오늘 할일]은 아직이다」는 폐지로 영구히 해소됐다.
            if (_dayRollover.CheckNow(Time.realtimeSinceStartupAsDouble)) LogDayRollover("실행 직후");

            // ★★★ 2026-09-29 — 이 자리에 있던 `TryGrantSeedCoinsOnce();`를 뗐다(첫 실행 시드 폐지).
            //    ★ 그 한 줄이 여기 있어야 했던 이유는 기록으로 남긴다: 반드시 위
            //      CharacterSaveStore.Load() <b>뒤</b>여야 했다 — 앞에서 부르면 RestoreFromSave가
            //      디스크의 seedGranted(=false)를 그대로 덮어써 지급이 통째로 사라지고, 그 실패는
            //      예외도 로그도 남기지 않은 채 <b>다음 실행에서도 똑같이</b> 사라졌다.
            //      「로드 뒤에 지급」은 새 지급 채널을 배선할 때마다 같은 함정으로 돌아온다.

            Debug.Log($"[성장] 준비 완료 — {CharacterProgressionModel.CharacterName} Lv.{CharacterProgressionModel.Level} " +
                $"({CharacterProgressionModel.CurrentXp:F0}/{CharacterProgressionModel.XpToNextLevel(_config):F0} XP). " +
                $"저장 파일={(CharacterSaveStore.LoadedFromFile ? "불러옴" : "없음 — 새 캐릭터로 시작")} " +
                $"({CharacterSaveStore.FilePath}). " +
                $"패시브 {(_config != null ? _config.progressionPassiveXpPerMinute : 0f):F1}XP/분.");
        }

        private void OnEnable()
        {
            StickmanEventBus.ArcheryShotChanged += OnArcheryShotChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.ArcheryShotChanged -= OnArcheryShotChanged;
        }

        private void OnApplicationQuit()
        {
            // 종료 직전 마지막 저장 — 주기 저장만 있으면 최대 1분치가 날아간다.
            // 기록(Core/CharacterStatsModel.cs)과 사용자가 옮긴 톱니 위치(Core/UiLayoutModel.cs)도 같은
            // 파일에 들어가므로 함께 본다 — 주기/종료 저장 경로는 이 컴포넌트 하나로 유지한다
            // (두 컴포넌트가 같은 파일을 번갈아 덮어쓰지 않게).
            if (IsAnythingDirty()) CharacterSaveStore.Save();
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측

            // ================================================================
            // ★★ 날짜 롤오버 — <b>이 Update에서 가장 먼저</b> 돈다
            // ================================================================
            // 앱을 켠 채로 자정을 넘긴 경우가 여기서 해소된다(재실행 경로는 Start의 CheckNow).
            //
            // ★ <b>지급 배선을 이 Update에 넣는다면 반드시 이 줄 「아래」다.</b> 위에 넣으면 자정 직후
            //   한 틱이 어제 카운터를 보고, 그 한 틱은 상한에 걸려 조용히 0을 준다 — 화면에도
            //   로그에도 아무 흔적이 없다. ★ 2026-09-29 현재 이 Update에 지급 배선은 <b>없다</b>
            //   (유휴 수급 폐지). 활쏘기는 이벤트 훅이고, 집중 모드 XP는 FocusWatchDirector가 부른다.
            //
            // ★ 일시정지(전체화면 게임 감지) 상태에서도 <b>멈추지 않는다</b>. 하루가 넘어간 것은
            //   달력의 사실이지 우리 연출 상태가 아니고, 저녁 내내 게임한 사용자만 리셋을 못 받는
            //   결과가 된다. FocusWatchDirector가 IsSuspended에서 반환하는 것과 성격이 다르다.
            //
            // 비용: 대부분의 프레임에서 double 뺄셈 1회(주기 게이트). 달력을 실제로 읽는 것은
            //   CurrencyDayRolloverTicker.CheckIntervalSeconds마다 한 번뿐이다.
            if (_dayRollover.TickIfDue(Time.realtimeSinceStartupAsDouble)) LogDayRollover("가동 중 날짜 경계 통과");

            // ★ 배율 적용 유예(랙돌/스펙터클 중)를 푸는 <b>상시 구동자</b>. 설정창도 부르지만 그쪽
            //   Update는 `if (!_open) return;`으로 시작한다 — 창을 닫으면 유예가 영영 안 풀려서
            //   "랙돌 중에 크기를 바꾸고 창을 닫으면 그 크기가 사라지는" 버그가 된다.
            //   대기 값이 없으면 즉시 반환하는 0비용 호출이다(24시간 상주 앱: 할당 0).
            CharacterScaleController.Tick();

            float passiveInterval = _config != null ? Mathf.Max(1f, _config.progressionPassiveTickSeconds) : 10f;
            _passiveTimer += Time.unscaledDeltaTime;
            if (_passiveTimer >= passiveInterval)
            {
                _passiveTimer -= passiveInterval;
                float perMinute = _config != null ? _config.progressionPassiveXpPerMinute : 0f;
                if (perMinute > 0f) Grant(perMinute * (passiveInterval / 60f), null);
            }

            float saveInterval = _config != null ? Mathf.Max(5f, _config.progressionAutoSaveIntervalSeconds) : 60f;
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= saveInterval)
            {
                _autoSaveTimer -= saveInterval;
                if (IsAnythingDirty()) CharacterSaveStore.Save();
            }
        }

        // ====================================================================
        // 유휴 수급 (2026-09-06 3차 배선) — ★★★ 2026-09-29 <b>폐지</b>
        // ====================================================================
        //
        // 지운 메서드: <c>AccrueIdleIncome()</c> · <c>LogIdleStallOnce()</c> ·
        //   <c>LogIdleIncomeIfDue(double)</c>. 배선 지점(<c>Update</c>의 롤오버 바로 아래)과
        //   <c>Awake</c>의 <c>_focusWatch</c> 조회, <c>Start</c>의 조립 경고도 함께 뗐다.
        //
        // ★ 이 절이 지키던 두 사실은 위쪽 「지운 것」 문단과 필드 절에 남겼다(기산점 무조건 전진 /
        //   «멈췄다»의 기준은 지급액이 아니라 창).

        /// <summary>같은 파일에 실리는 모델 중 하나라도 바뀌었는가 — 안 바뀌었으면 디스크를 두드리지
        /// 않는다(하루 종일 켜져 있는 앱이다).</summary>
        private static bool IsAnythingDirty()
            => CharacterProgressionModel.IsDirty || CharacterStatsModel.IsDirty || UiLayoutModel.IsDirty
               || TodoListModel.IsDirty    // v4 — 사용자가 적은 할일은 반드시 남아야 한다.
               || CharacterAppearanceModel.IsDirty    // v7 — 잉크색(우클릭 메뉴/단축키 경로는 즉시 저장을 부르지 않는다).
               || AppSettingsModel.IsDirty            // v8 — 설정창(슬라이더는 드래그 중 즉시 저장을 부르지 않는다).
               // ★ 등급 해금 · 장착한 춤 · 하루 경계 · 활쏘기/집중 XP 카운터. 이 한 줄이 빠지면
               //   그것들이 60초 주기 저장에도 종료 시 저장에도 실리지 않는다.
               //   (그리고 그 실패는 초록 테스트와 똑같이 생겼다 — 즉시 저장 경로만 보는 테스트는 통과한다.)
               //   ★ 2026-09-29 — 옛 주석은 「동전·구매 이력」을 이 줄의 이유로 적었다. 그 축은
               //   폐기됐지만 <b>이 줄은 더 중요해졌다</b>: 이제 등급 해금과 XP 천장이 여기 실린다.
               || CurrencyModel.IsDirty;

        /// <summary>
        /// 롤오버가 실제로 일어났을 때만 한 줄. <b>하루에 한 번</b>인 사건이라 상시 비용이 아니다.
        ///
        /// <para>★ <b>이 로그가 없으면 이 기능은 관측할 수 없다.</b> 롤오버는 «어제 다 썼던 것이
        /// 다시 된다»는 <b>부재의 해소</b>라, 성공했을 때 화면에 아무 일도 일어나지 않는다.
        /// 고장났을 때와 정상일 때가 똑같이 생겼다 — 이 저장소가 반복해서 당한 형태다.</para>
        ///
        /// <para>★ <b>여기서 저장을 강제하지 않는다.</b> 롤오버는 <c>CurrencyModel.IsDirty</c>를
        /// 세우므로 주기 저장(60초)과 종료 저장이 이미 싣는다. 그리고 그 둘을 <b>둘 다 놓쳐도</b>
        /// (전원이 끊기는 등) 파일에는 어제 날짜가 남아 있어 <b>다음 실행의 <see cref="Start"/>가
        /// 같은 롤오버를 다시 일으킨다</b> — 스스로 낫는다. 하루 한 번을 위해 디스크를 한 번 더
        /// 두드릴 이유가 없다(24시간 상주 앱이다).</para>
        /// </summary>
        private void LogDayRollover(string why)
        {
            // ★ 2026-09-29 — 문장에서 동전 문구를 뺐다. 롤오버가 실제로 여는 것은 이제 둘이다:
            //   활쏘기 보상 판정의 오늘 총량과 집중 모드 XP 일일 상한. 그리고 일자 번호가 전진한다.
            Debug.Log($"[성장] 날짜 롤오버({why}) — 일자 #{CurrencyModel.DayIndex}. " +
                $"오늘 활쏘기 보상 판정과 집중 모드 XP 상한(잔여 " +
                $"{CurrencyModel.RemainingFocusXpRoomToday()}XP)이 다시 열렸습니다. " +
                "이 일자 번호는 [오늘 할일] 날짜축이 읽는 «오늘»과 같은 값입니다. " +
                $"경계 오프셋 {CurrencyModel.DayBoundaryOffsetMinutes}분(고정됨=" +
                $"{CurrencyModel.HasDayBoundaryOffset}). 저장은 다음 주기/종료 저장에 실립니다.");
        }

        // ==================== 보너스 훅(전부 읽기 전용 구독) ====================

        private void OnArcheryShotChanged(ArcheryShotEvent shot)
        {
            if (shot.Result != ArcheryShotResult.Bullseye) return;
            if (shot.Phase != ArcheryShotPhase.Release) return;   // Aim/Release 중 한 번만.
            if (shot.ShotIndex == _lastRewardedShotIndex) return; // 같은 발 재발행 방어.
            _lastRewardedShotIndex = shot.ShotIndex;

            // ★★★ 2026-09-07 보안 결함 수정(design-systems 발견, §3-3) — <b>XP를 보상 판정의 성패에
            //    묶는다</b>. 옛 코드는 위 세 관문만 지나면 XP를 <b>무조건</b> 지급했다 — 쿨다운(단조
            //    600초)·일일 상한(72회)이 XP에는 없어서, 연속 도배 시 시간당 ~6,478XP(패시브의 72배)로
            //    Lv50 전체 요구량을 22.4시간 만에 채울 수 있었다
            //    (docs/DESIGN_SYSTEMS_LEVEL_STAT_GROWTH_PROPOSAL.md §3-3). <b>새 쿨다운/카운터를
            //    XP 전용으로 만들지 않는다</b> — <c>CurrencyModel.TryClaimArcheryAward</c>가 이미
            //    계산한 판정(쿨다운 통과 + 오늘 상한 이내)을 <c>&gt; 0</c>으로 그대로 재사용한다.
            //    판정처가 하나면 「한쪽은 막혔는데 다른 쪽은 새는」 갈라짐이 구조적으로 불가능해진다.
            //
            // ★★ 2026-09-29 DLC·재화 폐지 R5 — 여기서 나가던 <b>동전 20이 사라졌다</b>. 그런데
            //    <b>관문은 그대로 남겼다</b>: 위 문단이 못박은 XP 방어선이 정확히 그 관문이기 때문이다.
            //    「동전이 없어졌으니 활쏘기 판정도 지우자」는 <b>보안 회귀</b>다.
            int awarded = ClaimArcheryAward();
            if (awarded > 0)
            {
                Grant(_config != null ? _config.progressionBullseyeXp : 0f, "활쏘기 정중앙 명중");
            }
        }

        /// <summary>
        /// 활쏘기 정중앙 <b>1회당</b> 보상 판정. ★ 세션 완료당이 아니다 —
        /// <c>DESIGN_SYSTEMS_STATS</c> §13-3 표가 <i>"활쏘기 정중앙 1회 · 쿨다운 600초"</i>이고,
        /// 그래서 이 호출은 <b>명중 판정이 확정되는 그 자리</b>(위 훅)에 붙는다.
        ///
        /// <para>★★ <b>2026-09-29 — 이 함수는 더 이상 동전을 내지 않는다</b>(옛 이름
        /// <c>AwardArcheryCoins</c>). 반환값의 유일한 용도는 <b>XP 게이트</b>다.</para>
        ///
        /// <para>★ <b>판정 여부를 이 파일이 정하지 않는다.</b> 쿨다운(단조 시계 600초)도 일일 총량도
        /// <c>CurrencyModel.TryClaimArcheryAward</c> 안에만 있다. 여기서 «쿨다운이 지났는지»를 한 번 더
        /// 계산하면 같은 사실이 두 곳에서 살게 되고, 그 둘은 반드시 갈라진다.</para>
        ///
        /// <para>★ <b>0일 때도 로그를 남긴다.</b> 명중 연출은 그대로 도는데 XP만 안 나오는 화면은
        /// <b>고장과 똑같이 생겼다</b>. 실제로 이 저장소는 «지급이 고장났다»는 오진을 반복해서 받았다.
        /// 쿨다운 대기인지 오늘 상한 도달인지를 구분해 적는 이유도 같다 — 뒤쪽은 <b>내일까지
        /// 안 나온다</b>는 다른 사실이다(§20-8).</para>
        ///
        /// <para>★ <b>여기서 저장을 강제하지 않는다.</b> 판정은 <c>CurrencyModel.IsDirty</c>를 세우고
        /// 주기 저장(60초)·종료 저장이 이미 싣는다(<see cref="IsAnythingDirty"/>).
        /// 최악 손실은 1주기이고, 그 대가로 하루 종일 켜 두는 앱이 명중마다 디스크를 두드리지 않는다 —
        /// <c>DESIGN_SYSTEMS_STATS</c> §20-7 저장 빈도표가 이 채널에 대해 명시적으로 고른 저울이다.</para>
        /// </summary>
        /// <returns>관문을 통과한 단위(0이면 쿨다운 중이거나 오늘 상한에 도달 — 이때 XP도 지급하지 않는다).</returns>
        private int ClaimArcheryAward()
        {
            // 단조 시계다. 벽시계(DateTime.Now)를 넣으면 시계를 600초 되감는 것만으로 무한 파밍이
            // 되고(§20-3-b), Tests/EditMode/DailyLimitClampAuditTests가 그 순간 빨개진다.
            int awarded = CurrencyModel.TryClaimArcheryAward(Time.realtimeSinceStartupAsDouble);

            if (awarded > 0)
            {
                Debug.Log("[성장] 활쏘기 정중앙 명중 — 보상 관문 통과(XP 지급). " +
                    $"오늘 활쏘기 누계 {CurrencyModel.ArcheryCoinsToday}/" +
                    $"{CurrencyRules.ArcheryDailyCoinLimit}. 저장은 다음 주기/종료 저장에 실립니다.");
                return awarded;
            }

            Debug.Log("[성장] 활쏘기 정중앙 명중 — XP 보류. " +
                (CurrencyModel.ArcheryDailyLimitReached
                    ? $"오늘 활쏘기 보상이 상한({CurrencyModel.ArcheryCoinsToday})에 도달했습니다 — " +
                      "고장이 아니라 의도된 천장이고(§20-3-b), 날짜가 바뀌면 다시 열립니다. " +
                      "집중 모드와 패시브로는 계속 자랍니다."
                    : "보상 쿨다운 중입니다 — 고장이 아니라 의도된 간격이고(§20-3-b), " +
                      "쿨다운이 풀린 뒤 첫 정중앙에서 다시 나옵니다. " +
                      "쿨다운은 단조 시계로만 재므로 앱을 껐다 켜면 초기화됩니다(그 상한이 일일 총량입니다)."));
            return 0;
        }

        // 첫 실행 시드 — ★★★ 2026-09-29 <b>폐지</b>. 여기 있던 <c>TryGrantSeedCoinsOnce()</c>를 지웠다.
        //   1회 보장은 이 파일이 아니라 세이브 필드 <c>seedGranted</c> 하나에만 있었고, 그 필드는
        //   스키마에 그대로 남아 왕복만 한다. <b>되살리지 마라</b> — 기존 사용자 전원의 플래그가
        //   <c>false</c>라(금액이 0이던 기간에 플래그를 세우지 않는 설계였다) 되살리는 순간 전원에게
        //   한 번 더 나간다.

        // ==================== 집중 모드 XP (직접 호출, 2026-09-07) ====================

        /// <summary>집중 세션 <b>완주</b> XP — <c>FocusWatchDirector</c>가 세션 완주 시점에 부른다
        /// (2026-09-29까지는 코인 지급과 나란히였고, 지금은 이 XP 하나뿐이다).
        /// <para>상한 체크는 이 메서드가 하지 않는다 — <see cref="CurrencyModel.TryGrantFocusCompletionXp"/>가
        /// 오늘 이미 <c>CurrencyRules.FocusXpDailyCap</c>에 얼마나 가까운지를 판정해 클램프된 XP를
        /// 돌려주고, 여기는 그 결과가 0보다 클 때만 기존 <see cref="Grant"/>를 부른다 — 활쏘기가
        /// <see cref="ClaimArcheryAward"/>의 <c>&gt; 0</c>을 XP 게이트로 재사용하는 것과 <b>같은 모양</b>이다.</para>
        /// </summary>
        public void GrantFocusCompletionXp(double sessionDurationSeconds)
        {
            int xp = CurrencyModel.TryGrantFocusCompletionXp(sessionDurationSeconds);
            if (xp > 0) { Grant(xp, "집중 모드 완주"); return; }

            Debug.Log("[성장] 집중 모드 완주 — 0XP(오늘 상한 도달). " +
                $"고장이 아니라 의도된 천장입니다(오늘 집중 XP {CurrencyModel.FocusXpToday}/" +
                $"{CurrencyRules.FocusXpDailyCap}, design-systems §15-4). 날짜가 바뀌면 다시 열립니다.");
        }

        /// <summary>집중 세션 <b>중도 취소</b> XP — <c>FocusWatchDirector</c>가 취소/긴급정지 시점에 부른다.
        /// <para>0XP에는 <b>두 가지 다른 사유</b>가 있고 섞어 말하지 않는다: ① 경과가 1분 미만이면
        /// 산식 자체가 0을 낸다(§22-12와 같은 계단) — 상한이 아니라 "아직 안 쌓였다"는 사실이다.
        /// ② <see cref="CurrencyModel.FocusXpDailyLimitReached"/>가 참이면 그건 진짜 천장이다.</para>
        /// <para>★★ <b>2026-09-29 — ①에도 로그를 남기게 바꿨다.</b> 원래 ①은 조용히 넘어갔는데,
        /// 그 근거가 «코인 쪽 <c>PayCancelCoins</c>가 이미 같은 사유로 0을 알린다 — 두 번 말하지
        /// 않는다»였다. <b>그 코인 경로를 같은 라운드에 폐지했으므로 근거가 사라졌다.</b>
        /// 그대로 두면 1분 미만 취소가 <b>아무 기록도 남기지 않고</b> 끝나고, 그건 이 저장소가 반복해
        /// 받은 «지급이 고장났다» 오진을 그대로 불러온다(폐지된 <c>PayCancelCoins</c> 문서가 그 오진을
        /// 명시적으로 적어 뒀다). 이음매를 <b>잃은 것이 아니라 여기로 옮긴 것</b>이다.</para></summary>
        public void GrantFocusCancelXp(double elapsedSeconds)
        {
            int xp = CurrencyModel.TryGrantFocusCancelXp(elapsedSeconds);
            if (xp > 0) { Grant(xp, "집중 모드 중도 취소"); return; }

            if (!CurrencyModel.FocusXpDailyLimitReached)
            {
                Debug.Log($"[성장] 집중 모드 중도 취소 — 0XP(경과 {elapsedSeconds:F1}초). " +
                    "★ 1분을 채우지 못해 0입니다 — 고장이 아니라 의도된 계단이고(§22-12), " +
                    "패널티가 아니라 「아직 안 쌓였다」입니다. 다음 1분을 채우면 그때부터 붙습니다.");
                return;
            }

            Debug.Log("[성장] 집중 모드 중도 취소 — 0XP(오늘 상한 도달). " +
                $"고장이 아니라 의도된 천장입니다(오늘 집중 XP {CurrencyModel.FocusXpToday}/" +
                $"{CurrencyRules.FocusXpDailyCap}, design-systems §15-4). 날짜가 바뀌면 다시 열립니다.");
        }

        /// <summary>XP 적립의 단일 경로 — 레벨업 감지/즉시 저장/로그가 전부 여기 한 곳에만 있다.</summary>
        private void Grant(float amount, string bonusLabel)
        {
            if (amount <= 0f) return;

            int levelsGained = CharacterProgressionModel.AddXp(amount, _config);

            if (bonusLabel != null)
            {
                Debug.Log($"[성장] 보너스 +{amount:F0} XP ({bonusLabel}) — " +
                    $"Lv.{CharacterProgressionModel.Level} " +
                    $"{CharacterProgressionModel.CurrentXp:F0}/{CharacterProgressionModel.XpToNextLevel(_config):F0}.");
            }

            if (levelsGained <= 0) return;

            // 레벨업은 저장 시점이다(리더 지시). 이때 새 장비가 열렸는지도 함께 알린다.
            Debug.Log($"[성장] ★ 레벨업! Lv.{CharacterProgressionModel.Level - levelsGained} -> " +
                $"Lv.{CharacterProgressionModel.Level}. {DescribeNewUnlocks(levelsGained)}");
            StickmanEventBus.RaiseCharacterEquipmentChanged(); // 잠금 표시가 바뀌었다 — 정보창 갱신용.
            CharacterSaveStore.Save();
        }

        /// <summary>이번 레벨업으로 새로 열린 <b>아이템</b>을 사람이 읽는 문장으로. 없으면 그렇게 말한다.
        /// 2026-08-30 32종 확장 전에는 카테고리 4개만 훑었는데, 이제 해제는 아이템 단위라
        /// (카테고리는 처음부터 열려 있다) 카탈로그 전체를 훑는다. 한 번에 여러 개가 열리는 경우
        /// (오래 꺼뒀다 켜서 여러 레벨이 한꺼번에 오를 때)를 위해 개수도 함께 알린다.
        /// 레벨업은 몇 시간에 한 번 있는 사건이라 이 경로의 문자열 할당은 상시 비용이 아니다.</summary>
        private string DescribeNewUnlocks(int levelsGained)
        {
            int before = CharacterProgressionModel.Level - levelsGained;
            string firstName = null;
            int count = 0;

            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (!entry.RequiredLevel.HasValue) continue;   // 행동은 잠기지 않는다.

                // ★ 은퇴한 것(2026-09-06 [머리] 카테고리 · 이펙트 「없음」)은 정보창에 고를 자리가
                //   없다. 그래도 알리면 "정보창에서 착용할 수 있습니다"라는 이 문장의 뒷부분이
                //   <b>거짓</b>이 된다 — 없는 화면으로 사용자를 보내는 형태다(원칙 1의 "없는 것을
                //   주장하지 않는다"). 술어는 목록 판정 하나만 쓴다(ItemCatalog.IsListed).
                if (!ItemCatalog.IsListed(entry)) continue;

                int need = entry.RequiredLevel.Value;
                if (need <= before || need > CharacterProgressionModel.Level) continue;

                count++;
                if (firstName == null) firstName = entry.DisplayName;
            }

            if (count <= 0) return "새로 열린 장비는 없습니다.";

            string more = count > 1 ? $" 외 {count - 1}종" : string.Empty;
            return $"새 장비 해제: [{firstName}]{more} — 정보창({ShortcutLabel.Chord("I")} 또는 우상단 톱니)에서 " +
                   "착용할 수 있습니다.";
        }

        /// <summary>
        /// ★ 육안 검증 전용 진입점(리더 지시: "레벨을 테스트용으로 임시로 올려서 확인해라").
        /// 정상 게임플레이 경로에서는 호출되지 않는다 — 정보창/단축키/우클릭 메뉴 어디에도 이 메서드로
        /// 가는 길이 없고, 아래 <c>StickConfig.verboseDiagnosticsLogging</c>이 켜져 있을 때만 동작한다.
        /// (검증값을 원복하지 않아 사고가 난 전례가 이 프로젝트에 2번 있어, 아예 "일시적으로만 켜지는"
        ///  형태로 만들어 원복 대상 자체를 없앴다 — 진단 로그를 끄면 이 경로도 함께 닫힌다.)
        /// </summary>
        public void GrantDebugXpForVisualCheck(float amount)
        {
            if (_config == null || !_config.verboseDiagnosticsLogging)
            {
                Debug.LogWarning($"[성장] 검증용 XP 지급은 진단 로그({ShortcutLabel.Chord("D")})가 " +
                    "켜져 있을 때만 동작합니다.");
                return;
            }
            Grant(amount, "검증용 임시 지급");
        }
    }
}
