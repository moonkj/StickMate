using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>실제로 생성된 트리에서</b> 닫기 칩을 잰다 — 2026-09-02.
    ///
    /// ============================================================================
    /// 왜 EditMode 토큰 검사만으로는 부족한가
    /// ============================================================================
    /// <c>CloseChipAffordanceTests</c>(EditMode)는 <b>값</b>이 옳은지를 잰다. 그런데 이번 결함의
    /// 형태는 정확히 <i>"값은 있는데 호출부가 안 쓴다"</i>였다 — 팔레트에는 층을 나누는 토큰이
    /// 있었는데 칩 세 개가 창 바탕과 <b>1.00 : 1</b>인 면을 쓰고 있었다. 그 상태에서도 토큰 검사는
    /// 초록이다. 그래서 이 파일은 <b>씬을 띄우고 표면을 열어</b> 다음을 읽는다:
    /// <list type="bullet">
    ///   <item>칩 <c>Image.color</c> (선언값이 아니라 <b>그려진 값</b>)</item>
    ///   <item>바탕은 <b>패널 본체 Image의 색</b>을 읽는다(상수를 믿지 않는다)</item>
    ///   <item>칩 아래 모든 <c>Graphic</c>의 알파 — 반투명 겹 하나면 바탕화면이 비친다</item>
    ///   <item><c>Button.transition</c> — ColorTint가 살아 있으면 pressed에서 글자가 3.68:1이 된다</item>
    ///   <item>칩 사각형 — WCAG 2.2 SC 2.5.8(<see cref="UiChrome.MinTargetSizePoints"/>)</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 네거티브 컨트롤 — 같은 측정 경로로 옛 색을 다시 칠해 본다
    /// ============================================================================
    /// 이 저장소는 <b>실패한 측정과 성공한 측정이 똑같이 생긴</b> 거짓 초록을 하룻밤에 아홉 건 냈다.
    /// 그래서 <see cref="NegativeControl_옛_색을_다시_칠하면_같은_측정이_빨개진다"/>가
    /// <b>똑같은 함수</b>로 옛 면(<see cref="UiChrome.CardSurfaceMuted"/>)을 재서 반드시 미달이
    /// 나오는지 확인한다. 미달이 안 나오면 위쪽 초록은 "탐지력 0"과 구분되지 않는다.
    ///
    /// <para><b>대상은 다섯 표면 여섯 칩이다</b> — 정보창 [✕]·[설정], 설정창 [✕], 팝오버 3종 [✕].
    /// [설정]이 포함된 것은 리더의 실측 때문이다: 그 칩도 창 바탕과 <b>1.01 : 1</b>이었다.
    /// 나란히 붙은 두 칩 중 하나만 고치면 그 자리가 새로 어긋난다.</para>
    /// </summary>
    public sealed class CloseChipAffordanceSurfaceTests
    {
        private const string LogPrefix = "[닫기칩면-TEST]";

        private static readonly Rect AnchorRect = new Rect(400f, 400f, 44f, 44f);

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // ==================== 측정 도구 — 초록/빨강이 같은 경로를 지난다 ====================

        /// <summary>한 칩의 실측치. <b>계산이 아니라 트리에서 읽은 값</b>이다.</summary>
        private struct ChipReading
        {
            public string Where;
            public Color Face;        // 칩 Image의 실제 color
            public Color Backdrop;    // 패널 본체 Image의 실제 color
            public Color Ink;         // 칩 위 Text의 실제 color
            public float FaceRatio;
            public float InkRatio;
            public Vector2 Size;
            public Selectable.Transition Transition;
            public List<string> TranslucentPieces;
        }

        /// <summary>★ 초록도 빨강도 <b>이 함수 하나</b>를 지난다. 측정 경로가 갈리면 비교가 성립하지 않는다.</summary>
        private static ChipReading Measure(string where, Transform canvasRoot, string chipName, string panelBodyName)
        {
            Transform chip = FindDeep(canvasRoot, chipName);
            Assert.IsNotNull(chip, $"{LogPrefix} {where}: 칩 '{chipName}'을(를) 트리에서 못 찾았습니다 — " +
                "이름이 바뀌었다면 이 테스트는 아무것도 재지 않는 상태로 초록이 됩니다.");

            Transform body = FindDeep(canvasRoot, panelBodyName);
            Assert.IsNotNull(body, $"{LogPrefix} {where}: 패널 본체 '{panelBodyName}'을(를) 못 찾았습니다.");

            var faceImage = chip.GetComponent<Image>();
            Assert.IsNotNull(faceImage, $"{LogPrefix} {where}: 칩에 Image가 없습니다.");

            var bodyImage = body.GetComponent<Image>();
            Assert.IsNotNull(bodyImage, $"{LogPrefix} {where}: 패널 본체에 Image가 없습니다.");

            var label = chip.GetComponentInChildren<Text>(true);
            Assert.IsNotNull(label, $"{LogPrefix} {where}: 칩 안에 글자가 없습니다.");

            var button = chip.GetComponent<Button>();
            Assert.IsNotNull(button, $"{LogPrefix} {where}: 칩에 Button이 없습니다 — 누를 수 없는 칩입니다.");

            var translucent = new List<string>();
            foreach (Graphic g in chip.GetComponentsInChildren<Graphic>(true))
            {
                if (g.color.a < 0.999f) translucent.Add($"{g.name}({Hex(g.color)}, α={g.color.a:F2})");
            }

            return new ChipReading
            {
                Where = where,
                Face = faceImage.color,
                Backdrop = bodyImage.color,
                Ink = label.color,
                FaceRatio = UiChrome.ContrastRatio(faceImage.color, bodyImage.color),
                InkRatio = UiChrome.ContrastRatio(label.color, faceImage.color),
                Size = ((RectTransform)chip).rect.size,
                Transition = button.transition,
                TranslucentPieces = translucent,
            };
        }

        /// <summary>★ 면과 잉크를 <b>한 번에</b> 단언한다. 나누면 "면을 밝히면서 글자를 지운" 회귀를 못 잡는다.</summary>
        private static void AssertReadableButton(ChipReading r)
        {
            Assert.GreaterOrEqual(r.FaceRatio, UiChrome.MinNonTextContrast,
                $"{LogPrefix} {r.Where}: 칩 면 {Hex(r.Face)}이 창 바탕 {Hex(r.Backdrop)} 대비 " +
                $"{r.FaceRatio:F2}:1입니다(하한 {UiChrome.MinNonTextContrast:F1}). " +
                "이 앱에서 창을 닫는 마우스 경로는 이 칩 하나뿐이고, 못 찾은 사용자에게는 " +
                "<b>시간 상한이 없는</b> 클릭 차단막이 남습니다.");

            Assert.GreaterOrEqual(r.InkRatio, UiChrome.MinTextContrast,
                $"{LogPrefix} {r.Where}: 칩 위 글자 {Hex(r.Ink)}가 면 {Hex(r.Face)} 대비 " +
                $"{r.InkRatio:F2}:1입니다(하한 {UiChrome.MinTextContrast:F1}). 면을 밝히면서 그 위의 " +
                "글자를 지웠다면 고친 것이 아니라 옮긴 것입니다.");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Transform CanvasRoot(string canvasName)
        {
            GameObject go = GameObject.Find(canvasName);
            Assert.IsNotNull(go, $"{LogPrefix} 캔버스 '{canvasName}'을(를) 못 찾았습니다 — 표면이 열리지 " +
                "않았거나 이름이 바뀌었습니다. 이 상태의 초록은 아무것도 재지 않은 초록입니다.");
            return go.transform;
        }

        // ==================== ① 여섯 칩 전부 — 면과 잉크를 한 쌍으로 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 여섯_칩_전부_면과_잉크가_동시에_하한을_넘는다()
        {
            yield return LoadScene();

            var readings = new List<ChipReading>();

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            info.Open("닫기칩 측정");
            yield return null;
            Transform infoRoot = CanvasRoot("CharacterInfoCanvas");
            readings.Add(Measure("정보창 [✕]", infoRoot, "CloseButton", "InfoPanelBody"));
            readings.Add(Measure("정보창 [설정]", infoRoot, "SettingsButton", "InfoPanelBody"));
            info.Close("측정 끝");
            yield return null;

            var settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            settings.Open("닫기칩 측정");
            yield return null;
            readings.Add(Measure("설정창 [✕]", CanvasRoot("SettingsCanvas"), "Close", "SettingsPanelBody"));
            settings.Close("측정 끝");
            yield return null;

            yield return MeasurePopover<ActionCommandPopover>("행동 명령 팝오버 [✕]", readings);
            yield return MeasurePopover<TodoBoardPopover>("할일 팝오버 [✕]", readings);
            yield return MeasurePopover<FocusSessionPopover>("집중 팝오버 [✕]", readings);

            // 그물이 비어 있지 않다는 확인 — 0개를 재고 "전부 통과"를 내는 것을 막는다.
            Assert.AreEqual(6, readings.Count,
                $"{LogPrefix} 칩을 {readings.Count}개만 쟀습니다(기대 6). 표면 하나가 안 열렸다면 " +
                "그 표면의 결함은 이 테스트에 보이지 않습니다.");

            foreach (ChipReading r in readings)
            {
                AssertReadableButton(r);
                Debug.Log($"{LogPrefix} {r.Where} — 면 {Hex(r.Face)} {r.FaceRatio:F2}:1 / " +
                    $"글자 {Hex(r.Ink)} {r.InkRatio:F2}:1 / {r.Size.x:F0}×{r.Size.y:F0}pt / {r.Transition}");
            }
        }

        // ==================== ② 창 알파 · ③ ColorTint · ④ 크기 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 여섯_칩_전부_불투명하고_ColorTint가_꺼져_있고_목표크기를_넘는다()
        {
            yield return LoadScene();

            var readings = new List<ChipReading>();

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            info.Open("닫기칩 측정");
            yield return null;
            Transform infoRoot = CanvasRoot("CharacterInfoCanvas");
            readings.Add(Measure("정보창 [✕]", infoRoot, "CloseButton", "InfoPanelBody"));
            readings.Add(Measure("정보창 [설정]", infoRoot, "SettingsButton", "InfoPanelBody"));
            info.Close("측정 끝");
            yield return null;

            var settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            settings.Open("닫기칩 측정");
            yield return null;
            readings.Add(Measure("설정창 [✕]", CanvasRoot("SettingsCanvas"), "Close", "SettingsPanelBody"));
            settings.Close("측정 끝");
            yield return null;

            yield return MeasurePopover<ActionCommandPopover>("행동 명령 팝오버 [✕]", readings);
            yield return MeasurePopover<TodoBoardPopover>("할일 팝오버 [✕]", readings);
            yield return MeasurePopover<FocusSessionPopover>("집중 팝오버 [✕]", readings);

            Assert.AreEqual(6, readings.Count, $"{LogPrefix} 칩을 {readings.Count}개만 쟀습니다(기대 6).");

            foreach (ChipReading r in readings)
            {
                // 창 알파의 법칙 — 반투명 겹이 하나라도 있으면 그 화소로 데스크톱이 비친다.
                Assert.IsEmpty(r.TranslucentPieces,
                    $"{LogPrefix} {r.Where}: 칩 안에 반투명 겹이 남아 있습니다 — " +
                    string.Join(", ", r.TranslucentPieces) +
                    ". dstA' = srcA² + dstA(1−srcA)이므로 α0.10짜리 테두리 하나로 그 화소의 창 알파가 " +
                    "0.91이 되고 유저의 <b>바탕화면이 9% 비칩니다</b>(그래서 어두운 바탕화면에서 더 " +
                    "안 보였습니다). 면이 그 역할을 대신하므로 테두리를 되살릴 이유가 없습니다.");

                // ColorTint pressed는 targetGraphic.color에 0.7843을 곱한다 — 밝은 면에서는 부호가 뒤집혀
                // 글자를 3.68:1로 <b>내린다</b>. 어두운 칩에서는 같은 곱셈이 대비를 올려 함정이 안 보였다.
                Assert.AreEqual(Selectable.Transition.None, r.Transition,
                    $"{LogPrefix} {r.Where}: Button.transition이 {r.Transition}입니다. 밝은 면에서 " +
                    "ColorTint의 pressed(×0.7843)는 글자 대비를 " +
                    $"{UiChrome.MinTextContrast:F1}:1 아래로 <b>내립니다</b>.");

                // WCAG 2.2 SC 2.5.8 — 하한은 상수를 참조한다(숫자를 베끼지 않는다).
                Assert.GreaterOrEqual(r.Size.x, UiChrome.MinTargetSizePoints,
                    $"{LogPrefix} {r.Where}: 칩 폭이 {r.Size.x:F0}pt입니다(하한 {UiChrome.MinTargetSizePoints:F0}).");
                Assert.GreaterOrEqual(r.Size.y, UiChrome.MinTargetSizePoints,
                    $"{LogPrefix} {r.Where}: 칩 높이가 {r.Size.y:F0}pt입니다(하한 {UiChrome.MinTargetSizePoints:F0}).");
            }
        }

        // ==================== ⑤ 네거티브 컨트롤 ====================

        /// <summary>★ 같은 측정 함수로 <b>옛 색</b>을 재면 반드시 빨개져야 한다. 안 빨개지면
        /// 위쪽 초록은 "탐지력 0"과 구분되지 않는다 — 이 저장소가 하룻밤에 아홉 번 겪은 형태다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NegativeControl_옛_색을_다시_칠하면_같은_측정이_빨개진다()
        {
            yield return LoadScene();

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            info.Open("네거티브 컨트롤");
            yield return null;

            Transform root = CanvasRoot("CharacterInfoCanvas");

            // (a) 지금 상태는 통과한다.
            ChipReading now = Measure("정보창 [✕](현재)", root, "CloseButton", "InfoPanelBody");
            AssertReadableButton(now);

            // (b) 옛 면으로 되돌려 <b>같은 함수</b>로 다시 잰다.
            Transform chip = FindDeep(root, "CloseButton");
            var image = chip.GetComponent<Image>();
            Color restore = image.color;
            image.color = UiChrome.CardSurfaceMuted;   // 2026-09-02 낮까지의 바로 그 값.
            yield return null;

            ChipReading old = Measure("정보창 [✕](옛 값)", root, "CloseButton", "InfoPanelBody");
            image.color = restore;

            Assert.Less(old.FaceRatio, UiChrome.MinNonTextContrast,
                $"{LogPrefix} 옛 면 {Hex(UiChrome.CardSurfaceMuted)}을 다시 칠했는데 측정이 " +
                $"{old.FaceRatio:F2}:1로 <b>하한을 넘었습니다</b>. 그렇다면 이 테스트는 어떤 색을 " +
                "칠해도 통과한다는 뜻이고, 위쪽 초록은 아무 조건도 아닙니다.");

            Assert.Greater(now.FaceRatio, old.FaceRatio * 3f,
                $"{LogPrefix} 새 면 {now.FaceRatio:F2}:1이 옛 면 {old.FaceRatio:F2}:1보다 뚜렷하게 " +
                "밝지 않습니다 — 값만 바꾸고 그림은 그대로일 수 있습니다.");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 옛 값 {old.FaceRatio:F2}:1 / 새 값 {now.FaceRatio:F2}:1 " +
                "(같은 측정 함수).");

            info.Close("테스트 정리");
        }

        // ==================== 도구 ====================

        private IEnumerator MeasurePopover<T>(string where, List<ChipReading> readings) where T : PopoverPanel
        {
            var popover = Object.FindFirstObjectByType<T>();
            Assert.IsNotNull(popover, $"{LogPrefix} 씬에 {typeof(T).Name}이(가) 없습니다.");
            popover.Open(AnchorRect, "닫기칩 측정");
            yield return null;
            Assume.That(popover.IsOpen, Is.True, $"{LogPrefix} 전제: {typeof(T).Name}이(가) 열려야 합니다.");

            readings.Add(Measure(where, CanvasRoot(typeof(T).Name + "Canvas"), "Close", "PanelBody"));

            popover.Close("측정 끝");
            // 접힘은 벽시계로 넘긴다 — 프레임 수로 기다리면 배치모드(2,000fps 이상)에서 0.01초가 된다.
            yield return new WaitForSecondsRealtime(PopoverPanel.ShrinkSeconds * 3f + 0.1f);
        }
    }
}
