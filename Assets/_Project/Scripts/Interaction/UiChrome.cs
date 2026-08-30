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
    /// 이 앱 전체가 손그림 톤이라 UI가 화려하면 붕 뜬다. 그래서 채도가 낮은 회색 계열 표면에
    /// <b>강조색 하나</b>(<see cref="Accent"/>)만 쓰고, 입체감은 아주 옅은 그림자 한 겹과 1px 테두리로만
    /// 낸다. 그라데이션/광택/네온은 쓰지 않는다.
    /// </summary>
    public static class UiChrome
    {
        // ==================== 여백 체계 (4의 배수, 5단계) ====================
        public const float Space1 = 4f;
        public const float Space2 = 8f;
        public const float Space3 = 12f;
        public const float Space4 = 16f;
        public const float Space6 = 24f;

        // ==================== 모서리 반지름 ====================
        public const int RadiusPanel = 14;
        public const int RadiusCard = 10;
        public const int RadiusChip = 8;

        // ==================== 색 ====================
        /// <summary>창 바탕. 거의 불투명해야 어떤 배경 위에서도 글자가 읽힌다.</summary>
        public static readonly Color PanelSurface = new Color(0.972f, 0.976f, 0.984f, 0.985f);
        public static readonly Color PanelShadow = new Color(0f, 0f, 0f, 0.16f);
        public static readonly Color PanelBorder = new Color(0f, 0f, 0f, 0.10f);

        /// <summary>카드/입력칸 표면 — 바탕보다 <b>밝게</b> 띄워 층이 읽히게 한다.</summary>
        public static readonly Color CardSurface = new Color(1f, 1f, 1f, 0.92f);
        public static readonly Color CardBorder = new Color(0.10f, 0.12f, 0.16f, 0.10f);

        /// <summary>비활성/잠긴 카드 — 표면을 낮춰 "지금 쓸 수 없는 것"이 한눈에 보이게.</summary>
        public static readonly Color CardSurfaceMuted = new Color(0.94f, 0.945f, 0.955f, 0.85f);

        /// <summary>바탕보다 살짝 어두운 보조 표면(설명 카드/스탯 칸).</summary>
        public static readonly Color SubtleSurface = new Color(0.945f, 0.952f, 0.964f, 0.95f);

        public static readonly Color Accent = new Color(0.227f, 0.482f, 0.945f, 1f);
        public static readonly Color AccentSurface = new Color(0.227f, 0.482f, 0.945f, 0.10f);
        public static readonly Color AccentBorder = new Color(0.227f, 0.482f, 0.945f, 0.55f);
        public static readonly Color WarmAccent = new Color(0.94f, 0.60f, 0.24f, 1f);

        public static readonly Color TextPrimary = new Color(0.106f, 0.118f, 0.141f, 1f);
        public static readonly Color TextSecondary = new Color(0.353f, 0.376f, 0.412f, 1f);
        public static readonly Color TextTertiary = new Color(0.588f, 0.612f, 0.647f, 1f);
        public static readonly Color TextOnAccent = new Color(0.129f, 0.318f, 0.678f, 1f);

        /// <summary><b>진한</b> <see cref="Accent"/> 채움 위에 얹는 글자/기호. <see cref="TextOnAccent"/>는
        /// 옅은 <see cref="AccentSurface"/> 위 전용이라 진한 채움 위에서는 대비가 모자란다.</summary>
        public static readonly Color OnAccentSolid = new Color(1f, 1f, 1f, 1f);

        public static readonly Color TrackBackground = new Color(0.867f, 0.878f, 0.898f, 1f);
        public static readonly Color Divider = new Color(0f, 0f, 0f, 0.07f);

        // ==================== 타이포그래피 위계 ====================
        public const int FontDisplay = 22;   // 캐릭터 이름.
        public const int FontTitle = 14;     // 창 제목 / 카드 제목.
        public const int FontBody = 12;      // 본문.
        public const int FontLabel = 11;     // 라벨 / 부제.
        public const int FontCaption = 10;   // 캡션 / 각주.

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

        /// <summary>꽉 찬 원.</summary>
        public static Sprite Circle() => CircleSprite(0.5f);

        /// <summary>링. 두께는 <b>지름 대비 비율</b>이다 — 스프라이트가 통째로 늘어나므로 절대 픽셀로
        /// 정할 수 없다(예: Ø44 버튼에 2pt 링이면 2/44 ≈ 0.045).</summary>
        public static Sprite Ring(float thicknessFraction) => CircleSprite(thicknessFraction);

        private static Sprite CircleSprite(float thicknessFraction)
        {
            thicknessFraction = Mathf.Clamp(thicknessFraction, 0.01f, 0.5f);
            int key = Mathf.RoundToInt(thicknessFraction * 1000f);
            if (_circleCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int size = CircleTextureSize;
            float outer = size * 0.5f;
            float inner = outer - thicknessFraction * size;

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
                    float dx = x + 0.5f - outer;
                    float dy = y + 0.5f - outer;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(outer - d);                    // 바깥 1px 안티에일리어싱.
                    if (inner > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01(d - inner));
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

        /// <summary>둥근 끝(캡슐) 스트로크 한 획. 심볼 아이콘이 전부 이걸로 그려진다.</summary>
        private static Sprite _capsule;

        private static Sprite Capsule()
        {
            if (_capsule != null) return _capsule;

            const int w = 128, h = 32;
            float r = h * 0.5f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "UiChrome_Capsule",
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 선분 (r, r) - (w-r, r)까지의 거리 = 캡슐.
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, r, w - r);
                    float dx = px - cx, dy = py - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            _capsule = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _capsule.name = tex.name;
            _capsule.hideFlags = HideFlags.HideAndDontSave;
            return _capsule;
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
            rt.sizeDelta = new Vector2(length, thickness);
            rt.anchoredPosition = center;
            rt.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);

            var image = go.GetComponent<Image>();
            image.sprite = Capsule();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>부모 중심에 놓이는 원(꽉 찬 원 또는 링). 지름 하나로 정사각을 만든다.</summary>
        public static Image AddCircle(Transform parent, string name, float diameter, Color color,
            float ringThickness = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = ringThickness > 0f && diameter > 0f
                ? Ring(ringThickness / diameter)
                : Circle();
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

        /// <summary>패널 뒤에 까는 아주 옅은 그림자 한 겹(과하지 않게 — 리더 지시).</summary>
        public static Image AddShadow(Transform parent, string name, int radius, float spread, Vector2 offset)
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
            image.color = PanelShadow;
            image.raycastTarget = false;
            return image;
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
