using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 설정창 <b>부품 4종 + 행/카드 레이아웃</b> — docs/UX_FLOW.md 35-1-5, 35-1-9 P0-b.
    /// 2026-09-01 사용자 승인 시안(720×560)을 그대로 코드로 옮긴 것이다.
    ///
    /// ============================================================================
    /// 왜 <see cref="UiChrome"/>가 아니라 별도 파일인가
    /// ============================================================================
    /// <see cref="UiChrome"/>는 <b>색/여백/둥근 모서리 스프라이트</b>의 창고다(토큰 + 원시 부품).
    /// 여기 있는 것은 그보다 한 층 위인 <b>설정 행(row)의 문법</b>이다 — "라벨은 왼쪽, 컨트롤은
    /// 오른쪽 끝, 캡션은 라벨 밑, 행 사이에는 1pt 구분선". 이 문법은 설정창 밖에서는 쓰이지 않으므로
    /// 토큰 창고에 섞지 않는다. <b>색은 한 개도 새로 만들지 않았다</b> — 전부 UiChrome 토큰이거나
    /// 토큰 두 개를 <see cref="UiChrome.Flatten"/>으로 합성한 값이다.
    ///
    /// ============================================================================
    /// ★★ 알파 규칙 — 이 파일에서 가장 중요한 제약
    /// ============================================================================
    /// 이 앱의 창 뒤에는 우리 콘텐츠가 아니라 <b>유저의 다른 창</b>이 있고, 투명 오버레이의
    /// 프레임버퍼 알파가 곧 OS 합성 마스크다. uGUI 기본 셰이더는 알파 채널에도
    /// <c>Blend SrcAlpha OneMinusSrcAlpha</c>를 적용하므로 <b>반투명 겹을 얹을 때마다 창 알파가
    /// 내려간다</b>(dstA' = srcA² + dstA(1−srcA)). 그래서 이 파일의 모든 그래픽은
    ///   (a) α=1인 토큰을 쓰거나,
    ///   (b) 반투명 토큰이면 <b>바로 밑에 깔린 불투명색 위에 미리 합성</b>(<see cref="UiChrome.Flatten"/>)한다.
    /// 2026-08-31에 정보창/팝오버가 이 규칙을 어겨 데스크톱이 40% 비쳐 보였다
    /// (<c>InfoWindowPanelOpacityTests</c> / <c>PopoverAndHoverPanelOpacityTests</c>가 잠근 규칙).
    /// <b>예외는 하나</b>: 완전 투명(α=0)한 히트 영역은 프레임버퍼를 건드리지 않으므로 안전하다
    /// (srcA=0 → dstA' = dstA).
    ///
    /// ============================================================================
    /// 클릭 경로 — 창과 <b>같은</b> 3중 관례
    /// ============================================================================
    /// 부품은 자기 클릭을 스스로 듣지 않는다. <see cref="SettingsControlHost"/>에 히트 사각형과
    /// 동작을 등록해 두고, (1) uGUI <see cref="Button"/>과 (2) 창의 전역 커서 폴링이 <b>같은
    /// 동작</b>을 부른다. (3) 중복 제거는 창의 <c>TryClaimAction</c> 하나가 맡는다
    /// (<see cref="PopoverPanel"/>/<see cref="CharacterInfoWindow"/>와 같은 구조).
    /// </summary>
    public static class SettingsControls
    {
        // ==================== 치수 (시안 그대로) ====================

        /// <summary>그룹 카드 폭(창 720 − 좌우 패딩 20×2).</summary>
        public const float CardWidth = 680f;

        /// <summary>카드 안쪽 좌우 여백.</summary>
        public const float CardPadX = 14f;

        /// <summary>카드 제목 줄(11pt 볼드 Accent)이 차지하는 높이.</summary>
        public const float CardTitleHeight = 26f;

        /// <summary>카드 아래쪽 여백 + 카드 사이 간격.</summary>
        public const float CardBottomPad = 4f;
        public const float CardGap = 14f;

        /// <summary>행 높이 — 35-1-5가 확정한 값. 44는 32-1이 정한 최소 클릭 타깃이기도 하다.</summary>
        public const float RowHeight = 44f;

        /// <summary>캡션이 붙은 행. 라벨 줄 + 3pt 간격 + 10pt 캡션이 들어간다.</summary>
        public const float RowHeightWithCaption = 60f;

        public const float SwitchWidth = 38f;
        public const float SwitchHeight = 22f;
        public const float SwitchKnob = 18f;
        public const float TrackWidth = 96f;
        public const float TrackHeight = 5f;
        public const float StepButton = 20f;
        public const float ValueLabelWidth = 44f;
        public const float ControlGap = 8f;
        public const float SwatchSize = 22f;
        public const float SwatchGap = 6f;
        public const float ButtonHeight = 24f;
        public const float SegmentHeight = 22f;

        // ==================== 색 (전부 UiChrome 토큰 or 그 합성) ====================
        //
        // 카드 위에 얹히는 것은 CardSurface에, 창 바탕에 직접 얹히는 것은 PanelSurface에 합성한다.
        // 밑에 깔린 색이 다르면 같은 토큰이라도 합성 결과가 달라야 한다 — 그래서 두 벌이다.

        public static Color CardBorderOnCard => UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface);
        public static Color DividerOnCard => UiChrome.Flatten(UiChrome.Divider, UiChrome.CardSurface);
        public static Color DividerOnPanel => UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface);
        public static Color TrackOnCard => UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.CardSurface);
        public static Color OutlineOnCard => UiChrome.Flatten(UiChrome.PanelBorder, UiChrome.CardSurface);

        /// <summary>카드보다 <b>밝은</b> 버튼 표면. 시안의 <c>rgba(255,255,255,0.06)</c> 자리인데,
        /// 새 색을 만들지 않으려고 기존 토큰(CardBorder = 흰색 α0.10)을 카드 위에 합성해 만든다.</summary>
        public static Color ButtonSurfaceOnCard => UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface);

        /// <summary>선택된 세그먼트/스위치가 켜졌을 때의 강조 면. Accent는 α=1이라 합성이 필요 없다.</summary>
        public static Color AccentSolid => UiChrome.Accent;

        // ==================== 작은 도구 ====================

        /// <summary>부모의 <b>오른쪽 위</b>를 원점으로 배치한다(x는 왼쪽으로 갈수록 음수).
        /// 설정 행의 컨트롤은 전부 오른쪽 정렬이라 이 배치가 기본형이다 — 값이 세로 한 줄로 정렬돼야
        /// "무엇이 켜져 있나"를 훑을 수 있다(35-1-5의 정렬 규칙).</summary>
        public static void PlaceTopRight(RectTransform rt, float xFromRight, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-xFromRight, y);
        }

        /// <summary>클릭만 받고 <b>아무것도 그리지 않는</b> 판(α=0). 스위치처럼 작은 부품의 히트 영역을
        /// 손가락 크기로 넓힐 때 쓴다. α=0은 프레임버퍼 알파를 건드리지 않아 창 알파에 안전하다.</summary>
        public static Image AddHitArea(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            return image;
        }

        // ==================== 비활성 사유의 어휘 (K1 전용) ====================

        /// <summary>
        /// <b>아직 안 만들어짐</b>(<see cref="DisabledKind.NotBuilt"/>)을 가리키는 한 어절.
        ///
        /// <para>★ 설정창의 <b>탭바 배지</b>와 <b>행 캡션 접두사</b>가 이 한 자리를 같이 쓴다 — 유저는
        /// 탭에서 이 단어를 보고, 눌러서 같은 단어로 시작하는 문장을 읽는다. 두 자리가 각자 문자열을
        /// 들고 있으면 한쪽만 바뀌는 날이 오고, 그날 앱은 같은 사실을 두 어휘로 말한다.</para>
        /// </summary>
        public const string NotBuiltWord = "준비 중";

        /// <summary>K1 캡션의 접두사. <b>이 문자열은 앱 전체에서 여기 한 자리에만 있다</b> —
        /// 접두사 우회로를 뚫는 순간 "준비 중" 어법이 두 벌이 된다.</summary>
        public const string NotBuiltPrefix = NotBuiltWord + " — ";
    }

    // ================================================================================
    // 부품 0 — 비활성 사유는 <b>두 종류</b>다
    // ================================================================================

    /// <summary>
    /// 이 행을 왜 지금 못 만지는가 — 두 종류이고, <b>유저가 해야 할 일이 정반대다</b>
    /// (docs/UX_FLOW.md 43-2).
    /// </summary>
    public enum DisabledKind
    {
        /// <summary>앱이 그 기능을 <b>아직 갖고 있지 않다</b>. 유저의 올바른 행동은 <b>찾기를 멈추는 것</b>
        /// → 캡션에 <see cref="SettingsControls.NotBuiltPrefix"/>가 붙고, 문장은 <b>"언제 오는가"</b>를
        /// 말한다.</summary>
        NotBuilt = 0,

        /// <summary>기능은 <b>이 빌드에 들어 있다</b>. 조건만 만족시키면 지금 켜진다 → 접두사를 붙이지
        /// <b>않는다</b>. "준비 중"이라고 말하면 <b>한 번만 누르면 되는 일을 기다리게</b> 만드는 거짓말이고
        /// 그건 원칙 1의 정면 위반이다. 문장은 <b>"지금 무엇을 하면 되는가"</b>를 말한다.</summary>
        ConditionUnmet = 1,
    }

    /// <summary>
    /// 비활성 사유 한 줄 + <b>그 종류</b>. 둘을 한 값으로 묶은 것이 이 타입의 전부이고, 그 이유가
    /// 이 타입이 존재하는 이유다.
    ///
    /// <para><b>2026-09-02 이전</b>: <c>disabledNote</c>는 <b>문장만</b> 받았다. 접두사는 그 채널에
    /// K1만 온다고 <b>가정</b>했고, 실제로 콜사이트 5곳이 전부 K1이라 <b>배선의 우연으로</b> 맞았다.
    /// 다음 사람이 <c>disabledNote: "지금 붙잡을 만한 작은 창이 없어요"</c>(K2)를 여기로 흘리면
    /// 화면에는 <c>준비 중 — 지금 붙잡을 만한 작은 창이 없어요.</c>가 뜨고, <b>컴파일·리뷰·카피 테스트를
    /// 전부 통과한다</b>(문장 자체는 정상 유저 어휘다). 시간 문제이지 가능성 문제가 아니었다.</para>
    ///
    /// <para>★ 그래서 <b>기본값을 두지 않는다</b>: 문장을 주려면 종류를 반드시 골라야 하고, 고르는 자리는
    /// 문장을 쓰는 그 자리다. 종류를 <c>ComposeCaption</c>의 선택적 인자로 두면 다음 사람은 그것을
    /// <b>안 적을 수 있고</b>, 안 적히는 순간 같은 사고가 그대로 돌아온다.</para>
    ///
    /// <para><see cref="SettingsRowGate"/>는 이 타입을 쓰지 않는다 — 그 클래스는 <b>구조적으로</b>
    /// 언제나 <see cref="DisabledKind.ConditionUnmet"/>이다(런타임 조건으로 켜고 끄는 것이 존재 이유다).</para>
    /// </summary>
    public readonly struct DisabledReason
    {
        public readonly DisabledKind Kind;

        /// <summary>사용자가 읽을 문장. <b>내부 식별자를 넣지 않는다</b>(SettingsUserFacingCopyTests).</summary>
        public readonly string Text;

        private DisabledReason(DisabledKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        /// <summary>아직 안 만들어짐. 문장은 <b>"언제 오는가"</b>로 끝난다.</summary>
        public static DisabledReason NotBuilt(string text) => new DisabledReason(DisabledKind.NotBuilt, text);

        /// <summary>지금 조건이 안 맞음. 문장은 <b>"지금 무엇을 하면 되는가"</b>로 끝난다.</summary>
        public static DisabledReason ConditionUnmet(string text) => new DisabledReason(DisabledKind.ConditionUnmet, text);

        public bool HasText => !string.IsNullOrEmpty(Text);
    }

    /// <summary>
    /// 설정창 안의 <b>클릭 가능한 것 전부</b>를 한 곳에 모아 두는 등록소. 창은 커서 좌표 하나만 주고,
    /// 어떤 부품이 눌렸는지는 여기가 판정한다.
    ///
    /// <para>왜 부품마다 자기 클릭을 듣게 하지 않는가: 이 앱은 클릭관통 오버레이라 <b>uGUI 이벤트가
    /// 도착하지 않는 경우</b>(비활성 앱의 첫 클릭)가 정상 경로에 포함된다. 그래서 전역 폴링 히트테스트가
    /// 반드시 필요하고, 그 판정을 부품마다 흩뿌리면 "보이는데 안 눌리는" 부품이 반드시 하나 생긴다.</para>
    /// </summary>
    public sealed class SettingsControlHost
    {
        private struct Entry
        {
            public RectTransform Rect;
            public string Key;
            public Action Click;
            public Action<Vector2> Drag;
            public Func<bool> Interactable;
        }

        private readonly List<Entry> _entries = new List<Entry>(64);

        /// <summary>중복 제거 — 같은 클릭을 uGUI와 전역 폴링이 둘 다 처리하지 않게. 창의
        /// <c>TryClaimAction</c>을 그대로 꽂는다.</summary>
        public Func<string, bool> Claim;

        /// <summary>마스크(<see cref="RectMask2D"/>)까지 반영하는 히트테스트. 창이 제공한다 —
        /// "잘려서 안 보이는 자리는 눌리지 않는다"는 R2 M3의 규칙을 이 창에서도 그대로 지킨다.</summary>
        public Func<RectTransform, Vector2, bool> HitTest;

        public int Count => _entries.Count;

        /// <param name="target">uGUI <see cref="Button"/>을 붙일 그래픽. null이면 전역 폴링 경로만 쓴다.</param>
        /// <param name="drag">누른 채 끌 때 매 프레임 호출(슬라이더 전용). null이면 드래그 없음.</param>
        /// <param name="interactable">지금 조작 가능한가(회색 처리된 행은 false). null이면 항상 가능.</param>
        public void Register(RectTransform rect, Graphic target, string key, Action click,
            Action<Vector2> drag = null, Func<bool> interactable = null)
        {
            if (rect == null || click == null) return;
            _entries.Add(new Entry
            {
                Rect = rect,
                Key = key,
                Click = click,
                Drag = drag,
                Interactable = interactable,
            });

            if (target == null) return;
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            string capturedKey = key;
            Action capturedClick = click;
            Func<bool> capturedGate = interactable;
            button.onClick.AddListener(() =>
            {
                if (capturedGate != null && !capturedGate()) return;
                if (Claim != null && !Claim(capturedKey)) return;
                capturedClick();
            });
        }

        /// <summary>이 좌표에서 눌린 부품을 실행한다. 드래그가 있는 부품이면 그 인덱스를 돌려준다
        /// (창이 버튼을 뗄 때까지 <see cref="DragTo"/>로 이어 준다).</summary>
        public bool TryClick(Vector2 cursor, out int dragIndex)
        {
            dragIndex = -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                if (e.Rect == null || !e.Rect.gameObject.activeInHierarchy) continue;
                if (e.Interactable != null && !e.Interactable()) continue;
                if (HitTest != null ? !HitTest(e.Rect, cursor) : !ContainsScreenPoint(e.Rect, cursor)) continue;

                if (Claim != null && !Claim(e.Key)) return true;   // 방금 uGUI가 처리했다 — 소비는 됐다.
                if (e.Drag != null)
                {
                    dragIndex = i;
                    e.Drag(cursor);   // 누른 그 지점의 값으로 즉시 반응한다(트랙 클릭 = 그 자리로 이동).
                    return true;
                }
                e.Click();
                return true;
            }
            return false;
        }

        public void DragTo(int index, Vector2 cursor)
        {
            if (index < 0 || index >= _entries.Count) return;
            Entry e = _entries[index];
            if (e.Drag == null) return;
            if (e.Interactable != null && !e.Interactable()) return;
            e.Drag(cursor);
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀이다.</summary>
        public static bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(Corners);
            return screenPoint.x >= Corners[0].x && screenPoint.x <= Corners[2].x &&
                   screenPoint.y >= Corners[0].y && screenPoint.y <= Corners[2].y;
        }

        /// <summary>화면 사각형(테스트가 "이 부품을 누른다"를 좌표로 손으로 적지 않게 하는 창구).</summary>
        public static Rect ScreenRectOf(RectTransform rt)
        {
            if (rt == null) return new Rect();
            rt.GetWorldCorners(Corners);
            return Rect.MinMaxRect(Corners[0].x, Corners[0].y, Corners[2].x, Corners[2].y);
        }
    }

    // ================================================================================
    // 부품 1 — 토글 스위치
    // ================================================================================

    /// <summary>on/off 스위치 하나. 38×22 트랙 + 18 지름 손잡이(시안 그대로).</summary>
    public sealed class SettingsToggle
    {
        public RectTransform HitRect;
        public Image Track;
        public RectTransform Knob;
        public bool On { get; private set; }
        public bool Interactable = true;

        public void SetOn(bool on)
        {
            if (On == on) return;
            On = on;
            Apply();
        }

        /// <summary>값이 실제로 바뀐 프레임에만 색/위치를 다시 쓴다(하루 종일 켜져 있는 앱).</summary>
        public void Apply()
        {
            if (Track != null)
            {
                Track.color = !Interactable
                    ? SettingsControls.DividerOnCard
                    : On ? SettingsControls.AccentSolid : SettingsControls.TrackOnCard;
            }
            if (Knob != null)
            {
                float inset = (SettingsControls.SwitchHeight - SettingsControls.SwitchKnob) * 0.5f;
                float x = On ? SettingsControls.SwitchWidth - SettingsControls.SwitchKnob - inset : inset;
                Knob.anchoredPosition = new Vector2(x, -inset);
            }
        }
    }

    // ================================================================================
    // 부품 2 — 슬라이더 (스테퍼 [−][+] 포함)
    // ================================================================================

    /// <summary>
    /// [−] [트랙 96×5] [+] [값] 한 벌. 시안의 슬라이더는 <b>스테퍼가 붙은 슬라이더</b>라 두 부품을
    /// 따로 만들지 않고 하나로 조립한다(따로 두면 같은 값에 대한 반올림 규칙이 두 벌이 된다).
    ///
    /// <para>값은 항상 <see cref="Step"/> 격자에 스냅된다 — 트랙을 아무 데나 눌러도, [+]를 눌러도
    /// 같은 격자 위에 떨어진다. 표시 숫자와 실제 값이 갈라질 수 있는 경로를 없애기 위해서다(원칙 1).</para>
    /// </summary>
    public sealed class SettingsSlider
    {
        public RectTransform TrackHitRect;
        public RectTransform FillRect;
        public Image FillImage;
        public RectTransform MinusRect;
        public RectTransform PlusRect;
        public Text ValueLabel;

        /// <summary>지금 조작할 수 있는가. ★ 만들 때 정해지고 끝나는 값이 아니다 — 다른 컨트롤이
        /// 이 행을 무효로 만들 수 있다(<see cref="SettingsRowGate"/>).</summary>
        public bool Interactable = true;

        public float Min;
        public float Max;
        public float Step;
        public Func<float, string> Format;
        public Action<float> Changed;

        public float Value { get; private set; }

        /// <summary>격자에 스냅하고 범위로 clamp한다.</summary>
        public float Snap(float v)
        {
            if (float.IsNaN(v)) return Min;
            float clamped = Mathf.Clamp(v, Min, Max);
            if (Step <= 0f) return clamped;
            int steps = Mathf.RoundToInt((clamped - Min) / Step);
            return Mathf.Clamp(Min + steps * Step, Min, Max);
        }

        /// <summary>값을 넣는다(콜백 없음 — 외부 모델이 바뀌어 화면만 따라가는 경우).</summary>
        public void SetValueSilently(float v)
        {
            float next = Snap(v);
            if (Mathf.Approximately(next, Value) && ValueLabel != null && !string.IsNullOrEmpty(ValueLabel.text)) return;
            Value = next;
            Apply();
        }

        /// <summary>사용자 조작 — 값이 실제로 달라졌을 때만 콜백을 부른다.</summary>
        public void SetValueFromUser(float v)
        {
            float next = Snap(v);
            if (Mathf.Approximately(next, Value)) return;
            Value = next;
            Apply();
            Changed?.Invoke(next);
        }

        public void Nudge(int direction) => SetValueFromUser(Value + direction * Step);

        /// <summary>트랙 위 화면 좌표 → 값. 트랙의 실제 화면 사각형에서 역산하므로 배율/DPI가 바뀌어도
        /// "누른 자리"와 "결과"가 어긋나지 않는다.</summary>
        public void SetFromTrackPoint(Vector2 screenPoint)
        {
            if (TrackHitRect == null) return;
            Rect r = SettingsControlHost.ScreenRectOf(TrackHitRect);
            if (r.width <= 0.001f) return;
            float t = Mathf.Clamp01((screenPoint.x - r.xMin) / r.width);
            SetValueFromUser(Mathf.Lerp(Min, Max, t));
        }

        public void Apply()
        {
            if (FillRect != null)
            {
                float t = Max > Min ? Mathf.Clamp01((Value - Min) / (Max - Min)) : 0f;
                FillRect.sizeDelta = new Vector2(SettingsControls.TrackWidth * t, SettingsControls.TrackHeight);
            }
            if (ValueLabel != null)
            {
                string text = Format != null ? Format(Value) : Value.ToString("0.00");
                if (ValueLabel.text != text) ValueLabel.text = text;
            }
            if (FillImage != null)
            {
                Color fill = Interactable ? SettingsControls.AccentSolid : UiChrome.DisabledControlInk;
                if (FillImage.color != fill) FillImage.color = fill;
            }
        }
    }

    // ================================================================================
    // 부품 3 — 세그먼트
    // ================================================================================

    /// <summary>서로 배타적인 선택지 2~4개. 라디오 그룹의 시각형이다.</summary>
    public sealed class SettingsSegment
    {
        public RectTransform[] Rects;
        public Image[] Surfaces;
        public Image[] Outlines;
        public Text[] Labels;
        public Action<int> Changed;
        public bool Interactable = true;

        public int Index { get; private set; }

        public void SetIndexSilently(int index)
        {
            Index = Mathf.Clamp(index, 0, (Rects != null ? Rects.Length : 1) - 1);
            Apply();
        }

        public void SetIndexFromUser(int index)
        {
            int next = Mathf.Clamp(index, 0, (Rects != null ? Rects.Length : 1) - 1);
            if (next == Index) return;
            Index = next;
            Apply();
            Changed?.Invoke(next);
        }

        public void Apply()
        {
            if (Surfaces == null) return;
            for (int i = 0; i < Surfaces.Length; i++)
            {
                bool active = i == Index;
                if (Surfaces[i] != null)
                {
                    Surfaces[i].color = active
                        ? SettingsControls.AccentSolid
                        : UiChrome.CardSurface;   // 카드와 같은 색 = "비어 있음"(시안의 transparent와 같은 그림).
                }
                if (Outlines[i] != null)
                {
                    Outlines[i].color = active
                        ? SettingsControls.AccentSolid
                        : SettingsControls.OutlineOnCard;
                }
                if (Labels[i] != null)
                {
                    Labels[i].color = !Interactable
                        ? UiChrome.InkTitle(false)
                        : active ? UiChrome.OnAccentSolid : UiChrome.TextSecondary;
                    Labels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }
    }

    // ================================================================================
    // 부품 4 — 스와치(색 견본)
    // ================================================================================

    public sealed class SettingsSwatchRow
    {
        public RectTransform[] Rects;
        public Image[] Borders;
        public Action<int> Changed;
        public bool Interactable = true;

        public int Index { get; private set; } = -1;

        public void SetIndexSilently(int index)
        {
            Index = index;
            Apply();
        }

        public void SetIndexFromUser(int index)
        {
            if (index == Index) return;
            Index = index;
            Apply();
            Changed?.Invoke(index);
        }

        public void Apply()
        {
            if (Borders == null) return;
            for (int i = 0; i < Borders.Length; i++)
            {
                if (Borders[i] == null) continue;
                Borders[i].color = i == Index
                    ? UiChrome.TextPrimary                 // 선택 = 흰 테두리(시안 그대로).
                    : SettingsControls.OutlineOnCard;
            }
        }
    }


    // ================================================================================
    // 부품 5 — 행 게이트(한 컨트롤이 다른 행들을 무효로 만들 때)
    // ================================================================================

    /// <summary>
    /// ★★ 2026-09-02(docs/UX_FLOW.md 42-11 판정 G) — <b>한 컨트롤이 켜져 있어야만 뜻을 갖는 행들</b>을
    /// 묶어 함께 비활성으로 내리는 손잡이.
    ///
    /// <para><b>왜 필요했나</b>: <c>말풍선 표시</c>를 끄면 대사가 그려지지 않는데
    /// <c>말풍선 글자 크기</c>·<c>대사 표시 시간</c>·<c>잡담 빈도</c> 세 행이 그대로 활성이었다.
    /// <b>컨트롤 셋이 움직이는데 화면에서 아무 일도 일어나지 않는다</b> — 42절이 고치는 그 병이
    /// 같은 카드 안에 세 배로 있었다.</para>
    ///
    /// <para>★ <b>행 높이를 실행 중에 바꾸지 않는다.</b> 카드는 만들 때 한 번 쌓이고 각 행의 좌표가
    /// 그때 확정되므로, 비활성 사유 한 줄이 나중에 생기면 그 아래 모든 행이 밀린다. 그래서 게이트에
    /// 묶인 행은 <b>처음부터 캡션 줄을 확보</b>한다(사유가 없을 때는 빈 줄). 자리를 미리 비워 두는
    /// 비용이 "설정을 만졌더니 카드가 출렁이는" 화면보다 싸다.</para>
    ///
    /// <para>★ 색은 직접 고르지 않는다 — 전부 <see cref="UiChrome.Ink"/> 사다리를 지난다.
    /// 비활성은 <b>한 단만</b> 내려가고, 사유 한 줄(<see cref="UiChrome.InkMeta"/>)은 어떤 상태에서도
    /// 흐려지지 않는다(그 줄이 비활성 행에서 가장 중요한 글자다).</para>
    /// </summary>
    public sealed class SettingsRowGate
    {
        private sealed class Row
        {
            public Text[] TitleInk;
            public Text[] BodyInk;
            public Text Caption;
            public string BaseCaption;
            public Action<bool> SetInteractable;
        }

        private readonly List<Row> _rows = new List<Row>(4);
        private readonly string _disabledNote;

        public bool Enabled { get; private set; } = true;

        /// <param name="disabledNote">왜 지금 못 만지는가. <b>사용자가 읽을 문장만</b> 담는다.
        /// <para>★ 이 문장은 <b>언제나</b> <see cref="DisabledKind.ConditionUnmet"/>이라 접두사를 타지
        /// 않는다(<c>Apply()</c>가 raw로 쓴다) — 게이트는 정의상 "지금 조건이 안 맞음"만 만든다.
        /// 그래서 "지금 무엇을 하면 되는가"로 끝나야 한다.</para></param>
        public SettingsRowGate(string disabledNote)
        {
            _disabledNote = disabledNote;
        }

        internal void Register(Text[] titleInk, Text[] bodyInk, Text caption, string baseCaption,
            Action<bool> setInteractable)
        {
            _rows.Add(new Row
            {
                TitleInk = titleInk,
                BodyInk = bodyInk,
                Caption = caption,
                BaseCaption = baseCaption ?? string.Empty,
                SetInteractable = setInteractable,
            });
        }

        /// <summary>사유 한 줄. 게이트에 묶인 행은 이 문장을 위해 캡션 줄을 미리 확보한다.</summary>
        internal string DisabledNote => _disabledNote;

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;
            Enabled = enabled;
            Apply();
        }

        /// <summary>값이 실제로 바뀐 때만 불린다(하루 종일 켜져 있는 앱 — 매 프레임 색을 다시 쓰지 않는다).</summary>
        public void Apply()
        {
            Color title = UiChrome.InkTitle(Enabled);
            Color body = UiChrome.InkBody(Enabled);

            for (int r = 0; r < _rows.Count; r++)
            {
                Row row = _rows[r];

                if (row.TitleInk != null)
                {
                    for (int i = 0; i < row.TitleInk.Length; i++)
                        if (row.TitleInk[i] != null) row.TitleInk[i].color = title;
                }
                if (row.BodyInk != null)
                {
                    for (int i = 0; i < row.BodyInk.Length; i++)
                        if (row.BodyInk[i] != null) row.BodyInk[i].color = body;
                }
                if (row.Caption != null)
                {
                    string text = Enabled ? row.BaseCaption : _disabledNote;
                    if (row.Caption.text != text) row.Caption.text = text;
                }
                row.SetInteractable?.Invoke(Enabled);
            }
        }
    }

    // ================================================================================
    // 카드 + 행 조립기
    // ================================================================================

    /// <summary>
    /// 그룹 카드 하나를 <b>위에서 아래로</b> 쌓는다. 행을 더할 때마다 커서(y)가 내려가고,
    /// <see cref="Finish"/>가 카드 높이를 확정한다 — 행 개수를 손으로 세어 높이를 적는 순간
    /// 행 하나가 추가/삭제될 때마다 조용히 어긋나기 때문이다.
    /// </summary>
    public sealed class SettingsCardBuilder
    {
        private readonly RectTransform _card;
        private readonly SettingsControlHost _host;
        private readonly List<Image> _dividers = new List<Image>(8);
        private float _y;
        private int _rowIndex;

        public RectTransform Card => _card;

        /// <param name="topY">부모(내용 영역) 좌상단 기준 y(아래로 갈수록 음수).</param>
        public SettingsCardBuilder(RectTransform parent, string title, float topY, SettingsControlHost host)
        {
            _host = host;

            Image surface = UiChrome.AddSurface(parent, "Card_" + title, UiChrome.CardSurface, UiChrome.RadiusCard);
            _card = surface.rectTransform;
            UiChrome.PlaceTopLeft(_card, 0f, topY, SettingsControls.CardWidth, SettingsControls.RowHeight);
            surface.raycastTarget = false;
            UiChrome.AddOutline(_card, "Outline", SettingsControls.CardBorderOnCard, UiChrome.RadiusCard);

            Text titleText = UiChrome.AddText(_card, "Title", UiChrome.FontLabel, TextAnchor.MiddleLeft,
                UiChrome.Accent, bold: true);
            UiChrome.PlaceTopLeft(titleText.rectTransform, SettingsControls.CardPadX, -8f,
                SettingsControls.CardWidth - SettingsControls.CardPadX * 2f, 16f);
            titleText.text = title;

            _y = -SettingsControls.CardTitleHeight;
        }

        /// <summary>카드 높이를 확정하고 <b>다음 카드의 topY</b>를 돌려준다.</summary>
        public float Finish(float topY)
        {
            float height = -_y + SettingsControls.CardBottomPad;
            _card.sizeDelta = new Vector2(SettingsControls.CardWidth, height);

            // 마지막 행 아래의 구분선은 지운다(시안의 `.row:last-child { border-bottom: none }`).
            if (_dividers.Count > 0)
            {
                Image last = _dividers[_dividers.Count - 1];
                if (last != null) last.gameObject.SetActive(false);
            }
            return topY - height - SettingsControls.CardGap;
        }

        // -------------------- 행 뼈대 --------------------

        private RectTransform BeginRow(string name, string label, string caption, string hotkey,
            bool enabled, out float rowHeight)
            => BeginRow(name, label, caption, hotkey, enabled, null, out rowHeight, out _, out _);

        /// <param name="gate">이 행을 나중에 통째로 비활성으로 내릴 수 있는 손잡이(없으면 null).
        /// 게이트가 붙으면 <b>사유 한 줄을 위한 캡션 자리를 미리 확보</b>한다 — 이유는
        /// <see cref="SettingsRowGate"/> 문서.</param>
        private RectTransform BeginRow(string name, string label, string caption, string hotkey,
            bool enabled, SettingsRowGate gate, out float rowHeight,
            out Text labelOut, out Text captionOut)
        {
            // 게이트에 묶인 행은 캡션이 없어도 캡션 높이로 잡는다 — 실행 중에 행 높이가 바뀌면
            // 그 아래 카드가 통째로 밀린다(SettingsRowGate 문서의 "출렁임" 문단).
            bool reserveCaption = gate != null;
            rowHeight = string.IsNullOrEmpty(caption) && !reserveCaption
                ? SettingsControls.RowHeight
                : SettingsControls.RowHeightWithCaption;

            var go = new GameObject("Row_" + name, typeof(RectTransform));
            go.transform.SetParent(_card, false);
            var row = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(row, SettingsControls.CardPadX, _y,
                SettingsControls.CardWidth - SettingsControls.CardPadX * 2f, rowHeight);

            float labelY = string.IsNullOrEmpty(caption) && !reserveCaption ? -(rowHeight - 16f) * 0.5f : -12f;
            // ★ 이 행이 §2.4의 실측 현장이다 — 옛 코드에서 제목 2.09 < 캡션 5.33으로 서열이
            //   뒤집혀 있었다. 유저는 "뭔가 준비 중이구나"만 읽고 "뭐가?"는 못 읽었다.
            Text labelText = UiChrome.AddText(row, "Label", UiChrome.FontBody, TextAnchor.MiddleLeft,
                UiChrome.InkTitle(enabled));
            UiChrome.PlaceTopLeft(labelText.rectTransform, 0f, labelY, 420f, 16f);
            labelText.text = label;

            if (!string.IsNullOrEmpty(hotkey))
            {
                Text hot = UiChrome.AddText(row, "Hotkey", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                    UiChrome.InkMeta);
                UiChrome.PlaceTopLeft(hot.rectTransform, labelText.preferredWidth + 8f, labelY, 120f, 14f);
                hot.text = hotkey;
            }

            captionOut = null;
            if (!string.IsNullOrEmpty(caption) || reserveCaption)
            {
                Text cap = UiChrome.AddText(row, "Caption", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                    UiChrome.InkMeta);
                UiChrome.PlaceTopLeft(cap.rectTransform, 0f, -31f, 480f, 14f);
                cap.text = enabled || gate == null ? (caption ?? string.Empty) : gate.DisabledNote;
                captionOut = cap;
            }
            labelOut = labelText;

            // 행 사이 1pt 구분선 — 마지막 행 것은 Finish()가 끈다.
            Image divider = UiChrome.AddSurface(_card, "Divider" + _rowIndex, SettingsControls.DividerOnCard, 2);
            UiChrome.PlaceTopLeft(divider.rectTransform, SettingsControls.CardPadX, _y - rowHeight,
                SettingsControls.CardWidth - SettingsControls.CardPadX * 2f, 1f);
            divider.raycastTarget = false;
            _dividers.Add(divider);

            _y -= rowHeight;
            _rowIndex++;
            return row;
        }

        // -------------------- 부품별 행 --------------------

        public SettingsToggle AddToggle(string key, string label, bool on, Action<bool> changed,
            string caption = null, string hotkey = null, bool enabled = true,
            DisabledReason disabledNote = default)
        {
            RectTransform row = BeginRow(key, label,
                ComposeCaption(caption, enabled, disabledNote.Text, disabledNote.Kind), hotkey,
                enabled, out float rowHeight);

            var toggle = new SettingsToggle { Interactable = enabled };

            Image track = UiChrome.AddSurface(row, "Track", SettingsControls.TrackOnCard,
                Mathf.RoundToInt(SettingsControls.SwitchHeight * 0.5f));
            SettingsControls.PlaceTopRight(track.rectTransform, 0f,
                -(rowHeight - SettingsControls.SwitchHeight) * 0.5f,
                SettingsControls.SwitchWidth, SettingsControls.SwitchHeight);
            toggle.Track = track;
            toggle.HitRect = track.rectTransform;

            // 손잡이는 <see cref="UiChrome.AddCircle"/>가 아니라 반지름 9짜리 둥근 사각형으로 만든다 —
            // AddCircle은 안티에일리어싱 램프만큼 상자를 부풀려서(diameter + feather×2) 18pt 정확한
            // 좌우 이동 거리를 계산할 수 없다. 18×18에 반지름 9면 결과는 완전한 원이다.
            Image knob = UiChrome.AddSurface(track.rectTransform, "Knob",
                enabled ? UiChrome.TextPrimary : UiChrome.DisabledControlInk,
                Mathf.RoundToInt(SettingsControls.SwitchKnob * 0.5f));
            knob.raycastTarget = false;
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = knob.rectTransform.pivot = new Vector2(0f, 1f);
            knob.rectTransform.sizeDelta = new Vector2(SettingsControls.SwitchKnob, SettingsControls.SwitchKnob);
            toggle.Knob = knob.rectTransform;

            toggle.SetOn(on);
            toggle.Apply();

            _host?.Register(track.rectTransform, track, key, () =>
            {
                toggle.SetOn(!toggle.On);
                changed?.Invoke(toggle.On);
            }, null, () => toggle.Interactable);

            return toggle;
        }

        public SettingsSlider AddSlider(string key, string label, float min, float max, float step,
            float value, Func<float, string> format, Action<float> changed,
            string caption = null, bool enabled = true, DisabledReason disabledNote = default,
            SettingsRowGate gate = null)
        {
            RectTransform row = BeginRow(key, label,
                ComposeCaption(caption, enabled, disabledNote.Text, disabledNote.Kind), null,
                enabled, gate, out float rowHeight, out Text labelText, out Text captionText);

            var slider = new SettingsSlider
            {
                Min = min,
                Max = max,
                Step = step,
                Format = format,
                Changed = changed,
                Interactable = enabled,
            };

            float centerY = -(rowHeight - SettingsControls.StepButton) * 0.5f;

            Text valueLabel = UiChrome.AddText(row, "Value", UiChrome.FontLabel, TextAnchor.MiddleRight,
                UiChrome.InkBody(enabled));
            SettingsControls.PlaceTopRight(valueLabel.rectTransform, 0f, centerY,
                SettingsControls.ValueLabelWidth, SettingsControls.StepButton);
            slider.ValueLabel = valueLabel;

            float plusX = SettingsControls.ValueLabelWidth + SettingsControls.ControlGap;
            Image plus = AddStepButton(row, "Plus", "+", plusX, centerY, enabled);
            slider.PlusRect = plus.rectTransform;

            float trackX = plusX + SettingsControls.StepButton + SettingsControls.ControlGap;
            // 트랙은 5pt라 그대로는 못 누른다 — 20pt 높이의 투명 히트 영역 안에 그린다.
            Image hit = SettingsControls.AddHitArea(row, "TrackHit");
            SettingsControls.PlaceTopRight(hit.rectTransform, trackX, centerY,
                SettingsControls.TrackWidth, SettingsControls.StepButton);
            slider.TrackHitRect = hit.rectTransform;

            Image trackBg = UiChrome.AddSurface(hit.rectTransform, "Track", SettingsControls.TrackOnCard, 3);
            trackBg.rectTransform.anchorMin = trackBg.rectTransform.anchorMax = trackBg.rectTransform.pivot = new Vector2(0f, 0.5f);
            trackBg.rectTransform.sizeDelta = new Vector2(SettingsControls.TrackWidth, SettingsControls.TrackHeight);
            trackBg.rectTransform.anchoredPosition = Vector2.zero;
            trackBg.raycastTarget = false;

            Image fill = UiChrome.AddSurface(trackBg.rectTransform, "Fill",
                enabled ? SettingsControls.AccentSolid : UiChrome.DisabledControlInk, 3);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.raycastTarget = false;
            slider.FillRect = fill.rectTransform;
            slider.FillImage = fill;

            float minusX = trackX + SettingsControls.TrackWidth + SettingsControls.ControlGap;
            Image minus = AddStepButton(row, "Minus", "−", minusX, centerY, enabled);
            slider.MinusRect = minus.rectTransform;

            slider.SetValueSilently(value);
            slider.Apply();

            // ★ 상수 캡처(() => enabled)가 아니라 부품의 현재 상태를 본다 — 안 그러면 게이트가 내려간
            //   뒤에도 클릭이 그대로 먹는다("회색인데 눌리는" 행).
            Func<bool> clickable = () => slider.Interactable;
            _host?.Register(minus.rectTransform, minus, key + ".minus", () => slider.Nudge(-1), null, clickable);
            _host?.Register(plus.rectTransform, plus, key + ".plus", () => slider.Nudge(+1), null, clickable);
            _host?.Register(hit.rectTransform, hit, key + ".track", () => { },
                cursor => slider.SetFromTrackPoint(cursor), clickable);

            gate?.Register(
                new[] { labelText, StepGlyph(plus), StepGlyph(minus) },
                new[] { valueLabel },
                captionText, caption,
                on => { slider.Interactable = on; slider.Apply(); });

            return slider;
        }

        /// <summary>스텝 버튼의 글리프 텍스트(+/−). 게이트가 잉크를 다시 칠할 때 필요하다.</summary>
        private static Text StepGlyph(Image stepButton)
        {
            if (stepButton == null) return null;
            Transform t = stepButton.transform.Find("Label");
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Image AddStepButton(RectTransform row, string name, string glyph, float xFromRight,
            float y, bool enabled)
        {
            Image surface = UiChrome.AddSurface(row, name, SettingsControls.ButtonSurfaceOnCard, 5);
            SettingsControls.PlaceTopRight(surface.rectTransform, xFromRight, y,
                SettingsControls.StepButton, SettingsControls.StepButton);
            Text label = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.InkTitle(enabled));
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;
            return surface;
        }

        public SettingsSegment AddSegment(string key, string label, string[] options, int index,
            Action<int> changed, string caption = null, bool enabled = true,
            DisabledReason disabledNote = default, SettingsRowGate gate = null)
        {
            RectTransform row = BeginRow(key, label,
                ComposeCaption(caption, enabled, disabledNote.Text, disabledNote.Kind), null,
                enabled, gate, out float rowHeight, out Text labelText, out Text captionText);

            var segment = new SettingsSegment
            {
                Rects = new RectTransform[options.Length],
                Surfaces = new Image[options.Length],
                Outlines = new Image[options.Length],
                Labels = new Text[options.Length],
                Changed = changed,
                Interactable = enabled,
            };

            float centerY = -(rowHeight - SettingsControls.SegmentHeight) * 0.5f;
            float x = 0f;
            for (int i = options.Length - 1; i >= 0; i--)   // 오른쪽 끝에서 왼쪽으로 쌓는다.
            {
                // ★ 9f는 <b>글자 한 칸의 폭 근사</b>이고 라벨 폰트(UiChrome.FontLabel)와 묶여 있다.
                //   2026-09-01에 FontLabel이 11 -> 12로 올라갔지만(Windows 홀수 pt 번짐 수정) 여기는
                //   그대로 뒀다: 한글은 폭이 pt에 가까워 12pt에서 "24 + 9n >= 12n" 즉 <b>8자까지</b>
                //   안전하고, 지금 쓰는 캡션은 최장 3자("숨기기")다. 8자를 넘는 순한글 캡션을 새로
                //   넣는 날에는 9f를 UiChrome.FontLabel에서 파생시켜야 한다
                //   (CharacterInfoWindow.TabLabelWidth가 이미 그 형태다).
                float width = 24f + options[i].Length * 9f;
                Image surface = UiChrome.AddSurface(row, "Seg" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                SettingsControls.PlaceTopRight(surface.rectTransform, x, centerY, width, SettingsControls.SegmentHeight);
                Image outline = UiChrome.AddOutline(surface.rectTransform, "Outline",
                    SettingsControls.OutlineOnCard, UiChrome.RadiusChip);
                Text text = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontLabel,
                    TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(text.rectTransform);
                text.text = options[i];

                segment.Rects[i] = surface.rectTransform;
                segment.Surfaces[i] = surface;
                segment.Outlines[i] = outline;
                segment.Labels[i] = text;

                int captured = i;
                _host?.Register(surface.rectTransform, surface, key + "." + i,
                    () => segment.SetIndexFromUser(captured), null, () => segment.Interactable);

                x += width + UiChrome.Space1;
            }

            segment.SetIndexSilently(index);

            gate?.Register(new[] { labelText }, null, captionText, caption,
                on => { segment.Interactable = on; segment.Apply(); });

            return segment;
        }

        /// <summary>오른쪽에 버튼 1~3개가 붙은 행([숨기기][보이기] / [지금 종료]).</summary>
        public Image[] AddButtons(string key, string label, string[] captions, Action<int> clicked,
            string caption = null, bool enabled = true, DisabledReason disabledNote = default)
        {
            RectTransform row = BeginRow(key, label,
                ComposeCaption(caption, enabled, disabledNote.Text, disabledNote.Kind), null,
                enabled, out float rowHeight);

            var results = new Image[captions.Length];
            float centerY = -(rowHeight - SettingsControls.ButtonHeight) * 0.5f;
            float x = 0f;
            for (int i = captions.Length - 1; i >= 0; i--)
            {
                // 9f의 의미와 상한(순한글 8자)은 위 AddSegmented의 주석 참고.
                float width = 26f + captions[i].Length * 9f;
                Image surface = UiChrome.AddSurface(row, "Btn" + i, SettingsControls.ButtonSurfaceOnCard,
                    UiChrome.RadiusChip);
                SettingsControls.PlaceTopRight(surface.rectTransform, x, centerY, width, SettingsControls.ButtonHeight);
                UiChrome.AddOutline(surface.rectTransform, "Outline", SettingsControls.OutlineOnCard, UiChrome.RadiusChip);
                Text text = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontLabel,
                    TextAnchor.MiddleCenter, UiChrome.InkTitle(enabled), bold: true);
                UiChrome.Stretch(text.rectTransform);
                text.text = captions[i];
                results[i] = surface;

                int captured = i;
                _host?.Register(surface.rectTransform, surface, key + "." + i,
                    () => clicked?.Invoke(captured), null, () => enabled);

                x += width + UiChrome.Space2;
            }
            return results;
        }

        /// <summary>색 견본이 오른쪽에 붙은 행(잉크색 / 포인트 컬러).</summary>
        public SettingsSwatchRow AddSwatches(string key, string label, Color[] colors, int index,
            Action<int> changed, string caption = null, bool enabled = true,
            DisabledReason disabledNote = default)
        {
            RectTransform row = BeginRow(key, label,
                ComposeCaption(caption, enabled, disabledNote.Text, disabledNote.Kind), null,
                enabled, out float rowHeight);

            var swatches = new SettingsSwatchRow
            {
                Rects = new RectTransform[colors.Length],
                Borders = new Image[colors.Length],
                Changed = changed,
                Interactable = enabled,
            };

            float centerY = -(rowHeight - SettingsControls.SwatchSize) * 0.5f;
            float x = 0f;
            for (int i = colors.Length - 1; i >= 0; i--)
            {
                Image surface = UiChrome.AddSurface(row, "Swatch" + i, colors[i], UiChrome.RadiusChip);
                SettingsControls.PlaceTopRight(surface.rectTransform, x, centerY,
                    SettingsControls.SwatchSize, SettingsControls.SwatchSize);
                Image border = UiChrome.AddOutline(surface.rectTransform, "Border",
                    SettingsControls.OutlineOnCard, UiChrome.RadiusChip, 2);

                swatches.Rects[i] = surface.rectTransform;
                swatches.Borders[i] = border;

                int captured = i;
                _host?.Register(surface.rectTransform, surface, key + "." + i,
                    () => swatches.SetIndexFromUser(captured), null, () => swatches.Interactable);

                x += SettingsControls.SwatchSize + SettingsControls.SwatchGap;
            }

            swatches.SetIndexSilently(index);
            return swatches;
        }

        /// <summary>
        /// 바로 위 행에 <b>딸린 캡션 한 줄</b>. 조건부로 나타나는 경고(톱니 아이콘 끔 등)를 위한 자리다.
        ///
        /// <para><b>공간은 항상 예약한다</b> — 캡션이 나타났다 사라질 때 카드 높이가 바뀌면 아래 카드들이
        /// 통째로 흔들려, 끄는 순간 눌러야 할 다음 버튼이 움직인다. 보이고 숨기는 것은 호출부가
        /// <c>SetActive</c>로 한다.</para>
        ///
        /// <para>바로 위 행의 구분선은 <b>끈다</b>: 캡션은 그 행의 일부이지 다음 항목이 아니다
        /// (시안의 <c>.row.has-caption</c>이 캡션을 행 <b>안</b>에 두는 것과 같은 그림).</para>
        /// </summary>
        public Text AddCaptionLine(string name, string text, Color color)
        {
            if (_dividers.Count > 0)
            {
                Image previous = _dividers[_dividers.Count - 1];
                if (previous != null) previous.gameObject.SetActive(false);
            }

            Text caption = UiChrome.AddText(_card, "Caption_" + name, UiChrome.FontCaption,
                TextAnchor.MiddleLeft, color);
            UiChrome.PlaceTopLeft(caption.rectTransform, SettingsControls.CardPadX, _y - 2f,
                SettingsControls.CardWidth - SettingsControls.CardPadX * 2f, 14f);
            caption.text = text;

            _y -= 18f;
            return caption;
        }

        /// <summary>
        /// 비활성 행에는 <b>사유</b>를 반드시 적는다 — 35-1-7의 "숨기지 않는다. 회색 + 사유 캡션".
        ///
        /// <para>★ <paramref name="disabledNote"/>는 <b>사용자가 읽을 문장만</b> 담는다. 내부 식별자
        /// (GlobalKey/이슈번호/"라운드")를 넣으면 이 함수가 그대로 화면에 렌더한다 — 실제로 그렇게
        /// 새어 나갔다(페르소나 M6). 팀이 알아야 할 사정은 <c>SettingsWindow.LogRoadmapNotes()</c>에
        /// 적고, 규칙은 <c>SettingsUserFacingCopyTests</c>가 <b>실제로 렌더된 문자열</b>을 훑어 잠근다.</para>
        ///
        /// <para>★★ <paramref name="kind"/>에 <b>기본값이 없는 것이 이 함수의 요점이다</b>
        /// (docs/UX_FLOW.md 43-2). 접두사를 붙일지 말지는 <b>문장의 종류</b>가 정하고, 종류를 아는 것은
        /// 문장을 쓰는 호출자뿐이다. 기본값을 두면 다음 사람이 종류를 <b>안 적을 수 있고</b>, 안 적힌
        /// K2 문장은 "준비 중 — 지금 붙잡을 만한 작은 창이 없어요."로 렌더되어 <b>지금 할 수 있는 일을
        /// 기다리라고 말한다</b>. 그건 컴파일도 리뷰도 카피 테스트도 통과한다.</para>
        /// </summary>
        private static string ComposeCaption(string caption, bool enabled, string disabledNote, DisabledKind kind)
        {
            if (enabled) return caption;
            if (string.IsNullOrEmpty(disabledNote)) return caption;

            string note = kind == DisabledKind.NotBuilt
                ? SettingsControls.NotBuiltPrefix + disabledNote
                : disabledNote;
            return string.IsNullOrEmpty(caption) ? note : caption + "  ·  " + note;
        }
    }
}
