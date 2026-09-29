using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>창 UI 글꼴 해석</b>이 살아 있는가 — 그리고 글꼴을 바꾸면서 <b>기호를 잃지 않았는가</b>.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    /// 사용자 신고(Windows 150% 실기): <b>"두꺼운 폰트들이 깔끔하게 안 보이고 번져 보임"</b>.
    /// 처방은 <c>docs/UI_SURFACE_SPEC.md</c> <b>P2-12</b>이고, <c>UiChrome</c>가 그 1·2단계를 구현한다:
    /// ① 한글이 실제로 그려지는 <b>Regular</b> 페이스를 잡고 ② 같은 가족의 <b>진짜 Bold</b> 페이스로
    /// 굵기를 그려 <b>합성 볼드</b>를 끈다(합성 볼드가 작은 한글의 획을 서로 붙인다 — 이 저장소의
    /// 말풍선 렌더러가 이미 같은 결론을 주석에 못박아 두었다).
    ///
    /// ============================================================================
    /// 이 감사가 겨누는 <b>세 가지 조용한 실패</b>
    /// ============================================================================
    /// <list type="number">
    /// <item><b>기호를 잃는다.</b> 창 UI는 한글·라틴 말고도 닫기 칩 <c>✕</c>, 말줄임 <c>…</c>,
    ///   페이지 삼각형, 포스트잇 체크상자, macOS 수정키를 <b>글자로</b> 그린다. 새 페이스가 이것들을
    ///   못 그리면 두부(□)가 뜨는데, <b>한글은 잘 보이므로</b> 아무도 글꼴 교체를 의심하지 않는다.
    ///   ⇒ 그래서 <b>내장 폰트를 기준선</b>으로 두고 "덜 그리지 않는가"를 상대 비교한다
    ///   (절대 개수를 적으면 플랫폼마다 달라 이 파일이 곧 거짓이 된다).</item>
    /// <item><b>Bold가 조용히 Regular로 폴백한다.</b> <c>Font.CreateDynamicFontFromOSFont</c>는 없는
    ///   이름에도 <c>null</c>을 주지 않는다. 그 폴백을 "진짜 Bold"로 쓰면 합성 볼드를 끄는 바람에
    ///   굵은 글자가 <b>본문보다 얇게</b> 나온다 — 지금보다 나쁘다.
    ///   ⇒ <c>BoldFont</c>가 잡혔다면 <c>Font</c>와 <b>다른 객체</b>이고 글리프 수치도 달라야 한다.</item>
    /// <item><b>굵기를 켜는 자리가 새로 생긴다.</b> <c>fontStyle = FontStyle.Bold</c>를 직접 쓰면 그
    ///   자리만 합성 볼드로 남는다. 그리고 그런 자리는 하필 "지금 선택된 탭"처럼 <b>가장 눈에 띄는</b>
    ///   곳이다. ⇒ 소스를 전수해 <b>모든 자리가 <see cref="UiChrome.ApplyBold"/>를 지나는지</b> 본다.</item>
    /// </list>
    ///
    /// <para><b>한계(정직하게 적는다)</b>:
    /// <list type="bullet">
    /// <item><b>어느 페이스가 잡히는지는 플랫폼마다 다르다.</b> 이 머신은 macOS이고 <b>Windows는
    ///   이 저장소에서 한 번도 실행되지 않는다</b>. 그래서 이 감사는 페이스 <b>이름</b>을 단언하지
    ///   않고(그걸 적으면 Windows에서 거짓 빨강이 된다) <b>해석이 성립하는가</b>와 <b>기호를 덜
    ///   그리지 않는가</b>만 본다. 이름은 로그로 남긴다 — 실기 회귀를 눈으로 볼 창구는 그 로그다.</item>
    /// <item><b>배치모드는 <c>-nographics</c>다.</b> 다이내믹 OS 폰트가 이 모드에서 올라오지 않으면
    ///   내장 폰트로 폴백하는데, 그건 <b>종전 동작</b>이라 이 감사는 그때도 초록이다.
    ///   그 사실을 숨기지 않으려고 <b>확정 이름을 매번 로그로 찍는다</b> — 로그가
    ///   <c>LegacyRuntime.ttf</c>라고 말하면 이 실행은 <b>새 경로를 재지 않은 것</b>이다.</item>
    /// <item><b>최종 판정은 실기 캡처다</b> — Windows 100%/150% 두 배율의 12pt Bold 한글 1장씩
    ///   (P2-12의 검증 항목, 리더 배정 대기).</item>
    /// </list></para>
    /// </summary>
    public sealed class UiFontResolutionAuditTests
    {
        private const string LogPrefix = "[UI폰트-TEST]";

        /// <summary>테스트 자신의 한글 표본. <b>프로덕션 문안이 아니다</b> — 문안이 바뀌어도 이 자는
        /// 안 흔들려야 한다.</summary>
        private const string KoreanSample = "가나다라마";

        /// <summary>같은 글자 수의 라틴 표본. "한글이 라틴보다 넓다"는 자 검증에 쓴다.</summary>
        private const string LatinSample = "abcde";

        /// <summary>굵기가 <b>폭을 바꾸는가</b>를 재는 표본. 짧아야 차이가 상대적으로 크게 보인다.
        /// <para>프로덕션 탭 이름이 아니라 테스트 자신의 글자다 — 탭 이름이 바뀌어도 안 흔들려야 한다.</para></summary>
        private const string MetricsSample = "일반";

        /// <summary>음성 대조용 코드포인트 — 사용자 영역(U+E000)과 비문자(U+FFFF). 어떤 정상 폰트도
        /// 그리지 않으므로, 이 자가 "못 그린다"를 <b>말할 수 있는지</b>를 증명한다.</summary>
        private const string UnrenderableSample = "￿";

        // ============================================================================
        // 자 — 글리프가 실제로 잡히는가 (프로덕션 내부 함수를 부르지 않는다)
        // ============================================================================

        /// <summary>
        /// <paramref name="font"/>가 <b>못 그리는</b> 글자만 모아 돌려준다(전부 그리면 빈 문자열).
        ///
        /// <para>★ <b>프로덕션의 같은 판정 함수를 부르지 않는다.</b> 그 함수가 틀어지면 기대값도 함께
        /// 틀어져 아무것도 못 잰다(<c>CharacterStatReadoutTests</c>·<c>FullscreenAutoHideSwitchCopyAuditTests</c>가
        /// 같은 판단을 이미 적어 뒀다). 여기서는 Unity API를 직접 두드린다.</para>
        /// </summary>
        private static string Missing(Font font, string probe)
        {
            if (font == null) return probe ?? string.Empty;
            var missing = new StringBuilder();
            font.RequestCharactersInTexture(probe, UiChrome.FontBody, FontStyle.Normal);
            for (int i = 0; i < probe.Length; i++)
            {
                if (font.GetCharacterInfo(probe[i], out CharacterInfo info, UiChrome.FontBody, FontStyle.Normal)
                    && info.advance > 0) continue;
                missing.Append(probe[i]);
            }
            return missing.ToString();
        }

        private static string Describe(string glyphs)
            => glyphs.Length == 0 ? "(없음)" : "«" + glyphs + "»";

        /// <summary>잉크 폭(pt). <c>SettingsControls.MeasuredWidth</c>를 <b>부르지 않는다</b> — 그 함수가
        /// 틀어지면 기대값도 같이 틀어진다(같은 판단이 이미 세 테스트에 적혀 있다).</summary>
        private static float Ink(Text text, string content)
        {
            text.text = content ?? string.Empty;
            return Mathf.Ceil(text.preferredWidth);
        }

        // ============================================================================
        // 1. 해석이 성립하는가 + 확정 이름을 로그로 남긴다
        // ============================================================================

        [Test]
        public void 본문_글꼴이_해석되고_확정_이름이_로그에_남는다()
        {
            Font font = UiChrome.Font;
            Assert.IsNotNull(font,
                $"{LogPrefix} UiChrome.Font가 null입니다 — 이 앱의 모든 창 글자가 여기서 태어나므로 " +
                "화면에 글자가 한 자도 안 나옵니다. 폴백(내장 폰트)조차 실패한 상태입니다.");

            string name = UiChrome.ResolvedFontName;
            Assert.IsFalse(string.IsNullOrEmpty(name),
                $"{LogPrefix} 확정 페이스 이름이 비었습니다 — 해석 경로가 이름을 남기지 않으면 " +
                "Windows 회귀를 로그로 볼 수 없습니다(P2-12가 요구한 것이 바로 이 한 줄입니다).");

            bool builtinFallback = string.Equals(name, UiChrome.BuiltinFontResource, StringComparison.Ordinal);
            Debug.Log($"{LogPrefix} 확정 본문='{name}' · 굵게=" +
                      $"'{UiChrome.ResolvedBoldFontName ?? "(없음 — 합성 볼드로 떨어짐)"}' · " +
                      $"이 실행이 잰 것은 {(builtinFallback ? "내장 폴백(= 종전 경로)" : "새 OS 페이스 경로")}입니다.");

            var host = new GameObject("UI글꼴해석측정", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Text probe = UiChrome.AddText(host.transform, "Probe", UiChrome.FontBody,
                    TextAnchor.MiddleLeft, UiChrome.TextPrimary);
                Assert.AreSame(font, probe.font,
                    $"{LogPrefix} AddText가 UiChrome.Font가 아닌 다른 폰트를 붙였습니다 — " +
                    "글꼴 단일 창구가 깨졌습니다(굵기 경로 ApplyBold를 확인하세요).");

                // 양성 대조: 이 자가 0이 아닌 값을 내는가. 0을 재고 판정하면 그게 거짓 초록이다.
                probe.text = KoreanSample;
                float hangul = probe.preferredWidth;
                probe.text = LatinSample;
                float latin = probe.preferredWidth;
                Assert.Greater(hangul, 0f,
                    $"{LogPrefix} 한글 {KoreanSample.Length}자의 폭이 0입니다 — 폰트가 올라오지 않았습니다. " +
                    "이 실행의 폭 판정은 전부 무효입니다.");
                Assert.Greater(hangul, latin,
                    $"{LogPrefix} 한글 {hangul:F1}pt가 라틴 {latin:F1}pt보다 넓지 않습니다 — 자가 이상합니다.");

                Debug.Log($"{LogPrefix} 폭 표본({UiChrome.FontBody}pt) 한글 {KoreanSample.Length}자 " +
                          $"{hangul:F1}pt / 라틴 {LatinSample.Length}자 {latin:F1}pt — " +
                          "고정 폭 상자의 예산은 FullscreenAutoHideSwitchCopyAuditTests가 따로 잰다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ============================================================================
        // 2. 기호를 잃지 않았는가 — 기준선은 내장 폰트다
        // ============================================================================

        [Test]
        public void 새_글꼴이_내장_폰트보다_UI_기호를_덜_그리지_않는다()
        {
            Font builtin = Resources.GetBuiltinResource<Font>(UiChrome.BuiltinFontResource);
            Assert.IsNotNull(builtin,
                $"{LogPrefix} 내장 폰트 '{UiChrome.BuiltinFontResource}'를 못 불렀습니다 — " +
                "기준선이 없으면 이 비교는 아무 말도 하지 못합니다.");

            // ---- 양성 대조: 두 폰트 모두 평범한 라틴 한 자는 그려야 한다 ----
            Assert.AreEqual(string.Empty, Missing(builtin, "A"),
                $"{LogPrefix} 내장 폰트가 'A'조차 못 그립니다 — 이 환경에서는 글리프 조회가 죽어 있어 " +
                "아래 비교가 전부 무효입니다.");
            Assert.AreEqual(string.Empty, Missing(UiChrome.Font, "A"),
                $"{LogPrefix} 확정 페이스 '{UiChrome.ResolvedFontName}'가 'A'조차 못 그립니다 — " +
                "본문 글꼴로 쓸 수 없는 것을 잡았습니다.");

            // ---- 음성 대조: 이 자가 "못 그린다"를 말할 수 있는가 ----
            Assert.AreNotEqual(string.Empty, Missing(builtin, UnrenderableSample),
                $"{LogPrefix} 어떤 폰트도 그리지 않아야 할 코드포인트(U+E000·U+FFFF)를 내장 폰트가 " +
                "그렸다고 나옵니다 — 이 자는 '못 그린다'를 말할 수 없으므로 아래 판정이 공허합니다.");

            string missingBuiltin = Missing(builtin, UiChrome.SymbolProbe);
            string missingLive = Missing(UiChrome.Font, UiChrome.SymbolProbe);

            Debug.Log($"{LogPrefix} UI 기호 {UiChrome.SymbolProbe.Length}종 — " +
                      $"내장 '{UiChrome.BuiltinFontResource}' 미지원 {missingBuiltin.Length}종 {Describe(missingBuiltin)} / " +
                      $"확정 '{UiChrome.ResolvedFontName}' 미지원 {missingLive.Length}종 {Describe(missingLive)}. " +
                      "이 목록은 전수가 아니라 화면에 나가는 표본이다(UiChrome.SymbolProbe).");

            Assert.LessOrEqual(missingLive.Length, missingBuiltin.Length,
                $"{LogPrefix} 확정 페이스 '{UiChrome.ResolvedFontName}'가 내장 폰트보다 UI 기호를 " +
                $"덜 그립니다(미지원 {missingLive.Length} {Describe(missingLive)} vs 내장 " +
                $"{missingBuiltin.Length} {Describe(missingBuiltin)}). 그 글리프 자리에 두부(□)가 뜹니다 — " +
                "닫기 칩 ✕ · 말줄임 … · 페이지 삼각형 · 포스트잇 체크상자가 그 표본에 들어 있습니다. " +
                "후보 표(UiChrome.FacePairs)의 순서를 고치거나, 그 글리프를 글자 대신 도형으로 그리십시오.");
        }

        // ============================================================================
        // 3. 진짜 Bold 페이스를 "잡았다"면 그것이 실제로 다른 페이스인가
        // ============================================================================

        [Test]
        public void 진짜_Bold_페이스를_잡았다면_본문과_다른_페이스다()
        {
            Font regular = UiChrome.Font;
            Font bold = UiChrome.BoldFont;

            // ★ <b>Assert.Ignore를 쓰지 않는다.</b> 초판이 "이 환경에서는 못 잡았다"를 Ignore로 남겼다가
            //   TestClaimExpiryAuditTests에 걸렸다 — 이 저장소의 Ignore는 <b>명부 등록 + 역방향 장치</b>가
            //   있어야 한다. 그리고 그 요구가 옳다: 여기서 갈리는 것은 "못 고친 갭"이 아니라
            //   <b>환경에 따라 달라지는 정상 경로</b>이고, 두 갈래 <b>모두</b> 단언할 불변식이 있다.
            if (bold == null)
            {
                Debug.Log($"{LogPrefix} 이 환경은 진짜 Bold 페이스를 못 잡아 합성 볼드로 떨어졌다 " +
                          $"(본문='{UiChrome.ResolvedFontName}'). 종전 동작이라 결함은 아니지만, " +
                          "신고된 번짐이 이 경로에서는 고쳐지지 않는다 — 실기 캡처로 확인할 항목이다.");
                Assert.IsNull(UiChrome.ResolvedBoldFontName,
                    $"{LogPrefix} BoldFont는 null인데 확정 이름이 남아 있습니다 " +
                    $"('{UiChrome.ResolvedBoldFontName}') — 둘이 갈라지면 로그가 거짓말을 합니다.");

                // 폴백 경로의 불변식: 합성 볼드가 <b>실제로</b> 걸려야 한다(굵기가 사라지면 그게 회귀다).
                var fallbackHost = new GameObject("합성볼드폴백측정", typeof(RectTransform), typeof(Canvas));
                try
                {
                    fallbackHost.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    Text t = UiChrome.AddText(fallbackHost.transform, "Bold", UiChrome.FontBody,
                        TextAnchor.MiddleLeft, UiChrome.TextPrimary, bold: true);
                    Assert.AreEqual(FontStyle.Bold, t.fontStyle,
                        $"{LogPrefix} 진짜 Bold 페이스가 없는데 합성 볼드도 걸리지 않았습니다 — " +
                        "굵은 글자가 본문과 구별되지 않습니다(위계가 색 하나로 줄어듭니다).");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(fallbackHost);
                }
                return;
            }

            Assert.IsNotNull(UiChrome.ResolvedBoldFontName,
                $"{LogPrefix} 진짜 Bold 페이스를 잡았는데 이름이 비었습니다 — 로그로 볼 수 없습니다.");
            Assert.AreNotSame(regular, bold,
                $"{LogPrefix} BoldFont가 본문 페이스와 <b>같은 객체</b>입니다 — 합성 볼드를 끄면서 " +
                "같은 페이스를 쓰면 굵은 글자가 본문보다 얇게 나옵니다(지금보다 나쁩니다).");

            // 글리프 수치가 실제로 다른가 — 조용한 폴백을 여기서 한 번 더 가른다.
            string regularShape = Shape(regular);
            string boldShape = Shape(bold);
            Debug.Log($"{LogPrefix} 페이스 수치 — 본문 '{UiChrome.ResolvedFontName}' {regularShape} / " +
                      $"굵게 '{UiChrome.ResolvedBoldFontName}' {boldShape}");
            Assert.AreNotEqual(regularShape, boldShape,
                $"{LogPrefix} 두 페이스의 글리프 수치가 완전히 같습니다 — Bold 후보가 조용히 본문 " +
                "페이스로 폴백했을 가능성이 큽니다. 그대로 쓰면 합성 볼드가 꺼진 채 굵기가 사라집니다.");
        }

        /// <summary>글리프 수치 한 줄. <b>큰 pt로 잰다</b> — 작은 pt에서는 정수 반올림 때문에 진짜
        /// Bold와 Regular이 같게 나올 수 있다.</summary>
        private static string Shape(Font font)
        {
            const int points = 48;
            const string probe = "한W";
            var sb = new StringBuilder();
            font.RequestCharactersInTexture(probe, points, FontStyle.Normal);
            for (int i = 0; i < probe.Length; i++)
            {
                if (!font.GetCharacterInfo(probe[i], out CharacterInfo info, points, FontStyle.Normal))
                {
                    sb.Append(probe[i]).Append("=없음 ");
                    continue;
                }
                sb.Append(probe[i]).Append('=').Append(info.glyphWidth).Append('×')
                  .Append(info.glyphHeight).Append("/adv").Append(info.advance).Append(' ');
            }
            return sb.ToString().TrimEnd();
        }

        // ============================================================================
        // 4. ApplyBold가 font와 fontStyle을 한 쌍으로 바꾸는가
        // ============================================================================

        [Test]
        public void ApplyBold가_font와_fontStyle을_한_쌍으로_바꾼다()
        {
            var host = new GameObject("굵기창구측정", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                Text made = UiChrome.AddText(host.transform, "Bold", UiChrome.FontBody,
                    TextAnchor.MiddleLeft, UiChrome.TextPrimary, bold: true);
                AssertBoldPair(made, "AddText(bold: true)");

                UiChrome.ApplyBold(made, false);
                Assert.AreSame(UiChrome.Font, made.font,
                    $"{LogPrefix} 굵기를 끈 뒤에도 본문 페이스로 돌아오지 않았습니다 — 한 번 굵어진 " +
                    "라벨이 계속 Bold 페이스로 남으면 비활성 탭이 활성 탭처럼 보입니다.");
                Assert.AreEqual(FontStyle.Normal, made.fontStyle,
                    $"{LogPrefix} 굵기를 끈 뒤에도 fontStyle이 {made.fontStyle}입니다.");

                UiChrome.ApplyBold(made, true);
                AssertBoldPair(made, "ApplyBold(true) 재적용");

                // null은 조용히 무시한다(호출부가 배열 칸을 그대로 넘기는 자리가 다섯 곳 있다).
                Assert.DoesNotThrow(() => UiChrome.ApplyBold(null, true),
                    $"{LogPrefix} ApplyBold(null)이 던집니다 — 라벨 배열에 빈 칸이 있는 다섯 호출부가 " +
                    "그대로 죽습니다.");

                // ---- keepRegularMetrics 갈래: 폭이 바뀌면 안 되는 자리 ----
                //   탭·세그먼트 상자는 생성 시점에 Regular 폭으로 재 두므로, 그 자리에서 페이스를
                //   갈아타면 상자와 잉크가 갈라진다(실측으로 PlayMode 2건이 그것을 잡았다).
                Text kept = UiChrome.AddText(host.transform, "KeptMetrics", UiChrome.FontBody,
                    TextAnchor.MiddleCenter, UiChrome.TextPrimary);
                float regularInk = Ink(kept, MetricsSample);
                UiChrome.ApplyBold(kept, true, keepRegularMetrics: true);
                Assert.AreSame(UiChrome.Font, kept.font,
                    $"{LogPrefix} keepRegularMetrics인데 페이스를 갈아탔습니다 — 미리 잰 상자가 " +
                    "곧바로 어긋납니다(이 갈래의 존재 이유가 그것입니다).");
                Assert.AreEqual(FontStyle.Bold, kept.fontStyle,
                    $"{LogPrefix} keepRegularMetrics인데 합성 볼드도 안 걸렸습니다 — 굵기가 사라집니다.");
                float keptInk = Ink(kept, MetricsSample);
                Assert.AreEqual(regularInk, keptInk, 0.5f,
                    $"{LogPrefix} keepRegularMetrics로 굵게 했는데 폭이 {regularInk:F1} → {keptInk:F1}pt로 " +
                    "바뀌었습니다 — 이 갈래는 폭 불변이 유일한 목적입니다.");

                // ---- 음성 대조: 기본 갈래는 실제로 폭을 바꾼다(그래서 이 갈래가 필요하다) ----
                if (UiChrome.BoldFont != null)
                {
                    UiChrome.ApplyBold(kept, true);
                    float realBoldInk = Ink(kept, MetricsSample);
                    Debug.Log($"{LogPrefix} 폭 영향({UiChrome.FontBody}pt, «{MetricsSample}») — " +
                              $"Regular {regularInk:F1}pt / 합성 볼드 {keptInk:F1}pt / " +
                              $"진짜 Bold 페이스 {realBoldInk:F1}pt");
                    Assert.AreNotEqual(keptInk, realBoldInk,
                        $"{LogPrefix} 진짜 Bold 페이스가 합성 볼드와 같은 폭을 냅니다 — 그렇다면 " +
                        "keepRegularMetrics 갈래는 필요 없고, 탭·세그먼트도 진짜 페이스를 쓸 수 있습니다. " +
                        "그 사실을 확인했으면 ApplyBold 문서의 「판정 대기」를 닫으십시오.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>굵기는 <b>둘 중 하나의 짝</b>이어야 한다: 진짜 Bold 페이스 + 합성 볼드 끔,
        /// 또는 본문 페이스 + 합성 볼드 켬. 그 사이의 어떤 조합도 결함이다.</summary>
        private static void AssertBoldPair(Text text, string what)
        {
            if (UiChrome.BoldFont != null)
            {
                Assert.AreSame(UiChrome.BoldFont, text.font,
                    $"{LogPrefix} {what}: 진짜 Bold 페이스가 있는데 본문 페이스로 그립니다.");
                Assert.AreEqual(FontStyle.Normal, text.fontStyle,
                    $"{LogPrefix} {what}: 진짜 Bold 페이스 위에 합성 볼드를 또 걸었습니다 " +
                    "(굵기가 두 번 걸려 획이 서로 붙습니다 — 신고된 바로 그 증상입니다).");
                return;
            }
            Assert.AreSame(UiChrome.Font, text.font,
                $"{LogPrefix} {what}: Bold 페이스가 없는데 본문 페이스도 아닙니다.");
            Assert.AreEqual(FontStyle.Bold, text.fontStyle,
                $"{LogPrefix} {what}: Bold 페이스가 없으면 합성 볼드로 굵기를 내야 하는데 " +
                $"fontStyle이 {text.fontStyle}입니다 — 굵기가 아예 사라집니다.");
        }

        // ============================================================================
        // 5. 굵기를 켜는 자리가 <b>전부</b> ApplyBold를 지나는가 (소스 전수)
        // ============================================================================

        /// <summary>자기 파이프라인을 따로 가진 두 파일. <b>열거로</b> 적는다 — "몇 건이냐"로 세면
        /// 새 자리가 늘어도 숫자만 맞으면 통과할 수 있다(TEAM.md 규칙 47).</summary>
        private static readonly string[] OwnPipelineFiles =
        {
            "UiChrome.cs",                  // ApplyBold 자신 — 합성 볼드 폴백을 여기서만 건다.
            "DialogueBubbleRenderer.cs",    // 말풍선은 자기 폰트 해석을 가진 별개 파이프라인이다.
        };

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        [Test]
        public void 합성_볼드를_직접_거는_자리가_두_파이프라인_밖에_없다()
        {
            string testsRoot = (Path.Combine(ScriptsRoot, "Tests") + Path.DirectorySeparatorChar)
                .Replace('\\', '/');
            string[] all = Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.GreaterOrEqual(all.Length, 40,
                $"{LogPrefix} 스캔 대상이 비정상적으로 적습니다({all.Length}) — 경로 계산 오류로 " +
                "허위 통과할 위험이 있습니다.");

            var hits = new List<string>();
            var byFile = new Dictionary<string, int>(StringComparer.Ordinal);
            int scanned = 0;

            foreach (string path in all)
            {
                if (path.Replace('\\', '/').StartsWith(testsRoot, StringComparison.Ordinal)) continue;
                scanned++;
                string file = Path.GetFileName(path);
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;   // 주석 줄은 코드가 아니다.
                    if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;
                    if (trimmed.IndexOf("fontStyle", StringComparison.Ordinal) < 0) continue;
                    if (trimmed.IndexOf('=') < 0) continue;
                    if (trimmed.IndexOf("FontStyle.Bold", StringComparison.Ordinal) < 0) continue;

                    byFile.TryGetValue(file, out int n);
                    byFile[file] = n + 1;
                    if (Array.IndexOf(OwnPipelineFiles, file) >= 0) continue;
                    hits.Add($"{file}:{i + 1}  {trimmed}");
                }
            }

            Assert.GreaterOrEqual(scanned, 40,
                $"{LogPrefix} 테스트 폴더를 걸러낸 뒤 남은 파일이 {scanned}개입니다 — 필터가 너무 넓습니다.");

            // ---- 양성 대조: 허용된 두 자리를 이 스캐너가 <b>실제로</b> 찾는가 ----
            //   못 찾으면 니들이 죽은 것이고, 그때 아래 "위반 0건"은 아무 의미가 없다.
            foreach (string owner in OwnPipelineFiles)
            {
                Assert.IsTrue(byFile.ContainsKey(owner) && byFile[owner] > 0,
                    $"{LogPrefix} 양성 대조 실패 — '{owner}'에서 합성 볼드 대입을 한 건도 못 찾았습니다. " +
                    "니들('fontStyle' + '=' + 'FontStyle.Bold')이 죽었거나 그 파일의 굵기 경로가 " +
                    "바뀌었습니다. 이 스캐너가 죽은 채로는 아래 '위반 0건'이 공허합니다.");
            }

            Debug.Log($"{LogPrefix} 합성 볼드 직접 대입 — 프로덕션 {scanned}개 파일 중 적중 " +
                      $"{byFile.Count}개 파일: " +
                      string.Join(", ", DescribeCounts(byFile)) +
                      $" · 허용 파이프라인 {OwnPipelineFiles.Length}개 밖의 위반 {hits.Count}건.");

            Assert.IsEmpty(hits,
                $"{LogPrefix} 합성 볼드를 직접 거는 자리가 두 파이프라인 밖에 있습니다:\n  " +
                string.Join("\n  ", hits) +
                "\n⇒ UiChrome.ApplyBold(text, bold)로 바꾸십시오. fontStyle 단독 대입은 진짜 Bold " +
                "페이스를 잡은 환경에서 <b>그 자리만</b> 합성 볼드로 남기고, 그런 자리는 하필 " +
                "'지금 선택된 것'을 가리키는 가장 눈에 띄는 라벨입니다(UI_SURFACE_SPEC P2-12).");
        }

        private static IEnumerable<string> DescribeCounts(Dictionary<string, int> byFile)
        {
            var keys = new List<string>(byFile.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string k in keys) yield return $"{k}×{byFile[k]}";
        }
    }
}
