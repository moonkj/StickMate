using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 2026-09-06 — 초상화 무대 조명의 <b>알파 구멍</b>을 잠근다.
    ///
    /// ============================================================================
    /// 무엇을 재는가
    /// ============================================================================
    /// 이 앱의 창은 전체화면 투명 오버레이라 프레임버퍼의 알파가 곧 OS 합성기의 마스크다.
    /// uGUI의 <c>Blend SrcAlpha OneMinusSrcAlpha</c>는 알파 채널에도 똑같이 적용되므로,
    /// 불투명한 면(dstA = 1) 위에 α의 겹을 얹으면
    /// <code>
    ///   dstA' = srcA² + dstA(1 − srcA) = 1 − α(1 − α)
    /// </code>
    /// 가 되어 <b>바탕화면이 그만큼 비친다</b>. 안전한 값은 <b>0과 1 뿐</b>이고 α = 0.5가 최악이다.
    ///
    /// 무대의 두 겹은 이 법칙에 정면으로 걸려 있었다(design-art R13 「N-1」 실측):
    /// <list type="bullet">
    ///   <item><c>StageFloorGlow</c> = <see cref="UiChrome.AccentSurface"/>(α0.14) → 비침 <b>12.04 %</b></item>
    ///   <item><c>StageSheen</c> = <see cref="UiChrome.PanelSheen"/>(α0.10) → 비침 <b>9.00 %</b></item>
    /// </list>
    ///
    /// 처방은 <c>design-art</c>가 등급 글로우에 못박은 것과 <b>같은 기법</b>이다
    /// (docs/DESIGN_RARITY_GLOW.md §5-1 / docs/UX_CHARACTER_WINDOW_REFINE.md §4-4-0):
    /// <b>알파 램프를 얹지 않고 바탕의 RGB에 램프를 굽는다.</b> 그 구현이
    /// <see cref="CharacterPortraitStage.BackdropGlowColorAt"/>이고, 이 파일은 그 함수가
    /// <b>어떤 입력에서도 알파 1을 돌려준다</b>는 사실을 재는 것이 첫 번째 임무다.
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는다
    /// ============================================================================
    /// 색·세기·기하는 전부 프로덕션 심볼에서 읽고(<see cref="UiChrome.Accent"/>,
    /// <see cref="CharacterPortraitStage.BackdropGlowPeak"/> 등), 액자 크기는 <b>실제로 만든
    /// 촬영장 카메라</b>에서 잰다. 이 파일에 상수 리터럴은 하나도 없다.
    /// </summary>
    public sealed class PortraitBackdropGlowAlphaTests
    {
        private const string LogPrefix = "[무대광원-TEST]";

        /// <summary>「알파 채널의 법칙」 — 불투명 면(dstA = 1) 위에 α를 얹었을 때의 바탕화면 비침률.
        /// <para>식을 여기 한 번만 적는다. 이 식이 이 파일의 유일한 판정 기준이다.</para></summary>
        private static float ShowThrough01(float srcAlpha)
        {
            float dst = srcAlpha * srcAlpha + 1f * (1f - srcAlpha);
            return 1f - dst;
        }

        private static Color Paper => UiChrome.PortraitSurface;

        /// <summary>흰 잉크용 목탄 바탕 — 값을 베끼지 않고 프로덕션 분기에서 받아 온다.</summary>
        private static Color CharcoalOrPaper(bool whiteInk)
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                config.SetRuntimeInkColor(whiteInk ? StickmanInkColor.White : StickmanInkColor.Black);
                return CharacterPortraitStage.ResolveBackdropColor(config);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        // ============================================================================
        // (1) 핵심 — 알파는 어디에서도 1 미만이 되지 않는다
        // ============================================================================

        [Test]
        public void 램프_전_구간에서_알파가_정확히_1이다()
        {
            foreach (bool whiteInk in new[] { false, true })
            {
                Color backdrop = CharcoalOrPaper(whiteInk);
                for (int step = -4; step <= 24; step++)
                {
                    float t = step / 20f;                       // −0.20 … 1.20 (바깥/안쪽 모두)
                    Color c = CharacterPortraitStage.BackdropGlowColorAt(t, backdrop);
                    Assert.AreEqual(1f, c.a, 0f,
                        $"{LogPrefix} t={t:F2}(잉크 흰색={whiteInk})에서 알파가 {c.a:F4}입니다. " +
                        $"이 앱에서 그 값은 바탕화면 비침 {ShowThrough01(c.a) * 100f:F2}%를 뜻합니다 — " +
                        "램프를 다시 알파에 굽는 회귀입니다(docs/UX_CHARACTER_WINDOW_REFINE.md §4-4-0).");
                }

                Assert.AreEqual(1f, CharacterPortraitStage.BackdropGlowColorAt(float.NaN, backdrop).a, 0f,
                    $"{LogPrefix} 거리가 NaN일 때도 알파는 1이어야 합니다(NaN은 바깥으로 떨어집니다).");
            }
        }

        [Test]
        public void 바탕색이_반투명하게_들어와도_알파를_1로_되돌린다()
        {
            // ★ 네거티브 컨트롤의 반대편 — 입력이 오염돼도 출력 알파는 무조건 1이다.
            //   이 성질이 없으면 "바탕색 토큰의 알파가 언젠가 1이 아니게 되는" 경로로 구멍이 되살아난다.
            Color leaky = new Color(Paper.r, Paper.g, Paper.b, UiChrome.AccentSurface.a);
            for (int step = 0; step <= 20; step++)
            {
                Color c = CharacterPortraitStage.BackdropGlowColorAt(step / 20f, leaky);
                Assert.AreEqual(1f, c.a, 0f,
                    $"{LogPrefix} 반투명 바탕(α{leaky.a:F2})을 넣었더니 결과 알파가 {c.a:F4}가 됐습니다.");
            }
        }

        // ============================================================================
        // (2) 대조 — 옛 두 겹이 <b>실제로</b> 구멍이었다는 사실을 같은 자로 잰다
        // ============================================================================
        //
        // CLAUDE.md: 부재 단언은 썩으면 <b>조용히 초록</b>이 된다. 그래서 "지금은 알파가 1이다"만
        // 재고 끝내지 않는다 — 비교 대상인 두 토큰이 <b>여전히 α < 1</b>이라는 사실을 같은
        // 테스트 안에서 못박아, 이 검사가 겨누는 위험이 실재함을 증명한다.

        [Test]
        public void 대조_옛_두_겹의_토큰은_여전히_반투명하고_그것이_구멍을_낸다()
        {
            Assert.Less(UiChrome.PanelSheen.a, 1f,
                $"{LogPrefix} {nameof(UiChrome.PanelSheen)}가 더 이상 반투명이 아닙니다 — " +
                "그렇다면 이 대조는 아무것도 재지 못합니다. 검사 대상을 다시 정하십시오.");
            Assert.Less(UiChrome.AccentSurface.a, 1f,
                $"{LogPrefix} {nameof(UiChrome.AccentSurface)}가 더 이상 반투명이 아닙니다 — 위와 같은 이유.");

            float sheenLeak = ShowThrough01(UiChrome.PanelSheen.a);
            float glowLeak = ShowThrough01(UiChrome.AccentSurface.a);
            Assert.Greater(sheenLeak, 0f, $"{LogPrefix} 시인 겹의 비침이 0으로 계산됐습니다 — 식이 틀렸습니다.");
            Assert.Greater(glowLeak, sheenLeak,
                $"{LogPrefix} 바닥 광원(α{UiChrome.AccentSurface.a:F2})이 시인(α{UiChrome.PanelSheen.a:F2})보다 " +
                "덜 새는 것으로 계산됐습니다 — 알파 법칙의 방향이 뒤집혔습니다.");

            Assert.AreEqual(0f, ShowThrough01(1f), 0f,
                $"{LogPrefix} α=1의 비침이 0이 아닙니다 — 이 식으로는 아무것도 판정할 수 없습니다.");
            Assert.AreEqual(0f, ShowThrough01(0f), 0f,
                $"{LogPrefix} α=0의 비침이 0이 아닙니다 — 안전한 값은 0과 1 <b>둘</b>입니다.");

            Debug.Log($"{LogPrefix} 대조 실측 — 시인 α{UiChrome.PanelSheen.a:F2} → 비침 {sheenLeak * 100f:F2}% / " +
                $"바닥광원 α{UiChrome.AccentSurface.a:F2} → 비침 {glowLeak * 100f:F2}% / " +
                "구운 램프 α1.00 → 비침 0.00%.");
        }

        // ============================================================================
        // (3) 시각 효과가 보존되는가 — 색이 실제로 브라스 쪽으로 간다
        // ============================================================================

        [Test]
        public void 중심색이_브라스_합성색과_정확히_같다()
        {
            foreach (bool whiteInk in new[] { false, true })
            {
                Color backdrop = CharcoalOrPaper(whiteInk);
                Color tint = UiChrome.Accent;
                tint.a = CharacterPortraitStage.BackdropGlowPeak;
                Color expected = UiChrome.Flatten(tint, backdrop);

                Color actual = CharacterPortraitStage.BackdropGlowColorAt(0f, backdrop);
                Assert.AreEqual(expected.r, actual.r, 1e-5f, $"{LogPrefix} 중심 R(잉크 흰색={whiteInk})");
                Assert.AreEqual(expected.g, actual.g, 1e-5f, $"{LogPrefix} 중심 G(잉크 흰색={whiteInk})");
                Assert.AreEqual(expected.b, actual.b, 1e-5f, $"{LogPrefix} 중심 B(잉크 흰색={whiteInk})");

                // 그리고 그 색이 <b>실제로 바탕과 다르다</b> — 안 그러면 "구웠지만 안 보인다"다.
                float delta = Mathf.Abs(actual.r - backdrop.r) + Mathf.Abs(actual.g - backdrop.g)
                    + Mathf.Abs(actual.b - backdrop.b);
                Assert.Greater(delta, 0.05f,
                    $"{LogPrefix} 중심색이 바탕색과 사실상 같습니다(채널 합 차 {delta:F4}, 잉크 흰색={whiteInk}). " +
                    "알파 구멍은 막았지만 조명이 사라진 상태입니다 — 세기 " +
                    $"{nameof(CharacterPortraitStage.BackdropGlowPeak)}를 확인하십시오.");
            }
        }

        [Test]
        public void 가장자리에서_바탕색과_잔차가_0이다()
        {
            // 램프 사각형 바깥은 카메라 클리어 색이다. t=1에서 잔차가 남으면 그 경계가
            // <b>원반 테두리</b>로 보인다(UiChrome.RadialGlow가 제곱 감쇠를 쓰는 이유와 같은 문제).
            foreach (bool whiteInk in new[] { false, true })
            {
                Color backdrop = CharcoalOrPaper(whiteInk);
                foreach (float t in new[] { 1f, 1.5f, 4f })
                {
                    Color edge = CharacterPortraitStage.BackdropGlowColorAt(t, backdrop);
                    Assert.AreEqual(backdrop.r, edge.r, 0f, $"{LogPrefix} t={t} R 잔차(잉크 흰색={whiteInk})");
                    Assert.AreEqual(backdrop.g, edge.g, 0f, $"{LogPrefix} t={t} G 잔차");
                    Assert.AreEqual(backdrop.b, edge.b, 0f, $"{LogPrefix} t={t} B 잔차");
                }
            }
        }

        [Test]
        public void 감쇠가_중심에서_가장자리로_단조적으로_사라진다()
        {
            Color backdrop = Paper;
            Color core = CharacterPortraitStage.BackdropGlowCoreColor(backdrop);
            float previous = float.MaxValue;

            for (int step = 0; step <= 20; step++)
            {
                float t = step / 20f;
                Color c = CharacterPortraitStage.BackdropGlowColorAt(t, backdrop);
                // 중심색까지의 거리 — 0이면 중심, 클수록 바탕에 가깝다.
                float mix = Mathf.Abs(c.r - backdrop.r) + Mathf.Abs(c.g - backdrop.g) + Mathf.Abs(c.b - backdrop.b);
                Assert.LessOrEqual(mix, previous + 1e-6f,
                    $"{LogPrefix} t={t:F2}에서 램프가 다시 밝아졌습니다 — 감쇠가 단조가 아닙니다.");
                previous = mix;
            }

            float centerMix = Mathf.Abs(core.r - backdrop.r) + Mathf.Abs(core.g - backdrop.g)
                + Mathf.Abs(core.b - backdrop.b);
            Assert.Greater(centerMix, 0f, $"{LogPrefix} 중심과 바탕이 같은 색입니다 — 램프가 없습니다.");
            Assert.AreEqual(0f, previous, 1e-6f, $"{LogPrefix} 가장자리에서 램프가 0으로 떨어지지 않았습니다.");
        }

        // ============================================================================
        // (4) 기하 — 램프가 액자 안에 들어가는가
        // ============================================================================

        /// <summary>
        /// 램프가 액자를 넘으면 <b>잘린 원반</b>이 보인다. 액자 크기는 상수를 베끼지 않고
        /// <b>실제로 만든 촬영장 카메라</b>에서 잰다 — 액자 식(<c>FrameOrthoRatio</c> 등)은
        /// 계산식 상수라 소스에서 읽을 수도 없고, 읽어 봐야 식을 두 벌 만들 뿐이다.
        /// </summary>
        [Test]
        public void 램프가_액자_가시_사각형_안에_들어간다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            CharacterPortraitStage stage = CharacterPortraitStage.Create(config, null, null);
            try
            {
                Camera cam = stage.GetComponentInChildren<Camera>(true);
                Assert.IsNotNull(cam, $"{LogPrefix} 촬영장 카메라를 찾지 못했습니다 — 이 검사는 아무것도 재지 못합니다.");

                // metrics가 null이면 TotalHeight는 이 기준 키로 떨어진다(프로덕션 폴백 그대로).
                float h = StickConfig.BaselineCharacterTotalHeight;
                float halfY = cam.orthographicSize;
                float halfX = halfY * cam.aspect;
                float frameCenterY = cam.transform.localPosition.y;

                Assert.Greater(halfY, 0f, $"{LogPrefix} 액자 반높이가 0입니다.");
                Assert.Greater(halfX, 0f, $"{LogPrefix} 액자 반폭이 0입니다(종횡비를 못 읽었습니다).");

                float radius = h * CharacterPortraitStage.BackdropGlowDiameterInHeight * 0.5f;
                float glowCenterY = h * CharacterPortraitStage.BackdropGlowCenterHeightInHeight;

                float bottomMargin = (glowCenterY - radius) - (frameCenterY - halfY);
                float topMargin = (frameCenterY + halfY) - (glowCenterY + radius);
                float sideMargin = halfX - radius;

                Debug.Log($"{LogPrefix} 액자 여백(키 배수) — 아래 {bottomMargin / h:F4} / 위 {topMargin / h:F4} / " +
                    $"좌우 {sideMargin / h:F4}. 램프 반지름 {radius / h:F4}H, 중심 {glowCenterY / h:F4}H.");

                Assert.GreaterOrEqual(bottomMargin, 0f,
                    $"{LogPrefix} 램프 아래쪽이 액자 밖으로 {-bottomMargin / h:F4}H 넘습니다 — 바닥에서 원반이 잘려 보입니다. " +
                    $"{nameof(CharacterPortraitStage.BackdropGlowCenterHeightInHeight)}를 올리거나 " +
                    $"{nameof(CharacterPortraitStage.BackdropGlowDiameterInHeight)}를 줄이십시오.");
                Assert.GreaterOrEqual(topMargin, 0f,
                    $"{LogPrefix} 램프 위쪽이 액자 밖으로 {-topMargin / h:F4}H 넘습니다.");
                Assert.GreaterOrEqual(sideMargin, 0f,
                    $"{LogPrefix} 램프 좌우가 액자 밖으로 {-sideMargin / h:F4}H 넘습니다.");
            }
            finally
            {
                if (stage != null) Object.DestroyImmediate(stage.gameObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void 격자_분할수가_짝수여야_중심_꼭짓점이_생긴다()
        {
            // (1−t)²는 중심에서 미분이 불연속이다(원뿔 꼭짓점). 홀수 분할이면 그 자리에 정점이
            // 없어 꼭대기가 깎이고, 그러면 중심색 검사(위)는 통과하는데 화면에서는 더 흐리다.
            float segments = SourceConstantReaderSegments();
            Assert.AreEqual(0f, segments % 2f, 0f,
                $"{LogPrefix} 격자 분할수가 {segments}(홀수)입니다 — 램프 중심에 정점이 없어 꼭대기가 깎입니다.");
            Assert.GreaterOrEqual(segments, 8f,
                $"{LogPrefix} 격자 분할수가 {segments}로 너무 작습니다 — 격자면이 눈에 보입니다.");
        }

        private static float SourceConstantReaderSegments()
        {
            // private const라 리플렉션이 닿지 않는다 — 이 저장소의 관례대로 소스에서 읽는다.
            string path = SourceConstantReader.PortraitStagePath;
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");

            // 정수 상수라 SourceConstantReader.TryReadFloat(끝의 f를 요구한다)로는 못 읽는다.
            var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path),
                @"\bBackdropGlowGridSegments\s*=\s*(\d+)\s*;");
            Assert.IsTrue(match.Success,
                $"{LogPrefix} 'BackdropGlowGridSegments' 상수를 소스에서 찾지 못했습니다 — 이름이 바뀌었다면 " +
                "이 검사도 함께 갱신하십시오. 그 전까지 이 검사는 대상 없이 돕니다.");
            return float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ============================================================================
        // (5) ★ 아직 남은 갭 — 캔버스 쪽 두 겹(인계표 D1 / D2)
        // ============================================================================

        /// <summary>
        /// 촬영장 안은 고쳤지만 <b>정보창 캔버스의 두 겹은 아직 살아 있다</b>. 그 파일은
        /// 동시 편집 중이라 이번 라운드에서 손대지 못했다(리더 파일 분배).
        ///
        /// <para>CLAUDE.md 관례대로 <c>Assert.Fail</c>이 아니라 <see cref="Assert.Ignore(string)"/>로
        /// 남긴다 — 러너에 <b>「건너뜀」으로 계속 보여</b> 잊히지 않게 하고, D1/D2가 착지하는 순간
        /// 이 검사는 <b>자동으로 초록이 된다</b>.</para>
        ///
        /// <para>니들이 썩어 조용히 초록이 되지 않도록 토큰 이름은 <c>nameof</c>로 만든다 —
        /// 팔레트 토큰의 이름이 바뀌면 이 파일은 <b>컴파일이 깨진다</b>.</para>
        /// </summary>
        [Test]
        public void 정보창_캔버스의_raw_알파_두_겹이_사라졌는가()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction",
                "CharacterInfoWindow.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 정보창 소스를 찾지 못했습니다: {path}");

            string source = SourceConstantReader.ReadSurfaceText(path);
            Assert.Greater(source.Length, 10000,
                $"{LogPrefix} 정보창 표면을 {source.Length}자밖에 읽지 못했습니다 — 이 검사는 아무것도 " +
                "보지 못한 채 초록이 될 참이었습니다(경로/부분클래스 규칙을 확인하십시오).");

            string rawSheen = "= UiChrome." + nameof(UiChrome.PanelSheen) + ";";
            string rawGlow = "= UiChrome." + nameof(UiChrome.AccentSurface) + ";";

            bool sheenAlive = source.Contains(rawSheen);
            bool glowAlive = source.Contains(rawGlow);
            if (!sheenAlive && !glowAlive)
            {
                Debug.Log($"{LogPrefix} 정보창 캔버스의 raw 알파 두 겹이 사라졌습니다 — D1/D2 착지 확인. " +
                    "이제 무대의 바탕화면 비침은 0.00%입니다.");
                return;
            }

            float sheenLeak = ShowThrough01(UiChrome.PanelSheen.a) * 100f;
            float glowLeak = ShowThrough01(UiChrome.AccentSurface.a) * 100f;
            Assert.Ignore($"{LogPrefix} 아직 열려 있습니다 — CharacterInfoWindow.BuildColumn1의 " +
                (sheenAlive ? $"StageSheen({rawSheen} → 비침 {sheenLeak:F2}%) " : string.Empty) +
                (glowAlive ? $"StageFloorGlow({rawGlow} → 비침 {glowLeak:F2}%) " : string.Empty) +
                "가 남아 있습니다. 촬영장(RenderTexture) 안 조명은 이번 라운드에 알파 1.0으로 옮겼고" +
                "(CharacterPortraitStage.BackdropGlowColorAt), 이 두 겹은 액자 테두리 8pt 띠에서만 " +
                "보이므로 지워도 잃는 그림이 없습니다. 인계표 D1/D2 " +
                "— docs/UX_CHARACTER_WINDOW_REFINE.md §12-4. 그 파일은 동시 편집 중이라 리더가 " +
                "파일을 배분한 뒤에 적용해야 합니다.");
        }
    }
}
