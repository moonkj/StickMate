using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 캐릭터 <b>크기 다이얼</b> — docs/UX_FLOW.md 34-3. 구석 호버 패널(<see cref="CornerHoverPanel"/>)
    /// 안에 들어가는 방사형 눈금 다이얼이며, 캐릭터 크기의 <b>유일한 소유자</b>다(34-6-5: 880 정보창
    /// [외형] 탭에는 같은 값을 두지 않는다 — 두 곳에서 같은 값을 만지면 진실이 둘이 된다).
    ///
    /// ============================================================================
    /// 왜 MonoBehaviour가 아닌가 (34-9 #5에서 <b>의도적으로 벗어난 지점</b> — 리더 확인 요망)
    /// ============================================================================
    /// 설계 문서는 "신규 컴포넌트 2종"이라고 적었지만, 이 클래스는 <b>평범한 클래스</b>다.
    /// 컴포넌트로 만들면 프리팹 배선 지점이 하나 더 늘어나는데, 33-9 #10이 경고한 Blocker
    /// ("신규 3종이 프리팹에 없어 런타임에 존재하지 않음")가 정확히 그 지점에서 터졌다.
    /// 이 다이얼은 <b>호버 패널 없이 단독으로 존재할 이유가 하나도 없고</b> 수명도 완전히 같으므로,
    /// 소유자가 직접 만들어 들고 있는 편이 위험을 줄인다. 파일은 문서대로 분리했다.
    ///
    /// ============================================================================
    /// 기하 (전부 OS 포인트 — 캔버스가 ConstantPixelSize라 캔버스 유닛 = OS 포인트)
    /// ============================================================================
    /// <code>
    ///                12시 방향 176°는 비어 있다(눈금 없음, 상한 1.5 기준)
    ///     i=0  θ=-92°                                   i=23  θ=+92°
    ///     0.35×        ╲   ┌──────────┐   ╱             1.50×
    ///                    │ │  0.75×   │ │
    ///                    │ │   크기    │ │
    ///                  ╱   └──────────┘   ╲
    ///                       6시 = θ 0°
    /// </code>
    /// θ는 6시에서 <b>시계 방향</b>으로 +다. 화면 방향 벡터는 <c>(sin θ, −cos θ)</c>
    /// (θ=0 → 아래, θ=+92° → 오른쪽, θ=−92° → 왼쪽). 각도는 전부 <see cref="DegreesForIndex"/>
    /// 하나에서 나온다 — 상한이 바뀌면 눈금 수와 스윕이 함께 따라온다.
    ///
    /// <b>12시 쪽을 비워 둔 이유</b>(상한 1.5에서 176°, 그 전 2.0에서는 96°)는 장식이 아니다 — 패널이 접혀 있을 때 위쪽이 잘려도
    /// <b>눈금은 하나도 가려지지 않는다</b>. 그래서 접힌 상태에서도 크기 조절이 100% 가능하고,
    /// 펼침은 "미리보기를 곁들이는" 선택지일 뿐이다(34-5-3).
    ///
    /// ============================================================================
    /// 발광을 블러 없이 만드는 법 (34-3-3) — 눈금 1개당 최대 2겹
    /// ============================================================================
    /// 이 프로젝트에는 HDR도 블룸도 없다. 그런데 릴스 실측이 보여준 "발광"의 실체는
    /// <b>파란 헤일로 + 흰 코어의 2층</b>이고, 블룸이 하는 일은 그 2층을 카메라가 자동으로
    /// 만들어 주는 것뿐이다. 우리는 그 2층을 <b>직접 그린다</b>
    /// (<see cref="BattleMinigameRenderer"/>가 게이지를 4겹으로 쌓는 것과 같은 검증된 관례).
    ///
    /// <b>할당 규약</b>: GameObject는 생성 시 1회만 만든다. 값이 바뀌는 프레임에만 <b>색과 길이</b>를
    /// 갱신하고, 값이 그대로면 아무것도 건드리지 않는다(24시간 상주 앱).
    /// </summary>
    public sealed class SizeDialWidget
    {
        // ==================== 값 축 (34-3-2 / 34-3-5) ====================

        /// <summary>눈금 1칸 = 0.05배. 범위 0.35~1.50을 이 간격으로 나누면 정확히 24칸이다
        /// (★ 2026-08-31 상한 2.0 → 1.5, 그 전에는 34칸).
        /// <para>★ 2026-09-01 — 값을 여기 적지 않고 <see cref="CharacterScaleController.ValueStep"/>을
        /// 그대로 참조한다. 설정창 슬라이더가 생기면서 스냅 격자를 쓰는 곳이 둘이 됐고, 격자가 두 벌이면
        /// 같은 값이 한쪽에서 1.15, 다른 쪽에서 1.20으로 보인다(원칙 1 위반). public API는 그대로다.</para></summary>
        public const float ValueStep = CharacterScaleController.ValueStep;

        /// <summary>눈금 개수. <c>(1.50 - 0.35) / 0.05 + 1 = 24</c>. 범위는
        /// <see cref="StickConfig.MinCharacterScale"/>/<see cref="StickConfig.MaxCharacterScale"/>를
        /// <b>그대로</b> 쓴다 — 다이얼이 자기 범위를 새로 정의하면 인스펙터 슬라이더와 진실이 둘이 된다.</summary>
        public static readonly int TickCount = Mathf.RoundToInt(
            (StickConfig.MaxCharacterScale - StickConfig.MinCharacterScale) / ValueStep) + 1;

        /// <summary>눈금 사이 각도. <b>간격은 상한과 무관하게 8°로 고정</b>이다 — 상한이 바뀌면
        /// 스윕이 따라 바뀌지(23간격 x 8° = 184°, 12시 쪽 176° 남음) 눈금이 촘촘해지지 않는다.
        /// 눈금 밀도를 고정해야 "한 칸 = 0.05배"라는 손끝 감각이 상한 변경에도 그대로 유지된다.</summary>
        private const float DegreesPerTick = 8f;

        private static float SweepHalfDegrees => (TickCount - 1) * DegreesPerTick * 0.5f;

        /// <summary>
        /// 눈금 i가 놓인 각도 θ(6시에서 시계방향 +, 도). 그리기(<see cref="Refresh"/>)와
        /// 잡기(<see cref="BeginDrag"/>)와 <b>테스트</b>가 전부 이 하나를 쓴다.
        /// <para>★ 2026-08-31 상한 2.0 → 1.5 변경에서 필요해졌다 — 테스트가 <c>-132° + i x 8°</c>를
        /// 베껴 적고 있었고, 눈금 수가 34 → 24로 바뀌자 그 식이 <b>존재하지 않는 원 위</b>를 찍어
        /// "다이얼이 안 눌린다"로 실패했을 것이다. 식이 두 벌이면 상한이 바뀔 때마다 같은 사고가 난다.</para>
        /// </summary>
        public static float DegreesForIndex(int index) => -SweepHalfDegrees + index * DegreesPerTick;

        // ==================== 기하 (OS 포인트) ====================

        private const float TickInnerRadius = 38f;
        private const float TickLength = 10f;
        private const float TickLengthCurrent = 13f;      // 현재 값은 색만이 아니라 **길이로도** 구분한다(색맹 접근성).

        /// <summary>눈금 <b>그림</b>이 실제로 뻗는 바깥 반지름(pt). 히트 원환(<see cref="HitOuterRadius"/>)과
        /// 다르다 — 이쪽은 "눈에 보이는 끝"이다.
        /// <para>public인 이유: 다이얼이 <b>상자 밖에 그려지지 않는다</b>는 불변식
        /// (<c>CornerHoverPanel.ContentGateBlend</c>)이 이 수 위에서 성립한다. 눈금을 길게 만들면 그
        /// 게이트도 같이 올려야 하며, <c>CornerHoverPanelTests</c>가 그 등식을 잠근다.</para></summary>
        public const float TickVisualOuterRadius = TickInnerRadius + TickLengthCurrent;
        private const float TickThickness = 2.5f;
        private const float HaloThickness = 7f;
        private const float HaloThicknessCurrent = 9f;
        private const float HubRadius = 34f;
        private const float BloomDiameter = 150f;

        /// <summary>히트 원환. 안쪽 20pt는 <b>죽은 구역</b>이다 — 중심 근처에서는 각도의 변화율이 커서
        /// 1px 흔들림만으로 값이 튄다.</summary>
        public const float HitInnerRadius = 20f;
        public const float HitOuterRadius = 90f;

        /// <summary>"짧게 클릭"의 판정 — 이만큼 미만 움직이고 이 시간 안에 떼면 그 눈금으로 점프한다.</summary>
        private const float ClickMovePoints = 6f;
        private const float ClickSeconds = 0.25f;

        // ==================== 상태 ====================

        private readonly RectTransform _root;

        /// <summary>등장 알파를 한 곳에서 먹인다 — 눈금 68겹의 색을 하나씩 건드리지 않는다
        /// (색은 값에서만 파생돼야 한다: <see cref="Refresh"/>).</summary>
        private readonly CanvasGroup _group;

        private readonly Image[] _haloes;
        private readonly Image[] _cores;
        private readonly Image _bloom;
        private readonly Text _valueText;
        private readonly Text _pendingText;

        private int _index;
        private int _renderedIndex = -1;

        /// <summary>드래그를 시작한 순간의 값 — 링 밖으로 끌고 나가 떼면 여기로 되돌린다(34-3-1 탈출구).</summary>
        private int _grabIndex;
        private float _grabOffsetDegrees;
        private Vector2 _grabCursor;
        private float _grabTime;
        private bool _dragging;

        /// <summary>현재 값(캐릭터 배율).</summary>
        public float Value => IndexToValue(_index);

        public bool IsDragging => _dragging;

        /// <summary>다이얼 중심(Unity 스크린 픽셀) — <b>히트 판정용</b>. 소유자가 매 프레임 갱신한다.</summary>
        public Vector2 CenterScreen { get; set; }

        /// <summary>
        /// 다이얼 <b>그림</b>의 중심을 부모(패널) 좌하단 기준 좌표로 옮긴다.
        /// <para>★ 이 함수가 없으면 히트 판정(<see cref="CenterScreen"/>)과 그림이 <b>서로 다른 자리</b>에
        /// 있게 된다 — 실제로 첫 캡처에서 다이얼이 패널 한가운데(부모 중심 앵커의 기본값)에 그려져
        /// 미리보기 카드와 겹쳤다. 두 좌표가 같은 값에서 나와야 "보이는 곳을 누르면 먹는다"가 성립한다.</para>
        /// </summary>
        public void SetCenterInParentPoints(Vector2 centerFromParentBottomLeft)
        {
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            if (_root.anchoredPosition != centerFromParentBottomLeft)
                _root.anchoredPosition = centerFromParentBottomLeft;
        }

        /// <summary>1 OS 포인트가 몇 Unity 픽셀인가. 히트 판정이 화면 좌표계에서 이뤄지므로 필요하다.</summary>
        public float PixelsPerPoint { get; set; } = 1f;

        public static float IndexToValue(int index)
            => Mathf.Clamp(StickConfig.MinCharacterScale + index * ValueStep,
                StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale);

        public static int ValueToIndex(float value)
            => Mathf.Clamp(Mathf.RoundToInt((value - StickConfig.MinCharacterScale) / ValueStep), 0, TickCount - 1);

        // ==================== 만들기 ====================

        /// <param name="parent">다이얼 중심이 놓일 부모. 이 클래스는 부모 <b>중심</b> 기준으로 그린다.</param>
        public SizeDialWidget(Transform parent, float initialValue)
        {
            var go = new GameObject("SizeDial", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            _group = go.GetComponent<CanvasGroup>();
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(BloomDiameter, BloomDiameter);

            // (0) 링 블룸 — 눈금 **밑에** 깔린다. 값이 클수록 링 전체가 밝다(숫자를 안 봐도 "지금 크다"가 읽힌다).
            _bloom = AddGlow(_root, "RingBloom", BloomDiameter, UiChrome.Accent);

            _haloes = new Image[TickCount];
            _cores = new Image[TickCount];
            for (int i = 0; i < TickCount; i++)
            {
                // 헤일로를 먼저 붙여야 형제 순서상 코어 아래에 깔린다(34-3-3의 겹 순서).
                _haloes[i] = UiChrome.AddStroke(_root, "TickHalo", TickLengthCurrent, HaloThicknessCurrent,
                    0f, Vector2.zero, Color.clear);
                _cores[i] = UiChrome.AddStroke(_root, "TickCore", TickLengthCurrent, TickThickness,
                    0f, Vector2.zero, Color.clear);
            }

            // (2) 중앙 원판 — 유리 6겹 대신 **원**이라 표면/보더 2겹으로 같은 인상을 만든다.
            //     34-1의 알파 규칙: 다이얼 원판은 0.72(글자가 큰 값 하나뿐이라 더 투명해도 된다).
            var hubColor = new Color(UiChrome.PanelSurface.r, UiChrome.PanelSurface.g, UiChrome.PanelSurface.b, 0.72f);
            UiChrome.AddCircle(_root, "HubFill", HubRadius * 2f, hubColor);
            UiChrome.AddCircle(_root, "HubBorder", HubRadius * 2f, UiChrome.PanelBorder, 1.2f);

            _valueText = UiChrome.AddText(_root, "Value", UiChrome.FontTitle, TextAnchor.MiddleCenter,
                UiChrome.TextPrimary, bold: true);
            _valueText.rectTransform.anchorMin = _valueText.rectTransform.anchorMax =
                _valueText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _valueText.rectTransform.sizeDelta = new Vector2(HubRadius * 2f, 18f);
            _valueText.rectTransform.anchoredPosition = new Vector2(0f, 6f);

            Text label = UiChrome.AddText(_root, "Label", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.TextTertiary);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax =
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(HubRadius * 2f, 14f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            label.text = "크기";

            // "곧 적용" 캡션(34-3-6) — 랙돌/스펙터클 중이라 실캐릭터 적용이 미뤄질 때만 켠다.
            _pendingText = UiChrome.AddText(_root, "Pending", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.TextOnAccent);
            _pendingText.rectTransform.anchorMin = _pendingText.rectTransform.anchorMax =
                _pendingText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _pendingText.rectTransform.sizeDelta = new Vector2(HubRadius * 2f, 12f);
            _pendingText.rectTransform.anchoredPosition = new Vector2(0f, -21f);
            _pendingText.text = "곧 적용";
            _pendingText.gameObject.SetActive(false);

            _index = ValueToIndex(initialValue);
            Refresh();

            // ★ 태어날 때는 <b>보이지 않는다</b>. 소유자가 상자를 다 키운 뒤에 켠다(SetReveal 문서).
            Reveal = 0f;
            _group.alpha = 0f;
            go.SetActive(false);
        }

        private static Image AddGlow(Transform parent, string name, float diameter, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            var image = go.GetComponent<Image>();
            image.sprite = UiChrome.RadialGlow();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // ==================== 값 / 그리기 ====================

        /// <summary>값을 강제로 맞춘다(저장 복원 등). 눈금에 스냅된다.</summary>
        public void SetValue(float value)
        {
            int next = ValueToIndex(value);
            if (next == _index) return;
            _index = next;
            Refresh();
        }

        /// <summary>
        /// 등장 진행도 0..1. 소유자(<see cref="CornerHoverPanel"/>)가 매 프레임 밀어 넣는다.
        ///
        /// <para>★ 2026-08-31 사용자 신고 <i>"크기조절 원이 먼저 떠 있고 상자가 나중에 커짐"</i>의 수정.
        /// 이 위젯은 패널의 <b>자식</b>인데 패널에는 마스크가 없다 — 패널 사각형이 PEEK(104×14pt)일 때도
        /// 이 그림(중심이 패널 바닥에서 78pt 위, 눈금이 51pt까지 뻗는다)은 <b>잘리지 않고 전부 그려졌다</b>.
        /// 그래서 상자가 자라기 전부터 원이 허공에 떠 있었다. 이제 알파의 출처가 상자의 성장 진행도
        /// 하나뿐이라 두 그림의 등장 순서가 갈라질 수 없다.</para>
        ///
        /// <para><b>스케일은 건드리지 않는다</b> — 그림과 히트 판정이 같은 한 쌍의 수에서 나와야 하기
        /// 때문이다(<see cref="SetCenterInParentPoints"/> 문서). 알파만 움직이면 "보이는 곳"과
        /// "먹히는 곳"이 언제나 같은 자리에 있다.</para>
        /// </summary>
        public void SetReveal(float reveal01)
        {
            float v = Mathf.Clamp01(reveal01);
            if (Mathf.Approximately(v, Reveal)) return;   // 24시간 상주 앱 — 같은 값을 매 프레임 대입하지 않는다.
            Reveal = v;
            if (_group != null) _group.alpha = v;

            // 완전히 투명할 때는 아예 끈다 — 알파 0짜리 Image(눈금 2겹 x TickCount + 블룸)를
            // 캔버스 리빌드에 계속 태우지 않는다(24시간 상주 앱).
            bool visible = v > 0.001f;
            if (_root != null && _root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);
        }

        /// <summary>지금 등장 진행도(진단/테스트용).</summary>
        public float Reveal { get; private set; }

        public void SetPendingCaption(bool on)
        {
            if (_pendingText == null || _pendingText.gameObject.activeSelf == on) return;
            _pendingText.gameObject.SetActive(on);
        }

        /// <summary>값이 실제로 달라진 프레임에만 색과 길이를 다시 칠한다(24시간 상주 앱).</summary>
        private void Refresh()
        {
            if (_renderedIndex == _index) return;
            _renderedIndex = _index;

            for (int i = 0; i < TickCount; i++)
            {
                bool current = i == _index;
                bool lit = i <= _index;

                float length = current ? TickLengthCurrent : TickLength;
                float haloThickness = current ? HaloThicknessCurrent : HaloThickness;
                float degrees = DegreesForIndex(i);

                // (a)/(b) 헤일로 — 꺼진 눈금은 헤일로가 없다(몸통 한 겹만 남는다).
                Color haloColor = current
                    ? new Color(UiChrome.Accent.r, UiChrome.Accent.g, UiChrome.Accent.b, 0.55f)
                    : lit ? new Color(UiChrome.Accent.r, UiChrome.Accent.g, UiChrome.Accent.b, 0.35f)
                          : Color.clear;
                PlaceTick(_haloes[i], degrees, length, haloThickness, haloColor);

                // (c) 코어 — 현재 값만 순백, 켜진 눈금은 글로우 코어색, 꺼진 눈금은 흰색 α0.16.
                Color coreColor = current
                    ? Color.white
                    : lit ? UiChrome.AccentGlowCore
                          : new Color(1f, 1f, 1f, 0.16f);
                PlaceTick(_cores[i], degrees, length, TickThickness, coreColor);
            }

            // 링 블룸 — 켜진 비율 t에 따라 α = 0.04 + 0.10 t.
            float t = TickCount > 1 ? _index / (float)(TickCount - 1) : 0f;
            _bloom.color = new Color(UiChrome.Accent.r, UiChrome.Accent.g, UiChrome.Accent.b, 0.04f + 0.10f * t);

            _valueText.text = FormatValue(IndexToValue(_index));
        }

        /// <summary>눈금 하나를 각도/길이/두께/색으로 다시 놓는다. GameObject는 만들지 않는다.</summary>
        private static void PlaceTick(Image image, float degrees, float length, float thickness, Color color)
        {
            if (image == null) return;
            float rad = degrees * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
            Vector2 center = dir * (TickInnerRadius + length * 0.5f);

            var rt = image.rectTransform;
            // UiChrome.AddStroke와 같은 규약 — 사각형은 램프 폭만큼 부풀어 있다(그 함수 주석 참고).
            float boxHeight = thickness + StrokeFeatherPoints * 2f;
            rt.sizeDelta = new Vector2(length + StrokeFeatherPoints * 2f, boxHeight);
            rt.anchoredPosition = center;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            image.pixelsPerUnitMultiplier = UiChrome.CapsuleCapTexelsPublic * 2f / boxHeight;
            image.color = color;
        }

        /// <summary>UiChrome이 획 사각형을 부풀리는 폭. 그쪽 <c>EdgeFeather</c>와 같은 값이어야
        /// 눈금이 다른 획들과 같은 두께로 보인다(그래서 UiChrome이 공개한다).</summary>
        private static float StrokeFeatherPoints => UiChrome.EdgeFeatherPoints;

        /// <summary>"0.75×" — 소수 둘째 자리까지. 켜진 눈금 수와 이 숫자와 실제 적용 값이
        /// <b>구조적으로</b> 같다(전부 같은 <c>_index</c>에서 파생된다 — 원칙 1).</summary>
        public static string FormatValue(float value) => value.ToString("0.00") + "×";

        // ==================== 입력 (34-3-4) ====================

        /// <summary>
        /// 커서가 조작 원환(20 ≤ r ≤ 90pt) <b>이면서 눈금이 실제로 있는 각도 범위 안</b>인가
        /// (|θ| ≤ SweepHalfDegrees — 상한 1.5에서 92°).
        /// ★ 2026-08-31(통합검증 R2, M1) — 반지름만 보던 시절, 12시 쪽 빈 구역(눈금이 하나도 없는
        /// 자리)도 "원환 안"으로 잡혀서, 그 빈 자리를 탭하면 <see cref="EndDrag"/>의 탭 분기가
        /// <see cref="IndexForAngle"/>을 부르고 그 함수의 "빈 구역은 가까운 끝값에 붙인다" 규칙(랩어라운드
        /// 방지용 — <b>드래그 도중</b>에는 맞는 동작이다)이 그대로 적용돼, 아무 데나 짧게 누른 클릭이
        /// 크기를 0.35×나 2.00×로 순간 점프시켰다. 이 게이트는 <b>누르는 순간</b>(드래그 시작 여부)에만
        /// 걸리므로, 이미 시작된 드래그가 빈 구역으로 회전해 들어가는 것까지 막지는 않는다(그건 여전히
        /// 끝값에 붙는 게 맞는 동작).
        /// </summary>
        public bool IsInRing(Vector2 cursorScreen)
        {
            float r = (cursorScreen - CenterScreen).magnitude / Mathf.Max(0.0001f, PixelsPerPoint);
            if (r < HitInnerRadius || r > HitOuterRadius) return false;
            return Mathf.Abs(AngleOf(cursorScreen)) <= SweepHalfDegrees;
        }

        /// <summary>
        /// 잡는다. <b><see cref="_grabOffsetDegrees"/>가 필요한 이유</b>: 이게 없으면 링의 아무 데나
        /// 누르는 순간 값이 그 각도로 <b>순간이동</b>한다. 사용자는 "잡았을 뿐"인데 캐릭터 크기가
        /// 이미 바뀐 뒤다. 오프셋을 두면 누른 지점이 곧 현재 값이 되고, 그때부터 상대 회전만 반영된다.
        /// </summary>
        public void BeginDrag(Vector2 cursorScreen, float unscaledTime)
        {
            _dragging = true;
            _grabIndex = _index;
            _grabCursor = cursorScreen;
            _grabTime = unscaledTime;
            _grabOffsetDegrees = AngleOf(cursorScreen) - DegreesForIndex(_index);
        }

        /// <summary>드래그 중. 값이 실제로 바뀌면 true(호출부가 그때만 캐릭터에 반영한다).</summary>
        public bool DragTo(Vector2 cursorScreen)
        {
            if (!_dragging) return false;
            float degrees = AngleOf(cursorScreen) - _grabOffsetDegrees;
            int next = IndexForAngle(degrees);
            if (next == _index) return false;
            _index = next;
            Refresh();
            return true;
        }

        /// <summary>
        /// 뗀다. 반환값은 "값이 확정됐는가".
        /// <list type="bullet">
        /// <item>링 밖(r &gt; 90pt)에서 떼면 <b>취소</b> — 누르기 직전 값으로 되돌린다.</item>
        /// <item><b>짧게 클릭</b>(6pt 미만 + 0.25초 이내)이면 오프셋을 무시하고 그 눈금으로 점프한다 —
        /// "여기를 눌렀다"는 명시적 지목이기 때문이다.</item>
        /// </list>
        /// </summary>
        public bool EndDrag(Vector2 cursorScreen, float unscaledTime, out bool changed)
        {
            changed = false;
            if (!_dragging) return false;
            _dragging = false;

            float pixelsPerPoint = Mathf.Max(0.0001f, PixelsPerPoint);
            float movedPoints = (cursorScreen - _grabCursor).magnitude / pixelsPerPoint;
            bool tap = movedPoints < ClickMovePoints && unscaledTime - _grabTime < ClickSeconds;

            if (tap)
            {
                int next = IndexForAngle(AngleOf(cursorScreen));
                changed = next != _index;
                _index = next;
                Refresh();
                return true;
            }

            float radius = (cursorScreen - CenterScreen).magnitude / pixelsPerPoint;
            if (radius > HitOuterRadius)
            {
                changed = _index != _grabIndex;
                _index = _grabIndex;
                Refresh();
                return false;   // 취소 — 호출부가 "확정"으로 처리하면 안 된다.
            }

            changed = _index != _grabIndex;
            return true;
        }

        /// <summary>중앙 숫자를 눌렀는가 — 눌리면 기본값(배포 배율)으로 되돌린다(34-3-1 탈출구 ③).</summary>
        public bool IsOnHub(Vector2 cursorScreen)
            => (cursorScreen - CenterScreen).magnitude / Mathf.Max(0.0001f, PixelsPerPoint) < HitInnerRadius;

        /// <summary>커서 방향의 θ(6시에서 시계방향 +, 도).</summary>
        private float AngleOf(Vector2 cursorScreen)
        {
            Vector2 d = cursorScreen - CenterScreen;
            if (d.sqrMagnitude < 1e-6f) return 0f;
            // (sin θ, −cos θ) = d/|d| 의 역변환.
            return Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// θ → 눈금 인덱스. <b>랩어라운드를 금지</b>한다 — 12시 쪽 빈 구간을 통과할 수 없고,
        /// ±132°를 넘으면 끝값에 붙는다. 최대에서 조금 더 돌렸을 때 최소로 튀는 것이 회전식 UI의
        /// 가장 흔한 사고다.
        /// </summary>
        private static int IndexForAngle(float degrees)
        {
            // atan2는 (-180, 180]을 돌려준다. 빈 구역(|θ| > SweepHalfDegrees)에 들어오면 <b>가까운 끝값에 붙인다</b>.
            // 부호가 그대로 어느 쪽 끝인지 말해 준다 — 정수리 바로 왼쪽은 −179°(최소), 오른쪽은 +179°(최대).
            float half = SweepHalfDegrees;
            degrees = Mathf.Clamp(degrees, -half, half);
            return Mathf.Clamp(Mathf.RoundToInt((degrees + half) / DegreesPerTick), 0, TickCount - 1);
        }
    }
}
