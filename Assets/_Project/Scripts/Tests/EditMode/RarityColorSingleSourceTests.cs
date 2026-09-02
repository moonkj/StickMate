using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 등급색의 <b>단일 출처</b> — 다섯 리터럴이 <c>UiChrome.cs</c> 밖에 존재하지 않는가,
    /// 그리고 그 다섯 색이 실제로 <b>제 일을 하는가</b>.
    ///
    /// ============================================================================
    /// 왜 이 감사가 필요한가
    /// ============================================================================
    /// design-art 판정(PALETTE_SPEC §14-2): *"<c>#9C978C</c> / <c>#BCAC8B</c> / <c>#DBBD7F</c> /
    /// <c>#F9CB70</c> / <c>#3A4049</c> 다섯 리터럴은 **<c>UiChrome.cs</c> 안에서만** 존재해야 한다.
    /// 이 다섯 개 밖에서 등급색 hex가 나타나면 그것이 드리프트의 시작이다."*
    ///
    /// 그리고 이 저장소는 그 드리프트를 <b>이미 겪었다</b> — 카드용으로 밝게 잡은 색을 몸에 그대로
    /// 칠해 카드↔몸 ΔE가 최악 42.3까지 벌어졌던 사건(<c>ItemPaletteBandGateTests</c> 참조).
    /// 값이 두 벌이 되는 순간 한 벌만 고쳐지는 것은 시간 문제다.
    ///
    /// ============================================================================
    /// ★ 문자열만 보면 놓친다 — 그래서 <b>값</b>도 본다
    /// ============================================================================
    /// hex 표기를 안 쓰고 <c>new Color(0.612f, 0.592f, 0.549f, 1f)</c>로 적으면 grep은 조용하다.
    /// TEAM.md §4-4가 기록한 그 형태다(<c>strings</c>로 UTF-16 문자열을 찾아 "0건 = 깨끗").
    /// 그래서 이 감사는 <b>둘 다</b> 본다 — hex 표기와 실수/바이트 3튜플.
    /// 그리고 판정기가 눈이 멀지 않았다는 것을 <b>소유자 파일에서 다섯 개를 실제로 찾아</b> 증명한다.
    /// </summary>
    public sealed class RarityColorSingleSourceTests
    {
        private const string LogPrefix = "[등급색단일출처]";

        /// <summary>등급색의 소유자. 이 파일 하나만 면제다.</summary>
        private const string OwnerFileName = "UiChrome.cs";

        /// <summary>8비트 양자화 한 칸보다 조금 좁게 — 같은 색을 다르게 적은 것을 같은 색으로 본다.</summary>
        private const float ChannelTolerance = 0.6f / 255f;

        // ============================================================================
        // 0. 램프 자체 — 다섯 색이 제 일을 하는가
        // ============================================================================

        private static void AssertCalculatorCalibrated()
        {
            Assert.AreEqual(21.0f, UiChrome.ContrastRatio(Color.white, Color.black), 0.0005f,
                $"{LogPrefix} 흰/검 대비가 21.0이 아닙니다 — 계산기가 고장났으므로 이 파일의 모든 판정을 폐기하십시오.");
            Assert.AreEqual(1.0f, UiChrome.ContrastRatio(Color.white, Color.white), 0.0005f,
                $"{LogPrefix} 동일색 대비가 1.0이 아닙니다.");
            Assert.AreEqual(4.5422f, UiChrome.ContrastRatio(Hex(0x767676), Color.white), 0.0005f,
                $"{LogPrefix} #767676 / 흰색 대비가 WCAG 기준값 4.5422가 아닙니다.");
        }

        [Test]
        public void 대비_계산기가_알려진_값을_낸다()
        {
            AssertCalculatorCalibrated();
        }

        /// <summary>램프가 등급 수만큼 있고, 등급마다 <b>서로 다른</b> 색이 나온다.
        /// 길이가 어긋나면 <c>RarityColor</c>가 끝 색으로 물러나 화면의 칸 수와 색이 갈라진다.</summary>
        [Test]
        public void 램프가_등급마다_하나씩_있고_서로_다르다()
        {
            var seen = new List<Color>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color c = UiChrome.RarityColor(r);
                foreach (Color other in seen)
                {
                    Assert.IsFalse(Same(c, other),
                        $"{LogPrefix} {ItemCatalog.RarityName(r)}가 이미 나온 색 {Show(other)}와 같습니다 — " +
                        "등급 하나가 색을 잃었습니다.");
                }
                seen.Add(c);
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(ItemRarity)).Length, seen.Count);
            Debug.Log($"{LogPrefix} 램프 {seen.Count}색: " + string.Join(" ", seen.ConvertAll(Show)));
        }

        /// <summary>★ <b>황동 함량 램프</b> — 등급이 오르면 휘도가 <b>반드시</b> 오른다.
        /// 이게 깨지면 흑백 출력·완전색맹에서 서열이 뒤집힌다(PALETTE_SPEC §12-1: 램프가 색맹에서
        /// 버티는 유일한 이유가 "색상각 하나 + 단조 명도"였다).</summary>
        [Test]
        public void 등급이_오르면_휘도가_오른다()
        {
            float previous = -1f;
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                float l = UiChrome.RelativeLuminance(UiChrome.RarityColor(r));
                Assert.Greater(l, previous,
                    $"{LogPrefix} {ItemCatalog.RarityName(r)}의 휘도 {l:F4}가 바로 아래 등급({previous:F4}) 이하입니다 — " +
                    "흑백/완전색맹에서 서열이 뒤집힙니다.");
                previous = l;
            }
        }

        /// <summary>★ 트랙은 <b>두 방향</b>으로 동시에 서야 한다. 한쪽만 재면 반대쪽이 조용히 죽는다.
        /// <para>(a) 채움 ↔ 트랙 ≥ <see cref="UiChrome.MinNonTextContrast"/> — 몇 칸이 찼는지 읽혀야 한다.</para>
        /// <para>(b) 트랙 ↔ 카드 바탕 ≥ <b>이 저장소가 이미 "구획이 보인다"고 인정한 가장 옅은 테두리</b>
        /// (<see cref="UiChrome.CardBorder"/>를 카드 위에 합성한 값)의 대비 — 트랙이 안 보이면
        /// 총 폭이 사라져 <b>채움 비율</b>이 다시 <b>세는 일</b>이 된다.</para>
        /// <para>숫자를 적지 않는 이유: 두 하한 모두 프로덕션 토큰에서 계산해 낸다.</para></summary>
        [Test]
        public void 트랙이_채움과도_카드_바탕과도_동시에_선다()
        {
            AssertCalculatorCalibrated();

            List<string> failures = JudgeTrack(UiChrome.RarityTrack);
            Assert.IsEmpty(failures,
                $"{LogPrefix} 트랙 {Show(UiChrome.RarityTrack)}이 {failures.Count}건에서 미달입니다.\n" +
                string.Join("\n", failures));

            Debug.Log($"{LogPrefix} 트랙 {Show(UiChrome.RarityTrack)} — 카드 바탕 " +
                      $"{UiChrome.ContrastRatio(UiChrome.RarityTrack, UiChrome.CardSurface):F2}:1 " +
                      $"(하한 {TrackVisibilityFloor():F2}), 채움 최악 {WorstFillOnTrack(UiChrome.RarityTrack):F2}:1 " +
                      $"(하한 {UiChrome.MinNonTextContrast:F1}).");
        }

        /// <summary>채움 4색이 카드 표면들 위에서도 비텍스트 하한을 넘는가 — 리본은 트랙 위에만
        /// 앉는 것이 아니라 카드 상단 여백에 얹힌다.</summary>
        [Test]
        public void 채움_4색이_카드_표면들_위에서_보인다()
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
            int judged = 0;
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color fill = UiChrome.RarityColor(r);
                foreach ((string name, Color surface) in surfaces)
                {
                    judged++;
                    float cr = UiChrome.ContrastRatio(fill, surface);
                    if (cr >= UiChrome.MinNonTextContrast) continue;
                    failures.Add($"  {ItemCatalog.RarityName(r)} {Show(fill)} vs {name} = {cr:F2}:1");
                }
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(ItemRarity)).Length * surfaces.Length, judged,
                $"{LogPrefix} 잰 조합이 {judged}건입니다 — 열거가 샙니다.");
            Assert.IsEmpty(failures, $"{LogPrefix} 미달 {failures.Count}건.\n" + string.Join("\n", failures));
        }

        /// <summary>★ 등급색을 <b>몸에 칠하면 안 된다</b>는 판정을 값으로 박제한다(PALETTE_SPEC §12-2).
        /// <c>WornColor</c>를 통과시키면 네 색 전부가 배경 어딘가에서 비텍스트 하한 아래로 내려간다.
        /// 이 단언이 빨개지면 누군가 램프를 몸 대역으로 옮긴 것이고, 그건 크롬 대비를 잃었다는 뜻이다.</summary>
        [Test]
        public void 등급색은_크롬_대역에_산다_몸에_칠할_수_없는_값이다()
        {
            AssertCalculatorCalibrated();

            int wouldFail = 0;
            var trace = new List<string>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color worn = ItemCatalog.WornColor(UiChrome.RarityColor(r), Color.white);
                float worst = float.MaxValue;
                string worstBg = null;
                foreach ((string bgName, Color bg) in BodyBackdrops())
                {
                    float cr = UiChrome.ContrastRatio(worn, bg);
                    if (cr >= worst) continue;
                    worst = cr;
                    worstBg = bgName;
                }
                if (worst < UiChrome.MinNonTextContrast) wouldFail++;
                trace.Add($"{ItemCatalog.RarityName(r)} {Show(worn)} 최악 {worst:F2}:1({worstBg})");
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(ItemRarity)).Length, wouldFail,
                $"{LogPrefix} 등급 램프가 몸 대역(자립 대역)으로 들어왔습니다 — 크롬 전용이라는 전제가 " +
                "깨졌다면 §12-2의 '창 안에서만 산다' 판정을 다시 세워야 합니다.\n  " +
                string.Join("\n  ", trace));
            Debug.Log($"{LogPrefix} 램프 {wouldFail}색 전부가 몸에서는 하한 미달 — 크롬 전용이 맞다. " +
                      string.Join(" / ", trace));
        }

        // ============================================================================
        // 1. ★ 양성 대조 — 트랙 판정이 실제로 무는가 (design-art가 기각한 두 후보로)
        // ============================================================================

        /// <summary>
        /// <c>#2A2F38</c>(카드 바탕과 1.23 — 트랙이 안 보인다)와 <c>#4A515C</c>(채움과 2.75 — 채움이 죽는다)를
        /// <b>본안과 같은 판정 함수</b>에 넣는다. 두 후보가 <b>서로 다른 쪽</b>에서 걸리는 것이 핵심이다 —
        /// 한쪽만 재는 검사였다면 둘 중 하나는 통과했을 것이다.
        /// </summary>
        [TestCase(0x2A2F38, "카드 바탕", "트랙이 거의 안 보인다(§12-4에서 기각)")]
        [TestCase(0x4A515C, "채움", "채움이 비텍스트 하한 미달(§12-4에서 기각)")]
        [TestCase(0x14171C, "카드 바탕", "패널 바탕과 같은 값 — 트랙이 사라진다")]
        [TestCase(0xF9CB70, "채움", "전설 채움과 같은 값 — 빈 칸과 찬 칸이 구분되지 않는다")]
        public void 양성_대조_기각된_트랙_후보를_판정이_잡는다(int hex, string expectedSide, string why)
        {
            AssertCalculatorCalibrated();

            List<string> failures = JudgeTrack(Hex(hex));
            Assert.IsNotEmpty(failures,
                $"{LogPrefix} ★대조 실패 — {Show(Hex(hex))}({why})를 판정이 놓쳤습니다. " +
                "이 파일이 낸 트랙 관련 초록을 전부 폐기하십시오.");

            bool hitExpected = failures.Exists(f => f.Contains(expectedSide));
            Assert.IsTrue(hitExpected,
                $"{LogPrefix} ★대조 실패 — {Show(Hex(hex))}가 '{expectedSide}' 쪽에서 걸려야 하는데 " +
                $"다른 이유로 걸렸습니다. 두 방향 중 하나가 죽어 있을 수 있습니다.\n" +
                string.Join("\n", failures));

            Debug.Log($"{LogPrefix} 대조 통과 — {Show(Hex(hex))}:{failures[0]}");
        }

        /// <summary>★ <b>조건을 강화했는데 통과가 늘어나는 경로가 없는가.</b>
        /// design-art가 자기 도구에서 겪은 함정이다 — 여유를 9.0으로 올리자 결과가 8.06에서 6.21로
        /// <b>나빠졌고</b>, 원인은 해가 없을 때 조용히 무제약 값으로 돌아가는 폴백이었다.
        /// <para>판정 함수를 하한만 바꿔 가며 훑어 <b>실패 건수가 단조 비감소</b>인지 본다.
        /// 어딘가에서 줄어들면 그 하한 위에 폴백이 숨어 있다는 뜻이다.</para></summary>
        [Test]
        public void 하한을_올리면_통과가_늘어나는_경로가_없다()
        {
            AssertCalculatorCalibrated();

            int previous = -1;
            var trace = new List<string>();
            for (float floor = 1.0f; floor <= 8.0f + 1e-4f; floor += 0.25f)
            {
                int count = JudgeTrack(UiChrome.RarityTrack, floor, floor).Count;
                trace.Add($"{floor:F2}->{count}");
                Assert.GreaterOrEqual(count, previous,
                    $"{LogPrefix} 하한을 {floor:F2}로 올렸더니 미달 건수가 {previous} -> {count}로 " +
                    "줄었습니다. 제약을 강화했는데 결과가 좋아졌다면 판정 안에 폴백이 숨어 있습니다.\n" +
                    string.Join(" ", trace));
                previous = count;
            }

            Assert.Greater(previous, 0,
                $"{LogPrefix} 하한을 끝까지 올려도 미달이 0건입니다 — 판정이 아무것도 재지 않고 있습니다.\n" +
                string.Join(" ", trace));
            Debug.Log($"{LogPrefix} 하한 스윕 단조 확인 — {string.Join(" ", trace)}");
        }

        // ============================================================================
        // 2. 단일 출처 감사 — 다섯 리터럴이 UiChrome.cs 밖에 없는가
        // ============================================================================

        /// <summary>등급 다섯 색. <b>값은 프로덕션에서 꺼낸다</b> — 여기 hex를 적으면 이 파일이
        /// 감사 대상이 아니라 두 번째 출처가 된다.</summary>
        private static List<(string Name, Color Color)> OwnedColors()
        {
            var list = new List<(string, Color)>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                list.Add(($"{ItemCatalog.RarityName(r)}({r})", UiChrome.RarityColor(r)));
            }
            list.Add(("리본 트랙", UiChrome.RarityTrack));
            return list;
        }

        [Test]
        public void 등급색_리터럴이_UiChrome_밖에_존재하지_않는다()
        {
            List<string> files = ProductionFiles();
            var hits = new List<string>();
            int scanned = 0, ownerHits = 0;

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                List<string> found = ScanText(name, File.ReadAllText(file));
                if (name == OwnerFileName)
                {
                    ownerHits = found.Count;
                    continue;
                }
                scanned++;
                hits.AddRange(found);
            }

            // ★ 판정기가 눈이 멀지 않았다는 증명 — 소유자 파일에서는 다섯 개가 <b>전부</b> 나와야 한다.
            Assert.GreaterOrEqual(ownerHits, OwnedColors().Count,
                $"{LogPrefix} ★대조 실패 — 소유자 {OwnerFileName}에서 등급색 리터럴을 {ownerHits}건밖에 " +
                $"못 찾았습니다(기대 {OwnedColors().Count}건 이상). 판정기가 아무것도 못 보는 상태이므로 " +
                "아래 '위반 0건'은 무효입니다.");

            Assert.Greater(scanned, 40,
                $"{LogPrefix} 훑은 프로덕션 파일이 {scanned}개뿐입니다 — 경로 계산이 틀렸을 수 있습니다.");

            Assert.IsEmpty(hits,
                $"{LogPrefix} 등급색이 {OwnerFileName} 밖에서 {hits.Count}건 발견됐습니다. " +
                "등급색의 유일한 출처는 UiChrome.RarityColor / UiChrome.RarityTrack입니다 " +
                "(PALETTE_SPEC §14-2).\n" + string.Join("\n", hits));

            Debug.Log($"{LogPrefix} 프로덕션 {scanned}개 파일 위반 0건 / 소유자에서 {ownerHits}건 확인.");
        }

        /// <summary>★ 판정기를 <b>합성한 소스</b>에 물려 hex 표기·실수 3튜플·바이트 3튜플 세 형태를
        /// 전부 무는지 본다. 프로덕션 트리를 건드리지 않고 대조하는 유일한 방법이다.</summary>
        [Test]
        public void 양성_대조_세_가지_표기를_전부_잡는다()
        {
            Color epic = UiChrome.RarityColor(ItemRarity.Epic);
            string hex = ColorUtility.ToHtmlStringRGB(epic);
            var r8 = (int)Mathf.Round(epic.r * 255f);
            var g8 = (int)Mathf.Round(epic.g * 255f);
            var b8 = (int)Mathf.Round(epic.b * 255f);

            var samples = new List<(string Why, string Source)>
            {
                ("hex 표기(#RRGGBB)", $"public static readonly Color Ribbon = FromHex(\"#{hex}\");"),
                ("hex 표기(0x)", $"private static readonly Color Ribbon = Rgb(0x{hex});"),
                ("실수 3튜플", $"var c = new Color({epic.r:F3}f, {epic.g:F3}f, {epic.b:F3}f, 1f);"),
                ("Color32 바이트", $"var c = new Color32({r8}, {g8}, {b8}, 255);"),
            };

            foreach ((string why, string source) in samples)
            {
                List<string> found = ScanText("가짜파일.cs", source);
                Assert.IsNotEmpty(found,
                    $"{LogPrefix} ★대조 실패 — {why}를 판정기가 놓쳤습니다: {source}");
                Debug.Log($"{LogPrefix} 대조 통과 — {why}:{found[0]}");
            }

            // 그리고 관계없는 색은 잡지 않는다(무엇이든 빨개지는 판정기는 판정기가 아니다).
            Assert.IsEmpty(ScanText("가짜파일.cs",
                    "var c = new Color(0.365f, 0.631f, 0.961f, 1f); // Accent\nvar d = Rgb(0x5DA1F5);"),
                $"{LogPrefix} ★대조 실패 — 등급색이 아닌 강조색을 등급색으로 잡았습니다.");

            // 주석 줄은 보지 않는다 — 문서가 팔레트를 인용하는 것은 드리프트가 아니다.
            Assert.IsEmpty(ScanText("가짜파일.cs", $"        // 참고: 영웅 리본은 #{hex}다."),
                $"{LogPrefix} 주석 줄을 위반으로 셌습니다 — 문서 인용까지 막으면 감사가 못 쓰게 됩니다.");
        }

        // ============================================================================
        // 판정 — 본안과 대조가 같은 함수를 쓴다
        // ============================================================================

        /// <summary>이 저장소가 "구획이 보인다"고 이미 인정한 가장 옅은 테두리의 대비.
        /// 트랙은 적어도 이만큼은 카드 바탕에서 떨어져 있어야 한다.</summary>
        private static float TrackVisibilityFloor()
            => UiChrome.ContrastRatio(UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface),
                UiChrome.CardSurface);

        private static float WorstFillOnTrack(Color track)
        {
            float worst = float.MaxValue;
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                worst = Mathf.Min(worst, UiChrome.ContrastRatio(UiChrome.RarityColor(r), track));
            }
            return worst;
        }

        private static List<string> JudgeTrack(Color track)
            => JudgeTrack(track, UiChrome.MinNonTextContrast, TrackVisibilityFloor());

        /// <summary>트랙 두 방향 판정. 하한을 인자로 받는 것은 폴백 탐지 스윕이 <b>같은 함수</b>를
        /// 타야 하기 때문이다.</summary>
        private static List<string> JudgeTrack(Color track, float fillFloor, float visibilityFloor)
        {
            var failures = new List<string>();

            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color fill = UiChrome.RarityColor(r);
                float cr = UiChrome.ContrastRatio(fill, track);
                if (cr >= fillFloor) continue;
                failures.Add($"  채움 {ItemCatalog.RarityName(r)} {Show(fill)} vs 트랙 {Show(track)} = " +
                             $"{cr:F2}:1 (하한 {fillFloor:F2})");
            }

            foreach ((string name, Color surface) in new[]
                     {
                         ("카드 바탕", UiChrome.CardSurface),
                         ("카드 바탕(잠김)", UiChrome.CardSurfaceMuted),
                     })
            {
                float cr = UiChrome.ContrastRatio(track, surface);
                if (cr >= visibilityFloor) continue;
                failures.Add($"  트랙 {Show(track)} vs {name} {Show(surface)} = {cr:F2}:1 " +
                             $"(하한 {visibilityFloor:F2})");
            }

            return failures;
        }

        // ============================================================================
        // 스캐너 — hex 표기와 값 표기를 함께 본다
        // ============================================================================

        private static readonly Regex ColorFloatPattern = new Regex(
            @"new\s+Color\s*\(\s*(-?[0-9]*\.?[0-9]+)f?\s*,\s*(-?[0-9]*\.?[0-9]+)f?\s*,\s*(-?[0-9]*\.?[0-9]+)f?",
            RegexOptions.Compiled);

        private static readonly Regex Color32Pattern = new Regex(
            @"new\s+Color32\s*\(\s*([0-9]+)\s*,\s*([0-9]+)\s*,\s*([0-9]+)",
            RegexOptions.Compiled);

        /// <summary>이 소스에서 등급색을 <b>다시 적은</b> 자리를 전부 찾는다.
        /// 주석 줄은 건너뛴다 — 팔레트를 인용하는 문서까지 막으면 감사가 못 쓰게 된다.</summary>
        private static List<string> ScanText(string fileName, string text)
        {
            var hits = new List<string>();
            List<(string Name, Color Color)> owned = OwnedColors();

            // 파일마다 다시 굽지 않는다 — 203개 프로덕션 파일 × 수만 줄을 도는 감사다.
            var hexes = new string[owned.Count];
            for (int k = 0; k < owned.Count; k++) hexes[k] = ColorUtility.ToHtmlStringRGB(owned[k].Color);

            string[] lines = text.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;

                for (int k = 0; k < owned.Count; k++)
                {
                    if (line.IndexOf(hexes[k], System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    hits.Add($"  {fileName}:{i + 1} [{owned[k].Name} hex {hexes[k]}] {line.Trim()}");
                }

                foreach (Match m in ColorFloatPattern.Matches(line))
                {
                    if (!TryParse3(m, 1f, out Color c)) continue;
                    string owner = OwnerOf(owned, c);
                    if (owner == null) continue;
                    hits.Add($"  {fileName}:{i + 1} [{owner} 실수 3튜플] {line.Trim()}");
                }

                foreach (Match m in Color32Pattern.Matches(line))
                {
                    if (!TryParse3(m, 255f, out Color c)) continue;
                    string owner = OwnerOf(owned, c);
                    if (owner == null) continue;
                    hits.Add($"  {fileName}:{i + 1} [{owner} Color32] {line.Trim()}");
                }
            }
            return hits;
        }

        private static bool TryParse3(Match m, float divisor, out Color c)
        {
            c = default;
            if (!float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float r)) return false;
            if (!float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float g)) return false;
            if (!float.TryParse(m.Groups[3].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float b)) return false;
            c = new Color(r / divisor, g / divisor, b / divisor, 1f);
            return true;
        }

        private static string OwnerOf(List<(string Name, Color Color)> owned, Color c)
        {
            foreach ((string name, Color color) in owned)
            {
                if (Same(c, color)) return name;
            }
            return null;
        }

        private static List<string> ProductionFiles()
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string tests = (Path.Combine(scripts, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');

            var files = new List<string>(Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories));
            files.RemoveAll(p => p.Replace('\\', '/').StartsWith(tests, System.StringComparison.Ordinal));

            string editor = Path.Combine(Application.dataPath, "Editor");
            if (Directory.Exists(editor))
            {
                files.AddRange(Directory.GetFiles(editor, "*.cs", SearchOption.AllDirectories));
            }
            return files;
        }

        // ============================================================================
        // 유틸
        // ============================================================================

        private static (string Name, Color Color)[] _bodyBackdrops;

        /// <summary>몸에 칠한 색이 실제로 놓이는 배경 넷. 값은 프로덕션이 정한다
        /// (<see cref="ItemPaletteBandGateTests"/>와 같은 정의).</summary>
        private static (string Name, Color Color)[] BodyBackdrops()
        {
            if (_bodyBackdrops != null) return _bodyBackdrops;

            var blackInk = ScriptableObject.CreateInstance<StickConfig>();
            blackInk.SetRuntimeInkColor(StickmanInkColor.Black);
            var whiteInk = ScriptableObject.CreateInstance<StickConfig>();
            whiteInk.SetRuntimeInkColor(StickmanInkColor.White);
            try
            {
                _bodyBackdrops = new[]
                {
                    ("밝은 바탕화면", Color.white),
                    ("어두운 바탕화면", Color.black),
                    ("종이 무대", CharacterPortraitStage.ResolveBackdropColor(blackInk)),
                    ("목탄 무대", CharacterPortraitStage.ResolveBackdropColor(whiteInk)),
                };
            }
            finally
            {
                Object.DestroyImmediate(blackInk);
                Object.DestroyImmediate(whiteInk);
            }
            return _bodyBackdrops;
        }

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < ChannelTolerance
               && Mathf.Abs(a.g - b.g) < ChannelTolerance
               && Mathf.Abs(a.b - b.b) < ChannelTolerance;

        private static Color Hex(int hex)
            => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        private static string Show(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
