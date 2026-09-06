using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>UI 알파 비침 정책</b>의 회귀 잠금 — 2026-09-06
    /// (<c>docs/UI_ALPHA_BLEED_POLICY.md</c>, design-art).
    ///
    /// ============================================================================
    /// 무엇을 지키나
    /// ============================================================================
    /// uGUI 기본 셰이더는 <c>Blend SrcAlpha OneMinusSrcAlpha</c>를 <b>알파 채널에도</b> 적용한다
    /// (<c>dstA' = srcA² + dstA(1−srcA)</c>). 그래서 α&lt;1인 색을 <see cref="Image.color"/>에
    /// 그대로 넣으면 그 화소의 <b>창 알파가 내려가고 유저의 바탕화면이 비친다</b>:
    /// 불투명 겹 위에서 <c>α(1−α)</c>(α0.55 → <b>24.75 %</b>), 프레임버퍼 직접이면
    /// <c>1−α²</c>(α0.55 → <b>69.75 %</b>).
    ///
    /// <para>고치는 방법은 토큰을 바꾸는 것이 아니라 <b>호출부에서 합성</b>하는 것이다
    /// (§1-B). 이 파일은 그 규칙이 <b>소스에서</b> 지켜지는지(①), 합성해도 <b>보이는 색과
    /// 서열이 그대로인지</b>(③), 그리고 이번 라운드가 새로 만든 밝은 면 위에서
    /// <b>글자가 살아 있는지</b>(②④)를 잰다.</para>
    ///
    /// ============================================================================
    /// 네 검사 전부에 <b>대조</b>가 붙어 있다
    /// ============================================================================
    /// "고친 뒤 통과"만으로는 아무것도 증명하지 못한다. 그래서 각 검사는 <b>같은 판정기</b>에
    /// 옛 배선을 물려 <b>실제로 빨개지는지</b>를 같은 테스트 안에서 확인한다.
    /// 특히 ①은 스캐너 자신이 <b>0건을 세는 것과 아무것도 안 세는 것</b>을 구분하지 못하면
    /// 영원히 초록이므로(이 저장소의 거짓 통과 형태 그대로), 훑은 <b>파일 수</b>와
    /// <b>토큰 수</b>를 함께 단언하고 래퍼 목록을 비운 대조를 돌린다.
    /// </summary>
    public sealed class UiAlphaBleedPolicyAuditTests
    {
        private const string LogPrefix = "[알파비침-TEST]";

        /// <summary>두 색이 「다른 색」으로 읽히는 하한(ΔE*ab). PALETTE_SPEC §12-0이 쓰는 자다.
        /// <b>프로덕션 상수의 사본이 아니다</b> — 프로덕션에는 이 값이 없다
        /// (<c>RarityBorderSuccessionTests</c>와 같은 값을 같은 이유로 쓴다).</summary>
        private const float DistinctFloor = 7.8f;

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        /// <summary>
        /// 합성을 <b>대신 해 주는</b> 문들. 이 안에 들어 있으면 α는 이미 접혔다.
        ///
        /// <para>★ <c>Fade(</c>는 <b>일부러 넣지 않았다</b>. 정책 §4-1은 <c>Fade</c>를
        /// "<b>위 둘 중 하나</b>를 감싼 뒤의 애니메이션 α"로만 허용한다 — <c>Fade</c>를 래퍼로 인정하면
        /// <c>Fade(AccentBorder, α)</c>(= 이번 라운드가 고친 부채꼴의 결함 <b>그 자체</b>)가
        /// 통과해 버린다. 지금 남아 있는 <c>Fade(UiChrome.…)</c> 호출은 전부 α=1 토큰
        /// (PanelSurface / TextPrimary / Accent / WarmAccent / OnAccentSolid)이라 애초에
        /// 이 검사의 대상이 아니다.</para>
        /// </summary>
        private static readonly string[] Wrappers =
        {
            nameof(UiChrome.Flatten),
            nameof(UiChrome.EdgeOnSurface),
            nameof(UiChrome.ControlFaceOnSurface),
            nameof(UiChrome.InkOnSurface),
        };

        /// <summary>정책이 <b>덮지 않는</b> 경로(§4-4) — uGUI <see cref="Image"/>가 아니라
        /// <c>LineRenderer</c>다. 머티리얼이 <c>Blend One OneMinusSrcAlpha</c>라 같은 식을 쓰면
        /// 계산이 거짓말을 한다. <b>조용히 빼지 않고 여기 적어 둔다</b> — 별건 측정 대상(§8 W-1).</summary>
        private static readonly string[] NotUguiRenderers =
        {
            "WindowTheftRenderer.cs",
            // ★ 2026-09-06 — "FocusWatchRenderer.cs"를 뺐다. 그 파일이 삭제돼(발밑 타이머 링 제거,
            //   사용자 지시) 존재하지 않는 이름을 계속 건너뛰는 <b>죽은 니들</b>이 되기 때문이다.
        };

        // ============================================================================
        // ① 소스 감사 — α<1 토큰이 합성 밖에서 그리기에 도달하는가
        // ============================================================================

        [Test]
        public void α토큰과_등급테두리는_전부_합성_안에서만_쓰인다()
        {
            List<string> tokens = AlphaTokenNames();
            Assert.Greater(tokens.Count, 0,
                $"{LogPrefix} α<1 토큰을 하나도 못 찾았습니다 — 리플렉션이 실패했다면 이 검사는 " +
                "무엇이든 통과시키는 빈 조건입니다.");

            List<string> hits = Scan(tokens, Wrappers, out int scanned);

            Assert.Greater(scanned, 20,
                $"{LogPrefix} 훑은 프로덕션 파일이 {scanned}개뿐입니다 — 경로가 틀리면 위반 0건과 " +
                "<b>아무것도 안 본 것</b>이 똑같이 생깁니다(이 저장소의 거짓 통과 형태).");

            Assert.IsEmpty(hits,
                $"{LogPrefix} α<1 토큰이 합성({string.Join("/", Wrappers)}) 밖에서 쓰였습니다 — " +
                "그 화소의 창 알파가 내려가 유저의 바탕화면이 비칩니다. " +
                $"UiChrome.Flatten(토큰, 실제 바탕) 또는 UiChrome.EdgeOnSurface(바탕)으로 감싸십시오.\n  " +
                string.Join("\n  ", hits));

            // ★ 대조 — 래퍼 목록을 비우면 <b>같은 스캐너</b>가 실제로 잡아내는가.
            //   여기서 0건이면 위쪽 초록은 "규칙이 지켜졌다"가 아니라 "아무것도 안 봤다"이다.
            List<string> control = Scan(tokens, new string[0], out int controlScanned);
            Assert.AreEqual(scanned, controlScanned, $"{LogPrefix} 대조가 다른 파일 수를 훑었습니다.");
            Assert.Greater(control.Count, 20,
                $"{LogPrefix} ★대조 실패 — 래퍼를 전부 지웠는데도 {control.Count}건만 잡혔습니다. " +
                "스캐너가 토큰을 못 보고 있으므로 위의 통과는 무효입니다.");

            Debug.Log($"{LogPrefix} 소스 {scanned}개 · α토큰 {tokens.Count}종 — 합성 밖 사용 0건 " +
                      $"(대조: 래퍼를 지우면 {control.Count}건).");
        }

        /// <summary>★ 손으로 굴린 <c>EdgeOnSurface</c>(리터럴 흰 α0.18)가 <b>되살아나지 않았는가</b>.
        /// <para>부재 단언은 썩으면 <b>조용히 초록</b>이 된다(CLAUDE.md). 그래서 같은 테스트에서
        /// 대체물(<see cref="UiChrome.EdgeOnSurface"/>)이 <b>실재하는지</b>를 함께 못 박는다 —
        /// 둘 중 하나만 성립하면 이 검사는 거짓말이다.</para></summary>
        [Test]
        public void 손으로_굴린_테두리_리터럴은_규칙에_흡수되어_사라졌다()
        {
            string portrait = File.ReadAllText(
                Path.Combine(ScriptsRoot, "Interaction", "CharacterInfoWindow.cs"));

            Assert.IsTrue(portrait.Contains(nameof(UiChrome.EdgeOnSurface)),
                $"{LogPrefix} 액자/견본이 EdgeOnSurface를 부르지 않습니다 — 아래 부재 단언이 " +
                "「고쳤다」가 아니라 「그 코드가 통째로 사라졌다」를 뜻하게 됩니다.");

            Assert.IsFalse(Regex.IsMatch(portrait, @"new\s+Color\s*\(\s*1f\s*,\s*1f\s*,\s*1f\s*,\s*0\.18f\s*\)"),
                $"{LogPrefix} 흰 α0.18 리터럴이 되살아났습니다 — 그 값은 목탄 무대에서 1.79:1, " +
                "종이 무대에서 1.02:1이었습니다(밝은 바탕에서 흰 α는 원리상 실패합니다).");

            // 대조 — 정규식 자체가 살아 있는가(같은 자를 옛 문자열에 대 본다).
            Assert.IsTrue(Regex.IsMatch("x = new Color(1f, 1f, 1f, 0.18f);",
                    @"new\s+Color\s*\(\s*1f\s*,\s*1f\s*,\s*1f\s*,\s*0\.18f\s*\)"),
                $"{LogPrefix} ★대조 실패 — 니들이 옛 문자열조차 못 찾습니다. 위 부재 단언은 " +
                "어떤 소스에서도 통과하는 빈 조건입니다.");
        }

        // ============================================================================
        // ② F1 — 면과 잉크는 <b>한 쌍</b>이다
        // ============================================================================

        /// <summary>
        /// §9-2가 요구하는 형태 그대로: 면만 재는 검사는 <b>잉크 붕괴를 못 본다</b>.
        /// 두 하한을 <b>같은 반복 안에서</b> 함께 잰다.
        /// </summary>
        [Test]
        public void F1_슬래브는_면과_잉크가_동시에_하한을_넘는다()
        {
            int judged = 0;
            foreach ((string name, Color face, Color backdrop) in F1Faces())
            {
                float onBackdrop = UiChrome.ContrastRatio(face, backdrop);
                Assert.GreaterOrEqual(onBackdrop, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} {name} 면이 바탕 대비 {onBackdrop:F2}:1입니다 — 「누를 수 있는 것」이 " +
                    "면으로 보이지 않으면 그 버튼은 화면에 없는 것과 같습니다.");

                foreach (bool enabled in new[] { true, false })
                {
                    Color ink = UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled);
                    float ratio = UiChrome.ContrastRatio(ink, face);
                    Assert.GreaterOrEqual(ratio, UiChrome.MinTextContrast,
                        $"{LogPrefix} {name}({(enabled ? "활성" : "비활성")}) 라벨이 " +
                        $"{Hex(ink)} on {Hex(face)} = {ratio:F2}:1입니다 — 면을 올리고 잉크를 " +
                        "안 따라 올리면 글자가 지워집니다(정책 §2-E).");
                    judged++;
                }
            }

            Assert.AreEqual(4, judged,
                $"{LogPrefix} F1 면 2종 × 활성/비활성 = 4칸을 재야 하는데 {judged}칸만 쟀습니다.");

            // ★ 대조 — 이 면들이 실제로 <b>적대적인가</b>. 사다리 잉크가 그대로 통한다면
            //   위 초록은 "InkOnSurface로 갈아탄 것"을 아무것도 증명하지 않는다.
            foreach ((string name, Color face, Color _) in F1Faces())
            {
                foreach (bool enabled in new[] { true, false })
                {
                    float raw = UiChrome.ContrastRatio(UiChrome.Ink(UiChrome.InkRole.Title, enabled), face);
                    Assert.Less(raw, UiChrome.MinTextContrast,
                        $"{LogPrefix} ★대조 실패 — {name} 위에서 날것의 사다리 Title" +
                        $"({(enabled ? "활성" : "비활성")})가 {raw:F2}:1로 AA를 넘었습니다. " +
                        "그렇다면 이 면은 밝은 면이 아니고 이 파일이 지키는 사고도 실재하지 않습니다.");
                }
            }
        }

        // ============================================================================
        // ③ 등급 테두리 — 평탄화해도 <b>보이는 것</b>이 그대로인가
        // ============================================================================

        [Test]
        public void 등급_테두리는_평탄화_후에도_불투명하고_서열과_변별이_그대로다()
        {
            Color[] backdrops = { UiChrome.CardSurface, UiChrome.CardSurfaceMuted, UiChrome.ThumbSurfaceLocked };
            int judged = 0, pairs = 0;

            foreach (Color bg in backdrops)
            {
                var flats = new List<Color>();
                foreach (ItemRarity r in Enum.GetValues(typeof(ItemRarity)))
                {
                    Color flat = UiChrome.Flatten(UiChrome.RarityBorder(r), bg);
                    Assert.AreEqual(1f, flat.a, 1e-5f,
                        $"{LogPrefix} {ItemCatalog.RarityName(r)} 테두리를 {Hex(bg)} 위에 합성했는데 " +
                        $"α가 {flat.a:F3}입니다 — 합성의 <b>유일한</b> 목적이 α=1입니다.");
                    flats.Add(flat);
                    judged++;
                }

                for (int i = 1; i < flats.Count; i++)
                {
                    float d = DeltaE(flats[i - 1], flats[i]);
                    pairs++;
                    Assert.GreaterOrEqual(d, DistinctFloor,
                        $"{LogPrefix} {Hex(bg)} 위에서 인접 등급 ΔE {d:F2} < 하한 {DistinctFloor} — " +
                        "합성이 등급을 뭉갰다면 그건 「보이는 색이 그대로」가 아닙니다.");
                }

                // 4상태 서열: 선택 > 호버 > 착용 > 가장 밝은 등급.
                float brightest = 0f;
                foreach (Color f in flats)
                    brightest = Mathf.Max(brightest, UiChrome.ContrastRatio(f, bg));
                float hover = UiChrome.ContrastRatio(UiChrome.Flatten(UiChrome.CardBorderHover, bg), bg);
                float worn = UiChrome.ContrastRatio(UiChrome.CardBorderWorn, bg);
                float selected = UiChrome.ContrastRatio(UiChrome.TextPrimary, bg);

                Assert.Greater(hover, brightest,
                    $"{LogPrefix} {Hex(bg)} 위에서 호버({hover:F2})가 가장 밝은 등급({brightest:F2})보다 " +
                    "어둡습니다 — 상태를 얹었는데 테두리가 어두워지는 역전입니다.");
                Assert.Greater(worn, brightest, $"{LogPrefix} 착용 테두리가 등급 최대보다 어둡습니다.");
                Assert.Greater(selected, hover, $"{LogPrefix} 선택이 호버보다 어둡습니다.");
            }

            int steps = Enum.GetValues(typeof(ItemRarity)).Length;
            Assert.AreEqual(steps * backdrops.Length, judged,
                $"{LogPrefix} {steps}단 × 바탕 {backdrops.Length}종을 재야 하는데 {judged}칸만 쟀습니다.");
            Assert.AreEqual((steps - 1) * backdrops.Length, pairs,
                $"{LogPrefix} 인접 쌍 수가 맞지 않습니다({pairs}).");

            // ★ 대조 — 합성 <b>전</b> 값은 실제로 α<1이어야 한다(아니면 이 라운드는 아무것도 안 고쳤다).
            foreach (ItemRarity r in Enum.GetValues(typeof(ItemRarity)))
            {
                float a = UiChrome.RarityBorder(r).a;
                Assert.Less(a, 1f,
                    $"{LogPrefix} ★대조 실패 — 등급 테두리 원본 α가 {a:F2}입니다. α<1이 아니면 " +
                    "비침 자체가 없었고 이 검사는 빈 조건입니다.");
                float bleed = a * (1f - a);
                Assert.Greater(bleed, 0.2f,
                    $"{LogPrefix} ★대조 실패 — 원본 비침이 {bleed:P2}뿐입니다(실측 24.75 % 재현 실패).");
            }
        }

        // ============================================================================
        // ④ 게이트 회귀 — §7-3 ★ "이걸 안 고치면 이번 수정이 새 1.28:1 사고를 만든다"
        // ============================================================================

        [Test]
        public void 게이트가_내려가도_F1_버튼_라벨이_지워지지_않는다()
        {
            var trash = new List<GameObject>();
            try
            {
                Text rowLabel = NewText(trash);      // 카드 바탕 위(행 라벨)
                Text buttonLabel = NewText(trash);   // F1 슬래브 위(버튼 라벨)

                var gate = new SettingsRowGate("테스트 사유");
                gate.Register(
                    new[]
                    {
                        SettingsRowGate.GatedInk.OnCard(rowLabel),
                        new SettingsRowGate.GatedInk(buttonLabel, SettingsControls.ControlFaceOnCard),
                    },
                    null, null, null, null);

                gate.SetEnabled(false);

                float onSlab = UiChrome.ContrastRatio(buttonLabel.color, SettingsControls.ControlFaceOnCard);
                Assert.GreaterOrEqual(onSlab, UiChrome.MinTextContrast,
                    $"{LogPrefix} 게이트가 내려간 뒤 버튼 라벨이 " +
                    $"{Hex(buttonLabel.color)} on {Hex(SettingsControls.ControlFaceOnCard)} = " +
                    $"{onSlab:F2}:1입니다 — 2026-09-02 「1.28:1 사고」의 재발 형태입니다.");

                // 카드 위 글자는 <b>값이 바뀌면 안 된다</b> — 이 수정은 사다리를 건드리지 않는다.
                Assert.AreEqual(UiChrome.Ink(UiChrome.InkRole.Title, false), rowLabel.color,
                    $"{LogPrefix} 카드 위 행 라벨이 사다리에서 벗어났습니다 — 어두운 면에서는 " +
                    "InkOnSurface가 사다리를 그대로 돌려줘야 합니다(위계 보존).");

                // 다시 올리면 되돌아오는가(한 방향만 검사하면 절반만 보는 것이다).
                gate.SetEnabled(true);
                Assert.AreEqual(UiChrome.Ink(UiChrome.InkRole.Title, true), rowLabel.color,
                    $"{LogPrefix} 게이트를 다시 올렸는데 행 라벨이 활성 잉크로 돌아오지 않았습니다.");
                Assert.GreaterOrEqual(
                    UiChrome.ContrastRatio(buttonLabel.color, SettingsControls.ControlFaceOnCard),
                    UiChrome.MinTextContrast,
                    $"{LogPrefix} 게이트를 다시 올린 뒤 버튼 라벨이 하한 아래입니다.");

                // ★ 대조 — <b>옛 배선</b>(면을 모르는 게이트)이 실제로 1.77:1을 만드는가.
                float old = UiChrome.ContrastRatio(UiChrome.InkTitle(false),
                    SettingsControls.ControlFaceOnCard);
                Assert.Less(old, UiChrome.MinTextContrast,
                    $"{LogPrefix} ★대조 실패 — 옛 배선이 {old:F2}:1로 AA를 넘었습니다. " +
                    "그렇다면 §7-3 ★가 경고한 사고가 실재하지 않고 위 초록은 아무 조건도 아닙니다.");
                Assert.Less(old, 2f,
                    $"{LogPrefix} ★대조 실패 — 옛 배선이 {old:F2}:1입니다(실측 1.77:1 재현 실패).");
            }
            finally
            {
                foreach (GameObject go in trash)
                {
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        // ============================================================================
        // 판정 보조 — 본안과 대조가 <b>같은 함수</b>를 쓴다
        // ============================================================================

        private static IEnumerable<(string, Color, Color)> F1Faces()
        {
            yield return ("카드 위 슬래브", SettingsControls.ControlFaceOnCard, UiChrome.CardSurface);
            yield return ("창 바탕 위 슬래브", SettingsControls.ControlFaceOnPanel, UiChrome.PanelSurface);
        }

        private static Text NewText(List<GameObject> trash)
        {
            var go = new GameObject("GateLabel", typeof(RectTransform), typeof(Text));
            trash.Add(go);
            return go.GetComponent<Text>();
        }

        /// <summary>α&lt;1인 <see cref="UiChrome"/> 공개 색 토큰의 <b>이름</b>. 목록을 손으로 적지 않는다 —
        /// 새 토큰이 생기면 그날부터 자동으로 감사 대상이 된다.</summary>
        private static List<string> AlphaTokenNames()
        {
            var names = new List<string>();
            foreach (FieldInfo f in typeof(UiChrome).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType != typeof(Color)) continue;
                var c = (Color)f.GetValue(null);
                if (c.a < 1f) names.Add(f.Name);
            }
            return names;
        }

        /// <summary>
        /// 프로덕션 소스를 훑어 <paramref name="tokens"/>와 <c>RarityBorder(</c>가
        /// <paramref name="wrappers"/> <b>밖에서</b> 쓰인 자리를 모은다.
        ///
        /// <para>★ <b>괄호 균형은 파일 단위로 센다.</b> 줄 단위로 세면 여러 줄에 걸친
        /// <c>Flatten(</c> 호출의 인자를 위반으로 오탐한다(design-art의 첫 스캐너가
        /// <c>Cards.cs:509</c>에서 정확히 그 오탐을 냈다).</para>
        /// <para>★ 주석과 문자열은 <b>먼저 지운다</b> — 주석 속 <c>Flatten(</c> 한 개가
        /// 괄호 스택을 통째로 어긋나게 만든다.</para>
        /// </summary>
        private static List<string> Scan(List<string> tokens, string[] wrappers, out int scanned)
        {
            var hits = new List<string>();
            scanned = 0;

            Assert.IsTrue(Directory.Exists(ScriptsRoot), $"{LogPrefix} 소스 루트를 찾지 못했습니다: {ScriptsRoot}");

            foreach (string path in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string unix = path.Replace('\\', '/');
                if (unix.Contains("/Tests/")) continue;
                string file = Path.GetFileName(path);
                if (file == "UiChrome.cs") continue;                    // 토큰 <b>선언</b> 자리.
                if (Array.IndexOf(NotUguiRenderers, file) >= 0) continue;   // §4-4 — 다른 블렌드 경로.
                scanned++;

                string raw = File.ReadAllText(path);
                string src = StripNoise(raw);
                List<(int Start, int End, string Name)> spans = CallSpans(src);

                foreach (string token in tokens)
                {
                    AddHits(hits, path, raw, src, spans, wrappers, @"UiChrome\." + token + @"\b", token);
                }
                AddHits(hits, path, raw, src, spans, wrappers,
                    @"UiChrome\." + nameof(UiChrome.RarityBorder) + @"\s*\(", nameof(UiChrome.RarityBorder) + "()");
            }
            return hits;
        }

        private static void AddHits(List<string> hits, string path, string raw, string src,
            List<(int Start, int End, string Name)> spans, string[] wrappers, string pattern, string label)
        {
            foreach (Match m in Regex.Matches(src, pattern))
            {
                if (Wrapped(spans, m.Index, wrappers)) continue;
                int line = 1;
                for (int i = 0; i < m.Index && i < src.Length; i++) if (src[i] == '\n') line++;
                hits.Add($"{Path.GetFileName(path)}:{line}  {label}  |  {LineAt(raw, line)}");
            }
        }

        private static string LineAt(string raw, int line)
        {
            string[] lines = raw.Split('\n');
            if (line - 1 < 0 || line - 1 >= lines.Length) return string.Empty;
            string t = lines[line - 1].Trim();
            return t.Length > 110 ? t.Substring(0, 110) : t;
        }

        private static bool Wrapped(List<(int Start, int End, string Name)> spans, int index, string[] wrappers)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                if (index <= spans[i].Start || index >= spans[i].End) continue;
                if (Array.IndexOf(wrappers, spans[i].Name) >= 0) return true;
            }
            return false;
        }

        /// <summary>주석·문자열을 <b>같은 길이의 공백</b>으로 지운다(줄 번호가 보존된다).</summary>
        private static string StripNoise(string src)
        {
            var sb = new StringBuilder(src);
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    int j = src.IndexOf('\n', i);
                    if (j < 0) j = n;
                    Blank(sb, i, j);
                    i = j;
                }
                else if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    int j = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    j = j < 0 ? n : j + 2;
                    Blank(sb, i, j);
                    i = j;
                }
                else if (c == '"')
                {
                    int j = i + 1;
                    while (j < n && src[j] != '"')
                    {
                        if (src[j] == '\\') j++;
                        j++;
                    }
                    j = Mathf.Min(j + 1, n);
                    Blank(sb, i, j);
                    i = j;
                }
                else if (c == '\'')
                {
                    int j = i + 1;
                    while (j < n && src[j] != '\'')
                    {
                        if (src[j] == '\\') j++;
                        j++;
                    }
                    j = Mathf.Min(j + 1, n);
                    Blank(sb, i, j);
                    i = j;
                }
                else i++;
            }
            return sb.ToString();
        }

        private static void Blank(StringBuilder sb, int from, int to)
        {
            for (int k = from; k < to && k < sb.Length; k++)
            {
                if (sb[k] != '\n') sb[k] = ' ';
            }
        }

        /// <summary>여는 괄호마다 <b>바로 앞의 식별자</b>를 이름으로 달아 (시작, 끝) 구간을 만든다.</summary>
        private static List<(int Start, int End, string Name)> CallSpans(string src)
        {
            var spans = new List<(int, int, string)>();
            var stack = new Stack<(int Start, string Name)>();
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '(')
                {
                    int j = i - 1;
                    while (j >= 0 && char.IsWhiteSpace(src[j])) j--;
                    int end = j;
                    while (j >= 0 && (char.IsLetterOrDigit(src[j]) || src[j] == '_')) j--;
                    string name = end > j ? src.Substring(j + 1, end - j) : string.Empty;
                    stack.Push((i, name));
                }
                else if (c == ')' && stack.Count > 0)
                {
                    (int start, string name) = stack.Pop();
                    spans.Add((start, i, name));
                }
            }
            return spans;
        }

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ---- CIELAB(D65) + CIE76. RarityBorderSuccessionTests와 <b>같은 식</b>이다 ----

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

        /// <summary>★ 계산기 교정 — 이게 깨지면 위 ΔE 판정은 전부 무효다.</summary>
        [Test]
        public void 교정_ΔE_계산기가_외부_확인_가능한_값을_낸다()
        {
            Assert.AreEqual(0f, DeltaE(Color.white, Color.white), 1e-4f,
                $"{LogPrefix} 동일색 ΔE가 0이 아닙니다.");
            Assert.AreEqual(100f, DeltaE(Color.white, Color.black), 0.01f,
                $"{LogPrefix} 흰↔검 ΔE가 100이 아닙니다.");
            Assert.AreEqual(21f, UiChrome.ContrastRatio(Color.white, Color.black), 0.001f,
                $"{LogPrefix} 흰↔검 대비가 21이 아닙니다 — 대비 계산기가 고장났습니다.");
        }
    }
}
