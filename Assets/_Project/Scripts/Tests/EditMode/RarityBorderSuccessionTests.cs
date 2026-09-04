using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-03 사용자 지시 — *"리본을 저렇게 표시하면 전설인지 일반인지 잘 구분이 안갈거 같음
    /// <b>카드 외곽선을 각 레벨별로 분류하는게 어때</b>"* 의 회귀 잠금.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — <b>세 자리가 서로를 밀어내지 않는가</b>
    /// ============================================================================
    /// 카드 테두리에는 원래 <b>선택 &gt; 호버 &gt; 착용 중 &gt; 기본</b> 넷이 살았다. 거기 등급이
    /// 「기본」 자리를 승계했으므로, 이제 <b>한 줄에 일곱 색</b>이 산다(등급 4 + 상태 3).
    /// 일곱이 서로 안 갈리면 사용자는 <b>지금 뭘 입고 있는지</b>를 잃는다 — 그건 등급을 얻고
    /// 상태를 잃는 거래이고, 그런 거래는 하지 않기로 했다.
    ///
    /// 그래서 여기서 재는 것은 색 하나하나가 아니라 <b>관계</b>다:
    /// <list type="number">
    ///   <item>등급 테두리가 램프의 <b>알파 변형</b>일 뿐인가(신규 hex 0개가 지켜지는가).</item>
    ///   <item>일곱 색 <b>전 쌍</b>이 변별 하한을 넘는가.</item>
    ///   <item>상태 둘(호버·착용)이 <b>등급 최대보다 밝은가</b> — 안 그러면 호버할 때 어두워진다.</item>
    ///   <item>잠긴 카드에서 등급 테두리가 잠김 실루엣 색과 안 부딪히는가.</item>
    /// </list>
    ///
    /// ★ <b>카드 위 등급 낱말과 썸네일 프레임은 여기 없다</b> — 2026-09-03 리더 판정으로 창 골격
    /// 확정 후로 미뤄졌다(좌표가 카드 폭에 매여 있다). 그 라운드로 넘기는 실측은 아래 「5. (보류)」 절에
    /// 적어 두었다.
    ///
    /// ============================================================================
    /// ★ 니들을 쓰지 않는다 / 숫자를 베끼지 않는다
    /// ============================================================================
    /// 기대값은 전부 <b>프로덕션 상수에서 읽어 계산</b>한다. 하한만 이 파일이 들고 있는데,
    /// 그중 하나는 프로덕션에 있는 것을 <b>참조</b>하고(<see cref="UiChrome.MinTextContrast"/>),
    /// 프로덕션에 없는 것 하나(<see cref="DistinctFloor"/>)만 판정 기준으로 적는다 — 그것은 프로덕션 상수의 사본이 아니라 <b>이 테스트의 자</b>다.
    ///
    /// ★ <b>개수 단언을 함께 넣는다.</b> 열거형이 비거나 램프가 줄면 "위반 0건"은 조용히 초록이 된다 —
    /// 이 저장소가 반복해 당한 형태다. 그래서 모든 스윕이 <b>몇 개를 실제로 쟀는지</b>를 함께 단언한다.
    ///
    /// ★ <b>판정기가 살아 있는지 대조한다.</b> 하한을 통과시키는 판정은 «무엇이든 통과시키는 판정»과
    /// 똑같이 생겼다. 그래서 같은 판정기에 <b>일부러 미달인 입력</b>을 물려 빨개지는지 본다.
    ///
    /// ============================================================================
    /// 계산기 — CIELAB(D65) + CIE76 ΔE*ab
    /// ============================================================================
    /// <c>PackPaletteGateTests</c>가 쓰는 것과 <b>같은 식</b>이다(그쪽 private이라 참조할 수 없다).
    /// 두 벌이 갈라지지 않게 <b>같은 교정</b>을 이 파일도 먼저 통과한다 — 흰↔검 = 100 · 동일색 = 0,
    /// 그리고 <c>UiChrome.Accent</c>↔영웅 램프 = <b>12.86</b>(PALETTE_SPEC §12-5가 외부에서 공표한 값).
    /// 교정이 깨지면 아래 모든 판정은 무효다.
    /// </summary>
    public sealed class RarityBorderSuccessionTests
    {
        private const string LogPrefix = "[등급테두리-TEST]";

        /// <summary>두 색이 「다른 색」으로 읽히는 하한(ΔE*ab). PALETTE_SPEC §12-0이 쓰는 자다.
        /// <b>프로덕션 상수의 사본이 아니다</b> — 프로덕션에는 이 값이 없다.</summary>
        private const float DistinctFloor = 7.8f;

        // ============================================================================
        // 0. 교정 — 계산기가 살아 있는가
        // ============================================================================

        [Test]
        public void 교정_계산기가_외부에서_확인_가능한_값을_낸다()
        {
            Assert.AreEqual(0f, DeltaE(Color.white, Color.white), 1e-4f,
                $"{LogPrefix} 동일색 ΔE가 0이 아닙니다 — 계산기가 고장났으므로 아래 판정은 전부 무효입니다.");
            Assert.AreEqual(100f, DeltaE(Color.white, Color.black), 0.01f,
                $"{LogPrefix} 흰↔검 ΔE가 100이 아닙니다.");

            // 외부 공표값 대조 — 브라스 강조색 ↔ 영웅 램프 = 12.86 (PALETTE_SPEC §12-5 / UiChrome 문서).
            float brassVsEpic = DeltaE(UiChrome.Accent, UiChrome.RarityColor(ItemRarity.Epic));
            Assert.AreEqual(12.86f, brassVsEpic, 0.05f,
                $"{LogPrefix} 브라스↔영웅 ΔE가 {brassVsEpic:F2}입니다(공표값 12.86). " +
                "계산기가 다르거나 램프가 움직였습니다 — 어느 쪽이든 아래 판정을 신뢰할 수 없습니다.");

            Debug.Log($"{LogPrefix} 교정 통과 — 동일 0 / 흰검 100 / 브라스↔영웅 {brassVsEpic:F2}.");
        }

        // ============================================================================
        // 1. 등급 테두리는 램프의 「알파 변형」일 뿐이다 (신규 hex 0개)
        // ============================================================================

        [Test]
        public void 등급_테두리는_램프색의_알파만_바꾼_것이다()
        {
            var alphas = new List<float>();
            int judged = 0;

            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color ramp = UiChrome.RarityColor(r);
                Color border = UiChrome.RarityBorder(r);

                Assert.AreEqual(ramp.r, border.r, 1e-6f,
                    $"{LogPrefix} {ItemCatalog.RarityName(r)} 테두리의 R이 램프와 다릅니다 — " +
                    "등급색이 UiChrome 밖에서 새로 만들어졌다는 뜻입니다.");
                Assert.AreEqual(ramp.g, border.g, 1e-6f,
                    $"{LogPrefix} {ItemCatalog.RarityName(r)} 테두리의 G가 램프와 다릅니다.");
                Assert.AreEqual(ramp.b, border.b, 1e-6f,
                    $"{LogPrefix} {ItemCatalog.RarityName(r)} 테두리의 B가 램프와 다릅니다.");

                alphas.Add(border.a);
                judged++;
            }

            // ★ 개수 단언 — 열거형이 비어도 위 루프는 "위반 0건"으로 조용히 초록이 된다.
            int steps = System.Enum.GetValues(typeof(ItemRarity)).Length;
            Assert.AreEqual(steps, judged,
                $"{LogPrefix} {steps}단을 재야 하는데 {judged}단만 쟀습니다.");
            Assert.Greater(judged, 1,
                $"{LogPrefix} 잰 등급이 {judged}단뿐입니다 — 이 판정은 무효입니다.");

            // α는 등급마다 <b>같아야</b> 한다. 갈리면 그 순간 밝기가 등급을 말하기 시작해
            // 램프의 휘도 단조와 두 벌이 된다.
            for (int i = 1; i < alphas.Count; i++)
            {
                Assert.AreEqual(alphas[0], alphas[i], 1e-6f,
                    $"{LogPrefix} 등급 테두리의 α가 갈렸습니다({alphas[0]:F3} vs {alphas[i]:F3}) — " +
                    "α는 하나의 상수여야 합니다.");
            }
            Assert.Greater(alphas[0], 0f, $"{LogPrefix} 등급 테두리가 완전 투명입니다.");
            Assert.LessOrEqual(alphas[0], 1f, $"{LogPrefix} α가 1을 넘습니다.");

            Debug.Log($"{LogPrefix} 등급 {judged}단 전부 램프 RGB 그대로 · α {alphas[0]:F2} 단일.");
        }

        // ============================================================================
        // 2. α 하한 — 인접 등급이 카드면 위에서 갈리는가 (+ 판정기 대조)
        // ============================================================================

        [Test]
        public void 인접_등급_테두리가_카드면_위에서_갈린다()
        {
            float worst = WorstAdjacentDeltaE(UiChrome.RarityBorder, out int pairs);

            Assert.Greater(pairs, 0, $"{LogPrefix} 인접 쌍이 0건입니다 — 이 판정은 무효입니다.");
            Assert.AreEqual(System.Enum.GetValues(typeof(ItemRarity)).Length - 1, pairs,
                $"{LogPrefix} 인접 쌍 수가 등급 단 수와 맞지 않습니다.");
            Assert.GreaterOrEqual(worst, DistinctFloor,
                $"{LogPrefix} 인접 등급 테두리 최소 ΔE {worst:F2} < 하한 {DistinctFloor} — " +
                "옆 카드와 등급이 안 갈립니다. α를 내렸다면 되돌리십시오(하한 α ≈ 0.4640).");

            // ★ 대조 — 같은 판정기에 <b>일부러 미달인 α</b>를 물린다. 여기서 통과하면
            //   위의 "통과"는 무엇이든 통과시키는 빈 조건이라는 뜻이다.
            float weak = WorstAdjacentDeltaE(
                r => { Color c = UiChrome.RarityColor(r); c.a = UiChrome.RarityBorder(r).a * 0.5f; return c; },
                out int weakPairs);
            Assert.AreEqual(pairs, weakPairs, $"{LogPrefix} 대조가 다른 쌍 수를 쟀습니다.");
            Assert.Less(weak, DistinctFloor,
                $"{LogPrefix} ★대조 실패 — α를 절반으로 낮췄는데도 ΔE {weak:F2}로 하한을 넘었습니다. " +
                "판정기가 α에 반응하지 않으므로 위의 통과는 무효입니다.");

            Debug.Log($"{LogPrefix} 인접 {pairs}쌍 최소 ΔE {worst:F2} (하한 {DistinctFloor}) / 대조(α½) {weak:F2}.");
        }

        // ============================================================================
        // 3. 한 줄에 사는 일곱 색 전 쌍이 갈린다
        // ============================================================================

        [Test]
        public void 카드_테두리_일곱색이_서로_변별된다()
        {
            List<(string Name, Color Flat)> palette = BorderPalette();

            int expectedPairs = palette.Count * (palette.Count - 1) / 2;
            float worst = float.MaxValue;
            string worstPair = "—";
            int pairs = 0;

            for (int i = 0; i < palette.Count; i++)
            {
                for (int j = i + 1; j < palette.Count; j++)
                {
                    float d = DeltaE(palette[i].Flat, palette[j].Flat);
                    pairs++;
                    if (d >= worst) continue;
                    worst = d;
                    worstPair = $"{palette[i].Name} ↔ {palette[j].Name}";
                }
            }

            Assert.AreEqual(expectedPairs, pairs,
                $"{LogPrefix} {expectedPairs}쌍을 재야 하는데 {pairs}쌍만 쟀습니다.");
            Assert.Greater(pairs, 0, $"{LogPrefix} 잰 쌍이 0건입니다 — 이 판정은 무효입니다.");
            Assert.GreaterOrEqual(worst, DistinctFloor,
                $"{LogPrefix} 카드 테두리 {pairs}쌍 중 최소 ΔE {worst:F2}({worstPair}) < 하한 {DistinctFloor}. " +
                "등급과 상태가 한 줄에 사는데 둘이 뭉치면 「지금 뭘 입고 있는지」가 죽습니다.");

            Debug.Log($"{LogPrefix} 테두리 {palette.Count}색 / {pairs}쌍 최소 ΔE {worst:F2}({worstPair}).");
        }

        // ============================================================================
        // 4. 호버·착용이 등급 최대보다 <b>밝아야</b> 한다 (안 그러면 호버할 때 어두워진다)
        // ============================================================================

        [Test]
        public void 상태_테두리가_등급_최대보다_밝다()
        {
            float brightestRarity = 0f;
            string brightestName = "—";
            int judged = 0;

            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                float cr = OnCard(UiChrome.RarityBorder(r));
                judged++;
                if (cr <= brightestRarity) continue;
                brightestRarity = cr;
                brightestName = ItemCatalog.RarityName(r);
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(ItemRarity)).Length, judged,
                $"{LogPrefix} 등급 전수를 재지 못했습니다({judged}단).");
            Assert.Greater(brightestRarity, 1f,
                $"{LogPrefix} 등급 테두리가 카드면과 구분되지 않습니다 — 이 판정은 무효입니다.");

            foreach ((string name, Color color) in new[]
                     {
                         ("호버", UiChrome.CardBorderHover),
                         ("착용 중", UiChrome.CardBorderWorn),
                         ("선택", UiChrome.TextPrimary),
                     })
            {
                float cr = OnCard(color);
                Assert.Greater(cr, brightestRarity,
                    $"{LogPrefix} [{name}] 테두리 대비 {cr:F2}가 가장 밝은 등급({brightestName} {brightestRarity:F2})보다 " +
                    "어둡습니다 — 상태를 얹었는데 테두리가 <b>어두워지는</b> 역전입니다.");
            }

            Debug.Log($"{LogPrefix} 등급 최대 {brightestName} {brightestRarity:F2} < 착용 " +
                      $"{OnCard(UiChrome.CardBorderWorn):F2} < 호버 {OnCard(UiChrome.CardBorderHover):F2} " +
                      $"< 선택 {OnCard(UiChrome.TextPrimary):F2}.");
        }

        // ============================================================================
        // 5. (보류) 카드 위 등급 <b>낱말</b> · 썸네일 등급 프레임
        // ============================================================================
        //
        // ★ 2026-09-03 리더 판정으로 <b>골격 확정 후로 미뤘다</b>. 두 자리 모두 좌표·크기가
        //   카드 폭에 매여 있고, 창 1042 안의 3열 골격이 아직 안 정해졌다.
        //
        // ★★ <b>그 라운드로 넘기는 실측 하나 — 설계 대비표가 배경 하나를 빠뜨렸다.</b>
        //   UI_SURFACE_SPEC §15.14-d의 낱말 대비표는 배경을 셋(카드면 5.68 / 잠김 카드면 6.11 /
        //   잠긴 썸네일 6.40~13.14)만 실었는데, 실제 썸네일 색은 <b>넷</b>이다 —
        //   착용 중에는 <c>Flatten(AccentSurface, CardSurface)</c> = <c>#33312D</c>로 밝아진다.
        //   그 위에서 가장 어두운 단(일반 <c>#9C978C</c>)은 <b>4.44:1</b>로 텍스트 하한
        //   <see cref="UiChrome.MinTextContrast"/>에 <b>1.2% 미달</b>이다(희귀 5.79 / 영웅 7.37 / 전설 9.13).
        //   <b>낱말을 썸네일 안에 앉히는 안은 이 배경을 계산에 넣어야 한다.</b>
        //   그 자리가 확정되면 여기에 스윕 테스트를 넣는다 — 지금 넣으면 없는 표면을 재게 된다.

        // ============================================================================
        // 7. 잠긴 카드에서 등급 테두리가 잠김 실루엣과 안 부딪힌다
        // ============================================================================

        [Test]
        public void 잠긴_카드_테두리가_잠김_실루엣과_안_부딪힌다()
        {
            float worst = float.MaxValue;
            string worstName = "—";
            int judged = 0;

            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color flat = UiChrome.Flatten(UiChrome.RarityBorder(r), UiChrome.CardSurfaceMuted);
                float d = DeltaE(flat, UiChrome.TextTertiary);
                judged++;
                if (d >= worst) continue;
                worst = d;
                worstName = ItemCatalog.RarityName(r);
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(ItemRarity)).Length, judged,
                $"{LogPrefix} 등급 전수를 재지 못했습니다({judged}단).");
            Assert.GreaterOrEqual(worst, DistinctFloor,
                $"{LogPrefix} 잠긴 카드 테두리({worstName})가 잠김 실루엣 색과 ΔE {worst:F2}로 붙었습니다 — " +
                "\"가진 일반 아이템이 못 얻은 것처럼 보인다\"가 재현됩니다.");

            Debug.Log($"{LogPrefix} 잠긴 테두리 ↔ 잠김 실루엣 최소 ΔE {worst:F2}({worstName}).");
        }

        // ============================================================================
        // 판정 보조 — 본안과 대조가 <b>같은 함수</b>를 쓴다
        // ============================================================================

        /// <summary>카드면 위에 평탄화한 「한 줄에 사는 일곱 색」. 이름은 로그용이다.</summary>
        private static List<(string Name, Color Flat)> BorderPalette()
        {
            var list = new List<(string, Color)>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                list.Add(($"등급 {ItemCatalog.RarityName(r)}",
                    UiChrome.Flatten(UiChrome.RarityBorder(r), UiChrome.CardSurface)));
            }
            list.Add(("호버", UiChrome.Flatten(UiChrome.CardBorderHover, UiChrome.CardSurface)));
            list.Add(("착용 중", UiChrome.Flatten(UiChrome.CardBorderWorn, UiChrome.CardSurface)));
            list.Add(("선택", UiChrome.Flatten(UiChrome.TextPrimary, UiChrome.CardSurface)));
            return list;
        }

        /// <summary>인접 등급끼리의 최소 ΔE. <paramref name="pick"/>을 바꿔 대조에도 같은 식을 쓴다.</summary>
        private static float WorstAdjacentDeltaE(System.Func<ItemRarity, Color> pick, out int pairs)
        {
            var flats = new List<Color>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                flats.Add(UiChrome.Flatten(pick(r), UiChrome.CardSurface));
            }

            pairs = 0;
            float worst = float.MaxValue;
            for (int i = 1; i < flats.Count; i++)
            {
                worst = Mathf.Min(worst, DeltaE(flats[i - 1], flats[i]));
                pairs++;
            }
            return pairs == 0 ? 0f : worst;
        }

        private static float OnCard(Color border)
            => UiChrome.ContrastRatio(UiChrome.Flatten(border, UiChrome.CardSurface), UiChrome.CardSurface);

        // ---- CIELAB(D65) + CIE76 ----

        private static readonly Vector3 D65White = new Vector3(0.95047f, 1.00000f, 1.08883f);

        private static Vector3 Lab(Color c)
        {
            float r = Linear(c.r), g = Linear(c.g), b = Linear(c.b);
            float x = 0.4124564f * r + 0.3575761f * g + 0.1804375f * b;
            float y = 0.2126729f * r + 0.7151522f * g + 0.0721750f * b;
            float z = 0.0193339f * r + 0.1191920f * g + 0.9503041f * b;

            float fx = LabF(x / D65White.x), fy = LabF(y / D65White.y), fz = LabF(z / D65White.z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        private static float LabF(float t)
            => t > 216f / 24389f ? Mathf.Pow(t, 1f / 3f) : 841f / 108f * t + 4f / 29f;

        private static float Linear(float srgb)
        {
            srgb = Mathf.Clamp01(srgb);
            return srgb <= 0.04045f ? srgb / 12.92f : Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
        }

        private static float DeltaE(Color a, Color b) => Vector3.Distance(Lab(a), Lab(b));
    }
}
