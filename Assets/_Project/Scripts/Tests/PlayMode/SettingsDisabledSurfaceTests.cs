using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>비활성 컨트롤이 거짓말을 하지 않는가</b> — 2026-09-02.
    ///
    /// <para>이 파일은 <see cref="InkOnSurfaceTests"/>(순수 산술)의 <b>실물판</b>이다. 그쪽은 "규칙이
    /// 옳은가"를 재고, 여기서는 <b>실제로 씬에 그려진 Image/Text의 color 필드</b>를 읽어
    /// "그 규칙이 화면까지 도달했는가"를 잰다. 계산이 맞는데 배선이 빠져 있는 경우를
    /// 산술 테스트는 절대 잡지 못한다 — 이번 사고 자체가 정확히 그 모양이었다(사다리는 옳았고,
    /// 그 사다리를 부르지 않는 컨트롤이 하나 있었다).</para>
    ///
    /// <para><b>대비를 손으로 적지 않는다</b> — 부품이 실제로 칠해진 두 색을 읽어
    /// <see cref="UiChrome.ContrastRatio"/>(프로덕션과 <b>같은 함수</b>)로 잰다.</para>
    /// </summary>
    public sealed class SettingsDisabledSurfaceTests
    {
        private const string LogPrefix = "[비활성면-TEST]";

        private SettingsWindow _settings;

        private IEnumerator LoadAndOpen()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            _settings.Open("테스트");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CloseEverything()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            _settings = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        private static GameObject Canvas()
        {
            GameObject go = GameObject.Find("SettingsCanvas");
            Assert.IsNotNull(go, $"{LogPrefix} SettingsCanvas를 찾지 못했습니다.");
            return go;
        }

        /// <summary>이름으로 행을 찾는다 — 프로덕션도 같은 관례로 부품을 찾는다(SettingsControls).</summary>
        private static Transform FindRow(string rowKey)
        {
            foreach (Transform t in Canvas().GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Row_" + rowKey) return t;
            }
            return null;
        }

        /// <summary>세그먼트 칩(면, 글자) 쌍. 칩은 <c>Btn{i}</c>/<c>Seg{i}</c> 어느 이름이든
        /// "Image + 자식 Text 하나"라는 모양으로 찾는다 — 이름 규칙이 바뀌어도 조용히 0건이 되지 않게
        /// 개수를 함께 단언한다.</summary>
        private static List<(Image face, Text label)> ChipsIn(Transform row)
        {
            var found = new List<(Image, Text)>();
            if (row == null) return found;
            foreach (Image img in row.GetComponentsInChildren<Image>(true))
            {
                if (img.transform == row) continue;
                Text label = null;
                foreach (Text t in img.GetComponentsInChildren<Text>(true))
                {
                    if (t.transform.parent == img.transform) { label = t; break; }
                }
                if (label != null) found.Add((img, label));
            }
            return found;
        }

        // ==================== A — 세그먼트 칩 ====================

        /// <summary>
        /// ★ <c>말투</c>는 <b>준비 중</b>이라 태어날 때부터 비활성이다. 옛 코드에서 이 행의
        /// <c>[반말]</c> 칩이 <b>1.28 : 1</b>이었다 — 이 앱 최저값이자, 페르소나가
        /// <i>"한 글자도 없다"</i>고 적게 만든 2.35보다 <b>낮은</b> 값이다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DisabledSegmentChipsAreNeitherAccentFilledNorErased()
        {
            yield return LoadAndOpen();
            _settings.FeedClickForTests(_settings.TabScreenRect(SettingsWindow.Tab.Character).center);
            yield return null;

            Transform row = FindRow("character.tone");
            Assert.IsNotNull(row, $"{LogPrefix} `말투` 행(Row_character.tone)을 찾지 못했습니다 — " +
                "행 이름이 바뀌었다면 이 테스트도 함께 고쳐야 합니다.");

            List<(Image face, Text label)> chips = ChipsIn(row);
            Assert.GreaterOrEqual(chips.Count, 2,
                $"{LogPrefix} `말투` 칩을 {chips.Count}개 찾았습니다 — [반말]/[존댓말] 둘이어야 합니다.");

            foreach ((Image face, Text label) in chips)
            {
                float ratio = UiChrome.ContrastRatio(label.color, face.color);
                Assert.GreaterOrEqual(ratio, UiChrome.MinTextContrast,
                    $"{LogPrefix} 비활성 칩 \"{label.text}\"가 #{ColorUtility.ToHtmlStringRGB(label.color)} " +
                    $"on #{ColorUtility.ToHtmlStringRGB(face.color)} = {ratio:F2}:1입니다. " +
                    $"하한 {UiChrome.MinTextContrast:F1}:1 — 이 자리가 1.28:1이었습니다.");

                // 대비 1.00 = 같은 색. 강조색과 <b>구별될 만큼</b> 떨어져 있어야 한다.
                Assert.Greater(UiChrome.ContrastRatio(face.color, SettingsControls.AccentSolid), 1.05f,
                    $"{LogPrefix} 비활성 칩 \"{label.text}\"의 면이 아직 강조색입니다 " +
                    $"(#{ColorUtility.ToHtmlStringRGB(face.color)}) — 컨트롤이 '눌러도 된다'고 " +
                    "거짓말하고 있습니다. 면을 죽여야 글자가 살아납니다.");
            }
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 규칙</b>을 그대로 재현하면 이 검사가 실제로 빨개지는가.
        /// 재현되지 않으면 위 초록은 "고쳤다"가 아니라 "원래 무해했다"일 수 있다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NegativeControl_OldRuleReallyProduces1Point3To1()
        {
            yield return LoadAndOpen();

            // 옛 코드가 하던 그대로: 면은 active만 보고 강조색, 글자는 !Interactable이라 InkTitle(false).
            Color oldFace = SettingsControls.AccentSolid;
            Color oldInk = UiChrome.InkTitle(false);
            float oldRatio = UiChrome.ContrastRatio(oldInk, oldFace);

            Assert.Less(oldRatio, UiChrome.MinTextContrast,
                $"{LogPrefix} 옛 짝이 {oldRatio:F2}:1로 하한을 넘었습니다 — 위 테스트가 지키는 대상이 " +
                "실재하지 않는다는 뜻이고, 그러면 그 초록은 아무 조건도 아닙니다.");
            Assert.Less(oldRatio, 1.5f,
                $"{LogPrefix} 옛 짝이 {oldRatio:F2}:1입니다 — 실측 1.28:1이 재현되지 않았습니다.");
        }

        // ==================== B — 색 견본 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DisabledSwatchesAreVisiblyDimmerThanTheWorkingOnes()
        {
            yield return LoadAndOpen();
            _settings.FeedClickForTests(_settings.TabScreenRect(SettingsWindow.Tab.Character).center);
            yield return null;

            Transform live = FindRow("character.ink");        // 실제로 동작하는 행
            Transform dead = FindRow("character.point");      // `포인트 컬러 (준비 중)`
            Assert.IsNotNull(live, $"{LogPrefix} `잉크색` 행을 찾지 못했습니다.");
            Assert.IsNotNull(dead, $"{LogPrefix} `포인트 컬러` 행을 찾지 못했습니다.");

            List<Image> liveSwatches = SwatchesIn(live);
            List<Image> deadSwatches = SwatchesIn(dead);
            Assert.Greater(liveSwatches.Count, 0, $"{LogPrefix} 동작하는 견본을 찾지 못했습니다.");
            Assert.Greater(deadSwatches.Count, 0, $"{LogPrefix} 비활성 견본을 찾지 못했습니다.");

            float liveMax = 0f, deadMax = 0f;
            foreach (Image s in liveSwatches) liveMax = Mathf.Max(liveMax, UiChrome.RelativeLuminance(s.color));
            foreach (Image s in deadSwatches) deadMax = Mathf.Max(deadMax, UiChrome.RelativeLuminance(s.color));

            Assert.Less(deadMax, liveMax * 0.75f,
                $"{LogPrefix} 비활성 견본의 최대 휘도({deadMax:F4})가 동작하는 견본({liveMax:F4})과 " +
                "거의 같습니다 — `포인트 컬러 (준비 중)`가 바로 위 `잉크색`과 픽셀 단위로 같은 채도로 " +
                "빛나면, 캡션을 읽기 전까지는 눌러도 되는 줄 압니다(세그먼트 칩과 같은 뿌리).");
        }

        private static List<Image> SwatchesIn(Transform row)
        {
            var found = new List<Image>();
            foreach (Image img in row.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject.name.StartsWith("Swatch")) found.Add(img);
            }
            return found;
        }

        // ==================== E — 레일 끝 칩 ====================

        /// <summary>
        /// ★★ 2026-09-06 — <b>판정 축이 「글리프 색」에서 「면」으로 옮겨 왔다</b>
        /// (<c>docs/UI_ALPHA_BLEED_POLICY.md</c> §7-3 F1).
        ///
        /// <para>옛 판정은 <c>glyph.color == UiChrome.InkIcon(살아있음)</c>이라는 <b>등호</b>였다.
        /// 그건 "프로덕션이 그 함수를 불렀는가"를 물을 뿐이고, 그 사이 <b>칩의 면은 창 바탕보다
        /// 어두운 채</b>(1.01 : 1) 6일을 지났다 — 등호는 그걸 볼 수 없었다. 그래서 지금은
        /// <b>실제로 칠해진 두 색을 읽어 대비를 잰다</b>: 살아 있는 칩은 <b>면으로</b> 서고,
        /// 죽은 칩은 물러나며, 어느 상태에서도 화살표는 자기 면 위에서 읽힌다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RailChipsGoDeadAtTheEndsInsteadOfLookingClickable()
        {
            yield return LoadAndOpen();   // 처음 열면 [일반] 탭, 스크롤 0 = 맨 위

            (Image upFace, Text up) = RailChip("PageUp");
            (Image downFace, Text down) = RailChip("PageDown");
            Assert.IsNotNull(up, $"{LogPrefix} [▲] 칩 글리프를 찾지 못했습니다.");
            Assert.IsNotNull(down, $"{LogPrefix} [▼] 칩 글리프를 찾지 못했습니다.");
            Assert.IsNotNull(upFace, $"{LogPrefix} [▲] 칩의 면(Image)을 찾지 못했습니다.");
            Assert.IsNotNull(downFace, $"{LogPrefix} [▼] 칩의 면(Image)을 찾지 못했습니다.");
            Assume.That(up.gameObject.activeInHierarchy, Is.True,
                $"{LogPrefix} 전제: [일반] 탭은 내용이 넘쳐 레일이 보입니다.");

            AssertRailChip("[▲]", upFace, up, alive: false, where: "맨 위");
            AssertRailChip("[▼]", downFace, down, alive: true, where: "맨 위");

            // 두 상태가 <b>실제로 다르게</b> 그려졌는가 — 같은 모습이면 위 두 줄은 빈 조건이다.
            Assert.Greater(UiChrome.ContrastRatio(upFace.color, downFace.color), 1.5f,
                $"{LogPrefix} 맨 위인데 [▲]와 [▼]의 면이 사실상 같은 색입니다 " +
                $"(#{ColorUtility.ToHtmlStringRGB(upFace.color)} vs " +
                $"#{ColorUtility.ToHtmlStringRGB(downFace.color)}) — 살아 있음/죽음이 안 갈립니다.");

            // 바닥까지 내려간다(넘침이 한 번에 안 끝날 수 있으니 여러 번 누른다).
            for (int i = 0; i < 8; i++)
            {
                _settings.FeedClickForTests(_settings.PageDownScreenRect.center);
                yield return null;
            }

            AssertRailChip("[▼]", downFace, down, alive: false, where: "맨 아래");
            AssertRailChip("[▲]", upFace, up, alive: true, where: "맨 아래");
        }

        /// <summary>레일 칩 한 개의 (면, 글리프)를 <b>실제 칠해진 값으로</b> 판정한다.
        /// <para>hex도 토큰 이름도 베끼지 않는다 — 재는 것은 <b>관계</b>다:
        /// ① 살아 있으면 면이 창 바탕 위에 <b>선다</b>(비텍스트 하한) / 죽었으면 <b>물러난다</b>,
        /// ② 어느 상태에서도 화살표는 자기 면 위에서 읽힌다.</para></summary>
        private static void AssertRailChip(string name, Image face, Text glyph, bool alive, string where)
        {
            float onPanel = UiChrome.ContrastRatio(face.color, UiChrome.PanelSurface);
            float inkOnFace = UiChrome.ContrastRatio(glyph.color, face.color);
            string hex = $"면 #{ColorUtility.ToHtmlStringRGB(face.color)} / " +
                         $"글리프 #{ColorUtility.ToHtmlStringRGB(glyph.color)}";

            if (alive)
            {
                Assert.GreaterOrEqual(onPanel, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} {where}에서 살아 있어야 할 {name} 칩의 면이 창 바탕 대비 " +
                    $"{onPanel:F2}:1입니다({hex}). 누를 수 있는 칸이 <b>면으로 보이지 않으면</b> " +
                    "그 칩은 화면에 없는 것과 같습니다(하한 " +
                    $"{UiChrome.MinNonTextContrast:F1}:1).");
            }
            else
            {
                Assert.Less(onPanel, UiChrome.MinNonTextContrast,
                    $"{LogPrefix} {where}에서 죽어야 할 {name} 칩이 아직 면으로 서 있습니다 " +
                    $"({onPanel:F2}:1, {hex}) — 완전히 활성으로 보이면서 아무 일도 하지 않는 버튼은 " +
                    "이 저장소가 '최악'이라고 부르는 패턴입니다.");
            }

            Assert.GreaterOrEqual(inkOnFace, UiChrome.MinTextContrast,
                $"{LogPrefix} {where}의 {name} 화살표가 자기 면 위에서 {inkOnFace:F2}:1입니다({hex}) — " +
                "면을 바꾸면서 잉크를 안 따라 바꾸면 글리프가 지워집니다(정책 §2-E).");
        }

        private static (Image, Text) RailChip(string chipName)
        {
            foreach (Transform t in Canvas().GetComponentsInChildren<Transform>(true))
            {
                if (t.name != chipName) continue;
                Transform label = t.Find("Label");
                return (t.GetComponent<Image>(), label != null ? label.GetComponent<Text>() : null);
            }
            return (null, null);
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 배선</b>이 이 판정에서 실제로 빨개지는가.
        /// 옛 칩은 살아 있든 죽었든 면이 <see cref="UiChrome.CardSurfaceMuted"/> 하나였고, 그 면은
        /// 창 바탕보다 <b>어둡다</b>. 그게 하한을 못 넘는다는 사실이 재현되지 않으면 위 초록은
        /// 아무 조건도 아니다.</summary>
        [Test]
        public void NegativeControl_OldRailChipFaceCouldNotStandOnThePanel()
        {
            float old = UiChrome.ContrastRatio(UiChrome.CardSurfaceMuted, UiChrome.PanelSurface);
            Assert.Less(old, UiChrome.MinNonTextContrast,
                $"{LogPrefix} 옛 칩 면이 창 바탕 대비 {old:F2}:1로 하한을 넘었습니다 — 그렇다면 " +
                "이 라운드가 고친 대상이 실재하지 않는다는 뜻이고, 위 판정은 빈 조건입니다.");
            Assert.Less(old, 1.1f,
                $"{LogPrefix} 옛 칩 면이 {old:F2}:1입니다 — 실측 1.01:1(창 바탕보다 어둡다)이 " +
                "재현되지 않았습니다.");

            // 그리고 새 면은 같은 자로 재서 <b>넘어야</b> 한다 — 두 방향을 같은 함수로 잰다.
            float now = UiChrome.ContrastRatio(SettingsControls.ControlFaceOnPanel, UiChrome.PanelSurface);
            Assert.GreaterOrEqual(now, UiChrome.MinNonTextContrast,
                $"{LogPrefix} 새 칩 면이 창 바탕 대비 {now:F2}:1입니다 — 규칙이 만든 값이 하한을 " +
                "못 넘으면 F1 자체가 성립하지 않습니다.");
        }
    }
}
