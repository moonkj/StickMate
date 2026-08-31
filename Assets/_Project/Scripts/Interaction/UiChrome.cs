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
    /// <b>강조색 하나</b>(<see cref="Accent"/>)만 쓰고, 입체감은 아주 옅은 그림자 한 겹과 1px 테두리로만
    /// 낸다. 그라데이션/광택/네온은 쓰지 않는다.
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

        // ==================== 색 — 2026-08-30 다크 글로스 팔레트 (docs/UX_FLOW.md 34-1) ==========
        // ★ 같은 날 오전의 33-1 팔레트(종이빛 회색 + 테라코타)는 <b>폐기</b>했다. 이 파일을 쓰는 모든
        //   표면(기어 부채꼴 / 집중 모드 팝오버 / 할일 팝오버 / 캐릭터 창)이 함께 다크 글로스로
        //   갈아입는다 — 34-1이 못박은 <b>의도한 결과</b>다.
        //   값은 이 프로젝트 관례대로 hex/255 그대로(감마 공간) 넣는다.

        /// <summary>모달 딤(#0a0c10). <b>이 앱에서는 화면 전체를 덮는 용도로 쓰지 않는다</b> — 유저의
        /// 작업 화면을 통째로 가리면 비침해 원칙 2 정면 위반이다(33-7-7). 토큰만 남겨 두는 이유는
        /// 훗날 덮어도 되는 화면(온보딩 등)이 생겼을 때 색을 새로 고르지 않게 하기 위해서다.</summary>
        public static readonly Color ScreenScrim = new Color(0.039f, 0.047f, 0.063f, 1f);

        /// <summary>창 바탕(#14171c, α0.96). 큰 창은 사실상 불투명해야 어떤 배경 위에서도 글자가 읽힌다
        /// (34-1 대비표: TextSecondary 7.4:1).</summary>
        public static readonly Color PanelSurface = new Color(0.078f, 0.090f, 0.110f, 0.96f);

        /// <summary>패널 상단 시인(sheen) 시작색 rgba(255,255,255,0.10) — 34-2 (3)겹.
        /// "위쪽이 더 밝다"는 유리 판정 단서를 블러 없이 만든다.</summary>
        public static readonly Color PanelSheen = new Color(1f, 1f, 1f, 0.10f);

        /// <summary>패널 안쪽 상단 1px 하이라이트 rgba(255,255,255,0.30) — 34-2 (4)겹(굴절 테두리).</summary>
        public static readonly Color PanelHighlight = new Color(1f, 1f, 1f, 0.30f);

        /// <summary>패널 그림자 rgba(0,0,0,0.55) — 34-2에 따라 <see cref="AddShadow"/>가 2겹으로 쌓는다.</summary>
        public static readonly Color PanelShadow = new Color(0f, 0f, 0f, 0.55f);

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

        public static readonly Color TextPrimary = new Color(0.949f, 0.957f, 0.969f, 1f);      // #f2f4f7
        public static readonly Color TextSecondary = new Color(0.682f, 0.706f, 0.749f, 1f);    // #aeb4bf
        public static readonly Color TextTertiary = new Color(0.545f, 0.576f, 0.624f, 1f);     // #8b939f (캡션 전용)

        /// <summary>슬롯 코드/카운트처럼 <b>읽지 않아도 되는</b> 메타(#6c7480).</summary>
        public static readonly Color TextQuaternary = new Color(0.424f, 0.455f, 0.502f, 1f);

        /// <summary>잠긴 항목의 글자(#4b525c).</summary>
        public static readonly Color TextDisabled = new Color(0.294f, 0.322f, 0.361f, 1f);

        /// <summary>비활성 탭 라벨(#79808c).</summary>
        public static readonly Color TabInactive = new Color(0.475f, 0.502f, 0.549f, 1f);

        /// <summary>아이콘 기본 잉크(#d6dbe3). 다크에서는 잉크가 <b>밝은 쪽</b>이다.</summary>
        public static readonly Color IconInk = new Color(0.839f, 0.859f, 0.890f, 1f);

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
        public const int FontDisplay = 19;   // 캐릭터 이름.
        public const int FontTitle = 13;     // 창 제목 / 탭 / 상세 이름.
        public const int FontBody = 12;      // 본문 / 카드 이름 / 스탯.
        public const int FontLabel = 11;     // 라벨 / 부제.
        public const int FontCaption = 10;   // 캡션 / 슬롯코드 / 카운트 / 카드 메타.
        // ★ 스펙의 9.5px(카드 메타)은 채택하지 않는다 — 내장 비트맵 폰트에서 9pt 이하 한글은
        //   Retina가 아닌 디스플레이에서 획이 뭉갠다. 이 프로젝트는 이미 10pt를 하한으로 쓴다.

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

        /// <summary>
        /// 패널 뒤 그림자 — <b>2겹</b>(34-2). 넓고 옅은 앰비언트가 "떠 있음"을, 좁고 진한 키가
        /// "가장자리"를 만든다.
        /// <para>왜 2겹인가: 33-1의 1겹(밝은 바탕 전용)은 <b>어두운 패널이 어두운 바탕화면 위에 놓이면
        /// 경계가 통째로 사라진다</b>. 겹을 늘리는 대신 알파를 올리면 밝은 바탕에서 검은 테가 두꺼워
        /// 보인다 — 두 배경 모두에서 성립하는 해가 2겹이다.</para>
        /// 반환값은 <b>키 그림자</b>(위 겹)다. 호출부가 색/알파를 다시 칠할 대상이 그쪽이기 때문이다.
        /// </summary>
        public static Image AddShadow(Transform parent, string name, int radius, float spread, Vector2 offset)
        {
            // 앰비언트를 먼저 붙여야 형제 순서상 뒤(아래)에 깔린다.
            AddShadowLayer(parent, name + "Ambient", radius, spread * AmbientSpreadFactor,
                offset * AmbientOffsetFactor, AmbientShadowAlpha);
            return AddShadowLayer(parent, name, radius, spread, offset, PanelShadow.a);
        }

        private const float AmbientSpreadFactor = 2.4f;   // 34-2: spread 14 -> 34
        private const float AmbientOffsetFactor = 2.3f;   // 34-2: offset -6 -> -14
        private const float AmbientShadowAlpha = 0.28f;

        private static Image AddShadowLayer(Transform parent, string name, int radius, float spread,
            Vector2 offset, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            rt.offsetMin = new Vector2(-spread + offset.x, -spread + offset.y);
            rt.offsetMax = new Vector2(spread + offset.x, spread + offset.y);
            var image = go.GetComponent<Image>();
            image.sprite = RoundedFill(radius + Mathf.RoundToInt(spread));
            image.type = Image.Type.Sliced;
            image.color = new Color(PanelShadow.r, PanelShadow.g, PanelShadow.b, alpha);
            image.raycastTarget = false;
            return image;
        }

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
        //   (d) 바닥에서 떠 있음    → 그림자 2겹(AddShadow가 이미 2겹이다)
        // 넷 중 셋을 알파와 1px 선으로 만들 수 있으므로 블러 없이도 유리로 읽힌다.
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
        /// "원반"으로 보인다. 제곱이면 바깥이 길게 사라져 <b>발광</b>으로 읽힌다. 이건 이미
        /// <see cref="AddShadow"/>가 겪은 것과 같은 문제이며, 그림자 쪽은 9-슬라이스라 이 방식을
        /// 쓸 수 없어 알파 한 겹으로 타협했던 자리다.</para>
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
        /// ★ 34-2의 유리 6겹을 <b>한 번에</b> 만든다. 호출부가 겹 순서를 손으로 다시 적으면 창마다
        /// 유리가 달라진다 — 그래서 순서를 아는 곳을 여기 하나로 못박는다.
        ///
        /// <para>반환값은 <b>컨테이너</b>(그림 없는 RectTransform)다. 호출부는 이 사각형의 크기/위치만
        /// 정하면 되고 안쪽 6겹은 전부 <see cref="Stretch"/>로 따라온다. 그림자를 컨테이너의
        /// <b>형제가 아니라 첫 자식</b>으로 두면 본체 위에 얹혀 패널을 검게 덮으므로, 여기서는
        /// 그림자 → 본체 → 시인 → 하이라이트 → 보더 순으로 <b>형제 순서</b>를 만든다.</para>
        /// </summary>
        /// <param name="alpha">본체 알파. 34-1의 규칙: 큰 창 0.96 / 호버 패널·카드 0.86 / 다이얼 원판 0.72.</param>
        /// <param name="body">본체 표면 — 호출부가 색을 다시 칠하거나 알파를 애니메이션할 대상.</param>
        public static RectTransform AddGlassPanel(Transform parent, string name, float alpha, int radius,
            out Image body)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var container = go.GetComponent<RectTransform>();

            // (1) 그림자 2겹 — 넓고 옅은 앰비언트가 "떠 있음"을, 좁고 진한 키가 "가장자리"를 만든다.
            AddShadow(container, "Shadow", radius, 14f, new Vector2(0f, -6f));

            // (2) 본체.
            body = AddSurface(container, "Body", new Color(PanelSurface.r, PanelSurface.g, PanelSurface.b, alpha), radius);
            Stretch(body.rectTransform);
            body.raycastTarget = false;

            // (3) 세로 시인 — 위쪽이 더 밝다.
            var sheenGo = new GameObject("Sheen", typeof(RectTransform), typeof(Image));
            sheenGo.transform.SetParent(container, false);
            Stretch(sheenGo.GetComponent<RectTransform>());
            var sheen = sheenGo.GetComponent<Image>();
            sheen.sprite = VerticalGradientFill(radius);
            sheen.type = Image.Type.Simple;
            sheen.color = PanelSheen;
            sheen.raycastTarget = false;

            // (4) 안쪽 상단 1px 하이라이트 — 코너 곡선 구간을 피해야 선이 곡면 위에 얹히지 않는다.
            var hi = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
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
            hiImage.color = PanelHighlight;
            hiImage.raycastTarget = false;

            // (5) 보더.
            AddOutline(container, "Border", PanelBorder, radius);
            return container;
        }

        public static Text AddText(Transform parent, string name, int fontSize, TextAnchor anchor,
            Color color, bool bold = false, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
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
