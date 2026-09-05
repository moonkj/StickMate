using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StickMate.EditorTools
{
    /// <summary>
    /// ★ 앱 아이콘 검증 게이트 — <c>design/character/APP_ICON_SPEC.md</c> §8의 구현체.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 이 저장소의 규칙 셋
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>계산기를 만들면 알려진 값으로 먼저 교정한다.</b> 교정이 깨지면 그 뒤 숫자를 전부 폐기한다.
    ///     ⇒ <see cref="CalibrateMeasurement"/>(C0)가 <b>합성 도형</b>(반지름을 아는 원 · 폭과 각도를
    ///     아는 띠)을 같은 측정 경로에 넣어 되찾아지는지 먼저 본다. 여기서 깨지면 G1~G4를 아예 안 잰다.</item>
    ///   <item><b>모든 "없음/통과" 판정에 양성 대조를 붙인다.</b>
    ///     ⇒ G0이 <b>일부러 틀린 타일</b>을 G3에 넣어 <b>반드시 실패</b>하는지 본다.
    ///     G0이 통과해 버리면 G3는 죽은 게이트이고 그 라운드의 초록은 전부 무효다.</item>
    ///   <item><b>기대값을 측정 대상과 같은 함수로 만들지 않는다.</b>
    ///     ⇒ G1/G2의 기대값은 <b>프리팹 <c>widthCurve</c>·관절 좌표</b>에서 오고(<see cref="FigureGeometry"/>),
    ///     측정값은 <b>래스터</b>에서 온다. 둘은 카메라·렌더·다운샘플 체인을 공유하지 않으므로
    ///     그 체인이 기하를 왜곡하면 <b>갈라진다</b>. 그것이 G1/G2의 존재 이유다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 판정식 — 사양 §12(2026-09-05 2차 정정)를 반영한 <c>Σα</c> 계열 하나로 통일했다
    /// ============================================================================
    /// 사양 §8 G3의 <b>원래 문언</b>은 <i>"아래팔 중앙 가로 단면의 α ≥ 0.5 화소 폭 ≥ 1.0px"</i>이었고
    /// 그대로 구현하면 두 가지가 동시에 깨진다.
    /// <list type="number">
    ///   <item><b>정수 양자화</b> — "α ≥ 0.5인 화소의 개수"는 정수라 1.02와 0.92를 구분할 수 없다.</item>
    ///   <item><b>기울기</b> — 진짜 폭 W인 띠를 <b>가로선</b>이 자르면 현은 <c>W / cos θ</c>로 늘어난다.
    ///     그래서 <b>24px 전신의 팔(진짜 0.92px)</b>도 가로로는 1.4px대가 되어 "≥ 1.0px"를 합격한다.
    ///     ⇒ 사양 §8 G0이 "반드시 실패해야 한다"고 못박은 그 타일이 <b>통과해 버린다.</b></item>
    /// </list>
    /// 사양 §12-2가 이 정정을 채택했고(<c>Σα × |v_y|</c>), §12-3이 §9의 EDT 자를 <b>폐기</b>했다.
    /// 이 파일은 그 결과를 다음 세 추정량으로 구현한다 — <b>전부 같은 원리(Σα = 면적)</b>다.
    /// <list type="bullet">
    ///   <item><b>E1</b>(획 수직폭) = <c>median_over_rows(Σα(row)) × |v_y|</c>. G1·G3·G0이 쓴다.</item>
    ///   <item><b>E2b</b>(머리 잉크 지름) = 머리 상단 60% 구간에서 <c>c(y)² = 4R² − 4(y−y₀)²</c>를
    ///     <b>선형 최소제곱</b>으로 풀어 얻는 지름. G1의 분모, G2의 분모. (사양 §12-3)</item>
    ///   <item><b>잉크 높이</b> = 행별 <c>max α</c> 프로파일의 <b>0.5 교차점 선형보간</b>. G2의 분자.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 사양 §12-2(A)의 "α가중 PCA"에서 한 번 더 갈라진 지점 — <b>회귀</b>로 바꿨다
    /// ============================================================================
    /// §12-2(A)의 지시는 옳다: <i>"|dir.y|를 40°로 하드코딩하지 마라. 이미지에서 뽑아라."</i>
    /// (실제로 아래팔은 팔 벌림 40°에 팔꿈치가 더해져 <b>약 50°</b>다 — 40°를 적으면 19% 틀린다.)
    /// 그런데 <b>주축 PCA는 우리 측정창에서 편향된다.</b> 측정창은 <b>가로줄</b>로 잘라 모으므로
    /// 표본 영역이 <i>수평으로 잘린 평행사변형</i>이 되고, 그 2차 모멘트에는 띠의 <b>폭</b>이
    /// <c>Var(x) += b²/3</c>로 섞여 주축이 수평 쪽으로 끌린다. 오프라인 실측(8× 슈퍼샘플, 양자화 포함):
    /// <code>
    ///   폭 9.86px · 50° · 길이 60px  →  PCA |v_y| = 0.5787  (참값 cos50° = 0.6428, 오차 −0.064)
    ///   폭 1.02px · 50° · 길이 4px   →  PCA |v_y| = 0.4155  (오차 −0.227 — E1이 35% 작아진다)
    /// </code>
    /// ⇒ 대신 <b>행 중심 회귀</b>를 쓴다. 한 행의 런 중심(α가중)은 <b>정확히 중심선 위</b>에 있으므로
    /// (캡슐의 가로 현은 중심선에 대해 대칭이다), <c>x = a + t·y</c>를 α가중 최소제곱으로 풀면
    /// <c>t</c>가 곧 기울기고 <c>|v_y| = 1/√(1+t²)</c>다. 같은 합성 도형에서:
    /// <code>
    ///   위 두 경우 회귀 |v_y| = 0.6428 (오차 0.0000) / 0.6477 (오차 +0.005)
    ///   전 8케이스 최대 오차 +0.023 (행 2개짜리 최악), 대부분 ≤0.008
    /// </code>
    /// <b>이것도 "이미지에서 뽑은 방향"이고 하드코딩은 0개다</b> — 지시의 목적(자세 상수가 바뀌면
    /// 게이트가 조용히 틀려지는 것을 막는다)을 그대로 만족한다. 문언 그대로의 PCA 값도 <b>함께
    /// 보고</b>해 이 갈라짐이 숨지 않게 한다(C0 상세). ★ 이 항목은 <c>design-character</c>에게
    /// 되돌려야 한다(사양 §12-2(A) 문언 정정).
    ///
    /// <para>축은 <b>포즈의 성질이지 타일의 성질이 아니다.</b> 그래서 256 타일에서 <b>한 번</b> 재고
    /// (아래팔 기준 13행 → 오차 ≤0.008) 전 타일에 같은 값을 쓴다. 16px 타일에서는 아래팔이 2행뿐이라
    /// 거기서 축을 다시 재면 오히려 나빠진다.</para>
    /// </summary>
    public static class StickmanIconGates
    {
        // ====================================================================
        // 사양 §8 의 기대값 · 허용오차
        // ====================================================================

        /// <summary>사양 §1-2 / §8 G1: 평균 획 ÷ 머리 잉크 지름 = 22.29%.</summary>
        public const float DocumentedMeanStrokeRatio = 0.2229f;

        /// <summary>사양 §8 G1 허용오차 ±0.3%p.</summary>
        public const float StrokeRatioTolerance = 0.003f;

        /// <summary>사양 §1-2 / §8 G2: 머리 개수 4.485.</summary>
        public const float DocumentedHeadCount = 4.485f;

        /// <summary>사양 §8 G2 허용오차 ±0.02.</summary>
        public const float HeadCountTolerance = 0.02f;

        /// <summary>사양 §8 G3: 사양의 유일한 경성 하한.</summary>
        public const float MinArmStrokePixels = 1.0f;

        /// <summary>
        /// 사양 §12-2(B) G3b: <b>잉크가 있는 모든 가로줄의 <c>max α</c>의 최솟값</b> 하한.
        /// <para>G3(수직폭)은 <b>기하</b> 판정이고 이건 <b>렌더</b> 판정이다 — "회색 실오라기로 보이는가".
        /// 사양 §3-2 표의 마지막 열이 같은 지표이고, 기각된 후보 B(16px 굵힘)가 <b>0.45</b>로 바닥이었다.
        /// 즉 이 하한은 "B가 간신히 걸리는 선"이다.</para>
        /// </summary>
        public const float MinRowMaxAlpha = 0.45f;

        /// <summary>
        /// "잉크가 있는 가로줄"의 정의 — 그 줄의 <c>max α</c>가 이 값을 넘으면 잉크가 있다고 본다.
        /// 사양 §3-2의 회색죽 정의가 쓴 <b>α &gt; 0.02</b>와 같은 문턱이다.
        /// </summary>
        public const float InkRowAlphaEpsilon = 0.02f;

        /// <summary>
        /// 이미지에서 뽑은 축과 <b>프리팹 기하</b> 축의 허용 차(<c>|v_y|</c> 기준). 사양 §12-2(A)의
        /// 검산 폭(±0.05)과 같다. 벗어나면 <b>자세가 바뀐 것</b>이고 그것도 알아야 할 정보다 —
        /// 그래서 조용히 넘기지 않고 측정을 <b>무효</b>로 낸다.
        /// </summary>
        public const float AxisAgreementTolerance = 0.05f;

        /// <summary>행 중심 회귀에 필요한 최소 행 수. 미만이면 프리팹 기하 축으로 되돌아간다.</summary>
        public const int MinAxisRows = 3;

        /// <summary>사양 §12-3 E2b: 머리 <b>상단 60%</b> 구간만 쓴다(아래쪽은 목·어깨가 현을 넓힌다).</summary>
        public const float HeadBandTopFraction = 0.60f;

        /// <summary>E2b 최소제곱에 필요한 최소 행 수(미지수 3개 + 1).</summary>
        public const int MinHeadFitRows = 4;

        /// <summary>사양 §8 G4: 왼팔·몸통·오른팔 = 3덩어리.</summary>
        public const int ExpectedInkBlobs = 3;

        /// <summary>사양 §8 G4가 도는 타일.</summary>
        public static readonly int[] BlobScanTiles = { 16, 24 };

        /// <summary>
        /// G4의 스캔 높이 — 어깨에서 티어 S 크롭 하단(고관절 캡 아래 끝)까지의 60% 지점.
        /// <para>★ 사양 원문은 <i>"어깨 아래 60% 지점"</i>이라고만 적혀 있어 <b>기준선이 명시돼 있지 않다.</b>
        /// 여기서는 "어깨 → 반신 크롭 하단"을 구간으로 잡았다. 이건 <b>해석</b>이고, 값을 바꾸고 싶으면
        /// 이 상수 하나만 고치면 된다. 해석을 숨기지 않기 위해 보고서에 실제 스캔 y를 함께 찍는다.</para>
        /// </summary>
        public const float BlobScanFractionFromShoulder = 0.60f;

        /// <summary>α &gt; 이 값이면 잉크가 있다고 본다(런 경계 판정).</summary>
        public const float AlphaEpsilon = 1f / 255f;

        /// <summary>사양 문언의 임계 — "α ≥ 0.5".</summary>
        public const float AlphaHalf = 0.5f;

        /// <summary>
        /// 런 오염 판정 배수. 측정한 가로 단면이 기대 가로 단면의 이 배수를 넘으면
        /// <b>다른 마디와 붙어 버린 것</b>이므로 그 측정을 "무효"로 낸다.
        /// <para>★ 이게 없으면 G0이 조용히 통과한다 — 팔이 몸통과 붙어 런이 커지면
        /// "폭이 충분하다"로 읽히기 때문이다. <b>붙은 것은 통과가 아니라 측정 불능이다.</b></para>
        /// </summary>
        public const float RunContaminationFactor = 2.5f;

        // ====================================================================
        // 결과 자료구조
        // ====================================================================

        public enum GateStatus
        {
            /// <summary>잤고 통과했다.</summary>
            Pass,

            /// <summary>쟀고 실패했다.</summary>
            Fail,

            /// <summary>★ 못 쟀다. <b>통과가 아니다.</b> 측정 경로가 오염됐거나 입력이 없다.</summary>
            Invalid,

            /// <summary>★ 이 러너로는 구조적으로 못 재는 것(실기 캡처 · exe 리소스). <b>미확인</b>이다.</summary>
            Manual,
        }

        public sealed class GateResult
        {
            public string Id;
            public string Title;
            public GateStatus Status;
            public string Detail;

            public GateResult(string id, string title, GateStatus status, string detail)
            {
                Id = id; Title = title; Status = status; Detail = detail;
            }

            public string Mark => Status switch
            {
                GateStatus.Pass => "PASS",
                GateStatus.Fail => "FAIL",
                GateStatus.Invalid => "무효",
                _ => "미확인",
            };
        }

        public sealed class Report
        {
            public readonly List<GateResult> Results = new List<GateResult>();
            public readonly List<string> Notes = new List<string>();

            public void Add(string id, string title, GateStatus status, string detail) =>
                Results.Add(new GateResult(id, title, status, detail));

            /// <summary>
            /// ★ <b>자동으로 잰 게이트</b>만 본다. <see cref="GateStatus.Manual"/>은 세지 않는다 —
            /// 그것들은 통과한 것이 아니라 <b>아직 아무도 안 본 것</b>이다.
            /// 그래서 이 값이 true여도 "아이콘이 됐다"고 쓰면 안 된다.
            /// </summary>
            public bool AllAutomaticGatesPassed
            {
                get
                {
                    foreach (GateResult r in Results)
                    {
                        if (r.Status == GateStatus.Fail || r.Status == GateStatus.Invalid) return false;
                    }
                    return true;
                }
            }

            public string OneLineSummary()
            {
                int pass = 0, fail = 0, invalid = 0, manual = 0;
                foreach (GateResult r in Results)
                {
                    switch (r.Status)
                    {
                        case GateStatus.Pass: pass++; break;
                        case GateStatus.Fail: fail++; break;
                        case GateStatus.Invalid: invalid++; break;
                        default: manual++; break;
                    }
                }
                return $"자동 게이트 통과 {pass} / 실패 {fail} / 무효 {invalid}, 미확인(실기 필요) {manual}";
            }

            public string Render(StickmanIconBaker.Settings settings, StickmanIconBaker.BakeOutput output)
            {
                var sb = new StringBuilder();
                var inv = CultureInfo.InvariantCulture;
                sb.AppendLine("StickMate 앱 아이콘 굽기 보고서");
                sb.AppendLine("사양: design/character/APP_ICON_SPEC.md  (§4 확정 사양 / §6 렌더링 / §8 검증 게이트)");
                sb.AppendLine("생성: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv));
                sb.AppendLine();

                sb.AppendLine("── 파라미터 ────────────────────────────────────────────");
                sb.AppendLine($"  티어 경계          : {settings.TierBoundaryPx} px  (미만 = 반신 / 이상 = 전신)");
                sb.AppendLine("                       ★ 사양 §3-5 미확정 1건 — 40 vs 48은 실기 A/B(G7)로만 갈린다");
                sb.AppendLine($"  모서리 반경 계수   : {settings.CornerRadiusFactor.ToString("0.####", inv)}");
                sb.AppendLine($"  플레이트 테두리    : {settings.BorderWidthPx.ToString("0.##", inv)} px");
                sb.AppendLine("                       ★ 사양 §4-5 미확정 2건 — design-art 판정 대기");
                sb.AppendLine($"  프레임 채움 f      : {settings.FrameFillOverride.ToString("0.####", inv)}");
                sb.AppendLine($"  플레이트 색        : {ColorUtility.ToHtmlStringRGB(settings.PlateColor)} " +
                              "(UiChrome.PortraitSurface 토큰을 읽는다 — 신규 hex 0개)");
                sb.AppendLine($"  잉크색 덮어쓰기    : " +
                              (settings.InkColorOverride.HasValue
                                  ? "#" + ColorUtility.ToHtmlStringRGB(settings.InkColorOverride.Value)
                                  : "없음(프리팹에 구워진 색 = 출하 기본 검정)"));
                sb.AppendLine($"  잉크 사각형 출처   : " +
                              (settings.UseDocumentedInkRect ? "★ 사양 문서 값(실측 아님)" : "프리팹 실측"));
                sb.AppendLine();

                if (output.Geometry != null)
                {
                    FigureGeometry g = output.Geometry;
                    sb.AppendLine("── 프리팹 실측 기하 (루트 로컬 · 월드 유닛) ────────────");
                    sb.AppendLine($"  잉크 사각형        : x [{g.InkRect.xMin.ToString("0.000000", inv)}, {g.InkRect.xMax.ToString("0.000000", inv)}]  " +
                                  $"y [{g.InkRect.yMin.ToString("0.000000", inv)}, {g.InkRect.yMax.ToString("0.000000", inv)}]");
                    sb.AppendLine($"                       폭 {g.InkRect.width.ToString("0.000000", inv)} × 높이 {g.InkRect.height.ToString("0.000000", inv)}");
                    sb.AppendLine($"  고관절 y / 어깨 y  : {g.HipY.ToString("0.000000", inv)} / {g.ShoulderY.ToString("0.000000", inv)}");
                    sb.AppendLine($"  티어 S 크롭 하단   : {g.TierSmallBottomY.ToString("0.000000", inv)}  (= hipY − W_다리/2, 사양 §4-2)");
                    sb.AppendLine($"  머리 잉크 지름     : {g.HeadInkDiameter.ToString("0.000000", inv)}  (2R + W_링 — 2R로 나누면 오판한다)");
                    sb.AppendLine($"  획 팔/몸통/다리    : {g.ArmStrokeWidth.ToString("0.000000", inv)} / " +
                                  $"{g.TorsoStrokeWidth.ToString("0.000000", inv)} / {g.LegStrokeWidth.ToString("0.000000", inv)}");
                    sb.AppendLine($"  기대 평균 획 비율  : {(g.ExpectedMeanStrokeRatio * 100f).ToString("0.000", inv)} %   " +
                                  $"(사양 §1-2 = {(DocumentedMeanStrokeRatio * 100f).ToString("0.00", inv)} %)");
                    sb.AppendLine($"  기대 머리 개수     : {g.ExpectedHeadCount.ToString("0.0000", inv)} (잉크 높이 ÷ D — G2가 쓰는 값)  /  " +
                                  $"{g.ExpectedHeadCountAboveGround.ToString("0.0000", inv)} (지면~꼭대기 ÷ D — 사양 §1-2 = " +
                                  $"{DocumentedHeadCount.ToString("0.000", inv)})");
                    sb.AppendLine();
                }

                sb.AppendLine("── 타일 ────────────────────────────────────────────────");
                sb.AppendLine("   px   티어   orthoSize   중심(x, y)");
                foreach (StickmanIconBaker.Tile t in output.TileList)
                {
                    sb.AppendLine($"  {t.Size,4}   {(t.TierSmall ? "S 반신" : "L 전신")}   " +
                                  $"{t.OrthoSize.ToString("0.000000", inv),10}   " +
                                  $"({t.CameraCenter.x.ToString("0.000000", inv)}, {t.CameraCenter.y.ToString("0.000000", inv)})");
                }
                sb.AppendLine();

                sb.AppendLine("── 게이트 ──────────────────────────────────────────────");
                foreach (GateResult r in Results)
                {
                    sb.AppendLine($"  [{r.Mark,-4}] {r.Id} {r.Title}");
                    if (!string.IsNullOrEmpty(r.Detail))
                    {
                        foreach (string line in r.Detail.Split('\n')) sb.AppendLine("          " + line);
                    }
                }
                sb.AppendLine();
                sb.AppendLine("  " + OneLineSummary());
                sb.AppendLine();
                sb.AppendLine("  ★★ '미확인'은 통과가 아니다. G5(실기 5장)·G6(exe 그룹 아이콘 리소스 열거)를");
                sb.AppendLine("     끝내기 전에는 이 아이콘을 '됐다'고 쓰지 마라 — 사양 §8이 그것을 최종 판정으로 못박았다.");

                if (output.Log.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("── 굽기 로그 ───────────────────────────────────────────");
                    foreach (string l in output.Log) sb.AppendLine("  " + l);
                }
                if (Notes.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("── 메모 ────────────────────────────────────────────────");
                    foreach (string n in Notes) sb.AppendLine("  " + n);
                }
                return sb.ToString();
            }
        }

        // ====================================================================
        // C1 — 기하 교정 (실측 ↔ 사양 §4-2/§4-4)
        // ====================================================================

        /// <summary>
        /// 프리팹 실측 기하가 사양 §4-2/§4-4와 같은가. <c>null</c>이면 통과.
        /// <para>이게 깨졌다는 것은 둘 중 하나다 — <b>프리팹이 바뀌었거나 사양이 낡았다.</b>
        /// 어느 쪽이든 그 상태로 구운 아이콘의 숫자는 근거가 없다.</para>
        /// </summary>
        public static string CalibrateGeometry(FigureGeometry g, float toleranceUnits)
        {
            var problems = new List<string>();
            var inv = CultureInfo.InvariantCulture;

            void Check(string label, float measured, float documented, float tol)
            {
                if (Mathf.Abs(measured - documented) > tol)
                {
                    problems.Add($"{label}: 실측 {measured.ToString("0.000000", inv)} vs 사양 {documented.ToString("0.000000", inv)} " +
                                 $"(차 {(measured - documented).ToString("+0.000000;-0.000000", inv)}, 허용 ±{tol.ToString("0.000000", inv)})");
                }
            }

            Rect doc = StickmanIconBaker.DocumentedInkRect;
            Check("잉크 사각형 xMin", g.InkRect.xMin, doc.xMin, toleranceUnits);
            Check("잉크 사각형 xMax", g.InkRect.xMax, doc.xMax, toleranceUnits);
            Check("잉크 사각형 yMin", g.InkRect.yMin, doc.yMin, toleranceUnits);
            Check("잉크 사각형 yMax", g.InkRect.yMax, doc.yMax, toleranceUnits);
            Check("티어 S 크롭 하단", g.TierSmallBottomY, StickmanIconBaker.DocumentedTierSmallBottomY, toleranceUnits);

            // 카메라 값도 함께 본다 — 사양 §4-4는 잉크 사각형에서 유도된 값이므로, 위가 맞으면
            // 여기도 맞아야 한다. 안 맞으면 유도식이 잘못 옮겨진 것이다(그것이 이 두 줄의 목적이다).
            StickmanIconBaker.ResolveFraming(g.InkRect, g.TierSmallBottomY, false,
                StickmanIconBaker.FrameFill, out Vector2 cL, out float sL);
            StickmanIconBaker.ResolveFraming(g.InkRect, g.TierSmallBottomY, true,
                StickmanIconBaker.FrameFill, out Vector2 cS, out float sS);
            Check("티어 L orthoSize", sL, StickmanIconBaker.DocumentedOrthoSizeLarge, toleranceUnits);
            Check("티어 S orthoSize", sS, StickmanIconBaker.DocumentedOrthoSizeSmall, toleranceUnits);
            Check("중심 x", cL.x, StickmanIconBaker.DocumentedCenterX, toleranceUnits);
            Check("티어 L 중심 y", cL.y, StickmanIconBaker.DocumentedCenterYLarge, toleranceUnits);
            Check("티어 S 중심 y", cS.y, StickmanIconBaker.DocumentedCenterYSmall, toleranceUnits);

            // 비율 두 개 — 여기가 갈라지면 사양 §1-2의 표 전체가 낡은 것이다.
            Check("평균 획 ÷ 머리 지름", g.ExpectedMeanStrokeRatio, DocumentedMeanStrokeRatio, StrokeRatioTolerance);

            // ★ 머리 개수는 사양 안에 <정의가 두 개> 있다 — 문서가 실제로 계산한 쪽과 대조한다.
            //   §1-2의 4.4847 = 지면(y=0)~잉크 꼭대기 ÷ D.  §8 G2 문언의 "잉크 높이 ÷ D" = 4.6063.
            //   차 0.1216 = 지면 아래로 내려간 발 캡의 절반(|yMin|/D). FigureGeometry 주석 참고.
            Check("머리 개수(지면~꼭대기 ÷ D — 사양 §1-2가 계산한 값)",
                g.ExpectedHeadCountAboveGround, DocumentedHeadCount, HeadCountTolerance);

            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        // ====================================================================
        // C2 — 프러스텀 오염 (사양 §6-2 3단계)
        // ====================================================================

        /// <summary>
        /// 촬영 프러스텀 안에 <b>우리 인스턴스가 아닌</b> 렌더러가 있으면 그 경로를 돌려준다.
        /// <c>null</c>이면 깨끗하다.
        /// <para>사양 §6-2는 "20000 좌표에는 우리 인스턴스밖에 없다"고 <b>단언</b>했는데,
        /// 단언은 검사가 아니다. 다른 라운드가 촬영장 근처에 무언가를 놓는 순간 아이콘에 찍힌다.</para>
        /// </summary>
        public static string FindFrustumIntruder(Camera cam, GameObject root)
        {
            if (cam == null || root == null) return "카메라 또는 촬영장 루트가 없다";
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Renderer r in all)
            {
                if (r == null || !r.enabled) continue;
                if (r.transform == root.transform || r.transform.IsChildOf(root.transform)) continue;
                if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) return HierarchyPath(r.transform);
            }
            return null;
        }

        private static string HierarchyPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            Transform p = t.parent;
            while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        // ====================================================================
        // 게이트 실행
        // ====================================================================

        /// <summary>헤드리스 실행 — 자동 게이트가 전부 통과.</summary>
        public const int HeadlessPassExitCode = 0;

        /// <summary>헤드리스 실행 — 자동 게이트에 실패 또는 무효가 있다.</summary>
        public const int HeadlessFailExitCode = 1;

        /// <summary>헤드리스 실행 — 게이트를 <b>재지도 못했다</b>(예외). 실패와 구분한다.</summary>
        public const int HeadlessErrorExitCode = 2;

        /// <summary>
        /// ★ <b>배치모드 진입점</b> — code-inspection 발견 N6.
        ///
        /// <para><b>왜 필요한가</b>: 이 파일은 <c>Assets/Editor/</c>에 있어 사전정의 어셈블리
        /// <c>Assembly-CSharp-Editor</c>로 컴파일된다. asmdef 기반인 <c>StickMate.Tests.EditMode</c>는
        /// 사전정의 어셈블리를 <b>참조 목록에 적을 수 없다</b> — 즉 EditMode 러너가 이 1,800줄짜리
        /// 게이트를 <b>구조적으로</b> 부를 수 없다. 게이트가 있는데 아무도 안 부르는 상태다.</para>
        ///
        /// <para>파일을 <c>Assets/_Project/Scripts</c> 아래로 옮기는 안은 채택하지 않았다 —
        /// 이 코드는 <c>UnityEditor</c>·<c>StickmanIconBaker</c>(둘 다 에디터 전용)에 의존하고,
        /// 옮기면 런타임 어셈블리가 에디터 전용 코드를 물게 된다. 대신 <b>부를 수 있는 문</b>을 낸다.</para>
        ///
        /// <para>판정은 <b>종료코드가 아니라 산출물</b>로 한다(클래스 문서 "-nographics를 쓰지 마라" 참고).
        /// 이 반환값은 그 산출물 판정을 <b>요약</b>할 뿐이다.</para>
        /// </summary>
        /// <returns><see cref="HeadlessPassExitCode"/> / <see cref="HeadlessFailExitCode"/> /
        /// <see cref="HeadlessErrorExitCode"/>.</returns>
        public static int RunGatesHeadless()
        {
            try
            {
                bool ok = StickmanIconBaker.BakeWindows(new StickmanIconBaker.Settings());
                Debug.Log($"[아이콘게이트] 헤드리스 실행 종료 — 코드 " +
                    (ok ? HeadlessPassExitCode : HeadlessFailExitCode) +
                    ". ★ 통과여도 G5(실기 캡처)/G6(exe 리소스)는 미확인이다 — 보고서를 읽어라.");
                return ok ? HeadlessPassExitCode : HeadlessFailExitCode;
            }
            catch (Exception e)
            {
                Debug.LogError("[아이콘게이트] 헤드리스 실행이 예외로 끝났다 — 게이트를 재지 못했다. " + e);
                return HeadlessErrorExitCode;
            }
        }

        /// <summary><c>-executeMethod</c> 대상. Unity는 진입 메서드의 반환값을 버리므로
        /// 여기서 프로세스 종료코드로 옮긴다.
        /// <c>Unity -batchmode -projectPath &lt;repo&gt;
        /// -executeMethod StickMate.EditorTools.StickmanIconGates.RunGatesHeadlessBatch -logFile &lt;path&gt;</c>
        /// (<c>-nographics</c> 금지 · <c>-quit</c>은 <see cref="EditorApplication.Exit"/>가 대신한다.)</summary>
        public static void RunGatesHeadlessBatch() => EditorApplication.Exit(RunGatesHeadless());

        public static Report RunAll(StickmanIconBaker.BakeOutput output)
        {
            var report = new Report();
            var inv = CultureInfo.InvariantCulture;

            // ---- C0 측정기 교정 --------------------------------------------------------
            string c0 = CalibrateMeasurement(out string c0Detail);
            report.Add("C0", "측정기 교정 — 반지름/폭/기울기를 아는 합성 도형이 되찾아지는가",
                c0 == null ? GateStatus.Pass : GateStatus.Fail,
                c0 ?? "합성 원(지름 48.00px)과 합성 띠(폭 10.00px @ 40°)를 <출하되는 그 함수들로> 되찾았다.\n" +
                      c0Detail + "\n이게 깨지면 G0~G4의 숫자는 전부 폐기 대상이다.");
            if (c0 != null)
            {
                report.Add("G0~G4", "이후 게이트", GateStatus.Invalid, "C0(측정기 교정)이 깨져 재지 않았다.");
                AddManualGates(report, output);
                return report;
            }

            StickmanIconBaker.Tile big = output.TileList.Find(t => t.Size == StickmanIconBaker.PngEmbedTile);
            if (big == null)
            {
                report.Add("C3", "렌더 생존", GateStatus.Invalid, "256 타일이 없다.");
                AddManualGates(report, output);
                return report;
            }

            // ---- C3 렌더 생존 (-nographics / 빈 렌더 잡기) -------------------------------
            var sampler = InkSampler.Build(big, output.Config.PlateColor, out string c3);
            report.Add("C3", "렌더 생존 — 플레이트 색이 맞고 잉크가 실제로 찍혔는가",
                c3 == null ? GateStatus.Pass : GateStatus.Fail,
                c3 ?? $"플레이트 #{ColorUtility.ToHtmlStringRGB(output.Config.PlateColor)} 확인, " +
                      $"잉크 기준 휘도 {sampler.InkLuma.ToString("0.000", inv)} / 플레이트 휘도 {sampler.PlateLuma.ToString("0.000", inv)}.\n" +
                      "★ -nographics 로 돌리면 여기서 죽는다(빈 렌더도 PNG는 나온다).");
            if (c3 != null)
            {
                report.Add("G0~G4", "이후 게이트", GateStatus.Invalid, "C3(렌더 생존)이 깨져 재지 않았다.");
                AddManualGates(report, output);
                return report;
            }

            FigureGeometry g = output.Geometry;

            // ---- 축 — 포즈의 성질이므로 256에서 한 번만 잰다 (사양 §12-2(A)) --------------
            List<AxisEstimate> forearmAxes = MeasureAxes(big, g.Forearms, sampler);
            List<AxisEstimate> shinAxes = MeasureAxes(big, g.Shins, sampler);
            AxisEstimate torsoAxis = MeasureAxis(big, g.Torso, sampler);
            {
                var sb = new StringBuilder();
                bool axisOk = true, anyImage = false;
                foreach (AxisEstimate ax in forearmAxes) { sb.AppendLine(ax.Describe()); if (ax.Disagrees) axisOk = false; if (ax.FromImage) anyImage = true; }
                foreach (AxisEstimate ax in shinAxes) { sb.AppendLine(ax.Describe()); if (ax.Disagrees) axisOk = false; if (ax.FromImage) anyImage = true; }
                sb.AppendLine(torsoAxis.Describe());
                if (torsoAxis.Disagrees) axisOk = false;
                if (torsoAxis.FromImage) anyImage = true;
                sb.Append("★ 각도를 코드에 적지 않는다 — 이미지에서 뽑고 프리팹 기하와 대조만 한다(사양 §12-2(A)).\n" +
                          "  이미지 추정은 '행 중심 회귀'다. 문언의 PCA는 가로줄로 자른 측정창에서 편향된다(클래스 문서에 실측표).");
                report.Add("C4", "마디 기울기 — 이미지에서 뽑은 축이 프리팹 자세와 일치하는가",
                    !anyImage ? GateStatus.Invalid : (axisOk ? GateStatus.Pass : GateStatus.Fail), sb.ToString());
            }

            // ---- G0 양성 대조 (사양이 "먼저 돌린다"고 못박았다) --------------------------
            // ★ 판정식을 바꾼 라운드에는 G0를 새 식으로 다시 돌려야 한다(사양 §12-2 "G0 재적용 의무").
            //   대조 타일은 256 전신을 24px로 면적 축소한 것이고, 축은 같은 포즈이므로 256의 것을 쓴다.
            if (output.PositiveControl24 == null)
            {
                report.Add("G0", "양성 대조 — 일부러 틀린 타일이 G3에서 반드시 실패하는가",
                    GateStatus.Invalid, "양성 대조 타일을 만들지 않았다.");
                report.Add("G0b", "양성 대조 — 일부러 틀린 타일이 G3b에서 반드시 실패하는가",
                    GateStatus.Invalid, "양성 대조 타일을 만들지 않았다.");
                report.Add("G3", "각 타일 아래팔 획 ≥ 1.0px", GateStatus.Invalid,
                    "G0 없이 G3를 초록으로 읽으면 안 된다 — 죽은 게이트와 구분이 안 된다.");
            }
            else
            {
                StrokeMeasurement ctrl = MeasureWorst(output.PositiveControl24, g.Forearms, forearmAxes, sampler);
                if (ctrl.Contaminated)
                {
                    report.Add("G0", "양성 대조 — 일부러 틀린 타일이 G3에서 반드시 실패하는가",
                        GateStatus.Invalid,
                        $"대조 타일의 아래팔 측정이 무효다({ctrl.Reason}; 가로 단면 " +
                        ctrl.Run.ToString("0.00", inv) + "px, 기대 " + ctrl.ExpectedRun.ToString("0.00", inv) + "px).\n" +
                        "★ '붙었다'는 통과가 아니다. G3의 초록을 신뢰할 근거가 사라졌다.");
                }
                else if (ctrl.Perpendicular < MinArmStrokePixels)
                {
                    report.Add("G0", "양성 대조 — 일부러 틀린 타일이 G3에서 반드시 실패하는가",
                        GateStatus.Pass,
                        $"256 전신을 24px로 면적 축소한 타일: E1 수직 환산 팔 획 {ctrl.Perpendicular.ToString("0.000", inv)} px " +
                        $"< 하한 {MinArmStrokePixels.ToString("0.0", inv)} px ⇒ 예상대로 실패했다(사양 §4-6 예측 0.92).\n" +
                        $"문언 그대로의 'α≥0.5 화소 개수' = {ctrl.HalfAlphaPixels}개 · 가로 단면 {ctrl.Run.ToString("0.000", inv)} px " +
                        $"· 행 {ctrl.RowCount}개 · 프리팹 축으로 재도 {ctrl.GeometricPerpendicular.ToString("0.000", inv)} px\n" +
                        "★ 가로 단면만 봤다면 " + ctrl.Run.ToString("0.00", inv) + "px ≥ 1.0 이라 이 대조가 " +
                        "통과해 버렸을 것이다 — 클래스 문서의 '판정식' 절 참고.");
                }
                else
                {
                    report.Add("G0", "양성 대조 — 일부러 틀린 타일이 G3에서 반드시 실패하는가",
                        GateStatus.Fail,
                        $"틀린 타일이 통과했다(E1 수직 환산 {ctrl.Perpendicular.ToString("0.000", inv)} px ≥ 하한). " +
                        "⇒ G3는 죽은 게이트다. 이 굽기의 초록은 전부 무효다.");
                }

                RowAlphaFloor ctrlFloor = MeasureRowAlphaFloor(output.PositiveControl24, sampler);
                if (!ctrlFloor.Valid)
                {
                    report.Add("G0b", "양성 대조 — 일부러 틀린 타일이 G3b에서 반드시 실패하는가",
                        GateStatus.Invalid, "대조 타일에서 잉크 행을 못 찾았다.");
                }
                else
                {
                    bool ctrlFails = ctrlFloor.Min < MinRowMaxAlpha;
                    report.Add("G0b", "양성 대조 — 일부러 틀린 타일이 G3b에서 반드시 실패하는가",
                        ctrlFails ? GateStatus.Pass : GateStatus.Fail,
                        $"대조(256 전신 → 24px) 최소 max α = {ctrlFloor.Min.ToString("0.000", inv)} " +
                        $"(행 {ctrlFloor.MinRow}) vs 하한 {MinRowMaxAlpha.ToString("0.00", inv)}\n" +
                        $"경계 행 제외 최솟값 {ctrlFloor.InteriorMin.ToString("0.000", inv)} (행 {ctrlFloor.InteriorRow})\n" +
                        (ctrlFails
                            ? "⇒ 예상대로 실패했다(사양 §3-2 후보 A @24px 예측 0.29)."
                            : "⇒ ★ 틀린 타일이 G3b를 통과했다. G3b는 죽은 게이트다."));
                }
            }

            // ---- G1 획 ÷ 머리 지름 (256) -----------------------------------------------
            HeadMeasurement head = MeasureHead(big, g, sampler);
            {
                StrokeMeasurement arm = MeasureWorst(big, g.Forearms, forearmAxes, sampler);
                StrokeMeasurement torso = MeasureStroke(big, g.Torso, torsoAxis, sampler);
                StrokeMeasurement shin = MeasureWorst(big, g.Shins, shinAxes, sampler);

                if (!head.Valid || arm.Contaminated || torso.Contaminated || shin.Contaminated)
                {
                    report.Add("G1", "256 타일 평균 획 ÷ 머리 잉크 지름", GateStatus.Invalid,
                        "측정 불능 — " + (head.Valid ? "" : "머리: " + head.Reason + "; ") +
                        (arm.Contaminated ? "팔: " + arm.Reason + "; " : "") +
                        (torso.Contaminated ? "몸통: " + torso.Reason + "; " : "") +
                        (shin.Contaminated ? "다리: " + shin.Reason : ""));
                }
                else
                {
                    float mean = (arm.Perpendicular + torso.Perpendicular + shin.Perpendicular) / 3f;
                    float ratio = mean / head.Diameter;
                    float expected = g.ExpectedMeanStrokeRatio;      // ★ 프리팹에서 온 기대값(래스터와 독립)
                    bool ok = Mathf.Abs(ratio - expected) <= StrokeRatioTolerance;
                    report.Add("G1", "256 타일 평균 획 ÷ 머리 잉크 지름 (캡처가 기하를 왜곡했는가)",
                        ok ? GateStatus.Pass : GateStatus.Fail,
                        $"래스터 측정 {(ratio * 100f).ToString("0.000", inv)} % " +
                        $"vs 프리팹 기대 {(expected * 100f).ToString("0.000", inv)} % " +
                        $"(허용 ±{(StrokeRatioTolerance * 100f).ToString("0.0", inv)} %p)\n" +
                        $"E1 팔 {arm.Perpendicular.ToString("0.000", inv)} / 몸통 {torso.Perpendicular.ToString("0.000", inv)} / " +
                        $"다리 {shin.Perpendicular.ToString("0.000", inv)} px  (행 {arm.RowCount}/{torso.RowCount}/{shin.RowCount}개)\n" +
                        $"E2b 머리 잉크 지름 {head.Diameter.ToString("0.000", inv)} px " +
                        $"[2차 자 · 최대 현 {head.MaxChord.ToString("0.000", inv)} px, 차 {(head.MaxChord - head.Diameter).ToString("+0.000;-0.000", inv)}] " +
                        $"(적합 행 {head.Rows}개, 잔차 RMS {head.Residual.ToString("0.00", inv)} px²)\n" +
                        $"사양 §1-2 문서값 {(DocumentedMeanStrokeRatio * 100f).ToString("0.00", inv)} % (참고)\n" +
                        "★ 사양 §9의 EDT 자는 여기서 쓰지 않는다 — §12-3이 폐기했다(편향 −1.4~+0.8px, " +
                        "격자 위상·기울기에 따라 부호가 바뀌어 상수 보정이 불가능하고 G1 요구의 약 7배다).");
                }
            }

            // ---- T1 위상 확인 — EDT가 남는 용도(사양 §12-3 "남는 용도 3개" 중 2번) ---------
            if (!head.Valid)
            {
                report.Add("T1", "머리가 그림에서 유일한 큰 원반인가 (EDT 위상 — 거리값은 안 쓴다)",
                    GateStatus.Invalid, "머리를 못 재 위상 확인을 건너뛰었다 — " + head.Reason);
            }
            else
            {
                GateStatus t1Status = EdtHeadTopology(big, g, sampler, head, out string t1Detail);
                report.Add("T1", "머리가 그림에서 유일한 큰 원반인가 (EDT 위상 — 거리값은 안 쓴다)",
                    t1Status, t1Detail);
            }

            // ---- G2 머리 개수 (256) ------------------------------------------------------
            {
                InkHeightMeasurement ih = MeasureInkHeight(big, sampler);
                if (!head.Valid || !ih.Valid)
                {
                    report.Add("G2", "256 타일 머리 개수", GateStatus.Invalid,
                        "머리 또는 잉크 높이를 못 쟀다 — " + (head.Valid ? ih.Reason : head.Reason));
                }
                else
                {
                    float count = ih.Height / head.Diameter;
                    bool ok = Mathf.Abs(count - g.ExpectedHeadCount) <= HeadCountTolerance;
                    report.Add("G2", "256 타일 머리 개수 (잉크 높이 ÷ 머리 잉크 지름)",
                        ok ? GateStatus.Pass : GateStatus.Fail,
                        $"래스터 측정 {count.ToString("0.0000", inv)} vs 프리팹 기대 {g.ExpectedHeadCount.ToString("0.0000", inv)} " +
                        $"(허용 ±{HeadCountTolerance.ToString("0.00", inv)})\n" +
                        $"잉크 높이 {ih.Height.ToString("0.000", inv)} px (행별 max α 0.5 교차) " +
                        $"[2차 자 · 피복률 경계 {ih.CoverageHeight.ToString("0.000", inv)} px, " +
                        $"차 {(ih.CoverageHeight - ih.Height).ToString("+0.000;-0.000", inv)}]\n" +
                        $"E2b 머리 잉크 지름 {head.Diameter.ToString("0.000", inv)} px, " +
                        $"경계 행 {ih.BottomRow}~{ih.TopRow} (max α 하 {ih.BottomMaxAlpha.ToString("0.00", inv)} / " +
                        $"상 {ih.TopMaxAlpha.ToString("0.00", inv)})\n" +
                        $"★ 사양 문서값 {DocumentedHeadCount.ToString("0.000", inv)} 과 직접 비교하지 마라 — " +
                        $"그건 <지면(y=0)~꼭대기 ÷ D>이고 여기 기대값은 <잉크 높이 ÷ D>다. " +
                        $"차 {(g.ExpectedHeadCount - g.ExpectedHeadCountAboveGround).ToString("0.0000", inv)} 는 " +
                        "정확히 지면 아래 발 캡의 절반이다(사양 §8 G2 문언 정정 필요 — design-character).");
                }
            }

            // ---- G3 전 타일 아래팔 획 ≥ 1.0px -------------------------------------------
            {
                var sb = new StringBuilder();
                bool ok = true, invalid = false;
                foreach (StickmanIconBaker.Tile t in output.TileList)
                {
                    StrokeMeasurement m = MeasureWorst(t, g.Forearms, forearmAxes, sampler);
                    if (m.Contaminated)
                    {
                        invalid = true;
                        sb.AppendLine($"{t.Size,4}px {(t.TierSmall ? "S" : "L")}  측정 무효 — {m.Reason} " +
                                      $"(가로 {m.Run.ToString("0.00", inv)} px / 기대 {m.ExpectedRun.ToString("0.00", inv)} px)");
                        continue;
                    }
                    if (m.Perpendicular < MinArmStrokePixels) ok = false;
                    sb.AppendLine($"{t.Size,4}px {(t.TierSmall ? "S" : "L")}  E1 {m.Perpendicular.ToString("0.000", inv),6} px" +
                                  $"  (가로단면 {m.Run.ToString("0.000", inv),6} px · 행 {m.RowCount}개 · " +
                                  $"α≥0.5 화소 {m.HalfAlphaPixels}개 · 기대 {m.ExpectedPerpendicular.ToString("0.000", inv)} px)");
                }
                sb.AppendLine("★ 판정은 E1(= 행 중앙값 Σα × |v_y|)으로 한다 — 문언 그대로의 화소 개수는 정수 " +
                              "양자화 때문에 1.02와 0.92를 구분하지 못한다(클래스 문서 참고).");
                sb.Append("★ 16px 타일의 여유는 0.02px(1.02 vs 1.00)이고 추정량 오차도 같은 크기다 " +
                          "(합성 도형 실측 ≤0.04px) — 이 한 줄은 '통과/실패'가 아니라 '경계'로 읽어라. " +
                          "사양 §12-2가 같은 경고를 적어 뒀다.");
                report.Add("G3", $"각 타일 아래팔 획 ≥ {MinArmStrokePixels.ToString("0.0", inv)} px (사양의 유일한 경성 하한)",
                    invalid ? GateStatus.Invalid : (ok ? GateStatus.Pass : GateStatus.Fail), sb.ToString());
            }

            // ---- G3b 전 타일 "잉크 가로줄의 max α" 바닥 ≥ 0.45 (사양 §12-2(B)) -------------
            {
                var sb = new StringBuilder();
                bool ok = true, any = false, invalid = false;
                foreach (StickmanIconBaker.Tile t in output.TileList)
                {
                    RowAlphaFloor f = MeasureRowAlphaFloor(t, sampler);
                    if (!f.Valid)
                    {
                        // ★ 못 잰 것은 실패가 아니라 <무효>다. 둘을 같은 칸에 넣으면 원인이 섞인다.
                        sb.AppendLine($"{t.Size,4}px  잉크 행을 못 찾았다 — 측정 무효");
                        invalid = true;
                        continue;
                    }
                    any = true;
                    if (f.Min < MinRowMaxAlpha) ok = false;
                    string where = f.MinRow == f.TopRow ? "맨 위 경계행"
                                 : f.MinRow == f.BottomRow ? "맨 아래 경계행" : "내부행";
                    sb.AppendLine($"{t.Size,4}px {(t.TierSmall ? "S" : "L")}  최소 max α {f.Min.ToString("0.000", inv)} " +
                                  $"(행 {f.MinRow}, {where})  ·  경계행 제외 {f.InteriorMin.ToString("0.000", inv)} " +
                                  $"(행 {f.InteriorRow})  ·  잉크 행 {f.InkRows}개");
                }
                sb.AppendLine("★ 판정은 사양 문언대로 <전체 잉크 행>으로 한다. 다만 최솟값이 '경계행'에서 나오면 " +
                              "그건 획이 얇아서가 아니라 프레이밍 위상이다 —");
                sb.AppendLine("  그림 높이가 정수 px가 아닌 타일(20 → 여백 1.25 / 24 → 1.50 / 40 → 2.50)은 " +
                              "맨 위·아래 행이 반쯤만 덮인다. 그 경우 '경계행 제외' 값을 함께 보고 판단해라.");
                sb.Append(output.Config != null && output.Config.BorderWidthPx > 0f
                    ? "★ 플레이트 테두리가 켜져 있다 — 테두리 행이 프로파일을 지배해 이 값이 무의미해진다."
                    : "사양 §3-2 예측: 티어S 16px = 0.99 / 24px = 0.48(여유 0.03뿐). 0.45 아래면 알람이 아니라 조사 대상이다.");
                report.Add("G3b", $"각 타일 잉크 가로줄의 max α 바닥 ≥ {MinRowMaxAlpha.ToString("0.00", inv)} (렌더 판정)",
                    (!any || invalid) ? GateStatus.Invalid : (ok ? GateStatus.Pass : GateStatus.Fail), sb.ToString());
            }

            // ---- G4 16·24 타일에서 팔이 몸통과 구분되는가 --------------------------------
            {
                var sb = new StringBuilder();
                bool ok = true, any = false;
                foreach (int size in BlobScanTiles)
                {
                    StickmanIconBaker.Tile t = output.TileList.Find(x => x.Size == size);
                    if (t == null) continue;
                    any = true;
                    float scanY = g.ShoulderY - BlobScanFractionFromShoulder * (g.ShoulderY - g.TierSmallBottomY);
                    int row = Mathf.Clamp(Mathf.FloorToInt(t.WorldToPixel(new Vector2(0f, scanY)).y), 0, t.Size - 1);
                    int blobs = CountRuns(t, row, sampler, AlphaHalf);
                    if (blobs != ExpectedInkBlobs) ok = false;
                    sb.AppendLine($"{size,4}px  스캔 y(월드) {scanY.ToString("0.0000", inv)} → 행 {row}  잉크 덩어리 {blobs}개 " +
                                  $"(기대 {ExpectedInkBlobs}: 왼팔·몸통·오른팔)");
                }
                sb.Append($"스캔 높이 해석: 어깨 → 반신 크롭 하단 구간의 {(BlobScanFractionFromShoulder * 100f).ToString("0", inv)}% 지점. " +
                          "사양 §8 G4는 기준선을 명시하지 않았다 — 이건 해석이다.");
                report.Add("G4", "16·24 타일에서 두 팔이 몸통과 구분되는가 (반신 크롭의 유일한 조형 위험)",
                    !any ? GateStatus.Invalid : (ok ? GateStatus.Pass : GateStatus.Fail), sb.ToString());
            }

            AddManualGates(report, output);
            return report;
        }

        private static void AddManualGates(Report report, StickmanIconBaker.BakeOutput output)
        {
            report.Add("G5", "★ 실기 캡처 — Windows 11에서 5장",
                GateStatus.Manual,
                "① 다크 작업표시줄 ② 라이트 작업표시줄 ③ Alt+Tab ④ 바탕화면 바로가기 ⑤ 탐색기 [자세히]\n" +
                "다섯 장 전부에서 '사람 형태'로 읽혀야 한다. ★ 이 판정이 최종이고, 오프라인은 여기까지 못 온다.\n" +
                "★ 이 개발 머신에는 Windows가 없다 — 실행 검증 불가. '미확인'이 정직한 상태다.");

            report.Add("G6", "★ exe 그룹 아이콘 리소스에 타일 10종이 실제로 들어갔는가",
                GateStatus.Manual,
                "빌드 산출물의 그룹 아이콘 리소스를 ★직접 열거★해야 한다.\n" +
                "★ 빌드 날짜로 판단하지 마라 — StickMate.exe는 Unity 런처 스텁이다(TEAM.md 거짓통과 #7).\n" +
                ".ico 파일이 생겼다는 것은 exe에 들어갔다는 뜻이 아니다.");

            string g7 = output.BoundaryTierLarge != null && output.BoundaryTierSmall != null
                ? "A/B 두 장을 구웠다. G5의 ③④ 위치에서 나란히 보고 판정해라."
                : "[StickMate/Bake App Icon — 티어 경계 A/B (G7)] 로 두 장을 먼저 구워라.";
            report.Add("G7", "★ 티어 경계 A/B (사양 §3-5 미확정 1건: 40 vs 48)",
                GateStatus.Manual,
                g7 + "\n산술로는 안 갈린다 — 사양이 그렇게 못박았다. 코드가 대신 정하지 않는다.");
        }

        // ====================================================================
        // 측정 — 잉크 피복률(α) 기반. 전부 부분화소.
        // ====================================================================

        /// <summary>플레이트/잉크 기준색을 쥐고 화소의 잉크 피복률 α를 낸다.</summary>
        public sealed class InkSampler
        {
            public Vector3 Plate;      // 0..1
            public Vector3 Ink;        // 0..1
            public Vector3 Axis;       // Plate - Ink
            public float AxisSqr;
            public float PlateLuma, InkLuma;

            /// <summary>
            /// 256 타일에서 기준색을 뽑는다.
            /// <list type="bullet">
            ///   <item>플레이트 = 코너 화소(모서리 마스크는 <b>알파만</b> 건드리므로 RGB는 플레이트 그대로다).
            ///     그리고 그 값이 <c>Settings.PlateColor</c>와 2/255 안에서 같은지 <b>대조</b>한다.</item>
            ///   <item>잉크 = 가장 어두운 화소. 256 타일에는 반드시 포화된 잉크 화소가 있다.</item>
            /// </list>
            /// 두 기준의 휘도 차가 작으면 렌더가 죽은 것이다(<c>-nographics</c> 등) — 그때 <paramref name="error"/>가 찬다.
            /// </summary>
            public static InkSampler Build(StickmanIconBaker.Tile tile, Color expectedPlate, out string error)
            {
                error = null;
                Color32 corner = tile.Pixels[0];
                var s = new InkSampler();
                s.Plate = new Vector3(corner.r / 255f, corner.g / 255f, corner.b / 255f);

                var expected = (Color32)new Color(expectedPlate.r, expectedPlate.g, expectedPlate.b, 1f);
                if (Mathf.Abs(corner.r - expected.r) > 2 || Mathf.Abs(corner.g - expected.g) > 2 ||
                    Mathf.Abs(corner.b - expected.b) > 2)
                {
                    error = $"플레이트 색 불일치 — 코너 화소 #{corner.r:X2}{corner.g:X2}{corner.b:X2} vs " +
                            $"설정 #{expected.r:X2}{expected.g:X2}{expected.b:X2}. 렌더가 배경을 안 칠했거나 색공간이 어긋났다.";
                    return s;
                }

                float best = float.MaxValue;
                Color32 darkest = corner;
                foreach (Color32 c in tile.Pixels)
                {
                    float l = Luma(c);
                    if (l < best) { best = l; darkest = c; }
                }
                s.Ink = new Vector3(darkest.r / 255f, darkest.g / 255f, darkest.b / 255f);
                s.Axis = s.Plate - s.Ink;
                s.AxisSqr = Vector3.Dot(s.Axis, s.Axis);
                s.PlateLuma = Luma(corner);
                s.InkLuma = best;

                if (s.PlateLuma - s.InkLuma < 0.4f)
                {
                    error = $"잉크가 없다 — 플레이트/잉크 휘도 차 {(s.PlateLuma - s.InkLuma):0.000} < 0.4. " +
                            "빈 렌더(예: -nographics)이거나 잉크색이 플레이트와 너무 가깝다.";
                }
                return s;
            }

            public float Alpha(Color32 c)
            {
                if (AxisSqr <= 1e-6f) return 0f;
                var p = new Vector3(c.r / 255f, c.g / 255f, c.b / 255f);
                return Mathf.Clamp01(Vector3.Dot(Plate - p, Axis) / AxisSqr);
            }

            public static float Luma(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
        }

        // --------------------------------------------------------------------
        // 행 표본 — 모든 추정량의 공통 재료
        // --------------------------------------------------------------------

        /// <summary>한 가로줄에서 마디를 가로지르는 런 하나의 요약.</summary>
        public struct RowSample
        {
            public int Row;
            /// <summary>런의 Σα(px) = 그 줄의 <b>기하학적 현 길이</b>(안티에일리어싱이 면적을 보존한다).</summary>
            public float Sum;
            /// <summary>런의 α가중 중심 x(px, 화소 중심 기준). <b>정확히 중심선 위</b>에 있다.</summary>
            public float CentroidX;
            public int HalfAlphaPixels;
            public int Left, Right;
        }

        /// <summary>기대 가로 단면(px) — <b>프리팹 기하</b>에서 유도한다(오염 판정 기준은 흔들리면 안 된다).</summary>
        private static float ExpectedRunPixels(StickmanIconBaker.Tile tile, FigureGeometry.StrokeProbe probe)
        {
            float perp = probe.GeometricPerpendicularFactor;
            float expectedPerp = probe.NominalWidth * tile.UnitsToPixels;
            return perp > 1e-4f ? expectedPerp / perp : expectedPerp;
        }

        /// <summary>
        /// 측정창 <c>[T0, T1]</c> 안에서 행 중심(<c>row + 0.5</c>)이 마디에 드는 행을 전부 훑는다.
        /// <para>창 안에 행이 하나도 없을 만큼 타일이 작으면(16px 아래팔 = 약 2.3행) 중앙점 한 행으로
        /// 되돌아간다 — <b>측정 불가로 만들지 않되, 행 수를 보고서에 찍어</b> 그 사실이 보이게 한다.</para>
        /// </summary>
        private static List<RowSample> SampleRows(StickmanIconBaker.Tile tile, FigureGeometry.StrokeProbe probe,
            InkSampler sampler, float expectedRun, out bool contaminated)
        {
            contaminated = false;
            var rows = new List<RowSample>();

            Vector2 pa = tile.WorldToPixel(probe.Start);
            Vector2 pb = tile.WorldToPixel(probe.End);
            Vector2 wa = Vector2.LerpUnclamped(pa, pb, probe.WindowT0);
            Vector2 wb = Vector2.LerpUnclamped(pa, pb, probe.WindowT1);

            int rowLo = Mathf.CeilToInt(Mathf.Min(wa.y, wb.y) - 0.5f);
            int rowHi = Mathf.FloorToInt(Mathf.Max(wa.y, wb.y) - 0.5f);
            if (rowHi < rowLo)
            {
                rowLo = rowHi = Mathf.FloorToInt(tile.WorldToPixel(probe.Point).y);
            }

            float dy = pb.y - pa.y;
            for (int row = rowLo; row <= rowHi; row++)
            {
                if (row < 0 || row >= tile.Size) continue;
                float x;
                if (Mathf.Abs(dy) < 1e-4f)
                {
                    x = tile.WorldToPixel(probe.Point).x;
                }
                else
                {
                    float t = ((row + 0.5f) - pa.y) / dy;
                    x = pa.x + (pb.x - pa.x) * t;
                }
                int seed = Mathf.Clamp(Mathf.FloorToInt(x), 0, tile.Size - 1);
                RowSample s = RunSample(tile, row, seed, sampler);
                if (s.Sum <= 0f) continue;
                if (expectedRun > 0f && s.Sum > expectedRun * RunContaminationFactor) contaminated = true;
                rows.Add(s);
            }
            return rows;
        }

        // --------------------------------------------------------------------
        // 축(방향) — 사양 §12-2(A). ★ 이미지에서 뽑는다. 각도를 코드에 적지 않는다.
        // --------------------------------------------------------------------

        /// <summary>
        /// 마디의 기울기 추정. <b>행 중심 회귀</b>(클래스 문서 참고)로 <c>|v_y|</c>를 낸다.
        /// PCA를 쓰지 않는 이유와 실측 편향은 클래스 문서에 숫자로 적어 뒀다.
        /// </summary>
        public struct AxisEstimate
        {
            public string Label;

            /// <summary>판정에 쓰는 값. 이미지에서 뽑혔으면 그것, 아니면 프리팹 기하 값.</summary>
            public float PerpFactor;

            /// <summary>이미지(행 중심 회귀)에서 뽑은 값. <see cref="FromImage"/>가 false면 의미 없다.</summary>
            public float ImagePerpFactor;

            /// <summary>프리팹 트랜스폼에서 온 값 — <b>대조용</b>이자 되돌아갈 자리.</summary>
            public float GeometricPerpFactor;

            /// <summary>사양 §12-2(A) 문언 그대로의 α가중 <b>PCA</b> 값 — <b>보고 전용</b>(편향돼 있다).</summary>
            public float LiteralPcaPerpFactor;

            public int Rows;
            public bool FromImage;
            public bool Contaminated;

            /// <summary>이미지 값과 프리팹 값의 차. <see cref="AxisAgreementTolerance"/>를 넘으면 무효다.</summary>
            public float Disagreement => FromImage ? Mathf.Abs(ImagePerpFactor - GeometricPerpFactor) : 0f;

            public bool Disagrees => FromImage && Disagreement > AxisAgreementTolerance;

            public string Describe()
            {
                var inv = CultureInfo.InvariantCulture;
                string src = FromImage
                    ? $"이미지 회귀 {ImagePerpFactor.ToString("0.0000", inv)} (행 {Rows}개)"
                    : $"★ 이미지 추정 불가(행 {Rows}개, 최소 {MinAxisRows}" +
                      (Contaminated ? " · 런 오염" : "") + ") → 프리팹 기하 축 사용";
                return $"{Label} |v_y| = {PerpFactor.ToString("0.0000", inv)} · {src} · " +
                       $"프리팹 기하 {GeometricPerpFactor.ToString("0.0000", inv)} " +
                       $"(차 {Disagreement.ToString("0.0000", inv)}, 허용 {AxisAgreementTolerance.ToString("0.00", inv)}) · " +
                       $"[문언 PCA {LiteralPcaPerpFactor.ToString("0.0000", inv)} — 편향, 판정에 안 씀]";
            }
        }

        /// <summary>
        /// 축은 <b>포즈의 성질</b>이라 타일에 의존하지 않는다 — 가장 큰 타일에서 한 번 재서 전 타일에 쓴다.
        /// </summary>
        public static AxisEstimate MeasureAxis(StickmanIconBaker.Tile tile,
            FigureGeometry.StrokeProbe probe, InkSampler sampler)
        {
            var est = new AxisEstimate
            {
                Label = probe.Label,
                GeometricPerpFactor = probe.GeometricPerpendicularFactor,
                PerpFactor = probe.GeometricPerpendicularFactor,
            };

            List<RowSample> rows = SampleRows(tile, probe, sampler, ExpectedRunPixels(tile, probe),
                out bool contaminated);
            est.Rows = rows.Count;
            est.Contaminated = contaminated;
            if (contaminated || rows.Count < MinAxisRows) return est;

            // α가중 회귀 x = a + t·y. 한 행의 런 중심은 캡슐의 대칭성 때문에 정확히 중심선 위에 있다.
            double w = 0, my = 0, mx = 0;
            foreach (RowSample r in rows)
            {
                w += r.Sum; my += r.Sum * (r.Row + 0.5); mx += r.Sum * r.CentroidX;
            }
            if (w <= 1e-6) return est;
            my /= w; mx /= w;

            double syy = 0, sxy = 0;
            foreach (RowSample r in rows)
            {
                double dv = (r.Row + 0.5) - my;
                syy += r.Sum * dv * dv;
                sxy += r.Sum * dv * (r.CentroidX - mx);
            }
            if (syy <= 1e-9) return est;

            double slope = sxy / syy;
            est.ImagePerpFactor = (float)(1.0 / Math.Sqrt(1.0 + slope * slope));
            est.FromImage = true;
            est.PerpFactor = est.ImagePerpFactor;
            est.LiteralPcaPerpFactor = LiteralPcaPerpFactor(tile, rows, sampler);
            return est;
        }

        /// <summary>같은 표본에 사양 §12-2(A) 문언 그대로의 α가중 PCA를 돌린다 — <b>보고 전용</b>.</summary>
        private static float LiteralPcaPerpFactor(StickmanIconBaker.Tile tile, List<RowSample> rows, InkSampler sampler)
        {
            double w = 0, mx = 0, my = 0;
            foreach (RowSample r in rows)
            {
                for (int x = r.Left; x <= r.Right; x++)
                {
                    float a = sampler.Alpha(tile.Pixels[r.Row * tile.Size + x]);
                    if (a <= 0f) continue;
                    w += a; mx += a * (x + 0.5); my += a * (r.Row + 0.5);
                }
            }
            if (w <= 1e-6) return 0f;
            mx /= w; my /= w;

            double cxx = 0, cyy = 0, cxy = 0;
            foreach (RowSample r in rows)
            {
                for (int x = r.Left; x <= r.Right; x++)
                {
                    float a = sampler.Alpha(tile.Pixels[r.Row * tile.Size + x]);
                    if (a <= 0f) continue;
                    double dx = (x + 0.5) - mx, dv = (r.Row + 0.5) - my;
                    cxx += a * dx * dx; cyy += a * dv * dv; cxy += a * dx * dv;
                }
            }
            cxx /= w; cyy /= w; cxy /= w;

            double disc = Math.Sqrt(Math.Max(0.0, (cxx - cyy) * (cxx - cyy) + 4.0 * cxy * cxy));
            double l1 = (cxx + cyy + disc) * 0.5;
            double vx, vy;
            if (Math.Abs(cxy) > 1e-12) { vx = l1 - cyy; vy = cxy; }
            else if (cyy >= cxx) { vx = 0; vy = 1; }
            else { vx = 1; vy = 0; }
            double n = Math.Sqrt(vx * vx + vy * vy);
            return n <= 1e-12 ? 0f : (float)Math.Abs(vy / n);
        }

        // --------------------------------------------------------------------
        // E1 — 획 수직폭 (G0 · G1 · G3)
        // --------------------------------------------------------------------

        public struct StrokeMeasurement
        {
            public string Label;

            /// <summary>가로 단면 Σα(px)의 <b>행 중앙값</b>. 사양 §12-3 E1의 앞부분.</summary>
            public float Run;

            /// <summary>마디에 <b>수직</b>인 진짜 획 폭(px) = <see cref="Run"/> × |v_y|. 판정은 이걸로 한다.</summary>
            public float Perpendicular;

            /// <summary>같은 런에 <b>프리팹 기하</b> 축을 쓴 값 — 2차 자(축이 갈라지면 여기서 보인다).</summary>
            public float GeometricPerpendicular;

            /// <summary>사양 원문언 그대로의 "α ≥ 0.5 화소 개수"(정수). 대조용으로만 보고한다.</summary>
            public int HalfAlphaPixels;

            /// <summary>기대 가로 단면(px) — 프리팹 명목 폭에서 유도.</summary>
            public float ExpectedRun;

            /// <summary>기대 수직 폭(px).</summary>
            public float ExpectedPerpendicular;

            /// <summary>중앙값을 낸 행 수. 1이면 그 타일에서는 마디가 한 줄뿐이라는 뜻이다.</summary>
            public int RowCount;

            /// <summary>런이 다른 마디와 붙었거나 축이 갈라졌다 = <b>측정 불능</b>. 통과로 읽으면 안 된다.</summary>
            public bool Contaminated;

            public string Reason;
        }

        private static StrokeMeasurement MeasureStroke(StickmanIconBaker.Tile tile,
            FigureGeometry.StrokeProbe probe, AxisEstimate axis, InkSampler sampler)
        {
            var m = new StrokeMeasurement { Label = probe.Label };
            m.ExpectedPerpendicular = probe.NominalWidth * tile.UnitsToPixels;
            m.ExpectedRun = ExpectedRunPixels(tile, probe);

            List<RowSample> rows = SampleRows(tile, probe, sampler, m.ExpectedRun, out bool contaminated);
            m.RowCount = rows.Count;
            if (rows.Count == 0)
            {
                m.Contaminated = true;
                m.Reason = "측정창 안에서 런을 못 찾았다";
                return m;
            }
            if (contaminated)
            {
                m.Contaminated = true;
                m.Reason = "런이 다른 마디와 붙었다";
            }
            if (axis.Disagrees)
            {
                m.Contaminated = true;
                m.Reason = "이미지 축과 프리팹 축이 " + axis.Disagreement.ToString("0.000", CultureInfo.InvariantCulture) +
                           " 만큼 갈라졌다(자세가 바뀌었거나 측정창이 오염됐다)";
            }

            var sums = new List<float>(rows.Count);
            foreach (RowSample r in rows) sums.Add(r.Sum);
            sums.Sort();
            m.Run = sums[sums.Count / 2];

            // 중앙값을 낸 그 행의 "α≥0.5 화소 개수"를 함께 보고한다(사양 원문언 대조용).
            foreach (RowSample r in rows)
            {
                if (Mathf.Approximately(r.Sum, m.Run)) { m.HalfAlphaPixels = r.HalfAlphaPixels; break; }
            }

            m.Perpendicular = m.Run * axis.PerpFactor;
            m.GeometricPerpendicular = m.Run * axis.GeometricPerpFactor;
            return m;
        }

        private static StrokeMeasurement MeasureWorst(StickmanIconBaker.Tile tile,
            List<FigureGeometry.StrokeProbe> probes, List<AxisEstimate> axes, InkSampler sampler)
        {
            var worst = new StrokeMeasurement
            {
                Perpendicular = float.MaxValue, Contaminated = true, Reason = "표본이 없다",
            };
            for (int i = 0; i < probes.Count; i++)
            {
                StrokeMeasurement m = MeasureStroke(tile, probes[i], axes[i], sampler);
                if (m.Contaminated) return m;                      // 하나라도 오염되면 무효다
                if (m.Perpendicular < worst.Perpendicular) worst = m;
            }
            return worst;
        }

        /// <summary>각 표본의 축을 <b>한 타일에서</b> 재 둔다(보통 256).</summary>
        public static List<AxisEstimate> MeasureAxes(StickmanIconBaker.Tile tile,
            List<FigureGeometry.StrokeProbe> probes, InkSampler sampler)
        {
            var list = new List<AxisEstimate>(probes.Count);
            foreach (FigureGeometry.StrokeProbe p in probes) list.Add(MeasureAxis(tile, p, sampler));
            return list;
        }

        /// <summary>
        /// 한 행에서 <paramref name="seedX"/>를 포함하는 <b>연속 런</b>을 요약한다.
        /// <c>Σα</c>는 슬랜트된 띠를 가로선이 자른 현의 길이와 정확히 같다(부분화소).
        /// </summary>
        public static RowSample RunSample(StickmanIconBaker.Tile tile, int row, int seedX, InkSampler sampler)
        {
            var s = new RowSample { Row = row, Left = seedX, Right = seedX };
            int w = tile.Size;
            if (row < 0 || row >= w || seedX < 0 || seedX >= w) return s;
            int rowBase = row * w;
            if (sampler.Alpha(tile.Pixels[rowBase + seedX]) <= AlphaEpsilon)
            {
                // 표본점이 잉크 밖이다 — 좌우 1화소만 봐준다(반올림 경계).
                int alt = -1;
                if (seedX > 0 && sampler.Alpha(tile.Pixels[rowBase + seedX - 1]) > AlphaEpsilon) alt = seedX - 1;
                else if (seedX + 1 < w && sampler.Alpha(tile.Pixels[rowBase + seedX + 1]) > AlphaEpsilon) alt = seedX + 1;
                if (alt < 0) return s;
                seedX = alt;
            }

            int left = seedX;
            while (left - 1 >= 0 && sampler.Alpha(tile.Pixels[rowBase + left - 1]) > AlphaEpsilon) left--;
            int right = seedX;
            while (right + 1 < w && sampler.Alpha(tile.Pixels[rowBase + right + 1]) > AlphaEpsilon) right++;

            float sum = 0f, moment = 0f;
            for (int x = left; x <= right; x++)
            {
                float a = sampler.Alpha(tile.Pixels[rowBase + x]);
                sum += a;
                moment += a * (x + 0.5f);
                if (a >= AlphaHalf) s.HalfAlphaPixels++;
            }
            s.Left = left; s.Right = right; s.Sum = sum;
            s.CentroidX = sum > 1e-6f ? moment / sum : seedX + 0.5f;
            return s;
        }

        /// <summary>런의 Σα(px)만 필요할 때.</summary>
        public static float RunCoverage(StickmanIconBaker.Tile tile, int row, int seedX,
            InkSampler sampler, out int halfAlphaPixels)
        {
            RowSample s = RunSample(tile, row, seedX, sampler);
            halfAlphaPixels = s.HalfAlphaPixels;
            return s.Sum;
        }

        /// <summary>한 행의 잉크 덩어리 개수(α ≥ <paramref name="threshold"/> 연속 구간).</summary>
        public static int CountRuns(StickmanIconBaker.Tile tile, int row, InkSampler sampler, float threshold)
        {
            int w = tile.Size;
            if (row < 0 || row >= w) return -1;
            int rowBase = row * w, runs = 0;
            bool inRun = false;
            for (int x = 0; x < w; x++)
            {
                bool ink = sampler.Alpha(tile.Pixels[rowBase + x]) >= threshold;
                if (ink && !inRun) runs++;
                inRun = ink;
            }
            return runs;
        }

        // --------------------------------------------------------------------
        // E2b — 머리 잉크 지름 (G1의 분모 · G2의 분모). 사양 §12-3.
        // --------------------------------------------------------------------

        public struct HeadMeasurement
        {
            /// <summary>E2b(현-포물선 최소제곱) 지름 — <b>정본</b>.</summary>
            public float Diameter;

            /// <summary>최대 현 — <b>2차 자</b>. 원리가 달라 캡처가 깨지면 크게 갈린다.</summary>
            public float MaxChord;

            /// <summary>적합된 원 중심 y(px)와 프리팹 기하가 말하는 중심 y(px).</summary>
            public float FittedCenterY, GeometricCenterY;

            /// <summary>적합에 쓴 행 수와 <c>c²</c> 잔차 RMS(px²).</summary>
            public int Rows;
            public float Residual;

            public bool Valid;
            public string Reason;
        }

        /// <summary>
        /// 사양 §12-3 <b>E2b</b>: 머리 상단 60% 구간의 각 행에서 현 <c>c(y) = Σα</c>를 모아
        /// <c>c² = 4R² − 4(y−y₀)²</c>를 <b>[1, y, y²] 선형 최소제곱</b>으로 푼다.
        ///
        /// <para>★ §9의 EDT 자를 <b>대체</b>한 것이다(격하가 아니다). EDT는 이진 마스크에서
        /// −1.4 ~ +0.8px를 격자 위상·기울기에 따라 오가며 <b>상수 보정이 불가능</b>했고,
        /// 그 폭이 G1 요구(±0.3%p)의 약 7배였다. E2b는 오프라인 실측에서 오차 ≤0.07px다
        /// (16px 머리에서도 ≤0.04px, 256 머리에서 ≤0.03px — <see cref="CalibrateMeasurement"/>가
        /// 합성 원반으로 매 실행 재확인한다).</para>
        ///
        /// <para>구간을 <b>이미지에서</b> 잡는다: 꼭대기 잉크 행 → 그 아래로 훑어 최대 현을 찾고
        /// 그 위치를 중심으로 본다. 프리팹 기하는 <b>훑을 범위를 정하는 데만</b> 쓰고
        /// (±1px이 무관한 용도), 적합된 중심과 <b>대조</b>해 크게 어긋나면 무효로 낸다.</para>
        /// </summary>
        public static HeadMeasurement MeasureHead(StickmanIconBaker.Tile tile, FigureGeometry g, InkSampler sampler)
        {
            var m = new HeadMeasurement();
            var inv = CultureInfo.InvariantCulture;
            int w = tile.Size;
            float expectedD = g.HeadInkDiameter * tile.UnitsToPixels;
            m.GeometricCenterY = tile.WorldToPixel(g.HeadCenter).y;

            // 1) 꼭대기 잉크 행과 그 행의 최대 α 열(= 머리 꼭짓점 근방).
            int apexRow = -1, apexCol = 0;
            for (int row = w - 1; row >= 0 && apexRow < 0; row--)
            {
                float best = 0f; int bestCol = 0;
                for (int x = 0; x < w; x++)
                {
                    float alpha = sampler.Alpha(tile.Pixels[row * w + x]);
                    if (alpha > best) { best = alpha; bestCol = x; }
                }
                if (best > InkRowAlphaEpsilon) { apexRow = row; apexCol = bestCol; }
            }
            if (apexRow < 0)
            {
                m.Reason = "잉크가 있는 행이 하나도 없다";
                return m;
            }

            // 2) 아래로 훑으며 최대 현을 찾는다(= 원의 중심 행). 훑는 범위는 기대 지름의 1.4배.
            int walk = Mathf.Max(2, Mathf.CeilToInt(expectedD * 1.4f));
            int centerRow = apexRow;
            for (int row = apexRow; row > apexRow - walk && row >= 0; row--)
            {
                float run = RunSample(tile, row, apexCol, sampler).Sum;
                if (run > m.MaxChord) { m.MaxChord = run; centerRow = row; }
            }

            // 3) 상단 60% 구간 = [중심 − 0.1D, 꼭대기]. (머리 전체 높이의 60%가 위쪽에서 잘린 몫)
            int bandLo = Mathf.Max(0, centerRow - Mathf.RoundToInt(0.1f * m.MaxChord));
            var ys = new List<double>();
            var cs = new List<double>();
            for (int row = bandLo; row <= apexRow; row++)
            {
                float run = RunSample(tile, row, apexCol, sampler).Sum;
                if (run <= 0f) continue;
                ys.Add(row + 0.5);
                cs.Add(run);
            }
            m.Rows = ys.Count;
            if (m.Rows < MinHeadFitRows)
            {
                m.Reason = $"적합 행이 {m.Rows}개 (< {MinHeadFitRows}) — E2b를 풀 수 없다";
                return m;
            }

            // 4) [1, u, u²] 최소제곱 (u = y − ȳ 로 옮겨 수치 안정).
            double ybar = 0;
            foreach (double y in ys) ybar += y;
            ybar /= ys.Count;

            var s = new double[3, 4];
            for (int i = 0; i < ys.Count; i++)
            {
                double u = ys[i] - ybar;
                var basis = new[] { 1.0, u, u * u };
                double target = cs[i] * cs[i];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++) s[r, c] += basis[r] * basis[c];
                    s[r, 3] += basis[r] * target;
                }
            }
            double[] a = Solve3(s);
            if (a == null || a[2] >= 0)
            {
                m.Reason = "최소제곱이 풀리지 않았다(포물선이 위로 볼록하지 않다) — 머리가 원반이 아니다";
                return m;
            }

            double u0 = -a[1] / (2.0 * a[2]);
            double rsq = (a[0] + a[1] * u0 + a[2] * u0 * u0) / 4.0;
            if (rsq <= 0)
            {
                m.Reason = "적합된 R² ≤ 0";
                return m;
            }
            m.Diameter = (float)(2.0 * Math.Sqrt(rsq));
            m.FittedCenterY = (float)(ybar + u0);

            double sse = 0;
            for (int i = 0; i < ys.Count; i++)
            {
                double u = ys[i] - ybar;
                double fit = a[0] + a[1] * u + a[2] * u * u;
                double d = fit - cs[i] * cs[i];
                sse += d * d;
            }
            m.Residual = (float)Math.Sqrt(sse / ys.Count);

            // 5) 대조 — 적합된 중심이 프리팹이 말하는 중심에서 지름의 20% 넘게 벗어나면 무효다.
            float delta = Mathf.Abs(m.FittedCenterY - m.GeometricCenterY);
            if (delta > 0.2f * m.Diameter)
            {
                m.Reason = $"적합 중심 y {m.FittedCenterY.ToString("0.00", inv)} 가 프리팹 기하 중심 " +
                           $"{m.GeometricCenterY.ToString("0.00", inv)} 에서 {delta.ToString("0.00", inv)} px " +
                           "벗어났다 — 머리가 아닌 것을 쟀거나 캡처가 어긋났다";
                return m;
            }

            m.Valid = true;
            return m;
        }

        /// <summary>3×4 확대행렬 가우스 소거(부분 피벗). 특이하면 <c>null</c>.</summary>
        private static double[] Solve3(double[,] m)
        {
            for (int c = 0; c < 3; c++)
            {
                int p = c;
                for (int r = c + 1; r < 3; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[p, c])) p = r;
                if (Math.Abs(m[p, c]) < 1e-12) return null;
                if (p != c)
                {
                    for (int k = 0; k < 4; k++) { double t = m[c, k]; m[c, k] = m[p, k]; m[p, k] = t; }
                }
                for (int r = 0; r < 3; r++)
                {
                    if (r == c) continue;
                    double f = m[r, c] / m[c, c];
                    for (int k = c; k < 4; k++) m[r, k] -= f * m[c, k];
                }
            }
            return new[] { m[0, 3] / m[0, 0], m[1, 3] / m[1, 1], m[2, 3] / m[2, 2] };
        }

        // --------------------------------------------------------------------
        // 잉크 높이 (G2의 분자) · 행별 max α 프로파일 (G3b)
        // --------------------------------------------------------------------

        /// <summary>행별 <c>max α</c> 프로파일. 잉크 높이와 G3b가 같은 배열을 쓴다.</summary>
        public static float[] RowMaxAlphaProfile(StickmanIconBaker.Tile tile, InkSampler sampler)
        {
            int w = tile.Size;
            var prof = new float[w];
            for (int row = 0; row < w; row++)
            {
                float best = 0f;
                int b = row * w;
                for (int x = 0; x < w; x++)
                {
                    float a = sampler.Alpha(tile.Pixels[b + x]);
                    if (a > best) best = a;
                }
                prof[row] = best;
            }
            return prof;
        }

        public struct InkHeightMeasurement
        {
            /// <summary>사양 §12-3 G2′: 행별 <c>max α</c> 프로파일의 <b>0.5 교차점 선형보간</b> — 정본.</summary>
            public float Height;

            /// <summary>경계 행의 피복률을 그대로 경계 위치로 읽은 값 — 2차 자.</summary>
            public float CoverageHeight;

            public int TopRow, BottomRow;
            public float TopMaxAlpha, BottomMaxAlpha;
            public bool Valid;
            public string Reason;
        }

        /// <summary>
        /// 잉크 높이(px) — <b>그림 전체</b>의 위·아래 끝. 어느 열을 볼지 고를 필요가 없다.
        /// <para>★ 이전 구현은 정강이 <b>중앙점의 열</b>에서 아래 끝을 찾았는데, 다리가 벌어져 있어
        /// 그 열의 가장 아래 잉크는 <b>발끝이 아니라 종아리 옆구리</b>였다. 행 프로파일은 그 함정이 없다.</para>
        /// <para>사양 §12-3의 못: <c>α&gt;0</c> bbox를 쓰면 상·하 합쳐 약 +1px(= +0.02 머리)라
        /// 허용오차를 통째로 먹는다. 그래서 부분화소로 낸다.</para>
        /// </summary>
        public static InkHeightMeasurement MeasureInkHeight(StickmanIconBaker.Tile tile, InkSampler sampler)
        {
            var m = new InkHeightMeasurement { TopRow = -1, BottomRow = -1 };
            float[] prof = RowMaxAlphaProfile(tile, sampler);
            for (int row = prof.Length - 1; row >= 0; row--)
            {
                if (prof[row] > InkRowAlphaEpsilon) { m.TopRow = row; break; }
            }
            for (int row = 0; row < prof.Length; row++)
            {
                if (prof[row] > InkRowAlphaEpsilon) { m.BottomRow = row; break; }
            }
            if (m.TopRow < 0 || m.BottomRow < 0 || m.TopRow <= m.BottomRow)
            {
                m.Reason = "잉크 행을 못 찾았다";
                return m;
            }
            m.TopMaxAlpha = prof[m.TopRow];
            m.BottomMaxAlpha = prof[m.BottomRow];

            // 0.5 교차 — 위쪽: prof ≥ 0.5 인 가장 위 행 r 과 그 위 행 사이를 선형보간.
            int rt = -1;
            for (int row = m.TopRow; row >= m.BottomRow; row--)
            {
                if (prof[row] >= AlphaHalf) { rt = row; break; }
            }
            int rb = -1;
            for (int row = m.BottomRow; row <= m.TopRow; row++)
            {
                if (prof[row] >= AlphaHalf) { rb = row; break; }
            }
            if (rt < 0 || rb < 0)
            {
                m.Reason = "α ≥ 0.5 인 행이 없다 — 그림이 통째로 회색이다";
                return m;
            }
            float above = rt + 1 < prof.Length ? prof[rt + 1] : 0f;
            float below = rb - 1 >= 0 ? prof[rb - 1] : 0f;
            float top05 = (rt + 0.5f) + (prof[rt] - AlphaHalf) / Mathf.Max(prof[rt] - above, 1e-6f);
            float bot05 = (rb + 0.5f) - (prof[rb] - AlphaHalf) / Mathf.Max(prof[rb] - below, 1e-6f);
            m.Height = top05 - bot05;
            m.CoverageHeight = (m.TopRow + prof[m.TopRow]) - (m.BottomRow + 1f - prof[m.BottomRow]);
            m.Valid = m.Height > 0f;
            if (!m.Valid) m.Reason = "높이가 0 이하다";
            return m;
        }

        /// <summary>사양 §12-2(B) G3b — 잉크가 있는 가로줄들의 <c>max α</c> 중 최솟값.</summary>
        public struct RowAlphaFloor
        {
            public float Min;
            public int MinRow;

            /// <summary>맨 위·맨 아래 잉크 행을 <b>뺀</b> 최솟값. 판정에는 안 쓰고 <b>진단</b>으로만 쓴다.</summary>
            public float InteriorMin;
            public int InteriorRow;

            public int TopRow, BottomRow, InkRows;
            public bool Valid;
        }

        /// <summary>
        /// G3b 측정. <b>문언 그대로</b> 잉크가 있는 모든 가로줄을 센다.
        ///
        /// <para>★ 최솟값이 <b>맨 위/맨 아래 잉크 행</b>에서 나오는 것은 흔하고, 그건 획이 얇아서가 아니라
        /// <b>프레이밍 위상</b> 때문이다 — 그림 높이가 정수 px가 아닌 타일(20 → 여백 1.25 / 24 → 1.50 /
        /// 40 → 2.50)에서는 경계 행이 반쯤만 덮인다. 그래서 최솟값이 어느 행에서 나왔는지와
        /// "경계 행을 뺀 최솟값"을 <b>함께</b> 낸다. 판정은 사양대로 <b>전체</b>로 하되,
        /// 실패했을 때 원인이 획인지 위상인지 보고서에서 바로 갈리게 하기 위해서다.</para>
        /// </summary>
        public static RowAlphaFloor MeasureRowAlphaFloor(StickmanIconBaker.Tile tile, InkSampler sampler)
        {
            var f = new RowAlphaFloor { Min = float.MaxValue, InteriorMin = float.MaxValue, MinRow = -1, InteriorRow = -1 };
            float[] prof = RowMaxAlphaProfile(tile, sampler);
            f.TopRow = -1; f.BottomRow = -1;
            for (int row = prof.Length - 1; row >= 0; row--) { if (prof[row] > InkRowAlphaEpsilon) { f.TopRow = row; break; } }
            for (int row = 0; row < prof.Length; row++) { if (prof[row] > InkRowAlphaEpsilon) { f.BottomRow = row; break; } }
            if (f.TopRow < 0 || f.BottomRow < 0) return f;

            for (int row = 0; row < prof.Length; row++)
            {
                if (prof[row] <= InkRowAlphaEpsilon) continue;
                f.InkRows++;
                if (prof[row] < f.Min) { f.Min = prof[row]; f.MinRow = row; }
                if (row != f.TopRow && row != f.BottomRow && prof[row] < f.InteriorMin)
                {
                    f.InteriorMin = prof[row]; f.InteriorRow = row;
                }
            }
            f.Valid = f.MinRow >= 0;
            if (f.InteriorRow < 0) f.InteriorMin = f.Min;
            return f;
        }

        // ====================================================================
        // C0 — 측정기 교정
        // ====================================================================

        /// <summary>
        /// ★ 이 저장소의 1번 규칙: 계산기를 만들면 <b>알려진 값으로 먼저 교정</b>한다.
        /// 반지름을 아는 원과 폭·각도를 아는 띠를 <b>같은 래스터 모델</b>(8× 슈퍼샘플 + 박스 다운샘플,
        /// 실제 캡처와 동일)로 그린 뒤, <b>출하되는 추정량 전부</b>(E1 · 축 회귀 · E2b · 잉크 높이 ·
        /// G3b 바닥)가 그 값을 되찾는지 본다. <c>null</c>이면 통과.
        ///
        /// <para>★ 사양 §12-5가 요구한 "합성 도형 자기검증을 캡처 스크립트 안에 넣어라"가 이것이다.
        /// 자를 따로 만들면 <b>그 자를 아무도 검사하지 않는다</b> — 그래서 여기서도 실제 게이트와
        /// <b>같은 함수</b>(<see cref="MeasureAxis"/>/<see cref="MeasureStroke"/>/<see cref="MeasureHead"/>/
        /// <see cref="MeasureInkHeight"/>/<see cref="MeasureRowAlphaFloor"/>)를 태운다.</para>
        ///
        /// <para>오프라인 실측(이 합성 타일, 8× 슈퍼샘플 + 8bit 양자화 포함):
        /// 최대 현 +0.0000 / E2b +0.0056 / 축 회귀 −0.0000 / E1 −0.0052 / 잉크 높이 +0.0014 px.
        /// 아래 허용오차는 그 값 위에 넉넉히 얹은 것이다 — <b>추정량이 죽으면 잡되 위상 잡음에는
        /// 안 흔들리는</b> 폭이다.</para>
        /// </summary>
        public static string CalibrateMeasurement() => CalibrateMeasurement(out _);

        // C0 합성 도형 — ★ 한 곳에만 둔다. 보조 함수가 같은 숫자를 다시 적으면 언젠가 갈라진다.
        private const int Size = 256;
        private const float DiscRadius = 24f;
        private const float DiscCx = 128f, DiscCy = 190.5f;
        private const float StripWidth = 10f;
        private const float StripAngleDeg = 40f;
        private const float StripCx = 128f, StripCy = 80.5f;
        private const float StripClipLo = 20f, StripClipHi = 140f;

        /// <param name="detail">통과했을 때도 실제로 되찾은 값들을 보고서에 남긴다.</param>
        public static string CalibrateMeasurement(out string detail)
        {

            var plate = new Color32(233, 234, 230, 255);   // 값 자체는 무관하다 — 대비만 있으면 된다
            var ink = new Color32(0, 0, 0, 255);
            float cos = Mathf.Cos(StripAngleDeg * Mathf.Deg2Rad);
            float sin = Mathf.Sin(StripAngleDeg * Mathf.Deg2Rad);
            Vector2 stripDir = new Vector2(sin, cos);                 // 세로에서 40° 기운 방향
            Vector2 stripNormal = new Vector2(cos, -sin);

            int ss = StickmanIconBaker.Supersample;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int inside = 0;
                    for (int j = 0; j < ss; j++)
                    {
                        float py = y + (j + 0.5f) / ss;
                        for (int i = 0; i < ss; i++)
                        {
                            float px = x + (i + 0.5f) / ss;
                            float dx = px - DiscCx, dy = py - DiscCy;
                            bool hit = dx * dx + dy * dy <= DiscRadius * DiscRadius;
                            if (!hit && py >= StripClipLo && py <= StripClipHi)
                            {
                                Vector2 d = new Vector2(px - StripCx, py - StripCy);
                                float perp = Mathf.Abs(Vector2.Dot(d, stripNormal));
                                if (perp <= StripWidth * 0.5f) hit = true;
                            }
                            if (hit) inside++;
                        }
                    }
                    float a = inside / (float)(ss * ss);
                    pixels[y * Size + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(plate.r, ink.r, a)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(plate.g, ink.g, a)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(plate.b, ink.b, a)),
                        255);
                }
            }

            var tile = new StickmanIconBaker.Tile
            {
                Size = Size, TierSmall = false, Pixels = pixels,
                CameraCenter = Vector2.zero, OrthoSize = 1f, FileStem = "_c0",
            };
            var sampler = InkSampler.Build(tile,
                new Color(plate.r / 255f, plate.g / 255f, plate.b / 255f, 1f), out string err);
            if (err != null)
            {
                detail = null;
                return "합성 타일 자체가 깨졌다 — " + err;
            }

            var inv = CultureInfo.InvariantCulture;
            var problems = new List<string>();
            var note = new StringBuilder();

            // ---- 1) 최대 현 (2차 자) ------------------------------------------------
            float expectedDiameter = DiscRadius * 2f;
            float measuredDiameter = RunCoverage(tile, Mathf.FloorToInt(DiscCy), Mathf.FloorToInt(DiscCx), sampler, out _);
            if (Mathf.Abs(measuredDiameter - expectedDiameter) > 0.3f)
            {
                problems.Add($"합성 원 최대 현: 측정 {measuredDiameter.ToString("0.000", inv)} px vs 참값 " +
                             $"{expectedDiameter.ToString("0.000", inv)} px (허용 ±0.3)");
            }
            note.AppendLine($"최대 현      {measuredDiameter.ToString("0.0000", inv)} px  (참값 {expectedDiameter.ToString("0.00", inv)})");

            // ---- 2) E2b — 사양 §12-3이 EDT를 대체하려고 지정한 그 추정량 --------------
            var syntheticHead = new FigureGeometry
            {
                HeadCenter = PixelToLocal(tile, new Vector2(DiscCx, DiscCy)),
                HeadInkRadius = DiscRadius / tile.UnitsToPixels,
            };
            HeadMeasurement head = MeasureHead(tile, syntheticHead, sampler);
            if (!head.Valid)
            {
                problems.Add("E2b가 합성 원반에서 풀리지 않았다 — " + head.Reason);
            }
            else if (Mathf.Abs(head.Diameter - expectedDiameter) > 0.10f)
            {
                problems.Add($"E2b 지름: 측정 {head.Diameter.ToString("0.0000", inv)} px vs 참값 " +
                             $"{expectedDiameter.ToString("0.00", inv)} px (허용 ±0.10). " +
                             "사양 §12-3이 EDT 대신 이 추정량을 쓰라고 지정했다 — 이게 깨지면 G1·G2가 근거를 잃는다.");
            }
            note.AppendLine($"E2b 지름     {head.Diameter.ToString("0.0000", inv)} px  " +
                            $"(적합 행 {head.Rows}개, 중심 y {head.FittedCenterY.ToString("0.00", inv)} / 참 {DiscCy.ToString("0.00", inv)})");

            // ---- 3) 축 — 이미지에서 뽑은 기울기가 아는 각도를 되찾는가 -----------------
            var stripProbe = new FigureGeometry.StrokeProbe
            {
                Start = PixelToLocal(tile, StripPointAtY(StripClipLo + 5f, sin, cos)),
                End = PixelToLocal(tile, StripPointAtY(StripClipHi - 5f, sin, cos)),
                WindowT0 = 0f,
                WindowT1 = 1f,
                Direction = stripDir,
                NominalWidth = StripWidth / tile.UnitsToPixels,
                Label = "합성 띠",
            };
            AxisEstimate axis = MeasureAxis(tile, stripProbe, sampler);
            if (!axis.FromImage)
            {
                problems.Add($"축을 이미지에서 못 뽑았다(행 {axis.Rows}개) — 측정창 순회가 깨졌다. " +
                             "이러면 게이트가 프리팹 자세를 읽지 못하고 조용히 기하 값으로 되돌아간다.");
            }
            else if (Mathf.Abs(axis.ImagePerpFactor - cos) > 0.02f)
            {
                problems.Add($"축 회귀: |v_y| 측정 {axis.ImagePerpFactor.ToString("0.0000", inv)} vs " +
                             $"참값 cos{StripAngleDeg.ToString("0", inv)}° = {cos.ToString("0.0000", inv)} (허용 ±0.02)");
            }
            note.AppendLine($"축 회귀      |v_y| {axis.ImagePerpFactor.ToString("0.0000", inv)}  " +
                            $"(참값 {cos.ToString("0.0000", inv)}, 행 {axis.Rows}개)");
            note.AppendLine($"  ★ 같은 표본의 사양 문언(α가중 PCA) 값은 {axis.LiteralPcaPerpFactor.ToString("0.0000", inv)} 다 — " +
                            "긴 띠라 차가 작지만, 실제 아래팔처럼 짧은 창에서는 PCA가 −0.06~−0.23까지 틀어진다");
            note.AppendLine("    (측정창을 가로줄로 자르면 표본이 평행사변형이 되어 띠의 폭이 Var(x)에 섞인다 — 클래스 문서 참고).");

            // ---- 4) E1 — 획 수직폭 --------------------------------------------------
            StrokeMeasurement e1 = MeasureStroke(tile, stripProbe, axis, sampler);
            if (e1.Contaminated)
            {
                problems.Add("E1이 합성 띠에서 무효로 나왔다 — " + e1.Reason);
            }
            else if (Mathf.Abs(e1.Perpendicular - StripWidth) > 0.15f)
            {
                problems.Add($"E1 획 폭: 측정 {e1.Perpendicular.ToString("0.0000", inv)} px vs 참값 " +
                             $"{StripWidth.ToString("0.00", inv)} px (허용 ±0.15). " +
                             $"가로 현 중앙값 {e1.Run.ToString("0.000", inv)} px · α≥0.5 화소 {e1.HalfAlphaPixels}개");
            }
            note.AppendLine($"E1 획 폭     {e1.Perpendicular.ToString("0.0000", inv)} px  " +
                            $"(참값 {StripWidth.ToString("0.00", inv)}, 행 {e1.RowCount}개, 가로 현 중앙값 {e1.Run.ToString("0.000", inv)})");

            // ★ 양성 대조 — 기울기 보정이 실제로 일하고 있는가.
            //   보정을 빼면 가로 현(13.05)이 참값(10.00)보다 30% 크다. 그 차이가 안 나오면
            //   보정이 죽어 있다는 뜻이고, 그러면 G0(양성 대조)도 함께 죽는다.
            if (Mathf.Abs(e1.Run - e1.Perpendicular) < 1.0f)
            {
                problems.Add($"기울기 보정이 일하지 않는다 — 가로 현 {e1.Run.ToString("0.000", inv)} 과 " +
                             $"수직 환산 {e1.Perpendicular.ToString("0.000", inv)} 이 거의 같다. " +
                             $"{StripAngleDeg.ToString("0", inv)}° 띠라면 30% 차이가 나야 한다.");
            }

            // ---- 5) 잉크 높이 — G2의 분자 -------------------------------------------
            float expectedInkHeight = (DiscCy + DiscRadius) - StripClipLo;
            InkHeightMeasurement ih = MeasureInkHeight(tile, sampler);
            if (!ih.Valid)
            {
                problems.Add("잉크 높이를 못 쟀다 — " + ih.Reason);
            }
            else if (Mathf.Abs(ih.Height - expectedInkHeight) > 0.15f)
            {
                problems.Add($"잉크 높이: 측정 {ih.Height.ToString("0.0000", inv)} px vs 참값 " +
                             $"{expectedInkHeight.ToString("0.00", inv)} px (허용 ±0.15)");
            }
            note.AppendLine($"잉크 높이    {ih.Height.ToString("0.0000", inv)} px  " +
                            $"(참값 {expectedInkHeight.ToString("0.00", inv)}, 2차 자 피복률 {ih.CoverageHeight.ToString("0.0000", inv)})");

            // ---- 6) G3b 바닥 — 답을 아는 도형에서 재 본다 ----------------------------
            //   합성 원반의 아래 끝은 y = 166.5 라 그 행은 <정확히 반만> 덮인다 ⇒ 바닥 ≈ 0.50.
            RowAlphaFloor floor = MeasureRowAlphaFloor(tile, sampler);
            if (!floor.Valid)
            {
                problems.Add("G3b 행 프로파일에서 잉크 행을 못 찾았다.");
            }
            else if (Mathf.Abs(floor.Min - 0.5f) > 0.08f)
            {
                problems.Add($"G3b 바닥: 측정 {floor.Min.ToString("0.0000", inv)} vs 참값 0.50 (허용 ±0.08). " +
                             "합성 원반의 아래 끝이 행의 정확히 절반을 덮으므로 0.50이 나와야 한다.");
            }
            note.Append($"G3b 바닥     {floor.Min.ToString("0.0000", inv)} @행 {floor.MinRow}  (참값 0.50)");

            detail = note.ToString();
            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        /// <summary>C0 전용 — 합성 타일의 픽셀 좌표를 <c>WorldToPixel</c>의 역으로 되돌린다.</summary>
        private static Vector2 PixelToLocal(StickmanIconBaker.Tile tile, Vector2 pixel)
        {
            float s = tile.UnitsToPixels;
            return new Vector2(tile.CameraCenter.x + (pixel.x - tile.Size * 0.5f) / s,
                               tile.CameraCenter.y + (pixel.y - tile.Size * 0.5f) / s);
        }

        /// <summary>C0 전용 — 합성 띠 중심선 위에서 주어진 y를 지나는 점(픽셀).</summary>
        private static Vector2 StripPointAtY(float y, float sin, float cos)
        {
            float s = (y - StripCy) / cos;
            return new Vector2(StripCx + sin * s, y);
        }

        // ====================================================================
        // EDT — ★ 자로는 폐기됐다(사양 §12-3). 남는 것은 <b>위상 도구</b>로서의 용도뿐이다.
        // ====================================================================

        /// <summary>
        /// 사양 §12-3이 EDT에 남긴 용도 2번: <b>"머리가 그림에서 유일한 큰 원반"임을 확인</b> —
        /// <c>EDT_max</c>가 <b>어디에서</b> 나오는지의 위치 판정이다. <b>거리값은 쓰지 않는다.</b>
        ///
        /// <para>★ 왜 거리값을 안 쓰는가(사양 §12-3 실측): 이진 마스크 EDT의 편향은 상수가 아니라
        /// <b>−1.4 ~ +0.8px 사이를 격자 위상과 기울기에 따라 오간다.</b> 부호가 뒤집히므로 보정이
        /// 불가능하고, G1에 대입하면 실질 폭이 ±2%p가 되어 요구(±0.3%p)의 약 7배가 된다.
        /// 그래서 지름·획은 E2b·E1이 재고, EDT는 <b>"어느 화소가 가장 두꺼운가"</b>만 답한다
        /// — 그 답은 ±1px이 무관한 질문이라 편향이 개입하지 않는다.</para>
        /// </summary>
        private static GateStatus EdtHeadTopology(StickmanIconBaker.Tile tile, FigureGeometry g,
            InkSampler sampler, HeadMeasurement head, out string detail)
        {
            var inv = CultureInfo.InvariantCulture;
            int w = tile.Size;
            var mask = new bool[w * w];
            for (int i = 0; i < mask.Length; i++) mask[i] = sampler.Alpha(tile.Pixels[i]) >= AlphaHalf;

            double[] sq = SquaredEdt(mask, w, w);
            double best = -1;
            int bx = -1, by = -1;
            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (!mask[idx]) continue;
                    if (sq[idx] > best) { best = sq[idx]; bx = x; by = y; }
                }
            }
            if (bx < 0)
            {
                detail = "잉크 화소가 하나도 없다 — 위상 판정 불능.";
                return GateStatus.Invalid;
            }

            Vector2 hc = tile.WorldToPixel(g.HeadCenter);
            float dist = Vector2.Distance(new Vector2(bx + 0.5f, by + 0.5f), hc);
            float radius = head.Diameter * 0.5f;
            bool inside = dist <= radius;
            detail =
                $"EDT 최댓값 화소 ({bx}, {by}) — 머리 중심 ({hc.x.ToString("0.0", inv)}, {hc.y.ToString("0.0", inv)})에서 " +
                $"{dist.ToString("0.00", inv)} px, 머리 반경 {radius.ToString("0.00", inv)} px ⇒ " +
                (inside ? "머리 안이다." : "★ 머리 밖이다 — 머리보다 두꺼운 덩어리가 어딘가 있다.") + "\n" +
                "★ 거리값(2×EDT_max)은 지름으로 쓰지 않는다 — 사양 §12-3이 그 자를 폐기했다(편향 −1.4~+0.8px).";
            return inside ? GateStatus.Pass : GateStatus.Fail;
        }

        /// <summary>
        /// 정확 유클리드 거리변환(제곱). Felzenszwalb–Huttenlocher 의 1D 포물선 하부포락선을
        /// 행 → 열로 분리 적용한다. <c>scipy</c> 없이 도는 사양 §9-3과 같은 방법이다.
        /// <para>배경(마스크 false)까지의 거리 제곱을 낸다. 무한대 대신 <b>큰 유한값</b>을 쓴다 —
        /// 두 무한대의 차가 NaN이 되어 하부포락선이 조용히 무너지는 것을 막는다.</para>
        /// </summary>
        public static double[] SquaredEdt(bool[] mask, int w, int h)
        {
            const double Big = 1e12;   // 최대 거리²(2·256² = 131072)보다 훨씬 크고 double 정밀도 안이다
            var f = new double[Math.Max(w, h)];
            var d = new double[Math.Max(w, h)];
            var v = new int[Math.Max(w, h)];
            var z = new double[Math.Max(w, h) + 1];
            var grid = new double[w * h];

            for (int i = 0; i < grid.Length; i++) grid[i] = mask[i] ? Big : 0.0;

            for (int x = 0; x < w; x++)                       // 열 방향
            {
                for (int y = 0; y < h; y++) f[y] = grid[y * w + x];
                Dt1D(f, d, v, z, h);
                for (int y = 0; y < h; y++) grid[y * w + x] = d[y];
            }
            for (int y = 0; y < h; y++)                       // 행 방향
            {
                for (int x = 0; x < w; x++) f[x] = grid[y * w + x];
                Dt1D(f, d, v, z, w);
                for (int x = 0; x < w; x++) grid[y * w + x] = d[x];
            }
            return grid;
        }

        private static void Dt1D(double[] f, double[] d, int[] v, double[] z, int n)
        {
            int k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = ((f[q] + q * (double)q) - (f[v[k]] + v[k] * (double)v[k])) / (2.0 * q - 2.0 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + q * (double)q) - (f[v[k]] + v[k] * (double)v[k])) / (2.0 * q - 2.0 * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                double dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }
    }
}
