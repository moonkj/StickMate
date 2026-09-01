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
    /// ★ 2026-08-31 2차 — <b>"뒤 창이 비쳐 보인다"</b>가 정보창 말고 <b>두 창 더</b> 남아 있었다.
    /// <see cref="InfoWindowPanelOpacityTests"/>가 캐릭터 정보창에 대해 세운 잠금을
    /// <b>팝오버 3종</b>(집중 모드 / 할일 / 행동 명령)과 <b>좌하단 호버 패널</b>에 그대로 복제한다.
    ///
    /// ============================================================================
    /// 두 창이 각각 어떻게 깨져 있었나 (실측)
    /// ============================================================================
    /// 공식은 하나다(<c>UiChrome</c> 파일 머리 "알파 채널의 법칙"):
    /// <code>dstA' = srcA² + dstA(1 − srcA)</code>
    ///
    ///  · <b>팝오버</b>(<see cref="PopoverPanel"/>) — 정보창과 <b>완전히 같은 결함</b>이었다.
    ///    그림자 2겹을 패널 <c>Image</c>의 <b>자식</b>으로 달고 <c>SetAsFirstSibling()</c>으로 뒤로
    ///    보내려 했지만, uGUI는 부모 Graphic을 자식보다 먼저 그리므로 형제 순서로는 "부모 위"를
    ///    벗어날 수 없다.
    ///    <code>1 → 0.55² + 1×0.45 = 0.7525 → 0.28² + 0.7525×0.72 = 0.6202</code>
    ///    = 데스크톱이 <b>38.0%</b> 비쳤다.
    ///
    ///  · <b>옛 좌하단 호버 패널</b> — 겹 순서는 옳았고 <b>알파 자체</b>가 문제였다(본체 <c>alpha: 0.86</c>).
    ///    <code>0.28²=0.0784 → 0.55²+0.0784×0.45=0.3378 → 0.86²+0.3378×0.14=0.7869</code>
    ///    패널 한가운데에서 <b>21.3%</b>가 비쳤다. 그 패널 자체는 2026-09-01 사용자 요청으로
    ///    <b>삭제됐지만</b>, 그때 밝혀진 <b>원인</b>(α&lt;1은 무엇 위에 그리든 창 알파를 끌어내린다)은
    ///    이 앱의 모든 UI에 그대로 유효하다 — 그래서 아래 (3)(4)는 남긴다.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 절대 조건
    /// ============================================================================
    ///  ① 팝오버 <b>3종 전부</b>의 창 알파가 1이다(뒤 창이 1%도 비치지 않는다).
    ///  ② 두 창의 패널 컨테이너에 <b>Graphic이 없다</b> — 있으면 그 즉시 그림자가 본체 위로 올라간다.
    ///  ③ 본체 <b>이후에 그려지는 창 크롬 겹</b>(보더/하이라이트)의 알파가 전부 1이다.
    ///     이것들은 사각형 전체를 덮지 않아 ①의 측정에 잡히지 않지만, 그 1px 위에서만 창 알파를
    ///     떨어뜨려 "테두리 부분만 비치는" 결함이 된다(<see cref="UiChrome.Flatten"/>이 답이다).
    ///  ④ <see cref="UiChrome.AddGlassPanel"/>에 <b>alpha 파라미터가 없다</b> — 안전한 값이 1 하나뿐인
    ///     파라미터는 함정이다. 누가 다시 넣으면 여기서 멈춘다.
    ///  ⑤ (네거티브 컨트롤 2건) 각 창의 <b>옛 구조를 그대로 재현하면</b> ①이 실제로 깨진다 —
    ///     이 테스트가 진짜로 그 버그를 잡는다는 증명이다.
    ///
    /// ============================================================================
    /// 측정 방식 — <see cref="InfoWindowPanelOpacityTests"/>와 같다(의도적 복제)
    /// ============================================================================
    /// 색 상수를 다시 읽지 않고 <b>실제로 만들어진 계층</b>을 그리기 순서대로 걸으며 실측한다.
    /// 한 가지가 추가됐다: <b><see cref="CanvasGroup"/>의 알파를 누적해 곱한다</b>. 두 창 모두
    /// 등장 연출을 CanvasGroup으로 하므로, 연출이 끝난 뒤에도 알파가 1로 돌아오지 않으면
    /// 그것 역시 그대로 "뒤 창 비침"이다 — 겹의 색만 봐서는 절대 안 보이는 경로다.
    /// </summary>
    internal static class WindowAlphaProbe
    {
        /// <summary>이 값 미만이면 어두운 표면에서 사람 눈에 "뒤가 비친다"로 읽힌다.</summary>
        public const float RequiredWindowAlpha = 0.999f;

        /// <summary>uGUI 그리기 순서 — 부모 Graphic이 먼저, 그 다음 자식들이 형제 순서대로.
        /// <paramref name="groupAlpha"/>는 조상 <see cref="CanvasGroup"/> 알파의 누적곱이다.</summary>
        public static void CollectDrawOrder(Transform t, float groupAlpha, List<(Graphic g, float alpha)> into)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return;

            var cg = t.GetComponent<CanvasGroup>();
            if (cg != null && cg.enabled) groupAlpha *= Mathf.Clamp01(cg.alpha);

            var g = t.GetComponent<Graphic>();
            if (g != null && g.enabled) into.Add((g, groupAlpha));
            for (int i = 0; i < t.childCount; i++) CollectDrawOrder(t.GetChild(i), groupAlpha, into);
        }

        /// <summary>조상 <see cref="CanvasGroup"/>까지 거슬러 올라가 누적 알파를 구한다 —
        /// 패널이 캔버스 루트가 아니라 그 아래에 있을 때(팝오버/호버 패널 둘 다 그렇다) 필요하다.</summary>
        public static float InheritedGroupAlpha(Transform panel)
        {
            float a = 1f;
            for (Transform t = panel.parent; t != null; t = t.parent)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null && cg.enabled) a *= Mathf.Clamp01(cg.alpha);
            }
            return a;
        }

        public static Rect ScreenRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);   // ScreenSpaceOverlay 캔버스에서는 월드 = 화면 픽셀.
            return Rect.MinMaxRect(
                Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }

        /// <summary>이 겹이 <b>패널 한가운데</b>를 얼마나 덮는가(0~1).
        /// 스프라이트 중앙 텍셀을 읽으므로 <see cref="UiChrome.RoundedOutline"/> 같은 "가운데가 빈"
        /// 테두리는 자동으로 0으로 걸러진다. <see cref="Text"/>는 획 가장자리에서만 알파가 끼므로 0으로 본다.</summary>
        public static float CoverageAtCenter(Graphic g)
        {
            if (g is Text) return 0f;
            if (g is Image img)
            {
                if (img.sprite == null) return g.color.a;
                Texture2D tex = img.sprite.texture;
                if (tex == null || !tex.isReadable) return g.color.a;
                Rect r = img.sprite.textureRect;
                int x = Mathf.Clamp(Mathf.FloorToInt(r.center.x), 0, tex.width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(r.center.y), 0, tex.height - 1);
                return g.color.a * tex.GetPixel(x, y).a;
            }
            return g.color.a;
        }

        /// <summary>패널 사각형을 <b>통째로 덮는</b> 겹만 모아 실제 블렌드 공식으로 합성한 창 알파.
        /// 카드/글자/구분선처럼 일부만 덮는 것은 제외한다 — "창 전체가 비친다"는 증상의 원인이 될 수
        /// 없고, 넣으면 배치가 바뀔 때마다 흔들리는 테스트가 된다.</summary>
        public static float SimulateWindowAlpha(RectTransform panel, out string trace)
        {
            Rect panelRect = ScreenRectOf(panel);
            var order = new List<(Graphic g, float alpha)>(64);
            CollectDrawOrder(panel, InheritedGroupAlpha(panel), order);

            const float Eps = 0.5f;   // 화면 픽셀.
            float fbA = 0f;           // 카메라가 알파 0으로 지운 상태에서 출발한다.
            var log = new System.Text.StringBuilder();
            log.Append("알파 0(투명 배경)");

            for (int i = 0; i < order.Count; i++)
            {
                (Graphic g, float groupAlpha) = order[i];
                Rect r = ScreenRectOf(g.rectTransform);
                bool coversPanel = r.xMin <= panelRect.xMin + Eps && r.xMax >= panelRect.xMax - Eps
                                && r.yMin <= panelRect.yMin + Eps && r.yMax >= panelRect.yMax - Eps;
                if (!coversPanel) continue;

                float srcA = CoverageAtCenter(g) * groupAlpha;
                if (srcA <= 0.001f) continue;

                fbA = srcA * srcA + fbA * (1f - srcA);
                log.Append($" → [{g.name} α{srcA:F2}] {fbA:F4}");
            }

            trace = log.ToString();
            return fbA;
        }

        /// <summary>본체(첫 번째 α=1 전면 덮개) <b>다음에</b> 그려지는 크롬 겹의 알파가 전부 1인지 본다.
        /// 이것들은 사각형을 다 덮지 않아 <see cref="SimulateWindowAlpha"/>에 잡히지 않지만,
        /// 그 1px 위에서만 창 알파를 떨어뜨려 "테두리만 비치는" 결함을 만든다.</summary>
        public static void AssertChromeLayersAboveBodyAreOpaque(RectTransform panel, string label)
        {
            for (int i = 0; i < panel.childCount; i++)
            {
                Transform child = panel.GetChild(i);
                string n = child.name;
                bool isChrome = n.EndsWith("Body") || n.EndsWith("Outline") || n.EndsWith("Highlight")
                                || n.EndsWith("Border");
                if (!isChrome) continue;

                var img = child.GetComponent<Image>();
                if (img == null) continue;

                Assert.GreaterOrEqual(img.color.a, RequiredWindowAlpha,
                    $"{label} 창 크롬 겹 '{n}'의 알파가 {img.color.a:F3}입니다 — 1이어야 합니다. " +
                    "이 겹 아래에 있는 것은 <항상> 방금 그린 불투명 본체이므로, 같은 블렌드 결과를 " +
                    "UiChrome.Flatten으로 미리 계산해 α=1로 칠하면 <색은 완전히 같고 알파만 1로 남습니다>. " +
                    "α<1로 두면 그 선/링 위에서만 유저의 다른 창이 비쳐 보입니다.");
            }
        }

        /// <summary>이름으로 캔버스를 찾고 그 아래에서 패널 컨테이너를 찾는다.
        /// 이름이 바뀌면 이 테스트가 즉시 실패해 알려 준다 — "첫 자식"으로 찾으면 겹이 하나 추가되는
        /// 것만으로 조용히 엉뚱한 사각형을 재게 된다.</summary>
        public static RectTransform FindPanelUnderCanvas(string canvasName, string panelName)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (c == null || c.name != canvasName) continue;
                RectTransform found = FindDescendant(c.transform, panelName);
                if (found != null) return found;
            }
            return null;
        }

        private static RectTransform FindDescendant(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child as RectTransform;
                RectTransform deeper = FindDescendant(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }

    // ================================================================================
    // 팝오버 3종
    // ================================================================================
    public sealed class PopoverPanelOpacityTests
    {
        private const string LogPrefix = "[창알파-팝오버]";

        private PopoverPanel _opened;
        private GameObject _syntheticRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_opened != null) _opened.Close("테스트 정리");
            _opened = null;
            if (_syntheticRoot != null) Object.DestroyImmediate(_syntheticRoot);
            _syntheticRoot = null;
            yield return null;
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        /// <summary>등장 연출(0.16초)이 끝나야 CanvasGroup 알파가 1이 된다. 애니메이션 중의 반투명은
        /// 결함이 아니므로 <b>정지 상태</b>를 잰다.</summary>
        private static IEnumerator WaitForGrow()
        {
            yield return new WaitForSecondsRealtime(0.35f);
        }

        // ============================================================================
        // (1) 핵심 — 팝오버 3종 전부의 창 알파가 1이다
        // ============================================================================
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator EveryPopoverPanelIsFullyOpaqueSoNoOtherWindowShowsThrough()
        {
            yield return LoadScene();

            var popovers = Object.FindObjectsByType<PopoverPanel>(FindObjectsSortMode.None);
            Assert.Greater(popovers.Length, 0,
                $"{LogPrefix} 씬에 PopoverPanel이 하나도 없습니다 — SceneBootstrapper 배선이 빠졌습니다.");

            for (int i = 0; i < popovers.Length; i++)
            {
                PopoverPanel popover = popovers[i];
                string typeName = popover.GetType().Name;

                _opened = popover;
                popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 알파 테스트");
                yield return WaitForGrow();

                RectTransform panel = WindowAlphaProbe.FindPanelUnderCanvas(typeName + "Canvas", "Panel");
                Assert.IsNotNull(panel,
                    $"{LogPrefix} {typeName}Canvas 아래에서 'Panel'을 찾지 못했습니다.");

                // ② 컨테이너에 Graphic이 있으면 그림자가 본체 위로 올라간다 — 구조 자체를 잠근다.
                var containerGraphic = panel.GetComponent<Graphic>();
                Assert.IsNull(containerGraphic,
                    $"{LogPrefix} {typeName}의 패널 컨테이너에 " +
                    $"{(containerGraphic != null ? containerGraphic.GetType().Name : "?")}이(가) 붙어 있습니다 — " +
                    "uGUI는 부모를 자식보다 먼저 그리므로 그 즉시 그림자가 패널 <위>로 올라가고 창 알파가 " +
                    "무너집니다(UiChrome.AddOpaquePanel이 정답 형태입니다).");

                float alpha = WindowAlphaProbe.SimulateWindowAlpha(panel, out string trace);
                Debug.Log($"{LogPrefix} {typeName}: {trace} = 창 알파 {alpha:F4} " +
                    $"(뒤 창 비침 {(1f - alpha) * 100f:F1}%).");

                Assert.GreaterOrEqual(alpha, WindowAlphaProbe.RequiredWindowAlpha,
                    $"{LogPrefix} {typeName}의 창 알파가 {alpha:F4}입니다 — 유저의 데스크톱이 " +
                    $"{(1f - alpha) * 100f:F1}% 비쳐 보입니다. 겹 합성 경로: {trace}");

                // ③ 본체 위에 얹히는 크롬(보더)도 α=1이어야 한다.
                WindowAlphaProbe.AssertChromeLayersAboveBodyAreOpaque(panel, typeName);

                popover.Close("테스트 다음 항목");
                _opened = null;
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        // ============================================================================
        // (2) 네거티브 컨트롤 — 옛 구조를 재현하면 (1)이 실제로 깨진다
        // ============================================================================
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator OldPopoverStructureWithShadowInsideTheBodyIsActuallyDetected()
        {
            yield return LoadScene();

            // 수정 전 PopoverPanel.BuildChrome을 손으로 재현한다. UiChrome.AddShadow를 쓰면 이제
            // Error를 남기므로(그 방어막이 없었다고 가정한) <b>순수한 옛 계층</b>을 직접 세운다.
            _syntheticRoot = new GameObject("SyntheticOldPopoverCanvas", typeof(Canvas), typeof(CanvasScaler));
            _syntheticRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            Image body = UiChrome.AddSurface(_syntheticRoot.transform, "OldPanel",
                UiChrome.PanelSurface, UiChrome.RadiusPanel);
            RectTransform oldPanel = body.rectTransform;
            oldPanel.anchorMin = oldPanel.anchorMax = oldPanel.pivot = new Vector2(0.5f, 0.5f);
            oldPanel.anchoredPosition = Vector2.zero;
            oldPanel.sizeDelta = new Vector2(300f, 220f);

            // 옛 코드: AddShadow(...).SetAsFirstSibling(). 형제 순서를 바꿔도 부모 Image 위를 못 벗어난다.
            AddOldShadowLayer(oldPanel, "OldShadowAmbient", 6f * 2.4f, new Vector2(0f, -2f) * 2.3f, 0.28f);
            AddOldShadowLayer(oldPanel, "OldShadowKey", 6f, new Vector2(0f, -2f), 0.55f);
            oldPanel.GetChild(oldPanel.childCount - 1).SetAsFirstSibling();
            UiChrome.AddOutline(oldPanel, "OldBorder", UiChrome.PanelBorder, UiChrome.RadiusPanel);
            yield return null;

            float alpha = WindowAlphaProbe.SimulateWindowAlpha(oldPanel, out string trace);
            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 옛 팝오버 구조 재현: {trace} = 창 알파 {alpha:F4} " +
                $"(뒤 창 비침 {(1f - alpha) * 100f:F1}%).");

            Assert.Less(alpha, 0.7f,
                $"{LogPrefix} 네거티브 컨트롤이 통과해 버렸습니다(창 알파 {alpha:F4}) — 이 테스트는 옛 결함을 " +
                "잡지 못합니다. 측정 방식(겹 수집/커버리지 판정)을 먼저 의심해야 합니다.");
        }

        private static void AddOldShadowLayer(RectTransform parent, string name, float spread,
            Vector2 offset, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            UiChrome.Stretch(rt);
            rt.offsetMin = new Vector2(-spread + offset.x, -spread + offset.y);
            rt.offsetMax = new Vector2(spread + offset.x, spread + offset.y);
            var image = go.GetComponent<Image>();
            image.sprite = UiChrome.RoundedFill(UiChrome.RadiusPanel + Mathf.RoundToInt(spread));
            image.type = Image.Type.Sliced;
            image.color = new Color(0f, 0f, 0f, alpha);
            image.raycastTarget = false;
        }
    }

    // ================================================================================
    // 유리 알파 규칙 자체의 잠금 — 2026-09-01 좌하단 호버 패널 삭제 후 남은 부분
    //
    // 옛 CornerHoverPanel 본체/미리보기 카드를 실제로 측정하던 테스트 2건은 그 컴포넌트와 함께
    // 지웠다(측정 대상이 없는 테스트는 남길 수 없다). 그 라운드가 <b>발견한 규칙</b>은 패널이 아니라
    // UiChrome에 속하므로 여기 남는다 — 다음에 누가 α<1 유리를 다시 들여오면 (3)이 막고,
    // (4)가 "그 측정이 실제로 결함을 잡는다"를 증명한다.
    // ================================================================================
    public sealed class GlassPanelAlphaRuleTests
    {
        private const string LogPrefix = "[창알파-유리규칙]";

        private GameObject _syntheticRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_syntheticRoot != null) Object.DestroyImmediate(_syntheticRoot);
            _syntheticRoot = null;
            yield return null;
        }

        // ============================================================================
        // (3) 구조 잠금 — AddGlassPanel에 alpha 파라미터가 다시 생기면 여기서 멈춘다
        // ============================================================================
        [Test]
        public void GlassPanelFactoryHasNoAlphaParameterBecauseOnlyOneIsSafe()
        {
            System.Reflection.MethodInfo m = typeof(UiChrome).GetMethod("AddGlassPanel");
            Assert.IsNotNull(m, "UiChrome.AddGlassPanel이 사라졌습니다 — 이 테스트를 갱신하세요.");

            foreach (System.Reflection.ParameterInfo p in m.GetParameters())
            {
                Assert.AreNotEqual("alpha", p.Name,
                    "UiChrome.AddGlassPanel에 alpha 파라미터가 다시 생겼습니다. 이 앱의 UI는 알파 0으로 지운 " +
                    "프레임버퍼 위에 Blend SrcAlpha OneMinusSrcAlpha로 그려지고 그 블렌드가 <알파 채널에도> " +
                    "적용되므로, α<1은 무엇 위에 그리든 창 알파를 끌어내립니다(불투명 바탕 위에서도). " +
                    "안전한 값이 1 하나뿐인 파라미터는 함정입니다 — 넣지 마세요.");
            }
        }

        // ============================================================================
        // (4) 네거티브 컨트롤 — alpha 0.86 유리를 재현하면 측정이 실제로 결함을 잡는다
        // ============================================================================
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator OldTranslucentGlassAtAlpha086IsActuallyDetected()
        {
            yield return null;

            // 겹 순서는 <b>옳게</b> 세운다(컨테이너에 Graphic 없음). 즉 여기서 알파가 무너진다면 원인은
            // 순서가 아니라 <b>알파값 그 자체</b>다 — 옛 호버 패널의 결함이 팝오버/정보창과 <b>다른
            // 종류</b>였음을 이 컨트롤이 분리해 보여 준다.
            // ★ 2026-09-02: 옛 재현에 있던 그림자 겹은 뺐다(그림자 API가 삭제됐고, 이 컨트롤이 잡으려는
            //   것은 그림자가 아니라 α0.86 본체다 — 없어도 판정은 그대로 성립한다).
            _syntheticRoot = new GameObject("SyntheticOldGlassCanvas", typeof(Canvas), typeof(CanvasScaler));
            _syntheticRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var containerGo = new GameObject("OldGlass", typeof(RectTransform));
            containerGo.transform.SetParent(_syntheticRoot.transform, false);
            var container = containerGo.GetComponent<RectTransform>();
            container.anchorMin = container.anchorMax = container.pivot = new Vector2(0.5f, 0.5f);
            container.anchoredPosition = Vector2.zero;
            container.sizeDelta = new Vector2(240f, 392f);

            const float OldGlassAlpha = 0.86f;
            Image body = UiChrome.AddSurface(container, "Body",
                new Color(UiChrome.PanelSurface.r, UiChrome.PanelSurface.g, UiChrome.PanelSurface.b, OldGlassAlpha),
                UiChrome.RadiusPanel);
            UiChrome.Stretch(body.rectTransform);
            UiChrome.AddOutline(container, "Border", UiChrome.PanelBorder, UiChrome.RadiusPanel);
            yield return null;

            float alpha = WindowAlphaProbe.SimulateWindowAlpha(container, out string trace);
            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 옛 유리 α{OldGlassAlpha}: {trace} = 창 알파 {alpha:F4} " +
                $"(뒤 창 비침 {(1f - alpha) * 100f:F1}%).");

            Assert.Less(alpha, 0.9f,
                $"{LogPrefix} 네거티브 컨트롤이 통과해 버렸습니다(창 알파 {alpha:F4}) — 이 테스트는 " +
                "'유리 알파' 결함을 잡지 못합니다. 측정 방식을 먼저 의심해야 합니다.");

            // "alpha만 0.98로 올리면 되지 않나"에 대한 <b>수치로 된 답</b>. 겹 하나만으로도 4%가 남고,
            // 실제 창에는 그 위에 그림자/보더가 더 겹친다.
            float naive = 0.98f * 0.98f;
            Assert.Less(naive, WindowAlphaProbe.RequiredWindowAlpha,
                "0.98² = 0.9604 — 알파를 올리는 것으로는 비침이 사라지지 않습니다(1만이 안전합니다).");
            Debug.Log($"{LogPrefix} 참고: α0.98 한 겹만으로도 창 알파는 {naive:F4}입니다 " +
                $"(뒤 창 비침 {(1f - naive) * 100f:F1}%) — '알파만 올리기'는 해가 아닙니다.");
        }
    }
}
