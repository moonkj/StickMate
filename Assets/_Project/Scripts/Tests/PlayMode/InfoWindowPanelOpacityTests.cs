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
    /// ★ 2026-08-31 사용자 신고(스크린샷) — <b>"창이 여러 개로 겹쳐 보인다"</b>.
    /// 어두운 정보창 뒤로 데스크톱 날씨 위젯의 파란 그라데이션과 <c>24°</c>가 그대로 읽혔다.
    ///
    /// ============================================================================
    /// 무엇이 깨져 있었나 (회귀 — 커밋 b6755f4 "다크 톤 리스킨"이 심었다)
    /// ============================================================================
    /// 이 앱의 창은 전체화면 <b>투명 오버레이</b>다. 카메라가 배경을 알파 0으로 지우므로
    /// <b>프레임버퍼 알파가 곧 OS 합성 마스크</b>다. 그리고 uGUI 기본 셰이더의
    /// <c>Blend SrcAlpha OneMinusSrcAlpha</c>는 <b>알파 채널에도 그대로</b> 적용되어
    ///
    ///     dstA' = srcA² + dstA(1 − srcA)
    ///
    /// 가 된다 — 즉 <b>반투명 겹을 쌓을수록 창 알파가 내려간다</b>. 정보창은 두 가지를 동시에
    /// 어기고 있었다:
    ///
    ///   ① 바탕 <c>PanelSurface</c>의 알파가 0.96(→ 실효 0.9216)
    ///   ② 그림자 2겹(검정 α0.55 / α0.28)이 <b>패널 Image의 자식</b>이라 본체 <b>위</b>에 얹혔다.
    ///      (uGUI는 부모 Graphic을 자식보다 먼저 그린다. <c>SetAsFirstSibling()</c>은 형제 순서만
    ///      정하므로 "부모 위"를 절대 못 벗어난다.)
    ///
    /// 결과 창 알파 = 0.9216 → 0.7172 → <b>0.5948</b>. 데스크톱이 <b>40.5%</b> 비쳤다.
    /// 밝은 팔레트 시절에도 비침 자체는 있었지만(약 16%) 밝은 표면이 뒤 화소를 가려 체감 12%였고,
    /// 팔레트가 어두워지자 같은 비침이 체감 549%로 증폭됐다 — <b>그래서 리스킨 라운드에서 못 잡았다.</b>
    ///
    /// ============================================================================
    /// 이 파일이 지키는 절대 조건
    /// ============================================================================
    ///  ① 큰 창 바탕 토큰 <see cref="UiChrome.PanelSurface"/>의 알파는 <b>정확히 1</b>이다.
    ///  ② 정보창 패널 컨테이너에는 <b>Graphic이 없다</b>(있으면 본체가 그 위에 그려져 겹 순서가 뒤집힌다).
    ///  ③ 패널 전체를 덮는 겹만 모아 위 공식으로 합성한 <b>창 알파가 1</b>이다
    ///     (= 뒤 창이 단 1%도 비치지 않는다).
    ///  ④ (네거티브 컨트롤) 옛 구조를 그대로 다시 만들면 ③이 <b>실제로 깨진다</b> —
    ///     이 테스트가 이 버그를 진짜로 잡는다는 증명이다.
    ///  ⑤ <b>(삭제됨, 2026-09-02)</b> 여기에 "그림자를 Graphic 부모에 달면 Error" 구조적 잠금이 있었다.
    ///     사용자 지시로 UI 그림자를 전부 없애면서 <see cref="UiChrome"/>의 그림자 API 자체가 사라졌고,
    ///     함께 지웠다. 같은 실수(반투명 겹을 본체 위에 얹기)는 ③의 <b>실측</b>이 여전히 잡는다 —
    ///     구조적 잠금보다 늦게 잡지만, 잡는 것은 같은 결함이다.
    ///
    /// ============================================================================
    /// 측정 방식 — 프로덕션 공식을 베끼지 않는다
    /// ============================================================================
    /// 색 상수를 다시 읽지 않고 <b>실제로 만들어진 UI 계층</b>을 그리기 순서대로 걸으며,
    /// 각 <see cref="Graphic"/>의 <c>color.a</c>와 <b>스프라이트 중앙 텍셀의 알파</b>를 곱해
    /// "이 겹이 패널 한가운데를 얼마나 덮는가"를 실측한다. 그래서 누가 어떤 방법으로 겹을 더하든
    /// "창 뒤가 비치는가"만 본다(테두리 스프라이트는 중앙이 비어 있어 자동으로 0으로 걸러진다).
    /// </summary>
    public sealed class InfoWindowPanelOpacityTests
    {
        private const string LogPrefix = "[창알파-TEST]";

        /// <summary>이 값 미만이면 사람 눈에 "뒤가 비친다"로 읽힌다. 어두운 표면에서는 1%도 보이므로
        /// 여유를 두지 않는다(부동소수 오차만 허용).</summary>
        private const float RequiredWindowAlpha = 0.999f;

        private CharacterInfoWindow _window;
        private GameObject _syntheticRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_window != null) _window.Close("테스트 정리");
            _window = null;
            if (_syntheticRoot != null) Object.DestroyImmediate(_syntheticRoot);
            _syntheticRoot = null;
            yield return null;
        }

        private static T ExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length, $"씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            return found[0];
        }

        private IEnumerator SetUpOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = ExactlyOne<CharacterInfoWindow>();
            _window.Open("테스트");
            yield return null;
            yield return null;
        }

        /// <summary>정보창 캔버스에서 패널 컨테이너를 찾는다.
        /// <para>이름으로 찾는 것이 <b>여기서는</b> 안전하다 — 이 캔버스는 씬 루트의 독립 오브젝트이고
        /// (CharacterInfoWindow.BuildUi의 "캐릭터 자손으로 두지 않는다" 문단 참고) 이름을 바꾸는 순간
        /// 이 테스트가 즉시 실패해 알려 준다. 반대로 "캔버스의 첫 자식"으로 찾으면 나중에 겹이 하나
        /// 추가되는 것만으로 조용히 엉뚱한 사각형을 재게 된다.</para></summary>
        private static RectTransform FindPanel(CharacterInfoWindow window)
        {
            Assert.Greater(window.PanelSizePoints.x, 1f,
                $"{LogPrefix} 창이 열려 있지 않습니다(패널 크기 {window.PanelSizePoints}).");

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (c == null || c.name != "CharacterInfoCanvas") continue;
                for (int k = 0; k < c.transform.childCount; k++)
                {
                    var rt = c.transform.GetChild(k) as RectTransform;
                    if (rt != null && rt.name == "InfoPanel") return rt;
                }
            }
            Assert.Fail($"{LogPrefix} CharacterInfoCanvas 아래에서 InfoPanel을 찾지 못했습니다.");
            return null;
        }

        // ==================== 측정 도구 ====================

        /// <summary>그리기 순서 — uGUI는 <b>부모 Graphic을 먼저</b>, 그 다음 자식들을 형제 순서대로 그린다.</summary>
        private static void CollectDrawOrder(Transform t, List<Graphic> into)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return;
            var g = t.GetComponent<Graphic>();
            if (g != null && g.enabled) into.Add(g);
            for (int i = 0; i < t.childCount; i++) CollectDrawOrder(t.GetChild(i), into);
        }

        private static Rect ScreenRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);   // ScreenSpaceOverlay 캔버스에서는 월드 = 화면 픽셀.
            return Rect.MinMaxRect(
                Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }

        /// <summary>
        /// 이 겹이 <b>패널 한가운데</b>를 얼마나 덮는가(0~1).
        ///
        /// <para>스프라이트 중앙 텍셀을 읽는 것이 핵심이다. <see cref="UiChrome.RoundedOutline"/>로 만든
        /// 테두리는 사각형은 패널 전체를 덮지만 <b>가운데가 비어 있어</b> 실제로는 아무것도 덮지 않는다
        /// — 색 상수만 봤다면 테두리를 오탐했을 것이다. 9-슬라이스든 단순 늘리기든 사각형 중앙은
        /// 스프라이트 중앙 텍셀로 매핑되므로 이 한 번의 샘플이 정확하다.</para>
        ///
        /// <para><see cref="Text"/>는 0으로 본다 — 글자는 사각형을 가득 채우지 않고 획 가장자리에서만
        /// 알파가 낀다(그 1px 비침은 구조상 불가피하며 이 테스트의 대상이 아니다).</para>
        /// </summary>
        private static float CoverageAtCenter(Graphic g)
        {
            if (g is Text) return 0f;
            if (g is Image img)
            {
                if (img.sprite == null) return g.color.a;   // 스프라이트 없는 Image = 꽉 찬 사각형.
                Texture2D tex = img.sprite.texture;
                if (tex == null || !tex.isReadable) return g.color.a;
                Rect r = img.sprite.textureRect;
                int x = Mathf.Clamp(Mathf.FloorToInt(r.center.x), 0, tex.width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(r.center.y), 0, tex.height - 1);
                return g.color.a * tex.GetPixel(x, y).a;
            }
            return g.color.a;   // RawImage 등은 꽉 찬 사각형으로 본다.
        }

        /// <summary>
        /// 패널 사각형을 <b>통째로 덮는</b> 겹만 모아 실제 블렌드 공식으로 합성한 창 알파.
        /// 카드/글자/구분선처럼 패널 일부만 덮는 것은 제외한다 — 그것들은 "창 전체가 비친다"는
        /// 이번 증상의 원인이 될 수 없고, 넣으면 배치가 바뀔 때마다 흔들리는 테스트가 된다.
        /// </summary>
        private static float SimulateWindowAlphaOverPanel(RectTransform panel, out string trace)
        {
            Rect panelRect = ScreenRectOf(panel);
            var order = new List<Graphic>(64);
            CollectDrawOrder(panel, order);

            const float Eps = 0.5f;   // 화면 픽셀.
            float fbA = 0f;           // 카메라가 알파 0으로 지운 상태에서 출발한다.
            var log = new System.Text.StringBuilder();
            log.Append("알파 0(투명 배경)");

            for (int i = 0; i < order.Count; i++)
            {
                Graphic g = order[i];
                Rect r = ScreenRectOf(g.rectTransform);
                bool coversPanel = r.xMin <= panelRect.xMin + Eps && r.xMax >= panelRect.xMax - Eps
                                && r.yMin <= panelRect.yMin + Eps && r.yMax >= panelRect.yMax - Eps;
                if (!coversPanel) continue;

                float srcA = CoverageAtCenter(g);
                if (srcA <= 0.001f) continue;

                fbA = srcA * srcA + fbA * (1f - srcA);
                log.Append($" → [{g.name} α{srcA:F2}] {fbA:F4}");
            }

            trace = log.ToString();
            return fbA;
        }

        // ============================================================================
        // (1) 토큰 잠금 — 팔레트를 다시 만지는 사람이 여기서 먼저 멈춘다
        // ============================================================================
        [Test]
        public void BigWindowSurfaceTokenIsFullyOpaque()
        {
            Assert.AreEqual(1f, UiChrome.PanelSurface.a, 0.0001f,
                $"{LogPrefix} UiChrome.PanelSurface의 알파가 {UiChrome.PanelSurface.a:F3}입니다 — 1이어야 합니다. " +
                "이 앱의 창 뒤에는 우리 콘텐츠가 아니라 <유저의 다른 창>이 있습니다. " +
                "알파 유리는 이 아키텍처에서 성립하지 않습니다(UiChrome 파일 머리 '알파 채널의 법칙' 참고).");
        }

        // ============================================================================
        // (2) 핵심 — 실제로 만들어진 정보창의 창 알파가 1이다
        // ============================================================================
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator InfoWindowPanelIsFullyOpaqueSoNoOtherWindowShowsThrough()
        {
            yield return SetUpOpenWindow();

            RectTransform panel = FindPanel(_window);

            // ② 컨테이너에 Graphic이 있으면 그림자가 본체 위로 올라간다 — 구조 자체를 잠근다.
            var containerGraphic = panel.GetComponent<Graphic>();
            Assert.IsNull(containerGraphic,
                $"{LogPrefix} 패널 컨테이너에 {(containerGraphic != null ? containerGraphic.GetType().Name : "?")}이(가) " +
                "붙어 있습니다 — uGUI는 부모를 자식보다 먼저 그리므로 이 순간 그림자가 패널 <위>로 올라가고 " +
                "창 알파가 무너집니다. 컨테이너는 그림 없는 RectTransform이어야 합니다.");

            float alpha = SimulateWindowAlphaOverPanel(panel, out string trace);
            Debug.Log($"{LogPrefix} 패널 전체를 덮는 겹의 합성 결과: {trace} = 창 알파 {alpha:F4} " +
                $"(뒤 창 비침 {(1f - alpha) * 100f:F1}%).");

            Assert.GreaterOrEqual(alpha, RequiredWindowAlpha,
                $"{LogPrefix} 정보창의 창 알파가 {alpha:F4}입니다 — 유저의 데스크톱이 " +
                $"{(1f - alpha) * 100f:F1}% 비쳐 보입니다(사용자 신고 '창이 여러 개로 겹쳐 보임'). " +
                $"겹 합성 경로: {trace}");
        }

        // ============================================================================
        // (3) 네거티브 컨트롤 — 옛 구조를 그대로 재현하면 (2)가 실제로 깨진다
        // ============================================================================
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator OldStructureWithShadowOnTopOfTheBodyIsActuallyDetected()
        {
            yield return SetUpOpenWindow();

            // 커밋 b6755f4 시점의 구조를 손으로 다시 만든다. ★ 2026-09-02에 UiChrome의 그림자 API가
            // 통째로 삭제됐으므로, 이 컨트롤이 재현하는 <b>옛 계층</b>은 이제 여기 로컬 코드가 유일하다.
            // (이 파일이 잡으려는 결함은 "그림자"가 아니라 <b>반투명 겹이 본체 위에 오는 구조</b>다.)
            _syntheticRoot = new GameObject("SyntheticOldPanelCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _syntheticRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            Image bodyImage = UiChrome.AddSurface(_syntheticRoot.transform, "OldInfoPanel",
                new Color(0.078f, 0.090f, 0.110f, 0.96f), UiChrome.RadiusPanel);
            RectTransform oldPanel = bodyImage.rectTransform;
            oldPanel.anchorMin = oldPanel.anchorMax = oldPanel.pivot = new Vector2(0.5f, 0.5f);
            oldPanel.anchoredPosition = Vector2.zero;
            oldPanel.sizeDelta = new Vector2(400f, 300f);

            AddOldShadowLayer(oldPanel, "OldShadowKey", 18f, new Vector2(0f, -18f), 0.55f);
            AddOldShadowLayer(oldPanel, "OldShadowAmbient", 43.2f, new Vector2(0f, -41.4f), 0.28f);
            UiChrome.AddOutline(oldPanel, "OldOutline", UiChrome.PanelBorder, UiChrome.RadiusPanel);
            yield return null;

            float alpha = SimulateWindowAlphaOverPanel(oldPanel, out string trace);
            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 옛 구조 재현: {trace} = 창 알파 {alpha:F4} " +
                $"(뒤 창 비침 {(1f - alpha) * 100f:F1}%).");

            Assert.Less(alpha, 0.7f,
                $"{LogPrefix} 네거티브 컨트롤이 통과해 버렸습니다(창 알파 {alpha:F4}) — 이 테스트는 " +
                "옛 결함을 잡지 못합니다. 측정 방식(겹 수집/커버리지 판정)을 먼저 의심해야 합니다.");
        }

        /// <summary>옛 <c>AddShadowLayer</c>와 <b>같은 기하/색</b>으로 그림자 한 겹을 부모의 자식으로 붙인다.</summary>
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

        // ★ (4) 구조적 잠금(UiChrome.AddShadow의 Error 가드)은 2026-09-02 그림자 전면 삭제와 함께
        //   사라졌다 — 가드가 지키던 함수가 없다. 파일 머리 ⑤ 참고.
    }
}
