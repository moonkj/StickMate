using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 부채꼴 글리프 — <b>FG-6 실루엣 · FG-7 보조색 · FG-8 광학 크기</b>와,
    /// 2026-09-06 R26이 실제로 고친 결함들을 <b>결함별로</b> 겨눈 회귀 단언.
    /// </summary>
    public sealed partial class GearFanGlyphGateTests
    {
        // ================================================================================
        // FG-6 실루엣
        // ================================================================================

        /// <summary>
        /// ★★ 재조형 전 ①(스톱워치)과 ⑤(전원)은 <b>사실상 같은 그림</b>이었다 — 쌍 IoU 0.735,
        /// ⑤의 잉크 중 <b>2.9 %만</b>이 ①에 없는 잉크였다(94 % 포함). 프로덕션 주석은
        /// <i>"원은 스톱워치와 공유하지만 트인 틈 + 관통하는 세로획이 구분한다"</i>고 적어 두었는데
        /// 실측은 그 문장이 <b>의도였지 사실이 아니었다</b>고 말했다.
        /// </summary>
        [Test]
        public void FG6_다섯_실루엣은_서로_구별된다()
        {
            var offenders = new List<string>();
            float worstIou = 0f, worstUnique = 1f;
            string worstIouPair = "—", worstUniquePair = "—";

            for (int i = 0; i < _glyphs.Count; i++)
            {
                for (int j = i + 1; j < _glyphs.Count; j++)
                {
                    Silhouette(_glyphs[i].Mask, _glyphs[j].Mask, out float iou, out float unique);
                    string pair = $"{_glyphs[i]}↔{_glyphs[j]}";

                    if (iou > worstIou) { worstIou = iou; worstIouPair = pair; }
                    if (unique < worstUnique) { worstUnique = unique; worstUniquePair = pair; }

                    if (iou > SilhouetteIouMax) offenders.Add($"{pair} IoU {iou:F3} > {SilhouetteIouMax}");
                    if (unique < UniqueInkMin) offenders.Add($"{pair} 고유 잉크 {unique:F3} < {UniqueInkMin}");
                }
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 두 칸의 실루엣이 서로 구별되지 않습니다: {string.Join(", ", offenders)}.\n" +
                "1차 신호가 «자리»(한 호 위의 등간격)라 해도 2차 신호가 0이면 «다른 실루엣»이라는 " +
                "설계 문장 자체가 거짓이 됩니다. 2026-09-06 이전의 ①↔⑤가 정확히 그 상태였습니다 " +
                "(IoU 0.735 · ⑤ 고유 잉크 0.029).");

            Debug.Log($"{LogPrefix} FG-6 — 최악 IoU {worstIou:F3}({worstIouPair}, 상한 {SilhouetteIouMax}) · " +
                      $"최소 고유 잉크 {worstUnique:F3}({worstUniquePair}, 하한 {UniqueInkMin}).");
        }

        // ================================================================================
        // FG-7 보조색 — ★ 조각으로 센다(Image 개수로 세면 거짓 빨강)
        // ================================================================================

        [Test]
        public void FG7_Accent_고정_조각은_글리프당_하나뿐이다()
        {
            int totalAccentPieces = 0;
            foreach (Glyph g in _glyphs)
            {
                var accentPieces = new List<string>();
                foreach (string piece in g.PieceNames)
                {
                    foreach (InkCore c in g.Pieces[piece])
                    {
                        if (!IsAccent(c.Color)) continue;
                        accentPieces.Add(piece);
                        break;
                    }
                }
                totalAccentPieces += accentPieces.Count;

                Assert.LessOrEqual(accentPieces.Count, MaxAccentPiecesPerGlyph,
                    $"{LogPrefix} {g}의 Accent 고정 조각이 {accentPieces.Count}개입니다" +
                    $"({string.Join(", ", accentPieces)}, 상한 {MaxAccentPiecesPerGlyph}). " +
                    "보조색은 «이 한 곳이 뜻의 초점»이라는 표시라 둘이면 초점이 사라집니다 " +
                    "(옛 체크리스트가 체크마크를 캡슐 2조각으로 놓아 꼭짓점 각도가 두 곳에 흩어져 있었습니다).\n" +
                    "★ 이 수는 <b>Image 개수가 아니라 조형 조각 개수</b>입니다 — 그 둘을 혼동하면 " +
                    "꺾은선 한 조각이 «2개»로 세어져 거짓 빨강이 납니다.");
            }

            Assert.Greater(totalAccentPieces, 0,
                $"{LogPrefix} Accent 고정 조각이 카탈로그 전체에 0개입니다 — 이 단언이 아무것도 재지 " +
                "않았거나, «완료»를 뜻하는 유일한 색이 화면에서 사라졌습니다. 어느 쪽이든 확인이 필요합니다.");
            Debug.Log($"{LogPrefix} FG-7 — Accent 고정 조각 합계 {totalAccentPieces}개.");
        }

        private static bool IsAccent(Color c)
            => Mathf.Abs(c.r - UiChrome.Accent.r) < 0.002f
            && Mathf.Abs(c.g - UiChrome.Accent.g) < 0.002f
            && Mathf.Abs(c.b - UiChrome.Accent.b) < 0.002f;

        /// <summary>
        /// ★★★ <b>함정의 존재 증명</b> — <c>coder-ui</c>가 인계에 직접 적은 것이고
        /// <c>ux-designer</c> 문서 §32-4가 명문화했다: <b><c>SymbolFixedParts</c> 배열 길이로 FG-7을
        /// 세지 마라.</b>
        ///
        /// <para>이 테스트는 「같은 글리프를 두 방법으로 세면 값이 다르다」를 <b>측정으로</b> 보인다.
        /// 그래야 위 <see cref="FG7_Accent_고정_조각은_글리프당_하나뿐이다"/>의 초록이 «우연히 둘 다
        /// 1이라서»가 아니라 «세는 단위를 옳게 골라서»라는 것이 증명된다.</para>
        ///
        /// <para>★ 언젠가 체크마크가 선분 하나짜리 조각이 되면 두 수가 같아져 이 대조는 «구조적으로
        /// 만들 수 없는 함정»이 된다. 그때는 이 테스트를 <b>지워도 된다</b> — 아래 메시지가 그렇게 말한다
        /// (없는 함정을 지키는 테스트는 러너가 매 라운드 틀린 사실을 주장하게 만든다).</para>
        /// </summary>
        [Test]
        public void 대조_FG7을_Image_개수로_세면_거짓_빨강이_난다()
        {
            Glyph todo = Slot(GearMenuButton.Todo);

            int accentPieces = 0, accentImages = 0;
            foreach (string piece in todo.PieceNames)
            {
                bool any = false;
                foreach (InkCore c in todo.Pieces[piece])
                {
                    if (!IsAccent(c.Color)) continue;
                    any = true;
                    accentImages++;
                }
                if (any) accentPieces++;
            }

            Assert.AreEqual(1, accentPieces,
                $"{LogPrefix} 체크리스트의 Accent 조형 조각이 {accentPieces}개입니다(기대 1).");
            Assert.AreEqual(accentImages, todo.FixedPartImageCount,
                $"{LogPrefix} 되읽은 Accent Image {accentImages}개와 SymbolFixedParts 길이 " +
                $"{todo.FixedPartImageCount}개가 다릅니다 — 둘 중 하나가 그 배열 밖에서 만들어졌습니다.");

            Assert.Greater(accentImages, accentPieces,
                $"{LogPrefix} 이 글리프에서 Accent Image({accentImages})와 조형 조각({accentPieces})의 " +
                "수가 같아졌습니다 — 즉 «배열 길이로 세면 틀린다»는 함정이 이 자리에서 사라졌습니다.\n" +
                "체크마크가 선분 하나가 됐다면 정상적인 변화이고, 그때는 <b>이 대조 테스트를 지우십시오</b>. " +
                "존재하지 않는 함정을 지키는 테스트는 러너가 매 라운드 틀린 사실을 주장하게 만듭니다 " +
                "(GearRadialFanGeometryTests가 위성 폐지 때 같은 이유로 대조 두 개를 지웠습니다).");

            // 함정이 실제로 무엇을 하는지 — 「배열 길이」로 셌을 때의 판정을 그대로 재현한다.
            bool naiveWouldFail = todo.FixedPartImageCount > MaxAccentPiecesPerGlyph;
            Assert.IsTrue(naiveWouldFail,
                $"{LogPrefix} SymbolFixedParts 길이({todo.FixedPartImageCount})로 FG-7을 세도 통과합니다 — " +
                "그렇다면 이 대조는 아무것도 증명하지 못합니다.");

            Debug.Log($"{LogPrefix} 함정 확인 — 체크리스트의 Accent: 조형 조각 {accentPieces}개 / " +
                      $"Image {accentImages}개(SymbolFixedParts 길이 {todo.FixedPartImageCount}). " +
                      $"배열 길이로 세면 상한 {MaxAccentPiecesPerGlyph}을 넘어 거짓 빨강이 난다.");
        }

        // ================================================================================
        // FG-8 광학 크기
        // ================================================================================

        [Test]
        public void FG8_다섯_칸의_광학_무게가_고르다()
        {
            var offenders = new List<string>();
            float minInk = float.MaxValue, maxInk = 0f;

            foreach (Glyph g in _glyphs)
            {
                minInk = Mathf.Min(minInk, g.InkPercent);
                maxInk = Mathf.Max(maxInk, g.InkPercent);

                if (g.InkPercent < InkAreaMinPercent || g.InkPercent > InkAreaMaxPercent)
                    offenders.Add($"{g} 잉크 {g.InkPercent:F1}% (허용 {InkAreaMinPercent}~{InkAreaMaxPercent})");
                if (g.InkDiagonal < InkDiagonalMinPoints || g.InkDiagonal > InkDiagonalMaxPoints)
                    offenders.Add($"{g} 잉크 대각 {g.InkDiagonal:F2}pt (허용 {InkDiagonalMinPoints}~{InkDiagonalMaxPoints})");
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 광학 무게가 어긋난 칸이 있습니다: {string.Join(", ", offenders)}.\n" +
                "다섯 칸이 한 호 위에 나란히 뜨므로 한 칸만 무거우면 그 칸이 «먼저» 읽힙니다. " +
                "되돌릴 수 없는 [앱 종료]가 가장 크고 눈에 먼저 띄게 되는 것이 이 게이트가 막는 것입니다.");

            Debug.Log($"{LogPrefix} FG-8 — 잉크 " +
                      string.Join(" / ", _glyphs.ConvertAll(g => $"{g.Slot}:{g.InkPercent:F1}%·{g.InkDiagonal:F1}pt")) +
                      $" (산포 {maxInk / minInk:F2}배).");
        }

        // ================================================================================
        // ★★ 결함별 회귀 — 2026-09-06 R26이 실제로 고친 것들
        // ================================================================================

        /// <summary>
        /// ★★ <b>옛 확성기는 실루엣이 스피커(볼륨) 아이콘과 같았다.</b> 이 앱에는 소리 설정이 따로
        /// 있으므로 그 그림은 「행동 명령」이 아니라 <b>「음소거/소리」</b>로 읽혔다. 옛 주석은
        /// <i>"24pt 상자에서 손잡이 획은 잉크 얼룩이 된다"</i>며 손잡이를 뺐고, 그 결과가 이것이었다.
        ///
        /// <para>고친 방법 둘을 <b>둘 다</b> 잠근다:</para>
        /// <list type="number">
        ///   <item><b>손잡이를 되살렸다</b> — 나팔 몸통보다 <b>아래로</b> 뻗고 나팔에 <b>용접</b>된
        ///         조각이 하나 있어야 한다. 그것이 «들고 외치는 도구»와 «벽에 붙은 스피커»를 가른다.</item>
        ///   <item><b>나팔을 낱획 4개에서 닫힌 한 조각으로</b> — 그 낱획들이 서로 0.43pt로 붙어 있던
        ///         것이 카탈로그 전체 최악의 간극이었다. 닫힌 폐곡선이면 간극 규칙 자체가 사라진다.</item>
        /// </list>
        /// </summary>
        [Test]
        public void 확성기는_손잡이가_달려_있고_나팔이_닫힌_한_조각이다()
        {
            Glyph action = Slot(GearMenuButton.Action);

            // ── ② 나팔: 여러 선분이 <b>한 조각</b>으로 묶여 <b>닫혀</b> 있는가.
            //    ★ 「선분이 가장 많은 조각」으로 찾지 마라 — 소리선(호)이 8분할이라 나팔(4변)보다 많다.
            //      찾는 성질 자체가 «닫힘»이므로 그것으로 직접 찾는다.
            var closedPieces = new List<string>();
            string hornPiece = null;
            int hornSegments = 0;
            foreach (string piece in action.PieceNames)
            {
                List<InkCore> cores = action.Pieces[piece];
                if (cores.Count < 3) continue;
                if (Vector2.Distance(cores[cores.Count - 1].B, cores[0].A) > ChainTolerance) continue;
                closedPieces.Add(piece);
                if (cores.Count > hornSegments) { hornPiece = piece; hornSegments = cores.Count; }
            }

            Assert.AreEqual(1, closedPieces.Count,
                $"{LogPrefix} 확성기에 <b>닫힌 폐곡선</b> 조각이 {closedPieces.Count}개입니다" +
                $"(찾은 것: {string.Join(", ", closedPieces)} / 전체 조각: {string.Join(", ", action.PieceNames)}).\n" +
                "나팔은 «원래 한 물건»이므로 <b>닫힌 한 조각</b>으로 그립니다. 낱획 4개로 흩어 놓으면 그 " +
                "획들이 서로 0.43pt로 붙어 «한 덩어리로 뭉개지던» 옛 형태로 되돌아갑니다(카탈로그 전체 " +
                "최악의 간극이었고, 램프까지 세면 골이 −0.57px = 1×에서 두 획이 한 줄로 합쳐집니다).");

            List<InkCore> horn = action.Pieces[hornPiece];
            Assert.GreaterOrEqual(hornSegments, 4,
                $"{LogPrefix} 나팔('{hornPiece}')이 {hornSegments}변입니다 — 네 변(위·아래·입·뒤)이 필요합니다.");

            float hornBottom = float.MaxValue;
            foreach (InkCore c in horn) hornBottom = Mathf.Min(hornBottom, Mathf.Min(c.A.y, c.B.y) - c.HalfThickness);

            // ── ① 손잡이: 나팔보다 아래로 뻗고, 나팔에 용접된 조각이 있는가.
            string handle = null;
            float handleReach = 0f;
            foreach (string piece in action.PieceNames)
            {
                if (piece == hornPiece) continue;
                float bottom = float.MaxValue;
                bool welded = false;
                foreach (InkCore c in action.Pieces[piece])
                {
                    bottom = Mathf.Min(bottom, Mathf.Min(c.A.y, c.B.y) - c.HalfThickness);
                    foreach (InkCore h in horn)
                    {
                        if (CoreGap(c, h) < -WeldTolerance) welded = true;
                    }
                }
                if (!welded || bottom >= hornBottom) continue;
                handle = piece; handleReach = hornBottom - bottom;
            }

            Assert.NotNull(handle,
                $"{LogPrefix} 확성기에 <b>손잡이가 없습니다</b>(조각: {string.Join(", ", action.PieceNames)}).\n" +
                "손잡이 없는 나팔은 실루엣이 스피커(볼륨) 아이콘과 같아지고, 이 앱에는 소리 설정이 " +
                "따로 있으므로 이 버튼이 «소리/음소거»로 오독됩니다. 이 버튼은 캐릭터의 상태를 <b>보는</b> " +
                "곳이 아니라 캐릭터에게 <b>시키는</b> 곳이고, 손잡이가 있는 확성기라야 «들고 외치는 도구»로 " +
                "읽힙니다. 손잡이를 «가늘게» 만들어 얼룩을 피하려 하지 마십시오 — 처방은 <b>용접된 g0 한 획</b>입니다.");

            Debug.Log($"{LogPrefix} 확성기 — 나팔 '{hornPiece}' {hornSegments}변 닫힘(유일한 폐곡선) · " +
                      $"손잡이 '{handle}'이 나팔 아래로 {handleReach:F2}pt 뻗어 용접됨 · " +
                      $"전체 조각 {string.Join("/", action.PieceNames)}.");
        }

        /// <summary>
        /// ★★ <b>2026-09-06 이전 ⑤는 ①의 잉크에 94 % 삼켜져 있었다</b>(고유 잉크 2.9 %).
        /// 프로덕션 주석이 <i>"트인 틈 + 관통하는 세로획이 구분한다"</i>고 적어 둔 것은 <b>의도였지
        /// 사실이 아니었다</b>. 상수 하나(<c>PowerRingDiameterPoints</c> 20 → 22)가 그것을 풀었다.
        ///
        /// <para>이 테스트는 그 한 쌍만 <b>이름을 불러</b> 잠그고, 뒤이어 <b>양성 대조</b>로
        /// Ø20으로 되돌린 돌연변이를 <b>같은 판정 함수</b>에 먹여 그때 실제로 빨개지는지 보인다 —
        /// 「지금 통과한다」가 아니라 「되돌리면 잡힌다」를 증명한다.</para>
        /// </summary>
        [Test]
        public void 전원_기호는_스톱워치_잉크에_삼켜지지_않는다()
        {
            Glyph stopwatch = Slot(GearMenuButton.FocusMode);
            Glyph quit = Slot(GearMenuButton.Quit);

            Silhouette(stopwatch.Mask, quit.Mask, out float iou, out float unique);
            Assert.LessOrEqual(iou, SilhouetteIouMax,
                $"{LogPrefix} ①↔⑤의 실루엣 IoU가 {iou:F3}입니다(상한 {SilhouetteIouMax}).");
            Assert.GreaterOrEqual(unique, UniqueInkMin,
                $"{LogPrefix} ①↔⑤의 쌍별 고유 잉크가 {unique:F3}입니다(하한 {UniqueInkMin}) — " +
                "전원 기호가 스톱워치에 삼켜졌습니다. 프로덕션 주석이 주장하는 «트인 틈 + 관통하는 " +
                "세로획이 구분한다»가 다시 <b>사실이 아닌 문장</b>이 됩니다.");

            // ── 양성 대조: Ø22를 옛 값으로 되돌린 돌연변이.
            const float retiredDiameterPoints = 20f;   // 2026-09-06 이전 값. 상수가 아니라 «옛 값»이라 여기 적는다.

            InkCore ring = PieceCore(quit, "PowerRing");
            InkCore stem = PieceCore(quit, "PowerStem");
            float ringOuter = (ring.MedialRadius + ring.HalfThickness) * 2f;
            float stemTop = Mathf.Max(stem.A.y, stem.B.y) + 0f;

            // 돌연변이를 만들려면 프로덕션의 <b>파생식 두 개</b>가 지금 참이어야 한다.
            // 그 전제를 먼저 못박는다 — 아니면 아래 돌연변이는 «옛 ⑤»가 아니라 아무 도형이 된다.
            Assert.AreEqual(PowerRingDiameterPoints, ringOuter, ThicknessTolerance,
                $"{LogPrefix} 링 바깥 지름({ringOuter:F3})이 PowerRingDiameterPoints와 다릅니다.");
            Assert.AreEqual(PowerRingDiameterPoints * 0.5f, stemTop, 0.01f,
                $"{LogPrefix} 세로획의 위 끝({stemTop:F3}pt)이 링 반지름에서 파생되지 않았습니다 — " +
                "«원이 커지면 세로획도 같이 큰다»는 계약이 깨졌고, 아래 돌연변이의 전제가 성립하지 않습니다.");

            var mutant = new List<InkCore>();
            foreach (InkCore c in quit.Cores)
            {
                InkCore m = c;
                if (m.IsArc)
                {
                    // 두께는 그대로 두고 지름만 되돌린다: 중심선 반지름 = (d − 두께)/2.
                    m.MedialRadius = (retiredDiameterPoints - m.Thickness) * 0.5f;
                    m.HoleDiameter = (m.MedialRadius - m.HalfThickness) * 2f;
                }
                else
                {
                    // PowerStemPath = { (0, 아래끝), (0, d/2) } — 위 끝만 옛 지름에서 다시 푼다.
                    float top = retiredDiameterPoints * 0.5f;
                    if (m.A.y > m.B.y) m.A = new Vector2(m.A.x, top); else m.B = new Vector2(m.B.x, top);
                }
                mutant.Add(m);
            }

            bool[] mutantMask = RasterOf(mutant);
            Silhouette(stopwatch.Mask, mutantMask, out float mutantIou, out float mutantUnique);

            Assert.IsTrue(mutantIou > SilhouetteIouMax || mutantUnique < UniqueInkMin,
                $"{LogPrefix} Ø{retiredDiameterPoints:F0}으로 되돌린 돌연변이가 FG-6을 <b>통과</b>합니다" +
                $"(IoU {mutantIou:F3} · 고유 잉크 {mutantUnique:F3}) — 그렇다면 위 초록은 아무것도 " +
                "증명하지 못하고, Ø20→22라는 처방의 근거도 사라집니다. 실측(문서 §6-⑤)은 " +
                "Ø20에서 IoU 0.702 · 고유 잉크 0.056이었습니다.");

            Debug.Log($"{LogPrefix} ①↔⑤ — 현행 Ø{PowerRingDiameterPoints:F0}: IoU {iou:F3} · 고유 잉크 {unique:F3} " +
                      $"→ 되돌림 Ø{retiredDiameterPoints:F0}: IoU {mutantIou:F3} · 고유 잉크 {mutantUnique:F3} " +
                      $"(상한 {SilhouetteIouMax} · 하한 {UniqueInkMin}).");
        }

        /// <summary>
        /// ★ <b>이 버튼은 「캐릭터로 가는 문」의 표지</b>인데, 그 캐릭터 본체의 머리는
        /// <c>1.171932 R</c>짜리 <b>채운 원</b>이다(<c>docs/CHARACTER_BODY_AUDIT_2026-09-05.md</c> §2).
        /// 재조형 전 글리프의 머리는 <b>링</b>이었다 — 표지가 가리키는 대상과 다른 문법으로 그려져 있었다.
        /// <para>되돌림은 <c>AddCircle</c>에 <c>ringThickness</c>를 <b>다시 넘기는 한 줄</b>이라 조용하다.
        /// 그래서 이름이 아니라 <b>스프라이트가 실제로 구운 알파</b>로 «구멍이 있는가»를 본다.</para>
        /// </summary>
        [Test]
        public void 스틱맨의_머리는_링이_아니라_채운_원반이다()
        {
            Glyph character = Slot(GearMenuButton.Character);
            InkCore head = PieceCore(character, "IconHead");

            Assert.IsTrue(head.IsFill,
                $"{LogPrefix} 스틱맨의 머리가 <b>링</b>으로 그려져 있습니다(구멍 Ø{head.HoleDiameter:F2}pt). " +
                "이 버튼은 «캐릭터로 가는 문»의 표지이고, 그 캐릭터 본체의 머리는 <b>채운 원반</b>입니다 — " +
                "표지가 가리키는 대상과 다른 문법으로 그려지면 그 표지는 다른 것을 가리킵니다. " +
                "UiChrome.AddCircle의 ringThickness를 <b>생략</b>하는 것이 곧 «채움»입니다.");
            Assert.AreEqual(0f, head.HoleDiameter, 0.0001f,
                $"{LogPrefix} 채운 원반인데 구멍이 Ø{head.HoleDiameter:F2}pt로 잡혔습니다 — 자(尺)가 틀렸습니다.");

            // 머리는 몸통과 <b>진짜로 겹쳐야</b> 한다. 옛 형태는 정확한 접선(간극 0.00)이라
            // 「한 덩어리」로도 「떨어진 둘」로도 판정되지 않는 애매한 자리였다.
            InkCore spine = PieceCore(character, "IconSpine");
            float weld = CoreGap(head, spine);
            Assert.Less(weld, -WeldTolerance,
                $"{LogPrefix} 머리와 척추가 {weld:F3}pt로 «접선» 상태입니다 — 겹치지도 떨어지지도 않은 " +
                "이 자리는 FG-3이 판정할 수 없는 애매한 지점이고, 재조형이 없앤 것이 바로 그 애매함입니다.");

            // 다섯 칸의 무게가 같아야 한다 — 옛 스틱맨은 <b>혼자</b> 1.8pt(10 % 가늘었다).
            foreach (InkCore c in character.Cores)
            {
                if (c.IsFill) continue;
                Assert.AreEqual(SymbolStroke, c.Thickness, ThicknessTolerance,
                    $"{LogPrefix} 스틱맨의 '{c.Piece}' 획이 {c.Thickness:F2}pt입니다 — 이 글리프는 전부 g0" +
                    $"({SymbolStroke:0.##}pt)여야 합니다. 옛 형태는 다섯 칸 중 <b>혼자</b> 1.8pt라 1×에서 " +
                    "거미줄처럼 얇았습니다.");
            }

            Debug.Log($"{LogPrefix} 스틱맨 — 머리 Ø{head.HalfThickness * 2f:F2} 채움 · " +
                      $"머리↔척추 겹침 {-weld:F2}pt · 획 전부 g0.");
        }

        // ================================================================================
        // ★★ 양성/음성 대조 — 그물이 실제로 무는지 증명한다
        // ================================================================================

        /// <summary>
        /// ★ <see cref="FG4_조각은_2에서_6개이고_100퍼센트_가려진_조각이_없다"/>의 「가려진 조각 0」은
        /// <b>부재 단언</b>이라 썩으면 <b>조용히 초록</b>이 된다(CLAUDE.md — 부재 단언용 니들 61건이
        /// 그렇게 죽어 있었다). 그래서 옛 <c>Strike</c>와 <b>같은 상태</b>를 만들어 <b>같은 판정
        /// 함수</b>에 먹인다.
        /// </summary>
        [Test]
        public void 대조_가려진_조각을_주입하면_같은_검사가_빨개진다()
        {
            Glyph todo = Slot(GearMenuButton.Todo);

            Assert.IsEmpty(FullyHiddenImages(todo.Cores),
                $"{LogPrefix} 주입 전인데 이미 가려진 조각이 검출됐습니다 — 이 대조의 전제가 성립하지 않습니다.");

            // 옛 Strike의 재현: 아래 글줄 위에 <b>정확히 같은 자리·같은 색</b>으로 한 획을 더 얹는다.
            InkCore line = PieceCore(todo, "Line1");
            InkCore strike = line;
            strike.Piece = "Strike";

            var injected = new List<InkCore>(todo.Cores) { strike };
            List<string> hits = FullyHiddenImages(injected);

            Assert.IsNotEmpty(hits,
                $"{LogPrefix} 옛 Strike와 같은 겹친 획을 주입했는데 <b>검출되지 않았습니다</b> — " +
                "FG-4의 «가려진 조각 0»이 내는 초록은 아무것도 증명하지 못합니다.");
            CollectionAssert.Contains(hits.ConvertAll(h => h.Split('#')[0]), "Strike",
                $"{LogPrefix} 주입한 Strike가 아니라 엉뚱한 조각을 잡았습니다: {string.Join(", ", hits)}.");

            Debug.Log($"{LogPrefix} 대조(가려짐) — 주입 전 0건 → 주입 후 {hits.Count}건: {string.Join(", ", hits)}.");
        }

        /// <summary>
        /// ★ <b>사다리 밖 획</b>의 양성 대조. 옛 스틱맨의 1.8pt(사다리 어디에도 없는 값)를
        /// 코어 하나에 되돌려 넣고 <b>같은 판정 함수</b>(<c>Grade</c>)가 그것을 «등급 없음»으로
        /// 돌려주는지 본다.
        /// </summary>
        [Test]
        public void 대조_획_폭을_사다리_밖_값으로_되돌리면_FG2가_빨개진다()
        {
            const float retiredStrokePoints = 1.8f;   // 옛 스틱맨의 획. 상수가 아니라 «옛 값»이라 여기 적는다.

            // 전제: 그 값이 정말로 사다리 밖인가(사다리가 바뀌면 이 대조는 무효다).
            Assert.IsNull(Grade(retiredStrokePoints),
                $"{LogPrefix} {retiredStrokePoints}pt가 지금 사다리 안에 들어와 있습니다 " +
                $"(g1={SymbolStrokeDetail:0.##}/g0={SymbolStroke:0.##}/g2={SymbolStrokeHeavy:0.##}) — " +
                "이 대조는 아무것도 증명하지 못하므로 다른 «옛 값»으로 바꾸거나 이 테스트를 지우십시오.");

            Glyph character = Slot(GearMenuButton.Character);
            var mutant = new List<InkCore>(character.Cores);
            int target = -1;
            for (int i = 0; i < mutant.Count; i++)
            {
                if (mutant[i].IsFill) continue;
                InkCore m = mutant[i];
                m.HalfThickness = retiredStrokePoints * 0.5f;
                mutant[i] = m;
                target = i;
                break;
            }
            Assert.GreaterOrEqual(target, 0, $"{LogPrefix} 되돌릴 획을 못 찾았습니다.");

            var offenders = new List<string>();
            foreach (InkCore c in mutant)
            {
                if (c.IsFill) continue;
                if (Grade(c.Thickness) == null) offenders.Add($"{c.Piece} {c.Thickness:F2}pt");
            }

            Assert.AreEqual(1, offenders.Count,
                $"{LogPrefix} 사다리 밖 획을 하나 주입했는데 검출이 {offenders.Count}건입니다 " +
                $"({string.Join(", ", offenders)}) — FG-2의 초록이 아무것도 증명하지 못합니다.");

            // 그리고 <b>현행</b>은 그 검사에서 0건이어야 한다(음성 대조).
            var clean = new List<string>();
            foreach (InkCore c in character.Cores)
            {
                if (!c.IsFill && Grade(c.Thickness) == null) clean.Add(c.Piece);
            }
            Assert.IsEmpty(clean, $"{LogPrefix} 주입하지 않은 현행에서 사다리 밖 획이 나왔습니다: {string.Join(", ", clean)}.");

            Debug.Log($"{LogPrefix} 대조(획 사다리) — 현행 0건 → {retiredStrokePoints}pt 주입 후 {offenders.Count}건: " +
                      $"{string.Join(", ", offenders)}.");
        }

        /// <summary>
        /// ★ 문서 §8-3 제안 ③ — <b>전원 링의 틈 하한</b>을 닫힌 식으로 한 번 더 잠근다.
        /// 세로획 ↔ 틈 안쪽 가장자리 = <c>(d/2 − W)·sin(틈/2) − W/2</c> ≥ 1.5W.
        ///
        /// <para><see cref="FG3_서로_다른_덩어리는_1_5W_이상_떨어져_있다"/>가 이미 «코어 사이의
        /// 유클리드 최단 거리»로 같은 자리를 재지만, 이쪽은 <b>설계 문서가 실제로 푼 식</b>을
        /// 프로덕션 상수 두 개로 다시 풀어 «그 유도가 지금도 성립하는가»를 본다. 두 자가 서로 다른
        /// 방식이라 한쪽이 썩어도 다른 쪽이 남는다.</para>
        /// </summary>
        [Test]
        public void 전원_링의_틈은_세로획에서_1_5W_이상_떨어진다()
        {
            float w = SymbolStroke;
            float clearance = (PowerRingDiameterPoints * 0.5f - w)
                              * Mathf.Sin(PowerGapDegrees * 0.5f * Mathf.Deg2Rad) - w * 0.5f;
            float floor = GapFloorInStrokes * w;

            Assert.GreaterOrEqual(clearance, floor - 0.001f,
                $"{LogPrefix} 세로획 ↔ 틈 안쪽 가장자리가 {clearance:F2}pt({clearance / w:F2}W)로 하한 " +
                $"{floor:F1}pt에 미달합니다(Ø{PowerRingDiameterPoints:F0} · 틈 {PowerGapDegrees:F0}도).\n" +
                "옛 Ø20·50도가 2.38pt = 1.19W로 정확히 이 상태였습니다(design-art R14 표는 여기를 " +
                "3.23pt = 통과로 적었는데, 캡슐 캡 중심을 ±L/2로 잡은 뒤 반지름을 한 번 더 빼는 " +
                "계산 착오였습니다). Ø20을 유지한다면 틈의 하한은 60도입니다.");

            // 양성 대조 — 옛 조합(Ø20 · 50도)이 이 식에서 실제로 미달인가.
            const float retiredDiameter = 20f, retiredGap = 50f;
            float retired = (retiredDiameter * 0.5f - w) * Mathf.Sin(retiredGap * 0.5f * Mathf.Deg2Rad) - w * 0.5f;
            Assert.Less(retired, floor,
                $"{LogPrefix} 옛 조합(Ø{retiredDiameter:F0}·{retiredGap:F0}도)이 이 식에서 " +
                $"{retired:F2}pt로 하한을 넘습니다 — 그렇다면 이 식이 옛 결함을 설명하지 못합니다.");

            Debug.Log($"{LogPrefix} 전원 틈 — 현행 Ø{PowerRingDiameterPoints:F0}·{PowerGapDegrees:F0}도에서 " +
                      $"{clearance:F2}pt({clearance / w:F2}W) · 옛 Ø{retiredDiameter:F0}·{retiredGap:F0}도에서 " +
                      $"{retired:F2}pt({retired / w:F2}W) · 하한 {floor:F1}pt.");
        }

        // ================================================================================
        // ★★ FG-3 (화소) — 축소 폴백 · 펼침 전이 (design-art R27, 2026-09-06)
        // ================================================================================

        /// <summary>물리 화소 하한 — 골에 <b>알파가 정확히 0인 화소</b>가 최소 몇 개 남아야 하는가.
        /// 「두 획이 갈라져 보인다」의 화소 단위 정의라 1이 하한이다.</summary>
        private const float ZeroAlphaPixelFloor = 1f;

        /// <summary>판정에 쓰는 기기 배율. <b>1.0 = Windows 100 %(비Retina)</b>이고, 물리 화소가
        /// 가장 적은 조합이라 이것이 최악이다(macOS Retina 2×는 모든 지표에서 가장 안전한 열이다).
        /// 배율은 1보다 작아질 수 없으므로 이 하나로 전 DPI가 덮인다.</summary>
        private const float WorstDeviceScale = 1f;

        /// <summary>
        /// ★★ <b>배율이 바뀌면 FG-3의 「3.0pt」는 근거를 잃는다.</b> 그 숫자는 W = 2.0 · 배율 1일 때의
        /// <b>파생값</b>이지 독립된 기준이 아니었다 — 진짜 기준은 <b>화소</b>다.
        ///
        /// <para><b>유도</b>(design-art R27 §1 · 권고1): 스프라이트 굽기가
        /// <c>alpha = clamp01((core − d)/feather + 0.5)</c>이므로 알파가 0이 되는 곳은
        /// <c>d = core + feather/2</c>다. 두 획이 마주 보면 알파 0인 골은 양변 합
        /// <b><c>g − EdgeFeather</c></b>이고(★ <c>g − 2×EdgeFeather</c>가 <b>아니다</b>),
        /// 화면 배율 k와 기기 배율 S를 곱하면
        /// <code>알파0 골(px) = (g − EdgeFeather) · k · S ≥ 1.0</code></para>
        ///
        /// <para>★ <b>이 유도의 분기점을 주석이 아니라 측정으로 잠근다</b> — 되읽은 획마다
        /// 스프라이트에서 «알파가 처음 0이 되는 거리»를 재서 그것이 <c>EdgeFeather</c>가 아니라
        /// 그 <b>절반</b> 쪽임을 확인한다. <c>DESIGN_FAN_MENU_ICONS</c> §1-3의 «골 = g − 1.0pt»가
        /// 바로 그 오해였고, design-art가 오늘 그 모형으로 <b>가짜 미달</b>을 만들었다.</para>
        ///
        /// <para><b>검사 국면 셋</b>: 안착 Ø44 · 안착 축소폴백 Ø36 · <b>축소폴백 × 펼침 첫 프레임</b>.
        /// 마지막이 이 앱에서 버튼이 가장 작아지는 순간이다(<c>Group</c> 배치 배율과 <c>Root</c>
        /// 애니메이션 배율이 <b>곱해진다</b> — 아무도 이 곱을 재지 않고 있었다). 사용자 접힘 바닥
        /// (<c>1 − 0.28</c>)은 <see cref="GearRadialMenuWidget.StartScale"/>보다 <b>크므로</b>
        /// 이 셋 안에 덮인다 — 아래가 그 포함 관계까지 단언한다.</para>
        ///
        /// <para>상수는 전부 참조다: <see cref="GearRadialMenuWidget.ShrunkDiameterPoints"/> ·
        /// <see cref="GearRadialMenuWidget.ButtonDiameterPoints"/> ·
        /// <see cref="GearRadialMenuWidget.StartScale"/> · <see cref="UiChrome.EdgeFeatherPoints"/>.
        /// 숫자를 베끼면 오늘 design-art가 겪은 실수(램프 모형 오가정)가 테스트에 <b>굳는다</b>.</para>
        /// </summary>
        [Test]
        public void FG3화소_축소폴백과_펼침전이에서도_알파0_골이_한_화소_남는다()
        {
            float feather = UiChrome.EdgeFeatherPoints;

            // ── ① 유도의 전제를 측정으로 못박는다: 알파 0은 코어에서 feather «절반» 밖이다.
            int measured = 0;
            foreach (Glyph g in _glyphs)
            {
                foreach (InkCore c in g.Cores)
                {
                    if (c.IsArc || c.IsFill) continue;     // 캡슐 획에서만 잰다
                    measured++;
                    // 기대: 코어 가장자리에서 EdgeFeather의 <b>절반</b> 밖. 허용오차는 텍셀 한 칸
                    // (캡슐이 32텍셀 높이라 굵은 획에서 0.125pt)을 넉넉히 덮는 ±feather/4다.
                    Assert.AreEqual(feather * 0.5f, c.ZeroAlphaExtent, feather * 0.25f,
                        $"{LogPrefix} {g}·{c.Piece}의 알파 0 경계가 코어에서 {c.ZeroAlphaExtent:F3}pt " +
                        $"밖입니다(기대 {feather * 0.5f:F3}pt).\n" +
                        $"· {feather:F3}pt(=EdgeFeather 전부)에 가깝다면 굽는 식이 «램프를 코어 바깥에 " +
                        "통째로» 붙이도록 바뀐 것이고, 그때는 아래 화소 판정식이 (g − EdgeFeather)가 " +
                        "아니라 (g − 2×EdgeFeather)가 됩니다. DESIGN_FAN_MENU_ICONS §1-3이 그 모형으로 " +
                        "«골 = g − 1.0pt»라고 적어 두었고 design-art R27 교정6이 <b>측정으로</b> 정정했습니다.\n" +
                        "· 0에 가깝다면 램프가 사라진 것이고, 그러면 1×에서 계단이 그대로 보입니다.\n" +
                        "어느 쪽이든 <b>판정식을 다시 유도한 뒤에</b> 이 단언을 고치십시오.");
                }
            }
            Assert.Greater(measured, 10,
                $"{LogPrefix} 알파 0 경계를 잰 획이 {measured}개뿐입니다 — 그물이 비었습니다.");

            // ── ② 국면별 화면 배율. Group(배치) × Root(애니메이션)이 곱해진다.
            float shrink = GearRadialMenuWidget.ShrunkDiameterPoints
                           / GearRadialMenuWidget.ButtonDiameterPoints;
            float expandStart = GearRadialMenuWidget.StartScale;

            var phases = new List<KeyValuePair<string, float>>
            {
                new KeyValuePair<string, float>(
                    $"안착 · 기본 Ø{GearRadialMenuWidget.ButtonDiameterPoints:F0}", 1f),
                new KeyValuePair<string, float>(
                    $"안착 · 축소 폴백 Ø{GearRadialMenuWidget.ShrunkDiameterPoints:F0}", shrink),
                new KeyValuePair<string, float>(
                    "축소 폴백 × 펼침 첫 프레임(StartScale)", shrink * expandStart),
            };

            Assert.Less(shrink, 1f,
                $"{LogPrefix} ShrunkDiameterPoints({GearRadialMenuWidget.ShrunkDiameterPoints})가 " +
                $"ButtonDiameterPoints({GearRadialMenuWidget.ButtonDiameterPoints})보다 작지 않습니다 — " +
                "«축소 폴백»이 축소가 아니게 됐고, 이 테스트의 전제가 무너집니다.");
            Assert.Less(expandStart, 1f,
                $"{LogPrefix} StartScale({expandStart})이 1 이상입니다 — 펼침이 «커지며 들어오는» " +
                "연출이 아니라면 이 국면은 최소 배율이 아닙니다.");

            // ── ③ 판정 — 국면 × 글리프의 최악 쌍.
            float worstGapPoints = float.PositiveInfinity;
            string worstGapWhere = null;
            foreach (Glyph g in _glyphs)
            {
                float gap = WorstCrossBlobGap(g.Cores, out string a, out string b);
                if (float.IsPositiveInfinity(gap) || gap >= worstGapPoints) continue;
                worstGapPoints = gap; worstGapWhere = $"{g}·{a}↔{b}";
            }
            Assert.IsFalse(float.IsPositiveInfinity(worstGapPoints),
                $"{LogPrefix} 덩어리가 둘 이상인 글리프가 하나도 없습니다 — 이 테스트가 아무것도 " +
                "재지 않았습니다(전부 용접됐다면 화소 하한을 걱정할 골 자체가 없다는 뜻이지만, " +
                "그 사실을 <b>확인하고</b> 넘어가야 합니다).");

            var offenders = new List<string>();
            var report = new List<string>();
            foreach (KeyValuePair<string, float> phase in phases)
            {
                float worstPixels = float.PositiveInfinity;
                string worstWhere = null;
                foreach (Glyph g in _glyphs)
                {
                    float gap = WorstCrossBlobGap(g.Cores, out string a, out string b);
                    if (float.IsPositiveInfinity(gap)) continue;

                    float pixels = ZeroAlphaPixels(gap, feather, phase.Value);
                    if (pixels < worstPixels) { worstPixels = pixels; worstWhere = $"{g}·{a}↔{b}"; }
                    if (pixels < ZeroAlphaPixelFloor)
                        offenders.Add($"[{phase.Key}] {g}·{a}↔{b} 골 {gap:F2}pt → {pixels:F2}px");
                }
                report.Add($"{phase.Key}: 배율 {phase.Value:F4} · 최악 {worstPixels:F2}px({worstWhere})");
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 알파 0인 골이 물리 화소 {ZeroAlphaPixelFloor:F0}개 아래로 내려간 자리가 " +
                $"있습니다:\n  {string.Join("\n  ", offenders)}\n" +
                "이 자리에서는 두 잉크 덩어리가 <b>한 줄로 합쳐져</b> 보입니다. 판정식은 " +
                $"(간극 − EdgeFeather) × 화면배율 × 기기배율 ≥ {ZeroAlphaPixelFloor:F0}px이고, " +
                "기기배율은 최악값 1.0(Windows 100 %, 비Retina)으로 잡았습니다.\n" +
                "★ 고칠 곳은 «FG-3의 pt 숫자»가 아니라 <b>좌표를 벌리거나 축소 폴백 배율을 올리는 것</b>" +
                "입니다 — 3.0pt는 W = 2.0 · 배율 1에서 나온 파생값이지 독립된 기준이 아닙니다.");

            // ── ④ 이 검사가 덮는 범위를 못박는다.
            //     사용자 접힘 바닥(1 − 0.28 = 0.72)은 StartScale(0.62)보다 크므로 위 셋 안에 있다.
            //     그보다 더 작아지는 국면을 새로 만들면 여기에 항목을 더해야 한다.
            float breakScale = ZeroAlphaPixelFloor / (worstGapPoints - feather) / WorstDeviceScale;
            float smallest = shrink * expandStart;
            Assert.Less(breakScale, smallest,
                $"{LogPrefix} 화소 하한이 깨지기 시작하는 배율이 {breakScale:F4}인데 이 앱이 실제로 " +
                $"만드는 최소 배율은 {smallest:F4}입니다 — 여유가 없습니다.");

            // ── ⑤ 양성 대조 — <같은 판정식>이 실제로 무는지. 임계 바로 아래에서 빨개져야 한다.
            float belowBreak = breakScale * 0.99f;
            int caught = 0;
            foreach (Glyph g in _glyphs)
            {
                float gap = WorstCrossBlobGap(g.Cores, out _, out _);
                if (float.IsPositiveInfinity(gap)) continue;
                if (ZeroAlphaPixels(gap, feather, belowBreak) < ZeroAlphaPixelFloor) caught++;
            }
            Assert.Greater(caught, 0,
                $"{LogPrefix} 임계({breakScale:F4})보다 작은 배율 {belowBreak:F4}에서도 검출이 0건입니다 — " +
                "위 초록은 아무것도 증명하지 못합니다.");

            Debug.Log($"{LogPrefix} FG-3(화소) — 알파0 경계 = 코어 + {feather * 0.5f:F3}pt(획 {measured}개 실측) · " +
                      $"최악 골 {worstGapPoints:F3}pt({worstGapWhere}) · " +
                      $"{string.Join(" / ", report)} · " +
                      $"깨지는 배율 {breakScale:F4} vs 최소 배율 {smallest:F4}(여유 {smallest / breakScale:F2}배) · " +
                      $"임계 아래 대조 검출 {caught}건.");
        }

        /// <summary>골에 남는 <b>알파 0인 물리 화소</b> 수 — design-art R27 권고1의 판정식.
        /// <para><c>(간극 − EdgeFeather) × 화면배율 × 기기배율</c>. 램프가 코어 바깥으로 나가는 폭이
        /// 한쪽당 <c>EdgeFeather/2</c>이므로 양변 합이 <c>EdgeFeather</c> 하나다.</para></summary>
        private static float ZeroAlphaPixels(float gapPoints, float featherPoints, float layoutScale)
            => (gapPoints - featherPoints) * layoutScale * WorstDeviceScale;
    }
}
