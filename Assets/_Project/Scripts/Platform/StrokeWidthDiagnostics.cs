using UnityEngine;
using StickMate.Core;

namespace StickMate.Platform
{
    /// <summary>
    /// 씬에 실제로 그려지고 있는 <b>획 두께</b>를 재서 사람이 읽을 수 있는 한 줄로 만드는 계측기.
    /// 플랫폼 중립이다 — macOS/Windows 어느 쪽 오버레이 감시자든 이걸 부르면 <b>같은 숫자</b>가 나온다.
    ///
    /// <para><b>왜 여기(Platform/)에 있는가 — 2026-09-01 구조 수정.</b>
    /// 이 계측은 <c>Platform/MacOS/MacOverlayStateEnforcer</c> 안에 인라인으로 들어 있었다.
    /// 그 안에는 (a) 월드 유닛 → 물리픽셀 환산, (b) 물리픽셀 → OS 포인트 환산,
    /// (c) 하한(<see cref="StickConfig.MinStrokeScreenPoints"/>) 대비 판정이 함께 있었는데,
    /// 이것들은 전부 <b>플랫폼과 무관한 규칙</b>이다. macOS 전용 폴더에 있으면 Windows가
    /// 물리적으로 호출할 수 없어 같은 질문에 답하려면 코드를 다시 짜야 한다
    /// (<c>FullscreenSuspendPolicy</c> 사고와 같은 형태 — CLAUDE.md "정책은 플랫폼 중립 위치").
    /// <c>Tests/EditMode/PlatformParityAuditTests</c>의 C4 감사가 이 자리를 잠근다.</para>
    ///
    /// <para><b>왜 이 숫자가 중요한가.</b> 사람이 이 줄을 읽는 이유는 하나다 —
    /// "화면상 최소 획 하한이 실제로 지켜지고 있는가". 그래서 판정에 필요한 단위(OS 포인트)와
    /// 하한 자체를 같은 줄에 함께 남긴다. 물리픽셀만 찍으면 Retina/표시배율을 암산해야 하고,
    /// 그 암산이 틀리면 <b>정반대 결론</b>이 나온다(아래 사고 기록).</para>
    ///
    /// <para><b>★ 이 계측기가 고친 실제 사고(2026-09-01).</b> 옛 코드는
    /// <c>startWidth × lossyScale.x × pixelsPerUnit</c>으로 찍고 있었다.
    /// <c>LineRenderer.startWidth</c>는 <b>월드 유닛</b>이고 Transform 스케일을 따라가지 않으므로
    /// (<c>Core/StickmanAgent.MeasureInkHalfWidth</c>의 2026-08-30 실측 주석과 같은 사실)
    /// 곱하면 <b>로그 숫자만</b> 루트 스케일만큼 작아진다. 실기 재현(배율 0.60 / 루트 스케일 0.800 /
    /// 81.8333 물리픽셀·유닛): 찍히던 값 <c>3.20~9.43</c> 물리픽셀 → 참값 <c>4.00~11.79</c> 물리픽셀.
    /// 3.20px는 1.60pt라 <b>하한 2pt 미달로 읽힌다</b> — 실제로는 3.20/0.8 = 4.00px = 정확히 2.00pt로
    /// 하한이 지켜지고 있었다(<c>StickmanAgent.MinStrokeWorldWidth</c> = 2/40.9167 = 0.0488798 유닛
    /// × 81.8333 = 4.000px, 여섯 자리 일치). <b>그림에는 영향이 없어서 더 위험했다</b> —
    /// 아무도 고치지 않고 숫자만 계속 오독된다.</para>
    /// </summary>
    public static class StrokeWidthDiagnostics
    {
        /// <summary>한 번의 훑기 결과. 전부 <b>사실</b>이고 판정 문구는 <see cref="Describe"/>가 만든다.
        ///
        /// <para><b>★ 2026-09-02 — 통을 둘로 나눴다(M6).</b> 하한이 역할에 따라 둘로 갈리면서
        /// (낱선 2.00pt / 채움 경계선 1.00pt) 하나의 최소값을 하나의 하한과 비교하는 구조가
        /// <b>구조적으로 오진</b>하게 됐다. 그대로 뒀다면 Windows 사용자가 보낸 <c>[렌더품질]</c> 줄이
        /// 정상적으로 1.18pt로 그려진 채움 경계선을 "★ 하한 미달 — 결함"으로 신고했을 것이다
        /// (그리고 그 신고를 받은 사람은 멀쩡한 코드를 고치려고 한 라운드를 쓴다).</para></summary>
        public readonly struct Report
        {
            /// <summary>두께가 0보다 큰 LineRenderer 개수(0이면 아직 캐릭터가 안 그려진 프레임).</summary>
            public readonly int LineCount;
            /// <summary>그중 <b>채운 도형의 경계선</b>인 것(<see cref="FillOutlineStroke"/> 표식)의 개수.</summary>
            public readonly int FillOutlineCount;
            /// <summary>월드 1유닛이 몇 물리픽셀인가(직교 카메라가 없으면 0).</summary>
            public readonly float PixelsPerWorldUnit;
            public readonly float MinPixels;
            public readonly float MaxPixels;
            public readonly float MinPoints;
            public readonly float MaxPoints;
            /// <summary>비교 대상 하한(OS 포인트) — <b>낱선</b> 쪽. 상수를 베끼지 않고
            /// <see cref="StickConfig.MinStrokeScreenPoints"/>를 그대로 나른다.</summary>
            public readonly float FloorPoints;
            /// <summary>비교 대상 하한(OS 포인트) — <b>채움 경계선</b> 쪽
            /// (<see cref="StickConfig.MinFillOutlineScreenPoints"/>).</summary>
            public readonly float FillOutlineFloorPoints;

            /// <summary>낱선만 본 최소 <b>잉크 코어</b> 두께(OS 포인트) = 그려진 두께 − 겹수 × 막.
            /// 낱선이 하나도 없으면 0. <b>하한 판정은 이 값으로 한다</b>(아래 ★ 참고).</summary>
            public readonly float MinLineInkCorePoints;
            /// <summary>채움 경계선만 본 최소 <b>잉크 코어</b> 두께(OS 포인트). 그런 선이 없으면 0.</summary>
            public readonly float MinFillOutlineInkCorePoints;
            /// <summary>낱선만 본 최소 <b>그려진</b> 두께(OS 포인트) — 화면에 실제로 찍히는 폭.</summary>
            public readonly float MinLineDrawnPoints;
            /// <summary>채움 경계선만 본 최소 <b>그려진</b> 두께(OS 포인트).</summary>
            public readonly float MinFillOutlineDrawnPoints;

            /// <summary>막 한 겹의 두께(물리픽셀) — <see cref="InkMembraneStroke.MembranePhysicalPixels"/>를
            /// 그대로 나른다(상수를 베끼지 않는다). 0이면 이 보고의 잉크 코어 = 그려진 두께다.</summary>
            public readonly float MembranePixels;
            /// <summary>막 표식을 실제로 달고 있는 선의 개수. 막이 0이면 언제나 0이다.</summary>
            public readonly int MembraneLineCount;

            public Report(int lineCount, int fillOutlineCount, float pixelsPerWorldUnit,
                float minPixels, float maxPixels, float minPoints, float maxPoints,
                float minLineInkCorePoints, float minFillOutlineInkCorePoints,
                float minLineDrawnPoints, float minFillOutlineDrawnPoints,
                float membranePixels, int membraneLineCount,
                float floorPoints, float fillOutlineFloorPoints)
            {
                LineCount = lineCount;
                FillOutlineCount = fillOutlineCount;
                PixelsPerWorldUnit = pixelsPerWorldUnit;
                MinPixels = minPixels;
                MaxPixels = maxPixels;
                MinPoints = minPoints;
                MaxPoints = maxPoints;
                MinLineInkCorePoints = minLineInkCorePoints;
                MinFillOutlineInkCorePoints = minFillOutlineInkCorePoints;
                MinLineDrawnPoints = minLineDrawnPoints;
                MinFillOutlineDrawnPoints = minFillOutlineDrawnPoints;
                MembranePixels = membranePixels;
                MembraneLineCount = membraneLineCount;
                FloorPoints = floorPoints;
                FillOutlineFloorPoints = fillOutlineFloorPoints;
            }

            /// <summary>하한이 지켜지고 있는가 — <b>두 통을 각자의 하한과</b> 비교한다.
            /// 부동소수 여유 0.01pt(= 표시 자릿수)만 준다.
            /// <para>없는 통은 판정에 넣지 않는다(개수 0이면 최소값도 0이라 무조건 미달이 된다).</para>
            ///
            /// <para>★★ <b>2026-09-03 — 비교 대상이 「그려진 두께」에서 「잉크 코어」로 바뀌었다.</b>
            /// 예전에는 <c>lr.startWidth</c>(= 잉크 + 막)를 그대로 하한과 비교했다. 막을 켜는 순간
            /// <b>잉크가 0px인데 이 속성이 참을 돌려주고 <c>[렌더품질]</c>도 「하한 지킴」을 찍는다</b>
            /// (Windows 100% · 배율 0.35). 이 저장소의 서명 사고 — 실패한 측정과 성공한 측정이
            /// 똑같이 생겼다. 이제 <see cref="InkMembraneStroke.InkCore"/>로 되빼서 비교한다.</para></summary>
            public bool FloorHonored
            {
                get
                {
                    if (LineCount == 0) return false;
                    int standalone = LineCount - FillOutlineCount;
                    if (standalone > 0 && MinLineInkCorePoints < FloorPoints - 0.01f) return false;
                    if (FillOutlineCount > 0 && MinFillOutlineInkCorePoints < FillOutlineFloorPoints - 0.01f) return false;
                    return true;
                }
            }
        }

