using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Dialogue
{
    /// <summary>
    /// ★ 말풍선 렌더링 — docs/UX_FLOW.md 5절 `DialogueIntent` UX 계약의 **화면 구현부**.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가 (정직한 이력)
    /// ============================================================================
    /// 이 프로젝트의 1순위 원칙(CLAUDE.md 절대 불변 원칙 1 "행동-텍스트 싱크")의 산출물인
    /// `DialogueIntent` 파이프라인은 여러 라운드에 걸쳐 정교하게 만들어졌고 EditMode 테스트 8건으로
    /// 계약까지 고정돼 있었지만, <b>`StickmanEventBus.DialogueRequested`를 구독해 실제로 말풍선을 그리는
    /// 코드가 어디에도 없었다</b>. 대사는 계속 생성되고 만료됐지만 아무도 볼 수 없었다. 이 컴포넌트가
    /// 정확히 그 빠진 조각이다 — 파이프라인 쪽은 한 줄도 바꾸지 않고(이벤트 2개만 구독) 순수 소비자로
    /// 붙는다.
    ///
    /// ============================================================================
    /// UX 계약 준수 방식 (5절 규칙별 대응, 이 클래스의 존재 이유)
    /// ============================================================================
    /// · 규칙 3(a) 정상 종료  : `DialogueExpired`가 "그 전이가 강제 인터럽트가 아니었을 때" 도착하면
    ///                          최소 노출 시간을 채운 뒤 <see cref="FadeOutSeconds"/> 페이드아웃.
    /// · 규칙 3(b) 강제 취소  : `DialogueExpired`가 **같은 프레임의 강제 인터럽트 전이**로 인해 도착하면
    ///                          페이드아웃 없이 <b>그 자리에서 동기적으로 즉시 제거</b>
    ///                          (<see cref="HideImmediate"/>). 이벤트 핸들러 안에서 바로 지우므로
    ///                          "취소된 상태의 말풍선이 화면에 남아있는 시간"이 구조적으로 0 프레임이다.
    /// · 규칙 4 우선순위      : 최소 노출 시간(<see cref="StickConfig.dialogueMinVisibleSeconds"/>)은
    ///                          정상 종료 경로에만 적용되고, 강제 취소는 <b>항상</b> 이 규칙을 이긴다
    ///                          (HideImmediate는 경과 시간을 아예 보지 않는다).
    /// · 규칙 5 큐잉 금지     : 새 `DialogueRequested`가 오면 이전 말풍선을 즉시 교체한다 — 다음 대사를
    ///                          모아두었다가 나중에 꺼내는 큐가 애초에 없다.
    /// · 규칙 6 위치/스타일   : 캐릭터 머리 위 + 꼬리가 캐릭터를 가리킴, 등장 150ms/소멸 120ms 페이드,
    ///                          화면 경계 근처에서는 <b>꼬리 방향을 유지한 채 박스만 안쪽으로</b> 민다
    ///                          (<see cref="UpdatePlacement"/>).
    /// · 규칙 7 다중 캐릭터   : `Bind()`로 화자(StickmanStateMachine)를 지정하면 그 머신이 발급한
    ///                          대사만 표시한다 — 라이벌 스틱맨이 동시에 말해도 서로의 말풍선을 훔치지
    ///                          않는다(각자 자기 렌더러를 하나씩 갖는다).
    ///
    /// ============================================================================
    /// "강제 인터럽트인지"를 어떻게 아는가 — 이벤트 순서에 대한 근거
    /// ============================================================================
    /// `DialogueIntent`는 만료 사유를 페이로드에 싣지 않는다(세대 불일치만 본다). 그 판단 근거는
    /// `StateTransitionEvent.IsForcedInterrupt`에 있으므로 이 클래스가 두 이벤트를 함께 구독해 잇는다.
    /// 순서가 항상 성립하는 근거(StickmanStateMachine.ChangeState 구현 기준):
    ///   1) ChangeState -> 세대 증가 -> 새 상태 Enter()(여기서 새 DialogueIntent가 만들어질 수 있음)
    ///   2) 그 다음에야 RaiseStateTransitioned(from, to, isForcedInterrupt)
    ///   3) StateTransitioned 구독자 순서 = 구독 등록 순서 = [이 렌더러(OnEnable, 씬 시작 시점), ...,
    ///      각 DialogueIntent(생성 시점)] — 즉 <b>렌더러가 항상 먼저</b> 플래그를 받는다.
    ///   4) 구세대 DialogueIntent가 자기 차례에 Expire() -> RaiseDialogueExpired
    /// 따라서 DialogueExpired를 받는 시점에는 같은 프레임의 IsForcedInterrupt 값이 이미 손에 있다.
    /// 프레임 번호까지 함께 비교하므로(<see cref="_forcedInterruptFrame"/>) 오래된 플래그를 재사용하는
    /// 사고도 생기지 않는다.
    ///
    /// ============================================================================
    /// 렌더링 방식 — 왜 uGUI(Canvas)인가
    /// ============================================================================
    /// 캐릭터 자체는 LineRenderer로 그리지만(월드 공간), 말풍선은 <b>글자</b>가 본체라 텍스트 레이아웃/
    /// 줄바꿈/폰트 아틀라스가 필요하다. 이 프로젝트에는 TextMeshPro가 없고, 이미
    /// `Interaction/TodoPostItWidget.cs`와 `Interaction/AppControlDirector.cs`가 legacy uGUI
    /// (ScreenSpaceOverlay Canvas + `UnityEngine.UI.Text`)를 런타임 생성해 쓰는 전례가 있어 같은 관례를
    /// 따른다. 투명 오버레이에서도 문제가 없다 — 카메라는 알파 0으로 클리어하지만 ScreenSpaceOverlay
    /// 캔버스는 그 위에 자기 알파로 합성되므로, 불투명 흰 Image가 있는 픽셀만 알파 1이 되어 <b>말풍선
    /// 모양 그대로만</b> 화면에 남는다(배경은 그대로 비친다).
    ///
    /// 한글 폰트: Unity 내장 `LegacyRuntime.ttf`(Arial 계열)에는 한글 글리프가 없어 네모(두부)로 깨진다.
    /// 그래서 <see cref="ResolveKoreanFont"/>가 OS 설치 폰트에서 한글이 실제로 렌더링되는 것을
    /// **글리프 단위로 실측**해(RequestCharactersInTexture -> GetCharacterInfo) 고른다.
    /// </summary>
    public sealed class DialogueBubbleRenderer : MonoBehaviour
    {
        // ==================== 스타일 상수 ====================
        // 캐릭터가 "굵은 검은 획 + 빈 얼굴"이므로 말풍선도 같은 문법(흰 배경 + 굵은 검은 테두리 +
        // 검은 글씨)을 따른다. 값은 전부 Unity 스크린 픽셀(= macOS 포인트, Screen.height≈846 기준).
        private const int SortingOrderBubble = 31000;   // TodoPostItWidget(30000) 위, AppControlDirector 메뉴(32760) 아래.
        private const float BorderThickness = 2.5f;     // 검은 테두리 두께.
        private const float TextPadding = 7f;           // 테두리 안쪽 여백.
        private const float MaxTextWidth = 220f;        // 이 폭을 넘으면 줄바꿈.
        private const float TailWidth = 24f;
        private const float TailHeight = 15f;
        private const float TailPanelOverlap = 3f;      // 꼬리 채움이 박스 아래 테두리를 덮어 자연스럽게 잇는 양.
        private const float ScreenEdgeMargin = 8f;      // 화면 가장자리 최소 여백(규칙 6 "잘리지 않게").
        private const float HeadTopWorldOffset = 0.34f; // 머리 중심에서 꼬리 끝까지(월드 유닛) — 해상도/줌 무관.
        private const float FadeInSeconds = 0.15f;      // 규칙 6 "등장 150ms".
        private const float FadeOutSeconds = 0.12f;     // 규칙 6 "소멸 100~150ms".

        [SerializeField] private StickmanAgent _agent;   // 플레이어용 자동 배선(같은 GameObject 우선).
        [SerializeField] private Transform _anchor;      // 머리 Transform. 비면 Awake에서 "Head"를 찾는다.
        [SerializeField] private StickConfig _config;

        [Tooltip("true면 Bind()로 화자가 명시되기 전까지 어떤 대사도 그리지 않는다. 라이벌 스틱맨처럼 " +
                 "상태머신이 첫 대결 시점에야 만들어지는 화자용 — 이 플래그가 없으면 그 사이에 " +
                 "'화자 미지정 = 모든 대사 수신' 폴백이 걸려 플레이어의 대사를 라이벌 머리 위에 " +
                 "그려버린다(UX_FLOW.md 5절 규칙 7 위반).")]
        [SerializeField] private bool _requireBoundSpeaker;

        // 이 렌더러가 담당하는 화자. null이면 "모든 대사를 받는다"(단일 캐릭터 폴백).
        private StickmanStateMachine _machine;

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _panel;      // 검은 테두리(바깥)
        private RectTransform _tailOutline;
        private RectTransform _tailFill;
        private Image _panelOutlineImage;
        private Image _panelInnerImage;
        private Image _tailOutlineImage;
        private Image _tailFillImage;
        private Text _label;
        private Camera _camera;

        // ==================== 표시 상태 ====================
        private DialogueIntent _active;        // 지금 표시 중인 대사(만료됐지만 최소 노출 중일 수도 있음).
        private string _activeText;
        private float _shownAtUnscaledTime;
        private bool _expiredPendingFadeOut;   // 정상 종료로 만료됨 — 최소 노출 시간을 채운 뒤 페이드아웃.
        private bool _fadingOut;
        private float _alpha;

        // 같은 프레임의 강제 인터럽트 판정용(클래스 문서 "이벤트 순서에 대한 근거" 참고).
        private bool _lastTransitionForced;
        private int _forcedInterruptFrame = -1;

        // ==================== 테스트/진단용 공개 관측점 ====================
        /// <summary>지금 말풍선이 화면에 있는가(알파 &gt; 0이고 루트가 활성). 즉시 제거의 "같은 프레임"
        /// 보장을 PlayMode 테스트가 동기적으로 확인하는 지점이다.</summary>
        public bool IsBubbleVisible => _active != null || _fadingOut;

        /// <summary>지금 화면에 떠 있는 대사 텍스트(없으면 null).</summary>
        public string VisibleText => IsBubbleVisible ? _activeText : null;

        /// <summary>마지막으로 "강제 인터럽트에 의한 즉시 제거"가 일어난 Time.frameCount(없으면 -1).</summary>
        public int LastImmediateRemovalFrame { get; private set; } = -1;

        /// <summary>지금까지 즉시 제거가 몇 번 일어났는지(회귀 테스트 카운터).</summary>
        public int ImmediateRemovalCount { get; private set; }

        /// <summary>
        /// 이 렌더러가 담당할 화자를 지정한다. 라이벌 스틱맨처럼 자기 상태머신을 따로 가진 캐릭터가
        /// 자기 말풍선만 그리게 하려고 쓴다(5절 규칙 7). 플레이어는 Start()에서 자동 배선되므로 보통
        /// 호출할 필요가 없다.
        /// </summary>
        public void Bind(StickmanStateMachine machine, Transform anchor)
        {
            _machine = machine;
            if (anchor != null) _anchor = anchor;
        }

        private void Awake()
        {
            if (_agent == null) _agent = GetComponent<StickmanAgent>();
            if (_anchor == null) _anchor = transform.Find("Head");
            if (_anchor == null) _anchor = transform;
            if (_config == null && _agent != null) _config = _agent.Config;
            BuildUi();
            HideImmediateInternal(logReason: null);
        }

        private void Start()
        {
            _camera = ResolveCamera();
            if (_machine == null && _agent != null && _agent.Blackboard != null) _machine = _agent.Blackboard.Machine;
            if (_config == null && _agent != null) _config = _agent.Config;
            string speaker = _machine != null ? "지정됨"
                : (_requireBoundSpeaker ? "미지정(바인딩 전까지 아무것도 그리지 않음)" : "미지정(모든 대사 수신)");
            Debug.Log("[말풍선] 렌더러 준비 완료 — 화자=" + speaker +
                      $", 폰트='{(_label != null && _label.font != null ? _label.font.name : "없음")}'" +
                      $", 한글렌더={( _koreanGlyphVerified ? "실측 확인" : "미확인(폴백 폰트)")}.");
        }

        private void OnEnable()
        {
            // 구독 순서가 계약의 일부다 — 이 렌더러가 StateTransitioned를 **DialogueIntent보다 먼저**
            // 받아야 같은 프레임의 IsForcedInterrupt를 들고 DialogueExpired를 처리할 수 있다
            // (클래스 문서 "이벤트 순서에 대한 근거"). OnEnable은 어떤 상태 전이보다도 앞선다.
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.DialogueRequested += OnDialogueRequested;
            StickmanEventBus.DialogueExpired += OnDialogueExpired;
        }

        private void OnDestroy()
        {
            // 캔버스가 씬 루트에 있으므로(BuildUi 참고) 이 컴포넌트가 사라질 때 직접 정리해야 고아가
            // 남지 않는다 — 캐릭터가 파괴되는데 말풍선 캔버스만 화면에 남는 사고 방지.
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
        }

        private void OnDisable()
        {
            // 정적 이벤트가 파괴된 인스턴스를 붙들지 않도록 반드시 해제(StickmanEventBus 클래스 문서 3).
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.DialogueRequested -= OnDialogueRequested;
            StickmanEventBus.DialogueExpired -= OnDialogueExpired;
            HideImmediateInternal(logReason: null);
        }

        // ==================== 이벤트 처리 (UX 계약의 본체) ====================

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            _lastTransitionForced = evt.IsForcedInterrupt;
            if (!evt.IsForcedInterrupt) return;
            _forcedInterruptFrame = Time.frameCount;

            // 이미 페이드아웃 중이던(=정상 종료된 옛 상태의) 말풍선도 강제 인터럽트가 오면 즉시 지운다.
            // 규칙 4 "취소가 항상 이긴다"를 페이드 잔상에까지 확장한 것 — 널브러지는 캐릭터 옆에
            // 옛 대사가 반투명으로 남아 있는 순간을 0으로 만든다. 이 시점에는 새 상태의 Enter()가
            // 이미 끝나 있으므로, 새 대사가 있었다면 _fadingOut은 false로 초기화된 뒤다(교체 우선).
            if (_fadingOut)
            {
                LastImmediateRemovalFrame = Time.frameCount;
                ImmediateRemovalCount++;
                HideImmediateInternal("강제 인터럽트 — 페이드아웃 잔상 즉시 제거");
            }
        }

        private void OnDialogueRequested(DialogueIntent intent)
        {
            if (intent == null) return;
            if (!IsMine(intent)) return;
            if (_config != null && !_config.dialogueBubbleEnabled) return;

            // 규칙 5(큐잉 금지): 이전 말풍선은 즉시 교체된다 — 다음 대사를 모아두는 큐가 없다.
            _active = intent;
            _activeText = intent.Text;
            _expiredPendingFadeOut = false;
            _fadingOut = false;
            _shownAtUnscaledTime = Time.unscaledTime;
            _alpha = 0f;

            RefreshColors(); // 잉크색 프리셋(Ctrl+Opt+Cmd+C)이 런타임에 바뀌어도 다음 대사부터 즉시 반영.
            ApplyText(_activeText);
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            UpdatePlacement();
            ApplyAlpha(0f);

            Debug.Log($"[말풍선] 표시 ({intent.StateId}) \"{_activeText}\" — frame={Time.frameCount}");
        }

        private void OnDialogueExpired(DialogueIntent intent)
        {
            if (intent == null || intent != _active) return; // 이미 새 대사로 교체된 옛 대사는 무시.

            bool forcedNow = _lastTransitionForced && _forcedInterruptFrame == Time.frameCount;
            if (forcedNow)
            {
                // ★ 규칙 3(b)/규칙 4 — 강제 인터럽트는 최소 노출 시간을 무시하고 항상 이긴다.
                //   페이드아웃을 기다리지 않고 이 호출 스택 안에서 동기적으로 지운다 = 같은 프레임 제거.
                LastImmediateRemovalFrame = Time.frameCount;
                ImmediateRemovalCount++;
                HideImmediateInternal($"강제 인터럽트 즉시 제거 ({intent.StateId})");
                return;
            }

            // 규칙 3(a) — 정상 종료: 최소 노출 시간을 채운 뒤 페이드아웃(Tick에서 처리).
            _expiredPendingFadeOut = true;
        }

        /// <summary>이 대사가 내가 담당하는 화자의 것인지(규칙 7 다중 캐릭터 분리).</summary>
        private bool IsMine(DialogueIntent intent)
        {
            if (_machine == null) return !_requireBoundSpeaker; // 화자 미지정 = 단일 캐릭터 폴백(플래그로 차단 가능).
            return intent.OriginMachine == _machine;
        }

        // ==================== 프레임 갱신 ====================

        private void LateUpdate()
        {
            // 전체화면 게임 감지로 캐릭터가 숨겨졌으면 말풍선도 함께 사라져야 한다(비침해 원칙 2).
            if (_agent != null && _agent.IsSuspended && IsBubbleVisible)
            {
                HideImmediateInternal("전체화면 감지(Suspended)");
                return;
            }
            if (!IsBubbleVisible) return;

            float dt = Time.unscaledDeltaTime;
            float elapsed = Time.unscaledTime - _shownAtUnscaledTime;

            if (!_fadingOut)
            {
                float minVisible = _config != null ? _config.dialogueMinVisibleSeconds : 0.7f;
                float maxVisible = _config != null ? _config.dialogueMaxVisibleSeconds : 4f;

                // 정상 종료 대기분: 최소 노출 시간을 채우면 페이드아웃 시작(규칙 4의 "정상 진행 중"에만
                // 적용되는 최소 노출 시간).
                if (_expiredPendingFadeOut && elapsed >= minVisible) BeginFadeOut();
                // 상한: 상태가 아주 오래 지속돼도(예: Idle 6초) 말풍선이 화면에 눌러앉지 않게 한다.
                // 이 방향(더 일찍 사라짐)은 계약이 막는 실패 모드("행동보다 텍스트가 오래 남음")의
                // 반대편이라 안전하다.
                else if (!_expiredPendingFadeOut && maxVisible > 0f && elapsed >= maxVisible) BeginFadeOut();
            }

            if (_fadingOut)
            {
                _alpha -= dt / Mathf.Max(0.01f, FadeOutSeconds);
                if (_alpha <= 0f)
                {
                    // 정상 종료 경로의 제거도 로그로 남긴다 — 표시/제거가 항상 쌍으로 찍혀야
                    // "말풍선이 언제 사라졌는지"를 실행 로그만으로 재구성할 수 있다(빈도는 표시와
                    // 같으므로 로그가 늘어나는 양도 표시 로그와 동일하다).
                    HideImmediateInternal($"정상 종료 페이드아웃 완료, 노출 {(Time.unscaledTime - _shownAtUnscaledTime):F2}초");
                    return;
                }
            }
            else if (_alpha < 1f)
            {
                _alpha = Mathf.Min(1f, _alpha + dt / Mathf.Max(0.01f, FadeInSeconds));
            }

            UpdatePlacement();
            ApplyAlpha(_alpha);
        }

        private void BeginFadeOut()
        {
            if (_fadingOut) return;
            _fadingOut = true;
            _active = null; // 더 이상 새 만료 이벤트의 대상이 아니다(중복 처리 방지).
        }

        /// <summary>사라지는 애니메이션 없이 이 자리에서 즉시 제거(규칙 3(b)).</summary>
        private void HideImmediateInternal(string logReason)
        {
            _active = null;
            _expiredPendingFadeOut = false;
            _fadingOut = false;
            _alpha = 0f;
            ApplyAlpha(0f);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (logReason != null) Debug.Log($"[말풍선] 제거 — {logReason}, frame={Time.frameCount}");
            _activeText = null;
        }

        /// <summary>외부(테스트/긴급정지)에서 즉시 제거를 요청하는 공개 진입점.</summary>
        public void HideImmediate() => HideImmediateInternal("외부 요청");

        // ==================== 배치 (규칙 6) ====================

        private void UpdatePlacement()
        {
            if (_panel == null) return;
            if (_camera == null) _camera = ResolveCamera();
            if (_camera == null || _anchor == null) return;

            // 꼬리 끝이 가리키는 지점 = 머리 바로 위(월드 오프셋이라 줌/해상도가 바뀌어도 자동 추종).
            Vector3 tipWorld = _anchor.position + Vector3.up * HeadTopWorldOffset;
            Vector3 tip = _camera.WorldToScreenPoint(tipWorld);
            if (tip.z < 0f) return; // 카메라 뒤 — 배치 불가(직교 카메라에서는 사실상 발생하지 않음).

            Vector2 panelSize = _panel.sizeDelta;
            float screenW = Screen.width;
            float screenH = Screen.height;

            // 박스는 꼬리 위에 놓인다. 화면 위/아래로 넘치면 안쪽으로 민다.
            float panelBottom = tip.y + TailHeight - TailPanelOverlap;
            panelBottom = Mathf.Min(panelBottom, screenH - ScreenEdgeMargin - panelSize.y);
            panelBottom = Mathf.Max(panelBottom, ScreenEdgeMargin);

            // 규칙 6: "꼬리 방향을 유지한 채 박스만 안쪽으로" — 박스 x를 화면 안으로 클램프하고,
            // 꼬리는 캐릭터 x를 그대로 따라가되 박스 폭 안에 머물게만 한다.
            float half = panelSize.x * 0.5f;
            float panelCenterX = Mathf.Clamp(tip.x, ScreenEdgeMargin + half, screenW - ScreenEdgeMargin - half);
            float tailMinX = panelCenterX - half + TailWidth * 0.5f + BorderThickness + 2f;
            float tailMaxX = panelCenterX + half - TailWidth * 0.5f - BorderThickness - 2f;
            float tailCenterX = tailMaxX >= tailMinX ? Mathf.Clamp(tip.x, tailMinX, tailMaxX) : panelCenterX;

            _panel.anchoredPosition = new Vector2(panelCenterX, panelBottom);

            // 꼬리는 박스 바닥에서 아래로 뻗는다(TailPanelOverlap만큼 박스 안으로 파고들어 이음매를 지운다).
            var tailPos = new Vector2(tailCenterX, panelBottom + TailPanelOverlap);
            var tailSize = new Vector2(TailWidth, TailHeight + TailPanelOverlap);
            _tailOutline.anchoredPosition = tailPos;
            _tailOutline.sizeDelta = tailSize;
            _tailFill.anchoredPosition = tailPos;
            _tailFill.sizeDelta = tailSize;
        }

        private Camera ResolveCamera()
        {
            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.MainCamera != null)
                return _agent.Blackboard.MainCamera;
            return Camera.main;
        }

        private void ApplyAlpha(float a)
        {
            if (_group != null) _group.alpha = Mathf.Clamp01(a);
        }

        /// <summary>
        /// 지금 StickConfig에 설정된 잉크색/말풍선색을 네 개의 Image와 글자에 다시 입힌다.
        /// 캐릭터 선 색 프리셋 전환(Interaction/AppControlDirector.cs의 Ctrl+Opt+Cmd+C)은
        /// StickmanAgent.ApplyInkColorFromConfig()가 LineRenderer만 갱신하므로, 말풍선은 여기서
        /// 자기 몫을 따라간다 — 흰 캐릭터(어두운 배경)일 때 흰 말풍선에 흰 글씨가 되는 사고를 막는다.
        /// </summary>
        public void RefreshColors()
        {
            if (_panelOutlineImage == null) return;
            Color ink = _config != null ? _config.ResolveInkColor() : Color.black;
            Color bubble = _config != null ? _config.dialogueBubbleColor : Color.white;
            if (_config != null && _config.inkColor == StickmanInkColor.White) bubble = Color.black;

            _panelOutlineImage.color = ink;
            _tailOutlineImage.color = ink;
            _panelInnerImage.color = bubble;
            _tailFillImage.color = bubble;
            if (_label != null) _label.color = ink;
        }

        // ==================== UI 구성 (런타임 생성 — 씬/프리팹 수동 배선 불필요) ====================

        private void ApplyText(string text)
        {
            if (_label == null) return;
            _label.text = text ?? string.Empty;

            // 줄바꿈을 감안한 실제 크기 계산. CanvasScaler를 붙이지 않아 scaleFactor는 1이지만,
            // 나중에 누가 스케일러를 붙여도 조용히 깨지지 않도록 명시적으로 나눠준다.
            TextGenerationSettings settings = _label.GetGenerationSettings(new Vector2(MaxTextWidth, 0f));
            float scale = settings.scaleFactor > 0f ? settings.scaleFactor : 1f;
            TextGenerator gen = _label.cachedTextGeneratorForLayout;
            float textW = Mathf.Min(MaxTextWidth, gen.GetPreferredWidth(_label.text, settings) / scale);
            settings = _label.GetGenerationSettings(new Vector2(textW, 0f));
            float textH = gen.GetPreferredHeight(_label.text, settings) / scale;

            float inset = (BorderThickness + TextPadding) * 2f;
            _panel.sizeDelta = new Vector2(Mathf.Ceil(textW + inset), Mathf.Ceil(textH + inset));
        }

        private void BuildUi()
        {
            Color ink = _config != null ? _config.ResolveInkColor() : Color.black;
            Color bubble = _config != null ? _config.dialogueBubbleColor : Color.white;
            // 캐릭터 잉크가 흰색 프리셋이면 말풍선도 반전해야 보인다(어두운 배경 + 흰 캐릭터).
            if (_config != null && _config.inkColor == StickmanInkColor.White) bubble = Color.black;
            int fontSize = _config != null ? Mathf.Max(8, _config.dialogueFontSize) : 16;

            // 캔버스는 **씬 루트에** 만든다(부모 없음). ScreenSpaceOverlay 캔버스를 움직이는 캐릭터의
            // 자식으로 두면 RAGDOLL로 루트가 회전/이동할 때 부모 변환이 섞여 들어갈 수 있다.
            // Interaction/AppControlDirector.cs도 정확히 같은 이유로 SetParent(null)을 쓴다(그쪽은 실제
            // 실행에서 검증된 유일한 uGUI 경로다). 위치는 매 프레임 UpdatePlacement()가 스크린 좌표로
            // 직접 계산하므로 부모가 없어도 캐릭터를 정확히 따라간다.
            var canvasGo = new GameObject("DialogueBubbleCanvas (" + gameObject.name + ")", typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderBubble;
            _group = canvasGo.GetComponent<CanvasGroup>();
            // 말풍선은 순수 관전용 표시물이다 — 클릭을 절대 가로채지 않는다(비침해 원칙 2).
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // 그리는 순서(뒤 -> 앞): 꼬리 테두리 / 박스 테두리 / 박스 안쪽 / 꼬리 채움 / 글자.
            // 꼬리 채움이 박스 아래 테두리 위에 와야 꼬리와 박스가 하나로 이어져 보인다.
            _tailOutline = CreateTailPart(canvasGo.transform, "TailOutline", ink, filled: true, out _tailOutlineImage);
            _panel = CreatePanel(canvasGo.transform, ink, bubble);
            _tailFill = CreateTailPart(canvasGo.transform, "TailFill", bubble, filled: false, out _tailFillImage);

            // 글자는 박스의 자식이라 박스를 옮기면 함께 따라온다.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(_panel, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            float pad = BorderThickness + TextPadding;
            labelRect.offsetMin = new Vector2(pad, pad);
            labelRect.offsetMax = new Vector2(-pad, -pad);
            _label = labelGo.GetComponent<Text>();
            _label.font = ResolveKoreanFont(fontSize);
            _label.fontSize = fontSize;
            _label.fontStyle = FontStyle.Bold; // 캐릭터의 굵은 획과 같은 문법 + 작은 화면에서의 가독성.
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = ink;
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
        }

        private RectTransform CreatePanel(Transform parent, Color ink, Color bubble)
        {
            var go = new GameObject("BubblePanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f); // 바닥 중앙 기준 — 꼬리와 이어 붙이기 쉬운 기준점.
            rect.sizeDelta = new Vector2(80f, 30f);
            _panelOutlineImage = go.GetComponent<Image>();
            _panelOutlineImage.color = ink;
            _panelOutlineImage.raycastTarget = false;

            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(go.transform, false);
            var innerRect = innerGo.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(BorderThickness, BorderThickness);
            innerRect.offsetMax = new Vector2(-BorderThickness, -BorderThickness);
            _panelInnerImage = innerGo.GetComponent<Image>();
            _panelInnerImage.color = bubble;
            _panelInnerImage.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateTailPart(Transform parent, string name, Color color, bool filled, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 1f); // 위쪽 중앙 기준 — 박스 바닥에 매달린다.
            rect.sizeDelta = new Vector2(TailWidth, TailHeight);
            image = go.GetComponent<Image>();
            image.sprite = filled ? GetTailOutlineSprite() : GetTailFillSprite();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        // ==================== 꼬리 스프라이트 (런타임 생성 — 텍스처 에셋 없이) ====================
        //
        // 이 프로젝트에는 스프라이트 에셋이 하나도 없다(캐릭터조차 LineRenderer로 그린다). 삼각형
        // 꼬리는 사각형 Image 조합으로는 깔끔하게 나오지 않으므로, 알파 커버리지를 담은 작은 텍스처
        // 두 장을 코드로 만들어 쓴다. 색은 Image.color로 입히므로(텍스처는 흰색 마스크) 잉크색 프리셋
        // 전환이 그대로 반영된다.
        private static Sprite _tailOutlineSprite;
        private static Sprite _tailFillSprite;

        private static Sprite GetTailOutlineSprite()
        {
            if (_tailOutlineSprite == null) _tailOutlineSprite = BuildTriangleSprite(0f, "StickMateTailOutline");
            return _tailOutlineSprite;
        }

        private static Sprite GetTailFillSprite()
        {
            // 안쪽 채움은 두 빗변에서 테두리 두께만큼 안으로 들어간 삼각형(윗변은 줄이지 않는다 —
            // 그래야 박스 안쪽 흰 면과 이음매 없이 이어진다).
            if (_tailFillSprite == null)
            {
                float texBorder = BorderThickness * (TriangleTexWidth / TailWidth);
                _tailFillSprite = BuildTriangleSprite(texBorder, "StickMateTailFill");
            }
            return _tailFillSprite;
        }

        private const int TriangleTexWidth = 96;
        private const int TriangleTexHeight = 72;

        /// <summary>
        /// 아래로 뾰족한 삼각형의 알파 마스크 텍스처를 만든다. inset&gt;0이면 두 빗변에서 그만큼 안으로
        /// 들어간(윗변은 그대로인) 작은 삼각형이 된다. 경계는 1픽셀 안티에일리어싱되어 투명 오버레이
        /// 창에서도 계단이 보이지 않는다.
        /// </summary>
        private static Sprite BuildTriangleSprite(float inset, string name)
        {
            const int w = TriangleTexWidth;
            const int h = TriangleTexHeight;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            // 삼각형 꼭짓점: 위 좌우 모서리와 아래 중앙 꼭짓점(텍스처 좌표, y가 위쪽).
            var topLeft = new Vector2(0f, h);
            var topRight = new Vector2(w, h);
            var apex = new Vector2(w * 0.5f, 0f);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // 각 빗변 안쪽까지의 거리(양수 = 삼각형 안쪽). 왼쪽 빗변은 topLeft->apex,
                    // 오른쪽 빗변은 apex->topRight로 방향을 잡아 안쪽이 항상 왼편이 되게 한다.
                    float dLeft = SignedDistance(p, topLeft, apex);
                    float dRight = SignedDistance(p, apex, topRight);
                    float d = Mathf.Min(dLeft, dRight) - inset;
                    float coverage = Mathf.Clamp01(d + 0.5f); // 1픽셀 폭 안티에일리어싱.
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>선분 a->b 기준 부호 있는 거리. 진행 방향의 **왼쪽**이 양수다.</summary>
        private static float SignedDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.magnitude;
            if (len < 1e-5f) return 0f;
            Vector2 n = new Vector2(-ab.y, ab.x) / len; // 왼쪽 법선
            return Vector2.Dot(p - a, n);
        }

        // ==================== 한글 폰트 해석 ====================

        private static Font _cachedFont;
        private static bool _koreanGlyphVerified;

        /// <summary>
        /// 한글이 **실제로 그려지는** 폰트를 고른다. Unity 내장 LegacyRuntime.ttf(Arial 계열)는 한글
        /// 글리프가 없어 네모(두부)로 나오므로, OS 설치 폰트를 후보 순서대로 만들어 보고
        /// RequestCharactersInTexture -> GetCharacterInfo로 "한" 글자의 글리프 폭이 실제로 잡히는지를
        /// 실측해 첫 성공 폰트를 쓴다(이름만 보고 믿지 않는다 — 설치 여부/이름 표기가 OS마다 다르다).
        /// 전부 실패하면 내장 폰트로 폴백하고 경고를 남긴다(앱이 죽지는 않는다).
        /// </summary>
        private static Font ResolveKoreanFont(int size)
        {
            if (_cachedFont != null) return _cachedFont;

            var candidates = new List<string>
            {
                // macOS 기본 한글 폰트
                "Apple SD Gothic Neo", "AppleSDGothicNeo-Regular", "AppleGothic", "AppleMyungjo",
                // Windows 기본 한글 폰트
                "Malgun Gothic", "맑은 고딕", "Gulim", "Dotum", "Batang",
                // 흔히 설치되는 무료 한글 폰트
                "NanumGothic", "Nanum Gothic", "NanumBarunGothic",
                // CJK 전반을 담는 범용 폰트
                "PingFang SC", "Hiragino Sans", "Arial Unicode MS",
            };

            // 설치 목록을 훑어 이름에 한글 계열 키워드가 든 폰트도 후보 뒤에 붙인다(이름 표기가 달라
            // 위 목록이 전부 빗나가는 환경 대비).
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                if (installed != null)
                {
                    for (int i = 0; i < installed.Length; i++)
                    {
                        string n = installed[i];
                        if (string.IsNullOrEmpty(n)) continue;
                        if (n.IndexOf("Gothic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Nanum", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Myungjo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("PingFang", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Hiragino", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!candidates.Contains(n)) candidates.Add(n);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[말풍선] OS 폰트 목록 조회 실패(무시하고 후보 목록만 사용): " + e.Message);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Font f = TryCreateFont(candidates[i], size);
                if (f == null) continue;
                if (!CanRenderKorean(f, size)) continue;
                _cachedFont = f;
                _koreanGlyphVerified = true;
                Debug.Log($"[말풍선] 한글 폰트 확정: '{candidates[i]}' (글리프 실측 통과).");
                return _cachedFont;
            }

            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _koreanGlyphVerified = false;
            Debug.LogWarning("[말풍선] 한글을 렌더링할 수 있는 OS 폰트를 찾지 못해 내장 폰트로 폴백합니다 — " +
                             "말풍선의 한글이 네모로 보일 수 있습니다.");
            return _cachedFont;
        }

        private static Font TryCreateFont(string name, int size)
        {
            try { return Font.CreateDynamicFontFromOSFont(name, size); }
            catch { return null; }
        }

        /// <summary>"한글" 글자의 글리프가 실제로 잡히는지 실측한다(이름만 보고 믿지 않는다).</summary>
        private static bool CanRenderKorean(Font font, int size)
        {
            try
            {
                font.RequestCharactersInTexture("한글", size, FontStyle.Bold);
                if (!font.GetCharacterInfo('한', out CharacterInfo info, size, FontStyle.Bold)) return false;
                return info.glyphWidth > 0 && info.advance > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
