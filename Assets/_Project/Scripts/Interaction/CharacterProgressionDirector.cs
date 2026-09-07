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
    ///                    ★ 2026-09-06 — <b>같은 훅이 동전 20도 낸다</b>(아래 「2차」 절). 관문 셋을
    ///                    공유하므로 «XP는 들어왔는데 동전은 안 들어왔다»가 구조적으로 불가능하다.
    ///                    ★★ 2026-09-07 보안 결함 수정(design-systems 발견, §3-3) — <b>반대 방향도
    ///                    막았다</b>. 옛 코드는 동전이 쿨다운(600초)·일일 상한(72회)에 막혀도 XP는
    ///                    <b>무조건</b> 나갔다 — 연속 도배 시 시간당 ~6,478XP(패시브의 72배)로 Lv50
    ///                    전체 요구량을 22.4시간에 채우는 익스플로잇이었다. 지금은
    ///                    <see cref="AwardArcheryCoins"/>가 돌려주는 <c>coinsAwarded</c>(동전이 이미
    ///                    통과한 쿨다운·일일상한 판정 결과)가 0이면 XP도 지급하지 않는다 — XP 전용
    ///                    쿨다운을 새로 만들지 않고 동전 쪽 판정을 그대로 재사용한다.
    ///
    /// ★ 2026-09-02 — 보너스 소스가 <b>2종에서 1종</b>이 됐다(격파 승리 +25XP 삭제, 격파 놀이 기능
    ///   제거). 패시브가 주 경로라는 설계 덕에 성장 속도에 미치는 영향은 사실상 없다 —
    ///   위 XP 곡선 표(CharacterProgressionModel)는 애초에 패시브만으로 계산된 값이다.
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
    /// ★ 2026-09-06 (2차) — <b>첫 실행 시드</b>와 <b>활쏘기 상금</b>도 여기서 흐른다
    /// ============================================================================
    /// <c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §17-11의 착수 순서 2·3번이 지목한 자리가 둘 다
    /// 이 파일이다. 새 컴포넌트를 만들지 않은 이유는 롤오버와 <b>같다</b> —
    /// 시드는 «로드 직후 1회»라 <see cref="Start"/>의 <c>CharacterSaveStore.Load()</c> 바로 뒤가
    /// 유일하게 옳은 자리이고, 활쏘기는 <b>이미 있는 명중 훅</b>(<see cref="OnArcheryShotChanged"/>)에
    /// 얹는 것이 «명중 1회 = 판정 1회»를 한 이음매로 유지하는 유일한 방법이다.
    /// <list type="bullet">
    ///  <item><b>시드</b> — <see cref="TryGrantSeedCoinsOnce"/>. 1회 보장은 이 파일이 아니라
    ///    세이브 필드 <c>seedGranted</c>(v10, 이미 존재)와 <c>CurrencyRules.CanGrantSeed</c>가 한다.
    ///    <b>이 파일에는 「받았는가」를 판정하는 코드가 한 줄도 없다</b> — 같은 사실을 두 곳에서
    ///    계산하지 않는다.</item>
    ///  <item><b>활쏘기</b> — <see cref="AwardArcheryCoins"/>. 쿨다운(단조 시계 600초)과 일일 총량은
    ///    <c>CurrencyModel.TryAwardArcheryCoins</c> 안에만 있다. 이 파일은 <b>언제 물어볼지</b>만 안다.</item>
    ///  <item><b>유휴 수급</b>(2026-09-06 3차) — <see cref="AccrueIdleIncome"/>. <see cref="Update"/>의
    ///    롤오버 <b>바로 아래</b>. 이 배선의 본체는 지급이 아니라 <b>기산점을 조건 없이 전진시키는 것</b>이다 —
    ///    집중 세션 동안 멈춰 두면 세션이 끝난 첫 틱이 그 25분을 유휴로 <b>한 번 더</b> 지급한다(I-7′ 파손).</item>
    /// </list>
    /// <para>★ <b>지급액·쿨다운·상한 숫자는 이 파일에 한 개도 없다</b>(<c>CurrencyRules</c> 전용).
    /// 여기에 20이나 1200을 적으면 그 순간 같은 사실이 두 곳에 살게 된다.</para>
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

        /// <summary>같은 GameObject의 집중 모드 감시자. <b>«집중 세션 중인가»의 유일한 출처</b>다 —
        /// 유휴 수급이 그 사실을 따로 계산하면(그림자 상태) 두 값이 갈라지는 날 같은 1초가 두 번
        /// 지급된다(I-7′). 없으면 «세션이 존재할 수 없는 조립»이므로 유휴로 본다.</summary>
        private FocusWatchDirector _focusWatch;

        /// <summary>유휴 수급의 단조 기산점. <see cref="double.NaN"/>은 «아직 한 틱도 안 돌았다»이고,
        /// 그 첫 틱은 <b>기산점만 잡고 지급하지 않는다</b>(앱 시작~첫 프레임 사이를 지급하지 않기 위해).</summary>
        private double _idleTickMonotonic = double.NaN;

        /// <summary>직전 유휴 요약 로그의 단조 시각.</summary>
        private double _idleLogMonotonic = double.NaN;

        /// <summary>직전 요약 이후 유휴로 들어온 동전(요약 한 줄에 실어 보내고 0으로 되돌린다).</summary>
        private int _idleCoinsSinceLog;

        /// <summary>«유휴 수급이 멈췄다»를 이미 알렸는가. <b>창이 다시 갉히기 시작하면</b> 내려간다 —
        /// 그래야 다음 정지가 <b>새 사건</b>으로 한 번 더 보고된다.
        /// <para>★ 2026-09-06 정정 — 원래는 «동전이 1개라도 들어오면» 내렸다. 그 기준이
        /// <see cref="LogIdleStallOnce"/>의 오판과 짝을 이뤄 <b>5초에 한 줄</b>을 만들었다
        /// (0동전 프레임에서 찍고 → 5초 뒤 동전 1개에 플래그가 풀리고 → 다음 프레임에 또 찍는다).
        /// 이제 올리는 조건과 내리는 조건이 <b>같은 사실 하나</b>(창이 갉혔는가)를 본다.</para></summary>
        private bool _idleStallLogged;

        /// <summary>유휴 수급 요약 로그의 최소 간격(초). 동전은 5초에 1개꼴로 들어오므로 지급마다
        /// 찍으면 하루 1만 줄이 넘는다 — 24시간 상주 앱에서 그건 로그가 아니라 소음이다.</summary>
        private const double IdleIncomeLogIntervalSeconds = 1800.0;

        // ====================================================================
        // ★ 정지 로그의 식별 표지 — 테스트가 문장을 <b>베끼지 않고</b> 참조한다
        // ====================================================================
        // 문장을 테스트에 하드코딩하면 문구를 다듬는 라운드마다 «부재 단언»이 조용히 초록이 된다
        // (CLAUDE.md — 부재 단언용 니들이 썩으면 아무도 모른다). 여기 상수로 두면 이름이 바뀌는
        // 순간 테스트가 컴파일되지 않는다.

        /// <summary>정지 로그 한 줄의 머리말. 이 문자열이 로그에 있으면 «멈췄다»를 알린 것이다.</summary>
        public const string IdleStallLogMarker = "[재화] 유휴 수급이 멈췄습니다";

        /// <summary>정지 사유 ① — 오늘의 일일 상한을 다 채웠다.</summary>
        public const string IdleStallCapPhrase = "오늘 상한";

        /// <summary>정지 사유 ② — 8시간 창을 다 썼다. ★ 실제로는 <b>거의 도달할 수 없는</b> 사유다
        /// (<c>CurrencyRules.WindowToCeilingRatio</c> = 2.3배 — 창이 하루 절대 천장의 2배가 넘게
        /// 설계돼 있어 상한이 <b>먼저</b> 걸린다). 옛 구현은 이 사유를 <b>기본 분기</b>로 적었고,
        /// 그래서 상한 정지에 «480분을 다 썼다»는 거짓 문장이 붙어 나갔다.</summary>
        public const string IdleStallWindowPhrase = "지급 가능 시간";

        /// <summary>정지 사유 ③ — 위 둘 다 아니다. 여기에 오면 <b>우리가 모르는 정지</b>이므로
        /// 아는 척하지 않고 숫자를 그대로 늘어놓는다(두 사유를 동시에 주장하지 않는다).</summary>
        public const string IdleStallUnknownPhrase = "원인을 특정하지 못했습니다";

        /// <summary>
        /// ★ <b>재화 일일 리셋의 유일한 구동자</b>(2026-09-06 배선). 상한 리셋 · 무료 회복제 부활 ·
        /// 8시간 창 리셋 · [오늘 할일] · 활쏘기 카운터를 되돌리는 코드는
        /// <c>Core/CurrencyModel.TickDayRollover</c> 하나뿐인데(불변식 I-15′)
        /// <b>그것을 부르는 프로덕션 코드가 0건이었다</b>.
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
        //    ⇒ 영향: XP 보너스가 그 경우 누락되고, 동전은 어차피 600초 쿨다운이라 대부분 지연에 그친다.
        //    ⇒ <b>이번 라운드에서 고치지 않았다</b> — XP 거동을 바꾸는 변경이고, 먼저 빨간 테스트를
        //      세워야 한다(CLAUDE.md). 고칠 때는 형제 파일의 형태를 그대로 가져오면 된다.
        private int _lastRewardedShotIndex = -1;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 복제본에 이 컴포넌트가 남아 있어도
            // XP가 두 배로 들어가지 않게 하는 2차 방어(1차 방어는 SceneBootstrapper의 제거).
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;

            // ★ 유휴 수급이 «집중 세션 중인가»를 물어볼 상대. 같은 프리팹 루트에 함께 붙는다
            //   (Assets/Editor/SceneBootstrapper.cs가 둘 다 root에 AddComponent한다).
            //   FindFirstObjectByType을 쓰지 않는 이유는 위 _agent와 같다 — 복제본이 남의 세션을
            //   읽으면 그 복제본만 유휴 수급이 멈춘다.
            _focusWatch = GetComponent<FocusWatchDirector>();
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
            //    수급 배선이 그 창에서 「오늘 이미 다 받았다」를 보게 된다. ★ 2026-09-06 현재 그
            //    말이 실제로 걸리는 채널이 생겼다: 활쏘기 상금(archeryCoinsToday)은 <b>배선됐고</b>,
            //    유휴·[오늘 할일]은 아직이다. 즉 이 줄은 이제 가정이 아니라 <b>실효 방어</b>다.
            if (_dayRollover.CheckNow(Time.realtimeSinceStartupAsDouble)) LogDayRollover("실행 직후");

            // ★★ 첫 실행 시드 — 반드시 위 CharacterSaveStore.Load() <b>뒤</b>다(롤오버와 같은 함정).
            //    앞에서 부르면 RestoreFromSave가 디스크의 seedGranted(=false)와 coinBalance를 그대로
            //    덮어써 <b>지급이 통째로 사라진다</b>. 그리고 그 실패는 예외도 로그도 남기지 않고
            //    <b>다음 실행에서도 똑같이</b> 사라져, 사용자는 시드를 영영 못 받는다.
            //    롤오버 「아래」에 둔 것은 이 파일의 규약 그대로다(수급 배선은 롤오버 뒤).
            //    ※ 시드는 일일 카운터를 건드리지 않으므로 롤오버와 순서 의존이 실제로는 없다 —
            //      그래도 규약을 지켜 «수급은 항상 롤오버 뒤»를 한 줄도 예외 없이 유지한다.
            TryGrantSeedCoinsOnce();

            Debug.Log($"[성장] 준비 완료 — {CharacterProgressionModel.CharacterName} Lv.{CharacterProgressionModel.Level} " +
                $"({CharacterProgressionModel.CurrentXp:F0}/{CharacterProgressionModel.XpToNextLevel(_config):F0} XP). " +
                $"저장 파일={(CharacterSaveStore.LoadedFromFile ? "불러옴" : "없음 — 새 캐릭터로 시작")} " +
                $"({CharacterSaveStore.FilePath}). " +
                $"패시브 {(_config != null ? _config.progressionPassiveXpPerMinute : 0f):F1}XP/분.");

            // ★★ 조립 사고를 <b>조용하지 않게</b> 만든다. 이 참조가 null이면 유휴 수급이 집중 세션
            //    중에도 계속 돌아 «같은 1초가 두 번» 지급된다(I-7′). 그런데 그 상태는 화면에서
            //    <b>정상보다 오히려 후해 보여서</b> 아무도 신고하지 않는다 — 이 저장소가 반복해 당한
            //    «고장과 정상이 똑같이 생겼다»의 최악 형태다. 프리팹에서 컴포넌트가 빠지거나
            //    다른 GameObject로 옮겨 가면 여기서 한 줄이 뜬다.
            if (_focusWatch == null)
            {
                Debug.LogWarning("[재화] 같은 GameObject에서 " + nameof(FocusWatchDirector) + "를 찾지 못했습니다 — " +
                    "유휴 수급이 <b>집중 세션 중에도</b> 계속 적립됩니다(같은 1초가 두 번 지급, I-7′). " +
                    "프리팹 루트에 그 컴포넌트가 붙어 있는지 확인하세요" +
                    "(Assets/Editor/SceneBootstrapper.cs가 둘 다 root에 붙입니다).");
            }
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
            // ★ <b>수급 배선을 이 Update에 넣는다면 반드시 이 줄 「아래」다.</b> 위에 넣으면 자정 직후
            //   한 틱이 어제 카운터를 보고, 그 한 틱은 상한에 걸려 조용히 0동전을 준다 — 화면에도
            //   로그에도 아무 흔적이 없다. ★ 2026-09-06 현재 그 규약을 지키는 것이 바로 아래
            //   AccrueIdleIncome() 한 줄이다(활쏘기는 이벤트 훅, [오늘 할일]은 Core/TodoListModel).
            //
            // ★ 일시정지(전체화면 게임 감지) 상태에서도 <b>멈추지 않는다</b>. 하루가 넘어간 것은
            //   달력의 사실이지 우리 연출 상태가 아니고, 저녁 내내 게임한 사용자만 리셋을 못 받는
            //   결과가 된다. FocusWatchDirector가 IsSuspended에서 반환하는 것과 성격이 다르다.
            //
            // 비용: 대부분의 프레임에서 double 뺄셈 1회(주기 게이트). 달력을 실제로 읽는 것은
            //   CurrencyDayRolloverTicker.CheckIntervalSeconds마다 한 번뿐이다.
            if (_dayRollover.TickIfDue(Time.realtimeSinceStartupAsDouble)) LogDayRollover("가동 중 날짜 경계 통과");

            // ★★ 유휴 수급 — <b>반드시 위 롤오버 줄 아래</b>다(바로 위 문단이 요구한 자리).
            //    자정 직후 한 틱이 어제 카운터를 보면 상한에 걸려 조용히 0동전을 준다.
            AccrueIdleIncome();

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
        // ★★ 유휴 수급 (2026-09-06 3차 배선) — I-7′가 「구조로」 참인 자리
        // ====================================================================

        /// <summary>
        /// 유휴 수급 한 틱. ★ <b>이 메서드의 핵심은 지급이 아니라 기산점 전진이다.</b>
        ///
        /// <para><b>왜 <see cref="_idleTickMonotonic"/>을 조건 없이 매 프레임 전진시키는가.</b>
        /// 집중 세션 중에 기산점을 멈춰 두면, 세션이 끝난 <b>첫 틱의 델타에 세션 전체 길이가 실린다</b> —
        /// 25분 세션을 완주한 사용자가 완주 보상(600)을 받고 <b>그 25분을 유휴로 한 번 더</b> 받는다.
        /// 그게 정확히 불변식 I-7′(«같은 1초가 두 번 지급되지 않는다»)의 파손이고,
        /// <b>테스트로 잡기 전에 코드 모양으로 막아야 하는 종류</b>다. 그래서 전진은 무조건,
        /// 분기는 <c>isIdleEarning</c> 하나로만 한다(<c>CurrencyRules.IdleTick</c>이 false면
        /// 창도 안 갉고 0을 돌려준다 — 그 초는 <b>버려지는 것이 맞다</b>).</para>
        ///
        /// <para>★ <b>«집중 중인가»를 이 파일이 따로 계산하지 않는다.</b> 같은 GameObject의
        /// <see cref="FocusWatchDirector.IsSessionActive"/>를 그대로 읽는다 — 그림자 상태를 만들지
        /// 말라는 것이 <c>CurrencyModel.TickIdleIncome</c> 문서의 명시적 요구다. 컴포넌트가 없으면
        /// «세션이 존재할 수 없는 조립»이므로 유휴로 본다(그 방향이 안전한 쪽이다).</para>
        ///
        /// <para>★ <b>일시정지(전체화면 게임 감지) 중에도 멈추지 않는다</b> —
        /// <c>DESIGN_SYSTEMS_STATS §16-8 U-33</c>의 권고("온라인으로 치는 것을 권고 — 아니라고 하면
        /// 게임할 때만 손해 보는 벌칙이 된다")를 그대로 따랐다. ⚠ <b>리더 확정 대기 항목</b>이고,
        /// 뒤집으려면 이 메서드에 <c>IsSuspended</c> 분기 한 줄을 더하면 된다.</para>
        ///
        /// <para>★ 시간 입력은 <b>단조 시계 두 시점의 차</b>다. <c>Time.deltaTime</c> 누적을 쓰면
        /// ① 엔진 상한 때문에 조용히 적게 쌓이고 ② 기계가 잠든 시간을 통째로 잃는다(T-3-c).</para>
        ///
        /// <para>★★ <b>«멈췄다»의 기준은 동전이 아니라 창이다</b>(2026-09-06 수정). 요율이
        /// <c>IdleCoinsPerMinute</c>(분당 12 = 초당 0.2)라 <b>정상 상태에서도 프레임의 대부분이
        /// 0동전</b>이고 — 소수분은 <c>CarryCoins</c>로 다음 틱에 넘어간다 — 그 0을 정지로 읽던
        /// 옛 코드는 <b>정상 동작을 5초에 한 번씩 고장으로 신고</b>했다.
        /// <b>실측</b>(<c>CurrencyRules.IdleTick</c>을 그대로 컴파일해 60fps 1시간을 돌린 결과):
        /// 옛 기준 <b>720줄/시간</b>, 새 기준 <b>0줄</b>. 하루로는 동전 1개당 한 줄이라 상한(1,500)에
        /// 걸릴 때까지 약 1,500줄이고, 회복제 2개면 약 2,500줄이다.
        /// 그래서 분기는 지급액이 아니라 <c>windowSecondsSpent</c>를 본다: 그 값은 «지급이 실제로
        /// 일어난 초»에만 값이 있으므로(T-15-1-a) 0동전과 0초가 <b>다른 사실</b>이 된다.</para>
        /// </summary>
        private void AccrueIdleIncome()
        {
            double nowMonotonic = Time.realtimeSinceStartupAsDouble;
            double previousMonotonic = _idleTickMonotonic;
            _idleTickMonotonic = nowMonotonic;                  // ★ 조건 없이 전진(위 문단)
            if (double.IsNaN(previousMonotonic)) return;        // 첫 틱 — 기산점만 잡고 지급은 없다

            double elapsedSeconds = nowMonotonic - previousMonotonic;
            bool isIdleEarning = _focusWatch == null || !_focusWatch.IsSessionActive;
            int coins = CurrencyModel.TickIdleIncome(elapsedSeconds, isIdleEarning,
                out double windowSecondsSpent);

            if (coins > 0)
            {
                _idleCoinsSinceLog += coins;
                LogIdleIncomeIfDue(nowMonotonic);
            }

            if (!isIdleEarning) return;        // 집중 세션 중 — 0원이 정상이고 알릴 것이 없다.

            // 같은 단조 시각이 두 번 읽히면(또는 시계가 역행하면) 이 틱은 <b>수급에 대해 아무것도
            // 말하지 않는다</b> — 창이 안 갉힌 것은 정지가 아니라 «잰 시간이 없다»는 뜻이다.
            if (!(elapsedSeconds > 0.0)) return;

            // ★ 창이 갉혔다 = 수급은 살아 있다. 이번 틱이 0동전이어도 그건 요율의 결과일 뿐이다.
            //   여기서 플래그를 내리므로 «다음 정지»는 새 사건으로 다시 한 번 보고된다.
            if (windowSecondsSpent > 0.0) { _idleStallLogged = false; return; }

            LogIdleStallOnce();
        }

        /// <summary>
        /// 유휴 수급이 <b>멈춘 이유</b>를 딱 한 번 알린다.
        /// <para>★ <b>이 로그가 없으면 이 기능은 관측할 수 없다.</b> 상한/창에 걸린 상태는 화면에서
        /// «고장»과 똑같이 생겼다 — 앱은 그대로 떠 있고 동전만 안 는다. 이 저장소는 같은 형태의
        /// 오진을 반복해서 받았다. 대신 <b>매 프레임 찍지 않는다</b>(24시간 상주 앱).</para>
        ///
        /// <para>★★ <b>사유를 추측하지 않는다</b>(2026-09-06 수정). 옛 구현은 «상한이 아니면 창»이라는
        /// 2분기였는데, 하필 <b>창은 거의 도달할 수 없는 쪽</b>이다
        /// (<c>CurrencyRules.WindowToCeilingRatio</c> = 480분×12 ÷ 2,500 = 2.3배 — 상한이 항상
        /// 먼저 걸리도록 설계돼 있다). 그래서 그 기본 분기는 사실상 <b>거짓 문장 전용</b>이었고,
        /// 실제 로그에 «8시간 창을 0으로 리셋했다»와 «480분을 다 썼다»가 11줄 간격으로 함께 찍혔다.
        /// 지금은 두 사실을 <b>각각</b> 확인하고, 둘 다 아니면 그렇다고 적는다.</para>
        /// </summary>
        private void LogIdleStallOnce()
        {
            if (_idleStallLogged) return;
            _idleStallLogged = true;

            bool capReached = CurrencyModel.RemainingDailyRoomCoins() <= 0;
            bool windowExhausted = CurrencyModel.RemainingIdleWindowSeconds() <= 0.0;

            string capLine =
                $"{IdleStallCapPhrase}({CurrencyModel.DailyCapCoins()}동전)을 다 채웠습니다" +
                $"(오늘 유휴 {CurrencyModel.TodayGrantedCoins}동전). " +
                $"회복제를 쓰면 상한이 늘고(오늘 {CurrencyModel.PotionsUsedToday}/{CurrencyRules.MaxPotionsPerDay}개), " +
                "집중 모드 지급은 이 상한 <b>밖</b>이라 계속 들어옵니다.";

            string windowLine =
                $"오늘의 {IdleStallWindowPhrase}({CurrencyRules.IdleWindowCapMinutes}분)을 다 썼습니다. " +
                "앱이 켜져 있던 시간이 아니라 «동전이 실제로 나온 시간»만 세는 창입니다.";

            string why;
            if (capReached && windowExhausted)
            {
                // 설계상 거의 나올 수 없는 조합이다 — 나왔다면 그 사실 자체가 보고할 값어치가 있다.
                why = capLine + " 그리고 " + windowLine;
            }
            else if (capReached)
            {
                why = capLine;
            }
            else if (windowExhausted)
            {
                why = windowLine;
            }
            else
            {
                why = $"{IdleStallUnknownPhrase} — 상한도 창도 남아 있는데 창이 갉히지 않았습니다" +
                      $"(오늘 유휴 {CurrencyModel.TodayGrantedCoins}/{CurrencyModel.DailyCapCoins()}동전, " +
                      $"남은 창 {CurrencyModel.RemainingIdleWindowSeconds() / 60.0:F0}분). " +
                      "이건 «의도된 천장»이 아니라 우리가 모르는 상태입니다 — 이 줄이 보이면 " +
                      "CurrencyRules.IdleTick의 관문과 이 호출부의 인자를 함께 보십시오.";
            }

            Debug.Log($"{IdleStallLogMarker} — {why} " +
                $"잔액 {CurrencyModel.CoinBalance}동전. 날짜가 바뀌면 상한과 창이 함께 다시 열립니다.");
        }

        /// <summary>주기 요약. 동전은 5초에 1개꼴로 들어오므로 <b>한 번씩 찍으면 안 된다</b> —
        /// 하루 17,000줄이 된다. 대신 첫 지급과 <see cref="IdleIncomeLogIntervalSeconds"/>마다 누계만 남긴다.</summary>
        private void LogIdleIncomeIfDue(double nowMonotonic)
        {
            if (!double.IsNaN(_idleLogMonotonic)
                && nowMonotonic - _idleLogMonotonic < IdleIncomeLogIntervalSeconds)
            {
                return;
            }

            _idleLogMonotonic = nowMonotonic;
            // ★ 「남은 창」의 뺄셈을 여기서 다시 하지 않는다 — 클램프를 빠뜨린 사본이 하나 생기는
            //   순간 같은 사실을 두 곳이 다르게 말하게 된다(위 정지 로그도 같은 함수를 부른다).
            Debug.Log($"[재화] 유휴 수급 +{_idleCoinsSinceLog}동전(직전 요약 이후) — " +
                $"잔액 {CurrencyModel.CoinBalance}동전, 오늘 유휴 {CurrencyModel.TodayGrantedCoins}/" +
                $"{CurrencyModel.DailyCapCoins()}, 남은 창 " +
                $"{CurrencyModel.RemainingIdleWindowSeconds() / 60.0:F0}분. " +
                "저장은 다음 주기/종료 저장에 실립니다.");
            _idleCoinsSinceLog = 0;
        }

        /// <summary>같은 파일에 실리는 모델 중 하나라도 바뀌었는가 — 안 바뀌었으면 디스크를 두드리지
        /// 않는다(하루 종일 켜져 있는 앱이다).</summary>
        private static bool IsAnythingDirty()
            => CharacterProgressionModel.IsDirty || CharacterStatsModel.IsDirty || UiLayoutModel.IsDirty
               || TodoListModel.IsDirty    // v4 — 사용자가 적은 할일은 반드시 남아야 한다.
               || CharacterAppearanceModel.IsDirty    // v7 — 잉크색(우클릭 메뉴/단축키 경로는 즉시 저장을 부르지 않는다).
               || AppSettingsModel.IsDirty            // v8 — 설정창(슬라이더는 드래그 중 즉시 저장을 부르지 않는다).
               // ★ v10 — 동전·구매 이력·등급 해금·장착한 춤. 이 한 줄이 빠지면 유휴 수급이 60초 주기
               //   저장에도 종료 시 저장에도 실리지 않고, 사용자는 하루 종일 번 동전을 통째로 잃는다.
               //   (그리고 그 실패는 초록 테스트와 똑같이 생겼다 — 즉시 저장 경로만 보는 테스트는 통과한다.)
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
            Debug.Log($"[재화] 날짜 롤오버({why}) — 일자 #{CurrencyModel.DayIndex}. " +
                $"오늘 상한 {CurrencyModel.DailyCapCoins()}동전이 다시 열렸고" +
                $"(잔여 {CurrencyModel.RemainingDailyRoomCoins()}), 8시간 창·회복제·[오늘 할일]·활쏘기 " +
                "카운터가 함께 0으로 돌아갔습니다. " +
                $"지갑({CurrencyModel.CoinBalance}동전)은 건드리지 않습니다 — 리셋되는 것은 " +
                "「오늘의 예산」이지 「지갑」이 아닙니다. " +
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

            // ★★ 2026-09-06 — 동전이 <b>이 한 이음매</b>에서 나간다. 위 세 관문(정중앙 · Release ·
            //    같은 발 방어)을 XP와 <b>그대로 공유</b>하는 것이 요점이다. 별도 구독을 새로 만들면
            //    「명중 1회」의 정의가 두 벌이 되고, 둘이 갈라지는 날 «XP는 들어왔는데 동전은
            //    안 들어왔다»(또는 그 반대)가 된다 — 재현도 설명도 불가능한 형태다.
            //
            // ★★★ 2026-09-07 보안 결함 수정(design-systems 발견, §3-3) — <b>XP도 이 지급의 성패에
            //    묶는다</b>. 옛 코드는 위 세 관문만 지나면 XP를 <b>무조건</b> 지급했다 — 동전에는
            //    이미 있는 쿨다운(단조 600초)·일일 상한(72회)이 XP에는 없어서, 연속 도배 시 시간당
            //    ~6,478XP(패시브의 72배)로 Lv50 전체 요구량을 22.4시간 만에 채울 수 있었다
            //    (docs/DESIGN_SYSTEMS_LEVEL_STAT_GROWTH_PROPOSAL.md §3-3). <b>새 쿨다운/카운터를
            //    XP 전용으로 만들지 않는다</b> — <c>CurrencyModel.TryAwardArcheryCoins</c>가 이미
            //    계산한 판정(쿨다운 통과 + 오늘 상한 이내)을 <c>coinsAwarded &gt; 0</c>으로 그대로
            //    재사용한다. 코인과 XP가 같은 사용자 행동(정중앙 1회)을 보상하므로 같은 관문을
            //    공유하는 것이 자연스럽고, 판정처가 하나면 「코인은 막혔는데 XP는 새는」 갈라짐이
            //    구조적으로 불가능해진다.
            int coinsAwarded = AwardArcheryCoins();
            if (coinsAwarded > 0)
            {
                Grant(_config != null ? _config.progressionBullseyeXp : 0f, "활쏘기 정중앙 명중");
            }
        }

        /// <summary>
        /// 활쏘기 정중앙 <b>1회당</b> 상금. ★ 세션 완료당이 아니다 —
        /// <c>DESIGN_SYSTEMS_STATS</c> §13-3 표가 <i>"활쏘기 정중앙 1회 · 20동전 · 쿨다운 600초"</i>이고,
        /// 그래서 이 호출은 <b>명중 판정이 확정되는 그 자리</b>(위 훅)에 붙는다.
        ///
        /// <para>★ <b>지급 여부를 이 파일이 정하지 않는다.</b> 쿨다운(단조 시계 600초)도 일일 총량도
        /// <c>CurrencyModel.TryAwardArcheryCoins</c> 안에만 있다. 여기서 «쿨다운이 지났는지»를 한 번 더
        /// 계산하면 같은 사실이 두 곳에서 살게 되고, 그 둘은 반드시 갈라진다.</para>
        ///
        /// <para>★ <b>0동전일 때도 로그를 남긴다</b>(<c>FocusWatchDirector.PayCancelCoins</c>와 같은 이유).
        /// 명중 연출은 그대로 도는데 동전만 안 나오는 화면은 <b>고장과 똑같이 생겼다</b>. 실제로 이
        /// 저장소는 «지급이 고장났다»는 오진을 반복해서 받았다. 쿨다운 대기인지 오늘 상한 도달인지를
        /// 구분해 적는 이유도 같다 — 뒤쪽은 <b>내일까지 안 나온다</b>는 다른 사실이다(§20-8).</para>
        ///
        /// <para>★ <b>여기서 저장을 강제하지 않는다.</b> 지급은 <c>CurrencyModel.IsDirty</c>를 세우고
        /// 주기 저장(60초)·종료 저장이 이미 싣는다(<see cref="IsAnythingDirty"/>).
        /// 최악 손실은 1주기 = 20동전이고, 그 대가로 하루 종일 켜 두는 앱이 명중마다 디스크를
        /// 두드리지 않는다 — <c>DESIGN_SYSTEMS_STATS</c> §20-7 저장 빈도표가 이 채널에 대해
        /// <i>"<c>archeryCoinsToday</c> — <c>IsDirty</c>만, 주기 저장에 태운다(최악 1분/20동전 손실)"</i>로
        /// 명시적으로 고른 저울이다.</para>
        ///
        /// <para>★★ <b>반환값은 이제 XP 게이트로도 쓰인다</b>(2026-09-07 보안 결함 수정). 호출부
        /// (<see cref="OnArcheryShotChanged"/>)가 이 값이 0보다 클 때만 XP를 지급한다 — 쿨다운·일일
        /// 상한 판정을 이 함수 안에 <b>한 곳</b>에만 두고 XP가 그 결과를 빌려 쓰는 것이지, XP가
        /// 따로 판정하는 것이 아니다.</para>
        /// </summary>
        /// <returns>실제로 지급된 동전(0이면 쿨다운 중이거나 오늘 상한에 도달 — 이때 XP도 지급하지 않는다).</returns>
        private int AwardArcheryCoins()
        {
            // 단조 시계다. 벽시계(DateTime.Now)를 넣으면 시계를 600초 되감는 것만으로 무한 파밍이
            // 되고(§20-3-b), Tests/EditMode/DailyLimitClampAuditTests가 그 순간 빨개진다.
            int coins = CurrencyModel.TryAwardArcheryCoins(Time.realtimeSinceStartupAsDouble);

            if (coins > 0)
            {
                Debug.Log($"[재화] 활쏘기 정중앙 명중 — +{coins}동전. " +
                    $"잔액 {CurrencyModel.CoinBalance}동전(오늘 활쏘기 누계 {CurrencyModel.ArcheryCoinsToday}). " +
                    "저장은 다음 주기/종료 저장에 실립니다.");
                return coins;
            }

            Debug.Log("[재화] 활쏘기 정중앙 명중 — 0동전(XP도 함께 보류). " +
                (CurrencyModel.ArcheryDailyLimitReached
                    ? $"오늘 활쏘기 상금이 상한({CurrencyModel.ArcheryCoinsToday}동전)에 도달했습니다 — " +
                      "고장이 아니라 의도된 천장이고(§20-3-b), 날짜가 바뀌면 다시 열립니다. " +
                      "집중 모드·[오늘 할일]로는 계속 벌 수 있습니다."
                    : "상금 쿨다운 중입니다 — 고장이 아니라 의도된 간격이고(§20-3-b), " +
                      "쿨다운이 풀린 뒤 첫 정중앙에서 다시 나옵니다. " +
                      "쿨다운은 단조 시계로만 재므로 앱을 껐다 켜면 초기화됩니다(그 상한이 일일 총량입니다)."));
            return 0;
        }

        /// <summary>
        /// ★ 첫 실행 시드 — <b>평생 1회</b>. 이 파일에 «받았는가»를 판정하는 코드는 <b>한 줄도 없다</b>:
        /// 그 사실은 세이브 필드 <c>seedGranted</c>(v10, 이미 존재) 하나에만 있고
        /// <c>CurrencyRules.CanGrantSeed</c>가 그것을 읽는다. 여기서 «신규 유저인가»를 따로 재면
        /// (예: <c>CharacterSaveStore.LoadedFromFile</c>) <b>같은 사실이 두 곳에서 계산</b>되고,
        /// 두 판정이 갈라지는 날 시드가 두 번 나가거나 영영 안 나간다.
        ///
        /// <para>★ <b>「신규 캐릭터만」이 아니라 「전원 평생 1회」가 확정 계약이다</b>
        /// (<c>CurrencyRules.SeedCoins</c> 문서, 리더 승인 2026-09-05 U-42). 금액이 0이던 기간에
        /// <c>CanGrantSeed</c>가 <c>SeedCoins &gt; 0</c>을 요구한 덕에 <b>기존 사용자 전원의 플래그가
        /// 아직 false</b>이고, 그래서 이 배선이 켜지는 순간 그들도 첫 지급 대상이 된다.
        /// 그 방어가 실제로 값을 한 자리라 여기에 다시 적어 둔다 — <b>«신규 파일일 때만»으로 좁히면
        /// 그 설계가 조용히 무효가 된다.</b></para>
        ///
        /// <para>★ <b>저장을 강제하지 않는다.</b> 지급은 <c>IsDirty</c>를 세우므로 주기/종료 저장이
        /// 싣는다. 그 둘을 <b>둘 다 놓쳐도</b>(전원 차단 등) 디스크의 <c>seedGranted</c>가 여전히
        /// false라 <b>다음 실행이 같은 지급을 다시 한다</b> — 스스로 낫는다. 반대 방향(두 번 지급)은
        /// 구조적으로 불가능하다: 플래그와 잔액이 <b>같은 파일에 한 덩어리로</b> 실리므로
        /// «지급은 저장됐는데 플래그는 안 저장된» 상태가 존재할 수 없다.</para>
        /// </summary>
        private static void TryGrantSeedCoinsOnce()
        {
            int coins = CurrencyModel.TryGrantSeedCoins();
            if (coins <= 0) return;   // 이미 받았다 — 정상 경로이므로 조용히 지나간다.

            Debug.Log($"[재화] 첫 실행 시드 +{coins}동전 — 잔액 {CurrencyModel.CoinBalance}동전. " +
                "평생 1회이고, 받았다는 사실은 저장 파일의 seedGranted 한 곳에만 남습니다. " +
                "저장은 다음 주기/종료 저장에 실립니다 — 그 전에 앱이 죽으면 이 지급은 " +
                "«없던 일»이 되고 다음 실행이 다시 지급합니다(두 번 지급되는 방향은 없습니다).");
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
