using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 창 UI의 <b>디자인 토큰 + 부품 공장</b> — 2026-08-30 리더 지시
    /// ("캐릭터는 간단하지만 캐릭터창은 깔끔하고 요즘 게임 캐릭터창처럼 좋아야해").
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼는가
    /// ============================================================================
    /// 지금까지 이 프로젝트의 uGUI(우클릭 메뉴 / 투두 포스트잇 / 구 정보창)는 <b>직각 사각형 + 그때그때
    /// 고른 알파값</b>으로 만들어졌다. 색과 여백이 파일마다 조금씩 달라 "정돈된 화면"이 나올 수 없는
    /// 구조였다. 그래서 색/여백/글자 크기를 <b>토큰</b>으로 한 곳에 못박고, 둥근 모서리 같은 부품을
    /// 여기서만 만든다. 앞으로 창을 추가하는 사람은 색을 고르지 말고 여기서 <b>가져다 써야</b> 한다.
    ///
    /// ============================================================================
    /// 둥근 모서리를 어떻게 만드는가 — 스프라이트 에셋이 없는 프로젝트에서
    /// ============================================================================
    /// 이 앱에는 아트 에셋이 하나도 없다(모든 그림이 프로시저럴 선화다). 그래서 둥근 사각형 스프라이트를
    /// <b>런타임에 한 번 구워</b> 9-슬라이스로 늘려 쓴다. 반지름/테두리 두께 조합마다 32~40px짜리 작은
    /// 텍스처 하나이고, <see cref="_cache"/>에 담아 앱 수명 동안 재사용한다(24시간 상주 앱 — 매번 굽지
    /// 않는다). 알파는 경계에서 1px 부드럽게 떨어뜨려 Retina에서도 계단이 보이지 않는다.
    ///
    /// ============================================================================
    /// 톤 — "깔끔하고 정돈된" 쪽에 방점 (리더 지시)
    /// ============================================================================
    /// 이 앱 전체가 손그림 톤이라 UI가 화려하면 붕 뜬다. 그래서 채도가 낮은 표면에
    /// <b>강조색 하나</b>(<see cref="Accent"/>)만 쓰고, 층은 1px 테두리와 표면 밝기 차로만 낸다.
    /// 그라데이션/광택/네온은 쓰지 않는다. 그림자도 쓰지 않는다(2026-09-02 사용자 지시 — 아래 제거 노트).
    ///
    /// <b>2026-08-30 2차 팔레트 교체(docs/UX_FLOW.md 34-1)</b>: 오전에 넣은 33-1 팔레트(종이빛 회색 +
    /// 테라코타)는 <b>폐기</b>하고 <b>다크 글로스</b>(#14171c 유리 + #5da1f5 파랑 강조)로 전면 교체했다.
    /// <b>상수 이름은 하나도 바뀌지 않았다 — 값만 갈아끼웠다.</b> 그래서 여기를 쓰는 모든 표면
    /// (기어 부채꼴/팝오버 2종/캐릭터 창)이 <b>호출부 무수정으로</b> 함께 갈아입는다 — 이 파일의
    /// 존재 이유이자 34-1의 의도다. 예외는 <see cref="PortraitSurface"/> 하나뿐이고 그 이유는
    /// 그 토큰의 주석에 적혀 있다.
    /// </summary>
    public static class UiChrome
    {
        // ==================== 여백 체계 (4의 배수, 5단계) ====================
        public const float Space1 = 4f;
        public const float Space2 = 8f;
        public const float Space3 = 12f;
        public const float Space4 = 16f;
        public const float Space6 = 24f;

        // ==================== 모서리 반지름 (34-2: 릴스 카드가 폭 대비 약 5%로 둥글다) ==========
        public const int RadiusPanel = 14;
        public const int RadiusCard = 12;
        public const int RadiusChip = 6;   // 버튼/칩.
        public const int RadiusThumb = 6;  // 카드 썸네일 영역.
        public const int RadiusBadge = 5;  // 자물쇠 배지.
        public const int RadiusDot = 2;    // 카테고리 틴트 도트 / 게이지 트랙.

        // ====================================================================================
        // ★★★ 이 파일을 고치기 전에 반드시 읽을 것 — <b>알파 채널의 법칙</b> (2026-08-31)
        // ====================================================================================
        //
        // 이 앱의 창은 <b>전체화면 투명 오버레이</b>다. 카메라는 배경을 (0,0,0,<b>알파 0</b>)으로 지운다
        // (MacOverlayStateEnforcer.ApplyTransparentSafeCameraBackground / SceneBootstrapper).
        // 즉 <b>프레임버퍼의 알파 채널이 그대로 OS 합성기의 마스크</b>가 된다 — 알파가 0.6인 화소에는
        // 유저의 진짜 데스크톱이 40% 비쳐 든다. 보통의 게임 UI에서는 알파가 "예쁨"이지만 여기서는
        // <b>"뒤 창이 얼마나 보이는가"</b>다.
        //
        // 그리고 uGUI의 기본 셰이더(UI/Default)는 이렇게 섞는다:
        //
        //     Blend SrcAlpha OneMinusSrcAlpha          ← RGB<b>와 알파에 똑같이</b> 적용된다
        //     => dstA' = srcA*srcA + dstA*(1 - srcA)   ← srcA + dstA*(1-srcA) 가 <b>아니다</b>
        //
        // 여기서 두 가지 반직관적인 결과가 나온다. 둘 다 실제 버그를 냈다:
        //
        //   (1) 알파는 <b>제곱된다</b>. α0.96짜리 판 하나를 알파 0 위에 그리면 화면 알파는 0.96이
        //       아니라 0.9216이다(비침 4%가 아니라 7.8%).
        //   (2) 알파는 <b>줄어들 수 있다</b>. 이미 알파 0.92인 자리에 α0.55짜리 검은 그림자를 덮으면
        //       0.55² + 0.92×0.45 = <b>0.717</b>로 <b>내려간다</b>. 반투명 겹을 아무리 쌓아도
        //       "점점 불투명해지는" 일은 일어나지 않는다.
        //
        // 그래서 이 파일에는 두 가지 규칙이 있다:
        //
        //   · <b>큰 창의 바탕(PanelSurface)은 α=1이다.</b> α<1 유리는 "내 앱의 다른 부분"이 뒤에 있을
        //     때만 성립하는 연출인데, 우리 패널 뒤에는 유저의 다른 창이 있다(원칙 2의 관점에서도
        //     "남의 창을 반투명 필터로 덮은 화면"은 우리가 팔 그림이 아니다).
        //   · <b>어두운 반투명 겹을 본체 위에 깔지 않는다.</b> 위 (2) 때문에 그런 겹 하나가 창 알파를
        //     통째로 무너뜨린다. uGUI는 <b>부모 Graphic을 자식보다 먼저</b> 그리므로 "Image가 붙은
        //     오브젝트의 자식으로 넣으면 본체 <b>위</b>로 간다". 이 함정의 유일한 실사용처였던 그림자는
        //     2026-09-02에 전부 삭제됐다(아래 제거 노트).
        //
        // ==================== 색 — 2026-08-30 다크 글로스 팔레트 (docs/UX_FLOW.md 34-1) ==========
        // ★ 같은 날 오전의 33-1 팔레트(종이빛 회색 + 테라코타)는 <b>폐기</b>했다. 이 파일을 쓰는 모든
        //   표면(기어 부채꼴 / 집중 모드 팝오버 / 할일 팝오버 / 캐릭터 창)이 함께 다크 글로스로
        //   갈아입는다 — 34-1이 못박은 <b>의도한 결과</b>다.
        //   값은 이 프로젝트 관례대로 hex/255 그대로(감마 공간) 넣는다.

        /// <summary>모달 딤(#0a0c10). <b>이 앱에서는 화면 전체를 덮는 용도로 쓰지 않는다</b> — 유저의
        /// 작업 화면을 통째로 가리면 비침해 원칙 2 정면 위반이다(33-7-7). 토큰만 남겨 두는 이유는
        /// 훗날 덮어도 되는 화면(온보딩 등)이 생겼을 때 색을 새로 고르지 않게 하기 위해서다.</summary>
        public static readonly Color ScreenScrim = new Color(0.039f, 0.047f, 0.063f, 1f);

        /// <summary>
        /// 창 바탕(#14171c, <b>α = 1.0</b>).
        ///
        /// <para>★★ 2026-08-31 회귀 수정 — 사용자 신고 "창이 여러 개로 겹쳐 보인다"(스크린샷: 정보창
        /// 뒤로 날씨 위젯의 파란 그라데이션과 <c>24°</c>가 그대로 읽힘). 원인은 이 한 글자,
        /// <b>α 0.96</b>이었다. 34-2가 "유리 판정 단서 (a) 뒤가 살짝 비침"으로 넣은 값인데,
        /// <b>이 앱에서는 패널 뒤에 우리 콘텐츠가 없다 — 유저의 진짜 데스크톱이 있다.</b>
        /// 일반 앱에서 유리는 "내 앱의 다른 부분"을 비추지만, 전체화면 투명 오버레이에서는
        /// 곧바로 <b>남의 창이 비친다</b>. 즉 알파 유리는 이 아키텍처에서 성립하지 않는다.
        /// (b)(c) 시인/하이라이트는 그대로 남는다((d) 그림자는 2026-09-02에 삭제됐다).</para>
        ///
        /// <para>왜 오전 팔레트(α0.985, 밝은 회색)에서는 아무도 못 봤나: 비침 자체는 그때도 있었다
        /// (실측 시뮬레이션 결과 약 16%). 다만 <b>밝은 표면</b>은 뒤에서 새어 들어온 밝은 화소를 거의
        /// 가려서 체감 밝기 변화가 12%뿐이었다. 34-1에서 표면이 <b>어두워지자</b> 같은 비침이
        /// 체감 549%로 증폭됐다(어두운 바탕은 가릴 밝기 자체가 없다). <b>팔레트를 어둡게 바꾸는 것은
        /// 알파를 그대로 두어도 되는 변경이 아니다</b> — 이 문장이 이번 회귀의 교훈이다.</para>
        ///
        /// <para>대비표(34-1: TextSecondary 7.4:1)는 α=1에서 계산된 값이라 오히려 이제야 성립한다.</para>
        /// </summary>
        public static readonly Color PanelSurface = new Color(0.078f, 0.090f, 0.110f, 1f);

        /// <summary>패널 상단 시인(sheen) 시작색 rgba(255,255,255,0.10) — 34-2 (3)겹.
        /// "위쪽이 더 밝다"는 유리 판정 단서를 블러 없이 만든다.</summary>
        public static readonly Color PanelSheen = new Color(1f, 1f, 1f, 0.10f);

        /// <summary>패널 안쪽 상단 1px 하이라이트 rgba(255,255,255,0.30) — 34-2 (4)겹(굴절 테두리).</summary>
        public static readonly Color PanelHighlight = new Color(1f, 1f, 1f, 0.30f);

        /// <summary>패널 보더 rgba(255,255,255,0.16).</summary>
        public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.16f);

        /// <summary>카드/입력칸 표면(#1b1f26) — 바탕보다 <b>밝게</b> 띄워 층이 읽히게 한다(다크에서도 규칙은 같다).</summary>
        public static readonly Color CardSurface = new Color(0.106f, 0.122f, 0.149f, 1f);

        /// <summary>구획 보더 rgba(255,255,255,0.10).</summary>
        public static readonly Color CardBorder = new Color(1f, 1f, 1f, 0.10f);

        /// <summary>비활성/잠긴 카드 표면 + 썸네일 기본 배경(#15181e).</summary>
        public static readonly Color CardSurfaceMuted = new Color(0.082f, 0.094f, 0.118f, 1f);

        /// <summary>잠긴 카드의 썸네일 배경(#101318). 다크에서는 <b>어두운 쪽으로</b> 한 단 더 내려앉는다(34-7 #5).</summary>
        public static readonly Color ThumbSurfaceLocked = new Color(0.063f, 0.075f, 0.094f, 1f);

        /// <summary>아바타 액자 배경(#e9eae6).
        /// <para>★ 이 토큰만 밝은 값으로 남는 것은 <b>실수가 아니라 필수 예외</b>다(34-1). 캐릭터 잉크는
        /// 기본이 검정이라 액자 바탕까지 어두워지면 초상화가 통째로 사라진다.
        /// <see cref="CharacterPortraitStage.ResolveBackdropColor"/>가 "흰 잉크면 목탄 바탕, 아니면 종이
        /// 바탕"으로 이미 뒤집으므로 그 분기는 그대로 두고 종이 값만 새 팔레트에 맞춰 식혔다.</para></summary>
        public static readonly Color PortraitSurface = new Color(0.914f, 0.918f, 0.902f, 1f);

        /// <summary>상세 패널 등 보조 표면(#191d24).</summary>
        public static readonly Color SubtleSurface = new Color(0.098f, 0.114f, 0.141f, 1f);

        /// <summary>착용 중인 카드의 테두리 rgba(93,161,245,0.55) — 강조색 계열로 "지금 걸치고 있음"을 말한다.</summary>
        public static readonly Color CardBorderWorn = new Color(0.365f, 0.631f, 0.961f, 0.55f);

        /// <summary>커서가 올라간 카드의 테두리 rgba(255,255,255,0.24).</summary>
        public static readonly Color CardBorderHover = new Color(1f, 1f, 1f, 0.24f);

        /// <summary>강조색(#5da1f5). 릴스 실측에서 <b>색상(213°)만</b> 뽑고 명도는 우리 대비 계산으로
        /// 다시 정한 값이다(34-0 #3). 이 팔레트에도 강조색은 <b>하나뿐</b>이다.</summary>
        public static readonly Color Accent = new Color(0.365f, 0.631f, 0.961f, 1f);

        /// <summary>강조색의 옅은 채움(선택 칩 배경 등) rgba(93,161,245,0.14).</summary>
        public static readonly Color AccentSurface = new Color(0.365f, 0.631f, 0.961f, 0.14f);

        /// <summary>강조색 테두리 rgba(93,161,245,0.55).</summary>
        public static readonly Color AccentBorder = new Color(0.365f, 0.631f, 0.961f, 0.55f);

        /// <summary>글로우의 <b>흰 코어</b>(#e6f1ff). 릴스의 눈금은 "파란 헤일로 + 흰 코어" 2층 구조였다
        /// (34-0 실측). 발광 표현이 필요한 곳은 <see cref="Accent"/> 저알파 헤일로 위에 이 색을 얹는다.</summary>
        public static readonly Color AccentGlowCore = new Color(0.902f, 0.945f, 1.000f, 1f);

        /// <summary>
        /// "주의를 끄는 색"이 필요한 자리(집중 타이머 링/전체화면 안내)의 이름. 값은 <see cref="Accent"/>와
        /// <b>같다</b> — 이 팔레트도 강조색이 하나이고, 두 번째 강조색을 우리가 발명하면 그 색만
        /// 어떤 표에도 근거가 없는 값이 된다. 호출부의 <i>의도</i>를 지우지 않으려고 이름만 남긴다.
        /// </summary>
        public static readonly Color WarmAccent = new Color(0.365f, 0.631f, 0.961f, 1f);

        // ==================== 잉크 위계 (UI_SURFACE_SPEC §2.3 / §2.4 / §11 — 2026-09-01) ==========
        //
        // ★ 이 앱의 "글자가 흐리다"는 폰트도 렌더링도 아니라 <b>색</b>이었다. 심야 실측 16행에서
        //   이름은 15~18:1로 빛나는데 그 이름의 뜻을 알려 주는 문장이 2.1~3.8:1이었다(배수 5배).
        //   위계가 <b>굵기</b>가 아니라 <b>지워짐</b>으로 표현되고 있었고, 미달 6행이 나왔다.
        //
        // 그래서 여기서 두 가지를 동시에 못 박는다.
        //   (1) <b>단계</b>는 이 아래 네 개뿐이다. 새 단계를 만들지 마라 — 4.52와 3.80처럼 19%밖에
        //       차이 안 나는 두 단은 위계를 만들지 못하면서 가독성만 갉아먹었다(그래서 폐기됐다).
        //   (2) <b>콜사이트는 색을 고르지 않는다.</b> 역할(<see cref="InkRole"/>)과 활성 여부만 말하고
        //       색은 <see cref="Ink"/>가 정한다. 이름과 설명이 <b>같은 함수</b>에서 나오므로
        //       "비활성이 되는 순간 이름이 설명보다 흐려지는" 역전이 물리적으로 불가능해진다 —
        //       그 역전은 설정창과 행동창에서 <b>서로 모르는 채 독립적으로</b> 났다. 국소 실수가
        //       아니라 규칙의 부재였다.

        /// <summary>T1 — 이름/제목(#f2f4f7).</summary>
        public static readonly Color TextPrimary = new Color(0.949f, 0.957f, 0.969f, 1f);      // #f2f4f7

        /// <summary>T2 — 본문, 그리고 <b>비활성 이름</b>(#aeb4bf).</summary>
        public static readonly Color TextSecondary = new Color(0.682f, 0.706f, 0.749f, 1f);    // #aeb4bf

        /// <summary>T3 — 캡션/메타/설명. <b>글자의 마지막 단이자 하한</b>이다(#8b939f).</summary>
        public static readonly Color TextTertiary = new Color(0.545f, 0.576f, 0.624f, 1f);     // #8b939f

        /// <summary>
        /// <b>글자가 아닌</b> 옅은 기호·도트·자물쇠 배지의 잉크(#6c7480).
        /// <para>★ 이 색으로 <b>글자를 그리지 마라.</b> 비텍스트 하한(3:1)은 넘지만 본문 하한(4.5:1)에는
        /// 못 미친다 — 옛 이름이 <c>TextQuaternary</c>였고, 그 이름 때문에 슬롯코드·카운터·푸터·비활성
        /// 이유가 전부 3.4~4.0:1로 그려졌다. 이름이 틀리면 콜사이트가 따라 틀린다.</para>
        /// </summary>
        public static readonly Color NonTextMuted = new Color(0.424f, 0.455f, 0.502f, 1f);     // #6c7480

        /// <summary>
        /// <b>꺼진 컨트롤</b>의 채움(스위치 손잡이·슬라이더 채움 등)(#4b525c).
        /// <para>★ 글자 금지. 2.1:1이라 글자로 쓰면 사실상 존재하지 않는다 — 실제로 "화면에 한 글자도
        /// 없다"는 신고가 이 색으로 그려진 라벨에서 나왔다. 꺼졌다는 사실은 <b>컨트롤과 행 바탕</b>이
        /// 말한다(글자가 말하지 않는다). WCAG 1.4.11도 비활성 컨트롤은 대비 요구에서 제외한다.</para>
        /// </summary>
        public static readonly Color DisabledControlInk = new Color(0.294f, 0.322f, 0.361f, 1f); // #4b525c

        /// <summary>아이콘 기본 잉크(#d6dbe3). 다크에서는 잉크가 <b>밝은 쪽</b>이다.</summary>
        public static readonly Color IconInk = new Color(0.839f, 0.859f, 0.890f, 1f);

        /// <summary>
        /// 글자의 역할. <b>크기가 아니라 서열</b>이다 — 한 덩어리(행/타일/카드) 안에서
        /// <see cref="InkRole.Title"/> ≥ <see cref="InkRole.Body"/> ≥ <see cref="InkRole.Meta"/> 순서는
        /// 활성/비활성 어느 쪽에서도 뒤집히지 않는다.
        /// </summary>
        public enum InkRole
        {
            /// <summary>이름·제목·값처럼 "이게 무엇인가"를 말하는 글자.</summary>
            Title,
            /// <summary>설명 본문.</summary>
            Body,
            /// <summary>캡션·메타·슬롯코드·카운터, 그리고 <b>비활성 이유 문장</b>.</summary>
            Meta,
        }

        /// <summary>
        /// 역할 + 활성 여부 → 잉크. <b>이 앱에서 글자 색을 정하는 유일한 자리다.</b>
        ///
        /// <para><b>비활성 규칙</b>: 한 단만 내린다. 절대 두 단 내리지 않고, 행 안의 상대 순서도
        /// 바꾸지 않는다. 그래서 비활성 표는 활성 표를 그대로 한 칸 민 모양이다.</para>
        /// <code>
        ///            활성        비활성
        ///   Title    T1 15.0  →  T2 7.9
        ///   Body     T2  7.9  →  T3 5.3
        ///   Meta     T3  5.3  →  T3 5.3   ← 하한. 더 내릴 곳이 없다
        /// </code>
        /// <para><b>Meta가 안 내려가는 것은 실수가 아니다.</b> 비활성 행에서 가장 중요한 글자는
        /// "왜 못 쓰는가"를 말하는 그 한 줄이다. 그걸 흐리면 유저는 "뭔가 준비 중이구나"만 읽고
        /// "뭐가?"는 영영 못 읽는다.</para>
        /// </summary>
        public static Color Ink(InkRole role, bool enabled)
        {
            switch (role)
            {
                case InkRole.Title: return enabled ? TextPrimary : TextSecondary;
                case InkRole.Body: return enabled ? TextSecondary : TextTertiary;
                default: return TextTertiary;
            }
        }

        /// <summary>이름/제목/값의 잉크. 비활성이면 한 단 내려간다.</summary>
        public static Color InkTitle(bool enabled) => Ink(InkRole.Title, enabled);

        /// <summary>설명 본문의 잉크. 비활성이면 한 단 내려간다.</summary>
        public static Color InkBody(bool enabled) => Ink(InkRole.Body, enabled);

        /// <summary>캡션·메타·비활성 이유의 잉크. <b>활성 여부를 받지 않는 것이 규칙 그 자체다</b> —
        /// 이 글자는 어떤 상태에서도 흐려지지 않는다.</summary>
        public static Color InkMeta => Ink(InkRole.Meta, true);

        /// <summary>
        /// 탭 라벨의 잉크. 고른 탭은 <see cref="InkRole.Title"/>과 같은 서열을 쓰고
        /// (준비 안 된 탭이면 한 단 내려간다), 고르지 않은 탭은 <see cref="InkMeta"/> 단이다.
        /// <para>★ 고르지 않은 탭은 "준비 중"이어도 더 내리지 않는다. 옛 코드는 여기서 2.35:1까지
        /// 내려갔고, 그 결과 페르소나가 "화면에 한 글자도 없다"고 적었다 — 글자는 있었다.
        /// 준비 중이라는 사실은 <b>밑줄과 탭 내용</b>이 말한다.</para>
        /// </summary>
        public static Color InkTab(bool selected, bool ready = true)
            => selected ? Ink(InkRole.Title, ready) : InkMeta;

        /// <summary>아이콘 잉크. 비활성 아이콘도 <b>보여야 한다</b>(비텍스트 하한 3:1) —
        /// 못 쓴다는 사실은 옆의 이유 한 줄과 타일 바탕이 말한다.</summary>
        public static Color InkIcon(bool enabled) => enabled ? IconInk : TextTertiary;

        /// <summary>
        /// ★ <b>폐기된 잉크</b>(2026-09-01, UI_SURFACE_SPEC §11.4). <b>프로덕션에서 참조하지 마라</b> —
        /// <c>UiInkHierarchyTests</c>가 소스를 훑어 위반을 잡는다.
        /// <para>값을 지우지 않고 남기는 이유는 하나뿐이다: 회귀 테스트의 <b>네거티브 컨트롤</b>.
        /// "옛 값으로 되돌리면 실제로 빨개지는가"를 증명하지 못하면 그 초록은 초록이 아니다.</para>
        /// </summary>
        public static class RetiredInk
        {
            /// <summary>옛 <c>TabInactive</c>(#79808c, 4.15:1 on card). 어디에도 남아 있지 않은 값이다.</summary>
            public static readonly Color TabInactive = new Color(0.475f, 0.502f, 0.549f, 1f);

            /// <summary>옛 <c>TextQuaternary</c>. 값 자체는 <see cref="NonTextMuted"/>로 살아 있다 —
            /// 바뀐 것은 <b>글자에 쓸 수 없다</b>는 규칙이다.</summary>
            public static Color Quaternary => NonTextMuted;

            /// <summary>옛 <c>TextDisabled</c>. 값 자체는 <see cref="DisabledControlInk"/>로 살아 있다.</summary>
            public static Color Disabled => DisabledControlInk;
        }

        /// <summary>옅은 <see cref="AccentSurface"/> 위에 얹는 글자(#8fc3ff) — 같은 색조의 밝은 값.</summary>
        public static readonly Color TextOnAccent = new Color(0.561f, 0.765f, 1.000f, 1f);

        /// <summary><b>밝은</b> 채움(<see cref="TextPrimary"/>/<see cref="Accent"/>) 위에 얹는 글자/기호(#0b1016).
        /// ★ 33-1에서 반전됐다 — 강조색이 밝은 파랑이 됐으므로 그 위 글자는 어두워야 한다(34-7 #3).</summary>
        public static readonly Color OnAccentSolid = new Color(0.043f, 0.063f, 0.086f, 1f);

        /// <summary>게이지 트랙 rgba(255,255,255,0.09).</summary>
        public static readonly Color TrackBackground = new Color(1f, 1f, 1f, 0.09f);

        /// <summary>얇은 구분선 rgba(255,255,255,0.07) — <see cref="CardBorder"/>보다 한 단 더 옅다.</summary>
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.07f);

        // ==================== 착용 카테고리 틴트 (핸드오프) ====================
        // 8개 카테고리에 4가지 색이 두 번 돌아간다(장비 계열 4 + 외형 계열 4). 카테고리 → 색 매핑을
        // 여기서만 한다 — 카드/아이콘/상세 패널이 각자 색을 고르면 같은 카테고리가 화면마다 다른 색이 된다.
        // ★ 34-1: 어두운 바탕에서 33-1의 값(#c4622d 등)은 진흙색으로 가라앉는다 — 명도를 올렸다.
        //   EYES/HAIR만 파랑에서 <b>청록으로 이동</b>했다: 신규 Accent(#5da1f5)와 같은 색상대라
        //   "강조"와 "카테고리"가 구분되지 않기 때문이다.
        private static readonly Color[] _categoryTints =
        {
            new Color(0.910f, 0.514f, 0.290f, 1f),   // #e8834a 살구빛 주황 — HEAD / FACE
            new Color(0.310f, 0.753f, 0.776f, 1f),   // #4fc0c6 청록 — EYES / HAIR
            new Color(0.549f, 0.753f, 0.431f, 1f),   // #8cc06e 연둣빛 초록 — NECK / FX
            new Color(0.690f, 0.561f, 0.816f, 1f),   // #b08fd0 라벤더 — BACK / PET
        };

        /// <summary>이 카테고리의 대표색. 장비 계열(0~3)과 외형 계열(4~7)이 같은 4색을 나눠 쓴다 —
        /// 두 계열이 같은 자리에 대응한다는 것을 색으로 읽히게 한 핸드오프의 의도다.</summary>
        public static Color CategoryTint(StickMate.Core.EquipmentSlot slot)
            => _categoryTints[(int)slot & 3];

        /// <summary>틴트를 <b>넓은 면</b>에 깔 때(카드 썸네일 배경 등) 쓰는 옅은 채움.
        /// <para>알파 <b>30/255</b>. 34-1은 46/255를 제안했지만 실제로 찍어 보고 낮췄다 — 그 표가 쓰이는
        /// 유일한 자리인 "착용 중 카드 썸네일"에 <b>색이 있는 아이콘</b>이 얹히기 때문이다(같은 라운드에
        /// 아이템별 소재색이 들어왔다). 46/255에서는 초록 넥타이 아이콘이 초록 wash에 묻혀 형태가
        /// 사라졌다. 33-1의 26/255가 밝은 바탕 전용이었던 것과 정확히 대칭인 이유로 46은 "아이콘이
        /// 단색이던 시절 전용" 값이다. 착용 표시는 이미 테두리·메타 문구·wash 셋이 함께 말한다.</para></summary>
        public static Color TintWash(Color tint) => new Color(tint.r, tint.g, tint.b, 30f / 255f);

        // ==================== 타이포그래피 위계 (33-1: 크기 위계만 채택, 서체는 내장 폰트 하나) ====
        //
        // ★★ 2026-09-01 — <b>전부 짝수 pt다. 홀수로 되돌리지 마라.</b>
        //   사용자 신고(Windows 실기 사진): "텍스트도 다 번져보임". 원인은 폰트도 필터도 아니라
        //   <b>캔버스 배율 1.5</b>(Windows 디스플레이 150% = GetDpiForWindow 144/96)다. 레거시 uGUI
        //   <c>Text</c>는 글리프를 <c>round(pt × 배율)</c>px로 <b>한 번 굽고</b> 화면에는
        //   <c>pt × 배율</c>로 올린다. 홀수 pt는 그 둘이 달라(13pt → 19.5 요청 / 20px에 구움)
        //   비정수 배로 리샘플되고 획이 이웃 픽셀로 샌다. 짝수 pt는 <c>pt × 1.5</c>가 정수라 잔차 0이다.
        //   판정 규칙과 "왜 배율을 정수로 스냅하면 안 되는가"는 <c>Platform/UiGlyphScalePolicy.cs</c>에
        //   있고, <c>Tests/EditMode/UiGlyphExactnessAuditTests</c>가 이 다섯 줄을 소스에서 감시한다.
        //   <b>macOS는 무영향</b>: Retina 배율이 정수 2(또는 1)라 어떤 정수 pt도 이미 잔차가 없다.
        public const int FontDisplay = 20;   // 캐릭터 이름.   (19 -> 20: 28.5px -> 30px)
        public const int FontTitle = 14;     // 창 제목 / 탭 / 상세 이름. (13 -> 14: 19.5px -> 21px)
        public const int FontBody = 12;      // 본문 / 카드 이름 / 스탯.
        public const int FontLabel = 12;     // 라벨 / 부제.   (11 -> 12: 16.5px -> 18px)
        public const int FontCaption = 10;   // 캡션 / 슬롯코드 / 카운트 / 카드 메타.
        // ★ 스펙의 9.5px(카드 메타)은 채택하지 않는다 — 내장 비트맵 폰트에서 9pt 이하 한글은
        //   Retina가 아닌 디스플레이에서 획이 뭉갠다. 이 프로젝트는 이미 10pt를 하한으로 쓴다.
        // ★ <b>Label이 Body와 같은 12pt가 된 것은 의도다</b>. 배율 1.5에서 안전한 pt는 짝수뿐이라
        //   10/12/14/20의 네 계단만 남고, 11과 12를 따로 둘 자리가 없다. 둘의 구분은 이제 크기가
        //   아니라 <b>색</b>(TextSecondary/TextTertiary)과 굵기가 진다 — 어차피 1pt 차이는 화면에서
        //   위계로 읽히지 않았다. 이름을 합치지 않는 이유는 호출부 26곳의 <i>의도</i>를 지우지 않기
        //   위해서다(WarmAccent가 Accent와 같은 값이면서 이름이 남아 있는 것과 같은 이유).

        // ============================================================================
        // ★★ 창을 닫는 법 — 세 표면(정보창 · 설정창 · 팝오버 3종)의 <b>단일 근거지</b>
        // ============================================================================
        //
        // ★ 2026-09-02 밤 — 실측: 정보창을 연 상태에서 <c>Cmd+W</c>를 누르면 <b>뒤에 있던 Finder 창이
        //   닫혔다</b>. 우리 창은 layer=101 전체화면 <b>1장뿐</b>이고 정보창/설정창/팝오버는 그 안에
        //   그려진 그림이라 키보드 포커스를 못 받는다 — 키는 밑에 있는 남의 앱으로 간다.
        //   키를 가로채는 방향은 <b>검토하지 않는다</b>(포커스를 받으면 클릭관통이 깨져 원칙 2가 무너진다).
        //   그래서 이 앱에서 표면을 닫는 <b>마우스 경로는 [✕] 하나</b>다.
        //
        // ★★ 2026-09-02 사용자 지시 — <b>창 밖 클릭은 닫지 않는다</b>
        //   원문: "캐릭터창이나 다른 메뉴창들이 떠있을때 바탕화면을 클릭하면 꺼지는데 안꺼지고
        //   사용자가 닫기전에는 안꺼져야함". 2026-08-30에 33-7-9 ③으로 신설했던 "창 밖 클릭" 탈출구를
        //   세 표면에서 <b>동시에</b> 걷어냈다(정보창 FeedClick / 설정창 FeedClick / PopoverPanel.FeedClick).
        //
        //   <b>같은 날 넣었던 안내 문구도 함께 걷었다.</b> 동작을 없애 놓고 "창 밖을 클릭해도 닫혀요"를
        //   남기면 화면이 거짓말을 한다 — 그건 이 프로젝트가 원칙 1로 금지한 표시/실제 불일치다.
        //
        // ★ 무엇을 대가로 치르는가(원칙 2, 정직하게)
        //   창/팝오버가 열려 있는 동안 그 <b>패널 사각형</b>에는 클릭 차단막(isTrigger BoxCollider2D)이
        //   깔린다. 종전에는 "탈출 비용이 1클릭"이라 차단막의 수명에 사실상 상한이 있었지만, 이제
        //   <b>사용자가 [✕]를 누를 때까지 남는다</b>. 그 대신:
        //     · 차단막은 <b>패널 사각형 그대로</b>이고 한 픽셀도 넓지 않다. 그 밖은 100% 관통이다.
        //     · 창 밖 클릭은 우리가 <b>먹지 않는다</b> — 밑의 앱에 그대로 전달된다(콜라이더가 없으므로
        //       hitTestType=Raycast가 관통시킨다). "안 닫히는 것"과 "클릭을 뺏는 것"은 다른 문제다.
        //     · 팝오버 3종에는 무입력 자동 닫힘(PopoverPanel.DefaultIdleAutoCloseSeconds = 180초)이
        //       <b>살아 있다</b>. 정보창/설정창에는 그런 상한이 <b>없다</b> — 알고 치르는 대가다.
        //     · 전체화면 게임 감지 자동 숨김은 이 변경과 무관하게 그대로다(원칙 2).
        //
        // ★ 부채꼴(GearRadialMenu)은 예외다 — 그것은 "창"이 아니라 "메뉴"다.
        //   [✕]도 타이틀바도 없어서 바깥 클릭을 막으면 <b>즉시 닫을 수단이 사라진다</b>. 게다가 그쪽은
        //   6초 무반응 자동 접힘(GearRadialMenuWidget.AutoCollapseIdleSeconds)이 있어 바깥 클릭을
        //   막아도 어차피 6초 뒤 접힌다 — 반응만 굼떠질 뿐 사용자가 얻는 것이 없다.
        //   InfoGearIconWidget.BeginPress의 판단 근거 주석 참고.
        //
        // ★ 그럼 닫는 법을 화면이 어떻게 말하는가 — <b>[✕] 하나로 버틴다</b>(실측 근거)
        //   ✕ 글리프 대비: 정보창 5.73:1(TextTertiary on CardSurfaceMuted) /
        //                  설정창·팝오버 7.93:1(TextSecondary on CardSurface).
        //                  둘 다 <see cref="MinTextContrast"/> 4.5:1을 넘는다 — <b>글자는 읽힌다</b>.
        //   ✕ 칩(면/테두리) 대비: 면 1.01~1.09:1, 테두리 1.34~1.47:1 vs 창 바탕(PanelSurface).
        //                  <see cref="MinNonTextContrast"/> 3.0:1에 <b>한참 못 미친다</b> — 즉 글리프는
        //                  읽히지만 그것이 <b>누를 수 있는 버튼</b>이라는 신호는 거의 없다.
        //   ★ 이 갭은 이번 라운드가 만든 것이 아니라 <b>원래 있던 것</b>이고, 종전에는 창 밖 클릭이
        //     그것을 가려 주고 있었다. 탈출구가 2개에서 1개로 줄었으므로 이제는 가려지지 않는다.
        //     대체 문구를 넣을지 / 칩을 밝힐지는 <b>UX 소관</b>으로 리더에게 보고했다.
        //     설정창 푸터만은 문장을 지우지 않고 거짓인 절반만 도려냈다("[✕]를 누르면 닫혀요.") —
        //     그 줄이 바로 윗줄 "이 창을 여는 방법"과 짝을 이루는 자리이기 때문이다.
        //
        // ★ "Esc는 안 됩니다"는 <b>적지 않는다</b>. 안 되는 키를 화면에 적으면 사용자는 그 키를
        //   시도한다 — 문장을 읽고 나서. 되는 것만 적는다(SettingsWindow.BuildFooter의 선례).

        private static Font _font;

        /// <summary>이 프로젝트에는 TextMeshPro가 없다 — 내장 폰트를 한 번만 찾아 캐시한다.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // ==================== 둥근 사각형 스프라이트 ====================

        private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

        /// <summary>꽉 찬 둥근 사각형(9-슬라이스). 같은 반지름이면 같은 스프라이트를 돌려준다.</summary>
        public static Sprite RoundedFill(int radius) => Get(radius, 0);

        /// <summary>테두리만 있는 둥근 사각형(가운데는 투명). 카드 윤곽선/선택 강조용.</summary>
        public static Sprite RoundedOutline(int radius, int thickness) => Get(radius, Mathf.Max(1, thickness));

        private static Sprite Get(int radius, int thickness)
        {
            radius = Mathf.Clamp(radius, 2, 32);
            int key = radius * 100 + thickness;
            if (_cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            int size = radius * 2 + 4;                  // 코너 두 개 + 9-슬라이스 중앙 4px.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"UiChrome_R{radius}_T{thickness}",
                hideFlags = HideFlags.HideAndDontSave,  // 씬 저장/에디터 목록을 더럽히지 않는다.
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 둥근 사각형의 부호 있는 거리 — 모서리에서만 원, 나머지는 직선.
                    float px = x + 0.5f, py = y + 0.5f;
                    float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                    float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius; // >0 이면 도형 바깥.

                    float alpha = Mathf.Clamp01(0.5f - outside);            // 바깥 경계 1px 안티에일리어싱.
                    if (thickness > 0) alpha *= Mathf.Clamp01(outside + thickness + 0.5f); // 안쪽을 도려낸다.

                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            float border = radius + 1f;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _cache[key] = sprite;
            return sprite;
        }

        // ==================== 진짜 원 / 링 ====================

        /// <summary>
        /// 진짜 원과 링은 <see cref="RoundedFill"/>로 만들 수 없다 — 그쪽은 <c>size = radius*2 + 4</c>라
        /// 가운데에 항상 4px 직선 구간이 남는 <b>둥근 사각형</b>이고, 9-슬라이스로 늘리면 그 직선이
        /// 함께 늘어난다. 부채꼴 버튼(Ø44)처럼 원 자체가 형태인 곳에서는 그 4px이 눈에 띈다.
        /// 그래서 원은 별도 텍스처로 굽고 <see cref="Image.Type.Simple"/>로 통째로 늘린다
        /// (정사각 RectTransform이면 어떤 크기에서도 원을 유지한다).
        /// </summary>
        private const int CircleTextureSize = 128;

        private static readonly Dictionary<int, Sprite> _circleCache = new Dictionary<int, Sprite>();

        // ====================================================================================
        // ★ 2026-08-30 — 선/원의 <b>둥근 끝</b>과 가장자리 램프를 실제로 살린다
        // ====================================================================================
        // 사용자 지적: "모자들도 너무 조잡하게 구현되어있음". 코드를 읽어 찾은 원인 두 가지와,
        // <b>실제로 렌더해 픽셀을 재 본 결과</b>를 함께 남긴다(추측과 실측을 섞지 않는다).
        //
        //  (B) ★ <b>둥근 끝이 둥글지 않았다 — 실측으로 확인된 진짜 원인.</b>
        //      128×32 캡슐을 (length, thickness) 사각형에 <c>Simple</c>로 늘리면 가로/세로 축소율이
        //      다르다. 캡의 반원(반지름 16텍셀)이 세로로는 thickness/2가 되지만 가로로는 length/8이
        //      되어, 획 끝이 <b>길게 뾰족해진다</b>. 아이콘 한 획이 10~15pt라 taper가 획 길이의
        //      10%가 넘는다 — 32종이 전부 꺾은선이므로 이 왜곡이 곧 "조잡함"이다.
        //      실측(획 두께 2.125pt, 자유 끝에서 x가 2px씩 나아갈 때의 세로 총 잉크량):
        //          옛: 1.97 -> 2.90 -> 3.33 -> 3.70px  (8px에 걸쳐 창끝처럼 가늘어진다)
        //          새: 3.73 -> 3.89 -> 3.88 -> 3.96px  (첫 픽셀부터 제 두께 = 깔끔한 반원 캡)
        //      부작용으로 꺾은선 이음매도 정확해진다 — 캡 중심이 이제 <b>정확히 꼭짓점</b>에 온다
        //      (<see cref="AddPolyline"/>이 length에 thickness를 더하는 계산이 그제서야 성립한다).
        //
        //  (A) 가장자리 알파 램프가 축소율에 휩쓸렸다. 1텍셀 램프를 세로로 15배 축소하면 0.07pt가
        //      되어 화면에서 사라진다(이 앱의 UI는 ScreenSpaceOverlay라 MSAA도 받지 않는다).
        //      다만 <b>실측해 보니 이 항목의 기여는 작았다</b> — 같은 지그재그에서 중간 밝기 픽셀이
        //      1982개(옛) vs 2044개(새)로 거의 같다. 회전된 사각형을 바이리니어로 샘플하는 과정에서
        //      램프가 우연히 일부 복원되기 때문이다. 그래도 비율 램프로 바꾼 이유는 결과가
        //      <b>예측 가능해지기</b> 때문이다: 램프 폭이 획 길이·두께와 무관하게 항상
        //      <see cref="EdgeFeather"/>pt로 고정된다(전에는 획마다 달랐다).
        //
        // 고치는 방법(셰이더 0건, 렌더텍스처 0건 — 이 파일의 기존 규약 그대로):
        //  · 캡슐을 <b>가로 9-슬라이스</b>(border 16/0/16/0)로 붙인다. 양 끝 16텍셀(=캡)은 늘어나지
        //    않고 가운데만 늘어나므로 어떤 길이에서도 캡이 정확한 반원이다. 렌더 크기는
        //    <see cref="Image.pixelsPerUnitMultiplier"/>로 맞춘다.
        //  · 알파 램프를 <b>스프라이트 크기의 비율</b>로 굽고, 사각형을 램프 폭만큼 부풀려 붙인다.
        // ====================================================================================

        /// <summary>화면에서 유지할 가장자리 알파 램프 폭(캔버스 유닛). Retina(scaleFactor 2)에서
        /// 약 1 물리 픽셀 — 계단을 지우기에 충분하고, 더 넓히면 획이 흐려 보인다.</summary>
        private const float EdgeFeather = 0.5f;

        /// <summary>위 램프 폭의 공개 이름 — <see cref="SizeDialWidget"/>이 눈금을 <b>다시 놓을 때</b>
        /// (GameObject를 새로 만들지 않고 sizeDelta만 갱신할 때) 같은 규약을 지켜야 한다. 값을 그쪽에
        /// 다시 적으면 눈금만 다른 획들과 두께가 달라진다.</summary>
        public const float EdgeFeatherPoints = EdgeFeather;

        /// <summary>꽉 찬 원.</summary>
        public static Sprite Circle() => CircleSprite(0.5f, 1f);

        /// <summary>링. 두께는 <b>지름 대비 비율</b>이다 — 스프라이트가 통째로 늘어나므로 절대 픽셀로
        /// 정할 수 없다(예: Ø44 버튼에 2pt 링이면 2/44 ≈ 0.045).</summary>
        public static Sprite Ring(float thicknessFraction) => CircleSprite(thicknessFraction, 1f);

        /// <param name="coreFraction">스프라이트 지름 중 <b>실제 도형</b>이 차지하는 비율. 나머지가 알파 램프다.</param>
        private static Sprite CircleSprite(float thicknessFraction, float coreFraction)
        {
            thicknessFraction = Mathf.Clamp(thicknessFraction, 0.01f, 0.5f);
            coreFraction = Mathf.Clamp(coreFraction, 0.30f, 1f);
            int key = Mathf.RoundToInt(thicknessFraction * 1000f) * 1000 + Mathf.RoundToInt(coreFraction * 200f);
            if (_circleCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int size = CircleTextureSize;
            float half = size * 0.5f;
            float outer = half * coreFraction;                              // 보이는 반지름(텍셀)
            float feather = Mathf.Max(0.75f, half - outer);                 // 램프 폭(텍셀)
            float inner = outer - thicknessFraction * outer * 2f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"UiChrome_Circle_{key}",
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 알파 0.5 등고선이 정확히 outer에 놓인다 = 도형의 "진짜" 가장자리.
                    float alpha = Mathf.Clamp01((outer - d) / feather + 0.5f);
                    if (inner > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01((d - inner) / feather + 0.5f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _circleCache[key] = sprite;
            return sprite;
        }

        /// <summary>둥근 끝(캡슐) 스트로크 한 획. 심볼 아이콘이 전부 이걸로 그려진다.
        /// <para>가로 <b>9-슬라이스</b>(border 16/0/16/0)라서 가운데만 늘어나고 양 끝 캡은 절대 늘어나지
        /// 않는다. 세로는 통째로 늘어나므로 <paramref name="coreFraction"/>으로 램프 비율을 조절한다.</para></summary>
        private static readonly Dictionary<int, Sprite> _capsuleCache = new Dictionary<int, Sprite>();

        private const int CapsuleCapTexels = 16;

        /// <summary>위 캡 텍셀 수의 공개 이름 — <see cref="EdgeFeatherPoints"/>와 같은 이유
        /// (<see cref="Image.pixelsPerUnitMultiplier"/> 계산을 호출부가 재현해야 한다).</summary>
        public const int CapsuleCapTexelsPublic = CapsuleCapTexels;

        private static Sprite Capsule(float coreFraction)
        {
            coreFraction = Mathf.Clamp(coreFraction, 0.30f, 1f);
            int key = Mathf.RoundToInt(coreFraction * 200f);
            if (_capsuleCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int w = 96, h = 32;                 // 캡 16 + 중앙 64 + 캡 16
            float half = h * 0.5f;
            float core = half * coreFraction;         // 보이는 반두께(텍셀)
            float feather = Mathf.Max(0.75f, half - core);
            float capCenterL = CapsuleCapTexels, capCenterR = w - CapsuleCapTexels;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"UiChrome_Capsule_{key}",
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, capCenterL, capCenterR);   // 선분까지의 거리 = 캡슐
                    float dx = px - cx, dy = py - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01((core - d) / feather + 0.5f) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect,
                new Vector4(CapsuleCapTexels, 0f, CapsuleCapTexels, 0f));
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _capsuleCache[key] = sprite;
            return sprite;
        }

        // ==================== 부품 공장 ====================

        /// <summary>
        /// 심볼 한 획(길이 × 두께의 둥근 스트로크, <paramref name="angleDegrees"/>만큼 회전, 부모
        /// 중심 기준 <paramref name="center"/>에 놓는다). 아이콘 3종이 전부 이 함수의 조합이다 —
        /// 도형을 새로 만들지 않으므로 색/모양 결정이 이 파일 한 곳에 남는다.
        /// </summary>
        public static Image AddStroke(Transform parent, string name, float length, float thickness,
            float angleDegrees, Vector2 center, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            // 사각형을 램프 폭만큼 부풀려 붙인다 — 보이는 획은 여전히 정확히 (length × thickness)이고
            // 그 바깥으로 EdgeFeather만큼의 알파 램프만 더 생긴다(위 (A) 참고).
            float boxHeight = thickness + EdgeFeather * 2f;
            rt.sizeDelta = new Vector2(length + EdgeFeather * 2f, boxHeight);
            rt.anchoredPosition = center;
            rt.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);

            var image = go.GetComponent<Image>();
            image.sprite = Capsule(thickness / boxHeight);
            image.type = Image.Type.Sliced;
            // 9-슬라이스 테두리(16텍셀)가 화면에서 정확히 boxHeight/2 = 캡 반지름이 되게 한다.
            // 렌더 테두리 = borderPx / (스프라이트PPU/캔버스기준PPU × multiplier) = 16 / multiplier.
            image.pixelsPerUnitMultiplier = (CapsuleCapTexels * 2f) / boxHeight;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// 꺾은선 한 줄 — 인접한 두 점마다 <see cref="AddStroke"/>를 부르는 얇은 래퍼다.
        /// 40×40 아이콘 32종(<c>Core/ItemCatalog.cs</c>의 <c>ItemIconPart</c>)이 전부 이걸로 그려진다.
        /// 좌표는 <b>부모 중심 기준, y가 위</b>다(<see cref="AddStroke"/>와 같은 규약).
        /// <para>이음매는 처리하지 않는다 — 획이 둥근 캡(<see cref="Capsule"/>)이라 두 선분이 만나는
        /// 자리에 반원이 겹쳐 자연스러운 라운드 조인이 <b>공짜로</b> 나온다. 별도의 조인 도형을 만들면
        /// 같은 자리에 그림이 두 벌 생긴다.</para>
        /// </summary>
        public static void AddPolyline(Transform parent, string name, Vector2[] points, int count,
            float thickness, Color color)
        {
            if (points == null) return;
            count = Mathf.Min(count, points.Length);
            for (int i = 1; i < count; i++)
            {
                Vector2 a = points[i - 1], b = points[i];
                Vector2 d = b - a;
                float length = d.magnitude;
                if (length < 0.0001f) continue;
                // 길이에 두께를 더하는 이유: 캡슐의 반원 중심이 사각형 안쪽 thickness/2 지점이라,
                // 사각형을 정확히 length로 잡으면 두 선분이 만나는 자리에 thickness/2짜리 틈이 남는다.
                AddStroke(parent, name, length + thickness, thickness,
                    Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, (a + b) * 0.5f, color);
            }
        }

        public static void AddPolyline(Transform parent, string name, Vector2[] points, float thickness, Color color)
            => AddPolyline(parent, name, points, points != null ? points.Length : 0, thickness, color);

        /// <summary>부모 중심 기준 <paramref name="center"/>에 놓이는 원(꽉 찬 원 또는 링).
        /// 지름 하나로 정사각을 만든다.</summary>
        public static Image AddCircle(Transform parent, string name, float diameter, Color color,
            float ringThickness = 0f, Vector2 center = default)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            // 획과 같은 규약 — 램프 폭만큼 부풀리고, 스프라이트는 그만큼 안쪽에 도형을 굽는다.
            float box = diameter + EdgeFeather * 2f;
            rt.sizeDelta = new Vector2(box, box);
            rt.anchoredPosition = center;

            var image = go.GetComponent<Image>();
            float coreFraction = box > 0.0001f ? diameter / box : 1f;
            image.sprite = ringThickness > 0f && diameter > 0f
                ? CircleSprite(ringThickness / diameter, coreFraction)
                : CircleSprite(0.5f, coreFraction);
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>둥근 표면 하나(카드/패널). 반환값에 색을 다시 칠해도 된다.</summary>
        public static Image AddSurface(Transform parent, string name, Color color, int radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = RoundedFill(radius);
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        /// <summary>부모를 꽉 채우는 1px(또는 지정 두께) 둥근 테두리. 클릭을 먹지 않는다.</summary>
        public static Image AddOutline(Transform parent, string name, Color color, int radius, int thickness = 1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var image = go.GetComponent<Image>();
            image.sprite = RoundedOutline(radius, thickness);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // ====================================================================================
        // ★ 2026-09-02 — UI 그림자 <b>전면 제거</b>(사용자 지시: "캐릭터창 둘레로도 그림자들이 있는데
        //   다 없애줘 깔끔하게"). 여기에 AddShadow / AddShadowLayer / AddSoftShadowCircle /
        //   SoftShadowFill / SoftShadowCircle과 PanelShadow가 있었다.
        //
        //   <b>왜 함수를 no-op으로 남기지 않았나</b>: "있는데 아무것도 안 만드는 API"는 다음 사람에게
        //   "왜 안 나오지"를 만든다. 되살릴 일이 생기면 이 커밋을 되짚는 편이 정확하다.
        //
        //   <b>지우기 전에 쟀다</b> — 그림자는 이 앱에서 장식이 아니라 "패널과 임의의 바탕화면 사이
        //   분리막"이기도 했기 때문이다(<see cref="MinNonTextContrast"/> 기준):
        //     · 패널 가장자리 Flatten(PanelBorder, PanelSurface) = rgb(0.226, 0.236, 0.252)
        //       → 흰 바탕화면 11.05:1 / 중간 회색 2.78:1 / 검정 1.90:1
        //     · 불투명 패널 본체(PanelSurface) 자체가 흰 바탕화면에서 17.97:1
        //     · 옛 그림자 코어(검정 α0.55)는 <b>검은 바탕화면에서 1.00:1</b> — 원리상 0이다.
        //   즉 어두운 바탕에서 경계를 만들던 것은 처음부터 그림자가 아니라 보더였고(삭제된 AddShadow
        //   주석도 같은 말을 했다), 밝은 바탕에서는 패널 자신이 이미 압도적으로 분리된다.
        //   <b>그림자가 유일한 분리막이던 표면은 하나도 없다</b> — 전 호출부(창/팝오버/포스트잇/부채꼴
        //   버튼)가 예외 없이 보더 또는 불투명 표면을 함께 갖는다. 그래서 지워도 배경에 녹지 않는다.
        //   (어두운 바탕에서 보더가 3.0에 못 미치는 것은 그림자를 지우기 <b>전부터</b> 그랬다. 검정 위의
        //    검정은 대비를 만들 수 없으므로 그림자로는 고칠 수 없는 문제이고, 고치려면 보더를 올려야 한다.)
        // ====================================================================================
        // ====================================================================================
        // ★ 유리(glass) 프리미티브 3종 — docs/UX_FLOW.md 34-2 (2026-08-31)
        // ====================================================================================
        //
        // 릴스의 패널 뒤는 진짜 가우시안 블러(macOS NSVisualEffectView)다. 이 프로젝트는 Built-in RP에
        // 포스트프로세싱 스택이 없고, 있어도 24시간 상주 앱에 매 프레임 blit은 과하다. 그런데
        // <b>사람이 "유리"라고 판정하는 단서는 넷</b>이고 블러는 그중 하나일 뿐이다:
        //   (a) 뒤가 살짝 비침      → 알파
        //   (b) 위쪽이 더 밝음      → 세로 시인(sheen)          ← VerticalGradientFill
        //   (c) 가장자리 얇은 밝은 선 → 상단 1px 하이라이트 + 보더
        //   (d) 바닥에서 떠 있음    → 그림자                    ★ 2026-09-02 사용자 지시로 삭제됨
        // 이제 (c)만 남는다 — 그래도 유리로 읽히는지는 사용자 판정 사항이고, 되살리려면 위 제거 노트를 볼 것.
        // 셰이더 0건 / 렌더텍스처 0건 / 머티리얼 0건 — 이 파일의 기존 규약 그대로다.

        private static readonly Dictionary<int, Sprite> _gradientCache = new Dictionary<int, Sprite>();

        /// <summary>세로 알파 램프가 사라지는 지점(위에서부터의 비율). 34-2: 45%.
        /// 그보다 아래까지 내려오면 "위에서 온 빛"이 아니라 "그라데이션 배경"으로 읽힌다.</summary>
        private const float SheenFadeRatio = 0.45f;

        private const int GradientTextureWidth = 64;
        private const int GradientTextureHeight = 128;

        /// <summary>
        /// 위쪽이 밝고 45% 지점에서 완전히 투명해지는 <b>둥근 사각형</b>. 유리의 단서 (b)를 만든다.
        ///
        /// <para>★★ <b>2026-08-31 — 이 앱의 오버레이 캔버스에는 쓰지 말 것</b>(호출부 0건으로 정리됐다).
        /// 이 스프라이트는 램프를 <b>알파 채널</b>에 담는데, 파일 머리 "알파 채널의 법칙" (2)에 따라
        /// 알파 램프는 그 자리의 창 알파를 그대로 끌어내린다(α0.10 시인 한 겹 → 창 알파 0.91,
        /// 즉 데스크톱이 <b>9% 비친다</b>). 남겨 두는 이유는 훗날 <b>불투명 배경 위</b>(RenderTexture로
        /// 굽는 초상화 스테이지 등)에서는 여전히 옳은 도구이기 때문이다. 화면에 직접 그리는 캔버스에서
        /// 쓰려면 먼저 UI 머티리얼의 알파 블렌드를 분리해야 한다(<see cref="AddGlassPanel"/> 문서 참고).</para>
        ///
        /// <para><b>왜 9-슬라이스가 아니라 <see cref="Image.Type.Simple"/>인가</b>: 9-슬라이스는 가운데를
        /// 늘리는데, 그 가운데가 곧 그라데이션 구간이라 늘리면 램프까지 늘어나 "위 45%"라는 약속이
        /// 깨진다. 가로 방향은 색이 균일하므로 통째로 늘려도 왜곡이 보이지 않고, 세로만 정확히
        /// 유지하면 된다. 코너는 <b>위쪽 둘만</b> 둥글린다 — 아래쪽은 이미 완전히 투명하다.</para>
        /// </summary>
        /// <param name="radius">패널 모서리 반지름(pt). 텍스처 폭 기준으로 환산해 굽는다.</param>
        public static Sprite VerticalGradientFill(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 32);
            if (_gradientCache.TryGetValue(radius, out Sprite cached) && cached != null) return cached;

            const int w = GradientTextureWidth, h = GradientTextureHeight;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"UiChrome_Sheen_R{radius}",
                hideFlags = HideFlags.HideAndDontSave,
            };

            // 텍셀 단위 코너 반지름 — 가로는 통째로 늘어나므로 세로 기준(픽셀 정사각)으로 잡는다.
            float rTexels = Mathf.Max(2f, radius * (h / 148f));   // 148 = 34-4-4의 COLLAPSED 높이(pt) 기준.
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                // y = 0이 아래. 위(=y가 큰 쪽)에서 아래로 램프한다.
                float fromTop = (h - 1 - y) / (float)(h - 1);
                float ramp = fromTop >= SheenFadeRatio ? 0f : 1f - fromTop / SheenFadeRatio;
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    // 위쪽 코너 둘만 둥글린다.
                    float dx = Mathf.Max(rTexels - px, px - (w - rTexels), 0f);
                    float dy = Mathf.Max(py - (h - rTexels), 0f);
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - rTexels;
                    float shape = dx > 0f || dy > 0f ? Mathf.Clamp01(0.5f - outside) : 1f;

                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(ramp * shape) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _gradientCache[radius] = sprite;
            return sprite;
        }

        private static Sprite _radialGlow;

        /// <summary>
        /// 중심이 밝고 가장자리로 <b>제곱 감쇠</b>하는 원. 다이얼 링 블룸/코너 광원에 쓴다.
        ///
        /// <para><b>왜 선형이 아니라 제곱인가</b>: 선형 감쇠는 가장자리에서 알파가 갑자기 끊겨
        /// "원반"으로 보인다. 제곱이면 바깥이 길게 사라져 <b>발광</b>으로 읽힌다. 삭제된 그림자
        /// 스프라이트도 같은 문제를 겪었다(그쪽은 smoothstep으로 풀었다).</para>
        ///
        /// <para>★ 램프를 9-슬라이스에 태워야 한다면 "가운데가 늘어나는" 부분만 피하면 된다 — 램프를
        /// <b>보더 안</b>에 가두면 감쇠와 9-슬라이스는 함께 성립한다. 2026-09-01의 그림자 스프라이트가
        /// 그 방법을 썼고, 그 코드는 2026-09-02 그림자 제거와 함께 사라졌다(필요하면 그 커밋 참조).</para>
        /// </summary>
        public static Sprite RadialGlow()
        {
            if (_radialGlow != null) return _radialGlow;

            const int size = CircleTextureSize;
            float half = size * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "UiChrome_RadialGlow",
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float a = d >= 1f ? 0f : (1f - d) * (1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            _radialGlow = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            _radialGlow.name = tex.name;
            _radialGlow.hideFlags = HideFlags.HideAndDontSave;
            return _radialGlow;
        }

        /// <summary>상단 1px 하이라이트가 코너 곡선을 피해 들어가는 안쪽 여백(pt).</summary>
        private const float HighlightInsetPoints = 1f;

        /// <summary>
        /// ★ <b>유리 패널</b> — 34-2의 겹 순서 그대로, 그러나 <b>반투명 겹이 하나도 없다</b>
        /// (2026-08-31 "뒤 창이 비쳐 보인다" 회귀의 두 번째 호출부 수정).
        ///
        /// <para><b>왜 <c>alpha</c> 파라미터가 사라졌는가</b>: 파일 머리 "알파 채널의 법칙" (2) 때문에
        /// α&lt;1 겹은 <b>무엇 위에 그리든</b> 창 알파를 끌어내린다 — <b>우리 자신의 불투명 패널 위에
        /// 그려도 마찬가지다</b>(dstA=1 위의 α0.86 겹 → 0.86² + 1×0.14 = <b>0.88</b>). 그래서
        /// "α만 0.98로 올린다"도 해가 아니다(0.98² = 0.9604이고 여기에 시인/보더가 겹치면 0.83까지
        /// 내려간다). 이 아키텍처에서 <b>안전한 알파는 1 하나뿐</b>이라, 파라미터로 둘 값 자체가 없다.</para>
        ///
        /// <para><b>그런데도 유리로 읽히는 이유</b>: 34-2가 정리한 유리의 단서 넷 중 (c) 가장자리의 밝은
        /// 선과 (d) 떠 있음은 <see cref="Flatten"/>으로 <b>겉보기 색을 한 톤도 바꾸지 않고</b> α=1로
        /// 옮길 수 있다. 잃는 것은 (a) 비침 하나뿐인데, 그건 애초에 비침해 원칙 2가 금지한 그림이다
        /// ("남의 창을 반투명 필터로 덮은 화면"은 우리가 팔 그림이 아니다).</para>
        ///
        /// <para><b>(b) 세로 시인은 뺐다 — 못 한 게 아니라 성립하지 않는다.</b> 알파 램프를 α=1로
        /// 옮기려면 램프를 RGB에 굽고 알파를 <b>본체와 똑같은 실루엣 마스크</b>로 써야 한다. 그 마스크가
        /// 모든 크기에서 본체와 일치하려면 9-슬라이스여야 하는데, 9-슬라이스는 가운데를 늘려 램프를
        /// 망가뜨린다(<see cref="VerticalGradientFill"/> 문서가 이미 지적한 그 이유다). 유일한 도피처인
        /// "고정 높이 밴드"도 이 패널이 <b>104×14pt에서 240×392pt까지</b> 자라는 이상 성립하지 않는다
        /// (<c>CornerHoverPanel</c>). 램프를 되살리는 진짜 방법은 겹을 더 얹는 것이 아니라 UI 머티리얼의
        /// <b>알파 블렌드를 분리</b>하는 것이다(<c>Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha</c>
        /// → dstA' = srcA + dstA(1−srcA)). 그건 이 파일의 "셰이더 0건" 규약을 깨는 결정이라 리더 판단
        /// 사항으로 남겨 둔다.</para>
        ///
        /// <para>반환값은 <b>컨테이너</b>(그림 없는 RectTransform)다 — <see cref="AddOpaquePanel"/>과
        /// 완전히 같은 계약이며, 겹 순서를 아는 곳도 그쪽 하나로 모았다.</para>
        /// </summary>
        /// <param name="body">본체 표면 — 호출부가 색을 다시 칠할 대상. <b>알파는 1로 유지할 것.</b></param>
        public static RectTransform AddGlassPanel(Transform parent, string name, int radius, out Image body)
        {
            RectTransform container = AddOpaquePanel(parent, name, radius, out body);

            // 유리는 큰 창과 달리 바탕이 클릭을 먹지 않는다 — 호버 패널/카드는 전역 폴링과 차단막이
            // 클릭을 처리하고, uGUI 레이캐스트까지 이 판이 가로채면 두 경로가 같은 클릭을 다툰다.
            body.raycastTarget = false;

            // 안쪽 상단 1px 하이라이트 — 유리의 단서 (c). 보더(AddOpaquePanel이 이미 얹었다)와는
            // 1pt 어긋나 있어 겹치지 않으므로 형제 순서가 뒤여도 그림이 같다.
            // 색은 <b>미리 합성</b>한다: 이 선 아래에 있는 것은 항상 방금 그린 불투명 본체다.
            var hi = new GameObject(name + "Highlight", typeof(RectTransform), typeof(Image));
            hi.transform.SetParent(container, false);
            var hiRt = hi.GetComponent<RectTransform>();
            hiRt.anchorMin = new Vector2(0f, 1f);
            hiRt.anchorMax = new Vector2(1f, 1f);
            hiRt.pivot = new Vector2(0.5f, 1f);
            hiRt.offsetMin = new Vector2(radius, -HighlightInsetPoints - 1f);
            hiRt.offsetMax = new Vector2(-radius, -HighlightInsetPoints);
            var hiImage = hi.GetComponent<Image>();
            hiImage.sprite = RoundedFill(RadiusDot);
            hiImage.type = Image.Type.Sliced;
            hiImage.color = Flatten(PanelHighlight, PanelSurface);
            hiImage.raycastTarget = false;

            return container;
        }

        /// <summary>
        /// ★ <b>큰 창</b>(캐릭터 창 / 팝오버)의 바탕 — 2026-08-31 "뒤가 비쳐 보인다" 회귀 수정.
        ///
        /// <para><see cref="AddGlassPanel"/>과 <b>겹 순서 규약이 같고</b>(본체 → 보더) 차이는 딱 둘이다:
        /// 본체가 <b>α=1</b>이고, 유리 연출(시인/하이라이트)이 없다. 큰 창은 글을 읽는 표면이라
        /// 위쪽만 밝아지는 시인이 오히려 본문 대비를 흔든다.</para>
        ///
        /// <para><b>반환값은 컨테이너</b>(그림 없는 <see cref="RectTransform"/>)다. 호출부는 이 사각형의
        /// 크기/위치만 정하고 내용물을 여기에 붙이면 된다. <b>컨테이너에 Graphic을 붙이지 말 것</b> —
        /// 그 순간 본체가 그 그림 위에 그려져 겹 순서가 뒤집힌다.</para>
        /// </summary>
        /// <param name="body">본체 표면. 클릭을 받는 판이기도 하다(창 바탕을 눌러도 뒤로 새지 않게).</param>
        public static RectTransform AddOpaquePanel(Transform parent, string name, int radius,
            out Image body)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var container = go.GetComponent<RectTransform>();

            // (1) 본체 — α=1. 투명 오버레이에서 창 알파를 1로 만드는 유일한 겹이다.
            body = AddSurface(container, name + "Body", PanelSurface, radius);
            Stretch(body.rectTransform);

            // (2) 보더 — <b>미리 합성한 불투명색</b>으로 그린다. ★ 2026-09-02에 그림자를 전부 없앤
            //   뒤로는 이 1px 선이 <b>어두운 바탕화면에서 창 실루엣을 만드는 유일한 겹</b>이다
            //   (위 제거 노트의 대비 실측 참고). 색을 낮출 때는 그 사실을 먼저 고려할 것.
            //   PanelBorder(흰색 α0.16)를 그대로 얹으면 그 1px 위에서만 창 알파가 0.87로 내려간다
            //   (파일 머리 "알파 채널의 법칙" (2)). 보더 아래에 있는 것은 <b>항상 방금 그린 불투명 본체</b>
            //   이므로, 같은 블렌드 결과를 미리 계산해 α=1로 칠하면 <b>색은 완전히 같고 알파만 1로 남는다</b>.
            //   (스프라이트 자체의 가장자리 안티에일리어싱은 그대로다 — 그건 패널 실루엣이라 필요한 것이다.)
            AddOutline(container, name + "Outline", Flatten(PanelBorder, PanelSurface), radius);
            return container;
        }

        // ==================== 대비(WCAG 2.x) ====================
        //
        // 이 앱의 UI는 <b>불투명 패널 위</b>에 있어서 대비를 설계 시점에 계산해 둘 수 있었다. 그런데
        // 톱니 아이콘 하나만은 <b>유저의 임의의 데스크톱</b> 위에 맨몸으로 놓인다 — 그 배경은 우리가
        // 고를 수 없다. 그래서 "어떤 배경에서도 최소 대비를 보장하는가"를 <b>코드가 계산</b>하고
        // 테스트가 전 회색 구간을 훑어 확인한다(InfoGearIconWidget.ResolveHaloColor 참고).
        // 계산식을 프로덕션과 테스트가 <b>같은 함수</b>로 공유해야 "테스트에 상수를 베끼지 않는다"가
        // 성립한다(CLAUDE.md).

        /// <summary>
        /// 본문 글자의 대비 하한. 출처: <b>WCAG 2.2 성공기준 1.4.3 Contrast (Minimum), AA</b>
        /// (https://www.w3.org/TR/WCAG22/#contrast-minimum).
        /// </summary>
        public const float MinTextContrast = 4.5f;

        /// <summary>
        /// 아이콘·그래픽의 대비 하한. 출처: <b>WCAG 2.2 성공기준 1.4.11 Non-text Contrast, AA</b>
        /// (https://www.w3.org/TR/WCAG22/#non-text-contrast). 글자가 아니므로 4.5가 아니라 3.0이다.
        /// </summary>
        public const float MinNonTextContrast = 3.0f;

        /// <summary>
        /// ★ <b>글자가 실제로 얹히는 바탕 전부</b>. 잉크 하한은 이 목록의 <b>어느 하나에서도</b>
        /// 무너지면 안 된다.
        ///
        /// <para><b>왜 목록이 필요한가</b>: 이 앱은 패널 위에 카드를 얹고 그 위에 글자를 놓는다.
        /// "패널 위에서 5.8:1"만 재고 끝내면 카드 위(5.3:1)를 못 본다 — 실제로 옛
        /// <c>TabInactive</c>가 정확히 그 틈에 있었다(패널 4.52 ✔ / 카드 4.15 ✘).</para>
        ///
        /// <para><b>합성 후 색이다.</b> 여기 담긴 값은 전부 α=1인 실제 도색이다(파일 머리의
        /// "알파 채널의 법칙" — 이 앱의 큰 창 바탕은 α&lt;1일 수 없다). 반투명 겹 위에 글자를 놓는
        /// 새 표면이 생기면 <see cref="Flatten"/>으로 <b>미리 합성해서</b> 이 목록에 넣어라.
        /// 합성 전 색으로 재면 계산이 거짓말을 한다.</para>
        /// </summary>
        public static readonly Color[] TextBackdrops =
        {
            PanelSurface,        // 창/팝오버 바탕
            CardSurface,         // 카드·타일 (가장 밝다 = 가장 불리하다)
            CardSurfaceMuted,    // 잠긴/비활성 카드 바탕
            SubtleSurface,       // 상세 패널
        };

        // ================================================================================
        // ★★ 2026-09-02 — 바탕 목록이 <b>한 종류뿐</b>이었던 것이 이 앱 최저 대비를 만들었다
        // ================================================================================
        //
        // 실측 사고: [설정] > [캐릭터] > `말투` 행이 비활성일 때 `[반말]` 칩이
        //   면 <b>#5DA1F5</b>(강조색, 활성과 똑같다) + 글자 <b>#AEB4BF</b> = <b>1.28 : 1</b>.
        // 페르소나가 "한 글자도 없다"고 적게 만든 값이 2.35였다. <b>그보다 낮다.</b>
        //
        // 왜 위 사다리 검사가 못 잡았나 — <see cref="TextBackdrops"/>에 담긴 넷이 <b>전부 어두운 면</b>이라
        // "이 잉크가 밝은 면 위에 놓이면?"이라는 질문 자체가 존재하지 않았다. 검사는 통과했고 화면은 지워졌다.
        //
        // 그래서 바탕을 <b>세 종류</b>로 나누고, 각각 <b>어떤 잉크가 합법인지</b>를 값으로 적는다.
        // 새 표면을 만드는 사람은 목록에 한 줄 넣기만 하면 <see cref="InkOnSurface"/>가 알아서 고른다.

        /// <summary>
        /// 버튼·칩처럼 <b>한 단 들뜬 중성 면</b>(흰색 α0.10을 얹은 결과).
        /// <para>★ 실측: 이 면 위에서 <see cref="InkRole.Meta"/>(<see cref="TextTertiary"/>)는
        /// 카드 위 <b>3.94 : 1</b> / 창 위 <b>4.38 : 1</b>로 <b>둘 다 AA 미달</b>이다. 지금 이 면에
        /// 놓이는 글자는 전부 <see cref="InkRole.Title"/> 단(5.87 / 6.52 ✔)이라 사고는 없었지만,
        /// <b>선언되지 않은 바탕은 다음에 반드시 사고를 낸다</b>. 이제 선언되어 있고
        /// <see cref="InkOnSurface"/>가 자동으로 한 단 올려 준다.</para>
        /// </summary>
        public static readonly Color[] RaisedTextBackdrops =
        {
            Flatten(CardBorder, CardSurface),    // SettingsControls.ButtonSurfaceOnCard
            Flatten(CardBorder, PanelSurface),   // SettingsControls.ButtonSurfaceOnPanel
        };

        /// <summary>
        /// <b>강조색으로 가득 찬 면</b> — 선택된 세그먼트 칩, 채워진 버튼, 밝은 스와치.
        ///
        /// <para>★ 이 면 위에서 사다리 <b>3단이 전부 무너진다</b>(실측):
        /// Title 2.42 / Body 1.28 / Meta 1.16. 합법 잉크는 <see cref="OnAccentSolid"/> <b>하나</b>
        /// (7.16 ✔)뿐이다. 그래서 이건 "한 단 올리면 되는" 문제가 아니라 <b>다른 잉크 계열</b>이며,
        /// <see cref="TextBackdrops"/>와 같은 목록에 넣으면 안 된다(넣는 순간 사다리 검사가
        /// 영원히 빨간불이 되고, 그 빨간불을 끄는 유일한 방법은 사다리를 망가뜨리는 것이다).</para>
        /// </summary>
        public static readonly Color[] BrightTextBackdrops =
        {
            Accent,        // 채워진 강조 면(선택된 칩 / 켜진 스위치 트랙 / 슬라이더 채움)
            TextPrimary,   // 흰 채움 위에 기호를 얹는 자리
        };

        /// <summary>
        /// ★ <b>글자 색을 고르는 단 하나의 문</b>. 콜사이트는 <b>자기가 올라앉은 면</b>과 역할만 말하고,
        /// 색은 여기서 정한다(<see cref="Ink"/>의 "콜사이트는 색을 고르지 않는다" 규칙의 확장판).
        ///
        /// <para>규칙은 한 줄이다: <b>사다리 잉크가 그 면에서 AA를 넘으면 그것을 쓰고, 못 넘으면
        /// 넘는 것으로 바꾼다.</b> 목록에 없는 새 색이 와도 동작한다 — 값으로 판정하기 때문이다.</para>
        ///
        /// <para><b>위계가 사라지는 것 아닌가</b>: 밝은 면 위에서는 3단이 하나로 접힌다. 맞다.
        /// 그리고 그게 옳다 — 강조색으로 칠해진 칩은 <b>면 자체가</b> "이게 골라진 것"을 이미 말하고
        /// 있으므로 글자가 위계를 또 말할 이유가 없다. 위계를 글자로 유지하려다 <b>글자를 지운 것</b>이
        /// 정확히 이 함수가 고치는 사고다.</para>
        /// </summary>
        public static Color InkOnSurface(Color backdrop, InkRole role, bool enabled)
        {
            Color ladder = Ink(role, enabled);
            if (ContrastRatio(ladder, backdrop) >= MinTextContrast) return ladder;

            // (1) 밝은 면 — 어두운 잉크로 뒤집는다.
            if (ContrastRatio(OnAccentSolid, backdrop) >= MinTextContrast) return OnAccentSolid;

            // (2) 한 단 들뜬 중성 면 — 사다리 위쪽으로 올린다.
            if (ContrastRatio(TextSecondary, backdrop) >= MinTextContrast) return TextSecondary;
            if (ContrastRatio(TextPrimary, backdrop) >= MinTextContrast) return TextPrimary;

            // (3) 여기까지 왔으면 그 면 위에 읽히는 글자가 <b>존재하지 않는다</b>. 가장 나은 것을
            //     돌려주되 조용히 넘기지 않는다 — 면을 바꿔야 하는 상황이기 때문이다.
            Color best = ContrastRatio(TextPrimary, backdrop) >= ContrastRatio(OnAccentSolid, backdrop)
                ? TextPrimary : OnAccentSolid;
            WarnUnreadableBackdrop(backdrop, best);
            return best;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void WarnUnreadableBackdrop(Color backdrop, Color best)
        {
            Debug.LogError($"[잉크] 바탕 #{ColorUtility.ToHtmlStringRGB(backdrop)} 위에서는 어떤 잉크도 " +
                $"{MinTextContrast:F1}:1을 넘지 못합니다(최선 #{ColorUtility.ToHtmlStringRGB(best)} = " +
                $"{ContrastRatio(best, backdrop):F2}:1). 글자를 바꿀 게 아니라 <b>면</b>을 바꿔야 합니다.");
        }

        /// <summary>
        /// WCAG 상대 휘도. <b>우리가 발명한 식이 아니다</b> — 출처는
        /// <b>WCAG 2.2 "relative luminance"</b> 정의(https://www.w3.org/TR/WCAG22/#dfn-relative-luminance)이고,
        /// 대비비 식 <c>(L1+0.05)/(L2+0.05)</c>는 같은 문서의 "contrast ratio"
        /// (https://www.w3.org/TR/WCAG22/#dfn-contrast-ratio) 정의다. 계수 0.2126/0.7152/0.0722와
        /// 임계 0.04045, 지수 2.4는 전부 그 정의에서 온 값이므로 손대지 마라.
        /// <para>알파는 보지 않는다 — 합성은 <see cref="Flatten"/>이 먼저 한다.</para>
        /// </summary>
        public static float RelativeLuminance(Color c)
            => 0.2126f * LinearizeChannel(c.r) + 0.7152f * LinearizeChannel(c.g) + 0.0722f * LinearizeChannel(c.b);

        private static float LinearizeChannel(float srgb)
        {
            srgb = Mathf.Clamp01(srgb);
            return srgb <= 0.04045f ? srgb / 12.92f : Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>두 색의 대비비(1.0 ~ 21.0). 순서는 상관없다.</summary>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a);
            float lb = RelativeLuminance(b);
            float hi = Mathf.Max(la, lb), lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        /// <summary>반투명 <paramref name="over"/>를 불투명 <paramref name="onto"/> 위에 얹은 결과를
        /// <b>α=1 단색</b>으로 미리 계산한다. 보이는 색은 같고 창 알파만 지킨다.</summary>
        public static Color Flatten(Color over, Color onto)
        {
            float a = Mathf.Clamp01(over.a);
            return new Color(
                Mathf.Lerp(onto.r, over.r, a),
                Mathf.Lerp(onto.g, over.g, a),
                Mathf.Lerp(onto.b, over.b, a),
                1f);
        }

        /// <summary>
        /// ★ <b>글자에 비텍스트 잉크를 쓰면 여기서 잡는다</b>(생성 시점 1회, 프레임 비용 0).
        ///
        /// <para>소스 스캔만으로는 못 막는 경로가 있다 — 색을 변수에 담아 넘기면 문자열 검사는
        /// 통과한다. 그래서 <b>값 자체</b>를 본다. 이 앱에서 글자를 만드는 문은
        /// <see cref="AddText"/> 하나뿐이므로 이 한 자리가 전 표면을 덮는다.</para>
        ///
        /// <para>릴리스 빌드에서는 <b>사라진다</b>(Conditional). 유저에게 나가는 빌드가 이 검사 때문에
        /// 느려질 이유는 없고, 이 결함은 개발 중에만 생긴다.</para>
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void WarnIfNonTextInk(string name, Color color)
        {
            if (!Approximately(color, NonTextMuted) && !Approximately(color, DisabledControlInk)
                && !Approximately(color, RetiredInk.TabInactive)) return;

            Debug.LogError($"[잉크] '{name}'을(를) 글자에 쓸 수 없는 잉크 #{ColorUtility.ToHtmlStringRGB(color)}로 " +
                "그리려 했습니다. 이 값들은 기호/테두리/꺼진 컨트롤 전용이며 본문 하한 " +
                $"{MinTextContrast:F1}:1을 넘지 못합니다. 글자는 UiChrome.InkTitle/InkBody/InkMeta/InkTab을 " +
                "쓰십시오(UI_SURFACE_SPEC §2.3/§2.4/§11).");
        }

        private static bool Approximately(Color a, Color b)
            => Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g)
               && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);

        public static Text AddText(Transform parent, string name, int fontSize, TextAnchor anchor,
            Color color, bool bold = false, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            WarnIfNonTextInk(name, color);
            text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>말줄임 문자. 세 점(<c>...</c>)이 아니라 <b>한 글자</b>인 U+2026을 쓴다 — 폭이
        /// 1/3이라 잘라야 하는 본문 글자를 그만큼 덜 먹는다.</summary>
        public const string Ellipsis = "…";

        /// <summary>
        /// <paramref name="source"/>가 <paramref name="maxWidth"/>(캔버스 포인트)에 안 들어가면
        /// 뒤를 잘라 <see cref="Ellipsis"/>를 붙인 문자열을 돌려준다. 들어가면 <b>원본을 그대로</b>
        /// 돌려주고 아무것도 할당하지 않는다.
        ///
        /// <para>레거시 uGUI <see cref="Text"/>에는 말줄임 기능이 없다. <c>HorizontalWrapMode.Overflow</c>는
        /// 상자 밖으로 <b>그냥 흘려</b> 옆 요소와 겹치고(정보창 카드 이름 ↔ 메타가 실측 1.2pt까지 붙었다),
        /// <c>Wrap</c>은 한 줄짜리 상자에서 두 번째 줄을 반쯤 잘라 보여 준다. 둘 다 "잘렸다"는 사실을
        /// 숨긴다 — 말줄임표는 그 사실을 <b>드러낸다</b>.</para>
        ///
        /// <para><b>글자 수가 아니라 실제 폭으로 잰다.</b> 이 앱은 한글/라틴/숫자가 섞이고 폭이 2배 넘게
        /// 차이 나서, 글자 수 상한은 한글에서만 맞고 라틴에서는 반쯤 빈 상자를 남긴다. 폭 측정은
        /// <see cref="Text.preferredWidth"/>(폰트가 실제로 잰 값)를 쓰고, 후보를 이진 탐색해 측정 횟수를
        /// <c>log2(길이)</c>로 줄인다.</para>
        ///
        /// <para><b>호출부 규약</b>: 이 함수는 <see cref="Text.text"/>를 여러 번 바꿔 가며 재므로
        /// <b>내용이 실제로 바뀐 순간에만</b> 부른다(원본 문자열을 호출부가 캐시해 비교할 것).
        /// 상주 앱이라 4Hz 갱신 루프에서 매번 부르면 잘라 낸 문자열이 계속 새로 할당된다.</para>
        /// </summary>
        public static string Ellipsize(Text text, string source, float maxWidth)
        {
            if (text == null || string.IsNullOrEmpty(source)) return source;
            if (maxWidth <= 0f) return source;

            text.text = source;
            if (text.preferredWidth <= maxWidth) return source;   // 들어간다 — 할당 없음.

            // 잘라야 한다. lo = "말줄임표를 붙여도 들어가는" 최대 글자 수.
            int lo = 0, hi = source.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                text.text = source.Substring(0, mid) + Ellipsis;
                if (text.preferredWidth <= maxWidth) lo = mid; else hi = mid - 1;
            }
            return lo <= 0 ? Ellipsis : source.Substring(0, lo) + Ellipsis;
        }

        public static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>부모의 좌상단을 원점으로 배치한다 — y는 아래로 갈수록 음수(창 레이아웃 공통 규약).</summary>
        public static void PlaceTopLeft(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
