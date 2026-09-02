using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 카드 하단 <b>[착용]/[해제]/[잠김]</b> 칩의 면 회귀 잠금 — 2026-09-02.
    /// <see cref="CloseChipAffordanceTests"/>([✕])와 <b>같은 결함, 같은 규율</b>이고,
    /// 이쪽은 카드 <b>24장 × 4탭</b>에 살아 있었다.
    ///
    /// ============================================================================
    /// 무엇이 있었나 (실측 — 계산기는 흰/검 21.00 · 동일색 1.00 으로 먼저 교정했다)
    /// ============================================================================
    /// <code>
    ///   상태   면 색      바탕       면 대비          글리프
    ///   착용   #32353C   #1B1F26    1.35 : 1  ✘      11.14 : 1  ✔
    ///   해제   #243143   #1B1F26    1.26 : 1  ✘       7.16 : 1  ✔
    ///   잠김   #15181E   #15181E    1.00 : 1  ✘✘      5.73 : 1  ✔
    /// </code>
    /// 글자는 셋 다 잘 읽혔다 — <b>고칠 것은 잉크가 아니라 면</b>이었다.
    ///
    /// <para>★ <b>잠김이 1.08이 아니라 1.00인 이유</b>(UX 핸드오프의 숫자를 그대로 믿지 않고 다시 잰
    /// 지점이다): 잠긴 카드는 카드 <b>바탕 자체</b>가 <see cref="UiChrome.CardSurfaceMuted"/>로 바뀐다.
    /// 즉 칩과 그 바탕의 RGB가 <b>완전히 같았다</b> — 오늘 밤 [✕]가 1.00:1이던 것과 같은 값,
    /// 같은 원인이다. 바탕을 잘못 잡으면 결함을 실제보다 <b>가볍게</b> 본다.</para>
    ///
    /// ============================================================================
    /// ★ 이 파일의 규율 — 면과 잉크를 <b>한 테스트 안에서</b> 쌍으로 잰다
    /// ============================================================================
    /// 둘은 반대 방향이다. 면을 밝히면 면 대비는 오르고 그 위의 밝은 잉크는 죽는다. 나눠서 재면
    /// "면 초록 / 글자 소멸" 회귀를 <b>구조적으로</b> 못 잡는다
    /// (<see cref="네거티브_면만_밝히고_잉크를_그대로_두면_글자가_죽는다"/>가 실물로 보여 준다).
    ///
    /// ============================================================================
    /// 왜 어둡게 해서 구분하지 않았나 — <b>가능한 선택지가 아니었다</b>
    /// ============================================================================
    /// 카드 바탕이 이미 어두워서 면을 <b>순검정까지</b> 내려도 최대 1.27:1(<see cref="UiChrome.CardSurface"/>) /
    /// 1.18:1(<see cref="UiChrome.CardSurfaceMuted"/>)이다. 3.0은 아래쪽에 <b>존재하지 않는다</b>
    /// (<see cref="전제_이_바탕에서는_면을_어둡게_해서_하한에_도달할_수_없다"/>가 잠근다).
    /// 그래서 면은 반드시 밝아지고, 두 활성 상태는 밝기가 아니라 <b>색상</b>으로 갈린다.
    ///
    /// <para><b>화면 픽셀은 여기서 못 잰다.</b> 이 파일은 값(토큰) 층만 본다. 실제로 그려진
    /// <c>Image.color</c> / 칩 크기 / <c>Button.transition</c> / <c>interactable</c>은 PlayMode의
    /// <c>InfoWindowSurfaceRegressionTests</c>가 <b>생성된 트리에서 읽어</b> 잰다. 둘 다 필요하다 —
    /// 값이 맞아도 호출부가 안 쓰면 화면은 그대로다.</para>
    /// </summary>
    public sealed class CardActionAffordanceTests
    {
        private const string LogPrefix = "[착용칩-TEST]";

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>8비트로 <b>양자화된</b> 색. 화면에 실제로 나가는 값은 이것이다.</summary>
        private static Color Quantized(Color c)
        {
            ColorUtility.TryParseHtmlString("#" + ColorUtility.ToHtmlStringRGB(c), out Color q);
            return q;
        }

        /// <summary>칩 한 상태 = (이름, 면, <b>그 상태의 진짜 바탕</b>, 잉크 역할, 활성 여부).
        /// <para>바탕이 상태마다 다르다는 것이 이 표의 핵심이다 — 잠긴 카드만 바탕이 갈아탄다.</para></summary>
        private static IEnumerable<(string name, Color face, Color backdrop, UiChrome.InkRole role, bool owned)> States()
        {
            yield return ("착용(owned, !worn)", UiChrome.CardActionSurface, UiChrome.CardSurface, UiChrome.InkRole.Title, true);
            yield return ("해제(worn)", UiChrome.CardActionSurfaceWorn, UiChrome.CardSurface, UiChrome.InkRole.Title, true);
            yield return ("잠김(!owned)", UiChrome.CardSurfaceMuted, UiChrome.CardSurfaceMuted, UiChrome.InkRole.Meta, false);
        }

        // ==================== ★ 교정 먼저 ====================

        /// <summary>★ 계산기가 <b>알려진 값</b>을 맞히는지 먼저 본다. 이게 깨지면 아래 숫자는 전부 폐기다.</summary>
        [Test]
        public void 교정_대비계산기가_알려진_값을_맞힌다()
        {
            Assert.AreEqual(21f, UiChrome.ContrastRatio(Color.white, Color.black), 0.0001f,
                $"{LogPrefix} 흰/검이 21.00이 아닙니다 — 계산기가 고장났으므로 이 파일의 모든 숫자를 버리십시오.");
            Assert.AreEqual(1f, UiChrome.ContrastRatio(UiChrome.CardSurface, UiChrome.CardSurface), 0.0001f,
                $"{LogPrefix} 동일색이 1.00이 아닙니다 — 계산기가 고장났습니다.");
            Debug.Log($"{LogPrefix} 교정 통과 — 흰/검 21.0000, 동일색 1.0000.");
        }

        // ==================== ① 본론: 면과 잉크를 한 쌍으로 ====================

        /// <summary>★ 이 파일의 본론. <b>면 하한과 잉크 하한을 한 테스트 안에서</b> 확인한다.
        ///
        /// <para><b>잠김은 면 하한에서 면제된다</b> — WCAG 2.2 1.4.11은 "inactive user interface
        /// components"에 대비 요구를 두지 않는다. 다만 그 면제는 <b>실제로 비활성일 때만</b> 유효하다:
        /// 그래서 프로덕션이 <c>button.interactable = owned</c>를 넣었고(예전에는 <c>true</c>인 채
        /// 클릭만 무시했다 — 면제를 받을 자격이 없는 상태였다), 그 배선은 PlayMode가 확인한다.
        /// 면제를 받아도 <b>글자는 여전히 읽혀야 한다</b>는 쪽은 여기서 그대로 잠근다.</para></summary>
        [Test]
        public void 카드_칩의_면과_그_위의_글자를_한_쌍으로_잰다()
        {
            foreach ((string name, Color face, Color backdrop, UiChrome.InkRole role, bool owned) in States())
            {
                Color qFace = Quantized(face);
                Color qBack = Quantized(backdrop);
                float faceRatio = UiChrome.ContrastRatio(qFace, qBack);

                Color ink = UiChrome.InkOnSurface(face, role, owned);
                float inkRatio = UiChrome.ContrastRatio(Quantized(ink), qFace);

                if (owned)
                {
                    Assert.GreaterOrEqual(faceRatio, UiChrome.MinNonTextContrast,
                        $"{LogPrefix} {name}의 <b>면</b> {Hex(face)}이 바탕 {Hex(backdrop)} 대비 {faceRatio:F2}:1입니다. " +
                        $"하한 {UiChrome.MinNonTextContrast:F1}:1 미만이면 글자가 아무리 선명해도 그것이 " +
                        "<b>누를 수 있는 것</b>이라는 신호가 없습니다(고치기 전 실측 1.35 / 1.26 / 1.00:1).");
                }

                // 잉크는 <b>세 상태 모두</b> 하한을 지킨다 — 비활성이라고 읽히지 않아도 되는 것은 아니다.
                Assert.GreaterOrEqual(inkRatio, UiChrome.MinTextContrast,
                    $"{LogPrefix} {name} 위의 글자 {Hex(ink)}가 {inkRatio:F2}:1입니다. " +
                    $"하한 {UiChrome.MinTextContrast:F1}:1 미만이면 '흐린 글자'가 아니라 <b>없는 글자</b>입니다 — " +
                    "면을 밝히면서 글자를 지운 것이므로 고친 것이 아니라 옮긴 것입니다.");

                Debug.Log($"{LogPrefix} {name} — 면 {Hex(face)} {faceRatio:F2}:1 / 잉크 {Hex(ink)} {inkRatio:F2}:1");
            }
        }

        // ==================== ② 네거티브 컨트롤 ====================

        /// <summary>★ <b>고치기 전 값이 실제로 이 검사에서 빨간불인가.</b> 이게 통과하지 않으면
        /// 위의 초록불은 "검사가 무르다"는 뜻일 수도 있다.</summary>
        [Test]
        public void 네거티브_고치기_전_옛_면들은_전부_하한을_깬다()
        {
            var old = new (string name, Color face, Color backdrop)[]
            {
                ("옛 착용", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.CardSurface),
                ("옛 해제", UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurface), UiChrome.CardSurface),
                ("옛 잠김", UiChrome.CardSurfaceMuted, UiChrome.CardSurfaceMuted),
            };

            foreach ((string name, Color face, Color backdrop) in old)
            {
                float r = UiChrome.ContrastRatio(Quantized(face), Quantized(backdrop));
                Assert.Less(r, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} {name} {Hex(face)}가 {r:F2}:1로 하한을 <b>넘어</b> 버렸습니다 — " +
                    "옛 값이 통과한다면 이 검사는 회귀를 잡지 못합니다(전제가 무너졌습니다).");
                Debug.Log($"{LogPrefix} 네거티브 확인 — {name} {Hex(face)} {r:F2}:1 (하한 미달, 의도대로).");
            }
        }

        /// <summary>★ <b>면만 밝히는 풀이는 실제로 무너진다.</b> 면을 3.0에 겨우 맞추면서 옛 잉크를
        /// 그대로 두면 글자가 죽는 것을 <b>실물로</b> 보여 준다 — 이 함정 때문에 두 지표를 나누지 않는다.</summary>
        [Test]
        public void 네거티브_면만_밝히고_잉크를_그대로_두면_글자가_죽는다()
        {
            // 면 하한만 겨우 만족시키는 최소 흰색 혼합을 찾는다(잉크는 옛 흰 글자 그대로 둔다).
            Color naiveFace = default;
            bool found = false;
            for (int i = 0; i <= 1024; i++)
            {
                Color f = UiChrome.Flatten(new Color(1f, 1f, 1f, i / 1024f), UiChrome.CardSurface);
                if (UiChrome.ContrastRatio(Quantized(f), Quantized(UiChrome.CardSurface)) >= UiChrome.MinNonTextContrast)
                {
                    naiveFace = f; found = true; break;
                }
            }
            Assert.IsTrue(found, $"{LogPrefix} 전제가 깨졌습니다 — 면 하한을 만족하는 혼합을 못 찾았습니다.");

            // 이 순진한 면은 면 검사만 보면 <b>초록</b>이다.
            float faceRatio = UiChrome.ContrastRatio(Quantized(naiveFace), Quantized(UiChrome.CardSurface));
            Assert.GreaterOrEqual(faceRatio, UiChrome.MinNonTextContrast);

            // 그런데 옛 잉크(TextPrimary)를 그대로 두면 글자가 하한 근처로 무너진다.
            float naiveInk = UiChrome.ContrastRatio(Quantized(UiChrome.TextPrimary), Quantized(naiveFace));

            // 그리고 <b>프로덕션 규칙</b>은 같은 자리에서 무너지지 않는다 — 잉크를 면에서 파생시키기 때문이다.
            Color ruleInk = UiChrome.InkOnSurface(UiChrome.CardActionSurface, UiChrome.InkRole.Title, true);
            float ruleRatio = UiChrome.ContrastRatio(Quantized(ruleInk), Quantized(UiChrome.CardActionSurface));

            Assert.Less(naiveInk, ruleRatio,
                $"{LogPrefix} 전제가 깨졌습니다 — 순진한 풀이({naiveInk:F2}:1)가 규칙({ruleRatio:F2}:1)만큼 " +
                "좋습니다. 그렇다면 이 네거티브 컨트롤은 아무것도 보여 주지 못합니다.");
            Assert.GreaterOrEqual(ruleRatio, UiChrome.MinTextContrast,
                $"{LogPrefix} 규칙이 고른 잉크가 {ruleRatio:F2}:1로 하한 미달입니다.");

            Debug.Log($"{LogPrefix} 면만 맞춘 풀이 {Hex(naiveFace)} — 면 {faceRatio:F2}:1(초록)인데 " +
                $"흰 글자는 {naiveInk:F2}:1. 규칙이 고른 면/잉크는 {ruleRatio:F2}:1.");
        }

        /// <summary>★ <b>아래쪽에는 답이 없다</b>는 전제를 값으로 잠근다. 이게 깨지면(=바탕이 밝아지면)
        /// "면을 어둡게 해서 조용히 유지한다"는 더 나은 풀이가 생기므로 설계를 다시 봐야 한다.</summary>
        [Test]
        public void 전제_이_바탕에서는_면을_어둡게_해서_하한에_도달할_수_없다()
        {
            foreach ((string name, Color backdrop) in new[]
            {
                (nameof(UiChrome.CardSurface), UiChrome.CardSurface),
                (nameof(UiChrome.CardSurfaceMuted), UiChrome.CardSurfaceMuted),
            })
            {
                float darkest = UiChrome.ContrastRatio(Color.black, backdrop);
                Assert.Less(darkest, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} {name} 위에서 순검정이 {darkest:F2}:1로 하한을 넘습니다 — " +
                    "이제는 면을 <b>어둡게</b> 해서 하한을 만족할 수 있다는 뜻이므로, 밝히는 선택을 재검토하십시오.");
                Debug.Log($"{LogPrefix} {name}: 아래쪽 최대 {darkest:F2}:1 (< {UiChrome.MinNonTextContrast:F1}) — 밝히는 수밖에 없다.");
            }
        }

        // ==================== ③ 창 알파의 법칙 ====================

        /// <summary>칩을 이루는 색은 전부 α=1이어야 한다. 반투명 겹을 하나라도 얹으면 그 화소에서
        /// <b>유저의 바탕화면이 비친다</b> — 옛 생 <c>CardBorder</c>(α0.10) 테두리가 정확히 그것이었고,
        /// 그래서 어두운 바탕화면일수록 <b>더</b> 안 보였다.</summary>
        [Test]
        public void 칩을_이루는_색은_전부_불투명하다()
        {
            foreach ((string name, Color face, Color _, UiChrome.InkRole __, bool ___) in States())
            {
                Assert.AreEqual(1f, face.a, 0.0001f,
                    $"{LogPrefix} {name}의 면 {Hex(face)}의 알파가 {face.a:F3}입니다 — " +
                    "그 화소에서 유저의 바탕화면이 비칩니다(창 알파 0.91 사고와 같은 자리).");
            }

            // 테두리도 Flatten을 거쳐야 한다.
            Color outline = UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardActionSurface);
            Assert.AreEqual(1f, outline.a, 0.0001f,
                $"{LogPrefix} 테두리 {Hex(outline)}의 알파가 1이 아닙니다.");
            Assert.AreNotEqual(1f, UiChrome.CardBorder.a,
                $"{LogPrefix} 전제가 깨졌습니다 — 생 CardBorder가 이미 불투명하다면 Flatten이 필요 없습니다.");

            Debug.Log($"{LogPrefix} 면 3종 + 테두리 전부 α=1 확인.");
        }

        // ==================== ④ P0-4 위계 가드가 살아 있는가 ====================

        /// <summary>★ 접근성 하한을 넘기느라 <b>P0-4를 다시 깨지 않았는가</b>.
        /// 한 화면에 12개가 반복되는 이 막대가 카드에서 가장 밝은 면이 되면 안 된다
        /// (PlayMode <c>CardEquipButtonNeverGoesBackToTheWhiteFill</c>과 <b>같은 기준</b>을 값 층에서 미리 잰다).</summary>
        [Test]
        public void 밝아졌어도_카드에서_가장_밝은_것은_여전히_아이템_쪽이다()
        {
            float whiteL = UiChrome.RelativeLuminance(UiChrome.TextPrimary);
            float cardL = UiChrome.RelativeLuminance(UiChrome.CardSurface);
            Assert.Greater(whiteL, cardL, $"{LogPrefix} 전제가 깨졌습니다(다크 테마가 뒤집혔습니까?).");
            float midpoint = (whiteL + cardL) * 0.5f;

            foreach ((string name, Color face, Color _, UiChrome.InkRole __, bool ___) in States())
            {
                float l = UiChrome.RelativeLuminance(Quantized(face));
                Assert.Less(l, midpoint,
                    $"{LogPrefix} {name} 면의 휘도({l:F4})가 흰 채움과 카드 바탕의 중간({midpoint:F4}) 이상입니다 — " +
                    "접근성을 고치면서 P0-4(한 화면 12개 반복)를 다시 깼습니다.");
                Debug.Log($"{LogPrefix} {name} 휘도 {l:F4} < 중간값 {midpoint:F4}.");
            }
        }

        /// <summary>두 활성 상태는 <b>서로 구분</b>돼야 한다 — 같은 색이면 "지금 입고 있는가"를
        /// 면이 말하지 못하고 낱말 하나에만 기대게 된다.</summary>
        [Test]
        public void 착용과_해제는_서로_다른_면을_쓴다()
        {
            Color a = Quantized(UiChrome.CardActionSurface);
            Color b = Quantized(UiChrome.CardActionSurfaceWorn);
            Assert.AreNotEqual(a, b,
                $"{LogPrefix} [착용]과 [해제]의 면이 {Hex(a)}로 같습니다 — 상태를 면이 말하지 못합니다.");
            Debug.Log($"{LogPrefix} 착용 {Hex(a)} vs 해제 {Hex(b)} — 구분됨.");
        }

        // ==================== ⑤ 누를 수 있는 크기 (WCAG 2.5.8) ====================

        /// <summary>★ 칩 높이가 <see cref="UiChrome.MinTargetSizePoints"/>에서 <b>파생</b>되는가.
        /// <para>숫자 24를 다시 베껴 넣는 회귀를 막는다 — 하한이 움직여도 리터럴은 따라오지 않는다.
        /// 그리고 그 높이가 카드 세로 예산 안에 실제로 들어가는지도 <b>소스에서 읽어</b> 검산한다.</para></summary>
        [Test]
        public void 칩_높이는_하한에서_파생되고_카드_예산_안에_들어간다()
        {
            // ★ 2026-09-02 — 이 창은 partial 7개다. 한 파일만 읽으면 카드 상수가 조각으로 이사하는
            //   순간 <b>프로덕션이 멀쩡한데도</b> 빨개진다(같은 라운드에 실제로 그렇게 깨진 매처가
            //   둘 있었다). 표면 전체를 읽는다 — SourceConstantReader.ReadSurfaceText 문단 참고.
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction", "CharacterInfoWindow.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");
            string src = SourceConstantReader.ReadSurfaceText(path);

            // ---- 양성 대조: 이 판독기가 <b>조각까지 실제로 보는가</b> ----
            // MinPanelWidth는 CharacterInfoWindow.Layout.cs에만 있는 상수다. 베이스 파일만 읽으면
            // 못 찾고, 표면 전체를 읽으면 찾아야 한다. 둘 다 확인해야 "넓혔다"가 증명된다.
            Assert.IsFalse(SourceConstantReader.TryReadFloat(path, "MinPanelWidth", out _),
                $"{LogPrefix} 양성 대조 전제가 깨졌습니다 — MinPanelWidth가 베이스 파일로 돌아왔습니다. " +
                "조각에만 있는 다른 상수로 탐침을 바꾸십시오(안 그러면 이 대조가 공허해집니다).");
            Assert.IsTrue(SourceConstantReader.TryReadFloatInSurface(path, "MinPanelWidth", out float probe),
                $"{LogPrefix} 양성 대조 실패 — 표면 판독기가 partial 조각을 보지 못합니다. " +
                "이 파일은 다음 분할에서 또 눈이 멉니다.");
            Assert.Greater(probe, 0f, $"{LogPrefix} 탐침 상수를 읽었는데 값이 {probe}입니다.");

            // ---- 음성 대조: 없는 이름에는 확실히 거짓을 돌려주는가 ----
            Assert.IsFalse(SourceConstantReader.TryReadFloatInSurface(path, "NoSuchConstantXYZ123", out _),
                $"{LogPrefix} 음성 대조 실패 — 존재하지 않는 상수를 찾았다고 합니다(판독기가 아무 숫자나 셉니다).");

            Match decl = Regex.Match(src, @"CardActionHeight\s*=\s*(?<rhs>[^;]+);");
            Assert.IsTrue(decl.Success, $"{LogPrefix} CardActionHeight 선언을 찾지 못했습니다(이름이 바뀌었습니까?).");

            string rhs = decl.Groups["rhs"].Value.Trim();
            StringAssert.Contains(nameof(UiChrome.MinTargetSizePoints), rhs,
                $"{LogPrefix} CardActionHeight가 '{rhs}'입니다 — 하한을 숫자로 베껴 두면 " +
                $"{nameof(UiChrome.MinTargetSizePoints)}가 움직여도 따라오지 않습니다(옛 값 22f가 그렇게 하한 아래에 있었습니다).");

            // 세로 예산: |CardActionY| + 높이 ≤ CardHeight.
            float cardHeight = SourceConstantReader.ReadFloatInSurface(path, "CardHeight");
            float actionY = SourceConstantReader.ReadFloatInSurface(path, "CardActionY");
            float bottom = Mathf.Abs(actionY) + UiChrome.MinTargetSizePoints;

            Assert.LessOrEqual(bottom, cardHeight,
                $"{LogPrefix} 칩 아래끝이 {bottom:F0}pt로 카드 높이 {cardHeight:F0}pt를 넘습니다 — 칩이 카드 밖으로 나갑니다.");

            Assert.GreaterOrEqual(UiChrome.MinTargetSizePoints, 24f,
                $"{LogPrefix} MinTargetSizePoints가 WCAG 2.2 2.5.8의 24pt보다 작습니다.");

            Debug.Log($"{LogPrefix} 칩 높이 = {nameof(UiChrome.MinTargetSizePoints)}({UiChrome.MinTargetSizePoints:F0}pt), " +
                $"아래끝 {bottom:F0}pt ≤ 카드 {cardHeight:F0}pt (여백 {cardHeight - bottom:F0}pt).");
        }
    }
}
