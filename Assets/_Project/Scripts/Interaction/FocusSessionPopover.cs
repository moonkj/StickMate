using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 부채꼴 [집중 모드] 버튼에서 자라나는 팝오버 — docs/UX_FLOW.md <b>32-5</b> 확정 설계.
    /// 대기 244×252 / 진행 중 244×224.
    ///
    /// ============================================================================
    /// "즉시 토글"을 택하지 않은 이유
    /// ============================================================================
    /// 18절이 시작 흐름을 "시간 선택(15/25/50분)"으로 이미 못박았다. 토글 하나면 앱이 사용자 대신
    /// 길이를 몰래 정하는 셈이라 언제 끝날지 알 수 없는 타이머가 되고, 18절이 요구한 두 안전장치
    /// (감시 자체를 끄는 옵션 / 민감도 3단계)가 갈 자리도 사라진다.
    ///
    /// ============================================================================
    /// ★ 필수 계약: <see cref="FocusWatchDirector.ForceTriggerNow"/>를 부르지 않는다
    /// ============================================================================
    /// 그 메서드는 <b>90초 고정 데모</b>다. "25분"을 고른 사용자에게 90초짜리 세션을 주면 그 순간
    /// 화면의 숫자가 거짓이 된다(CLAUDE.md 원칙 1 직접 위반). 여기서는 언제나
    /// <see cref="FocusWatchDirector.StartFocusSession"/> / <see cref="FocusWatchDirector.StopFocusSession"/>만
    /// 부른다. 단축키 ⌃⌥⌘F / 우클릭 메뉴는 예전 데모 경로를 그대로 둔다.
    ///
    /// <b>상태 라인은 지어내지 않는다</b>: 기존 이벤트 <see cref="StickmanEventBus.FocusWatchTierChanged"/>와
    /// 실제 값(<see cref="StickmanAgent.IsSuspended"/>, <see cref="FocusWatchDirector.DistractionDetectionEnabled"/>)
    /// 에서만 파생한다. 특히 일시정지 행은 연출이 아니라 <b>사실 보고</b>다 — 전체화면 앱을 쓰는 동안
    /// <c>RemainingSeconds</c>가 실제로 줄지 않기 때문에, 설명이 없으면 "타이머가 고장 났다"로 읽힌다.
    /// </summary>
    public sealed class FocusSessionPopover : PopoverPanel
    {
        private const float Width = 244f;
        private const float IdleHeight = 252f;
        private const float RunningHeight = 224f;
        private const float ContentWidth = Width - UiChrome.Space4 * 2f;   // 212.

        private static readonly float[] DurationMinutes = { 15f, 25f, 50f };
        private static readonly string[] DurationLabels = { "15분", "25분", "50분" };
        private static readonly string[] SensitivityLabels = { "관대", "보통", "예민" };

        private const float RingDiameter = 84f;
        private const float RingThickness = 7f;

        private FocusWatchDirector _director;

        // 대기 페이지.
        private RectTransform _idlePage;
        private readonly Image[] _durationChips = new Image[3];
        private readonly Text[] _durationLabels = new Text[3];
        private readonly Image[] _durationOutlines = new Image[3];
        private readonly Image[] _sensitivityChips = new Image[3];
        private readonly Text[] _sensitivityLabels = new Text[3];
        private readonly Image[] _sensitivityOutlines = new Image[3];
        private Image _switchTrack;
        private RectTransform _switchKnob;
        private Image _switchKnobImage;
        private RectTransform _switchRect;
        private Image _startSurface;
        private Text _startLabel;
        private Text _suspendedNotice;

        // 진행 페이지.
        private RectTransform _runningPage;
        private Image _ringFill;
        private Text _timeText;
        private Text _statusText;
        private Image _stopSurface;

        private int _selectedDuration = 1;   // 기본 25분.
        private bool _running;
        private FocusWatchTier _tier = FocusWatchTier.None;
        private int _lastShownSeconds = -1;

        protected override Vector2 PanelSizePoints => new Vector2(Width, _running ? RunningHeight : IdleHeight);

        protected override string TitleText => "집중 모드";

        /// <summary>지금 고른 세션 길이(분) — 회귀 테스트가 "25분을 골랐는데 90초가 시작됐다"를 잡는다.</summary>
        public float SelectedMinutes => DurationMinutes[_selectedDuration];

        /// <summary>진행 중 화면을 보여주고 있는가.</summary>
        public bool ShowingRunningPage => _running;

        /// <summary>지금 표시 중인 상태 라인(테스트/진단 전용).</summary>
        public string StatusLine => _statusText != null ? _statusText.text : string.Empty;

        /// <summary>시간 칩의 화면 사각형(Unity 스크린 픽셀) — 테스트가 실제 클릭 경로로 누른다.</summary>
        public Rect DurationChipScreenRect(int index)
            => index >= 0 && index < 3 ? ScreenRectOf(_durationChips[index].rectTransform) : new Rect();

        public Rect StartButtonScreenRect => ScreenRectOf(_startSurface.rectTransform);
        public Rect StopButtonScreenRect => ScreenRectOf(_stopSurface.rectTransform);

        /// <summary>진행 링의 채움 비율(0~1) — 라벨의 mm:ss와 <b>같은 스냅샷</b>에서 나와야 한다.</summary>
        public float RingFillAmount => _ringFill != null ? _ringFill.fillAmount : -1f;

        /// <summary>진행 화면에 지금 찍혀 있는 mm:ss 문자열.</summary>
        public string TimeLabel => _timeText != null ? _timeText.text : string.Empty;

        protected override void Start()
        {
            base.Start();
            _director = GetComponent<FocusWatchDirector>();
        }

        private void OnEnable() => StickmanEventBus.FocusWatchTierChanged += OnTierChanged;

        protected override void OnDisable()
        {
            base.OnDisable();
            StickmanEventBus.FocusWatchTierChanged -= OnTierChanged;
        }

        private void OnTierChanged(FocusWatchTier tier)
        {
            _tier = tier;
            if (IsOpen) RefreshContent();
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
            subtitle.text = "정한 시간 동안 옆에서 지켜볼게요.";

            Text durationLabel = UiChrome.AddText(_idlePage, "DurationLabel", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(durationLabel.rectTransform, 0f, -24f, ContentWidth, 14f);
            durationLabel.text = "시간";

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Image chip = UiChrome.AddSurface(_idlePage, "Duration" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                UiChrome.PlaceTopLeft(chip.rectTransform, i * (66f + 7f), -42f, 66f, 32f);
                _durationOutlines[i] = UiChrome.AddOutline(chip.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
                Text label = UiChrome.AddText(chip.rectTransform, "Label", UiChrome.FontBody,
                    TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(label.rectTransform);
                label.text = DurationLabels[i];
                _durationChips[i] = chip;
                _durationLabels[i] = label;
                Wire(chip, "duration" + i, () => SelectDuration(index));
            }

            Image divider = UiChrome.AddSurface(_idlePage, "Divider", UiChrome.Divider, 2);
            UiChrome.PlaceTopLeft(divider.rectTransform, 0f, -84f, ContentWidth, 1f);

            Text watchLabel = UiChrome.AddText(_idlePage, "WatchLabel", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(watchLabel.rectTransform, 0f, -94f, 120f, 20f);
            watchLabel.text = "지켜보기";

            _switchTrack = UiChrome.AddSurface(_idlePage, "WatchSwitch", UiChrome.Accent, 10);
            _switchRect = _switchTrack.rectTransform;
            UiChrome.PlaceTopLeft(_switchRect, ContentWidth - 38f, -94f, 38f, 20f);
            _switchKnobImage = UiChrome.AddCircle(_switchRect, "Knob", 16f, UiChrome.OnAccentSolid);
            _switchKnob = _switchKnobImage.rectTransform;
            Wire(_switchTrack, "watchSwitch", ToggleWatch);

            Text sensitivityLabel = UiChrome.AddText(_idlePage, "SensitivityLabel", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(sensitivityLabel.rectTransform, 0f, -122f, 60f, 22f);
            sensitivityLabel.text = "민감도";

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Image chip = UiChrome.AddSurface(_idlePage, "Sensitivity" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                UiChrome.PlaceTopLeft(chip.rectTransform, ContentWidth - (3 - i) * 51f - (2 - i) * 2f, -122f, 51f, 22f);
                _sensitivityOutlines[i] = UiChrome.AddOutline(chip.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
                Text label = UiChrome.AddText(chip.rectTransform, "Label", UiChrome.FontCaption,
                    TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(label.rectTransform);
                label.text = SensitivityLabels[i];
                _sensitivityChips[i] = chip;
                _sensitivityLabels[i] = label;
                Wire(chip, "sensitivity" + i, () => SelectSensitivity(index));
            }

            _suspendedNotice = UiChrome.AddText(_idlePage, "SuspendedNotice", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.WarmAccent);
            UiChrome.PlaceTopLeft(_suspendedNotice.rectTransform, 0f, -148f, ContentWidth, 12f);
            _suspendedNotice.text = "전체화면 앱을 닫으면 시작할 수 있어요.";
            _suspendedNotice.gameObject.SetActive(false);

            _startSurface = UiChrome.AddSurface(_idlePage, "Start", UiChrome.Accent, UiChrome.RadiusCard);
            UiChrome.PlaceTopLeft(_startSurface.rectTransform, 0f, -160f, ContentWidth, 34f);
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

        private void BuildRunningPage(RectTransform content)
        {
            var pageGo = new GameObject("RunningPage", typeof(RectTransform));
            pageGo.transform.SetParent(content, false);
            _runningPage = pageGo.GetComponent<RectTransform>();
            UiChrome.Stretch(_runningPage);

            var ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(_runningPage, false);
            var ringRect = ringGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(ringRect, (ContentWidth - RingDiameter) * 0.5f, -4f, RingDiameter, RingDiameter);

            UiChrome.AddCircle(ringRect, "Track", RingDiameter, UiChrome.TrackBackground, RingThickness);
            _ringFill = UiChrome.AddCircle(ringRect, "Fill", RingDiameter, UiChrome.WarmAccent, RingThickness);
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;

            _timeText = UiChrome.AddText(ringRect, "Time", UiChrome.FontDisplay, TextAnchor.MiddleCenter,
                UiChrome.TextPrimary, bold: true);
            UiChrome.PlaceTopLeft(_timeText.rectTransform, 0f, -RingDiameter * 0.5f + 13f, RingDiameter, 26f);
            _timeText.text = "--:--";

            Text remainLabel = UiChrome.AddText(ringRect, "RemainLabel", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(remainLabel.rectTransform, 0f, -RingDiameter * 0.5f - 14f, RingDiameter, 14f);
            remainLabel.text = "남음";

            _statusText = UiChrome.AddText(_runningPage, "Status", UiChrome.FontLabel,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(_statusText.rectTransform, 0f, -96f, ContentWidth, 16f);
            _statusText.text = "지켜보는 중 · 조용해요";

            // ★ 빨강/파괴적 스타일 금지 — 18절 "패널티 없는 톤".
            _stopSurface = UiChrome.AddSurface(_runningPage, "Stop", UiChrome.CardSurface, UiChrome.RadiusCard);
            UiChrome.PlaceTopLeft(_stopSurface.rectTransform, 0f, -122f, ContentWidth, 32f);
            UiChrome.AddOutline(_stopSurface.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);
            Text stopLabel = UiChrome.AddText(_stopSurface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(stopLabel.rectTransform);
            stopLabel.text = "그만두기";
            Wire(_stopSurface, "stop", StopSession);

            _runningPage.gameObject.SetActive(false);
        }

        // ==================== 동작 ====================

        private void SelectDuration(int index)
        {
            _selectedDuration = Mathf.Clamp(index, 0, DurationMinutes.Length - 1);
            RefreshContent();
        }

        private void SelectSensitivity(int index)
        {
            if (_director == null) _director = GetComponent<FocusWatchDirector>();
            if (_director != null) _director.Sensitivity = (PomodoroSensitivity)Mathf.Clamp(index, 0, 2);
            RefreshContent();
        }

        private void ToggleWatch()
        {
            if (_director == null) _director = GetComponent<FocusWatchDirector>();
            if (_director == null) return;
            _director.DistractionDetectionEnabled = !_director.DistractionDetectionEnabled;
            RefreshContent();
        }

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
            //   집중 세션은 캐릭터가 지켜보고 말을 거는 기능이라 등급 2가 정확한 축이다. 등급 1에서는
            //   캐릭터가 그대로 노므로 세션도 그대로 유효하다(닫힌 팝오버 때문에 도달할 일이 없을 뿐).
            if (Agent != null && Agent.IsSuspended)
            {
                Debug.Log("[집중팝오버] 전체화면 앱 사용 중이라 시작하지 않습니다(비침해 원칙 2 — 자동 숨김 상태).");
                RefreshContent();
                return;
            }

            // ★ ForceTriggerNow(90초 데모)가 아니라 사용자가 고른 길이 그대로.
            _director.StartFocusSession(SelectedMinutes);
            Debug.Log($"[집중팝오버] 시작 — {SelectedMinutes:F0}분({SelectedMinutes * 60f:F0}초) 세션. " +
                $"민감도 {_director.Sensitivity}, 딴짓 감지 {(_director.DistractionDetectionEnabled ? "켬" : "끔")}.");
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

        protected override void RefreshContent()
        {
            if (_director == null) _director = GetComponent<FocusWatchDirector>();
            bool running = _director != null && _director.IsSessionActive;
            if (running != _running)
            {
                _running = running;
                if (Panel != null) Panel.sizeDelta = PanelSizePoints;
                _idlePage.gameObject.SetActive(!running);
                _runningPage.gameObject.SetActive(running);
            }
            SetTitle(running ? "집중 모드 · 진행 중" : "집중 모드");

            if (running) RefreshRunning();
            else RefreshIdle();
        }

        private void RefreshIdle()
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == _selectedDuration;
                _durationChips[i].color = on ? UiChrome.AccentSurface : UiChrome.CardSurface;
                _durationOutlines[i].color = on ? UiChrome.AccentBorder : UiChrome.CardBorder;
                _durationLabels[i].color = on ? UiChrome.TextOnAccent : UiChrome.TextSecondary;
                _durationLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }

            int sensitivity = _director != null ? (int)_director.Sensitivity : 1;
            for (int i = 0; i < 3; i++)
            {
                bool on = i == sensitivity;
                _sensitivityChips[i].color = on ? UiChrome.AccentSurface : UiChrome.CardSurface;
                _sensitivityOutlines[i].color = on ? UiChrome.AccentBorder : UiChrome.CardBorder;
                _sensitivityLabels[i].color = on ? UiChrome.TextOnAccent : UiChrome.TextSecondary;
            }

            bool watching = _director == null || _director.DistractionDetectionEnabled;
            _switchTrack.color = watching ? UiChrome.Accent : UiChrome.TrackBackground;
            _switchKnob.anchoredPosition = new Vector2(watching ? 9f : -9f, 0f);
            _switchKnobImage.color = UiChrome.OnAccentSolid;

            bool suspended = Agent != null && Agent.IsSuspended;
            if (_suspendedNotice.gameObject.activeSelf != suspended) _suspendedNotice.gameObject.SetActive(suspended);
            _startSurface.color = suspended ? UiChrome.CardSurfaceMuted : UiChrome.Accent;
            _startLabel.color = suspended ? UiChrome.TextTertiary : UiChrome.OnAccentSolid;
        }

        private void RefreshRunning()
        {
            float duration = Mathf.Max(1f, _director.SessionDurationSeconds);
            float remaining = Mathf.Max(0f, _director.RemainingSeconds);

            // 링과 숫자를 <b>같은 스냅샷</b>에서 쓴다 — 둘이 다른 프레임의 값을 쓰면 "링은 3할인데
            // 숫자는 다른 값"이 되고, 그 자체가 원칙 1 위반이다.
            _ringFill.fillAmount = Mathf.Clamp01(remaining / duration);
            int seconds = Mathf.CeilToInt(remaining);
            if (seconds != _lastShownSeconds)
            {
                _lastShownSeconds = seconds;
                _timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }

            _statusText.text = ResolveStatusLine();
            _statusText.color = Agent != null && Agent.IsSuspended ? UiChrome.WarmAccent : UiChrome.TextSecondary;
        }

        /// <summary>표에 있는 실제 값에서만 파생한다(32-5). 문구를 여기서 지어내지 않는다.</summary>
        private string ResolveStatusLine()
        {
            // 숨김 사유가 둘이 됐다(전체화면 감지 / 사용자가 직접). 고정 문장을 쓰면 수동 숨김일 때
            // 화면이 거짓말을 한다 — 원칙 1은 "확정된 상태로부터만 파생"이므로 사유까지 파생시킨다.
            if (Agent != null && Agent.IsSuspended)
                return Agent.IsUserHidden ? "일시정지 · 잠시 숨겨 뒀어요" : "일시정지 · 전체화면 앱 사용 중";
            if (_director != null && !_director.DistractionDetectionEnabled) return "순수 타이머로만 재고 있어요";
            return _tier switch
            {
                FocusWatchTier.Glance => "지켜보는 중 · 곁눈질했어요",
                FocusWatchTier.Nudge => "지켜보는 중 · 한마디 했어요",
                FocusWatchTier.WindowTap => "지켜보는 중 · 타이머를 두드리는 중",
                _ => "지켜보는 중 · 조용해요",
            };
        }

        // ==================== 전역 폴링 경로 ====================

        protected override void OnGlobalClick(Vector2 cursor)
        {
            if (_running)
            {
                if (ContainsScreenPoint(_stopSurface.rectTransform, cursor) && TryClaimAction("stop")) StopSession();
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                if (ContainsScreenPoint(_durationChips[i].rectTransform, cursor))
                {
                    if (TryClaimAction("duration" + i)) SelectDuration(i);
                    return;
                }
                if (ContainsScreenPoint(_sensitivityChips[i].rectTransform, cursor))
                {
                    if (TryClaimAction("sensitivity" + i)) SelectSensitivity(i);
                    return;
                }
            }
            if (ContainsScreenPoint(_switchRect, cursor))
            {
                if (TryClaimAction("watchSwitch")) ToggleWatch();
                return;
            }
            if (ContainsScreenPoint(_startSurface.rectTransform, cursor))
            {
                if (TryClaimAction("start")) StartSession();
            }
        }
    }
}
