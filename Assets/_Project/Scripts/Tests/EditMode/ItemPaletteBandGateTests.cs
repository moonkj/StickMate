using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 아이템 팔레트 <b>자립 대역 게이트</b> — 프로덕션 애셋(<c>Resources/Items/*.asset</c>)을 직접
    /// 읽어 "이 색이 <b>몸에 칠해진 뒤에도</b> 네 배경 전부에서 보이는가"를 잠근다.
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가 — 이 결함은 이미 출하돼 있었다
    /// ============================================================================
    /// 2026-09-02 실측: 출하 27색 중 <b>20색이 <see cref="ItemCatalog.WornColor"/>를 통과한 뒤에도</b>
    /// 대비 하한 3.0을 못 넘었다(최악 <c>#FFF0B8</c> -> 몸 <c>#CCBA76</c> = <b>1.60:1</b>, 종이 무대).
    /// 옛 값들이 "34-1 다크 카드 위에서 읽히도록" 명도를 올려 잡은 값이었기 때문이다 —
    /// 그런데 이 색들이 실제로 놓이는 배경은 카드만이 아니다.
    ///
    ///  · 밝은 바탕화면(극단: 흰색)       · 어두운 바탕화면(극단: 검은색)
    ///  · 종이 무대 <c>#E9EAE6</c>         · 목탄 무대 <c>#25282E</c>
    ///
    /// ★ <b>구속하는 것은 극단이 아니라 두 무대다.</b> 대비는 자기 휘도에 가까운 배경에서 0으로
    /// 가므로 <b>중간 밝기 배경이 흑백 극단보다 어렵다</b>. design-art가 1차 유도에서 흑백만 보고
    /// <c>L ∈ [0.10, 0.30]</c>을 냈다가, 그 대역에서 고른 색(<c>#7690CC</c>)이 종이 무대에서
    /// <b>2.62:1</b>로 미달하는 것을 스스로 발견했다. 그 실수를 아래 두 양성 대조에 박제해 둔다.
    /// (design-art의 산문은 이 값을 2.48로 적었지만 그쪽 도구의 출력도 2.62다 — 실측을 따른다.)
    ///
    /// ============================================================================
    /// 거짓 통과 방지 (TEAM.md §4)
    /// ============================================================================
    ///  · <b>계산기를 먼저 교정한다</b>(흰/검 21.0 · 동일색 1.0 · #767676/흰 4.5422).
    ///    교정이 깨지면 이 파일이 내는 <b>모든 초록을 폐기</b>해야 한다.
    ///  · <b>모든 "없음" 판정에 양성 대조.</b> 일부러 나쁜 색을 <b>본안과 같은 판정 함수</b>에 넣어
    ///    빨간불이 실제로 나오는지 본다(대조용 판정기를 따로 짜면 그건 대조가 아니다).
    ///  · <b>빈 목록이 초록이 되지 않게</b> 잰 항목 수를 <see cref="ItemCatalog.EquipmentCount"/>와
    ///    맞춘다. 숫자를 베끼지 않고 카탈로그 자신에게 묻는다.
    ///  · 임계값·표면색을 <b>숫자로 베끼지 않는다</b> — <see cref="UiChrome.MinNonTextContrast"/>,
    ///    <see cref="CharacterPortraitStage.ResolveBackdropColor"/>를 그대로 읽는다.
    /// </summary>
    public sealed class ItemPaletteBandGateTests
    {
        private const string LogPrefix = "[팔레트 대역]";

        // ============================================================================
        // 0. 계산기 교정 — 이게 깨지면 아래 숫자는 전부 무효다
        // ============================================================================

        [Test]
        public void 대비_계산기가_알려진_값을_낸다()
        {
            AssertCalculatorCalibrated();
        }

        private static void AssertCalculatorCalibrated()
        {
            Assert.AreEqual(21.0f, UiChrome.ContrastRatio(Color.white, Color.black), 0.0005f,
                $"{LogPrefix} 흰/검 대비가 21.0이 아닙니다 — 계산기가 고장났으므로 이 파일의 " +
                "모든 판정을 폐기하십시오.");
            Assert.AreEqual(1.0f, UiChrome.ContrastRatio(Color.white, Color.white), 0.0005f,
                $"{LogPrefix} 동일색(흰) 대비가 1.0이 아닙니다.");
            Assert.AreEqual(1.0f, UiChrome.ContrastRatio(UiChrome.CardSurface, UiChrome.CardSurface), 0.0005f,
                $"{LogPrefix} 동일색(카드 바탕) 대비가 1.0이 아닙니다.");
            Assert.AreEqual(4.5422f, UiChrome.ContrastRatio(Hex(0x767676), Color.white), 0.0005f,
                $"{LogPrefix} #767676 / 흰색 대비가 WCAG 기준값 4.5422가 아닙니다.");
        }

        // ============================================================================
        // 1. 배경 넷 — 값을 적지 않고 프로덕션에서 꺼낸다
        // ============================================================================

        private static (string Name, Color Color)[] _backdrops;

        /// <summary>장비 색이 실제로 놓이는 배경 넷. 바탕화면 두 극단은 "임의의 바탕화면"의
        /// 상계/하계라 상수가 아니라 정의고, 두 무대 색은 프로덕션이 정한다.</summary>
        private static (string Name, Color Color)[] Backdrops()
        {
            if (_backdrops != null) return _backdrops;

            var blackInk = ScriptableObject.CreateInstance<StickConfig>();
            blackInk.SetRuntimeInkColor(StickmanInkColor.Black);
            var whiteInk = ScriptableObject.CreateInstance<StickConfig>();
            whiteInk.SetRuntimeInkColor(StickmanInkColor.White);
            try
            {
                _backdrops = new[]
                {
                    ("밝은 바탕화면(극단 흰색)", Color.white),
                    ("어두운 바탕화면(극단 검은색)", Color.black),
                    ("종이 무대", CharacterPortraitStage.ResolveBackdropColor(blackInk)),
                    ("목탄 무대", CharacterPortraitStage.ResolveBackdropColor(whiteInk)),
                };
            }
            finally
            {
                Object.DestroyImmediate(blackInk);
                Object.DestroyImmediate(whiteInk);
            }
            return _backdrops;
        }

        /// <summary>배경 열거 자체의 네거티브 컨트롤 — 두 무대가 같은 색으로 붕괴하면
        /// "네 배경을 검사했다"가 거짓말이 된다(같은 배경을 두 번 재고 초록).</summary>
        [Test]
        public void 배경_넷이_서로_다르고_대역을_막는_것은_극단이_아니라_두_무대다()
        {
            (string Name, Color Color)[] bg = Backdrops();
            Assert.AreEqual(4, bg.Length, $"{LogPrefix} 배경이 넷이 아닙니다.");

            Color paper = bg[2].Color, charcoal = bg[3].Color;
            Assert.Greater(UiChrome.ContrastRatio(paper, charcoal), 2f,
                $"{LogPrefix} 종이 무대({Show(paper)})와 목탄 무대({Show(charcoal)})가 사실상 같은 색입니다 — " +
                "ResolveBackdropColor가 잉크 프리셋을 보지 않게 되면 이 게이트는 배경 셋만 재게 됩니다.");
            Assert.Greater(UiChrome.RelativeLuminance(paper), UiChrome.RelativeLuminance(charcoal),
                $"{LogPrefix} 종이 무대가 목탄 무대보다 어둡습니다 — 두 무대가 뒤바뀌었습니다.");

            // ★ 이 결함의 기하학 자체를 잠근다. 이 두 줄이 빨개지면 누군가 "흑백만 보면 된다"는
            //   옛 오해로 되돌아간 것이다.
            float floorFromDesktop = Floor(Color.black), floorFromStage = Floor(charcoal);
            float ceilFromDesktop = Ceil(Color.white), ceilFromStage = Ceil(paper);
            Assert.Greater(floorFromStage, floorFromDesktop,
                $"{LogPrefix} 대역의 아래를 막는 것이 목탄 무대가 아니라 검은 바탕화면입니다.");
            Assert.Less(ceilFromStage, ceilFromDesktop,
                $"{LogPrefix} 대역의 위를 막는 것이 종이 무대가 아니라 흰 바탕화면입니다.");
            Debug.Log($"{LogPrefix} 자립 대역 L ∈ [{floorFromStage:F4}, {ceilFromStage:F4}] " +
                      $"— 흑백만 볼 때의 [{floorFromDesktop:F4}, {ceilFromDesktop:F4}]보다 좁다.");
        }

        // ============================================================================
        // 2. 본안 — 몸에 칠해진 색이 배경 넷 전부에서 하한을 넘는가
        // ============================================================================

        /// <summary>
        /// ★ 판정 대상은 <b>카탈로그 색이 아니라 <see cref="ItemCatalog.WornColor"/>를 통과한 색</b>이다.
        /// 이 결함의 정체가 그것이었다 — 카드에서 멀쩡하던 <c>#FFF0B8</c>가 몸에서 <c>#CCBA76</c>이 되어
        /// 종이 무대에서 1.60:1로 사라졌다.
        /// <para>잉크가 흰색이냐 검은색이냐로 결과가 갈리지 않는지도 함께 본다(사용자가 바꿀 수 있다).</para>
        /// </summary>
        [Test]
        public void 모든_아이템_색이_몸에_칠해진_뒤에도_배경_넷에서_보인다()
        {
            AssertCalculatorCalibrated();

            var failures = new List<string>();
            int entries = 0, judged = 0;

            foreach (ItemCatalogEntry e in IconEntries())
            {
                entries++;
                for (int p = 0; p < e.Icon.Length; p++)
                {
                    Color c = e.Icon[p].Color;
                    if (IsInkMarker(c)) continue;   // 몸에서는 이 값이 아니라 캐릭터 잉크로 칠해진다
                    foreach ((Color ink, string inkName) in Inks())
                    {
                        judged++;
                        Judge($"{e.Id}#p{p}({inkName})", ItemCatalog.WornColor(c, ink), failures);
                    }
                }
            }

            AssertEnumerationNotEmpty(entries, judged);
            Assert.IsEmpty(failures,
                $"{LogPrefix} 몸에 칠한 뒤 배경 넷 중 하나 이상에서 " +
                $"{UiChrome.MinNonTextContrast:F1}:1을 못 넘는 조각이 {failures.Count}건입니다.\n" +
                string.Join("\n", failures));
        }

        /// <summary>
        /// 카드 색과 몸 색이 <b>바이트 단위로 같다</b>(= <see cref="ItemCatalog.WornColor"/> 항등).
        /// 이게 서면 "카드엔 색이 있는데 착용하면 다른 색"이라는 결함군이 <b>구조적으로</b> 사라진다
        /// (2026-08-30 사용자 신고의 뿌리. 옛 최악 카드↔몸 ΔE 42.3 — <c>#EEF2F8</c> -> <c>#7699CC</c>).
        /// </summary>
        [Test]
        public void 카드_색과_몸_색이_같다_WornColor_항등()
        {
            var failures = new List<string>();
            int entries = 0, judged = 0;

            foreach (ItemCatalogEntry e in IconEntries())
            {
                entries++;
                for (int p = 0; p < e.Icon.Length; p++)
                {
                    Color c = e.Icon[p].Color;
                    if (IsInkMarker(c)) continue;
                    judged++;
                    Color worn = ItemCatalog.WornColor(c, Color.white);
                    if (Same(worn, c)) continue;
                    failures.Add($"  {e.Id}#p{p} 카드 {Show(c)} -> 몸 {Show(worn)} " +
                                 "(WornColor가 이 색을 바꿉니다 — 카드와 몸이 갈라집니다)");
                }
            }

            AssertEnumerationNotEmpty(entries, judged);
            Assert.IsEmpty(failures,
                $"{LogPrefix} 카드 색과 몸 색이 다른 조각이 {failures.Count}건입니다.\n" +
                string.Join("\n", failures));
        }

        /// <summary>몸에서 살아남는 색이 <b>어두운 카드 위에서도</b> 살아남는가. 대역이 어두운 쪽이라
        /// 이 검사가 없으면 "바탕화면에서는 보이는데 보관함 카드에서 사라지는" 반대 사고가 열린다.</summary>
        [Test]
        public void 몸에서_살아남는_색이_어두운_카드_위에서도_보인다()
        {
            AssertCalculatorCalibrated();

            (string Name, Color Color)[] surfaces =
            {
                ("PanelSurface", UiChrome.PanelSurface),
                ("CardSurface", UiChrome.CardSurface),
                ("CardSurfaceMuted", UiChrome.CardSurfaceMuted),
                ("SubtleSurface", UiChrome.SubtleSurface),
                ("ThumbSurfaceLocked", UiChrome.ThumbSurfaceLocked),
            };

            var failures = new List<string>();
            int entries = 0, judged = 0;
            foreach (ItemCatalogEntry e in IconEntries())
            {
                entries++;
                for (int p = 0; p < e.Icon.Length; p++)
                {
                    Color c = e.Icon[p].Color;
                    if (IsInkMarker(c)) continue;
                    judged++;
                    foreach ((string name, Color surface) in surfaces)
                    {
                        float cr = UiChrome.ContrastRatio(c, surface);
                        if (cr >= UiChrome.MinNonTextContrast) continue;
                        failures.Add($"  {e.Id}#p{p} {Show(c)} vs {name} = {cr:F2}:1");
                    }
                }
            }

            AssertEnumerationNotEmpty(entries, judged);
            Assert.IsEmpty(failures,
                $"{LogPrefix} 카드 표면 위에서 {UiChrome.MinNonTextContrast:F1}:1을 못 넘는 조각이 " +
                $"{failures.Count}건입니다.\n" + string.Join("\n", failures));
        }

        /// <summary>면제가 정직한가 — 잉크 표식 둘은 <b>실제로 잉크색으로 바뀌기 때문에</b> 면제다.
        /// 이게 깨지면 위 검사들이 "면제"라는 이름으로 두 색을 그냥 안 본 것이 된다.</summary>
        [Test]
        public void 잉크_표식_면제는_실제로_잉크색으로_바뀌기_때문이다()
        {
            foreach (Color marker in new[] { ItemCatalog.InkTone, ItemCatalog.InkDimTone })
            {
                foreach (Color ink in new[]
                         {
                             Color.white, Color.black, new Color(0.2f, 0.4f, 0.9f, 1f),
                         })
                {
                    Color worn = ItemCatalog.WornColor(marker, ink);
                    Assert.IsTrue(Same(worn, ink),
                        $"{LogPrefix} 잉크 표식 {Show(marker)}가 잉크 {Show(ink)}로 바뀌지 않고 " +
                        $"{Show(worn)}가 됐습니다 — 대역 면제의 근거가 사라집니다.");
                }
            }

            // 면제 대상이 실제로 트리에 있는가(면제 목록이 비어 아무것도 안 재는 사고 방지, TEAM.md §4-5).
            int markers = 0;
            foreach (ItemCatalogEntry e in IconEntries())
            {
                for (int p = 0; p < e.Icon.Length; p++)
                {
                    if (IsInkMarker(e.Icon[p].Color)) markers++;
                }
            }
            Assert.Greater(markers, 0,
                $"{LogPrefix} 잉크 표식을 쓰는 조각이 하나도 없습니다 — 면제 규칙이 죽은 코드입니다.");
            Debug.Log($"{LogPrefix} 잉크 표식 조각 {markers}건을 면제했다.");
        }

        // ============================================================================
        // 3. 양성 대조 — 일부러 나쁜 색을 넣었을 때 같은 판정이 빨간불을 내는가
        // ============================================================================

        [TestCase(0xFFF0B8, "출하돼 있던 최악 색(금빛 하이라이트) — 몸에서 #CCBA76, 종이 무대 1.60:1")]
        [TestCase(0xE8E2D4, "옛 Ivory — 몸에서 #CCB276, 종이 무대 1.70:1")]
        [TestCase(0x7690CC, "★ design-art의 1차 유도(흑백 극단만) 산물 — 종이 무대 2.62:1")]
        [TestCase(0xA6532E, "반대쪽 이탈 — 너무 어두워 목탄 무대에서 2.74:1")]
        [TestCase(0x000000, "완전한 검정 — 어두운 바탕화면에서 사라진다")]
        [TestCase(0xFFFFFF, "완전한 흰색 — 밝은 바탕화면에서 사라진다")]
        public void 양성_대조_대역_밖_색은_게이트가_잡는다(int hex, string why)
        {
            AssertCalculatorCalibrated();

            var failures = new List<string>();
            Judge("대조", ItemCatalog.WornColor(Hex(hex), Color.white), failures);

            Assert.IsNotEmpty(failures,
                $"{LogPrefix} ★대조 실패 — {Show(Hex(hex))}({why})를 게이트가 놓쳤습니다. " +
                "이 파일이 낸 모든 '위반 0건'을 폐기하십시오.");
            Debug.Log($"{LogPrefix} 대조 통과 — {Show(Hex(hex))} 을(를) 잡았다:{failures[0]}");
        }

        /// <summary>
        /// ★ <b>중간 밝기 배경이 극단보다 어렵다</b>는 이 결함의 기하학을 색 하나로 박제한다.
        /// <c>#7690CC</c>는 흰 바탕화면에서도 검은 바탕화면에서도 하한을 넘지만 종이 무대에서 미달한다.
        /// 이 단언이 빨개지면 누군가 배경을 흑백 둘로 줄인 것이다.
        /// </summary>
        [Test]
        public void 양성_대조_흑백_극단만_보면_통과하는_색이_종이_무대에서_미달한다()
        {
            AssertCalculatorCalibrated();
            Color trap = Hex(0x7690CC);

            Assert.GreaterOrEqual(UiChrome.ContrastRatio(trap, Color.white), UiChrome.MinNonTextContrast,
                $"{LogPrefix} 함정 색이 흰 바탕화면에서 이미 미달합니다 — 대조가 성립하지 않습니다.");
            Assert.GreaterOrEqual(UiChrome.ContrastRatio(trap, Color.black), UiChrome.MinNonTextContrast,
                $"{LogPrefix} 함정 색이 검은 바탕화면에서 이미 미달합니다 — 대조가 성립하지 않습니다.");

            Color paper = Backdrops()[2].Color;
            float cr = UiChrome.ContrastRatio(trap, paper);
            Assert.Less(cr, UiChrome.MinNonTextContrast,
                $"{LogPrefix} 함정 색 {Show(trap)}이 종이 무대에서 {cr:F2}:1로 통과했습니다 — " +
                "무대 색이 바뀌었다면 대역을 다시 유도해야 합니다.");
            Debug.Log($"{LogPrefix} 대조 통과 — {Show(trap)}: 흰/검 바탕화면 통과, 종이 무대 {cr:F2}:1 미달.");
        }

        /// <summary>대역 안이어도 <see cref="ItemCatalog.WornColor"/> 항등이 아닌 색을 항등 검사가
        /// 잡는가. 채도 하한 미달(무채에 가까운 회색)이 가장 흔한 형태다.</summary>
        [Test]
        public void 양성_대조_항등이_아닌_색은_항등_검사가_잡는다()
        {
            Color grey = Hex(0x6E7176);
            Color worn = ItemCatalog.WornColor(grey, Color.white);
            Assert.IsFalse(Same(worn, grey),
                $"{LogPrefix} ★대조 실패 — 채도 하한 미달 색 {Show(grey)}를 WornColor가 그대로 뒀습니다. " +
                "항등 검사가 아무것도 못 잡는 상태입니다.");
            Debug.Log($"{LogPrefix} 대조 통과 — {Show(grey)} -> {Show(worn)} (항등 아님).");
        }

        // ============================================================================
        // 판정 — 본안과 대조가 <b>같은 함수</b>를 쓴다
        // ============================================================================

        /// <summary>배경 넷 전부에서 <see cref="UiChrome.MinNonTextContrast"/>를 넘는가.
        /// 못 넘으면 <paramref name="failures"/>에 사람이 읽을 수 있는 줄을 넣는다.</summary>
        private static void Judge(string owner, Color worn, List<string> failures)
        {
            foreach ((string name, Color backdrop) in Backdrops())
            {
                float cr = UiChrome.ContrastRatio(worn, backdrop);
                if (cr >= UiChrome.MinNonTextContrast) continue;
                failures.Add($"  {owner} 몸색 {Show(worn)} vs {name} {Show(backdrop)} = {cr:F2}:1 " +
                             $"(하한 {UiChrome.MinNonTextContrast:F1})");
            }
        }

        // ============================================================================
        // 열거 — 애셋에서 온 값만 본다(문서를 베끼지 않는다)
        // ============================================================================

        /// <summary>아이콘을 가진 카탈로그 항목 전부. 주색/보조색이 아니라 <b>조각 색 전부</b>를 본다 —
        /// 주색/보조색이 조각에서 뽑혀 나오므로 조각이 상위 집합이고, 새 아이템이 세 번째 색을 들고
        /// 들어와도 여기서 걸린다.</summary>
        private static IEnumerable<ItemCatalogEntry> IconEntries()
        {
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e?.Icon == null || e.Icon.Length == 0) continue;
                yield return e;
            }
        }

        private static IEnumerable<(Color Ink, string Name)> Inks()
        {
            yield return (Color.white, "흰 잉크");
            yield return (Color.black, "검은 잉크");
        }

        /// <summary>열거가 조용히 비면 "위반 0건"이 거짓 초록이 된다. 모수를 손으로 베끼지 않고
        /// <see cref="ItemCatalog.EquipmentCount"/>에게 묻는다 — 아이템이 늘면 이 하한도 함께 는다.</summary>
        private static void AssertEnumerationNotEmpty(int entries, int judged)
        {
            Assert.AreEqual(ItemCatalog.EquipmentCount, entries,
                $"{LogPrefix} 아이콘을 가진 항목이 {entries}개인데 장비는 " +
                $"{ItemCatalog.EquipmentCount}종입니다 — 열거가 새고 있습니다.");
            Assert.GreaterOrEqual(judged, entries,
                $"{LogPrefix} 잰 조각이 {judged}건뿐입니다(항목 {entries}개). " +
                "항목마다 적어도 한 조각은 재야 합니다.");
            Debug.Log($"{LogPrefix} 항목 {entries}개 / 조각 판정 {judged}건.");
        }

        // ============================================================================
        // 유틸
        // ============================================================================

        private static bool IsInkMarker(Color c)
            => Same(c, ItemCatalog.InkTone) || Same(c, ItemCatalog.InkDimTone);

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f
               && Mathf.Abs(a.b - b.b) < 0.004f;

        private static Color Hex(int hex)
            => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        private static string Show(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>이 배경 위에서 하한을 넘으려면 색의 상대휘도가 <b>이보다 커야</b> 하는 값.</summary>
        private static float Floor(Color backdrop)
            => UiChrome.MinNonTextContrast * (UiChrome.RelativeLuminance(backdrop) + 0.05f) - 0.05f;

        /// <summary>이 배경 위에서 하한을 넘으려면 색의 상대휘도가 <b>이보다 작아야</b> 하는 값.</summary>
        private static float Ceil(Color backdrop)
            => (UiChrome.RelativeLuminance(backdrop) + 0.05f) / UiChrome.MinNonTextContrast - 0.05f;
    }
}
