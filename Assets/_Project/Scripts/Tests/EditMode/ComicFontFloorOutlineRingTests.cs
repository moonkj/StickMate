using NUnit.Framework;
using StickMate.Dialogue;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 만화 레터링 글자 하한 — 사용자 신고 <b>"텍스트들도 선명하지 않고"</b>의 마지막 조각(2026-09-01).
    ///
    /// ============================================================================
    /// 앞 라운드가 못 고친 것
    /// ============================================================================
    /// 앞 라운드는 뭉갬의 범인을 <b>외곽선</b>으로 확정하고(기울기를 0으로 해도 속공간 열림은
    /// 0.000 -> 0.000, 외곽선만 줄이면 0.000 -> 0.649) <see cref="DialogueBubbleRenderer.TextOutlineEmRatio"/>를
    /// 실측에서 유도했다. 그런데 <b>실행 중인 빌드가 9pt로 그리고 있었다</b>. 리더 검산:
    /// <code>
    ///   링이 보이려면        외곽선 t ≥ 1 물리픽셀
    ///   속공간이 열리려면    t가 em 규칙으로 나와야 한다(고정 하한 0.4pt가 물면 예산 초과)
    ///   합치면               TextOutlineEmRatio × fontSize ≥ 1 물리픽셀
    /// </code>
    /// 9pt(=18물리px)에서 Heavy 한글 속공간은 약 2물리px이고 외곽선이 사방 1px씩 먹으면 0이 된다 —
    /// <b>외곽선을 어떻게 잡아도 9pt에서는 해결되지 않는다.</b> 고칠 수 있는 변수는 글자 크기뿐이다.
    ///
    /// ============================================================================
    /// 이 파일에 숫자가 없는 이유 (CLAUDE.md)
    /// ============================================================================
    /// 12.29도, 13도, 0.5pt도 적지 않는다. 전부 프로덕션 상수
    /// (<see cref="DialogueBubbleRenderer.TextOutlineEmRatio"/>,
    ///  <see cref="DialogueBubbleRenderer.OutlineRingMinPhysicalPixels"/>,
    ///  <see cref="DialogueBubbleRenderer.VerifiedCanvasScale"/>)에서 계산한다.
    /// 그래야 외곽선 비율을 다시 재는 날 이 테스트가 <b>스스로 새 하한을 요구</b>한다.
    ///
    /// ============================================================================
    /// ★ 네거티브 컨트롤을 반드시 동반한다
    /// ============================================================================
    /// 이 저장소는 하루에 "항상 참인 단언"이 두 건 나왔다. 그래서
    /// <see cref="네거티브컨트롤_옛_하한으로_되돌리면_같은_단언이_실패한다"/>가
    /// "하한을 <see cref="DialogueBubbleRenderer.LegacyComicFontFloor"/>로 되돌리면 실제로 빨개진다"를
    /// 증명하고, <see cref="하한은_한_pt도_남기지_않는_최소값이다"/>가 "그냥 큰 숫자를 박은 것이
    /// 아니라 부등식의 <b>정확한 해</b>"임을 증명한다.
    /// </summary>
    public sealed class ComicFontFloorOutlineRingTests
    {
        private const string LogPrefix = "[만화글자하한-TEST]";

        /// <summary>배율 <paramref name="scale"/>에서 링이 채워야 하는 두께(캔버스 유닛 = OS 포인트).</summary>
        private static float RequiredOutlinePoints(float scale)
            => DialogueBubbleRenderer.OutlineRingMinPhysicalPixels
               * DialogueBubbleRenderer.PointsPerPhysicalPixel(scale);

        /// <summary><paramref name="points"/>pt 글자가 실제로 얻는 외곽선 두께(em 규칙).</summary>
        private static float OutlinePointsAt(int points)
            => points * DialogueBubbleRenderer.TextOutlineEmRatio;

        private static float Retina => DialogueBubbleRenderer.VerifiedCanvasScale;

        // ========================================================================
        // (1) 본 단언 — 검증된 배율에서 링이 최소 1 물리픽셀이다
        // ========================================================================

        [Test]
        public void 검증된_배율에서_하한의_외곽선이_최소_한_물리픽셀이다()
        {
            int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(Retina);
            float actual = OutlinePointsAt(floor);
            float required = RequiredOutlinePoints(Retina);

            Debug.Log($"{LogPrefix} 배율 {Retina:F2}: 하한 {floor}pt -> 외곽선 {actual:F4}pt " +
                      $"= {actual * Retina:F3}물리px (요구 {required:F4}pt = " +
                      $"{DialogueBubbleRenderer.OutlineRingMinPhysicalPixels:F1}물리px).");

            Assert.GreaterOrEqual(actual, required,
                $"{LogPrefix} ★ 하한 {floor}pt에서 외곽선이 {actual * Retina:F3}물리픽셀입니다 — " +
                "1픽셀에 못 미치면 GPU가 부분 커버리지로 섞어 <반투명 얼룩>이 되고, 말풍선 도형을 " +
                "없앤 지금 그 링은 글자와 바탕화면 사이의 <유일한> 분리막입니다. " +
                $"처방: {nameof(DialogueBubbleRenderer)}.{nameof(DialogueBubbleRenderer.TextOutlineEmRatio)}를 " +
                "키우거나(속공간 예산과 상충) 하한 유도식을 다시 보세요.");
        }

        // ========================================================================
        // (2) ★ 네거티브 컨트롤 — 옛 하한이면 (1)이 실제로 실패해야 한다
        // ========================================================================

        [Test]
        public void 네거티브컨트롤_옛_하한으로_되돌리면_같은_단언이_실패한다()
        {
            int legacy = DialogueBubbleRenderer.LegacyComicFontFloor;
            float legacyOutline = OutlinePointsAt(legacy);
            float required = RequiredOutlinePoints(Retina);

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 옛 하한 {legacy}pt: 외곽선 {legacyOutline:F4}pt " +
                      $"= {legacyOutline * Retina:F3}물리px (요구 {required * Retina:F1}물리px).");

            Assert.Less(legacyOutline, required,
                $"{LogPrefix} ★ 옛 하한 {legacy}pt가 링 요구를 <통과>했습니다. 그렇다면 위 " +
                $"{nameof(검증된_배율에서_하한의_외곽선이_최소_한_물리픽셀이다)}는 " +
                "<항상 참인 단언>이라 아무것도 지키지 못합니다. " +
                "외곽선 비율이 크게 바뀌었다면 이 컨트롤의 기준(옛 하한)도 함께 갱신하세요.");

            // 컨트롤이 유효하다는 것은 곧 "고친 값이 옛 값보다 실제로 커야 한다"는 뜻이다.
            Assert.Greater(DialogueBubbleRenderer.ResolveMinComicFontSize(Retina), legacy,
                $"{LogPrefix} 하한이 옛 값 그대로입니다 — 신고가 그대로 남습니다.");
        }

        // ========================================================================
        // (3) 유도식의 해인가 — "그냥 큰 숫자"가 아님을 증명한다
        // ========================================================================

        [Test]
        public void 하한은_한_pt도_남기지_않는_최소값이다()
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, Retina, 3f })
            {
                int need = DialogueBubbleRenderer.OutlineLegibleFontFloor(scale);
                float required = RequiredOutlinePoints(scale);

                Assert.GreaterOrEqual(OutlinePointsAt(need), required,
                    $"{LogPrefix} 배율 {scale:F2}의 요구치 {need}pt가 요구를 못 채웁니다 — 유도식이 틀렸습니다.");
                Assert.Less(OutlinePointsAt(need - 1), required,
                    $"{LogPrefix} 배율 {scale:F2}에서 {need - 1}pt로도 요구가 채워집니다 — " +
                    $"{nameof(DialogueBubbleRenderer.OutlineLegibleFontFloor)}가 부등식의 최소해가 아니라 " +
                    "여유를 얹은 값입니다. 여유는 정책이므로 유도식이 아니라 " +
                    $"{nameof(DialogueBubbleRenderer.ResolveMinComicFontSize)}에 두세요.");
            }
        }

        [Test]
        public void 배율이_높을수록_요구치가_작아진다()
        {
            int prev = int.MaxValue;
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, Retina, 3f, 4f })
            {
                int need = DialogueBubbleRenderer.OutlineLegibleFontFloor(scale);
                Assert.LessOrEqual(need, prev,
                    $"{LogPrefix} 배율이 올라갔는데 요구치가 커졌습니다(배율 {scale:F2} -> {need}pt) — " +
                    "1 물리픽셀의 pt 환산이 배율의 역수라는 관계가 깨졌습니다.");
                prev = need;
            }
        }

        [Test]
        public void 배율을_모를_때는_가장_보수적인_요구치를_낸다()
        {
            int unknown = DialogueBubbleRenderer.OutlineLegibleFontFloor(0f);
            Assert.AreEqual(DialogueBubbleRenderer.OutlineLegibleFontFloor(1f), unknown,
                $"{LogPrefix} 배율 0(미보고)에서 요구치가 배율 1과 다릅니다 — 배율을 모르면 " +
                "가장 큰(=가장 안전한) 요구치를 내야 합니다.");
            Assert.AreEqual(unknown, DialogueBubbleRenderer.OutlineLegibleFontFloor(float.NaN),
                $"{LogPrefix} NaN 배율이 다른 값을 냅니다.");
        }

        // ========================================================================
        // (4) 정책 — 하한은 [가독성 하한, 검증된 배율의 요구치] 사이다
        // ========================================================================

        [Test]
        public void 하한은_가독성_하한과_검증된_요구치_사이에_있다()
        {
            int cap = DialogueBubbleRenderer.OutlineLegibleFontFloor(Retina);
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, Retina, 3f, 4f })
            {
                int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(scale);
                Assert.GreaterOrEqual(floor, DialogueBubbleRenderer.LegacyComicFontFloor,
                    $"{LogPrefix} 배율 {scale:F2}에서 하한이 가독성 하한 아래로 내려갔습니다({floor}pt).");
                Assert.LessOrEqual(floor, cap,
                    $"{LogPrefix} 배율 {scale:F2}에서 하한이 검증 범위({cap}pt)를 넘었습니다({floor}pt) — " +
                    "캡처로 판정하지 않은 크기를 사용자에게 내보내게 됩니다.");
            }
        }

        // ========================================================================
        // (5) ★ 아직 못 고친 갭 — 러너에 "건너뜀"으로 계속 보이게 남긴다 (CLAUDE.md)
        // ========================================================================

        [Test]
        public void 배율1에서는_두_요구가_양립하지_않는다_보류()
        {
            float scale = 1f;
            int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(scale);
            int need = DialogueBubbleRenderer.OutlineLegibleFontFloor(scale);
            float got = OutlinePointsAt(floor) * scale;

            if (got + 1e-4f >= DialogueBubbleRenderer.OutlineRingMinPhysicalPixels)
            {
                Assert.Pass($"{LogPrefix} 배율 1에서도 링이 {got:F3}물리px로 요구를 채웁니다 — " +
                            "갭이 해소됐습니다. 이 테스트를 실단언으로 바꾸세요.");
            }

            Assert.Ignore($"{LogPrefix} [보류] 배율 1(Windows 100%·비Retina)에서는 링 요구가 {need}pt를 " +
                          $"부르는데 실제 하한은 {floor}pt라 링이 {got:F3}물리px에 그칩니다. " +
                          $"{need}pt는 배율 0.35 캐릭터(화면상 약 28pt)보다 글자가 커지는 크기라 " +
                          "신고 하나를 고치고 다른 하나를 만드는 교환입니다. " +
                          "사용자 지시(2026-09-01 \"윈도우는 일단 미루고 맥만\")로 보류 — " +
                          "해소하려면 그 환경 전용 폰트 정책(또는 외곽선을 링이 아닌 그림자로 바꾸기)이 필요합니다.");
        }
    }
}
