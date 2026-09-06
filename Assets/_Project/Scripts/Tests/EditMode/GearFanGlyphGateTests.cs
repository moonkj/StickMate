using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 우클릭 부채꼴 <b>버튼 아이콘 5종</b>의 조형 회귀 잠금 —
    /// <c>docs/DESIGN_FAN_MENU_ICONS.md</c> §5 <b>FG-1 ~ FG-8</b> (2026-09-06 R26 재조형).
    ///
    /// <para><b>왜 생겼나</b>: 그 재조형은 «부품 이름·획 두께·좌표를 겨눈 테스트 <b>0건</b>»으로
    /// 착지했다. 같은 밤 모자 H-2 대역이 테스트 없이 방치돼 여러 라운드 회귀를 겪은 것과
    /// <b>같은 형태</b>다. 그래서 그 라운드가 고친 결함들을 <b>결함별로</b> 겨눈다 —
    /// 「지금 통과하는 값」을 얼려 스냅샷으로 비교하지 않는다.</para>
    ///
    /// <para><b>재발하면 어느 테스트가 빨개지는가</b>(그 라운드가 실제로 고친 것 다섯):</para>
    /// <list type="table">
    ///   <item><term>확성기가 「음소거/스피커」로 읽혔다</term>
    ///         <description><see cref="확성기는_손잡이가_달려_있고_나팔이_닫힌_한_조각이다"/></description></item>
    ///   <item><term>①↔⑤가 사실상 같은 그림(⑤ 고유 잉크 2.9 %)</term>
    ///         <description><see cref="전원_기호는_스톱워치_잉크에_삼켜지지_않는다"/></description></item>
    ///   <item><term><c>Strike</c> 획이 100 % 가려져 있었다</term>
    ///         <description><see cref="FG4_조각은_2에서_6개이고_100퍼센트_가려진_조각이_없다"/></description></item>
    ///   <item><term>스틱맨 머리가 링이라 캐릭터 본체와 문법이 달랐다</term>
    ///         <description><see cref="스틱맨의_머리는_링이_아니라_채운_원반이다"/></description></item>
    ///   <item><term>획 폭이 6종(1.0/1.4/1.6/1.8/2.0/4.0)으로 사다리 밖</term>
    ///         <description><see cref="FG2_획_폭은_사다리_세_단_밖의_값을_쓰지_않는다"/></description></item>
    /// </list>
    ///
    /// <para>★ <b>2026-09-06 추가(design-art R27 권고5)</b> —
    /// <see cref="FG3화소_축소폴백과_펼침전이에서도_알파0_골이_한_화소_남는다"/>.
    /// FG-3의 «3.0pt»는 W = 2.0 · 배율 1에서 나온 <b>파생값</b>이라 축소 폴백(Ø36)과 펼침 전이에서
    /// 근거를 잃는다. 진짜 기준은 <b>물리 화소</b>이고, 이 앱이 만드는 가장 작은 순간은
    /// <c>Group</c> 배치 배율 × <c>Root</c> 애니메이션 배율의 <b>곱</b>(= 실효 Ø22.3)이다 —
    /// 아무도 그 곱을 재지 않고 있었다.</para>
    ///
    /// <para>★★ <b>함정 하나를 명시적으로 피한다</b>(구현 담당 <c>coder-ui</c>가 직접 남긴 것,
    /// 문서 §32-4로 명문화됨): <b><c>SymbolFixedParts</c>/<c>SymbolParts</c> 배열 길이로 「조형 조각
    /// 개수」를 세지 마라.</b> <see cref="UiChrome.AddPolyline"/>은 꺾은선 <b>1개</b>를 선분마다
    /// <see cref="Image"/> 하나로 분해하므로 <b>배열 길이 ≠ 조각 개수</b>다(④ 확성기는 조형 3조각인데
    /// Image는 13개). 이 파일은 조각을 <b>이름(도형 그룹 태그)</b>으로 센다. 그 함정이 실재한다는 것은
    /// <see cref="대조_FG7을_Image_개수로_세면_거짓_빨강이_난다"/>가 <b>측정으로</b> 증명한다.</para>
    ///
    /// <para>★ <b>상수는 프로덕션을 참조한다</b>(CLAUDE.md). <c>SymbolStroke</c> ·
    /// <c>SymbolStrokeDetail</c> · <c>SymbolStrokeHeavy</c> · <c>SymbolBoxPoints</c> ·
    /// <c>SymbolFieldRadiusPoints</c> · <c>PowerRingDiameterPoints</c> · <c>PowerGapDegrees</c>는
    /// 전부 리플렉션으로 읽는다. 리터럴로 남은 것은 <b>게이트의 문턱값</b>뿐이고(IoU 0.35 · 고유
    /// 잉크 0.45 · 사다리 배수 0.75/1.00/1.50 …) 그것은 「무엇을 검사하는가」의 기준이지 구현값의
    /// 사본이 아니다.</para>
    ///
    /// <para><b><see cref="UiChromeNoShadowTests"/>와의 경계</b>: 그쪽은 <b>알파와 겹 수</b>를 본다
    /// (「접으면 아무것도 안 남는가」 · 「그림자 겹이 없는가」). 이쪽은 <b>기하와 실루엣</b>을 본다
    /// (「어디에 얼마나 굵게 그려졌는가」). 둘 다 <c>BuildButton</c>을 리플렉션으로 부르지만 겹치는
    /// 단언은 하나도 없다 — 실제로 <c>AddPolyline</c>의 <c>sink</c> 오버로드는 그쪽 테스트를 살리려고
    /// 신설됐고(조각을 <c>SymbolParts</c>에 담아야 접힘 알파가 걸린다), 이쪽은 그 <c>sink</c>가 담아
    /// 준 조각들의 <b>모양</b>을 처음으로 잰다. 상호보완이다.</para>
    /// </summary>
    public sealed partial class GearFanGlyphGateTests
    {
        // ================================================================================
        // 문서가 정의한 게이트 문턱 (DESIGN_FAN_MENU_ICONS §5). 구현값의 사본이 아니다.
        // ================================================================================

        /// <summary>FG-2 획 사다리의 <b>배수</b>. 장비 <c>strokeGrade</c> 0/2/3과 같은 값이다.</summary>
        private const float LadderDetailMultiple = 0.75f;
        private const float LadderBaseMultiple = 1.00f;
        private const float LadderHeavyMultiple = 1.50f;

        /// <summary>FG-2 — g2(가장 굵은 단)는 글리프당 최대 몇 조각인가.
        /// 「여기가 이 물건의 위계 꼭대기」라는 뜻이라 둘 이상이면 뜻이 사라진다.</summary>
        private const int MaxHeavyPiecesPerGlyph = 1;

        /// <summary>FG-3 — 서로 다른 덩어리의 코어 간극 하한(W 배수).
        /// 유도: <c>EdgeFeather</c> 0.5pt/변이라 화면에 남는 골 = 간극 − 1.0pt.
        /// 1×(Windows 100 %)에서 골이 2px 남으려면 3.0pt가 필요하다.</summary>
        private const float GapFloorInStrokes = 1.5f;

        /// <summary>FG-4 — 채운 덩어리와 그 구멍의 최소 폭(W 배수).</summary>
        private const float FillFloorInStrokes = 1.5f;

        private const int MinPiecesPerGlyph = 2;
        private const int MaxPiecesPerGlyph = 6;

        /// <summary>FG-1 — 광학 필드 반경(pt). 프로덕션 <c>SymbolFieldRadiusPoints</c>와
        /// <see cref="교정_계측_리그가_알려진_값을_먼저_맞춘다"/>가 대조한다.</summary>
        private const float DocumentedFieldRadiusPoints = 14f;

        /// <summary>FG-6 — 쌍별 실루엣 IoU 상한 · 고유 잉크 하한.</summary>
        private const float SilhouetteIouMax = 0.35f;
        private const float UniqueInkMin = 0.45f;

        /// <summary>FG-7 — <c>Accent</c> 고정 <b>조각</b>은 글리프당 최대 1개.</summary>
        private const int MaxAccentPiecesPerGlyph = 1;

        /// <summary>FG-8 — 잉크 면적(심볼 상자 대비 %)과 잉크 상자 대각(pt).</summary>
        private const float InkAreaMinPercent = 12f;
        private const float InkAreaMaxPercent = 26f;
        private const float InkDiagonalMinPoints = 24f;
        private const float InkDiagonalMaxPoints = 32f;

        // ---- 측정 오차 허용 ----
        /// <summary>두 자(尺)로 잰 획 두께가 어긋나도 되는 폭. <c>UiChrome.Capsule</c>의 캐시 키가
        /// 코어 비율을 1/200로 양자화하므로 0.015pt까지는 구조적으로 생긴다.</summary>
        private const float ThicknessTolerance = 0.05f;

        /// <summary>FG-3 간극 판정 여유. 현행 최악이 <b>정확히 3.00pt</b>라(체크리스트 박스↔글줄,
        /// 스톱워치 분침↔링) 원호 표본화 잔차가 그 등호를 뒤집지 않게 한다.</summary>
        private const float GapTolerance = 0.02f;

        /// <summary>「겹쳤다」로 볼 최소 침투. 이 값보다 얕으면 <b>접선</b>으로 보고 FG-3 금지대에 넣는다.</summary>
        private const float WeldTolerance = 0.02f;

        /// <summary>꺾은선 사슬의 이음매 허용 오차(pt).</summary>
        private const float ChainTolerance = 0.02f;

        // ================================================================================
        // 프로덕션 상수(전부 리플렉션 참조) + 되읽은 글리프
        // ================================================================================

        private static float SymbolStroke;
        private static float SymbolStrokeDetail;
        private static float SymbolStrokeHeavy;
        private static float SymbolBoxPoints;
        private static float SymbolFieldRadiusPoints;
        private static float PowerRingDiameterPoints;
        private static float PowerGapDegrees;
        private static float StopwatchRingDiameterPoints;
        private static float StickHeadDiameterPoints;

        private static readonly List<Glyph> _glyphs = new List<Glyph>();

        [OneTimeSetUp]
        public void 프로덕션_빌더를_실제로_돌려_글리프를_되읽는다()
        {
            _glyphs.Clear();

            SymbolStroke = PrivateFloatConst("SymbolStroke");
            SymbolStrokeDetail = PrivateFloatConst("SymbolStrokeDetail");
            SymbolStrokeHeavy = PrivateFloatConst("SymbolStrokeHeavy");
            SymbolBoxPoints = PrivateFloatConst("SymbolBoxPoints");
            SymbolFieldRadiusPoints = PrivateFloatConst("SymbolFieldRadiusPoints");
            PowerRingDiameterPoints = PrivateFloatConst("PowerRingDiameterPoints");
            PowerGapDegrees = PrivateFloatConst("PowerGapDegrees");
            StopwatchRingDiameterPoints = PrivateFloatConst("StopwatchRingDiameterPoints");
            StickHeadDiameterPoints = PrivateFloatConst("StickHeadDiameterPoints");

            var host = new GameObject("FanGlyphProbe");
            var spawned = new List<GameObject>();
            try
            {
                var widget = host.AddComponent<GearRadialMenuWidget>();
                MethodInfo build = typeof(GearRadialMenuWidget)
                    .GetMethod("BuildButton", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(build,
                    $"{LogPrefix} BuildButton을 못 찾았습니다 — 이 파일의 모든 테스트가 허위 통과합니다.");

                for (int slot = 0; slot < GearRadialMenuWidget.ButtonCount; slot++)
                {
                    object view = build.Invoke(widget, new object[] { slot });
                    var group = (RectTransform)view.GetType().GetField("Group").GetValue(view);
                    spawned.Add(group.gameObject);

                    var symbol = (RectTransform)view.GetType().GetField("Symbol").GetValue(view);
                    var fixedParts = (Image[])view.GetType().GetField("SymbolFixedParts").GetValue(view);
                    _glyphs.Add(ReadGlyph(slot, symbol, fixedParts?.Length ?? 0));
                }
            }
            finally
            {
                foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
                FieldInfo canvas = typeof(GearRadialMenuWidget)
                    .GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic);
                var w = host.GetComponent<GearRadialMenuWidget>();
                if (canvas != null && w != null) canvas.SetValue(w, null);   // OnDestroy가 Destroy를 부르지 않게
                Object.DestroyImmediate(host);
            }

            Assert.AreEqual(GearRadialMenuWidget.ButtonCount, _glyphs.Count,
                $"{LogPrefix} 글리프를 {_glyphs.Count}개만 읽었습니다.");
            foreach (Glyph g in _glyphs) MeasureRaster(g);

            var sb = new StringBuilder();
            sb.Append(LogPrefix).Append(" 실측 — 자: W=").Append(SymbolStroke.ToString("0.##"))
              .Append(" g1=").Append(SymbolStrokeDetail.ToString("0.##"))
              .Append(" g2=").Append(SymbolStrokeHeavy.ToString("0.##"))
              .Append(" 상자=").Append(SymbolBoxPoints.ToString("0.##"))
              .Append(" 필드 r≤").Append(SymbolFieldRadiusPoints.ToString("0.##")).Append('\n');
            sb.Append("칸                조각 Image 덩어리  최소간극  r_max  잉크%   대각\n");
            foreach (Glyph g in _glyphs)
            {
                float gap = WorstCrossBlobGap(g.Cores, out _, out _);
                sb.Append($"{g,-18}{g.PieceNames.Count,4}{g.Cores.Count,6}{BlobCount(g.Cores),7}")
                  .Append(float.IsPositiveInfinity(gap) ? "        —" : $"{gap,9:F2}")
                  .Append($"{g.RMax,8:F2}{g.InkPercent,7:F1}{g.InkDiagonal,7:F2}")
                  .Append(g.InactiveImages.Count == 0 ? "" : $"   (꺼진 조각 제외: {string.Join(", ", g.InactiveImages)})")
                  .Append('\n');
            }
            Debug.Log(sb.ToString());
        }

        private Glyph ReadGlyph(int slot, RectTransform symbol, int fixedPartImageCount)
        {
            Assert.NotNull(symbol, $"{LogPrefix} 슬롯 {slot}에 Symbol 상자가 없습니다.");
            Assert.AreEqual(SymbolBoxPoints, symbol.sizeDelta.x, 0.001f,
                $"{LogPrefix} 슬롯 {slot}의 심볼 상자 폭이 SymbolBoxPoints와 다릅니다.");

            var g = new Glyph
            {
                Slot = slot,
                Label = GearRadialMenuWidget.NameOf(slot),
                FixedPartImageCount = fixedPartImageCount,
            };

            var order = new List<string>();
            var pieces = new Dictionary<string, List<InkCore>>();
            string previousName = null;

            for (int i = 0; i < symbol.childCount; i++)
            {
                Transform child = symbol.GetChild(i);
                var img = child.GetComponent<Image>();
                Assert.NotNull(img,
                    $"{LogPrefix} 슬롯 {slot}의 심볼 자식 '{child.name}'에 Image가 없습니다 — " +
                    "그림이 아닌 것이 심볼 상자에 들어왔다면 이 자(尺)가 그 조각을 세지 못합니다.");

                // 꺼진 채로 태어나는 조각(잔여 시간 호 · 무장 카운트다운)은 화면에 없으므로 세지 않는다.
                if (!child.gameObject.activeSelf) { g.InactiveImages.Add(child.name); continue; }

                InkCore core = ReadCore(img);
                g.Cores.Add(core);

                if (!pieces.TryGetValue(core.Piece, out List<InkCore> bucket))
                {
                    bucket = new List<InkCore>();
                    pieces[core.Piece] = bucket;
                    order.Add(core.Piece);
                }
                else
                {
                    // 같은 이름이 <b>떨어져</b> 나타나면 서로 다른 두 조각이 이름을 공유한다는 뜻이고,
                    // 그 순간 「이름 = 조형 조각」이라는 이 자(尺)의 전제가 깨진다.
                    Assert.AreEqual(core.Piece, previousName,
                        $"{LogPrefix} 슬롯 {slot}에서 조각 이름 '{core.Piece}'이 형제 순서상 끊겨 다시 나타납니다 — " +
                        "서로 다른 두 조형 조각이 같은 이름을 쓰고 있습니다. 이 파일은 조각을 <b>이름</b>으로 " +
                        "세므로(Image 개수로 세면 FG-7이 거짓 빨강을 냅니다) 이름이 겹치면 판정이 무효입니다.");
                }
                bucket.Add(core);
                previousName = core.Piece;
            }

            g.PieceNames = order;
            g.Pieces = pieces;

            Assert.GreaterOrEqual(g.Cores.Count, MinPiecesPerGlyph,
                $"{LogPrefix} {g}에서 읽은 잉크 코어가 {g.Cores.Count}개뿐입니다 — 그물이 비었습니다.");
            return g;
        }

        private static int BlobCount(IReadOnlyList<InkCore> cores)
        {
            int[] labels = BlobsOf(cores);
            var seen = new HashSet<int>(labels);
            return seen.Count;
        }

        /// <summary>서로 다른 덩어리에 속한 코어 쌍의 <b>최악(최소) 간극</b>. 덩어리가 하나면 +∞.</summary>
        private static float WorstCrossBlobGap(IReadOnlyList<InkCore> cores, out string a, out string b)
        {
            int[] labels = BlobsOf(cores);
            float worst = float.PositiveInfinity;
            a = b = null;
            for (int i = 0; i < cores.Count; i++)
            {
                for (int j = i + 1; j < cores.Count; j++)
                {
                    if (labels[i] == labels[j]) continue;
                    float gap = CoreGap(cores[i], cores[j]);
                    if (gap >= worst) continue;
                    worst = gap; a = cores[i].Piece; b = cores[j].Piece;
                }
            }
            return worst;
        }

        private static string Grade(float thickness)
        {
            if (Mathf.Abs(thickness - SymbolStrokeDetail) <= ThicknessTolerance) return "g1";
            if (Mathf.Abs(thickness - SymbolStroke) <= ThicknessTolerance) return "g0";
            if (Mathf.Abs(thickness - SymbolStrokeHeavy) <= ThicknessTolerance) return "g2";
            return null;
        }

        // ================================================================================
        // ★ 0. 거울 교정 — 이게 깨지면 아래 숫자를 전부 폐기한다
        // ================================================================================

        /// <summary>
        /// CLAUDE.md/TEAM.md 공통 처방: <b>계산기를 만들면 알려진 값으로 먼저 교정한다.</b>
        /// 이 자(尺)가 되읽은 값이 프로덕션이 <b>선언한 값</b>과 맞는지 먼저 본다 —
        /// 안 맞으면 FG-1~FG-8의 초록/빨강은 아무것도 뜻하지 않는다.
        /// </summary>
        [Test]
        public void 교정_계측_리그가_알려진_값을_먼저_맞춘다()
        {
            // (1) 획 사다리가 문서의 배수와 같은가 — ux-designer 인계 계약 ②.
            Assert.AreEqual(LadderDetailMultiple * SymbolStroke, SymbolStrokeDetail, 0.0001f,
                $"{LogPrefix} SymbolStrokeDetail이 W×{LadderDetailMultiple}가 아닙니다 — FG-2 사다리의 유도가 깨졌습니다.");
            Assert.AreEqual(LadderHeavyMultiple * SymbolStroke, SymbolStrokeHeavy, 0.0001f,
                $"{LogPrefix} SymbolStrokeHeavy가 W×{LadderHeavyMultiple}가 아닙니다 — FG-2 사다리의 유도가 깨졌습니다.");
            Assert.AreEqual(LadderBaseMultiple * SymbolStroke, SymbolStroke, 0.0001f);

            // (2) FG-1 필드 반경이 문서와 프로덕션 두 곳에서 같은가.
            Assert.AreEqual(DocumentedFieldRadiusPoints, SymbolFieldRadiusPoints, 0.0001f,
                $"{LogPrefix} 프로덕션 SymbolFieldRadiusPoints({SymbolFieldRadiusPoints})와 문서 FG-1" +
                $"({DocumentedFieldRadiusPoints})이 갈라졌습니다 — 한쪽만 고치면 게이트가 다른 선을 잽니다.");

            // (3) 되읽은 링의 지름/두께가 선언값과 같은가.
            InkCore stopwatchRing = PieceCore(Slot(GearMenuButton.FocusMode), "Ring");
            Assert.IsTrue(stopwatchRing.IsArc, $"{LogPrefix} 스톱워치 Ring이 링으로 되읽히지 않았습니다.");
            float stopwatchOuter = (stopwatchRing.MedialRadius + stopwatchRing.HalfThickness) * 2f;
            Assert.AreEqual(StopwatchRingDiameterPoints, stopwatchOuter, ThicknessTolerance,
                $"{LogPrefix} 스톱워치 링의 되읽은 지름 {stopwatchOuter:F3}pt가 선언값 " +
                $"{StopwatchRingDiameterPoints:F3}pt와 다릅니다 — 이 자(尺)가 틀렸습니다.");
            Assert.AreEqual(SymbolStroke, stopwatchRing.Thickness, ThicknessTolerance,
                $"{LogPrefix} 스톱워치 링 두께를 잘못 읽었습니다({stopwatchRing.Thickness:F3}pt).");

            InkCore powerRing = PieceCore(Slot(GearMenuButton.Quit), "PowerRing");
            float powerOuter = (powerRing.MedialRadius + powerRing.HalfThickness) * 2f;
            Assert.AreEqual(PowerRingDiameterPoints, powerOuter, ThicknessTolerance,
                $"{LogPrefix} 전원 링의 되읽은 지름 {powerOuter:F3}pt가 선언값 " +
                $"{PowerRingDiameterPoints:F3}pt와 다릅니다.");

            // (4) 되읽은 틈 각도가 PowerGapDegrees와 같은가 — Filled 배선을 통째로 검산한다.
            float readGap = 360f - (powerRing.EndDeg - powerRing.StartDeg);
            Assert.AreEqual(PowerGapDegrees, readGap, 0.05f,
                $"{LogPrefix} 전원 링의 되읽은 틈이 {readGap:F2}도인데 PowerGapDegrees는 " +
                $"{PowerGapDegrees:F2}도입니다 — fillAmount/fillOrigin/회전 배선 중 하나가 바뀌었습니다.");

            // (5) 채운 원반의 지름.
            InkCore head = PieceCore(Slot(GearMenuButton.Character), "IconHead");
            Assert.AreEqual(StickHeadDiameterPoints, head.HalfThickness * 2f, ThicknessTolerance,
                $"{LogPrefix} 스틱맨 머리의 되읽은 지름이 선언값과 다릅니다.");

            Debug.Log($"{LogPrefix} 교정 통과 — 링 Ø{stopwatchOuter:F2}/Ø{powerOuter:F2} · 틈 {readGap:F2}도 · " +
                      $"머리 Ø{head.HalfThickness * 2f:F2} · 사다리 {SymbolStrokeDetail:0.##}/{SymbolStroke:0.##}/{SymbolStrokeHeavy:0.##}pt.");
        }

        // ================================================================================
        // FG-1 광학 필드
        // ================================================================================

        [Test]
        public void FG1_모든_잉크가_광학_필드_반경_안에_있다()
        {
            var offenders = new List<string>();
            float worst = 0f; string worstWhere = null;

            foreach (Glyph g in _glyphs)
            {
                foreach (InkCore c in g.Cores)
                {
                    float r = CoreMaxRadius(c);
                    if (r > worst) { worst = r; worstWhere = $"{g}·{c.Piece}"; }
                    if (r > SymbolFieldRadiusPoints + 0.001f)
                        offenders.Add($"{g}·{c.Piece} r={r:F2}pt");
                }
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 잉크가 광학 필드(r ≤ {SymbolFieldRadiusPoints:F1}pt)를 넘습니다: " +
                $"{string.Join(", ", offenders)}.\n" +
                "필드는 F-1 링 안쪽 가장자리(r = 20)에서 3W를 비운 선입니다. SymbolBoxPoints 사각형은 " +
                "경계가 아니라 좌표계라 이 판정을 대신하지 못합니다. 단일 돌출 하나만이라면 문서가 " +
                "r ≤ 15.0pt까지 허용하므로 그때는 이 단언을 그 사실과 함께 고치십시오.");

            Debug.Log($"{LogPrefix} FG-1 — 최악 r_max {worst:F2}pt ({worstWhere}), 한계 {SymbolFieldRadiusPoints:F1}pt, " +
                      $"여유 {SymbolFieldRadiusPoints - worst:F2}pt.");
        }

        // ================================================================================
        // FG-2 획 사다리
        // ================================================================================

        /// <summary>
        /// ★ 재조형 전에는 획이 <b>6종</b>(1.0 / 1.4 / 1.6 / 1.8 / 2.0 / 4.0)이었고 그중 다섯이
        /// 어떤 규칙에서도 유도되지 않았다. 1.0pt(0.5W) 박스는 1×에서 <b>점으로 뭉갰고</b>,
        /// 4.0pt(2W) 용두는 가장 덜 중요한 부속에 메뉴에서 가장 굵은 잉크를 얹어 <b>위계가 거꾸로</b>였다.
        /// </summary>
        [Test]
        public void FG2_획_폭은_사다리_세_단_밖의_값을_쓰지_않는다()
        {
            var offenders = new List<string>();
            var seen = new SortedSet<float>();

            foreach (Glyph g in _glyphs)
            {
                foreach (InkCore c in g.Cores)
                {
                    if (c.IsFill) continue;                    // 채운 덩어리는 획이 아니다(FG-4가 본다)
                    seen.Add(Mathf.Round(c.Thickness * 1000f) / 1000f);
                    if (Grade(c.Thickness) == null)
                        offenders.Add($"{g}·{c.Piece} {c.Thickness:F2}pt");
                }
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 사다리 밖 획이 있습니다: {string.Join(", ", offenders)}.\n" +
                $"허용값은 g1={SymbolStrokeDetail:0.##} · g0={SymbolStroke:0.##} · g2={SymbolStrokeHeavy:0.##}pt " +
                "뿐입니다(장비 strokeGrade와 같은 배수). 하한이 ×0.75인 이유는 광학입니다 — " +
                "EdgeFeather 0.5pt/변이라 1.5pt 획은 그려지는 폭의 38.5 %가 램프이고, 그 아래로 " +
                "내려가면 1×에서 회색 얼룩이 됩니다.");

            // g2는 글리프당 한 조각. ★ <b>조각</b>으로 센다 — 꺾은선이면 Image가 여럿이어도 한 조각이다.
            foreach (Glyph g in _glyphs)
            {
                int heavyPieces = 0;
                foreach (string piece in g.PieceNames)
                {
                    foreach (InkCore c in g.Pieces[piece])
                    {
                        if (!c.IsFill && Grade(c.Thickness) == "g2") { heavyPieces++; break; }
                    }
                }
                Assert.LessOrEqual(heavyPieces, MaxHeavyPiecesPerGlyph,
                    $"{LogPrefix} {g}에 g2(×{LadderHeavyMultiple}) 조각이 {heavyPieces}개입니다(최대 {MaxHeavyPiecesPerGlyph}). " +
                    "굵은 획은 «여기가 이 물건의 위계 꼭대기»라는 뜻이라 둘 이상이면 뜻이 사라집니다.");
            }

            Assert.LessOrEqual(seen.Count, 3,
                $"{LogPrefix} 획 폭이 {seen.Count}종입니다({string.Join("/", seen)}) — 사다리는 세 단뿐입니다.");
            Debug.Log($"{LogPrefix} FG-2 — 쓰인 획 폭 {seen.Count}종: {string.Join(" / ", seen)}pt.");
        }

        // ================================================================================
        // FG-3 골
        // ================================================================================

        /// <summary>
        /// ★ 재조형 전 카탈로그 최악은 확성기의 <b>나팔↔목 0.43pt</b>였다. 램프까지 세면 골이
        /// <b>−0.57px</b> — 1×에서 두 획이 <b>한 줄로 합쳐진다</b>. 처방은 「가늘게」가 아니라
        /// 「나팔을 낱획 4개에서 <b>닫힌 한 조각</b>으로」였고, 그러면 간극 규칙 자체가 사라진다.
        /// </summary>
        [Test]
        public void FG3_서로_다른_덩어리는_1_5W_이상_떨어져_있다()
        {
            float floor = GapFloorInStrokes * SymbolStroke;
            var offenders = new List<string>();
            float worst = float.PositiveInfinity; string worstWhere = "—";

            foreach (Glyph g in _glyphs)
            {
                float gap = WorstCrossBlobGap(g.Cores, out string a, out string b);
                if (float.IsPositiveInfinity(gap)) continue;   // 전부 한 덩어리로 용접됐다
                if (gap < worst) { worst = gap; worstWhere = $"{g}·{a}↔{b}"; }
                if (gap < floor - GapTolerance)
                    offenders.Add($"{g}·{a}↔{b} = {gap:F2}pt({gap / SymbolStroke:F2}W)");
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 금지대(0 < 간극 < {floor:F1}pt)에 놓인 쌍이 있습니다: {string.Join(", ", offenders)}.\n" +
                "두 조각은 «겹쳐서 한 덩어리»이거나 «코어 간극 ≥ 1.5W»여야 합니다. 그 사이에서는 " +
                "EdgeFeather 램프가 서로 겹쳐 1×에서 두 획이 한 줄로 읽힙니다. " +
                "붙여야 하는 물건이면 <b>용접</b>하십시오(원래 한 물건이면 한 조각으로 그립니다).");

            Debug.Log($"{LogPrefix} FG-3 — 최악 간극 " +
                      (float.IsPositiveInfinity(worst) ? "없음(전부 한 덩어리)" : $"{worst:F2}pt ({worstWhere})") +
                      $", 하한 {floor:F2}pt.");
        }

        // ================================================================================
        // FG-4 조각 · 채움 · 가려짐
        // ================================================================================

        /// <summary>
        /// ★★ <b>가려진 조각</b> 항목이 옛 체크리스트의 <c>Strike</c>(취소선)를 겨눈다. 그것은
        /// <c>Line2</c>와 <b>정확히 같은 자리·같은 색</b>으로 그려져 화면에 존재하지 않는
        /// <see cref="Image"/> 한 개였다.
        /// <para>★ 이 단언은 <b>부재 단언</b>이라 썩으면 <b>조용히 초록</b>이 된다(CLAUDE.md).
        /// 그래서 <see cref="대조_가려진_조각을_주입하면_같은_검사가_빨개진다"/>가 <b>같은 판정
        /// 함수</b>로 그물이 무는지 증명한다.</para>
        /// </summary>
        [Test]
        public void FG4_조각은_2에서_6개이고_100퍼센트_가려진_조각이_없다()
        {
            float fillFloor = FillFloorInStrokes * SymbolStroke;

            foreach (Glyph g in _glyphs)
            {
                Assert.GreaterOrEqual(g.PieceNames.Count, MinPiecesPerGlyph,
                    $"{LogPrefix} {g}의 조형 조각이 {g.PieceNames.Count}개입니다(하한 {MinPiecesPerGlyph}).");
                Assert.LessOrEqual(g.PieceNames.Count, MaxPiecesPerGlyph,
                    $"{LogPrefix} {g}의 조형 조각이 {g.PieceNames.Count}개입니다(상한 {MaxPiecesPerGlyph}) — " +
                    $"조각 이름: {string.Join(", ", g.PieceNames)}. " +
                    "★ 이 수는 <b>Image 개수가 아니라 이름의 가짓수</b>입니다(꺾은선 1조각 = Image 여럿).");

                List<string> hidden = FullyHiddenImages(g.Cores);
                Assert.IsEmpty(hidden,
                    $"{LogPrefix} {g}에 100 % 가려진 조각이 있습니다: {string.Join(", ", hidden)}.\n" +
                    "옛 체크리스트의 Strike(취소선)가 정확히 이 상태였습니다 — Line2 위에 같은 자리·같은 " +
                    "색으로 그려져 «화면에 존재하지 않는 Image 한 개»였습니다. 지우거나 자리를 옮기십시오.");

                foreach (InkCore c in g.Cores)
                {
                    if (c.IsFill)
                    {
                        Assert.GreaterOrEqual(c.HalfThickness * 2f, fillFloor - 0.001f,
                            $"{LogPrefix} {g}·{c.Piece}의 채운 덩어리 폭이 {c.HalfThickness * 2f:F2}pt로 " +
                            $"하한 {fillFloor:F1}pt 미만입니다.");
                    }
                    if (c.HoleDiameter > 0f)
                    {
                        Assert.GreaterOrEqual(c.HoleDiameter, fillFloor - 0.001f,
                            $"{LogPrefix} {g}·{c.Piece}의 구멍이 Ø{c.HoleDiameter:F2}pt로 하한 {fillFloor:F1}pt " +
                            "미만입니다 — 1×에서 구멍이 메워져 «채운 덩어리»로 읽힙니다.");
                    }
                }
            }

            Debug.Log($"{LogPrefix} FG-4 — 조각 수 " +
                      string.Join(" / ", _glyphs.ConvertAll(g => $"{g.Slot}:{g.PieceNames.Count}(Image {g.Cores.Count})")) +
                      ", 가려진 조각 0개.");
        }

        // ================================================================================
        // FG-5 꺾은선 — 한 조각의 선분들은 끊기지 않는다
        // ================================================================================

        /// <summary>
        /// FG-5는 「곡선인 물건은 꺾은선으로 그린다」인데, 실행 시점에 검사할 수 있는 그 규약의
        /// 실체는 <b>한 조각의 선분들이 이어진 사슬을 이룬다</b>는 것이다.
        /// <para>★ 이 검사가 겨누는 회귀는 확성기다 — 옛 나팔은 <b>낱획 4개</b>가 서로 0.43pt로
        /// 붙어 «한 덩어리로 뭉개진» 형태였다. 낱획으로 되돌리면 조각 이름이 갈라져 FG-4 조각 수가
        /// 늘고 FG-3 금지대가 되살아나지만, 이름만 같게 두고 좌표를 흩는 변형은 <b>여기서만</b> 잡힌다.</para>
        /// </summary>
        [Test]
        public void FG5_한_조각의_선분들은_끊기지_않은_사슬이다()
        {
            var offenders = new List<string>();
            int chains = 0;

            foreach (Glyph g in _glyphs)
            {
                foreach (string piece in g.PieceNames)
                {
                    List<InkCore> cores = g.Pieces[piece];
                    if (cores.Count < 2) continue;
                    chains++;
                    for (int i = 1; i < cores.Count; i++)
                    {
                        if (cores[i - 1].IsArc || cores[i].IsArc)
                        {
                            offenders.Add($"{g}·{piece}[{i}] 원호가 꺾은선 사슬에 섞였습니다");
                            continue;
                        }
                        float seam = Vector2.Distance(cores[i - 1].B, cores[i].A);
                        if (seam > ChainTolerance)
                            offenders.Add($"{g}·{piece} 이음매 {i - 1}→{i}가 {seam:F3}pt 벌어져 있습니다");
                    }
                }
            }

            Assert.Greater(chains, 0,
                $"{LogPrefix} 선분이 2개 이상인 조각이 하나도 없습니다 — 꺾은선이 통째로 사라졌다면 " +
                "이 앱의 곡선 문법(장비 카드 32종과 같은 평탄화 꺾은선)이 부채꼴에서만 되돌려진 것입니다.");
            Assert.IsEmpty(offenders,
                $"{LogPrefix} 꺾은선 사슬이 끊겼습니다: {string.Join(", ", offenders)}.\n" +
                "UiChrome.AddPolyline은 인접한 두 점마다 선분을 만들고, 그 이음매에 캡슐의 반원이 겹쳐 " +
                "라운드 조인이 «공짜로» 나옵니다. 이음매가 벌어졌다면 조각을 낱획으로 흩어 놓은 것이고 " +
                "(옛 확성기 나팔이 그랬습니다) 그 순간 조인이 사라집니다.");

            Debug.Log($"{LogPrefix} FG-5 — 여러 선분으로 realize된 조각 {chains}개, 이음매 전부 연결.");
        }

        private static Glyph Slot(GearMenuButton button) => _glyphs[(int)button];

        private static InkCore PieceCore(Glyph g, string piece)
        {
            Assert.IsTrue(g.Pieces.ContainsKey(piece),
                $"{LogPrefix} {g}에 조각 '{piece}'이 없습니다(있는 것: {string.Join(", ", g.PieceNames)}). " +
                "이름이 바뀌었다면 이 검사도 함께 갱신하십시오 — 그 전까지 이 단언은 <b>대상 없이</b> 돕니다.");
            return g.Pieces[piece][0];
        }
    }
}
