using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★★ <b>교정 스프라이트</b> — 그림이 한 장도 없는 상태에서 「부착·배율·z·반전·잉크박스·숨김」을
    /// <b>숫자로</b> 검증하기 위한 절차적 텍스처. 2026-09-09,
    /// docs/GAME_ARCHITECTURE_REVIEW.md §19-3 「P0가 그림 없이 가능한 이유」.
    ///
    /// ============================================================================
    /// 왜 이것이 <b>프로덕션 어셈블리</b>에 있는가
    /// ============================================================================
    /// 소비자가 셋이고 그 셋이 서로 다른 어셈블리에 있다:
    /// EditMode 테스트 · PlayMode 테스트(<c>InternalsVisibleTo</c> 대상이 아니다) ·
    /// 에디터 임포트 도구(<c>Assembly-CSharp-Editor</c>, 테스트 어셈블리를 참조할 수 없다).
    /// 세 곳에 각자 그리면 <b>「기대값」과 「측정값」이 같은 실수를 공유</b>하게 되고, 그건 교정의
    /// 목적을 정면으로 부순다. 그래서 <b>한 벌</b>을 모두가 참조할 수 있는 자리에 둔다.
    /// <para><see cref="RopeClimbQaOverride"/>·<see cref="StickMateDevTools"/>와 같은 부류다 —
    /// 배포 빌드에 들어가지만 <b>아무도 부르지 않으면 한 바이트도 할당하지 않는다</b>
    /// (정적 필드 없음, 순수 팩토리).</para>
    ///
    /// ============================================================================
    /// 그림의 규약 — <b>이 값들이 곧 기대값이다</b>
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>알파는 이진</b>이다(0 또는 255, 중간값 없음). 저작 규격과 <b>같은</b> 규약이라
    ///     (§19-8-2), 이 그림으로 통과한 검사는 실제 아트에도 같은 뜻을 갖는다.</item>
    ///   <item>잉크는 캔버스 <b>정중앙 50%</b>에만 있다(사방 여백 <see cref="MarginFraction"/> = 0.25).
    ///     즉 <b>타이트 박스가 캔버스보다 확실히 작다</b> — 여백을 잉크로 세는 결함이 있으면
    ///     정확히 <c>1 / 0.5 = 2배</c>로 틀린다.</item>
    ///   <item>네 모서리 마커의 크기가 <b>전부 다르다</b>(8/12/16/20 px). 좌우 반전·상하 반전·
    ///     90도 회전이 각각 다른 방식으로 틀린다 — 「뒤집혔는데 대칭이라 통과」가 불가능하다.</item>
    ///   <item>오른쪽 안쪽에만 있는 <b>탭</b> 하나가 좌우 반전의 직접 증거다.</item>
    /// </list>
    /// </summary>
    public static class WornSpriteCalibration
    {
        /// <summary>캔버스 한 변(px). 출하 권고 크기와 같다(§19-8-4: 저작 1024², 출하 256²).</summary>
        public const int Edge = 256;

        /// <summary>사방 투명 여백의 비율. <b>이 값이 곧 테스트의 기대값</b>이다 —
        /// 타이트 박스는 정확히 <c>[0.25, 0.75]</c>여야 한다.</summary>
        public const float MarginFraction = 0.25f;

        /// <summary>잉크 사각형의 정규화 최소값(= <see cref="MarginFraction"/>).</summary>
        public const float InkMinNormalized = MarginFraction;

        /// <summary>잉크 사각형의 정규화 최대값(= 1 − <see cref="MarginFraction"/>).</summary>
        public const float InkMaxNormalized = 1f - MarginFraction;

        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);
        private static readonly Color32 Frame = new Color32(20, 20, 20, 255);
        private static readonly Color32 Cross = new Color32(230, 60, 60, 255);
        private static readonly Color32 Corner = new Color32(60, 140, 230, 255);
        private static readonly Color32 Tab = new Color32(240, 190, 40, 255);
        private static readonly Color32 Field = new Color32(245, 245, 245, 255);

        /// <summary>
        /// 교정 텍스처 한 장을 굽는다. <b>부른 쪽이 <see cref="Object.DestroyImmediate(Object)"/>로
        /// 지운다</b> — 24시간 상주 앱에서 새는 자원을 만들지 않기 위해 소유권을 넘긴다.
        /// </summary>
        public static Texture2D BuildTexture()
        {
            var tex = new Texture2D(Edge, Edge, TextureFormat.RGBA32, false)
            {
                name = "WornSpriteCalibration",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color32[Edge * Edge];
            for (int i = 0; i < px.Length; i++) px[i] = Transparent;

            int lo = Mathf.RoundToInt(Edge * MarginFraction);          // 64
            int hi = Mathf.RoundToInt(Edge * InkMaxNormalized) - 1;    // 191 (포함)
            int span = hi - lo + 1;                                    // 128

            // 바탕(잉크 사각형 전체) — 알파 255로 채워야 타이트 박스가 정확히 사각형이 된다.
            Fill(px, lo, lo, span, span, Field);

            // 테두리(안쪽 4px) — 축소되면 가장 먼저 사라지는 요소라 «몇 배로 줄었는가»의 눈금이 된다.
            const int frameWidth = 4;
            Fill(px, lo, lo, span, frameWidth, Frame);
            Fill(px, lo, hi - frameWidth + 1, span, frameWidth, Frame);
            Fill(px, lo, lo, frameWidth, span, Frame);
            Fill(px, hi - frameWidth + 1, lo, frameWidth, span, Frame);

            // 십자선 — 중심이 부착 앵커에 오는지 눈으로 확인하는 표식.
            const int crossWidth = 4;
            int mid = lo + span / 2 - crossWidth / 2;
            Fill(px, lo, mid, span, crossWidth, Cross);
            Fill(px, mid, lo, crossWidth, span, Cross);

            // 모서리 마커 — 크기가 전부 다르다(좌하 8 / 우하 12 / 좌상 16 / 우상 20).
            Fill(px, lo, lo, 8, 8, Corner);
            Fill(px, hi - 12 + 1, lo, 12, 12, Corner);
            Fill(px, lo, hi - 16 + 1, 16, 16, Corner);
            Fill(px, hi - 20 + 1, hi - 20 + 1, 20, 20, Corner);

            // 오른쪽에만 있는 탭 — 좌우 반전의 직접 증거(대칭이 아니므로 «뒤집혔는데 같아 보인다»가 없다).
            Fill(px, hi - 12 + 1, lo + span / 2 - 12, 12, 24, Tab);

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// 교정 스프라이트 한 장. 피벗은 <b>캔버스 정중앙</b>이고(임포트 프리셋과 같은 규약),
        /// <c>pixelsPerUnit</c>은 프로젝트 기본값 100이다.
        /// <para>텍스처의 소유권은 스프라이트에 있지 않다 — 부른 쪽이
        /// <paramref name="texture"/>도 함께 지워야 한다.</para>
        /// </summary>
        public static Sprite BuildSprite(out Texture2D texture)
        {
            texture = BuildTexture();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Edge, Edge),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "WornSpriteCalibrationSprite";
            return sprite;
        }

        /// <summary>이 교정 그림이 <paramref name="rectInR"/>에 놓였을 때의 <b>기대 잉크 박스</b>.
        /// 테스트가 산수를 다시 적지 않게 하기 위한 파생값이다 — 규칙의 주인은
        /// <see cref="WornSpritePlacement.InkBoxFromNormalized"/> 하나다.</summary>
        public static Vector4 ExpectedInkBoxInR(Rect rectInR)
            => WornSpritePlacement.InkBoxFromNormalized(rectInR,
                InkMinNormalized, InkMinNormalized, InkMaxNormalized, InkMaxNormalized);

        private static void Fill(Color32[] px, int x, int y, int w, int h, Color32 color)
        {
            int x1 = Mathf.Min(Edge, x + w);
            int y1 = Mathf.Min(Edge, y + h);
            for (int yy = Mathf.Max(0, y); yy < y1; yy++)
            {
                int row = yy * Edge;
                for (int xx = Mathf.Max(0, x); xx < x1; xx++) px[row + xx] = color;
            }
        }
    }
}
