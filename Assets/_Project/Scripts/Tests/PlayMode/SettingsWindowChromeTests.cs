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
    /// ★ 설정창의 <b>창 알파</b>와 <b>배포 에셋 불변</b> — 2026-09-01 설정창 신설과 함께 만든 잠금.
    ///
    /// ============================================================================
    /// 왜 새 창마다 이 테스트를 다시 쓰는가
    /// ============================================================================
    /// 2026-08-31에 정보창과 팝오버가 <b>각각 따로</b> 같은 버그를 냈다("창 뒤가 비쳐 보인다").
    /// 원인은 uGUI 기본 셰이더가 알파 채널에도 <c>Blend SrcAlpha OneMinusSrcAlpha</c>를 적용해
    /// <b>반투명 겹을 쌓을수록 창 알파가 내려간다</b>는 것이고(dstA' = srcA² + dstA(1−srcA)),
    /// 이 앱은 투명 오버레이라 그 알파가 곧 <b>유저의 데스크톱이 비치는 정도</b>다.
    /// 부품(<see cref="UiChrome"/>)이 안전해도 <b>새 창이 그 부품을 잘못 쌓으면</b> 같은 버그가 난다 —
    /// 그래서 창마다 자기 몫의 측정이 필요하다(<c>InfoWindowPanelOpacityTests</c>와 같은 방법).
    ///
    /// 측정은 <b>실제로 만들어진 계층</b>을 그리기 순서대로 걸으며 각 겹의 색 알파 × 스프라이트 중앙
    /// 텍셀 알파를 곱해 합성한다. 색 상수를 다시 읽지 않으므로 누가 어떤 방법으로 겹을 더하든 잡힌다.
    /// </summary>
    public sealed class SettingsWindowChromeTests
    {
        private const string LogPrefix = "[설정창크롬-TEST]";
        private const float RequiredWindowAlpha = 0.999f;

        private SettingsWindow _window;
        private StickmanAgent _agent;
        private StickConfig _config;
        private StickmanInkColor _serializedInkBefore;
        private int _serializedFontSizeBefore;

        private IEnumerator LoadAndOpen()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            _config = _agent.Config;
            Assert.IsNotNull(_config, $"{LogPrefix} StickmanAgent에 StickConfig가 배선돼 있지 않습니다.");
            _serializedInkBefore = _config.inkColor;
            _serializedFontSizeBefore = _config.dialogueFontSize;

            _window = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에서 SettingsWindow를 찾지 못했습니다 — " +
                "SceneBootstrapper.EnsurePrefabComponents가 프리팹에 붙였는지 확인하세요.");

            _window.Open("테스트");
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close("테스트 정리");
            if (_config != null) _config.ClearRuntimeInkColor();
            AppSettingsModel.ResetForTesting();
            // 정적 상태는 씬 재로드에도 살아남아 <b>뒤에 오는 스위트</b>로 샌다(위 SettingsCharacterScale
            // 스위트의 TearDown 문서 참고).
            CharacterScaleController.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            CharacterAppearanceModel.ResetForTesting();
            // ★ 저장 파일까지 되돌린다. 이 테스트들은 <b>제품 경로</b>로 값을 바꾸므로 설정창이 실제로
            //   CharacterSaveStore.Save()를 부른다. PlayMode 저장 경로는 임시 폴더로 리디렉션돼 있지만
            //   그 파일은 <b>실행과 실행 사이에 남는다</b> — 그대로 두면 다음 실행의 씬 로드가 이 값을
            //   복원해 "씬 로드 직후인데 런타임 배율이 이미 설정돼 있다"로 다른 스위트를 깨뜨린다
            //   (실제로 DeployedConfigAssetImmutabilityTests가 그렇게 실패했다).
            //   모델을 비운 <b>뒤에</b> 한 번 더 저장해 파일을 기본값 상태로 되돌린다.
            CharacterSaveStore.Save();
            _window = null;
            _agent = null;
            _config = null;
        }

        // ==================== 측정 도구(InfoWindowPanelOpacityTests와 같은 방법) ====================

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
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }

        private static float CoverageAtCenter(Graphic g)
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

        private static float SimulateWindowAlphaOverPanel(RectTransform panel, out string trace)
        {
            Rect panelRect = ScreenRectOf(panel);
            var order = new List<Graphic>(128);
            CollectDrawOrder(panel, order);

            const float Eps = 0.5f;
            float fbA = 0f;
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

        private static RectTransform FindPanel()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (c == null || c.name != "SettingsCanvas") continue;
                for (int k = 0; k < c.transform.childCount; k++)
                {
                    var rt = c.transform.GetChild(k) as RectTransform;
                    if (rt != null && rt.name == "SettingsPanel") return rt;
                }
            }
            Assert.Fail($"{LogPrefix} SettingsCanvas 아래에서 SettingsPanel을 찾지 못했습니다.");
            return null;
        }

        // ============================================================================
        // (1) 창 알파 = 1 — 유저의 다른 창이 1%도 비치지 않는다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창의_창_알파가_1이라_뒤_창이_비치지_않는다()
        {
            yield return LoadAndOpen();

            RectTransform panel = FindPanel();

            var containerGraphic = panel.GetComponent<Graphic>();
            Assert.IsNull(containerGraphic,
                $"{LogPrefix} 패널 컨테이너에 Graphic이 붙어 있습니다 — uGUI는 부모를 자식보다 먼저 그리므로 " +
                "이 순간 그림자가 패널 <위>로 올라가고 창 알파가 무너집니다(2026-08-31 정보창/팝오버가 " +
                "각각 겪은 그 회귀). 컨테이너는 그림 없는 RectTransform이어야 합니다.");

            float alpha = SimulateWindowAlphaOverPanel(panel, out string trace);
            Debug.Log($"{LogPrefix} 겹 합성 결과: {trace} = 창 알파 {alpha:F4} " +
                $"(뒤 창 비침 {(1f - alpha) * 100f:F1}%).");

            Assert.GreaterOrEqual(alpha, RequiredWindowAlpha,
                $"{LogPrefix} 설정창의 창 알파가 {alpha:F4}입니다 — 유저의 데스크톱이 " +
                $"{(1f - alpha) * 100f:F1}% 비쳐 보입니다. 겹 합성 경로: {trace}");
        }

        // ============================================================================
        // (2) 창 안의 어떤 겹도 반투명이 아니다 (부품 단위 잠금)
        // ============================================================================
        //
        // (1)은 "패널 <전체>를 덮는" 겹만 본다. 카드/스위치/트랙처럼 일부만 덮는 겹이 반투명이면 그
        // 자리에서만 창 알파가 내려가고(=그 부분만 뒤가 비친다) (1)은 통과한다. 설정창은 그런 작은
        // 부품이 수십 개라 여기서 전수로 잠근다 — 완전 투명(α=0, 히트 영역)만 예외다.

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창_안의_모든_그래픽은_불투명이거나_완전투명이다()
        {
            yield return LoadAndOpen();

            RectTransform panel = FindPanel();
            var order = new List<Graphic>(256);
            CollectDrawOrder(panel, order);

            var offenders = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                Graphic g = order[i];
                if (g is Text) continue;                 // 글자는 획 가장자리에서만 알파가 낀다(구조상 불가피).
                // 그림자는 <b>일부러</b> 반투명이다 — 창 <b>바깥</b>에 깔려 데스크톱을 어둡게 물들이는 것이
                // 그 목적이고, 창 본체(α=1)가 그 위에 그려지므로 창 안쪽 알파에는 영향이 없다
                // (AddOpaquePanel의 겹 순서: 그림자 → 본체 → 보더).
                if (g.name.Contains("Shadow")) continue;
                float a = g.color.a;
                if (a <= 0.001f) continue;               // 완전 투명 히트 영역 — 프레임버퍼를 건드리지 않는다.
                if (a >= 0.999f) continue;
                offenders.Add($"{Path(g.transform)} α{a:F2}");
            }

            Debug.Log($"{LogPrefix} 그래픽 {order.Count}개 검사 — 반투명 {offenders.Count}개.");

            Assert.IsTrue(offenders.Count == 0,
                $"{LogPrefix} 반투명 겹이 발견됐습니다. 이 앱의 창 뒤에는 유저의 다른 창이 있으므로 " +
                "그 자리만 뒤가 비칩니다. UiChrome.Flatten(반투명토큰, 밑에깔린불투명색)으로 미리 합성하세요.\n" +
                string.Join("\n", offenders));
        }

        private static string Path(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            Transform p = t.parent;
            while (p != null && p.name != "SettingsPanel")
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }

        // ============================================================================
        // (3) 배포 에셋 불변 — 설정창의 어떤 조작도 .asset을 오염시키지 않는다
        // ============================================================================
        //
        // DeployedConfigAssetImmutabilityTests가 잠근 계약을 <b>설정창 경로에서</b> 다시 확인한다.
        // 그쪽은 "코드가 그렇게 쓰지 않는다"를 보고, 이쪽은 "실제 클릭이 그렇게 하지 않는다"를 본다.

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 잉크색_스와치_클릭이_배포_에셋의_직렬화_필드를_건드리지_않는다()
        {
            yield return LoadAndOpen();

            _window.FeedClickForTests(_window.TabScreenRect(SettingsWindow.Tab.Character).center);
            yield return null;

            // 배포 기본값의 반대색을 누른다(어느 쪽이 구워져 있어도 실제 변화가 일어나게).
            bool wantWhite = _serializedInkBefore != StickmanInkColor.White;
            Rect swatch = _window.InkSwatchScreenRect(wantWhite ? 1 : 0);
            Assert.Greater(swatch.width, 1f, $"{LogPrefix} 잉크 스와치의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(swatch.center);
            yield return null;

            StickmanInkColor expected = wantWhite ? StickmanInkColor.White : StickmanInkColor.Black;
            Debug.Log($"{LogPrefix} 스와치 클릭 후 — 직렬화 inkColor={_config.inkColor}(기대 {_serializedInkBefore} 그대로), " +
                $"실효 ResolveInkPreset()={_config.ResolveInkPreset()}, " +
                $"저장 모델={(CharacterAppearanceModel.HasInkColor ? CharacterAppearanceModel.InkColor.ToString() : "없음")}.");

            Assert.AreEqual(_serializedInkBefore, _config.inkColor,
                $"{LogPrefix} ★ 설정창이 배포 에셋(DefaultStickConfig.asset)의 직렬화 필드 inkColor를 " +
                $"{_serializedInkBefore} -> {_config.inkColor}로 바꿨습니다. 에디터가 이 애셋을 저장하는 순간 " +
                "그 색이 전 사용자의 출하 기본값이 됩니다(2026-08-31 R5와 같은 실패 모드).");
            Assert.AreEqual(expected, _config.ResolveInkPreset(),
                $"{LogPrefix} 실효 잉크색이 따라오지 않았습니다 — 위 단언이 '아무 일도 안 일어나서' 통과한 것입니다.");
            Assert.IsTrue(CharacterAppearanceModel.HasInkColor,
                $"{LogPrefix} 사용자의 선택이 저장 모델에 남지 않았습니다 — 앱을 껐다 켜면 초기화됩니다.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 말풍선_슬라이더가_배포_에셋의_직렬화_필드를_건드리지_않는다()
        {
            yield return LoadAndOpen();

            _window.FeedClickForTests(_window.TabScreenRect(SettingsWindow.Tab.Character).center);
            yield return null;

            int effectiveBefore = AppSettingsModel.ResolveDialogueFontSize(_config);
            Rect plus = _window.DialogueFontSizePlusScreenRect;
            Assert.Greater(plus.width, 1f, $"{LogPrefix} 글자 크기 [+] 버튼의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(plus.center);
            yield return null;

            int effectiveAfter = AppSettingsModel.ResolveDialogueFontSize(_config);
            Debug.Log($"{LogPrefix} [+] 클릭 후 — 직렬화 dialogueFontSize={_config.dialogueFontSize}" +
                $"(기대 {_serializedFontSizeBefore} 그대로), 실효={effectiveBefore} -> {effectiveAfter}.");

            Assert.AreEqual(_serializedFontSizeBefore, _config.dialogueFontSize,
                $"{LogPrefix} ★ 설정창이 배포 에셋의 직렬화 필드 dialogueFontSize를 바꿨습니다 — " +
                "characterScale/inkColor와 <b>정확히 같은</b> 실패 모드입니다. " +
                "사용자가 고른 값은 AppSettingsModel에 넣으세요.");
            Assert.AreEqual(effectiveBefore + 1, effectiveAfter,
                $"{LogPrefix} 실효 글자 크기가 한 칸 오르지 않았습니다 — 위 단언이 '아무 일도 안 일어나서' " +
                "통과한 것입니다.");
        }

        // ============================================================================
        // (3-b) ★ 42-11 판정 G — 켜져 있어야만 뜻을 갖는 행이 활성인 채로 무효이면 안 된다
        // ============================================================================

        /// <summary>
        /// ★★ <c>말풍선 표시</c>를 끄면 대사가 그려지지 않는데 <c>말풍선 글자 크기</c>·
        /// <c>대사 표시 시간</c>·<c>잡담 빈도</c> 세 행이 <b>그대로 활성</b>이었다.
        /// <b>컨트롤 셋이 움직이는데 화면에서 아무 일도 일어나지 않는다</b> — 42절이 고치는 그 병이
        /// 같은 카드 안에 세 배로 있었다(docs/UX_FLOW.md 42-11).
        ///
        /// <para><b>왜 색이 아니라 클릭을 재는가</b>: 회색으로 칠하기만 하고 클릭이 그대로 먹으면
        /// "회색인데 눌리는 행"이 되어 결함이 더 나빠진다. 그래서 <b>실제 클릭 경로</b>로 잰다.</para>
        ///
        /// <para><b>네거티브 컨트롤</b>: 토글을 다시 켜면 <b>같은 클릭</b>이 먹어야 한다. 그 짝이 없으면
        /// "그냥 영영 못 누르게 하기"라는 오답이 통과한다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 말풍선을_끄면_아래_세_행이_함께_비활성이_된다()
        {
            yield return LoadAndOpen();

            _window.FeedClickForTests(_window.TabScreenRect(SettingsWindow.Tab.Character).center);
            yield return null;

            Assert.IsTrue(_window.SpeechRowsEnabledForTests,
                $"{LogPrefix} 말풍선이 켜져 있는데 세 행이 이미 비활성입니다(사전 조건).");

            Rect toggle = _window.DialogueBubbleToggleScreenRect;
            Assert.Greater(toggle.width, 1f, $"{LogPrefix} 말풍선 표시 토글의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(toggle.center);
            yield return null;

            Assert.IsFalse(AppSettingsModel.ResolveDialogueBubbleEnabled(_config),
                $"{LogPrefix} 토글 클릭이 먹지 않았습니다 — 아래 단언들이 '아무 일도 안 일어나서' " +
                "통과하게 됩니다.");
            Assert.IsFalse(_window.SpeechRowsEnabledForTests,
                $"{LogPrefix} 말풍선을 껐는데 아래 세 행이 여전히 활성입니다 — 만져도 화면이 바뀌지 " +
                "않는 컨트롤 셋입니다(42-11 판정 G).");

            // ① 슬라이더 [+]가 먹지 않는다.
            int fontBefore = AppSettingsModel.ResolveDialogueFontSize(_config);
            _window.FeedClickForTests(_window.DialogueFontSizePlusScreenRect.center);
            yield return null;
            Assert.AreEqual(fontBefore, AppSettingsModel.ResolveDialogueFontSize(_config),
                $"{LogPrefix} 비활성인 글자 크기 슬라이더의 [+]가 그대로 먹었습니다 — '회색인데 눌리는' " +
                "행은 결함을 고친 것이 아니라 더 나쁘게 만든 것입니다.");

            // ② 세그먼트 칸도 먹지 않는다.
            DialogueVisibleLength lengthBefore = AppSettingsModel.DialogueVisibleLength;
            Rect chip = _window.DialogueVisibleLengthSegmentScreenRect(
                (int)DialogueVisibleLength.VeryLong);
            Assert.Greater(chip.width, 1f, $"{LogPrefix} `아주 길게` 칸의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(chip.center);
            yield return null;
            Assert.AreEqual(lengthBefore, AppSettingsModel.DialogueVisibleLength,
                $"{LogPrefix} 비활성인 `대사 표시 시간` 세그먼트가 그대로 먹었습니다.");

            // ③ 왜 못 쓰는지 화면이 말한다.
            Text reason = FindRowCaption("Row_character.visibleLength");
            Assert.IsNotNull(reason, $"{LogPrefix} `대사 표시 시간` 행의 캡션 줄을 찾지 못했습니다.");
            StringAssert.Contains("말풍선 표시", reason.text,
                $"{LogPrefix} 비활성 사유가 화면에 없습니다(\"{reason.text}\") — 유저는 왜 못 만지는지 " +
                "알 길이 없습니다.");

            // ★ 네거티브 컨트롤 — 다시 켜면 같은 클릭이 먹는다.
            //   같은 컨트롤의 연타는 창이 한 번으로 접으므로(ActionDedupSeconds) 그만큼 벽시계로
            //   기다린 뒤 누른다. 숫자를 베끼지 않고 그 상수를 참조한다.
            yield return new WaitForSecondsRealtime(SettingsWindow.ActionDedupSeconds + 0.05f);
            _window.FeedClickForTests(_window.DialogueBubbleToggleScreenRect.center);
            yield return null;
            Assert.IsTrue(_window.SpeechRowsEnabledForTests,
                $"{LogPrefix} 말풍선을 다시 켰는데 세 행이 비활성인 채로 남았습니다 — 비활성이 " +
                "영구화됐습니다.");

            _window.FeedClickForTests(_window.DialogueFontSizePlusScreenRect.center);
            yield return null;
            Assert.AreEqual(fontBefore + 1, AppSettingsModel.ResolveDialogueFontSize(_config),
                $"{LogPrefix} 말풍선을 다시 켰는데도 [+]가 안 먹습니다 — 비활성이 영구화됐습니다.");

            Debug.Log($"{LogPrefix} 42-11 G 확인 — 말풍선 OFF에서 슬라이더/세그먼트 클릭이 모두 막히고, " +
                      "사유가 화면에 있으며, 다시 켜면 같은 클릭이 복귀합니다.");
        }

        private static Text FindRowCaption(string rowName)
        {
            GameObject canvas = GameObject.Find("SettingsCanvas");
            Assert.IsNotNull(canvas, $"{LogPrefix} 씬에서 SettingsCanvas를 찾지 못했습니다.");
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != rowName) continue;
                Transform cap = t.Find("Caption");
                return cap != null ? cap.GetComponent<Text>() : null;
            }
            return null;
        }

        // ============================================================================
        // (4) 비침해 — 닫으면 차단막이 반드시 꺼진다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 창을_닫으면_클릭관통_차단막도_같이_꺼진다()
        {
            yield return LoadAndOpen();

            Assert.IsTrue(_window.IsClickBlockerEnabled,
                $"{LogPrefix} 창이 열려 있는데 차단막이 꺼져 있습니다 — 창 위 클릭이 뒤의 앱으로 샙니다.");

            _window.Close("테스트");
            yield return null;

            Assert.IsFalse(_window.IsCanvasActive, $"{LogPrefix} 닫았는데 캔버스가 살아 있습니다.");
            Assert.IsFalse(_window.IsClickBlockerEnabled,
                $"{LogPrefix} ★ 창이 사라졌는데 차단막이 남았습니다 — 그 화면 영역의 클릭관통이 영영 " +
                "해제된 채 남습니다(비침해 원칙 2 위반, 팝오버에서 실제로 났던 사고).");
        }

        // ============================================================================
        // (5) 배타 모달 — 설정창을 열면 정보창이 닫힌다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창을_열면_정보창이_닫힌다()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            _window = Object.FindFirstObjectByType<SettingsWindow>();
            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            _config = _agent != null ? _agent.Config : null;
            Assert.IsNotNull(info, $"{LogPrefix} 정보창을 찾지 못했습니다.");
            Assert.IsNotNull(_window, $"{LogPrefix} 설정창을 찾지 못했습니다.");

            info.Open("테스트");
            yield return null;
            Assert.IsTrue(info.IsOpen, $"{LogPrefix} 사전 조건: 정보창이 열리지 않았습니다.");

            _window.Open("테스트");
            yield return null;

            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 설정창이 열리지 않았습니다.");
            Assert.IsFalse(info.IsOpen,
                $"{LogPrefix} 설정창을 열었는데 정보창이 그대로 있습니다 — 두 개의 큰 모달이 겹칩니다" +
                "(35-1-7의 상호 배타 규칙).");
            Assert.IsFalse(info.IsClickBlockerEnabled,
                $"{LogPrefix} 정보창은 닫혔는데 그 차단막이 남았습니다(비침해).");
        }

        // ==================== 자르는 선이 글자 한가운데를 지나지 않는다 (2026-09-02) ====================

        /// <summary>
        /// ★ [캐릭터] 탭 첫 화면 맨 아래에서 <c>대사 표시 시간</c>이 <b>한글 높이의 약 60% 지점</b>에서
        /// 잘려 <c>ㄷㅐㅅㅏ ㅍㅅㅣ ㅅㅣ간</c>처럼 보였다. 사용자가 읽는 뜻은 "밑에 더 있다"가 아니라
        /// <b>"글꼴이 깨졌다"</b>였다. 정보창 캐러셀이 가로 방향에서 이미 배운 교훈
        /// (<i>"자르는 선의 위치가 틀렸다"</i>)을 세로에 적용한 것이 이 페이드다.
        ///
        /// <para><b>이 검사가 잡는 진짜 실패</b>는 "상수가 8인가"가 아니라 <b>그 값이 마스크까지
        /// 배선됐는가</b>다 — 뷰포트를 다시 만들면서 <c>softness</c> 대입을 빠뜨리는 것이 실제로
        /// 일어나는 회귀이고, 그때 화면은 <b>조용히</b> 옛날로 돌아간다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClippedContentFadesInsteadOfSlicingGlyphsInHalf()
        {
            yield return LoadAndOpen();

            GameObject canvas = GameObject.Find("SettingsCanvas");
            Assert.IsNotNull(canvas, $"{LogPrefix} SettingsCanvas를 찾지 못했습니다.");

            RectMask2D viewportMask = null;
            foreach (RectMask2D m in canvas.GetComponentsInChildren<RectMask2D>(true))
            {
                if (m.gameObject.name == "Viewport") { viewportMask = m; break; }
            }
            Assert.IsNotNull(viewportMask,
                $"{LogPrefix} 본문 뷰포트의 RectMask2D를 찾지 못했습니다 — 이름이 바뀌었다면 이 " +
                "테스트도 함께 고쳐야 합니다.");

            Assert.AreEqual(SettingsWindow.ClipFadePoints, viewportMask.softness.y,
                $"{LogPrefix} 뷰포트 마스크의 세로 페이드가 {viewportMask.softness.y}pt입니다 — " +
                $"프로덕션 상수({SettingsWindow.ClipFadePoints}pt)가 마스크까지 배선되지 않았습니다. " +
                "0이면 자르는 선이 다시 글자 한가운데를 지납니다.");

            // 페이드가 <b>글자를 녹일 만큼</b> 두꺼운가 — 캡션 글자 높이의 절반은 넘어야
            // "반쯤 잘린 글자"가 온전한 획으로 남지 않는다. 숫자를 베끼지 않고 폰트 상수를 참조한다.
            Assert.GreaterOrEqual(SettingsWindow.ClipFadePoints, UiChrome.FontCaption / 2,
                $"{LogPrefix} 페이드 {SettingsWindow.ClipFadePoints}pt가 캡션 글자" +
                $"({UiChrome.FontCaption}pt)의 절반보다 얇습니다 — 잘린 글자가 또렷하게 남습니다.");

            // 가로는 건드리지 않는다 — 카드 좌우는 원래 여백 안에 있고, 흐리게 만들 이유가 없다.
            Assert.AreEqual(0, viewportMask.softness.x,
                $"{LogPrefix} 가로 페이드가 켜졌습니다 — 좌우는 잘리지 않으므로 흐려질 이유가 없습니다.");
        }
    }
}
