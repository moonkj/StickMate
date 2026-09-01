using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 P0 — <b>잉크 위계</b>. 사용자 원문 불만("텍스트들도 선명하지 않고 깔끔한 게
    /// 하나도 없어")의 직접 원인을 잠근다.
    ///
    /// ============================================================================
    /// 이 파일이 검사하는 것은 "색이 예쁜가"가 아니다
    /// ============================================================================
    /// 심야 실측(<c>docs/UI_SURFACE_SPEC.md</c> §11) 16행 중 <b>6행이 WCAG AA 미달</b>이었다. 그런데
    /// 진짜 결함은 그 6행이 아니라 <b>규칙이 없다는 것</b>이었다 — 설정창과 행동창이 서로를 모르는
    /// 채 <b>같은 역전</b>(비활성 행에서 이름 2.10 &lt; 이유 3.51)을 독립적으로 만들어 냈다.
    /// 콜사이트를 하나씩 고치면 세 번째 표면에서 또 난다. 그래서 여기서 검사하는 것은 셋이다.
    /// <list type="number">
    ///   <item><b>하한</b>: 모든 글자 단계가 <b>모든 바탕</b>에서 AA(4.5:1) 이상인가.</item>
    ///   <item><b>위계</b>: 한 덩어리 안에서 이름 ≥ 본문 ≥ 메타 순서가 활성/비활성 <b>양쪽</b>에서
    ///         유지되는가. 비활성이 <b>두 단</b> 내려가지 않는가.</item>
    ///   <item><b>네거티브 컨트롤</b>: 옛 값으로 되돌리면 <b>정말로</b> 빨개지는가.
    ///         이게 없으면 위 두 초록은 초록이 아니다(이 저장소는 하룻밤에 거짓 초록을 네 건 냈다).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는다
    /// ============================================================================
    /// 이 파일에는 4.5와 3.0 말고는 어떤 대비값도 적혀 있지 않다. 그 둘조차
    /// <see cref="UiChrome.MinTextContrast"/> / <see cref="UiChrome.MinNonTextContrast"/>를 참조한다.
    /// 색도 바탕도 전부 프로덕션 상수에서 읽어 <see cref="UiChrome.ContrastRatio"/>(WCAG 표준식)로
    /// <b>다시 계산</b>한다 — 여기에 5.33을 손으로 적는 순간 이 파일은 프로덕션이 아니라
    /// 자기 자신을 검사하게 된다(CLAUDE.md).
    /// </summary>
    public sealed class UiInkHierarchyTests
    {
        private const string LogPrefix = "[잉크위계-TEST]";

        /// <summary>이름 ≥ 본문 ≥ 메타. <b>이 배열의 순서가 곧 계약</b>이다.</summary>
        private static readonly UiChrome.InkRole[] RankedRoles =
        {
            UiChrome.InkRole.Title,
            UiChrome.InkRole.Body,
            UiChrome.InkRole.Meta,
        };

        private static IEnumerable<Color> Backdrops => UiChrome.TextBackdrops;

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>어떤 바탕에서 가장 불리한가까지 함께 돌려준다 — 실패 메시지가 범인을 지목해야 한다.</summary>
        private static float WorstContrast(Color ink, out Color worstBackdrop)
        {
            float worst = float.MaxValue;
            worstBackdrop = default;
            foreach (Color bg in Backdrops)
            {
                float c = UiChrome.ContrastRatio(ink, bg);
                if (c >= worst) continue;
                worst = c;
                worstBackdrop = bg;
            }
            return worst;
        }

        // ==================================================================================
        // 0. 전제 — 재는 자와 바탕이 실제로 존재하는가
        // ==================================================================================

        /// <summary>
        /// 바탕 목록이 비었거나 반투명이면 그 뒤의 모든 계산이 거짓말이 된다.
        /// <para><b>합성 후 색으로 재야 한다</b>는 요구가 여기서 강제된다 — 목록에 α&lt;1인 색이
        /// 들어오면 실패시켜 <see cref="UiChrome.Flatten"/>으로 미리 합성하게 만든다.</para>
        /// </summary>
        [Test]
        public void TextBackdropsAreDeclaredAndAlreadyComposited()
        {
            Assert.IsNotNull(UiChrome.TextBackdrops, $"{LogPrefix} 바탕 목록이 없습니다.");
            Assert.Greater(UiChrome.TextBackdrops.Length, 1,
                $"{LogPrefix} 바탕이 하나뿐이면 '패널에서는 통과하는데 카드에서 미달'을 못 잡습니다 — " +
                "옛 TabInactive가 정확히 그 틈에 있었습니다(패널 ✔ / 카드 ✘).");

            foreach (Color bg in UiChrome.TextBackdrops)
            {
                Assert.AreEqual(1f, bg.a, 1e-4f,
                    $"{LogPrefix} 바탕 {Hex(bg)}의 알파가 {bg.a:F2}입니다. 반투명 바탕 위의 글자는 " +
                    "<b>합성 후 색</b>으로 재야 하며, 합성 전 색으로 재면 대비 계산이 거짓말을 합니다. " +
                    "UiChrome.Flatten으로 미리 합성해서 목록에 넣으십시오.");
            }
        }

        // ==================================================================================
        // 1. 하한 — 모든 글자 단계가 모든 바탕에서 AA를 넘는가
        // ==================================================================================

        [Test]
        public void EveryTextInkStepClearsAaOnEveryBackdrop()
        {
            foreach (UiChrome.InkRole role in RankedRoles)
            {
                foreach (bool enabled in new[] { true, false })
                {
                    Color ink = UiChrome.Ink(role, enabled);
                    float worst = WorstContrast(ink, out Color bg);
                    Assert.GreaterOrEqual(worst, UiChrome.MinTextContrast,
                        $"{LogPrefix} {role}({(enabled ? "활성" : "비활성")}) = {Hex(ink)}이(가) 바탕 {Hex(bg)} 위에서 " +
                        $"{worst:F2}:1입니다. 본문 하한 {UiChrome.MinTextContrast:F1}:1 미만이면 " +
                        "그 글자는 '흐린 글자'가 아니라 <b>없는 글자</b>입니다 — 실제로 페르소나가 " +
                        "글자가 있는 화면을 '한 글자도 없음'이라고 적었습니다.");
                }
            }
        }

        /// <summary>탭도 같은 하한을 받는다 — 고르지 않은 탭/준비 안 된 탭 전부.</summary>
        [Test]
        public void EveryTabLabelStateClearsAa()
        {
            foreach (bool selected in new[] { true, false })
            {
                foreach (bool ready in new[] { true, false })
                {
                    Color ink = UiChrome.InkTab(selected, ready);
                    float worst = WorstContrast(ink, out Color bg);
                    Assert.GreaterOrEqual(worst, UiChrome.MinTextContrast,
                        $"{LogPrefix} 탭(고름={selected}, 준비됨={ready}) = {Hex(ink)}이(가) 바탕 {Hex(bg)} 위에서 " +
                        $"{worst:F2}:1입니다. 죽은 탭 라벨이 2.35:1이라 읽히지 않았던 것이 " +
                        "'이 탭에 아무 설명이 없다'는 신고의 실제 원인이었습니다.");
                }
            }
        }

        /// <summary>아이콘은 글자가 아니므로 3:1이지만, <b>비활성이어도</b> 그 하한은 지킨다.</summary>
        [Test]
        public void MutedIconStillClearsNonTextFloor()
        {
            foreach (bool enabled in new[] { true, false })
            {
                Color ink = UiChrome.InkIcon(enabled);
                float worst = WorstContrast(ink, out Color bg);
                Assert.GreaterOrEqual(worst, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} 아이콘({(enabled ? "활성" : "비활성")}) = {Hex(ink)}이(가) 바탕 {Hex(bg)} 위에서 " +
                    $"{worst:F2}:1입니다(비텍스트 하한 {UiChrome.MinNonTextContrast:F1}:1).");
            }
        }

        // ==================================================================================
        // 2. ★ 위계 — 이 라운드의 본체
        // ==================================================================================

        /// <summary>
        /// 한 덩어리 안의 서열이 <b>활성/비활성 양쪽에서</b> 유지되는가.
        /// <para>실제 사고: 비활성 타일에서 이름 2.10 &lt; 이유 3.51. 유저는 "뭔가 안 된다"는 읽고
        /// "무엇이 안 되는지"는 못 읽었다.</para>
        /// </summary>
        [Test]
        public void RankOrderNeverInvertsInEitherState()
        {
            foreach (Color bg in Backdrops)
            {
                foreach (bool enabled in new[] { true, false })
                {
                    for (int i = 1; i < RankedRoles.Length; i++)
                    {
                        UiChrome.InkRole higher = RankedRoles[i - 1];
                        UiChrome.InkRole lower = RankedRoles[i];
                        float ch = UiChrome.ContrastRatio(UiChrome.Ink(higher, enabled), bg);
                        float cl = UiChrome.ContrastRatio(UiChrome.Ink(lower, enabled), bg);

                        Assert.GreaterOrEqual(ch, cl,
                            $"{LogPrefix} 바탕 {Hex(bg)} · {(enabled ? "활성" : "비활성")}에서 " +
                            $"{higher}({ch:F2}:1)가 {lower}({cl:F2}:1)보다 흐립니다 — <b>서열이 뒤집혔습니다</b>. " +
                            "비활성은 한 단만 내리고 행 안의 상대 순서는 절대 바꾸지 않습니다. " +
                            "'못 쓴다'는 사실은 컨트롤과 행 바탕이 말하지, 글자가 말하지 않습니다.");
                    }
                }
            }
        }

        /// <summary>비활성은 <b>한 단만</b> 내린다 — 더 흐려지지도, 더 진해지지도 않는다.</summary>
        [Test]
        public void DisabledDropsAtMostOneStepAndNeverBrightens()
        {
            foreach (Color bg in Backdrops)
            {
                foreach (UiChrome.InkRole role in RankedRoles)
                {
                    float on = UiChrome.ContrastRatio(UiChrome.Ink(role, true), bg);
                    float off = UiChrome.ContrastRatio(UiChrome.Ink(role, false), bg);

                    Assert.LessOrEqual(off, on + 1e-4f,
                        $"{LogPrefix} {role}이(가) 비활성일 때 오히려 진해집니다({off:F2} > {on:F2}, 바탕 {Hex(bg)}).");

                    // "한 단"의 정의: 비활성 값은 <b>사다리에 실재하는 다음 칸</b>이어야 한다.
                    // 임의의 중간색을 새로 만들어 끼우면 4단이 5단이 되고, 그 순간 §2.3이 폐기한
                    // "위계를 만들지 못하면서 가독성만 갉아먹는 단"이 다시 생긴다.
                    Color offInk = UiChrome.Ink(role, false);
                    bool onLadder = offInk == UiChrome.TextPrimary || offInk == UiChrome.TextSecondary
                                    || offInk == UiChrome.TextTertiary;
                    Assert.IsTrue(onLadder,
                        $"{LogPrefix} {role}의 비활성 잉크 {Hex(offInk)}가 글자 사다리(T1/T2/T3) 밖의 값입니다 — " +
                        "새 단을 만들지 마십시오.");
                }
            }
        }

        /// <summary>
        /// ★ 메타(이유 문장)는 <b>비활성에서도 흐려지지 않는다</b>. 이건 부수 효과가 아니라 규칙이다.
        /// </summary>
        [Test]
        public void ReasonLineNeverDims()
        {
            Assert.AreEqual(UiChrome.Ink(UiChrome.InkRole.Meta, true),
                UiChrome.Ink(UiChrome.InkRole.Meta, false),
                $"{LogPrefix} 비활성 이유 문장이 흐려졌습니다. 비활성 행에서 가장 중요한 글자는 " +
                "'왜 못 쓰는가'를 말하는 그 한 줄입니다.");
            Assert.AreEqual(UiChrome.Ink(UiChrome.InkRole.Meta, true), UiChrome.InkMeta,
                $"{LogPrefix} InkMeta가 Ink(Meta, ...)와 다른 값을 돌려줍니다 — 진실이 두 곳으로 갈라졌습니다.");
        }

        /// <summary>
        /// 실제 표면 조합 그대로의 검산 — 비활성 행에서 <b>이름이 캡션보다 진한가</b>.
        /// <para>설정창 행은 캡션이 활성 여부와 무관하게 메타 단이다(<c>SettingsControls.BeginRow</c>).
        /// 그래서 "비활성 이름 vs 활성 캡션"이라는 <b>가장 불리한 조합</b>이 실제로 화면에 나온다 —
        /// 옛 코드가 정확히 여기서 2.09 &lt; 5.33으로 뒤집혔다.</para>
        /// </summary>
        [Test]
        public void DisabledRowTitleStillOutranksItsAlwaysOnCaption()
        {
            foreach (Color bg in Backdrops)
            {
                float title = UiChrome.ContrastRatio(UiChrome.InkTitle(false), bg);
                float caption = UiChrome.ContrastRatio(UiChrome.InkMeta, bg);
                Assert.Greater(title, caption,
                    $"{LogPrefix} 바탕 {Hex(bg)}: 비활성 제목 {title:F2}:1 ≤ 항상 켜진 캡션 {caption:F2}:1. " +
                    "유저는 '뭔가가 준비 중이구나'는 읽고 '뭐가?'는 못 읽습니다.");
            }
        }

        /// <summary>계단 간격이 균일한가 — 한 칸이 너무 좁으면 두 단이 같은 색으로 읽힌다.</summary>
        [Test]
        public void LadderStepsAreDistinguishable()
        {
            Color[] ladder = { UiChrome.TextPrimary, UiChrome.TextSecondary, UiChrome.TextTertiary };
            foreach (Color bg in Backdrops)
            {
                for (int i = 1; i < ladder.Length; i++)
                {
                    float hi = UiChrome.ContrastRatio(ladder[i - 1], bg);
                    float lo = UiChrome.ContrastRatio(ladder[i], bg);
                    float step = hi / lo;
                    Assert.Greater(step, 1.25f,
                        $"{LogPrefix} 바탕 {Hex(bg)}에서 T{i}({hi:F2})과 T{i + 1}({lo:F2})의 배수가 {step:F2}배뿐입니다. " +
                        "§2.3이 폐기한 두 단(4.52 vs 3.80 = 1.19배)이 바로 이 상태였습니다 — " +
                        "위계를 만들지 못하면서 가독성만 갉아먹습니다.");
                }
            }
        }

        // ==================================================================================
        // 3. ★ 네거티브 컨트롤 — 옛 값으로 되돌리면 정말로 빨개지는가
        // ==================================================================================

        /// <summary>
        /// 폐기된 세 값이 <b>실제로</b> AA를 못 넘는지 확인한다. 못 넘어야 정상이다.
        /// <para>이게 통과하지 못하면 위의 모든 초록은 "무엇이든 통과하는 초록"일 수 있다.</para>
        /// </summary>
        [Test]
        public void RetiredInksActuallyFailAa_NegativeControl()
        {
            var retired = new (string Name, Color Value)[]
            {
                ("옛 TextQuaternary", UiChrome.RetiredInk.Quaternary),
                ("옛 TabInactive", UiChrome.RetiredInk.TabInactive),
                ("옛 TextDisabled", UiChrome.RetiredInk.Disabled),
            };

            foreach (var r in retired)
            {
                float worst = WorstContrast(r.Value, out Color bg);
                Assert.Less(worst, UiChrome.MinTextContrast,
                    $"{LogPrefix} {r.Name}({Hex(r.Value)})이(가) 바탕 {Hex(bg)}에서 {worst:F2}:1로 " +
                    $"하한 {UiChrome.MinTextContrast:F1}:1을 <b>넘었습니다</b>. 이 테스트의 전제가 깨졌다는 뜻이므로 " +
                    "대비 계산이나 바탕 목록이 제대로 돌고 있는지 먼저 의심해야 합니다.");
            }
        }

        /// <summary>
        /// ★ <b>옛 배치를 그대로 재현하면 위계 검사가 실패하는가.</b>
        /// 실측된 그 조합(이름 = TextDisabled, 이유 = TextQuaternary)을 넣어 본다.
        /// </summary>
        [Test]
        public void OldPairingReallyInvertsTheRank_NegativeControl()
        {
            foreach (Color bg in Backdrops)
            {
                float oldName = UiChrome.ContrastRatio(UiChrome.RetiredInk.Disabled, bg);
                float oldReason = UiChrome.ContrastRatio(UiChrome.RetiredInk.Quaternary, bg);
                Assert.Less(oldName, oldReason,
                    $"{LogPrefix} 옛 조합이 바탕 {Hex(bg)}에서 역전되지 않았습니다(이름 {oldName:F2} vs 이유 {oldReason:F2}). " +
                    "실측(ActionCommandPopover 옛 :627/:639)은 2.10 < 3.51이었습니다 — " +
                    "재현되지 않으면 이 검사가 무엇을 재고 있는지부터 확인해야 합니다.");
            }

            // 그리고 새 조합에서는 그 역전이 사라져야 한다.
            foreach (Color bg in Backdrops)
            {
                Assert.Greater(UiChrome.ContrastRatio(UiChrome.InkTitle(false), bg),
                    UiChrome.ContrastRatio(UiChrome.InkMeta, bg),
                    $"{LogPrefix} 새 조합도 바탕 {Hex(bg)}에서 역전돼 있습니다.");
            }
        }

        /// <summary>
        /// 대비 계산기 자체의 네거티브 컨트롤 — 같은 색끼리는 1.00, 흑백은 21.00이어야 한다.
        /// (WCAG 정의상 그 둘이 이론적 양 끝이다.)
        /// </summary>
        [Test]
        public void ContrastFormulaHasTheKnownEndpoints_NegativeControl()
        {
            Assert.AreEqual(1f, UiChrome.ContrastRatio(UiChrome.TextPrimary, UiChrome.TextPrimary), 1e-4f,
                $"{LogPrefix} 같은 색의 대비가 1.00이 아닙니다 — 계산기가 고장 났습니다.");
            Assert.AreEqual(21f, UiChrome.ContrastRatio(Color.black, Color.white), 1e-3f,
                $"{LogPrefix} 흑백 대비가 21.00이 아닙니다 — 상대 휘도 식을 확인하십시오.");
        }

        // ==================================================================================
        // 4. 콜사이트 — 폐기된 이름이 되살아나지 않았는가
        // ==================================================================================

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string UiChromePath =>
            Path.Combine(ScriptsRoot, "Interaction", "UiChrome.cs");

        /// <summary>
        /// <c>RetiredInk</c>는 <b>테스트 전용</b>이다. 프로덕션이 참조하면 폐기가 폐기가 아니게 된다.
        /// </summary>
        [Test]
        public void NoProductionCodeReferencesRetiredInk()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot), $"{LogPrefix} 소스 루트를 찾지 못했습니다: {ScriptsRoot}");

            var offenders = new List<string>();
            int scanned = 0;
            foreach (string path in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').Contains("/Tests/")) continue;
                if (path == UiChromePath) continue;   // 선언 자리 자체는 예외.
                scanned++;
                if (File.ReadAllText(path).Contains("RetiredInk"))
                    offenders.Add(Path.GetFileName(path));
            }

            Assert.Greater(scanned, 10,
                $"{LogPrefix} 훑은 파일이 {scanned}개뿐입니다 — 스캔 경로가 틀렸을 가능성이 큽니다(거짓 초록).");
            CollectionAssert.IsEmpty(offenders,
                $"{LogPrefix} 프로덕션이 RetiredInk를 참조합니다: {string.Join(", ", offenders)}. " +
                "그 값들은 '되돌리면 빨개지는가'를 증명하는 네거티브 컨트롤 전용입니다.");
        }

        /// <summary>
        /// 옛 토큰 이름이 새로 생기지 않았는가. 지금은 지워져 있어서 <b>컴파일 에러</b>가 1차 방어선이고,
        /// 이 검사는 "이름만 되살려 두는" 우회를 막는 2차 방어선이다.
        /// </summary>
        [Test]
        public void RetiredTokenNamesAreNotRedeclared()
        {
            // RetiredInk 안에는 옛 이름이 <b>일부러</b> 남아 있다(네거티브 컨트롤). 그 블록만 도려내고 본다 —
            // 이 스캔이 잡아야 하는 것은 "토큰이 UiChrome 본체로 되돌아온 것"이다.
            string src = StripRetiredInkBlock(File.ReadAllText(UiChromePath));
            foreach (string name in new[] { "TextQuaternary", "TabInactive", "TextDisabled" })
            {
                Match m = Regex.Match(src, @"public\s+static\s+readonly\s+Color\s+" + name + @"\s*=");
                Assert.IsFalse(m.Success,
                    $"{LogPrefix} 폐기된 토큰 '{name}'이(가) UiChrome에 다시 선언됐습니다. " +
                    "이름이 돌아오면 콜사이트가 따라 돌아옵니다 — 그것이 §11의 결함이 " +
                    "두 창에서 독립적으로 재발한 경로였습니다.");
            }

            // 네거티브 컨트롤 (1): 이 정규식이 <b>살아 있는</b> 토큰은 실제로 찾는가.
            Assert.IsTrue(Regex.IsMatch(src, @"public\s+static\s+readonly\s+Color\s+TextPrimary\s*="),
                $"{LogPrefix} 정규식이 TextPrimary조차 못 찾습니다 — 위 검사는 아무것도 확인하지 않고 있습니다.");

            // 네거티브 컨트롤 (2): 도려내기가 <b>너무 많이</b> 지우지 않았는가.
            // (RetiredInk 블록만 사라지고 본체는 온전해야 한다.)
            string whole = File.ReadAllText(UiChromePath);
            Assert.Less(src.Length, whole.Length,
                $"{LogPrefix} RetiredInk 블록을 한 글자도 못 도려냈습니다 — 그러면 위 검사는 " +
                "네거티브 컨트롤 자체를 위반으로 신고하게 됩니다.");
            Assert.Greater(src.Length, whole.Length / 2,
                $"{LogPrefix} 도려낸 양이 너무 많습니다({whole.Length - src.Length}자) — 중괄호 세기가 " +
                "어긋나 파일 뒷부분이 통째로 사라졌을 가능성이 큽니다(그 상태의 초록은 가짜입니다).");
            Assert.IsTrue(Regex.IsMatch(whole, @"class\s+RetiredInk"),
                $"{LogPrefix} RetiredInk 자체가 사라졌습니다 — 네거티브 컨트롤이 없어졌습니다.");
        }

        /// <summary>중첩된 <c>RetiredInk</c> 클래스 본문을 중괄호 세기로 도려낸다. 없으면 원본 그대로.</summary>
        private static string StripRetiredInkBlock(string source)
        {
            Match head = Regex.Match(source, @"public\s+static\s+class\s+RetiredInk");
            if (!head.Success) return source;

            int open = source.IndexOf('{', head.Index);
            if (open < 0) return source;

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                    return source.Substring(0, head.Index) + source.Substring(i + 1);
            }
            return source;
        }
    }
}
