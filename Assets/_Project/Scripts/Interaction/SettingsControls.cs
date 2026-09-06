using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

        /// <summary>
        /// 그룹 카드 폭. ★ 2026-09-02 (41-2 결정 3) — 680 → <b>656</b>.
        /// <para>창 720 − 좌 20 − 우 20 = 680이었는데, 그러면 본문이 뷰포트를 꽉 채워
        /// <b>세로 스크롤 레일이 앉을 자리가 0pt</b>였다. 그래서 [▲][▼]가 탭 줄에 얹혀 있었고,
        /// 탭 5개와 같은 줄·같은 높이라 <b>"탭 넘기는 버튼"</b>으로 읽혔다(민지 M12).
        /// 656으로 줄이면 x 680~700이 레일 자리가 되고 좌 20 / 우 20 대칭이 된다.</para>
        /// <para>카드 안쪽 폭 656 − <see cref="CardPadX"/>×2 = 628 ≥ 캡션 상자 480 / 라벨 상자 420
        /// → <b>잘리는 글자 없음</b>.</para>
        /// </summary>
        public const float CardWidth = 656f;

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

        /// <summary>글자를 직접 치는 칸의 크기(<see cref="SettingsCardBuilder.AddTextField"/>).
        /// <para>폭 검산: 행 안쪽 폭은 <c>CardWidth − CardPadX × 2 = 628</c>이고 라벨 상자가 x=0에서
        /// 420을 쓴다(<c>BeginRow</c>). 오른쪽 끝에 붙는 이 칸이 200이면 라벨과의 사이에 <b>8</b>이
        /// 남는다 — <see cref="ControlGap"/>과 같은 값이라 다른 행과 같은 리듬이다.</para>
        /// <para>높이 26은 <see cref="UiChrome.MinTargetSizePoints"/>(24) 위다. 행 자체가 44라
        /// 실제 클릭 타깃은 더 크지만, 칸을 하한 아래로 두면 "보이는 것과 누를 수 있는 것"이
        /// 갈라진다.</para></summary>
        public const float TextFieldWidth = 200f;
        public const float TextFieldHeight = 26f;

        // ==================== 칩 안쪽 여백 — ★ 글자 수 모형을 대체한 자리 ====================
        //
        // ★★ 2026-09-03 — 세그먼트/버튼 폭이 <b>«24f + 글자수 × 9f»</b>였다. 그 9f는 한글 자폭
        //    근사였고 <b>한글에서만</b> 맞았다. 라틴은 자폭이 절반 이하인데 같은 9f를 청구해서
        //    상자가 글리프의 두 배 가까이 부풀었고, 오른쪽에서 왼쪽으로 쌓는 이 행에서는 그 부풀음이
        //    그대로 <b>라벨 쪽으로 침범</b>한다. 즉 «영어가 길어서» 넘친 것이 아니라
        //    <b>모형이 넘침을 만들어 냈다</b>.
        //
        //    이제 글자 폭은 <see cref="UnityEngine.UI.Text.preferredWidth"/>(폰트가 실제로 잰 값)로
        //    묻고, 여기 두 상수는 <b>여백만</b> 맡는다. 근거와 같은 문장이 <c>UiChrome.Ellipsize</c>
        //    문서에 이미 적혀 있다 — 이 저장소는 답을 이미 알고 있었고 여섯 곳이 안 쓰고 있었을 뿐이다.
        //
        //    ※ 값은 옛 식의 여백을 <b>그대로</b> 옮겼다(24 = 12×2, 26 = 13×2). 여백을 바꾸면
        //      이번 변경의 '측정 효과'와 '여백 변경 효과'가 섞여 회귀 판정이 불가능해진다.

        /// <summary>세그먼트 칩의 좌우 여백(한쪽). 옛 식 <c>24f + …</c>의 24를 반으로 나눈 값.</summary>
        public const float SegmentPadX = 12f;

        /// <summary>버튼 칩의 좌우 여백(한쪽). 옛 식 <c>26f + …</c>의 26을 반으로 나눈 값.</summary>
        public const float ButtonPadX = 13f;

        /// <summary>
        /// 글자 상자 폭을 <b>폰트에게 물어</b> 정한다 — 글자 수 × 상수를 쓰지 않는다.
        ///
        /// <para><b>왜 <see cref="Mathf.Ceil"/>인가</b>: 이 창의 모든 배치는 정수 pt 격자 위에 있고
        /// (여백·간격·행 높이가 전부 정수), 내림/반올림은 <b>글리프 오른쪽 끝을 1pt 미만으로 깎을 수</b>
        /// 있다. 올림은 어떤 입력에서도 글리프를 자르지 않는다 — 안전한 방향이 하나뿐이라 고르는 것이지
        /// 취향이 아니다.</para>
        ///
        /// <para><b>호출 시점</b>: 부품을 <b>굽는 순간 1회</b>다. <c>preferredWidth</c>는 텍스트 제너레이터를
        /// 돌리므로 매 프레임 부르면 안 된다(<c>UiChrome.Ellipsize</c> 호출부 규약과 같은 이유).</para>
        /// </summary>
        public static float MeasuredWidth(Text text, string content)
        {
            if (text == null) return 0f;
            text.text = content ?? string.Empty;
            return Mathf.Ceil(text.preferredWidth);
        }

        // ==================== 색 (전부 UiChrome 토큰 or 그 합성) ====================
        //
        // 카드 위에 얹히는 것은 CardSurface에, 창 바탕에 직접 얹히는 것은 PanelSurface에 합성한다.
        // 밑에 깔린 색이 다르면 같은 토큰이라도 합성 결과가 달라야 한다 — 그래서 두 벌이다.

        public static Color CardBorderOnCard => UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface);
        public static Color DividerOnCard => UiChrome.Flatten(UiChrome.Divider, UiChrome.CardSurface);
        public static Color DividerOnPanel => UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface);
        public static Color TrackOnCard => UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.CardSurface);

        /// <summary>같은 트랙을 <b>창 바탕에 직접</b> 깔 때(2026-09-02: 본문 오른쪽 스크롤 레일).</summary>
        public static Color TrackOnPanel => UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.PanelSurface);
        public static Color OutlineOnCard => UiChrome.Flatten(UiChrome.PanelBorder, UiChrome.CardSurface);

        /// <summary>카드보다 <b>밝은</b> 표면. 시안의 <c>rgba(255,255,255,0.06)</c> 자리인데,
        /// 새 색을 만들지 않으려고 기존 토큰(CardBorder = 흰색 α0.10)을 카드 위에 합성해 만든다.
        /// <para>★ 2026-09-06 — 이제 <b>입력칸 전용</b>이다. 「누를 수 있는 슬래브」는
        /// <see cref="ControlFaceOnCard"/>로 올라갔지만 <b>입력칸은 따라가지 않는다</b>:
        /// 이 면을 밝히면 입력 글자가 11.10 → 3.34, 플레이스홀더가 3.94 → <b>1.18</b>로 지워진다
        /// (정책 §7-3 F2). 입력칸에서 부족한 것은 글자가 아니라 「상자가 있다는 사실」이다.</para></summary>
        public static Color ButtonSurfaceOnCard => UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface);

        /// <summary>같은 표면을 <b>창 바탕에 직접</b> 얹을 때. 밑에 깔린 색이 CardSurface가 아니라
        /// PanelSurface이므로 합성 상대가 다르다 — 위 "두 벌이다" 규칙 그대로다.
        /// <para>★ 2026-09-06 — <b>지금 이 값을 칠하는 자리는 없다</b>(푸터 [지금 종료]가
        /// <see cref="ControlFaceOnPanel"/>로 올라갔다). 지우지 않는 이유는 둘이다:
        /// (가) <see cref="ButtonSurfaceOnCard"/>의 <b>짝</b>이라 창 바탕 위에 한 단 들뜬 면이
        /// 다시 필요해지면 여기서 파생해야 하고(그때 손으로 합성하면 두 벌이 된다),
        /// (나) <c>UiChrome.RaisedTextBackdrops</c>가 이 값을 <b>글자가 얹히는 바탕</b>으로
        /// 선언하고 있어 잉크 사다리 검사가 이 면을 계속 지켜본다.</para></summary>
        public static Color ButtonSurfaceOnPanel => UiChrome.Flatten(UiChrome.CardBorder, UiChrome.PanelSurface);

        /// <summary>창 바탕에 직접 얹히는 버튼의 테두리.</summary>
        public static Color OutlineOnPanel => UiChrome.Flatten(UiChrome.PanelBorder, UiChrome.PanelSurface);

        /// <summary>
        /// ★★ <b>F1 — 「면이 곧 어포던스」인 슬래브의 면</b>(2026-09-06,
        /// <c>docs/UI_ALPHA_BLEED_POLICY.md</c> §7-2/§7-3). 스텝 <c>[+][−]</c> · 행 버튼 ·
        /// 푸터 <c>[지금 종료]</c> · 레일 <c>[▲][▼]</c>가 여기 앉는다.
        ///
        /// <para><b>왜 <see cref="ButtonSurfaceOnCard"/>로는 안 되나</b>: 그 면은 카드 대비
        /// <b>1.35 : 1</b>(창 위 1.32)이라 "누를 수 있는 칸"이 면만으로는 <b>보이지 않는다</b>.
        /// 그런데 <b>3.60으로 올릴 수는 없다</b> — design-art 실측: 면 3.30~3.93 구간은 그 위의
        /// 잉크가 동시에 죽는 <b>골짜기</b>이고, 이 저장소가 선언한
        /// <see cref="UiChrome.ControlFaceContrastTarget"/> 3.60이 정확히 그 안이다. 답은 값이 아니라
        /// <b>규칙</b>이다: 두 제약(면 ≥ 목표, 그 면 위 잉크 ≥ 목표)을 동시에 푸는 최소 혼합.</para>
        ///
        /// <para><b>새 hex 0개</b>다. 이 앱은 같은 문제를 이미 두 번 풀었고 두 번 다 같은 문을 썼다 —
        /// 창 크롬 칩(<see cref="UiChrome.ChromeButtonSurface"/>)과 카드 [착용] 칩
        /// (<see cref="UiChrome.CardActionSurface"/>). 설정창만 다른 밝기로 가면 이 앱에
        /// "누를 수 있는 것"의 시각 언어가 두 벌이 된다.</para>
        ///
        /// <para>★ <b>이 면 위의 글자는 반드시 <see cref="UiChrome.InkOnSurface"/>로 받아라.</b>
        /// 사다리 잉크는 여기서 전부 무너진다(Title 3.34 / Body 1.77 / Meta 1.18).
        /// <b>면만 바꾸면 화면이 지워진다</b> — 면과 잉크는 한 쌍이다.</para>
        ///
        /// <para>★ <b>속성(=&gt;)이 아니라 필드인 이유</b>: 위 토큰들은 <see cref="UiChrome.Flatten"/>
        /// 한 번이라 매번 계산해도 공짜지만, <see cref="UiChrome.ControlFaceOnSurface"/>는 최대 1025회
        /// 격자 탐색이다. 그 함수의 자기 주석이 <i>"표면을 만들 때 한 번 부르고 결과를 상수/필드에
        /// 담아 쓸 것"</i>이라고 못 박고 있다.</para>
        /// </summary>
        public static readonly Color ControlFaceOnCard = UiChrome.ControlFaceOnSurface(UiChrome.CardSurface);

        /// <summary>같은 슬래브를 <b>창 바탕에 직접</b> 얹을 때(푸터 [지금 종료] · 오른쪽 레일 칩).
        /// 밑에 깔린 색이 다르면 결과도 달라야 한다는 이 절의 "두 벌이다" 규칙 그대로다.</summary>
        public static readonly Color ControlFaceOnPanel = UiChrome.ControlFaceOnSurface(UiChrome.PanelSurface);

        /// <summary>선택된 세그먼트/스위치가 켜졌을 때의 강조 면. Accent는 α=1이라 합성이 필요 없다.</summary>
        public static Color AccentSolid => UiChrome.Accent;

        /// <summary>비활성 컨트롤의 <b>색이 곧 내용인</b> 면(색 견본)을 얼마나 남길 것인가.
        /// 0.45 = 원본에서 <b>2.67 : 1</b>만큼 물러난다(실측) — "꺼졌다"는 한눈에 읽히고
        /// "무슨 색이었나"는 여전히 읽힌다.</summary>
        public const float DisabledFillOpacity = 0.45f;

        /// <summary>견본/채움을 카드 바탕 쪽으로 접는다. 색상은 남기고 채도·밝기만 죽인다.</summary>
        public static Color Dimmed(Color fill)
            => UiChrome.Flatten(new Color(fill.r, fill.g, fill.b, DisabledFillOpacity), UiChrome.CardSurface);

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

        /// <summary>손잡이의 면. <b>게이트가 실행 중에 이 행을 내릴 수 있게</b> 들고 있는다 —
        /// 예전에는 손잡이 색이 생성 시점의 <c>enabled</c>로 한 번만 칠해져서, 게이트로 내린 토글은
        /// 트랙만 회색이 되고 손잡이는 계속 밝게 남았다(2026-09-02).</summary>
        public Image KnobImage;

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
            if (KnobImage != null)
            {
                KnobImage.color = Interactable ? UiChrome.TextPrimary : UiChrome.DisabledControlInk;
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

        /// <summary>
        /// ★★ 2026-09-02 — 이 함수가 <b>이 앱 최저 대비 1.28 : 1</b>을 만들고 있었다.
        ///
        /// <para><b>무엇이 뒤집혀 있었나</b>: 옛 코드는 면을 <c>active</c>만 보고 칠하고
        /// (<c>Interactable</c>을 <b>보지 않았다</b>) 글자만 <c>!Interactable</c>일 때 흐리게 내렸다.
        /// 그 결과 비활성 칩이 <b>강조색 면 그대로 밝게</b> 남아 "눌러도 될 것처럼" 보이고, 그 위에
        /// 얹힌 어두운-바탕용 잉크(<c>#AEB4BF</c>)가 밝은 파랑(<c>#5DA1F5</c>) 위에서 <b>1.28 : 1</b>로
        /// 지워졌다. [설정] &gt; [캐릭터] &gt; <c>말투</c>의 <c>[반말]</c>, 그리고 <c>말풍선 표시</c>를 끈
        /// 순간의 <c>대사 표시 시간</c> <c>[기본]</c> 칩이 그 자리다.</para>
        ///
        /// <para><b>규칙은 정반대여야 한다</b> — 이 앱의 다른 컨트롤은 전부 <b>면을 죽이고 글자는
        /// 그대로 둔다</b>: 스위치 트랙(<c>DividerOnCard</c>), 슬라이더 채움
        /// (<c>DisabledControlInk</c>), <see cref="FocusSessionPopover"/>의 시작 버튼
        /// (<c>CardSurfaceMuted</c> + <c>TextTertiary</c>). "못 쓴다"를 <b>글자로 말하지 않는 것</b>이
        /// 규칙이고, 면을 안 죽이는 컨트롤은 세그먼트 하나뿐이었으며 하필 그 하나만 글자가
        /// <b>면 위에</b> 얹혀 있었다.</para>
        ///
        /// <para><b>그래서 잉크를 면에서 파생시킨다</b>(<see cref="UiChrome.InkOnSurface"/>). 면과 글자를
        /// 각자 고르는 한 둘은 언젠가 반드시 어긋난다 — 지금까지가 그 증거다. 한 번의 계산에서 둘 다
        /// 나오면 어긋나는 것이 <b>물리적으로 불가능</b>해진다.</para>
        ///
        /// <para>비활성일 때 <b>어느 것이 골라져 있었는지</b>는 사라지지 않는다: 면이 한 단 들뜨고
        /// (2026-09-06부터 <see cref="SettingsControls.ControlFaceOnCard"/> — F1과 같은 슬래브다)
        /// 글자가 굵어진다. 스위치가 꺼진 채로도 손잡이 <b>위치</b>로 값을 말하는 것과 같은 규칙이다.
        /// 서열은 그대로 산다: 활성 = 강조색 6.83 &gt; 비활성 = 4.49 &gt; 안 고름 = 1.00.</para>
        /// </summary>
        public void Apply()
        {
            if (Surfaces == null) return;
            for (int i = 0; i < Surfaces.Length; i++)
            {
                bool active = i == Index;

                // ① 면을 먼저 정한다. 비활성이면 강조색은 <b>어디에도 남지 않는다</b>.
                // ★ 2026-09-06 (정책 §7-5) — 비활성 <b>활성</b>칩의 면이 F1 슬래브(4.49:1)로 올라간다.
                //   의도대로다: 활성 = 강조색 6.83 > 비활성 = 4.49 > 안 고름 = 1.00으로 서열이 산다.
                //   잉크는 아래 ②가 이미 <b>면에서</b> 뽑으므로 자동으로 따라온다.
                Color face = Interactable
                    ? (active ? SettingsControls.AccentSolid : UiChrome.CardSurface)
                    : (active ? SettingsControls.ControlFaceOnCard : UiChrome.CardSurface);

                if (Surfaces[i] != null) Surfaces[i].color = face;

                if (Outlines[i] != null)
                {
                    // ★ F4 — 안 고른 칩은 <b>테두리가 유일한 분리막</b>이다(면이 카드와 1.00:1).
                    //   기준 바탕은 <b>카드</b>다(생성부 AddSegment와 같은 값이어야 두 벌이 안 된다).
                    //   비활성 활성칩(밝은 면) 위에서는 이 링이 흐려지지만, 거기서는 <b>면</b>이
                    //   이미 상태를 말하고 있다.
                    Outlines[i].color = Interactable && active
                        ? SettingsControls.AccentSolid
                        : UiChrome.EdgeOnSurface(UiChrome.CardSurface);
                }

                // ② 글자는 <b>그 면에서</b> 나온다. 콜사이트가 색을 고르지 않는다.
                if (Labels[i] != null)
                {
                    Labels[i].color = UiChrome.InkOnSurface(face, UiChrome.InkRole.Body, enabled: true);
                    Labels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }
    }

    // ================================================================================
    // 부품 5 — 글자를 직접 치는 칸 (2026-09-06)
    // ================================================================================

    /// <summary>
    /// <see cref="SettingsCardBuilder.AddTextField"/>가 만든 입력칸 한 벌.
    ///
    /// <para><b>값을 들고 있지 않는다.</b> 이 창의 다른 부품(<see cref="SettingsToggle"/> 등)은
    /// 모델과 화면 사이에서 자기 상태를 캐시하지만, 글자 칸은 <see cref="InputField"/> 자체가
    /// 이미 그 역할을 한다. 한 겹 더 캐시하면 "두 곳이 같은 사실을 각자 계산하는" 상태가 되고,
    /// 그건 이 라운드가 정확히 피하려던 것이다(정보창과 설정창이 같은 이름을 보여준다).</para>
    /// </summary>
    public sealed class SettingsTextField
    {
        public InputField Input;
        public RectTransform Rect;

        /// <summary>지금 사용자가 이 칸에 글자를 치고 있는가 — <b>덮어쓰기 방어</b>에 쓴다.
        /// 다른 창이 이름을 바꿔 통지가 날아왔을 때 이 칸을 그대로 갈아치우면 타이핑 중인 글자가
        /// 사라진다.</summary>
        public bool IsFocused => Input != null && Input.isFocused;

        /// <summary><c>onEndEdit</c>를 <b>흘리지 않고</b> 표시값만 맞춘다. <see cref="InputField.text"/>
        /// 세터는 <c>onValueChanged</c>만 흘리므로 커밋 콜백이 되돌아 불리지 않는다 — 그래도
        /// "조용히"를 이름에 박아 두는 이유는 다른 부품(<c>SetIndexSilently</c> 등)과 같은 규약을
        /// 쓰기 위해서다.</summary>
        public void SetTextSilently(string value)
        {
            if (Input == null) return;
            string next = value ?? string.Empty;
            if (string.Equals(Input.text, next, StringComparison.Ordinal)) return;
            Input.text = next;
        }

        /// <summary>이 칸에 포커스를 준다(전역 폴링 클릭 경로). <see cref="EventSystem"/>이 없으면
        /// 아무 일도 하지 않는다 — 창이 <c>EnsureEventSystem()</c>으로 이미 보강하지만, 없는 상태를
        /// 예외로 만들지는 않는다.</summary>
        public void Focus()
        {
            if (Input == null) return;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(Input.gameObject);
            }
            Input.ActivateInputField();
        }
    }

    // ================================================================================
    // 부품 4 — 스와치(색 견본)
    // ================================================================================

    public sealed class SettingsSwatchRow
    {
        public RectTransform[] Rects;
        public Image[] Borders;

        /// <summary>견본 <b>면</b> 자체(2026-09-02 신설). 옛 코드는 테두리만 들고 있어서
        /// <c>Interactable</c>을 알아도 <b>죽일 것이 없었다</b>.</summary>
        public Image[] Surfaces;

        /// <summary>사용자가 고를 수 있을 때의 원래 색. 감쇠는 <b>여기서</b> 다시 계산한다 —
        /// 현재 색을 거듭 깎으면 껐다 켰다 할 때마다 색이 점점 죽는다.</summary>
        public Color[] BaseColors;

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

        /// <summary>
        /// ★ 2026-09-02 — 옛 코드는 <c>Interactable</c>을 <b>한 번도 보지 않았다</b>. 그래서
        /// <c>포인트 컬러 (준비 중)</c> 행의 견본이 바로 위 <b>실제로 동작하는</b> <c>잉크색</c> 행의
        /// 견본과 <b>픽셀 단위로 같은 채도</b>로 빛났다 — 세그먼트 칩(1.28:1)과 같은 뿌리다:
        /// <b>면이 거짓말을 한다.</b>
        ///
        /// <para>고르는 색이 곧 내용인 컨트롤이라 "회색으로 칠한다"는 답이 될 수 없다(무슨 색인지
        /// 못 읽게 된다). 대신 <b>카드 바탕 쪽으로 접는다</b> — 색상은 남고 채도/밝기만 죽는다.
        /// 강조색 견본 기준 원본 대비 <b>2.67 : 1</b>만큼 물러난다(실측).</para>
        ///
        /// <para>비텍스트 하한 3:1을 적용하지 않는 것은 의도다 — WCAG 2.2 §1.4.11이
        /// <b>비활성 컴포넌트를 명시적으로 면제</b>한다. 이 앱의 꺼진 스위치 손잡이가 1.70:1로
        /// 남아 있는 것과 같은 근거다.</para>
        /// </summary>
        public void Apply()
        {
            if (Borders == null) return;
            for (int i = 0; i < Borders.Length; i++)
            {
                bool hasFill = Surfaces != null && i < Surfaces.Length && Surfaces[i] != null
                    && BaseColors != null && i < BaseColors.Length;

                // ★ 2026-09-06 (정책 §7-3 F4) — 링의 바탕은 <b>지금 칠해진 견본 채움</b>이다.
                //   비활성이면 채움이 카드 쪽으로 접히므로(Dimmed) 링의 기준도 함께 움직여야 한다.
                Color fill = hasFill
                    ? (Interactable ? BaseColors[i] : SettingsControls.Dimmed(BaseColors[i]))
                    : UiChrome.CardSurface;

                if (hasFill) Surfaces[i].color = fill;

                if (Borders[i] == null) continue;

                // 고른 것: 흰 링은 <b>흰 견본에서 1.10:1로 사라진다</b> — 밝은 채움에서는 목탄으로
                // 뒤집는다(새 색 0개). 안 고른 것: 테두리의 문이 방향과 세기를 함께 고른다
                // (목표 대비는 그 함수가 들고 있다 — 여기에 숫자를 베끼면 목표가 바뀌는 날 갈라진다).
                // ★ 기준은 하한(3.0)이 아니라 <b>테두리의 목표</b>다 — 바로 아래 비선택 링이 그 목표를
                //   맞추므로, 하한으로 재면 「고른 것」이 「안 고른 것」보다 흐린 구간이 생긴다.
                bool selected = i == Index && Interactable;
                Borders[i].color = selected
                    ? (UiChrome.ContrastRatio(UiChrome.TextPrimary, fill) >= UiChrome.EdgeContrastTarget
                        ? UiChrome.TextPrimary : UiChrome.InkContrastCharcoal)
                    : UiChrome.EdgeOnSurface(fill);
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
        /// <summary>
        /// ★★ <b>글자 하나와 그 글자가 올라앉은 면의 쌍</b>(2026-09-06,
        /// <c>docs/UI_ALPHA_BLEED_POLICY.md</c> §7-3 ★).
        ///
        /// <para><b>왜 <see cref="Text"/>만으로는 안 되나</b>: 게이트는 등록된 글자에 잉크를
        /// <b>일괄 대입</b>한다. 등록된 글자가 전부 카드 바탕 위에 있을 때는 그게 옳았지만,
        /// F1 버튼 라벨이 <see cref="SettingsControls.ControlFaceOnCard"/>(<c>#838589</c>) 위로
        /// 올라가면서 같은 대입이 게이트가 내려가는 순간 <b>1.77 : 1</b>을 만든다 —
        /// 이 저장소가 2026-09-02에 이미 겪은 <b>1.28 : 1 사고와 같은 형태</b>다.</para>
        ///
        /// <para>고치는 방법은 게이트에 예외를 다는 것이 아니라 <b>게이트가 면을 알게 하는 것</b>이다.
        /// 면을 알면 잉크는 <see cref="UiChrome.InkOnSurface"/> 한 문에서 나오고, 카드 위 글자는
        /// <b>지금까지와 값이 완전히 같다</b>(어두운 면에서 그 문은 사다리를 그대로 돌려준다 —
        /// <c>InkOnSurfaceTests.어두운_면에서는_사다리를_그대로_돌려준다_위계_보존</c>이 잠근다).</para>
        /// </summary>
        public readonly struct GatedInk
        {
            public readonly Text Label;

            /// <summary>이 글자가 <b>실제로</b> 올라앉은 불투명 면. 잉크는 여기서 파생된다.</summary>
            public readonly Color Face;

            public GatedInk(Text label, Color face)
            {
                Label = label;
                Face = face;
            }

            /// <summary>설정 행의 기본형 — 글자가 <b>카드 바탕</b> 위에 직접 있다(행 라벨·값·캡션).</summary>
            public static GatedInk OnCard(Text label) => new GatedInk(label, UiChrome.CardSurface);
        }

        private sealed class Row
        {
            public GatedInk[] TitleInk;
            public GatedInk[] BodyInk;
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

        internal void Register(GatedInk[] titleInk, GatedInk[] bodyInk, Text caption, string baseCaption,
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

        /// <summary>값이 실제로 바뀐 때만 불린다(하루 종일 켜져 있는 앱 — 매 프레임 색을 다시 쓰지 않는다).
        /// <para>★ 2026-09-06 — <b>잉크를 미리 한 색으로 계산해 두지 않는다.</b> 등록된 글자마다
        /// 올라앉은 면이 다를 수 있고(F1 버튼 라벨은 <c>#838589</c> 위다), 면이 다르면 답도 달라야 한다.
        /// 루프 밖에서 <c>InkTitle(Enabled)</c> 한 색을 뽑아 전부에 대입하던 것이 정확히
        /// 「1.28 : 1 사고」의 형태였다.</para></summary>
        public void Apply()
        {
            for (int r = 0; r < _rows.Count; r++)
            {
                Row row = _rows[r];

                if (row.TitleInk != null)
                {
                    for (int i = 0; i < row.TitleInk.Length; i++)
                    {
                        GatedInk ink = row.TitleInk[i];
                        if (ink.Label == null) continue;
                        ink.Label.color = UiChrome.InkOnSurface(ink.Face, UiChrome.InkRole.Title, Enabled);
                    }
                }
                if (row.BodyInk != null)
                {
                    for (int i = 0; i < row.BodyInk.Length; i++)
                    {
                        GatedInk ink = row.BodyInk[i];
                        if (ink.Label == null) continue;
                        ink.Label.color = UiChrome.InkOnSurface(ink.Face, UiChrome.InkRole.Body, Enabled);
                    }
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
            DisabledReason disabledNote = default, SettingsRowGate gate = null)
        {
            RectTransform row = BeginRow(key, label,
                ComposeCaption(caption, enabled, disabledNote.Text, disabledNote.Kind), hotkey,
                enabled, gate, out float rowHeight, out Text labelText, out Text captionText);

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
            toggle.KnobImage = knob;

            toggle.SetOn(on);
            toggle.Apply();

            _host?.Register(track.rectTransform, track, key, () =>
            {
                toggle.SetOn(!toggle.On);
                changed?.Invoke(toggle.On);
            }, null, () => toggle.Interactable);

            gate?.Register(new[] { SettingsRowGate.GatedInk.OnCard(labelText) }, null, captionText, caption,
                onGate => { toggle.Interactable = onGate; toggle.Apply(); });

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

            // ★ 스텝 글리프는 <b>카드가 아니라 F1 슬래브 위</b>에 있다 — 면을 함께 등록하지 않으면
            //   게이트가 내려가는 순간 #838589 위에서 1.77:1이 된다(정책 §7-3 ★).
            gate?.Register(
                new[]
                {
                    SettingsRowGate.GatedInk.OnCard(labelText),
                    new SettingsRowGate.GatedInk(StepGlyph(plus), SettingsControls.ControlFaceOnCard),
                    new SettingsRowGate.GatedInk(StepGlyph(minus), SettingsControls.ControlFaceOnCard),
                },
                new[] { SettingsRowGate.GatedInk.OnCard(valueLabel) },
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
            // ★ F1(2026-09-06) — 면이 곧 어포던스인 슬래브. 면과 잉크를 <b>한 번에</b> 바꾼다:
            //   면만 올리면 그 위의 사다리 잉크(3.34:1)가 지워진다(정책 §2-E).
            Color face = SettingsControls.ControlFaceOnCard;
            Image surface = UiChrome.AddSurface(row, name, face, 5);
            SettingsControls.PlaceTopRight(surface.rectTransform, xFromRight, y,
                SettingsControls.StepButton, SettingsControls.StepButton);
            Text label = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled));
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
                // ★ 2026-09-03 — 글자 폭을 <b>폰트에게 묻는다</b>(옛 식: 24f + 글자수 × 9f).
                //   상자를 먼저 놓고 글자를 넣던 순서를 뒤집었다: 글자를 먼저 만들어 재고, 그 값으로
                //   상자를 놓는다. 형제 순서(면 → 테두리 → 글자)는 그대로라 겹 순서는 안 바뀐다.
                // ★ F4(2026-09-06, 정책 §7-3) — 안 고른 칩의 <b>면</b>은 올리지 않는다(고른 칩과의 차이가
                //   「면의 유무」로 읽혀야 한다). 대신 <b>테두리가 분리막을 진다</b> — 옛 OutlineOnCard는
                //   카드 대비 1.66이었다. 새 값은 EdgeOnSurface가 정한다(숫자를 여기 베끼지 않는다).
                Image surface = UiChrome.AddSurface(row, "Seg" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                Image outline = UiChrome.AddOutline(surface.rectTransform, "Outline",
                    UiChrome.EdgeOnSurface(UiChrome.CardSurface), UiChrome.RadiusChip, 2);
                Text text = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontLabel,
                    TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                float width = SettingsControls.SegmentPadX * 2f
                    + SettingsControls.MeasuredWidth(text, options[i]);
                SettingsControls.PlaceTopRight(surface.rectTransform, x, centerY, width, SettingsControls.SegmentHeight);
                UiChrome.Stretch(text.rectTransform);

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

            gate?.Register(new[] { SettingsRowGate.GatedInk.OnCard(labelText) }, null, captionText, caption,
                on => { segment.Interactable = on; segment.Apply(); });

            return segment;
        }

        /// <summary>오른쪽에 버튼 1~3개가 붙은 행([숨기기][보이기] / [지금 종료]).
        /// <para>★ 2026-09-03 — <paramref name="hotkey"/> 추가. 여기가 <b>유일하게</b>
        /// <see cref="BeginRow"/>에 <c>null</c>을 넘기던 부품이었고, 그래서 "이 동작에는 전역 단축키가
        /// 있다"를 말하려면 <b>토글 행을 따로 만드는 수밖에 없었다</b>. 그 토글이
        /// <i>"이 단축키를 켜고 끄는 스위치"</i>로 오독되어(페르소나 실측) 켜는 순간 캐릭터가 사라졌다.
        /// 기본값 <c>null</c>이라 기존 호출부의 동작은 <b>비트 단위로 같다</b>.</para></summary>
        public Image[] AddButtons(string key, string label, string[] captions, Action<int> clicked,
            string caption = null, bool enabled = true, DisabledReason disabledNote = default,
            SettingsRowGate gate = null, string hotkey = null)
        {
            RectTransform row = BeginRow(key, label,
                ComposeCaption(caption, enabled, disabledNote.Text, disabledNote.Kind), hotkey,
                enabled, gate, out float rowHeight, out Text labelText, out Text captionText);

            // ★ 상수 캡처(() => enabled)가 아니라 <b>지금 값</b>을 본다 — 게이트가 내려간 뒤에도 클릭이
            //   그대로 먹는 "회색인데 눌리는" 행을 만들지 않기 위해서다(AddSlider의 clickable과 같은 이유).
            //   C# 클로저는 로컬을 <b>참조로</b> 잡으므로 아래 게이트 람다의 대입이 그대로 보인다.
            bool rowInteractable = enabled;
            var buttonLabels = new Text[captions.Length];

            var results = new Image[captions.Length];
            float centerY = -(rowHeight - SettingsControls.ButtonHeight) * 0.5f;
            float x = 0f;
            for (int i = captions.Length - 1; i >= 0; i--)
            {
                // 실측으로 잡는 이유는 위 AddSegment의 주석 참고(옛 식: 26f + 글자수 × 9f).
                // ★ F1(2026-09-06) — 면 4.49:1 + 그 면에서 파생한 잉크 5.19:1. 짝으로 바꾼다.
                Image surface = UiChrome.AddSurface(row, "Btn" + i, SettingsControls.ControlFaceOnCard,
                    UiChrome.RadiusChip);
                UiChrome.AddOutline(surface.rectTransform, "Outline", SettingsControls.OutlineOnCard, UiChrome.RadiusChip);
                Text text = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontLabel,
                    TextAnchor.MiddleCenter,
                    UiChrome.InkOnSurface(SettingsControls.ControlFaceOnCard, UiChrome.InkRole.Title, enabled),
                    bold: true);
                // ★ 볼드다. 볼드는 같은 글자라도 폭이 넓으므로 <b>도색한 뒤에</b> 잰다 —
                //   순서를 바꾸면 재는 것과 그리는 것이 달라진다(그게 이 라운드가 고치는 병이다).
                float width = SettingsControls.ButtonPadX * 2f
                    + SettingsControls.MeasuredWidth(text, captions[i]);
                SettingsControls.PlaceTopRight(surface.rectTransform, x, centerY, width, SettingsControls.ButtonHeight);
                UiChrome.Stretch(text.rectTransform);
                results[i] = surface;
                buttonLabels[i] = text;

                int captured = i;
                _host?.Register(surface.rectTransform, surface, key + "." + i,
                    () => clicked?.Invoke(captured), null, () => rowInteractable);

                x += width + UiChrome.Space2;
            }

            // ★ 게이트가 내렸을 때의 모습은 <b>enabled:false로 태어난 행과 픽셀 단위로 같아야 한다</b>.
            //   그 행은 면(ControlFaceOnCard)을 그대로 두고 <b>잉크를 그 면에서 다시 뽑는다</b> — 여기서
            //   면까지 따로 어둡게 하면 같은 상태가 두 가지 모습을 갖고, 대비 하한 검사도 두 벌이 된다.
            //
            //   ★★ 2026-09-06 — 라벨을 <b>면과 함께</b> 등록한다(정책 §7-3 ★). 예전에는 Text만 넘겼고
            //     게이트가 거기에 InkTitle(Enabled)를 일괄 대입했다. 라벨이 카드 위에 있을 때는 그게
            //     옳았지만 F1로 면이 #838589가 되는 순간 그 대입은 <b>1.77 : 1</b>을 만든다 —
            //     2026-09-02 「1.28 : 1 사고」의 재발 형태 그대로다.
            //   ※ 이 면에서는 InkOnSurface가 활성/비활성 모두 같은 어두운 잉크(#0B1016 5.19:1)를
            //     돌려준다. 그게 옳다 — 이 면 위에 4.5:1을 넘는 잉크는 그 하나뿐이고, "못 쓴다"는
            //     행 라벨과 사유 한 줄이 말한다(이 창의 다른 컨트롤과 같은 규칙).
            var titleInk = new SettingsRowGate.GatedInk[buttonLabels.Length + 1];
            titleInk[0] = SettingsRowGate.GatedInk.OnCard(labelText);
            for (int i = 0; i < buttonLabels.Length; i++)
            {
                titleInk[i + 1] = new SettingsRowGate.GatedInk(buttonLabels[i],
                    SettingsControls.ControlFaceOnCard);
            }
            gate?.Register(titleInk, null, captionText, caption, onGate => rowInteractable = onGate);

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
                Surfaces = new Image[colors.Length],
                BaseColors = (Color[])colors.Clone(),   // 호출부가 나중에 배열을 고쳐도 원본이 흔들리지 않는다.
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
                // ★ F4(2026-09-06, 정책 §7-3/§4-3) — 이 테두리는 <b>견본 위</b>에 있다. 바탕이
                //   검정일 수도 흰색일 수도 있으므로 고정 토큰은 둘 중 하나에서 반드시 틀린다
                //   (옛 값 OutlineOnCard: 검정 견본 1.44 · 흰 견본 <b>1.00</b>). 방향까지 규칙이 고른다.
                Image border = UiChrome.AddOutline(surface.rectTransform, "Border",
                    UiChrome.EdgeOnSurface(colors[i]), UiChrome.RadiusChip, 2);

                swatches.Rects[i] = surface.rectTransform;
                swatches.Borders[i] = border;
                swatches.Surfaces[i] = surface;

                int captured = i;
                _host?.Register(surface.rectTransform, surface, key + "." + i,
                    () => swatches.SetIndexFromUser(captured), null, () => swatches.Interactable);

                x += SettingsControls.SwatchSize + SettingsControls.SwatchGap;
            }

            swatches.SetIndexSilently(index);
            return swatches;
        }

        /// <summary>
        /// ★ <b>글자를 직접 치는 행</b> — 2026-09-06 사용자 신고(<i>"캐릭터설정창에서 캐릭터 이름도
        /// 설정할수 있어야 하는데 안됨"</i>)로 신설. 지금 유일한 사용처는 [캐릭터] 탭의 이름이다.
        ///
        /// <para><b>왜 「값 → 누르면 입력칸」이 아니라 항상 입력칸인가</b>: 정보창의 인라인 편집이
        /// 정확히 그 형태였고, 그것이 이번 신고의 원인이었다 — 쉬는 상태가 <b>그냥 글자</b>라
        /// 눌러 볼 이유가 없었다. 입력칸을 늘 그려 두면 상태 기계도, 발견성 문제도 함께 사라진다.</para>
        ///
        /// <para><b>uGUI <see cref="Button"/>을 붙이지 않는다</b>(<c>target: null</c>). <see cref="InputField"/>는
        /// 그 자체가 <c>Selectable</c>이라 uGUI 클릭은 스스로 처리한다. 같은 오브젝트에 Button을 얹으면
        /// 두 Selectable이 같은 누름을 다투고, 캐럿을 찍으려는 클릭이 포커스 토글로 먹힌다.
        /// 대신 이 창의 <b>전역 폴링</b> 경로에는 등록한다 — 앱이 아직 활성화되지 않은 첫 클릭은
        /// uGUI에 도착하지 않기 때문이다(이 창의 다른 부품과 같은 사정).</para>
        ///
        /// <para><paramref name="committed"/>는 <b>Enter 또는 포커스 이탈</b>에서 한 번 불린다
        /// (<c>onEndEdit</c>). 값 정규화는 여기서 하지 않는다 — 그것은 모델의 일이고, 호출부가
        /// 정규화된 결과를 <see cref="SettingsTextField.SetTextSilently"/>로 되돌려 적는다.</para>
        /// </summary>
        public SettingsTextField AddTextField(string key, string label, string value, string placeholder,
            int characterLimit, Action<string> committed, string caption = null)
        {
            RectTransform row = BeginRow(key, label, caption, null, true, out float rowHeight);

            // ★★ F2(2026-09-06, 정책 §7-3) — <b>면은 올리지 않는다.</b> 실측이 반대로 말한다:
            //   이 면을 F1 슬래브(#838589)로 올리면 입력 글자가 11.10 → 3.34, 플레이스홀더가
            //   3.94 → <b>1.18</b>로 무너진다. 입력칸에서 부족한 것은 글자가 아니라
            //   <b>"상자가 있다는 사실"</b>이고, 그건 테두리의 일이다.
            Image surface = UiChrome.AddSurface(row, "Field", SettingsControls.ButtonSurfaceOnCard,
                UiChrome.RadiusChip);
            SettingsControls.PlaceTopRight(surface.rectTransform, 0f,
                -(rowHeight - SettingsControls.TextFieldHeight) * 0.5f,
                SettingsControls.TextFieldWidth, SettingsControls.TextFieldHeight);
            // 테두리는 옛 1.66에서 <b>EdgeOnSurface가 정한 값</b>으로, 두께는 1 → <b>2</b>.
            // 두께를 올리는 이유: 배율 1.0에서 1pt는
            // 물리 1픽셀이고, Windows 125/150/175%에서는 반 픽셀에 걸린다(견본 테두리가 이미 2다).
            UiChrome.AddOutline(surface.rectTransform, "Outline",
                UiChrome.EdgeOnSurface(UiChrome.CardSurface), UiChrome.RadiusChip, 2);

            Text text = UiChrome.AddText(surface.rectTransform, "Text", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(UiChrome.Space2, 0f);
            text.rectTransform.offsetMax = new Vector2(-UiChrome.Space2, 0f);
            text.supportRichText = false;

            Text hint = UiChrome.AddText(surface.rectTransform, "Placeholder", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.InkMeta);
            UiChrome.Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(UiChrome.Space2, 0f);
            hint.rectTransform.offsetMax = new Vector2(-UiChrome.Space2, 0f);
            hint.text = placeholder ?? string.Empty;
            hint.fontStyle = FontStyle.Italic;

            var input = surface.gameObject.AddComponent<InputField>();
            input.targetGraphic = surface;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = characterLimit;
            input.lineType = InputField.LineType.SingleLine;

            var field = new SettingsTextField { Input = input, Rect = surface.rectTransform };
            field.SetTextSilently(value);
            input.onEndEdit.AddListener(v => committed?.Invoke(v));

            _host?.Register(surface.rectTransform, null, key, () => field.Focus());
            return field;
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
