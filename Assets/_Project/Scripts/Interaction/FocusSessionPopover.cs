using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 부채꼴 [집중 모드] 버튼에서 자라나는 팝오버 — docs/UX_FLOW.md <b>32-5</b> +
    /// docs/UX_WIDGETS.md <b>§R5</b>(뽀모도로 다이얼 재설계 · 시간 자유 입력).
    /// 대기 244×252 / 진행 중 244×252.
    ///
    /// ============================================================================
    /// "즉시 토글"을 택하지 않은 이유
    /// ============================================================================
    /// 18절이 시작 흐름을 "시간 선택"으로 이미 못박았다. 토글 하나면 앱이 사용자 대신 길이를 몰래
    /// 정하는 셈이라 언제 끝날지 알 수 없는 타이머가 된다.
    ///
    /// ============================================================================
    /// ★ 진행 링은 <b>절대 다이얼</b>이다 — 한 바퀴 = 60분 고정 (§R5-1-1 / §R5-1-2)
    /// ============================================================================
    /// 옛 링은 <c>남은초 / 세션초</c>인 <b>상대</b> 링이라 어떤 길이를 골라도 시작이 꽉 찬 원이었다.
    /// 눈금판(<c>0 5 … 55</c>)을 그리는 순간 그 링은 <b>거짓말을 한다</b>: 25분 세션이 시작될 때
    /// 호 끝은 「0」(=한 바퀴)을 가리키는데 가운데 숫자는 <c>25:00</c>을 찍는다(원칙 1 직접 위반).
    /// 그래서 분모를 세션 길이가 아니라 <see cref="DialSpanSeconds"/>로 바꿨다 —
    /// <b>호 끝이 가리키는 눈금 = 남은 분</b>이라는 항등식이 선다.
    ///
    /// <para>이 전환이 성립하는 근거는 이미 저장소에 있었다: 세션 상한이 <b>60분</b>이라
    /// (§R2-2-3) 표현 가능한 모든 세션이 한 바퀴 안에 정확히 닫히고 넘침이 0이다. 그래도 상한을
    /// 넘는 세션이 들어오면(지금 UI로는 도달 불가 — <c>StartFocusSession</c>에 상한 클램프가 없다)
    /// <b>눈금판을 통째로 끄고 상대 링으로</b> 물러난다: 못 나르는 뜻을 나르는 척하지 않는다(§R5-7).</para>
    ///
    /// <para><b>계약(§R5-1-3)</b> — <i>눈금판이 없는 링은 상대다. 눈금판이 있는 링만 절대다.</i>
    /// ★ 2026-09-06부터 이 계약의 <b>적용 대상이 이 다이얼 하나뿐</b>이다: 짝이던 캐릭터 발밑 링
    /// (<c>FocusWatchRenderer</c>, Ø33, 얼굴 없는 상대 링)이 사용자 지시로 <b>삭제</b>됐다.
    /// <b>이제 남은 시간을 말하는 표면은 이 창 하나다</b> — 여기 숫자가 틀리면 대조할 곳이 없다.</para>
    ///
    /// ============================================================================
    /// ★ 필수 계약: <see cref="FocusWatchDirector.ForceTriggerNow"/>를 부르지 않는다
    /// ============================================================================
    /// 그 메서드는 <b>90초 고정 데모</b>다. "25분"을 고른 사용자에게 90초짜리 세션을 주면 그 순간
    /// 화면의 숫자가 거짓이 된다(CLAUDE.md 원칙 1 직접 위반). 여기서는 언제나
    /// <see cref="FocusWatchDirector.StartFocusSession"/> / <see cref="FocusWatchDirector.StopFocusSession"/>만
    /// 부른다. 단축키 ⌃⌥⌘F / 우클릭 메뉴는 예전 데모 경로를 그대로 둔다.
    ///
    /// <b>상태 라인은 지어내지 않는다</b>: 실제 값(<see cref="StickmanAgent.IsSuspended"/> /
    /// <see cref="StickmanAgent.IsUserHidden"/>)에서만 파생한다. 특히 일시정지 행은 연출이 아니라
    /// <b>사실 보고</b>다 — 전체화면 앱을 쓰는 동안 <c>RemainingSeconds</c>가 실제로 줄지 않기 때문에,
    /// 설명이 없으면 "타이머가 고장 났다"로 읽힌다.
    ///
    /// ============================================================================
    /// ★★★ 2026-09-06 사용자 지시 — «집중모드에서 지켜보기 기능 삭제해줘»
    /// ============================================================================
    /// 대기 페이지에서 <b>「지켜보기」 토글 스위치</b>(38×20)와 <b>「민감도」 칩 3개</b>(관대/보통/예민)가
    /// 삭제됐다. 민감도가 함께 사라진 이유: 그 칩이 조절하던 값은 <b>오직</b> 딴짓 감지의 에스컬레이션
    /// 임계 배율뿐이라, 감지가 사라진 뒤에는 <b>눌러도 아무 일도 안 일어나는 컨트롤</b>이 된다
    /// (화면이 거짓말을 하느니 없애는 쪽 — 이 파일의 닫기 힌트 삭제와 같은 판단).
    /// 진행 페이지의 상태줄에서도 «지켜보는 중 · …» 4문과 «순수 타이머로만 재고 있어요» 1문이 빠졌다.
    /// 대기 페이지 세로가 <b>252 → 188pt</b>로 줄었다(아래 IdleHeight 검산 참고).
    /// </summary>
    public sealed class FocusSessionPopover : PopoverPanel
    {
        // ★ Width / IdleHeight 의 <b>선언 형태(리터럴)</b>를 바꾸지 마라 —
        //   Tests/EditMode/TodoPostItExpansionAuditTests 가 이 두 줄을 소스에서 문자열로 파싱해
        //   포스트잇 펼침 상한을 계산한다(파서 키가 "const float <이름> = "다).
        private const float Width = 244f;
        private const float IdleHeight = 188f;

        /// <summary>진행 페이지 높이. ★ 224 → 252 (§R5-4-1) — 다이얼 Ø128이 들어갈 세로가 필요하다.
        ///
        /// <para>★ <b>2026-09-06 — 두 페이지 높이가 다시 갈라졌다</b>(대기 188 / 진행 252). 「지켜보기」
        /// 토글과 「민감도」 칩이 삭제되면서 대기 페이지가 64pt 짧아졌기 때문이다. 예전에 두 값을 맞춰
        /// 두었던 이유(224 시절)는 <b>Content가 패널 밖으로 삐져나가는 것</b>이었는데 그 조건은 지금
        /// 성립하지 않는다 — 진행 페이지가 쓰는 세로는 42(크롬) + 190(내용) + 16(아래 여백) =
        /// <b>248 ≤ 252</b>라 패널 안에 그대로 들어간다.</para>
        ///
        /// <para><see cref="PopoverPanel.BuildChrome"/>는 Content 사각형을 <b>Awake 시점의 대기 높이로</b>
        /// 잡으므로 지금 Content는 130pt다. 그래도 <b>그림이 달라지지 않는다</b>: 두 페이지는
        /// <c>UiChrome.Stretch</c>로 Content를 채우고, 그 안의 모든 자식은 <c>PlaceTopLeft</c>로
        /// <b>좌상단 기준</b> 배치라 부모의 <b>높이</b>에 좌표가 걸리지 않는다(마스크도 없어 잘리지도
        /// 않는다). Content 높이에 걸리는 것은 <c>Stretch</c>된 자식뿐이고, 진행 페이지에는 그런 자식이
        /// 페이지 자신 말고는 없다.</para></summary>
        private const float RunningHeight = 252f;

        private const float ContentWidth = Width - UiChrome.Space4 * 2f;   // 212.

        // ==================== 시간 선택 (프리셋 3 + 직접) ====================

        private static readonly float[] DurationMinutes = { 15f, 25f, 50f };

        /// <summary>칩 라벨. 프리셋 3개 + <b>[직접]</b> 1개 = 4개다 — <c>DurationMinutes</c>보다
        /// 하나 길다(마지막 칩은 길이가 아니라 <b>모드 전환</b>이다).</summary>
        private static readonly string[] DurationLabels = { "15분", "25분", "50분", "직접" };

        /// <summary>마지막 칩 = 자유 입력 모드로 들어가는 문. 인덱스를 손으로 적지 않는다.</summary>
        private const int CustomChipIndex = 3;

        // ==================== 다이얼 기하 (§R5-2-2) ====================

        /// <summary>
        /// ★ <b>다이얼 한 바퀴가 뜻하는 시간(초)</b> — 60분 고정. 진행 호의 분모가 이 값이지
        /// 세션 길이가 아니다(클래스 문서 "절대 다이얼" 절).
        /// <para><b>public인 이유</b>: 링 채움을 검증하는 테스트가 이 값을 <b>참조</b>해야 한다.
        /// <c>3600</c>을 테스트에 숫자로 베끼면 이 상수가 한 번 바뀔 때 기준과 대상이 갈라진 채
        /// 조용히 초록이 된다(CLAUDE.md 협업 프로토콜 — 상수 하드코딩 금지).</para>
        /// </summary>
        public const float DialSpanSeconds = 3600f;

        private const float SecondsPerMinute = 60f;

        /// <summary>눈금판이 가진 분의 개수 = 짧은 눈금 칸 수. 다이얼 얼굴에서 파생한다.</summary>
        private const int DialMinutes = (int)(DialSpanSeconds / SecondsPerMinute);   // 60.

        /// <summary>분 하나가 도는 각(도). 6°.</summary>
        private const float DegreesPerMinute = 360f / DialMinutes;

        private const float DialDiameter = 128f;
        private const float DialRadius = DialDiameter * 0.5f;

        // 링 Ø74 두께 8 → 반경 29…37 (AddCircle의 diameter는 <b>바깥</b> 지름이고 두께는 안으로 먹는다).
        private const float RingDiameter = 74f;
        private const float RingThickness = 8f;

        private const float NumberRingRadius = 55f;      // 숫자 12개의 중심 반경.
        private const float NumberBoxWidth = 24f;
        private const float NumberBoxHeight = 14f;

        private const float MajorTickMidRadius = 42f;    // r 38…46.
        private const float MajorTickLength = 8f;
        private const float MajorTickThickness = 2f;

        private const float MinorTickMidRadius = 44.25f; // r 42.5…46.
        private const float MinorTickLength = 3.5f;
        private const float MinorTickThickness = 1f;

        /// <summary>시작 표식 r 36…46. 색은 <see cref="UiChrome.TextPrimary"/>다 —
        /// ★ <see cref="UiChrome.Accent"/>와 <see cref="UiChrome.WarmAccent"/>는 <b>RGB가 완전히
        /// 같아서</b>(둘 다 0.784/0.631/0.353) "액센트 계열"로 칠하면 진행 호와 같은 색이 되어
        /// 표식이 사라진다(§R5-2-4 실측).</summary>
        private const float StartMarkerMidRadius = 41f;
        private const float StartMarkerLength = 10f;
        private const float StartMarkerThickness = 2f;

        private const float TimeBoxHeight = 26f;
        private const float RemainLabelOffsetY = -20f;
        private const float RemainLabelHeight = 14f;

        // ==================== 진행 페이지 세로 (§R5-4) ====================
        //  128 + 6 + 16 + 8 + 32 = 190 ≤ Content 194  → 여유 4pt.
        private const float StatusY = -134f;
        private const float StatusHeight = 16f;
        private const float StopY = -158f;
        private const float StopHeight = 32f;

        // ==================== 시간 칩 행 / 직접 입력 행 (§R5-3-2) ====================
        //  프리셋 : 4×47 + 3×8 = 212
        //  직접   : [목록 40] 8 [−5 26] 4 [− 26] 4 [값 44] 4 [+ 26] 4 [+5 26] = 212
        //  ★ 두 행은 <b>같은 y·같은 높이</b>라 세로 증가가 0pt다.
        private const float ChipRowY = -42f;
        private const float ChipRowHeight = 32f;
        private const float PresetChipWidth = 47f;
        private const float PresetChipGap = 8f;
        private const float ListChipWidth = 40f;
        private const float ListChipGap = 8f;     // [목록]은 되돌아가기라 오타격 비용이 커서 갭이 두 배다.
        private const float StepChipWidth = 26f;
        private const float StepGap = 4f;
        private const float CustomValueWidth = 44f;

        // ==================== 대기 페이지 아래쪽 (2026-09-06 재배치) ====================
        //  「지켜보기」 토글과 「민감도」 칩이 삭제되면서 그 아래가 통째로 64pt 올라왔다.
        //  검산: 크롬 42 + 내용 130 + 아래 여백 16 = 188 = IdleHeight.
        //        (내용 130 = |StartButtonY| 96 + StartButtonHeight 34)

        /// <summary>전체화면 감지로 [시작]이 막혀 있을 때만 뜨는 안내 한 줄. 평소에는 꺼져 있고,
        /// 그 자리를 다른 요소가 쓰지 않는다 — 켜졌다 꺼질 때 아래가 들썩이지 않게.</summary>
        private const float SuspendedNoticeY = -84f;

        private const float StartButtonY = -96f;
        private const float StartButtonHeight = 34f;

        /// <summary>
        /// 자유 입력의 하한(분).
        /// <para>★ <b>이 값은 여기서 고른 것이 아니다</b> — <see cref="FocusWatchDirector"/>의
        /// <c>MinimumSessionSeconds</c>(60초)가 정한 값이고, UI가 디렉터보다 좁을 이유가 없어
        /// 그대로 따른다(§R5-3-3). 설계 §R5-6 #15는 그 상수를 <c>public</c>으로 승격해 여기서
        /// <c>MinimumSessionSeconds / 60f</c>로 <b>파생</b>시키라고 요구했지만, 그 파일은 이 라운드에
        /// 다른 담당자가 편집 중이라 손대지 않았다.</para>
        /// <para>그래서 값을 옮겨 적되 <b>갈라지면 시끄럽게 빨개지도록</b> 못박아 둔다:
        /// <c>Tests/EditMode/FocusSessionDurationRangeTests</c>가 디렉터 <b>소스를 읽어</b>
        /// 두 값을 대조한다. 승격이 끝나면 이 상수를 파생식으로 바꾸고 그 대조를 지워라.</para>
        /// </summary>
        public const int MinimumSessionMinutes = 1;

        /// <summary>자유 입력의 상한(분) = <b>다이얼 얼굴 그 자체</b>. 61분은 호가 한 바퀴를 넘어
        /// 숫자판이 거짓이 된다(§R5-3-3).</summary>
        public const int MaximumSessionMinutes = DialMinutes;

        private const int CoarseStepMinutes = 5;
        private const int FineStepMinutes = 1;

        private FocusWatchDirector _director;

        // 대기 페이지.
        private RectTransform _idlePage;
        private RectTransform _presetRow;
        private RectTransform _customRow;
        private readonly Image[] _durationChips = new Image[DurationLabels.Length];
        private readonly Text[] _durationLabels = new Text[DurationLabels.Length];
        private readonly Image[] _durationOutlines = new Image[DurationLabels.Length];
        private Image _listChip;
        private Text _listLabel;
        private Image _coarseDownChip;
        private Text _coarseDownLabel;
        private Image _fineDownChip;
        private Text _fineDownLabel;
        private Image _fineUpChip;
        private Text _fineUpLabel;
        private Image _coarseUpChip;
        private Text _coarseUpLabel;
        private Text _customValueLabel;
        private Image _startSurface;
        private Text _startLabel;
        private Text _suspendedNotice;

        // 진행 페이지.
        private RectTransform _runningPage;
        private RectTransform _dialFace;
        private Image _startMarker;
        private Image _ringFill;
        private Text _timeText;
        private Text _statusText;
        private Image _stopSurface;

        private int _selectedDuration = 1;   // 기본 25분.
        private DurationMode _mode = DurationMode.Preset;
        private int _customMinutes = 25;
        private bool _customTouched;
        private int _shownCustomMinutes = -1;
        private bool _running;
        private int _lastShownSeconds = -1;
        private float _shownMarkerSeconds = -1f;
        private bool _ringDesaturated;

        /// <summary>시간 칩 행이 지금 무엇을 보여 주는가. 이건 <b>UI 상태일 뿐</b>이라
        /// 지급·동작에 영향이 0이다(§R5-3-5).</summary>
        private enum DurationMode { Preset, Custom }

        protected override Vector2 PanelSizePoints => new Vector2(Width, _running ? RunningHeight : IdleHeight);

        protected override string TitleText => "집중 모드";

        /// <summary>지금 고른 세션 길이(분) — 회귀 테스트가 "25분을 골랐는데 90초가 시작됐다"를 잡는다.
        /// <para>★ <b>회귀 계약</b>: 프리셋 모드에서 index 1은 언제나 25f다(팝오버는 프리셋 모드로 열린다).</para></summary>
        public float SelectedMinutes
            => _mode == DurationMode.Custom ? _customMinutes : DurationMinutes[_selectedDuration];

        /// <summary>자유 입력 값(분) — 진단/테스트 창구.</summary>
        public int CustomMinutesForTests => _customMinutes;

        /// <summary>지금 자유 입력(직접) 행을 보여 주고 있는가 — 진단/테스트 창구.</summary>
        public bool ShowingCustomDurationRow => _mode == DurationMode.Custom;

        /// <summary>눈금판(숫자·눈금·시작 표식)이 켜져 있는가 — <b>절대 다이얼로 읽히는 중인가</b>와
        /// 같은 뜻이다(§R5-1-3 계약).</summary>
        public bool DialFaceVisible => _dialFace != null && _dialFace.gameObject.activeSelf;

        /// <summary>진행 중 화면을 보여주고 있는가.</summary>
        public bool ShowingRunningPage => _running;

        /// <summary>지금 표시 중인 상태 라인(테스트/진단 전용).</summary>
        public string StatusLine => _statusText != null ? _statusText.text : string.Empty;

        /// <summary>시간 칩의 화면 사각형(Unity 스크린 픽셀) — 테스트가 실제 클릭 경로로 누른다.</summary>
        public Rect DurationChipScreenRect(int index)
            => index >= 0 && index < _durationChips.Length
                ? ScreenRectOf(_durationChips[index].rectTransform)
                : new Rect();

        /// <summary>자유 입력 행의 버튼 사각형 — 테스트가 실제 클릭 경로로 누른다.</summary>
        public Rect CustomStepScreenRect(int deltaMinutes)
        {
            Image chip = deltaMinutes switch
            {
                -CoarseStepMinutes => _coarseDownChip,
                -FineStepMinutes => _fineDownChip,
                FineStepMinutes => _fineUpChip,
                CoarseStepMinutes => _coarseUpChip,
                _ => null,
            };
            return chip != null ? ScreenRectOf(chip.rectTransform) : new Rect();
        }

        /// <summary>[목록](프리셋으로 되돌아가기) 칩의 화면 사각형.</summary>
        public Rect DurationListChipScreenRect => _listChip != null ? ScreenRectOf(_listChip.rectTransform) : new Rect();

        public Rect StartButtonScreenRect => ScreenRectOf(_startSurface.rectTransform);
        public Rect StopButtonScreenRect => ScreenRectOf(_stopSurface.rectTransform);

        /// <summary>진행 링의 채움 비율(0~1) — 라벨의 mm:ss와 <b>같은 스냅샷</b>에서 나와야 한다.
        /// 분모는 세션 길이가 아니라 <see cref="DialSpanSeconds"/>다(절대 다이얼).</summary>
        public float RingFillAmount => _ringFill != null ? _ringFill.fillAmount : -1f;

        /// <summary>진행 화면에 지금 찍혀 있는 mm:ss 문자열.</summary>
        public string TimeLabel => _timeText != null ? _timeText.text : string.Empty;

        protected override void Start()
        {
            base.Start();
            _director = GetComponent<FocusWatchDirector>();
        }

        // ==================== 내용 만들기 ====================

        protected override void BuildContent(RectTransform content)
        {
            BuildIdlePage(content);
            BuildRunningPage(content);
        }

        private void BuildIdlePage(RectTransform content)
        {
            var pageGo = new GameObject("IdlePage", typeof(RectTransform));
            pageGo.transform.SetParent(content, false);
            _idlePage = pageGo.GetComponent<RectTransform>();
            UiChrome.Stretch(_idlePage);

            Text subtitle = UiChrome.AddText(_idlePage, "Subtitle", UiChrome.FontLabel,
                TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(subtitle.rectTransform, 0f, 0f, ContentWidth, 16f);
            subtitle.text = "정한 시간 동안 옆에 있을게요.";

            Text durationLabel = UiChrome.AddText(_idlePage, "DurationLabel", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(durationLabel.rectTransform, 0f, -24f, ContentWidth, 14f);
            durationLabel.text = "시간";

            BuildPresetRow();
            BuildCustomRow();

            // ★ 2026-09-06 — 여기 있던 구분선 + 「지켜보기」 토글(−94…−114) + 「민감도」 칩 행(−122…−144)이
            //   삭제됐다(사용자 지시). 아래 두 줄은 그 자리만큼 <b>통째로 64pt 올라왔다</b>: 안내문
            //   −148 → −84, [시작] −160 → −96. 빈 자리를 남기지 않는다.
            _suspendedNotice = UiChrome.AddText(_idlePage, "SuspendedNotice", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.WarmAccent);
            UiChrome.PlaceTopLeft(_suspendedNotice.rectTransform, 0f, SuspendedNoticeY, ContentWidth, 12f);
            _suspendedNotice.text = "전체화면 앱을 닫으면 시작할 수 있어요.";
            _suspendedNotice.gameObject.SetActive(false);

            _startSurface = UiChrome.AddSurface(_idlePage, "Start", UiChrome.Accent, UiChrome.RadiusCard);
            UiChrome.PlaceTopLeft(_startSurface.rectTransform, 0f, StartButtonY, ContentWidth, StartButtonHeight);
            // ★ 2026-09-01 글리프 잔차 제거(사용자 신고 "텍스트도 다 번져보임"): 13 -> 14.
            //   Windows 디스플레이 150%(캔버스 배율 1.5)에서 13pt는 19.5px를 요청하고 아틀라스에는
            //   20px로 구워져 0.975배로 리샘플된다 = 획 번짐. 짝수 pt만 잔차가 0이다
            //   (Platform/UiGlyphScalePolicy.cs 참고). 12이 아니라 14로 올린 이유: 이 라벨은
            //   34pt 높이 주 버튼("시작")의 유일한 글자라 줄이면 CTA 위계가 내려간다.
            //   레이아웃 영향 없음 — 폭 212pt 안에 2글자, Stretch + MiddleCenter라 재배치가 없다.
            _startLabel = UiChrome.AddText(_startSurface.rectTransform, "Label", 14,
                TextAnchor.MiddleCenter, UiChrome.OnAccentSolid, bold: true);
            UiChrome.Stretch(_startLabel.rectTransform);
            _startLabel.text = "시작";
            Wire(_startSurface, "start", StartSession);
        }

        /// <summary>프리셋 행 — 15 / 25 / 50 / [직접]. 4×47 + 3×8 = 212.</summary>
        private void BuildPresetRow()
        {
            _presetRow = NewRow("PresetRow");
            for (int i = 0; i < _durationChips.Length; i++)
            {
                int index = i;
                Image chip = UiChrome.AddSurface(_presetRow, "Duration" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                UiChrome.PlaceTopLeft(chip.rectTransform, i * (PresetChipWidth + PresetChipGap), 0f,
                    PresetChipWidth, ChipRowHeight);
                _durationOutlines[i] = UiChrome.AddOutline(chip.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
                Text label = UiChrome.AddText(chip.rectTransform, "Label", UiChrome.FontBody,
                    TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(label.rectTransform);
                label.text = DurationLabels[i];
                _durationChips[i] = chip;
                _durationLabels[i] = label;
                Wire(chip, "duration" + i, () => SelectDurationChip(index));
            }
        }

        /// <summary>
        /// 자유 입력 행 — 2단 스테퍼(§R5-3-1에서 4안 중 유일하게 <b>기존 배선만</b> 쓰는 안).
        /// <para>텍스트 직접 입력은 이 앱에 <c>InputField</c>가 <b>0건</b>이라 IME·포커스·키보드 캡처를
        /// 클릭관통 오버레이 위에 새로 짜야 하고, 드래그 다이얼은 <see cref="PopoverPanel"/>에 포인터
        /// 드래그 배선이 <b>0건</b>이다. ±1만 두면 25→60이 35클릭이라 ±5를 함께 둔다(최악 9클릭).</para>
        /// </summary>
        private void BuildCustomRow()
        {
            _customRow = NewRow("CustomRow");

            float x = 0f;
            _listChip = AddStepChip("List", "목록", x, ListChipWidth, out _listLabel);
            Wire(_listChip, "durationList", ExitCustomMode);
            x += ListChipWidth + ListChipGap;

            _coarseDownChip = AddStepChip("CoarseDown", "−" + CoarseStepMinutes, x, StepChipWidth, out _coarseDownLabel);
            Wire(_coarseDownChip, "durationCoarseDown", () => AdjustCustomMinutes(-CoarseStepMinutes));
            x += StepChipWidth + StepGap;

            _fineDownChip = AddStepChip("FineDown", "−", x, StepChipWidth, out _fineDownLabel);
            Wire(_fineDownChip, "durationFineDown", () => AdjustCustomMinutes(-FineStepMinutes));
            x += StepChipWidth + StepGap;

            // 값 상자는 <b>버튼이 아니다</b> — 배선을 걸지 않는다(누를 곳처럼 보이면 안 눌리는 것이 결함이 된다).
            // ★ 2026-09-06 — 면을 <b>먼저</b> 정하고 테두리를 그 면 위에 합성한다(정책 §5-2).
            //   이 상자는 팝오버 바탕(PanelSurface) 위에 있고, 테두리는 그 상자 면 위에 있다.
            Color valueFace = UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface);
            Image valueSurface = UiChrome.AddSurface(_customRow, "CustomValue", valueFace, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(valueSurface.rectTransform, x, 0f, CustomValueWidth, ChipRowHeight);
            UiChrome.AddOutline(valueSurface.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.AccentBorder, valueFace), UiChrome.RadiusChip);
            _customValueLabel = UiChrome.AddText(valueSurface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextOnAccent, bold: true);
            UiChrome.Stretch(_customValueLabel.rectTransform);
            x += CustomValueWidth + StepGap;

            _fineUpChip = AddStepChip("FineUp", "+", x, StepChipWidth, out _fineUpLabel);
            Wire(_fineUpChip, "durationFineUp", () => AdjustCustomMinutes(FineStepMinutes));
            x += StepChipWidth + StepGap;

            _coarseUpChip = AddStepChip("CoarseUp", "+" + CoarseStepMinutes, x, StepChipWidth, out _coarseUpLabel);
            Wire(_coarseUpChip, "durationCoarseUp", () => AdjustCustomMinutes(CoarseStepMinutes));

            _customRow.gameObject.SetActive(false);
        }

        /// <summary>시간 칩 행이 통째로 교체되는 두 벌 — <b>같은 y·같은 높이</b>다(세로 증가 0pt).</summary>
        private RectTransform NewRow(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_idlePage, false);
            var rt = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(rt, 0f, ChipRowY, ContentWidth, ChipRowHeight);
            return rt;
        }

        private Image AddStepChip(string name, string glyph, float x, float width, out Text label)
        {
            Image surface = UiChrome.AddSurface(_customRow, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(surface.rectTransform, x, 0f, width, ChipRowHeight);
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            label = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;
            return surface;
        }

        private void BuildRunningPage(RectTransform content)
        {
            var pageGo = new GameObject("RunningPage", typeof(RectTransform));
            pageGo.transform.SetParent(content, false);
            _runningPage = pageGo.GetComponent<RectTransform>();
            UiChrome.Stretch(_runningPage);

            var dialGo = new GameObject("Dial", typeof(RectTransform));
            dialGo.transform.SetParent(_runningPage, false);
            var dialRect = dialGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(dialRect, (ContentWidth - DialDiameter) * 0.5f, 0f, DialDiameter, DialDiameter);

            // ★ AddCircle의 diameter는 <b>바깥</b> 지름이고 두께는 안쪽으로 먹는다 → r 29…37.
            UiChrome.AddCircle(dialRect, "Track", RingDiameter, UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.PanelSurface), RingThickness);
            _ringFill = UiChrome.AddCircle(dialRect, "Fill", RingDiameter, UiChrome.WarmAccent, RingThickness);
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;

            _timeText = UiChrome.AddText(dialRect, "Time", UiChrome.FontDisplay, TextAnchor.MiddleCenter,
                UiChrome.TextPrimary, bold: true);
            PlaceOnDial(_timeText.rectTransform, 0f, 0f, DialDiameter, TimeBoxHeight);
            _timeText.text = "--:--";

            // ★ 「{n}분 중 남음」으로 늘리지 않는다 — 그 y에서 링 안쪽 가용 반폭이 16.85pt뿐이라
            //   11.15pt 모자란다(§R5-7). 고른 길이는 <b>시작 표식</b>이 그린다.
            Text remainLabel = UiChrome.AddText(dialRect, "RemainLabel", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            PlaceOnDial(remainLabel.rectTransform, 0f, RemainLabelOffsetY, DialDiameter, RemainLabelHeight);
            remainLabel.text = "남음";

            // 눈금판 — 세션이 한 바퀴를 넘으면 <b>통째로</b> 꺼지고 링이 상대로 물러난다(§R5-1-3 계약).
            var faceGo = new GameObject("Face", typeof(RectTransform));
            faceGo.transform.SetParent(dialRect, false);
            _dialFace = faceGo.GetComponent<RectTransform>();
            UiChrome.Stretch(_dialFace);
            BuildDialFace(_dialFace);

            // 시작 표식은 눈금판의 <b>마지막</b> 자식이라 눈금 위에 그려진다(r 36…46이 링 바깥
            // 테두리를 1pt 파고들어 "여기서 출발했다"가 링 위에서 읽힌다).
            _startMarker = UiChrome.AddStroke(_dialFace, "StartMarker", StartMarkerLength,
                StartMarkerThickness, 90f, new Vector2(0f, StartMarkerMidRadius), UiChrome.TextPrimary);

            _statusText = UiChrome.AddText(_runningPage, "Status", UiChrome.FontLabel,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(_statusText.rectTransform, 0f, StatusY, ContentWidth, StatusHeight);
            _statusText.text = "집중 시간을 재는 중";

            // ★ 빨강/파괴적 스타일 금지 — 18절 "패널티 없는 톤".
            _stopSurface = UiChrome.AddSurface(_runningPage, "Stop", UiChrome.CardSurface, UiChrome.RadiusCard);
            UiChrome.PlaceTopLeft(_stopSurface.rectTransform, 0f, StopY, ContentWidth, StopHeight);
            UiChrome.AddOutline(_stopSurface.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusCard);
            Text stopLabel = UiChrome.AddText(_stopSurface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(stopLabel.rectTransform);
            stopLabel.text = "그만두기";
            Wire(_stopSurface, "stop", StopSession);

            _runningPage.gameObject.SetActive(false);
        }

        /// <summary>
        /// 눈금 60개 + 숫자 12개 + 시작 표식 1개 = <b>73개 정적 자식</b>. Awake에서 한 번 만들고
        /// 이후 어떤 프레임도 만지지 않는다(시작 표식만 세션 길이가 바뀐 프레임에 한 번 움직인다).
        /// <para>1장 텍스처로 굽지 않는 이유: 이 앱은 40×40 아이콘 32종을 전부 획 조합으로 그린다.
        /// 획은 어느 배율에서도 벡터로 선명하고 캡슐 스프라이트는 두께비 2종뿐이라 배칭이 유지된다.
        /// Ø128@2x 텍스처는 256KB에 150%/200%에서 얇은 방사선이 뭉갠다(§R5-5-4).</para>
        /// </summary>
        private static void BuildDialFace(RectTransform face)
        {
            for (int m = 0; m < DialMinutes; m++)
            {
                float theta = m * DegreesPerMinute;
                Vector2 u = DialDirection(theta);
                bool major = m % CoarseStepMinutes == 0;

                // AddStroke는 <b>수평</b> 캡슐을 angleDegrees만큼 돌린다 → 90° − θ 로 눕히면 반경 방향이 된다.
                UiChrome.AddStroke(face, major ? "TickMajor" : "TickMinor",
                    major ? MajorTickLength : MinorTickLength,
                    major ? MajorTickThickness : MinorTickThickness,
                    90f - theta,
                    u * (major ? MajorTickMidRadius : MinorTickMidRadius),
                    major ? UiChrome.TextTertiary : UiChrome.NonTextMuted);

                if (!major) continue;

                // 숫자는 <b>회전시키지 않는다</b>(참고 사진과 동일 — 전부 정립).
                Text number = UiChrome.AddText(face, "Number", UiChrome.FontCaption,
                    TextAnchor.MiddleCenter, UiChrome.TextTertiary);
                PlaceOnDial(number.rectTransform, u.x * NumberRingRadius, u.y * NumberRingRadius,
                    NumberBoxWidth, NumberBoxHeight);
                number.text = m.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>분 <paramref name="degreesFromTop"/>°(12시 = 0°, 시계방향)의 단위 방향.</summary>
        private static Vector2 DialDirection(float degreesFromTop)
        {
            float rad = degreesFromTop * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        /// <summary>다이얼 <b>중심 기준</b> 오프셋을 <see cref="UiChrome.PlaceTopLeft"/>(좌상단 기준)로 옮긴다.
        /// 부모는 128×128 다이얼 상자(또는 그것을 꽉 채운 눈금판)여야 한다.</summary>
        private static void PlaceOnDial(RectTransform rt, float cx, float cy, float width, float height)
            => UiChrome.PlaceTopLeft(rt, DialRadius + cx - width * 0.5f,
                -(DialRadius - cy - height * 0.5f), width, height);

        // ==================== 동작 ====================

        private void SelectDurationChip(int index)
        {
            if (index == CustomChipIndex) { EnterCustomMode(); return; }
            _mode = DurationMode.Preset;
            _selectedDuration = Mathf.Clamp(index, 0, DurationMinutes.Length - 1);
            RefreshContent();
        }

        /// <summary>[직접] — 같은 y·같은 높이의 스테퍼 행으로 통째로 교체한다.
        /// <para>한 번도 손대지 않은 값이면 <b>지금 고른 프리셋에서 출발</b>한다. 그래야 §R5-3-3의
        /// 클릭 비용(기본 25에서 최악 9클릭)이 실제로 성립한다. 사용자가 한 번이라도 스테퍼를
        /// 움직였으면 그 값이 남는다 — 「목록」으로 나갔다 돌아와도 그대로다.</para></summary>
        private void EnterCustomMode()
        {
            if (!_customTouched)
                _customMinutes = ClampMinutes(Mathf.RoundToInt(DurationMinutes[_selectedDuration]));
            _mode = DurationMode.Custom;
            RefreshContent();
        }

        private void ExitCustomMode()
        {
            _mode = DurationMode.Preset;
            RefreshContent();
        }

        /// <summary>되감기는 <b>없다</b> — 끝에서 한 번 더 누르면 반대 끝으로 튀는 대신 그 자리에
        /// 멈춘다. 「실수로 60분」이 그쪽이 더 비싸다(§R5-3-3). −5를 3분에서 누르면 −2가 아니라 1이다.</summary>
        private void AdjustCustomMinutes(int deltaMinutes)
        {
            _customTouched = true;
            _customMinutes = ClampMinutes(_customMinutes + deltaMinutes);
            RefreshContent();
        }

        private static int ClampMinutes(int minutes)
            => Mathf.Clamp(minutes, MinimumSessionMinutes, MaximumSessionMinutes);

        private void StartSession()
        {
            if (_director == null) _director = GetComponent<FocusWatchDirector>();
            if (_director == null)
            {
                Debug.LogWarning("[집중팝오버] 시작 실패 — 씬에 FocusWatchDirector가 없습니다.");
                return;
            }
            // ★ 2026-09-02 등급 배선 — 이 파일의 <c>IsSuspended</c> 참조들은 <b>일부러 그대로 둔다</b>.
            //   이 창(팝오버)을 걷는 일은 부모 <see cref="PopoverPanel.Update"/>가 <c>ArePanelsSuppressed</c>로
            //   이미 한다(등급 1). 여기 남은 참조들이 묻는 것은 표면이 아니라 <b>캐릭터가 멈췄는가</b>다 —
            //   집중 세션은 캐릭터가 옆에서 함께 있는 연출이라 등급 2가 정확한 축이다. 등급 1에서는
            //   캐릭터가 그대로 노므로 세션도 그대로 유효하다(닫힌 팝오버 때문에 도달할 일이 없을 뿐).
            if (Agent != null && Agent.IsSuspended)
            {
                Debug.Log("[집중팝오버] 전체화면 앱 사용 중이라 시작하지 않습니다(비침해 원칙 2 — 자동 숨김 상태).");
                RefreshContent();
                return;
            }

            // ★ ForceTriggerNow(90초 데모)가 아니라 사용자가 고른 길이 그대로.
            _director.StartFocusSession(SelectedMinutes);
            Debug.Log($"[집중팝오버] 시작 — {SelectedMinutes:F0}분({SelectedMinutes * 60f:F0}초) 세션" +
                $"({(_mode == DurationMode.Custom ? "직접 입력" : "프리셋")}).");
            RefreshContent();
        }

        private void StopSession()
        {
            if (_director == null) _director = GetComponent<FocusWatchDirector>();
            if (_director == null) return;
            _director.StopFocusSession();
            Debug.Log("[집중팝오버] 그만두기 — 패널티 없는 톤으로 종료합니다(18절).");
            RefreshContent();
        }

        // ==================== 갱신 ====================

        protected override void OnOpened()
        {
            if (_director == null) _director = GetComponent<FocusWatchDirector>();
        }

        protected override void TickSlow() => RefreshContent();

        /// <summary>
        /// ★ §R5-5-2 — <b>초 판정만</b> <see cref="TickSlow"/>(0.25초 주기) 밖으로 꺼내 매 프레임 돈다.
        ///
        /// <para>절대 다이얼에서 호 끝은 15fps 기준 프레임당 <b>0.0038pt</b>(1픽셀의 1/260) 움직인다 —
        /// 사실상 정지 그림이다. 그래서 진행 페이지에서 <b>움직이는 것이 mm:ss 하나만 남고</b>,
        /// 느린 갱신의 톱니(15fps에서 간격이 1066.7 / 1066.7 / <b>800.0</b>ms로 갈라진다 —
        /// <c>_slowTimer</c>가 나머지를 이월하지 않기 때문)가 숨을 곳이 없어진다.</para>
        ///
        /// <para>비용은 프레임당 <c>int</c> 비교 1회 + <c>float</c> 대입 1회, <b>할당 0</b>이다.
        /// 문자열은 초가 실제로 바뀐 프레임에만 만든다. ★ <c>HoldActiveForInteraction()</c>은
        /// 부르지 않는다 — 호가 필요로 하는 프레임 예산은 홀드 기준선의 1/694이고, 세션 60분 내내
        /// 60fps를 붙잡는 것은 그 자체가 원칙 2 위반이다(§R5-5-1).</para>
        /// </summary>
        protected override void Update()
        {
            base.Update();
            if (!IsOpen || !_running) return;
            RefreshTimerReadout();
        }

        protected override void RefreshContent()
        {
            if (_director == null) _director = GetComponent<FocusWatchDirector>();
            bool running = _director != null && _director.IsSessionActive;
            if (running != _running)
            {
                _running = running;
                _lastShownSeconds = -1;      // 페이지가 바뀌면 다음 갱신에서 반드시 다시 찍는다.
                _shownMarkerSeconds = -1f;
                if (Panel != null) Panel.sizeDelta = PanelSizePoints;
                _idlePage.gameObject.SetActive(!running);
                _runningPage.gameObject.SetActive(running);
            }
            SetTitle(running ? "집중 모드 · 진행 중" : "집중 모드");

            if (running) RefreshRunning();
            else RefreshIdle();
        }

        /// <summary>고른 칩 / 안 고른 칩의 <b>면</b>. 고른 칩의 강조 면은 α0.14라 그대로 칠하면
        /// 그 화소의 창 알파가 12% 내려간다 — 팝오버 바탕에 미리 합성한다(정책 §5-2).</summary>
        private static Color ChipFace(bool on)
            => on ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface) : UiChrome.CardSurface;

        /// <summary>그 칩의 <b>테두리</b> — 바탕은 팝오버가 아니라 <b>방금 정한 칩 면</b>이다.</summary>
        private static Color ChipEdge(bool on, Color face)
            => UiChrome.Flatten(on ? UiChrome.AccentBorder : UiChrome.CardBorder, face);

        private void RefreshIdle()
        {
            bool custom = _mode == DurationMode.Custom;
            if (_presetRow.gameObject.activeSelf == custom) _presetRow.gameObject.SetActive(!custom);
            if (_customRow.gameObject.activeSelf != custom) _customRow.gameObject.SetActive(custom);

            // ★ 함정(과거 실측) — 옛 코드는 <b>하나의</b> for(i<3)가 두 종류의 칩을 함께 돌았고,
            //   시간 칩이 4개가 되는 순간 그 루프는 [직접]을 보지 못했다. 루프는 반드시 <b>자기 배열의
            //   Length</b>를 센다(상수 3을 다시 적지 마라 — 그게 그때의 사고였다).
            for (int i = 0; i < _durationChips.Length; i++)
            {
                bool on = i == CustomChipIndex ? custom : !custom && i == _selectedDuration;
                // ★ 2026-09-06 — 면을 먼저 정하고 테두리를 <b>그 면 위에</b> 합성한다(정책 §5-2).
                Color face = ChipFace(on);
                _durationChips[i].color = face;
                _durationOutlines[i].color = ChipEdge(on, face);
                _durationLabels[i].color = on ? UiChrome.TextOnAccent : UiChrome.TextSecondary;
                _durationLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }

            if (custom) RefreshCustomRow();

            bool suspended = Agent != null && Agent.IsSuspended;
            if (_suspendedNotice.gameObject.activeSelf != suspended) _suspendedNotice.gameObject.SetActive(suspended);
            _startSurface.color = suspended ? UiChrome.CardSurfaceMuted : UiChrome.Accent;
            _startLabel.color = suspended ? UiChrome.TextTertiary : UiChrome.OnAccentSolid;
        }

        /// <summary>스테퍼가 끝에 닿으면 그 버튼만 꺼진 얼굴이 된다 — <b>사유 줄은 없다</b>.
        /// 숫자가 안 변하는 것이 자기설명적이고, 세로 여유가 0이라 놓을 자리도 없다(§R5-3-5).</summary>
        private void RefreshCustomRow()
        {
            if (_customMinutes != _shownCustomMinutes)
            {
                _shownCustomMinutes = _customMinutes;   // 4Hz 루프에서 문자열을 새로 만들지 않는다.
                _customValueLabel.text = _customMinutes + "분";
            }

            bool canGoDown = _customMinutes > MinimumSessionMinutes;
            bool canGoUp = _customMinutes < MaximumSessionMinutes;
            ApplyStepEnabled(_coarseDownChip, _coarseDownLabel, canGoDown);
            ApplyStepEnabled(_fineDownChip, _fineDownLabel, canGoDown);
            ApplyStepEnabled(_fineUpChip, _fineUpLabel, canGoUp);
            ApplyStepEnabled(_coarseUpChip, _coarseUpLabel, canGoUp);
            ApplyStepEnabled(_listChip, _listLabel, true);
        }

        /// <summary>꺼진 컨트롤의 어휘는 <see cref="_startSurface"/>가 서스펜드 때 쓰는 바로 그것이다
        /// (면 <see cref="UiChrome.CardSurfaceMuted"/> + 잉크 한 단 내림). 새 어휘를 만들지 않는다.
        /// <para>★ 설계 초안은 잉크를 <see cref="UiChrome.DisabledControlInk"/>로 적었지만 그것은
        /// <b>글자에 쓸 수 없는 잉크</b>다 — <c>UiChrome.AddText</c>가 본문 대비 하한 미달로
        /// LogError를 찍는다. 설계가 근거로 든 실제 선례(<c>_startLabel</c>)가 쓰는 값이
        /// <c>InkBody(false)</c>(= TextTertiary)라 그쪽을 따랐다.</para></summary>
        private static void ApplyStepEnabled(Image chip, Text label, bool enabled)
        {
            if (chip == null || label == null) return;
            chip.color = enabled ? UiChrome.CardSurface : UiChrome.CardSurfaceMuted;
            label.color = enabled ? UiChrome.TextSecondary : UiChrome.InkBody(false);
        }

        private void RefreshRunning()
        {
            RefreshTimerReadout();
            _statusText.text = ResolveStatusLine();
            _statusText.color = Agent != null && Agent.IsSuspended ? UiChrome.WarmAccent : UiChrome.TextSecondary;
        }

        /// <summary>
        /// 링과 숫자를 <b>같은 스냅샷</b>에서 쓴다 — 둘이 다른 프레임의 값을 쓰면 "링은 3할인데
        /// 숫자는 다른 값"이 되고, 그 자체가 원칙 1 위반이다. 매 프레임 돈다(<see cref="Update"/>).
        /// </summary>
        private void RefreshTimerReadout()
        {
            if (_director == null || _ringFill == null) return;

            float duration = Mathf.Max(1f, _director.SessionDurationSeconds);
            float remaining = Mathf.Max(0f, _director.RemainingSeconds);

            // 세션이 다이얼 한 바퀴를 넘으면 눈금판이 거짓이 된다 → 얼굴을 끄고 상대 링으로 물러난다.
            // 지금 UI로는 도달할 수 없지만 StartFocusSession에는 상한 클램프가 없다(§R5-6 #14).
            bool absolute = duration <= DialSpanSeconds;
            if (_dialFace != null && _dialFace.gameObject.activeSelf != absolute)
            {
                _dialFace.gameObject.SetActive(absolute);
                Debug.Log(absolute
                    ? "[집중팝오버] 눈금판을 다시 켭니다 — 세션이 60분 다이얼 한 바퀴 안에 들어옵니다."
                    : $"[집중팝오버] 세션 {duration / SecondsPerMinute:F0}분이 다이얼 한 바퀴" +
                      $"({DialSpanSeconds / SecondsPerMinute:F0}분)를 넘어 눈금판을 끄고 상대 링으로 물러납니다 — " +
                      "숫자판이 나르지 못하는 뜻을 나르는 척하지 않습니다(원칙 1).");
            }

            _ringFill.fillAmount = Mathf.Clamp01(remaining / (absolute ? DialSpanSeconds : duration));

            if (absolute && !Mathf.Approximately(duration, _shownMarkerSeconds))
            {
                _shownMarkerSeconds = duration;
                PlaceStartMarker(duration);
            }

            // ★ 서스펜드 중에는 RemainingSeconds가 실제로 줄지 않아 다이얼이 <b>얼어붙는다</b>.
            //   색이 안 변하면 "타이머가 고장났다"로 읽힌다. 탈색은 "멈췄다"만 말하고 이유를
            //   주장하지 않는다 — 이유는 상태줄이 두 가지로 갈라서 말한다(ResolveStatusLine).
            bool suspended = Agent != null && Agent.IsSuspended;
            if (suspended != _ringDesaturated)
            {
                _ringDesaturated = suspended;
                _ringFill.color = suspended ? UiChrome.NonTextMuted : UiChrome.WarmAccent;
            }

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds != _lastShownSeconds)
            {
                _lastShownSeconds = seconds;
                _timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }
        }

        /// <summary>
        /// 시작 표식 — 획 1개가 "여기서 출발해 여기까지 왔다"를 그린다. 없으면 5분 세션의 30° 호가
        /// <i>"거의 끝났다"</i>로 읽힌다(절대 다이얼의 유일한 약점이고, 이 획이 그것을 닫는다).
        /// <para><b>원칙 1</b>: <see cref="FocusWatchDirector.SessionDurationSeconds"/>(확정된 값)에서만
        /// 파생하고 서스펜드 중에도 <b>움직이지 않는다</b> — 움직이면 벽시계를 주장하는 셈이 된다.</para>
        /// </summary>
        private void PlaceStartMarker(float sessionSeconds)
        {
            if (_startMarker == null) return;
            float theta = Mathf.Clamp01(sessionSeconds / DialSpanSeconds) * 360f;
            var rt = _startMarker.rectTransform;
            rt.anchoredPosition = DialDirection(theta) * StartMarkerMidRadius;
            rt.localRotation = Quaternion.Euler(0f, 0f, 90f - theta);
        }

        /// <summary>표에 있는 실제 값에서만 파생한다(32-5). 문구를 여기서 지어내지 않는다.</summary>
        private string ResolveStatusLine()
        {
            // 숨김 사유가 둘이 됐다(전체화면 감지 / 사용자가 직접). 고정 문장을 쓰면 수동 숨김일 때
            // 화면이 거짓말을 한다 — 원칙 1은 "확정된 상태로부터만 파생"이므로 사유까지 파생시킨다.
            if (Agent != null && Agent.IsSuspended)
                return Agent.IsUserHidden ? "일시정지 · 잠시 숨겨 뒀어요" : "일시정지 · 전체화면 앱 사용 중";

            // ★ 2026-09-06 — 「지켜보는 중 · …」 4문과 「순수 타이머로만 재고 있어요」가 삭제됐다
            //   (지켜보기 기능 자체가 사라져 그 문장들이 가리킬 사실이 없다). 남은 한 문장은
            //   <b>지금 실제로 일어나는 일</b>(시간이 흐르는 중)만 말한다 — 원칙 1.
            return "집중 시간을 재는 중";
        }

        // ==================== 전역 폴링 경로 ====================

        protected override void OnGlobalClick(Vector2 cursor)
        {
            if (_running)
            {
                if (ContainsScreenPoint(_stopSurface.rectTransform, cursor) && TryClaimAction("stop")) StopSession();
                return;
            }

            // ★★ 함정(과거 실측) — 옛 코드는 <b>하나의</b> for(i<3)가 두 종류의 칩을 <b>함께</b> 돌았다.
            //   시간 칩이 4개가 되는데 그 루프는 3에서 멈추므로 [직접]이 <b>이 경로에서만</b> 조용히
            //   죽었다(uGUI Wire 경로로는 눌린다 = 입력 경로 하나만 사는 형태). 여기서는 반드시
            //   자기 배열의 Length를 센다 — 칩 개수가 또 바뀌어도 이 경로가 뒤처지지 않게.
            for (int i = 0; i < _durationChips.Length; i++)
            {
                if (!ContainsScreenPoint(_durationChips[i].rectTransform, cursor)) continue;
                if (TryClaimAction("duration" + i)) SelectDurationChip(i);
                return;
            }
            if (TryCustomRowClick(cursor)) return;

            if (ContainsScreenPoint(_startSurface.rectTransform, cursor))
            {
                if (TryClaimAction("start")) StartSession();
            }
        }

        /// <summary>자유 입력 행의 6개 버튼. 프리셋 모드에서는 행 자체가 꺼져 있어
        /// <see cref="PopoverPanel.ContainsScreenPoint"/>가 전부 false다.</summary>
        private bool TryCustomRowClick(Vector2 cursor)
        {
            if (_mode != DurationMode.Custom) return false;
            if (ContainsScreenPoint(_listChip.rectTransform, cursor))
            {
                if (TryClaimAction("durationList")) ExitCustomMode();
                return true;
            }
            if (ContainsScreenPoint(_coarseDownChip.rectTransform, cursor))
            {
                if (TryClaimAction("durationCoarseDown")) AdjustCustomMinutes(-CoarseStepMinutes);
                return true;
            }
            if (ContainsScreenPoint(_fineDownChip.rectTransform, cursor))
            {
                if (TryClaimAction("durationFineDown")) AdjustCustomMinutes(-FineStepMinutes);
                return true;
            }
            if (ContainsScreenPoint(_fineUpChip.rectTransform, cursor))
            {
                if (TryClaimAction("durationFineUp")) AdjustCustomMinutes(FineStepMinutes);
                return true;
            }
            if (ContainsScreenPoint(_coarseUpChip.rectTransform, cursor))
            {
                if (TryClaimAction("durationCoarseUp")) AdjustCustomMinutes(CoarseStepMinutes);
                return true;
            }
            return false;
        }
    }
}
