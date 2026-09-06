using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 부채꼴 버튼 글리프 <b>계측 리그</b>(자) — 판정은 <c>GearFanGlyphGateTests.cs</c>에 있다.
    ///
    /// <para><b>이 파일이 하는 일 하나</b>: 프로덕션 빌더 5종(<c>BuildStopwatchSymbol</c> …)이
    /// <b>실제로 만든 <see cref="Image"/></b>를 실행 시점에 훑어 「잉크 코어」 목록으로 되읽는다.
    /// 소스를 파싱하지 않는다 — 파싱은 «내가 구현했다고 믿는 것»을 재고, 이쪽은 «화면에 그려질 것»을 잰다.</para>
    ///
    /// <para><b>모형이 단 하나인 이유</b>: 이 앱의 심볼 잉크는 예외 없이 <b>「중심선 + 반지름」</b>이다.
    /// <list type="bullet">
    ///   <item><see cref="UiChrome.AddStroke"/>/<c>AddPolyline</c> 캡슐 → 중심선이 <b>선분</b></item>
    ///   <item><see cref="UiChrome.AddCircle"/> 링 → 중심선이 <b>원호</b>(반지름 (Ro+Ri)/2)</item>
    ///   <item><see cref="UiChrome.AddCircle"/> 채운 원반 → 중심선이 <b>한 점</b>(길이 0 선분) + 반지름 R</item>
    /// </list>
    /// 그래서 거리·포함·래스터를 전부 한 벌로 쓴다.</para>
    ///
    /// <para>★ <b>거울을 두 번 잰다</b>(CLAUDE.md 「계산기를 만들면 알려진 값으로 먼저 교정한다」).
    /// 획 두께를 <c>sizeDelta − 2·EdgeFeather</c>로 한 번, <b>스프라이트가 실제로 구운 알파</b>에서
    /// 한 번 재고 둘이 어긋나면 그 자리에서 실패시킨다. 두 값이 갈라지면 아래 모든 숫자가 무효다.</para>
    /// </summary>
    public sealed partial class GearFanGlyphGateTests
    {
        private const string LogPrefix = "[부채꼴글리프-TEST]";

        // ================================================================================
        // 잉크 코어 — 「중심선 + 반지름」
        // ================================================================================

        internal struct InkCore
        {
            /// <summary><b>조형 조각</b>의 이름 = <see cref="GameObject"/> 이름.
            /// ★ 꺾은선 한 조각은 선분마다 <see cref="Image"/>를 만들므로 <b>여러 코어가 같은 이름을 공유</b>한다.
            /// FG-4/FG-7이 세는 「조각」은 이 <b>이름의 가짓수</b>이지 코어 개수가 아니다.</summary>
            public string Piece;

            public bool IsArc;

            // 선분 중심선(캡슐/원반). 원반이면 A == B이고 HalfThickness가 반지름이다.
            public Vector2 A, B;

            // 원호 중심선(링).
            public Vector2 Center;
            public float MedialRadius;
            public float StartDeg, EndDeg;

            /// <summary>코어 반두께. 획이면 두께/2, 채운 원반이면 반지름.</summary>
            public float HalfThickness;

            /// <summary><c>true</c>면 <b>채운 덩어리</b>(FG-2 획 사다리 대상 밖, FG-4 최소 폭 대상).</summary>
            public bool IsFill;

            /// <summary>링 안쪽 구멍의 지름(링이 아니면 0). FG-4의 「구멍 최소 폭」이 이 값을 본다.</summary>
            public float HoleDiameter;

            public Color Color;

            /// <summary>코어 가장자리에서 <b>알파가 0이 되는 지점</b>까지의 거리(pt, 한쪽 변).
            /// 획(캡슐)에서만 잰다 — 원/원반은 0.
            /// <para>★ 이 값이 <c>EdgeFeather</c>인지 <c>EdgeFeather/2</c>인지가 FG-3 화소 유도의
            /// 분기점이다. <c>alpha = clamp01((core − d)/feather + 0.5)</c>이므로 알파 0은
            /// <c>d = core + feather/2</c>에서 온다 — 즉 코어 간극 g에 남는 알파 0 골은
            /// <b>g − feather</b>(양변 합)이지 <c>g − 2×feather</c>가 아니다.
            /// <c>DESIGN_FAN_MENU_ICONS</c> §1-3의 «골 = g − 1.0pt»가 그 오해였다
            /// (design-art R27 교정6이 텍셀 굽기를 재현해 정정했다).</para></summary>
            public float ZeroAlphaExtent;

            public float Thickness => HalfThickness * 2f;
        }

        internal sealed class Glyph
        {
            public int Slot;
            public string Label;
            public readonly List<InkCore> Cores = new List<InkCore>();
            public readonly List<string> InactiveImages = new List<string>();

            /// <summary><c>ButtonView.SymbolFixedParts</c>의 <b>배열 길이</b> = Accent 고정 조각이
            /// 실현된 <see cref="Image"/> 개수. ★ 이것으로 FG-7을 세면 <b>거짓 빨강</b>이 난다 —
            /// 그 함정을 <c>대조_FG7을_Image_개수로_세면_거짓_빨강이_난다</c>가 증명한다.</summary>
            public int FixedPartImageCount;

            public List<string> PieceNames;
            public Dictionary<string, List<InkCore>> Pieces;

            public bool[] Mask;
            public int InkCells;
            public float InkAreaPoints, InkPercent, InkWidth, InkHeight, InkDiagonal, RMax;

            public override string ToString() => $"{Slot}·{Label}";
        }

        // ================================================================================
        // 되읽기 — Image 하나 → 코어 하나
        // ================================================================================

        /// <summary>스프라이트가 실제로 구운 알파에서 캡슐의 <b>코어 비율</b>을 잰다
        /// (<see cref="UiChrome.AddStroke"/>가 <c>Capsule(thickness / boxHeight)</c>로 굽는 그 값).</summary>
        private static float MeasureCapsuleCoreFraction(Sprite sprite)
        {
            Texture2D tex = SpriteTexture(sprite);
            int w = tex.width, h = tex.height;
            float half = h * 0.5f;
            Color32[] px = PixelsOf(tex);

            // 9-슬라이스의 <b>가운데</b> 열(양 끝 캡 밖). 이 열에서 알파는 |y − 중심|의 함수다.
            int col = w / 2;
            float crossing = -1f;
            float previousD = 0f, previousA = 0f;
            for (int y = h / 2; y < h; y++)
            {
                float d = (y + 0.5f) - half;
                float a = px[y * w + col].a / 255f;
                if (y > h / 2 && previousA >= 0.5f && a < 0.5f)
                {
                    crossing = Mathf.Lerp(previousD, d, (previousA - 0.5f) / Mathf.Max(1e-6f, previousA - a));
                    break;
                }
                previousD = d; previousA = a;
            }
            Assert.Greater(crossing, 0f,
                $"{LogPrefix} 캡슐 스프라이트에서 알파 0.5 등고선을 찾지 못했습니다({sprite.name}) — " +
                "UiChrome.Capsule의 굽는 식이 바뀌었다면 이 자(尺)도 함께 고쳐야 합니다.");
            return crossing / half;
        }

        /// <summary>캡슐 스프라이트에서 <b>알파가 처음 0이 되는</b> 지점을 텍스처 반두께 대비 비율로.
        /// <para>FG-3 화소 유도가 <c>g − EdgeFeather</c>인지 <c>g − 2×EdgeFeather</c>인지를
        /// <b>측정으로</b> 가른다 — 주석이 아니라 구워진 알파가 그 답을 갖고 있다.</para></summary>
        private static float MeasureCapsuleZeroAlphaFraction(Sprite sprite)
        {
            Texture2D tex = SpriteTexture(sprite);
            int w = tex.width, h = tex.height;
            float half = h * 0.5f;
            Color32[] px = PixelsOf(tex);

            int col = w / 2;
            for (int y = h / 2 + 1; y < h; y++)
            {
                if (px[y * w + col].a != 0) continue;
                // 마지막 «알파 있는» 텍셀 중심과 첫 «알파 0» 텍셀 중심의 <b>중점</b>.
                // 첫 0 텍셀을 그대로 쓰면 언제나 한 텍셀만큼 위로 치우친다(굵은 획일수록 크다).
                return (y - half) / half;
            }
            return 1f;   // 램프가 텍스처 끝까지 간다 = 알파 0 화소가 없다
        }

        /// <summary>원 스프라이트의 <b>바깥/안쪽</b> 알파 0.5 등고선을 텍스처 반지름 대비 비율로 돌려준다.
        /// 안쪽이 없으면(=채운 원반) <paramref name="innerFraction"/>이 0이다.</summary>
        private static void MeasureCircleSprite(Sprite sprite, out float outerFraction, out float innerFraction)
        {
            Texture2D tex = SpriteTexture(sprite);
            int size = tex.width;
            float half = size * 0.5f;
            Color32[] px = PixelsOf(tex);

            int row = size / 2;
            float dy = (row + 0.5f) - half;

            outerFraction = -1f; innerFraction = 0f;
            float previousD = -1f, previousA = -1f;
            for (int x = size / 2; x < size; x++)
            {
                float dx = (x + 0.5f) - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = px[row * size + x].a / 255f;

                if (previousA >= 0f)
                {
                    if (previousA < 0.5f && a >= 0.5f && innerFraction <= 0f)
                        innerFraction = Mathf.Lerp(previousD, d, (0.5f - previousA) / Mathf.Max(1e-6f, a - previousA)) / half;
                    if (previousA >= 0.5f && a < 0.5f)
                    {
                        outerFraction = Mathf.Lerp(previousD, d, (previousA - 0.5f) / Mathf.Max(1e-6f, previousA - a)) / half;
                        break;
                    }
                }
                previousD = d; previousA = a;
            }

            Assert.Greater(outerFraction, 0f,
                $"{LogPrefix} 원 스프라이트에서 바깥 알파 0.5 등고선을 찾지 못했습니다({sprite.name}) — " +
                "UiChrome.CircleSprite의 굽는 식이 바뀌었다면 이 자(尺)도 함께 고쳐야 합니다.");
        }

        private static readonly Dictionary<Texture2D, Color32[]> _pixelCache =
            new Dictionary<Texture2D, Color32[]>();

        private static Color32[] PixelsOf(Texture2D tex)
        {
            if (_pixelCache.TryGetValue(tex, out Color32[] cached)) return cached;
            Color32[] px = tex.GetPixels32();
            _pixelCache[tex] = px;
            return px;
        }

        private static Texture2D SpriteTexture(Sprite sprite)
        {
            Assert.NotNull(sprite, $"{LogPrefix} 심볼 조각에 스프라이트가 없습니다 — 되읽기가 불가능합니다.");
            Texture2D tex = sprite.texture;
            Assert.NotNull(tex, $"{LogPrefix} 스프라이트 '{sprite.name}'에 텍스처가 없습니다.");
            Assert.Greater(tex.width, 8, $"{LogPrefix} 스프라이트 '{sprite.name}'의 텍스처가 너무 작습니다.");
            return tex;
        }

        /// <summary><see cref="Image"/> 하나를 잉크 코어 하나로 되읽는다.</summary>
        private static InkCore ReadCore(Image img)
        {
            RectTransform rt = img.rectTransform;
            Vector2 box = rt.sizeDelta;
            Vector2 center = rt.anchoredPosition;
            float feather = UiChrome.EdgeFeatherPoints;

            // 회전은 <b>오일러 각으로 읽지 않는다</b>. Quaternion.Euler(0,0,180)을 되돌리면 Unity가
            // (180,180,0)으로 분해해 z가 0으로 보이는 자리가 실제로 있다(체크리스트 박스의 윗변이
            // 정확히 180°다). 축 벡터를 직접 회전시켜 방향을 뽑으면 그 함정이 사라진다.
            Vector3 axis = rt.localRotation * Vector3.right;
            Assert.Less(Mathf.Abs(axis.z), 1e-3f,
                $"{LogPrefix} '{img.name}'의 회전이 평면(z축) 회전이 아닙니다 — 이 자(尺)의 모형이 성립하지 않습니다.");
            var dir = new Vector2(axis.x, axis.y).normalized;
            float rotDeg = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;

            Assert.NotNull(img.sprite, $"{LogPrefix} '{img.name}'에 스프라이트가 없습니다.");
            bool nineSliced = img.sprite.border.x > 0f;

            if (nineSliced)
            {
                // ---- UiChrome.AddStroke 캡슐 ----
                Assert.AreEqual(Image.Type.Sliced, img.type,
                    $"{LogPrefix} '{img.name}'은 9-슬라이스 스프라이트인데 Image.Type이 {img.type}입니다 — " +
                    "캡 반원이 늘어나 획 끝이 창끝처럼 뾰족해집니다(UiChrome 2026-08-30 (B)).");

                float thickness = box.y - feather * 2f;
                float visibleLength = box.x - feather * 2f;

                // ★ 거울 교정 — 같은 두께를 <b>구워진 알파</b>에서 한 번 더 잰다.
                //   허용오차는 Capsule 캐시 키의 양자화(코어 비율 1/200)에서 온다.
                float baked = MeasureCapsuleCoreFraction(img.sprite) * box.y;
                Assert.AreEqual(thickness, baked, ThicknessTolerance,
                    $"{LogPrefix} '{img.name}'의 획 두께가 두 자(尺)에서 다릅니다 — " +
                    $"상자 기준 {thickness:F3}pt vs 스프라이트가 구운 값 {baked:F3}pt. " +
                    "둘 중 하나의 규약이 바뀌었다는 뜻이고, 그 전까지 이 파일의 모든 숫자는 무효입니다.");

                float half = Mathf.Max(0f, visibleLength * 0.5f - thickness * 0.5f);

                // 알파 0 경계까지의 거리(코어 가장자리 기준). FG-3 화소 유도의 근거를 <b>측정</b>으로 둔다.
                float zeroExtent = MeasureCapsuleZeroAlphaFraction(img.sprite) * (box.y * 0.5f)
                                   - thickness * 0.5f;

                return new InkCore
                {
                    Piece = img.name,
                    IsArc = false,
                    A = center - dir * half,
                    B = center + dir * half,
                    HalfThickness = thickness * 0.5f,
                    ZeroAlphaExtent = zeroExtent,
                    Color = img.color,
                };
            }

            // ---- UiChrome.AddCircle (링 또는 채운 원반) ----
            Assert.AreEqual(box.x, box.y, 0.001f,
                $"{LogPrefix} '{img.name}'의 원 상자가 정사각형이 아닙니다({box.x:F2}×{box.y:F2}).");
            float diameter = box.x - feather * 2f;
            MeasureCircleSprite(img.sprite, out float outerFraction, out float innerFraction);

            float outerRadius = diameter * 0.5f;
            float innerRadius = outerRadius * (innerFraction / outerFraction);

            if (innerFraction <= 0f)
            {
                // 채운 원반 — AddCircle의 ringThickness 생략이 이것이다.
                return new InkCore
                {
                    Piece = img.name,
                    IsArc = false,
                    A = center, B = center,
                    HalfThickness = outerRadius,
                    IsFill = true,
                    Color = img.color,
                };
            }

            ArcRange(img, rotDeg, out float startDeg, out float endDeg);
            return new InkCore
            {
                Piece = img.name,
                IsArc = true,
                Center = center,
                MedialRadius = (outerRadius + innerRadius) * 0.5f,
                StartDeg = startDeg,
                EndDeg = endDeg,
                HalfThickness = (outerRadius - innerRadius) * 0.5f,
                HoleDiameter = innerRadius * 2f,
                Color = img.color,
            };
        }

        /// <summary><see cref="Image.Type.Filled"/>/<see cref="Image.FillMethod.Radial360"/> 배선에서
        /// 실제로 <b>그려지는 각도 구간</b>을 유도한다. 전원 기호의 「트인 틈」이 이 계산의 산물이다.</summary>
        private static void ArcRange(Image img, float rotDeg, out float startDeg, out float endDeg)
        {
            if (img.type != Image.Type.Filled)
            {
                startDeg = rotDeg; endDeg = rotDeg + 360f;
                return;
            }

            Assert.AreEqual(Image.FillMethod.Radial360, img.fillMethod,
                $"{LogPrefix} '{img.name}'이 Radial360이 아닌 채움({img.fillMethod})을 씁니다 — " +
                "이 자(尺)는 그 모양을 되읽지 못합니다.");

            float origin;
            if (img.fillOrigin == (int)Image.Origin360.Right) origin = 0f;
            else if (img.fillOrigin == (int)Image.Origin360.Top) origin = 90f;
            else if (img.fillOrigin == (int)Image.Origin360.Left) origin = 180f;
            else if (img.fillOrigin == (int)Image.Origin360.Bottom) origin = 270f;
            else { origin = 0f; Assert.Fail($"{LogPrefix} '{img.name}'의 fillOrigin({img.fillOrigin})을 모릅니다."); }

            float sweep = Mathf.Clamp01(img.fillAmount) * 360f;
            if (img.fillClockwise) { startDeg = origin - sweep; endDeg = origin; }
            else { startDeg = origin; endDeg = origin + sweep; }
            startDeg += rotDeg; endDeg += rotDeg;
        }

        // ================================================================================
        // 프로덕션 상수 — 숫자를 베끼지 않는다
        // ================================================================================

        /// <summary><c>private const</c>를 <b>참조</b>로 읽는다. 이름이 바뀌면 <b>실패</b>한다 —
        /// 조용히 0을 돌려주면 「상수가 사라졌다」가 「값이 0이다」로 둔갑한다.</summary>
        private static float PrivateFloatConst(string name)
        {
            FieldInfo f = typeof(GearRadialMenuWidget).GetField(name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(f,
                $"{LogPrefix} GearRadialMenuWidget에서 상수 '{name}'을 찾지 못했습니다. " +
                "이름이 바뀌었다면 읽는 쪽도 함께 갱신하십시오 — 그 전까지 이 검산은 <b>대상 없이</b> 돌고, " +
                "그 상태로 초록불이 뜨는 것이 이 저장소가 반복해 온 실패입니다.");
            object value = f.GetValue(null);
            Assert.IsInstanceOf<float>(value, $"{LogPrefix} 상수 '{name}'이 float이 아닙니다({value?.GetType()}).");
            return (float)value;
        }
    }
}
