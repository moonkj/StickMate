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

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RailChipsGoDeadAtTheEndsInsteadOfLookingClickable()
        {
            yield return LoadAndOpen();   // 처음 열면 [일반] 탭, 스크롤 0 = 맨 위

            Text up = RailGlyph("PageUp");
            Text down = RailGlyph("PageDown");
            Assert.IsNotNull(up, $"{LogPrefix} [▲] 칩 글리프를 찾지 못했습니다.");
            Assert.IsNotNull(down, $"{LogPrefix} [▼] 칩 글리프를 찾지 못했습니다.");
            Assume.That(up.gameObject.activeInHierarchy, Is.True,
                $"{LogPrefix} 전제: [일반] 탭은 내용이 넘쳐 레일이 보입니다.");

            Assert.AreEqual(UiChrome.InkIcon(false), up.color,
                $"{LogPrefix} 맨 위인데 [▲]가 살아 있는 색입니다 — 완전히 활성으로 보이면서 " +
                "아무 일도 하지 않는 버튼은 이 저장소가 '최악'이라고 부르는 패턴입니다.");
            Assert.AreEqual(UiChrome.InkIcon(true), down.color,
                $"{LogPrefix} 맨 위인데 [▼]까지 죽어 있습니다 — 내려갈 곳이 있습니다.");

            // 바닥까지 내려간다(넘침이 한 번에 안 끝날 수 있으니 여러 번 누른다).
            for (int i = 0; i < 8; i++)
            {
                _settings.FeedClickForTests(_settings.PageDownScreenRect.center);
                yield return null;
            }

            Assert.AreEqual(UiChrome.InkIcon(false), down.color,
                $"{LogPrefix} 맨 아래인데 [▼]가 아직 살아 있는 색입니다.");
            Assert.AreEqual(UiChrome.InkIcon(true), up.color,
                $"{LogPrefix} 맨 아래인데 [▲]가 죽어 있습니다 — 올라갈 곳이 있습니다.");
        }

        private static Text RailGlyph(string chipName)
        {
            foreach (Transform t in Canvas().GetComponentsInChildren<Transform>(true))
            {
                if (t.name != chipName) continue;
                Transform label = t.Find("Label");
                if (label != null) return label.GetComponent<Text>();
            }
            return null;
        }

        /// <summary>★ 네거티브 컨트롤 — 두 색이 애초에 다르지 않으면 위 검사는 무의미하다.</summary>
        [Test]
        public void NegativeControl_RailChipAliveAndDeadInksActuallyDiffer()
        {
            Assert.AreNotEqual(UiChrome.InkIcon(true), UiChrome.InkIcon(false),
                $"{LogPrefix} 아이콘 사다리의 활성/비활성이 같은 색입니다 — 그러면 위 [▲][▼] 검사는 " +
                "어떤 배선에서도 통과하는 빈 조건입니다.");
            Assert.Greater(UiChrome.RelativeLuminance(UiChrome.InkIcon(true)),
                UiChrome.RelativeLuminance(UiChrome.InkIcon(false)),
                $"{LogPrefix} 비활성 아이콘이 활성보다 밝습니다 — 위계가 뒤집혔습니다.");
        }
    }
}