        /// <summary>
        /// 씬의 모든 활성 <see cref="LineRenderer"/>를 <b>한 번</b> 훑어 획 두께를 잰다.
        /// 상주 앱이라 매 프레임 부르는 용도가 아니다 — 진단 로그를 찍는 순간에만 부른다.
        /// </summary>
        /// <param name="cam">직교 카메라. null이거나 직교가 아니면 픽셀 환산이 0이 된다(예외 대신 0).</param>
        /// <param name="config">DPI 배율 수동 오버라이드 출처. null이어도 자동 배율로 폴백한다.</param>
        public static Report Measure(Camera cam, StickConfig config)
        {
            // 세로 물리픽셀 / 세로 월드유닛(= orthographicSize * 2).
            float pixelsPerUnit = cam != null && cam.orthographic && cam.orthographicSize > 0f
                ? cam.pixelHeight / (cam.orthographicSize * 2f)
                : 0f;

            float minWidthPx = float.MaxValue, maxWidthPx = 0f;
            float minLineCorePx = float.MaxValue, minFillOutlineCorePx = float.MaxValue;
            float minLineDrawnPx = float.MaxValue, minFillOutlineDrawnPx = float.MaxValue;
            int lineCount = 0, fillOutlineCount = 0, membraneLineCount = 0;

            // ★ 막은 <b>물리픽셀</b>이 단위라 여기서는 환산이 필요 없다 — 이 계측기가 이미 물리픽셀로
            //   재고 있기 때문이다. StickmanAgent 쪽은 pt/유닛을 거쳐 월드 유닛으로 환산한다.
            //   <b>두 경로가 서로 다른 길로 같은 답에 도달하므로 서로의 검산이 된다.</b>
            const float membranePx = InkMembraneStroke.MembranePhysicalPixels;
            LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                if (lr == null) continue;

                // ★ lossyScale을 곱하지 않는다. startWidth는 월드 유닛이고 Transform 스케일을
                //   따라가지 않는다(클래스 문서의 사고 기록 참고). 위치/길이는 따라가지만 두께는 아니다.
                float widthPx = lr.startWidth * pixelsPerUnit;
                if (widthPx <= 0f) continue;
                lineCount++;
                if (widthPx < minWidthPx) minWidthPx = widthPx;
                if (widthPx > maxWidthPx) maxWidthPx = widthPx;

                // ★ 2026-09-02 M6 — 이 선이 어느 하한 소속인지는 <b>선 자신에게 묻는다</b>.
                //   이름/목록으로 가르면 DLC 도형이 조용히 빠져나간다(FillOutlineStroke 문서).
                // ★★ 막을 되뺀 <b>잉크 코어</b>가 하한 판정의 대상이다. 막이 걸렸는지도
                //    이름/역할이 아니라 <b>선 자신에게</b> 묻는다(FillOutlineStroke와 같은 방식) —
                //    막에서 제외된 선까지 되빼면 그 선의 잉크를 과소평가해 거짓 빨강이 난다.
                int membraneSides = InkMembraneStroke.AppliedSides(lr);
                if (membraneSides > 0) membraneLineCount++;
                float corePx = InkMembraneStroke.InkCore(widthPx, membranePx, membraneSides);

                if (FillOutlineStroke.Is(lr))
                {
                    fillOutlineCount++;
                    if (corePx < minFillOutlineCorePx) minFillOutlineCorePx = corePx;
                    if (widthPx < minFillOutlineDrawnPx) minFillOutlineDrawnPx = widthPx;
                }
                else
                {
                    if (corePx < minLineCorePx) minLineCorePx = corePx;
                    if (widthPx < minLineDrawnPx) minLineDrawnPx = widthPx;
                }
            }
            if (lineCount == 0) minWidthPx = 0f;
            if (minLineCorePx == float.MaxValue) minLineCorePx = 0f;
            if (minFillOutlineCorePx == float.MaxValue) minFillOutlineCorePx = 0f;
            if (minLineDrawnPx == float.MaxValue) minLineDrawnPx = 0f;
            if (minFillOutlineDrawnPx == float.MaxValue) minFillOutlineDrawnPx = 0f;

