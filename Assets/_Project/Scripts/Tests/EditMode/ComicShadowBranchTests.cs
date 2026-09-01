using NUnit.Framework;
using StickMate.Dialogue;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-02 — 만화 레터링 <b>분리막 분기</b>(링 ↔ 한 방향 그림자) + <b>하한을 아는 스냅(b′)</b>
    /// + <b>랩 폭 폰트 연동</b>. 설계는 <c>docs/UX_FLOW.md</c> §44-1/§44-2,
    /// 수치표는 <c>docs/UI_SURFACE_SPEC.md</c> §13.
    ///
    /// 사용자 원문 불만: <b>"텍스트들도 선명하지 않고 깔끔한 게 하나도 없어"</b>.
    /// 사용자 지시: <b>"맥에 적용한 사항 윈도우에도 모두 적용"</b> → 그래서 이 파일의 모든 검산은
    /// 출하하는 <b>다섯 캔버스 배율 전부</b>에서 돈다. macOS는 그중 2.00 한 칸이다.
    ///
    /// ============================================================================
    /// 합격선이 <b>둘</b>이다 — 이름을 안 붙이면 판정이 섞인다(실제로 섞여 있었다)
    /// ============================================================================
    /// <code>
    ///   0.113 x em물리px  =  막두께 x N  +  남는구멍        (N = 2 링 / 1 그림자)
    ///
    ///   C1 분리막 가시성 : 막   >= OutlineRingMinPhysicalPixels (1.000 물리px)
    ///   C2 속공간 개방   : 구멍 >= 검증 운용점의 구멍          (0.823 물리px)
    /// </code>
    /// <b>이 라운드가 사는 것은 C1뿐이다.</b> 그림자로 바꿔도 C2는 한 픽셀의 천분의 일도 나아지지
    /// 않는다 — 예산이 <b>총 소모량</b>으로 정의돼 있어 한쪽에 몰든 쪼개든 속공간이 잃는 양이 같기
    /// 때문이다. 그 사실 자체를 아래 <see cref="그림자로_바꿔도_속공간은_한_톨도_나아지지_않는다"/>가
    /// 단언으로 박아 둔다(다음 라운드가 "그림자로 바꿨는데 왜 아직 번지냐"를 다시 조사하지 않게).
    ///
    /// ============================================================================
    /// 이 파일에 숫자가 없는 이유 (CLAUDE.md)
    /// ============================================================================
    /// 13도 16도 0.823도 적지 않는다. 전부 프로덕션 상수/함수에서 계산한다. 그래야
    /// <see cref="DialogueBubbleRenderer.TextOutlineEmRatio"/>를 다시 재는 날 이 테스트가
    /// <b>스스로 새 결론</b>을 요구한다.
    /// </summary>
    public sealed class ComicShadowBranchTests
    {
        private const string LogPrefix = "[분리막분기-TEST]";

        /// <summary>출하하는 캔버스 배율 다섯 칸(Windows 100/125/150/175% · macOS Retina = 200%).
        /// 2.00은 <see cref="DialogueBubbleRenderer.VerifiedCanvasScale"/>에서 가져온다 — 숫자를 베끼면
        /// 그 상수가 바뀌는 날 이 표만 조용히 낡는다.</summary>
        private static float[] ShippedScales => new[]
        {
            1.00f, 1.25f, 1.50f, 1.75f, DialogueBubbleRenderer.VerifiedCanvasScale
        };

        private static float Retina => DialogueBubbleRenderer.VerifiedCanvasScale;

        /// <summary>
        /// 그 배율에서 <b>이 앱이 실제로 그리는 가장 작은 폰트</b>(= 하한이 물었을 때의 값).
        /// 프로덕션 <c>ResolveFontSize()</c>의 마지막 두 줄과 <b>같은 함수</b>를 부른다 —
        /// 사용자 설정/캐릭터 배율이 무엇이든 결과가 이 값 <b>아래로는</b> 내려가지 않으므로,
        /// C1을 여기서 통과시키면 모든 설정에서 통과한다(막은 폰트에 비례해 커진다).
        /// </summary>
        private static int ShippedFloorFont(float scale)
        {
            int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(scale);
            return DialogueBubbleRenderer.SnapPointsNotBelow(floor, floor, scale);
        }

        private static float MembranePhysicalPixels(float scale)
            => DialogueBubbleRenderer.MembranePointsFor(ShippedFloorFont(scale), scale) * scale;

        private static float CounterPhysicalPixels(float scale)
            => DialogueBubbleRenderer.RemainingCounterPointsFor(ShippedFloorFont(scale)) * scale;

        // ====================================================================
        // (1) ★ 본 단언 — C1이 <b>다섯 배율 전부</b>에서 충족된다
        //     두 임무(분기 + 스냅)는 독립이 아니다. 같이 넣어야 이 표가 닫힌다.
        // ====================================================================

        [Test]
        public void 다섯_배율_전부에서_분리막이_한_물리픽셀_이상이다()
        {
            foreach (float scale in ShippedScales)
            {
                int font = ShippedFloorFont(scale);
                bool ring = DialogueBubbleRenderer.UseOutlineRing(font, scale);
                float membrane = MembranePhysicalPixels(scale);

                Debug.Log($"{LogPrefix} 배율 {scale:F2}: 폰트 {font}pt / 분기 {(ring ? "링" : "그림자")} / " +
                          $"막 {membrane:F3}물리px / 구멍 {CounterPhysicalPixels(scale):F3}물리px.");

                Assert.GreaterOrEqual(membrane, DialogueBubbleRenderer.OutlineRingMinPhysicalPixels - 1e-4f,
                    $"{LogPrefix} ★ 배율 {scale:F2}에서 막이 {membrane:F3}물리픽셀에 그칩니다. " +
                    "서브픽셀 막은 <안 보여서>가 아니라 <위상 의존성> 때문에 실패합니다 — 같은 획인데도 " +
                    "위치에 따라 한 픽셀에 뭉치거나 두 픽셀로 갈라지고, 블록이 기울어 있어 획을 따라 " +
                    "밝아졌다 어두워졌다 합니다. 처방: 그 배율의 분기 판정" +
                    $"({nameof(DialogueBubbleRenderer.UseOutlineRing)})과 하한 스냅" +
                    $"({nameof(DialogueBubbleRenderer.SnapPointsNotBelow)})을 함께 보세요 — 둘은 독립이 아닙니다.");
            }
        }

        // ====================================================================
        // (2) ★ 네거티브 컨트롤 — 링을 강제하면 (1)이 실제로 빨개진다
        //     이게 없으면 (1)은 "언제나 참인 단언"일 수 있다(이 저장소가 하룻밤에 일곱 번 당했다).
        // ====================================================================

        [Test]
        public void 네거티브컨트롤_링을_강제하면_낮은_배율에서_C1이_깨진다()
        {
            int broken = 0;
            foreach (float scale in ShippedScales)
            {
                int font = ShippedFloorFont(scale);
                // 분기를 무시하고 링으로 그렸을 때의 막 = t (그림자의 절반).
                float ringOnly = font * DialogueBubbleRenderer.TextOutlineEmRatio * scale;
                if (ringOnly < DialogueBubbleRenderer.OutlineRingMinPhysicalPixels) broken++;
            }

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 링을 강제하면 {broken}/{ShippedScales.Length} 배율에서 C1이 깨진다.");

            Assert.Greater(broken, 0,
                $"{LogPrefix} ★ 링만으로도 다섯 배율 전부가 C1을 통과합니다. 그렇다면 위 " +
                $"{nameof(다섯_배율_전부에서_분리막이_한_물리픽셀_이상이다)}는 <그림자 분기가 없어도 참>이라 " +
                "아무것도 지키지 못합니다. 분기가 실제로 사는 값이 없다면 분기를 지우는 것이 옳습니다.");
        }

        // ====================================================================
        // (3) ★ macOS 경계 — 등식이라 여유가 0pt다. <b>변이</b>로 증명한다
        // ====================================================================

        [Test]
        public void 검증_배율은_링_분기다_그리고_그것은_구조적_항등이다()
        {
            int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(Retina);
            int need = DialogueBubbleRenderer.OutlineLegibleFontFloor(Retina);

            Assert.IsTrue(DialogueBubbleRenderer.UseOutlineRing(floor, Retina),
                $"{LogPrefix} ★ 검증 배율({Retina:F2} = macOS 전부 · Windows 200%)이 그림자 분기로 " +
                $"넘어갔습니다(하한 {floor}pt < 링 요구 {need}pt). 그림자는 획 둘레의 50%만 지키고 " +
                "나머지는 바탕화면 대비에 맡깁니다 — macOS는 링이 멀쩡히 서는 유일한 칸이므로 " +
                "그 거래를 할 이유가 없습니다.");

            // ★ 이 등식이 macOS를 링에 묶어 두는 <b>구조</b>다. ResolveMinComicFontSize의 상한이
            //   바로 OutlineLegibleFontFloor(VerifiedCanvasScale)라, 검증 배율에서는 하한과 요구가
            //   <b>같은 식</b>이 된다. 상한을 다른 것으로 바꾸는 순간 이 성질이 사라진다.
            Assert.AreEqual(need, floor,
                $"{LogPrefix} 검증 배율에서 하한({floor}pt)과 링 요구({need}pt)가 갈라졌습니다 — " +
                $"{nameof(DialogueBubbleRenderer.ResolveMinComicFontSize)}의 상한이 " +
                $"{nameof(DialogueBubbleRenderer.OutlineLegibleFontFloor)}" +
                "(VerifiedCanvasScale)가 아닌 것으로 바뀌었습니다. macOS가 링에 남는 근거가 그 한 줄입니다.");

            Assert.IsFalse(DialogueBubbleRenderer.UseOutlineRing(floor - 1, Retina),
                $"{LogPrefix} 하한보다 1pt 작은 글자도 링을 고릅니다 — pt 단위 여유가 0이라는 " +
                "이 파일의 주장이 실재하지 않습니다.");
        }

        /// <summary>
        /// ★ <b>리더 경고에 대한 실측 정정</b>(2026-09-02, coder).
        ///
        /// <para>배정문은 <i>"경계가 등식(여유 0pt)이라 <see cref="DialogueBubbleRenderer.TextOutlineEmRatio"/>를
        /// 조금만 내려도 macOS가 조용히 그림자로 넘어간다"</i> 고 적었다. <b>실측하면 그렇게 되지 않는다.</b>
        /// 하한(<see cref="DialogueBubbleRenderer.ResolveMinComicFontSize"/>)과 요구
        /// (<see cref="DialogueBubbleRenderer.OutlineLegibleFontFloor"/>)가 <b>같은 비율에서 유도되고</b>,
        /// 검증 배율에서는 하한의 상한이 곧 그 요구라서 <b>둘이 함께 움직인다</b> — 비율을 반으로
        /// 줄이면 하한이 13 → 25pt로 같이 올라가 여전히 링이다.</para>
        ///
        /// <para>그래서 이 테스트는 "1% 내리면 넘어가는가"(넘어가지 않는다 — 그런 단언을 넣었으면
        /// <b>거짓 빨강</b>이 됐을 것이다)가 아니라, <b>어떤 비율에서도 링이 유지되는가</b>를 스윕으로
        /// 확인한다. 진짜 위험은 비율이 아니라 <b>하한 정책</b>이며, 그쪽은 아래 네거티브 컨트롤이 잡는다.</para>
        /// </summary>
        [Test]
        public void 변이스윕_외곽선_비율을_어떻게_바꿔도_macOS는_링에_남는다()
        {
            float baseRatio = DialogueBubbleRenderer.TextOutlineEmRatio;
            foreach (float k in new[] { 0.50f, 0.80f, 0.90f, 0.95f, 0.99f, 1.05f, 1.20f, 2.00f })
            {
                float mutated = baseRatio * k;
                int need = DialogueBubbleRenderer.OutlineLegibleFontFloor(Retina, mutated);
                // 하한도 <같은 비율>에서 다시 유도된다 — 그것이 이 성질의 이유다.
                int floor = Mathf.Clamp(need, DialogueBubbleRenderer.LegacyComicFontFloor, need);

                Assert.IsTrue(DialogueBubbleRenderer.UseOutlineRing(floor, Retina, mutated),
                    $"{LogPrefix} em 비율 x{k:F2}에서 검증 배율이 그림자로 넘어갔습니다 " +
                    $"(요구 {need}pt / 하한 {floor}pt). 하한과 요구가 같은 식에서 나오지 않게 된 것이며, " +
                    "그 순간 macOS 화면이 배율 하나의 상수 변경으로 바뀝니다.");
            }
        }

        [Test]
        public void 네거티브컨트롤_하한을_옛_가독성_값으로_되돌리면_macOS가_그림자로_넘어간다()
        {
            // 진짜 위험은 em 비율이 아니라 <하한 정책>이다. 하한을 이력상 가독성 값(9pt)으로 되돌리면
            // 검증 배율조차 링을 세우지 못한다 — 이것이 위 두 단언이 공허하지 않다는 증거다.
            int legacy = DialogueBubbleRenderer.LegacyComicFontFloor;
            bool ring = DialogueBubbleRenderer.UseOutlineRing(legacy, Retina);

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 하한을 옛 가독성 값 {legacy}pt로 되돌리면 " +
                      $"검증 배율 분기 {(ring ? "링(변화 없음)" : "그림자(넘어감)")}.");

            Assert.IsFalse(ring,
                $"{LogPrefix} ★ 옛 하한 {legacy}pt로도 검증 배율이 링을 고릅니다. 그렇다면 위 " +
                $"{nameof(검증_배율은_링_분기다_그리고_그것은_구조적_항등이다)}는 <하한 정책과 무관하게 " +
                "언제나 참>이라 아무것도 지키지 못합니다.");
        }

        // ====================================================================
        // (4) ★ 그림자는 C2를 고치지 못한다 — 이 사실을 단언으로 박아 둔다
        // ====================================================================

        [Test]
        public void 그림자로_바꿔도_속공간은_한_톨도_나아지지_않는다()
        {
            foreach (float scale in ShippedScales)
            {
                int font = ShippedFloorFont(scale);

                // 링의 소모 = t x 2(양쪽), 그림자의 소모 = 2t x 1(한쪽). 예산은 <총 소모량>이므로 같다.
                float ringConsumed = font * DialogueBubbleRenderer.TextOutlineEmRatio * 2f;
                float shadowConsumed = font * DialogueBubbleRenderer.TextOutlineEmRatio
                                       * DialogueBubbleRenderer.ShadowBudgetMultiplier;

                Assert.AreEqual(ringConsumed, shadowConsumed, 1e-4f,
                    $"{LogPrefix} 배율 {scale:F2}에서 두 분기의 속공간 소모가 다릅니다 " +
                    $"(링 {ringConsumed:F4}pt / 그림자 {shadowConsumed:F4}pt). 예산은 <총 소모량>으로 " +
                    "정의돼 있습니다 — 두 값이 갈라졌다면 어느 한쪽이 예산을 몰래 늘리거나 줄인 것이고, " +
                    "그 순간 '분기를 바꿔도 속공간은 같다'는 이 라운드의 전제가 무너집니다.");
            }
        }

        [Test]
        public void 아직_못_고친_것은_C2이고_그_유일한_해는_폰트를_더_키우는_것이다()
        {
            float verified = CounterPhysicalPixels(Retina);
            int below = 0;
            foreach (float scale in ShippedScales)
            {
                if (CounterPhysicalPixels(scale) + 1e-4f < verified) below++;
            }

            Debug.Log($"{LogPrefix} C2 현황 — 검증 운용점의 구멍 {verified:F3}물리px 기준, " +
                      $"미달 {below}/{ShippedScales.Length} 배율.");

            // 갭이 사라지는 날 이 단언이 빨개져 "문서를 고쳐라"라고 말한다(갭이 조용히 닫히는 것도 사고다).
            Assert.Greater(below, 0,
                $"{LogPrefix} 다섯 배율 전부가 검증 운용점의 속공간을 확보했습니다 — " +
                $"{nameof(ComicFontFloorOutlineRingTests)}의 C2 보류(Assert.Ignore)를 실단언으로 " +
                "승격하고 UI_SURFACE_SPEC §13.3의 'C2 미달 3칸'을 갱신하세요.");
        }

        // ====================================================================
        // (5) (b′) 스냅 — 하한을 아는 스냅
        // ====================================================================

        [Test]
        public void 스냅은_하한을_절대_깨지_않는다()
        {
            foreach (float scale in ShippedScales)
            {
                int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(scale);
                for (int points = floor; points <= floor + 24; points++)
                {
                    int snapped = DialogueBubbleRenderer.SnapPointsNotBelow(points, floor, scale);
                    Assert.GreaterOrEqual(snapped, floor,
                        $"{LogPrefix} 배율 {scale:F2}에서 {points}pt가 {snapped}pt로 스냅되어 " +
                        $"하한 {floor}pt를 깼습니다 — 하한을 건 바로 그 함수가 스스로 깨는 결함입니다.");
                }
            }
        }

        [Test]
        public void 네거티브컨트롤_하한을_모르는_옛_스냅은_실제로_하한을_깬다()
        {
            int broken = 0;
            foreach (float scale in ShippedScales)
            {
                int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(scale);
                int legacy = UiGlyphScalePolicy.SnapPoints(floor, scale);
                if (legacy < floor)
                {
                    broken++;
                    Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 배율 {scale:F2}: 옛 스냅이 " +
                              $"하한 {floor}pt를 {legacy}pt로 내렸다.");
                }
            }

            Assert.Greater(broken, 0,
                $"{LogPrefix} ★ 옛 스냅이 어느 배율에서도 하한을 깨지 않습니다. 그렇다면 위 " +
                $"{nameof(스냅은_하한을_절대_깨지_않는다)}는 <언제나 참인 단언>이고 (b′)는 고친 것이 " +
                "없습니다. 신고된 배율(Windows 125% / 175%)이 이 목록에 남아 있는지부터 확인하세요.");
        }

        [Test]
        public void 하한이_물지_않는_구간에서는_옛_스냅과_한_톨도_다르지_않다()
        {
            // (b) "위로만 스냅"은 하한과 무관한 큰 폰트까지 부풀린다(배율 1.25에서 25pt → 28pt).
            // (b′)는 그 구간을 건드리지 않는다 — 그것이 (b)를 정정한 이유다.
            int compared = 0;
            foreach (float scale in ShippedScales)
            {
                int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(scale);
                for (int points = floor; points <= 40; points++)
                {
                    int legacy = UiGlyphScalePolicy.SnapPoints(points, scale);
                    if (legacy < floor) continue;   // 여기가 (b′)가 손대는 유일한 구간이다.

                    compared++;
                    Assert.AreEqual(legacy, DialogueBubbleRenderer.SnapPointsNotBelow(points, floor, scale),
                        $"{LogPrefix} 배율 {scale:F2} / {points}pt에서 거동이 달라졌습니다 — (b′)는 " +
                        "하한이 물 때만 위로 밀고 그 밖에서는 지금과 완전히 같아야 합니다. " +
                        "달라졌다면 '위로만 스냅'(기각된 (b))으로 되돌아간 것입니다.");
                }
            }

            Assert.Greater(compared, 50,
                $"{LogPrefix} 비교한 조합이 {compared}개뿐입니다 — 루프가 사실상 아무것도 안 보고 " +
                "초록이 됐습니다(고장 난 스캐너는 언제나 초록입니다).");
        }

        [Test]
        public void 무리수_배율이면_스냅을_포기하되_하한은_지킨다()
        {
            // 잔차 0 후보가 탐색 범위 안에 없는 배율(외장 모니터의 임의 스케일링). 1.37 x pt가
            // 정수가 되려면 pt가 100의 배수여야 한다 — UiGlyphScalePolicy는 그런 배율에서 포기한다.
            const float irrational = 1.37f;
            int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(irrational);

            Assert.GreaterOrEqual(DialogueBubbleRenderer.SnapPointsNotBelow(floor - 5, floor, irrational), floor,
                $"{LogPrefix} 스냅 실패가 하한 위반으로 샜습니다 — (b′)의 핵심이 이 폴백입니다.");

            foreach (float bad in new[] { 0f, float.NaN, float.PositiveInfinity })
            {
                Assert.GreaterOrEqual(DialogueBubbleRenderer.SnapPointsNotBelow(1, floor, bad), floor,
                    $"{LogPrefix} 배율 {bad}(미보고)에서 하한이 무너졌습니다.");
            }
        }

        // ====================================================================
        // (6) ★ 랩 폭 폰트 연동 — 이게 빠지면 블록 높이 상한이 안 닫힌다
        // ====================================================================

        [Test]
        public void 줄당_글자_수가_폰트와_무관하게_보존된다()
        {
            const float bubbleScale = 0.60f;   // 설계 검산이 쓴 캐릭터 배율(가장 불리한 쪽).
            float reference = -1f;

            foreach (float scale in ShippedScales)
            {
                int font = ShippedFloorFont(scale);
                float charsPerLine = DialogueBubbleRenderer.ComicWrapWidthPoints(font, bubbleScale) / font;

                if (reference < 0f) reference = charsPerLine;
                Assert.AreEqual(reference, charsPerLine, 1e-3f,
                    $"{LogPrefix} 배율 {scale:F2}(폰트 {font}pt)에서 줄당 글자 수가 {charsPerLine:F2}로 " +
                    $"달라집니다(기준 {reference:F2}). 랩 폭이 폰트에 안 비례하면 폰트만 커질 때 " +
                    "줄 수가 늘어나 블록 높이가 (줄수비 x 폰트비)로 뜁니다 — 설계 검산으로는 " +
                    "캐릭터 키의 157%가 되어 캐릭터를 삼킵니다.");
            }

            Debug.Log($"{LogPrefix} 랩 폭 연동 — 다섯 배율 전부에서 줄당 {reference:F2}글자 유지.");
        }

        [Test]
        public void 네거티브컨트롤_랩_폭을_폰트에서_떼면_줄당_글자_수가_흔들린다()
        {
            const float bubbleScale = 0.60f;
            int referenceFont = DialogueBubbleRenderer.ResolveMinComicFontSize(Retina);
            // 종전 식(랩 폭이 캐릭터 배율에만 비례) = 기준 폰트에서의 랩 폭으로 고정된 값.
            float legacyWrap = DialogueBubbleRenderer.ComicWrapWidthPoints(referenceFont, bubbleScale);

            float min = float.MaxValue, max = float.MinValue;
            foreach (float scale in ShippedScales)
            {
                float charsPerLine = legacyWrap / ShippedFloorFont(scale);
                min = Mathf.Min(min, charsPerLine);
                max = Mathf.Max(max, charsPerLine);
            }

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 옛 식의 줄당 글자 수 {min:F2} ~ {max:F2} " +
                      $"(폭 {(max - min) / min * 100f:F1}%).");

            Assert.Greater(max - min, 1e-2f,
                $"{LogPrefix} ★ 옛 식으로도 줄당 글자 수가 일정합니다. 그렇다면 위 " +
                $"{nameof(줄당_글자_수가_폰트와_무관하게_보존된다)}는 <언제나 참>이고 랩 폭 연동은 " +
                "고친 것이 없습니다 — 다섯 배율의 하한 폰트가 전부 같아졌는지 확인하세요.");
        }

        [Test]
        public void 검증_운용점의_폰트에서는_랩_폭이_종전_식과_완전히_같다()
        {
            // "macOS 영향 0"의 근거 — 출하 조합(하한이 무는 캐릭터 배율)의 랩 폭이 한 픽셀도 안 바뀐다.
            int referenceFont = DialogueBubbleRenderer.ResolveMinComicFontSize(Retina);
            foreach (float bubbleScale in new[] { 0.35f, 0.60f, 0.75f, 1.00f })
            {
                float legacy = DialogueBubbleRenderer.ComicMaxTextWidth * bubbleScale;   // 종전 식 그대로.
                Assert.AreEqual(legacy, DialogueBubbleRenderer.ComicWrapWidthPoints(referenceFont, bubbleScale),
                    1e-3f,
                    $"{LogPrefix} 캐릭터 배율 {bubbleScale:F2} / 기준 폰트 {referenceFont}pt에서 랩 폭이 " +
                    "종전 식과 다릅니다 — 이 지점에서 항등이 아니면 macOS 화면이 바뀝니다(영향 0이 깨진다).");
            }

            // 네거티브 컨트롤 — 기준 폰트를 벗어나면 실제로 달라져야 한다(안 달라지면 연동이 죽은 것).
            float grown = DialogueBubbleRenderer.ComicWrapWidthPoints(referenceFont + 3, 1f);
            Assert.Greater(grown, DialogueBubbleRenderer.ComicMaxTextWidth + 1e-3f,
                $"{LogPrefix} 폰트를 3pt 키웠는데 랩 폭이 그대로입니다 — 연동이 배선되지 않았습니다.");
        }
    }
}
