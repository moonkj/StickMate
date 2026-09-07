using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-07 — <b>글리프 비트맵이 픽셀 격자 위에 놓이는가</b>를 판정하는 순수 규칙.
    /// OS 호출이 한 줄도 없다(플랫폼 중립 위치 = <c>Platform/</c>, CLAUDE.md "정책은 중립 위치에").
    ///
    /// ============================================================================
    /// 이것은 <see cref="UiGlyphScalePolicy"/>와 <b>다른 축</b>이다 — 셋째 축이다
    /// ============================================================================
    /// 레거시 uGUI <c>Text</c>가 흐려지는 경로는 서로 독립인 <b>셋</b>이다. 이 저장소는 지금까지
    /// 앞의 둘만 재고 있었고, 셋째는 <b>한 번도 측정된 적이 없다</b>.
    /// <code>
    ///   (1) 크기 잔차   pt × canvasScale 이 정수가 아니다      → UiGlyphScalePolicy.IsExact
    ///   (2) 조상 스케일 부모 localScale 이 1이 아니다          → UiGlyphScalePolicy.IsResampleFree
    ///   (3) 위상 잔차   글자 블록의 <b>원점</b>이 정수 픽셀이 아니다 → ★ 이 클래스 ★
    /// </code>
    /// (1)과 (2)는 <b>크기</b>의 문제라 "글자가 몇 픽셀로 구워지는가"만 본다. (3)은 <b>위치</b>의
    /// 문제다 — 같은 20px 비트맵이라도 화면 x=100.37px에 얹히면 두 픽셀에 걸쳐 이중선형 보간되어
    /// 획이 이웃 픽셀로 샌다. <b>(1)과 (2)가 완벽해도 (3)은 그대로 남는다.</b>
    ///
    /// ============================================================================
    /// ★ 왜 지금까지 (3)이 항상 켜져 있었는가 — uGUI 소스에서 확인한 사실
    /// ============================================================================
    /// <c>UnityEngine.UI.Text.OnPopulateMesh</c>(ugui 2.0.0) 안의 <b>roundingOffset</b> 두 줄:
    /// <code>
    ///   Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
    ///   roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
    /// </code>
    /// 그리고 그것이 부르는 <c>UnityEngine.UI.Graphic.PixelAdjustPoint</c>:
    /// <code>
    ///   if (!canvas || canvas.renderMode == RenderMode.WorldSpace
    ///       || canvas.scaleFactor == 0.0f || <b>!canvas.pixelPerfect</b>)
    ///       return point;                    // ← 인자를 그대로 돌려준다
    /// </code>
    /// 즉 <b><c>canvas.pixelPerfect</c>가 false면 <c>roundingOffset</c>은 항상 <c>Vector2.zero</c></b>다 —
    /// uGUI는 글자를 <b>격자에 맞추지 않는다</b>. 그리고 이 저장소에서
    /// <c>pixelPerfect</c>를 대입하는 줄은 <b>한 곳도 없다</b>(전수 검색 0건, 캔버스 6개 전부 기본값 false).
    ///
    /// ============================================================================
    /// ★ 그런데 왜 <c>canvas.pixelPerfect = true</c>로 끝내지 않는가
    /// ============================================================================
    /// 그 스위치는 <b>캔버스 전체</b>에 걸린다. <c>Graphic.GetPixelAdjustedRect()</c>가 같은 플래그를
    /// 보고 <b>모든 <c>Image</c>의 사각형</b>을 <c>min</c>/<c>max</c> 각각 반올림하는데, 이 저장소의
    /// UI는 <b>1pt 미만 두께의 선</b>과 <b>회전된 획</b>과 <b>균일 축소된 묶음</b>으로 만들어져 있다:
    /// <list type="bullet">
    ///   <item><c>UiChrome.AddStroke</c> — <c>localRotation</c>이 걸린 대각선 획</item>
    ///   <item><c>CharacterInfoWindow</c>의 ±45° 셰브론, <c>FocusSessionPopover</c>의 눈금</item>
    ///   <item>부채꼴 메뉴의 Ø36 축소 폴백(<c>Group.localScale = 36/44</c>)</item>
    /// </list>
    /// min/max를 <b>따로</b> 반올림하면 <b>두께 자체가 바뀐다</b>(1.875px 획 → 1px 또는 2px). 그것은
    /// <c>design-art</c>가 대비값으로 교정해 둔 수치를 소리 없이 무너뜨린다.
    /// ⇒ <b>글자에만</b> 거는 방법이 필요하고, 그것이 <c>Interaction/CrispText</c>다.
    ///
    /// ============================================================================
    /// 이 규칙이 <b>고치지 못하는 것</b>(정직하게 남긴다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>낱글자 사이의 위상은 못 고친다.</b> 이 규칙은 글자 <b>블록의 원점</b> 하나만 격자에
    ///     올린다. 블록 안에서 글자마다 누적되는 advance의 소수부는 <c>TextGenerator</c>(네이티브)
    ///     소관이라 우리가 손댈 수 없다. 첫 글자가 정확하면 나머지도 대체로 정확해지지만 <b>보장은
    ///     아니다</b>.</item>
    ///   <item><b>회전된 글자는 건드리지 않는다</b>(<see cref="IsAxisAligned"/>가 false). 회전 상태에서
    ///     화면 좌표를 반올림하면 글리프 축이 어차피 격자와 어긋나 있어 아무 이득이 없고, 역변환된
    ///     이동량이 글자를 <b>비스듬히</b> 밀어 오히려 위치만 틀어진다.</item>
    ///   <item><b>움직이는 표면에서는 대가가 있다.</b> 말풍선처럼 매 프레임 움직이는 글자는 1 물리
    ///     픽셀 단위로 <b>계단 이동</b>하게 되고, 부드럽게 움직이는 말풍선 테두리와 최대 ±0.5 물리
    ///     픽셀까지 상대적으로 어긋난다. 그 대가를 치르는 이유는 <b>대안이 「항상 번져 있음」</b>이기
    ///     때문이다 — OS의 네이티브 텍스트 렌더러들도 같은 선택을 한다.</item>
    /// </list>
    /// </summary>
    public static class GlyphPixelSnapPolicy
    {
        /// <summary>
        /// 이 축의 <b>실기 로그 태그</b>. 아직 이 태그를 찍는 프로브는 <b>없다</b> —
        /// 크기 축의 <c>GLYPH-SCALE</c>에 대응하는 자리를 미리 못박아 둔 것이다.
        ///
        /// <para><b>왜 상수로 두는가</b>: (1) 프로브가 생기는 날 문자열이 두 벌이 되지 않게,
        /// (2) <b>「계기판이 생겼는가」를 기계적으로 판정</b>할 수 있게. 실제 사고: 이 항목의
        /// 자동 승격 래칫이 처음에는 <c>"Platform/ 안에 GlyphPixelSnapPolicy를 언급하는 파일이 있는가"</c>
        /// 였는데, <b>같은 라운드가 추가한 다른 파일의 주석 한 줄</b>이 그것을 만족시켜
        /// <b>열려 있는 갭이 조용히 초록으로 닫혔다</b>. 이름 언급은 계기판이 아니다 —
        /// <b>이 태그를 실제로 찍는 코드</b>가 계기판이다.</para>
        /// </summary>
        public const string DiagnosticTag = "GLYPH-PHASE";

        /// <summary>"이미 격자 위에 있다"로 볼 허용 오차(물리 픽셀). 이보다 작은 잔차를 고치려고
        /// 메시를 다시 만들면 24시간 상주 앱에서 <b>이득 없는 재빌드</b>만 쌓인다.
        ///
        /// <para>값의 근거: 잔차 0.01px에서 <see cref="PeakCoverage"/>는 0.99다 — 8비트 채널로
        /// 반올림하면 255 중 2.5 계단이라 사람 눈에 닿지 않는다. 반대로 0.1px(=25.5 계단)은
        /// 얇은 획에서 보인다.</para></summary>
        public const float ResidualEpsilon = 0.01f;

        /// <summary>축 정렬 판정의 허용 오차(단위 없는 방향 성분). 부동소수 잡음만 흡수한다 —
        /// 0.001은 각도로 약 0.057°이고, 이 저장소가 UI에 거는 회전은 최소 45°다.</summary>
        public const float AxisAlignEpsilon = 0.001f;

        /// <summary>
        /// <paramref name="screenPixel"/>이 정수 픽셀에서 얼마나 떨어져 있는가(부호 있는 <b>위상 잔차</b>,
        /// 항상 <c>[-0.5, 0.5]</c>).
        ///
        /// <para>이 값이 0이면 글리프 비트맵의 텍셀과 화면 픽셀이 1:1로 겹친다. 0이 아니면 그만큼
        /// 두 픽셀에 나뉘어 얹힌다 — 그것이 사용자가 "번져 보임"이라 부른 것이다.</para>
        ///
        /// <para>NaN/무한은 <b>0</b>(무해)으로 접는다. 못 잰 것을 결함으로 바꾸면 오탐이 되고,
        /// 오탐 한 번이면 아무도 진단을 안 믿는다(<see cref="UiGlyphScalePolicy.NormalizeScale"/>와 같은 규약).</para>
        /// </summary>
        public static float Residual(float screenPixel)
        {
            if (float.IsNaN(screenPixel) || float.IsInfinity(screenPixel)) return 0f;
            return screenPixel - Mathf.Round(screenPixel);
        }

        /// <summary>그 좌표를 격자에 올리기 위해 <b>더해야 할 양</b>(<c>-Residual</c>).</summary>
        public static float SnapDelta(float screenPixel) => -Residual(screenPixel);

        /// <summary>두 축의 잔차가 둘 다 <see cref="ResidualEpsilon"/> 안인가(= 손댈 필요가 없다).</summary>
        public static bool IsPixelAligned(float screenX, float screenY)
            => Mathf.Abs(Residual(screenX)) <= ResidualEpsilon
               && Mathf.Abs(Residual(screenY)) <= ResidualEpsilon;

        /// <summary>
        /// <b>흐림의 크기를 수로 만든 것</b> — 위상 잔차 <paramref name="residual"/>에서 <b>1 픽셀 폭 획</b>이
        /// 유지하는 최대 밝기(1.0 = 손실 없음, 0.5 = 최악).
        ///
        /// <para>모형: 텍셀 폭 1의 획이 offset <c>f</c>만큼 밀려 얹히면 이웃한 두 픽셀이 각각
        /// <c>1-f</c>와 <c>f</c>를 받는다(이중선형). 그래서 최대 밝기는 <c>max(f, 1-f) = 1 - |잔차|</c>다.</para>
        ///
        /// <para><b>왜 이 함수가 여기 있는가</b>: 이 저장소는 "번져 보인다"는 <b>인상</b>을 놓고 한
        /// 라운드를 허비한 적이 있다. 인상은 반증할 수 없지만 <b>0.75</b>는 반증할 수 있다.
        /// EditMode 테스트가 실기 없이 이 축을 잴 수 있는 것도 이 함수 덕이다.</para>
        ///
        /// <para>★ <b>교정값</b>(테스트가 매번 확인한다): 잔차 0 → 1.0(픽셀 정합) /
        /// 잔차 0.5 → 0.5(최악, 획이 두 픽셀에 반반) / 잔차의 균등 평균 기댓값 → 0.75.</para>
        /// </summary>
        public static float PeakCoverage(float residual)
        {
            if (float.IsNaN(residual) || float.IsInfinity(residual)) return 1f;
            return 1f - Mathf.Min(0.5f, Mathf.Abs(residual));
        }

        /// <summary>그 잔차에서 획이 번져 나가는 <b>픽셀 수</b>(1 = 안 번짐, 2 = 두 픽셀에 걸침).
        /// 사람이 읽는 진단 문구용이다.</summary>
        public static int SpreadPixels(float residual)
            => Mathf.Abs(Residual(residual)) <= ResidualEpsilon ? 1 : 2;

        /// <summary>
        /// 그 글자의 로컬 축이 화면 축과 <b>나란한가</b>(회전/기울임이 없는가).
        /// 인자는 <c>localToWorldMatrix</c>가 <c>right</c>/<c>up</c>을 옮긴 결과의 x·y 성분이다.
        ///
        /// <para>축이 어긋나 있으면 <b>스냅을 포기한다</b> — 클래스 문서의 "고치지 못하는 것" 참고.
        /// 뒤집힘(<c>xAxisX &lt;= 0</c> 등)도 같이 거른다: 이 저장소는 UI에 음수 스케일을 쓰지 않기로
        /// 했고(<c>CharacterPetRenderer</c>의 "localScale.x = -1 금지 규약"), 그런 값이 들어왔다면
        /// 우리가 모르는 경로라는 뜻이라 손대지 않는 편이 안전하다.</para>
        /// </summary>
        public static bool IsAxisAligned(float xAxisX, float xAxisY, float yAxisX, float yAxisY)
        {
            if (float.IsNaN(xAxisX) || float.IsNaN(xAxisY) || float.IsNaN(yAxisX) || float.IsNaN(yAxisY)) return false;
            if (float.IsInfinity(xAxisX) || float.IsInfinity(xAxisY)
                || float.IsInfinity(yAxisX) || float.IsInfinity(yAxisY)) return false;
            if (xAxisX <= 0f || yAxisY <= 0f) return false;                 // 뒤집힘/영 스케일.
            return Mathf.Abs(xAxisY) <= AxisAlignEpsilon
                && Mathf.Abs(yAxisX) <= AxisAlignEpsilon;
        }

        /// <summary>
        /// 사람이 읽는 한 줄 진단. <c>[GLYPH-PHASE]</c> 로그와 테스트 실패 메시지가 같은 문장을 쓰도록
        /// <b>여기 한 곳</b>에 둔다(사본을 두면 둘이 갈라지고, 그것이 이 저장소가 반복해 겪은 형태다).
        /// </summary>
        public static string Describe(float screenX, float screenY)
        {
            float rx = Residual(screenX);
            float ry = Residual(screenY);
            float worst = Mathf.Abs(rx) >= Mathf.Abs(ry) ? rx : ry;
            return IsPixelAligned(screenX, screenY)
                ? $"글자 원점=({screenX:F2}, {screenY:F2})px — 정수 픽셀 격자 위(위상 잔차 " +
                  $"{rx:+0.000;-0.000;0.000}, {ry:+0.000;-0.000;0.000}). 획이 한 픽셀에 온전히 들어갑니다."
                : $"글자 원점=({screenX:F2}, {screenY:F2})px — 위상 잔차 " +
                  $"({rx:+0.000;-0.000;0.000}, {ry:+0.000;-0.000;0.000})만큼 격자에서 벗어나 " +
                  $"1px 획이 {SpreadPixels(worst)}픽셀에 걸쳐 이중선형 보간됩니다. " +
                  $"최대 밝기가 {PeakCoverage(worst) * 100f:F0}%로 떨어집니다(격자 위였다면 100%).";
        }
    }
}