            // OS 포인트 = Unity 픽셀 x DpiScale(Retina 2x -> 0.5, Windows 표시배율 125% -> 0.8).
            // 곱셈 한 번이라 카메라가 없어 pixelsPerUnit이 0이어도 0이 나올 뿐 NaN이 생기지 않는다.
            float dpiScale = ScreenCoordinateConverter.ResolveDpiScale(config);
            return new Report(lineCount, fillOutlineCount, pixelsPerUnit,
                minWidthPx, maxWidthPx,
                minWidthPx * dpiScale, maxWidthPx * dpiScale,
                minLineCorePx * dpiScale, minFillOutlineCorePx * dpiScale,
                minLineDrawnPx * dpiScale, minFillOutlineDrawnPx * dpiScale,
                membranePx, membraneLineCount,
                StickConfig.MinStrokeScreenPoints, StickConfig.MinFillOutlineScreenPoints);
        }

        /// <summary>
        /// 로그 한 줄에 끼워 넣을 조각. <b>판정까지</b> 문장으로 적는다 —
        /// 읽는 사람이 단위 환산을 암산하게 두면 그 암산이 틀린다.
        /// </summary>
        public static string Describe(in Report r)
        {
            if (r.LineCount == 0) return "LineRenderer 0개(아직 캐릭터가 그려지기 전 — 획 두께 미측정)";

            string verdict = r.FloorHonored
                ? "하한 지켜짐"
                : "★ 하한 미달 — 결함";
            // ★ 하한이 둘이므로 <b>어느 통이 무엇과 비교됐는지</b>까지 적는다. 예전처럼 최소값 하나와
            //   하한 하나만 찍으면, 정상적으로 1.18pt인 채움 경계선이 "2pt 미달"로 읽힌다.
            int standalone = r.LineCount - r.FillOutlineCount;
            // ★ 막이 걸려 있으면 <b>무엇을 되뺐는지</b>를 같은 줄에 적는다. 안 적으면 읽는 사람이
            //   "그려진 두께"와 "잉크 코어"를 구분할 방법이 없고, 그 혼동이 이 게이트가 고친 사고다.
            string membrane = r.MembraneLineCount > 0
                ? $" · 막 {r.MembranePixels:F2}물리픽셀 × {r.MembraneLineCount}개(그려진 최소 낱선 " +
                  $"{r.MinLineDrawnPoints:F2}pt · 채움경계선 {r.MinFillOutlineDrawnPoints:F2}pt)"
                : string.Empty;
            return $"LineRenderer {r.LineCount}개 획 두께 실측 {r.MinPixels:F2}~{r.MaxPixels:F2} 물리픽셀 " +
                   $"(= {r.MinPoints:F2}~{r.MaxPoints:F2} OS pt / 낱선 {standalone}개 <b>잉크</b> 최소 " +
                   $"{r.MinLineInkCorePoints:F2}pt vs 하한 {r.FloorPoints:F1}pt · 채움경계선 {r.FillOutlineCount}개 " +
                   $"<b>잉크</b> 최소 {r.MinFillOutlineInkCorePoints:F2}pt vs 하한 " +
                   $"{r.FillOutlineFloorPoints:F1}pt{membrane} -> {verdict})";
        }
    }
}
