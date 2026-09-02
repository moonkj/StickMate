using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>"글자는 읽히는데 버튼으로는 안 읽힌다"</b>의 회귀 잠금 — 2026-09-02.
    ///
    /// ============================================================================
    /// 무엇이 있었나 (실측 — 계산값이 아니다)
    /// ============================================================================
    /// 리더가 <b>실행 중인 빌드를 캡처해 픽셀에서 직접</b> 쟀다:
    /// <code>
    ///   패널 배경        rgb(21, 23, 28)
    ///   [✕]  칩 바탕     rgb(21, 23, 28)   ← 패널과 완전히 같은 값 = 1.00 : 1
    ///   [설정] 칩 바탕    rgb(22, 24, 29)   ← 1.01 : 1
    ///   [✕]  글리프 대 칩 = 5.79 : 1 / [설정] 글자 대 칩 = 8.54 : 1  (둘 다 잘 읽힌다)
    /// </code>
    /// 즉 결함은 <b>잉크가 아니라 면</b>이었고, <b>[✕]만이 아니라 [설정]도 같았다</b>.
    /// 이 앱에서 창을 닫는 마우스 경로는 하나뿐이라(<see cref="UiChrome"/> "창을 닫는 법" 절)
    /// 그 하나가 버튼으로 안 읽히면 사용자에게는 <b>시간 상한이 없는</b> 클릭 차단막이 남는다.
    ///
    /// ============================================================================
    /// ★ 이 파일이 지키는 규율 — <b>면과 잉크를 언제나 한 쌍으로 잰다</b>
    /// ============================================================================
    /// 둘은 <b>반대 방향</b>이다. 면을 밝히면 면 대비는 오르고 그 위의 밝은 잉크는 죽는다.
    /// "<see cref="UiChrome.MinNonTextContrast"/> 3.0만 넘기면 된다"로 고치면(흰색 α=0.34)
    /// 면 검사는 <b>초록</b>이 되고 화면에서는 ✕가 사라진다 — 면만 재는 검사는 그 붕괴를 못 본다.
    /// 그래서 여기 있는 단언은 전부 <b>한 테스트 안에서 두 지표를 같이</b> 확인한다.
    /// (<see cref="네거티브_컨트롤_면만_3점0에_맞추면_그_위의_글자가_지워진다"/>가 그 함정을 실물로 보여 준다.)
    ///
    /// ============================================================================
    /// 네거티브 컨트롤이 이 파일의 절반이다
    /// ============================================================================
    /// "고친 뒤 통과"는 아무것도 증명하지 않는다 — 이 저장소는 <b>실패한 측정과 성공한 측정이
    /// 똑같이 생긴</b> 거짓 초록을 하룻밤에 아홉 건 냈다. 그래서 옛 값이 <b>실제로</b> 하한을
    /// 깨는지, 제약을 하나만 푸는 풀이가 <b>실제로</b> 무너지는지를 같은 파일에서 단언한다.
    ///
    /// <para><b>화면 픽셀은 여기서 못 잰다.</b> 이 파일은 값(토큰) 층만 본다. 실제로 그려진
    /// <c>Image.color</c> / 칩 크기 / <c>Button.transition</c>은 PlayMode의
    /// <c>CloseChipAffordanceSurfaceTests</c>가 <b>생성된 트리에서 읽어</b> 잰다. 둘 다 필요하다:
    /// 값이 맞아도 호출부가 안 쓰면 화면은 그대로이기 때문이다.</para>
    /// </summary>
    public sealed class CloseChipAffordanceTests
    {
        private const string LogPrefix = "[닫기칩-TEST]";

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>8비트로 <b>양자화된</b> 색. 화면에 실제로 나가는 값은 이것이다 —
        /// 부동소수 계산만으로 하한을 재면 마지막 한 계단에서 거짓말을 할 수 있다.</summary>
        private static Color Quantized(Color c)
        {
            ColorUtility.TryParseHtmlString("#" + ColorUtility.ToHtmlStringRGB(c), out Color q);
            return q;
        }

        /// <summary>이 앱에 실재하거나 실재할 수 있는 바탕 전부. 선언 목록 셋 + 밝은 예외 + 극단값.</summary>
        private static IEnumerable<(string name, Color color)> AllBackdrops()
        {
            foreach (Color c in UiChrome.TextBackdrops) yield return ("TextBackdrops " + Hex(c), c);
            foreach (Color c in UiChrome.RaisedTextBackdrops) yield return ("RaisedTextBackdrops " + Hex(c), c);
            foreach (Color c in UiChrome.BrightTextBackdrops) yield return ("BrightTextBackdrops " + Hex(c), c);

            // 앱 안에 <b>실재하는</b> 밝은 면. 여기서 무너지는 풀이는 라이트 쪽부터 깨진다.
            yield return ("PortraitSurface", UiChrome.PortraitSurface);

            // 극단값 — 규칙이 값으로 판정하는지(목록 조회가 아닌지) 확인한다.
            yield return ("순백", Color.white);
            yield return ("순흑", Color.black);
            yield return ("중간회색 #808080", new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        /// <summary>크롬 버튼의 세 밝기. 지금 화면에 나가는 것은 첫 번째뿐이지만(hover/pressed는 미배선),
        /// 셋 다 <see cref="UiChrome.BrightTextBackdrops"/>에 선언돼 있으므로 셋 다 성립해야 한다.</summary>
        private static IEnumerable<(string name, Color face)> ChromeFaces()
        {
            yield return (nameof(UiChrome.ChromeButtonSurface), UiChrome.ChromeButtonSurface);
            yield return (nameof(UiChrome.ChromeButtonSurfaceHover), UiChrome.ChromeButtonSurfaceHover);
            yield return (nameof(UiChrome.ChromeButtonSurfacePressed), UiChrome.ChromeButtonSurfacePressed);
        }

        // ==================== ① + ② 면과 잉크를 한 쌍으로 ====================

        /// <summary>★ 이 파일의 본론. <b>면 하한과 잉크 하한을 한 테스트 안에서</b> 확인한다 —
        /// 나누는 순간 α=0.34 회귀(면 초록 / 글자 소멸)를 못 잡는다.</summary>
        [Test]
        public void 크롬버튼_면은_창_바탕을_이기고_그_위의_글자는_면을_이긴다()
        {
            foreach ((string name, Color face) in ChromeFaces())
            {
                // 화면에 나가는 값(8비트)으로 잰다.
                Color qFace = Quantized(face);
                Color qPanel = Quantized(UiChrome.PanelSurface);
                float faceRatio = UiChrome.ContrastRatio(qFace, qPanel);

                Color ink = UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled: true);
                float inkRatio = UiChrome.ContrastRatio(Quantized(ink), qFace);

                Assert.GreaterOrEqual(faceRatio, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} {name} {Hex(face)}의 <b>면</b>이 창 바탕 {Hex(UiChrome.PanelSurface)} 대비 " +
                    $"{faceRatio:F2}:1입니다. 하한 {UiChrome.MinNonTextContrast:F1}:1을 못 넘으면 글자가 " +
                    "아무리 선명해도 그것이 <b>누를 수 있는 것</b>이라는 신호가 없습니다(실측 1.00:1이 그랬습니다).");

                Assert.GreaterOrEqual(inkRatio, UiChrome.MinTextContrast,
                    $"{LogPrefix} {name} 위의 글자 {Hex(ink)}가 {inkRatio:F2}:1입니다. " +
                    $"하한 {UiChrome.MinTextContrast:F1}:1 미만이면 '흐린 글자'가 아니라 <b>없는 글자</b>입니다 — " +
                    "면을 밝히면서 그 위의 ✕를 지운 것이므로 고친 것이 아니라 옮긴 것입니다.");

                Debug.Log($"{LogPrefix} {name} {Hex(face)} — 면 {faceRatio:F2}:1 / 잉크 {Hex(ink)} {inkRatio:F2}:1");
            }
        }

        /// <summary>창 알파의 법칙 — 칩을 이루는 색은 전부 α=1이어야 한다. 반투명 겹을 하나라도
        /// 얹으면 그 화소에서 <b>유저의 바탕화면이 비친다</b>(dstA' = srcA² + dstA(1−srcA) = 0.91).
        /// 옛 <c>AddOutline(CardBorder α0.10)</c>이 정확히 그것이었고, 그래서 어두운 바탕화면에서
        /// 테두리가 <b>더</b> 안 보였다(1.34 → 1.26).</summary>
        [Test]
        public void 크롬버튼_면과_그_잉크는_전부_불투명하다()
        {
            foreach ((string name, Color face) in ChromeFaces())
            {
                Assert.AreEqual(1f, face.a, 1e-4f,
                    $"{LogPrefix} {name}의 알파가 {face.a:F2}입니다 — 창 알파가 1 미만이 되면 그 화소로 " +
                    "유저의 바탕화면이 비치고, 대비 계산이 통째로 거짓말이 됩니다.");

                Color ink = UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled: true);
                Assert.AreEqual(1f, ink.a, 1e-4f, $"{LogPrefix} {name} 위 잉크 {Hex(ink)}의 알파가 1이 아닙니다.");
            }
        }

        // ==================== ③ 네거티브 컨트롤 — 옛 값은 실제로 깨진다 ====================

        /// <summary>★ 이게 없으면 ①의 초록은 "탐지력 0"과 구분되지 않는다.
        /// 옛 면(<see cref="UiChrome.CardSurfaceMuted"/> / <see cref="UiChrome.CardSurface"/>)이
        /// <b>실제로</b> 비텍스트 하한을 못 넘었는지 같은 계산기로 확인한다.</summary>
        [Test]
        public void 네거티브_컨트롤_옛_칩_면들은_창_바탕과_구분되지_않았다()
        {
            var old = new (string where, Color face)[]
            {
                ("정보창 [✕]·[설정]", UiChrome.CardSurfaceMuted),
                ("설정창 [✕]", UiChrome.CardSurfaceMuted),
                ("팝오버 [✕]", UiChrome.CardSurface),
            };

            foreach ((string where, Color face) in old)
            {
                float ratio = UiChrome.ContrastRatio(face, UiChrome.PanelSurface);
                Assert.Less(ratio, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} 옛 면 {Hex(face)}({where})이 창 바탕 대비 {ratio:F2}:1로 하한을 " +
                    "<b>넘었습니다</b> — 그렇다면 이 파일이 지키는 결함이 실재하지 않는다는 뜻이고, " +
                    "위쪽 초록은 아무 조건도 아닙니다.");

                // 그리고 옛 값은 실제로 "거의 같은 색"이었다. 1.2는 넉넉히 느슨한 상한이다.
                Assert.Less(ratio, 1.2f,
                    $"{LogPrefix} 옛 면 {Hex(face)}이 {ratio:F2}:1입니다 — 실측은 1.00~1.09였습니다. " +
                    "전제가 재현되지 않으면 팔레트가 바뀐 것이니 이 파일을 먼저 다시 재십시오.");

                // 새 면은 반드시 다른 답을 낸다 — 이름만 바뀐 같은 코드가 아니라는 확인.
                Assert.Greater(UiChrome.ContrastRatio(UiChrome.ChromeButtonSurface, UiChrome.PanelSurface), ratio * 3f,
                    $"{LogPrefix} 새 면이 옛 면보다 눈에 띄게 밝지 않습니다.");
            }
        }

        /// <summary>★★ 리더가 경고한 함정의 이 라운드 판본 — <b>"3.0만 넘기면 된다"의 결과</b>.
        /// 흰색 α=0.34면 면은 3.11:1로 <see cref="UiChrome.MinNonTextContrast"/>를 넘겨 <b>검사가
        /// 초록이 되고</b>, 그 위에 밝은 잉크를 유지한 채 hover로 한 단만 올리면(α=0.42) 글자가
        /// 3.99:1로 <b>AA 아래로 떨어진다</b>. 면만 재는 검사는 이 붕괴를 보지 못한다.</summary>
        [Test]
        public void 네거티브_컨트롤_면만_3점0에_맞추면_그_위의_글자가_지워진다()
        {
            Color trapFace = UiChrome.Flatten(new Color(1f, 1f, 1f, 0.34f), UiChrome.PanelSurface);
            Color trapHover = UiChrome.Flatten(new Color(1f, 1f, 1f, 0.42f), UiChrome.PanelSurface);

            // (a) 면만 보는 검사는 <b>통과한다</b> — 그래서 위험하다.
            float faceRatio = UiChrome.ContrastRatio(trapFace, UiChrome.PanelSurface);
            Assert.GreaterOrEqual(faceRatio, UiChrome.MinNonTextContrast,
                $"{LogPrefix} 함정 면 {Hex(trapFace)}이 {faceRatio:F2}:1로 하한을 못 넘었습니다 — " +
                "이 테스트의 전제(면 검사가 초록이 된다)가 성립하지 않습니다.");

            // (b) 그런데 밝은 잉크를 유지한 채 한 단만 밝히면 글자가 무너진다.
            float inkAfterHover = UiChrome.ContrastRatio(UiChrome.TextPrimary, trapHover);
            Assert.Less(inkAfterHover, UiChrome.MinTextContrast,
                $"{LogPrefix} 함정 면의 hover({Hex(trapHover)})에서 밝은 잉크가 {inkAfterHover:F2}:1로 " +
                "여전히 AA를 넘었습니다 — 그렇다면 밝은 잉크 구간이 실제로 좁지 않다는 뜻이고, " +
                "'어두운 잉크로 뒤집는다'는 이번 설계의 근거가 사라집니다.");

            // (c) 채택한 면은 같은 자리에서 무너지지 않는다 — 어두운 잉크 쪽은 밝힐수록 함께 오른다.
            Color chosenInk = UiChrome.InkOnSurface(UiChrome.ChromeButtonSurface, UiChrome.InkRole.Title, true);
            Assert.GreaterOrEqual(
                UiChrome.ContrastRatio(chosenInk, UiChrome.ChromeButtonSurfaceHover), UiChrome.MinTextContrast,
                $"{LogPrefix} 채택한 잉크가 hover 면에서 무너졌습니다 — 그러면 상태 변화를 넣을 수 없습니다.");
            Assert.GreaterOrEqual(
                UiChrome.ContrastRatio(chosenInk, UiChrome.ChromeButtonSurfacePressed), UiChrome.MinTextContrast,
                $"{LogPrefix} 채택한 잉크가 pressed 면에서 무너졌습니다.");

            Debug.Log($"{LogPrefix} 함정 확인 — α0.34 면 {faceRatio:F2}:1(초록) / hover 글자 {inkAfterHover:F2}:1(붕괴).");
        }

        // ==================== ⑥ 규칙 — 어떤 바탕에서도 성립하는가 ====================

        /// <summary>★ <see cref="UiChrome.ControlFaceOnSurface"/>가 <b>선언된 모든 바탕</b>과
        /// 극단값에서 두 하한을 <b>동시에</b> 만족한다. 이 앱에 테마 전환은 없지만, 앱 안에 실재하는
        /// 밝은 면(<see cref="UiChrome.PortraitSurface"/>) 위에 크롬 버튼을 놓게 되는 날
        /// 고정 상수 #898B8E는 조용히 무너진다 — 그 회귀의 방어선이다.</summary>
        [Test]
        public void 규칙은_모든_바탕에서_면과_잉크_두_하한을_동시에_만족한다()
        {
            int checkedCount = 0;
            foreach ((string name, Color backdrop) in AllBackdrops())
            {
                Color face = UiChrome.ControlFaceOnSurface(backdrop);
                Color ink = UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled: true);

                Assert.AreEqual(1f, face.a, 1e-4f,
                    $"{LogPrefix} {name}에서 돌려준 면 {Hex(face)}의 알파가 1이 아닙니다 — 창 알파가 깨집니다.");

                // 화면에 나가는 8비트 값으로 잰다. 하한은 <b>상수를 참조</b>한다(숫자를 베끼지 않는다).
                float faceRatio = UiChrome.ContrastRatio(Quantized(face), Quantized(backdrop));
                float inkRatio = UiChrome.ContrastRatio(Quantized(ink), Quantized(face));

                Assert.GreaterOrEqual(faceRatio, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} {name} 위에서 규칙이 낸 면 {Hex(face)}이 {faceRatio:F2}:1입니다.");
                Assert.GreaterOrEqual(inkRatio, UiChrome.MinTextContrast,
                    $"{LogPrefix} {name} 위 면 {Hex(face)}의 글자 {Hex(ink)}가 {inkRatio:F2}:1입니다 — " +
                    "두 제약 중 하나만 푼 풀이입니다.");
                checkedCount++;
            }

            // 그물이 비어 있지 않다는 확인 — 0개를 훑고 통과하는 것을 막는다.
            Assert.GreaterOrEqual(checkedCount, 10,
                $"{LogPrefix} 바탕을 {checkedCount}개밖에 훑지 않았습니다 — 목록이 비었거나 " +
                "열거가 끊겼습니다. 이 상태의 초록은 아무 조건도 아닙니다.");
            Debug.Log($"{LogPrefix} 규칙 검사 통과 — 바탕 {checkedCount}종.");
        }

        /// <summary>★ 네거티브 컨트롤 — 제약을 <b>①(면)만</b> 푸는 풀이는 실제로 무너진다.
        /// 무너지지 않는다면 "두 제약을 동시에 푼다"는 이 설계의 핵심이 장식이라는 뜻이다.</summary>
        [Test]
        public void 네거티브_컨트롤_면만_푸는_풀이는_어떤_바탕에서_글자를_지운다()
        {
            // ★ 2026-09-02 qa-regression — 이 검사는 <b>일부러</b> 프로덕션을 "읽히는 잉크가
            //   존재하지 않는 면"으로 몰아넣는다. 그때 UiChrome.WarnUnreadableBackdrop이
            //   Debug.LogError를 낸다(UNITY_EDITOR 조건부). 그것은 프로덕션이 <b>제대로 소리치는
            //   것</b>인데, Unity 테스트 프레임워크는 선언되지 않은 LogError를 무조건 실패로 잡는다.
            //   그래서 이 테스트가 <b>거짓 실패</b>로 여러 라운드를 떠돌았다(러너에 유일한 빨간불).
            //
            //   처방은 "무시"가 아니라 <b>2패스</b>다:
            //     1패스 — 어느 바탕이 깨지는지 <b>알아낸다</b>(그동안만 로그 실패를 끈다).
            //     2패스 — 깨지는 바탕마다 LogAssert.Expect를 <b>미리 선언하고</b> 다시 부른다.
            //   이러면 삼킨 것이 아니라 <b>"프로덕션이 정확히 그만큼 소리쳤다"를 단언</b>하게 된다.
            //   그냥 ignoreFailingMessages=true로 덮으면 프로덕션이 소리치기를 멈춰도 초록이다.
            var broken = new List<string>();
            var brokenBackdrops = new List<Color>();
            int checkedCount = 0;

            bool prevIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                foreach ((string name, Color backdrop) in AllBackdrops())
                {
                    Color face = FaceOnlySolution(backdrop);
                    Color ink = UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled: true);
                    float inkRatio = UiChrome.ContrastRatio(ink, face);
                    if (inkRatio < UiChrome.MinTextContrast)
                    {
                        broken.Add($"{name} → {Hex(face)} / {inkRatio:F2}:1");
                        brokenBackdrops.Add(backdrop);
                    }
                    checkedCount++;
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = prevIgnore;
            }

            Assert.GreaterOrEqual(checkedCount, 10, $"{LogPrefix} 바탕을 {checkedCount}개밖에 훑지 않았습니다.");
            Assert.IsNotEmpty(broken,
                $"{LogPrefix} 면만 {UiChrome.ControlFaceContrastTarget:F2}:1에 맞추는 풀이가 " +
                $"바탕 {checkedCount}종 어디에서도 글자를 깨지 않았습니다 — 그렇다면 " +
                "ControlFaceOnSurface가 두 제약을 <b>동시에</b> 풀 이유가 없고, 위쪽 초록은 " +
                "제약 하나짜리 풀이와 구분되지 않습니다.");

            // ── 2패스: 깨지는 바탕마다 프로덕션이 <b>실제로 소리치는지</b>를 단언한다.
            //    Expect를 미리 걸어 두면, 소리치지 않는 회귀는 "기대한 로그가 안 왔다"로 빨개진다.
            //    (조용히 나쁜 잉크를 돌려주는 것이 이 저장소가 가장 자주 당한 실패 방식이다.)
            foreach (Color backdrop in brokenBackdrops)
            {
                Color face = FaceOnlySolution(backdrop);
                LogAssert.Expect(LogType.Error, new Regex(@"\[잉크\].*넘지 못합니다"));
                UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled: true);
            }

            Debug.Log($"{LogPrefix} 면만 푸는 풀이는 {broken.Count}/{checkedCount} 바탕에서 글자를 지웁니다:\n  " +
                string.Join("\n  ", broken));
        }

        /// <summary>제약 ①만 푸는 "최소 혼합" — 이 파일 안에서만 쓰는 <b>일부러 틀린</b> 풀이.
        /// 프로덕션 함수와 같은 방향 판정·같은 격자를 쓰되 잉크 제약만 뺀다(비교가 성립하도록).</summary>
        private static Color FaceOnlySolution(Color backdrop)
        {
            bool up = UiChrome.ControlFaceContrastTarget * (UiChrome.RelativeLuminance(backdrop) + 0.05f) - 0.05f <= 1f;
            Color mix = up ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f);
            const int steps = 1024;
            for (int i = 0; i <= steps; i++)
            {
                Color face = UiChrome.Flatten(new Color(mix.r, mix.g, mix.b, i / (float)steps), backdrop);
                if (UiChrome.ContrastRatio(face, backdrop) >= UiChrome.ControlFaceContrastTarget) return face;
            }
            return UiChrome.Flatten(mix, backdrop);
        }

        // ==================== 상수와 규칙이 갈라지지 않게 ====================

        /// <summary>★ <see cref="UiChrome.ChromeButtonSurface"/>는 손으로 고른 값이고
        /// <see cref="UiChrome.ControlFaceOnSurface"/>는 규칙이다. 둘이 갈라지면 다음 사람은
        /// <b>어느 쪽이 맞는지 알 수 없다</b> — 그게 이 저장소가 "면 2종 / 잉크 2종 / 테두리 2종"의
        /// 3-way 분기를 얻은 경로다. 상수는 규칙의 해보다 <b>같거나 밝아야</b> 한다.</summary>
        [Test]
        public void 손으로_고른_면은_규칙이_내는_최소해보다_어둡지_않다()
        {
            Color ruleFace = UiChrome.ControlFaceOnSurface(UiChrome.PanelSurface);
            float ruleRatio = UiChrome.ContrastRatio(ruleFace, UiChrome.PanelSurface);
            float constRatio = UiChrome.ContrastRatio(UiChrome.ChromeButtonSurface, UiChrome.PanelSurface);

            Assert.GreaterOrEqual(constRatio, ruleRatio,
                $"{LogPrefix} 상수 {Hex(UiChrome.ChromeButtonSurface)}({constRatio:F2}:1)가 규칙의 최소해 " +
                $"{Hex(ruleFace)}({ruleRatio:F2}:1)보다 <b>어둡습니다</b>. 규칙이 '이보다 어두우면 안 된다'고 " +
                "말하는 값을 상수가 어기고 있으면 둘 중 하나는 틀린 것이고, 화면에 나가는 것은 상수입니다.");

            // 그리고 규칙 자체도 두 목표를 만족해야 한다(위 ⑥과 같은 짝이지만 여기선 목표치로 잰다).
            Assert.GreaterOrEqual(ruleRatio, UiChrome.ControlFaceContrastTarget,
                $"{LogPrefix} 규칙의 해가 목표 {UiChrome.ControlFaceContrastTarget:F2}:1에 미달합니다.");
            Assert.GreaterOrEqual(
                UiChrome.ContrastRatio(UiChrome.InkOnSurface(ruleFace, UiChrome.InkRole.Title, true), ruleFace),
                UiChrome.ControlInkContrastTarget,
                $"{LogPrefix} 규칙의 해 위 글자가 목표 {UiChrome.ControlInkContrastTarget:F3}:1에 미달합니다.");

            Debug.Log($"{LogPrefix} 상수 {Hex(UiChrome.ChromeButtonSurface)} {constRatio:F2}:1 ≥ " +
                $"규칙 {Hex(ruleFace)} {ruleRatio:F2}:1");
        }

        /// <summary>목표치는 <b>하한에서 파생</b>돼야 한다 — 숫자를 따로 타이핑해 두면 하한이 바뀐 날
        /// 목표만 옛날 값으로 남는다.</summary>
        [Test]
        public void 목표_대비는_하한에서_파생된다()
        {
            Assert.Greater(UiChrome.ControlFaceContrastTarget, UiChrome.MinNonTextContrast,
                $"{LogPrefix} 면 목표({UiChrome.ControlFaceContrastTarget:F2})가 하한" +
                $"({UiChrome.MinNonTextContrast:F1})보다 크지 않습니다 — 마진이 없으면 8비트 양자화 " +
                "한 계단만으로 미달이 됩니다.");
            Assert.Greater(UiChrome.ControlInkContrastTarget, UiChrome.MinTextContrast,
                $"{LogPrefix} 잉크 목표({UiChrome.ControlInkContrastTarget:F3})가 하한" +
                $"({UiChrome.MinTextContrast:F1})보다 크지 않습니다.");
        }
    }
}
